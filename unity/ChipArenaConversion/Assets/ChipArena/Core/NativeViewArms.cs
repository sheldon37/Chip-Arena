using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One class's first-person hold for the eye-anchored arms (NativeViewArms.Holds). Camera frame, metres: x right, y
    /// up, z ahead.</summary>
    public sealed class NativeViewArmsHold
    {
        public string Spec;
        /// <summary>The class's hold moved into view: the old view model's whole rig (its weapon, hands and effects), at the old
        /// view's scale.</summary>
        public Vector3 Lift;
        /// <summary>More of it while a bow is lowered (times 1 - its Raised: the carry held under the eye comes into view, the draw
        /// stays on the arrow line).</summary>
        public Vector3 CarryLift;
        /// <summary>The shoulders' midpoint in the eye frame at true size.</summary>
        public Vector3 Shoulder;
        /// <summary>Each elbow's pole in the eye frame at true size.</summary>
        public Vector3 PoleRight,PoleLeft;
        /// <summary>The hand that holds nothing (-1 the left, 1 the right, 0 none) and where it goes while it hangs low (eye frame,
        /// m, added to its place by TuckWeight; a bow's string hand by 1 - Raised): out of view, so no lone glove floats at the
        /// edge, back in as it rises into a cast or a draw.</summary>
        public int Free;public Vector3 Tuck;
    }

    /// <summary>The eye-anchored arms' look, for every class (NativeViewArms.Look).</summary>
    public sealed class NativeViewArmsLook
    {
        /// <summary>The view body's near-black cloth lifted by this colour (linear, the project's colour space; Resources/
        /// NativeVisualBodyFx.cginc ViewCloth: all of it under .01 luminance, none past .08), so a black sleeve takes the view
        /// layer's light as dark cloth.</summary>
        public Vector3 Cloth;
        /// <summary>A free hand is tucked away fully once it hangs TuckHigh below the view's centre line (the tangent, -y/z), not at
        /// all above TuckLow.</summary>
        public float TuckLow,TuckHigh;
    }

    /// <summary>The pilot's own arms in first person for every weapon the tactical view does not hold (owner 2026-11-22: "Go ahead
    /// and fix", after F1's first-person audit: the hands sat below the frame for most blades and staffs, the greatsword's left
    /// forearm ended in an open sleeve). The old view model keeps its clips, its hands and its weapon where they are; the view
    /// body's shoulders go to an eye-anchored place, the arms reach the hands by two-bone IK and are cut at the clavicles, so they
    /// run in from the screen's edges. Each class's hold (Holds) moves into view by its Lift, measured on the view at rest, with
    /// its own shoulders and elbow poles. Presentation only: aim, reach, hit volumes, timing and the wire are the game's.</summary>
    public static class NativeViewArms
    {
        /// <summary>The shoulders' midpoint in the eye frame at true size (24 cm under the eye and level with it: the body's arms
        /// reach .49 m, and from shoulders behind the eye the holds were out of reach, the upper arms dragged forward into view).</summary>
        public static readonly Vector3 ShoulderAnchor=new Vector3(0,-.24f,.02f);
        /// <summary>Each elbow's pole in the eye frame at true size: down, out and ahead, so the elbows hang under the frame.</summary>
        public static readonly Vector3 PoleRight=new Vector3(.32f,-.6f,.12f),PoleLeft=new Vector3(-.32f,-.6f,.12f);
        /// <summary>Where a free left or right hand goes while it hangs low (eye frame, m): down under the frame, toward its shoulder.</summary>
        public static readonly Vector3 TuckLeft=new Vector3(.05f,-.3f,-.1f),TuckRight=new Vector3(-.05f,-.3f,-.1f);

        /// <summary>The arms' look (one instance: a local Editor probe may try values on it in play).</summary>
        public static readonly NativeViewArmsLook Look=new NativeViewArmsLook{Cloth=new Vector3(.04f,.042f,.05f),TuckLow=.1f,TuckHigh=.25f};

        /// <summary>A class without its own hold: as the old view holds it, from the anchored shoulders.</summary>
        public static readonly NativeViewArmsHold Default=new NativeViewArmsHold{Spec="",Lift=Vector3.Zero,Shoulder=ShoulderAnchor,PoleRight=PoleRight,PoleLeft=PoleLeft};

        /// <summary>Each class's hold, measured on the first-person view at rest at 2000x1173 (Brickfall, 75 degrees, the view-body
        /// probe; F2 2026-11-22): the weapon hand brought to about x .8, y .8 of the screen (the blades' and staffs' grips had sat
        /// at the bottom edge or past the right one, the Fangs' and the DEV blades' under the frame), the greatsword's hilt lowered
        /// to the right (its blade had stood in the centre), the shield's hand brought back (it held the shield .8 m ahead, out of
        /// the arm's reach), the staffs to the left (the shaft crossed the centre), the fighters' fists up from the bottom edge.
        /// The free hand of the one-hand blades (the left) and the staffs (the right) tucks away.</summary>
        public static readonly NativeViewArmsHold[] Holds=Build();
        private static NativeViewArmsHold[] Build()
        {
            NativeViewArmsHold H(string spec,Vector3 lift,int free=0)=>new NativeViewArmsHold{Spec=spec,Lift=lift,Shoulder=ShoulderAnchor,PoleRight=PoleRight,PoleLeft=PoleLeft,
                Free=free,Tuck=free<0?TuckLeft:free>0?TuckRight:Vector3.Zero};
            var staff=new Vector3(-.2f,.43f,.15f);
            var bow=H(NativeBowWire.Spec,Vector3.Zero,1);bow.CarryLift=new Vector3(-.14f,.1f,.42f);bow.Tuck=new Vector3(0,-.4f,-.1f);
            return new[]{
                H("sword",new Vector3(-.15f,.15f,.08f),-1),
                H("katana",new Vector3(-.15f,.22f,.08f),-1),
                H(NativeDevMotions.Katana,new Vector3(-.39f,.45f,.08f),-1),
                H("glaive",new Vector3(-.15f,.24f,.08f),-1),
                H("daggers",new Vector3(-.15f,.16f,.08f)),
                H(NativeFangs.Spec,new Vector3(-.18f,.25f,0)),
                H("shield",new Vector3(0,.05f,-.25f)),
                H("greatsword",new Vector3(.04f,-.13f,.01f)),
                H(NativeDevMotions.Greatsword,new Vector3(-.25f,.26f,.06f),-1),
                bow,
                H("fire",staff,1),H("water",staff,1),H("lightning",staff,1),H("darkness",staff,1),H("leet",staff,1),
                H(NativeBombs.Spec,new Vector3(0,.1f,0)),
                H("emperor",new Vector3(0,.1f,0)),
                H("ansatsuken",new Vector3(0,.09f,0))};
        }

        /// <summary>Whether `spec` takes the eye-anchored arms: every melee, magic, bow, bomb and fighter class, but not the guns
        /// (the tactical view), the Siren (its voxel arms), the Top Dog and the Cyber Lord (they read as they are).</summary>
        public static bool Eligible(string spec)
        {
            if(string.IsNullOrEmpty(spec))return false;
            if(spec=="pistol"||spec=="rifle"||spec=="machinegun"||spec=="siren"||spec==NativeTopDog.Spec||spec==CyberLordSpec.Id)return false;
            return true;
        }

        /// <summary>The class's hold (Default for a class without its own). Allocation free.</summary>
        public static NativeViewArmsHold Of(string spec)
        {
            if(!string.IsNullOrEmpty(spec))foreach(var h in Holds)if(string.Equals(h.Spec,spec,StringComparison.Ordinal))return h;
            return Default;
        }

        /// <summary>The class's hold raised into view (camera frame, m, at the old view's scale); zero where the hold already shows.</summary>
        public static Vector3 Lift(string spec)=>Of(spec).Lift;
        /// <summary>The hold's lift with a bow raised by `raised` (0 lowered, 1 on the arrow line; 1 for every other class; a bad
        /// value as lowered).</summary>
        public static Vector3 LiftOf(NativeViewArmsHold h,float raised)=>h.Lift+h.CarryLift*(1-(raised>0?Math.Min(raised,1):0));
        /// <summary>How far a free hand at `eye` (eye frame, m) is tucked away: 0 raised into view, 1 hanging low (or at or behind
        /// the eye).</summary>
        public static float TuckWeight(Vector3 eye)
        {
            if(!(eye.Z>.01f))return 1;
            float below=-eye.Y/eye.Z,lo=Look.TuckLow,hi=Look.TuckHigh;if(!(hi>lo))return below>=lo?1:0;
            float t=MathUtil.Clamp((below-lo)/(hi-lo),0,1);return t*t*(3-2*t);
        }
    }
}
