using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory's backdrop (FACTORY_PROPOSAL.md 3.10 "Backdrop"): the rest of Corvane Works round the 80×64 hall, box-built and
    /// deterministic, baked once into five ChipArena/SourceLit sector meshes by Runtime/NativeFactoryPresentation.cs on
    /// <see cref="NativeSceneryBuilder.BackdropLayer"/> (no point light reaches it), for map id "factory" only. Only the ground
    /// plates reach the slab's edge (x ±<see cref="SlabX"/> / z ±<see cref="SlabZ"/>); everything standing lies beyond
    /// x ±<see cref="InnerX"/> / z ±<see cref="InnerZ"/> (NativeSceneryBuilder throws otherwise), and within 8 m of the menu's
    /// overview orbit nothing stands above <see cref="OrbitCap"/>.
    ///  - <see cref="Near"/>: the concrete apron and the outer ground to the fog, the perimeter road, the plant fence, four
    ///    floodlight masts, three rail sidings with sleepers along the south side.
    ///  - <see cref="North"/>: No. 3 blast furnace (banded shell, platform, uptakes, downcomer, dust catcher, skip incline), its cast
    ///    house, three hot-blast stoves and three banded smokestacks (<see cref="Stack"/>; plumes and aviation lights are
    ///    NativeFactoryFx).
    ///  - <see cref="East"/>: three sawtooth production sheds with clerestory glazing, the CORVANE WORKS sign over the nearest one
    ///    (read from the bay), the gas holder and a water tower.
    ///  - <see cref="South"/>: hopper wagons on the sidings, the ore and coke stockyard with its goliath gantry crane (its trolley
    ///    travels: <see cref="BuildTrolley"/>, <see cref="Trolley"/>), the sinter plant.
    ///  - <see cref="West"/>: the cooling tower, the substation with its gantries, a pylon line, the works office.
    /// Unity frame (x east, z north), metres; colours sRGB; allocation only while building.
    /// </summary>
    public static class NativeFactoryScenery
    {
        public const string MapId="factory";
        public static bool Active(string mapId)=>mapId==MapId;
        public const double SlabX=40,SlabZ=32,InnerX=41,InnerZ=33,OrbitCap=24;
        public const int Near=0,North=1,East=2,South=3,West=4,Sectors=5,Stacks=3;
        public const double FurnaceX=-16,FurnaceZ=96,FurnaceH=46,CoolingX=-128,CoolingZ=-14,CoolingH=66;
        private static readonly double[] StackX={34,50,-54},StackZ={66,76,72},StackH={58,52,56};
        /// <summary>Smokestack `c`: base position and height.</summary>
        public static void Stack(int c,out double x,out double z,out double h){x=StackX[c];z=StackZ[c];h=StackH[c];}
        public const int Concrete=0x8C8478,ConcreteDark=0x6A665E,Rust=0x6B4A3A,Steel=0x3A3E42,SteelLight=0x5E666C,Clad=0x7A8288,CladBlue=0x5E6A72,CladTan=0x8A7E6A,Roof=0x4A5056,
            Red=0xA8322A,White=0xD8D4CC,Green=0x4E6A5A,Asphalt=0x3E3C3A,Ground=0x5A564E,Apron=0x6A665E,Ore=0x6A4A3A,Coke=0x3A3634,Lime=0x8A8276,Timber=0x4A3A2C,Glass=0x2A3440,Lit=0xFFC27A,Yellow=0xE3A21A;
        public static double Hash(int a,int b=0,int c=0)=>NativeFactoryFx.Hash(a+101,b+7,c+13);
        public static double OrbitDistance(double x,double z)=>NativeBrickfallScenery.OrbitDistance(x,z);

        // ---------------- shapes ----------------
        /// <summary>An octagonal prism (two boxes turned 45°), apothem `r`.</summary>
        private static void Octagon(NativeSceneryBuilder b,double x,double y0,double y1,double z,double r,int hex,double shade=1,int glow=0,double level=0)
        {b.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r,hex,shade:shade,glow:glow,level:level);b.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r,hex,yaw:45,shade:shade,glow:glow,level:level);}
        /// <summary>A bar of section t × t between two points.</summary>
        private static void Bar(NativeSceneryBuilder b,double x0,double y0,double z0,double x1,double y1,double z1,double t,int hex,double shade=1)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-4)return;
            b.Box((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,t,t,l,hex,Math.Asin(Math.Max(-1,Math.Min(1,-dy/l)))*180/Math.PI,Math.Atan2(dx,dz)*180/Math.PI,shade:shade);
        }
        /// <summary>A lattice tower: four legs tapering from half width `w0` to `w1`, cross bracing every `step` m.</summary>
        private static void Lattice(NativeSceneryBuilder b,double x,double z,double h,double w0,double w1,double step,double t,int hex)
        {
            for(int k=0;k<4;k++){double sx=(k&1)==0?-1:1,sz=(k&2)==0?-1:1;Bar(b,x+sx*w0,0,z+sz*w0,x+sx*w1,h,z+sz*w1,t*1.4,hex);}
            for(double y=0;y<h-1e-6;y+=step)
            {
                double y1=Math.Min(h,y+step),a=w0+(w1-w0)*y/h,c=w0+(w1-w0)*y1/h;
                for(int f=0;f<4;f++){double s=f<2?-1:1;bool alongX=(f&1)==0;
                    if(alongX){Bar(b,x-a,y,z+s*a,x+c,y1,z+s*c,t,hex);Bar(b,x-c,y1,z+s*c,x+c,y1,z+s*c,t,hex);}else{Bar(b,x+s*a,y,z-a,x+s*c,y1,z+c,t,hex);Bar(b,x+s*c,y1,z-c,x+s*c,y1,z+c,t,hex);}}
            }
        }
        /// <summary>A ring of eight wall panels at radius r0 (bottom) to r1 (top), thickness t: one course of a tower shell.</summary>
        private static void Shell(NativeSceneryBuilder b,double x,double z,double y0,double y1,double r0,double r1,double t,int hex,double shade)
        {
            double r=(r0+r1)/2,w=2*r*Math.Tan(Math.PI/8)*1.04,lean=Math.Atan2(r0-r1,y1-y0)*180/Math.PI;
            for(int k=0;k<8;k++){double a=k*Math.PI/4;b.Box(x+Math.Sin(a)*r,(y0+y1)/2,z+Math.Cos(a)*r,w,(y1-y0)/Math.Cos(lean*Math.PI/180),t,hex,-lean,k*45,shade:shade*(1-.06*Math.Abs(Math.Cos(a-2.6))));}
        }
        /// <summary>A sawtooth-roofed shed: walls in `clad`, a clerestory band, teeth every 4 m with glazing (faces +z) along x.</summary>
        private static void Shed(NativeSceneryBuilder b,double x0,double x1,double z0,double z1,double h,int clad,int seed)
        {
            b.Box((x0+x1)/2,h/2,(z0+z1)/2,x1-x0,h,z1-z0,clad,shade:.92+.1*Hash(seed),grime:.15);
            for(double z=z0+2;z<z1-1;z+=4){b.Box((x0+x1)/2,h+1.1,z,x1-x0-.6,.25,4.4,Roof,-26,0);b.Box((x0+x1)/2,h+1.05,z+1.95,x1-x0-.6,2.1,.12,Glass,glow:0xB8C8D0,level:.08);}
            // clerestory glazing band and loading doors on the faces toward the hall
            double faceX=x0<0?x1:x0,o=x0<0?1:-1;b.Box(faceX+o*.05,h-3.2,(z0+z1)/2,.1,1.6,z1-z0-2,Glass,glow:Lit,level:.04);
            for(double z=z0+6;z<z1-4;z+=12)b.Box(faceX+o*.06,2.6,z,.12,5.2,4.6,CladBlue,shade:.85);
        }

        // ---------------- the sectors ----------------
        public static NativeSceneryMesh[] Build()
        {
            var b=new NativeSceneryBuilder(Sectors);
            b.Current=Near;GroundAndRails(b);
            b.ExcludeX=InnerX;b.ExcludeZ=InnerZ;
            b.Current=Near;NearRing(b);b.Current=North;NorthWorks(b);b.Current=East;EastSheds(b);b.Current=South;SouthYard(b);b.Current=West;WestWorks(b);
            var o=new NativeSceneryMesh[Sectors];for(int i=0;i<Sectors;i++)o[i]=b.Bake(i);return o;
        }
        private static void GroundAndRails(NativeSceneryBuilder b)
        {
            const double y=-.27,t=.5;
            b.Box(0,y,(SlabZ+120)/2,240,t,120-SlabZ,Apron,skip:1|2|16|32);b.Box(0,y,-(SlabZ+120)/2,240,t,120-SlabZ,Apron,skip:1|2|16|32);
            b.Box((SlabX+120)/2,y,0,120-SlabX,t,SlabZ*2,Apron,skip:1|2|16|32);b.Box(-(SlabX+120)/2,y,0,120-SlabX,t,SlabZ*2,Apron,skip:1|2|16|32);
            foreach(var (cx,cz,sx,sz) in new[]{(0.0,270.0,840.0,300.0),(0.0,-270.0,840.0,300.0),(270.0,0.0,300.0,240.0),(-270.0,0.0,300.0,240.0)})b.Box(cx,y-.01,cz,sx,t,sz,Ground,shade:.95);
            // the perimeter road, 6..12 m off the walls, with dashed centre lines
            foreach(double s in new[]{1.0,-1.0}){b.Box(0,-.015,s*(SlabZ+9),2*(SlabX+12),.02,6,Asphalt);b.Box(s*(SlabX+9),-.015,0,6,.02,2*(SlabZ+6),Asphalt);
                for(double x=-SlabX-10;x<SlabX+10;x+=4)b.Box(x,-.002,s*(SlabZ+9),2,.006,.15,White,shade:.8);for(double z=-SlabZ-4;z<SlabZ+4;z+=4)b.Box(s*(SlabX+9),-.002,z,.15,.006,2,White,shade:.8);}
            // three sidings along the south (rails and sleepers)
            foreach(double z in new[]{-46.0,-50.0,-54.0}){foreach(double d in new[]{-.72,.72})b.Box(0,.08,z+d,640,.15,.08,SteelLight);for(double x=-318;x<=318;x+=1.6)b.Box(x,.0,z,.24,.12,2.6,Timber,shade:.8+.2*Hash((int)x,(int)z));}
        }
        private static void NearRing(NativeSceneryBuilder b)
        {
            // the plant fence and four floodlight masts
            for(double x=-64;x<=64;x+=4)foreach(double s in new[]{1.0,-1.0}){b.Box(x,1.2,s*56,.08,2.4,.08,SteelLight);}
            for(double z=-56;z<=56;z+=4)foreach(double s in new[]{1.0,-1.0}){b.Box(s*64,1.2,z,.08,2.4,.08,SteelLight);}
            foreach(double s in new[]{1.0,-1.0}){b.Box(0,2.35,s*56,128,.05,.05,SteelLight);b.Box(s*64,2.35,0,.05,.05,112,SteelLight);b.Box(0,1.2,s*56,128,2.2,.01,SteelLight,shade:.5);b.Box(s*64,1.2,0,.01,2.2,112,SteelLight,shade:.5);}
            foreach(var (x,z) in new[]{(54.0,46.0),(-54.0,-46.0),(-54.0,46.0),(54.0,-46.0)})
            {
                b.Box(x,14,z,.7,28,.7,SteelLight);b.Box(x,28.4,z,3.4,.3,1.2,Steel);
                for(int k=0;k<4;k++)b.Box(x-1.2+k*.8,28.9,z+(z>0?-.4:.4),.6,.6,.25,Steel,glow:0xFFF1D6,level:.6);
            }
            // low annexes against the shell (seen from the menu's orbit): pump house, compressor house, pallet store
            b.Box(-20,3,38,16,6,8,CladTan,grime:.2);b.Box(-20,6.2,38,16.4,.4,8.4,Roof);b.Box(24,2.5,-38.5,12,5,7,Clad,grime:.2);b.Box(24,5.2,-38.5,12.4,.4,7.4,Roof);
            b.Box(46,2,-20,6,4,10,CladBlue,grime:.15);b.Box(-46,2,20,6,4,10,CladBlue,grime:.15);
            for(int k=0;k<6;k++)b.Box(-48+k*1.4,.6,-36,1.2,1.2,1.2,Timber,shade:.8+.2*Hash(k,3));
        }
        private static void NorthWorks(NativeSceneryBuilder b)
        {
            double fx=FurnaceX,fz=FurnaceZ;
            // cast house
            b.Box(fx-2,8,78,40,16,14,CladTan,grime:.25);b.Box(fx-2,16.6,78,38,1.2,10,Roof);b.Box(fx-2,17.6,78,8,1.2,9,Glass,glow:Lit,level:.05);
            // the furnace: hearth, bosh and banded stack, the top platform, four uptakes, the downcomer and dust catcher, the skip incline
            Octagon(b,fx,0,14,fz,7.5,Rust,.92);Octagon(b,fx,14,22,fz,8.4,Rust);Octagon(b,fx,22,30,fz,7.2,Rust,.95);Octagon(b,fx,30,38,fz,6.3,Rust,.9);Octagon(b,fx,38,FurnaceH,fz,5.4,Rust,.85);
            foreach(double y in new[]{13.7,21.7,29.7,37.7})Octagon(b,fx,y,y+.6,fz,y<20?8.0:y<28?8.8:y<36?7.6:6.7,Steel);
            b.Box(fx,FurnaceH+.3,fz,13,.6,13,Steel);for(int k=0;k<4;k++){double a=k*Math.PI/2+Math.PI/4;Bar(b,fx+Math.Sin(a)*4,FurnaceH,fz+Math.Cos(a)*4,fx+Math.Sin(a)*2.5,FurnaceH+10,fz+Math.Cos(a)*2.5,1.4,Rust);}
            b.Box(fx,FurnaceH+10.4,fz,6,1.4,6,Rust);Bar(b,fx+3,FurnaceH+10,fz,24,26,fz+6,2.2,Rust);Octagon(b,26,0,26,fz+6,4.2,Rust,.9);
            Bar(b,fx-1,FurnaceH,fz+4,fx-1,2,fz+44,1.6,Steel);Bar(b,fx+1,FurnaceH,fz+4,fx+1,2,fz+44,1.6,Steel);for(double t=.1;t<1;t+=.12){double y=FurnaceH+(2-FurnaceH)*t,z=fz+4+40*t;b.Box(fx,y,z,2.6,.3,.3,Steel);}
            // hot-blast stoves
            for(int k=0;k<3;k++){double x=fx+22+k*10,z=fz+22;Octagon(b,x,0,34,z,4.2,SteelLight,.9+.05*k);Octagon(b,x,34,36,z,3.6,SteelLight,.85);Octagon(b,x,36,37.4,z,2.6,SteelLight,.8);Bar(b,x,22,z-4,x,22,fz+8,1.0,Rust);}
            // smokestacks with red and white bands and a gallery near the top
            for(int c=0;c<Stacks;c++)
            {
                double x=StackX[c],z=StackZ[c],h=StackH[c];Octagon(b,x,0,h-10,z,2.8,Concrete,.95);
                for(int k=0;k<4;k++)Octagon(b,x,h-10+k*2.5,h-7.5+k*2.5,z,2.5,k%2==0?Red:White);Octagon(b,x,h-6.2,h-5.8,z,3.4,Steel);Octagon(b,x,h,h+.4,z,2.7,Steel);
            }
            // pipe bridges toward the hall (stopping short of the shell)
            foreach(double x in new[]{-30.0,8.0}){b.Box(x,10,52,1.2,.8,36,Steel);for(double z=36;z<=68;z+=8)b.Box(x,5,z,.4,10,.4,Steel);b.Box(x,10.9,52,.7,.7,36,SteelLight);}
        }
        private static void EastSheds(NativeSceneryBuilder b)
        {
            Shed(b,48,72,-62,-12,14,Clad,1);Shed(b,48,72,-2,52,15,CladBlue,2);Shed(b,82,112,-44,44,16,CladTan,3);
            // the CORVANE WORKS board on the roof of the near shed, facing the hall (read from the bay)
            // (kept under the menu orbit's 24 m cap: it stands within 8 m of the orbit)
            b.Box(48.6,19.6,26,.4,6.8,30,Steel);for(double z=12;z<=40;z+=7)b.Box(49,15.6,z,.3,1.2,.3,Steel);
            Letters(b,"CORVANE",48.35,19.2,40.0,.82,Yellow,.0);Letters(b,"WORKS",48.35,16.5,33.0,.52,White,.0);
            // gas holder: guide columns, ring girders, the bell
            double gx=104,gz=-82;Octagon(b,gx,0,28,gz,16.5,Green,.9);Octagon(b,gx,28,29,gz,15,Green,.8);
            for(int k=0;k<12;k++){double a=k*Math.PI/6;b.Box(gx+Math.Sin(a)*18,21,gz+Math.Cos(a)*18,.9,42,.9,Green,yaw:k*30);}
            foreach(double y in new[]{14.0,28.0,42.0})for(int k=0;k<12;k++){double a=(k+.5)*Math.PI/6;b.Box(gx+Math.Sin(a)*17.4,y,gz+Math.Cos(a)*17.4,9.4,.6,.6,Green,yaw:k*30+105);}
            // water tower
            double wx=62,wz=64;for(int k=0;k<4;k++){double sx=(k&1)==0?-1:1,sz=(k&2)==0?-1:1;Bar(b,wx+sx*3.4,0,wz+sz*3.4,wx+sx*2.4,22,wz+sz*2.4,.5,Steel);}Octagon(b,wx,22,28,wz,3.6,SteelLight);Octagon(b,wx,28,29.2,wz,2.6,SteelLight,.8);
        }
        private static void SouthYard(NativeSceneryBuilder b)
        {
            // hopper wagons on the middle siding and a shunter at their head
            for(int k=0;k<9;k++){double x=-130+k*12.5;b.Box(x,2.4,-50,11,3.2,3,k%3==0?Rust:Steel,shade:.85+.1*Hash(k,9));b.Box(x,.7,-50,10,.6,2.4,Steel,shade:.6);foreach(double d in new[]{-3.5,3.5})b.Box(x+d,.45,-50,1,.9,2.7,Steel,shade:.5);}
            b.Box(-14,2.6,-50,12,3.8,3.1,Yellow,grime:.3);b.Box(-10,4.6,-50,4,1.6,3.0,Yellow);b.Box(-10,4.9,-48.4,3.4,1.0,.05,Glass,glow:Lit,level:.08);
            // the stockyard: stepped piles of ore, coke and limestone, the goliath crane's legs and girder
            foreach(var (x,hex,h) in new[]{(-48.0,Ore,9.0),(-18.0,Coke,8.0),(12.0,Lime,7.0),(40.0,Ore,10.0)})for(int s=0;s<5;s++){double w=22-s*4,d=16-s*2.6;b.Box(x,h*(s+.5)/5,-96,w,h/5,d,hex,shade:.92+.06*s);}
            foreach(double x in new[]{-62.0,62.0}){Bar(b,x,0,-104,x,30,-96,1.2,Yellow);Bar(b,x,0,-88,x,30,-96,1.2,Yellow);b.Box(x,.6,-96,2,1.2,18,Steel);}
            b.Box(0,31.4,-96,132,2.8,2.4,Yellow);b.Box(0,33,-96,132,.4,3.2,Steel);
            // the sinter plant with its conveyor to the yard
            b.Box(-84,15,-66,30,30,20,CladTan,grime:.3);b.Box(-84,30.6,-66,26,1.2,16,Roof);Bar(b,-70,24,-70,-30,4,-96,2.4,Steel);
            Octagon(b,-96,30,44,-60,2.2,Concrete,.9);
        }
        private static void WestWorks(NativeSceneryBuilder b)
        {
            // the cooling tower: ten courses of a waisted shell and the rim
            for(int k=0;k<10;k++){double y0=k*CoolingH/10,y1=(k+1)*CoolingH/10,f0=y0/CoolingH,f1=y1/CoolingH;double R(double f)=>18-8.5*Math.Sin(Math.PI*f*.78);Shell(b,CoolingX,CoolingZ,y0,y1,R(f0),R(f1),.7,Concrete,.95-.02*k);}
            for(int k=0;k<16;k++){double a=k*Math.PI/8;Bar(b,CoolingX+Math.Sin(a)*18.4,0,CoolingZ+Math.Cos(a)*18.4,CoolingX+Math.Sin(a+.2)*17.6,5,CoolingZ+Math.Cos(a+.2)*17.6,.6,ConcreteDark);}
            // the substation: transformers, gantries, insulators
            for(int k=0;k<6;k++){double x=-58-(k%3)*7,z=12+(k/3)*9;b.Box(x,1.6,z,4,3.2,3,Green,shade:.9);b.Box(x,3.4,z,1.2,.4,1.2,Steel);for(int i=0;i<3;i++)b.Box(x-1.2+i*1.2,4.2,z,.25,1.2,.25,White,shade:.9);}
            foreach(double x in new[]{-52.0,-74.0})Lattice(b,x,16,12,1.2,.8,3,.2,SteelLight);b.Box(-63,12,16,24,.3,.3,SteelLight);
            // a pylon line west and its conductors
            double[] px={-60,-140,-220};for(int k=0;k<3;k++){Lattice(b,px[k],-46-k*8,36,3.4,1.0,4,.28,SteelLight);b.Box(px[k],30,-46-k*8,.4,.4,14,SteelLight);b.Box(px[k],34,-46-k*8,.4,.4,10,SteelLight);}
            for(int k=0;k<2;k++)foreach(double dz in new[]{-6.6,0.0,6.6}){double x0=px[k],x1=px[k+1],z0=-46-k*8+dz,z1=-46-(k+1)*8+dz,mx=(x0+x1)/2,mz=(z0+z1)/2;Bar(b,x0,29.6,z0,mx,25.5,mz,.08,Steel);Bar(b,mx,25.5,mz,x1,29.6,z1,.08,Steel);}
            // the works office: four storeys, a window grid with some lit panes
            double ox=-70,oz=58;b.Box(ox,8,oz,40,16,12,Concrete,grime:.15);b.Box(ox,16.3,oz,40.4,.6,12.4,ConcreteDark);
            for(int f=0;f<4;f++)for(int i=0;i<12;i++){double x=ox-17.5+i*3.2,y=2.2+f*3.8;bool lit=Hash(i,f,5)<.35;b.Box(x,y,oz-6.04,2,1.6,.1,Glass,glow:lit?Lit:0,level:lit?.35:0);}
        }
        /// <summary>A line of capital letters in a 3×5 cell font on a face at x (facing -x), the text running toward -z from z0.</summary>
        private static void Letters(NativeSceneryBuilder b,string text,double x,double y0,double z0,double cell,int hex,double level)
        {
            for(int i=0;i<text.Length;i++){int g=Glyph(text[i]);double zc=z0-i*cell*4;for(int row=0;row<5;row++)for(int col=0;col<3;col++)if(((g>>(14-(row*3+col)))&1)!=0)
                b.Box(x,y0+(4-row)*cell+cell/2,zc-col*cell-cell/2,.14,cell*.94,cell*.94,hex,glow:hex,level:.12+level);}
        }
        private static int Glyph(char c)
        {
            switch(c){case 'C':return 0x7927;case 'O':return 0x7B6F;case 'R':return 0x6BAD;case 'V':return 0x5B6A;case 'A':return 0x2BED;case 'N':return 0x6B6D;case 'E':return 0x79E7;case 'W':return 0x5B7D;case 'K':return 0x5BAD;case 'S':return 0x79CF;default:return 0;}
        }

        /// <summary>The goliath crane's trolley: travels the stockyard girder (±54 m) and back every 96 s, pausing at each end.</summary>
        public static void Trolley(double time,out double x,out double y,out double z){double u=(time%96)/96,s=u<.5?u*2:2-u*2;s=NativeFactoryHazards.Smooth(Math.Max(0,Math.Min(1,(s-.08)/.84)));x=-54+108*s;y=31.4;z=-96;}
        public static NativeSceneryMesh BuildTrolley()
        {
            var b=new NativeSceneryBuilder(1);b.Box(0,2.0,0,7,2.6,5.2,Yellow,grime:.2);b.Box(0,-1.6,1.6,2.2,2.2,2.2,Yellow);b.Box(0,-1.6,2.71,1.8,1.2,.05,Glass,glow:Lit,level:.08);
            for(int k=0;k<2;k++)b.Box(k==0?-.5:.5,-9,0,.06,18,.06,Steel);b.Box(0,-18.4,0,1.2,.8,.8,Yellow);return b.Bake(0);
        }
        /// <summary>The menu's plate over the slab (the menu draws no arena blocks).</summary>
        public static NativeSceneryMesh BuildMenuPlate(){var b=new NativeSceneryBuilder(1);b.Box(0,-.27,0,SlabX*2,.5,SlabZ*2,0x6E6A63,shade:.9,skip:1|2|16|32);return b.Bake(0);}

        /// <summary>A stack or cooling-tower plume puff (`c` = Stacks for the cooling tower): rising, leaning downwind, growing and fading.</summary>
        public static void Plume(int c,int puff,double time,double x0,double h,double z0,out double x,out double y,out double z,out double size,out double alpha)
        {
            double life=c==Stacks?34:24,t=(time/life+Hash(puff,c,3))%1,rise=c==Stacks?28:36;
            x=x0+(6+22*t)*t*1.2+(Hash(puff,c,4)-.5)*3*t;z=z0+(3+10*t)*t*.6+(Hash(puff,c,5)-.5)*3*t;y=h+rise*t*(1-.35*t);size=(c==Stacks?7:3)+(c==Stacks?16:11)*t;alpha=.55*(1-t)*Math.Min(1,t*8);
        }
    }
}
