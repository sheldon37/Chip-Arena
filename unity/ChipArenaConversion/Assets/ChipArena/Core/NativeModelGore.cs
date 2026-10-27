using System;

namespace ChipArena.UnityConversion
{
    // The model's insides at a gib's cut (owner 2026-10-27: "Can you show bones + flesh on gibs too? correct on anatomics too";
    // Runtime NativeVisualBodyPresentation.cs VisualCorpseBones). The model is a person, so where a corpse is severed at a joint
    // each side's cut shows red flesh and the bones that run through that side, each where it lies in the limb's cross-section:
    //  - the upper arm the humerus; the forearm the radius (to the outside) and the ulna (to the inside); the hand its carpals;
    //  - the thigh the femur; the shin the tibia (forward, inside) and the fibula (behind, outside); the foot the talus;
    //  - the neck and back the vertebrae, behind the middle (the neck's windpipe in front); the head the skull's base; the pelvis
    //    the hip bone; the collarbone at the shoulder.
    // Sizes for the model's slim build (its upper arm about 8 cm across). Each bone is a short stub through the cut, sticking out a
    // little on the cut piece (the limb side) and flush on the stump, its marrow dark at the end. Presentation only.
    public static class NativeModelGore
    {
        /// <summary>The cut's flesh, a bone and its marrow (sRGB).</summary>
        public const int Flesh=0x8c1419, Bone=0xe8dcc2, Marrow=0x5a0b12;
        /// <summary>A bone's stub: out of the cut piece (Out), into either side (Into; an end piece, a hand, a foot or the head,
        /// EndInto), out of the stump (Stump), metres. The skin parts a little past the joint, so these stand out a little more.</summary>
        public const float Out=.006f, Into=.04f, EndInto=.022f, Stump=.002f;
        /// <summary>How deep a bone's stub runs into its side (an end piece's shorter, so it stays inside).</summary>
        public static float Depth(string bone){var k=Kind(bone);return k=="hand"||k=="foot"||k=="head"?EndInto:Into;}
        /// <summary>One bone in a cross-section: its centre from the bone's axis, outward (away from the body's middle: for a left
        /// limb its left) and ahead (the body's front), and its radius (metres).</summary>
        public readonly struct Section { public readonly float Outward, Ahead, Radius; public Section(float o,float a,float r){Outward=o;Ahead=a;Radius=r;} }
        private static readonly Section[] None=Array.Empty<Section>();
        private static readonly Section[] Humerus={new Section(0,0,.0095f)};
        private static readonly Section[] Forearm={new Section(.0065f,.002f,.0055f),new Section(-.006f,-.003f,.005f)};
        private static readonly Section[] Carpals={new Section(.004f,0,.0055f),new Section(-.004f,0,.005f)};
        private static readonly Section[] Femur={new Section(-.002f,-.002f,.012f)};
        private static readonly Section[] Shin={new Section(-.003f,.006f,.0105f),new Section(.011f,-.004f,.0045f)};
        private static readonly Section[] Talus={new Section(0,0,.011f)};
        private static readonly Section[] Collarbone={new Section(0,.004f,.0055f)};
        private static readonly Section[] Hip={new Section(.004f,0,.013f)};
        private static readonly Section[] Spine={new Section(0,-.022f,.015f)};
        private static readonly Section[] Neck={new Section(0,-.008f,.0095f),new Section(0,.018f,.0055f)};
        private static readonly Section[] SkullBase={new Section(0,-.01f,.012f)};
        /// <summary>The bone's name without its side and twist (upperarm_twist_02_l: upperarm; spine_03: spine).</summary>
        public static string Kind(string bone)
        {
            if(string.IsNullOrEmpty(bone))return "";
            int twist=bone.IndexOf("_twist",StringComparison.Ordinal);if(twist>0)return bone.Substring(0,twist);
            int u=bone.IndexOf('_');return u>0?bone.Substring(0,u):bone;
        }
        /// <summary>The side of a bone: -1 left, 1 right, 0 the middle.</summary>
        public static int Side(string bone)=>bone==null?0:bone.EndsWith("_l",StringComparison.Ordinal)||bone.Contains("_l1")?-1:bone.EndsWith("_r",StringComparison.Ordinal)||bone.Contains("_r1")?1:0;
        /// <summary>The bones a cut shows on the side of bone `bone` (the bone of that side next to the joint), across from bone
        /// `across` (the other side's): none for a bone the table does not know (hair, fingers, the IK bones).</summary>
        public static Section[] At(string bone,string across)
        {
            switch(Kind(bone))
            {
                case "upperarm":return Humerus;
                case "lowerarm":return Forearm;
                case "hand":return Carpals;
                case "thigh":return Femur;
                case "calf":return Shin;
                case "foot":return Talus;
                case "clavicle":return Collarbone;
                case "pelvis":return Kind(across)=="thigh"?Hip:Spine;
                case "spine":return Spine;
                case "neck":return Neck;
                case "head":return SkullBase;
            }
            return None;
        }
    }
}
