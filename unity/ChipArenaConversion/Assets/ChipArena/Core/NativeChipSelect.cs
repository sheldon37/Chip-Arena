using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // PvE chip selection, as the other pilots see it (owner 2026-10-13: "when other people are picking chips on PVE, we can see
    // them looking at a holographic display through their wrist. Above, it'll show a small animated Text 'Selecting Chip'").
    // Presentation only: nothing here reads back into the match, the pick flow, the network frame or a save.
    //
    // Electron (client/pve/hologram.ts, world-view.ts, board.ts at 05950ea), the baseline: over every remote party pilot whose
    // personal chip screen is open (PveView.party[id].open; hidden while dead or beyond 55 m) a cyan, unlit, additive hologram
    // .55 m over the head (a fan of three card planes, a spinning chip die and a projector cone, growing in over .25 s from the
    // replicated openAt, flickering, blinking red at 6 Hz while the chooser is under attack), the choosing pose after the rig
    // animation (head down .25 rad, the right forearm raised toward the chest, eased in over .25 s) and "Choosing chip" in the
    // party board. Unity keeps the trigger, the red alarm, the fan of three cards and the cyan look, and moves the hologram onto
    // the wrist the pilot is looking at:
    //  - State: the replicated open pick of the pilot's PvE row (NativePveRowFrame.open, every row to every guest; NativeArena
    //    rebuilds NativePveRow.Open from it). Party pilots only (bots too: their wave picks think for 2-6 s), alive, connected,
    //    not during a hold, a cinematic or the end. No open timestamp is replicated, so each client eases in from the frame it
    //    first sees the pick open (a late joiner too); the gestures run on the pick's replicated grant time, so every client
    //    taps on the same beat. docs/PVE-CHIP-SELECT.md is the contract.
    //  - Pose (NativeChipSelectPose): the display wrist (the hand that does not hold the weapon: the left, or the right for the
    //    staffs, L33t H4x and the bow) comes up in front of the chest with the back of the wrist turned to the eyes, the head
    //    and a little of the chest bow to it. The weapon stays in its own hand: lowered (the greatsword trailing low behind, its
    //    tip off the floor), or kept where its idle carries it (the staff, the bow). An empty other hand (unarmed, the fighters'
    //    gauntlets) taps and swipes at the display now and then, a pistol or dagger hand swipes along its lower edge with the
    //    weapon kept pointing down, and the display wrist flicks for the long weapons. Legs and pelvis are never touched, so the
    //    feet stay planted and the gait walks under it. First person (NativeChipSelectPresentation's view model) the wrist sits
    //    low in view with a small panel and the weapon goes down out of view.
    //  - Hologram (NativeChipSelectHolo): a hardlight panel rising over the forearm with the offered chips as three small cards
    //    in their rarity colours, the lit one following the gestures, scanlines, flicker and a projection cone; danger red,
    //    blinking, while the pilot is being hit.
    //  - Tag (NativeChipSelectTag): "SELECTING CHIP" typed in over the head, then three pulsing dots.
    public enum ChipSelectCarry { Free, Fist, Pistol, LongGun, Blade, Daggers, Greatsword, Shield, Staff, Bow }
    public enum ChipSelectGestureKind { None, Tap, Swipe, Flick }
    /// <summary>What the hand without the display does: an empty hand (unarmed, the gauntlets) taps and swipes; a hand carrying a
    /// pistol or a dagger swipes along the panel's lower edge (its weapon kept pointing down, below the cards); a long weapon stays
    /// lowered and the display wrist flicks instead.</summary>
    public enum ChipSelectHands { Flicks, Swipes, Taps }
    public struct ChipSelectGesture
    {
        public ChipSelectGestureKind Kind;
        /// <summary>Seconds into the gesture, its length, and the card it lights at its contact (the card lit before it in Was).</summary>
        public float Age,Length;public int Card,Was;
        /// <summary>The gesture's reach 0..1 (out to the display and back) and its contact 0..1 (pressed, or the swipe's slide).</summary>
        public float Reach,Contact;
        /// <summary>Seconds since the light moved to Card (the press, the end of a swipe's slide, a flick's peak; negative before).</summary>
        public float SinceContact;
    }

    public static class NativeChipSelect
    {
        // ---- the replicated mapping (the one place the pick-open state is read; docs/PVE-CHIP-SELECT.md) ----
        /// <summary>Whether a pilot shows the selecting look: a party member (has a PvE row) with an open pick (its id, 0 none),
        /// alive and connected, while no hold, cinematic or end of run hides the personal pick screens.</summary>
        public static bool Selecting(bool member,int openPick,bool alive,bool connected,bool holding,bool cinematic,bool ended)
            =>member&&openPick>0&&alive&&connected&&!holding&&!cinematic&&!ended;

        // ---- the hold: which wrist wears the display and what the other hand does ----
        public static bool Staff(string spec)=>spec=="fire"||spec=="water"||spec=="lightning"||spec=="darkness"||spec=="leet";
        /// <summary>The display wrist is the left, except where the weapon rides the left hand (the staffs, L33t H4x, the bow).</summary>
        public static bool DisplayLeft(string spec)=>!(Staff(spec)||spec=="bow");
        public static ChipSelectCarry Carry(string spec)
        {
            switch(spec)
            {
                case "pistol":return ChipSelectCarry.Pistol;
                case "rifle":case "machinegun":return ChipSelectCarry.LongGun;
                case "sword":return ChipSelectCarry.Blade;
                case "daggers":return ChipSelectCarry.Daggers;
                case "greatsword":return ChipSelectCarry.Greatsword;
                case "shield":return ChipSelectCarry.Shield;
                case "bow":return ChipSelectCarry.Bow;
                case "emperor":case "ansatsuken":return ChipSelectCarry.Fist;
                default:return Staff(spec)?ChipSelectCarry.Staff:ChipSelectCarry.Free;
            }
        }
        public static ChipSelectHands Hands(ChipSelectCarry c)=>c==ChipSelectCarry.Free||c==ChipSelectCarry.Fist?ChipSelectHands.Taps:c==ChipSelectCarry.Pistol||c==ChipSelectCarry.Daggers?ChipSelectHands.Swipes:ChipSelectHands.Flicks;
        /// <summary>The other hand reaches the display (it taps or swipes; else the display wrist flicks).</summary>
        public static bool Taps(ChipSelectCarry c)=>Hands(c)!=ChipSelectHands.Flicks;
        /// <summary>The other hand keeps its idle hold (its weapon is carried low there already): the staffs and the bow.</summary>
        public static bool KeepsIdle(ChipSelectCarry c)=>c==ChipSelectCarry.Staff||c==ChipSelectCarry.Bow;

        // ---- timings ----
        /// <summary>The look eases in over BlendIn and out over BlendOut (s); a death drops it at once.</summary>
        public const float BlendIn=.42f,BlendOut=.3f;
        /// <summary>The hologram unfolds once the wrist is mostly up (weight from HoloFrom).</summary>
        public const float HoloFrom=.55f;
        /// <summary>A new pick opening straight after a choice (the queue) re-deals the cards over Redeal seconds.</summary>
        public const float Redeal=.42f;
        /// <summary>Gesture slots (s): the other hand acts at most once per slot; Tap / Swipe / Flick lengths.</summary>
        public const float TapSlot=2.4f,FlickSlot=2.1f,TapLength=.72f,SwipeLength=1.05f,FlickLength=.55f;
        /// <summary>Where in a gesture the contact lands (fraction of its length): the press, the swipe's slide, the flick's peak.</summary>
        public const float TapPress=.38f,TapLift=.5f,SwipeTouch=.3f,SwipeEnd=.68f,FlickPeak=.45f;
        /// <summary>A hit while the look is up (the Expedition hold keeps the chooser standing under fire): the layer yields up to HitYield
        /// of its weight to the route's pose, where the hit reaction plays, rising over FlinchRise and settling over FlinchFall, so the
        /// raised wrist and the gaze jolt with the blow instead of masking it (the torso's reaction already carries the raised arm).</summary>
        public const float HitYield=.45f,FlinchRise=.06f,FlinchFall=.4f;
        /// <summary>The flinch 0..1 `sinceHit` seconds after the newest damaging hit (negative or not finite: none).</summary>
        public static float Flinch(float sinceHit)
        {
            if(!MathUtil.Finite(sinceHit)||sinceHit<0||sinceHit>=FlinchRise+FlinchFall)return 0;
            return sinceHit<FlinchRise?sinceHit/FlinchRise:1-MathUtil.Smooth((sinceHit-FlinchRise)/FlinchFall);
        }

        // A small deterministic hash (the same beat on every client): 0..1 from a seed and an index.
        public static float Hash(int seed,int index,int salt)
        {
            uint h=unchecked((uint)seed*0x9e3779b1u^(uint)index*0x85ebca6bu^(uint)salt*0xc2b2ae35u);
            h^=h>>16;h=unchecked(h*0x7feb352du);h^=h>>15;h=unchecked(h*0x846ca68bu);h^=h>>16;return (h&0xffffff)/16777216f;
        }
        public static int Seed(int pilot,int pick)=>unchecked(pilot*7919+pick*104729+17);
        /// <summary>The card a slot's gesture lights: a seeded start and steps of one or two cards (closed form, so every client and
        /// every frame agree, and two slots in a row never light the same card).</summary>
        public static int SlotCard(int seed,int slot,int count)
        {
            if(count<=1||slot<0)return 0;int phase=(int)(Hash(seed,0,3)*count)%count;if(count==2)return (phase+slot)%2;
            return (phase+slot+(int)Math.Floor(slot*Hash(seed,0,5)))%count;
        }
        private static float SlotOf(ChipSelectHands hands)=>hands==ChipSelectHands.Flicks?FlickSlot:TapSlot;
        private static ChipSelectGestureKind KindOf(ChipSelectHands hands,int seed,int slot)=>hands==ChipSelectHands.Taps?(Hash(seed,slot,2)<.62f?ChipSelectGestureKind.Tap:ChipSelectGestureKind.Swipe):hands==ChipSelectHands.Swipes?ChipSelectGestureKind.Swipe:ChipSelectGestureKind.Flick;
        /// <summary>The gesture at `clock` seconds on the pick's beat (its grant time), for what the other hand does. The lit card is
        /// the last contact's (Highlight).</summary>
        public static ChipSelectGesture GestureAt(float clock,int seed,ChipSelectHands hands,int count)
        {
            var g=new ChipSelectGesture();if(!MathUtil.Finite(clock)||clock<0){g.SinceContact=-1;return g;}
            float slot=SlotOf(hands);int i=(int)Math.Floor(clock/slot);g.Card=g.Was=Highlight(clock,seed,hands,count);g.SinceContact=-1;
            var kind=KindOf(hands,seed,i);
            float length=kind==ChipSelectGestureKind.Tap?TapLength:kind==ChipSelectGestureKind.Swipe?SwipeLength:FlickLength,start=i*slot+.25f+(slot-length-.35f)*Hash(seed,i,4),age=clock-start;
            if(age<0||age>=length)return g;
            g.Kind=kind;g.Age=age;g.Length=length;g.Card=SlotCard(seed,i,count);g.Was=i>0?SlotCard(seed,i-1,count):0;
            // SinceContact counts from where the light moves: the press, the end of the swipe's slide, the flick's peak.
            float u=age/length,contact=kind==ChipSelectGestureKind.Tap?TapPress:kind==ChipSelectGestureKind.Swipe?SwipeEnd:FlickPeak;g.SinceContact=age-contact*length;
            switch(kind)
            {
                case ChipSelectGestureKind.Tap:g.Reach=u<TapPress?MathUtil.Smooth(u/TapPress):1-MathUtil.Smooth((u-TapLift)/(1-TapLift));g.Contact=u<TapPress-.08f?0:u<TapPress?MathUtil.Smooth((u-(TapPress-.08f))/.08f):u<TapLift?1:Math.Max(0,1-(u-TapLift)/.06f);break;
                case ChipSelectGestureKind.Swipe:g.Reach=u<SwipeTouch?MathUtil.Smooth(u/SwipeTouch):u<SwipeEnd?1:1-MathUtil.Smooth((u-SwipeEnd)/(1-SwipeEnd));g.Contact=u<SwipeTouch?0:u<SwipeEnd?MathUtil.Smooth((u-SwipeTouch)/(SwipeEnd-SwipeTouch)):1;break;
                default:g.Reach=(float)Math.Sin(Math.PI*u);g.Contact=u<FlickPeak?0:1;break;
            }
            return g;
        }
        /// <summary>The lit card at `clock`: the card of the last contact that has landed (0 before the first).</summary>
        public static int Highlight(float clock,int seed,ChipSelectHands hands,int count)
        {
            if(count<=1||!MathUtil.Finite(clock)||clock<0)return 0;float slot=SlotOf(hands);int i=(int)Math.Floor(clock/slot);
            for(int k=i;k>=0&&k>=i-1;k--)
            {
                var kind=KindOf(hands,seed,k);
                float length=kind==ChipSelectGestureKind.Tap?TapLength:kind==ChipSelectGestureKind.Swipe?SwipeLength:FlickLength,start=k*slot+.25f+(slot-length-.35f)*Hash(seed,k,4);
                float contact=start+(kind==ChipSelectGestureKind.Tap?TapPress:kind==ChipSelectGestureKind.Swipe?SwipeEnd:FlickPeak)*length;
                if(clock>=contact)return SlotCard(seed,k,count);
            }
            return 0;
        }
    }

    /// <summary>
    /// A blend between two hand turns that keeps going round the same way from frame to frame. Slerp takes the shorter way, so where the
    /// two turns are about half a turn apart (the dagger the display hand holds, turned from its rest to the watch) the way round flips
    /// as either end drifts across the half turn, and the hand spins through 160 degrees in one frame. Each end keeps the quaternion sign
    /// nearest its last frame's, and the blend runs between them as they are (the first frame after a Reset takes the shorter way).
    /// Allocation-free; one per blend and pilot.
    /// </summary>
    public struct ChipTurnBlend
    {
        private Quaternion a,b;private bool live;
        public void Reset(){live=false;}
        public Quaternion Blend(Quaternion from,Quaternion to,float t)
        {
            t=MathUtil.Clamp(MathUtil.Finite(t)?t:0,0,1);
            if(!live){if(Quaternion.Dot(from,to)<0)to=Quaternion.Negate(to);}
            else{if(Quaternion.Dot(from,a)<0)from=Quaternion.Negate(from);if(Quaternion.Dot(to,b)<0)to=Quaternion.Negate(to);}
            a=from;b=to;live=true;
            float d=MathUtil.Clamp(Quaternion.Dot(from,to),-1,1);
            if(d>.9995f)return Quaternion.Normalize(new Quaternion(from.X+(to.X-from.X)*t,from.Y+(to.Y-from.Y)*t,from.Z+(to.Z-from.Z)*t,from.W+(to.W-from.W)*t));
            float angle=(float)Math.Acos(d),sin=(float)Math.Sin(angle);if(sin<1e-5f)return t<.5f?from:to;
            float wa=(float)Math.Sin((1-t)*angle)/sin,wb=(float)Math.Sin(t*angle)/sin;
            return Quaternion.Normalize(new Quaternion(from.X*wa+to.X*wb,from.Y*wa+to.Y*wb,from.Z*wa+to.Z*wb,from.W*wa+to.W*wb));
        }
    }

    /// <summary>One pilot's selecting look on the presentation clock: the eased weight, the open edge (seen here, as no open time
    /// is replicated) and the re-deal when a queued pick opens straight after a choice. Allocation-free.</summary>
    public sealed class NativeChipSelectTrack
    {
        public float Weight{get;private set;}
        /// <summary>The linear ramp under Weight (0..1), the open pick last seen (0: none), when this run opened and when the pick
        /// last changed while open (presentation clock; NaN before).</summary>
        public float Ramp{get;private set;}public int Pick{get;private set;}public float OpenedAt{get;private set;}=float.NaN;public float ChangedAt{get;private set;}=float.NaN;
        private float last=float.NaN;
        public void Reset(){Weight=Ramp=0;Pick=0;OpenedAt=ChangedAt=last=float.NaN;}
        /// <summary>Steps to `time`. `selecting` from NativeChipSelect.Selecting, `pick` the open pick id (0 none), `dead` drops the
        /// look at once. A clock that jumps back (a killcam, a new round) or a gap over Gap (unposed: dead, revived) restarts it from
        /// nothing; a long frame eases at most .1 s.</summary>
        public const float Gap=.5f;
        public float Step(bool selecting,int pick,float time,bool dead=false)
        {
            if(!MathUtil.Finite(time))return Weight;
            // A clock that jumps back, or a pilot not posed for a while (dead, then revived), starts over from nothing.
            if(!float.IsNaN(last)&&(time<last-.05f||time-last>Gap))Reset();
            float dt=float.IsNaN(last)?0:Math.Min(.1f,Math.Max(0,time-last));last=time;
            if(dead){Ramp=Weight=0;Pick=0;return 0;}
            if(selecting)
            {
                if(Ramp<=0||Pick==0){OpenedAt=time;ChangedAt=float.NaN;}else if(pick!=Pick)ChangedAt=time;
                Pick=pick;Ramp=Math.Min(1,Ramp+dt/NativeChipSelect.BlendIn);
            }
            else{Ramp=Math.Max(0,Ramp-dt/NativeChipSelect.BlendOut);if(Ramp<=0)Pick=0;}
            Weight=MathUtil.Smooth(Ramp);return Weight;
        }
        /// <summary>Seconds since this run opened (the tag's typing, the hologram's boot); since the last re-deal (Redeal while it runs).</summary>
        public float Age(float time)=>float.IsNaN(OpenedAt)?0:Math.Max(0,time-OpenedAt);
        public float SinceChange(float time)=>float.IsNaN(ChangedAt)?float.PositiveInfinity:Math.Max(0,time-ChangedAt);
    }

    /// <summary>The input of one pose: the weight, the gesture clock (seconds on the pick's beat), the pilot's spec, the gesture
    /// seed, the offers shown (for the tap targets), first person (with its goals, rig-root frame, source axes) and the reach of
    /// a melee weapon (its stretched blade for the floor guard).</summary>
    public struct ChipSelectInput
    {
        public float Weight,Clock,Time,Reach;public string Spec;public int Seed,Cards;public bool Reduced,FirstPerson;
        /// <summary>The hit flinch 0..1 (NativeChipSelect.Flinch: the layer yields to the hit reaction) and the gestures' hush 0..1 (the
        /// wrist plant's: the other hand stops tapping while it takes the chip, NativeChipPlant). Both 0 by default.</summary>
        public float Flinch,Hush;
        /// <summary>First person: where the display wrist goes and the eye (the camera), in the view rig's root frame.</summary>
        public Vector3 ViewWrist,ViewEye;
    }

    /// <summary>
    /// The selecting pose layer over a pilot's pose (rig root frame, source axes: +x left), after the route's own pose: the
    /// display wrist raised in front of the chest with the back of the wrist to the eyes, the head (and a little chest bow)
    /// looking down at the display, the weapon kept in its own hand (lowered, or held as its idle holds it), the other hand tapping
    /// or swiping when it can (else the display wrist flicks). Arms, neck, head,
    /// spine and chest only; the legs, pelvis and body root keep the route's pose (feet planted, the gait walks under it). The
    /// outputs (Emitter .. PanelUp) are current on return; Measure adds the other hand's tip. Allocation-free.
    /// </summary>
    public sealed class NativeChipSelectPose
    {
        // The display (chest frame, authored for the left wrist, mirrored for the right): in front of the upper chest just past
        // the midline, the elbow down and out. The eye before the head turns (chest frame) aims the wrist; the emitter sits on
        // the back of the wrist cuff (hand frame: the hand's +z is the palm side, its -y the fingers).
        public static readonly Vector3 Raised=new Vector3(.06f,.04f,.33f),RaisedPole=new Vector3(1,-.75f,-.2f),RestPole=new Vector3(1,-.8f,-.25f),EyeChest=new Vector3(0,.6f,.13f);
        public static readonly Vector3 EmitterLocal=new Vector3(0,.02f,-.1f),TipLocal=new Vector3(0,-.13f,.03f);
        public const float Arch=.05f,PanelLift=.03f,PanelRise=.085f,PanelForward=.03f,ViewLift=.085f,HeadShare=.6f,HeadMax=.72f,YawShare=.35f,ChestBow=.06f,ChestTurn=.06f,SpineBow=.04f,FlickRoll=.32f,PanelSteep=.5f;
        /// <summary>The panel (third person / first person), metres: its width and height (cards inside it).</summary>
        public const float PanelWidth=.3f,PanelHeight=.18f,ViewWidth=.14f,ViewHeight=.085f;
        /// <summary>First person: the display wrist (a left one), camera frame (Unity: x right, y up, z forward).</summary>
        public static readonly Vector3 ViewWristCamera=new Vector3(-.1f,-.235f,.42f);
        /// <summary>The first-person display wrist for `spec` (camera frame): low left, or low right for a right display.</summary>
        public static Vector3 ViewWrist(string spec)=>NativeChipSelect.DisplayLeft(spec)?ViewWristCamera:new Vector3(-ViewWristCamera.X,ViewWristCamera.Y,ViewWristCamera.Z);
        /// <summary>
        /// First person under an Expedition chip tray (the cards docked at the bottom centre, at most 52% of the width): the display
        /// wrist beside the tray instead of behind its cards, TrayAcross of the way from the centre to the left edge (TrayAcrossRight to
        /// the right for a right display, whose panel rides further out) and TrayDown of the way down, at TrayDepth (camera frame;
        /// `tanHalfWidth` / `tanHalfHeight` the view's half angles). The greatsword's and the bow's view arms cannot reach out there
        /// (their view rigs stand the display shoulder too far in), so they keep their place under the tray.
        /// </summary>
        public static Vector3 TrayWrist(string spec,float tanHalfWidth,float tanHalfHeight)
        {
            if(!Beside(spec))return ViewWrist(spec);bool left=NativeChipSelect.DisplayLeft(spec);
            float x=-(left?TrayAcross:TrayAcrossRight)*tanHalfWidth*TrayDepth,y=-TrayDown*tanHalfHeight*TrayDepth;
            return new Vector3(left?x:-x,y,TrayDepth);
        }
        public static bool Beside(string spec)=>spec!="bow"&&spec!="greatsword";
        public const float TrayAcross=.74f,TrayAcrossRight=.44f,TrayDown=.32f,TrayDepth=.4f;
        // Outputs (rig root frame, source axes).
        public Vector3 Emitter,Normal=Vector3.UnitY,Forearm=Vector3.UnitX,Eye,PanelCentre,PanelNormal=Vector3.UnitZ,PanelRight=Vector3.UnitX,PanelUp=Vector3.UnitY,Tip;
        public bool Left;public ChipSelectCarry Carry;public ChipSelectGesture Gesture;
        /// <summary>The panel's size factor: 1 third person; first person it shrinks with its distance to the eye (ViewDistance at
        /// full size), so a view base whose arm cannot reach as far (the bow's) shows it at about the same angle.</summary>
        public float PanelScale=1;
        public const float ViewDistance=.4f;
        /// <summary>The other hand's tip against the panel (Measure): its distance to the panel plane (m, + in front) and its panel
        /// coordinates (-1..1 across, -1..1 up).</summary>
        public float TipDepth,TipU,TipV;
        private readonly Quaternion[] rest=new Quaternion[CapturedRig.Bones.Length],solved=new Quaternion[3];
        // The hands' turn blends (the display wrist's raise, the other hand's carry and its tap), kept round the same way frame to frame.
        private ChipTurnBlend raise,carry,tap;
        private static readonly int[] Touched={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        /// <summary>The joints the layer may write (everything else keeps the route's pose).</summary>
        public static bool Writes(int bone)=>Array.IndexOf(Touched,bone)>=0;
        // Authored for one side: `Mirror(v, true)` flips x (source frame: +x left).
        private static Vector3 Mirror(Vector3 v,bool flip)=>flip?new Vector3(-v.X,v.Y,v.Z):v;

        public void Apply(CapturedRig.Pose p,in ChipSelectInput input)
        {
            float w=MathUtil.Clamp(MathUtil.Finite(input.Weight)?input.Weight:0,0,1);Carry=NativeChipSelect.Carry(input.Spec);Left=NativeChipSelect.DisplayLeft(input.Spec);
            // A hit yields part of the look to the route's pose (the hit reaction under it), briefly.
            w*=1-NativeChipSelect.HitYield*MathUtil.Clamp(MathUtil.Finite(input.Flinch)?input.Flinch:0,0,1);
            bool taps=NativeChipSelect.Taps(Carry),first=input.FirstPerson;int cards=Math.Max(1,Math.Min(3,input.Cards));
            // First person a hand that carries a weapon stays down (it would fill the view): the display wrist flicks instead.
            var hands=NativeChipSelect.Hands(Carry);if(first&&hands==ChipSelectHands.Swipes)hands=ChipSelectHands.Flicks;taps=hands!=ChipSelectHands.Flicks;
            Gesture=NativeChipSelect.GestureAt(input.Clock,input.Seed,hands,cards);
            // Gestures wait for the wrist: one that started during the raise blends in with it.
            float ready=MathUtil.Smooth((w-.8f)/.2f)*(1-MathUtil.Clamp(MathUtil.Finite(input.Hush)?input.Hush:0,0,1));
            p.World();if(w<=1e-4f){raise.Reset();carry.Reset();tap.Reset();Outputs(p,first,input);return;}
            for(int i=0;i<Touched.Length;i++)rest[Touched[i]]=p.Rotation[Touched[i]];
            // A little bow and turn of the torso toward the display (not in first person: the camera is the head).
            if(!first)
            {
                p.Rotation[CapturedRig.Spine]=p.Rotation[CapturedRig.Spine]*CapturedRig.Euler(SpineBow*w);
                p.Rotation[CapturedRig.Chest]=p.Rotation[CapturedRig.Chest]*CapturedRig.Euler(ChestBow*w,(Left?ChestTurn:-ChestTurn)*w);p.World();
            }
            var chest=p.WorldPosition[CapturedRig.Chest];var chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            // ---- the display wrist ----
            int hand=Left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;
            Wrist(p,hand,chest,chestInv,out var restAt,out var restTurn);
            Vector3 goal,eye;
            if(first){goal=Vector3.Transform(input.ViewWrist-chest,chestInv);eye=Vector3.Transform(input.ViewEye-chest,chestInv);}
            else
            {
                goal=Mirror(Raised,!Left);eye=EyeChest;
                // Breathing in the hold (not with Reduced effects).
                if(!input.Reduced)goal+=new Vector3(0,.004f*(float)Math.Sin(input.Time*2.2f),.003f*(float)Math.Sin(input.Time*1.3f+.7f));
            }
            var pole=Vector3.Lerp(Mirror(RestPole,!Left),Mirror(RaisedPole,!Left),w);
            var travel=Vector3.Lerp(restAt,goal,w)+Vector3.UnitY*(Arch*(float)Math.Sin(Math.PI*w));
            if(Gesture.Kind==ChipSelectGestureKind.Tap)travel+=Vector3.UnitY*(-.012f*Gesture.Contact*ready);
            CapturedRig.Arm(p,Left,travel,restTurn,pole);p.World();
            // The back of the wrist to the eye, the fingers along the forearm.
            var fore=CapturedRig.Unit(Vector3.Transform(p.WorldPosition[hand]-p.WorldPosition[hand-1],chestInv),Mirror(-Vector3.UnitX,!Left));
            var y=-fore;var back=CapturedRig.Unit(eye-travel,Vector3.UnitY);back=CapturedRig.Unit(back-y*Vector3.Dot(back,y),Vector3.UnitY);var z=-back;
            var watch=CapturedRig.Basis(Vector3.Cross(y,z),y,z);
            // The flick: the display wrist rolls about the forearm and back.
            if(Gesture.Kind==ChipSelectGestureKind.Flick)watch=Quaternion.CreateFromAxisAngle(fore,(Left?-1:1)*FlickRoll*Gesture.Reach*ready)*watch;
            CapturedRig.Arm(p,Left,travel,raise.Blend(restTurn,watch,w),pole);
            Ease(p,arm,w);p.World();
            Outputs(p,first,input);
            // ---- the head (third person): the gaze down to the panel, following a tapping hand a little ----
            if(!first)
            {
                // From the eye (the head turns about the neck, the eyes do the rest): HeadShare of the angle down, capped; a damped yaw.
                var d=Vector3.Transform(PanelCentre-chest,chestInv)-EyeChest;float horizontal=(float)Math.Sqrt(d.X*d.X+d.Z*d.Z);
                float pitch=Math.Min(HeadMax,HeadShare*(float)Math.Atan2(-d.Y,Math.Max(.01f,horizontal))),yaw=YawShare*(float)Math.Atan2(d.X,Math.Max(.25f,d.Z));
                if(Gesture.Kind==ChipSelectGestureKind.Tap||Gesture.Kind==ChipSelectGestureKind.Swipe)yaw+=(Left?-.05f:.05f)*Gesture.Reach*ready;
                p.Rotation[CapturedRig.Neck]=Quaternion.Slerp(rest[CapturedRig.Neck],CapturedRig.Euler(pitch*.38f,yaw*.45f),w);
                p.Rotation[CapturedRig.Head]=Quaternion.Slerp(rest[CapturedRig.Head],CapturedRig.Euler(pitch*.62f,yaw*.55f),w);p.World();
                Outputs(p,false,input);
            }
            // ---- the other hand ----
            Other(p,input,w,ready,taps,chest,chestInv);
            p.World();
        }
        // The other hand: kept where its idle carries its weapon (staff, bow), lowered with its weapon, or (unarmed, gauntlets)
        // resting near the display; a tapper reaches out to tap or swipe the panel.
        private void Other(CapturedRig.Pose p,in ChipSelectInput input,float w,float ready,bool taps,Vector3 chest,Quaternion chestInv)
        {
            bool left=!Left,first=input.FirstPerson;int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;
            if(NativeChipSelect.KeepsIdle(Carry)&&!first)return;
            Wrist(p,hand,chest,chestInv,out var restAt,out var restTurn);
            // The carry's place rides the chest; its weapon axis is upright (the rig root's frame), so a crouched melee stance or a
            // bladed gun stance does not tip the weapon.
            Lowered(Carry,left,out var low,out var axis);axis=Vector3.Transform(axis,chestInv);
            // First person: the weapon goes down out of the view (and an empty hand rests just under it).
            if(first){low+=new Vector3(0,-.22f,-.1f);axis=Vector3.Transform(CapturedRig.Unit(Mirror(new Vector3(-.1f,-.75f,.65f),left),-Vector3.UnitY),chestInv);}
            // The weapon's axis is the hand's +z (blade, barrel, staff, arrow): turned from the idle's to the carry's, keeping its roll.
            var carryTurn=CapturedRig.FromTo(Vector3.Transform(Vector3.UnitZ,restTurn),axis)*restTurn;
            if(Carry==ChipSelectCarry.Free||Carry==ChipSelectCarry.Fist)carryTurn=Quaternion.Slerp(restTurn,carryTurn,.3f);
            var pole=Mirror(new Vector3(-1,-.7f,-.2f),left);
            var at=Vector3.Lerp(restAt,low,w)+Vector3.UnitY*(Arch*.6f*(float)Math.Sin(Math.PI*w));var turn=carry.Blend(restTurn,carryTurn,w);
            float reach=taps?Gesture.Reach*ready:0;
            if(reach>1e-4f)
            {
                // The tap: the tip onto the lit card (the swipe: across the panel, right to left as its owner reads it).
                var n=Vector3.Transform(PanelNormal,chestInv);var r=Vector3.Transform(PanelRight,chestInv);var u=Vector3.Transform(PanelUp,chestInv);var c=Vector3.Transform(PanelCentre-chest,chestInv);
                float half=(first?ViewWidth:PanelWidth)*.5f*PanelScale,tall=(first?ViewHeight:PanelHeight)*.5f*PanelScale;
                float across=Gesture.Kind==ChipSelectGestureKind.Swipe?.62f-1.24f*Gesture.Contact:CardOffset(Gesture.Card,Math.Max(1,Math.Min(3,input.Cards)));
                float press=Gesture.Kind==ChipSelectGestureKind.Tap?.012f*Gesture.Contact:Gesture.Contact>0&&Gesture.Contact<1?.004f:0;
                // A hand carrying a weapon sweeps along the panel's lower edge (the weapon below the cards); an empty one on the cards.
                bool armed=NativeChipSelect.Hands(Carry)==ChipSelectHands.Swipes;
                var touch=c+r*(across*half)+u*((armed?-.92f:.12f)*tall)+n*(.05f*(1-Math.Min(1,Gesture.Contact*4))+(armed?.012f:.006f)-press);
                // Fingers toward the display from the hand's own shoulder; the palm down, or a carried pistol or dagger kept pointing
                // down and out along its lowered axis (away from the panel and the face).
                var shoulder=Vector3.Transform(p.WorldPosition[arm]-chest,chestInv);var f=CapturedRig.Unit(touch-shoulder,-n);var ty=-f;
                var down=Carry==ChipSelectCarry.Free||Carry==ChipSelectCarry.Fist?-Vector3.UnitY:axis;var tz=CapturedRig.Unit(down-ty*Vector3.Dot(down,ty),n);var tapTurn=CapturedRig.Basis(Vector3.Cross(ty,tz),ty,tz);
                at=Vector3.Lerp(at,touch-Vector3.Transform(TipLocal,tapTurn),reach);turn=tap.Blend(turn,tapTurn,reach);
                pole=Vector3.Lerp(pole,Mirror(new Vector3(-1,-.9f,.15f),left),reach);
            }
            CapturedRig.Arm(p,left,at,turn,pole);
            Ease(p,arm,w);
            // The floor guard for a lowered blade or barrel (its stretched length), and for a dagger left in the display hand.
            if(!first&&Carry!=ChipSelectCarry.Free&&Carry!=ChipSelectCarry.Fist)KeepOffFloor(p,left,CarryLength(Carry,input.Spec,input.Reach));
            if(!first&&Carry==ChipSelectCarry.Daggers)KeepOffFloor(p,Left,CarryLength(Carry,input.Spec,input.Reach));
        }
        /// <summary>
        /// CapturedRig.BladeFloor's guard (a lowered blade's or barrel's tip kept over the floor) with a steady turn as the blade hangs
        /// plumb. The guard lifts the tip about the level axis across the blade, which is undefined for a plumb blade, so a long dagger
        /// hanging straight down (the dagger carry as the wrist rises) spun round from one frame to the next. Within Plumb of hanging
        /// straight down it turns instead about the hand's own across axis laid level, always the same way round; away from it the two
        /// agree on the original exactly.
        /// </summary>
        public static void KeepOffFloor(CapturedRig.Pose p,bool left,float length)
        {
            p.World();int h=left?CapturedRig.LHand:CapturedRig.RHand;const float floor=-.78f;
            var wrist=p.WorldPosition[h];var rot=p.WorldRotation[h];var dir=Vector3.Transform(Vector3.UnitZ,rot);
            if(!(length>1e-4f)||wrist.Y+dir.Y*length>=floor||wrist.Y<=floor)return;
            float want=MathUtil.Clamp((floor-wrist.Y)/length,-1,1);var swing=Vector3.Cross(dir,Vector3.UnitY);float level=swing.Length(),plumb=1-MathUtil.Smooth(level/Plumb);
            var world=rot;if(level>1e-6f)world=CapturedRig.Rot(swing/level,(float)(Math.Asin(want)-Math.Asin(MathUtil.Clamp(dir.Y,-1,1))))*rot;
            if(plumb>0)
            {
                // About the level across axis a the tip's height is dir.Y cos t + (a x dir).Y sin t: the turn that reaches the floor,
                // taken positive (one way round) and as small as it can be.
                var a=Vector3.Transform(Vector3.UnitX,rot);a.Y=0;a=CapturedRig.Unit(a,Vector3.UnitX);float A=dir.Y,B=Vector3.Cross(a,dir).Y,R=(float)Math.Sqrt(A*A+B*B);
                if(R>1e-5f)
                {
                    float phi=(float)Math.Atan2(B,A),c=(float)Math.Acos(MathUtil.Clamp(want/R,-1,1)),t1=Wrap(phi-c),t2=Wrap(phi+c);
                    float turn=t1>=0&&(t2<0||t1<=t2)?t1:t2>=0?t2:Math.Abs(t1)<Math.Abs(t2)?t1:t2;
                    world=Quaternion.Slerp(world,CapturedRig.Rot(a,turn)*rot,plumb);
                }
            }
            p.Rotation[h]=Quaternion.Inverse(p.WorldRotation[CapturedRig.Bones[h].Parent])*world;
        }
        /// <summary>The blade's level part (the sine of its tilt from plumb) under which the steady turn takes over.</summary>
        public const float Plumb=.35f;
        private static float Wrap(float a){while(a>Math.PI)a-=2*(float)Math.PI;while(a<-Math.PI)a+=2*(float)Math.PI;return a;
        }
        /// <summary>The length the floor guard keeps above the floor: the weapon's blade tip or muzzle (z in the hand frame).</summary>
        public static float CarryLength(ChipSelectCarry c,string spec,float reach)
        {
            var d=NativeWeaponModels.Get(spec);float z=d==null?1:!float.IsNaN(d.BladeTip.Z)?d.BladeTip.Z:!float.IsNaN(d.Muzzle.Z)?d.Muzzle.Z:1;
            return z*(reach>0&&reach<10&&(c==ChipSelectCarry.Blade||c==ChipSelectCarry.Greatsword||c==ChipSelectCarry.Daggers)?reach:1);
        }
        /// <summary>Where the weapon hand carries its weapon (chest frame; `left`: the weapon rides the left hand) and the weapon's
        /// axis there (the hand's +z; the rig root's upright frame): lowered (the greatsword trailing behind, tip down). Authored
        /// for the right hand; an empty hand rests near the display.</summary>
        public static void Lowered(ChipSelectCarry c,bool left,out Vector3 at,out Vector3 axis)
        {
            switch(c)
            {
                case ChipSelectCarry.Pistol:at=new Vector3(-.27f,-.3f,.1f);axis=new Vector3(-.12f,-.8f,.58f);break;
                case ChipSelectCarry.LongGun:at=new Vector3(-.25f,-.2f,.13f);axis=new Vector3(.2f,-.4f,.89f);break;
                case ChipSelectCarry.Blade:at=new Vector3(-.3f,-.26f,.1f);axis=new Vector3(-.42f,-.45f,.79f);break;
                case ChipSelectCarry.Daggers:at=new Vector3(-.28f,-.3f,.08f);axis=new Vector3(-.15f,-.85f,-.5f);break;
                case ChipSelectCarry.Greatsword:at=new Vector3(-.3f,-.22f,.02f);axis=new Vector3(-.28f,-.42f,-.86f);break;
                case ChipSelectCarry.Shield:at=new Vector3(-.33f,-.26f,.04f);axis=new Vector3(-.15f,-.9f,.4f);break;
                case ChipSelectCarry.Staff:at=new Vector3(-.25f,-.16f,.14f);axis=new Vector3(-.04f,.97f,.24f);break;
                case ChipSelectCarry.Bow:at=new Vector3(-.27f,-.3f,.08f);axis=new Vector3(-.1f,-.55f,.83f);break;
                default:at=new Vector3(-.13f,-.07f,.2f);axis=new Vector3(.2f,-.6f,.77f);break;
            }
            at=Mirror(at,left);axis=CapturedRig.Unit(Mirror(axis,left),Vector3.UnitZ);
        }
        /// <summary>A card's place across the panel (-1..1) for `count` cards (at most three show).</summary>
        public static float CardOffset(int card,int count)=>count<=1?0:count==2?(card==0?-.36f:.36f):(card-1)*.62f;
        // The arm's joints ease in from the route's pose over the first quarter of the weight, so the raise starts smoothly.
        private void Ease(CapturedRig.Pose p,int arm,float w)
        {
            float ease=MathUtil.Smooth(w/.25f);if(ease>=1)return;
            for(int i=0;i<3;i++){solved[i]=p.Rotation[arm+i];p.Rotation[arm+i]=Quaternion.Slerp(rest[arm+i],solved[i],ease);}
        }
        private static void Wrist(CapturedRig.Pose p,int hand,Vector3 chest,Quaternion chestInv,out Vector3 at,out Quaternion turn)
        {p.World();at=Vector3.Transform(p.WorldPosition[hand]-chest,chestInv);turn=chestInv*p.WorldRotation[hand];}
        // The display frame from the pose as it stands: the emitter on the back of the wrist cuff, the panel lifted off it along
        // the back of the wrist and turned to the eye, its across axis along the forearm (left to right as its owner reads it).
        private void Outputs(CapturedRig.Pose p,bool first,in ChipSelectInput input)
        {
            int hand=Left?CapturedRig.LHand:CapturedRig.RHand;var q=p.WorldRotation[hand];
            Emitter=p.WorldPosition[hand]+Vector3.Transform(EmitterLocal,q);Normal=CapturedRig.Unit(Vector3.Transform(-Vector3.UnitZ,q),Vector3.UnitY);
            Forearm=CapturedRig.Unit(p.WorldPosition[hand]-p.WorldPosition[hand-1],Vector3.UnitX);
            Eye=first?input.ViewEye:p.WorldPosition[CapturedRig.Head]+Vector3.Transform(new Vector3(0,.17f,.12f),p.WorldRotation[CapturedRig.Head]);
            var up=Vector3.Transform(Vector3.UnitY,p.WorldRotation[CapturedRig.Chest]);
            var forward=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.Chest]);
            // Third person it rises above the forearm (not toward the eyes, where the arms would hide it from everyone else).
            // First person it sits just over the wrist toward the top of the view (the camera's up is the eye's).
            PanelCentre=first?Emitter+Normal*.02f+CapturedRig.Unit(Vector3.Cross(Vector3.Cross(Eye-Emitter,up),Eye-Emitter),up)*ViewLift:Emitter+Normal*PanelLift+up*PanelRise+forward*PanelForward;PanelNormal=CapturedRig.Unit(Eye-PanelCentre,Normal);
            PanelScale=first?MathUtil.Clamp((Eye-PanelCentre).Length()/ViewDistance,.35f,1):1;
            // Third person the panel stands at least a little upright (its normal at most PanelSteep up), so the pilots round it
            // see its face, not its edge; the wearer reads it a little from above, as one reads a hologram.
            if(!first){float v=Vector3.Dot(PanelNormal,up);if(v>PanelSteep){var left=Vector3.Transform(Vector3.UnitX,p.WorldRotation[CapturedRig.Chest]);var flat=CapturedRig.Unit(-forward+left*(2*Vector3.Dot(Eye-PanelCentre,left)),-forward);PanelNormal=CapturedRig.Unit(flat*(float)Math.Sqrt(1-PanelSteep*PanelSteep)+up*PanelSteep,Normal);}}
            // The wearer's right is -x in the source frame: a left display's forearm points to it, a right display's away.
            var across=Left?Forearm:-Forearm;
            PanelRight=CapturedRig.Unit(across-PanelNormal*Vector3.Dot(across,PanelNormal),-Vector3.UnitX);
            PanelUp=CapturedRig.Unit(up-PanelNormal*Vector3.Dot(up,PanelNormal)-PanelRight*Vector3.Dot(up,PanelRight),Vector3.UnitY);
        }
        /// <summary>The outputs again from the pose as it stands (after a later layer moved the display wrist: the wrist plant's catch).</summary>
        public void Rebase(CapturedRig.Pose p,in ChipSelectInput input){p.World();Outputs(p,input.FirstPerson,input);}
        /// <summary>The other hand's tip (rig root frame) and its place against the panel, from the posed pose (World current).</summary>
        public void Measure(CapturedRig.Pose p,bool first)
        {
            int hand=Left?CapturedRig.RHand:CapturedRig.LHand;Tip=p.WorldPosition[hand]+Vector3.Transform(TipLocal,p.WorldRotation[hand]);
            var d=Tip-PanelCentre;TipDepth=Vector3.Dot(d,PanelNormal);TipU=Vector3.Dot(d,PanelRight)/((first?ViewWidth:PanelWidth)*.5f*PanelScale);TipV=Vector3.Dot(d,PanelUp)/((first?ViewHeight:PanelHeight)*.5f*PanelScale);
        }
    }

    /// <summary>The hologram's sink: one vertex (world space) with its colour (sRGB 0..1, alpha), the quad's uv, its kind and a
    /// parameter, and its HDR gain and seed; triangles by index.</summary>
    public interface INativeChipHoloSink
    {
        int Vertex(Vector3 at,Vector4 color,Vector4 uv,Vector4 extra);
        void Triangle(int a,int b,int c);
    }
    /// <summary>The look of one hologram on its frame (everything the builder animates from).</summary>
    public struct ChipHoloLook
    {
        /// <summary>Weight (the pose's), Age (s since this run opened), Redeal (s since a queued pick opened, +inf none), Time (the
        /// presentation clock: flicker and scan), the gesture of the pose, the offers (Count) and their rarity colours (sRGB hex
        /// for up to 12; the rainbow pick cycles hues), red (under attack, blinking unless Reduced), Reduced and Low tiers.</summary>
        public float Weight,Age,Redeal,Time;public ChipSelectGesture Gesture;public int Count,Seed;public bool Rainbow,Attacked,Reduced,Low,FirstPerson;
        public int C0,C1,C2;
        /// <summary>The wrist plant (Expedition, NativeChipPlantPose): the panel's Fold 0..1 (folded as the card leaves it), the other
        /// cards' Fade 0..1, the Chosen card (drawn as the chip instead), the chip (shown, its width in metres and its colour, sRGB hex)
        /// and the cuff's Pulse (0..1 while it runs, else negative).</summary>
        public bool Plant,ChipShown;public float Fold,Fade,ChipSize,Pulse;public int Chosen,ChipRgb;
    }
    public struct ChipHoloFrame
    {
        public Vector3 Emitter,Centre,Right,Up,Normal;public float Width,Height;
        /// <summary>The wrist plant's chip (its centre and its quad's axes) and the cuff's outward normal (the back of the wrist).</summary>
        public Vector3 Chip,ChipRight,ChipUp,CuffNormal;
    }

    /// <summary>
    /// The wrist hologram's geometry (world space, any axes): the projection cone from the emitter, the panel (a chamfered
    /// hardlight frame with corner brackets and a header line) and the offered chips as up to three small cards in their rarity
    /// colours, the lit card lifted toward the wearer; a ring where a tap lands and motes rising up the cone. Unity draws it with
    /// Resources/NativeChipSelect.shader (kinds below: the shader draws each kind's pattern, scanlines and flicker from the uv).
    /// Tiers: Full; Reduced (Reduced effects: no flicker, glitch, motes or scan travel, a steady red under attack); Low (low
    /// graphics: the panel and the cards, no cone, ring or motes). Allocation-free; Quads() is the quads a look builds.
    /// </summary>
    public static class NativeChipSelectHolo
    {
        public const float KindFrame=0,KindCard=1,KindBeam=2,KindMote=3,KindRing=4,KindLine=5;
        public const int Cyan=0x22e6ff,CyanSoft=0x7ff3ff,Danger=0xff4d6d,Ink=0xe8fbff;
        /// <summary>Electron hologram.ts HOLO: blinking red at 6 Hz while hit within the last 1.2 s.</summary>
        public const float BlinkHz=6,AttackedSeconds=1.2f;
        /// <summary>Electron hologram.ts holoBlinkRed: on for half of each 1/6 s; steady with Reduced effects.</summary>
        public static bool Red(bool attacked,float time,bool reduced)=>attacked&&(reduced||Math.Floor(time*BlinkHz*2)%2==0);
        /// <summary>The unfold 0..1 (Electron's grow (2 - grow) ease) once the weight passes HoloFrom, so it folds away first on the
        /// way out; and the cards' deal (each a little after the one before; a re-deal folds them and deals again).</summary>
        public static float Unfold(float weight)
        {
            float g=MathUtil.Clamp((weight-NativeChipSelect.HoloFrom)/(1-NativeChipSelect.HoloFrom),0,1);return g*(2-g);
        }
        public static float Deal(float unfold,float redeal,int card)
        {
            float d=MathUtil.Smooth((unfold-.35f-.12f*card)/.4f);
            if(redeal<NativeChipSelect.Redeal){float u=redeal/NativeChipSelect.Redeal;d*=u<.4f?1-MathUtil.Smooth(u/.4f):MathUtil.Smooth((u-.4f-.08f*card)/.4f);}
            return d;
        }
        /// <summary>Full-tier flicker (Electron .85 + .15 sin(37 t + seed)) with a short seeded glitch dip about every 2.7 s.</summary>
        public static float Flicker(float time,int seed,bool reduced)
        {
            if(reduced)return .93f;float f=.86f+.14f*(float)Math.Sin(time*37+seed%17);
            int k=(int)Math.Floor(time/2.7f);float at=k*2.7f+2.2f*NativeChipSelect.Hash(seed,k,9);if(time>=at&&time<at+.06f)f*=.35f;return f;
        }
        public static int Quads(in ChipHoloLook look)
        {
            int n=0;
            // A plant's panel counts only while it is still unfolded (it folds as the card leaves it); a plain look's always (its upper bound).
            if(!look.Plant||Unfold(look.Weight)*MathUtil.Clamp(look.Fold,0,1)>.002f)
            {
                int cards=Math.Max(1,Math.Min(3,look.Count)),shown=cards;
                if(look.Plant){shown=0;for(int i=0;i<cards;i++)if(i!=Chosen(look,cards)&&look.Fade>.01f)shown++;}
                n+=1+shown+1;// panel, cards, header line
                if(!look.Low)n+=2+2+1;// cone sheet (2), cone edges (2), emitter ring
                if(!look.Low&&!look.Reduced)n+=Motes;
                if(!look.Plant&&look.Gesture.Kind==ChipSelectGestureKind.Tap&&look.Gesture.SinceContact>=0&&look.Gesture.SinceContact<RingTime)n++;
            }
            if(PlantChip(look))n++;if(PlantPulse(look))n+=look.Low?1:2;
            return n;
        }
        // The wrist plant's parts: the chip while it shows, the cuff's pulse (a ring, and a flash but on the low tier) while it runs.
        private static bool PlantChip(in ChipHoloLook l)=>l.Plant&&l.ChipShown&&l.ChipSize>1e-4f&&MathUtil.Finite(l.ChipSize);
        private static bool PlantPulse(in ChipHoloLook l)=>l.Plant&&l.Pulse>=0&&l.Pulse<1;
        private static int Chosen(in ChipHoloLook l,int count)=>l.Chosen>=0?l.Chosen%count:-1;
        /// <summary>The cuff's pulse ring at its widest (m) and the chip cartridge's aspect (width over height, the shader's card).</summary>
        public const float PulseRadius=.075f,ChipAspect=.76f;
        public const int Motes=5;public const float RingTime=.32f;
        public static int Rgb(int index,in ChipHoloLook look,float time)
        {
            if(look.Rainbow){float h=(time*.25f+index*.31f)%1;return Hsv(h,.55f,1);}
            return index==0?look.C0:index==1?look.C1:look.C2;
        }
        public static int Hsv(float h,float s,float v)
        {
            h=(h%1+1)%1*6;int i=(int)h;float f=h-i,p=v*(1-s),q=v*(1-s*f),t=v*(1-s*(1-f));float r,g,b;
            switch(i){case 0:r=v;g=t;b=p;break;case 1:r=q;g=v;b=p;break;case 2:r=p;g=v;b=t;break;case 3:r=p;g=q;b=v;break;case 4:r=t;g=p;b=v;break;default:r=v;g=p;b=q;break;}
            return ((int)(r*255+.5f)<<16)|((int)(g*255+.5f)<<8)|(int)(b*255+.5f);
        }
        private static Vector4 Col(int hex,float a)=>new Vector4(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,a);
        private static void Quad(INativeChipHoloSink s,Vector3 c,Vector3 r,Vector3 u,Vector4 col,float kind,float param,float gain,float seed)
        {
            int a=s.Vertex(c-r-u,col,new Vector4(0,0,kind,param),new Vector4(gain,seed,0,0)),b=s.Vertex(c+r-u,col,new Vector4(1,0,kind,param),new Vector4(gain,seed,0,0));
            int d=s.Vertex(c+r+u,col,new Vector4(1,1,kind,param),new Vector4(gain,seed,0,0)),e=s.Vertex(c-r+u,col,new Vector4(0,1,kind,param),new Vector4(gain,seed,0,0));
            s.Triangle(a,b,d);s.Triangle(a,d,e);
        }
        // A quad from four corners (the cone's sheet and edges): uv x across, y from the emitter (0) to the panel (1).
        private static void Sheet(INativeChipHoloSink s,Vector3 a0,Vector3 a1,Vector3 b1,Vector3 b0,Vector4 col,float kind,float param,float gain,float seed)
        {
            int a=s.Vertex(a0,col,new Vector4(0,0,kind,param),new Vector4(gain,seed,0,0)),b=s.Vertex(a1,col,new Vector4(1,0,kind,param),new Vector4(gain,seed,0,0));
            int d=s.Vertex(b1,col,new Vector4(1,1,kind,param),new Vector4(gain,seed,0,0)),e=s.Vertex(b0,col,new Vector4(0,1,kind,param),new Vector4(gain,seed,0,0));
            s.Triangle(a,b,d);s.Triangle(a,d,e);
        }
        /// <summary>Builds one hologram into `s`. Returns the quads written (0 when it is folded away).</summary>
        public static int Build(INativeChipHoloSink s,in ChipHoloFrame f,in ChipHoloLook look)
        {
            float unfold=Unfold(look.Weight)*(look.Plant?MathUtil.Clamp(look.Fold,0,1):1);bool chip=PlantChip(look),pulse=PlantPulse(look);
            if(unfold<=.002f&&!chip&&!pulse)return 0;int quads=0;
            bool red=Red(look.Attacked,look.Time,look.Reduced);float flicker=Flicker(look.Time,look.Seed,look.Reduced),alpha=MathUtil.Clamp(unfold*1.4f,0,1)*flicker;
            int tone=red?Danger:Cyan,soft=red?0xff9aa8:CyanSoft;float seed=(look.Seed&1023)/1024f;
            if(unfold>.002f)
            {
                // The panel opens from a bright line across (its height first), Electron's grow ease.
                float w=f.Width*.5f*(.35f+.65f*unfold),h=f.Height*.5f*unfold;var r=f.Right*w;var u=f.Up*h;
                Quad(s,f.Centre,r,u,Col(tone,alpha),KindFrame,unfold,1.7f,seed);quads++;
                // The header line along the panel's top edge (the boot's bright line before the panel opens).
                Quad(s,f.Centre+f.Up*(h+f.Height*.04f),f.Right*w*.9f,f.Up*f.Height*.012f,Col(soft,alpha*(.6f+.4f*(1-unfold))),KindLine,0,2.2f,seed);quads++;
                if(!look.Low)
                {
                    // The projection cone: a sheet from the emitter to the panel's bottom edge, brightest at the wrist, and its two edges.
                    var bl=f.Centre-r-u;var br=f.Centre+r-u;var e0=f.Emitter-f.Right*.012f;var e1=f.Emitter+f.Right*.012f;
                    Sheet(s,e0,e1,br,bl,Col(tone,.55f*alpha),KindBeam,0,1.2f,seed);quads++;
                    var tl=f.Centre-r+u*.2f;var tr=f.Centre+r+u*.2f;Sheet(s,e0,e1,tr,tl,Col(tone,.22f*alpha),KindBeam,1,1,seed);quads++;
                    var side=Vector3.Cross(f.Normal,f.Up)*.0025f;
                    Sheet(s,f.Emitter-side,f.Emitter+side,bl+side,bl-side,Col(soft,.65f*alpha),KindLine,1,1.6f,seed);quads++;
                    Sheet(s,f.Emitter-side,f.Emitter+side,br+side,br-side,Col(soft,.65f*alpha),KindLine,1,1.6f,seed);quads++;
                    // The emitter ring on the cuff (in the panel's plane turned to the wrist).
                    Quad(s,f.Emitter+f.Normal*.002f,f.Right*.022f,Vector3.Normalize(Vector3.Cross(f.Right,f.Normal))*.022f,Col(soft,.9f*alpha),KindRing,.3f,2.4f,seed);quads++;
                    if(!look.Reduced)for(int i=0;i<Motes;i++)
                    {
                        // Motes rising up the cone, looping .9 s each, spread across it.
                        float k=(look.Time/.9f+i/(float)Motes+seed)%1,x=(NativeChipSelect.Hash(look.Seed,i,11)*2-1)*.8f;var at=Vector3.Lerp(f.Emitter,f.Centre-u+r*x,k);
                        Quad(s,at,f.Right*.006f,f.Up*.006f,Col(soft,alpha*(float)Math.Sin(Math.PI*k)),KindMote,0,2,seed);quads++;
                    }
                }
                // The cards: up to three in the panel, the lit one lifted toward the wearer, larger and brighter; a swipe slides them.
                int count=Math.Max(1,Math.Min(3,look.Count));int lit=look.Gesture.Card%count;float slide=look.Gesture.Kind==ChipSelectGestureKind.Swipe?look.Gesture.Contact:0;
                // A plant: the chosen card has left as the chip (drawn below); the others fade out.
                int chosen=look.Plant?Chosen(look,count):-1;float others=look.Plant?MathUtil.Clamp(look.Fade,0,1):1;
                int shownLit=look.Gesture.Kind==ChipSelectGestureKind.None||look.Gesture.SinceContact>=0?lit:look.Gesture.Was%count;
                float cw=f.Width*.21f,ch=f.Height*.48f;
                for(int i=0;i<count;i++)
                {
                    if(look.Plant&&(i==chosen||others<=.01f))continue;
                    float deal=Deal(unfold,look.Redeal,i);if(deal<=.01f)continue;float x=NativeChipSelectPose.CardOffset(i,count);
                    if(slide>0&&slide<1)x+=.12f*(float)Math.Sin(Math.PI*slide)*(i==shownLit?0:1);
                    bool on=i==shownLit;float lift=on?1:0,size=(on?1.14f:.9f)*(.6f+.4f*deal),bob=look.Reduced?0:.004f*(float)Math.Sin(look.Time*2.4f+i*1.9f);
                    var at=f.Centre+f.Right*(x*f.Width*.5f*(.35f+.65f*unfold))+f.Up*(f.Height*(.02f+.05f*lift)+bob)+f.Normal*(.006f+.014f*lift);
                    int rgb=red?Danger:Rgb(i,look,look.Time);float flash=look.Gesture.Kind==ChipSelectGestureKind.Tap&&on&&look.Gesture.SinceContact>=0&&look.Gesture.SinceContact<.25f?1-look.Gesture.SinceContact/.25f:0;
                    Quad(s,at,f.Right*(cw*.5f*size),f.Up*(ch*.5f*size*deal),Col(rgb,alpha*(on?1:.7f)*deal*others),KindCard,on?1:0,(on?1.5f:1.15f)+1.2f*flash,seed+i*.13f);quads++;
                }
                // A ring where a tap lands, opening and fading.
                if(!look.Plant&&look.Gesture.Kind==ChipSelectGestureKind.Tap&&look.Gesture.SinceContact>=0&&look.Gesture.SinceContact<RingTime)
                {
                    float k=look.Gesture.SinceContact/RingTime,rr=.012f+.05f*k;var at=f.Centre+f.Right*(NativeChipSelectPose.CardOffset(lit,count)*f.Width*.5f)+f.Up*(-.1f*f.Height*.5f)+f.Normal*.008f;
                    Quad(s,at,f.Right*rr,f.Up*rr,Col(soft,alpha*(1-k)*(1-k)),KindRing,.12f,2.2f,seed);quads++;
                }
            }
            // The wrist plant: the chosen chip in its rarity colour (a lit cartridge, steady: it is the chip, not the display), carried to
            // the cuff and pressed in; then the cuff's pulse, a ring opening over the back of the wrist and a flash fading from it.
            if(chip)
            {
                float half=look.ChipSize*.5f;Quad(s,f.Chip,f.ChipRight*half,f.ChipUp*(half/ChipAspect),Col(look.ChipRgb,1),KindCard,1,1.9f,seed+.5f);quads++;
            }
            if(pulse)
            {
                float k=look.Pulse,fall=(1-k)*(1-k);var n=f.CuffNormal.LengthSquared()>1e-8f?Vector3.Normalize(f.CuffNormal):f.Normal;
                var r=f.Right-n*Vector3.Dot(f.Right,n);r=r.LengthSquared()>1e-8f?Vector3.Normalize(r):Vector3.Normalize(Vector3.Cross(n,Vector3.UnitY+new Vector3(.01f,0,0)));var u=Vector3.Cross(n,r);
                float rr=.016f+(PulseRadius-.016f)*MathUtil.Smooth(k);Quad(s,f.Emitter+n*.003f,r*rr,u*rr,Col(look.ChipRgb,fall),KindRing,.18f,2.6f,seed);quads++;
                if(!look.Low){float fr=.03f*(1-.5f*k);Quad(s,f.Emitter+n*.004f,r*fr,u*fr,Col(look.ChipRgb,fall),KindMote,0,3f,seed);quads++;}
            }
            return quads;
        }
    }

    /// <summary>
    /// The "SELECTING CHIP" tag over a selecting pilot's head (screen space, drawn with the overhead bars): typed in from the
    /// open edge (a blinking block cursor while it types), then three dots lighting in turn; a soft glow breathing; danger red
    /// and blinking while the pilot is hit. Faded with distance (full to FadeFrom, gone at FadeTo, inside the names' 32 m) and
    /// occlusion like the overhead bars, never drawn for the local pilot in first person. Reduced effects: the whole text at
    /// once, still dots, no glow breathing, a steady red. Allocation-free: every prefix is a preallocated string.
    /// </summary>
    public static class NativeChipSelectTag
    {
        public const string Text="SELECTING CHIP";
        public static readonly string[] Typed=Prefixes();
        private static string[] Prefixes(){var a=new string[Text.Length+1];for(int i=0;i<=Text.Length;i++)a[i]=Text.Substring(0,i);return a;}
        /// <summary>Typing: starts Delay s after the edge at Rate characters a second; the cursor blinks at CursorHz while typing
        /// and for CursorHold after; the dots run DotPeriod per cycle.</summary>
        public const float Delay=.16f,Rate=28,CursorHz=3,CursorHold=.5f,DotPeriod=1.2f,FadeIn=.2f,FadeFrom=20,FadeTo=30,Size=10,Tracking=.14f;
        public static float TypedBy=>Delay+Text.Length/Rate;
        public static int Shown(float age,bool reduced)=>reduced?Text.Length:(int)MathUtil.Clamp((float)Math.Floor((age-Delay)*Rate),0,Text.Length);
        public static bool Cursor(float age,bool reduced)=>!reduced&&age>=Delay&&age<TypedBy+CursorHold&&Math.Floor(age*CursorHz*2)%2==0;
        /// <summary>Dot i (0..2) brightness: each lights in turn after the typing, a short glow fading; Reduced: all at .7.</summary>
        public static float Dot(int i,float age,bool reduced)
        {
            if(reduced)return .7f;float t=age-TypedBy;if(t<0)return 0;float k=(t/DotPeriod-i*.22f)%1;if(k<0)k+=1;return .3f+.7f*Math.Max(0,1-k/.45f);
        }
        public static float Glow(float time,bool reduced)=>reduced?.28f:.22f+.12f*(float)(.5+.5*Math.Sin(time*2*Math.PI/2.4f));
        public static float DistanceFade(float metres)=>!MathUtil.Finite(metres)?0:1-MathUtil.Smooth((metres-FadeFrom)/(FadeTo-FadeFrom));
        /// <summary>The tag's opacity: the look's weight (faded in over FadeIn of the run), the distance fade and how visible (0..1, the
        /// occlusion's smoothed state).</summary>
        public static float Alpha(float weight,float age,float metres,float visible)=>MathUtil.Clamp(weight,0,1)*MathUtil.Smooth(age/FadeIn)*DistanceFade(metres)*MathUtil.Clamp(visible,0,1);
        /// <summary>The occlusion's smoothing: toward 1 (seen) or 0 (hidden) over .15 s, so a pillar edge does not pop the tag.</summary>
        public static float Visible(float current,bool seen,float dt)=>MathUtil.Approach(current,seen?1:0,Math.Max(0,dt)/.15f);
    }
}
