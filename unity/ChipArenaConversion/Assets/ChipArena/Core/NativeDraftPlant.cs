using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The wrist plant on the draft stage (owner request, after the palm contact): the other forearm rises in
    // front of the chest with the inner wrist turned up toward the camera, the hand that took the chip carries it there over a
    // small arc and presses it into the wrist cuff, a short rarity pulse runs from the plant, then both arms settle back to
    // their rest (on the draft table, NativeDraftStage). The details spotlight follows the pulse; the install cue lands on the plant (NativeInstallCue). Times are
    // seconds after the authoritative pick (the source grab's .48 s contact slowed to .62 s and the plant with it, owner
    // 2026-10-10: smooth and unhurried; NativeDraftStage runs the reach), positions the stage rig's root frame in source axes.
    // Presentation only.
    public sealed class NativeDraftPlant
    {
        public const float Contact=.62f,RaiseStart=.40f,RaiseEnd=.94f,CarryEnd=1.14f,SeatSpan=.24f,PlantEnd=1.32f,PulseStart=1.14f,PulseEnd=1.84f,ReturnStart=1.36f,ReturnEnd=1.95f,SoundAt=1.14f,DetailsAt=1.5f;
        public static float Raise(float age)=>MathUtil.Smooth((age-RaiseStart)/(RaiseEnd-RaiseStart))*(1-Return(age));
        public static float Carry(float age)=>MathUtil.Smooth((age-Contact)/(CarryEnd-Contact));
        public static float Seat(float age)=>MathUtil.Smooth((age-(CarryEnd-SeatSpan))/SeatSpan);
        public static float Sink(float age)=>MathUtil.Smooth((age-CarryEnd)/(PlantEnd-CarryEnd));
        public static float Return(float age)=>MathUtil.Smooth((age-ReturnStart)/(ReturnEnd-ReturnStart));
        // 0..1 while the plant pulse runs, else -1.
        public static float Pulse(float age)=>age<PulseStart||age>=PulseEnd?-1:(age-PulseStart)/(PulseEnd-PulseStart);
        // draft-stage.ts chipModel: the chip closed in the hand from the contact is the source's .17 x .12 m processor package,
        // popping from half size over .12 s and breathing by .08 (not under reduced motion). The pilot's palm is .145 m wide
        // (CapturedRig hand box), so the held chip is capped at .13 m and even at the top of its breath stays inside the hand
        // (owner: never bigger than the hand). Unity draws it with the PvE pickup model (chip-model.ts, a .8 m package), so the
        // scale carries .13/.8; it sinks to nothing as it is pressed into the wrist.
        public const float PickupWidth=.8f,SourceChipWidth=.17f,PalmWidth=.145f,HeldChipWidth=.13f,Breath=.08f;
        public static float HeldChipScale(float age,float time,bool reduced)=>HeldChipWidth/PickupWidth*(.5f+.5f*Math.Min(1,Math.Max(0,(age-Contact)/.12f))+(reduced?0:Breath*(float)Math.Sin(time*5)))*(1-Sink(age));
        // The palm socket (draft-stage.ts PALM, hand frame) and the chip's turn in a hand (its +y along the hand's +z, the palm side).
        public static readonly Vector3 PalmLocal=new Vector3(0,-.035f,.04f);
        public static readonly Quaternion ChipInHand=CapturedRig.Rot(CapturedRig.X,(float)Math.PI/2);
        // The presented wrist (chest frame): in front of the chest just past the midline, within the carrying arm's reach, high
        // enough to clear the draft table the pilot bows over. Both arms travel to and from their rest (the table, or the idle)
        // over a small Arch (chest up), so no joint blend dips a hand through the table.
        public static Vector3 Raised(bool left)=>new Vector3(left?-.02f:.02f,.13f,.26f);
        public const float Arch=.06f;
        // Where the chip goes in: the palm side of the wrist cuff (hand frame).
        public static readonly Vector3 CuffLocal=new Vector3(0,.005f,.105f);
        // The tray's reaching wrist turn at the contact (NativeDraftStage), the carry's starting turn.
        public static readonly Quaternion ContactWrist=CapturedRig.Euler(-1.45f);
        // A hand turning between palm down and palm up (half a turn, which a plain slerp may take either way, flipping as the
        // body moves) goes through the palm facing the midline, fingers forward (chest frame), a quarter turn each way.
        public static Quaternion PalmIn(bool left)=>left?CapturedRig.Basis(Vector3.UnitY,-Vector3.UnitZ,-Vector3.UnitX):CapturedRig.Basis(-Vector3.UnitY,-Vector3.UnitZ,Vector3.UnitX);
        public static Quaternion Roll(Quaternion from,Quaternion to,bool left,float t)
        {t=MathUtil.Clamp(t,0,1);var via=PalmIn(left);return t<.5f?Quaternion.Slerp(from,via,t*2):Quaternion.Slerp(via,to,t*2-1);}

        // Outputs (root frame): the chip, its turn and whether it shows; the plant point and the wrist's outward normal.
        public Vector3 Chip,At,Normal=Vector3.UnitZ;public Quaternion ChipRotation=Quaternion.Identity;public bool ChipShown;
        private readonly Quaternion[] rest=new Quaternion[3],solved=new Quaternion[3];
        // After the tray reach: `left` the arm that took the chip, `reach` its wrist goal at the contact (chest frame, as the
        // tray solves it), `age` seconds after the pick. Raises the other wrist, then (from the contact) carries and plants the
        // chip with the reaching arm, blending both arms back to their rest (the pose as given) over the return. Before the contact only the other
        // wrist rises (the tray solves the reach). World() is current on return. Allocation-free.
        public void Pose(CapturedRig.Pose p,bool left,Vector3 reach,float age)
        {
            p.World();Present(p,!left,Raise(age));Place(p,left,age);if(age<Contact)return;
            int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;
            var chest=p.WorldPosition[CapturedRig.Chest];var chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            var plant=Vector3.Transform(At-chest,chestInv);var n=Vector3.Transform(Normal,chestInv);var shoulder=Vector3.Transform(p.WorldPosition[arm]-chest,chestInv);
            // The pressing hand: palm onto the wrist, fingers along the line from its own shoulder.
            var f=plant-shoulder;f=CapturedRig.Unit(f-n*Vector3.Dot(f,n),new Vector3(left?-1:1,0,0));var y=-f;var z=-n;var press=CapturedRig.Basis(Vector3.Cross(y,z),y,z);
            float carry=Carry(age),sink=Sink(age),ret=Return(age);
            var goal=plant+n*(.004f-.012f*sink)-Vector3.Transform(PalmLocal,press);
            goal=Vector3.Lerp(reach,goal,carry)+n*(.07f*(float)Math.Sin(Math.PI*carry));var wrist=Roll(ContactWrist,press,left,carry);
            // Keep the resting arm and carry the wrist back to it over the return along an arch (no switch between IK and the
            // rest), the joints settling exactly onto the rest over the return's last quarter.
            for(int i=0;i<3;i++)rest[i]=p.Rotation[arm+i];
            if(ret>0){Wrist(p,hand,chest,chestInv,out var restAt,out var restTurn);goal=Vector3.Lerp(goal,restAt,ret)+Vector3.UnitY*(Arch*(float)Math.Sin(Math.PI*ret));wrist=Quaternion.Slerp(wrist,restTurn,ret);}
            var bend=Vector3.Lerp(new Vector3(1,-.8f,-.25f),NativeDraftStage.RestPole,ret);if(!left)bend.X=-bend.X;
            CapturedRig.Arm(p,left,goal,wrist,bend);
            float settle=MathUtil.Smooth((ret-.75f)/.25f);if(settle>0)for(int i=0;i<3;i++){solved[i]=p.Rotation[arm+i];p.Rotation[arm+i]=Quaternion.Slerp(solved[i],rest[i],settle);}
            p.World();Place(p,left,age);
        }
        // The outputs from the pose as it stands (call again after anything turns the chest, such as the look): the plant
        // point on the other wrist, its normal and forearm axis; the chip riding the palm, turning to lie on the wrist over
        // the last part of the carry, then sinking into the cuff.
        public Vector3 Forearm=Vector3.UnitX;
        public void Place(CapturedRig.Pose p,bool left,float age)
        {
            int other=left?CapturedRig.RHand:CapturedRig.LHand,hand=left?CapturedRig.LHand:CapturedRig.RHand;
            At=p.WorldPosition[other]+Vector3.Transform(CuffLocal,p.WorldRotation[other]);Normal=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[other]);
            Forearm=CapturedRig.Unit(p.WorldPosition[other]-p.WorldPosition[other-1],Vector3.UnitX);
            float sink=Sink(age),seat=Seat(age);ChipShown=age>=Contact&&sink<.999f;if(age<Contact){Chip=At;ChipRotation=Quaternion.Identity;return;}
            var held=p.WorldPosition[hand]+Vector3.Transform(PalmLocal,p.WorldRotation[hand]);var heldRot=p.WorldRotation[hand]*ChipInHand;
            Chip=Vector3.Lerp(held,At,sink>0?1:seat)-Normal*(.012f*sink);ChipRotation=Quaternion.Slerp(heldRot,p.WorldRotation[other]*ChipInHand,seat);
        }
        // The receiving arm raised by `k` (0 rest .. 1 presented): forearm across the front of the chest, the hand along it
        // with the inner wrist (+z) turned up and toward the camera. The wrist travels from its rest over the arch, the joints
        // easing in from the rest over the first quarter.
        private void Present(CapturedRig.Pose p,bool left,float k)
        {
            if(k<=1e-3f)return;int h=left?CapturedRig.LHand:CapturedRig.RHand,arm=h-2;var chest=p.WorldPosition[CapturedRig.Chest];var chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            for(int i=0;i<3;i++)rest[i]=p.Rotation[arm+i];Wrist(p,h,chest,chestInv,out var restAt,out var restTurn);
            // The presented turn from the presented arm (elbow down and out); the travel bends the elbow from the rest's toward it.
            var goal=Raised(left);var presented=new Vector3(left?1:-1,-.9f,-.1f);var pole=Vector3.Lerp(NativeDraftStage.RestPole,new Vector3(1,-.9f,-.1f),k);if(!left)pole.X=-pole.X;
            CapturedRig.Arm(p,left,goal,restTurn,presented);p.World();
            var fore=CapturedRig.Unit(Vector3.Transform(p.WorldPosition[h]-p.WorldPosition[h-1],chestInv),new Vector3(left?-1:1,0,0));
            var up=new Vector3(0,.6f,.8f);up=CapturedRig.Unit(up-fore*Vector3.Dot(up,fore),Vector3.UnitY);var y=-fore;
            CapturedRig.Arm(p,left,Vector3.Lerp(restAt,goal,k)+Vector3.UnitY*(Arch*(float)Math.Sin(Math.PI*k)),Quaternion.Slerp(restTurn,CapturedRig.Basis(Vector3.Cross(y,up),y,up),k),pole);
            float ease=MathUtil.Smooth(k/.25f);if(ease<1)for(int i=0;i<3;i++){solved[i]=p.Rotation[arm+i];p.Rotation[arm+i]=Quaternion.Slerp(rest[i],solved[i],ease);}
            p.World();
        }
        // A hand's wrist position and turn in the chest frame, from the pose's current world transforms.
        private static void Wrist(CapturedRig.Pose p,int hand,Vector3 chest,Quaternion chestInv,out Vector3 at,out Quaternion turn)
        {p.World();at=Vector3.Transform(p.WorldPosition[hand]-chest,chestInv);turn=chestInv*p.WorldRotation[hand];}
    }
}
