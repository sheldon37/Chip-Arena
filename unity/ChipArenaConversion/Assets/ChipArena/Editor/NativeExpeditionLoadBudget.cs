using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        [MenuItem("Chip Arena/Benchmark Expedition hub (4 pilots, 88 zombies)")]
        public static void CheckExpeditionLoadBudget()
        {
            var previousMode=Physics.simulationMode;
            var previousGravity=Physics.gravity;
            try
            {
                Physics.simulationMode=SimulationMode.Script;Physics.gravity=new Vector3(0,-23,0);
                using(var fixture=new NativeExpeditionLoadFixture(4,_=>EditorSceneManager.NewPreviewScene(),s=>EditorSceneManager.ClosePreviewScene(s)))
                {
                    for(int i=0;i<NativeExpeditionLoadFixture.Warmup+NativeExpeditionLoadFixture.Samples;i++)fixture.Step();
                    Debug.Log(fixture.Report());
                }
            }
            finally{Physics.gravity=previousGravity;Physics.simulationMode=previousMode;}
        }
    }
}
