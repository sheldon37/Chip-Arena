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
        public Vector3 Vel;public bool Grounded=true,Sprinting,Guarding,Hurricane,WeaponAway;public int AttackIndex;
        public bool Commit,Followup;public float CommitAt,CommitUntil,CommitYaw,StepAt,StepUntil,StepDistance;
        public float BlockTime,LastBlock=float.NaN,HitStunUntil,RollAt=-100,RollUntil=-100,ActionAt=-100,ActionUntil=-100;public string ActionKind;
        public bool Reaction,Parry;public float ReactionAt,ReactionUntil,ReactionYaw,ReactionTurn,ReactionStrength;public Vector3 ReactionDir,ReactionPoint;public int ReactionZone;
        public bool Down;public string DownPhase;
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
        private readonly float[] row=new float[CapturedClip.Channels],rowB=new float[CapturedClip.Channels],rowC=new float[CapturedClip.Channels];
        private readonly MeleeCapture.RecoveryCache recovery=new MeleeCapture.RecoveryCache();
        private readonly MeleeCapture.WristHistory wrists=new MeleeCapture.WristHistory();
        private readonly Vector3[] keptAxes={Vector3.UnitX,Vector3.UnitX};
        private readonly Vector3[] entryAnkles=new Vector3[2];
        public string State{get;private set;}="idle";
        public static bool Handles(string spec)=>MeleeSet.For(spec)!=null;
        public const float GuardHold=2.4f,GuardFade=.8f,HoldRelease=.22f,StepReach=.475f;
        public static float Recovery(string spec)=>spec=="greatsword"?.5f:spec=="daggers"?.15f:.3f;
        private static readonly Dictionary<string,float> WeaponWeight=new Dictionary<string,float>{{"siren",4},{"daggers",2},{"sword",5},{"shield",8},{"greatsword",12},{"pistol",2},{"rifle",7},{"machinegun",10},{"fire",4},{"water",4},{"lightning",3},{"darkness",5},{"leet",3},{"emperor",1},{"ansatsuken",1}};

        public struct Attack{public float Age,Windup,Window;public bool Active;}
        public static Attack AttackPose(MeleeView p,float time)
        {
            float rate=p.AttackRate>0?p.AttackRate:1,age=Math.Max(0,time-p.LastAttack),windup=!float.IsNaN(p.SwingAt)?Math.Max(.025f,p.SwingAt-p.LastAttack):Math.Max(.025f,p.Windup);
            float window=!float.IsNaN(p.SwingUntil)&&!float.IsNaN(p.SwingAt)?p.SwingUntil-p.SwingAt:.16f/rate,recoverySeconds=Recovery(p.Spec)/rate;
            return new Attack{Age=age,Windup=windup,Window=window,Active=age<windup+window+recoverySeconds&&!p.RollCancelled(time)};
        }

        // ---- presented yaw (motion-quality presentedYaw) ----
        private string yawSource;private float yawFrom,yawAt=-1e9f,yawLast,yawTime;private bool yawHas;
        public float PresentedYaw(MeleeView p,float time,float input)
        {
            bool hit=p.Reaction&&p.HitStunUntil>time&&p.ReactionAt<=time,commit=!hit&&p.Committed(time),roll=!hit&&!commit&&p.Rolling(time);
            string source=hit?"hit":commit?"commit":roll?"roll":"input";float target=hit?p.ReactionYaw:commit?p.CommitYaw:roll?p.Yaw:input;
            if(!yawHas||MeleeGait.TimeJumped(yawTime,time)){yawHas=true;yawSource=source;yawFrom=target;yawAt=-1e9f;yawLast=target;yawTime=time;}
            float now=Math.Max(time,yawTime);if(source!=yawSource){yawSource=source;yawFrom=yawLast;yawAt=now;}
            bool strike=!float.IsNaN(p.SwingAt)&&!float.IsNaN(p.SwingUntil)&&time>=p.SwingAt&&time<p.SwingUntil;float u=(now-yawAt)/.12f,yaw=target;
            if(u<1&&!commit&&!strike)yaw=yawFrom+MeleeMath.Wrap(target-yawFrom)*(1-(1-u)*(1-u)*(1-u));
            yawLast=yaw;yawTime=now;return yaw;
        }

        // ---- melee-guard carry hold and readiness ----
        private bool holdHas;private float holdValue,holdTime,holdGuard,holdAt,holdCarry,holdReady;
        private float RunCarry()=>MeleeMath.Ease((Rig.Motion-.06f)/.45f);
        private static float Readiness(MeleeView p,float time)
        {
            float rate=p.AttackRate>0?p.AttackRate:1,windup=!float.IsNaN(p.SwingAt)?Math.Max(.025f,p.SwingAt-p.LastAttack):Math.Max(.025f,p.Windup),window=!float.IsNaN(p.SwingUntil)&&!float.IsNaN(p.SwingAt)?p.SwingUntil-p.SwingAt:.16f/rate;
            float end=float.NegativeInfinity;void Consider(float start,float stop){if(start<=time)end=Math.Max(end,Math.Min(stop,time));}
            Consider(p.LastAttack,p.LastAttack+windup+window+Recovery(p.Spec)/rate);if(p.ActionKind!=null)Consider(p.ActionAt,p.ActionUntil);if(!float.IsNaN(p.LastBlock))Consider(p.LastBlock,p.LastBlock+.3f);
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
        private bool gateHas,gateOn;private float gateAttack,gateAt,gateFrom;
        public MeleeImpacts Impacts;
        private float HitStop(MeleeView p,Attack a)
        {
            if(!HitStopKeys.TryGetValue(p.Spec,out var k)||string.IsNullOrEmpty(p.Id)||Impacts==null)return 0;float t=p.LastAttack+a.Age,h=Impacts.HitAt(p.Id,t,.5f);
            if(float.IsNaN(h)||h<p.LastAttack+a.Windup-.03f||h>p.LastAttack+a.Windup+a.Window+.05f)return 0;
            if(!gateHas||gateAttack!=p.LastAttack||gateAt!=h||t<gateFrom){gateHas=true;gateAttack=p.LastAttack;gateAt=h;gateFrom=t;gateOn=t-h<=.08f;}
            if(!gateOn)return 0;float hold=k[0],slow=k[1],catchUp=k[2],u=t-gateFrom,max=hold*(1-slow);
            return u<=0?0:u<hold?u*(1-slow):u<hold+catchUp?max*(1-MeleeMath.Ease((u-hold)/catchUp)):0;
        }
        private static float Step(float u)=>MeleeMath.Ease((u-.1f)/.75f);
        private static float Arc(float u)=>(float)Math.Sin(Math.PI*u);
        private static float Travel(MeleeView p,float at,float windup,float window,float time)
        {
            float stepAt=p.Commit?p.StepAt:at+windup*.55f,stepUntil=p.Commit?p.StepUntil:at+windup+window*.8f;
            float distance=p.Commit?p.StepDistance:p.Spec=="daggers"?.85f:p.Spec=="sword"?.65f:p.Spec=="shield"?.55f:.45f;
            return distance*MeleeMath.Smooth((time-stepAt)/Math.Max(.01f,stepUntil-stepAt));
        }
        // Feet anchored to the floor, not to the travelling root: a planted foot holds its world position (the authored root
        // travel is subtracted) and only moves while it is lifted.
        private void Feet(MeleeView p,Attack a,float phase,int sign,bool heavy,bool dagger,bool overhead,bool spin,float turn,float weight,bool seat)
        {
            var r=Rig;float now=p.LastAttack+a.Age,stepUntil=p.Commit?p.StepUntil:p.LastAttack+a.Windup+a.Window*.8f;
            float travel=Travel(p,p.LastAttack,a.Windup,a.Window,now),plantTravel=Travel(p,p.LastAttack,a.Windup,a.Window,p.LastAttack+a.Windup+a.Window*.65f),full=Travel(p,p.LastAttack,a.Windup,a.Window,stepUntil);
            var st=MeleeStance.For(p.Spec);bool leadingLeft=sign>0,leadIsLead=leadingLeft==st.LeadLeft;
            float lfx=leadIsLead?st.LeadX:st.RearX,lfz=leadIsLead?st.LeadZ:st.RearZ,lft=leadIsLead?st.LeadToe:st.RearToe,rfx=leadIsLead?st.RearX:st.LeadX,rfz=leadIsLead?st.RearZ:st.LeadZ,rft=leadIsLead?st.RearToe:st.LeadToe;
            float lx=(leadingLeft?1:-1)*Math.Abs(lfx),rx=(leadingLeft?-1:1)*Math.Abs(rfx),lyaw0=(leadingLeft?1:-1)*lft,ryaw0=(leadingLeft?-1:1)*rft;
            float cx=(leadingLeft?1:-1)*(heavy?.27f:dagger?.29f:.25f),worldPlant=.31f+plantTravel,su=MeleeMath.Clamp(travel/Math.Max(.01f,plantTravel),0,1),bu=MeleeMath.Clamp((phase-2.45f)/.5f,0,1),s=su*su*(3-2*su),back=Step(bu);
            float lift=(overhead?.18f:heavy?.14f:dagger?.07f:.09f)*Arc(su)+.07f*Arc(bu),cyaw=.15f*sign;
            float lz=phase<2.45f?lfz+(worldPlant-lfz)*s-travel:(worldPlant-full)*(1-back)+lfz*back;
            Vector3 lead=new Vector3(phase<2.45f?lx+(cx-lx)*s:cx+(lx-cx)*back,-.795f+lift,lz);
            float lyaw=phase<2.45f?lyaw0+(cyaw-lyaw0)*s:cyaw+(lyaw0-cyaw)*back,lheel=-.12f*Arc(su)+.22f*Arc(bu);
            float qu=MeleeMath.Clamp((phase-.75f)/1.65f,0,1),q=Step(qu);
            Vector3 rear=new Vector3(rx*(1+(heavy?.3f:.18f)*Arc(qu)),-.795f+(heavy?.05f:.065f)*Arc(qu)+(overhead?.09f*Arc(MeleeMath.Clamp((phase-.45f)/.85f,0,1)):0),rfz+full*q-travel-(heavy?.1f:0)*Arc(qu));
            float ryaw=ryaw0-.35f*sign*Arc(qu),rheel=(heavy?.38f:.32f)*(float)Math.Sin(Math.PI*Math.Min(1,qu*1.15f));
            Vector3? lin=weight<1?entryAnkles[leadingLeft?0:1]:(Vector3?)null,rin=weight<1?entryAnkles[leadingLeft?1:0]:(Vector3?)null;
            if(spin)
            {
                // A hopped turn: the feet hold through the windup, circle about the body in the air and land back on the stance.
                float u=MeleeMath.Clamp((phase-1.1f)/1.75f,0,1),b=Step(u),hop=Arc(u),air=MeleeMath.Ease((u-.05f)/.25f),rot=air*(SpinFrom+(MeleeMath.TAU-SpinFrom)*Math.Max(b,(turn-SpinFrom)/(MeleeMath.TAU-SpinFrom)));
                Vector3 body=r.P[CapturedRig.Body];float cxs=body.X,czs=body.Z,c=(float)Math.Cos(rot),sn=(float)Math.Sin(rot);body.Y+=.045f*hop;r.P[CapturedRig.Body]=body;
                Vector3 Place(float x,float z){float ox=x-cxs,oz=z-travel*(1-air)-czs;return new Vector3(cxs+ox*c+oz*sn,-.795f+.075f*hop,czs-ox*sn+oz*c);}
                lead=Place(lx,lfz);rear=Place(rx,rfz);lead=Reachable(leadingLeft,lead);rear=Reachable(!leadingLeft,rear);
                float spinYaw=turn*b;r.Foot(leadingLeft,lead,lyaw0+spinYaw,.3f*hop,weight,lin);r.Foot(!leadingLeft,rear,ryaw0+spinYaw,.3f*hop,weight,rin);return;
            }
            if(seat)SeatHips(leadingLeft,lead,rear,phase<2.45f?s:1-back,weight);lead=Reachable(leadingLeft,lead);rear=Reachable(!leadingLeft,rear);
            r.Foot(leadingLeft,lead,lyaw,lheel,weight,lin);r.Foot(!leadingLeft,rear,ryaw,rheel,weight,rin);
        }
        // Under a captured cut the body lowers until each foot would reach the floor under its target within STEP_REACH.
        private void SeatHips(bool leadingLeft,Vector3 lead,Vector3 rear,float leadK,float weight)
        {
            float drop=0;
            for(int i=0;i<2;i++)
            {
                bool left=i==0?leadingLeft:!leadingLeft;Vector3 t=i==0?lead:rear;float k=i==0?leadK:1;if(k<=0)continue;
                Vector3 hip=Rig.ChainPoint(left?CapturedRig.LHip:CapturedRig.RHip,-1);
                float h=Math.Min((float)Math.Sqrt((hip.X-t.X)*(hip.X-t.X)+(hip.Z-t.Z)*(hip.Z-t.Z)),.92f*StepReach),need=hip.Y+.795f-(float)Math.Sqrt(StepReach*StepReach-h*h);
                if(need>0)drop=Math.Max(drop,Math.Min(.15f,need)*k);
            }
            Vector3 b=Rig.P[CapturedRig.Body];b.Y-=drop*weight;Rig.P[CapturedRig.Body]=b;
        }
        // A lifted foot the hip cannot reach is drawn toward the hip (fading out as it comes down).
        private Vector3 Reachable(bool left,Vector3 target)
        {
            float k=MeleeMath.Clamp((target.Y+.795f)/.03f,0,1);if(k<=0)return target;
            Vector3 hip=Rig.ChainPoint(left?CapturedRig.LHip:CapturedRig.RHip,-1),to=target-hip;float d=to.Length();if(d<=StepReach)return target;
            return target+to*((StepReach/d-1)*k);
        }
        private void PhraseFootwork(MeleeView p,Attack a,float weight,float turn)
        {
            float rate=p.AttackRate>0?p.AttackRate:1,recoverySeconds=Recovery(p.Spec)/rate;bool heavy=p.Spec=="greatsword";float drag=heavy?.4f:p.Spec=="shield"?.35f:0;
            Describe(p.Spec,Math.Max(0,(p.AttackIndex>0?p.AttackIndex:1)-1),out int sign,out bool spin,out bool overhead);
            float phase=Phase(a,recoverySeconds,a.Age-HitStop(p,a),drag,heavy);
            Feet(p,a,phase,sign,heavy,p.Spec=="daggers",overhead,spin,turn,weight,true);
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
                if(carry*k<.999f){set.Walk.Sample(set.Walk.Marks[0]+phase*set.Walk.Duration,row);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(carry,set.Fit,legs:0,root:0));}
                if(k>.001f){set.Run.Sample(set.Run.Marks[0]+phase*set.Run.Duration,row);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(carry*k,set.Fit,legs:0,root:0));}
                State=k>.5f?"run":"walk";
            }
            else State="idle";
        }
        private bool FollowupBase(MeleeSet set,MeleeView p)
        {
            var prev=CutClip(set,Math.Max(1,p.AttackIndex-1));prev.Sample(prev.Marks[2],row);MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0));return true;
        }
        private void Cut(MeleeSet set,MeleeView p,Attack a,float lag,float weight)
        {
            var clip=CutClip(set,p.AttackIndex);float rate=p.AttackRate>0?p.AttackRate:1,recoverySeconds=Recovery(p.Spec)/rate,age=a.Age-lag;
            float ct=clip.AttackTime(age,a.Windup,a.Window,recoverySeconds);var m=clip.Marks;clip.Sample(ct,row);
            // A partial anticipation: the windup's pose eases toward the straight path between the start and contact poses.
            float keep=set.AnticipationFor(p.AttackIndex);
            if(keep<1&&ct>m[0]&&ct<m[1]){clip.Sample(m[0],rowB);clip.Sample(m[1],rowC);MeleeCapture.BlendRowsTurning(rowB,rowC,(ct-m[0])/(m[1]-m[0]),rowB);MeleeCapture.BlendRowsTurning(row,rowB,1-keep,row);}
            float settle=MeleeMath.Ease((age-a.Windup-a.Window)/recoverySeconds);
            if(settle>0&&set.Idle!=null&&p.Spec!="daggers")
            {
                clip.Sample(m[2],row);set.Idle.Sample(p.LastAttack+a.Windup+a.Window+recoverySeconds,rowB);
                MeleeCapture.Recovery(Rig,set,row,rowB,settle,p.LastAttack,p.AttackIndex,rate,recovery,keptAxes);
            }
            else MeleeCapture.Apply(Rig,set,row,CaptureOptions.Of(weight,set.Fit,MeleeClear.Turn,legs:0));
            Describe(p.Spec,Math.Max(0,(p.AttackIndex>0?p.AttackIndex:1)-1),out _,out bool spin,out _);
            float turn=spin?MeleeMath.Ease((age-a.Windup)/(a.Window+recoverySeconds))*MeleeMath.TAU:0;
            Rig.AddE(CapturedRig.Body,0,turn*weight,0);PhraseFootwork(p,a,weight,turn);
            // The Hurricane chip's full turn over the strike window.
            if(p.Hurricane&&!spin)Rig.AddE(CapturedRig.Body,0,MeleeMath.Ease((age-a.Windup)/a.Window)*MeleeMath.TAU*weight,0);
            State="cut"+(char)('A'+Array.IndexOf(set.Cuts,clip))+(settle>0?" recovery":"");
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
            if(set.BlockHit!=null&&!float.IsNaN(impact)&&time>=impact)
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
        // The pose family (transitionAction); an attack's index and a knockdown's phase are kept beside it, unconcatenated.
        private static string TransitionAction(MeleeView p,float time,out int index,out string phase)
        {
            var a=AttackPose(p,time);float rate=p.AttackRate>0?p.AttackRate:1,duration=a.Windup+a.Window+Recovery(p.Spec)/rate;index=0;phase=null;
            if(p.Down){phase=p.DownPhase??"";return "down";}
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
            if(action=="base")return previous=="attack"?(heavy?.28f:.22f):.13f;
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
                if(reaction==null&&ReactionFamily(trAction)&&trHasPrev&&trTime>trPrevTime)
                {
                    // Continue the last frame's joint velocities to this frame's time, then keep only the remaining discontinuity.
                    float ratio=MeleeMath.Clamp((time-trTime)/(trTime-trPrevTime),0,2);
                    for(int i=0;i<MeleeRig.N;i++)trProjected.Q[i]=MeleeMath.Slerp(trLast.Q[i]*MeleeMath.Inverse(trPrev.Q[i]),Quaternion.Identity,1-ratio)*trLast.Q[i];
                    trProjected.Position=(trLast.Position-trPrev.Position)*ratio+trLast.Position;incoming=trProjected;
                }
            }
            if(incoming!=null)
            {
                trHold=action=="base"&&trAction=="attack";Keep();trAt=time;trDuration=duration;trActive=duration>1e-4f;
                if(trActive){for(int i=0;i<MeleeRig.N;i++)trOffset.Q[i]=MeleeMath.Inverse(trCurrent.Q[i])*incoming.Q[i];trOffset.Position=incoming.Position-trCurrent.Position;}
            }
            if(trActive)
            {
                float u=MeleeMath.Ease((time-trAt)/trDuration);
                if(u>=1)trActive=false;
                else
                {
                    void Blend(){for(int i=0;i<MeleeRig.N;i++)Rig.Multiply(i,MeleeMath.Slerp(trOffset.Q[i],Quaternion.Identity,u));Rig.P[CapturedRig.Body]+=trOffset.Position*(1-u);}
                    // Weighted by travel speed: a standing recovery keeps its joint-space leg blend, a run resuming keeps its planted feet.
                    if(trHold&&p.Grounded&&p.Hp>0)Rig.HoldAnkles(Blend,Math.Min(1,(float)Math.Sqrt(p.Vel.X*p.Vel.X+p.Vel.Z*p.Vel.Z)/1.5f));else Blend();
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
            gait.Locomotion(r,shown,time,dt,planted);SprintLean(p,dt);
            var a=AttackPose(p,time);
            // Source order (sampleCharacter): knockdown, hitstun, roll, then block, special, cut or guard; the light hit
            // reaction rides the last three. Native layers pose knockdowns, rolls and specials over the captured guard.
            if(p.Down){Ready(set,p,time,MeleeReady(p,time));overlay?.Invoke(r);r.MocapPosed=false;}
            else if(p.HitStunUntil>time)
            {
                Stagger(set,p,time,MeleeReady(p,time));State="hit reaction";
                var h=ReactionOf(p);if(h==null||!React(p,h,time,true))FallbackFlinch(p,time);
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
                    CarryHold(RunCarry(),time,p);float entry=PhraseEntry(p,a.Windup),k=a.Age<entry?MeleeMath.Ease(a.Age/entry):1,carry=MeleeReady(p,time);
                    if(k<1){entryAnkles[0]=r.AnkleInGroup(true);entryAnkles[1]=r.AnkleInGroup(false);if(!(p.Followup&&FollowupBase(set,p)))Ready(set,p,time,carry);}
                    Cut(set,p,a,HitStop(p,a),k);
                }
                var light=ReactionOf(p);if(light!=null)React(p,light,time,false);
            }
            gait.GroundPass(r,p,time,dt,p.Hp<=0||p.Down||p.Rolling(time));
            FinishPose(p,time);
            FloorGuard(set,p,time);
            CaptureTo(trLast);
        }
        // Forward sprint lean under the carries (melee-guard sprintLean): read by the gait's spine before the captures pose it.
        private float sprintBlend;
        private void SprintLean(MeleeView p,float dt)
        {
            float k=p.Spec=="sword"?.1f:p.Spec=="daggers"?.12f:p.Spec=="shield"?.08f:.14f,run=MeleeMath.Ease((Rig.Motion-.1f)/.65f);
            sprintBlend+=((p.Sprinting?1:0)-sprintBlend)*(1-(float)Math.Exp(-Math.Min(.05f,Math.Max(0,dt))*10));
            Rig.AddE(CapturedRig.Spine,k*run,0,0);if(sprintBlend>1e-4f)Rig.AddE(CapturedRig.Spine,.12f*sprintBlend*run,0,0);
        }
        private void FloorGuard(MeleeSet set,MeleeView p,float time)
        {
            if(!p.Grounded||!Rig.MocapPosed)return;
            float rate=p.AttackRate>0?p.AttackRate:1,end=!float.IsNaN(p.SwingUntil)?p.SwingUntil:p.LastAttack+p.Windup+.16f/rate,age=time-end;
            bool recovering=p.Spec=="daggers"&&(p.AttackIndex>0?p.AttackIndex:1)%4!=0&&age>=0&&age<Recovery(p.Spec)/rate+1&&!p.Guarding&&p.BlockTime<=0&&!(p.HitStunUntil>time)&&!(p.RollAt>-90&&p.RollAt>p.LastAttack)&&!p.Action(time);
            MeleeCapture.FinishRecovery(Rig,set,time,recovering,rate,p.Spec,wrists,keptAxes);
        }
    }
}
