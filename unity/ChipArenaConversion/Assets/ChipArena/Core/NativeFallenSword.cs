using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's claymore, built in C# in place of the gear bank's sword pieces (the owner, 2026-11-08: "lets make his sword more stylized. Maybe
    /// slightly longer, thinner and overall a better design with glow effects"). In the bank's sword frame (tools/gear/fallen_boss.py to_sword():
    /// x across the edges, y through the flats, z up the blade, the fist's centre at the origin; bank units, the game draws them about 1.9 times):
    ///  - The blade: from the ricasso at .34 to the point at 1.9 (the bank's ran .15 to 1.65), a little under three quarters as wide (.046 half width
    ///    to the bank's .068) and thinner through the flats. A barb each side where it leaves the ricasso, swept back like a broken pinion; a slight
    ///    waist, a long leaf, and a needle taper. Hollow-ground: a bright edge bevel, the flats, a fuller channel up its lower two thirds, a ridge
    ///    above it to the point.
    ///  - Light in the fuller: a thin line of white light down the channel, both flats, tapering at each end (its own piece, so its glow is the
    ///    phase's). Phase 3's blade is black with its edges of light; phase 4's is all light, black flakes coming off it.
    ///  - The hilt: a crossguard of broken wings folding forward (three long primaries and a covert each side, swept out and up the blade, a hook
    ///    curling down under them), a boss with a point of light on each flat, the ricasso wrapped in chain, a long wrapped grip, and the pommel a
    ///    closed eye: a slit of light, opened in phases 3 and 4.
    ///  - The veins climbing from the guard (phase 1 a little way, phase 2 halfway: Runtime cuts them along the blade) and the words LUX CECIDIT
    ///    (the bank's, fitted to these flats by Runtime).
    /// Rule-free: the blade's reach is drawn only. Runtime NativeFallenBossBody draws the pieces on hand_r (FbSwordPiece). Pure.
    /// </summary>
    public static class NativeFallenSword
    {
        /// <summary>The pieces, in the order Runtime keeps them: and the phases each shows in (bit 0 phase 1).</summary>
        public const int BladeSteel=0,BladeDark=1,BladeLight=2,Fuller=3,Hilt=4,EyeShut=5,EyeOpen=6,Veins1=7,Veins2=8,Flakes=9,Count=10;
        public static readonly string[] Names={"blade-steel","blade-dark","blade-light","fuller","hilt","eye-shut","eye-open","veins-1","veins-2","flakes"};
        public static readonly int[] Phases={3,4,8,7,15,3,12,1,2,8};
        /// <summary>Where the blade leaves the ricasso, its point (z, bank units), and the ricasso's foot.</summary>
        public const float BladeFrom=.34f,Point=1.9f,RicassoFrom=.135f,HalfWidth=.046f;
        /// <summary>The blade's half width at t (0 where it leaves the ricasso, 1 the point).</summary>
        public static float Width(float t)
        {
            t=Math.Max(0,Math.Min(1,t));
            if(t<.012f)return .03f+(.064f-.03f)*t/.012f;
            if(t<.05f)return .064f+(.044f-.064f)*Smooth((t-.012f)/.038f);
            float w=HalfWidth*(1-.07f*(float)Math.Sin(Math.PI*Math.Min(1,(t-.05f)/.5f)));
            if(t>.55f)w*=1+.05f*(float)Math.Sin(Math.PI*Math.Min(1,(t-.55f)/.3f));
            if(t>.72f)w*=Math.Max(0,1-(float)Math.Pow((t-.72f)/.28f,1.6f));
            return w;
        }
        /// <summary>The flats' half thickness at t, and the fuller channel's floor there (the ridge above it, where it ends).</summary>
        public static float Flat(float t)=>.0088f*(1-.5f*t)+.0006f;
        public static float FullerFloor(float t){float f=Flat(t);float k=Smooth(t/.05f)*(1-Smooth((t-.6f)/.1f));return f-(.0032f*k)+.0016f*Smooth((t-.62f)/.12f);}
        /// <summary>The z of t along the blade.</summary>
        public static float Z(float t)=>BladeFrom+(Point-BladeFrom)*t;
        /// <summary>A face's height off the middle at a share of the half width across (0 the centre, 1 the edge), t along.</summary>
        public static float Surface(float across,float t)
        {
            float w=Width(t),pinch=Math.Min(1,w/.02f),f=Flat(t)*pinch,c=FullerFloor(t)*pinch,b=(.0022f+.05f*w)*pinch;
            return across<.3f?c+(f-c)*across/.3f:across<.8f?f+(b-f)*(across-.3f)/.5f:b*(1-(across-.8f)/.2f);
        }

        public static NativeFallenKit Build(int piece,int tier)
        {
            var k=new NativeFallenKit();
            switch(piece)
            {
                case BladeSteel:BladeOf(k,tier,NativeFallenKit.Steel,NativeFallenKit.Plate,false,false);break;
                case BladeDark:BladeOf(k,tier,NativeFallenKit.Black,NativeFallenKit.Light,false,true);break;
                case BladeLight:BladeOf(k,tier,NativeFallenKit.Light,NativeFallenKit.Light,true,true);break;
                case Fuller:FullerOf(k,tier);break;
                case Hilt:HiltOf(k,tier);break;
                case EyeShut:EyeOf(k,false);break;
                case EyeOpen:EyeOf(k,true);break;
                case Veins1:VeinsOf(k,tier,.12f,1);break;
                case Veins2:VeinsOf(k,tier,.55f,2);break;
                case Flakes:FlakesOf(k,tier);break;
            }
            k.Finish();return k;
        }

        // ---- the blade ----
        // A section round the blade at t: edge, bevel, flat, centre (fuller or ridge), flat, bevel, the other edge, and back along the other face.
        private static Vector3[] Section(float t)
        {
            float z=Z(t),w=Width(t),pinch=Math.Min(1,w/.02f),f=Flat(t)*pinch,c=FullerFloor(t)*pinch,b=(.0022f+.05f*w)*pinch;
            return new[]{new Vector3(w,0,z),new Vector3(w*.8f,b,z),new Vector3(w*.3f,f,z),new Vector3(0,c,z),new Vector3(-w*.3f,f,z),new Vector3(-w*.8f,b,z),
                new Vector3(-w,0,z),new Vector3(-w*.8f,-b,z),new Vector3(-w*.3f,-f,z),new Vector3(0,-c,z),new Vector3(w*.3f,-f,z),new Vector3(w*.8f,-b,z)};
        }
        // The bands from one section corner to the next: the bevels (edge colour) and the flats; each its own corners, so the bevel stays sharp.
        private static readonly bool[] EdgeBand={true,false,false,false,false,true,true,false,false,false,false,true};
        private static void BladeOf(NativeFallenKit k,int tier,uint body,uint edge,bool bodyGlow,bool edgeGlow)
        {
            int rows=tier==0?44:20;var secs=new Vector3[rows+1][];for(int r=0;r<=rows;r++)secs[r]=Section(Rows(r,rows));
            for(int band=0;band<12;band++)
            {
                int nb=(band+1)%12;bool e=EdgeBand[band];uint col=e?edge:body;bool glow=e?edgeGlow:bodyGlow;int start=k.Count;
                for(int r=0;r<=rows;r++){k.V(secs[r][band],col);k.V(secs[r][nb],col);}
                for(int r=0;r<rows;r++)
                {
                    int a=start+r*2,b=a+1,c=a+3,d=a+2;var mid=(secs[r][band]+secs[r][nb])*.5f;var o=new Vector3(mid.X,mid.Y,0);if(o.LengthSquared()<1e-12f)o=new Vector3(0,band<6?1:-1,0);
                    k.Quad(a,b,c,d,o,glow);
                }
            }
            // The foot, closed against the ricasso.
            var foot=secs[0];k.Cap(foot,new Vector3(0,0,foot[0].Z),-Vector3.UnitZ,body,bodyGlow);
        }
        // Rows bunch toward the barbs and the point, where the outline turns fastest.
        private static float Rows(int r,int rows)
        {
            float u=r/(float)rows;return u<.16f?.06f*u/.16f:.06f+.94f*(float)Math.Pow((u-.16f)/.84f,.92f);
        }
        private static void FullerOf(NativeFallenKit k,int tier)
        {
            int rows=tier==0?30:14;
            for(int face=1;face>=-1;face-=2)
            {
                int start=k.Count;
                for(int r=0;r<=rows;r++)
                {
                    float t=.03f+.6f*r/rows;float w=Width(t)*.13f*Smooth((t-.03f)/.06f)*(1-Smooth((t-.5f)/.13f))+.0006f;float y=face*(FullerFloor(t)+.00045f);float z=Z(t);
                    k.V(new Vector3(w,y,z),NativeFallenKit.Light,false);k.V(new Vector3(-w,y,z),NativeFallenKit.Light,false);
                }
                for(int r=0;r<rows;r++){int a=start+r*2;k.Quad(a,a+1,a+3,a+2,new Vector3(0,face,0),true);}
            }
        }

        // ---- the hilt ----
        private static void HiltOf(NativeFallenKit k,int tier)
        {
            int n=tier==0?14:8;
            // The grip, wrapped, and its collars.
            k.Tube(new[]{new Vector3(0,0,-.128f),new Vector3(0,0,-.02f),new Vector3(0,0,.088f)},new[]{.0172f,.0168f,.0176f},n,NativeFallenKit.Black);
            int wraps=tier==0?11:5;for(int i=0;i<wraps;i++){float z=-.116f+i*.196f/(wraps-1);k.Torus(new Vector3(0,0,z),Vector3.UnitZ,.0178f,.0029f,n,4,NativeFallenKit.SteelDark);}
            foreach(float z in new[]{-.128f,.088f})k.Tube(new[]{new Vector3(0,0,z-.007f),new Vector3(0,0,z+.007f)},.0215f,n,NativeFallenKit.Steel);
            // The pommel: a closed eye, an almond of steel with a rim, a spike under it.
            k.Ellipsoid(new Vector3(0,0,-.17f),new Vector3(.034f,0,0),new Vector3(0,.019f,0),new Vector3(0,0,.04f),n,tier==0?10:6,NativeFallenKit.Steel);
            var rim=new List<Vector3>();int rs=tier==0?20:10;for(int i=0;i<rs;i++){double a=2*Math.PI*i/rs;float s=(float)Math.Sin(a),c=(float)Math.Cos(a);rim.Add(new Vector3(.036f*s*(.6f+.4f*Math.Abs(s)),0,-.17f+.043f*c));}
            var rr=new float[rim.Count];for(int i=0;i<rr.Length;i++)rr[i]=.0036f;k.Tube(rim,rr,4,NativeFallenKit.SteelDark,false,true,false,true);
            k.Blade(new Vector3(0,0,-.205f),-Vector3.UnitZ,Vector3.UnitX,Vector3.UnitY,.06f,new[]{.011f,.009f,.005f,0},NativeFallenKit.SteelDark,false,true,.9f);
            // The boss of the guard, a point of light on each flat.
            k.Ellipsoid(new Vector3(0,0,.112f),new Vector3(.05f,0,0),new Vector3(0,.023f,0),new Vector3(0,0,.03f),n,tier==0?8:5,NativeFallenKit.Steel);
            for(int face=1;face>=-1;face-=2)Diamond(k,new Vector3(0,face*.0232f,.114f),.012f,.019f,face);
            // The wings: three primaries and a covert each side, swept out then up the blade, a hook curling down under them.
            for(int side=1;side>=-1;side-=2)
            {
                // The primaries fan from swept out (the lowest) to nearly up the blade, each a narrow blade with a hooked tip, a notch out of the
                // longest (a broken pinion); the coverts shorter over them on each face.
                for(int i=0;i<3;i++)
                {
                    var b=new Vector3(side*(.03f+.01f*i),0,.11f+.005f*i);float reach=new[]{.25f,.2f,.14f}[i],rise=new[]{.13f,.22f,.3f}[i];
                    var ctl=new[]{b,b+new Vector3(side*reach*.42f,0,-.012f+.01f*i),b+new Vector3(side*reach*.82f,0,rise*.3f),b+new Vector3(side*reach,0,rise*.72f),b+new Vector3(side*reach*.9f,0,rise)};
                    Feather(k,ctl,i==0?new[]{.008f,.015f,.017f,.006f,.014f,.009f,0}:new[]{.008f,.015f,.017f,.015f,.011f,.006f,0},i==1?NativeFallenKit.PlateTrim:NativeFallenKit.Steel,tier);
                }
                for(int face=1;face>=-1;face-=2)
                {
                    var b=new Vector3(side*.035f,face*.0105f,.112f);var ctl=new[]{b,b+new Vector3(side*.06f,0,.012f),b+new Vector3(side*.105f,0,.06f),b+new Vector3(side*.1f,0,.115f)};
                    Feather(k,ctl,new[]{.008f,.013f,.013f,.008f,0},NativeFallenKit.SteelDark,tier);
                }
                var h=new Vector3(side*.04f,0,.1f);Feather(k,new[]{h,h+new Vector3(side*.05f,0,-.03f),h+new Vector3(side*.065f,0,-.085f),h+new Vector3(side*.05f,0,-.115f)},new[]{.01f,.014f,.01f,.005f,0},NativeFallenKit.SteelDark,tier);
            }
            // The ricasso, a flat bar wrapped in chain.
            var bar=new List<Vector3[]>();var bc=new List<Vector3>();foreach(float z in new[]{RicassoFrom,.2f,.3f,BladeFrom+.002f}){float w=z>.3f?.024f:.0215f;bar.Add(NativeFallenKit.Ring(new Vector3(0,0,z),Vector3.UnitX,Vector3.UnitY,w,.0098f,n,5));bc.Add(new Vector3(0,0,z));}
            k.Loft(bar,bc,NativeFallenKit.SteelDark,false,true,true,false);
            int links=tier==0?9:4;for(int i=0;i<links;i++){float z=.152f+i*.165f/(links-1);double a=i*1.75;var at=new Vector3((float)Math.Cos(a)*.024f,(float)Math.Sin(a)*.0115f,z);var ax=new Vector3((float)Math.Sin(a),(float)-Math.Cos(a)*.5f,.55f);k.Torus(at,ax,.011f,.0026f,tier==0?10:6,4,NativeFallenKit.Chain,false,false);}
        }
        // A feather of the guard along a curve: its broad side to the flats.
        private static void Feather(NativeFallenKit k,Vector3[] ctl,float[] widths,uint color,int tier)
        {
            var pts=NativeFallenKit.Curve(ctl,tier==0?3:2);var across=new Vector3[pts.Count];var w=new float[pts.Count];
            for(int i=0;i<pts.Count;i++){var t=i+1<pts.Count?pts[i+1]-pts[i]:pts[i]-pts[i-1];across[i]=Vector3.Cross(Vector3.UnitY,Vector3.Normalize(t));float u=i/(float)(pts.Count-1)*(widths.Length-1);int j=Math.Min(widths.Length-2,(int)u);w[i]=widths[j]+(widths[j+1]-widths[j])*(u-j);}
            k.Quill(pts,across,w,color,false,true,.3f);
        }
        // A small flat diamond of light on a face.
        private static void Diamond(NativeFallenKit k,Vector3 c,float hx,float hz,int face)
        {
            int m=k.V(c+new Vector3(0,face*.0012f,0),NativeFallenKit.Light,false);int a=k.V(c+new Vector3(hx,0,0),NativeFallenKit.Light,false),b=k.V(c+new Vector3(0,0,hz),NativeFallenKit.Light,false),d=k.V(c+new Vector3(-hx,0,0),NativeFallenKit.Light,false),e=k.V(c+new Vector3(0,0,-hz),NativeFallenKit.Light,false);
            var o=new Vector3(0,face,0);k.Tri(m,a,b,o,true);k.Tri(m,b,d,o,true);k.Tri(m,d,e,o,true);k.Tri(m,e,a,o,true);
        }
        // The pommel's eye on each flat: shut, a slit of light; open, an almond of light round a black pupil.
        private static void EyeOf(NativeFallenKit k,bool open)
        {
            for(int face=1;face>=-1;face-=2)
            {
                float y=face*.0196f,hx=open?.024f:.025f,hz=open?.016f:.0028f;int c=k.V(new Vector3(0,y+face*.0016f,-.17f),NativeFallenKit.Light,false);int s=k.Count;const int seg=14;
                for(int i=0;i<seg;i++){double a=2*Math.PI*i/seg;k.V(new Vector3(hx*(float)Math.Sin(a),y,-.17f+hz*(float)Math.Cos(a)),NativeFallenKit.Light,false);}
                for(int i=0;i<seg;i++)k.Tri(c,s+i,s+(i+1)%seg,new Vector3(0,face,0),true);
                if(open)k.Ellipsoid(new Vector3(0,y+face*.002f,-.17f),new Vector3(.0065f,0,0),new Vector3(0,.0018f,0),new Vector3(0,0,.0085f),8,5,NativeFallenKit.Black,false,false);
            }
        }

        // ---- the veins, the flakes ----
        private static void VeinsOf(NativeFallenKit k,int tier,float reach,int seed)
        {
            var r=new NativeFallenKit.Rand(seed*31+7);int count=tier==0?9:4;
            for(int i=0;i<count;i++)for(int face=1;face>=-1;face-=2)
            {
                float x=r.Range(-.7f,.7f);float t=-.02f;float top=reach*r.Range(.55f,1);var pts=new List<Vector3>();
                pts.Add(new Vector3(x*.022f,face*.0105f,.2f+r.Range(0,.1f)));
                while(t<top){float u=Math.Max(0,t);pts.Add(new Vector3(x*Width(u),face*(Surface(Math.Abs(x),u)+.0006f),Z(t)));t+=r.Range(.03f,.06f);x=Math.Max(-.9f,Math.Min(.9f,x+r.Range(-.3f,.3f)));}
                if(pts.Count>1)k.Tube(pts,.0024f*(1+reach),4,NativeFallenKit.Vein,false,false,false);
            }
        }
        private static void FlakesOf(NativeFallenKit k,int tier)
        {
            var r=new NativeFallenKit.Rand(44);int count=tier==0?44:16;
            for(int i=0;i<count;i++)
            {
                float t=r.Range(.02f,.95f);float w=Width(t);float x=r.Range(-1.15f,1.15f)*w;float y=(r.Next()<.5f?1:-1)*r.Range(.012f,.05f);var at=new Vector3(x,y,Z(t));
                var way=Vector3.Normalize(new Vector3(r.Range(-.4f,.4f),r.Range(-.3f,.3f),1));k.Blade(at,way,Vector3.UnitX,Vector3.UnitY,r.Range(.02f,.065f),new[]{r.Range(.005f,.016f),r.Range(.005f,.018f),0},NativeFallenKit.Black);
            }
        }
        private static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
    }
}
