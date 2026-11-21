using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The physical hit reactions' rules (owner 2026-11-21: "Utilize Ragdoll Asset for hit reactions"; Runtime/NativeHitPuppets.cs,
    /// Runtime/NativeHitPuppetPresentation.cs). A pilot (player or bot) or an add drawn on the model that takes a non-lethal hit
    /// has its drawn pose pushed by an active ragdoll (the owner's licensed PuppetMaster, through a local bridge): the muscles
    /// near the hit unpin, a push lands at the hit point along the blow, scaled to it (a greatsword's heavy blow far more than a
    /// pistol round), and the pose regains. Presentation only: never read by authority; no change to hit timing, hitboxes,
    /// damage, cooldowns, stuns, movement or the network frame. Every client reacts to the hit events it already receives.
    /// Pure functions, no allocation.
    /// <list type="bullet">
    /// <item>The cost is capped: at most <see cref="Cap"/> bodies react at once, within <see cref="Reach"/> of the camera, a
    /// tier off on the low graphics tier (Performance keeps today's look), <see cref="MakesPerFrame"/> new puppets made a frame
    /// and <see cref="Kept"/> kept made.</item>
    /// <item>The push (<see cref="Push"/>): an impulse in N·s on a body of about 70 kg (the upper body's muscles), from the
    /// hit's felt weight (Core/NativeStagger's Flinch, Step, Heavy), its strength within that weight and, for a blade, the
    /// weapon's heft (Core/NativeHitReact: daggers .6 .. greatsword 1.4); how far the muscles near it unpin (0..1).</item>
    /// </list>
    /// </summary>
    public static class NativeHitPuppetRules
    {
        /// <summary>New puppets made a frame, and puppets kept made (sleeping ones cost nothing a frame).</summary>
        public const int MakesPerFrame=1,Kept=16;
        /// <summary>How long after the hit's own time it may still land (s): a later one is dropped as stale.</summary>
        public const float Stale=.3f;
        /// <summary>The longest a hit waits for its body to be posed on its clock (s) before it lands anyway.</summary>
        public const float Ahead=.25f;
        /// <summary>A body whose root moved further than this between two steps (m) is put back to its drawn pose (a teleport).</summary>
        public const float Jump=1.5f;
        /// <summary>The furthest the hit point is taken from the body's centre (m): a point further off is drawn in to it.</summary>
        public const float PointReach=1.1f;

        /// <summary>Bodies reacting at once: High 8, Balanced 4 (half with Reduced effects), none on the low tier.</summary>
        public static int Cap(NativeGraphicsTier tier,bool reducedEffects)
        {
            int n=tier==NativeGraphicsTier.High?8:tier==NativeGraphicsTier.Medium?4:0;
            return reducedEffects?n/2:n;
        }
        /// <summary>The distance from the camera within which a hit makes its body react (m): High 28, Balanced 18.</summary>
        public static float Reach(NativeGraphicsTier tier)=>tier==NativeGraphicsTier.High?28f:tier==NativeGraphicsTier.Medium?18f:0f;

        /// <summary>
        /// Whether a hit of this kind pushes the body: a direct hit, an ability's, a blast's, debris or a melee blow; never a
        /// damage tick (burn, poison, a field, thorns: "secondary"), a hazard's or a stage's (their own presentations show them).
        /// </summary>
        public static bool Felt(string detail,bool melee)
        {
            if(melee)return true;
            switch(detail)
            {
                case null:case "direct":case "ability":case "blast":case "debris":case "dev-monster":return true;
                default:return false;
            }
        }

        /// <summary>The impulse (N·s) of a lone hit of each weight, before its strength and heft.</summary>
        public static float Base(StaggerWeight w)=>w==StaggerWeight.Heavy?42f:w==StaggerWeight.Step?24f:w==StaggerWeight.Flinch?11f:0f;
        /// <summary>A blade's push over a round's of the same weight (a blow carries its swing through the body).</summary>
        public const float MeleeGain=1.35f;
        /// <summary>The largest push (N·s) and a head's share of it (the head is light: a headshot snaps it, it never flies).</summary>
        public const float MaxImpulse=80f,HeadShare=.45f;

        /// <summary>
        /// The push of a hit of weight `w` (its strength `scale`, Core NativeStaggerRules.Scale, .7..1.25), a blade's (`melee`)
        /// with heft `heft` (Core NativeHitRules.Heft), on the head (`head`): the impulse (N·s) and how far the muscles at the
        /// hit unpin (0..1). False for a hit that pushes nothing (weight None, a non-finite or empty one).
        /// </summary>
        public static bool Push(StaggerWeight w,float scale,bool melee,float heft,bool head,out float impulse,out float unpin)
        {
            impulse=0;unpin=0;float b=Base(w);if(!(b>0))return false;
            float s=Finite(scale,1);s=Math.Max(.7f,Math.Min(1.25f,s));
            float h=Finite(heft,1);h=Math.Max(.5f,Math.Min(1.5f,h));
            impulse=b*s*(melee?MeleeGain*h:1);
            if(head)impulse*=HeadShare;
            impulse=Math.Min(MaxImpulse,impulse);
            unpin=w==StaggerWeight.Heavy?.95f:w==StaggerWeight.Step?.7f:.4f;
            if(melee)unpin=Math.Min(1,unpin+.1f*h);
            return impulse>0;
        }
        private static float Finite(float v,float fallback)=>float.IsNaN(v)||float.IsInfinity(v)?fallback:v;
    }
}
