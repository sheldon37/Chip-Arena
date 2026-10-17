using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The coke battery's jammed-open oven door (x) and the door pitch along the bench.</summary>
        public const float CokeOpenX=-30.25f,CokePitch=1.84f;
        /// <summary>The converter shutter's opening in the converter hall's east wall (x, z half width, height).</summary>
        public const float ShutterX=-24f,ShutterHalf=4f,ShutterTop=5f;

        // ---------------- the coke oven battery ----------------
        private void Coke()
        {
            var b=B("battery");if(b==null)return;Under("battery");const double face=28;var steel=Iron(IronHex);var steelD=Detail(steel);
            // The doors along the bench: the spawn doors at their exits, regular doors between at the pitch, one jammed open.
            var spawnX=new List<double>();var doors=new List<double>();
            foreach(var sp in area?.spawns??Array.Empty<ExpeditionSpawnLayout>()){var e=sp?.entry;if(e?.exit==null||e.kind!="door"||e.exit.y>1||e.from.z<28.4||e.from.x>-24)continue;spawnX.Add(e.exit.x);}
            for(double x=-49.0;x<=-25.2;x+=CokePitch){bool near=false;foreach(double s in spawnX)if(Math.Abs(s-x)<1.35)near=true;if(Math.Abs(x-CokeOpenX)<1.35)near=true;if(!near)doors.Add(x);}
            doors.Add(CokeOpenX);foreach(double s in spawnX)doors.Add(s);doors.Sort();
            for(int i=0;i<doors.Count;i++)
            {
                double d=doors[i];bool spawn=spawnX.Contains(d),open=Math.Abs(d-CokeOpenX)<.01;
                if(spawn){short s=SpawnAt(d,0,25.6);Own(s);Begin();Doorway(d,0,face,2.9,Iron(IronDarkHex),.6f);Span(d-.72,d+.72,3.2,5.0,27.82,27.98,Iron(0x2E2A26));End();Own(-1);}
                else if(open)
                {
                    // Jammed open: the leaf stands off its seat beside the opening, the coke wall glowing inside.
                    Span(d-.85,d-.72,.4,5.0,27.8,28,Iron(IronDarkHex));Span(d+.72,d+.85,.4,5.0,27.8,28,Iron(IronDarkHex));Span(d-.85,d+.85,5.0,5.25,27.8,28,Iron(IronDarkHex));
                    Melt(d,2.7,27.97,1.44,4.6,.03,MoltenCoke,1,1,.25f,.82f,.66f,.25f,MDriveFurnace,-1,FrameStatic,(float)d);
                    Box(d+1.35,2.7,27.84,.12,4.6,1.36,Iron(0x2E2A26),Yaw(0));Span(d+.86,d+1.0,.5,4.9,27.72,27.86,Detail(Iron(SootHex)));
                    Light(d,2.6,26.4,0xFF5A14,1.4,9,GlowCokeDoor,0,-1,1.4,d);
                }
                else
                {
                    bool leak=Hash(i,17,3)<.35;var seam=Glow(SeamHex,leak?1.7f:1.0f,RoleMolten,DriveSeam,(float)d);
                    Span(d-.72,d+.72,.4,5.0,27.82,27.98,Iron(i%3==0?0x34302C:0x2E2A26));
                    Span(d-.74,d+.74,.36,.42,27.8,27.82,seam);Span(d-.74,d+.74,4.98,5.04,27.8,27.82,seam);Span(d-.78,d-.74,.4,5.0,27.8,27.82,seam);Span(d+.74,d+.78,.4,5.0,27.8,27.82,seam);
                    Span(d-.08,d+.08,1.4,3.8,27.74,27.82,Detail(Iron(SootHex)));foreach(double y in new[]{1.1,2.7,4.3})Span(d-.6,d+.6,y,y+.1,27.76,27.82,Detail(Iron(SootHex)));
                    Light(d,2.4,27.2,SeamHex,leak?.35:.18,5,GlowCokeSeam,0,-1,.6,d);
                }
            }
            // Buckstays between the doors (H-columns up the battery), the upper face's standpipes and the collecting main.
            for(int i=0;i+1<doors.Count;i++)
            {
                double x=(doors[i]+doors[i+1])/2;if(doors[i+1]-doors[i]<1.5)continue;
                Span(x-.16,x+.16,0,13.6,27.76,27.8,steel);Span(x-.014,x+.014,0,13.6,27.8,27.98,steelD);
            }
            for(int i=0;i<doors.Count;i++)
            {
                double d=doors[i];if(Math.Abs(d-MachineSpots[6])<1.3)continue;RodY(9.2,12.3,d,27.72,.14,Iron(0x2A2622));Span(d-.18,d+.18,9.2,9.32,27.55,27.95,Detail(Iron(SootHex)));
                Span(d-.32,d+.32,7.0,7.7,27.88,27.98,Iron(0x2E2A26));Span(d-.34,d+.34,6.98,7.02,27.86,27.88,Detail(Glow(SeamHex,.9f,RoleMolten,DriveSeam,(float)d)));
            }
            RodX(-49.6,-24.6,12.75,27.45,.48,Iron(0x2A2724));for(double x=-48.6;x<-25;x+=3.0)RodX(x-.06,x+.06,12.75,27.45,.55,Detail(Iron(IronDarkHex)));
            Text("COKE OVENS",-37,13.55,face,3,.12,.005,.02,P(NativeBlockLooks.Plain,0xB8B0A0,RoleSign));
            // The deck spawn's oven door in the west shell (they climb out onto the walkway).
            {short s=SpawnAt(-48,6,25);Own(s);Under("shell-w");Begin();DoorwayX(25,6,-50,2.3,Iron(IronDarkHex),.6f,1);End();Own(-1);}
            // The pusher car: its frame, cab, the ram (its own frame, into the oven before it), wheels and beacons; the bench rails.
            {
                var p=B("pusher-car");if(p!=null)
                {
                    Under("pusher-car");short hz=NativeIronworksHazards.Pusher;var yel=Iron(YellowHex,RoleHazard);var dk=Iron(IronDarkHex,RoleHazard);double top=Y1(p);
                    Span(X0(p)-.05,X1(p)+.05,top,top+.18,Z0(p)-.05,Z1(p)+.05,dk);Span(X0(p)+.25,X0(p)+1.35,1.6,2.8,Z0(p)-.03,Z0(p),Glow(WindowWarmHex,.3f,RoleHazard,DriveFlicker,.7f));
                    foreach(double x in new[]{X0(p)+.5,X1(p)-.5})foreach(double z in new[]{Z0(p)-.1,Z1(p)+.1})RodX(x-.3,x+.3,.35,z,.3,Iron(SootHex,RoleHazard));
                    var ram=With(Iron(0x4A4640,RoleHazard),frame:FramePusher);Span(-38.35,-37.55,1.8,2.6,Z1(p)-1.4,Z1(p)+.06,ram);Span(-38.5,-37.4,1.6,2.8,Z1(p)+.06,Z1(p)+.18,With(Iron(0x2A2622,RoleHazard),frame:FramePusher));
                    foreach(double x in new[]{X0(p)+.3,X1(p)-.3}){Span(x-.12,x+.12,top+.18,top+.42,b.pos.z-6.2,b.pos.z-5.95,Glow(AmberHex,3.2f,RoleLamp,DriveBeacon,(float)x,hz));}
                    Light(-37,4.1,25.3,AmberHex,1.2,10,GlowBeacon,-1,hz,.5);Text("PUSHER",-37,1.0,Z0(p),3,.07,.0,.012,P(NativeBlockLooks.Plain,0x2A2622,RoleSign));
                    // Dark corner posts and a waist rail break the yellow box into a machine; black chevrons along its foot on the
                    // lane side and the east end; the cab's window on the east end too.
                    foreach(double x in new[]{X0(p),X1(p)-.14})foreach(double z in new[]{Z0(p)-.03,Z1(p)-.11})Span(x,x+.14,.3,top,z,z+.14,dk);
                    Span(X0(p)+.14,X1(p)-.14,1.42,1.54,Z0(p)-.04,Z0(p),dk);Span(X1(p),X1(p)+.04,1.42,1.54,Z0(p)+.14,Z1(p)-.14,dk);
                    var black=Iron(0x1A1816,RoleHazard);for(double x=X0(p)+.3;x<X1(p)-.4;x+=.56)Span(x,x+.28,.35,.72,Z0(p)-.02,Z0(p),Detail(black));
                    for(double z=Z0(p)+.3;z<Z1(p)-.4;z+=.56)Span(X1(p),X1(p)+.02,.35,.72,z,z+.28,Detail(black));
                    Span(X1(p),X1(p)+.03,1.8,2.9,Z0(p)+.3,Z0(p)+1.5,dk);Span(X1(p)+.03,X1(p)+.05,1.9,2.8,Z0(p)+.4,Z0(p)+1.4,Glow(WindowWarmHex,.35f,RoleHazard,DriveFlicker,.4f));
                }
                Under("slab");foreach(double z in new[]{24.8,27.3})Span(-49.6,-24.6,0,.05,z-.04,z+.04,Iron(SteelLightHex,RoleFloor));
            }
            // The quench tower: louvres up its east face, the spray hood high over the quench grate, the grate, steam pipes.
            {
                var q=B("quench-tower");if(q!=null)
                {
                    Under("quench-tower");double fx=X1(q);for(double y=9.0;y<13.8;y+=.42)Box(fx+.08,y,(Z0(q)+Z1(q))/2,.12,.08,Z1(q)-Z0(q)-.3,Iron(0x2A2622),AxisRot(0,0,1,-30));
                    Span(-46.2,-41.8,9.6,10.0,15.95,20.2,Iron(IronDarkHex));for(double x=-45.6;x<-42;x+=.7)RodZ(16.1,20.0,x,9.5,.05,Detail(Iron(GreenDarkHex)));
                    Under("slab");Span(-45.9,-42.1,0,.04,16.15,19.85,Iron(0x1A1816,RoleFloor));for(double x=-45.7;x<-42.1;x+=.24)Span(x-.03,x+.03,0,.055,16.2,19.8,Detail(Iron(0x3A3632,RoleFloor)));
                    Under("quench-tower");RodY(0,9.4,fx+.18,15.6,.12,Iron(RustDarkHex));RodY(0,9.4,fx+.18,13.0,.12,Iron(RustDarkHex));Text("QUENCH",(Z0(q)+Z1(q))/2,7.2,fx,0,.1,.005,.02,P(NativeBlockLooks.Plain,0xB8B0A0,RoleSign));
                    Under("slab");foreach(double z in new[]{17.0,19.0})Span(-49.6,-38.6,0,.05,z-.04,z+.04,Iron(SteelLightHex,RoleFloor));
                }
            }
            // The coal hoppers: lumps heaped in them, their chutes.
            foreach(var stem in new[]{"hopper-0","hopper-1"})
            {
                var h=B(stem);if(h==null)continue;Under(stem);for(int i=0;i<10;i++){double x=X0(h)+.3+Hash(i,stem.Length,6)*(h.size.x-.6),z=Z0(h)+.3+Hash(i,stem[stem.Length-1],7)*(h.size.z-.6);Box(x,Y1(h)+.06,z,.3+Hash(i,3,8)*.3,.12,.25+Hash(i,4,9)*.3,Iron(CokeHex,RoleProp),Yaw(Hash(i,5,10)*180));}
                Span(X0(h)-.05,X1(h)+.05,Y1(h)-.08,Y1(h),Z0(h)-.05,Z1(h)+.05,Iron(IronDarkHex));
            }
        }

        // ---------------- the converter hall ----------------
        private void Converter()
        {
            var steel=Iron(IronHex);var dark=Iron(IronDarkHex);var bandP=Iron(0x2A2826);
            foreach(var stem in new[]{"converter-n","converter-s"})
            {
                var c=B(stem);if(c==null)continue;Under(stem);double cx=c.pos.x,cz=c.pos.z,x0=X0(c),x1=X1(c),z0=Z0(c),z1=Z1(c),top=Y1(c);bool north=cz>0;
                // Hoop bands round the vessel, corner angles, rivet rows (inside the band), the trunnion ring and its bearings.
                foreach(double y in new[]{.6,2.2,6.4}){Span(x0-.04,x1+.04,y,y+.3,z0-.04,z0,bandP);Span(x0-.04,x1+.04,y,y+.3,z1,z1+.04,bandP);Span(x0-.04,x0,y,y+.3,z0,z1,bandP);Span(x1,x1+.04,y,y+.3,z0,z1,bandP);}
                foreach(double sx in new[]{x0,x1})foreach(double sz in new[]{z0,z1})Span(sx-.1,sx+.1,0,top,sz-.1,sz+.1,dark);
                // Less of a box: lit plate edges down the vessel's corners and vertical stiffener ribs on its faces (the shell's
                // curve suggested by the light catching the ribs).
                var lit=Iron(0x5E5850);var rib=Iron(0x332F2B);
                // Lit plate edges either side of each corner on the faces the hall sees (east and the two long sides).
                foreach(double sz in new[]{z0,z1}){double i=sz==z0?1:-1;Span(x1,x1+.04,.3,top-.1,Math.Min(sz+i*.12,sz+i*.26),Math.Max(sz+i*.12,sz+i*.26),lit);Span(x1-.26,x1-.12,.3,top-.1,sz==z0?z0-.04:z1,sz==z0?z0:z1+.04,lit);}
                // Vertical ribs: on the east face, and on the long faces clear of the trunnion.
                foreach(double rz in new[]{z0+1.5,z0+3.0,z0+4.5})Span(x1,x1+.12,.9,top-.1,rz-.06,rz+.06,rib);
                foreach(double rx in new[]{x0+1.5,x0+4.5})foreach(double fz in new[]{z0-.12,z1})Span(rx-.06,rx+.06,.9,top-.1,fz,fz+.12,rib);
                Span(x0-.18,x1+.18,3.6,5.2,z0-.18,z0,Iron(0x3A3632));Span(x0-.18,x1+.18,3.6,5.2,z1,z1+.18,Iron(0x3A3632));Span(x0-.18,x0,3.6,5.2,z0,z1,Iron(0x3A3632));Span(x1,x1+.18,3.6,5.2,z0,z1,Iron(0x3A3632));
                double tz=north?z1:z0,o=north?1:-1;Span(cx-.9,cx+.9,3.9,4.9,Math.Min(tz,tz+o*.24),Math.Max(tz,tz+o*.24),Iron(YellowDarkHex));RodZ(Math.Min(tz,tz+o*.24),Math.Max(tz,tz+o*.24),cx,4.4,.32,Iron(0xB8902C));
                for(double x=x0+.35;x<x1-.2;x+=.55)foreach(double y in new[]{.75,2.35,6.55})Span(x-.03,x+.03,y-.03,y+.03,x>0?z1:z1,z1+.05,Detail(Iron(SteelHex)));
                // The tapered top and the mouth, tipped toward the pit as if waiting for a pour, its metal glowing and smoking.
                var tip=AxisRot(0,0,1,-14);
                Box(cx+.25,top+.35,cz,5.2,.7,5.2,Iron(0x2E2B28),tip);Box(cx+.45,top+.95,cz,4.0,.6,4.0,Iron(0x2A2724),tip);Box(cx+.62,top+1.5,cz,2.8,.55,2.8,Brick(0x5A3A2A,RoleHigh),tip);
                Box(cx+.72,top+1.8,cz,2.9,.12,2.9,Iron(IronDarkHex),tip);
                Melt(cx+.74,top+1.77,cz,1.9,.06,1.9,MoltenFlow,0,1,.15f,.78f,.6f,.45f,MDriveConverter,-1,FrameStatic,(float)cz);
                Light(cx+1.2,top+2.2,cz,0xFF8A33,1.5,13,GlowConverter,0,-1,2,cz*.1);
                Text(north?"VESSEL A":"VESSEL B",cz,1.4,x1,0,.08,.04,.012,P(NativeBlockLooks.Plain,0xC8C0AC,RoleSign));
            }
            // The pouring pit between them: a brick rim and the pit's crusted slag glowing through its cracks (flat: the boss stands here).
            Under("slab");var rim=P(NativeBlockLooks.BrickBuff,0x5A3A2A,RoleFloor);
            Span(-38.3,-31.7,0,.05,-3.3,-3.0,rim);Span(-38.3,-31.7,0,.05,3.0,3.3,rim);Span(-38.3,-38.0,0,.05,-3.0,3.0,rim);Span(-32.0,-31.7,0,.05,-3.0,3.0,rim);
            MeltSpan(-38.0,-32.0,.0,.035,-3.0,3.0,MoltenSlag,0,1,.03f,.42f,.32f,.72f,MDriveConverter,-1,FrameStatic,.4f);
            for(double x=-37.6;x<-32.2;x+=.6)Span(x-.04,x+.04,0,.055,-2.9,2.9,Detail(Iron(0x24201C,RoleFloor)));
            Light(-35,1.2,0,0xFF6A20,.9,12,GlowPit,0,-1,2);
            // The hall's glow spilling out through the shutter opening onto the casting floor's west end (field only): what the
            // party sees beyond the zombies coming down the floor.
            Light(-26.2,2.6,0,0xFF7A30,1.6,11,GlowFill,0,-1,2);
            // The converter crane: a bridge across the hall's runways and its hook over the pit.
            Under("conv-crane-runway-e");var yel=Iron(YellowHex);Span(-49.3,-26.7,13.32,14.15,-1.1,1.1,yel);Span(-49.4,-26.6,13.32,13.9,-1.5,-1.1,Iron(YellowDarkHex));Span(-49.4,-26.6,13.32,13.9,1.1,1.5,Iron(YellowDarkHex));
            Span(-36.0,-34.0,12.4,13.32,-.9,.9,Iron(GreenDarkHex));foreach(double dz in new[]{-.2,.2})Span(-35.02,-34.98,10.4,12.4,dz-.02,dz+.02,Iron(CableHex,RoleChain));
            Span(-35.45,-34.55,9.6,10.4,-.35,.35,Iron(YellowHex,RoleChain));Span(-35.1,-34.9,9.2,9.6,-.1,.1,Iron(IronDarkHex,RoleChain));Span(-35.1,-34.6,9.05,9.2,-.1,.1,Iron(IronDarkHex,RoleChain));
            // The shutter's frame in the east wall (the shutter itself is NativeIronworksShutter's): guide channels either side of the
            // opening on both faces, the drum housing over it on the hall's side, hazard stripes on the jambs, the sign.
            Under("wall-w-3");var guide=Iron(0x3A3836);var stripe=P(NativeBlockLooks.Demolition,0xF4B321,RoleSign);
            foreach(double side in new[]{-1.0,1.0})
            {
                double zj=side*(ShutterHalf+.22);foreach(double fx in new[]{-24.54,-23.7})Span(fx,fx+.24,0,ShutterTop+.45,zj-.16,zj+.16,guide);
                for(double y=.3;y<4.6;y+=.8){double a=side*(ShutterHalf+.06),c=side*(ShutterHalf+.1);Span(-24.58,-24.54,y,y+.4,Math.Min(a,c),Math.Max(a,c),stripe);Span(-23.46,-23.42,y,y+.4,Math.Min(a,c),Math.Max(a,c),stripe);}
            }
            Span(-24.95,-24.36,ShutterTop,ShutterTop+1.05,-ShutterHalf-.45,ShutterHalf+.45,Iron(0x2E2B28));RodZ(-ShutterHalf-.5,-ShutterHalf-.35,-24.62,ShutterTop+.52,.28,Iron(GreenDarkHex));
            Text("CONVERTERS",0,5.08,-23.7,0,.1,.005,.015,P(NativeBlockLooks.Plain,0xC8C0AC,RoleSign));
            Text("CONVERTER HALL",0,6.25,-24.3,1,.14,.005,.02,P(NativeBlockLooks.Plain,0xC8C0AC,RoleSign));
            Breach("breach-conv-w");
        }

        // ---------------- the slag yard and the drop forge ----------------
        private void Slag()
        {
            var steel=Iron(IronHex);var dark=Iron(IronDarkHex);short hz=NativeIronworksHazards.Slag;
            // The yard's floor: worn iron plates and slag-blackened ground (flat), not bare concrete, so the yard reads dark and
            // stained under its lamps; a few dull cinders where slag was spilled long ago (cold, nothing like the live tongue).
            {
                Under("slab");Span(-44.0,-24.4,0,.005,-33.9,-12.4,Paint(0x1E1A17));
                var plates=new[]{Iron(0x2A2622,RoleFloor),Iron(0x24201D,RoleFloor),Iron(0x302A24,RoleFloor),Paint(0x191614),Paint(0x26201A)};
                for(double x=-43.6;x<-24.8;x+=2.4)for(double z=-33.4;z<-14.8;z+=2.4)
                {
                    double cx=x+1.2,cz=z+1.2;if(OverBlock(cx,cz,1.3))continue;int k=(int)(Hash((int)(x*10),(int)(z*10),41)*plates.Length);
                    if(Hash((int)(x*10),(int)(z*10),44)<.3)continue;double jx=(Hash((int)(x*10),(int)(z*10),42)-.5)*.5,jz=(Hash((int)(x*10),(int)(z*10),45)-.5)*.5;
                    Span(x+.1+jx,x+2.2+jx,.006,.012+.004*(k%3),z+.1+jz,z+2.1+jz,Detail(plates[k]));
                }
                for(int i=0;i<5;i++){double cx=-42+Hash(i,43,1)*16,cz=-32.5+Hash(i,43,2)*16;if(OverBlock(cx,cz,1.2)||cx>-39.5&&cx<-32.5&&cz>-28&&cz<-21)continue;MeltSpan(cx-.5,cx+.5,.012,.03,cz-.35,cz+.35,MoltenSlag,0,1,.0f,.18f,.12f,.92f,MDriveSteady,-1,FrameStatic,0,.6f,true);}
            }
            // The slag bank: a glassy crust over its top with glowing cracks, the cracks down its face, lumps.
            {
                var b=B("slag-bank");if(b!=null)
                {
                    Under("slag-bank");double top=Y1(b);MeltSpan(X0(b)+.05,X1(b)-.05,top,top+.04,Z0(b)+.05,Z1(b)-.05,MoltenSlag,0,1,.01f,.24f,.14f,.86f,MDriveSteady,-1,FrameStatic,0,.9f);
                    for(int i=0;i<12;i++){double z=Z0(b)+.5+Hash(11,i,1)*(b.size.z-1);Span(X1(b),X1(b)+.04,.15+Hash(11,i,2)*.3,1.7+Hash(11,i,3)*.25,z,z+.06+Hash(11,i,4)*.05,Glow(0xFF3A0A,1.2f,RoleMolten,DriveSeam,(float)z));}
                    for(int i=0;i<14;i++){double x=X0(b)+.6+Hash(12,i,1)*(b.size.x-1.2),z=Z0(b)+.6+Hash(12,i,2)*(b.size.z-1.2);Box(x,top+.1,z,.4+Hash(12,i,3)*.6,.2,.3+Hash(12,i,4)*.5,Iron(0x15130F,RoleProp),Yaw(Hash(12,i,5)*180));}
                    Light(-46.5,2.6,-24,0xFF3A0A,.7,10,GlowSlag,0,-1,1.5);
                }
            }
            // The spill: the pot on the bank's edge (its own frame, tipping east), its pour down the bank's face, the tongue across
            // the yard (spreading east while live, cooling to the dark crust that stays).
            {
                Under("slag-bank");byte f=FrameSlagPot;double px=FrameOrigin[f*3],py=FrameOrigin[f*3+1],pz=FrameOrigin[f*3+2];var pot=With(Iron(0x2A2622,RoleHazard),frame:f);
                Span(px-.95,px+.95,py,py+.35,pz-1.0,pz+1.0,Iron(IronDarkHex,RoleHazard));Ring(px,py+.35,py+2.0,pz,.9,.12,10,pot);Ring(px,py+1.85,py+2.05,pz,.96,.14,10,With(Iron(IronDarkHex,RoleHazard),frame:f));
                Melt(px,py+1.82,pz,1.5,.05,1.5,MoltenSlag,0,1,.2f,.55f,.45f,.55f,MDriveSlagPot,hz,f);
                Melt(-43.97,1.0,-24.6,.05,2.0,.9,MoltenFlow,1,-1,1.6f,.85f,.72f,.25f,MDriveSlagTongue,hz,FrameStatic,0,1);
                Under("slab");MeltSpan(-44.0,-39.0,0,.04,-25.0,-24.2,MoltenSlag,0,1,.25f,.5f,.4f,.45f,MDriveSlagTongue,hz);
                MeltSpan(-39.0,-33.0,0,.045,-26.5,-22.5,MoltenSlag,0,1,.3f,.88f,.55f,.3f,MDriveSlagTongue,hz);
                Light(-37,1.0,-24.5,0xFF5A14,1.6,13,GlowSlag,-1,hz,2);
                // Older cooled tongues (dark glassy patches, flat).
                for(int i=0;i<5;i++){double x=-43+Hash(13,i,1)*8,z=-32+Hash(13,i,2)*5+(i%2==0?0:11);Box(x,.006,z,1.6+Hash(13,i,3)*2,.012,.8+Hash(13,i,4)*1.2,Detail(Paint(0x0E0C0B)),Yaw(Hash(13,i,5)*60-30));}
            }
            // The slag pots on their cars: bands, the crust glowing faintly, wheels.
            foreach(var stem in new[]{"slag-pot-0","slag-pot-1"})
            {
                var p=B(stem);if(p==null)continue;Under(stem);double top=Y1(p);
                foreach(double y in new[]{.5,1.6,2.6}){Span(X0(p)-.04,X1(p)+.04,y,y+.18,Z0(p)-.04,Z0(p),dark);Span(X0(p)-.04,X1(p)+.04,y,y+.18,Z1(p),Z1(p)+.04,dark);Span(X0(p)-.04,X0(p),y,y+.18,Z0(p),Z1(p),dark);Span(X1(p),X1(p)+.04,y,y+.18,Z0(p),Z1(p),dark);}
                MeltSpan(X0(p)+.25,X1(p)-.25,top,top+.04,Z0(p)+.25,Z1(p)-.25,MoltenSlag,0,1,.02f,.32f,.22f,.8f);
                foreach(double x in new[]{X0(p)+.6,X1(p)-.6})foreach(double z in new[]{Z0(p)-.12,Z1(p)+.12})if(Free(x-.3,x+.3,0,.62,z-.27,z+.27))RodX(x-.28,x+.28,.32,z,.3,Iron(SootHex));
            }
            // The drop hammer: guides on the columns, the ram (its own frame), the steam cylinder on the head, the anvil's billet.
            {
                var w=B("hammer-col-w");var e=B("hammer-col-e");var head=B("hammer-head");var anvil=B("anvil");
                if(w!=null&&e!=null&&head!=null&&anvil!=null)
                {
                    Under("hammer-head");short h=NativeIronworksHazards.Hammer;var ram=With(Iron(0x3E3C38,RoleHazard),frame:FrameRam);var die=With(Iron(0x6E6A64,RoleHazard),frame:FrameRam);
                    Span(X1(w),X1(w)+.12,.9,Y0(head),Z0(w)+.35,Z1(w)-.35,Iron(SteelLightHex));Span(X0(e)-.12,X0(e),.9,Y0(head),Z0(e)+.35,Z1(e)-.35,Iron(SteelLightHex));
                    Span(-30.6,-28.4,6.35,7.75,-28.95,-27.85,ram);Span(-30.5,-28.5,6.25,6.35,-28.9,-27.9,die);RodY(7.75,14.0,-29.5,-28.4,.16,With(Iron(SteelLightHex,RoleHazard),frame:FrameRam));
                    Span(-31.68,-31.64,7.3,7.8,-28.9,-27.9,Glow(AmberHex,2.4f,RoleLamp,DriveWarnLamp,1,h));Span(-27.36,-27.32,7.3,7.8,-28.9,-27.9,Glow(AmberHex,2.4f,RoleLamp,DriveWarnLamp,1.5f,h));
                    RodY(Y1(head),14.15,-29.5,-28.4,.5,Iron(GreenDarkHex));Span(-30.3,-28.7,14.15,14.32,-29.2,-27.6,dark);RodX(-31.8,-29.9,12.4,-28.4,.1,Iron(RustDarkHex));
                    Under("anvil");Span(X0(anvil)-.05,X1(anvil)+.05,Y1(anvil)-.1,Y1(anvil),Z0(anvil)-.05,Z1(anvil)+.05,Iron(0x2A2826));
                    MeltSpan(-30.1,-28.9,Y1(anvil),Y1(anvil)+.35,-28.85,-27.95,MoltenSlab,0,1,0,.86f,.8f,.2f,MDriveBillet,h);
                    Light(-29.5,1.6,-28.4,0xFF7A20,1.1,8,GlowBillet,-1,h,.8);Text("DROP FORGE",-28.4,10.35,Z1(head),2,.09,.005,.015,P(NativeBlockLooks.Plain,0xC8C0AC,RoleSign));
                }
            }
            // The forge hearth: coals glowing on it, its hood and flue up the shell, the tong rack.
            {
                var f=B("forge-hearth");if(f!=null)
                {
                    Under("forge-hearth");MeltSpan(X0(f)+.2,X1(f)-.2,Y1(f)-.02,Y1(f)+.04,Z0(f)+.2,Z1(f)-.2,MoltenCoke,0,1,.05f,.72f,.6f,.35f,MDriveFurnace,-1,FrameStatic,.6f);
                    Span(X0(f)-.1,X1(f)+.1,Y1(f),Y1(f)+.12,Z0(f)-.1,Z1(f)+.1,Iron(IronDarkHex));Span(X0(f)-.1,X1(f)+.1,3.2,3.8,Z0(f)-.1,Z1(f)+.1,Iron(0x2A2622));Span((X0(f)+X1(f))/2-.4,(X0(f)+X1(f))/2+.4,3.8,12.0,-33.9,-33.3,Iron(0x2A2622));
                    Under("shell-s");for(int i=0;i<5;i++)Member(-26.6+i*.18,1.0,-33.92,-26.5+i*.18,2.2,-33.92,.03,.03,Detail(Iron(0x2A2622,RoleProp)));
                    Light(-26,2.2,-32.2,0xFF6020,1.0,8,GlowHearth,0,-1,1);
                }
            }
            // Ingots stacked crosswise on the stack, the yard's door, the forge's breach, the slag pits' grates (their own).
            {var s=B("ingot-stack");if(s!=null){Under("ingot-stack");for(int i=0;i<4;i++)RodZ(Z0(s)+.1,Z1(s)-.1,X0(s)+.3+i*.47,Y1(s)+.1,.13,Iron(0x5A564E));}}
            {short s=SpawnAt(-26.7,0,-19.5);Own(s);Under("wall-w-2");Begin();DoorwayX(-19.5,0,-24.3,2.5,Iron(IronDarkHex),0,-1);End();Own(-1);}
            Breach("breach-forge-s");
            Grates();
        }
        // Every floor vent (slag trenches and pits, mill pits) gets a heavy grate over a dull glow: its own dressing (flat).
        private void Grates()
        {
            var spawns=area?.spawns;if(spawns==null)return;
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.from==null||e.kind!="vent"||e.surface=="ceiling")continue;Own(s);Under("slab");Begin();double x=e.from.x,y=e.from.y,z=e.from.z;
                Span(x-.95,x+.95,y,y+.045,z-.95,z+.95,Iron(0x1A1816,RoleSpawn));for(double u=-.8;u<=.81;u+=.2)Span(x+u-.03,x+u+.03,y,y+.06,z-.85,z+.85,Iron(0x3A3632,RoleSpawn));
                MeltSpan(x-.8,x+.8,y,y+.02,z-.8,z+.8,MoltenSlag,0,1,.05f,.4f,.3f,.7f,MDriveSteady,-1,FrameStatic,0,.7f);End();Own(-1);
            }
        }
    }
}
