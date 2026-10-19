using Innovatrics.ISegLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace obsDLL_v3.FingerPrint
{
    public class Biometrics
    {

        private ISegLib _segLib;
        private byte[] _licenseSeg;

        private readonly double fingerRatio = 1.0;

        public bool LoadLicense(string licensePath)
        {
            try
            {
                _licenseSeg = File.ReadAllBytes(licensePath);

                ISegLib.SetLicenseContent(_licenseSeg);

                _segLib = new ISegLib();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string VerifyFingerprint(Image fingerImage)
        {
            if (_segLib == null)
                return "License Not Loaded";

            bool isVerified = false;

            int quality = 0x3f;
            int realQuality = (int)Math.Round(quality * fingerRatio);

            int nFIQScore = _segLib.GetNFIQScore(fingerImage);
            int imageIntensity = _segLib.GetImageIntensity(fingerImage);

            int thresholdMin = 60;
            int thresholdMax = 90;

            isVerified =
                (_segLib.GetFingerprintQuality(fingerImage) > realQuality)
                && (imageIntensity > thresholdMin && imageIntensity < thresholdMax)
                && (nFIQScore < 3);

            return isVerified ? "Verified" : "Not Verified";
        }
    }
}
