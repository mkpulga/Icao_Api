using Innovatrics.IFace;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace obsDLL_v3.FaceRecognition
{
    public class IcaoFace
    {
        public IFace icaoFace { get; set; }

        IFace iface;
        Boolean iface_loaded;
        private FaceHandler fc;
        private FaceFeatureId[] facefeatures;

        Parameters par;

        private int minEyeDistance;
        private int maxEyeDistance;

        Pen redPen;

        ImageCodecInfo xImageCodecInfo;
        System.Drawing.Imaging.Encoder xEncoder;
        EncoderParameters xEncoderParameters;

        public Boolean valid_picture;


        public string loadICAO(string licensePath, string strArrayPath,Parameters parameters)
        {
            this.redPen = new Pen(Color.Red, 10f);
            this.minEyeDistance = 0x19;
            this.maxEyeDistance = 400;

            this.par = parameters;
            this.par.FileLocation1 = strArrayPath + "\\";//Application.StartupPath + @"\tmp_photo\"
            this.facefeatures = this.GetFaceFeatures();
            this.setEncoderParams();
            this.valid_picture = false;

            #region initialize license
            try
            {

                if (!this.iface_loaded)
                {
                    // string licensePath = Dashboard.icaoLicensepath;//Application.StartupPath+"\\onejkit.lic";
                    this.IFaceInit(licensePath);
                }
                this.fc = new Innovatrics.IFace.FaceHandler();
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message + " - " + ex.StackTrace;
                //MessageBox.Show(ex.Message + ": " + Dashboard.icaoLicensepath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                //this.close_form = true;
                //this.Close();
                //Application.DoEvents();
            }
            #endregion

        }

        private void IFaceInit(string licenseFile)
        {

            this.iface = IFace.Instance;
            try
            {
                this.iface.InitWithLicense(File.ReadAllBytes(licenseFile));
                this.iface_loaded = true;
            }
            catch (Exception ex)
            {
                // MessageBox.Show("Error: " + ex.Message);
                //   Environment.Exit(1);
            }
        }


        public ICAOclass verifyIcao(Bitmap image, string licensePath,Parameters icaoParams)
        {
            ICAOclass icao = new ICAOclass();
            string icaoInit =  loadICAO(licensePath, "", icaoParams);
            if (icaoInit.ToUpper().Equals("SUCCESS"))
            {

            
            Bitmap bitmap2 = (Bitmap)image.Clone();
            string verify = "";
            if (image != null)
            {
                try
                {

                    Graphics graphics = Graphics.FromImage(bitmap2);
                    Face[] faceArray = this.fc.DetectFaces(bitmap2, this.minEyeDistance, this.maxEyeDistance, 10);
                    FaceFeature[] features = faceArray[0].GetFeatures(this.facefeatures);
                    int num = ((features.Length - 1) - 1);
                    int index = 0;

                    while (true)
                    {

                        if ((index > num))
                        {

                            if (!this.VerifyIcao(this.par, faceArray[0]))
                            {
                                verify = "unverified";
                            }
                            else
                            {
                                verify = "verified";
                            }

                            break;
                        }
                        PointF pos = features[index].Pos;
                        graphics.DrawEllipse(this.redPen, features[index].Pos.X, pos.Y, 5, 5);
                        index += 1;
                    }



                }
                catch (Exception ex)
                {
                    verify = ex.Message;
                }

                icao.message = verify;
                icao.image = bitmap2;
            }
            }
            else
            {
                icao.message = icaoInit;
            }
            return icao;
        }
        public bool VerifyIcao(Parameters parm, Face fc)
        {
            FaceAttributeRangeStatus inRange = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus brightness = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus contrast = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus uniqueIntensityLevel = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus shadow = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus noseShadow = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus eyeGaze = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus eyeStatusR = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus eyeStatusL = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus glassstatus = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus mouthStatus = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus backgroundUniformity = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus redEyeR = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus redEyeL = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus roll = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus yaw = FaceAttributeRangeStatus.InRange;
            FaceAttributeRangeStatus pitch = FaceAttributeRangeStatus.InRange;
            if (parm.Sharpness1)
                inRange = fc.GetAttributeRangeStatus(FaceAttributeId.Sharpness, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.Brightness1)
                brightness = fc.GetAttributeRangeStatus(FaceAttributeId.Brightness, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.Contrast1)
                contrast = fc.GetAttributeRangeStatus(FaceAttributeId.Contrast, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.UniqueIntensityLevels1)
                uniqueIntensityLevel = fc.GetAttributeRangeStatus(FaceAttributeId.UniqueIntensityLevels, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.Shadow1)
                shadow = fc.GetAttributeRangeStatus(FaceAttributeId.Shadow, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.NoseShadow1)
                noseShadow = fc.GetAttributeRangeStatus(FaceAttributeId.NoseShadow, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.EyeGaze1)
                eyeGaze = fc.GetAttributeRangeStatus(FaceAttributeId.EyeGaze, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.EyeStatusR1)
                eyeStatusR = fc.GetAttributeRangeStatus(FaceAttributeId.EyeStatusR, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.EyeStatusL1)
                eyeStatusL = fc.GetAttributeRangeStatus(FaceAttributeId.EyeStatusL, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.MouthStatus1)
                mouthStatus = fc.GetAttributeRangeStatus(FaceAttributeId.MouthStatus, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.BackgroundUniformity1)
                backgroundUniformity = fc.GetAttributeRangeStatus(FaceAttributeId.BackgroundUniformity, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.RedEyeR1)
                redEyeR = fc.GetAttributeRangeStatus(FaceAttributeId.RedEyeR, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.RedEyeL1)
                redEyeL = fc.GetAttributeRangeStatus(FaceAttributeId.RedEyeL, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.Roll1)
                roll = fc.GetAttributeRangeStatus(FaceAttributeId.Roll, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.Yaw1)
                yaw = fc.GetAttributeRangeStatus(FaceAttributeId.Yaw, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.Pitch1)
                pitch = fc.GetAttributeRangeStatus(FaceAttributeId.Pitch, null/* TODO Change to default(_) if this is not a reference type */);
            if (parm.GlassStatus1)
                glassstatus = fc.GetAttributeRangeStatus(FaceAttributeId.GlassStatus, null/* TODO Change to default(_) if this is not a reference type */);
            return this.CheckCompliance(inRange, brightness, contrast, uniqueIntensityLevel, shadow, noseShadow, eyeGaze, eyeStatusR, eyeStatusL, mouthStatus, backgroundUniformity, redEyeR, redEyeL, roll, yaw, pitch, glassstatus);
        }

        #region version 1

        public IFace initializeIcaoIface(string licenseFile) 
        {
            IFace result = IFace.Instance;

            try
            {
                result.InitWithLicense(File.ReadAllBytes(licenseFile));
            }
            catch (Exception ex) 
            {
                throw new Exception(ex.Message);
            }

            return result;

        }
        public IcaoFaceClass initializeIcaoIfacev1(string licenseFile)
        {
            IcaoFaceClass result1 = new IcaoFaceClass();
                result1.iface = IFace.Instance;

            try
            {
                result1.iface.InitWithLicense(File.ReadAllBytes(licenseFile));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            return result1;

        }
        public class IcaoFaceClass
        {
            public IFace iface { get; set; }
            public FaceHandler faceHandler { get; set; }
            public string message { get; set; }
        }

     

        #endregion

        private bool CheckCompliance(
             FaceAttributeRangeStatus sharpness,
             FaceAttributeRangeStatus brightness,
             FaceAttributeRangeStatus contrast,
             FaceAttributeRangeStatus uniqueIntensityLevel,
             FaceAttributeRangeStatus shadow,
             FaceAttributeRangeStatus noseShadow,
             FaceAttributeRangeStatus eyeGaze,
             FaceAttributeRangeStatus eyeStatusR,
             FaceAttributeRangeStatus eyeStatusL,
             FaceAttributeRangeStatus mouthStatus,
             FaceAttributeRangeStatus backgroundUniformity,
             FaceAttributeRangeStatus redEyeR,
             FaceAttributeRangeStatus redEyeL,
             FaceAttributeRangeStatus roll,
             FaceAttributeRangeStatus yaw,
             FaceAttributeRangeStatus pitch,
             FaceAttributeRangeStatus glassstatus)
        {
            return sharpness == FaceAttributeRangeStatus.InRange
                && brightness == FaceAttributeRangeStatus.InRange
                && contrast == FaceAttributeRangeStatus.InRange
                && uniqueIntensityLevel == FaceAttributeRangeStatus.InRange
                && shadow == FaceAttributeRangeStatus.InRange
                && noseShadow == FaceAttributeRangeStatus.InRange
                && eyeGaze == FaceAttributeRangeStatus.InRange
                && eyeStatusR == FaceAttributeRangeStatus.InRange
                && eyeStatusL == FaceAttributeRangeStatus.InRange
                && mouthStatus == FaceAttributeRangeStatus.InRange
                && backgroundUniformity == FaceAttributeRangeStatus.InRange
                && redEyeR == FaceAttributeRangeStatus.InRange
                && redEyeL == FaceAttributeRangeStatus.InRange
                && roll == FaceAttributeRangeStatus.InRange
                && yaw == FaceAttributeRangeStatus.InRange
                && pitch == FaceAttributeRangeStatus.InRange
                && glassstatus == FaceAttributeRangeStatus.InRange;
        }
        public FaceFeatureId[] GetFaceFeatures()
        {

            List<FaceFeatureId> list = new List<FaceFeatureId>();

            foreach (FaceFeatureId item in Enum.GetValues(typeof(FaceFeatureId)))
            {
                list.Add(item);
            }

            return list.ToArray();
        }
        private void setEncoderParams()
        {
            this.xImageCodecInfo = GetEncoderInfo("image/jpeg");
            this.xEncoder = System.Drawing.Imaging.Encoder.Quality;
            this.xEncoderParameters = new EncoderParameters(1);
        }
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] imageEncoders = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo info in imageEncoders)
            {
                if (info.MimeType == mimeType)
                {
                    return info;
                }
            }
            return null;
        }

        public class ICAOclass
        {
            public string message { get; set; }
            public Image image { get; set; }
        }

    

    }

}
