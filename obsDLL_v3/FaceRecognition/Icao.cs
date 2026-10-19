using Innovatrics.IFace;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Image = System.Drawing.Image;

namespace obsDLL_v3.FaceRecognition
{
    public class Icao
    {
        public void InitializeLicense(string licensePath)
        {
            
            try
            {

                if(!Icao.iface_loaded)
                    this.IFaceInit(licensePath);

                this.fc = new Innovatrics.IFace.FaceHandler();
                this.redPen = new Pen(Color.Red, 10f);
                this.minEyeDistance = 0x19;
                this.maxEyeDistance = 400;
                this.par = new Parameters();
                this.facefeatures = this.GetFaceFeatures();

            }
            catch (Exception ex)
            {

              throw new Exception("Failed to initialize ICAO Face Recognition. Please check the license path and ensure the license is valid.", ex);
            }


        }
        public static bool iface_loaded { get; set; }


        private void IFaceInit(string licenseFile)
        {
            var iface = IFace.Instance;
            try
            {
                iface.InitWithLicense(File.ReadAllBytes(licenseFile));
                Icao.iface_loaded = true;

            }
            catch (Exception ex)
            {
               throw new Exception("Failed to initialize ICAO Face Recognition with the provided license. Please ensure the license file is correct and valid.", ex);
            }
        }

        private FaceHandler fc;
        private int minEyeDistance;
        private int maxEyeDistance;
        private FaceFeatureId[] facefeatures;


        private Parameters par;
        EncoderParameters xEncoderParameters;
        EncoderParameter xEncoderParameter;
        System.Drawing.Imaging.Encoder xEncoder;
        ImageCodecInfo xImageCodecInfo;
        Boolean valid_picture;
        Pen redPen;

        private FaceFeatureId[] GetFaceFeatures()
        {

            List<FaceFeatureId> list = new List<FaceFeatureId>();

            foreach (FaceFeatureId item in Enum.GetValues(typeof(FaceFeatureId)))
            {
                list.Add(item);
            }

            return list.ToArray();
        }

        public Image processImage(Bitmap image,string rootFolder)
        {
           
            Image result = null;
            try
            {
                Bitmap original = (Bitmap)image.Clone();
                Bitmap bitmap2 = (Bitmap)image.Clone();

                Graphics graphics = Graphics.FromImage(bitmap2);
                Face[] faceArray = this.fc.DetectFaces(bitmap2, this.minEyeDistance, this.maxEyeDistance, 10);
                FaceFeature[] features = faceArray[0].GetFeatures(this.facefeatures);
                int num = ((features.Length - 1) - 1);
                int index = 0;

                while (true)
                {

                    if ((index > num))
                    {
                        result = bitmap2;
                        break;
                    }
                    PointF pos = features[index].Pos;
                    graphics.DrawEllipse(this.redPen, features[index].Pos.X, pos.Y, 5, 5);
                    index += 1;
                }
            }
            catch (Exception exception1)
            { 
                 Exception ex = exception1;
                //throw new Exception("An error occurred while processing the image for ICAO compliance. Please ensure the image is clear and meets the necessary requirements.", ex);
            }
            return result;
        }

        public string VerifyIcaoCompliance(Bitmap image, Parameters parameter=null)
        {
            string result = "Not Verified";
            try
            {                
                Bitmap original = (Bitmap)image.Clone();
                Bitmap bitmap2 = (Bitmap)image.Clone();
                Face[] faceArray = this.fc.DetectFaces(bitmap2, this.minEyeDistance, this.maxEyeDistance, 10);
                if(parameter!=null)this.par = parameter;

                if (this.VerifyIcao(this.par, faceArray[0]))
                {
                    result = "Verified";
                }

            }
            catch (Exception exception1) { 
                result = exception1.Message;
            }
            return result;
        }


        private bool VerifyIcao(Parameters parm, Face fc)
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

        private Rectangle GetCropArea(float noseX, float noseY, int squaredistance)
        {
            int x = (int)Math.Round((double)(System.Convert.ToInt32(Math.Round((double)noseX)) - (double)squaredistance / 2));
            int y = (int)Math.Round((double)(System.Convert.ToInt32(Math.Round((double)noseY)) - (double)squaredistance / 2));

            return new Rectangle(x, y, squaredistance, squaredistance);
        }
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

    }
}
