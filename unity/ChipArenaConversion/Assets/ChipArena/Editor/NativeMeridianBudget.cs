using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        [MenuItem("Chip Arena/Benchmark Meridian candidates against A0")]
        public static void CheckMeridianCandidateBudgets() => MeasureMeridianCandidates(true);

        // Run the new blockout on the Windows builder once per geometry revision.
        // Relative timing is recorded for the owner walk and enforced by the existing
        // freeze benchmark. Incomplete samples are reported for their individual layouts.
        internal static void MeasureChangedMeridianCandidates()
        {
            const string asset = "Assets/ChipArena/Core/NativeMeridianStreetPlans.cs";
            string key = "ChipArena.MeridianWalkBenchmark." + Application.unityVersion;
            string revision = AssetDatabase.GetAssetDependencyHash(asset).ToString()
                + AssetDatabase.GetAssetDependencyHash("Assets/ChipArena/Core/NativeLayeredCityWalk.cs");
            if (EditorPrefs.GetString(key) == revision) return;
            MeasureMeridianCandidates(false);
            EditorPrefs.SetString(key, revision);
        }

        // The editor build normally uses -nographics. Run the actual Development
        // player separately so it can submit frames to the Windows graphics driver.
        internal static void ProfileMeridianPlayer(string exe,string buildRoot)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return;
            string key = "ChipArena.MeridianRendered." + Application.unityVersion;
            string revision = AssetDatabase.GetAssetDependencyHash("Assets/ChipArena/Core/NativeMeridianStreetPlans.cs").ToString()
                + AssetDatabase.GetAssetDependencyHash("Assets/ChipArena/Runtime/NativeExpeditionLoadPreview.cs")
                + AssetDatabase.GetAssetDependencyHash("Assets/ChipArena/Runtime/NativeArenaDressing.cs")
                + AssetDatabase.GetAssetDependencyHash("Assets/ChipArena/Core/NativeBlockLooks.cs");
            if (EditorPrefs.GetString(key) == revision) return;
            bool complete = true;
            for (int i = 0; i < NativeCityLayout.Plans.Count; i++)
            {
                string log = System.IO.Path.Combine(buildRoot,"meridian-candidate-"+i+".log");
                var start = new System.Diagnostics.ProcessStartInfo {
                    FileName = exe, UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(exe),
                    Arguments = "-force-d3d11 -noaudio -screen-fullscreen 0 -screen-width 1600 -screen-height 900 "
                        + "-expedition-load 4 -meridian-candidate "+i+" -logFile \""+log+"\""
                };
                using var player = System.Diagnostics.Process.Start(start);
                if (!player.WaitForExit(180000))
                {
                    player.Kill();
                    Debug.LogWarning("Meridian render sample timed out: "+log);
                    return;
                }
                int views = 0;
                var diagnostics = new List<string>();
                if (!System.IO.File.Exists(log))
                {
                    Debug.LogWarning("Meridian player produced no graphics log for "+NativeCityLayout.Plans[i].Name);
                    return;
                }
                foreach (var line in System.IO.File.ReadLines(log))
                {
                    if (line.StartsWith("MERIDIAN_VIEW ",StringComparison.Ordinal))
                    {
                        Debug.Log(line);
                        views++;
                    }
                    else if (diagnostics.Count < 12 && (line.Contains("Exception") || line.Contains("Meridian player")
                        || line.Contains("Direct3D") || line.Contains("Renderer:") || line.StartsWith("EXPEDITION_LOAD_PREVIEW")))
                        diagnostics.Add(line);
                }
                if (player.ExitCode != 0 || views != 12)
                {
                    complete = false;
                    Debug.LogWarning($"Meridian renderer could not complete {NativeCityLayout.Plans[i].Name}: exit {player.ExitCode}, {views}/12 views. See {log}\n{string.Join(Environment.NewLine, diagnostics)}");
                    continue;
                }
            }
            if (complete) EditorPrefs.SetString(key,revision);
        }

        private static void MeasureMeridianCandidates(bool freeze)
        {
            var mode = Physics.simulationMode;
            var gravity = Physics.gravity;
            var failures = new List<string>();
            double[] Measure(NativeCityLayout.Plan candidate)
            {
                using var fixture = new NativeExpeditionLoadFixture(4,
                    _ => EditorSceneManager.NewPreviewScene(), scene => EditorSceneManager.ClosePreviewScene(scene), candidate);
                int maxWireBytes = 0;
                for (int i = 0; i < NativeExpeditionLoadFixture.Warmup + NativeExpeditionLoadFixture.Samples; i++)
                {
                    fixture.Step();
                    if (i >= NativeExpeditionLoadFixture.Warmup && i % 3 == 0)
                    {
                        var packet = NativeNetCodec.Encode(new NativeNetMessage { type="frame", epoch=7, frame=fixture.Arena.NetworkFrame() });
                        maxWireBytes = Math.Max(maxWireBytes, System.Text.Encoding.UTF8.GetByteCount(packet));
                    }
                }
                string name = candidate?.Name ?? "A0";
                Debug.Log($"{fixture.Report()} layout={name} peakWireBytes={maxWireBytes} candidateGenerationMs={fixture.CandidateGenerationMs:F2}");
                if (!fixture.FullPopulation) failures.Add(name+" did not retain 88 live zombies on every measured tick; exclude this timing from freeze acceptance");
                if (maxWireBytes >= 1048576) failures.Add($"{name} peak area frame {maxWireBytes} bytes");
                if (fixture.CandidateGenerationMs > 1500) failures.Add($"{name} candidate generation {fixture.CandidateGenerationMs:F2} ms");
                return fixture.Means();
            }

            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.gravity = new Vector3(0, -23, 0);
                var baseline = Measure(null);
                string[] metrics = { "navigation", "crowd flow", "perception", "physics" };
                foreach (var candidate in NativeCityLayout.Plans)
                {
                    double[] measured;
                    try { measured = Measure(candidate); }
                    catch (InvalidOperationException e) when (!freeze)
                    {
                        failures.Add(candidate.Name+" native benchmark incomplete: "+e.Message);
                        continue;
                    }
                    for (int i = 0; i < metrics.Length; i++)
                        if (measured[i] > baseline[i] * 1.25)
                            failures.Add($"{candidate.Name} {metrics[i]} {measured[i]:F4} ms/tick exceeds {baseline[i]*1.25:F4}");
                }
                if (failures.Count > 0)
                {
                    string report = string.Join(Environment.NewLine, failures);
                    if (freeze) throw new InvalidOperationException(report);
                    Debug.LogWarning("Meridian walk exceeds freeze benchmark: " + report);
                }
            }
            finally
            {
                Physics.gravity = gravity;
                Physics.simulationMode = mode;
            }
        }
    }
}
