using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The generated Expedition's outdoor map dressed (Claude, 2026-11-01; docs/EXPEDITION-DEPLOYMENT.md "Next integration:
    // Claude": "Progressively blend scenery toward each instance using the cell influence weights, not hard seams between
    // biomes"). Astra's blockout (Core NativeExpeditionGeneratedWorld) stands 10 m building cells 8 to 14 m tall between its
    // streets, the sanctuary's metal fences and gates, a frame at each of the three entrances and the final court's walls, every
    // id under the generated map's prefix. Stem names them in the town's terms (a building "bldg-g:x:z", a fence section
    // "sanctuary-fence-...", the rest "gen-..."), so the town's facades, fences, gate headers and join lights dress them as
    // they dress the town. On top (presentation only: nothing here collides, moves a block or enters the network frame):
    //  - District looks: each building takes one district's look, picked by the blend at its centre
    //    (NativeDeployPresentation.Weights, the authority's cell influences) against a stable hash of its id, so a district's
    //    buildings thin out toward the plaza and mix where two districts meet, with no seam:
    //     - neutral: the town's dusk tenements, shops and offices;
    //     - industrial (City Ironworks): brick depots with roller doors, rust sheeting, pipes, stencils, burning chimneys;
    //     - rail (Central Station): cream tile, a teal band, the metro roundel and departure boards, cold lamps;
    //     - quarantine (Ward Zero): white cladding, cold ribbon lights, hazard hems, notices, red lamps, turning beacons.
    //  - Street lamps on the street faces in their district's light.
    //  - The streets' paint (flat): centre dashes, kerbs, the district's ground marks (rails set in the road, tactile strips,
    //    decon bands).
    //  - Each entrance frame clad in its district's look, its name over the doorway both ways, its accent lights and beacons.
    //  - The final court's walls as a checkpoint: blast panels, razor wire, floodlights, its lettering.
    // The town's placement rules hold (Tests/GeneratedDistrictChecks.cs): below 3.1 m nothing stands out more than WallBand
    // from a block except flat paint, and nothing lies in a record's keep-out (spawn exits, slots, doors and their airlock
    // units, join circles, the final gate).
    public sealed partial class NativeExpeditionCityKit
    {
        /// <summary>The generated map's block id prefix.</summary>
        public const string GeneratedPrefix=NativeExpeditionGeneratedWorld.MapId+"-";
        /// <summary>The district looks: the town's (neutral and any theme it does not know) and the three the authority rolls.</summary>
        public const int LookTown=0,LookIndustrial=1,LookRail=2,LookQuarantine=3;
        public static int LookOf(string theme)=>theme=="industrial"?LookIndustrial:theme=="rail"?LookRail:theme=="quarantine"?LookQuarantine:LookTown;
        /// <summary>For the checks: each block's district (0 neutral, 1 + the blueprint's district index), -1 for anything but a
        /// generated building.</summary>
        public int[] Districts=Array.Empty<int>();
        private ExpeditionDeploymentState blueprint;private bool generated;private int floorBlock=-1;
        private static readonly int[] RustHexes={0x6A3418,0x7A4020,0x5A2C16,0x8A4A26},BrickHexes={0x6E3A2C,0x5E3226,0x7A4232,0x64382C};
        private static readonly string[] IronStencils={"SMELTER","NO 2 YARD","HEAT","SLAG","CASTING","BAY 4"},RailBoards={"LINE 3 SUSPENDED","ALL LINES CLOSED","PLATFORM 2","SERVICE ENDED"},
            WardNotices={"QUARANTINE","DECON","NO ENTRY","BIOHAZARD"};
        private const int TileHex=0xC8BCA0,RailTealHex=0x2E8A8C,RailLampHex=0xBFF5EC,WardRedHex=0xC8281E,BlastHex=0x8E8A80;

        /// <summary>The district (0 neutral, 1 + district index) a generated building at (x, z) dresses as: the blend's shares,
        /// squared and renormalised (each district reads as its own near its entrance, still with no seam), stacked against a
        /// stable hash of its id.</summary>
        public static int DistrictAt(ExpeditionDeploymentState s,string id,double x,double z)
        {
            if(s==null)return 0;var w=NativeDeployPresentation.Weights(s,(float)x,(float)z);double sum=0;for(int k=0;k<4;k++)sum+=w[k]*w[k];if(!(sum>0))return 0;
            double r=Hash(id,7,3)*sum,acc=0;for(int k=0;k<4;k++){acc+=w[k]*w[k];if(r<acc)return k;}return 0;
        }
        private int LookOfDistrict(int d)=>d<=0||blueprint?.districts==null||d>blueprint.districts.Length?LookTown:LookOf(blueprint.districts[d-1].theme);
        private string ZoneOf(int d)=>d<=0||blueprint?.districts==null||d>blueprint.districts.Length?null:blueprint.districts[d-1].instance;

        // ---------------- buildings ----------------
        private void GeneratedBuilding(int i,string name)
        {
            var b=blocks[i];if(Districts.Length!=blocks.Length)Districts=new int[blocks.Length];
            int d=DistrictAt(blueprint,b.id,b.pos.x,b.pos.z);Districts[i]=d+1;int look=LookOfDistrict(d);string id=b.id;
            if(!facesOf.TryGetValue(i,out var fs))return;double h=b.size.y;
            int kind=look==LookIndustrial?Depot:look==LookRail?(h>=12?Office:Civic):h>=12&&Hash(id,0,11)<.35?Office:Hash(id,0,12)<.55?Shops:Residential;
            foreach(var f in fs)
            {
                Kerb(f);
                if(look==LookIndustrial){IronFace(f);facadeMaterial="brick";Facade(f,Depot,name);facadeMaterial=null;}
                else if(look==LookRail){RailFace(f);Facade(f,kind,name);}
                else if(look==LookQuarantine)WardFace(f);
                else Facade(f,kind==Shops&&f.Front<2?Residential:kind,name);
                if(f.Front==2)WallLamps(f,look);
            }
            if(look==LookIndustrial)IronRoof(i);else if(look==LookQuarantine)WardRoof(i);else Roof(i,name,kind);
        }
        /// <summary>A face's facade material for Facade (null: its block's own).</summary>
        private string facadeMaterial;
        /// <summary>A skin over a face's open area from y0 to y1 (m above its base), d out of the plane: one panel per band and run
        /// the face's occluders leave open.</summary>
        private void Skin(Face f,double y0,double y1,double d,Pen pen)
        {
            var ys=new List<double>{y0,y1};foreach(var r in f.Occ){if(r[3]>y0&&r[3]<y1)ys.Add(r[3]);if(r[2]>y0&&r[2]<y1)ys.Add(r[2]);}
            ys.Sort();
            for(int k=0;k+1<ys.Count;k++){double a=ys[k],c=ys[k+1];if(c-a<.05)continue;foreach(var run in Runs(f,a,c,f.U0,f.U1,0,false))FBox(f,(run[0]+run[1])/2,(a+c)/2,d,run[1]-run[0],c-a,.03,pen);}
        }
        // The kerb at a street face's foot: flat, dark, along its open runs.
        private void Kerb(Face f)
        {
            if(f.Front<1||f.Base>.01)return;
            foreach(var run in Runs(f,0,.3,f.U0,f.U1,.02,false))FBox(f,(run[0]+run[1])/2,.025,.17,run[1]-run[0],.05,.34,With(P(NativeBlockLooks.ConcreteBoard,0x7A766C,RoleGround),detail:true));
        }
        // ---------------- City Ironworks ----------------
        private void IronFace(Face f)
        {
            string id=blocks[f.Block].id;double h=f.Top-f.Base,s=Hash(id,f.Index,201);
            Skin(f,0,h,.016,P(NativeBlockLooks.BrickRed,Pick(BrickHexes,s*7),RoleFacade));
            // A hazard hem at the foot, rust sheeting over the top storey on some faces.
            foreach(var run in Runs(f,.05,.55,f.U0+.2,f.U1-.2,.05))FBox(f,(run[0]+run[1])/2,.3,.04,run[1]-run[0],.5,.02,With(P(NativeBlockLooks.Demolition,0,RoleFacade),detail:true));
            if(h>=10&&s<.55)foreach(var run in Runs(f,h-2.6,h-.6,f.U0+.3,f.U1-.3,.05)){double w=Math.Min(run[1]-run[0],6+4*Hash(id,f.Index,202));double u=run[0]+w/2;
                FBox(f,u,h-1.6,.06,w,2.0,.04,P(NativeBlockLooks.Corrugated,Pick(RustHexes,Hash(id,f.Index,203)*5),RoleFacade));Use(f,u-w/2,u+w/2,h-2.6,h-.6);break;}
            // Pipes up the wall from 3.2 m (clear of the wall band) and one run across at 5.6 m.
            if(f.Front>=1&&h>=8)
            {
                double pu=f.U0+.8+(f.U1-f.U0-1.6)*Hash(id,f.Index,204);
                if(Open(f,pu-.2,pu+.2,3.2,h-.4,.02)){Begin();FBox(f,pu,(3.2+h)/2,.16,.22,h-3.2,.22,P(NativeBlockLooks.SteelGreen,0x4A3E34,RoleFacade));for(double y=3.6;y<h-.5;y+=2.2)FBox(f,pu,y,.1,.34,.08,.12,With(P(NativeBlockLooks.SteelGreen,0x2E2A26,RoleFacade),detail:true));if(End())Use(f,pu-.2,pu+.2,3.2,h-.4);}
                foreach(var run in Runs(f,5.45,5.75,f.U0+.3,f.U1-.3,.05,false)){if(run[1]-run[0]<3)continue;Begin();FBox(f,(run[0]+run[1])/2,5.6,.14,run[1]-run[0],.18,.18,P(NativeBlockLooks.SteelGreen,0x3E3630,RoleFacade));End();}
            }
            // A stencilled bay name on the street faces.
            if(f.Front==2&&h>=10&&s>.4)
            {
                string t=IronStencils[(int)(Hash(id,f.Index,205)*IronStencils.Length)%IronStencils.Length];double cell=.11,u=(f.U0+f.U1)/2,y=h-3.2;
                if(Open(f,u-TextWidth(t,cell)/2-.2,u+TextWidth(t,cell)/2+.2,y-.1,y+5*cell+.1,.02)){Begin();Text(f,t,u,y,.03,cell,.01,P(NativeBlockLooks.Plain,0xD8C8A0,RoleSign));if(End())Use(f,u-TextWidth(t,cell)/2-.2,u+TextWidth(t,cell)/2+.2,y-.1,y+5*cell+.1);}
            }
        }
        // A brick stack burning at the top (its flames and smoke the atmosphere's), roof vents.
        private void IronRoof(int i)
        {
            var b=blocks[i];string id=b.id;double top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,z0=b.pos.z-b.size.z/2;
            if(Hash(id,0,210)<.45)
            {
                double px=x0+1.6+(b.size.x-3.2)*Hash(id,0,211),pz=z0+1.6+(b.size.z-3.2)*Hash(id,0,212),sh=4+3*Hash(id,0,213);
                Begin();Box(i,px,top+sh/2,pz,1.3,sh,1.3,With(P(NativeBlockLooks.BrickRed,0x5A2E24,RoleRoof),shadow:true));
                Box(i,px,top+sh-.15,pz,1.5,.3,1.5,P(NativeBlockLooks.ConcreteBoard,0x3A3430,RoleRoof));
                bool lit=Hash(id,0,214)<.6;Box(i,px,top+sh+.02,pz,.9,.04,.9,lit?Glow(FireHex,2.4f,RoleRoof,DriveFire,(float)Hash(id,0,215)*6):P(NativeBlockLooks.Plain,SootHex,RoleRoof));
                if(End()&&lit)Light(px,top+sh+.1,pz,FireHex,1.2,12,1.1,GlowFire,Hash(id,0,216)*6);
            }
            int n=1+(int)(Hash(id,0,217)*2);
            for(int k=0;k<n;k++){double px=x0+1.2+(b.size.x-2.4)*Hash(id,k,218),pz=z0+1.2+(b.size.z-2.4)*Hash(id,k,219);Begin();Box(i,px,top+.45,pz,1.4,.9,1.0,With(P(NativeBlockLooks.Corrugated,Pick(RustHexes,Hash(id,k,220)*5),RoleRoof),detail:true));End();}
        }
        // ---------------- Central Station ----------------
        private void RailFace(Face f)
        {
            string id=blocks[f.Block].id;double h=f.Top-f.Base;
            Skin(f,0,3.0,.016,P(NativeWardZeroLooks.Tile,TileHex,RoleFacade,NativeCyberCityLooks.Bank));
            Skin(f,3.0,3.24,.03,P(NativeBlockLooks.Plain,RailTealHex,RoleFacade));
            Skin(f,3.24,h,.016,P(NativeBlockLooks.ConcreteBoard,0xA8A090,RoleFacade));
            if(f.Front<2)return;
            // The metro roundel on a blade at the face's end, lit teal; a departure board over the ground floor.
            double bu=Hash(id,f.Index,231)<.5?f.U0+.9:f.U1-.9;
            if(Open(f,bu-.5,bu+.5,4.3,5.6,.05))
            {
                Begin();FBox(f,bu,4.95,.35,.12,.12,.7,P(NativeBlockLooks.SteelGreen,0x2A3034,RoleSign));
                FBox(f,bu,4.95,.75,.06,1.0,.9,P(NativeBlockLooks.Plain,0xE8E4D8,RoleSign));FBox(f,bu,4.95,.75,.08,.24,1.04,Glow(0x3FD8C8,1.5f,RoleSign));
                At(f,bu,0,.75,out double lx,out double lz);if(End()){Use(f,bu-.5,bu+.5,4.3,5.6);Light(lx,f.Base+4.95,lz,0x7AE8D0,.6,6,.3,GlowNeon);}
            }
            if(Hash(id,f.Index,232)<.6)
            {
                string t=RailBoards[(int)(Hash(id,f.Index,233)*RailBoards.Length)%RailBoards.Length];double cell=.05,w=TextWidth(t,cell)+.5,u=(f.U0+f.U1)/2;
                if(Open(f,u-w/2,u+w/2,3.35,3.95,.05)){Begin();FBox(f,u,3.65,.05,w,.5,.06,P(NativeBlockLooks.Plain,0x101214,RoleSign));Text(f,t,u,3.65-2.5*cell,.085,cell,.01,Glow(AmberHex,1.4f,RoleSign,Hash(id,f.Index,234)<.3?DriveFlicker:DriveSteady,(float)Hash(id,f.Index,235)*5));if(End())Use(f,u-w/2,u+w/2,3.35,3.95);}
            }
        }
        // ---------------- Ward Zero ----------------
        private void WardFace(Face f)
        {
            string id=blocks[f.Block].id;double h=f.Top-f.Base;
            Skin(f,0,h,.016,P(NativeBlockLooks.Plain,MeridianHex,RoleFacade));
            foreach(var run in Runs(f,.05,.55,f.U0+.2,f.U1-.2,.05))FBox(f,(run[0]+run[1])/2,.3,.04,run[1]-run[0],.5,.02,With(P(NativeBlockLooks.Demolition,0,RoleFacade),detail:true));
            Skin(f,3.1,3.3,.03,P(NativeBlockLooks.Plain,MeridianTealHex,RoleFacade));
            // Cold ribbon windows, one per storey: dark glass, a lit run here and there.
            for(double y=4.4;y+1.0<h-.6;y+=3.0)
                foreach(var run in Runs(f,y-.1,y+1.0,f.U0+.6,f.U1-.6,.1))
                {
                    double w=run[1]-run[0];if(w<1.5||!Open(f,run[0],run[1],y-.1,y+1.0,.02))continue;Use(f,run[0],run[1],y-.1,y+1.0);
                    FBox(f,(run[0]+run[1])/2,y+.45,.03,w,.8,.04,P(NativeBlockLooks.Plain,GlassBlueHex,RoleFacade));
                    double s=Hash(id,f.Index*13+(int)y,241);if(s<.45){double lw=Math.Min(w-.2,1.2+2*Hash(id,(int)y,242)),lu=run[0]+.1+(w-.2-lw)*Hash(id,(int)y,243);FBox(f,lu+lw/2,y+.45,.05,lw,.7,.02,Glow(ColdHex,(float)(.35+.3*s),RoleFacade,s<.1?DriveFlicker:DriveSteady,(float)s*9));}
                    FBox(f,(run[0]+run[1])/2,y-.05,.06,w+.1,.08,.1,With(P(NativeBlockLooks.SteelGreen,0xB8C0C0,RoleFacade),detail:true));
                }
            // The ground floor sealed: a shutter or a door with tape across; a red-lettered notice on the street faces.
            foreach(var run in Runs(f,.6,3.0,f.U0+.4,f.U1-.4))
            {
                double u=(run[0]+run[1])/2;if(run[1]-run[0]<2.2||!Open(f,u-1,u+1,.6,2.9,.02))continue;Use(f,u-1,u+1,.6,2.9);
                FBox(f,u,1.6,.03,1.8,2.2,.04,P(NativeBlockLooks.Corrugated,0xB4BAB8,RoleFacade));
                foreach(double r in new[]{-14.0,14.0})FBox(f,u,1.7,.06,2.0,.1,.01,With(P(NativeBlockLooks.Plain,0xE8C020,RoleFacade),detail:true),r);
                break;
            }
            if(f.Front==2)
            {
                string t=WardNotices[(int)(Hash(id,f.Index,244)*WardNotices.Length)%WardNotices.Length];double cell=.08,w=TextWidth(t,cell)+.4,u=f.U0+w/2+.4+(f.U1-f.U0-w-.8)*Hash(id,f.Index,245);
                if(f.U1-f.U0>w+.8&&Open(f,u-w/2,u+w/2,2.0,2.7,.02)){Begin();FBox(f,u,2.35,.03,w,.6,.02,P(NativeBlockLooks.Plain,0xF2F2EE,RoleSign));Text(f,t,u,2.35-2.5*cell,.045,cell,.006,P(NativeBlockLooks.Plain,WardRedHex,RoleSign));if(End())Use(f,u-w/2,u+w/2,2.0,2.7);}
            }
        }
        // An air handler and, on some, a turning red beacon.
        private void WardRoof(int i)
        {
            var b=blocks[i];string id=b.id;double top=b.pos.y+b.size.y/2,x0=b.pos.x-b.size.x/2,z0=b.pos.z-b.size.z/2;
            double px=x0+1.8+(b.size.x-3.6)*Hash(id,0,251),pz=z0+1.8+(b.size.z-3.6)*Hash(id,0,252);
            Begin();Box(i,px,top+.6,pz,2.2,1.2,1.6,P(NativeBlockLooks.Plain,0xC9CFCC,RoleRoof));Box(i,px,top+1.22,pz,1.4,.04,1.0,With(P(NativeBlockLooks.FactoryGrating,0x5D6B6E,RoleRoof),detail:true));End();
            if(Hash(id,0,253)<.35)
            {
                double bx=x0+.8+(b.size.x-1.6)*Hash(id,0,254),bz=z0+.8;float ph=(float)Hash(id,0,255);
                Begin();Box(i,bx,top+.2,bz,.3,.4,.3,P(NativeBlockLooks.SteelGreen,0x2F383B,RoleRoof));Box(i,bx,top+.52,bz,.26,.24,.26,Glow(RedHex,2.4f,RoleRoof,DriveStrobe,ph));
                if(End())Light(bx,top+.55,bz,RedHex,.8,9,.9,GlowBeacon,ph);
            }
        }
        // ---------------- street lamps ----------------
        private void WallLamps(Face f,int look)
        {
            string id=blocks[f.Block].id;double len=f.U1-f.U0;int n=len>=9?2:len>=4?1:0;
            int hex=look==LookIndustrial?SodiumHex:look==LookRail?RailLampHex:look==LookQuarantine?ColdHex:SodiumHex;
            for(int k=0;k<n;k++)
            {
                double u=n==1?(f.U0+f.U1)/2:f.U0+len*(k==0?.25:.75),s=Hash(id,f.Index*7+k,261);if(!Open(f,u-.35,u+.35,4.2,5.2,.02))continue;
                bool dead=look==LookTown&&s<.2,flicker=!dead&&s<.38;int lamp=look==LookQuarantine&&s>.8?RedHex:hex;
                Begin();FBox(f,u,4.65,.3,.1,.1,.6,P(NativeBlockLooks.SteelGreen,0x2A2C2E,RoleLamp));
                FBox(f,u,4.55,.62,.36,.14,.3,P(NativeBlockLooks.SteelGreen,0x3A3C3E,RoleLamp));
                FBox(f,u,4.46,.62,.3,.04,.24,dead?P(NativeBlockLooks.Plain,0x2A2622,RoleLamp):Glow(lamp,2.2f,RoleLamp,flicker?DriveFlicker:DriveSteady,(float)s*11));
                At(f,u,0,.62,out double lx,out double lz);
                if(End()){Use(f,u-.35,u+.35,4.2,5.2);if(!dead)Light(lx,f.Base+4.4,lz,lamp,.75,11,.5,flicker?GlowLampFlicker:GlowLamp,s*11);}
            }
        }
        // ---------------- the streets ----------------
        private bool RoadAt(int x,int z){var c=NativeDeployPresentation.CellAt(blueprint,x,z);return c!=null&&c.road;}
        private void GeneratedStreets()
        {
            if(blueprint?.cells==null||floorBlock<0)return;
            foreach(var c in blueprint.cells)
            {
                if(!c.road||c.sanctuary)continue;double cx=c.x*10,cz=c.z*10;bool ew=RoadAt(c.x-1,c.z)||RoadAt(c.x+1,c.z),ns=RoadAt(c.x,c.z-1)||RoadAt(c.x,c.z+1);
                int d=DistrictAt(blueprint,"cell-"+c.x+":"+c.z,cx,cz);int look=LookOfDistrict(d);string key="cell-"+c.x+":"+c.z;
                // Centre dashes along a straight run (none at a junction).
                if(ew!=ns)for(int k=-1;k<=1;k+=2){double a=k*2.5;Box(floorBlock,ew?cx+a:cx,.004,ew?cz:cz+a,ew?2.4:.14,.008,ew?.14:2.4,P(NativeBlockLooks.Plain,PaintWhiteHex,RoleGround));}
                if(look==LookIndustrial&&ew!=ns)
                    // Rails set in the road along the run.
                    foreach(double g in new[]{-.72,.72})Box(floorBlock,ew?cx:cx+g-2,.006,ew?cz+g-2:cz,ew?10:.08,.012,ew?.08:10,P(NativeBlockLooks.SteelGreen,0x4A4440,RoleGround));
                else if(look==LookRail&&ew!=ns)
                    // A yellow tactile strip along one side.
                    Box(floorBlock,ew?cx:cx+3.6,.005,ew?cz+3.6:cz,ew?10:.4,.01,ew?.4:10,P(NativeBlockLooks.Plain,PaintYellowHex,RoleGround));
                else if(look==LookQuarantine)
                    // A decon band across the street.
                    Box(floorBlock,cx,.005,cz,ew&&!ns?.8:9.6,.01,ew&&!ns?9.6:.8,P(NativeBlockLooks.Demolition,0,RoleGround));
                else if(Hash(key,0,271)<.35)
                    Box(floorBlock,cx+(Hash(key,0,272)-.5)*6,.004,cz+(Hash(key,0,273)-.5)*6,1.2+1.5*Hash(key,0,274),.008,1.0+1.2*Hash(key,0,275),With(P(NativeBlockLooks.Plain,0x1A1614,RoleGround),detail:true),AxisRot(0,1,0,Hash(key,0,276)*90));
            }
        }
        // ---------------- the generated pieces ----------------
        private void GeneratedPiece(int i,string s)
        {
            if(s=="gen-floor"){floorBlock=i;return;}
            if(Starts(s,"gen-lintel-"))EntranceLintel(i,s.Substring(11));
            else if(Starts(s,"gen-final-"))FinalWall(i,s.Substring(10));
        }
        // The frame over an entrance: clad in its district's look, its zone's name both ways, a light strip under it and its
        // beacons on top.
        private void EntranceLintel(int i,string zone)
        {
            var b=blocks[i];int d=0;if(blueprint?.districts!=null)for(int k=0;k<blueprint.districts.Length;k++)if(blueprint.districts[k].instance==zone)d=k+1;
            int look=LookOfDistrict(d);var z=NativeExpeditionJoinZones.Of(zone);int accent=z.Accent;bool alongX=b.size.x>b.size.z;
            var skin=look==LookIndustrial?P(NativeBlockLooks.Corrugated,0x5A3420,RoleLandmark):look==LookRail?P(NativeWardZeroLooks.Tile,TileHex,RoleLandmark,NativeCyberCityLooks.Bank):
                look==LookQuarantine?P(NativeBlockLooks.Plain,MeridianHex,RoleLandmark):P(NativeBlockLooks.SteelGreen,0x3A3C3E,RoleLandmark);
            double top=b.pos.y+b.size.y/2,bottom=b.pos.y-b.size.y/2,len=alongX?b.size.x:b.size.z;
            Begin();Box(i,b.pos.x,b.pos.y,b.pos.z,b.size.x+.06,b.size.y+.06,b.size.z+.06,With(skin,shadow:true));End();
            // A hazard or teal band along its foot, the light strip under it.
            Begin();Box(i,b.pos.x,bottom-.05,b.pos.z,alongX?len-.4:.5,.06,alongX?.5:len-.4,Glow(accent,1.8f,RoleLandmark));End();
            foreach(int sign in new[]{-1,1})
            {
                var f=MakeFace(i,alongX?2:0,sign);if(f==null)continue;double mid=(f.U0+f.U1)/2,cell=Math.Min(.12,(len-1.2)/(4*z.Name.Length-1));
                // A dark plate under the letters, so they read on any skin (the station's cream tile among them).
                Begin();FBox(f,mid,(f.Top-f.Base)/2,.06,TextWidth(z.Name,cell)+.4,5*cell+.24,.02,P(NativeBlockLooks.Plain,0x141618,RoleSign));
                Text(f,z.Name,mid,(f.Top-f.Base)/2-2.5*cell,.085,cell,.02,Glow(ZoneInk(zone),1.6f,RoleSign));End();
                At(f,mid,0,1.6,out double lx,out double lz);Light(lx,bottom-.3,lz,accent,.9,10,.4,GlowNeon);
            }
            // Beacons at both ends of its top.
            foreach(int sign in new[]{-1,1})
            {
                double bx=b.pos.x+(alongX?sign*(len/2-.5):0),bz=b.pos.z+(alongX?0:sign*(len/2-.5));float ph=sign>0?.5f:0;
                int hex=look==LookQuarantine?RedHex:look==LookRail?0x7AE8D0:AmberHex;byte drive=look==LookRail?DriveSteady:DriveStrobe;
                Begin();Box(i,bx,top+.15,bz,.34,.3,.34,P(NativeBlockLooks.SteelGreen,0x2A2A28,RoleLandmark));Box(i,bx,top+.42,bz,.26,.24,.26,Glow(hex,2.2f,RoleLandmark,drive,ph));
                if(End())Light(bx,top+.45,bz,hex,.7,9,.9,look==LookRail?GlowNeon:GlowBeacon,ph);
            }
        }
        // The final court's walls: blast panels with seams, razor wire along the top, floodlights, the checkpoint's name.
        private void FinalWall(int i,string which)
        {
            var b=blocks[i];bool alongX=b.size.x>b.size.z;double len=alongX?b.size.x:b.size.z,top=b.pos.y+b.size.y/2;
            foreach(int sign in new[]{-1,1})
            {
                var f=MakeFace(i,alongX?2:0,sign);if(f==null)continue;double h=f.Top-f.Base;
                Skin(f,0,h,.016,P(NativeBlockLooks.ConcreteBoard,BlastHex,RoleLandmark));
                for(double u=f.U0+2;u<f.U1-.5;u+=2)FBox(f,u,h/2,.04,.06,h-.2,.02,With(P(NativeBlockLooks.Plain,0x4A4842,RoleLandmark),detail:true));
                foreach(var run in Runs(f,.05,.55,f.U0+.1,f.U1-.1,.05,false))FBox(f,(run[0]+run[1])/2,.3,.045,run[1]-run[0],.5,.02,With(P(NativeBlockLooks.Demolition,0,RoleLandmark),detail:true));
                // The court's front, facing the city: its name over the gate's sides.
                if(Starts(which,"front-")&&sign<0)
                {
                    double cell=.12,mid=(f.U0+f.U1)/2;Begin();FBox(f,mid,4.3,.04,TextWidth("CHECKPOINT",cell)+.5,1.0,.03,P(NativeBlockLooks.Plain,0x1A1816,RoleSign));
                    Text(f,"CHECKPOINT",mid,4.3-2.5*cell,.07,cell,.012,Glow(RedHex,1.6f,RoleSign,DriveFlicker,(float)Hash(b.id,0,281)*7));End();
                }
            }
            // Razor wire coils along the top and floodlights on posts.
            int coils=(int)(len/.5);double a0=-len/2+.25;
            for(int k=0;k<coils;k++){double a=a0+k*.5;Box(i,alongX?b.pos.x+a:b.pos.x,top+.3,alongX?b.pos.z:b.pos.z+a,alongX?.5:.04,.5,alongX?.04:.5,With(P(NativeBlockLooks.SteelGreen,0x6A6E70,RoleHigh),detail:true),AxisRot(alongX?1:0,0,alongX?0:1,k*40));}
            for(double a=-len/2+5;a<len/2-1;a+=10)
            {
                double fx=alongX?b.pos.x+a:b.pos.x,fz=alongX?b.pos.z:b.pos.z+a;
                Begin();Box(i,fx,top+.8,fz,.14,1.6,.14,P(NativeBlockLooks.SteelGreen,0x2A2C2E,RoleHigh));Box(i,fx,top+1.65,fz,.6,.3,.4,Glow(ColdHex,2.6f,RoleHigh));
                if(End())Light(fx,top+1.6,fz,ColdHex,1.0,16,.6,GlowFlood);
            }
        }
    }
}
