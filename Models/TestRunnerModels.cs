using System;
using System.Collections.Generic;

namespace IncentivePortal.Models;

public class TestRunReport
{
    public string RunId { get; set; } = string.Empty;
    public string RunName { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime FinishTime { get; set; }
    
    public int TotalTests { get; set; }
    public int Executed { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }

    public double PassRate => TotalTests > 0 ? (double)Passed / TotalTests * 100.0 : 0.0;
    
    public double TotalDurationMs => (FinishTime - StartTime).TotalMilliseconds;

    public List<TestResultDetail> TestResults { get; set; } = new();

    public string? ConsoleOutput { get; set; }
}

public class TestResultDetail
{
    public string TestName { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }

    public string ClassName
    {
        get
        {
            if (string.IsNullOrEmpty(TestName)) return string.Empty;
            var parts = TestName.Split('.');
            if (parts.Length > 1)
            {
                return parts[parts.Length - 2];
            }
            return string.Empty;
        }
    }

    public string MethodName
    {
        get
        {
            if (string.IsNullOrEmpty(TestName)) return string.Empty;
            var parts = TestName.Split('.');
            if (parts.Length > 0)
            {
                return parts[parts.Length - 1];
            }
            return string.Empty;
        }
    }
}
