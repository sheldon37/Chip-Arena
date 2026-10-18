using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeChipPack
    {
        public int schema;public string id,name,version;public string[] requires;public ContentChip[] chips;
    }
    public sealed class NativeLoadedPack {public NativeChipPack Pack;public string Digest;}
    public static class NativeMods
    {
        public const int MaxPacks=16,MaxChips=64,MaxCatalog=512,MaxFileBytes=131072;
        public const int Airborne=1,Overclocked=2,Pinball=4;
        private static readonly Regex Id=new Regex("^[a-z][a-z0-9-]{1,23}$",RegexOptions.CultureInvariant);
        private static readonly Regex Version=new Regex("^[0-9]{1,4}\\.[0-9]{1,4}\\.[0-9]{1,4}$",RegexOptions.CultureInvariant);
        private static readonly HashSet<string> Stats=new HashSet<string>{"extraAirJumps","speed","jumpPower","playerGravity","rollDistance","rollSpeed","rollCost","staminaRegen","maxStamina","dashCooldown","bounces","impulse","projectileSpeed","projectileSize","attackRate","damage","hp","knockbackTaken"};
        private static readonly HashSet<string> Tags=new HashSet<string>{"all","melee","ranged","magic","projectile","roll"};
        public static void Validate(NativeChipPack pack,ConversionContent content)
        {
            void Require(bool ok,string why){if(!ok)throw new ArgumentException("Chip pack: "+why);}
            Require(pack!=null,"missing manifest");Require(pack.schema==1,"unsupported schema (expected 1)");
            Require(pack.id!=null&&Id.IsMatch(pack.id),"invalid pack id");Require(pack.version!=null&&Version.IsMatch(pack.version),"version must be major.minor.patch");
            Require(!string.IsNullOrWhiteSpace(pack.name)&&pack.name.Length<=48&&!pack.name.Any(char.IsControl),"invalid display name");
            Require(pack.chips!=null&&pack.chips.Length>0&&pack.chips.Length<=MaxChips,"expected 1–64 chips");
            Require(pack.requires==null||pack.requires.Length<=MaxPacks&&pack.requires.All(x=>x!=null&&x.Length<=40),"invalid dependencies");
            var ids=new HashSet<string>();
            foreach(var c in pack.chips)
            {
                Require(c!=null&&c.id!=null&&Id.IsMatch(c.id)&&ids.Add(c.id),"invalid or duplicate chip id");
                Require(!string.IsNullOrWhiteSpace(c.name)&&c.name.Length<=48&&!c.name.Any(char.IsControl),"invalid chip name");
                Require(c.description!=null&&c.description.Length<=240&&!c.description.Any(char.IsControl),"invalid chip description");
                Require(content.rarities.Any(r=>r.id==c.rarity&&r.weight>0),"unsupported rarity");
                Require(c.max>=1&&c.max<=10,"stack limit must be 1–10");
                Require(string.IsNullOrEmpty(c.mechanic)&&(c.excludes==null||c.excludes.Length==0),"scripted mechanics/exclusions are not supported in schema 1");
                Require(c.tags!=null&&c.tags.Length>0&&c.tags.Length<=6&&c.tags.All(Tags.Contains),"unsupported tags");
                Require(c.effects!=null&&c.effects.Length>0&&c.effects.Length<=6,"expected 1–6 effects");
                foreach(var e in c.effects)
                {
                    Require(e!=null&&e.stat!=null&&Stats.Contains(e.stat)&&content.baseStats.Any(s=>s.name==e.stat),"unsupported effect stat");
                    Require(!double.IsNaN(e.value)&&!double.IsInfinity(e.value)&&(e.op=="mul"&&e.value>=.25&&e.value<=3||e.op=="add"&&e.value>=0&&e.value<=100),"effect outside schema 1 bounds");
                    if(e.stat=="extraAirJumps"||e.stat=="bounces")Require(e.op=="add"&&e.value==Math.Floor(e.value)&&e.value<=6,"jump/bounce effects must add 0–6 whole charges");
                }
            }
        }
        // Validate the entire selection before mutating anything. Ordering is stable on every peer.
        public static string Apply(ConversionContent content,int rules,IEnumerable<NativeLoadedPack> selection)
        {
            if((rules&~7)!=0)throw new ArgumentException("Unknown gameplay modifier");
            var packs=selection.OrderBy(p=>p?.Pack?.id,StringComparer.Ordinal).ToArray();
            if(packs.Length>MaxPacks)throw new ArgumentException("Too many chip packs");
            foreach(var p in packs){if(p==null||p.Digest==null||!Regex.IsMatch(p.Digest,"^[a-f0-9]{64}$"))throw new ArgumentException("Missing pack digest");Validate(p.Pack,content);}
            if(packs.Select(p=>p.Pack.id).Distinct().Count()!=packs.Length)throw new ArgumentException("Duplicate pack id");
            var versions=new HashSet<string>(packs.Select(p=>p.Pack.id+"@"+p.Pack.version));
            foreach(var p in packs)foreach(var dependency in p.Pack.requires??Array.Empty<string>())if(!versions.Contains(dependency))throw new ArgumentException("Missing dependency "+dependency+" required by "+p.Pack.id);
            if(packs.Sum(p=>p.Pack.chips.Length)>MaxChips)throw new ArgumentException("At most 64 external chips may be enabled");
            var additions=packs.SelectMany(p=>p.Pack.chips.Select(c=>new ContentChip{id=p.Pack.id+":"+c.id,name=c.name,description=c.description,rarity=c.rarity,max=c.max,
                icon="sparkles",tags=c.tags.ToArray(),excludes=Array.Empty<string>(),effects=c.effects.Select(e=>new ChipEffect{stat=e.stat,op=e.op,value=e.value}).ToArray()})).ToArray();
            if(content.chips.Length+additions.Length>MaxCatalog)throw new ArgumentException("Chip catalog exceeds 512 entries");
            if(additions.Any(c=>content.chips.Any(old=>old.id==c.id)))throw new ArgumentException("External chip ID collision");
            if(rules==0&&packs.Length==0)return "Vanilla";
            var stats=content.baseStats.Select(s=>new NumberValue{name=s.name,value=s.value}).ToArray();
            void Scale(string id,double value){stats.Single(s=>s.name==id).value*=value;}
            if((rules&Airborne)!=0)stats.Single(s=>s.name=="extraAirJumps").value+=2;
            if((rules&Overclocked)!=0)Scale("speed",1.25);
            if((rules&Pinball)!=0){Scale("impulse",2);Scale("knockbackTaken",1.5);}
            string fingerprint=NativeMapRules.Revision("mods-v1|"+rules+"|"+string.Join("|",packs.Select(p=>p.Pack.id+"@"+p.Pack.version+":"+p.Digest)));
            content.baseStats=stats;content.chips=content.chips.Concat(additions).ToArray();content.contentRevision=NativeMapRules.Revision(content.contentRevision+"|"+fingerprint);
            return fingerprint;
        }
    }
}
