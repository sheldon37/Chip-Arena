using System;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeIronworksKit
    {
        /// <summary>The roof trusses' x lines (every 6.5 m; the gantry hang points sit under three of them), the bottom chord's
        /// underside (the Rigger's truss hook height, docs section 2.6) and the top chord's top (under the roof sheeting).</summary>
        public static readonly float[] TrussX={-43f,-36.5f,-30f,-17f,-10.5f,-4f,2.5f,9f,15.5f,22f,28.5f,35f,41.5f,48f};
        public const float TrussBottom=14.4f,TrussTop=17.75f;
        /// <summary>The roof monitors' z lines (cold skylight strips along the roof).</summary>
        public static readonly float[] MonitorZ={-24f,-2f,18f};
        /// <summary>The ladle crane's hook block rest height (its cables run from the trolley's drum down to it).</summary>
        public const float HookY=10.55f;

        private void Structure()
        {
            Under("roof");var truss=Iron(SootHex);var trussD=Detail(truss);
            // ---- Pratt trusses N-S across the whole works: chords, verticals every 3.4 m, diagonals falling to the middle ----
            foreach(float x in TrussX)
            {
                double z0=-33.9,z1=33.9;
                IBeam(x-.16,x+.16,TrussTop-.34,TrussTop,z0,z1,truss);IBeam(x-.16,x+.16,TrussBottom,TrussBottom+.3,z0,z1,truss);
                int n=14;double step=(z1-z0)/n;
                for(int i=0;i<=n;i++){double z=z0+i*step;Span(x-.09,x+.09,TrussBottom+.3,TrussTop-.34,z-.07,z+.07,i%2==0?truss:trussD);}
                for(int i=0;i<n;i++)
                {
                    double za=z0+i*step,zb=za+step;bool down=za+step/2<0;double ya=TrussBottom+.3,yb=TrussTop-.34;
                    if(down)Member(x,yb,za,x,ya,zb,.12,.1,trussD);else Member(x,ya,za,x,yb,zb,.12,.1,trussD);
                }
                // Gusset plates at the chords' panel points over the casting floor (detail).
                for(int i=2;i<n;i+=3){double z=z0+i*step;Span(x-.03,x+.03,TrussBottom-.02,TrussBottom+.55,z-.22,z+.22,trussD);}
            }
            // ---- purlins (one run each along the roof) and the monitors' cold glass with their mullions ----
            for(double z=-31;z<=31;z+=3.875)Span(-49.9,49.9,TrussTop,TrussTop+.2,z-.06,z+.06,trussD);
            foreach(float z in MonitorZ)
            {
                Span(-49.6,49.6,17.92,17.99,z-1.25,z+1.25,Glow(GlassColdHex,.22f,RoleHigh));
                for(double x=-48;x<=48;x+=4.8)Span(x-.05,x+.05,17.86,17.97,z-1.3,z+1.3,trussD);
                Span(-49.6,49.6,17.82,17.95,z-1.36,z-1.24,truss);Span(-49.6,49.6,17.82,17.95,z+1.24,z+1.36,truss);
            }
            // ---- wall pilasters at the truss lines on the exposed south shell, the partitions' faces and the east shell ----
            var pil=Iron(IronDarkHex,RoleWall);var pilD=Detail(pil);
            foreach(float x in TrussX){Under("shell-s");Pilaster(x,-34,1,17.4,pil,pilD);}
            for(double z=-30;z<=30;z+=6.5){Under("shell-e");PilasterX(50,-1,z,17.4,pil,pilD);Under("shell-w");PilasterX(-50,1,z,17.4,pil,pilD);}
            foreach(float x in new[]{-17f,-10.5f,-4f,2.5f,9f,15.5f,22f,28.5f}){Under("wall-mill-0");Pilaster(x,-13.7,1,5.6,pil,pilD);Pilaster(x,-14.3,-1,5.6,pil,pilD);}
            for(double z=-30;z<=30;z+=6.5){Under("wall-w-2");PilasterX(-23.7,1,z,13.9,pil,pilD);PilasterX(-24.3,-1,z,13.9,pil,pilD);Under("wall-e-2");PilasterX(35.7,-1,z,13.9,pil,pilD);PilasterX(36.3,1,z,13.9,pil,pilD);}
            // ---- the ladle crane's runways (hung from the trusses), the bridge, end trucks, trolley, cab, cables and hook ----
            CraneRunway(20.6,21.4);CraneRunway(-12.4,-11.6);Crane();
            // ---- the converter hall's runways: wall brackets and hangers ----
            Under("conv-crane-runway-e");Span(-27.05,-26.15,13.2,13.32,-11.7,11.7,Iron(SteelHex));
            for(double z=-10;z<=10;z+=5){if(Free(-26.7,-24.3,10.3,12.6,z-.2,z+.2))Member(-26.6,12.5,z,-24.3,10.4,z,.18,.18,Iron(IronDarkHex));Span(-26.7,-26.5,13.2,TrussBottom,z-.06,z+.06,Detail(Iron(IronDarkHex)));}
            Under("conv-crane-runway-w");Span(-49.85,-48.95,13.2,13.32,-11.7,11.7,Iron(SteelHex));
            for(double z=-10;z<=10;z+=5)Span(-50,-49,11.6,12.4,z-.25,z+.25,Iron(IronDarkHex));
            Walkways();Lamps();Mains();
        }
        // A wall pilaster (a flat steel column 0.3 m wide, 0.22 m deep: inside the wall band) on a face at z = plane, sign +1 out to +z.
        private void Pilaster(double x,double plane,int sign,double top,Pen pen,Pen detail)
        {
            double a=plane,b=plane+sign*.22;if(!Free(x-.24,x+.24,0,top,Math.Min(a,b+sign*.2),Math.Max(a,b+sign*.2)))return;Span(x-.15,x+.15,0,top,Math.Min(a,b),Math.Max(a,b),pen);double c=plane+sign*.24;
            Span(x-.22,x+.22,0,.04,Math.Min(a,c),Math.Max(a,c),detail);if(top>9)Span(x-.2,x+.2,top-.25,top,Math.Min(a,b+sign*.15),Math.Max(a,b+sign*.15),detail);
        }
        private void PilasterX(double plane,int sign,double z,double top,Pen pen,Pen detail)
        {
            double a=plane,b=plane+sign*.22;if(!Free(Math.Min(a,b+sign*.2),Math.Max(a,b+sign*.2),0,top,z-.24,z+.24))return;Span(Math.Min(a,b),Math.Max(a,b),0,top,z-.15,z+.15,pen);double c=plane+sign*.24;Span(Math.Min(a,c),Math.Max(a,c),0,.04,z-.22,z+.22,detail);
        }
        private void CraneRunway(double z0,double z1)
        {
            Under(z0>0?"crane-runway-n":"crane-runway-s");var rail=Iron(SteelLightHex);var h=Iron(IronDarkHex);var hd=Detail(h);double zc=(z0+z1)/2;
            Span(-23.6,35.6,13.2,13.33,zc-.06,zc+.06,rail);Span(-23.6,35.6,13.2,13.24,zc-.14,zc+.14,Detail(Iron(SteelHex)));
            foreach(float x in TrussX){if(x<-23||x>35)continue;Span(x-.05,x+.05,13.2,TrussBottom,z0-.08,z0+.02,h);Span(x-.05,x+.05,13.2,TrussBottom,z1-.02,z1+.08,h);}
            // Stiffeners along the box girder's sides and the end stops (bumpers) at the walls.
            for(double x=-22.5;x<35;x+=3){Span(x-.03,x+.03,12.45,13.15,z0-.05,z0,hd);Span(x-.03,x+.03,12.45,13.15,z1,z1+.05,hd);}
            foreach(double x in new[]{-23.4,35.4}){Span(x-.2,x+.2,13.2,13.8,zc-.3,zc+.3,Iron(YellowHex));Span(x-.25,x+.25,13.35,13.65,zc-.18,zc+.18,Iron(SootHex));}
        }
        private void Crane()
        {
            var y=Iron(YellowHex);var yd=Detail(y);var dark=Iron(IronDarkHex);
            Under("crane-bridge");
            // The bridge's walkway rail, stiffeners and its end trucks riding the runways' rails (wheels on the rails).
            for(double z=-12;z<=21;z+=3){Span(3.95,4.0,13.25,14.35,z-.03,z+.03,yd);Span(7.0,7.05,13.25,14.35,z-.03,z+.03,yd);Span(3.5,3.56,14.4,15.4,z-.03,z+.03,yd);}
            Span(3.5,3.56,15.3,15.4,-12.3,21.3,y);Span(3.5,3.56,14.85,14.92,-12.3,21.3,yd);Span(3.5,4.0,14.38,14.42,-12.3,21.3,Iron(SteelHex));
            foreach(double zc in new[]{21.0,-12.0})
            {
                Span(3.3,7.7,13.32,14.1,zc-.7,zc+.7,y);Span(3.25,7.75,13.32,13.4,zc-.75,zc+.75,dark);
                foreach(double x in new[]{3.8,7.2})RodX(x-.25,x+.25,13.45,zc,.2,dark);
                Span(3.3,7.7,14.1,14.3,zc-.5,zc+.5,Detail(Iron(SootHex)));
            }
            // The operator's cab under the bridge's north end: a lit window looking down on the pour.
            Span(4.2,6.8,10.7,13.2,16.6,19.2,Iron(YellowDarkHex));Span(4.15,6.85,11.5,12.6,16.55,16.6,Glow(WindowWarmHex,.55f,RoleHigh,DriveFlicker,.3f));
            Span(4.15,4.2,11.5,12.6,16.7,19.0,Glow(WindowWarmHex,.4f,RoleHigh));Span(4.1,6.9,10.6,10.7,16.5,19.3,dark);
            // The trolley: hoist drum, motor, gearbox and the rope sheaves.
            Under("crane-trolley");RingX(4.0,7.0,12.55,-6.0,.42,.08,10,dark);Span(3.8,4.4,12.2,12.95,-7.3,-6.7,Iron(GreenHex));Span(6.6,7.2,12.25,12.9,-5.4,-4.7,Iron(GreenHex));
            Span(3.65,7.35,13.17,13.22,-7.45,-4.55,Detail(Iron(SteelHex)));
            // The hook block and the four rope falls down to it (its own frame: it rises with the ladle when the crane parks).
            var hk=With(Iron(YellowHex),frame:FrameHook);var rope=With(Iron(CableHex),frame:FrameHook,role:RoleChain);
            Span(4.8,6.2,HookY,HookY+.75,-7.75,-6.95,hk);Span(4.75,6.25,HookY+.75,HookY+.85,-7.8,-6.9,With(Iron(SootHex),frame:FrameHook));
            foreach(double dx in new[]{-.45,.45})foreach(double dz in new[]{-.25,.25})Span(5.5+dx-.025,5.5+dx+.025,HookY+.85,12.2,-7.35+dz-.025,-7.35+dz+.025,rope);
            // The bail: two straps from the hook block down to the ladle's trunnions.
            var bail=With(Iron(IronDarkHex),frame:FrameHook);Span(5.3,5.7,HookY-1.95,HookY,-8.95,-8.75,bail);Span(5.3,5.7,HookY-1.95,HookY,-5.95,-5.75,bail);Span(5.2,5.8,HookY-.2,HookY,-8.95,-5.75,bail);
        }
        // The upper ring: toe boards along every deck edge, handrail tubes and posts on the rail panels, knee braces under the decks.
        private void Walkways()
        {
            var toe=Iron(YellowDarkHex,RoleStructure);var tube=Iron(YellowHex);var post=Detail(Iron(YellowHex));var joist=Detail(Iron(IronDarkHex));
            for(int i=0;i<blocks.Length;i++)
            {
                var s=Stem(blocks[i]?.id);if(s==null||!Starts(s,"deck-"))continue;var b=blocks[i];parent=i;
                if(s.Contains("-rail-"))
                {
                    bool alongX=b.size.x>b.size.z;double top=Y1(b);
                    if(alongX){RodX(X0(b),X1(b),top+.03,b.pos.z,.03,tube);for(double x=X0(b)+.05;x<X1(b);x+=1.4)Span(x-.03,x+.03,Y0(b),top,b.pos.z-.05,b.pos.z+.05,post);}
                    else{RodZ(Z0(b),Z1(b),b.pos.x,top+.03,.03,tube);for(double z=Z0(b)+.05;z<Z1(b);z+=1.4)Span(b.pos.x-.05,b.pos.x+.05,Y0(b),top,z-.03,z+.03,post);}
                    Span(X0(b)-.005,X1(b)+.005,Y0(b),Y0(b)+.14,Z0(b)-.005,Z1(b)+.005,toe);continue;
                }
                if(s.Contains("-col-"))
                {
                    Span(X0(b)-.1,X1(b)+.1,0,.03,Z0(b)-.1,Z1(b)+.1,Detail(Iron(SteelHex)));Span(X0(b)-.08,X1(b)+.08,Y1(b)-.12,Y1(b),Z0(b)-.08,Z1(b)+.08,Iron(IronDarkHex));
                    continue;
                }
                // A deck: its edge channel (fascia) all round and joists under it every 1.5 m (both above the floor's band).
                double y0=Y0(b),y1=Y1(b)-.02;var fascia=Iron(IronDarkHex);
                Span(X0(b),X1(b),y0-.12,y1,Z0(b)-.03,Z0(b),fascia);Span(X0(b),X1(b),y0-.12,y1,Z1(b),Z1(b)+.03,fascia);Span(X0(b)-.03,X0(b),y0-.12,y1,Z0(b),Z1(b),fascia);Span(X1(b),X1(b)+.03,y0-.12,y1,Z0(b),Z1(b),fascia);
                if(b.size.x>=b.size.z){for(double x=X0(b)+.75;x<X1(b);x+=1.5)Span(x-.05,x+.05,y0-.22,y0,Z0(b)+.05,Z1(b)-.05,joist);}
                else for(double z=Z0(b)+.75;z<Z1(b);z+=1.5)Span(X0(b)+.05,X1(b)-.05,y0-.22,y0,z-.05,z+.05,joist);
            }
            // Stair stringers: a channel up each side of every flight (inside the band of its steps).
            for(int i=0;i<blocks.Length;i++)
            {
                var s=Stem(blocks[i]?.id);if(s==null||!Starts(s,"stair-")||!s.EndsWith("-00",StringComparison.Ordinal))continue;string flight=s.Substring(0,s.Length-3);
                int last=-1;for(int k=23;k>=0;k--){last=Block(flight+"-"+k.ToString("00"));if(last>=0)break;}if(last<0)continue;var a=blocks[i];var c=blocks[last];parent=i;
                bool alongZ=Math.Abs(c.pos.z-a.pos.z)>Math.Abs(c.pos.x-a.pos.x);double ax=a.pos.x,az=a.pos.z,cx=c.pos.x,cz=c.pos.z;
                if(alongZ){foreach(double x in new[]{X0(a)-.04,X1(a)+.04})Member(x,.12,az,x,Y1(c)-.12,cz,.06,.28,Iron(IronDarkHex));}
                else foreach(double z in new[]{Z0(a)-.04,Z1(a)+.04})Member(ax,.12,z,cx,Y1(c)-.12,z,.06,.28,Iron(IronDarkHex));
            }
        }
        // Sodium high-bays and mercury work lamps (the cold counter-light): housings on rods from the trusses or brackets, lit lenses.
        private void Lamps()
        {
            Under("roof");var house=Iron(SootHex,RoleLamp);var rod=Detail(Iron(IronDarkHex,RoleLamp));
            foreach(var l in SodiumLamps)
            {
                float x=l[0],y=l[1],z=l[2];Span(x-.035,x+.035,y+.42,Math.Max(y+.5,TrussBottom),z-.035,z+.035,rod);Span(x-.36,x+.36,y+.12,y+.42,z-.36,z+.36,house);Span(x-.42,x+.42,y+.06,y+.14,z-.42,z+.42,house);
                Span(x-.3,x+.3,y,y+.07,z-.3,z+.3,Glow(SodiumHex,2.4f,RoleLamp,l[3]>0?DriveSodium:DriveSteady,(float)Hash((int)(x*10),(int)(z*10),3)));
                Light(x,y-.3,z,SodiumHex,l[4],l[5],GlowSodium,-1,-1,1,l[3]>0?1:0);
            }
            foreach(var l in MercuryLamps)
            {
                float x=l[0],y=l[1],z=l[2];Span(x-.035,x+.035,y+.3,y<6?5.7:Math.Max(y+.4,TrussBottom),z-.035,z+.035,rod);Span(x-.5,x+.5,y+.1,y+.3,z-.28,z+.28,house);
                Span(x-.44,x+.44,y,y+.1,z-.22,z+.22,Glow(MercuryHex,2.6f,RoleLamp,DriveMercury,(float)Hash((int)(x*10),(int)(z*10),4)));
                Light(x,y-.3,z,MercuryHex,l[3],l[4],GlowMercury);
            }
        }
        /// <summary>Sodium high-bays (x, y, z, stutters, intensity, range) and mercury lamps (x, y, z, intensity, range): the doc's
        /// practical lights and the picker's cool pair (docs section 3.1 and 9.2).</summary>
        public static readonly float[][] SodiumLamps={new[]{18f,12f,4f,1,.4f,14},new[]{-12f,12f,-2f,0,.4f,14},new[]{8f,11.2f,18.6f,0,.35f,13},new[]{-16f,10.4f,-25f,0,.5f,15},new[]{-5f,10.4f,-25f,0,.5f,15},new[]{6f,10.4f,-25f,0,.5f,15},
            new[]{17f,10.4f,-25f,1,.5f,15},new[]{28f,10.4f,-24f,0,.5f,15},new[]{-37f,10.6f,18f,0,.55f,14},new[]{-35f,12f,0f,0,.55f,18},new[]{-36f,10f,-24f,0,.5f,16},new[]{44f,11f,18f,0,.6f,18}};
        public static readonly float[][] MercuryLamps={new[]{-31f,9.2f,19.5f,1.3f,16},new[]{-29f,10.5f,9.5f,1.0f,17},new[]{-29f,10.5f,-9.5f,1.0f,17},new[]{-33f,9.4f,-17f,.75f,15},new[]{-14f,12f,12f,.9f,16},
            new[]{-14f,10.4f,-18.5f,.85f,14},new[]{45f,9f,24f,.8f,14},new[]{0f,12f,-11f,.8f,15},new[]{26f,12f,14f,.8f,15},new[]{20f,10.4f,-30.5f,.75f,14},
            new[]{-10.5f,12.6f,24.8f,.85f,14},new[]{22f,12.6f,24.8f,.85f,14},new[]{-43.5f,5.35f,24.6f,.7f,10},new[]{-33.6f,5.35f,24.6f,.7f,10},new[]{-7.5f,5.35f,24.6f,.6f,10},new[]{22.5f,5.35f,24.6f,.6f,10}};
        // The services: a steam main along the mill's south shell, a water main along the furnace faces, cable trays.
        private void Mains()
        {
            var pipe=Iron(RustDarkHex);var flange=Detail(Iron(IronDarkHex));var lag=P(NativeBlockLooks.ConcreteBoard,0x6A645A,RoleStructure);
            Under("shell-s");RodX(-23.4,35.4,9.0,-33.45,.3,lag);for(double x=-21;x<35;x+=6){RodX(x-.08,x+.08,9.0,-33.45,.38,flange);Span(x-.08,x+.08,8.55,8.62,-34,-33.1,flange);}
            RodX(-23.4,35.4,10.1,-33.65,.12,pipe);RodX(-49.6,-24.6,8.2,-33.5,.22,lag);
            Under("furnace-2");RodX(-23.5,35.5,12.4,27.82,.14,pipe);RodX(-23.5,35.5,12.85,27.82,.1,Iron(GreenDarkHex));for(double x=-22;x<35;x+=4)Span(x-.05,x+.05,12.1,13.0,27.7,28,flange);
            // Cable trays along the mill wall's north face and the time office partitions (under the band, on the wall).
            Under("wall-mill-0");Span(-23.5,35.5,4.6,4.66,-13.7,-13.48,Detail(Iron(SteelHex)));Span(-23.5,35.5,4.66,4.71,-13.66,-13.52,Detail(Iron(CableHex)));
        }
        // ---------------- the Rigger's hang points: chain hoists on their girders (docs section 2.6) ----------------
        /// <summary>The eight hang points (x, girder underside, z, the drop height of its spawn): the crane bridge, the converter
        /// hall's east runway, the trusses (casting floor twice, the mill), the north runway twice, the south runway.</summary>
        public static readonly float[] HangPoints={5.5f,13.2f,1.5f,10.8f, -27.8f,12.4f,-6f,10f, -4f,14.4f,9f,12f, 22f,14.4f,4f,12f, 3.5f,14.4f,-18.4f,12f, 24f,12.4f,20f,10f, -9f,12.4f,20f,10f, 12f,12.4f,-10.2f,10f};
        private void Hangs()
        {
            var clamp=Iron(IronDarkHex,RoleChain);var hoist=Iron(YellowDarkHex,RoleChain);var chain=Iron(ChainHex,RoleChain);
            for(int i=0;i<HangPoints.Length/4;i++)
            {
                float x=HangPoints[i*4],top=HangPoints[i*4+1],z=HangPoints[i*4+2],feet=HangPoints[i*4+3];Own(SpawnAt(x,0,z,.8));Under("roof");Begin();
                // An outrigger from the girder when the point is off its line (the runways' side, the converter runway, a truss
                // line 1 m off), the beam trolley and the chain hoist under it, its hand chain loop and the load chain with a hook.
                double gx=x,gz=z;OutriggerTo(x,top,z,ref gx,ref gz,clamp);
                Span(x-.2,x+.2,top-.16,top,z-.14,z+.14,clamp);Span(x-.17,x+.17,top-.62,top-.18,z-.13,z+.13,hoist);Span(x-.06,x+.06,top-.66,top-.62,z-.06,z+.06,clamp);
                double low=Math.Max(feet+.4,top-2.4);Chain(x,top-.66,low,z,.17,chain,true,i*23);Chain(x+.21,top-.4,top-1.5,z+.04,.11,Detail(chain),false,i*31);
                End();Own(-1);
            }
            // The crane bays: the heavy hooks a Hulk is lowered from (casting floor under the bridge's line, the converter hall).
            foreach(var hb in new[]{new[]{3f,6.5f,12f},new[]{-29.5f,0f,11f}})
            {
                float x=hb[0],z=hb[1],top=hb[2]+1.4f;Own(SpawnAt(x,0,z,.8));Begin();var hk=Iron(YellowHex,RoleChain);
                Span(x-.4,x+.4,top,top+.7,z-.3,z+.3,hk);foreach(double dx in new[]{-.2,.2})Span(x+dx-.02,x+dx+.02,top+.7,top+(x>0?1.8f:1.4f),z-.02,z+.02,Iron(CableHex,RoleChain));
                Span(x-.08,x+.08,top-.5,top,z-.08,z+.08,Iron(IronDarkHex,RoleChain));Span(x-.05,x+.25,top-.72,top-.5,z-.06,z+.06,Iron(IronDarkHex,RoleChain));End();Own(-1);
            }
        }
        // A short outrigger beam from the nearest girder to a hang point that is off the girder's line (moves gx, gz to the point).
        private void OutriggerTo(double x,double top,double z,ref double gx,ref double gz,Pen pen)
        {
            if(top>14)
            {   // A truss line: the nearest one, a beam across at the bottom chord.
                double best=TrussX[0];foreach(float t in TrussX)if(Math.Abs(t-x)<Math.Abs(best-x))best=t;if(Math.Abs(best-x)>.2)Span(Math.Min(best,x)-.08,Math.Max(best,x)+.08,top-.18,top,z-.07,z+.07,pen);gx=best;return;
            }
            if(Math.Abs(z-21)<1.6&&Math.Abs(x)<36){if(Math.Abs(z-21)>.4)Span(x-.07,x+.07,top-.18,top,Math.Min(z,21),Math.Max(z,21),pen);gz=21;return;}
            if(Math.Abs(z+12)<2.2&&Math.Abs(x)<36){if(Math.Abs(z+12)>.4)Span(x-.07,x+.07,top-.18,top,Math.Min(z,-12),Math.Max(z,-12),pen);gz=-12;return;}
            if(Math.Abs(x+26.6)<1.5){if(Math.Abs(x+26.6)>.4)Span(Math.Min(x,-26.6)-.07,Math.Max(x,-26.6)+.07,top-.18,top,z-.07,z+.07,pen);gx=-26.6;}
        }
    }
}
