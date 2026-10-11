using System;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Brickfall backdrop and atmosphere (Core/NativeSceneryBuilder.cs, NativeBrickfallScenery.cs, NativeBrickfallAtmosphere.cs,
    // NativeBrickfallFx.cs; Runtime/NativeBrickfallPresentation.cs): the city is deterministic, every triangle stays outside
    // the slab plus 2 m (x ±34, z ±30), sector meshes stay out of the arena's point-light reach, the menu orbit flies clear,
    // nothing stands in a crane jib's sweep; the train, jibs, birds and smoke are pure functions of the clock that never enter
    // the arena; atmosphere counts follow quality, pigeons scatter and return without jumps and the killcam reads the same
    // memory, collapse dust drifts east and the dust coat resets with the round; the frame builder allocates nothing; and the
    // runtime hooks, dormancy and draw rules are in place.
    internal static class BrickfallSceneryChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Brickfall scenery: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=1e-6){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        public static void Run(string root)
        {
            Backdrop();Moving();Atmosphere();Fx();Hooks(root);
            Console.WriteLine("PASS brickfall scenery ("+count+" checks: deterministic five-sector city outside x ±34 / z ±30 triangle by triangle, sector bounds clear of arena point lights, menu orbit and crane sweeps clear, §3.10 palette; train schedule, direction and viaduct span, jib slew clear of the arena, aviation flashes, birds and smoke outside; quality counts, round start, motes, scraps, pigeons scatter/return/continuity/killcam memory, tarps, shafts, collapse dust and dust coat; allocation-free frame builder; runtime hooks and draw rules)");
        }
        private static bool TriangleEnters(float[] p,int a,int b,int c,double rx,double rz)
        {
            double[] x={p[a*3],p[b*3],p[c*3]},z={p[a*3+2],p[b*3+2],p[c*3+2]};
            double[] nx={1,0,0,0,0},nz={0,1,0,0,0};for(int e=0;e<3;e++){int f=(e+1)%3;nx[2+e]=z[f]-z[e];nz[2+e]=x[e]-x[f];}
            for(int k=0;k<5;k++)
            {
                double l=Math.Sqrt(nx[k]*nx[k]+nz[k]*nz[k]);if(l<1e-9)continue;double ux=nx[k]/l,uz=nz[k]/l,lo=double.MaxValue,hi=double.MinValue;
                for(int i=0;i<3;i++){double d=x[i]*ux+z[i]*uz;lo=Math.Min(lo,d);hi=Math.Max(hi,d);}double r=rx*Math.Abs(ux)+rz*Math.Abs(uz);if(hi<=-r+1e-4||lo>=r-1e-4)return false;
            }
            return true;
        }
        private static void Backdrop()
        {
            var a=NativeBrickfallScenery.Build();var b=NativeBrickfallScenery.Build();Check(a.Length==NativeBrickfallScenery.Sectors&&a.Length==5,"five sector meshes");
            int total=0;
            for(int s=0;s<a.Length;s++)
            {
                var m=a[s];Check(m.Positions.SequenceEqual(b[s].Positions)&&m.Colors.SequenceEqual(b[s].Colors)&&m.Emission.SequenceEqual(b[s].Emission)&&m.Indices.SequenceEqual(b[s].Indices),"sector "+s+" is deterministic");
                Check(m.Indices.Length%3==0&&m.Indices.All(i=>i>=0&&i<m.Vertices)&&m.Normals.Length==m.Positions.Length&&m.Colors.Length==m.Vertices*4&&m.Emission.Length==m.Vertices*4,"sector "+s+" arrays are consistent");
                bool clear=true;for(int t=0;t<m.Indices.Length;t+=3)if(TriangleEnters(m.Positions,m.Indices[t],m.Indices[t+1],m.Indices[t+2],NativeBrickfallScenery.InnerX,NativeBrickfallScenery.InnerZ)){clear=false;break;}
                Check(clear,"no triangle of sector "+s+" enters x ±34 / z ±30");total+=m.Vertices;
                Check(m.Colors.All(v=>v>=0&&v<=1.2f)&&m.Emission.All(v=>v>=0&&v<=4),"sector "+s+" colours are linear and bounded");
            }
            Check(total<380000,"backdrop stays under 380k vertices ("+total+")");
            Check(a[NativeBrickfallScenery.North].MinZ>=38&&a[NativeBrickfallScenery.South].MaxZ<=-44&&a[NativeBrickfallScenery.East].MinX>=42&&a[NativeBrickfallScenery.West].MaxX<=-42,"sector meshes stay beyond the reach of point lights inside the arena (±30 / ±26 plus 12 m)");
            // The menu orbit (32 m up) never comes within 6 m of anything taller than 26 m.
            bool orbit=true;foreach(var m in a)for(int t=0;t<m.Indices.Length&&orbit;t+=3)
            {
                int i0=m.Indices[t],i1=m.Indices[t+1],i2=m.Indices[t+2];if(Math.Max(m.Positions[i0*3+1],Math.Max(m.Positions[i1*3+1],m.Positions[i2*3+1]))<=26)continue;
                double cx=(m.Positions[i0*3]+m.Positions[i1*3]+m.Positions[i2*3])/3,cz=(m.Positions[i0*3+2]+m.Positions[i1*3+2]+m.Positions[i2*3+2])/3;if(cx*cx+cz*cz>75*75)continue;
                if(NativeBrickfallScenery.OrbitDistance(cx,cz)<6||NativeBrickfallScenery.OrbitDistance(m.Positions[i0*3],m.Positions[i0*3+2])<6)orbit=false;
            }
            Check(orbit,"the menu overview orbit flies clear of everything taller than 26 m");
            // Nothing within reach of a jib (42 m) rises to the hook (mast top 45 m minus the hoist), except the mast itself.
            var jib=NativeBrickfallScenery.BuildJib();double hook=NativeBrickfallScenery.MastHeight+jib.MinY;Check(hook>39.5&&hook<41,"the hook hangs at about 40 m ("+hook+")");
            bool sweep=true;foreach(var m in a)for(int i=0;i<m.Vertices&&sweep;i++)for(int c=0;c<2;c++)
            {double dx=m.Positions[i*3]-NativeBrickfallScenery.CraneX[c],dz=m.Positions[i*3+2]-NativeBrickfallScenery.CraneZ[c],d=Math.Sqrt(dx*dx+dz*dz);if(d>4&&d<NativeBrickfallScenery.JibLength+2&&m.Positions[i*3+1]>hook-.5)sweep=false;}
            Check(sweep,"nothing but the masts stands in the jib sweep below the hook");
            Check(NativeBrickfallScenery.BrickRed==0x9C4A30&&NativeBrickfallScenery.Mortar==0xCDBB9E&&NativeBrickfallScenery.Buff==0xB8774E&&NativeBrickfallScenery.Terracotta==0xE3C9A0&&NativeBrickfallScenery.Green==0x3E6B5A&&NativeBrickfallScenery.Rust==0x7A3E22&&NativeBrickfallScenery.Yellow==0xF4B321&&NativeBrickfallScenery.Lit==0xFFB45E&&NativeBrickfallScenery.Neon==0x4FA8FF&&NativeBrickfallScenery.Dust==0xB8A68F&&NativeBrickfallScenery.Soot==0x4A403A,"§3.10 palette");
            Check(NativeBrickfallScenery.Active("brickfall")&&!NativeBrickfallScenery.Active("moonlit")&&!NativeBrickfallScenery.Active(null),"dormant on every other map");
            // The builder's exclusion: a beam straddling the arena with every corner outside is refused; a box just outside passes.
            bool refused=false;try{var x=new NativeSceneryBuilder(1){ExcludeX=34,ExcludeZ=30};x.Box(0,1,0,90,1,.5,0xffffff,yaw:30);}catch(InvalidOperationException){refused=true;}Check(refused,"a box crossing the arena is refused");
            var ok=new NativeSceneryBuilder(1){ExcludeX=34,ExcludeZ=30};ok.Box(35,1,0,1.9,1,4,0xffffff);ok.Box(0,1,31,4,1,1.9,0xffffff);Check(ok.Count(0)==2,"boxes beyond the line are accepted");
            var tiny=new NativeSceneryBuilder(1);tiny.Box(0,.5,0,1,1,1,0xffffff);var tm=tiny.Bake(0);Check(tm.Vertices==20&&tm.Indices.Length==30,"a box on the ground drops its bottom face");
            // Winding: every face's (b-a)x(c-a) points along its normal (Unity front faces).
            bool wind=true;for(int t=0;t<tm.Indices.Length;t+=3){int i0=tm.Indices[t],i1=tm.Indices[t+1],i2=tm.Indices[t+2];double ux=tm.Positions[i1*3]-tm.Positions[i0*3],uy=tm.Positions[i1*3+1]-tm.Positions[i0*3+1],uz=tm.Positions[i1*3+2]-tm.Positions[i0*3+2],vx=tm.Positions[i2*3]-tm.Positions[i0*3],vy=tm.Positions[i2*3+1]-tm.Positions[i0*3+1],vz=tm.Positions[i2*3+2]-tm.Positions[i0*3+2];double cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;if(cx*tm.Normals[i0*3]+cy*tm.Normals[i0*3+1]+cz*tm.Normals[i0*3+2]<=0)wind=false;}
            Check(wind,"faces wind clockwise seen from outside");
        }
        private static void Moving()
        {
            var train=NativeBrickfallScenery.BuildTrain();
            for(int n=0;n<4;n++)
            {
                double t0=n*NativeBrickfallScenery.TrainPeriod;
                Check(!NativeBrickfallScenery.Train(t0+NativeBrickfallScenery.TrainStart-.1,out _,out _),"period "+n+": no train before it starts");
                Check(NativeBrickfallScenery.Train(t0+NativeBrickfallScenery.TrainStart+1,out double h1,out bool east)&&east==(n%2==0),"period "+n+": the train runs "+(n%2==0?"east":"west"));
                NativeBrickfallScenery.Train(t0+NativeBrickfallScenery.TrainStart+2,out double h2,out _);Near(Math.Abs(h2-h1),NativeBrickfallScenery.TrainSpeed,"period "+n+" speed",1e-6);
                Check(!NativeBrickfallScenery.Train(t0+NativeBrickfallScenery.TrainStart+(NativeBrickfallScenery.TrainTo-NativeBrickfallScenery.TrainFrom+NativeBrickfallScenery.TrainLength)/NativeBrickfallScenery.TrainSpeed+.05,out _,out _),"period "+n+": the train is gone before the next one");
            }
            bool span=true,above=true;
            for(double t=0;t<95;t+=.25)
            {
                if(!NativeBrickfallScenery.Train(t,out double head,out bool east))continue;
                for(int i=0;i<train.Vertices;i++){double lx=train.Positions[i*3],ly=train.Positions[i*3+1],x=east?head+lx:head-lx;if(x<NativeBrickfallScenery.ViaductX0||x>NativeBrickfallScenery.ViaductX1)span=false;if(ly<0)above=false;}
            }
            Check(span,"the whole train stays on the viaduct (its ends are beyond the train's path)");Check(above,"the train rides on the rails");
            Check(NativeBrickfallScenery.TrainNorthZ<NativeBrickfallScenery.ViaductNorth&&NativeBrickfallScenery.TrainSouthZ>NativeBrickfallScenery.ViaductSouth&&NativeBrickfallScenery.TrainNorthZ-1.6<-NativeBrickfallScenery.InnerZ,"both tracks lie on the viaduct, outside the arena");
            var jib=NativeBrickfallScenery.BuildJib();bool clear=true,range=true;
            for(double t=0;t<320;t+=.5)for(int c=0;c<2;c++){if(!NativeBrickfallScenery.JibClear(c,t,jib))clear=false;double s=NativeBrickfallScenery.CraneSlew(c,t);if(c==0&&(s<-100-1e-9||s>90+1e-9)||c==1&&(s<80-1e-9||s>270+1e-9))range=false;}
            Check(clear,"every jib vertex stays outside the arena through the slew cycle");Check(range,"slews stay between their picks");
            Near(NativeBrickfallScenery.CraneSlew(0,0),-100,"north crane holds its first pick");Near(NativeBrickfallScenery.CraneSlew(0,150*.6),90,"north crane holds its second pick");
            int on=0;for(int i=0;i<3000;i++)if(NativeBrickfallScenery.AviationOn(0,1,i*.0137))on++;Check(on>3000*.36&&on<3000*.44,"aviation lights flash 40% of the time ("+on+")");
            for(int l=0;l<NativeBrickfallScenery.AviationLights;l++){NativeBrickfallScenery.AviationLocal(l,out _,out double ly,out _);Check(ly>0,"aviation light "+l+" sits on top of the jib");}
            bool birds=true;for(double t=0;t<240;t+=.7)for(int f=0;f<NativeBrickfallScenery.Flocks;f++)for(int i=0;i<NativeBrickfallScenery.FlockBirds;i++){NativeBrickfallScenery.Bird(f,i,t,out double x,out double y,out double z,out _,out double flap);if(Math.Abs(x)<NativeBrickfallScenery.InnerX+1&&Math.Abs(z)<NativeBrickfallScenery.InnerZ+1||y<35||Math.Abs(flap)>1)birds=false;}
            Check(birds,"bird flocks fly high and outside the arena");
            for(int c=0;c<NativeBrickfallScenery.SmokeColumns;c++)
            {
                // Puff 0 at two ages in the same cycle (the age is time/life + the column's phase).
                double phase=c*.13,ta=(1-phase)*NativeBrickfallScenery.SmokeLife+2,tb=ta+18;
                NativeBrickfallScenery.SmokePuff(c,0,ta,out double xa,out double ya,out double za,out double sa,out double aa);NativeBrickfallScenery.SmokePuff(c,0,tb,out double xb,out double yb,out _,out double sb,out double ab);
                Check(yb>ya+40&&xb>xa+10&&sb>sa&&aa>=0&&aa<=1&&ab>=0&&ab<=1,"smoke column "+c+" rises, leans east and swells");
                Check(Math.Sqrt(xa*xa+za*za)>80&&Math.Sqrt(xa*xa+za*za)<110,"smoke column "+c+" rises in the far row, inside the fog's reach");
            }
        }
        private static void Atmosphere()
        {
            Check(NativeBrickfallAtmosphere.Count(200,1,false)==200&&NativeBrickfallAtmosphere.Count(200,.3,false)==60&&NativeBrickfallAtmosphere.Count(200,1,true)==100&&NativeBrickfallAtmosphere.Count(30,.3,false)==9,"counts follow quality and halve on performance");
            Check(NativeBrickfallAtmosphere.Count(6,1,false,true)==6&&NativeBrickfallAtmosphere.Count(6,.3,false,true)==3&&NativeBrickfallAtmosphere.Count(6,1,true,true)==0,"six shafts, three on reduced, none on performance");
            Check(NativeBrickfallAtmosphere.RoundStart(NativePhase.Countdown,10,40)==40&&NativeBrickfallAtmosphere.RoundStart(NativePhase.Intro,10,40)==40&&NativeBrickfallAtmosphere.RoundStart(NativePhase.Fight,10,40)==10&&NativeBrickfallAtmosphere.RoundStart(NativePhase.Result,10,40)==10,"round start for the dust a rebuild wipes");
            bool motes=true;var rng=new Random(7);
            for(int k=0;k<40;k++){double cx=rng.NextDouble()*60-30,cy=rng.NextDouble()*12,cz=rng.NextDouble()*52-26,t=rng.NextDouble()*500;for(int i=0;i<NativeBrickfallAtmosphere.Motes;i++){NativeBrickfallAtmosphere.Mote(i,cx,cy,cz,t,out double x,out double y,out double z,out double s,out double al);if(Math.Abs(x-cx)>8.001||Math.Abs(y-cy)>8.001||Math.Abs(z-cz)>8.001||al<0||al>1||y<.05&&al!=0||s<=0)motes=false;}}
            Check(motes,"motes stay in the camera's box, none below the floor");
            bool scraps=true;for(double t=0;t<300;t+=.37)for(int i=0;i<NativeBrickfallAtmosphere.Scraps;i++){NativeBrickfallAtmosphere.Scrap(i,t,out double x,out double y,out double z,out _,out _,out _,out double al);if(Math.Abs(x)>30||Math.Abs(z)>26||y<.03||y>3.5||al<0||al>1)scraps=false;}
            Check(scraps,"paper scraps stay on the arena's streets, under 3.5 m");
            // Pigeons: perched, scattered by a scare within 18 m, back 25 s after the last scare, no jumps, killcam-safe memory.
            var mem=new NativeBrickfallMemory();var st=new double[NativeBrickfallMemory.PerPigeon];
            NativeBrickfallAtmosphere.Perch(0,out double px,out double py,out double pz,out _);Check(Math.Abs(py-14.02)<1e-9&&Math.Abs(pz)>26-.01,"pigeons perch on the 14 m frontage top");
            NativeBrickfallAtmosphere.Pigeon(0,50,0,out double x0,out double y0,out double z0,out _,out double f0,out double w0,out _);Check(x0==px&&y0==py&&z0==pz&&f0==0&&w0==0,"a calm pigeon sits on its perch, wings folded");
            mem.Spook(100,px+30,0,pz);Check(mem.Spooks(0,st)==0,"a scare 30 m away is ignored");
            mem.Spook(100,px,0,pz-4);mem.Spook(100,px,0,pz-4);Check(mem.Spooks(0,st)==1,"the same scare fed twice counts once");
            double A(double t){int n=mem.Spooks(0,st);return NativeBrickfallAtmosphere.Away(t,st,n);}
            Check(A(99.9)==0&&A(100.6)>0&&A(100.6)<1&&A(105)==1&&A(124.9)==1&&A(131)==0,"scatter at the scare, out until 25 s, home by 29 s");
            mem.Spook(118,px,1,pz-6);double last=A(100),worst=0;for(double t=100;t<160;t+=.01){double v=A(t);worst=Math.Max(worst,Math.Abs(v-last));last=v;}
            Check(worst<.03,"a second scare mid-flight keeps the pigeon out without a jump ("+worst+")");Check(A(142)==1&&A(148)==0,"it returns 25 s after the last scare");
            Check(A(99)==0,"a killcam frame before the scare sees the pigeon perched");
            for(int k=0;k<6;k++)mem.Spook(200+k,px,0,pz);Check(mem.Spooks(0,st)==NativeBrickfallMemory.PerPigeon&&st[0]==202&&st[3]==205,"the newest four scares are kept, oldest first");
            NativeBrickfallAtmosphere.Pigeon(0,215,1,out double fx,out double fy,out double fz,out _,out double ff,out double fw,out _);Check(fy>py+5&&fw==1&&Math.Abs(ff)<=1,"a scared pigeon flies its loop over the wall");
            // Tarps hang on the frontage above 4 m and billow within a metre of the wall.
            bool tarps=true;for(int t=0;t<NativeBrickfallAtmosphere.Tarps;t++){NativeBrickfallAtmosphere.TarpNormal(t,out double nx,out double nz);for(double time=0;time<60;time+=.9)for(int r=0;r<NativeBrickfallAtmosphere.TarpRows;r++)for(int c=0;c<NativeBrickfallAtmosphere.TarpCols;c++)
            {NativeBrickfallAtmosphere.TarpVertex(t,c,r,time,out double x,out double y,out double z);double wall=Math.Abs(nx)>.5?30:26,off=Math.Abs(nx)>.5?wall-Math.Abs(x):wall-Math.Abs(z);if(y<NativeBrickfallAtmosphere.TarpMinHeight||off<.05||off>1.1)tarps=false;}}
            Check(tarps,"tarps hang above 4 m within a metre of the frontage");
            for(int i=0;i<NativeBrickfallAtmosphere.Shafts;i++){NativeBrickfallAtmosphere.Shaft(i,i*7.3,out double x,out double y,out double z,out double l,out double w,out double al);Check(Math.Abs(x)<30&&Math.Abs(z)<26&&y>3&&l>w&&al>0&&al<.13,"shaft "+i+" hangs in the arena air");}
            Check(NativeBrickfallAtmosphere.ShaftView(.5,0)==0&&NativeBrickfallAtmosphere.ShaftView(10,1)==0&&NativeBrickfallAtmosphere.ShaftView(10,0)==1,"shafts fade near the eye and edge-on");
            // Collapse dust and the dust coat.
            var dust=new NativeBrickfallMemory();Near(NativeBrickfallAtmosphere.DustCoat(dust,100,0),0,"no dust coat before a collapse");
            dust.Collapse(50,0,0,0,8,1);dust.Collapse(50,0,0,0,8,1);Check(dust.CollapseCount==1,"a landing fed twice counts once");
            double c0=NativeBrickfallAtmosphere.DustCoat(dust,50,0),c1=NativeBrickfallAtmosphere.DustCoat(dust,54,0),c2=NativeBrickfallAtmosphere.DustCoat(dust,70,0);Check(c0==0&&c1>0&&c2>c1&&c2<1,"the dust coat rises as a landing settles");
            Near(NativeBrickfallAtmosphere.DustCoat(dust,49,0),0,"a killcam frame before the landing has no dust coat");Near(NativeBrickfallAtmosphere.DustCoat(dust,70,60),0,"a rebuilt round starts clean");
            for(int k=0;k<40;k++)dust.Collapse(60+k,k,0,0,6,.5);Check(dust.CollapseCount==NativeBrickfallMemory.Collapses&&NativeBrickfallAtmosphere.DustCoat(dust,200,0)<1,"landings are capped and the coat saturates below 1");
            NativeBrickfallAtmosphere.DustPuff(1,3,0,0,0,8,1,2,out double dx0,out _,out _,out _,out double da0);NativeBrickfallAtmosphere.DustPuff(1,3,0,0,0,8,1,12,out double dx1,out _,out _,out _,out double da1);
            Check(Math.Abs((dx1-dx0)-10*NativeBrickfallAtmosphere.DustDrift)<1.3&&da0>da1&&da1>0,"collapse dust drifts east at 0.7 m/s and thins");
            NativeBrickfallAtmosphere.DustPuff(1,3,0,0,0,8,1,NativeBrickfallAtmosphere.DustLife,out _,out _,out _,out _,out double daEnd);Near(daEnd,0,"collapse dust is gone after its life");
            Check(NativeBrickfallAtmosphere.DustCoatGlobal=="_ArenaDustCoat","the dust coat shader global");
        }
        private static void Fx()
        {
            var fx=new NativeBrickfallFx();var light=new NativeFxLight{Lx=-.53,Ly=.5,Lz=.68,Sr=1.5,Sg=.8,Sb=.3};for(int i=0;i<9;i++)light.Ambient[i]=.05;
            var mem=new NativeBrickfallMemory();mem.Spook(10,-20,0,22);mem.Collapse(12,-5,0,3,8,1);mem.Collapse(14,6,0,-3,6,.6);
            void Frame(double t,double q,bool low,bool atmo)=>fx.Build(t,0,12.2,4,1,0,0,0,1,0,light,q,low,atmo,mem,0);
            for(int i=0;i<5;i++)Frame(20+i*.016,1,false,true);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<60;i++)Frame(21+i*.016,1,false,true);long after=GC.GetAllocatedBytesForCurrentThread();
            Check(after==before,"the frame builder allocates nothing ("+(after-before)+" bytes over 60 frames)");
            Frame(22,1,false,true);int full=fx.Quads.Count;Check(full<fx.Quads.Capacity*.8&&fx.Shafts>=4&&fx.Pigeons==14&&fx.Birds==48&&fx.DustPuffs==24,"full quality fits the batch ("+full+" quads, "+fx.Shafts+" shafts)");
            var copy=(float[])fx.Quads.Corners.Clone();Frame(22,1,false,true);Check(fx.Quads.Corners.SequenceEqual(copy),"the same clock and camera build the same quads");
            Frame(22,.3,false,true);Check(fx.Quads.Count<full*.75&&fx.Shafts<=3&&fx.Pigeons==7&&fx.Birds==24&&fx.DustPuffs==8,"reduced effects thin the atmosphere");
            Frame(22,1,true,true);Check(fx.Shafts==0&&fx.Pigeons==7,"graphics=performance drops the shafts and halves the life");
            Frame(22,1,false,false);Check(fx.Shafts+fx.Motes+fx.Scraps+fx.Pigeons+fx.DustPuffs==0&&fx.Birds==48&&fx.Quads.Count>0,"behind the menu only the backdrop life is built");
            bool layers=true;for(int i=0;i<fx.Quads.Count;i++){if(fx.Quads.Layers[i]>1||fx.Quads.Modes[i]>1||fx.Quads.Colors[i*4+3]<=0||fx.Quads.Colors[i*4+3]>1)layers=false;}Check(layers,"quads carry a layer, mode and opacity");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var source=R("NativeSourcePresentation.cs");Check(source.Contains("BrickfallScenery(arena);"),"SourceWorld (arena and killcam replay) draws the scenery");
            var backdrop=R("NativeBackdropPresentation.cs");Check(backdrop.Contains("Camera camera=null")&&backdrop.Contains("BrickfallBackdrop(mapId,time,camera);"),"the menu backdrop draws the scenery");
            Check(R("ConversionGame.cs").Contains("RenderBackdrop(content.sourceCommit,map,Time.unscaledTime,cameraView)"),"the menu passes its camera");
            Check(R("NativeSettings.cs").Contains("presentation.LowGraphics=low;"),"graphics=performance reaches the presentation");
            Check(R("ArenaPresentation.cs").Contains("DisposeBrickfall();"),"the scenery is released with the presentation");
            var p=R("NativeBrickfallPresentation.cs");
            Check(p.Contains("if(!NativeBrickfallScenery.Active(arena.MapId)){BrickfallDormant();return;}")&&p.Contains("if(!NativeBrickfallScenery.Active(mapId)){BrickfallDormant();return;}"),"dormant unless the map is brickfall");
            Check(!p.Contains("AddComponent")&&!p.Contains("new GameObject")&&!p.Contains("Collider"),"no GameObjects or colliders");
            int draws=0,offs=0;int at=0;while((at=p.IndexOf("Graphics.DrawMesh",at,StringComparison.Ordinal))>=0){int open=at+"Graphics.DrawMesh".Length;if(p[open]!='('&&!p.Substring(open).StartsWith("Instanced(",StringComparison.Ordinal)){at=open;continue;}draws++;int end=p.IndexOf(';',at);if(p.Substring(at,end-at).Contains("ShadowCastingMode.Off"))offs++;at=end;}
            Check(draws>=4&&draws==offs,"every scenery draw casts no shadow");
            Check(p.Contains("arena.ReplicaScene.IsValid()")&&p.Contains("PresentationClock(arena)")&&p.Contains("e.Kind==EventKind.Explosion"),"the arena clock, the killcam memory rule and explosions feed the pigeons");
            Check(p.Contains("Shader.SetGlobalFloat(DustCoatId,value)")&&p.Contains("public void BrickfallStructure(NativeArena arena,NativeBrickfallCue cue,Vector3 position,float radius,float amount,float time)"),"the dust coat global and the structure hook");
            foreach(var f in new[]{"NativeSceneryBuilder.cs","NativeBrickfallScenery.cs","NativeBrickfallAtmosphere.cs","NativeBrickfallFx.cs"})Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f)).Contains("UnityEngine"),f+" is engine-free Core");
        }
    }
}
