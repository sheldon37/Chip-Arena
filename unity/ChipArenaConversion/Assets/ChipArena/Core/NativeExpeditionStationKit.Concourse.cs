using System;

namespace ChipArena.UnityConversion
{
    // The concourse, the ticket hall and the upper floor (docs/EXPEDITION-CENTRAL-STATION.md section 3).
    public sealed partial class NativeExpeditionStationKit
    {
        /// <summary>The vault over the concourse: arched ribs every <see cref="RibStep"/> m from x -40 to 40, springing at
        /// <see cref="VaultSpring"/> over the screen (z <see cref="VaultSouth"/>) and the clerestory (<see cref="VaultNorth"/>),
        /// <see cref="VaultRise"/> m to the crown; the roof beams (blocks) are its ties.</summary>
        public const float VaultSouth=-4.2f,VaultNorth=20.2f,VaultSpring=18.8f,VaultRise=3.0f,RibStep=8f;public const int VaultSegments=12;
        public static float VaultY(double z)=>(float)(VaultSpring+VaultRise*Math.Sin(Math.PI*Math.Max(0,Math.Min(1,(z-VaultSouth)/(VaultNorth-VaultSouth)))));
        /// <summary>The moon's travel (the draft look's key: elevation 62, bearing 205), unit.</summary>
        public static readonly double[] MoonTravel=MoonDir();
        private static double[] MoonDir(){NativeMapLooks.Toward(62,205,out double x,out double y,out double z);return new[]{-x,-y,-z};}
        /// <summary>A colour's terrazzo inlay shade: two thirds of it, toward grey.</summary>
        private static int Dim(int hex){int r=(hex>>16)&255,g=(hex>>8)&255,b=hex&255,m=(r+g+b)/3;r=(r+m)/2*3/4;g=(g+m)/2*3/4;b=(b+m)/2*3/4;return r<<16|g<<8|b;}
        /// <summary>The departure board's box (centre and half extents).</summary>
        public const float BoardX=0,BoardY=8.6f,BoardZ=11,BoardHx=5.2f,BoardHy=2.2f,BoardHz=2.2f;

        // A world point moved along the moon's travel down to height y.
        private static void MoonTo(double x,double y,double z,double toY,out double ox,out double oz){double t=(y-toY)/-MoonTravel[1];ox=x+MoonTravel[0]*t;oz=z+MoonTravel[2]*t;}
        private int NearestBeam(double z){int best=-1;double d=double.MaxValue;for(int k=0;k<4;k++){int i=B("roof-beam-"+k);if(i<0)continue;double e=Math.Abs(blocks[i].pos.z-z);if(e<d){d=e;best=i;}}return best;}
        private int Side(string stemA,string stemB,int sign){int a=B(stemA),b=B(stemB);if(a>=0&&Math.Sign(blocks[a].pos.x)==sign)return a;if(b>=0&&Math.Sign(blocks[b].pos.x)==sign)return b;return -1;}

        // ---------------- the concourse ----------------
        private void Concourse()
        {
            int floor=B("floor-north");if(floor<0)return;
            Vault();Screen();Clerestory();Board();Booth();Kiosks();Balconies();ConcourseFloor(floor);ConcourseFurniture(floor);
        }
        private void Vault()
        {
            if(NearestBeam(8)<0)return;var iron=With(L("steel-green",IronHex,RoleHigh),shadow:true);var ironThin=L("steel-green",0x1E2C26,RoleHigh);
            double zs=VaultSouth,zn=VaultNorth;int n=VaultSegments;
            // Ribs: arched, in straight chords, 0.28 wide and 0.5 deep under the glazing; the springing brackets.
            for(double x=-40;x<=40.01;x+=RibStep)
            {
                for(int k=0;k<n;k++)
                {
                    double z0=zs+(zn-zs)*k/n,z1=zs+(zn-zs)*(k+1)/n,y0=VaultY(z0),y1=VaultY(z1),len=Math.Sqrt((z1-z0)*(z1-z0)+(y1-y0)*(y1-y0)),pitch=-Math.Atan2(y1-y0,z1-z0)*180/Math.PI;
                    Box(NearestBeam((z0+z1)/2),x,(y0+y1)/2-.27,(z0+z1)/2,.28,.5,len+.06,iron,0,pitch);
                }
                // King posts from each tie (the roof beams' tops at 18.8) up to the rib; diagonals between them (detail).
                for(int b=0;b<4;b++){int beam=B("roof-beam-"+b);if(beam<0)continue;double bz=blocks[beam].pos.z,top=VaultY(bz)-.5;if(top-18.8<.1)continue;Box(beam,x,(18.8+top)/2,bz,.22,top-18.8,.22,iron);}
                for(int b=0;b<3;b++)
                {
                    int ba=B("roof-beam-"+b),bb=B("roof-beam-"+(b+1));if(ba<0||bb<0)continue;double za=blocks[ba].pos.z,zb=blocks[bb].pos.z,ya=18.8,yb=VaultY(zb)-.5;
                    double len=Math.Sqrt((zb-za)*(zb-za)+(yb-ya)*(yb-ya)),pitch=-Math.Atan2(yb-ya,zb-za)*180/Math.PI;Box(ba,x,(ya+yb)/2,(za+zb)/2,.12,.14,len,With(ironThin,detail:true),0,pitch);
                }
            }
            // Purlins along x at every chord joint; the springing gutters over the screen and the clerestory.
            for(int k=1;k<n;k++){double z=zs+(zn-zs)*k/n;Box(NearestBeam(z),0,VaultY(z)-.1,z,80.3,.16,.16,ironThin);}
            int screen=B("screen");if(screen>=0)Span(screen,-40.4,40.4,18.0,18.85,-4.65,-3.75,iron);
            int roof=B("upper-roof");if(roof>=0)Span(roof,-40.4,40.4,18.0,18.85,19.8,20.65,iron);
            // Glazing: a pane per bay and chord, faintly lit by the moon behind it, each its own shade; about a fifth are gone
            // (a shard or two left hanging; the moon falls through some of them), and the panes over the Hulk drops are out.
            var holes=new System.Collections.Generic.List<double[]>();
            for(double x=-40;x<40;x+=RibStep)
                for(int k=0;k<n;k++)
                {
                    double z0=zs+(zn-zs)*k/n,z1=zs+(zn-zs)*(k+1)/n,y0=VaultY(z0),y1=VaultY(z1),len=Math.Sqrt((z1-z0)*(z1-z0)+(y1-y0)*(y1-y0)),pitch=-Math.Atan2(y1-y0,z1-z0)*180/Math.PI;
                    double cx=x+RibStep/2,cy=(y0+y1)/2,cz=(z0+z1)/2,h=Hash("vault",(int)x,k);int beam=NearestBeam(cz);
                    bool overDrop=false;foreach(var kp in keeps)if(kp.Kind==1&&kp.Y1>15&&cx+4>kp.X0&&cx-4<kp.X1&&cz+1>kp.Z0&&cz-1<kp.Z1)overDrop=true;
                    if(h<.19||overDrop)
                    {
                        holes.Add(new[]{cx,cy,cz,h});
                        if(h<.12)Box(beam,cx-2.2+h*20,cy-.35,cz,.9+h*4,.025,.7,L("plain",0x2A3A44,RoleHigh),20+h*90,pitch+28,8);
                        continue;
                    }
                    var pane=Glow(h<.45?0x101C26:0x16242E,.55f+(float)h*.35f,RoleHigh);Box(beam,cx,cy+.03,cz,RibStep-.3,.03,len-.04,pane,0,pitch);
                    Box(beam,cx,cy-.02,cz,.06,.08,len,With(ironThin,detail:true),0,pitch);
                }
            // Moon shafts through the gaps whose light lands on the concourse floor clear of the board (the brightest eight),
            // and a puddle where each lands (the rain comes in there too).
            holes.Sort((a,b)=>a[3].CompareTo(b[3]));int shafts=0;
            foreach(var hh in holes)
            {
                if(shafts>=8)break;MoonTo(hh[0],hh[1],hh[2],.05,out double fx,out double fz);
                if(fz<-3||fz>19.5||Math.Abs(fx)>38.5||Math.Abs(fx-BoardX)<BoardHx+1.5&&fz>BoardZ-BoardHz-4&&fz<BoardZ+BoardHz+1)continue;
                Shaft(hh[0],hh[1]-.4,hh[2],fx,.05,fz,1.4,3.0,.9,ShaftMoon);shafts++;
                int fl=B("floor-north");if(fl>=0)Puddle(fl,fx,fz,.9+hh[3]*4,hh[3]);
            }
        }
        // The glazed screen between the concourse and the shed: mullions, transoms and panes on both faces, a few gone black.
        private void Screen()
        {
            int s=B("screen");if(s<0)return;var b=blocks[s];double zN=b.pos.z+b.size.z/2,zS=b.pos.z-b.size.z/2,y0=b.pos.y-b.size.y/2,y1=b.pos.y+b.size.y/2;
            var iron=L("steel-green",IronHex,RoleHigh);
            foreach(var (face,sg) in new[]{(zN,1.0),(zS,-1.0)})
            {
                for(int k=-10;k<=10;k++)Box(s,k*4.0,(y0+y1)/2,face+sg*.06,.16,y1-y0,.12,iron);
                foreach(double y in new[]{y0+.08,y0+2.7,y0+5.3,y1-.08})Box(s,0,y,face+sg*.06,80,.14,.12,iron);
                for(int k=-10;k<10;k++)for(int row=0;row<3;row++)
                {
                    double h=Hash("screen",k*3+row,sg>0?1:2),ya=y0+.15+row*2.6,yb=ya+2.45;
                    Box(s,k*4.0+2,(ya+yb)/2,face+sg*.012,3.82,yb-ya,.02,h<.22?L("plain",BlackHex,RoleHigh):Glow(h<.5?0x0A1218:h<.85?0x0E161C:0x1E2C38,.14f+(float)h*.12f,RoleHigh));
                    if(h>=.22&&h<.3)Box(s,k*4.0+2+(h-.26)*30,(ya+yb)/2-.4,face+sg*.03,1.2,.9,.02,L("plain",BlackHex,RoleHigh),0,0,(h-.26)*900);
                }
            }
            // CENTRAL: the facade's teal sign inside, on a dark green fascia under the screen; a smaller one faces the platforms.
            var fascia=With(L("steel-green",0x1A2A26,RoleSign),shadow:true);Span(s,-10.2,10.2,8.15,y0+.02,zS-.04,zN+.04,fascia);
            Text(s,"CENTRAL",0,9.1,zN+.09,0,.3,.06,Glow(TealHex,1.7f,RoleSign,DriveFlicker,.83f));
            Text(s,"CENTRAL",0,9.1,zS-.09,180,.18,.05,Glow(TealHex,1.4f,RoleSign));
            Light(0,8.6,zN+1.6,TealHex,.9,14,1.6,GlowSign);
            // The 6:47 clock hanging under the fascia, a face to each side.
            Clock(s,0,7.0,b.pos.z,.62,true);
        }
        /// <summary>A station clock (dial, ring, hour marks, hands at 6:47) at (x, y, z), facing +z (and -z when `both`).</summary>
        private void Clock(int parent,double x,double y,double z,double r,bool both,double yaw=0)
        {
            var ring=With(L("steel-green",0x24342E,RoleSign),fade:true);Box(parent,x,y,z,r*2+.16,r*2+.16,both?.2:.1,ring,yaw);
            foreach(double sg in both?new[]{1.0,-1.0}:new[]{1.0})
            {
                double a=(yaw+(sg<0?180:0))*Math.PI/180,nx=Math.Sin(a),nz=Math.Cos(a),rx=-nz,rz=nx,f=both?.11:.06;double fyaw=yaw+(sg<0?180:0);
                Box(parent,x+nx*f,y,z+nz*f,r*2,r*2,.02,Glow(0xF0E8D0,.32f,RoleSign),fyaw);
                for(int k=0;k<12;k++){double t=k*Math.PI/6;Box(parent,x+nx*(f+.02)+rx*Math.Sin(t)*r*.84,y+Math.Cos(t)*r*.84,z+nz*(f+.02)+rz*Math.Sin(t)*r*.84,.05,k%3==0?r*.22:r*.12,.01,With(L("plain",0x141210,RoleSign),detail:k%3!=0),fyaw,0,k*30);}
                foreach(var (deg,len,w) in new[]{(203.5,.55,.07),(282.0,.82,.04)}){double t=deg*Math.PI/180,l=r*len;Box(parent,x+nx*(f+.03)+rx*Math.Sin(t)*l/2,y+Math.Cos(t)*l/2,z+nz*(f+.03)+rz*Math.Sin(t)*l/2,w,l,.012,L("plain",0x101010,RoleSign),fyaw,0,deg);}
            }
        }
        // The north clerestory over the gallery: a brick wall up to the vault with tall arched windows and the great clock; the
        // vault's ends (lunettes) over the side walls.
        private void Clerestory()
        {
            int roof=B("upper-roof");if(roof<0)return;var brick=With(L("brick-buff",0x7A5E4A,RoleHigh),shadow:true);double z=VaultNorth;
            Span(roof,-40,40,10.5,18.0,z-.2,z+.3,brick);
            for(int k=0;k<8;k++)
            {
                double x=-35+k*10;if(Math.Abs(x)<6)continue;double h=Hash("clere",k);
                Box(roof,x,13.6,z-.22,2.4,4.6,.04,h<.3?L("plain",BlackHex,RoleHigh):Glow(0x101A22,.6f,RoleHigh));
                for(int j=0;j<4;j++){double w=2.4-j*.55;Box(roof,x,16.0+j*.14,z-.22,w,.14,.04,Glow(0x101A22,.6f,RoleHigh));}
                Box(roof,x,13.9,z-.26,.08,5.2,.06,L("steel-green",IronHex,RoleHigh));Box(roof,x,13.0,z-.26,2.4,.08,.06,L("steel-green",IronHex,RoleHigh));
                Box(roof,x,11.2,z-.28,2.8,.18,.14,L("concrete-board",0x9A9484,RoleHigh));
            }
            Clock(roof,0,14.4,z-.15,1.15,false,180);
            // The lunettes: the side walls carried up under the vault's ends.
            foreach(int sg in new[]{-1,1})
            {
                int wall=Side("mass-cw","mass-ce",sg);if(wall<0)continue;double x=sg*40.5;
                for(int k=0;k<VaultSegments;k++){double z0=VaultSouth+(VaultNorth-VaultSouth)*k/VaultSegments,z1=VaultSouth+(VaultNorth-VaultSouth)*(k+1)/VaultSegments,top=Math.Min(VaultY(z0),VaultY(z1));if(top<=20.05)continue;Span(wall,x-.5,x+.5,19.95,top,z0,z1,brick);}
            }
        }

        // ---------------- the departure board ----------------
        // Four faces over the booth: north and south DEPARTURES, five rows in columns (time, destination, platform, status) and
        // the paged ticker; east and west the 6:47 clock over PLATFORM 2 / EVAC SPECIAL / DOORS OPEN. Rows 0 and 4 and the title
        // carry every variant the game shows (one lit at a time, <see cref="Drive"/>).
        private const int ColTime=0,ColTo=6,ColPlatform=19,ColStatus=21,Columns=34;
        private void Board()
        {
            int booth=B("booth");if(booth<0)return;double bx=BoardX,by=BoardY,bz=BoardZ;
            // The body plain: the rusted steel read as camouflage from below under the board's own light.
            var body=With(L("plain",0x161E1C,RoleBoard),shadow:true);Box(booth,bx,by,bz,BoardHx*2,BoardHy*2,BoardHz*2,body);
            // The hangers: two to the tie over the board, two raked back to the next.
            int tie=NearestBeam(9),back=NearestBeam(15);var rod=With(L("steel-green",0x2A3430,RoleBoard),fade:true);
            if(tie>=0)foreach(double sx in new[]{-3.6,3.6}){double z0=blocks[tie].pos.z,y0=by+BoardHy,y1=blocks[tie].pos.y-.4;Box(tie,bx+sx,(y0+y1)/2,z0,.07,y1-y0,.07,rod);}
            if(back>=0)foreach(double sx in new[]{-3.6,3.6}){double za=bz+BoardHz-.3,ya=by+BoardHy,zb=blocks[back].pos.z,yb=blocks[back].pos.y-.4,len=Math.Sqrt((zb-za)*(zb-za)+(yb-ya)*(yb-ya));Box(back,bx+sx,(ya+yb)/2,(za+zb)/2,.07,.07,len,rod,0,-Math.Atan2(yb-ya,zb-za)*180/Math.PI);}
            foreach(double yaw in new[]{0.0,180.0})BoardFace(booth,yaw);
            foreach(double yaw in new[]{90.0,270.0})BoardSide(booth,yaw);
            Light(bx,by-BoardHy-.4,bz,AmberHex,1.1,15,2.4,GlowBoard,0,0);
        }
        // A point `u` along a board face (to the viewer's right) and `d` out of it.
        private static void FaceAt(double yaw,double cx,double cz,double half,double u,double d,out double x,out double z){double a=yaw*Math.PI/180,nx=Math.Sin(a),nz=Math.Cos(a);x=cx+nx*(half+d)+(-nz)*u;z=cz+nz*(half+d)+nx*u;}
        private void BoardFace(int booth,double yaw)
        {
            double half=Math.Abs(Math.Cos(yaw*Math.PI/180))>.5?BoardHz:BoardHx,width=BoardHx*2;
            FaceAt(yaw,BoardX,BoardZ,half,0,.012,out double px,out double pz);
            var panel=With(L("cyber-holo",0x2A1606,RoleBoard),drive:DriveBoard);panel.Gain=.55f;Box(booth,px,BoardY,pz,width-.4,BoardHy*2-.36,.02,panel,yaw);
            var frame=L("steel-green",0x2A3430,RoleBoard);
            FaceAt(yaw,BoardX,BoardZ,half,0,.03,out double fx,out double fz);
            Box(booth,fx,BoardY+BoardHy-.09,fz,width,.18,.06,frame,yaw);Box(booth,fx,BoardY-BoardHy+.09,fz,width,.18,.06,frame,yaw);
            foreach(double u in new[]{-(width/2-.09),width/2-.09}){FaceAt(yaw,BoardX,BoardZ,half,u,.03,out double ux,out double uz);Box(booth,ux,BoardY,uz,.18,BoardHy*2,.06,frame,yaw);}
            double d=.04;var amber=Glow(AmberHex,1.9f,RoleBoard,DriveBoard);var cream=Glow(0xFFE2A8,2.2f,RoleBoard,DriveBoard);var red=Glow(0xFF6A3A,2.0f,RoleBoard,DriveBoard);var white=Glow(0xFFF2D0,2.8f,RoleBoard,DriveBoard);
            // Title (both variants), the header rule.
            BoardText(booth,yaw,half,NativeExpeditionStation.BoardTitle,0,BoardY+1.62,.095,d,With(cream,tag:(short)(5*16+NativeStationState.TitleDepartures)),0);
            BoardText(booth,yaw,half,NativeExpeditionStation.BoardCancelled,0,BoardY+1.62,.095,d,With(red,tag:(short)(5*16+NativeStationState.TitleCancelled)),0);
            FaceAt(yaw,BoardX,BoardZ,half,0,d,out double rx,out double rz);Box(booth,rx,BoardY+1.2,rz,width-1.0,.03,.02,With(amber,tag:-1),yaw);
            double c=.066,left=-(Columns*4*c-c)/2;
            for(int i=0;i<5;i++)
            {
                double y=BoardY+.86-i*.5;
                BoardText(booth,yaw,half,NativeExpeditionStation.RowTime[i],left+ColTime*4*c,y,c,d,With(amber,tag:-1),-1);
                BoardText(booth,yaw,half,NativeExpeditionStation.RowTo[i],left+ColTo*4*c,y,c,d,With(amber,tag:-1),-1);
                BoardText(booth,yaw,half,NativeExpeditionStation.RowPlatform[i],left+ColPlatform*4*c,y,c,d,With(amber,tag:-1),-1);
                if(i==0)for(int v=0;v<NativeExpeditionStation.Row0Status.Length;v++)BoardText(booth,yaw,half,NativeExpeditionStation.Row0Status[v],left+ColStatus*4*c,y,c,d,With(v==NativeStationState.Row0LastCall?white:v==NativeStationState.Row0Cancelled?red:amber,tag:(short)v),-1);
                else if(i==4)for(int v=0;v<NativeExpeditionStation.Row4Status.Length;v++)BoardText(booth,yaw,half,NativeExpeditionStation.Row4Status[v],left+ColStatus*4*c,y,c,d,With(v==NativeStationState.Row4Approaching?white:v==NativeStationState.Row4Cancelled?red:amber,tag:(short)(4*16+v)),-1);
                else BoardText(booth,yaw,half,NativeExpeditionStation.RowStatus[i],left+ColStatus*4*c,y,c,d,With(red,tag:-1),-1);
            }
            // The ticker strip and its pages.
            FaceAt(yaw,BoardX,BoardZ,half,0,.03,out double tx,out double tz);Box(booth,tx,BoardY-1.66,tz,width-.6,.38,.02,L("plain",0x0C0806,RoleBoard),yaw);
            for(int pg=0;pg<NativeExpeditionStation.TickerPages.Length;pg++)BoardText(booth,yaw,half,NativeExpeditionStation.TickerPages[pg],0,BoardY-1.66,.05,d+.012,With(Glow(AmberHex,1.7f,RoleBoard,DriveTicker),tag:(short)pg,detail:true),0);
        }
        private void BoardText(int booth,double yaw,double half,string text,double u,double y,double cell,double d,Pen pen,int align)
        {
            FaceAt(yaw,BoardX,BoardZ,half,u,d,out double x,out double z);Text(booth,text,x,y,z,yaw,cell,.022,pen,align);
        }
        private void BoardSide(int booth,double yaw)
        {
            double half=BoardHx,width=BoardHz*2;
            // The side panels plain: the holo look tinted them violet on these faces.
            FaceAt(yaw,BoardX,BoardZ,half,0,.012,out double px,out double pz);var panel=With(L("plain",0x1E1006,RoleBoard),drive:DriveBoard);panel.Gain=.5f;Box(booth,px,BoardY,pz,width-.36,BoardHy*2-.36,.02,panel,yaw);
            FaceAt(yaw,BoardX,BoardZ,half,0,.1,out double cx,out double cz);Clock(booth,cx,BoardY+1.05,cz,.6,false,yaw);
            var amber=Glow(AmberHex,1.9f,RoleBoard,DriveBoard);
            for(int i=0;i<NativeExpeditionStation.Side.Length;i++)BoardText(booth,yaw,half,NativeExpeditionStation.Side[i],0,BoardY+(i==0?.0:-.1)-i*.46,i==0?.075:.052,.04,With(amber,tag:-1),0);
        }

        // ---------------- the booth, the kiosks ----------------
        private void Booth()
        {
            int b=B("booth");if(b<0)return;var k=blocks[b];double x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,z0=k.pos.z-k.size.z/2,z1=k.pos.z+k.size.z/2,top=k.pos.y+k.size.y/2;
            foreach(var (z,sg) in new[]{(z1,1.0),(z0,-1.0)})
            {
                Span(b,x0+.35,x1-.35,1.1,2.35,z+sg*.005,z+sg*.03,Glow(0x8A6A40,.55f,RoleWall));
                for(int m=0;m<=4;m++){double mx=x0+.35+(x1-x0-.7)*m/4;Span(b,mx-.04,mx+.04,1.1,2.35,z+sg*.03,z+sg*.06,L("steel-green",0x24342E,RoleWall));}
                Span(b,x0+.35,x1-.35,1.86,1.92,z+sg*.03,z+sg*.06,L("steel-green",0x24342E,RoleWall));
                Span(b,x0-.05,x1+.05,1.0,1.1,z,z+sg*.2,L("timber",0x6A4A30,RoleWall));
                Box(b,0,2.75,z+sg*.05,.5,.5,.04,L("plain",0x12201E,RoleSign));Text(b,"I",0,2.75,z+sg*.08,sg>0?0:180,.07,.02,Glow(TealHex,1.8f,RoleSign));
            }
            foreach(double sx in new[]{-1.0,1.0}){Box(b,k.pos.x+sx*(k.size.x/2+.03),2.75,k.pos.z,.04,.5,.5,L("plain",0x12201E,RoleSign));Text(b,"I",k.pos.x+sx*(k.size.x/2+.06),2.75,k.pos.z,sx>0?90:270,.07,.02,Glow(TealHex,1.8f,RoleSign));}
            Span(b,x0-.12,x1+.12,top,top+.22,z0-.12,z1+.12,L("steel-green",0x24342E,RoleWall));
            Light(k.pos.x,2.3,k.pos.z,0xFFD8A0,.55,7,0,GlowWarm);
        }
        private void Kiosks()
        {
            foreach(int sg in new[]{-1,1})
            {
                int b=Side("kiosk-e","kiosk-w",sg);if(b<0)continue;var k=blocks[b];double x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,zf=k.pos.z-k.size.z/2,top=k.pos.y+k.size.y/2,xc=k.pos.x;
                Span(b,x0-.1,x1+.1,top-.6,top,zf-.12,zf,L("steel-green",0x24342E,RoleSign));
                if(sg<0)
                {
                    // The cafe: burnt out, still smouldering. Sooted front, the shutter half down, CAFE in scorched paint, embers.
                    Span(b,x0+.2,x1-.2,.15,2.3,zf-.02,zf,L("plain",0x0A0807,RoleWall));Span(b,x0+.2,x1-.2,1.6,2.3,zf-.06,zf-.02,L("corrugated",0x3A3430,RoleWall));
                    Span(b,x0,x1,2.35,top-.6,zf-.03,zf,L("plain",0x161210,RoleWall));Text(b,"CAFE",xc,top-.3,zf-.13,180,.08,.01,L("plain",0x5A4A3A,RoleSign));
                    for(int i=0;i<12;i++){double h=Hash("embers",i);Box(b,x0+.6+h*4.6,.012,zf-.25-Hash("embers",i,1)*.5,.08+h*.1,.024,.06,Glow(0xFF5A1A,2.4f,RoleFlat,DriveFire,(float)h*9),h*180);}
                    Box(b,xc-1.3,.25,zf-.35,1.0,.5,.6,L("timber",0x2A1E14,RoleClutter),20);
                    Light(xc,.9,zf-.6,0xFF7A2E,1.0,9,.8,GlowFire,.37,0);
                }
                else
                {
                    // The newsagent: the fridge still humming, NEWS, a rack of papers, the shutter down to the counter.
                    Span(b,x0+.3,x0+2.3,.2,2.2,zf-.03,zf,Glow(0xC8F0FF,.75f,RoleWall));for(int j=0;j<4;j++)Span(b,x0+.3,x0+2.3,.6+j*.45,.63+j*.45,zf-.05,zf-.03,L("plain",0x2A3A40,RoleWall));
                    Span(b,x0+2.6,x1-.2,1.2,2.3,zf-.06,zf,L("corrugated",0x5A5E5C,RoleWall));Span(b,x0+2.6,x1-.2,.4,1.2,zf-.2,zf,L("timber",0x4A3424,RoleWall));
                    for(int j=0;j<6;j++)Box(b,x0+2.9+j*.48,.9,zf-.12,.36,.5,.03,L("plain",j%2==0?PaperHex:0xA83A2A,RoleWall),0,-12);
                    Text(b,"NEWS",xc,top-.3,zf-.13,180,.08,.01,Glow(0xE8E0C8,.5f,RoleSign));
                    Light(x0+1.3,1.2,zf-.8,0xC8F0FF,.45,6,.6,GlowCold);
                }
            }
        }

        // ---------------- the balconies and the gallery ----------------
        private void Balconies()
        {
            var iron=L("steel-green",IronHex,RoleWall);var rail=L("timber",0x4A3424,RoleWall);
            foreach(int sg in new[]{-1,1})
            {
                int deck=Side("deck-w","deck-e",sg);if(deck<0)continue;double xi=sg*30.0;
                // The deck's fascia over the arcade: cream with the station's green and a teal line.
                Span(deck,xi,xi-sg*.1,5.5,6.0,-4.0,20.0,L("ward-tile",CreamHex,RoleHigh));Span(deck,xi-sg*.1,xi-sg*.13,5.62,5.7,-4.0,20.0,Glow(TealHex,.7f,RoleHigh));
                int inner=Side("rail-w-inner","rail-e-inner",sg);if(inner>=0)RailDress(inner,"z",sg<0?1:-1);
                int land=Side("rail-w-landing","rail-e-landing",sg);if(land>=0)RailDress(land,"z",sg<0?1:-1);
                // Globe lamps on the rail, one in three lit.
                foreach(double z in new[]{5.0,11.0,17.0})
                {
                    if(inner<0)break;double h=Hash("globe",sg,(int)z);bool lit=h<.4;var r=blocks[inner];double top=r.pos.y+r.size.y/2;
                    Box(inner,r.pos.x,top+.6,z,.08,1.2,.08,With(iron,fade:true));Box(inner,r.pos.x,top+1.32,z,.36,.36,.36,lit?Glow(0xFFE8C0,1.6f,RoleLamp):L("plain",0x3A3630,RoleLamp));
                    if(lit)Light(r.pos.x,top+1.3,z,0xFFD8A0,.7,9,.7,GlowGlobe,h,6);
                }
                // The field hospital under the west arcade; luggage and a cart under the east.
                int wall=Side("mass-cw","mass-ce",sg);int floor=B("floor-north");if(wall<0||floor<0)continue;double xw=sg*40;
                if(sg<0)
                {
                    for(int k=0;k<5;k++){double z=-1.5+k*3.6;Begin();Cot(floor,-38.1,z,90);End();}
                    Span(wall,xw+.01,xw+.03,2.6,4.2,4.0,6.4,L("plain",0xE0E0D8,RoleWall));Span(wall,xw+.03,xw+.05,3.1,3.7,4.95,5.45,L("plain",0xC02A20,RoleWall));Span(wall,xw+.03,xw+.05,3.25,3.55,4.65,5.75,L("plain",0xC02A20,RoleWall));
                    int post=B("post-e-1");if(post<0)post=B("post-w-1");if(post>=0&&Math.Sign(blocks[post].pos.x)<0){var pb=blocks[post];Box(post,pb.pos.x-.38,2.7,pb.pos.z,.18,.3,.3,Glow(0xFFF0D8,1.4f,RoleLamp),0,0,-30);Light(pb.pos.x-1.2,2.4,pb.pos.z,0xFFF0D8,.7,8,.5,GlowWarm,0,0);}
                }
                else
                {
                    for(int k=0;k<4;k++){double z=14+k*.7,h=Hash("bags",k);Box(floor,39.4-h*.4,.22,z,.55,.44,.32,L("plain",new[]{0x6A2A2A,0x2A3A5A,0x3A3A3A,0x5A4A2A}[k],RoleClutter),h*40);}
                }
                // Blind arches high on the side walls over the balcony.
                for(int k=0;k<3;k++){double z=-.5+k*7.5;Span(wall,xw-sg*.02,xw-sg*.06,9.0,15.5,z-1.6,z+1.6,L("plain",0x2A2420,RoleHigh));for(int j=0;j<4;j++){double w=3.4-j*.7;Span(wall,xw-sg*.02,xw-sg*.08,15.5+j*.3,15.8+j*.3,z-w/2,z+w/2,L("brick-buff",0x6A5040,RoleHigh));}}
            }
            int gal=B("gallery-rail");if(gal>=0)
            {
                RailDress(gal,"x",-1);var g=blocks[gal];double top=g.pos.y+g.size.y/2;
                foreach(double x in new[]{-22.0,-8.0,8.0,22.0}){double h=Hash("gallery",(int)x);bool lit=h<.45;Box(gal,x,top+.6,g.pos.z,.08,1.2,.08,With(iron,fade:true));Box(gal,x,top+1.32,g.pos.z,.36,.36,.36,lit?Glow(0xFFE8C0,1.6f,RoleLamp):L("plain",0x3A3630,RoleLamp));if(lit)Light(x,top+1.3,g.pos.z,0xFFD8A0,.7,9,.7,GlowGlobe,h,6);}
            }
        }
        /// <summary>A solid rail block dressed as cast iron: a timber handrail, a bottom rail and balusters on the face toward
        /// `sign` along its thin axis (detail).</summary>
        private void RailDress(int rail,string along,int sign)
        {
            var r=blocks[rail];double top=r.pos.y+r.size.y/2,bot=r.pos.y-r.size.y/2;bool alongZ=along=="z";double len=alongZ?r.size.z:r.size.x,thin=alongZ?r.size.x:r.size.z;
            double face=(alongZ?r.pos.x:r.pos.z)+sign*thin/2;
            void At(double u,double y,double d,double su,double sy,double sd,Pen pen){if(alongZ)Box(rail,face+sign*d,y,r.pos.z+u,sd,sy,su,pen);else Box(rail,r.pos.x+u,y,face+sign*d,su,sy,sd,pen);}
            At(0,top+.04,-thin/2,len+.06,.08,thin+.12,L("timber",0x4A3424,RoleWall));
            At(0,bot+.12,.02,len,.08,.04,L("steel-green",IronHex,RoleWall));
            for(double u=-len/2+.3;u<len/2-.2;u+=.62)At(u,(top+bot)/2,.02,.04,top-bot-.1,.035,With(L("steel-green",IronHex,RoleWall),detail:true));
        }

        // ---------------- the concourse floor and furniture ----------------
        private void ConcourseFloor(int floor)
        {
            var band=With(L("plain",0x5A5448,RoleFlat),level:1);
            for(int x=-36;x<=36;x+=4)Box(floor,x,.003,8,.1,.006,23.8,band);
            for(int z=0;z<=16;z+=4)Box(floor,0,.003,z,79.8,.006,.1,band);
            // The brass and teal rosette under the board, the compass of line colours.
            for(int k=0;k<48;k++){double a=k*Math.PI/24;Box(floor,BoardX+Math.Sin(a)*6.4,.005,BoardZ+Math.Cos(a)*6.4,.86,.01,.05,With(L("plain",k%2==0?0x8A7440:0x3A4A44,RoleFlat),detail:k%2==1),k*7.5+90);}
            for(int k=0;k<4;k++){double a=(45+90*k)*Math.PI/180;Box(floor,BoardX+Math.Sin(a)*5.2,.006,BoardZ+Math.Cos(a)*5.2,.08,.012,1.2,L("plain",Dim(LineHex[k]),RoleFlat),45+90*k);}
            // Timetables and papers, glass grit round the fallen girder.
            for(int k=0;k<36;k++){double x=-30+Hash("paper",k)*60,z=-2.5+Hash("paper",k,1)*21;Box(floor,x,.004,z,.22,.008,.3,With(L("plain",k%3==0?0x8A3A2A:PaperHex,RoleFlat),detail:true),Hash("paper",k,2)*180);}
            int g=B("girder");double gx=g>=0?blocks[g].pos.x:8,gz=g>=0?blocks[g].pos.z:-1.2;
            for(int k=0;k<40;k++){double a=Hash("grit",k)*Math.PI*2,r=1+Hash("grit",k,1)*5;Box(floor,gx+Math.Cos(a)*r,.005,gz+Math.Sin(a)*r*.6,.06+Hash("grit",k,2)*.22,.01,.05+Hash("grit",k,3)*.16,With(L("plain",k%3==0?0x3A4A54:0x6A8A9A,RoleFlat),detail:true),Hash("grit",k,4)*180);}
            // Two more puddles where the floor dips; the water in the vault's gaps (Vault) has its own.
            foreach(var (x,z,r) in new[]{(-6.5,3.5,1.3),(24.0,1.0,1.2)})Puddle(floor,x,z,r,x*.01);
        }
        /// <summary>A puddle on a floor: a water record the atmosphere darkens softly and streaks with the lights' reflections.</summary>
        private void Puddle(int floor,double x,double z,double r,double seed)
        {
            double top=blocks[floor].pos.y+blocks[floor].size.y/2;
            Water(x-r,x+r,z-r*.65,z+r*.65,top+.006,WaterPuddle);
        }
        /// <summary>An army cot on the floor (frame, olive canvas, a folded blanket), long along `yaw`.</summary>
        private void Cot(int floor,double x,double z,double yaw)
        {
            Box(floor,x,.225,z,.8,.45,1.9,L("plain",0x3A4A2A,RoleClutter),yaw);Box(floor,x,.2,z,.84,.24,1.7,L("steel-green",0x2A2C28,RoleClutter),yaw);
            double a=yaw*Math.PI/180;Box(floor,x+Math.Sin(a)*.55,.39,z+Math.Cos(a)*.55,.7,.2,.5,L("plain",0x6A6A58,RoleClutter),yaw);
        }
        private void ConcourseFurniture(int floor)
        {
            var iron=L("steel-green",0x22302A,RoleFurniture);
            foreach(var stem in new[]{"benches-w-0","benches-w-1","benches-e-0","benches-e-1"})
            {
                int b=B(stem);if(b<0)continue;var k=blocks[b];double top=k.pos.y+k.size.y/2,back=k.pos.z+k.size.z/2-.06;
                Box(b,k.pos.x,top+.22,back,k.size.x-.1,.4,.06,L("timber",0x5A3E2A,RoleFurniture),0,-8);
                foreach(double sx in new[]{-1.0,1.0})Box(b,k.pos.x+sx*(k.size.x/2-.15),top/2+.12,k.pos.z,.08,top+.24,k.size.z,iron);
            }
            foreach(var stem in new[]{"trolley-w","trolley-e"})
            {
                int b=B(stem);if(b<0)continue;var k=blocks[b];double top=k.pos.y+k.size.y/2;
                for(int i=0;i<3;i++){double h=Hash(stem,i);Box(b,k.pos.x+(h-.5)*.8,top+.18+i*.02,k.pos.z+(i-1)*.55,.8,.36,.45,L("plain",new[]{0x5A3A2A,0x2A3A5A,0x6A5A3A}[i],RoleFurniture),h*30);}
            }
            int g=B("girder");if(g>=0)
            {
                var k=blocks[g];double yaw=2*Math.Atan2(k.rotation?.y??0,k.rotation?.w??1)*180/Math.PI,c=Math.Cos(yaw*Math.PI/180),s=Math.Sin(yaw*Math.PI/180),top=k.pos.y+k.size.y/2;
                for(int i=-4;i<=4;i++){double u=i*1.1;Box(g,k.pos.x+c*u,top+.02,k.pos.z-s*u,.12,.04,k.size.z+.06,L("steel-green",0x1A2620,RoleFurniture),yaw);}
                Box(g,k.pos.x,top+.01,k.pos.z,k.size.x+.2,.03,k.size.z+.2,L("steel-green",0x2E463C,RoleFurniture),yaw);
            }
            // The toppled vending machine against the newsagent, its panel still lit.
            int news=Side("kiosk-e","kiosk-w",1);if(news>=0){var k=blocks[news];double xe=k.pos.x+k.size.x/2;Box(floor,xe+.35,.32,k.pos.z,.64,.62,1.8,L("cyber-panel",0x8A2A2A,RoleClutter));Box(floor,xe+.675,.3,k.pos.z-.2,.02,.36,.9,Glow(0xFF4A3A,.9f,RoleClutter));}
            // The platform gates: posts on the barrier ends, a sign over each gap (the line colours under), WAY OUT behind.
            var gates=new[]{(-21.0,"PLATFORMS 1 2",0,1),(0.0,"ALL PLATFORMS",0,3),(21.0,"PLATFORMS 3 4",2,3)};
            int screen=B("screen");
            foreach(var (gx,name,c0,c1) in gates)
            {
                int bar=-1;double best=1e9;for(int i=0;i<4;i++){int bi=B("barrier-"+i);if(bi<0)continue;double d=Math.Abs(blocks[bi].pos.x-gx);if(d<best){best=d;bar=bi;}}if(bar<0)continue;
                double w=6,z=-4.2;var post=With(L("steel-green",IronHex,RoleWall),shadow:true);
                Box(bar,gx-w/2-.08,2.05,z,.16,4.1,.3,post);Box(bar,gx+w/2+.08,2.05,z,.16,4.1,.3,post);
                Box(bar,gx,3.6,z,w+.32,.7,.16,L("steel-green",0x1A2A26,RoleSign));
                for(int k=c0;k<=c1;k++)Box(bar,gx-w/2+(k-c0+.5)*w/(c1-c0+1),3.22,z,w/(c1-c0+1),.06,.17,Glow(LineHex[k],.9f,RoleSign));
                Text(bar,name,gx,3.61,z+.1,0,.05,.012,Glow(0xE8E0C8,.8f,RoleSign));
                if(gx==0)Text(bar,"WAY OUT",gx,3.61,z-.1,180,.06,.012,Glow(0xF2D21A,1.1f,RoleSign));
            }
        }

        // ---------------- the ticket hall ----------------
        private void Hall()
        {
            int ceil=B("hall-ceiling"),floor=B("floor-north");if(ceil<0||floor<0)return;var c=blocks[ceil];double under=c.pos.y-c.size.y/2;
            // Troffers: three rows of five; about a third dead and a few stuttering.
            for(int i=0;i<5;i++)for(int j=0;j<3;j++)
            {
                double x=-14+i*7,z=23.5+j*4.5,h=Hash("troffer",i,j);bool dead=h<.3,stutter=!dead&&h<.5;
                Box(ceil,x,under-.035,z,2.6,.07,.8,L("steel-green",0x3A403C,RoleHigh));
                Box(ceil,x,under-.08,z,2.4,.04,.6,With(L("ward-light",FluorHex,RoleHigh),drive:stutter?DriveFluor:DriveSteady,level:dead?0:1),0);
                if(!dead)Light(x,under-.4,z,FluorHex,stutter?.6:.85,9,0,stutter?GlowTubeFlicker:GlowTube,h,0);
            }
            // The walls: the green dado band and skirting on the cream tile; the north wall tiled round the way out.
            foreach(int sg in new[]{-1,1})
            {
                int wall=Side("mass-nw","mass-ne",sg);if(wall<0)continue;double xw=sg*22.0,d=-sg*.025;
                Span(wall,xw,xw+d,1.05,1.25,20.2,35.8,L("steel-green",GreenHex,RoleWall));Span(wall,xw,xw+d,0,.16,20.2,35.8,L("steel-green",0x24342E,RoleWall));Span(wall,xw,xw+d*2,5.15,5.5,20.2,35.8,L("concrete-board",0xB0A894,RoleWall));
            }
            int north=B("shell-n");if(north>=0)
            {
                double zf=36.0;foreach(var (a,b) in new[]{(-22.0,-2.1),(2.1,22.0)}){Span(north,a,b,0,5.5,zf-.03,zf,L("ward-tile",CreamHex,RoleWall));Span(north,a,b,1.05,1.25,zf-.05,zf,L("steel-green",GreenHex,RoleWall));Span(north,a,b,0,.16,zf-.05,zf,L("steel-green",0x24342E,RoleWall));}
                Span(north,-2.1,2.1,3.85,5.5,zf-.03,zf,L("ward-tile",CreamHex,RoleWall));
                Span(north,-1.6,1.6,4.0,4.55,zf-.09,zf-.03,Glow(0xF2D21A,1.5f,RoleSign));Text(north,"WAY OUT",0,4.27,zf-.11,180,.07,.02,L("plain",0x101010,RoleSign));
                Light(0,4.2,zf-.6,0xF2D21A,.35,5,.6,GlowWarm,0,0);
                // Posters and the EVAC CANCELLED notice beside the door.
                Span(north,-9.0,-6.6,1.5,3.2,zf-.06,zf-.03,L("plain",0xE0D8C0,RoleWall));Text(north,"EVAC",-7.8,2.75,zf-.075,180,.07,.008,L("plain",0xB82A20,RoleSign));Text(north,"CANCELLED",-7.8,2.25,zf-.075,180,.045,.008,L("plain",0xB82A20,RoleSign));
                foreach(var (x,hex) in new[]{(6.5,0x8A2A3A),(9.0,0x2A5A8A),(11.5,0xC89A3A)})Span(north,x-.6,x+.6,1.5,3.1,zf-.05,zf-.03,L("plain",hex,RoleWall));
            }
            // The clock over the gate line, hanging from the hall's edge beam; the beam's fascia.
            Span(ceil,-22,22,under-.25,under,20.0,20.35,L("concrete-board",0xB0A894,RoleHigh));
            // The ceiling's beams: two over the columns along the hall, two across between the troffer rows.
            foreach(double bx in new[]{-11.0,11.0})Span(ceil,bx-.3,bx+.3,under-.35,under,20.35,35.8,L("concrete-board",0x9A9280,RoleHigh));
            foreach(double bz in new[]{25.75,30.25})Span(ceil,-21.9,21.9,under-.3,under,bz-.25,bz+.25,L("concrete-board",0x9A9280,RoleHigh));Clock(ceil,-11,4.25,21.3,.5,true);Box(ceil,-11,4.95,21.3,.05,.6,.05,With(L("steel-green",IronHex,RoleHigh),fade:true));
            // The ticket office: four windows over the counter, one lit; TICKETS on the green board above.
            int office=B("hall-office"),counter=B("hall-counter");
            if(office>=0&&counter>=0)
            {
                var o=blocks[office];double xf=o.pos.x+o.size.x/2;
                for(int k=0;k<4;k++){double z=27.0+k*1.8;bool lit=k==2;Box(office,xf+.015,1.85,z,.03,1.3,1.4,lit?With(L("window-lit",0xFFD8A0,RoleWall),level:1):Glow(0x0E161C,.4f,RoleWall));Box(office,xf+.04,1.85,z,.04,1.4,.08,L("steel-green",0x3A4642,RoleWall));Box(office,xf+.04,2.55,z,.04,.06,1.5,L("steel-green",0x3A4642,RoleWall));}
                Box(office,xf+.04,2.95,o.pos.z,.06,.42,7.0,L("steel-green",0x1E2E2A,RoleSign));Text(office,"TICKETS",xf+.08,2.95,o.pos.z,90,.065,.012,Glow(AmberHex,1.4f,RoleSign));
                Light(xf+1.0,2.0,30.6,0xFFD8A0,.55,6,.4,GlowWarm,0,0);
            }
            int mach=B("hall-machines");if(mach>=0)
            {
                var m=blocks[mach];double xf=m.pos.x-m.size.x/2;
                for(int k=0;k<4;k++){double z=26.0+k*2.0;bool dead=k==1||k==3;var scr=L("cyber-holo",0x2A8AD8,RoleWall);scr.Gain=.7f;scr.Level=dead?0:1;Box(mach,xf-.015,1.3,z,.03,.5,.7,scr);Box(mach,xf-.02,.85,z,.03,.25,.4,With(L("plain",0x2A3034,RoleWall),detail:true));}
                Light(xf-1.0,1.3,27.0,0x2A8AD8,.3,5,.3,GlowScreen,0,0);
            }
            // The turnstiles: a red lamp on each (locked) and the paddles; sandbags on the checkpoint walls with police tape.
            for(int i=0;i<4;i++)
            {
                int gb=B("hall-gates-"+i);if(gb<0)continue;var gk=blocks[gb];double top=gk.pos.y+gk.size.y/2,x0=gk.pos.x-gk.size.x/2;int n=(int)(gk.size.x/2);
                Box(gb,gk.pos.x,top+.02,gk.pos.z,gk.size.x,.04,gk.size.z+.04,L("cyber-metal",0x2A3034,RoleWall));
                for(int k=0;k<n;k++){double x=x0+1+k*2;Box(gb,x,top+.075,gk.pos.z,.14,.07,.14,Glow(RedHex,2.2f,RoleWall));Box(gb,x-.45,.75,gk.pos.z-gk.size.z/2-.04,.6,.06,.06,With(L("cyber-metal",0x5A6460,RoleWall),detail:true));
                    Box(gb,x+.55,top+.28,gk.pos.z,.04,.5,.7,With(Glow(0x8AB8C0,.35f,RoleWall),level:1));Box(gb,x-.6,top+.05,gk.pos.z+.2,.18,.03,.08,L("plain",0xD8B000,RoleWall));Box(gb,x,gk.pos.y,gk.pos.z-gk.size.z/2-.005,.04,gk.size.y-.1,.01,With(L("plain",0x161A1C,RoleWall),detail:true));}
            }
            foreach(var stem in new[]{"hall-sandbags-w","hall-sandbags-e"})
            {
                int sb=B(stem);if(sb<0)continue;var s=blocks[sb];double x0=s.pos.x-s.size.x/2;
                for(int row=0;row<3;row++)for(int k=0;k<7;k++){double x=x0+.32+k*.62+(row%2==1?.16:0);if(x>x0+s.size.x-.2)continue;double h=Hash(stem,row,k);Box(sb,x,.15+row*.3,s.pos.z,.6,.29,.64,L("plain",h<.6?0x8A7A58:0x7A6A4A,RoleFurniture),(h-.5)*8);}
                Box(sb,s.pos.x,.92,s.pos.z,s.size.x,.02,.06,L("plain",0xE8D020,RoleFurniture));
            }
            // The checkpoint: cots and crates by the walls, a radio on a crate, the roundel in the floor.
            foreach(var (x,z) in new[]{(-12.0,34.3),(-9.5,34.3),(9.5,34.3),(12.0,34.3)}){Begin();Cot(floor,x,z,0);End();}
            foreach(var (x,z,yaw) in new[]{(-20.9,26.4,10.0),(-20.6,27.3,-5.0),(20.0,34.6,20.0),(19.2,34.8,-8.0),(-15.2,23.2,12.0),(-14.3,23.6,-20.0),(13.4,23.0,5.0),(16.0,24.6,-14.0),(-7.4,23.2,30.0)}){Begin();Box(floor,x,.3,z,.8,.6,.8,L("timber",0x7A5A3A,RoleClutter),yaw);Box(floor,x,.3,z,.84,.61,.1,With(L("plain",0x3A2A1A,RoleClutter),detail:true),yaw);End();}
            foreach(var (x0,x1) in new[]{(-12.6,-9.4),(9.4,12.6)})for(int row=0;row<2;row++)for(double x=x0+.3;x<x1-.1;x+=.62){double h=Hash("gatebags",(int)(x*10),row);Begin();Box(floor,x+(row==1?.31:0),.15+row*.29,22.65,.6,.29,.62,L("plain",h<.6?0x8A7A58:0x7A6A4A,RoleClutter),(h-.5)*10);End();}
            {int sb=B("hall-sandbags-e");if(sb>=0){var s=blocks[sb];double top=s.pos.y+s.size.y/2,x=s.pos.x+s.size.x/2-.4;Box(sb,x,top+.11,s.pos.z,.4,.18,.3,L("plain",0x2A3A2A,RoleFurniture));Box(sb,x-.1,top+.21,s.pos.z-.08,.05,.02,.05,Glow(SignalGreenHex,2.5f,RoleFurniture));Light(x,top+.5,s.pos.z-.5,SignalGreenHex,.12,3,.12,GlowScreen,0,0);}}
            for(int k=0;k<40;k++){double a=k*Math.PI/20;Box(floor,Math.Sin(a)*2.9,.005,27.2+Math.Cos(a)*2.9,.47,.01,.05,With(L("plain",k%2==0?0x8A7440:0x6A6250,RoleFlat),detail:k%2==1),k*9+90);}
            for(int k=0;k<4;k++){double a=(45+90*k)*Math.PI/180;Box(floor,Math.Sin(a)*1.7,.006,27.2+Math.Cos(a)*1.7,.07,.012,.9,L("plain",Dim(LineHex[k]),RoleFlat),45+90*k);}
            Box(floor,0,.007,27.2,.7,.014,.7,L("plain",0x2A5A52,RoleFlat),45);
            // The columns: a green band, base and capital on the cream tile.
            foreach(var stem in new[]{"hall-column-w","hall-column-e"}){int cb=B(stem);if(cb<0)continue;var k=blocks[cb];Box(cb,k.pos.x,1.15,k.pos.z,k.size.x+.04,.2,k.size.z+.04,L("steel-green",GreenHex,RoleWall));Box(cb,k.pos.x,.1,k.pos.z,k.size.x+.1,.2,k.size.z+.1,L("steel-green",0x24342E,RoleWall));Box(cb,k.pos.x,5.25,k.pos.z,k.size.x+.24,.5,k.size.z+.24,L("concrete-board",0xB0A894,RoleHigh));}
            // The route map and timetables on the west wall.
            int west=Side("mass-nw","mass-ne",-1);if(west>=0){double xw=-22+.03;Span(west,xw-.03,xw,2.0,3.6,22.6,25.4,L("plain",0x1A1E22,RoleWall));for(int l=0;l<4;l++)Span(west,xw,xw+.012,2.45+l*.3,2.5+l*.3,22.9,25.1-l*.3,L("plain",LineHex[l],RoleWall));}
        }

        // ---------------- the upper floor ----------------
        private void Upper()
        {
            // The office wall over the gallery: dark windows (one lit), the doors' frames and name plates; the PA console's ON lamp.
            for(int i=0;i<7;i++)
            {
                int w=B("office-wall-"+i);if(w<0)continue;var k=blocks[w];if(k.size.y<3)continue;double x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,zf=k.pos.z-k.size.z/2;
                for(double x=x0+1.6;x<x1-1.2;x+=3.4){double h=Hash("office",i,(int)x);Box(w,x,8.0,zf-.015,1.6,1.4,.03,h<.12?With(L("window-lit",0xFFE0B0,RoleHigh),level:1):Glow(0x0C1418,.35f,RoleHigh));Box(w,x,7.25,zf-.05,1.8,.08,.1,L("concrete-board",0x9A9484,RoleHigh));}
            }
            foreach(var (x,name) in new[]{(-26.2,"LOST PROPERTY"),(0.0,"STATION MASTER"),(26.2,"PA ROOM")})
            {
                int w=B("office-wall-3");foreach(int i in new[]{1,3,5}){int bi=B("office-wall-"+i);if(bi>=0&&Math.Abs(blocks[bi].pos.x-x)<.5)w=bi;}if(w<0)continue;double zf=24.0;
                Text(w,name,x,9.65,zf-.03,180,.035,.01,With(L("plain",0xE8E0C8,RoleSign),detail:true));
            }
            int pa=B("office-desk-pa");if(pa>=0){var k=blocks[pa];double top=k.pos.y+k.size.y/2;Box(pa,k.pos.x,top+.05,k.pos.z,1.2,.1,.5,L("cyber-metal",0x2A3034,RoleFurniture));Box(pa,k.pos.x+.4,top+.12,k.pos.z,.06,.04,.06,Glow(RedHex,3.0f,RoleFurniture));Light(k.pos.x,top+.8,k.pos.z-.4,0xFF5A3A,.45,6,.3,GlowWarm,0,6);}
            int desk=B("office-desk");if(desk>=0){var k=blocks[desk];double top=k.pos.y+k.size.y/2;for(int j=0;j<5;j++)Box(desk,k.pos.x-2+j*.9,top+.004,k.pos.z,.28,.008,.36,L("plain",PaperHex,RoleFurniture),j*17);}
            int lockers=B("office-lockers");if(lockers>=0){var k=blocks[lockers];double zf=k.pos.z-k.size.z/2;for(int j=0;j<10;j++)Box(lockers,k.pos.x-k.size.x/2+.4+j*.8,k.pos.y,zf-.01,.04,k.size.y-.1,.02,With(L("plain",0x1A1E20,RoleWall),detail:true));}
        }
    }
}
