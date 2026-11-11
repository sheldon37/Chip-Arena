using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pier 9's backdrop (docs/arenas/HARBOR.md "The look"): what stands round the pier in the night sea, box-built and
    /// deterministic, baked once into ChipArena/SourceLit sector meshes (Runtime NativePierPresentation draws them on
    /// <see cref="NativeSceneryBuilder.BackdropLayer"/> without shadows):
    /// <list type="bullet">
    /// <item>Near (the stage art models these; with the bank this sector is not drawn): the Ferris wheel's two A-frames, axle and
    /// landing stage off the west side, the coaster's trestle past both edges and its two spikes with their lattice and lamps, the
    /// boardwalk's neck south to the shore with its lamp posts, the pier head's landing stage north.</item>
    /// <item>Landmark: the fireworks barge moored out east, the old lighthouse on its rock in the north-east, the bell buoys.</item>
    /// <item>Far: the sea itself, the shore in the south (the beach, the promenade and its lamps, the seafront's lit hotels and
    /// the hills behind), the headland in the east, ships on the horizon.</item>
    /// </list>
    /// The keep-out (<see cref="NativeNewStageScenery.Enters"/>, thrown on while building): nothing stands inside the bounds grown
    /// by 1 m except what lies under the sea's surface line (the sea is no floor: a fall dies at killY just under it). Unity
    /// frame, metres; colours sRGB; allocation only while building.
    /// </summary>
    public static class NativePierScenery
    {
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public const int SeaDeep=0x0A1630,SeaCrest=0x2A4A7A,Timber=0x6E4E36,TimberDark=0x3A2A20,White=0xE8E4DA,Cream=0xE6D8B8,Red=0xB8302A,Teal=0x1F8E96,Gold=0xD8A840,Iron=0x2A2E36,Navy=0x1C2A4A,
            Warm=0xFFC870,Pink=0xFF5FA8,Cyan=0x5FE8FF,Sand=0x8A7A62,Rock=0x23262E,Hill=0x10141E,Hotel=0x4A4658,HotelLight=0x6A6272,Window=0xFFD890;
        /// <summary>The fireworks barge (its deck's middle) and the lighthouse's lantern: the effects launch and sweep from them.</summary>
        public const double BargeX=118,BargeZ=34,BargeY=NativePierLooks.Sea+1.6,LightX=190,LightY=44,LightZ=250,ShoreZ=-96;
        private static double H(int a,int b=0,int c=0)=>NativePierLooks.Hash(a,b,c);

        /// <summary>The backdrop's sectors (null for another map): [near, landmark, far].</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(!NativePierLooks.Active(map?.id))return null;
            var s=new NativeNewStageScenery.Sink{Map=map.id};NativeNewStageScenery.PlaySpace(map,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);s.Under=NativePierLooks.Sea+.6;s.B.Anime=true;
            WheelFrame(s);Coaster(s);Neck(s);Out(s);Sea(s);Shore(s);
            var o=new NativeSceneryMesh[Sectors];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);return o;
        }
        private static void Bar(NativeNewStageScenery.Sink s,double x0,double y0,double z0,double x1,double y1,double z1,double t,int hex,int glow=0,double level=0)=>NativeNewStageScenery.Bar(s,x0,y0,z0,x1,y1,z1,t,hex,1,glow,level);
        private static void Pile(NativeNewStageScenery.Sink s,double x,double z,double top,double t=.45){s.Box(x,(top+NativePierLooks.Sea-2.4)/2,z,t,top-(NativePierLooks.Sea-2.4),t,TimberDark,grime:.3);}

        // ---------------- near: the wheel's frame, the coaster, the neck ----------------
        private static void WheelFrame(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;double cx=NativePierLooks.WheelX,cy=NativePierLooks.WheelY,g=NativePierLooks.WheelGap+1.6;
            // the landing stage in two halves, the track between them; piles under it
            foreach(int side in new[]{-1,1})
            {
                s.Box(cx,-.25,side*7.4,13,.5,10.4,Timber);for(double x=cx-5.5;x<=cx+5.5;x+=5.5)for(double z=3.4;z<=11.4;z+=4)Pile(s,x,side*z,-.5);
                s.Box(cx,.55,side*12.5,13,1.1,.16,Cream);s.Box(cx-6.4,.55,side*7.4,.16,1.1,10.4,Cream);
                // the A-frames' legs to the axle and their feet
                foreach(int f in new[]{-1,1}){Bar(s,cx+f*g,0,side*10.4,cx+f*g,cy,0,.75,White);s.Box(cx+f*g,.35,side*10.4,1.5,.7,1.5,Iron);for(int k=1;k<12;k++){double u=k/12.0;s.Box(cx+f*(g+.42),u*cy,side*10.4*(1-u),.12,.2,.2,Warm,glow:Warm,level:1.8);}}
                for(int k=1;k<=3;k++){double u=k/4.0;Bar(s,cx-g,u*cy,side*10.4*(1-u),cx+g,u*cy,side*10.4*(1-u),.3,White);}
            }
            foreach(int f in new[]{-1,1}){for(int k=1;k<=2;k++){double u=k/3.0;Bar(s,cx+f*g,u*cy,-10.4*(1-u),cx+f*g,u*cy,10.4*(1-u),.3,White);}}
            s.Box(cx,cy,0,2*g+1.2,1.1,1.1,Iron);s.Box(cx,cy,0,2*g+1.6,1.7,1.7,Gold,pitch:45);
            // the ticket hut and the queue's lamps
            s.Box(cx+4,1.4,8.2,2.6,2.8,2.4,Red);s.Box(cx+4,2.95,8.2,3.2,.3,3.0,Cream);s.Box(cx+4,1.7,6.98,1.4,.9,.06,Window,glow:Window,level:1.5);
            for(int k=0;k<6;k++)s.Box(cx-5+k*2,2.6,-11.9,.22,.22,.22,k%2==0?Pink:Cyan,glow:k%2==0?Pink:Cyan,level:2);
        }
        private static void Coaster(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;const double half=20;
            foreach(int side in new[]{-1,1})
            {
                double px=0,py=0;bool first=true;int n=0;
                for(double d=5.1;d<=NativePierLooks.TrackTop+.01;d+=1.9,n++)
                {
                    NativePierLooks.Track(side*(half+d),half,out double x,out double y,out double pitch);
                    if(!first)
                    {
                        foreach(double z in new[]{-.72,.72})Bar(s,px,py-.05,z,x,y-.05,z,.12,0x9AA2AA);
                        foreach(double z in new[]{-.98,.98})Bar(s,px,py-.3,z,x,y-.3,z,.24,Red);
                        s.Box((px+x)/2,(py+y)/2-.16,0,.22,.14,2.3,TimberDark,roll:pitch*180/Math.PI);
                        // the lamps up the spike's edges
                        if(d>NativePierLooks.TrackLevel&&n%2==0)foreach(double z in new[]{-1.16,1.16})s.Box(x,y-.3,z,.16,.16,.16,n%4==0?Warm:Cyan,glow:n%4==0?Warm:Cyan,level:2);
                    }
                    px=x;py=y;first=false;
                }
                // the bents: a pair of posts under the track every 4 m of run, braced to each other and to the next bent
                double lastX=0,lastY=0;bool any=false;
                for(double d=6.5;d<=NativePierLooks.TrackTop-1;d+=d<NativePierLooks.TrackLevel?5:3.4)
                {
                    NativePierLooks.Track(side*(half+d),half,out double x,out double y,out _);double top=y-.45,foot=NativePierLooks.Sea-2.4;
                    // the level run's bents leave the wheel's landing stage alone on the west side
                    foreach(double z in new[]{-1.25,1.25}){s.Box(x,(top+foot)/2,z*(1+.012*(top-foot)),.34,top-foot,.34,White);}
                    for(double yb=NativePierLooks.Sea+1.2;yb<top-2.2;yb+=4.4){double w0=1.25*(1+.012*(yb-foot)),w1=1.25*(1+.012*(yb+3.6-foot));Bar(s,x,yb,-w0,x,yb+3.6,w1,.16,White);Bar(s,x,yb,w0,x,yb+3.6,-w1,.16,White);}
                    if(any&&top>4)foreach(double z in new[]{-1.25,1.25}){Bar(s,lastX,Math.Max(NativePierLooks.Sea+1,lastY-9),z,x,top-1,z,.16,White);Bar(s,lastX,lastY-1,z,x,Math.Max(NativePierLooks.Sea+1,top-9),z,.16,White);}
                    lastX=x;lastY=top;any=true;
                }
                // the spike's cap: a stop and a red lamp
                NativePierLooks.Track(side*(half+NativePierLooks.TrackTop),half,out double tx,out double ty,out _);s.Box(tx,ty+.3,0,.5,1.2,2.4,Red);s.Box(tx,ty+1.2,0,.4,.4,.4,0xFF4030,glow:0xFF4030,level:2.6);
            }
        }
        private static void Neck(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;
            // the boardwalk on south from the Gate to the shore; a short landing stage off the pier head in the north
            s.Box(0,-.25,(-39.2+ShoreZ)/2,12,.5,-39.2-ShoreZ,Timber);
            for(double z=-46;z>ShoreZ;z-=6){foreach(double x in new[]{-5.2,5.2})Pile(s,x,z,-.5);foreach(int side in new[]{-1,1}){s.Box(side*5.9,.55,z+3,.14,1.1,5.6,Cream);}}
            for(double z=-46;z>ShoreZ+4;z-=9)foreach(int side in new[]{-1,1}){s.Box(side*5.6,2.2,z,.14,3.4,.14,Iron);s.Box(side*5.6,4.1,z,.4,.4,.4,Warm,glow:Warm,level:2.2);}
            s.Box(0,-.25,42.2,9,.5,6,Timber);foreach(double x in new[]{-4,4})foreach(double z in new[]{40.0,44.4})Pile(s,x,z,-.5);
            s.Box(0,3.2,44.6,.16,6.4,.16,White);s.Box(.7,5.9,44.6,1.2,.8,.05,Red);
        }

        // ---------------- landmark: the barge, the lighthouse, the buoys ----------------
        private static void Out(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Landmark;double sea=NativePierLooks.Sea;
            // the fireworks barge: a flat hull, mortar racks, a wheelhouse, deck lamps
            s.Box(BargeX,sea+.6,BargeZ,26,2,9,Rock);s.Box(BargeX,sea+1.7,BargeZ,25,.2,8.4,Iron);s.Box(BargeX-9,sea+3.2,BargeZ,4,3,5,Navy);s.Box(BargeX-9,sea+3.9,BargeZ-2.52,2.6,.9,.06,Window,glow:Window,level:1.4);
            for(int k=0;k<8;k++){s.Box(BargeX-3+k*1.9,sea+2.3,BargeZ,.8,1.1,5.6,0x3A3E46,pitch:(k%2==0?8:-8));}
            foreach(int f in new[]{-1,1})for(int k=0;k<5;k++)s.Box(BargeX-11+k*5.5,sea+2.4,BargeZ+f*4.3,.25,.25,.25,Warm,glow:Warm,level:2);
            // the lighthouse on its rock
            for(int k=0;k<7;k++)s.Box(LightX+(H(31,k)-.5)*26,sea+1+H(31,k,1)*3,LightZ+(H(31,k,2)-.5)*20,10+H(31,k,3)*12,5+H(31,k,4)*5,9+H(31,k,5)*10,Rock,yaw:H(31,k,6)*90);
            for(int k=0;k<6;k++)NativeNewStageScenery.Octagon(s,LightX,sea+5+k*6,sea+11+k*6,LightZ,4.2-k*.38,k%2==0?White:Red);
            NativeNewStageScenery.Octagon(s,LightX,sea+41,sea+42,LightZ,3.4,Iron);NativeNewStageScenery.Octagon(s,LightX,LightY-1.6,LightY+1.6,LightZ,1.9,Window,1,Window,3.2);NativeNewStageScenery.Octagon(s,LightX,LightY+1.6,LightY+3,LightZ,2.3,Iron);
            // bell buoys
            double[] buoys={62,-58, -74,52, 20,96, -52,-70};
            for(int k=0;k<buoys.Length/2;k++){double x=buoys[k*2],z=buoys[k*2+1];s.Box(x,sea+.5,z,1.6,1.4,1.6,Red,yaw:45);s.Box(x,sea+2.2,z,.2,2.4,.2,Iron);s.Box(x,sea+3.6,z,.4,.4,.4,k%2==0?0x40FF90:0xFF4030,glow:k%2==0?0x40FF90:0xFF4030,level:2.4);}
        }

        // ---------------- far: the sea, the shore, the headland, the ships ----------------
        private static void Sea(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Far;NativeNewStageScenery.Sea(s,41,NativePierLooks.Sea,-900,900,ShoreZ-6,900,4,5,SeaDeep,SeaCrest,11);
        }
        private static void Shore(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Far;double sea=NativePierLooks.Sea;
            // the beach, the sea wall, the promenade and its lamps
            s.Box(0,sea-.2,ShoreZ-20,1800,1.2,40,Sand,pitch:-2);s.Box(0,sea+1.6,ShoreZ-34,1800,4.6,3,0x5A5A62);s.Box(0,-.1,ShoreZ-60,1800,.6,50,0x2A2C34);
            for(double x=-520;x<=520;x+=13){s.Box(x,2.6,ShoreZ-33,.2,5,.2,Iron);s.Box(x,5.3,ShoreZ-33,.6,.6,.6,Warm,glow:Warm,level:2.2);}
            // the seafront: hotels with lit windows, a domed pavilion, a clock tower
            int n=0;
            for(double x=-560;x<560;n++)
            {
                double w=16+H(51,n)*22,h=14+H(51,n,1)*20,d=18+H(51,n,2)*8,z=ShoreZ-78-H(51,n,3)*8;int wall=H(51,n,4)<.5?Hotel:HotelLight;
                if(Math.Abs(x+w/2)<14){x+=w+2;continue;}
                s.Box(x+w/2,h/2,z,w,h,d,wall);s.Box(x+w/2,h+.5,z,w+1,1,d+1,0x2E2A36);
                for(double wy=3.2;wy<h-2;wy+=3.4)for(double wx=x+2;wx<x+w-1.5;wx+=3.1){double lit=H(52,(int)(wx*3),(int)(wy*3));if(lit<.42)continue;int c=lit>.93?Cyan:lit>.86?Pink:Window;s.Box(wx,wy,z+d/2+.05,1.5,1.9,.1,c,glow:c,level:lit>.86?1.9:1.25);}
                if(n%5==2){NativeNewStageScenery.Octagon(s,x+w/2,h+1,h+5,z,4,Teal);s.Box(x+w/2,h+7,z,.5,4,.5,Gold);}
                x+=w+2+H(51,n,5)*5;
            }
            // the entrance arch where the boardwalk meets the promenade
            foreach(int side in new[]{-1,1}){s.Box(side*7.5,4,ShoreZ-37,2.4,8,2.4,Cream);s.Box(side*7.5,8.8,ShoreZ-37,3,1.6,3,Teal);}
            s.Box(0,8.2,ShoreZ-37,13,1.6,1,Navy);for(int k=0;k<19;k++)s.Box(-5.4+k*.6,8.2,ShoreZ-36.4,.24,.24,.24,k%2==0?Warm:Pink,glow:k%2==0?Warm:Pink,level:2.4);
            // the hills behind the town and the headland running out east
            for(int k=0;k<26;k++){double x=-700+k*56+H(61,k)*20,h=30+H(61,k,1)*50;s.Box(x,h/2-4,ShoreZ-170-H(61,k,2)*60,90,h,70,Hill,yaw:H(61,k,3)*30);}
            for(int k=0;k<12;k++){double t=k/11.0,x=420+t*260,z=ShoreZ-60+t*330,h=46*(1-t*.75)+H(62,k)*8;s.Box(x,h/2-3,z,70,h,60,Hill,yaw:35+H(62,k,1)*20);}
            // ships on the horizon
            double[] ships={-380,620,90, 260,700,60, 640,420,120};
            for(int k=0;k<ships.Length/3;k++){double x=ships[k*3],z=ships[k*3+1],l=ships[k*3+2];s.Box(x,sea+3,z,l,7,12,0x0C0E16);s.Box(x-l*.3,sea+11,z,l*.2,10,9,0x14161E);for(int j=0;j<6;j++)s.Box(x-l*.4+j*l*.16,sea+8,z-6.2,1.2,1.2,.3,Window,glow:Window,level:2);}
        }
    }
}
