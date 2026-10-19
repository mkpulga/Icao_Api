using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace obsDLL_v3.FaceRecognition
{
    public class Parameters
    {
        private string fileLocation = "";//(Application.StartupPath + @"\tmp_photo\");
        private bool RightEyeOuterCorner = true;
        private bool RightEyeCentre = true;
        private bool RightEyeInnerCorner = true;
        private bool LeftEyeInnerCorner = true;
        private bool LeftEyeCentre = true;
        private bool LeftEyeOuterCorner = true;
        private bool NoseTip = true;
        private bool MouthRightCorner = true;
        private bool MouthCenter = true;
        private bool MouthLeftCorner = true;
        private bool MouthUpperEdge = true;
        private bool MouthLowerEdge = true;
        private bool RightEyeBrowOuterEnd = true;
        private bool RightEyeBrowInnerEnd = true;
        private bool LeftEyeBrowInnerEnd = true;
        private bool LeftEyeBrowOuterEnd = true;
        private bool FaceRightEdge = true;
        private bool FaceChinTip = true;
        private bool FaceLeftEdge = true;
        public static bool MouthStatus = true;
        public static bool EyeStatusR = true;
        public static bool EyeStatusL = true;
        public static bool RedEyeR = true;
        public static bool RedEyeL = true;
        public static bool Roll = true;
        public static bool Sharpness = false;
        public static bool Brightness = false;
        public static bool Contrast = false;
        public static bool UniqueIntensityLevels = false;
        public static bool Shadow = false;
        public static bool NoseShadow = false;
        public static bool Specularity = false;
        public static bool EyeGaze = false;
        public static bool GlassStatus = false;
        public static bool HeavyFrame = false;
        public static bool BackgroundUniformity = false;
        public static bool Age = false;
        public static bool Yaw = false;
        public static bool Pitch = false;
        public static bool Gender = false;
        public static bool SegmentationMask = false;
        public static bool Crop = false;
        public static bool Template = false;
        public static bool EyeDistance = false;
        public static bool FaceConfidence = false;
        public static bool LastItem = false;

        public string FileLocation1
        {
            get =>
                this.fileLocation;
            set =>
                this.fileLocation = value;
        }

        public bool RightEyeOuterCorner1
        {
            get =>
                this.RightEyeOuterCorner;
            set =>
                this.RightEyeOuterCorner = value;
        }

        public bool RightEyeCentre1
        {
            get =>
                this.RightEyeCentre;
            set =>
                this.RightEyeCentre = value;
        }

        public bool RightEyeInnerCorner1
        {
            get =>
                this.RightEyeInnerCorner;
            set =>
                this.RightEyeInnerCorner = value;
        }

        public bool LeftEyeInnerCorner1
        {
            get =>
                this.LeftEyeInnerCorner;
            set =>
                this.LeftEyeInnerCorner = value;
        }

        public bool LeftEyeCentre1
        {
            get =>
                this.LeftEyeCentre;
            set =>
                this.LeftEyeCentre = value;
        }

        public bool LeftEyeOuterCorner1
        {
            get =>
                this.LeftEyeOuterCorner;
            set =>
                this.LeftEyeOuterCorner = value;
        }

        public bool NoseTip1
        {
            get =>
                this.NoseTip;
            set =>
                this.NoseTip = value;
        }

        public bool MouthRightCorner1
        {
            get =>
                this.MouthRightCorner;
            set =>
                this.MouthRightCorner = value;
        }

        public bool MouthCenter1
        {
            get =>
                this.MouthCenter;
            set =>
                this.MouthCenter = value;
        }

        public bool MouthLeftCorner1
        {
            get =>
                this.MouthLeftCorner;
            set =>
                this.MouthLeftCorner = value;
        }

        public bool MouthUpperEdge1
        {
            get =>
                this.MouthUpperEdge;
            set =>
                this.MouthUpperEdge = value;
        }

        public bool MouthLowerEdge1
        {
            get =>
                this.MouthLowerEdge;
            set =>
                this.MouthLowerEdge = value;
        }

        public bool RightEyeBrowOuterEnd1
        {
            get =>
                this.RightEyeBrowOuterEnd;
            set =>
                this.RightEyeBrowOuterEnd = value;
        }

        public bool RightEyeBrowInnerEnd1
        {
            get =>
                this.RightEyeBrowInnerEnd;
            set =>
                this.RightEyeBrowInnerEnd = value;
        }

        public bool LeftEyeBrowInnerEnd1
        {
            get =>
                this.LeftEyeBrowInnerEnd;
            set =>
                this.LeftEyeBrowInnerEnd = value;
        }

        public bool LeftEyeBrowOuterEnd1
        {
            get =>
                this.LeftEyeBrowOuterEnd;
            set =>
                this.LeftEyeBrowOuterEnd = value;
        }

        public bool FaceRightEdge1
        {
            get =>
                this.FaceRightEdge;
            set =>
                this.FaceRightEdge = value;
        }

        public bool FaceChinTip1
        {
            get =>
                this.FaceChinTip;
            set =>
                this.FaceChinTip = value;
        }

        public bool FaceLeftEdge1
        {
            get =>
                this.FaceLeftEdge;
            set =>
                this.FaceLeftEdge = value;
        }

        public bool Sharpness1
        {
            get =>
                Sharpness;
            set =>
                Sharpness = value;
        }

        public bool Brightness1
        {
            get =>
                Brightness;
            set =>
                Brightness = value;
        }

        public bool Contrast1
        {
            get =>
                Contrast;
            set =>
                Contrast = value;
        }

        public bool UniqueIntensityLevels1
        {
            get =>
                UniqueIntensityLevels;
            set =>
                UniqueIntensityLevels = value;
        }

        public bool Shadow1
        {
            get =>
                Shadow;
            set =>
                Shadow = value;
        }

        public bool NoseShadow1
        {
            get =>
                NoseShadow;
            set =>
                NoseShadow = value;
        }

        public bool Specularity1
        {
            get =>
                Specularity;
            set =>
                Specularity = value;
        }

        public bool EyeGaze1
        {
            get =>
                EyeGaze;
            set =>
                EyeGaze = value;
        }

        public bool EyeStatusR1
        {
            get =>
                EyeStatusR;
            set =>
                EyeStatusR = value;
        }

        public bool EyeStatusL1
        {
            get =>
                EyeStatusL;
            set =>
                EyeStatusL = value;
        }

        public bool GlassStatus1
        {
            get =>
                GlassStatus;
            set =>
                GlassStatus = value;
        }

        public bool HeavyFrame1
        {
            get =>
                HeavyFrame;
            set =>
                HeavyFrame = value;
        }

        public bool MouthStatus1
        {
            get =>
                MouthStatus;
            set =>
                MouthStatus = value;
        }

        public bool BackgroundUniformity1
        {
            get =>
                BackgroundUniformity;
            set =>
                BackgroundUniformity = value;
        }

        public bool Age1
        {
            get =>
                Age;
            set =>
                Age = value;
        }

        public bool RedEyeR1
        {
            get =>
                RedEyeR;
            set =>
                RedEyeR = value;
        }

        public bool RedEyeL1
        {
            get =>
                RedEyeL;
            set =>
                RedEyeL = value;
        }

        public bool Roll1
        {
            get =>
                Roll;
            set =>
                Roll = value;
        }

        public bool Yaw1
        {
            get =>
                Yaw;
            set =>
                Yaw = value;
        }

        public bool Pitch1
        {
            get =>
                Pitch;
            set =>
                Pitch = value;
        }

        public bool Gender1
        {
            get =>
                Gender;
            set =>
                Gender = value;
        }

        public bool SegmentationMask1
        {
            get =>
                SegmentationMask;
            set =>
                SegmentationMask = value;
        }

        public bool Crop1
        {
            get =>
                Crop;
            set =>
                Crop = value;
        }

        public bool Template1
        {
            get =>
                Template;
            set =>
                Template = value;
        }

        public bool EyeDistance1
        {
            get =>
                EyeDistance;
            set =>
                EyeDistance = value;
        }

        public bool FaceConfidence1
        {
            get =>
                FaceConfidence;
            set =>
                FaceConfidence = value;
        }

        public bool LastItem1
        {
            get =>
                LastItem;
            set =>
                LastItem = value;
        }

        public enum SaveType
        {
            File,
            Clipboard
        }
    }

}
