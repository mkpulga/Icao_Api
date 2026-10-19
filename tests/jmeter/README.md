# verifyPhoto stress test (Apache JMeter)

Load test for `POST /verifyPhoto` of obsIcaoApi: a linear ramp up to the target rate (10 TPS by default), then sustained load, in 15 minutes. Every request uploads a real face image as `multipart/form-data`. The API runs its full ICAO check with the Innovatrics SDK; nothing is mocked.

| Path | What it is |
| --- | --- |
| `verifyphoto_stress_test.jmx` | The test plan (JMeter 5.6.3, no plugins) |
| `config\local.properties` | Settings for an API on this PC (default) |
| `config\smoke.properties` | Overlay: 1 TPS for 20 s, to check the setup before a real run |
| `config\server.example.properties` | Template for a server run; copy it to `server.properties`, which is git-ignored |
| `config\results.properties` | Results columns and dashboard settings, always applied |
| `scripts\run-verifyphoto-test.ps1` | Runs a test: monitor, JMeter in non-GUI mode, dashboard, summary |
| `scripts\monitor-resources.ps1` | Records CPU, memory, handles and threads, and stops the test at resource limits |
| `scripts\summarize-results.ps1` | Writes `summary.md` and `summary.json` for a run |
| `scripts\generate-dashboard.ps1` | Rebuilds the HTML dashboard from a run's `results.jtl` |
| `scripts\stop-test.ps1` | Stops a running test gracefully |
| `results\` | One folder per run, git-ignored |

## 1. Install Java and JMeter (Windows, by hand)

The plan was built and run with **Apache JMeter 5.6.3**. JMeter 5.6.3 needs **Java 8 or later**; use a current LTS JDK, **Java 17 or 21**. Already installed? Skip to the checks at the end of this section.

**Java (Eclipse Temurin, free OpenJDK build):**

1. Open https://adoptium.net/temurin/releases/ and choose Windows, x64, JDK, version 21 (or 17), `.msi`.
2. Run the installer. On the *Custom Setup* page, set **Add to PATH** and **Set JAVA_HOME variable** to "Will be installed on local hard drive".
3. Open a **new** PowerShell window and check:

   ```powershell
   java -version          # should print 21.x (or 17.x)
   $env:JAVA_HOME         # should print the JDK folder
   ```

**JMeter:**

1. Open https://jmeter.apache.org/download_jmeter.cgi. Download `apache-jmeter-5.6.3.zip` and its `.sha512` file. Older releases are under "Archives" (https://archive.apache.org/dist/jmeter/binaries/).
2. Check the download, then unzip it:

   ```powershell
   Get-FileHash .\apache-jmeter-5.6.3.zip -Algorithm SHA512    # compare with the .sha512 file
   Expand-Archive .\apache-jmeter-5.6.3.zip -DestinationPath C:\tools
   [Environment]::SetEnvironmentVariable('JMETER_HOME', 'C:\tools\apache-jmeter-5.6.3', 'User')
   ```

3. Open a new PowerShell window and check:

   ```powershell
   & "$env:JMETER_HOME\bin\jmeter.bat" --version     # prints the JMeter 5.6.3 banner
   ```

No plugins are needed. If PowerShell refuses to run the scripts, allow local scripts for your user with `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`, or start each one with `powershell -ExecutionPolicy Bypass -File <script>`.

## 2. Why non-GUI mode

Run load tests from the command line (`jmeter -n`, which the run script does), not the JMeter window. The GUI redraws listeners and keeps results in memory while it runs. At 10 TPS with 4 MB responses, that takes CPU and heap from the load generator, which then delays requests and inflates response times; the numbers would partly measure JMeter, not the API. Use the GUI only to read or edit the plan (**File → Open**, then the `.jmx`). Keep its *View Results Tree* listener disabled.

## 3. Run a test

From this folder (`tests\jmeter`), in PowerShell:

```powershell
# 1. Smoke test: about 20 requests at 1 TPS. Checks the URL, image, license and response.
.\scripts\run-verifyphoto-test.ps1 -Config config\local.properties,config\smoke.properties -Label smoke

# 2. Full test: 2 min ramp 1 -> 10 TPS, then 13 min at 10 TPS (15 min, ~8,500 requests).
.\scripts\run-verifyphoto-test.ps1 -Label full
```

The script shows the effective settings and asks you to type `YES` before it sends load (`-Yes` skips that). Before a full run:

- Start the API outside Visual Studio (the debugger slows it down), and close large applications: the API, JMeter and the monitor need about 1.5 GB of free memory together.
- Run the smoke test first.

To change a setting for one run, add `-Set NAME=value` (repeat as needed), for example `-Set TARGET_TPS=12,SUSTAIN_SECONDS=300`. For a different environment, put the settings in a properties file and pass it after `config\local.properties`. Later files override earlier ones.

**Credentials and secrets.** The API has no authentication today, so the plan sends none. If a gateway or token is added later, never write it into the `.jmx` or a committed properties file. Keep it in a git-ignored file (`config\*.local.properties`, `config\server.properties`) or pass it at run time with `-Set`.

**Generating the dashboard by hand.** The run script builds it with `-e -o`. The equivalent commands:

```powershell
# run and build the dashboard in one go
& "$env:JMETER_HOME\bin\jmeter.bat" -n -t verifyphoto_stress_test.jmx -q config\local.properties -q config\results.properties `
  -l results\manual\results.jtl -j results\manual\jmeter.log -e -o results\manual\dashboard -JRESULTS_DIR="$PWD\results\manual"
# rebuild a dashboard from an existing results file
.\scripts\generate-dashboard.ps1 -RunDir results\<run folder>
```

## 4. What the test does

1. **setUp**: checks the settings and the image file, calls `GET /checkLicense` (the face engine must be valid), then sends one **baseline** `verifyPhoto` request. Any failure stops the test before load starts.
2. **Load**: requests follow an **open-model schedule**. Request *n* is sent when the rate profile has produced *n* arrivals (linear ramp from `RAMP_START_TPS` to `TARGET_TPS` over `RAMP_UP_SECONDS`, then `TARGET_TPS` for `SUSTAIN_SECONDS`). The offered rate therefore does not drop when the API slows down. Slowness shows up as response time and, once every thread is busy, as send lag. `MAX_IN_FLIGHT` threads (40) cap the requests in progress; they keep 10 TPS while responses take under 4 s.
3. **tearDown**: writes `run-info.properties` (plan, baseline, how the run ended).

**What counts as a successful verification.** HTTP 200 is not enough: the API also answers 200 when the check itself failed, with the error text in `result.message`. A request succeeds only when all of these hold:

| Check | Failure class |
| --- | --- |
| A response arrived before `RESPONSE_TIMEOUT_MS` | `TIMEOUT`, `CONNECTION_ERROR` |
| HTTP 200 | `HTTP_<code>` |
| `result.message` is `verified` or `unverified` | `INVALID_VERDICT` (e.g. `Object is currently in use elsewhere.`) |
| A complete `base64` image field | `INVALID_RESPONSE` |
| The verdict equals `EXPECTED_MESSAGE`, or the baseline's verdict | `VERDICT_MISMATCH` |
| `STRICT_BODY_MATCH=true`: body identical (MD5) to the baseline response | `BODY_MISMATCH` |

The same image always produces the same response, so the strict body check catches corrupted or mixed-up results (for example, feature marks drawn from another request) that a verdict check alone would miss.

## 5. Safety limits

All limits are settings. The defaults are conservative: they stop a run within seconds of trouble rather than push a failing API further. Every stop is **graceful**: no new requests are sent, requests already sent finish or time out, then tearDown, the dashboard and the summary still run. The reason is printed, written to `stop-reason.txt`, and shown in `summary.md`.

| Condition | Metric | Default threshold | Window | Set by |
| --- | --- | --- | --- | --- |
| Error rate | failed requests (any class) | > 5% | last 100 requests (~10 s at 10 TPS), after 30 have completed | `ERROR_RATE_PERCENT`, `ERROR_WINDOW`, `ERROR_MIN_SAMPLES` |
| API down / refusing | consecutive failures | 5 in a row | consecutive | `MAX_CONSECUTIVE_FAILURES` |
| API unresponsive | timeouts (no full response within 30 s) | 3 | last 100 requests | `MAX_TIMEOUTS_IN_WINDOW`, `RESPONSE_TIMEOUT_MS` |
| API stalling | responses slower than 10 s | 5 in a row | consecutive | `SLOW_RESPONSE_MS`, `MAX_CONSECUTIVE_SLOW` |
| API not keeping up | send lag (all 40 threads busy) | > 5 s late | per request | `MAX_SCHEDULE_LAG_MS`, `MAX_IN_FLIGHT` |
| Unstable verification | `INVALID_VERDICT`, `VERDICT_MISMATCH`, `BODY_MISMATCH`, `INVALID_RESPONSE` | 3 in the run | whole run | `MAX_BAD_VERDICTS` |
| API process gone | process `obsIcaoApi` exits | immediately | 5 s samples | monitor |
| Machine memory pressure | available physical memory | < 500 MB | 3 samples in a row (15 s) | monitor `-MinAvailableMemoryMB` |
| SDK / native memory growth | API private bytes | > 3,000 MB | any sample | monitor `-MaxApiPrivateMB` |
| Handle leak | API handle count | > 20,000 | any sample | monitor `-MaxApiHandles` |
| Thread explosion | API thread count | > 500 | any sample | monitor `-MaxApiThreads` |
| CPU saturation | total CPU | off by default (a busy CPU is a measurement, not a hazard); e.g. `-MaxCpuPercent 95` | 6 samples in a row (30 s) | monitor `-MaxCpuPercent` |
| Run too long | wall clock | 1,200 s for the load phase | | `HARD_MAX_SECONDS` |
| Operator | `.\scripts\stop-test.ps1 [-Reason "..."]` | | | manual; add `-Now` to also send JMeter's own Shutdown |

**How the stops work.** JMeter-side conditions are evaluated by the plan's *Safety monitor* listener on every completed request, and by the *Arrival scheduler* for send lag. Resource conditions come from `monitor-resources.ps1`, which runs on the API machine every 5 s: on a breach it writes the reason to the run's `STOP` file. The plan checks for that file every 2 s and stops. When JMeter runs on a different machine from the API, the monitor must write to a folder the JMeter machine can read (a share) via `-StopFile`. Otherwise, watch the monitor console on the server and stop the test from the JMeter machine with `stop-test.ps1`.

**Choosing thresholds for another server.** Set `-MinAvailableMemoryMB` to leave room for everything else on that server, and keep `-MaxApiPrivateMB` well below its RAM. Each concurrent verifyPhoto request needs about 74 MB of SDK memory (one face handler) plus ~15 MB of buffers. The API at rest with 4 handlers uses ~500 MB private memory.

**Before resuming after a stop:**

1. Read `stop-reason.txt` and the *Failures* table in `summary.md` (failure class and first message).
2. Confirm the API is healthy: `curl.exe http://<host>:7006/Hello`, then a single request (`smoke.properties`). Restart it if it does not answer.
3. Check `resources.csv` for a trend that led to the stop: private memory climbing and not returning, handles climbing, or available memory falling.
4. Check the API's console output, or Event Viewer → Windows Logs → Application when it runs as a service.
5. Fix the cause, or lower the load (`TARGET_TPS`) to find the level the API sustains. Do not just raise the threshold that tripped.

## 6. Read the results

Each run writes `results\<yyyyMMdd-HHmmss>-<label>\`:

| File | Contents |
| --- | --- |
| `summary.md` | Acceptance criteria, successful TPS per phase and per 10 s window against the plan, response-time percentiles, failures by class, CPU and memory |
| `dashboard\index.html` | JMeter's HTML dashboard: throughput, response-time percentiles and distribution, errors, over-time graphs. Labels `verifyPhoto_ramp` and `verifyPhoto_sustain` separate the phases |
| `results.jtl` | One CSV row per request, with `PHASE`, `TARGET_TPS_NOW`, `DUE_MS`, `LAG_MS`, `VERDICT`, `FAIL_CLASS` |
| `resources.csv`, `monitor.log` | System CPU and available memory; API CPU, private memory, handles and threads; JMeter CPU and memory, every 5 s |
| `run-info.properties`, `stop-reason.txt`, `jmeter.log` | How the run ended and why |
| `settings-effective.properties`, `config-*.properties` | The exact settings used |

In the dashboard, *Statistics* and *Errors* give counts, errors and percentiles per label. *Charts → Over Time* shows throughput and response time across the run. JMeter's *Throughput* counts all completed requests, failed ones included; `summary.md` counts only successful verifications.

**Acceptance criteria** (evaluated in `summary.md`; the thresholds are parameters of `summarize-results.ps1`):

1. The target is reached during ramp-up: every full ramp window and the first sustained window deliver at least 90% of the planned rate as successful verifications.
2. Sustained: at least 99.5% of `TARGET_TPS` as an average over the sustained phase (allowing for window edges), and every full 10 s window at least 90% of `TARGET_TPS`.
3. Failed requests at most 1%, and no safety stop.
4. No invalid or inconsistent verification results.
5. No timeouts, the schedule completed, p99 send lag at most 1 s (the load generator kept up), and no resource limit was crossed.

Response times are reported (average, median, p90, p95, p99) but not judged: no latency target has been set.

## 7. Validate the same build on the server

1. **Publish** the build you tested: `dotnet publish ..\..\obsICao\obsIcaoApi.csproj -c Release -r win-x64 --self-contained true -o <folder>`. Copy it to the server the same way as the existing deployment (`deploy\setup.text.txt`). The server needs its own `onejkit.lic` for its hardware ID.
2. **Size the face pool** for the server. `VerifyPhoto:MaxConcurrency` is how many face checks run at once: add `"VerifyPhoto": { "MaxConcurrency": 4 }` to `appsettings.json`, or set the `VerifyPhoto__MaxConcurrency` environment variable on the service. The default is 4, or the number of CPUs if that is lower. Each one holds ~74 MB of SDK memory. With 4, the development laptop carried 10 TPS at p95 337 ms and 15 TPS at p95 2.5 s (`PERFORMANCE-REPORT.md`). Start with the default.
3. **Check it** on the server: `curl.exe http://localhost:7006/checkLicense` must show `"face":{"valid":true`, and the log must show `verifyPhoto: N face handlers ready`.
4. **Regression check**: run `..\regression\capture-responses.ps1` against the server and compare it with a capture from your PC (`..\regression\compare-responses.ps1`). The verdicts and images must match.
5. **Load generator**: run JMeter on a separate machine near the server, not on the API server and not across a slow link. At 10 TPS the responses come to ~42 MB/s (~340 Mbit/s); the network must carry that, or response times will measure the link.
6. **Monitor on the server**: `powershell -ExecutionPolicy Bypass -File monitor-resources.ps1 -OutFile C:\loadtest\server-resources.csv -StopFile \\<jmeter-pc>\share\STOP`, or without `-StopFile` while you watch it.
7. **Run**: copy `config\server.example.properties` to `config\server.properties` and fill it in. Run the smoke test, then the full test with `-ApiOnThisMachine:$false`. Then copy `server-resources.csv` into the run folder as `resources.csv` and rerun `summarize-results.ps1` to include it.
