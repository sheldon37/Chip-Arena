using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The heavy kinds' jump-down entrance (owner 2026-10-12: "For now, we will have them enter from the middle of the map by
    /// jumping down and creating a small slam effect on their landing. (I am planning to revamp this map, so keep this in mind)";
    /// 2026-10-13, the Hulk: "The jump in too, shaking the ground and a even bigger stage effect when landing").
    /// Presentation side of a contract the rules do not implement yet (docs/SIREN-ZOMBIES.md hands it to Astra): an add whose
    /// replicated entry is a map entry of kind <see cref="Kind"/> drops from <see cref="HeightOf"/> above that entry's landing
    /// point (PveEntry.exit), on its kind's <see cref="Profile"/> from its replicated SpawnAt to its replicated ActiveAt:
    /// <list type="number">
    /// <item>The warning: a landing ring telegraphs the spot (the add is not shown yet; the Hulk's is wider, longer and rumbles).</item>
    /// <item>The leap: it appears above the spot out of an ink tear, crouches and springs (HyperChase jump start).</item>
    /// <item>The fall (<see cref="Fall"/>, from the height under the profile's gravity), tucked (the jump's air take).</item>
    /// <item>The landing at <see cref="LandAt"/> = ActiveAt - the profile's recovery: the slam (ring, debris, dust, ink, a camera
    /// shake for pilots near it, existing-bank thud; the Hulk's craters the floor, throws up a dust wall and jolts the props
    /// near it), then the landing take's recovery until ActiveAt.</item>
    /// </list>
    /// The landing point, its facing and the drop height are map data (the entry), never coordinates here, so a revamped map
    /// moves the drop by editing its entries. Everything is derived from the add's replicated entry index, SpawnAt and
    /// ActiveAt, so host, guests and the killcam show the same drop with no new frame field.
    /// </summary>
    public static class NativeHordeDrop
    {
        /// <summary>The map entry kind (PveEntry.kind) of a jump-down entrance.</summary>
        public const string Kind="drop";
        /// <summary>One kind's drop: its timeline (s), fall, the landing slam's shown radius and the camera shake's reach and peak.</summary>
        public readonly struct DropProfile
        {
            public readonly float Warn,Leap,Recover,Gravity,Hop,DefaultHeight,SlamRadius,ShakeRange,ShakePeak,ShakeKick;public readonly bool Heavy;
            public DropProfile(float warn,float leap,float recover,float gravity,float hop,float height,float slam,float range,float peak,float kick,bool heavy)
            {Warn=warn;Leap=leap;Recover=recover;Gravity=gravity;Hop=hop;DefaultHeight=height;SlamRadius=slam;ShakeRange=range;ShakePeak=peak;ShakeKick=kick;Heavy=heavy;}
        }
        /// <summary>The brute's (and any kind's but the Hulk's): a short warning, a 6.5 m fall, a small slam.</summary>
        public static readonly DropProfile Brute=new DropProfile(.7f,.35f,.9f,24f,.35f,6.5f,2.4f,12f,.07f,0,false);
        /// <summary>The Hulk's: a longer, wider, rumbling warning, a 9 m fall, a stage-shaking landing (5.5 m slam, 34 m shake).</summary>
        public static readonly DropProfile Hulk=new DropProfile(1.6f,.5f,1.5f,24f,.5f,9f,5.5f,34f,.2f,4.5f,true);
        public static DropProfile Profile(int kind)=>kind==NativeHordeKinds.Hulk?Hulk:Brute;

        // ---- the brute's numbers (the original contract; every call below without a profile is the brute's) ----
        public const float DefaultHeight=6.5f;
        public const float Warn=.7f,Leap=.35f,Recover=.9f,Gravity=24f,Hop=.35f;
        /// <summary>The landing slam's shown radius (m; the proposed rule's knock-back radius) and the shake's reach.</summary>
        public const float SlamRadius=2.4f,ShakeRange=12f;
        public static bool Is(PveEntry e)=>e!=null&&string.Equals(e.kind,Kind,StringComparison.Ordinal);
        /// <summary>The entry's drop height (today the profile's default: PveEntry has no height field yet).</summary>
        public static float HeightOf(PveEntry e)=>DefaultHeight;
        public static float HeightOf(PveEntry e,in DropProfile p)=>p.DefaultHeight;
        /// <summary>The fall from the top of the hop to the floor (s).</summary>
        public static float Fall(float height)=>Fall(height,Brute);
        public static float Fall(float height,in DropProfile p)=>(float)Math.Sqrt(2*(Math.Max(.5f,height)+p.Hop)/p.Gravity);
        /// <summary>The whole entrance, SpawnAt to ActiveAt, as the presentation plays it (the rules' timeline should match it).</summary>
        public static float Duration(float height)=>Duration(height,Brute);
        public static float Duration(float height,in DropProfile p)=>p.Warn+p.Leap+Fall(height,p)+p.Recover;
        /// <summary>When the add touches down, from the replicated ActiveAt.</summary>
        public static float LandAt(float activeAt)=>activeAt-Recover;
        public static float LandAt(float activeAt,in DropProfile p)=>activeAt-p.Recover;
        /// <summary>The phases of the entrance at `age` seconds after SpawnAt, for an add active at `span` after it.</summary>
        public enum Phase{Warn,Leap,Air,Land,Done}
        /// <summary>
        /// Where the entrance is at `age` (s after SpawnAt) when the add turns active `span` s after SpawnAt: its phase, the seconds into
        /// that phase, and the root's height above its standing height (m). The leap and fall are laid back from the landing, so a
        /// longer or shorter rules timeline only stretches the warning.
        /// </summary>
        public static Phase At(float age,float span,float height,out float into,out float lift)=>At(age,span,height,Brute,out into,out lift);
        public static Phase At(float age,float span,float height,in DropProfile p,out float into,out float lift)
        {
            float land=span-p.Recover,fall=Fall(height,p),leap=land-fall-p.Leap;
            if(age>=span){into=age-span;lift=0;return Phase.Done;}
            if(age>=land){into=age-land;lift=0;return Phase.Land;}
            if(age>=leap+p.Leap)
            {
                into=age-(leap+p.Leap);float top=Math.Max(.5f,height)+p.Hop,t=fall-into;lift=Math.Max(0,.5f*p.Gravity*t*t);if(lift>top)lift=top;return Phase.Air;
            }
            if(age>=leap){into=age-leap;float u=into/p.Leap;lift=Math.Max(.5f,height)+p.Hop*(u*u*(3-2*u));return Phase.Leap;}
            into=age;lift=Math.Max(.5f,height);return Phase.Warn;
        }
        /// <summary>The warning's rumble (0..1) at `age` s into a heavy drop's warning: building to the leap (the Hulk shakes the ground
        /// before it lands); 0 for a light profile.</summary>
        public static float Rumble(float age,float span,float height,in DropProfile p)
        {
            if(!p.Heavy)return 0;var ph=At(age,span,height,p,out float into,out _);
            if(ph!=Phase.Warn&&ph!=Phase.Leap)return 0;float warn=span-p.Recover-Fall(height,p)-p.Leap;float u=ph==Phase.Leap?1:Math.Min(1,into/Math.Max(.1f,warn));return u*u;
        }
    }
}
