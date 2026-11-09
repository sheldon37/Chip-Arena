using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The parry's hexagon barrier (Core NativeParryBarrier; owner 2026-11-09: "pressing F; Lets add more of an effect rather that just turning
    // character a color. Make it high quality, animated, and visible. Maybe like hexagon barriers?"): while a pilot's replicated parry window is
    // open (Actor.BlockAt .. BlockUntil) and through its burst after, a dome of hex cells stands round it in its accent (Resources
    // NativeParryBarrier.shader): the cells snap in from its front round to its back, breathe and flicker, light up and ripple out from where
    // a parried blow lands (the Block events, NativeBlockFxPresentation.cs) and burst outward as the window shuts. The body's own hologram
    // (NativeParryHoloPresentation.cs) glows softly under it. The local pilot's own dome is fainter (in first person its eye stands inside it).
    // Presentation only: nothing here reaches the authority, the network frame, hit timing, damage or movement.
    public sealed partial class ArenaPresentation
    {
        private Mesh parryBarrierMesh;private int parryBarrierTier=-1;private Material parryBarrierMaterial;private bool parryBarrierFailed;private MaterialPropertyBlock parryBarrierBlock;
        private static readonly int BarrierColorId=Shader.PropertyToID("_BarrierColor"),BarrierAgeId=Shader.PropertyToID("_Age"),BarrierClosedId=Shader.PropertyToID("_Closed"),
            BarrierFacingId=Shader.PropertyToID("_Facing"),BarrierHitId=Shader.PropertyToID("_Hit"),BarrierStrengthId=Shader.PropertyToID("_Strength");
        /// <summary>The local pilot's own dome's strength: first person (its eye is inside it), and third person (the camera looks through it).</summary>
        public const float OwnBarrier=.3f,OwnThirdBarrier=.6f;
        private Mesh ParryBarrierMesh()
        {
            int tier=LowGraphics?1:0;if(parryBarrierMesh!=null&&parryBarrierTier==tier)return parryBarrierMesh;
            var m=NativeParryBarrier.Build(tier>0?NativeParryBarrier.Low:NativeParryBarrier.Full);int count=m.P.Count;
            var p=new List<Vector3>(count);var n=new List<Vector3>(count);var uv=new List<Vector2>(count);var mid=new List<Vector3>(count);
            for(int i=0;i<count;i++){p.Add(new Vector3(m.P[i].X,m.P[i].Y,m.P[i].Z));n.Add(new Vector3(m.N[i].X,m.N[i].Y,m.N[i].Z));uv.Add(new Vector2(m.Uv[i].X,m.Uv[i].Y));mid.Add(new Vector3(m.Middle[i].X,m.Middle[i].Y,m.Middle[i].Z));}
            var mesh=new Mesh{name="Parry barrier",hideFlags=HideFlags.HideAndDontSave};mesh.SetVertices(p);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetUVs(1,mid);mesh.SetTriangles(m.T,0);
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2.6f);if(parryBarrierMesh!=null)Destroy(parryBarrierMesh);parryBarrierMesh=mesh;parryBarrierTier=tier;return mesh;
        }
        private Material ParryBarrierMaterial()
        {
            if(parryBarrierMaterial!=null||parryBarrierFailed)return parryBarrierMaterial;var shader=Resources.Load<Shader>("NativeParryBarrier");
            if(shader==null||!shader.isSupported){parryBarrierFailed=true;return null;}
            return parryBarrierMaterial=new Material(shader){name="Parry barrier",hideFlags=HideFlags.HideAndDontSave};
        }
        /// <summary>RenderBlockFx, each frame: every parrying pilot's barrier (and a shut one's burst).</summary>
        private void RenderParryBarriers(Snapshot s,float time)
        {
            if(s?.Actors==null)return;Material mat=null;Mesh mesh=null;
            foreach(var a in s.Actors)
            {
                if(a==null||!a.Alive||a.BlockUntil<=a.BlockAt)continue;float age=time-a.BlockAt,closed=time-a.BlockUntil;if(age<0||closed>NativeParryBarrier.After)continue;
                mat??=ParryBarrierMaterial();if(mat==null)return;mesh??=ParryBarrierMesh();parryBarrierBlock??=new MaterialPropertyBlock();
                // The newest parried blow of this window, and its way from the dome's middle (its height's stretch undone).
                float hitAge=-1;var hit=Vector3.zero;
                foreach(var r in blockRecords)if(r.Blocker==a.Id&&r.Detail!="hold"&&r.Detail!="guard-break"&&r.At<=time&&r.At>=a.BlockAt-.05f&&(hitAge<0||time-r.At<hitAge)){hitAge=time-r.At;hit=r.Point;}
                var feet=visualPositions.TryGetValue(a.Id,out var drawn)?drawn:Position(a);float scale=Mathf.Max(.3f,a.RigScale>0?a.RigScale:1);
                var middle=feet+Vector3.up*(NativeParryBarrier.Middle*scale);var way=hitAge>=0?hit-middle:Vector3.forward;way.y/=NativeParryBarrier.Tall;way=way.sqrMagnitude>1e-6f?way.normalized:Vector3.forward;
                var facing=Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;bool own=a.Id==LocalId;
                var accent=AccentTint(a.AccentColor);var colour=new Color(accent.r*.45f+.1f,accent.g*.45f+.45f,accent.b*.45f+.6f)*1.2f;
                parryBarrierBlock.Clear();parryBarrierBlock.SetColor(BarrierColorId,colour.linear);parryBarrierBlock.SetFloat(BarrierAgeId,age);parryBarrierBlock.SetFloat(BarrierClosedId,closed>=0?closed:-1);
                parryBarrierBlock.SetVector(BarrierFacingId,facing);parryBarrierBlock.SetVector(BarrierHitId,new Vector4(way.x,way.y,way.z,hitAge));parryBarrierBlock.SetFloat(BarrierStrengthId,own?(FirstPerson?OwnBarrier:OwnThirdBarrier):1);
                float radius=NativeParryBarrier.Radius*scale;
                Graphics.DrawMesh(mesh,Matrix4x4.TRS(middle,Quaternion.identity,new Vector3(radius,radius*NativeParryBarrier.Tall,radius)),mat,0,null,0,parryBarrierBlock,ShadowCastingMode.Off,false);
            }
        }
    }
}
