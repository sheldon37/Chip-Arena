using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Katana's saya on the model and the blade's way into it (owner 2026-11-11, after the first round: "Katana intro is broken, they dont
    /// hold it. Also, sheathe is still not on body."; docs/KATANA.md "The saya"). Where the Frank Slash Pack's katana set wears it: its
    /// Sword_Case bone rides the pelvis, the hilt at the front of the belly on the left and the saya down along the outside of the left thigh
    /// (the sheathed idle's left hand rests on it; the case keeps 13 cm off the thigh bone through the idles and runs). The pack's clips move
    /// it with the left hand: the stance-three moves (the iai, the rush, the storm) carry it pulled forward for the draw, the sheathing takes
    /// it up for the blade; and the sheathing turns the sword over in the right hand (about 165 degrees, early in the clip) so the blade goes
    /// in edge up. The blade sits in the case at the iai's draw (0.6 s) and the sheathing's click (1.867 s).
    /// Measured on the pack's bones (Blender) and on the model at the same clip times; the numbers are in the model's own bone frames, Unity
    /// axes (x, y, z, w), each frame without scale; the saya's frame is its mouth, +z along it to its end, -x its edge side (the saya model's,
    /// Core NativeWeaponModels.BuildSaya). Presentation only: nothing here reaches the rules, the network or the hitboxes.
    /// </summary>
    public static class NativeKatanaSaya
    {
        /// <summary>The saya's mouth frame on the model's pelvis bone: in the sheathed idle 11 cm left, 13 cm up and 15 cm ahead of the pelvis,
        /// the saya 48 degrees under level, down and out along the left thigh.</summary>
        public static readonly Vector3 RestAt=new Vector3(-.0923f,-.1200f,-.1679f);
        public static readonly Quaternion RestTurn=new Quaternion(-.792126f,.216047f,-.134837f,.554689f);
        /// <summary>The carries: the stance-three draw's (the saya pulled forward and turned for the draw) and the sheathing's (taken up to
        /// receive the blade).</summary>
        public const int Draw=0,Sheathe=1;
        /// <summary>The saya's mouth frame in the model's left hand bone frame while that hand carries it, per carry: where the blade in the
        /// right hand lies in it at the iai's draw and the sheathing's click, so it leaves and enters without a jump.</summary>
        public static readonly Vector3[] CarryAt={new Vector3(-.1442f,-.0163f,-.1141f),new Vector3(-.0475f,-.0485f,-.0478f)};
        public static readonly Quaternion[] CarryTurn={new Quaternion(-.009691f,.524399f,.074841f,.848122f),new Quaternion(.443487f,.295695f,.791637f,.298655f)};
        /// <summary>The sword turned over in the right hand for the sheathing, left-multiplied onto its hold (the right hand bone's frame).</summary>
        public static readonly Quaternion SheatheTurn=new Quaternion(.71252f,-.686653f,-.001035f,.144298f);
        /// <summary>The pack's moments: the iai's hand takes the blade off its dummy, the sheathing's click seats it.</summary>
        public const float IaiDraw=.6f,SheatheClick=1.867f;

        /// <summary>How much of the saya the left hand carries in `clip` at `t` (0: on the pelvis, 1: in the hand), and which carry.</summary>
        public static float Carry(string clip,float t,out int hold)
        {
            hold=Draw;
            switch(clip)
            {
                // The stance-three moves keep it in the left hand throughout (the storm lets go for its flurry).
                case NativeBladeMotion.KatanaIai:case NativeBladeMotion.KatanaRush:return 1;
                case NativeBladeMotion.KatanaStorm:return 1-Smooth(1.25f,1.45f,t);
                // The sheathing: the hand takes it up as the blade turns over, holds it through the click, and lets it hang again.
                case NativeBladeMotion.KatanaSheathe:hold=Sheathe;return Smooth(0,.55f,t)*(1-Smooth(2.05f,2.4f,t));
            }
            return 0;
        }
        /// <summary>How far the sword is turned over in the hand in `clip` at `t` (0..1 of SheatheTurn).</summary>
        public static float Turn(string clip,float t)=>clip==NativeBladeMotion.KatanaSheathe?Smooth(.08f,.3f,t):0;
        private static float Smooth(float a,float b,float t){float x=Math.Clamp((t-a)/(b-a),0f,1f);return x*x*(3-2*x);}
    }

    /// <summary>
    /// How the match holds the blade classes' blades on the model: the weapon frame in the hand bone's frame (Unity axes, at the wrist; the
    /// draw moves it into the palm, Runtime PalmGrip), the hold the arena's body takes from the game pose's wrist on its first frame
    /// (Runtime VisualMotionPose), measured on the bake's skeleton. An entrance's keyed stance never holds a sword the way the game's pose
    /// does, so the hold its own first frame gave hung the blade from the bare wrist along the forearm (owner 2026-11-11: "Katana intro is
    /// broken, they dont hold it"; the Fangs' was 30 degrees off): the blade classes' entrances take these. Presentation only.
    /// </summary>
    public static class NativeBladeGrip
    {
        public static readonly Quaternion Katana=new Quaternion(-.046041f,-.208413f,-.963306f,-.162748f),
            Fangs=new Quaternion(.09697f,-.103489f,-.90813f,-.393938f),FangsLeft=new Quaternion(-.895441f,-.367591f,-.048578f,-.246382f);
        /// <summary>The match's hold of `spec`'s blade in that hand (false: it has none there).</summary>
        public static bool Of(string spec,bool left,out Quaternion turn)
        {
            turn=Quaternion.Identity;
            if(spec==NativeKatana.Spec&&!left){turn=Katana;return true;}
            if(spec==NativeFangs.Spec){turn=left?FangsLeft:Fangs;return true;}
            return false;
        }
    }
}
