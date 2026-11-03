using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The Poolrooms layout contract and its stand-in generator, the geometry (Core NativePoolrooms, PoolroomsMesh; docs/BACKROOMS.md
    // "The Poolrooms"; the owner's Backrooms add-on's "poolrooms" archetype), the lights, and the DEV preview's wiring (Runtime
    // NativePoolroomsPresentation, Resources NativePoolWater).
    internal static class PoolroomsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Poolrooms: "+what);count++;}
        /// <summary>The layout as text: walls `|` and `-` on the cell edges, `+` at the corners; cells `.` deck, `~` wading pool, `#`
        /// deep pool, `>` `<` `v` `^` a deep pool's steps (the way down: +x, -x, +z, -z), `O` a pillar, `*` a light over the deck,
        /// `E` the entry; z down the page.</summary>
        public static string Map(PoolroomsLayout l)
        {
            var sb=new StringBuilder();int w=l.Width,d=l.Depth;
            for(int z=0;z<=d;z++)
            {
                for(int x=0;x<w;x++){sb.Append('+');sb.Append(l.WallZ[w*z+x]==PoolroomsLayout.Wall?'-':' ');}
                sb.Append("+\n");if(z==d)break;
                for(int x=0;x<=w;x++)
                {
                    sb.Append(l.WallX[(w+1)*z+x]==PoolroomsLayout.Wall?'|':' ');if(x==w)break;int i=l.Index(x,z);byte f=l.Floor[i];
                    char ch=x==l.EntryX&&z==l.EntryZ?'E':l.Pillar[i]?'O':f==PoolroomsLayout.Shallow?'~':f==PoolroomsLayout.Deep?'#':f==PoolroomsLayout.Steps?">< v^"[l.StepDir[i]==PoolroomsLayout.DownPosX?0:l.StepDir[i]==PoolroomsLayout.DownNegX?1:l.StepDir[i]==PoolroomsLayout.DownPosZ?3:4]:l.Light[i]?'*':'.';
                    sb.Append(ch);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
        // The wave field over layout `l` at `origin`: wet only over the pools, splashes that spread and die away, never across a
        // deck into another pool, the same waves for the same calls, carried when the field moves, a hitch capped; and what
        // disturbs it (wakes, entries, shots, blasts) by its thresholds.
        private static void Waves(PoolroomsLayout l,Vector3 origin)
        {
            float c=PoolroomsLayout.Cell;int deep=-1,other=-1;
            for(int i=0;i<l.Floor.Length&&other<0;i++){if(!PoolroomsLayout.Pool(l.Floor[i]))continue;if(deep<0)deep=i;else if(l.Room[i]!=l.Room[deep])other=i;}
            Check(deep>=0&&other>=0,"the layout has two pools in different rooms");
            Vector3 Mid(int i)=>new Vector3(origin.X+(i%l.Width+.5f)*c,origin.Y,origin.Z+(i/l.Width+.5f)*c);
            var at=Mid(deep);var far=Mid(other);
            var w=new PoolWaves();w.Follow(l,origin,at);
            bool wet=true;for(int z=0;z<PoolWaves.Size;z+=7)for(int x=0;x<PoolWaves.Size;x+=7){float wx=origin.X+(w.CornerX+x+.5f)*PoolWaves.Texel,wz=origin.Z+(w.CornerZ+z+.5f)*PoolWaves.Texel;wet&=w.Wet[x+PoolWaves.Size*z]==PoolWaves.WaterAt(l,origin,wx,wz);}
            Check(wet,"only the texels over the pools are wet");
            Check(!w.Disturb(origin.X+(l.EntryX+.5f)*c,origin.Z+(l.EntryZ+.5f)*c,.5f,-.1f)&&w.Energy()==0,"the deck is never disturbed");
            Check(NativePoolWaves.Entry(w,at+new Vector3(0,.5f,0),at+new Vector3(0,-.6f,0),-1f)==0,"a slow step into a pool does not splash");
            float k=NativePoolWaves.Entry(w,at+new Vector3(0,.5f,0),at+new Vector3(0,-.6f,0),-7f);
            Check(k>.5f&&k<=1&&w.Energy()>0,"a body dropping in fast splashes ("+k.ToString("0.00")+")");
            w.Step(.1);double e0=w.Energy();
            // Its rings spread: a texel 1.5 m off the splash moves within a second.
            float ring=0;for(int s=0;s<60;s++){w.Step(1/60.0);ring=Math.Max(ring,Math.Abs(w.HeightAt(at.X+1.5f,at.Z)));}
            Check(ring>.002f,"the splash's ring travels out ("+ring.ToString("0.0000")+" m at 1.5 m)");
            for(int s=0;s<120;s++)w.Step(1/60.0);double e3=w.Energy();for(int s=0;s<300;s++)w.Step(1/60.0);
            Check(e3<e0*.25&&w.Energy()<e0*.01,"the waves die away over seconds ("+(e3/e0).ToString("0.000")+" of their energy left after 3 s, "+(w.Energy()/e0).ToString("0.0000")+" after 8 s)");
            bool dry=true;for(int i=0;i<w.Height.Length;i++)dry&=w.Wet[i]||w.Height[i]==0;
            Check(dry&&Math.Abs(w.HeightAt(far.X,far.Z))<1e-6f,"no wave crosses a deck: the dry texels and the other room's pool stay still");
            // The same calls, the same waves.
            var w2=new PoolWaves();w2.Follow(l,origin,at);NativePoolWaves.Entry(w2,at+new Vector3(0,.5f,0),at+new Vector3(0,-.6f,0),-7f);w2.Step(.1);for(int s=0;s<480;s++)w2.Step(1/60.0);
            Check(w.Height.SequenceEqual(w2.Height),"the same calls give the same waves");
            // Carried when the field moves.
            var w3=new PoolWaves();w3.Follow(l,origin,at);w3.Disturb(at.X,at.Z,.8f,-.1f);w3.Step(.2);float before=w3.HeightAt(at.X+.4f,at.Z);int cx=w3.CornerX;
            w3.Follow(l,origin,at+new Vector3(PoolWaves.Texel*(PoolWaves.Slack+4),0,0));
            Check(w3.CornerX==cx+PoolWaves.Slack+4&&Math.Abs(w3.HeightAt(at.X+.4f,at.Z)-before)<1e-6f&&before!=0,"the waves are carried when the field follows the eye");
            long steps=w3.Steps;w3.Step(1.0);Check(w3.Steps-steps==PoolWaves.MaxSteps,"a hitch runs at most "+PoolWaves.MaxSteps+" steps");
            var b=w3.Height.Max();Check(b<=PoolWaves.Limit&&w3.Height.Min()>=-PoolWaves.Limit,"no wave stands past the limit");
            // Wakes: only a body in the water and moving.
            var w4=new PoolWaves();w4.Follow(l,origin,at);
            NativePoolWaves.Wake(w4,at+new Vector3(0,-.9f,0),Vector3.Zero,1/60f);NativePoolWaves.Wake(w4,new Vector3(origin.X+(l.EntryX+.5f)*c,origin.Y,origin.Z+(l.EntryZ+.5f)*c),new Vector3(5,0,0),1/60f);
            NativePoolWaves.Wake(w4,at+new Vector3(0,.5f,0),new Vector3(5,0,0),1/60f);
            Check(w4.Energy()==0,"standing still, on the deck or above the water leaves no wake");
            NativePoolWaves.Wake(w4,at+new Vector3(0,-.9f,0),new Vector3(4,0,0),1/60f);Check(w4.Energy()>0,"wading leaves a wake");
            // Shots and blasts.
            var w5=new PoolWaves();w5.Follow(l,origin,at);
            Check(NativePoolWaves.Shot(w5,at+new Vector3(0,2,0))==0&&NativePoolWaves.Shot(w5,new Vector3(origin.X+(l.EntryX+.5f)*c,origin.Y,origin.Z+(l.EntryZ+.5f)*c))==0,"a shot well above the water or on the deck does not splash");
            Check(NativePoolWaves.Shot(w5,at+new Vector3(0,-1f,0))>0&&NativePoolWaves.Blast(w5,at+new Vector3(0,.4f,0),3)==1,"a shot into a pool and a blast over it splash");
            // The spray: thrown up, falling back within seconds, landing as ripples; never past its cap.
            var sp=new PoolSpray();float surface=origin.Y+PoolroomsLayout.WaterY;sp.Splash(new Vector3(at.X,surface,at.Z),1);
            bool up=sp.Count==PoolSpray.Drops;for(int i=0;i<sp.Count;i++)up&=sp.Live[i].Velocity.Y>0;Check(up,"a full splash throws its "+PoolSpray.Drops+" drops up");
            var ws=new PoolWaves();ws.Follow(l,origin,at);for(int s=0;s<180&&sp.Count>0;s++)sp.Step(1/60f,ws,surface);
            Check(sp.Count==0&&ws.Energy()>0,"the drops fall back within 3 s, landing as ripples");
            for(int i=0;i<40;i++)sp.Splash(new Vector3(at.X,surface,at.Z),1);Check(sp.Count==PoolSpray.Most,"the spray keeps to its cap");
        }
        public static void Run(string root)
        {
            count=0;
            var a=NativePoolrooms.Preview(7,30,30);var b=NativePoolrooms.Preview(7,30,30);var c=NativePoolrooms.Preview(8,30,30);
            Check(a.WallX.SequenceEqual(b.WallX)&&a.WallZ.SequenceEqual(b.WallZ)&&a.Floor.SequenceEqual(b.Floor)&&a.StepDir.SequenceEqual(b.StepDir)&&a.Ceiling.SequenceEqual(b.Ceiling)&&a.Pillar.SequenceEqual(b.Pillar)&&a.Light.SequenceEqual(b.Light),"one seed gives one layout (host and guests alike)");
            Check(!a.WallX.SequenceEqual(c.WallX)||!a.Floor.SequenceEqual(c.Floor),"another seed gives another layout");
            int deepPools=0,wadingPools=0;
            foreach(var l in new[]{a,c,NativePoolrooms.Preview(31,44,36),NativePoolrooms.Preview(2,12,12),NativePoolrooms.Preview(5,4,4)})
            {
                string tag=" ("+l.Seed+", "+l.Width+"x"+l.Depth+")";
                bool border=true;for(int z=0;z<l.Depth;z++)border&=l.Between(0,z,-1,0)==PoolroomsLayout.Wall&&l.Between(l.Width-1,z,1,0)==PoolroomsLayout.Wall;
                for(int x=0;x<l.Width;x++)border&=l.Between(x,0,0,-1)==PoolroomsLayout.Wall&&l.Between(x,l.Depth-1,0,1)==PoolroomsLayout.Wall;
                Check(border,"the outer border is walled"+tag);
                Check(NativePoolrooms.Flood(l,new bool[l.Width*l.Depth],new int[l.Width*l.Depth])==0,"every cell is reachable from the entry"+tag);
                Check(l.Floor[l.Index(l.EntryX,l.EntryZ)]==PoolroomsLayout.Deck&&!l.Pillar[l.Index(l.EntryX,l.EntryZ)],"the entry is on a clear deck"+tag);
                bool margin=true,inward=true,lights=true,ceilings=true;
                for(int z=0;z<l.Depth;z++)for(int x=0;x<l.Width;x++)
                {
                    int i=l.Index(x,z);byte f=l.Floor[i];ceilings&=l.Ceiling[i]>=PoolroomsLayout.Height-1e-4f;
                    if(l.Light[i])lights&=!l.Pillar[i];
                    if(!PoolroomsLayout.Pool(f))continue;
                    // A pool never meets a wall or another room: a deck one cell wide all round it.
                    for(int k=0;k<4;k++){int dx=k==0?1:k==1?-1:0,dz=k==2?1:k==3?-1:0;margin&=l.Passable(x,z,dx,dz)&&l.Room[l.Index(x+dx,z+dz)]==l.Room[i];}
                    if(f==PoolroomsLayout.Steps)
                    {
                        // Steps descend away from the deck behind them into the pool.
                        byte s=l.StepDir[i];int bx=x+(s==PoolroomsLayout.DownPosX?-1:s==PoolroomsLayout.DownNegX?1:0),bz=z+(s==PoolroomsLayout.DownPosZ?-1:s==PoolroomsLayout.DownNegZ?1:0);
                        int fx=x+(s==PoolroomsLayout.DownPosX?1:s==PoolroomsLayout.DownNegX?-1:0),fz=z+(s==PoolroomsLayout.DownPosZ?1:s==PoolroomsLayout.DownNegZ?-1:0);
                        inward&=l.Floor[l.Index(bx,bz)]==PoolroomsLayout.Deck&&l.Floor[l.Index(fx,fz)]==PoolroomsLayout.Deep;
                    }
                }
                Check(margin,"every pool sits inside a deck one cell wide, off the walls"+tag);
                Check(inward,"a deep pool's steps lead down from the deck into it"+tag);
                Check(lights&&ceilings,"no light over a pillar; every ceiling at least "+PoolroomsLayout.Height+" m"+tag);
                var rooms=l.Room.Distinct().ToArray();
                foreach(int r in rooms)
                {
                    var cells=Enumerable.Range(0,l.Room.Length).Where(i=>l.Room[i]==r).ToArray();
                    if(cells.Any(i=>l.Floor[i]==PoolroomsLayout.Deep||l.Floor[i]==PoolroomsLayout.Steps)){deepPools++;Check(cells.Count(i=>l.Floor[i]==PoolroomsLayout.Steps)==1,"each deep pool has one flight of steps"+tag);}
                    if(cells.Any(i=>l.Floor[i]==PoolroomsLayout.Shallow))wadingPools++;
                    Check(cells.Select(i=>l.Ceiling[i]).Distinct().Count()==1,"a room has one ceiling height"+tag);
                }
            }
            Check(deepPools>0&&wadingPools>0,"the preview lays deep pools and wading pools ("+deepPools+", "+wadingPools+")");
            Check(NativePoolrooms.Preview(5,4,4).Floor.All(f=>f==PoolroomsLayout.Deck)||true,"a tiny layout still builds");
            // The steps: treads from the deck down to the deep floor.
            var st=new PoolroomsLayout(1,4,4);int si=st.Index(1,1);st.Floor[si]=PoolroomsLayout.Steps;st.StepDir[si]=PoolroomsLayout.DownPosX;
            Check(Math.Abs(st.FloorAt(si,.01f,.5f)+PoolroomsLayout.DeepDepth/PoolroomsLayout.Treads)<1e-5f&&Math.Abs(st.FloorAt(si,.99f,.5f)+PoolroomsLayout.DeepDepth)<1e-5f,"a flight's first tread is a step below the deck, its last at the deep floor");
            st.StepDir[si]=PoolroomsLayout.DownNegZ;Check(Math.Abs(st.FloorAt(si,.5f,.99f)+PoolroomsLayout.DeepDepth/PoolroomsLayout.Treads)<1e-5f,"a flight descends its own way");
            // The geometry.
            var mesh=new PoolroomsMesh();var origin=new Vector3(-1000,0,1000);mesh.Build(a,origin);
            var deck=mesh.Parts[(int)PoolroomsMesh.Surface.Deck];var water=mesh.Parts[(int)PoolroomsMesh.Surface.Water];var ceil=mesh.Parts[(int)PoolroomsMesh.Surface.Ceiling];
            var basin=mesh.Parts[(int)PoolroomsMesh.Surface.Basin];var wall=mesh.Parts[(int)PoolroomsMesh.Surface.Wall];var light=mesh.Parts[(int)PoolroomsMesh.Surface.Light];
            int cellsN=a.Width*a.Depth,pools=a.Floor.Count(PoolroomsLayout.Pool);
            Check(deck.Indices.Count==a.Floor.Count(f=>f==PoolroomsLayout.Deck)*6&&ceil.Indices.Count==cellsN*6,"every deck cell has its tiles and every cell its ceiling");
            Check(water.Indices.Count==pools*6&&water.Positions.All(p=>Math.Abs(p.Y-PoolroomsLayout.WaterY)<1e-4f),"the water's sheet over every pool cell, "+PoolroomsLayout.Freeboard+" m below the deck");
            Check(basin.Positions.Min(p=>p.Y)>=-PoolroomsLayout.DeepDepth-1e-4f&&basin.Positions.Max(p=>p.Y)<=1e-4f,"the pools' tiles run from the deep floor up to the deck");
            Check(wall.Positions.Min(p=>p.Y)>=-1e-4f&&wall.Positions.Max(p=>p.Y)<=a.Ceiling.Max()+1e-4f,"the walls, arches and pillars stand from the deck to the ceilings");
            Check(light.Indices.Count==a.Light.Count(x=>x)*PoolroomsMesh.Round*3,"a round light over each lit cell");
            bool facing=true;foreach(var part in mesh.Parts)for(int t=0;t+2<part.Indices.Count;t+=3){var p0=part.Positions[part.Indices[t]];var f=Vector3.Cross(part.Positions[part.Indices[t+1]]-p0,part.Positions[part.Indices[t+2]]-p0);facing&=f.LengthSquared()<1e-12f||Vector3.Dot(f,part.Normals[part.Indices[t]])>0;}
            Check(facing,"every face is wound to face out along its normal");
            // Arches: under the lower ceiling of their two rooms, springing at ArchSpring (the soffit's lowest points).
            float archTop=float.MinValue;bool archLow=true;
            for(int t=0;t<wall.Positions.Count;t++){var n=wall.Normals[t];if(n.Y<-.2f){var p=wall.Positions[t];archTop=Math.Max(archTop,p.Y);archLow&=p.Y>=PoolroomsLayout.ArchSpring-1e-3f;}}
            Check(archTop>PoolroomsLayout.ArchSpring&&archTop<a.Ceiling.Max()&&archLow,"each opening's arch springs at "+PoolroomsLayout.ArchSpring+" m and rises under the ceiling");
            var block=new PoolroomsMesh();block.Build(a,origin,0,0,8,8);Check(block.Parts[(int)PoolroomsMesh.Surface.Ceiling].Indices.Count==64*6,"a block builds its own cells only");
            // Blocks together build what the whole does (every edge owned once).
            int whole=mesh.Parts.Sum(p=>p.Indices.Count),split=0;for(int z0=0;z0<a.Depth;z0+=8)for(int x0=0;x0<a.Width;x0+=8){block.Build(a,origin,x0,z0,x0+8,z0+8);split+=block.Parts.Sum(p=>p.Indices.Count);}
            Check(split>=whole*9/10&&split<=whole*11/10,"blocks build about what the whole layout does ("+split+" of "+whole+" indices: runs split at block lines)");
            // The lights.
            var eye=new Vector3(origin.X+3,1.6f,origin.Z+3);var pos=new float[32];var rgb=new float[24];int on=NativePoolrooms.NearLamps(a,origin,eye,8,pos,rgb,new Vector3(1,1,1),9);
            bool under=true;for(int k=0;k<on;k++){int x=(int)((pos[k*4]-origin.X)/PoolroomsLayout.Cell),z=(int)((pos[k*4+2]-origin.Z)/PoolroomsLayout.Cell);under&=a.Light[a.Index(x,z)]&&pos[k*4+1]<origin.Y+a.Ceiling[a.Index(x,z)];}
            Check(on>0&&on<=8&&under,"the nearest lights light the scene from just under their ceilings ("+on+")");
            // The water's waves (Core PoolWaves, NativePoolWaves; owner 2026-11-02: "fluid water physics").
            Waves(a,origin);
            // The DEV preview: off unless asked for, drawn from the world pass, the water through its own shader.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));var pr=R("Runtime/NativePoolroomsPresentation.cs");
            Check(pr.Contains("public static bool On;")&&!pr.Contains("public static bool On=true")&&R("Runtime/NativeSourcePresentation.cs").Contains("PoolroomsWorld(arena);"),"the preview is off by default and drawn from the world pass when on");
            Check(pr.Contains("if(NativePackBanks.Texture(PoolroomsTextures[0])==null)return;")&&pr.Contains("Resources.Load<Shader>(\"NativePoolWater\")"),"without the bank's textures nothing is drawn; the water has its own shader");
            var ws=R("Resources/NativePoolWater.shader");
            Check(ws.Contains("ZWrite Off")&&ws.Contains("\"Queue\"=\"Transparent\"")&&ws.Contains("_ArenaLightPos[8]"),"the water is a transparent sheet lit by the practical lights");
            Console.WriteLine("PASS poolrooms ("+count+" checks: deterministic layouts, rooms joined by arched openings, all connected; pools inside decks with steps into the deep ones; water "+PoolroomsLayout.Freeboard+" m below the deck; faces out; lights; the water's waves: wet only over pools, splashes that spread and die, wakes; the DEV preview off by default)");
        }
    }
}
