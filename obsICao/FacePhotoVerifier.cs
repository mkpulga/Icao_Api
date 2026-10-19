using System.Collections.Concurrent;
using System.Drawing;
using Innovatrics.IFace;
using obsDLL_v3.FaceRecognition;

namespace obsIcaoApi
{
    // Runs the verifyPhoto check with the same steps and settings as IcaoFace.verifyIcao in
    // obsDLL_v3, but on a fixed pool of IFace FaceHandlers instead of one shared IcaoFace.
    //
    // IcaoFace.verifyIcao is not safe for concurrent use: every call replaces the instance's
    // FaceHandler and Pen while other requests are still using them (the cause of "Object is
    // currently in use elsewhere." under load). Every call also creates a new FaceHandler,
    // which takes ~140 ms and ~74 MB of native memory and is never disposed.
    //
    // Here each FaceHandler is created once and used by one request at a time. Separate
    // handlers ran in parallel without errors and with identical results in profiling. The
    // pool size is also the number of detections that may run at once, which bounds memory
    // (~74 MB per handler) and keeps the CPU from being oversubscribed; further requests
    // wait asynchronously, without holding a thread.
    public sealed class FacePhotoVerifier : IDisposable
    {
        // Same detection settings as IcaoFace.loadICAO.
        private const int MinEyeDistance = 0x19;
        private const int MaxEyeDistance = 400;
        private const int MaxFaces = 10;
        private static readonly FaceFeatureId[] FaceFeatures = (FaceFeatureId[])Enum.GetValues(typeof(FaceFeatureId));

        // VerifyIcao reads only its arguments and the static Parameters flags, so one
        // instance can serve every request.
        private readonly IcaoFace _compliance = new IcaoFace();
        private readonly ConcurrentBag<FaceHandler> _idleHandlers = new ConcurrentBag<FaceHandler>();
        private readonly SemaphoreSlim _slots;

        public int MaxConcurrency { get; }

        public FacePhotoVerifier(int maxConcurrency)
        {
            MaxConcurrency = Math.Max(1, maxConcurrency);
            _slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        }

        // Creates the handlers up front so the first requests do not pay for it.
        public void WarmUp()
        {
            for (int i = _idleHandlers.Count; i < MaxConcurrency; i++)
                _idleHandlers.Add(new FaceHandler());
        }

        public async Task<IcaoFace.ICAOclass> VerifyAsync(Bitmap image, Parameters parameters, CancellationToken cancellationToken)
        {
            await _slots.WaitAsync(cancellationToken);
            FaceHandler? handler = null;
            bool engineFailed = false;
            try
            {
                if (!_idleHandlers.TryTake(out handler))
                    handler = new FaceHandler();

                return Verify(handler, image, parameters, out engineFailed);
            }
            finally
            {
                // A handler that hit an error inside the SDK is replaced rather than reused,
                // so one bad request cannot affect later ones.
                if (handler != null)
                {
                    if (engineFailed)
                        handler.Dispose();
                    else
                        _idleHandlers.Add(handler);
                }
                _slots.Release();
            }
        }

        private IcaoFace.ICAOclass Verify(FaceHandler handler, Bitmap image, Parameters parameters, out bool engineFailed)
        {
            engineFailed = false;
            IcaoFace.ICAOclass icao = new IcaoFace.ICAOclass();

            // Outside the try, as in verifyIcao: a failure here reaches the endpoint and becomes HTTP 400.
            Bitmap bitmap2 = (Bitmap)image.Clone();
            string verify;
            Face[]? faceArray = null;
            try
            {
                using Graphics graphics = Graphics.FromImage(bitmap2);
                using Pen redPen = new Pen(Color.Red, 10f);
                faceArray = handler.DetectFaces(bitmap2, MinEyeDistance, MaxEyeDistance, MaxFaces);
                FaceFeature[] features = faceArray[0].GetFeatures(FaceFeatures);

                // Marks every feature except the last (FaceFeatureId.Unknown), like the original loop.
                for (int index = 0; index <= features.Length - 2; index++)
                    graphics.DrawEllipse(redPen, features[index].Pos.X, features[index].Pos.Y, 5, 5);

                verify = _compliance.VerifyIcao(parameters, faceArray[0]) ? "verified" : "unverified";
            }
            catch (Exception ex)
            {
                // Same as verifyIcao: the error text becomes the message (e.g. no face found).
                verify = ex.Message;
                engineFailed = ex is IFaceException;
            }
            finally
            {
                if (faceArray != null)
                    foreach (Face face in faceArray)
                        face.Dispose();
            }

            icao.message = verify;
            icao.image = bitmap2;
            return icao;
        }

        public void Dispose()
        {
            while (_idleHandlers.TryTake(out FaceHandler? handler))
                handler.Dispose();
            _slots.Dispose();
        }
    }
}
