using System;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The pour: where the stream leaves the ladle's lip at full tilt and where it lands (in a mold of row 1).</summary>
        public const float LipX=7.0f,LipY=8.0f,PourZ=-7.35f,LandX=7.8f,LandY=.95f;
        public const int StreamSegments=9;

        private void Casting()
        {
            var steel=Iron(IronHex);var dark=Iron(IronDarkHex);var steelD=Detail(steel);
            // ---- the torpedo track: rails and sleepers along the floor (flat) ----
            Under("slab");var rail=Iron(SteelLightHex,RoleFloor);foreach(double z in new[]{1.75,3.25})Span(-23.6,35.6,0,.05,z-.04,z+.04,rail);
            for(double x=-23.2;x<35.5;x+=1.2)Span(x-.12,x+.12,0,.03,1.35,3.65,Detail(Iron(0x2A2622,RoleFloor)));
            // ---- the torpedo cars: bogies, wheels, hoops, the refractory mouth with its metal ----
            for(int t=0;t<2;t++)
            {
                var b=B("torpedo-"+(t+1));if(b==null)continue;Under("torpedo-"+(t+1));double x0=X0(b),x1=X1(b),cx=b.pos.x,z0=Z0(b),z1=Z1(b),top=Y1(b);
                foreach(double bx in new[]{x0+1.15,x1-1.15})
                {
                    Span(bx-.85,bx+.85,0,.78,z0-.12,z1+.12,dark);
                    foreach(double wx in new[]{bx-.5,bx+.5}){RodZ(z0-.22,z0-.08,wx,.4,.36,Iron(SootHex));RodZ(z1+.08,z1+.22,wx,.4,.36,Iron(SootHex));}
                }
                foreach(double hx in new[]{x0+2.4,x0+4.0,cx,x1-4.0,x1-2.4}){Span(hx-.09,hx+.09,.85,top+.03,z0-.05,z0,steel);Span(hx-.09,hx+.09,.85,top+.03,z1,z1+.05,steel);Span(hx-.09,hx+.09,top,top+.05,z0,z1,steel);}
                Span(x0-.2,x0,.5,.9,b.pos.z-.25,b.pos.z+.25,Iron(YellowDarkHex));Span(x1,x1+.2,.5,.9,b.pos.z-.25,b.pos.z+.25,Iron(YellowDarkHex));
                Span(cx-1.0,cx+1.0,top,top+.26,b.pos.z-.8,b.pos.z+.8,Brick(RefractoryDarkHex,RoleProp));Span(cx-1.05,cx+1.05,top+.24,top+.3,b.pos.z-.85,b.pos.z+.85,dark);
                MeltSpan(cx-.78,cx+.78,top+.22,top+.27,b.pos.z-.58,b.pos.z+.58,MoltenFlow,0,1,.12f,.6f,.52f,.62f,MDriveSteady,-1,FrameStatic,0,.9f);
                Text((t+1).ToString(),cx,1.4,z0,3,.12,.0,.012,P(NativeBlockLooks.Plain,0x8A8478,RoleSign));
                Light(cx,top+1.0,b.pos.z,0xFF5A18,.7,8,GlowTorpedo,0,-1,1.2,t*.7);
            }
            // ---- spare ladles on their stands: banded shells, rims, a skull of crusted metal in each ----
            foreach(var stem in new[]{"ladle-stand-w","ladle-stand-e"})
            {
                var b=B(stem);if(b==null)continue;Under(stem);double cx=b.pos.x,cz=b.pos.z,y0=Y1(b);
                Ring(cx,y0,y0+2.3,cz,1.6,.16,10,Iron(0x2E2A26,RoleProp));Ring(cx,y0+.5,y0+.66,cz,1.66,.08,10,Detail(dark));Ring(cx,y0+1.7,y0+1.86,cz,1.66,.08,10,Detail(dark));Ring(cx,y0+2.2,y0+2.36,cz,1.7,.2,10,dark);
                MeltSpan(cx-1.2,cx+1.2,y0+2.12,y0+2.18,cz-1.2,cz+1.2,MoltenSlag,0,1,.02f,.22f,.16f,.9f,MDriveSteady,-1,FrameStatic,0,.8f);
                Span(cx-.22,cx+.22,y0+1.25,y0+1.65,cz-1.9,cz+1.9,Iron(YellowDarkHex,RoleProp));
                foreach(double sx in new[]{-1.0,1.0})foreach(double sz in new[]{-1.0,1.0})Span(cx+sx*1.65-.12,cx+sx*1.65+.12,0,Y1(b),cz+sz*1.65-.12,cz+sz*1.65+.12,steelD);
            }
            Ladle();Molds();
            // ---- the pig bed: rows of pig molds, the poured pigs cooling red to black ----
            {
                var b=B("pig-bed");if(b!=null)
                {
                    Under("pig-bed");double top=Y1(b);
                    for(int r=0;r<5;r++)for(int i=0;i<6;i++)
                    {
                        double x=X0(b)+.35+i*.92,z=Z0(b)+.35+r*.78;Span(x-.04,x+.84,top,top+.05,z-.03,z+.6,Detail(Iron(0x2A2724)));
                        float heat=(float)(r<2?.62-.06*i+.04*Hash(r,i,8):r<4?.36-.04*i:.12);MeltSpan(x+.04,x+.8,top+.02,top+.07,z+.02,z+.55,MoltenSlab,0,1,0,heat,heat*.85f,.25f,MDriveSteady,-1,FrameStatic,0,1,i>3);
                    }
                    Light(25,1.3,9.6,0xFF4A10,.6,8,GlowPig,0,-1,1.6);
                }
            }
            // ---- skulls (solidified iron) on their pile, scrap in the boxes ----
            {var b=B("skull-pile");if(b!=null){Under("skull-pile");for(int i=0;i<9;i++){double x=X0(b)+.4+Hash(9,i,1)*(b.size.x-.8),z=Z0(b)+.3+Hash(9,i,2)*(b.size.z-.6);Box(x,Y1(b)+.08,z,.5+Hash(9,i,3)*.5,.18,.4+Hash(9,i,4)*.4,Iron(0x1E1B19,RoleProp),Yaw(Hash(9,i,5)*180));}}}
            foreach(var stem in new[]{"scrap-box-w","scrap-box-e"}){var b=B(stem);if(b==null)continue;Under(stem);Scrap(X0(b)+.1,X1(b)-.1,Y1(b),Z0(b)+.1,Z1(b)-.1,stem[stem.Length-1]);}
            // ---- the floor: plates round the pour, worn hazard paint, walkway lines, cooled spills ----
            Under("slab");var plate=Detail(Iron(0x2E2B28,RoleFloor));
            for(double x=-1.5;x<11.5;x+=2.2)for(double z=-9.4;z<-2.4;z+=2.2)Span(x+.02,x+2.18,0,.012,z+.02,z+2.18,plate);
            var chev=Paint(0x9C7A2A);var black=Paint(0x15130F);
            Span(-1.8,11.6,0,.012,-9.75,-9.55,black);Span(-1.8,11.6,0,.012,-2.25,-2.05,black);for(double x=-1.8;x<11.6;x+=.9){Span(x,x+.45,0,.014,-9.75,-9.55,chev);Span(x,x+.45,0,.014,-2.25,-2.05,chev);}
            foreach(double z in new[]{-11.0,13.4})Span(-19,30,0,.01,z-.06,z+.06,Detail(Paint(0x8A7430)));
            for(int i=0;i<16;i++){double x=-18+Hash(31,i,1)*46,z=-11+Hash(31,i,2)*22;if(Math.Abs(z-2.5)<1.6)continue;Box(x,.006,z,.8+Hash(31,i,3)*1.8,.012,.5+Hash(31,i,4)*1.2,Detail(Paint(0x141210)),Yaw(Hash(31,i,5)*180));}
            for(int i=0;i<10;i++){double x=3+Hash(32,i,1)*9,z=-9+Hash(32,i,2)*6.5;Box(x,.008,z,.25+Hash(32,i,3)*.5,.016,.2+Hash(32,i,4)*.4,Detail(Iron(0x24201C,RoleFloor)),Yaw(Hash(32,i,5)*180));}
            // ---- signs: CAST HOUSE over the time office's opening (west face), MOLTEN METAL and CRANE OVERHEAD on the mill wall ----
            Under("wall-e-3");Text("CAST HOUSE",0,8.6,35.7,1,.16,.005,.025,Glow(0xE8DCC0,.55f,RoleSign));
            Under("wall-mill-2");var sign=P(NativeBlockLooks.Plain,0xD8B040,RoleSign);Span(9.4,16.6,2.05,3.05,-13.7,-13.68,Iron(0x1C1A18,RoleSign));Text("MOLTEN METAL",13,2.25,-13.7,2,.11,.02,.012,sign);
            Under("wall-mill-0");Span(-16.6,-7.4,2.05,3.05,-13.7,-13.68,Iron(0x1C1A18,RoleSign));Text("CRANE OVERHEAD",-12,2.25,-13.7,2,.1,.02,.012,sign);
        }
        // The crane's ladle (its own frame, tilting about its trunnions to pour toward +x) and the pour's stream.
        private void Ladle()
        {
            Under("crane-trolley");byte f=FrameLadle;double cx=FrameOrigin[f*3],cy=FrameOrigin[f*3+1],cz=FrameOrigin[f*3+2];short hz=NativeIronworksHazards.Ladle;
            var shell=With(Iron(0x2E2A26,RoleHazard),frame:f);var band=With(Iron(IronDarkHex,RoleHazard),frame:f);var yel=With(Iron(YellowHex,RoleHazard),frame:f);
            Ring(cx,cy-1.5,cy+.95,cz,1.25,.14,10,shell);Span(cx-1.1,cx+1.1,cy-1.66,cy-1.48,cz-1.1,cz+1.1,band);
            Ring(cx,cy-1.0,cy-.86,cz,1.31,.08,10,band);Ring(cx,cy+.5,cy+.64,cz,1.31,.08,10,band);Ring(cx,cy+.9,cy+1.03,cz,1.33,.2,10,band);
            Span(cx+1.1,cx+1.52,cy+.82,cy+1.02,cz-.32,cz+.32,band);Span(cx+1.12,cx+1.5,cy+.78,cy+.84,cz-.24,cz+.24,shell);
            Span(cx-.2,cx+.2,cy-.2,cy+.2,cz-1.5,cz-1.26,yel);Span(cx-.2,cx+.2,cy-.2,cy+.2,cz+1.26,cz+1.5,yel);
            Melt(cx,cy+.62,cz,1.9,.05,1.9,MoltenFlow,0,1,.35f,.74f,.6f,.62f,MDriveLadle,hz,f);
            // Skull drips frozen down the outside, the lip's crusted spill.
            for(int i=0;i<5;i++){double a=-.6+i*.3;Box(cx+Math.Cos(a)*1.27,cy+.4-Hash(5,i,1)*.9,cz+Math.Sin(a)*1.27,.08,.4+Hash(5,i,2)*.5,.05,With(Iron(0x1C1916,RoleHazard),frame:f,detail:true),Yaw(-a*180/Math.PI+90));}
            // The warning beacons and the klaxon on the trolley (the hazard kit's amber, the ladle's warning).
            Under("crane-trolley");foreach(double z in new[]{-7.3,-4.7}){Span(3.7,3.9,11.75,12.0,z-.1,z+.1,Glow(AmberHex,3.2f,RoleLamp,DriveBeacon,(float)(z*.1),hz));Span(3.68,3.92,11.7,11.76,z-.12,z+.12,Iron(SootHex,RoleLamp));}
            Span(7.4,7.75,11.75,12.15,-6.2,-5.8,Iron(0x3A3A3A,RoleLamp));Light(3.8,11.6,-6.0,AmberHex,1.4,12,GlowBeacon,-1,hz,.5);
            // The stream from the lip at full tilt to the mold (static segments: the pour shows them while it runs).
            for(int i=0;i<StreamSegments;i++)
            {
                double t0=i/(double)StreamSegments,t1=(i+1)/(double)StreamSegments;
                double xa=LipX+(LandX-LipX)*Math.Sqrt(t0),ya=LipY+(LandY-LipY)*t0,xb=LipX+(LandX-LipX)*Math.Sqrt(t1),yb=LipY+(LandY-LipY)*t1;
                double w=.2-.07*t0;Under("mold-row-1");var q=Along(xb-xa,yb-ya,0);double len=Math.Sqrt((xb-xa)*(xb-xa)+(yb-ya)*(yb-ya))+.04;
                // The stream piece: its local z runs along the fall (Along), so its flow axis is z, downward along it.
                if(Blocked((xa+xb)/2,(ya+yb)/2,PourZ,w/2,w/2,len/2,q)){Rejected++;continue;}
                molten.Add(new NativeMoltenPiece{Parent=parent,X=(float)((xa+xb)/2),Y=(float)((ya+yb)/2),Z=PourZ,Hx=(float)(w/2),Hy=(float)(w/2),Hz=(float)(len/2),Qx=(float)q.X,Qy=(float)q.Y,Qz=(float)q.Z,Qw=(float)q.W,
                    Speed=4.5f,HeatA=1f,HeatB=.96f,Crust=0,Gain=1,Seed=(float)Hash(i,41),Param=(float)t0,Kind=MoltenStream,Axis=2,Sign=1,Drive=MDriveStream,Room=RoomCasting,Hazard=hz,Owner=-1});
            }
            Light(LandX-.3,2.6,PourZ,0xFFB45A,2.4,13,GlowPour,-1,hz,2,1.1);
        }
        // The ingot molds on their rows: the one under the pour fills with each pour and cools between; the rows round it hold
        // metal poured earlier (cooling under crust) or stand empty.
        private void Molds()
        {
            for(int r=0;r<4;r++)
            {
                var b=B("mold-row-"+r);if(b==null)continue;Under("mold-row-"+r);double top=Y1(b),z0=Z0(b),z1=Z1(b);
                for(int i=0;i<4;i++)
                {
                    double a=X0(b)+.15+i*1.2,c=a+1.0;var rim=Iron(0x2A2724,RoleProp);
                    Span(a,c,top,top+.07,z0+.1,z0+.18,rim);Span(a,c,top,top+.07,z1-.18,z1-.1,rim);Span(a,a+.08,top,top+.07,z0+.18,z1-.18,rim);Span(c-.08,c,top,top+.07,z0+.18,z1-.18,rim);
                    bool pour=r==1&&i==1;int state=pour?3:(int)(Hash(r,i,61)*3);
                    if(pour){MeltSpan(a+.08,c-.08,top+.01,top+.06,z0+.18,z1-.18,MoltenFlow,0,1,.25f,.95f,.24f,.25f,MDriveMold,NativeIronworksHazards.Ladle,FrameStatic,24);Light((a+c)/2,1.3,(z0+z1)/2,0xFF8A30,.9,7,GlowMold,0,NativeIronworksHazards.Ladle,1);}
                    else if(state==2)MeltSpan(a+.08,c-.08,top+.01,top+.055,z0+.18,z1-.18,MoltenFlow,0,1,.04f,.48f-.08f*r,.4f-.08f*r,.78f,MDriveSteady);
                    else if(state==1)MeltSpan(a+.08,c-.08,top+.01,top+.05,z0+.18,z1-.18,MoltenSlab,0,1,0,.2f,.15f,.85f,MDriveSteady);
                    else Span(a+.08,c-.08,top+.005,top+.01,z0+.18,z1-.18,Iron(0x0E0D0C,RoleProp));
                    // The sprue's frozen dribble down the row's side (detail).
                    if(state>=1&&Hash(r,i,62)<.6)Span(a+.3,a+.42,top-.4,top,z0-.03,z0,Detail(Iron(0x1E1A16,RoleProp)));
                }
            }
        }
    }
}
