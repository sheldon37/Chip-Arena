using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's ranged-mode body (Core/CyberLordRanged.cs; docs/CYBER-LORD.md "Ranged mode"), a layer over the
    /// fighter route's pose (CyberLordMotion.Style, after the capture and the idle) and over the first-person view model: the
    /// emitter hold (both arms out along the aim, the open hardlight emitters at the fingertips, the chest following a share
    /// of the view's pitch), the key-5 reconfiguration (the forearms fold in to the chest and snap out into the other mode,
    /// a glitch jolt at its middle), and each ranged cast's motion on its replicated window: the Bit Bolt's alternate-hand
    /// thrust and kick, Ping Lance's cock and thrust, Lag Field's underhand lob, Proxy Swap's tuck through the blink, Root
    /// Access's two-handed charge (a tremor that grows) and its recoil. A melee take still owning the frame (the guard, Q,
    /// E) and a sprint forward lower the hold; a ranged cast always raises it. Pure functions of the view and the clock on
    /// the rig the route posed; nothing here reaches the authority, hit timing, hitboxes, damage, cooldowns or movement. No
    /// allocation.
    /// </summary>
    public static class CyberRangedPose
    {
        /// <summary>The hold (group space, m): the emitters' reach ahead of the shoulders, their spread from the centre line,
        /// their drop below the shoulders; the first-person view's from where the view's stance holds the fists (in view, in
        /// its lower half): reached further ahead, drawn in toward the centre to ViewSpread of their spread, lifted; the share
        /// of the pitch the chest takes.</summary>
        public const float Reach=.46f,Spread=.1f,Drop=.05f,ViewReach=.07f,ViewSpread=.72f,ViewLift=.03f,ViewGap=.11f,ChestPitch=.3f,PitchMax=1.1f;
        /// <summary>The casts' motion (m, rad): the bolt's thrust and kick, the lance's cock and thrust, the lob's reach back and
        /// up, the swap's tuck and dip, the beam's converge, tremor and recoil (thrown back and up, the chest back).</summary>
        public const float BoltThrust=.03f,BoltKick=.055f,BoltLift=.3f,LanceCock=.14f,LanceThrust=.06f,LanceSnap=.06f,LobBack=.36f,LobLead=.06f,LobSwing=.17f,TuckIn=.14f,SwapDip=.06f,RootTremor=.005f,RootKick=.12f,RootBack=.25f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Ease(float x)=>MeleeMath.Ease(Clamp01(x));
        private static float Decay(float age,float span)=>age<0?0:(float)Math.Exp(-age/Math.Max(1e-3f,span));
        // A recoil: eased in over `attack` (no one-frame snap), then decaying over `span`.
        private static float Kick(float age,float attack,float span)=>age<0?0:age<attack?Ease(age/attack):Decay(age-attack,span);
        private static readonly Vector3 Up=Vector3.UnitY,Side=Vector3.UnitX;

        /// <summary>One frame's ranged layer: the hold's weight, the hands' targets and turns (group space) and the body's tweaks.</summary>
        public struct Frame
        {
            public float Weight,ChestPitch,Dip;public Vector3 Right,Left;public Quaternion RightTurn,LeftTurn;public string State;
            public bool Live=>Weight>1e-4f;
        }
        private static float H(int n,int salt){unchecked{uint h=(uint)n*0x9e3779b1u^(uint)salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;return (h&0xffffff)/16777216f;}}

        /// <summary>
        /// The layer's frame for the view at `time`: `pitch` the view's (rad, + up; 0 in first person), `yieldTo` how much a melee
        /// take owns the frame (the guard, Q, E: the hold gives way), `lower` the sprint's (a sprint forward carries the arms),
        /// from the shoulders `shoulderL` / `shoulderR` (group space).
        /// </summary>
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,float lower,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR)=>
            Of(v,time,pitch,yieldTo,lower,firstPerson,reduced,shoulderL,shoulderR,default,default);
        /// <summary>As above; in first person `fistL` / `fistR` are where the view's stance holds the fists (group space).</summary>
        public static Frame Of(CyberLordView v,float time,float pitch,float yieldTo,float lower,bool firstPerson,bool reduced,Vector3 shoulderL,Vector3 shoulderR,Vector3 fistL,Vector3 fistR)
        {
            var f=new Frame();float mode=v.ModeWeight(time),sw=v.SwitchU(time);bool casting=v.RangedCasting(time)||RecoveringRanged(v,time);
            if(mode<=1e-4f&&sw<0){return f;}
            float hold=mode*(1-Clamp01(yieldTo))*(casting?1:1-.75f*Clamp01(lower));
            // Through a switch the arms take the hold from its start (melee to ranged) or keep it to its end (ranged to melee),
            // folding in to the chest at its middle.
            if(sw>=0)hold=Math.Max(hold,(1-Clamp01(yieldTo))*(v.Ranged?Ease(sw/.45f):1-Ease((sw-.55f)/.45f)));
            f.Weight=hold;if(hold<=1e-4f)return f;
            float p=MeleeMath.Clamp(pitch,-PitchMax,PitchMax);var d=new Vector3(0,(float)Math.Sin(p),(float)Math.Cos(p));var up=new Vector3(0,(float)Math.Cos(p),-(float)Math.Sin(p));
            var mid=(shoulderL+shoulderR)*.5f;Vector3 o;float spread;
            if(firstPerson){var c=(fistL+fistR)*.5f;o=c+d*ViewReach+up*ViewLift;spread=Math.Max(.05f,Math.Abs(fistL.X-fistR.X)*.5f*ViewSpread);}
            else{o=mid-up*Drop+d*Reach;spread=Spread;}
            var right=o-Side*spread;var left=o+Side*spread;var turn=CapturedRig.Look(d,up);var rTurn=turn;var lTurn=turn;
            f.ChestPitch=-p*ChestPitch;f.State="cyber-ranged-aim";
            // The reconfiguration: both forearms fold in across the chest, a glitch jolt at the middle, and out into the hold.
            if(sw>=0)
            {
                // (In first person the fold stays in view: the fists meet in front of the camera.)
                float fold=(float)Math.Sin(Math.PI*Clamp01(sw));
                var inR=firstPerson?o-Side*.035f-up*.03f:mid+d*.16f-up*.12f+Side*.05f;var inL=firstPerson?o+Side*.035f-up*.02f:mid+d*.16f-up*.1f-Side*.05f;
                right=Vector3.Lerp(right,inR,fold);left=Vector3.Lerp(left,inL,fold);
                if(!reduced&&sw>.4f&&sw<.62f){int step=(int)(time*24);var jolt=new Vector3(H(step,3)-.5f,H(step,5)-.5f,H(step,7)-.5f)*.035f;right+=jolt;left-=jolt;}
                rTurn=Quaternion.Slerp(turn,CapturedRig.Look(Side-d*.3f,up),fold*.7f);lTurn=Quaternion.Slerp(turn,CapturedRig.Look(-Side-d*.3f,up),fold*.7f);
                f.State="cyber-reconfigure";
            }
            // The casts on their windows (the newest live one; their settles ease back to the hold).
            int slot=Newest(v,time);
            if(slot>=0)
            {
                var ph=v.RPhase(slot,time);float rel=v.RRelease[slot],age=time-rel;
                switch(slot)
                {
                    case CyberRangedSpec.Bolt:
                    {
                        // The firing emitter thrusts over the windup and kicks back and up at the release; the other stays aimed.
                        bool leftHand=v.BoltHand==1;float push=ph.Part==0?Ease(ph.U):0,kick=Kick(age,.03f,.07f);
                        var off=d*(BoltThrust*push-BoltKick*kick)+up*(.015f*kick);var lift=Quaternion.CreateFromAxisAngle(Vector3.Transform(Vector3.UnitX,turn),-BoltLift*kick);
                        if(leftHand){left+=off;lTurn=lift*lTurn;}else{right+=off;rTurn=lift*rTurn;}
                        f.State="cyber-bit-bolt";break;
                    }
                    case CyberRangedSpec.Lance:
                    {
                        // The cannon arm cocks back to the shoulder over the windup, the guide hand forward; it thrusts at the
                        // release and recoils.
                        // (The cock lets go over LanceSnap from the release, the thrust peaking there and easing out.)
                        var cock=-d*LanceCock+up*.03f-Side*.03f;
                        if(ph.Part==0){float k=Ease(ph.U);right+=cock*k;left+=d*(.03f*k);}
                        else{float c=1-Ease(age/LanceSnap),k=Ease(age/LanceSnap)*(1-Ease((age-LanceSnap)/.16f));right+=cock*c+d*(LanceThrust*k)+up*(.02f*k);left+=d*(.03f*c);}
                        f.State="cyber-ping-lance";break;
                    }
                    case CyberRangedSpec.Field:
                    {
                        // The underhand lob: the left hand swings low and back past the hip, then forward and up to the release.
                        // Back over the first part of the windup; the forward swing starts LobLead before the release and
                        // reaches the release point LobSwing later (a throw's speed, no snap).
                        var low=mid+Side*.2f-up*LobBack-d*.04f;var high=mid+Side*.12f+d*.45f+up*.12f;float at=v.RAt[slot];
                        float back=Ease((time-at)/Math.Max(.01f,(rel-at-LobLead)*.9f)),fwd=Ease((time-(rel-LobLead))/LobSwing);
                        // Both swings arc about the shoulder (a pendulum in front of the hip, never through the body).
                        if(ph.Part<2)left=Swing(shoulderL,Swing(shoulderL,left,low,back),high,fwd);else left=Swing(shoulderL,high,left,Ease(ph.U));
                        f.State="cyber-lag-field";break;
                    }
                    case CyberRangedSpec.Swap:
                    {
                        // Tucked through the blink (the arms in, a dip), out again over the settle.
                        float k=ph.Part<2?Ease(ph.Age/.09f):1-Ease(ph.U);var tuck=firstPerson?o-up*.05f-d*.03f:mid+d*TuckIn-up*.1f;right=Vector3.Lerp(right,tuck-Side*.06f,k);left=Vector3.Lerp(left,tuck+Side*.06f,k);f.Dip=SwapDip*k;
                        f.State="cyber-proxy-swap";break;
                    }
                    case CyberRangedSpec.Root:
                    {
                        if(ph.Part==0)
                        {
                            // The charge: the emitters close together on the centre line, the body leaning in, a growing tremor.
                            // (In first person a gap stays between them, the charge's light in it.)
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
            }
            f.Right=right;f.Left=left;f.RightTurn=rTurn;f.LeftTurn=lTurn;return f;
        }
        // From `a` to `b` by `k` along the arc about `pivot` (the direction turning on the great circle, the distance eased).
        private static Vector3 Swing(Vector3 pivot,Vector3 a,Vector3 b,float k)
        {
            var u=a-pivot;var w=b-pivot;float lu=u.Length(),lw=w.Length();if(lu<1e-4f||lw<1e-4f)return Vector3.Lerp(a,b,k);
            var turn=MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(u/lu,w/lw),k);return pivot+Vector3.Transform(u/lu,turn)*MeleeMath.Lerp(lu,lw,k);
        }
        /// <summary>The newest ranged cast live at `time` (to the end of its settle), or -1.</summary>
        public static int Newest(CyberLordView v,float time)
        {
            int newest=-1;float at=-100;
            for(int s=0;s<CyberRangedSpec.Slots;s++){float start=v.RAt[s],end=Math.Max(v.RUntil[s],v.RRecover[s]);if(start>-90&&time>=start&&time<end&&start>at){newest=s;at=start;}}
            return newest;
        }
        private static bool RecoveringRanged(CyberLordView v,float time)=>Newest(v,time)>=0;
        /// <summary>
        /// The frame onto the posed rig: the chest's pitch and the swap's dip, then both arms to their emitter targets (chest-
        /// space IK, the wrists turned to the aim) at the hold's weight. Returns the frame's state (null when it does not show).
        /// </summary>
        public static string Apply(MeleeRig r,in Frame f)
        {
            if(!f.Live)return null;
            if(Math.Abs(f.ChestPitch)>1e-5f)r.Multiply(CapturedRig.Chest,MeleeMath.AxisAngle(MeleeMath.X,f.ChestPitch*f.Weight));
            if(f.Dip>1e-5f){var b=r.P[CapturedRig.Body];r.P[CapturedRig.Body]=new Vector3(b.X,b.Y-f.Dip*f.Weight,b.Z);}
            var cq=r.ChainQuat(CapturedRig.Chest,-1);var cp=r.ChainPoint(CapturedRig.Chest,-1);var inv=MeleeMath.Inverse(cq);
            r.Hand(false,Vector3.Transform(f.Right-cp,inv),inv*f.RightTurn,f.Weight);r.Hand(true,Vector3.Transform(f.Left-cp,inv),inv*f.LeftTurn,f.Weight);
            return f.State;
        }
        /// <summary>The layer in one call: the shoulders from the posed rig, the frame, applied.</summary>
        public static string Layer(MeleeRig r,CyberLordView v,float time,float pitch,float yieldTo,float lower,bool firstPerson,bool reduced)
        {
            var f=Of(v,time,pitch,yieldTo,lower,firstPerson,reduced,r.ChainPoint(CapturedRig.LArm,-1),r.ChainPoint(CapturedRig.RArm,-1),
                firstPerson?r.ChainPoint(CapturedRig.LHand,-1):default,firstPerson?r.ChainPoint(CapturedRig.RHand,-1):default);
            return Apply(r,f);
        }
    }
}
