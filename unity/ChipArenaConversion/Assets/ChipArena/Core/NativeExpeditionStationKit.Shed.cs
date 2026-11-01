using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The train shed, the tracks, the trains, the footbridges, the corridor, the signal box, the entrances and the express
    // (docs/EXPEDITION-CENTRAL-STATION.md sections 3 and 6).
    public sealed partial class NativeExpeditionStationKit
    {
        /// <summary>The platforms' centre lines (z) the sodium pendants hang over and the lines' signs name, P1..P4.</summary>
        public static readonly float[] PlatformZ={-7f,-21f,-37f,-50f};
        /// <summary>The pits' z ranges, T1..T3 (the draft's TRACKS).</summary>
        public static readonly float[] PitZ0={-15f,-32f,-47f},PitZ1={-10f,-27f,-42f};
        /// <summary>The third rail's sections: line (1..3) and x range, in NativeExpeditionStation.Rail* order.</summary>
        public static readonly float[] RailLine={1,1,2,3,3},RailFrom={-44f,2.1f,-44f,-19.4f,2.1f},RailTo={-2.1f,44f,-30.6f,-2.1f,44f};
        /// <summary>The flood: the T3 pit west of the wagon's far end and the west throat (the draft's flood volumes).</summary>
        public static readonly float[][] Flood={new[]{-44f,-20f,-47f,-42f},new[]{-44f,-38f,-42f,-25f}};
        private const float TB=NativeExpeditionStation.TrackBed,Mouth=NativeExpeditionStation.Mouth;

        // ---------------- the shed ----------------
        private void Shed()
        {
            int mid=B("shed-roof-2");if(mid<0)return;var iron=With(L("steel-green",0x1E2C26,RoleHigh),shadow:true);var ironThin=L("steel-green",0x1E2C26,RoleHigh);
            // Trusses under the roof every 8 m: a top chord, a bottom chord and posts; the lamps hang from the bottom chords.
            for(double x=-40;x<=40.01;x+=8)
            {
                Box(mid,x,11.86,-28.7,.24,.24,48.6,iron);Box(mid,x,10.6,-28.7,.28,.3,48.6,iron);
                for(double z=-50.6;z<-5;z+=4.05)Box(mid,x,11.23,z,.14,1.0,.14,With(ironThin,detail:true));
            }
            // The skylights between the roof's strips: glazing (some gone: the moon falls through), its bars.
            var roofs=new List<ContentBlock>();for(int k=0;k<8;k++){int r=B("shed-roof-"+k);if(r>=0)roofs.Add(blocks[r]);}roofs.Sort((a,b)=>a.pos.z.CompareTo(b.pos.z));
            int shafts=0;
            for(int k=0;k+1<roofs.Count;k++)
            {
                double z0=roofs[k].pos.z+roofs[k].size.z/2,z1=roofs[k+1].pos.z-roofs[k+1].size.z/2;if(z1-z0<.5)continue;double zc=(z0+z1)/2;int parent=B("shed-roof-"+k);if(parent<0)parent=mid;
                for(double x=-44;x<44;x+=8)
                {
                    double h=Hash("skylight",(int)x,k);
                    if(h<.3)
                    {
                        MoonTo(x+4,12.2,zc,0,out double fx,out double fz);double floorY=FloorUnder(fx,fz);MoonTo(x+4,12.2,zc,floorY,out fx,out fz);
                        if(shafts<12&&h<.22&&Math.Abs(fx)<44){Shaft(x+4,12.1,zc,fx,floorY+.05,fz,1.0,2.2,.75,ShaftMoon);shafts++;}
                        continue;
                    }
                    Box(parent,x+4,12.25,zc,7.9,.03,z1-z0,Glow(h<.6?0x0E1820:0x142028,.5f,RoleHigh));
                    Box(parent,x+4,12.2,zc,.08,.08,z1-z0,With(ironThin,detail:true));
                }
            }
            Pendants();PlatformEdges();PlatformSigns();PlatformFurniture();Mouths();
        }
        /// <summary>The walkable floor's height at (x, z): a platform's top, or the track bed in the pits and throats.</summary>
        private double FloorUnder(double x,double z)
        {
            foreach(var kv in byStem){if(!kv.Key.StartsWith("plat-",StringComparison.Ordinal))continue;var b=blocks[kv.Value];if(Math.Abs(x-b.pos.x)<=b.size.x/2&&Math.Abs(z-b.pos.z)<=b.size.z/2)return b.pos.y+b.size.y/2;}
            return z>-4?0:z<-53?0:TB;
        }
        // Sodium pendants over each platform's centre line from the trusses (about half lit, a few flickering); a cone of light
        // under each lit one. None over a footbridge or a drop column (the keep-outs).
        private void Pendants()
        {
            int mid=B("shed-roof-2");var rod=With(L("steel-green",0x24342E,RoleLamp),fade:true);
            for(int p=0;p<4;p++)
                for(double x=-40;x<=40.01;x+=8)
                {
                    if(Math.Abs(Math.Abs(x)-34)<4.5)continue;double z=PlatformZ[p],h=Hash("pendant",p,(int)x);bool lit=h>.42,flick=lit&&h<.55;
                    Begin();
                    Box(mid,x,7.85,z,.05,5.5,.05,rod);Box(mid,x,5.06,z,.8,.14,.42,L("steel-green",0x2A3A32,RoleLamp));
                    Box(mid,x,4.97,z,.56,.04,.28,lit?Glow(0xFFC27A,2.4f,RoleLamp,flick?DriveFlicker:DriveSteady,(float)h*11):L("plain",0x2A2622,RoleLamp));
                    if(lit){Light(x,4.75,z,SodiumHex,flick?.85:1.15,13,.7,flick?GlowLampFlicker:GlowLamp,h*11,0);}
                    if(End()&&lit)Shaft(x,4.9,z,x,.04,z,.22,1.7,.42,ShaftLamp);
                }
        }
        // Each platform's pit edges: the white coping, the yellow line, and red danger strips along the pit wall below the lip
        // (the danger lanes read at a glance; some broken, some dim).
        private void PlatformEdges()
        {
            var edges=new[]{-10f,-15f,-27f,-32f,-42f,-47f};
            foreach(var kv in byStem)
            {
                if(!kv.Key.StartsWith("plat-",StringComparison.Ordinal))continue;int i=kv.Value;var b=blocks[i];double x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
                foreach(var (e,inward) in new[]{(z0,1.0),(z1,-1.0)})
                {
                    if(Array.IndexOf(edges,(float)Math.Round(e))<0||x1-x0<1)continue;
                    Box(i,(x0+x1)/2,.008,e+inward*.175,x1-x0,.016,.35,L("concrete-board",CopingHex,RoleFlat));
                    Box(i,(x0+x1)/2,.009,e+inward*.62,x1-x0,.018,.12,L("plain",YellowHex,RoleFlat));
                    for(double a=x0+.3;a+5.4<=x1+.01;a+=6)
                    {
                        double h=Hash("strip",(int)(a*10),(int)(e*10));if(h<.18)continue;
                        Box(i,a+2.7,-.3,e-inward*.02,5.4,.05,.04,With(Glow(0xFF2A1A,h<.3?.5f:1.7f,RoleTrack,DriveStrip,(float)h*7),level:1));
                    }
                }
            }
        }
        private void PlatformSigns()
        {
            int mid=B("shed-roof-2");var rod=With(L("steel-green",0x24342E,RoleSign),fade:true);
            for(int p=0;p<4;p++)
                foreach(double x in new[]{-12.0,12.0})
                {
                    double z=PlatformZ[p];Begin();
                    Box(mid,x-1.0,8.0,z,.05,8.0,.05,rod);Box(mid,x+1.0,8.0,z,.05,8.0,.05,rod);
                    Box(mid,x,3.6,z,2.6,.72,.08,With(L("steel-green",0x1A2A26,RoleSign),fade:true));
                    foreach(double sg in new[]{1.0,-1.0})
                    {
                        double yaw=sg>0?0:180,d=sg*.05;double ux=sg>0?1:-1;
                        Box(mid,x+ux*.85,3.6,z+d,.62,.62,.02,Glow(LineHex[p],.75f,RoleSign),yaw);
                        Text(mid,(p+1).ToString(),x+ux*.85,3.6,z+d+sg*.02,yaw,.09,.015,Glow(0xF4F0E8,1.6f,RoleSign));
                        if(sg>0)Text(mid,"PLATFORM",x-.35,3.6,z+d+.01,yaw,.04,.01,With(Glow(0xE8E0C8,.9f,RoleSign),detail:true));
                    }
                    End();
                }
        }
        private void PlatformFurniture()
        {
            // Rain through the skylights: puddles on the islands (the lamps streak in them), papers, a dropped case or two.
            for(int p=1;p<3;p++)
            {
                int plat=B("plat-p"+(p+1)+"-w"),plat2=B("plat-p"+(p+1)+"-e");if(plat<0||plat2<0)continue;double z=PlatformZ[p];
                for(int k=0;k<7;k++){double x=-34+k*11+Hash("platpuddle",p,k)*5,r=.7+Hash("platpuddle",p,k+9)*.9,pz=z+(Hash("platpuddle",p,k+3)-.5)*5;if(Math.Abs(x)<2.5)continue;Water(x-r,x+r,pz-r*.6,pz+r*.6,.006,WaterPuddle);}
                for(int k=0;k<14;k++){double x=-36+Hash("platpaper",p,k)*72,pz=z+(Hash("platpaper",p,k+1)-.5)*9;int b=x<0?plat:plat2;Box(b,x,.004,pz,.22,.008,.3,With(L("plain",k%4==0?0x8A3A2A:PaperHex,RoleFlat),detail:true),Hash("platpaper",p,k+2)*180);}
                for(int k=0;k<2;k++){double x=(k==0?-23:27)+Hash("platcase",p,k)*3,pz=z+(Hash("platcase",p,k+1)-.5)*6;int b=x<0?plat:plat2;Begin();Box(b,x,.2,pz,.6,.4,.24,L("plain",k==0?0x5A2A2A:0x2A3A4A,RoleClutter),Hash("platcase",p,k+2)*90);End();}
            }
            var iron=L("steel-green",0x22302A,RoleFurniture);
            foreach(var stem in new[]{"bench-p2","bench-p3"})
            {
                int b=B(stem);if(b<0)continue;var k=blocks[b];double top=k.pos.y+k.size.y/2,back=k.pos.z+k.size.z/2-.06;
                Box(b,k.pos.x,top+.22,back,k.size.x-.1,.4,.06,L("timber",0x5A3E2A,RoleFurniture),0,-8);
                foreach(double sx in new[]{-1.0,1.0})Box(b,k.pos.x+sx*(k.size.x/2-.15),top/2+.12,k.pos.z,.08,top+.24,k.size.z,iron);
            }
            int kiosk=B("kiosk-p2");if(kiosk>=0)
            {
                var k=blocks[kiosk];double zs=k.pos.z-k.size.z/2,top=k.pos.y+k.size.y/2;
                Span(kiosk,k.pos.x-k.size.x/2+.2,k.pos.x+k.size.x/2-.2,.1,2.1,zs-.06,zs,L("corrugated",0x5A5E5C,RoleWall));
                Span(kiosk,k.pos.x-k.size.x/2-.1,k.pos.x+k.size.x/2+.1,top-.45,top,zs-.12,zs,L("steel-green",0x24342E,RoleSign));
                Span(kiosk,k.pos.x-k.size.x/2-.03,k.pos.x-k.size.x/2,1.3,2.0,k.pos.z-.5,k.pos.z+.5,With(L("cyber-holo",AmberHex,RoleWall),level:.7f));
            }
            // The signal cabinets (the spark hazard): doors, louvres, a yellow warning plate and the door gaps that glow as they fizz.
            string[] cab={"cabinet-p2","cabinet-p3"};
            for(int c=0;c<2;c++)
            {
                int b=B(cab[c]);if(b<0)continue;var k=blocks[b];double zs=k.pos.z-k.size.z/2,x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2;
                Cabinet[c*3]=k.pos.x;Cabinet[c*3+1]=k.pos.y;Cabinet[c*3+2]=(float)zs;
                Span(b,x0+.06,k.pos.x-.02,.1,1.5,zs-.02,zs,L("steel-green",0x34483E,RoleWall));Span(b,k.pos.x+.02,x1-.06,.1,1.5,zs-.02,zs,L("steel-green",0x34483E,RoleWall));
                Box(b,k.pos.x,.8,zs-.025,.04,1.3,.02,Glow(0xBFE8FF,1.6f,RoleWall,DriveCabinet,c,(short)c));Box(b,k.pos.x,1.52,zs-.025,1.0,.03,.02,Glow(0xBFE8FF,1.4f,RoleWall,DriveCabinet,c+.5f,(short)c));
                Box(b,k.pos.x-.3,1.2,zs-.03,.22,.22,.01,L("plain",YellowHex,RoleWall),0,0,45);
                for(int j=0;j<4;j++)Box(b,k.pos.x+.3,.4+j*.1,zs-.03,.3,.03,.02,With(L("plain",0x1A2420,RoleWall),detail:true));
                Light(k.pos.x,1.2,zs-.6,0xBFE8FF,.5,6,.4,GlowCabinet,c,0,(short)c);
            }
            // Timetable screens on the footbridges' piers that face the platforms (some dead).
            foreach(var kv in byStem)
            {
                if(!kv.Key.StartsWith("pier-",StringComparison.Ordinal))continue;var k=blocks[kv.Value];bool inner=Math.Abs(k.pos.x)<34;if(!inner)continue;double xf=k.pos.x-Math.Sign(k.pos.x)*(k.size.x/2+.015);
                double h=Hash(kv.Key);double sgx=-Math.Sign(k.pos.x);Box(kv.Value,xf,1.65,k.pos.z,.03,.72,.52,L("plain",0x060606,RoleWall));
                if(h>=.4)for(int j=0;j<5;j++)Box(kv.Value,xf+sgx*.02,1.9-j*.12,k.pos.z,.01,.035,.36-(j%2)*.1,Glow(AmberHex,1.4f,RoleWall,DriveFlicker,(float)h*5+j));
            }
        }
        // The tunnel mouths: the dark beyond, a brick arch ring, the signals beside each mouth, beacons and STAND BACK at the
        // express line's ends, NO ENTRY over every mouth.
        private void Mouths()
        {
            for(int side=-1;side<=1;side+=2)
            {
                int shell=Side("shell-w","shell-e",side);double xf=side*Mouth;
                for(int l=0;l<3;l++)
                {
                    double z0=PitZ0[l],z1=PitZ1[l],zc=(z0+z1)/2;int stub=B("end-"+(side<0?"w":"e")+"-"+(l==0?5:l==1?3:1));if(stub<0)continue;
                    if(shell>=0)Span(shell,side*47.9,side*47.98,TB,3.8,z0+.05,z1-.05,L("plain",BlackHex,RoleWall));
                    var brick=L("brick-red",0x3A2620,RoleWall);
                    Span(stub,xf,xf-side*.12,3.8,4.35,z0-.35,z1+.35,brick);
                    foreach(double zz in new[]{z0-.35,z1})Span(B("end-"+(side<0?"w":"e")+"-"+(l==0?(zz<z0?4:6):l==1?(zz<z0?2:4):(zz<z0?0:2))),xf,xf-side*.12,TB,3.8,zz,zz+.35,brick);
                    for(int r=0;r<3;r++){double x=side*(44.6+r*1.2);Span(stub,x-.08,x+.08,3.55,3.8,z0+.05,z1-.05,With(L("concrete-board",0x2A2826,RoleWall),detail:true));}
                    // The signal head on the end wall north of the mouth: red over green, a hood over each.
                    int wall=B("end-"+(side<0?"w":"e")+"-"+(l==0?6:l==1?4:2));if(wall<0)continue;double sz=z1+.65;double yaw=side<0?90:270;
                    Begin();Span(wall,xf,xf-side*.2,1.85,3.25,sz-.22,sz+.22,L("plain",0x101010,RoleWall));
                    Box(wall,xf-side*.21,2.95,sz,.2,.2,.03,Glow(RedHex,3.2f,RoleWall,DriveSignal,l,0),yaw);Box(wall,xf-side*.21,2.5,sz,.2,.2,.03,Glow(SignalGreenHex,3.2f,RoleWall,DriveSignal,l,1),yaw);
                    Box(wall,xf-side*.23,3.08,sz,.26,.03,.06,With(L("plain",0x101010,RoleWall),detail:true),yaw);Box(wall,xf-side*.23,2.63,sz,.26,.03,.06,With(L("plain",0x101010,RoleWall),detail:true),yaw);End();
                    Light(xf-side*.6,2.7,sz,RedHex,.5,6,.35,GlowSignal,l,TB,(short)(l*2+(side<0?0:1)));
                    // NO ENTRY over the mouth (the express line's plates in full; the others detail).
                    Span(stub,xf-side*.12,xf-side*.15,4.5,5.0,zc-.95,zc+.95,With(L("plain",0xB0201A,RoleSign),detail:l>0));
                    Text(stub,"NO ENTRY",xf-side*.17,4.75,zc,yaw,.045,.01,With(L("plain",0xF0ECE0,RoleSign),detail:l>0));
                    if(l!=0)continue;
                    // The express line: beacons either side of the mouth, STAND BACK at the platform's end.
                    foreach(double bz in new[]{z1+.8,z0-.8})
                    {
                        int bw=B("end-"+(side<0?"w":"e")+"-"+(bz>z1?6:4));if(bw<0)continue;Begin();
                        Span(bw,xf,xf-side*.3,3.95,4.25,bz-.15,bz+.15,L("plain",0x1A1A1A,RoleWall));Box(bw,xf-side*.15,4.42,bz,.22,.3,.22,Glow(BeaconHex,2.6f,RoleWall,DriveBeacon,(float)Hash("beacon",side,(int)bz)));
                        Box(bw,xf-side*.15,4.42,bz,.26,.32,.26,With(L("plain",0x2A2A2A,RoleWall),detail:true));End();
                        Light(xf-side*.6,4.4,bz,BeaconHex,1.2,13,.6,GlowBeacon,Hash("beacon",side,(int)bz),0,(short)(side<0?(bz>z1?0:1):(bz>z1?2:3)));
                    }
                    int pw=B("end-"+(side<0?"w":"e")+"-6");if(pw<0)continue;double pz=-7.0;
                    Begin();Span(pw,xf,xf-side*.05,2.15,2.75,pz-1.25,pz+1.25,L("plain",0xB0201A,RoleSign));Span(pw,xf-side*.05,xf-side*.07,2.15,2.75,pz-1.25,pz+1.25,With(Glow(0xFF4A3A,.8f,RoleSign,DriveDanger),level:1));
                    Text(pw,"STAND BACK",xf-side*.09,2.45,pz,yaw,.05,.012,L("plain",0xF4F0E8,RoleSign));End();
                }
            }
            // The headlight's glow in the west mouth of the express line through the warning (the train behind it in the dark).
            int sw=Side("shell-w","shell-e",-1);if(sw>=0)foreach(double dz in new[]{-.75,.75})Box(sw,-47.88,TB+1.15,NativeExpeditionStation.LineZ[0]+dz,.04,.34,.34,Glow(HeadlightHex,7f,RoleTrain,DriveMouth));
        }

        // ---------------- the tracks ----------------
        private void Tracks()
        {
            int bed=B("track-bed");if(bed<0)return;var steel=L("cyber-metal",0x5A5E60,RoleTrack);var head=L("plain",0xA8AEB0,RoleTrack);var sleeper=With(L("timber",0x3A2E24,RoleTrack),detail:true);
            for(int l=0;l<3;l++)
            {
                double zc=(PitZ0[l]+PitZ1[l])/2;
                foreach(double dz in new[]{-.72,.72}){Box(bed,0,TB+.08,zc+dz,96,.16,.07,steel);Box(bed,0,TB+.168,zc+dz,96,.016,.074,head);}
                // Sleepers (full tier) and their stand-in strip (low tier); none under the stalled train, the flood or a crossing.
                double runA=-47.6;
                for(double x=-47.6;x<=47.61;x+=.7)
                {
                    bool skip=l==1&&x>-30.6&&x<25||l==2&&x<-19.6||l!=1&&Math.Abs(x)<1.7;
                    if(!skip)Box(bed,x,TB+.05,zc,.26,.1,2.5,sleeper);
                    if(skip||x+.7>47.61){double runB=skip?x-.35:x+.35;if(runB-runA>1)Box(bed,(runA+runB)/2,TB+.04,zc,runB-runA,.08,2.4,With(L("timber",0x2E241C,RoleTrack),tier:TierLow));runA=x+.35;}
                }
                // Puddles on the ballast (dry under the train and in the flood).
                for(int k=0;k<6;k++){double x=-40+k*15+Hash("ballast",l,k)*5;if(l==1&&x>-31&&x<25||l==2&&x<-19)continue;double r=.9+Hash("ballast",l,k+9),pz=zc+(Hash("ballast",l,k+3)-.5)*1.6;Water(x-r,x+r,pz-.5,pz+.5,TB+.006,WaterPuddle);}
                // Cables along both pit walls.
                foreach(double wz in new[]{PitZ0[l]+.05,PitZ1[l]-.05})Box(bed,0,-.82,wz,88,.06,.06,With(L("plain",0x161616,RoleTrack),detail:true));
            }
            // The live rail at each pit's centre on white insulators, the yellow guard board over it, a blue glint on each
            // insulator while its section is live; its ends ramp down.
            for(int s=0;s<NativeExpeditionStation.RailSections;s++)
            {
                int l=(int)RailLine[s]-1;double zc=(PitZ0[l]+PitZ1[l])/2,x0=RailFrom[s],x1=RailTo[s];RailX0[s]=(float)x0;RailX1[s]=(float)x1;RailZ[s]=(float)zc;
                Box(bed,(x0+x1)/2,TB+.225,zc,x1-x0,.09,.09,L("cyber-metal",0x8A9092,RoleTrack));
                Box(bed,(x0+x1)/2,TB+.3,zc,x1-x0,.025,.32,L("plain",0xC8A010,RoleTrack));
                int n=Math.Max(2,(int)((x1-x0)/3));
                for(int k=0;k<=n;k++){double x=x0+(x1-x0)*k/n;Box(bed,x,TB+.09,zc,.14,.18,.14,L("plain",0xD8D4C8,RoleTrack));Box(bed,x,TB+.2,zc+.11,.06,.06,.06,Glow(0x7FD8FF,1.8f,RoleTrack,DriveRail,(float)Hash("rail",s,k),(short)s));}
                foreach(double end in new[]{x0,x1}){double sg=end==x0?-1:1;Box(bed,end+sg*.45,TB+.17,zc,.9,.05,.09,With(L("cyber-metal",0x8A9092,RoleTrack),detail:true),0,0,sg*-8);}
            }
            // The barrow crossings: hazard paint on the bed either side of the boards.
            foreach(int l in new[]{0,2}){double zc=(PitZ0[l]+PitZ1[l])/2;foreach(double sx in new[]{-1.75,1.75})Box(bed,sx,TB+.004,zc,.25,.008,4.6,L("demolition",0,RoleTrack));}
        }

        // ---------------- the stalled train, the wagon, the flood ----------------
        private short SpawnId(string suffix)
        {
            var spawns=area?.spawns;if(spawns==null)return -1;
            for(short s=0;s<spawns.Length;s++){var id=spawns[s]?.entry?.id;if(id!=null&&id.EndsWith(suffix,StringComparison.Ordinal))return s;}
            return -1;
        }
        private void Train()
        {
            var band=L("plain",0xA0282A,RoleTrain);var under=L("plain",0x1A1C1E,RoleTrain);var dark=L("plain",0x0C1418,RoleTrain);var leaf=L("cyber-panel",0x9AA4A8,RoleSpawn);
            foreach(var (stem,doors,label) in new[]{("train-car1",new[]{-26.0,-17.0},"car1"),("train-car3",new[]{11.0,20.0},"car3")})
            {
                int c=B(stem);if(c<0)continue;var k=blocks[c];double x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,zn=k.pos.z+k.size.z/2,zs=k.pos.z-k.size.z/2,top=k.pos.y+k.size.y/2;
                foreach(var (face,sg,sd) in new[]{(zn,1.0,"n"),(zs,-1.0,"s")})
                {
                    // Between the doors: the underframe, the red band, the windows (a third lit and flickering).
                    var cuts=new List<double>{x0+.2};foreach(double d in doors){cuts.Add(d-1.4);cuts.Add(d+1.4);}cuts.Add(x1-.2);
                    for(int j=0;j+1<cuts.Count;j+=2)
                    {
                        double a=cuts[j],b=cuts[j+1];if(b-a<.3)continue;
                        Span(c,a,b,TB+.3,0,face,face+sg*.02,under);Span(c,a,b,.9,1.05,face,face+sg*.02,band);
                        int n=(int)((b-a)/1.6);for(int w=0;w<n;w++){double x=a+(b-a)*(w+.5)/n,h=Hash(stem+sd,w,j);bool lit=h<.32;
                            Box(c,x,1.8,face+sg*.012,1.25,1.1,.02,lit?With(Glow(0xBFE8E0,.6f,RoleTrain,DriveWindow,(float)h*13),level:1):dark);}
                    }
                    // The doors (the train-door spawns: they come out here), slid open on a dark vestibule.
                    for(int d=0;d<doors.Length;d++)
                    {
                        owner=SpawnId("train-"+label+"-"+sd+d);Begin();
                        Box(c,doors[d],1.08,face+sg*.012,1.3,2.15,.02,L("plain",0x050607,RoleSpawn));
                        Box(c,doors[d]-1.0,1.08,face+sg*.03,.68,2.1,.03,With(leaf,tag:-1));Box(c,doors[d]+1.0,1.08,face+sg*.03,.68,2.1,.03,leaf);
                        Box(c,doors[d],2.2,face+sg*.03,1.5,.08,.04,With(L("plain",0x1A1C1E,RoleSpawn),detail:true));
                        End();owner=-1;
                    }
                    // NOT IN SERVICE on the destination display.
                    double dx=sd=="n"?x0+2.1:x1-2.1;Box(c,dx,2.72,face+sg*.012,2.4,.32,.02,L("plain",0x050505,RoleTrain));
                    Text(c,"NOT IN SERVICE",dx,2.72,face+sg*.03,sg>0?0:180,.035,.01,Glow(AmberHex,2.0f,RoleTrain,DriveFlicker,(float)Hash(stem+sd)));
                }
                // The roof's pods; the cab ends (car 1's west, car 3's east): windscreen, dead lamps, the yellow panel.
                Span(c,x0+1.5,x0+4.5,top,top+.35,k.pos.z-1.1,k.pos.z+1.1,L("cyber-metal",0x6A7478,RoleTrain));Span(c,x1-4.5,x1-1.5,top,top+.35,k.pos.z-1.1,k.pos.z+1.1,L("cyber-metal",0x6A7478,RoleTrain));
                bool west=stem=="train-car1";double xe=west?x0:x1,se=west?-1:1;
                Span(c,xe,xe+se*.02,1.75,2.7,k.pos.z-1.3,k.pos.z+1.3,Glow(0x0C141C,.5f,RoleTrain));Span(c,xe,xe+se*.02,.15,1.35,k.pos.z-1.5,k.pos.z+1.5,L("plain",0xC8A020,RoleTrain));
                foreach(double lz in new[]{-1.1,1.1})Box(c,xe+se*.03,.6,k.pos.z+lz,.03,.18,.28,west?L("plain",0x2A2A26,RoleTrain):Glow(0xFF2A1A,.6f,RoleTrain,DriveFlicker,(float)lz));
            }
            // Car 2: lit and walked through. Outside: the band, windows (all lit, stuttering), the doors' leaves slid aside.
            // Inside: three ceiling tubes (one dead, one stuttering), moquette seats with backs, ads, grab rails, the floor runner.
            int floor2=B("train-car2-floor"),roof2=B("train-car2-roof");if(floor2<0||roof2<0)return;double cz=blocks[floor2].pos.z,c2x0=blocks[floor2].pos.x-blocks[floor2].size.x/2,c2x1=blocks[floor2].pos.x+blocks[floor2].size.x/2;
            foreach(var (sd,sg) in new[]{("n",1.0),("s",-1.0)})
            {
                for(int j=0;j<5;j++)
                {
                    int w=B("train-car2-side-"+sd+"-"+j);if(w<0)continue;var k=blocks[w];if(k.size.y<2)continue;double a=k.pos.x-k.size.x/2,b=k.pos.x+k.size.x/2,fo=k.pos.z+sg*k.size.z/2,fi=k.pos.z-sg*k.size.z/2;
                    Span(w,a,b,.9,1.05,fo,fo+sg*.02,band);
                    int n=Math.Max(1,(int)((b-a)/1.6));for(int q=0;q<n;q++){double x=a+(b-a)*(q+.5)/n,h=Hash("car2"+sd,j,q);Box(w,x,1.75,fo+sg*.012,Math.Min(1.25,(b-a)/n-.2),1.05,.02,With(Glow(0xD8F0E8,.8f,RoleTrain,DriveWindow,(float)h*13),level:1));
                        Box(w,x,1.6,fi-sg*.012,Math.Min(1.25,(b-a)/n-.2),.9,.02,Glow(0x0A1218,.35f,RoleTrain));}
                    Span(w,a+.1,b-.1,2.25,2.6,fi,fi-sg*.015,L("plain",new[]{0xB8402A,0x2A6AA8,0xD8C060,0x3A8A5A,0x8A3A7A}[j],RoleTrain));
                    Span(w,a,b,2.22,2.26,fi-sg*.2,fi-sg*.24,With(L("cyber-metal",0xB8C0C0,RoleTrain),detail:true));
                }
            }
            for(int s=0;s<3;s++)
                foreach(var sd in new[]{"n","s"})
                {
                    int seat=B("train-car2-seats-"+sd+s);if(seat<0)continue;var k=blocks[seat];double top=k.pos.y+k.size.y/2,wall=sd=="n"?k.pos.z+k.size.z/2:k.pos.z-k.size.z/2;double sg=sd=="n"?1:-1;
                    Box(seat,k.pos.x,top+.04,k.pos.z,k.size.x-.06,.08,k.size.z-.04,L("plain",0x2A3A6A,RoleFurniture));Box(seat,k.pos.x,top+.3,wall-sg*.05,k.size.x-.06,.5,.08,L("plain",0x24325C,RoleFurniture),0,sg*-6);
                }
            for(int t=0;t<3;t++){double x=-8.5+t*6,h=Hash("tube",t);Box(roof2,x,2.86,cz,2.2,.06,.3,With(L("ward-light",FluorHex,RoleTrain),drive:t==1?DriveFluor:DriveSteady,level:t==2?0:1));}
            Light(-5.5,2.4,cz,0xD8F0E8,.85,8,0,GlowTube,0,0);Light(.5,2.4,cz,0xD8F0E8,.6,8,0,GlowTubeFlicker,.4,0);
            Span(floor2,c2x0+.3,c2x1-.3,0,.006,cz-.45,cz+.45,L("plain",0x1A2A3A,RoleFlat));
            foreach(var (stem,sg) in new[]{("train-car2-end-w",1.0),("train-car2-end-e",-1.0)}){int e=B(stem);if(e<0)continue;var k=blocks[e];double xf=k.pos.x+sg*k.size.x/2;Span(e,xf,xf+sg*.02,.05,2.1,cz-.45,cz+.45,L("plain",0x1A1E22,RoleTrain));}
            foreach(var stem in new[]{"train-coupling-0","train-coupling-1"}){int cp=B(stem);if(cp<0)continue;var k=blocks[cp];Box(cp,k.pos.x,1.6,k.pos.z,k.size.x+.02,2.6,k.size.z-.2,With(L("plain",0x161616,RoleTrain),detail:true));}
            Wagon();
        }
        private void Wagon()
        {
            int w=B("wagon");if(w<0)return;var k=blocks[w];double x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,z0=k.pos.z-k.size.z/2,z1=k.pos.z+k.size.z/2,top=k.pos.y+k.size.y/2;
            Span(w,x0-.1,x1+.1,top,top+.08,z0-.1,z1+.1,L("steel-green",0x3A2A20,RoleTrain));
            Box(w,k.pos.x+.6,top+.25,k.pos.z,7.2,.36,3.4,L("plain",0x4A4A30,RoleTrain),3,0,2);
            foreach(double z in new[]{z0,z1}){double sg=z==z0?-1:1;for(int j=0;j<6;j++)Span(w,x0+.6+j*1.6,x0+.75+j*1.6,-.6,top-.1,z,z+sg*.03,With(L("plain",RustHex,RoleTrain),detail:true));Span(w,x0+.3,x1-.3,-.85,-.55,z,z+sg*.06,L("plain",0x1A1410,RoleTrain));}
            // Litter on the water round it.
            int bed=B("track-bed");if(bed<0)return;
            for(int j=0;j<10;j++){double x=-43+Hash("float",j)*22,z=-46.6+Hash("float",j,1)*4.2;if(x>x0-.3&&x<x1+.3&&z>z0-.3&&z<z1+.3)continue;Box(bed,x,NativeExpeditionStation.WaterY+.012,z,.2+Hash("float",j,2)*.4,.02,.15+Hash("float",j,3)*.3,With(L("plain",j%3==0?PaperHex:0x3A3A34,RoleWater),detail:true),Hash("float",j,4)*180);}
        }
        private void FloodWater()
        {
            int bed=B("track-bed");if(bed<0)return;
            foreach(var f in Flood)
            {
                Box(bed,(f[0]+f[1])/2,NativeExpeditionStation.WaterY,(f[2]+f[3])/2,f[1]-f[0],.02,f[3]-f[2],With(Glow(WaterHex,1.4f,RoleWater,DriveFlood),level:1));
                Water(f[0],f[1],f[2],f[3],NativeExpeditionStation.WaterY+.01,WaterFlood);
            }
            Light(-36,NativeExpeditionStation.WaterY+.3,-44.5,0x2EF0C0,.35,7,0,GlowWater,0,TB);
        }

        // ---------------- the footbridges ----------------
        private void Bridges()
        {
            FloodWater();
            var iron=With(L("steel-green",0x24342E,RoleHigh),shadow:true);var thin=L("steel-green",0x24342E,RoleWall);
            foreach(var (stem,sg) in new[]{("bridge-w",-1),("bridge-e",1)})
            {
                int b=B(stem);if(b<0)continue;var k=blocks[b];double x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,z0=k.pos.z-k.size.z/2,z1=k.pos.z+k.size.z/2,bot=k.pos.y-k.size.y/2;
                for(double z=z0+2;z<z1-.5;z+=4)Span(b,x0,x1,bot-.32,bot,z-.15,z+.15,iron);
                Span(b,x0-.04,x0,bot,bot+.5,z0,z1,L("steel-green",0x2E463C,RoleHigh));Span(b,x1,x1+.04,bot,bot+.5,z0,z1,L("steel-green",0x2E463C,RoleHigh));
                // Lamps on the outer parapet, half lit.
                int outer=B(stem+"-parapet-o");if(outer>=0)
                {
                    var o=blocks[outer];double top=o.pos.y+o.size.y/2;
                    foreach(double z in sg<0?new[]{-16.0,-30.0,-44.0}:new[]{-16.0,-30.0,-43.0}){double h=Hash("bridgelamp",sg,(int)z);bool lit=h<.5||z<-42;Box(outer,o.pos.x,top+.6,z,.08,1.2,.08,With(thin,fade:true));Box(outer,o.pos.x,top+1.33,z,.34,.34,.34,lit?Glow(0xFFE8C0,1.6f,RoleLamp):L("plain",0x3A3630,RoleLamp));if(lit)Light(o.pos.x,top+1.15,z,0xFFD8A0,.65,9,.7,GlowGlobe,h,6);}
                    Box(outer,o.pos.x,top+.03,o.pos.z,.3,.06,o.size.z,L("timber",0x3A2A1E,RoleWall));
                }
                // The lattice on the shed side of the inner parapet (detail), its handrail.
                for(int i=0;i<5;i++)
                {
                    int p=B(stem+"-parapet-i-"+i);if(p<0)continue;var q=blocks[p];double top=q.pos.y+q.size.y/2,face=q.pos.x-sg*q.size.x/2,pz0=q.pos.z-q.size.z/2,pz1=q.pos.z+q.size.z/2;
                    Box(p,q.pos.x,top+.03,q.pos.z,.3,.06,q.size.z,L("timber",0x3A2A1E,RoleWall));
                    for(double z=pz0+.6;z<pz1-.4;z+=1.25)Box(p,face-sg*.02,q.pos.y,z,.03,1.25,.05,With(thin,detail:true),90,0,(Math.Round(z*.8)%2==0?40:-40));
                }
            }
            foreach(var kv in byStem){if(!kv.Key.StartsWith("pier-",StringComparison.Ordinal))continue;var k=blocks[kv.Value];Box(kv.Value,k.pos.x,.12,k.pos.z,k.size.x+.2,.24,k.size.z+.2,With(L("steel-green",0x1E2C26,RoleWall),detail:true));Box(kv.Value,k.pos.x,5.3,k.pos.z,k.size.x+.24,.4,k.size.z+.24,With(L("steel-green",0x1E2C26,RoleHigh),detail:true));}
        }

        // ---------------- the corridor ----------------
        private void Corridor()
        {
            int shell=B("shell-s"),floor=B("floor-corridor");if(shell<0||floor<0)return;double zw=-60;
            // Pipes along the outer wall (green, brown, grey; the steam main the lowest, burst at the hazard), brackets.
            foreach(var (y,hex) in new[]{(3.35,0x6A6A60),(3.75,0x4A3A2A),(4.12,0x2A4A3A)})Box(shell,0,y,zw+.13,88,.22,.22,L("steel-green",hex,RoleWall));
            for(double x=-42;x<=42;x+=4)Box(shell,x,3.75,zw+.06,.08,1.1,.1,With(L("steel-green",0x1A1A18,RoleWall),detail:true));
            SteamMain[0]=-6;SteamMain[1]=3.35f;SteamMain[2]=(float)(zw+.24);
            Box(shell,-6.3,3.35,zw+.13,.3,.34,.34,L("steel-green",0x4A4A40,RoleWall),0,0,18);Box(shell,-5.7,3.32,zw+.15,.28,.3,.32,L("steel-green",0x4A4A40,RoleWall),0,0,-24);
            Box(shell,-4.2,3.6,zw+.3,.04,.42,.42,With(L("plain",0xA0201A,RoleWall),detail:true));
            // The tray on the inner wall, red strips on the ceiling every 6 m (stuttering), wall boxes and warning plates.
            int inner=B("wall-s-3");if(inner>=0){double zi=-54;Box(inner,0,3.95,zi-.15,86,.08,.3,L("steel-green",0x3A3E3A,RoleWall));Box(inner,0,4.0,zi-.12,86,.05,.18,With(L("plain",0x161616,RoleWall),detail:true));}
            for(double x=-40;x<=40.01;x+=6)
            {
                if(x>13.5&&x<26.5)continue;int c=B(x<20?"corridor-ceiling-0":"corridor-ceiling-3");if(c<0)continue;double h=Hash("corridorstrip",(int)x);
                Box(c,x,4.47,-57,1.0,.05,.14,Glow(0xFF3A2A,h<.2?.4f:1.9f,RoleHigh,DriveStrip,(float)h*9));
                if(((int)Math.Round((x+40)/6))%2==0)Light(x,4.1,-57,0xFF3A2A,.7,10,.4,GlowStrip,h*9,0);
            }
            foreach(double x in new[]{-20.0,4.0,30.0}){int w=x<-12?B("wall-s-0"):x<10?B("wall-s-3"):B("wall-s-6");if(w<0)continue;Begin();Box(w,x,1.6,-54.12,.6,.8,.22,L("cyber-metal",0x5A6060,RoleWall));Box(w,x,1.75,-54.24,.12,.12,.02,With(L("plain",YellowHex,RoleWall),detail:true),0,0,45);End();}
            // The pump room: water over its floor, the pumps' flanges; the generator's amber lamp, its panel and exhaust.
            Box(floor,-39.2,.012,-57,17.6,.024,6,With(Glow(WaterHex,.45f,RoleFlat),level:1));Water(-48,-30.4,-60,-54,.024,WaterPump);
            int pumps=B("pumps");if(pumps>=0){var k=blocks[pumps];foreach(double dx in new[]{-1.2,1.2})Box(pumps,k.pos.x+dx,k.pos.y,k.pos.z,.25,k.size.y+.1,k.size.z+.1,L("steel-green",0x1E3A2E,RoleWall));}
            int gen=B("generator");if(gen>=0)
            {
                var k=blocks[gen];double top=k.pos.y+k.size.y/2,zf=k.pos.z+k.size.z/2;
                Box(gen,k.pos.x+2.2,top+.14,k.pos.z,.28,.28,.28,Glow(0xFFA31A,2.4f,RoleWall,DriveFlicker,.61f));Light(k.pos.x+2.2,top+.5,k.pos.z+1.0,0xFFA31A,.65,8,.4,GlowGenerator,.61,0);
                var scr=L("cyber-holo",0x3AB04A,RoleWall);scr.Gain=.5f;Box(gen,k.pos.x-1.5,1.5,zf+.015,1.0,.6,.03,scr);
                Box(shell,k.pos.x-2.6,3.0,zw+.14,.3,3.0,.3,L("steel-green",0x2A2A26,RoleWall));
            }
            for(int k=0;k<6;k++){double x=-26+k*12+Hash("wet",k)*4,z=-57+(Hash("wet",k,1)-.5)*2,r=.8+Hash("wet",k,2)*.5;Water(x-r,x+r,z-r*.6,z+r*.6,.006,WaterPuddle);}
        }

        // ---------------- the signal box ----------------
        private void SignalBox()
        {
            int n0=B("signal-wall-n-0"),n2=B("signal-wall-n-2"),roof=B("signal-roof");if(n0<0||n2<0)return;double zo=-50.0,zi=-50.25;
            var red=With(Glow(0xB8301C,.62f,RoleHigh,DriveBox,.3f),level:1);var mull=L("steel-green",0x2A3A32,RoleHigh);
            foreach(var (w,a,b,n) in new[]{(n0,30.2,32.2,1),(n2,35.8,41.8,3)})
            {
                double step=(b-a)/n;for(int k=0;k<n;k++){double x=a+step*(k+.5);Box(w,x,8.0,zo+.012,step-.12,1.9,.02,red);Box(w,x,8.0,zi-.012,step-.12,1.9,.02,Glow(0x0A0E12,.3f,RoleHigh));}
                for(int k=0;k<=n;k++)Box(w,a+step*k,8.0,zo+.04,.08,2.0,.06,mull);
                for(int k=0;k<n;k++)for(int j=1;j<3;j++)Box(w,a+step*(k+j/3.0),8.0,zo+.03,.035,1.9,.04,With(mull,detail:true));
                foreach(double my in new[]{7.6,8.3})Span(w,a,b,my-.018,my+.018,zo,zo+.04,With(mull,detail:true));Span(w,a-.05,b+.05,6.95,7.05,zo,zo+.14,L("concrete-board",0x8A8276,RoleHigh));Span(w,a-.05,b+.05,8.95,9.05,zo,zo+.08,mull);
            }
            int w2=B("signal-wall-w-2");if(w2>=0)foreach(double z in new[]{-55.2,-52.4})Box(w2,29.99,8.0,z,.02,1.9,2.2,red);
            if(roof>=0){Span(roof,30.3,41.7,9.85,10.45,-50.02,-49.94,L("steel-green",0x1E2E2A,RoleSign));Text(roof,"CENTRAL SIGNAL BOX",36,10.15,-49.92,0,.062,.012,Glow(0xE8E0C8,.55f,RoleSign));}
            // The lever frame: sixteen levers in the station's colours, home (leaning away) or thrown (pulled toward the
            // operator) by the levers' state; the frame's plate.
            int lev=B("signal-levers");if(lev>=0)
            {
                var k=blocks[lev];double top=k.pos.y+k.size.y/2,lz=k.pos.z;int[] colors={0xC02A20,0xC02A20,0x2A5AC0,0xE8C020,0x101010,0xC02A20,0x2A5AC0,0xC02A20};
                Box(lev,k.pos.x,top+.02,lz,k.size.x+.1,.04,k.size.z+.1,L("steel-green",0x2A2A26,RoleFurniture));
                for(int i=0;i<16;i++)
                {
                    double x=k.pos.x-k.size.x/2+.3+i*(k.size.x-.6)/15;int col=colors[i%colors.Length];
                    for(int pose=0;pose<2;pose++)
                    {
                        double a=(pose==0?-18:24)*Math.PI/180,dy=Math.Cos(a),dz=Math.Sin(a);
                        Box(lev,x,top+dy*.45,lz+dz*.45,.06,.9,.06,With(L("plain",col,RoleFurniture),drive:DriveLever,tag:(short)pose),0,pose==0?-18:24);
                        Box(lev,x,top+dy*.92,lz+dz*.92,.08,.1,.08,With(L("plain",0xC8A040,RoleFurniture),drive:DriveLever,tag:(short)pose),0,pose==0?-18:24);
                    }
                }
            }
            // The track diagram on the far wall: the express route (white through the warning), the live sections in red.
            int sw=B("signal-wall-s");if(sw>=0)
            {
                double zf=-59.75+.015;Box(sw,36,8.15,zf,9.6,1.7,.02,Glow(0x08142A,.6f,RoleWall));
                double[] ly={8.65,8.15,7.65};
                for(int s=0;s<NativeExpeditionStation.RailSections;s++){int l=(int)RailLine[s]-1;double a=31.6+(RailFrom[s]+48)/96*8.8,b=31.6+(RailTo[s]+48)/96*8.8;Box(sw,(a+b)/2,ly[l],zf+.015,b-a,.05,.01,Glow(0xFF3A2A,1.6f,RoleWall,DriveDiagram,0,(short)s),180);}
                Box(sw,36,ly[0]+.1,zf+.015,8.8,.025,.01,Glow(0xF4F8FF,1.6f,RoleWall,DriveDiagram,0,100),180);
                for(int p=0;p<4;p++)Box(sw,36,8.9-p*.5,zf+.012,7.6,.12,.01,Glow(0xE0E8F0,.5f,RoleWall),180);
            }
            // The box's lamp: a red-shaded pendant over the frame.
            if(roof>=0){Box(roof,36,9.36,-54.8,.6,.18,.6,L("steel-green",0x3A1A14,RoleHigh));Box(roof,36,9.255,-54.8,.44,.03,.44,Glow(RedHex,2.0f,RoleHigh,DriveBox,.7f));}
            Light(36,8.9,-54.6,RedHex,1.0,12,1.2,GlowBox,.7,6);
        }

        // ---------------- the entrances ----------------
        private void Spawns()
        {
            var spawns=area?.spawns??Array.Empty<ExpeditionSpawnLayout>();
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.from==null||e.exit==null)continue;owner=s;
                if(e.kind=="door")
                {
                    if(e.id!=null&&e.id.Contains("train-"))continue;int wall=Containing(e.from.x,e.from.y+1,e.from.z);if(wall<0)continue;var b=blocks[wall];
                    double dx=e.exit.x-e.from.x,dz=e.exit.z-e.from.z;bool alongX=Math.Abs(dx)>Math.Abs(dz);if(!alongX)continue;double sg=Math.Sign(dx),face=b.pos.x+sg*b.size.x/2,y=e.from.y;double yaw=sg>0?90:270;
                    Begin();
                    Span(wall,face,face+sg*.015,y,y+2.48,e.from.z-.65,e.from.z+.65,L("plain",0x050506,RoleSpawn));
                    Span(wall,face,face+sg*.14,y,y+2.64,e.from.z-.79,e.from.z-.65,L("concrete-board",0x5A544C,RoleSpawn));Span(wall,face,face+sg*.14,y,y+2.64,e.from.z+.65,e.from.z+.79,L("concrete-board",0x5A544C,RoleSpawn));
                    Span(wall,face,face+sg*.14,y+2.48,y+2.66,e.from.z-.79,e.from.z+.79,L("concrete-board",0x5A544C,RoleSpawn));
                    double side=Hash(e.id)<.5?-1:1;Box(wall,face+sg*.06,y+1.1,e.from.z+side*1.25,.9,2.2,.06,With(L("timber",0x3A2A1E,RoleSpawn),detail:true),yaw,0,side*4);
                    Box(wall,face+sg*.02,y+1.9,e.from.z-side*1.2,.42,.2,.01,With(L("plain",0xE0D8C0,RoleSpawn),detail:true),yaw);
                    End();
                }
                else if(e.kind=="vent"&&e.from.y>-.5)
                {
                    int g=FloorBlock(e.from.x,e.from.y,e.from.z);if(g<0)continue;double y=e.from.y;
                    Begin();Box(g,e.from.x,y+.012,e.from.z,1.0,.024,1.0,L("cyber-grate",0x4A4E50,RoleSpawn));
                    foreach(var (ox,oz,sx,sz) in new[]{(0.0,-.56,1.2,.12),(0.0,.56,1.2,.12),(-.56,0.0,.12,1.0),(.56,0.0,.12,1.0)})Box(g,e.from.x+ox,y+.008,e.from.z+oz,sx,.016,sz,L("steel-green",0x2A2C2C,RoleSpawn));End();
                }
                else if(e.kind=="vent")Light(e.from.x,NativeExpeditionStation.WaterY-.1,e.from.z,0x2EF0C0,.25,4,.5,GlowWater,s,TB);
                else if(e.kind=="breach"&&!string.IsNullOrEmpty(e.block))
                {
                    int p=B(Stem(e.block)??"");if(p<0)continue;var k=blocks[p];double sg=Math.Sign(e.exit.x-e.from.x),face=k.pos.x+sg*k.size.x/2,top=k.pos.y+k.size.y/2,z0=k.pos.z-k.size.z/2,z1=k.pos.z+k.size.z/2;
                    Begin();var stone=L("concrete-board",0x8A8276,RoleSpawn);
                    double wall=face-sg*k.size.x;Span(p,wall,face+sg*.02,0,top+.1,z0-.22,z0,stone);Span(p,wall,face+sg*.02,0,top+.1,z1,z1+.22,stone);Span(p,wall,face+sg*.02,top,top+.45,z0-.22,z1+.22,stone);
                    Box(p,face+sg*.04,top+.22,k.pos.z,.04,.5,.4,stone);
                    Box(p,face+sg*.04,1.6,k.pos.z,2.6,.12,.03,With(L("timber",0x5A4030,RoleSpawn),detail:true),sg>0?90:270,0,25);Box(p,face+sg*.06,1.6,k.pos.z,2.6,.12,.03,With(L("timber",0x5A4030,RoleSpawn),detail:true),sg>0?90:270,0,-25);
                    End();
                }
            }
            owner=-1;
        }
        // The block whose box holds a world point (a wall a door spawn starts inside), or -1.
        private int Containing(double x,double y,double z)
        {
            for(int i=0;i<blocks.Length;i++){var b=blocks[i];if(Stem(b?.id)==null)continue;if(Math.Abs(x-b.pos.x)<=b.size.x/2+.01&&Math.Abs(y-b.pos.y)<=b.size.y/2+.01&&Math.Abs(z-b.pos.z)<=b.size.z/2+.01&&b.size.y>1.5)return i;}
            return -1;
        }
        // The floor block whose top is at height y under (x, z), or -1.
        private int FloorBlock(double x,double y,double z)
        {
            for(int i=0;i<blocks.Length;i++){var b=blocks[i];if(Stem(b?.id)==null)continue;if(Math.Abs(b.pos.y+b.size.y/2-y)<.06&&Math.Abs(x-b.pos.x)<=b.size.x/2&&Math.Abs(z-b.pos.z)<=b.size.z/2)return i;}
            return -1;
        }

        // ---------------- the express ----------------
        // Three stainless cars in the train's own frame (+x forward, the nose at 0; y over the track bed; z across the pit):
        // the body with its lit window band (it strobes as it passes), the red line, the skirt and underframe, bogies, the
        // gangways; a tapered nose in the yellow warning paint with its windscreen, two headlights and EXPRESS over it; the tail
        // lights. Posed by the state's nose (<see cref="ExpressPlace"/>); drawn only while it is in the station.
        private void BuildExpress()
        {
            void T(double x,double y,double z,double sx,double sy,double sz,Pen pen,double yaw=0,double pitch=0,double roll=0)
            {
                var q=Euler(yaw,pitch,roll);pen.Drive=DriveExpress;
                express.Add(new NativeStationPart{Parent=-1,Look=pen.Look,Flags=pen.Flags,Color=pen.Color,X=(float)x,Y=(float)y,Z=(float)z,Hx=(float)(sx/2),Hy=(float)(sy/2),Hz=(float)(sz/2),Qx=(float)q.X,Qy=(float)q.Y,Qz=(float)q.Z,Qw=(float)q.W,
                    Gain=pen.Gain,Level=pen.Level,Param=pen.Param,Drive=DriveExpress,Role=RoleTrain,Tier=pen.Tier,Tag=pen.Tag,Shadow=pen.Shadow,Room=RoomShed});
            }
            var body=With(L("cyber-panel",0xA8B0B4,RoleTrain),shadow:true);var roof=L("cyber-metal",0x5A6268,RoleTrain);var dark=L("plain",0x161819,RoleTrain);
            var band=With(Glow(0xE0F4FF,1.3f,RoleTrain),tag:1,level:1);var red=L("plain",0xB0201A,RoleTrain);var yellow=L("plain",0xE8C020,RoleTrain);
            float car=NativeExpeditionStation.CarLength,gap=NativeExpeditionStation.CarGap;
            for(int c=0;c<3;c++)
            {
                double x1=-c*(car+gap),x0=x1-car,b0=x0+(c==2?.0:0),b1=x1-(c==0?2.6:0),mid=(b0+b1)/2,len=b1-b0;
                T(mid,2.2,0,len,3.5,2.9,body);T(mid,4.02,0,len-.3,.16,2.5,roof);T(mid,3.93,0,len,.12,2.9,roof);
                foreach(double s in new[]{-1.0,1.0})
                {
                    T(mid,2.45,s*1.455,len-1.2,.9,.03,band);
                    for(double x=b0+1.0;x<b1-.6;x+=1.6)T(x,2.45,s*1.47,.1,.92,.02,With(dark,detail:true));
                    T(mid,1.55,s*1.46,len-.1,.18,.03,red);T(mid,.68,s*1.42,len,.46,.06,dark);
                    foreach(double dx in new[]{.25,.75})T(b0+len*dx,1.9,s*1.47,1.3,2.2,.02,With(L("plain",0x5A6064,RoleTrain),detail:true));
                }
                T(mid,.3,0,len,.3,2.4,dark);
                foreach(double bx in new[]{b0+3.0,b1-3.0}){T(bx,.32,0,2.6,.4,2.0,With(dark,detail:true));foreach(double s in new[]{-1.0,1.0})foreach(double wx in new[]{-.9,.9})T(bx+wx,.42,s*.74,.84,.84,.12,With(L("cyber-metal",0x3A3E40,RoleTrain),detail:true));}
                if(c<2)T(x0-gap/2,2.1,0,gap+.2,3.0,2.4,dark);
            }
            // The nose: stepped taper over 2.6 m, the yellow panel, windscreen, headlights, EXPRESS.
            for(int k=0;k<5;k++){double l=.52,x=-2.6+k*l+l/2,h=3.5-k*.42,w=2.9-k*.18;T(x,.45+h/2,0,l,h,w,k<2?body:yellow);}
            T(-.15,1.25,0,.3,1.4,2.2,yellow);T(-1.17,3.14,0,1.15,.06,2.4,Glow(0x0A1018,.4f,RoleTrain),0,0,-47);
            foreach(double s in new[]{-.75,.75}){T(.02,1.15,s,.06,.24,.36,Glow(HeadlightHex,8f,RoleTrain));T(.03,1.15,s,.08,.3,.42,dark);}
            T(-1.555,3.33,0,.03,.3,1.7,dark);
            float cell=.045f;double w0=-TextWidth("EXPRESS",cell)/2;
            for(int i=0;i<7;i++)foreach(var g in Glyph("EXPRESS"[i])){double u=w0+(i*4+g[0]+g[2]*.5)*cell,v=(5-g[1]-g[3]*.5)*cell-2.5*cell;T(-1.535,3.33+v,-u,.02,g[3]*cell,g[2]*cell,Glow(AmberHex,2.4f,RoleTrain));}
            double tail=-(3*car+2*gap);foreach(double s in new[]{-.8,.8})T(tail-.02,1.2,s,.05,.2,.3,Glow(0xFF2A1A,4f,RoleTrain));
        }
    }
}
