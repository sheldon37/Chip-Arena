using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128's static kit (docs/arenas/CLUB.md "The look"): what NativeBlockLit instances draw on and over the club's
    /// dressed blocks, built once per map. Two kinds of part, as the M1 stages' kit:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/> the block): the truss towers' lattice, the speaker cabinets' seams, grilles and
    /// corners, the stage's LED lip and matting, the DJ's decks and mixer, the bar's brass rail, LED underglow and back-bar
    /// bottles, the decks' LED fascias, the rails' chrome caps, the stairs' LED nosings, the walls' crane-rail beam, uplights and
    /// the neon sign, the arches' trim, the columns' plinths and capitals, the podiums' lit rims, the floor's LED border, the
    /// laser wall's slots and the drop zones' hatching. Each stays inside its block grown by <see cref="Proud"/> (what looks
    /// like cover is cover).</item>
    /// <item>Overhead (parent -1): the lighting truss over the dance floor, the cube's and the mirror balls' hangers, the
    /// great south window's frame. Each stands at least <see cref="Clearance"/> m over every standable top under it.</item>
    /// </list>
    /// Emissive parts carry their HDR gain in the part's window value (above 1: Runtime NativeClubDraw multiplies the colour by
    /// it and sends a light level of 1), so LEDs and neon bloom. Deterministic; allocates only while building.
    /// </summary>
    public sealed class NativeClubKit
    {
        public const float Proud=.25f;public const double Clearance=2.0,Decal=.012;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();public bool[] Overhead=Array.Empty<bool>();
        private readonly List<int> parents=new List<int>();private readonly List<bool> overhead=new List<bool>();
        private int current=-1;private bool over;private string family="",ownerId;
        /// <summary>The stage art's layout (Tests/StageArtLayout.cs, docs/arenas/STAGE-ART.md): each part's family (the builder that made it),
        /// the block it dresses (null: overhead) and its kind. Nothing draws by them.</summary>
        public readonly List<string> Family=new List<string>(),Owner=new List<string>();public readonly List<int> Kind=new List<int>();
        public int Count=>Parts.Count;
        private NativeClubKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(2048,true,looks);}
        private void Track(int before,int kind){for(int i=before;i<Parts.Count;i++){parents.Add(current);overhead.Add(over);Family.Add(family);Owner.Add(over?null:ownerId);Kind.Add(kind);}}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);Track(n,kind);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Box(Math.Min(x0,x1),Math.Max(x0,x1),Math.Min(y0,y1),Math.Max(y0,y1),Math.Min(z0,z1),Math.Max(z0,z1),kind,shadow,color,window);Track(n,kind);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow,color);Track(n,kind);}
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;}
        private readonly Dictionary<string,Bx> blocks=new Dictionary<string,Bx>(StringComparer.Ordinal);private readonly List<Bx> all=new List<Bx>();
        private void On(Bx b){current=b.Index;over=false;ownerId=b.Id;}
        private void Above(){current=-1;over=true;ownerId=null;}
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static double H(int a,int b=0,int c=0)=>NativeClubLooks.Hash(a,b,c);
        /// <summary>An emissive part's HDR gain (the window value; see the summary).</summary>
        public const double Glow=2.4,GlowSoft=1.4,GlowHot=3.4;

        /// <summary>The club's kit for its look (null for another map).</summary>
        public static NativeClubKit Build(ContentMap map,MapLook look)
        {
            if(map==null||!NativeClubLooks.Active(map.id))return null;var k=new NativeClubKit(NativeClubLooks.Resolve(look?.kitLooks));
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                var x=new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i};k.all.Add(x);k.blocks[b.id]=x;
            }
            k.Dress();k.Truss();k.Parent=k.parents.ToArray();k.Overhead=k.overhead.ToArray();return k;
        }

        // ---------------- face helpers (a hair proud) ----------------
        /// <summary>A plate on one face of the block: side 0 +x, 1 -x, 2 +z, 3 -z, 4 the top; (u0..u1) along it (z for ±x faces, x
        /// for ±z faces and the top), (v0..v1) up it (world y; z on the top), `t` thick, `o` proud.</summary>
        private void Face(Bx b,int side,double u0,double u1,double v0,double v1,int kind,int color=-1,double t=Decal,double o=.004,bool shadow=false,double window=1)
        {
            switch(side)
            {
                case 0:Box(b.X1+o-t,b.X1+o,v0,v1,u0,u1,kind,shadow,color,window);break;case 1:Box(b.X0-o,b.X0-o+t,v0,v1,u0,u1,kind,shadow,color,window);break;
                case 2:Box(u0,u1,v0,v1,b.Z1+o-t,b.Z1+o,kind,shadow,color,window);break;case 3:Box(u0,u1,v0,v1,b.Z0-o,b.Z0-o+t,kind,shadow,color,window);break;
                default:Box(u0,u1,b.Y1+o-t,b.Y1+o,v0,v1,kind,shadow,color,window);break;
            }
        }
        private static void Span(Bx b,int side,out double u0,out double u1){if(side<=1){u0=b.Z0;u1=b.Z1;}else{u0=b.X0;u1=b.X1;}}
        private bool Get(string id,out Bx b)=>blocks.TryGetValue(id,out b);

        private void Dress()
        {
            foreach(var b in all)
            {
                string id=b.Id;
                if(Starts(id,"club-tower-"))Tower(b);
                else if(Starts(id,"club-speaker-"))Speaker(b);
                else if(id=="club-stage")Stage(b);
                else if(Starts(id,"club-stage-step-")||Starts(id,"club-stage-back-")||Starts(id,"club-stair-")&&!id.EndsWith("-landing",StringComparison.Ordinal))Nosing(b,id);
                else if(id=="club-dj-booth")Booth(b);
                else if(id=="club-bar-counter")Counter(b);
                else if(id=="club-backbar")BackBar(b);
                else if(Starts(id,"club-mezz-")||Starts(id,"club-vip-")&&!id.Contains("-sofa")&&!id.Contains("-table")&&id!="club-vip-booth"||id=="club-catwalk"||id.EndsWith("-landing",StringComparison.Ordinal))Fascia(b,id);
                else if(Starts(id,"club-rail-")||Starts(id,"club-barrier-"))Rail(b,id);
                else if(id=="club-wall-s")SouthWall(b);
                else if(id=="club-wall-w"||id=="club-wall-e")SideWall(b,id=="club-wall-w"?0:1);
                else if(Starts(id,"club-wall-w-pier-")||Starts(id,"club-wall-e-pier-"))Pier(b,Starts(id,"club-wall-w")?0:1);
                else if(id=="club-wall-n")LedWallFrame(b);
                else if(Starts(id,"club-column-"))Column(b);
                else if(Starts(id,"club-podium-"))Podium(b);
                else if(id=="club-cube")CubeFrame(b);
                else if(Starts(id,"club-booth-")||Starts(id,"club-sofa-")||id.EndsWith("-sofa",StringComparison.Ordinal)||id=="club-vip-booth")Upholstery(b);
                else if(id=="club-bar-pillar")Pillar(b);
                else if(Starts(id,"club-green-")&&id.EndsWith("-wall-s",StringComparison.Ordinal))Mirror(b);
            }
            Floor();
        }

        // ---------------- the hall's pieces ----------------
        // A truss tower (solid in the sim): four chrome chords at its corners and X bracing on each face, a base plate and a sleeve.
        private void Tower(Bx b)
        {
            family="tower";On(b);double t=.07,step=.8;int K=NativeClubLooks.Truss,C=NativeClubLooks.Chrome;
            foreach(var (sx,sz) in new[]{(0,0),(1,0),(0,1),(1,1)}){double x=sx==0?b.X0+t/2-.01:b.X1-t/2+.01,z=sz==0?b.Z0+t/2-.01:b.Z1-t/2+.01;Box(x-t/2,x+t/2,b.Y0,b.Y1,z-t/2,z+t/2,C,true);}
            for(int side=0;side<4;side++)
            {
                Span(b,side,out double u0,out double u1);
                for(double y=b.Y0+.3;y<b.Y1-.3;y+=step)
                {
                    double ya=y,yb=Math.Min(b.Y1-.2,y+step);bool flip=((int)((y-b.Y0)/step)&1)==0;
                    double a=flip?u0+.06:u1-.06,c=flip?u1-.06:u0+.06;
                    switch(side)
                    {
                        case 0:Bar(b.X1+.012,ya,a,b.X1+.012,yb,c,.035,K,false);break;case 1:Bar(b.X0-.012,ya,a,b.X0-.012,yb,c,.035,K,false);break;
                        case 2:Bar(a,ya,b.Z1+.012,c,yb,b.Z1+.012,.035,K,false);break;default:Bar(a,ya,b.Z0-.012,c,yb,b.Z0-.012,.035,K,false);break;
                    }
                }
            }
            Box(b.X0-.08,b.X1+.08,b.Y0,b.Y0+.05,b.Z0-.08,b.Z1+.08,NativeClubLooks.Steel);Box(b.X0-.05,b.X1+.05,b.Y1-.45,b.Y1,b.Z0-.05,b.Z1+.05,NativeClubLooks.Steel);
            // a blue LED chase strip up the hall-facing corner (the rig does not move it; the bloom sells it)
            double cx=b.Cx<0?b.X1+.02:b.X0-.02,cz=b.Cz<0?b.Z1+.02:b.Z0-.02;Box(cx-.015,cx+.015,b.Y0+.4,b.Y1-.6,cz-.015,cz+.015,NativeClubLooks.Led,false,NativeClubLooks.Violet,Glow);
        }
        // A speaker stack: four cabinets (seams), corner protectors, the grille the rig's cones sit on, the brand lamp.
        private void Speaker(Bx b)
        {
            family="speaker";On(b);double front=b.Z0;int D=NativeClubLooks.Dark,G=NativeClubLooks.Grille,M=NativeClubLooks.Metal;
            double cab=(b.Y1-b.Y0)/4;
            for(int k=0;k<4;k++)
            {
                double y0=b.Y0+k*cab;Face(b,3,b.X0+.06,b.X1-.06,y0+.06,y0+cab-.06,G,-1,.02,.004);
                Face(b,3,b.X0,b.X1,y0+cab-.025,y0+cab+.025,D,-1,.03,.008);
                foreach(double x in new[]{b.X0,b.X1})foreach(double y in new[]{y0,y0+cab})Box(x-.07,x+.07,y-.07,y+.07,front-.02,front+.12,M,false);
                Face(b,3,b.Cx-.12,b.Cx+.12,y0+cab-.18,y0+cab-.1,NativeClubLooks.Led,0x22E6FF,.01,.03,false,GlowSoft);
            }
            foreach(int side in new[]{0,1})Face(b,side,b.Z0+.1,b.Z1-.1,b.Y0+.2,b.Y1-.2,G,-1,.012,.004);
        }
        private void Stage(Bx b)
        {
            family="stage";On(b);
            Face(b,4,b.X0+.06,b.X1-.06,b.Z0+.06,b.Z1-.06,NativeClubLooks.Rubber,-1,Decal,.004);
            Face(b,3,b.X0,b.X1,b.Y1-.09,b.Y1-.03,NativeClubLooks.Led,NativeClubLooks.Cyan,.02,.01,false,Glow);
            Face(b,3,b.X0,b.X1,b.Y0,b.Y0+.12,NativeClubLooks.Dark,-1,.02,.006);
            // a row of floor lamps along the lip (they wash the LED wall, the FX draws their beams)
            for(double x=b.X0+1;x<b.X1-.5;x+=2)Box(x-.14,x+.14,b.Y1,b.Y1+.1,b.Z0+.12,b.Z0+.4,NativeClubLooks.Dark,false);
        }
        private void Nosing(Bx b,string id)
        {
            family="nosing";On(b);
            // the nosing on the riser's top edge facing down the flight: grand stairs climb north (riser faces south), the bar
            // stairs climb west (their risers face east), the stage's front steps climb north, its back steps climb east
            int side=Starts(id,"club-stair-bar-w")?0:Starts(id,"club-stair-bar-e")?1:Starts(id,"club-stage-back-w")?1:Starts(id,"club-stage-back-e")?0:3;
            Span(b,side,out double u0,out double u1);Face(b,side,u0+.05,u1-.05,b.Y1-.04,b.Y1-.012,NativeClubLooks.Led,NativeClubLooks.Violet,.012,.006,false,GlowSoft);
        }
        private void Booth(Bx b)
        {
            family="dj-booth";On(b);
            Face(b,3,b.X0+.1,b.X1-.1,b.Y0+.12,b.Y1-.12,NativeClubLooks.Screen,NativeClubLooks.Violet,.02,.006,false,1.05);
            Face(b,3,b.X0,b.X1,b.Y1-.06,b.Y1-.02,NativeClubLooks.Led,NativeClubLooks.Cyan,.012,.01,false,Glow);
            double y=b.Y1;int D=NativeClubLooks.Dark,C=NativeClubLooks.Chrome;
            foreach(double x in new[]{-1.55,1.55}){Box(x-.3,x+.3,y,y+.06,b.Z0+.25,b.Z1-.25,D);Box(x-.21,x+.21,y+.06,y+.075,b.Cz-.21,b.Cz+.21,C,false);}
            Box(-.32,.32,y,y+.08,b.Z0+.3,b.Z1-.3,D);for(int k=0;k<6;k++)Box(-.25+k*.1-.015,-.25+k*.1+.015,y+.08,y+.095,b.Cz-.1,b.Cz+.1,NativeClubLooks.Led,false,k%2==0?NativeClubLooks.Cyan:NativeClubLooks.Magenta,GlowHot);
            Box(.55,1.0,y,y+.02,b.Z1-.45,b.Z1-.15,D);Box(.55,1.0,y+.02,y+.24,b.Z1-.17,b.Z1-.15,NativeClubLooks.Screen,false,0xC8F4FF,1.2);
        }
        private void Counter(Bx b)
        {
            family="bar-counter";On(b);
            Face(b,4,b.X0-.02,b.X1+.02,b.Z0-.06,b.Z1+.02,NativeClubLooks.Dark,0x0E0C14,.04,.04);
            Face(b,2,b.X0,b.X1,b.Y1-.1,b.Y1-.05,NativeClubLooks.Led,NativeClubLooks.Magenta,.015,.02,false,Glow);
            Box(b.X0+.1,b.X1-.1,.22,.27,b.Z1+.12,b.Z1+.17,NativeClubLooks.Brass,true);
            for(double x=b.X0+.6;x<b.X1-.4;x+=1.6)Box(x-.025,x+.025,.1,.27,b.Z1+.02,b.Z1+.17,NativeClubLooks.Brass,false);
            for(double x=b.X0+1.5;x<b.X1-1;x+=3.2){Box(x-.03,x+.03,b.Y1+.04,b.Y1+.24,b.Z0+.25,b.Z0+.31,NativeClubLooks.Chrome,false);Box(x-.1,x+.1,b.Y1+.18,b.Y1+.24,b.Z0+.2,b.Z0+.36,NativeClubLooks.Chrome,false);}
        }
        private void BackBar(Bx b)
        {
            family="backbar";On(b);double face=b.Z1;
            Face(b,2,b.X0+.1,b.X1-.1,.9,3.5,NativeClubLooks.Mirror,-1,.01,.004);
            int[] bottle={0x5AFF8A,0xFFB45E,0x6AC8FF,0xFF6FA0,0xE8E0C8};
            for(int s=0;s<3;s++)
            {
                double y=1.2+s*.75;Box(b.X0+.15,b.X1-.15,y-.03,y,face,face+.24,NativeClubLooks.Glass,false);Box(b.X0+.15,b.X1-.15,y-.05,y-.03,face+.18,face+.24,NativeClubLooks.Led,false,0xFFE6C8,GlowSoft);
                for(double x=b.X0+.3;x<b.X1-.25;x+=.17){double h=.22+.12*H((int)(x*10),s);Box(x-.035,x+.035,y,y+h,face+.05,face+.13,NativeClubLooks.Bottle,false,bottle[(int)(H((int)(x*10),s,3)*bottle.Length)%bottle.Length],1.1+.6*H((int)(x*10),s,5));}
            }
        }
        private void Fascia(Bx b,string id)
        {
            family="fascia";On(b);int L=NativeClubLooks.Led;int col=id=="club-catwalk"?NativeClubLooks.Magenta:NativeClubLooks.Cyan;
            // the deck's edges that face the hall: the mezzanines' inner side, the VIP's north edge and stairwells, both catwalk sides
            if(Starts(id,"club-mezz-w"))Face(b,0,b.Z0,b.Z1,b.Y0+.08,b.Y0+.14,L,col,.012,.006,false,Glow);
            else if(Starts(id,"club-mezz-e"))Face(b,1,b.Z0,b.Z1,b.Y0+.08,b.Y0+.14,L,col,.012,.006,false,Glow);
            else if(id=="club-catwalk"){Face(b,2,b.X0,b.X1,b.Y0+.08,b.Y0+.14,L,col,.012,.006,false,Glow);Face(b,3,b.X0,b.X1,b.Y0+.08,b.Y0+.14,L,col,.012,.006,false,Glow);
                for(double x=b.X0+1;x<b.X1;x+=2)Face(b,4,x-.4,x+.4,b.Cz-.04,b.Cz+.04,L,NativeClubLooks.Violet,.006,.002,false,GlowSoft);}
            else if(Starts(id,"club-vip-")){if(b.Z1>-14.7)Face(b,2,b.X0,b.X1,b.Y0+.08,b.Y0+.14,L,col,.012,.006,false,Glow);if(b.Z0>-18)Face(b,3,b.X0,b.X1,b.Y0+.08,b.Y0+.14,L,NativeClubLooks.Violet,.012,.006,false,GlowSoft);}
            else if(id.EndsWith("-landing",StringComparison.Ordinal)){Face(b,b.Cx<0?0:1,b.Z0,b.Z1,b.Y0+.08,b.Y0+.14,L,col,.012,.006,false,Glow);Face(b,3,b.X0,b.X1,b.Y0+.02,b.Y0+.08,L,NativeClubLooks.Violet,.012,.006,false,GlowSoft);}
        }
        private void Rail(Bx b,string id)
        {
            family="rail";On(b);bool alongX=b.X1-b.X0>=b.Z1-b.Z0;double t=.035;
            if(alongX){Box(b.X0,b.X1,b.Y1,b.Y1+.05,b.Cz-t,b.Cz+t,NativeClubLooks.Chrome,false);}else{Box(b.Cx-t,b.Cx+t,b.Y1,b.Y1+.05,b.Z0,b.Z1,NativeClubLooks.Chrome,false);}
            if(Starts(id,"club-barrier-"))return;
            // an LED line under the cap on both faces (the rails glow with the club)
            if(alongX){Face(b,2,b.X0,b.X1,b.Y1-.08,b.Y1-.04,NativeClubLooks.Led,NativeClubLooks.Violet,.008,.004,false,GlowSoft);Face(b,3,b.X0,b.X1,b.Y1-.08,b.Y1-.04,NativeClubLooks.Led,NativeClubLooks.Violet,.008,.004,false,GlowSoft);}
            else{Face(b,0,b.Z0,b.Z1,b.Y1-.08,b.Y1-.04,NativeClubLooks.Led,NativeClubLooks.Violet,.008,.004,false,GlowSoft);Face(b,1,b.Z0,b.Z1,b.Y1-.08,b.Y1-.04,NativeClubLooks.Led,NativeClubLooks.Violet,.008,.004,false,GlowSoft);}
        }
        // The south wall above the VIP: the club's neon (a bolt and "128") and the window's sill; the window frame is overhead.
        private void SouthWall(Bx b)
        {
            family="south-wall";On(b);double z=b.Z1;
            Face(b,2,b.X0,b.X1,b.Y1-.3,b.Y1,NativeClubLooks.Steel,-1,.06,.03,true);
            // the sign's backing and its strokes (a lightning bolt then 1, 2, 8 as segments), magenta and cyan neon
            Face(b,2,-5.2,5.2,5.0,7.0,NativeClubLooks.Dark,0x0A0810,.03,.012);
            // the sign reads from the hall looking south, where +x is on the left: each stroke is laid out left to right and mirrored
            void S(double x0,double y0,double x1,double y1,int c){x0=-x0;x1=-x1;double t=.07;if(Math.Abs(x1-x0)>Math.Abs(y1-y0))Box(Math.Min(x0,x1)-t/2,Math.Max(x0,x1)+t/2,y0-t/2,y0+t/2,z+.03,z+.09,NativeClubLooks.Sign,false,c,GlowHot);else Box(x0-t/2,x0+t/2,Math.Min(y0,y1)-t/2,Math.Max(y0,y1)+t/2,z+.03,z+.09,NativeClubLooks.Sign,false,c,GlowHot);}
            int m=NativeClubLooks.Magenta,c=NativeClubLooks.Cyan;
            // the bolt
            S(-4.4,6.7,-3.8,6.7,m);S(-3.8,6.7,-3.8,6.0,m);S(-4.4,6.0,-3.8,6.0,m);S(-4.4,6.0,-4.4,5.3,m);S(-4.4,5.3,-3.6,5.3,m);
            // 1
            S(-2.2,5.3,-2.2,6.7,c);S(-2.6,6.4,-2.2,6.7,c);
            // 2
            S(-1.2,6.7,.0,6.7,c);S(.0,6.7,.0,6.0,c);S(-1.2,6.0,.0,6.0,c);S(-1.2,6.0,-1.2,5.3,c);S(-1.2,5.3,.0,5.3,c);
            // 8
            S(1.2,6.7,2.4,6.7,c);S(1.2,6.0,2.4,6.0,c);S(1.2,5.3,2.4,5.3,c);S(1.2,5.3,1.2,6.7,c);S(2.4,5.3,2.4,6.7,c);
            // the underline
            S(3.4,5.3,4.6,5.3,m);S(3.4,5.6,4.6,5.6,m);
            // uplights along the wall's foot behind the bar
            for(double x=-22;x<=22.1;x+=4.4)if(Math.Abs(x)>4.5)Box(x-.15,x+.15,0,.12,z,z+.2,NativeClubLooks.Dark,false);
        }
        // The west and east walls: the old crane rail on corbels under the arches, a skirting, and the uplights at the foot.
        private void SideWall(Bx b,int east)
        {
            family="side-wall";On(b);int side=east==0?0:1;double f=east==0?b.X1:b.X0;
            Face(b,side,b.Z0+.4,b.Z1-.4,b.Y1-.55,b.Y1-.05,NativeClubLooks.Steel,-1,.18,.16,true);
            for(double z=-18;z<=18.1;z+=4)Face(b,side,z-.15,z+.15,b.Y1-1.0,b.Y1-.55,NativeClubLooks.Steel,-1,.14,.12,true);
            Face(b,side,b.Z0+.4,b.Z1-.4,0,.18,NativeClubLooks.Dark,-1,.03,.012);
            for(double z=-17;z<=17.1;z+=3.4){double s=east==0?1:-1;Box(f,f+s*.2,0,.12,z-.15,z+.15,NativeClubLooks.Dark,false);}
        }
        private void Pier(Bx b,int east)
        {
            family="pier";On(b);int side=east==0?0:1;
            Face(b,side,b.Z0,b.Z1,b.Y0,b.Y0+.18,NativeClubLooks.Steel,-1,.06,.03);Face(b,side,b.Z0+.1,b.Z1-.1,b.Y1-.5,b.Y1-.3,NativeClubLooks.Brick,0x7A4A40,.05,.03);
            // the arches' lit reveals: a violet LED line up each pier's edges facing the arch (and the gallery's light through it)
            double x=east==0?b.X1+.012:b.X0-.012;foreach(double z in new[]{b.Z0+.05,b.Z1-.05})if(z>-19.9&&z<19.9)Box(x-.012,x+.012,b.Y0+.2,b.Y1-.6,z-.02,z+.02,NativeClubLooks.Led,false,NativeClubLooks.Violet,Glow);
        }
        private void LedWallFrame(Bx b)
        {
            family="led-wall";On(b);int D=NativeClubLooks.Dark;
            Face(b,3,b.X0,b.X1,NativeClubLooks.WallY0-.1,NativeClubLooks.WallY0,D,-1,.06,.03);Face(b,3,b.X0,b.X1,b.Y1-.12,b.Y1,D,-1,.06,.03);
            Face(b,3,b.X0,b.X0+.12,NativeClubLooks.WallY0,b.Y1,D,-1,.06,.03);Face(b,3,b.X1-.12,b.X1,NativeClubLooks.WallY0,b.Y1,D,-1,.06,.03);
        }
        private void Column(Bx b)
        {
            family="column";On(b);
            Box(b.X0-.08,b.X1+.08,b.Y0,b.Y0+.35,b.Z0-.08,b.Z1+.08,NativeClubLooks.Steel);Box(b.X0-.05,b.X1+.05,b.Y0+.35,b.Y0+.42,b.Z0-.05,b.Z1+.05,NativeClubLooks.Brass,false);
            if(b.Y1>8){Box(b.X0-.1,b.X1+.1,b.Y1-.6,b.Y1-.4,b.Z0-.1,b.Z1+.1,NativeClubLooks.Steel);Box(b.X0-.12,b.X1+.12,b.Y1-.25,b.Y1,b.Z0-.12,b.Z1+.12,NativeClubLooks.Steel);}
            else Box(b.X0-.06,b.X1+.06,b.Y1-.25,b.Y1,b.Z0-.06,b.Z1+.06,NativeClubLooks.Steel);
        }
        private void Podium(Bx b)
        {
            family="podium";On(b);int L=NativeClubLooks.Led;double t=.05;
            Face(b,4,b.X0+.08,b.X1-.08,b.Z0+.08,b.Z1-.08,NativeClubLooks.Screen,NativeClubLooks.Violet,Decal,.004,false,1.3);
            foreach(int side in new[]{0,1,2,3}){Span(b,side,out double u0,out double u1);Face(b,side,u0,u1,b.Y1-t-.03,b.Y1-.03,L,NativeClubLooks.Magenta,.012,.006,false,Glow);Face(b,side,u0,u1,b.Y0+.03,b.Y0+.06,L,NativeClubLooks.Cyan,.012,.006,false,GlowSoft);}
        }
        private void CubeFrame(Bx b)
        {
            family="cube";On(b);double t=.06;int C=NativeClubLooks.Chrome;
            foreach(var (sx,sz) in new[]{(0,0),(1,0),(0,1),(1,1)}){double x=sx==0?b.X0:b.X1,z=sz==0?b.Z0:b.Z1;Box(x-t,x+t,b.Y0-.02,b.Y1+.02,z-t,z+t,C,false);}
            foreach(double y in new[]{b.Y0,b.Y1}){Box(b.X0,b.X1,y-t,y+t,b.Z0-t,b.Z0+t,C,false);Box(b.X0,b.X1,y-t,y+t,b.Z1-t,b.Z1+t,C,false);Box(b.X0-t,b.X0+t,y-t,y+t,b.Z0,b.Z1,C,false);Box(b.X1-t,b.X1+t,y-t,y+t,b.Z0,b.Z1,C,false);}
        }
        private void Upholstery(Bx b)
        {
            family="upholstery";On(b);bool alongX=b.X1-b.X0>=b.Z1-b.Z0;double h=b.Y1-b.Y0;int V=NativeClubLooks.Velvet;
            // the seat's cushion line and buttoned tufting on its long faces
            for(int side=0;side<4;side++)
            {
                if(alongX!=(side>=2))continue;Span(b,side,out double u0,out double u1);
                Face(b,side,u0+.03,u1-.03,b.Y0+h*.42,b.Y0+h*.42+.03,V,0x1A0612,.008,.004);
                for(double u=u0+.25;u<u1-.2;u+=.35)for(double y=b.Y0+h*.6;y<b.Y1-.1;y+=.22)Face(b,side,u-.02,u+.02,y-.02,y+.02,NativeClubLooks.Brass,-1,.008,.006);
            }
            Face(b,4,b.X0+.04,b.X1-.04,b.Z0+.04,b.Z1-.04,V,-1,Decal,.004);
        }
        private void Pillar(Bx b)
        {
            family="pillar";On(b);
            foreach(int side in new[]{0,1,2,3}){Span(b,side,out double u0,out double u1);Face(b,side,u0+.1,u1-.1,1.0,3.4,NativeClubLooks.Led,NativeClubLooks.Violet,.012,.004,false,.9);}
        }
        // The green room: the dressing mirror with its bulbs on the inside of the south wall.
        private void Mirror(Bx b)
        {
            family="mirror";On(b);double z=b.Z1;double x0=b.X0+1.0,x1=b.X0+3.4;
            Face(b,2,x0,x1,1.1,1.9,NativeClubLooks.Mirror,-1,.01,.004);
            for(double x=x0+.1;x<x1;x+=.3){Box(x-.03,x+.03,1.94,2.0,z,z+.05,NativeClubLooks.Lens,false,0xFFF0D8,GlowSoft);}
            for(double y=1.15;y<1.9;y+=.25){Box(x0-.06,x0,y-.03,y+.03,z,z+.05,NativeClubLooks.Lens,false,0xFFF0D8,GlowSoft);Box(x1,x1+.06,y-.03,y+.03,z,z+.05,NativeClubLooks.Lens,false,0xFFF0D8,GlowSoft);}
        }
        // The floor: the dance floor's LED border, the laser wall's slots, the drop zones' hatching in front of the stacks.
        private void Floor()
        {
            family="floor";if(!Get("club-floor-w",out var fw)||!Get("club-floor-n",out var fn)||!Get("club-floor-s",out var fs)||!Get("club-floor-e",out var fe))return;
            double x0=NativeClubLooks.FloorX0,x1=NativeClubLooks.FloorX1,z0=NativeClubLooks.FloorZ0,z1=NativeClubLooks.FloorZ1,w=.12;int L=NativeClubLooks.Led;
            On(fn);Face(fn,4,x0,x1,z1+.02,z1+.02+w,L,NativeClubLooks.Cyan,Decal,.003,false,Glow);
            Face(fn,4,x0,x1,NativeClubLooks.LaserRailZ-.1,NativeClubLooks.LaserRailZ+.1,NativeClubLooks.Dark,0x08080A,Decal,.002);
            On(fs);Face(fs,4,x0,x1,z0-.02-w,z0-.02,L,NativeClubLooks.Cyan,Decal,.003,false,Glow);
            Face(fs,4,x0,x1,-NativeClubLooks.LaserRailZ-.1,-NativeClubLooks.LaserRailZ+.1,NativeClubLooks.Dark,0x08080A,Decal,.002);
            On(fw);Face(fw,4,x0-.02-w,x0-.02,z0-.02-w,z1+.02+w,L,NativeClubLooks.Cyan,Decal,.003,false,Glow);
            On(fe);Face(fe,4,x1+.02,x1+.02+w,z0-.02-w,z1+.02+w,L,NativeClubLooks.Cyan,Decal,.003,false,Glow);
            // the drop zones: diagonal hazard hatching in a frame where the stacks' blast reaches (the vents' columns), laid on
            // the side slab and the north slab either side of x = ±9
            foreach(double sx in new[]{-1.0,1.0})
            {
                double lo=Math.Min(sx*NativeClubLooks.SpeakerX0,sx*NativeClubLooks.SpeakerX1),hi=Math.Max(sx*NativeClubLooks.SpeakerX0,sx*NativeClubLooks.SpeakerX1),za=9.5,zb=NativeClubLooks.SpeakerZ;
                foreach(var (b,u0,u1) in new[]{(sx<0?fw:fe,sx<0?lo:9.0,sx<0?-9.0:hi),(fn,sx<0?-9.0:lo,sx<0?hi:9.0)})
                {
                    On(b);Face(b,4,u0,u1,za,za+.1,NativeClubLooks.Paint,0xE8E4F0,Decal,.003);
                    for(double z=za+.35;z<zb-.2;z+=.5)Face(b,4,Math.Max(u0,lo+.2),Math.Min(u1,hi-.2),z,z+.16,NativeClubLooks.Hazard,-1,Decal,.003);
                }
                On(sx<0?fw:fe);Face(sx<0?fw:fe,4,sx<0?lo:hi-.1,sx<0?lo+.1:hi,za,zb,NativeClubLooks.Paint,0xE8E4F0,Decal,.003);
                On(fn);Face(fn,4,sx<0?hi-.1:lo,sx<0?hi:lo+.1,za,zb,NativeClubLooks.Paint,0xE8E4F0,Decal,.003);
            }
        }

        // ---------------- overhead: the lighting truss, the hangers, the window frame ----------------
        private void Truss()
        {
            family="truss";Above();double X=NativeClubLooks.TrussX,Z=NativeClubLooks.TrussZ,y=NativeClubLooks.TrussY,d=NativeClubLooks.TrussDepth;
            BoxTruss(-X,y,-Z,X,y,-Z,d);BoxTruss(-X,y,Z,X,y,Z,d);BoxTruss(-X,y,-Z,-X,y,Z,d);BoxTruss(X,y,-Z,X,y,Z,d);BoxTruss(-X,y,0,X,y,0,d);BoxTruss(0,y,-Z,0,y,Z,d);
            // the cube on four chains from the truss, a motor at each; the mirror balls on their motors and drop cables
            family="hanger";double cy=NativeClubLooks.CubeY+NativeClubLooks.CubeHalfY,ch=NativeClubLooks.CubeHalf-.2;
            foreach(var (sx,sz) in new[]{(-1,-1),(1,-1),(-1,1),(1,1)}){Bar(sx*ch,cy,sz*ch,sx*.25,y-d/2,sz*.25,.03,NativeClubLooks.Cable,false);}
            Box(-.35,.35,y-d/2-.3,y-d/2,-.35,.35,NativeClubLooks.Dark);
            foreach(double sx in new[]{-1.0,1.0})
            {
                double bx=sx*NativeClubLooks.BallX;Box(bx-.18,bx+.18,y-d/2-.28,y-d/2,-.18,.18,NativeClubLooks.Dark);
                Bar(bx,y-d/2-.28,0,bx,NativeClubLooks.BallY+NativeClubLooks.BallRadius,0,.02,NativeClubLooks.Cable,false);
            }
            // the moving heads' clamps on the truss (the heads themselves are the rig's)
            family="clamp";foreach(double z in new[]{-Z,Z})foreach(double x in NativeClubLooks.HeadX)Box(x-.12,x+.12,y-d/2-.12,y-d/2,z-.12,z+.12,NativeClubLooks.Dark);
            // the great south window's frame over the south wall (its top is out of reach: nothing standable under it)
            family="window";if(Get("club-wall-s",out var ws))
            {
                double z0=ws.Z0,z1=ws.Z1,top=12.0,sill=ws.Y1;
                for(double x=ws.X0;x<=ws.X1+.01;x+=(ws.X1-ws.X0)/12)Box(x-.09,x+.09,sill,top,z0+.08,z1-.08,NativeClubLooks.Steel);
                foreach(double yy in new[]{sill+1.5,sill+3.0})Box(ws.X0,ws.X1,yy-.05,yy+.05,z0+.12,z1-.12,NativeClubLooks.Steel);
                Box(ws.X0,ws.X1,top-.25,top,z0,z1,NativeClubLooks.Steel);
            }
        }
        // A square box truss from a to b at its chords' centre height, `d` deep: four chords, zig-zag lacing on its four faces.
        private void BoxTruss(double ax,double ay,double az,double bx,double by,double bz,double d)
        {
            double h=d/2,t=.05,dx=bx-ax,dz=bz-az,len=Math.Sqrt(dx*dx+dz*dz);if(len<.1)return;double ux=dx/len,uz=dz/len,px=-uz,pz=ux;int K=NativeClubLooks.Truss;
            foreach(var (sy,sp) in new[]{(-1,-1),(-1,1),(1,-1),(1,1)})Bar(ax+px*sp*h,ay+sy*h,az+pz*sp*h,bx+px*sp*h,by+sy*h,bz+pz*sp*h,t,K,true);
            int bays=Math.Max(1,(int)Math.Round(len/.6));double step=len/bays;
            for(int k=0;k<bays;k++)
            {
                double s0=k*step,s1=(k+1)*step;bool flip=(k&1)==0;double a0=flip?s0:s1,a1=flip?s1:s0;
                foreach(int sp in new[]{-1,1})Bar(ax+ux*a0+px*sp*h,ay-h,az+uz*a0+pz*sp*h,ax+ux*a1+px*sp*h,ay+h,az+uz*a1+pz*sp*h,.025,K,false);
                foreach(int sy in new[]{-1,1})Bar(ax+ux*a0-px*h,ay+sy*h,az+uz*a0-pz*h,ax+ux*a1+px*h,ay+sy*h,az+uz*a1+pz*h,.025,K,false);
            }
        }
    }
}
