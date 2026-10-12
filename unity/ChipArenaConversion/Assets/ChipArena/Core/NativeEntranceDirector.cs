using System;
using System.Collections.Generic;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>A blast an entrance fires into its own NativeBlastSim at presentation time `At` (source frame).</summary>
    public struct NativeEntranceBlast
    {
        public float At,Radius,Size;public NativeBlastStyle Style;public Vector3 Position;public NativeBlastTint? Tint;public bool Impact;public int ImpactColor;
    }
    /// <summary>An afterimage of the pilot: the entrance's own pose `Lag` s earlier on its clock, a flat see-through silhouette.</summary>
    public struct NativeEntranceGhost{public float Lag,Opacity;public Vector3 Color,Offset;}
    /// <summary>Points the effects follow (sampled once per entrance, NativeEntranceScript.Anchor).</summary>
    public enum NativeEntranceAnchor{Root,Pelvis,Chest,Head,LeftHand,RightHand,LeftTip,RightTip,LeftFoot,RightFoot,Forward,LeftElbow,RightElbow,LeftKnee,RightKnee,LeftShoulder,RightShoulder,Focus,Count}

    /// <summary>
    /// Presentation only: one directed entrance (owner 2026-10-11 rework of every non-DEV entrance). A script is a set of
    /// pure functions of the entrance's presentation clock: the pilot's pose, an authored multi-shot camera, the stage's
    /// lights and set pieces, closed-form effects, afterimages and the blasts it fires. The match only ever sees the style
    /// and its unchanged duration (NativeIntroRules.Duration, 4.8 s; name card 3.2 s); nothing here feeds back into play.
    /// </summary>
    public abstract class NativeEntranceScript
    {
        public readonly string Style;
        /// <summary>The main beat (s): the weapon's hit (WEAPON_INTROS hit, the sting's accent) or the entrance's impact.</summary>
        public float Hit;
        /// <summary>The slow-motion beat at Hit (NativeEntranceMath.Warp): rate, hold, recovery and catch-up.</summary>
        public float SlowRate=.3f,SlowHold=.12f,SlowRecover=.28f,SlowCatchUp=.9f;
        public Vector3 Accent;
        public NativeEntranceShot[] Shots;
        /// <summary>The camera settles still from here to the end (a clean hand-off to the match camera).</summary>
        public const float Settle=4.45f,Duration=4.8f,NameAt=3.2f;
        protected NativeEntranceScript(string style,float hit,int accent){Style=style;Hit=hit;Accent=NativeEntranceFx.Hex(accent);}
        /// <summary>The presentation clock at match age `age`: the age, slowed through the beat and caught up before the name card.</summary>
        public float Clock(float age){if(!(age>0))return 0;return NativeEntranceMath.Warp(Math.Min(age,Duration+1),Hit,SlowRate,SlowHold,SlowRecover,SlowCatchUp);}
        /// <summary>Binds the baked clip (weapon signatures; the procedural entrances ignore it).</summary>
        public virtual void Bind(NativeEntranceClip clip){}
        public virtual bool Ready=>true;
        /// <summary>The pilot's pose at presentation time `t` (rig frame) and its root (source frame).</summary>
        public abstract void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation);
        /// <summary>The weapon the intro rig holds (NativeIntroRules.IntroWeapon), its tip length and hand.</summary>
        protected virtual float TipLength(bool left)=>.15f;
        public virtual void Lights(float t,NativeEntranceLights lights){NativeEntranceStage.Rig(lights,1,Accent,1,1);}
        public virtual void Props(float t,INativeEntranceProps props,float quality){Stage(props,t,.6f);}
        /// <summary>Pylon `i`'s strip level at `t` (default: the power-on sweep, the hit flash and the name-card pulse).</summary>
        protected virtual float Pylon(int i,float t)=>PowerOn(i,t,.05f);
        /// <summary>The arena (NativeEntranceStage.Arena) with this entrance's pylon levels (no allocation).</summary>
        protected void Stage(INativeEntranceProps props,float t,float grid,float pulseAt=-10,Vector3? gridColor=null){Span<float> lv=stackalloc float[NativeEntranceStage.Pylons];for(int i=0;i<lv.Length;i++)lv[i]=Pylon(i,t);NativeEntranceStage.Arena(props,t,Accent,lv,grid,pulseAt,gridColor);}
        public virtual void Effects(float t,INativeEntranceFx fx,float quality){}
        public virtual int Ghosts(float t,Span<NativeEntranceGhost> into)=>0;
        /// <summary>How solid the pilot's bone `bone` is (1 opaque; less is dithered away); 0 hides it.</summary>
        public virtual float Solid(float t,int bone)=>1;
        /// <summary>A hologram copy of the pilot (flat colour, see-through), or false.</summary>
        public virtual bool Holo(float t,out Vector3 color,out float opacity,out Vector3 offset){color=Vector3.Zero;opacity=0;offset=Vector3.Zero;return false;}
        /// <summary>Solid set pieces the camera must stay out of (centre, half size pairs).</summary>
        public virtual int Boxes(float t,Span<Vector4> into)=>NativeEntranceStage.PylonBoxes(into,0);
        public NativeEntranceBlast[] Blasts=Array.Empty<NativeEntranceBlast>();
        /// <summary>Builds Blasts once the anchors exist.</summary>
        protected virtual void Plan(){}

        // ---- anchors: the pose sampled at 120 Hz over the clip, built on first use (a pure function of the script) ----
        public const int AnchorRate=120;
        private Vector3[] anchors;private readonly CapturedRig.Pose anchorPose=new CapturedRig.Pose();
        private void BuildAnchors()
        {
            int n=(int)(Duration*AnchorRate)+2,k=(int)NativeEntranceAnchor.Count;var a=new Vector3[n*k];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)AnchorRate;Pose(t,anchorPose,out var root,out var rot);anchorPose.World();int o=i*k;
                a[o]=root;a[o+1]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.Pelvis,Vector3.Zero);a[o+2]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.Chest,new Vector3(0,.1f,0));
                a[o+3]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.Head,new Vector3(0,.165f,0));a[o+4]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.LHand,new Vector3(0,-.05f,0));a[o+5]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.RHand,new Vector3(0,-.05f,0));
                a[o+6]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.LHand,new Vector3(0,0,TipLength(true)));a[o+7]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.RHand,new Vector3(0,0,TipLength(false)));
                a[o+8]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.LFoot,Vector3.Zero);a[o+9]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.RFoot,Vector3.Zero);
                // The facing: the pelvis's forward, flattened (a flip or a tumble keeps the last upright heading's sign).
                var f=Vector3.Transform(Vector3.UnitZ,NativeEntrancePose.WorldRotation(anchorPose,rot,CapturedRig.Pelvis));a[o+10]=new Vector3(f.X,0,f.Z);
                a[o+11]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.LElbow,Vector3.Zero);a[o+12]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.RElbow,Vector3.Zero);
                a[o+13]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.LKnee,Vector3.Zero);a[o+14]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.RKnee,Vector3.Zero);
                a[o+15]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.LArm,Vector3.Zero);a[o+16]=NativeEntrancePose.Point(anchorPose,root,rot,CapturedRig.RArm,Vector3.Zero);
                a[o+17]=Focus(t,a[o+2]);
            }
            anchors=a;Plan();
        }
        /// <summary>What a shot following NativeEntranceAnchor.Focus looks at (a set piece the pilot rides or arrives in); the chest by default.</summary>
        protected virtual Vector3 Focus(float t,Vector3 chest)=>chest;
        /// <summary>Anchor `which` at presentation time `t` (linear between the 120 Hz samples).</summary>
        public Vector3 Anchor(NativeEntranceAnchor which,float t)
        {
            if(anchors==null)BuildAnchors();int k=(int)NativeEntranceAnchor.Count,n=anchors.Length/k;float x=Math.Max(0,Math.Min(n-1.001f,t*AnchorRate));int i=(int)x;float f=x-i;
            return Vector3.Lerp(anchors[i*k+(int)which],anchors[(i+1)*k+(int)which],f);
        }
        /// <summary>Anchor `which` at `t` on a Catmull-Rom curve through the table (no velocity kinks at its samples: what the camera follows).</summary>
        public Vector3 AnchorCurve(NativeEntranceAnchor which,float t)
        {
            if(anchors==null)BuildAnchors();int k=(int)NativeEntranceAnchor.Count,n=anchors.Length/k;float x=Math.Max(0,Math.Min(n-1.001f,t*AnchorRate));int i=(int)x;float f=x-i;
            var p0=anchors[Math.Max(0,i-1)*k+(int)which];var p1=anchors[i*k+(int)which];var p2=anchors[(i+1)*k+(int)which];var p3=anchors[Math.Min(n-1,i+2)*k+(int)which];
            float f2=f*f,f3=f2*f;return .5f*(2*p1+(p2-p0)*f+(2*p0-5*p1+4*p2-p3)*f2+(3*p1-p0-3*p2+p3)*f3);
        }
        /// <summary>The anchor's velocity (m/s) at `t`.</summary>
        public Vector3 Velocity(NativeEntranceAnchor which,float t)=>(Anchor(which,t+1f/AnchorRate)-Anchor(which,t-1f/AnchorRate))*(AnchorRate*.5f);
        /// <summary>Discards the anchors (a new clip).</summary>
        protected void Invalidate(){anchors=null;}
        public void Prepare(){if(anchors==null)BuildAnchors();}

        // ---- the camera ----
        /// <summary>The subject the shots frame: the pelvis smoothed over +-.35 s (no step, breath or landing jolt reaches the camera).</summary>
        public Vector3 Subject(float t)
        {
            var sum=Vector3.Zero;float w=0;for(int i=-7;i<=7;i++){float k=(float)Math.Exp(-i*i/18.0);sum+=AnchorCurve(NativeEntranceAnchor.Pelvis,t+i*.05f)*k;w+=k;}
            var p=sum/w;return new Vector3(p.X,p.Y-.64f,p.Z);
        }
        /// <summary>The subject's heading (radians about +y; 0 faces +z) at presentation time `t`.</summary>
        public float Heading(float t){var f=Anchor(NativeEntranceAnchor.Forward,t);return f.LengthSquared()<1e-6f?0:(float)Math.Atan2(f.X,f.Z);}
        /// <summary>Anchor `which` smoothed over +-.15 s (what a following look aims at); the Focus (a set piece that may stop dead) over +-.25 s.</summary>
        public Vector3 Smoothed(int which,float t)
        {
            int n=which==(int)NativeEntranceAnchor.Focus?5:3;double spread=n==5?12.5:4.5;var sum=Vector3.Zero;float w=0;
            for(int i=-n;i<=n;i++){float k=(float)Math.Exp(-i*i/spread);sum+=AnchorCurve((NativeEntranceAnchor)which,t+i*.05f)*k;w+=k;}return sum/w;
        }
        private readonly Vector4[] boxes=new Vector4[64];private Func<float,Vector3> subject;private Func<float,float> heading;private Func<int,float,Vector3> follow;
        /// <summary>The camera at match age `age` (shots on the presentation clock), guarded out of the floor, the pilot and the set.</summary>
        public void Camera(float age,out NativeEntranceView view)
        {
            float t=Clock(Math.Min(age,Settle));subject??=Subject;heading??=Heading;follow??=Smoothed;
            NativeEntranceCamera.Evaluate(Shots,t,Clock(Settle),subject,heading,out view,follow);
            int n=Boxes(t,boxes);
            view.Eye=NativeEntranceCamera.Guard(view.Eye,NativeEntranceStage.FloorY,Anchor(NativeEntranceAnchor.Head,t),Anchor(NativeEntranceAnchor.Pelvis,t),(Anchor(NativeEntranceAnchor.LeftFoot,t)+Anchor(NativeEntranceAnchor.RightFoot,t))*.5f,new ReadOnlySpan<Vector4>(boxes,0,n));
        }

        // ---- shot helpers (subject frame: +z its front, +x its left, heights above the floor; Track follows the subject) ----
        protected static NativeEntranceShot Shot(float at,NativeEntranceCut cut,float fov0,float fov1,Vector3 eye0,Vector3 eye1,Vector3 look0,Vector3 look1,byte ease=0,float track=1,float inTime=.16f,float roll0=0,float roll1=0,float face=-1,float trackY=0)
            =>new NativeEntranceShot{At=at,Face=face,TrackY=trackY,Follow=-1,In=cut,InTime=inTime,Eye0=eye0,Eye1=(eye0+eye1)*.5f,Eye2=eye1,Look0=look0,Look1=(look0+look1)*.5f,Look2=look1,Fov0=fov0,Fov1=fov1,Roll0=roll0,Roll1=roll1,Track=track,Ease=ease};
        protected static NativeEntranceShot Arc(float at,NativeEntranceCut cut,float fov0,float fov1,Vector3 eye0,Vector3 mid,Vector3 eye1,Vector3 look0,Vector3 look1,byte ease=0,float track=1,float inTime=.16f,float roll0=0,float roll1=0,float face=-1,float trackY=0)
        {var s=Shot(at,cut,fov0,fov1,eye0,eye1,look0,look1,ease,track,inTime,roll0,roll1,face,trackY);s.Eye1=2*mid-.5f*(eye0+eye1);return s;}
        /// <summary>An orbit: degrees from the subject's front (positive toward its left), radius and height, swept on the circle.</summary>
        protected static NativeEntranceShot Orbit(float at,NativeEntranceCut cut,float fov0,float fov1,float deg0,float deg1,float r0,float r1,float h0,float h1,Vector3 look0,Vector3 look1,byte ease=0,float track=1,float inTime=.16f,float roll0=0,float roll1=0,float face=-1,float trackY=0)
            =>new NativeEntranceShot{At=at,Face=face,TrackY=trackY,Follow=-1,In=cut,InTime=inTime,Orbit=true,Eye0=new Vector3(deg0,r0,h0),Eye1=new Vector3((deg0+deg1)*.5f,(r0+r1)*.5f,(h0+h1)*.5f),Eye2=new Vector3(deg1,r1,h1),Look0=look0,Look1=(look0+look1)*.5f,Look2=look1,Fov0=fov0,Fov1=fov1,Roll0=roll0,Roll1=roll1,Track=track,Ease=ease};
        protected static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        /// <summary>The shot with its look following anchor `which` (Look keys become offsets from it).</summary>
        protected static NativeEntranceShot Follow(NativeEntranceShot s,NativeEntranceAnchor which){s.Follow=(int)which;return s;}

        // ---- shared staging helpers ----
        /// <summary>The pylons' power-on sweep (from the back of the ring round to the front, each with a small overshoot),
        /// starting at `from`, plus a flash at the hit and a pulse at the name card.</summary>
        protected float PowerOn(int i,float t,float from,float hitFlash=1.6f)
        {
            int order=Math.Min(i,NativeEntranceStage.Pylons-1-i);float on=from+.045f*(5-order);float level=NativeEntranceMath.Ramp(t,on,on+.12f)*(1+.8f*NativeEntranceMath.Pulse(t,on+.05f,.03f,.12f));
            level*=1+hitFlash*NativeEntranceMath.Pulse(t,Hit,.02f,.22f)+.6f*NativeEntranceMath.Pulse(t,NameAt+.02f*order,.04f,.25f);return level;
        }
        /// <summary>Faint dust motes hanging in the stage air (depth for the moving camera).</summary>
        protected static void Motes(INativeEntranceFx fx,float t,float quality,Vector3 tint,int seed=901)
        {
            for(int i=0;i<36;i++)
            {
                if(NativeEntranceMath.Rand(seed,i+400)>Math.Max(.15f,quality))continue;
                var p=new Vector3(NativeEntranceMath.Signed(seed,i*3)*4.5f,.2f+2.6f*NativeEntranceMath.Rand(seed,i*3+1),NativeEntranceMath.Signed(seed,i*3+2)*4.5f);
                p+=new Vector3((float)Math.Sin(t*.3f+i)*.15f,(float)Math.Sin(t*.21f+i*1.7f)*.12f,(float)Math.Cos(t*.27f+i*.6f)*.15f);
                fx.Glow(p,tint*(.05f+.05f*(float)Math.Sin(t*1.3f+i*2.3f)),0,.035f,0,0,seed+i);
            }
        }
        /// <summary>Light beams from the tops of every other pylon down onto the dais (stacked soft glows; skipped on the low effects setting).</summary>
        protected void Beams(INativeEntranceFx fx,float t,float quality,Vector3 color,float gain=1)
        {
            if(quality<.45f)return;
            for(int i=0;i<NativeEntranceStage.Pylons;i+=2)
            {
                float l=Pylon(i,t)*gain;if(l<=.01f)continue;var p=NativeEntranceStage.Pylon(i);var top=new Vector3(p.X,NativeEntranceStage.GroundY+NativeEntranceStage.PylonHeight,p.Z)-Vector3.Normalize(p)*.25f;
                var bottom=new Vector3(p.X*.2f,NativeEntranceStage.FloorY,p.Z*.2f);NativeEntranceFx.Shaft(fx,t,top,bottom,.06f,.55f,color*.05f,l,9,0,700+i,quality);
            }
        }
        /// <summary>A muzzle flash at `at` along `dir` born at `born`: a white-hot core, a cone of streaks and a soft halo.</summary>
        protected static void MuzzleFlash(INativeEntranceFx fx,float t,float born,Vector3 at,Vector3 dir,float size,Vector3 color,int seed,float quality)
        {
            float a=t-born;if(a<0||a>.09f)return;float k=1-a/.09f;
            fx.Glow(at+dir*(size*.3f),Vector3.One*(5*k),0,size*1.1f,1,0,seed);fx.Glow(at+dir*(size*.6f),color*(3*k),0,size*2.2f,0,0,seed+1);
            for(int i=0;i<7;i++){if(NativeEntranceMath.Rand(seed,i)>Math.Max(.3f,quality))continue;var d=Vector3.Normalize(dir+NativeEntranceMath.Direction(seed,i)*.32f);fx.Streak(at+d*(size*(1.1f+.9f*NativeEntranceMath.Rand(seed,i+9))),d,Vector3.Lerp(Vector3.One*4,color*3,NativeEntranceMath.Rand(seed,i+20))*k,size*.16f,size*.8f,0,.6f,0,seed+i);}
        }
        /// <summary>A tracer leaving `from` along `dir` at `speed` (m/s on the presentation clock) up to `range` m, with a hot head.</summary>
        protected static void Tracer(INativeEntranceFx fx,float t,float born,Vector3 from,Vector3 dir,float speed,float range,Vector3 color,float width,int seed)
        {
            float a=t-born;if(a<0)return;float d=a*speed;if(d>range+2.5f)return;float head=Math.Min(d,range),tail=Math.Max(0,d-2.4f);if(head-tail<.01f)return;
            fx.Streak(from+dir*head,dir*(head-tail),color*3,width,0,1,.9f,0,seed);if(d<range)fx.Glow(from+dir*head,color*2.5f,0,width*7,0,0,seed+1);
        }
        /// <summary>A spent casing kicked out toward `side` at `born`: a brass streak that arcs, bounces once and glints out.</summary>
        protected static void Casing(INativeEntranceFx fx,float t,float born,Vector3 from,Vector3 side,int seed)
        {
            float a=t-born;if(a<0||a>1.1f)return;var v=side*(1.5f+.6f*NativeEntranceMath.Rand(seed,1))+new Vector3(0,1.9f+.6f*NativeEntranceMath.Rand(seed,2),0);
            var p=from+v*a+new Vector3(0,-4.9f*a*a,0);var vel=v+new Vector3(0,-9.8f*a,0);float floor=NativeEntranceStage.FloorY+.02f;if(p.Y<floor){p.Y=floor+(floor-p.Y)*.25f;vel=new Vector3(vel.X*.4f,-vel.Y*.25f,vel.Z*.4f);}
            float spin=(float)Math.Sin(a*40+seed);fx.Streak(p,vel*.02f+side*(.03f*spin),NativeEntranceFx.Hex(0xffc76a,1.6f)*(1-a/1.1f),.018f,.04f,0,.2f,0,seed);
        }
        /// <summary>A floating holographic target (a framed ring and bullseye, gently bobbing) until `burst`, then a spark burst.</summary>
        protected static void Target(INativeEntranceProps props,float t,Vector3 at,Vector3 facing,float burst,Vector3 color)
        {
            if(t>=burst)return;float bob=.05f*(float)Math.Sin(t*2.1f);var p=at+new Vector3(0,bob,0);var q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.Atan2(facing.X,facing.Z))*Quaternion.CreateFromAxisAngle(Vector3.UnitX,(float)Math.PI/2);
            float flicker=.85f+.15f*(float)Math.Sin(t*23);props.Piece(WeaponShape.Ring,true,p,q,new Vector3(.34f,.34f,.34f),color*(2.2f*flicker));props.Piece(WeaponShape.Ring,true,p,q,new Vector3(.18f,.18f,.18f),color*(1.6f*flicker));
            props.Piece(WeaponShape.Sphere,true,p,Quaternion.Identity,new Vector3(.05f),Vector3.One*(3*flicker));props.Piece(WeaponShape.Cube,false,p-new Vector3(0,.48f,0),Quaternion.Identity,new Vector3(.04f,.6f,.04f),NativeEntranceFx.Hex(0x2a3248));
        }
        /// <summary>How fast (0..1) anchor `which` moves at `t` (full at `full` m/s): afterimages show only on fast motion.</summary>
        protected float Speed(NativeEntranceAnchor which,float t,float full)=>NativeEntranceMath.Smooth(Velocity(which,t).Length()/full);
        /// <summary>The subject's right (unit, flat) at presentation time `t`.</summary>
        protected Vector3 RightOf(float t){var f=Anchor(NativeEntranceAnchor.Forward,t);if(f.LengthSquared()<1e-6f)f=Vector3.UnitZ;f=Vector3.Normalize(f);return new Vector3(-f.Z,0,f.X);}
        protected Vector3 ForwardOf(float t){var f=Anchor(NativeEntranceAnchor.Forward,t);if(f.LengthSquared()<1e-6f)return Vector3.UnitZ;return Vector3.Normalize(f);}
        /// <summary>The direction weapon tip `left` points (hand to tip) at `t`.</summary>
        protected Vector3 Aim(bool left,float t){var d=Anchor(left?NativeEntranceAnchor.LeftTip:NativeEntranceAnchor.RightTip,t)-Anchor(left?NativeEntranceAnchor.LeftHand:NativeEntranceAnchor.RightHand,t);return d.LengthSquared()<1e-6f?ForwardOf(t):Vector3.Normalize(d);}
        /// <summary>A trail of anchor `which` from `t` back `span` s (`count` samples), between `from` and `to` (presentation).</summary>
        protected void Trail(INativeEntranceFx fx,NativeEntranceAnchor which,float t,float from,float to,float span,Vector3 head,Vector3 tail,float width,int seed)
        {
            if(t<from||t>to+span)return;Span<Vector3> pts=stackalloc Vector3[16];int n=0;
            for(int i=0;i<16;i++){float s=t-span*i/15f;if(s<from||s>to)continue;pts[n++]=Anchor(which,s);}
            float fade=1-NativeEntranceMath.Ramp(t,to,to+span);NativeEntranceFx.Ribbon(fx,pts,n,head*fade,tail*fade,width,seed);
        }
    }

    /// <summary>A weapon signature: the baked clip and its weapon-intro layer, re-staged (camera, lights, set, effects).</summary>
    public abstract class NativeWeaponEntrance:NativeEntranceScript
    {
        protected NativeEntranceClip Clip;protected readonly string Spec;protected readonly int WeaponColor;
        protected NativeWeaponEntrance(string style,float hit,int color):base(style,hit,color){Spec=style.Substring(7);WeaponColor=color;}
        public override void Bind(NativeEntranceClip clip){if(Clip==clip)return;Clip=clip;Invalidate();}
        public override bool Ready=>Clip!=null;
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            if(Clip==null){pose.Reset();root=new Vector3(0,.88f,0);rotation=Quaternion.Identity;return;}
            NativeEntrancePose.Baked(Style,Clip,t,pose,out root,out rotation);
        }
        protected override float TipLength(bool left)
        {
            switch(Spec){case "sword":return 1.24f;case "greatsword":return 1.62f;case "daggers":return .8f;case "pistol":return .51f;case "rifle":return 1.22f;case "machinegun":return .91f;case "shield":return .22f;}
            return left?.88f:.1f;// staffs ride the left hand
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float key=.3f+.7f*NativeEntranceMath.Ramp(t,.15f,.6f);NativeEntranceStage.Rig(l,key,Accent,1,.25f+.75f*NativeEntranceMath.Ramp(t,.3f,.8f));
            // The hit light: a white-hot flash in the weapon's colour where the weapon lands.
            float flash=NativeEntranceMath.Pulse(t,Hit,.015f,.22f);if(flash>.01f){var at=Anchor(HitAnchor,Hit)+ForwardOf(Hit)*.9f+new Vector3(0,.3f,0);at.Y=Math.Max(at.Y,NativeEntranceStage.FloorY+1.0f);l.Add(at,Vector3.Lerp(Accent,Vector3.One,.5f)*(24*flash),7);}
            // The armour's own glow on the chest (player-intros.ts ArmorGlow, cut to .4 in close-ups).
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f),3.2f);
        }
        protected virtual NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.RightTip;
        public override void Props(float t,INativeEntranceProps props,float quality)=>Stage(props,t,.55f+.45f*NativeEntranceMath.Ramp(t,.1f,.7f),Hit);
    }

    /// <summary>The directed entrances (every non-DEV entrance), by style.</summary>
    public static class NativeEntranceDirector
    {
        private static readonly Dictionary<string,NativeEntranceScript> scripts=new Dictionary<string,NativeEntranceScript>();
        private static readonly Dictionary<string,Func<NativeEntranceScript>> makers=new Dictionary<string,Func<NativeEntranceScript>>
        {
            {"weapon-sword",()=>new FlashDraw()},{"weapon-daggers",()=>new TwinPhantom()},{"weapon-shield",()=>new BastionDrop()},{"weapon-greatsword",()=>new ColossusOath()},
            {"weapon-pistol",()=>new Deadeye()},{"weapon-rifle",()=>new Longwatch()},{"weapon-machinegun",()=>new BeltRattle()},
            {"orbital-drop",()=>new OrbitalDrop()},{"grid-rez",()=>new GridRez()},{"holo-projection",()=>new HoloProjection()},{"blackout",()=>new Blackout()},
            {"skyline-zip",()=>new SkylineZip()},{"hero-landing",()=>new HeroLanding()},{"overclock",()=>new Overclock()},
            {"weapon-fire",()=>new CinderCrown()},{"weapon-water",()=>new TidalSerpent()},{"weapon-lightning",()=>new Thunderstep()},{"weapon-darkness",()=>new EventideGate()},
        };
        /// <summary>True for a style the director stages (the eleven weapon signatures and the procedural entrances).</summary>
        public static bool Directed(string style)=>style!=null&&makers.ContainsKey(style);
        /// <summary>The style's script (made once and kept: its anchors are built on first use).</summary>
        public static NativeEntranceScript Script(string style)
        {
            if(style==null||!makers.TryGetValue(style,out var make))return null;if(!scripts.TryGetValue(style,out var s)){s=make();scripts.Add(style,s);}return s;
        }
        public static IEnumerable<string> Styles=>makers.Keys;
    }
}
