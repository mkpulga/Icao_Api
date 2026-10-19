using System.Text;
using System.Text.RegularExpressions;
using Innovatrics.IFace;
using Innovatrics.ISegLib;

namespace obsIcaoApi
{
    public record EngineLicense(bool Valid, string? Error);

    public record LicenseStatus(
        bool Valid,
        string Message,
        string? MachineHardwareId,
        string? LicenseHardwareId,
        EngineLicense Face,
        EngineLicense Finger,
        string LicensePath,
        DateTime CheckedAt);

    public static class LicenseChecker
    {
        // Loads onejkit.lic into the face (IFace) and fingerprint (ISegLib) engines and
        // reports whether each one accepted it. Loading is the real test: the engines check
        // the signature and the hardware ID the license was issued for.
        //
        // The engines keep the license they loaded for the life of the process, so after
        // replacing onejkit.lic the service must be restarted to pick it up.
        public static LicenseStatus Check(string licensePath, out ISegLib? finger)
        {
            finger = null;
            string? machineId = GetMachineHardwareId();

            if (!File.Exists(licensePath))
            {
                var missing = new EngineLicense(false, "License file not found");
                return new LicenseStatus(false, $"License file not found: {licensePath}",
                    machineId, null, missing, missing, licensePath, DateTime.Now);
            }

            byte[] content = File.ReadAllBytes(licensePath);
            string? licenseId = ReadLicenseHardwareId(content);

            EngineLicense face = CheckFace(content);
            EngineLicense fingerResult = CheckFinger(content, out finger);
            bool valid = face.Valid && fingerResult.Valid;

            string message;
            if (valid)
                message = "License is valid";
            else if (licenseId != null && machineId != null &&
                     !licenseId.Equals(machineId, StringComparison.OrdinalIgnoreCase))
                message = $"License was issued for hardware ID {licenseId}, but this machine is {machineId}. " +
                          $"Request a new license for {machineId}.";
            else
                message = "License was rejected: " + (face.Error ?? fingerResult.Error);

            return new LicenseStatus(valid, message, machineId, licenseId,
                face, fingerResult, licensePath, DateTime.Now);
        }

        private static EngineLicense CheckFace(byte[] content)
        {
            try
            {
                IFace.Instance.InitWithLicense(content);
                return new EngineLicense(true, null);
            }
            catch (IFaceException ex) when (ex.Code == IFaceExceptionCode.LicenseAlreadyInitialized)
            {
                // A license was already loaded successfully earlier in this process.
                return new EngineLicense(true, null);
            }
            catch (Exception ex)
            {
                return new EngineLicense(false, ex.Message);
            }
        }

        private static EngineLicense CheckFinger(byte[] content, out ISegLib? finger)
        {
            // SetLicenseContent only stores the bytes; the constructor is what validates them.
            try
            {
                ISegLib.SetLicenseContent(content);
                finger = new ISegLib();
                return new EngineLicense(true, null);
            }
            catch (Exception ex)
            {
                finger = null;
                return new EngineLicense(false, ex.Message);
            }
        }

        private static string? GetMachineHardwareId()
        {
            try
            {
                return IFace.Instance.GetHardwareId();
            }
            catch
            {
                return null;
            }
        }

        // The license header holds, in plain text, the hardware ID it was issued for.
        private static string? ReadLicenseHardwareId(byte[] content)
        {
            string header = Encoding.Latin1.GetString(content, 0, Math.Min(content.Length, 64));
            Match match = Regex.Match(header, "[0-9A-F]{16}");
            return match.Success ? match.Value : null;
        }
    }
}
