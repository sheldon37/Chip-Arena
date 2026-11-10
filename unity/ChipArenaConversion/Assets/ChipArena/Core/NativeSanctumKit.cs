using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's static dressing (docs/arenas/SANCTUM.md "The kit"), drawn as NativeBlockLit instances by
    /// Runtime/NativeSanctumPresentation.cs: never sim data and never a collider. Two families, as the stage kits keep them:
    /// <list type="bullet">
    /// <item>attached detail on the blocks (<see cref="Attached"/>): the piers' shafts, bases and capitals, the walls' plinths,
    /// string courses and wall shafts, the windows' tracery inside their openings, the galleries' fascias and beamed soffits,
    /// the balustrades' copings and quatrefoils, the stairs' nosings, the pews' ends and backs, the screens' arcading, the
    /// altars' cloths and candlesticks, the tombs' effigies, the confessionals' curtains, the saints in relief, the crypt's
    /// niches. Every part lies inside its block grown by 0.25 m (the stage rule: below 3 m nothing stands proud of what it
    /// dresses by more), most within a few centimetres of a face;</item>
    /// <item>the overhead structure no block stands for (<see cref="Overhead"/>): the ribbed vaults, their bosses, the oculus
    /// ring and the censer's yoke, the belfry and its louvres over the oculus, the organ's pipe towers above the impost, the
    /// hanging sigil over each rood loft. Each stands at least 2 m over every standable top under it.</item>
    /// </list>
    /// It also lists the candles (<see cref="Candles"/>: wax tip x, y, z, flame size, seed) the effects light and the warm
    /// light sources (<see cref="Lights"/>) the practical slots and the light volume's bake choose from. Deterministic: the same
    /// blocks and looks give the same parts. Build allocates once per map; nothing here runs per frame.
    /// </summary>
    public sealed class NativeSanctumKit
    {
        public const byte Attached=0,Overhead=1;
        public readonly NativeFactoryKit.Writer Parts;public readonly List<byte> Role=new List<byte>();
        /// <summary>Candle flames: x, y, z (the wick), size (m), seed (0..1), per candle.</summary>
        public readonly List<double> Candles=new List<double>();
        /// <summary>Warm light sources: x, y, z, range (m), linear r, g, b times intensity, per source.</summary>
        public readonly List<double> Lights=new List<double>();
        private byte role;
        private NativeSanctumKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(4096,true,looks);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true,int color=-1,double window=1){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color,window);if(Parts.Count>n)Role.Add(role);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true,int color=-1,double window=1)
        {if(x1<x0){var t=x0;x0=x1;x1=t;}if(y1<y0){var t=y0;y0=y1;y1=t;}if(z1<z0){var t=z0;z0=z1;z1=t;}Add((x0+x1)/2,(y0+y1)/2,(z0+z1)/2,(x1-x0)/2,(y1-y0)/2,(z1-z0)/2,FactoryQ.Identity,kind,shadow,color,window);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double tx,double ty,int kind,bool shadow=true,int color=-1){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,tx,ty,kind,shadow,color);if(Parts.Count>n)Role.Add(role);}
        private void Candle(double x,double y,double z,double tall,double r=.035,bool lit=true)
        {
            Box(x-r,x+r,y,y+tall,z-r,z+r,NativeSanctumLooks.Wax,false);if(!lit)return;
            double s=NativeSanctumLooks.Hash((int)Math.Round(x*97),(int)Math.Round(y*89),(int)Math.Round(z*83));Candles.Add(x);Candles.Add(y+tall+.01);Candles.Add(z);Candles.Add(.11+.05*s);Candles.Add(s);
        }
        private void Light(double x,double y,double z,double range,int hex,double intensity){Lights.Add(x);Lights.Add(y);Lights.Add(z);Lights.Add(range);Lights.Add(NativeSceneryBuilder.Channel(hex,16)*intensity);Lights.Add(NativeSceneryBuilder.Channel(hex,8)*intensity);Lights.Add(NativeSceneryBuilder.Channel(hex,0)*intensity);}
        public const int Warm=0xFFB468;

        private struct B {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;public double Sx=>X1-X0;public double Sz=>Z1-Z0;}
        private static bool Starts(string id,string prefix)=>id!=null&&id.StartsWith(prefix,StringComparison.Ordinal);

        /// <summary>The kit for the sanctum's blocks with these looks (another map: an empty kit).</summary>
        public static NativeSanctumKit Build(ContentMap map,NativeFactoryKit.Looks looks)
        {
            var k=new NativeSanctumKit(looks);if(map==null||!NativeSanctumLooks.Active(map.id))return k;var all=new List<B>();
            foreach(var b in map.blocks??Array.Empty<ContentBlock>())if(b?.id!=null)all.Add(new B{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id});
            k.role=Attached;
            var flights=new Dictionary<string,List<B>>();
            foreach(var b in all)
            {
                string id=b.Id;
                if(Starts(id,"sanctum-pier-cross-"))k.Pier(b,true);
                else if(Starts(id,"sanctum-pier-"))k.Pier(b,false);
                else if(Starts(id,"sanctum-column-"))k.LoftColumn(b);
                else if(Starts(id,"sanctum-wall-"))k.Wall(b);
                else if(Starts(id,"sanctum-gallery-"))k.Gallery(b,all);
                else if(Starts(id,"sanctum-balustrade-"))k.Balustrade(b,all);
                else if(Starts(id,"sanctum-stair-")){int dash=id.LastIndexOf('-');string key=id.Substring(0,dash);if(!flights.TryGetValue(key,out var list))flights[key]=list=new List<B>();list.Add(b);}
                else if(Starts(id,"sanctum-pew-"))k.Pew(b);
                else if(Starts(id,"sanctum-screen-"))k.Screen(b);
                else if(Starts(id,"sanctum-altar-"))k.Altar(b);
                else if(Starts(id,"sanctum-confessional-"))k.Confessional(b);
                else if(Starts(id,"sanctum-plinth-"))k.Saint(b);
                else if(Starts(id,"sanctum-tomb-low-")||Starts(id,"sanctum-crypt-tomb-"))k.LowTomb(b);
                else if(Starts(id,"sanctum-tomb-"))k.FounderTomb(b);
                else if(Starts(id,"sanctum-crypt-pillar-"))k.CryptPillar(b);
                else if(id=="sanctum-crypt-shrine")k.Shrine(b);
                else if(Starts(id,"sanctum-crypt-wall-"))k.CryptWall(b);
            }
            foreach(var f in flights)k.Flight(f.Value);
            k.FloorMarks(map);k.Tracery();k.CryptVault();
            k.role=Overhead;k.Vaults();k.Belfry();k.Organ();k.Retable();k.Sigils();
            return k;
        }

        // ---------------- piers, columns ----------------
        // An arcade pier (1.4 m, round in the style) gets four attached shafts on its faces, a moulded base and a capital band at
        // the springing; a crossing pier (1.8 m) eight shafts. Candles stand on some plinths.
        private void Pier(B b,bool crossing)
        {
            double cx=b.Cx,cz=b.Cz,hx=b.Sx/2,hz=b.Sz/2,spring=crossing?12.0:13.0,r=.13;int S=NativeSanctumLooks.Stone,T=NativeSanctumLooks.Trim;
            Box(cx-hx-.12,cx+hx+.12,0,.34,cz-hz-.12,cz+hz+.12,T);Box(cx-hx-.06,cx+hx+.06,.34,.5,cz-hz-.06,cz+hz+.06,T);
            void Shaft(double sx,double sz){Bar(sx,.5,sz,sx,spring,sz,2*r,2*r,S);Box(sx-r-.05,sx+r+.05,spring,spring+.28,sz-r-.05,sz+r+.05,T);Box(sx-r-.04,sx+r+.04,.5,.62,sz-r-.04,sz+r+.04,T);}
            Shaft(cx-hx-r+.03,cz);Shaft(cx+hx+r-.03,cz);Shaft(cx,cz-hz-r+.03);Shaft(cx,cz+hz+r-.03);
            if(crossing)foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Shaft(cx+sx*(hx-.02),cz+sz*(hz-.02));
            Box(cx-hx-.2,cx+hx+.2,spring+.28,spring+.62,cz-hz-.2,cz+hz+.2,T);Box(cx-hx-.1,cx+hx+.1,spring+.62,spring+.74,cz-hz-.1,cz+hz+.1,S);
            // the pier's candles: a cluster on the plinth's nave side for the arcade's every other pier, a ring for the crossing
            double h=NativeSanctumLooks.Hash((int)Math.Round(cx*10),(int)Math.Round(cz*10),7);
            if(crossing){double face=-Math.Sign(cz)*(hz+.04);for(int i=0;i<7;i++){double u=(i-3)*.12;Candle(cx+u,.5,cz+face,.1+.22*NativeSanctumLooks.Hash(i,(int)(cx*5),3),.03);}Light(cx,1.0,cz-Math.Sign(cz)*(hz+.6),5,Warm,.6);}
            else if(h<.6){double face=-Math.Sign(cz)*(hz+.05);for(int i=0;i<5;i++){double u=(i-2)*.12;Candle(cx+u,.5,cz+face,.08+.2*NativeSanctumLooks.Hash(i,(int)(cx*7),5),.03);}}
        }
        private void LoftColumn(B b)
        {
            double cx=b.Cx,cz=b.Cz,hx=b.Sx/2,hz=b.Sz/2;int T=NativeSanctumLooks.Trim;
            Box(cx-hx-.1,cx+hx+.1,0,.3,cz-hz-.1,cz+hz+.1,T);Box(cx-hx-.14,cx+hx+.14,b.Y1-.42,b.Y1,cz-hz-.14,cz+hz+.14,T);
        }

        // ---------------- walls and their windows ----------------
        // Every wall piece: a plinth on its inner face at the foot, the string course under the sills (8 m), wall shafts at the
        // bay lines rising to the vault's springing; a window's jambs, sill, mullion and pointed head (lancets) or its rose's
        // spandrels, rings and spokes inside the opening (the glass itself is the scenery's).
        private void Wall(B b)
        {
            int S=NativeSanctumLooks.Stone,T=NativeSanctumLooks.Trim;bool alongX=b.Sx>=b.Sz;double inner;bool aisle=b.Id.Contains("-aisle-"),armEnd=b.Id.Contains("-armend-"),end=b.Id.Contains("-end-");
            if(alongX){inner=b.Z0>0?b.Z0:b.Z1;double s=b.Z0>0?-1:1;
                if(b.Y0<.1){Box(b.X0,b.X1,0,.45,inner,inner+s*.12,T);Box(b.X0,b.X1,.45,.55,inner,inner+s*.06,T);}
                if(b.Y0<8&&b.Y1>=7.99){Box(b.X0,b.X1,7.7,8.0,inner,inner+s*.18,T);}
                if(b.Y0<.1&&aisle)foreach(double x in new[]{-24.0,-18,-12,12,18,24})if(x>b.X0+.3&&x<b.X1-.3){Bar(x,.55,inner+s*.14,x,15.5,inner+s*.14,.26,.26,S);Box(x-.22,x+.22,15.5,15.8,inner,inner+s*.3,T);}
                if(b.Y0<.1&&aisle){Arcade(b.X0,b.X1,inner,s,.6,2.9,true);Arcade(b.X0,b.X1,inner,s,4.4,7.3,true);}
            }
            else{inner=b.X0>0?b.X0:b.X1;double s=b.X0>0?-1:1;
                if(b.Y0<.1){Box(inner,inner+s*.12,0,.45,b.Z0,b.Z1,T);Box(inner,inner+s*.06,.45,.55,b.Z0,b.Z1,T);if(b.Id.Contains("-arm-"))Arcade(b.Z0,b.Z1,inner,s,.6,3.6,false);}
                if(b.Y0<8&&b.Y1>=7.99&&!end){Box(inner,inner+s*.18,7.7,8.0,b.Z0,b.Z1,T);}
            }
        }
        // Blind arcading along a wall face (u along x when `alongX`): dark pointed niches 1.1 m wide between colonnettes, every 1.5 m.
        private void Arcade(double u0,double u1,double face,double s,double y0,double y1,bool alongX)
        {
            int D=NativeSanctumLooks.StoneDark,T=NativeSanctumLooks.Trim;int n=(int)Math.Floor((u1-u0-.3)/1.5);if(n<1)return;double pitch=(u1-u0)/n,head=y1-.55;
            void P(double a0,double a1,double b0,double b1,double d,int kind,bool shadow){if(alongX)Box(a0,a1,b0,b1,face,face+s*d,kind,shadow);else Box(face,face+s*d,b0,b1,a0,a1,kind,shadow);}
            for(int i=0;i<n;i++)
            {
                double c=u0+pitch*(i+.5),w=pitch*.36;P(c-w,c+w,y0,head,.015,D,false);
                for(int r=0;r<4;r++){double va=head+.55*r/4,vb=head+.55*(r+1)/4,hw=w*Math.Sqrt(Math.Max(0,1-((r+.5)/4.2)*((r+.5)/4.2)));P(c-hw,c+hw,va,vb,.015,D,false);}
                double cu=u0+pitch*i;if(i>0)P(cu-.06,cu+.06,y0,y1,.09,T,false);
            }
            P(u0,u1,y1,y1+.12,.1,T,false);P(u0,u1,y0-.1,y0,.08,T,false);
        }
        /// <summary>Each window's tracery, once (called from the overhead pass: the openings are the windows' own boxes).</summary>
        private void Tracery()
        {
            foreach(var w in NativeSanctumLooks.Windows){if(w.Rose)Rose(w);else Lancet(w);}
        }
        // Local frame: u along the wall (unit (ux, uz)), the inward normal (nx, nz); the tracery plane sits .18 m outward of the
        // wall's middle, the glass's plane .3 m (scenery).
        private void WallPart(in NativeSanctumLooks.Window w,double u0,double u1,double v0,double v1,double d0,double d1,int kind,bool shadow=true)
        {
            double ux=w.Nz!=0?1:0,uz=w.Nx!=0?1:0;double cx=w.X,cz=w.Z;
            double ax=cx+ux*u0-w.Nx*d0,bx=cx+ux*u1-w.Nx*d1,az=cz+uz*u0-w.Nz*d0,bz=cz+uz*u1-w.Nz*d1;Box(ax,bx,v0,v1,az,bz,kind,shadow);
        }
        private void Lancet(in NativeSanctumLooks.Window w)
        {
            int S=NativeSanctumLooks.Stone,T=NativeSanctumLooks.Trim;double hw=w.Half,y0=w.Y-w.HalfY,y1=w.Y+w.HalfY,spring=y1-1.6;
            // the splayed jambs and sill (inside the opening's depth), the mullion, the pointed head's spandrels in rows
            WallPart(w,-hw,-hw+.14,y0,spring,-.4,.4,T);WallPart(w,hw-.14,hw,y0,spring,-.4,.4,T);WallPart(w,-hw,hw,y0,y0+.16,-.4,.4,T);
            WallPart(w,-.09,.09,y0,spring+.55,.1,.26,S);
            double L=hw-.14,H=1.6,c=(H*H-L*L)/(2*L),R=L+c;
            for(int i=0;i<8;i++)
            {
                double va=spring+H*i/8,vb=spring+H*(i+1)/8,v=(va+vb)/2-spring;double a=Math.Max(0,Math.Min(L,Math.Sqrt(Math.Max(0,R*R-v*v))-c));
                if(L-a>.02){WallPart(w,-hw,-a,va,vb,-.4,.4,S);WallPart(w,a,hw,va,vb,-.4,.4,S);}
            }
            // the two lights' heads under the main arch and a quatrefoil in its eye
            for(int s=-1;s<=1;s+=2){double cu=s*(L/2+.02),l2=L/2-.07;for(int i=0;i<4;i++){double va=spring+.0+.55*i/4,vb=spring+.55*(i+1)/4,v=(va+vb)/2-spring;double a=l2*Math.Sqrt(Math.Max(0,1-(v/.6)*(v/.6)));WallPart(w,cu-l2,cu-a,va,vb,.1,.26,S);WallPart(w,cu+a,cu+l2,va,vb,.1,.26,S);}}
            double ey=spring+1.02;foreach(var (du,dv) in new[]{(0.0,.2),(0.0,-.2),(.2,0.0),(-.2,0.0)})WallPart(w,du-.07,du+.07,ey+dv-.07,ey+dv+.07,.1,.26,S);
            // candles on the sill under the glass
            double ux=w.Nz!=0?1:0,uz=w.Nx!=0?1:0;for(int i=-1;i<=1;i++){double px=w.X+ux*i*.55+w.Nx*.25,pz=w.Z+uz*i*.55+w.Nz*.25;Candle(px,y0+.16,pz,.14+.06*Math.Abs(i),.03);}
        }
        private void Rose(NativeSanctumLooks.Window w)
        {
            int S=NativeSanctumLooks.Stone,T=NativeSanctumLooks.Trim;double half=w.Half,r=half-.18;int rows=22;
            for(int i=0;i<rows;i++)
            {
                double va=-half+2*half*i/rows,vb=-half+2*half*(i+1)/rows,v=(va+vb)/2;double a=Math.Sqrt(Math.Max(0,r*r-v*v));
                if(half-a>.02){WallPart(w,-half,-a,w.Y+va,w.Y+vb,-.4,.4,S);WallPart(w,a,half,w.Y+va,w.Y+vb,-.4,.4,S);}
            }
            double ux=w.Nz!=0?1:0,uz=w.Nx!=0?1:0;double d=.18;
            void P(double u,double v,out double x,out double y,out double z){x=w.X+ux*u-w.Nx*d;z=w.Z+uz*u-w.Nz*d;y=w.Y+v;}
            void Ring(double rr,int n,double t){for(int i=0;i<n;i++){double a0=2*Math.PI*i/n,a1=2*Math.PI*(i+1)/n;P(rr*Math.Cos(a0),rr*Math.Sin(a0),out double x0,out double y0,out double z0);P(rr*Math.Cos(a1),rr*Math.Sin(a1),out double x1,out double y1,out double z1);Bar(x0,y0,z0,x1,y1,z1,t,.2,T,true);}}
            Ring(r-.06,32,.16);Ring(r*.62,24,.12);Ring(r*.26,16,.12);
            int spokes=w.Design%2==0?12:16;
            for(int i=0;i<spokes;i++){double a=2*Math.PI*(i+.5)/spokes;P(r*.26*Math.Cos(a),r*.26*Math.Sin(a),out double x0,out double y0,out double z0);P((r-.1)*Math.Cos(a),(r-.1)*Math.Sin(a),out double x1,out double y1,out double z1);Bar(x0,y0,z0,x1,y1,z1,.1,.18,T,true);}
            // cusped petals between the middle and outer rings
            for(int i=0;i<spokes;i++){double a=2*Math.PI*i/spokes,rr=r*.81;P(rr*Math.Cos(a),rr*Math.Sin(a),out double x,out double y,out double z);double rp=r*.15;for(int j=0;j<6;j++){double b0=2*Math.PI*j/6,b1=2*Math.PI*(j+1)/6;P(rr*Math.Cos(a)+rp*Math.Cos(b0),rr*Math.Sin(a)+rp*Math.Sin(b0),out double x0,out double y0,out double z0);P(rr*Math.Cos(a)+rp*Math.Cos(b1),rr*Math.Sin(a)+rp*Math.Sin(b1),out double x1,out double y1,out double z1);Bar(x0,y0,z0,x1,y1,z1,.07,.12,S,false);}}
            P(0,0,out double cx,out double cy,out double cz);Add(cx,cy,cz,.22,.22,.22,FactoryQ.Axis(w.Nx,0,w.Nz,Math.PI/4),NativeSanctumLooks.Gold);
        }

        // ---------------- galleries, balustrades, stairs ----------------
        private void Gallery(B b,List<B> all)
        {
            int T=NativeSanctumLooks.Trim,O=NativeSanctumLooks.OakDark;
            // the soffit's oak beams across the narrow way, every 1.5 m, and the fascia over every open edge
            bool alongX=b.Sx>=b.Sz;
            if(alongX){for(double x=b.X0+.75;x<b.X1-.3;x+=1.5)Box(x-.12,x+.12,b.Y0-.24,b.Y0,b.Z0+.05,b.Z1-.05,O);}
            else{for(double z=b.Z0+.75;z<b.Z1-.3;z+=1.5)Box(b.X0+.05,b.X1-.05,b.Y0-.24,b.Y0,z-.12,z+.12,O);}
            void Fascia(bool x,double at,double s,double from,double to){if(to-from<.2)return;if(x)Box(at,at+s*.06,b.Y0-.05,b.Y1+.02,from,to,T);else Box(from,to,b.Y0-.05,b.Y1+.02,at,at+s*.06,T);}
            if(!Covered(all,b.X0-.05,b.Y0+.2,b.Cz,b))Fascia(true,b.X0,-1,b.Z0,b.Z1);if(!Covered(all,b.X1+.05,b.Y0+.2,b.Cz,b))Fascia(true,b.X1,1,b.Z0,b.Z1);
            if(!Covered(all,b.Cx,b.Y0+.2,b.Z0-.05,b))Fascia(false,b.Z0,-1,b.X0,b.X1);if(!Covered(all,b.Cx,b.Y0+.2,b.Z1+.05,b))Fascia(false,b.Z1,1,b.X0,b.X1);
        }
        private static bool Covered(List<B> all,double x,double y,double z,B self){foreach(var o in all){if(o.Id==self.Id)continue;if(x>o.X0&&x<o.X1&&y>o.Y0&&y<o.Y1&&z>o.Z0&&z<o.Z1)return true;}return false;}
        // A balustrade: a moulded coping and base, the faces in panels of blind quatrefoils (dark lozenges in a frame), votive
        // candles along the coping in twos and threes.
        private void Balustrade(B b,List<B> all)
        {
            int T=NativeSanctumLooks.Trim,D=NativeSanctumLooks.StoneDark;bool alongX=b.Sx>=b.Sz;double len=alongX?b.Sx:b.Sz,t=alongX?b.Sz:b.Sx;
            Box(b.X0-.05,b.X1+.05,b.Y1-.08,b.Y1+.04,b.Z0-.05,b.Z1+.05,T);Box(b.X0-.04,b.X1+.04,b.Y0,b.Y0+.12,b.Z0-.04,b.Z1+.04,T);
            int n=Math.Max(1,(int)Math.Round(len/.75));double step=len/n;
            for(int i=0;i<n;i++)
            {
                double u=(alongX?b.X0:b.Z0)+step*(i+.5),cy=(b.Y0+b.Y1)/2-.02;
                foreach(int side in new[]{-1,1})
                {
                    double face=alongX?(side<0?b.Z0:b.Z1):(side<0?b.X0:b.X1);
                    var q=alongX?FactoryQ.Axis(0,0,1,Math.PI/4):FactoryQ.Axis(1,0,0,Math.PI/4);
                    if(alongX){Add(u,cy,face+side*.012,.17,.17,.012,q,D,false);Box(u+step/2-.03,u+step/2+.03,b.Y0+.12,b.Y1-.08,face,face+side*.025,T,false);}
                    else{Add(face+side*.012,cy,u,.012,.17,.17,q,D,false);Box(face,face+side*.025,b.Y0+.12,b.Y1-.08,u+step/2-.03,u+step/2+.03,T,false);}
                }
                double hc=NativeSanctumLooks.Hash((int)Math.Round(u*13),(int)Math.Round(b.Y1*7),alongX?1:2);
                if(hc<.42){double cx=alongX?u:(b.X0+b.X1)/2,cz=alongX?(b.Z0+b.Z1)/2:u;int m=1+(int)(hc*7)%3;for(int j=0;j<m;j++){double o=(j-(m-1)/2.0)*.11;Candle(alongX?cx+o:cx,b.Y1+.04,alongX?cz:cz+o,.07+.06*NativeSanctumLooks.Hash(i,j,9),.028);}}
            }
            Light((b.X0+b.X1)/2,b.Y1+.5,(b.Z0+b.Z1)/2,b.Y0<.5?5:7,Warm,b.Y0<.5?.35:.55);
        }
        private void Flight(List<B> steps)
        {
            int T=NativeSanctumLooks.Trim;
            foreach(var s in steps)
            {
                bool alongX=s.Sx<s.Sz;// a step's run is its short side
                if(alongX)Box(s.X0-.03,s.X1+.03,s.Y1-.05,s.Y1+.012,s.Z0,s.Z1,T,false);
                else Box(s.X0,s.X1,s.Y1-.05,s.Y1+.012,s.Z0-.03,s.Z1+.03,T,false);
            }
        }

        // ---------------- furniture ----------------
        // A pew faces east (+x, the high altar): its back on the west, seat and kneeler, carved ends with a poppy-head finial.
        private void Pew(B b)
        {
            int O=NativeSanctumLooks.Oak,D=NativeSanctumLooks.OakDark;double x0=b.X0,x1=b.X1,z0=b.Z0,z1=b.Z1;
            Box(x0,x0+.07,.4,b.Y1+.02,z0+.06,z1-.06,D);Box(x0-.02,x0+.09,b.Y1-.05,b.Y1+.05,z0+.06,z1-.06,O);
            Box(x0+.07,x0+.52,.42,.48,z0+.06,z1-.06,O);Box(x1-.25,x1-.02,.08,.2,z0+.1,z1-.1,D);
            foreach(double z in new[]{z0,z1}){double s=z==z0?1:-1;Box(x0,x1,0,b.Y1+.05,z,z+s*.08,D);Add((x0+x1)/2,b.Y1+.12,z+s*.04,.08,.08,.05,FactoryQ.Axis(0,0,1,Math.PI/4),O);Box(x0+.18,x1-.18,.25,b.Y1-.15,z-s*.008,z+s*.02,O,false);}
        }
        // The west screen (A, blind arcading, a statue niche each bay) and the reredos (B, gilded panels round a sunburst).
        private void Screen(B b)
        {
            bool east=b.X0>0;double face=east?b.X0:b.X1,s=east?-1:1;int S=NativeSanctumLooks.Stone,T=NativeSanctumLooks.Trim,G=NativeSanctumLooks.Gold,D=NativeSanctumLooks.StoneDark;
            int bays=5;double bay=b.Sz/bays;
            for(int i=0;i<bays;i++)
            {
                double z0=b.Z0+bay*i,z1=z0+bay,zc=(z0+z1)/2;
                Bar(face+s*.1,.2,z0+.06,face+s*.1,3.3,z0+.06,.16,.16,S);
                Box(face,face+s*.02,.45,2.6,zc-bay*.36,zc+bay*.36,east?G:D,false);
                for(int r=0;r<5;r++){double va=2.6+.14*r,vb=va+.14,w=bay*.36*Math.Sqrt(Math.Max(0,1-((r+.5)/5.2)*((r+.5)/5.2)));Box(face,face+s*.02,va,vb,zc-w,zc+w,east?G:D,false);}
                if(!east){Box(face,face+s*.18,.45,.62,zc-.3,zc+.3,T);Box(face,face+s*.16,.62,1.9,zc-.18,zc+.18,NativeSanctumLooks.Statue);Box(face,face+s*.15,1.9,2.14,zc-.1,zc+.1,NativeSanctumLooks.Statue);}
            }
            Box(face,face+s*.12,3.3,3.6,b.Z0,b.Z1,T);Box(face,face+s*.12,0,.3,b.Z0,b.Z1,T);
            if(east){for(int i=0;i<16;i++){double a=2*Math.PI*i/16;Bar(face+s*.03,1.8,0,face+s*.03,1.8+Math.Sin(a)*1.1,Math.Cos(a)*1.1,.05,.04,G,false);}Add(face+s*.05,1.8,0,.05,.32,.32,FactoryQ.Axis(1,0,0,Math.PI/4),G);}
        }
        private void Altar(B b)
        {
            bool east=b.X0>0;int M=NativeSanctumLooks.Marble,G=NativeSanctumLooks.Gold,V=NativeSanctumLooks.Velvet,Br=NativeSanctumLooks.Brass;double front=east?b.X0:b.X1,s=east?-1:1,back=east?b.X1:b.X0;
            Box(b.X0-.06,b.X1+.06,b.Y1-.06,b.Y1+.02,b.Z0-.06,b.Z1+.06,M);
            Box(front,front+s*.025,.08,b.Y1-.12,b.Z0+.1,b.Z1-.1,east?G:V,false);Box(front,front+s*.03,.08,.16,b.Z0+.1,b.Z1-.1,G,false);
            Box(b.X0+.05,b.X1-.05,b.Y1+.02,b.Y1+.035,b.Z0+.05,b.Z1-.05,V,false);
            for(int i=0;i<6;i++){double z=b.Z0+.3+(b.Sz-.6)*i/5,x=back+s*.18,tall=.32+.14*(1-Math.Abs(i-2.5)/2.5);Box(x-.05,x+.05,b.Y1+.035,b.Y1+.07,z-.05,z+.05,Br);Box(x-.02,x+.02,b.Y1+.07,b.Y1+.07+tall,z-.02,z+.02,Br);Candle(x,b.Y1+.07+tall,z,.14,.025);}
            for(int i=0;i<7;i++){double z=b.Z0+.5+(b.Sz-1)*i/6;Candle(front-s*.12,b.Y1+.035,z+.05*(i%2),.05+.05*NativeSanctumLooks.Hash(i,east?1:2,4),.024);}
            Light((b.X0+b.X1)/2,b.Y1+.8,(b.Z0+b.Z1)/2,9,Warm,1.4);
        }
        private void Confessional(B b)
        {
            int O=NativeSanctumLooks.Oak,D=NativeSanctumLooks.OakDark,V=NativeSanctumLooks.Velvet,G=NativeSanctumLooks.Gold;
            bool nfacing=b.Z0>0;double face=nfacing?b.Z0:b.Z1,s=nfacing?-1:1;double w=b.Sx/3;
            for(int i=0;i<3;i++){double x0=b.X0+w*i+.06,x1=x0+w-.12;Box(x0,x1,.1,2.15,face,face+s*.04,i==1?D:V,false);Box(x0-.04,x1+.04,2.15,2.3,face,face+s*.08,O);}
            Box(b.X0,b.X1,2.3,b.Y1+.08,face,face+s*.12,D);for(int i=0;i<4;i++){double x=b.X0+b.Sx*i/3;Box(x-.05,x+.05,0,b.Y1+.14,face,face+s*.14,O);}
            Add(b.Cx,b.Y1-.18,face+s*.15,.14,.14,.03,FactoryQ.Axis(0,0,1,Math.PI/4),G);
        }
        // A saint in relief on its plinth's aisle face: robe, hands at a candle, hooded head, the gilded halo behind.
        private void Saint(B b)
        {
            // the face toward the aisle's middle (|z| about 9)
            double mid=Math.Sign(b.Cz)*9.1,face=Math.Abs(b.Z0-mid)<Math.Abs(b.Z1-mid)?b.Z0:b.Z1,s=face==b.Z0?-1:1,cu=b.Cx;
            int St=NativeSanctumLooks.Statue,G=NativeSanctumLooks.Gold,T=NativeSanctumLooks.Trim;
            Box(b.X0,b.X1,0,.6,face,face+s*.08,T);Box(b.X0,b.X1,b.Y1-.2,b.Y1+.05,face,face+s*.1,T);
            Box(cu-.36,cu+.36,.6,1.9,face,face+s*.22,St);Box(cu-.3,cu+.3,1.9,2.08,face,face+s*.2,St);Box(cu-.16,cu+.16,2.08,2.36,face,face+s*.2,St);
            Box(cu-.22,cu+.22,1.25,1.42,face,face+s*.24,St);Candle(cu,1.42,face+s*.17,.16,.03);
            for(int i=0;i<12;i++){double a=2*Math.PI*i/12;Box(cu+Math.Cos(a)*.3-.04,cu+Math.Cos(a)*.3+.04,2.22+Math.Sin(a)*.3-.04,2.22+Math.Sin(a)*.3+.04,face,face+s*.012,G,false);}
            Light(cu,1.6,face+s*.9,4,Warm,.45);
        }
        // A founder's tomb: the chest with shields, a recumbent effigy, the canopy's open arcade painted dark between its
        // colonnettes, pinnacles over the canopy's corners; candles on the chest's ledge.
        private void FounderTomb(B b)
        {
            int St=NativeSanctumLooks.Statue,T=NativeSanctumLooks.Trim,D=NativeSanctumLooks.StoneDark,G=NativeSanctumLooks.Gold,R=NativeSanctumLooks.ClothRed;
            double y1=b.Y1;Box(b.X0-.08,b.X1+.08,0,.25,b.Z0-.08,b.Z1+.08,T);Box(b.X0-.05,b.X1+.05,1.05,1.18,b.Z0-.05,b.Z1+.05,T);
            foreach(int sz in new[]{-1,1}){double f=sz<0?b.Z0:b.Z1;for(int i=0;i<3;i++){double x=b.X0+b.Sx*(i+.5)/3;Add(x,.62,f+sz*.012,.22,.26,.012,FactoryQ.Identity,i==1?G:R,false);}
                Box(b.X0+.2,b.X1-.2,1.3,y1-.45,f,f+sz*.012,D,false);for(int i=0;i<=4;i++){double x=b.X0+.1+(b.Sx-.2)*i/4;Bar(x,1.18,f+sz*.08,x,y1-.35,f+sz*.08,.12,.12,St);}}
            foreach(int sx in new[]{-1,1}){double f=sx<0?b.X0:b.X1;Box(f,f+sx*.012,1.3,y1-.45,b.Z0+.2,b.Z1-.2,D,false);}
            Box(b.X0-.06,b.X1+.06,y1-.35,y1,b.Z0-.06,b.Z1+.06,T);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1}){double x=sx<0?b.X0+.12:b.X1-.12,z=sz<0?b.Z0+.12:b.Z1-.12;Box(x-.09,x+.09,y1,y1+.2,z-.09,z+.09,T);Add(x,y1+.22,z,.06,.06,.06,FactoryQ.Axis(0,1,0,Math.PI/4),G);}
            for(int i=0;i<5;i++){double x=b.X0+.3+(b.Sx-.6)*i/4;Candle(x,1.18,b.Z0-.03,.12+.08*NativeSanctumLooks.Hash(i,3,8),.03);}
            Light(b.Cx,1.8,b.Z0-.8,5,Warm,.5);
        }
        private void LowTomb(B b)
        {
            int St=NativeSanctumLooks.Statue,T=NativeSanctumLooks.Trim,G=NativeSanctumLooks.Gold;bool alongX=b.Sx>=b.Sz;
            Box(b.X0-.05,b.X1+.05,b.Y0,b.Y0+.14,b.Z0-.05,b.Z1+.05,T);Box(b.X0-.04,b.X1+.04,b.Y1-.08,b.Y1+.02,b.Z0-.04,b.Z1+.04,T);
            // the effigy lying along the chest: head on a cushion, hands folded, a sword at its side
            double cx=b.Cx,cz=b.Cz,L=(alongX?b.Sx:b.Sz)/2-.18,W=(alongX?b.Sz:b.Sx)/2-.16;
            void P(double a0,double a1,double h,double w0,int kind){if(alongX)Box(cx+a0,cx+a1,b.Y1+.02,b.Y1+.02+h,cz-w0,cz+w0,kind);else Box(cx-w0,cx+w0,b.Y1+.02,b.Y1+.02+h,cz+a0,cz+a1,kind);}
            P(-L,L-.3,.16,W*.8,St);P(L-.42,L-.1,.2,W*.45,St);P(L-.1,L,.08,W*.6,T);P(-L*.2,L*.2,.2,W*.25,St);P(-L*.85,L*.5,.05,.04,G);
            foreach(int s in new[]{-1,1}){double x=alongX?cx+s*(L+.05):cx+(W+.02),z=alongX?cz+(W+.02):cz+s*(L+.05);Candle(x,b.Y1+.02,z,.12+.04*(s+1),.028);}
        }
        private void CryptPillar(B b)
        {
            int T=NativeSanctumLooks.Trim;double cx=b.Cx,cz=b.Cz,h=b.Sx/2;
            Box(cx-h-.12,cx+h+.12,b.Y0,b.Y0+.3,cz-h-.12,cz+h+.12,T);Box(cx-h-.2,cx+h+.2,b.Y1-.45,b.Y1,cz-h-.2,cz+h+.2,T);
        }
        // The crypt's low vault: groin ribs under the crossing's slab between the pillars and the walls (within 0.25 m of the
        // slab), bosses, and a red oil lamp hanging in each of the middle bays.
        private void CryptVault()
        {
            int T=NativeSanctumLooks.Trim,G=NativeSanctumLooks.Gold,Gl=NativeSanctumLooks.Glow,Ch=NativeSanctumLooks.Chain;double y=-.52;
            double[] xs={-8,-4,4,8},zs={-4.6,-2.4,2.4,4.6};
            for(int i=0;i+1<xs.Length;i++)for(int j=0;j+1<zs.Length;j++)
            {
                double x0=xs[i],x1=xs[i+1],z0=zs[j],z1=zs[j+1],cx=(x0+x1)/2,cz=(z0+z1)/2;
                Bar(x0,y,z0,x1,y,z1,.18,.2,T,false);Bar(x0,y,z1,x1,y,z0,.18,.2,T,false);Add(cx,y-.02,cz,.16,.1,.16,FactoryQ.Axis(0,1,0,Math.PI/4),G,false);
                if(j==1&&i!=1){role=Overhead;Box(cx-.12,cx+.12,-1.55,-1.3,cz-.12,cz+.12,Gl,false,0xFFA040);Bar(cx,-1.3,cz,cx,-.4,cz,.03,.03,Ch,false);role=Attached;Light(cx,-1.6,cz,6,0xFFA050,.8);}
            }
            foreach(double x in xs)Box(x-.12,x+.12,y-.1,-.4,-4.6,4.6,T,false);foreach(double z in zs)Box(-8,8,y-.1,-.4,z-.12,z+.12,T,false);
        }
        private void Shrine(B b)
        {
            int G=NativeSanctumLooks.Gold,Gl=NativeSanctumLooks.Glow,M=NativeSanctumLooks.Marble,V=NativeSanctumLooks.Velvet;double y=b.Y1;
            Box(b.X0-.06,b.X1+.06,b.Y0,b.Y0+.2,b.Z0-.06,b.Z1+.06,M);Box(b.X0-.04,b.X1+.04,y-.12,y+.02,b.Z0-.04,b.Z1+.04,G);
            foreach(int sz in new[]{-1,1}){double f=sz<0?b.Z0:b.Z1;Box(b.X0+.25,b.X1-.25,.35+b.Y0,y-.4,f,f+sz*.02,V,false);for(int i=0;i<5;i++){double x=b.X0+.3+(b.Sx-.6)*i/4;Bar(x,b.Y0+.3,f+sz*.05,x,y-.2,f+sz*.05,.08,.08,G);}
                Box(b.Cx-.3,b.Cx+.3,b.Y0+.9,b.Y0+1.5,f,f+sz*.03,Gl,false,-1,1);}
            for(int i=0;i<10;i++){double a=2*Math.PI*i/10;Candle(b.Cx+Math.Cos(a)*(b.Sx/2+.12),b.Y0+.2,b.Cz+Math.Sin(a)*(b.Sz/2+.12),.1+.12*NativeSanctumLooks.Hash(i,5,1),.03);}
            Light(b.Cx,b.Y0+1.6,b.Cz,9,0xFF9440,1.6);
        }
        // The crypt's walls: niches with skulls and candles, a plinth.
        private void CryptWall(B b)
        {
            int D=NativeSanctumLooks.StoneDark,Bo=NativeSanctumLooks.Bone,T=NativeSanctumLooks.Trim;bool alongX=b.Sx>=b.Sz;if((alongX?b.Sx:b.Sz)<1.2)return;
            double inner,s;if(alongX){inner=Math.Abs(b.Z0)<Math.Abs(b.Z1)?b.Z0:b.Z1;s=inner==b.Z0?-1:1;}else{inner=Math.Abs(b.X0)<Math.Abs(b.X1)?b.X0:b.X1;s=inner==b.X0?-1:1;}
            double from=alongX?b.X0:b.Z0,to=alongX?b.X1:b.Z1;int n=(int)((to-from)/1.6);
            for(int i=0;i<n;i++)
            {
                double u=from+(to-from)*(i+.5)/n;
                if(alongX){Box(u-.4,u+.4,-2.7,-1.9,inner,inner+s*.02,D,false);Box(u-.45,u+.45,-2.78,-2.7,inner,inner+s*.08,T);for(int j=-1;j<=1;j++)Box(u+j*.24-.07,u+j*.24+.07,-2.7,-2.56,inner+s*.03,inner+s*.13,Bo);Candle(u+.33,-2.7,inner+s*.06,.1,.025);}
                else{Box(inner,inner+s*.02,-2.7,-1.9,u-.4,u+.4,D,false);Box(inner,inner+s*.08,-2.78,-2.7,u-.45,u+.45,T);for(int j=-1;j<=1;j++)Box(inner+s*.03,inner+s*.13,-2.7,-2.56,u+j*.24-.07,u+j*.24+.07,Bo);Candle(inner+s*.06,-2.7,u+.33,.1,.025);}
            }
        }
        // The floor's marks: the central aisle's runner (crimson with a gold border), the four brass rings the chandeliers fall
        // into (the danger reads at rest), the crossing's twelve-pointed star in brass and dark stone, grave slabs in the aisles,
        // the holy-fire lines across the transept passages (gold glyphs flush with the floor).
        private void FloorMarks(ContentMap map)
        {
            int C=NativeSanctumLooks.Carpet,G=NativeSanctumLooks.ClothGold,In=NativeSanctumLooks.Inlay,D=NativeSanctumLooks.StoneDark,Br=NativeSanctumLooks.Brass;
            foreach(int s in new[]{-1,1})
            {
                double x0=s<0?-23.9:7.4,x1=s<0?-7.4:23.9;Box(x0,x1,0,.012,-.85,.85,C,false);Box(x0,x1,.012,.016,-.85,-.75,G,false);Box(x0,x1,.012,.016,.75,.85,G,false);
            }
            foreach(var h in map.hazards??Array.Empty<MapHazard>())
            {
                if(h.kind==NativeFactoryHazards.Crane)foreach(var z in h.zones){double r=Math.Max(z.size.x,z.size.z)/2;for(int i=0;i<24;i++){double a0=2*Math.PI*i/24,a1=2*Math.PI*(i+1)/24;Bar(z.center.x+Math.Cos(a0)*r,.02,z.center.z+Math.Sin(a0)*r,z.center.x+Math.Cos(a1)*r,.02,z.center.z+Math.Sin(a1)*r,.08,.022,Br,false);}
                    for(int i=0;i<8;i++){double a=2*Math.PI*i/8;Bar(z.center.x+Math.Cos(a)*(r-.5),.02,z.center.z+Math.Sin(a)*(r-.5),z.center.x+Math.Cos(a)*(r-.1),.02,z.center.z+Math.Sin(a)*(r-.1),.06,.02,Br,false);}}
                if(h.kind==NativeWardZeroHazards.Burner)foreach(var z in h.zones)
                {
                    double y=.015,x0=z.center.x-z.size.x/2,x1=z.center.x+z.size.x/2,z0=z.center.z-z.size.z/2,z1=z.center.z+z.size.z/2;
                    Box(x0+.1,x0+.16,0,y,z0+.1,z1-.1,Br,false);Box(x1-.16,x1-.1,0,y,z0+.1,z1-.1,Br,false);
                    for(double zz=z0+.4;zz<z1-.3;zz+=.55){double xc=(x0+x1)/2;Add(xc,y/2,zz,.12,y/2,.12,FactoryQ.Axis(0,1,0,Math.PI/4),Br,false);}
                }
            }
            // the crossing's star: twelve brass rays round a dark disc
            for(int i=0;i<12;i++){double a=2*Math.PI*i/12;Bar(Math.Cos(a)*1.3,.015,Math.Sin(a)*1.3,Math.Cos(a)*4.4,.015,Math.Sin(a)*4.4,.1,.02,Br,false);double b2=a+Math.PI/12;Bar(Math.Cos(a)*4.4,.015,Math.Sin(a)*4.4,Math.Cos(b2+Math.PI/12)*2.6,.015,Math.Sin(b2+Math.PI/12)*2.6,.06,.02,In,false);}
            for(int i=0;i<32;i++){double a0=2*Math.PI*i/32,a1=2*Math.PI*(i+1)/32;Bar(Math.Cos(a0)*1.3,.016,Math.Sin(a0)*1.3,Math.Cos(a1)*1.3,.016,Math.Sin(a1)*1.3,.08,.02,Br,false);Bar(Math.Cos(a0)*4.6,.016,Math.Sin(a0)*4.6,Math.Cos(a1)*4.6,.016,Math.Sin(a1)*4.6,.1,.02,Br,false);}
            Add(0,.008,0,1.2,.008,1.2,FactoryQ.Axis(0,1,0,Math.PI/4),D,false);
            // grave slabs in the aisles
            foreach(int s in new[]{-1,1})foreach(double x in new[]{-25.5,-16.5,16.5,25.5})foreach(int zs in new[]{-1,1})
            {double z=zs*7.9;if(x*s<0)continue;Box(x-.55,x+.55,0,.012,z-1.0,z+1.0,D,false);Box(x-.55,x+.55,.012,.016,z-1.0,z-.94,Br,false);Box(x-.55,x+.55,.012,.016,z+.94,z+1.0,Br,false);Add(x,.014,z,.18,.004,.18,FactoryQ.Axis(0,1,0,Math.PI/4),Br,false);}
        }

        // ---------------- overhead ----------------
        // The ribbed vault: per bay, four diagonal half ribs to the crown's boss and the bay's edge ribs (transverse and wall
        // ribs, each once), pointed arcs from the springing; the crossing's ribs ring the oculus instead of meeting at a boss.
        private static readonly double[] XLines={-30,-24,-18,-12,-6,6,12,18,24,30},ZBands={-11.5,-6,6,11.5};
        private readonly HashSet<string> springers=new HashSet<string>();
        private void Vaults()
        {
            var edges=new HashSet<string>();
            void Bay(double x0,double x1,double z0,double z1,double crown,bool oculus)
            {
                double cx=(x0+x1)/2,cz=(z0+z1)/2;
                foreach(var (ax,az) in new[]{(x0,z0),(x1,z0),(x0,z1),(x1,z1)})
                {
                    if(oculus){double dx=cx-ax,dz=cz-az,l=Math.Sqrt(dx*dx+dz*dz);double ox=cx-dx/l*NativeSanctumLooks.Oculus*1.2,oz=cz-dz/l*NativeSanctumLooks.Oculus*1.2;Rib(ax,az,ox,oz,crown,.34,.42);}
                    else Rib(ax,az,cx,cz,crown,.3,.38);
                }
                void Edge(double ax,double az,double bx,double bz){string key=Math.Round(Math.Min(ax,bx),2)+","+Math.Round(Math.Min(az,bz),2)+","+Math.Round(Math.Max(ax,bx),2)+","+Math.Round(Math.Max(az,bz),2);if(!edges.Add(key))return;double mx=(ax+bx)/2,mz=(az+bz)/2;Rib(ax,az,mx,mz,crown-.5,.26,.34);Rib(bx,bz,mx,mz,crown-.5,.26,.34);}
                Edge(x0,z0,x1,z0);Edge(x0,z1,x1,z1);Edge(x0,z0,x0,z1);Edge(x1,z0,x1,z1);
                if(!oculus){Add(cx,crown-.1,cz,.32,.22,.32,FactoryQ.Axis(0,1,0,Math.PI/4),NativeSanctumLooks.Gold);for(int i=0;i<4;i++){double a=Math.PI/2*i;Add(cx+Math.Cos(a)*.36,crown-.12,cz+Math.Sin(a)*.36,.14,.06,.14,FactoryQ.Axis(0,1,0,a+Math.PI/4),NativeSanctumLooks.Trim);}}
                else{for(int i=0;i<24;i++){double a0=2*Math.PI*i/24,a1=2*Math.PI*(i+1)/24,r=NativeSanctumLooks.Oculus*1.2;Bar(Math.Cos(a0)*r,crown-.05,Math.Sin(a0)*r,Math.Cos(a1)*r,crown-.05,Math.Sin(a1)*r,.4,.5,NativeSanctumLooks.Trim);Bar(Math.Cos(a0)*(r-.25),crown-.3,Math.Sin(a0)*(r-.25),Math.Cos(a1)*(r-.25),crown-.3,Math.Sin(a1)*(r-.25),.14,.14,NativeSanctumLooks.Gold,false);}}
            }
            for(int i=0;i+1<XLines.Length;i++)
            {
                double x0=XLines[i],x1=XLines[i+1];if(x0==-6)continue;
                for(int j=0;j+1<ZBands.Length;j++)Bay(x0,x1,ZBands[j],ZBands[j+1],j==1?20.5:20.2,false);
            }
            Bay(-6,6,-6,6,20.6,true);
            foreach(int s in new[]{-1,1}){Bay(-6,6,s<0?-11.5:6,s<0?-6:11.5,20.2,false);Bay(-6,6,s<0?-20:11.5,s<0?-11.5:20,20.3,false);}
            // the censer's yoke across the oculus: an iron beam with its pulley block
            Box(-NativeSanctumLooks.Oculus-.3,NativeSanctumLooks.Oculus+.3,21.0,21.35,-.16,.16,NativeSanctumLooks.Iron);Box(-.3,.3,20.75,21.05,-.3,.3,NativeSanctumLooks.Iron);
        }
        /// <summary>A half rib from its springing over (ax, az) to its crown over (bx, bz): a pointed arc (or a straight rise where
        /// the bay is too wide for one), in ten bars of the given section.</summary>
        private void Rib(double ax,double az,double bx,double bz,double crown,double w,double d)
        {
            double L=Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az));if(L<.1)return;double spring=Math.Min(13.0,crown-L-.25);double H=crown-spring,c=(H*H-L*L)/(2*L),R=L+c,th=Math.Acos(Math.Max(-1,Math.Min(1,c/R)));
            double ux=(bx-ax)/L,uz=(bz-az)/L;const int n=10;double px=ax,py=spring,pz=az;
            if(springers.Add(Math.Round(ax,2)+","+Math.Round(az,2)+","+Math.Round(spring,2)))Bar(ax,spring-1.6,az,ax,spring,az,w*.9,w*.9,NativeSanctumLooks.Stone);
            for(int i=1;i<=n;i++){double t=th*i/n,u=(L+c)-R*Math.Cos(t),y=spring+R*Math.Sin(t);double qx=ax+ux*u,qz=az+uz*u;Bar(px,py,pz,qx,y,qz,w,d,NativeSanctumLooks.Stone);px=qx;py=y;pz=qz;}
        }
        // The belfry over the oculus: four walls with louvred openings to the sky, the bell frame's beams (the bell is the rig's).
        private void Belfry()
        {
            double y0=NativeSanctumLooks.BelfryFloor,y1=NativeSanctumLooks.BelfryTop,h=3.4,o0=24.4,o1=28.6;int S=NativeSanctumLooks.Stone,O=NativeSanctumLooks.OakDark,T=NativeSanctumLooks.Trim;
            foreach(int s in new[]{-1,1})
            {
                // each wall: below and above its two louvred openings, the corner piers and the middle pier between them
                Box(-h-.4,h+.4,y0,o0,s*h,s*(h+.4),S);Box(-h-.4,h+.4,o1,y1,s*h,s*(h+.4),S);Box(s*h,s*(h+.4),y0,o0,-h,h,S);Box(s*h,s*(h+.4),o1,y1,-h,h,S);
                foreach(int t in new[]{-1,1}){Box(t*(h-.7),t*(h+.4),o0,o1,s*h,s*(h+.4),S);Box(s*h,s*(h+.4),o0,o1,t*(h-.7),t*(h+.4),S);}
                Box(-.35,.35,o0,o1,s*h,s*(h+.4),S);Box(s*h,s*(h+.4),o0,o1,-.35,.35,S);
                Box(-h-.4,h+.4,o0-.18,o0,s*(h-.1),s*(h+.5),T);Box(s*(h-.1),s*(h+.5),o0-.18,o0,-h-.4,h+.4,T);
                for(double y=o0+.35;y<o1-.1;y+=.42)foreach(int t in new[]{-1,1})
                {
                    double c=t*(h-.7+.35)/2+t*.17;Add(c,y,s*(h+.2),(h-1.05)/2,.03,.22,FactoryQ.Axis(1,0,0,s*.6),O,true);Add(s*(h+.2),y,c,.22,.03,(h-1.05)/2,FactoryQ.Axis(0,0,1,-s*.6),O,true);
                }
            }
            Box(-h-.4,h+.4,y1,y1+.5,-h-.4,h+.4,S);
            foreach(int s in new[]{-1,1}){Box(-h,h,y0+3.0,y0+3.4,s*2.4,s*2.8,O);Box(-.25,.25,y0,y0+5.8,s*2.4-.2,s*2.4+.2,O);}
            Box(-.3,.3,y0+5.6,y0+6.0,-2.6,2.6,O);
            Box(-.16,.16,y0+5.0,y0+5.4,2.2,2.5,NativeSanctumLooks.Glow,false,0xFFB060);Candle(0,y0+5.05,2.35,.12,.03);
        }
        // The organ over the west loft (A): two pipe towers flanking the rose, a low flat under it, the case's impost and its
        // gilded shades; flat to the wall below the impost (7 m), standing out above it.
        private void Organ()
        {
            double wall=-NativeSanctumLooks.EndWall;int P=NativeSanctumLooks.Pipe,O=NativeSanctumLooks.OakDark,G=NativeSanctumLooks.Gold,L=NativeSanctumLooks.Lead;
            Box(wall,wall+.22,4.0,7.0,-8.8,8.8,O);for(int i=0;i<12;i++){double z=-8.4+16.8*i/11;Box(wall+.22,wall+.24,4.3,6.6,z-.5,z+.5,O,false);}
            Box(wall,wall+.95,7.0,7.35,-8.8,8.8,O);Box(wall+.9,wall+1.0,7.0,7.12,-8.8,8.8,G,false);
            foreach(int s in new[]{-1,1})
            {
                double zc=s*6.6;Box(wall,wall+.9,7.35,14.6,zc-2.0,zc-1.9,O);Box(wall,wall+.9,7.35,14.6,zc+1.9,zc+2.0,O);Box(wall,wall+.95,14.6,15.0,zc-2.05,zc+2.05,O);
                for(int i=0;i<9;i++){double z=zc-1.7+3.4*i/8,tall=4.6+1.8*(1-Math.Abs(i-4)/4.0),r=.12+.04*(1-Math.Abs(i-4)/4.0);double x=wall+.55;Bar(x,7.35,z,x,7.35+tall,z,2*r,2*r,P);Box(x-.04-r,x-r+.01,7.85,8.05,z-r*.6,z+r*.6,L,false);}
                for(int i=0;i<9;i++){double z=zc-1.7+3.4*i/8;Bar(wall+.6,14.1-.3*Math.Abs(i-4)/4,z-.19,wall+.6,14.4,z+.19,.05,.05,G,false);}
            }
            for(int i=0;i<11;i++){double z=-3.6+7.2*i/10,tall=1.4+.9*Math.Abs(i-5)/5.0,r=.08;Bar(wall+.4,7.35,z,wall+.4,7.35+tall,z,2*r,2*r,P);}
            Light(wall+1.5,7.5,0,8,Warm,.5);
        }
        // The east loft (B): a gilded retable against the wall under the rose, its niches of saints, a great sunburst.
        private void Retable()
        {
            double wall=NativeSanctumLooks.EndWall;int G=NativeSanctumLooks.Gold,St=NativeSanctumLooks.Statue,O=NativeSanctumLooks.OakDark,D=NativeSanctumLooks.StoneDark;
            Box(wall-.22,wall,4.0,7.0,-8.8,8.8,O);Box(wall-.95,wall,7.0,7.35,-8.8,8.8,O);Box(wall-1.0,wall-.9,7.0,7.12,-8.8,8.8,G,false);
            foreach(int s in new[]{-1,1})for(int i=0;i<3;i++)
            {
                double zc=s*(5.0+1.4*i);Box(wall-.4,wall,7.35,10.6-.3*i,zc-.6,zc+.6,O);Box(wall-.42,wall-.4,7.6,10.2-.3*i,zc-.45,zc+.45,D,false);
                Box(wall-.38,wall-.12,7.6,9.4-.3*i,zc-.2,zc+.2,St);Box(wall-.36,wall-.14,9.4-.3*i,9.7-.3*i,zc-.1,zc+.1,St);Box(wall-.44,wall-.4,10.2-.3*i,10.6-.3*i,zc-.6,zc+.6,G,false);
            }
            for(int i=0;i<24;i++){double a=2*Math.PI*i/24,r0=.6,r1=i%2==0?2.4:1.7;Bar(wall-.5,8.6+Math.Sin(a)*r0,Math.Cos(a)*r0,wall-.5,8.6+Math.Sin(a)*r1,Math.Cos(a)*r1,.08,.06,G,false);}
            Add(wall-.5,8.6,0,.06,.5,.5,FactoryQ.Axis(1,0,0,Math.PI/4),G);
            Light(wall-1.5,7.5,0,8,Warm,.6);
        }
        // A hanging sigil over each rood loft: a gilded sunburst disc on chains, 8 m over the loft.
        private void Sigils()
        {
            foreach(int s in new[]{-1,1})
            {
                double x=s*7.95,y=11.5;int G=NativeSanctumLooks.Gold,Ch=NativeSanctumLooks.Chain;
                for(int i=0;i<20;i++){double a=2*Math.PI*i/20,r1=i%2==0?2.0:1.4;Bar(x,y+Math.Sin(a)*.45,Math.Cos(a)*.45,x,y+Math.Sin(a)*r1,Math.Cos(a)*r1,.1,.08,G);}
                for(int i=0;i<24;i++){double a0=2*Math.PI*i/24,a1=2*Math.PI*(i+1)/24;Bar(x,y+Math.Sin(a0)*.5,Math.Cos(a0)*.5,x,y+Math.Sin(a1)*.5,Math.Cos(a1)*.5,.14,.12,G);}
                Add(x,y,0,.06,.2,.2,FactoryQ.Axis(1,0,0,Math.PI/4),G);
                foreach(int sz in new[]{-1,1})Bar(x,y+1.6,sz*1.2,x,NativeSanctumLooks.Vault-.4,sz*2.4,.04,.04,Ch,false);
            }
        }
    }
}
