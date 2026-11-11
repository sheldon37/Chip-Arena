using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The stage art (docs/arenas/STAGE-ART.md): a stage's modelled meshes, made in Blender by tools/stages/ (stagekit.py and the
    /// stage's own script) on the map's blocks and its Core kit's parts, into its own pack bank Resources/ChipArenaStage&lt;Name&gt;
    /// (git-ignored, from the builder's Drive zip; Core NativePackBank's format). Runtime NativeStageArtDraw draws it in place of
    /// the boxes it replaces; without the bank nothing here is read and the box kit draws exactly as before. Presentation only:
    /// never sim data, never a collider, never in the network frame.
    /// <para>The bank's props, by name (tier 0 Fine, 1 Mid, 2 Low):</para>
    /// <list type="bullet">
    /// <item><c>static/&lt;chunk&gt;/&lt;tier&gt;</c> (group "static"): a world-space chunk of the static scenery.</item>
    /// <item><c>block/&lt;block id&gt;/&lt;tier&gt;</c> (group "block"): the model in a breakable block's place, in the block's own frame
    /// (its centre), drawn where the block is while it stands at <see cref="Keep"/> or better.</item>
    /// <item><c>rig/&lt;name&gt;/&lt;tier&gt;</c> (group "rig"): a moving part in its own frame, posed each frame by the stage's Core
    /// art pose (NativeSanctumArt for the Sanctum) from its rig's state.</item>
    /// <item><c>cover/&lt;block id&gt;</c> (group "cover", no parts): a map block whose dressed box the art replaces.</item>
    /// </list>
    /// Pure; allocates only while reading a bank (once per map).
    /// </summary>
    public static class NativeStageArt
    {
        public const int Tiers=3,Fine=0,Mid=1,Low=2;
        /// <summary>The worst damage stage (NativeBlockLooks.Stage) a block's model still draws at; past it the dressing's box
        /// (the stub) draws again.</summary>
        public const int Keep=2;
        /// <summary>The distances (m, eye to a piece's bounds) inside which the Fine and the Mid tier draw.</summary>
        public const float FineReach=16f,MidReach=38f;
        /// <summary>The stages with art: map id, bank name. A new stage adds its line.</summary>
        private static readonly (string map,string bank)[] Banks={(NativeSanctumLooks.MapId,"ChipArenaStageSanctum"),(NativeClubLooks.MapId,NativeSubstationArt.Bank),(NativeCausewayLooks.MapId,"ChipArenaStageCauseway"),
            (NativeEmporiumLooks.MapId,"ChipArenaStageEmporium"),
            (NativeCyberCityLooks.MapId,NativeCyberCityArt.Bank),
            (NativePierLooks.MapId,NativePierArt.Bank),
            (NativeDeepwellLooks.MapId,NativeDeepwellArt.Bank),
            (NativePolarLooks.MapId,NativePolarArt.Bank),
            (NativeBrickfallScenery.MapId,NativeBrickfallArt.Bank),
            (NativeSkyframeLooks.MapId,NativeSkyframeArt.Bank),
            (NativeWardZeroLooks.MapId,NativeWardZeroArt.Bank),("expedition-ward-zero",NativeWardZeroArt.Bank),
            (NativeFactoryHazards.MapId,NativeFactoryArt.Bank),
            (NativeBreakwaterLooks.MapId,NativeBreakwaterArt.Bank)};
        public static string BankOf(string mapId){if(mapId==null)return null;foreach(var b in Banks)if(b.map==mapId)return b.bank;return null;}
        /// <summary>Layers (docs/arenas/STAGE-ART.md "Depth fighting"; owner 2026-11-11: "There is Z-Fighting on the stage
        /// remodelings ... Floors mainly"). A face that lies on another draws nearer the eye along its own ray, by its layer, so the
        /// two never fight however flat the view (ChipArena/StageLit; the bank carries each triangle's layer). A block's or a rig's
        /// model draws <see cref="ModelLayers"/> up; a kit box a stage keeps drawing over its art <see cref="KitLayers"/> up and
        /// one more for each kept box it lies on (<see cref="BoxLayers"/>).</summary>
        public const int ModelLayers=2,KitLayers=8,MostLayers=16;
        /// <summary>How far towards the eye a point `layers` up draws: the share of its distance (m) it comes nearer. The same as
        /// the shader's StageLayer (4e-6 z + 5e-5 per layer, at most a thirtieth).</summary>
        public static float Nearer(float distance,float layers)=>layers<=0?0:Math.Min(Math.Min(layers,MostLayers)*(4e-6f*distance+5e-5f),.033f);
        /// <summary>The kept kit boxes' own stacking: for each box (centre x, y, z and half extents, world axes: six floats), how
        /// many kept boxes it lies on (a box lies on another when a face of it stands 0 to 30 mm out from the other's same face and
        /// overlaps it; in one plane the smaller lies on the larger), at most <see cref="MostLayers"/> - <see cref="KitLayers"/>.
        /// A box with <paramref name="skip"/> set takes no part (a turned box). Once per map.</summary>
        public static void BoxLayers(float[] boxes,int count,bool[] skip,int[] layers)
        {
            const float reach=.03f,tie=2e-5f,least=2e-4f;int most=MostLayers-KitLayers;Array.Clear(layers,0,count);
            var under=new List<int>[count];
            // sorted by x so only neighbours in x are met
            var order=new int[count];for(int i=0;i<count;i++)order[i]=i;Array.Sort(order,(p,q)=>(boxes[p*6]-boxes[p*6+3]).CompareTo(boxes[q*6]-boxes[q*6+3]));
            for(int oi=0;oi<count;oi++)
            {
                int i=order[oi];if(skip!=null&&skip[i])continue;int a=i*6;float aMax=boxes[a]+boxes[a+3]+reach;
                for(int oj=oi+1;oj<count;oj++)
                {
                    int j=order[oj];int b=j*6;if(boxes[b]-boxes[b+3]>aMax)break;if(skip!=null&&skip[j])continue;
                    // one order per pair: where two faces disagree (each box proud of the other on one side), the smaller box is on top
                    bool aUp=false,bUp=false;
                    for(int ax=0;ax<3;ax++)
                    {
                        int u=(ax+1)%3,v=(ax+2)%3;
                        float ou=Math.Min(boxes[a+u]+boxes[a+3+u],boxes[b+u]+boxes[b+3+u])-Math.Max(boxes[a+u]-boxes[a+3+u],boxes[b+u]-boxes[b+3+u]);
                        float ov=Math.Min(boxes[a+v]+boxes[a+3+v],boxes[b+v]+boxes[b+3+v])-Math.Max(boxes[a+v]-boxes[a+3+v],boxes[b+v]-boxes[b+3+v]);
                        if(ou<=0||ov<=0||ou*ov<least)continue;
                        float areaA=boxes[a+3+u]*boxes[a+3+v],areaB=boxes[b+3+u]*boxes[b+3+v];
                        for(int sign=-1;sign<=1;sign+=2)
                        {
                            float gap=sign*((boxes[a+ax]+sign*boxes[a+3+ax])-(boxes[b+ax]+sign*boxes[b+3+ax]));if(Math.Abs(gap)>reach)continue;
                            if(gap>tie||gap>=-tie&&(areaA<areaB||areaA==areaB&&i>j))aUp=true;else bUp=true;
                        }
                    }
                    if(!aUp&&!bUp)continue;
                    if(aUp&&bUp){float va=boxes[a+3]*boxes[a+4]*boxes[a+5],vb=boxes[b+3]*boxes[b+4]*boxes[b+5];aUp=va<vb||va==vb&&i>j;}
                    int top=aUp?i:j,bottom=aUp?j:i;(under[top]??=new List<int>()).Add(bottom);
                }
            }
            for(int sweep=0;sweep<most;sweep++)
            {
                bool changed=false;
                for(int i=0;i<count;i++){var us=under[i];if(us==null)continue;int v=0;foreach(int k in us)v=Math.Max(v,layers[k]);v=Math.Min(most,v+1);if(v>layers[i]){layers[i]=v;changed=true;}}
                if(!changed)break;
            }
        }
        /// <summary>The tier a piece draws at from the eye (the low graphics tier always draws the Low one).</summary>
        public static int TierAt(float distance,bool low)=>low?Low:distance<FineReach?Fine:distance<MidReach?Mid:Low;

        /// <summary>A bank's props sorted by role: the static chunks (each with its three tiers' props and bounds), the block
        /// models and the rig parts by name (prop index per tier, -1 where a tier is missing: the nearest present tier stands in),
        /// and the covered blocks.</summary>
        public sealed class Plan
        {
            public readonly List<Chunk> Static=new List<Chunk>();
            public readonly Dictionary<string,int[]> Blocks=new Dictionary<string,int[]>(StringComparer.Ordinal);
            public readonly Dictionary<string,int[]> Rig=new Dictionary<string,int[]>(StringComparer.Ordinal);
            public readonly HashSet<string> Covers=new HashSet<string>(StringComparer.Ordinal);
            public int[] RigProps(string name)=>name!=null&&Rig.TryGetValue(name,out var p)?p:null;
        }
        public sealed class Chunk{public string Id;public readonly int[] Props={-1,-1,-1};public System.Numerics.Vector3 Min=new System.Numerics.Vector3(float.MaxValue),Max=new System.Numerics.Vector3(float.MinValue);}

        /// <summary>The plan of a stage bank (null bank: null). Unknown names are ignored, so a bank may carry more than a runtime
        /// knows.</summary>
        public static Plan Read(NativePackBank.Bank bank)
        {
            if(bank==null)return null;var plan=new Plan();var chunks=new Dictionary<string,Chunk>(StringComparer.Ordinal);
            for(int i=0;i<bank.Props.Count;i++)
            {
                var p=bank.Props[i];var name=p.Name;if(name==null)continue;
                if(p.Group=="cover"&&name.StartsWith("cover/",StringComparison.Ordinal)){plan.Covers.Add(name.Substring(6));continue;}
                if(!Split(name,out string kind,out string id,out int tier))continue;
                if(kind=="static"&&p.Group=="static")
                {
                    if(!chunks.TryGetValue(id,out var c)){c=new Chunk{Id=id};chunks[id]=c;plan.Static.Add(c);}
                    c.Props[tier]=i;c.Min=System.Numerics.Vector3.Min(c.Min,p.Min);c.Max=System.Numerics.Vector3.Max(c.Max,p.Max);
                }
                else if(kind=="block"&&p.Group=="block")Slot(plan.Blocks,id)[tier]=i;
                else if(kind=="rig"&&p.Group=="rig")Slot(plan.Rig,id)[tier]=i;
            }
            foreach(var c in plan.Static)Fill(c.Props);foreach(var v in plan.Blocks.Values)Fill(v);foreach(var v in plan.Rig.Values)Fill(v);
            return plan;
        }
        private static int[] Slot(Dictionary<string,int[]> d,string id){if(!d.TryGetValue(id,out var s)){s=new[]{-1,-1,-1};d[id]=s;}return s;}
        // A missing tier takes the nearest present one (the finer first).
        private static void Fill(int[] t){for(int i=0;i<Tiers;i++)if(t[i]<0)for(int d=1;d<Tiers&&t[i]<0;d++){if(i-d>=0&&t[i-d]>=0)t[i]=t[i-d];else if(i+d<Tiers&&t[i+d]>=0)t[i]=t[i+d];}}
        /// <summary>"kind/id/tier" (the id may hold slashes); the tier a digit 0..2.</summary>
        public static bool Split(string name,out string kind,out string id,out int tier)
        {
            kind=id=null;tier=-1;int a=name.IndexOf('/'),b=name.LastIndexOf('/');if(a<=0||b<=a+1||b==name.Length-1)return false;
            if(!int.TryParse(name.Substring(b+1),out tier)||tier<0||tier>=Tiers)return false;kind=name.Substring(0,a);id=name.Substring(a+1,b-a-1);return true;
        }
        /// <summary>The block's damage stage (NativeBlockLooks.Stage) from its health as the dressing reads it.</summary>
        public static int Stage(float health,float max,bool indestructible,float s1,float s2)=>NativeBlockLooks.Stage(NativeBlockLooks.Fraction(health,max,indestructible),s1,s2);
    }

    /// <summary>
    /// One frame's rig draws of a stage's art: each a rig slot (the stage's own list of rig prop names), a place (3 x 4 affine rows,
    /// Unity space, the prop's frame to the world) and a tint (rgb times the glow sub-mesh's light; w the solid's damage, 0).
    /// Fixed capacity: allocation free per frame.
    /// </summary>
    public sealed class NativeStageArtPose
    {
        public const int Capacity=512;
        public int Count;public readonly int[] Slot=new int[Capacity];public readonly float[] Place=new float[Capacity*12],Tint=new float[Capacity*4];
        public void Clear()=>Count=0;
        /// <summary>A draw at translation t, rotation q (Unity quaternion), scaled by s along the prop's y.</summary>
        public void Add(int slot,double tx,double ty,double tz,in FactoryQ q,double sy=1,double r=1,double g=1,double b=1)
        {
            if(Count>=Capacity)return;int o=Count*12;double x=q.X,y=q.Y,z=q.Z,w=q.W;
            Place[o]=(float)(1-2*(y*y+z*z));Place[o+1]=(float)(2*(x*y-z*w)*sy);Place[o+2]=(float)(2*(x*z+y*w));Place[o+3]=(float)tx;
            Place[o+4]=(float)(2*(x*y+z*w));Place[o+5]=(float)((1-2*(x*x+z*z))*sy);Place[o+6]=(float)(2*(y*z-x*w));Place[o+7]=(float)ty;
            Place[o+8]=(float)(2*(x*z-y*w));Place[o+9]=(float)(2*(y*z+x*w)*sy);Place[o+10]=(float)(1-2*(x*x+y*y));Place[o+11]=(float)tz;
            int t=Count*4;Tint[t]=(float)r;Tint[t+1]=(float)g;Tint[t+2]=(float)b;Tint[t+3]=0;Slot[Count]=slot;Count++;
        }
        /// <summary>A draw at translation t, rotation q, scaled along the prop's own axes (a unit crystal, rock or shard at its
        /// size).</summary>
        public void AddScaled(int slot,double tx,double ty,double tz,in FactoryQ q,double sx,double sy,double sz,double r=1,double g=1,double b=1)
        {
            if(Count>=Capacity)return;int o=Count*12;Add(slot,tx,ty,tz,q,sy,r,g,b);
            Place[o]*=(float)sx;Place[o+4]*=(float)sx;Place[o+8]*=(float)sx;Place[o+2]*=(float)sz;Place[o+6]*=(float)sz;Place[o+10]*=(float)sz;
        }
        /// <summary>A prop of unit length along its y (a chain, a rope) laid from one point to another.</summary>
        public void Span(int slot,double x0,double y0,double z0,double x1,double y1,double z1)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-4)return;
            Add(slot,x0,y0,z0,FromTo(0,1,0,dx/l,dy/l,dz/l),l);
        }
        /// <summary>The shortest turn taking unit a to unit b.</summary>
        public static FactoryQ FromTo(double ax,double ay,double az,double bx,double by,double bz)
        {
            double d=ax*bx+ay*by+az*bz;if(d>.999999)return FactoryQ.Identity;if(d<-.999999)return FactoryQ.Axis(1,0,0,Math.PI);
            double cx=ay*bz-az*by,cy=az*bx-ax*bz,cz=ax*by-ay*bx,w=1+d,n=Math.Sqrt(cx*cx+cy*cy+cz*cz+w*w);return new FactoryQ{X=cx/n,Y=cy/n,Z=cz/n,W=w/n};
        }
    }
}
