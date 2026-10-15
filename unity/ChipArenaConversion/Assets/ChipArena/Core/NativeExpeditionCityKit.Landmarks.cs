using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The town kit's landmarks: Meridian Biocontainment, the city hall, the sealed foundry and transit frontages, the final
    // checkpoint, and the doorways the layout reserves (NativeExpeditionCityKit.cs).
    public sealed partial class NativeExpeditionCityKit
    {
        private Pen Clad=>P(NativeWardZeroLooks.Panel,MeridianHex,RoleLandmark,NativeCyberCityLooks.Bank);
        /// <summary>The doorways no dressing may cover (face keep rectangles): door spawns, the instance door and the reserved
        /// foundry and transit doorways. Door spawns are remembered for their own dressing.</summary>
        private readonly List<(Face f,double u,short spawn)> doorways=new List<(Face,double,short)>();
        private void FaceKeeps()
        {
            var spawns=area?.spawns??Array.Empty<ExpeditionSpawnLayout>();
            for(short s=0;s<spawns.Length;s++)
            {
                var e=spawns[s]?.entry;if(e?.kind!="door"||e.from==null||e.exit==null)continue;double dx=e.exit.x-e.from.x,dz=e.exit.z-e.from.z,l=Math.Sqrt(dx*dx+dz*dz);if(l<1e-6)continue;dx/=l;dz/=l;
                var f=FaceAt(e.from.x+dx*.6,e.from.z+dz*.6,dx,dz);if(f==null)continue;double u=U(f,e.from.x,e.from.z);f.Keep.Add(new[]{u-1.15,u+1.15,-1,3.1});doorways.Add((f,u,s));
            }
            foreach(var d in area?.doors??Array.Empty<ExpeditionDoorLayout>())
            {
                if(d.closure==null)continue;double a=d.closure.yaw*Math.PI/180;var f=FaceAt(d.closure.x+Math.Sin(a)*.2,d.closure.z+Math.Cos(a)*.2,-Math.Sin(a),-Math.Cos(a));
                if(f!=null){double u=U(f,d.closure.x,d.closure.z),w=d.closure.sx/2+UnitWing+.1;f.Keep.Add(new[]{u-w,u+w,-1,UnitTop});}
            }
            // The reserved doorways (the hub record's, exp-layout-3: exp-foundry at (-84, 0, 0) facing +x, exp-transit at (0, 0, -84)
            // facing +z; the same places without it).
            double fx=-84,fz=0,tx=0,tz=-84;
            if(hub!=null)foreach(var r in hub.Routes){if(!r.Reserved)continue;if(r.To=="exp-foundry"){fx=r.X;fz=r.Z;}else if(r.To=="exp-transit"){tx=r.X;tz=r.Z;}}
            var fd=FaceAt(fx,fz,1,0);if(fd!=null){double u=U(fd,fx,fz);fd.Keep.Add(new[]{u-3.9,u+3.9,-1,5.4});}var tr=FaceAt(tx,tz,0,1);if(tr!=null){double u=U(tr,tx,tz);tr.Keep.Add(new[]{u-4.4,u+4.4,-1,4.0});}
        }
        /// <summary>The building face whose plane passes through (x, z) facing (dx, dz) (its dominant axis), or null.</summary>
        private Face FaceAt(double x,double z,double dx,double dz)
        {
            int axis=Math.Abs(dx)>=Math.Abs(dz)?0:2,sign=(axis==0?dx:dz)>0?1:-1;double p=axis==0?x:z;
            foreach(var f in faces){if(f.Axis!=axis||f.Sign!=sign||Math.Abs(f.Plane-p)>.08)continue;double u=U(f,x,z);if(u>=f.U0-.01&&u<=f.U1+.01)return f;}
            return null;
        }

        // ---------------- Meridian Biocontainment (Ward Zero) ----------------
        private void MeridianFace(Face f)
        {
            if(f.Front==0)return;double h=f.Top-f.Base;
            // White cladding over the ground floor (the doorway left clear), a teal band, hazard stripes at the foot.
            foreach(var run in Runs(f,.05,4.2,f.U0+.2,f.U1-.2,.05))FBox(f,(run[0]+run[1])/2,2.15,.03,run[1]-run[0],4.2,.06,Clad);
            foreach(var run in Runs(f,4.25,4.55,f.U0+.2,f.U1-.2,.05))FBox(f,(run[0]+run[1])/2,4.4,.05,run[1]-run[0],.3,.1,P(NativeBlockLooks.Plain,MeridianTealHex,RoleLandmark));
            foreach(var run in Runs(f,.0,.36,f.U0+.2,f.U1-.2,.05))FBox(f,(run[0]+run[1])/2,.18,.07,run[1]-run[0],.36,.04,With(P(NativeBlockLooks.Demolition,0,RoleLandmark),detail:true));
            // Two ribbon windows of cold glass: some panes lit by the labs' fluorescents (one stutters), some broken.
            foreach(double y in new[]{5.4,9.0})foreach(var run in Runs(f,y,y+1.5,f.U0+.6,f.U1-.6,.05))
            {
                int n=Math.Max(1,(int)((run[1]-run[0])/2.0));double w=(run[1]-run[0])/n;
                for(int k=0;k<n;k++){double u=run[0]+w*(k+.5),s=Hash(blocks[f.Block].id,(int)y*10+f.Index,k);
                    if(s<.3)FBox(f,u,y+.75,.02,w-.12,1.5,.04,Glow(ColdHex,.45f,RoleLandmark,s<.06?DriveFlicker:DriveSteady,(float)s*9));
                    else if(s<.5)FBox(f,u,y+.75,.02,w-.12,1.5,.04,P(NativeBlockLooks.Plain,BlackHex,RoleLandmark));
                    else FBox(f,u,y+.75,.02,w-.12,1.5,.04,P(NativeBlockLooks.Plain,0x2A3A44,RoleLandmark));
                    FBox(f,u-w/2,y+.75,.05,.12,1.6,.1,P(NativeBlockLooks.SteelGreen,0xC8CCCC,RoleLandmark));}
                FBox(f,run[1],y+.75,.05,.12,1.6,.1,P(NativeBlockLooks.SteelGreen,0xC8CCCC,RoleLandmark));
                FBox(f,(run[0]+run[1])/2,y-.08,.08,run[1]-run[0],.16,.16,P(NativeBlockLooks.SteelGreen,0xC8CCCC,RoleLandmark));
            }
            // MERIDIAN BIOCONTAINMENT across the front between the ribbons, cold white, one letter dead; a quarantine notice and
            // a smear of blood beside the doorway.
            if(f.Axis==0&&f.Sign<0)
            {
                double cell=.11;Text(f,"MERIDIAN BIOCONTAINMENT",0,7.25,.07,cell,.05,Glow(0xE8F8FF,1.5f,RoleSign,DriveSteady),0,13,Glow(0x2A3236,.05f,RoleSign));
                FBox(f,0,7.25+2.5*cell,.035,TextWidth("MERIDIAN BIOCONTAINMENT",cell)+.6,.9,.04,P(NativeBlockLooks.Plain,0x1E2A2E,RoleSign));
                Notice(f,-6.5,2.0,.07,true);Notice(f,6.5,1.8,.07,false);Poster(f,8.4,1.7,.07,.8);
                FBox(f,4.6,1.3,.065,.5,1.1,.012,With(P(NativeBlockLooks.Plain,BloodHex,RoleLandmark),detail:true),14);FBox(f,4.75,.6,.065,.08,.9,.012,With(P(NativeBlockLooks.Plain,BloodDryHex,RoleLandmark),detail:true));
                Message(f,"MERIDIAN LIED",-12,2.4,.07,5.5,.12);
            }
        }
        private void MeridianTowerFace(Face f)
        {
            if(f.Front==0&&!(f.Axis==0&&f.Sign<0))return;double h=f.Top-f.Base;
            foreach(double y in new[]{.8,3.8})foreach(var run in Runs(f,y,y+1.6,f.U0+.5,f.U1-.5,.05))
            {
                int n=Math.Max(1,(int)((run[1]-run[0])/2.2));double w=(run[1]-run[0])/n;
                for(int k=0;k<n;k++){double u=run[0]+w*(k+.5),s=Hash(blocks[f.Block].id,(int)y*10+f.Index,k);FBox(f,u,y+.8,.02,w-.15,1.6,.04,s<.25?Glow(ColdHex,.35f,RoleLandmark):P(NativeBlockLooks.Plain,s<.5?BlackHex:0x2A3A44,RoleLandmark));}
            }
            if(f.Axis==0&&f.Sign<0)
            {
                // MERIDIAN high on the tower toward the town, lit cold, its frame and the beacon on the roof.
                double cell=.42;Text(f,"MERIDIAN",0,h-4.2,.12,cell,.1,Glow(0xD8F4FF,1.7f,RoleSign,DriveSteady),0,4,Glow(0x9FB8C0,.25f,RoleSign,DriveFlicker,.6f));
                At(f,0,0,1.5,out double lx,out double lz);Light(lx,f.Base+h-3.2,lz,0xCFF2FF,.9,14,4,GlowCold);
                At(f,f.U0+1,0,-1,out double bx,out double bz);Box(f.Block,bx,f.Top+.3,bz,.4,.6,.4,Glow(RedHex,2.2f,RoleRoof,DriveStrobe,.1f));Light(bx,f.Top+.6,bz,RedHex,.7,10,1.2,GlowBeacon,.1);
            }
        }
        private void Portico(int i,string s)
        {
            var b=blocks[i];
            if(s=="wz-portico-canopy")
            {
                // WARD ZERO on the canopy's fascia (toward the street, -x), downlights under it, the rotating beacon on top.
                var f=MakeFace(i,0,-1);if(f!=null){Text(f,"WARD ZERO",0,.07,.03,.052,.02,Glow(0xFF3A30,1.6f,RoleSign));FBox(f,0,.2,.012,TextWidth("WARD ZERO",.052)+.3,.34,.02,P(NativeBlockLooks.Plain,0x1A2226,RoleSign));}
                for(int k=-1;k<=1;k++)Box(i,b.pos.x-.4,b.pos.y-b.size.y/2-.03,k*3.0,.6,.06,.3,Glow(ColdHex,1.2f,RoleLandmark,k==1?DriveFlicker:DriveSteady,.4f));
                Box(i,b.pos.x-1.2,b.pos.y+b.size.y/2+.2,0,.36,.4,.36,Glow(RedHex,2.4f,RoleLandmark,DriveStrobe,0));Light(b.pos.x-1.2,b.pos.y+b.size.y/2+.5,0,RedHex,.8,11,1.0,GlowBeacon,0);
                Box(i,b.pos.x,b.pos.y+b.size.y/2+.02,0,b.size.x-.2,.04,b.size.z-.2,With(P(NativeBlockLooks.Tar,0x2A2A2C,RoleLandmark),detail:true));
            }
            else
            {
                // A pylon: white cladding, hazard tape round it at waist height.
                foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f==null)continue;FBox(f,(f.U0+f.U1)/2,1.0,.015,f.U1-f.U0+.03,.12,.02,With(P(NativeBlockLooks.Demolition,0,RoleLandmark),detail:true));FBox(f,(f.U0+f.U1)/2,3.8,.015,f.U1-f.U0+.03,.08,.02,P(NativeBlockLooks.Plain,MeridianTealHex,RoleLandmark));}
            }
        }

        // ---------------- the city hall ----------------
        private void CityHallFace(Face f)
        {
            if(f.Front==0)return;string id=blocks[f.Block].id;double h=f.Top-f.Base;var stone=P(NativeBlockLooks.ConcreteBoard,0xB4AA98,RoleLandmark);
            bool front=f.Axis==2&&f.Sign<0;if(!front){Facade(f,Civic,"city-hall");return;}
            // Pilasters every 4 m, an entablature with CITY HALL, a cornice; tall windows between the pilasters.
            for(double u=-22;u<=22.01;u+=4){if(Open(f,u-.35,u+.35,.95,12.9,.02))FBox(f,u,6.95,.11,.6,12.0,.22,With(stone,shadow:true));}
            foreach(var run in Runs(f,12.9,14.3,f.U0,f.U1,.02,false))FBox(f,(run[0]+run[1])/2,13.6,.2,run[1]-run[0],1.4,.4,With(stone,shadow:true));
            foreach(var run in Runs(f,15.4,16,f.U0,f.U1,.02,false))FBox(f,(run[0]+run[1])/2,15.75,.32,run[1]-run[0],.5,.64,With(P(NativeBlockLooks.ConcreteBoard,0x9A9080,RoleLandmark),shadow:true));
            Text(f,"CITY HALL",0,13.15,.42,.19,.05,P(NativeBlockLooks.Plain,0x2E2A26,RoleSign));
            for(double u=-20;u<=20.01;u+=4)
            {
                if(Math.Abs(u)<2.1)continue;double s=Hash(id,(int)(u+40),1);
                foreach(var (y0,hh) in new[]{(1.4,3.0),(6.2,4.2)})
                {
                    if(!Open(f,u-.95,u+.95,y0-.2,y0+hh+.45))continue;Begin();
                    double yc=y0+hh/2;int state=(int)(Hash(id,(int)(u*10),(int)y0)*5);
                    FBox(f,u,yc,.02,1.4,hh,.04,state==0?P(NativeBlockLooks.Timber,BoardHex,RoleLandmark):state==1?P(NativeBlockLooks.Plain,BlackHex,RoleLandmark):state==2&&y0>5?Glow(WarmHex,.3f,RoleLandmark,DriveFlicker,(float)s):P(NativeBlockLooks.Plain,GlassHex,RoleLandmark));
                    FBox(f,u,y0+hh+.15,.06,1.1,.3,.12,stone);FBox(f,u,y0+hh+.38,.06,.6,.16,.12,stone);FBox(f,u,y0-.07,.08,1.7,.14,.16,stone);
                    if(state==3)FBox(f,u,yc,.045,.08,hh,.02,With(P(NativeBlockLooks.Plain,FrameHex,RoleLandmark),detail:true));
                    End();
                }
            }
            // The main doors behind the steps: chained timber leaves under a pediment; lanterns either side (one dead).
            Begin();FBox(f,0,2.85,.03,3.2,3.9,.06,P(NativeBlockLooks.Timber,0x4A3424,RoleLandmark));FBox(f,0,2.85,.065,.06,3.9,.02,P(NativeBlockLooks.Plain,FrameHex,RoleLandmark));
            FBox(f,0,5.05,.15,4.2,.5,.3,With(stone,shadow:true));FBox(f,0,5.5,.12,3.0,.4,.24,stone);FBox(f,-1.9,2.85,.1,.3,3.9,.2,stone);FBox(f,1.9,2.85,.1,.3,3.9,.2,stone);
            FBox(f,0,2.6,.09,2.6,.06,.04,With(P(NativeBlockLooks.SteelGreen,0x5A5A58,RoleLandmark),detail:true),22);FBox(f,0,2.6,.1,2.6,.06,.04,With(P(NativeBlockLooks.SteelGreen,0x5A5A58,RoleLandmark),detail:true),-22);
            FBox(f,0,2.6,.12,.24,.3,.06,With(P(NativeBlockLooks.SteelGreen,0x8A7A40,RoleLandmark),detail:true));
            End();
            foreach(double u in new[]{-3.0,3.0}){Begin();FBox(f,u,4.2,.12,.08,.3,.24,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleLandmark));FBox(f,u,3.8,.24,.32,.5,.32,u<0?Glow(WarmHex,1.1f,RoleLandmark,DriveFlicker,.7f):P(NativeBlockLooks.Plain,0x2A2622,RoleLandmark));End();
                if(u<0){At(f,u,0,.6,out double lx,out double lz);Light(lx,f.Base+3.7,lz,WarmHex,.7,8,.9,GlowLampFlicker,.7);}}
            // Tattered banners hung from the entablature in front of four pilasters.
            foreach(double u in new[]{-14.0,-6.0,6.0,14.0}){Begin();FBox(f,u,10.6,.26,1.0,4.0,.03,With(P(NativeBlockLooks.Plain,0x5A1A1A,RoleLandmark),fade:true));FBox(f,u+.25,8.45,.26,.5,.3,.03,With(P(NativeBlockLooks.Plain,0x5A1A1A,RoleLandmark),detail:true));FBox(f,u,10.8,.275,.5,.5,.012,With(P(NativeBlockLooks.Plain,0xC8A050,RoleLandmark),detail:true));End();}
            Notice(f,-16,2.0,.24,true);Message(f,"GOD HELP US",16,1.6,.24,3.4,.05);
        }
        private void ClockFace(Face f)
        {
            if(!(f.Axis==2&&f.Sign<0))return;double h=f.Top-f.Base;var stone=P(NativeBlockLooks.ConcreteBoard,0xB4AA98,RoleLandmark);
            // A square clock face, its hour marks and the hands stopped at 6:47; a cornice and a stepped crown.
            double cy=h*.55;FBox(f,0,cy,.06,3.6,3.6,.12,With(stone,shadow:true));FBox(f,0,cy,.13,3.1,3.1,.04,Glow(0xF0E2B8,.35f,RoleLandmark));
            for(int k=0;k<12;k++){double a=k*30*Math.PI/180;FBox(f,Math.Sin(a)*1.28,cy+Math.Cos(a)*1.28,.16,.1,k%3==0?.34:.2,.02,P(NativeBlockLooks.Plain,0x1E1A16,RoleLandmark),k*30);}
            foreach(var (deg,len,w) in new[]{(203.5,.8,.12),(282.0,1.2,.07)}){double a=deg*Math.PI/180;FBox(f,Math.Sin(a)*len/2,cy+Math.Cos(a)*len/2,.18,w,len,.03,P(NativeBlockLooks.Plain,0x141210,RoleLandmark),deg);}
            FBox(f,0,h-.2,.2,f.U1-f.U0+.3,.4,.4,With(stone,shadow:true));
            At(f,0,0,1.6,out double lx,out double lz);Light(lx,f.Base+cy,lz,0xF0E2B8,.35,7,2.2,GlowCold);
        }

        // ---------------- the sealed foundry ----------------
        private void FoundryFace(Face f)
        {
            if(f.Front==0)return;string id=blocks[f.Block].id;double h=f.Top-f.Base;
            foreach(var run in Runs(f,0,1.0,f.U0+.2,f.U1-.2,.05))FBox(f,(run[0]+run[1])/2,.5,.05,run[1]-run[0],1.0,.1,P(NativeBlockLooks.ConcreteBoard,0x6A645A,RoleLandmark));
            // The clerestory: a band of dirty, broken panes high up.
            foreach(var run in Runs(f,h-3.4,h-1.4,f.U0+.6,f.U1-.6,.05)){int n=Math.Max(1,(int)((run[1]-run[0])/1.8));double w=(run[1]-run[0])/n;
                for(int k=0;k<n;k++){double u=run[0]+w*(k+.5),s=Hash(id,f.Index,k);FBox(f,u,h-2.4,.02,w-.14,1.8,.04,P(NativeBlockLooks.Plain,s<.35?BlackHex:0x4A4A3A,RoleLandmark));}}
            if(f.Axis==0&&f.Sign>0)
            {
                Text(f,"CITY IRONWORKS",0,7.3,.07,.17,.03,P(NativeBlockLooks.Plain,0xC8BCA0,RoleSign),0,5,P(NativeBlockLooks.Plain,0x6A6050,RoleSign));
                FBox(f,0,7.3+2.5*.17,.035,TextWidth("CITY IRONWORKS",.17)+.8,1.3,.04,P(NativeBlockLooks.Plain,0x2A2A26,RoleSign));
                // Side doors either side of the shutter, welded shut.
                foreach(double u in new[]{-12.0,12.0}){if(!Open(f,u-.8,u+.8,0,2.6))continue;Begin();FBox(f,u,1.15,.04,1.2,2.3,.06,P(NativeBlockLooks.SteelGreen,0x4A5048,RoleLandmark));FBox(f,u,1.15,.08,1.3,.08,.04,With(P(NativeBlockLooks.SteelGreen,0x3A3A36,RoleLandmark),detail:true),35);Text(f,"KEEP OUT",u,1.9,.08,.04,.01,P(NativeBlockLooks.Plain,0xB82A20,RoleSign));End();}
                Notice(f,-6.8,1.8,.08,false);Message(f,"DONT GO EAST",7.5,1.5,.08,4.4,.53);
            }
        }
        private void StackFace(Face f)
        {
            double h=f.Top-f.Base;for(int k=1;k<4;k++)FBox(f,(f.U0+f.U1)/2,k*h/4,.06,f.U1-f.U0+.24,.3,.12,P(NativeBlockLooks.ConcreteBoard,0x5A544C,RoleHigh));
            FBox(f,(f.U0+f.U1)/2,h-.6,.02,f.U1-f.U0,1.2,.04,P(NativeBlockLooks.Plain,SootHex,RoleHigh));
            if(f.Index==0){At(f,(f.U0+f.U1)/2,0,-1.5,out double x,out double z);Box(f.Block,x,f.Top+.2,z,.3,.3,.3,Glow(RedHex,1.4f,RoleHigh,DriveBlink,.3f));}
        }
        private void FoundryShutter(int i)
        {
            // The foundry's doorway sealed: the roller shutter, welded plates and a cross brace, a chain, SEALED in red.
            var b=blocks[i];var f=MakeFace(i,0,1);if(f==null)return;double u=(f.U0+f.U1)/2,w=f.U1-f.U0,h=f.Top-f.Base;
            FBox(f,u,h/2,.03,w,h,.06,P(NativeBlockLooks.Corrugated,0x5A5E5C,RoleLandmark));
            for(int k=0;k<3;k++)FBox(f,u,.9+k*1.4,.08,w-.4,1.1,.04,P(NativeBlockLooks.SteelGreen,0x4A4E46,RoleLandmark));
            double diag=Math.Sqrt(w*w+h*h)-.6,ang=Math.Atan2(h,w)*180/Math.PI;FBox(f,u,h/2,.13,diag,.22,.06,With(P(NativeBlockLooks.SteelGreen,0x3A3A36,RoleLandmark),shadow:true),ang);FBox(f,u,h/2,.19,diag,.22,.06,With(P(NativeBlockLooks.SteelGreen,0x3A3A36,RoleLandmark),shadow:true),-ang);
            Text(f,"SEALED",u,h*.62,.23,.13,.01,P(NativeBlockLooks.Plain,0xB82A20,RoleSign));
            for(int k=0;k<6;k++)FBox(f,u-.6+k*.24,1.2+((k&1)*.04),.2,.22,.1,.05,With(P(NativeBlockLooks.SteelGreen,0x8A8A84,RoleLandmark),detail:true),k%2==0?20:-20);
            FBox(f,u+.2,1.0,.2,.2,.26,.08,With(P(NativeBlockLooks.SteelGreen,0x8A7A30,RoleLandmark),detail:true));
            foreach(double s in new[]{-1.0,1.0})FBox(f,u+s*(w/2+.1),h/2,.05,.2,h,.1,P(NativeBlockLooks.Demolition,0,RoleLandmark));
        }

        // ---------------- the transit station ----------------
        private void TransitFace(Face f)
        {
            if(f.Front==0)return;string id=blocks[f.Block].id;double h=f.Top-f.Base;bool front=f.Axis==2&&f.Sign>0;
            // A cream tiled dado, a high band of arched station windows, CENTRAL STATION over the canopy, the route map, posters.
            foreach(var run in Runs(f,.05,2.6,f.U0+.3,f.U1-.3,.05))FBox(f,(run[0]+run[1])/2,1.3,.03,run[1]-run[0],2.5,.06,P(NativeWardZeroLooks.Tile,0xC8BCA0,RoleLandmark,NativeCyberCityLooks.Bank));
            foreach(var run in Runs(f,5.2,7.3,f.U0+.8,f.U1-.8,.05)){int n=Math.Max(1,(int)((run[1]-run[0])/2.6));double w=(run[1]-run[0])/n;
                for(int k=0;k<n;k++){double u=run[0]+w*(k+.5),s=Hash(id,f.Index,k);Begin();FBox(f,u,6.1,.02,1.4,1.6,.04,P(NativeBlockLooks.Plain,s<.4?BlackHex:GlassHex,RoleLandmark));FBox(f,u,7.0,.02,.9,.2,.04,P(NativeBlockLooks.Plain,s<.4?BlackHex:GlassHex,RoleLandmark));FBox(f,u,5.25,.06,1.7,.12,.12,P(NativeBlockLooks.ConcreteBoard,SillHex,RoleLandmark));End();}}
            if(!front)return;
            FBox(f,0,5.55+2.5*.15,.035,TextWidth("CENTRAL STATION",.15)+.8,1.1,.04,P(NativeBlockLooks.Plain,0x1E2E2A,RoleSign));
            Text(f,"CENTRAL STATION",0,5.55,.07,.15,.03,Glow(0x7AE8D0,.9f,RoleSign,DriveFlicker,.8f),0,8,Glow(0x223430,.05f,RoleSign));
            foreach(double u in new[]{-9.0,9.0})
            {
                if(!Open(f,u-1.3,u+1.3,.8,2.6))continue;Begin();FBox(f,u,1.7,.08,2.2,1.5,.04,P(NativeBlockLooks.Plain,0x1A1E22,RoleLandmark));
                int[] lines={0xD8302A,0x2A8AD8,0xE8B030,0x3AB04A};for(int l=0;l<4;l++)FBox(f,u-.2+l*.08,1.7+(l-1.5)*.24,.105,1.6-l*.2,.05,.012,With(P(NativeBlockLooks.Plain,lines[l],RoleLandmark),detail:true),(l-1.5)*8);
                End();
            }
            Poster(f,-12.5,1.6,.07,.7);Poster(f,13.0,1.7,.07,.2);Message(f,"EVAC CANCELLED",-15.5,1.4,.07,4.8,.82);
            // The station clock on a bracket over the doorway's corner.
            At(f,-6.8,0,.5,out double cx,out double cz);Begin();Box(f.Block,cx,f.Base+4.2,cz,.7,.7,.2,With(P(NativeBlockLooks.SteelGreen,0x2A3A34,RoleHigh),fade:true));Box(f.Block,cx,f.Base+4.2,cz,.55,.55,.22,Glow(0xF0E8D0,.3f,RoleHigh));End();
        }
        private void TransitPiece(int i,string s)
        {
            var b=blocks[i];
            if(s=="transit-boards")
            {
                // The doorway boarded: planks over it, cross braces, STATION CLOSED and a notice.
                var f=MakeFace(i,2,1);if(f==null)return;double w=f.U1-f.U0,h=f.Top-f.Base;
                for(int k=0;k<(int)(h/.3);k++)FBox(f,(f.U0+f.U1)/2+(Hash(b.id,k,1)-.5)*.3,.15+k*.3,.05,w-.1-Hash(b.id,k,2)*.6,.27,.06,P(NativeBlockLooks.Timber,Pick(new[]{0x8A6A44,0x7A5A3A,0x9A7A50},Hash(b.id,k,3)),RoleLandmark));
                FBox(f,-1.8,h/2,.11,4.4,.22,.05,P(NativeBlockLooks.Timber,0x6A4A30,RoleLandmark),38);FBox(f,1.8,h/2,.11,4.4,.22,.05,P(NativeBlockLooks.Timber,0x6A4A30,RoleLandmark),-38);
                FBox(f,0,2.6,.15,3.2,.5,.02,P(NativeBlockLooks.Plain,0xE0D8C0,RoleSign));Text(f,"STATION CLOSED",0,2.6-.1,.165,.04,.008,P(NativeBlockLooks.Plain,0xB82A20,RoleSign));
                Notice(f,2.6,1.4,.15,false);
            }
            else if(s=="transit-canopy")
            {
                var f=MakeFace(i,2,1);if(f!=null){FBox(f,0,.2,.015,f.U1-f.U0-.2,.3,.02,P(NativeBlockLooks.Plain,0x2A3A34,RoleSign));Text(f,"TRANSIT",0,.08,.03,.05,.015,P(NativeBlockLooks.Plain,0xE8E0C8,RoleSign));}
                for(int k=-2;k<=2;k++)Box(i,k*2.2,b.pos.y-b.size.y/2-.03,b.pos.z,.8,.06,.3,k==0?Glow(WarmHex,.9f,RoleLandmark,DriveFlicker,.2f):P(NativeBlockLooks.Plain,0x3A3630,RoleLandmark));
                Light(0,b.pos.y-.6,b.pos.z+.6,WarmHex,.5,7,1,GlowLampFlicker,.2);
            }
            else foreach(var (ax,sg) in new[]{(0,1),(0,-1),(2,1),(2,-1)}){var f=MakeFace(i,ax,sg);if(f!=null)FBox(f,(f.U0+f.U1)/2,1.2,.015,f.U1-f.U0+.03,.1,.02,With(P(NativeBlockLooks.Demolition,0,RoleLandmark),detail:true));}
        }

        // ---------------- the final checkpoint ----------------
        private void Gate(int i,string s)
        {
            var b=blocks[i];double h=b.size.y;
            foreach(int sg in new[]{-1,1})
            {
                var f=MakeFace(i,2,sg);if(f==null)continue;bool south=sg<0;double mid=(f.U0+f.U1)/2,w=f.U1-f.U0;
                if(s=="gate-lintel")
                {
                    if(south){Text(f,"CHECKPOINT",0,3.0,.05,.17,.04,P(NativeBlockLooks.Plain,0xE8E0D0,RoleSign));FBox(f,0,3.0+2.5*.17,.025,TextWidth("CHECKPOINT",.17)+.6,1.2,.03,P(NativeBlockLooks.Plain,0x8A1A16,RoleSign));
                        At(f,0,0,.3,out double lx,out double lz);Begin();Box(i,lx,f.Base+5.0,lz,.5,.5,.4,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleGate));Box(i,lx,f.Base+5.0,lz-.05,.36,.36,.42,Glow(RedHex,2.0f,RoleGate,DriveBlink,.0f));End();}
                    else Text(f,"NO RETURN",0,3.2,.05,.15,.04,P(NativeBlockLooks.Plain,0xC8302A,RoleSign));
                    continue;
                }
                bool tower=Starts(s,"gate-tower-");
                if(!tower)
                {
                    // Blast-wall slabs along the wall's foot, razor wire along its top, a painted warning.
                    for(double u=f.U0+.9;u<f.U1-.7;u+=1.6)FBox(f,u,1.8,.11,1.45,3.6,.2,P(NativeBlockLooks.ConcreteBoard,CheckpointHex,RoleGate));
                    if(south)for(double u=f.U0+.4;u<f.U1-.3;u+=1.2){FBox(f,u,h+.22,-.4,.62,.04,.04,With(P(NativeBlockLooks.SteelGreen,0x8A8A84,RoleGate),detail:true),40);FBox(f,u,h+.22,-.4,.62,.04,.04,With(P(NativeBlockLooks.SteelGreen,0x8A8A84,RoleGate),detail:true),-40);}
                    if(south){string word=b.pos.x<0?"NO ENTRY":"KEEP BACK";Text(f,word,mid,6.2,.05,.2,.03,P(NativeBlockLooks.Plain,0xC8302A,RoleSign),-2);Notice(f,mid+(b.pos.x<0?-4:4),5.0,.03,true);}
                    else{Notice(f,mid,4.5,.03,true);if(b.pos.x<0)Message(f,"NO WAY OUT",mid+3,2.0,.24,4.2,.1);}
                }
                else
                {
                    // A tower: hazard corners, a floodlight at the top looking down the avenue (or the plaza).
                    FBox(f,b.pos.x<0?f.U0+.2:f.U1-.2,1.6,.02,.4,3.2,.04,P(NativeBlockLooks.Demolition,0,RoleGate));
                    At(f,mid,0,.6,out double fx,out double fz);Begin();Box(i,fx,f.Base+h-1.2,fz,.12,.8,.12,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleGate));Box(i,fx,f.Base+h-.7,fz+(south?-.3:.3),.7,.5,.4,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleGate),AxisRot(1,0,0,south?-25:25));
                    Box(i,fx,f.Base+h-.75,fz+(south?-.52:.52),.56,.36,.04,Glow(0xF0F4FF,2.2f,RoleGate),AxisRot(1,0,0,south?-25:25));End();
                    Light(fx,f.Base+h-1.6,fz+(south?-2.0:2.0),0xE8F0FF,.9,16,2.0,GlowFlood);
                }
            }
        }
    }
}
