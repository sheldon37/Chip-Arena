using System;
using System.Threading.Tasks;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The City Ironworks' molten light field (docs/EXPEDITION-CITY-IRONWORKS.md section 4, gap 4, and 9.2: "beyond 8, bake the
    /// molten glows into a field (NativeWardZeroField's method) rather than adding lights"). The works has dozens of glowing
    /// sources (the furnace mouths, the tapholes and the length of every runner, the coke doors' seams, the converters' mouths,
    /// the pit, the slag bank, the pig bed and the cooling bed); this bakes every glow the kit marks with a field channel, once
    /// per map, into the volume NativeBlockLit, NativeMolten and the source models already sample (Resources/NativeWardLight.cginc
    /// WardField: the same layout as NativeWardZeroField, so NativeLighting.WardField sends it unchanged):
    /// <see cref="Nx"/> x <see cref="Nz"/> cells of <see cref="NativeWardZeroField.Cell"/> m, the eight slices at
    /// NativeWardZeroField.SliceY (four under the ring's decks, four over them: the ring is at 6 m, the field's split at 5.8 m),
    /// RGBA = four channels: 0 the steady glows, 1 to 3 the three runners (each runner's level follows its hazard view).
    /// Each texel sums its sources' light with the practical lights' fall-off ((1 - (d/r)^2)^2, so heat stays local) where the
    /// straight line to the source is clear of the blocks (a wall keeps a furnace's light in its hall); texels inside blocks take
    /// their open neighbours' light (a wall face reads the room in front of it). The shader multiplies by each channel's level
    /// (<see cref="Levels"/>) and the field's colour (<see cref="Colour"/>: molten orange). Rows bake in parallel (each texel is
    /// its own sum). Pure: the same kit and blocks give the same field; Levels allocates nothing.
    /// </summary>
    public sealed class NativeIronworksField
    {
        public const int Channels=4;
        /// <summary>The field's linear colour (molten orange) and its gain (the owner can turn it down as Ward Zero's was).</summary>
        public const float Gain=.26f,ColourR=1f,ColourG=.5f,ColourB=.22f;
        /// <summary>A plugged runner's level with no hazard clock (normal play today): the crusted trough's residual warmth on the floor
        /// round it, well under a live run's 1 so it never reads as one.</summary>
        public const float Residual=.3f;
        /// <summary>The vertical voxel size of the blocking grid (m) and its height (the walls' tops are at 14 m).</summary>
        public const float Voxel=.5f,Height=15f;
        public int Nx,Nz,Sources;public float MinX,MinZ;public float[] Data=Array.Empty<float>();public double BakeMs;

        /// <summary>Bakes the field for the kit's glows over the map's blocks inside (x0..x1, z0..z1).</summary>
        public static NativeIronworksField Build(NativeIronworksKit kit,ContentBlock[] blocks,float x0,float x1,float z0,float z1)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();float cell=NativeWardZeroField.Cell;
            var f=new NativeIronworksField{MinX=x0,MinZ=z0,Nx=Math.Max(1,(int)Math.Ceiling((x1-x0)/cell)),Nz=Math.Max(1,(int)Math.Ceiling((z1-z0)/cell))};
            int slices=NativeWardZeroField.Slices,ny=(int)Math.Ceiling(Height/Voxel);
            // The blocking grid (cells a block fills, by its world AABB; open grating and thin rails stay open).
            var solid=new bool[f.Nx*f.Nz*ny];
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b?.id==null||b.size.y<.35f&&b.pos.y>1||b.size.x<.12f||b.size.z<.12f||b.id.Contains("-rail-"))continue;
                double bx0=b.pos.x-b.size.x/2,bx1=b.pos.x+b.size.x/2,by0=b.pos.y-b.size.y/2,by1=b.pos.y+b.size.y/2,bz0=b.pos.z-b.size.z/2,bz1=b.pos.z+b.size.z/2;
                int ia=Math.Max(0,(int)Math.Ceiling((bx0-x0)/cell-.5)),ib=Math.Min(f.Nx-1,(int)Math.Floor((bx1-x0)/cell-.5)),ka=Math.Max(0,(int)Math.Ceiling((bz0-z0)/cell-.5)),kb=Math.Min(f.Nz-1,(int)Math.Floor((bz1-z0)/cell-.5));
                int ja=Math.Max(0,(int)Math.Ceiling(by0/Voxel-.5)),jb=Math.Min(ny-1,(int)Math.Floor(by1/Voxel-.5));
                for(int j=ja;j<=jb;j++)for(int k=ka;k<=kb;k++)for(int i=ia;i<=ib;i++)solid[(j*f.Nz+k)*f.Nx+i]=true;
            }
            var src=new System.Collections.Generic.List<NativeIronGlow>();foreach(var g in kit?.Glows??Array.Empty<NativeIronGlow>())if(g.Channel>=0&&g.Channel<Channels&&g.Frame==NativeIronworksKit.FrameStatic)src.Add(g);
            var sources=src.ToArray();f.Sources=sources.Length;var data=new float[f.Nx*f.Nz*slices*Channels];int nx=f.Nx,nz=f.Nz;
            bool Solid(double x,double y,double z){int i=(int)Math.Floor((x-x0)/cell),k=(int)Math.Floor((z-z0)/cell),j=(int)Math.Floor(y/Voxel);if(i<0||k<0||j<0||i>=nx||k>=nz||j>=ny)return false;return solid[(j*nz+k)*nx+i];}
            // A clear line from the source to the point (the last .4 m at either end is not tested: a source sits on its surface).
            bool Clear(double ax,double ay,double az,double bx,double by,double bz)
            {
                double dx=bx-ax,dy=by-ay,dz=bz-az,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<.9)return true;int n=(int)Math.Ceiling(l/(cell*.5));
                for(int s=1;s<n;s++){double t=s/(double)n;if(t*l<.4||(1-t)*l<.4)continue;if(Solid(ax+dx*t,ay+dy*t,az+dz*t))return false;}return true;
            }
            Parallel.For(0,nz,k=>
            {
                double z=z0+(k+.5)*cell;
                for(int i=0;i<nx;i++)
                {
                    double x=x0+(i+.5)*cell;
                    for(int s=0;s<slices;s++)
                    {
                        double y=NativeWardZeroField.SliceY[s];int o=((s*nz+k)*nx+i)*Channels;
                        foreach(var g in sources)
                        {
                            double dx=g.X-x,dz=g.Z-z;if(Math.Abs(dx)>g.Range||Math.Abs(dz)>g.Range)continue;double dy=g.Y-y,d2=dx*dx+dy*dy+dz*dz,r2=g.Range*(double)g.Range;if(d2>=r2)continue;
                            // A source under the ring lights the ring's floor only through its open grating (half), and the reverse; the
                            // bays' work lights hang over open floor and light it fully.
                            double q=1-d2/r2,a=q*q;if(g.Y<5.8!=y<5.8&&g.Kind!=NativeIronworksKit.GlowFill)a*=.35;if(a<.002||!Clear(g.X,g.Y,g.Z,x,y,z))continue;
                            data[o+g.Channel]+=(float)(a*(g.R*.55+g.G*.35+g.B*.1));
                        }
                    }
                }
            });
            // Texels inside blocks take the brightest of their open neighbours (two passes), so a face reads the room before it.
            for(int pass=0;pass<2;pass++)for(int s=0;s<slices;s++)
            {
                int j=(int)Math.Floor(NativeWardZeroField.SliceY[s]/Voxel);var copy=new float[nx*nz*Channels];Array.Copy(data,s*nx*nz*Channels,copy,0,copy.Length);
                for(int k=0;k<nz;k++)for(int i=0;i<nx;i++)
                {
                    if(!solid[(j*nz+k)*nx+i])continue;int o=((s*nz+k)*nx+i)*Channels;
                    for(int dk=-1;dk<=1;dk++)for(int di=-1;di<=1;di++){int ii=i+di,kk=k+dk;if(ii<0||kk<0||ii>=nx||kk>=nz||solid[(j*nz+kk)*nx+ii]&&pass==0)continue;int p=(kk*nx+ii)*Channels;for(int c=0;c<Channels;c++)data[o+c]=Math.Max(data[o+c],copy[p+c]);}
                }
            }
            f.Data=data;f.BakeMs=watch.Elapsed.TotalMilliseconds;return f;
        }
        /// <summary>Each channel's level this frame into `into` (4): the steady glows' fire flicker, and each runner's state (dull while
        /// plugged, building in the warning, full while it runs, fading as it crusts over).</summary>
        public static void Levels(IronworksHazardView[] views,double time,float[] into,in NativeZoneBossBeat music=default)
        {
            // Channel 0 (the furnaces' glow and the baked work lights) follows the zone boss song; the runners' channels never.
            into[0]=Gain*(.94f+.06f*NativeIronworksKit.Level(NativeIronworksKit.DriveFire,.3,time*.6))*NativeIronworksKit.MusicField(music);
            for(int r=0;r<3;r++)
            {
                var v=views!=null&&r<views.Length?views[NativeIronworksHazards.R1+r]:default;
                float k=v.Phase==NativeHazardPhase.Active?1:v.Phase==NativeHazardPhase.Warn?.22f+.78f*v.Level*v.Level:v.Phase==NativeHazardPhase.Clear?.22f+.78f*v.Level:v.Rate==NativeIronworksHazards.RateOff?Residual:.18f;
                into[1+r]=Gain*k;
            }
        }
        public static void Colour(out float r,out float g,out float b){r=ColourR;g=ColourG;b=ColourB;}
    }
}
