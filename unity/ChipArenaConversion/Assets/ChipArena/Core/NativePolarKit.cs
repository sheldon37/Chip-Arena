using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Polaris Station's box kit (docs/arenas/POLAR.md "The look"): what NativeBlockLit instances draw on the map's dressed blocks
    /// when the modelled art (the stage bank ChipArenaStagePolar, docs/arenas/STAGE-ART.md) is not on the machine. Three roles:
    /// <list type="bullet">
    /// <item><see cref="Attached"/> (its <see cref="Parent"/> the block): the hull's boot-topping, white strake and planked deck,
    /// the wheelhouse's lit windows, the lifeboats' strakes, the terrace's lit edge, the pedestal's bands and beacons, the
    /// cabins' lit windows, skirts, snowed roofs and stovepipes, the ice's facets and its glowing seams, the tanks' hoops, the
    /// snowcats' cabs and tracks, the vents' grates. Every attached part stays inside its block grown by <see cref="Proud"/>
    /// (a stovepipe or a lamp, thinner than <see cref="Thin"/>, within <see cref="ThinReach"/>): what looks like cover is cover.</item>
    /// <item><see cref="Below"/>: the sea in the channel under the thin ice and past the shelf's edge, wholly under
    /// <see cref="BelowY"/> (a body there is already dying).</item>
    /// <item><see cref="Overhead"/>: nothing static; the rig's dish, balloon and lamps (Core NativePolarRig).</item>
    /// </list>
    /// <see cref="Family"/>, <see cref="Owner"/>, <see cref="Kind"/> and <see cref="Role"/> describe each part for the art's layout
    /// export (nothing draws by them). Deterministic; allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativePolarKit
    {
        public const float Proud=.25f,Thin=.3f,ThinReach=.9f;public const double Clearance=2.0,Decal=.012,BelowY=-1.0;
        public const int Attached=0,Overhead=1,Below=2;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();
        public readonly List<string> Family=new List<string>(),Owner=new List<string>();public readonly List<int> Kind=new List<int>(),Role=new List<int>();
        private readonly List<int> parents=new List<int>();private int current=-1,role=Attached;private string family="",owner;
        public int Count=>Parts.Count;
        private NativePolarKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(1024,true,looks);}
        private void Track(int before,int kind){for(int i=before;i<Parts.Count;i++){parents.Add(role==Attached?current:-1);Family.Add(family);Owner.Add(role==Attached?owner:null);Kind.Add(kind);Role.Add(role);}}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow,color,window);Track(n,kind);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);Track(n,kind);}
        private static double H(int a,int b=0,int c=0)=>NativePolarLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private static bool Ends(string id,string p)=>id!=null&&id.EndsWith(p,StringComparison.Ordinal);
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;public double Cy=>(Y0+Y1)/2;}
        private const int Snow=NativePolarLooks.Snow,Ice=NativePolarLooks.Ice,IceDeep=NativePolarLooks.IceDeep,Crystal=NativePolarLooks.Crystal,Hull=NativePolarLooks.Hull,HullDark=NativePolarLooks.HullDark,
            White=NativePolarLooks.White,Steel=NativePolarLooks.Steel,SteelDark=NativePolarLooks.SteelDark,Timber=NativePolarLooks.Timber,TimberDark=NativePolarLooks.TimberDark,Window=NativePolarLooks.Window,
            Lamp=NativePolarLooks.Lamp,Red=NativePolarLooks.Red,Orange=NativePolarLooks.Orange,Water=NativePolarLooks.Water,Glass=NativePolarLooks.Glass,Tarp=NativePolarLooks.Tarp,Vent=NativePolarLooks.Vent,
            Stripe=NativePolarLooks.Stripe,Beacon=NativePolarLooks.Beacon,Deck=NativePolarLooks.Deck;

        /// <summary>The map's kit for its look (null for another map).</summary>
        public static NativePolarKit Build(ContentMap map,MapLook look)
        {
            if(!NativePolarLooks.Active(map?.id))return null;var k=new NativePolarKit(NativePolarLooks.Resolve(look?.kitLooks));
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null||b.role=="carried"||b.role=="structural"||b.role=="cover")continue;
                var x=new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i};
                k.current=i;k.owner=b.id;k.role=Attached;k.Dress(x);
            }
            // the vents' grates on the shelf (the floor block under each)
            foreach(var h in map.hazards??Array.Empty<MapHazard>())
            {
                if(h?.kind!=NativeFactoryHazards.Vent)continue;int floor=FloorUnder(map,h.a.x,h.a.y,h.a.z);if(floor<0)continue;k.current=floor;k.owner=map.blocks[floor].id;k.family="vent";
                double x=h.a.x,y=h.a.y,z=h.a.z,r=h.size.x/2;k.Box(x-r+.12,x+r-.12,y+.004,y+.05,z-r+.12,z+r-.12,Vent,false);
                k.Box(x-r,x+r,y+.004,y+.035,z-r,z-r+.12,Stripe,false);k.Box(x-r,x+r,y+.004,y+.035,z+r-.12,z+r,Stripe,false);k.Box(x-r,x-r+.12,y+.004,y+.035,z-r+.12,z+r-.12,Stripe,false);k.Box(x+r-.12,x+r,y+.004,y+.035,z-r+.12,z+r-.12,Stripe,false);
            }
            // the sea: in the channel under the thin ice, and past the shelf's south edge out to the play space's bound
            k.role=Below;k.current=-1;k.owner=null;k.family="sea";double c=NativePolarLooks.ChannelHalf-.01,sea=NativePolarLooks.Sea;
            k.Box(-c,c,sea-2.4,sea,NativePolarLooks.ChannelS+.01,NativePolarLooks.ChannelN-.01,Water,false);
            k.Box(-NativePolarLooks.ShelfX-1,NativePolarLooks.ShelfX+1,sea-2.4,sea,(map.bounds?.minZ??-29)-1,NativePolarLooks.ShelfZ-.01,Water,false);
            k.Parent=k.parents.ToArray();return k;
        }
        private static int FloorUnder(ContentMap map,double x,double y,double z)
        {
            for(int i=0;i<map.blocks.Length;i++){var b=map.blocks[i];if(b==null)continue;if(Math.Abs(b.pos.y+b.size.y/2-y)<.01&&Math.Abs(x-b.pos.x)<=b.size.x/2&&Math.Abs(z-b.pos.z)<=b.size.z/2)return i;}return -1;
        }

        /// <summary>A plate on face `side` (0 +x, 1 -x, 2 +z, 3 -z, 4 top, 5 bottom): u0..u1 along it (z on ±x faces, x on ±z faces and
        /// the top), v0..v1 up it (y; z on the top), `t` thick from `o` proud.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,double t=Decal,double o=.004,bool shadow=false,double window=1)
        {
            // (wholly outside its face: two faces' plates never share a corner)
            t=Math.Min(t,o);
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow,-1,window);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow,-1,window);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow,-1,window);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow,-1,window);break;
                case 4:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow,-1,window);break;default:Box(u0,u1,b.Y0-o,b.Y0-o+t,v0,v1,kind,shadow,-1,window);break;
            }
        }
        private static void Along(Bx b,int side,out double u0,out double u1){if(side<=1){u0=b.Z0;u1=b.Z1;}else{u0=b.X0;u1=b.X1;}}
        /// <summary>A lit window on a wall face: the pane and its dark frame behind it.</summary>
        private void Pane(Bx b,int side,double u0,double u1,double v0,double v1,int seed)
        {Face(b,side,u0-.08,u1+.08,v0-.08,v1+.08,TimberDark,.02,.012);Face(b,side,u0,u1,v0,v1,Window,.012,.02,false,.75+.25*H(seed,b.Index));}
        /// <summary>A cap of snow on a block's top.</summary>
        private void Cap(Bx b,double inset=.08,double thick=.16){Box(b.X0+inset,b.X1-inset,b.Y1,b.Y1+thick,b.Z0+inset,b.Z1-inset,Snow,false);}

        private void Dress(Bx b)
        {
            string id=b.Id;
            if(Starts(id,"polar-ridge-")){family="ridge";Cap(b,0,.2);return;}
            if(Starts(id,"polar-ship-hull")||Starts(id,"polar-ship-bow-"))
            {
                family="hull";bool bow=Starts(id,"polar-ship-bow-");
                foreach(int side in bow?new[]{0,1,3}:new[]{0,1})
                {Along(b,side,out double u0,out double u1);double d=side==3?.006:0;Face(b,side,u0,u1,b.Y0+d,b.Y0+.9-d,HullDark,.03,.02);Face(b,side,u0,u1,b.Y1-.62+d,b.Y1-.44-d,White,.03,.02);}
                Face(b,4,b.X0+.3,b.X1-.3,bow?b.Z0:b.Z0+.3,b.Z1,Deck,.02,.012);return;
            }
            if(id=="polar-ship-bridge")
            {
                family="wheelhouse";double w=b.Sx/7;
                for(int i=0;i<6;i++)Pane(b,3,b.X0+w*(i+.62),b.X0+w*(i+1.38),b.Y0+6.0,b.Y0+7.2,i);                 // the wheelhouse's windows
                for(int i=0;i<5;i++)Pane(b,3,b.X0+1.2+i*2.3,b.X0+1.75+i*2.3,b.Y0+2.6,b.Y0+3.15,10+i);            // portholes
                Face(b,3,b.X0,b.X1,b.Y1-.5,b.Y1,Red,.03,.02);Face(b,3,b.X0,b.X1,b.Y0+4.6,b.Y0+4.8,SteelDark,.05,.04);
                Face(b,3,-.55,.55,b.Y0,b.Y0+2.1,SteelDark,.04,.03);return;
            }
            if(Starts(id,"polar-ship-rail-")){family="bulwark";Box(b.X0-.03,b.X1+.03,b.Y1,b.Y1+.06,b.Z0,b.Z1,Timber,false);return;}
            if(id=="polar-ship-house")
            {family="winch-house";Face(b,3,-.45,.45,b.Y0,b.Y0+2.0,SteelDark,.04,.03);Pane(b,3,-1.9,-1.3,b.Y0+1.3,b.Y0+1.9,1);Pane(b,3,1.3,1.9,b.Y0+1.3,b.Y0+1.9,2);Pane(b,2,-.4,.4,b.Y0+1.3,b.Y0+1.9,3);Cap(b);return;}
            if(Starts(id,"polar-ship-boat-"))
            {family="lifeboat";foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0+.5,b.Y1-.5,Orange,.03,.02);Face(b,side,u0+.01,u1-.01,b.Y0+1.5,b.Y0+1.62,White,.04,.03);}Cap(b);return;}
            if(id=="polar-ship-windlass"){family="windlass";Box(b.X0+.1,b.X1-.1,b.Y1-.34,b.Y1+.02,b.Cz-.3,b.Cz+.3,SteelDark);return;}
            if(id=="polar-ship-mast"||Starts(id,"polar-mast-")||id=="polar-obs-beacon")
            {
                family=id=="polar-ship-mast"?"foremast":id=="polar-obs-beacon"?"beacon":"weather-mast";
                foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);for(double y=b.Y0+1.2;y<b.Y1-.8;y+=2.4)Face(b,side,u0,u1,y,y+.6,Red,.02,.012);Face(b,side,u0+.1,u1-.1,b.Y1-.45,b.Y1-.12,Beacon,.03,.03);}
                return;
            }
            if(id=="polar-obs-terrace")
            {family="terrace";Face(b,2,b.X0,b.X1,b.Y1-.34,b.Y1-.26,Lamp,.03,.02);Face(b,2,b.X0,b.X1,b.Y0,b.Y0+.4,SteelDark,.03,.02);Face(b,0,b.Z0,b.Z1,b.Y0,b.Y0+.4,SteelDark,.03,.02);Face(b,1,b.Z0,b.Z1,b.Y0,b.Y0+.4,SteelDark,.03,.02);return;}
            if(id=="polar-obs-pedestal")
            {
                family="pedestal";
                foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);foreach(double y in new[]{b.Y0+1.6,b.Y0+4.1,b.Y1-.4})Face(b,side,u0,u1,y,y+.22,SteelDark,.05,.04);Face(b,side,(u0+u1)/2-.2,(u0+u1)/2+.2,b.Y1-1.2,b.Y1-.8,Beacon,.03,.03);}
                Face(b,2,-.5,.5,b.Y0,b.Y0+2.1,SteelDark,.04,.03);Pane(b,2,-1.2,-.75,b.Y0+1.3,b.Y0+1.8,4);return;
            }
            if(id=="polar-obs-back"){family="windbreak";for(double y=b.Y0+.5;y<b.Y1-.2;y+=.6)Face(b,2,b.X0,b.X1,y,y+.05,TimberDark,.02,.012);Cap(b,0,.1);return;}
            if(Starts(id,"polar-obs-rack-"))
            {family="rack";int inner=b.Cx<0?0:1;for(int i=0;i<4;i++){double u=b.Z0+.4+i*.85;Face(b,inner,u,u+.5,b.Y0+1.3,b.Y0+1.62,i%2==0?Glass:Lamp,.02,.02,false,.8);Face(b,inner,u,u+.5,b.Y0+.6,b.Y0+.7,Beacon,.02,.02);}return;}
            if(Starts(id,"polar-obs-console-")){family="console";Face(b,4,b.X0+.15,b.X1-.15,b.Z0+.12,b.Z1-.12,Glass,.02,.012,false,.9);return;}
            if(Starts(id,"polar-cabin-")||Starts(id,"polar-lab-")||Starts(id,"polar-shed-")){Building(b);return;}
            if(Starts(id,"polar-spire-")||Starts(id,"polar-floe-"))
            {
                family=Starts(id,"polar-spire-")?"spire":"floe";
                foreach(int side in new[]{0,1,2,3})
                {
                    Along(b,side,out double u0,out double u1);double w=u1-u0;
                    Face(b,side,u0+w*.12,u0+w*.5,b.Y0+b.Sy*.1,b.Y1-b.Sy*(.1+.2*H(b.Index,side)),IceDeep,.03,.02);
                    if(family=="spire")Face(b,side,u0+w*(.58+.2*H(b.Index,side,1)),u0+w*(.64+.2*H(b.Index,side,1)),b.Y0+.3,b.Y1-.4,Crystal,.02,.03);
                }
                Cap(b,.2,.14);return;
            }
            if(Starts(id,"polar-tank-")){family="tank";foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0+.5,b.Y0+.62,SteelDark,.04,.03);Face(b,side,u0,u1,b.Y1-.7,b.Y1-.58,SteelDark,.04,.03);Face(b,side,u0,u1,b.Y0+1.3,b.Y0+1.6,Stripe,.02,.012);}Cap(b);return;}
            if(Starts(id,"polar-snowcat-")&&!Ends(id,"-blade"))
            {
                family="snowcat";foreach(int side in new[]{0,1}){Face(b,side,b.Z0,b.Z1,b.Y0,b.Y0+.75,HullDark,.06,.05);Pane(b,side,b.Z1-2.0,b.Z1-.5,b.Y0+1.45,b.Y0+2.15,side);}
                Pane(b,2,b.X0+.35,b.X1-.35,b.Y0+1.45,b.Y0+2.15,3);Face(b,2,b.X0+.3,b.X0+.6,b.Y0+.95,b.Y0+1.2,Lamp,.03,.03);Face(b,2,b.X1-.6,b.X1-.3,b.Y0+.95,b.Y0+1.2,Lamp,.03,.03);Cap(b);return;
            }
            if(Starts(id,"polar-hut-")){family="hut";Pane(b,b.Cx<0?0:1,b.Cz-.5,b.Cz+.5,b.Y0+1.3,b.Y0+1.9,1);Face(b,2,b.Cx-.45,b.Cx+.45,b.Y0,b.Y0+2.0,TimberDark,.04,.03);Cap(b);return;}
            if(Starts(id,"polar-stack-")){family="stack";foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0+.9,b.Y1,Tarp,.03,.02);}Face(b,4,b.X0,b.X1,b.Z0,b.Z1,Tarp,.03,.02);return;}
        }
        // A cabin, the lab or the shed: by the piece's name. Outward is away from the building's own middle.
        private void Building(Bx b)
        {
            string id=b.Id;bool cabin=Starts(id,"polar-cabin-");family=cabin?"cabin":Starts(id,"polar-lab-")?"lab":"shed";int frame=cabin?TimberDark:SteelDark;
            if(Ends(id,"-roof"))
            {
                family+="-roof";Cap(b,.1,.2);foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0,b.Y1,SteelDark,.03,.02);}
                // the stovepipe (thin: within ThinReach of the roof)
                double px=b.Cx+(b.Cx<0?1:-1)*(b.Sx/2-1.2),pz=b.Cz+b.Sz/2-1.4;Box(px-.11,px+.11,b.Y1,b.Y1+.85,pz-.11,pz+.11,SteelDark);return;
            }
            if(Has(id,"-porch")){family+="-porch";int side=b.Cx<0?0:1;if(b.Sx>b.Sz)side=3;Along(b,side,out double u0,out double u1);for(double y=b.Y0+.45;y<b.Y1-.2;y+=.55)Face(b,side,u0,u1,y,y+.06,TimberDark,.02,.012);Cap(b,0,.1);return;}
            if(Has(id,"-stove")){family+="-stove";foreach(int side in new[]{0,1}){Face(b,side,b.Z0+.15,b.Z1-.15,b.Y0+.3,b.Y0+.7,NativePolarLooks.VentHot,.02,.02);}return;}
            if(Has(id,"-bunk")||Has(id,"-bench")){family+="-bench";Face(b,4,b.X0,b.X1,b.Z0,b.Z1,Has(id,"-bunk")?Red:SteelDark,.03,.012);return;}
            if(Has(id,"-genset")){family+="-genset";int side=b.Cx<0?0:1;Face(b,side,b.Z0+.3,b.Z0+.7,b.Y0+.7,b.Y0+.95,Lamp,.02,.02);Face(b,side,b.Z1-1.2,b.Z1-.3,b.Y0+.3,b.Y0+.9,SteelDark,.03,.02);return;}
            if(Has(id,"-screen")){family+="-screen";return;}
            if(Has(id,"-lintel")){family+="-lintel";int side=b.Sx<b.Sz?(OutX(b)>0?0:1):(OutZ(b)>0?2:3);Along(b,side,out double u0,out double u1);Face(b,side,(u0+u1)/2-.3,(u0+u1)/2+.3,b.Y0+.25,b.Y0+.45,Lamp,.04,.04);return;}
            if(!Has(id,"-wall"))return;family+="-wall";
            // a wall: its outer face takes a skirt and, where it is long enough, lit windows
            bool alongZ=b.Sx<b.Sz;int outer=alongZ?(OutX(b)>0?0:1):(OutZ(b)>0?2:3);Along(b,outer,out double a0,out double a1);double len=a1-a0;
            Face(b,outer,a0,a1,b.Y0,b.Y0+.35,frame,.03,.02);
            // the cabins' back walls face the ridge a metre off: no windows there
            if(cabin&&alongZ&&Math.Abs(b.Cx)>30)return;
            if(len>=2.4){int n=len>=7?3:len>=4.4?2:1;double step=len/n;for(int i=0;i<n;i++){double c=a0+step*(i+.5);Pane(b,outer,c-.55,c+.55,b.Y0+1.25,b.Y0+2.15,i+outer*7);}}
        }
        // Which way a building's wall faces out: away from its building's middle (the cabins at x ±26.8, the lab and the shed at
        // x ±26.6 or ±27; north of z 10 the lab, south of -10 the shed).
        private static double OutX(Bx b)=>b.Cx-(b.Cx<0?-26.8:26.8);
        private static double OutZ(Bx b)=>b.Cz-(b.Cz>10?17.6:b.Cz<-10?-18.0:0);
    }

    /// <summary>
    /// Polaris Station's rig for one frame (docs/arenas/POLAR.md): the set pieces that move all match long and the hazards' fittings,
    /// as box parts for NativeBlockLit instances and, tagged with the art's slots (<see cref="Slot"/>, <see cref="Level"/>), as the
    /// places Core NativePolarArt poses the modelled rig at. Every pose is a pure function of the replicated state and the field
    /// clock (Core NativePolarLooks), so it stands the same on every client and in the killcam:
    /// <list type="bullet">
    /// <item>The great dish on its yoke over the observatory, turning and tilting (overhead: its rim never comes under
    /// NativePolarLooks.DishLowest; the pedestal's top under the yoke is capped).</item>
    /// <item>The icebreaker's searchlight on the wheelhouse roof (past the north bound), panning.</item>
    /// <item>The weather balloon and its sonde, let go from behind a ridge and rising across the field.</item>
    /// <item>The weather masts' anemometers, spinning.</item>
    /// <item>Each steam vent's lamp ring on its grate: dark, amber and beating through the warning, white-hot through the burst.</item>
    /// <item>Where a sheet of thin ice has gone: broken floes bobbing in the open water (all under NativePolarKit.BelowY).</item>
    /// </list>
    /// Roles (<see cref="Role"/>): Overhead, Floor (a thin plate on a floor), Attached (on its block), Below. Allocation free per frame.
    /// </summary>
    public sealed class NativePolarRig
    {
        public const int Capacity=420;public const int Overhead=0,Floor=1,Attached=2,Below=3;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>For part i: its role, the art's rig slot it stands for (-1: none) and a level (a vent lamp's heat 0..2; the
        /// dish's elevation; a floe's or a balloon's scale).</summary>
        public readonly int[] Role=new int[Capacity],Slot=new int[Capacity];public readonly double[] Level=new double[Capacity];
        private readonly double[] masts;private readonly int mastCount;
        /// <summary>The frame's sheets: how far each has cracked (0..1) and sunk (0..1), by segment index.</summary>
        public readonly double[] Crack=new double[8],Sink=new double[8];
        private int roleNow=Overhead,slotNow=-1;private double levelNow;
        public NativePolarRig(NativeFactoryKit.Looks looks,ContentMap map)
        {
            Parts=new NativeFactoryKit.Writer(Capacity,false,looks);var m=new List<double>();
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())if(k?.id!=null&&k.id.StartsWith("polar-mast-",StringComparison.Ordinal)){m.Add(k.pos.x);m.Add(k.pos.y+k.size.y/2);m.Add(k.pos.z);}
            masts=m.ToArray();mastCount=masts.Length/3;
        }
        private void Put(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Role[n]=roleNow;Slot[n]=slotNow;Level[n]=levelNow;slotNow=-1;}}
        private void Begin(int role,int slot=-1,double level=0){roleNow=role;slotNow=slot;levelNow=level;}
        private static double H(int a,int b=0,int c=0)=>NativePolarLooks.Hash(a,b,c);

        /// <summary>A point of the reflector's own frame (boresight +z, up +y, origin the elevation bearing) in the world at `t`.</summary>
        public static void DishPoint(double t,double lx,double ly,double lz,out double x,out double y,out double z)
        {NativePolarLooks.DishAim(t).Rotate(lx,ly,lz,out x,out y,out z);x+=NativePolarLooks.DishX;y+=NativePolarLooks.DishY;z+=NativePolarLooks.DishZ;}
        /// <summary>The reflector's surface: how far along the boresight (from the bearing) the paraboloid stands at radius r.</summary>
        public static double DishSurface(double r){double k=r/NativePolarLooks.DishRadius;return -NativePolarLooks.DishBack+NativePolarLooks.DishDepth*k*k;}
        /// <summary>The petals round the reflector and the rings along each.</summary>
        public const int Petals=16,Rings=3;
        /// <summary>The floes that bob where a sheet has gone: how many a sheet, and floe `i` of sheet `s` at render time `t`
        /// (its centre, size, yaw and tilt), inside the sheet's own rectangle.</summary>
        public const int Floes=5;
        public static void FloeAt(StructureDefinition d,int s,int i,double t,out double x,out double y,out double z,out double size,out double yaw,out double tilt)
        {
            double cx=d.crush.center.x,cz=d.crush.center.z,hx=d.crush.size.x/2-.7,hz=d.crush.size.z/2-.6;
            x=cx+(H(s,i,1)*2-1)*hx;z=cz+(H(s,i,2)*2-1)*hz;size=.45+H(s,i,3)*.5;yaw=H(s,i,4)*6.28+Math.Sin(t*.21+i)*.25;
            y=NativePolarLooks.Sea-.02+Math.Sin(t*1.1+i*1.7+s)*.035;tilt=Math.Sin(t*.8+i*2.3+s)*.07;
        }

        /// <summary>Fills <see cref="Parts"/> for the hazards and the replicated state (round, phase, FightAt; each segment's state
        /// and t0, null before the structures exist) at field clock `now`.</summary>
        public void Build(ContentMap map,int[] segState,float[] segT0,int round,NativePhase phase,double fightAt,double now)
        {
            Parts.Clear();double clock=NativeFactoryHazards.Clock(phase,fightAt,now),t=now;var hazards=map?.hazards;var segs=map?.segments;
            Dish(t);Searchlight(t);Balloon(t);Cups(t);
            for(int i=0;i<(hazards?.Length??0);i++){var h=hazards[i];if(h?.kind==NativeFactoryHazards.Vent)VentRig(h,round,clock,t);}
            for(int s=0;s<Crack.Length;s++)Crack[s]=Sink[s]=0;
            for(int s=0;s<(segs?.Length??0)&&s<Crack.Length;s++)
            {
                var d=segs[s];int st=segState!=null&&s<segState.Length?segState[s]:0;double t0=segT0!=null&&s<segT0.Length?segT0[s]:0;
                NativePolarLooks.SheetAt(d,st,t0,now,out Crack[s],out Sink[s],out bool open,out _);if(!open||Sink[s]<.6)continue;
                for(int i=0;i<Floes;i++)
                {FloeAt(d,s,i,t,out double x,out double y,out double z,out double size,out double yaw,out double tilt);Begin(Below,NativePolarArt.Floe,size);Put(x,y-.14,z,size,.12,size*.8,FactoryQ.Axis(0,1,0,yaw)*FactoryQ.Axis(1,0,0,tilt),NativePolarLooks.Ice,false);}
            }
        }
        private void Dish(double t)
        {
            var q=NativePolarLooks.DishAim(t);var yaw=NativePolarLooks.YokeTurn(t);double el=NativePolarLooks.DishElevation(t);
            double X=NativePolarLooks.DishX,Y=NativePolarLooks.DishY,Z=NativePolarLooks.DishZ,R=NativePolarLooks.DishRadius;
            // the yoke: a turntable on the pedestal's top, two arms up to the bearing, the cross beam
            Begin(Overhead,NativePolarArt.Yoke);Put(X,Y-2.65,Z,2.3,.22,2.3,yaw,NativePolarLooks.SteelDark);
            for(int s=-1;s<=1;s+=2){yaw.Rotate(s*2.05,0,0,out double ax,out _,out double az);Begin(Overhead);Put(X+ax,Y-1.2,Z+az,.24,1.45,.5,yaw,NativePolarLooks.Steel);}
            Begin(Overhead);Put(X,Y,Z,2.3,.2,.2,q,NativePolarLooks.SteelDark);
            // the reflector: petals along the paraboloid, the hub, the back's ring, the feed on its legs
            Begin(Overhead,NativePolarArt.Dish,el);Put(X,Y,Z,.9,.9,.3,q,NativePolarLooks.Steel);
            for(int p=0;p<Petals;p++)
            {
                double a=(p+.5)*2*Math.PI/Petals,ca=Math.Cos(a),sa=Math.Sin(a);
                for(int r=0;r<Rings;r++)
                {
                    double r0=.8+(R-.8)*r/Rings,r1=.8+(R-.8)*(r+1)/Rings,rm=(r0+r1)/2,zm=(DishSurface(r0)+DishSurface(r1))/2,slope=Math.Atan2(DishSurface(r1)-DishSurface(r0),r1-r0);
                    DishPoint(t,ca*rm,sa*rm,zm,out double px,out double py,out double pz);
                    // the petal's own turn: about the boresight to its spoke, then tipped along the paraboloid
                    var pq=q*FactoryQ.Axis(0,0,1,a)*FactoryQ.Axis(0,1,0,-slope);Begin(Overhead);
                    Put(px,py,pz,(r1-r0)/2/Math.Cos(slope)+.02,Math.PI*rm/Petals*1.02,.05,pq,(p+r)%2==0?NativePolarLooks.Dish:NativePolarLooks.White,r==Rings-1);
                }
            }
            for(int k=0;k<3;k++)
            {
                double a=k*2*Math.PI/3+Math.PI/2;DishPoint(t,Math.Cos(a)*R*.62,Math.Sin(a)*R*.62,DishSurface(R*.62),out double x0,out double y0,out double z0);
                DishPoint(t,0,0,NativePolarLooks.DishFeed-NativePolarLooks.DishBack,out double x1,out double y1,out double z1);Begin(Overhead);int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,.14,.14,NativePolarLooks.SteelDark,false);if(Parts.Count>n){Role[n]=Overhead;Slot[n]=-1;Level[n]=0;}
            }
            DishPoint(t,0,0,NativePolarLooks.DishFeed-NativePolarLooks.DishBack,out double fx,out double fy,out double fz);Begin(Overhead);Put(fx,fy,fz,.32,.32,.45,q,NativePolarLooks.SteelDark);
            DishPoint(t,0,0,NativePolarLooks.DishFeed-NativePolarLooks.DishBack+.5,out fx,out fy,out fz);Begin(Overhead);Put(fx,fy,fz,.12,.12,.08,q,NativePolarLooks.Beacon,false);
        }
        private void Searchlight(double t)
        {
            NativePolarLooks.Searchlight(t,out double x,out double y,out double z);double dx=x-NativePolarLooks.LampX,dy=y-NativePolarLooks.LampY,dz=z-NativePolarLooks.LampZ,l=Math.Sqrt(dx*dx+dy*dy+dz*dz);
            var q=FactoryQ.YawPitch(Math.Atan2(dx,dz),Math.Asin(Math.Max(-1,Math.Min(1,-dy/l))));
            Begin(Overhead,NativePolarArt.Searchlight);Put(NativePolarLooks.LampX,NativePolarLooks.LampY,NativePolarLooks.LampZ,.34,.34,.42,q,NativePolarLooks.SteelDark);
            q.Rotate(0,0,.44,out double ox,out double oy,out double oz);Begin(Overhead);Put(NativePolarLooks.LampX+ox,NativePolarLooks.LampY+oy,NativePolarLooks.LampZ+oz,.28,.28,.03,q,NativePolarLooks.Lamp,false);
        }
        private void Balloon(double t)
        {
            if(!NativePolarLooks.BalloonAt(t,out double x,out double y,out double z,out double scale,out double sway))return;double r=NativePolarLooks.BalloonRadius*scale;
            Begin(Overhead,NativePolarArt.Balloon,scale);Put(x,y,z,r*.82,r,r*.82,FactoryQ.Axis(0,1,0,t*.2),NativePolarLooks.Balloon);
            Begin(Overhead);Put(x,y,z,r*.82,r*.82,r*.82,FactoryQ.Axis(0,1,0,t*.2+.785),NativePolarLooks.Balloon,false);
            double sx=x+Math.Sin(sway)*NativePolarLooks.SondeDrop,sy=y-r-Math.Cos(sway)*NativePolarLooks.SondeDrop,sz=z;
            Begin(Overhead);int n=Parts.Count;Parts.Bar(x,y-r,z,sx,sy,sz,.03,.03,NativePolarLooks.SteelDark,false);if(Parts.Count>n){Role[n]=Overhead;Slot[n]=-1;Level[n]=0;}
            Begin(Overhead,NativePolarArt.Sonde,sway);Put(sx,sy-.18,sz,.16,.2,.16,FactoryQ.Axis(0,0,1,sway),NativePolarLooks.Orange,false);
            Begin(Overhead);Put(sx,sy-.44,sz,.06,.06,.06,FactoryQ.Identity,NativePolarLooks.Beacon,false);
        }
        /// <summary>The anemometers' spin (rad) at `t`: gusty.</summary>
        public static double CupSpin(double t,int mast)=>t*5.2+1.4*Math.Sin(t*.33+mast*2.1);
        private void Cups(double t)
        {
            for(int m=0;m<mastCount;m++)
            {
                double x=masts[m*3],y=masts[m*3+1]+.14,z=masts[m*3+2],a=CupSpin(t,m);var q=FactoryQ.Axis(0,1,0,a);
                Begin(Attached,NativePolarArt.Cups,a);Put(x,y,z,.34,.03,.03,q,NativePolarLooks.SteelDark,false);Begin(Attached);Put(x,y,z,.03,.03,.34,q,NativePolarLooks.SteelDark,false);
                for(int k=0;k<4;k++){double b=a+k*Math.PI/2;Begin(Attached);Put(x+Math.Cos(b)*.3,y,z-Math.Sin(b)*.3,.07,.07,.07,q,k%2==0?NativePolarLooks.Red:NativePolarLooks.White,false);}
            }
        }
        /// <summary>A vent's lamp ring: its heat 0 (idle) .. 1 (the warning's end, beating) .. 2 (the burst).</summary>
        public static double VentHeat(double warn,double burst,double t)=>burst>0?1+burst:warn>0?warn*(.55+.45*(.5+.5*Math.Sin(t*(8+10*warn)))):0;
        private void VentRig(MapHazard h,int round,double clock,double t)
        {
            NativePolarLooks.VentAt(h,round,clock,out double warn,out double burst);double heat=VentHeat(warn,burst,t);double x=h.a.x,y=h.a.y,z=h.a.z,r=h.size.x/2+.16;
            int kind=heat>1?NativePolarLooks.Lamp:heat>.02?NativePolarLooks.VentHot:NativePolarLooks.SteelDark;
            // four lamps at the grate's corners (thin plates on the floor), lit by the heat
            for(int k=0;k<4;k++){double cx=x+((k&1)==0?-r:r),cz=z+((k&2)==0?-r:r);Begin(Floor,NativePolarArt.VentLamp,heat);Put(cx,y+.03,cz,.12,.03,.12,FactoryQ.Identity,kind,false);}
        }
    }
}
