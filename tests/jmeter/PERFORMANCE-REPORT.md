# verifyPhoto performance report: 10 TPS target

19 Oct 2026 · obsIcaoApi `POST /verifyPhoto` · development laptop

## Result

**The optimized build met all five acceptance criteria.** Over a 15-minute run it reached 10 TPS during the ramp, then completed **7,800 of 7,800 sustained requests as successful verifications: 10.00 per second for 13 minutes, with no errors**. Sustained response time was median 239 ms, p95 337 ms, p99 436 ms.

**The original build cannot sustain the target.** It stopped itself at an offered rate of about 6.6 TPS, because responses with HTTP 200 began to carry `Object is currently in use elsewhere.` instead of a verdict.

The API contract did not change. The responses of both builds are byte-identical across 21 regression cases. The SDK, the license and the verification logic are the same.

| | Original | Optimized |
| --- | --- | --- |
| Highest offered rate handled before failing | ~5.9 TPS; wrong results from 6–7 TPS | 15 TPS for 2 min, 0 errors (headroom probe) |
| 10 TPS for 13 min | not reachable | 10.00 successful/s, 7,800 / 7,800 |
| Failed requests | 5 of 270 (1.85%), all `INVALID_VERDICT` with HTTP 200 | 0 of 8,460 |
| Response time at the same offered rates (1–6.6 TPS): median / p95 / p99 | 406 / 729 / 1,269 ms | 187 / 280 / 327 ms |
| One request, alone, inside the process (profiler) | 323 ms | 178 ms |
| API CPU at load | ≤ 0.9 of 12 cores | 0.76 cores average at 10 TPS (peak 2.1) |
| API private memory | 385 → 652 MB in 70 s, still rising | 538 MB idle; plateau 700–780 MB at 10 TPS |

## 1. Test environment and configuration

| Item | Value |
| --- | --- |
| Machine | Laptop, Intel Core i7-1255U (10 cores, 12 threads), 15.7 GB RAM, Windows 11 Home 23H2 (10.0.22631), on AC power, Balanced power plan |
| Other software | Oracle DB, Chrome and others kept running. Visual Studio was closed for the reported runs; see section 8 for why |
| API | obsIcaoApi, .NET 9.0.6 runtime, Server GC (project default), Release builds of the original and optimized source, each run from its own folder on port 7006, freshly started before each run |
| SDK | Innovatrics IFace 3.1.0.2 (`C:\Windows\System32\IFace.dll`), license `onejkit.lic` for this machine's hardware ID |
| Load generator | Apache JMeter 5.6.3, Java 20.0.1, non-GUI, on the same laptop, heap 128–384 MB |
| Test plan | `tests\jmeter\verifyphoto_stress_test.jmx` with `config\local.properties` |
| Load profile | Open model: linear ramp 1 → 10 TPS over 120 s, then 10 TPS for 780 s (15 min, 8,460 requests). At most 40 requests in flight |
| Input | `deploy\samples\face.jpg` (1024×1024 JPEG, 559,794 bytes) for every request. Verdict `unverified`; response 4,194,870 bytes |
| Success rule | HTTP 200, `result.message` is `verified`/`unverified`, same verdict as the baseline request, and the body MD5-identical to the baseline response |

Both builds were run back to back with the same plan, settings, input and conditions. Available memory never fell below 2.5 GB in either reported run.

## 2. Baseline (original code)

Run `results\20261019-163843-baseline-original-r2`. It stopped automatically after 72 s: MAX_BAD_VERDICTS was reached (3 invalid results).

| 10 s window | Planned TPS | Successful TPS | Failed |
| --- | --- | --- | --- |
| 0–40 s | 1.4 → 3.6 | 1.4 → 3.6 | 0 |
| 40–50 s | 4.4 | 4.3 | 0 |
| 50–60 s | 5.1 | 5.1 | 0 |
| 60–70 s | 5.9 | 5.7 | 1 |
| 70–80 s | 6.6 | 1.5 (stopping) | 4 |

- **Requests**: 270 sent, 265 successful, 5 failed (1.85%). Every failure was HTTP 200 with `result.message` = `Object is currently in use elsewhere.`
- **Response time** (ms): mean 436, median 406, p90 567, p95 729, p99 1,269, max 1,334.
- **Resources**: API CPU at most 0.9 of 12 cores. Private memory 385 → 652 MB in 70 s. Peak 571 handles, 39 threads.

An earlier baseline run (`20261019-162757-baseline-original`), made while Visual Studio was open and memory was short, stopped the same way at about 4.4 TPS. Its p99 was 6,064 ms.

## 3. Optimized build

Run `results\20261019-164039-optimized-pool4-r2`: **PASSED**, and the schedule completed without a safety stop.

| Phase | Requests | Successful | Successful TPS | Mean | Median | p90 | p95 | p99 | Max |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Ramp 1 → 10 TPS, 120 s | 660 | 660 | tracked the plan in every window (last 9.6 of planned 9.6) | 202 | 194 | 231 | 240 | 310 | 351 |
| Sustained 10 TPS, 780 s | 7,800 | 7,800 | **10.00** (lowest 10 s window 9.8, highest 10.2) | 249 | 239 | 303 | 337 | 435 | 529 |

Response times are in ms. The JMeter dashboard gives the same values within a few ms; it uses a different percentile method.

- **Offered vs completed**: 8,460 planned, 8,460 sent, 8,460 successful. Send lag was 0 ms for every request, so the load generator kept the schedule.
- **Errors**: none. No timeouts, no HTTP errors, no invalid or mismatched verdicts, and no body differences from the baseline.
- **Resources**:
  - API CPU averaged 0.76 cores in the sustained phase (peak 2.1).
  - Private memory was 538 MB idle. It settled at 700–780 MB within 2 minutes and stayed there (peak 811).
  - Handles peaked at 606 and threads at 39.
  - Machine CPU averaged 9% (peak 58%). Available memory never went below 2.6 GB.
  - JMeter used 0.11 cores and 538 MB.

**Headroom probe** (`20261019-165639-optimized-headroom-15tps`): a ramp from 10 to 15 TPS over 60 s, then 15 TPS for 120 s, all on the same build.

- **Requests**: all 2,550 were successful verifications, sustained at 15.00/s.
- **Response time at 15 TPS** rose to median 649 ms, p95 2,453 ms and p99 2,627 ms. The pool of 4 face handlers is close to saturation there.
- **API resources**: 1.9 cores and up to 1,010 MB of private memory.

On this laptop the build therefore has about 50% headroom above 10 TPS before queueing dominates.

## 4. What made verifyPhoto slow, and what verifyFinger_v1 does differently

I compared the two endpoints in the source, then timed each step of the verifyPhoto path in a separate profiling program. That program used the same DLLs, license and image, measured without HTTP.

| Finding | Evidence | Action |
| --- | --- | --- |
| **A new `FaceHandler` per request.** `IcaoFace.verifyIcao` calls `loadICAO`, which creates a new IFace `FaceHandler` every time and never disposes it. verifyFinger_v1 uses one `ISegLib` created at startup | Creating a handler costs **140 ms** and **74 MB** of native memory. Detection with a new handler takes 289 ms, against 149 ms with a reused one. The leaked handlers are only freed by the finalizer: in the baseline run, API memory rose from 385 to 652 MB within 70 s at 1–6 TPS | Handlers are created once, at startup, and reused |
| **Shared mutable state.** One `static IcaoFace` serves every request, and each call overwrites its `fc` (handler), `redPen` and `par` fields while other requests are still using them | IL of the shipped `obsDLL_v3.dll`: `verifyIcao` reads `fc`, `redPen` and `par`, which `loadICAO` rewrites per call. Under load: `Object is currently in use elsewhere.` (GDI+ objects used from two threads at once), returned as HTTP 200 | Each request uses its own handler (from the pool) and its own `Pen`; nothing mutable is shared |
| **The SDK call mostly waits on memory, not CPU.** IFace runs single-threaded per call (`global.thrd.num = 1`, `management_mode = min_memory`, the SDK defaults) | `DetectFaces` takes 150–185 ms of wall time but only 9–94 ms of CPU. It causes about **5,300 page faults per call** (~21 MB of fresh memory) and no disk I/O | Requests run in parallel on separate handlers. Throughput scales with handlers, at little CPU cost. This is also why the original never used more than 0.9 cores |
| **Separate handlers are safe in parallel** (no vendor documentation was found for IFace 3.1) | The profiler ran 1–12 threads with one handler each: 0 errors and 0 result differences over several hundred operations. The JMeter runs then gave 11,000+ requests identical to the baseline | A bounded pool: each handler is used by one request at a time and is never shared concurrently |
| **No bound on concurrency.** Every request ran the SDK at once | Nothing limited how many requests were inside the SDK, each creating its own 74 MB handler. The earlier investigation saw failures with 8–30 requests in flight | `VerifyPhoto:MaxConcurrency` (default 4) caps detections in progress. Other requests wait asynchronously, without holding a thread |
| **Undisposed native objects** (`Face[]`, `Graphics`, the result `Bitmap`) | GDI+ and IFace objects left to the finalizer | Disposed deterministically. The result image is released when the response has been written |
| **Large garbage per response.** BMP saved into a growing `MemoryStream`, copied with `ToArray`, then turned into an 8.4 MB Base64 string | 14 MB allocated per response (measured). In the profiler, GC pauses took up to 12% of wall time at 4 threads | BMP buffer sized up front; the bytes are serialized as Base64 directly by System.Text.Json. Same JSON, about 3–4 MB per response instead of 14 MB |
| License re-initialized on every request | 0.04 ms: the SDK throws `LicenseAlreadyInitialized`, which `loadICAO` swallows | No separate change needed: the license is loaded once at startup by `LicenseChecker`, as before |
| JPEG decode (37 ms), multipart copy (0.1 ms), JSON (13 ms) | Small next to detection, or no measurable gain from changing | Not changed |

Kept deliberately:

- the image clone and drawing step
- the uncompressed BMP in the response
- the HTTP 200 answer for processing errors
- the SDK's global settings

They are part of the API contract or of the SDK's configured behaviour.

## 5. Changes

| File | Change | Why |
| --- | --- | --- |
| `obsICao\FacePhotoVerifier.cs` (new) | Runs the verifyPhoto check with the same steps and settings as `IcaoFace.verifyIcao`: clone, detect (eye distance 25–400, up to 10 faces), all features, draw every feature but the last, then `IcaoFace.VerifyIcao` from the DLL for the compliance verdict. It does this on a fixed pool of `FaceHandler`s with `SemaphoreSlim` limits, a per-request `Pen`, and disposal of faces, `Graphics` and handlers. A handler that hits an SDK exception is replaced, not reused | Removes the per-request 140 ms / 74 MB handler, the shared-state race and the unbounded concurrency |
| `obsICao\Worker.cs` | `verifyPhoto` calls the verifier. The pool is created and warmed at startup (only when the face license is valid) and disposed at shutdown. The result image is registered for disposal after the response. The BMP buffer is pre-sized and serialized from bytes. `IConfiguration` is injected for the pool size | Wiring for the above; less garbage per response |

There are no changes to:

- `obsDLL_v3` (binary or source)
- `verifyFinger`, `verifyFinger_v1`, `Hello`, `checkLicense`
- the license handling, the SDK or the routes
- the request or response format, or the status codes

**Setting**: `VerifyPhoto:MaxConcurrency` sets how many detections run at once. Set it in `appsettings.json` (`"VerifyPhoto": { "MaxConcurrency": 4 }`) or with the environment variable `VerifyPhoto__MaxConcurrency`. The default is 4, or the number of logical CPUs if that is lower. Each handler holds ~74 MB.

**Behaviour differences**: one, in an error path that did not occur in testing. If the SDK cannot create a face handler at all, the original returned HTTP 400 with `Object reference not set to an instance of an object.`; the new code returns HTTP 400 with the SDK's error message. The status code is unchanged.

## 6. Regression and compatibility checks

| Check | Result |
| --- | --- |
| `tests\regression`: 21 requests covering every verifyPhoto path (6 photo variants, PNG, 32-bit, 8-bit indexed, no face, 16×16 image, not an image, empty upload, no file, repeats) plus Hello, checkLicense, verifyFinger, verifyFinger_v1 | **21 / 21 identical** (status, content type, body bytes): original Release vs optimized Release, and original vs the delivered Debug build in `obsICao\bin\Debug` |
| Responses during load (strict MD5 check against the baseline) | 8,460 / 8,460 identical (10 TPS run), 2,550 / 2,550 (15 TPS probe) |
| Build | Release and Debug build without errors. 16 warnings, all CA1416 (Windows-only API), the same kind the original code already has |
| Test plan validation | Smoke run 20 / 20. Safety-stop dry run: the monitor wrote `STOP` on a forced memory limit, and JMeter stopped gracefully within 5 s |

The face image gives `unverified`. No compliant (`verified`) photo was available, so the `verified` branch is covered by code review only: it is the same `VerifyIcao` call from the DLL. Add a compliant photo to the regression set when one is available.

## 7. Acceptance criteria (optimized build, 10 TPS run)

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Reaches the target during ramp-up | pass | Successful TPS tracked the plan in every 10 s window of the ramp; first sustained window 10.0 |
| 2 | Sustains ≥ 10 successful verifications/s through the sustained phase | pass | 10.00/s over 780 s; every 10 s window between 9.8 and 10.2 |
| 3 | Error rate and resources within the safety limits | pass | 0% errors; no safety stop; no resource limit crossed |
| 4 | Results correct and consistent with the existing implementation | pass | Every response byte-identical to the baseline; regression 21/21 |
| 5 | Responsive and stable | pass | 0 timeouts; max response 529 ms; send lag 0; memory plateaued |

These thresholds are documented in `README.md` and set in `summarize-results.ps1`. No latency limit was given, so latency is reported, not judged.

## 8. Runs that did not count, and why

The first optimized run (`20261019-162958-optimized-pool4`) ran clean through the ramp and 40 s at 10 TPS:

- 0 errors
- ramp p95 286 ms

Then, at 16:32:38, Visual Studio started its background services (Roslyn analysis, the Copilot language server, MSBuild, IntelliCode; about 2 GB together). Available memory fell to 95 MB and machine CPU reached 86%. Response times rose to 5–6 s, and the monitor's low-memory limit stopped the run.

The API's own memory was flat until then; the cause was the laptop. Visual Studio was closed, and the baseline and optimized runs were both repeated under the same conditions (sections 2 and 3). This is also why JMeter's heap was reduced from 768 MB to 384 MB: it needs far less, and its default 1 GB took memory the API needed.

## 9. Limitations and recommendations

1. **Server validation is still to do.** These numbers come from a laptop, with JMeter on the same machine. Follow `README.md` section 7: publish Release, run the regression check, then the smoke and full runs with JMeter on a separate machine near the server.
2. **Free memory matters.** Each detection touches ~21 MB of fresh memory inside the SDK. When the machine is short of RAM, detection slows sharply (section 8). Plan for the API's ~0.8 GB at 10 TPS (1 GB at 15 TPS) **plus** at least 1–2 GB free on the server, and keep the monitor's memory limit on during tests.
3. **Network.** Each response is still a 4.2 MB Base64 BMP (the contract), which is ~42 MB/s (~340 Mbit/s) at 10 TPS. On the server this is likely to become the limit before the API is. A smaller image format, or HTTP response compression, would need agreement with the Flutter app team.
4. **Capacity and pool size.** With 4 handlers this laptop carries 15 TPS, but with p95 of 2.5 s. For higher targets, raise `VerifyPhoto:MaxConcurrency` (~74 MB each) and measure; only 4 was load-tested.
5. **Soak test.** Memory plateaued at 700–780 MB during 13 minutes. Run 60 minutes at 10 TPS to confirm there is no slow growth.
6. **Ask Innovatrics**, as IFace 3.1 has no public threading documentation:
   - confirm that separate `FaceHandler`s may run concurrently (it worked without a single difference in testing)
   - ask whether a `global.thrd.management_mode` other than `min_memory` reduces the per-call memory churn without changing results; it was left at the default
7. **Combined load.** verifyFinger_v1 responses are unchanged, but face and fingerprint have not been load-tested together since this change. The earlier package's `face-and-finger` mode can do that.
8. **Clients.** Processing errors still come back as HTTP 200 with the error text in `result.message`, unchanged for compatibility. Clients and monitoring must check the verdict, not only the status code.

## 10. Reproduce

```powershell
cd C:\mkpulga\obs_API\tests\jmeter
.\scripts\run-verifyphoto-test.ps1 -Config config\local.properties,config\smoke.properties -Label smoke   # ~20 requests
.\scripts\run-verifyphoto-test.ps1 -Label full                                                           # 15 minutes
```

Each run writes `results\<time>-<label>\summary.md` and `dashboard\index.html`. The runs cited here are in `tests\jmeter\results\` (git-ignored). Profiling figures in section 4 come from a separate measurement program run outside the repository, against the same DLLs.
