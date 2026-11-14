using System;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The shift board's stopped time (the city's stopped clock, docs section 1).</summary>
        public const string ShiftTime="6:47";

        // ---------------- the time office, the stockhouse and the pattern shop ----------------
        private void East()
        {
            var steel=Iron(IronHex);var dark=Iron(IronDarkHex);var paper=Detail(P(NativeBlockLooks.Plain,PaperHex,RoleSign));
            // ---- the time office: lockers, the clock card rack and its clock, the shift board stopped at 6:47, the stuttering tube ----
            {
                var l=B("lockers-n");if(l!=null)
                {
                    Under("lockers-n");double z=Z0(l);
                    for(int i=0;i<8;i++)
                    {
                        double x0=X0(l)+.04+i*.49,x1=x0+.45;Span(x0,x1,.1,Y1(l)-.06,z-.02,z,Metal(i%3==0?0x4E5A52:0x46524A));for(int v=0;v<4;v++)Span(x0+.08,x1-.08,1.7+v*.06,1.72+v*.06,z-.03,z-.02,Detail(dark));
                        Span(x1-.08,x1-.05,.95,1.15,z-.05,z-.02,Detail(Iron(SteelLightHex)));Span(x0+.12,x1-.12,1.45,1.53,z-.03,z-.02,Detail(P(NativeBlockLooks.Plain,0xD8D0B8,RoleSign)));
                    }
                    Span(X0(l)+.3,X0(l)+.62,Y1(l),Y1(l)+.18,Z0(l)+.3,Z0(l)+.62,Detail(Iron(YellowHex,RoleProp)));Span(X0(l)+1.5,X0(l)+1.82,Y1(l),Y1(l)+.18,Z0(l)+.4,Z0(l)+.72,Detail(Iron(0xD8D4C8,RoleProp)));
                }
                var r=B("clock-rack");if(r!=null)
                {
                    Under("clock-rack");double x=X1(r);for(int row=0;row<5;row++)for(int c=0;c<6;c++){double y=.5+row*.25,z=Z0(r)+.15+c*.3;Span(x,x+.02,y,y+.2,z,z+.22,Detail(Iron(0x2A2E2A)));if(Hash(row,c,71)<.7)Span(x+.02,x+.03,y+.08,y+.26,z+.04,z+.18,Detail(P(NativeBlockLooks.Plain,0xC8BC9C,RoleSign)));}
                    Span(x-.05,x+.22,Y1(r)+.05,Y1(r)+.55,b2(r)-.25,b2(r)+.25,Metal(0x5A3A2A));Span(x+.22,x+.24,Y1(r)+.12,Y1(r)+.48,b2(r)-.18,b2(r)+.18,P(NativeBlockLooks.Plain,0xE8E0C8,RoleSign));
                    // The time clock's hands, stopped at 6:47.
                    double cy=Y1(r)+.3,cz=b2(r);Box(x+.25,cy-.05,cz,.01,.12,.02,Iron(SootHex,RoleSign));Box(x+.25,cy+.02,cz-.05,.01,.14,.015,Iron(SootHex,RoleSign),AxisRot(1,0,0,-80));
                }
                // The shift board on the partition over the lockers: SHIFT B, the red LED digits stopped at 6:47.
                Under("wall-office-n-2");double fz=9.7;Span(44.7,49.3,2.5,4.15,fz-.06,fz,Iron(0x101010,RoleSign));Text("SHIFT B",47,3.7,fz-.06,3,.055,.0,.01,P(NativeBlockLooks.Plain,0xD8D4C8,RoleSign));
                var led=Glow(LedHex,2.2f,RoleSign,DriveLed,.4f);Text("6",45.95,2.75,fz-.06,3,.12,.0,.015,led);Text("47",47.35,2.75,fz-.06,3,.12,.0,.015,led);
                Span(46.48,46.58,2.95,3.05,fz-.075,fz-.06,led);Span(46.48,46.58,3.2,3.3,fz-.075,fz-.06,led);Light(47,3.2,9.0,LedHex,.5,6,GlowLed,-1,-1,.6);
                // The office tube on its chains over the bench: a sodium tube that stutters.
                Under("roof");Span(41.4,45.6,3.98,4.12,-.18,.18,Iron(SootHex,RoleLamp));Span(41.5,45.5,3.92,3.98,-.12,.12,Glow(OfficeHex,2.0f,RoleLamp,DriveFlicker,.55f));
                foreach(double x in new[]{42.0,45.0})Span(x-.015,x+.015,4.12,7.0,-.015,.015,Detail(Iron(ChainHex,RoleChain)));Light(43.5,3.8,0,OfficeHex,.9,12,GlowOffice,-1,-1,1,.55);
                var bn=B("bench");if(bn!=null){Under("bench");Span(X0(bn)+.05,X1(bn)-.05,Y1(bn),Y1(bn)+.04,Z0(bn)+.05,Z1(bn)-.05,Detail(P(NativeBlockLooks.Timber,TimberLightHex,RoleProp)));Span(X0(bn)+.3,X0(bn)+.6,Y1(bn)+.04,Y1(bn)+.16,Z0(bn)+.2,Z0(bn)+.5,Detail(Iron(0x8A2A22,RoleProp)));}
                // Notices and the CLOCK IN sign on the partitions, a fire bucket rack (red buckets) by the opening.
                Under("wall-office-s-0");for(int i=0;i<5;i++){double x=36.8+i*.62;Span(x,x+.42,1.4+Hash(i,3,72)*.3,1.95+Hash(i,3,72)*.3,-9.7,-9.68,paper);}
                Text("CLOCK IN",38.2,2.6,-9.7,2,.07,.0,.012,P(NativeBlockLooks.Plain,0xD8D4C8,RoleSign));
                Under("wall-e-2");for(int i=0;i<4;i++){double z=-6.8+i*.42;Span(36.3,36.52,1.0,1.32,z-.15,z+.15,Iron(RedPaintHex,RoleProp));}Span(36.3,36.42,1.45,1.52,-7.1,-5.3,Detail(dark));Text("FIRE",-6.0,1.7,36.3,0,.05,.0,.01,P(NativeBlockLooks.Plain,0xD8D4C8,RoleSign));
                // Edge angles on the west opening's jambs (both faces) and the exit's frame plates.
                Under("wall-e-2");foreach(double fx in new[]{35.62,36.3})Span(fx,fx+.08,0,5,-4.08,-4.0,steel);Under("wall-e-4");foreach(double fx in new[]{35.62,36.3})Span(fx,fx+.08,0,5,4.0,4.08,steel);
                Under("shell-e");Text("TOWN",0,4.35,50,1,.08,.005,.012,P(NativeBlockLooks.Plain,0xD8D4C8,RoleSign));
                Under("roof");Text("TIME OFFICE",0,10.2,36.3,0,.14,.005,.02,P(NativeBlockLooks.Plain,0xB8B0A0,RoleSign));
            }
            // ---- the stockhouse: the bunkers' hoppers and the chutes over the ore bridge, the conveyor, the ore cart ----
            {
                var bk=B("bunkers");if(bk!=null)
                {
                    Under("bunkers");double fz=Z0(bk);
                    for(double x=37.5;x<50;x+=2.7){Span(x-.12,x+.12,0,13.8,fz-.2,fz,steel);}
                    foreach(double y in new[]{4.8,10.4,13.4})Span(X0(bk),X1(bk),y,y+.18,fz-.24,fz-.2,dark);
                    for(int i=0;i<4;i++){double x=38.85+i*2.7;Text("ORE "+(i+1),x,11.4,fz,3,.07,.005,.01,P(NativeBlockLooks.Plain,0x8A8478,RoleSign));}
                    // The chutes: from the bunkers' gates down over the ore bridge (the bunker-chute spawns' own: they drop out of them).
                    foreach(double cx in new[]{39.0,44.0})
                    {
                        short s=SpawnAt(cx,6,27);Own(s);Begin();var ch=Iron(RustDarkHex);
                        Box(cx,10.6,28.6,.9,.16,2.2,ch,AxisRot(1,0,0,32));Span(cx-.5,cx+.5,9.9,11.6,29.4,29.98,dark);Span(cx-.6,cx+.6,11.6,11.9,29.3,30,Iron(YellowDarkHex));
                        Span(cx-.48,cx-.44,9.4,10.4,27.5,28.2,ch);Span(cx+.44,cx+.48,9.4,10.4,27.5,28.2,ch);End();Own(-1);
                    }
                    Under("deck-ore");for(int i=0;i<10;i++){double x=40.5+Hash(i,5,73)*9,z=22.5+Hash(i,6,73)*5;Box(x,6.006,z,.4+Hash(i,7,73)*.8,.012,.3+Hash(i,8,73)*.6,Detail(Paint(OreHex)),Yaw(Hash(i,9,73)*180));}
                }
                var cv=B("conveyor-frame");if(cv!=null)
                {
                    Under("conveyor-frame");double top=Y1(cv);Span(X0(cv),X1(cv),top,top+.05,Z0(cv)+.15,Z1(cv)-.15,Iron(0x161616));
                    for(int i=0;i<16;i++){double x=X0(cv)+.3+Hash(i,1,74)*(cv.size.x-.6),z=Z0(cv)+.35+Hash(i,2,74)*(cv.size.z-.7);Box(x,top+.1,z,.12+Hash(i,3,74)*.15,.1,.1+Hash(i,4,74)*.12,Iron(OreHex,RoleProp),Yaw(Hash(i,5,74)*180));}
                    for(double x=X0(cv)+.3;x<X1(cv);x+=.6){RodZ(Z0(cv)-.08,Z0(cv),x,top-.1,.06,Detail(dark));RodZ(Z1(cv),Z1(cv)+.08,x,top-.1,.06,Detail(dark));}
                    RodZ(Z0(cv)+.05,Z1(cv)-.05,X1(cv)+.12,top-.05,.18,Iron(GreenDarkHex));
                }
                var oc=B("ore-cart");if(oc!=null){Under("ore-cart");for(int i=0;i<8;i++)Box(X0(oc)+.4+Hash(i,1,75)*1.7,Y1(oc)+.12,Z0(oc)+.4+Hash(i,2,75)*1.7,.3+Hash(i,3,75)*.3,.2,.3+Hash(i,4,75)*.3,Iron(OreHex,RoleProp),Yaw(Hash(i,5,75)*90));
                    foreach(double x in new[]{X0(oc)+.5,X1(oc)-.5})foreach(double z in new[]{Z0(oc)-.08,Z1(oc)+.08})RodX(x-.22,x+.22,.28,z,.24,Iron(SootHex));}
                {short s=SpawnAt(47.6,0,19);Own(s);Under("shell-e");Begin();DoorwayX(19,0,50,2.5,Iron(IronDarkHex),0,-1);End();Own(-1);}
                Under("slab");for(int i=0;i<14;i++){double x=37+Hash(i,1,76)*12.5,z=11+Hash(i,2,76)*18;Box(x,.006,z,.5+Hash(i,3,76)*1.4,.012,.4+Hash(i,4,76)*1.0,Detail(Paint(OreHex)),Yaw(Hash(i,5,76)*180));}
            }
            // ---- the pattern shop: wooden patterns on the racks, flasks, the sand muller, its warm lamp, sand on the floor ----
            {
                var wood=P(NativeBlockLooks.Timber,TimberLightHex,RoleProp);var woodD=P(NativeBlockLooks.Timber,TimberHex,RoleProp);
                var r0=B("pattern-rack-0");if(r0!=null)
                {
                    Under("pattern-rack-0");double fx=X1(r0);
                    foreach(double y in new[]{.7,1.4,2.1})Span(fx,fx+.05,y,y+.04,Z0(r0)+.05,Z1(r0)-.05,woodD);
                    for(int i=0;i<12;i++){double z=Z0(r0)+.4+i*.68,y=(i%3)*.7+.74;if(i%4==1){RingFace(fx+.02,y+.22,z,.2,wood);continue;}Span(fx,fx+.18,y,y+.18+Hash(i,1,77)*.2,z-.25,z+.2,i%2==0?wood:woodD);}
                }
                var r1=B("pattern-rack-1");if(r1!=null)
                {
                    Under("pattern-rack-1");foreach(double fz in new[]{Z0(r1)-.05,Z1(r1)})foreach(double y in new[]{.7,1.4,2.1})Span(X0(r1)+.05,X1(r1)-.05,y,y+.04,fz,fz+.05,woodD);
                    for(int i=0;i<16;i++){double x=X0(r1)+.3+(i%8)*.72,y=(i/8)*.7+.74;bool n=i%2==0;double fz=n?Z1(r1):Z0(r1)-.18;Span(x-.25,x+.25,y,y+.15+Hash(i,2,77)*.25,fz,fz+.18,i%3==0?woodD:wood);}
                }
                var mb=B("mold-boxes");if(mb!=null){Under("mold-boxes");for(int i=0;i<3;i++){double y=.4*i;foreach(double fz in new[]{Z0(mb)-.04,Z1(mb)})Span(X0(mb),X1(mb),y+.02,y+.08,fz,fz+.04,Iron(0x3A3632));}Span(X0(mb)+.1,X1(mb)-.1,Y1(mb),Y1(mb)+.03,Z0(mb)+.1,Z1(mb)-.1,Detail(P(NativeBlockLooks.Plain,SandHex,RoleProp)));}
                var sm=B("sand-muller");if(sm!=null)
                {
                    Under("sand-muller");double cx=sm.pos.x,cz=sm.pos.z,top=Y1(sm);Ring(cx,top-.1,top+.18,cz,1.45,.08,12,Iron(0x4A4A44));Span(cx-1.3,cx+1.3,top,top+.04,cz-1.3,cz+1.3,P(NativeBlockLooks.Plain,SandHex,RoleProp));
                    RodY(top,top+.24,cx,cz,.16,Iron(GreenDarkHex));foreach(double s in new[]{-1.0,1.0}){Box(cx+s*.65,top+.17,cz,.22,.26,.26,Iron(0x2A2826,RoleProp));}Span(cx-.75,cx+.75,top+.19,top+.24,cz-.06,cz+.06,Iron(0x3A3632));
                }
                Under("roof");Span(42.7,43.3,4.3,4.55,-22.3,-21.7,Iron(0x3A4A40,RoleLamp));Span(42.75,43.25,4.24,4.3,-22.25,-21.75,Glow(OfficeHex,2.0f,RoleLamp));Span(42.985,43.015,4.55,9.0,-22.015,-21.985,Detail(Iron(CableHex,RoleChain)));
                Light(43,3.9,-22,OfficeHex,.6,14,GlowLamp,-1,-1,1);
                foreach(double z in new[]{-30.0}){short s=SpawnAt(47.6,0,z);Own(s);Under("shell-e");Begin();DoorwayX(z,0,50,2.5,Iron(IronDarkHex),0,-1);End();Own(-1);}
                Under("slab");for(int i=0;i<12;i++){double x=37+Hash(i,1,78)*12,z=-33+Hash(i,2,78)*21;Box(x,.006,z,.6+Hash(i,3,78)*1.2,.012,.5+Hash(i,4,78)*1.0,Detail(Paint(0x6A5A40)),Yaw(Hash(i,5,78)*180));}
                Under("wall-office-s-2");Text("PATTERN SHOP",47,3.0,-10.3,3,.08,.005,.012,P(NativeBlockLooks.Plain,0xB8B0A0,RoleSign));
            }
        }
        private static double b2(ContentBlock b)=>b.pos.z;
        // A gear pattern seen face on (a ring of plates standing out of a rack's +x face).
        private void RingFace(double x,double y,double z,double r,Pen pen){for(int i=0;i<8;i++){double a=i*Math.PI/4;Box(x+.04,y+Math.Cos(a)*r,z+Math.Sin(a)*r,.08,r*.8,.07,pen,AxisRot(1,0,0,a*180/Math.PI));}}
    }
}
