using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Polaris Station's box-built backdrop (docs/arenas/POLAR.md), baked once per map into ChipArena/SourceLit sector meshes on
    /// NativeSceneryBuilder.BackdropLayer (no shadows). It draws only without the modelled art: with the stage bank
    /// ChipArenaStagePolar the bank's own chunks stand at the same places. Three sectors:
    /// <list type="bullet">
    /// <item>Near: the ice shelf beyond the pressure ridges with its drifts, the sea past the shelf's edge, the icebreaker's
    /// after body (the hull running north out of the map, the house above the wheelhouse, the funnel, the mainmast, boats and
    /// lit ports) and the snow piled against her.</item>
    /// <item>Landmark: icebergs standing in the sea, the brash ice along the shelf, the far met tower and the fuel depot's lights.</item>
    /// <item>Far: the ice cap's rim and its nunataks round the horizon.</item>
    /// </list>
    /// The keep-out (<see cref="NativeNewStageScenery.Enters"/>, thrown on while building): nothing stands inside the bounds grown by
    /// 1 m except what lies wholly under the shelf's base. Unity frame, metres; colours sRGB; allocation only while building.
    /// </summary>
    public static class NativePolarScenery
    {
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public const int SnowLit=0xDCE8F4,SnowShade=0xB4C8DC,IceBlue=0x8FC4E0,IceDeep=0x4E8FB8,Rock=0x1C2234,SeaDark=0x08202E,HullRed=0x9A2F26,HullBlack=0x1B1E26,House=0xE6E4DA,Warm=0xFFC88A,Funnel=0xD8A028;
        private static double H(int a,int b=0,int c=0)=>NativePolarLooks.Hash(a,b,c);

        /// <summary>The backdrop's sectors (null for another map): [near, landmark, far].</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(!NativePolarLooks.Active(map?.id))return null;
            var s=new NativeNewStageScenery.Sink{Map=map.id};NativeNewStageScenery.PlaySpace(map,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);s.B.Anime=true;
            Shelf(s);Ship(s);Bergs(s);Horizon(s);
            var o=new NativeSceneryMesh[Sectors];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);return o;
        }

        // ---------------- near: the shelf beyond the ridges, the sea ----------------
        private static void Shelf(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;double edge=NativePolarLooks.ShelfZ,sea=NativePolarLooks.Sea;
            // the sea out to the horizon (south of the shelf's edge; the play space's own strip is the kit's)
            s.Box(0,sea-.5,edge-5.2-500,2400,1,1000,SeaDark,skip:1);
            foreach(int side in new[]{-1,1})s.Box(side*(34.2+600),sea-.5,edge-2.6,1200,1,5.2,SeaDark,skip:1);
            // the shelf west, east and north of the map, and its ice cliff along the sea
            foreach(int side in new[]{-1,1}){s.Box(side*(33.02+500),-2,edge+500,1000,4,1000,SnowLit);s.Box(side*(33.02+500),-2.2,edge-.4,1000,3.6,.8,IceBlue);}
            s.Box(0,-2,27.02+400,66,4,800,SnowLit);
            // drifts shouldering the ridges from outside, and sastrugi out across the shelf
            for(int i=0;i<26;i++){double z=-22+i*2*1.0*1.9;foreach(int side in new[]{-1,1}){double h=3+H(11,i,side+2)*3.4;s.Box(side*(37.6+H(12,i,side+2)*1.6),h/2-.3,z,4.6,h,6,SnowShade,yaw:(H(13,i,side+2)-.5)*24,pitch:0,roll:side*-12);}}
            for(int i=0;i<10;i++){double x=-30+i*6.6;foreach(int side in new[]{-1,1}){if(Math.Abs(x)<9)continue;double h=3+H(14,i)*3;s.Box(x,h/2-.3,31.4+H(15,i),7,h,4.4,SnowShade,yaw:(H(16,i)-.5)*20,pitch:10);}}
            for(int i=0;i<360;i++)
            {
                double a=H(21,i)*2*Math.PI,r=48+Math.Pow(H(21,i,1),.8)*380,x=Math.Cos(a)*r,z=Math.Sin(a)*r;if(z<edge+4||Math.Abs(x)<40&&z<36)continue;
                double l=4+H(21,i,2)*14,h=.2+H(21,i,3)*(r<90?1.2:.7);s.Box(x,h/2,z,l,h,1.2+H(21,i,4)*2.2,H(21,i,5)<.5?SnowLit:SnowShade,yaw:24+(H(21,i,6)-.5)*24,roll:(H(21,i,7)-.5)*8);
            }
        }

        // ---------------- near: the icebreaker's after body ----------------
        private static void Ship(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;const double z0=27.04;
            // the hull on north out of the map (a little beam lost toward the stern), the boot-topping, the white strake
            s.Box(0,1.6,z0+19,12,3.2,38,HullRed);s.Box(0,.45,z0+19,12.06,.9,38.02,HullBlack);s.Box(0,2.66,z0+19,12.06,.18,38.02,House);
            s.Box(0,1.6,z0+40.5,10,3.2,5,HullRed,yaw:0);s.Box(0,1.6,z0+44,7,3.2,3,HullRed);
            // the house: three decks stepping back above the wheelhouse front, lit ports along each
            double[] top={12,15.2,18};double[] len={22,15,8};double[] wid={11.4,10,7};
            for(int k=0;k<3;k++)
            {
                double y0=k==0?3.2:top[k-1],zc=z0+len[k]/2+k*1.6;s.Box(0,(y0+top[k])/2,zc,wid[k],top[k]-y0,len[k],House,grime:.06);s.Box(0,top[k]+.08,zc,wid[k]+.5,.16,len[k]+.5,SnowLit);
                for(double u=1.4;u<len[k]-1;u+=2.2)foreach(int side in new[]{-1,1})if(H(31,k,(int)(u*10)+side)<.8)s.Box(side*(wid[k]/2+.03),top[k]-1.5,z0+k*1.6+u,.06,.7,1.1,Rock,glow:Warm,level:1.5);
            }
            // the wheelhouse's upper front (above the block's 12 m) and its windows looking south over the bow
            for(int i=0;i<5;i++)s.Box(-3.6+i*1.8,16.6,z0+1.57,1.3,1.0,.06,Rock,glow:Warm,level:1.7);
            // the funnel with its band, the mainmast and its yard, the radar's pedestal, boats in their davits
            s.Box(0,21.5,z0+13,4.2,7,5.2,Funnel);s.Box(0,23.4,z0+13,4.3,1.1,5.3,HullBlack);s.Box(0,25.2,z0+13,3.6,.5,4.6,HullBlack);
            NativeNewStageScenery.Octagon(s,0,18,34,z0+4.4,.32,House);s.Box(0,29.5,z0+4.4,7.5,.22,.22,House);s.Box(0,34.3,z0+4.4,.3,.3,.3,0xFF3020,glow:0xFF3020,level:2.6);
            foreach(int side in new[]{-1,1}){s.Box(side*3.75,29.75,z0+4.4,.16,.16,.16,side<0?0xFF3020:0x30FF60,glow:side<0?0xFF3020:0x30FF60,level:2.4);s.Box(side*6.3,13.4,z0+10,1.5,1.4,5.6,0xE8862A);s.Box(side*6.3,14.2,z0+10,1.56,.3,5.7,SnowLit);}
            // the snow drifted against her sides north of the map
            foreach(int side in new[]{-1,1})for(int i=0;i<5;i++)s.Box(side*(7.4+H(41,i,side+2)),1.1,z0+5.6+i*7,4.4,2.6+H(42,i,side+2)*1.4,7.4,SnowShade,roll:side*-22,yaw:(H(43,i,side+2)-.5)*14);
        }

        // ---------------- landmark: the bergs, the brash ice, the far station ----------------
        private static void Bergs(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Landmark;double sea=NativePolarLooks.Sea;
            (double x,double z,double w,double h)[] bergs={(-58,-96,34,26),(44,-128,46,34),(-150,-210,80,48),(120,-260,70,40),(8,-340,110,58),(-260,-150,60,30),(250,-140,54,28)};
            for(int i=0;i<bergs.Length;i++)
            {
                var b=bergs[i];s.Box(b.x,sea+b.h*.34,b.z,b.w,b.h*.7,b.w*.66,IceBlue,pitch:(H(51,i)-.5)*12,yaw:H(51,i,1)*180,roll:(H(51,i,2)-.5)*12);
                s.Box(b.x+b.w*.12,sea+b.h*.62,b.z-b.w*.05,b.w*.55,b.h*.76,b.w*.42,SnowLit,pitch:(H(51,i,3)-.5)*16,yaw:H(51,i,4)*180,roll:(H(51,i,5)-.5)*18);
                s.Box(b.x-b.w*.2,sea+b.h*.2,b.z+b.w*.1,b.w*.5,b.h*.4,b.w*.5,IceDeep,yaw:H(51,i,6)*180,roll:(H(51,i,7)-.5)*10);
            }
            // brash ice and small floes along the shelf's edge
            for(int i=0;i<120;i++)
            {
                double x=(H(52,i)-.5)*260,z=NativePolarLooks.ShelfZ-6-Math.Pow(H(52,i,1),1.6)*90;if(Math.Abs(x)<38&&z>-37)continue;
                double w=1+H(52,i,2)*5;s.Box(x,sea+.06,z,w,.3,w*(.6+H(52,i,3)*.5),H(52,i,4)<.6?SnowLit:IceBlue,yaw:H(52,i,5)*180);
            }
            // the far met tower and the fuel depot's lights across the shelf
            NativeNewStageScenery.Octagon(s,-118,0,30,84,.5,0xC8CCD4);s.Box(-118,30.5,84,.5,.5,.5,0xFF3020,glow:0xFF3020,level:2.6);
            for(int k=0;k<4;k++){s.Box(96+k*7,1.3,70,5.4,2.6,3.6,k%2==0?0xC84A2A:0x2F5F9E,grime:.08);s.Box(96+k*7,2.72,70,5.6,.2,3.8,SnowLit);s.Box(96+k*7,1.5,68.17,1.2,.7,.06,Rock,glow:Warm,level:1.5);}
        }

        // ---------------- far: the ice cap's rim and its nunataks ----------------
        private static void Horizon(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Far;
            for(int i=0;i<40;i++)
            {
                double a=-.35+H(61,i)*(Math.PI+.7),r=640+H(61,i,1)*260,x=Math.Cos(a)*r,z=Math.Sin(a)*r,h=40+H(61,i,2)*120,w=70+H(61,i,3)*140;
                s.Box(x,h/2-8,z,w,h,w*.7,Rock,pitch:(H(61,i,4)-.5)*30,yaw:H(61,i,5)*180,roll:(H(61,i,6)-.5)*30);
                s.Box(x,h*.64-8,z,w*.72,h*.36,w*.52,NativeNewStageScenery.Mix(Rock,SnowShade,.7),pitch:(H(61,i,4)-.5)*30,yaw:H(61,i,5)*180+10,roll:(H(61,i,6)-.5)*30);
            }
            // the ice cap's cliff where it meets the sea, east and west
            for(int i=0;i<18;i++){double x=-900+i*105;if(Math.Abs(x)<130)continue;s.Box(x,6,-90-H(62,i)*60+Math.Abs(x)*.25,110,16+H(62,i,1)*10,30,IceBlue,yaw:(H(62,i,2)-.5)*10);}
        }
    }
}
