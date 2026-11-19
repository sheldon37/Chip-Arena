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
        public static void CheckMeridianCandidateBudgets()
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
                    var measured = Measure(candidate);
                    for (int i = 0; i < metrics.Length; i++)
                        if (measured[i] > baseline[i] * 1.25)
                            failures.Add($"{candidate.Name} {metrics[i]} {measured[i]:F4} ms/tick exceeds {baseline[i]*1.25:F4}");
                }
                if (failures.Count > 0)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
            }
            finally
            {
                Physics.gravity = gravity;
                Physics.simulationMode = mode;
            }
        }
    }
}
