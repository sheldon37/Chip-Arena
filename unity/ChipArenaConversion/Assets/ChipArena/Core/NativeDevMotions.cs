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
    }
}
