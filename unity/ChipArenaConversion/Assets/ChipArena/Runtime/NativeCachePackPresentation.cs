using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The caches' licensed effects layer (Core NativeCachePackFx; Claude C2, 2026-11-25): Action RPG FX's chest glow at the latch
    // line of each steel locker for sale (the nearest few to the camera) and its chest opening once per opening as the lid lifts,
    // each at its tier's level and colour, as particle systems from the git-ignored effects prefab (Resources/ChipArenaCacheFx).
    // Read once; without the prefab nothing extra draws. Pooled, no allocation once warm; off on the low graphics tier and with
    // Reduced effects (then only the lid moves, the owner's rule). The opening's moment, tier and seed are the replicated view's
    // (NativeCacheClock), so every client plays the same one burst; nothing here reaches a rule, a collider or the network.
    public sealed partial class ArenaPresentation
    {
        private sealed class CachePackLive
        {
            public GameObject Go;public ParticleSystem Root,Halo,Sparkles,Rays,Splash,Linger;public bool Burst,Stopping;public NativeCacheTier Tier;
            public string Cache;public float Until=float.PositiveInfinity;public int Claimed=-1;
        }
        private GameObject cachePackGlow,cachePackOpen;private bool cachePackRead;private NativeFxLife cachePackRoot;private int cachePackFrame=-1;
        private readonly List<CachePackLive> cachePackLive=new List<CachePackLive>(16);
        private readonly NativeCacheBursts cacheBursts=new NativeCacheBursts();
        private readonly List<(float d,int i)> cachePackNear=new List<(float,int)>(16);
        private static readonly System.Comparison<(float d,int i)> CachePackNearest=(a,b)=>a.d.CompareTo(b.d);

        private bool CachePackBank()
        {
            if(cachePackRead)return cachePackGlow!=null;cachePackRead=true;
            var prefab=Resources.Load<GameObject>(NativeCachePackFx.Resource);if(prefab==null)return false;
            var glow=prefab.transform.Find(NativeCachePackFx.Glow);var open=prefab.transform.Find(NativeCachePackFx.Open);if(glow==null||open==null)return false;
            cachePackGlow=glow.gameObject;cachePackOpen=open.gameObject;var root=new GameObject("Cache pack effects");root.transform.SetParent(transform,false);
            cachePackRoot=root.AddComponent<NativeFxLife>();cachePackRoot.Off=CachePackOff;return true;
        }
        private void CachePackOff(){foreach(var l in cachePackLive)if(l.Go!=null&&l.Go.activeSelf){l.Go.SetActive(false);l.Cache=null;}}

        // CacheFx, once a frame: the nearest lockers for sale keep their glow, each opening plays its burst once, the rest put away.
        private void CachePackFx(Camera camera)
        {
            if(cachePackFrame==Time.frameCount)return;cachePackFrame=Time.frameCount;
            if(LowGraphics||CacheReduced||!NativeCachePackFx.On||!CachePackBank())return;
            cachePackRoot.Drawn=Time.frameCount;float now=Time.time;int frame=Time.frameCount;
            // the for-sale glows: the nearest MaxGlows lockers within GlowReach of the camera
            var eye=camera!=null?camera.transform.position:Vector3.zero;cachePackNear.Clear();
            for(int i=0;i<cacheFrameCount;i++)
            {
                var s=cacheAll[i];if(!NativeCachePackFx.Glowing(s,CacheReduced))continue;
                float d=(new Vector3(s.X,s.Y,s.Z)-eye).sqrMagnitude;if(d<=NativeCachePackFx.GlowReach*NativeCachePackFx.GlowReach)cachePackNear.Add((d,i));
            }
            cachePackNear.Sort(CachePackNearest);
            for(int n=0;n<cachePackNear.Count&&n<NativeCachePackFx.MaxGlows;n++)
            {
                var s=cacheAll[cachePackNear[n].i];CachePackLive use=null;
                foreach(var l in cachePackLive)if(!l.Burst&&l.Go.activeSelf&&!l.Stopping&&l.Tier==s.Tier&&l.Cache==s.Id){use=l;break;}
                if(use==null)use=CachePackStart(false,s,NativeCachePackFx.GlowX,NativeCachePackFx.GlowY,NativeCachePackFx.GlowZ,float.PositiveInfinity);
                if(use!=null)use.Claimed=frame;
            }
            // a glow no longer wanted (bought, sold, restocking, another tier, too far) stops emitting; its sparkles live out
            foreach(var l in cachePackLive)
                if(!l.Burst&&l.Go.activeSelf&&!l.Stopping&&l.Claimed!=frame){l.Stopping=true;l.Until=now+NativeCachePackFx.GlowFade;l.Root.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
            // each opening's one burst, of the tier that opened, as the lid lifts
            for(int i=0;i<cacheFrameCount;i++)
            {
                var s=cacheAll[i];if(!cacheBursts.Fire(s,CacheReduced))continue;
                CachePackStart(true,s,NativeCachePackFx.OpenX,NativeCachePackFx.OpenY,NativeCachePackFx.OpenZ,now+NativeCachePackFx.OpenLife);
            }
            foreach(var l in cachePackLive)if(l.Go.activeSelf&&now>=l.Until){l.Go.SetActive(false);l.Cache=null;}
        }
        // A glow or a burst started at the locker-frame point (x, y, z) of cache `s`, in its tier's level and colour: a free pooled
        // one, a new one, or (bursts only, at the cap) the oldest burst.
        private CachePackLive CachePackStart(bool burst,in NativeCacheShow s,float x,float y,float z,float until)
        {
            int same=0;CachePackLive use=null,oldest=null;
            foreach(var l in cachePackLive)
            {
                if(l.Burst!=burst)continue;
                if(!l.Go.activeSelf){use??=l;continue;}
                same++;if(oldest==null||l.Until<oldest.Until)oldest=l;
            }
            if(use==null&&same>=(burst?NativeCachePackFx.MaxBursts:NativeCachePackFx.MaxGlows)){if(!burst)return null;use=oldest;}
            if(use==null)
            {
                var go=Instantiate(burst?cachePackOpen:cachePackGlow,cachePackRoot.transform,false);go.name=burst?"Cache pack opening":"Cache pack glow";go.SetActive(false);
                use=new CachePackLive{Go=go,Burst=burst,Root=go.GetComponent<ParticleSystem>(),Halo=CachePackSystem(go,NativeCachePackFx.Halo),Sparkles=CachePackSystem(go,NativeCachePackFx.Sparkles),
                    Rays=CachePackSystem(go,NativeCachePackFx.Rays),Splash=CachePackSystem(go,NativeCachePackFx.Splash),Linger=CachePackSystem(go,NativeCachePackFx.Linger)};
                if(use.Root==null){Destroy(go);return null;}
                cachePackLive.Add(use);
            }
            NativeCacheOpenFx.ToWorld(s.Yaw,x,z,out float wx,out float wz);
            use.Go.transform.SetPositionAndRotation(new Vector3(s.X+wx,s.Y+y,s.Z+wz),Quaternion.Euler(0,s.Yaw+180,0));use.Go.transform.localScale=Vector3.one*NativeCachePackFx.Scale;
            use.Cache=s.Id;use.Tier=s.Tier;use.Stopping=false;use.Until=until;
            CachePackLevel(use,s.Tier);
            if(!use.Go.activeSelf)use.Go.SetActive(true);
            use.Root.Clear(true);use.Root.Play(true);
            return use;
        }
        private static ParticleSystem CachePackSystem(GameObject go,string name){var t=go.transform.Find(name);return t!=null?t.GetComponent<ParticleSystem>():null;}
        // The tier's level on the pooled effect (Core NativeCachePackFx.Levels: the pack's Lv1-4 numbers) and its colour (the tier's;
        // the sparkles keep the atlas tile's own colour).
        private static void CachePackLevel(CachePackLive l,NativeCacheTier tier)
        {
            var v=NativeCachePackFx.LevelOf(tier);var tint=CachePackColor(NativeCacheTiers.Hex(tier));
            CachePackTint(l.Root,tint);CachePackTint(l.Halo,tint);
            float frame=v.Tile/(float)(NativeCachePackFx.TilesX*NativeCachePackFx.TilesY);
            if(l.Sparkles!=null)
            {
                var m=l.Sparkles.main;m.startLifetime=new ParticleSystem.MinMaxCurve(v.SparkLifeMin,v.SparkLifeMax);
                var e=l.Sparkles.emission;e.rateOverTime=v.SparkRate;var uv=l.Sparkles.textureSheetAnimation;uv.startFrame=frame;
            }
            if(l.Rays!=null)
            {
                var m=l.Rays.main;m.startLifetime=new ParticleSystem.MinMaxCurve(v.RaysLifeMin,v.RaysLifeMax);m.startSize=new ParticleSystem.MinMaxCurve(1,v.RaysSize);
                tint.a=v.RaysAlpha;m.startColor=tint;
            }
            CachePackCount(l.Splash,v.Splash,frame,v.SplashSpeed);CachePackCount(l.Linger,v.Linger,frame,-1);
        }
        private static void CachePackTint(ParticleSystem ps,Color tint){if(ps==null)return;var m=ps.main;float a=m.startColor.mode==ParticleSystemGradientMode.Color?m.startColor.color.a:1;tint.a=a;m.startColor=tint;}
        private static void CachePackCount(ParticleSystem ps,int count,float frame,float speed)
        {
            if(ps==null)return;var e=ps.emission;if(e.burstCount>0){var b=e.GetBurst(0);b.count=count;e.SetBurst(0,b);}
            var uv=ps.textureSheetAnimation;uv.startFrame=frame;
            if(speed>0){var m=ps.main;m.startSpeed=new ParticleSystem.MinMaxCurve(1,speed);}
        }
        private static Color CachePackColor(int hex)=>new Color(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,1);
    }
    // A licensed effects root (the caches', the missile trial's): puts every effect away a frame after the presentation stops drawing
    // them (no arena, the menu).
    internal sealed class NativeFxLife:MonoBehaviour
    {
        internal int Drawn=-1;internal System.Action Off;
        private void LateUpdate(){if(Off!=null&&Drawn<Time.frameCount-1)Off();}
    }
}
