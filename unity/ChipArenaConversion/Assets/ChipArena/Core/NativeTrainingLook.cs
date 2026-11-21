using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Training Room's presentation layout (docs/arenas/NEW-STAGES.md "Training Room presentation"; map id `training`, look
    /// `training-studio`): presentation constants derived deterministically from the map's authored `trainingPads`, its blocks,
    /// hazards and bounds, plus the few draft constants the map does not carry (the sprint straight, the ring's apothem). Never
    /// sim data, never a collider, never in the network frame (Astra's handoff: "keep marks as presentation constants").
    /// <list type="bullet">
    /// <item><see cref="NativeTrainingPlan"/>: the firing line (the range pads' x minus their authored distance), the lanes
    /// (range pads clustered by z), the distances, the ring centre (the melee pads' mean), each pad's lane and labels.</item>
    /// <item><see cref="NativeTrainingMarks"/>: two baked meshes (floor and wall paint without outlines; fixtures with them)
    /// for ChipArena/SourceLit, the floor grid quads for the studio shader, and the readout boards' cells.</item>
    /// </list>
    /// Everything lies on the floor (paint at most <see cref="FloorPaintTop"/> above it), on a wall's inner face or a block's
    /// face (at most <see cref="FacePaint"/> proud), or in the roof band (<see cref="RoofBand"/> and up, where no pilot reaches):
    /// Tests/TrainingLookChecks.cs proves it part by part. Deterministic; builds allocate, nothing here runs per frame.
    /// </summary>
    public static class NativeTrainingLook
    {
        public const string MapId="training",Look="training-studio";
        public static bool Active(string mapId)=>mapId==MapId;
        // NativeCyberTheme's palette (Runtime; repeated here for the engine-free builders), and the studio's paints (sRGB).
        public const int Cyan=0x22E6FF,CyanSoft=0x7FF3FF,CyanDeep=0x0A8FB3,Magenta=0xFF3DCF,Violet=0x8B5CFF,Void=0x05070F,Navy=0x0A1024,Indigo=0x131A3D,Panel=0x0D1430,
            Edge=0x2A3A6B,Ink=0xE8FBFF,Muted=0x8AA6C4,Dim=0x4F6687,Danger=0xFF4D6D,Warn=0xFFC24D,Good=0x4DFFA6;
        public const int RulerLine=0x667080,Paint=0xEEF2F5,PlateFill=0x1A2236,PadFill=0x232C3B,Melee=0xFF8A3D,Wainscot=0x2B3442,Seam=0x8E959E,Housing=0x1B2230,Rod=0x3A414B,Lamp=0xF4F8FF,
            Wood=0xC8873A,Masonry=0xC0503A,Structural=0xFFC24D;
        /// <summary>Floor paint layers (m above the floor top): the grid quad, fills, lines, text and marks over fills.</summary>
        public const float GridY=.004f,RailY=.006f,FillY=.008f,PadY=.009f,UnderY=.010f,LineY=.012f,MarkY=.014f,TextY=.016f,FloorPaintTop=.03f;
        /// <summary>How far paint on a wall or a block face stands proud of it at most (m).</summary>
        public const float FacePaint=.03f;
        /// <summary>Fixtures above this height are out of every pilot's reach over the range, the ring and the lab (m).</summary>
        public const float RoofBand=5.5f;
        /// <summary>Draft constants the map does not carry (training_m1.py): the sprint straight (x from, x to, z from, z to), its
        /// run-out end, and the melee ring's apothem.</summary>
        public static readonly float[] Sprint={-46.5f,-16.5f,4.0f,7.4f};public const float SprintRunout=-10f,RingApothem=7f;
        /// <summary>The readout boards: range boards' bottom, cell height (a cell is <see cref="CellAspect"/> wide), header band
        /// height and frame margin (m); the melee board's bottom and its plane's z offset south of the range barrier.</summary>
        public const float BoardBottom=7.0f,HeaderHeight=.85f,FrameMargin=.18f,BoardDepth=.2f,MeleeBottom=5.8f,CellAspect=NativeTrainingReadoutRaster.Width/(float)NativeTrainingReadoutRaster.Height;
    }

    /// <summary>One authored pad as the presentation reads it: its pad index, lane (-1 for the ring), labels and facing.</summary>
    public sealed class NativeTrainingPadInfo
    {
        public int Index,Lane;public TrainingPad Pad;public string Id,Short,DistanceLabel,Header,Tag;public int Accent;public float FaceX,FaceZ;
        public bool Melee=>Lane<0;
    }

    /// <summary>The Training Room's layout derived from the map (deterministic: the same map gives the same plan).</summary>
    public sealed class NativeTrainingPlan
    {
        public float FiringX,LaneHalf,RangeEndX,StripZ0,StripZ1,MinX,MaxX,MinZ,MaxZ,Roof,RingX,RingZ;
        public float[] LaneZ,Distances;public NativeTrainingPadInfo[] Pads;
        public static NativeTrainingPlan Of(ContentMap map)
        {
            if(map==null||map.trainingPads==null||map.trainingPads.Length==0)throw new InvalidOperationException("Training look: the map has no training pads");
            var p=new NativeTrainingPlan();var b=map.bounds;p.MinX=b?.minX??-48;p.MaxX=b?.maxX??48;p.MinZ=b?.minZ??-28;p.MaxZ=b?.maxZ??28;p.Roof=b!=null&&b.ceiling>0?b.ceiling:14;
            var range=new List<TrainingPad>();var melee=new List<TrainingPad>();foreach(var pad in map.trainingPads)(pad.kind=="melee"?melee:range).Add(pad);
            // the firing line: every range pad's x less its authored distance (the median, so one odd pad cannot move it)
            var fx=new List<float>();foreach(var pad in range)fx.Add(pad.pos.x-pad.distance);fx.Sort();p.FiringX=fx.Count>0?fx[fx.Count/2]:p.MinX+6;
            // lanes: range pads clustered by z (gaps under 1 m join), each lane at its most common z
            var zs=new List<float>();foreach(var pad in range)zs.Add(pad.pos.z);zs.Sort();var lanes=new List<float>();
            for(int i=0;i<zs.Count;)
            {
                int j=i;while(j+1<zs.Count&&zs[j+1]-zs[j]<1f)j++;
                float best=zs[i];int bestN=0;for(int k=i;k<=j;k++){int n=0;for(int q=i;q<=j;q++)if(Math.Abs(zs[q]-zs[k])<1e-3f)n++;if(n>bestN){bestN=n;best=zs[k];}}
                lanes.Add(best);i=j+1;
            }
            p.LaneZ=lanes.ToArray();float pitch=float.MaxValue;for(int i=1;i<p.LaneZ.Length;i++)pitch=Math.Min(pitch,p.LaneZ[i]-p.LaneZ[i-1]);
            p.LaneHalf=p.LaneZ.Length>1?Math.Max(.6f,(pitch-.3f)/2):1.75f;
            var ds=new List<float>();foreach(var pad in range){float d=(float)Math.Round(pad.distance,2);if(!ds.Contains(d))ds.Add(d);}ds.Sort();p.Distances=ds.ToArray();
            float maxD=ds.Count>0?ds[ds.Count-1]:0;p.RangeEndX=p.FiringX+maxD+5;p.StripZ0=p.LaneZ.Length>0?p.LaneZ[0]-p.LaneHalf-2:p.MinZ;p.StripZ1=p.MaxZ;
            foreach(var blk in map.blocks??Array.Empty<ContentBlock>())
            {
                if(blk?.id=="training-backstop-range")p.RangeEndX=blk.pos.x-blk.size.x/2;
                if(blk?.id=="training-barrier-range")p.StripZ0=blk.pos.z+blk.size.z/2;
            }
            float rx=0,rz=0;foreach(var pad in melee){rx+=pad.pos.x;rz+=pad.pos.z;}if(melee.Count>0){p.RingX=rx/melee.Count;p.RingZ=rz/melee.Count;}
            p.Pads=new NativeTrainingPadInfo[map.trainingPads.Length];
            for(int i=0;i<p.Pads.Length;i++)
            {
                var pad=map.trainingPads[i];var info=new NativeTrainingPadInfo{Index=i,Pad=pad,Id=pad.id,Lane=-1};double yaw=pad.yaw*Math.PI/180;info.FaceX=(float)Math.Sin(yaw);info.FaceZ=(float)Math.Cos(yaw);
                if(pad.kind!="melee"){float bestD=float.MaxValue;for(int l=0;l<p.LaneZ.Length;l++){float d=Math.Abs(pad.pos.z-p.LaneZ[l]);if(d<bestD){bestD=d;info.Lane=l;}}}
                info.DistanceLabel=Metres(pad.distance);
                string tail=pad.id!=null&&pad.id.StartsWith("training-pad-",StringComparison.Ordinal)?pad.id.Substring("training-pad-".Length):pad.id??"";
                if(info.Melee){string side=tail.StartsWith("melee-",StringComparison.Ordinal)?tail.Substring(6):tail;info.Short=("M-"+side).ToUpperInvariant();info.Header=("MELEE "+side).ToUpperInvariant();info.Tag=info.DistanceLabel;info.Accent=NativeTrainingLook.Melee;}
                else{info.Short="L"+(info.Lane+1)+"-"+Metres(pad.distance,false);info.Header="L"+(info.Lane+1)+" · "+info.DistanceLabel;info.Tag=pad.kind=="strafe"?"STRAFE":"STATIC";info.Accent=pad.kind=="strafe"?NativeTrainingLook.Magenta:NativeTrainingLook.Cyan;}
                p.Pads[i]=info;
            }
            return p;
        }
        /// <summary>A distance label: whole metres as "20 M", otherwise one decimal ("3.5 M"); without the unit when `unit` is false.</summary>
        public static string Metres(float d,bool unit=true)
        {
            double r=Math.Round(d,1);string n=Math.Abs(r-Math.Round(r))<1e-6?((int)Math.Round(r)).ToString(System.Globalization.CultureInfo.InvariantCulture):r.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
            return unit?n+" M":n;
        }
        /// <summary>The pad on lane `lane` at authored distance `d` (-1 none).</summary>
        public int PadAt(int lane,float d){foreach(var i in Pads)if(i.Lane==lane&&Math.Abs(i.Pad.distance-d)<.01f)return i.Index;return -1;}
    }

    /// <summary>
    /// A 5 x 7 pixel font for the Training Room's paint, signs and readouts: the hazard kit's letters (NativeKitFont, A-Z) and
    /// its own digits and marks. Lower case reads as upper case; an unknown character is blank. Glyph cells are y down from the
    /// top; a glyph advances 6 cells. Allocation free after the first use (rectangles built once per glyph).
    /// </summary>
    public static class NativeTrainingFont
    {
        public const int Columns=5,Rows=7,Advance=6;
        private const string Marks="0123456789.,:-+/%·()<>#=±'_";
        private static readonly string[] Shapes={
            ".###.#...##..###.#.###..##...#.###.", "..#...##....#....#....#....#...###.", ".###.#...#....#...#...#...#...#####", ".###.#...#....#..##.....##...#.###.",
            "...#...##..#.#.#..#.#####...#....#.", "######....####.....#....##...#.###.", "..##..#...#....####.#...##...#.###.", "#####....#...#...#...#....#....#...",
            ".###.#...##...#.###.#...##...#.###.", ".###.#...##...#.####....#...#..##..", "...............................##..", "..........................#...#....",
            "......##...##.........##...##......", "...............###.................", ".......#....#..#####..#....#.......", "....#....#...#...#...#...#....#....",
            "##..###..#...#...#...#...#..###..##", "...............##...##.............", "...#...#...#....#....#.....#.....#.", ".#.....#.....#....#....#...#...#...",
            ".#.....#.....#.....#...#...#...#...", "...#...#...#...#.....#.....#.....#.", ".#.#..#.#.#####.#.#.#####.#.#..#.#.", "..........#####.....#####..........",
            "..#....#..#####..#....#.......#####", "..#....#...#.......................", "..............................#####"};
        private static readonly int[][] rects=new int[128+1][];
        /// <summary>Whether cell (x, y) of glyph `c` is lit (x 0..4, y 0..6 from the top).</summary>
        public static bool Lit(char c,int x,int y)
        {
            if(x<0||x>=Columns||y<0||y>=Rows)return false;if(c>='a'&&c<='z')c=(char)(c-32);
            if(c>='A'&&c<='Z')return NativeKitFont.Lit(c-'A',x,y);
            int m=Marks.IndexOf(c);return m>=0&&Shapes[m][y*Columns+x]=='#';
        }
        /// <summary>The glyph's lit cells as greedy rectangles (x0, x1, y0, y1 cells, ends exclusive), built once per glyph.</summary>
        public static int[] Rects(char c)
        {
            if(c>='a'&&c<='z')c=(char)(c-32);int slot=c<128?c:c=='·'?128:-1;if(slot<0)slot=0;
            var r=rects[slot];if(r!=null)return r;var used=new bool[Columns,Rows];var list=new List<int>();
            for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
            {
                if(!Lit(c,x,y)||used[x,y])continue;int x1=x;while(x1+1<Columns&&Lit(c,x1+1,y)&&!used[x1+1,y])x1++;
                int y1=y;bool grow=true;while(grow&&y1+1<Rows){for(int k=x;k<=x1;k++)if(!Lit(c,k,y1+1)||used[k,y1+1]){grow=false;break;}if(grow)y1++;}
                for(int yy=y;yy<=y1;yy++)for(int k=x;k<=x1;k++)used[k,yy]=true;list.Add(x);list.Add(x1+1);list.Add(y);list.Add(y1+1);
            }
            return rects[slot]=list.ToArray();
        }
        /// <summary>A text's width in cells.</summary>
        public static int Width(int length)=>length<=0?0:length*Advance-1;
    }

    /// <summary>
    /// A flat-coloured mesh for ChipArena/SourceLit in the scenery mesh's layout (linear colour, linear emission in uv1): quads
    /// with a chosen facing, axis-aligned boxes, discs, arcs and 5 x 7 text on any plane. Every emitted piece is also recorded
    /// with its bounds and where it sits (<see cref="Parts"/>) for the placement checks. Allocates while building only.
    /// </summary>
    public sealed class NativeTrainingMesh
    {
        /// <summary>Where a piece sits: floor paint, a hall wall's inner face, a block's face, or the roof band.</summary>
        public enum Place{Floor,Wall,Face,Roof}
        public struct Part{public float X0,Y0,Z0,X1,Y1,Z1;public Place At;public string Tag;}
        public readonly List<Part> Parts=new List<Part>();
        private readonly List<float> p=new List<float>(),n=new List<float>(),c=new List<float>(),e=new List<float>();private readonly List<int> t=new List<int>();
        public Place At=Place.Floor;public string Tag="";
        public int Quads{get;private set;}
        private static float Ch(int hex,int shift)=>(float)NativeSceneryBuilder.Channel(hex,shift);
        private void Record(float x0,float y0,float z0,float x1,float y1,float z1)=>Parts.Add(new Part{X0=Math.Min(x0,x1),Y0=Math.Min(y0,y1),Z0=Math.Min(z0,z1),X1=Math.Max(x0,x1),Y1=Math.Max(y0,y1),Z1=Math.Max(z0,z1),At=At,Tag=Tag});
        /// <summary>A quad a, b, c, d (in order round its edge) facing (nx, ny, nz); `glow` an sRGB emission colour times `level`.</summary>
        public void Quad(V3 a,V3 b,V3 cc,V3 d,V3 normal,int hex,int glow=0,float level=0)
        {
            int start=p.Count/3;float r=Ch(hex,16),g=Ch(hex,8),bl=Ch(hex,0),er=glow==0?0:Ch(glow,16)*level,eg=glow==0?0:Ch(glow,8)*level,eb=glow==0?0:Ch(glow,0)*level;
            foreach(var v in new[]{a,b,cc,d}){p.Add(v.x);p.Add(v.y);p.Add(v.z);n.Add(normal.x);n.Add(normal.y);n.Add(normal.z);c.Add(r);c.Add(g);c.Add(bl);c.Add(1);e.Add(er);e.Add(eg);e.Add(eb);e.Add(0);}
            // Unity front faces: cross(b - a, c - a) along the normal for (a, b, c).
            V3 u=b-a,w=cc-a;float cx=u.y*w.z-u.z*w.y,cy=u.z*w.x-u.x*w.z,cz=u.x*w.y-u.y*w.x;
            if(cx*normal.x+cy*normal.y+cz*normal.z>=0){t.Add(start);t.Add(start+1);t.Add(start+2);t.Add(start);t.Add(start+2);t.Add(start+3);}
            else{t.Add(start);t.Add(start+2);t.Add(start+1);t.Add(start);t.Add(start+3);t.Add(start+2);}
            Quads++;
            float x0=Math.Min(Math.Min(a.x,b.x),Math.Min(cc.x,d.x)),x1=Math.Max(Math.Max(a.x,b.x),Math.Max(cc.x,d.x)),y0=Math.Min(Math.Min(a.y,b.y),Math.Min(cc.y,d.y)),y1=Math.Max(Math.Max(a.y,b.y),Math.Max(cc.y,d.y)),z0=Math.Min(Math.Min(a.z,b.z),Math.Min(cc.z,d.z)),z1=Math.Max(Math.Max(a.z,b.z),Math.Max(cc.z,d.z));
            Record(x0,y0,z0,x1,y1,z1);
        }
        /// <summary>A rectangle on the floor at height y (x0..x1, z0..z1), facing up.</summary>
        public void Floor(float x0,float z0,float x1,float z1,float y,int hex,int glow=0,float level=0)
        {if(x1<=x0||z1<=z0)return;Quad(new V3(x0,y,z0),new V3(x1,y,z0),new V3(x1,y,z1),new V3(x0,y,z1),new V3(0,1,0),hex,glow,level);}
        /// <summary>A rectangle centred at (cx, cz) turned by `yaw` radians (its length along the turned x), on the floor at y.</summary>
        public void FloorTurned(float cx,float cz,float length,float width,float yaw,float y,int hex,int glow=0,float level=0)
        {
            float ux=(float)Math.Cos(yaw),uz=(float)-Math.Sin(yaw),vx=-uz,vz=ux;float hl=length/2,hw=width/2;
            Quad(new V3(cx-ux*hl-vx*hw,y,cz-uz*hl-vz*hw),new V3(cx+ux*hl-vx*hw,y,cz+uz*hl-vz*hw),new V3(cx+ux*hl+vx*hw,y,cz+uz*hl+vz*hw),new V3(cx-ux*hl+vx*hw,y,cz-uz*hl+vz*hw),new V3(0,1,0),hex,glow,level);
        }
        /// <summary>An axis-aligned box; `skip` drops faces (1 +x, 2 -x, 4 +y, 8 -y, 16 +z, 32 -z).</summary>
        public void Box(float x0,float y0,float z0,float x1,float y1,float z1,int hex,int glow=0,float level=0,int skip=0)
        {
            if(x1<=x0||y1<=y0||z1<=z0)return;
            if((skip&1)==0)Quad(new V3(x1,y0,z0),new V3(x1,y1,z0),new V3(x1,y1,z1),new V3(x1,y0,z1),new V3(1,0,0),hex,glow,level);
            if((skip&2)==0)Quad(new V3(x0,y0,z0),new V3(x0,y1,z0),new V3(x0,y1,z1),new V3(x0,y0,z1),new V3(-1,0,0),hex,glow,level);
            if((skip&4)==0)Quad(new V3(x0,y1,z0),new V3(x1,y1,z0),new V3(x1,y1,z1),new V3(x0,y1,z1),new V3(0,1,0),hex,glow,level);
            if((skip&8)==0)Quad(new V3(x0,y0,z0),new V3(x1,y0,z0),new V3(x1,y0,z1),new V3(x0,y0,z1),new V3(0,-1,0),hex,glow,level);
            if((skip&16)==0)Quad(new V3(x0,y0,z1),new V3(x1,y0,z1),new V3(x1,y1,z1),new V3(x0,y1,z1),new V3(0,0,1),hex,glow,level);
            if((skip&32)==0)Quad(new V3(x0,y0,z0),new V3(x1,y0,z0),new V3(x1,y1,z0),new V3(x0,y1,z0),new V3(0,0,-1),hex,glow,level);
        }
        /// <summary>A flat ring (r0..r1) round (cx, cz) at height y from angle a0 to a1 (radians from +x toward +z), `segments` steps.</summary>
        public void Arc(float cx,float cz,float y,float r0,float r1,float a0,float a1,int segments,int hex,int glow=0,float level=0)
        {
            for(int i=0;i<segments;i++)
            {
                double s=a0+(a1-a0)*i/segments,f=a0+(a1-a0)*(i+1)/segments;
                V3 P(double a,float r)=>new V3(cx+(float)Math.Cos(a)*r,y,cz+(float)Math.Sin(a)*r);
                Quad(P(s,r0),P(s,r1),P(f,r1),P(f,r0),new V3(0,1,0),hex,glow,level);
            }
        }
        /// <summary>A filled disc round (cx, cz) at height y.</summary>
        public void Disc(float cx,float cz,float y,float r,int segments,int hex,int glow=0,float level=0)
        {
            for(int i=0;i<segments;i+=2)
            {
                double a=Math.PI*2*i/segments,b=Math.PI*2*(i+1)/segments,cc=Math.PI*2*(i+2)/segments;
                V3 P(double q)=>new V3(cx+(float)Math.Cos(q)*r,y,cz+(float)Math.Sin(q)*r);
                Quad(new V3(cx,y,cz),P(a),P(b),P(cc),new V3(0,1,0),hex,glow,level);
            }
        }
        /// <summary>
        /// Text centred on `at` (align -1 left, 0 centre, 1 right of it along the reading direction `u`), its glyphs' up `v`, on the
        /// plane facing `normal`; `cu` and `cv` the cell size (m) along u and v (stretch a floor text along the view with cv > cu).
        /// Returns its width along u (m).
        /// </summary>
        public float Text(string text,V3 at,V3 u,V3 v,V3 normal,float cu,float cv,int hex,int glow=0,float level=0,int align=0)
        {
            if(string.IsNullOrEmpty(text))return 0;float width=NativeTrainingFont.Width(text.Length)*cu,start=align<0?0:align>0?-width:-width/2;
            for(int i=0;i<text.Length;i++)
            {
                var rs=NativeTrainingFont.Rects(text[i]);
                for(int k=0;k<rs.Length;k+=4)
                {
                    float u0=start+(i*NativeTrainingFont.Advance+rs[k])*cu,u1=start+(i*NativeTrainingFont.Advance+rs[k+1])*cu,v0=(3.5f-rs[k+3])*cv,v1=(3.5f-rs[k+2])*cv;
                    V3 P(float a,float b)=>at+u*a+v*b;
                    Quad(P(u0,v0),P(u1,v0),P(u1,v1),P(u0,v1),normal,hex,glow,level);
                }
            }
            return width;
        }
        /// <summary>Floor text at height y: reading along (ux, 0, uz), glyphs up along (vx, 0, vz).</summary>
        public float FloorText(string text,float x,float z,float y,float ux,float uz,float vx,float vz,float cu,float cv,int hex,int glow=0,float level=0,int align=0)
        =>Text(text,new V3(x,y,z),new V3(ux,0,uz),new V3(vx,0,vz),new V3(0,1,0),cu,cv,hex,glow,level,align);
        public NativeSceneryMesh Bake()
        {
            var m=new NativeSceneryMesh{Positions=p.ToArray(),Normals=n.ToArray(),Colors=c.ToArray(),Emission=e.ToArray(),Indices=t.ToArray(),Boxes=Quads};
            m.MinX=m.MinY=m.MinZ=float.MaxValue;m.MaxX=m.MaxY=m.MaxZ=float.MinValue;
            for(int i=0;i<m.Positions.Length;i+=3){m.MinX=Math.Min(m.MinX,m.Positions[i]);m.MaxX=Math.Max(m.MaxX,m.Positions[i]);m.MinY=Math.Min(m.MinY,m.Positions[i+1]);m.MaxY=Math.Max(m.MaxY,m.Positions[i+1]);m.MinZ=Math.Min(m.MinZ,m.Positions[i+2]);m.MaxZ=Math.Max(m.MaxZ,m.Positions[i+2]);}
            if(m.Positions.Length==0)m.MinX=m.MinY=m.MinZ=m.MaxX=m.MaxY=m.MaxZ=0;
            return m;
        }
    }

    /// <summary>A floor grid quad for the studio shader (Resources/NativeTrainingStudio.shader, mode 0): its rectangle at
    /// <see cref="NativeTrainingLook.GridY"/>, the lines' origin, which line families it draws (lines of constant x, of constant z)
    /// and the rectangle it leaves to another grid (zero area: none).</summary>
    public struct NativeTrainingGrid{public float X0,Z0,X1,Z1,OriginX,OriginZ,LinesX,LinesZ,CutX0,CutZ0,CutX1,CutZ1;}

    /// <summary>A readout cell: the pad it shows and its quad (centre, outward normal, the viewer's right, width, height).</summary>
    public struct NativeTrainingCell{public int Pad;public V3 Center,Normal,Right;public float Width,Height;}

    /// <summary>
    /// The Training Room's static presentation, built once per map (<see cref="Build"/>):
    /// <list type="bullet">
    /// <item>`Paint`: floor and wall paint (drawn without outlines): the zone fills and labels, the firing line, lane edges,
    /// shooter's boxes and lane numbers, the range ruler, each pad's disc, ring and facing mark, its ID plate with the authored
    /// distance label, the strafe pad's rail from rail.a to rail.b with its ticks and stops, the melee pads' reach arcs, the
    /// spawn pad, the sprint straight's marks every 5 m, step, gap, deck, cover and window heights, the destruction samples'
    /// colour plates, the hazard bay's labels, the walls' wainscot, seams and height rulers.</item>
    /// <item>`Fixtures`: the readout boards' housings, header lettering and hangers, and the roof's light panels (outlined).</item>
    /// <item>`Grids`: the hall's metre grid and the range's own grid measured from the firing line.</item>
    /// <item>`Cells`: one readout cell per pad on the range boards (one board per authored distance, a cell over each lane) and
    /// the melee board north of the ring.</item>
    /// </list>
    /// </summary>
    public sealed class NativeTrainingMarks
    {
        public NativeTrainingPlan Plan;public NativeSceneryMesh Paint,Fixtures;public NativeTrainingGrid[] Grids;public NativeTrainingCell[] Cells;
        /// <summary>Every piece with its place, for the placement checks.</summary>
        public List<NativeTrainingMesh.Part> Parts=new List<NativeTrainingMesh.Part>();
        private static readonly V3 Up=new V3(0,1,0);
        public static NativeTrainingMarks Build(ContentMap map)
        {
            var plan=NativeTrainingPlan.Of(map);var m=new NativeTrainingMarks{Plan=plan};var paint=new NativeTrainingMesh();var fix=new NativeTrainingMesh();
            Grid(m,plan);Range(paint,plan,map);Pads(paint,plan);Zones(paint,plan,map);Walls(paint,plan);Boards(m,fix,plan);Roof(fix,plan);
            m.Paint=paint.Bake();m.Fixtures=fix.Bake();m.Parts.AddRange(paint.Parts);m.Parts.AddRange(fix.Parts);return m;
        }

        // ---------------- the grids ----------------
        private static void Grid(NativeTrainingMarks m,NativeTrainingPlan p)
        {
            float lz0=p.LaneZ[0]-p.LaneHalf,lz1=p.LaneZ[p.LaneZ.Length-1]+p.LaneHalf;
            m.Grids=new[]{
                // the hall: lines on whole metres both ways, leaving the lanes to the range grid
                new NativeTrainingGrid{X0=p.MinX,Z0=p.MinZ,X1=p.MaxX,Z1=p.MaxZ,OriginX=0,OriginZ=0,LinesX=1,LinesZ=1,CutX0=p.FiringX,CutZ0=lz0,CutX1=p.RangeEndX,CutZ1=lz1},
                // the lanes: lines across them every metre from the firing line (5 m and 10 m bolder: the distances)
                new NativeTrainingGrid{X0=p.FiringX,Z0=lz0,X1=p.RangeEndX,Z1=lz1,OriginX=p.FiringX,OriginZ=0,LinesX=1,LinesZ=0}};
        }

        // ---------------- the range: firing line, lanes, ruler ----------------
        private static void Range(NativeTrainingMesh g,NativeTrainingPlan p,ContentMap map)
        {
            g.At=NativeTrainingMesh.Place.Floor;g.Tag="range";float fx=p.FiringX,h=p.LaneHalf;int lanes=p.LaneZ.Length;
            float lz0=p.LaneZ[0]-h,lz1=p.LaneZ[lanes-1]+h;
            // the firing line: a cyan strip across the strip, with a white keyline each side
            g.Floor(fx-.08f,p.StripZ0+.05f,fx+.08f,p.StripZ1-.05f,NativeTrainingLook.LineY,NativeTrainingLook.Cyan,NativeTrainingLook.Cyan,.55f);
            g.Floor(fx-.16f,p.StripZ0+.05f,fx-.11f,p.StripZ1-.05f,NativeTrainingLook.LineY,NativeTrainingLook.Paint);
            g.Floor(fx+.11f,p.StripZ0+.05f,fx+.16f,p.StripZ1-.05f,NativeTrainingLook.LineY,NativeTrainingLook.Paint);
            // "FIRING LINE" behind it in the south walkway, read facing downrange (up +x, reading -z)
            g.FloorText("FIRING LINE",fx-.75f,(p.StripZ0+lz0)/2,NativeTrainingLook.TextY,0,-1,1,0,.03f,.07f,NativeTrainingLook.Cyan,NativeTrainingLook.Cyan,.35f);
            // lane edges where no kerb stands (south of lane 1, north of the last lane), dashed white
            foreach(float ez in new[]{lz0,lz1})for(float x=fx+.5f;x<p.RangeEndX-.5f;x+=2f)g.Floor(x,ez-.03f,Math.Min(x+1.2f,p.RangeEndX-.5f),ez+.03f,NativeTrainingLook.LineY,NativeTrainingLook.Paint);
            // each lane: the shooter's box behind the line and the lane number in it, read facing downrange
            for(int l=0;l<lanes;l++)
            {
                float z=p.LaneZ[l],bx0=fx-2.0f,bx1=fx-.3f,bz0=z-h+.25f,bz1=z+h-.25f,w=.06f;
                g.Floor(bx0,bz0,bx1,bz0+w,NativeTrainingLook.LineY,NativeTrainingLook.Paint);g.Floor(bx0,bz1-w,bx1,bz1,NativeTrainingLook.LineY,NativeTrainingLook.Paint);
                g.Floor(bx0,bz0,bx0+w,bz1,NativeTrainingLook.LineY,NativeTrainingLook.Paint);g.Floor(bx1-w,bz0,bx1,bz1,NativeTrainingLook.LineY,NativeTrainingLook.Paint);
                g.FloorText((l+1).ToString(System.Globalization.CultureInfo.InvariantCulture),(bx0+bx1)/2,z,NativeTrainingLook.TextY,0,-1,1,0,.13f,.2f,NativeTrainingLook.Paint);
            }
            // the lane markers: a lit cyan strip along every kerb's top, a white one along the barrier's
            g.At=NativeTrainingMesh.Place.Face;
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;bool kerb=b.id.StartsWith("training-kerb-range-",StringComparison.Ordinal),barrier=b.id=="training-barrier-range";if(!kerb&&!barrier)continue;
                float top=b.pos.y+b.size.y/2+.006f,x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2;g.Tag="lane marker "+b.id;
                g.Floor(x0+.05f,b.pos.z-.05f,x1-.05f,b.pos.z+.05f,top,kerb?NativeTrainingLook.Cyan:NativeTrainingLook.Paint,kerb?NativeTrainingLook.Cyan:0,kerb?.7f:0);
            }
            g.At=NativeTrainingMesh.Place.Floor;
            // the ruler along the barrier side: ticks every metre, long every 5 m, numbers every 10 m (read facing downrange)
            float rz=(p.StripZ0+lz0)/2;
            for(int d=1;fx+d<p.RangeEndX-.3f;d++)
            {
                bool five=d%5==0,ten=d%10==0;float len=ten?.9f:five?.6f:.25f;
                g.Floor(fx+d-(ten?.03f:.02f),lz0-.12f-len,fx+d+(ten?.03f:.02f),lz0-.12f,NativeTrainingLook.LineY,ten?NativeTrainingLook.Paint:NativeTrainingLook.Seam);
                if(ten||d==5||d==15||d==25||d==35||d==45||d==55||d==65||d==75)
                    g.FloorText(d.ToString(System.Globalization.CultureInfo.InvariantCulture),fx+d+.45f,rz-.45f,NativeTrainingLook.TextY,0,-1,1,0,.05f,.08f,ten?NativeTrainingLook.Paint:NativeTrainingLook.Seam);
            }
        }

        // ---------------- the pads ----------------
        private static void Pads(NativeTrainingMesh g,NativeTrainingPlan p)
        {
            g.At=NativeTrainingMesh.Place.Floor;
            foreach(var info in p.Pads)
            {
                var pad=info.Pad;float x=pad.pos.x,z=pad.pos.z;bool melee=info.Melee,strafe=pad.kind=="strafe";int ring=melee?NativeTrainingLook.Melee:strafe?NativeTrainingLook.Magenta:NativeTrainingLook.Cyan;g.Tag="pad "+info.Id;
                if(strafe&&pad.rail!=null){Rail(g,pad);g.Tag="pad "+info.Id;}
                // the puck: a dark disc, its ring in the kind's colour, a crosshair and the facing chevron
                g.Disc(x,z,NativeTrainingLook.PadY,.66f,40,NativeTrainingLook.PadFill);
                g.Arc(x,z,NativeTrainingLook.MarkY,.56f,.64f,0,(float)(Math.PI*2),40,ring,ring,.6f);
                foreach(var (ax,az) in new[]{(1f,0f),(-1f,0f),(0f,1f),(0f,-1f)})g.FloorTurned(x+ax*.3f,z+az*.3f,.22f,.035f,(float)Math.Atan2(-az,ax),NativeTrainingLook.MarkY,NativeTrainingLook.Paint);
                g.Disc(x,z,NativeTrainingLook.TextY,.05f,12,NativeTrainingLook.Paint);
                float fxd=info.FaceX,fzd=info.FaceZ,cx=x+fxd*.78f,cz=z+fzd*.78f,lx=-fzd,lz=fxd,cy=NativeTrainingLook.MarkY;
                g.Quad(new V3(cx+fxd*.16f,cy,cz+fzd*.16f),new V3(cx+lx*.2f,cy,cz+lz*.2f),new V3(cx-fxd*.04f,cy,cz-fzd*.04f),new V3(cx-lx*.2f,cy,cz-lz*.2f),Up,ring,ring,.5f);
                // the ID plate in front of the pad (toward the shooter or the ring centre): the authored distance, then the ID
                if(melee)
                {
                    // behind the pad, read from the ring's centre (glyphs up outward)
                    float ox=x-p.RingX,oz=z-p.RingZ,ol=(float)Math.Sqrt(ox*ox+oz*oz);if(ol<1e-3f){ox=1;oz=0;ol=1;}ox/=ol;oz/=ol;float ux=oz,uz=-ox;
                    float pcx=x+ox*1.45f,pcz=z+oz*1.45f;
                    g.FloorTurned(pcx,pcz,1.5f,.86f,(float)Math.Atan2(-uz,ux),NativeTrainingLook.FillY,NativeTrainingLook.PlateFill);
                    g.FloorTurned(pcx-ox*.43f,pcz-oz*.43f,1.5f,.04f,(float)Math.Atan2(-uz,ux),NativeTrainingLook.LineY,ring,ring,.45f);
                    g.FloorText(info.DistanceLabel,pcx+ox*.15f,pcz+oz*.15f,NativeTrainingLook.TextY,ux,uz,ox,oz,Math.Min(.05f,1.3f/NativeTrainingFont.Width(info.DistanceLabel.Length)),.058f,NativeTrainingLook.Paint);
                    g.FloorText(info.Short,pcx-ox*.24f,pcz-oz*.24f,NativeTrainingLook.TextY,ux,uz,ox,oz,.032f,.036f,ring,ring,.3f);
                    Reach(g,x,z,-ox,-oz,ring);
                }
                else
                {
                    // range pads face the firing line (-x): the plate lies between the pad and the shooter, read facing downrange
                    float px0=x-2.65f,px1=x-1.05f,pz=z;string id=strafe?info.Short+" STRAFE":info.Short;
                    g.Floor(px0,pz-1.05f,px1,pz+1.05f,NativeTrainingLook.FillY,NativeTrainingLook.PlateFill);
                    g.Floor(px1-.05f,pz-1.05f,px1,pz+1.05f,NativeTrainingLook.LineY,ring,ring,.45f);
                    g.FloorText(info.DistanceLabel,px1-.48f,pz,NativeTrainingLook.TextY,0,-1,1,0,Math.Min(.06f,1.9f/NativeTrainingFont.Width(info.DistanceLabel.Length)),.11f,NativeTrainingLook.Paint);
                    g.FloorText(id,px0+.32f,pz,NativeTrainingLook.TextY,0,-1,1,0,Math.Min(.032f,1.9f/NativeTrainingFont.Width(id.Length)),.05f,ring,ring,.3f);
                }
            }
        }
        // The strafing rail from rail.a to rail.b: a dark slot, lit edges, stops at both ends, metre ticks and its ± labels.
        private static void Rail(NativeTrainingMesh g,TrainingPad pad)
        {
            var a=pad.rail.a;var b=pad.rail.b;float dx=b.x-a.x,dz=b.z-a.z,len=(float)Math.Sqrt(dx*dx+dz*dz);if(len<.1f)return;float ux=dx/len,uz=dz/len,vx=-uz,vz=ux,yaw=(float)Math.Atan2(-uz,ux);
            float cx=(a.x+b.x)/2,cz=(a.z+b.z)/2;g.Tag="rail "+pad.id;
            g.FloorTurned(cx,cz,len+.6f,.26f,yaw,NativeTrainingLook.RailY,NativeTrainingLook.Void);
            foreach(float s in new[]{-1f,1f})g.FloorTurned(cx+vx*s*.16f,cz+vz*s*.16f,len+.6f,.04f,yaw,NativeTrainingLook.LineY,NativeTrainingLook.Magenta,NativeTrainingLook.Magenta,.55f);
            foreach(var end in new[]{a,b})
            {
                float ex=end.x+(end.x==a.x&&end.z==a.z?-ux:ux)*.42f,ez=end.z+(end.x==a.x&&end.z==a.z?-uz:uz)*.42f;
                g.FloorTurned(ex,ez,.18f,.62f,yaw,NativeTrainingLook.LineY,NativeTrainingLook.Warn);
                for(int k=-1;k<=1;k++)g.FloorTurned(ex+vx*k*.2f,ez+vz*k*.2f,.19f,.06f,yaw+(float)(Math.PI/4),NativeTrainingLook.TextY,NativeTrainingLook.Void);
            }
            int ticks=(int)Math.Floor(len/2+1e-3);
            for(int k=-ticks;k<=ticks;k++)
            {
                float tx=cx+ux*k,tz=cz+uz*k,side=vx<=0?-1:1;g.FloorTurned(tx+vx*side*.36f,tz+vz*side*.36f,.03f,k==0?.34f:.2f,yaw,NativeTrainingLook.LineY,NativeTrainingLook.Paint);
                if(k==-ticks||k==0||k==ticks)
                {
                    string label=k==0?"0":(k>0?"+":"-")+Math.Abs(k).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    g.FloorText(label,tx+vx*side*.82f,tz+vz*side*.82f,NativeTrainingLook.TextY,0,-1,1,0,.04f,.06f,NativeTrainingLook.Paint);
                }
            }
        }
        // A melee pad's reach arcs on its inward side: 1 m and 2 m over ±45°, 3 m over ±32° (adjacent pads' arcs never cross).
        private static void Reach(NativeTrainingMesh g,float x,float z,float ix,float iz,int color)
        {
            float a=(float)Math.Atan2(iz,ix);
            foreach(var (r,span) in new[]{(1f,.785f),(2f,.785f),(3f,.56f)})
            {
                g.Arc(x,z,NativeTrainingLook.UnderY,r-.025f,r+.025f,a-span,a+span,24,color,color,.25f);
                float lx=x+(float)Math.Cos(a+span+.09f)*r,lz=z+(float)Math.Sin(a+span+.09f)*r;
                g.FloorText(((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture)+"M",lx,lz,NativeTrainingLook.TextY,-iz,ix,-ix,-iz,.026f,.03f,color,color,.2f);
            }
        }

        // ---------------- zones, course and lab marks ----------------
        private static ContentBlock Block(ContentMap map,string id){foreach(var b in map.blocks??Array.Empty<ContentBlock>())if(b?.id==id)return b;return null;}
        private static float Top(ContentBlock b)=>b.pos.y+b.size.y/2;
        private static void Zones(NativeTrainingMesh g,NativeTrainingPlan p,ContentMap map)
        {
            const float ZoneCell=.075f;int ink=NativeTrainingLook.Paint;g.At=NativeTrainingMesh.Place.Floor;
            // zone labels on the floor, read walking in from the spawn (up +z, reading +x)
            void Label(string text,float x,float z,float cell=ZoneCell){g.Tag="zone "+text;g.FloorText(text,x,z,NativeTrainingLook.TextY,1,0,0,1,cell,cell*1.15f,ink);}
            // the spawn pad: an outlined fill round the spawn points, a chevron on each, its label
            var sp=map.spawns?.ffa??Array.Empty<MapSpawn>();
            if(sp.Length>0)
            {
                float x0=float.MaxValue,x1=float.MinValue,z0=float.MaxValue,z1=float.MinValue;foreach(var s in sp){x0=Math.Min(x0,s.pos.x);x1=Math.Max(x1,s.pos.x);z0=Math.Min(z0,s.pos.z);z1=Math.Max(z1,s.pos.z);}
                x0-=1.5f;x1+=1.5f;z0-=1.5f;z1+=1.5f;g.Tag="spawn pad";
                g.Floor(x0,z0,x1,z1,NativeTrainingLook.FillY,0x8F99A5);
                foreach(var (a0,b0,a1,b1) in new[]{(x0,z0,x1,z0+.08f),(x0,z1-.08f,x1,z1),(x0,z0,x0+.08f,z1),(x1-.08f,z0,x1,z1)})g.Floor(a0,b0,a1,b1,NativeTrainingLook.LineY,NativeTrainingLook.Cyan,NativeTrainingLook.Cyan,.45f);
                foreach(var s in sp)
                {
                    double yaw=s.yaw*Math.PI/180;float fx=(float)Math.Sin(yaw),fz=(float)Math.Cos(yaw),lx=-fz,lz=fx,cx=s.pos.x,cz=s.pos.z;
                    g.Quad(new V3(cx+fx*.45f,NativeTrainingLook.LineY,cz+fz*.45f),new V3(cx+lx*.4f-fx*.15f,NativeTrainingLook.LineY,cz+lz*.4f-fz*.15f),new V3(cx,NativeTrainingLook.LineY,cz+fz*.08f*0),new V3(cx-lx*.4f-fx*.15f,NativeTrainingLook.LineY,cz-lz*.4f-fz*.15f),Up,NativeTrainingLook.Paint);
                }
                Label("SPAWN",(x0+x1)/2,z0+.55f,.06f);
            }
            // the range's entrance and the ring
            Label("FIRING RANGE",p.FiringX-2.4f,p.StripZ0-1.2f,.07f);
            if(p.Pads.Length>0)
            {
                Label("MELEE RING",p.RingX,p.RingZ-NativeTrainingLook.RingApothem-.95f,.07f);g.Tag="ring centre";
                g.Floor(p.RingX-.6f,p.RingZ-.03f,p.RingX+.6f,p.RingZ+.03f,NativeTrainingLook.LineY,ink);g.Floor(p.RingX-.03f,p.RingZ-.6f,p.RingX+.03f,p.RingZ+.6f,NativeTrainingLook.LineY,ink);
                g.Arc(p.RingX,p.RingZ,NativeTrainingLook.MarkY,.32f,.37f,0,(float)(Math.PI*2),24,ink);
            }
            // the sprint straight: edges, start, a mark every 5 m with its distance (read sprinting east), the run-out hatched
            {
                float sx0=NativeTrainingLook.Sprint[0],sx1=NativeTrainingLook.Sprint[1],sz0=NativeTrainingLook.Sprint[2],sz1=NativeTrainingLook.Sprint[3];g.Tag="sprint";
                g.Floor(sx0,sz0,NativeTrainingLook.SprintRunout,sz1,NativeTrainingLook.FillY,0x98A2AD);
                g.Floor(sx0,sz0,NativeTrainingLook.SprintRunout,sz0+.07f,NativeTrainingLook.LineY,ink);g.Floor(sx0,sz1-.07f,NativeTrainingLook.SprintRunout,sz1,NativeTrainingLook.LineY,ink);
                for(int k=0;sx0+5*k<=sx1+1e-3f;k++)
                {
                    float x=sx0+5*k;bool end=k==0||x>=sx1-1e-3f;g.Floor(x-(end?.08f:.05f),sz0,x+(end?.08f:.05f),sz1,NativeTrainingLook.MarkY,NativeTrainingLook.Cyan,NativeTrainingLook.Cyan,end?.6f:.4f);
                    g.FloorText((5*k).ToString(System.Globalization.CultureInfo.InvariantCulture),x+.55f,sz0+.75f,NativeTrainingLook.TextY,0,-1,1,0,.06f,.1f,ink);
                }
                for(float x=sx1+.4f;x<NativeTrainingLook.SprintRunout-.3f;x+=.9f)g.FloorTurned(x+.25f,(sz0+sz1)/2,(sz1-sz0)*1.2f,.12f,(float)(Math.PI*.3),NativeTrainingLook.UnderY,0x7A8592);
                g.FloorTurned((sx1+NativeTrainingLook.SprintRunout)/2,sz0-.02f,NativeTrainingLook.SprintRunout-sx1,.04f,0,NativeTrainingLook.LineY,ink);
                Label("SPRINT "+NativeTrainingPlan.Metres(sx1-sx0),sx0+4.6f,sz0-.62f,.06f);
            }
            // the movement course: each step's height on its top, the gaps' widths between the platforms, deck heights, nosings
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;float top=Top(b),x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
                if(b.id.StartsWith("training-step-",StringComparison.Ordinal))
                {
                    g.At=NativeTrainingMesh.Place.Face;g.Tag="step "+b.id;
                    g.FloorText(top.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),b.pos.x,b.pos.z,top+.006f,1,0,0,1,.07f,.085f,NativeTrainingLook.Panel);
                    g.Floor(x0,z1-.1f,x1,z1,top+.006f,NativeTrainingLook.Warn);
                    // its height on the north riser too (read from the floor beside it)
                    string riser=top.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" M";float rh=Math.Min(.06f,Math.Min(b.size.y*.11f,(b.size.x-.3f)/NativeTrainingFont.Width(riser.Length)));
                    if(rh>.012f)g.Text(riser,new V3(b.pos.x,top-rh*4.6f,z1+.006f),new V3(-1,0,0),Up,new V3(0,0,1),rh,rh,NativeTrainingLook.Panel);
                    g.At=NativeTrainingMesh.Place.Floor;
                }
                else if(b.id.StartsWith("training-platform-gap-",StringComparison.Ordinal))
                {
                    g.At=NativeTrainingMesh.Place.Face;g.Tag="platform "+b.id;g.Floor(x0,z0,x0+.1f,z1,top+.006f,NativeTrainingLook.Warn);g.Floor(x1-.1f,z0,x1,z1,top+.006f,NativeTrainingLook.Warn);g.At=NativeTrainingMesh.Place.Floor;
                }
                else if(b.id.StartsWith("training-deck-tower-",StringComparison.Ordinal)&&!b.id.EndsWith("-wing",StringComparison.Ordinal))
                {
                    g.At=NativeTrainingMesh.Place.Face;g.Tag="deck "+b.id;string label="DECK "+top.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" M";
                    g.FloorText(label,b.pos.x,z1-.75f,top+.006f,1,0,0,1,.07f,.08f,NativeTrainingLook.Warn);g.At=NativeTrainingMesh.Place.Floor;
                }
            }
            // the gaps between consecutive platforms: their width on the floor
            var plats=new List<ContentBlock>();foreach(var b in map.blocks??Array.Empty<ContentBlock>())if(b?.id!=null&&b.id.StartsWith("training-platform-gap-",StringComparison.Ordinal))plats.Add(b);
            plats.Sort((a,b)=>a.pos.x.CompareTo(b.pos.x));
            for(int i=1;i<plats.Count;i++)
            {
                float gx0=plats[i-1].pos.x+plats[i-1].size.x/2,gx1=plats[i].pos.x-plats[i].size.x/2,gz=plats[i].pos.z;if(gx1-gx0<.5f)continue;g.Tag="gap";
                g.FloorText(NativeTrainingPlan.Metres(gx1-gx0),(gx0+gx1)/2,gz,NativeTrainingLook.TextY,1,0,0,1,.055f,.07f,ink);
                g.Floor(gx0+.05f,gz-1.3f,gx1-.05f,gz-1.25f,NativeTrainingLook.LineY,ink);
            }
            if(plats.Count>0)Label("MOVEMENT COURSE",-31.5f,-22.6f,.07f);
            // the cover lab: heights on the cover pieces' west faces, the window and door marks, the samples' colour plates
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null)continue;float top=Top(b),x0=b.pos.x-b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
                if(b.id.StartsWith("training-cover-",StringComparison.Ordinal))
                {
                    g.At=NativeTrainingMesh.Place.Face;g.Tag="cover "+b.id;
                    g.Text(top.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" M",new V3(x0-.006f,top-.2f,b.pos.z),new V3(0,0,-1),Up,new V3(-1,0,0),.045f,.045f,NativeTrainingLook.Panel);
                    g.Box(x0-.012f,top-.04f,z0,x0,top,z1,NativeTrainingLook.Warn,0,0,1|4|8|16|32);
                    g.At=NativeTrainingMesh.Place.Floor;
                }
                else if(b.hp>0&&(b.role=="cover"||b.role=="masonry"||b.role=="structural"))
                {
                    int code=b.role=="cover"?NativeTrainingLook.Wood:b.role=="masonry"?NativeTrainingLook.Masonry:NativeTrainingLook.Structural;string kind=b.role=="cover"?"WOOD":b.role=="masonry"?"MASONRY":"STRUCTURAL";
                    float bx0=x0-.35f,bx1=b.pos.x+b.size.x/2+.35f,bz0=z0-.35f,bz1=z1+.35f;g.Tag="sample "+b.id;
                    foreach(var (a0,c0,a1,c1) in new[]{(bx0,bz0,bx1,bz0+.1f),(bx0,bz1-.1f,bx1,bz1),(bx0,bz0,bx0+.1f,bz1),(bx1-.1f,bz0,bx1,bz1)})g.Floor(a0,c0,a1,c1,NativeTrainingLook.LineY,code,code,.3f);
                    g.FloorText(kind,bx0-.35f,b.pos.z,NativeTrainingLook.TextY,0,-1,1,0,.05f,.07f,code,code,.25f);
                    g.FloorText(((int)Math.Round(b.hp)).ToString(System.Globalization.CultureInfo.InvariantCulture)+" HP",bx0-.95f,b.pos.z,NativeTrainingLook.TextY,0,-1,1,0,.045f,.06f,ink);
                }
            }
            // the window's sill and head and the door's head: a tick at each height on the pier beside the opening, labelled
            var sill=Block(map,"training-wall-lab-sill");var head=Block(map,"training-wall-lab-head");var lintel=Block(map,"training-wall-lab-lintel");
            ContentBlock Pier(float z,bool after){foreach(var b in map.blocks??Array.Empty<ContentBlock>())if(b?.id!=null&&b.id.StartsWith("training-wall-lab-",StringComparison.Ordinal)&&b!=sill&&b!=head&&b!=lintel&&Math.Abs((after?b.pos.z-b.size.z/2:b.pos.z+b.size.z/2)-z)<.05f)return b;return null;}
            void Tick(ContentBlock pier,float level,float edge,int side,string text)
            {
                if(pier==null)return;float x=pier.pos.x-pier.size.x/2-.006f;var n=new V3(-1,0,0);float z0=edge+side*.03f,z1=edge+side*1.1f;
                g.Quad(new V3(x,level-.012f,Math.Min(z0,z1)),new V3(x,level-.012f,Math.Max(z0,z1)),new V3(x,level+.012f,Math.Max(z0,z1)),new V3(x,level+.012f,Math.Min(z0,z1)),n,NativeTrainingLook.Warn);
                g.Text(text,new V3(x-.002f,level+.17f,edge+side*.06f),new V3(0,0,-1),Up,n,.035f,.035f,NativeTrainingLook.Panel,0,0,side>0?1:-1);
            }
            g.At=NativeTrainingMesh.Place.Face;g.Tag="lab wall";
            if(sill!=null){float zEdge=sill.pos.z+sill.size.z/2;var pier=Pier(zEdge,true);Tick(pier,Top(sill),zEdge,1,"SILL "+Top(sill).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" M");
                if(head!=null)Tick(pier,head.pos.y-head.size.y/2,zEdge,1,"HEAD "+(head.pos.y-head.size.y/2).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" M");}
            if(lintel!=null){float zEdge=lintel.pos.z-lintel.size.z/2;Tick(Pier(zEdge,false),lintel.pos.y-lintel.size.y/2,zEdge,-1,"DOOR "+(lintel.pos.y-lintel.size.y/2).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" M");}
            g.At=NativeTrainingMesh.Place.Floor;
            Label("COVER LAB",18.8f,-7.6f,.07f);
            // the hazard bay: its sign beside the door on the bay wall, each sample's name on the floor beside it
            var bayW=Block(map,"training-wall-bay-w-1");
            if(bayW!=null)
            {
                float wx=bayW.pos.x-bayW.size.x/2-.006f,wz=bayW.pos.z;g.At=NativeTrainingMesh.Place.Face;g.Tag="bay sign";
                g.Box(wx-.004f,1.55f,wz-3.3f,wx,2.55f,wz+3.3f,NativeTrainingLook.Panel,0,0,1|4|8|16|32);
                g.Text("HAZARD BAY",new V3(wx-.01f,2.05f,wz),new V3(0,0,-1),Up,new V3(-1,0,0),.075f,.075f,NativeTrainingLook.Warn,NativeTrainingLook.Warn,.8f);
                for(int k=0;k<12;k++){float s0=wz-3.3f+k*.55f;g.Quad(new V3(wx-.008f,1.55f,s0),new V3(wx-.008f,1.55f,s0+.28f),new V3(wx-.008f,1.7f,s0+.13f),new V3(wx-.008f,1.7f,s0-.15f),new V3(-1,0,0),NativeTrainingLook.Warn);}
                g.At=NativeTrainingMesh.Place.Floor;
            }
            foreach(var h in map.hazards??Array.Empty<MapHazard>())
            {
                if(h==null)continue;g.Tag="hazard "+h.id;string name=h.kind=="saw-slot"?"SAW":h.kind=="vent"?"VENT":h.kind=="press"?"PRESS":h.kind=="grid"?"GRID":h.kind=="crane"?"CRANE":null;if(name==null)continue;
                float x,z;
                switch(h.kind)
                {
                    case "saw-slot":x=Math.Min(h.a.x,h.b.x)-h.radius-2.2f;z=h.a.z;break;
                    case "vent":x=h.a.x-h.size.x/2-.75f;z=h.a.z;break;
                    case "press":x=h.a.x-h.size.x/2-1.25f;z=h.a.z;break;
                    case "grid":x=h.center.x-h.size.x/2-.85f;z=h.center.z;break;
                    default:{var zn=h.zones!=null&&h.zones.Length>0?h.zones[0].center:h.a;x=zn.x-h.size.x/2-1.3f;z=zn.z;break;}
                }
                g.FloorText(name,x,z,NativeTrainingLook.TextY,0,-1,1,0,.06f,.08f,NativeTrainingLook.Warn,NativeTrainingLook.Warn,.35f);
            }
        }

        // ---------------- the hall walls ----------------
        private static void Walls(NativeTrainingMesh g,NativeTrainingPlan p)
        {
            g.At=NativeTrainingMesh.Place.Wall;const float W=1.2f,o=.008f;
            // per wall: a point on its inner face, its inward normal, its run (along u) from a to b
            var walls=new (float x,float z,float nx,float nz,float ux,float uz,float a,float b,string name)[]{
                (0,p.MaxZ,0,-1,-1,0,p.MinX,p.MaxX,"north"),(0,p.MinZ,0,1,1,0,p.MinX,p.MaxX,"south"),(p.MinX,0,1,0,0,-1,p.MinZ,p.MaxZ,"west"),(p.MaxX,0,-1,0,0,1,p.MinZ,p.MaxZ,"east")};
            foreach(var w in walls)
            {
                g.Tag="wall "+w.name;bool alongX=w.nz!=0;
                V3 P(float s,float y,float off)=>alongX?new V3(s,y,w.z+w.nz*off):new V3(w.x+w.nx*off,y,s);
                var nrm=new V3(w.nx,0,w.nz);
                g.Quad(P(w.a,0,o),P(w.b,0,o),P(w.b,W,o),P(w.a,W,o),nrm,NativeTrainingLook.Wainscot);
                g.Quad(P(w.a,W,o*2),P(w.b,W,o*2),P(w.b,W+.06f,o*2),P(w.a,W+.06f,o*2),nrm,NativeTrainingLook.Cyan,NativeTrainingLook.Cyan,.5f);
                for(float s=(float)Math.Ceiling(w.a/4)*4;s<w.b-.1f;s+=4)if(s>w.a+.1f)g.Quad(P(s-.02f,W+.06f,o),P(s+.02f,W+.06f,o),P(s+.02f,p.Roof-.4f,o),P(s-.02f,p.Roof-.4f,o),nrm,NativeTrainingLook.Seam);
            }
            // height rulers: the south wall behind the gap run and the west wall beside the tower (lines every 0.5 m, labels each metre)
            Ruler(g,"south",p.MinX+.4f,-8f,p.MinZ,true,8);Ruler(g,"west",-13f,-1f,p.MinX,false,10);
            // the hall's name over the spawn side, the range's over the lanes
            g.Tag="wall title";g.Text("CALIBRATION BAY",new V3(0,9.2f,p.MinZ+.02f),new V3(-1,0,0),Up,new V3(0,0,1),.16f,.16f,NativeTrainingLook.Panel);
            g.Text("FIRING RANGE  "+NativeTrainingPlan.Metres(p.Distances[0],false)+" - "+NativeTrainingPlan.Metres(p.Distances[p.Distances.Length-1]),new V3((p.FiringX+p.RangeEndX)/2,12.3f,p.MaxZ-.02f),new V3(1,0,0),Up,new V3(0,0,-1),.11f,.11f,NativeTrainingLook.Panel);
            g.Text("HAZARD BAY",new V3(p.MaxX-.02f,7.4f,-19f),new V3(0,0,-1),Up,new V3(-1,0,0),.13f,.13f,NativeTrainingLook.Panel);
        }
        // A wall ruler from s0 to s1 along the wall (alongX: the south or north wall at z = at; else the west or east wall at x = at).
        private static void Ruler(NativeTrainingMesh g,string name,float s0,float s1,float at,bool alongX,int metres)
        {
            g.Tag="ruler "+name;const float o=.014f;float sign=at<0?1:-1;var nrm=alongX?new V3(0,0,sign):new V3(sign,0,0);
            var u=alongX?new V3(-sign,0,0):new V3(0,0,sign);
            V3 P(float s,float y,float off)=>alongX?new V3(s,y,at+sign*off):new V3(at+sign*off,y,s);
            for(int k=1;k<=metres*2;k++)
            {
                float y=k*.5f;bool whole=k%2==0;float hh=whole?.018f:.01f,e=whole?s1:s0+(s1-s0)*.25f;
                g.Quad(P(s0,y-hh,o),P(e,y-hh,o),P(e,y+hh,o),P(s0,y+hh,o),nrm,whole?NativeTrainingLook.RulerLine:NativeTrainingLook.Seam);
                if(whole)for(float s=s0+1f;s<s1-.5f;s+=8)g.Text((k/2).ToString(System.Globalization.CultureInfo.InvariantCulture)+" M",P(s,y+.17f,o*1.5f),u,Up,nrm,.035f,.035f,NativeTrainingLook.Panel);
            }
        }

        // ---------------- the readout boards ----------------
        private static void Boards(NativeTrainingMarks m,NativeTrainingMesh f,NativeTrainingPlan p)
        {
            var cells=new List<NativeTrainingCell>();f.At=NativeTrainingMesh.Place.Roof;
            float h=p.LaneHalf,cw=2*h+.1f,ch=cw/NativeTrainingLook.CellAspect,zA=p.LaneZ[0]-h-.25f,zB=p.LaneZ[p.LaneZ.Length-1]+h+.25f;
            foreach(float d in p.Distances)
            {
                float x=p.FiringX+d,y0=NativeTrainingLook.BoardBottom,y1=y0+ch+.12f+NativeTrainingLook.HeaderHeight;f.Tag="board "+NativeTrainingPlan.Metres(d);
                Housing(f,x,(zA+zB)/2,y0,y1,zB-zA,true,p.Roof);
                float face=x-NativeTrainingLook.BoardDepth/2-.012f;
                f.Text(NativeTrainingPlan.Metres(d),new V3(face,y1-NativeTrainingLook.HeaderHeight/2-.04f,(zA+zB)/2),new V3(0,0,-1),new V3(0,1,0),new V3(-1,0,0),.1f,.1f,NativeTrainingLook.Ink,NativeTrainingLook.Ink,1.4f);
                f.Text("DISTANCE",new V3(face,y1-NativeTrainingLook.HeaderHeight/2-.04f,zB-.35f),new V3(0,0,-1),new V3(0,1,0),new V3(-1,0,0),.045f,.045f,NativeTrainingLook.Muted,NativeTrainingLook.Muted,.6f,-1);
                f.Text("READOUT",new V3(face,y1-NativeTrainingLook.HeaderHeight/2-.04f,zA+.35f),new V3(0,0,-1),new V3(0,1,0),new V3(-1,0,0),.045f,.045f,NativeTrainingLook.Muted,NativeTrainingLook.Muted,.6f,1);
                for(int l=0;l<p.LaneZ.Length;l++)
                {
                    int pad=p.PadAt(l,d);if(pad<0)continue;
                    cells.Add(new NativeTrainingCell{Pad=pad,Center=new V3(face-.004f,y0+.06f+ch/2,p.LaneZ[l]),Normal=new V3(-1,0,0),Right=new V3(0,0,-1),Width=cw-.12f,Height=ch-.08f});
                }
            }
            // the ring's board, north of it over the range barrier, facing the ring (west to east: north pad first)
            var melee=new List<NativeTrainingPadInfo>();foreach(var i in p.Pads)if(i.Melee)melee.Add(i);
            if(melee.Count>0)
            {
                melee.Sort((a,b)=>a.Pad.pos.x!=b.Pad.pos.x?a.Pad.pos.x.CompareTo(b.Pad.pos.x):b.Pad.pos.z.CompareTo(a.Pad.pos.z));
                float zb=p.StripZ0-.5f,width=melee.Count*cw+.25f,x0=p.RingX-width/2,y0=NativeTrainingLook.MeleeBottom,y1=y0+ch+.12f+NativeTrainingLook.HeaderHeight;f.Tag="board melee";
                Housing(f,p.RingX,zb,y0,y1,width,false,p.Roof);float face=zb-NativeTrainingLook.BoardDepth/2-.012f;
                f.Text("MELEE RING",new V3(p.RingX,y1-NativeTrainingLook.HeaderHeight/2-.04f,face),new V3(1,0,0),new V3(0,1,0),new V3(0,0,-1),.1f,.1f,NativeTrainingLook.Ink,NativeTrainingLook.Ink,1.4f);
                for(int k=0;k<melee.Count;k++)cells.Add(new NativeTrainingCell{Pad=melee[k].Index,Center=new V3(x0+.125f+cw*(k+.5f),y0+.06f+ch/2,face-.004f),Normal=new V3(0,0,-1),Right=new V3(1,0,0),Width=cw-.12f,Height=ch-.08f});
            }
            m.Cells=cells.ToArray();
        }
        // A board's housing: the dark case (its face toward -x for a range board, -z for the ring's), a lit keyline round the
        // face, a cyan sill under the cells, and two hangers to the roof.
        private static void Housing(NativeTrainingMesh f,float cx,float cz,float y0,float y1,float span,bool range,float roof)
        {
            float d=NativeTrainingLook.BoardDepth/2,mg=NativeTrainingLook.FrameMargin;
            if(range)
            {
                float z0=cz-span/2-mg,z1=cz+span/2+mg;f.Box(cx-d,y0-mg,z0,cx+d,y1+mg*.6f,z1,NativeTrainingLook.Housing);
                float fx=cx-d-.006f;f.Box(fx-.006f,y0-mg+.02f,z0+.02f,fx,y0-mg+.09f,z1-.02f,NativeTrainingLook.Cyan,NativeTrainingLook.Cyan,1.1f);
                f.Box(fx-.004f,y1-NativeTrainingLook.HeaderHeight-.02f,z0+.1f,fx,y1-NativeTrainingLook.HeaderHeight+.0f,z1-.1f,NativeTrainingLook.Edge,NativeTrainingLook.CyanDeep,.6f);
                foreach(float hz in new[]{z0+1.2f,z1-1.2f})f.Box(cx-.03f,y1+mg*.6f,hz-.03f,cx+.03f,roof,hz+.03f,NativeTrainingLook.Rod);
            }
            else
            {
                float x0=cx-span/2-mg,x1=cx+span/2+mg;f.Box(x0,y0-mg,cz-d,x1,y1+mg*.6f,cz+d,NativeTrainingLook.Housing);
                float fz=cz-d-.006f;f.Box(x0+.02f,y0-mg+.02f,fz-.006f,x1-.02f,y0-mg+.09f,fz,NativeTrainingLook.Melee,NativeTrainingLook.Melee,1.1f);
                f.Box(x0+.1f,y1-NativeTrainingLook.HeaderHeight-.02f,fz-.004f,x1-.1f,y1-NativeTrainingLook.HeaderHeight,fz,NativeTrainingLook.Edge,NativeTrainingLook.CyanDeep,.6f);
                foreach(float hx in new[]{x0+1.2f,x1-1.2f})f.Box(hx-.03f,y1+mg*.6f,cz-.03f,hx+.03f,roof,cz+.03f,NativeTrainingLook.Rod);
            }
        }
        // The roof's light panels: rows of soft white troffers just under the roof (the studio's even top light).
        private static void Roof(NativeTrainingMesh f,NativeTrainingPlan p)
        {
            f.At=NativeTrainingMesh.Place.Roof;f.Tag="roof lights";float y=p.Roof;
            for(float x=p.MinX+6;x<=p.MaxX-5.9f;x+=7.5f)for(float z=p.MinZ+4;z<=p.MaxZ-3.9f;z+=6.5f)
            {
                f.Box(x-1.5f,y-.1f,z-.36f,x+1.5f,y-.02f,z+.36f,NativeTrainingLook.Housing,0,0,4);
                f.Box(x-1.4f,y-.11f,z-.28f,x+1.4f,y-.1f,z+.28f,NativeTrainingLook.Lamp,NativeTrainingLook.Lamp,2.2f,4);
            }
        }
    }
}
