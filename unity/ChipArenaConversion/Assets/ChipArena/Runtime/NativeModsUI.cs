using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool activeRoundsCardsPlus;
        private string modsError="",modsFingerprint="Vanilla";private string[] modsFiles=Array.Empty<string>();private int activeModRules;private string[] activeChipPacks=Array.Empty<string>();
        private const string ModsHelp="Chip Arena JSON chip packs, schema 1\nUse Settings > Mods > Create example chip pack. Enable the file and restart. Share the exact JSON with friends; they must enable the same rules and packs. No code, DLLs or automatic downloads.\nPack/chip IDs: 2-24 lowercase letters/digits/hyphens, starting with a letter. Version: major.minor.patch. Dependencies: requires [pack-id@version]. Runtime chip IDs: pack-id:chip-id.\n16 enabled packs, 64 external chips total, 128 KiB/file. Ordinary rarities; tags all/melee/ranged/magic/projectile/roll. 1-10 stacks, 1-6 effects/chip.\nStats: extraAirJumps, speed, jumpPower, playerGravity, rollDistance, rollSpeed, rollCost, staminaRegen, maxStamina, dashCooldown, bounces, impulse, projectileSpeed, projectileSize, attackRate, damage, hp, knockbackTaken.\nEffects: add 0-100, mul 0.25-3. extraAirJumps and bounces require add with a whole number 0-6. Optional author at pack or chip level (chip overrides pack), up to 48 plain-text characters. No set, mechanic or excludes. Names up to 48 characters; descriptions up to 240.\nAir jumps add to the weapon's natural budget, capped at 6. Speed caps at 2.5x; gravity at 0.1-3x; jump power at 0.1-2.5x. Invalid enabled packs block hosting/joining; disable and restart. Selections and packs persist across game updates.\n";
        private string ModsDirectory=>Path.Combine(NativeUserDataPath,"Mods");
        private bool ModsReady{get{if(modsError.Length==0||activeModRules==0&&activeChipPacks.Length==0)return true;networkStatus="Mods could not be loaded. Check Settings > Mods, then restart.";return false;}}
        private void LoadMods()
        {
            activeRoundsCardsPlus=prefs.roundsCardsPlus;if(activeRoundsCardsPlus){NativeRoundsCards.Apply(content);modsFingerprint=content.contentRevision;}
            activeModRules=prefs.modRules;activeChipPacks=(prefs.chipPacks??Array.Empty<string>()).ToArray();
            try
            {
                Directory.CreateDirectory(ModsDirectory);var notice=Resources.Load<TextAsset>("CardsPlus-LICENSE");if(notice!=null)File.WriteAllText(Path.Combine(ModsDirectory,"CardsPlus-LICENSE.txt"),notice.text);string readme=Path.Combine(ModsDirectory,"README.txt");if(!File.Exists(readme))File.WriteAllText(readme,ModsHelp);ScanMods();
                if(activeChipPacks.Length>NativeMods.MaxPacks||activeChipPacks.Distinct(StringComparer.Ordinal).Count()!=activeChipPacks.Length)throw new ArgumentException("Invalid pack selection");
                var packs=new List<NativeLoadedPack>();
                foreach(string name in activeChipPacks)
                {
                    if(string.IsNullOrEmpty(name)||name!=Path.GetFileName(name)||name.IndexOfAny(new[]{'/', '\\', ':'})>=0||!modsFiles.Contains(name))throw new ArgumentException("Selected pack is missing: "+name);
                    string path=Path.Combine(ModsDirectory,name);if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("Pack links are not supported: "+name);
                    using(var stream=File.OpenRead(path))
                    {
                        if(stream.Length>NativeMods.MaxFileBytes)throw new ArgumentException("Pack is larger than 128 KiB: "+name);
                        using(var reader=new StreamReader(stream)){var buffer=new char[NativeMods.MaxFileBytes+1];int count=reader.ReadBlock(buffer,0,buffer.Length);if(count>NativeMods.MaxFileBytes)throw new ArgumentException("Pack grew beyond its size limit: "+name);string json=new string(buffer,0,count);packs.Add(new NativeLoadedPack{Pack=JsonUtility.FromJson<NativeChipPack>(json),Digest=NativeMapRules.Revision(json)});}
                    }
                }
                modsFingerprint=NativeMods.Apply(content,activeModRules,packs);if(activeRoundsCardsPlus)modsFingerprint=content.contentRevision;
            }
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException){modsError=e.Message;Debug.LogWarning("Mods: "+modsError);}
        }
        private void ScanMods()=>modsFiles=Directory.GetFiles(ModsDirectory,"*.json",SearchOption.TopDirectoryOnly).Select(Path.GetFileName).OrderBy(x=>x,StringComparer.Ordinal).Take(64).ToArray();
        private void ModsBody()
        {
            Field("Mods");Fine("Active: "+(modsFingerprint=="Vanilla"?"Vanilla":modsFingerprint.Substring(0,12))+". Players must use the same rules and exact chip-pack files to join.");
            Fine("Selections apply after restarting Chip Arena. Packs add chips to draft and PvE offers; they do not grant chips immediately.");
            if(modsError.Length>0)Fine("Load error: "+modsError);
            GUILayout.Label("Gameplay modifiers",small);int flags=prefs.modRules;
            void Rule(int bit,string label){bool on=(flags&bit)!=0;if(GUILayout.Toggle(on,label))flags|=bit;else flags&=~bit;}
            Rule(NativeMods.Airborne,"Airborne: everyone gets +2 air jumps");Rule(NativeMods.Overclocked,"Overclocked: everyone gets +25% movement speed");Rule(NativeMods.Pinball,"Pinball: double outgoing knockback, +50% knockback taken");
            GUILayout.Label("Bundled adaptations",small);bool rounds=GUILayout.Toggle(prefs.roundsCardsPlus,"Cards+ adaptations: 20 chips · original author willis81808");
            Fine("Original Chip Arena implementations with adjusted effects. Author credit appears on each chip. MIT notice: CardsPlus-LICENSE.txt in the Mods folder.");
            if(rounds!=prefs.roundsCardsPlus){prefs.roundsCardsPlus=rounds;Save();}
            GUILayout.Label("External chip packs",small);bool changed=flags!=prefs.modRules;prefs.modRules=flags;var selected=new HashSet<string>(prefs.chipPacks??Array.Empty<string>(),StringComparer.Ordinal);
            foreach(string file in modsFiles.Concat(selected).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray())
            {bool old=selected.Contains(file),on=GUILayout.Toggle(old,file+(modsFiles.Contains(file)?"":" (missing)"));if(old!=on){changed=true;if(on)selected.Add(file);else selected.Remove(file);}}
            if(changed){prefs.chipPacks=selected.OrderBy(x=>x,StringComparer.Ordinal).ToArray();Save();}
            if(prefs.roundsCardsPlus!=activeRoundsCardsPlus||prefs.modRules!=activeModRules||!selected.SetEquals(activeChipPacks))Fine("Restart required to apply these selections.");
            if(GUILayout.Button("Disable all mods for next launch",button)){prefs.roundsCardsPlus=false;prefs.modRules=0;prefs.chipPacks=Array.Empty<string>();Save();}
            if(GUILayout.Button("Open Mods folder",button))Application.OpenURL(new Uri(ModsDirectory+Path.DirectorySeparatorChar).AbsoluteUri);
            if(GUILayout.Button("Create example chip pack",button))
            {try{string path=Path.Combine(ModsDirectory,"example-mobility.json");var pack=new NativeChipPack{schema=1,id="example-mobility",name="Example Mobility",version="1.0.0",requires=Array.Empty<string>(),chips=new[]{new ContentChip{id="hop-drive",name="Hop Drive",description="+1 extra mid-air jump and +8% movement speed.",rarity="rare",max=2,tags=new[]{"all"},effects=new[]{NativeMovementContent.Effect("extraAirJumps","add",1),NativeMovementContent.Effect("speed","mul",1.08)}}}};using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(var writer=new StreamWriter(stream))writer.Write(JsonUtility.ToJson(pack,true));ScanMods();networkStatus="Example created; enable it and restart to use it.";}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){networkStatus=e.Message;}}
            if(GUILayout.Button("Refresh pack list",button))try{ScanMods();}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){networkStatus=e.Message;}
            Fine("Schema 1: JSON chips using supported effects only. No DLLs, scripts or automatic downloads. Use Create example chip pack; instructions are in the Mods folder.");
        }
    }
}
