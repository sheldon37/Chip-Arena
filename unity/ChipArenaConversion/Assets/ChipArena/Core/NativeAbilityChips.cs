using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>Chips for one ability (owner 2026-11-04, "chips that directly effect weapons abilities (provided they have that specific ability slotted)"; docs/CHIP-EXPANSION.md).
    /// Generated from one table with the doc's rows. A chip is offered only while its ability is in the pilot's Q or E.
    /// Nothing calls Apply until the rules are in; Astra adds the call beside NativeBombChips.Apply and the slotted test to the draft's filter.</summary>
    public static class NativeAbilityChips
    {
        public const string Prefix="ab-";
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal);
        private static ContentChip C(string ability,string id,string name,string rarity,int max,string icon,string text)=>new ContentChip{id=Prefix+id,name=name,description=text,rarity=rarity,max=max,icon=icon,tags=new[]{"ability"},excludes=Array.Empty<string>(),mechanic=Prefix+id,effects=Array.Empty<ChipEffect>()};
        private static readonly string[,] Table={
            {"afterimage-cut","flash-step","Flash Step"},
            {"sky-splitter","rising-dragon","Rising Dragon"},
            {"perfect-form","riposte","Riposte"},
            {"moonlit-edge","iaido","Iaido"},
            {"knife-in-the-dark","shadow-step","Shadow Step"},
            {"twin-kunai","kunai-mark","Kunai Mark"},
            {"choking-smoke","smoke-veil","Smoke Veil"},
            {"barbed-chain","chain-hook","Chain Hook"},
            {"battering-ram","bulwark-charge","Bulwark Charge"},
            {"triple-bank","aegis-ricochet","Aegis Ricochet"},
            {"spiked-wall","bastion","Bastion"},
            {"aftershock-slam","ground-slam","Ground Slam"},
            {"fissure","earthsplitter","Earthsplitter"},
            {"eye-of-the-storm","whirlwind","Whirlwind"},
            {"deep-anchor","anchor-throw","Anchor Throw"},
            {"molten-trail","blade-drag","Blade Drag"},
            {"six-aces","fan-hammer","Fan the Hammer"},
            {"cluster-frag","frag-grenade","Frag Grenade"},
            {"lucky-coin","ricochet-coin","Ricochet Coin"},
            {"blinder","flash-round","Flash Round"},
            {"cooling-jacket","overdrive","Overdrive"},
            {"twin-rockets","rocket","Underbarrel Rocket"},
            {"second-gun","sentry-drop","Sentry Drop"},
            {"belt-feed","bipod","Bipod"},
            {"danger-close","orbital-mark","Orbital Mark"},
            {"hook-shot","grapple-line","Grapple Line"},
            {"deep-scan","sonar-dart","Sonar Dart"},
            {"hollow-point","breacher-round","Breacher Round"},
            {"wider-volley","volley","Volley"},
            {"burning-rain","rain-of-arrows","Rain Of Arrows"},
            {"twin-hawks","spirit-hawk","Spirit Hawk"},
            {"short-leash","tether-arrow","Tether Arrow"},
            {"white-heat","flame-lance","Flame Lance"},
            {"twin-meteor","meteor-strike","Meteor Strike"},
            {"phoenix-dive","ignition-leap","Ignition Leap"},
            {"deep-lungs","dragon-breath","Dragon Breath"},
            {"big-wave","tidal-wave","Tidal Wave"},
            {"scalding-steam","geyser-wall","Geyser Wall"},
            {"sacred-spring","healing-spring","Healing Spring"},
            {"double-bubble","bubble-trap","Bubble Trap"},
            {"double-blink","blink-strike","Blink Strike"},
            {"ball-bearing","ball-lightning","Ball Lightning"},
            {"full-circuit","static-recall","Static Recall"},
            {"sonic-boom","thunderclap","Thunderclap"},
            {"heavy-chains","shadow-shackles","Shadow Shackles"},
            {"trade-up","shade-swap","Shade Swap"},
            {"undertaker","grave-walk","Grave Walk"},
            {"soul-feast","soul-siphon","Soul Siphon"},
            // The four that are already in the game, once they are gated the same way.
            {"cinder-field","meteor-strike","Meteor Strike"},
            {"undertow","tidal-wave","Tidal Wave"},
            {"abyssal-core","singularity","Black Hole"},
            {"storm-recall","blink-strike","Blink Strike"}};
        public static ContentChip[] Chips()=>new[]{
            C("flash-step","afterimage-cut","Afterimage Cut","rare",1,"swords","Flash Step leaves a cut along its path."),
            C("rising-dragon","sky-splitter","Sky Splitter","rare",1,"move-up","Rising Dragon climbs higher and lands hard."),
            C("riposte","perfect-form","Perfect Form","rare",1,"shield-check","Riposte is easier, and a parry pays you back."),
            C("iaido","moonlit-edge","Moonlit Edge","epic",1,"moon","Iaido charges faster and cuts farther."),
            C("shadow-step","knife-in-the-dark","Knife in the Dark","rare",1,"sword","Your stab after Shadow Step always crits."),
            C("kunai-mark","twin-kunai","Twin Kunai","rare",1,"split","Kunai Mark throws two, and the mark lasts longer."),
            C("smoke-veil","choking-smoke","Choking Smoke","rare",1,"biohazard","Enemies in your smoke choke and slow down."),
            C("chain-hook","barbed-chain","Barbed Chain","rare",1,"spline","Chain Hook reaches farther, and bites."),
            C("bulwark-charge","battering-ram","Battering Ram","rare",1,"shield","Slam them into a wall with Bulwark Charge."),
            C("aegis-ricochet","triple-bank","Triple Bank","epic",1,"corner-up-right","Aegis Ricochet bounces to two more enemies."),
            C("bastion","spiked-wall","Spiked Wall","rare",1,"brick-wall","Bastion is tougher and hurts to touch."),
            C("ground-slam","aftershock-slam","Aftershock","rare",1,"waves","Ground Slam lands twice."),
            C("earthsplitter","fissure","Fissure","rare",1,"split","Earthsplitter leaves a crack that slows."),
            C("whirlwind","eye-of-the-storm","Eye of the Storm","epic",1,"orbit","Whirlwind drags enemies in."),
            C("anchor-throw","deep-anchor","Deep Anchor","rare",1,"anchor","Where your blade lands, enemies are pulled in."),
            C("blade-drag","molten-trail","Molten Trail","rare",1,"flame","Blade Drag leaves fire on the floor."),
            C("fan-hammer","six-aces","Six Aces","rare",1,"dices","The last shot of Fan Hammer hits twice as hard."),
            C("frag-grenade","cluster-frag","Cluster Frag","epic",1,"bomb","Frag Grenade bursts into three more."),
            C("ricochet-coin","lucky-coin","Lucky Coin","rare",1,"coins","The coin comes back faster and turns twice."),
            C("flash-round","blinder","Blinder","rare",1,"sun","Flash Round blinds longer and wider."),
            C("overdrive","cooling-jacket","Cooling Jacket","rare",1,"snowflake","Overdrive lasts longer and kicks less."),
            C("rocket","twin-rockets","Twin Rockets","epic",1,"rocket","Your rocket launches two."),
            C("sentry-drop","second-gun","Second Gun","rare",1,"crosshair","Your sentry is tougher and fires faster."),
            C("bipod","belt-feed","Belt Feed","rare",1,"layers","Bipod stays up longer and patches you up."),
            C("orbital-mark","danger-close","Danger Close","epic",1,"radiation","Orbital Mark hits harder and wider."),
            C("grapple-line","hook-shot","Hook Shot","rare",1,"spline","Your next shot after Grapple Line hits harder."),
            C("sonar-dart","deep-scan","Deep Scan","rare",1,"radio","Sonar Dart pings more, and farther."),
            C("breacher-round","hollow-point","Hollow Point","rare",1,"scan-line","Breacher Round slows whoever it hits."),
            C("volley","wider-volley","Wider Volley","rare",1,"rows-3","Volley fires two more arrows."),
            C("rain-of-arrows","burning-rain","Burning Rain","epic",1,"flame","Rain of Arrows sets the ground alight."),
            C("spirit-hawk","twin-hawks","Twin Hawks","epic",1,"feather","Spirit Hawk flies as a pair."),
            C("tether-arrow","short-leash","Short Leash","rare",1,"anchor","Tether Arrow holds tighter and snaps harder."),
            C("flame-lance","white-heat","White Heat","rare",1,"flame","Flame Lance burns twice as hot."),
            C("meteor-strike","twin-meteor","Twin Meteor","epic",1,"sparkles","A second meteor follows the first."),
            C("ignition-leap","phoenix-dive","Phoenix Dive","rare",1,"move-up","Press again in the air to dive, bursting on landing."),
            C("dragon-breath","deep-lungs","Deep Lungs","rare",1,"wind","Dragon Breath lasts longer and goes wider."),
            C("tidal-wave","big-wave","Big Wave","rare",1,"waves","Tidal Wave comes in taller and wider."),
            C("geyser-wall","scalding-steam","Scalding Steam","rare",1,"cloud-lightning","Geyser Wall leaves scalding steam."),
            C("healing-spring","sacred-spring","Sacred Spring","epic",1,"heart-plus","Healing Spring heals more and washes away burns and poison."),
            C("bubble-trap","double-bubble","Double Bubble","rare",1,"circle-dot","Bubble Trap bursts for damage when it pops."),
            C("blink-strike","double-blink","Double Blink","epic",1,"zap","Blink Strike can blink once more right after."),
            C("ball-lightning","ball-bearing","Ball Bearing","rare",1,"circle-dot","Ball Lightning moves faster and bounces."),
            C("static-recall","full-circuit","Full Circuit","rare",1,"battery-charging","Static Recall heals you when you bolt back."),
            C("thunderclap","sonic-boom","Sonic Boom","rare",1,"audio-lines","Thunderclap reaches farther and stuns longer."),
            C("shadow-shackles","heavy-chains","Heavy Chains","rare",1,"anchor","Shackles jump to a second enemy."),
            C("shade-swap","trade-up","Trade Up","rare",1,"shuffle","Shade Swap dazes them and heals you."),
            C("grave-walk","undertaker","Undertaker","rare",1,"ghost","Rising from Grave Walk bursts up through the floor."),
            C("soul-siphon","soul-feast","Soul Feast","epic",1,"heart-crack","Soul Siphon reaches farther and drains their resource.")};
        private static Dictionary<string,string> ability,names,icons;
        private static void Ensure()
        {
            if(ability!=null)return;ability=new Dictionary<string,string>();names=new Dictionary<string,string>();icons=new Dictionary<string,string>();
            for(int i=0;i<Table.GetLength(0);i++){string id=Table[i,0];bool old=i>=Table.GetLength(0)-4;string key=old?id:Prefix+id;ability[key]=Table[i,1];names[key]=Table[i,2];}
            foreach(var x in Chips())icons[x.id]=x.icon;
        }
        /// <summary>The ability a chip needs slotted (null: any chip that needs none).</summary>
        public static string Requires(string chip){if(chip==null)return null;Ensure();return ability.TryGetValue(chip,out var a)?a:null;}
        /// <summary>That ability's name, for the card (null: none).</summary>
        public static string RequiresName(string chip){if(chip==null)return null;Ensure();return names.TryGetValue(chip,out var a)?a:null;}
        /// <summary>The chip's icon (null: not one of these).</summary>
        public static string Icon(string id){if(!Is(id))return null;Ensure();return icons.TryGetValue(id,out var i)?i:null;}
        /// <summary>Adds the rows to the content (nothing if they are there).</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(x=>Is(x.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nability-chips-v1");
        }
    }
}
