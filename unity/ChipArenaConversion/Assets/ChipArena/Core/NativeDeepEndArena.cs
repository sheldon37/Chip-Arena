using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE DEEP END's room, the natatorium (docs/EXPEDITION-BOSS-BACKROOMS.md section 4 "The arena"): 20 x 20 cells (40 m square), the ceiling at 7.2 m, no pillars, no chairs, no boards. A
    /// central deep pool 10 x 10 cells with steps on all four sides, and in its middle a 4 x 4 abyss where the floor falls away into black. Round lights in the ceiling, and lights under the
    /// water that stay on when the rest die. <see cref="Carve"/> is pure, used by the DEV preview and later by Astra's generator. Places are relative to the room's centre (the abyss's middle
    /// on the deck's level); <see cref="Centre"/> gives it on a layout.
    /// </summary>
    public static class NativeDeepEndArena
    {
        public const int Cells=20,PoolCells=10,AbyssCells=4;public const float Ceiling=7.2f,Half=Cells*PoolroomsLayout.Cell*.5f,PoolHalf=PoolCells*PoolroomsLayout.Cell*.5f,AbyssHalf=AbyssCells*PoolroomsLayout.Cell*.5f;
        public const int RoomId=9000;
        /// <summary>The deep pool's level, the deepest the abyss's walls go (m, from the deck).</summary>
        public const float PoolFloor=-PoolroomsLayout.DeepDepth;
        /// <summary>A copy of `source` with the room carved round cell (cx, cz) (its centre on that cell's corner; clamped so it fits). Walls seal it; the rest of the layout is left as it was.</summary>
        public static PoolroomsLayout Carve(PoolroomsLayout source,int cx,int cz)
        {
            var l=new PoolroomsLayout(source.Seed,source.Width,source.Depth);
            Array.Copy(source.Floor,l.Floor,l.Floor.Length);Array.Copy(source.StepDir,l.StepDir,l.StepDir.Length);Array.Copy(source.Ceiling,l.Ceiling,l.Ceiling.Length);Array.Copy(source.Pillar,l.Pillar,l.Pillar.Length);Array.Copy(source.Light,l.Light,l.Light.Length);Array.Copy(source.Room,l.Room,l.Room.Length);
            Array.Copy(source.WallX,l.WallX,l.WallX.Length);Array.Copy(source.WallZ,l.WallZ,l.WallZ.Length);l.EntryX=source.EntryX;l.EntryZ=source.EntryZ;
            int half=Cells/2;cx=Math.Max(half,Math.Min(l.Width-half,cx));cz=Math.Max(half,Math.Min(l.Depth-half,cz));int x0=cx-half,z0=cz-half,x1=Math.Min(l.Width,cx+half),z1=Math.Min(l.Depth,cz+half);
            int ph=PoolCells/2,ah=AbyssCells/2;
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                int i=l.Index(x,z);l.Room[i]=RoomId;l.Ceiling[i]=Ceiling;l.Pillar[i]=false;l.Floor[i]=PoolroomsLayout.Deck;l.StepDir[i]=0;
                int rx=x-x0,rz=z-z0;l.Light[i]=rx%3==1&&rz%3==1;
                int dx=x-cx,dz=z-cz;bool pool=dx>=-ph&&dx<ph&&dz>=-ph&&dz<ph,abyss=dx>=-ah&&dx<ah&&dz>=-ah&&dz<ah;
                if(pool)l.Floor[i]=abyss?PoolroomsLayout.Abyss:PoolroomsLayout.Deep;
                // Open inside, walled round the outside.
                l.Set(x,z,1,0,x+1<x1?PoolroomsLayout.Open:PoolroomsLayout.Wall);l.Set(x,z,0,1,z+1<z1?PoolroomsLayout.Open:PoolroomsLayout.Wall);
                if(x==x0)l.Set(x,z,-1,0,PoolroomsLayout.Wall);if(z==z0)l.Set(x,z,0,-1,PoolroomsLayout.Wall);
            }
            // The four flights: two cells wide, down from the deck in the pool's outer row at each side's middle, facing the pit.
            for(int k=-1;k<=0;k++)
            {
                Step(l,cx+k,cz-ph,PoolroomsLayout.DownPosZ);Step(l,cx+k,cz+ph-1,PoolroomsLayout.DownNegZ);Step(l,cx-ph,cz+k,PoolroomsLayout.DownPosX);Step(l,cx+ph-1,cz+k,PoolroomsLayout.DownNegX);
            }
            return l;
        }
        private static void Step(PoolroomsLayout l,int x,int z,byte dir){if(!l.Inside(x,z))return;int i=l.Index(x,z);l.Floor[i]=PoolroomsLayout.Steps;l.StepDir[i]=dir;}
        /// <summary>The room's centre for a carve at (cx, cz) on layout `l` laid at `origin` (world x, z).</summary>
        public static Vector3 Centre(PoolroomsLayout l,Vector3 origin,int cx,int cz){int half=Cells/2;cx=Math.Max(half,Math.Min(l.Width-half,cx));cz=Math.Max(half,Math.Min(l.Depth-half,cz));return origin+new Vector3(cx*PoolroomsLayout.Cell,0,cz*PoolroomsLayout.Cell);}
        /// <summary>Whether (x, z), from the room's centre, is in the room, over the deep pool, over the abyss.</summary>
        public static bool InRoom(float x,float z)=>Math.Abs(x)<Half&&Math.Abs(z)<Half;
        public static bool InPool(float x,float z)=>Math.Abs(x)<PoolHalf&&Math.Abs(z)<PoolHalf;
        public static bool InAbyss(float x,float z)=>Math.Abs(x)<AbyssHalf&&Math.Abs(z)<AbyssHalf;
        /// <summary>The floor's height at (x, z) from the room's centre (the abyss's far below; the steps ramped).</summary>
        public static float Floor(float x,float z)
        {
            if(InAbyss(x,z))return PoolroomsLayout.AbyssFloor;if(!InPool(x,z))return 0;float ax=Math.Abs(x),az=Math.Abs(z);
            if(Math.Min(ax,az)<2&&Math.Max(ax,az)>PoolHalf-2)return -PoolroomsLayout.DeepDepth*Clamp01((PoolHalf-Math.Max(ax,az))/2);
            return PoolFloor;
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;

        // ---- the lights under the water ----
        /// <summary>One round light under the water: its place from the room's centre, the way it faces, its size, and when it goes out in the intro (0 first .. 1 last: from the far end toward the pit).</summary>
        public struct WaterLamp{public Vector3 At,Facing;public float Size,Order;public byte Kind;}
        public const byte PoolWall=0,AbyssWall=1,RoomWall=2;
        public static readonly WaterLamp[] Lamps=MakeLamps();
        private static WaterLamp[] MakeLamps()
        {
            var a=new System.Collections.Generic.List<WaterLamp>();
            // The deep pool's walls, every two cells, clear of the steps: y under the level's water.
            for(int k=-4;k<=4;k+=2)
            {
                float m=k*PoolroomsLayout.Cell+0;if(Math.Abs(m)<3)continue;
                foreach(int side in new[]{-1,1})
                {
                    float wall=side*(PoolHalf-.02f);
                    a.Add(new WaterLamp{At=new Vector3(m,-.72f,wall),Facing=new Vector3(0,0,-side),Size=.3f,Kind=PoolWall});
                    a.Add(new WaterLamp{At=new Vector3(wall,-.72f,m),Facing=new Vector3(-side,0,0),Size=.3f,Kind=PoolWall});
                }
            }
            // The abyss's shaft: rings going down, each dimmer by the water over it.
            float[] depths={-2.1f,-5.2f,-9f,-14f,-20f,-27f};
            foreach(float y in depths)for(int k=-1;k<=1;k+=2)foreach(int side in new[]{-1,1})
            {
                float wall=side*(AbyssHalf-.02f),m=k*AbyssHalf*.5f;
                a.Add(new WaterLamp{At=new Vector3(m,y,wall),Facing=new Vector3(0,0,-side),Size=.26f,Kind=AbyssWall});
                a.Add(new WaterLamp{At=new Vector3(wall,y,m),Facing=new Vector3(-side,0,0),Size=.26f,Kind=AbyssWall});
            }
            // The room's walls, near the floor: dry until the tide rises over them, then the room's own light.
            for(int k=-8;k<=8;k+=4)foreach(int side in new[]{-1,1})
            {
                float wall=side*(Half-PoolroomsLayout.Thickness*.5f-.02f),m=(k+.5f)*PoolroomsLayout.Cell*.5f;
                a.Add(new WaterLamp{At=new Vector3(m,.42f,wall),Facing=new Vector3(0,0,-side),Size=.26f,Kind=RoomWall});
                a.Add(new WaterLamp{At=new Vector3(wall,.42f,m),Facing=new Vector3(-side,0,0),Size=.26f,Kind=RoomWall});
            }
            var r=a.ToArray();float lo=float.MaxValue,hi=float.MinValue;
            // They go out in order of the path down: along the far (+z) side first, then round toward the camera's end, then the shaft going down.
            float Key(WaterLamp l)=>l.Kind==AbyssWall?60-l.At.Y:-l.At.Z+Math.Abs(l.At.X)*.15f+(l.Kind==RoomWall?-.5f:0);
            foreach(var l in r){float k=Key(l);lo=Math.Min(lo,k);hi=Math.Max(hi,k);}
            for(int i=0;i<r.Length;i++)r[i].Order=(Key(r[i])-lo)/Math.Max(.001f,hi-lo);
            return r;
        }
    }
}
