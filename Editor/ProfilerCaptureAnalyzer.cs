#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

[InitializeOnLoad]
internal static class ProfilerCaptureAnalyzer
{
    private const string SessionKey = "ProjectX.ProfilerCaptureAnalyzer.Running";
    private static int attempts;

    static ProfilerCaptureAnalyzer()
    {
        EditorApplication.delayCall += Begin;
    }

    private static void Begin()
    {
        string capture = Path.GetFullPath(Path.Combine(Application.dataPath, "BattleTestResults/profilerdata.data"));
        string report = Path.GetFullPath(Path.Combine(Application.dataPath, "BattleTestResults/profiler-analysis.txt"));
        if (!File.Exists(capture) || File.Exists(report) || SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        ProfilerDriver.LoadProfile(capture, false);
        attempts = 0;
        EditorApplication.update += WaitForProfile;
    }

    private static void WaitForProfile()
    {
        attempts++;
        if (ProfilerDriver.lastFrameIndex < ProfilerDriver.firstFrameIndex)
        {
            if (attempts < 600) return;
            Finish("Profiler capture did not become available after loading.");
            return;
        }

        try { Analyze(); }
        catch (Exception exception) { Finish(exception.ToString()); }
    }

    private static void Analyze()
    {
        EditorApplication.update -= WaitForProfile;
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        List<FrameRecord> frames = new List<FrameRecord>();
        for (int frameIndex = first; frameIndex <= last; frameIndex++)
        {
            using (RawFrameDataView frame = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (!frame.valid) continue;
                frames.Add(new FrameRecord { Index = frameIndex, Milliseconds = frame.frameTimeMs });
            }
        }

        List<FrameRecord> worst = frames.OrderByDescending(item => item.Milliseconds).Take(30).ToList();
        Dictionary<string, SampleAggregate> samples = new Dictionary<string, SampleAggregate>(StringComparer.Ordinal);
        for (int w = 0; w < worst.Count; w++)
        {
            using (RawFrameDataView frame = ProfilerDriver.GetRawFrameDataView(worst[w].Index, 0))
            {
                if (!frame.valid) continue;
                for (int sampleIndex = 0; sampleIndex < frame.sampleCount; sampleIndex++)
                {
                    string name = frame.GetSampleName(sampleIndex);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!samples.TryGetValue(name, out SampleAggregate aggregate))
                    {
                        aggregate = new SampleAggregate();
                        samples[name] = aggregate;
                    }
                    float time = frame.GetSampleTimeMs(sampleIndex);
                    aggregate.TotalMilliseconds += time;
                    aggregate.MaximumMilliseconds = Math.Max(aggregate.MaximumMilliseconds, time);
                    aggregate.Calls++;
                }
            }
        }

        StringBuilder text = new StringBuilder(32768);
        text.AppendLine("Project X Unity profiler capture analysis");
        text.AppendLine("Frames: " + frames.Count + " (" + first + ".." + last + ")");
        if (frames.Count > 0)
        {
            List<float> ordered = frames.Select(item => item.Milliseconds).OrderBy(value => value).ToList();
            text.AppendLine(string.Format("Frame ms: median {0:F2}, p90 {1:F2}, p99 {2:F2}, max {3:F2}, mean {4:F2}",
                Percentile(ordered, .5f), Percentile(ordered, .9f), Percentile(ordered, .99f), ordered[ordered.Count - 1], ordered.Average()));
        }
        text.AppendLine();
        text.AppendLine("Worst frames:");
        foreach (FrameRecord frame in worst) text.AppendLine(string.Format("{0}: {1:F3} ms", frame.Index, frame.Milliseconds));
        text.AppendLine();
        text.AppendLine("Largest main-thread samples across the 30 worst frames (inclusive time):");
        foreach (KeyValuePair<string, SampleAggregate> entry in samples.OrderByDescending(item => item.Value.TotalMilliseconds).Take(120))
            text.AppendLine(string.Format("{0:F3} ms total | {1:F3} ms max | {2} calls | {3}",
                entry.Value.TotalMilliseconds, entry.Value.MaximumMilliseconds, entry.Value.Calls, entry.Key));

        Finish(text.ToString());
    }

    private static float Percentile(List<float> ordered, float percentile)
    {
        if (ordered.Count == 0) return 0f;
        return ordered[Mathf.Clamp(Mathf.RoundToInt((ordered.Count - 1) * percentile), 0, ordered.Count - 1)];
    }

    private static void Finish(string contents)
    {
        EditorApplication.update -= WaitForProfile;
        string report = Path.GetFullPath(Path.Combine(Application.dataPath, "BattleTestResults/profiler-analysis.txt"));
        File.WriteAllText(report, contents);
        SessionState.SetBool(SessionKey, false);
        Debug.Log("Profiler capture analysis written to " + report);
    }

    private sealed class FrameRecord { public int Index; public float Milliseconds; }
    private sealed class SampleAggregate { public float TotalMilliseconds; public float MaximumMilliseconds; public int Calls; }
}
#endif
