using System.Diagnostics;
using UnityEngine;

/// <summary>Low-overhead hitch diagnostics for development builds.</summary>
public static class CampaignPerformanceTrace
{
    public static long Stamp() => Stopwatch.GetTimestamp();

    public static double MillisecondsSince(long stamp) =>
        (Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency;

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Report(string area, double milliseconds, string details = null, double thresholdMs = 4.0)
    {
        if (milliseconds < thresholdMs) return;
        UnityEngine.Debug.LogWarning("[CampaignPerf] " + area + " " + milliseconds.ToString("0.00") + " ms" +
            (string.IsNullOrEmpty(details) ? string.Empty : " | " + details));
    }
}
