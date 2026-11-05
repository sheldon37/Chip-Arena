using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>The fifty wild chips (owner 2026-11-04, "fun to use chips ... rounds inspired"; docs/CHIP-EXPANSION.md). Ordinary rarities in the ordinary draft.
    /// Generated from one table with the doc's rows. Mechanic chips name their mechanic and are refused by NativeArena.ImplementedChip until the rules support it.
    /// Nothing calls Apply until the rules are in; Astra adds the call beside NativeBombChips.Apply.</summary>
    public static class NativeWildChips
    {
        public const string Prefix="wild-";
        public static bool Is(string id)=>id!=null&&id.StartsWith(Prefix,StringComparison.Ordinal);
        private static ContentChip C(string id,string name,string rarity,string tag,int max,string icon,string text,bool mechanic,params ChipEffect[] effects)=>new ContentChip{id=Prefix+id,name=name,description=text,rarity=rarity,max=max,icon=icon,tags=new[]{tag},excludes=Array.Empty<string>(),mechanic=mechanic?Prefix+id:null,effects=effects};
        public static ContentChip[] Chips()=>new[]{
            C("top-shape","Top Shape","common","all",3,"heart-plus","Hit harder while you're healthy.",true),
            C("cornered","Cornered","common","all",3,"shield-alert","Fight faster when you're nearly dead.",true),
            C("hangtime","Hangtime","common","all",3,"move-up","Hit harder from the air.",true),
            C("planted","Planted","common","all",3,"anchor","Stand still, hit harder.",true),
            C("frenzy-scent","Frenzy Scent","common","all",3,"footprints","Hurting someone makes you quick.",true),
            C("opening-move","Opening Move","common","all",3,"sword","Your first hit lands heavy.",true),
            C("thick-skull","Thick Skull","common","all",2,"brain","Headshots barely count.",true),
            C("spin-up","Spin Up","common","ranged",2,"rotate-cw","Hold the trigger, fire faster.",true),
            C("bloodhound","Bloodhound","uncommon","all",2,"target","Run faster at the nearest enemy.",true),
            C("radar-block","Radar Block","uncommon","all",1,"radio","A Block shows you everyone.",true),
            C("charged-reply","Charged Reply","uncommon","all",2,"zap","Your next attack after a Block hits hard.",true),
            C("tapeworm","Tapeworm","uncommon","all",2,"biohazard","Your hits weaken their hits.",true),
            C("cold-shoulder","Cold Shoulder","uncommon","all",2,"snowflake","Enemies near you slow down.",true),
            C("hand-to-mouth","Hand to Mouth","uncommon","ranged",1,"hand-coins","Hits put rounds back in the magazine.",true,NativeMovementContent.Effect("ammoCapacity","mul",0.8)),
            C("trick-shot","Trick Shot","uncommon","projectile",2,"corner-up-right","Every bounce makes it hit harder.",true,NativeMovementContent.Effect("bounces","add",1)),
            C("snowball","Snowball","uncommon","projectile",2,"expand","Shots grow the farther they fly.",true),
            C("bullet-hose","Bullet Hose","uncommon","ranged",1,"activity","Spray and pray.",true,NativeMovementContent.Effect("attackRate","mul",2.5),NativeMovementContent.Effect("ammoCapacity","mul",2),NativeMovementContent.Effect("damage","mul",0.4)),
            C("hot-swap","Hot Swap","uncommon","ranged",2,"radiation","Reloading sends out a shock.",true,NativeMovementContent.Effect("reloadSpeed","mul",1.1)),
            C("afterimage","Afterimage","uncommon","all",1,"copy","Your dodge leaves a decoy behind.",true),
            C("mana-tap","Mana Tap","uncommon","magic",3,"droplets","Hits refill your mana.",true),
            C("follow-through","Follow Through","uncommon","melee",3,"swords","Hits give your stamina back.",true),
            C("steering-wheel","Steering Wheel","rare","projectile",1,"spline","Shots steer toward your crosshair.",true,NativeMovementContent.Effect("projectileSpeed","mul",0.7)),
            C("curveball","Curveball","rare","projectile",1,"orbit","Shots bend the way you were turning.",true),
            C("anti-gravity","Anti-Gravity","rare","all",1,"move-up","Every fourth hit makes them fall upward.",true),
            C("battering-block","Battering Block","rare","all",1,"shield","A Block throws you forward.",true),
            C("blink-block","Blink Block","rare","all",1,"undo-2","A Block teleports you where you aim.",true,NativeMovementContent.Effect("blockWindow","mul",0.7)),
            C("hush","Hush","rare","all",1,"volume-2","A Block silences everyone near you.",true),
            C("overkill","Overkill","rare","all",1,"split","Extra damage jumps to the next enemy.",true),
            C("buzz-saws","Buzz Saws","rare","all",2,"circle-dashed","Saw blades circle you.",true,NativeMovementContent.Effect("speed","mul",0.95)),
            C("double-tap","Double Tap","rare","all",1,"copy","Every attack repeats as a ghost.",true),
            C("spite","Spite","rare","all",1,"skull","When you die, you explode.",true),
            C("splat","Splat","rare","all",2,"brick-wall","Walls hurt.",true,NativeMovementContent.Effect("impulse","mul",1.5)),
            C("shrink-ray","Shrink Ray","rare","all",1,"shrink","Your hits shrink them.",true),
            C("jet-rounds","Jet Rounds","rare","projectile",1,"rocket","Hits strap a rocket to them.",true),
            C("slow-bleed","Slow Bleed","epic","all",1,"hourglass","Damage reaches you over 4 s.",true,NativeMovementContent.Effect("hp","mul",0.9)),
            C("blood-magazine","Blood Magazine","epic","ranged",1,"heart-crack","Never reload. Every shot costs you health.",true),
            C("wall-hack","Wall Hack","epic","projectile",1,"brick-wall","Shots pass through the first wall.",true,NativeMovementContent.Effect("damage","mul",0.75)),
            C("bank-error","Bank Error","epic","projectile",1,"coins","Bounces aim at the nearest enemy.",true,NativeMovementContent.Effect("bounces","add",1)),
            C("flanker","Flanker","epic","projectile",1,"move","A second shot hits them from behind.",true),
            C("bodyguard-drone","Bodyguard Drone","epic","all",1,"bot","A drone shoots down incoming shots.",true),
            C("doomsday-clock","Doomsday Clock","epic","all",1,"clock-3","Hold still for 3 s and everything near you pays.",true),
            C("domino","Domino","epic","all",1,"network","Enemies near your target share the hit.",true),
            C("head-stomp","Head Stomp","epic","all",1,"chevrons-down-up","Land on someone.",true),
            C("triple-tap","Triple Tap","epic","ranged",1,"rows-3","Every shot is a three-round burst.",true,NativeMovementContent.Effect("damage","mul",0.5),NativeMovementContent.Effect("attackRate","mul",0.6)),
            C("mint-condition","Mint Condition","legendary","all",1,"gem","Untouched, you are nearly unkillable.",true),
            C("all-in","All In","legendary","ranged",1,"bomb","One shot. The whole magazine.",true,NativeMovementContent.Effect("reloadSpeed","mul",1.6)),
            C("spare-body","Spare Body","legendary","all",1,"ghost","Once a round, die and come back.",true),
            C("time-thief","Time Thief","legendary","all",1,"hourglass","Hits steal cooldown.",true),
            C("haymaker","Haymaker","mythic","all",1,"hand-fist","Every 12 s your next hit deals six times the damage.",true),
            C("evil-twin","Evil Twin","mythic","all",1,"users","A copy of you fights at your side.",true)};
        /// <summary>The mechanics the rules must support (a chip whose stats are the whole chip has none).</summary>
        public static string[] Mechanics()=>Chips().Where(x=>x.mechanic!=null).Select(x=>x.mechanic).ToArray();
        /// <summary>Adds the rows to the content (nothing if they are there).</summary>
        public static void Apply(ConversionContent content)
        {
            if(content.chips.Any(x=>Is(x.id)))return;content.chips=content.chips.Concat(Chips()).ToArray();
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nwild-chips-v1");
        }
        private static Dictionary<string,string> icons;
        /// <summary>The chip's icon (null: not one of these).</summary>
        public static string Icon(string id){if(!Is(id))return null;if(icons==null){icons=new Dictionary<string,string>();foreach(var x in Chips())icons[x.id]=x.icon;}return icons.TryGetValue(id,out var i)?i:null;}
    }
}
