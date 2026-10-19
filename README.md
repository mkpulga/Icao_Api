# obsIcaoApi

Windows service (ASP.NET Core, .NET 9) that checks face photos against ICAO quality rules and fingerprint images for quality, using the Innovatrics IFace 3.1 and ISegLib SDKs. It listens on port 7006.

| Endpoint | Purpose |
| --- | --- |
| `POST /verifyPhoto` | Face photo (multipart, first file). Returns `result.message` (`verified` / `unverified`, or the error text) and the photo with the detected features marked, as a Base64 BMP |
| `POST /verifyFinger`, `POST /verifyFinger_v1` | Fingerprint quality (`Verified` / `Not Verified`); v1 also returns the image when verified |
| `GET /checkLicense` | Whether the license loaded for the face and fingerprint engines |
| `GET /Hello` | Liveness check |

## Repository layout

| Path | Contents |
| --- | --- |
| `obsICao\` | The API (`obsIcaoApi.csproj`). `Worker.cs` hosts the endpoints; `FacePhotoVerifier.cs` runs the verifyPhoto check on a pool of face handlers; `LicenseChecker.cs` loads the license at startup |
| `obsDLL_v3\` | Source of `obsDLL_v3.dll` (face ICAO and fingerprint helpers, .NET Framework 4.8). The API references the prebuilt `obsICao\dlls\obsDLL_v3.dll` |
| `FaceDllService\` | Template project, not used by the API |
| `tests\jmeter\` | JMeter stress test for verifyPhoto, with scripts and the performance report (`PERFORMANCE-REPORT.md`) |
| `tests\regression\` | Byte-for-byte response comparison between two builds |

## Not in this repository

These files are deliberately kept out (see `.gitignore`); copy them in before building or running:

- **Innovatrics SDK binaries** (proprietary): put `Innovatrics.IFace.dll`, `Innovatrics.IEngine.dll`, `ISegLib.NET.dll` and `iseglib.dll` in `obsICao\dlls\`, and `iseglib.dll` also in `obsICao\`. The native `IFace.dll` (3.1) must be installed on the machine; the Visual C++ 2010 x64 runtime is required.
- **License**: `onejkit.lic` in `obsICao\`. It is issued for one machine's hardware ID; `GET /checkLicense` shows the ID of the machine it runs on.
- **Sample images**: face and fingerprint photos are biometric data. The tests take their paths as settings.

## Build and run

```powershell
dotnet build obsICao\obsIcaoApi.csproj
dotnet run --project obsICao\obsIcaoApi.csproj
curl.exe http://localhost:7006/checkLicense

# Release package for a server
dotnet publish obsICao\obsIcaoApi.csproj -c Release -r win-x64 --self-contained true -o publish
```

`VerifyPhoto:MaxConcurrency` (in `appsettings.json`, or the `VerifyPhoto__MaxConcurrency` environment variable) is how many face checks run at once. The default is 4, or the number of CPUs if lower; each one holds ~74 MB of SDK memory.

## Tests

- Load test: `tests\jmeter\README.md` covers installing Java and JMeter, running, the safety limits and validating on a server.
- Regression: `tests\regression\README.md`.
