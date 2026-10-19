using Innovatrics.ISegLib;
using obsDLL_v3.FaceRecognition;
using obsDLL_v3.FingerPrint;

namespace obsIcaoApi
{
    public class Worker : BackgroundService
    {
        private static string licensePath = Path.Combine(AppContext.BaseDirectory, "onejkit.lic");
        private IHost? _webApiHost;
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        ISegLib? finger;
        private FacePhotoVerifier? _faceVerifier;
        private static FingerPrint fingerPrint = new FingerPrint();

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _webApiHost = Host.CreateDefaultBuilder()
                  .ConfigureWebHostDefaults(webBuilder => {

                      webBuilder.ConfigureServices(services =>
                      {
                          services.AddCors(options =>
                          {
                              options.AddDefaultPolicy(policy =>
                              {
                                  policy.AllowAnyOrigin()
                                   .AllowAnyMethod()
                                   .AllowAnyHeader();
                              });
                          });
                      });

                      webBuilder.UseKestrel()
                      //.UseUrls("http://localhost:7006")
                      .UseUrls("http://0.0.0.0:7006")
                      .Configure(app =>
                      {
                          // A bad license no longer stops the service: the API stays up so
                          // checkLicense can explain what is wrong, and the verify endpoints
                          // answer with the license error instead.
                          var license = LicenseChecker.Check(licensePath, out finger);
                          if (license.Valid)
                              _logger.LogInformation("License is valid for hardware ID {HardwareId}", license.MachineHardwareId);
                          else
                              _logger.LogError("License check failed: {Message} Face: {FaceError} Finger: {FingerError}",
                                  license.Message, license.Face.Error, license.Finger.Error);

                          // How many verifyPhoto requests may run face detection at once. Each one
                          // holds a FaceHandler (~74 MB) and one CPU core for ~0.15-0.25 s; later
                          // requests wait their turn. Set VerifyPhoto:MaxConcurrency in
                          // appsettings.json or the VerifyPhoto__MaxConcurrency environment variable.
                          int faceConcurrency = _configuration.GetValue("VerifyPhoto:MaxConcurrency",
                              Math.Min(4, Environment.ProcessorCount));
                          _faceVerifier = new FacePhotoVerifier(faceConcurrency);
                          if (license.Face.Valid)
                          {
                              try
                              {
                                  _faceVerifier.WarmUp();
                                  _logger.LogInformation("verifyPhoto: {Count} face handlers ready", _faceVerifier.MaxConcurrency);
                              }
                              catch (Exception ex)
                              {
                                  // Not fatal: handlers are created on first use instead.
                                  _logger.LogError(ex, "verifyPhoto: could not create face handlers at startup");
                              }
                          }

                          app.UseCors();
                          app.UseRouting();
                          app.UseEndpoints(endpoint =>
                          {
                              endpoint.MapGet("Hello", () => "Hello from API Listening to port 7006");
                              endpoint.MapGet("checkLicense", () => Results.Ok(license));
                              endpoint.MapPost("verifyPhoto", async (HttpRequest request) =>
                              {
                                  if (!license.Face.Valid)
                                      return Results.BadRequest("Face license is not valid: " + license.Face.Error);

                                  var form = await request.ReadFormAsync();
                                  var file = form.Files.FirstOrDefault();

                                  if (file == null||file.Length==0)
                                      return Results.BadRequest("No File Uploaded");

                                  try
                                  {
                                      using var stream = file.OpenReadStream();
                                      using var memoryStream = new MemoryStream();
                                      await stream.CopyToAsync(memoryStream);
                                      memoryStream.Seek(0,SeekOrigin.Begin);

                                      Parameters parameters = new Parameters();
                                      using var bitmap = new System.Drawing.Bitmap(memoryStream);

                                      IcaoFace.ICAOclass result = await _faceVerifier!.VerifyAsync(bitmap, parameters, request.HttpContext.RequestAborted);
                                      // The response serializes the image's properties after this
                                      // handler returns, so it is released when the request ends.
                                      request.HttpContext.Response.RegisterForDispose(result.image);

                                      // The BMP buffer is sized up front, and its bytes are serialized
                                      // as Base64 directly. The JSON is the same as before, without a
                                      // ~3 MB copy and an ~8 MB string per request, which kept the
                                      // garbage collector busy under load.
                                      using var outputSteam = new MemoryStream(EstimateBmpSize(result.image));
                                      result.image.Save(outputSteam,System.Drawing.Imaging.ImageFormat.Bmp);
                                      var base64 = new ReadOnlyMemory<byte>(outputSteam.GetBuffer(), 0, (int)outputSteam.Length);

                                      return Results.Ok(new { result,base64});
                                  }
                                  catch(Exception ex)
                                  {
                                      return Results.BadRequest(ex.Message);
                                  }
                              });
                              endpoint.MapPost("verifyFinger", async (HttpRequest request) =>
                              {
                                  if (finger == null)
                                      return Results.BadRequest("Fingerprint license is not valid: " + license.Finger.Error);

                                  var form = await request.ReadFormAsync();
                                  var file = form.Files.FirstOrDefault();

                                  if (file == null || file.Length == 0)
                                      return Results.BadRequest("No File Uploaded");

                                  try
                                  {
                                      using var stream = file.OpenReadStream();
                                      using var memoryStream = new MemoryStream();
                                      await stream.CopyToAsync(memoryStream);
                                      memoryStream.Seek(0, SeekOrigin.Begin);

                                      using var bitmap = new System.Drawing.Bitmap(memoryStream);

                                      string result = fingerPrint.verifyFingerPrintv1(bitmap, finger);                                    

                                      return Results.Ok(new { result });
                                  }
                                  catch (Exception ex)
                                  {
                                      return Results.BadRequest(ex.Message);
                                  }
                              });

                              endpoint.MapPost("verifyFinger_v1", async (HttpRequest request) =>
                              {
                                  if (finger == null)
                                      return Results.BadRequest("Fingerprint license is not valid: " + license.Finger.Error);

                                  var form = await request.ReadFormAsync();
                                  var file = form.Files.FirstOrDefault();

                                  if (file == null || file.Length == 0)
                                      return Results.BadRequest("No File Uploaded");

                                  try
                                  {
                                      using var stream = file.OpenReadStream();
                                      using var memoryStream = new MemoryStream();
                                      await stream.CopyToAsync(memoryStream);
                                      memoryStream.Seek(0, SeekOrigin.Begin);

                                      using var bitmap = new System.Drawing.Bitmap(memoryStream);

                                      string result = fingerPrint.verifyFingerPrintv1(bitmap, finger);

                                      // Only a "Verified" verdict carries an image back. A rejection
                                      // returns the verdict alone — the same shape as the original
                                      // verifyFinger endpoint, which clients already handle.
                                      //
                                      // This is not only correctness: the client verifies up to ten
                                      // frames a second and most are rejected, and each image is
                                      // ~600 KB of Base64. Sending one only on the frame that is
                                      // actually kept cuts almost all of that traffic.
                                      //
                                      // Equality, not Contains — "Not Verified".Contains("Verified")
                                      // is true, which would defeat the whole check.
                                      if (!string.Equals(result?.Trim(), "Verified",
                                              StringComparison.OrdinalIgnoreCase))
                                      {
                                          return Results.Ok(new { result });
                                      }

                                      using var outputSteam = new MemoryStream();
                                      bitmap.Save(outputSteam, System.Drawing.Imaging.ImageFormat.Bmp);
                                      var imageBytes = outputSteam.ToArray();

                                      var base64 = Convert.ToBase64String(imageBytes);

                                      return Results.Ok(new { result, base64 });
                                  }
                                  catch (Exception ex)
                                  {
                                      return Results.BadRequest(ex.Message);
                                  }
                              });

                          });
                      });
                  })
                  .Build();

            await _webApiHost.RunAsync(stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_webApiHost != null)
            {
                await _webApiHost.StopAsync(cancellationToken);
                _webApiHost?.Dispose();
            }
            _faceVerifier?.Dispose();
            await base.StopAsync(cancellationToken);
        }

        // Size of the BMP the image will be saved as: header, room for a palette, and padded
        // rows. A low guess is still correct, the stream just grows.
        private static int EstimateBmpSize(System.Drawing.Image image)
        {
            int bitsPerPixel = System.Drawing.Image.GetPixelFormatSize(image.PixelFormat);
            long stride = ((long)image.Width * bitsPerPixel + 31) / 32 * 4;
            return (int)Math.Min(int.MaxValue, 54 + 1024 + stride * image.Height);
        }
    }
}
