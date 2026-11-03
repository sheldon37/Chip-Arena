using System;

namespace ChipArena.UnityConversion
{
    // The generated Expedition's districts round the Backrooms' two entrances (Claude, 2026-11-03; docs/EXPEDITION-ENEMY-REWORK.md
    // section 5; owner: "design the 'Surrounding area' around the instance for these as well"). The authority rolls them as the
    // themes `backrooms` (Level 0) and `poolrooms`; NativeExpeditionCityKit.Generated blends each building toward them as it does
    // the other three:
    //  - Level 0, the office park: beige stucco offices with long ribbon windows, every pane lit the same sick fluorescent
    //    yellow (the odd one dead or stuttering), mullions and sills, a darker plinth; tinted glass lobby doors on the street faces,
    //    their EXIT signs green or, one in four, wrongly red; air handlers and a transformer on the roofs; fluorescent street lamps
    //    that flicker more than the town's; faded parking bays along the streets.
    //  - The Poolrooms, the municipal baths: white tile to 3.2 m under a teal band, white render above, glass-block panels lit pool
    //    blue from inside, BATHS and SWIMMING lettering; frosted glass doors; barrel-vaulted roofs of glass and white ribs, lit
    //    from below, a boiler stack on some; pool-white street lamps; puddles on the streets.
    // Their entrance frames and halls take the same looks (Generated: EntranceLintel, InstanceHall). Same placement rules as the
    // rest: below 3.1 m nothing stands out more than WallBand except flat paint; every piece goes through Open and Use.
    public sealed partial class NativeExpeditionCityKit
    {
        public const int LookLiminal=4,LookBaths=5;
        private static readonly int[] StuccoHexes={0xCBB98E,0xC4B286,0xD0C096,0xBFAE84};
        private const int LiminalGlowHex=0xF4E7A2,LiminalLampHex=0xF2EDB8,LiminalTintHex=0x2E3228,ExitGreenHex=0x3FE07A,
            BathsTileHex=0xE9EEEA,BathsTealHex=0x2FA6A6,BathsRenderHex=0xDDE3E0,BathsGlowHex=0x7FD8E0,BathsLampHex=0xCFF6F2;
        private static readonly string[] BathsSigns={"BATHS","SWIMMING","PUBLIC BATHS","LEISURE POOL"};

        // ---------------- Level 0: the office park ----------------
        private void LiminalFace(Face f)
        {
            string id=blocks[f.Block].id;double h=f.Top-f.Base,s=Hash(id,f.Index,301);
            Skin(f,0,h,.016,P(NativeBlockLooks.Plain,Pick(StuccoHexes,s*7),RoleFacade));
            foreach(var run in Runs(f,.05,.6,f.U0+.1,f.U1-.1,.05,false))FBox(f,(run[0]+run[1])/2,.33,.03,run[1]-run[0],.56,.02,With(P(NativeBlockLooks.ConcreteBoard,0x8E8068,RoleFacade),detail:true));
            // Ribbon windows from 3.8 m, one per storey: panes every 1.5 m, each lit the same yellow (one in a dozen dead, a few
            // stuttering), mullions between them, a sill under the run.
            for(double y=3.8;y+1.3<h-.5;y+=3.2)
                foreach(var run in Runs(f,y-.1,y+1.3,f.U0+.5,f.U1-.5,.1))
                {
                    double w=run[1]-run[0];if(w<1.6||!Open(f,run[0],run[1],y-.1,y+1.3,.02))continue;Use(f,run[0],run[1],y-.1,y+1.3);
                    int panes=Math.Max(1,(int)Math.Round(w/1.5));double pw=w/panes;
                    for(int k=0;k<panes;k++)
                    {
                        double u=run[0]+pw*(k+.5),q=Hash(id,f.Index*31+k+(int)(y*7),302);
                        FBox(f,u,y+.6,.03,pw-.08,1.1,.04,q<.08?P(NativeBlockLooks.Plain,0x2A2A22,RoleFacade):Glow(LiminalGlowHex,1.15f,RoleFacade,q<.14?DriveFlicker:DriveSteady,(float)q*9));
                    }
                    for(int k=0;k<=panes;k++)FBox(f,run[0]+pw*k,y+.6,.05,.08,1.2,.06,With(P(NativeBlockLooks.SteelGreen,0x6E6658,RoleFacade),detail:true));
                    FBox(f,(run[0]+run[1])/2,y-.04,.06,w+.1,.08,.1,With(P(NativeBlockLooks.Plain,0xA89C7C,RoleFacade),detail:true));
                }
            if(f.Front<1)return;
            // A lobby on the street: tinted glass doors in a dark frame, the lobby's dim glow behind the tint, an EXIT sign over it.
            foreach(var run in Runs(f,.6,3.0,f.U0+.6,f.U1-.6))
            {
                double u=(run[0]+run[1])/2;if(run[1]-run[0]<3.2||!Open(f,u-1.45,u+1.45,.05,3.05,.02))continue;Use(f,u-1.45,u+1.45,.05,3.05);
                Begin();
                FBox(f,u,1.35,.02,2.6,2.6,.03,P(NativeBlockLooks.Plain,LiminalTintHex,RoleFacade));
                FBox(f,u,1.3,.026,2.3,2.3,.02,Glow(0xC9B060,.32f,RoleFacade));
                FBox(f,u,2.68,.045,2.8,.1,.06,P(NativeBlockLooks.SteelGreen,0x3A3830,RoleFacade));
                foreach(int sg in new[]{-1,0,1})FBox(f,u+sg*1.35,1.35,.045,sg==0?.06:.1,2.7,.06,P(NativeBlockLooks.SteelGreen,0x3A3830,RoleFacade));
                if(Hash(id,f.Index,303)<.65)
                {
                    bool wrong=Hash(id,f.Index,304)<.25;double cell=.03,tw=TextWidth("EXIT",cell);
                    FBox(f,u,2.9,.04,tw+.12,.2,.03,P(NativeBlockLooks.Plain,0x161816,RoleSign));
                    Text(f,"EXIT",u,2.9-2.5*cell,.058,cell,.006,Glow(wrong?RedHex:ExitGreenHex,1.8f,RoleSign,Hash(id,f.Index,305)<.3?DriveFlicker:DriveSteady,(float)Hash(id,f.Index,306)*7));
                }
                End();break;
            }
        }
        // Air handlers and a humming transformer box; on some, a lit tube sign frame on the parapet.
        private void LiminalRoof(int i)
        {
            var b=blocks[i];string id=b.id;double top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,z0=b.pos.z-b.size.z/2;
            int n=1+(int)(Hash(id,0,311)*3);
            for(int k=0;k<n;k++)
            {
                double px=x0+1.6+(b.size.x-3.2)*Hash(id,k,312),pz=z0+1.6+(b.size.z-3.2)*Hash(id,k,313);
                Begin();Box(i,px,top+.6,pz,2.4,1.2,1.6,P(NativeBlockLooks.Plain,0xB8AC8E,RoleRoof));
                Box(i,px,top+1.22,pz,1.6,.04,1.0,With(P(NativeBlockLooks.FactoryGrating,0x5E5A50,RoleRoof),detail:true));
                Box(i,px+.9,top+1.35,pz,.5,.3,.5,With(P(NativeBlockLooks.SteelGreen,0x4A463E,RoleRoof),detail:true));End();
            }
            if(Hash(id,0,314)<.4)
            {
                double px=x0+1.2+(b.size.x-2.4)*Hash(id,0,315),pz=z0+1.2+(b.size.z-2.4)*Hash(id,0,316);
                Begin();Box(i,px,top+.7,pz,1.2,1.4,.9,P(NativeBlockLooks.SteelGreen,0x5A5C4E,RoleRoof));
                Box(i,px,top+1.45,pz,.12,.1,.12,Glow(ExitGreenHex,1.2f,RoleRoof,DriveFlicker,(float)Hash(id,0,317)*5));End();
            }
        }

        // ---------------- the Poolrooms: the municipal baths ----------------
        private void BathsFace(Face f)
        {
            string id=blocks[f.Block].id;double h=f.Top-f.Base;
            Skin(f,0,3.2,.016,P(NativeWardZeroLooks.Tile,BathsTileHex,RoleFacade,NativeCyberCityLooks.Bank));
            Skin(f,3.2,3.45,.03,P(NativeBlockLooks.Plain,BathsTealHex,RoleFacade));
            Skin(f,3.45,h,.016,P(NativeBlockLooks.Plain,BathsRenderHex,RoleFacade));
            // Glass-block panels from 4.3 m, one per storey: frosted blocks lit pool blue from inside, the grid between them.
            for(double y=4.3;y+1.6<h-.5;y+=3.3)
                foreach(var run in Runs(f,y-.1,y+1.6,f.U0+.6,f.U1-.6,.1))
                {
                    double w=Math.Min(run[1]-run[0],2.4+2.4*Hash(id,f.Index*11+(int)y,321));if(w<1.8)continue;
                    double u=run[0]+w/2+(run[1]-run[0]-w)*Hash(id,f.Index*13+(int)y,322);if(!Open(f,u-w/2,u+w/2,y-.1,y+1.6,.02))continue;Use(f,u-w/2,u+w/2,y-.1,y+1.6);
                    FBox(f,u,y+.75,.03,w,1.5,.04,Glow(BathsGlowHex,.55f,RoleFacade,Hash(id,f.Index,323)<.12?DriveFlicker:DriveSteady,(float)Hash(id,f.Index,324)*6));
                    for(double a=-w/2;a<=w/2+1e-3;a+=.3)FBox(f,u+a,y+.75,.05,.035,1.5,.04,With(P(NativeBlockLooks.Plain,0xE4ECEA,RoleFacade),detail:true));
                    for(double c=0;c<=1.5+1e-3;c+=.3)FBox(f,u,y+c,.05,w,.035,.04,With(P(NativeBlockLooks.Plain,0xE4ECEA,RoleFacade),detail:true));
                }
            if(f.Front<1)return;
            // The name in teal letters on the render, its edge lit; frosted doors under a teal canopy band.
            if(f.Front==2&&h>=8&&Hash(id,f.Index,325)<.7)
            {
                string t=BathsSigns[(int)(Hash(id,f.Index,326)*BathsSigns.Length)%BathsSigns.Length];double cell=.1,u=(f.U0+f.U1)/2,y=h-2.2,tw=TextWidth(t,cell);
                if(tw+.6<f.U1-f.U0&&Open(f,u-tw/2-.3,u+tw/2+.3,y-.15,y+5*cell+.15,.02)){Begin();Text(f,t,u,y,.04,cell,.012,P(NativeBlockLooks.Plain,BathsTealHex,RoleSign));FBox(f,u,y-.1,.03,tw+.3,.05,.02,Glow(BathsGlowHex,1.2f,RoleSign));if(End())Use(f,u-tw/2-.3,u+tw/2+.3,y-.15,y+5*cell+.15);}
            }
            foreach(var run in Runs(f,.6,3.0,f.U0+.6,f.U1-.6))
            {
                double u=(run[0]+run[1])/2;if(run[1]-run[0]<2.6||!Open(f,u-1.2,u+1.2,.05,3.05,.02))continue;Use(f,u-1.2,u+1.2,.05,3.05);
                Begin();FBox(f,u,1.3,.02,2.2,2.5,.03,Glow(0xBFE8E8,.4f,RoleFacade));
                FBox(f,u,1.3,.04,.06,2.5,.05,P(NativeBlockLooks.SteelGreen,0x9AA4A2,RoleFacade));
                FBox(f,u,2.82,.05,2.6,.22,.08,P(NativeBlockLooks.Plain,BathsTealHex,RoleFacade));
                At(f,u,0,.6,out double lx,out double lz);if(End())Light(lx,f.Base+2.6,lz,BathsGlowHex,.55,7,.3,GlowNeon);
                break;
            }
        }
        // A barrel vault along the roof's long side: glass panes lit pool blue between white ribs; a boiler stack on some.
        private void BathsRoof(int i)=>BathsVault(i,1.0,Hash(blocks[i].id,0,331)<.35);
        private void BathsVault(int i,double scale,bool stack)
        {
            var b=blocks[i];string id=b.id;double top=b.pos.y+b.size.y/2;bool alongX=b.size.x>=b.size.z;double len=Math.Max(b.size.x,b.size.z)-1.2,wid=Math.Min(b.size.x,b.size.z);
            double r=Math.Min(3.5,wid*.36)*scale;const int segs=7;
            for(int k=0;k<segs;k++)
            {
                double a0=Math.PI*k/segs,a1=Math.PI*(k+1)/segs,am=(a0+a1)/2,c=-r*Math.Cos(am),y=r*Math.Sin(am),chord=2*r*Math.Sin((a1-a0)/2),deg=(am-Math.PI/2)*180/Math.PI;
                double px=alongX?b.pos.x:b.pos.x+c,pz=alongX?b.pos.z+c:b.pos.z;
                Begin();Box(i,px,top+y,pz,alongX?len:chord,.08,alongX?chord:len,Glow(0xA8E6EA,.35f,RoleRoof),AxisRot(alongX?1:0,0,alongX?0:1,alongX?-deg:deg));End();
            }
            for(double a=-len/2;a<=len/2+1e-3;a+=len/Math.Max(1,Math.Round(len/2.4)))
                for(int k=0;k<segs;k++)
                {
                    double a0=Math.PI*k/segs,a1=Math.PI*(k+1)/segs,am=(a0+a1)/2,c=-r*Math.Cos(am),y=r*Math.Sin(am),chord=2*r*Math.Sin((a1-a0)/2),deg=(am-Math.PI/2)*180/Math.PI;
                    double px=alongX?b.pos.x+a:b.pos.x+c,pz=alongX?b.pos.z+c:b.pos.z+a;
                    Begin();Box(i,px,top+y,pz,alongX?.12:chord+.04,.14,alongX?chord+.04:.12,With(P(NativeBlockLooks.Plain,0xEEF2F0,RoleRoof),detail:true),AxisRot(alongX?1:0,0,alongX?0:1,alongX?-deg:deg));End();
                }
            if(!stack)return;
            double sx=b.pos.x+(alongX?len*.4:wid*.38),sz=b.pos.z+(alongX?wid*.38:len*.4),sh=5+2*Hash(id,0,332);
            Begin();Box(i,sx,top+sh/2,sz,.9,sh,.9,With(P(NativeBlockLooks.Plain,0xD8DCDA,RoleRoof),shadow:true));Box(i,sx,top+sh-.2,sz,1.1,.4,1.1,P(NativeBlockLooks.Plain,BathsTealHex,RoleRoof));End();
        }
    }
}
