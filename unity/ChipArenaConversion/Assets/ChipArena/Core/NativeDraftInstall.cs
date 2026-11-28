using System;
using System.Collections.Generic;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The install on the draft stage's rig (NativeInstallShot's timeline; presentation only), in place of the wrist plant the stage
    // ran before (NativeDraftPlant stays for the Expedition's plant). Positions are the stage rig's root frame in source axes.
    // The receiving arm (the right) sinks a centimetre and rises as one checks a watch: the bay's designed frame (PresentAt, its
    // normal and its forearm: InstallPoint in front of the upper chest, the forearm across the body square to the lens and rising a
    // little toward the hand, the bay's face turned to the lens), arriving with a little overshoot; it braces over the line-up, gives
    // under the press (the paired reaction), closes into a fist as the light leaves the device, turns it as the pilot rises into the
    // power, strikes the hero hold (the clench and nod, or the Raise: the fist up beside the head, clear of the visor) and settles
    // back to the lap. On the model the draw solves the drawn arm onto that frame (the device rides the model's forearm, so the
    // stage hand's frame cannot place it: ArenaPresentation.VisualDraftPresent); the stage rig's own arm takes it as nearly as its
    // hand's frame does (the voxel body's palm-side cuff, the bind's start). The hand that took the card (the left) carries the
    // cartridge from the catch over the forearm on an S-curve, from the wrist side with its fingers toward the elbow, squares it a
    // few centimetres over the bay, presses it onto the frame, lifts off along the bay's normal and back toward its own shoulder
    // (never across the forearm) as the shell bursts, and settles low. The cartridge's frame is planned relative to the bay as posed
    // this frame (a Hermite path from the catch to the hover, the line-up, the press, the latch), so it lands on the bay whatever
    // the arms do; on the model the draw solves the drawn hand onto it (ArenaPresentation.VisualDraftContact). The torso and the
    // head take small keyed layers (the breath in at the lock, the lean and turn toward the catch, the held breath of the line-up,
    // the drop at the clack, the rise into the power). The Siren drafter devours the cartridge instead (SirenPose). World() is
    // current after each call. Allocation-free per frame.
    public sealed class NativeDraftInstall
    {
        // ---- the cartridge ----
        /// <summary>The held cartridge's width on the model (never bigger than the hand: the draw takes the smaller of this and
        /// HandShare of the drawn glove), the Hot Dog's, the voxel body's (its palm .145 m) and the Siren's (.8 of her palm).</summary>
        public const float HeldWidth=.06f,HandShare=.66f,HotDogWidth=.075f,VoxelWidth=.10f,SirenWidth=.116f;
        /// <summary>Model units across the cartridge (NativeMachineModels.PickupBodyWidth): a width's scale is Width / ModelWidth.</summary>
        public const float ModelWidth=NativeMachineModels.PickupBodyWidth;
        // The core's centre in each tier's model (NativeMachineModels.Pickup: z along the crest, the faces' normal y), the crest edge
        // CrestEdge model units past it and the front face's plane FaceDepth under the middle; measured once.
        public const float CrestEdge=.35f,FaceDepth=.045f;
        private static readonly Dictionary<string,float> coreZ=Cores();
        private static Dictionary<string,float> Cores(){var d=new Dictionary<string,float>(StringComparer.Ordinal);foreach(var t in NativeMachineModels.PickupTiers)d[t]=NativeMachineModels.PickupAxis(t).Z;return d;}
        /// <summary>The core's place along the crest axis (model units) in tier `tier`'s cartridge (the common's for a tier without a model).</summary>
        public static float CoreZ(string tier)=>tier!=null&&coreZ.TryGetValue(tier,out float z)?z:coreZ["common"];
        // ---- the present (chest frame of the seated rest, held in the room) ----
        // The bay's seat just to the receiving side of the midline in front of the upper chest, high enough that the shot holds the visor
        // and the bay together close up (the bay about a quarter metre under the visor) and in front of the bowed helmet, so it never
        // hides the device from the lens. Its forearm runs across the body square to the lens (no part of it toward the camera, so the
        // bay's face can turn to the lens), rising a little toward the hand as one checks a watch; the bay's normal points at the lens
        // and a little up (about 25 degrees above level: within 20 degrees of the camera's line to it).
        public static readonly Vector3 InstallPoint=new Vector3(-.04f,.24f,.30f);
        public static readonly Vector3 NormalWant=Vector3.Normalize(new Vector3(0,.42f,.91f)),ForearmWant=Vector3.Normalize(new Vector3(1,.22f,0));
        /// <summary>The pilot's visor's height in the planned chest's frame (NativeDraftStage: the stage rig's eyes until the drawn body's is
        /// measured). The install's heights follow it: the bay at most BayUnderVisor under it (so the shot holds the visor high and the device
        /// low together; the player's model wears its visor low, about 35 cm over the chest), the Raise's at most RaiseOverVisor over it (the
        /// ear line), so the fist stays under the crown.</summary>
        public float VisorChestY=.57f;
        public const float BayUnderVisor=.21f,RaiseOverVisor=-.02f;
        public Vector3 InstallAt=>new Vector3(InstallPoint.X,Math.Min(InstallPoint.Y,VisorChestY-BayUnderVisor),InstallPoint.Z);
        public Vector3 RaiseAt=>new Vector3(RaisePoint.X,Math.Min(RaisePoint.Y,VisorChestY+RaiseOverVisor),RaisePoint.Z);
        // The Raise (the hero hold of the rare and up): the fist up beside the head on the receiving side, the bay at the ear line a
        // helmet's half width and more off the visor, its forearm leaning about 45 degrees outward (never upright across the face), the
        // fist under the crown; the bay's face still to the lens.
        public static readonly Vector3 RaisePoint=new Vector3(-.31f,.50f,.16f);
        public static readonly Vector3 RaiseNormal=Vector3.Normalize(new Vector3(.12f,.12f,1)),RaiseForearm=Vector3.Normalize(new Vector3(-.68f,.72f,.08f));
        public static readonly Vector3 PresentPole=new Vector3(-1,-.9f,-.1f),RaisePole=new Vector3(-.6f,-1,-.4f);
        /// <summary>The Raise's order (shares of it): out to the side by RaiseOut, up from RaiseUp, the forearm turning up from RaiseTurn.</summary>
        public const float RaiseOut=.55f,RaiseUp=.25f,RaiseTurn=.3f;
        /// <summary>The cartridge over the bay: hovering Hover above it (m, along the normal), Align after the line-up; the shell rests on
        /// the frame (NativePilotGear.FrameHeight) and the core sinks LatchDepth into the seat.</summary>
        public const float Hover=.045f,Align=.03f,LatchDepth=.0065f,Tremble=.0015f,TrembleHz=18,HoverTwist=6*(float)Math.PI/180;
        /// <summary>The give under the press (m along the bay's inward normal, at level 1; boss relics BossGive) and its wrist turn.</summary>
        public const float Give=.01f,GiveLevel=.005f,BossGive=.02f,GiveTurn=6*(float)Math.PI/180;
        /// <summary>The fist's turn about its forearm as the pilot rises into the power (rad), testing it; the bay stays to the lens.</summary>
        public const float PowerTurn=.21f;
        /// <summary>How far the draw may slide the model's present along the lens's line (m) so its upper arm keeps its length.</summary>
        public const float PresentSlide=.12f;
        /// <summary>The hand that takes the card: where its pinch sits in the stage hand's frame (source axes: -y down the fingers, +z
        /// the palm) and the hand's turn on the cartridge (its fingers along the cartridge toward the connector, its palm on the back
        /// face: hand y the crest, hand z the front face's normal).</summary>
        public static readonly Vector3 PinchLocal=new Vector3(0,-.085f,.025f);
        public static readonly Quaternion HandOnCartridge=CapturedRig.Basis(Vector3.UnitX,Vector3.UnitZ,-Vector3.UnitY);
        /// <summary>The lift off the shell at the release (m): up along the bay's normal and back toward the taking hand's own shoulder (in the
        /// bay's plane), clear of the lid and never across the receiving forearm; the low rest after it (chest frame).</summary>
        public const float FlickUp=.025f,FlickOver=.03f;
        public static readonly Vector3 LowRest=new Vector3(.13f,-.06f,.30f);

        // ---- inputs (the runtime's, each frame) ----
        /// <summary>The pilot is drawn on the model (the right forearm's device): BayLocal / BayNormal the drawn bay's seat and outward
        /// normal in the receiving hand's stage frame (the draw measures them; a frame behind); else the palm-side cuff.</summary>
        public bool Device;public Vector3 BayLocal=NativeDraftPlant.DefaultBay,BayNormal=-Vector3.UnitZ;
        // The bay the stage's own arm plans its hand on: BayLocal / BayNormal as they were at the pick, held through the install (a turn
        // planned on each frame's measurement made the measurement move with it).
        private Vector3 presentLocal=NativeDraftPlant.DefaultBay,presentNormal=-Vector3.UnitZ;private bool presentHeld;
        private Vector3 PresentBay=>presentHeld?presentLocal:BayLocal;private Vector3 PresentBayNormal=>presentHeld?presentNormal:BayNormal;
        /// <summary>The cartridge's tier and width (m), the profile, the hero hold, the turn's hold after the pick (s), whether the pick
        /// is a short follow-through (the PvE pick, the Deathmatch's: it ends on the power pose) and the Hot Dog's cartoon timing.</summary>
        public string Tier="common";public float Width=HeldWidth,TurnSeconds=NativeInstallShot.TurnSeconds;public int Hero;public bool Short,Toon;public NativeInstallShot.Profile Profile=NativeInstallShot.Of("common",null);
        /// <summary>The planned chest (the seated rest's, root frame): the install point and its normal are held there in the room, so the
        /// bay stays where the shot frames it while the chest bows over it and breathes.</summary>
        public Vector3 PlannedChestAt;public Quaternion PlannedChestQ=Quaternion.Identity;

        // ---- outputs (root frame) ----
        /// <summary>The bay's seat, its outward normal and the forearm (elbow to hand, square to the normal) as posed (on the model, the
        /// designed present as far as the arm has risen into it).</summary>
        public Vector3 At,Normal=Vector3.UnitZ,Forearm=Vector3.UnitX;
        /// <summary>The cartridge: its centre and its model's frame (x across, y the back face's normal, z the crest), whether it shows,
        /// the core's sink into the seat (0..1, from the contact) and whether the core is seated (from the release, the shell gone).</summary>
        public Vector3 Chip;public Quaternion ChipRotation=Quaternion.Identity;public bool ChipShown,CoreSeated;public float Latch,Lights=1;
        /// <summary>The pinch on the cartridge's crest edge (where the fingers hold it), the press point (its back over the core) and how
        /// much of the cartridge's frame is planned on the bay (0 at the catch, 1 from the carry's end).</summary>
        public Vector3 Pinch,PressAt;public float BayShare;
        /// <summary>The taking hand's own pinch (the stage hand's, on its planned path) and the cartridge's turn at the catch (the hand
        /// readies the pinch on it as the card flies in): the drawn hand reaches for these before the catch.</summary>
        public Vector3 HandPinch;public Quaternion CatchRotation=Quaternion.Identity;
        /// <summary>Where the cartridge appeared (Burst: on the camera ray through the card's centre at the catch's depth) and its turn.</summary>
        public Vector3 BurstAt;public Quaternion BurstTurn=Quaternion.Identity;public bool Burst;
        /// <summary>The planned present (root frame): the bay's seat and normal at the install point (the camera's focus reads them).</summary>
        public Vector3 PlannedAt,PlannedNormal;
        /// <summary>The settle's progress (0..1; the short follow-through never settles) and the head's added pitch (rad, + down).</summary>
        public float Settle,HeadPitch;
        /// <summary>The designed present (root frame): the bay's seat, its outward normal and its forearm (elbow to hand) this frame, the
        /// give, the power-up's turn and the hero hold in them; ForeWeight how far the arm has risen into it (0 the rest .. 1 presented),
        /// ForeAiming whether it presents this frame. The draw solves the model's arm onto it by that weight; the Raise's share (0..1).</summary>
        public Vector3 PresentAt,PresentNormal=Vector3.UnitZ,PresentForearm=Vector3.UnitX;public float ForeWeight,RaiseShare;public bool ForeAiming;
        private Vector3 catchAt,catchFlight;private Quaternion catchTurn=Quaternion.Identity;private bool caught;
        private readonly Quaternion[] rest=new Quaternion[3],solved=new Quaternion[3];

        /// <summary>A new pick: the snapshot of the catch and the burst is cleared.</summary>
        public void Reset(){caught=Burst=false;ChipShown=CoreSeated=false;Latch=0;Settle=0;HeadPitch=0;presentHeld=false;ForeWeight=0;RaiseShare=0;ForeAiming=false;}
        /// <summary>The scale of the cartridge model at its width.</summary>
        public float Scale=>Width/ModelWidth;
        private float CoreAlong=>CoreZ(Tier)*Scale;
        private float Stack=>NativePilotGear.FrameHeight+FaceDepth*Scale;
        /// <summary>The cartridge's frame (centre, turn) whose core sits `lift` over the bay seat `at` with normal `n` and forearm `f`.</summary>
        public void OnBay(Vector3 at,Vector3 n,Vector3 f,float lift,out Vector3 centre,out Quaternion turn)
        {
            turn=CapturedRig.Basis(Vector3.Cross(n,f),n,f);centre=at+n*(Stack+lift)-f*CoreAlong;
        }
        /// <summary>The pinch on a cartridge at `centre` turned `turn` (its crest edge, on the middle plane).</summary>
        public Vector3 PinchOn(Vector3 centre,Quaternion turn)=>centre+Vector3.Transform(Vector3.UnitZ,turn)*((CoreZ(Tier)+CrestEdge)*Scale);
        /// <summary>The press point on a cartridge: its back face over the core.</summary>
        public Vector3 PressOn(Vector3 centre,Quaternion turn)=>centre+Vector3.Transform(Vector3.UnitZ,turn)*CoreAlong+Vector3.Transform(Vector3.UnitY,turn)*(FaceDepth*Scale);
        /// <summary>The lift off the shell at pose age `p` (0..1 over the release), and the direction it lifts along: up the bay's normal `n`
        /// and back from the bay `at` toward the taking hand's shoulder `shoulder` (in the bay's plane, away from the receiving forearm `f`).</summary>
        public static float Lift(float p)=>NativeInstallShot.EaseOut((p-NativeInstallShot.Release)/(NativeInstallShot.ReleaseEnd-NativeInstallShot.Release));
        public static Vector3 LiftAway(Vector3 at,Vector3 n,Vector3 f,Vector3 shoulder)
        {
            var back=shoulder-at;back-=n*Vector3.Dot(back,n);var away=CapturedRig.Unit(back,Vector3.UnitY);
            // (Never along the forearm toward the elbow: the receiving arm lies that way.)
            float along=Vector3.Dot(away,f);if(along<0)away=CapturedRig.Unit(away-f*along,away);
            return away;
        }

        // ---- the torso and head layers ----
        /// <summary>The layers on the spine, chest, clavicles and the body's height at pose age `p` (after the stage's lean and bow),
        /// `left` the side that took the card. HeadPitch gets the head's (applied after the look).</summary>
        public void Torso(CapturedRig.Pose pose,bool left,float p,bool reduced)
        {
            HeadPitch=0;if(!(p>=0))return;float k=1-Settle,deg=(float)Math.PI/180,side=left?1:-1;
            // The breath in at the lock; the lean and the turn toward the catch, then toward the device over the carry.
            float breath=NativeInstallShot.Ramp(p,0,.10f)*(1-NativeInstallShot.Ramp(p,.30f,.60f));
            float reach=NativeInstallShot.Ramp(p,NativeInstallShot.FlyStart,NativeInstallShot.Catch),carry=NativeInstallShot.Ramp(p,NativeInstallShot.Catch,NativeInstallShot.AlignEnd);
            float lean=3*deg*reach*(1-.6f*NativeInstallShot.Ramp(p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd));
            float yaw=side*(6*deg*reach*(1-carry)-2*deg*carry*(1-NativeInstallShot.Ramp(p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd)));
            // The give at the catch and the drop at the clack; the rise into the power (an inhale, the clavicles up).
            float give=-.005f*NativeInstallShot.Kick(p-NativeInstallShot.Catch,22,.45f)-.005f*NativeInstallShot.Kick(p-NativeInstallShot.Seat,30,.4f);
            float power=NativeInstallShot.Ramp(p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd)*(Short?1:1-NativeInstallShot.Ramp(p,NativeInstallShot.HoldEnd,NativeInstallShot.HoldEnd+.4f)*.4f);
            float extend=(-2*deg*breath-3*deg*power)*k,chestUp=(.01f*breath+.008f*power+give)*k;
            pose.Rotation[CapturedRig.Spine]=pose.Rotation[CapturedRig.Spine]*CapturedRig.Euler((lean*k+extend),yaw*.4f*k);
            pose.Rotation[CapturedRig.Chest]=pose.Rotation[CapturedRig.Chest]*CapturedRig.Euler(-2*deg*power*k,yaw*.6f*k);
            pose.Position[CapturedRig.Body].Y+=chestUp;
            // The clavicles: up with the breath and the power, the reaching one forward with the reach, the receiving one up as it presents
            // and dipping under the press.
            float lift=(.023f*breath+.043f*power)*k,present=NativeInstallShot.Ramp(p,NativeInstallShot.RiseStart,NativeInstallShot.RiseEnd)*(1-Settle);
            float dip=-.017f*NativeInstallShot.Kick(p-NativeInstallShot.Seat,30,.4f);
            int lc=left?CapturedRig.LClav:CapturedRig.RClav,rc=left?CapturedRig.RClav:CapturedRig.LClav;
            pose.Rotation[lc]=pose.Rotation[lc]*CapturedRig.Euler(0,-side*.072f*reach*(1-carry)*k,side*lift);
            pose.Rotation[rc]=pose.Rotation[rc]*CapturedRig.Euler(0,0,-side*(lift+.029f*present+dip*k));
            // The head: a nod up at the lock, a dip at the catch, a nod at the clack, the chin up into the power, the hero's nod.
            float nod=-3*deg*breath+4*deg*NativeInstallShot.Kick(p-NativeInstallShot.Catch,18,.5f)+3*deg*NativeInstallShot.Kick(p-NativeInstallShot.Seat,22,.45f)-4*deg*NativeInstallShot.Kick(p-NativeInstallShot.PowerStart-.2f,14,.5f);
            if(Hero==NativeInstallShot.ClenchNod&&!Short)nod+=4*deg*NativeInstallShot.Kick(p-1.95f,16,.5f);
            HeadPitch=nod*k;pose.World();
        }

        // ---- the arms ----
        /// <summary>
        /// The install's arms at pose age `p` over the pose as the stage left it (the rest in the lap, the reaching arm on its way to
        /// the catch until Catch). `left` the hand that took the card, `catchLocal` its pinch at the catch (chest frame), `toCamera` the
        /// direction from the catch to the camera (root frame). World() is current on return.
        /// </summary>
        public void Pose(CapturedRig.Pose pose,bool left,Vector3 catchLocal,Vector3 toCamera,float p,bool reduced)
        {
            pose.World();if(!(p>=0)){Settle=0;return;}
            if(!presentHeld){presentLocal=BayLocal;presentNormal=BayNormal;presentHeld=true;}
            ForeWeight=0;RaiseShare=0;ForeAiming=false;
            Settle=Short?0:NativeInstallShot.Ramp(p,NativeInstallShot.SettleStart(TurnSeconds),NativeInstallShot.SettleEnd(TurnSeconds));
            var chest=pose.WorldPosition[CapturedRig.Chest];var chestQ=pose.WorldRotation[CapturedRig.Chest];var chestInv=Quaternion.Inverse(chestQ);
            Present(pose,left,p,chest,chestQ,chestInv);
            Place(pose,left,p,toCamera);
            if(p>=NativeInstallShot.Catch)Carry(pose,left,catchLocal,p,reduced);
            pose.World();Place(pose,left,p,toCamera);
        }
        /// <summary>The designed present at pose age `p` (chest frame of the seated rest): the bay's seat, normal and forearm with the Raise's
        /// share `raise`, the power-up's turn, the give under the press (and the boss relics' tremble) and the rise's overshoot along `travel`
        /// (a unit direction, chest frame) by `over` metres.</summary>
        public void PresentFrame(float p,float raise,out Vector3 at,out Vector3 normal,out Vector3 forearm)
        {
            // The Raise in order, so the arm never sweeps across the face: the device goes out to the side first (RaiseOut), then up (RaiseUp),
            // and only then does the forearm turn up beside the head (RaiseTurn); back down in the reverse order.
            float outward=NativeInstallShot.Smooth(raise/RaiseOut),upward=NativeInstallShot.Smooth((raise-RaiseUp)/(1-RaiseUp)),turned=NativeInstallShot.Smooth((raise-RaiseTurn)/(1-RaiseTurn));
            var n=Vector3.Normalize(Vector3.Lerp(NormalWant,RaiseNormal,turned));var f0=Vector3.Lerp(ForearmWant,RaiseForearm,turned);var f=CapturedRig.Unit(f0-n*Vector3.Dot(f0,n),Vector3.UnitX);
            var from=InstallAt;var to=RaiseAt;at=new Vector3(from.X+(to.X-from.X)*outward,from.Y+(to.Y-from.Y)*upward,from.Z+(to.Z-from.Z)*outward);
            // The power-up: the fist turned a little about its forearm, as if testing it (the bay's face turning with it, still to the lens).
            n=Vector3.Transform(n,CapturedRig.Rot(f,PowerTurn*NativeInstallShot.Ramp(p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd)*(1-raise)));
            // The give: back along the bay's inward normal under the press, the boss relics further, with a tremble.
            float give=NativeInstallShot.Kick(p-NativeInstallShot.Seat,30,.4f),depth=Profile.Boss?BossGive:Give+GiveLevel*Profile.Level;at-=n*(depth*give);
            if(Profile.Boss&&p>NativeInstallShot.Seat)at+=n*(.003f*(float)Math.Sin(2*Math.PI*9*(p-NativeInstallShot.Seat))*Math.Max(0,1-(p-NativeInstallShot.Seat)/.35f));
            // (Its wrist turn under the press: the forearm dips its hand end a little.)
            f=Vector3.Transform(f,CapturedRig.Rot(Vector3.Cross(f,n),-GiveTurn*give));normal=n;forearm=f;
        }
        // The receiving arm (the right when the left took the card): its designed present (for the draw) and the stage arm's from the rest
        // over an arch to it, the give, the power-up's turn, the hero hold, the settle.
        private void Present(CapturedRig.Pose pose,bool right,float p,Vector3 chest,Quaternion chestQ,Quaternion chestInv)
        {
            float k=NativeInstallShot.Ramp(p,NativeInstallShot.RiseStart,NativeInstallShot.RiseEnd)*(1-Settle);if(k<=1e-4f)return;
            int h=right?CapturedRig.RHand:CapturedRig.LHand,arm=h-2;bool leftArm=!right;
            for(int i=0;i<3;i++)rest[i]=pose.Rotation[arm+i];var restAt=Vector3.Transform(pose.WorldPosition[h]-chest,chestInv);var restTurn=chestInv*pose.WorldRotation[h];
            float raise=NativeInstallShot.Ramp(p,NativeInstallShot.HoldStart,NativeInstallShot.HoldStart+.3f)*(1-NativeInstallShot.Ramp(p,2.20f,NativeInstallShot.HoldEnd))*(Hero==NativeInstallShot.Raise&&!Short?1:0);
            PresentFrame(p,raise,out var bay,out var n,out var f);
            // The rise: a centimetre's sink first, then up over an arch, arriving with an overshoot along its travel.
            var restBay=restAt+Vector3.Transform(Device?PresentBay:NativeDraftPlant.CuffLocal,restTurn);var travel=CapturedRig.Unit(bay-restBay,Vector3.UnitY);
            float sink=-.01f*NativeInstallShot.Bump((p-NativeInstallShot.RiseStart)/.16f),over=(Toon?.016f:.012f)*NativeInstallShot.Bump((p-.76f)/(NativeInstallShot.RiseSettled-.76f));
            bay+=Vector3.UnitY*sink+travel*over;
            // Designed in the seated rest's chest, held in the room: the draw's present in the root frame.
            PresentAt=PlannedChestAt+Vector3.Transform(bay,PlannedChestQ);PresentNormal=Vector3.Transform(n,PlannedChestQ);PresentForearm=Vector3.Transform(f,PlannedChestQ);
            ForeWeight=k;RaiseShare=raise;ForeAiming=Device;
            // The stage arm: its hand turned so its bay (the model's as measured at the pick, or the voxel's cuff) takes the frame, its wrist
            // where that bay lands on the seat; into this frame's chest.
            var turn=HandTurn(n,f,out var wrist,bay);var held=chestInv*PlannedChestQ;turn=held*turn;var goal=Vector3.Transform(PlannedChestAt+Vector3.Transform(wrist,PlannedChestQ)-chest,chestInv);
            var at=Vector3.Lerp(restAt,goal,k)+Vector3.UnitY*(NativeDraftPlant.Arch*(float)Math.Sin(Math.PI*k));
            var presented=Vector3.Lerp(PresentPole,RaisePole,raise);var pole=Vector3.Lerp(right?new Vector3(-NativeDraftStage.RestPole.X,NativeDraftStage.RestPole.Y,NativeDraftStage.RestPole.Z):NativeDraftStage.RestPole,right?presented:new Vector3(-presented.X,presented.Y,presented.Z),k);
            CapturedRig.Arm(pose,leftArm,at,Quaternion.Slerp(restTurn,turn,k),pole);
            // The joints ease in from the rest over the rise's first quarter (no IK flip as it leaves the lap) and back over the settle.
            float ease=MathUtil.Smooth(k/.25f);if(ease<1)for(int i=0;i<3;i++){solved[i]=pose.Rotation[arm+i];pose.Rotation[arm+i]=Quaternion.Slerp(rest[i],solved[i],ease);}
            pose.World();
        }
        /// <summary>The receiving hand's turn (chest frame) that lands its bay's normal on `n` with the forearm along `f` (elbow to hand), and
        /// its wrist (chest frame) that puts the bay's seat on `bay` (the voxel pilot's palm-side cuff when not on the model).</summary>
        public Quaternion HandTurn(Vector3 n,Vector3 f,out Vector3 wrist,Vector3 bay)
        {
            var bn=Device?CapturedRig.Unit(PresentBayNormal,-Vector3.UnitZ):Vector3.UnitZ;var bf=CapturedRig.Unit(-Vector3.UnitY-bn*Vector3.Dot(-Vector3.UnitY,bn),Vector3.UnitX);
            var hand=CapturedRig.Basis(bf,bn,Vector3.Cross(bf,bn));var want=CapturedRig.Basis(f,n,Vector3.Cross(f,n));var turn=want*Quaternion.Inverse(hand);
            wrist=bay-Vector3.Transform(Device?PresentBay:NativeDraftPlant.CuffLocal,turn);return turn;
        }
        // The taking hand from the catch: the cartridge's planned frame, the hand on it (its pinch, then its press), the lift and the rest.
        private void Carry(CapturedRig.Pose pose,bool left,Vector3 catchLocal,float p,bool reduced)
        {
            int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;var chest=pose.WorldPosition[CapturedRig.Chest];var chestQ=pose.WorldRotation[CapturedRig.Chest];var chestInv=Quaternion.Inverse(chestQ);
            for(int i=0;i<3;i++)rest[i]=pose.Rotation[arm+i];var restAt=Vector3.Transform(NativeDraftStage.RestHand(left)-chest,chestInv);var restTurn=chestInv*NativeDraftStage.RestWrist(left);
            // Where the hand goes: on the cartridge through the press (its pinch, the press point from the thumb's let-go), lifted off it
            // along the bay's normal and back toward its own shoulder, then low in front of the body.
            var on=Vector3.Lerp(Pinch,PressAt,NativeInstallShot.Ramp(p,NativeInstallShot.ThumbOff,NativeInstallShot.PressShaped));
            var handTurn=ChipRotation*HandOnCartridge;
            if(!ChipShown&&CoreSeated)
            {
                OnBay(At,Normal,Forearm,0,out var seatC,out var seatQ);var pressed=PressOn(seatC,seatQ);handTurn=seatQ*HandOnCartridge;float lift=Lift(p);
                on=pressed+Normal*(FlickUp*lift)+LiftAway(At,Normal,Forearm,pose.WorldPosition[arm])*(FlickOver*lift);
            }
            var wrist=on-Vector3.Transform(PinchLocal,handTurn);var goal=Vector3.Transform(wrist-chest,chestInv);var goalTurn=chestInv*handTurn;
            // The absorb at the catch: the hand gives back along the flight and the wrist flexes.
            float absorb=NativeInstallShot.Kick(p-NativeInstallShot.Catch,22,.45f)*(Toon?1.3f:1);goal-=Vector3.Transform(catchFlight,chestInv)*(.015f*absorb);
            goalTurn=CapturedRig.Rot(Vector3.Transform(Vector3.UnitX,goalTurn),-.14f*absorb)*goalTurn;
            // After the lift the hand settles low (Rest), then into the lap with the settle.
            float low=NativeInstallShot.Ramp(p,1.24f,1.62f);
            if(low>0){var lowAt=new Vector3(left?LowRest.X:-LowRest.X,LowRest.Y,LowRest.Z);var lowTurn=Quaternion.Slerp(restTurn,goalTurn,.35f);goal=Vector3.Lerp(goal,lowAt,low)+Vector3.UnitY*(.04f*(float)Math.Sin(Math.PI*low));goalTurn=Quaternion.Slerp(goalTurn,lowTurn,low);}
            if(Settle>0){goal=Vector3.Lerp(goal,restAt,Settle)+Vector3.UnitY*(NativeDraftPlant.Arch*(float)Math.Sin(Math.PI*Settle));goalTurn=Quaternion.Slerp(goalTurn,restTurn,Settle);}
            var bend=new Vector3(left?1:-1,-.75f,-.35f);CapturedRig.Arm(pose,left,goal,goalTurn,Vector3.Lerp(bend,left?NativeDraftStage.RestPole:new Vector3(-NativeDraftStage.RestPole.X,NativeDraftStage.RestPole.Y,NativeDraftStage.RestPole.Z),Settle));
            float settle=MathUtil.Smooth((Settle-.75f)/.25f);if(settle>0)for(int i=0;i<3;i++){solved[i]=pose.Rotation[arm+i];pose.Rotation[arm+i]=Quaternion.Slerp(solved[i],rest[i],settle);}
            pose.World();
        }
        /// <summary>
        /// The outputs from the pose as it stands: the bay (on the model the designed present by the arm's rise into it, the receiving hand's
        /// bay before; the voxel pilot's cuff on its hand), and the cartridge's planned frame at pose age `p`: from the burst into the catch
        /// (Burst..Catch, toward the taking hand's pinch, slowing, its bank settling), from the catch over an S-curve to the hover over the bay
        /// (twisted a few degrees off square), squared and lowered to Align over the line-up (with a tremble), pressed onto the frame (to
        /// Contact), the core latched into the seat (to Seat); the shell bursts at Release. Call again after anything turns the chest.
        /// </summary>
        public void Place(CapturedRig.Pose pose,bool left,float p,Vector3 toCamera)
        {
            int other=left?CapturedRig.RHand:CapturedRig.LHand,hand=left?CapturedRig.LHand:CapturedRig.RHand;
            var q=pose.WorldRotation[other];At=pose.WorldPosition[other]+Vector3.Transform(Device?BayLocal:NativeDraftPlant.CuffLocal,q);
            Normal=CapturedRig.Unit(Vector3.Transform(Device?CapturedRig.Unit(BayNormal,-Vector3.UnitZ):Vector3.UnitZ,q),Vector3.UnitZ);
            var f=pose.WorldPosition[other]-pose.WorldPosition[other-1];Forearm=CapturedRig.Unit(f-Normal*Vector3.Dot(f,Normal),Vector3.UnitX);
            // On the model the bay is where the draw puts it: the designed present, as far as the arm has risen into it.
            if(Device&&ForeAiming&&ForeWeight>0)
            {
                float k=ForeWeight;At=Vector3.Lerp(At,PresentAt,k);Normal=CapturedRig.Unit(Vector3.Lerp(Normal,PresentNormal,k),PresentNormal);
                var pf=Vector3.Lerp(Forearm,PresentForearm,k);Forearm=CapturedRig.Unit(pf-Normal*Vector3.Dot(pf,Normal),PresentForearm);
            }
            PlannedAt=PlannedChestAt+Vector3.Transform(InstallAt,PlannedChestQ);PlannedNormal=Vector3.Transform(NormalWant,PlannedChestQ);
            var handAt=pose.WorldPosition[hand];var handQ=pose.WorldRotation[hand];var pinchNow=handAt+Vector3.Transform(PinchLocal,handQ);HandPinch=pinchNow;
            ChipShown=p>=NativeInstallShot.Burst&&p<NativeInstallShot.Release;CoreSeated=p>=NativeInstallShot.Release;Latch=NativeInstallShot.EaseIn((p-NativeInstallShot.Contact)/(NativeInstallShot.Seat-NativeInstallShot.Contact));
            Lights=1+.6f*(1-NativeInstallShot.Smooth((p-NativeInstallShot.Burst)/(.80f-NativeInstallShot.Burst)));
            // The catch's turn: its back face to the camera, its crest toward the taking hand (CatchTurn); the burst's crest up, as the card's.
            var catchQ=CatchTurn(toCamera,left);CatchRotation=catchQ;var face=CapturedRig.Unit(toCamera,Vector3.UnitZ);var up=CapturedRig.Unit(Vector3.UnitY-face*Vector3.Dot(Vector3.UnitY,face),Vector3.UnitY);var burstQ=CapturedRig.Basis(Vector3.Cross(face,up),face,up);
            if(p<NativeInstallShot.Catch)
            {
                caught=false;if(!ChipShown){Chip=pinchNow;ChipRotation=catchQ;BayShare=0;Pinch=pinchNow;PressAt=pinchNow;return;}
                // In the air from the burst into the hand: slowing, turning a quarter about its own normal from the card's upright (its
                // bank settling) to its crest toward the hand.
                if(!Burst){BurstAt=pinchNow;BurstTurn=burstQ;}
                float u=(p-NativeInstallShot.Burst)/(NativeInstallShot.Catch-NativeInstallShot.Burst),e=NativeInstallShot.EaseOut(u);
                ChipRotation=Quaternion.Slerp(burstQ*CapturedRig.Rot(Vector3.UnitY,NativeInstallShot.FlyBank*(float)Math.PI/180),catchQ,NativeInstallShot.EaseOutBack(u,.8f));
                var to=pinchNow-Vector3.Transform(Vector3.UnitZ,ChipRotation)*((CoreZ(Tier)+CrestEdge)*Scale);Chip=Vector3.Lerp(BurstAt,to,e);
                Pinch=PinchOn(Chip,ChipRotation);PressAt=PressOn(Chip,ChipRotation);BayShare=0;return;
            }
            if(!caught){caught=true;catchTurn=catchQ;catchAt=pinchNow-Vector3.Transform(Vector3.UnitZ,catchQ)*((CoreZ(Tier)+CrestEdge)*Scale);var flight=catchAt-BurstAt;catchFlight=CapturedRig.Unit(flight,-toCamera);}
            // Planned on the bay as posed: hovering, lined up, pressed.
            OnBay(At,Normal,Forearm,Hover,out var hoverC,out var seatQ);var hoverQ=seatQ*CapturedRig.Rot(Vector3.UnitY,HoverTwist);
            if(p<NativeInstallShot.CarryEnd)
            {
                float u=(p-NativeInstallShot.Catch)/(NativeInstallShot.CarryEnd-NativeInstallShot.Catch),s=NativeInstallShot.Smooth(u);
                // The S-curve: leaving along the flight a little, coming in from the wrist side (along -forearm) and down onto the hover.
                var m0=catchFlight*.12f+Vector3.UnitY*.10f;var m1=-Forearm*.22f-Normal*.08f;
                Chip=NativeInstallShot.Hermite(catchAt,m0,hoverC,m1,s);ChipRotation=Quaternion.Slerp(catchTurn,hoverQ,NativeInstallShot.Smooth(u*1.15f));BayShare=s;
                // A wild chip twitches in the pinch, dying away through the carry.
                if(Profile.Wild){float t=p-NativeInstallShot.Catch;ChipRotation=ChipRotation*CapturedRig.Rot(Vector3.UnitY,.105f*(float)Math.Sin(2*Math.PI*14*t)*(float)Math.Exp(-t/.25f));}
            }
            else if(p<NativeInstallShot.AlignEnd)
            {
                float u=(p-NativeInstallShot.CarryEnd)/(NativeInstallShot.AlignEnd-NativeInstallShot.CarryEnd);OnBay(At,Normal,Forearm,Hover+(Align-Hover)*NativeInstallShot.EaseOut(u),out Chip,out _);
                ChipRotation=Quaternion.Slerp(hoverQ,seatQ,NativeInstallShot.Smooth(u));BayShare=1;
                // The tremble against the device's pull (none with Reduced effects).
                Chip+=Vector3.Transform(Vector3.UnitX,seatQ)*(Tremble*(float)Math.Sin(2*Math.PI*TrembleHz*(p-NativeInstallShot.CarryEnd))*NativeInstallShot.Bump(u));
            }
            else
            {
                float u=(p-NativeInstallShot.AlignEnd)/(NativeInstallShot.Contact-NativeInstallShot.AlignEnd);OnBay(At,Normal,Forearm,Align*(1-NativeInstallShot.EaseIn(u)),out Chip,out _);ChipRotation=seatQ;BayShare=1;
            }
            Pinch=PinchOn(Chip,ChipRotation);PressAt=PressOn(Chip,ChipRotation);
        }

        // ---- the Siren drafter ----
        /// <summary>The Siren's hold on the cartridge: at the tips of her claws (hand frame: down the claw), pushed out toward the lens so the
        /// talons never cover it; and how far her claw stops short of the grin (to its side and a little under it, so the grin stays readable).</summary>
        public static readonly Vector3 SirenTips=new Vector3(0,-.11f,.02f),SirenGrinSide=new Vector3(.13f,.01f,.05f);
        public const float SirenToLens=.05f;
        /// <summary>
        /// The Siren's devour on the same keys (her reaching claw on the card's side): the claw strikes at the card with an overshoot,
        /// the cartridge flies into her talons' tips at Catch (held there on the camera's side, lit), she draws it up beside her grin (the
        /// head tilting back, then down onto it), light leaks between the claws, the crush at the clack (the shell bursts between them), the
        /// core's light streams into the grin, then a silent laugh (the head back, the shoulders shaking); the claw lowers over the settle.
        /// `grin` where she eats (root frame), `toCamera` the direction to the lens.
        /// </summary>
        public void SirenPose(CapturedRig.Pose pose,bool left,Vector3 grin,float p,Vector3 toCamera)
        {
            pose.World();if(!(p>=0)){Settle=0;HeadPitch=0;return;}
            Settle=Short?0:NativeInstallShot.Ramp(p,NativeInstallShot.SettleStart(TurnSeconds),NativeInstallShot.SettleEnd(TurnSeconds));
            int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;var chest=pose.WorldPosition[CapturedRig.Chest];var chestInv=Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]);
            float draw=NativeInstallShot.Ramp(p,NativeInstallShot.Catch,1.0f)*(1-Settle),laugh=NativeInstallShot.Ramp(p,1.34f,1.40f)*(1-NativeInstallShot.Ramp(p,1.62f,1.75f));
            // Her head: snapping toward the card at the pick, back as she draws it up, down onto it, back into the laugh, shaking.
            float shake=(float)Math.Sin(2*Math.PI*7*(p-1.34f))*laugh;
            HeadPitch=(-.14f*NativeInstallShot.Bump((p-.62f)/.5f)+.10f*NativeInstallShot.Ramp(p,.95f,1.10f)*(1-NativeInstallShot.Ramp(p,1.30f,1.40f))-.12f*laugh)*(1-Settle);
            pose.Position[CapturedRig.Body].Y+=.02f*NativeInstallShot.Ramp(p,0,.08f)*(1-NativeInstallShot.Ramp(p,.4f,.7f))+.012f*shake;
            if(draw>1e-4f)
            {
                for(int i=0;i<3;i++)rest[i]=pose.Rotation[arm+i];var from=Vector3.Transform(pose.WorldPosition[hand]-chest,chestInv);var fromTurn=Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest])*pose.WorldRotation[hand];
                // Beside the grin, not over it: the claw stops to its side and under it.
                var beside=grin+Vector3.Transform(new Vector3(left?SirenGrinSide.X:-SirenGrinSide.X,SirenGrinSide.Y,SirenGrinSide.Z),pose.WorldRotation[CapturedRig.Head]);
                var to=Vector3.Transform(beside-chest,chestInv);var squeeze=NativeInstallShot.Kick(p-NativeInstallShot.Seat,28,.35f)*.02f;
                var goal=Vector3.Lerp(from,to-new Vector3(0,squeeze,0),NativeInstallShot.Smooth(draw))+new Vector3(0,.06f,0)*(float)Math.Sin(Math.PI*draw);
                CapturedRig.Arm(pose,left,goal,Quaternion.Slerp(fromTurn,CapturedRig.Euler(-1.2f,left?-.6f:.6f),draw),new Vector3(left?1:-1,-.7f,-.4f));pose.World();
            }
            int h=hand;ChipShown=p>=NativeInstallShot.Burst&&p<NativeInstallShot.Seat;CoreSeated=false;Latch=0;
            var face=CapturedRig.Unit(toCamera,Vector3.UnitZ);var tips=pose.WorldPosition[h]+Vector3.Transform(SirenTips,pose.WorldRotation[h])+face*SirenToLens;
            At=grin;Normal=Vector3.UnitZ;Forearm=Vector3.UnitX;HandPinch=tips;
            if(p<NativeInstallShot.Catch){if(!Burst){BurstAt=tips;}float e=NativeInstallShot.EaseOut((p-NativeInstallShot.Burst)/(NativeInstallShot.Catch-NativeInstallShot.Burst));Chip=Vector3.Lerp(BurstAt,tips,e);}
            else Chip=tips;
            // Its face to the lens in the talons (the back face toward the camera, its crest up the claw).
            var up=CapturedRig.Unit(Vector3.Transform(Vector3.UnitY,pose.WorldRotation[h])-face*Vector3.Dot(Vector3.Transform(Vector3.UnitY,pose.WorldRotation[h]),face),Vector3.UnitY);
            ChipRotation=CapturedRig.Basis(Vector3.Cross(face,up),face,up);Pinch=PressAt=Chip;BayShare=0;CatchRotation=ChipRotation;
        }
        /// <summary>The cartridge's turn at the catch: its back face toward the camera (`toCamera`, root frame), its crest toward the hand
        /// that takes it (the pilot's left for the left hand), so the carry has only its face to tilt onto the bay.</summary>
        public static Quaternion CatchTurn(Vector3 toCamera,bool left)
        {
            var face=CapturedRig.Unit(toCamera,Vector3.UnitZ);var side=new Vector3(left?1:-1,0,0);side=CapturedRig.Unit(side-face*Vector3.Dot(side,face),Vector3.UnitX);return CapturedRig.Basis(Vector3.Cross(face,side),face,side);
        }
        /// <summary>Snapshots the burst (the runtime calls it the first frame past Burst with the point the 2D card hands over at).</summary>
        public void Hand(Vector3 at,Quaternion turn){BurstAt=at;BurstTurn=turn;Burst=true;}
    }
}
