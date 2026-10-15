using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition chip pick's wrist plant on the live rig (owner 2026-10-15: "Can we change the chip selection to just pressing C
    // and it'll be us in a "Chip Selection" animation? I purposely wanted to make people vulnerable when doing this."; "(Like we had
    // for ward zero, but can keep the open view. I just want players to stay in place while doing this.)"; "Still make them put their
    // chip in their wrist after selected, then they are free to move."). The authority holds the chooser (PR #237) until the final
    // Install plus NativeDraftPlant.ReturnEnd; this is what the pilot does meanwhile, on the Boss Test plant's own timeline
    // (NativeDraftPlant: the catch at Contact, the carry, the seat and the install cue at SoundAt, the sink by PlantEnd, the pulse,
    // the return by ReturnEnd), laid over the wrist-hologram pose (NativeChipSelectPose) that is already up:
    //  - The chosen card lifts off the panel toward the wearer, the other cards fade and the panel folds away behind it.
    //  - A hand that already reaches the panel (empty hands, the gauntlets, a pistol or a dagger; third person only) takes the card
    //    at the contact (it is the chip from then on: a lit cartridge in its rarity colour), carries it over a small
    //    arc to the cuff on the back of the display wrist and presses it in (palm down, or a carried weapon kept pointing down).
    //  - A long weapon stays lowered (and first person, every class): the chip drops down the projection cone into the cuff and the
    //    display wrist rises a little to take it, then gives as it seats.
    //  - The cuff pulses in the chip's colour from the seat; then both arms return: to the route's pose, or, when another pick
    //    follows (Stay), back to the hologram pose with the panel unfolding again.
    // Presentation only: nothing here reads or writes the run, the hold, the input or the network. Allocation-free.

    /// <summary>A plant to pose: its age (seconds since the pick, the plant's clock; negative or past Span: none), the card it took
    /// (-1: the lit one), the chip (its id; the colour comes from its rarity), whether another pick follows it (Stay) and a key that
    /// changes with each plant.</summary>
    public struct ChipPlantCue
    {
        public float Age;public int Chosen,Key;public string Chip;public bool Stay;
        public bool Active=>NativeChipPlant.Active(Age);
        public static ChipPlantCue None=>new ChipPlantCue{Age=-1,Chosen=-1};
    }

    public static class NativeChipPlant
    {
        /// <summary>The plant's length (s after the pick): the authority's hold after the final pick ends here too
        /// (NativeExpedition.ChoicePlantSeconds, the run's Install time plus NativeDraftPlant.ReturnEnd).</summary>
        public const float Span=NativeDraftPlant.ReturnEnd;
        /// <summary>The hand leaves for the card at ReachStart (the contact is NativeDraftPlant.Contact); the card lifts over LiftSpan;
        /// the panel folds from FoldFrom to FoldTo; the other cards fade by FadeTo; the gestures hush over HushSpan.</summary>
        public const float ReachStart=.08f,LiftSpan=.25f,FoldFrom=.3f,FoldTo=.75f,FadeTo=.3f,HushSpan=.15f;
        /// <summary>The chip's width in the hand (m: the lit card shrinks to it over the carry), the carry's arc off the wrist, the press
        /// into the cuff, the long-weapon wrist's rise to take the dropping chip and its give at the seat.</summary>
        public const float ChipWidth=.045f,Arc=.06f,Press=.012f,Catch=.02f,Give=.012f;
        /// <summary>After a plant that did not stay, the look waits Settle seconds before it rises again for another pilot's queued
        /// pick (the authority's flag can still read choosing for a moment after the final plant).</summary>
        public const float Settle=.5f;
        public static bool Active(float age)=>MathUtil.Finite(age)&&age>=0&&age<Span;
        public static float Reach(float age)=>MathUtil.Smooth((age-ReachStart)/(NativeDraftPlant.Contact-ReachStart));
        public static float Lift(float age)=>MathUtil.Smooth(age/LiftSpan);
        public static float Hush(float age)=>MathUtil.Smooth(age/HushSpan);
        public static float Fade(float age)=>1-MathUtil.Smooth(age/FadeTo);
        /// <summary>The panel 1 open .. 0 folded: it folds as the card leaves it, and unfolds over the return when another pick follows.</summary>
        public static float Fold(float age,bool stay){float f=1-MathUtil.Smooth((age-FoldFrom)/(FoldTo-FoldFrom));return stay?Math.Max(f,NativeDraftPlant.Return(age)):f;}
        /// <summary>The look's weight under the plant: held up until the return, then down with it (or kept up when another pick follows).</summary>
        public static float Weight(float look,float age,bool stay)=>stay?look:look*(1-NativeDraftPlant.Return(age));
        /// <summary>Whether a hand carries the chip (third person, a hand that reaches the panel already: taps or swipes), else the display
        /// wrist takes it as it drops. First person the wrist always takes it: a hand crossing the view to the wrist beside the tray covered
        /// the chip and most of the screen.</summary>
        public static bool Hand(ChipSelectCarry c,bool first)=>!first&&NativeChipSelect.Hands(c)!=ChipSelectHands.Flicks;
    }

    /// <summary>One plant pose: the cue's age, the look's weight (as given to NativeChipSelectPose), the hit flinch, the card taken
    /// (-1: the lit one), the cards shown, first person, and Stay.</summary>
    public struct ChipPlantInput{public float Age,Weight,Flinch;public int Chosen,Cards;public bool FirstPerson,Stay;}

    /// <summary>
    /// The plant layer, after NativeChipSelectPose.Apply on the same pose (rig root frame, source axes): moves the hand that carries
    /// the chip (or, for a long weapon, the display wrist) and returns where the chip is, the panel's fold, the other cards' fade and
    /// the cuff's pulse for the hologram (NativeChipSelectHolo with ChipHoloLook.Plant). Arms only; the legs, pelvis, body root, spine,
    /// chest and head keep what the route and the hologram pose gave them. Allocation-free.
    /// </summary>
    public sealed class NativeChipPlantPose
    {
        /// <summary>The chip (centre, quad axes, width), whether it shows; the cuff (on the back of the display wrist) and its normal.</summary>
        public Vector3 Chip,ChipRight=Vector3.UnitX,ChipUp=Vector3.UnitY,Cuff,CuffNormal=Vector3.UnitY;public float ChipSize;public bool ChipShown;
        /// <summary>The panel's fold, the other cards' fade, the cuff's pulse (0..1 while it runs, else -1), the card taken, and
        /// whether a hand carries the chip.</summary>
        public float Fold=1,Fade=1,Pulse=-1;public int Card;public bool Hand;
        private readonly Quaternion[] keep=new Quaternion[3],solved=new Quaternion[3];
        // The carrying hand's turn blends (to the card, to the cuff, the return, the weight), kept round the same way frame to frame.
        private ChipTurnBlend toCard,toCuff,back,weigh;
        /// <summary>A new plant, or none this frame: its turn blends start again on the shorter way.</summary>
        public void Reset(){toCard.Reset();toCuff.Reset();back.Reset();weigh.Reset();}

        public void Apply(CapturedRig.Pose p,in ChipPlantInput input,NativeChipSelectPose sel,in ChipSelectInput look)
        {
            float age=MathUtil.Finite(input.Age)?Math.Max(0,input.Age):0;bool first=input.FirstPerson;int cards=Math.Max(1,Math.Min(3,input.Cards));
            Card=input.Chosen>=0?input.Chosen%cards:Math.Max(0,sel.Gesture.Card)%cards;Hand=NativeChipPlant.Hand(sel.Carry,first);
            Fold=NativeChipPlant.Fold(age,input.Stay);Fade=NativeChipPlant.Fade(age);Pulse=NativeDraftPlant.Pulse(age);
            float contact=NativeDraftPlant.Contact,carry=NativeDraftPlant.Carry(age),seat=NativeDraftPlant.Seat(age),sink=NativeDraftPlant.Sink(age),ret=NativeDraftPlant.Return(age);
            // The plant's authority over the arms: the look's weight, yielding to a hit as the hologram pose does.
            float k=MathUtil.Clamp(MathUtil.Finite(input.Weight)?input.Weight:0,0,1)*(1-NativeChipSelect.HitYield*MathUtil.Clamp(MathUtil.Finite(input.Flinch)?input.Flinch:0,0,1));
            p.World();
            // The chosen card on the panel, where the hologram lays its lit card, lifting toward the wearer.
            float half=(first?NativeChipSelectPose.ViewWidth:NativeChipSelectPose.PanelWidth)*.5f*sel.PanelScale,tall=(first?NativeChipSelectPose.ViewHeight:NativeChipSelectPose.PanelHeight)*.5f*sel.PanelScale;
            var card=sel.PanelCentre+sel.PanelRight*(NativeChipSelectPose.CardOffset(Card,cards)*half)+sel.PanelUp*(tall*.14f)+sel.PanelNormal*(.02f+.03f*NativeChipPlant.Lift(age));
            float cardWidth=half*2*.21f*1.14f;
            Cuff=sel.Emitter+sel.Normal*.004f;CuffNormal=sel.Normal;
            var chest=p.WorldPosition[CapturedRig.Chest];var chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            var n=Vector3.Transform(sel.Normal,chestInv);
            if(Hand&&k>1e-4f)
            {
                // ---- a hand takes the card and presses the chip into the cuff ----
                bool left=!sel.Left;int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;
                var fromAt=Vector3.Transform(p.WorldPosition[hand]-chest,chestInv);var fromTurn=chestInv*p.WorldRotation[hand];
                var shoulder=Vector3.Transform(p.WorldPosition[arm]-chest,chestInv);bool armed=NativeChipSelect.Hands(sel.Carry)==ChipSelectHands.Swipes;
                NativeChipSelectPose.Lowered(sel.Carry,left,out _,out var axis);axis=Vector3.Transform(axis,chestInv);
                var cardAt=Vector3.Transform(card-chest,chestInv);var pressAt=Vector3.Transform(Cuff-chest,chestInv)+n*(.004f-NativeChipPlant.Press*sink);
                float reach=NativeChipPlant.Reach(age);
                var tip=age<contact?cardAt:Vector3.Lerp(cardAt,pressAt,carry)+n*(NativeChipPlant.Arc*(float)Math.Sin(Math.PI*carry));
                // Fingers toward the target from the hand's own shoulder; the palm down onto the card, then onto the wrist (-n), or a
                // carried pistol or dagger kept pointing down along its lowered axis.
                var pn=Vector3.Transform(sel.PanelNormal,chestInv);
                var pinch=Turn(cardAt,shoulder,armed?axis:-Vector3.UnitY,-pn);var press=Turn(pressAt,shoulder,armed?axis:-n,-n);
                var turn=age<contact?toCard.Blend(fromTurn,pinch,reach):toCuff.Blend(pinch,press,carry);
                var at=tip-Vector3.Transform(NativeChipSelectPose.TipLocal,turn);if(age<contact)at=Vector3.Lerp(fromAt,at,reach);
                // The return: back to where the hologram pose holds the hand (the route's own once the look is down).
                at=Vector3.Lerp(at,fromAt,ret);turn=back.Blend(turn,fromTurn,ret);
                at=Vector3.Lerp(fromAt,at,k);turn=weigh.Blend(fromTurn,turn,k);
                var pole=Vector3.Lerp(Mirror(new Vector3(-1,-.7f,-.2f),left),Mirror(new Vector3(-1,-.9f,.15f),left),reach*(1-ret));
                for(int i=0;i<3;i++)keep[i]=p.Rotation[arm+i];
                CapturedRig.Arm(p,left,at,turn,pole);
                // The joints ease from the hologram pose's as the reach starts and back onto it over the return, so nothing pops.
                float ease=MathUtil.Smooth(Math.Min(reach,1-ret)/.25f)*Math.Min(1,k*4);
                if(ease<1)for(int i=0;i<3;i++){solved[i]=p.Rotation[arm+i];p.Rotation[arm+i]=Quaternion.Slerp(keep[i],solved[i],ease);}
                p.World();
                if(!first&&armed)NativeChipSelectPose.KeepOffFloor(p,left,NativeChipSelectPose.CarryLength(sel.Carry,look.Spec,look.Reach));
                p.World();
                // The chip: the card until the contact, then between the fingers, settling flat onto the cuff as it is pressed in.
                if(age<contact){Chip=card;ChipRight=sel.PanelRight;ChipUp=sel.PanelUp;ChipSize=cardWidth;}
                else
                {
                    var q=p.WorldRotation[hand];var held=p.WorldPosition[hand]+Vector3.Transform(NativeChipSelectPose.TipLocal,q);
                    Chip=Vector3.Lerp(held,Cuff,seat)-sel.Normal*(.01f*sink);Lie(sel,carry);ChipSize=cardWidth+(NativeChipPlant.ChipWidth-cardWidth)*carry;
                }
            }
            else
            {
                // ---- a long weapon stays lowered: the chip drops down the cone into the cuff; the wrist rises to it and gives ----
                if(k>1e-4f&&age>=contact&&sink<1)
                {
                    bool left=sel.Left;int hand=left?CapturedRig.LHand:CapturedRig.RHand,arm=hand-2;
                    var at=Vector3.Transform(p.WorldPosition[hand]-chest,chestInv);var turn=chestInv*p.WorldRotation[hand];
                    float lift=NativeChipPlant.Catch*(float)Math.Sin(Math.PI*carry)-NativeChipPlant.Give*(float)Math.Sin(Math.PI*sink);
                    var pole=Vector3.Lerp(Mirror(NativeChipSelectPose.RestPole,!left),Mirror(NativeChipSelectPose.RaisedPole,!left),MathUtil.Clamp(look.Weight,0,1));
                    for(int i=0;i<3;i++)keep[i]=p.Rotation[arm+i];
                    CapturedRig.Arm(p,left,at+n*(lift*k),turn,pole);
                    // The same target, turn and pole as the hologram pose solves, raised a little: eased so the solve cannot pop.
                    float ease=MathUtil.Smooth(Math.Min(carry,1-sink)*4);
                    if(ease<1)for(int i=0;i<3;i++){solved[i]=p.Rotation[arm+i];p.Rotation[arm+i]=Quaternion.Slerp(keep[i],solved[i],ease);}
                    p.World();sel.Rebase(p,look);Cuff=sel.Emitter+sel.Normal*.004f;CuffNormal=sel.Normal;
                }
                if(age<contact){Chip=card;ChipRight=sel.PanelRight;ChipUp=sel.PanelUp;ChipSize=cardWidth;}
                else{Chip=Vector3.Lerp(card,Cuff,carry)+sel.PanelNormal*(.02f*(float)Math.Sin(Math.PI*carry))-sel.Normal*(.01f*sink);Lie(sel,carry);ChipSize=cardWidth+(NativeChipPlant.ChipWidth-cardWidth)*carry;}
            }
            ChipShown=sink<.999f&&k>1e-3f;
        }
        // The chip's quad turns from the panel's plane (facing the wearer) to lie flat on the back of the wrist along the forearm.
        private void Lie(NativeChipSelectPose sel,float t)
        {
            var flatUp=CapturedRig.Unit(sel.Forearm-sel.Normal*Vector3.Dot(sel.Forearm,sel.Normal),sel.PanelUp);if(!sel.Left)flatUp=-flatUp;
            var flatRight=CapturedRig.Unit(Vector3.Cross(flatUp,sel.Normal),sel.PanelRight);
            var r=Vector3.Lerp(sel.PanelRight,flatRight,t);ChipRight=CapturedRig.Unit(r,sel.PanelRight);
            var u=Vector3.Lerp(sel.PanelUp,flatUp,t);u-=ChipRight*Vector3.Dot(u,ChipRight);ChipUp=CapturedRig.Unit(u,sel.PanelUp);
        }
        // A hand's turn with its fingers from `shoulder` toward `target` and its +z (the palm, or a carried weapon's axis) along `face`;
        // as `face` comes into line with the fingers, `second` (toward the surface touched) takes over smoothly, so the turn never flips.
        private static Quaternion Turn(Vector3 target,Vector3 shoulder,Vector3 face,Vector3 second)
        {
            var f=CapturedRig.Unit(target-shoulder,Vector3.UnitZ);var y=-f;var z=face-y*Vector3.Dot(face,y);float across=z.Length();
            if(across<Aligned){var alt=second-y*Vector3.Dot(second,y);z+=alt*((Aligned-across)/Aligned);}
            z=CapturedRig.Unit(z,Vector3.UnitZ);return CapturedRig.Basis(Vector3.Cross(y,z),y,z);
        }
        private const float Aligned=.5f;
        private static Vector3 Mirror(Vector3 v,bool flip)=>flip?new Vector3(-v.X,v.Y,v.Z):v;
    }
}
