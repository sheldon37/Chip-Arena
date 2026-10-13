using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The split-in-two death (owner 2026-10-12: "For the buzzsaw death on factory, can we add essentially a "split in the
    /// middle" gib? This can be useful for future things too"). Presentation only, from replicated data only: a pilot's death
    /// record that a stage hazard killed it on the Factory, the map's hazards, the round, FightAt and the record's time and
    /// place find the saw that did it (Core NativeSawBisect, as the authority's hazard step would have), so the host, guests,
    /// late joiners and the killcam split the same body the same way. The cut (Core NativeBisect) is the killing blade's plane
    /// through the pelvis; the body model the corpse draws (its skin's tier, wound cuts included) and its hat are split once
    /// into two half models with sealed caps (NativeBisectModels), each drawn on its own half ragdoll (NativeBisectCorpse). The
    /// halves hold on the blade for the request's hitch (shuddering, sparks, blood out of the seam), then part; the blood
    /// sprays along the cut and off the blade's rim land as the round's splats, each half's trunk fountains as it falls, and
    /// the blade keeps its blood (NativeFactoryRig). The dropped weapon stays whole. Gore off (<see cref="Gore"/>), a killer's
    /// purchased kill effect, a Siren or a revivable PvE body: the current death, unchanged. Reduced effects and the low
    /// graphics tier thin the sparks and the blood (the split itself stays). No hit, damage, timing, network or hazard rule
    /// reads any of this.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>Gore: false keeps every death as it was (no split, no cut blood, no blade blood). There is no player setting
        /// for it yet; this is the switch one would drive.</summary>
        public bool Gore=true;
        private sealed class BisectFx
        {
            public string Key,Hat,Model;public BisectRequest Request;public BisectLook Look;public Matrix4x4[] Bind;public int Skin,Band=-1,Fired;public uint Seed;public bool Halo,Fixed,Settled;
            public NativeSourceModels.BisectJob Job;public readonly Dictionary<string,string[]> Models=new Dictionary<string,string[]>();
        }
        /// <summary>The split runs on a worker while the body holds on the blade (false: at once, as it always does on WebGL,
        /// which has no threads).</summary>
        public bool SplitInBackground=true;
        private int bisectKeys;private readonly BisectSpark[] bisectSparks=new BisectSpark[NativeBisect.MaxSparks];
        private static NVector3 CutNum(Vector3 v)=>new NVector3(v.x,v.y,v.z);
        private static Vector3 CutVec(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);

        /// <summary>A pilot death a Factory saw made, split: both half corpses (the second the first's Twin), or null for the
        /// current death (not a saw, gore off, a kill effect that shows no body, a Siren or a revivable body, models missing).</summary>
        private NativeCorpse SawBisect(NativeArena arena,NativeDeath death,CapturedAnimation.Player rig)
        {
            if(sourceModels==null||death==null||death.damageKind!="hazard"||!NativeFactoryHazards.Active(arena.MapId)||!NativeBisect.Applies(Gore,death.style,death.kept,death.model))return null;
            var hazards=arena.Map?.hazards;if(hazards==null||hazards.Length==0)return null;int round=NativeStructureFx.RoundIndex(arena.Match.Set,arena.Match.Round);
            int index=NativeSawBisect.Find(hazards,round,arena.Match.FightAt,death.at,NativeArena.Vec(death.position));if(index<0)return null;
            double clock=NativeFactoryHazards.Clock(NativePhase.Fight,arena.Match.FightAt,death.at);var saw=hazards[index];
            return SplitCorpse(arena,death,rig,-1,pelvis=>NativeSawBisect.Request(saw,round,clock,CutNum(pelvis),unchecked((uint)death.seed*2654435761u)));
        }
        /// <summary>Splits a death's body by the request `cut` makes for its pelvis; `skin` -1 the pilot's own (its wound cuts
        /// included), else that skin unwounded (the Dev Sandbox preview).</summary>
        private NativeCorpse SplitCorpse(NativeArena arena,NativeDeath death,CapturedAnimation.Player rig,int skin,Func<Vector3,BisectRequest> cut)
        {
            var a=death.actor;var fx=new BisectFx{Key="bisect-"+death.id+"-"+death.seed+"-"+(++bisectKeys),Seed=unchecked((uint)death.seed*2246822519u+7u),Fixed=skin>=0};
            fx.Halo=a?.Hat==NativeAuditorHalo.Hat;fx.Hat=!fx.Halo&&!string.IsNullOrEmpty(a?.Hat)&&a.Hat!="none"?HatModel(a.Hat,a.HatColor):null;if(fx.Hat!=null&&!sourceModels.Has(fx.Hat))fx.Hat=null;
            // The corpses come from the split's plan (which bodies each half keeps, and their bounds); the split itself runs on a
            // worker while the body holds on the blade (the whole body draws meanwhile, still), and is waited for before the halves
            // part. A body first seen after its hold (a late join, a corpse rebuilt after the killcam) splits at once.
            bool background=SplitInBackground&&Application.platform!=RuntimePlatform.WebGLPlayer&&!fx.Fixed;
            var corpse=NativeCorpse.Bisected(death,rig,arena,(bind,pelvis)=>
            {
                fx.Bind=bind;fx.Request=cut(pelvis);int tier=BodyTier(pelvis,false);string model;
                if(skin>=0){fx.Skin=skin;model=BodyModel(skin,tier);}
                else model=sourceModels.WoundedPilot(death.id,a?.Wounds,a?.BodyColor,ForceCompatibilityRendering,BodyAt(death.id,pelvis,out fx.Skin));
                fx.Look=NativeBodyModels.CutLook(fx.Skin);fx.Model=model;var r=fx.Request;
                fx.Job=sourceModels.BisectBegin(fx.Key+"/"+model,model,fx.Hat,bind,new BisectPlane(r.Point,r.Normal),fx.Look,tier,fx.Seed);if(fx.Job==null)return (null,null);
                var plan=NativeBisect.Plan(fx.Job.Sources,fx.Job.Draw,fx.Job.Plane);background&=arena.Time-death.at<.5f*r.Hitch;NativeSourceModels.BisectRun(fx.Job,background);return (plan,r);
            });
            if(corpse==null){ReleaseBisect(fx);return null;}
            corpse.Bisect=fx;corpse.OnRelease=()=>ReleaseBisect(fx);corpse.AwaitSplit=()=>Settle(corpse);if(!background)Settle(corpse);return corpse;
        }
        /// <summary>The split is done (or is waited for): its halves are registered, and the corpses take its exact pieces (their
        /// fountains start at their cut faces). A failed split keeps the whole body.</summary>
        private void Settle(NativeCorpse corpse)
        {
            if(!(corpse.Bisect is BisectFx fx)||fx.Settled)return;fx.Settled=true;var names=sourceModels?.BisectFinish(fx.Job);var cut=fx.Job?.Cut;
            if(names==null||cut==null)return;fx.Models[fx.Model]=names;corpse.Settle(cut);
        }
        /// <summary>Another tier of the body (the camera's distance changed), split at once.</summary>
        private string[] Halves(BisectFx fx,string model,int tier)
        {
            var r=fx.Request;var names=sourceModels.Bisect(fx.Key+"/"+model,model,fx.Hat,fx.Bind,new BisectPlane(r.Point,r.Normal),fx.Look,tier,fx.Seed,out var cut);
            if(names!=null)fx.Models[model]=names;return names;
        }
        private void ReleaseBisect(BisectFx fx){if(sourceModels==null)return;foreach(var model in fx.Models.Keys)sourceModels.ReleaseBisect(fx.Key+"/"+model);fx.Models.Clear();}
        /// <summary>Both halves as the corpse is presented, each held off the plane by the hitch's shudder.</summary>
        private static void BisectPresent(NativeCorpse corpse,float age,Matrix4x4 overlay,float sink,float scale)
        {
            if(!(corpse.Bisect is BisectFx fx)||corpse.Twin==null)return;float off=NativeBisect.Shudder(fx.Request,age);var n=CutVec(fx.Request.Normal);
            corpse.Present(Matrix4x4.Translate(-n*off)*overlay,sink,scale);corpse.Twin.Present(Matrix4x4.Translate(n*off)*overlay,sink,scale);
        }
        /// <summary>
        /// Draws a split body instead of the whole one: each half's body and hat models on that half's bones (the body at the
        /// tier the whole corpse would draw now: `model` is the corpse's own wounded model id, built on first sight of a new
        /// tier), the dropped weapon on its half, an Auditor halo over the head's half and the team bands on their halves.
        /// </summary>
        private bool BisectDraw(NativeCorpse corpse,NativeDeath death,string model,int bodySkin,float alpha,float clock)
        {
            if(!(corpse.Bisect is BisectFx fx)||corpse.Twin==null)return false;
            // Until the split is in, the whole body draws (it holds still on the blade meanwhile).
            if(!fx.Settled){if(fx.Job!=null&&fx.Job.Done)Settle(corpse);if(!fx.Settled)return false;}if(fx.Models.Count==0)return false;
            if(fx.Fixed)model=BodyModel(fx.Skin,BodyTier(corpse.PresentedPosition(CapturedRig.Pelvis),false));
            if(model==null||!fx.Models.TryGetValue(model,out var names)){names=null;if(model!=null&&fx.Models.Count<4)names=Halves(fx,model,BodyTier(corpse.PresentedPosition(CapturedRig.Pelvis),false));if(names==null)foreach(var any in fx.Models.Values){names=any;break;}}
            if(names==null)return false;var a=death.actor;Color tint=BodyTint(a?.BodyColor),accent=BodyAccent(fx.Fixed?fx.Skin:bodySkin,a?.AccentColor,a?.Hat,a?.HatColor);Color? hat=HatBody(a?.Hat,a?.HatColor);
            for(int h=0;h<2;h++)
            {
                var half=h==0?corpse:corpse.Twin;sourceModels.QueueShadowless(names[h],Matrix4x4.identity,half.PresentedBone,half.ShownBone,tint,accent,alpha);
                if(names[2+h]!=null)sourceModels.QueueShadowless(names[2+h],Matrix4x4.identity,half.PresentedBone,null,hat,null,alpha);
                var drop=half.PresentedWeapon;if(drop.HasValue&&corpse.LooseModel!=null)sourceModels.QueueShadowless(corpse.LooseModel,drop.Value,null,null,null,null,alpha);
            }
            var r=fx.Request;NativeCorpse Side(Vector3 p)=>r.Side(CutNum(p))>=0?corpse.Twin:corpse;
            if(fx.Halo)AuditorHalo(a?.Hat,Side(corpse.Bind[CapturedRig.Head].GetColumn(3)).PresentedBone(CapturedRig.Head),clock,alpha,HaloReduced,0,null,color:a?.HatColor);
            if(fx.Band>=0&&alpha>.5f&&StatusReady())for(int k=0;k<bandLocal.Length;k++)
            {
                int bone=NativeStatusVisuals.BandBones[k];var on=Side((corpse.Bind[bone]*bandLocal[k]).GetColumn(3));teamBands[fx.Band].Add(on.PresentedBone(bone)*bandLocal[k]);
            }
            return true;
        }
        /// <summary>The cut's blood as its moments pass (Core NativeBisect.Gush: out of the seam along the cut, then flung off
        /// the blade's rim), each landing as the round's splats; a late first sight skips the gushes long gone.</summary>
        private void BisectBlood(NativeCorpse corpse,NativeDeath death,float now)
        {
            if(!(corpse.Bisect is BisectFx fx))return;float age=now-death.at,low=death.position.y-NativeMeleeSlice.RootHeight,high=low+1.75f;
            while(fx.Fired<NativeBisect.Gushes)
            {
                int k=fx.Fired;if(!NativeBisect.Gush(fx.Request,k,low,high,out float at,out var origin,out var normal,out var along,out bool blade)){fx.Fired++;continue;}
                if(age<at)break;fx.Fired++;if(age-at>.6f||fx.Request.Blood<=0)continue;
                long key=((long)(death.id&0xffff)<<40)^((long)(death.seed&0xffffff)<<12)^(k+1)^(1L<<62);bool stain=stainedWounds.Add(key);
                blood.Spray(blade?"cut":"sever",blade?.5f:.35f,unchecked((uint)death.seed*2654435761u^(uint)(k+1)*0x9e3779b1u),key,origin,normal,along,death.at+at,BloodQuality,LowGraphics,stain,0,death.id,true);
            }
        }
        /// <summary>The cuts' sparks this frame (a metal cutter's, over its hold and a little after), as the burst sparks' streaks.</summary>
        private void BisectSparks(float now)
        {
            if(vfx==null)return;float q=Mathf.Clamp01(EffectsQuality)*(LowGraphics?.5f:1);
            foreach(var corpse in corpses.Values)if(corpse.Bisect is BisectFx fx)CutSparks(fx.Request,now-corpse.Death.at,q);
            if(galleryCorpse?.Bisect is BisectFx g)CutSparks(g.Request,now-galleryDeath.at,q);
        }
        private void CutSparks(BisectRequest r,float age,float quality)
        {
            int n=NativeBisect.Sparks(r,age,quality,bisectSparks);
            for(int i=0;i<n;i++){var s=bisectSparks[i];var c=new Color(4.2f,1.75f,.45f,0)*s.Heat;vfx.Streak(CutVec(s.Head),CutVec(s.Velocity),c,s.Width,s.Width*2.2f,.022f,.9f,s.U,s.Seed&0xffff);}
        }

        // ---- the Factory's blades keep their blood (NativeFactoryRig smears from the hazard clock of each saw's last kill) ----
        private double[] sawBlood=Array.Empty<double>();private readonly Dictionary<long,int> sawKills=new Dictionary<long,int>();
        private double[] SawBlood(NativeArena arena,FactoryHazard[] hazards)
        {
            if(!Gore||hazards==null)return null;if(sawBlood.Length!=hazards.Length)sawBlood=new double[hazards.Length];for(int i=0;i<sawBlood.Length;i++)sawBlood[i]=double.NaN;
            int round=NativeStructureFx.RoundIndex(arena.Match.Set,arena.Match.Round);if(sawKills.Count>256)sawKills.Clear();
            foreach(var p in arena.Pilots)
            {
                var d=p.Death;if(d==null||d.damageKind!="hazard")continue;long key=((long)d.id<<32)^(uint)d.seed;
                if(!sawKills.TryGetValue(key,out int index)){index=NativeSawBisect.Find(hazards,round,arena.Match.FightAt,d.at,NativeArena.Vec(d.position));sawKills[key]=index;}
                if(index<0||index>=sawBlood.Length)continue;double at=NativeFactoryHazards.Clock(NativePhase.Fight,arena.Match.FightAt,d.at);if(double.IsNaN(sawBlood[index])||at>sawBlood[index])sawBlood[index]=at;
            }
            return sawBlood;
        }

        // ---- the Dev Sandbox preview: a split on a loop before you, presentation only ----
        /// <summary>Dev Sandbox "Preview Saw Split": every <see cref="SawGalleryLoop"/> s a stand-in of you, 3.2 m ahead and facing
        /// you, is split by a phantom blade (a slot blade rising through it, then a rail blade sweeping across), cycling the
        /// Standard, Sheldon Clothes and Prototype bodies in your hat. Nothing reaches the match.</summary>
        public bool SawSplitGallery;public const float SawGalleryLoop=3.4f;
        private NativeCorpse galleryCorpse;private NativeDeath galleryDeath;private int galleryRun;
        private void ClearSawGallery(){galleryCorpse?.Dispose();galleryCorpse=null;galleryDeath=null;}
        private void SawSplitPreview(NativeArena arena)
        {
            if(!SawSplitGallery||!arena.Sandbox){if(galleryCorpse!=null)ClearSawGallery();return;}
            var me=arena.Local;if(me?.Body==null||sourceModels==null)return;float now=arena.Time;
            if(galleryCorpse==null||now-galleryDeath.at>=SawGalleryLoop||now<galleryDeath.at)
            {
                ClearSawGallery();int run=galleryRun++;int skin=new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex,NativeBodySkins.PrototypeIndex}[run%3];bool slot=run%2==0;
                float yaw=me.Yaw;var facing=Quaternion.Euler(0,yaw,0)*Vector3.forward;var at=me.Body.position+facing*3.2f;var actor=me.Pose.ViewCopy();actor.Wounds=null;actor.Hp=Math.Max(1,actor.Hp);actor.Yaw=yaw+180;
                galleryDeath=new NativeDeath{id=990700+run%64,seed=0x5a17+run,at=now,position=at,yaw=yaw+180,style="impact-ragdoll",model="pilot",damageKind="hazard",actor=actor};
                var rig=captured.Create(transform,galleryDeath.id);rig.ActorPose(actor,at-Vector3.up*NativeMeleeSlice.RootHeight,now);
                var across=Quaternion.Euler(0,yaw,0)*Vector3.right;
                galleryCorpse=SplitCorpse(arena,galleryDeath,rig,skin,pelvis=>
                {
                    // Slot: a vertical blade along your line of sight rising from the floor (a left/right split). Rail: one across it
                    // at chest height sweeping sideways (front/back).
                    var normal=slot?across:facing;var travel=slot?-facing*6:across*7.5f;var hub=slot?new Vector3(pelvis.x,me.Body.position.y-NativeMeleeSlice.RootHeight,pelvis.z)+facing*.25f:pelvis+Vector3.up*.55f-across*.2f;
                    var r=NativeBisect.Request(slot?"preview-saw-slot":"preview-saw-rail",CutNum(pelvis),CutNum(normal),CutNum(travel),unchecked((uint)galleryDeath.seed*2654435761u),part:slot?1.55f:1.35f,lift:slot?1.1f:.5f,push:.16f,topple:slot?2.6f:2.1f,hitch:slot?.17f:.13f,blood:1.6f,sparks:true);
                    r.Hub=CutNum(hub);r.Axle=CutNum(normal);r.Rate=slot?46:-46;r.Radius=slot?.8f:.85f;return r;
                });
                rig.Dispose();if(galleryCorpse==null){galleryDeath=null;return;}
                galleryCorpse.SetupBisect(NativeKillEffectRules.MaxDeathVoxels/3,Mathf.Clamp01(EffectsQuality),false,null);
            }
            float age=now-galleryDeath.at,clock=KillClock(arena);galleryCorpse.Step(age);BisectPresent(galleryCorpse,age,Matrix4x4.identity,0,1);
            BisectDraw(galleryCorpse,galleryDeath,null,0,1,clock);BisectBlood(galleryCorpse,galleryDeath,clock);
            KillSpray(galleryCorpse,age,1,clock);if(galleryCorpse.Twin!=null)KillSpray(galleryCorpse.Twin,age,1,clock);
            GalleryBlade((BisectFx)galleryCorpse.Bisect,age);
        }
        /// <summary>The preview's phantom blade (four turned plates on a hub, as the Factory's) passing through the cut, bloodied after it.</summary>
        private void GalleryBlade(BisectFx fx,float age)
        {
            var r=fx.Request;if(age>.9f)return;var hub=CutVec(r.Hub)+CutVec(r.Travel)*(age-.08f);var axle=CutVec(r.Axle).normalized;float spin=r.Rate*age,R=r.Radius,half=R/Mathf.Sqrt(2);
            var basis=Quaternion.LookRotation(axle,Mathf.Abs(axle.y)<.9f?Vector3.up:Vector3.right);var steel=new Color(.55f,.58f,.62f);var dark=new Color(.08f,.09f,.1f);var red=new Color(.36f,.02f,.03f);
            for(int k=0;k<4;k++)sourceModels.WoundCube(Matrix4x4.TRS(hub,basis*Quaternion.Euler(0,0,(spin+k*Mathf.PI/8)*Mathf.Rad2Deg),new Vector3(half*2,half*2,.05f)),steel);
            sourceModels.WoundCube(Matrix4x4.TRS(hub,basis*Quaternion.Euler(0,0,spin*Mathf.Rad2Deg),new Vector3(.34f,.34f,.09f)),dark);
            if(age>.05f)for(int k=0;k<6;k++){float a=spin+k*2.4f,rr=R*(.4f+.09f*k);var p=hub+basis*new Vector3(Mathf.Cos(a)*rr,Mathf.Sin(a)*rr,0);sourceModels.WoundCube(Matrix4x4.TRS(p,basis*Quaternion.Euler(0,0,a*Mathf.Rad2Deg),new Vector3(.14f,.04f,.058f)),red);}
        }
    }
}
