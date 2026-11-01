using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The heavy kinds' body animation, runtime side (owner 2026-10-14: "Slam/landing/wall break animation also needs improvements
    // from hulk/brute. Hulk zombie needs adjustments on their movement ... I'd like them to all have a charge animation (they can
    // charge through walls) and a couple of punch animations (like a straight punch, and a swipe)"; from the Windows test build:
    // "The hulk needs to punch and charge at the player too, not just the wall ... They literally can charge from across the map
    // to the player breaking the walls if they are hunting them and 'know' where they are"). Presentation only: reads the
    // replicated add state, the breach records and the event feed, writes only the motion input (Core/NativeHordeHeavy).
    //  - A wall knock (a breach entrance, or a Hulk breaking a wall block) poses the three blows and the carry through the hole on
    //    Core/NativeBreach's clock (the same clock the breach's cracks, burst and sounds use).
    //  - The proposed moves (Core/NativeHulkMoves, docs/SIREN-ZOMBIES.md section 10, contract v2 in 10.5) are dormant: they play
    //    only once the rules report them. Until the frame fields exist the presentation accepts the same state as Impact events
    //    ("hulk-move", "hulk-charge" with the hunted seat, "hulk-charge-end"); HordeHulkState (a Core NativeHulkCharge.State per
    //    add) is the one place that holds it, and HordeHulkFrame the one place the frame fields will be read.
    //  - A charge: the wind-up turns with the replicated yaw; its heading locks from the yaw on the first frame past the wind-up
    //    and the run holds the lowered shoulder for as long as the authority runs it (cross-map), on the heavy gait's planted feet
    //    at its real speed; a wall it crashes through (a breach record of this Hulk broken in its run) flinches the shoulder and it
    //    runs on. The charge's tell, lane, warning and effects: Runtime/NativeHordeHulkCharge.cs.
    //  - The Dev Sandbox gallery's third Hulk shows the moves on a script: a turn in place, the heavy walk and run, a straight punch,
    //    a swipe with the left arm, a charge into a solid stop, and a wall knock with its carry; the fourth (NativeHordeHulkCharge)
    //    a punch and a swipe at a hardlight dummy, then a wind-up at the local pilot and a cross-map charge through two walls.
    public sealed partial class ArenaPresentation
    {
        // ---- the proposed moves' state per add (from the events until the frame fields exist) ----
        private struct HordeHulkState{public float MoveFor;public NativeHulkMoves.Move Move;public NativeHulkCharge.State Charge;}
        private readonly Dictionary<int,HordeHulkState> hordeHulkStates=new Dictionary<int,HordeHulkState>();
        private static HordeHulkState NoHulkState=>new HordeHulkState{MoveFor=-100,Charge=NativeHulkCharge.State.None};
        /// <summary>HordeKindsBegin: a new event that reports a heavy add's move or charge.</summary>
        private void HordeHulkEvent(in ArenaEvent e)
        {
            if(e.Kind!=EventKind.Impact||e.Detail==null||!e.Detail.StartsWith("hulk-",StringComparison.Ordinal))return;
            if(!hordeHulkStates.TryGetValue(e.Target,out var h))h=NoHulkState;
            if(e.Detail==NativeHulkMoves.MoveEvent){h.MoveFor=e.Time;h.Move=NativeHulkMoves.Of((int)e.Amount);}
            // NativeHulkMoves.ChargeEvent (Amount the hunted seat) and NativeHulkMoves.ChargeEndEvent (Amount how it ended).
            else if(!h.Charge.Event(e.Detail,e.Amount,e.Time))return;
            hordeHulkStates[e.Target]=h;
        }
        /// <summary>
        /// The frame fields (docs/SIREN-ZOMBIES.md 10.5; Astra's PR #215: NativeMinionFrame move, chargeAt, chargeEndAt, chargeEnd,
        /// chargeTarget, applied to NativePveAdd through NativeHulkHunt on host and replicas), the one place they are read. The
        /// events above stay as the fallback (a lost event is filled in by the frame, and a frame without an add reads nothing).
        /// </summary>
        private void HordeHulkFrame(NativeMinion m)
        {
            var a=m?.Pve;if(a==null)return;
            if(!hordeHulkStates.TryGetValue(m.Id,out var h))h=NoHulkState;
            if(h.MoveFor!=m.LastAttack){h.MoveFor=m.LastAttack;h.Move=NativeHulkMoves.Of(a.Move);}
            h.Charge.Frame(a.ChargeAt,a.ChargeEndAt,a.ChargeEnd,a.ChargeTarget);hordeHulkStates[m.Id]=h;
        }
        private void ClearHordeHulkMoves(){hordeHulkStates.Clear();ClearHordeCharges();}
        /// <summary>The attack an add plays at LastAttack: the reported move (its strike and recovery the contract's, the effects
        /// of the nearest existing attack: a punch's thrust, a swipe's rake) or the content's attack as today.</summary>
        private NativeHulkMoves.Move HordeHulkAttack(NativeMinion m,bool crawl,int lost,ref NativeHordeKinds.Attack attack,ref float strike,ref float recover)
        {
            HordeHulkFrame(m);
            if(!hordeHulkStates.TryGetValue(m.Id,out var h)||Math.Abs(h.MoveFor-m.LastAttack)>.02f||h.Move==NativeHulkMoves.Move.Content)return NativeHulkMoves.Move.Content;
            if(crawl||NativeHordeKinds.Armless(lost))return NativeHulkMoves.Move.Content;
            strike=NativeHulkMoves.Strike(h.Move,strike,crawl,lost);recover=NativeHulkMoves.Recover(h.Move,recover,crawl,lost);
            attack=h.Move==NativeHulkMoves.Move.Punch?NativeHordeKinds.Attack.Lunge:NativeHordeKinds.Attack.Swipe;return h.Move;
        }
        /// <summary>The knock and the charge into the motion input: a breach entrance's knock (SpawnAt to ActiveAt), a wall block's
        /// knock and carry (the dynamic record's actor), a reported charge (its phase from Core NativeHulkCharge.State: the wind-up,
        /// the run on the heading locked from the replicated yaw, the stop), and a wall it crashed through on the run.</summary>
        private void HordeHulkInput(ref HordeMotionInput f,NativeMinion m,float now,bool entering,bool breach)
        {
            var a=m.Pve;hordeChargePending=false;
            if(breach&&entering&&now>=a.SpawnAt){f.Knocking=true;f.KnockAge=now-a.SpawnAt;}
            else if(!entering&&HordeWallCarry(m.Id,now,out float knockAt)){f.Knocking=true;f.KnockAge=now-knockAt;}
            if(entering||!hordeHulkStates.TryGetValue(m.Id,out var h)||!h.Charge.Known)return;
            float sc=NativeHordeKinds.Scale[f.Kind>=0&&f.Kind<NativeHordeKinds.Count?f.Kind:NativeHordeKinds.Hulk];var vel=new Vector3(f.Velocity.X,0,f.Velocity.Z)*sc;
            var floor=new Vector3(f.Root.X*sc,f.Ground*sc,f.Root.Z*sc);
            var ph=h.Charge.Step(now,m.Yaw,vel.magnitude,ToN(floor),out float age,out float endAge);hordeHulkStates[m.Id]=h;
            if(ph==NativeHulkMoves.ChargePhase.None)return;
            bool stop=ph==NativeHulkMoves.ChargePhase.Stop;
            f.Charging=true;f.ChargeAge=age;f.ChargeEnded=stop;f.ChargeEndAge=stop?endAge:0;f.ChargeHow=stop?h.Charge.How:NativeHulkMoves.ChargeEnd.Running;
            // The run keeps the heading locked at the wind-up's end (the authority holds the yaw on it).
            if(ph==NativeHulkMoves.ChargePhase.Run&&h.Charge.Locked)f.Yaw=h.Charge.LockYaw;
            // A wall it ran through (a breach of this actor broken in the run): the shoulder's flinch.
            if(HordeWallBroke(m.Id,h.Charge.ChargeAt+NativeHulkMoves.ChargeWind-.05f,now,out float brokeAt)){f.ChargeBroke=true;f.ChargeBreakAge=now-brokeAt;}
            HordeChargeBegin(m.Id,h.Charge,ph,age,endAge,floor,vel,h.Charge.Locked&&ph!=NativeHulkMoves.ChargePhase.Wind?h.Charge.LockYaw:m.Yaw,f.Seed,false,f.ChargeBroke?f.ChargeBreakAge:float.NaN);
        }
        /// <summary>Whether Hulk `actor` is knocking a wall block or carrying through the hole it broke (the dynamic record's knock
        /// start to its break plus the carry), and when its knock began. A wall it crashed through on a charge has neither.</summary>
        private bool HordeWallCarry(int actor,float now,out float knockAt)
        {
            for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Dynamic&&!b.Charged&&b.Actor==actor&&now>=b.SpawnAt&&now<b.SpawnAt+NativeHordeGait.KnockEnd+NativeHulkMoves.BlendOut){knockAt=b.SpawnAt;return true;}}
            knockAt=0;return false;
        }
        /// <summary>The newest wall Hulk `actor` broke between `from` and `now` (a wall block's record or a breach panel's naming it
        /// as the actor; its break time).</summary>
        private bool HordeWallBroke(int actor,float from,float now,out float breakAt)
        {
            breakAt=float.NegativeInfinity;for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Actor==actor&&b.BreakAt>=from&&b.BreakAt<=now&&b.BreakAt>breakAt)breakAt=b.BreakAt;}
            return !float.IsNegativeInfinity(breakAt);
        }
        /// <summary>The Hulk's bellow through a charge's wind-up (its jaw gapes as it rears): the wind-up's age, else `roar` (the
        /// seeded roar's age or NaN). The jaw only; the keyed wind-up replaces the roar's body.</summary>
        private float HordeHulkBellow(int id,float now,float roar)
        {
            if(!hordeHulkStates.TryGetValue(id,out var h)||!h.Charge.Known||h.Charge.Ended)return roar;float age=now-h.Charge.ChargeAt;
            return age>=0&&age<NativeHulkMoves.ChargeWind?Mathf.Min(age,NativeHordeRoar.Seconds-.5f):roar;
        }
        /// <summary>Whether a break by Hulk `actor` at `breakAt` came in its charge's run (no knock: it crashed through on contact).</summary>
        private bool HordeChargedBreak(int actor,float breakAt)=>actor!=0&&hordeHulkStates.TryGetValue(actor,out var h)&&h.Charge.RanThrough(breakAt);

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
            HordeKindJob(s,id,k,attack,age,strike,recover,pos,yaw,ground,st,me,rig,now,f.Side);if(s.Gait.Landed!=0)HordeDust(s,k,ground,now);
            s.Jaw=NativeHordeJaw.Open(k,attack,age,strike,float.NaN,float.NaN,now,seed);
            NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            var glow=HordeGlow(id);HordeQueue(rig,NativeHordeModels.Id(k,1,NativeHordeModels.Full),id,0,0,0,glow,1);HordeQueueJaw(rig,k,1,NativeHordeModels.Full,s.Jaw,glow,1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8+1);
        }
        private void ClearHordeHulkMovesGallery(){galleryMovesRig?.Dispose();galleryMovesRig=null;galleryMovesState=null;galleryMovesLast=float.NaN;}
    }
}
