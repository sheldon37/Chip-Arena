using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One moment of the idle Cyber Lord's glitch (CyberLordIdlePose.Glitch): what the pose, the hardlight parts and
    /// the RGB-split ghosts show. All zero between the glitch moments.</summary>
    public struct CyberIdleGlitch
    {
        /// <summary>The upper body torn sideways off the hips, and the head band torn on its own (m, rig frame: +x the rig's left).</summary>
        public float Slice,HeadSlice;
        /// <summary>The RGB-split ghosts' strength (0..1) and the gap between the two (m).</summary>
        public float Split,SplitOffset;
        /// <summary>The scanline band on the hardlight parts: its strength (0..1) and its centre (m, rig rest space: the feet at -.795).</summary>
        public float Scan,ScanY;
        /// <summary>A rare one- or two-step jolt (-1..1).</summary>
        public float Twitch;
        /// <summary>The glitch step (seeds the flicker pattern; it holds between steps).</summary>
        public int Step;
        public bool Visible=>Math.Abs(Slice)>1e-4f||Math.Abs(HeadSlice)>1e-4f||Split>1e-3f||Scan>1e-3f||Math.Abs(Twitch)>1e-3f;
    }

    /// <summary>
    /// When the DEV Cyber Lord settles into its idle stance (owner 2026-10-12: "Id like to add a idle pose for the DEV Cyber
    /// Lord class (Matching the sheldon look) slight glitch effects"). One per rig, like the gait under it: a third-person
    /// pilot that has stood still, unhurt and out of every move for Delay seconds blends in over BlendIn; any move, attack,
    /// cast, guard, roll, hit reaction, stun, jump or step blends it out within BlendOut. Every window is read from the
    /// replicated actor and its CyberLordView; the only memory is when the pilot last moved (the gait's travel, the actor's
    /// velocity), so a fresh rig (a late join, the killcam) of a pilot already standing still shows the idle at once.
    /// Presentation only: it reads the actor and never writes it, and the hitboxes are the capsule, not the pose.
    /// </summary>
    public sealed class CyberLordIdle
    {
        /// <summary>The still time before the stance (s), its blend in, and its blend out on any action.</summary>
        public const float Delay=.9f,BlendIn=.6f,BlendOut=.08f;
        /// <summary>Above this ground speed (m/s), or with the gait still carrying (travel), the pilot is moving.</summary>
        public const float StillSpeed=.35f,StillTravel=.02f;
        public float Weight{get;private set;}
        private float last=float.NaN,quietSince=float.NegativeInfinity;
        public void Reset(){Weight=0;last=float.NaN;quietSince=float.NegativeInfinity;}
        /// <summary>This frame's weight (0..1). `travel` is the gait's carry (0 standing), `stun` the route's hit stun.</summary>
        public float Update(Actor a,CyberLordView v,float time,float travel,bool stun)
        {
            bool busy=Busy(a,v,time,out float end)|stun|travel>StillTravel;
            bool fresh=float.IsNaN(last)||time<last||time-last>.35f;float dt=fresh?0:time-last;last=time;
            if(busy)quietSince=time;else if(fresh)quietSince=end;else quietSince=Math.Max(quietSince,end);
            float target=busy?0:MeleeMath.Ease((time-quietSince-Delay)/BlendIn);
            Weight=fresh||target>=Weight?target:Math.Max(target,Weight-dt/BlendOut);
            return Weight;
        }
        /// <summary>
        /// Whether something keeps the pilot out of the idle at `time` (dead, airborne, moving or sprinting, a combo swing or a
        /// cast with its settle and blend out, Kernel Panic, a guard, a roll, a hit reaction or stun, an attack window), and
        /// the latest end of those windows already over (`end`; -inf for none). Windows starting after `time`, and the never-used
        /// ones (ending at or before the match's start), are ignored.
        /// </summary>
        public static bool Busy(Actor a,CyberLordView v,float time,out float end)
        {
            end=float.NegativeInfinity;if(a==null||!a.Alive||!a.Grounded||a.Sprinting)return true;
            if(a.Velocity.x*a.Velocity.x+a.Velocity.z*a.Velocity.z>StillSpeed*StillSpeed)return true;
            bool busy=false;float t=time,e=end;
            void Window(float from,float until){if(!(from>-90)||!(until>0))return;if(t>=from&&t<until)busy=true;else if(until<=t&&until>e)e=until;}
            float o=CyberLordMotion.BlendOut;
            if(v!=null)
            {
                Window(v.ComboAt,v.ComboUntil+o);
                for(int s=0;s<CyberLordSpec.Slots;s++)Window(v.At[s],Math.Max(v.Until[s],v.RecoverUntil[s])+o);
                if(v.PanicConnect>-90)Window(v.PanicConnect,(v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90?v.PanicEndedAt:v.PanicStart+KernelPanicPath.End)+o);
            }
            Window(a.BlockAt>-90?a.BlockAt:a.BlockUntil-Rules.BlockDuration,a.BlockUntil);
            Window(a.RollAt,a.RollAt+a.VisualRollDuration);Window(a.ReactionAt,a.ReactionUntil);Window(a.AttackAt,a.AttackUntil);Window(a.SpecialAt,a.SpecialUntil);
            if(a.StunUntil>0){if(t<a.StunUntil)busy=true;else if(a.StunUntil>e)e=a.StunUntil;}
            end=e;return busy;
        }
    }

    /// <summary>
    /// The DEV Cyber Lord's idle stance and its glitch, after the Sheldon Intro's resolved ending (Core/NativeSheldonIntro.cs,
    /// NativeSheldonIntroScript: Stare and Arms): upright and contained, the left hand on the hood's front edge at the temple
    /// (two fingers at the temple when no Sheldon Head hood is worn), the right arm loose, a slow visible breath lifting the
    /// chest and shoulders while the head holds its stare on the facing (and the view's pitch, eased). In a match it layers
    /// over the posed fighter rig with the gait's legs kept (Layer); in the lobby, Locker and pilot-card portraits it is the
    /// whole stance from rest (Stand, NativeLobbyPoses). The glitch is slight and occasional, seeded per pilot: a brief slice
    /// (the upper body or the head torn sideways), an RGB-split shimmer (the runtime's two tinted ghosts), a scanline band
    /// running over the hardlight parts, and a rare twitch, all on 24 Hz steps like the Sheldon Intro's. Reduced effects keep
    /// a softer version: no slice or twitch, the split eased in and out at a lower strength, the band a smooth glow, no
    /// strobing. Pure functions of the time, the seed and the weight; no allocation.
    /// </summary>
    public static class CyberLordIdlePose
    {
        /// <summary>The breath: its period (s) and, at a full in-breath, the spine's counter-turn, the chest's lift and the
        /// shoulders' rise (radians; the Sheldon Intro's stare breath made a little slower and larger so it reads in a match).</summary>
        public const float BreathPeriod=3.2f,BreathSpine=.03f,BreathChest=.018f,BreathShoulder=.1f;
        /// <summary>The stare's chin (rad, + down) and the share of the view's pitch the head follows, clamped.</summary>
        public const float StareChin=.07f,PitchShare=.45f,PitchMax=.32f;
        /// <summary>The chest turned a little toward the raised hand (the intro's stare key) and the loose right arm.</summary>
        public static readonly Vector3 ChestTurn=new Vector3(-.04f,-.06f,0),RightArm=new Vector3(-.12f,0,-.2f);public const float RightElbow=-.3f;
        /// <summary>The wrist on the hood (the intro's NativeSheldonIntroScript.HandOnHood), and two fingers at the temple of the
        /// bare voxel head (its side at .17 m; head space, source frame: +x the rig's left).</summary>
        public static readonly Vector3 HandOnHood=NativeSheldonIntroScript.HandOnHood,HandAtTemple=new Vector3(.205f,.13f,.075f);
        /// <summary>The raised arm before the reach (the intro's stare key: LA (-.6, 0, .5), LE -1.6): the reach keeps its elbow's
        /// side, as NativeIntroMotion.Reach does; the fallback pole (clavicle space) when it has none.</summary>
        public static readonly Vector3 RaisedArm=new Vector3(-.6f,0,.5f),RaisedPole=new Vector3(1,-.4f,-.1f);public const float RaisedElbow=-1.6f;
        /// <summary>The upright rise of the hips over the fighter stance's soft knees (m, at full weight; the feet stay planted).</summary>
        public const float Rise=.035f;
        /// <summary>The lobby stance's turn (rad, the raised hand toward the camera; the intro's hero frame is turned -.22).</summary>
        public const float LobbyYaw=-.18f;
        public static bool Handles(string spec)=>spec==CyberLordSpec.Id;
        /// <summary>Whether the hat is the Sheldon Head (its hood takes the hand).</summary>
        public static bool Hood(string hat)=>hat==NativeSheldonHead.Hat;
        public static float Breath(float time)=>.5f-.5f*(float)Math.Cos(2*Math.PI*time/BreathPeriod);

        // ---- the glitch ----
        /// <summary>Glitch steps per second (the Sheldon Intro's): full and reduced effects.</summary>
        public const float StepRate=NativeSheldonIntro.StepRate,ReducedStepRate=NativeSheldonIntro.ReducedStepRate;
        /// <summary>The moments' schedule: one slot every Slot s holds a moment with chance Chance, kind split (below
        /// SplitShare), slice (below SliceShare) or scan, lasting SplitSeconds, SliceSeconds or ScanSeconds; a twitch slot every
        /// TwitchSlot s with chance TwitchChance, TwitchSteps steps long. Reduced effects keep the split and the scan, eased.</summary>
        public const float Slot=2.3f,Chance=.6f,SplitShare=.45f,SliceShare=.75f,SplitSeconds=.2f,SliceSeconds=.125f,ScanSeconds=.5f,ReducedSplitSeconds=.6f,ReducedScanSeconds=.9f;
        public const float TwitchSlot=6.5f,TwitchChance=.55f;public const int TwitchSteps=2;
        /// <summary>The largest slice (m), the split's gap (m) and the split ghosts' strength with reduced effects.</summary>
        public const float SliceMax=.055f,HeadSliceMax=.035f,SplitGap=.03f,ReducedSplit=.45f;
        /// <summary>The glitch only shows once the stance has settled (the weight above this).</summary>
        public const float Settled=.95f;
        private static float H(int n,int salt)=>NativeSheldonHead.Hash01(n,salt);
        private static int Slot0(int seed,float time,float slot,int salt,out float start)
        {
            float phase=H(seed,salt)*slot;int n=(int)Math.Floor((time+phase)/slot);start=n*slot-phase;return n;
        }
        /// <summary>The glitch at `time` for the pilot `seed` at stance weight `weight`.</summary>
        public static CyberIdleGlitch Glitch(int seed,float time,float weight,bool reduced)
        {
            var g=new CyberIdleGlitch();if(!(weight>=Settled)||float.IsNaN(time))return g;
            g.Step=(int)Math.Floor(time*(reduced?ReducedStepRate:StepRate));
            int n=Slot0(seed,time,Slot,11,out float slot);int key=seed*7919+n*131;
            if(H(key,1)<Chance)
            {
                float kind=H(key,3),length=kind<SplitShare?(reduced?ReducedSplitSeconds:SplitSeconds):kind<SliceShare?SliceSeconds:(reduced?ReducedScanSeconds:ScanSeconds);
                float at=slot+.2f+H(key,2)*(Slot-length-.4f),age=time-at;
                if(age>=0&&age<length)
                {
                    float u=age/length;
                    if(kind<SplitShare)
                    {
                        // Full: the ghosts flash on most steps, at a stepped strength; reduced: one eased swell, no steps.
                        if(reduced){g.Split=ReducedSplit*(float)Math.Sin(Math.PI*u);g.SplitOffset=SplitGap*.6f;}
                        else{float on=H(g.Step*31+seed,5);if(on>.25f){g.Split=.55f+.45f*H(g.Step*17+seed,6);g.SplitOffset=SplitGap*(.7f+.6f*H(g.Step*13+seed,7));}}
                    }
                    else if(kind<SliceShare)
                    {
                        // A tear (full effects only): the upper body jumps sideways each step, the head on its own some steps.
                        if(!reduced)
                        {
                            float s=H(g.Step*29+seed,8)-.5f;g.Slice=(s<0?-1:1)*SliceMax*(.4f+.6f*Math.Abs(s)*2);
                            if(H(g.Step*23+seed,9)>.5f)g.HeadSlice=-(g.Slice>0?1:-1)*HeadSliceMax*(.5f+.5f*H(g.Step*19+seed,10));
                            g.Split=.35f;g.SplitOffset=SplitGap*.5f;
                        }
                    }
                    else
                    {
                        // The scanline band sweeps the hardlight parts, up or down.
                        bool up=H(key,4)<.6f;float y=-.85f+1.85f*u;g.ScanY=up?y:.15f-y;g.Scan=reduced?(float)Math.Sin(Math.PI*u):1;
                    }
                }
            }
            if(!reduced)
            {
                int m=Slot0(seed,time,TwitchSlot,12,out float tslot);int tk=seed*104729+m*977;
                if(H(tk,1)<TwitchChance)
                {
                    float at=tslot+.5f+H(tk,2)*(TwitchSlot-1),age=time-at;
                    if(age>=0&&age<TwitchSteps/StepRate)g.Twitch=H(tk,3)<.5f?-1:1;
                }
            }
            return g;
        }
        /// <summary>The rest height of each joint (rig rest space; CapturedRig.Bones are rest offsets with no rest turn).</summary>
        private static readonly float[] restY=RestHeights();
        private static float[] RestHeights(){var y=new float[CapturedRig.Bones.Length];for(int i=0;i<y.Length;i++){int p=CapturedRig.Bones[i].Parent;y[i]=CapturedRig.Bones[i].Position.Y+(p>=0?y[p]:0);}return y;}
        public const float ScanHalf=.12f;
        /// <summary>A hardlight piece's gain under the scanline band (1 outside it). Full effects flicker the pieces in the band
        /// dim and bright per step; reduced effects only brighten them softly.</summary>
        public static float ScanGain(in CyberIdleGlitch g,in CyberPiece p,int index,bool reduced)
        {
            if(!(g.Scan>1e-3f)||p.Bone<0||p.Bone>=restY.Length)return 1;
            float d=Math.Abs(restY[p.Bone]+p.Position.Y-g.ScanY),band=1-MeleeMath.SmoothStep(d,0,ScanHalf);if(band<=0)return 1;
            if(reduced)return 1+.45f*band*g.Scan;
            float flick=H(g.Step*37+index,13)<.5f?.25f:1.9f;return 1+(flick-1)*band*g.Scan;
        }

        // ---- the pose ----
        /// <summary>
        /// The match layer over a posed fighter rig at weight `w` (group space; the legs stay the gait's, the hips rise a touch
        /// over the planted feet): the stare, the breath, the loose right arm, the raised left hand, then the glitch's tear and
        /// twitch. `pitch` is the view's pitch (rad, + up).
        /// </summary>
        public static void Layer(MeleeRig r,float w,float time,bool hood,float pitch,in CyberIdleGlitch g)
        {
            if(w<=1e-4f)return;
            // Upright: the pelvis keeps only its heading (the fighter stance tips it forward) and the hips rise a touch; the gait's
            // ground pass after the style re-plants the feet under them.
            var pf=Vector3.Transform(Vector3.UnitZ,r.Q[CapturedRig.Pelvis]);if(pf.X*pf.X+pf.Z*pf.Z>1e-6f)r.Slerp(CapturedRig.Pelvis,MeleeMath.AxisAngle(MeleeMath.Y,(float)Math.Atan2(pf.X,pf.Z)),w);
            var hips=r.P[CapturedRig.Body];r.P[CapturedRig.Body]=new Vector3(hips.X,hips.Y+Rise*w,hips.Z);
            Upper(r,w,time,hood,pitch,g);
        }
        /// <summary>
        /// The lobby stance from rest (NativeLobbyPoses, the portraits): standing even on both feet, turned LobbyYaw, the same
        /// upper body at full weight; returns the glitch for the renderer's ghosts. `time` is the portrait clock (0 with reduced
        /// motion: the stance holds still and nothing glitches).
        /// </summary>
        public static CyberIdleGlitch Stand(MeleeRig r,float time,bool hood,int seed,bool reduced)
        {
            r.Reset();r.WeaponReach=1;float b=Breath(time);
            r.P[CapturedRig.Body]=new Vector3(0,-.004f+b*.002f,0);r.SetE(CapturedRig.Body,0,LobbyYaw,0);r.SetE(CapturedRig.Pelvis,0,.03f,0);
            r.Foot(false,new Vector3(-.12f,MeleeRig.FLOOR,-.01f),-.1f,0,1,null,NativeLobbyPoses.StandingReach);r.Foot(true,new Vector3(.125f,MeleeRig.FLOOR,.035f),.14f,0,1,null,NativeLobbyPoses.StandingReach);
            var g=time>0?Glitch(seed,time,1,reduced):default;Upper(r,1,time,hood,0,g);return g;
        }
        private static void Upper(MeleeRig r,float w,float time,bool hood,float pitch,in CyberIdleGlitch g)
        {
            const int Sp=CapturedRig.Spine,C=CapturedRig.Chest,N=CapturedRig.Neck,Hd=CapturedRig.Head;
            float b=Breath(time),sh=BreathShoulder*b;
            // Upright: the spine counter-turns the chest's lift so the neck stays put; the chest turned a little to the raised hand.
            r.Slerp(Sp,MeleeMath.Euler(BreathSpine*b,0,0),w);r.Slerp(C,MeleeMath.Euler(ChestTurn.X-(BreathChest+BreathSpine)*b,ChestTurn.Y,ChestTurn.Z),w);
            r.Slerp(CapturedRig.LClav,MeleeMath.Euler(0,0,sh),w);r.Slerp(CapturedRig.RClav,MeleeMath.Euler(0,0,-sh),w);
            // The loose right arm rides the shoulder up but keeps hanging.
            r.Slerp(CapturedRig.RArm,MeleeMath.AxisAngle(MeleeMath.Z,sh)*MeleeMath.Euler(RightArm.X,RightArm.Y,RightArm.Z),w);r.Slerp(CapturedRig.RElbow,MeleeMath.Euler(RightElbow,0,0),w);
            r.Slerp(CapturedRig.RHand,MeleeMath.Euler(0,0,.06f),w);
            // The stare: the neck upright, the head set in the rig's frame whatever is under it (the breath never reaches it).
            r.Slerp(N,Quaternion.Identity,w);
            float look=StareChin-PitchShare*MeleeMath.Clamp(pitch,-PitchMax/PitchShare,PitchMax/PitchShare);
            r.Slerp(Hd,MeleeMath.Inverse(r.ChainQuat(N,-1))*MeleeMath.Euler(look,0,0),w);
            // The glitch's twitch (a step or two) and tear (the joint offsets; the next frame's rig starts from rest).
            if(g.Twitch!=0){r.Premultiply(N,MeleeMath.AxisAngle(MeleeMath.Y,.07f*g.Twitch*w));r.Multiply(CapturedRig.RClav,MeleeMath.AxisAngle(MeleeMath.Z,-.1f*g.Twitch*w));}
            if(g.Slice!=0)r.P[Sp]=new Vector3(r.P[Sp].X+g.Slice*w,r.P[Sp].Y,r.P[Sp].Z);
            if(g.HeadSlice!=0)r.P[Hd]=new Vector3(r.P[Hd].X+g.HeadSlice*w,r.P[Hd].Y,r.P[Hd].Z);
            // The left hand on the hood's front edge at the temple (or two fingers at the bare temple), fingers up over the brim:
            // the intro's keyed raised arm (out to the side, the elbow folded), then the reach keeping that arm's elbow pole.
            r.Slerp(CapturedRig.LArm,MeleeMath.Euler(RaisedArm.X,RaisedArm.Y,RaisedArm.Z),w);r.Slerp(CapturedRig.LElbow,MeleeMath.Euler(RaisedElbow,0,0),w);
            Vector3 s=r.ChainPoint(CapturedRig.LArm,-1),line=r.ChainPoint(CapturedRig.LHand,-1)-s,bend=r.ChainPoint(CapturedRig.LElbow,-1)-s;float len=line.LengthSquared();if(len>1e-8f)bend-=line*(Vector3.Dot(bend,line)/len);
            var pole=bend.LengthSquared()>1e-8f?Vector3.Transform(bend,MeleeMath.Inverse(r.ChainQuat(CapturedRig.LClav,-1))):RaisedPole;
            Quaternion head=r.ChainQuat(Hd,-1),chest=r.ChainQuat(C,-1),chestInv=MeleeMath.Inverse(chest);
            var target=r.ChainPoint(Hd,-1)+Vector3.Transform(hood?HandOnHood:HandAtTemple,head);
            r.Hand(true,Vector3.Transform(target-r.ChainPoint(C,-1),chestInv),chestInv*head*NativeSheldonIntroScript.HandTurn,w,pole);
        }
    }
}
