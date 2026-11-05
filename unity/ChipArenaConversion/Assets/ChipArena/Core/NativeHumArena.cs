using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM's room (docs/EXPEDITION-BOSS-BACKROOMS.md section 3 "The arena"): twelve by twelve cells of a Backrooms layout (38.4 m square) under the 4 m
    /// ceiling, no pillars, no doors (they are papered over as it arrives), fixtures every other cell in the level's own grid on eight circuits, and eight
    /// free-standing partition segments that move when nobody is looking. Pure: <see cref="Carve"/> is what Astra's generator would call round the exit, and what the
    /// DEV preview draws in place of the map's own layout. The partitions are not part of the carved walls: they move, so the presentation draws them from
    /// <see cref="HumRoom"/>, and Astra's colliders should read the same table.
    /// </summary>
    public sealed class HumRoom
    {
        public readonly int X0,Z0;public readonly Vector3 Origin;
        /// <summary>The room's world bounds and centre.</summary>
        public readonly float MinX,MinZ,MaxX,MaxZ;public Vector3 Centre=>new Vector3((MinX+MaxX)*.5f,Origin.Y,(MinZ+MaxZ)*.5f);
        public HumRoom(int x0,int z0,Vector3 origin)
        {
            X0=x0;Z0=z0;Origin=origin;float c=BackroomsLayout.Cell;MinX=origin.X+x0*c;MinZ=origin.Z+z0*c;MaxX=MinX+NativeHumArena.Cells*c;MaxZ=MinZ+NativeHumArena.Cells*c;
            for(int i=0;i<NativeHumArena.FixtureCount;i++)
            {
                int col=i%NativeHumArena.FixtureSide,row=i/NativeHumArena.FixtureSide;
                FixtureAt[i]=new Vector3(MinX+(col*2+.5f)*c,origin.Y+BackroomsLayout.Height-BackroomsMesh.FixtureDrop,MinZ+(row*2+.5f)*c);
            }
        }
        /// <summary>Each fixture's centre (row r, column c at r*side+c; fixtures hang over the even cells of the room).</summary>
        public readonly Vector3[] FixtureAt=new Vector3[NativeHumArena.FixtureCount];
        /// <summary>A partition segment's centre and axis in one of its two places.</summary>
        public void Partition(int segment,int place,out Vector3 centre,out bool alongX)
        {
            NativeHumArena.Place(segment,place,out float cx,out float cz,out alongX);float c=BackroomsLayout.Cell;
            centre=new Vector3(MinX+cx*c,Origin.Y,MinZ+cz*c);
        }
        /// <summary>Whether a world point is inside the room.</summary>
        public bool Inside(float x,float z)=>x>=MinX&&x<=MaxX&&z>=MinZ&&z<=MaxZ;
    }

    public static class NativeHumArena
    {
        public const int Cells=12,FixtureSide=6,FixtureCount=FixtureSide*FixtureSide,Circuits=8,Partitions=8,PartitionCells=2;
        public const float Size=Cells*BackroomsLayout.Cell;
        /// <summary>The intro: how far behind the boss's end pose the wall it comes through lies (m) and which wall: the north (z max).</summary>
        public const float IntroWallBehind=7;
        /// <summary>The boss's place when the fight starts: facing south (the party's side) with the north wall <see cref="IntroWallBehind"/> behind it.</summary>
        public static void Spawn(HumRoom room,out Vector3 root,out float yawDegrees){root=new Vector3((room.MinX+room.MaxX)*.5f,room.Origin.Y,room.MaxZ-IntroWallBehind);yawDegrees=180;}
        /// <summary>The circuit a fixture is on (blocks of two by two fixtures, the ninth block sharing the eighth's breaker).</summary>
        public static int CircuitOf(int fixture){int col=fixture%FixtureSide,row=fixture/FixtureSide;int b=col/2+row/2*3;return b>=Circuits?Circuits-1:b;}
        /// <summary>The partitions' two places (room cells, along x or z): layout 0 is the first place of each, and a shift moves a segment to its other one. Each is two cells
        /// long, on a cell line, at least two cells from any other's run and from the outer wall, so no mix of places can seal a pocket (the checks flood every mix).</summary>
        private static readonly (float x,float z,bool alongX,float x2,float z2,bool alongX2)[] Table={
            (3,3,true, 2,5,false),   (9,3,true, 10,5,false),
            (3,9,true, 2,7,false),   (9,9,true, 10,7,false),
            (6,4,false, 5,2,true),   (6,8,false, 7,10,true),
            (4,6,true, 5,10,true),   (8,6,true, 7,2,true)};
        public static void Place(int segment,int place,out float cellX,out float cellZ,out bool alongX)
        {
            var t=Table[segment];if(place==0){cellX=t.x;cellZ=t.z;alongX=t.alongX;}else{cellX=t.x2;cellZ=t.z2;alongX=t.alongX2;}
        }
        /// <summary>Which place a segment is in for a shift mask (a bit per segment: the record's shiftMask).</summary>
        public static int PlaceOf(int segment,int mask)=>(mask>>segment)&1;

        /// <summary>
        /// Carves the room into `l` round the cell (cx, cz), clamped to fit: every edge inside it open, the border walled (any door papered over), no pillars and no pit, fixtures on its
        /// own grid and circuits. Returns the room (at `origin`, the layout's corner cell in the world).
        /// </summary>
        public static HumRoom Carve(BackroomsLayout l,int cx,int cz,Vector3 origin=default)
        {
            int x0=Math.Max(0,Math.Min(l.Width-Cells,cx-Cells/2)),z0=Math.Max(0,Math.Min(l.Depth-Cells,cz-Cells/2));
            for(int z=z0;z<z0+Cells;z++)for(int x=x0;x<x0+Cells;x++)
            {
                int i=l.Index(x,z);l.Pillar[i]=BackroomsLayout.NoPillar;l.Floor[i]=BackroomsLayout.Carpet;
                bool lit=((x-x0)&1)==0&&((z-z0)&1)==0;l.Light[i]=lit;
                l.Circuit[i]=lit?(byte)(240+CircuitOf((z-z0)/2*FixtureSide+(x-x0)/2)):(byte)240;
                if(x+1<x0+Cells)l.Set(x,z,1,0,BackroomsLayout.Open);if(z+1<z0+Cells)l.Set(x,z,0,1,BackroomsLayout.Open);
            }
            for(int i=0;i<Circuits;i++)l.Circuits[240+i]=BackroomsLayout.On;
            for(int k=0;k<Cells;k++)
            {
                if(l.Inside(x0-1,z0+k)||x0==0)l.WallX[(l.Width+1)*(z0+k)+x0]=BackroomsLayout.Wall;l.WallX[(l.Width+1)*(z0+k)+x0+Cells]=BackroomsLayout.Wall;
                l.WallZ[l.Width*z0+x0+k]=BackroomsLayout.Wall;l.WallZ[l.Width*(z0+Cells)+x0+k]=BackroomsLayout.Wall;
            }
            return new HumRoom(x0,z0,origin);
        }
        /// <summary>The presentation's copy of a layout (the live generator's or the map's): the room carved, and its fixtures left out: the presenter draws them one by one, each at its own level.
        /// `l` itself is untouched. The room's own lights are in <see cref="HumRoom.FixtureAt"/>.</summary>
        public static BackroomsLayout PresentationCopy(BackroomsLayout l,int cx,int cz,Vector3 origin,out HumRoom room)
        {
            var c=new BackroomsLayout(l.Level,l.Seed,l.Width,l.Depth);
            Array.Copy(l.Floor,c.Floor,l.Floor.Length);Array.Copy(l.Pillar,c.Pillar,l.Pillar.Length);Array.Copy(l.Circuit,c.Circuit,l.Circuit.Length);Array.Copy(l.Light,c.Light,l.Light.Length);
            Array.Copy(l.WallX,c.WallX,l.WallX.Length);Array.Copy(l.WallZ,c.WallZ,l.WallZ.Length);Array.Copy(l.Circuits,c.Circuits,l.Circuits.Length);c.EntryX=l.EntryX;c.EntryZ=l.EntryZ;c.ExitX=l.ExitX;c.ExitZ=l.ExitZ;
            room=Carve(c,cx,cz,origin);
            for(int z=room.Z0;z<room.Z0+Cells;z++)for(int x=room.X0;x<room.X0+Cells;x++)c.Light[c.Index(x,z)]=false;
            return c;
        }
        /// <summary>The cells a set of partition places walls off in `l` (for the flood check and Astra's nav): sets those edges to Wall in a copy of the room's edges.</summary>
        public static void WallPartitions(BackroomsLayout l,HumRoom room,int mask)
        {
            for(int s=0;s<Partitions;s++)
            {
                Place(s,PlaceOf(s,mask),out float cx,out float cz,out bool alongX);int x=room.X0+(int)Math.Floor(cx),z=room.Z0+(int)Math.Floor(cz);
                for(int k=-1;k<PartitionCells-1;k++)
                {
                    if(alongX){int cell=x+k;if(l.Inside(cell,z)&&l.Inside(cell,z-1))l.Set(cell,z-1,0,1,BackroomsLayout.Wall);}
                    else{int cell=z+k;if(l.Inside(x,cell)&&l.Inside(x-1,cell))l.Set(x-1,cell,1,0,BackroomsLayout.Wall);}
                }
            }
        }
        /// <summary>A directed camera's eye pushed forward past any partition between it and what it looks at.</summary>
        public static void PushEye(HumRoom room,int mask,ref Vector3 eye,Vector3 look)
        {
            float half=PartitionCells*BackroomsLayout.Cell*.5f+.12f;
            for(int s=0;s<Partitions;s++)
            {
                room.Partition(s,PlaceOf(s,mask),out var c,out bool alongX);var d=look-eye;float t;
                if(alongX){if(Math.Abs(d.Z)<1e-4f)continue;t=(c.Z-eye.Z)/d.Z;if(t<0||t>1)continue;float x=eye.X+d.X*t;if(Math.Abs(x-c.X)>half)continue;}
                else{if(Math.Abs(d.X)<1e-4f)continue;t=(c.X-eye.X)/d.X;if(t<0||t>1)continue;float z=eye.Z+d.Z*t;if(Math.Abs(z-c.Z)>half)continue;}
                float pushed=Math.Min(.92f,t+.45f/Math.Max(.5f,d.Length()));float y=eye.Y;eye=eye+d*pushed;eye.Y=y;
            }
        }
        /// <summary>A lane's fixtures: row r of an axis: axis 0 is a row of fixtures along x at the same z, axis 1 along z at the same x.</summary>
        public static int RowOf(int fixture,int axis)=>axis==0?fixture/FixtureSide:fixture%FixtureSide;
        /// <summary>The world line of row `row` on an axis (z for axis 0, x for axis 1).</summary>
        public static float RowLine(HumRoom room,int axis,int row)=>(axis==0?room.MinZ:room.MinX)+(row*2+.5f)*BackroomsLayout.Cell;
        /// <summary>The nearest point on any wall of the room or partition (in place set `mask`) to (x, z), its outward normal, and how far.</summary>
        public static float NearestWall(HumRoom room,int mask,float x,float z,out float wx,out float wz,out float nx,out float nz)
        {
            float best=float.MaxValue;float bx=x,bz=z,bnx=0,bnz=1;
            void Try(float px,float pz,float ax,float az)
            {
                float dx=x-px,dz=z-pz,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d>=best)return;best=d;bx=px;bz=pz;
                float s=ax*dz-az*dx;bnx=s>=0?-az:az;bnz=s>=0?ax:-ax;
            }
            Try(room.MinX,BossMath.Clamp(z,room.MinZ,room.MaxZ),0,1);Try(room.MaxX,BossMath.Clamp(z,room.MinZ,room.MaxZ),0,1);Try(BossMath.Clamp(x,room.MinX,room.MaxX),room.MinZ,1,0);Try(BossMath.Clamp(x,room.MinX,room.MaxX),room.MaxZ,1,0);
            float half=PartitionCells*BackroomsLayout.Cell*.5f;
            for(int s=0;s<Partitions;s++)
            {
                room.Partition(s,PlaceOf(s,mask),out var c,out bool alongX);
                if(alongX)Try(BossMath.Clamp(x,c.X-half,c.X+half),c.Z,1,0);else Try(c.X,BossMath.Clamp(z,c.Z-half,c.Z+half),0,1);
            }
            wx=bx;wz=bz;nx=bnx;nz=bnz;return best;
        }
    }
}
