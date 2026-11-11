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
            (NativeDeepwellLooks.MapId,NativeDeepwellArt.Bank)};
        public static string BankOf(string mapId){if(mapId==null)return null;foreach(var b in Banks)if(b.map==mapId)return b.bank;return null;}
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
