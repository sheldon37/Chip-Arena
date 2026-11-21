using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Top Dog Emporium's backdrop (docs/arenas/EMPORIUM.md "Look"): what stands round and over the hall past its 8 m walls, box-built
    /// and deterministic, baked once into ChipArena/SourceLit sector meshes (Runtime/NativeEmporiumPresentation.cs draws them on
    /// <see cref="NativeSceneryBuilder.BackdropLayer"/> without shadows):
    /// <list type="bullet">
    /// <item>Near: the hall's clerestory over the walls (cream piers on the trusses' lines, iron mullions, a pink cornice under the
    /// eaves) and the two end gables under the vault, glazed as sunbursts that glow in the low sun (amber at the west, rose at the
    /// east), with their rose windows.</item>
    /// <item>Landmark: the street round the palace, seen through the clerestory and the glass: the brick and stucco blocks across it
    /// with their lit floors, a rooftop billboard with a giant hot dog on it, water towers, the art-deco tower south with its spire.</item>
    /// <item>Far: the city's skyline to the haze, its windows coming on.</item>
    /// </list>
    /// The keep-out (thrown while building): nothing stands inside the map's bounds unless it lies wholly above
    /// NativeEmporiumLooks.RoofLow (nobody reaches there) or wholly under the floor. Unity frame, metres, sRGB; allocates only while
    /// building.
    /// </summary>
    public static class NativeEmporiumScenery
    {
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public sealed class Sink
        {
            public readonly NativeSceneryBuilder B=new NativeSceneryBuilder(Sectors){Anime=true};public double MinX,MaxX,MinZ,MaxZ;public int Boxes;
            public void Box(double x,double y,double z,double sx,double sy,double sz,int hex,double pitch=0,double yaw=0,double roll=0,int glow=0,double level=0,double shade=1,double grime=0)
            {
                if(!(sx>0&&sy>0&&sz>0))return;
                NativeNewStageScenery.Extent(x,y,z,sx,sy,sz,pitch,yaw,roll,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
                if(Enters(this,x0,x1,y0,y1,z0,z1))throw new InvalidOperationException("Emporium scenery: a box enters the play space at "+x.ToString("0.#")+", "+y.ToString("0.#")+", "+z.ToString("0.#"));
                B.Box(x,y,z,sx,sy,sz,hex,pitch,yaw,roll,glow,level,shade,grime);Boxes++;
            }
        }
        /// <summary>Whether a world box enters the play space: inside the bounds, not wholly over the roof line nor under the floor.</summary>
        public static bool Enters(Sink s,double x0,double x1,double y0,double y1,double z0,double z1)=>y0<NativeEmporiumLooks.RoofLow-1e-4&&y1>-.5+1e-4&&x1>s.MinX+1e-4&&x0<s.MaxX-1e-4&&z1>s.MinZ+1e-4&&z0<s.MaxZ-1e-4;
        private static double H(int a,int b=0,int c=0)=>NativeEmporiumLooks.Hash(a,b,c);

        /// <summary>The sectors' meshes for the map (null for any other map).</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(!NativeEmporiumLooks.Active(map?.id))return null;var b=map.bounds;
            var s=new Sink{MinX=b?.minX??-28,MaxX=b?.maxX??28,MinZ=b?.minZ??-23,MaxZ=b?.maxZ??23};
            s.B.Current=Near;Clerestory(s);Gables(s);
            s.B.Current=Landmark;Street(s);
            s.B.Current=Far;Skyline(s);
            var o=new NativeSceneryMesh[Sectors];for(int i=0;i<Sectors;i++)o[i]=s.B.Bake(i);return o;
        }

        private static void Clerestory(Sink s)
        {
            double hx=NativeEmporiumLooks.RoofHalfX,hz=NativeEmporiumLooks.RoofHalfZ,eav=NativeEmporiumLooks.Eaves;const int cream=0xF3E2C4,pink=0xF27BA8,iron=0x2C4A48;
            // the long sides: piers on the truss lines, mullions between, the cornice; the outer skin's sill at 8 m
            foreach(int side in new[]{-1,1})
            {
                double z=side*(hz+.2);
                for(double x=-hx;x<=hx+1e-6;x+=4.0){s.Box(x,(8+eav)/2,z,.8,eav-8,.6,cream);s.Box(x,eav-.6,z+side*.02,1.1,.5,.7,0xE8D0AA);}
                for(double x=-hx+2;x<hx;x+=4.0){s.Box(x,(8+eav)/2,z,.08,eav-8.2,.12,iron);s.Box(x,10.2,z,3.2,.08,.12,iron);s.Box(x,eav-.5,z,3.2,.5,.3,cream);}
                s.Box(0,8.0,z,2*hx+.8,.4,.8,cream);s.Box(0,eav+.1,z,2*hx+1.2,.4,1.0,pink);s.Box(0,eav+.36,z,2*hx+1.4,.12,1.1,0xFFF2DC);
            }
            foreach(int side in new[]{-1,1})
            {
                double x=side*(hx+.2);
                for(double z=-hz;z<=hz+1e-6;z+=4.68){s.Box(x,(8+eav)/2,z,.6,eav-8,.8,cream);}
                s.Box(x,8.0,0,.8,.4,2*hz+.8,cream);s.Box(x,eav+.1,0,1.0,.4,2*hz+1.2,pink);
            }
        }
        /// <summary>The end gables under the vault: a sunburst of glazing bars from the rose window over the door line, the panes
        /// glowing (amber west, rose east), an iron frame along the arch.</summary>
        private static void Gables(Sink s)
        {
            double hz=NativeEmporiumLooks.RoofHalfZ,hx=NativeEmporiumLooks.RoofHalfX+.3,eav=NativeEmporiumLooks.Eaves;const int iron=0x2C4A48;
            foreach(int side in new[]{-1,1})
            {
                double x=side*hx;int warm=side<0?0xFFB060:0xFF8AB0,warm2=side<0?0xFF7A3A:0xC86AE0;
                // panes: vertical strips filling under the arch, colour banded by angle from the rose
                for(double z=-hz+.6;z<hz-.3;z+=1.2)
                {
                    double top=NativeEmporiumKit.Arch(z)-.15,h=top-eav;if(h<.2)continue;double a=Math.Atan2(Math.Abs(z),3)/(Math.PI/2);int col=NativeEmporiumLooks.Mix(warm,warm2,a);
                    s.Box(x,eav+h/2,z,.08,h,1.12,col,glow:col,level:.9+.4*(1-a));
                }
                // the sunburst's bars from the rose (at the eaves' middle) to the arch, and the arch frame
                for(int k=0;k<=12;k++){double a=Math.PI*k/12,dz=Math.Cos(a),dy=Math.Sin(a);double r=Math.Min(hz,(NativeEmporiumLooks.Ridge-eav)/Math.Max(.05,dy));
                    double ez=dz*r*.98,ey=eav+dy*r*.98;if(ey>NativeEmporiumKit.Arch(ez))ey=NativeEmporiumKit.Arch(ez)-.1;Rod(s,x+side*.06,eav+.2,0,x+side*.06,ey,ez,.14,iron);}
                for(int k=0;k<16;k++){double z0=-hz+2*hz*k/16,z1=-hz+2*hz*(k+1)/16;Rod(s,x+side*.08,NativeEmporiumKit.Arch(z0),z0,x+side*.08,NativeEmporiumKit.Arch(z1),z1,.3,iron);}
                s.Box(x+side*.08,eav,0,.3,.3,2*hz,iron);
                // the rose: a ring of petals round a bright hub
                for(int k=0;k<12;k++){double a=k*Math.PI/6;s.Box(x+side*.1,eav+2.6+Math.Sin(a)*1.5,Math.Cos(a)*1.5,.1,.5,.5,0xFFE6A0,yaw:0,pitch:0,roll:0,glow:0xFFD070,level:1.6);}
                s.Box(x+side*.12,eav+2.6,0,.1,.9,.9,0xFFF6D0,glow:0xFFF0C0,level:2.2);
            }
        }
        private static void Rod(Sink s,double x0,double y0,double z0,double x1,double y1,double z1,double t,int hex,int glow=0,double level=0)
        {
            double dx=x1-x0,dy=y1-y0,dz=z1-z0,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l<1e-4)return;
            double yaw=Math.Atan2(dx,dz)*180/Math.PI,pitch=-Math.Asin(Math.Max(-1,Math.Min(1,dy/l)))*180/Math.PI;
            s.Box((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,t,t,l,hex,pitch:pitch,yaw:yaw,glow:glow,level:level);
        }
        /// <summary>A building block: walls, its lit floors as window bands on the faces toward the hall, a cornice, a roof.</summary>
        private static void Block(Sink s,double x,double z,double w,double d,double h,int wall,int seed,double lit=.55)
        {
            s.Box(x,h/2,z,w,h,d,wall,shade:.9+.2*H(seed));s.Box(x,h+.3,z,w+.6,.6,d+.6,NativeEmporiumLooks.Scale(wall,.75));
            for(double y=4.5;y<h-1.5;y+=3.4)
            {
                bool on=H(seed,(int)y)<lit;int glass=on?(H(seed,(int)y,3)<.5?0xFFC27A:0xFFE6B0):0x3A3050;
                double nz=z>0?z-d/2-.06:z+d/2+.06,nx=x>0?x-w/2-.06:x+w/2+.06;
                s.Box(x,y,nz,w*.86,1.3,.1,glass,glow:on?glass:0,level:on?1.2:0);s.Box(nx,y,z,.1,1.3,d*.86,glass,glow:on?glass:0,level:on?1.1:0);
            }
        }
        private static void Street(Sink s)
        {
            int[] walls={0xB0583C,0xD8A070,0xE8C8A0,0x8A4A40,0xC07850,0xA8B0B8,0xD88A78};
            // across the north street and the south street: rows of blocks 22..36 m
            int n=0;
            foreach(int side in new[]{-1,1})
            {
                for(double x=-70;x<=70;x+=17.5)
                {
                    double h=20+H(601,n)*16,w=15+H(601,n,1)*2,d=14;Block(s,x+(H(601,n,2)-.5)*3,side*(46+H(601,n,3)*6),w,d,h,walls[n%walls.Length],700+n);n++;
                }
                for(double z=-50;z<=50;z+=17.5){double h=18+H(602,n)*14;Block(s,side*(52+H(602,n,1)*6),z,14,15,h,walls[(n+3)%walls.Length],700+n);n++;}
            }
            // the billboard on the north block: a frame, a panel and a giant hot dog
            double bx=-12,bz=44,by=30;s.Box(bx,by-2.5,bz,.4,5,.4,0x2A2A30);s.Box(bx+10,by-2.5,bz,.4,5,.4,0x2A2A30);s.Box(bx+5,by+3,bz,14,6.5,.5,0xFFE84A,glow:0xFFE07A,level:.6);
            s.Box(bx+5,by+2.2,bz-.4,10,1.6,.4,0xE9A85A);s.Box(bx+5,by+3.1,bz-.5,11,1.0,.4,0xC4502C,glow:0xC4502C,level:.2);for(int k=0;k<8;k++)s.Box(bx+.8+k*1.2,by+3.7,bz-.72,.9,.16,.1,0xFFC81E,roll:(k&1)==0?20:-20,glow:0xFFC81E,level:.8);
            s.Box(bx+5,by+5.6,bz-.3,13,.7,.2,0xFF3FA4,glow:0xFF3FA4,level:2.0);
            // water towers and the deco tower south with its spire
            foreach(var (x,z,y) in new[]{(30.0,48.0,26.0),(-40.0,-50.0,24.0),(58.0,-20.0,22.0)}){Oct(s,x,y,y+5,z,2.4,0x7A5034);s.Box(x,y+5.8,z,5.4,1.6,5.4,0x5A3A28,yaw:45);s.Box(x,y-3,z,.3,6,.3,0x3A3A40);}
            Oct(s,20,0,70,-70,7,0xE8D8C0);Oct(s,20,70,84,-70,5,0xF0E2CC);Oct(s,20,84,94,-70,3,0xF6EAD6);s.Box(20,102,-70,.6,16,.6,0xD8C8A0);s.Box(20,111,-70,.8,.8,.8,0xFF3020,glow:0xFF3020,level:2.4);
            for(double y=8;y<68;y+=4)Oct(s,20,y,y+1.3,-70,7.08,0xFFD8A0,0xFFC27A,H(603,(int)y)<.6?1.0:0);
        }
        private static void Oct(Sink s,double x,double y0,double y1,double z,double r,int hex,int glow=0,double level=0)
        {s.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r,hex,glow:glow,level:level);s.Box(x,(y0+y1)/2,z,2*r,y1-y0,2*r,hex,yaw:45,glow:glow,level:level);}
        private static void Skyline(Sink s)
        {
            for(int k=0;k<56;k++)
            {
                double a=k*2*Math.PI/56+H(604,k)*.08,r=170+H(604,k,1)*130,x=Math.Sin(a)*r,z=Math.Cos(a)*r,h=30+H(604,k,2)*90,w=14+H(604,k,3)*20;
                int col=NativeEmporiumLooks.Mix(0x6A4A78,0xA0607A,H(604,k,4));s.Box(x,h/2,z,w,h,w,col,yaw:a*180/Math.PI);
                for(double y=6;y<h-4;y+=7){if(H(604,k,(int)y)>.4)continue;s.Box(x-Math.Sin(a)*(w/2+.1),y,z-Math.Cos(a)*(w/2+.1),w*.8,1.2,.2,0xFFC88A,yaw:a*180/Math.PI,glow:0xFFC88A,level:1.0);}
            }
        }
    }
}
