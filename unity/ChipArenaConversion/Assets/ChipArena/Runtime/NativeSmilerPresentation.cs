using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The Smiler drawn as what it is, an apparition of black smoke with a face (owner 2026-11-03: "I think the smiler shouldn't
    // be a player model. Keep in mind enemies dont have to always use the player model. The smiler is usually an black ghostly
    // apparition like this"; Core NativeSmilerLook for what it shows when, Resources/NativeSmiler.shader for how). Its rig is
    // still posed on the gait (hits, arrows and the effects find its body there), but neither the player's model nor the voxel
    // model is drawn: one raymarched box round the cloud (the authority keeps one Smiler at a time). Hits tear black smoke off
    // it instead of blood (no wounds, no severed limbs); its death thins and rises away instead of falling (no corpse, no gore);
    // no footfalls and no zombie voice (its lunge's strike is the rush, its blink silent: the horde cues). Presentation only.
    public sealed partial class ArenaPresentation
    {
        private static bool SmilerIs(int kind)=>kind>=0&&kind==NativeSmilerLook.Kind;
        private Material smilerOutside,smilerInside;private Mesh smilerBox;private Texture3D smilerNoise;private MaterialPropertyBlock smilerBlock;private bool smilerFailed;
        // The billows' noise: built once per process off the main thread (0 idle, 1 building, 2 ready, 3 failed), uploaded when ready.
        private static byte[] smilerNoiseBytes;private static int smilerNoiseBuilt;
        private sealed class SmilerView{public float HitAt=float.NaN,Gather,Surge,Open,Squint;public Vector3 Drift;public int Frame;}
        private readonly Dictionary<int,SmilerView> smilerViews=new Dictionary<int,SmilerView>();private readonly List<int> smilerStale=new List<int>();
        private readonly HashSet<int> smilerBursts=new HashSet<int>();
        // Smoke torn off by the hits and blown off at its death (drawn in the effects pass as soft black puffs).
        private struct SmilerPuff{public Vector3 At,Velocity;public float Born,Size,Life;public uint Seed;}
        private readonly SmilerPuff[] smilerPuffs=new SmilerPuff[64];private int smilerPuffNext;
        private static readonly Color SmilerSmoke=new Color(.012f,.011f,.013f,0);
        private static readonly int SmilerToCloudId=Shader.PropertyToID("_SmilerToCloud"),SmilerToWorldId=Shader.PropertyToID("_SmilerToWorld"),SmilerBoxMinId=Shader.PropertyToID("_SmilerBoxMin"),SmilerBoxMaxId=Shader.PropertyToID("_SmilerBoxMax");
        private static readonly int SmilerAId=Shader.PropertyToID("_SmilerA"),SmilerBId=Shader.PropertyToID("_SmilerB"),SmilerCId=Shader.PropertyToID("_SmilerC"),SmilerDId=Shader.PropertyToID("_SmilerD"),SmilerEId=Shader.PropertyToID("_SmilerE");
        private static readonly int SmilerNoiseId=Shader.PropertyToID("_SmilerNoise"),SmilerNoiseReadyId=Shader.PropertyToID("_SmilerNoiseReady"),SmilerSoftId=Shader.PropertyToID("_SmilerSoft");

        /// <summary>HordeKindsBegin: starts the billows' noise (once per process, off the main thread).</summary>
        private static void SmilerNoiseStart()
        {
            if(Interlocked.CompareExchange(ref smilerNoiseBuilt,1,0)!=0)return;
            WaitCallback build=_=>{try{Volatile.Write(ref smilerNoiseBytes,NativeSmilerLook.Noise());Volatile.Write(ref smilerNoiseBuilt,2);}catch(Exception){Volatile.Write(ref smilerNoiseBuilt,3);}};
            try{if(!ThreadPool.QueueUserWorkItem(build))build(null);}catch(NotSupportedException){build(null);}
        }
        private bool SmilerReady()
        {
            if(smilerOutside!=null)return true;if(smilerFailed)return false;
            var shader=Resources.Load<Shader>("NativeSmiler");if(shader==null||!shader.isSupported){smilerFailed=true;Debug.LogWarning("The Smiler's shader is missing or unsupported: it is not drawn");return false;}
            // From outside the box its front faces, depth tested; from inside it (a pilot it lunges at) its back faces, always.
            smilerOutside=new Material(shader){name="Native Smiler (outside)",renderQueue=(int)RenderQueue.Transparent,hideFlags=HideFlags.HideAndDontSave};
            smilerOutside.SetFloat("_Cull",(float)CullMode.Back);smilerOutside.SetFloat("_ZTest",(float)CompareFunction.LessEqual);
            smilerInside=new Material(shader){name="Native Smiler (inside)",renderQueue=(int)RenderQueue.Transparent,hideFlags=HideFlags.HideAndDontSave};
            smilerInside.SetFloat("_Cull",(float)CullMode.Front);smilerInside.SetFloat("_ZTest",(float)CompareFunction.Always);
            smilerBox=SmilerBoxMesh();smilerBlock=new MaterialPropertyBlock();SmilerNoiseStart();return true;
        }
        private void SmilerNoiseUpload()
        {
            if(smilerNoise!=null||Volatile.Read(ref smilerNoiseBuilt)!=2)return;int n=NativeSmilerLook.NoiseSize;
            var tex=new Texture3D(n,n,n,GraphicsFormat.R8G8B8A8_UNorm,TextureCreationFlags.None){name="Native Smiler billows",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            tex.SetPixelData(smilerNoiseBytes,0);tex.Apply(false,true);smilerNoise=tex;smilerNoiseBytes=null;
            foreach(var mat in new[]{smilerOutside,smilerInside}){mat.SetTexture(SmilerNoiseId,tex);mat.SetFloat(SmilerNoiseReadyId,1);}
        }
        // A box from -1 to 1, its faces outward (clockwise seen from outside).
        private static Mesh SmilerBoxMesh()
        {
            var mesh=new Mesh{name="Native Smiler box",hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
            mesh.triangles=new[]{0,3,2,0,2,1, 5,6,7,5,7,4, 4,7,3,4,3,0, 1,2,6,1,6,5, 3,7,6,3,6,2, 1,5,4,1,4,0};
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2);return mesh;
        }
        private SmilerView SmilerViewOf(int id){if(!smilerViews.TryGetValue(id,out var v)){v=new SmilerView();smilerViews[id]=v;}return v;}
        // A release eases back (a cancelled lunge does not snap); a rise is immediate.
        private static float SmilerEase(ref float was,float to,float dt){was=to>=was?to:Mathf.Lerp(was,to,1-Mathf.Exp(-dt*14));return was;}

        /// <summary>HordeKindAdd: a living Smiler (its rig posed, not drawn) at the presented root, facing `yaw`, over `ground`.</summary>
        private void SmilerAdd(NativeMinion m,Vector3 root,float yaw,Vector3 velocity,float ground,float now)
        {
            if(!SmilerReady())return;SmilerNoiseUpload();var a=m.Pve;var v=SmilerViewOf(m.Id);v.Frame=Time.frameCount;uint seed=NativeHordeKinds.Seed(m.Id);
            // Its drift (the tail and lobes trail it), smoothed, in its own frame.
            v.Drift=Vector3.Lerp(v.Drift,Quaternion.Euler(0,-yaw,0)*new Vector3(velocity.x,0,velocity.z),1-Mathf.Exp(-hordeDt*4));
            var p=NativeSmilerLook.Living(seed,now,a.Zone?.attack,now-m.LastAttack,now-a.SpawnAt,now-v.HitAt);
            p.Gather=SmilerEase(ref v.Gather,p.Gather,hordeDt);p.Surge=SmilerEase(ref v.Surge,p.Surge,hordeDt);p.Open=SmilerEase(ref v.Open,p.Open,hordeDt);p.Squint=SmilerEase(ref v.Squint,p.Squint,hordeDt);
            SmilerDraw(new Vector3(root.x,ground,root.z),yaw,p,v.Drift,seed,now);
        }
        /// <summary>HordeKindDeath: the dying Smiler `age` s after its death (its smoke thinning and rising away, a burst torn off).</summary>
        private void SmilerDeath(NativeDeath death,float age)
        {
            if(!(age>=0)||NativeSmilerLook.Gone(age)||!SmilerReady())return;SmilerNoiseUpload();uint seed=NativeHordeKinds.Seed(death.id);
            var floor=new Vector3(death.position.x,death.position.y-NativeHordeKinds.RootHeight(NativeSmilerLook.Kind,false),death.position.z);
            SmilerDraw(floor,death.yaw,NativeSmilerLook.Dying(seed,hordeNow,age),Vector3.zero,seed,hordeNow);
            if(age<.3f&&smilerBursts.Add(death.id))
            {
                var core=floor+Vector3.up*NativeSmilerLook.Core;
                for(int i=0;i<10;i++){var r=new Vector3(SmilerHash(seed,i,0)-.5f,SmilerHash(seed,i,1)-.35f,SmilerHash(seed,i,2)-.5f).normalized;SmilerPuffAdd(core+r*.45f,r*(1.2f+1.4f*SmilerHash(seed,i,3))+Vector3.up*.4f,.55f+.4f*SmilerHash(seed,i,4),1.6f,seed+(uint)i);}
            }
        }
        private void SmilerDraw(Vector3 floor,float yaw,in NativeSmilerLook.Pose p,Vector3 drift,uint seed,float time)
        {
            var toWorld=Matrix4x4.TRS(floor+Vector3.up*NativeSmilerLook.Core,Quaternion.Euler(0,yaw,0),Vector3.one);var toCloud=toWorld.inverse;
            // The box round the cloud: grown as it spreads, risen as it dies, drawn back and thrown long ahead through the lunge.
            float grow=1+.6f*p.Spread;var bm=NativeSmilerLook.BoxMin;var bx=NativeSmilerLook.BoxMax;
            var min=new Vector3(bm.X,bm.Y,bm.Z)*grow;var max=new Vector3(bx.X,bx.Y,bx.Z)*grow;max.y+=p.Rise;max.z+=p.Surge*NativeSmilerLook.Reach;min.z-=.2f*p.Gather;
            var box=toWorld*Matrix4x4.TRS((min+max)*.5f,Quaternion.identity,(max-min)*.5f);
            var eye=WorldEye;bool inside=false;
            if(eye!=null){var c=toCloud.MultiplyPoint3x4(eye.transform.position);float pad=eye.nearClipPlane+.05f;inside=c.x>min.x-pad&&c.x<max.x+pad&&c.y>min.y-pad&&c.y<max.y+pad&&c.z>min.z-pad&&c.z<max.z+pad;}
            var b=smilerBlock;b.SetMatrix(SmilerToCloudId,toCloud);b.SetMatrix(SmilerToWorldId,toWorld);b.SetVector(SmilerBoxMinId,min);b.SetVector(SmilerBoxMaxId,max);
            b.SetFloat(SmilerSoftId,!LowGraphics&&eye!=null&&(eye.depthTextureMode&DepthTextureMode.Depth)!=0?1:0);
            b.SetVector(SmilerAId,new Vector4(time,(seed%977)*.0137f,p.Presence,p.Spread));b.SetVector(SmilerBId,new Vector4(p.Gather,p.Surge,p.Rise,p.Churn));
            b.SetVector(SmilerCId,new Vector4(drift.x,drift.y,drift.z,p.Bob));b.SetVector(SmilerDId,new Vector4(p.Eyes,p.Squint,p.Grin,p.Open));b.SetVector(SmilerEId,new Vector4(p.Face,p.Glow,NativeSmilerLook.Reach,0));
            Graphics.DrawMesh(smilerBox,box,inside?smilerInside:smilerOutside,0,null,0,b,ShadowCastingMode.Off,false);
        }
        /// <summary>HordeGoreHit: a hit on a Smiler tears black smoke off it along the shot (no wound, no blood) and roils it;
        /// true when the add was a Smiler.</summary>
        private bool SmilerHit(NativeMinion m,HordeKindState s,in ArenaEvent e,Vector3 at,Vector3 dir)
        {
            int kind=s!=null&&s.Kind>=0?s.Kind:NativeHordeKinds.Resolve(m?.Pve?.Stats?.id);if(!SmilerIs(kind))return false;
            if(m!=null)SmilerViewOf(m.Id).HitAt=e.Time;
            var point=e.Position.x==0&&e.Position.y==0&&e.Position.z==0?at+Vector3.up*.3f:NativeArena.Vec(e.Position);uint seed=unchecked((uint)e.Id*2654435761u);
            for(int i=0;i<4;i++){var r=new Vector3(SmilerHash(seed,i,0)-.5f,SmilerHash(seed,i,1)-.3f,SmilerHash(seed,i,2)-.5f);SmilerPuffAdd(point+r*.15f,dir*(.8f+1.2f*SmilerHash(seed,i,3))+r*.9f,.3f+.3f*SmilerHash(seed,i,4),1.1f,seed+(uint)i);}
            return true;
        }
        private void SmilerPuffAdd(Vector3 at,Vector3 velocity,float size,float life,uint seed)=>smilerPuffs[smilerPuffNext++%smilerPuffs.Length]=new SmilerPuff{At=at,Velocity=velocity,Born=hordeClock,Size=size,Life=life,Seed=seed};
        private static float SmilerHash(uint seed,int i,int axis){uint h=seed^unchecked((uint)(i*0x9E3779B1)+(uint)axis*0x85EBCA77u);h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h&0xFFFFFF)/16777216f;}
        /// <summary>DrawHordeKindFx: the torn smoke (soft black puffs slowing, swelling and thinning as they drift up); the views
        /// of Smilers no longer drawn are let go.</summary>
        private void SmilerFx(float now)
        {
            if(vfx!=null)for(int i=0;i<smilerPuffs.Length;i++)
            {
                var q=smilerPuffs[i];if(q.Life<=0)continue;float age=now-q.Born;if(age<0||age>q.Life){if(age>q.Life)smilerPuffs[i].Life=0;continue;}
                float u=age/q.Life,go=(1-Mathf.Exp(-age*2.5f))/2.5f;
                vfx.Vapor(q.At+q.Velocity*go+Vector3.up*(.18f*age),SmilerSmoke,.85f*Mathf.Pow(1-u,1.5f),q.Size*(1+1.6f*u),u,q.Seed%8191u,NativeVfxDraw.NoFloor,0);
            }
            if((Time.frameCount&63)!=0)return;smilerStale.Clear();
            foreach(var pair in smilerViews)if(pair.Value.Frame<Time.frameCount-120)smilerStale.Add(pair.Key);
            foreach(int id in smilerStale)smilerViews.Remove(id);
            if(smilerBursts.Count>32)smilerBursts.Clear();
        }
        private void ClearSmilers(){smilerViews.Clear();smilerBursts.Clear();for(int i=0;i<smilerPuffs.Length;i++)smilerPuffs[i].Life=0;}
    }
}
