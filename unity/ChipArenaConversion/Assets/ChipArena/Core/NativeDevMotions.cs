namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV animation classes (owner 2026-11-11: "For those new animations; Maybe lets put them on DEV classes. Call them like ... DEV Katana
    /// and DEV Greatsword"): a DEV class that is another class's copy in everything but its model's animation. Its rules, numbers, kit, models,
    /// effects and sounds are its base class's, so it plays exactly as the base does; only the model plays another animation set (Core
    /// NativeVisualMotion: Grruzam's great sword and katana sets). Its content row (devOnly) is what the Locker, the DEV screen and the grants
    /// offer; when the arena spawns a seat that picked it, the seat plays its base class and keeps the DEV class as its motion (`NativeSeat.motion`,
    /// on the wire with the seat; every actor of it carries it as `Actor.Motion`, filled from the seat on host and guests).
    /// </summary>
    public static class NativeDevMotions
    {
        public const string Greatsword="dev-greatsword",Katana="dev-katana";
        public static readonly string[] All={Greatsword,Katana};
        /// <summary>The class a DEV animation class plays as (null: none).</summary>
        public static string Base(string spec)=>spec==Greatsword?"greatsword":spec==Katana?NativeKatana.Spec:null;
        public static bool Is(string spec)=>Base(spec)!=null;
        /// <summary>A seat about to play: a DEV animation class plays its base, the class kept as its motion; a motion whose base the seat no
        /// longer plays (another class picked) is cleared.</summary>
        public static void Normalize(NativeSeat seat)
        {
            if(seat==null)return;
            if(Is(seat.spec)){seat.motion=seat.spec;seat.spec=Base(seat.spec);return;}
            seat.motion=Clean(seat.motion,seat.spec);
        }
        /// <summary>A seat's motion as it may be: a DEV animation class whose base the seat plays, else none.</summary>
        public static string Clean(string motion,string spec)=>Is(motion)&&Base(motion)==spec?motion:"";
        /// <summary>The animation set an actor's model plays by: its motion when it has one, else its own class.</summary>
        public static string Set(Actor a)=>a==null?null:!string.IsNullOrEmpty(a.Motion)?a.Motion:a.SourceSpec;
        /// <summary>Each class's own entrance (owner 2026-11-12, the DEV animation picks: "entrance → Intro"): the pack's Intro clip on the
        /// model (Core NativeDevIntros), in place of its base class's (the Colossus Oath, Crimson Moon).</summary>
        public const string GreatswordEntrance="dev-greatsword-intro",KatanaEntrance="dev-katana-intro";
        /// <summary>A DEV animation class's own entrance (null: not one).</summary>
        public static string Entrance(string spec)=>spec==Greatsword?GreatswordEntrance:spec==Katana?KatanaEntrance:null;
        /// <summary>The DEV animation class a seat plays, whether it is picked (its spec) or spawned (its motion); null: none.</summary>
        public static string Of(NativeSeat seat)=>seat==null?null:Is(seat.spec)?seat.spec:Is(seat.motion)?seat.motion:null;
        /// <summary>How an entrance's model holds its class's weapon (the weapon's frame in the right hand bone's, as the match holds it): an
        /// entrance's keyed stance never holds it the way the game's pose does (Core NativeBladeGrip). The DEV Katana's is the Katana's; the
        /// DEV Greatsword's the Greatsword's as the arena's body takes it from the game pose (its blade along 0.144, 0.468, 0.872 in the hand,
        /// its grip 0.096, -0.018, -0.015 from the wrist).</summary>
        public static bool Grip(string spec,out System.Numerics.Vector3 at,out System.Numerics.Quaternion turn)
        {
            at=System.Numerics.Vector3.Zero;turn=System.Numerics.Quaternion.Identity;
            if(spec==Katana){turn=NativeBladeGrip.Katana;return true;}
            if(spec==Greatsword){at=new System.Numerics.Vector3(.096f,-.018f,-.015f);turn=new System.Numerics.Quaternion(-.134251f,-.214495f,-.934753f,.24941f);return true;}
            return false;
        }
        /// <summary>The clip a pilot held in a paired move plays (Core NativeBladeMotion.Victim), by the attacker's move clip and the attacker
        /// seat's motion: the DEV Katana's counter (its rules the Katana's paired critical) gives its foe the katana set's big hit reaction
        /// (NativeDevKatanaMotion.CounterHit), timed to the blow; every other the pack's own victim clip.</summary>
        public static string VictimClip(string attackerClip,string attackerMotion)=>attackerMotion==Katana&&attackerClip==NativeBladeMotion.KatanaCritical?NativeDevKatanaMotion.CounterHit:NativeBladeMotion.VictimClip(attackerClip);
    }
}
