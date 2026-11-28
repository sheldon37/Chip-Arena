using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Skyframe's backdrop (docs/arenas/SKYFRAME.md "The look") when the stage bank is absent (with it the modelled far chunks draw
    /// instead): box-built and deterministic, baked once into ChipArena/SourceLit sector meshes (Runtime NativeSkyframePresentation
    /// draws them on <see cref="NativeSceneryBuilder.BackdropLayer"/> without shadows):
    /// <list type="bullet">
    /// <item>Near: the tower's glazed body going down into the clouds under the garden, its floor bands and corner piers, the
    /// outriggers at its corners and the wind turbines' masts and nacelles on them (the rotors turn in the rig).</item>
    /// <item>Landmark: the other towers standing out of the cloud sea, some still under construction, their crown lights.</item>
    /// <item>Far: the cloud sea itself, its swells, and the great cloud banks on the horizon lit by the low sun.</item>
    /// </list>
    /// The keep-out (<see cref="NativeNewStageScenery.Enters"/>, thrown on while building): nothing stands inside the bounds grown
    /// by 1 m except what lies wholly under the map's lowest block base (the garden's body ends at y -7). Unity frame, metres;
    /// colours sRGB; allocation only while building.
    /// </summary>
    public static class NativeSkyframeScenery
    {
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public const int Glass=0x6FA8B8,GlassDeep=0x3E6E86,Band=0xE4D9C4,Pier=0xBFB3A0,Steel=0x4A4E55,White=0xF2F0EA,Cloud=0xFFE9D2,CloudShade=0xE8B8A0,CloudGold=0xFFD9A8,Lit=0xFFC27A;
        private static double H(int a,int b=0,int c=0)=>NativeSkyframeLooks.Hash(a,b,c);

        /// <summary>The backdrop's sectors (null for another map): [near, landmark, far].</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(!NativeSkyframeLooks.Active(map?.id))return null;
            var s=new NativeNewStageScenery.Sink{Map=map.id};NativeNewStageScenery.PlaySpace(map,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);s.B.Anime=true;
            Tower(s);Skyline(s);Sea(s);
            var o=new NativeSceneryMesh[Sectors];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);return o;
        }

        private static void Tower(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;double top=s.Under;
            // the body: glass between floor bands, piers at the corners, stepping out once on its way down
            for(int t=0;t<3;t++)
            {
                double w=52+t*5,d=32+t*5,h=t==2?70:16,y=top-h/2;s.Box(0,y,0,w-.6,h,d-.6,t%2==0?Glass:GlassDeep,glow:Lit,level:.08);
                for(double fy=top-.3;fy>top-h+1;fy-=4)s.Box(0,fy,0,w,.5,d,Band);
                foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})s.Box(sx*(w/2-.4),y,sz*(d/2-.4),1.4,h,1.4,Pier);
                for(int k=0;k<14;k++){double u=(k+.5)/14-.5;s.Box(u*w,y,d/2-.1,.22,h,.3,Steel);s.Box(u*w,y,-d/2+.1,.22,h,.3,Steel);}
                for(int k=0;k<9;k++){double u=(k+.5)/9-.5;s.Box(w/2-.1,y,u*d,.3,h,.22,Steel);s.Box(-w/2+.1,y,u*d,.3,h,.22,Steel);}
                top-=h;
            }
            // the turbines: an outrigger from each corner under the garden, the mast up past it, the nacelle (the rotor is the rig's)
            for(int i=0;i<NativeSkyframeLooks.Turbines;i++)
            {
                NativeSkyframeLooks.Turbine(i,out double x,out double y,out double z);double cx=Math.Sign(x)*26,cz=Math.Sign(z)*16,by=s.Under-2.5;
                double dx=x-cx,dz=z-cz,l=Math.Sqrt(dx*dx+dz*dz),yaw=Math.Atan2(dx,dz)*180/Math.PI;
                s.Box((x+cx)/2,by,(z+cz)/2,.9,1.2,l+1,Steel,yaw:yaw);
                s.Box(x,(y+by)/2,z,.8,y-by,.8,White);s.Box(x+.5,y,z,2.6,1.1,1.1,White);s.Box(x,by+.4,z,1.6,.5,1.6,Steel);
            }
        }
        private static void Skyline(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Landmark;
            for(int i=0;i<16;i++)
            {
                double a=(i+H(401,i)*.7)/16*2*Math.PI,r=240+H(401,i,1)*520,x=Math.Sin(a)*r,z=Math.Cos(a)*r,w=24+H(401,i,2)*30,d=20+H(401,i,3)*26,top=-18+H(401,i,4)*78,h=top-NativeSkyframeLooks.CloudY+30,yaw=H(401,i,5)*90;
                s.Box(x,top-h/2,z,w,h,d,i%3==0?GlassDeep:Glass,yaw:yaw,glow:Lit,level:.06);
                for(double fy=top-2;fy>top-h+4;fy-=6)s.Box(x,fy,z,w+.6,.8,d+.6,Band,yaw:yaw);
                if(i%4==1){s.Box(x,top+6,z,w*.8,12,1.2,0xB4553A,yaw:yaw);s.Box(x,top+6,z,1.2,12,d*.8,0xB4553A,yaw:yaw);s.Box(x,top+12,z,w,.8,d,0xB4553A,yaw:yaw);}
                else s.Box(x,top+2,z,w*.4,4,d*.4,Pier,yaw:yaw);
                s.Box(x,top+(i%4==1?13:4.6),z,.8,.8,.8,0xFF3A2A,glow:0xFF3A2A,level:2.4);
            }
        }
        private static void Sea(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Far;double y=NativeSkyframeLooks.CloudY;
            // the sea's floor under everything, its swells, and the banks on the horizon
            s.Box(0,y-10,0,3200,8,3200,CloudShade,glow:CloudShade,level:.5);
            for(int i=0;i<150;i++)
            {
                double a=H(409,i)*2*Math.PI,r=70+Math.Pow(H(409,i,1),.7)*1150,x=Math.Sin(a)*r,z=Math.Cos(a)*r,w=50+H(409,i,2)*150,h=6+H(409,i,3)*16,yaw=H(409,i,5)*180;
                s.Box(x,y-8+h/2,z,w,h,w*(.6+.3*H(409,i,4)),i%4==0?CloudGold:Cloud,yaw:yaw,glow:i%4==0?CloudGold:Cloud,level:.55);
                if(i%3==0)s.Box(x+w*.1,y-8+h+h*.3,z,w*.5,h*.7,w*.36,Cloud,yaw:yaw+20,glow:Cloud,level:.6);
            }
            for(int i=0;i<22;i++)
            {
                double a=(i+H(419,i)*.6)/22*2*Math.PI,r=950+H(419,i,1)*500,x=Math.Sin(a)*r,z=Math.Cos(a)*r,w=260+H(419,i,2)*260,h=60+H(419,i,3)*150;
                for(int k=0;k<4;k++){double f=1-k*.22;s.Box(x+(H(419,i,10+k)-.5)*w*.3,y-10+h*(k*.3+.15),z+(H(419,i,20+k)-.5)*w*.3,w*f,h*.34,w*f*.8,k%2==0?Cloud:CloudGold,yaw:H(419,i,5)*180+k*17,glow:k%2==0?Cloud:CloudGold,level:.6);}
            }
        }
    }
}
