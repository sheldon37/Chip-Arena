using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Breakwater's box-built backdrop (docs/arenas/BREAKWATER.md), baked once per map into ChipArena/SourceLit sectors: what
    /// stands round the mole when the stage bank is absent (with the bank the modelled sea, lighthouse, mole and town draw in
    /// its place; docs/arenas/STAGE-ART.md). Near: the faceted sea and the armour stones at the wall's foot. Landmark: the
    /// lighthouse on its rock past the east hall. Far: the mole running west to the shore with its lamps, the far pier head,
    /// the harbour town on its hill, the headlands.
    /// The keep-out (<see cref="NativeNewStageScenery.Enters"/>, thrown on while building): nothing stands inside the bounds
    /// grown by a metre unless it lies wholly under the quay's floor (the sea and the stones in it). Unity frame, metres;
    /// colours sRGB; allocation only while building.
    /// </summary>
    public static class NativeBreakwaterScenery
    {
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public const int Stone=0x6E7278,StoneDark=0x4A4E56,StoneWet=0x33383E,SeaDeep=0x12283C,SeaCrest=0x3E6682,LampHex=0xFFC27A,Lime=0xE4E0D4,Band=0xA83A30,Copper=0x4F8A72,Iron=0x23262C,Slate=0x39414C;
        private static double H(int a,int b=0,int c=0)=>NativeBreakwaterLooks.Hash(a,b,c);

        /// <summary>The backdrop's sectors (null for another map): [near, landmark, far].</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(!NativeBreakwaterLooks.Active(map?.id))return null;
            var s=new NativeNewStageScenery.Sink{Map=map.id};NativeNewStageScenery.PlaySpace(map,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);s.B.Anime=true;
            // the sea lies inside the bounds (they run 4 m past both open edges): what may stand there lies under the quay's floor
            s.Under=NativeBreakwaterLooks.QuayY-.1;
            Sea(s);Lighthouse(s);Shore(s);
            var o=new NativeSceneryMesh[Sectors];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);return o;
        }
        private static void Sea(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;double sea=NativeBreakwaterLooks.SeaY;
            NativeNewStageScenery.Sea(s,11,sea,-760,760,-760,760,3,6,SeaDeep,SeaCrest,8,new[]{-22.0,22.0,NativeBreakwaterLooks.QuayZ,NativeBreakwaterLooks.WallZ});
            // armour stones heaped against the wave wall's foot (awash) and round the lighthouse's rock
            for(int i=0;i<44;i++)
            {
                double x=-23+i*1.07+(H(21,i)-.5)*.6,z=NativeBreakwaterLooks.WallZ+.9+H(21,i,1)*2.2,sz=.9+H(21,i,2)*.9;
                s.Box(x,sea-.15+H(21,i,3)*.3-sz*.2,z,sz*1.3,sz*.8,sz,i%3==0?StoneWet:StoneDark,pitch:(H(21,i,4)-.5)*30,yaw:H(21,i,5)*90,roll:(H(21,i,6)-.5)*30);
            }
        }
        private static void Lighthouse(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Landmark;double x=NativeBreakwaterLooks.LightX,z=NativeBreakwaterLooks.LightZ,sea=NativeBreakwaterLooks.SeaY,foot=NativeBreakwaterLooks.LightFoot,gal=NativeBreakwaterLooks.GalleryY;
            // the rock and the round plinth, the neck from the hall
            NativeNewStageScenery.Octagon(s,x,sea-3,foot-.6,z,5.6,StoneDark);NativeNewStageScenery.Octagon(s,x,foot-.6,foot,z,5.0,Stone);
            s.Box(24.8,-1.6,z,3.4,3.2+foot,6,Stone,grime:.2);
            for(int i=0;i<18;i++){double a=i*2*Math.PI/18,r=6.6+H(23,i)*1.6,sz=1.2+H(23,i,1)*1.4;if(x+Math.Cos(a)*r-sz<24)continue;s.Box(x+Math.Cos(a)*r,sea-.2,z+Math.Sin(a)*r,sz*1.4,sz,sz*1.1,i%2==0?StoneWet:StoneDark,pitch:(H(23,i,2)-.5)*40,yaw:H(23,i,3)*90);}
            // the tower: tapering lime-washed courses with two red bands, lit windows up its west face
            int courses=16;double y=foot;
            for(int c=0;c<courses;c++){double y1=y+(gal-foot)/courses,r=4.1-c*.085;NativeNewStageScenery.Round(s,x,y,y1,z,r,c==5||c==6||c==11||c==12?Band:Lime,.92+.08*H(25,c));y=y1;}
            foreach(double wy in new[]{5.2,10.4,15.6,19.6})s.Box(x-(4.0-(wy-foot)/(gal-foot)*1.35)+.02,wy,z,.1,.9,.5,0x2A3440,glow:LampHex,level:.8);
            // the gallery and its rail, the lantern's glazing and frame, the copper dome and its vane
            NativeNewStageScenery.Octagon(s,x,gal,gal+.35,z,3.7,Iron);
            for(int k=0;k<16;k++){double a=k*Math.PI/8;s.Box(x+Math.Sin(a)*3.55,gal+.9,z+Math.Cos(a)*3.55,.07,1.1,.07,Iron);}
            NativeNewStageScenery.Octagon(s,x,gal+1.4,gal+1.48,z,3.55,Iron);
            NativeNewStageScenery.Octagon(s,x,gal+.35,gal+1.0,z,2.2,Iron);NativeNewStageScenery.Octagon(s,x,gal+1.0,gal+3.5,z,2.0,0x1E2C3C,1,0xFFE8B0,.08);
            for(int k=0;k<8;k++){double a=k*Math.PI/4+Math.PI/8;s.Box(x+Math.Sin(a)*2.08,gal+2.25,z+Math.Cos(a)*2.08,.09,2.5,.09,Iron);}
            NativeNewStageScenery.Octagon(s,x,gal+3.5,gal+3.8,z,2.3,Copper);NativeNewStageScenery.Octagon(s,x,gal+3.8,gal+4.5,z,1.7,Copper);NativeNewStageScenery.Octagon(s,x,gal+4.5,gal+5.0,z,.9,Copper);s.Box(x,gal+5.7,z,.12,1.4,.12,Iron);
        }
        private static void Shore(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Far;
            // the mole running west to the shore: its body, the wave wall along its north side, the quay along its south, lamps
            s.Box(-112,-3.5,0,176,7,10,Stone,grime:.15);s.Box(-112,-2.3,6.7,176,9.4,3.4,StoneDark,grime:.2);s.Box(-112,-4.7,-7.25,176,4.6,4.5,StoneDark);
            for(double x=-34;x>=-170;x-=17){s.Box(x,1.9,-4.6,.18,3.8,.18,Iron);s.Box(x,3.9,-4.2,.16,.12,.9,Iron);s.Box(x,3.6,-3.7,.28,.34,.28,LampHex,glow:LampHex,level:2.2);}
            // the far pier head across the harbour mouth and its green light
            s.Box(-110,-3.2,-118,110,6,9,StoneDark);NativeNewStageScenery.Round(s,-64,-.2,6.4,-118,1.5,Lime);s.Box(-64,7.0,-118,.9,1.0,.9,0x5AFFA0,glow:0x5AFFA0,level:1.6);
            // the town on its hill: tiers of houses with lit windows, the church, the headlands north and south
            for(int i=0;i<52;i++)
            {
                double a=H(31,i),x=-200-H(31,i,1)*170,z=-220+a*400,rise=(-x-200)*.16,w=7+H(31,i,2)*9,d=7+H(31,i,3)*8,h=6+H(31,i,4)*9;
                NativeNewStageScenery.Building(s,300+i,x,z,w,d,-3+rise,rise+h,NativeNewStageScenery.Mix(0x39414F,0x51484A,H(31,i,5)),0x1E2534,LampHex,.6,1.1,3.0);
                s.Box(x,rise+h+.8,z,w*1.04,1.6,d*.6,Slate);
            }
            s.Box(-270,22,-40,7,34,7,0x4A4E58);s.Box(-270,43,-40,4,10,4,Slate,yaw:45);
            for(int i=0;i<9;i++){double z=-420+i*105;s.Box(-470-H(33,i)*80,6+H(33,i,1)*22,z,240,60+H(33,i,1)*40,150,0x16202C,yaw:15+H(33,i,2)*30);}
            for(int i=0;i<7;i++){double x=-420+i*150;s.Box(x,4+H(34,i)*14,-560-H(34,i,1)*60,220,40+H(34,i,1)*30,90,0x141C28,yaw:H(34,i,2)*24);}
        }
    }
}
