using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's backdrop (docs/arenas/CLUB.md "The look"): the old substation hall round the club, box-built and
    /// deterministic, baked once into three ChipArena/SourceLit sector meshes (Runtime NativeClubDraw draws them on
    /// <see cref="NativeSceneryBuilder.BackdropLayer"/> without shadows):
    /// <list type="bullet">
    /// <item>Near: the hall's brick above the 12 m walls to the roof, the riveted roof trusses, purlins and the ridge's
    /// clerestory with the night through it, the old gantry crane parked on its rails, pendant lamps; the crowd galleries beyond
    /// the side walls' arches (decks, rails, back walls, lamps); the sunken crowd pit beyond the north barrier.</item>
    /// <item>Landmark: outside the great south window, the quay below the club, the river with the lights' reflections, the
    /// lit cable-stayed bridge and the cooling tower turned into a billboard on the far bank.</item>
    /// <item>Far: the city on the far bank, towers on a grid with lit windows, aviation lights' masts, haze bands.</item>
    /// </list>
    /// The keep-out (<see cref="Enters"/>, thrown while building): nothing stands inside the map's bounds grown by
    /// <see cref="Margin"/> m between the floor's base and the bounds' ceiling (12 m, the hall walls' height: nothing can reach
    /// above it); the roof and the upper walls stand above it, the pit's floor and the quay below the floor's base.
    /// Unity frame, metres; colours sRGB; allocation only while building.
    /// </summary>
    public static class NativeClubScenery
    {
        public const double Margin=1;public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public const double RoofY=17.0,PitFloor=-.4,QuayY=-3.2,RiverY=-4.6;
        public sealed class Sink
        {
            public readonly NativeSceneryBuilder B=new NativeSceneryBuilder(Sectors);public double MinX,MaxX,MinZ,MaxZ,Under,Ceiling;public int Boxes;
            public void Box(double x,double y,double z,double sx,double sy,double sz,int hex,double pitch=0,double yaw=0,double roll=0,int glow=0,double level=0,double shade=1,double grime=0,int skip=0)
            {
                if(!(sx>0&&sy>0&&sz>0))return;
                NativeNewStageScenery.Extent(x,y,z,sx,sy,sz,pitch,yaw,roll,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
                if(Enters(this,x0,x1,y0,y1,z0,z1))throw new InvalidOperationException("Club scenery: a box enters the play space at "+x.ToString("0.##")+", "+y.ToString("0.##")+", "+z.ToString("0.##"));
                B.Box(x,y,z,sx,sy,sz,hex,pitch,yaw,roll,glow,level,shade,grime,skip);Boxes++;
            }
        }
        /// <summary>Whether a world box enters the play space: inside the bounds grown by Margin, above the floor's base and below the ceiling.</summary>
        public static bool Enters(Sink s,double x0,double x1,double y0,double y1,double z0,double z1)=>y1>s.Under+1e-4&&y0<s.Ceiling-1e-4&&x1>s.MinX+1e-4&&x0<s.MaxX-1e-4&&z1>s.MinZ+1e-4&&z0<s.MaxZ-1e-4;
        private static double H(int a,int b=0,int c=0)=>NativeClubLooks.Hash(a,b,c);

        /// <summary>The club's backdrop sectors (null for another map).</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(map==null||!NativeClubLooks.Active(map.id))return null;var b=map.bounds;
            var s=new Sink{MinX=(b?.minX??-26)-Margin,MaxX=(b?.maxX??26)+Margin,MinZ=(b?.minZ??-20)-Margin,MaxZ=(b?.maxZ??20)+Margin,Ceiling=b!=null&&b.ceiling>0?b.ceiling:12};s.B.Anime=true;
            foreach(var k in map.blocks??Array.Empty<ContentBlock>())if(k!=null)s.Under=Math.Min(s.Under,k.pos.y-k.size.y/2);
            s.B.Current=Near;Hall(s);Galleries(s);Pit(s);
            s.B.Current=Landmark;River(s);
            s.B.Current=Far;City(s);
            var o=new NativeSceneryMesh[Sectors];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);return o;
        }

        // ---------------- the hall above 12 m ----------------
        public const int Brick=0x5A3230,BrickDark=0x3A2226,Iron=0x2A2830,IronLit=0x4A4656,Roof=0x141018,Glass=0x1A2246,Lamp=0xFFC88A;
        private static void Hall(Sink s)
        {
            double top=s.Ceiling;
            // the walls carried up to the eaves (12..17), the gable band, the brick's string course
            s.Box(-25.8,(top+RoofY)/2,0,.6,RoofY-top,41.4,Brick,grime:.12);s.Box(25.8,(top+RoofY)/2,0,.6,RoofY-top,41.4,Brick,grime:.12);
            s.Box(0,(top+RoofY)/2,-19.8,52.2,RoofY-top,.6,Brick,grime:.12);s.Box(0,(top+RoofY)/2,19.8,52.2,RoofY-top,.6,Brick,grime:.12);
            foreach(double x in new[]{-25.45,25.45})s.Box(x,top+.35,0,.14,.3,40,BrickDark);s.Box(0,top+.35,-19.45,51,.3,.14,BrickDark);s.Box(0,top+.35,19.45,51,.3,.14,BrickDark);
            // the roof deck, the ridge clerestory (night blue through it), riveted trusses every 4 m and purlins
            s.Box(-13.5,RoofY+.6,0,25,.4,41.4,Roof,roll:-6);s.Box(13.5,RoofY+.6,0,25,.4,41.4,Roof,roll:6);
            s.Box(0,RoofY+2.0,0,3.4,.2,41,Glass,glow:0x3A4A9A,level:.35);
            for(double z=-18;z<=18.1;z+=4)
            {
                s.Box(0,top+.6,z,51,.35,.28,Iron);
                foreach(double sx in new[]{-1.0,1.0}){s.Box(sx*12.8,RoofY-.2,z,26,.28,.24,Iron,roll:sx*-6);for(int k=0;k<6;k++){double x=sx*(2+k*4);s.Box(x,(top+.8+RoofY-.2-Math.Abs(x)*.105)/2,z,.14,RoofY-1-Math.Abs(x)*.105-top,.14,IronLit);}}
                // pendant lamps hung from the bottom chord: dim, warm
                foreach(double x in new[]{-16.0,0.0,16.0}){s.Box(x,top+.5,z+2,.05,.6,.05,Iron);s.Box(x,top+.12,z+2,.5,.18,.5,Iron,glow:Lamp,level:.35);}
            }
            for(double x=-22;x<=22.1;x+=5.5)s.Box(x,RoofY+.15-Math.Abs(x)*.105,0,.2,.2,40.6,IronLit);
            // the old gantry crane parked over the north end on its rails (the substation's), its hook block high
            foreach(double x in new[]{-24.9,24.9})s.Box(x,13.2,0,.5,.5,40,Iron);
            s.Box(0,13.6,15.5,50,.9,1.0,0x6A5A2A,grime:.25);s.Box(0,13.6,17.0,50,.9,1.0,0x6A5A2A,grime:.25);s.Box(-6,14.4,16.2,2.6,1.2,2.6,0x5A4A26);s.Box(-6,12.9,16.2,.08,1.6,.08,Iron);s.Box(-6,12.25,16.2,.5,.4,.3,0x8A7A3A);
        }
        // ---------------- the crowd galleries beyond the side walls' arches ----------------
        private static void Galleries(Sink s)
        {
            double y=NativeClubLooks.GalleryY;
            foreach(double sx in new[]{-1.0,1.0})
            {
                double inner=sx*27.05,back=sx*34;
                s.Box((inner+back)/2,y-.25,0,Math.Abs(back-inner),.5,41,0x24202C);
                s.Box(sx*27.25,y+.55,0,.1,1.1,40,IronLit);s.Box(sx*27.25,y+1.12,0,.16,.06,40,0x8A8A96);
                s.Box(back,y+3,0,.4,6,41,BrickDark,grime:.2);s.Box((inner+back)/2,y+5.4,0,Math.Abs(back-inner)+.6,.3,41,Roof);
                // lamps on the back wall, coloured, dim: the crowd stands against them
                for(int k=0;k<10;k++){double z=-18+k*4;int c=NativeClubLooks.Party[k%NativeClubLooks.Party.Length];s.Box(back-sx*.25,y+3.2,z,.1,.5,.9,c,glow:c,level:.9);}
                // the LED band the crowd stands against (backlit silhouettes), the downlights in the gallery's ceiling
                for(int k=0;k<8;k++){double z=-17.5+k*5;int c=NativeClubLooks.Party[(k*3)%NativeClubLooks.Party.Length];s.Box(back-sx*.22,y+1.4,z,.06,1.6,4.6,c,glow:c,level:1.3);}
                for(int k=0;k<10;k++){double z=-18+k*4;s.Box(sx*29.5,y+5.2,z,.5,.08,.5,0xFFE0C0,glow:0xFFE0C0,level:1.6);}
                // the stair down at each end and the bar counter at the back (silhouettes)
                s.Box(sx*31.5,y+.55,-14,1.2,1.1,4,0x3A2018);s.Box(sx*31.5,y+.55,14,1.2,1.1,4,0x3A2018);
            }
        }
        // ---------------- the crowd pit beyond the north barrier (sunk 0.4 m) ----------------
        private static void Pit(Sink s)
        {
            foreach(double sx in new[]{-1.0,1.0})
            {
                double x0=sx*10.8,x1=sx*26.2,cx=(x0+x1)/2,w=Math.Abs(x1-x0);
                s.Box(cx,PitFloor-.3,25.6,w,.6,11.2,0x1A1622);
                s.Box(cx,5,31.4,w,11,.6,BrickDark,grime:.25);s.Box(sx*26.2,5,26.275,.6,11,10.45,BrickDark);s.Box(sx*10.6,6,26,.4,12,10,0x0E0C12);
                s.Box(cx,11.2,26.275,w,.4,10.45,Roof);
                for(int k=0;k<4;k++){double x=x0+sx*(2+k*3.6);s.Box(x,8.6,30.95,1.4,.12,.12,NativeClubLooks.Magenta,glow:NativeClubLooks.Magenta,level:1.6);s.Box(x,3.2,30.95,.12,4.6,.12,0x22E6FF,glow:0x22E6FF,level:.8);}
                s.Box(cx,1.6,31.0,w-.6,2.2,.1,NativeClubLooks.Violet,glow:NativeClubLooks.Violet,level:1.1);
            }
        }
        // ---------------- outside the great window: the quay, the river, the bridge, the far bank's billboard ----------------
        private static void River(Sink s)
        {
            s.Box(0,QuayY-.4,-27,160,.8,12,0x2A2630);for(double x=-60;x<=60.1;x+=8)s.Box(x,QuayY+1.6,-30.5,.12,3.2,.12,Iron);
            for(double x=-60;x<=60.1;x+=8)s.Box(x,QuayY+3.25,-30.5,.4,.14,.4,Lamp,glow:Lamp,level:2.2);
            s.Box(0,QuayY+.55,-32.6,160,.3,.3,IronLit);
            // the water: dark facets (a slow swell's bands) with the city's light laid on in streaks
            for(int i=0;i<40;i++)for(int j=0;j<12;j++)
            {
                double x=-120+i*6+3,z=-36-j*9-4.5,h1=H(i,j,1);int c=NativeNewStageScenery.Mix(0x0A0C1C,0x1E2650,.25*h1+.2*Math.Sin(x*.05+z*.03));
                s.Box(x,RiverY,z,6.2,.1,9.2,c,(h1-.5)*1.2,0,(H(j,i,2)-.5)*1.2,skip:1|2|8|16|32);
            }
            for(int k=0;k<36;k++){double x=-110+k*6.3+H(k,5)*3,len=8+H(k,6)*28;int c=NativeClubLooks.Party[k%NativeClubLooks.Party.Length];s.Box(x,RiverY+.08,-60-len/2-H(k,7)*30,.35+H(k,8)*.4,.04,len,c,glow:c,level:.55+H(k,9)*.5,skip:1|2|8|16|32);}
            // the cable-stayed bridge downstream: deck, two pylons, the stays as lit lines
            double bz=-150;s.Box(0,6,bz,400,1.4,10,0x1E1C26);for(double x=-190;x<=190.1;x+=10)s.Box(x,7.2,bz-5.2,.3,.3,.3,Lamp,glow:Lamp,level:2.6);
            foreach(double px in new[]{-60.0,60.0})
            {
                s.Box(px,30,bz,3,52,3,0x2E2C38);s.Box(px,56.5,bz,1.2,1.2,1.2,0xFF3020,glow:0xFF3020,level:3);
                for(int k=1;k<=7;k++)foreach(double sx in new[]{-1.0,1.0}){double ax=px,ay=54-k*3,bx2=px+sx*k*10,by2=6.8,dx=bx2-ax,dy=by2-ay,l=Math.Sqrt(dx*dx+dy*dy);
                    s.Box((ax+bx2)/2,(ay+by2)/2,bz,l,.14,.14,0xC8F4FF,0,0,Math.Atan2(dy,dx)*180/Math.PI,glow:0xC8F4FF,level:1.2);}
            }
            // the far bank's old cooling tower, its skin a billboard of light (a ring of neon bands)
            double tx=-95,tz=-260;for(int k=0;k<14;k++){double y=k*4+2,r=26-10*Math.Sin(Math.PI*k/14*.9);s.Box(tx,y,tz,2*r,4.1,2*r,0x2A2634);s.Box(tx,y,tz,2*r,4.1,2*r,0x2A2634,yaw:45);
                if(k%3==1){int c=NativeClubLooks.Party[(k/3)%NativeClubLooks.Party.Length];s.Box(tx,y+1.4,tz+r+.05,r*1.6,.5,.2,c,glow:c,level:2.0);}}
        }
        // ---------------- the far bank ----------------
        private static void City(Sink s)
        {
            int seed=0;
            for(int i=-9;i<=9;i++)for(int j=0;j<4;j++)
            {
                seed++;double x=i*26+(H(seed,1)-.5)*10,z=-200-j*60-H(seed,2)*20;if(Math.Abs(x+95)<40&&j==1)continue;
                double h=24+H(seed,3)*(j==0?50:110),w=12+H(seed,4)*14,d=12+H(seed,5)*14;
                Tower(s,seed,x,z,w,d,h);
            }
            // haze bands over the water and the bank, aviation masts
            for(int k=0;k<3;k++)s.Box(0,4+k*7,-180-k*40,600,3,6,NativeNewStageScenery.Mix(0x1A1030,0x2A1A48,k*.4),glow:0x2A1A48,level:.25);
            foreach(double x in new[]{-180.0,40.0,210.0}){s.Box(x,60,-330,1.4,120,1.4,0x24222C);s.Box(x,121,-330,1.6,1.6,1.6,0xFF3020,glow:0xFF3020,level:3.0);}
        }
        private static void Tower(Sink s,int seed,double x,double z,double w,double d,double h)
        {
            s.Box(x,h/2,z,w,h,d,NativeNewStageScenery.Mix(0x14121C,0x24202E,H(seed,7)),grime:.1);
            int rows=(int)(h/3.6);
            for(int r=0;r<rows;r++)
            {
                double y=2+r*3.6;bool lit=H(seed,r,8)<.42;if(!lit)continue;int c=H(seed,r,9)<.6?0xFFD8A8:H(seed,r,9)<.8?0x9AD8FF:0xFF9AD8;
                s.Box(x,y,z+d/2+.05,w*(.4+.5*H(seed,r,10)),1.3,.1,c,glow:c,level:.55+.6*H(seed,r,11));
            }
            if(H(seed,12)<.35){int c=NativeClubLooks.Party[seed%NativeClubLooks.Party.Length];s.Box(x,h+.4,z+d/2+.1,w*.8,.6,.12,c,glow:c,level:2.2);}
        }
    }
}
