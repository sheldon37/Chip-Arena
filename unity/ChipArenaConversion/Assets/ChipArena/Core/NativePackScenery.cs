using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One bank prop placed round a map: the prop, its world place (prop space to the Unity frame, System.Numerics row
    /// vectors), and its tier: detail (left out on the low tier) or the low tier's lighter stand-in (drawn only there).</summary>
    public struct PackPlacement {public int Prop;public Matrix4x4 World;public bool Detail,LowOnly;}
    /// <summary>A map's harbour yards: the bank props, the ground boxes (aprons, quay walls, water; SourceLit sector meshes: 0 the
    /// ground, 1 its detail) and the keep-out rectangle every piece stays outside (the map blocks' footprint grown by
    /// <see cref="NativePackScenery.Margin"/>).</summary>
    public sealed class PackScene {public PackPlacement[] Props=Array.Empty<PackPlacement>();public NativeSceneryMesh[] Ground=Array.Empty<NativeSceneryMesh>();public double MinX,MaxX,MinZ,MaxZ;}
    /// <summary>One baked backdrop mesh of bank props: every placement's parts of one bank material, in world space (positions,
    /// normals, uv0; 32-bit indices), so a map's yards draw in a few draws.</summary>
    public sealed class PackBakedMesh {public int Material;public bool Detail,LowOnly;public float[] Positions,Normals,Uv;public int[] Indices;public float MinX,MinY,MinZ,MaxX,MaxY,MaxZ;public int Vertices=>Positions.Length/3;}

    /// <summary>
    /// The harbour yards round a map (docs/arenas/STAGE-KITS.md "Yards"): container terminals, ship-to-shore and quay cranes,
    /// moored freighters and tugs, quay walls over dark water, lamps, fences and yard clutter, from PackDressing.json's `yards`
    /// and the pack bank. Presentation only, outside play space: nothing here collides or is sim data, and every piece stands
    /// outside the keep-out rectangle (the map blocks' footprint grown by <see cref="Margin"/> m; <see cref="Build"/> throws
    /// otherwise, as NativeSceneryBuilder does for a backdrop box). Runtime/NativePackStage draws it on
    /// NativeSceneryBuilder.BackdropLayer without shadows; the lights atlas glows by the map's `glow`.
    /// A yard's frame: its anchor (x, z), turned by `yaw` (Unity's: local +z toward (sin yaw, 0, cos yaw)); `length` runs along the
    /// turned x, `depth` along the turned z (a quay's water and a crane's boom lie toward +z).
    /// Kinds: apron (a ground plate), quay (the wall, its kerb, bollards and fenders, the water beyond), stacks (container blocks
    /// `height` tiers at most), sts (ship-to-shore cranes along the edge, booms over the water), crane (quay cranes), ship
    /// (a freighter moored along the edge), tug, lights (lamps along a line), fence, scatter (yard clutter on a grid), trucks
    /// (tractors and trailers in a row), building (one shed or block).
    /// Deterministic (integer hashes of the yard seed); allocates only in Build and Bake.
    /// </summary>
    public static class NativePackScenery
    {
        public static readonly string[] Kinds={"apron","quay","stacks","sts","crane","ship","tug","lights","fence","scatter","trucks","building"};
        public const double Margin=2,Water=-2.2,Deck=0;
        public const int Ground=0,GroundDetail=1,Groups=2;
        public const int Concrete=0x6A665E,ConcreteDark=0x4E4A44,Kerb=0xC8A028,Bollard=0x2A2826,Fender=0x1A1A1C,WaterColor=0x12222A,Apron=0x5C5852;
        // The kinds' props when a yard names none (tools/pack_bank/selections/kit-harbor.json).
        public static readonly string[] Containers40={"harbor-shipping-container-03a","harbor-shipping-container-03b","harbor-shipping-container-03c","harbor-shipping-container-03d","harbor-shipping-container-03e","harbor-shipping-container-03f",
            "harbor-shipping-container-06a","harbor-shipping-container-06b","harbor-shipping-container-06c","harbor-shipping-container-06d","harbor-shipping-container-06e","harbor-shipping-container-06f","harbor-shipping-container-06g"};
        public static readonly string[] Containers20={"harbor-shipping-container-02a","harbor-shipping-container-02b","harbor-shipping-container-02c","harbor-shipping-container-02d","harbor-shipping-container-02e","harbor-shipping-container-02f",
            "harbor-shipping-container-05a","harbor-shipping-container-05b","harbor-shipping-container-05c","harbor-shipping-container-05d","harbor-shipping-container-05e","harbor-shipping-container-05f","harbor-shipping-container-05g"};
        public static readonly string[] Sts={"harbor-gantry-crane-01a","harbor-gantry-crane-01c"},Cranes={"harbor-harbor-crane-01a","harbor-harbor-crane-01e"},
            Ships={"harbor-freighter-03a-loaded"},ShipsLow={"harbor-freighter-02a"},Tugs={"harbor-tugboat-01a","harbor-tugboat-03a"},Lamps={"harbor-streetlight-01a","harbor-streetlight-01b"},Fences={"harbor-metal-fence-01a"},
            Scatter={"harbor-wooden-pallet-01a","harbor-barrel-01a","harbor-barrel-05a","harbor-shipping-wooden-crate-01a","harbor-shipping-metal-crate-01b","harbor-cage-barrels-01a","harbor-gas-barrel-01a","harbor-plank-pile-01a"},
            Cabins={"harbor-truck-cabin-01a","harbor-truck-cabin-01c","harbor-truck-cabin-02b"},Trailers={"harbor-truck-trailer-02a","harbor-truck-trailer-02c","harbor-truck-trailer-01b"},Buildings={"harbor-metal-structure-kit-building-05a"};
        public const double Bay40=12.6,Row=3.3,Tier=3.0;
        // A lamp's ground pool: the three steps' radii (m) and shares of the yard's `height` level; the sodium glow.
        private static readonly double[] PoolRadius={7.5,4.8,2.6},PoolLevel={.35,.65,1.0};public const int Sodium=0xFFB060;
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+4049,b+17,c+29);

        /// <summary>The keep-out rectangle: the footprint of every block in the map (turned blocks by their corners) grown by Margin.</summary>
        public static void KeepOut(ContentMap map,out double minX,out double maxX,out double minZ,out double maxZ)
        {
            minX=minZ=double.MaxValue;maxX=maxZ=double.MinValue;
            foreach(var b in map?.blocks??Array.Empty<ContentBlock>())
            {
                if(b==null)continue;var r=b.rotation;double qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r==null?1:r.w;
                for(int c=0;c<8;c++)
                {
                    double lx=((c&1)==0?-.5:.5)*b.size.x,ly=((c&2)==0?-.5:.5)*b.size.y,lz=((c&4)==0?-.5:.5)*b.size.z;
                    double tx=2*(qy*lz-qz*ly),ty=2*(qz*lx-qx*lz),tz=2*(qx*ly-qy*lx);double wx=lx+qw*tx+(qy*tz-qz*ty),wz=lz+qw*tz+(qx*ty-qy*tx);
                    minX=Math.Min(minX,b.pos.x+wx);maxX=Math.Max(maxX,b.pos.x+wx);minZ=Math.Min(minZ,b.pos.z+wz);maxZ=Math.Max(maxZ,b.pos.z+wz);
                }
            }
            if(minX>maxX){minX=maxX=minZ=maxZ=0;}
            minX-=Margin;maxX+=Margin;minZ-=Margin;maxZ+=Margin;
        }
        /// <summary>Whether a world footprint (axis-aligned) enters the open keep-out rectangle.</summary>
        public static bool Enters(PackScene s,double x0,double x1,double z0,double z1)=>x1>s.MinX+.01&&x0<s.MaxX-.01&&z1>s.MinZ+.01&&z0<s.MaxZ-.01;

        private sealed class Sink
        {
            public readonly PackScene Scene;public readonly NativePackBank.Bank Bank;public readonly NativeSceneryBuilder Boxes=new NativeSceneryBuilder(Groups);public readonly List<PackPlacement> Props=new List<PackPlacement>();public string Where;
            public Sink(PackScene s,NativePackBank.Bank b){Scene=s;Bank=b;}
            public int Find(string name)=>name!=null&&Bank.PropIndex.TryGetValue(name,out int k)?k:-1;
            /// <summary>A prop at a world point and turn (degrees), lifted by y; skipped when the bank lacks it.</summary>
            public void Put(string name,double x,double y,double z,double yaw,bool detail,bool lowOnly=false)
            {
                int k=Find(name);if(k<0)return;var p=Bank.Props[k];
                var m=Matrix4x4.CreateRotationY((float)(yaw*Math.PI/180))*Matrix4x4.CreateTranslation((float)x,(float)y,(float)z);
                NativePackDressing.Bounds(p,m,out var lo,out var hi);
                if(Enters(Scene,lo.X,hi.X,lo.Z,hi.Z))throw new InvalidOperationException("Pack yard "+Where+": "+name+" enters the arena at "+x.ToString("0.#")+", "+z.ToString("0.#"));
                Props.Add(new PackPlacement{Prop=k,World=m,Detail=detail,LowOnly=lowOnly});
            }
            /// <summary>A flat-coloured box (centre, full size, yaw) into a ground group, outside the keep-out.</summary>
            public void Box(int group,double x,double y,double z,double sx,double sy,double sz,int hex,double yaw=0,double shade=1,int skip=0,int glow=0,double level=0)
            {
                double t=yaw*Math.PI/180,c=Math.Abs(Math.Cos(t)),n=Math.Abs(Math.Sin(t)),ex=(c*sx+n*sz)/2,ez=(n*sx+c*sz)/2;
                if(Enters(Scene,x-ex,x+ex,z-ez,z+ez))throw new InvalidOperationException("Pack yard "+Where+": a ground box enters the arena at "+x.ToString("0.#")+", "+z.ToString("0.#"));
                Boxes.Current=group;Boxes.Box(x,y,z,sx,sy,sz,hex,yaw:yaw,shade:shade,skip:skip,glow:glow,level:level);
            }
        }
        // The yard's frame: local (u along the turned x, w along the turned z) to world.
        private static void At(PackYard y,double u,double w,out double x,out double z){double t=y.yaw*Math.PI/180,c=Math.Cos(t),s=Math.Sin(t);x=y.x+u*c+w*s;z=y.z-u*s+w*c;}
        private static string Pick(string[] list,int a,int b=0,int c=0)=>list[Math.Min(list.Length-1,(int)(Hash(a,b,c)*list.Length))];

        /// <summary>The map's yards (an empty scene without a bank or yards). Throws when a piece would enter the arena.</summary>
        public static PackScene Build(PackMapDressing entry,ContentMap map,NativePackBank.Bank bank)
        {
            var scene=new PackScene();KeepOut(map,out scene.MinX,out scene.MaxX,out scene.MinZ,out scene.MaxZ);
            if(bank==null||entry?.yards==null||entry.yards.Length==0)return scene;var s=new Sink(scene,bank);int n=0;
            foreach(var y in entry.yards){s.Where=entry.map+" #"+n+" "+y.kind;Yard(s,y,n++);}
            scene.Props=s.Props.ToArray();scene.Ground=new NativeSceneryMesh[Groups];for(int g=0;g<Groups;g++)scene.Ground[g]=s.Boxes.Bake(g);
            return scene;
        }
        private static void Yard(Sink s,PackYard y,int n)
        {
            int seed=y.seed*131+n;bool d=y.detail;double hl=y.length/2,hd=y.depth/2;
            switch(y.kind)
            {
                case "apron":{At(y,0,0,out double x,out double z);s.Box(Ground,x,-.27,z,y.length,.5,y.depth,Apron,y.yaw,.95+.05*Hash(seed),1|2|8|16|32);break;}
                case "quay":
                {
                    // The wall's face at w = 0 down to the water, a kerb and the deck behind it, the water out to `depth`.
                    At(y,0,.4,out double fx,out double fz);s.Box(Ground,fx,-1.6,fz,y.length,3.2,.8,ConcreteDark,y.yaw,1,8);
                    At(y,0,-3,out double dx,out double dz);s.Box(Ground,dx,-.27,dz,y.length,.5,6,Concrete,y.yaw,1,8);
                    At(y,0,-.25,out double kx,out double kz);s.Box(GroundDetail,kx,.08,kz,y.length,.16,.3,Kerb,y.yaw,.9,8);
                    At(y,0,.8+hd,out double wx,out double wz);s.Box(Ground,wx,Water-.1,wz,y.length,.2,y.depth,WaterColor,y.yaw,1,8);
                    for(double u=-hl+6;u<=hl-6;u+=12)
                    {
                        At(y,u,-.8,out double bx,out double bz);s.Box(GroundDetail,bx,.3,bz,.5,.6,.5,Bollard,y.yaw,1,8);
                        At(y,u+6,.95,out double ex,out double ez);s.Box(GroundDetail,ex,-1.2,ez,1.2,1.8,.3,Fender,y.yaw,1,0);
                    }
                    break;
                }
                case "stacks":
                {
                    // Container blocks: bays of one 40 ft or two 20 ft boxes (long side along u), rows across w, up to `height` tiers.
                    var c40=y.props!=null&&y.props.Length>0?y.props:Containers40;int bays=Math.Max(1,(int)(y.length/Bay40)),rows=Math.Max(1,(int)(y.depth/Row)),tiers=Math.Max(1,(int)(y.height>0?y.height:4));
                    for(int b=0;b<bays;b++)for(int r=0;r<rows;r++)
                    {
                        double u=-hl+Bay40*(b+.5),w=-hd+Row*(r+.5);double h=Hash(seed,b,r);int t=h<.08?0:1+(int)(Hash(seed,b,r+97)*tiers);if(t>tiers)t=tiers;
                        for(int k=0;k<t;k++)
                        {
                            bool pair=Hash(seed,b*31+r,k+5)<.3;
                            if(!pair){At(y,u,w,out double x,out double z);s.Put(Pick(c40,seed,b*17+r,k),x,Deck+k*Tier,z,y.yaw+90+(Hash(seed,b,r*7+k)<.5?0:180),d);}
                            else foreach(double off in new[]{-3.15,3.15}){At(y,u+off,w,out double x,out double z);s.Put(Pick(Containers20,seed,b*17+r,k+(off>0?50:0)),x,Deck+k*Tier,z,y.yaw+90+(Hash(seed,b,r*7+k+3)<.5?0:180),d);}
                        }
                    }
                    break;
                }
                case "sts":case "crane":case "lights":case "tug":
                {
                    // `count` along u, spaced evenly over the length; cranes face +w (their booms over the water).
                    var list=y.props!=null&&y.props.Length>0?y.props:y.kind=="sts"?Sts:y.kind=="crane"?Cranes:y.kind=="tug"?Tugs:Lamps;int count=Math.Max(1,y.count);
                    for(int i=0;i<count;i++)
                    {
                        double u=count==1?0:-hl+y.length*i/(count-1);At(y,u,0,out double x,out double z);s.Put(Pick(list,seed,i),x,y.kind=="tug"?Water:Deck,z,y.yaw+(y.kind=="tug"?90+(Hash(seed,i,3)<.5?0:180):0),d);
                        // A lamp's pool of light on the ground (`height`: its brightest step): three steps, each four turned squares (a
                        // sixteen-point rosette, near round), the painted light of a toon night.
                        if(y.kind=="lights"&&y.height>0)for(int k=0;k<3;k++){double r=PoolRadius[k]*.85,t=.004*(k+1);for(int q=0;q<4;q++)s.Box(Ground,x,.002+t/2,z,2*r,t,2*r,Apron,y.yaw+q*22.5,.9,8,Sodium,y.height*PoolLevel[k]);}
                    }
                    break;
                }
                case "ship":
                {
                    // The loaded freighter on the normal tiers; the low tier draws the empty hull in its place (a fifth of the triangles).
                    At(y,0,0,out double x,out double z);bool named=y.props!=null&&y.props.Length>0;
                    s.Put(named?y.props[0]:Ships[0],x,Water,z,y.yaw+90,true);if(!d)s.Put(named?(y.props.Length>1?y.props[1]:y.props[0]):ShipsLow[0],x,Water,z,y.yaw+90,false,true);
                    break;
                }
                case "building":{At(y,0,0,out double x,out double z);s.Put(Pick(y.props!=null&&y.props.Length>0?y.props:Buildings,seed),x,Deck,z,y.yaw,d);break;}
                case "fence":
                {
                    var list=y.props!=null&&y.props.Length>0?y.props:Fences;int k=s.Find(list[0]);if(k<0)break;double pitch=Math.Max(.5,s.Bank.Props[k].Size.X);
                    for(double u=-hl+pitch/2;u<=hl-pitch/2+1e-6;u+=pitch){At(y,u,0,out double x,out double z);s.Put(list[0],x,Deck,z,y.yaw,d);}
                    break;
                }
                case "scatter":
                {
                    // Clutter on a 3 m grid: each cell holds a prop by the hash (about half the cells), turned by quarter turns.
                    var list=y.props!=null&&y.props.Length>0?y.props:Scatter;int nu=Math.Max(1,(int)(y.length/3)),nw=Math.Max(1,(int)(y.depth/3)),cap=y.count>0?y.count:int.MaxValue,placed=0;
                    for(int i=0;i<nu&&placed<cap;i++)for(int j=0;j<nw&&placed<cap;j++){if(Hash(seed,i,j)>.5)continue;At(y,-hl+3*(i+.5),-hd+3*(j+.5),out double x,out double z);s.Put(Pick(list,seed,i*53+j),x,Deck,z,y.yaw+90*(int)(Hash(seed,j,i)*4),d);placed++;}
                    break;
                }
                case "trucks":
                {
                    // Tractor and trailer pairs parked side by side along u, noses toward +w.
                    int count=Math.Max(1,y.count);
                    for(int i=0;i<count;i++)
                    {
                        double u=count==1?0:-hl+y.length*i/(count-1);string cab=Pick(Cabins,seed,i),trailer=Pick(Trailers,seed,i,1);int kc=s.Find(cab),kt=s.Find(trailer);if(kc<0||kt<0)continue;
                        double lc=s.Bank.Props[kc].Size.Z,lt=s.Bank.Props[kt].Size.Z;
                        At(y,u,lt*.5+.2,out double x,out double z);s.Put(cab,x,Deck,z,y.yaw,d);At(y,u,-lc*.15,out x,out z);if(Hash(seed,i,9)<.8)s.Put(trailer,x,Deck,z,y.yaw,d);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// The scene's props baked into one mesh per bank material and tier (both tiers, detail, the low tier's stand-ins), in world space: positions, the normals
        /// through each place's inverse transpose, uv0, 32-bit indices. Parts with a non-opaque material (glass) bake as the others.
        /// </summary>
        public static PackBakedMesh[] Bake(NativePackBank.Bank bank,PackPlacement[] props)
        {
            var o=new List<PackBakedMesh>();if(bank==null||props==null)return o.ToArray();
            for(int tier=0;tier<3;tier++)for(int mat=0;mat<bank.Materials.Count;mat++)
            {
                var P=new List<float>();var N=new List<float>();var U=new List<float>();var T=new List<int>();
                foreach(var pl in props)
                {
                    if((pl.LowOnly?2:pl.Detail?1:0)!=tier)continue;var prop=bank.Props[pl.Prop];
                    foreach(var part in prop.Parts)
                    {
                        var mesh=bank.Meshes[part.Mesh];bool any=false;foreach(var sm in mesh.Submeshes)if(sm.Material==mat){any=true;break;}if(!any)continue;
                        var m=part.Place*pl.World;Matrix4x4.Invert(m,out var inv);var nt=Matrix4x4.Transpose(inv);int start=P.Count/3;
                        for(int v=0;v<mesh.Positions.Length;v++)
                        {
                            var p=Vector3.Transform(mesh.Positions[v],m);var q=Vector3.TransformNormal(mesh.Normals[v],nt);float l=q.Length();if(l>1e-8f)q/=l;
                            P.Add(p.X);P.Add(p.Y);P.Add(p.Z);N.Add(q.X);N.Add(q.Y);N.Add(q.Z);U.Add(mesh.Uv0[v].X);U.Add(mesh.Uv0[v].Y);
                        }
                        foreach(var sm in mesh.Submeshes)if(sm.Material==mat)foreach(int i in sm.Indices)T.Add(start+i);
                    }
                }
                if(T.Count==0)continue;var b=new PackBakedMesh{Material=mat,Detail=tier==1,LowOnly=tier==2,Positions=P.ToArray(),Normals=N.ToArray(),Uv=U.ToArray(),Indices=T.ToArray()};
                b.MinX=b.MinY=b.MinZ=float.MaxValue;b.MaxX=b.MaxY=b.MaxZ=float.MinValue;
                for(int i=0;i<b.Positions.Length;i+=3){b.MinX=Math.Min(b.MinX,b.Positions[i]);b.MaxX=Math.Max(b.MaxX,b.Positions[i]);b.MinY=Math.Min(b.MinY,b.Positions[i+1]);b.MaxY=Math.Max(b.MaxY,b.Positions[i+1]);b.MinZ=Math.Min(b.MinZ,b.Positions[i+2]);b.MaxZ=Math.Max(b.MaxZ,b.Positions[i+2]);}
                o.Add(b);
            }
            return o.ToArray();
        }

        /// <summary>The x of a quay facing west (its water toward -x: yaw 270) in this entry, or NaN: the town's west ground plate ends
        /// there so the basin's water shows (NativeExpeditionCityScenery.Build's `westWater`).</summary>
        public static double WestQuay(PackMapDressing entry)
        {
            if(entry?.yards!=null)foreach(var y in entry.yards)if(y?.kind=="quay"&&Math.Abs(((y.yaw%360)+360)%360-270)<1e-3)return y.x;
            return double.NaN;
        }
        /// <summary>A yard's footprint in its own frame (u along the turned x, w along the turned z): a quay's deck and water, a
        /// crane's reach, a line's width; the rest centred on the anchor.</summary>
        public static void Footprint(PackYard y,out double u0,out double u1,out double w0,out double w1)
        {
            double hl=y.length/2,hd=y.depth/2;u0=-hl;u1=hl;w0=-hd;w1=hd;
            switch(y.kind)
            {
                case "quay":w0=-6;w1=.8+y.depth;break;
                case "sts":u0-=10;u1+=10;w0=-35;w1=80;break;
                case "crane":u0-=10;u1+=10;w0=-25;w1=25;break;
                case "ship":u0=-115;u1=115;w0=-14;w1=14;break;
                case "tug":u0-=20;u1+=20;w0=-7;w1=7;break;
                case "lights":case "fence":w0=-3;w1=3;u0-=2;u1+=2;break;
                case "trucks":u0-=3;u1+=3;w0=-14;w1=10;break;
                case "building":u0=-32;u1=32;w0=-24;w1=24;break;
            }
        }
        /// <summary>Whether a world footprint lies under a yard of this entry (the town's skyline leaves its lots there empty while the
        /// yards draw: NativeExpeditionCityScenery.Build's `reserved`), each yard's footprint (<see cref="Footprint"/>) turned and
        /// grown by 4 m.</summary>
        public static bool Reserved(PackMapDressing entry,double x0,double x1,double z0,double z1)
        {
            if(entry?.yards==null)return false;
            foreach(var y in entry.yards)
            {
                if(y==null)continue;Footprint(y,out double u0,out double u1,out double w0,out double w1);double lx=double.MaxValue,hx=double.MinValue,lz=double.MaxValue,hz=double.MinValue;
                for(int c=0;c<4;c++){At(y,(c&1)==0?u0:u1,(c&2)==0?w0:w1,out double x,out double z);lx=Math.Min(lx,x);hx=Math.Max(hx,x);lz=Math.Min(lz,z);hz=Math.Max(hz,z);}
                if(x1>lx-4&&x0<hx+4&&z1>lz-4&&z0<hz+4)return true;
            }
            return false;
        }
    }
}
