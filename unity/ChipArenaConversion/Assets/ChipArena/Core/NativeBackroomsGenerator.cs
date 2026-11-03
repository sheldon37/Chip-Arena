using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
namespace ChipArena.UnityConversion
{
    // Rules/collision output. The renderer consumes Layout unchanged; no licensed data is generated here.
    public sealed class BackroomsGenerated
    {
        public BackroomsLayout Layout;
        public ContentMap Map;
        public int[] Players,Enemies;
        public string Identity;
        public V3 Entry=>NativeBackroomsGenerator.Position(Layout,Layout.Index(Layout.EntryX,Layout.EntryZ));
        public V3 Exit=>NativeBackroomsGenerator.Position(Layout,Layout.Index(Layout.ExitX,Layout.ExitZ));
    }
    public static class NativeBackroomsGenerator
    {
        public const string Version="backrooms-level0-4",ArenaId="backrooms-level-0",InstanceMap="expedition-backrooms-level-0",AreaId="exp-backrooms";
        public const int ArenaSeed=647031,ExpeditionSize=40,ArenaSize=16;
        private static readonly (int x,int z)[] Directions={(1,0),(0,1),(-1,0),(0,-1)};
        private struct Random
        {
            private uint state;
            public Random(int seed){state=unchecked((uint)seed)^0xa341316cu;if(state==0)state=1;}
            public int Next(int max){unchecked{state^=state<<13;state^=state>>17;state^=state<<5;}return (int)(((ulong)state*(uint)max)>>32);}
        }
        public static bool IsMap(string id)=>id==ArenaId||id==InstanceMap;
        public static V3 Position(BackroomsLayout l,int cell)=>new V3((cell%l.Width+.5f)*BackroomsLayout.Cell,0,(cell/l.Width+.5f)*BackroomsLayout.Cell);
        public static bool Walkable(BackroomsLayout l,int cell)=>cell>=0&&cell<l.Width*l.Depth&&l.Floor[cell]!=BackroomsLayout.Pit&&l.Pillar[cell]==BackroomsLayout.NoPillar;
        private static bool ReturnDoorEdge(int x,int z,int dx,int dz)=>x==1&&dx==0&&(z==1&&dz==-1||z==0&&dz==1);
        public static bool Link(BackroomsLayout l,int cell,int dx,int dz)
        {
            int x=cell%l.Width,z=cell/l.Width;
            return !ReturnDoorEdge(x,z,dx,dz)&&l.Inside(x+dx,z+dz)&&Walkable(l,cell)&&Walkable(l,l.Index(x+dx,z+dz))&&l.Between(x,z,dx,dz)!=BackroomsLayout.Wall;
        }
        public static int[] Distances(BackroomsLayout l,int start)
        {
            var distances=Enumerable.Repeat(-1,l.Width*l.Depth).ToArray();if(!Walkable(l,start))return distances;
            var q=new int[distances.Length];int head=0,tail=0;q[tail++]=start;distances[start]=0;
            while(head<tail){int cell=q[head++];foreach(var d in Directions)if(Link(l,cell,d.x,d.z)){int n=cell+d.x+d.z*l.Width;if(distances[n]<0){distances[n]=distances[cell]+1;q[tail++]=n;}}}
            return distances;
        }
        public static BackroomsGenerated Build(int seed,bool expedition)
        {
            int size=expedition?ExpeditionSize:ArenaSize;var l=NativeBackrooms.Preview(seed,size,size);var rng=new Random(seed);
            // Use Claude's enclosed-room topology, then certify production spawn and collision routes.
            Array.Clear(l.Pillar,0,l.Pillar.Length);
            for(int z=1;z<4;z++)for(int x=1;x<5;x++){if(x<4)l.Set(x,z,1,0,BackroomsLayout.Open);if(z<3)l.Set(x,z,0,1,BackroomsLayout.Open);}
            l.EntryX=1;l.EntryZ=1;l.Set(1,1,0,-1,BackroomsLayout.Short);
            int entry=l.Index(1,1);
            // The return portal shuts during the encounter. Never depend on its edge for navigation.
            while(true)
            {
                var reach=Distances(l,entry);if(reach.All(d=>d>=0))break;bool opened=false;
                for(int c=0;c<reach.Length&&!opened;c++)if(reach[c]>=0)foreach(var d in Directions)
                {int x=c%size,z=c/size;if(!l.Inside(x+d.x,z+d.z)||ReturnDoorEdge(x,z,d.x,d.z)||reach[l.Index(x+d.x,z+d.z)]>=0)continue;l.Set(x,z,d.x,d.z,BackroomsLayout.Short);opened=true;break;}
                if(!opened)throw new InvalidOperationException("Cannot connect Level 0 around its return portal.");
            }
            var distances=Distances(l,entry);int exit=Enumerable.Range(0,distances.Length).OrderByDescending(c=>distances[c]).ThenBy(c=>c).First();l.ExitX=exit%size;l.ExitZ=exit/size;
            var reserved=new HashSet<int>(Enumerable.Range(0,8).Select(i=>l.Index(1+i%4,1+i/4))){exit};
            // Optional blocked cells may never sever the playable graph. Pits are expedition-only and never mandatory routes.
            for(int trial=0;trial<size*size/10;trial++)
            {
                int c=rng.Next(size*size);if(reserved.Contains(c)||!Walkable(l,c)||(Position(l,c)-Position(l,entry)).Length<12||(Position(l,c)-Position(l,exit)).Length<6)continue;
                if(Directions.Count(d=>Link(l,c,d.x,d.z))<3)continue;
                bool pit=expedition&&rng.Next(12)==0;
                if(pit)l.Floor[c]=BackroomsLayout.Pit;else l.Pillar[c]=BackroomsLayout.SmallPillar;
                var reach=Distances(l,entry);if(Enumerable.Range(0,reach.Length).Any(i=>Walkable(l,i)&&reach[i]<0)){l.Floor[c]=BackroomsLayout.Carpet;l.Pillar[c]=BackroomsLayout.NoPillar;}
            }
            l.Circuits[l.Circuit[entry]]=BackroomsLayout.On;l.Light[entry]=true;
            var players=expedition?Enumerable.Range(0,8).Select(i=>l.Index(1+i%4,1+i/4)).ToArray():Spread(l,8,entry);
            var enemies=Spread(l,24,exit).Where(c=>players.All(p=>(Position(l,p)-Position(l,c)).Length>=12)&&c!=exit).ToArray();
            var result=new BackroomsGenerated{Layout=l,Players=players,Enemies=enemies};result.Map=Map(result,expedition);result.Identity=Identity(result);return result;
        }
        private static int[] Spread(BackroomsLayout l,int count,int first)
        {
            var chosen=new List<int>{first};var best=Distances(l,first);
            while(chosen.Count<count)
            {
                int next=Enumerable.Range(0,best.Length).Where(c=>best[c]>=0&&!chosen.Contains(c)).OrderByDescending(c=>best[c]).ThenBy(c=>c).First();chosen.Add(next);
                var d=Distances(l,next);for(int i=0;i<best.Length;i++)best[i]=Math.Min(best[i],d[i]);
            }
            return chosen.ToArray();
        }
        private static ContentMap Map(BackroomsGenerated g,bool expedition)
        {
            var l=g.Layout;float cell=BackroomsLayout.Cell,height=BackroomsLayout.Height,thickness=BackroomsLayout.Thickness;string id=expedition?InstanceMap:ArenaId;var blocks=new List<ContentBlock>();
            void Box(string key,float x,float y,float z,float sx,float sy,float sz,string material="stone")=>blocks.Add(new ContentBlock{id=id+":"+key,role="static",material=material,pos=new V3(x,y,z),size=new V3(sx,sy,sz),rotation=new ContentRotation()});
            // A real ceiling prevents jumping onto maze walls. Pit shafts have a bottom below the map's kill plane.
            Box("ceiling",l.Width*cell/2,height+.15f,l.Depth*cell/2,l.Width*cell,.3f,l.Depth*cell);
            for(int z=0;z<l.Depth;z++)for(int x=0;x<l.Width;x++)
            {
                int c=l.Index(x,z);var p=Position(l,c);
                if(l.Floor[c]!=BackroomsLayout.Pit)Box("floor-"+c,p.x,-.15f,p.z,cell,.3f,cell,"foundation");
                else{Box("pit-bottom-"+c,p.x,-5.15f,p.z,cell,.3f,cell);foreach(var d in Directions)Box("pit-edge-"+c+"-"+d.x+"-"+d.z,p.x+d.x*cell/2,-2.5f,p.z+d.z*cell/2,d.x==0?cell:thickness,5,d.z==0?cell:thickness);}
                if(l.Pillar[c]!=BackroomsLayout.NoPillar){float width=BackroomsLayout.PillarSize;Box("pillar-"+c,p.x,height/2,p.z,width,height,width);}
            }
            void Wall(string key,float x,float z,bool alongX,byte kind)
            {
                if(kind==BackroomsLayout.Open)return;
                float length=kind==BackroomsLayout.Short?(cell-BackroomsLayout.Doorway+thickness)/2:cell+thickness;
                void Part(string suffix,float offset)=>Box(key+suffix,x+(alongX?offset:0),height/2,z+(alongX?0:offset),alongX?length:thickness,height,alongX?thickness:length);
                if(kind==BackroomsLayout.Short){float offset=(cell+BackroomsLayout.Doorway+thickness)/4;Part("-a",-offset);Part("-b",offset);}else Part("",0);
            }
            for(int z=0;z<=l.Depth;z++)for(int x=0;x<l.Width;x++)Wall("wall-z-"+x+"-"+z,(x+.5f)*cell,z*cell,true,l.WallZ[l.Width*z+x]);
            for(int z=0;z<l.Depth;z++)for(int x=0;x<=l.Width;x++)Wall("wall-x-"+x+"-"+z,x*cell,(z+.5f)*cell,false,l.WallX[(l.Width+1)*z+x]);
            var nodes=new List<MapNavNode>();var links=new List<MapNavLink>();
            for(int c=0;c<l.Width*l.Depth;c++)if(Walkable(l,c))
            {
                nodes.Add(new MapNavNode{id="br:"+c,pos=Position(l,c)+new V3(0,.85f,0)});
                foreach(var d in Directions.Take(2))if(Link(l,c,d.x,d.z)){int n=c+d.x+d.z*l.Width;links.Add(new MapNavLink{id="br:"+c+":"+n,a="br:"+c,b="br:"+n});}
            }
            MapSpawn Spawn(int c)=>new MapSpawn{pos=Position(l,c)+new V3(0,.85f,0),yaw=0};
            return new ContentMap{id=id,name=expedition?"Backrooms · Level 0":"Level 0",tag="Generated maze",theme="backrooms",look="training-studio",unity=true,rotation=!expedition,sky=0x827b46,floor=0xa49b68,accent=0xe6dc8f,
                blocks=blocks.ToArray(),bounds=new MapBounds{minX=0,maxX=l.Width*cell,minZ=0,maxZ=l.Depth*cell,ceiling=height,killY=-4},spawns=new MapSpawns{ffa=g.Players.Select(Spawn).ToArray(),teams=new[]{new MapTeamSpawns{side=0,points=g.Players.Take(4).Select(Spawn).ToArray()},new MapTeamSpawns{side=1,points=g.Players.Skip(4).Select(Spawn).ToArray()}}},
                nav=new MapNav{nodes=nodes.ToArray(),links=links.ToArray()},segments=Array.Empty<StructureDefinition>(),pilotBlocks=Array.Empty<MapPilotBlock>(),hazards=Array.Empty<MapHazard>(),ramps=Array.Empty<MapRamp>()};
        }
        private static string Identity(BackroomsGenerated g)
        {
            var l=g.Layout;var s=new StringBuilder(Version).Append('|').Append(l.Seed.ToString(CultureInfo.InvariantCulture)).Append('|').Append(l.Width);
            foreach(var a in new[]{l.Floor,l.Pillar,l.Circuit,l.WallX,l.WallZ,l.Circuits}){s.Append('|');foreach(byte b in a)s.Append((char)('a'+b));}
            s.Append('|');foreach(bool lit in l.Light)s.Append(lit?'1':'0');
            foreach(var a in new[]{g.Players,g.Enemies}){s.Append('|');foreach(int c in a)s.Append(c.ToString(CultureInfo.InvariantCulture)).Append(',');}
            s.Append('|').Append(l.ExitX).Append('|').Append(l.ExitZ);return NativeMapRules.Revision(s.ToString());
        }
    }
}
