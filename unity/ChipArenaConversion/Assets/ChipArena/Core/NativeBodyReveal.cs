using System.Numerics;

namespace ChipArena.UnityConversion
{
    // A directed entrance's per-bone reveal on the visual body (owner 2026-10-28: "Sheldon intro still needs revisions, it uses
    // voxel model in the "glitching""). The voxel pilot drops a bone's parts out (or dithers them) by the entrance's Solid share;
    // the model has no parts, so its shader (Resources/NativeVisualBodyFx.cginc RevealClip) gives each pixel the share of the
    // game bone whose segment is nearest: each bone from its joint to its child's (the pelvis to the spine, the shoulder to the
    // elbow, the hip to the knee), the leaves on along their parent's way (a hand Hand, a toe Toe metres) and the head up its own
    // axis (Head metres). Runtime/NativeVisualBodyPresentation.cs VisualRevealLook writes the segments each frame the entrance
    // reveals. Presentation only.
    public static class NativeBodyReveal
    {
        /// <summary>The segments: one per game bone but the motion root (bone b is segment b-1).</summary>
        public const int Segments=21;
        public const float Head=.24f,Hand=.14f,Toe=.06f;
        /// <summary>Each bone's child joint its segment ends on (-1: a leaf).</summary>
        public static readonly int[] Child={-1,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,-1,
            CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,-1,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand,-1,
            CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.LToe,-1,CapturedRig.RKnee,CapturedRig.RFoot,CapturedRig.RToe,-1};

        /// <summary>Bone `bone`'s segment from the joints' world positions `at` (by bone) and the head's up axis.</summary>
        public static void Segment(int bone,Vector3[] at,Vector3 headUp,out Vector3 from,out Vector3 to)
        {
            from=at[bone];int child=Child[bone];
            if(child>=0){to=at[child];return;}
            if(bone==CapturedRig.Head){to=from+headUp*Head;return;}
            var way=from-at[CapturedRig.Bones[bone].Parent];
            to=from+(way.LengthSquared()>1e-8f?Vector3.Normalize(way):-Vector3.UnitY)*(bone==CapturedRig.LHand||bone==CapturedRig.RHand?Hand:Toe);
        }

        /// <summary>The bone whose segment is nearest `p` (the shader's pick; segments by bone-1). A joint goes to the later
        /// segment, its child's, whose voxel part (the elbow's, the knee's cap) starts there.</summary>
        public static int Nearest(Vector3 p,Vector3[] from,Vector3[] to)
        {
            float best=float.MaxValue;int pick=CapturedRig.Pelvis;
            for(int k=0;k<Segments;k++)
            {
                var a=from[k];var ab=to[k]-a;float t=Vector3.Dot(p-a,ab)/System.Math.Max(Vector3.Dot(ab,ab),1e-6f);t=t<0?0:t>1?1:t;
                float e=Vector3.DistanceSquared(p,a+ab*t);if(e<=best){best=e;pick=k+1;}
            }
            return pick;
        }
    }
}
