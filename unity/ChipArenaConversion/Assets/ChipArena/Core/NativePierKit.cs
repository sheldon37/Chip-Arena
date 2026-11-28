using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pier 9's static kit (docs/arenas/HARBOR.md "The look"): what NativeBlockLit instances draw on the map's dressed blocks by
    /// id stem when the stage bank is not on the machine (with it, the modelled art draws instead: docs/arenas/STAGE-ART.md).
    /// Three kinds of part:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/> the block): the boards' seams, the track's rails and sleepers, the halls' battens,
    /// cornices and bulbs, the kiosk's lit glass, the sign's letters in bulbs, the stalls' shelves, prizes, striped roofs and
    /// valances, the booths' stripes, the rides' mirrors and fascias, the masts' bands and lamps, planters, crates, carts, rails'
    /// posts and caps, stair nosings, the drop squares on the boards. Every attached part stays inside its block grown by
    /// <see cref="Proud"/> (the stage rule: what looks like cover is cover).</item>
    /// <item>Thin (<see cref="Parent"/> -2): the planters' lamp posts, slender and standing on their blocks.</item>
    /// <item>Free (<see cref="Parent"/> -1): what hangs overhead, at least <see cref="Clearance"/> m over every standable top
    /// under it (the turrets' domes, the carousel's canopy and the band stand's dome on their columns, the lamps' globes, the
    /// strings of bulbs, the signals' crossbucks, the masts' crowns), and what stands where no floor is: the piles and braces under
    /// the boards (below every floor, clear of the catwalk) and the track running on past both edges.</item>
    /// </list>
    /// Deterministic; allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativePierKit
    {
        public const float Proud=.25f;public const double Clearance=2.0,Decal=.012;public const int Free=-1,Thin=-2;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();
        private readonly List<int> parents=new List<int>();private int current=Free;
        public int Count=>Parts.Count;
        private NativePierKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(2048,true,looks);}
        private void Track(int before){for(int i=before;i<Parts.Count;i++)parents.Add(current);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);Track(n);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow);Track(n);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);Track(n);}
        private static double H(int a,int b=0,int c=0)=>NativePierLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Ends(string id,string p)=>id!=null&&id.EndsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        internal struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;public double Cy=>(Y0+Y1)/2;}
        private const int Timber=NativePierLooks.Timber,TimberDark=NativePierLooks.TimberDark,Cream=NativePierLooks.Cream,Red=NativePierLooks.Red,Teal=NativePierLooks.Teal,Gold=NativePierLooks.Gold,Iron=NativePierLooks.Iron,
            Brass=NativePierLooks.Brass,Glass=NativePierLooks.Glass,Bulb=NativePierLooks.Bulb,Pink=NativePierLooks.BulbPink,Cyan=NativePierLooks.BulbCyan,Canvas=NativePierLooks.Canvas,Mirror=NativePierLooks.Mirror,
            Navy=NativePierLooks.Navy,Dark=NativePierLooks.Dark,Rail=NativePierLooks.Rail,BulbRed=NativePierLooks.BulbRed,Lilac=NativePierLooks.Lilac,White=NativePierLooks.White;
        private Bx[] all=Array.Empty<Bx>();

        /// <summary>The map's blocks as boxes (rotated blocks left out; Pier 9 has none).</summary>
        internal static Bx[] Boxes(ContentMap map)
        {
            var l=new List<Bx>();
            for(int i=0;i<(map?.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                l.Add(new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i});
            }
            return l.ToArray();
        }
        /// <summary>The map's kit for its look (null for another map).</summary>
        public static NativePierKit Build(ContentMap map,MapLook look)
        {
            if(!NativePierLooks.Active(map?.id))return null;var k=new NativePierKit(NativePierLooks.Resolve(look?.kitLooks));k.all=Boxes(map);
            foreach(var x in k.all){k.current=x.Index;k.Dress(x);}
            k.current=Free;k.Overhead(map);k.Under();k.Beyond(map);
            k.Parent=k.parents.ToArray();return k;
        }

        // ---------------- face helpers (a hair proud) ----------------
        /// <summary>A plate on face `side` (0 +x, 1 -x, 2 +z, 3 -z, 4 top, 5 bottom): u0..u1 along it (z on ±x faces, x on ±z faces and
        /// the top), v0..v1 up it (y; z on the top), `t` thick from `o` proud.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,double t=Decal,double o=.004,bool shadow=false)
        {
            if(!(u1>u0&&v1>v0))return;
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow);break;
                case 4:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow);break;default:Box(u0,u1,b.Y0-o,b.Y0-o+t,v0,v1,kind,shadow);break;
            }
        }
        private static void Along(Bx b,int side,out double u0,out double u1){if(side<=1){u0=b.Z0;u1=b.Z1;}else{u0=b.X0;u1=b.X1;}}
        /// <summary>A row of bulbs along face `side` at height y, `step` apart, standing `o` proud; the colours run in threes.</summary>
        private void Bulbs(Bx b,int side,double y,double step,double o,int a,int c=-1,int d=-1)
        {
            Along(b,side,out double u0,out double u1);int n=Math.Max(1,(int)Math.Floor((u1-u0)/step));double pad=(u1-u0-(n-1)*step)/2;
            for(int i=0;i<n;i++){double u=u0+pad+i*step;int kind=i%3==1&&c>=0?c:i%3==2&&d>=0?d:a;Face(b,side,u-.04,u+.04,y-.04,y+.04,kind,.07,o+.07);}
        }
        private Bx? Find(string id){foreach(var x in all)if(x.Id==id)return x;return null;}
        /// <summary>A decal on whichever floor lies under each metre of it (top at `y`): each piece belongs to its own block.</summary>
        private void Floor(double x0,double x1,double z0,double z1,double y,int kind,double o=.004)
        {
            int keep=current;
            for(double x=x0;x<x1-1e-6;x+=1)for(double z=z0;z<z1-1e-6;z+=1)
            {
                double xa=x,xb=Math.Min(x1,x+1),za=z,zb=Math.Min(z1,z+1),cx=(xa+xb)/2,cz=(za+zb)/2;
                foreach(var f in all)if(Math.Abs(f.Y1-y)<1e-3&&cx>f.X0&&cx<f.X1&&cz>f.Z0&&cz<f.Z1){current=f.Index;Box(Math.Max(xa,f.X0),Math.Min(xb,f.X1),y+o-Decal,y+o,Math.Max(za,f.Z0),Math.Min(zb,f.Z1),kind,false);break;}
            }
            current=keep;
        }

        private void Dress(Bx b)
        {
            string id=b.Id;
            if(id=="harbor-track"){TrackBed(b);return;}
            if(Starts(id,"harbor-deck-")||Starts(id,"harbor-bridge-")||Starts(id,"harbor-landing-")||Starts(id,"harbor-catwalk-")||Starts(id,"harbor-crown-")||Ends(id,"-floor")){Boards(b);return;}
            if(Starts(id,"harbor-hall-")){Wall(b);return;}
            if(Starts(id,"harbor-kiosk-")){Kiosk(b);return;}
            if(Starts(id,"harbor-turret-")){Turret(b);return;}
            if(Starts(id,"harbor-sign-")){Sign(b);return;}
            if(Starts(id,"harbor-stall-")){Stall(b);return;}
            if(Starts(id,"harbor-booth-")){Booth(b);return;}
            if(Starts(id,"harbor-ride-")){Ride(b);return;}
            if(Starts(id,"harbor-tower-")){Mast(b);return;}
            if(Starts(id,"harbor-planter-")){Planter(b);return;}
            if(Starts(id,"harbor-crate-")){Crate(b);return;}
            if(Starts(id,"harbor-cart-")){Cart(b);return;}
            if(Starts(id,"harbor-signal-")){Signal(b);return;}
            if(Starts(id,"harbor-rail-")){Fence(b);return;}
            if(Starts(id,"harbor-stair-")){Step(b);return;}
            if(id=="harbor-caisson"||Starts(id,"harbor-pile-")){Pile(b);return;}
        }

        // ---------------- the boards ----------------
        private void Boards(Bx b)
        {
            // the planks run across the pier: a dark seam every .75 m, off the world's grid so neighbouring plates line up
            for(double z=Math.Ceiling(b.Z0/.75)*.75;z<b.Z1-.05;z+=.75)if(z>b.Z0+.05)Face(b,4,b.X0+.02,b.X1-.02,z-.012,z+.012,TimberDark,.006,.002);
        }
        private void TrackBed(Bx b)
        {
            // the boarded crossing: sleepers under two steel rails, the lane's gold edge lines
            for(double x=b.X0+.5;x<b.X1;x+=1)Face(b,4,x-.11,x+.11,-1.15,1.15,TimberDark,.008,.002);
            foreach(double z in new[]{-.72,.72}){Box(b.X0,b.X1,b.Y1+.01,b.Y1+NativePierLooks.RailY,z-.04,z+.04,Rail,false);}
            foreach(double z in new[]{-1.5,1.5})Face(b,4,b.X0,b.X1,z-.05,z+.05,Gold,.008,.003);
            foreach(int side in new[]{2,3})Face(b,side,b.X0,b.X1,b.Y0+.04,b.Y1-.04,TimberDark,.06,.05,true);
        }

        // ---------------- the halls ----------------
        private void Wall(Bx b)
        {
            bool alongX=b.Sx>=b.Sz;int s0=alongX?2:0,s1=alongX?3:1;double top=b.Y1;
            if(b.Y0>.5)
            {   // an arch head or a lintel: a gold band and its bulbs
                foreach(int side in new[]{s0,s1}){Along(b,side,out double a0,out double a1);Face(b,side,a0,a1,b.Y0+.02,b.Y0+.16,Gold,.05,.05,true);Bulbs(b,side,b.Y0+.3,.4,.03,Bulb,Pink);}
                return;
            }
            foreach(int side in new[]{s0,s1})
            {
                Along(b,side,out double u0,out double u1);
                Face(b,side,u0,u1,b.Y0,b.Y0+.4,Navy,.05,.05,true);
                for(double u=u0+.3;u<u1-.1;u+=1.25)Face(b,side,u-.09,u+.09,b.Y0+.4,top-.36,Red,.04,.04,true);
                Face(b,side,u0,u1,top-.36,top-.2,Gold,.09,.09,true);Bulbs(b,side,top-.5,.6,.03,Bulb,Bulb,Cyan);
            }
        }
        private void Kiosk(Bx b)
        {
            // the fortune teller's cabinet: lit glass in gold frames on both long faces, a navy plinth and a bulb crown
            foreach(int side in new[]{2,3})
            {
                Face(b,side,b.X0,b.X1,b.Y0,b.Y0+.7,Navy,.05,.05,true);Face(b,side,b.X0,b.X1,b.Y1-.5,b.Y1-.3,Gold,.08,.08,true);
                for(int k=0;k<3;k++){double c=b.X0+b.Sx*(k+.5)/3;Face(b,side,c-.85,c+.85,b.Y0+.9,b.Y1-.75,Gold,.03,.03);Face(b,side,c-.75,c+.75,b.Y0+1.0,b.Y1-.85,Glass,.02,.05);}
                Bulbs(b,side,b.Y1-.14,.45,.03,Pink,Bulb,Cyan);
            }
        }
        private void Turret(Bx b)
        {
            foreach(int side in new[]{0,1,2,3})
            {
                Along(b,side,out double u0,out double u1);for(double u=u0+.25;u<u1-.1;u+=.5)Face(b,side,u-.11,u+.11,b.Y0+.3,b.Y1-.5,Red,.03,.03);
                Face(b,side,u0,u1,b.Y0,b.Y0+.3,Navy,.05,.05,true);Face(b,side,u0,u1,b.Y1-.5,b.Y1-.2,Gold,.08,.08,true);Bulbs(b,side,b.Y1-.62,.4,.03,Bulb,Pink);
                Face(b,side,(u0+u1)/2-.35,(u0+u1)/2+.35,b.Y0+1.6,b.Y0+2.9,Glass,.02,.05);
            }
        }
        private static readonly string[] Font={"11110100011000111110100001000010000","11111001000010000100001000010011111","11111100001000011110100001000011111","11110100011000111110101001001010001","00000000000000000000000000000000000","01110100011000101111000010000101110"};
        private void Sign(Bx b)
        {
            // PIER 9 in bulbs on the face toward the midway, in a gold frame with a chasing border
            int side=b.Cz<0?2:3;double dir=b.Cz<0?-1:1,pitch=.17,w=6*6*pitch-pitch,x0=b.Cx-dir*w/2,ytop=b.Y1-.55;
            foreach(int s in new[]{2,3}){Face(b,s,b.X0,b.X1,b.Y0,b.Y0+.12,Gold,.05,.05,true);Face(b,s,b.X0,b.X1,b.Y1-.12,b.Y1,Gold,.05,.05,true);}
            for(int g=0;g<6;g++)for(int r=0;r<7;r++)for(int c=0;c<5;c++)
            {
                if(Font[g][r*5+c]!='1')continue;double x=x0+dir*(g*6+c)*pitch,y=ytop-r*pitch;Face(b,side,x-.055,x+.055,y-.055,y+.055,g==5?Pink:Bulb,.07,.1);
            }
            Bulbs(b,side,b.Y0+.3,.36,.03,Cyan,Pink,Bulb);Bulbs(b,side,b.Y1-.28,.36,.03,Pink,Bulb,Cyan);
        }

        // ---------------- the midway ----------------
        private void Stall(Bx b)
        {
            string stem=b.Id.Substring(0,b.Id.LastIndexOf('-'));if(Has(b.Id,"-cheek-"))stem=stem.Substring(0,stem.LastIndexOf('-'));
            var back=Find(stem+"-back");var counter=Find(stem+"-counter");if(back==null||counter==null)return;
            // the front: the face of the stall its counter stands at
            bool xFront=back.Value.Sx<back.Value.Sz;int front=xFront?(counter.Value.Cx>back.Value.Cx?0:1):(counter.Value.Cz>back.Value.Cz?2:3);
            if(Ends(b.Id,"-back"))
            {
                // shelves of prizes on the nook's side of the back wall
                Along(b,front,out double u0,out double u1);
                for(int row=0;row<3;row++)
                {
                    double y=b.Y0+.75+row*.62;Face(b,front,u0+.25,u1-.25,y-.04,y,TimberDark,.2,.2,true);
                    for(double u=u0+.5;u<u1-.45;u+=.42){int n=(int)Math.Round(u*10)+row*7;int kind=new[]{Red,Teal,Gold,Lilac,White}[(int)(H(n,row)*5)%5];double s=.1+.05*H(n,row,1);Face(b,front,u-s,u+s,y,y+2*s+.06,kind,.18,.19,false);}
                }
                Bulbs(b,front,b.Y1-.2,.45,.03,Bulb,Pink,Cyan);
            }
            else if(Has(b.Id,"-cheek-"))
            {
                foreach(int side in xFront?new[]{2,3}:new[]{0,1}){Along(b,side,out double u0,out double u1);for(double u=u0+.2;u<u1-.1;u+=.8)Face(b,side,u-.2,u+.2,b.Y0,b.Y1,Red,.02,.02);}
            }
            else if(Ends(b.Id,"-counter"))
            {
                Face(b,4,b.X0-.06,b.X1+.06,b.Z0-.06,b.Z1+.06,Timber,.06,.06,true);
                Along(b,front,out double u0,out double u1);for(double u=u0+.2;u<u1-.1;u+=.8)Face(b,front,u-.2,u+.2,b.Y0,b.Y1-.1,Red,.02,.02);
            }
            else if(Ends(b.Id,"-roof"))
            {
                // the striped awning on the roof's top, the valance and the bulbs along its front edge
                bool wideX=!xFront;double a0=wideX?b.X0:b.Z0,a1=wideX?b.X1:b.Z1;
                for(double u=a0;u<a1-.05;u+=1){double ub=Math.Min(a1,u+.5);if(wideX)Face(b,4,u,ub,b.Z0,b.Z1,Red,.01,.004);else Face(b,4,b.X0,b.X1,u,ub,Red,.01,.004);}
                Along(b,front,out double f0,out double f1);int k=0;for(double u=f0;u<f1-.05;u+=.5,k++)Face(b,front,u+.02,Math.Min(f1,u+.5)-.02,b.Y0-.2,b.Y0,k%2==0?Red:Canvas,.03,.05);
                Bulbs(b,front,b.Cy,.4,.03,Bulb,Pink,Bulb);
            }
        }
        private void Booth(Bx b)
        {
            foreach(int side in new[]{0,1,2,3})
            {
                Along(b,side,out double u0,out double u1);for(double u=u0+.2;u<u1-.1;u+=.8)Face(b,side,u-.2,u+.2,b.Y0,b.Y1-.45,Red,.02,.02);
                Face(b,side,u0,u1,b.Y1-.45,b.Y1-.12,Gold,.1,.1,true);Bulbs(b,side,b.Y1-.56,.4,.03,Bulb,Cyan,Bulb);
                if(side>=2)Face(b,side,(u0+u1)/2-.7,(u0+u1)/2+.7,b.Y0+1.15,b.Y0+2.0,Glass,.02,.05);
            }
        }
        private void Ride(Bx b)
        {
            bool carousel=b.Cz<0;int lamp=carousel?Pink:Cyan;
            if(Has(b.Id,"-base-")){foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0,b.Y1,Gold,.02,.02);}Face(b,4,b.X0+.1,b.X1-.1,b.Z0+.1,b.Z1-.1,carousel?Teal:Navy,.008,.003);return;}
            if(Ends(b.Id,"-drum"))
            {
                foreach(int side in new[]{0,1,2,3})
                {
                    Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0,b.Y0+.3,Gold,.06,.06,true);Face(b,side,u0,u1,b.Y1-.3,b.Y1,Gold,.06,.06,true);
                    for(int k=0;k<3;k++){double c=u0+(u1-u0)*(k+.5)/3;Face(b,side,c-.33,c+.33,b.Y0+.45,b.Y1-.45,carousel?Mirror:Glass,.02,.03);}
                    for(int k=0;k<=3;k++){double c=Math.Max(u0+.06,Math.Min(u1-.06,u0+(u1-u0)*k/3));Face(b,side,c-.06,c+.06,b.Y0+.3,b.Y1-.3,carousel?Gold:Red,.07,.07,true);}
                    Bulbs(b,side,b.Y1-.38,.3,.03,lamp,Bulb);
                }
                return;
            }
            // the upper deck's fascia: a gold band and bulbs on every side (those against a neighbouring deck box are hidden in it)
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0,b.Y1,Gold,.03,.03);Bulbs(b,side,b.Cy,.35,.04,lamp,Bulb,lamp);}
        }
        private void Mast(Bx b)
        {
            // the drop tower's mast: red and white bands, a lattice on each face, a red lamp at every band's corners
            foreach(int side in new[]{0,1,2,3})
            {
                Along(b,side,out double u0,out double u1);
                for(double y=b.Y0;y<b.Y1-.1;y+=4)Face(b,side,u0,u1,y,Math.Min(b.Y1,y+2),Red,.01,.004);
                for(double y=b.Y0+2;y<b.Y1-.1;y+=4)Face(b,side,u0,u1,y,Math.Min(b.Y1,y+2),White,.01,.004);
                for(double y=b.Y0+1;y<b.Y1;y+=2)Face(b,side,u0,u1,y-.05,y+.05,Iron,.05,.06,true);
                Face(b,side,u0,u0+.1,b.Y0,b.Y1,Iron,.06,.07,true);Face(b,side,u1-.1,u1,b.Y0,b.Y1,Iron,.06,.07,true);
            }
            // the drop square on the boards round it
            double r=2.3;Floor(b.Cx-r,b.Cx+r,b.Cz-r,b.Cz-r+.14,0,Gold);Floor(b.Cx-r,b.Cx+r,b.Cz+r-.14,b.Cz+r,0,Gold);Floor(b.Cx-r,b.Cx-r+.14,b.Cz-r+.14,b.Cz+r-.14,0,Gold);Floor(b.Cx+r-.14,b.Cx+r,b.Cz-r+.14,b.Cz+r-.14,0,Gold);
        }
        private void Planter(Bx b)
        {
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0+.12,b.Y0+.24,TimberDark,.03,.03);Face(b,side,u0,u1,b.Y1-.2,b.Y1-.08,TimberDark,.03,.03);}
            Face(b,4,b.X0+.1,b.X1-.1,b.Z0+.1,b.Z1-.1,Dark,.01,.004);
            bool lx=b.Sx>=b.Sz;double len=lx?b.Sx:b.Sz;int n=0;
            for(double u=.3;u<len-.2;u+=.34,n++)
            {
                if(Math.Abs(u-len/2)<.25)continue;double fx=lx?b.X0+u:b.Cx+(H(b.Index,n)-.5)*.4,fz=lx?b.Cz+(H(b.Index,n)-.5)*.4:b.Z0+u,s=.07+.05*H(b.Index,n,1);
                Box(fx-s,fx+s,b.Y1,b.Y1+.1+s,fz-s,fz+s,new[]{Red,Lilac,Gold,White}[n%4],false);
            }
        }
        private void Crate(Bx b)
        {
            foreach(int side in new[]{0,1,2,3})
            {
                Along(b,side,out double u0,out double u1);Face(b,side,u0,u0+.12,b.Y0,b.Y1,TimberDark,.03,.03);Face(b,side,u1-.12,u1,b.Y0,b.Y1,TimberDark,.03,.03);
                for(double y=b.Y0+.02;y<b.Y1-.1;y+=1.15)Face(b,side,u0+.12,u1-.12,y,y+.12,TimberDark,.03,.031);
                Face(b,side,(u0+u1)/2-.22,(u0+u1)/2+.22,b.Y1-.75,b.Y1-.31,Red,.01,.004);
            }
        }
        private void Cart(Bx b)
        {
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);for(double u=u0+.15;u<u1-.1;u+=.6)Face(b,side,u-.15,u+.15,b.Y0+.3,b.Y1-.12,Red,.02,.02);Face(b,side,u0,u1,b.Y1-.12,b.Y1,Brass,.04,.04,true);Face(b,side,u0,u1,b.Y0,b.Y0+.3,Dark,.03,.03);}
            Face(b,4,b.X0+.1,b.X1-.1,b.Z0+.1,b.Z1-.1,Glass,.02,.02);
        }
        private void Signal(Bx b)
        {
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);for(double y=b.Y0;y<1.6;y+=.4)Face(b,side,u0,u1,y,y+.2,White,.01,.004);}
        }
        private void Fence(Bx b)
        {
            bool alongX=b.Sx>=b.Sz;int s0=alongX?2:0,s1=alongX?3:1;
            foreach(int side in new[]{s0,s1})
            {
                Along(b,side,out double u0,out double u1);int n=Math.Max(1,(int)Math.Round((u1-u0)/1.4));
                for(int k=0;k<=n;k++){double u=Math.Max(u0+.06,Math.Min(u1-.06,u0+(u1-u0)*k/n));Face(b,side,u-.06,u+.06,b.Y0,b.Y1,Red,.03,.03);}
                Face(b,side,u0,u1,b.Y0+.5,b.Y0+.58,Gold,.02,.02);
            }
            Face(b,4,b.X0-.03,b.X1+.03,b.Z0-.03,b.Z1+.03,Timber,.05,.05,true);
        }
        private void Step(Bx b)
        {
            bool alongX=b.Sx<b.Sz;double w=.05;
            // a nosing on the tread's lower edge: flights rise to the south in the south hall and down the south pit (north in the north)
            bool hall=Has(b.Id,"-hall-");bool south=b.Cz<0;int edge=alongX?0:(hall?(south?2:3):(south?3:2));
            if(edge==2)Face(b,4,b.X0,b.X1,b.Z1-w,b.Z1,Gold,.01,.004);else if(edge==3)Face(b,4,b.X0,b.X1,b.Z0,b.Z0+w,Gold,.01,.004);
        }
        private void Pile(Bx b)
        {
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);for(double y=b.Y0+.6;y<b.Y1-.2;y+=1.8)Face(b,side,u0,u1,y,y+.16,Iron,.03,.03);Face(b,side,u0,u1,NativePierLooks.Sea-.2,NativePierLooks.Sea+.7,Dark,.02,.02);}
        }

        // ---------------- overhead (free parts) ----------------
        /// <summary>The strings of bulbs: each from one high point to another (x, y, z pairs), sagging `sag` m at its middle. The
        /// north pier's are the south's turned half round.</summary>
        public static readonly double[] Strings={-4.2,8.3,-26, -10,7.9,-15, 1.4,   4.2,8.3,-26, 10,9.0,-15, 1.6,   -10,7.9,-15, -7.4,3.9,-5.7, .8,   10,9.0,-15, 7.5,3.9,-5.7, 1.0,
            -10,7.9,-15, -18,5.2,-21.5, .7,   10,9.0,-15, 18.5,5.4,-21.6, .9};
        /// <summary>The point at parameter u (0..1) of string k of side `turn` (1: the south's own, -1: turned half round).</summary>
        public static void StringAt(int k,int turn,double u,out double x,out double y,out double z)
        {int o=k*7;x=turn*(Strings[o]+(Strings[o+3]-Strings[o])*u);z=turn*(Strings[o+2]+(Strings[o+5]-Strings[o+2])*u);y=Strings[o+1]+(Strings[o+4]-Strings[o+1])*u-Strings[o+6]*4*u*(1-u);}
        private void Overhead(ContentMap map)
        {
            foreach(var b in all)
            {
                if(Starts(b.Id,"harbor-turret-"))
                {
                    // an onion dome in stepped tiers, a gold finial and its lamp
                    double[] r={1.15,1.3,1.2,.95,.62,.3};for(int k=0;k<r.Length;k++){double y=b.Y1+.05+k*.42;Add(b.Cx,y+.21,b.Cz,r[k],.21,r[k],FactoryQ.Identity,k%2==0?Teal:Gold);Add(b.Cx,y+.21,b.Cz,r[k],.2,r[k],FactoryQ.Axis(0,1,0,Math.PI/4),Teal);}
                    Box(b.Cx-.05,b.Cx+.05,b.Y1+2.55,b.Y1+3.5,b.Cz-.05,b.Cz+.05,Gold);Box(b.Cx-.12,b.Cx+.12,b.Y1+3.5,b.Y1+3.74,b.Cz-.12,b.Cz+.12,Bulb,false);
                }
                else if(Starts(b.Id,"harbor-tower-")){Box(b.Cx-.9,b.Cx+.9,b.Y1+.02,b.Y1+.3,b.Cz-.9,b.Cz+.9,Iron);Box(b.Cx-.5,b.Cx+.5,b.Y1+.3,b.Y1+.9,b.Cz-.5,b.Cz+.5,Gold);}
                else if(Starts(b.Id,"harbor-planter-"))
                {
                    // the lamp post (thin, on its planter) and its globe
                    current=Thin;Box(b.Cx-.05,b.Cx+.05,b.Y1,b.Y1+2.25,b.Cz-.05,b.Cz+.05,Iron);current=Free;
                    Box(b.Cx-.3,b.Cx+.3,b.Y1+2.25,b.Y1+2.31,b.Cz-.05,b.Cz+.05,Iron);foreach(int s in new[]{-1,1})Box(b.Cx+s*.3-.14,b.Cx+s*.3+.14,b.Y1+2.31,b.Y1+2.6,b.Cz-.14,b.Cz+.14,Glass,false);
                }
                else if(Starts(b.Id,"harbor-signal-")){foreach(int s in new[]{-1,1})Add(b.Cx,b.Y1-.45,b.Cz+(b.Cz<0?.22:-.22),.6,.07,.02,FactoryQ.Axis(0,0,1,s*.6),White,false);}
                else if(Ends(b.Id,"-drum"))
                {
                    bool carousel=b.Cz<0;double cx=b.Cx,cz=b.Cz,deck=NativePierLooks.RideDeck,eave=NativePierLooks.RideEave;
                    // eight columns from the upper deck to the eave, the eave's ring and its bulbs
                    for(int k=0;k<8;k++)
                    {
                        double a=k*Math.PI/4+Math.PI/8,x=cx+Math.Sin(a)*3.75,z=cz+Math.Cos(a)*3.75;Box(x-.07,x+.07,deck+.02,eave,z-.07,z+.07,carousel?Brass:Cream);
                        double a2=k*Math.PI/4;Add(cx+Math.Sin(a2)*4.1,eave+.15,cz+Math.Cos(a2)*4.1,1.75,.15,.1,FactoryQ.Axis(0,1,0,a2),Gold);
                        for(int j=-3;j<=3;j++){double bx=cx+Math.Sin(a2)*4.22+Math.Cos(a2)*j*.45,bz=cz+Math.Cos(a2)*4.22-Math.Sin(a2)*j*.45;Add(bx,eave+.15,bz,.05,.05,.05,FactoryQ.Identity,(j+k)%2==0?(carousel?Pink:Cyan):Bulb,false);}
                    }
                    if(carousel)
                    {
                        // the canopy: eight striped leaves pitched up to the peak, a gold crown and its lamp
                        for(int k=0;k<8;k++){double a=k*Math.PI/4,run=4.15,rise=1.75,len=Math.Sqrt(run*run+rise*rise),mx=cx+Math.Sin(a)*run/2,mz=cz+Math.Cos(a)*run/2;
                            Add(mx,eave+.3+rise/2,mz,1.72*.52,.04,len/2,FactoryQ.Axis(0,1,0,a)*FactoryQ.Axis(1,0,0,Math.Atan2(rise,run)),k%2==0?Red:Canvas);}
                        Box(cx-.35,cx+.35,eave+1.95,eave+2.4,cz-.35,cz+.35,Gold);Box(cx-.14,cx+.14,eave+2.4,eave+2.7,cz-.14,cz+.14,Bulb,false);
                    }
                    else
                    {
                        // the dome in stepped tiers, its lantern; the star is the rig's
                        double[] r={4.1,3.7,3.1,2.3,1.4};for(int k=0;k<r.Length;k++){double y=eave+.3+k*.46;Add(cx,y+.23,cz,r[k],.23,r[k]*.414,FactoryQ.Identity,Teal);Add(cx,y+.23,cz,r[k]*.414,.226,r[k],FactoryQ.Identity,Teal);
                            Add(cx,y+.23,cz,r[k],.222,r[k]*.414,FactoryQ.Axis(0,1,0,Math.PI/4),Teal);Add(cx,y+.23,cz,r[k]*.414,.218,r[k],FactoryQ.Axis(0,1,0,Math.PI/4),Teal);}
                        Box(cx-.4,cx+.4,eave+2.6,eave+3.0,cz-.4,cz+.4,Gold);Box(cx-.06,cx+.06,eave+3.0,NativePierLooks.StarY-1.0,cz-.06,cz+.06,Gold);
                    }
                }
            }
            // the strings of bulbs
            for(int turn=1;turn>=-1;turn-=2)for(int k=0;k<Strings.Length/7;k++)
            {
                StringAt(k,turn,0,out double px,out double py,out double pz);double length=0;
                for(int i=1;i<=8;i++){StringAt(k,turn,i/8.0,out double x,out double y,out double z);Bar(px,py,pz,x,y,z,.02,Dark,false);length+=Math.Sqrt((x-px)*(x-px)+(y-py)*(y-py)+(z-pz)*(z-pz));px=x;py=y;pz=z;}
                int n=Math.Max(2,(int)(length/.7));for(int i=1;i<n;i++){StringAt(k,turn,i/(double)n,out double x,out double y,out double z);Add(x,y-.07,z,.05,.06,.05,FactoryQ.Identity,new[]{Bulb,Pink,Bulb,Cyan}[i%4],false);}
            }
        }

        // ---------------- under the boards and past the edges (free parts where no floor is) ----------------
        /// <summary>The pile bents under the boards: rows across the pier every 6 m, six piles each.</summary>
        public static readonly double[] PileX={-18,-12,-6,6,12,18},PileZ={-32,-26,-21,-14,-8,8,14,21,26,32};
        private void Under()
        {
            double foot=NativePierLooks.Sea-2.2;
            foreach(double z in PileZ)
            {
                for(int i=0;i<PileX.Length;i++)
                {
                    double x=PileX[i],top=Math.Abs(x)<10&&Math.Abs(z)>25?-1.04:NativePierLooks.Under-.04;
                    Box(x-.22,x+.22,foot,top,z-.22,z+.22,TimberDark);Box(x-.26,x+.26,NativePierLooks.Sea-.3,NativePierLooks.Sea+.6,z-.26,z+.26,Dark,false);
                    if(i+1<PileX.Length&&!(PileX[i]<0&&PileX[i+1]>0)){double x1=PileX[i+1];Bar(x,-1.3,z,x1,-3.6,z,.1,TimberDark,false);Bar(x,-3.6,z,x1,-1.3,z,.1,TimberDark,false);}
                }
                Box(-18.3,-5.7,-1.25,-1.1,z-.15,z+.15,TimberDark,false);Box(5.7,18.3,-1.25,-1.1,z-.15,z+.15,TimberDark,false);
            }
        }
        private void Beyond(ContentMap map)
        {
            // the track on past both edges to the play space's margin (the trestle and the spikes beyond are the backdrop's): bare
            // rails on open sleepers over the water
            var t=Find("harbor-track");if(t==null)return;var b=t.Value;double far=(map.bounds?.maxX??24)+NativeNewStageScenery.Margin;
            foreach(int s in new[]{-1,1})
            {
                double x0=s>0?b.X1+.06:-far,x1=s>0?far:b.X0-.06;
                foreach(double z in new[]{-.72,.72})Box(x0,x1,b.Y1+.01,b.Y1+NativePierLooks.RailY,z-.04,z+.04,Rail,false);
                for(double x=Math.Min(Math.Abs(x0),Math.Abs(x1))+.5;x<far-.1;x+=1)Box(s*x-.1,s*x+.1,b.Y1-.14,b.Y1+.01,-1.1,1.1,TimberDark,false);
                foreach(double z in new[]{-.95,.95})Box(x0,x1,b.Y1-.4,b.Y1-.14,z-.09,z+.09,Iron,false);
            }
        }
    }

    /// <summary>
    /// Pier 9's rig for one frame (docs/arenas/HARBOR.md): the set pieces and the hazards' machines as NativeBlockLit parts, built
    /// from the render clock (the Ferris wheel and its gondolas, the carousel's turntable and its rising and falling mounts, the
    /// band stand's dolls and star, the boats at their moorings) and from the replicated hazard state (the coaster's train on its
    /// track, the crossing's wig-wag lamps and the lane's lamps; the drop towers' gondolas and beacons). Each part that a modelled
    /// rig prop stands for is tagged with the art's slot (<see cref="Slot"/>; Core NativePierArt poses the models from the tagged
    /// parts), so the box rig and the art can never drift apart. Allocation free per frame; deterministic.
    /// </summary>
    public sealed class NativePierRig
    {
        public const int Capacity=1400;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>For part i: its rig slot (-1: the art has no piece of its own for it), a level (a lamp's light 0..1) and its kit kind.</summary>
        public readonly int[] Slot=new int[Capacity],Kind=new int[Capacity];public readonly double[] Level=new double[Capacity];
        private void Put(int slot,double level,double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;Kind[n]=kind;}}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=false)
        {int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);if(Parts.Count>n){Slot[n]=-1;Level[n]=0;Kind[n]=kind;}}
        private readonly double[] signals;private readonly int signalCount;
        /// <summary>The render clock of the last Build (the art's chases run on it).</summary>
        public double Render{get;private set;}
        /// <summary>The coaster's telegraph (0 idle, 0..1 through the warning, 1 in the danger and the pass) and each drop tower's
        /// (index = its order among the map's press hazards), for the art's lamps and the effects.</summary>
        public double CoasterLevel{get;private set;}public bool CoasterDanger{get;private set;}
        public NativePierRig(NativeFactoryKit.Looks looks,ContentMap map)
        {
            Parts=new NativeFactoryKit.Writer(Capacity,false,looks);var s=new List<double>();
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())if(k?.id!=null&&k.id.StartsWith("harbor-signal-",StringComparison.Ordinal)){s.Add(k.pos.x);s.Add(k.pos.z);}
            signals=s.ToArray();signalCount=signals.Length/2;
        }
        public int Signals=>signalCount;
        private const int Timber=NativePierLooks.Timber,TimberDark=NativePierLooks.TimberDark,Cream=NativePierLooks.Cream,Red=NativePierLooks.Red,Teal=NativePierLooks.Teal,Gold=NativePierLooks.Gold,Iron=NativePierLooks.Iron,
            Brass=NativePierLooks.Brass,Glass=NativePierLooks.Glass,Bulb=NativePierLooks.Bulb,Pink=NativePierLooks.BulbPink,Cyan=NativePierLooks.BulbCyan,Canvas=NativePierLooks.Canvas,Navy=NativePierLooks.Navy,
            Dark=NativePierLooks.Dark,Rail=NativePierLooks.Rail,BulbRed=NativePierLooks.BulbRed,Dim=NativePierLooks.BulbDim,Hull=NativePierLooks.Hull,Lilac=NativePierLooks.Lilac,White=NativePierLooks.White;

        private static readonly int[] CabinPaint={Red,Teal,Gold,Lilac},Coats={White,Cream,Teal,Lilac,Gold},DollCoats={Red,Navy,Teal,Lilac};
        /// <summary>The boats at their moorings off the pier (outside the bounds): x, z, heading (rad).</summary>
        public static readonly double[] Boats={31,-21,.5, -30,24,2.4, 34,12,-1.1, -29,-27,3.6};
        /// <summary>Boat k at render time t: its place on the swell, its heading, its pitch and roll.</summary>
        public static void Boat(int k,double t,out double x,out double y,out double z,out double yaw,out double pitch,out double roll)
        {x=Boats[k*3];z=Boats[k*3+1];yaw=Boats[k*3+2]+.08*Math.Sin(t*.21+k);y=NativePierLooks.Sea+.12*Math.Sin(t*.9+k*1.7);pitch=.06*Math.Sin(t*.8+k*2.1);roll=.09*Math.Sin(t*1.1+k);}

        /// <summary>Fills <see cref="Parts"/> for the hazards and the replicated state (round, phase, FightAt) at field clock `now`
        /// and render clock `render`.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();Render=render;CoasterLevel=0;CoasterDanger=false;double clock=NativeFactoryHazards.Clock(phase,fightAt,now);int tower=0;
            Wheel(render);Carousel(render);Band(render);Fleet(render);
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Press)DropRig(h,round,clock,render,tower++);
                else if(NativeCyberCityHazards.IsMaglev(h)){var st=NativeCyberCityHazards.At(h,phase,round,fightAt,now);Coaster(h,NativePierLooks.TrainAt(h,phase,round,fightAt,now));Crossing(h,st,render);}
            }
        }
        private void Wheel(double t)
        {
            double cx=NativePierLooks.WheelX,cy=NativePierLooks.WheelY,cz=NativePierLooks.WheelZ,R=NativePierLooks.WheelR,g=NativePierLooks.WheelGap,a0=NativePierLooks.WheelAngle(t);
            var turn=FactoryQ.Axis(1,0,0,-a0);
            Put(NativePierArt.Wheel,0,cx,cy,cz,g+.5,.8,.8,turn,Gold);
            int n=NativePierLooks.Gondolas*2;int chase=(int)Math.Floor(t*3);
            for(int i=0;i<n;i++)
            {
                double a=a0+i*2*Math.PI/n,b=a0+(i+1)*2*Math.PI/n,y0=cy+R*Math.Sin(a),z0=cz+R*Math.Cos(a),y1=cy+R*Math.Sin(b),z1=cz+R*Math.Cos(b);
                for(int s=-1;s<=1;s+=2){Bar(cx+s*g,y0,z0,cx+s*g,y1,z1,.22,White);if(i%2==0)Bar(cx+s*g,cy,cz,cx+s*g,y0,z0,.1,Rail);
                    Put(-1,0,cx+s*(g+.14),y0,z0,.07,.09,.09,FactoryQ.Identity,((i+chase)%3==0)?Bulb:(i%2==0?Pink:Cyan),false);}
                if(i%2==0)Bar(cx-g,y0,z0,cx+g,y0,z0,.1,Rail);
            }
            for(int i=0;i<NativePierLooks.Gondolas;i++)
            {
                NativePierLooks.Gondola(i,t,out double x,out double y,out double z,out double sway);var q=FactoryQ.Axis(1,0,0,sway);
                // the cabin hangs from its pivot: its middle 1.5 m under it
                q.Rotate(0,-1.5,0,out double ox,out double oy,out double oz);int paint=CabinPaint[i%4];
                Put(NativePierArt.Cabin+i%4,0,x+ox,y+oy,z+oz,.85,.62,.75,q,paint);q.Rotate(0,-.75,0,out ox,out oy,out oz);Put(-1,0,x+ox,y+oy,z+oz,.95,.08,.85,q,Cream);
                q.Rotate(0,-1.45,0,out ox,out oy,out oz);Put(-1,0,x+ox,y+oy,z+oz,.8,.24,.7,q,Glass,false);q.Rotate(0,-.38,0,out ox,out oy,out oz);Put(-1,0,x+ox,y+oy,z+oz,.04,.38,.04,q,Iron,false);
            }
        }
        private void Carousel(double t)
        {
            double cx=NativePierLooks.CarouselX,cz=NativePierLooks.CarouselZ,deck=NativePierLooks.RideDeck,a0=NativePierLooks.CarouselAngle(t);var turn=FactoryQ.Axis(0,1,0,a0);
            // the turntable: a thin round floor on the upper deck (two boxes turned 45 degrees apart) and its mirrored hub
            Put(NativePierArt.Table,0,cx,deck+.05,cz,3.7,.03,1.53,turn,Teal,false);Put(-1,0,cx,deck+.05,cz,1.53,.029,3.7,turn,Teal,false);
            Put(-1,0,cx,deck+.05,cz,3.7,.028,1.53,turn*FactoryQ.Axis(0,1,0,Math.PI/4),Gold,false);Put(-1,0,cx,deck+.05,cz,1.53,.027,3.7,turn*FactoryQ.Axis(0,1,0,Math.PI/4),Gold,false);
            Put(-1,0,cx,deck+1.5,cz,.7,1.4,.7,turn,Gold);Put(-1,0,cx,deck+1.5,cz,.72,1.1,.5,turn,NativePierLooks.Mirror,false);Put(-1,0,cx,deck+1.5,cz,.5,1.1,.72,turn,NativePierLooks.Mirror,false);
            for(int i=0;i<NativePierLooks.Mounts;i++)
            {
                NativePierLooks.Mount(i,t,out double x,out double y,out double z,out double yaw,out _);var q=FactoryQ.Axis(0,1,0,yaw);int coat=Coats[i%5];
                Put(NativePierArt.Mount+i%2,0,x,y,z,.2,.26,.62,q,coat);
                q.Rotate(0,.42,.62,out double ox,out double oy,out double oz);Put(-1,0,x+ox,y+oy,z+oz,.11,.3,.16,q*FactoryQ.Axis(1,0,0,.5),coat);
                q.Rotate(0,.72,.86,out ox,out oy,out oz);Put(-1,0,x+ox,y+oy,z+oz,.1,.12,.24,q,coat);
                for(int s=-1;s<=1;s+=2)for(int f=-1;f<=1;f+=2){q.Rotate(s*.14,-.5,f*.42,out ox,out oy,out oz);Put(-1,0,x+ox,y+oy,z+oz,.05,.26,.05,q*FactoryQ.Axis(1,0,0,f*.35),coat,false);}
                Put(-1,0,x,(deck+NativePierLooks.RideEave)/2+.05,z,.03,(NativePierLooks.RideEave-deck)/2-.06,.03,FactoryQ.Identity,Brass,false);
                q.Rotate(0,.08,-.05,out ox,out oy,out oz);Put(-1,0,x+ox,y+oy,z+oz,.22,.05,.3,q,Red,false);
            }
        }
        private void Band(double t)
        {
            double cx=NativePierLooks.BandX,cz=NativePierLooks.BandZ,a=NativePierLooks.StarAngle(t),y=NativePierLooks.StarY;var q=FactoryQ.Axis(0,1,0,a);
            // the star over the dome: two crossed bars and a diagonal pair, a lit hub, bulbs at the points
            Put(NativePierArt.Star,0,cx,y,cz,.22,.22,.12,q,Gold);
            for(int k=0;k<4;k++){var r=q*FactoryQ.Axis(0,0,1,k*Math.PI/4);Put(-1,0,cx,y,cz,k%2==0?1.3:.9,.07,.05-.004*k,r,Gold,false);
                for(int s=-1;s<=1;s+=2){double l=k%2==0?1.3:.9;r.Rotate(s*l,0,0,out double ox,out double oy,out double oz);Put(-1,0,cx+ox,y+oy,cz+oz,.07,.07,.07,FactoryQ.Identity,k%2==0?Bulb:Cyan,false);}}
            for(int i=0;i<NativePierLooks.Dolls;i++)
            {
                NativePierLooks.Doll(i,t,out double x,out double dy,out double z,out double yaw,out double tilt);var d=FactoryQ.Axis(0,1,0,yaw)*FactoryQ.Axis(0,0,1,tilt);
                Put(NativePierArt.Doll,0,x,dy,z,.14,.3,.11,d,DollCoats[i%4]);d.Rotate(0,.42,0,out double ox,out double oy,out double oz);Put(-1,0,x+ox,dy+oy,z+oz,.1,.11,.1,d,Cream,false);
                d.Rotate(0,.58,0,out ox,out oy,out oz);Put(-1,0,x+ox,dy+oy,z+oz,.12,.05,.12,d,Gold,false);
            }
        }
        private void Fleet(double t)
        {
            for(int k=0;k<Boats.Length/3;k++)
            {
                Boat(k,t,out double x,out double y,out double z,out double yaw,out double pitch,out double roll);var q=FactoryQ.Axis(0,1,0,yaw)*FactoryQ.Axis(1,0,0,pitch)*FactoryQ.Axis(0,0,1,roll);
                Put(NativePierArt.Boat,0,x,y+.25,z,.95,.45,2.6,q,k%2==0?Hull:Navy);q.Rotate(0,.9,-.6,out double ox,out double oy,out double oz);Put(-1,0,x+ox,y+.25+oy,z+oz,.6,.5,.8,q,Cream);
                q.Rotate(0,2.1,.4,out ox,out oy,out oz);Put(-1,0,x+ox,y+.25+oy,z+oz,.03,1.4,.03,q,TimberDark,false);q.Rotate(0,3.4,.4,out ox,out oy,out oz);Put(-1,0,x+ox,y+.25+oy,z+oz,.09,.11,.09,q,Bulb,false);
            }
        }
        private void Coaster(MapHazard h,in NativePierLooks.Train train)
        {
            double half=h.size.x/2;
            for(int j=0;j<NativePierLooks.Cars;j++)
            {
                NativePierLooks.Car(train,j,half,out double x,out double y,out double pitch);var q=FactoryQ.Axis(0,0,1,pitch);bool end=j==0||j==NativePierLooks.Cars-1;
                // an end car's nose points out of the train: the first car's toward -s
                var body=j==0?q*FactoryQ.Axis(0,1,0,Math.PI):q;double wx=h.center.x+x,wz=h.center.z;
                q.Rotate(0,.62,0,out double ox,out double oy,out double oz);
                Put(end?NativePierArt.CarEnd:NativePierArt.Car,0,wx+ox,y+oy,wz+oz,1.2,.42,.78,body,j%2==0?Red:Teal);
                q.Rotate(0,.22,0,out ox,out oy,out oz);Put(-1,0,wx+ox,y+oy,wz+oz,1.05,.1,.86,q,Iron,false);
                for(int s=-1;s<=1;s+=2){q.Rotate(s*.45,1.25,0,out ox,out oy,out oz);Put(-1,0,wx+ox,y+oy,wz+oz,.12,.32,.66,q,Navy,false);}
                q.Rotate(0,1.06,0,out ox,out oy,out oz);Put(-1,0,wx+ox,y+oy,wz+oz,1.22,.04,.8,q,Gold,false);
                if(end){double sgn=j==0?-1:1;q.Rotate(sgn*1.22,.62,0,out ox,out oy,out oz);Put(-1,0,wx+ox,y+oy,wz+oz,.06,.14,.3,q,Bulb,false);}
            }
        }
        private void Crossing(MapHazard h,in NativeHazardState st,double t)
        {
            bool warn=st.Phase==NativeHazardPhase.Warn,danger=st.Phase==NativeHazardPhase.Danger||st.Phase==NativeHazardPhase.Active;
            CoasterLevel=warn?st.Level:danger?1:st.Phase==NativeHazardPhase.Clear?st.Level*.5:0;CoasterDanger=danger;
            // the wig-wag lamps on each signal: dark at rest; alternating through the warning (faster in the danger)
            double beat=Math.Floor(t*(danger?5:2.5));
            for(int i=0;i<signalCount;i++)
            {
                double x=signals[i*2],z=signals[i*2+1];
                for(int s=-1;s<=1;s+=2){bool lit=(warn||danger)&&((int)beat+(s>0?1:0))%2==0;double level=lit?1:st.Phase==NativeHazardPhase.Clear?.2*st.Level:0;Put(NativePierArt.Wigwag,level,x+s*.3,2.35,z,.1,.1,.1,FactoryQ.Identity,lit?BulbRed:Dim,false);}
            }
            // the lane's lamps along both edges: a slow warm run at rest; in the warning they chase toward the train's side; in
            // the danger and the pass they burn red
            double x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2;int dir=st.Direction==0?1:st.Direction;
            for(double x=x0+.625;x<x1;x+=1.25)
            {
                double along=dir>0?(x1-x)/(x1-x0):(x-x0)/(x1-x0),lit;int kind=Bulb;
                switch(st.Phase)
                {
                    case NativeHazardPhase.Warn:lit=.25+.75*Math.Max(0,Math.Sin(t*9-along*18));break;
                    case NativeHazardPhase.Danger:lit=.7+.3*Math.Sin(t*24+x);kind=BulbRed;break;
                    case NativeHazardPhase.Active:lit=1;kind=BulbRed;break;
                    case NativeHazardPhase.Clear:lit=.2+.5*st.Level;kind=BulbRed;break;
                    default:lit=.18+.12*Math.Sin(t*1.3+x*.4);break;
                }
                for(int e=-1;e<=1;e+=2)Put(NativePierArt.LaneLamp,lit,x,.03,h.center.z+e*1.5,.09,.03,.05,FactoryQ.Identity,lit>.45?kind:Dim,false);
            }
        }
        private void DropRig(MapHazard h,int round,double clock,double t,int tower)
        {
            var d=NativePierLooks.DropAt(h,round,clock);double x=h.b.x,z=h.b.z,r=h.size.x/2,y=d.Y;
            // the gondola: a square ring round the mast (a collar, four beams, a seat row on each side with its shoulder bars)
            Put(NativePierArt.Ring,d.Warn,x,y+.35,z,.95,.35,.95,FactoryQ.Identity,Iron);
            for(int s=-1;s<=1;s+=2)
            {
                Put(-1,0,x+s*(r-.45),y+.3,z,.45,.3,r,FactoryQ.Identity,Navy);Put(-1,0,x,y+.3,z+s*(r-.45),r-.9,.3,.45,FactoryQ.Identity,Navy);
                Put(-1,0,x+s*(r-.75),y+1.25,z,.12,.75,r-.25,FactoryQ.Identity,Red);Put(-1,0,x,y+1.25,z+s*(r-.75),r-1.0,.75,.12,FactoryQ.Identity,Red);
                Put(-1,0,x+s*(r-.12),y+.85,z,.05,.05,r-.3,FactoryQ.Identity,Gold,false);Put(-1,0,x,y+.85,z+s*(r-.12),r-.3,.05,.05,FactoryQ.Identity,Gold,false);
                Put(-1,0,x+s*.5,y+1.2,z,.06,.9,.06,FactoryQ.Identity,Iron,false);Put(-1,0,x,y+1.2,z+s*.5,.06,.9,.06,FactoryQ.Identity,Iron,false);
            }
            // the beacon on the mast's crown: slow at rest, flashing faster through the telegraph, steady in the fall
            bool warn=d.Phase==NativeFactoryHazards.Warning,fall=d.Phase==NativeFactoryHazards.Slamming||d.Phase==NativeFactoryHazards.Holding;
            double level=fall?1:warn?(Math.Sin(t*(8+14*d.Warn))>0?1:.15):.25+.2*Math.Sin(t*1.7+tower*2);
            Put(NativePierArt.Beacon,level,x,h.a.y+3.3,z,.3,.3,.3,FactoryQ.Identity,level>.5?BulbRed:Dim,false);
        }
    }
}
