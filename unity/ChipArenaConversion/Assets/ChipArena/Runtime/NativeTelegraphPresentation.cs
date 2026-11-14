using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NV=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    // Attack warnings on the ground (Core NativeTelegraph; docs/TELEGRAPHS.md): any presenter adds this frame's warnings with
    // Telegraph(); RenderAbilities clears the list before the world is drawn and draws them all after it, one quad each in one
    // mesh through Resources/NativeTelegraph.shader. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeTelegraphList telegraphs=new NativeTelegraphList(1024);
        private Material telegraphMat;private Mesh telegraphMesh;private bool telegraphFailed;
        private readonly List<Vector3> tgP=new List<Vector3>(4096);private readonly List<Vector4> tgL=new List<Vector4>(4096),tgS=new List<Vector4>(4096),tgT=new List<Vector4>(4096),tgK=new List<Vector4>(4096);private readonly List<int> tgI=new List<int>(6144);
        private static readonly int TgClockId=Shader.PropertyToID("_TgClock");
        /// <summary>The warnings drawn last frame (the DEV readout and the checks).</summary>
        public int TelegraphsDrawn{get;private set;}

        /// <summary>Adds a warning for this frame. offset: the presenter's stage origin (a DEV preview's stage far below the level).</summary>
        internal void Telegraph(in NativeTelegraph.Spec s,Vector3 offset){var w=s;w.At+=new NV(offset.x,offset.y,offset.z);telegraphs.Add(w);}
        internal void Telegraph(in NativeTelegraph.Spec s)=>telegraphs.Add(s);
        private void TelegraphsBegin()=>telegraphs.Clear();
        // The Siren's kit's ground warnings (a pilot's casts or the horde Siren's), on the field clock, from the replicated fields.
        private void SirenWarnings(NativeArena arena)
        {
            var list=arena.SirenFields;if(list==null||list.Count==0)return;float now=FieldClock(arena);
            for(int i=0;i<list.Count;i++){var f=list[i];if(NativeTelegraph.SirenField(f.Kind,new NV(f.Position.x,f.Position.y,f.Position.z),f.Radius,f.At,f.Until,now,out var s))Telegraph(s);}
        }

        private void TelegraphsDraw(float clock)
        {
            TelegraphsDrawn=0;int n=telegraphs.Count;if(n==0||telegraphFailed)return;
            if(telegraphMat==null)
            {
                var shader=Resources.Load<Shader>("NativeTelegraph");if(shader==null||!shader.isSupported){telegraphFailed=true;Debug.LogWarning("Telegraphs: the NativeTelegraph shader is missing or unsupported; warnings not drawn");return;}
                telegraphMat=new Material(shader){name="Telegraphs",hideFlags=HideFlags.HideAndDontSave};telegraphMesh=new Mesh{name="Telegraphs",hideFlags=HideFlags.HideAndDontSave};telegraphMesh.MarkDynamic();
            }
            tgP.Clear();tgL.Clear();tgS.Clear();tgT.Clear();tgK.Clear();tgI.Clear();
            for(int i=0;i<n;i++)
            {
                ref readonly var s=ref telegraphs.Items[i];
                NativeTelegraph.Corners(s,out var a,out var b,out var c,out var d,out var la,out var lb,out var lc,out var ld);
                int o=tgP.Count;float lift=.03f;
                tgP.Add(new Vector3(a.X,a.Y+lift,a.Z));tgP.Add(new Vector3(b.X,b.Y+lift,b.Z));tgP.Add(new Vector3(c.X,c.Y+lift,c.Z));tgP.Add(new Vector3(d.X,d.Y+lift,d.Z));
                float sh=s.Shape,st=s.Style;
                tgL.Add(new Vector4(la.X,la.Y,sh,st));tgL.Add(new Vector4(lb.X,lb.Y,sh,st));tgL.Add(new Vector4(lc.X,lc.Y,sh,st));tgL.Add(new Vector4(ld.X,ld.Y,sh,st));
                var size=new Vector4(s.A,s.B,s.C,s.Fill);var time=new Vector4(s.Fill01,s.Lock,s.After,s.Live);var k=new Vector4(Mathf.Clamp01(s.Strength),(s.Seed%997)*.0137f,0,0);
                for(int j=0;j<4;j++){tgS.Add(size);tgT.Add(time);tgK.Add(k);}
                tgI.Add(o);tgI.Add(o+1);tgI.Add(o+2);tgI.Add(o);tgI.Add(o+2);tgI.Add(o+3);
            }
            var mesh=telegraphMesh;mesh.Clear(true);mesh.SetVertices(tgP);mesh.SetUVs(0,tgL);mesh.SetUVs(1,tgS);mesh.SetUVs(2,tgT);mesh.SetUVs(3,tgK);mesh.SetTriangles(tgI,0,false);
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*100000);telegraphMat.SetFloat(TgClockId,clock%10000);
            Graphics.DrawMesh(mesh,Matrix4x4.identity,telegraphMat,0,null,0,null,ShadowCastingMode.Off,false);TelegraphsDrawn=n;
        }
    }
}
