using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The five M1 stages' backdrops (docs/arenas/NEW-STAGES.md "Stage looks (presentation)"): what stands round each map, box-built
    /// and deterministic, baked once into ChipArena/SourceLit sector meshes (Runtime/NativeNewStagePresentation.cs draws them on
    /// <see cref="NativeSceneryBuilder.BackdropLayer"/> without shadows). These draw with or without the kit bank: they are the
    /// stages' own ground, water and skyline, and the bank's yards (PackDressing.json) only add to them.
    /// <list type="bullet">
    /// <item>Breakwater: the blue-hour sea in faceted swell, the pier's stone body and its armour of tetrapods, the causeway to the
    /// shore with its lamps, the parapet's lamp posts outside the wall, the moored tug and the channel buoys, the lighthouse on its
    /// plinth past the east parapet, the harbour town and its cranes in the west under the last of the light.</item>
    /// <item>Pier 9: the basin's black water under the quay face and its fenders, the freighter alongside past the bounds with its
    /// container bays and lit house, the terminal on beyond the fences (stacks, masts, quay and two more ship-to-shore cranes),
    /// the sheds south, the city and a lit bridge in the distance.</item>
    /// <item>Skyframe: floor 31's tower under the slab (the open frame of floors 30 and 29, then the glazed floors down to the
    /// street 112 m below, a hoist mast with its car parked low), the neighbours behind the party walls, and the city round it to
    /// the haze: towers on a street grid, rooftops, two more tower cranes.</item>
    /// <item>Deepwell: the storm sea, the jacket under the cellar deck (legs and braces clear of every landing), lifeboats in their
    /// davits past the lifeboat stations, the flare boom leaning north with its tip, a standby vessel and distant platforms.</item>
    /// <item>Polaris: the ice shelf past the snow banks (sastrugi, flag lines, a skiway's lights), outbuildings, an antenna field
    /// and a wind turbine, the nunataks on the horizon.</item>
    /// </list>
    /// The keep-out (<see cref="Enters"/>, thrown on while building): nothing stands inside the map's bounds grown by
    /// <see cref="Margin"/> m, except what lies wholly under the map's floor (its lowest block's base): the sea, the pier's body,
    /// the tower's facade, the jacket. Unity frame (x east, z north), metres; colours sRGB; allocation only while building.
    /// </summary>
    public static class NativeNewStageScenery
    {
        public const double Margin=1;
        /// <summary>Sectors: the near ground and water, the landmark, the far skyline (one draw each).</summary>
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        /// <summary>A builder that refuses a box entering the play space (bounds grown by Margin, above the floor's base).</summary>
        public sealed class Sink
        {
            public readonly NativeSceneryBuilder B=new NativeSceneryBuilder(Sectors);public double MinX,MaxX,MinZ,MaxZ,Under;public string Map;
            public int Boxes;
            public void Box(double x,double y,double z,double sx,double sy,double sz,int hex,double pitch=0,double yaw=0,double roll=0,int glow=0,double level=0,double shade=1,double grime=0,int skip=0)
            {
                if(!(sx>0&&sy>0&&sz>0))return;
                Extent(x,y,z,sx,sy,sz,pitch,yaw,roll,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
                if(Enters(this,x0,x1,y0,y1,z0,z1))throw new InvalidOperationException("New stage scenery "+Map+": a box enters the play space at "+x.ToString("0.#")+", "+y.ToString("0.#")+", "+z.ToString("0.#"));
                B.Box(x,y,z,sx,sy,sz,hex,pitch,yaw,roll,glow,level,shade,grime,skip);Boxes++;
            }
        }
        /// <summary>A box's world extent (centre, full size, Unity Euler degrees) as an axis-aligned box.</summary>
        public static void Extent(double x,double y,double z,double sx,double sy,double sz,double pitch,double yaw,double roll,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)
        {
            double a=pitch*Math.PI/180,b=yaw*Math.PI/180,c=roll*Math.PI/180,ca=Math.Cos(a),sa=Math.Sin(a),cb=Math.Cos(b),sb=Math.Sin(b),cc=Math.Cos(c),sc=Math.Sin(c);
            double m00=cb*cc+sb*sa*sc,m01=-cb*sc+sb*sa*cc,m02=sb*ca,m10=ca*sc,m11=ca*cc,m12=-sa,m20=-sb*cc+cb*sa*sc,m21=sb*sc+cb*sa*cc,m22=cb*ca;
            double hx=sx/2,hy=sy/2,hz=sz/2,ex=Math.Abs(m00)*hx+Math.Abs(m01)*hy+Math.Abs(m02)*hz,ey=Math.Abs(m10)*hx+Math.Abs(m11)*hy+Math.Abs(m12)*hz,ez=Math.Abs(m20)*hx+Math.Abs(m21)*hy+Math.Abs(m22)*hz;
            x0=x-ex;x1=x+ex;y0=y-ey;y1=y+ey;z0=z-ez;z1=z+ez;
        }
        /// <summary>Whether a world box enters the play space: inside the bounds grown by Margin and not wholly under the floor.</summary>
        public static bool Enters(Sink s,double x0,double x1,double y0,double y1,double z0,double z1)=>y1>s.Under+1e-4&&x1>s.MinX+1e-4&&x0<s.MaxX-1e-4&&z1>s.MinZ+1e-4&&z0<s.MaxZ-1e-4;
        /// <summary>The map's play space: its bounds grown by Margin, and the floor's base (its lowest block's).</summary>
        public static void PlaySpace(ContentMap map,out double minX,out double maxX,out double minZ,out double maxZ,out double under)
        {
            var b=map?.bounds;minX=(b?.minX??-40)-Margin;maxX=(b?.maxX??40)+Margin;minZ=(b?.minZ??-32)-Margin;maxZ=(b?.maxZ??32)+Margin;under=0;
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())if(k!=null)under=Math.Min(under,k.pos.y-k.size.y/2);
        }
        private static double H(int a,int b=0,int c=0)=>NativeNewStageLooks.Hash(a,b,c);

        // ---------------- shapes ----------------
        /// <summary>An octagonal prism (two boxes turned 45°), apothem r.</summary>
        public static void Octagon(Sink b,double x,double y0,double y1,double z,double r,int hex,double shade=1,int glow=0,double level=0)
        {b.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r,hex,shade:shade,glow:glow,level:level);b.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r,hex,yaw:45,shade:shade,glow:glow,level:level);}
        /// <summary>A sixteen-sided prism (four boxes turned 22.5° apart), apothem r: round enough for a tower.</summary>
        public static void Round(Sink b,double x,double y0,double y1,double z,double r,int hex,double shade=1)
        {for(int k=0;k<4;k++)b.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r*Math.Tan(Math.PI/16)*2.02,hex,yaw:k*45+22.5,shade:shade);for(int k=0;k<4;k++)b.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r*Math.Tan(Math.PI/16)*2.02,hex,yaw:k*45,shade:shade*.97);}
        /// <summary>A bar of section t × t between two points.</summary>
        public static void Bar(Sink b,double x0,double y0,double z0,double x1,double y1,double z1,double t,int hex,double shade=1,int glow=0,double level=0)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-4)return;
            b.Box((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,t,t,l,hex,Math.Asin(Math.Max(-1,Math.Min(1,-dy/l)))*180/Math.PI,Math.Atan2(dx,dz)*180/Math.PI,shade:shade,glow:glow,level:level);
        }
        /// <summary>A lattice: four legs from half width w0 at y0 to w1 at y1, braced in X every step m.</summary>
        public static void Lattice(Sink b,double x,double z,double y0,double y1,double w0,double w1,double step,double t,int hex)
        {
            double h=y1-y0;for(int k=0;k<4;k++){double sx=(k&1)==0?-1:1,sz=(k&2)==0?-1:1;Bar(b,x+sx*w0,y0,z+sz*w0,x+sx*w1,y1,z+sz*w1,t*1.5,hex);}
            for(double y=y0;y<y1-1e-6;y+=step)
            {
                double ya=Math.Min(y1,y+step),a=w0+(w1-w0)*(y-y0)/h,c=w0+(w1-w0)*(ya-y0)/h;
                for(int f=0;f<4;f++){double s=f<2?-1:1;bool alongX=(f&1)==0;
                    if(alongX){Bar(b,x-a,y,z+s*a,x+c,ya,z+s*c,t,hex);Bar(b,x-c,ya,z+s*c,x+c,ya,z+s*c,t,hex);}else{Bar(b,x+s*a,y,z-a,x+s*c,ya,z+c,t,hex);Bar(b,x+s*c,ya,z-c,x+s*c,ya,z+c,t,hex);}}
            }
        }
        /// <summary>
        /// A faceted sea over the rectangle (x0..x1, z0..z1) at `level`, out from the stage's centre: tiles `near` m wide within 70 m,
        /// three times that to 260 m and nine times beyond, each turned a few degrees by its hash (`chop` the largest pitch or roll)
        /// and shaded between `deep` and `crest` (a swell's band where `swell` m long rollers run), on a flat floor under them so no
        /// gap shows. Tiles under `dry` (the stage's own footprint, x0, x1, z0, z1) are left out. Top faces only.
        /// </summary>
        public static void Sea(Sink b,int seed,double level,double x0,double x1,double z0,double z1,double near,double chop,int deep,int crest,double swell,double[] dry=null)
        {
            b.Box((x0+x1)/2,level-.32,(z0+z1)/2,x1-x0,.2,z1-z0,Mix(deep,0x000000,.35),skip:1|2|8|16|32);
            foreach(var (tile,from,to) in new[]{(near,0.0,70.0),(near*3,70.0,260.0),(near*9,260.0,1e9)})
            {
                double ox=Math.Floor(x0/tile)*tile,oz=Math.Floor(z0/tile)*tile;
                for(double x=ox;x<x1;x+=tile)for(double z=oz;z<z1;z+=tile)
                {
                    double cx=x+tile/2,cz=z+tile/2,r=Math.Max(Math.Abs(cx),Math.Abs(cz));if(r<from||r>=to)continue;
                    if(dry!=null&&cx>dry[0]&&cx<dry[1]&&cz>dry[2]&&cz<dry[3])continue;
                    int i=(int)Math.Floor(cx/tile*7.31),j=(int)Math.Floor(cz/tile*5.17);double h1=H(seed,i,j),h2=H(seed+3,j,i),h3=H(seed+5,i+j,i-j);
                    double band=swell>0?.5+.5*Math.Sin((cx*.8+cz*.6)/swell*2*Math.PI+h1*.8):0,far=Math.Min(1,r/600);double k=Math.Max(0,Math.Min(1,.25*h3+.55*band*band*(1-far*.6)));
                    double p=(h1-.5)*2*chop*(1-far*.7),ro=(h2-.5)*2*chop*(1-far*.7);
                    b.Box(cx,level-.05,cz,tile*1.06,.1,tile*1.06,Mix(deep,crest,k),p,0,ro,skip:1|2|8|16|32);
                }
            }
        }
        /// <summary>Two sRGB colours mixed (each channel straight).</summary>
        public static int Mix(int a,int b,double t){t=Math.Max(0,Math.Min(1,t));int C(int s)=>(int)Math.Round(((a>>s)&255)+(((b>>s)&255)-((a>>s)&255))*t);return C(16)<<16|C(8)<<8|C(0);}
        /// <summary>A block of a building from y0 to y1 with window rows every `floor` m (lit ones glow `lit` at `level` by the hash).</summary>
        public static void Building(Sink b,int seed,double x,double z,double w,double d,double y0,double y1,int wall,int glass,int lit,double litShare,double level,double floor=3.4,double yaw=0)
        {
            b.Box(x,(y0+y1)/2,z,w,y1-y0,d,wall,yaw:yaw,shade:.9+.12*H(seed,1),grime:.1);
            if(floor<=0)return;int rows=(int)((y1-y0-1.2)/floor);if(rows<=0)return;double t=yaw*Math.PI/180,c=Math.Cos(t),s=Math.Sin(t);
            for(int r=0;r<rows;r++)
            {
                double y=y0+1.4+r*floor+floor*.35;bool on=H(seed,r,7)<litShare;int g=on?lit:0;double lv=on?level*(.6+.6*H(seed,r,9)):0;
                // a band of glass on each of the four faces, a hair proud of the wall
                b.Box(x+s*(d/2+.03),y,z+c*(d/2+.03),w*.86,floor*.42,.06,glass,yaw:yaw,glow:g,level:lv);b.Box(x-s*(d/2+.03),y,z-c*(d/2+.03),w*.86,floor*.42,.06,glass,yaw:yaw,glow:g,level:lv*(H(seed,r,11)<.5?1:0));
                bool side=H(seed,r,13)<litShare;b.Box(x+c*(w/2+.03),y,z-s*(w/2+.03),.06,floor*.42,d*.86,glass,yaw:yaw,glow:side?lit:0,level:side?lv:0);b.Box(x-c*(w/2+.03),y,z+s*(w/2+.03),.06,floor*.42,d*.86,glass,yaw:yaw,glow:0,level:0);
            }
        }

        /// <summary>The map's backdrop sectors (null for a map that is not one of the five). `yards`: the kit bank's yards draw for
        /// this map (PackDressing.json; Runtime NativePackStage), so the box-built pieces they replace stand down (Pier 9's
        /// ship-to-shore cranes and container stacks beyond the fences); without the bank the stage stands on its own.</summary>
        public static NativeSceneryMesh[] Build(ContentMap map,bool yards=false)
        {
            int stage=NativeNewStageLooks.Index(map?.id);if(stage<0)return null;
            var s=new Sink{Map=map.id};PlaySpace(map,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);s.B.Anime=true;
            switch(stage){case 0:Breakwater(s);break;case 1:Harbor(s,yards);break;case 2:Skyframe(s);break;case 3:Rig(s);break;default:Polar(s);break;}
            var o=new NativeSceneryMesh[Sectors];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);return o;
        }

        // ---------------- Breakwater: the pier head at blue hour ----------------
        public const int Stone=0x6A6E70,StoneDark=0x4A4E52,StoneWet=0x3E4448,Tetra=0x8A8880,SeaDeep=0x16385A,SeaCrest=0x5A8AAE,Lamp=0xFFC27A,Lime=0xE8E2D2,Band=0xB8302A,Copper=0x4F8A72;
        private static void Breakwater(Sink s)
        {
            var b=s.B;b.Current=Near;double sea=NativeNewStageLooks.SeaLevel(0);
            Sea(s,11,sea,-900,900,-900,900,3,7,SeaDeep,SeaCrest,7,new[]{-21.0,21.0,-14.0,14.0});
            // The pier head's body under its deck, the shoulder outside the parapet, wet at the waterline.
            s.Box(0,(sea-2.6-.5)/2,0,42.4,-.5-(sea-2.6),28.4,StoneDark);
            foreach(int side in new[]{-1,1})
            {
                s.Box(0,-.65,side*15.3,46.4,.7,2.2,Stone,grime:.2);s.Box(0,sea-.3,side*15.6,46.6,1.2,2.6,StoneWet);
                s.Box(side*22.3,-.65,0,2.2,.7,32.8,Stone,grime:.2);s.Box(side*22.6,sea-.3,0,2.6,1.2,33.2,StoneWet);
            }
            // Tetrapods heaped against the north, south and east faces (legs as four bars from a hub), below the shoulder.
            int n=0;
            for(double u=-24;u<=26;u+=2.4)foreach(int side in new[]{-1,1})for(int row=0;row<2;row++)
            {
                double x=u+(H(21,n,1)-.5)*1.2,z=side*(17.4+row*2.4+H(21,n,2)*.8),ty=sea-.6+row*-.5+H(21,n,3)*.4;Tetrapod(s,x,ty,z,1.0+H(21,n,4)*.3,n++);
            }
            for(double v=-15;v<=15;v+=2.4)for(int row=0;row<2;row++){double x=24.6+row*2.4+H(22,n,2)*.8,z=v+(H(22,n,1)-.5)*1.2;if(Math.Abs(z)<5.5)continue;Tetrapod(s,x,sea-.6-row*.5+H(22,n,3)*.4,z,1.0+H(22,n,4)*.3,n++);}
            // The causeway west to the shore: its deck, parapets both sides, lamps every 15 m.
            s.Box(-86,-.25,0,130,.5,14,Stone,grime:.15);s.Box(-86,(sea-2-.5)/2,0,130,-.5-(sea-2),15,StoneDark);
            foreach(int side in new[]{-1,1})s.Box(-86,.6,side*6.4,130,1.2,1.2,Lime,shade:.85,grime:.25);
            for(double x=-30;x>=-150;x-=15)foreach(int side in new[]{-1,1}){double z=side*6.9;s.Box(x,2.6,z,.16,4.0,.16,0x2A2C30);s.Box(x,4.55,z-side*.45,.5,.18,.9,0x2A2C30);s.Box(x,4.42,z-side*.45,.34,.08,.6,Lamp,glow:Lamp,level:2.2);}
            // The parapet's lamp posts, outside the wall on the shoulder, their heads leaning in over the coping.
            for(int k=0;k<6;k++){double x=-15+k*6;foreach(int side in new[]{-1,1}){double z=side*15.0;s.Box(x,1.7,z,.18,4.6,.18,0x2A2C30);s.Box(x,3.95,z-side*.36,.18,.12,.74,0x2A2C30);s.Box(x,3.82,z-side*.62,.42,.16,.42,0x2A2C30);s.Box(x,3.72,z-side*.62,.3,.05,.3,Lamp,glow:Lamp,level:2.4);}}
            foreach(double z in new[]{-11.0,-5.5,5.5,11.0}){double x=22.1;const int side=1;s.Box(x,1.7,z,.18,4.6,.18,0x2A2C30);s.Box(x-side*.36,3.95,z,.74,.12,.18,0x2A2C30);s.Box(x-side*.62,3.82,z,.42,.16,.42,0x2A2C30);s.Box(x-side*.62,3.72,z,.3,.05,.3,Lamp,glow:Lamp,level:2.4);}
            // Bollards and mooring rings on the shoulder, iron ladders down the faces.
            for(double x=-18;x<=18;x+=9)foreach(int side in new[]{-1,1}){s.Box(x,-.05,side*15.6,.5,.5,.5,0x24262A);s.Box(x,.22,side*15.6,.62,.08,.62,0x24262A);}
            foreach(double x in new[]{-12.0,12.0})foreach(int side in new[]{-1,1})for(double ly=sea+.2;ly<-.4;ly+=.35)s.Box(x,ly,side*16.45,.5,.05,.08,0x2A2C30);
            // The tug alongside the south face and the channel buoys.
            Tug(s,5,-21.5,sea);
            Buoy(s,-60,38,sea,0xB8302A);Buoy(s,-60,-40,sea,0x2F8A4A);Buoy(s,70,46,sea,0xB8302A);Buoy(s,80,-52,sea,0x2F8A4A);
            // The lighthouse on its plinth past the east parapet, joined to the pier by a stone neck.
            b.Current=Landmark;double lx=NativeNewStageLooks.LighthouseX,lz=NativeNewStageLooks.LighthouseZ;
            s.Box(25.8,-.25,0,6.6,.5,7.0,Stone,grime:.15);s.Box(25.8,(sea-2-.5)/2,0,6.6,-.5-(sea-2),7.4,StoneDark);
            foreach(int side in new[]{-1,1})s.Box(25.8,.55,side*3.9,6.6,1.1,.6,Lime,shade:.85,grime:.2);
            Octagon(s,lx,sea-2.4,.2,lz,6.2,StoneDark);Octagon(s,lx,-.05,.25,lz,6.0,Stone);
            for(int k=0;k<16;k++){double a=k*Math.PI/8;s.Box(lx+Math.Sin(a)*5.8,.8,lz+Math.Cos(a)*5.8,.1,1.1,.1,0x2A2C30);}
            for(int k=0;k<8;k++){double a=k*Math.PI/4+Math.PI/8,w=2*5.8*Math.Tan(Math.PI/8);s.Box(lx+Math.Sin(a)*5.8*Math.Cos(Math.PI/8),1.32,lz+Math.Cos(a)*5.8*Math.Cos(Math.PI/8),w,.06,.06,0x2A2C30,yaw:a*180/Math.PI);}
            // The keeper's store at the tower's foot.
            s.Box(lx-3.2,1.4,lz-3.4,3.6,2.8,2.6,Lime,grime:.2);s.Box(lx-3.2,2.95,lz-3.4,3.9,.3,2.9,0x3A3E44);s.Box(lx-3.2,1.6,lz-4.72,.9,.7,.06,0x2A3440,glow:Lamp,level:.9);
            // The tower: tapering, three red bands on lime-white, small lit windows, the gallery, the lantern, the copper dome.
            double y=.25;for(int c=0;c<16;c++){double y1=y+.925,r=2.45-c*.04-.02;int band=c/2;Round(s,lx,y,y1,lz,r,band==1||band==4||band==7?Band:Lime);y=y1;}
            foreach(double wy in new[]{4.2,8.0,11.6})s.Box(lx-2.1+.02,wy,lz,.08,.7,.45,0x2A3440,glow:Lamp,level:.7);
            double g=y;Octagon(s,lx,g,g+.3,lz,2.95,0x2E3236);for(int k=0;k<16;k++){double a=k*Math.PI/8;s.Box(lx+Math.Sin(a)*2.85,g+.8,lz+Math.Cos(a)*2.85,.06,1.0,.06,0x2E3236);}
            Octagon(s,lx,g+1.24,g+1.32,lz,2.85,0x2E3236);
            Octagon(s,lx,g+.3,g+.85,lz,1.7,0x2E3236);Octagon(s,lx,g+.85,g+3.55,lz,1.55,0x1E2C3C,1,0xFFE8B0,.06);
            for(int k=0;k<8;k++){double a=k*Math.PI/4+Math.PI/8;s.Box(lx+Math.Sin(a)*1.6,g+2.2,lz+Math.Cos(a)*1.6,.08,2.7,.08,0x2E3236);}
            Octagon(s,lx,g+3.55,g+3.8,lz,1.75,Copper);Octagon(s,lx,g+3.8,g+4.3,lz,1.3,Copper);Octagon(s,lx,g+4.3,g+4.65,lz,.7,Copper);s.Box(lx,g+5.1,lz,.12,1.0,.12,0x2E3236);
            // The town across the harbour in the west, its cranes, the hills behind, the far shore north.
            b.Current=Far;
            for(int i=0;i<46;i++)
            {
                double x=-170-H(31,i)*150,z=-150+i*6.6+(H(31,i,2)-.5)*3,w=6+H(31,i,3)*12,d=8+H(31,i,4)*10,h=6+H(31,i,5)*14;if(H(31,i,6)<.12)h+=12;
                Building(s,300+i,x,z,w,d,-1.0,h,Mix(0x3A4252,0x5A4E48,H(31,i,7)),0x22283A,0xFFC27A,.55,1.1,3.2);
                if(H(31,i,8)<.3)s.Box(x,h+1.0,z,w*.9,2.0,d*.3,0x30343C,roll:0,pitch:0);
            }
            Bar(s,-205,0,-30,-205,34,-30,1.2,0x3A3E46);s.Box(-205,36,-30,.5,4,.5,0x3A3E46);s.Box(-205,38.4,-30,.4,.4,.4,0xFF3020,glow:0xFF3020,level:2.4);
            foreach(double cz in new[]{70.0,104.0})StsCrane(s,-196,cz,90,0x8A9096,0x5A6066,true);
            for(int i=0;i<9;i++){double z=-260+i*70;s.Box(-420-H(33,i)*60,10+H(33,i,1)*16,z,160,30+H(33,i,1)*30,90,0x1E2A36,yaw:20+H(33,i,2)*30);}
            for(int i=0;i<12;i++){double x=-300+i*60;s.Box(x,6+H(34,i)*8,320+H(34,i,1)*40,120,16+H(34,i,1)*16,60,0x1A2430,yaw:H(34,i,2)*30);if(H(34,i,3)<.6)s.Box(x+10,2.5,290,1.2,1.2,1.2,0xFFC27A,glow:0xFFC27A,level:1.6);}
        }
        private static void Tetrapod(Sink s,double x,double y,double z,double size,int n)
        {
            double yaw=H(41,n)*360,pitch=(H(41,n,1)-.5)*50,t=.62*size,l=1.25*size;
            // four legs to the corners of a tetrahedron, turned by the hash
            double[][] dirs={new[]{0.0,1,0},new[]{.943,-.333,0},new[]{-.471,-.333,.816},new[]{-.471,-.333,-.816}};double a=yaw*Math.PI/180,p=pitch*Math.PI/180;
            foreach(var d in dirs)
            {
                double dx=d[0],dy=d[1]*Math.Cos(p)-d[2]*Math.Sin(p),dz=d[1]*Math.Sin(p)+d[2]*Math.Cos(p);double rx=dx*Math.Cos(a)+dz*Math.Sin(a),rz=-dx*Math.Sin(a)+dz*Math.Cos(a);
                Bar(s,x,y,z,x+rx*l,y+dy*l,z+rz*l,t,Tetra,.9+.15*H(41,n,2));
            }
        }
        private static void Tug(Sink s,double x,double z,double sea)
        {
            int hull=0x2A2E34,red=0x8A2A24,white=0xD8D6CE;
            s.Box(x,sea-.4,z,14,2.2,5.2,hull);s.Box(x,sea+.9,z,13.4,.5,5.0,red);s.Box(x+7.6,sea+.5,z,2.2,1.6,3.4,hull,yaw:0);s.Box(x+8.6,sea+.6,z,1.2,1.2,2.0,hull);
            for(double u=-6;u<=6;u+=1.5)s.Box(x+u,sea+.55,z+2.7,.7,.7,.4,0x1A1A1C);
            s.Box(x-1,sea+2.3,z,6,2.2,3.6,white,grime:.1);s.Box(x+.4,sea+4.2,z,3,1.6,3.0,white);s.Box(x+1.95,sea+4.3,z,.06,.8,2.6,0x2A3440,glow:Lamp,level:.9);
            s.Box(x-3.6,sea+3.4,z,1.0,2.6,1.2,0x2A2C30);s.Box(x-3.6,sea+4.85,z,1.1,.3,1.3,red);
            s.Box(x+.4,sea+6.4,z,.14,3.0,.14,0x2A2C30);s.Box(x+.4,sea+8.0,z,.24,.24,.24,0xFFF4D8,glow:0xFFF4D8,level:2.2);s.Box(x+.4,sea+5.2,z+1.6,.2,.2,.2,0x30FF60,glow:0x30FF60,level:2);s.Box(x+.4,sea+5.2,z-1.6,.2,.2,.2,0xFF3020,glow:0xFF3020,level:2);
        }
        private static void Buoy(Sink s,double x,double z,double sea,int hex)
        {s.Box(x,sea+.2,z,1.6,.8,1.6,hex);s.Box(x,sea+.2,z,1.6,.8,1.6,hex,yaw:45);s.Box(x,sea+1.6,z,.5,2.2,.5,hex);s.Box(x,sea+2.9,z,.36,.36,.36,0xFFF0C8,glow:hex==0xB8302A?0xFF4030:0x40FF70,level:2.4);}
        /// <summary>A ship-to-shore crane: legs 18 m apart along x, the portal at 16 m, the boom out to +w (yaw) over the water, the
        /// machinery house on the back reach, aviation lights.</summary>
        public static void StsCrane(Sink s,double x,double z,double yaw,int paint,int dark,bool lights)
        {
            double t=yaw*Math.PI/180,c=Math.Cos(t),sn=Math.Sin(t);void P(double u,double w,out double px,out double pz){px=x+u*c+w*sn;pz=z-u*sn+w*c;}
            foreach(double u in new[]{-9.0,9.0})foreach(double w in new[]{-8.0,8.0}){P(u,w,out double px,out double pz);s.Box(px,8.5,pz,1.4,17,1.4,paint);}
            foreach(double u in new[]{-9.0,9.0}){P(u,0,out double px,out double pz);s.Box(px,16.8,pz,1.6,1.8,18,paint,yaw:yaw);s.Box(px,1.2,pz,1.8,2.4,18,dark,yaw:yaw);}
            foreach(double w in new[]{-8.0,8.0}){P(0,w,out double px,out double pz);s.Box(px,16.8,pz,19.6,1.8,1.6,paint,yaw:yaw);}
            {P(0,6,out double px,out double pz);s.Box(px,33,pz,4,2.2,62,paint,yaw:yaw);}
            {P(0,-6,out double px,out double pz);s.Box(px,35.4,pz,8,4.2,12,dark,yaw:yaw);}
            foreach(double u in new[]{-2.0,2.0}){P(u,-2,out double ax,out double az);P(u,-8,out double bx,out double bz);Bar(s,ax,17.6,az,ax,44,az,1.0,paint);Bar(s,bx,17.6,bz,ax,44,az,.8,paint);P(u,30,out double tx,out double tz);Bar(s,ax,44,az,tx,34,tz,.25,dark);}
            if(!lights)return;{P(0,-2,out double px,out double pz);s.Box(px,44.6,pz,.5,.5,.5,0xFF3020,glow:0xFF3020,level:2.6);P(0,36,out px,out pz);s.Box(px,34.4,pz,.4,.4,.4,0xFF3020,glow:0xFF3020,level:2.4);}
        }

        // ---------------- Pier 9: the night container terminal ----------------
        public const int Asphalt=0x34373C,Quay=0x56524C,Basin=0x0E1C26,BasinCrest=0x2A4458,Sodium=0xFFA64A,HullBlack=0x2A3036,HullRed=0x5A2420;
        public static readonly int[] Livery={0xB8402E,0x2F5F9E,0xD8A030,0x3F7F5A,0xC8642A,0x6A2F7A,0x2A7F86,0xD8D8D0,0x8A2A24,0x4A5A6A};
        private static void Harbor(Sink s,bool yards)
        {
            var b=s.B;b.Current=Near;double water=NativeNewStageLooks.SeaLevel(1);
            Sea(s,12,water,-700,700,30.4,800,4,1.6,Basin,BasinCrest,0,null);
            // The quay face under the slab's edge: concrete, black fenders, ladders, a kerb light row.
            s.Box(0,(water-.6-.5)/2,30.15,84,-.5-(water-.6),.3,0x4A4640);
            for(double x=-38;x<=38;x+=8)s.Box(x,-1.35,30.5,1.6,1.5,.4,0x141416);
            foreach(double x in new[]{-22.0,10.0,30.0})for(double y=water+.2;y<-.6;y+=.3)s.Box(x,y,30.35,.45,.04,.06,0x2A2C30);
            // The terminal on beyond the fences, east and west: aprons, stacks, the quay, two more ship-to-shore cranes, light masts.
            foreach(int side in new[]{-1,1})
            {
                double cx=side*96;s.Box(cx,-.25,-6,108,.5,76,Asphalt);s.Box(cx,(water-.6-.5)/2,30.15,108,-.5-(water-.6),.3,0x4A4640);
                for(double x=44;x<=150;x+=8)s.Box(side*x,-1.35,30.5,1.6,1.5,.4,0x141416);
                if(!yards)for(int r=0;r<5;r++)for(int bay=0;bay<6;bay++)
                {
                    double x=side*(52+bay*13.6),z=-24+r*8.2;int tiers=1+(int)(H(51+side,r,bay)*4);if(H(52+side,r,bay)<.1)continue;
                    for(int k=0;k<tiers;k++)foreach(int lane in new[]{-1,1})Container(s,x,k*2.6,z+lane*1.3,12.2,Livery[(int)(H(53+side,r*7+bay,k*3+lane+5)*Livery.Length)%Livery.Length],r*31+bay*7+k);
                }
                for(int k=0;k<3;k++){double x=side*(56+k*30),z=16;LightMast(s,x,z,30);}
                for(int k=0;k<3;k++){double x=side*(56+k*30);LightMast(s,x,-38,26);}
                for(double x=44;x<=150;x+=12){s.Box(side*x,.35,29.2,.5,.7,.5,0x24262A);}
            }
            if(!yards){StsCrane(s,-70,22,0,0xD6D4CC,0x4A5058,true);StsCrane(s,72,22,0,0xD6D4CC,0x4A5058,true);}
            // South: the road and the warehouses past the shed's wall, their bay lights.
            s.Box(0,-.25,-45,300,.5,24,Asphalt);
            for(int i=0;i<7;i++){double x=-120+i*40;s.Box(x,6,-70,34,12,24,Mix(0x4A5058,0x5A4E44,H(55,i)),grime:.2);s.Box(x,12.4,-70,35,.8,25,0x2E3236);for(int d=0;d<3;d++){double dx=x-10+d*10;s.Box(dx,2.6,-57.9,4,5,.1,0x2A2C30);s.Box(dx,5.6,-57.6,1.2,.3,.6,Sodium,glow:Sodium,level:2.2);}}
            for(int i=0;i<6;i++)LightMast(s,-100+i*40,-46,18);
            // The freighter alongside past the bounds: hull, boot line, deck, coamings, container bays, the house aft, the funnel.
            b.Current=Landmark;double hz0=35.6,beam=24,deck=7.6,len=170,sx=-8;
            s.Box(sx,(water-6+deck)/2,hz0+beam/2,len,deck-(water-6),beam,HullBlack);s.Box(sx,water+.5,hz0-.04,len,1.4,.1,HullRed);s.Box(sx,water+1.25,hz0-.05,len,.18,.1,0xD8D6CE);
            s.Box(sx+len/2+5,(water-4+deck+2)/2,hz0+beam/2,12,deck+2-(water-4),beam*.7,HullBlack);s.Box(sx+len/2+10,deck+1.2,hz0+beam/2,4,2.4,beam*.4,HullBlack);
            s.Box(sx+12,deck-1.1,hz0+.06,90,.9,.1,0xD8D6CE,glow:0,level:0);
            for(double x=-78;x<=50;x+=13.5)
            {
                s.Box(x,deck+.6,hz0+beam/2,12.6,1.2,beam-2,0x3A3E44);int tiers=3+(int)(H(61,(int)x)*4);
                for(int row=0;row<8;row++)for(int k=0;k<tiers;k++){if(H(62,(int)x*13+row,k)<.06)continue;Container(s,x,deck+1.2+k*2.6,hz0+2.5+row*2.5,12.2,Livery[(int)(H(63,(int)x+row*17,k)*Livery.Length)%Livery.Length],(int)x*7+row*3+k,true);}
            }
            double hx=62;s.Box(hx,deck+8,hz0+beam/2,14,16,beam-2,0xE0DED6,grime:.1);
            for(int f=0;f<5;f++){double y=deck+2+f*3;s.Box(hx,y,hz0+.94,12,1.0,.06,0x22283A,glow:H(64,f)<.7?0xFFE0A8:0,level:1.2);s.Box(hx-7.04,y,hz0+beam/2,.06,1.0,beam-4,0x22283A,glow:H(65,f)<.5?0xFFE0A8:0,level:1.0);}
            s.Box(hx,deck+16.6,hz0+beam/2,15,1.2,beam,0xE0DED6);s.Box(hx,deck+17.4,hz0+beam/2,3,.6,beam+4,0xE0DED6);s.Box(hx+4,deck+20,hz0+beam/2,5,7,6,0xB8302A);s.Box(hx+4,deck+23.6,hz0+beam/2,5.2,.6,6.2,0x1A1A1C);
            s.Box(hx-2,deck+21,hz0+beam/2,.3,6,.3,0x2A2C30);s.Box(hx-2,deck+24.2,hz0+beam/2,.4,.4,.4,0xFFF4D8,glow:0xFFF4D8,level:2.4);s.Box(sx-len/2+6,deck+8,hz0+beam/2,.3,10,.3,0x2A2C30);s.Box(sx-len/2+6,deck+13.2,hz0+beam/2,.4,.4,.4,0xFFF4D8,glow:0xFFF4D8,level:2.4);
            for(double x=-80;x<=70;x+=12){s.Box(x,deck+.2,hz0+.3,.1,.9,.1,0x2A2C30);s.Box(x+6,deck-2.6,hz0-.03,.5,.5,.06,0x22283A,glow:0xFFE0A8,level:1.2);}
            for(double x=-70;x<=50;x+=30){s.Box(x,deck+.5,hz0+1.2,.6,1.0,.6,0x2A2C30);s.Box(x,deck+1.1,hz0+1.2,.5,.2,.5,0xFFE8C0,glow:0xFFE8C0,level:2.2);}s.Box(sx,deck+.7,hz0+.3,len-6,.06,.06,0x2A2C30);
            // The city south and east, the far shore north and the lit bridge across the mouth.
            b.Current=Far;
            for(int i=0;i<60;i++)
            {
                double a=-.2-H(71,i)*2.7,r=180+H(71,i,1)*260,x=Math.Cos(a)*r,z=Math.Sin(a)*r-40,w=14+H(71,i,2)*24,d=14+H(71,i,3)*24,h=12+H(71,i,4)*40;if(H(71,i,5)<.1)h+=50;
                Building(s,700+i,x,z,w,d,-.5,h,Mix(0x2A3040,0x3E3A3C,H(71,i,6)),0x1C2230,0xFFD08A,.45,1.0,3.4,H(71,i,7)*30);
                if(H(71,i,8)<.25)s.Box(x,h+1.2,z,.5,2.4,.5,0x30343C);if(h>50)s.Box(x,h+.4,z,.5,.5,.5,0xFF3020,glow:0xFF3020,level:2.2);
            }
            for(int i=0;i<14;i++){double x=-520+i*80;s.Box(x,8+H(72,i)*10,640+H(72,i,1)*80,140,20+H(72,i,1)*18,70,0x161E28,yaw:H(72,i,2)*24);for(int k=0;k<4;k++)if(H(72,i,k+3)<.6)s.Box(x-40+k*22,1.5,600,1.6,1.6,1.6,0xFFC890,glow:0xFFC890,level:1.6);}
            for(int k=0;k<2;k++){double x=k==0?-120:180;s.Box(x,30,470,6,64,8,0x30343C);s.Box(x,62.6,470,10,1.2,10,0x30343C);s.Box(x,63.6,470,.6,.6,.6,0xFF3020,glow:0xFF3020,level:2.6);}
            s.Box(30,22,470,640,1.6,10,0x2A2E36);for(double x=-280;x<=330;x+=12)s.Box(x,23.2,465,.4,.4,.4,0xFFD08A,glow:0xFFD08A,level:2.0);
            for(int seg=0;seg<24;seg++){double u0=seg/24.0,u1=(seg+1)/24.0;double X(double u)=>-120+300*u;double Y(double u)=>62-36*4*u*(1-u);Bar(s,X(u0),Y(u0),470,X(u1),Y(u1),470,.35,0x6A7078);}
        }
        /// <summary>A shipping container (2.4 wide, 2.6 tall, `length` along x, door end at -x): body, corner posts, the rails.</summary>
        public static void Container(Sink s,double x,double y0,double z,double length,int livery,int seed,bool lean=false)
        {
            double sh=.88+.16*H(81,seed);s.Box(x,y0+1.3,z,length,2.6,2.4,livery,shade:sh,grime:.08);
            if(lean)return;
            foreach(double u in new[]{-1.0,1.0})s.Box(x+u*(length/2-.08),y0+1.3,z,.18,2.62,2.44,Mix(livery,0x101010,.35));
            s.Box(x,y0+2.55,z,length,.12,2.44,Mix(livery,0x101010,.3));
        }
        /// <summary>A floodlight mast: a tapering pole to `height`, a head frame with four sodium lamps facing down and in.</summary>
        public static void LightMast(Sink s,double x,double z,double height)
        {
            s.Box(x,height/2,z,.7,height,.7,0x5A6066);s.Box(x,height/2,z,.7,height,.7,0x5A6066,yaw:45);s.Box(x,.3,z,1.6,.6,1.6,0x4A4E52);
            s.Box(x,height+.3,z,3.4,.4,3.4,0x3A3E44);for(int k=0;k<4;k++){double a=k*Math.PI/2+Math.PI/4;s.Box(x+Math.Sin(a)*1.3,height-.1,z+Math.Cos(a)*1.3,.9,.4,.9,0x2A2C30);s.Box(x+Math.Sin(a)*1.3,height-.32,z+Math.Cos(a)*1.3,.7,.06,.7,Sodium,glow:Sodium,level:2.6);}
        }

        // ---------------- Skyframe: floor 31 at high noon ----------------
        public const int Glass=0x5F7F9C,GlassDeep=0x3E5A78,Spandrel=0xC8C4BC,FrameSteel=0x4A5056,Brick=0x8A4A36,Street=0x3A3C40,Park=0x4A6A3A;
        public const double StreetY=-112;
        private static void Skyframe(Sink s)
        {
            var b=s.B;b.Current=Near;
            // The tower under the slab: floors 30 and 29 an open steel frame with netting (Core NativeNewStageFx), then glass.
            for(int f=1;f<=2;f++)
            {
                double y=-f*3.6+.15;s.Box(0,y-.15,0,56,.3,52,0x9E9B93,skip:4);
                for(double z=-24;z<=24;z+=8)foreach(int side in new[]{-1,1})s.Box(side*27.8,y+1.475,z,.4,2.95,.4,FrameSteel);
                foreach(int side in new[]{-1,1})s.Box(side*27.8,y+2.65,0,.5,.6,52,FrameSteel);
            }
            s.Box(0,(StreetY+(-7.2-.1))/2,0,55.2,-7.2-StreetY,51.2,GlassDeep);
            for(double y=-10.8;y>StreetY+3;y-=3.6)
            {
                foreach(int side in new[]{-1,1}){s.Box(side*27.72,y,0,.12,1.0,51.4,Spandrel);s.Box(0,y,side*25.72,55.4,1.0,.12,Spandrel);}
            }
            for(double z=-24;z<=24;z+=3)foreach(int side in new[]{-1,1})s.Box(side*27.75,(StreetY-7.2)/2,z,.1,-7.2-StreetY,.14,0x2E3238);
            for(double x=-27;x<=27;x+=3)foreach(int side in new[]{-1,1})s.Box(x,(StreetY-7.2)/2,side*25.75,.14,-7.2-StreetY,.1,0x2E3238);
            // The material hoist's mast down the east face, its car parked eight floors down; the west one's at the street.
            foreach(int side in new[]{-1,1})foreach(double hz in new[]{-11.0,11.0})
            {
                double hx=side*29.4;s.Box(hx,(StreetY-1)/2,hz,.8,-1-StreetY,.8,0xD8A028);
                double car=side>0?-30:-90;s.Box(hx,car,hz+(hz>0?-1.2:1.2),1.6,2.6,2.0,0xD8A028);s.Box(hx,car+1.4,hz+(hz>0?-1.2:1.2),1.7,.12,2.1,0x2E3238);
                for(double y=-3;y>StreetY;y-=3.6)s.Box(side*28.6,y,hz,1.6,.12,.3,0x5A6066);
            }
            // The street and plaza, the neighbours behind the party walls: a glass tower north, an older stone block south.
            s.Box(0,StreetY-.25,0,1600,.5,1600,Street,skip:1|2|8|16|32);
            s.Box(0,StreetY+.2,0,70,.4,66,0x8A8680);
            s.Box(0,(StreetY+60)/2,48,74,60-StreetY,44,GlassDeep);for(double y=StreetY+4;y<60;y+=3.6){s.Box(0,y,25.94,74.2,.6,.14,0xA8B8C4);s.Box(-37.04,y,48,.14,.6,44.2,0xA8B8C4);s.Box(37.04,y,48,.14,.6,44.2,0xA8B8C4);}
            s.Box(0,60.6,48,72,1.2,42,0x6A7076);s.Box(18,63,48,8,4,6,0x8A9096);
            s.Box(0,(StreetY+16)/2,-46,64,16-StreetY,40,0xB8A88E,grime:.25);
            for(double y=StreetY+4;y<15;y+=3.4)for(double x=-28;x<=28;x+=4){s.Box(x,y,-25.95,1.6,1.8,.1,0x2E3640);}
            for(double y=StreetY+4;y<15;y+=3.4)foreach(int side in new[]{-1,1})for(double z=-62;z<=-30;z+=4)s.Box(side*32.05,y,z,.1,1.8,1.6,0x2E3640);
            s.Box(0,16.5,-46,65,1,41,0x8A7E6E);s.Box(-16,19,-52,5,4,5,0x6A5A4A);s.Box(-16,21.4,-52,5.6,.8,5.6,0x4A3E34);s.Box(10,17.5,-38,6,1,4,0x8A9096);
            // The city: towers on a street grid to the haze, rooftop plant, two more cranes over building sites.
            b.Current=Far;int n=0;
            for(int gx=-9;gx<=9;gx++)for(int gz=-9;gz<=9;gz++)
            {
                double cx=gx*72,cz=gz*72;if(Math.Abs(gx)<=1&&Math.Abs(gz)<=1)continue;double r=Math.Sqrt(cx*cx+cz*cz);if(r>700)continue;
                for(int k=0;k<2;k++)
                {
                    n++;double w=18+H(91,n)*22,d=18+H(91,n,1)*22,x=cx+(k==0?-14:16)+(H(91,n,2)-.5)*8,z=cz+(H(91,n,3)-.5)*20;
                    double top=StreetY+18+Math.Pow(H(91,n,4),1.8)*110;if(H(91,n,5)<.05)top=StreetY+150+H(91,n,6)*60;
                    int wall=H(91,n,7)<.55?Mix(0x5F7F9C,0x8EAAC0,H(91,n,8)):Mix(0xB8B0A0,0x8A6A5A,H(91,n,8));
                    s.Box(x,(StreetY+top)/2,z,w,top-StreetY,d,wall,shade:.92+.1*H(91,n,9));
                    for(double y=StreetY+6;y<top-2;y+=7.2)s.Box(x,y,z,w+.1,.5,d+.1,Mix(wall,0xE0E0E0,.35));
                    s.Box(x,top+.6,z,w*.5,1.2,d*.4,0x8A9096);if(H(91,n,10)<.3)s.Box(x+w*.2,top+2,z-d*.2,2.4,2.8,2.4,0x6A5A4A);
                }
                if(Math.Abs(gx)>=3||Math.Abs(gz)>=3)if(H(92,gx+20,gz+20)<.06)s.Box(cx,StreetY+.3,cz,50,.6,50,Park);
            }
            Tower(s,150,-110,StreetY+60);Tower(s,-210,160,StreetY+40);
        }
        /// <summary>A tower crane on another site: mast to `top`, jib and counter-jib, counterweights, a red light on the apex.</summary>
        private static void Tower(Sink s,double x,double z,double top)
        {
            Lattice(s,x,z,StreetY,top,1.0,1.0,2.4,.12,0xD8A028);s.Box(x,top+.6,z,3,1.2,3,0x2E3238);
            s.Box(x+14,top+1.6,z,30,1.2,1.2,0xD8A028);s.Box(x-6,top+1.6,z,12,1.2,1.4,0xD8A028);s.Box(x-10,top+.4,z,3,2.2,2.4,0x8C8880);
            Bar(s,x,top+6,z,x+20,top+2.2,z,.12,0x2E3238);Bar(s,x,top+6,z,x-11,top+2.2,z,.12,0x2E3238);s.Box(x,top+3.8,z,.8,4.4,.8,0xD8A028);s.Box(x,top+6.3,z,.4,.4,.4,0xFF3020,glow:0xFF3020,level:1.6);
        }

        // ---------------- Deepwell: the platform in a force nine ----------------
        public const int Storm=0x22404E,StormCrest=0x7E9AAA,Jacket=0xC8A028,JacketDark=0x6A5A30,Orange=0xE8642A;
        private static void Rig(Sink s)
        {
            var b=s.B;b.Current=Near;double sea=NativeNewStageLooks.SeaLevel(3);
            Sea(s,13,sea,-900,900,-900,900,3,9,Storm,StormCrest,11,new[]{-28.0,28.0,-20.0,20.0});
            // The jacket: four legs and the inner pair under the cellar deck, X braces at three levels, the deck's girders; none under a landing.
            foreach(double lx in new[]{-25.5,-8.0,8.0,25.5})foreach(double lz in new[]{-16.0,16.0}){Octagon(s,lx,sea-28,-.35,lz,1.3,Jacket);Octagon(s,lx,sea-.6,sea+.8,lz,1.45,0x3A4048);}
            foreach(double y in new[]{-2.6,sea-6,sea-14})foreach(double lz in new[]{-16.0,16.0})for(int k=0;k<3;k++)
            {double a=new[]{-25.5,-8.0,8.0}[k],c=new[]{-8.0,8.0,25.5}[k];Bar(s,a,y,lz,c,y+(y>-3?-2.2:6),lz,.55,JacketDark);Bar(s,a,y+(y>-3?-2.2:6),lz,c,y,lz,.55,JacketDark);}
            foreach(double y in new[]{-2.6,sea-6})foreach(double lx in new[]{-25.5,25.5}){Bar(s,lx,y,-16,lx,y+(y>-3?-2.2:6),16,.55,JacketDark);Bar(s,lx,y+(y>-3?-2.2:6),-16,lx,y,16,.55,JacketDark);}
            for(double x=-27;x<=27;x+=3)s.Box(x,-.85,0,.3,1.0,39.6,0x4A4E52);for(double z=-19;z<=19;z+=6)s.Box(0,-1.15,z,55.6,.4,.3,0x4A4E52);
            // Lifeboats in their davits past the two lifeboat stations, the boat landings' fenders.
            foreach(int side in new[]{-1,1})
            {
                double x=side*-18.5,z=side*24.9;s.Box(x,1.2,z,7.2,2.2,2.8,Orange);s.Box(x,2.5,z,6.4,.6,2.4,Orange,shade:1.08);s.Box(x,1.0,z+side*1.42,6.8,.2,.06,0xE8E8E0);
                foreach(double u in new[]{-2.8,2.8}){Bar(s,x+u,-.2,side*23.5,x+u,4.6,side*24.9,.24,0x8A9096);s.Box(x+u,4.6,z,.24,.24,1.4,0x8A9096);s.Box(x+u,3.2,z,.05,2.6,.05,0x1A1A1C);}
            }
            foreach(int side in new[]{-1,1})s.Box(side*12.5,sea+1.2,side*23.6,3.4,2.4,1.0,0x141416);
            // The flare boom leaning north from the platform's edge, its tip and wind shield, the riser.
            b.Current=Landmark;double fx=NativeNewStageLooks.FlareX,fy=NativeNewStageLooks.FlareY,fz=NativeNewStageLooks.FlareZ;double ax=fx,ay=2.6,az=23.4;
            double[] cu={-1.3,1.3,0},cv={0,0,1.9};
            for(int k=0;k<3;k++)Bar(s,ax+cu[k],ay+cv[k],az,fx+cu[k]*.5,fy-1.6+cv[k]*.5,fz,.3,0xD8D6CE);
            for(int i=0;i<14;i++)
            {
                double t0=i/14.0,t1=(i+1)/14.0;double Px(double t,int k)=>ax+(fx-ax)*t+cu[k]*(1-.5*t);double Py(double t,int k)=>ay+(fy-1.6-ay)*t+cv[k]*(1-.5*t);double Pz(double t)=>az+(fz-az)*t;
                for(int k=0;k<3;k++){int m=(k+1)%3;Bar(s,Px(t0,k),Py(t0,k),Pz(t0),Px(t1,m),Py(t1,m),Pz(t1),.14,i%2==0?0xB8302A:0xD8D6CE);Bar(s,Px(t1,k),Py(t1,k),Pz(t1),Px(t1,m),Py(t1,m),Pz(t1),.12,0xD8D6CE);}
            }
            Bar(s,fx,fy-2,fz,fx,fy,fz,.9,0x3A3430);s.Box(fx,fy+.15,fz,1.6,.3,1.6,0x2A2420);s.Box(fx,fy+.4,fz,1.2,.2,1.2,0xFF8A30,glow:0xFF8A30,level:2.2);
            s.Box(fx-1.2,fy-1.4,fz,.1,2.6,2.0,0x3A3430);s.Box(fx+1.2,fy-1.4,fz,.1,2.6,2.0,0x3A3430);
            // The standby vessel south-east, pitching in its own lights; two distant platforms with their flares on the horizon.
            b.Current=Far;double vx=70,vz=-90;s.Box(vx,sea+.6,vz,40,4,11,0x1E2A34,pitch:2,roll:-3);s.Box(vx+12,sea+5,vz,12,5,9,0xE8E6DE,pitch:2,roll:-3);s.Box(vx-8,sea+3,vz,22,.6,10,0xE8642A,pitch:2,roll:-3);
            s.Box(vx+16,sea+11,vz,.3,6,.3,0x2A2C30);s.Box(vx+16,sea+14.2,vz,.5,.5,.5,0xFFF4D8,glow:0xFFF4D8,level:2.4);s.Box(vx+12,sea+7,vz-4.56,10,1.2,.1,0x22283A,glow:0xFFE0A8,level:1.2);
            foreach(var (px,pz) in new[]{(-420.0,520.0),(560.0,380.0)})
            {
                Octagon(s,px,sea-2,sea+22,pz,3,0x3A3E44);s.Box(px,sea+26,pz,46,8,38,0x2E3236);s.Box(px+8,sea+42,pz,4,24,4,0x3A3E44);Bar(s,px-10,sea+30,pz+18,px-26,sea+58,pz+40,1.4,0x3A3E44);
                for(int k=0;k<6;k++)s.Box(px-20+k*8,sea+31,pz-19.1,2,1,.1,0x22283A,glow:0xFFD08A,level:1.8);s.Box(px+8,sea+54.4,pz,.8,.8,.8,0xFF3020,glow:0xFF3020,level:2.6);
            }
        }

        // ---------------- Polaris Station: the ice shelf under the aurora ----------------
        public const int SnowLit=0xE6EEF4,SnowShade=0xC8D6E2,Ice=0x9AB4C8,Rock=0x22283A,StationRed=0xC84A2A,StationYellow=0xD8A028,Warm=0xFFC88A;
        private static void Polar(Sink s)
        {
            var b=s.B;b.Current=Near;
            // The ice shelf out to the horizon (beyond the floor's edge), drifted: sastrugi ridges with the wind, drift banks.
            foreach(var (x,z,w,d) in new[]{(0.0,-464.0,1868.0,872.0),(0.0,464.0,1868.0,872.0),(-484.0,0.0,900.0,56.0),(484.0,0.0,900.0,56.0)})s.Box(x,-.25,z,w,.5,d,SnowLit,skip:1|2|8|16|32);
            for(int i=0;i<520;i++)
            {
                double a=H(101,i)*2*Math.PI,r=44+Math.Pow(H(101,i,1),.8)*420,x=Math.Cos(a)*r,z=Math.Sin(a)*r;if(Math.Abs(x)<46&&Math.Abs(z)<40)continue;
                double l=4+H(101,i,2)*14,h=.2+H(101,i,3)*(r<90?1.4:.8);s.Box(x,h/2-.25,z,l,h,1.2+H(101,i,4)*2.2,H(101,i,5)<.5?SnowLit:SnowShade,pitch:0,yaw:28+(H(101,i,6)-.5)*24,roll:(H(101,i,7)-.5)*8);
            }
            // Drift banks shouldering the station's own banks from outside.
            for(double u=-34;u<=34;u+=5.6)foreach(int side in new[]{-1,1}){double h=3.2+H(102,(int)u,side+3)*2.6;s.Box(u,h/2-.3,side*(30+H(103,(int)u,side+3)*2),7,h,4.5,SnowShade,yaw:(H(104,(int)u,side+3)-.5)*20,roll:side*-14);}
            for(double v=-26;v<=26;v+=5.6)foreach(int side in new[]{-1,1}){double h=3.0+H(105,(int)v,side+3)*2.6;s.Box(side*(36+H(106,(int)v,side+3)*2),h/2-.3,v,4.5,h,7,SnowShade,yaw:(H(107,(int)v,side+3)-.5)*20,pitch:side*14);}
            // Flag lines along the routes out, the skiway's edge lights east, buried drum lines.
            for(int k=0;k<22;k++){double x=40+k*12;foreach(int side in new[]{-1,1}){s.Box(x,1.0,side*14,.06,2.0,.06,0x2A2C30);s.Box(x+.2,1.8,side*14,.4,.28,.03,k%2==0?0xD8302A:0x2A9A4A);}}
            for(int k=0;k<18;k++){double x=60+k*22;foreach(int side in new[]{-1,1}){s.Box(x,.25,side*32,.3,.5,.3,0x1A1A1C);s.Box(x,.56,side*32,.24,.12,.24,0x40C8FF,glow:0x40C8FF,level:2.2);}}
            for(int k=0;k<16;k++){double z=-60+k*8;s.Box(-46,.15,z,1.2,.6,.8,0x6A3A2A);s.Box(-46,.48,z,1.4,.12,1.0,SnowLit);}
            // Outbuildings beyond the banks: a science module on stilts, the generator house, fuel bladders, the met tower.
            b.Current=Landmark;
            Module(s,-58,40,16,0xD8A028,1);Module(s,62,-42,14,0xC84A2A,2);Module(s,-70,-36,10,0x2F5F9E,3);
            s.Box(64,3.2,44,12,6.4,8,0x8A9096,grime:.1);s.Box(64,6.6,44,12.4,.4,8.4,SnowLit);s.Box(58.9,3.6,44,.1,1.4,4,0x22283A,glow:Warm,level:1.4);Octagon(s,68,6.4,12,44,.5,0x3A3E44);
            for(int k=0;k<4;k++){s.Box(-40+k*6,1.0,-48,5,2.0,3.4,0x2A2C30);s.Box(-40+k*6,2.05,-48,5.1,.1,3.5,SnowLit);}
            Lattice(s,48,52,0,24,.6,.4,2.0,.08,0xC8C8C8);s.Box(48,24.4,52,.4,.4,.4,0xFF3020,glow:0xFF3020,level:2.4);
            // The antenna field and the wind turbine.
            for(int k=0;k<5;k++){double x=-120+k*16,z=70+H(108,k)*10;s.Box(x,9,z,.3,18,.3,0xC8C8C8);s.Box(x,18.3,z,.36,.36,.36,0xFF3020,glow:0xFF3020,level:2.2);if(k<4)Bar(s,x,16,z,x+16,16,70+H(108,k+1)*10,.05,0x3A3E44);}
            Octagon(s,110,0,28,-70,1.1,0xE6E6E0);s.Box(110,28.4,-70,2.2,1.6,4.4,0xE6E6E0);
            for(int k=0;k<3;k++){double a=k*2*Math.PI/3+.3;Bar(s,110,28.4,-72.4,110+Math.Sin(a)*13,28.4+Math.Cos(a)*13,-72.6,.6,0xE6E6E0);}
            // The horizon: nunataks (dark, wind-scoured rock with snow on their shoulders) and the shelf's distant ice cliff.
            b.Current=Far;
            for(int i=0;i<34;i++)
            {
                double a=H(111,i)*2*Math.PI,r=650+H(111,i,1)*250,x=Math.Cos(a)*r,z=Math.Sin(a)*r,h=30+H(111,i,2)*90,w=60+H(111,i,3)*120;
                s.Box(x,h/2-6,z,w,h,w*.7,Rock,pitch:(H(111,i,4)-.5)*30,yaw:H(111,i,5)*180,roll:(H(111,i,6)-.5)*30);
                s.Box(x,h*.62-6,z,w*.7,h*.35,w*.5,Mix(Rock,SnowShade,.65),pitch:(H(111,i,4)-.5)*30,yaw:H(111,i,5)*180+10,roll:(H(111,i,6)-.5)*30);
            }
            for(int i=0;i<16;i++){double x=-700+i*90;s.Box(x,4,-560-H(112,i)*30,95,10+H(112,i,1)*6,24,Ice,yaw:(H(112,i,2)-.5)*8);}
        }
        /// <summary>An outbuilding: a module on stilts above the snow, lit windows, a snow cap, its steps.</summary>
        private static void Module(Sink s,double x,double z,double length,int paint,int seed)
        {
            foreach(double u in new[]{-.4,.4})foreach(double w in new[]{-1.0,1.0})s.Box(x+u*length,.9,z+w*1.6,.3,1.8,.3,0x3A3E44);
            s.Box(x,3.3,z,length,3.0,3.6,paint,grime:.08);s.Box(x,4.9,z,length+.2,.2,3.8,SnowLit);
            for(double u=-length/2+1.5;u<length/2-1;u+=2.6)if(H(113,seed,(int)(u*10))<.75)s.Box(x+u,3.5,z-1.83,1.2,.8,.06,0x22283A,glow:Warm,level:1.3);
            for(int k=0;k<6;k++)s.Box(x-length/2-.6-k*.3,1.8-k*.3,z,.6,.12,1.2,0x3A3E44);
        }
    }
}
