using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace obsDLL_v3.FingerPrint
{
    public class FutronicHandler

    {
        [StructLayout(LayoutKind.Sequential)]
        public struct _FTRSCAN_FAKE_REPLICA_PARAMETERS
        {
            private bool bCalculated;
            private int nCalculatedSum1;
            private int nCalculatedSumFuzzy;
            private int nCalculatedSumEmpty;
            private int nCalculatedSum2;
            private double dblCalculatedTremor;
            private double dblCalculatedValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct _FTRSCAN_IMAGE_SIZE
        {
            public int nWidth;
            public int nHeight;
            public int nImageSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct _FTRSCAN_FRAME_PARAMETERS
        {
            private int nContrastOnDose2;
            private int nContrastOnDose4;
            private int nDose;
            private int nBrightnessOnDose1;
            private int nBrightnessOnDose2;
            private int nBrightnessOnDose3;
            private int nBrightnessOnDose4;
            private FutronicHandler._FTRSCAN_FAKE_REPLICA_PARAMETERS FakeReplicaParams;
            private FutronicHandler._FTRSCAN_FAKE_REPLICA_PARAMETERS Reserved;
        }



        public class Device
        {
            private IntPtr my_device;
            private const int FTR_OPTIONS_CHECK_FAKE_REPLICA = 1;
            private const int FTR_OPTIONS_DETECT_FAKE_FINGER = 1;
            private const int FTR_OPTIONS_FAST_FINGER_DETECT_METHOD = 2;
            private const int FTR_OPTIONS_RECEIVE_LONG_IMAGE = 4;
            private const int FTR_OPTIONS_RECEIVE_FAKE_IMAGE = 8;
            private const int FTR_OPTIONS_SCALE_IMAGE = 0x10;
            private const int FTR_OPTIONS_IMPROVE_IMAGE = 0x20;
            private const int FTR_OPTIONS_INVERT_IMAGE = 0x40;
            private const int FTR_OPTIONS_PREVIEW_MODE = 0x80;
            private const int FTR_OPTIONS_IMAGE_FORMAT_MASK = 0x700;
            private const int FTR_OPTIONS_IMAGE_FORMAT_1 = 0x100;
            private const int FTR_OPTIONS_ELIMINATE_BACKGROUND = 0x800;
            private const int FTR_OPTIONS_IMPROVE_BACKGROUND = 0x1000;
            private const int FTR_OPTIONS_ROLL_THRESHOLD_MASK = 0x1f0000;
            private const int FTR_OPTIONS_ROLL_THRESHOLD_1 = 0x10000;
            private const int FTR_OPTIONS_DISABLE_MOVABLE_FINGER_CHECKING = 0x200000;

            public bool DetectEmptyFinger() =>
                ftrScanSetOptions(this.my_device, 2, 2);

            public bool DetectFakeFinger() =>
                ftrScanSetOptions(this.my_device, 1, 1);

            public void Dispose()
            {
                if (this.Connected)
                {
                    ftrScanCloseDevice(this.my_device);
                    this.my_device = IntPtr.Zero;
                }
            }

            public Bitmap ExportBitMap()
            {
                Bitmap bitmap;
                if (!this.Connected)
                {
                    bitmap = null;
                }
                else
                {
                    FutronicHandler._FTRSCAN_IMAGE_SIZE pImageSize = new FutronicHandler._FTRSCAN_IMAGE_SIZE();
                    ftrScanGetImageSize(this.my_device, out pImageSize);
                    byte[] pBuffer = new byte[(pImageSize.nImageSize - 1) + 1];
                    ftrScanGetImage2(this.my_device, 4, pBuffer);
                    Bitmap bitmap2 = new Bitmap(pImageSize.nWidth, pImageSize.nHeight);
                    int num = pImageSize.nWidth - 1;
                    int x = 0;
                    while (true)
                    {
                        if (x > num)
                        {
                            bitmap = bitmap2;
                            break;
                        }
                        int num3 = pImageSize.nHeight - 1;
                        int y = 0;
                        while (true)
                        {
                            if (y > num3)
                            {
                                x++;
                                break;
                            }
                            int red = 0xff - pBuffer[(y * pImageSize.nWidth) + x];
                            bitmap2.SetPixel(x, y, Color.FromArgb(red, red, red));
                            y++;
                        }
                    }
                }
                return bitmap;
            }

            [DllImport("ftrScanAPI.dll")]
            private static extern void ftrScanCloseDevice(IntPtr ftrHandle);
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanGetDiodesStatus(IntPtr ftrHandle, out bool pbIsGreenDiodeOn, out bool pbIsRedDiodeOn);
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanGetImage(IntPtr ftrHandle, int nDose, byte[] pBuffer);
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanGetImage2(IntPtr ftrHandle, int nDose, byte[] pBuffer);
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanGetImageSize(IntPtr ftrHandle, out FutronicHandler._FTRSCAN_IMAGE_SIZE pImageSize);
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanIsFingerPresent(IntPtr ftrHandle, out FutronicHandler._FTRSCAN_FRAME_PARAMETERS pFrameParameters);
            [DllImport("ftrScanAPI.dll")]
            private static extern IntPtr ftrScanOpenDevice();
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanSetDiodesStatus(IntPtr ftrHandle, byte byGreenDiodeStatus, byte byRedDiodeStatus);
            [DllImport("ftrScanAPI.dll")]
            private static extern bool ftrScanSetOptions(IntPtr ftrHandle, int dwMask, int dwFlags);
            public void GetDiodesStatus(out bool green, out bool red)
            {
                ftrScanGetDiodesStatus(this.my_device, out green, out red);
            }

            public bool Init()
            {
                if (!this.Connected)
                {
                    this.my_device = ftrScanOpenDevice();
                }
                return this.Connected;
            }

            public bool InvertImage() =>
                ftrScanSetOptions(this.my_device, 0x40, 0x40);

            public bool IsFinger()
            {
                FutronicHandler._FTRSCAN_FRAME_PARAMETERS pFrameParameters = new FutronicHandler._FTRSCAN_FRAME_PARAMETERS();
                return ftrScanIsFingerPresent(this.my_device, out pFrameParameters);
            }

            public byte[] RawData()
            {
                byte[] buffer;
                if (!this.Connected)
                {
                    buffer = null;
                }
                else
                {
                    FutronicHandler._FTRSCAN_IMAGE_SIZE pImageSize = new FutronicHandler._FTRSCAN_IMAGE_SIZE();
                    ftrScanGetImageSize(this.my_device, out pImageSize);
                    byte[] pBuffer = new byte[(pImageSize.nImageSize - 1) + 1];
                    ftrScanGetImage2(this.my_device, 4, pBuffer);
                    buffer = pBuffer;
                }
                return buffer;
            }

            public byte[] RawData2()
            {
                byte[] buffer;
                if (!this.Connected)
                {
                    buffer = null;
                }
                else
                {
                    FutronicHandler._FTRSCAN_IMAGE_SIZE pImageSize = new FutronicHandler._FTRSCAN_IMAGE_SIZE();
                    ftrScanGetImageSize(this.my_device, out pImageSize);
                    byte[] pBuffer = new byte[(pImageSize.nImageSize - 1) + 1];
                    ftrScanGetImage2(this.my_device, 4, pBuffer);
                    buffer = pBuffer;
                }
                return buffer;
            }

            public void SetDiodesStatus(bool green, bool red)
            {
                ftrScanSetDiodesStatus(this.my_device, green ? ((byte)0xff) : ((byte)0), red ? ((byte)0xff) : ((byte)0));
            }

            public bool Connected
            {
                get =>
                    this.my_device != IntPtr.Zero;
                set
                {
                }
            }
        }

    }

}
