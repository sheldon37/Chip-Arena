using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // NOCTIEL's stage, the owner's next pass (2026-11-08): "Lets also make the ripple effect on the stage more 'water-like' like were stepping in water,
    // not just random ripples. Have the background (The void area) react with the music as well."
    //  - The void: a sphere round the eye behind everything (Resources/NativeFallenVoid.shader), black but for what the song puts in it: a low glow on
    //    the horizon with its level, curtains of light falling from above that flare with the bass and climb the sky as it plays, a flash and a band
    //    rising on each slam. The Still Water mirrors it.
    //  - The water: what touches it rings it. The pilots' footfalls as they run (alternate feet), their landings, a dash's wake; NOCTIEL's feet as they
    //    come down and, smaller, as they lift (FallenFeet). The song no longer rings the floor (its bass lines and slams were the random ripples).
    public sealed partial class ArenaPresentation
    {
        private Mesh fbVoidMesh;private Material fbVoidMat;private bool fbVoidFailed;
        private static readonly int FbVoidSlamId=Shader.PropertyToID("_FbVoidSlam"),FbVoidDimId=Shader.PropertyToID("_FbVoidDim");
        /// <summary>The void's sphere: its radius (m, inside the stage camera's far plane).</summary>
        private const float FbVoidRadius=380;
        private void FallenVoidDraw(Vector3 eye,float clip,float dim,float now)
        {
            if(fbVoidMat==null)
            {
                if(fbVoidFailed)return;var sh=Resources.Load<Shader>("NativeFallenVoid");if(sh==null||!sh.isSupported){fbVoidFailed=true;Debug.LogWarning("NOCTIEL: the void's shader is missing");return;}
                fbVoidMat=new Material(sh){name="NOCTIEL void",hideFlags=HideFlags.HideAndDontSave};
            }
            if(fbVoidMesh==null)fbVoidMesh=FallenVoidSphere();
            if(fbSongTex!=null)fbVoidMat.SetTexture(FbSongId,fbSongTex);fbVoidMat.SetVector(FbSongAtId,new Vector4(clip,1f/NativeFallenSong.End,fbSongTex!=null?1:0,0));fbVoidMat.SetFloat(FbClockId,now%1000);
            float slam=fbSong.LastSlam(clip);fbVoidMat.SetFloat(FbVoidSlamId,float.IsNaN(slam)?-1:clip-slam);fbVoidMat.SetFloat(FbVoidDimId,dim);
            Graphics.DrawMesh(fbVoidMesh,Matrix4x4.TRS(eye,Quaternion.identity,Vector3.one*FbVoidRadius),fbVoidMat,0,null,0,null,ShadowCastingMode.Off,false);FallenBossDraws++;
        }
        // A unit sphere, faces in (the shader culls front), its vertices its directions.
        private static Mesh FallenVoidSphere()
        {
            const int lat=24,lon=48;var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=lat;i++){float th=Mathf.PI*i/lat;for(int j=0;j<=lon;j++){float ph=2*Mathf.PI*j/lon;v.Add(new Vector3(Mathf.Sin(th)*Mathf.Cos(ph),Mathf.Cos(th),Mathf.Sin(th)*Mathf.Sin(ph)));}}
            for(int i=0;i<lat;i++)for(int j=0;j<lon;j++){int a=i*(lon+1)+j,b=a+1,c=a+lon+1,d=c+1;t.Add(a);t.Add(b);t.Add(d);t.Add(a);t.Add(d);t.Add(c);}
            var m=new Mesh{name="NOCTIEL void",hideFlags=HideFlags.HideAndDontSave};m.SetVertices(v);m.SetTriangles(t,0);m.bounds=new Bounds(Vector3.zero,Vector3.one*2);return m;
        }
        private void FallenVoidDispose(){if(fbVoidMat!=null)CapturedAnimation.Release(fbVoidMat);fbVoidMat=null;if(fbVoidMesh!=null)CapturedAnimation.Release(fbVoidMesh);fbVoidMesh=null;fbVoidFailed=false;fbPilotSteps.Clear();}

        // ---- the pilots on the water ----
        private struct FbPilotStep{public Vector3 Last,Prev;public bool Air;public int Side;}
        private readonly Dictionary<int,FbPilotStep> fbPilotSteps=new Dictionary<int,FbPilotStep>();private readonly List<int> fbPilotGone=new List<int>();private readonly HashSet<int> fbPilotSeen=new HashSet<int>();
        /// <summary>A pilot's stride on the water (m between footfalls), how high off it counts as in the air, and the speed past which a dash leaves a wake.</summary>
        private const float FbPilotStride=.75f,FbPilotAir=.35f,FbPilotDash=9f;
        private void FallenPilotSteps(NativeArena arena,Vector3 origin,float now,float dt)
        {
            if(arena==null)return;fbPilotSeen.Clear();float floor=origin.y;
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Hp<=0||p.Seat==null)continue;int id=p.Seat.id;fbPilotSeen.Add(id);var at=p.Body.position;
                if(!fbPilotSteps.TryGetValue(id,out var s)){fbPilotSteps[id]=new FbPilotStep{Last=at,Prev=at,Side=1};continue;}
                float up=at.y-floor;var flat=at-s.Last;flat.y=0;float travelled=flat.magnitude;
                if(up>FbPilotAir){s.Air=true;s.Last=at;}
                else if(s.Air&&up<.15f){s.Air=false;FbStepAdd(at,origin,now,.95f);FbStepAdd(at+new Vector3(.3f,0,.2f),origin,now-.05f,.45f);s.Last=at;}
                else
                {
                    bool dash=dt>0&&new Vector2(at.x-s.Prev.x,at.z-s.Prev.z).magnitude>FbPilotDash*dt;
                    float stride=dash?FbPilotStride*.5f:FbPilotStride;
                    if(travelled>=stride&&up<.15f)
                    {
                        var way=flat/travelled;var side=new Vector3(way.z,0,-way.x)*(.12f*s.Side);s.Side=-s.Side;
                        FbStepAdd(at+side,origin,now,dash?.3f:Mathf.Lerp(.32f,.55f,Mathf.Clamp01(travelled/(stride*2))));s.Last=at;
                    }
                }
                s.Prev=at;fbPilotSteps[id]=s;
            }
            fbPilotGone.Clear();foreach(var k in fbPilotSteps.Keys)if(!fbPilotSeen.Contains(k))fbPilotGone.Add(k);foreach(var k in fbPilotGone)fbPilotSteps.Remove(k);
        }
    }
}
