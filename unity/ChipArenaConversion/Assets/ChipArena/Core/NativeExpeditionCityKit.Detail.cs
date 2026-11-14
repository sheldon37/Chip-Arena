using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The city detail pass (Claude, 2026-10-20; docs/EXPEDITION-HUB.md "The city in detail"), laid over the dressing above in the
    // same blocky language and under the same rules (NativeExpeditionCityKit.cs: the wall band below 3.1 m, flat paint at most
    // 6 cm, every keep-out, every part stable per block id):
    //  - windows: painted frames, shutters (some hanging off a hinge), balcony stacks with railings and their clutter, air
    //    conditioners with their drips;
    //  - walls: drainpipes with hoppers and shoes, grime and damp at the foot (the wall's own brick or board, darker), rain
    //    stains under the sills, moss and weeds at the foot, street clutter (meter boxes, cabinets, pay phones, bins, benches,
    //    poster walls, rubble);
    //  - street: pavements of slabs with kerbs, gutters and drains along the streets, manholes (some steaming), cracks with weeds,
    //    potholes, puddles that catch the dusk, oil, more litter and paper out in the quarters;
    //  - overhead: cables strung across the streets and alleys, lamps hung from some, washing on the alley lines;
    //  - roofs: water tanks on legs, chimney stacks, aerials, stair huts.
    // Fine pieces are `Detail` (the low tier leaves them out) and are drawn by size (NativeExpeditionCityKit.Range); everything
    // on a building face carries its back-face record, so the runtime leaves it out whenever that building hides it.
    public sealed partial class NativeExpeditionCityKit
    {
        public const byte RoleStreet=14,RoleWeather=15,RoleOverhead=16;
        private struct WindowRec {public Face F;public double U,Y0,W,H,S,Lift;public int Row,Col,Used;}
        private readonly List<WindowRec> windowRecs=new List<WindowRec>();
        /// <summary>Steam sources (world x, y, z, phase): the manholes and grates that breathe (NativeExpeditionCityFx).</summary>
        public float[] Steam=Array.Empty<float>();private readonly List<float> steam=new List<float>();
        private static readonly int[] FrameHexes={0xC8BCA0,0x2E3A30,0x4A2A24,0x6A6A64,0xD0CCC0,0x1E1E20,0x3A4A5A};
        private static readonly int[] ShutterHexes={0x3A5A44,0x3A4A6A,0x5A3A26,0x6A6A60,0x6A2A22,0x4A5A5A};
        private static readonly int[] ClothHexes={0xC8C0B0,0x8A3A34,0x3A5A8A,0xD8C870,0x5A7A4A,0xE0D8D0,0x7A5A8A};
        /// <summary>Whether face f is in the rect free of occluders, keeps and every other dressing (the rect `skip` of its Used list,
        /// the window's own, left out).</summary>
        private bool OpenSkip(Face f,double u0,double u1,double y0,double y1,double pad,int skip)
        {
            if(!Open(f,u0,u1,y0,y1,pad,false))return false;
            for(int i=0;i<f.Used.Count;i++){if(i==skip)continue;var r=f.Used[i];if(u0<r[1]&&u1>r[0]&&y0<r[3]&&y1>r[2])return false;}
            return true;
        }
        private int KindOfFace(Face f){var s=Stem(blocks[f.Block].id);return s!=null&&s.Length>5?KindOf(s.Substring(5)):Residential;}
        /// <summary>The town's own wall colour behind a part of face f (MapLooks blockLooks by material: brick-red 7A4A3E, stone
        /// concrete-board 8A8478), darkened by `k`, in the wall's own look so its courses run on.</summary>
        private Pen WallTone(Face f,double k,byte role)
        {
            var b=blocks[f.Block];bool brick=b.material=="brick";int hex=brick?0x7A4A3E:0x8A8478;
            int r=(int)(((hex>>16)&255)*k),g=(int)(((hex>>8)&255)*k),bl=(int)((hex&255)*k);
            return With(P(brick?NativeBlockLooks.BrickRed:NativeBlockLooks.ConcreteBoard,(r<<16)|(g<<8)|bl,role),detail:true);
        }

        private void Detail()
        {
            foreach(var w in windowRecs)WindowDetail(w);
            foreach(var f in faces)WallDetail(f);
            Pavements();RoadDetail();Overhead();RoofDetail();
            Steam=steam.ToArray();
        }

        // ---------------- windows ----------------
        private void WindowDetail(WindowRec w)
        {
            var f=w.F;int kind=KindOfFace(f);if(kind>=Meridian&&kind<Exchange)return;string id=blocks[f.Block].id;double yc=w.Y0+w.H/2,s=w.S;bool boarded=s>=.1&&s<.25;
            // The frame: a painted surround a little proud of the wall, behind the pane (the pane stands 2 cm further out).
            int frameHex=Pick(FrameHexes,Hash(id,0,501));
            if(!boarded){Begin();FBox(f,w.U,yc,.008+w.Lift,w.W+.14,w.H+.14,.016,With(P(NativeBlockLooks.Plain,frameHex,RoleFacade),detail:true));
                if(s>.42&&s<.6)FBox(f,w.U,w.Y0+w.H*.66,.045+w.Lift,w.W,.05,.012,With(P(NativeBlockLooks.Plain,frameHex,RoleFacade),detail:true));End();}
            if(w.Row<0||w.Y0<3.4)return;bool homes=kind==Residential||kind==Civic||kind==Hotel||kind==Cinema||kind==Shops;
            // Balconies: some window columns on the homes carry a stack of them (above a Hulk, over the street or the alley).
            if(homes&&f.Front>=1&&Hash(id,f.Index*7,w.Col+900)<.2&&w.Row>=0)
            {
                double bw=w.W+.9,y=w.Y0-.18;
                if(OpenSkip(f,w.U-bw/2-.05,w.U+bw/2+.05,y-.15,y+1.1,.02,w.Used))
                {
                    // A concrete balcony: its slab, a solid front wall and two cheeks, a steel rail on top.
                    Begin();var slab=With(P(NativeBlockLooks.ConcreteBoard,0x9A9488,RoleHigh),fade:true,detail:true);
                    FBox(f,w.U,y,.4,bw,.12,.8,slab);FBox(f,w.U,y+.42,.76,bw,.72,.08,slab);FBox(f,w.U-bw/2+.04,y+.42,.4,.08,.72,.68,slab);FBox(f,w.U+bw/2-.04,y+.42,.4,.08,.72,.68,slab);
                    FBox(f,w.U,y+.82,.76,bw+.04,.05,.1,With(P(NativeBlockLooks.SteelGreen,0x2A2C2A,RoleHigh),fade:true,detail:true));
                    double t=Hash(id,w.Row,w.Col+910);
                    if(t<.3){FBox(f,w.U-bw*.25,y+.3,.45,.4,.45,.4,With(P(NativeBlockLooks.Plain,0x4A6A3A,RoleHigh),detail:true));}
                    else if(t<.55){for(int k=0;k<3;k++)FBox(f,w.U-bw*.3+k*.4,y+.75,.6,.3,.5,.02,With(P(NativeBlockLooks.Plain,Pick(ClothHexes,Hash(id,k,911+w.Row)),RoleHigh),detail:true,fade:true));}
                    else if(t<.7){FBox(f,w.U+bw*.2,y+.28,.35,.4,.05,.4,With(P(NativeBlockLooks.Timber,0x5A4632,RoleHigh),detail:true));FBox(f,w.U+bw*.2,y+.55,.17,.4,.5,.04,With(P(NativeBlockLooks.Timber,0x5A4632,RoleHigh),detail:true));}
                    if(End()){Use(f,w.U-bw/2,w.U+bw/2,y-.1,y+1.0);return;}
                }
            }
            // Shutters on the homes' upper windows, a leaf missing on some, one hanging off its top hinge.
            if((kind==Residential||kind==Civic)&&!boarded&&Hash(id,w.Row*31+w.Col,920)<.32)
            {
                int hex=Pick(ShutterHexes,Hash(id,0,921));double lw=Math.Min(.55,w.W*.55);
                for(int side=-1;side<=1;side+=2)
                {
                    double t=Hash(id,w.Row*31+w.Col,922+side);if(t<.18)continue;double lu=w.U+side*(w.W/2+.18+lw/2),roll=t>.9?side*-14:0;
                    if(!OpenSkip(f,lu-lw/2,lu+lw/2,w.Y0-.05,w.Y0+w.H+.05,.01,w.Used))continue;
                    Begin();FBox(f,lu,yc-(roll!=0?.08:0),.025,lw,w.H+.04,.03,With(P(NativeBlockLooks.Timber,hex,RoleFacade),detail:true),roll);End();Use(f,lu-lw/2,lu+lw/2,w.Y0,w.Y0+w.H);
                }
                return;
            }
            // Air conditioners under some office and home windows, a rust drip down the wall from each.
            if((kind==Office||kind==Residential||kind==Hotel)&&w.Y0>=3.95&&Hash(id,w.Row*17+w.Col,930)<.1)
            {
                double y=w.Y0-.5;if(!OpenSkip(f,w.U-.45,w.U+.45,y-1.6,y+.3,.02,w.Used))return;
                Begin();FBox(f,w.U,y,.28,.78,.5,.55,With(P(NativeBlockLooks.SteelGreen,0xA8A8A0,RoleHigh),fade:true,detail:true));FBox(f,w.U,y,.56,.6,.36,.012,With(P(NativeCyberCityLooks.Grate,0x5A5C5A,RoleHigh,NativeCyberCityLooks.Bank),detail:true));
                FBox(f,w.U,y-.27,.15,.7,.04,.3,With(P(NativeBlockLooks.SteelGreen,0x3A3C3A,RoleHigh),detail:true));FBox(f,w.U+.2,y-1.0,.006,.07,1.4,.012,With(P(NativeBlockLooks.Plain,RustHex,RoleWeather),detail:true));
                if(End())Use(f,w.U-.45,w.U+.45,y-1.6,y+.3);return;
            }
            // Rain stains running down from some sills.
            if(Hash(id,w.Row*13+w.Col,940)<.16){double ln=1.2+1.6*Hash(id,w.Row,941+w.Col);if(OpenSkip(f,w.U-w.W*.3,w.U+w.W*.3,w.Y0-ln,w.Y0-.1,.01,w.Used)){Begin();FBox(f,w.U+(Hash(id,w.Row,942)-.5)*w.W*.3,w.Y0-ln/2-.1,.004,w.W*.45,ln,.008,WallTone(f,.7,RoleWeather));End();}}
        }

        // ---------------- walls ----------------
        private void WallDetail(Face f)
        {
            if(f.Front==0)return;int kind=KindOfFace(f);if(kind>=Meridian&&kind<Exchange)return;string id=blocks[f.Block].id;double h=f.Top-f.Base,len=f.U1-f.U0;bool wild=WildFace(f,(f.U0+f.U1)/2);
            var iron=With(P(NativeBlockLooks.SteelGreen,Hash(id,f.Index,950)<.3?0x5A3420:0x262826,RoleFacade),detail:true);
            // Drainpipes down from the roof near the face's ends: a hopper under the parapet, the pipe, brackets, a shoe.
            if(h>=5)foreach(double end in new[]{0.0,1.0})
            {
                if(len<6&&end>0)break;double u=end<.5?f.U0+.32:f.U1-.32;if(Hash(id,f.Index,951+(int)end)<.25)continue;
                if(!Open(f,u-.16,u+.16,.05,h-.45,.02))continue;
                Begin();FBox(f,u,(h-.5)/2+.05,.08,.11,h-.6,.11,iron);FBox(f,u,h-.55,.12,.3,.3,.22,iron);FBox(f,u,.12,.11,.22,.2,.2,iron);
                for(double y=2.4;y<h-1;y+=2.6)FBox(f,u,y,.04,.18,.06,.06,iron);
                if(End())Use(f,u-.16,u+.16,0,h);
            }
            // The foot of the wall: grime and damp (the wall's own courses, darker) in a ragged band, moss and weeds in it.
            foreach(var run in Runs(f,.0,1.3,f.U0+.05,f.U1-.05,.05))
            {
                for(double u=run[0];u<run[1]-.3;)
                {
                    double wdt=Math.Min(run[1]-u,.8+2.2*Hash(id,(int)(u*10),960)),hgt=.25+.85*Hash(id,(int)(u*10),961);
                    if(Open(f,u,u+wdt,0,hgt,.0))
                    {
                        Begin();FBox(f,u+wdt/2,hgt/2,.004,wdt,hgt,.008,WallTone(f,wild?.5:.6,RoleWeather));End();
                        if(Hash(id,(int)(u*10),962)<(wild||f.Front==1?.7:.3))for(int k=0;k<2+(int)(Hash(id,(int)(u*10),963)*3);k++)
                        {
                            double wu=u+wdt*Hash(id,(int)(u*10)+k,964),wh=.05+.18*Hash(id,(int)(u*10)+k,965),ww=.1+.2*Hash(id,(int)(u*10)+k,966);
                            if(wu-ww/2<run[0]||wu+ww/2>run[1])continue;Begin();FBox(f,wu,wh/2,.06,ww,wh,.1+.08*Hash(id,k,967),With(P(NativeBlockLooks.Plain,Pick(new[]{0x3A4A26,0x4A5A2A,0x2E3A1E,0x5A5A30},Hash(id,(int)(u*10)+k,968)),RoleWeather),detail:true),(Hash(id,k,969)-.5)*20);End();
                        }
                    }
                    u+=wdt*(.6+.5*Hash(id,(int)(u*10),970));
                }
            }
            // Street clutter at the wall: one to three pieces in clear stretches.
            int n=f.Front==2?1+(int)(Hash(id,f.Index,980)*2.99):(int)(Hash(id,f.Index,980)*2.5);
            for(int k=0;k<n;k++)
            {
                double u=f.U0+1+(len-2)*Hash(id,f.Index*11+k,981),t=Hash(id,f.Index*11+k,982);
                if(t<.18){if(!Open(f,u-.4,u+.4,.3,1.6,.05))continue;Begin();FBox(f,u,.95,.1,.6,1.1,.2,With(P(NativeBlockLooks.SteelGreen,0x7A7E78,RoleFacade),detail:true));FBox(f,u,.95,.205,.02,1.0,.012,With(P(NativeBlockLooks.Plain,0x2A2C2A,RoleFacade),detail:true));
                    FBox(f,u,1.35,.205,.3,.08,.012,With(P(NativeBlockLooks.Plain,0xD8C830,RoleFacade),detail:true));if(End())Use(f,u-.4,u+.4,.3,1.6);}
                else if(t<.32){if(!Open(f,u-.3,u+.3,.9,1.6,.05))continue;Begin();FBox(f,u,1.25,.1,.42,.5,.2,With(P(NativeBlockLooks.SteelGreen,0x4A4E46,RoleFacade),detail:true));FBox(f,u,1.25,.205,.2,.12,.012,With(P(NativeBlockLooks.Plain,0x141414,RoleFacade),detail:true));if(End())Use(f,u-.3,u+.3,.9,1.6);}
                else if(t<.46&&f.Front==2){if(!Open(f,u-.45,u+.45,.8,2.4,.05))continue;Begin();var steel=With(P(NativeBlockLooks.SteelGreen,0x2A3A44,RoleFacade),detail:true);
                    FBox(f,u,2.25,.12,.6,.08,.24,steel);FBox(f,u-.28,1.85,.12,.04,.8,.24,steel);FBox(f,u+.28,1.85,.12,.04,.8,.24,steel);FBox(f,u,1.7,.1,.32,.5,.2,With(P(NativeBlockLooks.SteelGreen,0x5A5C5A,RoleFacade),detail:true));
                    FBox(f,u-.08,1.55,.205,.06,.24,.04,With(P(NativeBlockLooks.Plain,0x141414,RoleFacade),detail:true),Hash(id,k,983)<.4?60:0);FBox(f,u,2.4,.24,.5,.06,.012,With(P(NativeBlockLooks.Plain,0xD8D0C0,RoleSign),detail:true));if(End())Use(f,u-.45,u+.45,.8,2.4);}
                else if(t<.6){if(!Open(f,u-.3,u+.3,.3,1.2,.05))continue;Begin();FBox(f,u,.78,.12,.42,.55,.22,With(P(NativeBlockLooks.SteelGreen,0x2E3A30,RoleFacade),detail:true));FBox(f,u,1.08,.12,.46,.06,.24,With(P(NativeBlockLooks.SteelGreen,0x262826,RoleFacade),detail:true));
                    FBox(f,u+.05,1.13,.13,.3,.12,.18,With(P(NativeBlockLooks.Plain,0x141614,RoleFacade),detail:true));if(End())Use(f,u-.3,u+.3,.3,1.2);}
                else if(t<.72&&f.Front==2){if(!Open(f,u-.95,u+.95,0,1.0,.05))continue;Begin();var wood=With(P(NativeBlockLooks.Timber,0x5A4632,RoleFacade),detail:true);var leg=With(P(NativeBlockLooks.SteelGreen,0x262826,RoleFacade),detail:true);
                    FBox(f,u,.45,.13,1.7,.06,.24,wood);FBox(f,u,.78,.03,1.7,.3,.04,wood);FBox(f,u-.7,.22,.12,.06,.44,.2,leg);FBox(f,u+.7,.22,.12,.06,.44,.2,leg);if(End())Use(f,u-.95,u+.95,0,1.0);}
                else if(t<.86){if(!Open(f,u-.9,u+.9,.4,2.4,.05))continue;Begin();for(int p=0;p<5;p++){double pu=u-.6+1.2*Hash(id,k*7+p,984),py=.9+1.1*Hash(id,k*7+p,985);Poster(f,pu,py,.012+.005*p,Hash(id,k*7+p,986));}if(End())Use(f,u-.9,u+.9,.4,2.4);}
                else{if(!Open(f,u-.8,u+.8,0,.6,.05))continue;Begin();for(int p=0;p<6;p++){double pu=u-.6+1.2*Hash(id,k*7+p,987),sz=.15+.2*Hash(id,k*7+p,988);
                    FBox(f,pu,sz*.4,.05+.05*Hash(id,p,989),sz*1.2,sz*.8,sz*.5,With(P(p%3==0?NativeBlockLooks.BrickRed:NativeBlockLooks.ConcreteBoard,p%3==0?0x6A3A2E:0x6A6460,RoleStreet),detail:true),(Hash(id,p,990)-.5)*30,(Hash(id,p,991)-.5)*20);}if(End())Use(f,u-.8,u+.8,0,.6);}
            }
        }

        // ---------------- the street ----------------
        /// <summary>Pavements along the streets: slabs from the wall to a kerb about 1.6 m out, the gutter and a drain now and then
        /// beyond it; in pieces along each street face that stop short of every keep-out (spawn exits, doors, slots). The slabs lie
        /// under every other piece of flat paint (2.5 mm), the kerb stands 5 cm.</summary>
        private void Pavements()
        {
            foreach(var f in faces)
            {
                if(f.Front!=2||f.Top-f.Base<4)continue;int kind=KindOfFace(f);if(kind>=Meridian&&kind<Exchange)continue;string id=blocks[f.Block].id;
                double wid=1.6;const double cell=.5;int nc=(int)Math.Floor((f.U1-f.U0)/cell);if(nc<3)continue;var ok=new bool[nc];
                for(int c=0;c<nc;c++)
                {
                    double u=f.U0+(c+.5)*cell;At(f,u,0,.4,out double x0,out double z0);At(f,u,0,wid+.4,out double x1,out double z1);int g0=GroundAt(x0,z0),g1=GroundAt(x1,z1);
                    At(f,u,0,(wid+.5)/2,out double xm,out double zm);double hu=cell/2,hd=(wid+.5)/2;At(f,u,0,2*wid+1.6,out double x2,out double z2);
                    // A street, not an alley: the far side's own pavement and the carriageway fit before the next wall.
                    ok[c]=g0>=0&&g1>=0&&Starts(Stem(blocks[g0].id),"slab-")&&Starts(Stem(blocks[g1].id),"slab-")&&!Wild(x1,z1)&&!InBlock(x1,z1,.05)&&!InBlock(x2,z2,.05)&&!Blocked(xm,.03,zm,f.Axis==0?hd:hu,.03,f.Axis==0?hu:hd,Rot.None);
                }
                for(int c=0;c<nc;)
                {
                    if(!ok[c]){c++;continue;}int e=c;while(e<nc&&ok[e]&&e-c<20)e++;
                    double ua=f.U0+c*cell,ub=f.U0+e*cell,um=(ua+ub)/2,ln=ub-ua;At(f,um,0,wid/2,out double px,out double pz);int g=GroundAt(px,pz);
                    if(g>=0&&ln>=1.0)
                    {
                        At(f,um,0,0,out _,out _);double yaw=f.Axis==0?0:90;
                        // The pavement, kerb, gutter: each its own box on the street slab (a box the keep-outs refuse is left out).
                        var pave=With(P(NativeBlockLooks.Terracotta,0x5E5C56,RoleStreet),detail:true);var kerb=With(P(NativeBlockLooks.ConcreteBoard,0x8C887E,RoleStreet),detail:true);var gutter=With(P(NativeBlockLooks.Plain,0x1A1818,RoleStreet),detail:true);
                        GroundBox(f,g,um,wid/2,ln,wid,.0025,pave);GroundBox(f,g,um,wid+.09,ln,.18,.05,kerb);GroundBox(f,g,um,wid+.33,ln,.3,.006,gutter);
                        for(double u=ua+1.5;u<ub-1;u+=11){if(Hash(id,(int)(u*10),1001)<.5)continue;GroundBox(f,g,u,wid+.33,.55,.32,.012,With(P(NativeCyberCityLooks.Grate,0x3A3C3C,RoleStreet,NativeCyberCityLooks.Bank),detail:true));}
                    }
                    c=e;
                }
            }
        }
        /// <summary>A flat box on ground block g in front of face f: centre u along the face, d out of it, `along` x `across` m,
        /// `height` tall from the slab's top (refused by any keep-out like every other part).</summary>
        private void GroundBox(Face f,int g,double u,double d,double along,double across,double height,Pen pen,double turn=0)
        {
            At(f,u,0,d,out double x,out double z);double sx=f.Axis==0?across:along,sz=f.Axis==0?along:across;Box(g,x,height/2,z,sx,height,sz,pen,turn!=0?AxisRot(0,1,0,turn):Rot.None);
        }
        /// <summary>The road surface: manholes (round, a few breathing steam), cracks with weeds, potholes with their spoil, puddles
        /// that catch the dusk sky, oil stains; litter and paper out in the quarters and the wilds' edge.</summary>
        private void RoadDetail()
        {
            var iron=With(P(NativeBlockLooks.SteelGreen,0x2A2826,RoleStreet),detail:true);int holes=0,cracks=0,puddles=0,oil=0,paper=0;
            for(int k=0;k<1400;k++)
            {
                double x=TownMinX+4+(TownMaxX-TownMinX-8)*Hash("road",k,1),z=TownMinZ+4+(TownMaxZ-TownMinZ-8)*Hash("road",k,2);if(Math.Abs(x)<14&&Math.Abs(z)<14)continue;
                int g=GroundAt(x,z);if(g<0||!Starts(Stem(blocks[g].id),"slab-")||InBlock(x,z,.6))continue;double t=Hash("road",k,3);bool wild=Wild(x,z);if(wild&&t<.74)continue;
                if(t<.12&&holes<46)
                {
                    // A manhole: a round iron cover (four squares turned 22.5°), its rim.
                    Begin();for(int q=0;q<4;q++)Box(g,x,.006,z,.66,.012,.66,iron,AxisRot(0,1,0,q*22.5));Box(g,x,.014,z,.36,.004,.06,With(P(NativeBlockLooks.Plain,0x1A1816,RoleStreet),detail:true));
                    if(End()){holes++;if(Hash("road",k,4)<.55){steam.Add((float)x);steam.Add(.05f);steam.Add((float)z);steam.Add((float)Hash("road",k,5));}}
                }
                else if(t<.4&&cracks<90)
                {
                    // A crack: a dark zigzag, weeds in its joints.
                    double a=Hash("road",k,6)*Math.PI*2,cx=x,cz=z;Begin();int segs=3+(int)(Hash("road",k,7)*4);
                    for(int s=0;s<segs;s++){double ln=.35+.6*Hash("road",k,8+s),nx=cx+Math.Sin(a)*ln,nz=cz+Math.Cos(a)*ln;Box(g,(cx+nx)/2,.004,(cz+nz)/2,.035+.03*Hash("road",k,20+s),.008,ln,With(P(NativeBlockLooks.Plain,0x121012,RoleStreet),detail:true),AxisRot(0,1,0,a*180/Math.PI));
                        if(Hash("road",k,30+s)<.35)Box(g,nx,.025,nz,.12,.05,.12,With(P(NativeBlockLooks.Plain,Pick(new[]{0x3A4A26,0x4A5A2A,0x5A5A30},Hash("road",k,40+s)),RoleWeather),detail:true),AxisRot(0,1,0,s*37));
                        cx=nx;cz=nz;a+=(Hash("road",k,50+s)-.5)*1.4;}
                    if(End())cracks++;
                }
                else if(t<.48&&holes<46)
                {
                    // A pothole: the dark hollow, chunks of asphalt spoil round it.
                    double sz=.5+.7*Hash("road",k,9);Begin();Box(g,x,.005,z,sz,.01,sz*.8,With(P(NativeBlockLooks.Plain,0x100E0E,RoleStreet),detail:true),AxisRot(0,1,0,Hash("road",k,10)*90));
                    for(int s=0;s<4;s++){double a=Hash("road",k,11+s)*Math.PI*2,r=sz*.6+.2*Hash("road",k,15+s);Box(g,x+Math.Sin(a)*r,.02,z+Math.Cos(a)*r,.12+.1*Hash("road",k,19+s),.04,.1+.08*Hash("road",k,23+s),With(P(NativeBlockLooks.Asphalt,0x3A3840,RoleStreet),detail:true),AxisRot(0,1,0,a*57));}
                    if(End())holes++;
                }
                else if(t<.66&&puddles<70&&!wild)
                {
                    // A puddle: wet dark asphalt and the dusk sky in it (a faint emissive sheen), two or three lobes.
                    Begin();int lobes=2+(int)(Hash("road",k,12)*2);
                    for(int s=0;s<lobes;s++){double ox=(Hash("road",k,60+s)-.5)*1.2,oz=(Hash("road",k,64+s)-.5)*1.2,sz=.8+1.2*Hash("road",k,68+s),yaw=Hash("road",k,72+s)*180;
                        Box(g,x+ox,.004,z+oz,sz+.25,.008,sz*.6+.2,With(P(NativeBlockLooks.Plain,0x161418,RoleStreet),detail:true),AxisRot(0,1,0,yaw));
                        Box(g,x+ox,.006,z+oz,sz,.012,sz*.6,With(Glow(Pick(new[]{0x5A4268,0x6A4A5A,0x4A3A60},Hash("road",k,76+s)),.22f,RoleStreet),detail:true),AxisRot(0,1,0,yaw));}
                    if(End())puddles++;
                }
                else if(t<.74&&oil<40){Begin();Box(g,x,.004,z,.6+.8*Hash("road",k,13),.008,.4+.5*Hash("road",k,14),With(P(NativeBlockLooks.Plain,0x0E0C0E,RoleStreet),detail:true),AxisRot(0,1,0,Hash("road",k,15)*180));if(End())oil++;}
                else if(paper<140&&(Math.Abs(x)>44||z<-44||wild))
                {
                    // Litter and newspaper out in the quarters (the inner streets have theirs, Litter).
                    bool news=Hash("road",k,16)<.4;Begin();Box(g,x,.003+.002*(k%3),z,news?.42:.2+.3*Hash("road",k,17),.006,news?.3:.25+.3*Hash("road",k,18),With(P(NativeBlockLooks.Plain,news?0xC8C4B4:Pick(new[]{PaperHex,0xA8A090,0x6A5A48,0x3A3430},Hash("road",k,19)),RoleGround),detail:true),AxisRot(0,1,0,Hash("road",k,20)*360));
                    if(news)Box(g,x,.008+.002*(k%3),z,.3,.004,.04,With(P(NativeBlockLooks.Plain,0x3A3632,RoleGround),detail:true),AxisRot(0,1,0,Hash("road",k,20)*360));
                    if(End())paper++;
                }
            }
        }

        // ---------------- overhead ----------------
        /// <summary>Cables strung across streets and alleys between facing walls (6-9 m up, sagging), a lamp hung from some over the
        /// streets, washing on the alley lines. Each cable is made once, from the wall with the smaller key.</summary>
        private void Overhead()
        {
            foreach(var f in faces)
            {
                if(f.Front==0||f.Top-f.Base<8)continue;int kind=KindOfFace(f);if(kind>=Meridian&&kind<Exchange)continue;string id=blocks[f.Block].id;double len=f.U1-f.U0;
                for(double u0=f.U0+2+5*Hash(id,f.Index,1101);u0<f.U1-2;u0+=7+6*Hash(id,f.Index,(int)(u0*10)))
                {
                    double y=6.2+2.4*Hash(id,(int)(u0*10),1102);if(y>f.Top-f.Base-1)continue;At(f,u0,0,.1,out double x,out double z);
                    // The facing wall across: a face looking back along the normal, 3.5-16 m off, whose span holds the point.
                    Face g=null;double gd=99,gu=0;
                    foreach(var o in faces)
                    {
                        if(o==f||o.Axis!=f.Axis||o.Sign!=-f.Sign||o.Front==0||o.Top-o.Base<y+1)continue;double dd=(o.Plane-f.Plane)*f.Sign;if(dd<3.5||dd>16||dd>gd)continue;
                        double ou=U(o,x,z);if(ou<o.U0+1||ou>o.U1-1)continue;if(dd==gd&&g!=null&&string.CompareOrdinal(blocks[o.Block].id,blocks[g.Block].id)>=0)continue;g=o;gd=dd;gu=ou;
                    }
                    if(g==null)continue;string gid=blocks[g.Block].id;if(string.CompareOrdinal(id+f.Index,gid+g.Index)>0)continue;
                    double ua=u0;if(!Anchors(f,ref ua,g,ref gu,y,out y))continue;
                    bool alley=f.Front==1||g.Front==1||gd<6;
                    double t=Hash(id,(int)(u0*10),1103);Begin();Wire(f,ua,g,gu,y,.3+.25*gd/10,Math.Max(4,(int)(gd/2.4)),out double mx,out double my,out double mz);
                    if(Hash(id,(int)(u0*10),1104)<.35){Wire(f,ua+.15,g,gu-.15*(Tx(f)*Tx(g)+Tz(f)*Tz(g)),y-.25,.45+.25*gd/10,Math.Max(4,(int)(gd/2.4)),out _,out _,out _);}
                    if(alley&&t<.45)
                    {
                        // Washing: shirts, sheets and towels pegged along the line's middle, hanging with its sag.
                        At(f,ua,0,.1,out double ax,out double az);At(g,gu,0,.1,out double bx,out double bz);double sag=.3+.25*gd/10;int cloths=3+(int)(Hash(id,(int)(u0*10),1105)*5);double yaw=Math.Atan2(bx-ax,bz-az)*180/Math.PI;
                        for(int k=0;k<cloths;k++){double tt=.2+.6*(k+.5)/cloths,cx=ax+(bx-ax)*tt,cz=az+(bz-az)*tt,cy=f.Base+y-sag*4*tt*(1-tt);double cw=.3+.45*Hash(id,k,1106),ch=.4+.5*Hash(id,k,1107);
                            onFace=null;Box(f.Block,cx,cy-ch/2-.02,cz,.02,ch,cw,With(P(NativeBlockLooks.Plain,Pick(ClothHexes,Hash(id,k+(int)(u0*10),1108)),RoleOverhead),detail:true,fade:true),AxisRot(0,1,0,yaw+90));}
                    }
                    else if(!alley&&t<.22)
                    {
                        double s=Hash(id,(int)(u0*10),1109);int state=s<.5?0:s<.75?1:2;onFace=null;
                        Box(f.Block,mx,my-.2,mz,.03,.36,.03,With(P(NativeBlockLooks.Plain,0x141414,RoleOverhead),detail:true));Box(f.Block,mx,my-.44,mz,.44,.14,.44,P(NativeBlockLooks.SteelGreen,0x1E2220,RoleOverhead));
                        Box(f.Block,mx,my-.53,mz,.3,.04,.3,state==2?P(NativeBlockLooks.Plain,0x3A3630,RoleOverhead):Glow(SodiumHex,1.8f,RoleLamp,state==1?DriveFlicker:DriveSteady,(float)s*11));
                        if(state<2&&!Wild(mx,mz))Light(mx,my-.8,mz,SodiumHex,.9,11,1.0,state==0?GlowLamp:GlowLampFlicker,s*11);
                    }
                    if(End()){Use(f,ua-.25,ua+.25,y-.4,y+.4);Use(g,gu-.25,gu+.25,y-.4,y+.4);}
                }
            }
        }

        // ---------------- roofs ----------------
        /// <summary>More on the roofs: wooden water tanks on legs over the taller homes, chimney stacks with pots on brick, aerials,
        /// stair huts: the skyline seen from the street.</summary>
        private void RoofDetail()
        {
            foreach(var kv in byStem)
            {
                if(!IsBuilding(kv.Key))continue;int i=kv.Value;var b=blocks[i];string id=b.id;int kind=KindOf(kv.Key.Substring(5));if(kind>=Meridian)continue;
                double top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,x1=b.pos.x+b.size.x/2,z0=b.pos.z-b.size.z/2,z1=b.pos.z+b.size.z/2;if(top<7||x1-x0<5||z1-z0<5)continue;
                int n=1+(int)(Hash(id,0,1201)*2.5);
                for(int k=0;k<n;k++)
                {
                    double px=x0+2+(x1-x0-4)*Hash(id,k,1202),pz=z0+2+(z1-z0-4)*Hash(id,k,1203),t=Hash(id,k,1204);if(Overlapping(px,top+1.5,pz,1.6,1.5,1.6,i))continue;
                    if(t<.3&&(kind==Residential||kind==Shops||kind==Civic||kind==Hotel)&&top>=9)
                    {
                        // A water tank: four legs, a deck, the staved tank, its cone (a true taper under the anime map style), the finial.
                        double r=1.0+.3*Hash(id,k,1205);Begin();var leg=P(NativeBlockLooks.SteelGreen,0x2A2622,RoleRoof);var stave=P(NativeBlockLooks.Timber,Pick(new[]{0x5A4632,0x4A3A2C,0x6A5440},Hash(id,k,1206)),RoleRoof);
                        foreach(var (sx,sz) in new[]{(-1,-1),(1,-1),(1,1),(-1,1)})Box(i,px+sx*(r-.25),top+1.1,pz+sz*(r-.25),.12,2.2,.12,With(leg,detail:true));
                        Box(i,px,top+2.25,pz,2*r+.2,.12,2*r+.2,With(P(NativeBlockLooks.Timber,0x3A2E22,RoleRoof),detail:true));Round(i,px,top+2.3,pz,r*.92,2.3,stave);
                        Round(i,px,top+4.6,pz,r*.98,.35,P(NativeBlockLooks.Tar,0x2A2624,RoleRoof));Box(i,px,top+5.15,pz,r*.9,.5,r*.9,P(NativeBlockLooks.Tar,0x2A2624,RoleRoof,NativeMapStyle.Taper),AxisRot(0,1,0,45));
                        Box(i,px,top+5.6,pz,.08,.5,.08,With(leg,detail:true));
                        foreach(double hy in new[]{2.9,4.0})Box(i,px,top+hy,pz,2*r*.94,.05,2*r*.94,With(P(NativeBlockLooks.SteelGreen,0x2A2622,RoleRoof),detail:true),AxisRot(0,1,0,11.25));
                        End();
                    }
                    else if(t<.55&&b.material=="brick")
                    {
                        Begin();Box(i,px,top+.9,pz,1.4,1.8,.7,With(P(NativeBlockLooks.BrickRed,0x6A3A2A,RoleRoof),detail:true));Box(i,px,top+1.86,pz,1.5,.12,.8,With(P(NativeBlockLooks.ConcreteBoard,0x6A645C,RoleRoof),detail:true));
                        for(int p=0;p<3;p++)Box(i,px-.45+p*.45,top+2.1,pz,.22,.36,.22,With(P(NativeBlockLooks.Terracotta,0x8A4A30,RoleRoof),detail:true));End();
                    }
                    else if(t<.78)
                    {
                        Begin();var mast=With(P(NativeBlockLooks.SteelGreen,0x3A3C3E,RoleRoof),detail:true);Box(i,px,top+2.0,pz,.06,4.0,.06,mast);
                        for(int p=0;p<3;p++)Box(i,px,top+2.6+p*.55,pz,1.4-p*.3,.03,.03,mast,AxisRot(0,1,0,Hash(id,k,1207)*180));
                        Box(i,px+.5,top+1.6,pz,.02,.02,1.6,With(P(NativeBlockLooks.Plain,0x141414,RoleRoof),detail:true),Then(AxisRot(0,1,0,30),AxisRot(1,0,0,-55)));End();
                    }
                    else
                    {
                        Begin();Box(i,px,top+1.2,pz,2.0,2.4,2.4,P(b.material=="brick"?NativeBlockLooks.BrickRed:NativeBlockLooks.ConcreteBoard,b.material=="brick"?0x6A3E30:0x7A7468,RoleRoof));
                        Box(i,px,top+2.46,pz,2.3,.12,2.7,P(NativeBlockLooks.Tar,0x2A2828,RoleRoof));Box(i,px+1.01,top+1.0,pz,.04,2.0,.9,With(P(NativeBlockLooks.SteelGreen,0x3A3E36,RoleRoof),detail:true));End();
                    }
                }
            }
        }
    }
}
