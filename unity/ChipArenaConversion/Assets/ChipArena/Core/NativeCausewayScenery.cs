using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Starfall Causeway's backdrop (docs/arenas/CAUSEWAY.md "The look"): what hangs round and under the map in the void, box-built
    /// and deterministic, baked once into ChipArena/SourceLit sector meshes (Runtime NativeCausewayPresentation draws them on
    /// <see cref="NativeSceneryBuilder.BackdropLayer"/> without shadows), plus the ringed planet as two smooth meshes of its own:
    /// <list type="bullet">
    /// <item>Near: the towers' masonry stumps under the halls (buttressed, rune-banded, breaking into floating blocks), the bridge's
    /// hanging arches and pendant finials under the keel with their crystal tips, the rubble frozen in its fall under the Breach,
    /// the walkways' lantern chains below them.</item>
    /// <item>Landmark: the causeway's lost spans drifting on past the towers (broken deck lengths with their balustrades, pylons and
    /// lit runes, tilted as they float), a ruined shrine on a floating rock in the north, a broken colossus in the south.</item>
    /// <item>Far: the asteroid belt round the void, distant obelisks, far drifting islands.</item>
    /// <item>The planet: a banded gas giant low in the south-east, its day side to the distant sun, and its rings with the
    /// planet's shadow across them (<see cref="Planet"/>): self-lit through the emission, so the arena's lights never change it.</item>
    /// </list>
    /// The keep-out (<see cref="NativeNewStageScenery.Enters"/>, thrown on while building): nothing stands inside the bounds grown by
    /// 1 m except what lies wholly under the map's lowest block base (the towers' and keel's stone bodies end at y -7). Unity frame,
    /// metres; colours sRGB; allocation only while building.
    /// </summary>
    public static class NativeCausewayScenery
    {
        public const int Near=0,Landmark=1,Far=2,Sectors=3;
        public const int Stone=0xCDBFA8,StoneMid=0xAA9C86,StoneDeep=0x7A6E64,StoneDark=0x5A5458,Metal=0x3A4466,MetalLight=0x8EA0C8,Rune=0xFFB45A,Cold=0x7FE0FF,Violet=0xB48CFF,Rock=0x4A4250,RockDark=0x2E2A36;
        private static double H(int a,int b=0,int c=0)=>NativeCausewayLooks.Hash(a,b,c);

        /// <summary>The backdrop's sectors and the planet's two meshes (null for another map): [near, landmark, far, planet, rings].</summary>
        public static NativeSceneryMesh[] Build(ContentMap map)
        {
            if(!NativeCausewayLooks.Active(map?.id))return null;
            var s=new NativeNewStageScenery.Sink{Map=map.id};NativeNewStageScenery.PlaySpace(map,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);s.B.Anime=true;
            Stumps(s);Underside(s);Lost(s);Belt(s);
            var o=new NativeSceneryMesh[Sectors+2];for(int g=0;g<Sectors;g++)o[g]=s.B.Bake(g);
            Planet(out o[Sectors],out o[Sectors+1]);return o;
        }

        // ---------------- near: the towers' stumps and the bridge's underside ----------------
        private static void Stumps(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;
            foreach(int side in new[]{-1,1})
            {
                double cx=side*33,seed=side>0?7:3;
                // tiers tapering down from the hall's body (its block ends at -7), each a little narrower, rune bands between
                double w=10,d=16,y=-7;
                for(int t=0;t<5;t++)
                {
                    double h=3.2+t*.6,w1=w-1.1,d1=d-1.6;s.Box(cx,y-h/2,0,w,h,d,t%2==0?StoneMid:StoneDeep,shade:1-.06*t,grime:.25);
                    // buttress ribs down each long face and the ends
                    for(int k=-2;k<=2;k++)foreach(int f in new[]{-1,1}){s.Box(cx+k*(w/5),y-h/2,f*(d/2+.25),.7,h,.5,StoneMid,shade:.92);}
                    foreach(int f in new[]{-1,1})for(int k=-3;k<=3;k++)s.Box(cx+f*(w/2+.25),y-h/2,k*(d/7),.5,h,.7,StoneMid,shade:.92);
                    s.Box(cx,y-.15,0,w+.6,.3,d+.6,StoneDark);
                    if(t<4)foreach(int f in new[]{-1,1}){s.Box(cx,y-h*.55,f*(d/2+.52),w*.84,.12,.04,Rune,glow:Rune,level:1.6);s.Box(cx+f*(w/2+.52),y-h*.55,0,.04,.12,d*.84,Rune,glow:Rune,level:1.6);}
                    y-=h;w=w1;d=d1;
                }
                // the broken base: jagged blocks hanging off it, and blocks breaking away below
                for(int i=0;i<14;i++)
                {
                    double bx=cx+(H((int)seed,i,1)-.5)*w,bz=(H((int)seed,i,2)-.5)*d,bh=2+H((int)seed,i,3)*5;s.Box(bx,y-bh/2,bz,1.4+H((int)seed,i,4)*2.4,bh,1.4+H((int)seed,i,5)*2.4,StoneDeep,pitch:(H((int)seed,i,6)-.5)*12,roll:(H((int)seed,i,7)-.5)*12,grime:.3);
                }
                for(int i=0;i<22;i++)
                {
                    double r=H((int)seed+1,i)*9,a=H((int)seed+1,i,1)*2*Math.PI,by=y-6-H((int)seed+1,i,2)*42-i*.8,sz=.8+H((int)seed+1,i,3)*2.6;
                    s.Box(cx+Math.Cos(a)*r,by,Math.Sin(a)*r,sz,sz*(.6+H((int)seed+1,i,4)),sz*(.7+H((int)seed+1,i,5)*.6),i%3==0?StoneMid:StoneDeep,pitch:H((int)seed+1,i,6)*360,yaw:H((int)seed+1,i,7)*360,roll:H((int)seed+1,i,8)*90);
                    if(i%4==0)s.Box(cx+Math.Cos(a)*r,by-sz*.5,Math.Sin(a)*r,.2,.2,.2,Cold,glow:Cold,level:2.2);
                }
                // the crystal heart hung under each tower: a long violet shard and its cradle
                double hy=y-10;s.Box(cx,hy,0,1.2,7,1.2,Violet,yaw:45,glow:Violet,level:1.8);s.Box(cx,hy-4.2,0,.6,2.2,.6,Violet,yaw:45,glow:0xE4D4FF,level:2.6);
                foreach(int f in new[]{-1,1}){s.Box(cx+f*1.8,hy+2.6,0,.3,4,.3,Metal,roll:f*18);s.Box(cx,hy+2.6,f*1.8,.3,4,.3,Metal,pitch:-f*18);}
            }
        }
        private static void Underside(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Near;
            // each half span hangs two inverted arches under its keel (its block ends at -7): voussoir rings to a low crown, the
            // spandrel filled with carved ribs, a pendant finial at the low point with a crystal tip
            foreach(int side in new[]{-1,1})
            {
                double x0=side<0?-20:3,x1=side<0?-3:20;
                foreach(int bay in new[]{0,1})
                {
                    double a0=x0+(x1-x0)*bay/2,a1=x0+(x1-x0)*(bay+1)/2,mid=(a0+a1)/2,half=(a1-a0)/2,sag=6.5;int seg=12;
                    for(int k=0;k<seg;k++)
                    {
                        double t0=(double)k/seg,t1=(double)(k+1)/seg;double X(double t)=>a0+(a1-a0)*t;double Y(double t)=>-7.9-sag*Math.Sin(Math.PI*t);
                        double xa=X(t0),ya=Y(t0),xb=X(t1),yb=Y(t1);double len=Math.Sqrt((xb-xa)*(xb-xa)+(yb-ya)*(yb-ya)),ang=Math.Atan2(yb-ya,xb-xa)*180/Math.PI;
                        s.Box((xa+xb)/2,(ya+yb)/2,0,len+.06,1.1,5.6,k%2==0?StoneMid:Stone,roll:ang,shade:.95);
                        s.Box((xa+xb)/2,(ya+yb)/2-.62,0,len*.9,.14,4.2,Rune,roll:ang,glow:Rune,level:1.3);
                        // spandrel ribs from the arch up to the keel
                        double top=-7.05,hgt=(ya+yb)/2-top;if(hgt<-.4){s.Box((xa+xb)/2,(top+(ya+yb)/2)/2,0,.5,-hgt,5.0,StoneDeep,shade:.9);}
                    }
                    double py=-7.9-sag;s.Box(mid,py-1.4,0,1.2,2.4,1.2,StoneMid,yaw:45);s.Box(mid,py-3.2,0,.7,1.6,.7,StoneMid,yaw:45);s.Box(mid,py-4.6,0,.36,1.6,.36,Cold,yaw:45,glow:Cold,level:2.4);
                    foreach(int f in new[]{-1,1})s.Box(mid,py-.2,f*2.95,half*1.2,.6,.3,Metal,shade:.9);
                }
                // keel buttresses: the stone body's flanks under each wing (the deck wings end at -0.6; nothing here is above -7)
                for(double x=x0+1;x<=x1-1;x+=2.8)foreach(int f in new[]{-1,1})s.Box(x,-8.2,f*3.1,.7,2.4,.6,StoneDeep);
            }
            // under the walkways (their slabs end at -3.6 inside the bounds): lantern chains hung below the play space, each a
            // string of links to a warm lantern, under the outer rail line
            foreach(int side in new[]{-1,1})for(double x=-14;x<=14;x+=4)
            {
                double z=side*8.3,top=-7.05,len=3.2+H(61,(int)x,side)*3.2;
                for(double y=top-.2;y>top-len;y-=.42)s.Box(x,y,z,.08,.32,.08,Metal);
                s.Box(x,top-len-.3,z,.5,.6,.5,0xA07A48);s.Box(x,top-len-.3,z,.36,.44,.36,Rune,glow:Rune,level:2.4);
            }
            // the rubble frozen in its fall below the Breach
            for(int i=0;i<26;i++)
            {
                double x=(H(71,i)-.5)*7,z=(H(71,i,1)-.5)*9,y=-8-H(71,i,2)*36-i*.4,sz=.5+H(71,i,3)*1.8;
                s.Box(x,y,z,sz*1.4,sz*.7,sz,i%3==0?Stone:StoneMid,pitch:H(71,i,4)*360,yaw:H(71,i,5)*360,roll:H(71,i,6)*360);
                if(i%5==0)s.Box(x,y,z,sz*1.45,.06,.06,Rune,pitch:H(71,i,4)*360,yaw:H(71,i,5)*360,roll:H(71,i,6)*360,glow:Rune,level:1.8);
            }
        }

        // ---------------- landmark: the lost spans and the floating ruins ----------------
        /// <summary>A drifting length of the causeway: deck on its keel, coping, balustrades, two pylons with crystal tips, runes;
        /// centred at (x, y, z), `len` long along its own x, turned `yaw` and tipped `pitch`/`roll` (degrees).</summary>
        private static void Fragment(NativeNewStageScenery.Sink s,double x,double y,double z,double len,double yaw,double pitch,double roll,int seed)
        {
            // the group's turn, Unity's Euler (yaw · pitch · roll) as NativeSceneryBuilder applies it to each box
            double a=pitch*Math.PI/180,b=yaw*Math.PI/180,g=roll*Math.PI/180,ca=Math.Cos(a),sa=Math.Sin(a),cb=Math.Cos(b),sb=Math.Sin(b),cc=Math.Cos(g),sc=Math.Sin(g);
            double m00=cb*cc+sb*sa*sc,m01=-cb*sc+sb*sa*cc,m02=sb*ca,m10=ca*sc,m11=ca*cc,m12=-sa,m20=-sb*cc+cb*sa*sc,m21=sb*sc+cb*sa*cc,m22=cb*ca;
            void B(double u,double v,double w,double su,double sv,double sw,int hex,int glow=0,double level=0)
            {s.Box(x+m00*u+m01*v+m02*w,y+m10*u+m11*v+m12*w,z+m20*u+m21*v+m22*w,su,sv,sw,hex,pitch,yaw,roll,glow,level);}
            B(0,-.3,0,len,.6,10,Stone);B(0,-3.6,0,len*.96,6,6.8,StoneMid,0,0);B(0,-6.8,0,len*.7,.5,4.2,Rune,Rune,1.4);
            foreach(int f in new[]{-1,1})
            {
                B(0,.55,f*4.85,len*.92,1.1,.3,StoneMid);
                foreach(double u in new[]{-len*.3,len*.3}){B(u,2.4,f*4.4,1.2,4.8,1.2,Metal);B(u,5.2,f*4.4,.5,1.2,.5,Cold,Cold,2.0);}
                B(0,.02,f*1.6,len*.9,.06,.12,Rune,Rune,1.2);
            }
            // the broken ends: jagged stones off each end
            for(int i=0;i<6;i++){double u=(i<3?-1:1)*(len/2+.4+H(seed,i)*1.2);B(u,-.6-H(seed,i,1)*2.5,(H(seed,i,2)-.5)*8,1.2+H(seed,i,3)*1.6,1+H(seed,i,4)*2,1.2+H(seed,i,5)*1.6,StoneMid);}
        }
        private static void Lost(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Landmark;
            // the causeway once ran on past both towers: lengths of it drift away along its old line, rising and tipping
            Fragment(s,-66,-6,3,22,4,0,-5,201);Fragment(s,-104,-14,-12,18,-11,0,9,202);Fragment(s,-150,-4,20,24,16,0,-14,203);Fragment(s,-210,-30,-30,20,-24,0,20,204);
            Fragment(s,66,-9,-4,20,-6,0,6,205);Fragment(s,108,2,10,18,9,0,-11,206);Fragment(s,158,-22,-24,22,-18,0,15,207);Fragment(s,224,-8,36,18,28,0,-22,208);
            // the north shrine on its floating rock: a ring of columns under a broken dome, a lit altar
            Island(s,40,-26,150,26,301);Shrine(s,40,-12,150);
            // the south colossus: a kneeling guardian's broken torso and its sword, adrift on a rock
            Island(s,-70,-34,-140,22,302);Colossus(s,-70,-20,-140);
            // smaller islands with single ruins
            Island(s,-150,10,90,12,303);Column(s,-150,18,90,9);Island(s,170,-40,110,14,304);Column(s,170,-28,110,12);Island(s,120,26,-120,10,305);
        }
        private static void Island(NativeNewStageScenery.Sink s,double x,double y,double z,double r,int seed)
        {
            // a floating rock: a flat grassy-grey top, a long tapering underside of stacked turned slabs, crystals in its flanks
            s.Box(x,y,z,r*2,1.4,r*1.7,Rock,yaw:H(seed)*90,shade:1.05);s.Box(x,y,z,r*1.8,1.3,r*1.9,Rock,yaw:H(seed)*90+40,shade:.98);
            double yy=y-.6;double rr=r;
            for(int k=0;k<7;k++){double h=2+k*.8;rr*=.78;s.Box(x+(H(seed,k)-.5)*2,yy-h/2,z+(H(seed,k,1)-.5)*2,rr*1.8,h,rr*1.6,k%2==0?Rock:RockDark,yaw:H(seed,k,2)*180,shade:.95-.05*k);yy-=h*.9;}
            for(int k=0;k<6;k++){double a=H(seed,k,3)*2*Math.PI,d=r*.6;s.Box(x+Math.Cos(a)*d,y-2-H(seed,k,4)*6,z+Math.Sin(a)*d,.6,2.6,.6,k%2==0?Cold:Violet,pitch:(H(seed,k,5)-.5)*60,yaw:a*57,glow:k%2==0?Cold:Violet,level:2.0);}
        }
        private static void Shrine(NativeNewStageScenery.Sink s,double x,double y,double z)
        {
            s.Box(x,y+.4,z,22,.8,22,Stone);s.Box(x,y+.9,z,18,.4,18,StoneMid);
            for(int k=0;k<10;k++){double a=k*Math.PI/5,cx=x+Math.Cos(a)*7.5,cz=z+Math.Sin(a)*7.5;double h=k%3==1?5+H(311,k)*3:11;s.Box(cx,y+1.1+h/2,cz,1.1,h,1.1,Stone);s.Box(cx,y+1.3,cz,1.6,.4,1.6,StoneMid);if(h>10)s.Box(cx,y+1.1+h+.3,cz,1.5,.6,1.5,StoneMid);}
            for(int k=0;k<10;k++){if(k%3==1)continue;double a=(k+.5)*Math.PI/5,cx=x+Math.Cos(a)*7.5,cz=z+Math.Sin(a)*7.5;s.Box(cx,y+12.6,cz,4.8,.9,1.2,StoneMid,yaw:-a*180/Math.PI+90);}
            // the broken dome: three ribs to a ring
            for(int k=0;k<3;k++){double a=k*2*Math.PI/3+.4;NativeNewStageScenery.Bar(s,x+Math.Cos(a)*7.2,y+13,z+Math.Sin(a)*7.2,x+Math.Cos(a)*1.8,y+18.5,z+Math.Sin(a)*1.8,.7,StoneMid);}
            NativeNewStageScenery.Octagon(s,x,y+18.2,y+19,z,2.2,Stone);
            s.Box(x,y+2.1,z,2.4,2,1.4,StoneMid);s.Box(x,y+3.2,z,2.6,.2,1.6,Rune,glow:Rune,level:2.0);s.Box(x,y+4.6,z,.8,2.4,.8,Cold,yaw:45,glow:Cold,level:2.6);
        }
        private static void Colossus(NativeNewStageScenery.Sink s,double x,double y,double z)
        {
            // kneeling, facing the bridge; the head fallen beside it; the sword planted, its edge lit
            s.Box(x,y+3,z,9,6,6,Stone,yaw:20);s.Box(x+.5,y+10,z,7,8,4.4,Stone,yaw:20,roll:-6);s.Box(x+1,y+15,z,10,2.6,4.8,StoneMid,yaw:20,roll:-6);
            s.Box(x-4.6,y+9,z+1.6,2.2,9,2.2,Stone,yaw:20,roll:14);s.Box(x+6,y+8,z-2,2.2,8,2.2,Stone,yaw:20,roll:-24);
            s.Box(x+9,y+1.2,z+6,4.4,4.4,4.4,StoneMid,pitch:20,yaw:50,roll:30);s.Box(x+9,y+1.6,z+6,1.4,.3,4.6,Cold,pitch:20,yaw:50,roll:30,glow:Cold,level:2.2);
            s.Box(x-8,y+9,z-3,1.0,20,.3,MetalLight,yaw:20,roll:6);s.Box(x-8.06,y+9,z-3,.06,19,.34,Cold,yaw:20,roll:6,glow:Cold,level:2.4);s.Box(x-8.6,y+19.6,z-3.2,4.2,.8,.8,Metal,yaw:20,roll:6);
            for(int k=0;k<4;k++)s.Box(x+2,y+6+k*2.4,z+2.25,4.8,.12,.06,Rune,yaw:20,glow:Rune,level:1.6);
        }
        private static void Column(NativeNewStageScenery.Sink s,double x,double y,double z,double h)
        {s.Box(x,y+h/2,z,1.6,h,1.6,Stone);s.Box(x,y+.3,z,2.4,.6,2.4,StoneMid);s.Box(x,y+h+.3,z,2.2,.6,2.2,StoneMid);s.Box(x,y+h+1.4,z,.6,1.6,.6,Cold,yaw:45,glow:Cold,level:2.4);}

        // ---------------- far: the asteroid belt and the far islands ----------------
        private static void Belt(NativeNewStageScenery.Sink s)
        {
            s.B.Current=Far;
            // a broad, uneven ring of tumbling rock round the void, tilted: dense in a band, sparse above and below
            for(int i=0;i<170;i++)
            {
                double a=H(401,i)*2*Math.PI,r=340+Math.Pow(H(401,i,1),.8)*520,tilt=Math.Sin(a*2+.6)*.12,y=-60+Math.Sin(a)*r*tilt+(H(401,i,2)-.5)*70;
                double sz=2.5+Math.Pow(H(401,i,3),3)*30;double x=Math.Cos(a)*r,z=Math.Sin(a)*r;
                int hex=NativeNewStageScenery.Mix(0x2E2A36,0x564A54,H(401,i,4));
                s.Box(x,y,z,sz,sz*(.55+H(401,i,5)*.5),sz*(.6+H(401,i,6)*.6),hex,pitch:H(401,i,7)*360,yaw:H(401,i,8)*360,roll:H(401,i,9)*360,shade:.75+.2*H(401,i,10));
                s.Box(x+sz*.12,y-sz*.08,z,sz*.74,sz*.62,sz*.86,NativeNewStageScenery.Mix(hex,RockDark,.35),pitch:H(401,i,11)*360,yaw:H(401,i,12)*360,roll:H(401,i,14)*360,shade:.8);
                if(sz>9)s.Box(x-sz*.2,y+sz*.12,z+sz*.1,sz*.5,sz*.45,sz*.55,hex,pitch:H(401,i,15)*360,yaw:H(401,i,16)*360,shade:.7);
                if(H(401,i,13)<.05)s.Box(x,y+sz*.3,z,sz*.12,sz*.4,sz*.12,Cold,yaw:a*57,glow:Cold,level:2.0);
            }
            // distant obelisks and drifting islands
            for(int i=0;i<12;i++)
            {
                double a=i*2*Math.PI/12+H(411,i)*.4,r=620+H(411,i,1)*240,x=Math.Cos(a)*r,z=Math.Sin(a)*r,y=-40+(H(411,i,2)-.5)*160;
                Island(s,x,y,z,18+H(411,i,3)*16,420+i);if(i%2==0){s.Box(x,y+18,z,4,34,4,Stone,yaw:H(411,i,4)*90);s.Box(x,y+36,z,1.6,3.6,1.6,i%4==0?Violet:Cold,yaw:45,glow:i%4==0?Violet:Cold,level:2.6);}
            }
        }

        // ---------------- the planet and its rings ----------------
        /// <summary>The planet's sphere and its rings as smooth meshes (positions, smooth normals, near-black albedo, the shading in
        /// the emission): bands by latitude, lit toward <see cref="NativeCausewayLooks.SunX"/>, a soft terminator, a thin night-side
        /// glow; the rings in banded tones with the Cassini gap and the planet's shadow across them.</summary>
        public static void Planet(out NativeSceneryMesh sphere,out NativeSceneryMesh rings)
        {
            double cx=NativeCausewayLooks.PlanetX,cy=NativeCausewayLooks.PlanetY,cz=NativeCausewayLooks.PlanetZ,R=NativeCausewayLooks.PlanetR;
            double lx=NativeCausewayLooks.SunX,ly=NativeCausewayLooks.SunY,lz=NativeCausewayLooks.SunZ,ll=Math.Sqrt(lx*lx+ly*ly+lz*lz);lx/=ll;ly/=ll;lz/=ll;
            // the planet's pole: tipped like its rings
            double tilt=NativeCausewayLooks.RingTilt,turn=NativeCausewayLooks.RingTurn;
            void Pole(double x,double y,double z,out double ox,out double oy,out double oz){double y1=y*Math.Cos(tilt)-z*Math.Sin(tilt),z1=y*Math.Sin(tilt)+z*Math.Cos(tilt);ox=x*Math.Cos(turn)+z1*Math.Sin(turn);oy=y1;oz=-x*Math.Sin(turn)+z1*Math.Cos(turn);}
            int[] band={0xE8D2A8,0xD8A868,0xF0E2C0,0xB87848,0xE4C890,0xA8683C,0xF2E6CC,0xC89058,0xDCC498,0x8A6A58,0xE6D4AE};
            int lon=72,lat=40;var p=new List<float>();var n=new List<float>();var c=new List<float>();var e=new List<float>();var idx=new List<int>();
            for(int j=0;j<=lat;j++)for(int i=0;i<=lon;i++)
            {
                double th=Math.PI*j/lat,ph=2*Math.PI*i/lon;double ux=Math.Sin(th)*Math.Cos(ph),uy=Math.Cos(th),uz=Math.Sin(th)*Math.Sin(ph);Pole(ux,uy,uz,out double wx,out double wy,out double wz);
                p.Add((float)(cx+wx*R));p.Add((float)(cy+wy*R));p.Add((float)(cz+wz*R));n.Add((float)wx);n.Add((float)wy);n.Add((float)wz);
                // bands by latitude with a wavy edge, a storm spot
                double v=uy*5.2+Math.Sin(ph*3+uy*7)*.18+Math.Sin(ph*7.3)*.05;int b0=(int)Math.Floor(v+5.5);double f=v+5.5-b0;b0=Math.Max(0,Math.Min(band.Length-2,b0));
                int hex=NativeNewStageScenery.Mix(band[b0],band[b0+1],f*f*(3-2*f));double spot=Math.Exp(-((uy+.32)*(uy+.32)*90+Math.Pow(Math.Sin((ph-1.1)/2),2)*60));hex=NativeNewStageScenery.Mix(hex,0xC0502E,spot*.8);
                double dot=wx*lx+wy*ly+wz*lz,day=Math.Max(0,Math.Min(1,(dot+.12)/.5));day=day*day*(3-2*day);
                double r0=NativeSceneryBuilder.Channel(hex,16),g0=NativeSceneryBuilder.Channel(hex,8),b1=NativeSceneryBuilder.Channel(hex,0),k=.04+.86*day;
                double night=(1-day)*.05;c.Add(.01f);c.Add(.01f);c.Add(.012f);c.Add(1);e.Add((float)(r0*k+night*.3));e.Add((float)(g0*k+night*.35));e.Add((float)(b1*k+night*.9));e.Add(0);
            }
            for(int j=0;j<lat;j++)for(int i=0;i<lon;i++){int a=j*(lon+1)+i,b=a+1,d=a+lon+1,g=d+1;idx.Add(a);idx.Add(b);idx.Add(g);idx.Add(a);idx.Add(g);idx.Add(d);}
            sphere=Mesh(p,n,c,e,idx);
            // the rings: an annulus in the planet's equator plane, both faces, bands by radius, the shadow where the sun's ray to a
            // point passes through the planet
            p=new List<float>();n=new List<float>();c=new List<float>();e=new List<float>();idx=new List<int>();
            int seg=160,rad=24;double rin=NativeCausewayLooks.RingIn,rout=NativeCausewayLooks.RingOut;
            Pole(0,1,0,out double nx,out double ny,out double nz);
            for(int face=0;face<2;face++)
            {
                int start=p.Count/3;
                for(int j=0;j<=rad;j++)for(int i=0;i<=seg;i++)
                {
                    double r=rin+(rout-rin)*j/rad,ph=2*Math.PI*i/seg;Pole(Math.Cos(ph)*r,0,Math.Sin(ph)*r,out double wx,out double wy,out double wz);
                    p.Add((float)(cx+wx));p.Add((float)(cy+wy));p.Add((float)(cz+wz));float s=face==0?1:-1;n.Add((float)(nx*s));n.Add((float)(ny*s));n.Add((float)(nz*s));
                    double q=(r-rin)/(rout-rin);double bright=.55+.35*Math.Sin(q*31)+.2*Math.Sin(q*83+1);if(q>.58&&q<.64)bright*=.08;if(q<.08)bright*=q/.08;if(q>.94)bright*=(1-q)/.06;bright=Math.Max(0,bright);
                    // the planet's shadow: does the ray from the point toward the sun pass within R of the centre?
                    double px=wx,py=wy,pz=wz,t=-(px*lx+py*ly+pz*lz),dx=px+lx*t,dy=py+ly*t,dz=pz+lz*t,dist=Math.Sqrt(dx*dx+dy*dy+dz*dz);double lit=t>0&&dist<R?Math.Max(.06,Math.Min(1,(dist-R*.94)/(R*.06))):1;
                    int hex=NativeNewStageScenery.Mix(0xD8C4A0,0xA88A6A,.5+.5*Math.Sin(q*17));double k=bright*lit*.75;
                    c.Add(.01f);c.Add(.01f);c.Add(.012f);c.Add(1);e.Add((float)(NativeSceneryBuilder.Channel(hex,16)*k));e.Add((float)(NativeSceneryBuilder.Channel(hex,8)*k));e.Add((float)(NativeSceneryBuilder.Channel(hex,0)*k));e.Add(0);
                }
                for(int j=0;j<rad;j++)for(int i=0;i<seg;i++){int a=start+j*(seg+1)+i,b=a+1,d=a+seg+1,g=d+1;if(face==0){idx.Add(a);idx.Add(g);idx.Add(b);idx.Add(a);idx.Add(d);idx.Add(g);}else{idx.Add(a);idx.Add(b);idx.Add(g);idx.Add(a);idx.Add(g);idx.Add(d);}}
            }
            rings=Mesh(p,n,c,e,idx);
        }
        private static NativeSceneryMesh Mesh(List<float> p,List<float> n,List<float> c,List<float> e,List<int> idx)
        {
            var m=new NativeSceneryMesh{Positions=p.ToArray(),Normals=n.ToArray(),Colors=c.ToArray(),Emission=e.ToArray(),Indices=idx.ToArray()};
            m.MinX=m.MinY=m.MinZ=float.MaxValue;m.MaxX=m.MaxY=m.MaxZ=float.MinValue;
            for(int i=0;i<m.Positions.Length;i+=3){m.MinX=Math.Min(m.MinX,m.Positions[i]);m.MaxX=Math.Max(m.MaxX,m.Positions[i]);m.MinY=Math.Min(m.MinY,m.Positions[i+1]);m.MaxY=Math.Max(m.MaxY,m.Positions[i+1]);m.MinZ=Math.Min(m.MinZ,m.Positions[i+2]);m.MaxZ=Math.Max(m.MaxZ,m.Positions[i+2]);}
            return m;
        }
    }
}
