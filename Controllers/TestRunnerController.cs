using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using IncentivePortal.Models;
using IncentivePortal.Data;

namespace IncentivePortal.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class TestRunnerController : Controller
{
    private readonly IWebHostEnvironment _env;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public TestRunnerController(IWebHostEnvironment env)
    {
        _env = env;
    }

    // =========================================================
    // INDEX — RENDER TEST SUITE DASHBOARD
    // =========================================================
    public IActionResult Index()
    {
        var resultsDir = Path.Combine(_env.WebRootPath, "testresults");
        var trxPath = Path.Combine(resultsDir, "test_results.trx");
        
        TestRunReport? report = null;
        if (System.IO.File.Exists(trxPath))
        {
            try
            {
                report = ParseTrxFile(trxPath);
                var consolePath = Path.Combine(resultsDir, "console_output.txt");
                if (System.IO.File.Exists(consolePath))
                {
                    report.ConsoleOutput = System.IO.File.ReadAllText(consolePath);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error parsing test results: " + ex.Message;
            }
        }

        return View(report);
    }

    // =========================================================
    // RUN TESTS — EXECUTE DOTNET TEST VIA PROCESS
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunTests()
    {
        // Try acquiring lock instantly to prevent concurrent processes
        if (!await _semaphore.WaitAsync(0))
        {
            return Json(new { success = false, message = "Another unit test execution is currently in progress. Please wait." });
        }

        try
        {
            var testProjectDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "IncentivePortal.Tests"));
            var resultsDir = Path.Combine(_env.WebRootPath, "testresults");
            
            if (!Directory.Exists(resultsDir))
            {
                Directory.CreateDirectory(resultsDir);
            }

            var trxFile = Path.Combine(resultsDir, "test_results.trx");
            var consoleFile = Path.Combine(resultsDir, "console_output.txt");

            if (System.IO.File.Exists(trxFile))
            {
                System.IO.File.Delete(trxFile);
            }
            if (System.IO.File.Exists(consoleFile))
            {
                System.IO.File.Delete(consoleFile);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{testProjectDir}\\IncentivePortal.Tests.csproj\" --logger:\"trx;LogFileName=test_results.trx\" --results-directory \"{resultsDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(testProjectDir)
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var fullConsole = stdout + "\n" + stderr;
            await System.IO.File.WriteAllTextAsync(consoleFile, fullConsole);

            if (System.IO.File.Exists(trxFile))
            {
                var report = ParseTrxFile(trxFile);
                report.ConsoleOutput = fullConsole;
                return Json(new { success = true, report });
            }
            else
            {
                return Json(new { success = false, message = "Tests executed but results file was not found. Please review console logs.", console = fullConsole });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Exception executing tests: " + ex.Message });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // =========================================================
    // HELPER — PARSE TRX XML REPORT
    // =========================================================
    private TestRunReport ParseTrxFile(string filePath)
    {
        var doc = XDocument.Load(filePath);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        var run = doc.Root;
        var times = run?.Element(ns + "Times");
        var creationTime = times?.Attribute("creation")?.Value;
        var startTime = times?.Attribute("start")?.Value;
        var finishTime = times?.Attribute("finish")?.Value;

        var resultSummary = run?.Element(ns + "ResultSummary");
        var outcome = resultSummary?.Attribute("outcome")?.Value ?? "Unknown";
        var counters = resultSummary?.Element(ns + "Counters");

        var report = new TestRunReport
        {
            RunId = run?.Attribute("id")?.Value ?? "",
            RunName = run?.Attribute("name")?.Value ?? "",
            Outcome = outcome,
            CreationTime = DateTime.TryParse(creationTime, out var ct) ? ct : DateTime.MinValue,
            StartTime = DateTime.TryParse(startTime, out var st) ? st : DateTime.MinValue,
            FinishTime = DateTime.TryParse(finishTime, out var ft) ? ft : DateTime.MinValue,
            TotalTests = int.TryParse(counters?.Attribute("total")?.Value, out var total) ? total : 0,
            Executed = int.TryParse(counters?.Attribute("executed")?.Value, out var exec) ? exec : 0,
            Passed = int.TryParse(counters?.Attribute("passed")?.Value, out var pass) ? pass : 0,
            Failed = int.TryParse(counters?.Attribute("failed")?.Value, out var fail) ? fail : 0,
            Skipped = int.TryParse(counters?.Attribute("skipped")?.Value, out var skip) ? skip : 0,
        };

        var resultsNode = run?.Element(ns + "Results");
        if (resultsNode != null)
        {
            foreach (var resultEl in resultsNode.Elements(ns + "UnitTestResult"))
            {
                var testName = resultEl.Attribute("testName")?.Value ?? "";
                var durationStr = resultEl.Attribute("duration")?.Value ?? "";
                var resultOutcome = resultEl.Attribute("outcome")?.Value ?? "";
                var errorInfo = resultEl.Element(ns + "Output")?.Element(ns + "ErrorInfo");
                var errorMessage = errorInfo?.Element(ns + "Message")?.Value;
                var stackTrace = errorInfo?.Element(ns + "StackTrace")?.Value;

                var duration = TimeSpan.Zero;
                if (TimeSpan.TryParse(durationStr, out var d))
                {
                    duration = d;
                }

                report.TestResults.Add(new TestResultDetail
                {
                    TestName = testName,
                    Outcome = resultOutcome,
                    DurationMs = (int)duration.TotalMilliseconds,
                    ErrorMessage = errorMessage,
                    StackTrace = stackTrace
                });
            }
        }

        return report;
    }
}
