using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;

namespace ChipArena.UnityConversion
{
    // The Siren minion kinds on the PvE horde (owner 2026-10-12: "improve animations of siren minions and design. Give them
    // hitting from melee too. Add some variants (Like brutes, spitters, runners, etc.)"). Presentation only: everything comes
    // from what host, guests and the killcam replay all hold (each add's replicated kind, id, position, velocity, yaw, lost limbs,
    // crawl, possession, entry and LastAttack; the Hit / Headshot / Death / PveGib events; the content's timings) and the
    // presentation clock. No hit, collider, timing, damage, movement or network state is read back or changed.
    //  - Kinds and looks: the add's kind id (NativeMinionFrame.addKind) picks its sculpted model (Core/NativeHordeModels), its
    //    look by id; an unknown kind draws as the shambler. The DEV preview (Settings · Developer, or the Dev Sandbox's minion
    //    gallery) draws the proposed kinds before Astra's rules exist, looks only.
    //  - Crowd tiers, as horde-view.ts gives them out: the nearest 24 adds (16 on the low graphics tier) and the possessed add
    //    draw the full model, the rest within 40 m the reduced one and the far crowd the impostor, with 3 m of hysteresis.
    //  - Animation: Core/NativeHordeMotion poses every add on its own planted feet and seed; attacks wind up into a telegraph and
    //    strike at LastAttack + the authority's hit time (NativeHordeKinds.Strike), recover by its Recover, and a stagger that
    //    the authority applies in the wind-up cancels the shown blow as it cancels the real one. The entrances are the zombie's too
    //    (owner 2026-10-12: "They still use old model when entering through doors and vents"): a vent climb claws up and hauls
    //    over the rim, a door or cell walks it in on the gait after a waking twitch, a dive throws it into the shaft, and the
    //    brute's jump-down (NativeHordeDrop) leaps, falls and slams; all on the kind's own model, timed to the authority's entry.
    //  - Felt hits (Core/NativeHordeReact) from the replicated hits: directional recoil, the shove and stagger-back steps, the
    //    authority's stagger reel, a hit-stop on melee blows and the struck flash (NativeHitFlash) on the body; Siren ink sprays
    //    from the wound and lands as splats that stay (the felt hits' blood sim on its ink flag).
    //  - Telegraphs and effects (Core/NativeHordeFx), budgeted like the minion effects (24 adds' worth, halved past 24 m, none
    //    past 40 m; Reduced effects thin them): the glow flare of every wind-up, the brute's slam disc and shockwave, the runner's
    //    streaks, the shambler's claw crescent, the spitter's sac, glob and acid puddle, the bloater's swell, slam puff and burst.
    //  - Deaths per kind (the body chars and burns away on NativeMinionFx's clock as before), melee kills that slice the add along
    //    the killing swing (NativeMeleeSlice's plan; the severed parts tumble and burn away), and the authority's own severed
    //    limbs thrown as the model's real parts instead of the old bank's olive cubes.
    // Allocation free in steady state: the per-add states, rigs, part and effect pools are made once and reused.
    public sealed partial class ArenaPresentation
    {
        /// <summary>DEV preview (looks only): -1 draws each add as its own kind, 0..5 every add as that kind, Mix every add as a kind
        /// hashed from its id. The authority, its timings and hit volumes stay the add's real ones (Settings · Developer).</summary>
        public int HordePreview{get=>NativeHordeKinds.Preview;set=>NativeHordeKinds.Preview=value;}
        /// <summary>The Dev Sandbox's minion gallery: every kind in a row before the local pilot, looping its whole set (presentation only).</summary>
        public bool MinionGallery;
        private sealed class HordeKindState
        {
            public readonly NativeHordeGait Gait=new NativeHordeGait();public readonly NativeHordeReact React=new NativeHordeReact();
            public int Id,Kind=-1,Real,Look,Tier=-1,Lost,Frame;public float CancelAt=float.NaN,CancelFor=-100,Distance;public bool Possessed;
            public MeleeBlow Blow;public bool HasBlow;public float BlowAt=-100;public NVector2 Push;public string Spec;
            public void Reset(int id){Id=id;Kind=-1;Tier=-1;Lost=-1;CancelAt=float.NaN;CancelFor=-100;HasBlow=false;BlowAt=-100;Push=NVector2.Zero;Spec=null;Gait.Reset();React.Clear();Possessed=false;}
        }
        private readonly Dictionary<int,HordeKindState> hordeKindStates=new Dictionary<int,HordeKindState>();private readonly Stack<HordeKindState> hordeKindSpare=new Stack<HordeKindState>();
        private readonly List<int> hordeKindStale=new List<int>();private int hordeKindFrame,hordeKindEvent,hordeKindGeneration=-1;private NativeArena hordeKindArena;private float hordeKindTime=float.NaN;
        // The camera the tiers rank by (the last frame's: SourceWorld runs before the frame's camera reaches RenderGlow).
        private Vector3 hordeEye;private bool hordeEyeKnown;
        private float[] hordeTierDistance=new float[64];private int[] hordeTierOrder=new int[64];private HordeKindState[] hordeTierStates=new HordeKindState[64];
        /// <summary>Full models for the nearest adds (horde-view.ts maxRigs: quality 24, performance 16), reduced to 40 m, impostors beyond.</summary>
        public const int HordeFullRigs=24,HordeFullRigsLow=16;public const float HordeReducedRange=40,HordeTierHysteresis=3;
        // ---- the dead: each add's kind, look and last blow, kept for its death (the authority's record names only horde-N) ----
        private struct HordeDead{public int Kind,Look;public NVector2 Push;public bool Melee;public MeleeBlow Blow;public bool HasBlow;public float At;public uint Seed;public int Slice;public bool Planned;}
        private readonly Dictionary<int,HordeDead> hordeDead=new Dictionary<int,HordeDead>();private readonly List<int> hordeDeadStale=new List<int>();
        private readonly SlicePlan hordeSlicePlan=new SlicePlan();

        /// <summary>The kind an add of real kind `real` and id is drawn as (the DEV preview may substitute it).</summary>
        private static int HordeDrawKind(int real,int id){int p=NativeHordeKinds.Preview;return p>=0&&p<NativeHordeKinds.Count?p:p==NativeHordeKinds.Mix?(int)(NativeHordeKinds.Hash(NativeHordeKinds.Seed(id),3)*NativeHordeKinds.Count)%NativeHordeKinds.Count:real;}
        /// <summary>Whether this presentation draws the add with a kind model (the gore then throws the model's own parts).</summary>
        private bool HordeKindDrawn(int id)=>hordeKindStates.ContainsKey(id)||hordeDead.ContainsKey(id);
        private bool HordeKindsReady=>sourceModels!=null&&sourceModels.HordeKindModelCount>0;
        private HordeKindState HordeState(int id)
        {
            if(hordeKindStates.TryGetValue(id,out var s))return s;s=hordeKindSpare.Count>0?hordeKindSpare.Pop():new HordeKindState();s.Reset(id);hordeKindStates[id]=s;return s;
        }

        // ================================================================================================================
        // Frame begin: hits into reactions, ink, blows; the crowd's tiers
        // ================================================================================================================
        private void HordeKindsBegin(NativeArena arena)
        {
            hordeKindFrame++;if(!HordeKindsReady)return;float now=arena.Time;
            if(arena!=hordeKindArena||arena.Generation!=hordeKindGeneration||!float.IsNaN(hordeKindTime)&&now<hordeKindTime-.25f)ClearHordeKinds(arena);
            hordeKindTime=now;
            // New hits on adds (each once): the felt hit, the ink, the last melee blow (a melee kill slices along it).
            int newest=hordeKindEvent;var events=arena.Events;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=hordeKindEvent)continue;if(e.Id>newest)newest=e.Id;
                if((e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)||e.Target<10000||!(e.Amount>0))continue;
                var m=HordeMinion(arena,e.Target);if(m!=null&&m.Pve==null)continue;
                HordeHit(arena,m,e);
            }
            hordeKindEvent=newest;
            // The tiers: rank the drawn adds by distance (the possessed first), full for the nearest, reduced to 40 m, impostors past.
            int n=0;var minions=arena.Minions;
            for(int i=0;i<minions.Count;i++)
            {
                var m=minions[i];if(m.Pve==null||m.Hp<=0)continue;var s=HordeState(m.Id);s.Frame=hordeKindFrame;s.Possessed=m.Pve.Possessed;
                float d=hordeEyeKnown?Vector3.Distance(m.Body.transform.position,hordeEye):0;s.Distance=d;
                if(n==hordeTierStates.Length){Array.Resize(ref hordeTierStates,n*2);Array.Resize(ref hordeTierDistance,n*2);Array.Resize(ref hordeTierOrder,n*2);}
                hordeTierStates[n]=s;hordeTierDistance[n]=s.Possessed?-1:d-(s.Tier==NativeHordeModels.Full?HordeTierHysteresis:0);hordeTierOrder[n]=n;n++;
            }
            Array.Sort(hordeTierDistance,hordeTierOrder,0,n);int full=LowGraphics?HordeFullRigsLow:HordeFullRigs;
            for(int r=0;r<n;r++)
            {
                var s=hordeTierStates[hordeTierOrder[r]];float far=HordeReducedRange+(s.Tier==NativeHordeModels.Impostor?0:HordeTierHysteresis);
                s.Tier=r<full&&(s.Possessed||s.Distance<=far)?NativeHordeModels.Full:s.Distance<=far?NativeHordeModels.Reduced:NativeHordeModels.Impostor;
            }
        }
        private static NativeMinion HordeMinion(NativeArena arena,int id){var list=arena.Minions;for(int i=0;i<list.Count;i++)if(list[i].Id==id)return list[i];return null;}
        private static NativePilot HordePilot(NativeArena arena,int seat){var list=arena.Pilots;for(int i=0;i<list.Count;i++)if(list[i].Seat.id==seat)return list[i];return null;}
        // One replicated hit on an add (alive this frame, or killed by it).
        private void HordeHit(NativeArena arena,NativeMinion m,in ArenaEvent e)
        {
            var attacker=e.Source>0?HordePilot(arena,e.Source):null;string spec=attacker?.Seat.spec;var actor=attacker?.Pose;
            var w=NativeStaggerRules.Classify(spec,e.Detail??"direct",e.Amount);bool melee=e.Detail=="melee"||NativeHitRules.Standard(spec)&&(e.Detail=="direct"||e.Detail=="ability");
            // The authority's push (ArenaEvent.Normal), else from the attacker to the add.
            Vector3 at=m!=null?m.Body.position:NativeArena.Vec(e.Position);Vector3 dir=NativeArena.Vec(e.Normal);dir.y=0;
            if(dir.sqrMagnitude<1e-6f&&actor!=null){dir=at-NativeArena.Vec(actor.Position);dir.y=0;}
            if(dir.sqrMagnitude>1e-6f)dir.Normalize();var push=new NVector2(dir.x,dir.z);
            // The add (or, for the blow that killed it this frame, its state before HordeKindsEnd keeps it for the death).
            HordeKindState s=m!=null?HordeState(m.Id):hordeKindStates.TryGetValue(e.Target,out var gone)?gone:null;
            if(s==null&&!hordeDead.ContainsKey(e.Target))return;
            if(s!=null)
            {
                var stats=m?.Pve?.Stats;bool stagger=m!=null&&(e.Amount>=m.MaxHp*.25f||melee&&w==StaggerWeight.Heavy);
                s.React.Bind(s.Kind>=0?s.Kind:NativeHordeKinds.Resolve(stats?.id));s.React.Add(e.Time,push,w,e.Amount,melee,e.Kind==EventKind.Headshot,stagger,stats?.stagger??.45f);
                // A stagger in the wind-up cancels the blow (StepPveHorde: StaggerAt, Struck=true), so the shown one stops too.
                if(stagger&&stats!=null)
                {
                    float strike=NativeHordeKinds.Strike(stats.hit,m.Pve.Crawl,m.Pve.Lost),age=e.Time-m.LastAttack;
                    if(age>=0&&age<strike){s.CancelAt=e.Time;s.CancelFor=m.LastAttack;}
                }
                s.Push=push;
                if(melee&&actor!=null&&NativeHitRules.Standard(spec)){s.Spec=spec;s.HasBlow=true;s.BlowAt=e.Time;s.Blow=new MeleeBlow{At=e.Time,Source=e.Source,Spec=spec,CommitYaw=actor.CommitYaw,Index=actor.Combo+1,SwingAt=actor.SwingAt,SwingUntil=actor.SwingUntil,Air=!actor.Grounded,Special=actor.SpecialKind!=null&&e.Time>=actor.SpecialAt-.05f&&e.Time<=actor.SpecialUntil+.05f?actor.SpecialKind:null,Kit=e.Detail=="ability"&&actor.MeleeKit!=null&&!string.IsNullOrEmpty(actor.MeleeKit.Kind)?actor.MeleeKit.Kind:null,From=actor.Position,Yaw=actor.Yaw,Detail=e.Detail,Amount=e.Amount};}
            }
            else if(hordeDead.TryGetValue(e.Target,out var dead)&&Math.Abs(dead.At-e.Time)<.1f){dead.Push=push;dead.Melee|=melee;hordeDead[e.Target]=dead;}
            // The Siren's ink out of the wound, landing and staying as splats (directed hits within 30 m of the camera).
            if(dir.sqrMagnitude>.5f&&(!hordeEyeKnown||(at-hordeEye).sqrMagnitude<30*30))
            {
                Vector3 origin=e.Position.x==0&&e.Position.z==0?at+Vector3.up*.3f:NativeArena.Vec(e.Position);if(origin.y<at.y-.6f)origin.y=at.y+.2f;
                long key=((long)(e.Target&0xffffff)<<36)^((long)(BitConverter.SingleToInt32Bits(e.Time)&0xfffffff)<<4)^(1L<<62);bool stain=stainedWounds.Add(key);
                uint seed=NativeGoreRules.FnvDigits(e.Target)^(uint)BitConverter.SingleToInt32Bits(e.Time);bool kill=m==null;
                blood.Spray(melee?"cut":"puncture",Mathf.Clamp01(e.Amount/30f),seed,key,new NVector(origin.x,origin.y,origin.z),new NVector(-dir.x,.25f,-dir.z),new NVector(dir.x,0,dir.z),e.Time,BloodQuality*(melee?.8f:.5f),LowGraphics,stain,kill?6:0,e.Target,kill,true);
            }
        }
        /// <summary>RenderGlow: the camera the next frame's tiers rank by.</summary>
        private void HordeKindsCamera(Camera camera){if(camera==null)return;hordeEye=camera.transform.position;hordeEyeKnown=true;}

        // ================================================================================================================
        // One living or entering add (SourceWorld)
        // ================================================================================================================
        private readonly HordeMotionInput[] hordeInput=new HordeMotionInput[1];
        private CapturedAnimation.Player hordeQueueRig;private float hordeQueueSwell,hordeQueueBelly;private int hordeQueueLost,hordeQueueHidden;
        private Func<int,Matrix4x4> hordeQueueBone;private Func<int,bool> hordeQueueVisible;
        /// <summary>Poses and queues a horde add with its kind's model; false leaves it to the bank path (no kind models loaded).</summary>
        private bool HordeKindAdd(NativeArena arena,NativeMinion m,CapturedAnimation.Player rig,Vector3 pos)
        {
            var a=m.Pve;if(a==null||!HordeKindsReady)return false;var s=HordeState(m.Id);float now=arena.Time;
            int real=NativeHordeKinds.Resolve(a.Stats?.id),kind=HordeDrawKind(real,m.Id);
            if(kind!=s.Kind){s.Kind=kind;s.Look=NativeHordeKinds.Look(kind,m.Id);s.React.Bind(kind);s.Gait.Reset();}s.Real=real;
            if(s.Tier<0)s.Tier=NativeHordeModels.Reduced;
            // The authority severs limbs (PveGib): the lost part flies off as the model's own part.
            if(s.Lost>=0){int fresh=a.Lost&~s.Lost;if(fresh!=0)HordeSever(rig,s,fresh,now,pos.y-NativeHordeKinds.RootHeight(real,a.Crawl));}s.Lost=a.Lost;
            Vector3 velocity=arena.Replica?m.NetworkVelocity:m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity;float yaw=m.Yaw;
            float ground=pos.y-NativeHordeKinds.RootHeight(real,a.Crawl);
            bool entering=!a.Travelling&&now<a.ActiveAt;uint seed=NativeHordeKinds.Seed(m.Id);
            if(a.Travelling)
            {
                // Diving into a vent: it throws itself forward into the shaft on the zombie take (hidden after .9 s).
                if(now-a.DiveAt>=.9f)return true;NativeHordeGait.Dive(minionPose,kind,seed,now-a.DiveAt);s.Gait.Reset();
            }
            else if(entering&&a.Entry.kind=="vent")
            {
                // Climbing out of a vent: the body where the authority's climb has it (exit plus NativeEntryRules.Climb, turned to
                // the add's facing), clawing up to the rim, hauling over it and standing on the zombie take.
                if(now<a.SpawnAt)return true;var climb=NativeEntryRules.Climb(now,a.ActiveAt);float tau=1.95f-(a.ActiveAt-now);
                var standing=NativeArena.Vec(a.Entry.@exit.Position(.9f));pos=standing+Quaternion.Euler(0,yaw,0)*new Vector3(0,climb.y,climb.z);ground=standing.y-.9f;
                NativeHordeGait.VentClimb(minionPose,kind,seed,tau,new NVector(0,climb.y+(.9f-NativeHordeKinds.RootHeight(real,false)),climb.z));s.Gait.Reset();
            }
            else if(entering&&NativeHordeDrop.Is(a.Entry))
            {
                // The brute's jump-down (Core/NativeHordeDrop): the landing spot telegraphed, the leap, the fall and the slam.
                if(now<a.SpawnAt)return true;float height=NativeHordeDrop.HeightOf(a.Entry);var land=NativeArena.Vec(a.Entry.@exit.Position(0));
                bool shown=NativeHordeGait.Drop(minionPose,seed,now-a.SpawnAt,a.ActiveAt-a.SpawnAt,height,out float lift,out var phase);
                HordeDropJob(m.Id,kind,land,a.Entry.yaw*Mathf.Rad2Deg,height,now-a.SpawnAt,a.ActiveAt-a.SpawnAt,seed);s.Gait.Reset();
                if(!shown)return true;pos=land+Vector3.up*(NativeHordeKinds.RootHeight(real,false)+lift);ground=land.y;yaw=a.Entry.yaw*Mathf.Rad2Deg;
            }
            else
            {
                var st=a.Stats;var attack=NativeHordeKinds.AttackOf(kind,st?.attack,a.Crawl);float strike=NativeHordeKinds.Strike(st?.hit??.34f,a.Crawl,a.Lost),recover=NativeHordeKinds.Recover(st?.hit??.34f,st?.cycle??1,a.Crawl,a.Lost);
                float age=now-m.LastAttack;if(age<0||age>recover+.3f||entering)age=float.NaN;
                // A door or cell walks the add in (the authority moves it from behind the opening): the gait plants its steps on that
                // walk, the wait before it plays the zombie's waking twitch. The floor is the entry's, not the walk's raised centre.
                if(entering){if(now<a.SpawnAt)return true;ground=NativeArena.Vec(a.Entry.@exit.Position(0)).y;}
                // A blow the authority cancelled (stagger in the wind-up) fades out at once instead of landing.
                if(!float.IsNaN(age)&&s.CancelFor==m.LastAttack&&now>=s.CancelAt)age=float.NaN;
                var target=HordeTarget(arena,pos);
                ref var f=ref hordeInput[0];
                f=new HordeMotionInput{Kind=kind,Look=s.Look,Id=m.Id,Seed=NativeHordeKinds.Seed(m.Id),Time=now,Yaw=yaw,Ground=ground,Root=new NVector(pos.x,pos.y,pos.z),Velocity=new NVector(velocity.x,velocity.y,velocity.z),
                    Crawl=a.Crawl,Possessed=a.Possessed,Lost=a.Lost,AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=HordeSide(m.LastAttack,a.Lost),Health=m.MaxHp>0?m.Hp/m.MaxHp:1,
                    HasTarget=target!=null,Target=target!=null?ToN(target.Body.position):default,Entering=entering,EntryAge=now-a.SpawnAt,EntryWalk=a.WalkAt-a.SpawnAt};
                s.Gait.Pose(f,minionPose,s.React);yaw=s.Gait.Yaw;
                HordeKindJob(s,m.Id,kind,attack,age,strike,recover,pos,m.Yaw,ground,st,target,rig,arena.Time);
                if(s.Gait.Landed!=0)HordeDust(s,kind,ground,now);
            }
            NativePoseWriter.Write(rig.Bones,minionPose,"a horde rig");
            rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));rig.Root.transform.localScale=Vector3.one;
            float swell=HordeSwellNow(s,m,now);
            HordeQueue(rig,NativeHordeModels.Id(kind,s.Look,s.Tier),m.Id,a.Lost,0,swell,HordeGlowFor(m.Id,s,m,now),HitFlashCode(m.Id,now));
            bool vent=a.Entry.kind=="vent";
            HordeAdd(rig,m.Id,a.Travelling?float.NaN:(float)NativeMinionFx.VentAge(vent,a.ActiveAt,hordeClock),float.NaN,vent?NativeArena.Vec(a.Entry.@exit.Position(.9f)).y-.9f:ground,ground,a.Possessed?a.PossessedAt:float.NaN,velocity,a.Lost,true,kind*8+s.Look);
            return true;
        }
        private static NVector ToN(Vector3 v)=>new NVector(v.x,v.y,v.z);
        // The striking arm: the authority alternates it (not replicated); a hash of the attack's start alternates it here, the
        // one arm left when the other is gone.
        private static int HordeSide(float lastAttack,int lost){int side=((int)(lastAttack*7.31f)&1)==0?1:-1;if(side==1&&NativeHordeKinds.RightArmless(lost))side=-1;else if(side==-1&&NativeHordeKinds.LeftArmless(lost))side=1;return side;}
        // The pilot an add is after (nearest living within 14 m): the head and the alert stance only.
        private static NativePilot HordeTarget(NativeArena arena,Vector3 at)
        {
            NativePilot best=null;float bestD=14*14;var list=arena.Pilots;
            for(int i=0;i<list.Count;i++){var p=list[i];if(p.Hp<=0||!p.Seat.connected||p.Seat.AuthorityBoss||p.Body==null)continue;float d=(p.Body.position-at).sqrMagnitude;if(d<bestD){bestD=d;best=p;}}
            return best;
        }
        private float HordeSwellNow(HordeKindState s,NativeMinion m,float now)
        {
            var st=m.Pve?.Stats;if(st==null)return 0;var attack=NativeHordeKinds.AttackOf(s.Kind,st.attack,m.Pve.Crawl);float strike=NativeHordeKinds.Strike(st.hit,m.Pve.Crawl,m.Pve.Lost);
            float age=now-m.LastAttack;if(s.CancelFor==m.LastAttack&&now>=s.CancelAt)return 0;return NativeHordeFx.Swell(attack,age,strike);
        }
        // The add's glow this frame: the minion flicker's phase and drop bits (HordeGlow) brightened through a wind-up.
        private Vector4 HordeGlowFor(int id,HordeKindState s,NativeMinion m,float now)
        {
            var g=HordeGlow(id);var st=m.Pve?.Stats;if(st==null)return g;float strike=NativeHordeKinds.Strike(st.hit,m.Pve.Crawl,m.Pve.Lost),recover=NativeHordeKinds.Recover(st.hit,st.cycle,m.Pve.Crawl,m.Pve.Lost),age=now-m.LastAttack;
            if(s.CancelFor==m.LastAttack&&now>=s.CancelAt)age=float.NaN;float k=NativeHordeFx.Brighten(age,strike,recover)*(s.Possessed?1.15f:1);return new Vector4(k,k,k,g.w);
        }
        /// <summary>Queues a horde model on its rig: lost limbs (and `hidden` joints) left out, the spitter's sac and the bloater's
        /// belly swollen by `swell` about their joints, its glow and its struck flash. No per-call closures (cached delegates).</summary>
        private void HordeQueue(CapturedAnimation.Player rig,string model,int id,int lost,int hidden,float swell,Vector4 glow,float flash)
        {
            hordeQueueRig=rig;hordeQueueLost=lost;hordeQueueHidden=hidden;hordeQueueSwell=swell;
            hordeQueueBone??=b=>
            {
                var mtx=hordeQueueRig.Bones[b].localToWorldMatrix;float k=hordeQueueSwell;if(k<=0)return mtx;
                if(b==CapturedRig.Neck)return mtx*Matrix4x4.Translate(new Vector3(0,0,.075f))*Matrix4x4.Scale(Vector3.one*(1+.45f*k))*Matrix4x4.Translate(new Vector3(0,0,-.075f));
                if(b==CapturedRig.Spine)return mtx*Matrix4x4.Translate(new Vector3(0,.07f,.06f))*Matrix4x4.Scale(new Vector3(1+.1f*k,1+.08f*k,1+.12f*k))*Matrix4x4.Translate(new Vector3(0,-.07f,-.06f));
                return mtx;
            };
            hordeQueueVisible??=b=>HordeJointShown(hordeQueueLost,b)&&(hordeQueueHidden==0||!HordeHiddenJoint(hordeQueueHidden,b));
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,hordeQueueBone,hordeQueueVisible,glow:glow,hitFlash:flash);
        }
        // `hidden`: a mask of joint roots (bit per CapturedRig joint) whose subtrees are drawn elsewhere (a sliced part in flight).
        private static bool HordeHiddenJoint(int mask,int b){for(int j=b;j>=0;j=CapturedRig.Bones[j].Parent)if((mask&(1<<j))!=0)return true;return false;}

        // ================================================================================================================
        // Effects jobs (telegraphs, strikes) collected while posing, drawn in DevEffects within the budgets
        // ================================================================================================================
        private struct HordeJob{public int Id,Kind;public NativeHordeKinds.Attack Attack;public float Age,Strike,Recover,Yaw,PresentedYaw,Ground,Radius,Distance;public uint Seed;public Vector3 Root,HandL,HandR,Mouth,Chest,Target;public bool HasTarget;}
        private HordeJob[] hordeJobs=new HordeJob[64];private int hordeJobCount;private float[] hordeJobRank=new float[64];private int[] hordeJobOrder=new int[64];
        private void HordeKindJob(HordeKindState s,int id,int kind,NativeHordeKinds.Attack attack,float age,float strike,float recover,Vector3 root,float yaw,float ground,PveAddStats st,NativePilot target,CapturedAnimation.Player rig,float now)
        {
            if(float.IsNaN(age)||rig==null)return;
            if(hordeJobCount==hordeJobs.Length){Array.Resize(ref hordeJobs,hordeJobCount*2);Array.Resize(ref hordeJobRank,hordeJobCount*2);Array.Resize(ref hordeJobOrder,hordeJobCount*2);}
            // The bones are written after this call; the hands and mouth come from the pose's own world joints (source frame).
            var p=minionPose;p.World();var g=s.Gait;Vector3 W(int b){var l=p.WorldPosition[b];var w=g.ToWorld(l,ToN(root));return new Vector3(w.X,w.Y,w.Z);}
            hordeJobs[hordeJobCount++]=new HordeJob{Id=id,Kind=kind,Attack=attack,Age=age,Strike=strike,Recover=recover,Yaw=yaw,PresentedYaw=g.Yaw,Ground=ground,Radius=st?.smashRadius>0?st.smashRadius:NativeHordeKinds.SmashRadius,Distance=s.Distance,Seed=NativeHordeKinds.Seed(id),Root=root,
                HandL=W(CapturedRig.LHand),HandR=W(CapturedRig.RHand),Mouth=W(CapturedRig.Head),Chest=W(CapturedRig.Chest),HasTarget=target!=null,Target=target!=null?target.Body.position:root};
        }
        // Heavy footfalls: dust under the brute's and the bloater's feet, and the runner's sprint kicks.
        private struct HordeDustPuff{public Vector3 At;public float Born,Power;public uint Seed;}
        private readonly HordeDustPuff[] hordeDust=new HordeDustPuff[48];private int hordeDustNext;
        private void HordeDust(HordeKindState s,int kind,float ground,float now)
        {
            float power=kind==NativeHordeKinds.Brute?1:kind==NativeHordeKinds.Bloater?.8f:kind==NativeHordeKinds.Runner&&s.Gait.Speed>3.5f?.45f:0;if(power<=0||s.Distance>24)return;
            for(int f=0;f<4;f++)if((s.Gait.Landed&(1<<f))!=0){var w=s.Gait.FootWorld[f];hordeDust[hordeDustNext++%hordeDust.Length]=new HordeDustPuff{At=new Vector3(w.X,ground,w.Z),Born=now,Power=power,Seed=NativeHordeKinds.Seed(s.Id)^(uint)(hordeDustNext*977)};}
        }
        // Spit globs and their acid puddles (the DEV preview predicts them from the strike; Astra's minion-spit / minion-acid
        // events replace the prediction once the rules emit them).
        private struct HordeGlob{public Vector3 From,To;public float At,Flight;public uint Seed;public int Id;public float Radius,Duration;}
        private readonly HordeGlob[] hordeGlobs=new HordeGlob[16];private int hordeGlobNext;private readonly Dictionary<int,float> hordeSpat=new Dictionary<int,float>();private readonly HashSet<int> hordeSpitEvents=new HashSet<int>();

        /// <summary>DevEffects: the frame's horde telegraphs and effects into the shared effect layers, after every rig is posed.</summary>
        private void DrawHordeKindFx(NativeArena arena,Camera camera)
        {
            HordeKindsCamera(camera);if(fxDraw==null||camera==null||arena==null){hordeJobCount=0;return;}
            var draw=fxDraw;float quality=Mathf.Clamp01(EffectsQuality),now=hordeClock;Vector3 eye=camera.transform.position,right=camera.transform.right,up=camera.transform.up;bool low=quality<.5f;
            HordeSpitEvents(arena,now);HordeLocal(arena);
            for(int n=0;n<hordeJobCount;n++){hordeJobOrder[n]=n;hordeJobRank[n]=Vector3.Distance(hordeJobs[n].Root,eye);}
            Array.Sort(hordeJobRank,hordeJobOrder,0,hordeJobCount);int budget=NativeMinionFx.FullRigs;
            for(int r=0;r<hordeJobCount;r++)
            {
                var j=hordeJobs[hordeJobOrder[r]];float dist=hordeJobRank[r];double band=NativeMinionFx.Band(dist,quality);if(band<0||budget<=0)continue;budget--;bool half=band<.5;
                var seam=NativeArena.ColorOf(NativeHordeKinds.Seam[j.Kind]);float a=j.Age,H=j.Strike;float yr=j.Yaw*Mathf.Deg2Rad;Vector3 fwd=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr)),side=new Vector3(fwd.z,0,-fwd.x);
                Vector3 floor=new Vector3(j.Root.x,j.Ground+.012f,j.Root.z);
                switch(j.Attack)
                {
                    case NativeHordeKinds.Attack.Smash:
                    {
                        // The authority's own area: a 1.6 m disc 1.2 m ahead; its fill closes in to the rim at the strike.
                        Vector3 c=floor+fwd*NativeHordeKinds.SmashAhead;
                        if(NativeHordeFx.SlamTelegraph(a,H,out float rim,out float fill,out float fa)){draw.Ring(draw.Effects,c,.93f,NativeHordeKinds.SmashRadius,Alpha(seam,rim),NativeFxDraw.Authored+NativeFxDraw.Additive);if(fill>.02f)draw.Ring(draw.Effects,c+Vector3.up*.002f,0,fill,Alpha(seam,fa),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                        if(NativeHordeFx.Shockwave(a-H,out float rr,out float inner,out float al)){draw.Ring(draw.Effects,c+Vector3.up*.006f,inner,rr,Alpha(new Color(1,.85f,.7f),al),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Sprite(draw.Effects,c+Vector3.up*.25f,right,up,1.8f*(1-Mathf.Clamp01((a-H)/.3f)),Alpha(seam,al),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                        for(int i=0;i<NativeHordeFx.DebrisCount;i+=half?2:1)if(NativeHordeFx.Debris(j.Seed,i,a-H,out var o,out float sz,out float spin,out float da))draw.Cube(draw.Solid,c+new Vector3(o.X,o.Y+sz*.5f,o.Z),Quaternion.Euler(spin*57.3f,spin*40f,spin*17f),Vector3.one*sz*da,new Color(.07f,.06f,.09f),NativeFxDraw.Authored);
                        // The zombie brute's hammer: dust round the disc, cracks out of it, and the shake (a kick inside the disc).
                        if(a>=H){HordeSlamExtras(draw,c,a-H,j.Seed^0x5a3,NativeHordeKinds.SmashRadius,right,up,half||low,seam);if(a-H<.12f)HordeSmashShake(c,now-(a-H),j.Seed^(uint)j.Id,NativeHordeKinds.SmashRadius);}
                        break;
                    }
                    case NativeHordeKinds.Attack.Lunge:case NativeHordeKinds.Attack.Pounce:
                    {
                        bool low2=j.Kind==NativeHordeKinds.Crawler;for(int i=0;i<NativeHordeFx.Streaks;i+=half?2:1)if(NativeHordeFx.Streak(j.Seed,i,a,H,out float len,out float h,out float off,out float al,low2?.4f:1)){Vector3 p0=floor+side*off+Vector3.up*h;HordeStreak(draw,p0,p0-fwd*len,low2?.018f:.028f,Alpha(seam,al*(low2?.5f:1)),eye);}
                        if(a>=H-.02f&&a<H+.1f){float k=1-Mathf.Abs(a-H)/.1f;draw.Sprite(draw.Effects,j.HandL+Vector3.down*.08f,right,up,.35f*k,Alpha(seam,.8f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Sprite(draw.Effects,j.HandR+Vector3.down*.08f,right,up,.35f*k,Alpha(seam,.8f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                        break;
                    }
                    case NativeHordeKinds.Attack.Swipe:
                    {
                        // The striking claw glows through the telegraph; the claw crescent rakes across at the strike.
                        float cock=a<H?Mathf.Clamp01(a/(H*.8f)):Mathf.Max(0,1-(a-H)/.15f);var hand=HordeSide(j.Age,0)>0?j.HandR:j.HandL;
                        if(cock>0)draw.Sprite(draw.Effects,hand+Vector3.down*.08f,right,up,.3f*cock,Alpha(seam,.7f*cock),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        if(NativeHordeFx.Slash(a,H,out float sweep,out float fade)){HordeSlashMatrix(j.Root+Vector3.up*.25f,j.PresentedYaw,HordeSide(j.Age,0));draw.Crescent(draw.Effects,hordeSlash,sweep,fade,new Color(.02f,.01f,.03f),seam);}
                        break;
                    }
                    case NativeHordeKinds.Attack.Spit:
                    {
                        float sw=NativeHordeFx.Swell(j.Attack,a,H);Vector3 mouth=j.Mouth+fwd*.12f+Vector3.up*.08f;
                        if(sw>0)draw.Sprite(draw.Effects,mouth+Vector3.down*.15f,right,up,.35f+.4f*sw,Alpha(seam,.55f*sw),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        // The DEV preview predicts the glob from the strike (where it faces its pilot), once per attack.
                        if(a>=H&&!hordeSpitEvents.Contains(j.Id)&&(!hordeSpat.TryGetValue(j.Id,out float last)||last!=now-a))
                        {
                            hordeSpat[j.Id]=now-a;Vector3 to=j.HasTarget?new Vector3(j.Target.x,j.Ground,j.Target.z):floor+fwd*6;Vector3 flat=to-floor;flat.y=0;if(flat.magnitude>11)to=floor+flat.normalized*11;
                            float flight=NativeHordeFx.Flight(Vector3.Distance(new Vector3(mouth.x,0,mouth.z),new Vector3(to.x,0,to.z)));
                            hordeGlobs[hordeGlobNext++%hordeGlobs.Length]=new HordeGlob{From=mouth,To=to,At=now-a+H,Flight=flight,Seed=j.Seed^(uint)(hordeGlobNext*7919),Id=j.Id,Radius=1.3f,Duration=4};
                        }
                        break;
                    }
                    case NativeHordeKinds.Attack.Belly:
                    {
                        float sw=NativeHordeFx.Swell(j.Attack,a,H);if(sw>0)draw.Sprite(draw.Effects,j.Chest+fwd*.3f,right,up,.6f+.5f*sw,Alpha(seam,.35f*sw),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        if(NativeHordeFx.Puff(a-H,j.Radius,out float pr,out float pa))draw.Ring(draw.Effects,floor+fwd*.6f+Vector3.up*.03f,.55f,pr,Alpha(seam,pa),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        break;
                    }
                }
            }
            hordeJobCount=0;
            DrawHordeDrops(draw,now,right,up,low);
            HordeGlobsDraw(draw,now,right,up,quality);
            // Footfall dust.
            for(int i=0;i<hordeDust.Length;i++){var d=hordeDust[i];if(d.Power<=0)continue;float since=now-d.Born;if(since<0||since>.5f)continue;for(int k=0;k<NativeHordeFx.DustCount;k+=low?2:1)if(NativeHordeFx.Dust(d.Seed,k,since,d.Power,out var o,out float sz,out float al))draw.Sprite(draw.Effects,d.At+new Vector3(o.X,o.Y,o.Z),right,up,sz*2.4f,Alpha(new Color(.42f,.38f,.48f),al),NativeFxDraw.Authored);}
            HordePartsFx(draw,now,right,up);
        }
        private readonly double[] hordeSlash=new double[16],hordeSlashA=new double[16],hordeSlashB=new double[16];
        // The Siren's claw crescent (NativeSirenFx.SlashMatrix shape) at a horde add's chest, smaller: source frame (x mirrored).
        private void HordeSlashMatrix(Vector3 at,float yaw,int side)
        {
            NativeFxMath.Compose(hordeSlashA,-at.x,at.y,at.z,0,-yaw*Mathf.Deg2Rad,0,1,1,1);
            NativeFxMath.Compose(hordeSlashB,0,0,.05,.15,0,side*.6,.95*-side,1,.95);
            NativeFxMath.Multiply(hordeSlash,hordeSlashA,hordeSlashB);
        }
        // A camera-facing streak from a to b.
        private static void HordeStreak(NativeFxDraw draw,Vector3 a,Vector3 b,float width,Color color,Vector3 eye)
        {
            Vector3 d=b-a,toEye=eye-a;Vector3 s=Vector3.Cross(d,toEye).normalized*width;
            draw.Quad(draw.Effects,a-s,b-s*.2f,b+s*.2f,a+s,color,NativeFxDraw.Authored+NativeFxDraw.Additive);
        }
        // Astra's spit events (once the rules emit them): a glob from From to Position over Amount seconds; its puddle Radius.
        private int hordeSpitEvent;
        private void HordeSpitEvents(NativeArena arena,float now)
        {
            var events=arena.Events;int newest=hordeSpitEvent;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=hordeSpitEvent)continue;if(e.Id>newest)newest=e.Id;if(e.Detail!="minion-spit")continue;
                hordeSpitEvents.Add(e.Target);var from=NativeArena.Vec(e.From);var to=NativeArena.Vec(e.Position);
                hordeGlobs[hordeGlobNext++%hordeGlobs.Length]=new HordeGlob{From=from,To=to,At=e.Time,Flight=e.Amount>0?e.Amount:NativeHordeFx.Flight(Vector3.Distance(from,to)),Seed=NativeGoreRules.FnvDigits(e.Id),Id=e.Target,Radius=e.Radius>0?e.Radius:1.3f,Duration=4};
            }
            if(newest<hordeSpitEvent)hordeSpitEvents.Clear();hordeSpitEvent=newest;
        }
        private void HordeGlobsDraw(NativeFxDraw draw,float now,Vector3 right,Vector3 up,float quality)
        {
            var acid=NativeArena.ColorOf(NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);
            for(int i=0;i<hordeGlobs.Length;i++)
            {
                var g=hordeGlobs[i];if(g.Flight<=0)continue;float since=now-g.At;if(since<-.05f||since>g.Flight+g.Duration+.1f)continue;
                if(NativeHordeFx.Glob(ToN(g.From),ToN(g.To),g.Flight,since,out var at)){Vector3 p=new Vector3(at.X,at.Y,at.Z);draw.Cube(draw.Solid,p,Quaternion.Euler(since*400,since*260,0),new Vector3(.11f,.09f,.11f),new Color(.05f,.09f,.03f),NativeFxDraw.Authored);draw.Sprite(draw.Effects,p,right,up,.34f,Alpha(acid,.6f),NativeFxDraw.Authored+NativeFxDraw.Additive);continue;}
                float landed=since-g.Flight;Vector3 c=g.To+Vector3.up*.01f;
                if(NativeHordeFx.Puddle(landed,g.Duration,g.Radius,out float r,out float al)){draw.Ring(draw.Solid,c,0,r,new Color(.03f*al,.06f*al,.02f*al),NativeFxDraw.Authored);draw.Ring(draw.Effects,c+Vector3.up*.003f,.82f,r,Alpha(acid,al*.55f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    for(int k=0;k<NativeHordeFx.Bubbles;k+=quality<.5f?2:1)if(NativeHordeFx.Bubble(g.Seed,k,landed,r,out var o,out float sz,out float ba))draw.Sprite(draw.Effects,c+new Vector3(o.X,o.Y,o.Z),right,up,sz*2,Alpha(acid,ba*.7f),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                for(int k=0;k<NativeHordeFx.Splashes;k+=quality<.5f?2:1)if(NativeHordeFx.Splash(g.Seed,k,landed,out var o2,out float s2,out float sa))draw.Cube(draw.Solid,c+new Vector3(o2.X,o2.Y,o2.Z),Quaternion.identity,Vector3.one*s2,new Color(.06f,.12f,.03f),NativeFxDraw.Authored);
            }
        }

        // ================================================================================================================
        // Severed and sliced parts in flight (the model's own parts, tumbling, bouncing, burning away)
        // ================================================================================================================
        private sealed class HordePart{public bool Live;public string Model;public int Joint,Id;public uint Seed;public float At,Kick,Floor;public NVector2 Push;public readonly Matrix4x4[] Bones=new Matrix4x4[22];public Matrix4x4 Root;}
        private readonly HordePart[] hordeParts=MakeHordeParts();private int hordePartNext;
        private static HordePart[] MakeHordeParts(){var p=new HordePart[24];for(int i=0;i<p.Length;i++)p[i]=new HordePart();return p;}
        public const float HordePartSeconds=2.6f;
        private void HordeLaunch(CapturedAnimation.Player rig,string model,int id,int joint,float at,NVector2 push,float kick,float floor)
        {
            var p=hordeParts[hordePartNext++%hordeParts.Length];p.Live=true;p.Model=model;p.Joint=joint;p.Id=id;p.At=at;p.Push=push;p.Kick=kick;p.Floor=floor;p.Seed=NativeHordeKinds.Seed(id)^(uint)(joint*2654435761u);
            p.Root=rig.Root.transform.localToWorldMatrix;for(int b=0;b<22;b++)p.Bones[b]=rig.Bones[b].localToWorldMatrix;
        }
        // The authority's severed limbs (new LIMB bits on a living add): each root limb's part flies off along the newest blow.
        private void HordeSever(CapturedAnimation.Player rig,HordeKindState s,int fresh,float now,float ground)
        {
            if(rig==null||s.Kind<0)return;string model=NativeHordeModels.Id(s.Kind,s.Look,Math.Min(s.Tier<0?1:s.Tier,NativeHordeModels.Reduced));
            for(int limb=0;limb<GoreLimbBones.Length;limb++)if(NativeGoreRules.RootLimb(fresh,limb)){int joint=GoreLimbBones[limb];HordeLaunch(rig,model,s.Id,joint,now,s.Push,1,Mathf.Max(.05f,rig.Bones[joint].position.y-ground));}
        }
        private HordePart hordeDrawPart;private float hordeDrawAge;private Func<int,Matrix4x4> hordePartBone;private Func<int,bool> hordePartVisible;private Func<int,Color> hordePartTint;
        private void HordePartsFx(NativeFxDraw draw,float now,Vector3 right,Vector3 up){}
        /// <summary>SourceWorld: the parts in flight (queued with the frame's models).</summary>
        private void HordePartsDraw(float now)
        {
            hordePartBone??=b=>
            {
                var p=hordeDrawPart;var j=p.Bones[p.Joint].GetColumn(3);Vector3 joint=new Vector3(j.x,j.y,j.z);
                NativeHordeFx.Gib(p.Seed,p.Joint,hordeDrawAge,p.Push,p.Kick,p.Floor,out var off,out var axis,out float angle,NativeHordeFx.Weight(p.Joint));
                var turn=Matrix4x4.Translate(joint+new Vector3(off.X,off.Y,off.Z))*Matrix4x4.Rotate(Quaternion.AngleAxis(angle*Mathf.Rad2Deg,new Vector3(axis.X,axis.Y,axis.Z)))*Matrix4x4.Translate(-joint);
                var m=turn*p.Bones[b];double v=hordeDrawAge>1.2f?NativeHordeFx.PartVanish(hordeDrawAge):0;if(v<=0)return m;float k=Mathf.Max(.001f,1-(float)v);
                return Matrix4x4.Translate(joint+new Vector3(off.X,off.Y,off.Z))*Matrix4x4.Scale(Vector3.one*k)*Matrix4x4.Translate(-(joint+new Vector3(off.X,off.Y,off.Z)))*m;
            };
            hordePartVisible??=b=>BoneWithin(b,hordeDrawPart.Joint);
            hordePartTint??=b=>{float c=(float)NativeMinionFx.Charred(hordeDrawPart.Seed,b,hordeDrawAge),k=1-(1-(float)NativeMinionFx.CharScale)*c;return new Color(k,k,k,1-c);};
            for(int i=0;i<hordeParts.Length;i++)
            {
                var p=hordeParts[i];if(!p.Live)continue;float age=now-p.At;if(age<0||age>HordePartSeconds){p.Live=false;continue;}
                hordeDrawPart=p;hordeDrawAge=age;sourceModels.Queue(p.Model,p.Root,hordePartBone,hordePartVisible,partTint:hordePartTint,glow:HordeGlow(p.Id));
            }
        }

        // ================================================================================================================
        // Deaths (SourceMinionDeath): the kind's fall, the burst, the slice
        // ================================================================================================================
        /// <summary>Learns this frame's dead adds (their kind, look and last blow) before their state is dropped.</summary>
        private void HordeKindsEnd(NativeArena arena)
        {
            if(!HordeKindsReady)return;float now=arena.Time;
            hordeKindStale.Clear();
            foreach(var pair in hordeKindStates)
            {
                var s=pair.Value;if(s.Frame==hordeKindFrame)continue;hordeKindStale.Add(pair.Key);
                // Gone this frame: if it died, keep what its death needs.
                var deaths=arena.MinionDeaths;for(int i=deaths.Count-1;i>=0;i--)if(deaths[i].id==pair.Key&&now-deaths[i].at<.5f&&s.Kind>=0)
                {bool melee=s.HasBlow&&Math.Abs(s.BlowAt-deaths[i].at)<.06f;hordeDead[pair.Key]=new HordeDead{Kind=s.Kind,Look=s.Look,Push=s.Push,Melee=melee,Blow=s.Blow,HasBlow=melee,At=deaths[i].at,Seed=NativeHordeKinds.Seed(pair.Key)^(uint)deaths[i].seed};break;}
            }
            foreach(int id in hordeKindStale){hordeKindSpare.Push(hordeKindStates[id]);hordeKindStates.Remove(id);}
            hordeDeadStale.Clear();foreach(var pair in hordeDead){bool listed=false;var deaths=arena.MinionDeaths;for(int i=0;i<deaths.Count;i++)if(deaths[i].id==pair.Key&&now-deaths[i].at<NativeMinionFx.DeathFxSeconds+.5f){listed=true;break;}if(!listed)hordeDeadStale.Add(pair.Key);}
            foreach(int id in hordeDeadStale)hordeDead.Remove(id);
            HordePartsDraw(now);
            if(MinionGallery)HordeGallery(arena);else if(galleryRigs[0]!=null)ClearHordeGallery();
        }
        /// <summary>A horde add's death with its kind (false: an add this presentation never drew; the bank death plays).</summary>
        private bool HordeKindDeath(NativeDeath death,CapturedAnimation.Player rig,float age)
        {
            if(!HordeKindsReady||!hordeDead.TryGetValue(death.id,out var d))return false;float fx=hordeClock-death.at;
            // The killing blow's push in the rig's frame (source axes) for the fall.
            float yr=death.yaw*Mathf.Deg2Rad;Vector2 fw=new Vector2(Mathf.Sin(yr),Mathf.Cos(yr)),rt=new Vector2(fw.y,-fw.x);var push=new NVector2(-(d.Push.X*rt.x+d.Push.Y*rt.y),d.Push.X*fw.x+d.Push.Y*fw.y);
            NativeHordeGait.Death(minionPose,d.Kind,d.Seed,Mathf.Min(Mathf.Max(0,fx),(float)NativeMinionFx.DeathSeconds),push);
            NativePoseWriter.Write(rig.Bones,minionPose,"a horde rig");rig.Root.transform.SetPositionAndRotation(death.position,Quaternion.Euler(0,death.yaw,0));rig.Root.transform.localScale=Vector3.one;
            string model=NativeHordeModels.Id(d.Kind,d.Look,NativeHordeModels.Full);
            // A melee kill slices along the swing (once): the planned parts fly off as the model's own parts.
            if(!d.Planned)
            {
                d.Planned=true;d.Slice=0;
                if(d.Melee&&d.HasBlow&&NativeMeleeSlice.Plan(d.Blow,true,d.Blow.Spec,null,NativeArena.Vec(death.position),death.yaw,death.seed,hordeSlicePlan))
                {
                    var tr=hordeSlicePlan.Travel;var along=new NVector2(tr.X,tr.Z);if(along.LengthSquared()<1e-4f)along=d.Push;
                    for(int i=0;i<hordeSlicePlan.Count;i++){int j=hordeSlicePlan.Joints[i];if(!HordeJointShown(death.lost,j))continue;d.Slice|=1<<j;HordeLaunch(rig,model,death.id,j,death.at,along,hordeSlicePlan.Kick*.35f,Mathf.Max(.05f,rig.Bones[j].position.y-(death.position.y-.85f)));}
                    if(hordeSlicePlan.Crush>=0&&HordeJointShown(death.lost,hordeSlicePlan.Crush))d.Slice|=1<<hordeSlicePlan.Crush;
                }
                hordeDead[death.id]=d;
            }
            bool body=!NativeMinionFx.Gone(fx)&&!(d.Kind==NativeHordeKinds.Bloater&&fx>=NativeHordeGait.BurstAt);
            if(body)
            {
                // The lost and sliced joints left out through the cached visibility test (no closure per frame).
                hordeQueueLost=death.lost;hordeQueueHidden=d.Slice;hordeQueueVisible??=b=>HordeJointShown(hordeQueueLost,b)&&(hordeQueueHidden==0||!HordeHiddenJoint(hordeQueueHidden,b));
                HordeDeathBody(rig,model,death.id,death.lost,fx,hordeQueueVisible);
            }
            HordeAdd(rig,death.id,float.NaN,Mathf.Max(0,fx),death.position.y-.85f,death.position.y-.85f,float.NaN,Vector3.zero,death.lost,body,d.Kind*8+d.Look);
            if(d.Kind==NativeHordeKinds.Bloater)HordeBurst(death,fx);
            return true;
        }
        private struct HordeBurstJob{public Vector3 At;public float Since;public uint Seed;}
        private readonly HordeBurstJob[] hordeBursts=new HordeBurstJob[8];private int hordeBurstCount;
        private void HordeBurst(NativeDeath death,float fx){float since=fx-NativeHordeGait.BurstAt;if(since<0||since>1.5f||hordeBurstCount>=hordeBursts.Length)return;hordeBursts[hordeBurstCount++]=new HordeBurstJob{At=death.position+Vector3.up*.05f,Since=since,Seed=NativeGoreRules.FnvDigits(death.id)};}
        /// <summary>DevEffects: the bloaters' bursts (ink chunks and amber gas), then cleared.</summary>
        private void DrawHordeBursts(Camera camera)
        {
            if(fxDraw==null||camera==null){hordeBurstCount=0;return;}var draw=fxDraw;Vector3 right=camera.transform.right,up=camera.transform.up;var gas=NativeArena.ColorOf(NativeHordeKinds.Seam[NativeHordeKinds.Bloater]);bool low=EffectsQuality<.5f;
            for(int b=0;b<hordeBurstCount;b++)
            {
                var job=hordeBursts[b];
                for(int i=0;i<NativeHordeFx.BurstPieces;i+=low?2:1)if(NativeHordeFx.Burst(job.Seed,i,job.Since,out var o,out float sz,out float al,out bool isGas))
                {Vector3 p=job.At+new Vector3(o.X,o.Y,o.Z);if(isGas)draw.Sprite(draw.Effects,p,right,up,sz*2.2f,Alpha(gas,al*.8f),NativeFxDraw.Authored+NativeFxDraw.Additive);else draw.Cube(draw.Solid,p,Quaternion.Euler(job.Since*500,i*40,0),Vector3.one*sz,new Color(.04f,.03f,.05f),NativeFxDraw.Authored);}
                if(job.Since<.25f)draw.Sprite(draw.Effects,job.At+Vector3.up*.2f,right,up,2.4f*(1-job.Since/.25f),Alpha(gas,.8f*(1-job.Since/.25f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            }
            hordeBurstCount=0;
        }

        // ================================================================================================================
        // The Dev Sandbox's minion gallery (presentation only: rigs posed on a script, no authority adds)
        // ================================================================================================================
        private readonly CapturedAnimation.Player[] galleryRigs=new CapturedAnimation.Player[NativeHordeKinds.Count];
        private readonly HordeKindState[] galleryStates=new HordeKindState[NativeHordeKinds.Count];private Vector3 galleryAt;private float galleryYaw,galleryStart=float.NaN;
        /// <summary>The gallery's loop (s): the entrances (the shambler climbs out of a vent, the runner walks in through a door after
        /// its waking twitch, the brute jumps down onto its spot with the slam; the rest wake), idle, walk a circle, run, two attacks,
        /// three hits (dagger, sword, greatsword), death.</summary>
        public const float GalleryLoop=19,GalleryEntrance=3;
        private static readonly float[] GalleryHitAt={15.4f,16,16.7f},GalleryHitAmount={14,26,48};private static readonly string[] GalleryHitSpecs={"daggers","sword","greatsword"};
        private void HordeGallery(NativeArena arena)
        {
            var me=arena.Local;if(me?.Body==null)return;float now=hordeClock;
            if(float.IsNaN(galleryStart)||galleryRigs[0]==null){galleryStart=now;galleryYaw=me.Yaw;float r=galleryYaw*Mathf.Deg2Rad;galleryAt=me.Body.position+new Vector3(Mathf.Sin(r),0,Mathf.Cos(r))*6;galleryAt.y=me.Body.position.y;}
            float yawToMe=galleryYaw+180,yr=galleryYaw*Mathf.Deg2Rad;Vector3 across=new Vector3(Mathf.Cos(yr),0,-Mathf.Sin(yr));float ground=galleryAt.y-.85f;
            for(int k=0;k<NativeHordeKinds.Count;k++)
            {
                int id=990000+k;if(galleryRigs[k]==null){galleryRigs[k]=captured.Create(transform,id);galleryStates[k]=new HordeKindState();galleryStates[k].Reset(id);galleryStates[k].Kind=k;galleryStates[k].Look=0;galleryStates[k].React.Bind(k);}
                var rig=galleryRigs[k];var s=galleryStates[k];var st=NativeHordeKinds.ProposedRow(k);float t=Mathf.Repeat(now-galleryStart+k*.37f,GalleryLoop);uint seed=NativeHordeKinds.Seed(id);
                Vector3 slot=galleryAt+across*((k-(NativeHordeKinds.Count-1)*.5f)*1.9f);Vector3 pos=slot;float yaw=yawToMe;Vector3 vel=Vector3.zero;float age=float.NaN;
                var attack=NativeHordeKinds.AttackOf(k,st.attack,false);float strike=NativeHordeKinds.Strike(st.hit,false,0),recover=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);
                bool entering=false;float entryAge=0,entryWalk=0;
                // The entrances (presentation only: the same poses and effects the live adds play from their replicated entries).
                if(t<GalleryEntrance)
                {
                    if(k==NativeHordeKinds.Brute)
                    {
                        bool shown=NativeHordeGait.Drop(minionPose,seed,t,GalleryEntrance,NativeHordeDrop.DefaultHeight,out float lift,out _);HordeDropJob(id,k,new Vector3(slot.x,ground,slot.z),yaw,NativeHordeDrop.DefaultHeight,t,GalleryEntrance,seed);
                        s.Gait.Reset();if(!shown)continue;NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(slot+Vector3.up*lift,Quaternion.Euler(0,yaw,0));
                        HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,0,HordeGlow(id),1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,Vector3.zero,0,true,k*8);continue;
                    }
                    if(k==NativeHordeKinds.Shambler)
                    {
                        float tau=Mathf.Min(t,1.95f);var climb=NativeEntryRules.Climb(tau,1.95f);var at=slot+Quaternion.Euler(0,yaw,0)*new Vector3(0,climb.y,climb.z);
                        NativeHordeGait.VentClimb(minionPose,k,seed,tau,new NVector(0,climb.y,climb.z));s.Gait.Reset();NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
                        HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,0,HordeGlow(id),1);HordeAdd(rig,id,(float)NativeMinionFx.VentAge(true,1.95f,tau),float.NaN,ground,ground,float.NaN,Vector3.zero,0,true,k*8);continue;
                    }
                    // A door's walk-in: the waking twitch, then 2.3 m in 1.2 s on the gait; the others wake where they stand.
                    entering=true;entryAge=t;entryWalk=k==NativeHordeKinds.Runner?.6f:GalleryEntrance;
                    if(k==NativeHordeKinds.Runner){float fwd=yaw*Mathf.Deg2Rad;var dir=new Vector3(Mathf.Sin(fwd),0,Mathf.Cos(fwd));float u=Mathf.Clamp01((t-.6f)/1.2f);pos=slot-dir*2.3f*(1-u);if(t>=.6f&&t<1.8f)vel=dir*(2.3f/1.2f);}
                }
                float g=t-GalleryEntrance;
                // walk a circle (2-6 s), run it (6-8 s): real motion, so the feet plant
                if(g>=2&&g<8){float speed=g<6?st.walk:Mathf.Min(st.run,3.2f),rad=.75f,ang=(g-2)*speed/rad;pos=slot+new Vector3(Mathf.Cos(ang)-1,0,Mathf.Sin(ang))*rad;vel=new Vector3(-Mathf.Sin(ang),0,Mathf.Cos(ang))*speed;yaw=Mathf.Atan2(vel.x,vel.z)*Mathf.Rad2Deg;}
                else if(g>=8.4f&&g<8.4f+recover)age=g-8.4f;else if(g>=10.3f&&g<10.3f+recover)age=g-10.3f;
                // three hits from me: a dagger, a sword, a greatsword
                for(int h=0;h<3;h++){float at=galleryStart+Mathf.Floor((now-galleryStart+k*.37f)/GalleryLoop)*GalleryLoop-k*.37f+GalleryHitAt[h];if(now>=at&&s.React.Newest<at-.01f){var w=NativeStaggerRules.Classify(GalleryHitSpecs[h],"melee",GalleryHitAmount[h]);float yr2=yawToMe*Mathf.Deg2Rad;s.React.Add(at,new NVector2(-Mathf.Sin(yr2),-Mathf.Cos(yr2)),w,GalleryHitAmount[h],true,false,GalleryHitAmount[h]>=st.hp*.25f,st.stagger);}}
                if(g>=14.4f)
                {
                    // The death, then hidden until the loop starts over.
                    float d=g-14.4f;NativeHordeGait.Death(minionPose,k,seed,Mathf.Min(d,2.3f),new NVector2(0,-1));NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");
                    rig.Root.transform.SetPositionAndRotation(slot,Quaternion.Euler(0,yaw,0));bool body=!NativeMinionFx.Gone(d)&&!(k==NativeHordeKinds.Bloater&&d>=NativeHordeGait.BurstAt);
                    if(body)HordeDeathBody(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,d,b=>true);HordeAdd(rig,id,float.NaN,d,ground,ground,float.NaN,Vector3.zero,0,body,k*8);
                    if(k==NativeHordeKinds.Bloater){float since=d-NativeHordeGait.BurstAt;if(since>=0&&since<1.5f&&hordeBurstCount<hordeBursts.Length)hordeBursts[hordeBurstCount++]=new HordeBurstJob{At=slot,Since=since,Seed=NativeGoreRules.FnvDigits(id)};}
                    s.Gait.Reset();continue;
                }
                ref var f=ref hordeInput[0];
                f=new HordeMotionInput{Kind=k,Look=0,Id=id,Seed=seed,Time=now,Yaw=yaw,Ground=ground,Root=ToN(pos),Velocity=ToN(vel),AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=1,Health=1,HasTarget=!entering,Target=ToN(me.Body.position),Entering=entering,EntryAge=entryAge,EntryWalk=entryWalk};
                s.Gait.Pose(f,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);
                HordeKindJob(s,id,k,attack,age,strike,recover,pos,yaw,ground,st,me,rig,now);
                NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));
                float swell=NativeHordeFx.Swell(attack,age,strike),gk=NativeHordeFx.Brighten(age,strike,recover);var glow=HordeGlow(id);
                float flash=1;for(int h=0;h<3;h++){float at=galleryStart+Mathf.Floor((now-galleryStart+k*.37f)/GalleryLoop)*GalleryLoop-k*.37f+GalleryHitAt[h];if(now>=at&&now-at<.12f)flash=NativeHitFlash.Code(now-at,false,EffectsQuality);}
                HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,swell,new Vector4(gk,gk,gk,glow.w),flash);
                HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8);
            }
        }
        private void ClearHordeGallery(){for(int k=0;k<galleryRigs.Length;k++){galleryRigs[k]?.Dispose();galleryRigs[k]=null;galleryStates[k]=null;}galleryStart=float.NaN;}
        /// <summary>A new arena, generation or rewind: forget every add's state, the dead, the parts and the effects in flight.</summary>
        private void ClearHordeKinds(NativeArena arena)
        {
            foreach(var s in hordeKindStates.Values)hordeKindSpare.Push(s);hordeKindStates.Clear();hordeDead.Clear();hordeKindArena=arena;hordeKindGeneration=arena?.Generation??-1;hordeKindEvent=0;hordeSpitEvent=0;hordeSpitEvents.Clear();hordeSpat.Clear();
            for(int i=0;i<hordeParts.Length;i++)hordeParts[i].Live=false;for(int i=0;i<hordeGlobs.Length;i++)hordeGlobs[i].Flight=0;for(int i=0;i<hordeDust.Length;i++)hordeDust[i].Power=0;hordeJobCount=0;hordeBurstCount=0;ClearHordeZombieFx();
        }
    }

    public sealed partial class NativeAudio
    {
        // ---------- the Siren minions' wind-ups, strikes and heavy footfalls (Core/NativeHordeKinds cue keys) ----------
        // Existing bank keys only, played when the owner's LocalAssets bank has them (BankPlay; silent otherwise, as today). Timed
        // from each add's replicated LastAttack and the content's strike time, once per attack; the brute's and the bloater's steps
        // by the distance they cover. The DEV preview's substituted kinds sound as drawn.
        private readonly Dictionary<int,float> hordeStrides=new Dictionary<int,float>();private readonly Dictionary<int,Vector3> hordeLast=new Dictionary<int,Vector3>();private int hordeCueGeneration=-1;
        private void HordeCues(NativeArena arena)
        {
            if(arena?.Pve==null)return;if(arena.Generation!=hordeCueGeneration){hordeCueGeneration=arena.Generation;hordeStrides.Clear();hordeLast.Clear();}float t=arena.Time;
            foreach(var m in arena.Minions)
            {
                var a=m.Pve;if(a==null||m.Hp<=0||a.Stats==null||a.Travelling)continue;
                // The brute's jump-down lands with the slam's thud (existing bank keys: the Siren's slam over the ground impact).
                if(t<a.ActiveAt){if(NativeHordeDrop.Is(a.Entry)){float land=NativeHordeDrop.LandAt(a.ActiveAt);if(t>=land&&t-land<.12f&&Once("add"+m.Id+"d",land)){BankPlay(NativeHordeKinds.StrikeCue(NativeHordeKinds.Attack.Smash),m.Body.position,NativeHordeKinds.StrikeGain);BankPlay(NativeHordeKinds.StrikeUnder(NativeHordeKinds.Attack.Smash),m.Body.position,NativeHordeKinds.UnderGain,0,null,false,null,.7f);}}continue;}int real=NativeHordeKinds.Resolve(a.Stats.id),kind=NativeHordeKinds.Preview>=0&&NativeHordeKinds.Preview<NativeHordeKinds.Count?NativeHordeKinds.Preview:real;
                var attack=NativeHordeKinds.AttackOf(kind,a.Stats.attack,a.Crawl);float strike=NativeHordeKinds.Strike(a.Stats.hit,a.Crawl,a.Lost);Vector3 pos=m.Body.position;
                // The cue keys are only built inside a cue's window (no string per add per frame).
                string wind=NativeHordeKinds.WindupCue(attack);if(wind!=null&&t>=m.LastAttack&&t-m.LastAttack<.15f&&Once("add"+m.Id+"w",m.LastAttack))BankPlay(wind,pos,NativeHordeKinds.WindupGain);
                float at=m.LastAttack+strike;if(t>=at&&t-at<.12f&&Once("add"+m.Id+"s",m.LastAttack)){BankPlay(NativeHordeKinds.StrikeCue(attack),pos,NativeHordeKinds.StrikeGain);string under=NativeHordeKinds.StrikeUnder(attack);if(under!=null)BankPlay(under,pos,NativeHordeKinds.UnderGain,0,null,false,null,.8f);}
                string step=NativeHordeKinds.StepCue(kind);if(step==null)continue;
                if(hordeLast.TryGetValue(m.Id,out var last)){var d=pos-last;d.y=0;float moved=d.magnitude;if(moved<1){hordeStrides.TryGetValue(m.Id,out float acc);acc+=moved;float stride=NativeHordeGait.Stride(kind,Mathf.Max(.5f,moved/Mathf.Max(1e-3f,Time.unscaledDeltaTime)))*.5f;if(acc>=stride){acc-=stride;BankPlay(step,pos,NativeHordeKinds.StepGain,0,null,false,null,kind==NativeHordeKinds.Brute?.72f:.82f);}hordeStrides[m.Id]=acc;}}
                hordeLast[m.Id]=pos;
            }
        }
    }
}

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Settings · Developer's Siren minion preview (NativeHordeKinds.Preview): off, one kind for every add, or a mix by id.
        private static readonly string[] HordePreviewIds={"off","shambler","runner","brute","spitter","crawler","bloater","mix"},HordePreviewNames={"Off","Shambler","Runner","Brute","Spitter","Crawler","Bloater","Mix"};
        private static string HordePreviewId(int preview)=>preview==NativeHordeKinds.Mix?"mix":preview>=0&&preview<NativeHordeKinds.Count?NativeHordeKinds.Ids[preview]:"off";
        private static int HordePreviewChoice(string id)=>id=="mix"?NativeHordeKinds.Mix:id=="off"?-1:NativeHordeKinds.Resolve(id);
    }
}
