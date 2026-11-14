using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // NOCTIEL's adds drawn as Core NativeFallenAdds poses them each frame (the owner, 2026-11-08: "Begin animating his minions as well"), in place of
    // the gear bank's rigid pieces: each add's body rebuilt into its own mesh on the stage's frame, in the body shader, cut at the water so what is
    // under it (rising, sinking) neither shows nor reflects; the Cantors' songs and the Thrones' shots drawn from the mouth and the eye that make
    // them; the water ringing where a hand comes down or a shot lands, a crown of it where one rises. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeFallenAddMotion fbAdds=new NativeFallenAddMotion();private readonly NativeFallenKit fbAddKit=new NativeFallenKit();
        private readonly List<Mesh> fbAddMeshes=new List<Mesh>();private readonly List<bool> fbAddGlowOnly=new List<bool>();private readonly List<float> fbAddGlow=new List<float>();private int fbAddUsed;
        /// <summary>The body shader's cut keeping what is above the water (y 0 on the stage's frame).</summary>
        private static readonly Vector4 FbAboveWater=new Vector4(0,-1,0,0);
        private void FallenAddsStep(float now,float dt,Vector3 origin,Vector3 eye)
        {
            var seen=fbFx.AddsSeen;fbAdds.Step(seen,seen.Count,now,dt);fbAddUsed=0;
            foreach(var a in fbAdds.Adds)
            {
                var at=origin+NativeBossPoseApply.ToUnity(a.At);int tier=LowGraphics||(eye-at).sqrMagnitude>625?1:0;
                fbAddKit.Clear();NativeFallenAddMotion.Pose(a,now,tier,fbAddKit);fbAddKit.Finish();if(fbAddKit.Count==0)continue;
                if(fbAddUsed>=fbAddMeshes.Count){var m=new Mesh{name="NOCTIEL add",hideFlags=HideFlags.HideAndDontSave};m.MarkDynamic();fbAddMeshes.Add(m);fbAddGlowOnly.Add(false);fbAddGlow.Add(1);}
                FbKitMesh(fbAddKit,Matrix4x4.identity,"NOCTIEL add",out bool glowOnly,fbAddMeshes[fbAddUsed]);fbAddGlowOnly[fbAddUsed]=glowOnly;
                fbAddGlow[fbAddUsed]=a.Kind==NativeFallenAdds.Throne?2.2f:a.Kind==NativeFallenAdds.Cantor?1.8f:1;fbAddUsed++;
            }
            // Their songs and shots, a light where each leaves them and, a shot's, where it lands.
            fbAdds.BeamsOf(now);
            foreach(var b in fbAdds.Beams){fbFx.AddBeam(b.from,b.to,b.width,b.alpha,2.4f);fbFx.Glow(b.from,.18f+b.width,b.alpha*.8f,2.4f);if(b.width>.12f)fbFx.Glow(b.to,.5f+b.width*2,b.alpha,2.2f);}
            foreach(var r in fbAdds.Rings)FbStepAdd(origin+NativeBossPoseApply.ToUnity(r.at),origin,now,r.strength);
            foreach(var s in fbAdds.Splashes)fbFx.Splash(s.at,s.age,s.seed);
        }
        private void FallenAddsDraw(Vector3 origin,Matrix4x4 mirror,float inner,float flash)
        {
            var world=Matrix4x4.Translate(origin);var under=mirror*world;
            for(int i=0;i<fbAddUsed;i++){var m=fbAddMeshes[i];float g=inner*fbAddGlow[i];FbDrawKit(m,fbAddGlowOnly[i],world,g,flash,0,FbAboveWater);FbDrawKit(m,fbAddGlowOnly[i],under,g,flash,1,FbAboveWater);}
        }
        private void FallenAddsDispose(){foreach(var m in fbAddMeshes)if(m!=null)CapturedAnimation.Release(m);fbAddMeshes.Clear();fbAddGlowOnly.Clear();fbAddGlow.Clear();fbAddUsed=0;fbAdds.Clear();}
    }
}
