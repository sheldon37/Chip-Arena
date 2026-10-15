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
    /// - The stance (owner 2026-10-15: "For ranged, lets change it to something similar to their sprint animation (arms back,
    ///   curved forward a bit more like their holding energy in their hands)"): both arms swept back and out from the
    ///   shoulders, the elbows bent behind so the forearms curve forward, the palms cupped up and forward round a plasma
    ///   fireball each (the effects draw them); the chest open and a little forward, with a slow breath.
    /// - The throws (v2's Plasma Byte; v1's Bit Bolt window on a v1 record): the throwing hand sweeps forward from behind the
    ///   hip, accelerating through the release point on the replicated release (following the aim's pitch), decelerating out to
    ///   full extension, then swings back to the stance as the chest comes round; the other hand holds its energy, drawn back a
    ///   little. Every window starts and ends on the stance, so throws chain at any interval and rate with no pop. The
    ///   Overflow (every third v2 throw) sweeps both hands from behind into a joined push.
    /// - The glide (the ranged sprint): a leaning glide with both arms swept back and out, the palms trailing (the effects stream
    ///   energy off them), on the carry's eased sprint ramp. A throw from the glide swings forward from behind and back.
    /// - v1's other casts (Ping Lance, Lag Field, Proxy Swap, Root Access) and v2's (Daisy Chain, Singularity, Packet Storm,
    ///   Orbital Uplink, the last steering the column with a pointing arm) on their replicated windows, swung in from the
    ///   stance about the shoulders and back out over their settles; a cast still settling when the next one or a throw starts
    ///   hands over to it; a cut throw or cast settles back. The key-5 reconfiguration folds the forearms in to the chest.
    /// A melee take still owning the frame (the guard, Q, E) yields the layer. Pure functions of the view, the clock and the rig
    /// the route posed; nothing here reaches the authority, hit timing, hitboxes, damage, cooldowns or movement. No allocation.
    /// </summary>
    public static class CyberRangedPose
    {
        /// <summary>The first-person view's hold (from where the view's stance holds the fists, in its lower half): reached ahead,
        /// drawn in toward the centre to ViewSpread of their spread, lifted; the gap Root Access keeps; the share of the pitch the
        /// chest takes; the largest pitch followed.</summary>
        public const float ViewReach=.07f,ViewSpread=.72f,ViewLift=.03f,ViewGap=.11f,ChestPitch=.3f,PitchMax=1.1f;
        /// <summary>The stance and the throw (m from each hand's own shoulder: x out from the body, y up, z forward): the held
        /// energy (Hold, in the body's frame, lifted HoldLift x sin(pitch) with the aim), the release point the hand passes on
        /// the release (half way along the sweep, in front of the chest) and full extension (in the aim's frame), the Overflow's
        /// hands closing at the release and joined in their push, the glide's trailing hand (from the shoulders' line).</summary>
        public static readonly Vector3 Hold=new Vector3(.24f,-.29f,-.1f),ReleaseAt=new Vector3(.02f,-.16f,.2f),Extend=new Vector3(-.13f,-.02f,.48f),
            Gather=new Vector3(-.04f,-.15f,.2f),Push=new Vector3(-.27f,-.04f,.45f),Glide=new Vector3(.24f,-.26f,-.36f);
        /// <summary>Half the shoulders' span (the rig's clavicle and shoulder offsets).</summary>
        public const float ShoulderHalf=.345f;
        private const int HoldAt=0,MarkAt=1,ExtendAt=2,GatherAt=3,PushAt=4,GlideAt=5;
        private static readonly Vector3[] Offsets={Hold,ReleaseAt,Extend,Gather,Push,Glide};
        /// <summary>First person (the view's frame from where its stance holds each fist: x toward the centre, y up, z along the
        /// aim; the Overflow's two from the centre line): the held energy low at the screen's edges, the release point, the
        /// extension at the centre, the Overflow's joined hands and their push.</summary>
        public static readonly Vector3[] ViewOffsets={new Vector3(-.25f,-.12f,-.12f),new Vector3(.04f,.04f,.1f),new Vector3(.1f,.06f,.26f),new Vector3(.035f,.02f,.11f),new Vector3(.04f,.05f,.25f),new Vector3(0,-.04f,-.02f)};
        /// <summary>The stance: the palms' share of the pitch and the held energy's lift with it (m), how much of the bladed melee
        /// stance's twist the chest turns out (standing; the gait keeps its own swing on the move), its lean into the aim (rad) and
        /// its breath (rad/s, rad, m). The throw: its wind and uncoil about the spine (the throwing shoulder back, then forward),
        /// the other hand's draw back (m), the sweep's pace through the release (m per unit of the path) and the power of its
        /// clock, the Overflow's lean back and in. The glide's lift and stride swing, the run's bob, and a cut throw's or cast's
        /// settle back to the stance (s).</summary>
        public const float HoldPitch=.3f,HoldLift=.1f,SquareShare=.85f,StanceLean=.07f,BreathRate=1.85f,BreathPitch=.018f,BreathLift=.012f,Wind=.06f,Uncoil=.26f,OffPull=.05f,SweepPace=.42f,SweepPower=1.3f,
            OverflowBack=-.1f,OverflowIn=.14f,GlideLift=.3f,GlideSwing=.035f,RunBob=.016f,CutSettle=.15f;
        /// <summary>v1's other casts' motion (m, rad): the lance's cock and thrust, the lob's reach back and up, the swap's tuck and
        /// dip, the beam's converge, tremor and recoil (thrown back and up, the chest back). They play from V1Hold (both hands
        /// out ahead of the chest, their look before the cyber mage), eased in from the stance.</summary>
        public const float LanceCock=.14f,LanceThrust=.06f,LanceSnap=.06f,LobBack=.26f,LobLead=.045f,LobSwing=.17f,TuckIn=.14f,SwapDip=.06f,RootTremor=.005f,RootKick=.12f,RootBack=.25f;
        public static readonly Vector3 V1Hold=new Vector3(-.16f,-.01f,.36f);
        /// <summary>Kept for the v1 checks' reference: the old bolt thrust and kick (the throw replaces them).</summary>
        public const float BoltThrust=.03f,BoltKick=.055f,BoltLift=.3f;
        /// <summary>The v2 casts' reach (m): the chain's arm out at the target, the storm's arm raised, the orbital's pointing arm.</summary>
        public const float Reach=.47f,Raise=.43f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Ease(float x)=>MeleeMath.Ease(Clamp01(x));
        // The cubic smoothstep: gentler at its middle than Ease (the throw's return, quick under Overclock).
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float Decay(float age,float span)=>age<0?0:(float)Math.Exp(-age/Math.Max(1e-3f,span));
        // A recoil: eased in over `attack` (no one-frame snap), then decaying over `span`.
        private static float Kick(float age,float attack,float span)=>age<0?0:age<attack?Ease(age/attack):Decay(age-attack,span);
        private static readonly Vector3 Up=Vector3.UnitY,Side=Vector3.UnitX;
        private static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f?v/l:fallback;}
        // A cubic Hermite segment (positions and tangents per unit of u).
        private static Vector3 Hermite(Vector3 p0,Vector3 t0,Vector3 p1,Vector3 t1,float u){float u2=u*u,u3=u2*u;return p0*(2*u3-3*u2+1)+t0*(u3-2*u2+u)+p1*(-2*u3+3*u2)+t1*(u3-u2);}

        /// <summary>One frame's ranged layer: the hold's weight, the hands' targets and turns (group space), the chest's coil and
        /// pitch, the hips' dip, and the presented state.</summary>
        public struct Frame
        {
            public float Weight,ChestPitch,ChestYaw,Dip;public Vector3 Right,Left;
            /// <summary>The chest's turn back toward square (rad about the spine; the hands are already placed from the squared
            /// shoulders, so it is not carried onto them as ChestYaw is).</summary>
            public float Square;public Quaternion RightTurn,LeftTurn;public string State;
            /// <summary>How far each elbow is swept back behind the body (the stance's held energy; 0 the IK's default bend).</summary>
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
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,in Gait gait,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 fistL,Vector3 fistR,in Aims aims)=>
            Of(v,time,pitch,yieldTo,gait,firstPerson,reduced,shoulderL,shoulderR,fistL,fistR,aims,(shoulderL+shoulderR)*.5f,Quaternion.Identity);
        /// <summary>As above, with the posed chest (group space: its joint and turn) the stance squares about.</summary>
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,in Gait gait,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 fistL,Vector3 fistR,in Aims aims,Vector3 chestAt,Quaternion chestTurn)
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
            // The held energy only rises and falls a little with the pitch (the arms stay swept back; the palms take a share of
            // it); the throw follows all of it.
            float hp=p*HoldPitch,lift=firstPerson?0:HoldLift*(float)Math.Sin(p);var dh=new Vector3(0,(float)Math.Sin(hp),(float)Math.Cos(hp));var uh=new Vector3(0,(float)Math.Cos(hp),-(float)Math.Sin(hp));
            float glide=firstPerson?0:Clamp01(sprint),eg=Ease(glide);
            // The chest opened square to the aim (the melee stance is bladed): the shoulders' line turned back toward square,
            // standing (the gait's own swing is kept on the move); the hands are placed from the squared shoulders.
            float square=firstPerson?0:(float)Math.Atan2(shoulderL.Z-shoulderR.Z,shoulderL.X-shoulderR.X)*SquareShare*(1-Clamp01(travel))*(1-glide);f.Square=square;
            if(Math.Abs(square)>1e-5f)
            {
                var sq=Quaternion.Normalize(chestTurn*MeleeMath.AxisAngle(MeleeMath.Y,square*hold)*MeleeMath.Inverse(chestTurn));
                shoulderL=chestAt+Vector3.Transform(shoulderL-chestAt,sq);shoulderR=chestAt+Vector3.Transform(shoulderR-chestAt,sq);
            }
            var mid=(shoulderL+shoulderR)*.5f;var turn=CapturedRig.Look(d,up);
            Vector3 o;float spread;
            if(firstPerson){var c=(fistL+fistR)*.5f;o=c+d*ViewReach+up*ViewLift;spread=Math.Max(.05f,Math.Abs(fistL.X-fistR.X)*.5f*ViewSpread);dh=d;uh=up;}
            else{o=mid-up*.05f+d*.46f;spread=.1f;}
            f.ChestPitch=-p*ChestPitch;f.State="cyber-ranged-aim";
            // ---- the stance and the throws (the newest throw window, v2's when its record is live) ----
            var th=ThrowOf(v,time);
            // The slow breath: the chest rising and the held energy floating with it (less on the move, none in the glide).
            float breath=(float)Math.Sin(time*BreathRate)*(1-glide)*(1-.6f*Clamp01(travel));
            Vector3 Place(bool left,int at)
            {
                if(firstPerson)return ViewPlace(left,at,d,up,o,spread);
                // (The glide trails both hands from the shoulders' line, square to the run whatever the torso's twist.)
                var off=Offsets[at];float sx=left?1:-1;var sh=at==GlideAt?mid+Side*(sx*ShoulderHalf):left?shoulderL:shoulderR;
                return at==HoldAt?sh+new Vector3(sx*off.X,off.Y+lift,off.Z):sh+Side*(sx*off.X)+up*off.Y+d*off.Z;
            }
            // A hand's rest: the held energy, eased into the glide while sprinting; its turn cupped round the energy (the glide's
            // trailing hands keep the turns they had).
            Vector3 Rest(bool left){var s=Place(left,HoldAt)+Up*(BreathLift*breath);if(glide>1e-4f)s=Vector3.Lerp(s,Place(left,GlideAt),eg);return s;}
            Quaternion RestTurn(bool left)
            {
                var q=HandTurn(dh,uh,left,Cup);if(glide<=1e-4f)return q;
                var gq=HandTurn(d,up,left,LoadedHand(th.Steady,left)?GlideLead:GlideTrail);
                if(th.Reset>=0&&th.Reset<1)gq=Quaternion.Slerp(HandTurn(d,up,left,LoadedHand(th.Before,left)?GlideLead:GlideTrail),gq,Ease(th.Reset));
                return Quaternion.Slerp(q,gq,eg);
            }
            var right=Rest(false);var left=Rest(true);var rTurn=RestTurn(false);var lTurn=RestTurn(true);float cockR=1,cockL=1;
            f.ChestPitch+=(StanceLean+BreathPitch*breath)*(1-glide)-GlideLift*glide;
            if(th.Live||th.Settle>0)
            {
                // The pose the throw inherits: the stance, or a v2 cast still settling when it began (the throwing hand sweeps on
                // from where that cast left it; the other hand settles on with the cast below).
                Vector3 fromR=right,fromL=left;Quaternion fromRq=rTurn,fromLq=lTurn;float castR=0,castL=0;
                if(th.Kit2&&v.Kit2R.Live){var scratch=new Frame();Kit2(v.Kit2R,th.At,d,up,firstPerson,reduced,shoulderL,shoulderR,o,spread,aims,th,false,ref scratch,ref fromR,ref fromL,ref fromRq,ref fromLq,out castR,out castL);}
                // The throw at its age (a cut throw: frozen at its cut, easing back to the stance over CutSettle).
                float a=Math.Max(.02f,th.Release-th.At),w=Math.Max(2*a+.04f,th.Span),age=th.Live?time-th.At:Math.Max(0,th.Until-th.At),k=th.Live?0:Ease(th.Settle);
                bool both=th.Hand==2;f.State=th.Kit2?(both?"cyber-plasma-overflow":"cyber-plasma-byte"):"cyber-bit-bolt";
                float back=Smooth((age-2*a)/Math.Max(.02f,w-2*a)),bell=Ease(age/a)*(1-Ease((age-2*a)/Math.Max(.02f,w-2*a)));
                for(int h=0;h<2;h++)
                {
                    bool lh=h==1;var home=lh?left:right;var homeTurn=lh?lTurn:rTurn;Vector3 at;Quaternion q;float cock;
                    if(LoadedHand(th.Hand,lh))
                    {
                        // From behind, forward through the release point on the release (accelerating into it), out to the extension
                        // one windup later (decelerating): two Hermite segments sharing the release's tangent on mirrored
                        // SweepPower clocks (no kink; the speed peaks on the release, broad enough to show on more than one frame
                        // even under Overclock), then swung back to the stance about the shoulder.
                        var start=lh?fromL:fromR;var rest=lh?fromLq:fromRq;
                        var mark=Place(lh,both?GatherAt:MarkAt);var tip=Place(lh,both?PushAt:ExtendAt);if(glide>1e-4f)mark=Vector3.Lerp(mark,Vector3.Lerp(start,tip,.45f),eg*.5f);
                        float sx=lh?1:-1;var t0=dh*.25f+Side*(sx*.05f);var tm=Unit(Unit(mark-start,dh)*.4f+Unit(tip-mark,d)*.6f,d)*Math.Min(SweepPace,2.6f*Vector3.Distance(tip,mark));
                        Vector3 path;
                        if(age<a)path=Hermite(start,t0,mark,tm,(float)Math.Pow(age/a,SweepPower));
                        else if(age<2*a)path=Hermite(mark,tm,tip,Vector3.Zero,1-(float)Math.Pow(1-(age-a)/a,SweepPower));
                        else path=tip;
                        at=firstPerson?Vector3.Lerp(path,home,back):Swing(lh?shoulderL:shoulderR,path,home,back);
                        // The palm turns from cupping the energy to pushing it at the aim through the release, along the aim at the
                        // extension, and back; the elbow comes forward under the throw.
                        float push=Ease(age/a),ext=Ease((age-a)/a);q=Quaternion.Slerp(Quaternion.Slerp(rest,HandTurn(d,up,lh,both?PushBoth:PushOne),push),HandTurn(d,up,lh,Thrust),ext*.55f);
                        q=Quaternion.Slerp(q,homeTurn,back);cock=MeleeMath.Lerp((1-Ease(age/(1.5f*a)))*(1-(lh?castL:castR)),1,back);
                    }
                    else
                    {
                        // The other hand keeps its energy, drawn back a little against the throw.
                        at=home-dh*(OffPull*bell)+uh*(.01f*bell);q=homeTurn;cock=1;
                    }
                    if(lh){left=Vector3.Lerp(at,left,k);lTurn=Quaternion.Slerp(q,lTurn,k);cockL=MeleeMath.Lerp(cock,1,k);}else{right=Vector3.Lerp(at,right,k);rTurn=Quaternion.Slerp(q,rTurn,k);cockR=MeleeMath.Lerp(cock,1,k);}
                }
                // The chest: winds the throwing shoulder back into the sweep, uncoils it forward through the release, comes back
                // square; the Overflow leans back and drives in.
                float sign=th.Hand==0?-1:th.Hand==1?1:0,g=1-glide;
                float yaw=age<a*.5f?sign*Wind*g*Ease(age/(a*.5f)):age<2*a?MeleeMath.Lerp(sign*Wind*g,-sign*Uncoil*g,Ease((age-a*.5f)/(1.5f*a))):MeleeMath.Lerp(-sign*Uncoil*g,0,Ease((age-2*a)/Math.Max(.02f,w-2*a)));
                float lean=both?(age<a?OverflowBack*Ease(age/a):age<2*a?MeleeMath.Lerp(OverflowBack,OverflowIn,Ease((age-a)/a)):OverflowIn*(1-Ease((age-2*a)/Math.Max(.02f,w-2*a))))*g
                    :.05f*Kick(age-a,.04f,.08f)*(1-Ease((age-2*a)/Math.Max(.02f,w-2*a)))*g;
                f.ChestYaw=yaw*(1-k);f.ChestPitch+=lean*(1-k);
            }
            // ---- the run's bob and the glide's swing ----
            if(!firstPerson&&travel>1e-3f)
            {
                float sp=stride,bob=RunBob*Clamp01(travel)*(1-glide);
                right+=up*(bob*(float)Math.Sin(sp))+d*(bob*.6f*(float)Math.Cos(sp));left+=up*(bob*(float)Math.Sin(sp+Math.PI))+d*(bob*.6f*(float)Math.Cos(sp+Math.PI));
                if(glide>1e-4f){float gs=GlideSwing*glide;right+=up*(gs*(float)Math.Sin(sp+.6f));left+=up*(gs*(float)Math.Sin(sp+.6f+Math.PI));}
            }
            if(glide>.5f&&!th.Live&&f.State=="cyber-ranged-aim")f.State="cyber-ranged-sprint";
            // ---- the reconfiguration: both forearms fold in across the chest, a glitch jolt at the middle, and out ----
            float fold=0;
            if(sw>=0)
            {
                // (In first person the fold stays in view: the fists meet in front of the camera.)
                fold=(float)Math.Sin(Math.PI*Clamp01(sw));
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
                var ph=v.RPhase(slot,time);float rel=v.RRelease[slot],age=time-rel,in1=Ease(ph.Part<2?ph.Age/.12f:1-ph.U);f.ChestYaw*=1-Ease(ph.Part<2?ph.Age/.08f:1-ph.U);
                // (They play from both hands out ahead of the chest, as before the cyber mage, swung in from the stance.)
                if(firstPerson){right=Vector3.Lerp(right,ViewPlace(false,MarkAt,d,up,o,spread),in1);left=Vector3.Lerp(left,ViewPlace(true,MarkAt,d,up,o,spread),in1);}
                else{right=Swing(shoulderR,right,shoulderR-Side*V1Hold.X+up*V1Hold.Y+d*V1Hold.Z,in1);left=Swing(shoulderL,left,shoulderL+Side*V1Hold.X+up*V1Hold.Y+d*V1Hold.Z,in1);}
                cockR*=1-in1;cockL*=1-in1;
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
                rTurn=Quaternion.Slerp(rTurn,turn,Ease(ph.Part<2?ph.Age/.08f:1-ph.U));lTurn=Quaternion.Slerp(lTurn,turn,Ease(ph.Part<2?ph.Age/.08f:1-ph.U));
            }
            // ---- v2's casts (Daisy Chain, Singularity, Packet Storm, Orbital Uplink) ----
            if(v.Kit2R.Live){Kit2(v.Kit2R,time,d,up,firstPerson,reduced,shoulderL,shoulderR,o,spread,aims,th,true,ref f,ref right,ref left,ref rTurn,ref lTurn,out float cwR,out float cwL);cockR*=1-cwR;cockL*=1-cwL;}
            if(!firstPerson){float keep=(1-glide)*(1-fold);f.RightCock=cockR*keep;f.LeftCock=cockL*keep;}
            f.Right=right;f.Left=left;f.RightTurn=rTurn;f.LeftTurn=lTurn;return f;
        }

        // ---- the throws ----
        private struct Throw{public bool Live,Kit2,Fired;public float At,Release,Until,Span,Settle,Reset;public int Hand,Next,Steady,Before;}
        // (Reset: the v2 chain's reset blend, 0..1 over its quarter second; -1 outside it. Settle: a cut throw's ease back to the
        // stance, 0..1 over CutSettle from its cut; 0 otherwise. Span: the window's length as it began, so a cut throw's pose
        // freezes where it was.)
        /// <summary>The newest throw window at `time` and the hands: the thrower set (0 right, 1 left, 2 both), the next throw's set,
        /// and the next throw's set as of `time` (Steady: the glide's leading hand).</summary>
        private static Throw ThrowOf(CyberLordView v,float time)
        {
            var t=new Throw{Hand=-1,Reset=-1};var k=v.Kit2R;
            if(k.Live)
            {
                const int P=CyberKit2Ranged.Plasma;t.Kit2=true;t.Fired=k.Released(P);t.At=k.At[P];t.Release=k.Release[P];t.Until=k.Until[P];
                bool seen=t.At>-90&&time>=t.At;t.Hand=k.Hand;t.Next=k.Shot<0?0:CyberKit2Spec.PlasmaHand(k.Shot+1);t.Live=seen&&time<t.Until;
                // After the chain resets (ComboReset past the throw) the right hand leads.
                float reset=t.Until+CyberKit2Spec.ComboReset;t.Steady=!seen||t.Live?t.Hand:time>=reset?0:t.Next;t.Before=t.Steady;
                if(seen&&!t.Live&&t.Next!=0&&time>=reset&&time<reset+.25f){t.Reset=(time-reset)/.25f;t.Before=t.Next;}
                if(seen&&!t.Live&&k.CutAt[P]>-90&&time<t.Until+CutSettle)t.Settle=Math.Max(1e-4f,(time-t.Until)/CutSettle);
                // A cut rewrites the window's end (and, before the release, its release); the throw's Ready keeps its length at
                // the attack rate it began with, and the windup is the spec's share of it.
                t.Span=t.Until-t.At;float span=k.Ready[P]-t.At;
                if(k.CutAt[P]>-90&&span>.02f){t.Span=span;if(!t.Fired)t.Release=t.At+span*(CyberKit2Spec.PlasmaWindup/CyberKit2Spec.PlasmaInterval);}
                return t;
            }
            const int B=CyberRangedSpec.Bolt;t.Fired=v.RReleased(B);t.At=v.RAt[B];t.Release=v.RRelease[B];t.Until=v.RUntil[B];
            bool on=t.At>-90&&time>=t.At;t.Hand=v.BoltHand;t.Next=1-v.BoltHand;t.Live=on&&time<t.Until;
            // Before the record's throw (a rewound clock) the stance leads with that throw's hand; else the next one.
            t.Steady=on?(t.Live?t.Hand:t.Next):t.At>-90?t.Hand:t.Next;t.Before=t.Steady;t.Span=t.Until-t.At;
            return t;
        }
        private static bool LoadedHand(int set,bool left)=>set==2||set==(left?1:0);
        /// <summary>
        /// The held fireballs (the effects draw them over the palms): how much energy each hand holds at `time` (1: both hands
        /// hold it in the stance and through a throw until its release) and how far its fireball has re-formed since it last threw
        /// (0 at its release .. 1, quickly enough under any attack rate to be whole again by the window's end). The same throw
        /// windows as the pose, so the fireball leaves the hand that throws it.
        /// </summary>
        public static void Hands(CyberLordView v,float time,out float loadR,out float loadL,out float formR,out float formL)
        {
            var th=ThrowOf(v,time);loadR=loadL=1;formR=formL=1;
            // A cut before the release threw nothing: the fireball stays.
            if(!(th.At>-90)||time<th.Release||!th.Fired)return;
            float age=time-th.Release,span=MeleeMath.Clamp(th.Until-th.Release-.06f,.08f,.22f),form=Ease((age-.04f)/span);
            if(LoadedHand(th.Hand,false))formR=form;if(LoadedHand(th.Hand,true))formL=form;
        }
        // The hands' turns: cupping the energy (palms up and forward, the fingers forward and a little in), pushing it at the aim
        // (one hand; both hands joined), thrust along the aim at the extension (the palm turned down and in), and the glide's
        // leading and trailing hands (as the glide has always held them).
        private const int Cup=0,PushOne=1,PushBoth=2,Thrust=3,GlideLead=4,GlideTrail=5;
        private static Quaternion HandTurn(Vector3 d,Vector3 up,bool left,int kind)
        {
            float sx=left?1:-1;
            switch(kind)
            {
                case Cup:return CapturedRig.Look(Vector3.Normalize(d*.85f-Side*(sx*.3f)+up*.1f),Vector3.Normalize(-up*.9f+d*.25f+Side*(sx*.35f)));
                case PushOne:return CapturedRig.Look(Vector3.Normalize(d*.7f+up*.55f-Side*(sx*.15f)),Vector3.Normalize(-d*.8f+up*.4f+Side*(sx*.2f)));
                case PushBoth:return CapturedRig.Look(Vector3.Normalize(d*.6f+up*.6f-Side*(sx*.3f)),Vector3.Normalize(-d*.8f+up*.3f+Side*(sx*.35f)));
                case Thrust:return CapturedRig.Look(d,Vector3.Normalize(up*.8f+Side*(sx*.6f)));
                case GlideLead:return CapturedRig.Look(Vector3.Normalize(d*.45f+up*.9f),Vector3.Normalize(-d*.6f+up*.2f+Side*(sx*.5f)));
                default:return CapturedRig.Look(Vector3.Normalize(d+up*.35f),Vector3.Normalize(up+Side*(sx*.45f)));
            }
        }
        // First person: the hands placed from the view stance's fists (the Overflow's from the centre line).
        private static Vector3 ViewPlace(bool left,int at,Vector3 d,Vector3 up,Vector3 o,float spread)
        {
            float sx=left?1:-1;var off=ViewOffsets[at];float x=at==GatherAt||at==PushAt?off.X:spread-off.X;return o+Side*(sx*x)+up*off.Y+d*off.Z;
        }
        // From `a` to `b` by `k` along the arc about `pivot` (the direction turning on the great circle, the distance eased).
        private static Vector3 Swing(Vector3 pivot,Vector3 a,Vector3 b,float k)
        {
            if(k<=0)return a;if(k>=1)return b;
            var u=a-pivot;var w=b-pivot;float lu=u.Length(),lw=w.Length();if(lu<1e-4f||lw<1e-4f)return Vector3.Lerp(a,b,k);
            var turn=MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(u/lu,w/lw),k);return pivot+Vector3.Transform(u/lu,turn)*MeleeMath.Lerp(lu,lw,k);
        }

        // ---- v2's casts ----
        // How fast each cast takes the arms from the stance (the hands start low behind the hips).
        private static float Rise(int slot)=>slot==CyberKit2Ranged.Chain?.14f:slot==CyberKit2Ranged.Singularity||slot==CyberKit2Ranged.Storm?.15f:.2f;
        // A cast's weight over the stance at `time` (in over its rise, held through the cast, out over its settle; a cut cast out
        // over CutSettle from its cut) and the phase its pose is drawn at (frozen at a cut).
        private static float CastAt(CyberKit2RangedView k,int slot,float time,out CyberPhase ph)
        {
            float until=k.Until[slot];
            if(k.CutAt[slot]>-90&&k.At[slot]>-90&&time>=until&&until>k.At[slot])
            {
                if(time>=until+CutSettle){ph=default;return 0;}
                ph=k.Phase(slot,until-1e-4f);return Weight(ph,Rise(slot))*(1-Ease((time-until)/CutSettle));
            }
            ph=k.Phase(slot,time);return Weight(ph,Rise(slot));
        }
        private static float Weight(in CyberPhase ph,float rise)=>!ph.Live?0:ph.Part<2?Ease(ph.Age/rise):1-Ease(ph.U);
        // Where an arm points: from its shoulder toward `target` (kept in front of the body), `reach` out.
        private static Vector3 Point(Vector3 shoulder,Vector3 target,Vector3 d,float reach)
        {
            var dir=target-shoulder;if(dir.LengthSquared()<1e-4f)dir=d;dir=Vector3.Normalize(dir);if(dir.Z<-.1f){dir.Z=-.1f;dir=Vector3.Normalize(dir);}return shoulder+dir*reach;
        }
        /// <summary>The v2 casts over the stance and the throws: the newest cast showing, and the one before it while it still
        /// settles (a cast starts only once the last has ended, so the older is in its settle); each hands over to whatever starts
        /// after it (a cast through its rise; a throw's hand at once, as the throw sweeps on from where the cast left it, the
        /// other hand settling on as it would), so no cast's settle is dropped. `wR` / `wL`: the weight posed on each hand.
        /// `afterThrow` false poses the casts as they stood when the throw began (its start).</summary>
        private static void Kit2(CyberKit2RangedView k,float time,Vector3 d,Vector3 up,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 o,float spread,in Aims aims,in Throw th,bool afterThrow,
            ref Frame f,ref Vector3 right,ref Vector3 left,ref Quaternion rTurn,ref Quaternion lTurn,out float wR,out float wL)
        {
            wR=wL=0;
            int a1=-1,a2=-1;
            for(int s=CyberKit2Ranged.Chain;s<CyberKit2Ranged.Slots;s++){if(CastAt(k,s,time,out _)<=1e-4f)continue;if(a1<0||k.At[s]>k.At[a1]){a2=a1;a1=s;}else if(a2<0||k.At[s]>k.At[a2])a2=s;}
            if(a1<0)return;
            float throwAt=afterThrow&&th.At>-90&&time>=th.At?th.At:-100;bool tR=LoadedHand(th.Hand,false),tL=LoadedHand(th.Hand,true);
            // (The older first: the newer is posed over it.)
            if(a2>=0)Cast(k,a2,time,Over(k,a2,a1,throwAt,time,tR),Over(k,a2,a1,throwAt,time,tL),throwAt<=k.At[a2],d,up,firstPerson,reduced,shoulderL,shoulderR,o,spread,aims,ref f,ref right,ref left,ref rTurn,ref lTurn,ref wR,ref wL);
            Cast(k,a1,time,Over(k,a1,-1,throwAt,time,tR),Over(k,a1,-1,throwAt,time,tL),throwAt<=k.At[a1],d,up,firstPerson,reduced,shoulderL,shoulderR,o,spread,aims,ref f,ref right,ref left,ref rTurn,ref lTurn,ref wR,ref wL);
        }
        // How much of cast `s` is left on a hand once a newer cast `newer` has begun (through its rise), or a throw started at
        // `throwAt` with that hand (`thrower`: none, the throw carries it on; the other hand settles on).
        private static float Over(CyberKit2RangedView k,int s,int newer,float throwAt,float time,bool thrower)
        {
            float keep=newer>=0?1-Ease((time-k.At[newer])/Rise(newer)):1;
            return thrower&&throwAt>k.At[s]?0:keep;
        }
        private static void Cast(CyberKit2RangedView k,int slot,float time,float keepR,float keepL,bool owns,Vector3 d,Vector3 up,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 o,float spread,in Aims aims,
            ref Frame f,ref Vector3 right,ref Vector3 left,ref Quaternion rTurn,ref Quaternion lTurn,ref float mostR,ref float mostL)
        {
            float cw=CastAt(k,slot,time,out var ph),wR=cw*keepR,wL=cw*keepL,w=Math.Max(wR,wL);if(w<=1e-4f)return;mostR=Math.Max(mostR,wR);mostL=Math.Max(mostL,wL);
            var mid=(shoulderL+shoulderR)*.5f;Vector3 r,l;Quaternion rq=CapturedRig.Look(d,up),lq=rq;float yaw=0,pitch=0;string state;
            Vector3 ownR=firstPerson?o-Side*spread:shoulderR,ownL=firstPerson?o+Side*spread:shoulderL;float reach=firstPerson?.2f:Reach;
            switch(slot)
            {
                case CyberKit2Ranged.Chain:
                {
                    // The right arm thrown out at the first strike through the windup (the cast's weight carries it from the
                    // stance), the left hand keeping its energy drawn back, a tremor through the hops.
                    var target=aims.HasChain&&!firstPerson?aims.Chain:ownR+d*4;var outR=Point(ownR,target,d,reach);
                    float k0=Ease(ph.Part==0?ph.U:1);r=outR;l=left-d*(.06f*k0)+up*(.02f*k0);
                    if(ph.Part>=1&&!reduced){float tr=.007f*(1-(ph.Part==2?ph.U:0));var shake=up*(float)Math.Sin(time*61)*tr+Side*(float)Math.Sin(time*47+1)*tr;r+=shake;l+=shake*.6f;}
                    var aim=Vector3.Normalize(outR-ownR);rq=CapturedRig.Look(aim,up);lq=lTurn;yaw=.12f*k0;pitch=.04f*k0;state="cyber-daisy-chain";break;
                }
                case CyberKit2Ranged.Singularity:
                {
                    // The orb gathered between both hands low at the chest, then pushed out and up together, fingers spread.
                    var cup=firstPerson?o-up*.05f:mid+d*.24f-up*.14f;var push=firstPerson?o+d*.12f+up*.04f:mid+d*.42f+up*.06f;
                    float sp=firstPerson?spread*.5f:.075f;float rise=ph.Part==0?Ease(ph.U):1;var c=Vector3.Lerp(cup,cup+up*.05f-d*.04f,rise);
                    float out1=ph.Part==0?0:Ease(Math.Min(1,(time-k.Release[slot])/.12f));var at=Vector3.Lerp(c,push,out1);r=at-Side*(sp+.05f*out1);l=at+Side*(sp+.05f*out1);
                    rq=CapturedRig.Look(Vector3.Normalize(Side+d*(.4f+out1)),up);lq=CapturedRig.Look(Vector3.Normalize(-Side+d*(.4f+out1)),up);pitch=-.08f*rise*(1-out1)+.08f*out1;state="cyber-singularity";break;
                }
                case CyberKit2Ranged.Storm:
                {
                    // The right arm raised to the sky over the windup, then brought down to point at the ring as the drops are called.
                    var sky=firstPerson?ownR+up*.18f+d*.06f:shoulderR+up*Raise+d*.06f-Side*.02f;var target=aims.HasStorm&&!firstPerson?aims.Storm:ownR+d*6-up*1.2f;var down=Point(ownR,target,d,reach);
                    float raise=ph.Part==0?Ease(ph.U/.8f):1,cast=ph.Part==0?0:Ease(Math.Min(1,(time-k.Release[slot])/.1f));r=Vector3.Lerp(Vector3.Lerp(right,sky,raise),down,cast);
                    l=Vector3.Lerp(left,firstPerson?o+Side*spread-up*.04f:mid+d*.18f-up*.1f+Side*.1f,.8f);
                    rq=CapturedRig.Look(Vector3.Normalize(Vector3.Lerp(up,Vector3.Normalize(down-ownR),cast)),Vector3.Normalize(-d+up*.1f));pitch=-.1f*raise*(1-cast)+.1f*cast;yaw=-.1f*raise;state="cyber-packet-storm";break;
                }
                default:
                {
                    // Orbital Uplink: the left hand raised to the sky (the uplink), the right arm pointing at the call-down point and
                    // then at the column, following it as it chases the aim.
                    var target=aims.HasOrbital&&!firstPerson?aims.Orbital:ownR+d*8-up*1.4f;r=Point(ownR,target,d,reach);
                    l=firstPerson?ownL+up*.16f+d*.02f:shoulderL+up*Raise+d*.04f+Side*.05f;
                    if(ph.Part==0){float raise=Ease(ph.U/.5f);l=Vector3.Lerp(left,l,raise);r=Vector3.Lerp(right,r,Ease((ph.U-.2f)/.6f));}
                    if(!reduced){float hum=.004f;l+=up*(hum*(float)Math.Sin(time*29));}
                    rq=CapturedRig.Look(Vector3.Normalize(r-ownR),up);lq=CapturedRig.Look(up,Vector3.Normalize(-d));yaw=.1f;pitch=.05f;state=ph.Part==0?"cyber-orbital-call":"cyber-orbital-steer";break;
                }
            }
            // Swung in from the stance about the shoulders (the hands come forward round the body, never through it) and out again.
            if(firstPerson){right=Vector3.Lerp(right,r,wR);left=Vector3.Lerp(left,l,wL);}else{right=Swing(shoulderR,right,r,wR);left=Swing(shoulderL,left,l,wL);}
            rTurn=Quaternion.Slerp(rTurn,rq,wR);lTurn=Quaternion.Slerp(lTurn,lq,wL);
            float wc=Math.Min(wR,wL);f.ChestYaw=MeleeMath.Lerp(f.ChestYaw,yaw,wc);f.ChestPitch+=pitch*wc;if(owns)f.State=state;
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
            float yaw=f.ChestYaw*f.Weight,square=f.Square*f.Weight,pitch=f.ChestPitch*f.Weight;var cq0=r.ChainQuat(CapturedRig.Chest,-1);var carry=Quaternion.Identity;
            if(Math.Abs(yaw+square)>1e-5f||Math.Abs(pitch)>1e-5f)
            {
                var ry=MeleeMath.AxisAngle(MeleeMath.Y,yaw+square);var rx=MeleeMath.AxisAngle(MeleeMath.X,pitch);var t=ry*rx;r.Multiply(CapturedRig.Chest,t);
                // The neck takes the turn back out (the head stays on the aim; it still follows the chest's pitch).
                r.Premultiply(CapturedRig.Neck,MeleeMath.Inverse(t)*rx);
                // The targets ride round with the throw's coil (the square turn is already in them).
                carry=Quaternion.Normalize(cq0*MeleeMath.AxisAngle(MeleeMath.Y,yaw)*MeleeMath.Inverse(cq0));
            }
            if(f.Dip>1e-5f){var b=r.P[CapturedRig.Body];r.P[CapturedRig.Body]=new Vector3(b.X,b.Y-f.Dip*f.Weight,b.Z);}
            var cq=r.ChainQuat(CapturedRig.Chest,-1);var cp=r.ChainPoint(CapturedRig.Chest,-1);var inv=MeleeMath.Inverse(cq);
            var rt=cp+Vector3.Transform(f.Right-cp,carry);var lt=cp+Vector3.Transform(f.Left-cp,carry);
            r.Hand(false,Vector3.Transform(rt-cp,inv),inv*carry*f.RightTurn,f.Weight,Pole(false,f.RightCock));r.Hand(true,Vector3.Transform(lt-cp,inv),inv*carry*f.LeftTurn,f.Weight,Pole(true,f.LeftCock));
            return f.State;
        }
        // The elbow's bend direction (the arm's frame): the IK's default (out, down and a little back), swung out and back behind
        // the body for the stance's held energy (the forearms then curve forward to the cupped hands).
        private static Vector3? Pole(bool left,float cock)
        {
            if(cock<=1e-3f)return null;float sx=left?1:-1;var a=new Vector3(sx,-.65f,-.3f);var b=new Vector3(sx*.25f,-.2f,-1);return Vector3.Lerp(a,b,Clamp01(cock));
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
                firstPerson?r.ChainPoint(CapturedRig.LHand,-1):default,firstPerson?r.ChainPoint(CapturedRig.RHand,-1):default,aims,r.ChainPoint(CapturedRig.Chest,-1),r.ChainQuat(CapturedRig.Chest,-1));
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
