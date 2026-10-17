using System;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The roller line's axis (z), the rollers' pitch and the stands' centres (x).</summary>
        public const float MillZ=-25f,RollerPitch=.75f;
        public static readonly float[] StandX={15f,8f,1.5f,-10f};

        private void Mill()
        {
            var steel=Iron(IronHex);var dark=Iron(IronDarkHex);var green=Iron(GreenHex);var greenD=Iron(GreenDarkHex);var roll=Iron(0x5A5652);var rollD=Detail(Iron(0x3A3836));
            // ---- the roller tables: rollers across the line (octagonal), their bearings, the side guards ----
            foreach(var stem in new[]{"roller-table-0","roller-table-1","roller-table-2"})
            {
                var b=B(stem);if(b==null)continue;Under(stem);double top=Y1(b),z0=Z0(b),z1=Z1(b);
                for(double x=X0(b)+.35;x<X1(b)-.2;x+=RollerPitch)
                {
                    RodZ(z0+.12,z1-.12,x,top+.02,.11,roll);Span(x-.12,x+.12,top-.25,top+.05,z0-.04,z0+.12,rollD);Span(x-.12,x+.12,top-.25,top+.05,z1-.12,z1+.04,rollD);
                }
                Span(X0(b),X1(b),top-.3,top+.14,z0-.08,z0-.02,greenD);Span(X0(b),X1(b),top-.3,top+.14,z1+.02,z1+.08,greenD);
                for(double x=X0(b)+1;x<X1(b);x+=2)foreach(double z in new[]{z0-.1,z1+.1})Span(x-.04,x+.04,.1,top-.3,z-.03,z+.03,Detail(steel));
            }
            // ---- the stands: housing posts' machined faces, screwdown motors on the lintels, the top work roll, the stand number ----
            for(int s=0;s<4;s++)
            {
                double x=StandX[s];string st="mill-stand-"+s;var n=B(st+"-n");var so=B(st+"-s");var li=B(st+"-lintel");if(n==null||so==null||li==null)continue;Under(st+"-lintel");
                double top=Y1(li);Span(x-.9,x+.9,top,top+.85,-26.1,-23.9,green);Span(x-.6,x+.6,top+.85,top+1.45,-25.6,-24.4,Iron(0x3A4A40));RodZ(-25.7,-24.3,x,top+1.62,.24,greenD);
                Span(x-1.25,x+1.25,top,top+.12,-26.25,-23.75,dark);foreach(double z in new[]{-25.5,-24.5})Span(x-.12,x+.12,top+.85,top+1.2,z-.12,z+.12,Detail(Iron(SteelLightHex)));
                RodZ(-26.15,-23.85,x,Y0(li)-.11,.13,Iron(0x6E6A64));Span(x-1.4,x+1.4,Y0(li)-.03,Y0(li),-26.2,-23.8,Detail(Iron(SootHex)));
                Under(st+"-n");foreach(double fx in new[]{X0(n)-.04,X1(n)})Span(fx,fx+.04,0,Y1(n),Z0(n)+.2,Z1(n)-.2,Iron(0x3E4E44));
                for(double y=.6;y<4.2;y+=.9){Span(X0(n)+.15,X0(n)+.25,y,y+.1,Z1(n),Z1(n)+.04,Detail(Iron(SteelLightHex)));Span(X1(n)-.25,X1(n)-.15,y,y+.1,Z1(n),Z1(n)+.04,Detail(Iron(SteelLightHex)));}
                Text("STAND "+(4-s),x,3.0,Z1(n),2,.06,.005,.012,P(NativeBlockLooks.Plain,0xD8D0B8,RoleSign));
                // The rolls' chocks and necks standing out of both housings' outer faces (bright machined necks: the stand reads as a
                // machine, not a block).
                foreach(double y in new[]{.95,1.75})
                {
                    Span(x-.42,x+.42,y-.24,y+.24,Z1(n),Z1(n)+.12,Iron(0x2A302C));Span(x-.13,x+.13,y-.13,y+.13,Z1(n),Z1(n)+.24,Iron(SteelLightHex));
                    Span(x-.42,x+.42,y-.24,y+.24,Z0(so)-.12,Z0(so),Iron(0x2A302C));Span(x-.13,x+.13,y-.13,y+.13,Z0(so)-.24,Z0(so),Iron(SteelLightHex));
                }
                Under(st+"-s");foreach(double fx in new[]{X0(so)-.04,X1(so)})Span(fx,fx+.04,0,Y1(so),Z0(so)+.2,Z1(so)-.2,Iron(0x3E4E44));
                // The water sprays' headers either side of the pass line (they hiss when the slab goes through: effects).
                Under(st+"-lintel");foreach(double dx in new[]{-1.45,1.45})RodZ(-26.1,-23.9,x+dx,2.35,.05,Detail(Iron(GreenDarkHex)));
            }
            // The drive's spindles from the motor to stand 3 and stand 2 (on top of the drive block, within its band).
            {var b=B("mill-drive");if(b!=null){Under("mill-drive");RodX(X0(b),X1(b),Y1(b)+.12,-22.4,.13,Iron(SteelLightHex));Span(X0(b)+.4,X0(b)+1.6,Y1(b),Y1(b)+.2,Z0(b)+.2,Z1(b)-.2,green);RingX(X0(b)+2.0,X0(b)+2.6,Y1(b)+.0,-22.4,.24,.08,8,Detail(dark));}}
            // ---- the reheat furnace: the discharge door's frame and fire, the side peepholes, buckstays, the stack ----
            {
                var b=B("reheat-furnace");if(b!=null)
                {
                    Under("reheat-furnace");double fx=X0(b);short hz=NativeIronworksHazards.Slab;
                    Span(fx-.22,fx,0,3.0,-26.75,-26.4,dark);Span(fx-.22,fx,0,3.0,-23.6,-23.25,dark);Span(fx-.24,fx,2.3,3.0,-26.4,-23.6,dark);Span(fx-.2,fx,.9,.98,-26.4,-23.6,Iron(SteelLightHex));
                    Melt(fx-.025,1.62,-25,.03,1.36,2.75,MoltenFire,1,1,.6f,.92f,.62f,0f,MDriveBillet,hz,FrameStatic,1.3f);
                    Span(fx-.18,fx-.02,3.0,4.35,-26.6,-23.4,Iron(0x3A3632));Span(fx-.2,fx,2.98,3.04,-26.55,-23.45,Glow(SeamHex,1.4f,RoleMolten,DriveHazardGlow,.35f,hz));
                    for(double z=-28.6;z<-20.2;z+=.9)if(z<-26.9||z>-23.1)Span(fx-.2,fx,0,8.8,z-.11,z+.11,Detail(steel));
                    for(double x=X0(b)+.6;x<X1(b)-.3;x+=1.1){Span(x-.12,x+.12,1.3,1.55,Z1(b),Z1(b)+.05,Glow(FurnaceHex,1.5f,RoleMolten,DriveFire,(float)x));Span(x-.2,x+.2,1.2,1.65,Z1(b)+.05,Z1(b)+.08,Detail(dark));}
                    Span(X0(b)-.02,X1(b),Y1(b),Y1(b)+.3,Z0(b)-.02,Z1(b)+.02,dark);RodY(Y1(b),17.6,33,-27,.62,Iron(0x2E2A26));for(double y=10;y<17.5;y+=1.6)RodY(y,y+.12,33,-27,.68,Detail(dark));
                    Text("REHEAT",-25,4.9,fx,1,.13,.005,.02,P(NativeBlockLooks.Plain,0xC8C0AC,RoleSign));
                    Light(fx-.8,1.8,-25,0xFF5A18,1.9,13,GlowReheat,-1,hz,1.5,.9);Light(fx-.6,1.6,-25,0xFF5A18,.7,7,GlowReheat,0,-1,1);
                }
            }
            // ---- the hot slab (its own frame: it runs west along the table from the reheat door while live) ----
            {
                Under("roller-table-1");short hz=NativeIronworksHazards.Slab;
                Melt(FrameOrigin[FrameSlab*3],FrameOrigin[FrameSlab*3+1],FrameOrigin[FrameSlab*3+2],6.0,.44,1.5,MoltenSlab,0,-1,0,.96f,.5f,.15f,MDriveSlab,hz,FrameSlab);
                LightFrame(0,.9,0,0xFF6A1C,1.5,9,GlowSlab,hz,FrameSlab);
            }
            // ---- the flying shear: the blade in its own frame, the cylinders on the lintel, its warning lamps ----
            {
                var li=B("shear-housing-lintel");if(li!=null)
                {
                    Under("shear-housing-lintel");short hz=NativeIronworksHazards.Shear;var blade=With(Iron(0x4A4A4C,RoleHazard),frame:FrameShear);
                    Span(-15.62,-15.38,2.3,3.4,-25.95,-24.05,blade);Span(-15.6,-15.4,2.26,2.32,-25.9,-24.1,With(Iron(0xB8B8B8,RoleHazard),frame:FrameShear));
                    foreach(double z in new[]{-25.6,-24.4}){RodY(Y1(li),Y1(li)+1.3,-15.5,z,.16,Iron(GreenDarkHex));RodY(Y1(li)+1.3,Y1(li)+1.5,-15.5,z,.2,Detail(dark));}
                    foreach(double z in new[]{-26.15,-23.85})Span(-15.65,-15.35,2.6,2.8,z-.05,z+.05,Glow(AmberHex,2.6f,RoleLamp,DriveWarnLamp,(float)z,hz));
                    Text("SHEAR",-15.5,3.2,-21,2,.06,.005,.012,P(NativeBlockLooks.Plain,0xD8B040,RoleSign));
                }
            }
            // ---- the cooling bed: rake bars and bars cooling from red to black ----
            {
                var b=B("cooling-bed");if(b!=null)
                {
                    Under("cooling-bed");double top=Y1(b);for(double x=X0(b)+.25;x<X1(b);x+=.5)Span(x-.04,x+.04,top,top+.07,Z0(b)+.1,Z1(b)-.1,Iron(0x3A3836));
                    for(int i=0;i<6;i++){double z=Z0(b)+.4+i*.62;float heat=(float)Math.Max(.03,.42-.08*i);MeltSpan(X0(b)+.3,X1(b)-.3,top+.07,top+.17,z-.14,z+.14,MoltenSlab,0,1,0,heat,heat*.8f,.3f,MDriveSteady,-1,FrameStatic,0,1,i>3);}
                    Light(-19.5,1.2,-31.6,0xFF4A10,.45,6,GlowSlab,0,-1,1);
                }
            }
            // ---- the pulpit: its lit window on the line, the screens behind it, the roof ----
            {
                var b=B("pulpit");if(b!=null)
                {
                    Under("pulpit");double z=Z1(b);Span(X0(b)+.3,X1(b)-.3,1.5,2.6,z,z+.03,P(NativeBlockLooks.WindowLit,0x6E8F96,RoleProp));
                    Span(X0(b)+.6,X0(b)+1.5,1.6,2.1,z+.03,z+.05,Glow(0x7AE0B0,.8f,RoleProp,DriveFlicker,.2f));Span(X1(b)-1.5,X1(b)-.6,1.6,2.1,z+.03,z+.05,Glow(AmberHex,.35f,RoleProp));
                    Span(X0(b)-.15,X1(b)+.15,Y1(b),Y1(b)+.12,Z0(b)-.15,Z1(b)+.25,dark);Span(X0(b)+.2,X1(b)-.2,1.45,1.5,z,z+.05,Detail(Iron(SteelHex)));
                    Light((X0(b)+X1(b))/2,2.2,z+.8,0xFFC266,.35,6,GlowWindow,-1,-1,1);
                }
            }
            // ---- coils (end eyes and straps on the stacks' faces) and billets on their rack ----
            foreach(var stem in new[]{"coil-stack-0","coil-stack-1"})
            {
                var b=B(stem);if(b==null)continue;Under(stem);
                for(int i=0;i<3;i++){double x=X0(b)+.5+i*1.0;Span(x-.48,x+.48,Y1(b),Y1(b)+.04,Z0(b)+.1,Z1(b)-.1,Detail(Iron(0x8A8070)));Span(x-.03,x+.03,0,Y1(b)+.05,Z1(b),Z1(b)+.03,Detail(Iron(0x2A2622)));Span(x-.03,x+.03,0,Y1(b)+.05,Z0(b)-.03,Z0(b),Detail(Iron(0x2A2622)));}
                foreach(double fx in new[]{X0(b)-.02,X1(b)})Span(fx,fx+.02,.2,Y1(b)-.2,b.pos.z-.35,b.pos.z+.35,Iron(0x161412));
            }
            {var b=B("billet-rack");if(b!=null){Under("billet-rack");for(int i=0;i<6;i++){double z=Z0(b)+.12+i*.19;RodX(X0(b)+.1,X1(b)-.1,Y1(b)+.08,z,.08,Iron(i%2==0?0x5A564E:0x4A463E));}}}
            // ---- the doors in the south shell, the breach's bricked-up opening, scale and oil on the floor ----
            foreach(double x in new[]{32.5,-12.5}){short s=SpawnAt(x,0,-31.6);Own(s);Under("shell-s");Begin();Doorway(x,0,-34,2.6,Iron(IronDarkHex),0,1);End();Own(-1);}
            Breach("breach-mill-s");
            Under("slab");for(int i=0;i<14;i++){double x=-20+Hash(44,i,1)*48,z=-27.6-Hash(44,i,2)*3+(Hash(44,i,3)<.5?6:0);Box(x,.006,z,.6+Hash(44,i,4)*1.4,.012,.4+Hash(44,i,5)*.8,Detail(Paint(0x15120E)),Yaw(Hash(44,i,6)*180));}
            Under("wall-mill-2");Text("ROLLING MILL",14,3.2,-14.3,3,.13,.005,.02,P(NativeBlockLooks.Plain,0xC8C0AC,RoleSign));
        }
        /// <summary>A bricked-up opening a Hulk can break (its parts hide with its block): a steel lintel over it, the infill's
        /// mortar lines and a stencilled X.</summary>
        private void Breach(string stem)
        {
            var b=B(stem);if(b==null)return;Under(stem);Own(SpawnAt(b.pos.x,0,b.pos.z,3.2));Begin();bool alongX=b.size.x>b.size.z;var lintel=Iron(IronDarkHex,RoleBreach);var paint=Detail(P(NativeBlockLooks.Plain,0x9A9488,RoleBreach));
            if(alongX)
            {
                bool north=b.pos.z<0;double face=north?Z1(b):Z0(b),o=north?.03:-.03;
                Span(X0(b)-.3,X1(b)+.3,Y1(b),Y1(b)+.3,Math.Min(face,face+o*4),Math.Max(face,face+o*4),lintel);
                Member(X0(b)+.4,.4,face+o/2,X1(b)-.4,Y1(b)-.4,face+o/2,.08,.02,paint);Member(X0(b)+.4,Y1(b)-.4,face+o/2,X1(b)-.4,.4,face+o/2,.08,.02,paint);
            }
            else
            {
                bool east=b.pos.x<0;double face=east?X1(b):X0(b),o=east?.03:-.03;
                Span(Math.Min(face,face+o*4),Math.Max(face,face+o*4),Y1(b),Y1(b)+.3,Z0(b)-.3,Z1(b)+.3,lintel);
                Member(face+o/2,.4,Z0(b)+.4,face+o/2,Y1(b)-.4,Z1(b)-.4,.02,.08,paint);Member(face+o/2,Y1(b)-.4,Z0(b)+.4,face+o/2,.4,Z1(b)-.4,.02,.08,paint);
            }
            End();Own(-1);
        }
    }
}
