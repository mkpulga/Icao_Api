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

        ISegLib? finger;
        private static IcaoFace icao = new IcaoFace();
        private static FingerPrint fingerPrint = new FingerPrint();

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
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

                                      IcaoFace.ICAOclass result = icao.verifyIcao(bitmap, licensePath, parameters);

                                      using var outputSteam = new MemoryStream();
                                      result.image.Save(outputSteam,System.Drawing.Imaging.ImageFormat.Bmp);
                                      var imageBytes = outputSteam.ToArray();

                                      var base64 = Convert.ToBase64String(imageBytes);

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
            await base.StopAsync(cancellationToken);
        }
    }
}
