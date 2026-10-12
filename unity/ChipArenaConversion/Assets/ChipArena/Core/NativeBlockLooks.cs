using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Arena blocks drawn through Resources/NativeBlockLit.shader by NativeArenaDressing (ARENA_PROPOSAL.md 2.2 "Block geometry"
    /// and "Surfaces", 2.3, 2.4). The rules the runtime, the shader and the checks share: the look keys (their index is the
    /// shader's LOOK_* id), look selection from MapLooks.json, damage stages, per-block seeds and mesh variants, the bevelled
    /// stage meshes and the vertex lattice the shader expands them with, the draw budget, the blast-flash fall-off, the band
    /// lamp (<see cref="Lamp"/>) and the round's one dust coat (<see cref="NativeDustCoat"/>). Pure; Build allocates once per
    /// stage mesh, nothing else allocates.
    /// <list type="bullet">
    /// <item>Selection (<see cref="Resolve"/>): the map's blockLooks, then MapLooks.json's dev `preview` list, then the material's
    /// default look. Within a list an exact block id wins, then the first glob in list order that matches (`*` matches any
    /// run of characters, so specific entries such as "hotel-pier-*" or "*-sill-*" go before "hotel-*"), then a
    /// "material:brick" entry. A listed look takes the entry's
    /// `color`, else the look's palette colour (3.10); a material default keeps the block's own content colour, so a dev
    /// preview on an old map keeps today's colours under the new surfaces. `band` paints the 2.3 load-bearing chevrons and
    /// the DANGER tag, fitted to the block's height (<see cref="BandFit"/>), and the band lamp at the band's middle; the dust
    /// coat mostly spares them (<see cref="BandCoatKeep"/>) and a stub (S3) or a loose fragment shows none of them.</item>
    /// <item>Stages (3.9, 5.3): health over the block's stage scale (<see cref="StageMax"/>: 5.2's per-block `hp` and
    /// `stages`, falling back to material durability and 66/33%). S0 above the first
    /// threshold, S1 down to the second, S2 above 0, S3 at 0 (a 24% stub; only a prop the sim keeps at 0 health shows it: no
    /// prop does today, a broken one leaves Props). Indestructible blocks stay S0.</item>
    /// <item>Meshes: one unit mesh per stage whose vertices carry a lattice (A, B) per axis: the shader places each at
    /// A·h + sign(B)·min(|B|·c, .42·|A|·h) for the instance's half extents h and chamfer c, so one mesh serves every block
    /// size with a fixed 5 cm chamfer (thin blocks clamp it), and the corner darkening ends 30 cm (B = 6) in from each
    /// corner. Vertex colour bakes the bevel highlight (+18%) and the corner darkening (-22%) times <see cref="ShadeStore"/>,
    /// alpha marks fresh breaks. S1 chips three corners, S2 six corners deeper plus, on blocks 1.8 m tall or more, an 8 cm
    /// waist over the middle third (mesh 4 is S2 without it), S3 is the
    /// S0 lattice with a ragged, fresh top drawn at 24% of the block's height.</item>
    /// </list>
    /// </summary>
    public static class NativeBlockLooks
    {
        /// <summary>Look keys; the index is the id NativeBlockLit.shader reads (its LOOK_* defines).</summary>
        public static readonly string[] Keys={"plain","brick-red","brick-buff","terracotta","concrete-board","steel-green","timber","tar","asphalt","cobble","window-lit","neon","corrugated","demolition","factory-grating"};
        /// <summary>Each look's base colour (sRGB) when its entry names none: ARENA_PROPOSAL.md 3.10. Plain has none (-1: the block's own colour).</summary>
        public static readonly int[] Palette={-1,0x9C4A30,0xB8774E,0xE3C9A0,0x8F8A80,0x3E6B5A,0xB4834F,0x2B2A2E,0x3A3836,0x6B625A,0x6E8F96,0x4FA8FF,0x7D8478,0xF4B321,0x8C9296};
        public const int Plain=0,BrickRed=1,BrickBuff=2,Terracotta=3,ConcreteBoard=4,SteelGreen=5,Timber=6,Tar=7,Asphalt=8,Cobble=9,WindowLit=10,Neon=11,Corrugated=12,Demolition=13,FactoryGrating=14;
        /// <summary>Per-instance flags (bits of the shader's flags value): the 2.3 chevron band and the DANGER tag (each drawn only
        /// with its own bit, so a look never implies the tag), a fragment (fresh break tint), the band lamp lit amber or red this
        /// frame (<see cref="Lamp"/>; neither: the lamp is dark), and the mesh's 180-degree turns. The window light of the 3.9
        /// telegraph is not a flag: NativeArenaDressing sends it per instance (_BlockColor.w, NativeStructureFx.TelegraphAt).</summary>
        public const int Band=1,Tag=2,Fragment=4,LampAmber=8,LampRed=16,SpinY=32,SpinZ=64;
        /// <summary>The chamfer (m, 4-6 cm in 2.2), its clamp share of a half extent, the corner darkening's reach in chamfers (30 cm).</summary>
        public const float Chamfer=.05f,ClampShare=.42f,Inner=6;
        /// <summary>Bevel highlight, corner darkening and the vertex colour scale they are stored at (the shader multiplies by 1/ShadeStore).</summary>
        public const float EdgeHighlight=1.18f,CornerDark=.78f,ShadeStore=.8f;
        /// <summary>Stage thresholds on health / max (3.9), the S2 waist (in chamfers: 8 cm in) and the S3 stub's share of the height.</summary>
        public const float Stage1=.66f,Stage2=.33f,Waist=1.6f,StubShare=.24f;
        /// <summary>The 2.3 band (m above the block's base) on blocks at least BandFullMin tall; on shorter ones a band of at most
        /// BandMax (BandShare of the flat face) centred on the block. The DANGER tag plate: half height, gap to the band, the
        /// clearance it keeps from a bevel, and the half width a face needs for it (the plate is .28 m each side).</summary>
        public const float BandLow=1,BandHigh=1.6f,BandFullMin=1.9f,BandMax=.6f,BandShare=.8f,TagHalf=.12f,TagGap=.08f,TagClear=.02f,TagMinHalfWidth=.3f;
        /// <summary>The share of the dust coat the band, the tag and the lamp keep (2.1 rule 4: telegraphs are never reduced; the
        /// rest of the coat greys the face around them).</summary>
        public const float BandCoatKeep=.1f;
        /// <summary>The 2.3 band lamp NativeBlockLit draws at the middle of the band on each side face (m): the lens and its dark
        /// housing ring, and its glow over the lens colour (amber #FFB000, danger red #D6332A). How long the members stay locked
        /// after FightAt (3.9 fairness rule 1), which the amber lamp shows.</summary>
        public const float LampLens=.055f,LampHousing=.075f,LampGlow=1.5f,LockSeconds=4;
        public const int LampAmberColor=0xFFB000,LampRedColor=0xD6332A;
        /// <summary>Instances per draw (one D3D11 constant buffer holds 500 of the shader's per-instance block) and the arena's draw budget (2.4).</summary>
        public const int Chunk=500,DrawBudget=60,Stages=4;

        /// <summary>A key's look id: its index in Keys, else a Cyber City bank key's NativeCyberCityLooks id (Offset + slot), else -1.</summary>
        public static int Id(string key){if(key==null)return -1;for(int i=0;i<Keys.Length;i++)if(Keys[i]==key)return i;return NativeCyberCityLooks.Id(key);}
        /// <summary>The look a material gets when no entry names its block (the 2.3 colour code: grey concrete and buff stone never fall).</summary>
        public static int MaterialDefault(string material)
        {
            switch(material)
            {
                case "brick":return BrickRed;case "stone":case "concrete":return ConcreteBoard;case "metal":case "steel":return SteelGreen;case "wood":case "timber":return Timber;
                case "foundation":case "asphalt":return Asphalt;case "glass":return WindowLit;case "tar":return Tar;case "cobble":return Cobble;case "terracotta":return Terracotta;default:return Plain;
            }
        }
        /// <summary>A block's look id, base colour (sRGB) and flags; Listed when an entry chose it.</summary>
        public struct Choice {public int Look,Color,Flags;public bool Listed;}
        public static Choice Resolve(MapLookBlock[] entries,MapLookBlock[] preview,string id,string material,int materialColor)
        {
            var e=Best(entries,id,material)??Best(preview,id,material);
            if(e!=null){int look=Math.Max(0,Id(e.look));if(NativeCyberCityLooks.Is(look))return CyberChoice(NativeCyberCityLooks.Slot(look),e,materialColor);return new Choice{Look=look,Color=e.color!=0?e.color:Palette[look]>=0?Palette[look]:materialColor,Flags=e.band?Band|Tag:0,Listed=true};}
            return new Choice{Look=MaterialDefault(material),Color=materialColor};
        }
        // A Cyber City bank look (Core NativeCyberCityLooks): the slot as the look id plus the bank flag, the slot's palette colour.
        private static Choice CyberChoice(int slot,MapLookBlock e,int materialColor)
        {int p=NativeCyberCityLooks.Palette[slot];return new Choice{Look=slot,Color=e.color!=0?e.color:p>=0?p:materialColor,Flags=(e.band?Band|Tag:0)|NativeCyberCityLooks.Bank,Listed=true};}
        private static MapLookBlock Best(MapLookBlock[] entries,string id,string material)
        {
            if(entries==null)return null;MapLookBlock best=null;int score=-1;
            foreach(var e in entries){if(e==null||string.IsNullOrEmpty(e.block))continue;int s=Score(e.block,id??"",material??"");if(s>score){score=s;best=e;}}
            return best;
        }
        /// <summary>An entry's match score (the first entry with the highest wins): -1 none, 0 its material, 1 a glob, int.MaxValue the exact id.</summary>
        public static int Score(string pattern,string id,string material)
        {
            if(pattern.StartsWith("material:",StringComparison.Ordinal))return pattern.Length-9==material.Length&&string.CompareOrdinal(pattern,9,material,0,material.Length)==0?0:-1;
            if(pattern.IndexOf('*')<0)return pattern==id?int.MaxValue:-1;
            return Glob(pattern,id)?1:-1;
        }
        /// <summary>`*` matches any run of characters (none included); everything else matches itself.</summary>
        public static bool Glob(string pattern,string text)
        {
            int p=0,t=0,star=-1,mark=0;
            while(t<text.Length)
            {
                if(p<pattern.Length&&pattern[p]!='*'&&pattern[p]==text[t]){p++;t++;}
                else if(p<pattern.Length&&pattern[p]=='*'){star=p++;mark=t;}
                else if(star>=0){p=star+1;t=++mark;}
                else return false;
            }
            while(p<pattern.Length&&pattern[p]=='*')p++;return p==pattern.Length;
        }
        /// <summary>Whether NativeArenaDressing draws the map's blocks: its look opts in (`blocks.dressed`), or the dev preview is on.</summary>
        public static bool Dressed(MapLook look,bool preview)=>preview||look?.blocks!=null&&look.blocks.dressed;
        /// <summary>Health as a share of the block's stage scale (1 for indestructible blocks and a zero scale).</summary>
        public static float Fraction(float health,float max,bool indestructible)=>indestructible||!(max>0)?1:Math.Max(0,Math.Min(1,health/max));
        public static int Stage(float fraction)=>Stage(fraction,Stage1,Stage2);
        public static int Stage(float fraction,float s1,float s2)=>fraction>s1?0:fraction>s2?1:fraction>0?2:3;
        /// <summary>
        /// A block's stage scale and thresholds (3.9, 5.3: stage = replicated health / content `hp`, with the block's own
        /// `stages`), the one place NativeArenaDressing reads them. Legacy blocks fall back to material durability and 66/33%.
        /// </summary>
        public static float StageMax(ContentBlock block,ContentMaterial material,out float s1,out float s2){Thresholds(block?.stages,out s1,out s2);return StageMax(block?.hp??0,material!=null?material.durability:0);}
        /// <summary>The block's `hp` when it has one (above 0), else the material's durability.</summary>
        public static float StageMax(float hp,float durability)=>hp>0?hp:durability;
        /// <summary>A block's `stages` (two descending shares strictly inside 0..1), else 3.9's 66/33%.</summary>
        public static void Thresholds(float[] stages,out float s1,out float s2)
        {if(stages!=null&&stages.Length==2&&stages[0]<1&&stages[0]>stages[1]&&stages[1]>0){s1=stages[0];s2=stages[1];}else{s1=Stage1;s2=Stage2;}}
        /// <summary>The health the shader reads (its cracks start at 66% and its spalls and rebar at 33%): the fraction mapped
        /// piecewise linearly so the block's own thresholds land on 66/33% (the identity for the default thresholds).</summary>
        public static float Normalised(float fraction,float s1,float s2)=>fraction>s1?Stage1+(fraction-s1)/(1-s1)*(1-Stage1):fraction>s2?Stage2+(fraction-s2)/(s1-s2)*(Stage1-Stage2):fraction>0?fraction/s2*Stage2:0;
        /// <summary>
        /// The 2.3 band and DANGER tag on a side face of a block `height` m tall (NativeBlockLit's BkBand; m above the block's
        /// base). The flat face runs from the bevel (the chamfer, clamped on thin blocks as <see cref="Corner"/> clamps it) to
        /// height minus the bevel. The band is 1.0-1.6 m on blocks at least 1.9 m tall, else at most .6 m (80% of the flat face)
        /// centred on the block. The .24 m tag plate sits 8 cm above the band where it clears the top bevel by 2 cm, else 8 cm
        /// below it where it clears the bottom bevel, else nowhere (`tag` -1); the shader also needs a face more than .3 m
        /// either side of its middle for it.
        /// </summary>
        public static void BandFit(float height,out float low,out float high,out float tag)
        {
            float c=Math.Min(Chamfer,ClampShare*height*.5f);
            if(height>=BandFullMin){low=BandLow;high=BandHigh;}else{float half=.5f*Math.Min(BandMax,BandShare*(height-2*c));low=height*.5f-half;high=height*.5f+half;}
            float above=high+TagGap+TagHalf,below=low-TagGap-TagHalf;
            tag=above+TagHalf<=height-c-TagClear?above:below-TagHalf>=c+TagClear?below:-1;
        }
        /// <summary>
        /// The band lamp's flag for a banded member this frame (2.3: "locked (the first 4 s of each fight); it goes dark once armed
        /// and strobes red at the last damage stage"): red while <paramref name="redOn"/> (NativeStructureFx.LampStrobe's 4 Hz on
        /// the presentation clock, so host, guests and the killcam blink together) at S2 or while the member's segment telegraph
        /// holds (3.9; never reduced), else amber while <paramref name="locked"/>, else dark. A stub (S3) has no band and no lamp.
        /// </summary>
        public static int Lamp(int stage,bool stressed,bool locked,bool redOn)=>stage==3?0:stage==2||stressed?(redOn?LampRed:0):locked?LampAmber:0;
        /// <summary>
        /// Whether the members are locked (the amber lamp): before the fight (countdown, draft, intro) and for its first
        /// <see cref="LockSeconds"/> after FightAt, using the same arming rule as structural damage.
        /// </summary>
        public static bool Locked(NativePhase phase,double fightAt,double now)=>phase==NativePhase.Countdown||phase==NativePhase.Draft||phase==NativePhase.Intro||phase==NativePhase.Fight&&!NativeStructureRules.Armed(phase,fightAt,now);
        /// <summary>FNV-1a of the block id: the same on the host, guests and the killcam, since every frame carries the id.</summary>
        public static uint Hash(string id){uint h=2166136261;if(id!=null)foreach(char c in id){h^=c;h*=16777619;}return h;}
        public static float Seed(string id)=>(Hash(id)&0xFFFFFF)/16777216f;
        /// <summary>The 180-degree mesh turns (SpinY, SpinZ) from the id, so chipped corners differ block to block at no extra draw; a stub never turns over.</summary>
        public static int Spin(string id,int stage)=>Spin(Hash(id),stage);
        public static int Spin(uint hash,int stage){uint h=hash>>24;return ((h&1)!=0?SpinY:0)|((h&2)!=0&&stage!=3?SpinZ:0);}
        /// <summary>Instanced draws for these instance counts per mesh (main pass; each shadow cascade and the depth pass repeat them).</summary>
        public static int Draws(params int[] perMesh){int n=0;foreach(int c in perMesh)n+=c<=0?0:(c+Chunk-1)/Chunk;return n;}
        /// <summary>The shader's lattice for one axis: A·h + sign(B)·min(|B|·c, .42·|A|·h).</summary>
        public static float Corner(float a,float b,float h,float c)=>a*h+Math.Sign(b)*Math.Min(Math.Abs(b)*c,ClampShare*Math.Abs(a)*h);
        /// <summary>
        /// The blast-flash feed's fall-off: Unity's built-in point light curve 1/(1 + 25q²), q = d/range, cut to 0 over the last
        /// 10% of the range (min(1, 5(1-q²))), so a flash lights dressed walls about as its point light lights everything else.
        /// </summary>
        public static double FlashFalloff(double distance,double range){if(!(range>0))return 0;double q=distance/range;if(q>=1)return 0;q*=q;return Math.Min(1,5*(1-q))/(1+25*q);}

        /// <summary>A stage mesh: per vertex the unit preview position and outward normal (flat per face, bevel and corner, averaged
        /// over the S2 waist; for bounds, the shadow normal offset and the shader's orientation hint), lattice A and inset B
        /// (xyz each, w 0), and shade (rgb x ShadeStore, a fresh break); triangles wind outward (Unity: clockwise).</summary>
        public sealed class StageMesh {public float[] Preview,Normal,Lattice,Inset,Shade;public int[] Triangles;public int Vertices=>Preview.Length/3;}
        // An axis coordinate code: -1 the minus face plane, Lines the plus face plane, else a lattice line index.
        private struct Line {public float A,B;public bool Outer;}
        private static Line[] Lines(bool mid)
        {
            var o=new List<Line>{new Line{A=-1,B=1,Outer=true},new Line{A=-1,B=Inner}};
            if(mid){o.Add(new Line{A=-1/3f,B=0});o.Add(new Line{A=1/3f,B=0});}
            o.Add(new Line{A=1,B=-Inner});o.Add(new Line{A=1,B=-1,Outer=true});return o.ToArray();
        }
        // Chipped corners (bit 0 +x, bit 1 +y, bit 2 +z) and their insets per axis in chamfers.
        private static readonly int[] ChipsS1={7,2,1},ChipsS2={7,2,1,4,6,3};
        private static readonly float[][] DepthS1={new[]{3.5f,2.6f,3f},new[]{2.4f,3.4f,2f},new[]{3f,2f,3.6f}};
        private static readonly float[][] DepthS2={new[]{5.2f,4f,4.6f},new[]{4f,5.4f,3.6f},new[]{4.6f,3.2f,5.4f},new[]{3.8f,4.4f,3.2f},new[]{5f,3.6f,4.2f},new[]{3.4f,4.8f,4f}};
        /// <summary>The S3 stub's ragged top: metres below the cut per lattice cell (x line, z line), so coincident vertices agree.</summary>
        public static readonly float[] StubTop={-.10f,-.04f,-.08f,-.02f,-.03f,0f,-.06f,-.09f,-.07f,-.02f,-.01f,-.05f,-.01f,-.08f,-.04f,-.11f};
        /// <summary>The mesh for a stage: S0-S3, or for S2 on a block under 1.8 m tall the unwaisted S2 (mesh 4; a waist on a squat
        /// block would fold its bevels).</summary>
        public static int MeshFor(int stage,float halfHeight)=>stage==2&&halfHeight<WaistMinHalf?4:stage;
        public const int Meshes=5;public const float WaistMinHalf=.9f;
        public static StageMesh Build(int mesh)
        {
            if(mesh<0||mesh>=Meshes)throw new ArgumentOutOfRangeException(nameof(mesh));int stage=mesh==4?2:mesh;bool waisted=mesh==2;
            var axes=new[]{Lines(false),Lines(waisted),Lines(false)};var pos=new List<float>();var big=new List<float>();var nrm=new List<float>();var lat=new List<float>();var ins=new List<float>();var shade=new List<float>();var tris=new List<int>();
            int[] chips=stage==1?ChipsS1:stage==2?ChipsS2:Array.Empty<int>();float[][] depth=stage==1?DepthS1:DepthS2;
            // Vertices are shared within a facet group (a face, a bevel strip, a corner), never across groups (shades differ).
            int group=0;var cache=new Dictionary<long,int>();
            // A vertex from its three axis codes and facet kind (0 face, 1 bevel, 2 corner).
            int Vertex(int[] code,int kind)
            {
                long key=((long)group<<16)|(long)((code[0]+1)<<8|(code[1]+1)<<4|(code[2]+1));if(cache.TryGetValue(key,out int found))return found;cache[key]=pos.Count/3;
                float[] a=new float[3],b=new float[3];bool corner=true,chipped=false;int bits=0;
                for(int k=0;k<3;k++)
                {
                    var lines=axes[k];int c=code[k];
                    if(c<0){a[k]=-1;b[k]=0;}else if(c>=lines.Length){a[k]=1;b[k]=0;bits|=1<<k;}else{a[k]=lines[c].A;b[k]=lines[c].B;if(!lines[c].Outer)corner=false;if(lines[c].A>0)bits|=1<<k;}
                }
                if(corner)for(int i=0;i<chips.Length;i++)if(chips[i]==bits){chipped=true;for(int k=0;k<3;k++){int c=code[k];if(c>=0&&c<axes[k].Length&&axes[k][c].Outer)b[k]=-Math.Sign(a[k])*depth[i][k];}}
                bool waist=waisted&&code[1]>=0&&code[1]<axes[1].Length&&axes[1][code[1]].B==0;if(waist){b[0]-=Math.Sign(a[0])*Waist;b[2]-=Math.Sign(a[2])*Waist;}
                bool top=stage==3&&(code[1]>=axes[1].Length||code[1]==axes[1].Length-1);
                if(top){int ix=Math.Max(0,Math.Min(3,code[0])),iz=Math.Max(0,Math.Min(3,code[2]));b[1]+=StubTop[ix*4+iz]/Chamfer;}
                float s=(kind==1?EdgeHighlight:1)*(corner?CornerDark:1);float fresh=kind==2&&chipped||kind==1&&chipped?1:waist&&kind<2?.35f:0;
                if(stage==3&&top)fresh=kind==0&&code[1]<axes[1].Length?.7f:1;
                for(int k=0;k<3;k++){pos.Add(Corner(a[k],b[k],.5f,Chamfer));big.Add(Corner(a[k],b[k],2,Chamfer));lat.Add(a[k]);ins.Add(b[k]);}lat.Add(0);ins.Add(0);
                shade.Add(s*ShadeStore);shade.Add(s*ShadeStore);shade.Add(s*ShadeStore);shade.Add(fresh);nrm.Add(0);nrm.Add(0);nrm.Add(0);return pos.Count/3-1;
            }
            // Two triangles (or one) facing `outward`; its unit normal adds to its vertices' (normalised at the end: the flat
            // normal on a flat group, the mean on the S2 waist).
            void Facet(int[] v,float ox,float oy,float oz)
            {
                float nx=0,ny=0,nz=0;
                for(int i=1;i+1<v.Length;i++){Cross(v[0],v[i],v[i+1],out float cx,out float cy,out float cz);if(cx*ox+cy*oy+cz*oz<0){cx=-cx;cy=-cy;cz=-cz;}nx+=cx;ny+=cy;nz+=cz;}
                float l=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);if(l<1e-12f){nx=ox;ny=oy;nz=oz;l=(float)Math.Sqrt(ox*ox+oy*oy+oz*oz);}nx/=l;ny/=l;nz/=l;
                foreach(int i in v){nrm[i*3]+=nx;nrm[i*3+1]+=ny;nrm[i*3+2]+=nz;}
                for(int i=1;i+1<v.Length;i++){Cross(v[0],v[i],v[i+1],out float cx,out float cy,out float cz);if(cx*ox+cy*oy+cz*oz>=0){tris.Add(v[0]);tris.Add(v[i]);tris.Add(v[i+1]);}else{tris.Add(v[0]);tris.Add(v[i+1]);tris.Add(v[i]);}}
            }
            // Winding and normals come from a 4 m cube, where no chamfer, chip or inner line is clamped (the true shape).
            void Cross(int i0,int i1,int i2,out float cx,out float cy,out float cz)
            {
                float ax=big[i1*3]-big[i0*3],ay=big[i1*3+1]-big[i0*3+1],az=big[i1*3+2]-big[i0*3+2],bx=big[i2*3]-big[i0*3],by=big[i2*3+1]-big[i0*3+1],bz=big[i2*3+2]-big[i0*3+2];
                cx=ay*bz-az*by;cy=az*bx-ax*bz;cz=ax*by-ay*bx;
            }
            // Faces: the plane on its own axis, a grid of the other two axes' lines.
            for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
            {
                int u=(axis+1)%3,w=(axis+2)%3;int plane=sign<0?-1:axes[axis].Length;group++;
                for(int i=0;i+1<axes[u].Length;i++)for(int j=0;j+1<axes[w].Length;j++)
                {
                    // A face corner's quad splits through the corner, which a chip pushes inward (the quad would turn concave).
                    int[] q=new int[4];int n=0,first=0;
                    foreach(var (du,dw) in new[]{(0,0),(1,0),(1,1),(0,1)}){var code=new int[3];code[axis]=plane;code[u]=i+du;code[w]=j+dw;if((code[u]==0||code[u]==axes[u].Length-1)&&(code[w]==0||code[w]==axes[w].Length-1))first=n;q[n++]=Vertex(code,0);}
                    Facet(new[]{q[first],q[(first+1)%4],q[(first+2)%4],q[(first+3)%4]},axis==0?sign:0,axis==1?sign:0,axis==2?sign:0);
                }
            }
            // Bevels: along each edge between faces a (sign sa) and b (sign sb), a strip over the third axis's lines.
            for(int e=0;e<3;e++){int a=(e+1)%3,b=(e+2)%3;foreach(int sa in new[]{-1,1})foreach(int sb in new[]{-1,1})
            {
                int pa=sa<0?-1:axes[a].Length,oa=sa<0?0:axes[a].Length-1,pb=sb<0?-1:axes[b].Length,ob=sb<0?0:axes[b].Length-1;group++;
                for(int k=0;k+1<axes[e].Length;k++)
                {
                    int[] Code(int ca,int cb,int ce){var c=new int[3];c[a]=ca;c[b]=cb;c[e]=ce;return c;}
                    var q=new[]{Vertex(Code(pa,ob,k),1),Vertex(Code(oa,pb,k),1),Vertex(Code(oa,pb,k+1),1),Vertex(Code(pa,ob,k+1),1)};
                    Facet(q,a==0?sa:b==0?sb:0,a==1?sa:b==1?sb:0,a==2?sa:b==2?sb:0);
                }
            }}
            // Corners: one triangle joining the three faces' corner vertices.
            foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                int P(int k,int s)=>s<0?-1:axes[k].Length;int O(int k,int s)=>s<0?0:axes[k].Length-1;group++;
                var t=new[]{Vertex(new[]{P(0,sx),O(1,sy),O(2,sz)},2),Vertex(new[]{O(0,sx),P(1,sy),O(2,sz)},2),Vertex(new[]{O(0,sx),O(1,sy),P(2,sz)},2)};
                Facet(t,sx,sy,sz);
            }
            for(int i=0;i<nrm.Count;i+=3){float l=(float)Math.Sqrt(nrm[i]*nrm[i]+nrm[i+1]*nrm[i+1]+nrm[i+2]*nrm[i+2]);nrm[i]/=l;nrm[i+1]/=l;nrm[i+2]/=l;}
            return new StageMesh{Preview=pos.ToArray(),Normal=nrm.ToArray(),Lattice=lat.ToArray(),Inset=ins.ToArray(),Shade=shade.ToArray(),Triangles=tris.ToArray()};
        }
    }
    /// <summary>
    /// The arena's one dust coat for the round (2.2 "a dust-coat grid", 3.10 "a dust coat that greys nearby surfaces as the round
    /// goes on"): the only coat NativeBlockLit reads. Fed on the match clock; every entry is idempotent on (time, place), so an
    /// event fed again (the killcam's replay arena, an event buffer re-read after a rewind) never doubles:
    /// <list type="bullet">
    /// <item><see cref="Break"/>: every Break and Collapse event on a dressed map (NativeArenaDressing's intake from the live
    /// arena, also while the killcam draws): <see cref="PerBreak"/> at the event, falling off linearly to <see cref="Radius"/>,
    /// on a 2 m grid within ±48 m, each cell capped at <see cref="Cap"/>. A break's place is its prop's centre with its height,
    /// so stacked members one blast breaks in the same tick (same x, z) each add their own stamp.</item>
    /// <item><see cref="Land"/>: every structure landing (StructureLand on Brickfall and the dev Sandbox collapse preview, both
    /// through the destruction kit and ArenaPresentation.StructureHeard): a local coat of <see cref="LandLocal"/> × (.4 + .6
    /// energy) out to <see cref="LandReach"/> × its radius (6-14 m, the dust wall's reach) and the map-wide level 1 - exp(-Σ
    /// <see cref="LandWide"/> × (.4 + .6 energy)) (the curve the atmosphere published on its own before), both settling in over
    /// <see cref="Settle"/> s as the dust wall thins.</item>
    /// </list>
    /// Reads are at a clock (<see cref="At"/>): only entries at or before it count, so the killcam, replaying a past moment on
    /// the same match clock, shows the coat as it was then; a later clock stamps the new entries, an earlier one rebuilds the
    /// grid (at most <see cref="BreakSlots"/> stamps). Outputs: <see cref="Wide"/> (published as the <see cref="Global"/> shader
    /// value, 0..1) and <see cref="Local"/> at a block's centre (the instance's dust, at most <see cref="Cap"/>). NativeBlockLit
    /// adds MapLooks `blocks.dust`, <see cref="WideShare"/> × Wide and Local, weighs the sum by face (tops 1, sides .55) and
    /// keeps a tenth of it on the band, tag and lamp (<see cref="NativeBlockLooks.BandCoatKeep"/>). Cleared at each round
    /// rebuild (NativeArenaDressing: a new live arena or generation). Past the slots the oldest entries fold into a base that
    /// every clock sees (only after 512 breaks or 16 landings in one round, long before any killcam window). Allocation-free
    /// after construction.
    /// </summary>
    public sealed class NativeDustCoat
    {
        public const float Cell=2,Extent=48,PerBreak=.12f,Radius=6,Cap=.8f,LandLocal=.3f,LandReach=1.6f,LandReachMin=6,LandReachMax=14,WideShare=.6f;
        public const double Settle=8,LandWide=.22;public const int Size=48,BreakSlots=512,LandSlots=16;
        /// <summary>The map-wide level's shader global (0..1; NativeArenaDressing publishes 0 while no dressed map is drawn).</summary>
        public const string Global="_ArenaDustCoat";
        private readonly float[] grid=new float[Size*Size],baseGrid=new float[Size*Size];
        private readonly double[] breakAt=new double[BreakSlots];private readonly float[] breakX=new float[BreakSlots],breakY=new float[BreakSlots],breakZ=new float[BreakSlots];private readonly bool[] stamped=new bool[BreakSlots];private int breaks,nextBreak;
        private readonly double[] landAt=new double[LandSlots];private readonly float[] landX=new float[LandSlots],landZ=new float[LandSlots],landReach=new float[LandSlots],landWeight=new float[LandSlots],settled=new float[LandSlots];private int lands,nextLand;
        private double clock=double.NegativeInfinity,baseSum;
        /// <summary>The map-wide level at the last <see cref="At"/> clock (0..1).</summary>
        public float Wide{get;private set;}
        public int Breaks=>breaks;public int Lands=>lands;
        public void Clear(){Array.Clear(grid,0,grid.Length);Array.Clear(baseGrid,0,baseGrid.Length);breaks=nextBreak=lands=nextLand=0;baseSum=0;clock=double.NegativeInfinity;Wide=0;}
        /// <summary>A Break or Collapse at (x, y, z) at match time `time` (it stamps at x, z; y keeps stacked breaks of one tick
        /// apart); false when it is already in (or not a number).</summary>
        public bool Break(double time,float x,float y,float z)
        {
            if(!Valid(time,x,z)||float.IsNaN(y)||float.IsInfinity(y))return false;for(int i=0;i<breaks;i++)if(Same(breakAt[i],breakX[i],breakZ[i],time,x,z)&&Math.Abs(breakY[i]-y)<.05f)return false;
            int k;if(breaks<BreakSlots)k=breaks++;else{k=nextBreak;nextBreak=(nextBreak+1)%BreakSlots;Stamp(baseGrid,breakX[k],breakZ[k],PerBreak,Radius);if(!stamped[k])Stamp(grid,breakX[k],breakZ[k],PerBreak,Radius);}
            breakAt[k]=time;breakX[k]=x;breakY[k]=y;breakZ[k]=z;stamped[k]=false;return true;
        }
        /// <summary>A structure landing at (x, z) at `time`, its radius (m) and energy (0..1); false when it is already in.</summary>
        public bool Land(double time,float x,float z,float radius,float energy)
        {
            if(!Valid(time,x,z))return false;for(int i=0;i<lands;i++)if(Same(landAt[i],landX[i],landZ[i],time,x,z))return false;
            int k;if(lands<LandSlots)k=lands++;else{k=nextLand;nextLand=(nextLand+1)%LandSlots;baseSum+=LandWide*landWeight[k];Stamp(baseGrid,landX[k],landZ[k],LandLocal*landWeight[k],landReach[k]);Stamp(grid,landX[k],landZ[k],LandLocal*landWeight[k],landReach[k]);}
            float e=energy>0?Math.Min(1,energy):0,r=radius>0?radius:0;
            landAt[k]=time;landX[k]=x;landZ[k]=z;landWeight[k]=.4f+.6f*e;landReach[k]=Math.Max(LandReachMin,Math.Min(LandReachMax,r*LandReach));settled[k]=0;return true;
        }
        /// <summary>Moves the coat to the clock `now`: stamps the breaks due by then (rebuilding first when the clock went back)
        /// and settles the landings, which sets <see cref="Wide"/> and what <see cref="Local"/> returns.</summary>
        public void At(double now)
        {
            if(double.IsNaN(now))return;
            if(now<clock-1e-3){Array.Copy(baseGrid,grid,grid.Length);Array.Clear(stamped,0,breaks);}
            for(int i=0;i<breaks;i++)if(!stamped[i]&&breakAt[i]<=now){Stamp(grid,breakX[i],breakZ[i],PerBreak,Radius);stamped[i]=true;}
            clock=now;double sum=baseSum;
            for(int k=0;k<lands;k++){float s=landAt[k]<=now?(float)Smooth((now-landAt[k])/Settle):0;settled[k]=s;sum+=LandWide*landWeight[k]*s;}
            Wide=(float)(1-Math.Exp(-sum));
        }
        /// <summary>The coat at a block centre (x, z) at the last <see cref="At"/> clock: the break grid plus the settling
        /// landings' local coat, at most <see cref="Cap"/>.</summary>
        public float Local(float x,float z)
        {
            float g=Sample(x,z);
            for(int k=0;k<lands;k++){float s=settled[k];if(!(s>0))continue;float dx=x-landX[k],dz=z-landZ[k],d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<landReach[k])g+=LandLocal*landWeight[k]*(1-d/landReach[k])*s;}
            return Math.Min(Cap,g);
        }
        /// <summary>The break grid alone, bilinear between cell centres; 0 outside the grid.</summary>
        public float Sample(float x,float z)
        {
            float fx=(x+Extent)/Cell-.5f,fz=(z+Extent)/Cell-.5f;if(!(fx>-1&&fz>-1&&fx<Size&&fz<Size))return 0;int ix=(int)Math.Floor(fx),iz=(int)Math.Floor(fz);float tx=fx-ix,tz=fz-iz;
            return Lerp(Lerp(GridAt(ix,iz),GridAt(ix+1,iz),tx),Lerp(GridAt(ix,iz+1),GridAt(ix+1,iz+1),tx),tz);
        }
        private static void Stamp(float[] cells,float x,float z,float amount,float radius)
        {
            if(!(radius>0)||!(amount>0))return;int x0=Index(x-radius),x1=Index(x+radius),z0=Index(z-radius),z1=Index(z+radius);
            for(int i=x0;i<=x1;i++)for(int j=z0;j<=z1;j++){float cx=(i+.5f)*Cell-Extent,cz=(j+.5f)*Cell-Extent,d=(float)Math.Sqrt((cx-x)*(cx-x)+(cz-z)*(cz-z));if(d>=radius)continue;int k=i*Size+j;cells[k]=Math.Min(Cap,cells[k]+amount*(1-d/radius));}
        }
        private static bool Valid(double t,float x,float z)=>!double.IsNaN(t)&&!double.IsInfinity(t)&&!float.IsNaN(x)&&!float.IsNaN(z)&&!float.IsInfinity(x)&&!float.IsInfinity(z);
        private static bool Same(double t0,float x0,float z0,double t,float x,float z)=>Math.Abs(t0-t)<1e-3&&Math.Abs(x0-x)<.05f&&Math.Abs(z0-z)<.05f;
        private static double Smooth(double x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        private float GridAt(int i,int j)=>i<0||j<0||i>=Size||j>=Size?0:grid[i*Size+j];
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static int Index(float v)=>Math.Max(0,Math.Min(Size-1,(int)Math.Floor((v+Extent)/Cell)));
    }
}
