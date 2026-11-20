using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>What a v2 intro is for (docs/INTROS-V2.md "Registering an intro").</summary>
    public enum NativeIntroKind:byte
    {
        /// <summary>Any pilot may pick it.</summary>
        Generic,
        /// <summary>A weapon's own intro: it plays for a pilot of that weapon who picked nothing (and replaces the old signature).</summary>
        Weapon,
        /// <summary>The new default: a pilot whose weapon has no intro of its own (and a DEV class without one) plays it.</summary>
        Default,
        /// <summary>DEV only (the owner's live DEV access): never listed or staged for anyone else.</summary>
        Dev,
    }

    /// <summary>One registered v2 intro: its id, display name, kind (and weapon spec), length, name beat and maker.</summary>
    public sealed class NativeIntroEntry
    {
        public readonly string Id,Name,Spec,Author,Blurb;public readonly NativeIntroKind Kind;public readonly float Duration,NameAt;public readonly Func<NativeEntranceScript> Make;
        private NativeIntroEntry(string id,string name,NativeIntroKind kind,string spec,float duration,float nameAt,Func<NativeEntranceScript> make,string author,string blurb)
        {Id=id;Name=name;Kind=kind;Spec=spec;Duration=duration;NameAt=nameAt;Make=make;Author=author;Blurb=blurb;}
        /// <summary>A generic intro "intro2-&lt;slug&gt;".</summary>
        public static NativeIntroEntry Generic(string slug,string name,float duration,float nameAt,Func<NativeEntranceScript> make,string author,string blurb=null)=>new NativeIntroEntry(NativeIntroCatalog.Prefix+slug,name,NativeIntroKind.Generic,null,duration,nameAt,make,author,blurb);
        /// <summary>Weapon `spec`'s intro "intro2-weapon-&lt;spec&gt;".</summary>
        public static NativeIntroEntry Weapon(string spec,string name,float duration,float nameAt,Func<NativeEntranceScript> make,string author,string blurb=null)=>new NativeIntroEntry(NativeIntroCatalog.WeaponPrefix+spec,name,NativeIntroKind.Weapon,spec,duration,nameAt,make,author,blurb);
        /// <summary>The new default "intro2-default".</summary>
        public static NativeIntroEntry Default(string name,float duration,float nameAt,Func<NativeEntranceScript> make,string author,string blurb=null)=>new NativeIntroEntry(NativeIntroCatalog.DefaultId,name,NativeIntroKind.Default,null,duration,nameAt,make,author,blurb);
        /// <summary>A DEV-only intro "dev-&lt;slug&gt;" (the owner's live DEV access).</summary>
        public static NativeIntroEntry Dev(string slug,string name,float duration,float nameAt,Func<NativeEntranceScript> make,string author,string blurb=null)=>new NativeIntroEntry("dev-"+slug,name,NativeIntroKind.Dev,null,duration,nameAt,make,author,blurb);
    }

    /// <summary>
    /// The v2 intro registry and the archive (owner 2026-11-20, docs/INTROS-V2.md). Every author registers each intro with ONE
    /// line in <see cref="All"/>, on its own slot (the comment lines keep the slots apart so merges stay trivial). Presentation
    /// and Locker data only: the match reads Duration and NameAt through NativeIntroRules, as for every entrance.
    /// </summary>
    public static class NativeIntroCatalog
    {
        public const string Prefix="intro2-",WeaponPrefix="intro2-weapon-",DefaultId="intro2-default";
        /// <summary>The new dev intro's id (registered by its author as NativeIntroEntry.Dev("architect", ...)) and the framework's showcase.</summary>
        public const string ArchitectId="dev-architect",ShowcaseId="dev-intro2-showcase";
        /// <summary>The weapons that get a v2 intro (the 12 base weapons, the Katana, the Glaive, the Fangs).</summary>
        public static readonly string[] WeaponSpecs={"sword","daggers","shield","greatsword","pistol","rifle","machinegun","bow","fire","water","lightning","darkness",NativeKatana.Spec,NativeGlaive.Spec,NativeFangs.Spec};

        // ======== THE REGISTRY: one line per intro, on your slot. Keep the blank comment lines between slots. ========
        private static readonly NativeIntroEntry[] All=
        {
            // ---- I0 (framework): the showcase that proves the pipeline (DEV only; may be removed later) ----
            NativeIntroEntry.Dev("intro2-showcase","DEV · Pipeline Proof",NativeIntroShowcase.Total,NativeIntroShowcase.CardAt,()=>new NativeIntroShowcase(),"I0","The v2 pipeline end to end: a crane out of the dark, a column of light, the hero frame."),
            //
            // ---- the new default (I2) ----
            NativeIntroEntry.Default("Cloudbreak",NativeIntroCloudbreak.Total,NativeIntroCloudbreak.CardAt,()=>new NativeIntroCloudbreak(),"I2"),
            //
            // ---- generic intros (I2: five) ----
            //
            NativeIntroEntry.Generic("whiteout","Whiteout",NativeIntroWhiteout.Total,NativeIntroWhiteout.CardAt,()=>new NativeIntroWhiteout(),"I2"),
            //
            NativeIntroEntry.Generic("slipstream","Slipstream",NativeIntroSlipstream.Total,NativeIntroSlipstream.CardAt,()=>new NativeIntroSlipstream(),"I2"),
            //
            NativeIntroEntry.Generic("threshold","Threshold",NativeIntroThreshold.Total,NativeIntroThreshold.CardAt,()=>new NativeIntroThreshold(),"I2"),
            //
            NativeIntroEntry.Generic("flashbulb","Flashbulb",NativeIntroFlashbulb.Total,NativeIntroFlashbulb.CardAt,()=>new NativeIntroFlashbulb(),"I2"),
            //
            // generic slot 5
            //
            // ---- weapon intros: NativeIntroEntry.Weapon("<spec>", ...) ----
            //
            // sword
            //
            // daggers
            //
            // shield
            //
            // greatsword
            //
            // pistol
            //
            // rifle
            //
            // machinegun
            //
            // bow
            //
            // fire
            //
            // water
            //
            // lightning
            //
            // darkness
            //
            // katana
            //
            // glaive
            //
            // fangs
            //
            NativeIntroEntry.Dev("architect","DEV · Architect of Reality",NativeIntroArchitect.Total,NativeIntroArchitect.CardAt,()=>new NativeIntroArchitect(),"I1","The arena dissolves into code and light, and you rebuild it around you. The world obeys."),
            //
        };
        // ======== end of the registry ========

        private static Dictionary<string,NativeIntroEntry> byId;
        private static Dictionary<string,NativeIntroEntry> Index
        {
            get
            {
                if(byId!=null)return byId;var d=new Dictionary<string,NativeIntroEntry>(StringComparer.Ordinal);
                foreach(var e in All){if(e==null)continue;if(d.ContainsKey(e.Id))throw new InvalidOperationException("NativeIntroCatalog: "+e.Id+" registered twice");d.Add(e.Id,e);}
                return byId=d;
            }
        }
        public static IEnumerable<NativeIntroEntry> Entries{get{foreach(var e in All)if(e!=null)yield return e;}}
        /// <summary>The registered intro `id`, or null.</summary>
        public static NativeIntroEntry Find(string id)=>id!=null&&Index.TryGetValue(id,out var e)?e:null;
        public static bool Registered(string id)=>Find(id)!=null;
        /// <summary>The maker of registered intro `id` (NativeEntranceDirector.Script builds it once), or null.</summary>
        public static Func<NativeEntranceScript> Maker(string id)=>Find(id)?.Make;
        /// <summary>Weapon `spec`'s registered v2 intro id, or null (then its old signature still plays).</summary>
        public static string WeaponIntro(string spec){if(spec==null)return null;var e=Find(WeaponPrefix+spec);return e!=null&&e.Kind==NativeIntroKind.Weapon?e.Id:null;}
        /// <summary>The new default is registered.</summary>
        public static bool DefaultReady=>Find(DefaultId)!=null;
        /// <summary>DEV only: needs the owner's live DEV access to be listed, picked or staged from a pick.</summary>
        public static bool DevOnly(string id){var e=Find(id);return e!=null&&e.Kind==NativeIntroKind.Dev;}
        /// <summary>Whether `access` may pick registered intro `id` (DEV ones: live DEV access; the rest: everyone).</summary>
        public static bool Allowed(string id,NativeAccess access,double now){var e=Find(id);if(e==null)return false;if(e.Kind!=NativeIntroKind.Dev)return true;var a=access??NativeAccess.Guest;return a.Developer&&a.Expires>now;}

        // ---- the archive ----
        /// <summary>The DEV classes whose own entrance is archived (they take their base weapon's v2 intro, else the new default).</summary>
        public static readonly string[] ArchivedClasses={"emperor","ansatsuken","leet",CyberLordSpec.Id};
        /// <summary>
        /// Archived (owner 2026-11-20): every intro before v2 except the Top Dog's Order Up! and the Bombadier's Last Warning. Their
        /// code and data stay and they can still be staged (a replicated style from an older host, a weapon whose v2 intro is not
        /// registered yet), but they are hidden from the Locker and a saved, profile or replicated pick of one migrates away
        /// (NativeIntroRules.Migrate / Style: the pilot's weapon's intro).
        /// </summary>
        public static bool Archived(string id)
        {
            if(id==null||id==NativeTopDog.Entrance||id==NativeBombs.Entrance||Registered(id))return false;
            if(Array.IndexOf(NativeIntroRules.Entrances,id)>=0||Array.IndexOf(NativeIntroRules.Retired,id)>=0)return true;
            return id==NativeCosmeticGate.SheldonIntro||id==CyberLordSpec.Entrance||id==NativeGlaiveIntro.Id||id==NativeKatana.Entrance||id==NativeFangs.Entrance||id==NativeDevMotions.GreatswordEntrance||id==NativeDevMotions.KatanaEntrance||id.StartsWith("dev-",StringComparison.Ordinal);
        }
        /// <summary>
        /// A pilot's own entrance under v2 when it picked nothing (or its pick is archived), or null to keep today's rule: its
        /// weapon's registered v2 intro (a DEV animation class its base weapon's), else for an archived DEV class the new default
        /// once registered. The Siren (the boss's own entrance), the Top Dog and the Bombadier keep theirs.
        /// </summary>
        public static string Signature(string spec)
        {
            if(spec==null||spec=="siren"||spec==NativeTopDog.Spec||spec==NativeBombs.Spec)return null;
            var own=WeaponIntro(spec)??WeaponIntro(NativeDevMotions.Base(spec));if(own!=null)return own;
            // (a DEV animation class keeps its own entrance until its base weapon's v2 intro is registered)
            return Array.IndexOf(ArchivedClasses,spec)>=0&&DefaultReady?DefaultId:null;
        }
        /// <summary>The ids the Locker lists for `access` (after the kept DEV classes' two): the registered v2 intros it may pick,
        /// the default first, then generic, weapon and DEV.</summary>
        public static void Listed(List<string> into,NativeAccess access,double now)
        {
            for(int pass=0;pass<4;pass++)foreach(var e in Entries)
            {
                int order=e.Kind==NativeIntroKind.Default?0:e.Kind==NativeIntroKind.Generic?1:e.Kind==NativeIntroKind.Weapon?2:3;
                if(order==pass&&Allowed(e.Id,access,now)&&!into.Contains(e.Id))into.Add(e.Id);
            }
        }
    }
}
