using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The source PlayerView fields the captured-melee layers read, in the source frame (radians, +x the rig's left).
    // NaN marks an absent optional timestamp (swingAt/swingUntil/lastBlock); -100 an absent roll/action.
    public sealed class MeleeView
    {
        public string Id="",Spec="sword";public float Yaw,Pitch,DashTime,Hp=100,LastAttack=-100,Windup=.14f,SwingAt=float.NaN,SwingUntil=float.NaN,AttackRate=1;
        public float? AirJumpAt;public Vector3 Vel;public bool Grounded=true,Sprinting,Guarding,Hurricane,WeaponAway;public int AttackIndex;
        public bool Commit,Followup;public float CommitAt,CommitUntil,CommitYaw,StepAt,StepUntil,StepDistance;
        // The swing's feel flags (NativeMeleeFeel): a lock-on opener's long lunge and a swing entered from a sprint.
        public bool OpenerLunge,SprintAttack;
        // The jump attack's explicit flag once the simulation replicates one (1 air, 0 ground); -1 until then, when the
        // presented airtime decides (NativeAirAttackRules.Air).
        public int AirAttack=-1;
        // The plunging stab's replicated data (Core/NativePlungeStab: NativePlungeFields.Of, the one field mapping); None otherwise.
        public NativePlungeData Plunge=NativePlungeData.None;
        public float BlockTime,LastBlock=float.NaN,HitStunUntil,RollAt=-100,RollUntil=-100,ActionAt=-100,ActionUntil=-100;public string ActionKind;
        public bool Reaction,Parry;public float ReactionAt,ReactionUntil,ReactionYaw,ReactionTurn,ReactionStrength;public Vector3 ReactionDir,ReactionPoint;public int ReactionZone;
        public bool Down,Juggle;public string DownPhase;
        // The held weapons' length scale along +z: PlayerView.reach / WEAPONS[spec].range (stats.reach), 1 when absent.
        public float Reach=1;
        public bool Rolling(float t)=>RollAt>-90&&t>=RollAt&&t<RollUntil;
        public bool Committed(float t)=>Commit&&t<CommitUntil;
        public bool RollCancelled(float t)=>RollAt>-90&&RollAt>LastAttack&&RollAt<=t;
        public bool Action(float t)=>ActionKind!=null&&t<ActionUntil;
        public MeleeView Clone()=>(MeleeView)MemberwiseClone();
    }
    // Hit reaction in the struck pilot's facing frame (shared/hit-reaction.ts). Zones: 0 torso, 1 head, 2 legs, 3 left arm, 4 right arm.
    public sealed class MeleeReaction
    {
        public bool Parry;public float At,Until,Yaw,Turn,Strength;public Vector3 Direction,Point;public int Zone;
        // From native (Unity-frame) world vectors and a native yaw in degrees.
        public static MeleeReaction Hit(V3 position,float yawDegrees,V3 direction,V3 point,float time,float damage,bool headshot)
        {
            float yaw=-yawDegrees*(float)Math.PI/180,c=(float)Math.Cos(yaw),s=(float)Math.Sin(yaw);
            Vector3 Local(Vector3 a)=>new Vector3(a.X*c-a.Z*s,a.Y,a.X*s+a.Z*c);
            Vector3 hit=Local(new Vector3(-(point.x-position.x),point.y-position.y,point.z-position.z)),force=Local(new Vector3(-direction.x,direction.y,direction.z));
            float horizontal=(float)Math.Sqrt(force.X*force.X+force.Z*force.Z);if(horizontal>.001f){force.X/=horizontal;force.Z/=horizontal;}
            float torque=hit.Z*force.X-hit.X*force.Z;
            return new MeleeReaction{At=time,Until=time+.24f,Yaw=yaw,Direction=force,Point=hit,Zone=headshot||hit.Y>.43f?1:hit.Y<-.27f?2:Math.Abs(hit.X)>.24f?(hit.X>0?3:4):0,
                Turn=MeleeMath.Clamp(torque*2.2f+force.X*.18f,-.7f,.7f),Strength=MeleeMath.Clamp(.45f+damage/70,.45f,1)};
        }
        public static MeleeReaction ParryOf(V3 position,float yawDegrees,V3 direction,float time,float until)
        {var r=Hit(position,yawDegrees,direction,position,time,40,false);r.Parry=true;r.Until=until;r.Turn=-.35f;return r;}
    }
    // Client-only records of landed melee hits (attacker) and successful blocks (defender), from the frame's events
    // (block-impacts.ts): the attacker's hit-stop and the defender's captured block impact read them.
    public sealed class MeleeImpacts
    {
        private readonly Dictionary<string,float> hits=new Dictionary<string,float>(),blocks=new Dictionary<string,float>();
        public void RecordHit(string attacker,float at)
        {
            if(string.IsNullOrEmpty(attacker))return;Prune(hits,at);
            if(hits.TryGetValue(attacker,out float prev)&&at>=prev&&at-prev<.2f)return;hits[attacker]=at;
        }
        public void RecordBlock(string defender,float at){if(string.IsNullOrEmpty(defender))return;Prune(blocks,at);blocks[defender]=at;}
        private static readonly List<string> stale=new List<string>();
        private static void Prune(Dictionary<string,float> d,float time){stale.Clear();foreach(var kv in d)if(Math.Abs(time-kv.Value)>2)stale.Add(kv.Key);foreach(var k in stale)d.Remove(k);}
        public float HitAt(string id,float time,float window){return hits.TryGetValue(id,out float at)&&time>=at&&time-at<=window?at:float.NaN;}
        public float BlockAt(string id,float time,float window){if(!blocks.TryGetValue(id,out float at))return float.NaN;float age=time-at;return age>=0&&age<=window?at:float.NaN;}
    }
    // Port of the source's captured-melee animation (character-animation sampleCharacter for the four captured weapons,
    // melee-motion, melee-block-motion, the mocap layers, primary-melee-motion footwork, hit-reaction-motion, the ground pass,
    // pose transitions and the final floor guard). Presentation only: never reads or writes authority state.
    // Native layers the source draws procedurally (rolls, E specials, knockdown recovery, source motions) are applied by
    // CapturedAnimation through `overlay`, between the action layers and the ground pass, exactly where the source runs its own.
    public sealed class MeleeMotion
    {
        public readonly MeleeRig Rig=new MeleeRig();
        private readonly MeleeGait gait=new MeleeGait();
        // The gait under the action layers (its stance inputs, MeleeKitMotion.Stance).
        public MeleeGait Gait=>gait;
        private readonly float[] row=new float[CapturedClip.Channels],rowB=new float[CapturedClip.Channels],rowC=new float[CapturedClip.Channels];
        private readonly MeleeCapture.RecoveryCache recovery=new MeleeCapture.RecoveryCache();private readonly MeleeRig fitScratch=new MeleeRig();
        private readonly MeleeCapture.WristHistory wrists=new MeleeCapture.WristHistory();
        private readonly Vector3[] keptAxes={Vector3.UnitX,Vector3.UnitX};
        private readonly Vector3[] entryAnkles=new Vector3[2];
        public string State{get;private set;}="idle";
        /// <summary>
        /// The blade's own clock for the captured cut posed this frame (presentation reads it: the slash crescent samples the
        /// blade by it): the swing's start, the age the cut was posed at (after the hit-stop hold and an air attack's landing
        /// freeze, so it runs behind the replicated window while the blade does), and the windup and window it was posed with.
        /// CutAge is NaN when this frame posed no cut. Read only; nothing in the pose depends on it.
        /// </summary>
        public float CutAt=float.NaN,CutAge=float.NaN,CutWindup,CutWindow;
        public static bool Handles(string spec)=>MeleeSet.For(spec)!=null;
        public const float GuardHold=2.4f,GuardFade=.8f,HoldRelease=.22f,StepReach=.475f;
        public static float Recovery(string spec)=>spec=="greatsword"?.5f:spec=="daggers"?.15f:.3f;
        private static readonly Dictionary<string,float> WeaponWeight=new Dictionary<string,float>{{"siren",4},{"daggers",2},{"sword",5},{"shield",8},{"greatsword",12},{"pistol",2},{"rifle",7},{"machinegun",10},{"fire",4},{"water",4},{"lightning",3},{"darkness",5},{"leet",3},{"emperor",1},{"ansatsuken",1}};

        public struct Attack{public float Age,Windup,Window,Recovery;public bool Active;}
        public static Attack AttackPose(MeleeView p,float time)
        {
            float rate=p.AttackRate>0?p.AttackRate:1,age=Math.Max(0,time-p.LastAttack),windup=!float.IsNaN(p.SwingAt)?Math.Max(.025f,p.SwingAt-p.LastAttack):Math.Max(.025f,p.Windup);
            float window=!float.IsNaN(p.SwingUntil)&&!float.IsNaN(p.SwingAt)?p.SwingUntil-p.SwingAt:.16f/rate,recoverySeconds=RecoverySeconds(p,windup,window);
            return new Attack{Age=age,Windup=windup,Window=window,Recovery=recoverySeconds,Active=age<windup+window+recoverySeconds&&!p.RollCancelled(time)};
        }
        // The real recovery (Unity-owned, docs/MELEE-FEEL.md): while the swing's commitment is replicated, its end (the authority's
        // AttackEnd, held at least the weapon interval) less the windup and window, never shorter than the source's Recovery. A held
        // swing then reaches the guard as the next one starts instead of standing there for 0.08-0.10 s (shield, greatsword).
        public static float RecoverySeconds(MeleeView p,float windup,float window)
        {
            float rate=p.AttackRate>0?p.AttackRate:1,source=Recovery(p.Spec)/rate;
            return p.Commit&&Math.Abs(p.CommitAt-p.LastAttack)<.002f?Math.Max(source,p.CommitUntil-p.LastAttack-windup-window):source;
        }

        // ---- presented yaw (motion-quality presentedYaw) ----
        // A committed swing's facing eases over TurnBlend when it starts away from the shown facing (Unity-owned,
        // docs/MELEE-FEEL.md): a swing begun after the camera turned 40 deg snapped the body in one frame, a 66 m/s blade-tip pop.
        // The windup's own turn toward the aim or the lock (NativeMeleeFeel.TurnRate) is then followed as it comes, at most
        // TurnFollow x that rate: no lag or second easing on a 720 deg/s turn (restarting the blend every tick trailed it by
        // up to 35 deg and popped at the hit), a 20 Hz replicated heading glides between its snapshots, and a chain that starts from
        // the last swing's heading carries straight on. An entry blend still running when the strike starts finishes over
        // StrikeBlend, so the strike shows the authority's heading.
        public const float TurnBlend=.1f,TurnFollow=1.6f,StrikeBlend=.04f;
        private string yawSource;private float yawFrom,yawAt=-1e9f,yawLast,yawTime,yawSpan=TurnBlend,yawFollow,yawOffset,yawCommitAt=float.NaN;private bool yawHas,yawStrike;
        public float PresentedYaw(MeleeView p,float time,float input)
        {
            bool hit=p.Reaction&&p.HitStunUntil>time&&p.ReactionAt<=time,commit=!hit&&p.Committed(time),roll=!hit&&!commit&&p.Rolling(time);
            string source=hit?"hit":commit?"commit":roll?"roll":"input";float target=hit?p.ReactionYaw:commit?p.CommitYaw:roll?p.Yaw:input;
            if(!yawHas||MeleeGait.TimeJumped(yawTime,time)){yawHas=true;yawSource=source;yawFrom=target;yawAt=-1e9f;yawLast=target;yawTime=time;yawFollow=target;yawOffset=0;yawCommitAt=commit?p.CommitAt:float.NaN;yawStrike=false;}
            float now=Math.Max(time,yawTime),dt=now-yawTime,yaw=target;
            bool strike=!float.IsNaN(p.SwingAt)&&!float.IsNaN(p.SwingUntil)&&time>=p.SwingAt&&time<p.SwingUntil;
            if(commit)
            {
                // A new commitment eases from the shown facing; its turn is then followed at up to TurnFollow x the weapon's rate.
                float rate=NativeMeleeFeel.TurnRate(p.Spec)*TurnFollow*(float)Math.PI/180;
                if(source!=yawSource||p.CommitAt!=yawCommitAt){yawSource=source;yawCommitAt=p.CommitAt;yawFollow=target;yawOffset=MeleeMath.Wrap(yawLast-target);yawAt=now;yawSpan=TurnBlend;yawStrike=false;}
                else yawFollow+=MeleeMath.Clamp(MeleeMath.Wrap(target-yawFollow),-rate*dt,rate*dt);
                float u=MeleeMath.Clamp((now-yawAt)/yawSpan,0,1),left=yawOffset*(1-u)*(1-u)*(1-u);
                if(strike&&!yawStrike){yawStrike=true;if(Math.Abs(left)>1e-4f){yawOffset=left;yawAt=now;yawSpan=StrikeBlend;}}
                u=MeleeMath.Clamp((now-yawAt)/yawSpan,0,1);yaw=yawFollow+yawOffset*(1-u)*(1-u)*(1-u);
            }
            else
            {
                if(source!=yawSource){yawSource=source;yawFrom=yawLast;yawAt=now;yawCommitAt=float.NaN;}
                float u=(now-yawAt)/.12f;
                if(u<1&&!strike)yaw=yawFrom+MeleeMath.Wrap(target-yawFrom)*(1-(1-u)*(1-u)*(1-u));
                yawFollow=yaw;yawOffset=0;
            }
            yawLast=yaw;yawTime=now;return yaw;
        }

        // ---- melee-guard carry hold and readiness ----
        private bool holdHas;private float holdValue,holdTime,holdGuard,holdAt,holdCarry,holdReady;
        private float RunCarry()=>MeleeMath.Ease((Rig.Motion-.06f)/.45f);
        private static float Readiness(MeleeView p,float time)
        {
            float rate=p.AttackRate>0?p.AttackRate:1,windup=!float.IsNaN(p.SwingAt)?Math.Max(.025f,p.SwingAt-p.LastAttack):Math.Max(.025f,p.Windup),window=!float.IsNaN(p.SwingUntil)&&!float.IsNaN(p.SwingAt)?p.SwingUntil-p.SwingAt:.16f/rate;
            float end=float.NegativeInfinity;void Consider(float start,float stop){if(start<=time)end=Math.Max(end,Math.Min(stop,time));}
            Consider(p.LastAttack,p.LastAttack+windup+window+RecoverySeconds(p,windup,window));if(p.ActionKind!=null)Consider(p.ActionAt,p.ActionUntil);if(!float.IsNaN(p.LastBlock))Consider(p.LastBlock,p.LastBlock+.3f);
            return float.IsNegativeInfinity(end)?0:1-MeleeMath.Ease((time-end-GuardHold)/GuardFade);
        }
        private void CarryHold(float target,float time,MeleeView p)
        {
            if(!holdHas||MeleeGait.TimeJumped(holdTime,time)){holdHas=true;holdValue=target;holdTime=time;holdGuard=1-target;holdAt=time>=p.LastAttack?p.LastAttack:float.NaN;holdCarry=target;holdReady=1-target;}
            else
            {
                if(holdAt!=p.LastAttack&&time>=p.LastAttack){holdAt=p.LastAttack;holdCarry=holdValue;holdReady=holdGuard;}
                float dt=MeleeMath.Clamp(time-holdTime,0,.35f);holdTime=Math.Max(time,holdTime);holdValue+=(target-holdValue)*(1-(float)Math.Exp(-dt*6));
            }
            holdGuard=(1-holdValue)*Readiness(p,time);
        }
        // meleeReady: the carry weight shown (the guard and carry poses are covered by the captured layers).
        private float MeleeReady(MeleeView p,float time){CarryHold(RunCarry(),time,p);return holdValue;}
        private float EntryGuard(MeleeView p)=>holdHas&&holdAt==p.LastAttack?holdReady:1;
        private float PhraseEntry(MeleeView p,float windup){float b=Math.Min(.065f,windup);return b+Math.Max(0,Math.Min(.85f*windup,Math.Max(.16f,.6f*windup))-b)*(1-EntryGuard(p));}

        // ---- primary-melee-motion: phase, hit-stop and footwork ----
        private static readonly float[] Keys={0,.55f,1,1.28f,1.65f,2,2.3f,2.65f,3},SpinTurn={0,-.35f,-.6f,.9f,2.4f,3.9f,5.0f,5.9f,MeleeMath.TAU};
        private static readonly float SpinFrom=MeleeMath.PoseCurve(Keys,SpinTurn,1.1f);
        private static float Herm(float s,float p0,float p1,float t0,float t1){float s2=s*s,s3=s2*s;return p0+(p1-p0)*(3*s2-2*s3)+t0*(s3-2*s2+s)+t1*(s3-s2);}
        public static float Phase(Attack a,float recoverySeconds,float age,float drag,bool accelerate)
        {
            if(age<a.Windup){float s=Math.Max(0,age)/a.Windup;return accelerate?Herm(s,0,1,0,Math.Min(3,a.Windup/Math.Max(.01f,a.Window))):s;}
            if(age<a.Windup+a.Window)return 1+(age-a.Windup)/a.Window;
            float u=Math.Min(1,(age-a.Windup-a.Window)/Math.Max(.01f,recoverySeconds)),m=Math.Min(accelerate?3:2.1f,recoverySeconds/Math.Max(.01f,a.Window));
            if(drag>0){float v=.35f/(1-drag);return u<drag?Herm(u/drag,2,2.65f,m*drag,v*drag):Herm((u-drag)/(1-drag),2.65f,3,v*(1-drag),0);}
            return 2+u*(m+u*((3-2*m)+u*(m-2)));
        }
        public static void Describe(string spec,int index,out int sign,out bool spin,out bool overhead)
        {
            bool dagger=spec=="daggers",heavy=spec=="greatsword",shield=spec=="shield",reverse=dagger||heavy||shield?index%2==1:index%3==1;
            sign=reverse?-1:1;overhead=!heavy&&!dagger&&!shield&&index%3==2;spin=dagger&&index%4==3;
        }
        private static readonly Dictionary<string,float[]> HitStopKeys=new Dictionary<string,float[]>{{"greatsword",new[]{.075f,.1f,.09f}},{"sword",new[]{.05f,.18f,.08f}},{"shield",new[]{.06f,.14f,.08f}},{"daggers",new[]{.028f,.3f,.05f}}};
        private bool gateHas,gateOn;private float gateAttack,gateAt,gateFrom;public const float CatchUpRate=1.3f;
        public MeleeImpacts Impacts;
        // The native melee hit layer (Core/NativeHitReact), run after the reactions and before the ground pass; null outside
        // the arena rigs (and in the source parity checks), which keep the source port exactly.
        public NativeHitLayer HitLayer;
        // The jump layer (Core/NativeJumpMotion), after the action layers and before the hit layer; null where HitLayer is.
        public NativeJumpMotion JumpLayer;
        // The pack's directional travel carry (Core/NativeLocomotionCarry); null keeps the captured walk and run (source parity).
        public NativeLocomotionCarry Locomotion;
        // The per-weapon sprint carry (Core/NativeSprintMotion), before the jump layer; null where JumpLayer is.
        public NativeSprintMotion SprintLayer;
        private float HitStop(MeleeView p,Attack a)
        {
            if(!HitStopKeys.TryGetValue(p.Spec,out var k)||string.IsNullOrEmpty(p.Id)||Impacts==null)return 0;float t=p.LastAttack+a.Age,h=Impacts.HitAt(p.Id,t,.5f);
            if(float.IsNaN(h)||h<p.LastAttack+a.Windup-.03f||h>p.LastAttack+a.Windup+a.Window+.05f)return 0;
            if(!gateHas||gateAttack!=p.LastAttack||gateAt!=h||t<gateFrom){gateHas=true;gateAttack=p.LastAttack;gateAt=h;gateFrom=t;gateOn=t-h<=.08f;}
            if(!gateOn)return 0;float hold=k[0],slow=k[1],u=t-gateFrom,max=hold*(1-slow);
            // The time lost in the hold is caught up at most CatchUpRate x (Unity-owned, docs/MELEE-FEEL.md), over the
            // follow-through if need be: the source's .05-.09 s catch-up raced the retimed sweep, the greatsword's blade to 180 m/s.
            float latest=p.LastAttack+a.Windup+a.Window+.8f*a.Recovery-gateFrom-hold,catchUp=Math.Max(k[2],Math.Min(1.875f*max/(CatchUpRate-1),latest));
            return u<=0?0:u<hold?u*(1-slow):u<hold+catchUp?max*(1-MeleeMath.Ease((u-hold)/catchUp)):0;
        }
        private static float Arc(float u)=>(float)Math.Sin(Math.PI*u);
        private static float Travel(MeleeView p,float at,float windup,float window,float time)
        {
            float stepAt=p.Commit?p.StepAt:at+windup*.55f,stepUntil=p.Commit?p.StepUntil:at+windup+window*.8f;
            float distance=p.Commit?p.StepDistance:p.Spec=="daggers"?.85f:p.Spec=="sword"?.65f:p.Spec=="shield"?.55f:.45f;
            return distance*MeleeMath.Smooth((time-stepAt)/Math.Max(.01f,stepUntil-stepAt));
        }
        // The swing's footwork (Unity-owned, docs/MELEE-FEEL.md "Footwork"): Core/MeleeFootwork plans real steps over the
        // simulated travel from the feet as shown when the swing began (a chain's recovery step, a run's stride), and pins every
        // foot on the floor in world space, so none slides: the lead steps into the cut and plants in the hit window, the rear
        // pushes off and follows with a recovery step, long steps stride first. The daggers' hopped spin keeps its own hop.
        public const float FootLand=.1f,SpinHop=.12f;
        private void Feet(MeleeView p,Attack a,float phase,int sign,bool heavy,bool dagger,bool overhead,bool spin,float turn,float weight,bool seat)
        {
            var r=Rig;float now=p.LastAttack+a.Age,stepUntil=p.Commit?p.StepUntil:p.LastAttack+a.Windup+a.Window*.8f;
            float travel=Travel(p,p.LastAttack,a.Windup,a.Window,now),full=Travel(p,p.LastAttack,a.Windup,a.Window,stepUntil);
            var st=MeleeStance.For(p.Spec);bool leadingLeft=sign>0,leadIsLead=leadingLeft==st.LeadLeft;
            float lfx=leadIsLead?st.LeadX:st.RearX,lfz=leadIsLead?st.LeadZ:st.RearZ,lft=leadIsLead?st.LeadToe:st.RearToe,rfx=leadIsLead?st.RearX:st.LeadX,rfz=leadIsLead?st.RearZ:st.LeadZ,rft=leadIsLead?st.RearToe:st.LeadToe;
            float lx=(leadingLeft?1:-1)*Math.Abs(lfx),rx=(leadingLeft?-1:1)*Math.Abs(rfx),lyaw0=(leadingLeft?1:-1)*lft,ryaw0=(leadingLeft?-1:1)*rft;
            Vector3? lin=weight<1?entryAnkles[leadingLeft?0:1]:(Vector3?)null,rin=weight<1?entryAnkles[leadingLeft?1:0]:(Vector3?)null;
            if(spin)
            {
                // A hopped turn: the feet hold through the windup, circle about the body in the air and land back on the stance.
                // The feet turn only while they are clear of the floor (Unity-owned, docs/MELEE-FEEL.md "Footwork"): they rise
                // and land straight up and down, completing their turn in the air while the hips finish theirs over them.
                float u=MeleeMath.Clamp((phase-1.1f)/1.75f,0,1),b=MeleeMath.Ease((u-.15f)/.7f),hop=Arc(u),air=MeleeMath.Ease((u-.12f)/.2f),rot=air*(SpinFrom+(MeleeMath.TAU-SpinFrom)*Math.Max(b,(turn-SpinFrom)/(MeleeMath.TAU-SpinFrom)));
                Vector3 body=r.P[CapturedRig.Body];float cxs=body.X,czs=body.Z,c=(float)Math.Cos(rot),sn=(float)Math.Sin(rot);body.Y+=.045f*hop;r.P[CapturedRig.Body]=body;
                Vector3 Place(float x,float z){float ox=x-cxs,oz=z-travel*(1-air)-czs;return new Vector3(cxs+ox*c+oz*sn,-.795f+SpinHop*hop,czs-ox*sn+oz*c);}
                // From a shown start each foot holds where it stood until the hop takes it onto the stance.
                Vector3 l0=footHas?footStart[leadingLeft?0:1]:new Vector3(lx,FLOOR,lfz),r0=footHas?footStart[leadingLeft?1:0]:new Vector3(rx,FLOOR,rfz);float down=1-MeleeMath.Ease(Math.Max(0,a.Age)/FootLand);
                Vector3 sl=Place(l0.X+(lx-l0.X)*air,l0.Z+(lfz-l0.Z)*air),sr=Place(r0.X+(rx-r0.X)*air,r0.Z+(rfz-r0.Z)*air);sl.Y+=Math.Max(0,l0.Y-FLOOR)*down;sr.Y+=Math.Max(0,r0.Y-FLOOR)*down;
                sl=Reachable(leadingLeft,sl);sr=Reachable(!leadingLeft,sr);float spinYaw=MeleeMath.TAU*b;stepped=true;
                if(footHas){r.StepFoot(leadingLeft,sl,lyaw0+spinYaw,.3f*hop,-.14f*hop,1,sl);r.StepFoot(!leadingLeft,sr,ryaw0+spinYaw,.3f*hop,-.14f*hop,1,sr);}
                else{r.StepFoot(leadingLeft,sl,lyaw0+spinYaw,.3f*hop,-.14f*hop,weight,lin);r.StepFoot(!leadingLeft,sr,ryaw0+spinYaw,.3f*hop,-.14f*hop,weight,rin);}return;
            }
            // The steps (Core/MeleeFootwork): every foot on the floor pinned where it stands, the others on real steps timed to
            // the simulated travel; then the hips down into the plant and low enough that each grounded foot is reached.
            stepped=true;stepHips[0]=r.ChainPoint(CapturedRig.LHip,-1);stepHips[1]=r.ChainPoint(CapturedRig.RHip,-1);footwork.Pose(now,r.GroupPosition,r.GroupYaw,stepHips,stepPose,p.Grounded?0:Math.Max(0,r.GroupPosition.Y-footFloor));
            if(seat){PlantDrop(p,a,weight,Lunge(p,full));SeatSteps(weight);}
            for(int i=0;i<2;i++)
            {
                bool left=i==0;var fp=stepPose[i];Vector3 target=fp.Contact?fp.Ankle:Reachable(left,fp.Ankle);
                r.StepFoot(left,target,fp.Yaw,fp.Pitch,fp.Toe,footHas?1:weight,footHas?target:(weight<1?entryAnkles[i]:(Vector3?)null));
            }
        }
        // The hips drop into the stepping foot's plant (a stationary swing's hit) and rise again over the recovery: deeper and
        // held longer for the greatsword, deeper still into a lunge.
        private void PlantDrop(MeleeView p,Attack a,float weight,float lunge)
        {
            var t=MeleeFootwork.For(p.Spec);float now=p.LastAttack+a.Age,plant=footwork.PlantAt,depth=t.Drop*(1+.6f*lunge);
            if(float.IsNaN(plant)){plant=p.LastAttack+a.Windup;depth*=.5f;}
            float rise=Math.Max(.08f,t.DropRise*a.Recovery),drop=depth*MeleeMath.Ease((now-(plant-.07f))/.09f)*(1-MeleeMath.Ease((now-plant-t.DropHold)/rise));
            if(drop<=1e-5f)return;Vector3 b=Rig.P[CapturedRig.Body];b.Y-=drop*weight;Rig.P[CapturedRig.Body]=b;
        }
        // Under a captured cut the body lowers until each foot reaches its target within STEP_REACH: a foot on the floor counts
        // fully, a lifted one as it leaves the floor and as it comes down. The hips rise back at most SeatRise (Unity-owned,
        // docs/MELEE-FEEL.md): as a step's feet plant the drop could fall 7 cm in a frame, a hip pop that also snapped a low
        // blade (lowering stays immediate, so the feet still reach).
        private void SeatSteps(float weight)
        {
            float drop=0;
            for(int i=0;i<2;i++)
            {
                var fp=stepPose[i];float k=fp.Contact?1:fp.Landing;if(k<=0)continue;Vector3 t=fp.Ankle;
                Vector3 hip=Rig.ChainPoint(i==0?CapturedRig.LHip:CapturedRig.RHip,-1);
                float h=Math.Min((float)Math.Sqrt((hip.X-t.X)*(hip.X-t.X)+(hip.Z-t.Z)*(hip.Z-t.Z)),.92f*StepReach),need=hip.Y-t.Y-(float)Math.Sqrt(StepReach*StepReach-h*h);
                if(need>0)drop=Math.Max(drop,Math.Min(MeleeFootwork.SeatMax,need)*k);
            }
            float now=swingTime;drop*=weight;
            if(seatAt==swingNow.At&&now>seatTime&&now-seatTime<.1f)drop=Math.Max(drop,seatDrop-SeatRise*(now-seatTime));
            seatAt=swingNow.At;seatTime=now;seatDrop=drop;
            Vector3 b=Rig.P[CapturedRig.Body];b.Y-=drop;Rig.P[CapturedRig.Body]=b;
        }
        private const float FLOOR=MeleeRig.FLOOR;
        // 0 for a default step, 1 for a lock-on opener's lunge (OpenerLunge, 0.9 m or more past the weapon's default step); a
        // chained lock-on step past the default reads as part of one.
        public static float Lunge(MeleeView p,float full)=>p.Commit?MeleeMath.Clamp((full-NativeMeleeFeel.DefaultStep(p.Spec))/.9f,0,1)*(p.OpenerLunge?1:.6f):0;
        // The torso leans into a lunge's travel (most as the lead foot plants, easing out through the recovery), and a swing
        // entered from a sprint (SprintAttack) carries the run's momentum: the body pitches on over the planted feet as the
        // authority stops it dead, then rights itself through the hit window. A sprint attack's take already drives its trunk
        // into the lunge (SprintCut), so it keeps only the plant's lean (stacked, the trunk pitched past 50-90 deg).
        public const float LungeLean=.3f,MomentumLean=.24f,MomentumCarry=.06f;
        private void StepLean(MeleeView p,Attack a,float phase)
        {
            float now=p.LastAttack+a.Age,stepUntil=p.Commit?p.StepUntil:p.LastAttack+a.Windup+a.Window*.8f,full=Travel(p,p.LastAttack,a.Windup,a.Window,stepUntil),age=Math.Max(0,a.Age);
            bool take=SprintSwing(p);float f=full>.01f?Travel(p,p.LastAttack,a.Windup,a.Window,now)/full:0,lean=take?0:LungeLean*Lunge(p,full)*MeleeMath.Ease(f/.7f)*(1-MeleeMath.Ease((phase-1.8f)/.9f));
            float carry=p.SprintAttack&&!take?MeleeMath.Ease(age/.06f)*(1-MeleeMath.Ease((age-.06f)/(a.Windup+a.Window))):0;
            // Every step leans the trunk into its cut as the lead foot plants (Unity-owned, docs/MELEE-FEEL.md "Footwork"), and
            // the hips lead the shoulders into the strike: the pelvis turns toward the blade's sweep ahead of the chest, which
            // keeps the capture's facing, and gives the turn back through the window.
            float plant=footwork.PlantAt;
            if(!float.IsNaN(plant)&&full>MeleeFootwork.StandStep)lean+=StepLeanBase*Math.Min(1.4f,full/NativeMeleeFeel.DefaultStep(p.Spec))*MeleeMath.Ease((now-(plant-.12f))/.12f)*(1-MeleeMath.Ease((now-plant-.04f)/Math.Max(.1f,.5f*a.Recovery)));
            HipsLead(p,a,now);
            lean+=MomentumLean*carry;if(lean<=1e-5f)return;
            Rig.AddE(CapturedRig.Body,.25f*lean,0,0);Rig.AddE(CapturedRig.Spine,.75f*lean,0,0);
            Vector3 b=Rig.P[CapturedRig.Body];b.Z+=MomentumCarry*carry;Rig.P[CapturedRig.Body]=b;
        }
        public const float StepLeanBase=.07f,HipLead=.14f;
        private void HipsLead(MeleeView p,Attack a,float now)
        {
            var set=MeleeSet.For(p.Spec);if(set==null)return;float dir=MeleeSwing.VisibleSweep(set,CutClip(set,p.AttackIndex)).Direction,hit=p.LastAttack+a.Windup;
            float lead=HipLead*(p.Spec=="greatsword"?1.3f:1)*MeleeMath.Smooth((now-(hit-.5f*a.Windup))/Math.Max(.03f,.5f*a.Windup))*(1-MeleeMath.Smooth((now-hit-.4f*a.Window)/Math.Max(.05f,.6f*a.Window+.3f*a.Recovery)));
            if(lead<=1e-4f)return;Quaternion pelvis=Rig.Q[CapturedRig.Pelvis],turn=MeleeMath.AxisAngle(MeleeMath.Y,-dir*lead);
            Rig.SetQ(CapturedRig.Pelvis,turn*pelvis);Rig.SetQ(CapturedRig.Spine,MeleeMath.Inverse(pelvis)*MeleeMath.Inverse(turn)*pelvis*Rig.Q[CapturedRig.Spine]);
        }
        public const float SeatRise=1.2f;private float seatAt=float.NaN,seatTime,seatDrop;
        // A lifted foot the hip cannot reach is drawn toward the hip (fading out as it comes down).
        private Vector3 Reachable(bool left,Vector3 target)
        {
            float k=MeleeMath.Clamp((target.Y+.795f)/.03f,0,1);if(k<=0)return target;
            Vector3 hip=Rig.ChainPoint(left?CapturedRig.LHip:CapturedRig.RHip,-1),to=target-hip;float d=to.Length();if(d<=StepReach)return target;
            return target+to*((StepReach/d-1)*k);
        }
        private void PhraseFootwork(MeleeView p,Attack a,float weight,float turn)
        {
            float recoverySeconds=a.Recovery;bool heavy=p.Spec=="greatsword";float drag=heavy?.4f:p.Spec=="shield"?.35f:0;
            Describe(p.Spec,Math.Max(0,(p.AttackIndex>0?p.AttackIndex:1)-1),out int sign,out bool spin,out bool overhead);
            float phase=Phase(a,recoverySeconds,a.Age-HitStop(p,a),drag,heavy);
            if(!spin)StepLean(p,a,phase);Feet(p,a,phase,sign,heavy,p.Spec=="daggers",overhead,spin,turn,weight,true);
        }

        // ---- mocap-layer.ts ----
        private bool speedHas;private float speedV,speedT;
        private float GaitSpeed(MeleeView p,float time)
        {
            float target=(float)Math.Sqrt(p.Vel.X*p.Vel.X+p.Vel.Z*p.Vel.Z);
            if(!speedHas||time<speedT-.1f||time-speedT>.35f){speedHas=true;speedV=target;speedT=time;}
            else{float dt=Math.Max(0,time-speedT);speedV+=(target-speedV)*(1-(float)Math.Exp(-dt/.25f));speedT=Math.Max(speedT,time);}
            return speedV;
        }
        private void Replant(MeleeView p)
        {
            if(!p.Grounded)return;
            for(int i=0;i<2;i++){if(!gait.GaitFoot(i==0,out Vector3 t,out float yaw,out float pitch,out float toe))return;Rig.LayerLeg(i==0,t,yaw,pitch,toe);}
        }
        private static CapturedClip CutClip(MeleeSet set,int attackIndex)=>set.Cut(attackIndex>0?attackIndex:1);
        private void Ready(MeleeSet set,MeleeView p,float time,float carry)
        {
            float still=1-MeleeMath.Ease(Rig.Motion/.2f);bool walking=carry>.001f&&set.Walk!=null&&set.Run!=null;float k=walking?MeleeMath.Ease((GaitSpeed(p,time)-3.2f)/2.8f):0;
            set.Idle.Sample(time,row);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(1,set.Fit,legs:0,root:still,arms:!(walking&&carry>=.999f)));
            if(still>.001f)Replant(p);
            if(walking)
            {
                float phase=MeleeMath.Frac(Rig.Stride/MeleeMath.TAU);
                // The pack's directional walk and run carries (Core/NativeLocomotionCarry) where the rig has them; else the captured pair.
                if(Locomotion!=null&&Locomotion.Carry(Rig,set,p,carry,k,phase,time)){}
                else{
                if(carry*k<.999f){set.Walk.Sample(set.Walk.Marks[0]+phase*set.Walk.Duration,row);DampTorso(set.Walk,row,WalkTorsoKeep);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(carry,set.Fit,legs:0,root:0));}
                if(k>.001f){set.Run.Sample(set.Run.Marks[0]+phase*set.Run.Duration,row);DampTorso(set.Run,row,RunTorsoKeep);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(carry*k,set.Fit,legs:0,root:0));}
                }
                SteadyTorso(carry,carry*k);
                State=k>.5f?"run":"walk";
            }
            else State="idle";
        }
        // ---- steady locomotion torso (Unity-owned; owner 2026-10-12: "notice how the body rocks when moving") ----
        // The captured walk/run torso twist and roll were authored against the capture's own pelvis. Over the procedural pelvis
        // (another amplitude and phase) and at 1.9-3x the authored cadence they rocked a sword runner's chest 52 deg and rolled
        // its shoulders 20 deg at the stride rate. Each clip keeps its cycle-mean carry posture and only part of its per-stride
        // swing on the spine, chest, neck and head; the spine counters most of the gait's pelvis twist and roll (as the raised
        // gun cancels them on the chest, GunMotion), so the shoulders keep a small counter-swing; the run's forward hunch lifts
        // (more when backpedalling) and the neck keeps the gaze. Every weight follows the carry and run blends, so walking,
        // running and stopping stay continuous. Presentation only: no timing, hitbox, movement or network change.
        public const float WalkTorsoKeep=.35f,RunTorsoKeep=.25f,PelvisTwistCounter=.75f,PelvisRollCounter=.8f,RunHunchLift=.26f,BackpedalLift=.22f;
        private Vector3 gaitPelvis;
        private static void DampTorso(CapturedClip clip,float[] f,float keep)
        {
            float[] mean=clip.CycleMean();
            for(int c=MeleeCapture.Spine;c<MeleeCapture.Head+3;c++)f[c]=mean[c]+(f[c]-mean[c])*keep;
        }
        private void SteadyTorso(float carry,float run)
        {
            if(carry<=.001f)return;
            // Running backward the forward run clip's hunch lifts further (the gait already leans the body back a little).
            float lift=(RunHunchLift+BackpedalLift*gait.Back)*run;
            // The gait pelvis is Euler XYZ (0, twist, roll): a share of its inverse, then the lift about the steadied frame.
            Quaternion counter=MeleeMath.AxisAngle(MeleeMath.Z,-PelvisRollCounter*gaitPelvis.Z*carry)*MeleeMath.AxisAngle(MeleeMath.Y,-PelvisTwistCounter*gaitPelvis.Y*carry)*MeleeMath.AxisAngle(MeleeMath.X,-lift);
            Rig.Premultiply(CapturedRig.Spine,counter);
            if(lift>1e-5f)Rig.Premultiply(CapturedRig.Neck,MeleeMath.AxisAngle(MeleeMath.X,lift));
        }
        // ---- chained swings (Unity-owned, docs/MELEE-FEEL.md) ----
        // The swing shown before this one, as last replicated. A swing pressed before that one's end (NativeMeleeFeel's early
        // chain) enters from its live follow-through or recovery, which carries on under the entry blend, straight into the
        // next windup: entering from the guard snapped the pose back toward it and hitched. A held swing that starts at the
        // last one's end keeps the guard entry (the follow-through has settled there).
        private struct SwingMemo{public bool Has,Air,Sprint,Plunge;public string Id,Spec;public int Index;public float At,Windup,Window,Recovery,Rate,End,Drawn;}
        private SwingMemo swingNow,swingPrev;private float swingTime=float.NaN;
        private readonly MeleeCapture.RecoveryCache chainRecovery=new MeleeCapture.RecoveryCache();private readonly Vector3[] chainKept={Vector3.UnitX,Vector3.UnitX};
        private void TrackSwing(MeleeView p,Attack a,float time)
        {
            if(float.IsNaN(swingTime)||time<swingTime-1e-4f||time-swingTime>.35f)swingNow.Has=swingPrev.Has=false;
            swingTime=time;if(p.LastAttack<-90||time<p.LastAttack)return;
            if(!swingNow.Has||swingNow.At!=p.LastAttack||swingNow.Id!=p.Id)
            {
                swingPrev=swingNow.Has&&swingNow.Id==p.Id&&swingNow.At<p.LastAttack?swingNow:default;
                swingNow=new SwingMemo{Has=true,Id=p.Id,At=p.LastAttack,Drawn=float.NegativeInfinity};Array.Copy(keptAxes,chainKept,2);
            }
            swingNow.Spec=p.Spec;swingNow.Index=p.AttackIndex;swingNow.Windup=a.Windup;swingNow.Window=a.Window;swingNow.Recovery=a.Recovery;
            swingNow.Rate=p.AttackRate>0?p.AttackRate:1;swingNow.End=p.LastAttack+a.Windup+a.Window+a.Recovery;
        }
        // Chained: the last swing was being drawn as this one began, before its end.
        private bool Chained(MeleeView p)=>swingPrev.Has&&!swingPrev.Plunge&&swingNow.Has&&swingNow.At==p.LastAttack&&swingPrev.Spec==p.Spec&&p.LastAttack<swingPrev.End-.002f&&p.LastAttack-swingPrev.Drawn<.1f;
        public static float ChainEntry(float windup)=>Math.Min(.85f*windup,Math.Max(.1f,.5f*windup));
        private void ChainBase(MeleeSet set,MeleeView p,float time)
        {
            var c=swingPrev;CapturedClip air=c.Air?NativeAirAttackRules.Clip(p.Spec):null,sprint=c.Sprint?NativeSprintAttackRules.Clip(p.Spec):null;var clip=air??sprint??CutClip(set,c.Index);float age=time-c.At;bool dagger=p.Spec=="daggers"&&air==null&&sprint==null;
            clip.Sample(air!=null?NativeAirAttackRules.ClipTime(clip,age,c.Windup,c.Window,c.Recovery):sprint!=null?NativeSprintAttackRules.ClipTime(clip,p.Spec,age,c.Windup,c.Window,c.Recovery):MeleeSwing.ClipTime(clip,age,c.Windup,c.Window,c.Recovery,dagger),row);
            if(age>c.Windup+c.Window&&set.Idle!=null&&!dagger)
            {
                set.Idle.Sample(c.At+c.Windup+c.Window+c.Recovery,rowB);
                MeleeCapture.FollowThrough(Rig,set,row,rowB,MeleeSwing.Settle(age,c.Windup,c.Window,c.Recovery),c.At,c.Index,c.Rate,chainRecovery,chainKept,fitScratch);
            }
            else MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0));
        }
        // ---- the feet as shown (world, the source frame), so a swing's footwork starts where they stood ----
        private readonly Vector3[] shownAnkles=new Vector3[2],footStart=new Vector3[2],shownBefore=new Vector3[2];private readonly float[] shownYaw=new float[2],shownPitch=new float[2];
        private float shownTime=float.NaN,shownBeforeTime=float.NaN,footAt=float.NaN;private string shownId;private bool footHas;
        private void KeepShownAnkles(MeleeView p,float time)
        {
            Quaternion g=Quaternion.CreateFromAxisAngle(Vector3.UnitY,Rig.GroupYaw);bool same=shownId==p.Id&&time>shownTime&&time-shownTime<.1f;
            for(int i=0;i<2;i++)
            {
                shownBefore[i]=shownAnkles[i];Vector3 ankle=Rig.ReadAnkle(i==0,out Quaternion q,out _),fwd=Vector3.Transform(Vector3.Transform(Vector3.UnitZ,q),g);
                shownAnkles[i]=Vector3.Transform(ankle,g)+Rig.GroupPosition;shownYaw[i]=(float)Math.Atan2(fwd.X,fwd.Z);shownPitch[i]=(float)Math.Asin(MeleeMath.Clamp(-fwd.Y,-1,1));
            }
            shownBeforeTime=same?shownTime:float.NaN;shownTime=time;shownId=p.Id;
        }
        private readonly MeleeFootwork footwork=new MeleeFootwork();private readonly MeleeFootwork.FootPose[] stepPose=new MeleeFootwork.FootPose[2];private readonly Vector3[] stepHips=new Vector3[2];private bool stepped;private float footFloor;
        private void StartFeet(MeleeView p,Attack a,float time)
        {
            if(footAt==p.LastAttack)return;
            footAt=p.LastAttack;footHas=shownId==p.Id&&time>shownTime&&time-shownTime<.1f;
            // The root height of the floor the swing starts on (a hop leaves it: MeleeFootwork measures the body's height over it).
            var track=JumpLayer?.Track??airTrack;footFloor=p.Grounded||!track.Has?Rig.GroupPosition.Y:track.Floor;
            // The footwork's plan (Core/MeleeFootwork) from the feet as shown, or from the cut's stance where none were.
            Describe(p.Spec,Math.Max(0,(p.AttackIndex>0?p.AttackIndex:1)-1),out int sign,out _,out _);
            float stepAt=p.Commit?p.StepAt:p.LastAttack+a.Windup*.55f,stepUntil=p.Commit?p.StepUntil:p.LastAttack+a.Windup+a.Window*.8f,full=Travel(p,p.LastAttack,a.Windup,a.Window,stepUntil);
            var swing=new MeleeFootwork.Swing{At=p.LastAttack,StepAt=stepAt,StepUntil=stepUntil,Distance=full,HitAt=p.LastAttack+a.Windup,HitUntil=p.LastAttack+a.Windup+a.Window,End=p.LastAttack+a.Windup+a.Window+a.Recovery,LeadLeft=sign>0,Spec=p.Spec,Sprint=SprintSwing(p)};
            Quaternion g=Quaternion.CreateFromAxisAngle(Vector3.UnitY,Rig.GroupYaw);var shown=new MeleeFootwork.Shown[2];
            for(int i=0;i<2;i++)
            {
                if(footHas)
                {
                    Vector3 v=!float.IsNaN(shownBeforeTime)&&shownTime>shownBeforeTime?(shownAnkles[i]-shownBefore[i])/(shownTime-shownBeforeTime):Vector3.Zero;
                    shown[i]=new MeleeFootwork.Shown{Ankle=new Vector3(shownAnkles[i].X,shownAnkles[i].Y-Rig.GroupPosition.Y,shownAnkles[i].Z),Velocity=v,Yaw=shownYaw[i],Pitch=shownPitch[i]};
                }
                else
                {
                    MeleeStance.For(p.Spec).Foot(i==0,out float x,out float z,out float yaw);Vector3 w=Vector3.Transform(new Vector3(x,0,z),g)+Rig.GroupPosition;
                    shown[i]=new MeleeFootwork.Shown{Ankle=new Vector3(w.X,FLOOR,w.Z),Yaw=Rig.GroupYaw+yaw};
                }
            }
            footwork.Begin(swing,Rig.GroupPosition,Rig.GroupYaw,shown[0],shown[1]);
            if(!footHas)return;
            Quaternion gi=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-Rig.GroupYaw);float travel=Travel(p,p.LastAttack,a.Windup,a.Window,time);
            for(int i=0;i<2;i++){Vector3 f=Vector3.Transform(shownAnkles[i]-Rig.GroupPosition,gi);f.Z+=travel;footStart[i]=f;}
        }
        private bool FollowupBase(MeleeSet set,MeleeView p)
        {
            var prev=CutClip(set,Math.Max(1,p.AttackIndex-1));prev.Sample(MeleeSwing.For(prev).SweepEnd,row);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0));return true;
        }
        private void Cut(MeleeSet set,MeleeView p,Attack a,float lag,float weight)
        {
            var clip=CutClip(set,p.AttackIndex);float rate=p.AttackRate>0?p.AttackRate:1,recoverySeconds=a.Recovery,age=a.Age-lag;bool dagger=p.Spec=="daggers";
            // Unity-owned swing timing (MeleeSwing): the authored sweep across the hit window, then the follow-through.
            var shape=MeleeSwing.For(clip);float ct=MeleeSwing.ClipTime(clip,age,a.Windup,a.Window,recoverySeconds,dagger);clip.Sample(ct,row);
            // A partial anticipation: the windup's pose eases toward the straight path between the start and sweep poses.
            float keep=set.AnticipationFor(p.AttackIndex);
            if(keep<1&&ct>shape.Start&&ct<shape.SweepStart){clip.Sample(shape.Start,rowB);clip.Sample(shape.SweepStart,rowC);MeleeCapture.BlendRowsTurning(rowB,rowC,(ct-shape.Start)/(shape.SweepStart-shape.Start),rowB);MeleeCapture.BlendRowsTurning(row,rowB,1-keep,row);}
            float settle=age>a.Windup+a.Window?MeleeSwing.Settle(age,a.Windup,a.Window,recoverySeconds):0;bool recovering=age>a.Windup+a.Window;
            if(recovering&&set.Idle!=null&&!dagger)
            {
                // Follow-through: the clip carries on past its sweep while the pose settles into the guard (no dead stop at the window's end).
                set.Idle.Sample(p.LastAttack+a.Windup+a.Window+recoverySeconds,rowB);
                MeleeCapture.FollowThrough(Rig,set,row,rowB,settle,p.LastAttack,p.AttackIndex,rate,recovery,keptAxes,fitScratch);
            }
            else MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(weight,set.Fit,MeleeClear.Turn,legs:0));
            Describe(p.Spec,Math.Max(0,(p.AttackIndex>0?p.AttackIndex:1)-1),out _,out bool spin,out _);
            float turn=spin?MeleeMath.Ease((age-a.Windup)/(a.Window+recoverySeconds))*MeleeMath.TAU:0;
            Rig.AddE(CapturedRig.Body,0,turn*weight,0);PhraseFootwork(p,a,weight,turn);
            // The Hurricane chip's full turn over the strike window.
            if(p.Hurricane&&!spin)Rig.AddE(CapturedRig.Body,0,MeleeMath.Ease((age-a.Windup)/a.Window)*MeleeMath.TAU*weight,0);
            State="cut"+(char)('A'+Array.IndexOf(set.Cuts,clip))+(recovering?" recovery":"");
        }
        // ---- the jump attack (Unity-owned, Core/NativeAirAttack, docs/MELEE-FEEL.md "Jump attacks") ----
        // A swing that starts while the pilot is presented airborne plays the weapon's air take instead of its ground cut: the
        // weapon raised overhead and held through the windup, the chop across the real hit window, then the follow-through.
        // A landing during it holds the pose low through the jump layer's deeper landing before the follow-through and the
        // settle into the guard carry on; ending in the air, it settles into the guard the jump's air pose rides on. The air
        // legs stay the jump layer's (no step footwork in the air).
        public readonly NativeAirAttackWatch AirWatch=new NativeAirAttackWatch();private readonly NativeJumpTrack airTrack=new NativeJumpTrack();
        private void WatchAir(MeleeView p,Attack a,float time)
        {
            // The jump layer's own track (observed last frame, after this sample's pose); without one, the rig's own reading.
            var t=JumpLayer?.Track;bool owned=p.Hp<=0||p.Juggle||p.Down||p.Rolling(time)||p.HitStunUntil>time||p.Action(time);
            if(t==null){airTrack.Observe(time,p.Grounded,Rig.GroupPosition.Y,owned);t=airTrack;}
            AirWatch.Observe(t,p.LastAttack>-90?p.LastAttack:-100,p.LastAttack+a.Windup+a.Window+a.Recovery,NativePlungeRules.ForwardFlag(p.AirAttack,p.SprintAttack,p.Plunge.Down&&p.Plunge.At==p.LastAttack),time,owned);
        }
        private bool AirSwing(MeleeView p)=>!PlungeSwing(p)&&AirWatch.Air&&AirWatch.SwingAt==p.LastAttack&&NativeAirAttackRules.Clip(p.Spec)!=null;
        // The jump layer's landing drop scale while a landed air attack is still being drawn (its end hands the deeper hips back
        // through the pose transition into the guard).
        private float HeavyLanding(MeleeView p,float time)=>airLive&&!float.IsNaN(AirWatch.Landed(time))?NativeAirAttackRules.LandScale(AirWatch.Strength):1;
        private bool airLive;
        // The greatsword's leaping sprint attack is being drawn: its footwork, not the jump layer, poses the hop (NativeJumpMotion.Leap).
        private bool leapLive;
        private void AirCut(MeleeSet set,MeleeView p,Attack a,float lag,float weight,float time)
        {
            var clip=NativeAirAttackRules.Clip(p.Spec);float rate=p.AttackRate>0?p.AttackRate:1,recoverySeconds=a.Recovery,landed=AirWatch.Landed(time),standY=Rig.P[CapturedRig.Body].Y;
            float age=a.Age-lag,landAge=float.IsNaN(landed)?float.NaN:AirWatch.ImpactAt-p.LastAttack;
            age-=NativeAirAttackRules.Frozen(age,landAge,a.Windup,a.Window,AirWatch.Strength);CutAge=age;
            clip.Sample(NativeAirAttackRules.ClipTime(clip,age,a.Windup,a.Window,recoverySeconds),row);
            bool recovering=age>a.Windup+a.Window;
            if(recovering&&set.Idle!=null)
            {
                set.Idle.Sample(p.LastAttack+a.Windup+a.Window+recoverySeconds,rowB);
                MeleeCapture.FollowThrough(Rig,set,row,rowB,MeleeSwing.Settle(age,a.Windup,a.Window,recoverySeconds),p.LastAttack,p.AttackIndex,rate,recovery,keptAxes,fitScratch);
            }
            else MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(weight,set.Fit,MeleeClear.Turn,legs:0));
            // The heavy landing: the take's air-borne hips come down to the stance's and sink a further Crouch while the trunk
            // folds over the planted chop as it holds, then rise with the settle (the jump layer's deeper landing rides on it).
            if(!float.IsNaN(landed))
            {
                float e=MeleeMath.Ease(landed/.05f)*(1-MeleeMath.Ease((landed-NativeAirAttackRules.Hold(AirWatch.Strength)-.04f)/.3f)),k=AirWatch.Strength*e;
                Vector3 b=Rig.P[CapturedRig.Body];b.Y=standY+(b.Y-standY)*(1-e)-NativeAirAttackRules.HeavyCrouch*k;Rig.P[CapturedRig.Body]=b;Rig.AddE(CapturedRig.Body,.12f*k,0,0);Rig.AddE(CapturedRig.Spine,.22f*k,0,0);
            }
            State="air attack"+(float.IsNaN(landed)?"":" landed")+(recovering?" recovery":"");
        }
        // ---- the plunging stab (Unity-owned presentation, Core/NativePlungeStab + NativePlungePose, docs/MELEE-FEEL.md "Plunging stab") ----
        // A swing the replicated data marks as the plunging stab (NativePlungeData.Down, never the airtime alone) plays the weapon's
        // blade-down keys on the replicated timeline (the .08 s windup's tuck, the committed drop, the plant at the impact, the
        // stuck hold, the pull-out timed to the recovery, the settle into the guard). It owns the legs (the jump layer stands aside
        // as for the greatsword's leap: JumpLayer.Leap); its planted blade is left in the floor (FloorGuard) until it comes free.
        public readonly NativePlungeWatch PlungeWatch=new NativePlungeWatch();private readonly MeleeRig plungeGuard=new MeleeRig();private bool plungeLive,plungeStuck;
        private void WatchPlunge(MeleeView p,float time)
        {
            bool owned=p.Hp<=0||p.Juggle||p.Down||p.Rolling(time)||p.HitStunUntil>time||p.Action(time);
            PlungeWatch.Observe(p.Plunge,p.Spec,Rig.GroupPosition.Y,p.Grounded,time,owned);
        }
        private bool PlungeSwing(MeleeView p)=>PlungeWatch.Is(p.LastAttack)&&!p.SprintAttack&&NativePlungePose.For(p.Spec)!=null;
        private static readonly int[] PlungeUpper={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        private void PlungeCut(MeleeSet set,MeleeView p,float time)
        {
            var w=NativePlungePose.For(p.Spec);var r=Rig;
            if(!PlungeWatch.Clock(time,out var c)){Ready(set,p,time,0);State="plunge recovery";return;}
            // The guard the windup leaves and the settle returns to: the weapon's captured idle at the swing's start (held still).
            set.Idle.Sample(p.LastAttack,rowB);bool guard=c.Phase==NativePlungePhase.Windup&&c.Tuck<.5f||c.Phase==NativePlungePhase.Settle;
            if(guard){MeleeCapture.Apply(r,set,rowB,CaptureOptions.Of(1,set.Fit,MeleeClear.Full,legs:0));plungeGuard.Copy(r);}
            var k=NativePlungePose.At(w,c,p.Spec,out float depth,out float wrench,out float g);
            NativePlungePose.Apply(r,set,w,k,rowB,depth,wrench,c.Landed);
            if(guard)
            {
                // The windup turns out of the guard into the raise (all of it, legs too, in the air); the settle turns the upper body
                // and the hips back to it over the planted soles (the legs re-solved onto the stance).
                if(c.Phase==NativePlungePhase.Windup){for(int i=0;i<MeleeRig.N;i++)r.SetQ(i,MeleeMath.Slerp(r.Q[i],plungeGuard.Q[i],g));r.P[CapturedRig.Body]=MeleeMath.Lerp(r.P[CapturedRig.Body],plungeGuard.P[CapturedRig.Body],g);}
                else
                {
                    foreach(int i in PlungeUpper)r.SetQ(i,MeleeMath.Slerp(r.Q[i],plungeGuard.Q[i],g));
                    r.SetQ(CapturedRig.Body,MeleeMath.Slerp(r.Q[CapturedRig.Body],plungeGuard.Q[CapturedRig.Body],g));r.SetQ(CapturedRig.Pelvis,MeleeMath.Slerp(r.Q[CapturedRig.Pelvis],plungeGuard.Q[CapturedRig.Pelvis],g));
                    r.P[CapturedRig.Body]=MeleeMath.Lerp(r.P[CapturedRig.Body],plungeGuard.P[CapturedRig.Body],g);
                    r.SolveLeg(true,k.LFoot,.12f,k.LPitch*(1-g),0,null,k.LKnee);r.SolveLeg(false,k.RFoot,-.12f,k.RPitch*(1-g),0,null,k.RKnee);r.LegsAuthored=true;
                }
            }
            r.MocapPosed=true;plungeStuck=c.Stuck;
            State=NativePlungeRules.Label(c.Phase);
        }
        // ---- the sprint attack (Unity-owned, Core/NativeSprintAttackVisuals, docs/MELEE-FEEL.md "Sprint attacks") ----
        // A swing the authority flags as entered from a sprint (Astra's NativeSprintAttack: grounded openers only, so the
        // greatsword's hop keeps it) plays the weapon's own running take instead of its first cut, on the swing's replicated
        // timing (NativeSprintAttackRules.ClipTime: its lead-in over the windup, its strike across the hit window, the daggers'
        // twin out and back, the follow-through into the guard over the recovery); the shield's bash drives the shield out at
        // the hit. The feet are the footwork's, the run's stride carrying into the strike; the greatsword leaps on the hop
        // (the jump layer leaves it to the footwork: NativeJumpMotion.Leap). The take carries its own lean (StepLean).
        private bool SprintSwing(MeleeView p)=>!AirSwing(p)&&!PlungeSwing(p)&&NativeSprintAttackRules.Plays(p.Spec,p.SprintAttack,false);
        private void SprintCut(MeleeSet set,MeleeView p,Attack a,float lag,float weight)
        {
            var clip=NativeSprintAttackRules.Clip(p.Spec);float rate=p.AttackRate>0?p.AttackRate:1,recoverySeconds=a.Recovery,age=a.Age-lag;
            clip.Sample(NativeSprintAttackRules.ClipTime(clip,p.Spec,age,a.Windup,a.Window,recoverySeconds),row);
            float bash=p.Spec=="shield"?NativeSprintAttackRules.Bash(age,a.Windup,a.Window,recoverySeconds):0;
            if(bash>0){row[MeleeCapture.Grip+2]+=NativeSprintAttackRules.BashReach*bash;row[MeleeCapture.Grip+1]+=NativeSprintAttackRules.BashLift*bash;}
            bool recovering=age>a.Windup+a.Window;
            if(recovering&&set.Idle!=null)
            {
                set.Idle.Sample(p.LastAttack+a.Windup+a.Window+recoverySeconds,rowB);
                MeleeCapture.FollowThrough(Rig,set,row,rowB,MeleeSwing.Settle(age,a.Windup,a.Window,recoverySeconds),p.LastAttack,p.AttackIndex,rate,recovery,keptAxes,fitScratch);
            }
            else MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(weight,set.Fit,MeleeClear.Turn,legs:0));
            // The shield's bash: the shield shoulder driven forward over the pitched chest as the body pushes on.
            if(bash>0){Rig.AddE(CapturedRig.Spine,NativeSprintAttackRules.BashPitch*bash*weight,NativeSprintAttackRules.BashTurn*bash*weight,0);Vector3 b=Rig.P[CapturedRig.Body];b.Z+=NativeSprintAttackRules.BashPush*bash*weight;Rig.P[CapturedRig.Body]=b;}
            PhraseFootwork(p,a,weight,0);
            State="sprint attack"+(recovering?" recovery":"");
        }
        private static readonly float[] BlockKeys={0,.13f,.36f},BlockHitKeys={0,.1f,.45f};private readonly float[] staggerKeys={0,0,0};
        private bool raiseHas;private float raiseSince,raiseTime;
        private float RaiseStart(MeleeView p,float time)
        {
            bool timed=p.BlockTime>0&&!float.IsNaN(p.LastBlock)&&p.LastBlock<=time&&time-p.LastBlock<.5f;
            if(!raiseHas||time<raiseTime&&MeleeGait.TimeJumped(raiseTime,time)){raiseHas=true;raiseSince=time-1;raiseTime=time;}
            else if(time-raiseTime>.2f)raiseSince=timed&&p.LastBlock>raiseTime?p.LastBlock:time;
            raiseTime=Math.Max(time,raiseTime);return timed?Math.Max(raiseSince,p.LastBlock):raiseSince;
        }
        private void Block(MeleeSet set,MeleeView p,float time)
        {
            MeleeReady(p,time);float since=RaiseStart(p,time),impact=Impacts!=null?Impacts.BlockAt(p.Id,time,.3f):float.NaN,still=1-MeleeMath.Clamp(Rig.Motion/.32f,0,1),legs=p.Grounded?still:0;
            set.Block.Sample(CapturedClip.Retime(BlockKeys,set.Block.Marks,time-since),row);
            if(set.BlockHit!=null&&!float.IsNaN(impact)&&time>=impact&&!(HitLayer!=null&&HitLayer.DrivesBlock(time)))
            {
                float u=time-impact,k=MeleeMath.Ease(u/.04f)*(1-MeleeMath.Ease((u-.35f)/.12f));
                if(k>.001f){set.BlockHit.Sample(CapturedClip.Retime(BlockHitKeys,set.BlockHit.Marks,u),rowB);MeleeCapture.BlendRows(row,rowB,k,row);}
            }
            MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(1,set.Fit,legs:0,root:legs));if(legs>.001f)Replant(p);State="block";
        }
        private void Stagger(MeleeSet set,MeleeView p,float time,float carry)
        {
            Ready(set,p,time,carry);if(set.Stagger==null)return;
            float at=p.Reaction&&p.ReactionAt<=time?p.ReactionAt:time,until=Math.Max(p.HitStunUntil,at+.05f),span=until-at,front=p.Reaction?Math.Min(1,Math.Max(.35f,-p.ReactionDir.Z*1.4f+.35f)):.6f;
            staggerKeys[1]=span*.4f;staggerKeys[2]=span;set.Stagger.Sample(CapturedClip.Retime(staggerKeys,set.Stagger.Marks,time-at),row);
            MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(.7f*front*MeleeMath.Ease((time-at)/.06f)*(1-MeleeMath.Ease((time-until+.1f)/.1f)),set.Fit,legs:0));Replant(p);
        }

        // ---- hit-reaction-motion.ts ----
        public static float HitImpulse(float t,float tp,float z,float rebound=1){if(t<=0)return 0;float x=t/tp,v=(float)(Math.Exp(z*(1-x))*Math.Sin(Math.PI/2*x));return x>2?v*rebound:v;}
        private void Nudge(int node,float x,float y,float z,bool local){Quaternion q=MeleeMath.Euler(x,y,z);if(local)Rig.Multiply(node,q);else Rig.Premultiply(node,q);}
        private bool React(MeleeView p,MeleeReaction h,float time,bool stunned)
        {
            var r=Rig;float until=stunned?Math.Max(h.Until,p.HitStunUntil):h.Until;if(time<h.At||time>=until)return false;
            float duration=Math.Max(.04f,until-h.At),age=time-h.At,u=age/duration,tp=Math.Min(.095f,duration*.26f),end=1-MeleeMath.Ease((u-.72f)/.28f),scale=stunned?1:.35f;Vector3 d=h.Direction;
            if(h.Parry){ParryMotion(p,age,age/Math.Min(duration,.95f),tp,end,h.Turn);return true;}
            float pelvis=HitImpulse(age,tp,.55f,.6f)*end*scale,chest=HitImpulse(age-.016f,tp,.45f,.6f)*1.1f*end*scale,head=HitImpulse(age-.033f,tp,.25f,.45f)*1.25f*end*(stunned?1:.6f),arms=HitImpulse(age-.045f,tp,.2f,.5f)*end*(stunned?1:0);
            float strength=pelvis*h.Strength,torso=chest*h.Strength;bool low=h.Zone==2,headHit=h.Zone==1;float side=h.Point.X>=0?1:-1;
            // A two-handed hold keeps its support hand on the grip through the recoil.
            bool twoHand=false;
            if(p.Spec!="shield"&&MeleeSet.For(p.Spec)!=null&&r.WeaponVisible){Vector3 grip=r.ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(r.Reached(MeleeRig.SupportGrip),r.ChainQuat(CapturedRig.RHand,-1));twoHand=Vector3.Distance(grip,r.ChainPoint(CapturedRig.LHand,-1))<.06f;}
            Vector3 b=r.P[CapturedRig.Body];b.X+=d.X*.05f*strength;b.Z+=d.Z*.05f*strength;b.Y-=(low?.16f:.07f)*strength;r.P[CapturedRig.Body]=b;
            r.AddE(CapturedRig.Body,d.Z*(low?.12f:headHit?.22f:.32f)*strength,0,0);r.AddE(CapturedRig.Body,0,0,-d.X*.25f*strength);r.AddE(CapturedRig.Body,0,h.Turn*strength,0);
            r.AddE(CapturedRig.Chest,0,h.Turn*.65f*torso,0);r.AddE(CapturedRig.Chest,d.Z*(low?.06f:.15f)*torso,0,0);
            r.AddE(CapturedRig.Head,d.Z*(headHit?.48f:.12f)*head*h.Strength,0,0);r.AddE(CapturedRig.Head,0,0,-d.X*(headHit?.4f:.1f)*head*h.Strength);
            float weight=WeaponWeight.TryGetValue(p.Spec,out float ww)?ww:5,heavy=1/(1+weight*.08f),a=arms*h.Strength*heavy,inertia=Math.Min(.92f,.55f+.03f*weight);
            float trunk=d.Z*(low?.12f:headHit?.22f:.32f)*strength+d.Z*(low?.06f:.15f)*torso;
            for(int i=0;i<2;i++)
            {
                bool left=i==0;float sign=left?1:-1;bool struck=h.Zone==(left?3:4);int arm=left?CapturedRig.LArm:CapturedRig.RArm,elbow=left?CapturedRig.LElbow:CapturedRig.RElbow,wrist=left?CapturedRig.LHand:CapturedRig.RHand;
                float dx=d.Z*.45f*a+(struck?.5f*a:0)-(stunned?0:.1f*a),dz=sign*(.22f+(struck?.3f:0))*a-d.X*.15f*a,bend=struck?.35f*a:-.3f*a;
                Nudge(arm,dx,0,dz,false);Nudge(elbow,bend,0,0,true);Nudge(wrist,-(trunk+dx+bend)*inertia,0,0,true);
            }
            if(twoHand)r.SupportHand(1);
            if(stunned)
            {
                // The feet stay planted (the knees take the body drop) and a small catch step follows the push.
                float plant=Math.Min(1,MeleeMath.Ease(age/.05f)*end),catchStep=MeleeMath.Ease((u-.10f)/.35f),lift=(float)Math.Sin(Math.PI*catchStep)*.085f;
                r.Foot(true,new Vector3(.18f+d.X*.10f,-.795f+(side>0?lift:0),.05f+d.Z*.13f*catchStep),h.Turn*.5f,0,plant);
                r.Foot(false,new Vector3(-.18f+d.X*.10f,-.795f+(side<0?lift:0),-.05f+d.Z*.13f*catchStep),h.Turn*.5f,0,plant);
            }
            return true;
        }
        private void ParryMotion(MeleeView p,float age,float u,float tp,float end,float turn)
        {
            var r=Rig;float Rise(float t)=>t/tp<.786f?HitImpulse(t,tp,.55f)/1.062f:1;
            float w=Rise(age)*end,limb=Math.Min(1,Rise(age-.025f)*end),slump=MeleeMath.Ease((u-.18f)/.25f),rebound=1-.55f*slump;bool heavy=p.Spec=="greatsword";
            Vector3 b=r.P[CapturedRig.Body];b.Y-=.12f*w;b.Z-=.065f*(heavy?1.25f:1)*w;r.P[CapturedRig.Body]=b;
            r.AddE(CapturedRig.Body,(-.30f*rebound+.16f*slump)*w,0,0);r.AddE(CapturedRig.Body,0,turn*w,0);r.AddE(CapturedRig.Chest,0,-.22f*w,0);r.AddE(CapturedRig.Head,(-.28f*rebound+.20f*slump)*w,0,0);
            if(heavy)r.BraceWeapon(new Vector3(-.1f-.02f*slump,.45f-.25f*slump,.35f-.05f*slump),MeleeMath.Euler(-1.75f+.55f*slump,-.4f+.1f*slump,-.3f+.1f*slump),limb);
            else{ParryHand(false,new Vector3(-.46f,.38f-.38f*slump,.03f),MeleeMath.Euler(-1.7f+.8f*slump,-.8f,-.55f),limb);ParryHand(true,new Vector3(.43f,.08f-.18f*slump,.10f),MeleeMath.Euler(-.4f,.4f,.5f),limb);}
            r.Foot(true,new Vector3(.19f,-.795f,.10f),.14f,0,Math.Min(1,w));
            r.Foot(false,new Vector3(-.22f,-.795f+(float)Math.Sin(Math.PI*MeleeMath.Ease(u/.35f))*.075f,-.18f*MeleeMath.Ease(u/.35f)),-.3f,0,Math.Min(1,w));
        }
        // hand() blends the wrist locally while the forearm turns; blend its group orientation instead.
        private void ParryHand(bool left,Vector3 target,Quaternion orientation,float weight)
        {
            if(weight<=0)return;var r=Rig;int wrist=left?CapturedRig.LHand:CapturedRig.RHand;Quaternion held=r.ChainQuat(wrist,-1);
            r.Hand(left,target,orientation,weight);Quaternion desired=r.ChainQuat(CapturedRig.Chest,-1)*orientation;held=MeleeMath.Slerp(held,desired,weight);
            r.SetQ(wrist,MeleeMath.Inverse(r.ChainQuat(MeleeRig.Parent(wrist),-1))*held);
        }
        // Light hit reactions over a native (non-captured-melee) pose: the source's impulse recoil of the body, chest and
        // head (hit-reaction-motion; the arms keep the native weapon pose, so a two-handed gun never loses its grip).
        public void ReactOver(CapturedRig.Pose pose,MeleeView p,float time)
        {
            var h=ReactionOf(p);if(h==null||h.Parry||time<h.At||time>=h.Until||p.HitStunUntil>time)return;
            Rig.ReadPose(pose);React(p,h,time,false);Rig.WritePose(pose);
        }
        private bool flinchHas;private float flinchUntil,flinchAt;
        private void FallbackFlinch(MeleeView p,float time)
        {
            float until=p.HitStunUntil;if(!flinchHas){flinchHas=true;flinchUntil=until;flinchAt=time;}else if(flinchUntil!=until||time<flinchAt||time-flinchAt>until-flinchAt+.35f){flinchUntil=until;flinchAt=time;}
            var h=flinch;h.Parry=false;h.At=flinchAt;h.Until=until;h.Yaw=p.Yaw;h.Direction=new Vector3(0,0,-1);h.Point=new Vector3(0,.1f,.2f);h.Zone=0;h.Turn=0;h.Strength=.6f;React(p,h,time,true);
        }
        private readonly MeleeReaction flinch=new MeleeReaction(),reaction=new MeleeReaction();
        private MeleeReaction ReactionOf(MeleeView p)
        {
            if(!p.Reaction)return null;var h=reaction;h.Parry=p.Parry;h.At=p.ReactionAt;h.Until=p.ReactionUntil;h.Yaw=p.ReactionYaw;h.Direction=p.ReactionDir;h.Point=p.ReactionPoint;h.Zone=p.ReactionZone;h.Turn=p.ReactionTurn;h.Strength=p.ReactionStrength;return h;
        }

        // ---- pose-transitions.ts finishPose ----
        private sealed class Stored{public readonly Quaternion[] Q=new Quaternion[MeleeRig.N];public Vector3 Position;}
        private bool trHas,trActive,trHold,trHasPrev,trParry;private string trId,trSpec,trAction,trPhase;private int trIndex;private float trReaction=float.NaN,trTime,trPrevTime,trAt,trDuration;
        private Stored trLast=new Stored(),trPrev=new Stored();private readonly Stored trOffset=new Stored(),trCurrent=new Stored(),trProjected=new Stored();
        private void CaptureTo(Stored s){Array.Copy(Rig.Q,s.Q,MeleeRig.N);s.Position=Rig.P[CapturedRig.Body];}
        private static bool ReactionFamily(string action)=>action=="juggle"||action=="down";
        // pose-transitions.ts DEV_GROUNDED: the fighter keys whose legs stand on the gait's planted feet.
        private static bool DevGrounded(string spec,string action)=>NativeFighterPose.Handles(spec)&&(action=="base"||action=="attack"||action=="jab"||action=="combo"||action=="electric"||action=="hadoken"||action=="charge"||action=="shinku"||action=="guard"||action=="block");
        private bool trHoldAll,trFeet;private float trU;private Action trBlend;
        // The decaying offset over the sampled pose (one cached delegate: no allocation while a transition holds the ankles).
        private void BlendTransition(){float u=trU;for(int i=0;i<MeleeRig.N;i++)Rig.Multiply(i,MeleeMath.Slerp(trOffset.Q[i],Quaternion.Identity,u));Rig.P[CapturedRig.Body]+=trOffset.Position*(1-u);}
        // The pose family (transitionAction); an attack's index and a knockdown's phase are kept beside it, unconcatenated.
        private static string TransitionAction(MeleeView p,float time,out int index,out string phase)
        {
            // A DEV fighter's strike tail lasts .8 s / rate (transitionAction's attackDuration outside the standard specs).
            var a=AttackPose(p,time);float rate=p.AttackRate>0?p.AttackRate:1,duration=Handles(p.Spec)?a.Windup+a.Window+a.Recovery:.8f/rate;index=0;phase=null;
            if(p.Juggle)return "juggle";if(p.Down){phase=p.DownPhase??"";return "down";}
            if(p.HitStunUntil>time)return "flinch";if(p.Rolling(time))return "roll";if(p.Guarding||p.BlockTime>0)return "block";if(p.Action(time))return p.ActionKind;
            if(time-p.LastAttack<duration&&!p.RollCancelled(time)){index=p.AttackIndex;return "attack";}
            return "base";
        }
        private static float TransitionDuration(MeleeView p,float time,string action,string phase,string previous,MeleeReaction reaction)
        {
            if(reaction!=null)return Math.Min(.075f,Math.Max(.02f,(Math.Max(reaction.Until,p.HitStunUntil)-time)*.28f));
            if(ReactionFamily(action))return action!="juggle"&&phase!="air"&&ReactionFamily(previous)?.08f:.09f;
            bool heavy=p.Spec=="greatsword";
            if(action=="attack")
            {
                float windup=!float.IsNaN(p.SwingAt)?Math.Max(.025f,p.SwingAt-p.LastAttack):Math.Max(.025f,p.Windup),d=Math.Min(.55f*windup,.09f);
                if(!float.IsNaN(p.SwingAt)&&time<(float.IsNaN(p.SwingUntil)?p.SwingAt:p.SwingUntil))d=Math.Max(0,Math.Min(d,p.SwingAt-time));return d;
            }
            if(action=="base")return previous=="attack"&&Handles(p.Spec)?(heavy?.28f:.22f):.13f;
            if(action=="block")return heavy?.11f:.09f;
            return .055f;
        }
        private void FinishPose(MeleeView p,float time)
        {
            string action=TransitionAction(p,time,out int index,out string phase);var h=ReactionOf(p);var reaction=h!=null&&time>=h.At&&time<Math.Max(h.Until,p.HitStunUntil)?h:null;
            float reactionAt=reaction!=null?reaction.At:float.NaN;bool parry=reaction!=null&&reaction.Parry;
            // The key (spec, family, attack index, knockdown phase, reaction impact) a transition blends on changing.
            void Keep(){trSpec=p.Spec;trAction=action;trIndex=index;trPhase=phase;trReaction=reactionAt;trParry=parry;}
            bool same=trSpec==p.Spec&&trAction==action&&trIndex==index&&trPhase==phase&&(trReaction==reactionAt||float.IsNaN(trReaction)&&float.IsNaN(reactionAt))&&trParry==parry;
            if(!trHas){trHas=true;trId=p.Id;Keep();trTime=trPrevTime=trAt=time;trDuration=0;CaptureTo(trLast);trHasPrev=false;trActive=false;trHold=false;return;}
            if(time<trTime||time-trTime>.35f||p.Id!=trId){trId=p.Id;Keep();trTime=trPrevTime=trAt=time;trActive=false;trHasPrev=false;CaptureTo(trLast);return;}
            CaptureTo(trCurrent);Stored incoming=null;float duration=0;
            if(!same)
            {
                duration=TransitionDuration(p,time,action,phase,trAction,reaction);incoming=trLast;
                // Into, out of and between the captured weapons' swings too (Unity-owned, docs/MELEE-FEEL.md): the first transition
                // frame held the last pose, a one-frame stop at every swing's start and end. The DEV fighters keep the source's.
                if(reaction==null&&(ReactionFamily(trAction)||Handles(p.Spec)&&(trAction=="attack"||action=="attack"))&&trHasPrev&&trTime>trPrevTime)
                {
                    // Continue the last frame's joint velocities to this frame's time, then keep only the remaining discontinuity.
                    float ratio=MeleeMath.Clamp((time-trTime)/(trTime-trPrevTime),0,2);
                    for(int i=0;i<MeleeRig.N;i++)trProjected.Q[i]=MeleeMath.Slerp(trLast.Q[i]*MeleeMath.Inverse(trPrev.Q[i]),Quaternion.Identity,1-ratio)*trLast.Q[i];
                    trProjected.Position=(trLast.Position-trPrev.Position)*ratio+trLast.Position;incoming=trProjected;
                }
            }
            if(incoming!=null)
            {
                // A DEV fighter's grounded keys (strings, specials, guard, base) keep the ankles where they are (holdAll).
                trHold=action=="base"&&trAction=="attack";trHoldAll=reaction==null&&DevGrounded(p.Spec,action)&&DevGrounded(p.Spec,trAction);
                // A captured swing's footwork starts on the feet as shown (Feet): the blend leaves them where the swing put them.
                // So does the end of a jump attack that landed: it hands its deep landing crouch to the guard on planted feet.
                trFeet=reaction==null&&Handles(p.Spec)&&(action=="attack"&&footHas&&footAt==p.LastAttack||action=="base"&&trAction=="attack"&&(AirWatch.Air&&AirWatch.SwingAt==p.LastAttack&&!float.IsNaN(AirWatch.Landed(time))||PlungeWatch.Is(p.LastAttack)&&MathUtil.Finite(PlungeWatch.ImpactAt)));Keep();trAt=time;trDuration=duration;trActive=duration>1e-4f;
                if(trActive){for(int i=0;i<MeleeRig.N;i++)trOffset.Q[i]=MeleeMath.Inverse(trCurrent.Q[i])*incoming.Q[i];trOffset.Position=incoming.Position-trCurrent.Position;}
            }
            if(trActive)
            {
                float u=MeleeMath.Ease((time-trAt)/trDuration);
                if(u>=1)trActive=false;
                else
                {
                    // Weighted by travel speed: a standing recovery keeps its joint-space leg blend, a run resuming keeps its planted feet.
                    // A captured swing's soles (Core/MeleeFootwork) neither slide nor tilt while the pose blends into or out of it.
                    trU=u;bool soles=trFeet||trHold&&Handles(p.Spec);
                    if((trHold||trHoldAll||trFeet)&&p.Grounded&&p.Hp>0)Rig.HoldAnkles(trBlend??=BlendTransition,trHoldAll||soles?1:Math.Min(1,(float)Math.Sqrt(p.Vel.X*p.Vel.X+p.Vel.Z*p.Vel.Z)/1.5f),soles);else BlendTransition();
                }
            }
            var spare=trPrev;trPrev=trLast;trLast=spare;CaptureTo(trLast);trHasPrev=true;trPrevTime=trTime;trTime=time;
        }

        // ---- the sample ----
        // `overlay` poses native layers (roll, E special, knockdown recovery, source motions) over the base when active.
        public void Sample(MeleeView p,float time,float dt,Action<MeleeRig> overlay=null)
        {
            var set=MeleeSet.For(p.Spec);var r=Rig;r.Reset();r.LegsAuthored=r.LegsLayered=r.MocapPosed=false;r.WeaponVisible=!p.WeaponAway;r.WeaponReach=p.Reach>0&&p.Reach<1e3f?p.Reach:1;
            bool planted=p.Committed(time)||p.HitStunUntil>time||p.Rolling(time);
            if(planted)r.Motion*=(float)Math.Exp(-MeleeMath.Clamp(dt,0,.05f)*22);
            var vel=planted?new Vector3(0,p.Vel.Y,0):p.Vel;var shown=p;if(planted){shown=p.Clone();shown.Vel=vel;}
            gait.Locomotion(r,shown,time,dt,planted);SprintLean(p,dt);gaitPelvis=r.E[CapturedRig.Pelvis];
            var a=AttackPose(p,time);TrackSwing(p,a,time);WatchAir(p,a,time);WatchPlunge(p,time);airLive=false;plungeStuck=false;CutAge=float.NaN;
            // Source order (sampleCharacter): juggle, knockdown, hitstun, roll, then block, special, cut or guard; the light hit
            // reaction rides the last three. Native layers pose juggles, knockdowns, rolls and specials over the captured guard.
            if(p.Juggle||p.Down){Ready(set,p,time,MeleeReady(p,time));overlay?.Invoke(r);r.MocapPosed=false;}
            else if(p.HitStunUntil>time)
            {
                // The native hit layer's pack reaction (Core/NativePackReact) owns a melee stun over the guard when it drives it.
                if(HitLayer!=null&&HitLayer.Drives(time)){Ready(set,p,time,MeleeReady(p,time));State="hit reaction";}
                else
                {
                    Stagger(set,p,time,MeleeReady(p,time));State="hit reaction";
                    var h=ReactionOf(p);if(h==null||!React(p,h,time,true))FallbackFlinch(p,time);
                }
            }
            else if(p.Rolling(time)){Ready(set,p,time,MeleeReady(p,time));overlay?.Invoke(r);r.MocapPosed=false;}
            else
            {
                if(p.Guarding||p.BlockTime>0)Block(set,p,time);
                else if(p.Action(time)){Ready(set,p,time,MeleeReady(p,time));overlay?.Invoke(r);r.MocapPosed=false;}
                else if(!a.Active)Ready(set,p,time,MeleeReady(p,time));
                else
                {
                    // A captured cut blends in over the pose the rig showed (guard idle, running carry or, for an Echo Strike
                    // follow-up, the cut it follows); its footwork blends from the ankles as the gait placed them.
                    // A chain enters from the last swing's live follow-through instead (ChainBase), its feet from where they stood.
                    bool chained=Chained(p);CarryHold(RunCarry(),time,p);float entry=chained?ChainEntry(a.Windup):PhraseEntry(p,a.Windup),k=a.Age<entry?MeleeMath.Ease(a.Age/entry):1,carry=MeleeReady(p,time);
                    StartFeet(p,a,time);bool plunge=PlungeSwing(p),air=!plunge&&AirSwing(p),sprint=!plunge&&!air&&SprintSwing(p);
                    if(k<1){entryAnkles[0]=r.AnkleInGroup(true);entryAnkles[1]=r.AnkleInGroup(false);if(chained)ChainBase(set,p,time);else if(!(p.Followup&&FollowupBase(set,p)))Ready(set,p,time,carry);}
                    float lag=plunge?0:HitStop(p,a);CutAt=p.LastAttack;CutWindup=a.Windup;CutWindow=a.Window;CutAge=a.Age-lag;
                    if(plunge)PlungeCut(set,p,time);else if(air)AirCut(set,p,a,lag,k,time);else if(sprint)SprintCut(set,p,a,lag,k);else Cut(set,p,a,lag,k);swingNow.Drawn=time;swingNow.Air=airLive=air;swingNow.Sprint=sprint;swingNow.Plunge=plunge;leapLive=sprint&&NativeSprintAttackRules.Leaps(p.Spec);plungeLive=plunge;
                }
                var light=ReactionOf(p);if(light!=null)React(p,light,time,false);
            }
            // The gait takes the footwork's feet over exactly (not within its 1.5 cm), so a swing hands its planted soles on unmoved.
            gait.SyncTolerance=stepped?0:MeleeGait.SyncNear;stepped=false;
            if(JumpLayer!=null){JumpLayer.LandScale=HeavyLanding(p,time);JumpLayer.Leap=leapLive||plungeLive;}leapLive=false;plungeLive=false;
            SprintLayer?.Melee(r,p,time,dt);
            JumpLayer?.Melee(r,p,time,dt);
            HitLayer?.Layer(r,time);
            gait.GroundPass(r,p,time,dt,p.Hp<=0||p.Juggle||p.Down||p.Rolling(time));
            FinishPose(p,time);HitLayer?.Replant(r);
            FloorGuard(set,p,time);
            CaptureTo(trLast);KeepShownAnkles(p,time);
        }
        /// <summary>A style's reach seat after the ground pass (the Cyber Lord's travel carry, Core/CyberLordRun.cs: its planted feet
        /// always reach); null for every other fighter. The style sets it each frame.</summary>
        public CyberLordRunCarry FighterSeat;
        // ---- the DEV fighting styles (Core/NativeFighterPose): the same gait, then the caller's style layer, then FighterFinish ----
        public void FighterBase(MeleeView p,float time,float dt)
        {
            var r=Rig;r.Reset();r.LegsAuthored=r.LegsLayered=r.MocapPosed=false;r.WeaponVisible=!p.WeaponAway;r.WeaponReach=1;FighterSeat=null;
            bool planted=p.Committed(time)||p.HitStunUntil>time||p.Rolling(time);
            if(planted)r.Motion*=(float)Math.Exp(-MeleeMath.Clamp(dt,0,.05f)*22);
            var vel=p.Vel;if(planted)p.Vel=new Vector3(0,vel.Y,0);gait.Locomotion(r,p,time,dt,planted);p.Vel=vel;SprintLean(p,dt);
        }
        // The hit stun's reaction (or flinch) and otherwise the light reaction over the style, then the ground pass and the pose
        // transitions (finishPose; a fighter's key is its style action, p.ActionKind, so a wavedash cancelled into the Electric
        // Hook blends over .055 s instead of snapping).
        public void FighterFinish(MeleeView p,float time,float dt)
        {
            if(!p.Juggle&&!p.Down&&p.HitStunUntil>time){var h=ReactionOf(p);if(h==null||!React(p,h,time,true))FallbackFlinch(p,time);}
            else if(!p.Juggle&&!p.Down&&!p.Rolling(time)){var light=ReactionOf(p);if(light!=null)React(p,light,time,false);}
            JumpLayer?.Fighter(Rig,p,time,dt);
            HitLayer?.Layer(Rig,time);
            gait.GroundPass(Rig,p,time,dt,p.Hp<=0||p.Juggle||p.Down||p.Rolling(time));FighterSeat?.Seat(Rig,gait,p.Grounded,time,p.Hp<=0||p.Juggle||p.Down||p.Rolling(time));
            FinishPose(p,time);HitLayer?.Replant(Rig);
        }
        // gaitFoot: this sample's gait ankle target, heading, pitch and toe for a foot (the fighter layers edit over it).
        public bool GaitFoot(bool left,out Vector3 target,out float yaw,out float pitch,out float toe)=>gait.GaitFoot(left,out target,out yaw,out pitch,out toe);
        // Forward sprint lean under the carries (melee-guard sprintLean): read by the gait's spine before the captures pose it.
        private float sprintBlend;
        private void SprintLean(MeleeView p,float dt)
        {
            float k=p.Spec=="sword"?.1f:p.Spec=="daggers"?.12f:p.Spec=="shield"?.08f:p.Spec=="greatsword"?.14f:0,run=MeleeMath.Ease((Rig.Motion-.1f)/.65f);
            sprintBlend+=((p.Sprinting?1:0)-sprintBlend)*(1-(float)Math.Exp(-Math.Min(.05f,Math.Max(0,dt))*10));
            Rig.AddE(CapturedRig.Spine,k*run,0,0);if(sprintBlend>1e-4f)Rig.AddE(CapturedRig.Spine,.12f*sprintBlend*run,0,0);
        }
        private void FloorGuard(MeleeSet set,MeleeView p,float time)
        {
            // A plunging stab's blade stays in the floor until it comes free (NativePlungePose).
            if(!p.Grounded||!Rig.MocapPosed||plungeStuck)return;
            float rate=p.AttackRate>0?p.AttackRate:1,end=!float.IsNaN(p.SwingUntil)?p.SwingUntil:p.LastAttack+p.Windup+.16f/rate,age=time-end;
            bool recovering=p.Spec=="daggers"&&(p.AttackIndex>0?p.AttackIndex:1)%4!=0&&age>=0&&age<Recovery(p.Spec)/rate+1&&!p.Guarding&&p.BlockTime<=0&&!(p.HitStunUntil>time)&&!(p.RollAt>-90&&p.RollAt>p.LastAttack)&&!p.Action(time);
            MeleeCapture.FinishRecovery(Rig,set,time,recovering,rate,p.Spec,wrists,keptAxes);
        }
    }

    // Unity-owned swing timing for the four captured weapons (owner 2026-10-12, docs/MELEE-FEEL.md), shared by the
    // third-person rigs (MeleeMotion), the first-person view model (CapturedAnimation) and the slash crescent (NativeSlashFx).
    // The source retime (CapturedClip.AttackTime) maps a cut's contact marks onto the authoritative hit window, but the authored
    // blade whip starts up to 70 ms before the first mark: the blade peaked in the windup, crawled through the window at
    // 0.13-0.4x and stopped dead at its end. Each cut names its authored sweep [SweepStart, SweepEnd] (clip seconds) and plays it
    // linearly across the hit window, so the fastest motion lands inside it; the windup plays [Start, SweepStart] on the cut's
    // pace keys, ending at the window's clip rate; after the window the clip carries on at that rate and eases to rest at
    // FollowEnd (the follow-through) while the pose settles into the guard over the real recovery. Presentation only: the
    // window, its timing and the hit sweep stay the authority's.
    public static class MeleeSwing
    {
        public readonly struct Shape
        {
            // Even: how far the windup's pacing moves from the cut's pace keys toward even steps (0 keeps the keys).
            public readonly float Start,SweepStart,SweepEnd,FollowEnd,Even;
            public Shape(float start,float sweepStart,float sweepEnd,float followEnd,float even=0){Start=start;SweepStart=sweepStart;SweepEnd=sweepEnd;FollowEnd=followEnd;Even=even;}
        }
        // From the authored blade-tip speed of each cut: the sweep holds its whip, ending while the blade still moves (the
        // greatsword's just before its chop meets the floor). The daggers start after their wind-back, which the entry blend from
        // the guard stands in for, cutting their windup compression from x4.7-5.7 to x2.5-3.0. Sword cut B's pace keys crowd two
        // fast spans into its windup (3.7-3.9x); half-even pacing keeps its peak inside the window.
        private static readonly Dictionary<string,Shape> Shapes=new Dictionary<string,Shape>
        {
            {"sword/cutA",new Shape(0,.255f,.36f,.40f)},{"sword/cutB",new Shape(0,.275f,.395f,.435f,.5f)},{"sword/cutC",new Shape(0,.245f,.355f,.40f)},
            {"daggers/cutA",new Shape(.2f,.425f,.545f,.58f)},{"daggers/cutB",new Shape(.1f,.335f,.45f,.49f)},{"daggers/cutC",new Shape(.1f,.37f,.475f,.5f)},{"daggers/cutD",new Shape(.1f,.335f,.45f,.49f)},
            {"shield/cutA",new Shape(0,.43f,.52f,.6f)},{"shield/cutB",new Shape(0,.27f,.36f,.42f)},
            {"greatsword/cutA",new Shape(0,.40f,.47f,.49f)},{"greatsword/cutB",new Shape(0,.43f,.54f,.555f)},
            // The air attacks (NativeAirAttackRules: the windup is their own raise and hang; the window plays the chop and the
            // take's held impact, so the blade reaches the bottom two thirds of the way through it).
            // The sprint takes (NativeSprintAttackRules), from their posed blade tips: the sword's slash whips across the front at
            // up to 100 m/s; the daggers cross both blades (the twin plays it out and back); the shield's charge holds the shield
            // out (its bash is the rig's); the greatsword's cleave sweeps in from the right as its hop comes down.
            {"sprint/sword",new Shape(0,.333f,.433f,.55f)},{"sprint/daggers",new Shape(0,.067f,.2f,.38f)},{"sprint/shield",new Shape(0,.2f,.4f,.55f)},{"sprint/greatsword",new Shape(0,.2f,.333f,.45f)},
            {"air/sword",new Shape(0,.133f,.233f,.3f)},{"air/daggers",new Shape(0,.133f,.233f,.3f)},{"air/shield",new Shape(0,.133f,.233f,.3f)},{"air/greatsword",new Shape(0,.133f,.233f,.3f)},
        };
        // A clip without a shape keeps its contact marks.
        public static Shape For(CapturedClip c)=>Shapes.TryGetValue(c.Name,out var s)?s:new Shape(c.Marks[0],c.Marks[1],c.Marks[2],c.Marks[2]);
        // The follow-through lasts at most this share of the recovery.
        public const float FollowShare=.6f;
        // The clip time `age` seconds into a swing. keep: the daggers' own captured recovery to their settled mark (their wrist
        // limiter settles it) instead of the follow-through.
        public static float ClipTime(CapturedClip c,float age,float windup,float active,float recovery,bool keep=false)
        {
            var s=For(c);active=Math.Max(.001f,active);float rate=(s.SweepEnd-s.SweepStart)/active;
            if(age>=windup&&age<=windup+active)return s.SweepStart+(age-windup)*rate;
            if(age>windup+active)
            {
                float t=age-windup-active;
                if(keep){float h=Math.Max(.001f,recovery),end=Math.Max(s.SweepEnd,c.Marks[3]);return t>=h?end:MeleeMath.Hermite(s.SweepEnd,end,Math.Min(rate*h,3*(end-s.SweepEnd)),0,t/h);}
                // Decelerating evenly from the window's rate to rest (a monotone Hermite: its start tangent is 2/3 of the bound).
                float d=s.FollowEnd-s.SweepEnd;if(d<=1e-5f)return s.SweepEnd;
                float span=Math.Min(2*d/Math.Max(rate,1e-4f),FollowShare*Math.Max(.001f,recovery));
                return t>=span?s.FollowEnd:MeleeMath.Hermite(s.SweepEnd,s.FollowEnd,rate*span,0,t/span);
            }
            // The windup: the pace keys (evenly spaced over it) rescaled from [mark 0, mark 1] onto [Start, SweepStart], monotone
            // Hermite tangents (CapturedClip.Retime) except the last, which takes the window's rate (bounded to stay monotone).
            int n=c.Pace.Length+1;float m0=c.Marks[0],scale=(s.SweepStart-s.Start)/Math.Max(1e-4f,c.Marks[1]-m0),h2=Math.Max(1e-4f,windup)/n;
            float V(int k)=>k<=0?s.Start:k>=n?s.SweepStart:MeleeMath.Lerp(s.Start+(c.Pace[k-1]-m0)*scale,s.Start+(s.SweepStart-s.Start)*k/n,s.Even);
            float S(int k)=>(V(k+1)-V(k))/h2;
            float T(int k){if(k<=0)return S(0);if(k>=n)return MeleeMath.Clamp(rate,0,3*Math.Max(0,S(n-1)));float a=S(k-1),b=S(k);return a*b<=0?0:2*a*b/(a+b);}
            float x=Math.Max(0,age)/h2;int i=Math.Min(n-1,(int)x);
            return MeleeMath.Hermite(V(i),V(i+1),T(i)*h2,T(i+1)*h2,Math.Min(1,x-i));
        }
        // The guard's weight over the recovery: easing in from the window's end (the follow-through dominates its start, so the
        // blade turns back toward the guard without stopping) to 1 at the attack's end; a cubic, so a held swing does not idle
        // in the long flat tail of a quintic ease before the next one starts.
        public static float Settle(float age,float windup,float active,float recovery)=>MeleeMath.Smooth((age-windup-active)/Math.Max(.001f,recovery));

        // ---- the visible blade over the hit window (the slash crescent follows it) ----
        // The posed blade tip's bearing round the body at 17 even steps through the window (the sweep plays linearly, so this
        // depends on the clip alone), in the native frame (radians from the facing, + toward the pilot's right).
        public sealed class Visible
        {
            public const int Steps=16;
            public readonly float[] Bearing=new float[Steps+1],Path=new float[Steps+1];
            // +1: the blade crosses the front toward the pilot's right; -1: toward its left.
            public float Direction;
            // The blade's main crossing of the front half (the samples it spans), for the crescent's growth.
            public int Enter,Leave;
            // A bash (the shield) barely turns across the front: its crescent grows with the distance the face has travelled.
            public bool Bash;
        }
        private static readonly Dictionary<CapturedClip,Visible> visible=new Dictionary<CapturedClip,Visible>();
        public static Visible VisibleSweep(MeleeSet set,CapturedClip c)
        {
            if(visible.TryGetValue(c,out var v))return v;
            v=new Visible();var r=new MeleeRig();var row=new float[CapturedClip.Channels];var s=For(c);var tips=new Vector3[2,Visible.Steps+1];float[] path=new float[2];
            for(int j=0;j<=Visible.Steps;j++)
            {
                c.Sample(s.SweepStart+(s.SweepEnd-s.SweepStart)*j/Visible.Steps,row);r.Reset();
                MeleeCapture.Apply(r,set,row,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0));
                for(int h=0;h<(set.Dual?2:1);h++)
                {
                    int hand=h==0?CapturedRig.RHand:CapturedRig.LHand;tips[h,j]=r.ChainPoint(hand,-1)+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(hand,-1));
                    if(j>0)path[h]+=Vector3.Distance(tips[h,j],tips[h,j-1]);
                }
            }
            // The daggers strike with either hand: the blade that travels further over the window is the one seen to cut.
            int lead=set.Dual&&path[1]>path[0]?1:0;
            // The source frame's +x is the pilot's left: the native bearing is its mirror.
            for(int j=0;j<=Visible.Steps;j++){Vector3 tip=tips[lead,j];v.Bearing[j]=-(float)Math.Atan2(tip.X,tip.Z);v.Path[j]=j==0?0:v.Path[j-1]+Vector3.Distance(tip,tips[lead,j-1]);}
            for(int j=0;j<=Visible.Steps;j++)v.Path[j]/=Math.Max(1e-4f,v.Path[Visible.Steps]);
            // The main crossing of the front: of the runs of samples within 100 deg of the facing, the one that turns furthest (a
            // cut that loops behind the body first can show the blade beside it before it comes round).
            int first=0,last=Visible.Steps;float travel=0,most=-1;
            for(int j=0;j<=Visible.Steps;j++)
            {
                if(Math.Abs(v.Bearing[j])>=1.75f)continue;int k=j;float turn=0;
                while(k<Visible.Steps&&Math.Abs(v.Bearing[k+1])<1.75f){turn+=MeleeMath.Wrap(v.Bearing[k+1]-v.Bearing[k]);k++;}
                if(Math.Abs(turn)>most){most=Math.Abs(turn);travel=turn;first=j;last=k;}j=k;
            }
            v.Direction=travel>=0?1:-1;v.Enter=first;v.Leave=last;v.Bash=Math.Abs(travel)<.35f;
            visible[c]=v;return v;
        }
        // The share (0..1) of a crescent spanning `span` radians round the facing that the visible blade has swept at window share
        // `u`: the blade's bearing clamped to the arc, never moving back (a bash: the share of the face's travel).
        public static float Swept(Visible v,float u,float span)
        {
            float half=Math.Max(1e-4f,span*.5f),best=0;u=MeleeMath.Clamp(u,0,1);
            int last=(int)Math.Floor(u*Visible.Steps);float frac=u*Visible.Steps-last;
            if(v.Bash)return last>=Visible.Steps?1:MeleeMath.Lerp(v.Path[last],v.Path[last+1],frac);
            // Before the crossing nothing; through it the furthest bearing reached (between samples, interpolated), kept after it.
            if(last<v.Enter)return 0;
            for(int j=v.Enter;j<=Math.Min(last+1,v.Leave);j++)
            {
                float b=v.Bearing[j];if(j==last+1){float a=v.Bearing[last];b=a+MeleeMath.Wrap(b-a)*frac;}
                best=Math.Max(best,MeleeMath.Clamp((v.Direction*b+half)/(2*half),0,1));
            }
            return best;
        }
    }
}
