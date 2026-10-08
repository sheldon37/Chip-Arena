using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeBotOption {public string weapon="random",difficulty="normal";public int team=-1;}
    [Serializable] public sealed class NativeRoomOptions
    {
        public string map="moonlit",botDifficulty="normal";public bool cooperative,teams,friendlyFire,skipBotDrafts,bothDraft,skipPlayerIntros,randomizeMap,mirrorWeapons,stationary,skipWaves,autoStart=true;
        public int bots=3,rounds=2,sets=5,choices=6,selectionSeconds=18,hostTeam;public NativeBotOption[] botOptions=Array.Empty<NativeBotOption>();
        public void Normalize(ConversionContent content)
        {
            if(map!="random"&&!content.maps.Any(m=>m.id==map))map=content.maps.First(m=>m.id!="siren").id;
            bots=Math.Max(0,Math.Min(cooperative?3:7,bots));rounds=Math.Max(1,Math.Min(10,rounds));sets=Math.Max(1,Math.Min(20,sets));choices=Math.Max(1,Math.Min(12,choices));selectionSeconds=Math.Max(5,Math.Min(120,selectionSeconds));hostTeam=Math.Max(0,Math.Min(7,hostTeam));
            botDifficulty=NativeLobbyRules.Difficulty(botDifficulty);botOptions=(botOptions??Array.Empty<NativeBotOption>()).Take(7).Select(b=>b??new NativeBotOption()).ToArray();
            foreach(var b in botOptions){if(b.weapon!="random"&&!content.weapons.Any(w=>w.id==b.weapon&&!w.devOnly))b.weapon="random";b.team=Math.Max(-1,Math.Min(7,b.team));b.difficulty=NativeLobbyRules.Difficulty(b.difficulty);}
        }
    }
    public static class NativeLobbyRules
    {
        public static string PreviewMap(string selected,bool cooperative)=>cooperative?"siren":selected=="random"?"moonlit":selected;
        public static string Difficulty(string value)=>value=="dummy"||value=="easy"||value=="hard"?value:"normal";
        public static int DefaultTeam(int id,bool cooperative)=>cooperative?0:Math.Max(0,id-1)%2;
        public static NativeSeat Bot(ConversionContent content,NativeRoomOptions options,int index,string hostWeapon,uint seed)
        {
            var config=index<options.botOptions.Length?options.botOptions[index]:null;var random=new SourceRandom(seed+(uint)(index*7919));var weapons=content.weapons.Where(w=>!w.devOnly&&(!options.cooperative||w.@class=="ranged"||w.@class=="melee")).ToArray();
            string spec=options.mirrorWeapons?hostWeapon:config?.weapon??"random";if(spec=="random")spec=weapons[Math.Min(weapons.Length-1,(int)(random.Next()*weapons.Length))].id;
            string[] names={"Nova","Circuit","Echo","Flux","Pixel","Comet","Volt"};string[] colors={"#70e1d0","#ff776d","#bca7ff","#eac558","#75aaff","#f09fc7","#a3d876"};
            return new NativeSeat{id=100+index,name=names[index%names.Length],spec=spec,bot=true,team=options.cooperative?0:config?.team>=0?config.team:(index+1)%2,difficulty=config?.difficulty??options.botDifficulty,accentColor=colors[index%colors.Length],bodyColor=colors[(index+3)%colors.Length],hat=index%2==0?"cap":"none"};
        }
    }
}
