using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The chip draft's 3D stage (owner request 2026-10-10; presentation only). The pilot stands behind the tray: the cards sit
    // in the lower part of the screen, in front of the pilot's waist, and the pilot bows head, neck and upper spine toward
    // them and looks at whichever card is highlighted (the local picker's hover or keyboard focus, else the AI / remote
    // picker's scan). Before the pick the arm on the considered card's side lifts toward it (draft-stage.ts). Every move
    // eases: the gaze and the arms are critically damped springs, the reach, grab and wrist plant run on the
    // slowed timeline below with smoothstep ramps, the framing eases after layout changes, the pick's age runs on a smooth
    // clock (the authoritative one steps with ticks and network frames) and the idle breathes at IdlePace. Positions are the
    // stage rig's root frame in source axes (+x the pilot's left = screen right, +z toward the camera; Unity world is
    // (-x, y + .88, z)). Allocation-free per frame. Rules, timers, picks and messages are not touched.
    public sealed class NativeDraftStage
    {
        // Camera: the source draft stage's 24 degree lens, a little above the framing centre so it looks slightly down.
        public const float Fov=24,Lift=.22f;
        // Where the cards are perceived in front of the pilot: the gaze meets a card's camera ray on the plane z = LookZ, the
        // reaching hand on z = ReachZ (draft-stage.ts DRAFT_REACH_Z; its look plane at .9 comes nearer, for a deeper bow).
        public const float LookZ=.75f,ReachZ=.42f;
        // The outermost card's turn at most (tan 1.1 rad): a wider tray moves the look plane out to keep it there.
        public const float OuterTurn=1.96f;
        // Framing: the head's top (HeadTop above the head joint, HatTop more under a hat, BossTop for the Siren) at least
        // Headroom below the screen's top; the tray's top edge across the waist (Cut, below the motion root); the upper body at
        // most MaxBody of the screen height; at least MinWidth metres across; at most MaxHeight metres tall (never tiny).
        public const float Headroom=.07f,HeadTop=.36f,HatTop=.16f,BossTop=.14f,Cut=-.13f,MaxBody=.56f,MinWidth=1.8f,MaxHeight=4f,DefaultTray=.72f;
        // Springs (rad/s, critically damped): the gaze settles to 95% in about .37 s (.47 s while picking), framing in .8 s.
        // The gaze's target itself turns at most TurnSpeed rad/s (seen from the eye), so a glance across the whole tray takes
        // its time instead of whipping the head through the middle.
        public const float GazeOmega=13,PickGazeOmega=10,FrameOmega=6,TurnSpeed=6;
        // The idle breathes at .75 of the source draft pose's pace (a 5 s breath).
        public const float IdlePace=.75f;
        // The bow: the share of the turn toward the gaze direction taken by the spine, chest and neck in turn (the head takes
        // the rest and looks straight at the point), how much of a downward look the chain follows (LookDown; the source halved
        // it, the stage bows) and of an upward one, and the limits (the turn wider than the source's 1.05 rad, so the outer
        // card of a wide or two-row tray still reads as the one looked at; the spine, chest and neck carry half of it).
        public const float SpineShare=.2f,ChestShare=.3f,NeckShare=.4f,LookDown=1,LookUp=.55f,YawLimit=1.3f,PitchDown=.8f,PitchUp=.4f,EyeUp=.165f;
        // Reach: shoulder to wrist at most ArmReach when taking the card and Consider (draft-stage.ts CONSIDER, a soft elbow)
        // while considering it, the hand kept Beside its own side of the body.
        public const float ArmReach=.5f,Consider=.495f,Beside=.16f;
        // Considering (draft-stage.ts): the arm on the local picker's highlighted card's side lifts toward it (weight spring
        // LiftOmega) while the other lowers a little quicker (LowerOmega), so a switch of sides never holds both arms up; each
        // arm's aim point follows the card on the reach plane (AimOmega). The arm that was lifting keeps lifting through the
        // pick, and the pick's reach takes the larger of the two.
        public const float LiftOmega=7,LowerOmega=9,AimOmega=9;
        // The share of the squared sideways distance that flattens a look's pitch (Aim).
        public const float SideReach=.25f;
        // The pick's timeline (seconds after the authoritative pick, with NativeDraftPlant's slowed plant): the hand starts down
        // toward the card at ReachStart and closes on it at NativeDraftPlant.Contact; the card starts flying into the palm at
        // FlyStart and fades in the hand over the FadeSpan before the contact; the gaze passes from the card to the hand over
        // the HandOver before it.
        public const float ReachStart=.16f,FlyStart=.12f,FadeSpan=.1f,HandOver=.2f;
        public static float Reach(float age)=>MathUtil.Smooth((age-ReachStart)/(NativeDraftPlant.Contact-ReachStart));
        public static float Fly(float age)=>MathUtil.Smooth((age-FlyStart)/(NativeDraftPlant.Contact-FlyStart));
        public static float Fade(float age)=>MathUtil.Smooth((age-(NativeDraftPlant.Contact-FadeSpan))/FadeSpan);
        // The card the pilot considers: the chosen one, else the local picker's hover or keyboard focus, else the boot scan
        // (following the entrance, .22 s a card) and then a slow cycle (1.8 s a card); reduced motion holds the first card.
        public const float BootStep=.22f,ScanStep=1.8f;
        public static int Focus(int chosen,int hover,float since,int count,bool reduced)
        {
            if(count<=0)return -1;if(chosen>=0)return Math.Min(chosen,count-1);if(hover>=0)return Math.Min(hover,count-1);if(reduced)return 0;
            since=Math.Max(0,since);return since<count*BootStep?Math.Min(count-1,(int)(since/BootStep)):(int)(since/ScanStep)%count;
        }
        // Exact critically damped spring step: one step of dt or many smaller ones land on the same curve (any frame rate).
        public static void Spring(ref float x,ref float v,float target,float omega,float dt)
        {float d=x-target,k=(v+omega*d)*dt,e=(float)Math.Exp(-omega*dt);x=target+(d+k)*e;v=(v-omega*k)*e;}
        // The same spring (per axis) following a target that moves evenly from `from` to `to` over the step (exact for a ramp: the
        // offset from the ramp's steady lag decays like a still target), so a moving target agrees at any frame rate too.
        public static void Spring(ref Vector3 x,ref Vector3 v,Vector3 from,Vector3 to,float omega,float dt)
        {
            if(dt<=0)return;var s=(to-from)/dt;var lag=s*(2/omega);var y=x-from+lag;var w=v-s;var k=(w+omega*y)*dt;float e=(float)Math.Exp(-omega*dt);
            x=(y+k)*e+to-lag;v=(w-omega*k)*e+s;
        }

        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly NativeDraftPlant Plant=new NativeDraftPlant();
        // Outputs: the camera (root frame), the smoothed gaze point, the reaching side, the palm of that hand, the presentation
        // clock (seconds), the smoothed pick age (-1 before the pick) and the look plane's depth.
        public Vector3 CameraPosition,CameraTarget,Gaze,Palm;public float Aspect=16f/9,Clock,Age=-1,LookDepth=LookZ;public bool Left;
        private readonly float restHead;private readonly Quaternion[] restArm=new Quaternion[3];
        private float frameHeight,frameCentre,frameHeightV,frameCentreV,lastAge=-1;private Vector3 gazeV,lead;private bool framed,gazed,picked,carry;
        // Per arm (0 the left, 1 the right): the considering weight, its velocity and last target, and the aim point's spring.
        private readonly float[] armW=new float[2],armV=new float[2],armGoal=new float[2];private readonly Vector3[] armAim=new Vector3[2],armAimV=new Vector3[2];private readonly bool[] armAimed=new bool[2];
        public float Weight(bool left)=>armW[left?0:1];
        public NativeDraftStage(){NativeDraftRules.Pose(Pose,0);Pose.World();restHead=Pose.WorldPosition[CapturedRig.Head].Y;}
        // A new turn or pilot: the springs restart at their targets on the next frame and the arms start in the idle.
        public void Reset(){framed=gazed=picked=carry=false;Age=lastAge=-1;for(int i=0;i<2;i++){armW[i]=armV[i]=armGoal[i]=0;armAimV[i]=Vector3.Zero;armAimed[i]=false;}}

        // The pick's age on a smooth clock: the authoritative age (`raw`, -1 before the pick) steps with the simulation tick or
        // the network frames, so the stage runs its own copy on the frame time and eases it onto the authority; it snaps on a
        // new pick or a jump and never runs backwards.
        public float SmoothAge(float raw,float dt)
        {
            if(raw<0)return Age=-1;if(Age<0||Math.Abs(raw-Age)>.25f)return Age=raw;
            float next=Age+dt;next+=(raw-next)*(1-(float)Math.Exp(-4*dt));return Age=Math.Max(Age,next);
        }
        // The idle (the source draft pose at IdlePace) for this frame; the runtime may replace it (the Siren's own idle) before Step.
        public void Begin(float dt,bool reduced){Clock+=Math.Max(0,Math.Min(.1f,dt));NativeDraftRules.Pose(Pose,reduced?0:Clock*IdlePace);}

        // Framing: `aspect` the screen's, `trayTop` the top edge of the first card row (fraction of the screen height from the
        // top; negative while the tray is not laid out yet: a default, and the framing snaps again on the first real one).
        // Snaps on the first frame after a Reset, then eases, so a resize or a re-laid tray never cuts.
        public void Frame(float aspect,float trayTop,bool hat,bool boss,float dt)
        {
            bool known=trayTop>=0&&MathUtil.Finite(trayTop);if(!known){trayTop=DefaultTray;framed=false;}
            Aspect=Math.Max(.2f,aspect);float top=restHead+HeadTop+(hat?HatTop:0)+(boss?BossTop:0),body=top-Cut,cut=MathUtil.Clamp(trayTop,.2f,.92f);
            float need=body/Math.Max(.05f,cut-Headroom),height=Math.Min(MaxHeight,Math.Max(need,Math.Max(body/MaxBody,MinWidth/Aspect)));
            // The tray's top edge across the waist, unless that would push the head off the top (a tall tray): then the head
            // keeps its headroom and the cards cover more of the body.
            float centre=Math.Max(Cut+(cut-.5f)*height,top+(Headroom-.5f)*height);
            if(!framed){frameHeight=height;frameCentre=centre;frameHeightV=frameCentreV=0;framed=known;}
            else{dt=Math.Max(0,Math.Min(.1f,dt));Spring(ref frameHeight,ref frameHeightV,height,FrameOmega,dt);Spring(ref frameCentre,ref frameCentreV,centre,FrameOmega,dt);}
            CameraTarget=new Vector3(0,frameCentre,0);CameraPosition=new Vector3(0,frameCentre+Lift,frameHeight*.5f/(float)Math.Tan(Fov*Math.PI/360));
        }
        // The camera ray through a screen point (fractions: x from the left, y from the top) where it meets the plane z.
        public Vector3 OnPlane(float sx,float sy,float z)
        {
            Basis(out var f,out var r,out var u,out float t);var d=f+r*((2*sx-1)*t*Aspect)+u*((1-2*sy)*t);
            return CameraPosition+d*((z-CameraPosition.Z)/Math.Min(-1e-4f,d.Z));
        }
        // A root-frame point as screen fractions (x from the left, y from the top).
        public Vector2 Project(Vector3 p)
        {
            Basis(out var f,out var r,out var u,out float t);var d=p-CameraPosition;float z=Math.Max(1e-4f,Vector3.Dot(d,f));
            return new Vector2((Vector3.Dot(d,r)/(z*t*Aspect)+1)*.5f,(1-Vector3.Dot(d,u)/(z*t))*.5f);
        }
        private void Basis(out Vector3 f,out Vector3 r,out Vector3 u,out float t)
        {f=Vector3.Normalize(CameraTarget-CameraPosition);r=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));u=Vector3.Cross(r,f);t=(float)Math.Tan(Fov*Math.PI/360);}

        // One frame over the idle in Pose (after Begin and Frame). `dt` presentation seconds, `age` the smoothed pick age (-1
        // before the pick), `focus` the considered card (Focus), `consider` whether that card is the local picker's highlighted
        // one (the arm on its side lifts toward it; never for the scan), `cards` the card centres as screen fractions (first
        // `count`). The Siren drafter keeps her idle arms and holds the chip (no reach or plant). World() is current on return.
        public void Step(float dt,float age,int focus,bool consider,Vector2[] cards,int count,bool siren,bool reduced)
        {
            dt=Math.Max(0,Math.Min(.1f,dt));var p=Pose;p.World();count=cards==null?0:Math.Min(count,cards.Length);focus=count>0?Math.Max(0,Math.Min(count-1,focus)):-1;
            // The look plane moves out for a wide or two-row tray, so the outermost card stays inside the head's turn.
            LookDepth=LookZ;for(int i=0;i<count;i++)LookDepth=Math.Max(LookDepth,Math.Abs(OnPlane(cards[i].X,cards[i].Y,LookZ).X)/OuterTurn);
            var card=focus>=0?OnPlane(cards[focus].X,cards[focus].Y,LookDepth):new Vector3(0,Cut,LookZ);var reachGoal=focus>=0?OnPlane(cards[focus].X,cards[focus].Y,ReachZ):card;
            // The reaching hand is the one on the chosen card's side, chosen once per pick; if that arm was already lifting
            // toward a card it keeps lifting (no dip before the pick's reach takes over).
            if(age<0)picked=false;else if(!picked){picked=true;Left=reachGoal.X>=0;carry=armGoal[Left?0:1]>0;}
            bool planting=age>=0&&!siren;float contact=NativeDraftPlant.Contact;
            // The arms' considering springs: before the pick the side of the local picker's highlighted card, from the pick the
            // reaching side; never for the Siren drafter.
            int lifting=siren?-1:age>=0?(Left?0:1):consider&&focus>=0?(reachGoal.X>=0?0:1):-1;
            for(int i=0;i<2;i++)
            {
                bool mine=i==lifting;float goal=mine&&(age<0||carry)?1:0;armGoal[i]=goal;
                Spring(ref armW[i],ref armV[i],goal,goal>armW[i]?LiftOmega:LowerOmega,dt);armW[i]=MathUtil.Clamp(armW[i],0,1);
                if(mine){if(armAimed[i])Spring(ref armAim[i],ref armAimV[i],reachGoal,reachGoal,AimOmega,dt);else{armAim[i]=reachGoal;armAimV[i]=Vector3.Zero;armAimed[i]=true;}}
            }
            // The gaze: the card; over the last .2 s before the contact it passes to the hand taking it, then follows the chip in
            // the hand (last frame's) to the plant, and as the arms settle it eases to a point low in front, where the tray
            // was. Every hand-over blends, so no target jumps.
            var target=card;
            if(age>=0)
            {
                var held=siren||lastAge<contact?Palm:Plant.ChipShown?Plant.Chip:Plant.At;target=Vector3.Lerp(card,held,MathUtil.Smooth((age-(contact-HandOver))/HandOver));
                if(!siren)target=Vector3.Lerp(target,new Vector3(0,Cut,LookZ),MathUtil.Smooth((age-NativeDraftPlant.ReturnStart)/(NativeDraftPlant.ReturnEnd-NativeDraftPlant.ReturnStart)));
            }
            if(!gazed){Gaze=lead=target;gazeV=Vector3.Zero;gazed=true;}else{var from=lead;Turn(ref lead,target,new Vector3(0,restHead+EyeUp,0),TurnSpeed*dt);Spring(ref Gaze,ref gazeV,from,lead,age>=0?PickGazeOmega:GazeOmega,dt);}
            lastAge=age;
            // The bow: spine and chest take their share of the look first (the arms ride on the chest), then the arms, then the
            // neck and head finish the look, so the reach and the plant land where they are solved.
            var eye=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,p.WorldRotation[CapturedRig.Head]);var aim=Aim(eye,Gaze);var along=Vector3.Normalize(aim-eye);
            Turn(p,CapturedRig.Spine,along,SpineShare);Turn(p,CapturedRig.Chest,along,ChestShare);p.World();
            // The arms: down and forward to their aim on the reach plane (arm's length at most: Consider, stretching to ArmReach
            // with the pick's reach; on the hand's own side), from where the idle left the wrist over a forward arc; the joints
            // ease from the idle into the solve. The reaching arm takes the larger of its considering weight and the pick's
            // reach, and from the contact the plant carries it (the other arm, lowering, is raised for the plant from the idle
            // pose it is blending to).
            var plantLocal=Vector3.Zero;
            for(int s=0;s<2;s++)
            {
                bool left=s==0,reaching=planting&&left==Left;float w=armW[s];if(reaching)w=Math.Max(w,Reach(age));if(!armAimed[s]||w<=1e-4f&&!reaching)continue;
                int c=left?CapturedRig.LClav:CapturedRig.RClav,a=c+1,hand=c+3;var shoulder=p.WorldPosition[a];var dir=armAim[s]-shoulder;float len=dir.Length(),ext=reaching?Consider+(ArmReach-Consider)*Reach(age):Consider;
                var point=len>1e-5f?shoulder+dir*(Math.Min(len,ext)/len):shoulder;point.X=left?Math.Max(Beside,point.X):Math.Min(-Beside,point.X);
                var rest=p.WorldPosition[hand];var control=shoulder+new Vector3(left?.08f:-.08f,-.3f,.3f);point=point*(w*w)+control*(2*w*(1-w))+rest*((1-w)*(1-w));
                var local=Vector3.Transform(point-p.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]));if(reaching)plantLocal=local;
                if((!reaching||age<contact)&&w>1e-4f)
                {
                    for(int i=0;i<3;i++)restArm[i]=p.Rotation[a+i];
                    CapturedRig.Arm(p,left,local,NativeDraftPlant.ContactWrist,new Vector3(left?1:-1,-.8f,-.25f));
                    float k=MathUtil.Smooth(w/.6f);if(k<1)for(int i=0;i<3;i++)p.Rotation[a+i]=Quaternion.Slerp(restArm[i],p.Rotation[a+i],k);
                }
            }
            if(planting)Plant.Pose(p,Left,plantLocal,age);
            // The head looks from where the bow carried the eye, within the same limits.
            Turn(p,CapturedRig.Neck,along,NeckShare);p.World();eye=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,p.WorldRotation[CapturedRig.Head]);Look(p,Aim(eye,Gaze));p.World();
            if(planting)Plant.Place(p,Left,age);
            int h=Left?CapturedRig.LHand:CapturedRig.RHand;Palm=p.WorldPosition[h]+Vector3.Transform(NativeDraftPlant.PalmLocal,p.WorldRotation[h]);
        }
        // The look point the chain turns to: the gaze seen from the eye with the downward part followed by LookDown (upward by
        // LookUp), the pitch taken over the depth and a quarter of the sideways distance (an outer card still gets a bow, as on
        // a counter in front), the turn limited to YawLimit and the pitch to PitchDown / PitchUp (draft-stage.ts lookAt
        // limits, the turn widened).
        private static Vector3 Aim(Vector3 eye,Vector3 gaze)
        {
            var d=gaze-eye;d.Y*=d.Y>0?LookUp:LookDown;float flat=(float)Math.Sqrt(SideReach*d.X*d.X+d.Z*d.Z),distance=Math.Max(.3f,d.Length());
            float yaw=MathUtil.Clamp((float)Math.Atan2(d.X,Math.Max(.05f,d.Z)),-YawLimit,YawLimit),pitch=MathUtil.Clamp((float)Math.Atan2(d.Y,flat),-PitchDown,PitchUp);
            return eye+new Vector3((float)(Math.Sin(yaw)*Math.Cos(pitch)),(float)Math.Sin(pitch),(float)(Math.Cos(yaw)*Math.Cos(pitch)))*distance;
        }
        // Move `point` toward `target` by at most `step` radians of turn seen from `pivot` (yaw and pitch, the distance in
        // proportion): a constant-speed turn whatever the frame rate, arriving exactly.
        private static void Turn(ref Vector3 point,Vector3 target,Vector3 pivot,float step)
        {
            var a=point-pivot;var b=target-pivot;float da=a.Length(),db=b.Length();if(da<1e-4f||db<1e-4f){point=target;return;}
            float ya=(float)Math.Atan2(a.X,a.Z),pa=(float)Math.Asin(MathUtil.Clamp(a.Y/da,-1,1)),yb=(float)Math.Atan2(b.X,b.Z),pb=(float)Math.Asin(MathUtil.Clamp(b.Y/db,-1,1));
            float dy=yb-ya,dp=pb-pa,angle=(float)Math.Sqrt(dy*dy+dp*dp);if(angle<=step){point=target;return;}
            float k=step/angle,yaw=ya+dy*k,pitch=pa+dp*k,distance=da+(db-da)*k;
            point=pivot+new Vector3((float)(Math.Sin(yaw)*Math.Cos(pitch)),(float)Math.Sin(pitch),(float)(Math.Cos(yaw)*Math.Cos(pitch)))*distance;
        }
        // Turn `bone` so its forward (+z) takes `weight` of the way toward the world `direction` (the bow: each joint of the
        // chain turns part of what its parents left).
        private static void Turn(CapturedRig.Pose p,int bone,Vector3 direction,float weight)
        {
            p.World();var q=p.WorldRotation[bone];var change=Quaternion.Slerp(Quaternion.Identity,CapturedRig.FromTo(Vector3.Transform(Vector3.UnitZ,q),direction),weight);
            p.Rotation[bone]=Quaternion.Inverse(p.WorldRotation[CapturedRig.Bones[bone].Parent])*change*q;
        }
        // The head's forward from the eye straight at `target`.
        private static void Look(CapturedRig.Pose p,Vector3 target)
        {
            p.World();var q=p.WorldRotation[CapturedRig.Head];var direction=target-(p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,q));
            if(direction.LengthSquared()>1e-8f)Turn(p,CapturedRig.Head,Vector3.Normalize(direction),1);
        }
    }
    // The local picker's highlighted card held across the gaps between cards (the pointer leaves one card well before it
    // reaches the next, and the scan card in between can be at the far end of the tray): the last hovered or keyboard-focused
    // card stays the considered one until another is highlighted or the turn ends (Reset), so the head never swings toward
    // an unrelated card; the arm keeps reaching for it for HoldReach after the pointer left it, then lowers. Presentation only.
    public sealed class NativeDraftHover
    {
        public const float HoldReach=.6f;
        public int Card{get;private set;}=-1;private float away;
        public void Reset(){Card=-1;away=0;}
        // `hover` this frame's highlighted card (-1 none), `dt` frame seconds; returns the held card (-1 before any).
        public int Step(int hover,float dt){if(hover>=0){Card=hover;away=0;}else if(Card>=0)away+=Math.Max(0,dt);return Card;}
        // Whether the held card is still being reached for (the pointer on it, or between cards for under HoldReach).
        public bool Reaching=>Card>=0&&away<HoldReach;
    }
}
