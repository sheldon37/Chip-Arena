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
        /// <summary>DEV preview (looks only): -1 draws each add as its own kind, 0..6 every add as that kind, Mix every add as a kind
        /// hashed from its id. The authority, its timings and hit volumes stay the add's real ones (Settings · Developer).</summary>
        public int HordePreview{get=>NativeHordeKinds.Preview;set=>NativeHordeKinds.Preview=value;}
        /// <summary>The Dev Sandbox's minion gallery: every kind in a row before the local pilot, looping its whole set (presentation only).</summary>
        public bool MinionGallery;
        private sealed class HordeKindState
        {
            public readonly NativeHordeGait Gait=new NativeHordeGait();public readonly NativeHordeReact React=new NativeHordeReact();
            public int Id,Kind=-1,Real,Look,Tier=-1,Lost,Frame;public float CancelAt=float.NaN,CancelFor=-100,Distance;public bool Possessed;
            public MeleeBlow Blow;public bool HasBlow;public float BlowAt=-100;public NVector2 Push;public string Spec;
            /// <summary>The drawn scale (the Hulk's), the jaw's opening, the roar's age (NaN none), the mouth in the world, the sac's
            /// swell when a hit cut the spit off.</summary>
            public float Scale=1,Jaw,RoarAge=float.NaN,CancelSwell;public Vector3 Mouth;public bool MouthKnown;
            public void Reset(int id){Id=id;Kind=-1;Tier=-1;Lost=-1;CancelAt=float.NaN;CancelFor=-100;HasBlow=false;BlowAt=-100;Push=NVector2.Zero;Spec=null;Gait.Reset();React.Clear();Possessed=false;Scale=1;Jaw=0;RoarAge=float.NaN;CancelSwell=0;MouthKnown=false;}
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
        private static int HordeDrawKind(int real,int id){int p=NativeHordeKinds.Preview;return p>=0&&p<NativeHordeKinds.Count?p:p==NativeHordeKinds.Mix?(int)(NativeHordeKinds.Hash(NativeHordeKinds.Seed(id),3)*NativeHordeKinds.MixCount)%NativeHordeKinds.MixCount:real;}
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
                HordeHulkEvent(e);
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
                var stats=m?.Pve?.Stats;int kind=s.Kind>=0?s.Kind:NativeHordeKinds.Resolve(stats?.id);bool heavy=melee&&w==StaggerWeight.Heavy;
                bool stagger=m!=null&&NativeHordeKinds.Staggers(kind,e.Amount,m.MaxHp,heavy);
                s.React.Bind(kind);s.React.Add(e.Time,push,w,e.Amount,melee,e.Kind==EventKind.Headshot,stagger,stats?.stagger??.45f);
                // A stagger in the wind-up cancels the blow (StepPveHorde: StaggerAt, Struck=true), so the shown one stops too; any
                // damaging hit interrupts a spitter's spit (the proposed rule): the sac deflates and the acid fizzles at its feet.
                if(m!=null&&stats!=null&&s.CancelFor!=m.LastAttack)
                {
                    var attack=NativeHordeKinds.AttackOf(kind,stats.attack,m.Pve.Crawl);float strike=NativeHordeKinds.Strike(stats,m.Pve.Crawl,m.Pve.Lost),age=e.Time-m.LastAttack;
                    if(age>=0&&age<strike&&NativeHordeKinds.Interrupts(kind,attack,e.Amount,m.MaxHp,heavy))
                    {
                        s.CancelAt=e.Time;s.CancelFor=m.LastAttack;s.CancelSwell=NativeHordeFx.Swell(attack,age,strike);
                        if(attack==NativeHordeKinds.Attack.Spit)HordeFizzle(s,m.Body.position.y-NativeHordeKinds.RootHeight(s.Real,m.Pve.Crawl),e.Time);
                    }
                }
                s.Push=push;
                if(melee&&actor!=null&&NativeHitRules.Standard(spec)){s.Spec=spec;s.HasBlow=true;s.BlowAt=e.Time;s.Blow=new MeleeBlow{At=e.Time,Source=e.Source,Spec=spec,CommitYaw=actor.CommitYaw,Index=actor.Combo+1,SwingAt=actor.SwingAt,SwingUntil=actor.SwingUntil,Air=actor.AirAttack,Special=actor.SpecialKind!=null&&e.Time>=actor.SpecialAt-.05f&&e.Time<=actor.SpecialUntil+.05f?actor.SpecialKind:null,Kit=e.Detail=="ability"&&actor.MeleeKit!=null&&!string.IsNullOrEmpty(actor.MeleeKit.Kind)?actor.MeleeKit.Kind:null,From=actor.Position,Yaw=actor.Yaw,Detail=e.Detail,Amount=e.Amount,Toward=e.Normal,Aimed=Mathf.Abs(e.Normal.x*e.Normal.x+e.Normal.y*e.Normal.y+e.Normal.z*e.Normal.z-1)<.19f};}
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
            // A big kind (the Hulk) is its rig drawn at its scale: the gait runs in the rig's metres (the world over the scale).
            float sc=NativeHordeKinds.Scale[kind];s.Scale=sc;
            // The authority severs limbs (PveGib): the lost part flies off as the model's own part.
            if(s.Lost>=0){int fresh=a.Lost&~s.Lost;if(fresh!=0)HordeSever(rig,s,fresh,now,pos.y-NativeHordeKinds.RootHeight(real,a.Crawl));}s.Lost=a.Lost;
            Vector3 velocity=arena.Replica?m.NetworkVelocity:m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity;float yaw=m.Yaw;
            float ground=pos.y-NativeHordeKinds.RootHeight(real,a.Crawl);
            bool entering=!a.Travelling&&now<a.ActiveAt;uint seed=NativeHordeKinds.Seed(m.Id);
            var st0=a.Stats;var attack0=NativeHordeKinds.AttackOf(kind,st0?.attack,a.Crawl);float strike0=NativeHordeKinds.Strike(st0,a.Crawl,a.Lost),recover0=NativeHordeKinds.Recover(st0,a.Crawl,a.Lost);
            float roar=float.NaN,jawAge=float.NaN;
            if(a.Travelling)
            {
                // Diving into a vent: it throws itself forward into the shaft on the zombie take (hidden after .9 s).
                if(now-a.DiveAt>=.9f)return true;NativeHordeGait.Dive(minionPose,kind,seed,now-a.DiveAt);s.Gait.Reset();
            }
            else if(entering&&a.Entry.kind=="vent"&&a.Entry.surface!="ceiling"&&a.Entry.surface!="wall")
            {
                // Climbing out of a vent: the body where the authority's climb has it (exit plus NativeEntryRules.Climb, turned to
                // the add's facing), clawing up to the rim, hauling over it and standing on the zombie take.
                if(now<a.SpawnAt)return true;var climb=NativeEntryRules.Climb(now,a.ActiveAt);float tau=1.95f-(a.ActiveAt-now);
                var standing=NativeArena.Vec(a.Entry.@exit.Position(.9f));pos=standing+Quaternion.Euler(0,yaw,0)*new Vector3(0,climb.y,climb.z);ground=standing.y-.9f;
                NativeHordeGait.VentClimb(minionPose,kind,seed,tau,new NVector(0,climb.y+(.9f-NativeHordeKinds.RootHeight(real,false)),climb.z)/sc);s.Gait.Reset();
            }
            else if(entering&&(NativeHordeDrop.Is(a.Entry)||a.Entry.surface=="ceiling"))
            {
                // The heavy kinds' jump-down (Core/NativeHordeDrop, on the kind's profile): the spot telegraphed, the leap, the fall, the slam.
                if(now<a.SpawnAt)return true;var profile=NativeHordeDrop.Profile(kind);float height=NativeHordeDrop.HeightOf(a.Entry,profile);var land=NativeArena.Vec(a.Entry.@exit.Position(0));
                bool shown=NativeHordeGait.Drop(minionPose,seed,now-a.SpawnAt,a.ActiveAt-a.SpawnAt,height,profile,out float lift,out var phase);
                HordeDropJob(m.Id,kind,land,a.Entry.yaw*Mathf.Rad2Deg,height,now-a.SpawnAt,a.ActiveAt-a.SpawnAt,seed);s.Gait.Reset();
                if(!shown)return true;pos=land+Vector3.up*(NativeHordeKinds.DrawRoot(kind,false)+lift);ground=land.y;yaw=a.Entry.yaw*Mathf.Rad2Deg;
            }
            else
            {
                var st=st0;var attack=attack0;float strike=strike0,recover=recover0;
                // A heavy add's reported move (a punch or a swipe: Runtime/NativeHordeHulkMotion; the content's attack until reported).
                var move=HordeHulkAttack(m,a.Crawl,a.Lost,ref attack,ref strike,ref recover);
                float age=now-m.LastAttack;if(age<0||age>recover+.3f||entering)age=float.NaN;
                bool breach=entering&&NativeBreach.Is(a.Entry),knocking=false;float breakAt=breach?NativeBreach.BreakAt(a.ActiveAt):float.NaN;
                // A door or cell walks the add in (the authority moves it from behind the opening): the gait plants its steps on that
                // walk, the wait before it plays the zombie's waking twitch. The floor is the entry's, not the walk's raised centre.
                if(entering){if(now<a.SpawnAt)return true;ground=NativeArena.Vec(a.Entry.@exit.Position(0)).y;}
                if(breach)
                {
                    // A breach (Core/NativeBreach): behind the wall it pounds it on the smash take (each blow's impact on a knock),
                    // then charges through the hole it breaks; the wall itself is the breach's own draw (HordeBreachJob).
                    HordeBreachJob(arena,a.Entry,a.SpawnAt,breakAt,NativeArena.Vec(a.Entry.from.Position(0)),NativeArena.Vec(a.Entry.@exit.Position(0)),kind,seed);
                    if(now<breakAt){attack=NativeHordeKinds.Attack.Smash;strike=.42f;recover=.6f;age=NativeBreach.KnockAge(now-a.SpawnAt,strike);}
                }
                // A Hulk breaking a wall block it cannot fit past (the replicated dynamic breach, by its actor): it stands at the wall
                // and pounds it on the same take and knocks. The blows land on the wall (the breach's draw), so no slam job is made.
                else if(!entering&&HordeWallKnock(m.Id,now,out float knockAt)){attack=NativeHordeKinds.Attack.Smash;strike=.42f;recover=.6f;age=NativeBreach.KnockAge(now-knockAt,strike);knocking=true;}
                if(a.Stats.id=="spitter"&&a.SpitCancelAt>=m.LastAttack&&a.SpitCancelAt<m.LastAttack+NativeSpitterRules.Hit&&s.CancelFor!=m.LastAttack)
                {s.CancelAt=a.SpitCancelAt;s.CancelFor=m.LastAttack;s.CancelSwell=NativeHordeFx.Swell(attack,a.SpitCancelAt-m.LastAttack,strike);HordeFizzle(s,ground,a.SpitCancelAt);}
                // A blow the authority cancelled (stagger in the wind-up) fades out at once instead of landing.
                jawAge=age;
                if(!float.IsNaN(age)&&s.CancelFor==m.LastAttack&&now>=s.CancelAt)age=float.NaN;
                if(!entering)roar=HordeHulkBellow(m.Id,now,NativeHordeRoar.Age(kind,seed,now,a.ActiveAt,age,recover));
                var target=HordeTarget(arena,pos);
                ref var f=ref hordeInput[0];float inv=1/sc;
                f=new HordeMotionInput{Kind=kind,Look=s.Look,Id=m.Id,Seed=NativeHordeKinds.Seed(m.Id),Time=now,Yaw=yaw,Ground=ground*inv,Root=new NVector(pos.x,ground+NativeHordeKinds.DrawRoot(kind,a.Crawl),pos.z)*inv,Velocity=new NVector(velocity.x,velocity.y,velocity.z)*inv,
                    Crawl=a.Crawl,Possessed=a.Possessed,Lost=a.Lost,AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=HordeSide(m.LastAttack,a.Lost),Health=m.MaxHp>0?m.Hp/m.MaxHp:1,
                    HasTarget=target!=null,Target=target!=null?ToN(target.Body.position)*inv:default,Entering=entering,EntryAge=now-a.SpawnAt,EntryWalk=(breach?breakAt:a.WalkAt)-a.SpawnAt,Roaring=!float.IsNaN(roar),RoarAge=roar,Move=move};
                HordeHulkInput(ref f,m,now,entering,breach);
                s.Gait.Pose(f,minionPose,s.React);yaw=s.Gait.Yaw;pos.y=ground+NativeHordeKinds.DrawRoot(kind,a.Crawl);HordeHulkPosed(s,now);
                HordeKindJob(s,m.Id,kind,attack,knocking?float.NaN:age,strike,recover,pos,m.Yaw,ground,st,target,rig,arena.Time,f.Side);
                if(s.Gait.Landed!=0)HordeDust(s,kind,ground,now);
                attack0=attack;strike0=strike;
            }
            // The hinged jaw (the spitter's unhinging spit, the Hulk's roar) and the mouth the spit leaves from.
            s.RoarAge=roar;float cancelAge=s.CancelFor==m.LastAttack&&!float.IsNaN(s.CancelAt)?now-s.CancelAt:float.NaN;
            s.Jaw=NativeHordeJaw.Open(kind,attack0,jawAge,strike0,cancelAge,roar,now,seed);HordeMouth(s,pos);
            NativePoseWriter.Write(rig.Bones,minionPose,"a horde rig");
            rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            float swell=HordeSwellNow(s,m,now);var glow=HordeGlowFor(m.Id,s,m,now);float flash=HitFlashCode(m.Id,now);
            HordeQueue(rig,NativeHordeModels.Id(kind,s.Look,s.Tier),m.Id,a.Lost,0,swell,glow,flash);HordeQueueJaw(rig,kind,s.Look,s.Tier,s.Jaw,glow,flash);
            bool vent=a.Entry.kind=="vent";
            HordeAdd(rig,m.Id,a.Travelling?float.NaN:(float)NativeMinionFx.VentAge(vent,a.ActiveAt,hordeClock),float.NaN,vent?NativeArena.Vec(a.Entry.@exit.Position(.9f)).y-.9f:ground,ground,a.Possessed?a.PossessedAt:float.NaN,velocity,a.Lost,true,kind*8+s.Look);
            return true;
        }
        // The mouth socket in the world (the head joint's frame times NativeHordeModels.MouthSocket, at the add's scale).
        private void HordeMouth(HordeKindState s,Vector3 root)
        {
            var p=minionPose;p.World();var local=NativeHordeModels.MouthSocket(s.Kind);var head=p.WorldPosition[CapturedRig.Head]+System.Numerics.Vector3.Transform(new NVector(-local.X,local.Y,local.Z),p.WorldRotation[CapturedRig.Head]);
            var w=s.Gait.ToWorld(head*s.Scale,ToN(root));s.Mouth=new Vector3(w.X,w.Y,w.Z);s.MouthKnown=true;
        }
        /// <summary>A hinged kind's jaw, turned about its hinge on the head joint (the model's own "-jaw" model; none on the impostor tier).</summary>
        private Func<int,Matrix4x4> hordeJawBone;private float hordeJawOpen;private int hordeJawKind;
        private void HordeQueueJaw(CapturedAnimation.Player rig,int kind,int look,int tier,float open,Vector4 glow,float flash)
        {
            if(!NativeHordeJaw.Hinged(kind)||tier==NativeHordeModels.Impostor||!HordeJointShown(hordeQueueLost,CapturedRig.Head))return;string id=NativeHordeModels.JawId(kind,look,tier);if(!sourceModels.Has(id))return;
            hordeQueueRig=rig;hordeJawOpen=open;hordeJawKind=kind;
            hordeJawBone??=b=>{var mtx=hordeQueueRig.Bones[b].localToWorldMatrix;if(b!=CapturedRig.Head)return mtx;var h=NativeHordeModels.JawHinge(hordeJawKind);var hinge=new Vector3(h.X,h.Y,h.Z);
                return mtx*Matrix4x4.Translate(hinge)*Matrix4x4.Rotate(Quaternion.Euler(hordeJawOpen*Mathf.Rad2Deg,0,0))*Matrix4x4.Translate(-hinge);};
            sourceModels.Queue(id,rig.Root.transform.localToWorldMatrix,hordeJawBone,null,glow:glow,hitFlash:flash);
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
            var st=m.Pve?.Stats;if(st==null)return 0;var attack=NativeHordeKinds.AttackOf(s.Kind,st.attack,m.Pve.Crawl);float strike=NativeHordeKinds.Strike(st,m.Pve.Crawl,m.Pve.Lost);
            float age=now-m.LastAttack;if(s.CancelFor==m.LastAttack&&now>=s.CancelAt)return s.CancelSwell*(1-Mathf.SmoothStep(0,1,(now-s.CancelAt)/.35f));return NativeHordeFx.Swell(attack,age,strike);
        }
        // The add's glow this frame: the minion flicker's phase and drop bits (HordeGlow) brightened through a wind-up.
        private Vector4 HordeGlowFor(int id,HordeKindState s,NativeMinion m,float now)
        {
            var g=HordeGlow(id);var st=m.Pve?.Stats;if(st==null)return g;float strike=NativeHordeKinds.Strike(st,m.Pve.Crawl,m.Pve.Lost),recover=NativeHordeKinds.Recover(st,m.Pve.Crawl,m.Pve.Lost),age=now-m.LastAttack;
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
        private struct HordeJob{public int Id,Kind,Side;public NativeHordeKinds.Attack Attack;public float Age,Strike,Recover,Yaw,PresentedYaw,Ground,Radius,Distance,Scale;public uint Seed;public Vector3 Root,HandL,HandR,Mouth,Neck,Chest,Target;public bool HasTarget;}
        private HordeJob[] hordeJobs=new HordeJob[64];private int hordeJobCount;private float[] hordeJobRank=new float[64];private int[] hordeJobOrder=new int[64];
        private void HordeKindJob(HordeKindState s,int id,int kind,NativeHordeKinds.Attack attack,float age,float strike,float recover,Vector3 root,float yaw,float ground,PveAddStats st,NativePilot target,CapturedAnimation.Player rig,float now,int side=1)
        {
            if(float.IsNaN(age)||rig==null)return;
            if(hordeJobCount==hordeJobs.Length){Array.Resize(ref hordeJobs,hordeJobCount*2);Array.Resize(ref hordeJobRank,hordeJobCount*2);Array.Resize(ref hordeJobOrder,hordeJobCount*2);}
            // The bones are written after this call; the hands and mouth come from the pose's own world joints (source frame).
            var p=minionPose;p.World();var g=s.Gait;float sc=s.Scale;Vector3 W(int b){var l=p.WorldPosition[b];var w=g.ToWorld(l*sc,ToN(root));return new Vector3(w.X,w.Y,w.Z);}
            float radius=kind==NativeHordeKinds.Hulk?NativeHordeKinds.HulkSmashRadius:st?.smashRadius>0?st.smashRadius:NativeHordeKinds.SmashRadius;
            HordeMouth(s,root);
            hordeJobs[hordeJobCount++]=new HordeJob{Id=id,Kind=kind,Side=side,Attack=attack,Age=age,Strike=strike,Recover=recover,Yaw=yaw,PresentedYaw=g.Yaw,Ground=ground,Radius=radius,Distance=s.Distance,Seed=NativeHordeKinds.Seed(id),Root=root,
                HandL=W(CapturedRig.LHand),HandR=W(CapturedRig.RHand),Mouth=s.Mouth,Neck=W(CapturedRig.Neck),Chest=W(CapturedRig.Chest),HasTarget=target!=null,Target=target!=null?target.Body.position:root,Scale=sc};
        }
        // Heavy footfalls: dust under the brute's and the bloater's feet, and the runner's sprint kicks.
        private struct HordeDustPuff{public Vector3 At;public float Born,Power;public uint Seed;}
        private readonly HordeDustPuff[] hordeDust=new HordeDustPuff[48];private int hordeDustNext;
        private void HordeDust(HordeKindState s,int kind,float ground,float now)
        {
            float power=kind==NativeHordeKinds.Hulk?1.9f:kind==NativeHordeKinds.Brute?1:kind==NativeHordeKinds.Bloater?.8f:kind==NativeHordeKinds.Runner&&s.Gait.Speed>3.5f?.45f:0;if(power<=0||s.Distance>(kind==NativeHordeKinds.Hulk?40:24))return;
            for(int f=0;f<4;f++)if((s.Gait.Landed&(1<<f))!=0)
            {
                // (The gait's feet are in the rig's metres: a big kind's are its scale times further out.)
                var w=s.Gait.FootWorld[f]*s.Scale;var at=new Vector3(w.X,ground,w.Z);uint seed=NativeHordeKinds.Seed(s.Id)^(uint)(hordeDustNext*977);
                hordeDust[hordeDustNext++%hordeDust.Length]=new HordeDustPuff{At=at,Born=now,Power=power,Seed=seed};
                // The Hulk's footfalls shake the ground and crack the floor.
                if(kind==NativeHordeKinds.Hulk)HordeStomp(at,now,seed);
            }
        }

        /// <summary>DevEffects: the frame's horde telegraphs and effects into the shared effect layers, after every rig is posed.</summary>
        private void DrawHordeKindFx(NativeArena arena,Camera camera)
        {
            HordeKindsCamera(camera);if(fxDraw==null||camera==null||arena==null){hordeJobCount=0;HordeChargesSkip();return;}
            var draw=fxDraw;float quality=Mathf.Clamp01(EffectsQuality),now=hordeClock;Vector3 eye=camera.transform.position,right=camera.transform.right,up=camera.transform.up;bool low=quality<.5f;
            // The acid and the impacts' high-quality pass (NativeZombieFxDraw: the shared effects renderer and its decal layer).
            zombieFx??=new NativeZombieFxDraw();var zfx=zombieFx;zfx.Begin(vfx,eye,right,up,NativeZombieFx.Tier(LowGraphics,quality));
            HordeSpitEvents(arena,now);HordeLocal(arena);HordeChargesFx(draw,arena,now,eye,right,up,low);
            for(int n=0;n<hordeJobCount;n++){hordeJobOrder[n]=n;hordeJobRank[n]=Vector3.Distance(hordeJobs[n].Root,eye);}
            Array.Sort(hordeJobRank,hordeJobOrder,0,hordeJobCount);int budget=NativeMinionFx.FullRigs;
            for(int r=0;r<hordeJobCount;r++)
            {
                var j=hordeJobs[hordeJobOrder[r]];float dist=hordeJobRank[r];double band=NativeMinionFx.Band(dist,quality);if(band<0||budget<=0)continue;budget--;bool half=band<.5;zfx.Within(dist,j.Kind==NativeHordeKinds.Hulk);
                var seam=NativeArena.ColorOf(NativeHordeKinds.Seam[j.Kind]);float a=j.Age,H=j.Strike;float yr=j.Yaw*Mathf.Deg2Rad;Vector3 fwd=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr)),side=new Vector3(fwd.z,0,-fwd.x);
                Vector3 floor=new Vector3(j.Root.x,j.Ground+.012f,j.Root.z);
                switch(j.Attack)
                {
                    case NativeHordeKinds.Attack.Smash:
                    {
                        // The authority's own area (the brute's 1.6 m disc 1.2 m ahead; the Hulk's proposed 2.8 m, 2 m ahead); its
                        // fill closes in to the rim at the strike.
                        bool hulk=j.Kind==NativeHordeKinds.Hulk;float R=hulk?j.Radius:NativeHordeKinds.SmashRadius;Vector3 c=floor+fwd*NativeHordeKinds.SmashAheadOf(j.Kind);
                        if(NativeHordeFx.SlamTelegraph(a,H,R,out float rim,out float fill,out float fa)){draw.Ring(draw.Effects,c,R-.11f,R,Alpha(seam,rim),NativeFxDraw.Authored+NativeFxDraw.Additive);if(fill>.02f)draw.Ring(draw.Effects,c+Vector3.up*.002f,0,fill,Alpha(seam,fa),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                        // The strike (at LastAttack + the authority's hit, the take's impact mark): one impact on the authority's disc,
                        // held where it struck (NativeZombieFx.Smash: the upright wave to the disc's rim, cracks, floor-hugging dust, lit
                        // rubble bouncing to rest inside it, sparks), the shake, and the Hulk's shove for the pilot it hits.
                        if(a>=H)
                        {
                            HordeImpactAt(j.Id,c,now-(a-H),j.Seed^0x5a3,NativeZombieFx.Smash(j.Kind),NativeHordeKinds.Seam[j.Kind]);
                            if(a-H<.12f)HordeSmashShake(c,now-(a-H),j.Seed^(uint)j.Id,R,hulk);if(hulk)HordeShove(draw,arena,c,R,a-H,fwd,right,up);
                        }
                        break;
                    }
                    case NativeHordeKinds.Attack.Lunge:case NativeHordeKinds.Attack.Pounce:
                    {
                        // A heavy kind's reported straight punch (Runtime/NativeHordeHulkCharge.cs: its trail, burst, the struck pilot).
                        if(NativeHulkMoves.Heavy(j.Kind)){HordeHulkBlowFx(draw,arena,j,a,H,fwd,side,eye,right,up,half,now);break;}
                        bool low2=j.Kind==NativeHordeKinds.Crawler;for(int i=0;i<NativeHordeFx.Streaks;i+=half?2:1)if(NativeHordeFx.Streak(j.Seed,i,a,H,out float len,out float h,out float off,out float al,low2?.4f:1)){Vector3 p0=floor+side*off+Vector3.up*h;HordeStreak(draw,p0,p0-fwd*len,low2?.018f:.028f,Alpha(seam,al*(low2?.5f:1)),eye);}
                        if(a>=H-.02f&&a<H+.1f){float k=1-Mathf.Abs(a-H)/.1f;draw.Sprite(draw.Effects,j.HandL+Vector3.down*.08f,right,up,.35f*k,Alpha(seam,.8f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Sprite(draw.Effects,j.HandR+Vector3.down*.08f,right,up,.35f*k,Alpha(seam,.8f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                        break;
                    }
                    case NativeHordeKinds.Attack.Swipe:
                    {
                        // A heavy kind's reported swipe (its arm's trail, its crescent at the body's scale, the burst, the struck pilot).
                        if(NativeHulkMoves.Heavy(j.Kind)){HordeHulkBlowFx(draw,arena,j,a,H,fwd,side,eye,right,up,half,now);break;}
                        // The striking claw glows through the telegraph; the claw crescent rakes across at the strike.
                        float cock=a<H?Mathf.Clamp01(a/(H*.8f)):Mathf.Max(0,1-(a-H)/.15f);var hand=HordeSide(j.Age,0)>0?j.HandR:j.HandL;
                        if(cock>0)draw.Sprite(draw.Effects,hand+Vector3.down*.08f,right,up,.3f*cock,Alpha(seam,.7f*cock),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        if(NativeHordeFx.Slash(a,H,out float sweep,out float fade)){HordeSlashMatrix(j.Root+Vector3.up*.25f,j.PresentedYaw,HordeSide(j.Age,0));draw.Crescent(draw.Effects,hordeSlash,sweep,fade,new Color(.02f,.01f,.03f),seam);}
                        break;
                    }
                    case NativeHordeKinds.Attack.Spit:
                    {
                        // The wind-up: the throat and sac swell and glow acid green under the unhinging jaw, drool runs off it.
                        float sw=NativeHordeFx.Swell(j.Attack,a,H);Vector3 mouth=j.Mouth,throat=Vector3.Lerp(j.Neck,j.Mouth,.45f)+Vector3.down*.04f;
                        if(sw>0)draw.Sprite(draw.Effects,throat,right,up,.24f+.3f*sw,Alpha(seam,.35f*sw),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        zfx.Windup(mouth,throat,fwd,Vector3.up,a,H,NativeHordeJaw.Open(j.Kind,j.Attack,a,H,float.NaN,float.NaN,now,j.Seed),sw,j.Seed);
                        // The glob leaves the mouth at the strike (the DEV preview predicts where it lands, facing its pilot, once per
                        // attack; Astra's minion-spit events replace the prediction).
                        if(arena?.Pve==null&&a>=H&&!hordeSpitEvents.Contains(j.Id)&&(!hordeSpat.TryGetValue(j.Id,out float last)||last!=now-a))
                        {
                            hordeSpat[j.Id]=now-a;Vector3 to=j.HasTarget?new Vector3(j.Target.x,j.Ground,j.Target.z):floor+fwd*6;Vector3 flat=to-floor;flat.y=0;if(flat.magnitude>NativeSpitterRules.Range)to=floor+flat.normalized*NativeSpitterRules.Range;
                            float flight=NativeHordeFx.Flight(Vector3.Distance(new Vector3(mouth.x,0,mouth.z),new Vector3(to.x,0,to.z)));
                            HordeLaunchGlob(mouth,to,now-a+H,flight,j.Seed^(uint)(hordeGlobNext*7919),j.Id,NativeSpitterRules.Radius,NativeSpitterRules.Duration,0);
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
            HordeAcidDraw(draw,arena,now,eye,right,up,quality);DrawHordeStomps(draw,now,low);DrawHordeBreachFx(draw,now,eye,right,up,low);
            // Footfall dust (toon puffs kicked round the foot).
            for(int i=0;i<hordeDust.Length;i++){var d=hordeDust[i];if(d.Power<=0)continue;float since=now-d.Born;if(since<0||since>.5f)continue;if(zfx.Within(Vector3.Distance(eye,d.At),d.Power>1.5f))zfx.Footfall(d.At,since,d.Power,d.Seed);}
            HordePartsFx(draw,now,right,up);zfx.Submit(camera,now);
        }
        private readonly double[] hordeSlash=new double[16],hordeSlashA=new double[16],hordeSlashB=new double[16];
        // The Siren's claw crescent (NativeSirenFx.SlashMatrix shape) at a horde add's chest, smaller: source frame (x mirrored).
        private void HordeSlashMatrix(Vector3 at,float yaw,int side,float scale=1)
        {
            NativeFxMath.Compose(hordeSlashA,-at.x,at.y,at.z,0,-yaw*Mathf.Deg2Rad,0,scale,scale,scale);
            NativeFxMath.Compose(hordeSlashB,0,0,.05,.15,0,side*.6,.95*-side,1,.95);
            NativeFxMath.Multiply(hordeSlash,hordeSlashA,hordeSlashB);
        }
        // A camera-facing streak from a to b.
        private static void HordeStreak(NativeFxDraw draw,Vector3 a,Vector3 b,float width,Color color,Vector3 eye)
        {
            Vector3 d=b-a,toEye=eye-a;Vector3 s=Vector3.Cross(d,toEye).normalized*width;
            draw.Quad(draw.Effects,a-s,b-s*.2f,b+s*.2f,a+s,color,NativeFxDraw.Authored+NativeFxDraw.Additive);
        }
        // The authority's acid records, on every peer (host, guests, late joins and the killcam's replay world): PvE's
        // NativePveFrame.acid and, on any other map, the DEV monsters' devAcid (both read through NativeArena.PveAcidAt). The
        // spit's Impact event is a transient notification the event buffer may drop in a busy tick, so it only tells the DEV
        // preview which adds the authority spits for; every glob, splash and pool comes from a record (seed: the record's id), so
        // PvE and DEV monsters on other maps draw the same acid.
        private int hordeSpitEvent;
        private void HordeSpitEvents(NativeArena arena,float now)
        {
            int recordCount=arena.PveAcidCount;
            for(int i=0;i<hordeGlobs.Length;i++){if(!hordeGlobs[i].Record)continue;bool found=false;for(int k=0;k<recordCount;k++){var r=arena.PveAcidAt(k);if(hordeGlobs[i].Id==r.actor&&hordeGlobs[i].At==r.at){found=true;break;}}
                // A record gone before its pool ended (a phase hold, a reset, cleared DEV monsters) takes its acid with it; one that ended on its own leaves its etch.
                if(!found&&now<hordeGlobs[i].At+hordeGlobs[i].Flight+hordeGlobs[i].Duration-.05f)hordeGlobs[i].Flight=0;}
            for(int k=0;k<recordCount;k++)
            {
                var r=arena.PveAcidAt(k);hordeSpitEvents.Add(r.actor);bool found=false;for(int i=0;i<hordeGlobs.Length;i++)if(hordeGlobs[i].Flight>0&&hordeGlobs[i].Id==r.actor&&hordeGlobs[i].At==r.at){found=true;break;}
                if(!found)HordeLaunchGlob(NativeArena.Vec(r.from),NativeArena.Vec(r.to),r.at,r.flight,(uint)r.id,r.actor,NativeSpitterRules.Radius,NativeSpitterRules.Duration,record:true);
            }
            if(arena.Pve!=null)return;
            var events=arena.Events;int newest=hordeSpitEvent;
            for(int i=0;i<events.Count;i++){var e=events[i];if(e.Id<=hordeSpitEvent)continue;if(e.Id>newest)newest=e.Id;if(e.Detail=="minion-spit")hordeSpitEvents.Add(e.Target);}
            if(newest<hordeSpitEvent)hordeSpitEvents.Clear();hordeSpitEvent=newest;
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
            HordeBreachFrames(arena);HordeBreachesDraw(now);HordeJoltsDraw(now);HordeTremblesDraw(now);
        }
        /// <summary>A horde add's death with its kind (false: an add this presentation never drew; the bank death plays).</summary>
        private bool HordeKindDeath(NativeDeath death,CapturedAnimation.Player rig,float age)
        {
            if(!HordeKindsReady||!hordeDead.TryGetValue(death.id,out var d))return false;float fx=hordeClock-death.at;
            // The killing blow's push in the rig's frame (source axes) for the fall.
            float yr=death.yaw*Mathf.Deg2Rad;Vector2 fw=new Vector2(Mathf.Sin(yr),Mathf.Cos(yr)),rt=new Vector2(fw.y,-fw.x);var push=new NVector2(-(d.Push.X*rt.x+d.Push.Y*rt.y),d.Push.X*fw.x+d.Push.Y*fw.y);
            NativeHordeGait.Death(minionPose,d.Kind,d.Seed,Mathf.Min(Mathf.Max(0,fx),(float)NativeMinionFx.DeathSeconds),push);
            float sc=NativeHordeKinds.Scale[d.Kind],floorY=death.position.y-NativeHordeKinds.RootHeight(d.Kind,false);
            NativePoseWriter.Write(rig.Bones,minionPose,"a horde rig");rig.Root.transform.SetPositionAndRotation(new Vector3(death.position.x,floorY+NativeHordeKinds.DrawRoot(d.Kind,false),death.position.z),Quaternion.Euler(0,death.yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            // The Hulk crashes down: a thud of dust, cracks and a shake where its chest hits the floor.
            if(d.Kind==NativeHordeKinds.Hulk&&fx>=NativeHordeGait.DownAt&&fx<NativeHordeGait.DownAt+.5f){var chest=rig.Bones[CapturedRig.Chest].position;HordeCrash(death.id,new Vector3(chest.x,floorY,chest.z),death.at+NativeHordeGait.DownAt,d.Seed);}
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
                HordeDeathBody(rig,model,death.id,death.lost,fx,hordeQueueVisible);HordeDeathJaw(rig,d.Kind,d.Look,death.id,hordeQueueVisible);
            }
            HordeAdd(rig,death.id,float.NaN,Mathf.Max(0,fx),floorY,floorY,float.NaN,Vector3.zero,death.lost,body,d.Kind*8+d.Look);
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
            HordeGalleryHulk(me,now,across,yawToMe,ground);HordeGalleryBreach(arena,me,now,yawToMe,ground);HordeGalleryHulkMoves(me,now,across,yawToMe,ground);HordeGalleryHulkCharge(arena,me,now,across,yawToMe,ground);
            for(int k=0;k<NativeHordeKinds.Count;k++)
            {
                if(k==NativeHordeKinds.Hulk)continue;
                int id=990000+k;if(galleryRigs[k]==null){galleryRigs[k]=captured.Create(transform,id);galleryStates[k]=new HordeKindState();galleryStates[k].Reset(id);galleryStates[k].Kind=k;galleryStates[k].Look=0;galleryStates[k].React.Bind(k);}
                var rig=galleryRigs[k];var s=galleryStates[k];var st=NativeHordeKinds.ProposedRow(k);float t=Mathf.Repeat(now-galleryStart+k*.37f,GalleryLoop);uint seed=NativeHordeKinds.Seed(id);
                Vector3 slot=galleryAt+across*((k-(NativeHordeKinds.Hulk-1)*.5f)*1.9f);Vector3 pos=slot;float yaw=yawToMe;Vector3 vel=Vector3.zero;float age=float.NaN;
                var attack=NativeHordeKinds.AttackOf(k,st.attack,false);float strike=NativeHordeKinds.Strike(st,false,0),recover=NativeHordeKinds.Recover(st,false,0);
                bool entering=false;float entryAge=0,entryWalk=0,jawAge=float.NaN,cancelAge=float.NaN;
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
                else if(k==NativeHordeKinds.Spitter)
                {
                    // The spitter: a whole spit, then one a dagger cuts off in its wind-up (the sac deflates, the acid fizzles).
                    float start2=11.2f,cancel=start2+strike*.6f,startAt=now-(g-start2);
                    if(g>=8.4f&&g<8.4f+recover){age=g-8.4f;jawAge=age;}else if(g>=start2&&g<start2+recover){jawAge=g-start2;if(g<cancel)age=jawAge;}
                    if(g>=cancel&&g<start2+recover&&Mathf.Abs(s.CancelFor-startAt)>.01f)
                    {
                        float at=now-(g-cancel);s.CancelFor=startAt;s.CancelAt=at;s.CancelSwell=NativeHordeFx.Swell(attack,cancel-start2,strike);HordeFizzle(s,ground,at);
                        float yr3=yawToMe*Mathf.Deg2Rad;s.React.Add(at,new NVector2(-Mathf.Sin(yr3),-Mathf.Cos(yr3)),StaggerWeight.Flinch,10,true,false,false,st.stagger);
                    }
                    if(g>=cancel&&g<start2+recover)cancelAge=g-cancel;
                }
                else if(g>=8.4f&&g<8.4f+recover)age=g-8.4f;else if(g>=10.3f&&g<10.3f+recover)age=g-10.3f;
                // three hits from me: a dagger, a sword, a greatsword
                for(int h=0;h<3;h++){float at=galleryStart+Mathf.Floor((now-galleryStart+k*.37f)/GalleryLoop)*GalleryLoop-k*.37f+GalleryHitAt[h];if(now>=at&&s.React.Newest<at-.01f){var w=NativeStaggerRules.Classify(GalleryHitSpecs[h],"melee",GalleryHitAmount[h]);float yr2=yawToMe*Mathf.Deg2Rad;s.React.Add(at,new NVector2(-Mathf.Sin(yr2),-Mathf.Cos(yr2)),w,GalleryHitAmount[h],true,false,GalleryHitAmount[h]>=st.hp*.25f,st.stagger);}}
                if(g>=14.4f)
                {
                    // The death, then hidden until the loop starts over.
                    float d=g-14.4f;NativeHordeGait.Death(minionPose,k,seed,Mathf.Min(d,2.3f),new NVector2(0,-1));NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");
                    rig.Root.transform.SetPositionAndRotation(slot,Quaternion.Euler(0,yaw,0));bool body=!NativeMinionFx.Gone(d)&&!(k==NativeHordeKinds.Bloater&&d>=NativeHordeGait.BurstAt);
                    if(body){HordeDeathBody(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,d,null);HordeDeathJaw(rig,k,0,id,null);}HordeAdd(rig,id,float.NaN,d,ground,ground,float.NaN,Vector3.zero,0,body,k*8);
                    if(k==NativeHordeKinds.Bloater){float since=d-NativeHordeGait.BurstAt;if(since>=0&&since<1.5f&&hordeBurstCount<hordeBursts.Length)hordeBursts[hordeBurstCount++]=new HordeBurstJob{At=slot,Since=since,Seed=NativeGoreRules.FnvDigits(id)};}
                    s.Gait.Reset();continue;
                }
                ref var f=ref hordeInput[0];
                f=new HordeMotionInput{Kind=k,Look=0,Id=id,Seed=seed,Time=now,Yaw=yaw,Ground=ground,Root=ToN(pos),Velocity=ToN(vel),AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=1,Health=1,HasTarget=!entering,Target=ToN(me.Body.position),Entering=entering,EntryAge=entryAge,EntryWalk=entryWalk};
                s.Gait.Pose(f,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);
                HordeKindJob(s,id,k,attack,age,strike,recover,pos,yaw,ground,st,me,rig,now);
                NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));
                float swell=!float.IsNaN(cancelAge)?s.CancelSwell*(1-Mathf.SmoothStep(0,1,cancelAge/.35f)):NativeHordeFx.Swell(attack,age,strike),gk=NativeHordeFx.Brighten(age,strike,recover);var glow=HordeGlow(id);
                if(float.IsNaN(jawAge))jawAge=age;s.Jaw=NativeHordeJaw.Open(k,attack,jawAge,strike,cancelAge,float.NaN,now,seed);
                float flash=1;for(int h=0;h<3;h++){float at=galleryStart+Mathf.Floor((now-galleryStart+k*.37f)/GalleryLoop)*GalleryLoop-k*.37f+GalleryHitAt[h];if(now>=at&&now-at<.12f)flash=NativeHitFlash.Code(now-at,false,EffectsQuality);}
                HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,swell,new Vector4(gk,gk,gk,glow.w),flash);HordeQueueJaw(rig,k,0,NativeHordeModels.Full,s.Jaw,new Vector4(gk,gk,gk,glow.w),flash);
                HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8);
            }
        }
        private void ClearHordeGallery(){for(int k=0;k<galleryRigs.Length;k++){galleryRigs[k]?.Dispose();galleryRigs[k]=null;galleryStates[k]=null;}galleryStart=float.NaN;ClearHordeHulkGallery();ClearHordeHulkMovesGallery();ClearHordeHulkChargeGallery();}
        /// <summary>A new arena, generation or rewind: forget every add's state, the dead, the parts and the effects in flight.</summary>
        private void ClearHordeKinds(NativeArena arena)
        {
            foreach(var s in hordeKindStates.Values)hordeKindSpare.Push(s);hordeKindStates.Clear();hordeDead.Clear();ClearHordeHulkMoves();hordeKindArena=arena;hordeKindGeneration=arena?.Generation??-1;hordeKindEvent=0;hordeSpitEvent=0;hordeSpitEvents.Clear();hordeSpat.Clear();
            for(int i=0;i<hordeParts.Length;i++)hordeParts[i].Live=false;ClearHordeAcid();ClearHordeBreaches();for(int i=0;i<hordeDust.Length;i++)hordeDust[i].Power=0;hordeJobCount=0;hordeBurstCount=0;ClearHordeZombieFx();
        }
    }

    public sealed partial class NativeAudio
    {
        // ---------- the Siren minions' wind-ups, strikes and heavy footfalls (Core/NativeHordeKinds cue keys) ----------
        // Existing bank keys only, played when the owner's LocalAssets bank has them (BankPlay; silent otherwise, as today). Timed
        // from each add's replicated LastAttack and the content's strike time, once per attack; the brute's and the bloater's steps
        // by the distance they cover. The DEV preview's substituted kinds sound as drawn.
        private readonly Dictionary<int,float> hordeStrides=new Dictionary<int,float>();private readonly Dictionary<int,Vector3> hordeLast=new Dictionary<int,Vector3>();private int hordeCueGeneration=-1;
        private struct HordeBreachCue{public float BreakAt;public Vector3 At;}
        private readonly Dictionary<PveEntry,HordeBreachCue> hordeBreachCues=new Dictionary<PveEntry,HordeBreachCue>();private readonly List<PveEntry> hordeBreachDone=new List<PveEntry>();
        // The Hulks seen alive this round (by add id), whose deaths crash down with a thud.
        private readonly HashSet<int> hordeHulks=new HashSet<int>();
        private void HordeCues(NativeArena arena)
        {
            if(arena==null)return;HordeWallCues(arena);if(arena.Pve==null)return;if(arena.Generation!=hordeCueGeneration){hordeCueGeneration=arena.Generation;hordeStrides.Clear();hordeLast.Clear();hordeBreachCues.Clear();hordeHulks.Clear();}float t=arena.Time;
            foreach(var m in arena.Minions)
            {
                var a=m.Pve;if(a==null||m.Hp<=0||a.Stats==null||a.Travelling)continue;
                int real=NativeHordeKinds.Resolve(a.Stats.id),kind=NativeHordeKinds.Preview>=0&&NativeHordeKinds.Preview<NativeHordeKinds.Count?NativeHordeKinds.Preview:real;
                if(kind==NativeHordeKinds.Hulk)hordeHulks.Add(m.Id);
                if(t<a.ActiveAt)
                {
                    // The jump-down lands with the slam's thud (existing bank keys: the Siren's slam over the ground impact); the Hulk's
                    // warning rumbles and its landing quakes.
                    if(NativeHordeDrop.Is(a.Entry))
                    {
                        var p=NativeHordeDrop.Profile(kind);float land=NativeHordeDrop.LandAt(a.ActiveAt,p);
                        if(p.Heavy&&t>=a.SpawnAt&&t-a.SpawnAt<.15f&&Once("add"+m.Id+"r",a.SpawnAt))BankPlay("structure.rumble",m.Body.position,.8f,0,null,false,null,.7f);
                        if(t>=land&&t-land<.12f&&Once("add"+m.Id+"d",land))
                        {
                            BankPlay(NativeHordeKinds.StrikeCue(NativeHordeKinds.Attack.Smash),m.Body.position,NativeHordeKinds.StrikeGain);BankPlay(NativeHordeKinds.StrikeUnder(NativeHordeKinds.Attack.Smash),m.Body.position,NativeHordeKinds.UnderGain,0,null,false,null,p.Heavy?.55f:.7f);
                            if(p.Heavy){BankPlay("blast.quake",m.Body.position,.9f,0,null,false,null,.8f);BankPlay("structure.boom",m.Body.position,.7f,0,null,false,null,.75f);}
                        }
                    }
                    // A breach: the blows on the wall from behind, then it bursts (its restore is cued from the remembered break).
                    else if(NativeBreach.Is(a.Entry))
                    {
                        float breakAt=NativeBreach.BreakAt(a.ActiveAt);var wall=(NativeArena.Vec(a.Entry.from.Position(0))+NativeArena.Vec(a.Entry.@exit.Position(0)))*.5f;hordeBreachCues[a.Entry]=new HordeBreachCue{BreakAt=breakAt,At=wall};
                        for(int b=0;b<NativeBreach.Blows.Length-1;b++){float at=a.SpawnAt+NativeBreach.Blows[b];if(t>=at&&t-at<.12f&&Once("add"+m.Id+"k"+b,at)){BankPlay("structure.thud",wall,.85f,0,null,false,null,.7f);BankPlay("structure.crack",wall,.5f);}}
                        if(t>=breakAt&&t-breakAt<.12f&&Once("add"+m.Id+"b",breakAt)){BankPlay("structure.boom",wall,.85f,0,null,false,null,.8f);BankPlay("structure.debris",wall,.7f);}
                    }
                    continue;
                }
                var attack=NativeHordeKinds.AttackOf(kind,a.Stats.attack,a.Crawl);float strike=NativeHordeKinds.Strike(a.Stats,a.Crawl,a.Lost);Vector3 pos=m.Body.position;
                // The cue keys are only built inside a cue's window (no string per add per frame).
                string wind=NativeHordeKinds.WindupCue(attack);if(wind!=null&&t>=m.LastAttack&&t-m.LastAttack<.15f&&Once("add"+m.Id+"w",m.LastAttack))BankPlay(wind,pos,NativeHordeKinds.WindupGain);
                float at2=m.LastAttack+strike;if(t>=at2&&t-at2<.12f&&Once("add"+m.Id+"s",m.LastAttack)){BankPlay(NativeHordeKinds.StrikeCue(attack),pos,NativeHordeKinds.StrikeGain);string under=NativeHordeKinds.StrikeUnder(attack);if(under!=null)BankPlay(under,pos,NativeHordeKinds.UnderGain,0,null,false,null,kind==NativeHordeKinds.Hulk?.6f:.8f);if(kind==NativeHordeKinds.Hulk)BankPlay("blast.quake",pos,.6f,0,null,false,null,.9f);}
                // The Hulk's roar (the same seeded moments the presentation roars at): a deep groan.
                if(kind==NativeHordeKinds.Hulk){float roar=NativeHordeRoar.Age(kind,NativeHordeKinds.Seed(m.Id),t,a.ActiveAt,t-m.LastAttack,NativeHordeKinds.Recover(a.Stats,a.Crawl,a.Lost));if(!float.IsNaN(roar)&&roar<.12f&&Once("add"+m.Id+"o",Mathf.Round((t-roar)*10)/10))BankPlay("structure.groan",pos,.95f,0,null,false,null,.55f);}
                string step=NativeHordeKinds.StepCue(kind);if(step==null)continue;
                if(hordeLast.TryGetValue(m.Id,out var last)){var d=pos-last;d.y=0;float moved=d.magnitude;if(moved<1){hordeStrides.TryGetValue(m.Id,out float acc);acc+=moved;float sc=NativeHordeKinds.Scale[kind];float stride=NativeHordeGait.Stride(kind,Mathf.Max(.5f/sc,moved/Mathf.Max(1e-3f,Time.unscaledDeltaTime)/sc))*.5f*sc;if(acc>=stride){acc-=stride;BankPlay(step,pos,NativeHordeKinds.StepGainOf(kind),0,null,false,null,kind==NativeHordeKinds.Hulk?.55f:kind==NativeHordeKinds.Brute?.72f:.82f);}hordeStrides[m.Id]=acc;}}
                hordeLast[m.Id]=pos;
            }
            // A Hulk's death hits the floor (NativeHordeGait.DownAt into the fall) with a heavy thud.
            if(hordeHulks.Count>0)for(int i=0;i<arena.MinionDeaths.Count;i++)
            {
                var d=arena.MinionDeaths[i];if(!hordeHulks.Contains(d.id))continue;float down=d.at+NativeHordeGait.DownAt;
                if(t>=down&&t-down<.12f&&Once("add"+d.id+"x",down))BankPlay("structure.thud",d.position,.9f,0,null,false,null,.6f);
            }
            // The breaches' holographic restore: the scan's hum, the wall setting solid.
            hordeBreachDone.Clear();
            foreach(var pair in hordeBreachCues)
            {
                float restore=NativeBreach.DefaultRestoreAt(pair.Value.BreakAt),solid=NativeBreach.SolidAt(restore);
                if(t>=restore&&t-restore<.12f&&Once("breach"+pair.Key.id+"h",restore))BankPlay("siren.reveal",pair.Value.At,.6f,0,null,false,null,1.2f);
                if(t>=solid&&t-solid<.12f&&Once("breach"+pair.Key.id+"x",solid))BankPlay("structure.snap",pair.Value.At,.6f);
                if(t>solid+1||t<pair.Value.BreakAt-10)hordeBreachDone.Add(pair.Key);
            }
            foreach(var e in hordeBreachDone)hordeBreachCues.Remove(e);
        }
        // A Hulk's breach of a wall block (a replicated dynamic record; PvE and the Ward sandbox alike): the blows on the wall at
        // its punch point, the burst, the restore's hum and the block setting solid, the breach's own sounds above.
        private void HordeWallCues(NativeArena arena)
        {
            if(!(arena.WardBreaches is List<NativeBreachRecord> list))return;float t=arena.Time;
            for(int i=0;i<list.Count;i++)
            {
                var r=list[i];if(r==null||!r.Dynamic)continue;float knock=NativeBreach.KnockAt(r.breakAt),solid=NativeBreach.SolidAt(r.restoreAt);if(t<knock||t>solid+1)continue;var at=new Vector3(r.point.x,r.point.y,r.point.z);
                for(int b=0;b<NativeBreach.Blows.Length-1;b++){float k=knock+NativeBreach.Blows[b];if(t>=k&&t-k<.12f&&Once("wall"+r.block+"k"+b,k)){BankPlay("structure.thud",at,.85f,0,null,false,null,.7f);BankPlay("structure.crack",at,.5f);}}
                if(t>=r.breakAt&&t-r.breakAt<.12f&&Once("wall"+r.block+"b",r.breakAt)){BankPlay("structure.boom",at,.85f,0,null,false,null,.8f);BankPlay("structure.debris",at,.7f);}
                if(t>=r.restoreAt&&t-r.restoreAt<.12f&&Once("wall"+r.block+"h",r.restoreAt))BankPlay("siren.reveal",at,.6f,0,null,false,null,1.2f);
                if(t>=solid&&t-solid<.12f&&Once("wall"+r.block+"x",solid))BankPlay("structure.snap",at,.6f);
            }
        }
    }
}

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Settings · Developer's Siren minion preview (NativeHordeKinds.Preview): off, one kind for every add, or a mix by id.
        private static readonly string[] HordePreviewIds={"off","shambler","runner","brute","spitter","crawler","bloater","hulk","mix"},HordePreviewNames={"Off","Shambler","Runner","Brute","Spitter","Crawler","Bloater","Hulk","Mix"};
        private static string HordePreviewId(int preview)=>preview==NativeHordeKinds.Mix?"mix":preview>=0&&preview<NativeHordeKinds.Count?NativeHordeKinds.Ids[preview]:"off";
        private static int HordePreviewChoice(string id)=>id=="mix"?NativeHordeKinds.Mix:id=="off"?-1:NativeHordeKinds.Resolve(id);
    }
}
