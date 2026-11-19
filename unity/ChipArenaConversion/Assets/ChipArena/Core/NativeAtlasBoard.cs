using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// ATLAS, the Deployment Station's split-flap departures board (docs/EXPEDITION-OVERHAUL.md R1 and W2; Claude, 2026-11-18):
    /// the tileset machine's presentation, before it has a choice. Presentation only and engine-free: it reads the registry
    /// (NativeTilesets) and this client's clock since the station opened, and decides nothing (no rule, ticket or wire).
    /// <list type="bullet">
    /// <item><b>Where:</b> hung from the ceiling beam over the DEPLOY gate on two drop rods, its face 2.5 m in front of the back
    /// wall (the plan's wall band sits behind that beam from the Overview); about 8.4 x 1.2 m, as tall as the Overview lets it be
    /// (the light bar in front of its top, the DEPLOY sign under its bottom), so the whole face reads from every station shot
    /// that sees it, its headings included.</item>
    /// <item><b>What:</b> headed ATLAS · DEPARTURES, four columns (TILESET · ENEMIES · INSTANCES · FINAL) and four lines: the
    /// selected tileset's on the large first line (CITY · MERIDIAN | THE INFECTED | 3 INSTANCES | THE SIREN), the others small
    /// and dim amber, NOT IN SERVICE where no tileset is (no invented names, no dates).</item>
    /// <item><b>When the station opens:</b> the emblem flap falls, then line 1 clatters into place left to right (each flap runs
    /// through the drum's glyphs before its own), lands within 1.2 s, and a bell rings with a spark off each cheek: an arrival,
    /// not a spin, so nothing pretends to roll. The camera opens on the board (its own shot) and eases to the Overview.</item>
    /// <item><b>Later (A3):</b> when the selection changes, the cells that differ flap to the new text (Retarget).</item>
    /// </list>
    /// The flaps are emissive (they light nothing: the station keeps its eight practicals). Sparks are machine 2's
    /// (NativeDeploySparks' kinds); the sounds are Claude's own table (NativeAtlasSounds), so Astra's NativeDeploySounds is unchanged.
    /// </summary>
    public static class NativeAtlasBoard
    {
        // ---- placement (station frame: metres, x right, y up, z away from the default camera) -----------------------------
        /// <summary>The face's bottom centre; the model is authored from it (x right, y up, z back), facing -z. From the Overview
        /// its bottom stays over the gate's DEPLOY sign and its top under the light bar at z 6 (both checked by ray).</summary>
        public static readonly Vector3 At=new Vector3(0,5.28f,7.1f);
        public const float Width=8.4f,Height=1.195f,Depth=.36f,Bezel=.045f;
        /// <summary>The ceiling beam it hangs from (NativeDeployModels.StationRoom: the I-beam at z 7.5, its bottom flange at 6.70),
        /// and the drop rods' x.</summary>
        public const float BeamZ=7.5f,BeamBottom=6.70f,RodX=3.0f;
        public static Matrix4x4 Root=>Matrix4x4.CreateTranslation(At);
        /// <summary>The model's footprint (model space): the cabinet, its drop rods and their clamps.</summary>
        public static void Footprint(out Vector3 min,out Vector3 max){min=new Vector3(-Width/2-.02f,-.02f,-.04f);max=new Vector3(Width/2+.02f,BeamBottom-At.Y+.01f,Depth+.1f);}
        /// <summary>The station shot that frames the board: the camera opens on it while the line arrives (TILESET's card), with
        /// the gate under it, between the console's title and its controls; the console's side panels step aside while it
        /// holds (<see cref="NativeAtlasStage.Panels"/>), so no aspect or Interface scale puts them over the board.</summary>
        public static readonly NativeDeployPresentation.View Shot=new NativeDeployPresentation.View{Eye=new Vector3(0,3.35f,-3.2f),Target=new Vector3(0,4.75f,7.1f),Fov=48};
        /// <summary>After the hold the board hands the camera to the Overview along a smoothstep over HandOff seconds of the
        /// station's clock, so the move starts and ends at rest (the station's own ease follows the path); the console's side
        /// panels ease back in along the same curve, or within PanelsIn seconds when a roll takes the camera sooner.</summary>
        public const float HandOff=1.2f,PanelsIn=.35f;
        /// <summary>The longest the opening's clock waits for the flaps to be drawable (NativeAtlasStage.Waiting).</summary>
        public const float MaxWait=1.5f;
        public static NativeDeployPresentation.View ViewAt(float since,in NativeDeployPresentation.View overview)
        {
            float u=(since-HoldUntil)/HandOff;if(!(u>0))return Shot;if(u>=1)return overview;float s=u*u*(3-2*u);
            return new NativeDeployPresentation.View{Eye=Vector3.Lerp(Shot.Eye,overview.Eye,s),Target=Vector3.Lerp(Shot.Target,overview.Target,s),Fov=Shot.Fov+(overview.Fov-Shot.Fov)*s};
        }

        // ---- the drum and the glyph atlas's tiles ---------------------------------------------------------------------------
        /// <summary>The flaps on every drum, in order (blank first): the station's stroke font's capitals, digits and marks.</summary>
        public const string Drum=" ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-·";
        public static int Glyphs=>Drum.Length;
        /// <summary>A character's drum index (unknown characters show blank).</summary>
        public static int GlyphOf(char c){int i=Drum.IndexOf(char.ToUpperInvariant(c));return i<0?0:i;}
        /// <summary>Atlas tiles: the drum's glyphs, the blank emblem, then each registry tileset's emblem in registry order.</summary>
        public static int BlankEmblem=>Glyphs;
        public static int EmblemTile(int registry)=>Glyphs+1+registry;
        public static int Tiles=>Glyphs+1+NativeTilesets.All.Length;

        // ---- the layout (board space: x right from the centre, y up from the face's bottom, the face at z 0) ------------------
        public enum Kind:byte{Glyph,Emblem}
        public struct Cell{public int Line,Column,Index;public Kind Kind;public bool Large;public float X0,Y0,X1,Y1;}
        /// <summary>The flaps' shape: width over height for a glyph flap (the emblem's is square); flaps are spaced by Gap.</summary>
        public const float FlapAspect=.533f,LargeFlap=.25f,SmallFlap=.14f,LargePitchY=.29f,SmallPitchY=.162f;
        public const float HeaderTop=Height-Bezel,HeaderBottom=.982f,LabelBottom=.84f,LargeTop=.83f,SmallTop=.538f;
        /// <summary>The header's and the column labels' lettering: ATLAS, then · DEPARTURES, then the labels (heights in metres and
        /// their strokes as a share of the height): large and bold enough to read from the Overview at 1080p (about 8.5 px tall,
        /// 1.5 px strokes; checked), where the station's own thin lettering (a tenth) would not.</summary>
        public const float TitleHeight=.13f,TitleWeight=.15f,SubtitleHeight=.11f,LabelHeight=.11f,LabelWeight=.175f;
        public const float LampX=-4.08f,EmblemX=-4.02f,ColumnsX=-3.735f,ColumnsEnd=4.075f,ColumnGap=.3f,Gap=.012f,SmallGap=.008f;
        public sealed class Layout
        {
            public Cell[] Cells;
            /// <summary>Each line's first cell and cell count (its emblem first, then its glyphs left to right, column by column).</summary>
            public int[] LineStart,LineCount;
            /// <summary>Large-line columns: each column's glyph count, its left edge and right edge (the small lines share the edges).</summary>
            public int[] Count;public float[] Left,Right;public float Pitch,SmallPitch;
            /// <summary>Per registry tileset (index k; k = All.Length for an empty line) and line size: each cell's target tile.</summary>
            public int[][] Large,Small;
            public int Line(int line,int k)=>line==0?Large[k].Length:Small[k].Length;
        }
        private static Layout board;
        /// <summary>The board's layout from the registry (built once): the large line's columns as wide as their longest text.</summary>
        public static Layout Board=>board??=Build();
        private static Layout Build()
        {
            var L=new Layout{Count=new int[4],Left=new float[4],Right=new float[4]};int n=NativeTilesets.Columns.Length;
            for(int c=0;c<n;c++){int w=NativeTilesets.Cell(null,c).Length;foreach(var t in NativeTilesets.All)w=Math.Max(w,NativeTilesets.Cell(t,c).Length);L.Count[c]=Math.Max(1,w);}
            int total=0;for(int c=0;c<n;c++)total+=L.Count[c];
            L.Pitch=Math.Min(.16f,(ColumnsEnd-ColumnsX-ColumnGap*(n-1))/total);L.SmallPitch=SmallFlap*FlapAspect+SmallGap;
            float x=ColumnsX;for(int c=0;c<n;c++){L.Left[c]=x;L.Right[c]=x+L.Count[c]*L.Pitch;x=L.Right[c]+ColumnGap;}
            var cells=new List<Cell>();L.LineStart=new int[NativeTilesets.Lines];L.LineCount=new int[NativeTilesets.Lines];
            for(int line=0;line<NativeTilesets.Lines;line++)
            {
                bool large=line==0;float top=large?LargeTop:SmallTop-(line-1)*SmallPitchY,h=large?LargeFlap:SmallFlap;L.LineStart[line]=cells.Count;int index=0;
                cells.Add(new Cell{Line=line,Column=-1,Index=index++,Kind=Kind.Emblem,Large=large,X0=EmblemX,Y0=top-h,X1=EmblemX+h,Y1=top});
                for(int c=0;c<n;c++)
                {
                    float pitch=large?L.Pitch:L.SmallPitch,fw=large?L.Pitch-Gap:L.SmallPitch-SmallGap;int k=large?L.Count[c]:(int)Math.Floor((L.Right[c]-L.Left[c]+1e-4f)/pitch);
                    for(int i=0;i<k;i++){float x0=L.Left[c]+i*pitch;cells.Add(new Cell{Line=line,Column=c,Index=index++,Kind=Kind.Glyph,Large=large,X0=x0,Y0=top-h,X1=x0+fw,Y1=top});}
                }
                L.LineCount[line]=cells.Count-L.LineStart[line];
            }
            L.Cells=cells.ToArray();
            // every tileset's (and the empty line's) target tiles for a large line and a small one, left-aligned in each column
            int kinds=NativeTilesets.All.Length+1;L.Large=new int[kinds][];L.Small=new int[kinds][];
            for(int k=0;k<kinds;k++)
            {
                var t=k<NativeTilesets.All.Length?NativeTilesets.All[k]:null;
                L.Large[k]=Targets(L,0,t,k);L.Small[k]=Targets(L,1,t,k);
            }
            return L;
        }
        private static int[] Targets(Layout L,int line,NativeTilesets.Tileset t,int k)
        {
            var o=new int[L.LineCount[line]];
            for(int i=0;i<o.Length;i++)
            {
                var c=L.Cells[L.LineStart[line]+i];
                if(c.Kind==Kind.Emblem){o[i]=t!=null&&t.Emblem!=null?EmblemTile(k):BlankEmblem;continue;}
                string text=NativeTilesets.Cell(t,c.Column);int first=L.LineStart[line]+1;for(int j=0;j<c.Column;j++)first+=Columns(L,line,j);
                int at=L.LineStart[line]+i-first;o[i]=at<text.Length?GlyphOf(text[at]):0;
            }
            return o;
        }
        /// <summary>How many glyph cells a column has on a line.</summary>
        public static int Columns(Layout L,int line,int column){int n=0;int s=L.LineStart[line],e=s+L.LineCount[line];for(int i=s;i<e;i++)if(L.Cells[i].Column==column)n++;return n;}
        /// <summary>The registry index of a tileset (an empty line: All.Length).</summary>
        public static int Kinds(NativeTilesets.Tileset t){for(int k=0;k<NativeTilesets.All.Length;k++)if(ReferenceEquals(NativeTilesets.All[k],t))return k;return NativeTilesets.All.Length;}

        // ---- the arrival ----------------------------------------------------------------------------------------------------
        /// <summary>When the station opens (this client's clock): the emblem flap falls at EmblemAt; line 1's flaps start between
        /// ArriveAt and ArriveAt + Stagger (left to right, a little jitter), each falling MinFlips..MaxFlips flaps of FlipSeconds
        /// (the flap itself falls in FallShare of that), so the line has landed by LandAt (1.2 s after it began: the bell); the
        /// board shot holds until HoldUntil.</summary>
        public const float EmblemAt=.08f,ArriveAt=.2f,FlipSeconds=.052f,FallShare=.85f,Stagger=.28f,Jitter=.03f,LandAt=1.4f,HoldUntil=2.8f;
        public const int MinFlips=6,MaxFlips=13;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        // A hash of three ints in [0,1) (NativeDeploySparks' mixer): every flap's and spark's chances, fixed.
        internal static float H(int a,int b,int c)
        {
            unchecked{uint h=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+(uint)c*0xC2B2AE3Du+0x27D4EB2Fu;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h>>8)*(1f/16777216f);}
        }
        /// <summary>A cell's first flap and its number of flaps for a change begun at `begin` (0: the station opening): an emblem
        /// falls once at begin + EmblemAt; a glyph's run starts at begin + ArriveAt, staggered by its place along the line, and
        /// `salt` varies a later re-flap's chances.</summary>
        public static void Plan(in Cell c,float begin,int salt,out float start,out int flips)
        {
            if(c.Kind==Kind.Emblem){start=begin+EmblemAt;flips=1;return;}
            float along=Clamp01((c.X0-ColumnsX)/(ColumnsEnd-ColumnsX));
            start=begin+ArriveAt+Stagger*along+Jitter*H(c.Line*512+c.Index,salt,1);flips=MinFlips+(int)Math.Floor(H(c.Line*512+c.Index,salt,2)*(MaxFlips-MinFlips+1));
            if(flips>MaxFlips)flips=MaxFlips;
        }
        /// <summary>The glyph a cell shows after flap j of a run of `flips` ending on `target`: the drum's glyphs before it, in order
        /// (an emblem run is the single flap onto the emblem).</summary>
        public static int Sequence(int target,int flips,int j)
        {
            if(target>=Glyphs)return target;int n=Glyphs;return ((target-(flips-1-j))%n+n)%n;
        }
        /// <summary>A cell this frame: the glyph its top and the fallen flap show (Next), the one its bottom still shows (Prev), and
        /// how far the flap between them has fallen (Phase 0..1; negative: at rest on Next).</summary>
        public struct Pose{public int Prev,Next;public float Phase;}
        /// <summary>The pose `since` s into a run (start, flips, from, target).</summary>
        public static Pose PoseOf(float since,float start,int flips,int from,int target)
        {
            if(!(since>=start)||flips<=0)return new Pose{Prev=from,Next=flips<=0?target:from,Phase=-1};
            float u=(since-start)/FlipSeconds;int j=(int)Math.Floor(u);if(j>=flips)return new Pose{Prev=target,Next=target,Phase=-1};
            float phase=(u-j)/FallShare;int prev=j==0?from:Sequence(target,flips,j-1),next=Sequence(target,flips,j);
            return phase>=1?new Pose{Prev=next,Next=next,Phase=-1}:new Pose{Prev=prev,Next=next,Phase=phase};
        }
        /// <summary>The falling flap's angle (radians, 0 up to pi down) at a phase: it tips slowly and drops fast.</summary>
        public static float FlapAngle(float phase)=>(float)(Math.PI*Math.Pow(Clamp01(phase),1.7));
        /// <summary>The bell's flash on line 1 (1 as it rings, gone in .6 s).</summary>
        public static float Flash(float since){float a=since-LandAt;if(!(a>=0)||a>=.6f)return 0;float k=1-a/.6f;return k*k;}

        // ---- sparks: machine 2 (NativeDeploySparks' kinds, drawn by Runtime NativeAtlasScene) --------------------------------
        /// <summary>The most sparks a frame draws, and when the last dies.</summary>
        public const int SparkLimit=48;public const float SparkEnd=LandAt+1.1f;
        private static Vector4 C(float r,float g,float b,float level)=>new Vector4(r*level,g*level,b*level,1);
        private static Vector4 Mix(Vector4 a,Vector4 b,float t){t=Clamp01(t);return a+(b-a)*t;}
        /// <summary>As the bell rings: a burst of warm sparks out of each cheek's vent at line 1's height, out, up and toward the
        /// room, falling; and a flash at line 1's in-service lamp in the tileset's accent. Reduced effects: about a third.
        /// Adds to `into` from `count` (never past its length) and returns the new count.</summary>
        public static int Sparks(NativeDeploySparks.Spark[] into,int count,float since,int accent,bool reduced)
        {
            float age0=since-LandAt;if(into==null||!(age0>=0)||age0>=SparkEnd-LandAt)return count;
            int n=reduced?6:18;var lineY=LargeTop-LargeFlap/2;
            for(int side=-1;side<=1;side+=2)
            {
                var vent=At+new Vector3(side*(Width/2+.005f),lineY,.08f);int salt=side<0?7:11;
                for(int j=0;j<n&&count<into.Length;j++)
                {
                    float t0=.05f*H(salt,j,1),age=age0-t0,life=.45f+.4f*H(salt,j,2);if(age<0||age>=life)continue;float a=age/life;
                    var v=new Vector3(side*(.7f+1.5f*H(salt,j,3)),.3f+1.7f*H(salt,j,4),-(.4f+1.2f*H(salt,j,5)));
                    var o=vent+new Vector3(0,(H(salt,j,6)-.5f)*.22f,0);var p=o+v*age+new Vector3(0,-4.9f*age*age,0);var vel=v+new Vector3(0,-9.8f*age,0);
                    var hot=a<.35f?Mix(C(1,.95f,.75f,1),C(1,.72f,.25f,1),a/.35f):Mix(C(1,.72f,.25f,1),C(.9f,.3f,.05f,1),(a-.35f)/.65f);
                    into[count++]=new NativeDeploySparks.Spark{At=p,Axis=vel*NativeDeploySparks.Streak,Width=.024f,Color=hot*(5.5f*(1-a*a)),Kind=NativeDeploySparks.Kind.Burst};
                }
            }
            if(count<into.Length&&age0<.45f)
            {
                float u=age0/.45f;var c=WeaponPaint.Linear(accent);
                into[count++]=new NativeDeploySparks.Spark{At=At+new Vector3(LampX,lineY,-.03f),Axis=Vector3.Zero,Width=.5f*(.7f+.5f*u),Color=new Vector4(c.X,c.Y,c.Z,1)*(3.2f*(1-u)*(1-u)),Kind=NativeDeploySparks.Kind.Flash};
            }
            return count;
        }

        // ---- sound moments (Claude's table: NativeAtlasSounds) ----------------------------------------------------------------
        public enum CueKind:byte{Clatter,Bell}
        /// <summary>A moment of the arrival for sound, at `At` s after the station opened, and how late this frame hears it (`Late`,
        /// s: the frame's lag, or a stall), so its clip can start that far in and stay on the picture (NativeAtlasSounds.StartAt).
        /// Machine 2: Astra's NativeDeploySounds plays machines 0 and 1 only, so these never double up with the reels' sounds.</summary>
        public struct Cue{public CueKind Kind;public float At,Late;}
        public const int Machine=2;
        public static readonly Cue[] Schedule={new Cue{Kind=CueKind.Clatter,At=EmblemAt},new Cue{Kind=CueKind.Bell,At=LandAt}};
        /// <summary>The moments heard between two frames' clocks (`before` exclusive, `now` inclusive), added to `into` from `count`
        /// with their lateness: a reopened board (now before `before`) starts again; an unknown `before` hears only the last
        /// quarter second.</summary>
        public static int Heard(float before,float now,Cue[] into,int count)
        {
            if(!(now>=0)||into==null)return count;if(!(before>=0))before=now-.25f;else if(before>now)before=-1e-4f;
            foreach(var c in Schedule)if(c.At>before&&c.At<=now&&count<into.Length)into[count++]=new Cue{Kind=c.Kind,At=c.At,Late=now-c.At};
            return count;
        }
        /// <summary>Line 1's flaps landing, as the clatter's clicks: each flap's fall end (the emblem's first), seconds after
        /// EmblemAt, when the clatter's cue plays (the arrival from blank; NativeAtlasSounds builds the clatter from it, so the sound
        /// lands with the picture).</summary>
        public static List<float> Clicks(Layout L,int registry)
        {
            var o=new List<float>();var targets=L.Large[registry];
            for(int i=0;i<targets.Length;i++)
            {
                var c=L.Cells[L.LineStart[0]+i];if(targets[i]==0&&c.Kind==Kind.Glyph)continue;Plan(c,0,0,out float start,out int flips);
                for(int j=0;j<flips;j++)o.Add(start+(j+FallShare)*FlipSeconds-EmblemAt);
            }
            o.Sort();return o;
        }
    }

    /// <summary>
    /// ATLAS posed for a frame (owned by NativeDeployStage, which frames the camera on it; Runtime NativeAtlasScene draws it):
    /// the tileset on each line from the registry and the selection hook, every flap's pose from this client's clock since the
    /// station opened (a clock that runs backwards, as the DEV preview's restart does, opens it again), the lamps' levels, the
    /// bell's sparks, the moments heard for sound and how much the console's side panels show. The opening's clock starts on
    /// the station's second frame: the first one's time is read before the station's scene is built (a first visit's build
    /// and first draw take a tenth of a second or more), so none of that runs the arrival. Frame allocates nothing.
    /// </summary>
    public sealed class NativeAtlasStage
    {
        public readonly NativeAtlasBoard.Layout Layout=NativeAtlasBoard.Board;
        public readonly NativeAtlasBoard.Pose[] Poses;
        /// <summary>The tileset on each line (null: NOT IN SERVICE) and the selected one (line 1's).</summary>
        public readonly NativeTilesets.Tileset[] Lines=new NativeTilesets.Tileset[NativeTilesets.Lines];public NativeTilesets.Tileset Selected;
        /// <summary>Seconds since the station opened on this client (NaN before the first frame).</summary>
        public float Since=float.NaN;
        /// <summary>Line 1 has landed (the bell has rung); a flap is falling this frame; the camera frames the board.</summary>
        public bool Landed,Moving,Framed;
        /// <summary>The camera the board asks for while Frame returns true: its shot, then the eased hand-off to the Overview.</summary>
        public NativeDeployPresentation.View View;
        /// <summary>How much the console's side panels (the machines' cards, display only) show, 0..1: none while the board holds
        /// its shot, so they never cover it whatever the window's aspect or the Interface scale; back along the hand-off's curve,
        /// or within NativeAtlasBoard.PanelsIn s once a roll takes the camera; 1 when the station opens on another shot.</summary>
        public float Panels=1;
        /// <summary>Set by the Runtime while it cannot draw the flaps yet (no bake: the glyph atlas rasterising on a worker, a
        /// first visit's tenth of a second or so): the opening's clock waits for it, at most NativeAtlasBoard.MaxWait s, so the
        /// arrival never plays to a blank board.</summary>
        public bool Waiting;
        /// <summary>Changes whenever a flap's pose does (the Runtime uploads the flaps only then).</summary>
        public int Version{get;private set;}
        /// <summary>This frame's levels: the header, line 1's flaps and lamp (with the bell's flash), the other lines'.</summary>
        public float Header,LineGlyph,LineLamp,Dim,DimLamp,Flash;
        public readonly NativeDeploySparks.Spark[] Sparks=new NativeDeploySparks.Spark[NativeAtlasBoard.SparkLimit];public int SparkCount;
        public readonly NativeAtlasBoard.Cue[] Cues=new NativeAtlasBoard.Cue[4];public int CueCount;
        private readonly float[] start;private readonly int[] flips,from,target;private readonly int[] kinds=new int[NativeTilesets.Lines];
        private float openAt=float.NaN,last=float.NaN,heard=float.NaN,waitFrom,prevSince=float.NaN;private bool moved,anchored;private int salt;
        /// <summary>Where the selection comes from: the registry's hook (the checks put a stand-in here to run a re-flap).</summary>
        internal Func<ExpeditionDeploymentState,NativeTilesets.Tileset> Select=NativeTilesets.Selected;
        public NativeAtlasStage()
        {
            int n=Layout.Cells.Length;Poses=new NativeAtlasBoard.Pose[n];start=new float[n];flips=new int[n];from=new int[n];target=new int[n];
            for(int i=0;i<kinds.Length;i++)kinds[i]=-1;
        }
        // The station opened at `t`: line 1 arrives from blank; the other lines already show their text.
        private void Open(float t)
        {
            openAt=t;heard=float.NaN;salt=0;
            for(int line=0;line<NativeTilesets.Lines;line++)
            {
                int k=kinds[line]=NativeAtlasBoard.Kinds(Lines[line]);var targets=line==0?Layout.Large[k]:Layout.Small[k];int s=Layout.LineStart[line];
                for(int i=0;i<targets.Length;i++)
                {
                    int c=s+i;target[c]=targets[i];
                    if(line==0){from[c]=Layout.Cells[c].Kind==NativeAtlasBoard.Kind.Emblem?NativeAtlasBoard.BlankEmblem:0;if(from[c]==target[c]){flips[c]=0;start[c]=0;}else NativeAtlasBoard.Plan(Layout.Cells[c],0,0,out start[c],out flips[c]);}
                    else{from[c]=target[c];flips[c]=0;start[c]=0;}
                }
            }
        }
        // A line's tileset changed (A3's selection): each cell whose glyph differs flaps from what it shows now to the new text.
        private void Retarget(int line,int k,float since)
        {
            kinds[line]=k;var targets=line==0?Layout.Large[k]:Layout.Small[k];int s=Layout.LineStart[line];salt++;
            for(int i=0;i<targets.Length;i++)
            {
                int c=s+i;if(targets[i]==target[c])continue;var p=NativeAtlasBoard.PoseOf(since,start[c],flips[c],from[c],target[c]);
                from[c]=p.Next;target[c]=targets[i];NativeAtlasBoard.Plan(Layout.Cells[c],since,salt,out start[c],out flips[c]);
            }
        }
        /// <summary>Poses the board for one frame; true while the board has the camera (View), only over the Overview: its shot
        /// while the line arrives and holds (Framed), then the eased hand-off to the Overview.</summary>
        public bool Frame(in DeployInput f,NativeDeployPresentation.Shot shot)
        {
            float t=f.Time;Selected=Select(f.State)??NativeTilesets.Of(NativeTilesets.City);NativeTilesets.Board(Selected,Lines);
            bool opening=!(t>=last)||float.IsNaN(openAt);float dt=opening?0:t-last;
            if(opening){Open(t);anchored=false;waitFrom=t;}
            // the opening's second frame: its clock starts now (the first frame's time was read before the scene was built), or
            // once the flaps can draw
            else if(!anchored){openAt=t;anchored=!Waiting||t-waitFrom>=NativeAtlasBoard.MaxWait;}
            last=t;Since=t-openAt;float since=Since,before=opening?float.NaN:prevSince;prevSince=since;
            for(int line=0;line<NativeTilesets.Lines;line++){int k=NativeAtlasBoard.Kinds(Lines[line]);if(k!=kinds[line])Retarget(line,k,since);}
            bool moving=false;
            for(int c=0;c<Poses.Length;c++)
            {
                var p=NativeAtlasBoard.PoseOf(since,start[c],flips[c],from[c],target[c]);
                // a one-flap run (an emblem) is never skipped: a frame that jumps from before its fall to past it (a long frame,
                // as a first visit's station has) shows it mid-fall, and the next shows it landed
                if(flips[c]==1&&p.Phase<0&&before<start[c]&&since>=start[c])p=new NativeAtlasBoard.Pose{Prev=from[c],Next=target[c],Phase=.5f};
                if(p.Phase>=0)moving=true;
                if(p.Prev!=Poses[c].Prev||p.Next!=Poses[c].Next||p.Phase!=Poses[c].Phase){Poses[c]=p;moved=true;}
            }
            if(moved){Version++;moved=false;}
            Moving=moving;
            bool any=false;for(int c=0;c<Poses.Length&&!any;c++)if(since<start[c]+flips[c]*NativeAtlasBoard.FlipSeconds)any=true;
            Landed=since>=NativeAtlasBoard.LandAt&&!any;
            // the lamps: line 1's in-service lamp pulses while its line arrives, then holds in the accent with the bell's flash
            Flash=NativeAtlasBoard.Flash(since);float pulse=.5f+.5f*(float)Math.Sin(t*7);
            Header=1.55f+.5f*Flash;LineGlyph=1.35f+1.1f*Flash;LineLamp=since>=NativeAtlasBoard.LandAt?2.3f+2.2f*Flash:.35f+.9f*pulse;Dim=.55f;DimLamp=.32f;
            SparkCount=NativeAtlasBoard.Sparks(Sparks,0,since,Selected.Accent,f.Reduced);
            CueCount=NativeAtlasBoard.Heard(heard,since,Cues,0);heard=since;
            bool over=shot==NativeDeployPresentation.Shot.Overview&&since>=0;
            Framed=over&&since<NativeAtlasBoard.HoldUntil;bool handing=over&&!Framed&&since<NativeAtlasBoard.HoldUntil+NativeAtlasBoard.HandOff;
            if(Framed||handing)View=NativeAtlasBoard.ViewAt(since,NativeDeployPresentation.ShotOf(NativeDeployPresentation.Shot.Overview));
            // the console's side panels: out while the board holds, back with the hand-off (or soon after a roll takes the camera)
            float u=(since-NativeAtlasBoard.HoldUntil)/NativeAtlasBoard.HandOff,panels=Framed?0:handing?u*u*(3-2*u):1;
            Panels=opening||panels<=Panels?panels:Math.Min(panels,Panels+dt/NativeAtlasBoard.PanelsIn);
            return Framed||handing;
        }
    }
}
