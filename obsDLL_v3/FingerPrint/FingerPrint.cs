using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Innovatrics.IEngine;
using Innovatrics.ISegLib;

namespace obsDLL_v3.FingerPrint
{
    public class FingerPrint
    {
        private Innovatrics.ISegLib.ISegLib iseglib;
        private byte[] licenseSeg;
        private float fingerRatio;
        public string loadLicense(string licensePath)
        {
            try
            {
                this.licenseSeg = System.IO.File.ReadAllBytes(licensePath);
                Innovatrics.ISegLib.ISegLib.SetLicenseContent(this.licenseSeg);
                this.iseglib = new Innovatrics.ISegLib.ISegLib();
                // this.fingerVerifierInit = true;
                return "Success";
            }catch(Exception ex)
            {
                return ex.Message;
            }            
        }
        public string verifyFingerPrint(Image fingerImage, string licensePath)
        {
            string result = "";

            string loadIcao = loadLicense(licensePath);

            if (!(loadIcao.Equals("Success")))
            {
                result = loadIcao;
            }
            else
            {
                bool isVerified = false;
                int quality = 0x3f;
                int realQuality = (int)Math.Round((double)(quality * this.fingerRatio));
                isVerified = verifyFingerQuality(fingerImage,60,90, realQuality);
                if (isVerified)
                    result = "Verified";
                else result = "Not Verified";
            }
            return result;
        }

        private bool verifyFingerQuality(Image fingerImage, int ThresholdMin, int ThresholdMax, int realQuality)
        {
            int nFIQScore = this.iseglib.GetNFIQScore(fingerImage);
            int imageIntensity = this.iseglib.GetImageIntensity(fingerImage);
            return (((this.iseglib.GetFingerprintQuality(fingerImage) > realQuality) && ((imageIntensity > ThresholdMin) && (imageIntensity < ThresholdMax))) && (nFIQScore < 3));
        }

        public static Image ConvertByteToGrayScaleImage(byte[] imageData, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            System.Drawing.Imaging.ColorPalette ncp = bitmap.Palette;
            for (int i = 0; i < 256; i++)
                ncp.Entries[i] = Color.FromArgb(255, i, i, i);
            bitmap.Palette = ncp;

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

            Marshal.Copy(imageData, 0, data.Scan0, imageData.Length);
            bitmap.UnlockBits(data);

            return bitmap;
        }

        #region version 1

        public ISegLib LoadLicense(string licensePath)
        {
            ISegLib result;
            try
            {
                this.licenseSeg = System.IO.File.ReadAllBytes(licensePath);
                Innovatrics.ISegLib.ISegLib.SetLicenseContent(this.licenseSeg);
                result = new Innovatrics.ISegLib.ISegLib();
                // this.fingerVerifierInit = true;
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }

        public string verifyFingerPrintv1(Image fingerImage, ISegLib iseglib)
        {
            string result = "";

            bool isVerified = false;
            int quality = 0x3f;
            int realQuality = (int)Math.Round((double)(quality * this.fingerRatio));


            int nFIQScore = iseglib.GetNFIQScore(fingerImage);
            int imageIntensity = iseglib.GetImageIntensity(fingerImage);
            int ThresholdMin = 60;
            int ThresholdMax = 90;
            isVerified = (((iseglib.GetFingerprintQuality(fingerImage) > realQuality) && ((imageIntensity > ThresholdMin) && (imageIntensity < ThresholdMax))) && (nFIQScore < 3));


            if (isVerified)
                result = "Verified";
            else result = "Not Verified";


            return result;
        }
        #endregion
    }
}
