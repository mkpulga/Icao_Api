# verifyPhoto regression check

Checks that a build answers exactly like a reference build: same HTTP status, same content type, and byte-identical bodies. The inputs reach every path of `verifyPhoto`: verdicts, no face, an indexed-colour image, an image too small for the SDK, a file that is not an image, an empty upload and a missing file. `verifyFinger`, `verifyFinger_v1`, `Hello` and `checkLicense` are captured too, to show they did not change.

```powershell
# 1. Reference build running on port 7006 (e.g. the version before your change)
.\capture-responses.ps1 -FaceImage C:\data\face.jpg -FingerImage C:\data\finger.bmp -OutDir C:\temp\captures\reference

# 2. New build running on port 7006
.\capture-responses.ps1 -FaceImage C:\data\face.jpg -FingerImage C:\data\finger.bmp -OutDir C:\temp\captures\new

# 3. Compare (exit code 1 if anything differs)
.\compare-responses.ps1 -Expected C:\temp\captures\reference -Actual C:\temp\captures\new
```

The test images are generated from the face photo you pass, and are written only to the capture folder: they are biometric data, so keep the captures out of source control. Requests are sent one at a time; this is a correctness check, not a load test. `checkLicense` is compared without its `checkedAt` and `licensePath` values, which change with the time and the install folder.
