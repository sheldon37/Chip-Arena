using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
namespace ChipArena.UnityConversion
{
    public sealed class PoolroomsGenerated
    {
        public PoolroomsLayout Layout;public ContentMap Map;public int[] Players,Enemies;public int ExitCell;public string Identity;
        /// <summary>The instance's machines (none in the PvP arena): NativeInstanceMachines. Each takes its cell off the navigation.</summary>
        public InstanceMachineSpot[] Machines=Array.Empty<InstanceMachineSpot>();
        public V3 Entry=>NativePoolroomsGenerator.Position(Layout,Players[0]);
        public V3 Exit=>NativePoolroomsGenerator.Position(Layout,ExitCell);
    }
    public static class NativePoolroomsGenerator
    {
        public const string Version="poolrooms-rules-2-entities1",ArenaId="backrooms-poolrooms",InstanceMap="expedition-backrooms-poolrooms",AreaId="exp-poolrooms";
        public const int ArenaSeed=647032,ArenaSize=24,ExpeditionSize=32;
        private static bool ReturnEdge(int x,int z,int dx,int dz)=>x==1&&dx==0&&(z==1&&dz==-1||z==0&&dz==1);
        private static readonly (int x,int z)[] Directions={(1,0),(0,1),(-1,0),(0,-1)};
        public static string SpawnKind(string map,string kind)=>IsMap(map)&&kind=="hulk"?"brute":kind;
        public static bool IsMap(string id)=>id==ArenaId||id==InstanceMap;
        public static V3 Position(PoolroomsLayout l,int cell)=>new V3((cell%l.Width+.5f)*2,(l.Floor[cell]==PoolroomsLayout.Steps?-PoolroomsLayout.DeepDepth/2:l.FloorAt(cell,.5f,.5f)),(cell/l.Width+.5f)*2);
        public static PoolroomsGenerated Build(int seed,bool expedition)
        {
            int size=expedition?ExpeditionSize:ArenaSize;var l=NativePoolrooms.Preview(seed,size,size);
            // Cell-centre navigation has no safe path through a pillar's centre. Production halls remain clear.
            Array.Clear(l.Pillar,0,l.Pillar.Length);
            int entry=l.Index(l.EntryX,l.EntryZ);var reach=Distances(l,entry);
            var decks=Enumerable.Range(0,reach.Length).Where(c=>l.Floor[c]==PoolroomsLayout.Deck&&reach[c]>=0).ToArray();
            if(decks.Length<8)throw new InvalidOperationException("Poolrooms requires eight reachable dry spawn cells.");
            var players=new List<int>{entry};var best=(int[])reach.Clone();
            while(players.Count<8){int c=expedition?decks.Where(n=>!players.Contains(n)).OrderBy(n=>reach[n]).ThenBy(n=>n).First():decks.Where(n=>!players.Contains(n)).OrderByDescending(n=>best[n]).ThenBy(n=>n).First();players.Add(c);var d=Distances(l,c);for(int i=0;i<best.Length;i++)best[i]=Math.Min(best[i],d[i]);}
            int exit=decks.OrderByDescending(c=>reach[c]).ThenBy(c=>c).First();
            var enemies=decks.Where(c=>c!=exit&&players.All(p=>(Position(l,p)-Position(l,c)).Length>=12)).OrderByDescending(c=>reach[c]).Where((c,i)=>i%4==0).Take(32).ToArray();
            var g=new PoolroomsGenerated{Layout=l,Players=players.ToArray(),Enemies=enemies,ExitCell=exit};if(expedition)g.Machines=NativeInstanceMachines.Poolrooms(l,g.Players,enemies,entry,exit,(c,dx,dz)=>{int cx=c%l.Width,cz=c/l.Width;return !ReturnEdge(cx,cz,dx,dz)&&l.Passable(cx,cz,dx,dz);});g.Map=Map(g,expedition);
            var s=new StringBuilder(Version).Append('|').Append(seed.ToString(CultureInfo.InvariantCulture)).Append('|').Append(size.ToString(CultureInfo.InvariantCulture));
            foreach(var a in new[]{l.Floor,l.StepDir,l.WallX,l.WallZ}){s.Append('|');foreach(byte b in a)s.Append((char)('a'+b));}
            foreach(float h in l.Ceiling)s.Append('|').Append(h.ToString("R",CultureInfo.InvariantCulture));
            foreach(int room in l.Room)s.Append('|').Append(room.ToString(CultureInfo.InvariantCulture));
            foreach(bool light in l.Light)s.Append(light?'1':'0');
            foreach(int c in g.Players.Concat(g.Enemies).Concat(new[]{exit}))s.Append('|').Append(c.ToString(CultureInfo.InvariantCulture));
            foreach(var m in g.Machines)s.Append('|').Append(m.Kind).Append(m.Cell.ToString(CultureInfo.InvariantCulture)).Append(':').Append(m.Dx.ToString(CultureInfo.InvariantCulture)).Append(':').Append(m.Dz.ToString(CultureInfo.InvariantCulture));
            g.Identity=NativeMapRules.Revision(s.ToString());return g;
        }
        // Deck-only reachability certifies spawn/exit routes independently of water access.
        public static int[] Distances(PoolroomsLayout l,int start)
        {
            var d=Enumerable.Repeat(-1,l.Floor.Length).ToArray();var q=new Queue<int>();d[start]=0;q.Enqueue(start);
            while(q.Count>0){int c=q.Dequeue(),x=c%l.Width,z=c/l.Width;foreach(var v in Directions)if(!ReturnEdge(x,z,v.x,v.z)&&l.Passable(x,z,v.x,v.z)){int n=l.Index(x+v.x,z+v.z);if(d[n]<0&&l.Floor[n]==PoolroomsLayout.Deck){d[n]=d[c]+1;q.Enqueue(n);}}}return d;
        }
        private static ContentMap Map(PoolroomsGenerated g,bool expedition)
        {
            var l=g.Layout;string id=expedition?InstanceMap:ArenaId;var blocks=new List<ContentBlock>();var ramps=new List<MapRamp>();
            void Box(string key,float x,float y,float z,float sx,float sy,float sz)=>blocks.Add(new ContentBlock{id=id+":"+key,role="static",material="stone",pos=new V3(x,y,z),size=new V3(sx,sy,sz),rotation=new ContentRotation()});
            for(int z=0;z<l.Depth;z++)for(int x=0;x<l.Width;x++)
            {
                int c=l.Index(x,z);float h=l.Ceiling[c];Box("ceiling-"+c,x*2+1,h+.15f,z*2+1,2,.3f,2);
                if(l.Floor[c]!=PoolroomsLayout.Steps){float y=PoolroomsLayout.FloorY(l.Floor[c]);Box("floor-"+c,x*2+1,(y-1.6f)/2,z*2+1,2,y+1.6f,2);}
                else for(int k=0;k<4;k++){byte dir=l.StepDir[c];bool alongX=dir<2;float t=(dir==PoolroomsLayout.DownNegX||dir==PoolroomsLayout.DownNegZ)?3-k:k;float y=-(k+1)*PoolroomsLayout.DeepDepth/4;Box("step-"+c+"-"+k,x*2+(alongX?t*.5f+.25f:1),(y-1.6f)/2,z*2+(alongX?1:t*.5f+.25f),alongX?.5f:2,y+1.6f,alongX?2:.5f);}
            }
            // Keep the four rendered treads, but use the same closed collision wedges as the other
            // authored stairs. A .45 m capsule overlaps the next .325 m riser on a .5 m tread.
            for(int c=0;c<l.Floor.Length;c++)if(l.Floor[c]==PoolroomsLayout.Steps)
            {
                byte d=l.StepDir[c];var down=new V3(d==0?1:d==1?-1:0,0,d==2?1:d==3?-1:0);
                var centre=new V3(c%l.Width*2+1,0,c/l.Width*2+1);
                ramps.Add(new MapRamp{id=id+":pool-stair-"+c,kind="stair",high=centre-down,low=centre+down-new V3(0,PoolroomsLayout.DeepDepth,0),width=2,bottom=-1.6f,
                    blocks=Enumerable.Range(0,4).Select(k=>id+":step-"+c+"-"+k).ToArray()});
            }
            // Match the mesh's 8-cell block boundaries and opening runs. Thin slabs conservatively approximate its 14-sided arch.
            void Line(int line,int from,int to,bool xLine)
            {
                int Cell(int along,bool high)=>xLine?l.Index(Math.Max(0,Math.Min(l.Width-1,line-(high?0:1))),along):l.Index(along,Math.Max(0,Math.Min(l.Depth-1,line-(high?0:1))));
                byte Kind(int a)=>xLine?l.WallX[(l.Width+1)*a+line]:l.WallZ[l.Width*line+a];
                int s=from;
                while(s<to){int a=Cell(s,false),b=Cell(s,true);float top=Math.Max(l.Ceiling[a],l.Ceiling[b]),low=Math.Min(l.Ceiling[a],l.Ceiling[b]);int e=s+1;
                    while(e<to&&Kind(e)==Kind(s)&&l.Room[Cell(e,false)]==l.Room[a]&&l.Room[Cell(e,true)]==l.Room[b]&&Math.Abs(Math.Max(l.Ceiling[Cell(e,false)],l.Ceiling[Cell(e,true)])-top)<.0001f)e++;
                    string key=(xLine?"x":"z")+line+"-"+s;
                    void Slab(string suffix,float at,float width,float bottom)=>Box("wall-"+key+suffix,xLine?line*2:at,(bottom+top)/2,xLine?at:line*2,xLine?.3f:width,top-bottom,xLine?width:.3f);
                    if(Kind(s)==PoolroomsLayout.Wall)Slab("",s+e,(e-s)*2+.3f,-1.3f);
                    else if(l.Room[a]!=l.Room[b]){float half=e-s,mid=s+e,rise=Math.Max(.3f,Math.Min(half,low-PoolroomsLayout.ArchSpring-.25f));for(int k=0;k<56;k++){double t0=Math.PI*k/56,t1=Math.PI*(k+1)/56;float u0=mid-half*(float)Math.Cos(t0),u1=mid-half*(float)Math.Cos(t1);float Arc(int sample){int segment=Math.Min(13,sample/4);double lo=Math.PI*segment/14,hi=Math.PI*(segment+1)/14;float u=mid-half*(float)Math.Cos(Math.PI*sample/56),ul=mid-half*(float)Math.Cos(lo),uh=mid-half*(float)Math.Cos(hi);float blend=(u-ul)/(uh-ul);return PoolroomsLayout.ArchSpring+rise*((float)Math.Sin(lo)*(1-blend)+(float)Math.Sin(hi)*blend);}float y=Math.Min(Arc(k),Arc(k+1));Slab("-arch-"+k,(u0+u1)/2,u1-u0,y);}}
                    s=e;
                }
            }
            for(int x=0;x<=l.Width;x++)for(int z=0;z<l.Depth;z+=8)Line(x,z,Math.Min(z+8,l.Depth),true);
            for(int z=0;z<=l.Depth;z++)for(int x=0;x<l.Width;x+=8)Line(z,x,Math.Min(x+8,l.Width),false);
            var nodes=new List<MapNavNode>();var links=new List<MapNavLink>();
            string Key(int c,int k=0)=>"pool:"+c+":"+k;
            // Put the lower waypoint near the landing so its outgoing chord does not sample the
            // concave ramp/floor seam with a flat-floor capsule embedded in the slope.
            V3 Point(int c,int k){var p=Position(l,c);if(l.Floor[c]!=PoolroomsLayout.Steps)return p;byte d=l.StepDir[c];float along=k==3?1.85f:k*.5f+.25f,t=(d==1||d==3)?2-along:along;return new V3(c%l.Width*2+(d<2?t:1),-along*PoolroomsLayout.DeepDepth/2,c/l.Width*2+(d<2?1:t));}
            void Link(int c,int k,int n,int j){var a=Point(c,k);var b=Point(n,j);if(Math.Abs(a.y-b.y)>.401f)return;links.Add(new MapNavLink{id=Key(c,k)+"/"+Key(n,j),a=Key(c,k),b=Key(n,j)});}
            var machineCells=NativeInstanceMachines.Cells(g.Machines);
            for(int c=0;c<l.Floor.Length;c++){if(machineCells.Contains(c))continue;int count=l.Floor[c]==PoolroomsLayout.Steps?4:1;for(int k=0;k<count;k++){nodes.Add(new MapNavNode{id=Key(c,k),pos=Point(c,k)+new V3(0,.85f,0)});if(k>0)Link(c,k-1,c,k);}
                foreach(var v in Directions.Take(2)){int x=c%l.Width,z=c/l.Width;if(ReturnEdge(x,z,v.x,v.z)||!l.Passable(x,z,v.x,v.z))continue;int n=l.Index(x+v.x,z+v.z);if(machineCells.Contains(n))continue;int nc=l.Floor[n]==PoolroomsLayout.Steps?4:1;for(int k=0;k<count;k++)for(int j=0;j<nc;j++){bool End(int cell,int tread,int dx,int dz){if(l.Floor[cell]!=PoolroomsLayout.Steps)return true;byte dir=l.StepDir[cell];int ax=dir==0?1:dir==1?-1:0,az=dir==2?1:dir==3?-1:0;return (dx==ax&&dz==az&&tread==3)||(dx==-ax&&dz==-az&&tread==0);}if(!End(c,k,v.x,v.z)||!End(n,j,-v.x,-v.z))continue;Link(c,k,n,j);}}
            }
            MapSpawn Spawn(int c)=>new MapSpawn{pos=Position(l,c)+new V3(0,.85f,0)};
            return new ContentMap{id=id,name="Poolrooms",tag="Generated pools",theme="poolrooms",look="training-studio",unity=true,rotation=!expedition,sky=0xc8dddd,floor=0xc8dddd,accent=0x73c8cd,
                blocks=blocks.ToArray(),bounds=new MapBounds{minX=0,maxX=l.Width*2,minZ=0,maxZ=l.Depth*2,ceiling=l.Ceiling.Max(),killY=-4},spawns=new MapSpawns{ffa=g.Players.Select(Spawn).ToArray(),teams=new[]{new MapTeamSpawns{side=0,points=g.Players.Take(4).Select(Spawn).ToArray()},new MapTeamSpawns{side=1,points=g.Players.Skip(4).Select(Spawn).ToArray()}}},nav=new MapNav{nodes=nodes.ToArray(),links=links.ToArray()},segments=Array.Empty<StructureDefinition>(),pilotBlocks=Array.Empty<MapPilotBlock>(),hazards=Array.Empty<MapHazard>(),ramps=ramps.ToArray()};
        }
    }
    public static class NativePoolroomsWater
    {
        // Feet-relative immersion; waves remain visual and never change authority or guest prediction.
        public static float Depth(PoolroomsLayout l,V3 feet)
        {
            if(l==null||!NativeMapRules.Finite(feet))return 0;int x=(int)Math.Floor(feet.x/2),z=(int)Math.Floor(feet.z/2);if(!l.Inside(x,z)||!PoolroomsLayout.Pool(l.Floor[l.Index(x,z)])||feet.y < -1.4f)return 0;
            return Math.Max(0,Math.Min(1.3f,PoolroomsLayout.WaterY-feet.y));
        }
        public static float Speed(float depth)=>depth<=0?1:1-.5f*Math.Min(1,depth/1.15f);
        public static void Pose(Actor actor,PoolroomsLayout l,V3 feet){actor.WaterDepth=Depth(l,feet);actor.Wading=actor.WaterDepth>0;actor.Swimming=false;actor.Waterline=actor.Wading?PoolroomsLayout.WaterY:0;}
    }
}
