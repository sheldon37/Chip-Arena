using System;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeFallenSeat
    {
        public int id,lives=5,reviver=-1;public float respawnAt=-100,reviveAt=-100,hushAt,x,z;
        public NativeFallenSeat Copy()=>(NativeFallenSeat)MemberwiseClone();
    }
    [Serializable] public sealed class NativeFallenRecord
    {
        public float started,phaseChangeAt=-100,draftAt=-100,resumeAt=-100,phaseStarted,lastOhk=-100;
        public bool draftGranted;public int seed,patternSeed;public string[] chipOffer=Array.Empty<string>();
        public NativeFallenSeat[] hush=Array.Empty<NativeFallenSeat>();
        public V3[] pools=Array.Empty<V3>();public float[] verdicts=Array.Empty<float>();
        public NativeFallenRecord Copy(){var f=(NativeFallenRecord)MemberwiseClone();f.chipOffer=(string[])chipOffer.Clone();f.hush=hush.Select(s=>s.Copy()).ToArray();f.pools=(V3[])pools.Clone();f.verdicts=(float[])verdicts.Clone();return f;}
        public bool Valid()=>new[]{started,phaseChangeAt,draftAt,resumeAt,phaseStarted,lastOhk}.All(NativeZoneCombat.Finite)&&started>=0&&
            chipOffer!=null&&chipOffer.Length<=3&&chipOffer.Distinct().Count()==chipOffer.Length&&chipOffer.All(c=>NativeFallenBoss.Chips.Any(k=>k.Id==c))&&
            pools!=null&&pools.Length<=8&&pools.All(p=>NativeZoneCombat.Finite(p.x)&&NativeZoneCombat.Finite(p.z)&&Math.Abs(p.x)<=80&&Math.Abs(p.z)<=80)&&
            verdicts!=null&&verdicts.Length<=3&&verdicts.All(NativeZoneCombat.Finite)&&hush!=null&&hush.Length<=8&&hush.All(s=>s!=null&&s.id>=0&&s.lives>=0&&s.lives<=5&&s.reviver>=-1&&new[]{s.respawnAt,s.reviveAt,s.hushAt,s.x,s.z}.All(NativeZoneCombat.Finite))&&hush.Select(s=>s.id).Distinct().Count()==hush.Length;
    }
    public static class NativeFallenRules
    {
        public const string Map="expedition-lux-cecidit",Area="exp-lux-cecidit";
        public const float DraftSeconds=20,RevealSeconds=4,DeathSeconds=38.2f;
        public static bool Chip(NativeFallenRecord f,string id)=>f!=null&&Array.IndexOf(f.chipOffer,id)>=0;
        public static float Wind(FallenBossMove m,NativeFallenRecord f)=>Math.Max(m.Kills?2.4f:0,m.Wind*(Chip(f,"haste")?.88f:1));
        public static float NextBar(float time,float epoch)=>epoch+(float)Math.Ceiling((time-epoch)/NativeFallenSong.Bar+1e-4f)*NativeFallenSong.Bar;
        public static string[] Chips(int seed,int phase)=>NativeFallenBoss.Chips.OrderBy(c=>NativePveRandom.Hash((uint)seed,phase*131+Array.FindIndex(NativeFallenBoss.Chips,x=>x.Id==c.Id))).Take(3).Select(c=>c.Id).ToArray();
        public static Vector2 Dir(float angle){float a=angle*(float)Math.PI/180;return new Vector2((float)Math.Sin(a),(float)Math.Cos(a));}
        public static float Line(Vector2 p,Vector2 through,float angle)=>Math.Abs(Vector2.Dot(p-through,new Vector2(Dir(angle).Y,-Dir(angle).X)));
        public static float Hash(float x){double y=Math.Sin(x*127.1+311.7)*43758.5453123;return (float)(y-Math.Floor(y));}
        // Full-disc grid with a clear corridor; columns never share an edge, so a walking escape always exists.
        public static bool Column(int x,int z,int seed)=>x*x+z*z<=110&&x!=(seed%9)-4&&Hash(x*13.1f+z*7.3f+seed)>=.58f;
        public static bool InColumn(Vector2 p,int seed){int x=(int)Math.Floor(p.X/8),z=(int)Math.Floor(p.Y/8);return Column(x,z,seed)&&Math.Abs(p.X-(x*8+4))<=3.68f&&Math.Abs(p.Y-(z*8+4))<=3.68f;}
        public static Vector2 Pool(Vector2 p,int seed){var q=p+Dir(seed%360)*5;return q.Length()>72?Vector2.Normalize(q)*72:q;}
        public static bool SafePool(Vector2 p,V3[] pools)=>pools.Any(v=>Vector2.Distance(p,new Vector2(v.x,v.z))<=6);
        public static float HushSeconds(NativeFallenRecord f,int phase,float radius)=>Math.Max(2,(radius>=74?3:phase==4?4:6)-(Chip(f,"no-shade")?1.5f:0));
        public static ContentMap Arena()
        {
            var sp=Enumerable.Range(0,8).Select(i=>new MapSpawn{pos=new V3((i-3.5f)*2,1,-32)}).ToArray();
            return new ContentMap{id=Map,name="Still Water",tag="NOCTIEL",theme="ward",music=NativeFallenSong.Id,sky=0,floor=0,accent=0xffffff,unity=true,
                bounds=new MapBounds{minX=-84,maxX=84,minZ=-84,maxZ=84,ceiling=90,killY=-8},
                blocks=new[]{new ContentBlock{id="still-water",role="static",material="foundation",pos=new V3(0,-1,0),size=new V3(168,2,168),rotation=new ContentRotation()}},
                spawns=new MapSpawns{ffa=sp,teams=new[]{new MapTeamSpawns{side=0,points=sp},new MapTeamSpawns{side=1,points=new[]{new MapSpawn{pos=new V3(0,1,0)}}}}},
                nav=new MapNav{nodes=Array.Empty<MapNavNode>(),links=Array.Empty<MapNavLink>()},segments=Array.Empty<StructureDefinition>(),hazards=Array.Empty<MapHazard>(),ramps=Array.Empty<MapRamp>(),pilotBlocks=Array.Empty<MapPilotBlock>()};
        }
        public static ExpeditionAreaLayout Layout()=>new ExpeditionAreaLayout{id=Area,role="instance",map=Map,anchors=Array.Empty<ExpeditionWorldAnchor>(),interactions=Array.Empty<ExpeditionInteractionLayout>(),gates=Array.Empty<ExpeditionGateLayout>(),spawns=Array.Empty<ExpeditionSpawnLayout>(),sanctuary=Array.Empty<ExpeditionVolume>(),slots=Array.Empty<ExpeditionPoint>(),bossPoints=new[]{new ExpeditionPoint()},doors=new[]{new ExpeditionDoorLayout{entries=Enumerable.Range(0,8).Select(i=>new ExpeditionPoint{x=(i-3.5f)*2,z=-32}).ToArray(),returns=Array.Empty<ExpeditionPoint>()}}};
    }
}
