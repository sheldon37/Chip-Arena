using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Magic Missiles visual trial (Core NativeMissileTrial; Claude C2, 2026-11-25): the fire staff's primary bolt as the pack's
    // FireMissile riding the bolt's own head, its hits as the pack's FireExplosion at the same Impact or direct Hit event. From the
    // git-ignored effects prefab (Resources/ChipArenaMissileFx), read once; without it, or on the low graphics tier, the bolt draws as
    // today. Pooled, no allocation once warm. Presentation only: the path, the hit's moment and its damage are the authority's.
    public sealed partial class ArenaPresentation
    {
        private sealed class TrialFx{public GameObject Go;public ParticleSystem Root;public long Key;public bool Explosion,Stopping;public float Until=float.PositiveInfinity;public int Frame=-1;}
        private GameObject trialMissile,trialExplosion;private bool trialRead;private NativeFxLife trialRoot;
        private readonly List<TrialFx> trialLive=new List<TrialFx>(24);

        private bool TrialBank()
        {
            if(trialRead)return trialMissile!=null;trialRead=true;
            var prefab=Resources.Load<GameObject>(NativeMissileTrial.Resource);if(prefab==null)return false;
            var m=prefab.transform.Find(NativeMissileTrial.Missile);var x=prefab.transform.Find(NativeMissileTrial.Explosion);if(m==null||x==null)return false;
            trialMissile=m.gameObject;trialExplosion=x.gameObject;var root=new GameObject("Missile trial effects");root.transform.SetParent(transform,false);
            trialRoot=root.AddComponent<NativeFxLife>();trialRoot.Off=TrialOff;return true;
        }
        private void TrialOff(){foreach(var l in trialLive)if(l.Go!=null&&l.Go.activeSelf){l.Go.SetActive(false);l.Key=0;}}

        // ShotFx: the trial's bolt as the pack missile at its head this frame; true when it is drawn so (ShotFx then draws nothing else
        // for it: no energy core, pack head, trail sprites or ribbon).
        private bool MissileTrialShot(in NativeShotView v)
        {
            if(LowGraphics||!NativeMissileTrial.Draws(v)||!TrialBank())return false;
            long key=NativeShotScene.Key(v.Kind,v.Id);var at=new Vector3((float)v.X,(float)v.Y,(float)v.Z);var velocity=new Vector3((float)v.VX,(float)v.VY,(float)v.VZ);
            var turn=velocity.sqrMagnitude>1e-6f?Quaternion.LookRotation(velocity):Quaternion.identity;
            TrialFx use=null;foreach(var l in trialLive)if(!l.Explosion&&!l.Stopping&&l.Go.activeSelf&&l.Key==key){use=l;break;}
            if(use!=null)use.Go.transform.SetPositionAndRotation(at,turn);
            else{use=TrialStart(false,at,turn,NativeMissileTrial.MissileScale,float.PositiveInfinity);if(use==null)return false;use.Key=key;}
            use.Frame=Time.frameCount;trialRoot.Drawn=Time.frameCount;return true;
        }
        // MagicPackHits, for each new hit of a school bolt: the trial's hit as the pack explosion at the event (out of the surface along its
        // normal, or back along the hit); true when the trial owns the hit (the sprite burst is then not drawn).
        private bool MissileTrialHit(NativeMagicSchool school,Vector3 at,Vector3 normal,float age,float now)
        {
            if(LowGraphics||!NativeMissileTrial.DrawsHit(school)||!TrialBank())return false;
            if(age<NativeMissileTrial.Stale)TrialStart(true,at+normal*.05f,normal.sqrMagnitude>.25f?Quaternion.LookRotation(normal):Quaternion.identity,NativeMissileTrial.ExplosionScale,now+NativeMissileTrial.ExplosionLife);
            return true;
        }
        // Once a frame, after the shots: a missile whose bolt left the frame stops emitting (its trails live out); finished ones put away.
        private void MissileTrialFrame(float now)
        {
            if(trialRoot==null)return;int frame=Time.frameCount;bool any=false;
            foreach(var l in trialLive)
            {
                if(!l.Go.activeSelf)continue;any=true;
                if(!l.Explosion&&!l.Stopping&&l.Frame!=frame){l.Stopping=true;l.Until=now+NativeMissileTrial.TrailFade;l.Root.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
                if(now>=l.Until){l.Go.SetActive(false);l.Key=0;}
            }
            if(any)trialRoot.Drawn=frame;
        }
        private TrialFx TrialStart(bool explosion,Vector3 at,Quaternion turn,float scale,float until)
        {
            int same=0;TrialFx use=null,oldest=null;
            foreach(var l in trialLive)
            {
                if(l.Explosion!=explosion)continue;
                if(!l.Go.activeSelf){use??=l;continue;}
                same++;if(oldest==null||l.Until<oldest.Until)oldest=l;
            }
            if(use==null&&same>=(explosion?NativeMissileTrial.MaxExplosions:NativeMissileTrial.MaxMissiles)){if(!explosion)return null;use=oldest;}
            if(use==null)
            {
                var go=Instantiate(explosion?trialExplosion:trialMissile,trialRoot.transform,false);go.name=explosion?"Missile trial explosion":"Missile trial missile";go.SetActive(false);
                use=new TrialFx{Go=go,Root=go.GetComponent<ParticleSystem>(),Explosion=explosion};if(use.Root==null){Destroy(go);return null;}
                trialLive.Add(use);
            }
            use.Go.transform.SetPositionAndRotation(at,turn);use.Go.transform.localScale=Vector3.one*scale;use.Stopping=false;use.Until=until;
            if(!use.Go.activeSelf)use.Go.SetActive(true);
            use.Root.Clear(true);use.Root.Play(true);
            return use;
        }
    }
}
