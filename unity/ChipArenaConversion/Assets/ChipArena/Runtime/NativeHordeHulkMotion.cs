using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The heavy kinds' body animation, runtime side (owner 2026-10-14: "Slam/landing/wall break animation also needs improvements
    // from hulk/brute. Hulk zombie needs adjustments on their movement ... I'd like them to all have a charge animation (they can
    // charge through walls) and a couple of punch animations (like a straight punch, and a swipe)"). Presentation only: reads
    // the replicated add state, the breach records and the event feed, writes only the motion input (Core/NativeHordeHeavy).
    //  - A wall knock (a breach entrance, or a Hulk breaking a wall block) poses the three blows and the carry through the hole on
    //    Core/NativeBreach's clock (the same clock the breach's cracks, burst and sounds use).
    //  - The proposed moves (Core/NativeHulkMoves, docs/SIREN-ZOMBIES.md section 10) are dormant: they play only once the rules
    //    report them. Until the frame fields exist the presentation accepts the same state as Impact events ("hulk-move",
    //    "hulk-charge", "hulk-charge-end"); HordeHulkState is the one place that reads it, so the frame fields replace the events
    //    there alone.
    //  - The Dev Sandbox gallery's third Hulk shows them on a script: a turn in place, the heavy walk and run, a straight punch,
    //    a swipe with the left arm, a charge into a solid stop, and a wall knock with its carry.
    public sealed partial class ArenaPresentation
    {
        // ---- the proposed moves' state per add (from the events until the frame fields exist) ----
        private struct HordeHulkState{public float MoveFor,ChargeAt,ChargeEndAt;public NativeHulkMoves.Move Move;public NativeHulkMoves.ChargeEnd How;}
        private readonly Dictionary<int,HordeHulkState> hordeHulkStates=new Dictionary<int,HordeHulkState>();
        private static HordeHulkState NoHulkState=>new HordeHulkState{MoveFor=-100,ChargeAt=-100,ChargeEndAt=-100};
        /// <summary>HordeKindsBegin: a new event that reports a heavy add's move or charge.</summary>
        private void HordeHulkEvent(in ArenaEvent e)
        {
            if(e.Kind!=EventKind.Impact||e.Detail==null||!e.Detail.StartsWith("hulk-",StringComparison.Ordinal))return;
            if(!hordeHulkStates.TryGetValue(e.Target,out var h))h=NoHulkState;
            if(e.Detail==NativeHulkMoves.MoveEvent){h.MoveFor=e.Time;h.Move=NativeHulkMoves.Of((int)e.Amount);}
            else if(e.Detail==NativeHulkMoves.ChargeEvent){h.ChargeAt=e.Time;h.ChargeEndAt=-100;h.How=NativeHulkMoves.ChargeEnd.Running;}
            else if(e.Detail==NativeHulkMoves.ChargeEndEvent){h.ChargeEndAt=e.Time;h.How=NativeHulkMoves.EndOf((int)e.Amount);}
            else return;
            hordeHulkStates[e.Target]=h;
        }
        private void ClearHordeHulkMoves()=>hordeHulkStates.Clear();
        /// <summary>The attack an add plays at LastAttack: the reported move (its strike and recovery the contract's, the effects
        /// of the nearest existing attack: a punch's thrust, a swipe's rake) or the content's attack as today.</summary>
        private NativeHulkMoves.Move HordeHulkAttack(NativeMinion m,bool crawl,int lost,ref NativeHordeKinds.Attack attack,ref float strike,ref float recover)
        {
            if(!hordeHulkStates.TryGetValue(m.Id,out var h)||Math.Abs(h.MoveFor-m.LastAttack)>.02f||h.Move==NativeHulkMoves.Move.Content)return NativeHulkMoves.Move.Content;
            if(crawl||NativeHordeKinds.Armless(lost))return NativeHulkMoves.Move.Content;
            strike=NativeHulkMoves.Strike(h.Move,strike,crawl,lost);recover=NativeHulkMoves.Recover(h.Move,recover,crawl,lost);
            attack=h.Move==NativeHulkMoves.Move.Punch?NativeHordeKinds.Attack.Lunge:NativeHordeKinds.Attack.Swipe;return h.Move;
        }
        /// <summary>The knock and the charge into the motion input: a breach entrance's knock (SpawnAt to ActiveAt), a wall block's
        /// knock and carry (the dynamic record's actor), a reported charge.</summary>
        private void HordeHulkInput(ref HordeMotionInput f,NativeMinion m,float now,bool entering,bool breach)
        {
            var a=m.Pve;
            if(breach&&entering&&now>=a.SpawnAt){f.Knocking=true;f.KnockAge=now-a.SpawnAt;}
            else if(!entering&&HordeWallCarry(m.Id,now,out float knockAt)){f.Knocking=true;f.KnockAge=now-knockAt;}
            if(!hordeHulkStates.TryGetValue(m.Id,out var h)||h.ChargeAt<-90||now<h.ChargeAt)return;
            bool ended=h.ChargeEndAt>-90&&h.ChargeEndAt>=h.ChargeAt;
            if(ended?now-h.ChargeEndAt>NativeHulkMoves.Stop(h.How)+NativeHulkMoves.BlendOut:now-h.ChargeAt>NativeHulkMoves.ChargeWind+NativeHulkMoves.ChargeRun+1)return;
            f.Charging=true;f.ChargeAge=now-h.ChargeAt;f.ChargeEnded=ended;f.ChargeEndAge=ended?now-h.ChargeEndAt:0;f.ChargeHow=ended?h.How:NativeHulkMoves.ChargeEnd.Running;
            // A wall it ran through on the run (a dynamic breach of this actor broken after the wind-up): the shoulder's jolt.
            if(HordeWallBroke(m.Id,h.ChargeAt+NativeHulkMoves.ChargeWind,now,out float brokeAt)){f.ChargeBroke=true;f.ChargeBreakAge=now-brokeAt;}
        }
        /// <summary>Whether Hulk `actor` is knocking a wall block or carrying through the hole it broke (the dynamic record's knock
        /// start to its break plus the carry), and when its knock began.</summary>
        private bool HordeWallCarry(int actor,float now,out float knockAt)
        {
            for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Dynamic&&b.Actor==actor&&now>=b.SpawnAt&&now<b.SpawnAt+NativeHordeGait.KnockEnd+NativeHulkMoves.BlendOut){knockAt=b.SpawnAt;return true;}}
            knockAt=0;return false;
        }
        /// <summary>The newest wall block Hulk `actor` broke between `from` and `now` (its break time).</summary>
        private bool HordeWallBroke(int actor,float from,float now,out float breakAt)
        {
            breakAt=float.NegativeInfinity;for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Dynamic&&b.Actor==actor&&b.BreakAt>=from&&b.BreakAt<=now&&b.BreakAt>breakAt)breakAt=b.BreakAt;}
            return !float.IsNegativeInfinity(breakAt);
        }

        // ---- the Dev Sandbox gallery's moves Hulk ----
        public const float GalleryMovesLoop=27;
        private CapturedAnimation.Player galleryMovesRig;private HordeKindState galleryMovesState;private float galleryMovesLast=float.NaN;
        /// <summary>The gallery's third Hulk, beside the first: a turn in place, the heavy walk and run, a straight punch, a swipe with
        /// the left arm, a charge into a solid stop, a wall knock and its carry (the proposed moves on a script; no authority add).</summary>
        private void HordeGalleryHulkMoves(NativePilot me,float now,Vector3 across,float yawToMe,float ground)
        {
            int k=NativeHordeKinds.Hulk,id=990020;
            if(galleryMovesRig==null){galleryMovesRig=captured.Create(transform,id);galleryMovesState=new HordeKindState();galleryMovesState.Reset(id);galleryMovesState.Kind=k;galleryMovesState.Look=1;galleryMovesState.React.Bind(k);}
            var rig=galleryMovesRig;var s=galleryMovesState;var st=NativeHordeKinds.ProposedRow(k);uint seed=NativeHordeKinds.Seed(id);float sc=NativeHordeKinds.Scale[k];s.Scale=sc;
            float g=Mathf.Repeat(now-galleryStart,GalleryMovesLoop);if(!float.IsNaN(galleryMovesLast)&&g<galleryMovesLast)s.Gait.Reset();galleryMovesLast=g;
            Vector3 away=-GalleryDir(yawToMe),p0=galleryAt+away*4.2f+across*4.6f;p0.y=ground;float root=NativeHordeKinds.DrawRoot(k,false),rad=2f;
            // The path: a circle walked then run from p0, the moves where it stops, the charge across, the knock where it ends.
            Vector3 Circle(float ang)=>p0+(across*(1-Mathf.Cos(ang))+away*Mathf.Sin(ang))*rad;
            const float walkAt=3.6f,runAt=9.6f,stopAt=11.4f,punchAt=12f,swipeAt=14.4f,chargeAt=17.2f,chargeStop=19.4f,knockAt=21.6f;
            float endAng=(runAt-walkAt)*st.walk/rad+(stopAt-runAt)*st.run/rad;Vector3 p1=Circle(endAng),dirC=-across,p2=p1+dirC*(chargeStop-chargeAt-NativeHulkMoves.ChargeWind)*NativeHulkMoves.ChargeSpeed,p2b=p2-dirC*.4f;
            float yawC=Mathf.Atan2(dirC.x,dirC.z)*Mathf.Rad2Deg;Vector3 toMe=me.Body.position-p1;float yawP1=Mathf.Atan2(toMe.x,toMe.z)*Mathf.Rad2Deg;
            Vector3 pos=p0,vel=Vector3.zero;float yaw=yawToMe;
            var f=new HordeMotionInput{Kind=k,Look=1,Id=id,Seed=seed,Time=now,Health=1,AttackAge=float.NaN,Attack=NativeHordeKinds.Attack.Smash,Side=1,HasTarget=true,Target=ToN(me.Body.position)/sc};
            var attack=NativeHordeKinds.Attack.Smash;float age=float.NaN,strike=NativeHordeKinds.Strike(st.hit,false,0),recover=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);
            if(g<walkAt){yaw=g<.6f?yawToMe:g<2.4f?yawToMe+110:yawToMe;}
            else if(g<stopAt)
            {
                float ang=g<runAt?(g-walkAt)*st.walk/rad:(runAt-walkAt)*st.walk/rad+(g-runAt)*st.run/rad,speed=g<runAt?st.walk:st.run;
                pos=Circle(ang);vel=(across*Mathf.Sin(ang)+away*Mathf.Cos(ang))*speed;yaw=Mathf.Atan2(vel.x,vel.z)*Mathf.Rad2Deg;
            }
            else if(g<chargeAt)
            {
                pos=p1;yaw=yawP1;
                var move=g>=swipeAt?NativeHulkMoves.Move.Swipe:g>=punchAt?NativeHulkMoves.Move.Punch:NativeHulkMoves.Move.Content;
                if(move!=NativeHulkMoves.Move.Content)
                {
                    float start=move==NativeHulkMoves.Move.Punch?punchAt:swipeAt;f.Move=move;f.Side=move==NativeHulkMoves.Move.Swipe?-1:1;
                    strike=NativeHulkMoves.Strike(move,strike,false,0);recover=NativeHulkMoves.Recover(move,recover,false,0);attack=move==NativeHulkMoves.Move.Punch?NativeHordeKinds.Attack.Lunge:NativeHordeKinds.Attack.Swipe;
                    if(g-start<recover+.25f)age=g-start;
                }
            }
            else if(g<knockAt)
            {
                yaw=yawC;f.Charging=true;f.ChargeAge=g-chargeAt;f.ChargeEnded=g>=chargeStop;f.ChargeEndAge=g-chargeStop;f.ChargeHow=NativeHulkMoves.ChargeEnd.Solid;
                float run0=chargeAt+NativeHulkMoves.ChargeWind;
                if(g<run0)pos=p1;else if(g<chargeStop){pos=p1+dirC*(g-run0)*NativeHulkMoves.ChargeSpeed;vel=dirC*NativeHulkMoves.ChargeSpeed;}
                else{float u=Mathf.Clamp01((g-chargeStop)/.4f);pos=Vector3.Lerp(p2,p2b,Mathf.SmoothStep(0,1,u));}
            }
            else
            {
                yaw=yawC;f.Knocking=true;f.KnockAge=g-knockAt;float breakAt=knockAt+NativeBreach.Knock;
                float u=Mathf.Clamp01((g-breakAt)/NativeBreach.Through);pos=p2b+dirC*1.2f*u;if(g>=breakAt&&g<breakAt+NativeBreach.Through)vel=dirC*(1.2f/NativeBreach.Through);
            }
            pos.y=ground+root;float inv=1/sc;
            f.Yaw=yaw;f.Ground=ground*inv;f.Root=ToN(pos)*inv;f.Velocity=ToN(vel)*inv;f.AttackAge=age;f.Strike=strike;f.Recover=recover;f.Attack=attack;
            ref var input=ref hordeInput[0];input=f;
            s.Gait.Pose(input,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);
            HordeKindJob(s,id,k,attack,age,strike,recover,pos,yaw,ground,st,me,rig,now);if(s.Gait.Landed!=0)HordeDust(s,k,ground,now);
            s.Jaw=NativeHordeJaw.Open(k,attack,age,strike,float.NaN,float.NaN,now,seed);
            NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            var glow=HordeGlow(id);HordeQueue(rig,NativeHordeModels.Id(k,1,NativeHordeModels.Full),id,0,0,0,glow,1);HordeQueueJaw(rig,k,1,NativeHordeModels.Full,s.Jaw,glow,1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8+1);
        }
        private void ClearHordeHulkMovesGallery(){galleryMovesRig?.Dispose();galleryMovesRig=null;galleryMovesState=null;galleryMovesLast=float.NaN;}
    }
}
