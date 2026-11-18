using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The chip draft's 3D stage (owner 2026-10-10: the pilot "from the chest and up" with a display of chips in front of them;
    // 2026-10-11: "zoomed in a lot more; essentially solar-plexus area and up; not as far back as they are"; presentation only).
    // The camera comes in close for a portrait from a little above the eyes: the visible pilot runs from the solar plexus (Plexus,
    // the lower sternum) up to the head's top with a little crown room (a tall hat's tip or the Auditor halo's flames may cross
    // the top edge, the pilot staying close to the bare-headed scale), the plexus just above the designed 2D tray's top line, so
    // nothing of the pilot that matters hides behind the cards (NativeDraftScreen keeps their art, sizes and motion). There is no
    // table: the hands rest low in the lap under the screen's lower shade (ShadeLine / ShadeDeep: opaque just under the plexus
    // line in every state, NativeDraftScreen.DraftShades), so below the plexus only the cards and a raised hand show. The pilot
    // looks at whichever card is highlighted (the local picker's hover or keyboard focus, else the AI / remote picker's scan),
    // turning the head toward it with the face kept to the camera, and the hand on that card's side rises out of the shade toward
    // it, above the tray's top line (draft-stage.ts consider); the pick carries that hand on to the catch in front of the chest,
    // palm up, the chosen card flies to that catch (CatchPalm) and the wrist plant follows in front of the chest
    // (NativeDraftPlant). Every move eases: the gaze and the arms are critically damped springs, the catch and the wrist plant run
    // on the slowed timeline below with smoothstep ramps, the framing eases after layout changes, the pick's age runs on a smooth
    // clock (the authoritative one steps with ticks and network frames) and the idle breathes at IdlePace. Positions are the
    // stage rig's root frame in source axes (+x the pilot's left = screen right, +z toward the camera; Unity world is (-x, y + .88,
    // z)). Allocation-free per frame. Rules, timers, picks and messages are not touched.
    public sealed class NativeDraftStage
    {
        // Camera: the source draft stage's 24 degree lens (about a 56 mm portrait lens), brought in to the close framing rather
        // than narrowed (2.8 m from the pilot at 16:9 where the table framing stood 3.7 m back), tilted Tilt degrees down at the
        // pilot. With no table to look across, the lens sits at about eye height (12 degrees at the close distance put it over
        // the head's top, looking down onto it).
        public const float Fov=24,Tilt=8;
        // Seated: the lean toward the cards (spine, rad). The Siren's idle stands her SirenSink taller, so she sits that much lower
        // (her chest and head where the pilot's are).
        public const float Lean=.08f,SirenSink=.2f;
        // Resting hands (root frame, low in the lap, inside the lower shade's opaque band on every screen): wrists RestX either
        // side, RestY high and RestZ in front, palms down with the fingers turned RestTurn rad in; the elbows out and back
        // (RestPole, chest frame).
        public const float RestX=.2f,RestY=-.12f,RestZ=.24f,RestTurn=.35f;
        public static readonly Vector3 RestPole=new Vector3(.4f,-.5f,-.6f),ReachPole=new Vector3(1,-.8f,-.25f);
        // Framing: the head's top (HeadTop above the head joint) and its crown room at Headroom below the screen's top, and the
        // resting solar plexus PlexusGap above the tray's first row. Under a hat or the Auditor halo the crown room is its
        // measured top (the crown Frame takes) with CrownGap over it for the bow and the turn, but never more than CrownRoom over
        // the bare head's top (CrownTaken): a taller hat's tip or the halo's flames cross the screen's top edge while the pilot
        // stays close to the bare-headed scale (the hats' brims and lower parts and the halo's ring in view). The Siren drafter's
        // head is the pilot's (her look pieces sit on the face), so she takes only BossTop more for her idle's rise. The visible pilot
        // (head top to plexus) at most MaxSeat of the screen's height (never closer: the plexus keeps its row and the head gains
        // room) and at least MinSeat (never smaller: a tray taller still then covers the lower chest, the head keeping its room), at
        // least MinWidth metres across (the shoulders and the raised hands), at most MaxHeight tall. A crown is taken up to
        // MaxCrown. MinSeat was .5 until 2026-10-29 (owner: "The player is under chips if theres around 12 chips"): two rows of
        // cards then hid the pilot to the chin; at .3 the pilot sits above the second row on every screen. The player's model
        // (ModelHeadTop: its hair's top over the head joint) is framed by its own head, the voxel body's block by HeadTop.
        public const float Headroom=.05f,HeadTop=.36f,ModelHeadTop=.3f,CrownGap=.03f,CrownRoom=.2f,MaxCrown=1.5f,BossTop=.04f,PlexusGap=.04f,MaxSeat=.8f,MinSeat=.3f,MinWidth=1.2f,MaxHeight=4f,DefaultTray=.72f;
        // The room Frame takes over the bare head's top (`headTop` over the head joint) for a worn cosmetic whose measured top is
        // `crown` (m over the head joint).
        public static float CrownTaken(float crown,float headTop=HeadTop)=>crown>0&&MathUtil.Finite(crown)?MathUtil.Clamp(Math.Min(crown,MaxCrown)+CrownGap-headTop,0,CrownRoom):0;
        // The screen's lower shade (NativeDraftScreen.DraftShades): from just above its line (ShadeLine: the tray's top, or
        // PlexusGap under the resting plexus when a tall tray covers the plexus) it deepens to ShadeOpaque by ShadeDrop under the
        // resting plexus as the eased camera sees it, so with the pilot's size on every screen (ShadeDeep: at least ShadeRamp of
        // the screen under the line), and holds there, so below the solar plexus the lap, the resting hands and the legs stay
        // hidden in every state (browse, pick, the "Upgrade secured" hold and the details) while the cards, the nameplate and a
        // raised hand draw over it. Rows are fractions of the screen height from the top; until the first framing the line is the
        // tray's top and the shade turns opaque DefaultRamp under it.
        public const float ShadeDrop=.15f,ShadeRamp=.02f,DefaultRamp=.1f,ShadeOpaque=1;
        // Springs (rad/s, critically damped): the gaze settles to 95% in about .37 s (.47 s while picking), framing in .8 s.
        // The gaze's target itself turns at most TurnSpeed rad/s (seen from the eye), so a glance across the whole display takes
        // its time instead of whipping the head through the middle.
        public const float GazeOmega=13,PickGazeOmega=10,FrameOmega=6,TurnSpeed=6;
        // The lock-in: for LockSpan after the pick the gaze's spring runs at LockGazeOmega, so the visor snaps onto the card.
        public const float LockGazeOmega=18,LockSpan=.15f;
        // The install's look down at most (rad): the head bows onto the bay, its face still to the camera.
        public const float InstallPitch=.32f;
        // The idle breathes at .75 of the source draft pose's pace (a 5 s breath).
        public const float IdlePace=.75f;
        // The bow: the share of the turn toward the gaze direction taken by the spine, chest and neck in turn (the head takes
        // the rest and looks straight at the point), how much of a downward look the chain follows (LookDown) and of an upward
        // one, and the limits (the turn wider than the source's 1.05 rad, so the outer card of a wide or two-row display still
        // reads as the one looked at). Seated, the spine and chest bow less than standing (the cards are near); the Siren,
        // already hunched in her idle, keeps her chest and follows a smaller share of a downward look (SirenDown), so her face
        // reads. Close up, a card is read mostly through the turn and less through the pitch, so the visor stays toward the
        // camera (its forward at least FaceCamera of the way to the lens) on every card.
        public const float SpineShare=.14f,ChestShare=.24f,NeckShare=.4f,LookDown=.75f,SirenDown=.4f,LookUp=.55f,YawLimit=1.3f,PitchDown=.6f,PitchUp=.4f,EyeUp=.165f,FaceCamera=.7f;
        // A card as seen on the display: the gaze meets its camera ray at the depth LookZ, well in front of the pilot (nearer the
        // camera still for the outer cards of a wide tray, so their turn from an eye at EyeZ stays inside OuterTurn = tan .7 rad):
        // the head points along the camera ray through the card with a moderate turn. With no card in mind (and after the pick)
        // the gaze rests on the display straight ahead, RestLook under the tray's top line.
        // A two-row tray's lower cards likewise: the depth comes nearer (in LookStep steps, short of the lens by LensGap) until
        // the look down from the eye stays inside OuterDrop (the smaller pilot over a tall tray looks down further).
        public const float LookZ=1.3f,EyeZ=.3f,OuterTurn=.84f,RestLook=.1f,OuterDrop=.6f,LookStep=.1f,LensGap=.3f;
        // Considering: the hand rises out of the lap ConsiderReach of the way to its catch (above the tray's top line, in front of
        // the lower chest on its card's side), slid ConsiderSlide toward the card and turning ConsiderTurn of the way to the open
        // palm (draft-stage.ts CONSIDER, a soft elbow), kept Beside its own side of the body, the path arching Arc above the
        // straight line. The catch: the reaching wrist rises in front of the chest on the card's side, palm up, CatchUp above the
        // resting plexus and CatchOut in front of it, out from the midline by CatchSpread of the card's sideways offset (between
        // Beside and CatchWide), at most ArmReach from the shoulder. A pilot's left hand takes every card (the chip goes into the
        // right wrist's device): for a card on the right it reaches across to CrossBeside from the midline, just short of it.
        public const float ArmReach=.5f,ConsiderReach=.85f,ConsiderSlide=.06f,ConsiderTurn=.3f,Beside=.16f,Arc=.08f,CatchUp=.18f,CatchOut=.26f,CatchSpread=.35f,CatchWide=.3f,CrossBeside=.04f,CrossBlend=.12f;
        /// <summary>How far a hand's card is on its own side, 0..1 (1 on its side, 0 across the body), blended over CrossBlend of the
        /// card's sideways offset either side of the middle, so a highlight crossing the middle never jumps the hand.</summary>
        public static float Own(bool left,Vector3 aim)=>MathUtil.Smooth(((left?aim.X:-aim.X)+CrossBlend)/(2*CrossBlend));
        // Considering (draft-stage.ts): the arm on the local picker's highlighted card's side lifts toward it (weight spring
        // LiftOmega) while the other lowers a little quicker (LowerOmega), so a switch of sides never holds both arms up; each
        // arm's aim point follows the card (AimOmega). The arm that was lifting keeps lifting through the pick, and the pick's
        // catch takes the larger of the two.
        public const float LiftOmega=7,LowerOmega=9,AimOmega=9;
        // The share of the squared sideways distance that flattens a look's pitch (Aim).
        public const float SideReach=.25f;
        // The pick's timeline (seconds after the authoritative pick: the install, NativeInstallShot): the hand cocks at the lock and
        // leaves for the card at ReachStart on an easing arc that overshoots the catch by Overshoot toward the card and comes back to
        // it at NativeInstallShot.Catch, where the cartridge flies into its pinch; the card leaves its slot at FlyStart (the 3D
        // cartridge takes over at NativeInstallShot.Burst); the gaze passes from the card to the cartridge about the burst.
        public const float ReachStart=NativeInstallShot.ReachStart,FlyStart=NativeInstallShot.FlyStart,FadeSpan=.1f,HandOver=.2f,Overshoot=.02f,SirenLunge=.03f,Cock=.02f,CockTurn=.35f;
        // (An ease out that starts from rest: its first .15 eased in, so the hand never jumps to its top speed.)
        public static float Reach(float age){float u=(age-ReachStart)/(NativeInstallShot.Catch-ReachStart);u=MathUtil.Clamp(u,0,1);float i=1-u;return (1-i*i*i*Math.Max(0,1-.4f*u))*MathUtil.Smooth(u/.1f);}
        public static float Fly(float age)=>NativeInstallShot.Fly(age);
        public static float Fade(float age)=>MathUtil.Smooth((age-(NativeInstallShot.Burst-FadeSpan))/FadeSpan);
        /// <summary>The reach's overshoot past its line toward the card (0..1 of Overshoot): out and back by the catch.</summary>
        public static float Over(float age)=>NativeInstallShot.Bump((((age-ReachStart)/(NativeInstallShot.Catch-ReachStart))-.55f)/.45f);
        /// <summary>The cock at the lock (0..1): the hand draws back and its wrist extends, then the reach takes it.</summary>
        public static float Cocked(float age)=>NativeInstallShot.Ramp(age,0,.08f)*(1-NativeInstallShot.Ramp(age,.10f,.26f));
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
        // The resting wrist (root frame) in the lap and its turn: palm down, fingers forward and turned in.
        public static Vector3 RestHand(bool left)=>new Vector3(left?RestX:-RestX,RestY,RestZ);
        public static Quaternion RestWrist(bool left)
        {
            var fingers=Vector3.Normalize(new Vector3(left?-RestTurn:RestTurn,-.12f,1));var down=-Vector3.UnitY;down=Vector3.Normalize(down-fingers*Vector3.Dot(down,fingers));var y=-fingers;
            return CapturedRig.Basis(Vector3.Cross(y,down),y,down);
        }
        // The solar plexus of a posed rig: the chest's front face (the front of its boxes, along the chest's forward) at the
        // height halfway between the spine joint (the navel's height) and the neck joint (the sternal notch), where the lower
        // sternum lies (the xiphoid sits about halfway between the sternal notch and the navel).
        public static readonly float ChestFront=FrontOf(CapturedRig.Chest);
        private static float FrontOf(int bone){float z=0;foreach(var b in CapturedRig.Boxes)if(b.Bone==bone)z=Math.Max(z,b.Position.Z+b.Size.Z*.5f);return z;}
        public static Vector3 Plexus(CapturedRig.Pose p)
        {
            var q=p.WorldRotation[CapturedRig.Chest];var chest=p.WorldPosition[CapturedRig.Chest];var local=Vector3.Transform((p.WorldPosition[CapturedRig.Spine]+p.WorldPosition[CapturedRig.Neck])*.5f-chest,Quaternion.Inverse(q));
            local.Z=ChestFront;return chest+Vector3.Transform(local,q);
        }

        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly NativeDraftInstall Plant=new NativeDraftInstall();
        // Outputs: the camera (root frame), the smoothed gaze point, the reaching side, the palm of that hand and, from the pick,
        // where that palm meets the card at the contact (CatchPalm: the solved catch with the palm turned up; the palm before the
        // pick), the presentation clock (seconds), the smoothed pick age (-1 before the pick); the seated pilot's resting solar
        // plexus (the framing's lower anchor) and the tray's top line it is framed on (a fraction of the screen height from the top).
        public Vector3 CameraPosition,CameraTarget,Gaze,Palm,CatchPalm;public float Aspect=16f/9,Clock,Age=-1,TrayLine=DefaultTray;public bool Left;public readonly Vector3 RestPlexus;
        // The resting plexus's screen row under the eased camera; -1 until the first framing. The lower shade's line for a tray
        // whose first row's top is `trayTop`, and the row where it turns opaque (ShadeDrop).
        public float PlexusRow=>framed?Project(RestPlexus).Y:-1;
        // Under the install's shot, once the tray's last shard has gone, the line lets go of the tray (over .2 s) and follows the plexus
        // on the shot's camera; it comes back with the camera (TrayFade).
        public float ShadeLine(float trayTop){if(!framed)return trayTop;float plexus=PlexusRow+PlexusGap;return Math.Max(trayTop+(plexus-trayTop)*TrayFade,plexus);}
        public float TrayFade;
        public float ShadeDeep(float line)=>Math.Max(line+ShadeRamp,framed?Project(RestPlexus-new Vector3(0,ShadeDrop,0)).Y:line+DefaultRamp);
        private readonly float restHead;private readonly Quaternion[] restQ=new Quaternion[2];private float lookNear=LookZ;
        private float frameHeight,frameCentre,frameHeightV,frameCentreV,lastAge=-1;private Vector3 gazeV,lead;private bool framed,gazed,picked,carry;private int cardCount;
        // The install's plan from the seated rest (the shot's focus): the bay's seat at the install point, its normal, the visor.
        public readonly Vector3 PlannedBay,PlannedNormal,PlannedVisor;private readonly Vector3 plannedChestAt;private readonly Quaternion plannedChestQ;private float crownTop,crownTaken;
        // The burst: where the 2D card hands over to the 3D cartridge (screen fractions) and the cartridge's width there (a share of the
        // screen's width); valid while Bursting.
        public Vector2 BurstScreen;public float BurstWidth;public bool Bursting;
        // Per arm (0 the left, 1 the right): the considering weight, its velocity and last target, and the aim point's spring.
        private readonly float[] armW=new float[2],armV=new float[2],armGoal=new float[2];private readonly Vector3[] armAim=new Vector3[2],armAimV=new Vector3[2];private readonly bool[] armAimed=new bool[2];
        public float Weight(bool left)=>armW[left?0:1];
        public NativeDraftStage()
        {
            NativeDraftRules.Pose(Pose,0);Pose.World();restHead=Pose.WorldPosition[CapturedRig.Head].Y;restQ[0]=RestWrist(true);restQ[1]=RestWrist(false);
            Pose.Rotation[CapturedRig.Spine]=Pose.Rotation[CapturedRig.Spine]*CapturedRig.Euler(Lean);Pose.World();RestPlexus=Plexus(Pose);
            var cq=Pose.WorldRotation[CapturedRig.Chest];PlannedBay=Pose.WorldPosition[CapturedRig.Chest]+Vector3.Transform(NativeDraftInstall.InstallPoint,cq);PlannedNormal=Vector3.Transform(NativeDraftInstall.NormalWant,cq);
            PlannedVisor=Pose.WorldPosition[CapturedRig.Head]+new Vector3(0,EyeUp-.015f,.02f);plannedChestAt=Pose.WorldPosition[CapturedRig.Chest];plannedChestQ=cq;
        }
        // A new turn or pilot: the springs restart at their targets on the next frame and the arms start at rest in the lap.
        public void Reset(){framed=gazed=picked=carry=false;Age=lastAge=-1;Bursting=false;TrayFade=0;Plant.Reset();for(int i=0;i<2;i++){armW[i]=armV[i]=armGoal[i]=0;armAimV[i]=Vector3.Zero;armAimed[i]=false;}}

        // The pick's age on a smooth clock: the authoritative age (`raw`, -1 before the pick) steps with the simulation tick or
        // the network frames, so the stage runs its own copy on the frame time and eases it onto the authority; it snaps on a
        // new pick or a jump and never runs backwards.
        public float SmoothAge(float raw,float dt)
        {
            if(raw<0)return Age=-1;if(Age<0||Math.Abs(raw-Age)>.25f)return Age=raw;
            float next=Age+dt;next+=(raw-next)*(1-(float)Math.Exp(-4*dt));return Age=Math.Max(Age,next);
        }
        // The idle (the source draft pose at IdlePace) for this frame; the runtime may replace it (the Siren's own idle) before Step.
        // `pace`: the idle's breath held (0) through the install's line-up and its hold (NativeInstallShot).
        public void Begin(float dt,bool reduced,float pace=1){Clock+=Math.Max(0,Math.Min(.1f,dt))*MathUtil.Clamp(pace,0,1);NativeDraftRules.Pose(Pose,reduced?0:Clock*IdlePace);}
        /// <summary>The idle's pace through the install at pose age `p`: held from the line-up to just after the clack.</summary>
        public static float IdleHeld(float p)=>p<0?1:1-NativeInstallShot.Ramp(p,NativeInstallShot.CarryEnd-.04f,NativeInstallShot.AlignEnd)*(1-NativeInstallShot.Ramp(p,NativeInstallShot.Release,1.30f));

        // Framing: `aspect` the screen's, `trayTop` / `trayBottom` the first card row's top edge and the last row's bottom edge
        // (fractions of the screen height from the top; negative while the tray is not laid out yet: a default, and the framing
        // snaps again on the first real one; the bottom only tells a laid-out tray). `crown` is the worn head cosmetic's top
        // above the head joint in the head's frame (m; 0 bare-headed): ArenaPresentation.DraftCrown measures the hat model once
        // per outfit, as the portraits' hatTop does, and takes the Auditor halo's reach (NativeAuditorHalo.Top); Frame takes at
        // most CrownRoom of it over the bare head's top (CrownTaken). Snaps on the first frame after a Reset, then eases, so a
        // resize or a re-laid tray never cuts. The camera is solved so the head's top (with its crown room) and the solar plexus
        // land on their rows.
        public void Frame(float aspect,float trayTop,float trayBottom,float crown,bool boss,float dt,float headTop=HeadTop)
        {
            bool known=trayTop>=0&&MathUtil.Finite(trayTop)&&MathUtil.Finite(trayBottom)&&trayBottom>trayTop;if(!known){trayTop=DefaultTray;framed=false;}
            Aspect=Math.Max(.2f,aspect);float top=restHead+headTop+CrownTaken(crown,headTop)+(boss?BossTop:0),py=RestPlexus.Y,pz=RestPlexus.Z;crownTop=top;crownTaken=CrownTaken(crown,headTop);
            TrayLine=MathUtil.Clamp(trayTop,.3f,.95f);float plexusRow=TrayLine-PlexusGap,headRow=Headroom;
            // The camera's height and centre (on the plane z = 0) that put the head's top and the plexus on their rows.
            float height=(top-py)/Math.Max(.05f,plexusRow-headRow),centre=top-(.5f-headRow)*height;
            for(int i=0;i<6;i++)
            {
                const float e=1e-3f;float r1=Row(top,0,centre,height)-headRow,r2=Row(py,pz,centre,height)-plexusRow;
                float a11=(Row(top,0,centre+e,height)-headRow-r1)/e,a12=(Row(top,0,centre,height+e)-headRow-r1)/e,a21=(Row(py,pz,centre+e,height)-plexusRow-r2)/e,a22=(Row(py,pz,centre,height+e)-plexusRow-r2)/e;
                float det=a11*a22-a12*a21;if(Math.Abs(det)<1e-7f)break;centre-=(r1*a22-r2*a12)/det;height=Math.Max(.3f,height-(a11*r2-a21*r1)/det);
            }
            // Never closer than MaxSeat of the screen nor narrower than MinWidth (then the plexus keeps its row and the head gains
            // room), never smaller than MinSeat nor taller than MaxHeight (then the head keeps its room and the cards cover the
            // lower chest).
            float least=Math.Max((top-py)/MaxSeat,MinWidth/Aspect),most=Math.Max(least,Math.Min(MaxHeight,(top-py)/MinSeat));
            if(height<least){height=least;centre=Solve(py,pz,plexusRow,centre,height);}
            else if(height>most){height=most;centre=Solve(top,0,headRow,centre,height);}
            if(!framed){frameHeight=height;frameCentre=centre;frameHeightV=frameCentreV=0;framed=known;}
            else{float step=Math.Max(0,Math.Min(.1f,dt));Spring(ref frameHeight,ref frameHeightV,height,FrameOmega,step);Spring(ref frameCentre,ref frameCentreV,centre,FrameOmega,step);}
            float distance=frameHeight*.5f/(float)Math.Tan(Fov*Math.PI/360),tilt=Tilt*(float)Math.PI/180;
            CameraTarget=new Vector3(0,frameCentre,0);CameraPosition=CameraTarget+new Vector3(0,(float)Math.Sin(tilt),(float)Math.Cos(tilt))*distance;
        }
        // The screen row (fraction from the top) of the root-frame point (0, y, z) for a camera centred at `centre` showing
        // `height` metres (Frame's camera before easing).
        private static float Row(float y,float z,float centre,float height)
        {
            float tilt=Tilt*(float)Math.PI/180,s=(float)Math.Sin(tilt),c=(float)Math.Cos(tilt),t=(float)Math.Tan(Fov*Math.PI/360),distance=height*.5f/t;
            float dy=y-(centre+s*distance),dz=z-c*distance,depth=-s*dy-c*dz,up=c*dy-s*dz;return (1-up/(Math.Max(1e-4f,depth)*t))*.5f;
        }
        // The camera centre (height fixed) that puts (0, y, z) on `row`.
        private static float Solve(float y,float z,float row,float centre,float height)
        {
            for(int i=0;i<5;i++){const float e=1e-3f;float r=Row(y,z,centre,height)-row,d=(Row(y,z,centre+e,height)-row-r)/e;if(Math.Abs(d)<1e-7f)break;centre-=r/d;}
            return centre;
        }
        // ---- the install's shot (NativeInstallShot), applied after Frame: Project, OnPlane, the shade and the backdrop see one camera ----
        // A dolly, not a zoom (the lens and its tilt kept): from the portrait to the hero framing (HeroHeight metres tall at the focus,
        // between the planned bay and the visor, at most HeroMost times the portrait; Reduced effects ReducedMost, the Siren SirenMost;
        // lowered until the visor, the bay and the pinch sit inside the safe frame), orbiting Orbit toward the device; the PvP readout
        // then eases it out to ReadoutMagnify, panned so the pilot sits at NativeInstallShot.PilotX, and the settle takes it back to the
        // portrait; the short follow-through creeps on by ShortCreep. The clack kicks it (KickPull of its distance, KickRoll of roll for
        // Unity's camera: Project ignores it) and shakes it (ShakeSize at ShakeHz); the lock punches it by Punch. Closed form in `a`.
        public const float HeroHeight=.62f,HeroMost=1.75f,ReducedMost=1.15f,SirenMost=1.3f,ReadoutMagnify=1.12f,Orbit=3*(float)Math.PI/180,ShortCreep=1.08f,Creep=.03f,
            Punch=.02f,KickPull=.012f,KickRoll=.6f,ShakeSize=.003f,ShakeHz=22,ShakeSpan=.15f,SafeLeft=.06f,SafeTop=.08f,SafeBottom=.82f,EyesBelow=.10f,CrownEdge=.04f,BareCrop=.05f;
        /// <summary>The shot this frame: its magnification over the portrait, its weight (0 the portrait .. 1 the hero framing), the hero
        /// framing's magnification after the safe frame, and Unity's roll (degrees).</summary>
        public float Magnify=1,ShotWeight,HeroMagnify=1,Roll;
        /// <summary>The shot for the smoothed pick age `a` (before the pick: the portrait). `turn` the PvP turn's hold after the pick,
        /// `shortCut` the PvE / Deathmatch follow-through, `beside` the readout beside the pilot (else the centred card: no pan).</summary>
        public void Shot(float a,in NativeInstallShot.Profile pr,float turn,bool shortCut,bool siren,bool reduced,bool beside)
        {
            Magnify=1;ShotWeight=0;Roll=0;if(!(a>=0)||!framed)return;
            var t0=CameraTarget;var p0=CameraPosition;float d0=(p0-t0).Length();if(d0<1e-3f)return;float tan=(float)Math.Tan(Fov*Math.PI/360),tilt=Tilt*(float)Math.PI/180;
            var focus=PlannedBay+(PlannedVisor-PlannedBay)*.4f;
            float most=reduced?ReducedMost:siren?SirenMost:HeroMost,hero=MathUtil.Clamp(2*d0*tan/HeroHeight,1,most),orbit=reduced?0:Orbit;
            hero=HeroMagnify=Safe(focus,d0,hero,orbit);
            // (Where even the portrait's distance cannot hold the planned frame, under a tall crown on a narrow screen, the target moves only
            // as far toward the focus as still keeps it, so the crown never rises out of the top.)
            if(hero<=1.0001f&&!Fits(focus,d0,orbit)){float lo=0,hi=1;for(int i=0;i<6;i++){float mid=(lo+hi)*.5f;if(Fits(Vector3.Lerp(t0,focus,mid),d0,orbit))lo=mid;else hi=mid;}focus=Vector3.Lerp(t0,focus,lo);}
            // The keyframes' weights: the push, the readout's pull-back and pan, the settle.
            float push=NativeInstallShot.Cubic((a-NativeInstallShot.PushStart)/(NativeInstallShot.PushEnd-NativeInstallShot.PushStart));
            float pan=!shortCut&&beside?NativeInstallShot.Pan(a):0;
            float settle=shortCut?0:NativeInstallShot.Settle(a,turn);
            float heroM=hero*(1+(pr.Grade>=5&&!reduced?Creep*NativeInstallShot.Ramp(a,NativeInstallShot.CarryEnd,NativeInstallShot.Seat):0))*(shortCut?1+(ShortCreep-1)*NativeInstallShot.Ramp(a,NativeInstallShot.ShortHold,NativeInstallShot.ShortEnd):1);
            float readM=Math.Min(heroM,ReadoutMagnify),readW=heroM>1.001f?(readM-1)/(heroM-1):0;var readT=Vector3.Lerp(t0,focus,readW+(1-(1-readW)*(1-readW)-readW)*MathUtil.Clamp(crownTaken/.08f,0,1));
            float panX=(.5f-NativeInstallShot.PilotX(Aspect))*2*(d0/readM)*tan*Aspect;
            // (Under a crown the target leads the dolly, so a hat's brim never leaves the frame on the way in: it rises first, then the distance
            // closes; bare-headed it keeps pace, so the catch stays over the tray.)
            float lead=MathUtil.Clamp(crownTaken/.08f,0,1),m=1+(heroM-1)*push;var target=Vector3.Lerp(t0,focus,push+(1-(1-push)*(1-push)-push)*lead);float yaw=orbit*push,side=0;
            if(pan>0){m+=(readM-m)*pan;target=Vector3.Lerp(target,readT,pan);yaw*=1-.7f*pan;side=panX*pan;}
            if(settle>0){m+=(1-m)*settle;target=Vector3.Lerp(target,t0,settle);yaw*=1-settle;side*=1-settle;}
            ShotWeight=MathUtil.Clamp(push*(1-settle),0,1);
            // The lock's punch, the clack's kick and shake (none with Reduced effects).
            float kick=0,shake=0;
            if(!reduced)
            {
                m*=1+Punch*NativeInstallShot.Ramp(a,0,.06f)*(1-NativeInstallShot.Ramp(a,.06f,.24f));
                kick=NativeInstallShot.Kick(a-NativeInstallShot.Seat,34,.38f)*pr.Kick;m*=1+KickPull*kick;Roll=KickRoll*kick;
                float u=(a-NativeInstallShot.Seat)/ShakeSpan;if(u>0&&u<1)shake=ShakeSize*pr.Kick*(float)Math.Sin(2*Math.PI*ShakeHz*(a-NativeInstallShot.Seat))*(1-u)*(1-u);
            }
            Magnify=m;Place(target,d0/m,yaw,side,shake,tilt);
        }
        // The camera at `target` (moved `side` along x), `distance` back along the portrait's view turned `yaw` about the vertical.
        private void Place(Vector3 target,float distance,float yaw,float side,float shake,float tilt)
        {
            var back=new Vector3((float)Math.Sin(yaw)*(float)Math.Cos(tilt),(float)Math.Sin(tilt),(float)Math.Cos(yaw)*(float)Math.Cos(tilt));
            CameraTarget=target+new Vector3(side,0,0);CameraPosition=CameraTarget+back*distance+new Vector3(shake,shake*.6f,0);
        }
        // The hero framing's magnification (at most `most`) lowered by bisection until the planned visor, bay and pinch sit inside the
        // safe frame (x .06-.94, y .08 to .82 and above the shot's shade) with the eyes at least .10 below the top edge.
        private float Safe(Vector3 focus,float d0,float most,float yaw)
        {
            if(Fits(focus,d0/most,yaw))return most;float lo=1,hi=most;
            for(int i=0;i<6;i++){float mid=(lo+hi)*.5f;if(Fits(focus,d0/mid,yaw))lo=mid;else hi=mid;}
            return lo;
        }
        private bool Fits(Vector3 focus,float distance,float yaw)
        {
            var keepT=CameraTarget;var keepP=CameraPosition;Place(focus,distance,yaw,0,0,Tilt*(float)Math.PI/180);
            var pinch=PlannedBay+PlannedNormal*.05f+new Vector3(.04f,0,0);float shade=Math.Min(SafeBottom,Project(RestPlexus).Y+PlexusGap-.03f);
            bool In(Vector2 s)=>s.X>=SafeLeft&&s.X<=1-SafeLeft&&s.Y>=SafeTop&&s.Y<=shade;var visor=Project(PlannedVisor);
            // (The crown too: under a hat or the halo the head's top with its crown room, as Frame keeps it, stays under the top edge; a bare
            // head's top may leave it by BareCrop of the height, a close-up's crop, the eyes still EyesBelow under the edge.)
            bool ok=In(visor)&&In(Project(PlannedBay))&&In(Project(pinch))&&visor.Y>=EyesBelow&&Project(new Vector3(0,crownTop,PlannedVisor.Z+.25f)).Y>=(crownTaken>0?CrownEdge:-BareCrop);
            CameraTarget=keepT;CameraPosition=keepP;return ok;
        }
        // The camera ray through a screen point (fractions: x from the left, y from the top) where it meets the plane z.
        public Vector3 OnPlane(float sx,float sy,float z)
        {
            Basis(out var f,out var r,out var u,out float t);var d=f+r*((2*sx-1)*t*Aspect)+u*((1-2*sy)*t);
            return CameraPosition+d*((z-CameraPosition.Z)/Math.Min(-1e-4f,d.Z));
        }
        // A screen point on the display: its camera ray at the look depth (Step: LookZ, nearer the camera for a wide tray).
        public Vector3 OnDisplay(float sx,float sy)=>OnPlane(sx,sy,lookNear);
        // A root-frame point as screen fractions (x from the left, y from the top).
        public Vector2 Project(Vector3 p)
        {
            Basis(out var f,out var r,out var u,out float t);var d=p-CameraPosition;float z=Math.Max(1e-4f,Vector3.Dot(d,f));
            return new Vector2((Vector3.Dot(d,r)/(z*t*Aspect)+1)*.5f,(1-Vector3.Dot(d,u)/(z*t))*.5f);
        }
        private void Basis(out Vector3 f,out Vector3 r,out Vector3 u,out float t)
        {f=Vector3.Normalize(CameraTarget-CameraPosition);r=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));u=Vector3.Cross(r,f);t=(float)Math.Tan(Fov*Math.PI/360);}
        // The catch (root frame): the reaching wrist in front of the chest on its card's side (`aim` the card's display point).
        public Vector3 Catch(bool left,Vector3 aim,Vector3 shoulder)
        {
            float x=CrossBeside+(MathUtil.Clamp(Math.Abs(aim.X)*CatchSpread,Beside,CatchWide)-CrossBeside)*Own(left,aim);var at=new Vector3(left?x:-x,RestPlexus.Y+CatchUp,RestPlexus.Z+CatchOut);
            var d=at-shoulder;float len=d.Length();return len>ArmReach?shoulder+d*(ArmReach/len):at;
        }

        // One frame over the idle in Pose (after Begin, Frame and Shot). `dt` presentation seconds (scaled by the install's pose rate),
        // `age` the pick's pose age (NativeInstallShot.PoseAge of the smoothed age; -1 before the pick), `focus` the considered card
        // (Focus), `consider` whether that card is the local picker's highlighted one (the arm on its side lifts toward it; never for
        // the scan), `cards` the card centres as screen fractions (first `count`). The Siren drafter keeps her claws in her lap until
        // the pick, then strikes at the card with the claw on its side and devours the cartridge (NativeDraftInstall.SirenPose); a
        // pilot installs it (NativeDraftInstall). World() is current on return.
        public void Step(float dt,float age,int focus,bool consider,Vector2[] cards,int count,bool siren,bool reduced)
        {
            dt=Math.Max(0,Math.Min(.1f,dt));var p=Pose;count=cards==null?0:Math.Min(count,cards.Length);focus=count>0?Math.Max(0,Math.Min(count-1,focus)):-1;cardCount=count;
            // Seated: leaning a little toward the cards (the Siren, hunched in her idle, sits lower instead).
            if(siren)p.Position[CapturedRig.Body].Y-=SirenSink;else p.Rotation[CapturedRig.Spine]=p.Rotation[CapturedRig.Spine]*CapturedRig.Euler(Lean);p.World();
            lookNear=LookZ;for(int i=0;i<count;i++)lookNear=Math.Max(lookNear,EyeZ+Math.Abs(OnPlane(cards[i].X,cards[i].Y,LookZ).X)/OuterTurn);
            float eyeY=p.WorldPosition[CapturedRig.Head].Y+EyeUp;
            for(int i=0;i<count;i++)
            {
                float z=lookNear;
                for(int k=0;k<16&&z<CameraPosition.Z-LensGap;k++){if(eyeY-OnPlane(cards[i].X,cards[i].Y,z).Y<=OuterDrop*(z-EyeZ))break;z+=LookStep;}
                lookNear=Math.Max(lookNear,z);
            }
            var rest=OnDisplay(.5f,TrayLine+RestLook);var card=focus>=0?OnDisplay(cards[focus].X,cards[focus].Y):rest;
            // The reaching hand is the left for a pilot (owner 2026-10-22: the chip goes into the device on the right wrist), the
            // one on the chosen card's side for the Siren drafter, chosen once per pick; if that arm was already lifting toward a
            // card it keeps lifting (no dip before the pick's catch takes over).
            if(age<0){if(picked)Plant.Reset();picked=false;Bursting=false;}else if(!picked){picked=true;Left=!siren||card.X>=0;carry=armGoal[Left?0:1]>0;Plant.Reset();Bursting=false;}
            bool planting=age>=0&&!siren,caught=false;float contact=NativeInstallShot.Catch,lower=age>=0?Plant.Settle:0;
            // The arms' considering springs: before the pick the left arm toward the local picker's highlighted card (never the
            // Siren drafter's), from the pick the reaching side.
            int lifting=age>=0?(Left?0:1):!siren&&consider&&focus>=0?0:-1;
            for(int i=0;i<2;i++)
            {
                bool mine=i==lifting;float goal=mine&&(age<0||carry&&lower<=0)?1:0;armGoal[i]=goal;
                Spring(ref armW[i],ref armV[i],goal,goal>armW[i]?LiftOmega:LowerOmega,dt);armW[i]=MathUtil.Clamp(armW[i],0,1);
                if(mine){if(armAimed[i])Spring(ref armAim[i],ref armAimV[i],card,card,AimOmega,dt);else{armAim[i]=card;armAimV[i]=Vector3.Zero;armAimed[i]=true;}}
            }
            // The gaze: the card, then (about the burst) the cartridge in the air and in the hand, the bay from GazeToBay (fully bowed
            // onto it through the line-up), level as the pilot rises into the power, the lens for the look (the short follow-through
            // holds it), the readout on a long turn, and back to the display ahead with the settle. The Siren: the card, the cartridge
            // in her claw, then her laugh at the lens. Every hand-over blends, so no target jumps.
            var target=card;
            if(age>=0)
            {
                var held=Plant.ChipShown?Plant.Chip:Plant.At;
                if(siren){target=Vector3.Lerp(card,lastAge<contact?Palm:held,MathUtil.Smooth((age-(contact-HandOver))/HandOver));target=Vector3.Lerp(target,CameraPosition,NativeInstallShot.Ramp(age,1.34f,1.5f)*(1-NativeInstallShot.Ramp(age,1.75f,2.0f)));}
                else
                {
                    target=Vector3.Lerp(card,held,NativeInstallShot.Ramp(age,NativeInstallShot.Burst-.12f,NativeInstallShot.Burst+.04f));
                    target=Vector3.Lerp(target,Plant.At,NativeInstallShot.Ramp(age,NativeInstallShot.GazeToBay,NativeInstallShot.GazeToBay+.16f));
                    target=Vector3.Lerp(target,new Vector3(0,restHead+EyeUp-.02f,lookNear),NativeInstallShot.Ramp(age,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd));
                    float look=Plant.Short?NativeInstallShot.Ramp(age,NativeInstallShot.PowerEnd,NativeInstallShot.PowerEnd+.2f):NativeInstallShot.Ramp(age,NativeInstallShot.LookStart,NativeInstallShot.LookStart+.12f)*(1-NativeInstallShot.Ramp(age,NativeInstallShot.LookEnd,NativeInstallShot.LookEnd+.2f));
                    target=Vector3.Lerp(target,CameraPosition,look);
                    if(!Plant.Short&&Plant.TurnSeconds>NativeInstallShot.TurnSeconds+.1f)target=Vector3.Lerp(target,OnDisplay(NativeInstallShot.ReadoutX(Aspect),.45f),NativeInstallShot.Ramp(age,NativeInstallShot.ReadAt,NativeInstallShot.ReadAt+.3f));
                }
                target=Vector3.Lerp(target,rest,Plant.Settle);
            }
            float gazeOmega=age>=0&&age<LockSpan?LockGazeOmega:age>=0?PickGazeOmega:GazeOmega;
            if(!gazed){Gaze=lead=target;gazeV=Vector3.Zero;gazed=true;}else{var from=lead;Turn(ref lead,target,new Vector3(0,restHead+EyeUp,0),TurnSpeed*dt);Spring(ref Gaze,ref gazeV,from,lead,gazeOmega,dt);}
            lastAge=age;
            // The bow: spine and chest take their share of the look first (the arms ride on the chest), then the install's torso
            // layers, then the arms, then the neck and head finish the look, so the catch and the bay land where they are solved.
            // (Through the install the look down is held to InstallPitch, so the visor and the face stay toward the lens over the bay.)
            float down=siren?SirenDown:LookDown,pitchDown=age>=0&&!siren?PitchDown+(InstallPitch-PitchDown)*NativeInstallShot.Ramp(age,0,.3f)*(1-Plant.Settle):PitchDown;
            var eye=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,p.WorldRotation[CapturedRig.Head]);var aim=Aim(eye,Gaze,down,pitchDown);var along=Vector3.Normalize(aim-eye);
            if(!siren){Turn(p,CapturedRig.Spine,along,SpineShare);Turn(p,CapturedRig.Chest,along,ChestShare);}p.World();
            if(planting)Plant.Torso(p,Left,age,reduced);
            // The arms: each rests in the lap (wrist, palm down; the Siren's claws too) unless it considers or catches a card: then it
            // lifts along an arch toward its aim (ConsiderReach of the way to the catch, slid toward the card, while considering; from
            // the pick, cocked back at the lock, on to the catch in front of the chest on an easing arc that overshoots toward the card
            // and comes back, turning to take the cartridge by its edge). The reaching arm takes the larger of its considering weight
            // and the pick's reach; from the catch the install carries it (the receiving arm is raised from its rest from RiseStart).
            var plantLocal=Vector3.Zero;var toCamera=Vector3.UnitZ;
            for(int s=0;s<2;s++)
            {
                bool left=s==0,reaching=age>=0&&left==Left;float reach=reaching?Reach(age)*(1-lower):0,w=reaching?Math.Max(armW[s],reach):armW[s];
                int a=(left?CapturedRig.LClav:CapturedRig.RClav)+1;var chest=p.WorldPosition[CapturedRig.Chest];var chestQ=p.WorldRotation[CapturedRig.Chest];var chestInv=Quaternion.Inverse(chestQ);
                var restHand=RestHand(left);var point=restHand;float turn=0;var wrist=NativeDraftPlant.Roll(chestInv*restQ[s],NativeDraftPlant.ContactWrist,left,0);
                if(armAimed[s]&&(w>1e-4f||reaching))
                {
                    var full=Catch(left,armAim[s],p.WorldPosition[a]);
                    var slide=armAim[s]-restHand;slide.Y=0;slide=slide.LengthSquared()>1e-6f?Vector3.Normalize(slide)*ConsiderSlide:Vector3.Zero;
                    float inner=CrossBeside+(Beside-CrossBeside)*Own(left,armAim[s]);var hover=Vector3.Lerp(restHand,full,ConsiderReach)+slide;hover.X=left?Math.Max(inner,hover.X):Math.Min(-inner,hover.X);var aimAt=reaching?Vector3.Lerp(hover,full,reach):hover;
                    // The cartridge's turn at the catch (its back face to the camera, its crest up) and the hand on it; the catch's
                    // pinch where that hand's wrist lands on the catch.
                    var catchHand=Quaternion.Identity;
                    if(reaching)
                    {
                        toCamera=CapturedRig.Unit(CameraPosition-full,Vector3.UnitZ);var up=CapturedRig.Unit(Vector3.UnitY-toCamera*Vector3.Dot(Vector3.UnitY,toCamera),Vector3.UnitY);
                        catchHand=siren?chestQ*NativeDraftPlant.ContactWrist:NativeDraftInstall.CatchTurn(toCamera,left)*NativeDraftInstall.HandOnCartridge;
                        var pinch=full+Vector3.Transform(siren?NativeDraftPlant.PalmLocal:NativeDraftInstall.PinchLocal,catchHand);plantLocal=Vector3.Transform(pinch-chest,chestInv);CatchPalm=pinch;caught=true;
                        // The overshoot toward the card and the cock at the lock (back toward the body).
                        aimAt+=CapturedRig.Unit(full-restHand,Vector3.UnitY)*((siren?SirenLunge:Overshoot)*Over(age)*(Plant.Toon?1.3f:1));
                        if(!siren)aimAt+=Vector3.Transform(new Vector3(0,0,-Cock),chestQ)*Cocked(age);
                    }
                    // (Past the catch the install carries the hand on from here.)
                    {
                        var control=(restHand+aimAt)*.5f+new Vector3(0,Arc,0);point=restHand*((1-w)*(1-w))+control*(2*w*(1-w))+aimAt*(w*w);
                        turn=reaching?Math.Max(ConsiderTurn*armW[s],reach):ConsiderTurn*w;
                        wrist=NativeDraftPlant.Roll(chestInv*restQ[s],NativeDraftPlant.ContactWrist,left,reaching?ConsiderTurn*armW[s]:turn);
                        if(reaching){wrist=Quaternion.Slerp(wrist,chestInv*catchHand,reach);if(!siren)wrist=wrist*CapturedRig.Rot(Vector3.UnitX,-CockTurn*Cocked(age));}
                    }
                }
                var pole=Vector3.Lerp(RestPole,ReachPole,w);if(!left)pole.X=-pole.X;
                CapturedRig.Arm(p,left,Vector3.Transform(point-chest,chestInv),wrist,pole);
            }
            p.World();
            // The burst: the first frame past it, where the 2D card hands over (its centre on its flight at Burst, on the camera ray at
            // the catch's depth) and the cartridge's width there.
            if(age>=NativeInstallShot.Burst&&!Bursting&&focus>=0&&caught)
            {
                var c=CatchPalm;var sc=NativeInstallShot.CardAt(cards[focus],Project(c),NativeInstallShot.Burst);var at=OnPlane(sc.X,sc.Y,c.Z);
                BurstScreen=sc;Basis(out var f,out _,out _,out float t);float depth=Math.Max(.05f,Vector3.Dot(at-CameraPosition,f));BurstWidth=Plant.Width/(2*depth*t*Aspect);Bursting=true;
                var up=CapturedRig.Unit(Vector3.UnitY-toCamera*Vector3.Dot(Vector3.UnitY,toCamera),Vector3.UnitY);Plant.Hand(at,CapturedRig.Basis(Vector3.Cross(toCamera,up),toCamera,up));
            }
            if(planting){Plant.PlannedChestAt=plannedChestAt;Plant.PlannedChestQ=plannedChestQ;Plant.Pose(p,Left,plantLocal,toCamera,age,reduced);}
            else if(siren&&age>=0){p.World();var grin=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(new Vector3(0,.07f,.22f),p.WorldRotation[CapturedRig.Head]);Plant.SirenPose(p,Left,grin,age,CapturedRig.Unit(CameraPosition-grin,Vector3.UnitZ));}
            // The head looks from where the bow carried the eye, within the same limits, with the install's nods.
            Turn(p,CapturedRig.Neck,along,NeckShare);p.World();eye=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,p.WorldRotation[CapturedRig.Head]);Look(p,Aim(eye,Gaze,down,pitchDown));
            if(age>=0&&Plant.HeadPitch!=0)p.Rotation[CapturedRig.Head]=p.Rotation[CapturedRig.Head]*CapturedRig.Euler(Plant.HeadPitch);p.World();
            if(planting)Plant.Place(p,Left,age,toCamera);
            int h=Left?CapturedRig.LHand:CapturedRig.RHand;Palm=p.WorldPosition[h]+Vector3.Transform(siren?NativeDraftPlant.PalmLocal:NativeDraftInstall.PinchLocal,p.WorldRotation[h]);if(!caught)CatchPalm=Palm;
            TrayFade=age>=0?NativeInstallShot.Ramp(Age,NativeInstallShot.TrayGone(cardCount),NativeInstallShot.TrayGone(cardCount)+.2f)*Math.Min(1,ShotWeight*4):0;
        }
        // The look point the chain turns to: the gaze seen from the eye with the downward part followed by `down` (upward by
        // LookUp), the pitch taken over the depth and a quarter of the sideways distance (an outer card still gets a bow, as on
        // a display in front), the turn limited to YawLimit and the pitch to PitchDown / PitchUp (draft-stage.ts lookAt limits,
        // the turn widened).
        private static Vector3 Aim(Vector3 eye,Vector3 gaze,float down,float pitchDown=PitchDown)
        {
            var d=gaze-eye;d.Y*=d.Y>0?LookUp:down;float flat=(float)Math.Sqrt(SideReach*d.X*d.X+d.Z*d.Z),distance=Math.Max(.3f,d.Length());
            float yaw=MathUtil.Clamp((float)Math.Atan2(d.X,Math.Max(.05f,d.Z)),-YawLimit,YawLimit),pitch=MathUtil.Clamp((float)Math.Atan2(d.Y,flat),-pitchDown,PitchUp);
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
