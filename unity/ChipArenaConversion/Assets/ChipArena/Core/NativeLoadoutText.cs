using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // The source's class and specialization card text and grouping, for the menus' class cards and weapon cards:
    // CLASS_INFO names and defaults (shared/content.ts:175), the class taglines (client/main.ts:119), weaponInClass and
    // availableClasses (shared/lobby-presentation.ts:12-15) and the specialization chip's description and stat line
    // (client/main.ts:157). Presentation only: picking still sets the one preferred weapon the game already saves.
    public static class NativeLoadoutText
    {
        public static readonly string[] Classes={"ranged","magic","melee","dev"};
        public static string Name(string c)=>c=="ranged"?"Ranged":c=="magic"?"Magic":c=="melee"?"Melee":"???";
        public static string Tagline(string c)=>c=="ranged"?"PRECISION & PRESSURE":c=="magic"?"ELEMENTS & CONTROL":c=="melee"?"CLOSE & PERSONAL":"DEV CLASSES";
        public static string Default(string c)=>c=="ranged"?"pistol":c=="magic"?"fire":c=="melee"?"sword":"emperor";
        // The DEV class is offered to developers only; the other three to everyone.
        public static bool Offered(string c,bool developer)=>c!="dev"||developer;
        // weaponInClass: DEV holds the developer weapons (developers only); every other class holds its own weapons, a
        // restricted one only when `allowed` (a developer, or an account granted that weapon).
        public static bool InClass(ContentWeapon w,string c,bool developer,bool allowed)=>w!=null&&allowed&&(c=="dev"?developer&&w.devOnly:w.@class==c);
        // The class a weapon is shown under when no class has been picked yet.
        public static string ClassOf(ContentWeapon w,bool developer)=>w==null?"ranged":w.devOnly&&developer?"dev":w.@class;
        // preferredWeapon: the current weapon when it is in the class, otherwise the class default (or, when the default is
        // not available, the first weapon of the class in content order).
        public static string Pick(IList<ContentWeapon> weapons,string current,string c,bool developer,Func<ContentWeapon,bool> allowed)
        {
            ContentWeapon first=null,fallback=null;
            foreach(var w in weapons)
            {
                if(!InClass(w,c,developer,allowed(w)))continue;if(w.id==current)return w.id;
                if(first==null)first=w;if(w.id==Default(c))fallback=w;
            }
            return (fallback??first)?.id??current;
        }
        public static string Description(ContentWeapon w)=>Text(w)+(w!=null&&w.gravity>0?" Projectiles fall; aim higher at range.":"");
        // The weapon's description as the menus show it: content's, verbatim, except the DEV Cyber Lord's, whose content still
        // names its v1 kit (NativeCyberLordText.Description: Kit v2, the shipped kit).
        public static string Text(ContentWeapon w)=>w==null?"":w.id==CyberLordSpec.Id?NativeCyberLordText.Description:w.description??"";
        // The weapon tiles' info strip (shown on hover, under the grid): a one-line role drawn from each description, without
        // ability names so it stays true as kits change. Another weapon shows its description's first sentence.
        public static string Role(ContentWeapon w)
        {
            switch(w?.id)
            {
                case null:return "";
                case "siren":return "DEV boss · melee and ranged modes";
                case "emperor":return "DEV · chained punches and launches";
                case "ansatsuken":return "DEV · arcade punches, kicks and specials";
                case "leet":return "DEV · binary bolts that lock on";
                case "cyberlord":return "DEV · hardlight fists and cyber spells";
                case "pistol":return "Precise sidearm · 7-round magazine";
                case "rifle":return "Long-range sniper · 3 heavy rounds";
                case "machinegun":return "Rapid automatic fire · 48 rounds";
                case "bow":return "Draw to charge · arrows arc and stick";
                case "fire":return "Firebolts that blast and burn";
                case "water":return "Fast globes that soak and slow";
                case "lightning":return "Rapid bolts that chain between enemies";
                case "darkness":return "Heavy orbs that drain health";
                case "sword":return "Three flowing cuts with hitstun";
                case "daggers":return "Fast alternating cuts and low lunges";
                case "shield":return "Slams that knock enemies down";
                case "greatsword":return "Low sweeping cuts, pivoting footwork";
            }
            string d=w.description??"";int end=d.IndexOf(". ",StringComparison.Ordinal);return (end>0?d.Substring(0,end):d).TrimEnd('.');
        }
        // `${damage} DMG · ${(1/interval).toFixed(1)} / SEC` + cost (STAMINA for melee, MANA otherwise) + melee range.
        // Invariant formatting: comma-decimal Windows locales must still read 2.5, not 2,5.
        public static string Stats(ContentWeapon w)
        {
            if(w==null)return "";var inv=CultureInfo.InvariantCulture;bool melee=w.@class=="melee";
            // The Bow's damage is its draw's (Astra's NativeBowRules: quick shot to full draw at .8 s), not one shot a second.
            if(w.id=="bow")return NativeBowRules.Damage(0).ToString(inv)+"\u2013"+NativeBowRules.Damage(1).ToString(inv)+" DMG · "+(NativeBow.FullDraw).ToString("0.0",inv)+" S DRAW";
            string s=w.damage.ToString(inv)+" DMG · "+(w.interval>0?(1/w.interval).ToString("0.0",inv):"0.0")+" / SEC";
            if(w.cost!=0)s+=" · "+w.cost.ToString(inv)+(melee?" STAMINA":" MANA");
            if(melee)s+=" · "+w.range.ToString(inv)+" M";
            return s;
        }
    }

    /// <summary>
    /// The DEV Cyber Lord's menu text for Kit v2, the shipped kit (Astra's authority, PR #231; the names, keys, icons, cooldowns
    /// and costs from CyberKit2Spec, docs/CYBER-LORD.md "Kit v2"). Astra's content (GameContent.json from
    /// tools/native-content.json) still carries the v1 kit's names and text under its v1 ids (data-dash, firewall, packet-burst,
    /// siphon-link, system-crash), so the presentation shows these in their place: the weapon description (NativeLoadoutText),
    /// the Locker's ability rows (NativeLocker.SlotOptions) and the catalogue. The ids stay; Kernel Panic, unchanged in v2,
    /// keeps its content entry. Presentation only.
    /// </summary>
    public static class NativeCyberLordText
    {
        private static string N(float x)=>x.ToString("0.#",CultureInfo.InvariantCulture);
        /// <summary>One line per v2 slot (CyberKit2Spec order), for the Locker's ability cards.</summary>
        public static readonly string[] Texts=
        {
            "Fire a hardlight cable up to "+N(CyberKit2Spec.UplinkRange)+" m: zip to the surface it hits, or yank an enemy in and stun them. A miss retracts.",
            "Overclock for "+N(CyberKit2Spec.OverclockSeconds)+" s: faster movement and attacks, cooldowns recover twice as fast, melee hits arc to a nearby enemy and ranged throws split in three.",
            "Five hardlight strikes: jab, cross, spinning backfist, rising uppercut and a Pulse Palm shockwave.",
            "Three hardlight clones copy your combo for "+N(CyberKit2Spec.ForkSeconds)+" s, then dash to enemies and burst.",
            "Dash-slash "+N(CyberKit2Spec.CutDistance)+" m through enemies; everyone you pass is marked, and the marks detonate together.",
            "Leap, aim a landing up to "+N(CyberKit2Spec.MeteorRange)+" m away, then dive with a giant hardlight fist that launches enemies.",
            "Lunge up to "+N(NativeKernelPanicRules.Range)+" m with a wide sweep; connect to isolate one enemy for a cinematic combo.",
            "Cyber fireballs from alternating hands while you move; every third is an Overflow from both hands with a bigger blast.",
            "Chain lightning: a bolt to the aimed enemy hops to up to "+CyberKit2Spec.Hops+" more, weaker each hop, slowing each.",
            "Throw an orb that becomes a black hole, pulling enemies in for "+N(CyberKit2Spec.HoleSeconds)+" s before it implodes and throws them out.",
            "Mark a floor point up to "+N(CyberKit2Spec.StormRange)+" m away; "+CyberKit2Spec.Drops+" cyber meteors rain into the ring.",
            "Call down an orbital laser for "+N(CyberKit2Spec.OrbitalSeconds)+" s and steer it with your aim; shares Kernel Panic's cooldown.",
        };
        /// <summary>The weapon description: both modes' keys and names.</summary>
        public static readonly string Description=BuildDescription();
        private static string BuildDescription()
        {
            string Keys(int from,int to){var b=new System.Text.StringBuilder();for(int s=from;s<=to;s++){if(b.Length>0)b.Append("; ");b.Append(CyberKit2Spec.Keys[s]).Append(' ').Append(CyberKit2Spec.Names[s]);}return b.ToString();}
            return "DEV hardlight brawler and cyber mage. "+Keys(CyberKit2Spec.Q,CyberKit2Spec.Panic)+". "+CyberRangedSpec.ModeKey+" Reconfigure to ranged: "+Keys(CyberKit2Spec.RangedLmb,CyberKit2Spec.Orbital)+".";
        }
        /// <summary>The v2 slot a Cyber Lord content ability's key shows (q Uplink, e Overclock, 1 Fork Bomb, 2 Glitch Cut, 3
        /// Meteor Byte), else -1 (4 is Kernel Panic, unchanged: its content entry stands; any other weapon or key).</summary>
        public static int Slot(ContentWeapon w,ContentAbility a)
        {
            if(w==null||a==null||w.id!=CyberLordSpec.Id||a.spec!=CyberLordSpec.Id)return -1;
            switch(a.slot){case "q":return CyberKit2Spec.Q;case "e":return CyberKit2Spec.E;case "1":return CyberKit2Spec.Fork;case "2":return CyberKit2Spec.Cut;case "3":return CyberKit2Spec.Meteor;}
            return -1;
        }
    }
}
