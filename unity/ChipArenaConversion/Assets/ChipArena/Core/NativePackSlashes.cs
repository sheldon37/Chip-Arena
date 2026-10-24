namespace ChipArena.UnityConversion
{
    // The slash pack on the special moves and finishers (owner 2026-10-24: "bring the slash pack's ring slashes, claw marks and
    // line slashes onto the special moves and finishers"; Runtime NativePackSlashPresentation.cs, docs/PACK-EFFECTS.md):
    // which move plays which of the bake's effects, laid how. A finisher is the last cut of a weapon's cycle (the captured
    // sets' order: the sword's third, a thrust; the daggers' fourth; the greatsword's second); the melee Q/E abilities and
    // the special moves play theirs at their release. Presentation only: what a move does, when and to whom is the rules'.
    public static class NativePackSlashes
    {
        public const int None=-1, Ring=0, RingReverse=1, Lines=2, Claws=3, Kinds=4;
        /// <summary>The bake's entries under VisualSlashFx, by kind.</summary>
        public static readonly string[] Names={"ring","ring-reverse","lines","claws"};
        /// <summary>How an effect lies about the pilot: flat round it, stood up across its facing (a rising cut), or ahead along
        /// its facing.</summary>
        public enum Lay { Flat, Upright, Ahead }
        /// <summary>Kind, lay, size (the bake's effect at scale 1 is the pack's, a ring some 5 m across) and the height of its
        /// centre over the pilot's rig root (its hips, 0.865 m over its feet), metres.</summary>
        public struct Look { public int Kind; public Lay Lay; public float Scale, Height; }

        /// <summary>A strike's finisher look (attack index from 1, as NativeMeleeRules.Begin counts; reverse: the cut runs
        /// right to left), or false for an ordinary cut.</summary>
        public static bool Finisher(string spec,int index,bool reverse,out Look look)
        {
            look=default;
            if(spec=="sword"&&index>0&&index%3==0){look=new Look{Kind=Lines,Lay=Lay.Ahead,Scale=.45f,Height=.1f};return true;}
            if(spec=="daggers"&&index>0&&index%4==0){look=new Look{Kind=Claws,Lay=Lay.Ahead,Scale=.7f,Height=.2f};return true;}
            if(spec=="greatsword"&&index>0&&index%2==0){look=new Look{Kind=reverse?RingReverse:Ring,Lay=Lay.Flat,Scale=.5f,Height=0};return true;}
            return false;
        }
        /// <summary>A special move's or melee Q/E ability's look at its release (its kind as replicated: the special's when one
        /// is set, else the kit's), or false (the shield's throws and charges' other moves, a kunai mark, Skyfall).</summary>
        public static bool Ability(string kind,out Look look)
        {
            look=default;
            switch(kind)
            {
                case "whirlwind":case "wolf-spin":look=new Look{Kind=Ring,Lay=Lay.Flat,Scale=.6f,Height=-.15f};return true;
                case "rising-dragon":case "sword-launch":look=new Look{Kind=Ring,Lay=Lay.Upright,Scale=.45f,Height=.3f};return true;
                case "flash-step":case "shadow-step":case "bulwark-charge":look=new Look{Kind=Lines,Lay=Lay.Ahead,Scale=.55f,Height=0};return true;
                case "dagger-throw":look=new Look{Kind=Claws,Lay=Lay.Ahead,Scale=.6f,Height=.2f};return true;
                default:return false;
            }
        }
        /// <summary>An effect plays for EffectLife seconds; at most PerKind of a kind at once (the oldest replaced). Releases older
        /// than Stale seconds when first seen (a late join, a long frame) draw nothing.</summary>
        public const float EffectLife=2.2f, Stale=.25f;
        public const int PerKind=4;
    }
}
