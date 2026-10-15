using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Ward Zero's ceiling light field (owner, 2026-10-15: dimmer, lit by its own ceiling lights). NativeBlockLit has eight
    /// practical slots; the ward has about three hundred troffers and high-bays. This bakes every one of them, once per map,
    /// into a small volume the shader samples (Resources/NativeWardLight.cginc WardField): a pool on the floor under each
    /// fixture with a soft edge, a weak spill round it, darker gaps between, walls and floors that stand between a fixture
    /// and a point shading it (so a hall's light stays in the hall), the pools' bounce off the white tile spreading through
    /// the open space round them, and a faint floor everywhere one can stand (<see cref="Floor"/>). The slots are then left
    /// to what moves: alarms, beacons, furnaces, tanks, the ink and the Spine (NativeWardZeroLights with `field` true).
    /// <para>Layout: <see cref="Nx"/> x <see cref="Nz"/> cells of <see cref="Cell"/> m from (<see cref="MinX"/>, <see cref="MinZ"/>),
    /// <see cref="Slices"/> heights (<see cref="SliceY"/>: four through the ground floor under the decks, four through the upper
    /// floor), RGBA per texel = the light of the four lighting circuits (<see cref="Circuit"/>), x fastest, then z, then the
    /// slice (Unity's Texture3D order). A texel is the fixtures' light reaching that point (no surface term); the shader adds
    /// the surface's facing (<see cref="Facing"/>) and multiplies by each circuit's level this frame (<see cref="Levels"/>: the
    /// mood's fluorescent level, the emergency circuits and the brownouts) and the colour (<see cref="Colour"/>: clinical
    /// white, red in an emergency). The bake runs its rows on worker threads (each texel is its own sum, so the result never
    /// depends on the order).</para>
    /// Pure: the same kit and blocks give the same field (the bake is the only allocation); Levels, Colour and Sample allocate
    /// nothing.
    /// </summary>
    public sealed class NativeWardZeroField
    {
        public const float Cell=.5f;public const int Slices=8,Circuits=4;
        /// <summary>The slices' heights (m): the ground floor's four under its 5.6 m ceiling, the upper floor's four under the roof.</summary>
        public static readonly float[] SliceY={.25f,1.85f,3.45f,5.05f,6.25f,8.35f,10.45f,12.55f};
        /// <summary>A surface below <see cref="Split"/> reads the ground floor's slices, above it the upper floor's (never a mix
        /// across the deck). A wall reads the field <see cref="Offset"/> m (a cell) out along its normal, so it reads the room in
        /// front of it and never the wall itself or the room behind it; a floor or a ceiling reads it <see cref="Offset"/> times
        /// <see cref="FlatOffset"/> out (<see cref="OffsetFor"/>).</summary>
        public const float Split=5.8f,Offset=.5f,FlatOffset=.3f;
        /// <summary>How far out along its normal (y component `ny`) a surface reads the field.</summary>
        public static float OffsetFor(float ny)=>Offset*(1-(1-FlatOffset)*Math.Abs(ny));
        /// <summary>The share a wall (<see cref="Side"/>) and a ceiling (<see cref="Down"/>) take of a floor's light.</summary>
        public const float Side=.55f,Down=.2f;
        /// <summary>A fixture's pool on its floor: full inside <see cref="PoolIn"/> times its height over the floor of the point
        /// under it, gone by <see cref="PoolOut"/> times it (a troffer 5.5 m up: 0.55 m and 1.8 m; one 6.9 m up: 0.7 m and 2.3 m); a
        /// high-bay's is <see cref="BayScale"/> times wider. <see cref="Spill"/>: the weak light round it.</summary>
        public const float PoolIn=.1f,PoolOut=.33f,BayScale=1f,Spill=.05f;
        /// <summary>Reach (times the fixture's height over its floor) and strength of a troffer and of a high-bay.</summary>
        public const float TrofferReach=1.55f,BayReach=1.3f,TrofferPower=1f,BayPower=.9f;
        /// <summary>The field's colour: the troffers' white times <see cref="Gain"/> (linear).</summary>
        public const float Gain=.34f;
        /// <summary>The light the pools throw back off the floor (white tile): each floor's pools spread through the open space
        /// round them (<see cref="BounceSteps"/> passes of a neighbour average that walls stop, so it turns through doorways
        /// and never through a wall) and <see cref="Bounce"/> of it lights every height of that floor: the walls and ceiling
        /// beside a pool, the gaps between pools.</summary>
        public const float Bounce=.35f;public const int BounceSteps=160;
        /// <summary>A fixture's light below this at a texel is left out (a pool's centre is about 1).</summary>
        public const float MinLight=.002f;
        /// <summary>The faint light every open texel keeps (exit signs, status lamps, the glow off the white tile), shared by the
        /// four circuits: nowhere a pilot can stand is black.</summary>
        public const float Floor=.095f;
        /// <summary>How many cells into a thick block the light round it is carried (4 m).</summary>
        public const int DilatePasses=8;
        public int Nx,Nz,Fixtures;public float MinX,MinZ;public float[] Data=Array.Empty<float>();public double BakeMs;

        /// <summary>The lighting circuit a fixture is on (0..3), from its seed (the kit's Param).</summary>
        public static int Circuit(float seed){double v=seed*7.123+.31;return (int)Math.Floor((v-Math.Floor(v))*Circuits)&3;}
        /// <summary>A circuit's brownout this frame (1: steady): in the failing moods (flicker above .15) each circuit drops now
        /// and then for a moment, all its fixtures and their pools together (the kit's FluorLevel takes the same factor).</summary>
        public static float Brownout(int circuit,in NativeWardMood m,double time)
        {
            float chance=(m.Flicker-.15f)*.35f;if(chance<=0)return 1;double rate=1.3+.37*circuit,k=Math.Floor(time*rate+circuit*.37);
            if(Hash01((int)k,77+circuit)>=chance)return 1;return .12f+.3f*(float)Hash01((int)k,91+circuit);
        }
        /// <summary>Each circuit's level this frame (the mood's fluorescent level, the emergency circuits that stay on in an
        /// emergency, the beat's lift, the brownouts) into `into` (4).</summary>
        public static void Levels(in NativeWardMood m,double time,float[] into){float b=(m.Fluor+EmergencyLevel*m.Emergency)*(1+.18f*m.Beat);for(int c=0;c<Circuits;c++)into[c]=b*Brownout(c,m,time);}
        /// <summary>The share of the fixtures' light the emergency circuits keep in an emergency (red: <see cref="Colour"/>).</summary>
        public const float EmergencyLevel=.36f;
        /// <summary>The field's linear colour: the troffers' clinical white, turned emergency red by the mood's red cast.</summary>
        public static void Colour(in NativeWardMood m,out float r,out float g,out float b)
        {
            int f=NativeWardZeroLooks.FluorHex;r=(float)NativeSceneryBuilder.Channel(f,16);g=(float)NativeSceneryBuilder.Channel(f,8);b=(float)NativeSceneryBuilder.Channel(f,0);
            float red=Math.Min(1,m.Red*.85f);r=r*(1-red)+red;g*=1-red*.95f;b*=1-red*.9f;r*=Gain;g*=Gain;b*=Gain;
        }
        /// <summary>The share of a floor's light a surface facing `ny` (its normal's y) takes: 1 up, <see cref="Side"/> on a wall,
        /// <see cref="Down"/> facing down.</summary>
        public static float Facing(float ny)=>ny>=0?Side+(1-Side)*ny:Side+(Down-Side)*-ny;
        /// <summary>The slice coordinate (0..7) a surface at height `y` reads at the height `py` (its point moved out along its normal).</summary>
        public static float SliceAt(float y,float py)
        {
            if(y<Split)return (Math.Max(SliceY[0],Math.Min(SliceY[3],py))-SliceY[0])/(SliceY[1]-SliceY[0]);
            return 4+(Math.Max(SliceY[4],Math.Min(SliceY[7],py))-SliceY[4])/(SliceY[5]-SliceY[4]);
        }
        /// <summary>A fixture's light at a point (no shading, no occlusion): its pool on its floor through the point (the ray's
        /// radius where it meets the floor), the weak spill round it, falling off with distance (full at the floor under it) to
        /// nothing at its reach. `h` the fixture's height over its floor, `pool` its pool scale (m: its height for a troffer).</summary>
        public static float Falloff(float fx,float fy,float fz,float h,float pool,float reach,float px,float py,float pz)
        {
            float v=fy-py;if(v<=.05f||h<=.1f)return 0;float dx=px-fx,dz=pz-fz,rho=(float)Math.Sqrt(dx*dx+dz*dz),d=(float)Math.Sqrt(rho*rho+v*v);if(d>=reach)return 0;
            float q=d/reach;q*=q;float fade=1-q*q,near=h/Math.Max(d,.85f*h),onFloor=rho*h/v;
            float t=(onFloor-pool*PoolOut)/(pool*(PoolIn-PoolOut));t=t<0?0:t>1?1:t;
            return (t*t*(3-2*t)+Spill*v/d)*near*near*fade;
        }

        /// <summary>Bakes the field over the box (the map's bounds, widened by 2 m) from the kit's troffers and high-bays (dead
        /// ones left out) and the map's walls and floors between them.</summary>
        public static NativeWardZeroField Build(NativeWardZeroKit kit,ContentBlock[] blocks,float minX,float maxX,float minZ,float maxZ)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();int[] bucket=null;
            var f=new NativeWardZeroField{MinX=(float)Math.Floor(minX-2),MinZ=(float)Math.Floor(minZ-2)};f.Nx=(int)Math.Ceiling((maxX+2-f.MinX)/Cell);f.Nz=(int)Math.Ceiling((maxZ+2-f.MinZ)/Cell);
            f.Data=new float[f.Nx*f.Nz*Slices*4];var glows=kit?.Glows??Array.Empty<NativeWardGlow>();
            int n=0;foreach(var g in glows)if(Lamp(g))n++;
            var fx=new float[n];var fy=new float[n];var fz=new float[n];var fh=new float[n];var fp=new float[n];var fr=new float[n];var fw=new float[n];var fc=new int[n];n=0;
            foreach(var g in glows)
            {
                if(!Lamp(g))continue;bool bay=g.Kind==NativeWardZeroKit.GlowHighBay;fx[n]=g.X;fy[n]=g.Y-.04f;fz[n]=g.Z;fh[n]=Math.Max(.5f,g.Y-g.Floor);fp[n]=fh[n]*(bay?BayScale:1);
                fr[n]=fh[n]*(bay?BayReach:TrofferReach);fw[n]=bay?BayPower:TrofferPower;fc[n]=Circuit(g.Param);n++;
            }
            f.Fixtures=n;var solid=new Columns(blocks,f.MinX,f.MinZ,f.Nx*Cell,f.Nz*Cell);var inside=new bool[f.Nx*f.Nz*Slices];
            // The fixtures by 4 m bucket (each listed in every bucket its reach touches), so a texel only visits its neighbours.
            const float B=4;int bx=(int)Math.Ceiling(f.Nx*Cell/B),bz=(int)Math.Ceiling(f.Nz*Cell/B);var count=new int[bx*bz+1];
            for(int pass=0;pass<2;pass++)
            {
                var at=pass==0?null:(int[])count.Clone();
                for(int j=0;j<n;j++)
                {
                    int x0=Math.Max(0,(int)Math.Floor((fx[j]-fr[j]-f.MinX)/B)),x1=Math.Min(bx-1,(int)Math.Floor((fx[j]+fr[j]-f.MinX)/B)),z0=Math.Max(0,(int)Math.Floor((fz[j]-fr[j]-f.MinZ)/B)),z1=Math.Min(bz-1,(int)Math.Floor((fz[j]+fr[j]-f.MinZ)/B));
                    for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){int k=z*bx+x;if(pass==0)count[k+1]++;else bucket[at[k]++]=j;}
                }
                if(pass==0){for(int k=0;k<bx*bz;k++)count[k+1]+=count[k];bucket=new int[count[bx*bz]];}
            }
            // Each texel column on its own (rows in parallel: every texel is its own sum, so the order never matters). For a
            // column and a fixture one march toward the fixture serves all eight slices: each slice's ray climbs through the
            // walls' and floors' height spans of the columns it crosses.
            var data=f.Data;int nxs=f.Nx,nzs=f.Nz;float mx=f.MinX,mz=f.MinZ;var list=bucket;
            System.Threading.Tasks.Parallel.For(0,nzs,iz=>
            {
                Span<float> e=stackalloc float[Slices];Span<bool> open=stackalloc bool[Slices];float pz=mz+(iz+.5f)*Cell;
                for(int ix=0;ix<nxs;ix++)
                {
                    float px=mx+(ix+.5f)*Cell;int col=iz*nxs+ix;bool any=false;
                    for(int s=0;s<Slices;s++){bool solidAt=solid.Inside(px,pz,SliceY[s]);inside[s*nxs*nzs+col]=solidAt;any|=!solidAt;}
                    if(!any)continue;
                    int bk=Math.Min(bz-1,(int)((pz-mz)/B))*bx+Math.Min(bx-1,(int)((px-mx)/B));
                    for(int bi=count[bk];bi<count[bk+1];bi++)
                    {
                        int j=list[bi];float dx=px-fx[j],dz=pz-fz[j];if(dx*dx+dz*dz>=fr[j]*fr[j])continue;bool lit=false;
                        for(int s=0;s<Slices;s++){e[s]=inside[s*nxs*nzs+col]?0:Falloff(fx[j],fy[j],fz[j],fh[j],fp[j],fr[j],px,SliceY[s],pz)*fw[j];open[s]=e[s]>=MinLight;lit|=open[s];}
                        if(!lit)continue;solid.March(px,pz,fx[j],fy[j]-.12f,fz[j],open);
                        for(int s=0;s<Slices;s++)if(open[s])data[(s*nxs*nzs+col)*4+fc[j]]+=e[s];
                    }
                }
            });
            // The bounce: each floor's lowest slice spread through its open texels, added to all four of that floor's slices.
            {
                int plane=nxs*nzs;var a=new float[plane*4];var b=new float[plane*4];
                for(int floor=0;floor<2;floor++)
                {
                    int s0=floor*4;Array.Copy(data,s0*plane*4,a,0,plane*4);
                    for(int step=0;step<BounceSteps;step++)
                    {
                        var from=a;var to=b;
                        System.Threading.Tasks.Parallel.For(0,nzs,iz=>
                        {
                            for(int ix=0;ix<nxs;ix++)
                            {
                                int t=iz*nxs+ix;if(inside[s0*plane+t]){for(int c=0;c<4;c++)to[t*4+c]=0;continue;}float k=1;float c0=from[t*4],c1=from[t*4+1],c2=from[t*4+2],c3=from[t*4+3];
                                for(int o=0;o<4;o++){int x=ix+(o==0?1:o==1?-1:0),z=iz+(o==2?1:o==3?-1:0);if(x<0||z<0||x>=nxs||z>=nzs)continue;int u=z*nxs+x;if(inside[s0*plane+u])continue;c0+=from[u*4];c1+=from[u*4+1];c2+=from[u*4+2];c3+=from[u*4+3];k++;}
                                to[t*4]=c0/k;to[t*4+1]=c1/k;to[t*4+2]=c2/k;to[t*4+3]=c3/k;
                            }
                        });
                        a=to;b=from;
                    }
                    for(int s=s0;s<s0+4;s++)for(int t=0;t<plane;t++)if(!inside[s*plane+t])for(int c=0;c<4;c++)data[(s*plane+t)*4+c]+=Bounce*a[t*4+c];
                }
            }
            for(int t=0;t<inside.Length;t++)if(!inside[t])for(int c=0;c<4;c++)data[t*4+c]+=Floor/Circuits;
            // Texels inside a wall or a floor take the mean of their filled neighbours in the slice (open ones first, then, pass by
            // pass, the ones filled before), so the filter's reach into a wall never darkens the floor beside it, and the top of a
            // thick block (a freezer one can stand on) reads the light round it.
            var filled=new bool[inside.Length];for(int t=0;t<inside.Length;t++)filled[t]=!inside[t];var now=new bool[inside.Length];
            for(int pass=0;pass<DilatePasses;pass++)
            {
                bool any=false;
                for(int s=0;s<Slices;s++)for(int iz=0;iz<f.Nz;iz++)for(int ix=0;ix<f.Nx;ix++)
                {
                    int t=(s*f.Nz+iz)*f.Nx+ix;if(filled[t])continue;int k=0;float a=0,b=0,c=0,d=0;
                    for(int o=0;o<4;o++){int x=ix+(o==0?1:o==1?-1:0),z=iz+(o==2?1:o==3?-1:0);if(x<0||z<0||x>=f.Nx||z>=f.Nz)continue;int u=(s*f.Nz+z)*f.Nx+x;if(!filled[u])continue;a+=f.Data[u*4];b+=f.Data[u*4+1];c+=f.Data[u*4+2];d+=f.Data[u*4+3];k++;}
                    if(k>0){f.Data[t*4]=a/k;f.Data[t*4+1]=b/k;f.Data[t*4+2]=c/k;f.Data[t*4+3]=d/k;now[t]=true;any=true;}
                }
                for(int t=0;t<now.Length;t++)if(now[t]){filled[t]=true;now[t]=false;}
                if(!any)break;
            }
            f.BakeMs=watch.Elapsed.TotalMilliseconds;return f;
        }
        private static bool Lamp(in NativeWardGlow g)=>(g.Kind==NativeWardZeroKit.GlowFluor||g.Kind==NativeWardZeroKit.GlowHighBay)&&g.Param>=NativeWardZeroKit.DeadShare;

        /// <summary>The field's light at a surface point with its normal (linear, before the circuits' levels and colour: the
        /// four circuits' raw values in `c0..c3`), as the shader reads it: trilinear between the texel centres of its floor's
        /// slices. Allocation free.</summary>
        public void Sample(float x,float y,float z,float nx,float ny,float nz,out float c0,out float c1,out float c2,out float c3)
        {
            c0=c1=c2=c3=0;if(Data.Length==0)return;float o=OffsetFor(ny),px=x+nx*o,py=y+ny*o,pz=z+nz*o;
            float u=(px-MinX)/Cell-.5f,w=(pz-MinZ)/Cell-.5f,s=SliceAt(y,py);
            int x0=(int)Math.Floor(u),z0=(int)Math.Floor(w),s0=(int)Math.Floor(s);float ax=u-x0,az=w-z0,as_=s-s0;
            for(int k=0;k<8;k++)
            {
                int xi=Math.Max(0,Math.Min(Nx-1,x0+(k&1))),zi=Math.Max(0,Math.Min(Nz-1,z0+((k>>1)&1))),si=Math.Max(0,Math.Min(Slices-1,s0+((k>>2)&1)));
                float wgt=((k&1)!=0?ax:1-ax)*(((k>>1)&1)!=0?az:1-az)*(((k>>2)&1)!=0?as_:1-as_);if(wgt<=0)continue;int t=((si*Nz+zi)*Nx+xi)*4;
                c0+=Data[t]*wgt;c1+=Data[t+1]*wgt;c2+=Data[t+2]*wgt;c3+=Data[t+3]*wgt;
            }
            float fa=Facing(ny);c0*=fa;c1*=fa;c2*=fa;c3*=fa;
        }

        // The walls and floors that shade the field: axis-aligned blocks at least 2.2 m tall, and floors and decks (2 m by 2 m or
        // more, 0.2 m thick or more); not the ground slab or the roof (nothing passes them), glass, tanks, the Cradle's shards or
        // anything thinner than 0.2 m. Kept as 0.2 m columns, each with the height spans its blocks fill (merged), so a march
        // toward a fixture tests every slice's ray at once against the spans of the columns it crosses.
        private sealed class Columns
        {
            private const float V=.2f;private readonly int nx,nz;private readonly float x0,z0;private readonly int[] start;private readonly float[] lo,hi;
            public Columns(ContentBlock[] blocks,float minX,float minZ,float sizeX,float sizeZ)
            {
                x0=minX;z0=minZ;nx=(int)Math.Ceiling(sizeX/V);nz=(int)Math.Ceiling(sizeZ/V);var spans=new System.Collections.Generic.List<(float a,float b)>[nx*nz];
                foreach(var b in blocks??Array.Empty<ContentBlock>())
                {
                    if(b?.id==null||!Occludes(b))continue;var r=b.rotation;bool quarter=r!=null&&Math.Abs(Math.Abs(r.y)-.70710678f)<.01f&&Math.Abs(Math.Abs(r.w)-.70710678f)<.01f;
                    float hx=(quarter?b.size.z:b.size.x)/2,hy=b.size.y/2,hz=(quarter?b.size.x:b.size.z)/2;
                    int ax=Math.Max(0,(int)Math.Ceiling((b.pos.x-hx-x0)/V-.5f)),bx=Math.Min(nx-1,(int)Math.Floor((b.pos.x+hx-x0)/V-.5f));
                    int az=Math.Max(0,(int)Math.Ceiling((b.pos.z-hz-z0)/V-.5f)),bz=Math.Min(nz-1,(int)Math.Floor((b.pos.z+hz-z0)/V-.5f));
                    for(int z=az;z<=bz;z++)for(int x=ax;x<=bx;x++){ref var l=ref spans[z*nx+x];l??=new System.Collections.Generic.List<(float,float)>(2);l.Add((b.pos.y-hy,b.pos.y+hy));}
                }
                start=new int[nx*nz+1];int total=0;
                for(int c=0;c<spans.Length;c++){var l=spans[c];if(l!=null){l.Sort((p,q)=>p.a.CompareTo(q.a));int m=0;for(int i=0;i<l.Count;i++){if(m>0&&l[i].a<=l[m-1].b){l[m-1]=(l[m-1].a,Math.Max(l[m-1].b,l[i].b));continue;}l[m++]=l[i];}l.RemoveRange(m,l.Count-m);total+=m;}start[c+1]=total;}
                lo=new float[total];hi=new float[total];for(int c=0;c<spans.Length;c++){var l=spans[c];if(l==null)continue;for(int i=0;i<l.Count;i++){lo[start[c]+i]=l[i].a;hi[start[c]+i]=l[i].b;}}
            }
            private static bool Occludes(ContentBlock b)
            {
                string id=b.id;if(id=="siren-slab"||id=="siren-vault"||id.IndexOf("-tank-",StringComparison.Ordinal)>=0||id.IndexOf("cradle-shard",StringComparison.Ordinal)>=0)return false;
                var r=b.rotation;bool upright=r==null||Math.Abs(r.x)<.01f&&Math.Abs(r.z)<.01f&&(Math.Abs(r.y)<.01f||Math.Abs(Math.Abs(r.y)-.70710678f)<.01f||Math.Abs(Math.Abs(r.y)-1)<.01f);if(!upright)return false;
                return Math.Min(b.size.x,b.size.z)>=.2f&&(b.size.y>=2.2f||b.size.y>=.2f&&b.size.x>=2&&b.size.z>=2);
            }
            private int Column(float x,float z){int i=(int)Math.Floor((x-x0)/V),k=(int)Math.Floor((z-z0)/V);return i<0||k<0||i>=nx||k>=nz?-1:k*nx+i;}
            /// <summary>Whether (x, y, z) is inside a wall or a floor.</summary>
            public bool Inside(float x,float z,float y){int c=Column(x,z);if(c<0)return false;for(int i=start[c];i<start[c+1];i++)if(y>lo[i]&&y<hi[i])return true;return false;}
            /// <summary>Marches from the texel column at (px, pz) toward the point (fx, fy, fz) in 0.15 m steps (a wall fills two
            /// columns or more) and closes each open slice whose ray, climbing from the slice's height to fy, passes through a
            /// wall's or a floor's span on the way.</summary>
            public void March(float px,float pz,float fx,float fy,float fz,Span<bool> open)
            {
                float dx=fx-px,dz=fz-pz,rho=(float)Math.Sqrt(dx*dx+dz*dz);int steps=Math.Max(1,(int)Math.Ceiling(rho/.15f));
                for(int k=0;k<steps;k++)
                {
                    float t0=(float)k/steps,t1=(float)(k+1)/steps,tm=(t0+t1)*.5f;int c=Column(px+dx*tm,pz+dz*tm);if(c<0||start[c]==start[c+1])continue;bool left=false;
                    for(int s=0;s<Slices;s++)
                    {
                        if(!open[s])continue;float y=SliceY[s],ya=y+(fy-y)*t0,yb=y+(fy-y)*t1;
                        for(int i=start[c];i<start[c+1];i++)if(ya<hi[i]&&yb>lo[i]){open[s]=false;break;}
                        left|=open[s];
                    }
                    if(!left)return;
                }
            }
        }
        private static double Hash01(int a,int c){unchecked{uint x=(uint)a*0x9E3779B9U;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;x^=(uint)c*0x85EBCA6BU;x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x/4294967296.0;}}
    }
}
