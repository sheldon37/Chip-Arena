using System;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The room reacting to THE DEEP END (docs/EXPEDITION-BOSS-BACKROOMS.md section 7; Core NativeDeepEndBossStage is the hook): a real rising water sheet over the whole room (a displaced grid, Resources/NativeDeepEndWater), the wave field stepped and uploaded (Core DeepEndWaves),
    // caustics over every surface (the level's own meshes drawn again, Resources/NativeDeepEndCaustics), the lamps' light in the water, the room's own light by phase, the screen's two moments. None of this runs while the boss's stage is off, so the live Poolrooms looks as it does today.
    public sealed partial class ArenaPresentation
    {
        private DeepEndWaves deepEndWaves;private Texture2D deepEndFieldTex,deepEndGlowTex;private Mesh deepEndSheet,deepEndScreenTri;private Material deepEndWaterMat,deepEndCausticsMat,deepEndUnderMat,deepEndOverMat,deepEndScreenMat;private bool deepEndWaterFailed,deepEndOwnsDepth;private Camera deepEndDepthCam;
        private readonly float[] deepEndGlow=new float[GlowSize*GlowSize*4],deepEndGlowStatic=new float[GlowSize*GlowSize*3*3];private bool deepEndGlowBaked;private const int GlowSize=48;
        private static readonly int DeepFieldId=Shader.PropertyToID("_DeepField"),DeepRectId=Shader.PropertyToID("_DeepRect"),DeepRoomId=Shader.PropertyToID("_DeepRoom"),DeepRoomYId=Shader.PropertyToID("_DeepRoomY"),DeepLightId=Shader.PropertyToID("_DeepLight"),DeepTideId=Shader.PropertyToID("_DeepTide"),DeepClockId=Shader.PropertyToID("_DeepClock"),DeepGlowTexId=Shader.PropertyToID("_DeepGlowTex"),DeepRipplesId=Shader.PropertyToID("_DeepRipples"),DeepWhirlId=Shader.PropertyToID("_DeepWhirl"),DeepCausticsId=Shader.PropertyToID("_DeepCaustics"),DeepDepthOnId=Shader.PropertyToID("_DeepDepthOn"),DeepSoftId=Shader.PropertyToID("_DeepSoftAmount"),DeepWashId=Shader.PropertyToID("_DeepWashAmount"),DeepScreenClockId=Shader.PropertyToID("_DeepScreenClock"),DeepMurkId=Shader.PropertyToID("_DeepMurk");
        private readonly float[] deepEndLightPos=new float[NativeMapLooks.MaxLights*4],deepEndLightRgb=new float[NativeMapLooks.MaxLights*3];
        private NativeDeepEndFlowDraw deepEndUnderDraw,deepEndOverDraw;private NativeVfxDraw deepEndVfx;private bool deepEndVfxFailed;

        private bool DeepEndWaterReady()
        {
            if(deepEndWaterMat!=null)return true;if(deepEndWaterFailed)return false;
            var water=Resources.Load<Shader>("NativeDeepEndWater");var ca=Resources.Load<Shader>("NativeDeepEndCaustics");var flow=Resources.Load<Shader>("NativeDeepEndFlow");var screen=Resources.Load<Shader>("NativeDeepEndScreen");
            if(water==null||ca==null||flow==null||!water.isSupported||!flow.isSupported){deepEndWaterFailed=true;Debug.LogWarning("THE DEEP END: its water shaders are missing or unsupported");return false;}
            deepEndWaterMat=new Material(water){name="THE DEEP END water",renderQueue=3000,hideFlags=HideFlags.HideAndDontSave};var ripples=NativePackBanks.Texture(PoolroomsWaterNormal);if(ripples!=null){ripples.wrapMode=TextureWrapMode.Repeat;deepEndWaterMat.SetTexture("_DeepNormal",ripples);}
            deepEndCausticsMat=ca!=null&&ca.isSupported?new Material(ca){name="THE DEEP END caustics",hideFlags=HideFlags.HideAndDontSave}:null;
            deepEndUnderMat=new Material(flow){name="THE DEEP END under",renderQueue=2990,hideFlags=HideFlags.HideAndDontSave};deepEndOverMat=new Material(flow){name="THE DEEP END over",renderQueue=3020,hideFlags=HideFlags.HideAndDontSave};
            deepEndScreenMat=screen!=null&&screen.isSupported?new Material(screen){name="THE DEEP END screen",hideFlags=HideFlags.HideAndDontSave}:null;
            deepEndWaves=new DeepEndWaves();deepEndFieldTex=new Texture2D(DeepEndWaves.Size,DeepEndWaves.Size,TextureFormat.RGFloat,false,true){name="THE DEEP END field",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            deepEndGlowTex=new Texture2D(GlowSize,GlowSize,TextureFormat.RGBAFloat,false,true){name="THE DEEP END glow",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            deepEndUnderDraw=new NativeDeepEndFlowDraw(deepEndUnderMat);deepEndOverDraw=new NativeDeepEndFlowDraw(deepEndOverMat);
            deepEndScreenTri=new Mesh{name="THE DEEP END screen",hideFlags=HideFlags.HideAndDontSave};deepEndScreenTri.vertices=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0)};deepEndScreenTri.uv=new[]{new Vector2(0,0),new Vector2(2,0),new Vector2(0,2)};deepEndScreenTri.triangles=new[]{0,1,2};deepEndScreenTri.bounds=new Bounds(Vector3.zero,Vector3.one*1e5f);
            return true;
        }
        // The sheet: a static grid over the room, its height the field's.
        private Mesh DeepEndSheetMesh(NVector centre)
        {
            const int n=132;float half=NativeDeepEndArena.Half-PoolroomsLayout.Thickness*.5f+.02f;var vs=new Vector3[(n+1)*(n+1)];var ts=new int[n*n*6];int t=0;
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)vs[x+(n+1)*z]=new Vector3(centre.X-half+2*half*x/n,centre.Y,centre.Z-half+2*half*z/n);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){int a=x+(n+1)*z,b=a+1,c=a+n+1,d=c+1;ts[t++]=a;ts[t++]=c;ts[t++]=d;ts[t++]=a;ts[t++]=d;ts[t++]=b;}
            var m=new Mesh{name="THE DEEP END sheet",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};m.vertices=vs;m.triangles=ts;m.bounds=new Bounds(new Vector3(centre.X,centre.Y,centre.Z),new Vector3(half*2+2,40,half*2+2));m.UploadMeshData(true);return m;
        }
        private NVector deepEndSheetAt=new NVector(float.NaN,0,0);
        private void DeepEndWaterDraw(Camera eye,in DeepEndBossStage s,float now)
        {
            var o=deepEndOrigin;
            // The scene's depth: what the water reads to know how much water it is looking through.
            if((eye.depthTextureMode&DepthTextureMode.Depth)==0&&!LowGraphics){eye.depthTextureMode|=DepthTextureMode.Depth;deepEndOwnsDepth=true;deepEndDepthCam=eye;}
            bool depth=(eye.depthTextureMode&DepthTextureMode.Depth)!=0;
            // The field and the lamps' light in the water.
            deepEndFieldTex.SetPixelData(deepEndWaves.Rg,0);deepEndFieldTex.Apply(false,false);DeepEndGlowField(s);
            float side=DeepEndWaves.Extent;var rect=new Vector4(o.X-side*.5f,o.Z-side*.5f,side,DeepEndWaves.Texel);
            Shader.SetGlobalTexture(DeepFieldId,deepEndFieldTex);Shader.SetGlobalVector(DeepRectId,rect);Shader.SetGlobalVector(DeepRoomId,new Vector4(o.X,o.Z,NativeDeepEndArena.Half-PoolroomsLayout.Thickness*.5f,NativeDeepEndArena.Ceiling));Shader.SetGlobalFloat(DeepRoomYId,o.Y);
            Shader.SetGlobalVector(DeepLightId,new Vector4(s.Ceiling,s.Ambient,s.Warm,s.CeilingOut));Shader.SetGlobalFloat(DeepTideId,o.Y+s.Tide);Shader.SetGlobalFloat(DeepClockId,now);Shader.SetGlobalTexture(DeepGlowTexId,deepEndGlowTex);
            var mat=deepEndWaterMat;mat.SetFloat(DeepMurkId,s.Murk);mat.SetFloat(DeepRipplesId,s.Ripples);mat.SetVector(DeepWhirlId,new Vector4(o.X,o.Z,s.Whirl,s.WhirlTurn));mat.SetFloat(DeepDepthOnId,depth?1:0);
            if(!(deepEndSheetAt==o)){if(deepEndSheet!=null)CapturedAnimation.Release(deepEndSheet);deepEndSheet=DeepEndSheetMesh(o);deepEndSheetAt=o;}
            Graphics.DrawMesh(deepEndSheet,Matrix4x4.identity,mat,0,null,0,null,ShadowCastingMode.Off,false);
            // The caustics: the level's own meshes again.
            if(deepEndCausticsMat!=null&&s.Caustics>.005f)
            {
                deepEndCausticsMat.SetFloat(DeepCausticsId,s.Caustics);var e=eye.transform.position;float r2=PoolroomsReach*PoolroomsReach;
                foreach(var b in poolroomsBlocks){var dc=b.Centre-e;dc.y=0;if(dc.sqrMagnitude>r2)continue;for(int k=0;k<b.Meshes.Length;k++){if(b.Meshes[k]==null||k==(int)PoolroomsMesh.Surface.Water||k==(int)PoolroomsMesh.Surface.Light)continue;Graphics.DrawMesh(b.Meshes[k],Matrix4x4.identity,deepEndCausticsMat,0,null,0,null,ShadowCastingMode.Off,false);}}
            }
            // The screen's two moments.
            if(deepEndScreenMat!=null&&(s.Soft>.005f||s.Wash>.005f)){deepEndScreenMat.SetFloat(DeepSoftId,s.Soft);deepEndScreenMat.SetFloat(DeepWashId,s.Wash);deepEndScreenMat.SetFloat(DeepScreenClockId,now);Graphics.DrawMesh(deepEndScreenTri,Matrix4x4.identity,deepEndScreenMat,0,eye,0,null,ShadowCastingMode.Off,false);}
        }
        // The light in the water, low resolution, over the room's square: the lamps under the water by group (baked once), the boss's flank lamps and its open mouth each frame.
        private void DeepEndGlowField(in DeepEndBossStage s)
        {
            float side=DeepEndWaves.Extent,tx=side/GlowSize;
            if(!deepEndGlowBaked)
            {
                deepEndGlowBaked=true;Array.Clear(deepEndGlowStatic,0,deepEndGlowStatic.Length);var lamps=NativeDeepEndArena.Lamps;
                for(int g=0;g<3;g++)for(int z=0;z<GlowSize;z++)for(int x=0;x<GlowSize;x++)
                {
                    float wx=(x+.5f)*tx-side*.5f,wz=(z+.5f)*tx-side*.5f,sum=0;
                    foreach(var l in lamps){if(l.Kind!=g)continue;float dx=wx-l.At.X,dz=wz-l.At.Z,dy=l.Kind==NativeDeepEndArena.AbyssWall?Math.Max(0,-l.At.Y)*.65f:.4f;sum+=1/(1+.45f*(dx*dx+dz*dz+dy*dy));}
                    deepEndGlowStatic[(x+GlowSize*z)*3+g*GlowSize*GlowSize*3]=sum;
                }
            }
            float over=Mathf.Clamp01(1-s.WaterOut*1.15f)*s.Water;float pool=.34f*over,abyss=.26f*over,room=.3f*over*Mathf.Clamp01((s.Tide-.1f)/.4f);int n=GlowSize*GlowSize;
            for(int i=0;i<n;i++){float k=deepEndGlowStatic[i*3]*pool+deepEndGlowStatic[i*3+n*3]*abyss+deepEndGlowStatic[i*3+n*6]*room;deepEndGlow[i*4]=k*.28f;deepEndGlow[i*4+1]=k*.84f;deepEndGlow[i*4+2]=k*.86f;deepEndGlow[i*4+3]=1;}
            // The boss's flank lamps, near the surface; and the open mouth.
            var o=deepEndOrigin;var surface=s.Tide;
            for(int l=0;l<NativeDeepEndBossLook.Lamps+1;l++)
            {
                NVector at;float strength;Vector3 tint;
                if(l<NativeDeepEndBossLook.Lamps){float g=deepEndFx.LampGlow[l];if(g<.02f)continue;deepEndRig.LampFrame(l,out at,out _,out _);at-=o;strength=g*.42f;tint=new Vector3(.34f,.9f,.9f);}else{at=deepEndRig.MouthAt-o;strength=deepEndRig.Iris*.9f;tint=new Vector3(.3f,.66f,.96f);if(strength<.02f)continue;}
                float dy=Math.Abs(at.Y-surface);if(dy>11)continue;float wx=at.X/tx+GlowSize*.5f-.5f,wz=at.Z/tx+GlowSize*.5f-.5f;int rad=4;int x0=Mathf.Max(0,Mathf.FloorToInt(wx)-rad),x1=Mathf.Min(GlowSize-1,Mathf.CeilToInt(wx)+rad),z0=Mathf.Max(0,Mathf.FloorToInt(wz)-rad),z1=Mathf.Min(GlowSize-1,Mathf.CeilToInt(wz)+rad);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){float dx=(x-wx)*tx,dz=(z-wz)*tx;float a=strength/(1+.42f*(dx*dx+dz*dz+dy*dy*.6f));int i=(x+GlowSize*z)*4;deepEndGlow[i]+=a*tint.x;deepEndGlow[i+1]+=a*tint.y;deepEndGlow[i+2]+=a*tint.z;}
            }
            deepEndGlowTex.SetPixelData(deepEndGlow,0);deepEndGlowTex.Apply(false,false);
        }
        // The room's soft light by the stage, and the practical lights: the nearest of the ceiling's by their level, the nearest under the water, the boss's own.
        private void DeepEndLightsDraw(Camera eye,in DeepEndBossStage s)
        {
            var warm=Color.Lerp(new Color(.5f,.54f,.54f),new Color(.66f,.62f,.5f),s.Warm);var a=warm*s.Ambient;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=a;RenderSettings.ambientEquatorColor=a*.95f;RenderSettings.ambientGroundColor=Color.Lerp(a*1.2f,new Color(.2f,.45f,.45f)*(.3f+.6f*s.Ambient),.55f);
            if(RenderSettings.sun!=null){RenderSettings.sun.intensity=0;RenderSettings.sun.shadows=LightShadows.None;}
            var fogLit=Color.Lerp(PoolroomsHaze,new Color(.74f,.7f,.6f),s.Warm);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=fogLit*Mathf.Lerp(.03f,1,s.Ambient);RenderSettings.fogStartDistance=16;RenderSettings.fogEndDistance=140;NativePackStage.Glow(1);if(NativeLighting.StyleActive)NativeLighting.StyleOff();
            // The nearest four of the ceiling's lights.
            var e=eye.transform.position;float exz=e.x-deepEndOrigin.X,ezz=e.z-deepEndOrigin.Z;int slots=NativeMapLooks.MaxLights;for(int k=0;k<slots;k++){deepEndLightPos[k*4]=0;deepEndLightPos[k*4+1]=-1e4f;deepEndLightPos[k*4+2]=0;deepEndLightPos[k*4+3]=.0001f;deepEndLightRgb[k*3]=deepEndLightRgb[k*3+1]=deepEndLightRgb[k*3+2]=0;}
            int used=0;float c=PoolroomsLayout.Cell;int cells=NativeDeepEndArena.Cells;
            for(int want=0;want<3;want++)
            {
                float best=float.MaxValue;float bx=0,bz=0;bool found=false;
                for(int z=1;z<cells;z+=3)for(int x=1;x<cells;x+=3)
                {
                    float wx=(x-cells*.5f+.5f)*c,wz=(z-cells*.5f+.5f)*c,d=(wx-exz)*(wx-exz)+(wz-ezz)*(wz-ezz);if(d>=best)continue;bool taken=false;for(int q=0;q<used;q++)if(Mathf.Abs(deepEndLightPos[q*4]-(deepEndOrigin.X+wx))<.1f&&Mathf.Abs(deepEndLightPos[q*4+2]-(deepEndOrigin.Z+wz))<.1f)taken=true;if(taken)continue;best=d;bx=wx;bz=wz;found=true;
                }
                if(!found)break;float order=Mathf.Sqrt(bx*bx+bz*bz)/28f;float lv=s.Ceiling*(1-BossMath.Smooth((s.CeilingOut*1.25f-order)/.14f));var tint=Color.Lerp(new Color(.62f,.62f,.58f),new Color(.66f,.54f,.36f),s.Warm)*lv;
                deepEndLightPos[used*4]=deepEndOrigin.X+bx;deepEndLightPos[used*4+1]=deepEndOrigin.Y+NativeDeepEndArena.Ceiling-.3f;deepEndLightPos[used*4+2]=deepEndOrigin.Z+bz;deepEndLightPos[used*4+3]=14;deepEndLightRgb[used*3]=tint.r;deepEndLightRgb[used*3+1]=tint.g;deepEndLightRgb[used*3+2]=tint.b;used++;
            }
            // The body's own.
            for(int k=0;k<deepEndFx.LightCount&&used<slots-1;k++){for(int q=0;q<4;q++)deepEndLightPos[used*4+q]=deepEndFx.LightPos[k*4+q];for(int q=0;q<3;q++)deepEndLightRgb[used*3+q]=deepEndFx.LightRgb[k*3+q];used++;}
            // The nearest of those under the water, for what stands in it.
            if(used<slots)
            {
                float best=float.MaxValue;int at=-1;var lamps=NativeDeepEndArena.Lamps;for(int k=0;k<lamps.Length;k++){if(lamps[k].Kind==NativeDeepEndArena.AbyssWall)continue;float d=(lamps[k].At.X-exz)*(lamps[k].At.X-exz)+(lamps[k].At.Z-ezz)*(lamps[k].At.Z-ezz);if(d<best){best=d;at=k;}}
                if(at>=0){var l=lamps[at];float lv=s.Water*(1-BossMath.Smooth((s.WaterOut*1.12f-l.Order)/.05f));deepEndLightPos[used*4]=deepEndOrigin.X+l.At.X;deepEndLightPos[used*4+1]=deepEndOrigin.Y+l.At.Y;deepEndLightPos[used*4+2]=deepEndOrigin.Z+l.At.Z;deepEndLightPos[used*4+3]=9;deepEndLightRgb[used*3]=.14f*lv;deepEndLightRgb[used*3+1]=.42f*lv;deepEndLightRgb[used*3+2]=.42f*lv;used++;}
            }
            NativeLighting.PickedLights(deepEndLightPos,deepEndLightRgb);
        }
        private void DeepEndFxDraw(Camera eye,in DeepEndBossStage s,float now)
        {
            deepEndUnderDraw.Draw(deepEndFx.Under);deepEndOverDraw.Draw(deepEndFx.Over);DeepEndBossDraws+=2;
            if(deepEndVfx==null&&!deepEndVfxFailed){try{deepEndVfx=new NativeVfxDraw();}catch(Exception e){deepEndVfxFailed=true;Debug.LogWarning("THE DEEP END: effects renderer unavailable ("+e.Message+")");}}
            if(deepEndVfx!=null&&deepEndFx.Vfx.Count>0){deepEndVfx.Low=LowGraphics;deepEndVfx.Begin();NativeBossVfxSubmit.Submit(deepEndFx.Vfx,deepEndVfx);deepEndVfx.Submit(eye);DeepEndBossDraws+=2;}
        }
        private void DeepEndWaterHide(){if(deepEndOwnsDepth&&deepEndDepthCam!=null){deepEndDepthCam.depthTextureMode&=~DepthTextureMode.Depth;}deepEndOwnsDepth=false;deepEndDepthCam=null;}
        private void DeepEndWaterDispose()
        {
            DeepEndWaterHide();if(deepEndWaterMat!=null)CapturedAnimation.Release(deepEndWaterMat);deepEndWaterMat=null;if(deepEndCausticsMat!=null)CapturedAnimation.Release(deepEndCausticsMat);deepEndCausticsMat=null;if(deepEndUnderMat!=null)CapturedAnimation.Release(deepEndUnderMat);deepEndUnderMat=null;if(deepEndOverMat!=null)CapturedAnimation.Release(deepEndOverMat);deepEndOverMat=null;if(deepEndScreenMat!=null)CapturedAnimation.Release(deepEndScreenMat);deepEndScreenMat=null;
            if(deepEndFieldTex!=null)CapturedAnimation.Release(deepEndFieldTex);deepEndFieldTex=null;if(deepEndGlowTex!=null)CapturedAnimation.Release(deepEndGlowTex);deepEndGlowTex=null;if(deepEndSheet!=null)CapturedAnimation.Release(deepEndSheet);deepEndSheet=null;deepEndSheetAt=new NVector(float.NaN,0,0);if(deepEndScreenTri!=null)CapturedAnimation.Release(deepEndScreenTri);deepEndScreenTri=null;
            deepEndUnderDraw?.Dispose();deepEndUnderDraw=null;deepEndOverDraw?.Dispose();deepEndOverDraw=null;deepEndVfx?.Dispose();deepEndVfx=null;deepEndVfxFailed=false;deepEndGlowBaked=false;deepEndWaves=null;
        }
    }

    /// <summary>One frame's water geometry (Core DeepEndFlow) as a dynamic mesh with the advanced Mesh API: the vertex array uploaded as Core built it, only the used range. One draw. Allocation free per frame.</summary>
    internal sealed class NativeDeepEndFlowDraw:IDisposable
    {
        private static readonly VertexAttributeDescriptor[] Layout={new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4)};
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);private readonly Mesh mesh;private readonly Material material;private int vertexCapacity=-1,indexCapacity=-1;
        public NativeDeepEndFlowDraw(Material m){material=m;mesh=new Mesh{name="THE DEEP END / water geometry",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();}
        public void Draw(DeepEndFlow f)
        {
            if(f==null||f.VertexCount==0||f.IndexCount==0)return;
            if(vertexCapacity!=f.V.Length){mesh.SetVertexBufferParams(f.V.Length,Layout);vertexCapacity=f.V.Length;}
            if(indexCapacity!=f.T.Length){mesh.SetIndexBufferParams(f.T.Length,IndexFormat.UInt32);indexCapacity=f.T.Length;mesh.subMeshCount=1;}
            mesh.SetVertexBufferData(f.V,0,0,f.VertexCount,0,Quiet);mesh.SetIndexBufferData(f.T,0,0,f.IndexCount,Quiet);mesh.SetSubMesh(0,new SubMeshDescriptor(0,f.IndexCount){bounds=Everywhere,firstVertex=0,vertexCount=f.VertexCount},Quiet);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){CapturedAnimation.Release(mesh);}
    }
}
