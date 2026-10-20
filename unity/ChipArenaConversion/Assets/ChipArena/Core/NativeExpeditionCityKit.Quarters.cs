using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The city detail pass's outer quarters (Claude, 2026-10-20; docs/EXPEDITION-HUB.md "The city in detail"): each landmark that
    // ends a forecourt's axis gets its own silhouette and front, so the three arms read apart from far:
    //  - the Exchange (east): an ashlar temple front, six engaged columns, STOCK EXCHANGE on the frieze, a pediment with a stopped
    //    clock over the roof line, a stepped copper dome on a drum with a lantern and a torn flag, bronze figures on the corners,
    //    a dead ticker board over the doors;
    //  - the Union Hall (west): brick piers, a stepped gable rising over a slate roof with a rose window and a bell turret,
    //    an arched entrance with UNION HALL over it, tall arched windows, red banners, a painted sign on the lane;
    //  - the Tram Depot (south): four arched tram bays (one open on a tram's nose, one round the spawn's wicket door), CITY
    //    TRAMWAYS on the frieze, a sawtooth north-light roof, rails and setts out across the cross street, the overhead wires;
    //  - the main lane's rows: the Roxy cinema's marquee and blade sign (east), the Hotel Argo's blade sign (west);
    //  - the squares: a market stall on the crates, a kiosk and a makeshift memorial against the landmark's back;
    //  - the route through each quarter: signs on span wires over the lane, a lamp every few metres down its centre line.
    // Static dressing like the rest of the kit: nothing collides, nothing below 3.1 m stands off a block (WallBand), every record
    // the authority reads is kept clear (the keep-outs), and the route's own line (its `via`) stays free below the span wires.
    public sealed partial class NativeExpeditionCityKit
    {
        private const int Exchange=14,UnionHall=15,TramDepot=16,Cinema=17;
        public const int StoneHex=0xB8B0A0,StoneShadeHex=0x8E8678,CopperHex=0x4E8270,BronzeHex=0x3A5446,GiltHex=0xC8A050,SlateHex=0x3A3A40,LiveryCreamHex=0xC8B890,LiveryRedHex=0x7A2620;
        /// <summary>Whether face f looks back toward the town's centre (the landmarks' fronts across their cross streets).</summary>
        private bool TownFacing(Face f){var b=blocks[f.Block];double cx=b.pos.x,cz=b.pos.z,l=Math.Sqrt(cx*cx+cz*cz);return l>1&&-(Nx(f)*cx+Nz(f)*cz)/l>.9;}
        /// <summary>Whether face f looks out of the town (the landmark's back on its square).</summary>
        private bool OutFacing(Face f){var b=blocks[f.Block];double cx=b.pos.x,cz=b.pos.z,l=Math.Sqrt(cx*cx+cz*cz);return l>1&&(Nx(f)*cx+Nz(f)*cz)/l>.9;}
        /// <summary>A stepped arch on face f: `n` voussoirs on a half circle of radius r over the springing point (u, y), the ring
        /// `ring` thick and `depth` deep `d` out of the face, a keystone at the crown.</summary>
        private void Arch(Face f,double u,double y,double r,double ring,double d,double depth,Pen pen,int n=9,bool key=true)
        {
            double len=Math.PI*(r+ring*.5)/n*1.04;
            for(int k=0;k<n;k++){double t=(k+.5)/n*180,a=t*Math.PI/180;FBox(f,u+(r+ring*.5)*Math.Cos(a),y+(r+ring*.5)*Math.Sin(a),d,len,ring,depth,pen,90-t);}
            if(key)FBox(f,u,y+r+ring*.55,d+.015,ring*.75,ring*1.35,depth+.03,pen);
        }
        /// <summary>A round prism standing on a block: four squares of half side `a` turned 22.5° apart (a sixteen-pointed outline
        /// within 8% of a circle, read as ribs), from y0 to y0 + h.</summary>
        private void Round(int parent,double x,double y0,double z,double a,double h,Pen pen,double yaw=0)
        {
            for(int k=0;k<4;k++)Box(parent,x,y0+h/2,z,2*a,h,2*a,pen,AxisRot(0,1,0,yaw+k*22.5));
        }

        // ---------------- the Exchange (east quarter) ----------------
        private void ExchangeFace(Face f)
        {
            if(f.Front==0)return;string id=blocks[f.Block].id;double h=f.Top-f.Base;
            var stone=P(NativeBlockLooks.Terracotta,StoneHex,RoleLandmark);var shade=P(NativeBlockLooks.Terracotta,StoneShadeHex,RoleLandmark);
            if(!TownFacing(f))
            {
                // The sides and the back: the generic civic windows over a rusticated base and the front's cornice carried round.
                if(OutFacing(f))SquareWall(f,"xq");
                foreach(var run in Runs(f,.05,4.0,f.U0+.05,f.U1-.05,.04))FBox(f,(run[0]+run[1])/2,2.02,.012,run[1]-run[0],3.95,.024,shade);
                Facade(f,Civic,"xq-exchange");
                foreach(var run in Runs(f,h-2.6,h-1.9,f.U0,f.U1,.02,false))FBox(f,(run[0]+run[1])/2,h-2.25,.3,run[1]-run[0],.5,.6,With(stone,shadow:true));
                return;
            }
            double u0=f.U0,u1=f.U1,uc=(u0+u1)/2,w=u1-u0;
            // The ashlar skin: a darker rusticated base in pale courses, the pale stone above; doorways and occluders left clear.
            foreach(var run in Runs(f,.05,4.2,u0+.05,u1-.05,.04))
            {
                FBox(f,(run[0]+run[1])/2,2.1,.012,run[1]-run[0],4.15,.024,shade);
                for(int c=0;c<7;c++)FBox(f,(run[0]+run[1])/2,.32+c*.6,.035,run[1]-run[0],.5,.022,With(stone,detail:c%2==1));
            }
            foreach(var run in Runs(f,4.2,h-.05,u0+.05,u1-.05,.04))FBox(f,(run[0]+run[1])/2,(4.2+h-.05)/2,.012,run[1]-run[0],h-4.25,.024,stone);
            // Six engaged columns on a plinth course: base, shaft (two flutes), capital, abacus.
            var col=With(P(NativeBlockLooks.Terracotta,0xC4BCAC,RoleLandmark),fade:false);
            for(int k=0;k<6;k++)
            {
                double u=u0+w/12+k*w/6;Begin();
                FBox(f,u,4.45,.27,1.3,.5,.5,col);FBox(f,u,9.15,.24,.9,8.9,.44,col);
                FBox(f,u-.18,9.15,.465,.07,8.4,.012,With(P(NativeBlockLooks.Plain,0x8A8274,RoleLandmark),detail:true));FBox(f,u+.18,9.15,.465,.07,8.4,.012,With(P(NativeBlockLooks.Plain,0x8A8274,RoleLandmark),detail:true));
                FBox(f,u,13.9,.27,1.3,.6,.5,col);FBox(f,u,14.28,.29,1.5,.16,.54,col);
                End();
            }
            // The entablature: architrave, the gilt STOCK EXCHANGE on the frieze, the cornice (casting), the attic.
            FBox(f,uc,14.7,.25,w-.1,.5,.46,With(stone,shadow:true));
            Text(f,"STOCK EXCHANGE",uc,15.05,.07,.19,.03,P(NativeBlockLooks.Plain,GiltHex,RoleSign));
            FBox(f,uc,16.25,.28,w+.2,.42,.52,With(stone,shadow:true));FBox(f,uc,16.75,.12,w-.2,.5,.2,stone);
            // Tall windows in the bays between the columns, two storeys, each with an architrave and a cornice over it.
            for(int k=0;k<5;k++)
            {
                double u=u0+w/6*(k+1);
                foreach(double y0 in new[]{5.3,9.6})
                {
                    if(!Open(f,u-1.15,u+1.15,y0-.3,y0+3.4,.02,false))continue;
                    Window(f,u,y0,1.6,2.9,false,y0>8?2:1,k,false,.04);
                    Begin();FBox(f,u,y0+3.15,.11,2.2,.22,.18,stone);FBox(f,u,y0-.12,.1,2.0,.16,.18,stone);
                    if(y0<8)for(int b=0;b<5;b++)FBox(f,u-.72+b*.36,y0-.55,.1,.14,.7,.14,With(stone,detail:true));
                    End();
                }
            }
            // The doors: the great door on the axis, a blind door to its left; the spawn's own doorway (right) gets an arch over it.
            Begin();FBox(f,uc,1.95,.045,2.6,3.9,.03,P(NativeBlockLooks.Timber,0x2E241C,RoleLandmark));FBox(f,uc,1.95,.065,.06,3.9,.012,P(NativeBlockLooks.Plain,FrameHex,RoleLandmark));
            for(int k=0;k<4;k++)FBox(f,uc,.6+k*.9,.066,2.4,.05,.012,With(P(NativeBlockLooks.SteelGreen,0x5A5040,RoleLandmark),detail:true));
            FBox(f,uc,4.05,.1,3.4,.3,.14,stone);End();
            double ud=uc-w/6;if(Open(f,ud-1.0,ud+1.0,0,3.5,.02,false)){Begin();FBox(f,ud,1.4,.045,1.6,2.8,.03,P(NativeBlockLooks.Timber,0x2A2018,RoleLandmark));FBox(f,ud,3.0,.08,2.0,.25,.1,stone);End();}
            // Bronze lanterns either side of the great door on the base, one still lit.
            foreach(double s in new[]{-1.0,1.0}){double lu=uc+s*1.75;Begin();FBox(f,lu,3.05,.14,.1,.32,.22,P(NativeBlockLooks.SteelGreen,BronzeHex,RoleLandmark));FBox(f,lu,2.72,.13,.26,.46,.2,s<0?Glow(WarmHex,1.1f,RoleLandmark,DriveFlicker,.62f):P(NativeBlockLooks.Plain,0x2A2622,RoleLandmark));
                if(End()&&s<0){At(f,lu,0,.6,out double lx,out double lz);Light(lx,f.Base+2.7,lz,WarmHex,.7,8,.9,GlowLampFlicker,.62);}}
            foreach(var (fd,u,s) in doorways)if(fd==f){Arch(f,u,2.75,.85,.28,.08,.12,stone,7);}
            // The ticker board over the great door: MARKET CLOSED in amber LEDs, one letter dead, on a dark band.
            {
                double cell=.055;string tick="MARKET CLOSED";double tw=TextWidth(tick,cell);
                if(Open(f,uc-tw/2-.2,uc+tw/2+.2,3.3,3.9,.02,false)){Begin();FBox(f,uc,3.6,.06,tw+.3,.42,.04,P(NativeBlockLooks.Plain,0x141210,RoleSign));
                    Text(f,tick,uc,3.6-2.5*cell,.085,cell,.012,Glow(AmberHex,1.5f,RoleSign,DriveFlicker,.35f),0,4,Glow(0x3A2A10,.05f,RoleSign));End();
                    At(f,uc,0,.7,out double lx,out double lz);Light(lx,f.Base+3.3,lz,AmberHex,.4,5,.8,GlowNeon,.35);}
            }
            // The pediment over the roof line on the four middle bays: stepped tympanum, raking cornices, a stopped clock.
            {
                double half=w/3+.6,top=17.0;var ped=With(stone,shadow:false);
                for(int k=0;k<6;k++){double hw=half*(1-k/6.0)-.1;FBox(f,uc,top+.28+k*.55,.18,2*hw,.56,.34,ped);}
                double rise=3.3,ang=Math.Atan2(rise,half)*180/Math.PI,len=Math.Sqrt(half*half+rise*rise)+.4;
                FBox(f,uc-half/2,top+rise/2+.15,.22,len,.34,.44,With(stone,shadow:true),-ang);FBox(f,uc+half/2,top+rise/2+.15,.22,len,.34,.44,With(stone,shadow:true),ang);
                FBox(f,uc,top+1.3,.37,1.3,1.3,.04,P(NativeBlockLooks.Plain,0x2A2622,RoleLandmark));FBox(f,uc,top+1.3,.4,1.1,1.1,.02,Glow(0xE8DCB0,.28f,RoleLandmark));
                foreach(var (deg,ln,wd) in new[]{(97.0,.36,.07),(-60.0,.48,.05)}){double a=deg*Math.PI/180;FBox(f,uc+Math.Sin(a)*ln/2,top+1.3+Math.Cos(a)*ln/2,.42,wd,ln,.012,P(NativeBlockLooks.Plain,0x141210,RoleLandmark),deg);}
            }
            // Bronze figures on the attic's corners: a plinth, the robed body, the head and a raised arm (one holds a torch).
            foreach(double su in new[]{u0+1.0,u1-1.0})
            {
                Begin();var bronze=P(NativeBlockLooks.SteelGreen,BronzeHex,RoleLandmark);FBox(f,su,17.25,-.4,.9,.5,.8,stone);
                FBox(f,su,18.25,-.4,.55,1.5,.45,bronze);FBox(f,su,19.18,-.4,.3,.34,.3,bronze);FBox(f,su+(su<uc?.32:-.32),19.2,-.4,.12,.9,.12,bronze,su<uc?-20:20);
                if(su<uc){FBox(f,su+.52,19.72,-.4,.16,.2,.16,Glow(FireHex,.12f,RoleLandmark));}
                End();
            }
        }
        private void ExchangeRoof(int i)
        {
            // The dome: a stone drum with its windows, a cornice ring, five copper tiers, the lantern and a flag that is mostly gone.
            var b=blocks[i];double top=b.pos.y+b.size.y/2,x=b.pos.x,z=b.pos.z;var stone=P(NativeBlockLooks.Terracotta,StoneHex,RoleLandmark);
            var copper=P(NativeBlockLooks.Corrugated,CopperHex,RoleLandmark);
            Round(i,x,top,z,4.4,.6,stone);Round(i,x,top+.6,z,4.0,4.4,stone);Round(i,x,top+5.0,z,4.25,.3,stone);
            for(int k=0;k<8;k++){double a=(k*45+22.5)*Math.PI/180;Box(i,x+Math.Sin(a)*4.02,top+2.9,z+Math.Cos(a)*4.02,.7,2.2,.06,P(NativeBlockLooks.Plain,k%3==0?0x2A3A44:BlackHex,RoleLandmark),AxisRot(0,1,0,k*45+22.5));}
            double y=top+5.3;foreach(var (a,hh) in new[]{(3.9,.8),(3.5,.7),(2.9,.6),(2.1,.5),(1.2,.4)}){Round(i,x,y,z,a,hh,copper,11.25);y+=hh;}
            Round(i,x,y,z,.6,1.1,stone);Round(i,x,y+1.1,z,.75,.2,copper);Box(i,x,y+2.1,z,.1,1.8,.1,P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleLandmark));
            Box(i,x,y+2.62,z+.5,.03,.6,.95,With(P(NativeBlockLooks.Plain,0x5A1A16,RoleLandmark),detail:true),AxisRot(1,0,0,8));
            Box(i,x,y+3.05,z,.16,.16,.16,Glow(RedHex,1.6f,RoleLandmark,DriveBlink,.7f));Light(x,y+3.0,z,RedHex,.5,9,.9,GlowBeacon,.7);
        }

        // ---------------- the Union Hall (west quarter) ----------------
        private void UnionHallFace(Face f)
        {
            if(f.Front==0)return;string id=blocks[f.Block].id;double h=f.Top-f.Base;
            var pier=P(NativeBlockLooks.BrickRed,0x5E3428,RoleLandmark);var stone=P(NativeBlockLooks.Terracotta,0xC0B6A2,RoleLandmark);
            if(!TownFacing(f))
            {
                if(OutFacing(f))SquareWall(f,"fq");
                Facade(f,Civic,"fq-union-hall");
                // A painted sign on the lane, faded: UNION HALL, ALL WELCOME.
                if(f.Front==2&&!OutFacing(f)){double cell=.16,su=(f.U0+f.U1)/2;if(Open(f,su-5.5,su+5.5,h-3.4,h-1.3,.05,false)){Begin();
                    FBox(f,su,h-2.3,.012,11,1.9,.012,With(P(NativeBlockLooks.Plain,0x4A3A30,RoleSign),detail:true));Text(f,"UNION HALL",su,h-2.0,.022,cell,.006,With(P(NativeBlockLooks.Plain,0xB8AC90,RoleSign),detail:true));
                    Text(f,"ALL WELCOME",su,h-3.0,.022,.08,.006,With(P(NativeBlockLooks.Plain,0x9A8E74,RoleSign),detail:true));End();}}
                return;
            }
            double u0=f.U0,u1=f.U1,uc=(u0+u1)/2,w=u1-u0,gh=7.5;
            // Brick piers every bay, a stone plinth course and a corbel table under the eaves.
            int bays=6;double bay=w/bays;
            for(int k=0;k<=bays;k++){double u=u0+k*bay;u=Math.Max(u0+.35,Math.Min(u1-.35,u));foreach(var run in Runs(f,.05,h-.6,u-.3,u+.3,.02))FBox(f,(run[0]+run[1])/2,(.05+h-.6)/2,.1,.6,h-.65,.2,pier);}
            foreach(var run in Runs(f,.0,.55,u0+.05,u1-.05,.04))FBox(f,(run[0]+run[1])/2,.28,.13,run[1]-run[0],.55,.24,stone);
            for(double u=u0+.4;u<u1-.3;u+=.6)if(Math.Abs(u-uc)>gh+.3)FBox(f,u,h-.75,.14,.3,.3,.28,With(pier,detail:true));
            // Tall arched windows between the piers (upper floor) and lower windows (ground floor), the middle bays left to the gable.
            for(int k=0;k<bays;k++)
            {
                double u=u0+bay*(k+.5);
                if(Open(f,u-1.1,u+1.1,6.0,12.6,.02,false)){Window(f,u,6.2,1.8,4.6,true,2,k,false,.02);Arch(f,u,10.8,.9,.24,.06,.12,stone,7);FBox(f,u,10.8,.05,1.7,.08,.02,With(P(NativeBlockLooks.Plain,FrameHex,RoleLandmark),detail:true));}
                if(Math.Abs(u-uc)>1.8&&Open(f,u-.9,u+.9,1.2,4.3,.02,false)){Window(f,u,1.3,1.4,2.4,true,0,k,false,.02);Arch(f,u,3.7,.7,.2,.06,.1,stone,6,false);}
            }
            // The entrance on the axis: double timber doors under a brick arch; UNION HALL on a stone band; LOCAL 9; two lanterns.
            if(Open(f,uc-1.6,uc+1.6,0,4.8,.02,false))
            {
                Begin();FBox(f,uc,1.85,.04,2.6,3.7,.03,P(NativeBlockLooks.Timber,0x4A2E1E,RoleLandmark));FBox(f,uc,1.85,.06,.06,3.7,.012,P(NativeBlockLooks.Plain,FrameHex,RoleLandmark));
                FBox(f,uc-.75,1.1,.06,.5,.5,.012,With(P(NativeBlockLooks.SteelGreen,0x8A7A40,RoleLandmark),detail:true));FBox(f,uc+.75,1.1,.06,.5,.5,.012,With(P(NativeBlockLooks.SteelGreen,0x8A7A40,RoleLandmark),detail:true));End();
                Arch(f,uc,3.7,1.35,.32,.08,.14,P(NativeBlockLooks.BrickRed,0x6A3628,RoleLandmark),9);
                foreach(double s in new[]{-1.0,1.0}){Begin();FBox(f,uc+s*2.2,3.75,.12,.1,.3,.24,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleLandmark));FBox(f,uc+s*2.2,3.36,.2,.3,.45,.3,s<0?Glow(WarmHex,1.0f,RoleLandmark,DriveFlicker,.45f):P(NativeBlockLooks.Plain,0x2A2622,RoleLandmark));End();}
                At(f,uc-2.2,0,.6,out double lx,out double lz);Light(lx,f.Base+3.3,lz,WarmHex,.7,8,.9,GlowLampFlicker,.45);
            }
            Begin();FBox(f,uc,5.7,.1,7.6,.8,.14,stone);Text(f,"UNION HALL",uc,5.38,.17,.13,.02,P(NativeBlockLooks.Plain,0x3A2A20,RoleSign));Text(f,"LOCAL 9",uc,4.92,.17,.05,.012,P(NativeBlockLooks.Plain,0x3A2A20,RoleSign));End();
            // The stepped gable over the middle bays (to 22 m, above the roof line), its copings, the rose window, red banners.
            {
                double baseY=h;int steps=5;double rise=1.5;
                for(int k=0;k<steps;k++){double hw=gh*(1-k/(double)steps);FBox(f,uc,baseY+(k+.5)*rise,.15,2*hw,rise,.3,P(NativeBlockLooks.BrickRed,0x6E3E30,RoleLandmark));
                    FBox(f,uc-hw+.2,baseY+(k+1)*rise+.08,.16,.6,.16,.42,stone);FBox(f,uc+hw-.2,baseY+(k+1)*rise+.08,.16,.6,.16,.42,stone);}
                FBox(f,uc,baseY+steps*rise+.1,.16,2*gh/steps+.3,.2,.42,stone);
                double ry=baseY+2.6;Begin();FBox(f,uc,ry,.31,2.0,2.0,.02,P(NativeBlockLooks.Plain,0x1A1E22,RoleLandmark));FBox(f,uc,ry,.31,2.0,2.0,.02,P(NativeBlockLooks.Plain,0x1A1E22,RoleLandmark),45);
                for(int k=0;k<12;k++){double a=k*30*Math.PI/180;FBox(f,uc+Math.Cos(a)*1.32,ry+Math.Sin(a)*1.32,.33,.72,.26,.06,stone,90-k*30);}
                FBox(f,uc,ry,.34,.07,2.2,.02,With(stone,detail:true));FBox(f,uc,ry,.34,2.2,.07,.02,With(stone,detail:true));End();
                foreach(double s in new[]{-4.6,0.0,4.6}){if(s==0)continue;Begin();FBox(f,uc+s,h-2.6,.32,1.2,5.0,.03,With(P(NativeBlockLooks.Plain,0x6A1A18,RoleLandmark),fade:true));FBox(f,uc+s,h-.15,.33,1.4,.12,.06,With(P(NativeBlockLooks.SteelGreen,0x2A2622,RoleLandmark),detail:true));
                    FBox(f,uc+s,h-2.0,.34,.6,.6,.012,With(P(NativeBlockLooks.Plain,0xD8D0C0,RoleLandmark),detail:true));FBox(f,uc+s,h-2.0,.345,.6,.6,.012,With(P(NativeBlockLooks.Plain,0xD8D0C0,RoleLandmark),detail:true),45);FBox(f,uc+s,h-2.0,.35,.3,.3,.012,With(P(NativeBlockLooks.Plain,0x6A1A18,RoleLandmark),detail:true));End();}
            }
        }
        private void UnionHallRoof(int i)
        {
            // A slate roof behind the gable (two slopes from the eaves to a ridge over the middle bays) and a bell turret on it.
            var b=blocks[i];double top=b.pos.y+b.size.y/2;bool alongX=Math.Abs(b.pos.x)>Math.Abs(b.pos.z);double gh=7.5,rise=7.0,ang=Math.Atan2(rise,gh)*180/Math.PI,slope=Math.Sqrt(gh*gh+rise*rise);
            double depth=(alongX?b.size.x:b.size.z)-1.0,cx=b.pos.x,cz=b.pos.z;var slate=P(NativeBlockLooks.Timber,SlateHex,RoleLandmark);
            for(int s=-1;s<=1;s+=2)
            {
                double ox=alongX?0:s*gh/2,oz=alongX?s*gh/2:0;var q=alongX?AxisRot(1,0,0,s*ang):AxisRot(0,0,1,-s*ang);
                Box(i,cx+ox,top+rise/2-.05,cz+oz,alongX?depth:slope,.22,alongX?slope:depth,slate,q);
            }
            Box(i,cx,top+rise+.05,cz,alongX?depth:.4,.25,alongX?.4:depth,P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleLandmark));
            // The turret near the front: louvred bell chamber, a stepped pyramid cap, its weathervane.
            double fx=cx+(alongX?-Math.Sign(cx)*(depth/2-1.6):0),fz=cz+(alongX?0:-Math.Sign(cz)*(depth/2-1.6));var brick=P(NativeBlockLooks.BrickRed,0x6E3E30,RoleLandmark);
            Box(i,fx,top+rise+1.2,fz,1.8,2.4,1.8,brick);for(int k=0;k<4;k++){double a=k*90*Math.PI/180;Box(i,fx+Math.Sin(a)*.91,top+rise+1.5,fz+Math.Cos(a)*.91,.7,1.1,.04,P(NativeBlockLooks.Plain,BlackHex,RoleLandmark),AxisRot(0,1,0,k*90));}
            for(int k=0;k<3;k++)Box(i,fx,top+rise+2.5+k*.35,fz,2.0-k*.6,.35,2.0-k*.6,slate);Box(i,fx,top+rise+3.9,fz,.06,1.0,.06,P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleLandmark));
            Box(i,fx,top+rise+4.2,fz,.5,.05,.05,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleLandmark),detail:true),AxisRot(0,1,0,30));
        }

        // ---------------- the Tram Depot (south quarter) ----------------
        private void TramDepotFace(Face f)
        {
            if(f.Front==0)return;string id=blocks[f.Block].id;double h=f.Top-f.Base;
            var stone=P(NativeBlockLooks.Terracotta,0xB0A894,RoleLandmark);var door=P(NativeBlockLooks.Timber,0x2E4A3A,RoleLandmark);
            if(!TownFacing(f))
            {
                if(OutFacing(f))SquareWall(f,"tq");
                Facade(f,Civic,"tq-tram-depot");
                if(f.Front==2&&!OutFacing(f)){double su=(f.U0+f.U1)/2;if(Open(f,su-4,su+4,h-2.9,h-1.5,.05,false)){Begin();Text(f,"TRAM DEPOT",su,h-2.6,.02,.13,.006,With(P(NativeBlockLooks.Plain,0xC8BCA0,RoleSign),detail:true));End();}}
                return;
            }
            double u0=f.U0,u1=f.U1,uc=(u0+u1)/2,w=u1-u0;int n=4;double bay=w/n,bw=bay-1.6,bh=5.0;
            // Piers between the bays, a stone frieze with CITY TRAMWAYS and NO 4 DEPOT, the depot clock.
            for(int k=0;k<=n;k++){double u=Math.Max(u0+.4,Math.Min(u1-.4,u0+k*bay));foreach(var run in Runs(f,.05,h-.3,u-.4,u+.4,.02))FBox(f,(run[0]+run[1])/2,(.05+h-.3)/2,.1,.8,h-.35,.2,stone);}
            FBox(f,uc,9.3,.14,w-.1,1.5,.26,stone);Text(f,"CITY TRAMWAYS",uc,9.0,.28,.15,.03,P(NativeBlockLooks.Plain,0x2A3A30,RoleSign));
            FBox(f,uc,h-.25,.2,w+.2,.5,.42,With(stone,shadow:true));
            for(int k=0;k<n;k++)
            {
                double u=u0+bay*(k+.5);bool open=k==1,wicket=false;foreach(var (fd,du,s) in doorways)if(fd==f&&Math.Abs(du-u)<bw/2)wicket=true;
                Arch(f,u,bh,bw/2,.42,.12,.22,stone,11);
                // The door leaves: plank doors with cross braces round the spawn's wicket; one bay open on a tram's nose.
                if(open&&!wicket&&Open(f,u-bw/2,u+bw/2,0,bh,.02,false))
                {
                    Begin();FBox(f,u,bh/2,.015,bw,bh,.03,P(NativeBlockLooks.Plain,0x08080A,RoleLandmark));
                    var cream=P(NativeBlockLooks.Plain,LiveryCreamHex,RoleLandmark);var red=P(NativeBlockLooks.Plain,LiveryRedHex,RoleLandmark);double tw=2.4;
                    FBox(f,u,1.05,.09,tw,1.5,.12,red);FBox(f,u,2.6,.08,tw,1.6,.1,cream);FBox(f,u,3.55,.1,tw+.1,.3,.14,red);
                    FBox(f,u-.6,2.6,.135,.95,1.1,.012,P(NativeBlockLooks.Plain,GlassHex,RoleLandmark));FBox(f,u+.6,2.6,.135,.95,1.1,.012,P(NativeBlockLooks.Plain,0x2A3440,RoleLandmark));
                    FBox(f,u,3.55,.18,1.0,.2,.012,Glow(AmberHex,.5f,RoleSign,DriveFlicker,.6f));Text(f,"4",u,3.48,.19,.025,.006,P(NativeBlockLooks.Plain,0x141210,RoleSign));
                    FBox(f,u,.62,.16,.3,.24,.03,P(NativeBlockLooks.Plain,0xD8D4C0,RoleLandmark));FBox(f,u,.22,.12,tw+.2,.18,.2,With(P(NativeBlockLooks.SteelGreen,0x1E1E1E,RoleLandmark),detail:true));End();
                }
                else foreach(var run in Runs(f,.05,bh,u-bw/2,u+bw/2,.06))
                {
                    double ru=(run[0]+run[1])/2,rw=run[1]-run[0];FBox(f,ru,bh/2,.02,rw,bh-.05,.04,door);
                    double diag=Math.Sqrt(rw*rw+(bh*.5)*(bh*.5)),ang=Math.Atan2(bh*.5,rw)*180/Math.PI;if(rw>1.2){FBox(f,ru,bh*.3,.05,diag-.2,.14,.02,With(P(NativeBlockLooks.Timber,0x26402F,RoleLandmark),detail:true),-ang);FBox(f,ru,bh*.75,.05,rw-.1,.14,.02,With(P(NativeBlockLooks.Timber,0x26402F,RoleLandmark),detail:true));}
                }
                // The lunette over the doors: glazing bars in the arch's half circle.
                if(Open(f,u-bw/2,u+bw/2,bh,bh+bw/2,.02,false)){Begin();FBox(f,u,bh+bw*.22,.015,bw*.8,bw*.4,.03,P(NativeBlockLooks.Plain,BlackHex,RoleLandmark));for(int g=-2;g<=2;g++)FBox(f,u+g*bw*.16,bh+bw*.2,.035,.05,bw*.38,.012,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleLandmark),detail:true),g*12);End();}
            }
            {double cu=uc;Begin();FBox(f,cu,7.2,.12,1.4,1.4,.12,P(NativeBlockLooks.SteelGreen,0x2A3A30,RoleHigh));FBox(f,cu,7.2,.19,1.15,1.15,.02,Glow(0xF0E8D0,.3f,RoleHigh));
                FBox(f,cu+.18,7.25,.205,.06,.5,.012,P(NativeBlockLooks.Plain,0x141210,RoleHigh),-70);FBox(f,cu,7.38,.21,.05,.4,.012,P(NativeBlockLooks.Plain,0x141210,RoleHigh),10);End();}
            // The overhead line's brackets on the frieze: an arm out over each track with the contact wire's insulator.
            for(int k=0;k<n;k++){double u=u0+bay*(k+.5);Begin();FBox(f,u,6.1,1.0,.08,.08,2.0,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleHigh),detail:true,fade:true));FBox(f,u,5.95,1.95,.1,.22,.1,With(P(NativeBlockLooks.Plain,0x8AA0A0,RoleHigh),detail:true,fade:true));End();}
            // Gooseneck lamps on the piers either side of the middle (one working, one stuttering).
            foreach(double s in new[]{-1.0,1.0}){double u=uc+s*bay;Begin();FBox(f,u,6.9,.45,.1,.1,.9,P(NativeBlockLooks.SteelGreen,0x2A2E2C,RoleLamp));FBox(f,u,6.78,.92,.36,.18,.42,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleLamp));
                FBox(f,u,6.67,.92,.26,.04,.3,Glow(SodiumHex,1.9f,RoleLamp,s<0?DriveSteady:DriveFlicker,.27f));if(End()){At(f,u,0,1.0,out double lx,out double lz);Light(lx,f.Base+6.4,lz,SodiumHex,1.0,12,1.0,s<0?GlowLamp:GlowLampFlicker,.27);}}
            // The tracks: a pair of rails and their bed of setts out of each bay across the cross street (in 1 m lengths, so a
            // length that meets a spawn's exit or the drop's column is left out and the rest stay).
            {
                int g=SlabAt(f,uc);if(g>=0)for(int k=0;k<n;k++)
                {
                    double u=u0+bay*(k+.5);
                    for(double d=.55;d<5.2;d+=1.0){Begin();GroundBox(f,g,u,d,1.75,1.0,.0025,With(P(NativeBlockLooks.Cobble,0x5A5650,RoleStreet),detail:true));
                        foreach(double r in new[]{-.72,.72})GroundBox(f,g,u+r,d,.07,1.0,.022,P(NativeBlockLooks.SteelGreen,0x5A5450,RoleStreet));End();}
                }
            }
        }
        private void TramDepotRoof(int i)
        {
            // The sawtooth north-light roof: five teeth across the hall, each a pitched slab and a glazed upright face.
            var b=blocks[i];double top=b.pos.y+b.size.y/2;bool alongX=b.size.x>b.size.z;double span=(alongX?b.size.x:b.size.z)-.8,depth=(alongX?b.size.z:b.size.x)-.6;
            int n=5;double pitch=depth/n,rise=2.6,ang=Math.Atan2(rise,pitch)*180/Math.PI,len=Math.Sqrt(pitch*pitch+rise*rise);int outSign=alongX?(b.pos.z<0?-1:1):(b.pos.x<0?-1:1);
            for(int k=0;k<n;k++)
            {
                double a0=-depth/2+k*pitch;double mid=a0+pitch/2;
                double sx=alongX?span:len,sz=alongX?len:span,x=b.pos.x+(alongX?0:mid*outSign),z=b.pos.z+(alongX?mid*outSign:0);
                var q=alongX?AxisRot(1,0,0,ang*outSign):AxisRot(0,0,1,-ang*outSign);
                Box(i,x,top+rise/2,z,sx,.16,sz,P(NativeBlockLooks.Corrugated,0x4A4C4A,RoleLandmark),q);
                double gx=b.pos.x+(alongX?0:(a0+pitch)*outSign),gz=b.pos.z+(alongX?(a0+pitch)*outSign:0);
                var glaze=P(NativeBlockLooks.WindowLit,0x3A4450,RoleLandmark);glaze.Level=0;
                Box(i,gx,top+rise/2,gz,alongX?span-.12:.08,rise,alongX?.08:span-.12,glaze);
            }
        }

        // ---------------- the main lane's rows ----------------
        private void CinemaFront(Face f,double a,double c)
        {
            // The Roxy: a marquee over the doors (its lit edges, the dead title board), glazed poster cases, the ticket window.
            string id=blocks[f.Block].id;double len=c-a;if(len<8)return;double u=(a+c)/2;Use(f,a,c,0,4.6);
            Begin();
            FBox(f,u,1.6,.03,5.0,3.2,.04,P(NativeBlockLooks.Plain,0x141012,RoleFacade));for(int k=-1;k<=1;k++)FBox(f,u+k*1.6,1.4,.06,1.3,2.7,.02,P(NativeBlockLooks.Plain,k==0?GlassHex:0x1E1A1C,RoleFacade));
            foreach(double s in new[]{-1.0,1.0}){double pu=u+s*3.6;FBox(f,pu,1.6,.08,1.1,1.6,.08,P(NativeBlockLooks.SteelGreen,0x3A2A1E,RoleFacade));FBox(f,pu,1.6,.125,.9,1.35,.012,Glow(Pick(new[]{0xD8C8A0,0xC8A0A8},Hash(id,(int)s,1)),.35f,RoleFacade,DriveFlicker,(float)Hash(id,(int)s,2)));}
            FBox(f,u-2.4,1.3,.06,.8,.9,.04,P(NativeBlockLooks.Plain,0x2A2420,RoleFacade));FBox(f,u-2.4,1.4,.085,.6,.4,.012,P(NativeBlockLooks.Plain,GlassHex,RoleFacade));
            End();
            // The marquee: a deep canopy above a Hulk (3.6-4.4 m) with tube-lit edges, a title board on its front, ROXY on top.
            Begin();var steel=With(P(NativeBlockLooks.SteelGreen,0x2A1E22,RoleHigh),shadow:true,fade:true);FBox(f,u,3.75,.9,6.4,.3,1.8,steel);
            FBox(f,u,4.15,1.72,6.4,.55,.08,P(NativeBlockLooks.Plain,0xE8E0C8,RoleSign));Text(f,"NOW SHOWING",u,4.25,1.765,.035,.008,P(NativeBlockLooks.Plain,0x1A1A1A,RoleSign));Text(f,"THE END",u,3.98,1.765,.035,.008,P(NativeBlockLooks.Plain,0x8A1A16,RoleSign));
            FBox(f,u,3.58,1.8,6.4,.05,.05,Glow(0xFFD890,1.4f,RoleSign,DriveBulbs,.3f));FBox(f,u-3.2,3.58,.9,.05,.05,1.8,Glow(0xFFD890,1.4f,RoleSign,DriveBulbs,.6f));FBox(f,u+3.2,3.58,.9,.05,.05,1.8,Glow(0xFFD890,1.4f,RoleSign,DriveBulbs,.9f));
            for(int k=0;k<8;k++)FBox(f,u-2.8+k*.8,3.585,1.5,.12,.04,.12,Glow(0xFFE8B0,1.6f,RoleSign,DriveBulbs,k*.37f));
            End();
            At(f,u,0,1.4,out double lx,out double lz);Light(lx,f.Base+3.3,lz,0xFFD890,1.0,9,1.6,GlowNeon,.3);
            // The blade sign up the corner: ROXY in neon down a tall blade, both faces, one letter out.
            double bu=a+.8;if(!Open(f,bu-.3,bu+.3,5.0,12.0,.05,false))bu=c-.8;
            if(Open(f,bu-.3,bu+.3,5.0,12.0,.05,false))
            {
                Begin();double y0=5.2,ln=6.0;FBox(f,bu,y0+ln/2,.75,.2,ln,1.3,With(P(NativeBlockLooks.Plain,0x1E1418,RoleHigh),shadow:true,fade:true));
                string word="ROXY";double cell=.26;
                for(int side=-1;side<=1;side+=2)for(int k=0;k<word.Length;k++){double ly=y0+ln-(k+1)*6*cell+cell*.5;At(f,bu,0,.75,out double cx,out double cz);
                    foreach(var g in GlyphBoxes(word[k])){double gy=ly+(5-g[1]-g[3]*.5)*cell,gd=(g[0]+g[2]*.5-1.5)*cell*-side;double ox=f.Axis==0?f.Sign*gd:Tx(f)*.11*side,oz=f.Axis==2?f.Sign*gd:Tz(f)*.11*side;
                        double sx=f.Axis==0?g[2]*cell:.03,sz=f.Axis==2?g[2]*cell:.03;Box(f.Block,cx+ox,f.Base+gy,cz+oz,sx,g[3]*cell,sz,k==2?Glow(0x3A2030,.05f,RoleSign):Glow(0xFF4AA0,1.8f,RoleSign,k==1?DriveFlicker:DriveSteady,.15f));}}
                End();At(f,bu,0,1.4,out double hx,out double hz);Light(hx,f.Base+y0+ln*.5,hz,0xFF4AA0,.6,8,1.6,GlowNeon,.15);
            }
        }
        /// <summary>A blade sign out of face f at u (a hotel's, a bar's): `word` down the blade on both faces, neon or dark.</summary>
        private void Blade(Face f,double u,double y0,double ln,string word,int hex,bool lit,int dead)
        {
            if(!Open(f,u-.3,u+.3,y0,y0+ln,.05,false))return;
            Begin();FBox(f,u,y0+ln/2,.62,.18,ln,1.0,With(P(NativeBlockLooks.Plain,0x1E1A1E,RoleHigh),shadow:true,fade:true));double cell=Math.Min(.16,ln/(word.Length*6));
            for(int side=-1;side<=1;side+=2)for(int k=0;k<word.Length;k++){double ly=y0+ln-(k+1)*6*cell+cell*.5;At(f,u,0,.62,out double cx,out double cz);
                foreach(var g in GlyphBoxes(word[k])){double gy=ly+(5-g[1]-g[3]*.5)*cell,gd=(g[0]+g[2]*.5-1.5)*cell*-side;double ox=f.Axis==0?f.Sign*gd:Tx(f)*.1*side,oz=f.Axis==2?f.Sign*gd:Tz(f)*.1*side;
                    double sx=f.Axis==0?g[2]*cell:.03,sz=f.Axis==2?g[2]*cell:.03;Box(f.Block,cx+ox,f.Base+gy,cz+oz,sx,g[3]*cell,sz,!lit||k==dead?Glow(0x302428,.05f,RoleSign):Glow(hex,1.7f,RoleSign,DriveFlicker,.55f));}}
            End();if(lit){At(f,u,0,1.0,out double hx,out double hz);Light(hx,f.Base+y0+ln*.6,hz,hex,.55,7,1.4,GlowNeon,.55);}
        }

        // ---------------- the squares ----------------
        /// <summary>The quarters' squares: a market stall on the crates in the middle of each (the kiosk and the makeshift memorial
        /// on the landmark's back wall are laid with its face, <see cref="SquareWall"/>). Clear of the drop (its keep-out) and of
        /// the route's line (the route turns up the lane before the square).</summary>
        private void QuarterSquares()
        {
            foreach(var pre in new[]{"xq","fq","tq"})
            {
                var crates=new List<int>();for(int k=0;k<3;k++)if(byStem.TryGetValue("crate-"+pre+"-"+k,out int c))crates.Add(c);if(crates.Count==3)Stall(crates,pre);
            }
        }
        private void Stall(List<int> crates,string pre)
        {
            // Posts at the crates' corners (each within the wall band of its crate), a striped canopy over the lot above a Hulk,
            // produce boxes and sacks on the crates, a lamp hung under the canopy.
            double x0=double.MaxValue,x1=double.MinValue,z0=double.MaxValue,z1=double.MinValue;
            foreach(int c in crates){var b=blocks[c];x0=Math.Min(x0,b.pos.x-b.size.x/2);x1=Math.Max(x1,b.pos.x+b.size.x/2);z0=Math.Min(z0,b.pos.z-b.size.z/2);z1=Math.Max(z1,b.pos.z+b.size.z/2);}
            int owner0=crates[0];double cx=(x0+x1)/2,cz=(z0+z1)/2;var post=P(NativeBlockLooks.Timber,0x4A3A2A,RoleClutter);string id=blocks[owner0].id;
            Begin();
            foreach(int c in crates)
            {
                var b=blocks[c];double hx=b.size.x/2,hz=b.size.z/2;double px=b.pos.x+(b.pos.x<cx?-hx+.06:hx-.06),pz=b.pos.z+(b.pos.z<cz?-hz+.06:hz-.06);
                Box(c,px,(b.pos.y+b.size.y/2+3.75)/2,pz,.09,3.75-(b.pos.y+b.size.y/2),.09,post);
                for(int k=0;k<3;k++){double s=Hash(b.id,k,1);Box(c,b.pos.x+(s-.5)*hx,b.pos.y+b.size.y/2+.14,b.pos.z+(Hash(b.id,k,2)-.5)*hz,.45,.28,.35,With(P(k==1?NativeBlockLooks.Plain:NativeBlockLooks.Timber,k==1?Pick(new[]{0x6A7A3A,0x8A3A2A,0xA8803A},s):0x7A5A3A,RoleClutter),detail:true),AxisRot(0,1,0,s*40));}
                Box(c,b.pos.x+(Hash(b.id,4,1)-.5)*hx,b.pos.y+b.size.y/2+.2,b.pos.z+(Hash(b.id,4,2)-.5)*hz,.5,.4,.4,With(P(NativeBlockLooks.Plain,SandbagHex,RoleClutter),detail:true),AxisRot(0,1,0,20));
            }
            int strips=6;double w=x1-x0+.7,d=z1-z0+.7;bool alongX=w>=d;var st=AwningStripes[(int)(Hash(id,0,31)*AwningStripes.Length)%AwningStripes.Length];
            for(int t=0;t<strips;t++){double a=-(alongX?d:w)/2+(alongX?d:w)/strips*(t+.5);double sx=alongX?w:(w/strips),sz=alongX?(d/strips):d;
                Box(owner0,cx+(alongX?0:a),3.78-.03*(t-2.5),cz+(alongX?a:0),sx,.04,sz,With(P(NativeBlockLooks.Plain,st[t%2],RoleHigh),fade:true),alongX?AxisRot(1,0,0,4):AxisRot(0,0,1,-4));}
            Box(owner0,cx,3.5,cz,.04,.36,.04,With(P(NativeBlockLooks.Plain,0x1A1A1A,RoleHigh),detail:true));Box(owner0,cx,3.25,cz,.24,.14,.24,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleHigh));Box(owner0,cx,3.16,cz,.16,.04,.16,Glow(0xFFC88A,1.6f,RoleHigh,DriveFlicker,.8f));
            if(End())Light(cx,2.9,cz,0xFFC88A,.8,7,.8,GlowLampFlicker,.8);
        }
        private void SquareWall(Face f,string pre)
        {
            // The kiosk on the square (a shuttered window, its counter, the canopy above a Hulk, its sign), and a makeshift
            // memorial along the wall: photographs and notes, candles at the foot, flowers, WE REMEMBER chalked over it.
            string id=blocks[f.Block].id;double len=f.U1-f.U0,uc=(f.U0+f.U1)/2;
            double ku=uc+len*.22;string sign=pre=="xq"?"NEWS":pre=="fq"?"TOBACCO":"TICKETS";
            if(Open(f,ku-1.6,ku+1.6,0,4.2,.05))
            {
                Use(f,ku-1.6,ku+1.6,0,4.2);Begin();
                FBox(f,ku,1.55,.05,2.6,1.5,.08,P(NativeBlockLooks.Corrugated,ShutterHex,RoleClutter));FBox(f,ku,.75,.12,2.8,.08,.22,P(NativeBlockLooks.Timber,0x5A4632,RoleClutter));FBox(f,ku,.4,.05,2.8,.7,.1,P(NativeBlockLooks.Plain,0x3A3430,RoleClutter));
                for(int k=0;k<4;k++)FBox(f,ku-1.05+k*.7,.42,.11,.5,.36,.012,With(P(NativeBlockLooks.Plain,Pick(new[]{0xC8B890,0x8AA0B8,0xD89A70,0xA8B890},Hash(id,k,61)),RoleClutter),detail:true),(Hash(id,k,62)-.5)*8);
                FBox(f,ku,3.5,.75,3.4,.1,1.5,With(P(NativeBlockLooks.SteelGreen,0x2A3A34,RoleHigh),shadow:true,fade:true),0,6);
                FBox(f,ku,3.82,1.48,3.0,.4,.06,P(NativeBlockLooks.Plain,0x1E2A2E,RoleSign));Text(f,sign,ku,3.72,1.515,.045,.01,P(NativeBlockLooks.Plain,0xE8D890,RoleSign));
                End();
            }
            double mu=uc-len*.18;
            if(Open(f,mu-2.2,mu+2.2,0,2.6,.05))
            {
                Use(f,mu-2.2,mu+2.2,0,2.6);Begin();
                for(int k=0;k<16;k++){double s=Hash(id,k,71);double pu=mu-1.9+3.8*Hash(id,k,72),py=1.0+1.1*Hash(id,k,73);bool photo=s<.4;
                    FBox(f,pu,py,.012+.004*(k%3),photo?.16:.2,photo?.22:.26,.004,With(P(NativeBlockLooks.Plain,photo?0x3A3632:Pick(new[]{0xE8E0C8,0xD8C8A8,0xF0E8D8},s),RoleFacade),detail:true),(s-.5)*20);}
                Text(f,"WE REMEMBER",mu,2.2,.03,.05,.006,With(P(NativeBlockLooks.Plain,0xE8E4D8,RoleFacade),detail:true),(Hash(id,1,74)-.5)*4);
                for(int k=0;k<9;k++){double cu=mu-1.8+3.6*Hash(id,k,75),ch=.06+.06*Hash(id,k,76);FBox(f,cu,ch/2,.08+.06*Hash(id,k,77),.05,ch,.05,P(NativeBlockLooks.Plain,0xE8E0C8,RoleClutter));
                    FBox(f,cu,ch+.03,.08+.06*Hash(id,k,77),.03,.05,.03,Glow(0xFFB060,1.4f,RoleClutter,DriveFire,(float)Hash(id,k,78)));}
                for(int k=0;k<5;k++){double cu=mu-1.5+3.0*Hash(id,k,79);FBox(f,cu,.025,.15,.3,.05,.18,With(P(NativeBlockLooks.Plain,Pick(new[]{0x8A2A3A,0xD8C840,0xE8E0E0,0x6A3A6A},Hash(id,k,80)),RoleClutter),detail:true),0,0);}
                End();At(f,mu,0,.4,out double lx,out double lz);Light(lx,f.Base+.25,lz,0xFFB060,.35,3.5,.6,GlowNeon,.2);
            }
        }

        // ---------------- the routes through the quarters ----------------
        /// <summary>The signs the routes' span wires carry (for the checks).</summary>
        public int SpanSigns;private string RouteTo;
        /// <summary>Wayfinding down each live route's centre line (the record's `via`): over the start of each straight stretch down a
        /// street, a sign on a span wire across it (the zone's name and the metres left in its ink, a lit edge in its accent, above
        /// 5.5 m); then a lamp hung from a span wire every 6.5 m. The wires run wall to wall (both ends on building faces, between
        /// their windows); the wilds stay dark.</summary>
        private void RouteWires()
        {
            if(hub==null)return;
            foreach(var r in hub.Routes)
            {
                if(r.Reserved)continue;var g=hub.Gate(r.Gate);if(g==null)continue;var line=RouteLine(r,g);double len=line.Length;if(len<20)continue;
                int accent=NativeExpeditionJoinZones.Of(r.To).Accent;string name=RouteSign(r,true);RouteTo=r.To;
                // Each straight stretch of the line down a street (square to the walls, 8 m or longer, past the inner ring).
                for(int i=1;i<line.X.Length;i++)
                {
                    double ex=line.X[i]-line.X[i-1],ez=line.Z[i]-line.Z[i-1],sl=Math.Sqrt(ex*ex+ez*ez);if(sl<8||Math.Max(Math.Abs(ex),Math.Abs(ez))/sl<.97)continue;
                    bool signed=false;double lastLamp=-99;
                    for(double at=line.S[i-1]+2.5;at<line.S[i]-2.5;at+=1.5)
                    {
                        if(at<25)continue;line.At(at,out double x,out double z,out double dx,out double dz);if(Wild(x,z))break;
                        if(!Span(x,z,dx,dz,out var fa,out double ua,out var fb,out double ub,out double width))continue;
                        if(!signed){if(SpanSign(fa,ua,fb,ub,x,z,dx,dz,name,accent,r.Path*(1-at/len))){signed=true;SpanSigns++;lastLamp=at;}continue;}
                        if(at-lastLamp>=6.5&&SpanLamp(fa,ua,fb,ub,x,z,(int)at))lastLamp=at;
                    }
                }
            }
        }
        /// <summary>The two building faces across the street at (x, z) looking along (dx, dz): faces whose planes cross the line's
        /// normal within 14 m either side and whose spans hold the point, nearest each side.</summary>
        private bool Span(double x,double z,double dx,double dz,out Face fa,out double ua,out Face fb,out double ub,out double width)
        {
            fa=fb=null;ua=ub=width=0;double nx=-dz,nz=dx;double da=99,db=99;
            foreach(var f in faces)
            {
                if(f.Front==0||f.Top-f.Base<7)continue;if(Math.Abs(Nx(f)*nx+Nz(f)*nz)<.95)continue;
                double dist=f.Axis==0?(f.Plane-x)*Math.Sign(nx):(f.Plane-z)*Math.Sign(nz);if(Math.Abs(dist)>14||Math.Abs(dist)<1)continue;
                // The face must look back at the line.
                double toward=f.Axis==0?Math.Sign(x-f.Plane)*f.Sign:Math.Sign(z-f.Plane)*f.Sign;if(toward<=0)continue;
                double u=U(f,x,z);if(u<f.U0+1||u>f.U1-1)continue;
                if(dist>0&&(dist<da||dist==da&&fa!=null&&string.CompareOrdinal(blocks[f.Block].id,blocks[fa.Block].id)<0)){da=dist;fa=f;ua=u;}
                else if(dist<0&&(-dist<db||-dist==db&&fb!=null&&string.CompareOrdinal(blocks[f.Block].id,blocks[fb.Block].id)<0)){db=-dist;fb=f;ub=u;}
            }
            width=da+db;return fa!=null&&fb!=null&&width<22;
        }
        /// <summary>A height (from `first`, then the storeys' gaps) and a shift along both walls where a wire's two anchors find clear
        /// wall: between the windows, lamps and escapes already on them. False when none does.</summary>
        private bool Anchors(Face fa,ref double ua,Face fb,ref double ub,double first,out double y)
        {
            foreach(double h in new[]{first,7.5,6.6,8.3,9.2,5.9})foreach(double sh in new[]{0.0,.8,-.8,1.6,-1.6})
            {
                double a=ua+sh,b=ub-sh*(Tx(fa)*Tx(fb)+Tz(fa)*Tz(fb));if(h>fa.Top-fa.Base-1||h>fb.Top-fb.Base-1)continue;
                if(Open(fa,a-.25,a+.25,h-.35,h+.35,.03)&&Open(fb,b-.25,b+.25,h-.35,h+.35,.03)){ua=a;ub=b;y=h;return true;}
            }
            y=0;return false;
        }
        private void Wire(Face fa,double ua,Face fb,double ub,double y,double sag,int segs,out double mx,out double my,out double mz)
        {
            At(fa,ua,0,.05,out double ax,out double az);At(fb,ub,0,.05,out double bx,out double bz);double ay=fa.Base+y,by=fb.Base+y;
            double len=Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az)),yaw=Math.Atan2(bx-ax,bz-az)*180/Math.PI;var pen=With(P(NativeBlockLooks.Plain,0x141414,RoleHigh),detail:true);
            double Y(double t)=>ay+(by-ay)*t-sag*4*t*(1-t);
            for(int k=0;k<segs;k++){double t0=(double)k/segs,t1=(k+1.0)/segs,tm=(t0+t1)/2,run=len/segs,dy=Y(t1)-Y(t0);
                onFace=null;Box(fa.Block,ax+(bx-ax)*tm,Y(tm),az+(bz-az)*tm,.025,.025,Math.Sqrt(run*run+dy*dy)+.01,pen,Then(AxisRot(0,1,0,yaw),AxisRot(1,0,0,-Math.Atan2(dy,run)*180/Math.PI)));}
            FBox(fa,ua,y,.06,.08,.08,.12,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleHigh),detail:true));FBox(fb,ub,y,.06,.08,.08,.12,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleHigh),detail:true));
            mx=(ax+bx)/2;mz=(az+bz)/2;my=Y(.5);
        }
        private bool SpanSign(Face fa,double ua,Face fb,double ub,double x,double z,double dx,double dz,string name,int accent,double left)
        {
            if(!Anchors(fa,ref ua,fb,ref ub,6.6,out double wy))return false;
            Begin();Wire(fa,ua,fb,ub,wy,.25,8,out double mx,out double my,out double mz);
            // The board hangs under the wire, square to the street (it faces along the route both ways), its letters in the accent.
            double yaw=Math.Atan2(dx,dz)*180/Math.PI;string word=name+" "+Metres(left)+"M";double cell=.07,tw=TextWidth(word,cell)+.5;
            int parent=fa.Block;onFace=null;var q=AxisRot(0,1,0,yaw);
            Box(parent,mx,my-.45,mz,tw,.62,.06,P(NativeBlockLooks.Plain,0x10161A,RoleSign),q);Box(parent,mx,my-.12,mz,.03,.4,.03,With(P(NativeBlockLooks.Plain,0x141414,RoleSign),detail:true));
            // Lettered on the face that meets the walk out (toward the gate: -dx, -dz), its lit edge on both faces.
            {
                int side=-1;double ox=dx*side*.035,oz=dz*side*.035;double rx=dz*side,rz=-dx*side;double w0=TextWidth(word,cell);
                for(int k=0;k<word.Length;k++)foreach(var gl in GlyphBoxes(word[k])){double cu=-w0/2+k*4*cell+(gl[0]+gl[2]*.5)*cell,cv=(5-gl[1]-gl[3]*.5)*cell-2.5*cell;
                    Box(parent,mx+ox+rx*cu,my-.45+cv,mz+oz+rz*cu,gl[2]*cell,gl[3]*cell,.012,P(NativeBlockLooks.Plain,ZoneInk(RouteTo),RoleSign),q);}
            }
            Box(parent,mx,my-.45-.27,mz,tw-.1,.03,.09,Glow(accent,.9f,RoleSign),q);
            lettering.Add(word);
            if(!End())return false;Use(fa,ua-.3,ua+.3,wy-.4,wy+.4);Use(fb,ub-.3,ub+.3,wy-.4,wy+.4);Light(mx,my-.6,mz,accent,.6,8,1.2,GlowNeon);return true;
        }
        private bool SpanLamp(Face fa,double ua,Face fb,double ub,double x,double z,int k)
        {
            if(!Anchors(fa,ref ua,fb,ref ub,6.4,out double wy))return false;
            Begin();Wire(fa,ua,fb,ub,wy,.35,8,out double mx,out double my,out double mz);
            double s=Hash("span",(int)(x*10),(int)(z*10));int state=s<.55?0:s<.8?1:2;int parent=fa.Block;onFace=null;
            Box(parent,mx,my-.2,mz,.04,.36,.04,With(P(NativeBlockLooks.Plain,0x141414,RoleHigh),detail:true));Box(parent,mx,my-.46,mz,.5,.16,.5,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleHigh));
            Box(parent,mx,my-.56,mz,.34,.05,.34,state==2?P(NativeBlockLooks.Plain,0x3A3630,RoleHigh):Glow(SodiumHex,1.9f,RoleLamp,state==1?DriveFlicker:DriveSteady,(float)s*11));
            if(!End())return false;Use(fa,ua-.3,ua+.3,wy-.4,wy+.4);Use(fb,ub-.3,ub+.3,wy-.4,wy+.4);if(state<2)Light(mx,my-.8,mz,SodiumHex,1.0,12,1.0,state==0?GlowLamp:GlowLampFlicker,s*11);return true;
        }
    }
}
