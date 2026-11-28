using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Skyframe's static kit (docs/arenas/SKYFRAME.md "The look"): the detail NativeBlockLit instances draw on the map's dressed
    /// blocks by id stem when the stage bank is absent (with it the modelled art draws instead, docs/arenas/STAGE-ART.md). Two
    /// kinds of part:
    /// <list type="bullet">
    /// <item>Attached (<see cref="Parent"/> the block): the garden's paving joints, lawns, reflecting pools and painted drop zones,
    /// its glazed fascia and the vines down it; the core's panel joints, lift doors and lobby lamps; the columns' base plates and
    /// bands; the balustrades' handrails and posts; the pavilions' timber slats, plinths, cornices and door lamps; the stairs'
    /// nosings; the planters' rims, soil and shrubs; the hedges' and living walls' frames and darker leaves; the straps on the
    /// beam bundles and pallets. Every attached part stays inside its block grown by <see cref="Proud"/> (what looks like cover
    /// is cover).</item>
    /// <item>Overhead (<see cref="Parent"/> -1): the unfinished frame over the gallery (the eight columns going on up, ring and
    /// radial beams thinning out storey by storey, the core's concrete and its starter bars, the crane's mast), all inside
    /// <see cref="NativeSkyframeLooks.FrameHalf"/> in plan and at least <see cref="Clearance"/> m over every standable top.</item>
    /// </list>
    /// Deterministic; allocates only while building (Runtime packs it once per map).
    /// </summary>
    public sealed class NativeSkyframeKit
    {
        public const float Proud=.25f;public const double Clearance=4.5,Decal=.012;
        public readonly NativeFactoryKit.Writer Parts;public int[] Parent=Array.Empty<int>();
        private readonly List<int> parents=new List<int>();private int current=-1;
        public int Count=>Parts.Count;
        private NativeSkyframeKit(NativeFactoryKit.Looks looks){Parts=new NativeFactoryKit.Writer(4096,true,looks);}
        private void Track(int before){for(int i=before;i<Parts.Count;i++)parents.Add(current);}
        private void Add(double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);Track(n);}
        private void Box(double x0,double x1,double y0,double y1,double z0,double z1,int kind,bool shadow=true){int n=Parts.Count;Parts.Box(x0,x1,y0,y1,z0,z1,kind,shadow);Track(n);}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=true){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);Track(n);}
        private static double H(int a,int b=0,int c=0)=>NativeSkyframeLooks.Hash(a,b,c);
        private static bool Starts(string id,string p)=>id!=null&&id.StartsWith(p,StringComparison.Ordinal);
        private static bool Has(string id,string p)=>id!=null&&id.IndexOf(p,StringComparison.Ordinal)>=0;
        private struct Bx {public double X0,X1,Y0,Y1,Z0,Z1;public string Id;public int Index;public double Sx=>X1-X0;public double Sy=>Y1-Y0;public double Sz=>Z1-Z0;public double Cx=>(X0+X1)/2;public double Cz=>(Z0+Z1)/2;}
        private const int Stone=NativeSkyframeLooks.Stone,StoneDark=NativeSkyframeLooks.StoneDark,Trim=NativeSkyframeLooks.Trim,Steel=NativeSkyframeLooks.Steel,Primer=NativeSkyframeLooks.Primer,Glass=NativeSkyframeLooks.Glass,
            Leaf=NativeSkyframeLooks.Leaf,LeafDark=NativeSkyframeLooks.LeafDark,Soil=NativeSkyframeLooks.Soil,Timber=NativeSkyframeLooks.Timber,Lamp=NativeSkyframeLooks.Lamp,Paint=NativeSkyframeLooks.Paint,
            Water=NativeSkyframeLooks.Water,Lawn=NativeSkyframeLooks.Lawn,Dark=NativeSkyframeLooks.CraneDark,Weight=NativeSkyframeLooks.Weight,CraneK=NativeSkyframeLooks.Crane;

        /// <summary>The map's kit for its look (null for another map).</summary>
        public static NativeSkyframeKit Build(ContentMap map,MapLook look)
        {
            if(!NativeSkyframeLooks.Active(map?.id))return null;var k=new NativeSkyframeKit(NativeSkyframeLooks.Resolve(look?.kitLooks));
            for(int i=0;i<(map.blocks?.Length??0);i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;var r=b.rotation;if(r!=null&&(Math.Abs(r.x)>1e-4||Math.Abs(r.y)>1e-4||Math.Abs(r.z)>1e-4))continue;
                var x=new Bx{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2,Id=b.id,Index=i};
                k.current=i;k.Dress(x,map);
            }
            k.current=-1;k.Frame(map);
            k.Parent=k.parents.ToArray();return k;
        }

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
        private static bool LongX(Bx b)=>b.Sx>=b.Sz;

        private void Dress(Bx b,ContentMap map)
        {
            string id=b.Id;
            if(id=="skyframe-garden"){Garden(b,map);return;}
            if(Starts(id,"skyframe-core-")){Core(b);return;}
            if(Starts(id,"skyframe-gallery-")||Starts(id,"skyframe-bridge-")){Deck(b);return;}
            if(Starts(id,"skyframe-column-")){Column(b);return;}
            if(Starts(id,"skyframe-rail-")){Rail(b);return;}
            if(Starts(id,"skyframe-stair-")){Step(b);return;}
            if(Starts(id,"skyframe-pavilion-")){if(Has(id,"-roof"))Roof(b,map);else Wall(b);return;}
            if(Starts(id,"skyframe-terrace-")){Terrace(b);return;}
            if(Starts(id,"skyframe-planter-")||Starts(id,"skyframe-counter-")){Planter(b);return;}
            if(Starts(id,"skyframe-hedge-")){Hedge(b);return;}
            if(Starts(id,"skyframe-greenwall-")){GreenWall(b);return;}
            if(Starts(id,"skyframe-bundle-")||Starts(id,"skyframe-pallet-")||Starts(id,"skyframe-crate-")){Stack(b);return;}
        }

        // ---------------- the garden ----------------
        private void Garden(Bx b,ContentMap map)
        {
            // paving joints every 2 m both ways
            for(double x=b.X0+2;x<b.X1-.5;x+=2)Face(b,4,x-.02,x+.02,b.Z0+.05,b.Z1-.05,StoneDark,.006,.003);
            for(double z=b.Z0+2;z<b.Z1-.5;z+=2)Face(b,4,b.X0+.05,b.X1-.05,z-.02,z+.02,StoneDark,.006,.003);
            foreach(int s in new[]{-1,1})
            {
                // the lawn of each drop-zone quarter and the reflecting pool before each base (point mirrored)
                Plate(b,s,-24.6,-10.2,9.4,15.4,Lawn,.010,.005);Plate(b,s,-16.0,-13.2,-2.0,2.0,Trim,.012,.005);Plate(b,s,-15.85,-13.35,-1.85,1.85,Water,.014,.007);
                // a darker border course round the edge
                Plate(b,s,-25.75,-25.45,-15.7,15.7,StoneDark,.008,.004);Plate(b,s,-25.4,25.4,15.4,15.7,StoneDark,.008,.004);
            }
            Zones(b,map,b.Y1);
            // the fascia: a stone band under the edge, mullions and glass below it (the tower's top glazed storey), vines down it
            for(int side=0;side<4;side++)
            {
                double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1;
                Face(b,side,u0,u1,-.7,0,Trim,.16,.16,true);Face(b,side,u0,u1,-6.6,-.9,Glass,.02,.02);
                for(double u=u0+1;u<u1-.5;u+=2)Face(b,side,u-.07,u+.07,-6.9,-.7,Steel,.1,.1,true);
                Face(b,side,u0,u1,-3.9,-3.6,Steel,.12,.12,true);
                for(int k=0;k<22;k++){double u=u0+1+H(b.Index,side,k)*(u1-u0-2),l=.8+H(b.Index,side,k+40)*2.6,w=.25+H(b.Index,side,k+80)*.5;Face(b,side,u-w,u+w,-.7-l,-.05,k%3==0?LeafDark:Leaf,.2,.22,true);}
            }
        }
        // A decal on the top of `b` at the point-mirrored rectangle (s -1: the rectangle as given; +1: its mirror).
        private void Plate(Bx b,int s,double x0,double x1,double z0,double z1,int kind,double t,double o)
        {
            if(s>0){double a=-x1,c=-x0,d=-z1,e=-z0;x0=a;x1=c;z0=d;z1=e;}
            Face(b,4,x0,x1,z0,z1,kind,t,o);
        }
        // The painted drop zones on this block's top: a hazard frame of four bars and its corner marks.
        private void Zones(Bx b,ContentMap map,double top)
        {
            foreach(var h in map.hazards??Array.Empty<MapHazard>())
            {
                if(h?.kind!=NativeFactoryHazards.Crane||h.zones==null)continue;
                foreach(var z in h.zones)
                {
                    if(Math.Abs(z.center.y-top)>.05||z.center.x<b.X0||z.center.x>b.X1||z.center.z<b.Z0||z.center.z>b.Z1)continue;
                    double hx=h.size.x/2+.5,hz=h.size.z/2+.5,cx=z.center.x,cz=z.center.z;
                    Face(b,4,cx-hx,cx+hx,cz-hz,cz-hz+.14,Paint,.014,.008);Face(b,4,cx-hx,cx+hx,cz+hz-.14,cz+hz,Paint,.014,.008);
                    Face(b,4,cx-hx,cx-hx+.14,cz-hz,cz+hz,Paint,.014,.008);Face(b,4,cx+hx-.14,cx+hx,cz-hz,cz+hz,Paint,.014,.008);
                    for(double x=cx-hx+.8;x<cx+hx-.4;x+=.9)Add(x,top+.009,cz,.07,.006,hz*.72,FactoryQ.Axis(0,1,0,.6),Paint,false);
                }
            }
        }

        // ---------------- the core, the columns, the decks ----------------
        private void Core(Bx b)
        {
            bool lintel=Has(b.Id,"-lintel-");
            // formwork joints every 3 m up the faces
            for(double y=Math.Max(b.Y0+3,3);y<b.Y1-.5;y+=3)for(int side=0;side<4;side++){double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1;Face(b,side,u0,u1,y-.03,y+.03,StoneDark,.008,.004);}
            if(lintel)
            {
                // the lobby's ceiling lamp and the lit head over its mouth
                Face(b,5,b.Cx-1.1,b.Cx+1.1,b.Cz-.5,b.Cz+.5,Lamp,.03,.02);
                int mouth=b.Cz>0?2:3;Face(b,mouth,b.X0+.2,b.X1-.2,b.Y0+.08,b.Y0+.2,Lamp,.03,.03);return;
            }
            if(Has(b.Id,"-body-"))
            {
                // the lift doors on the lobby's back wall, their frame and call lamp
                int back=b.Cz<0?2:3;double c=b.Cx;
                Face(b,back,c-1.15,c+1.15,0,2.45,Steel,.06,.06,true);Face(b,back,c-1.0,c-.03,.05,2.3,Trim,.02,.075);Face(b,back,c+.03,c+1.0,.05,2.3,Trim,.02,.075);
                Face(b,back,c-.25,c+.25,2.52,2.68,Lamp,.03,.05);
            }
        }
        private void Column(Bx b)
        {
            // a base plate, flange lines up the faces, bands where the decks frame in
            Box(b.X0-.12,b.X1+.12,b.Y0,b.Y0+.08,b.Z0-.12,b.Z1+.12,Dark);
            for(int side=0;side<4;side++){double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1;Face(b,side,u0,u0+.07,b.Y0+.1,b.Y1,Primer,.05,.05,true);Face(b,side,u1-.07,u1,b.Y0+.1,b.Y1,Primer,.05,.05,true);}
            foreach(double y in new[]{3.0,9.0})Box(b.X0-.1,b.X1+.1,y-.14,y+.14,b.Z0-.1,b.Z1+.1,Dark);
        }
        private void Deck(Bx b)
        {
            // an edge beam under each long edge, joints across the walk
            bool lx=LongX(b);
            if(lx){Face(b,2,b.X0,b.X1,b.Y0-.1,b.Y1,Steel,.08,.08,true);Face(b,3,b.X0,b.X1,b.Y0-.1,b.Y1,Steel,.08,.08,true);for(double x=b.X0+1.5;x<b.X1-.3;x+=1.5)Face(b,4,x-.015,x+.015,b.Z0+.1,b.Z1-.1,StoneDark,.006,.003);}
            else{Face(b,0,b.Z0,b.Z1,b.Y0-.1,b.Y1,Steel,.08,.08,true);Face(b,1,b.Z0,b.Z1,b.Y0-.1,b.Y1,Steel,.08,.08,true);for(double z=b.Z0+1.5;z<b.Z1-.3;z+=1.5)Face(b,4,b.X0+.1,b.X1-.1,z-.015,z+.015,StoneDark,.006,.003);}
        }
        private void Terrace(Bx b)
        {
            for(double x=b.X0+2;x<b.X1-.5;x+=2)Face(b,4,x-.02,x+.02,b.Z0+.05,b.Z1-.05,StoneDark,.006,.003);
            for(int side=0;side<4;side++){double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1;Face(b,side,u0,u1,b.Y0,b.Y1,Steel,.1,.1,true);}
            // the wind's gap: a painted warning strip along the outer edge
            double zo=b.Cz>0?b.Z1-.45:b.Z0+.15;Face(b,4,b.Cx-6,b.Cx+6,zo,zo+.3,Paint,.012,.006);
        }
        private void Rail(Bx b)
        {
            // a handrail along the top and a post at each end (the pane between is the block's own glass look)
            bool lx=LongX(b);double y=b.Y1;
            if(lx){Box(b.X0,b.X1,y-.05,y+.03,b.Z0-.03,b.Z1+.03,Steel);Box(b.X0,b.X0+.08,b.Y0,y,b.Z0-.02,b.Z1+.02,Steel);Box(b.X1-.08,b.X1,b.Y0,y,b.Z0-.02,b.Z1+.02,Steel);}
            else{Box(b.X0-.03,b.X1+.03,y-.05,y+.03,b.Z0,b.Z1,Steel);Box(b.X0-.02,b.X1+.02,b.Y0,y,b.Z0,b.Z0+.08,Steel);Box(b.X0-.02,b.X1+.02,b.Y0,y,b.Z1-.08,b.Z1,Steel);}
        }
        private void Step(Bx b){bool lx=b.Sx<b.Sz;if(lx){Face(b,4,b.X0,b.X0+.05,b.Z0,b.Z1,Trim,.012,.006);Face(b,4,b.X1-.05,b.X1,b.Z0,b.Z1,Trim,.012,.006);}else{Face(b,4,b.X0,b.X1,b.Z0,b.Z0+.05,Trim,.012,.006);Face(b,4,b.X0,b.X1,b.Z1-.05,b.Z1,Trim,.012,.006);}}

        // ---------------- the pavilions ----------------
        private void Wall(Bx b)
        {
            bool head=Has(b.Id,"-head-");bool lx=LongX(b);
            foreach(int side in lx?new[]{2,3}:new[]{0,1})
            {
                double u0=lx?b.X0:b.Z0,u1=lx?b.X1:b.Z1;
                if(!head)Face(b,side,u0,u1,b.Y0,b.Y0+.35,StoneDark,.08,.08,true);
                Face(b,side,u0,u1,b.Y1-.3,b.Y1,Trim,.1,.1,true);
                if(head){Face(b,side,u0+.1,u1-.1,b.Y0+.06,b.Y0+.18,Lamp,.03,.04);continue;}
                for(double u=u0+.3;u<u1-.1;u+=.42)Face(b,side,u-.07,u+.07,b.Y0+.45,b.Y1-.38,Timber,.05,.06,true);
            }
        }
        private void Roof(Bx b,ContentMap map)
        {
            for(int side=0;side<4;side++){double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1;Face(b,side,u0,u1,b.Y0-.1,b.Y1,Trim,.14,.14,true);}
            for(double x=b.X0+2;x<b.X1-.5;x+=2)Face(b,4,x-.02,x+.02,b.Z0+.05,b.Z1-.05,StoneDark,.006,.003);
            Zones(b,map,b.Y1);
        }

        // ---------------- the garden's cover ----------------
        private void Planter(Bx b)
        {
            bool counter=Starts(b.Id,"skyframe-counter-");
            Box(b.X0-.05,b.X1+.05,b.Y1-.14,b.Y1+.02,b.Z0-.05,b.Z1+.05,Trim);Box(b.X0-.04,b.X1+.04,b.Y0,b.Y0+.1,b.Z0-.04,b.Z1+.04,StoneDark);
            Face(b,4,b.X0+.12,b.X1-.12,b.Z0+.12,b.Z1-.12,Soil,.03,.03);
            int n=counter?7:12;
            for(int k=0;k<n;k++)
            {
                double x=b.X0+.3+H(b.Index,k)*(b.Sx-.6),z=b.Z0+.3+H(b.Index,k,1)*(b.Sz-.6),r=.16+H(b.Index,k,2)*.1;if(!counter&&Math.Abs(x-b.Cx)<.45&&Math.Abs(z-b.Cz)<.45)continue;
                Add(x,b.Y1+.03+r*.4,z,r,r*.4,r,FactoryQ.Axis(0,1,0,H(b.Index,k,3)*3),k%3==0?LeafDark:Leaf);
            }
        }
        private void Hedge(Bx b)
        {
            Box(b.X0-.06,b.X1+.06,b.Y0,b.Y0+.28,b.Z0-.06,b.Z1+.06,Stone);
            for(int k=0;k<14;k++)
            {
                int side=k%4;double u0=side<=1?b.Z0:b.X0,u1=side<=1?b.Z1:b.X1,u=u0+.2+H(b.Index,k)*(u1-u0-.4),v=b.Y0+.4+H(b.Index,k,1)*(b.Sy-.6),w=.14+H(b.Index,k,2)*.2;
                Face(b,side,Math.Max(u0,u-w),Math.Min(u1,u+w),v-.12,Math.Min(b.Y1,v+.12),LeafDark,.06,.07,true);
            }
        }
        private void GreenWall(Bx b)
        {
            bool lx=LongX(b);
            // a steel frame round it, leaves in darker and lighter patches on both faces
            if(lx){Box(b.X0-.05,b.X0+.08,b.Y0,b.Y1+.05,b.Z0-.05,b.Z1+.05,Steel);Box(b.X1-.08,b.X1+.05,b.Y0,b.Y1+.05,b.Z0-.05,b.Z1+.05,Steel);Box(b.X0,b.X1,b.Y1-.06,b.Y1+.05,b.Z0-.05,b.Z1+.05,Steel);}
            else{Box(b.X0-.05,b.X1+.05,b.Y0,b.Y1+.05,b.Z0-.05,b.Z0+.08,Steel);Box(b.X0-.05,b.X1+.05,b.Y0,b.Y1+.05,b.Z1-.08,b.Z1+.05,Steel);Box(b.X0-.05,b.X1+.05,b.Y1-.06,b.Y1+.05,b.Z0,b.Z1,Steel);}
            Box(b.X0-.08,b.X1+.08,b.Y0,b.Y0+.3,b.Z0-.08,b.Z1+.08,Stone);
            foreach(int side in lx?new[]{2,3}:new[]{0,1})for(int k=0;k<26;k++)
            {
                double u0=lx?b.X0+.1:b.Z0+.1,u1=lx?b.X1-.1:b.Z1-.1,u=u0+H(b.Index,k,side)*(u1-u0),v=b.Y0+.4+H(b.Index,k,side+9)*(b.Sy-.6),w=.16+H(b.Index,k,side+19)*.26;
                Face(b,side,Math.Max(u0,u-w),Math.Min(u1,u+w),v-.14,Math.Min(b.Y1-.08,v+.14),k%5==0?Trim:k%2==0?LeafDark:Leaf,.07,.09+.01*(k%3),true);
            }
        }
        private void Stack(Bx b)
        {
            bool lx=LongX(b);bool bundle=Starts(b.Id,"skyframe-bundle-");
            // straps round it; a pallet under a stack
            int n=bundle?3:2;for(int k=0;k<n;k++){double f=(k+.5)/n;if(lx){double x=b.X0+f*b.Sx;Box(x-.05,x+.05,b.Y0,b.Y1+.02,b.Z0-.02,b.Z1+.02,Dark);}else{double z=b.Z0+f*b.Sz;Box(b.X0-.02,b.X1+.02,b.Y0,b.Y1+.02,z-.05,z+.05,Dark);}}
            if(!bundle)Box(b.X0-.04,b.X1+.04,b.Y0,b.Y0+.14,b.Z0-.04,b.Z1+.04,Timber);
        }

        // ---------------- overhead: the unfinished frame, the core's rise and the crane's mast ----------------
        /// <summary>Whether the frame's beam from column a to column b stands at storey s (the frame thins out as it rises).</summary>
        public static bool FrameBeam(int s,int a,int b)=>s==0||H(211,s,a*16+b)>(s==1?.12:s==2?.34:s==3?.6:.82);
        /// <summary>How many storeys the frame's column c rises (1..<see cref="NativeSkyframeLooks.Storeys"/>).</summary>
        public static int FrameRise(int c)=>Math.Min(NativeSkyframeLooks.Storeys,3+(int)(H(223,c)*2.999));
        /// <summary>The frame's columns in ring order (by bearing from the core): the map's own column blocks carry them to 12 m.</summary>
        public static List<(double x,double z)> Columns(ContentMap map)
        {
            var cols=new List<(double x,double z)>();
            foreach(var b in map?.blocks??Array.Empty<ContentBlock>())if(Starts(b?.id,"skyframe-column-"))cols.Add((b.pos.x,b.pos.z));
            cols.Sort((p,q)=>Math.Atan2(p.x,p.z).CompareTo(Math.Atan2(q.x,q.z)));return cols;
        }
        private void Frame(ContentMap map)
        {
            var cols=Columns(map);int n=cols.Count;if(n==0)return;
            double fy=NativeSkyframeLooks.FrameY,st=NativeSkyframeLooks.Storey,top=NativeSkyframeLooks.CoreTop;
            for(int c=0;c<n;c++)
            {
                int rise=FrameRise(c);double y1=fy+(rise-1)*st+1.2;Box(cols[c].x-.26,cols[c].x+.26,top,y1,cols[c].z-.26,cols[c].z+.26,Primer);
                for(int s=0;s<rise;s++)
                {
                    double y=fy+s*st;int d=(c+1)%n;
                    if(FrameRise(d)>s&&FrameBeam(s,c,d))Bar(cols[c].x,y,cols[c].z,cols[d].x,y,cols[d].z,.22,Primer);
                    // a radial beam in to the core's face (the core's concrete stops at CoreTop + CoreRise: above it the beam ends on a stub)
                    if(FrameBeam(s,c,c+8)){double l=Math.Max(Math.Abs(cols[c].x),Math.Abs(cols[c].z)),k=5.0/l;Bar(cols[c].x,y,cols[c].z,cols[c].x*k,y,cols[c].z*k,.18,Primer);}
                }
            }
            // the core's concrete going on up, its starter bars, the mast through it to the slewing ring
            double cr=top+NativeSkyframeLooks.CoreRise;Box(-5,5,top,cr,-5,5,Weight);Box(-5.1,5.1,cr-.5,cr-.3,-5.1,5.1,StoneDark,false);
            for(int k=0;k<28;k++){double a=k/28.0*4,x=a<1?-4.8+a*9.6:a<2?4.8:a<3?4.8-(a-2)*9.6:-4.8,z=a<1?-4.8:a<2?-4.8+(a-1)*9.6:a<3?4.8:4.8-(a-3)*9.6;Box(x-.03,x+.03,cr,cr+.9+H(229,k)*.7,z-.03,z+.03,Dark,false);}
            double m=NativeSkyframeLooks.MastHalf,my=NativeSkyframeLooks.JibY-1.2;
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Box(sx*m-.09,sx*m+.09,cr,my,sz*m-.09,sz*m+.09,CraneK);
            for(double y=cr+.4;y<my-1.5;y+=2.2)
            {
                foreach(int sd in new[]{-1,1}){Bar(-m,y,sd*m,m,y+2.2,sd*m,.07,CraneK,false);Bar(sd*m,y,-m,sd*m,y+2.2,m,.07,CraneK,false);}
            }
        }
    }

    /// <summary>
    /// Skyframe's rig for one frame (docs/arenas/SKYFRAME.md), as NativeBlockLit instances built from the replicated state (the box
    /// rig) and, part by part, the frames the modelled art is posed from (<see cref="Slot"/>; Core NativeSkyframeArt):
    /// <list type="bullet">
    /// <item>The tower crane: the slewing top (turntable, cab, jib, counter-jib and its weights, the cat head and its pendants),
    /// the trolley, the hoist ropes, the hook block and its spreader, the bundle of beams (carried, falling, landed, hoisted).</item>
    /// <item>The trees' crowns swaying, harder as the crosswind rises; the cloud terraces' windsocks filling before each gust.</item>
    /// <item>The wind turbines' rotors turning; the airships on their circuits; the birds wheeling round the frame; the cloud
    /// islands drifting below.</item>
    /// </list>
    /// Everything overhead stays out of the play space; the bundle is where the simulation's state puts it. Deterministic and
    /// allocation free per frame.
    /// </summary>
    public sealed class NativeSkyframeRig
    {
        public const int Capacity=1100;
        public readonly NativeFactoryKit.Writer Parts;
        /// <summary>The stage art's view of the frame's parts: for part i its rig slot (-1: the modelled art has no piece of its
        /// own for it). Nothing draws by it without the stage bank.</summary>
        public readonly int[] Slot=new int[Capacity];
        private void Put(int slot,double x,double y,double z,double hx,double hy,double hz,FactoryQ q,int kind,bool shadow=true){int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n)Slot[n]=slot;}
        private void Bar(double x0,double y0,double z0,double x1,double y1,double z1,double t,int kind,bool shadow=false){int n=Parts.Count;Parts.Bar(x0,y0,z0,x1,y1,z1,t,t,kind,shadow);for(int i=n;i<Parts.Count;i++)Slot[i]=-1;}
        private readonly double[] trees,socks;private readonly int treeCount,sockCount;
        /// <summary>The wind over the stage this frame (0..1; NativeSkyframeLooks.Windy) and the crane's moment (a crane hazard
        /// present: <see cref="HasCrane"/>).</summary>
        public double Wind;public NativeSkyframeLooks.CraneMoment Crane;public bool HasCrane;
        public NativeSkyframeRig(NativeFactoryKit.Looks looks,ContentMap map)
        {
            Parts=new NativeFactoryKit.Writer(Capacity,false,looks);var t=new List<double>();var s=new List<double>();
            foreach(var k in map?.blocks??Array.Empty<ContentBlock>())if(k?.id!=null&&k.id.StartsWith("skyframe-tree-",StringComparison.Ordinal)){t.Add(k.pos.x);t.Add(k.pos.y+k.size.y/2);t.Add(k.pos.z);}
            foreach(var h in map?.hazards??Array.Empty<MapHazard>())if(h?.kind==NativeFactoryHazards.Vent){SockAt(h,out double x,out double y,out double z);s.Add(x);s.Add(y);s.Add(z);s.Add(Math.Atan2(h.axis.x,h.axis.z));}
            trees=t.ToArray();treeCount=trees.Length/3;socks=s.ToArray();sockCount=socks.Length/4;
        }
        public int Trees=>treeCount;public double TreeX(int i)=>trees[i*3];public double TreeTop(int i)=>trees[i*3+1];public double TreeZ(int i)=>trees[i*3+2];
        public int Socks=>sockCount;
        /// <summary>A windsock's ring: on a pole bracketed off the cloud terrace's outer edge (outside the floor: nothing to stand
        /// beside), a step along from the gust's section.</summary>
        public const double SockPole=2.4,SockLength=1.5;
        public static void SockAt(MapHazard h,out double x,out double y,out double z)
        {
            double side=h.axis.z>=0?1:-1;x=h.a.x-side*(h.size.x/2+.5);z=h.a.z+side*(h.size.z/2+.2);y=h.a.y+1.1+SockPole;
        }
        /// <summary>A windsock's lift (0 limp .. 1 full) for the wind's telegraph and gust.</summary>
        public static double SockLift(double warn,double gust,double after)=>Math.Max(Math.Max(gust,Math.Min(1,warn*1.4)),after<1.2?1-after/1.2:0);
        /// <summary>A tree crown's sway at render time t in wind w (0..1): the lean (rad) and its heading (rad).</summary>
        public static void Sway(int i,double t,double w,out double lean,out double heading)
        {
            double a=.018+.07*w;lean=a*(.6+.4*Math.Sin(t*(1.1+.2*H(131,i))+i*1.9))+.012*Math.Sin(t*2.7+i);heading=Math.PI/2+.5*Math.Sin(t*.37+i*2.2);
        }
        private static double H(int a,int b=0,int c=0)=>NativeSkyframeLooks.Hash(a,b,c);
        private const int Steel=NativeSkyframeLooks.Steel,Leaf=NativeSkyframeLooks.Leaf,LeafDark=NativeSkyframeLooks.LeafDark,CraneK=NativeSkyframeLooks.Crane,Dark=NativeSkyframeLooks.CraneDark,Cable=NativeSkyframeLooks.Cable,
            Load=NativeSkyframeLooks.Load,Weight=NativeSkyframeLooks.Weight,Cloth=NativeSkyframeLooks.Cloth,Accent=NativeSkyframeLooks.ClothAccent,White=NativeSkyframeLooks.White,BirdK=NativeSkyframeLooks.Bird,
            CloudK=NativeSkyframeLooks.Cloud,Glass=NativeSkyframeLooks.Glass,Red=NativeSkyframeLooks.Red;

        /// <summary>Fills <see cref="Parts"/> for the hazards and the replicated state (round, phase, FightAt) at field clock `now`
        /// and render clock `render`.</summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render)
        {
            Parts.Clear();double clock=NativeFactoryHazards.Clock(phase,fightAt,now);Wind=NativeSkyframeLooks.Windy(hazards,round,clock);HasCrane=false;int sock=0;
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Crane&&!HasCrane){Crane=NativeSkyframeLooks.CraneAt(h,round,clock);HasCrane=true;CraneRig(h,Crane,render);}
                else if(h.kind==NativeFactoryHazards.Vent&&sock<sockCount){NativeSkyframeLooks.WindAt(h,round,clock,out double w,out double g,out double after);Sock(sock++,SockLift(w,g,after),render);}
            }
            Crowns(render);Turbines(render);Airships(render);Birds(render);Clouds(render);
        }

        // ---------------- the tower crane ----------------
        private void CraneRig(MapHazard h,in NativeSkyframeLooks.CraneMoment m,double t)
        {
            double a=m.Heading,s=Math.Sin(a),c=Math.Cos(a),jy=NativeSkyframeLooks.JibY,ty=jy+NativeSkyframeLooks.JibDepth,L=NativeSkyframeLooks.JibLength,C=NativeSkyframeLooks.CounterJib,w=NativeSkyframeLooks.JibHalf;
            double X(double u,double v)=>s*u+c*v;double Z(double u,double v)=>c*u-s*v;var yaw=FactoryQ.Axis(0,1,0,a);
            // the slewing top: the turntable (the art's frame: the mast's axis at the jib's bottom chord), the machinery deck, the cab
            Put(NativeSkyframeArt.CraneTop,0,jy,0,1.6,.14,1.6,yaw,Dark);Put(-1,0,jy-.7,0,1.25,.55,1.25,yaw,CraneK);
            Put(-1,X(1.5,1.9),jy-.75,Z(1.5,1.9),.8,.85,.75,yaw,CraneK);Put(-1,X(2.32,1.9),jy-.6,Z(2.32,1.9),.02,.5,.62,yaw,Glass,false);
            // the jib: two bottom chords and the top chord, laced; the counter-jib and its weights; the cat head and its pendants
            for(int sd=-1;sd<=1;sd+=2){double v=sd*w;Bar(X(1.2,v),jy,Z(1.2,v),X(L,v),jy,Z(L,v),.16,CraneK,true);}
            Bar(X(1.2,0),ty,Z(1.2,0),X(L-1.5,0),ty-.5,Z(L-1.5,0),.16,CraneK,true);
            for(double u=1.2;u<L-2;u+=2.4)
            {
                double u1=Math.Min(L-1.5,u+2.4),y0=ty-.5*(u-1.2)/(L-2.7),y1=ty-.5*(u1-1.2)/(L-2.7);
                for(int sd=-1;sd<=1;sd+=2){double v=sd*w;Bar(X(u,v),jy,Z(u,v),X(u1,0),y1,Z(u1,0),.07,CraneK);Bar(X(u,0),y0,Z(u,0),X(u,v),jy,Z(u,v),.07,CraneK);}
                Bar(X(u,-w),jy,Z(u,-w),X(u,w),jy,Z(u,w),.06,CraneK);
            }
            for(int sd=-1;sd<=1;sd+=2){double v=sd*w;Bar(X(-1.2,v),jy,Z(-1.2,v),X(-C,v),jy,Z(-C,v),.16,CraneK,true);}
            for(int k=0;k<4;k++)Put(-1,X(-C+1+k*1.05,0),jy-.75,Z(-C+1+k*1.05,0),w+.15,.75,.45,yaw,Weight);
            Put(-1,X(-C*.5,0),jy+.08,Z(-C*.5,0),w,.05,C*.5-.8,yaw,Dark,false);
            double cat=jy+8;for(int sd=-1;sd<=1;sd+=2)Bar(X(0,sd),jy,Z(0,sd),X(0,0),cat,Z(0,0),.14,CraneK,true);
            Bar(X(0,0),cat,Z(0,0),X(L*.62,0),ty-.3,Z(L*.62,0),.05,Cable);Bar(X(0,0),cat,Z(0,0),X(-C+.6,0),jy+.2,Z(-C+.6,0),.05,Cable);
            Put(-1,X(0,0),cat+.25,Z(0,0),.12,.12,.12,yaw,Red,false);Put(-1,X(L,0),jy+.3,Z(L,0),.12,.12,.12,yaw,Red,false);
            // the trolley, the hoist ropes, the hook block and its spreader, the slings
            double r=m.Reach,hx=m.X,hz=m.Z,hy=m.HookY;
            Put(NativeSkyframeArt.Trolley,X(r,0),jy-.3,Z(r,0),w+.1,.22,.75,yaw,Dark);
            for(int sd=-1;sd<=1;sd+=2){double v=sd*.28,top=jy-.5,bot=hy+.9;Put(NativeSkyframeArt.Cable,X(r,v),(top+bot)/2,Z(r,v),.022,(top-bot)/2,.022,FactoryQ.Identity,Cable,false);}
            Put(NativeSkyframeArt.Hook,hx,hy+.45,hz,.32,.45,.26,FactoryQ.Identity,CraneK);Put(-1,hx,hy+.05,hz,2.3,.07,.09,FactoryQ.Identity,CraneK);
            double lt=m.LoadY+h.size.y,sl=m.State.Attached?Math.Max(.05,hy-lt):.9;
            for(int sd=-1;sd<=1;sd+=2)Put(NativeSkyframeArt.Cable,hx+sd*2.2,hy-sl/2,hz,.02,sl/2,.02,FactoryQ.Identity,Cable,false);
            // the bundle of beams, along x all the way (taglines hold its heading), where the simulation's state puts it
            bool free=m.State.Phase==NativeFactoryHazards.Falling||m.State.Landed;double lx=free?m.State.X:hx,lz=free?m.State.Z:hz;
            var roll=FactoryQ.Axis(1,0,0,m.State.Sway*2);
            Put(NativeSkyframeArt.Load,lx,m.LoadY+h.size.y/2,lz,h.size.x/2,h.size.y/2,h.size.z/2,roll,Load);
            for(int sd=-1;sd<=1;sd++)Put(-1,lx+sd*2.2,m.LoadY+h.size.y/2,lz,.05,h.size.y/2+.015,h.size.z/2+.015,roll,Dark,false);
        }

        // ---------------- the trees and the windsocks ----------------
        private void Crowns(double t)
        {
            for(int i=0;i<treeCount;i++)
            {
                double x=trees[i*3],y=trees[i*3+1],z=trees[i*3+2];Sway(i,t,Wind,out double lean,out double head);
                var q=FactoryQ.Axis(Math.Cos(head),0,-Math.Sin(head),lean)*FactoryQ.Axis(0,1,0,i*1.1);
                Put(NativeSkyframeArt.Crown+i%NativeSkyframeArt.Crowns,x,y,z,.2,.2,.2,q,NativeSkyframeLooks.Bark);
                // the box crown: three leaf masses over the trunk's top, leaning with it
                q.Rotate(0,1,0,out double ux,out double uy,out double uz);
                Put(-1,x+ux*1.1,y+uy*1.1,z+uz*1.1,1.7,1.0,1.6,q,Leaf);Put(-1,x+ux*2.0+.3,y+uy*2.0,z+uz*2.0-.2,1.2,.8,1.25,q*FactoryQ.Axis(0,1,0,.6),LeafDark);
                Put(-1,x+ux*.5-.5,y+uy*.5,z+uz*.5+.5,1.1,.7,1.0,q*FactoryQ.Axis(0,1,0,1.1),LeafDark);
            }
        }
        private void Sock(int i,double lift,double t)
        {
            double x=socks[i*4],y=socks[i*4+1],z=socks[i*4+2],yawA=socks[i*4+3];
            // the pole (up past the balustrade from the terrace's edge beam), the ring, the sock: hanging limp, standing out along the
            // wind as it fills
            Put(-1,x,y-(SockPole+1.5)/2,z,.035,(SockPole+1.5)/2,.035,FactoryQ.Identity,Steel);
            double droop=(1-lift)*1.3+Math.Sin(t*(3+6*lift)+i)*(.05+.07*lift),sw=Math.Sin(t*2.3+i*2)*.12*(1-lift);
            var q=FactoryQ.Axis(0,1,0,yawA+sw)*FactoryQ.Axis(1,0,0,droop);q.Rotate(0,0,1,out double dx,out double dy,out double dz);
            Put(NativeSkyframeArt.Windsock,x+dx*SockLength/2,y+dy*SockLength/2,z+dz*SockLength/2,.16,.16,SockLength/2,q,Accent,false);
        }

        // ---------------- the turbines, the airships, the birds, the clouds ----------------
        private void Turbines(double t)
        {
            double R=NativeSkyframeLooks.TurbineRadius;
            for(int i=0;i<NativeSkyframeLooks.Turbines;i++)
            {
                NativeSkyframeLooks.Turbine(i,out double x,out double y,out double z);double a=NativeSkyframeLooks.TurbineAngle(i,t);
                Put(NativeSkyframeArt.Rotor,x-.9,y,z,.45,.45,.45,FactoryQ.Axis(1,0,0,a),White,false);
                for(int k=0;k<3;k++){double b=a+k*2*Math.PI/3,cy=Math.Cos(b),sz=Math.Sin(b);Put(-1,x-.9,y+cy*R/2,z+sz*R/2,.07,R/2,.3,FactoryQ.Axis(1,0,0,b),White,false);}
            }
        }
        private void Airships(double t)
        {
            for(int i=0;i<NativeSkyframeLooks.Airships;i++)
            {
                NativeSkyframeLooks.Airship(i,t,out double x,out double y,out double z,out double head,out double pitch);double L=NativeSkyframeLooks.AirshipLength(i),r=L*.14;
                var q=FactoryQ.Axis(0,1,0,head)*FactoryQ.Axis(1,0,0,-pitch);q.Rotate(0,0,1,out double fx,out double fy,out double fz);q.Rotate(0,1,0,out double ux,out double uy,out double uz);q.Rotate(1,0,0,out double sx,out double sy,out double sz);
                Put(NativeSkyframeArt.Airship+i%NativeSkyframeArt.AirshipShapes,x,y,z,r,r,L/2,q,Cloth,false);
                // the box ship: a tapered hull of three lengths, a gondola, four fins, a stripe
                Put(-1,x+fx*L*.36,y+fy*L*.36,z+fz*L*.36,r*.7,r*.7,L*.16,q,Cloth,false);Put(-1,x-fx*L*.38,y-fy*L*.38,z-fz*L*.38,r*.62,r*.62,L*.14,q,Cloth,false);
                Put(-1,x-ux*(r+.6)+fx*L*.05,y-uy*(r+.6),z-uz*(r+.6)+fz*L*.05,r*.3,.55,L*.11,q,Accent,false);
                for(int sd=-1;sd<=1;sd+=2){Put(-1,x-fx*L*.42+ux*sd*r*.9,y+uy*sd*r*.9-fy*L*.42,z-fz*L*.42+uz*sd*r*.9,.08,r*.6,L*.07,q,Accent,false);Put(-1,x-fx*L*.42+sx*sd*r*.9,y+sy*sd*r*.9-fy*L*.42,z-fz*L*.42+sz*sd*r*.9,r*.6,.08,L*.07,q,Accent,false);}
                Put(-1,x,y,z,r*1.01,r*.12,L*.5*1.01,q,Accent,false);
            }
        }
        private void Birds(double t)
        {
            for(int i=0;i<NativeSkyframeLooks.Birds;i++)
            {
                NativeSkyframeLooks.BirdAt(i,t,out double x,out double y,out double z,out double head,out double bank,out double flap);
                var q=FactoryQ.Axis(0,1,0,head)*FactoryQ.Axis(0,0,1,bank);Put(NativeSkyframeArt.Bird,x,y,z,.07,.06,.26,q,BirdK,false);
                for(int sd=-1;sd<=1;sd+=2)
                {
                    var wq=q*FactoryQ.Axis(0,0,1,sd*flap);wq.Rotate(sd*.42,0,0,out double wx,out double wy,out double wz);
                    Put(sd<0?NativeSkyframeArt.WingL:NativeSkyframeArt.WingR,x+wx,y+wy,z+wz,.42,.015,.13,wq,BirdK,false);
                }
            }
        }
        private void Clouds(double t)
        {
            for(int i=0;i<NativeSkyframeLooks.Clouds;i++)
            {
                NativeSkyframeLooks.CloudAt(i,t,out double x,out double y,out double z,out double hx,out double hy,out double hz,out int shape);
                Put(NativeSkyframeArt.Cloud+shape,x,y,z,hx,hy,hz,FactoryQ.Identity,CloudK,false);
                Put(-1,x+hx*.2,y+hy*1.1,z-hz*.1,hx*.6,hy*.7,hz*.62,FactoryQ.Identity,CloudK,false);Put(-1,x-hx*.3,y+hy*1.9,z+hz*.1,hx*.32,hy*.5,hz*.36,FactoryQ.Identity,CloudK,false);
            }
        }
    }
}
