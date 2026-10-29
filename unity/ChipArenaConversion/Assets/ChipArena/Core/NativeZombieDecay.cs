using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The zombies' decay openings on the model (owner 2026-10-29: "Need to make the models look more undead, torn clothes, flesh
    // wounds, bones visible"; docs/ZOMBIE-MODELS.md; Runtime/NativeZombieModelPresentation.cs): each variant lists a few sites
    // where its skin and clothes are torn open onto a flesh pit with the model's own skeleton inside (Core NativeModelSkeleton): the
    // ribs through a flank or the back, the ulna and radius through a forearm, the jaw and teeth through a cheek, the tibia through
    // a shin, the femur through a thigh, the humerus through an upper arm. A site is found once per variant at the model's rest
    // (root space, +z its front, +y up): a point inside the body from the rest joints, a direction out of it; the runtime casts
    // from the point along the direction onto the outermost worn surface, and the opening goes in from there. The shader cuts it
    // in the meshes' own rest space, so it rides the skin as it deforms. Presentation only.
    public static class NativeZombieDecay
    {
        public sealed class Site
        {
            public string Name,Bone,From,To;public float Share;
            /// <summary>The direction out: across (toward the site's side), ahead (+z) and up, before it is turned square to the limb.</summary>
            public float Across,Ahead,Up;
            /// <summary>Radius and, for a trunk or head site, its depth (m); a limb's goes to its bone and this past it.</summary>
            public float Radius,Depth;public bool Limb;
        }
        /// <summary>At most this many openings a body (the shader's arrays).</summary>
        public const int Max=4;
        private static Site S(string name,string bone,string from,string to,float share,float across,float ahead,float up,float radius,float depth,bool limb)
            =>new Site{Name=name,Bone=bone,From=from,To=to,Share=share,Across=across,Ahead=ahead,Up=up,Radius=radius,Depth=depth,Limb=limb};
        /// <summary>Every site, a left and a right where it has one (the side's bones end in _l or _r).</summary>
        public static readonly Site[] All=
        {
            S("ribs-l","spine_04","spine_04","spine_05",.15f,.85f,.5f,0,.068f,.046f,false),S("ribs-r","spine_04","spine_04","spine_05",.15f,.85f,.5f,0,.068f,.046f,false),
            S("back","spine_04","spine_03","spine_04",.4f,0,-1,0,.07f,.055f,false),
            S("forearm-l","lowerarm_l","lowerarm_l","hand_l",.45f,.55f,.25f,.8f,.038f,.008f,true),S("forearm-r","lowerarm_r","lowerarm_r","hand_r",.45f,.55f,.25f,.8f,.038f,.008f,true),
            S("upperarm-l","upperarm_l","upperarm_l","lowerarm_l",.45f,.8f,.4f,0,.042f,.008f,true),S("upperarm-r","upperarm_r","upperarm_r","lowerarm_r",.45f,.8f,.4f,0,.042f,.008f,true),
            S("cheek-l","head","head","head",0,.75f,.66f,0,.032f,.034f,false),S("cheek-r","head","head","head",0,.75f,.66f,0,.032f,.034f,false),
            S("shin-l","calf_l","calf_l","foot_l",.45f,.15f,1,0,.042f,.008f,true),S("shin-r","calf_r","calf_r","foot_r",.45f,.15f,1,0,.042f,.008f,true),
            S("thigh-l","thigh_l","thigh_l","calf_l",.5f,.6f,.8f,0,.055f,.01f,true),S("thigh-r","thigh_r","thigh_r","calf_r",.5f,.6f,.8f,0,.055f,.01f,true),
        };
        public static Site Find(string name){foreach(var s in All)if(s.Name==name)return s;return null;}
        /// <summary>Where a cheek opening starts, above the head joint and ahead of it (m, at the model's scale).</summary>
        public const float CheekUp=.05f,CheekAhead=.03f;
        /// <summary>A site's point inside the body and its direction out (root space at rest), from the rest joints (`rest(name)`); false
        /// when a joint is missing. The side is the side of its bones (a left site goes toward the left clavicle); a limb's direction
        /// is turned square to the limb.</summary>
        public static bool Place(Site s,Func<string,Vector3?> rest,out Vector3 point,out Vector3 dir)
        {
            point=dir=Vector3.Zero;if(s==null)return false;
            var a=rest(s.From);var b=rest(s.To);var cl=rest("clavicle_l");var cr=rest("clavicle_r");if(a==null||b==null||cl==null||cr==null)return false;
            point=Vector3.Lerp(a.Value,b.Value,s.Share);
            bool left=s.Name.EndsWith("-l",StringComparison.Ordinal),right=s.Name.EndsWith("-r",StringComparison.Ordinal);
            var leftward=cl.Value-cr.Value;leftward.Y=0;if(leftward.LengthSquared()<1e-8f)return false;leftward=Vector3.Normalize(leftward);
            var across=left?leftward:right?-leftward:Vector3.Zero;
            if(s.Name.StartsWith("cheek",StringComparison.Ordinal))point+=new Vector3(0,CheekUp,CheekAhead);
            dir=across*s.Across+new Vector3(0,0,1)*s.Ahead+new Vector3(0,1,0)*s.Up;
            if(s.Limb){var axis=b.Value-a.Value;if(axis.LengthSquared()<1e-8f)return false;axis=Vector3.Normalize(axis);dir-=axis*Vector3.Dot(dir,axis);}
            if(dir.LengthSquared()<1e-8f)return false;dir=Vector3.Normalize(dir);return true;
        }
        /// <summary>An opening's depth from the surface (`surface` m out from the site's point): a limb's to its bone and past it, a
        /// trunk's or head's its own.</summary>
        public static float DepthOf(Site s,float surface)=>s.Limb?surface+s.Depth:s.Depth;
        /// <summary>The ragged rim: the opening's radius at angle `a` round its axis, as a share of its radius (0.56 to 1.08; the
        /// shader's DecayClip draws the same).</summary>
        public static float Rag(float a,float seed)=>.82f+.18f*MathF.Sin(a*5f+seed*97f)+.08f*MathF.Sin(a*11f+3f);
    }
}
