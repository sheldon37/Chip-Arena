using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The chip draft's 3D stage (owner request 2026-10-10: "from the pilot's chest and up, kind of like they're sitting at a
    // table with a display of chips in front of them"; presentation only). The pilot sits behind a display table that spans
    // the frame at about lower-chest height, leaning a little toward it with the forearms on the table and the hands near its
    // edge, and the camera shows them from the chest up, from a seat a little above across the table. The designed 2D tray is
    // the display on the table: the framing puts the resting hands just above the first card row and the table's lit front rim
    // just under the last, so the cards lie on its display surface (NativeDraftScreen keeps their art, sizes and motion). The
    // pilot looks at whichever card is highlighted (the local picker's hover or keyboard focus, else the AI / remote picker's
    // scan); before the pick the hand on that card's side lifts off the table toward it (draft-stage.ts consider), the pick
    // reaches across the table to the chosen card and the wrist plant follows (NativeDraftPlant). Every move eases: the gaze and
    // the arms are critically damped springs, the reach, grab and wrist plant run on the slowed timeline below with smoothstep
    // ramps, the framing and the table ease after layout changes, the pick's age runs on a smooth clock (the authoritative one
    // steps with ticks and network frames) and the idle breathes at IdlePace. Positions are the stage rig's root frame in source
    // axes (+x the pilot's left = screen right, +z toward the camera; Unity world is (-x, y + .88, z)). Allocation-free per
    // frame. Rules, timers, picks and messages are not touched.
    public sealed class NativeDraftStage
    {
        // Camera: the source draft stage's 24 degree lens, tilted Tilt degrees down at the pilot (a seat across the table).
        public const float Fov=24,Tilt=12;
        // The table: its top at TableY (root frame, the pilot's lower chest), the edge nearest the pilot at TableBack (in front of
        // the chest), the slab TableThick thick on a front face FaceDepth tall. The front edge follows the tray's last row and the
        // width the camera (both eased with the framing), at least MinDepth deep. The Siren's idle stands her SirenSink taller,
        // so she sits that much lower (her chest and head where the pilot's are).
        public const float TableY=-.065f,SirenSink=.2f,TableBack=.19f,TableThick=.045f,FaceDepth=1.4f,MinDepth=.35f;
        // Resting hands (root frame, on the table): wrists RestX either side and RestZ in front, RestUp above the top, palms down
        // with the fingers turned RestTurn rad in; the elbows out and back (RestPole, chest frame). The seated lean (spine, rad).
        public const float RestX=.17f,RestZ=.42f,RestUp=.13f,RestTurn=.35f,Lean=.08f;
        public static readonly Vector3 RestPole=new Vector3(1,0,-.4f),ReachPole=new Vector3(1,-.8f,-.25f);
        // Framing: the head's top (HeadTop above the head joint, HatTop more under a hat, BossTop for the Siren) at Headroom below
        // the screen's top, and the table top under the resting fingertips (AnchorZ) HandGap above the tray's first row, so the
        // hands and forearms stay in view above the cards; the visible pilot (head top to table) at most MaxSeat of the screen's
        // height (never closer) and at least MinSeat (never smaller: a tall tray then covers the table's edge and the hands,
        // the head keeping its room), at least MinWidth metres across, at most MaxHeight tall. The front rim sits FrontGap
        // under the last row.
        public const float Headroom=.05f,HeadTop=.36f,HatTop=.24f,BossTop=.2f,AnchorZ=.5f,HandGap=.012f,MaxSeat=.74f,MinSeat=.44f,MinWidth=1.8f,MaxHeight=4f,FrontGap=.03f,DefaultTray=.72f,DefaultTrayBottom=.93f;
        // Springs (rad/s, critically damped): the gaze settles to 95% in about .37 s (.47 s while picking), framing in .8 s.
        // The gaze's target itself turns at most TurnSpeed rad/s (seen from the eye), so a glance across the whole display takes
        // its time instead of whipping the head through the middle.
        public const float GazeOmega=13,PickGazeOmega=10,FrameOmega=6,TurnSpeed=6;
        // The idle breathes at .75 of the source draft pose's pace (a 5 s breath).
        public const float IdlePace=.75f;
        // The bow: the share of the turn toward the gaze direction taken by the spine, chest and neck in turn (the head takes
        // the rest and looks straight at the point), how much of a downward look the chain follows (LookDown) and of an upward
        // one, and the limits (the turn wider than the source's 1.05 rad, so the outer card of a wide or two-row display still
        // reads as the one looked at). Seated at the table the spine and chest bow less than standing (the table is near); the
        // Siren, already hunched in her idle, keeps her chest and follows half a downward look (SirenDown), so her face reads.
        public const float SpineShare=.14f,ChestShare=.24f,NeckShare=.4f,LookDown=1,SirenDown=.5f,LookUp=.55f,YawLimit=1.3f,PitchDown=.8f,PitchUp=.4f,EyeUp=.165f;
        // A card as seen on the display: the gaze meets its camera ray CardLift above the table top (kept between the back edge
        // plus NearLook and the front edge; nearer the camera for the outer cards of a wide tray, so their turn from an eye at
        // EyeZ stays inside OuterTurn = tan 1.1 rad), the reaching hand aims ReachLift above it.
        public const float CardLift=.02f,ReachLift=.1f,NearLook=.45f,EyeZ=.3f,OuterTurn=1.96f;
        // Reach: shoulder to wrist at most ArmReach toward the card; considering, the hand lifts ConsiderUp off the table and slides
        // ConsiderSlide across it toward the card, turning ConsiderTurn of the way to the open palm (draft-stage.ts CONSIDER, a
        // soft elbow); the wrist never lower than HandClear above the table, the hand kept Beside its own side of the body, the
        // path arching Arc above the straight line from the table.
        public const float ArmReach=.5f,ConsiderSlide=.16f,ConsiderUp=.07f,ConsiderTurn=.3f,HandClear=.1f,Beside=.16f,Arc=.08f;
        // Considering (draft-stage.ts): the arm on the local picker's highlighted card's side lifts toward it (weight spring
        // LiftOmega) while the other lowers a little quicker (LowerOmega), so a switch of sides never holds both arms up; each
        // arm's aim point follows the card (AimOmega). The arm that was lifting keeps lifting through the pick, and the pick's
        // reach takes the larger of the two.
        public const float LiftOmega=7,LowerOmega=9,AimOmega=9;
        // The share of the squared sideways distance that flattens a look's pitch (Aim).
        public const float SideReach=.25f;
        // The pick's timeline (seconds after the authoritative pick, with NativeDraftPlant's slowed plant): the hand starts toward
        // the card at ReachStart and closes on it at NativeDraftPlant.Contact; the card starts flying into the palm at FlyStart and
        // fades in the hand over the FadeSpan before the contact; the gaze passes from the card to the hand over the HandOver
        // before it.
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
        // The resting wrist (root frame) on the table and its turn: palm down, fingers forward and turned in.
        public static Vector3 RestHand(bool left)=>new Vector3(left?RestX:-RestX,TableY+RestUp,RestZ);
        public static Quaternion RestWrist(bool left)
        {
            var fingers=Vector3.Normalize(new Vector3(left?-RestTurn:RestTurn,-.12f,1));var down=-Vector3.UnitY;down=Vector3.Normalize(down-fingers*Vector3.Dot(down,fingers));var y=-fingers;
            return CapturedRig.Basis(Vector3.Cross(y,down),y,down);
        }

        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly NativeDraftPlant Plant=new NativeDraftPlant();
        // Outputs: the camera (root frame), the smoothed gaze point, the reaching side, the palm of that hand, the presentation
        // clock (seconds), the smoothed pick age (-1 before the pick); the table (its top, front edge and half width).
        public Vector3 CameraPosition,CameraTarget,Gaze,Palm;public float Aspect=16f/9,Clock,Age=-1,TableFront=1,TableHalfWidth=3;public bool Left;public float Table=>TableY;
        private readonly float restHead;private readonly Quaternion[] restQ=new Quaternion[2];private float lookNear=TableBack+NearLook;
        private float frameHeight,frameCentre,frameHeightV,frameCentreV,frontV,widthV,lastAge=-1;private Vector3 gazeV,lead;private bool framed,gazed,picked,carry;
        // Per arm (0 the left, 1 the right): the considering weight, its velocity and last target, and the aim point's spring.
        private readonly float[] armW=new float[2],armV=new float[2],armGoal=new float[2];private readonly Vector3[] armAim=new Vector3[2],armAimV=new Vector3[2];private readonly bool[] armAimed=new bool[2];
        public float Weight(bool left)=>armW[left?0:1];
        public NativeDraftStage(){NativeDraftRules.Pose(Pose,0);Pose.World();restHead=Pose.WorldPosition[CapturedRig.Head].Y;restQ[0]=RestWrist(true);restQ[1]=RestWrist(false);}
        // A new turn or pilot: the springs restart at their targets on the next frame and the arms start at rest on the table.
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

        // Framing: `aspect` the screen's, `trayTop` / `trayBottom` the first card row's top edge and the last row's bottom edge
        // (fractions of the screen height from the top; negative while the tray is not laid out yet: defaults, and the framing
        // snaps again on the first real one). Snaps on the first frame after a Reset, then eases, so a resize or a re-laid tray
        // never cuts. The camera is solved so the head's top and the resting hands land on their rows.
        public void Frame(float aspect,float trayTop,float trayBottom,bool hat,bool boss,float dt)
        {
            bool known=trayTop>=0&&MathUtil.Finite(trayTop)&&MathUtil.Finite(trayBottom)&&trayBottom>trayTop;if(!known){trayTop=DefaultTray;trayBottom=DefaultTrayBottom;framed=false;}
            Aspect=Math.Max(.2f,aspect);float top=restHead+HeadTop+(hat?HatTop:0)+(boss?BossTop:0),hy=Table;
            float handRow=MathUtil.Clamp(trayTop,.3f,.95f)-HandGap,headRow=Headroom;
            // The camera's height and centre (on the plane z = 0) that put the head's top on its row and the hands on theirs.
            float height=(top-hy)/Math.Max(.05f,handRow-headRow),centre=top-(.5f-headRow)*height;
            for(int i=0;i<6;i++)
            {
                const float e=1e-3f;float r1=Row(top,0,centre,height)-headRow,r2=Row(hy,AnchorZ,centre,height)-handRow;
                float a11=(Row(top,0,centre+e,height)-headRow-r1)/e,a12=(Row(top,0,centre,height+e)-headRow-r1)/e,a21=(Row(hy,AnchorZ,centre+e,height)-handRow-r2)/e,a22=(Row(hy,AnchorZ,centre,height+e)-handRow-r2)/e;
                float det=a11*a22-a12*a21;if(Math.Abs(det)<1e-7f)break;centre-=(r1*a22-r2*a12)/det;height=Math.Max(.3f,height-(a11*r2-a21*r1)/det);
            }
            // Never closer than MaxSeat of the screen nor narrower than MinWidth (then the hands keep their row and the head gains
            // room), never smaller than MinSeat nor taller than MaxHeight (then the head keeps its room and the cards cover the
            // table's edge and the hands).
            float least=Math.Max((top-hy)/MaxSeat,MinWidth/Aspect),most=Math.Max(least,Math.Min(MaxHeight,(top-hy)/MinSeat));
            if(height<least){height=least;centre=Solve(hy,AnchorZ,handRow,centre,height);}
            else if(height>most){height=most;centre=Solve(top,0,headRow,centre,height);}
            if(!framed){frameHeight=height;frameCentre=centre;frameHeightV=frameCentreV=0;}
            else{float step=Math.Max(0,Math.Min(.1f,dt));Spring(ref frameHeight,ref frameHeightV,height,FrameOmega,step);Spring(ref frameCentre,ref frameCentreV,centre,FrameOmega,step);}
            float distance=frameHeight*.5f/(float)Math.Tan(Fov*Math.PI/360),tilt=Tilt*(float)Math.PI/180;
            CameraTarget=new Vector3(0,frameCentre,0);CameraPosition=CameraTarget+new Vector3(0,(float)Math.Sin(tilt),(float)Math.Cos(tilt))*distance;
            // The table's front edge under the last row and its width past the screen's sides at the back edge (the widest part
            // in view), eased with the framing.
            float front=MathUtil.Clamp(TableZ(MathUtil.Clamp(trayBottom+FrontGap,.4f,1.2f)),TableBack+MinDepth,CameraPosition.Z-.5f);
            Basis(out var f,out _,out _,out float t);float width=Vector3.Dot(new Vector3(0,Table,TableBack)-CameraPosition,f)*t*Aspect*1.2f+.3f;
            if(!framed){TableFront=front;TableHalfWidth=width;frontV=widthV=0;framed=known;}
            else{float step=Math.Max(0,Math.Min(.1f,dt));Spring(ref TableFront,ref frontV,front,FrameOmega,step);Spring(ref TableHalfWidth,ref widthV,width,FrameOmega,step);}
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
        // Where the camera ray through the screen's centre column at `row` meets the table top (z; far if it never does).
        private float TableZ(float row)
        {
            Basis(out var f,out _,out var u,out float t);var d=f+u*((1-2*row)*t);if(d.Y>-1e-4f)return CameraPosition.Z;
            return CameraPosition.Z+d.Z*((Table-CameraPosition.Y)/d.Y);
        }
        // The camera ray through a screen point (fractions: x from the left, y from the top) where it meets the plane z.
        public Vector3 OnPlane(float sx,float sy,float z)
        {
            Basis(out var f,out var r,out var u,out float t);var d=f+r*((2*sx-1)*t*Aspect)+u*((1-2*sy)*t);
            return CameraPosition+d*((z-CameraPosition.Z)/Math.Min(-1e-4f,d.Z));
        }
        // A screen point on the display: where its camera ray meets the table top raised by `lift`, kept between the near look
        // depth (Step) and the front edge (beyond them, the point on the ray at that depth).
        public Vector3 OnTable(float sx,float sy,float lift)
        {
            Basis(out var f,out var r,out var u,out float t);var d=f+r*((2*sx-1)*t*Aspect)+u*((1-2*sy)*t);float near=lookNear,far=Math.Max(near,TableFront);
            if(d.Y>-1e-4f)return OnPlane(sx,sy,near);float z=CameraPosition.Z+d.Z*((Table+lift-CameraPosition.Y)/d.Y);
            return z>=near&&z<=far?CameraPosition+d*((Table+lift-CameraPosition.Y)/d.Y):OnPlane(sx,sy,MathUtil.Clamp(z,near,far));
        }
        // A root-frame point as screen fractions (x from the left, y from the top).
        public Vector2 Project(Vector3 p)
        {
            Basis(out var f,out var r,out var u,out float t);var d=p-CameraPosition;float z=Math.Max(1e-4f,Vector3.Dot(d,f));
            return new Vector2((Vector3.Dot(d,r)/(z*t*Aspect)+1)*.5f,(1-Vector3.Dot(d,u)/(z*t))*.5f);
        }
        private void Basis(out Vector3 f,out Vector3 r,out Vector3 u,out float t)
        {f=Vector3.Normalize(CameraTarget-CameraPosition);r=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));u=Vector3.Cross(r,f);t=(float)Math.Tan(Fov*Math.PI/360);}

        // The table as boxes (root frame, axis aligned), for the presentation and the checks: `shade` is a lit piece's linear
        // grey or a glowing piece's share of the stage tint. 0 the slab, 1 the front face, 2 the display (a dim glowing pane
        // inset in the top), 3 the front rim, 4 the back rim, 5 a rim along the face under the front edge, 6-7 faint display rules.
        public const int TablePieces=8;
        public bool TablePiece(int i,out Vector3 centre,out Vector3 size,out float shade,out bool glow)
        {
            float y=Table,back=TableBack,front=Math.Max(back+MinDepth,TableFront),w=TableHalfWidth*2,depth=front-back;glow=i>=2;
            switch(i)
            {
                case 0:centre=new Vector3(0,y-TableThick*.5f,(back+front)*.5f);size=new Vector3(w,TableThick,depth);shade=.022f;return true;
                case 1:centre=new Vector3(0,y-TableThick-FaceDepth*.5f,front-.04f);size=new Vector3(w,FaceDepth,.08f);shade=.012f;return true;
                case 2:centre=new Vector3(0,y+.0015f,(back+.07f+front-.05f)*.5f);size=new Vector3(w,.002f,Math.Max(.05f,depth-.12f));shade=.05f;return true;
                case 3:centre=new Vector3(0,y+.004f,front-.012f);size=new Vector3(w,.008f,.024f);shade=.95f;return true;
                case 4:centre=new Vector3(0,y+.003f,back+.012f);size=new Vector3(w,.006f,.016f);shade=.38f;return true;
                case 5:centre=new Vector3(0,y-TableThick-.03f,front+.001f);size=new Vector3(w,.006f,.004f);shade=.45f;return true;
                default:float k=i==6?.36f:.68f;centre=new Vector3(0,y+.0025f,back+.07f+(depth-.12f)*k);size=new Vector3(w,.002f,.006f);shade=.16f;return depth>.5f;
            }
        }

        // One frame over the idle in Pose (after Begin and Frame). `dt` presentation seconds, `age` the smoothed pick age (-1
        // before the pick), `focus` the considered card (Focus), `consider` whether that card is the local picker's highlighted
        // one (the arm on its side lifts toward it; never for the scan), `cards` the card centres as screen fractions (first
        // `count`). The Siren drafter rests her claws on the table and holds the chip (no lift, reach or plant). World() is current on return.
        public void Step(float dt,float age,int focus,bool consider,Vector2[] cards,int count,bool siren,bool reduced)
        {
            dt=Math.Max(0,Math.Min(.1f,dt));var p=Pose;count=cards==null?0:Math.Min(count,cards.Length);focus=count>0?Math.Max(0,Math.Min(count-1,focus)):-1;
            // Seated: leaning a little toward the table (the Siren, hunched in her idle, sits lower instead).
            if(siren)p.Position[CapturedRig.Body].Y-=SirenSink;else p.Rotation[CapturedRig.Spine]=p.Rotation[CapturedRig.Spine]*CapturedRig.Euler(Lean);p.World();
            lookNear=TableBack+NearLook;for(int i=0;i<count;i++)lookNear=Math.Max(lookNear,EyeZ+Math.Abs(OnPlane(cards[i].X,cards[i].Y,TableBack+NearLook).X)/OuterTurn);
            var rest=new Vector3(0,Table,TableBack+.35f);
            var card=focus>=0?OnTable(cards[focus].X,cards[focus].Y,CardLift):rest;var reachGoal=focus>=0?OnTable(cards[focus].X,cards[focus].Y,ReachLift):card;
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
            // the hand (last frame's) to the plant, and as the arms settle it eases to a point low in front on the table. Every
            // hand-over blends, so no target jumps.
            var target=card;
            if(age>=0)
            {
                var held=siren||lastAge<contact?Palm:Plant.ChipShown?Plant.Chip:Plant.At;target=Vector3.Lerp(card,held,MathUtil.Smooth((age-(contact-HandOver))/HandOver));
                if(!siren)target=Vector3.Lerp(target,rest,MathUtil.Smooth((age-NativeDraftPlant.ReturnStart)/(NativeDraftPlant.ReturnEnd-NativeDraftPlant.ReturnStart)));
            }
            if(!gazed){Gaze=lead=target;gazeV=Vector3.Zero;gazed=true;}else{var from=lead;Turn(ref lead,target,new Vector3(0,restHead+EyeUp,0),TurnSpeed*dt);Spring(ref Gaze,ref gazeV,from,lead,age>=0?PickGazeOmega:GazeOmega,dt);}
            lastAge=age;
            // The bow: spine and chest take their share of the look first (the arms ride on the chest), then the arms, then the
            // neck and head finish the look, so the reach and the plant land where they are solved.
            float down=siren?SirenDown:LookDown;var eye=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,p.WorldRotation[CapturedRig.Head]);var aim=Aim(eye,Gaze,down);var along=Vector3.Normalize(aim-eye);
            if(!siren){Turn(p,CapturedRig.Spine,along,SpineShare);Turn(p,CapturedRig.Chest,along,ChestShare);}p.World();
            // The arms: each rests on the table (wrist, palm down; the Siren's claws too, which never reach) unless it considers or
            // takes a card: then it lifts off along an arch toward its aim (part of the way, a little raised, while considering; to
            // arm's length with the pick's reach, turning the palm up to receive the card). The reaching arm takes the larger of its
            // considering weight and the pick's reach, and from the contact the plant carries it from where the reach left it,
            // blending back to the rest on the table over the return (the other arm, lowering, is raised for the plant from its rest).
            var plantLocal=Vector3.Zero;
            for(int s=0;s<2;s++)
            {
                bool left=s==0,reaching=planting&&left==Left;float reach=reaching?Reach(age):0,w=reaching?Math.Max(armW[s],reach):armW[s];
                int a=(left?CapturedRig.LClav:CapturedRig.RClav)+1;var chest=p.WorldPosition[CapturedRig.Chest];var chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
                var restHand=RestHand(left);var point=restHand;float turn=0;
                if(armAimed[s]&&(w>1e-4f||reaching))
                {
                    var shoulder=p.WorldPosition[a];var dir=armAim[s]-shoulder;float len=dir.Length();
                    var full=len>1e-5f?shoulder+dir*(Math.Min(len,ArmReach)/len):shoulder;full.X=left?Math.Max(Beside,full.X):Math.Min(-Beside,full.X);full.Y=Math.Max(Table+HandClear,full.Y);
                    var slide=armAim[s]-restHand;slide.Y=0;slide=slide.LengthSquared()>1e-6f?Vector3.Normalize(slide)*ConsiderSlide:Vector3.Zero;
                    var hover=restHand+slide+new Vector3(0,ConsiderUp,0);hover.X=left?Math.Max(Beside,hover.X):Math.Min(-Beside,hover.X);var aimAt=reaching?Vector3.Lerp(hover,full,reach):hover;
                    if(reaching)plantLocal=Vector3.Transform(full-chest,chestInv);
                    if(!reaching||age<contact)
                    {
                        var control=(restHand+aimAt)*.5f+new Vector3(0,Arc,0);point=restHand*((1-w)*(1-w))+control*(2*w*(1-w))+aimAt*(w*w);
                        turn=reaching?Math.Max(ConsiderTurn*armW[s],reach):ConsiderTurn*w;
                    }
                }
                float k=reaching&&age>=contact?0:w;var pole=Vector3.Lerp(RestPole,ReachPole,k);if(!left)pole.X=-pole.X;
                CapturedRig.Arm(p,left,Vector3.Transform(point-chest,chestInv),NativeDraftPlant.Roll(chestInv*restQ[s],NativeDraftPlant.ContactWrist,left,turn),pole);
            }
            if(planting)Plant.Pose(p,Left,plantLocal,age);
            // The head looks from where the bow carried the eye, within the same limits.
            Turn(p,CapturedRig.Neck,along,NeckShare);p.World();eye=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*EyeUp,p.WorldRotation[CapturedRig.Head]);Look(p,Aim(eye,Gaze,down));p.World();
            if(planting)Plant.Place(p,Left,age);
            int h=Left?CapturedRig.LHand:CapturedRig.RHand;Palm=p.WorldPosition[h]+Vector3.Transform(NativeDraftPlant.PalmLocal,p.WorldRotation[h]);
        }
        // The look point the chain turns to: the gaze seen from the eye with the downward part followed by `down` (upward by
        // LookUp), the pitch taken over the depth and a quarter of the sideways distance (an outer card still gets a bow, as on
        // a table in front), the turn limited to YawLimit and the pitch to PitchDown / PitchUp (draft-stage.ts lookAt limits,
        // the turn widened).
        private static Vector3 Aim(Vector3 eye,Vector3 gaze,float down)
        {
            var d=gaze-eye;d.Y*=d.Y>0?LookUp:down;float flat=(float)Math.Sqrt(SideReach*d.X*d.X+d.Z*d.Z),distance=Math.Max(.3f,d.Length());
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
