using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's ranged-mode body, the cyber mage (Core/CyberLordRanged.cs, Core/CyberKit2Ranged.cs; docs/CYBER-LORD.md
    /// "Ranged mode" and "Kit v2"; owner 2026-10-15: "Ranged is also intended to be like a cyber mage- throwing like
    /// "Cyber-themed fire balls" ... sprint animation on ranged doesn't look good at all ... allow movement during ranged
    /// attacks"). A layer over the fighter route's pose (CyberLordMotion.Style, after the capture and the idle) and over the
    /// first-person view model, on the upper body only: the legs stay the gait's (running, strafing, backpedalling) whatever the
    /// arms do, so every throw and cast reads over movement.
    /// - The stance: a pitcher's set. The next throw's hand is loaded beside the head (a plasma fireball held there by the
    ///   effects), the other hand forward on the aim; the chest coils toward the loaded side and the head stays on the aim.
    /// - The throws (v1's Bit Bolt window today, v2's Plasma Byte when its record lands): the loaded hand whips overhand from
    ///   behind the shoulder, passing the release point on the replicated release (accelerating into it, decelerating out of it,
    ///   so no frame jumps), out to full extension as the chest uncoils, then settles forward while the other hand loads: the
    ///   alternate-hand rhythm is continuous at any interval, and the pose at a window's start equals the stance before it. The
    ///   Overflow (every third v2 throw) gathers both hands at the chest and pushes them out together.
    /// - The glide (the ranged sprint): a leaning glide with both arms swept back and out, the palms trailing (the effects stream
    ///   energy off them), on the carry's eased sprint ramp. A throw from the glide swings forward from behind and back.
    /// - v1's other casts (Ping Lance, Lag Field, Proxy Swap, Root Access) and v2's (Daisy Chain, Singularity, Packet Storm,
    ///   Orbital Uplink, the last steering the column with a pointing arm) on their replicated windows, and the key-5
    ///   reconfiguration (the forearms fold in to the chest and out into the other mode).
    /// A melee take still owning the frame (the guard, Q, E) yields the layer. Pure functions of the view, the clock and the rig
    /// the route posed; nothing here reaches the authority, hit timing, hitboxes, damage, cooldowns or movement. No allocation.
    /// </summary>
    public static class CyberRangedPose
    {
        /// <summary>The first-person view's hold (from where the view's stance holds the fists, in its lower half): reached ahead,
        /// drawn in toward the centre to ViewSpread of their spread, lifted; the gap Root Access keeps; the share of the pitch the
        /// chest takes; the largest pitch followed.</summary>
        public const float ViewReach=.07f,ViewSpread=.72f,ViewLift=.03f,ViewGap=.11f,ChestPitch=.3f,PitchMax=1.1f;
        /// <summary>The stance and the throw (m from each hand's own shoulder in the aim frame: x out from the body, y up, z along
        /// the aim): the forward hand, the loaded hand beside the head, the release point the hand passes on the release, full
        /// extension, the Overflow's gather at the chest and its push, the glide's trailing hand.</summary>
        public static readonly Vector3 Guard=new Vector3(-.16f,-.01f,.36f),Loaded=new Vector3(.05f,.19f,-.12f),ReleaseAt=new Vector3(-.06f,.13f,.2f),Extend=new Vector3(-.16f,.0f,.47f),
            Gather=new Vector3(-.27f,-.04f,.19f),Push=new Vector3(-.22f,.02f,.45f),Glide=new Vector3(.24f,-.26f,-.36f);
        /// <summary>Half the shoulders' span (the rig's clavicle and shoulder offsets).</summary>
        public const float ShoulderHalf=.345f;
        private const int GuardAt=0,LoadedAt=1,MarkAt=2,ExtendAt=3,GatherAt=4,PushAt=5,GlideAt=6;
        private static readonly Vector3[] Offsets={Guard,Loaded,ReleaseAt,Extend,Gather,Push,Glide};
        /// <summary>First person (the view's frame from where its stance holds each fist: x toward the centre, y up, z along the
        /// aim): the forward hand, the loaded hand (back and out, still in view), the release point, the extension at the
        /// centre, the Overflow's gather and push.</summary>
        public static readonly Vector3[] ViewOffsets={new Vector3(.03f,.03f,.06f),new Vector3(-.03f,.085f,-.02f),new Vector3(.06f,.075f,.08f),new Vector3(.12f,.05f,.2f),new Vector3(.11f,.03f,.05f),new Vector3(.13f,.05f,.2f),new Vector3(0,-.04f,-.02f)};
        /// <summary>The coil (rad about the spine: the loaded shoulder back), the throw's extra coil and its uncoil (the throwing
        /// shoulder forward), the Overflow's lean back and in, the stance's lean into the aim, the glide's stride swing (m).</summary>
        public const float GlideLift=.3f,Coil=.17f,Wind=.08f,Uncoil=.3f,OverflowBack=-.1f,OverflowIn=.14f,StanceLean=.04f,GlideSwing=.035f,RunBob=.016f;
        /// <summary>v1's other casts' motion (m, rad): the lance's cock and thrust, the lob's reach back and up, the swap's tuck and
        /// dip, the beam's converge, tremor and recoil (thrown back and up, the chest back).</summary>
        public const float LanceCock=.14f,LanceThrust=.06f,LanceSnap=.06f,LobBack=.26f,LobLead=.045f,LobSwing=.17f,TuckIn=.14f,SwapDip=.06f,RootTremor=.005f,RootKick=.12f,RootBack=.25f;
        /// <summary>Kept for the v1 checks' reference: the old bolt thrust and kick (the throw replaces them).</summary>
        public const float BoltThrust=.03f,BoltKick=.055f,BoltLift=.3f;
        /// <summary>The v2 casts' reach (m): the chain's arm out at the target, the storm's arm raised, the orbital's pointing arm.</summary>
        public const float Reach=.47f,Raise=.43f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Ease(float x)=>MeleeMath.Ease(Clamp01(x));
        private static float Decay(float age,float span)=>age<0?0:(float)Math.Exp(-age/Math.Max(1e-3f,span));
        // A recoil: eased in over `attack` (no one-frame snap), then decaying over `span`.
        private static float Kick(float age,float attack,float span)=>age<0?0:age<attack?Ease(age/attack):Decay(age-attack,span);
        private static readonly Vector3 Up=Vector3.UnitY,Side=Vector3.UnitX;

        /// <summary>One frame's ranged layer: the hold's weight, the hands' targets and turns (group space), the chest's coil and
        /// pitch, the hips' dip, and the presented state.</summary>
        public struct Frame
        {
            public float Weight,ChestPitch,ChestYaw,Dip;public Vector3 Right,Left;public Quaternion RightTurn,LeftTurn;public string State;
            /// <summary>How far each elbow swings out and back (the loaded arm's overhand set; 0 the IK's default bend).</summary>
            public float RightCock,LeftCock;
            public bool Live=>Weight>1e-4f;
        }
        /// <summary>Where the v2 casts point (group space, the rig's frame): the chain's first strike, the storm's centre, the
        /// orbital column's floor point now. Zero when unknown (the arm then points along the aim).</summary>
        public struct Aims{public Vector3 Chain,Storm,Orbital;public bool HasChain,HasStorm,HasOrbital;}
        /// <summary>The locomotion under the layer: the carry's travel weight, the gait's stride phase (rad) and the eased sprint.</summary>
        public struct Gait{public float Travel,Stride,Sprint;}
        private static float H(int n,int salt){unchecked{uint h=(uint)n*0x9e3779b1u^(uint)salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;return (h&0xffffff)/16777216f;}}

        /// <summary>
        /// The layer's frame for the view at `time`: `pitch` the view's (rad, + up; 0 in first person), `yieldTo` how much a melee
        /// take owns the frame (the guard, Q, E: the hold gives way), `lower` the sprint's weight, from the shoulders `shoulderL` /
        /// `shoulderR` (group space).
        /// </summary>
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,float lower,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR)=>
            Of(v,time,pitch,yieldTo,lower,firstPerson,reduced,shoulderL,shoulderR,default,default);
        /// <summary>As above; in first person `fistL` / `fistR` are where the view's stance holds the fists (group space).</summary>
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,float lower,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 fistL,Vector3 fistR)=>
            Of(v,time,pitch,yieldTo,new Gait{Sprint=lower,Travel=lower},firstPerson,reduced,shoulderL,shoulderR,fistL,fistR,default);
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,in Gait gait,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 fistL,Vector3 fistR,in Aims aims)
        {
            float travel=gait.Travel,stride=gait.Stride,sprint=gait.Sprint;
            var f=new Frame();float mode=v.ModeWeight(time),sw=v.SwitchU(time);
            if(mode<=1e-4f&&sw<0)return f;
            float hold=mode*(1-Clamp01(yieldTo));
            // Through a switch the arms take the hold from its start (melee to ranged) or keep it to its end (ranged to melee),
            // folding in to the chest at its middle.
            if(sw>=0)hold=Math.Max(hold,(1-Clamp01(yieldTo))*(v.Ranged?Ease(sw/.45f):1-Ease((sw-.55f)/.45f)));
            f.Weight=hold;if(hold<=1e-4f)return f;
            float p=MeleeMath.Clamp(pitch,-PitchMax,PitchMax);var d=new Vector3(0,(float)Math.Sin(p),(float)Math.Cos(p));var up=new Vector3(0,(float)Math.Cos(p),-(float)Math.Sin(p));
            // The loaded hand only half follows the pitch (it stays beside the head).
            float hp=p*.5f;var dh=new Vector3(0,(float)Math.Sin(hp),(float)Math.Cos(hp));var uh=new Vector3(0,(float)Math.Cos(hp),-(float)Math.Sin(hp));
            var mid=(shoulderL+shoulderR)*.5f;var turn=CapturedRig.Look(d,up);
            Vector3 o;float spread;
            if(firstPerson){var c=(fistL+fistR)*.5f;o=c+d*ViewReach+up*ViewLift;spread=Math.Max(.05f,Math.Abs(fistL.X-fistR.X)*.5f*ViewSpread);}
            else{o=mid-up*.05f+d*.46f;spread=.1f;}
            f.ChestPitch=-p*ChestPitch;f.State="cyber-ranged-aim";
            // ---- the stance and the throws (the newest throw window, v2's when its record is live) ----
            var th=ThrowOf(v,time);float glide=firstPerson?0:Clamp01(sprint);
            Vector3 Place(bool left,int at)
            {
                if(firstPerson)return ViewPlace(left,at,d,up,o,spread);
                // (The glide trails both hands from the shoulders' line, square to the run whatever the torso's twist.)
                var off=Offsets[at];bool loaded=at==LoadedAt;float sx=left?1:-1;var sh=at==GlideAt?mid+Side*(sx*ShoulderHalf):left?shoulderL:shoulderR;var dd=loaded?dh:d;var uu=loaded?uh:up;return sh+Side*(sx*off.X)+uu*off.Y+dd*off.Z;
            }
            // A hand's rest in the stance (loaded or forward), eased into the glide while sprinting.
            Vector3 Rest(bool left,bool loaded){var s=Place(left,loaded?LoadedAt:GuardAt);if(glide>1e-4f)s=Vector3.Lerp(s,Place(left,GlideAt),Ease(glide));return s;}
            float Coiled(int loaded)=>(loaded==0?-Coil:loaded==1?Coil:0)*(1-glide)*(1-.4f*Clamp01(travel));
            bool LoadedHand(int set,bool left)=>set==2||set==(left?1:0);
            var right=Rest(false,LoadedHand(th.Steady,false));var left=Rest(true,LoadedHand(th.Steady,true));
            var rTurn=HandTurn(d,up,false,LoadedHand(th.Steady,false)?1:0);var lTurn=HandTurn(d,up,true,LoadedHand(th.Steady,true)?1:0);
            f.ChestYaw=Coiled(th.Steady);f.ChestPitch+=StanceLean*(1-glide)-GlideLift*glide;
            // The v2 chain's reset: the loaded hand changes to the right over a quarter second.
            if(th.Reset>=0&&th.Reset<1)
            {
                var r0=Rest(false,LoadedHand(th.Before,false));var l0=Rest(true,LoadedHand(th.Before,true));float k=Ease(th.Reset);
                right=Vector3.Lerp(r0,right,k);left=Vector3.Lerp(l0,left,k);f.ChestYaw=MeleeMath.Lerp(Coiled(th.Before),f.ChestYaw,k);
            }
            if(th.Live)
            {
                float a=Math.Max(.02f,th.Release-th.At),age=time-th.At,w=Math.Max(2*a+.04f,th.Until-th.At);
                bool both=th.Hand==2;f.State=th.Kit2?(both?"cyber-plasma-overflow":"cyber-plasma-byte"):"cyber-bit-bolt";
                for(int h=0;h<2;h++)
                {
                    bool lh=h==1,thrower=LoadedHand(th.Hand,lh);var start=Rest(lh,LoadedHand(th.Hand,lh));var end=Rest(lh,LoadedHand(th.Next,lh));Vector3 at;Quaternion q;
                    if(thrower)
                    {
                        // Overhand (or, from the glide, sidearm) through the release point on the release, out to the extension.
                        var mark=Place(lh,both?GatherAt:MarkAt);var tip=Place(lh,both?PushAt:ExtendAt);if(glide>1e-4f)mark=Vector3.Lerp(mark,Vector3.Lerp(start,tip,.45f),Ease(glide)*.5f);
                        float s=age<a?.5f*(age/a)*(age/a):age<2*a?.5f+(age-a)/a-(age-a)*(age-a)/(2*a*a):1;
                        var ctrl=2*mark-.5f*(start+tip);float s1=1-s;var path=s1*s1*start+2*s*s1*ctrl+s*s*tip;
                        float back=Ease((age-2*a-.02f)/Math.Max(.02f,w-2*a-.02f));at=Vector3.Lerp(path,end,back);
                        float flick=age<a?Ease(age/a)*.6f:age<2*a?.6f+.4f*Ease((age-a)/a):1;q=Quaternion.Slerp(HandTurn(d,up,lh,1),HandTurn(d,up,lh,2),flick*(1-back));
                        q=Quaternion.Slerp(q,HandTurn(d,up,lh,LoadedHand(th.Next,lh)?1:0),back);
                    }
                    else
                    {
                        // The other hand loads as the thrower releases (the counter-arm of the next throw).
                        float k=Ease((age-a*.6f)/Math.Max(.05f,w*.85f-a*.6f));at=Vector3.Lerp(start,end,k);
                        q=Quaternion.Slerp(HandTurn(d,up,lh,0),HandTurn(d,up,lh,LoadedHand(th.Next,lh)?1:0),k);
                    }
                    if(lh){left=at;lTurn=q;}else{right=at;rTurn=q;}
                }
                // The chest: the extra coil into the release, the uncoil out to the extension (the throwing shoulder forward),
                // round to the next stance's coil; the Overflow leans back and drives in.
                float c0=Coiled(th.Hand),c1=Coiled(th.Next),sign=th.Hand==0?-1:th.Hand==1?1:0,g=1-glide;
                float yaw=age<a*.5f?c0+sign*Wind*g*Ease(age/(a*.5f)):age<2*a?MeleeMath.Lerp(c0+sign*Wind*g,-sign*Uncoil*g,Ease((age-a*.5f)/(1.5f*a))):MeleeMath.Lerp(-sign*Uncoil*g,c1,Ease((age-2*a)/Math.Max(.02f,w-2*a)));
                f.ChestYaw=yaw;
                if(both)f.ChestPitch+=(age<a?OverflowBack*Ease(age/a):age<2*a?MeleeMath.Lerp(OverflowBack,OverflowIn,Ease((age-a)/a)):OverflowIn*(1-Ease((age-2*a)/Math.Max(.02f,w-2*a))))*g;
                else f.ChestPitch+=.05f*Kick(age-a,.04f,.08f)*(1-Ease((age-2*a)/Math.Max(.02f,w-2*a)))*g;
            }
            else if(th.Settle>0)
            {
                // A cut throw eases back to the stance over its settle.
                f.State=th.Kit2?"cyber-plasma-byte":"cyber-bit-bolt";
            }
            // ---- the run's bob and the glide's swing (no throw owns the arms) ----
            if(!firstPerson&&travel>1e-3f)
            {
                float sp=stride,bob=RunBob*Clamp01(travel)*(1-glide);
                right+=up*(bob*(float)Math.Sin(sp))+d*(bob*.6f*(float)Math.Cos(sp));left+=up*(bob*(float)Math.Sin(sp+Math.PI))+d*(bob*.6f*(float)Math.Cos(sp+Math.PI));
                if(glide>1e-4f){float gs=GlideSwing*glide;right+=up*(gs*(float)Math.Sin(sp+.6f));left+=up*(gs*(float)Math.Sin(sp+.6f+Math.PI));}
            }
            if(glide>.5f&&!th.Live&&f.State=="cyber-ranged-aim")f.State="cyber-ranged-sprint";
            // ---- the reconfiguration: both forearms fold in across the chest, a glitch jolt at the middle, and out ----
            if(sw>=0)
            {
                // (In first person the fold stays in view: the fists meet in front of the camera.)
                float fold=(float)Math.Sin(Math.PI*Clamp01(sw));
                var inR=firstPerson?o-Side*.035f-up*.03f:mid+d*.16f-up*.12f+Side*.05f;var inL=firstPerson?o+Side*.035f-up*.02f:mid+d*.16f-up*.1f-Side*.05f;
                right=Vector3.Lerp(right,inR,fold);left=Vector3.Lerp(left,inL,fold);f.ChestYaw*=1-fold;
                if(!reduced&&sw>.4f&&sw<.62f){int step=(int)(time*24);var jolt=new Vector3(H(step,3)-.5f,H(step,5)-.5f,H(step,7)-.5f)*.035f;right+=jolt;left-=jolt;}
                rTurn=Quaternion.Slerp(rTurn,CapturedRig.Look(Side-d*.3f,up),fold*.7f);lTurn=Quaternion.Slerp(lTurn,CapturedRig.Look(-Side-d*.3f,up),fold*.7f);
                f.State="cyber-reconfigure";
            }
            // ---- v1's other casts on their windows (the newest live one; their settles ease back to the hold) ----
            int slot=Newest(v,time);
            if(slot>CyberRangedSpec.Bolt)
            {
                var ph=v.RPhase(slot,time);float rel=v.RRelease[slot],age=time-rel;f.ChestYaw*=1-Ease(ph.Part<2?ph.Age/.08f:1-ph.U);
                switch(slot)
                {
                    case CyberRangedSpec.Lance:
                    {
                        // The cannon arm cocks back to the shoulder over the windup, the guide hand forward; it thrusts at the
                        // release and recoils (the cock lets go over LanceSnap from the release, the thrust peaking there).
                        var cock=-d*LanceCock+up*.03f-Side*.03f;
                        if(ph.Part==0){float k=Ease(ph.U);right+=cock*k;left+=d*(.03f*k);}
                        else{float c=1-Ease(age/LanceSnap),k=Ease(age/LanceSnap)*(1-Ease((age-LanceSnap)/.16f));right+=cock*c+d*(LanceThrust*k)+up*(.02f*k);left+=d*(.03f*c);}
                        f.State="cyber-ping-lance";break;
                    }
                    case CyberRangedSpec.Field:
                    {
                        // The underhand lob: the left hand swings low and back past the hip, then forward and up to the release,
                        // both swings arcing about the shoulder (a pendulum in front of the hip, never through the body).
                        var low=mid+Side*.2f-up*LobBack-d*.04f;var high=mid+Side*.12f+d*.45f+up*.12f;float at=v.RAt[slot];
                        float back=Ease((time-at)/Math.Max(.01f,(rel-at-LobLead)*.9f)),fwd=Ease((time-(rel-LobLead))/LobSwing);
                        if(ph.Part<2)left=Swing(shoulderL,Swing(shoulderL,left,low,back),high,fwd);else left=Swing(shoulderL,high,left,Ease(ph.U));
                        f.State="cyber-lag-field";break;
                    }
                    case CyberRangedSpec.Swap:
                    {
                        // Tucked through the blink (the arms in, a dip), out again over the settle.
                        float k=ph.Part<2?Ease(ph.Age/.15f):1-Ease(ph.U);var tuck=firstPerson?o-up*.05f-d*.03f:mid+d*TuckIn-up*.1f;right=Vector3.Lerp(right,tuck-Side*.06f,k);left=Vector3.Lerp(left,tuck+Side*.06f,k);f.Dip=SwapDip*k;
                        f.State="cyber-proxy-swap";break;
                    }
                    case CyberRangedSpec.Root:
                    {
                        if(ph.Part==0)
                        {
                            // The charge: the emitters close together on the centre line, the body leaning in, a growing tremor
                            // (in first person a gap stays between them, the charge's light in it).
                            float k=Ease(ph.U*2.5f),gap=firstPerson?ViewGap:.025f;var meet=o-up*.02f;right=Vector3.Lerp(right,meet-Side*gap,k);left=Vector3.Lerp(left,meet+Side*gap,k);
                            if(!reduced){float tr=RootTremor*ph.U;var shake=up*(float)Math.Sin(time*55)*tr+Side*(float)Math.Sin(time*41+1)*tr;right+=shake;left+=shake;}
                            f.ChestPitch+=.08f*k;f.State="cyber-root-charge";
                        }
                        else
                        {
                            // The beam: both arms thrown back and up, the chest rocked back, settling to the hold.
                            float k=Kick(age,.05f,.12f);var meet=o-up*.02f;var kicked=meet-d*RootKick+up*(.1f);
                            float back=ph.Part==1?1:1-Ease(ph.U),gap=firstPerson?ViewGap:.025f;right=Vector3.Lerp(right,Vector3.Lerp(meet-Side*gap,kicked-Side*(gap+.015f),k),back);left=Vector3.Lerp(left,Vector3.Lerp(meet+Side*gap,kicked+Side*(gap+.015f),k),back);
                            f.ChestPitch+=-RootBack*k;f.State="cyber-root-fire";
                        }
                        break;
                    }
                }
                if(slot!=CyberRangedSpec.Bolt){rTurn=Quaternion.Slerp(rTurn,turn,Ease(ph.Part<2?ph.Age/.08f:1-ph.U));lTurn=Quaternion.Slerp(lTurn,turn,Ease(ph.Part<2?ph.Age/.08f:1-ph.U));}
            }
            // ---- v2's casts (Daisy Chain, Singularity, Packet Storm, Orbital Uplink) ----
            if(v.Kit2R.Live)Kit2(v.Kit2R,time,d,up,firstPerson,reduced,shoulderL,shoulderR,o,spread,aims,ref f,ref right,ref left,ref rTurn,ref lTurn);
            if(!firstPerson){Hands(v,time,out float lr,out float ll,out _,out _);f.RightCock=lr*(1-glide)*(1-Math.Max(0,sw>=0?(float)Math.Sin(Math.PI*Clamp01(sw)):0));f.LeftCock=ll*(1-glide)*(1-Math.Max(0,sw>=0?(float)Math.Sin(Math.PI*Clamp01(sw)):0));}
            f.Right=right;f.Left=left;f.RightTurn=rTurn;f.LeftTurn=lTurn;return f;
        }

        // ---- the throws ----
        private struct Throw{public bool Live,Kit2,Fired;public float At,Release,Until,Settle,Reset;public int Hand,Next,Steady,Before;}
        // (Reset: the v2 chain's reset blend, 0..1 over its quarter second; -1 outside it.)
        /// <summary>The newest throw window at `time` and the hands: the thrower set (0 right, 1 left, 2 both), the next throw's set,
        /// and the stance's loaded set (the next throw's, before a window starts: the stance is the loaded next hand).</summary>
        private static Throw ThrowOf(CyberLordView v,float time)
        {
            var t=new Throw{Hand=-1,Reset=-1};var k=v.Kit2R;
            if(k.Live)
            {
                const int P=CyberKit2Ranged.Plasma;t.Kit2=true;t.Fired=k.Released(P);t.At=k.At[P];t.Release=k.Release[P];t.Until=k.Until[P];
                bool seen=t.At>-90&&time>=t.At;t.Hand=k.Hand;t.Next=k.Shot<0?0:CyberKit2Spec.PlasmaHand(k.Shot+1);t.Live=seen&&time<t.Until;
                // After the chain resets (ComboReset past the throw) the right hand loads.
                float reset=t.Until+CyberKit2Spec.ComboReset;t.Steady=!seen||t.Live?t.Hand:time>=reset?0:t.Next;t.Before=t.Steady;
                if(seen&&!t.Live&&t.Next!=0&&time>=reset&&time<reset+.25f){t.Reset=(time-reset)/.25f;t.Before=t.Next;}
                if(seen&&!t.Live&&k.CutAt[P]>-90&&time<k.Recover[P]+.05f)t.Settle=1;
                return t;
            }
            const int B=CyberRangedSpec.Bolt;t.Fired=v.RReleased(B);t.At=v.RAt[B];t.Release=v.RRelease[B];t.Until=v.RUntil[B];
            bool on=t.At>-90&&time>=t.At;t.Hand=v.BoltHand;t.Next=1-v.BoltHand;t.Live=on&&time<t.Until;
            // Before the record's throw (a rewound clock) the stance loads that throw's hand; else the next one.
            t.Steady=on?(t.Live?t.Hand:t.Next):t.At>-90?t.Hand:t.Next;t.Before=t.Steady;
            return t;
        }
        /// <summary>
        /// The held fireballs (the effects draw them in the palms): how loaded each hand is at `time` (1 the next throw's hand
        /// beside the head, 0 the forward hand; eased as the pose eases) and how far its fireball has re-formed since it last
        /// threw (0 at its release .. 1). The same throw windows as the pose, so the fireball leaves the hand that throws it.
        /// </summary>
        public static void Hands(CyberLordView v,float time,out float loadR,out float loadL,out float formR,out float formL)
        {
            var th=ThrowOf(v,time);loadR=Load(th.Steady,false);loadL=Load(th.Steady,true);formR=formL=1;
            if(th.Reset>=0&&th.Reset<1){float k=Ease(th.Reset);loadR=MeleeMath.Lerp(Load(th.Before,false),loadR,k);loadL=MeleeMath.Lerp(Load(th.Before,true),loadL,k);}
            if(!(th.At>-90)||time<th.At)return;
            if(th.Live)
            {
                float a=Math.Max(.02f,th.Release-th.At),w=Math.Max(2*a+.04f,th.Until-th.At),k=Ease((time-th.At-a*.6f)/Math.Max(.05f,w*.85f-a*.6f));
                loadR=MeleeMath.Lerp(Load(th.Hand,false),Load(th.Next,false),Load(th.Hand,false)>0?Ease((time-th.At-2*a)/Math.Max(.02f,w-2*a)):k);
                loadL=MeleeMath.Lerp(Load(th.Hand,true),Load(th.Next,true),Load(th.Hand,true)>0?Ease((time-th.At-2*a)/Math.Max(.02f,w-2*a)):k);
            }
            // A cut before the release threw nothing: the fireball stays.
            if(time<th.Release||!th.Fired)return;float age=time-th.Release,form=Ease((age-.06f)/.22f);
            if(Load(th.Hand,false)>0)formR=form;if(Load(th.Hand,true)>0)formL=form;
        }
        private static float Load(int set,bool left)=>set==2||set==(left?1:0)?1:0;
        /// <summary>A hand's turn (group space): 0 the forward hand (knuckles up the aim, the back of the hand out), 1 loaded beside
        /// the head (knuckles up and forward, the fireball over them), 2 the release (knuckles along the aim, the palm turned down
        /// and in: the push).</summary>
        private static Quaternion HandTurn(Vector3 d,Vector3 up,bool left,int kind)
        {
            float sx=left?1:-1;
            switch(kind)
            {
                case 1:return CapturedRig.Look(Vector3.Normalize(d*.45f+up*.9f),Vector3.Normalize(-d*.6f+up*.2f+Side*(sx*.5f)));
                case 2:return CapturedRig.Look(d,Vector3.Normalize(up*.8f+Side*(sx*.6f)));
                default:return CapturedRig.Look(Vector3.Normalize(d+up*.35f),Vector3.Normalize(up+Side*(sx*.45f)));
            }
        }
        // First person: the hands placed from the view stance's fists (the throws push from the lower corner to the centre).
        private static Vector3 ViewPlace(bool left,int at,Vector3 d,Vector3 up,Vector3 o,float spread)
        {
            float sx=left?1:-1;var off=ViewOffsets[at];return o+Side*(sx*(spread-off.X))+up*off.Y+d*off.Z;
        }
        // From `a` to `b` by `k` along the arc about `pivot` (the direction turning on the great circle, the distance eased).
        private static Vector3 Swing(Vector3 pivot,Vector3 a,Vector3 b,float k)
        {
            var u=a-pivot;var w=b-pivot;float lu=u.Length(),lw=w.Length();if(lu<1e-4f||lw<1e-4f)return Vector3.Lerp(a,b,k);
            var turn=MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(u/lu,w/lw),k);return pivot+Vector3.Transform(u/lu,turn)*MeleeMath.Lerp(lu,lw,k);
        }

        // ---- v2's casts ----
        // A cast's weight over the stance: in over its first moments, held through the cast, out over its settle.
        private static float CastWeight(in CyberPhase ph,float rise=.07f)=>!ph.Live?0:ph.Part<2?Ease(ph.Age/rise):1-Ease(ph.U);
        // Where an arm points: from its shoulder toward `target` (kept in front of the body), `reach` out.
        private static Vector3 Point(Vector3 shoulder,Vector3 target,Vector3 d,float reach)
        {
            var dir=target-shoulder;if(dir.LengthSquared()<1e-4f)dir=d;dir=Vector3.Normalize(dir);if(dir.Z<-.1f){dir.Z=-.1f;dir=Vector3.Normalize(dir);}return shoulder+dir*reach;
        }
        private static void Kit2(CyberKit2RangedView k,float time,Vector3 d,Vector3 up,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 o,float spread,in Aims aims,
            ref Frame f,ref Vector3 right,ref Vector3 left,ref Quaternion rTurn,ref Quaternion lTurn)
        {
            int slot=k.Newest(time);if(slot<=CyberKit2Ranged.Plasma)return;var ph=k.Phase(slot,time);float w=CastWeight(ph,slot==CyberKit2Ranged.Chain||slot==CyberKit2Ranged.Singularity?.12f:slot==CyberKit2Ranged.Storm?.15f:.2f);if(w<=1e-4f)return;
            var mid=(shoulderL+shoulderR)*.5f;Vector3 r,l;Quaternion rq=CapturedRig.Look(d,up),lq=rq;float yaw=0,pitch=0;
            Vector3 ownR=firstPerson?o-Side*spread:shoulderR,ownL=firstPerson?o+Side*spread:shoulderL;float reach=firstPerson?.2f:Reach;
            switch(slot)
            {
                case CyberKit2Ranged.Chain:
                {
                    // The right arm thrown out at the first strike through the windup (the cast's weight carries it from the
                    // stance), the left hand bracing its forearm, a tremor through the hops.
                    var target=aims.HasChain&&!firstPerson?aims.Chain:ownR+d*4;var outR=Point(ownR,target,d,reach);
                    float k0=Ease(ph.Part==0?ph.U:1);r=outR;l=Vector3.Lerp(ownR,outR,.62f)+Side*.06f-up*.03f;
                    if(ph.Part>=1&&!reduced){float tr=.007f*(1-(ph.Part==2?ph.U:0));var shake=up*(float)Math.Sin(time*61)*tr+Side*(float)Math.Sin(time*47+1)*tr;r+=shake;l+=shake*.6f;}
                    var aim=Vector3.Normalize(outR-ownR);rq=CapturedRig.Look(aim,up);lq=CapturedRig.Look(Vector3.Normalize(aim-Side*.4f),up);yaw=.12f*k0;pitch=.04f*k0;f.State="cyber-daisy-chain";break;
                }
                case CyberKit2Ranged.Singularity:
                {
                    // The orb gathered between both hands low at the chest, then pushed out and up together, fingers spread.
                    var cup=firstPerson?o-up*.05f:mid+d*.24f-up*.14f;var push=firstPerson?o+d*.12f+up*.04f:mid+d*.42f+up*.06f;
                    float sp=firstPerson?spread*.5f:.075f;float rise=ph.Part==0?Ease(ph.U):1;var c=Vector3.Lerp(cup,cup+up*.05f-d*.04f,rise);
                    float out1=ph.Part==0?0:Ease(Math.Min(1,(time-k.Release[slot])/.12f));var at=Vector3.Lerp(c,push,out1);r=at-Side*(sp+.05f*out1);l=at+Side*(sp+.05f*out1);
                    rq=CapturedRig.Look(Vector3.Normalize(Side+d*(.4f+out1)),up);lq=CapturedRig.Look(Vector3.Normalize(-Side+d*(.4f+out1)),up);pitch=-.08f*rise*(1-out1)+.08f*out1;f.State="cyber-singularity";break;
                }
                case CyberKit2Ranged.Storm:
                {
                    // The right arm raised to the sky over the windup, then brought down to point at the ring as the drops are called.
                    var sky=firstPerson?ownR+up*.18f+d*.06f:shoulderR+up*Raise+d*.06f-Side*.02f;var target=aims.HasStorm&&!firstPerson?aims.Storm:ownR+d*6-up*1.2f;var down=Point(ownR,target,d,reach);
                    float raise=ph.Part==0?Ease(ph.U/.8f):1,cast=ph.Part==0?0:Ease(Math.Min(1,(time-k.Release[slot])/.1f));r=Vector3.Lerp(Vector3.Lerp(right,sky,raise),down,cast);
                    l=Vector3.Lerp(left,firstPerson?o+Side*spread-up*.04f:mid+d*.18f-up*.1f+Side*.1f,.8f);
                    rq=CapturedRig.Look(Vector3.Normalize(Vector3.Lerp(up,Vector3.Normalize(down-ownR),cast)),Vector3.Normalize(-d+up*.1f));pitch=-.1f*raise*(1-cast)+.1f*cast;yaw=-.1f*raise;f.State="cyber-packet-storm";break;
                }
                default:
                {
                    // Orbital Uplink: the left hand raised to the sky (the uplink), the right arm pointing at the call-down point and
                    // then at the column, following it as it chases the aim.
                    var target=aims.HasOrbital&&!firstPerson?aims.Orbital:ownR+d*8-up*1.4f;r=Point(ownR,target,d,reach);
                    l=firstPerson?ownL+up*.16f+d*.02f:shoulderL+up*Raise+d*.04f+Side*.05f;
                    if(ph.Part==0){float raise=Ease(ph.U/.5f);l=Vector3.Lerp(left,l,raise);r=Vector3.Lerp(right,r,Ease((ph.U-.2f)/.6f));}
                    if(!reduced){float hum=.004f;l+=up*(hum*(float)Math.Sin(time*29));}
                    rq=CapturedRig.Look(Vector3.Normalize(r-ownR),up);lq=CapturedRig.Look(up,Vector3.Normalize(-d));yaw=.1f;pitch=.05f;f.State=ph.Part==0?"cyber-orbital-call":"cyber-orbital-steer";break;
                }
            }
            right=Vector3.Lerp(right,r,w);left=Vector3.Lerp(left,l,w);rTurn=Quaternion.Slerp(rTurn,rq,w);lTurn=Quaternion.Slerp(lTurn,lq,w);
            f.ChestYaw=MeleeMath.Lerp(f.ChestYaw,yaw,w);f.ChestPitch+=pitch*w;
        }

        /// <summary>The newest v1 ranged cast live at `time` (to the end of its settle), or -1.</summary>
        public static int Newest(CyberLordView v,float time)
        {
            int newest=-1;float at=-100;
            for(int s=0;s<CyberRangedSpec.Slots;s++){float start=v.RAt[s],end=Math.Max(v.RUntil[s],v.RRecover[s]);if(start>-90&&time>=start&&time<end&&start>at){newest=s;at=start;}}
            return newest;
        }
        /// <summary>
        /// The frame onto the posed rig: the chest's coil and pitch (the head kept on the aim's heading), the swap's dip, then both
        /// arms to their targets carried round with the coil (chest-space IK, the wrists turned) at the hold's weight. Returns the
        /// frame's state (null when it does not show).
        /// </summary>
        public static string Apply(MeleeRig r,in Frame f)
        {
            if(!f.Live)return null;
            float yaw=f.ChestYaw*f.Weight,pitch=f.ChestPitch*f.Weight;var cq0=r.ChainQuat(CapturedRig.Chest,-1);var carry=Quaternion.Identity;
            if(Math.Abs(yaw)>1e-5f||Math.Abs(pitch)>1e-5f)
            {
                var ry=MeleeMath.AxisAngle(MeleeMath.Y,yaw);var rx=MeleeMath.AxisAngle(MeleeMath.X,pitch);var t=ry*rx;r.Multiply(CapturedRig.Chest,t);
                // The neck takes the coil back out (the head stays on the aim; it still follows the chest's pitch).
                r.Premultiply(CapturedRig.Neck,MeleeMath.Inverse(t)*rx);
                carry=Quaternion.Normalize(cq0*ry*MeleeMath.Inverse(cq0));
            }
            if(f.Dip>1e-5f){var b=r.P[CapturedRig.Body];r.P[CapturedRig.Body]=new Vector3(b.X,b.Y-f.Dip*f.Weight,b.Z);}
            var cq=r.ChainQuat(CapturedRig.Chest,-1);var cp=r.ChainPoint(CapturedRig.Chest,-1);var inv=MeleeMath.Inverse(cq);
            var rt=cp+Vector3.Transform(f.Right-cp,carry);var lt=cp+Vector3.Transform(f.Left-cp,carry);
            r.Hand(false,Vector3.Transform(rt-cp,inv),inv*carry*f.RightTurn,f.Weight,Pole(false,f.RightCock));r.Hand(true,Vector3.Transform(lt-cp,inv),inv*carry*f.LeftTurn,f.Weight,Pole(true,f.LeftCock));
            return f.State;
        }
        // The elbow's bend direction (the arm's frame): the IK's default (out, down and a little back), swung out and back for
        // the loaded arm's overhand set.
        private static Vector3? Pole(bool left,float cock)
        {
            if(cock<=1e-3f)return null;float sx=left?1:-1;var a=new Vector3(sx,-.65f,-.3f);var b=new Vector3(sx*.75f,-.25f,-.85f);return Vector3.Lerp(a,b,Clamp01(cock));
        }
        /// <summary>The layer in one call: the shoulders from the posed rig, the frame, applied (`lower`: the sprint's weight).</summary>
        public static string Layer(MeleeRig r,CyberLordView v,float time,float pitch,float yieldTo,float lower,bool firstPerson,bool reduced)=>
            Layer(r,v,time,pitch,yieldTo,new Gait{Sprint=lower,Travel=lower},firstPerson,reduced);
        public static string Layer(MeleeRig r,CyberLordView v,float time,float pitch,float yieldTo,in Gait gait,bool firstPerson,bool reduced)
        {
            var aims=new Aims();var k=v.Kit2R;
            if(k.Live&&!firstPerson)
            {
                if(k.ChainCount>0){aims.Chain=ToGroup(r,k.ChainPoints[0]);aims.HasChain=true;}
                if(k.At[CyberKit2Ranged.Storm]>-90){aims.Storm=ToGroup(r,k.StormCentre);aims.HasStorm=true;}
                if(k.At[CyberKit2Ranged.Orbital]>-90){aims.Orbital=ToGroup(r,time<k.Release[CyberKit2Ranged.Orbital]?k.OrbitalCentre:k.OrbitalAt(time));aims.HasOrbital=true;}
            }
            var f=Of(v,time,pitch,yieldTo,gait,firstPerson,reduced,r.ChainPoint(CapturedRig.LArm,-1),r.ChainPoint(CapturedRig.RArm,-1),
                firstPerson?r.ChainPoint(CapturedRig.LHand,-1):default,firstPerson?r.ChainPoint(CapturedRig.RHand,-1):default,aims);
            return Apply(r,f);
        }
        /// <summary>A world point (Unity metres) in rig `r`'s group frame (the source frame: x mirrored, about the group's origin
        /// and heading).</summary>
        public static Vector3 ToGroup(MeleeRig r,Vector3 world)
        {
            var src=new Vector3(-world.X,world.Y,world.Z)-r.GroupPosition;return Vector3.Transform(src,MeleeMath.AxisAngle(MeleeMath.Y,-r.GroupYaw));
        }
    }
}
