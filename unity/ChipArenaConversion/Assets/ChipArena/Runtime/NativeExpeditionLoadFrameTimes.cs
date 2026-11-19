#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The 4-pilot, 88-zombie frame-time row of the expedition overhaul's budget table (docs/EXPEDITION-DEPLOYMENT.md; W0), taken
    /// alongside Astra's DEV load harness as pushed (A0, Runtime NativeExpeditionLoadPreview: an isolated session launched with
    /// `-expedition-load N`, High by default, `-load-balanced` or `-load-low`). It reads the same flags and nothing else: launched
    /// without `-expedition-load` it does nothing. It waits out the harness's load and warm-up (<see cref="Settle"/> s of real time
    /// after its first frame), records the next <see cref="Frames"/> rendered frames' unscaled times (and, in the Editor, its
    /// render stats), and logs one EXPEDITION_FRAME_BUDGET line (Core NativeFrameBudget, as BoZo's xperf windows report the
    /// 1-pilot rows). Editor and Development builds only. Presentation measurement only: nothing here is a rule.
    /// </summary>
    public sealed class NativeExpeditionLoadFrameTimes : MonoBehaviour
    {
        /// <summary>Astra's harness flag (A0's NativeExpeditionLoadPreview), and its tier flags.</summary>
        public const string Flag="-expedition-load",Balanced="-load-balanced",Low="-load-low";
        public const float Settle=12f;public const int Frames=600;
        private readonly float[] ms=new float[Frames];private int n;private float start=-1;private string label;
        private long batches,tris;private int stats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,Flag);if(at<0)return;
            string pilots=at+1<args.Length?args[at+1]:"?";string tier=Array.IndexOf(args,Low)>=0?"Low":Array.IndexOf(args,Balanced)>=0?"Balanced":"High";
            var go=new GameObject("Expedition load frame times");DontDestroyOnLoad(go);
            go.AddComponent<NativeExpeditionLoadFrameTimes>().label="EXPEDITION_FRAME_BUDGET pilots="+pilots+" zombies=88 graphics="+tier;
        }
        private void Update()
        {
            if(n>=Frames)return;float now=Time.realtimeSinceStartup;if(start<0){start=now;return;}if(now-start<Settle)return;
            ms[n++]=Time.unscaledDeltaTime*1000f;
#if UNITY_EDITOR
            batches+=UnityEditor.UnityStats.batches;tris+=UnityEditor.UnityStats.triangles;stats++;
#endif
            if(n<Frames)return;
            string extra=stats>0?"; batches mean "+(batches/stats)+", triangles mean "+(tris/stats):"";
            Debug.Log(NativeFrameBudget.Line(label,NativeFrameBudget.Of(ms,n))+extra+"; screen "+Screen.width+"x"+Screen.height+"; gpu "+SystemInfo.graphicsDeviceName+"; cpu "+SystemInfo.processorType+" ("+SystemInfo.processorCount+" threads); memory "+SystemInfo.systemMemorySize+" MB");
        }
    }
}
#endif
