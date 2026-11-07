using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The loading screen's stage scan: a holographic plan of the stage being loaded, from its content blocks (the same records
    /// the arena builds), with its spawn points and hazards. Each block's footprint is the top-down extent of its rotated box,
    /// normalised to the plan (u 0..1 west to east, v 0..1 north to south; Aspect is the plan's width over its depth), with
    /// its top height as a 0..1 level (brightness) and a kind: 0 a floor slab (drawn as the plate), 1 a block, 2 cover or a
    /// breakable/dynamic piece, 3 a deck (a thin raised floor, gallery or roof: drawn as a faint pane, so it never hides the
    /// rooms under it). Blocks are kept lowest first, so taller ones draw over; past Max, the smallest go first. Built
    /// once per map and cached; the arrays are read by the draw, never rebuilt per frame.
    /// </summary>
    public sealed class NativeStageSchematic
    {
        public const int Max=900;
        public const byte Plate=0,Solid=1,Cover=2,Deck=3;
        public readonly string Map;
        public readonly int Count;
        /// <summary>u, v, w, h per block (plan units 0..1).</summary>
        public readonly float[] Box;
        public readonly float[] Level;
        public readonly byte[] Kind;
        public readonly float Aspect;
        /// <summary>Spawn points (u, v pairs) and hazard centres (u, v pairs).</summary>
        public readonly float[] Spawns,Hazards;
        /// <summary>Every block with a footprint (Count may keep fewer) and its label, "616 BLOCKS".</summary>
        public readonly int Total;public readonly string Label;

        private NativeStageSchematic(string map,int total,float[] box,float[] level,byte[] kind,float aspect,float[] spawns,float[] hazards)
        {Map=map;Total=total;Count=level.Length;Box=box;Level=level;Kind=kind;Aspect=aspect;Spawns=spawns;Hazards=hazards;Label=total+NativeStageLabels.Blocks;}

        private static readonly Dictionary<ContentMap,NativeStageSchematic> cache=new Dictionary<ContentMap,NativeStageSchematic>();
        /// <summary>The cached scan of <paramref name="map"/> (null for a map with no blocks).</summary>
        public static NativeStageSchematic For(ContentMap map)
        {
            if(map==null)return null;if(cache.TryGetValue(map,out var s))return s;
            if(cache.Count>32)cache.Clear();s=Build(map);cache[map]=s;return s;
        }

        private struct Foot{public float X0,Z0,X1,Z1,Top,Height,Area;public byte Kind;}
        public static NativeStageSchematic Build(ContentMap map,int max=Max)
        {
            var blocks=map?.blocks;if(blocks==null||blocks.Length==0)return null;
            var feet=new List<Foot>(blocks.Length);float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            foreach(var b in blocks)
            {
                if(b==null)continue;var f=FootOf(b);if(!(f.X1>f.X0)||!(f.Z1>f.Z0))continue;
                feet.Add(f);minX=Math.Min(minX,f.X0);maxX=Math.Max(maxX,f.X1);minZ=Math.Min(minZ,f.Z0);maxZ=Math.Max(maxZ,f.Z1);
            }
            if(feet.Count==0)return null;
            float sx=Math.Max(.01f,maxX-minX),sz=Math.Max(.01f,maxZ-minZ),plan=sx*sz;
            // Floor slabs: wide and thin (an eighth of the plan or more, at most 2 m deep) draw as the plate under the rest.
            float floor=float.MaxValue;foreach(var f in feet)floor=Math.Min(floor,f.Top);
            for(int i=0;i<feet.Count;i++)
            {
                var f=feet[i];if(f.Kind==Cover)continue;
                if(f.Area>=plan*.08f&&f.Height<=2.01f)f.Kind=f.Top-f.Height<=floor+1?Plate:Deck;else if(f.Height<=.8f&&f.Top-f.Height>floor+1.5f)f.Kind=Deck;feet[i]=f;
            }
            int total=feet.Count;
            if(feet.Count>max){feet.Sort((a,b)=>b.Area.CompareTo(a.Area));feet.RemoveRange(max,feet.Count-max);}
            // Levels: from the lowest slab top to the 95th percentile top, so a lone mast does not flatten the rest.
            var tops=new float[feet.Count];for(int i=0;i<tops.Length;i++)tops[i]=feet[i].Top;Array.Sort(tops);
            float low=tops[0],high=tops[Math.Min(tops.Length-1,(int)(tops.Length*.95f))];if(high-low<.5f)high=low+.5f;
            feet.Sort((a,b)=>a.Kind==Plate&&b.Kind!=Plate?-1:b.Kind==Plate&&a.Kind!=Plate?1:a.Top.CompareTo(b.Top));
            int n=feet.Count;var box=new float[n*4];var level=new float[n];var kind=new byte[n];
            for(int i=0;i<n;i++)
            {
                var f=feet[i];box[i*4]=(f.X0-minX)/sx;box[i*4+1]=(maxZ-f.Z1)/sz;box[i*4+2]=(f.X1-f.X0)/sx;box[i*4+3]=(f.Z1-f.Z0)/sz;
                level[i]=Clamp01((f.Top-low)/(high-low));kind[i]=f.Kind;
            }
            var spawns=new List<float>();
            void Spawn(MapSpawn p){if(p==null)return;spawns.Add(Clamp01((p.pos.x-minX)/sx));spawns.Add(Clamp01((maxZ-p.pos.z)/sz));}
            if(map.spawns?.ffa!=null)foreach(var p in map.spawns.ffa)Spawn(p);
            if(map.spawns?.teams!=null)foreach(var t in map.spawns.teams)if(t?.points!=null)foreach(var p in t.points)Spawn(p);
            var hazards=new List<float>();
            if(map.hazards!=null)foreach(var h in map.hazards){if(h==null)continue;hazards.Add(Clamp01((h.center.x-minX)/sx));hazards.Add(Clamp01((maxZ-h.center.z)/sz));}
            return new NativeStageSchematic(map.id,total,box,level,kind,sx/sz,spawns.ToArray(),hazards.ToArray());
        }
        // The top-down extent of the block's rotated box: its eight corners turned by the block's rotation.
        private static Foot FootOf(ContentBlock b)
        {
            float hx=Math.Abs(b.size.x)/2,hy=Math.Abs(b.size.y)/2,hz=Math.Abs(b.size.z)/2;var r=b.rotation;float qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r?.w??1;
            float ql=(float)Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(ql>1e-6f){qx/=ql;qy/=ql;qz/=ql;qw/=ql;}else{qx=qy=qz=0;qw=1;}
            var f=new Foot{X0=float.MaxValue,Z0=float.MaxValue,X1=float.MinValue,Z1=float.MinValue,Top=float.MinValue};float bottom=float.MaxValue;
            for(int c=0;c<8;c++)
            {
                float x=(c&1)!=0?hx:-hx,y=(c&2)!=0?hy:-hy,z=(c&4)!=0?hz:-hz;Rotate(qx,qy,qz,qw,ref x,ref y,ref z);
                x+=b.pos.x;y+=b.pos.y;z+=b.pos.z;f.X0=Math.Min(f.X0,x);f.X1=Math.Max(f.X1,x);f.Z0=Math.Min(f.Z0,z);f.Z1=Math.Max(f.Z1,z);f.Top=Math.Max(f.Top,y);bottom=Math.Min(bottom,y);
            }
            f.Height=f.Top-bottom;f.Area=(f.X1-f.X0)*(f.Z1-f.Z0);
            f.Kind=b.dynamic||b.role=="cover"||b.role=="masonry"||b.role=="structural"||b.role=="carried"?Cover:Solid;return f;
        }
        private static void Rotate(float qx,float qy,float qz,float qw,ref float x,ref float y,ref float z)
        {
            // v' = v + 2w(q x v) + 2q x (q x v)
            float tx=2*(qy*z-qz*y),ty=2*(qz*x-qx*z),tz=2*(qx*y-qy*x);
            float rx=x+qw*tx+(qy*tz-qz*ty),ry=y+qw*ty+(qz*tx-qx*tz),rz=z+qw*tz+(qx*ty-qy*tx);x=rx;y=ry;z=rz;
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
    }
}
