using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The brute's jump-down entrance (owner 2026-10-12: "For now, we will have them enter from the middle of the map by jumping
    /// down and creating a small slam effect on their landing. (I am planning to revamp this map, so keep this in mind)").
    /// Presentation side of a contract the rules do not implement yet (docs/SIREN-ZOMBIES.md hands it to Astra): an add whose
    /// replicated entry is a map entry of kind <see cref="Kind"/> drops from <see cref="HeightOf"/> above that entry's landing
    /// point (PveEntry.exit), on this timeline from its replicated SpawnAt to its replicated ActiveAt:
    /// <list type="number">
    /// <item><see cref="Warn"/>: a landing ring telegraphs the spot (the brute is not shown yet).</item>
    /// <item><see cref="Leap"/>: it appears above the spot out of an ink tear, crouches and springs (HyperChase jump start).</item>
    /// <item>The fall (<see cref="Fall"/>, from the height under <see cref="Gravity"/>), tucked (the jump's air take).</item>
    /// <item>The landing at <see cref="LandAt"/> = ActiveAt - <see cref="Recover"/>: the slam (ring, debris, dust, ink, a
    /// short camera shake for pilots near it, existing-bank thud), then the landing take's recovery until ActiveAt.</item>
    /// </list>
    /// The landing point, its facing and the drop height are map data (the entry), never coordinates here, so a revamped map
    /// moves the drop by editing its entries. Everything is derived from the add's replicated entry index, SpawnAt and
    /// ActiveAt, so host, guests and the killcam show the same drop with no new frame field.
    /// </summary>
    public static class NativeHordeDrop
    {
        /// <summary>The map entry kind (PveEntry.kind) of a jump-down entrance.</summary>
        public const string Kind="drop";
        /// <summary>The fall height when the entry names none (m above the landing floor). The entry's own height
        /// (the proposed PveEntry.height) replaces it once the content carries one.</summary>
        public const float DefaultHeight=6.5f;
        public const float Warn=.7f,Leap=.35f,Recover=.9f,Gravity=24f,Hop=.35f;
        /// <summary>The landing slam's shown radius (m; the proposed rule's knock-back radius) and the shake's reach.</summary>
        public const float SlamRadius=2.4f,ShakeRange=12f;
        public static bool Is(PveEntry e)=>e!=null&&string.Equals(e.kind,Kind,StringComparison.Ordinal);
        /// <summary>The entry's drop height (today the default: PveEntry has no height field yet).</summary>
        public static float HeightOf(PveEntry e)=>DefaultHeight;
        /// <summary>The fall from the top of the hop to the floor (s).</summary>
        public static float Fall(float height)=>(float)Math.Sqrt(2*(Math.Max(.5f,height)+Hop)/Gravity);
        /// <summary>The whole entrance, SpawnAt to ActiveAt, as the presentation plays it (the rules' timeline should match it).</summary>
        public static float Duration(float height)=>Warn+Leap+Fall(height)+Recover;
        /// <summary>When the brute touches down, from the replicated ActiveAt.</summary>
        public static float LandAt(float activeAt)=>activeAt-Recover;
        /// <summary>The phases of the entrance at `age` seconds after SpawnAt, for an add active at `span` after it.</summary>
        public enum Phase{Warn,Leap,Air,Land,Done}
        /// <summary>
        /// Where the entrance is at `age` (s after SpawnAt) when the add turns active `span` s after SpawnAt: its phase, the seconds into
        /// that phase, and the root's height above its standing height (m). The leap and fall are laid back from the landing, so a
        /// longer or shorter rules timeline only stretches the warning.
        /// </summary>
        public static Phase At(float age,float span,float height,out float into,out float lift)
        {
            float land=span-Recover,fall=Fall(height),leap=land-fall-Leap;
            if(age>=span){into=age-span;lift=0;return Phase.Done;}
            if(age>=land){into=age-land;lift=0;return Phase.Land;}
            if(age>=leap+Leap)
            {
                into=age-(leap+Leap);float top=Math.Max(.5f,height)+Hop,t=fall-into;lift=Math.Max(0,.5f*Gravity*t*t);if(lift>top)lift=top;return Phase.Air;
            }
            if(age>=leap){into=age-leap;float u=into/Leap;lift=Math.Max(.5f,height)+Hop*(u*u*(3-2*u));return Phase.Leap;}
            into=age;lift=Math.Max(.5f,height);return Phase.Warn;
        }
    }
}
