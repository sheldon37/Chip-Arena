using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The town kit's buildings: facades, storefronts and signs, landmark frontages and roofs (NativeExpeditionCityKit.cs).
    public sealed partial class NativeExpeditionCityKit
    {
        private const int Shops=0,Residential=1,Office=2,Hotel=3,Depot=4,Kiosk=5,Civic=6,Meridian=7,MeridianTower=8,Foundry=9,Stack=10,Transit=11,CityHall=12,Clock=13;
        private static int KindOf(string n)
        {
            switch(n)
            {
                case "ne-shops":case "nw-shops":case "se-shops":case "sw-shops":return Shops;
                case "ne-tower":case "nw-tower":return Office;case "ne-hotel":return Hotel;case "e-depot":case "w-depot":return Depot;case "kiosk-e":case "kiosk-w":return Kiosk;
                case "wz-facility":return Meridian;case "wz-tower":return MeridianTower;case "foundry-hall":return Foundry;case "foundry-stack":return Stack;case "transit-hall":return Transit;
                case "city-hall":return CityHall;case "city-hall-clock":return Clock;case "e-annex":case "flank-e":case "flank-w":case "sw-row":return Civic;
                default:return Residential;
            }
        }
        /// <summary>Original shop names (no real brands), painted or in neon over the storefronts.</summary>
        public static readonly string[] ShopNames={"PHARMACY","ROSA'S DINER","LAUNDRY","HARDWARE","LUCKY PAWN","BAKERY","LIQUOR","GROCERY","BARBER","CORNER DELI","BOOKS","RADIO REPAIR","NOODLE BAR","CAFE","TAILOR","PIZZA","FLOWERS","SHOES","OPTICIAN","DONUTS","ARCADE","CLINIC"};
        private static readonly int[] FasciaHexes={0x2A3A34,0x3A2420,0x24283A,0x302A22,0x1E2A2E,0x3A3020,0x2C2230};
        private static readonly int[] NeonHexes={0xFF4A6A,0x4AD8FF,0xFFB040,0x7DFF8A,0xFF6AD0,0xFFF0C0};
        private static readonly int[] PaintHexes={0xE8DCC0,0xD8C890,0xC8D8D0,0xE0B0A0};
        private static readonly int[][] AwningStripes={new[]{0x8A2A24,0xC8BCA8},new[]{0x2A5A3A,0xC8BCA8},new[]{0x2A3A6A,0xB8B0A0},new[]{0x7A5A1A,0x2A2420}};
        private static readonly string[] OfficeNames={"NORTHGATE","WESTMARK"};
        private static readonly string[] BurningBlocks={"nw-tenement","se-loft","w-tower"};

        private void Building(int i,string name)
        {
            var b=blocks[i];int kind=KindOf(name);if(!facesOf.TryGetValue(i,out var fs))return;string id=b.id;
            foreach(var f in fs)
            {
                switch(kind)
                {
                    case Meridian:MeridianFace(f);break;
                    case MeridianTower:MeridianTowerFace(f);break;
                    case CityHall:CityHallFace(f);break;
                    case Clock:ClockFace(f);break;
                    case Foundry:FoundryFace(f);break;
                    case Stack:StackFace(f);break;
                    case Transit:TransitFace(f);break;
                    default:Facade(f,kind,name);break;
                }
            }
            if(kind!=Stack&&kind!=Clock)Roof(i,name,kind);
        }

        // ---------------- generic facades ----------------
        /// <summary>The open runs along face f between heights y0 and y1, inside [u0, u1] (occluders and keep rectangles cut them).</summary>
        private List<double[]> Runs(Face f,double y0,double y1,double u0,double u1,double pad=.15,bool keeps=true)
        {
            var cuts=new List<double[]>();foreach(var r in f.Occ)if(y0<r[3]&&y1>r[2])cuts.Add(new[]{r[0]-pad,r[1]+pad});if(keeps)foreach(var r in f.Keep)if(y0<r[3]&&y1>r[2])cuts.Add(new[]{r[0]-pad,r[1]+pad});
            cuts.Sort((a,c)=>a[0].CompareTo(c[0]));var o=new List<double[]>();double at=u0;
            foreach(var c in cuts){if(c[0]>at)o.Add(new[]{at,Math.Min(c[0],u1)});at=Math.Max(at,c[1]);if(at>=u1)break;}
            if(at<u1)o.Add(new[]{at,u1});o.RemoveAll(r=>r[1]-r[0]<.3);return o;
        }
        private void Facade(Face f,int kind,string name)
        {
            var b=blocks[f.Block];string id=b.id;double h=f.Top-f.Base;bool brick=b.material=="brick",stone=b.material=="stone";
            double ground=kind==Shops?4.4:kind==Office||kind==Hotel||kind==Depot?4.6:kind==Kiosk?h:4.0;
            double top=h-.9,span=top-ground;double bay=kind==Office?2.0:kind==Civic?2.6:kind==Shops?2.6:2.4;
            int storeys=span<2.4?0:Math.Max(1,(int)Math.Round(span/(kind==Office?3.2:3.0)));double storey=storeys>0?span/storeys:0;
            double ww=kind==Office?1.5:kind==Civic?1.1:1.0,wh=kind==Office?Math.Min(2.0,storey-.9):Math.Min(kind==Civic?1.7:1.5,storey-1.0);
            // Upper storeys: punched windows in a grid fitted to each open run.
            for(int r=0;r<storeys;r++)
            {
                double y0=ground+r*storey+.85,y1=y0+wh;
                foreach(var run in Runs(f,y0-.2,y1+.3,f.U0+.45,f.U1-.45,.1))
                {
                    int cols=(int)Math.Floor((run[1]-run[0])/bay);if(cols<1)continue;double pitch=(run[1]-run[0])/cols;
                    for(int c=0;c<cols;c++){double u=run[0]+pitch*(c+.5);if(!Open(f,u-ww/2-.12,u+ww/2+.12,y0-.12,y1+.25))continue;Window(f,u,y0,ww,wh,brick,r,c,Array.IndexOf(BurningBlocks,name)>=0&&r==storeys-1);}
                    if(r>=2)FBox(f,(run[0]+run[1])/2,y0-.05,.06,run[1]-run[0]-.2,.09,.12,P(NativeBlockLooks.ConcreteBoard,SillHex,RoleFacade));
                }
                // A string course under each storey on stone fronts.
                if(stone&&kind!=Kiosk)foreach(var run in Runs(f,y0-.95,y0-.6,f.U0,f.U1,.02))FBox(f,(run[0]+run[1])/2,y0-.75,.06,run[1]-run[0],.16,.12,With(P(NativeBlockLooks.ConcreteBoard,0xA49C8E,RoleFacade),detail:true));
            }
            // The cornice and parapet line along the top, and a band over the ground floor.
            foreach(var run in Runs(f,h-.5,h,f.U0,f.U1,.02,false))FBox(f,(run[0]+run[1])/2,h-.28,.11,run[1]-run[0],.32,.22,With(P(brick?NativeBlockLooks.ConcreteBoard:NativeBlockLooks.ConcreteBoard,brick?0x7A7064:0x9A9286,RoleHigh),shadow:true));
            if(kind!=Kiosk)foreach(var run in Runs(f,ground-.45,ground-.1,f.U0,f.U1,.02))FBox(f,(run[0]+run[1])/2,ground-.28,.08,run[1]-run[0],.24,.16,With(P(NativeBlockLooks.ConcreteBoard,brick?0x6E665C:0x8C8478,RoleFacade),detail:true));
            // The ground floor by kind.
            foreach(var run in Runs(f,.2,ground-.6,f.U0+.3,f.U1-.3))
            {
                switch(kind)
                {
                    case Shops:Storefronts(f,run[0],run[1],ground);break;
                    case Office:case Hotel:Lobby(f,run[0],run[1],ground,kind,name);break;
                    case Depot:RollerDoors(f,run[0],run[1],name);break;
                    case Kiosk:KioskFront(f,run[0],run[1],name);break;
                    default:Dwelling(f,run[0],run[1],brick);break;
                }
            }
            // Fire escapes on alley and side faces of the tall residential blocks (all above 3 m).
            if((kind==Residential||kind==Civic)&&storeys>=2&&f.Front>=1&&Hash(id,f.Index,77)<(f.Front==1?.85:.4))FireEscape(f,ground,storey,storeys,ww,bay);
            // The hotel's blade sign over the forecourt (its north face).
            if(kind==Hotel&&f.Front>=1&&f.Axis==2&&f.Sign>0)HotelSign(f,ground,h);
        }
        private void Window(Face f,double u,double y0,double w,double h,bool brick,int row,int col,bool burning)
        {
            string id=blocks[f.Block].id;double s=Hash(id,f.Index*97+row,col);double y1=y0+h,yc=(y0+y1)/2;Use(f,u-w/2-.15,u+w/2+.15,y0-.15,y1+.25);
            Begin();
            if(row<2)FBox(f,u,y0-.05,.07,w+.22,.09,.14,With(P(NativeBlockLooks.ConcreteBoard,SillHex,RoleFacade),detail:row>0));
            if(brick&&row==0)FBox(f,u,y1+.1,.04,w+.24,.2,.08,With(P(NativeBlockLooks.ConcreteBoard,LintelHex,RoleFacade),detail:true));
            // Burning: one window of the top storey on three blocks, by hash.
            if(burning&&Hash(id,f.Index,row*131+col)<.22)
            {
                FBox(f,u,yc,.02,w,h,.04,Glow(FireHex,1.7f,RoleFacade,DriveFire,(float)s));Soot(f,u,y1,w,1.3);
                At(f,u,0,.35,out double gx,out double gz);Light(gx,f.Base+y0+.05,gz,FireHex,1.4,8,w*.9,GlowWindowFire,s);End();return;
            }
            if(s<.07){FBox(f,u,yc,.02,w,h,.04,Glow(Pick(new[]{WarmHex,0xFFC890,0xFF9A50},s*97),.55f+.4f*(float)(s*10),RoleFacade));FBox(f,u,y1-h*.18,.035,w,h*.36,.02,With(P(NativeBlockLooks.Plain,0x3A2A1E,RoleFacade),detail:true));}
            else if(s<.1)FBox(f,u,yc,.02,w,h,.04,Glow(TvHex,.4f,RoleFacade,DriveTv,(float)s*13));
            else if(s<.25){FBox(f,u,yc,.04,w+.12,h+.1,.06,P(NativeBlockLooks.Timber,BoardHex,RoleFacade));FBox(f,u,yc+h*.1,.08,w+.3,.16,.04,With(P(NativeBlockLooks.Timber,0x7A5A38,RoleFacade),detail:true),roll:(s-.175)*300);}
            else if(s<.42)
            {
                FBox(f,u,yc,.02,w,h,.04,P(NativeBlockLooks.Plain,BlackHex,RoleFacade));
                FBox(f,u+(s<.33?-1:1)*w*.38,y1-h*.2,.035,w*.16,h*.36,.02,With(P(NativeBlockLooks.Plain,0x3A4652,RoleFacade),detail:true),roll:(s<.33?-1:1)*14);
                if(s<.3)Soot(f,u,y1,w,.9);
            }
            else
            {
                // Dark glass catching the dusk sky (a faint violet sheen, stronger on the upper floors), a mullion on a third.
                FBox(f,u,yc,.02,w,h,.04,Glow(s<.7?GlassSkyHex:GlassDuskHex,(float)(.1+.05*Math.Min(3,row)+.06*Hash(id,row,col+500)),RoleFacade));
                if(s>.86)FBox(f,u,yc,.045,.06,h,.02,With(P(NativeBlockLooks.Plain,FrameHex,RoleFacade),detail:true));
            }
            End();
        }
        private void Soot(Face f,double u,double y,double w,double reach)=>FBox(f,u,y+reach*.4,.012,w*.9,reach*.8,.02,With(P(NativeBlockLooks.Plain,SootHex,RoleFacade),detail:true));
        private static int Pick(int[] list,double h)=>list[Math.Min(list.Length-1,(int)(Math.Abs(h)%1*list.Length))];
        private static string PickName(string[] list,double h)=>list[Math.Min(list.Length-1,(int)(Math.Abs(h)%1*list.Length))];

        // Shops: storefronts about 5.5 m wide: pilasters, a stall riser, glass (dark, broken, boarded or a roller shutter down), a
        // door, the fascia with the shop's name and, on some, a striped awning whose low edge stays above a Hulk.
        /// <summary>Where an awning meets the wall (m): its valance's lower edge stays above a Hulk's 3.06 m.</summary>
        private const double AwningTop=3.54;
        private void Storefronts(Face f,double a,double c,double ground)
        {
            string id=blocks[f.Block].id;int n=Math.Max(1,(int)Math.Round((c-a)/5.5));double w=(c-a)/n;
            for(int k=0;k<n;k++)
            {
                double u0=a+k*w,u1=u0+w,u=(u0+u1)/2;double s=Hash(id,f.Index*31+k,5);if(w<2.2)continue;Use(f,u0,u1,0,4.2);
                Begin();
                FBox(f,u0+.15,1.6,.08,.3,3.2,.16,P(NativeBlockLooks.ConcreteBoard,0x5A524A,RoleFacade));if(k==n-1)FBox(f,u1-.15,1.6,.08,.3,3.2,.16,P(NativeBlockLooks.ConcreteBoard,0x5A524A,RoleFacade));
                double gw=w-.5;int fascia=Pick(FasciaHexes,s*7);
                FBox(f,u,3.81,.09,w-.3,.5,.14,P(NativeBlockLooks.Plain,fascia,RoleSign));
                // The name: on street fronts (alley shops keep a bare fascia); neon on some (a letter dead on half of those), painted
                // on the rest (the low tier leaves painted letters out).
                string name=PickName(ShopNames,Hash(id,f.Index*31+k,9));double cell=Math.Min(.08,(w-.9)/(4*name.Length-1));
                bool lettered=f.Front==2&&Hash(id,f.Index*31+k,10)<.8,neon=lettered&&s<.45;int deadLetter=neon&&Hash(id,k,11)<.5?(int)(Hash(id,k,12)*name.Length):-1;
                if(neon)Text(f,name,u,3.81-2.5*cell,.18,cell,.04,Glow(Pick(NeonHexes,s*13),1.6f,RoleSign,Hash(id,k,13)<.3?DriveFlicker:DriveSteady,(float)s*7),0,deadLetter,Glow(0x3A3A40,.05f,RoleSign));
                else if(lettered)Text(f,name,u,3.81-2.5*cell,.17,cell,.02,With(P(NativeBlockLooks.Plain,Pick(PaintHexes,s*3),RoleSign),detail:true));
                if(neon){At(f,u,0,.5,out double lx,out double lz);Light(lx,f.Base+3.6,lz,Pick(NeonHexes,s*13),.5,5,w*.5,GlowNeon,s);}
                FBox(f,u,.22,.05,gw,.44,.1,P(NativeBlockLooks.Plain,0x2A2624,RoleFacade));
                double state=Hash(id,f.Index*31+k,21);
                if(state<.22)
                {
                    // The shutter down over glass and door, with graffiti on it.
                    FBox(f,u,1.7,.06,gw,3.0,.08,P(NativeBlockLooks.Corrugated,ShutterHex,RoleFacade));FBox(f,u,3.25,.12,gw+.1,.2,.16,P(NativeBlockLooks.Plain,0x3A3C40,RoleFacade));
                    if(Hash(id,k,22)<.3)Graffiti(f,u,1.2,.11,gw-.4,Hash(id,k,23));
                }
                else
                {
                    double dw=Math.Min(1.0,gw*.3),du=Hash(id,k,24)<.5?u0+.35+dw/2:u1-.35-dw/2;
                    FBox(f,du,1.2,.02,dw,2.4,.04,P(NativeBlockLooks.Plain,0x141210,RoleFacade));FBox(f,du,2.45,.05,dw+.12,.1,.1,P(NativeBlockLooks.Plain,FrameHex,RoleFacade));
                    double gu0=du<u?du+dw/2+.08:u0+.35,gu1=du<u?u1-.35:du-dw/2-.08,gu=(gu0+gu1)/2,gww=gu1-gu0;
                    if(gww>.5)
                    {
                        if(state<.5){FBox(f,gu,1.7,.02,gww,2.45,.04,P(NativeBlockLooks.Plain,BlackHex,RoleFacade));FBox(f,gu-gww*.35,2.6,.04,gww*.25,.5,.02,With(P(NativeBlockLooks.Plain,0x5A6A78,RoleFacade),detail:true),roll:15);FBox(f,gu+gww*.3,.75,.04,gww*.3,.4,.02,With(P(NativeBlockLooks.Plain,0x4A5866,RoleFacade),detail:true),roll:-20);GlassOnGround(f,gu,gww);}
                        else if(state<.68){FBox(f,gu,1.7,.05,gww+.1,2.5,.06,P(NativeBlockLooks.Timber,BoardHex,RoleFacade));if(Hash(id,k,25)<.25)Graffiti(f,gu,1.3,.09,gww-.3,Hash(id,k,26));}
                        else{FBox(f,gu,1.7,.02,gww,2.45,.04,P(NativeBlockLooks.Plain,GlassHex,RoleFacade));FBox(f,gu,1.7,.045,.07,2.45,.02,With(P(NativeBlockLooks.Plain,FrameHex,RoleFacade),detail:true));
                            if(Hash(id,k,27)<.5)Poster(f,gu+gww*.2,1.6,.05,Hash(id,k,28));}
                    }
                }
                // The awning: hung under the fascia, its valance's low edge above 3.1 m (above a Hulk), striped, torn on some.
                if(Hash(id,f.Index*31+k,30)<.42)
                {
                    var st=AwningStripes[(int)(Hash(id,k,31)*AwningStripes.Length)%AwningStripes.Length];double aw=w-.5,depth=1.05,pitch=12;int strips=Math.Max(3,(int)(aw/.8));double sw=aw/strips;bool torn=Hash(id,k,32)<.35;
                    for(int t=0;t<strips;t++){double su=u-aw/2+sw*(t+.5);double reach=torn&&t>=strips-2?depth*.55:depth;
                        FBox(f,su,AwningTop-Math.Sin(pitch*Math.PI/180)*reach/2,Math.Cos(pitch*Math.PI/180)*reach/2,sw,.04,reach,With(P(NativeBlockLooks.Plain,st[t%2],RoleHigh),shadow:true,fade:true),0,pitch);}
                    FBox(f,u,AwningTop-Math.Sin(pitch*Math.PI/180)*depth-.08,Math.Cos(pitch*Math.PI/180)*depth+.01,aw,.14,.03,With(P(NativeBlockLooks.Plain,st[0],RoleHigh),fade:true,detail:true));
                }
                End();
            }
        }
        // Residential ground floors: a front door with its frame and number lamp, windows (barred or boarded) between.
        private void Dwelling(Face f,double a,double c,bool brick)
        {
            string id=blocks[f.Block].id;double len=c-a;if(len<1.6)return;
            double du=f.Front>=1&&len>=3?a+.6+(len-1.2)*Hash(id,f.Index,41):double.NaN;
            if(!double.IsNaN(du))
            {
                Use(f,du-.8,du+.8,0,3.0);Begin();
                FBox(f,du,1.3,.02,1.2,2.6,.04,P(NativeBlockLooks.Timber,Hash(id,f.Index,42)<.5?0x3A2A1E:0x2A3A44,RoleFacade));FBox(f,du-.68,1.4,.07,.16,2.8,.14,P(NativeBlockLooks.ConcreteBoard,0x9A9284,RoleFacade));FBox(f,du+.68,1.4,.07,.16,2.8,.14,P(NativeBlockLooks.ConcreteBoard,0x9A9284,RoleFacade));
                FBox(f,du,2.88,.08,1.52,.2,.16,P(NativeBlockLooks.ConcreteBoard,0x9A9284,RoleFacade));
                if(Hash(id,f.Index,43)<.3)FBox(f,du+.3,1.2,.05,.2,.6,.03,With(P(NativeBlockLooks.Plain,0x8A1A14,RoleFacade),detail:true),roll:8);
                End();
            }
            int cols=(int)Math.Floor(len/2.4);for(int k=0;k<cols;k++)
            {
                double u=a+(len/cols)*(k+.5);if(!double.IsNaN(du)&&Math.Abs(u-du)<1.5)continue;double s=Hash(id,f.Index*13+k,44);Use(f,u-.65,u+.65,.85,2.4);
                Begin();
                FBox(f,u,.95,.06,1.2,.08,.12,P(NativeBlockLooks.ConcreteBoard,SillHex,RoleFacade));
                if(s<.4){FBox(f,u,1.65,.04,1.1,1.35,.06,P(NativeBlockLooks.Timber,BoardHex,RoleFacade));if(s<.06)Graffiti(f,u,1.4,.08,1.0,Hash(id,k,45));}
                else{FBox(f,u,1.65,.02,1.0,1.3,.04,P(NativeBlockLooks.Plain,s<.65?BlackHex:GlassHex,RoleFacade));for(int bar=0;bar<3;bar++)FBox(f,u-.3+bar*.3,1.65,.06,.035,1.3,.035,With(P(NativeBlockLooks.SteelGreen,0x2A2C2E,RoleFacade),detail:true));}
                End();
            }
        }
        // Office and hotel lobbies: tall glass in a frame, mullions, some panes smashed; the building's name above.
        private void Lobby(Face f,double a,double c,double ground,int kind,string name)
        {
            string id=blocks[f.Block].id;double len=c-a;if(len<2)return;int panes=Math.Max(1,(int)Math.Round(len/1.6));double pw=len/panes;Use(f,a,c,0,4.6);
            for(int k=0;k<panes;k++)
            {
                double u=a+pw*(k+.5),s=Hash(id,f.Index*17+k,51);Begin();
                FBox(f,u,2.0,.02,pw-.1,3.6,.04,P(NativeBlockLooks.Plain,s<.35?BlackHex:s<.45?0x1A2A20:GlassBlueHex,RoleFacade));
                FBox(f,u-pw/2,2.0,.06,.1,3.7,.12,P(NativeBlockLooks.SteelGreen,0x2C3034,RoleFacade));
                if(s<.35)FBox(f,u,3.3,.04,pw*.4,.6,.02,With(P(NativeBlockLooks.Plain,0x5A6A78,RoleFacade),detail:true),roll:22);
                End();
            }
            FBox(f,c,2.0,.06,.1,3.7,.12,P(NativeBlockLooks.SteelGreen,0x2C3034,RoleFacade));FBox(f,(a+c)/2,3.86,.06,len,.12,.12,P(NativeBlockLooks.SteelGreen,0x2C3034,RoleFacade));
            if(f.Front==2&&kind==Office)
            {
                string label=name=="ne-tower"?OfficeNames[0]:OfficeNames[1];double cell=Math.Min(.09,(len-1)/(4*label.Length-1));
                Text(f,label,(a+c)/2,4.1,.05,cell,.04,P(NativeBlockLooks.Plain,0xC8C0B0,RoleSign));
            }
        }
        // Depots: roller doors in steel frames, hazard-striped sills and the depot number.
        private void RollerDoors(Face f,double a,double c,string name)
        {
            string id=blocks[f.Block].id;double len=c-a;int n=(int)Math.Floor(len/5.2);if(n<1)return;double w=len/n;Use(f,a,c,0,4.8);
            for(int k=0;k<n;k++)
            {
                double u=a+w*(k+.5),s=Hash(id,f.Index*7+k,61);double up=s<.25?1.6:0;Begin();
                FBox(f,u,(3.8+up)/2,.06,3.6,3.8-up,.08,P(NativeBlockLooks.Corrugated,s<.5?0x6A6E5A:0x5E6670,RoleFacade));
                if(up>0)FBox(f,u,up/2,.02,3.6,up,.04,P(NativeBlockLooks.Plain,BlackHex,RoleFacade));
                FBox(f,u-1.9,2.0,.09,.2,4.0,.18,P(NativeBlockLooks.SteelGreen,0x3A4038,RoleFacade));FBox(f,u+1.9,2.0,.09,.2,4.0,.18,P(NativeBlockLooks.SteelGreen,0x3A4038,RoleFacade));FBox(f,u,4.05,.12,4.0,.3,.24,P(NativeBlockLooks.SteelGreen,0x3A4038,RoleFacade));
                if(up==0)FBox(f,u,.12,.11,3.6,.2,.04,With(P(NativeBlockLooks.Demolition,0,RoleFacade),detail:true));
                Text(f,k==0?(name=="e-depot"?"DEPOT 7":"BAY 3"):(k+1).ToString(System.Globalization.CultureInfo.InvariantCulture),u,4.45,.06,.1,.03,P(NativeBlockLooks.Plain,0xD8D0B8,RoleSign));
                End();
            }
        }
        private void KioskFront(Face f,double a,double c,string name)
        {
            string id=blocks[f.Block].id;double len=c-a;if(len<2)return;double u=(a+c)/2;Use(f,a,c,0,3.4);Begin();
            FBox(f,u,1.6,.06,len-.4,1.6,.08,P(NativeBlockLooks.Corrugated,ShutterHex,RoleFacade));FBox(f,u,.4,.06,len-.4,.8,.1,P(NativeBlockLooks.Plain,0x3A3430,RoleFacade));
            string label=f.Front==2?(name=="kiosk-e"?"NEWS":"TICKETS"):"";if(label.Length>0){FBox(f,u,3.0,.09,len-.4,.7,.14,P(NativeBlockLooks.Plain,0x2A3A44,RoleSign));Text(f,label,u,2.8,.17,.08,.03,P(NativeBlockLooks.Plain,0xE8D890,RoleSign));}
            if(Hash(id,f.Index,71)<.3)Graffiti(f,u,1.5,.11,len-.8,Hash(id,f.Index,72));
            End();
        }
        // A fire escape: a landing at each storey (1.2 m out), railings, flights between, the drop ladder ending 3.2 m up.
        private void FireEscape(Face f,double ground,double storey,int storeys,double ww,double bay)
        {
            string id=blocks[f.Block].id;double len=f.U1-f.U0;if(len<5)return;double u=f.U0+2+(len-4)*Hash(id,f.Index,81);
            if(!Open(f,u-1.4,u+1.4,ground,ground+storey*storeys,.05,false))return;Use(f,u-1.5,u+1.5,3.0,ground+storey*storeys+1.2);
            var steel=With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleHigh),shadow:true,fade:true);var rail=With(P(NativeBlockLooks.SteelGreen,0x262826,RoleHigh),fade:true,detail:true);
            Begin();
            for(int r=0;r<storeys;r++)
            {
                double y=ground+r*storey+.75;FBox(f,u,y,.62,2.6,.08,1.2,steel);
                FBox(f,u,y+.5,1.2,2.6,.05,.05,rail);FBox(f,u,y+.95,1.2,2.6,.05,.05,rail);FBox(f,u-1.28,y+.5,.62,.05,1.0,1.2,rail);FBox(f,u+1.28,y+.5,.62,.05,1.0,1.2,rail);
                FBox(f,u-1.25,y+.5,1.2,.04,1.0,.04,rail);FBox(f,u+1.25,y+.5,1.2,.04,1.0,.04,rail);
                if(r>0){double rise=storey,run=2.0,ang=Math.Atan2(rise,run)*180/Math.PI;FBox(f,u+.1,y-rise/2,.62,Math.Sqrt(rise*rise+run*run),.06,.8,steel,ang*(f.Sign*(f.Axis==0?1:-1)>0?1:-1));}
            }
            double ly=ground+.75;FBox(f,u+1.0,(ly+3.25)/2,.9,.05,ly-3.25,.05,rail);FBox(f,u+1.5,(ly+3.25)/2,.9,.05,ly-3.25,.05,rail);for(double yy=3.4;yy<ly;yy+=.5)FBox(f,u+1.25,yy,.9,.5,.03,.03,rail);
            End();
        }
        private void HotelSign(Face f,double ground,double h)
        {
            string id=blocks[f.Block].id;double u=f.U0+1.2;if(!Open(f,u-.4,u+.4,ground+.6,h-1.2,.12,false))u=f.U1-1.2;if(!Open(f,u-.4,u+.4,ground+.6,h-1.2,.12,false))return;
            Begin();double y0=ground+.8,len=h-ground-2.2;var box=With(P(NativeBlockLooks.Plain,0x2A1E22,RoleHigh),shadow:true,fade:true);
            FBox(f,u,y0+len/2,.62,.18,len,1.0,box);
            // HOTEL down the blade, on both its faces (letters stacked), neon red with a dead letter.
            string word="HOTEL";double cell=Math.Min(.14,len/(word.Length*6));
            for(int side=-1;side<=1;side+=2)for(int k=0;k<word.Length;k++)
            {
                double ly=y0+len-(k+1)*6*cell+cell*.5;At(f,u,0,.62,out double cx,out double cz);
                foreach(var g in GlyphBoxes(word[k]))
                {
                    double gy=ly+(5-g[1]-g[3]*.5)*cell,gd=(g[0]+g[2]*.5-1.5)*cell*-side;
                    // The blade sticks straight out of the wall: letters lie in its plane (along the normal), on its two faces.
                    double ox=f.Axis==0?f.Sign*gd:Tx(f)*.1*side,oz=f.Axis==2?f.Sign*gd:Tz(f)*.1*side;
                    double sx=f.Axis==0?g[2]*cell:.03,sz=f.Axis==2?g[2]*cell:.03;
                    Box(f.Block,cx+ox,f.Base+gy,cz+oz,sx,g[3]*cell,sz,k==3?Glow(0x3A2020,.05f,RoleSign):Glow(0xFF3040,1.8f,RoleSign,DriveFlicker,.3f));
                }
            }
            At(f,u,0,1.0,out double hx,out double hz);Light(hx,f.Base+y0+len*.6,hz,0xFF3040,.6,7,1.6,GlowNeon,.2);
            End();
        }

        // ---------------- signage helpers ----------------
        /// <summary>Short tags sprayed on shutters, boards and walls; the longer messages go where the story wants them
        /// (<see cref="Message"/>).</summary>
        private static readonly string[] Tags={"HELP","RUN","X","NO","SOS","DEAD","HIDE"};
        private static readonly int[] SprayHexes={0xC8302A,0xE8E4D8,0x3AB84A,0xE89A2A,0x2A2A2A};
        private void Graffiti(Face f,double u,double y,double d,double width,double h)
        {
            string tag=Tags[Math.Min(Tags.Length-1,(int)(h*Tags.Length))];Message(f,tag,u,y,d,width,h);
        }
        private void Message(Face f,string tag,double u,double y,double d,double width,double h)
        {
            double cell=Math.Min(.11,width/(4*tag.Length-1));if(cell<.035)return;
            Text(f,tag,u,y,d,cell,.012,With(P(NativeBlockLooks.Plain,SprayHexes[(int)(h*977)%SprayHexes.Length],RoleFacade),detail:true),(h-.5)*10);
        }
        private void Poster(Face f,double u,double y,double d,double h)
        {
            bool notice=h<.5;int bg=notice?0xE0D8B8:Pick(new[]{0xC8A050,0x6A8AA8,0xB86A50},h*7);
            FBox(f,u,y,d,.62,.86,.012,With(P(NativeBlockLooks.Plain,bg,RoleFacade),detail:true),(h-.5)*6);
            if(notice){FBox(f,u,y+.31,d+.008,.6,.16,.012,With(P(NativeBlockLooks.Plain,0xB82A20,RoleFacade),detail:true),(h-.5)*6);for(int l=0;l<4;l++)FBox(f,u-.04,y+.08-l*.12,d+.008,.46-(l%2)*.12,.04,.012,With(P(NativeBlockLooks.Plain,0x2A2622,RoleFacade),detail:true),(h-.5)*6);}
            else{FBox(f,u,y+.1,d+.008,.4,.36,.012,With(P(NativeBlockLooks.Plain,0x2A2A30,RoleFacade),detail:true),(h-.5)*6);FBox(f,u,y-.26,d+.008,.46,.1,.012,With(P(NativeBlockLooks.Plain,0xE8E0C8,RoleFacade),detail:true),(h-.5)*6);}
        }
        /// <summary>A quarantine notice on face f: a paper sheet, the red QUARANTINE head in block letters and text lines.</summary>
        private void Notice(Face f,double u,double y,double d,bool big)
        {
            double w=big?3.6:1.2,hgt=big?1.6:.9;var paper=With(P(NativeBlockLooks.Plain,0xE4DCC0,RoleFacade),detail:!big);
            FBox(f,u,y,d,w,hgt,.012,paper);FBox(f,u,y+hgt*.32,d+.008,w-.08,hgt*.26,.012,With(P(NativeBlockLooks.Plain,0xB8221A,RoleFacade),detail:!big));
            if(big){double cell=Math.Min(hgt*.026,(w-.2)/39);Text(f,"QUARANTINE",u,y+hgt*.32-2.5*cell,d+.016,cell,.008,P(NativeBlockLooks.Plain,0xF0E8D8,RoleFacade));}
            for(int l=0;l<(big?3:2);l++)FBox(f,u-w*.05,y-hgt*.05-l*hgt*.15,d+.008,w*(.8-(l%2)*.2),hgt*.05,.012,With(P(NativeBlockLooks.Plain,0x2A2622,RoleFacade),detail:true));
        }
        private void GlassOnGround(Face f,double u,double w)
        {
            string id=blocks[f.Block].id;int slab=SlabAt(f,u);if(slab<0)return;
            for(int k=0;k<5;k++){double gu=u+(Hash(id,(int)(u*10)+k,91)-.5)*w,gd=.3+Hash(id,(int)(u*10)+k,92)*.9;At(f,gu,0,gd,out double x,out double z);
                Box(slab,x,.006,z,.12+.1*Hash(id,k,93),.012,.1,With(P(NativeBlockLooks.Plain,0x8A9AA8,RoleGround),detail:true),AxisRot(0,1,0,Hash(id,k,94)*90));}
        }
        /// <summary>The slab or paving block under the point 0.5 m in front of face f at u (-1 none).</summary>
        private int SlabAt(Face f,double u){At(f,u,0,.5,out double x,out double z);return GroundAt(x,z);}
        private int GroundAt(double x,double z)
        {
            for(int i=0;i<blocks.Length;i++){var b=blocks[i];var s=Stem(b?.id);if(s==null||!(Starts(s,"slab-")||s=="sanctuary-paving"||s=="square-paving"))continue;
                if(Math.Abs(x-b.pos.x)<=b.size.x/2&&Math.Abs(z-b.pos.z)<=b.size.z/2&&Math.Abs(b.pos.y+b.size.y/2)<.01)return i;}
            return -1;
        }

        // ---------------- roofs ----------------
        private void Roof(int i,string name,int kind)
        {
            var b=blocks[i];string id=b.id;double top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;
            // The parapet: a low wall round the roof edge, where no taller neighbour stands against it.
            if(facesOf.TryGetValue(i,out var fs))foreach(var f in fs)foreach(var run in Runs(f,f.Top-f.Base-.4,f.Top-f.Base+.8,f.U0,f.U1,.02,false))
                FBox(f,(run[0]+run[1])/2,f.Top-f.Base+.36,-.13,run[1]-run[0],.72,.26,With(P(b.material=="brick"?NativeBlockLooks.BrickRed:NativeBlockLooks.ConcreteBoard,b.material=="brick"?0x6A3A2A:0x8A8478,RoleRoof),shadow:true));
            // Plant on the roof: AC units, vents, an antenna or a dish, a chimney on brick (never over another block).
            int n=1+(int)(Hash(id,0,101)*3);
            for(int k=0;k<n;k++)
            {
                double px=x0+1.2+(x1-x0-2.4)*Hash(id,k,102),pz=z0+1.2+(z1-z0-2.4)*Hash(id,k,103);if(x1-x0<3||z1-z0<3)break;
                if(Overlapping(px,top+1,pz,1,1,1,i))continue;double t=Hash(id,k,104);
                if(t<.4){Box(i,px,top+.42,pz,1.2,.84,.9,With(P(NativeBlockLooks.SteelGreen,0x8A8E88,RoleRoof),detail:true));Box(i,px,top+.86,pz,.7,.04,.7,With(P(NativeBlockLooks.Plain,0x2A2C2E,RoleRoof),detail:true));}
                else if(t<.6&&b.material=="brick")Box(i,px,top+.8,pz,.7,1.6,.7,With(P(NativeBlockLooks.BrickRed,0x7A3A28,RoleRoof),detail:true));
                else if(t<.8){Box(i,px,top+1.6,pz,.08,3.2,.08,With(P(NativeBlockLooks.SteelGreen,0x3A3C3E,RoleRoof),detail:true));Box(i,px,top+2.6,pz,.9,.05,.05,With(P(NativeBlockLooks.SteelGreen,0x3A3C3E,RoleRoof),detail:true));}
                else{Box(i,px,top+.5,pz,.12,1.0,.12,With(P(NativeBlockLooks.SteelGreen,0x5A5C5E,RoleRoof),detail:true));Box(i,px,top+1.05,pz,1.0,1.0,.06,With(P(NativeBlockLooks.Plain,0xB8B4AC,RoleRoof),detail:true),AxisRot(1,0,0,-35+Hash(id,k,105)*20));}
            }
            // A billboard on two roofs over the streets.
            if(name=="ne-shops"||name=="sw-tenement")Billboard(i,name);
        }
        private bool Overlapping(double x,double y,double z,double hx,double hy,double hz,int self)
        {
            for(int j=0;j<blocks.Length;j++){if(j==self)continue;var b=blocks[j];if(Stem(b?.id)==null)continue;
                if(Math.Abs(x-b.pos.x)<hx+b.size.x/2&&Math.Abs(y-b.pos.y)<hy+b.size.y/2&&Math.Abs(z-b.pos.z)<hz+b.size.z/2)return true;}
            return false;
        }
        private void Billboard(int i,string name)
        {
            var b=blocks[i];double top=b.pos.y+b.size.y/2;bool south=name=="ne-shops";double z=south?b.pos.z-b.size.z/2+1.0:b.pos.z+b.size.z/2-1.0,x=b.pos.x+(south?2:-2),sgn=south?-1:1;
            string line=south?"STAY HOME":"MERIDIAN";string line2=south?"STAY SAFE":"CURING TOMORROW";Begin();
            var leg=With(P(NativeBlockLooks.SteelGreen,0x2A2C2E,RoleRoof),shadow:true);Box(i,x-2.6,top+1.2,z,.14,2.4,.14,leg);Box(i,x+2.6,top+1.2,z,.14,2.4,.14,leg);
            Box(i,x,top+3.8,z,7.0,3.0,.16,With(P(NativeBlockLooks.Plain,south?0x2A5A7A:0xE8EEF0,RoleRoof),shadow:true));Box(i,x,top+2.25,z+sgn*.3,7.0,.08,.6,With(P(NativeBlockLooks.SteelGreen,0x2A2C2E,RoleRoof),detail:true));
            // Its face toward the street: big letters, a tear, two lamps (one dead).
            int c1=south?0xF0E8D0:0x2E8A8C,c2=south?0xF0C040:0x2A3A44;double cell=.11;
            Letters(i,line,x,top+4.25,z+sgn*.1,cell,c1,sgn);Letters(i,line2,x,top+3.35,z+sgn*.1,south?.11:.075,c2,sgn);
            Box(i,x+1.8,top+3.0,z+sgn*.09,1.3,1.1,.02,With(P(NativeBlockLooks.Plain,0x3A3634,RoleRoof),detail:true),AxisRot(0,0,1,12));
            Box(i,x-1.8,top+2.35,z+sgn*.55,.3,.12,.2,Glow(0xFFE0B0,1.2f,RoleRoof));Box(i,x+1.8,top+2.35,z+sgn*.55,.3,.12,.2,Glow(0x3A3630,.05f,RoleRoof));
            Light(x-1.8,top+2.6,z+sgn*.9,0xFFE0B0,.5,6,1.2,GlowFlood);
            End();
        }
        // Letters on an axis-aligned face facing ±z (sgn), centred at x.
        private void Letters(int parent,string text,double x,double y,double z,double cell,int hex,double sgn)
        {
            double width=TextWidth(text,cell),x0=x+sgn*width/2;
            for(int k=0;k<text.Length;k++)foreach(var g in GlyphBoxes(text[k])){double cx=x0-sgn*(k*4*cell+(g[0]+g[2]*.5)*cell);Box(parent,cx,y+(5-g[1]-g[3]*.5)*cell,z+sgn*.012,g[2]*cell,g[3]*cell,.02,P(NativeBlockLooks.Plain,hex,RoleRoof));}
        }
        private void RoofProp(int i,string s)
        {
            var b=blocks[i];double top=b.pos.y+b.size.y/2,bot=b.pos.y-b.size.y/2;
            if(s.EndsWith("-tank",StringComparison.Ordinal))
            {
                // A water tank: hoops, a stepped lid, legs under the overhang.
                for(int k=0;k<3;k++)Box(i,b.pos.x,bot+.6+k*.8,b.pos.z,b.size.x+.08,.08,b.size.z+.08,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleRoof),detail:true));
                Box(i,b.pos.x,top+.2,b.pos.z,b.size.x*.7,.4,b.size.z*.7,P(NativeBlockLooks.Timber,0x5A4A38,RoleRoof));Box(i,b.pos.x,top+.5,b.pos.z,b.size.x*.35,.2,b.size.z*.35,P(NativeBlockLooks.Timber,0x5A4A38,RoleRoof));
            }
            else if(s.EndsWith("-stair",StringComparison.Ordinal))
            {
                Box(i,b.pos.x,top+.06,b.pos.z,b.size.x+.2,.12,b.size.z+.2,P(NativeBlockLooks.ConcreteBoard,0x6A645C,RoleRoof));
                Box(i,b.pos.x-b.size.x/2-.02,bot+1.05,b.pos.z,.04,2.1,.9,P(NativeBlockLooks.SteelGreen,0x3A4A3A,RoleRoof));
            }
            else
            {
                Box(i,b.pos.x,top+.06,b.pos.z,b.size.x*.8,.12,b.size.z*.8,With(P(NativeCyberCityLooks.Grate,0x5A6070,RoleRoof,NativeCyberCityLooks.Bank),detail:true));
                Box(i,b.pos.x+b.size.x*.3,top+.6,b.pos.z,.3,1.2,.3,With(P(NativeBlockLooks.SteelGreen,0x6A6E70,RoleRoof),detail:true));
            }
        }
    }
}
