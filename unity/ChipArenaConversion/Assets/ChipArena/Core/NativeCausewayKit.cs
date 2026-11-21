using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Starfall Causeway's static kit (docs/arenas/CAUSEWAY.md "The look"): the carved detail NativeBlockLit instances draw on the
    /// map's dressed blocks by id stem, the sculpted pieces that stand in for their own blocks, and the overhead stones. Three kinds
    /// of part:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/> the block): the deck's star-metal comet channel, gold inlays and compass medallions;
    /// the keel's pilasters, cornice, rune band and lit cracks; the Breach's broken faces with rebar and cold light in the cracks;
    /// the balustrades' plinths, copings, relief panels and crystal finials; the pylons' silver edges, crystal slits and crowns;
    /// the halls' pilasters, string courses, rune-lit niches and the great arch's voussoirs; the altar's rune columns; the
    /// gables' rose windows; the spire's ribs and broken crown; the gates' frames and lanterns; the stairs' gold nosings and the
    /// floating flights' cold undersides. Every attached part stays inside its block grown by <see cref="Proud"/> (the stage
    /// rule: what looks like cover is cover).</item>
    /// <item>Sculpted (<see cref="NativeCausewayLooks.KitBlock"/>: the dressing leaves these blocks' own boxes undrawn while the
    /// kit draws): the star-winged wardens and the watchers holding their stars, the bronze crystal braziers, the fallen column
    /// drums. Each fills its block to within <see cref="Proud"/> of every face.</item>
    /// <item>Overhead: the drifting voussoirs of the Shattered Arch over the Breach, posed each frame by <see cref="NativeCausewayRig"/>,
    /// at least <see cref="Clearance"/> m above every standable top under them.</item>
    /// </list>
    /// Deterministic; allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativeCausewayKit
    {
        public const float Proud=.25f;public const double Clearance=2.0,Decal=.012;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();
        private readonly List<int> parents=new List<int>();private int current=-1;
        public int Count=>Parts.Count;
        private NativeCausewayKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(2048,true,looks);}
        private void Track(int before){for(int i=before;i<Parts.Count;i++)parents.Add(current);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color);Track(n);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow,color);Track(n);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);Track(n);}
        private static double H(int a,int b=0,int c=0)=>NativeCausewayLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;public double Cy=>(Y0+Y1)/2;}
        private const int Stone=NativeCausewayLooks.Stone,StoneDark=NativeCausewayLooks.StoneDark,Trim=NativeCausewayLooks.Trim,Metal=NativeCausewayLooks.Metal,Edge=NativeCausewayLooks.MetalEdge,Rune=NativeCausewayLooks.Rune,
            Cold=NativeCausewayLooks.RuneCold,Crystal=NativeCausewayLooks.Crystal,Violet=NativeCausewayLooks.CrystalViolet,Bronze=NativeCausewayLooks.Bronze,Glass=NativeCausewayLooks.Glass,Statue=NativeCausewayLooks.Statue,
            Dark=NativeCausewayLooks.Dark,Gold=NativeCausewayLooks.Gold;

        /// <summary>The map's kit for its look (null for another map).</summary>
        public static NativeCausewayKit Build(ContentMap map,MapLook look)
        {
            if(!NativeCausewayLooks.Active(map?.id))return null;var k=new NativeCausewayKit(NativeCausewayLooks.Resolve(look?.kitLooks));
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                var x=new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i};
                k.current=i;k.Dress(x);
            }
            k.Parent=k.parents.ToArray();return k;
        }

        // ---------------- face helpers (a hair proud) ----------------
        /// <summary>A plate on face `side` (0 +x, 1 -x, 2 +z, 3 -z, 4 top, 5 bottom): u0..u1 along it (z on ±x faces, x on ±z faces and
        /// the top), v0..v1 up it (y; z on the top), `t` thick from `o` proud.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,double t=Decal,double o=.004,bool shadow=false)
        {
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow);break;
                case 4:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow);break;default:Box(u0,u1,b.Y0-o,b.Y0-o+t,v0,v1,kind,shadow);break;
            }
        }
        private static void Along(Bx b,int side,out double u0,out double u1){if(side<=1){u0=b.Z0;u1=b.Z1;}else{u0=b.X0;u1=b.X1;}}
        private static bool LongX(Bx b)=>b.Sx>=b.Sz;

        private void Dress(Bx b)
        {
            string id=b.Id;
            if(id=="causeway-span-w"||id=="causeway-span-e"){Keel(b);return;}
            if(Starts(id,"causeway-span-")){Wing(b);return;}
            if(id=="causeway-keystone"){Keystone(b);return;}
            if(Starts(id,"causeway-rail-")){Rail(b);return;}
            if(Starts(id,"causeway-pylon-")){Pylon(b);return;}
            if(Starts(id,"causeway-warden-")||Starts(id,"causeway-watcher-")){Guardian(b,Starts(id,"causeway-watcher-"));return;}
            if(Starts(id,"causeway-brazier-")){Brazier(b);return;}
            if(Starts(id,"causeway-fallen-")){Fallen(b);return;}
            if(Starts(id,"causeway-plaza-")||Has(id,"-floor")){Plaza(b);return;}
            if(Starts(id,"causeway-crown-")){Crown(b);return;}
            if(Starts(id,"causeway-hall-")){Wall(b);return;}
            if(Starts(id,"causeway-altar-")){Altar(b);return;}
            if(Starts(id,"causeway-gable-")){Gable(b);return;}
            if(Starts(id,"causeway-spire-")){Spire(b);return;}
            if(Starts(id,"causeway-gate-")){Gate(b);return;}
            if(Starts(id,"causeway-balcony-")){Balcony(b);return;}
            if(Starts(id,"causeway-walk-")){Walk(b);return;}
            if(Starts(id,"causeway-stair-")){Step(b);return;}
        }

        // ---------------- the span ----------------
        private void Keel(Bx b)
        {
            bool west=b.Cx<0;double breach=west?b.X1:b.X0,far=west?b.X0:b.X1;
            // the comet's channel: star-metal edges and a darker bed along z ±1.6, gold ticks every 2.5 m
            foreach(double z in new[]{-1.6,1.6}){Face(b,4,b.X0,b.X1,z-.08,z+.08,Edge);Face(b,4,b.X0,b.X1,z-.03,z+.03,Rune,.016,.006);}
            for(double x=b.X0+1.25;x<b.X1-.5;x+=2.5)foreach(double z in new[]{-1.25,1.25})Face(b,4,x-.06,x+.06,z-.22,z+.22,Gold,.014,.005);
            // flagstone seams: darker joints every 2.5 m across the deck and along it, off the comet's channel
            for(double x=b.X0+2.5;x<b.X1-.3;x+=2.5){Face(b,4,x-.025,x+.025,b.Z0+.05,-1.75,StoneDark,.008,.003);Face(b,4,x-.025,x+.025,1.75,b.Z1-.05,StoneDark,.008,.003);}
            foreach(double z in new[]{-2.6,2.6})Face(b,4,b.X0+.05,b.X1-.05,z-.025,z+.025,StoneDark,.008,.003);
            // the compass medallion at the half's centre: eight rays and a ring
            double cx=(b.X0+b.X1)/2+(west?-1.5:1.5);
            for(int k=0;k<8;k++){double a=k*Math.PI/4,l=k%2==0?2.8:1.8;Add(cx+Math.Sin(a)*l/2,b.Y1+.005,Math.Cos(a)*l/2,.05,.006,l/2,FactoryQ.Axis(0,1,0,a),Gold,false);}
            for(int k=0;k<24;k++){double a=k*Math.PI/12;Add(cx+Math.Sin(a)*1.9,b.Y1+.005,Math.Cos(a)*1.9,.045,.006,.26,FactoryQ.Axis(0,1,0,a+Math.PI/2),Rune,false);}
            // the keel's faces (z ±3.4, y -7..-0.6): pilasters, a cornice under the deck, a rune band, lit cracks
            foreach(int side in new[]{2,3})
            {
                for(double x=b.X0+1.05;x<b.X1-.4;x+=2.1)Face(b,side,x-.21,x+.21,b.Y0+.2,-.95,Stone,.12,.12,true);
                Face(b,side,b.X0,b.X1,-1.0,-.72,Trim,.2,.2,true);Face(b,side,b.X0,b.X1,-3.06,-2.94,Rune,.014,.006);
                for(int c=0;c<3;c++){double x0=b.X0+2+H(b.Index,c,side)*(b.Sx-4),y0=-1.3-H(b.Index,c,side+9)*2.2;for(int s=0;s<4;s++){double x1=x0+(H(b.Index,c*7+s,side)-.5)*1.2,y1=y0-.3-H(b.Index,c*5+s,side)*.3;
                    double zf=side==2?b.Z1+.006:b.Z0-.006;Add((x0+x1)/2,(y0+y1)/2,zf,.025,Math.Sqrt((x1-x0)*(x1-x0)+(y1-y0)*(y1-y0))/2,.012,FactoryQ.Axis(0,0,1,Math.Atan2(-(x1-x0),y1-y0)),Cold,false);x0=x1;y0=y1;}}
            }
            // the Breach's broken face: jagged stones within a hand of it, cold light in the cracks, rebar
            int sd=west?0:1;double sgn=west?1:-1;
            for(int i=0;i<16;i++)
            {
                double y0=b.Y0+H(91+sd,i)*(b.Sy-.4),y1=Math.Min(b.Y1-.02,y0+.4+H(91+sd,i,1)*1.6),z0=b.Z0+H(91+sd,i,2)*(b.Sz-1),z1=Math.Min(b.Z1,z0+.5+H(91+sd,i,3)*1.4),d=.06+H(91+sd,i,4)*.17;
                if(west)Box(breach,breach+d,y0,y1,z0,z1,i%3==0?StoneDark:Stone);else Box(breach-d,breach,y0,y1,z0,z1,i%3==0?StoneDark:Stone);
            }
            for(int c=0;c<5;c++){double z=b.Z0+.4+H(95+sd,c)*(b.Sz-.8),y0=-.3-H(95+sd,c,1)*1.5,y1=y0-1.6-H(95+sd,c,2)*3;Add(breach+sgn*.008,(y0+y1)/2,z,.012,Math.Abs(y1-y0)/2,.03,FactoryQ.Identity,Cold,false);}
            for(int c=0;c<6;c++){double z=b.Z0+.6+H(97+sd,c)*(b.Sz-1.2),y=-.5-H(97+sd,c,1)*5.5;Bar(breach-sgn*.05,y,z,breach+sgn*.22,y+(H(97+sd,c,2)-.5)*.2,z+(H(97+sd,c,3)-.5)*.2,.05,Edge);}
        }
        private void Wing(Bx b)
        {
            bool north=b.Cz>0;int side=north?2:3;double edge=north?b.Z1:b.Z0;
            Face(b,side,b.X0,b.X1,b.Y0+.02,b.Y0+.16,Trim,.08,.08,true);
            for(double x=b.X0+.2;x<b.X1-.1;x+=.4)Face(b,side,x-.08,x+.08,b.Y0-.0,b.Y0+.0+.12,StoneDark,.06,.14,true);
            Face(b,side,b.X0,b.X1,b.Y1-.3,b.Y1-.24,Rune,.012,.006);
            for(double x=b.X0+2.5;x<b.X1-.3;x+=2.5)Face(b,4,x-.025,x+.025,b.Z0+.05,b.Z1-.05,StoneDark,.008,.003);
            double z=north?4.52:-4.52;Face(b,4,b.X0,b.X1,z-.03,z+.03,Gold,.014,.005);
            if(Math.Abs(b.Cx)<12)Face(b,4,b.X0,b.X1,(north?3.4:-3.46),(north?3.46:-3.4),Edge,.014,.005);
        }
        private void Keystone(Bx b)
        {
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,-.36,-.24,Rune,.014,.006);Face(b,side,u0,u1,-2.2,-2.08,Cold,.014,.006);Face(b,side,u0+.1,u1-.1,b.Y0+.15,-2.4,StoneDark,.06,.06,true);}
            for(int k=0;k<8;k++){double a=k*Math.PI/4;Add(Math.Sin(a)*.55,b.Y1+.005,Math.Cos(a)*.55,.04,.006,.5,FactoryQ.Axis(0,1,0,a),Gold,false);}
            Face(b,5,b.X0+.3,b.X1-.3,b.Z0+.3,b.Z1-.3,Crystal,.1,.1);
        }

        // ---------------- balustrades and parapets ----------------
        private void Rail(Bx b)
        {
            bool lx=LongX(b);double len=lx?b.Sx:b.Sz,a0=lx?b.X0:b.Z0;int s0=lx?2:0,s1=lx?3:1;
            // plinth and coping on both long faces, the coping capping the top
            foreach(int side in new[]{s0,s1}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y0,b.Y0+.16,Trim,.04,.04,true);Face(b,side,u0,u1,b.Y1-.12,b.Y1,Trim,.05,.05,true);}
            if(lx)Box(b.X0,b.X1,b.Y1-.004,b.Y1+.03,b.Z0-.05,b.Z1+.05,Trim);else Box(b.X0-.05,b.X1+.05,b.Y1-.004,b.Y1+.03,b.Z0,b.Z1,Trim);
            // relief panels every 1 m with a rune lozenge, posts with crystal finials every 3 m
            int n=Math.Max(1,(int)Math.Round(len/1.0));double pitch=len/n;
            for(int i=0;i<n;i++)
            {
                double u=a0+(i+.5)*pitch;
                foreach(int side in new[]{s0,s1})Face(b,side,u-pitch*.36,u+pitch*.36,b.Y0+.26,b.Y1-.22,StoneDark,.012,.006);
                foreach(int side in new[]{s0,s1})Face(b,side,u-.05,u+.05,b.Cy-.05,b.Cy+.05,i%2==0?Rune:Cold,.014,.012);
            }
            int posts=Math.Max(1,(int)Math.Floor(len/3.0));
            for(int i=0;i<=posts;i++)
            {
                double u=a0+.14+(len-.28)*i/posts,t=.06;
                if(lx){Box(u-.14,u+.14,b.Y0,b.Y1+.06,b.Z0-t,b.Z1+t,Stone);Add(u,b.Y1+.15,b.Cz,.06,.09,.06,FactoryQ.Axis(0,1,0,Math.PI/4),Crystal,false);}
                else{Box(b.X0-t,b.X1+t,b.Y0,b.Y1+.06,u-.14,u+.14,Stone);Add(b.Cx,b.Y1+.15,u,.06,.09,.06,FactoryQ.Axis(0,1,0,Math.PI/4),Crystal,false);}
            }
        }

        // ---------------- star-metal pylons ----------------
        private void Pylon(Bx b)
        {
            foreach(var (x,z) in new[]{(b.X0,b.Z0),(b.X0,b.Z1),(b.X1,b.Z0),(b.X1,b.Z1)})Box(x-.06,x+.06,b.Y0,b.Y1,z-.06,z+.06,Edge);
            foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);double m=(u0+u1)/2;for(double y=1.3;y<b.Y1-1.1;y+=.42)Face(b,side,m-.035,m+.035,y,y+.26,Crystal,.014,.012);Face(b,side,u0+.08,u1-.08,.9,1.06,Gold,.03,.03);Face(b,side,u0+.08,u1-.08,b.Y1-.62,b.Y1-.48,Gold,.03,.03);Face(b,side,m-.16,m+.16,b.Y1-.95,b.Y1-.75,Rune,.014,.012);}
            Box(b.X0-.1,b.X1+.1,b.Y1-.3,b.Y1,b.Z0-.1,b.Z1+.1,Metal);Box(b.X0-.06,b.X1+.06,b.Y0,b.Y0+.4,b.Z0-.06,b.Z1+.06,Metal);
            Box(b.Cx-.25,b.Cx+.25,b.Y1,b.Y1+.2,b.Cz-.25,b.Cz+.25,Edge);
        }

        // ---------------- sculpted: wardens, watchers, braziers, fallen drums ----------------
        /// <summary>A star-winged guardian in its block (1.4 x 5.x x 1.4): plinth, robe, belt, chest, hood and face, wings folded
        /// behind, arms raised to the star it holds over its head (the warden) or before it (the watcher). It faces the span.</summary>
        private void Guardian(Bx b,bool watcher)
        {
            double cx=b.Cx,cz=b.Cz,y0=b.Y0,top=b.Y1;
            // facing: wardens at the plazas look along the span toward the Breach, watchers back toward their own tower
            double fx=watcher?(cx<0?-1:1):(cx<0?1:-1);
            Box(b.X0+.02,b.X1-.02,y0,y0+.62,b.Z0+.02,b.Z1-.02,StoneDark);Box(b.X0+.08,b.X1-.08,y0+.62,y0+.78,b.Z0+.08,b.Z1-.08,Trim);
            Box(b.X0+.06,b.X1-.06,y0+.3,y0+.42,b.Z0,b.Z0+.05,Rune,false);
            double s=top-y0;double R(double t)=>y0+.78+(s-.78)*t;
            // the robe in three tapering tiers, folds, the gold belt
            Add(cx,(R(0)+R(.36))/2,cz,.56,(R(.36)-R(0))/2,.52,FactoryQ.Identity,Statue);
            Add(cx,(R(.36)+R(.56))/2,cz,.5,(R(.56)-R(.36))/2,.46,FactoryQ.Identity,Statue);
            for(int k=-2;k<=2;k++)Add(cx+fx*.5,(R(0)+R(.5))/2,cz+k*.18,.04,(R(.5)-R(0))/2,.05,FactoryQ.Identity,Statue);
            Add(cx,R(.56),cz,.5,.06,.47,FactoryQ.Identity,Gold);
            // chest and shoulders, the hood and its shadowed face, a halo
            Add(cx,(R(.56)+R(.74))/2,cz,.46,(R(.74)-R(.56))/2,.42,FactoryQ.Identity,Statue);
            foreach(int sd in new[]{-1,1})Add(cx,R(.72),cz+sd*.44,.26,.14,.2,FactoryQ.Axis(1,0,0,sd*.3),Statue);
            Add(cx,(R(.75)+R(.88))/2,cz,.26,(R(.88)-R(.75))/2,.24,FactoryQ.Axis(0,1,0,Math.PI/4),Statue);
            Add(cx+fx*.2,R(.8),cz,.08,.12,.14,FactoryQ.Identity,Dark,false);
            for(int k=0;k<12;k++){double a=k*Math.PI/6;Add(cx-fx*.12,R(.82)+Math.Cos(a)*.42,cz+Math.Sin(a)*.42,.02,.04,.11,FactoryQ.Axis(1,0,0,-a),Gold,false);}
            // wings folded behind: two long feathered plates from the shoulders down the back
            foreach(int sd in new[]{-1,1})for(int k=0;k<3;k++)
            {
                double y=R(.32+k*.15),len=(R(.92)-R(.18))*(1-k*.2);Add(cx-fx*(.52-k*.04),y+len*.2,cz+sd*(.26+k*.06),.04,len/2,.15,FactoryQ.Axis(1,0,0,sd*(.06+k*.05)),k==0?Statue:Trim);
            }
            // arms raised to the star: the warden's over its head, the watcher's before its chest
            foreach(int sd in new[]{-1,1})
            {
                double sx=cx,sy=R(.72),sz=cz+sd*.42,hx=cx+fx*(watcher?.46:.08),hy=watcher?R(.66):Math.Min(top-.32,R(.97)),hz=cz+sd*.1;
                Bar(sx,sy,sz,hx,hy,hz,.16,Statue);
            }
            double stx=cx+fx*(watcher?.52:.08),sty=watcher?R(.68):Math.Min(top-.2,R(1)-.18);
            Add(stx,sty,cz,.14,.14,.14,FactoryQ.Axis(1,0,0,Math.PI/4)*FactoryQ.Axis(0,1,0,Math.PI/4),Crystal,false);
            // the crown of points over the hood (the watcher's reaches the top of its block; the warden's star rides above it)
            for(int k=0;k<5;k++){double a=k*Math.PI*2/5;double ty=watcher?top-.16:R(.9)+.1;Add(cx-fx*.08+Math.Cos(a)*.16,ty,cz+Math.Sin(a)*.16,.035,.15,.035,FactoryQ.Axis(Math.Sin(a),0,-Math.Cos(a),.25),Gold,false);}
        }
        private void Brazier(Bx b)
        {
            double cx=b.Cx,cz=b.Cz,y0=b.Y0;
            for(int k=0;k<3;k++){double a=k*2*Math.PI/3+.4;Bar(cx+Math.Cos(a)*.5,y0+.02,cz+Math.Sin(a)*.5,cx+Math.Cos(a)*.22,y0+.62,cz+Math.Sin(a)*.22,.08,Bronze);}
            Add(cx,y0+.05,cz,.5,.05,.5,FactoryQ.Axis(0,1,0,Math.PI/8),StoneDark);
            Add(cx,y0+.8,cz,.48,.16,.48,FactoryQ.Identity,Bronze);Add(cx,y0+.8,cz,.48,.16,.48,FactoryQ.Axis(0,1,0,Math.PI/4),Bronze);
            Add(cx,y0+.66,cz,.3,.06,.3,FactoryQ.Axis(0,1,0,Math.PI/8),Bronze);Add(cx,y0+.97,cz,.5,.025,.5,FactoryQ.Axis(0,1,0,Math.PI/8),Gold,false);
            Add(cx,y0+.97,cz,.4,.03,.4,FactoryQ.Identity,Crystal,false);
        }
        private void Fallen(Bx b)
        {
            // a column drum lying along its long axis (octagonal: two boxes turned 45° about it), fluting, a broken end; or a block
            bool lx=LongX(b);double len=lx?b.Sx:b.Sz,r=Math.Min(b.Sy,lx?b.Sz:b.Sx)/2-.02;var along=lx?FactoryQ.Axis(1,0,0,Math.PI/4):FactoryQ.Axis(0,0,1,Math.PI/4);
            double cy=b.Y0+r+.01;
            if(len/(2*r)>1.4)
            {
                Add(b.Cx,cy,b.Cz,lx?len/2-.04:r*.92,r*.92,lx?r*.92:len/2-.04,FactoryQ.Identity,Stone);Add(b.Cx,cy,b.Cz,lx?len/2-.06:r*.92,r*.92,lx?r*.92:len/2-.06,along,Stone);
                for(int k=0;k<8;k++){double a=k*Math.PI/4+Math.PI/8;double oy=Math.Cos(a)*r*.93,os=Math.Sin(a)*r*.93;if(lx)Add(b.Cx,cy+oy,b.Cz+os,len/2-.12,.03,.03,FactoryQ.Identity,StoneDark,false);else Add(b.Cx+os,cy+oy,b.Cz,.03,.03,len/2-.12,FactoryQ.Identity,StoneDark,false);}
                if(lx)Add(b.Cx,cy,b.Cz,.05,r*.95,r*.95,FactoryQ.Identity,Rune,false);else Add(b.Cx,cy,b.Cz,r*.95,r*.95,.05,FactoryQ.Identity,Rune,false);
                // fill the box's floor under the drum so the cover reads full height to its top
                Box(b.X0+.04,b.X1-.04,b.Y0,b.Y0+.2,b.Z0+.04,b.Z1-.04,StoneDark);
            }
            else
            {
                Box(b.X0+.02,b.X1-.02,b.Y0,b.Y1-.02,b.Z0+.02,b.Z1-.02,Stone);Box(b.X0+.1,b.X1-.1,b.Y1-.12,b.Y1+.02,b.Z0+.1,b.Z1-.1,StoneDark);
                // a fallen capital's block: a broken corner, a rune star on its top
                Box(b.X1-.42,b.X1+.04,b.Y1-.5,b.Y1-.1,b.Z1-.4,b.Z1+.04,StoneDark);Add(b.Cx,b.Y1+.025,b.Cz,.3,.006,.05,FactoryQ.Axis(0,1,0,.6),Gold,false);Add(b.Cx,b.Y1+.025,b.Cz,.3,.006,.05,FactoryQ.Axis(0,1,0,-.6),Gold,false);
                foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0+.15,u1-.15,b.Y0+.3,b.Y1-.3,Rune,.014,-.016);}
            }
        }

        // ---------------- floors, halls, crowns ----------------
        private void Plaza(Bx b)
        {
            bool hall=Has(b.Id,"-floor");double cx=hall?(b.Cx<0?-31.8:31.8):b.Cx,r=hall?3.6:3.0;
            for(int k=0;k<32;k++){double a=k*Math.PI/16;Add(cx+Math.Sin(a)*r,b.Y1+.005,Math.Cos(a)*r,.045,.006,r*Math.PI/32,FactoryQ.Axis(0,1,0,a+Math.PI/2),Rune,false);}
            for(int k=0;k<8;k++){double a=k*Math.PI/8,l=k%2==0?r*1.7:r*1.1;Add(cx,b.Y1+.005,0,.04,.006,l/2,FactoryQ.Axis(0,1,0,a),Gold,false);}
            if(!hall){foreach(double z in new[]{-9.5,9.5})Face(b,4,b.X0+.2,b.X1-.2,z-.03,z+.03,Gold,.014,.005);
                foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,-.42,-.3,Rune,.014,.006);Face(b,side,u0,u1,-.9,-.6,Trim,.18,.18,true);}}
            else foreach(int side in new[]{2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,-.42,-.3,Rune,.014,.006);}
        }
        private void Crown(Bx b)
        {
            foreach(int side in new[]{0,1,2,3})
            {
                Along(b,side,out double u0,out double u1);bool outer=side==2&&b.Z1>9.7||side==3&&b.Z0<-9.7||side==1&&Math.Abs(b.X0)>37.9||side==0&&Math.Abs(b.X1)>37.9;if(!outer)continue;
                Face(b,side,u0,u1,b.Y0+.06,b.Y0+.18,Rune,.014,.006);Face(b,side,u0,u1,b.Y1-.12,b.Y1,Trim,.1,.1,true);
                for(double u=u0+.3;u<u1-.2;u+=.6){if(side<=1)Box(side==0?b.X1:b.X0-.22,side==0?b.X1+.22:b.X0,b.Y0-.22,b.Y0,u-.12,u+.12,StoneDark);else Box(u-.12,u+.12,b.Y0-.22,b.Y0,side==2?b.Z1:b.Z0-.22,side==2?b.Z1+.22:b.Z0,StoneDark);}
            }
            if(b.Sx>4&&b.Sz>4){double cx=b.Cx<0?-33.0:33.0,cz=b.Cx<0?-1.0:1.0;if(cx>b.X0&&cx<b.X1&&cz>b.Z0&&cz<b.Z1)for(int k=0;k<20;k++){double a=k*Math.PI/10;Add(cx+Math.Sin(a)*2.2,b.Y1+.005,cz+Math.Cos(a)*2.2,.04,.006,.36,FactoryQ.Axis(0,1,0,a+Math.PI/2),Cold,false);}}
        }
        private void Wall(Bx b)
        {
            bool lx=LongX(b);int s0=lx?2:0,s1=lx?3:1;bool head=Has(b.Id,"-arch")||Has(b.Id,"-lintel");
            foreach(int side in new[]{s0,s1})
            {
                Along(b,side,out double u0,out double u1);bool outside=side==2&&b.Z1>7.9||side==3&&b.Z0<-7.9||side==0&&b.X1>-28.05&&b.X1<0||side==1&&b.X0<28.05&&b.X0>0||side==1&&b.X0<-37.9||side==0&&b.X1>37.9;
                Face(b,side,u0,u1,Math.Max(b.Y0,3.5),Math.Min(b.Y1,3.72),Trim,.14,.14,true);
                if(head){Face(b,side,u0,u1,b.Y0,b.Y0+.12,Gold,.03,.03);if(Has(b.Id,"-arch")){double m=(u0+u1)/2;Face(b,side,m-.22,m+.22,b.Y0+.05,b.Y1-.05,Rune,.06,.06);}continue;}
                if(!outside){Face(b,side,u0,u1,2.16,2.24,Rune,.014,.006);continue;}
                for(double u=u0+1.25;u<u1-.6;u+=2.5)Face(b,side,u-.22,u+.22,b.Y0,Math.Min(b.Y1,3.5),Stone,.15,.15,true);
                Face(b,side,u0,u1,b.Y0,b.Y0+.35,StoneDark,.1,.1,true);
                for(double u=u0+2.5;u<u1-1.2;u+=2.5){Face(b,side,u-.5,u+.5,1.0,2.7,Dark,.012,.006);Face(b,side,u-.56,u+.56,2.7,2.82,Gold,.03,.03);Face(b,side,u-.06,u+.06,1.2,2.5,Rune,.014,.012);}
            }
        }
        private void Altar(Bx b)
        {
            // a carved stone stele: plinth and cornice, two rune columns, a gold star round a small cold crystal, star-metal edges
            foreach(int side in new[]{0,1})
            {
                Along(b,side,out double u0,out double u1);
                Face(b,side,u0,u1,b.Y0,b.Y0+.45,StoneDark,.12,.12,true);Face(b,side,u0,u1,b.Y1-.4,b.Y1,Trim,.12,.12,true);Face(b,side,u0,u1,b.Y1-.52,b.Y1-.44,Gold,.03,.03);
                foreach(double u in new[]{u0+.75,u1-.75}){Face(b,side,u-.22,u+.22,.6,b.Y1-.6,Stone,.08,.08,true);Face(b,side,u-.04,u+.04,.8,b.Y1-.8,Rune,.014,.09);}
                double cy=2.1;for(int k=0;k<8;k++){double a=k*Math.PI/4,l=k%2==0?.9:.55;double xf=side==0?b.X1+.03:b.X0-.03;Add(xf,cy+Math.Cos(a)*l/2,b.Cz+Math.Sin(a)*l/2,.02,.035,l/2,FactoryQ.Axis(1,0,0,-a),Gold,false);}
                Face(b,side,b.Cz-.16,b.Cz+.16,cy-.16,cy+.16,Crystal,.05,.05);
            }
            foreach(var z in new[]{b.Z0,b.Z1})Box(b.X0-.05,b.X1+.05,b.Y0,b.Y1,z-.06,z+.06,Edge);
        }
        private void Gable(Bx b)
        {
            bool sill=Has(b.Id,"-sill"),head=Has(b.Id,"-head");int outer=b.X0<0?0:1,inner=b.X0<0?1:0;
            Face(b,outer,b.Z0,b.Z1,b.Y1-.2,b.Y1,Trim,.12,.12,true);Face(b,inner,b.Z0,b.Z1,b.Y1-.2,b.Y1,Trim,.12,.12,true);
            if(sill){Face(b,outer,b.Z0,b.Z1,b.Y1-.08,b.Y1,Gold,.05,.05);return;}
            if(head)
            {
                // the rose window's upper ring and its keystone on the outer face
                double cz=0,cy=6.4,r=1.62;double xf=outer==0?b.X1+.03:b.X0-.03;
                for(int k=0;k<9;k++){double a=Math.PI*(k+.5)/9;double z=cz-Math.Cos(a)*r,y=cy+Math.Sin(a)*r;if(y<b.Y0+.05)continue;Add(xf,y,z,.03,.09,.22,FactoryQ.Axis(1,0,0,a-Math.PI/2),Gold,false);}
                Face(b,outer,-.25,.25,b.Y1-.75,b.Y1-.2,Rune,.05,.05);return;
            }
            // the window's jambs (lit), pilasters and rune bands
            bool south=b.Cz<0;double jamb=south?b.Z1:b.Z0;Box(b.Cx-.04,b.Cx+.04,b.Y0+1.05,b.Y0+2.95,south?jamb-.01:jamb-.004,south?jamb+.004:jamb+.01,Rune,false);
            Box(b.X0-.04,b.X1+.04,b.Y0+.92,b.Y0+1.0,south?jamb-.3:jamb,south?jamb:jamb+.3,Gold,false);
            for(double z=b.Z0+.6;z<b.Z1-.3;z+=1.4)Face(b,outer,z-.18,z+.18,b.Y0,b.Y1-.2,Stone,.12,.12,true);
            Face(b,outer,b.Z0,b.Z1,b.Y0+3.4,b.Y0+3.5,Rune,.014,.006);Face(b,inner,b.Z0,b.Z1,b.Y0+2.4,b.Y0+2.48,Cold,.014,.006);
        }
        private void Spire(Bx b)
        {
            foreach(var (x,z) in new[]{(b.X0,b.Z0),(b.X0,b.Z1),(b.X1,b.Z0),(b.X1,b.Z1)})Box(x-.12,x+.12,b.Y0,b.Y1-.4,z-.12,z+.12,Trim);
            for(double y=b.Y0+1.6;y<b.Y1-1.8;y+=2.0)foreach(int side in new[]{0,1,2,3}){Along(b,side,out double u0,out double u1);Face(b,side,u0+.15,u1-.15,y,y+.1,Rune,.014,.008);Face(b,side,(u0+u1)/2-.25,(u0+u1)/2+.25,y+.4,y+1.4,Dark,.012,.006);}
            // the broken crown: jagged stones on the top, a stub of cold crystal
            for(int i=0;i<7;i++){double w=.3+H(133,i)*.5,d=.3+H(135,i)*.5,x=b.X0+.1+H(131,i,b.X0<0?0:1)*(b.Sx-.2-w),z=b.Z0+.1+H(132,i,b.X0<0?0:1)*(b.Sz-.2-d);Box(x,x+w,b.Y1-.3,b.Y1+.05+H(134,i)*.18,z,z+d,Stone);}
        }
        private void Gate(Bx b)
        {
            bool headPart=Has(b.Id,"-head");int s0=0,s1=1;
            foreach(int side in new[]{s0,s1})
            {
                Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y1-.2,b.Y1,Trim,.12,.12,true);
                if(headPart){Face(b,side,u0+.1,u1-.1,b.Y0,b.Y0+.12,Gold,.04,.04);Face(b,side,(u0+u1)/2-.18,(u0+u1)/2+.18,b.Y0+.3,b.Y1-.3,Rune,.05,.05);continue;}
                for(double u=u0+.9;u<u1-.4;u+=1.6)Face(b,side,u-.2,u+.2,b.Y0,b.Y1-.2,Stone,.12,.12,true);
                Face(b,side,u0,u1,b.Y0,b.Y0+.3,StoneDark,.08,.08,true);Face(b,side,u0,u1,3.0,3.08,Rune,.014,.006);
                // the lantern by the door: a bracket and a warm glass
                double ud=b.Cz>0?u1-.25:u0+.25;double xf=side==0?b.X1:b.X0,sg=side==0?1:-1;Box(Math.Min(xf,xf+sg*.22),Math.Max(xf,xf+sg*.22),2.3,2.36,ud-.04,ud+.04,Bronze);
                Box(Math.Min(xf+sg*.06,xf+sg*.24),Math.Max(xf+sg*.06,xf+sg*.24),1.86,2.26,ud-.09,ud+.09,Glass,false);
            }
        }
        private void Balcony(Bx b)
        {
            bool north=b.Cz>0;int side=north?2:3;
            Face(b,side,b.X0,b.X1,b.Y0,b.Y0+.1,Trim,.08,.08,true);Face(b,side,b.X0,b.X1,b.Y1-.2,b.Y1-.14,Rune,.012,.006);
            for(double x=b.X0+.55;x<b.X1-.3;x+=1.1){double z0=north?b.Z0+.2:b.Z1-1.9,z1=north?b.Z0+1.9:b.Z1-.2;Box(x-.15,x+.15,b.Y0-.24,b.Y0,z0,z1,StoneDark);}
            double cx=b.Cx,cz=b.Cz;for(int k=0;k<16;k++){double a=k*Math.PI/8;Add(cx+Math.Sin(a)*.8,b.Y1+.005,cz+Math.Cos(a)*.8,.03,.006,.16,FactoryQ.Axis(0,1,0,a+Math.PI/2),Gold,false);}
        }
        private void Walk(Bx b)
        {
            foreach(int side in new[]{2,3})
            {
                Along(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y1-.36,b.Y1-.12,Trim,.16,.16,true);Face(b,side,u0,u1,-4.26,-4.18,Rune,.014,.006);
                for(double u=u0+1.2;u<u1-.6;u+=2.4){Face(b,side,u-.7,u+.7,-6.6,-4.6,StoneDark,.012,.006);Face(b,side,u-.78,u+.78,-4.6,-4.5,Stone,.08,.08,true);Face(b,side,u-.06,u+.06,-6.2,-5.0,Cold,.014,.012);}
            }
            Face(b,4,b.X0,b.X1,(b.Cz>0?9.32:-9.36),(b.Cz>0?9.36:-9.32),Gold,.014,.005);
        }
        private void Step(Bx b)
        {
            bool alongX=b.Sz>b.Sx;bool floating=b.Sy<.3;
            // gold lines along both tread edges; a floating step's cold underside
            if(alongX)foreach(double x in new[]{b.X0,b.X1})Box(x-.02,x+.02,b.Y1-.03,b.Y1+.004,b.Z0+.05,b.Z1-.05,Gold,false);
            else foreach(double z in new[]{b.Z0,b.Z1})Box(b.X0+.05,b.X1-.05,b.Y1-.03,b.Y1+.004,z-.02,z+.02,Gold,false);
            if(floating)Box(b.X0+.04,b.X1-.04,b.Y0-.01,b.Y0+.004,b.Z0+.1,b.Z1-.1,Cold,false);
        }
    }

    /// <summary>
    /// Starfall Causeway's moving pieces for one frame (docs/arenas/CAUSEWAY.md), posed from the replicated hazard state and the
    /// render clock into NativeBlockLit parts: the falling stars (the burning stone through its fall, the shards thrown from the
    /// crater), the comet lane's glyphs waking from the far end through the warning, the pylons' banners standing out in the solar
    /// wind, the crystals floating over the pylons, braziers and the spire, the Shattered Arch's voussoirs drifting over the Breach,
    /// and the near asteroids tumbling round the void. Deterministic (the same state gives the same parts); allocation free after
    /// construction.
    /// </summary>
    public sealed class NativeCausewayRig
    {
        public const int Capacity=2400;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>The stage art's view of the frame's parts (docs/arenas/STAGE-ART.md; Core NativeCausewayArt reads them): for
        /// part i its rig slot (-1: the modelled art has no piece of its own for it), a level (a lane glyph's light; a floating
        /// crystal's turn about y, NaN to keep the part's own) and its kit kind. Nothing draws by them without the stage bank.</summary>
        public readonly int[] Slot=new int[Capacity],Kind=new int[Capacity];public readonly double[] Level=new double[Capacity];
        private void Put(int slot,double level,double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true)
        {int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;Kind[n]=kind;}}
        private readonly double[] pylons,braziers,spires;private readonly int pylonCount,brazierCount,spireCount;
        public NativeCausewayRig(NativeFactoryKit.Looks looks,ContentMap map)
        {
            Parts=new NativeFactoryKit.Writer(Capacity,false,looks);var p=new List<double>();var b=new List<double>();var s=new List<double>();
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())
            {
                if(k?.id==null)continue;double top=k.pos.y+k.size.y/2;
                if(k.id.StartsWith("causeway-pylon-",StringComparison.Ordinal)){p.Add(k.pos.x);p.Add(top);p.Add(k.pos.z);}
                else if(k.id.StartsWith("causeway-brazier-",StringComparison.Ordinal)){b.Add(k.pos.x);b.Add(top);b.Add(k.pos.z);}
                else if(k.id.StartsWith("causeway-spire-",StringComparison.Ordinal)){s.Add(k.pos.x);s.Add(top);s.Add(k.pos.z);}
            }
            pylons=p.ToArray();braziers=b.ToArray();spires=s.ToArray();pylonCount=pylons.Length/3;brazierCount=braziers.Length/3;spireCount=spires.Length/3;
        }
        public int Pylons=>pylonCount;public int Braziers=>brazierCount;
        public double PylonX(int i)=>pylons[i*3];public double PylonTop(int i)=>pylons[i*3+1];public double PylonZ(int i)=>pylons[i*3+2];
        public double BrazierX(int i)=>braziers[i*3];public double BrazierTop(int i)=>braziers[i*3+1];public double BrazierZ(int i)=>braziers[i*3+2];
        public int Spires=>spireCount;public double SpireX(int i)=>spires[i*3];public double SpireTop(int i)=>spires[i*3+1];public double SpireZ(int i)=>spires[i*3+2];
        private static double H(int a,int b=0,int c=0)=>NativeCausewayLooks.Hash(a,b,c);
        private const int Stone=NativeCausewayLooks.Stone,Rune=NativeCausewayLooks.Rune,Cold=NativeCausewayLooks.RuneCold,Crystal=NativeCausewayLooks.Crystal,Violet=NativeCausewayLooks.CrystalViolet,Rock=NativeCausewayLooks.Rock,
            Ember=NativeCausewayLooks.Ember,Cloth=NativeCausewayLooks.Cloth,ClothEdge=NativeCausewayLooks.ClothEdge,Metal=NativeCausewayLooks.Metal,Trim=NativeCausewayLooks.Trim,Dark=NativeCausewayLooks.Dark,Comet=NativeCausewayLooks.Comet;

        /// <summary>The Shattered Arch's stones: index k of <see cref="ArchStones"/>, its rest position on the broken curve over the
        /// Breach (x along the span, y from 7.4 to 10.6) and drift at render time `t`.</summary>
        public const int ArchStones=9;
        public static void ArchStone(int k,double t,out double x,out double y,out double z,out double tilt)
        {
            double f=(k+.5)/ArchStones,a=Math.PI*f;x=-Math.Cos(a)*6.4;y=7.4+Math.Sin(a)*3.2;z=0;tilt=-(a-Math.PI/2);
            double gap=k==ArchStones/2?0:Math.Sign(x)*(.35+.25*Math.Abs(x)/6.4);x+=gap+Math.Sin(t*.31+k*1.7)*.12;y+=Math.Sin(t*.43+k*2.3)*.18+(k==ArchStones/2?.5:0);z+=Math.Sin(t*.27+k)*.1;tilt+=Math.Sin(t*.37+k*.9)*.05;
        }

        /// <summary>Fills <see cref="Parts"/> for the hazards and the replicated state (round, phase, FightAt) at field clock `now`
        /// and render clock `render`.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();double clock=NativeFactoryHazards.Clock(phase,fightAt,now);double gust=0,gustAx=0,gustWarn=0;double gx0=1e9,gx1=-1e9;
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Press)StarRig(h,round,clock,i);
                else if(NativeCyberCityHazards.IsMaglev(h)){var st=NativeCausewayLooks.CometAt(h,phase,round,fightAt,now);LaneRig(h,st,render);CometCore(h,st,render);}
                else if(h.kind==NativeFactoryHazards.Vent){NativeCausewayLooks.WindAt(h,round,clock,out double w,out double g,out _);if(g>gust||w>gustWarn){gust=Math.Max(gust,g);gustWarn=Math.Max(gustWarn,w);gustAx=h.axis.z;gx0=h.a.x-h.size.x/2;gx1=h.a.x+h.size.x/2;}}
            }
            Banners(render,gust,gustWarn,gustAx,gx0,gx1);Floaters(render);Arch(render);CrownRuins(render);Asteroids(render);
        }
        private void StarRig(MapHazard h,int round,double clock,int index)
        {
            var s=NativeCausewayLooks.StarAt(h,round,clock);
            if(s.Fall>=0)
            {
                // the burning stone: a dark core of turned blocks with molten seams, tumbling along its path
                double spin=s.Fall*9+index;var q=FactoryQ.Axis(.6,.7,.38,spin);
                Put(NativeCausewayArt.Star,0,s.Px,s.Py,s.Pz,.62,.55,.58,q,Rock);Put(-1,0,s.Px,s.Py,s.Pz,.5,.66,.44,q*FactoryQ.Axis(0,1,0,.8),Rock);Put(-1,0,s.Px,s.Py,s.Pz,.66,.08,.66,q*FactoryQ.Axis(1,0,0,.5),Ember,false);
                Put(-1,0,s.Px,s.Py,s.Pz,.07,.7,.07,q,Ember,false);
            }
            if(s.Since<1.6)
            {
                // shards thrown from the crater on ballistic arcs
                double t=s.Since;for(int k=0;k<10;k++){double a=k*2*Math.PI/10+H(index,s.Cycle,k),v=3.5+H(index,k,s.Cycle)*4.5,up=4+H(index,k+20,s.Cycle)*5;
                    double x=s.X+Math.Cos(a)*v*t,z=s.Z+Math.Sin(a)*v*t,y=s.Y+.2+up*t-11*t*t;if(y<s.Y+.05)continue;double sz=.08+H(index,k+40)*.12;
                    Put(k%3==0?NativeCausewayArt.ShardEmber:NativeCausewayArt.Shard,0,x,y,z,sz,sz*.8,sz,FactoryQ.Axis(.3,.8,.5,t*12+k),k%3==0?Ember:Rock,false);}
            }
        }
        private void LaneRig(MapHazard h,NativeHazardState st,double t)
        {
            // glyph tiles along both channel edges: dim at rest; in the warning they wake from the far end toward the comet's near
            // portal; in the danger they burn; in the pass they flare behind the head and fade through the clear
            double x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2;int dir=st.Direction==0?1:st.Direction;
            for(double x=x0+.625;x<x1;x+=1.25)
            {
                if(Math.Abs(x)<3.1)continue;double along=dir>0?(x1-x)/(x1-x0):(x-x0)/(x1-x0);double lit=.12;int kind=Rune;
                switch(st.Phase)
                {
                    case NativeHazardPhase.Warn:{double wave=Math.Max(0,Math.Min(1,st.Level*1.25-along*.6));lit=.12+.88*wave*(.75+.25*Math.Sin(t*9-along*14));break;}
                    case NativeHazardPhase.Danger:lit=.8+.2*Math.Sin(t*22+x);kind=Cold;break;
                    case NativeHazardPhase.Active:{double behind=dir>0?st.Head-x:x-st.Head;lit=behind>=0?Math.Max(.4,1-behind/30):1;kind=Cold;break;}
                    case NativeHazardPhase.Clear:lit=.15+.6*st.Level;kind=Cold;break;
                }
                if(lit<.2&&st.Phase==NativeHazardPhase.Idle){if(((int)Math.Floor(x)&1)==0)continue;}
                double hy=.004+.006*lit;for(int e=-1;e<=1;e+=2)Put(lit>.2?NativeCausewayArt.Glyph:-1,lit,x,hy,e*1.25,.16,hy,.07,FactoryQ.Identity,lit>.5?kind:Rune,false);
            }
        }
        private void Banners(double t,double gust,double warn,double ax,double gx0,double gx1)
        {
            // a star-blue banner on the deck face of each pylon, hung from its crown: it sways at rest, shivers in the warning and
            // stands out downwind in the gust over that section
            for(int i=0;i<pylonCount;i++)
            {
                double x=PylonX(i),top=PylonTop(i)-.5,z=PylonZ(i),zf=z>0?z-.62:z+.62;bool inWind=x>gx0-.5&&x<gx1+.5;double g=inWind?gust:0,w=inWind?warn:0;
                double sway=.06*Math.Sin(t*1.3+i*1.7)+.08*w*Math.Sin(t*13+i);double toward=z>0?-1:1;double lift=g>0?g*(1.25+.12*Math.Sin(t*17+i*3)):0;
                // a banner on the downwind side of its pylon stands out over the deck; one on the upwind side is pressed flat to it
                bool free=toward*ax>0;double angle=free?sway+Math.Sign(ax)*lift:sway*.4+Math.Sign(ax)*.03*g;
                // hinge at the top: the cloth swings about x; `angle` > 0 swings it toward +z
                double len=2.4,wid=.42;int seg=3;double py=top,pz=zf;
                for(int k=0;k<seg;k++)
                {
                    double a=angle*(1+.35*k)+(g>0?.1*Math.Sin(t*21+k+i):0),l=len/seg;double ny=py-Math.Cos(a)*l,nz=pz+Math.Sin(a)*l;
                    Put(NativeCausewayArt.Banner,0,x,(py+ny)/2,(pz+nz)/2,wid,l/2+.01,.012,FactoryQ.Axis(1,0,0,-a),Cloth,false);
                    if(k==seg-1)Put(NativeCausewayArt.BannerHem,0,x,ny+.03,nz,wid+.02,.04,.016,FactoryQ.Axis(1,0,0,-a),ClothEdge,false);
                    py=ny;pz=nz;
                }
                Put(-1,0,x,top+.02,zf,wid+.08,.03,.03,FactoryQ.Identity,Metal,false);
                Put(NativeCausewayArt.BannerStar,0,x,top-len*.42,zf+(z>0?-.01:.01)+Math.Sin(angle)*len*.42,.09,.09,.014,FactoryQ.Axis(1,0,0,-angle)*FactoryQ.Axis(0,0,1,Math.PI/4),Rune,false);
            }
        }
        private void Floaters(double t)
        {
            // crystals over the pylons, the braziers and the spires: turning and bobbing
            for(int i=0;i<pylonCount;i++){double y=PylonTop(i)+.75+.12*Math.Sin(t*1.1+i);var q=FactoryQ.Axis(0,1,0,t*.6+i)*FactoryQ.Axis(1,0,0,Math.PI/4)*FactoryQ.Axis(0,0,1,Math.PI/4);Put(NativeCausewayArt.Crystal,t*.6+i,PylonX(i),y,PylonZ(i),.13,.2,.13,q,Crystal,false);}
            for(int i=0;i<brazierCount;i++)
            {
                double x=BrazierX(i),z=BrazierZ(i),y=BrazierTop(i)+.45+.08*Math.Sin(t*1.4+i*2);
                for(int k=0;k<4;k++){double a=t*.5+k*Math.PI/2+i,r=k==0?0:.16;var q=FactoryQ.Axis(0,1,0,a)*FactoryQ.Axis(1,0,0,.25*k);Put(k%2==0?NativeCausewayArt.Crystal:NativeCausewayArt.CrystalViolet,a,x+Math.Cos(a)*r,y-.06*k,z+Math.Sin(a)*r,.07,k==0?.26:.15,.07,q,k%2==0?Crystal:Violet,false);}
            }
            for(int i=0;i<spireCount;i++){double y=SpireTop(i)+1.6+.25*Math.Sin(t*.7+i);var q=FactoryQ.Axis(0,1,0,t*.3+i)*FactoryQ.Axis(0,0,1,.12);Put(NativeCausewayArt.CrystalViolet,t*.3+i,SpireX(i),y,SpireZ(i),.22,.9,.22,q,Violet,false);Put(NativeCausewayArt.Crystal,t*.3+i,SpireX(i),y-1.05,SpireZ(i),.1,.24,.1,q,Crystal,false);}
        }
        private void Arch(double t)
        {
            for(int k=0;k<ArchStones;k++)
            {
                ArchStone(k,t,out double x,out double y,out double z,out double tilt);var q=FactoryQ.Axis(0,0,1,tilt);bool key=k==ArchStones/2;
                Put(key?NativeCausewayArt.VoussoirKey:NativeCausewayArt.Voussoir,0,x,y,z,.62,.5,1.1,q,Stone);Put(-1,0,x,y,z,.64,.08,1.12,q,key?Crystal:Rune,false);if(key)Put(NativeCausewayArt.Crystal,t*.8,x,y+.62,z,.18,.3,.18,q*FactoryQ.Axis(0,1,0,t*.8),Crystal,false);
            }
        }
        private void CometCore(MapHazard h,in NativeHazardState st,double t)
        {
            // the comet's nucleus: a dark, tumbling lump of ice with bright facets under its coma
            if(!NativeCausewayLooks.CometHead(h,st,out double x,out double y,out double z,out double near))return;double s=.18+.2*near;var q=FactoryQ.Axis(.5,.8,.3,t*3.1);
            Put(NativeCausewayArt.Comet,0,x,y,z,s,s*.8,s*.9,q,Comet,false);Put(-1,0,x,y,z,s*.8,s,s*.7,q*FactoryQ.Axis(0,1,0,.8),Comet,false);
        }
        /// <summary>Above each crown its tower's lost storeys float apart: masonry fragments with their rune courses, turning slowly
        /// (index k of <see cref="CrownStones"/>; the lowest is 7 m over the crown).</summary>
        public const int CrownStones=7;
        public static void CrownStone(int side,int k,double t,out double x,out double y,out double z,out double size,out double spin)
        {
            x=side*(33+Math.Sin(k*2.3+side)*3.2);z=side*Math.Cos(k*1.7+.4)*5.2;y=11.6+k*1.35+.35*Math.Sin(t*.29+k*1.9+side);size=1.0+.25*((k*7+3)%5);spin=t*(.03+.01*k)*(k%2==0?1:-1)+k;
        }
        private void CrownRuins(double t)
        {
            for(int side=-1;side<=1;side+=2)for(int k=0;k<CrownStones;k++)
            {
                CrownStone(side,k,t,out double x,out double y,out double z,out double s,out double spin);var q=FactoryQ.Axis(.2,1,.15,spin)*FactoryQ.Axis(1,0,0,.25*Math.Sin(k));
                Put(NativeCausewayArt.Crown+k,0,x,y,z,s,s*.55,s*.8,q,Stone);Put(-1,0,x,y-s*.5,z,s*.9,s*.12,s*.7,q,Trim,false);Put(-1,0,x,y+s*.05,z,s*1.02,.06,s*.82,q,k%3==0?Cold:Rune,false);
                if(k%3==1)Put(-1,0,x,y+s*.75,z,s*.2,s*.5,s*.2,q,Stone);
            }
        }
        private void Asteroids(double t)
        {
            // near rocks round the void, always outside the bounds (orbit radius 58 m and more), turning slowly
            for(int i=0;i<16;i++)
            {
                double r=85+H(501,i)*120,w=(.006+H(501,i,1)*.01)*(i%2==0?1:-1),a=H(501,i,2)*2*Math.PI+t*w,y=-50+H(501,i,3)*80+Math.Sin(t*.05+i)*2;
                double x=Math.Cos(a)*r*1.25,z=Math.Sin(a)*r;double sz=1.2+Math.Pow(H(501,i,4),2)*6;var q=FactoryQ.Axis(H(501,i,5)-.5,1,H(501,i,6)-.5,t*(.05+H(501,i,7)*.15)+i);
                // a lumpy rock: three turned masses of their own proportions, one offset
                Put(NativeCausewayArt.Asteroid+(i&3),0,x,y,z,sz,sz*.62,sz*.78,q,Rock,sz>3);Put(-1,0,x,y,z,sz*.72,sz*.8,sz*.58,q*FactoryQ.Axis(.3,1,.2,.9),Rock,false);
                Put(-1,0,x+sz*.35,y+sz*.2,z-sz*.15,sz*.5,sz*.42,sz*.55,q*FactoryQ.Axis(1,.4,0,1.3),Rock,false);
                if(i%5==0)Put(i%10==0?NativeCausewayArt.CrystalViolet:NativeCausewayArt.Crystal,double.NaN,x,y+sz*.6,z,sz*.12,sz*.35,sz*.12,q,i%10==0?Violet:Crystal,false);
            }
        }
    }
}
