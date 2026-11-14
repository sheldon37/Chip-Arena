using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Katana and its saya (owner 2026-11-10: "very high quality designed katana ... really great designs"; original geometry,
    // no copy of any real or fictional signature sword). In the sword family's frame: the right hand at the origin just under the
    // tsuba, the blade along +z, its flats +-y, the cutting edge on +x and the back (mune) on -x, so the sori bends the blade (and,
    // barely, the grip) toward -x about the tsuba. The blade is a loft of true shinogi-zukuri sections: the sharp edge, the hardened
    // band (ha) up to a lit, wandering hamon in the pilot's accent, the ji up to the ridge (shinogi), a bo-hi groove in the
    // shinogi-ji, the roofed back, and past the yokote a curved kissaki ending on the back line. Below it a two-step habaki, seppa
    // either side of a pierced iron tsuba (a crescent moon over a bank of cloud, cut through, gilt in the cuts, a gilt rim), and a
    // waisted tsuka: ito crossing over pale ray skin that shows in sunken diamonds, a menuki on each face, fuchi and kashira.
    // Two handed: Support is the left hand's place near the kashira. The hilt is Rigid; the blade stretches from the habaki's
    // mouth (StretchBase) to the tip. The saya is its own model in its own frame: the mouth (koiguchi) at the origin, the body
    // along +z on the same curve (the sheathed sword sits KatanaSayaSeat down its own z), black lacquer, a sageo wound below the
    // kurigata, a kojiri, and a lit line inlaid down both faces. Presentation only: nothing here changes reach or hits.
    public static partial class NativeWeaponModels
    {
        /// <summary>The sori: the sword's line leaves the grip axis by KatanaCurve * (z - KatanaCurveZ)^2 toward -x (m).</summary>
        public const float KatanaCurve=.095f,KatanaCurveZ=.0655f;
        /// <summary>Along the grip axis (m): the kashira's end, the tsuka's two ends, the tsuba's two faces, where the blade leaves
        /// the habaki (the blade base and the stretch base), the yokote, and the tip.</summary>
        public const float KatanaButtZ=-.232f,KatanaTsukaZ0=-.206f,KatanaTsukaZ1=.044f,KatanaTsubaZ0=.061f,KatanaTsubaZ1=.070f,KatanaBaseZ=.115f,KatanaYokoteZ=.995f,KatanaTipZ=1.09f;
        /// <summary>The blade's width at the habaki and at the yokote, its thickness at both, and the tsuba's radius (m).</summary>
        public const float KatanaWidth=.056f,KatanaTipWidth=.041f,KatanaThick=.017f,KatanaTipThick=.012f,KatanaTsubaRadius=.061f;
        /// <summary>The left hand's place on the tsuka (z, m).</summary>
        public const float KatanaSupportZ=-.14f;
        /// <summary>The saya: its length from the mouth, and where the sheathed katana's own z sits at the mouth (the habaki goes in).</summary>
        public const float SayaLength=1.05f,KatanaSayaSeat=.073f;
        /// <summary>How far the katana's line has left the grip axis at z (toward -x, so negative).</summary>
        public static float KatanaBend(float z){float d=z-KatanaCurveZ;return -KatanaCurve*d*d;}
        /// <summary>The saya's centre line at z from its mouth (saya frame).</summary>
        public static float SayaBend(float z)=>KatanaBend(z+KatanaSayaSeat)-KatanaBend(KatanaSayaSeat);
        /// <summary>The sockets (katana frame): the real tip (on the back line), the blade's middle where it leaves the habaki, the
        /// left hand's grip.</summary>
        public static readonly Vector3 KatanaBladeTip=V(KatanaBend(KatanaTipZ)-KatanaWidth/2,0,KatanaTipZ),KatanaBladeBase=V(KatanaBend(KatanaBaseZ),0,KatanaBaseZ),KatanaSupport=V(KatanaBend(KatanaSupportZ),0,KatanaSupportZ);
        /// <summary>The saya (saya frame): the mouth's middle, the axis there, and the middle of its far end.</summary>
        public static readonly Vector3 SayaMouth=V(0,0,0),SayaAxis=V(0,0,1),SayaEnd=V(SayaBend(SayaLength),0,SayaLength);
        /// <summary>The katana's Design (not registered here: the class's wiring adds it to the registry).</summary>
        public static Design KatanaDesign(string spec)=>new Design{Spec=spec,Build=BuildKatana,StretchBase=KatanaBaseZ,StretchTip=KatanaTipZ,BladeTip=KatanaBladeTip,BladeBase=KatanaBladeBase,Support=KatanaSupport};

        // ---- hand-laid faces, turned to face `toward` (local frame) ----
        private static void QuadTo(WeaponSculpt k,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 toward)
        {
            var n=Vector3.Cross(d-a,e-b);if(Vector3.Dot(n,toward)<0){var t=b;b=e;e=t;}k.Quad(c,a,b,d,e);
        }
        private static void TriTo(WeaponSculpt k,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 toward)
        {
            var n=Vector3.Cross(b-a,d-a);if(Vector3.Dot(n,toward)<0){var t=b;b=d;d=t;}k.Tri(c,a,b,d);
        }

        // ---- the blade ----
        // One shinogi-zukuri section (16 points, counter-clockwise from the edge): the back at xb, width w, thickness t, the hamon's
        // middle `ham` in from the edge and half as wide as `half`, the bo-hi `groove` deep. Bands: 0/15 the hardened edge, 1/14 the
        // hamon, 2/13 the ji, 3/6/9/12 the shinogi-ji, 4/5/10/11 the groove, 7/8 the roofed back.
        private const float KatanaRidge=.37f;
        private static Vector3[] KatanaRing(float z,float xb,float w,float t,float ham,float half,float groove)
        {
            if(w<=1e-4f)return Point(16,V(xb,0,z));
            float xe=xb+w,xs=xb+w*KatanaRidge,ht=t/2,sh=t*.33f,span=xe-xs;
            ham=Math.Min(ham,span*.82f);half=Math.Min(half,ham*.6f);
            float x1=xe-(ham-half),x2=xe-(ham+half),xa=xs-(xs-xb)*.2f,xl=xs-(xs-xb)*.8f,xm=(xa+xl)/2;
            float Ji(float x)=>ht*(xe-x)/span;float Sj(float x)=>sh+(ht-sh)*(x-xb)/(xs-xb);
            var o=new Vector3[16];
            o[0]=V(xe,0,z);o[1]=V(x1,Ji(x1),z);o[2]=V(x2,Ji(x2),z);o[3]=V(xs,ht,z);o[4]=V(xa,Sj(xa),z);o[5]=V(xm,Sj(xm)-groove,z);o[6]=V(xl,Sj(xl),z);o[7]=V(xb,sh,z);
            o[8]=V(xb-t*.2f,0,z);
            for(int i=1;i<8;i++)o[16-i]=V(o[i].X,-o[i].Y,z);
            return o;
        }
        // The hamon's place in from the edge as a share of the width at z: a slow swell with a quicker ripple on it, calm at the
        // habaki (the yakidashi) and settling into the kissaki.
        private static float KatanaHamon(float z,bool ripple)
        {
            float u=(z-KatanaBaseZ)/(KatanaYokoteZ-KatanaBaseZ),calm=Math.Min(1,Math.Max(0,u*7))*Math.Min(1,Math.Max(0,(1-u)*9+.25f));
            float wave=.05f*(float)Math.Sin(z*48f+.6f)+(ripple?.024f*(float)Math.Sin(z*21f+2f):0);
            return .27f+wave*calm;
        }
        private static void KatanaBlade(WeaponSculpt k)
        {
            var ha=Paint(0xf4faf8,.3f);var ji=Paint(0x93a3ad,.1f);var sj=Paint(0x778893,.12f);var hi=Paint(0x4d5a63,.04f);var mune=Paint(0x8b9aa3,.1f);var point=Paint(0xdbe7e9,.22f);
            var hamon=WeaponPaint.Accent(k.Fine?1.25f:1.4f,.5f);
            int body=k.Fine?46:k.Mid?22:10,tip=k.Fine?8:k.Mid?4:3;float half=k.Fine?.0024f:k.Mid?.0027f:.0034f;var rings=new List<Vector3[]>();
            for(int i=0;i<=body;i++)
            {
                float u=(float)i/body,z=KatanaBaseZ+(KatanaYokoteZ-KatanaBaseZ)*u,w=KatanaWidth+(KatanaTipWidth-KatanaWidth)*u,t=KatanaThick+(KatanaTipThick-KatanaThick)*u;
                // The bo-hi: in from the habaki, out before the yokote.
                float groove=k.Mid&&z>KatanaBaseZ+.03f&&z<KatanaYokoteZ-.07f?.0032f:0;
                rings.Add(KatanaRing(z,KatanaBend(z)-KatanaWidth/2,w,t,w*KatanaHamon(z,k.Fine),half,groove));
            }
            for(int j=1;j<=tip;j++)
            {
                // The kissaki: the edge rounds up (fukura) to meet the back, the section thinning with it.
                float u=(float)j/tip,z=KatanaYokoteZ+(KatanaTipZ-KatanaYokoteZ)*u,w=KatanaTipWidth*(1-(float)Math.Pow(u,1.55f)),t=KatanaTipThick*(1-.6f*u);
                rings.Add(KatanaRing(z,KatanaBend(z)-KatanaWidth/2,w,t,w*.27f,half,0));
            }
            k.Loft(rings,(i,s)=>s==1||s==14?hamon:s==0||s==15?ha:s==2||s==13?(i>=body?point:ji):s==7||s==8?mune:s==4||s==5||s==10||s==11?(i>=body?sj:hi):sj,0,false,false);
        }

        // ---- the tsuba: a pierced plate ----
        // Where the plate is cut through along the ray at angle a from the blade's middle (plate frame: +y a flat's side): the hole's
        // near and far radius. The moon, toward +y: one disc less another set up and aside. The cloud, toward -y: three discs joined,
        // their feet cut level on an arc. False where the ray meets iron all the way.
        private static bool Chord(float a,float cx,float cy,float r,out float near,out float far)
        {
            float dx=(float)Math.Cos(a),dy=(float)Math.Sin(a),b=dx*cx+dy*cy,disc=b*b-(cx*cx+cy*cy-r*r);near=far=0;
            if(disc<=0||b<=0)return false;float s=(float)Math.Sqrt(disc);near=b-s;far=b+s;return true;
        }
        private static bool KatanaTsubaCut(float a,out float lo,out float hi)
        {
            lo=hi=0;
            if(Chord(a,-.002f,.0362f,.0128f,out float n1,out float f1))
            {
                lo=n1;hi=f1;
                if(Chord(a,.0036f,.0412f,.0112f,out float n2,out float f2)&&n2<f1&&f2>n1)
                {
                    // What is left of the first disc's chord: the longer piece of it.
                    float below=Math.Max(0,Math.Min(n2,f1)-n1),above=Math.Max(0,f1-Math.Max(f2,n1));
                    if(below>=above){lo=n1;hi=n1+below;}else{lo=f1-above;hi=f1;}
                }
                return hi-lo>.0006f;
            }
            const float foot=.0305f;bool any=false;float near=float.MaxValue,far=0;
            for(int c=0;c<3;c++)
            {
                float at=(c==0?244f:c==1?268f:292f)*Pi/180,rr=c==1?.0372f:.0348f,r=c==0?.0076f:c==1?.0102f:.0086f;
                if(Chord(a,rr*(float)Math.Cos(at),rr*(float)Math.Sin(at),r,out float n,out float f)){any=true;near=Math.Min(near,n);far=Math.Max(far,f);}
            }
            if(!any)return false;lo=Math.Max(foot,near);hi=far;return hi-lo>.0006f;
        }
        private static void KatanaTsuba(WeaponSculpt k,WeaponPaint iron,WeaponPaint gilt,WeaponPaint rim,WeaponPaint lit)
        {
            float z0=KatanaTsubaZ0,z1=KatanaTsubaZ1,R=KatanaTsubaRadius,web=R-.0055f;
            if(!k.Mid)
            {
                // Far away: the plate whole, its rim gilt.
                k.Lathe(i=>i==1?rim:iron,new[]{web,z0,R,z0+.0015f,R,z1-.0015f,web,z1},k.Sides(20),0,Pi/20);return;
            }
            // The gilt rim (mimi), standing a little proud of the plate.
            k.Lathe(i=>rim,new[]{web-.001f,z0-.0008f,R-.0022f,z0-.0012f,R,z0+.0028f,R,z1-.0028f,R-.0022f,z1+.0012f,web-.001f,z1+.0008f,web-.001f,z0-.0008f},k.Sides(40),1,0,false);
            // The plate between the seppa-dai (under the seppa) and the rim, sampled round the blade: finer where it is cut.
            int steps=k.Fine?144:72,keep=k.Fine?4:2;var open=new bool[steps];var lo=new float[steps];var hi=new float[steps];
            for(int i=0;i<steps;i++)open[i]=KatanaTsubaCut(i*2*Pi/steps,out lo[i],out hi[i]);
            var at=new List<int>();for(int i=0;i<steps;i++)if(i%keep==0||open[i]||open[(i+1)%steps]||open[(i+steps-1)%steps])at.Add(i);
            float pz0=z0+.0012f,pz1=z1-.0012f;
            // A lit hairline inlaid round both faces, inside the rim.
            if(k.Fine)for(int f=0;f<2;f++)
            {
                int seg=36;float z=f==0?pz1+.0004f:pz0-.0004f,ra=web-.0048f,rb=web-.0034f;
                for(int i=0;i<seg;i++){float a=i*2*Pi/seg,b=(i+1)*2*Pi/seg;QuadTo(k,lit,V(ra*(float)Math.Cos(a),ra*(float)Math.Sin(a),z),V(rb*(float)Math.Cos(a),rb*(float)Math.Sin(a),z),V(rb*(float)Math.Cos(b),rb*(float)Math.Sin(b),z),V(ra*(float)Math.Cos(b),ra*(float)Math.Sin(b),z),V(0,0,f==0?1:-1));}
            }
            for(int s=0;s<at.Count;s++)
            {
                int i=at[s],j=at[(s+1)%at.Count];float a=i*2*Pi/steps,b=(j<=i?j+steps:j)*2*Pi/steps;
                Vector3 At(float ang,float r,float z)=>V(r*(float)Math.Cos(ang),r*(float)Math.Sin(ang),z);
                float Core(float ang){float c=(float)Math.Cos(ang)/.034f,sn=(float)Math.Sin(ang)/.019f;return 1/(float)Math.Sqrt(c*c+sn*sn);}
                float ia=Core(a),ib=Core(b);var up=V(0,0,1);
                if(!open[i]&&!open[j])
                {
                    QuadTo(k,iron,At(a,ia,pz1),At(a,web,pz1),At(b,web,pz1),At(b,ib,pz1),up);QuadTo(k,iron,At(a,ia,pz0),At(a,web,pz0),At(b,web,pz0),At(b,ib,pz0),-up);continue;
                }
                // A cut closing at one end runs out to a point at its neighbour's middle.
                float la=open[i]?lo[i]:(lo[j]+hi[j])/2,ha=open[i]?hi[i]:la,lb=open[j]?lo[j]:(lo[i]+hi[i])/2,hb=open[j]?hi[j]:lb;
                for(int f=0;f<2;f++)
                {
                    float z=f==0?pz1:pz0;var n=f==0?up:-up;
                    QuadTo(k,iron,At(a,ia,z),At(a,la,z),At(b,lb,z),At(b,ib,z),n);QuadTo(k,iron,At(a,ha,z),At(a,web,z),At(b,web,z),At(b,hb,z),n);
                }
                float mid=(a+b)/2;var radial=V((float)Math.Cos(mid),(float)Math.Sin(mid),0);
                QuadTo(k,gilt,At(a,la,pz0),At(b,lb,pz0),At(b,lb,pz1),At(a,la,pz1),radial);
                QuadTo(k,gilt,At(a,ha,pz0),At(b,hb,pz0),At(b,hb,pz1),At(a,ha,pz1),-radial);
            }
        }

        // ---- the tsuka: ito over ray skin ----
        private const float TsukaW=.056f,TsukaH=.037f,TsukaC=.009f;
        private static Vector3[] TsukaRing(float z,float grow=0)
        {
            float u=(z-KatanaTsukaZ0)/(KatanaTsukaZ1-KatanaTsukaZ0),s=1-.075f*(float)Math.Sin(Math.Max(0,Math.Min(1,u))*Math.PI);
            return Rect(TsukaW*s+grow*2,TsukaH*s+grow*2,TsukaC*s+grow*.6f,z,KatanaBend(z));
        }
        private static void KatanaTsuka(WeaponSculpt k,WeaponPaint same,WeaponPaint ito,WeaponPaint under,WeaponPaint gold)
        {
            int cells=k.Mid?10:5,rows=cells*2;var r=new Vector3[rows+1][];
            for(int j=0;j<=rows;j++)r[j]=TsukaRing(KatanaTsukaZ0+(KatanaTsukaZ1-KatanaTsukaZ0)*j/rows);
            // The same rings a little proud between the crossings: the turns of the wrap stand as ribs round the edges.
            var rib=new Vector3[rows+1][];for(int j=0;j<=rows;j++)rib[j]=k.Fine&&j%2==1?TsukaRing(KatanaTsukaZ0+(KatanaTsukaZ1-KatanaTsukaZ0)*j/rows,.0014f):r[j];
            // Round the edges the wrap lies turn on turn.
            // (A rib keeps the plain ring where it borders a face, so the faces stay flat.)
            Vector3 Rib(int j,int c)=>c==2||c==3||c==6||c==7?r[j][c]:rib[j][c];
            for(int j=0;j<rows;j++)for(int s=0;s<8;s++)
            {
                if(s==2||s==6)continue;int s1=(s+1)%8;var outward=(r[j][s]+r[j][s1])*.5f-V(KatanaBend(r[j][s].Z),0,r[j][s].Z);
                QuadTo(k,j%2==0?ito:under,Rib(j,s),Rib(j,s1),Rib(j+1,s1),Rib(j+1,s),outward);
            }
            // On each face the two strands cross between the cells; in each cell the ray skin shows in a sunken diamond.
            for(int face=0;face<2;face++)
            {
                int p=face==0?2:6,q=p+1;var n=V(0,face==0?1:-1,0);float depth=k.Mid?.0024f:0;
                for(int i=0;i<cells;i++)
                {
                    Vector3 A=r[2*i][p],B=r[2*i][q],Mp=r[2*i+1][p],Mq=r[2*i+1][q],C=r[2*i+2][q],D=r[2*i+2][p],Cb=(A+B)/2,Ct=(C+D)/2,O=(Mp+Mq)/2;
                    Vector3 dB=Vector3.Lerp(O,Cb,.74f),dT=Vector3.Lerp(O,Ct,.74f),dP=Vector3.Lerp(O,Mp,.9f),dQ=Vector3.Lerp(O,Mq,.9f);
                    // The strand over the other at each crossing, turn about.
                    WeaponPaint pa=i%2==0?ito:under,pb=i%2==0?under:ito,pc=i%2==0?under:ito,pd=i%2==0?ito:under;
                    TriTo(k,pa,A,Cb,dB,n);TriTo(k,pa,A,dB,dP,n);TriTo(k,pa,A,dP,Mp,n);
                    TriTo(k,pb,B,Mq,dQ,n);TriTo(k,pb,B,dQ,dB,n);TriTo(k,pb,B,dB,Cb,n);
                    TriTo(k,pc,C,Ct,dT,n);TriTo(k,pc,C,dT,dQ,n);TriTo(k,pc,C,dQ,Mq,n);
                    TriTo(k,pd,D,Mp,dP,n);TriTo(k,pd,D,dP,dT,n);TriTo(k,pd,D,dT,Ct,n);
                    Vector3 In(Vector3 v)=>O+(v-O)*.8f-n*depth;Vector3 fB=In(dB),fT=In(dT),fP=In(dP),fQ=In(dQ);
                    QuadTo(k,same,fB,fP,fT,fQ,n);
                    if(depth>0)
                    {
                        var shade=same.Scaled(.62f);
                        QuadTo(k,shade,dB,dP,fP,fB,O-(dB+dP)*.5f+n*.004f);QuadTo(k,shade,dP,dT,fT,fP,O-(dP+dT)*.5f+n*.004f);
                        QuadTo(k,shade,dT,dQ,fQ,fT,O-(dT+dQ)*.5f+n*.004f);QuadTo(k,shade,dQ,dB,fB,fQ,O-(dQ+dB)*.5f+n*.004f);
                    }
                    // The menuki: one toward the tsuba on this face, one toward the kashira on the other, lying in the wrap.
                    if(k.Mid&&i==(face==0?6:3))
                    {
                        var m=O-n*.0012f;k.Push(T(m.X,m.Y,m.Z));k.Sphere(gold,Vector3.Zero,V(.0075f,.0048f,.019f),k.Sides(6),k.Sides(8),true);k.Pop();
                    }
                }
            }
        }

        /// <summary>The katana, in the right hand's frame (two sections: the Rigid hilt, the Blade that stretches with reach).</summary>
        public static void BuildKatana(WeaponSculpt k)
        {
            var gold=Paint(Gold,.14f);var goldDark=Paint(GoldDark,.08f);var iron=Paint(0x24272e,.04f);var ironLit=Paint(0x343842,.06f);
            var same=Paint(0xe8e1cf,.08f);var ito=Paint(0x232838,.03f);var under=Paint(0x1a1e2b,.02f);var glow=WeaponPaint.Accent(2.2f,.45f);
            k.Into(RightHand,WeaponStretch.Rigid);
            // The kashira: an iron cap with a gilt lip, and a lit stud in its end.
            float bz=KatanaButtZ,bx=KatanaBend(bz);
            k.Loft(new[]{TsukaRing(bz,-.006f),TsukaRing(bz+.006f,.0022f),TsukaRing(KatanaTsukaZ0-.004f,.0022f),TsukaRing(KatanaTsukaZ0-.004f,.0034f),TsukaRing(KatanaTsukaZ0,.0034f),TsukaRing(KatanaTsukaZ0,.0005f)},
                (i,s)=>i==3?gold:i==0?ironLit:iron,0,true,false);
            if(k.Mid){k.Push(Rx(Pi)*T(bx,0,bz+.0005f));k.Gem(glow,Vector3.Zero,.0075f,.006f,k.Sides(6),.12f);k.Pop();}
            KatanaTsuka(k,same,ito,under,gold);
            // The fuchi: the collar under the tsuba.
            float fz=KatanaTsukaZ1;
            k.Loft(new[]{TsukaRing(fz,.0005f),TsukaRing(fz,.0034f),TsukaRing(fz+.004f,.0034f),TsukaRing(fz+.004f,.0022f),TsukaRing(fz+.014f,.0022f)},(i,s)=>i==1?gold:iron,0,false,false);
            // Seppa, the tsuba between them, and the habaki in two steps.
            for(int s=0;s<2;s++){float a=s==0?fz+.014f:KatanaTsubaZ1,b=s==0?KatanaTsubaZ0:KatanaSayaSeat;k.Lathe(i=>gold,new[]{.035f,a,.037f,(a+b)/2,.035f,b},k.Sides(20),0,Pi/20,true,1,.62f);}
            KatanaTsuba(k,iron,goldDark,gold,WeaponPaint.Accent(1.5f,.4f));
            float hx=-KatanaWidth/2;
            Vector3[] Collar(float z,float grow)=>KatanaRing(z,KatanaBend(z)+hx-grow,KatanaWidth+grow*2,KatanaThick+grow*2,.012f,.002f,0);
            k.Loft(new[]{Collar(KatanaSayaSeat,.0042f),Collar(.094f,.0042f),Collar(.095f,.0024f),Collar(KatanaBaseZ-.002f,.0024f),Collar(KatanaBaseZ,.0008f)},(i,s)=>i==1||i==3?goldDark:s==0||s==15||s==7||s==8?gold.Scaled(.88f):gold,0,false,false);
            k.Into(RightHand,WeaponStretch.Blade);
            KatanaBlade(k);
        }

        // ---- the saya ----
        // One section of the saya (14 points round an oval, counter-clockwise; bands 3 and 10 the inlaid line down each face).
        private static readonly float[] SayaAngles={0,30,62,85.5f,94.5f,118,150,180,210,242,265.5f,274.5f,298,330};
        private static Vector3[] SayaRing(float z,float scale,float grow=0)
        {
            var o=new Vector3[14];float taper=1-.13f*z/SayaLength,rx=.0385f*taper*scale+grow,ry=.0195f*taper*scale+grow,cx=SayaBend(z);
            for(int i=0;i<14;i++){double a=SayaAngles[i]*Math.PI/180;o[i]=V(cx+rx*(float)Math.Cos(a),ry*(float)Math.Sin(a),z);}
            return o;
        }
        /// <summary>The saya in its own frame: the mouth at the origin, the body along +z on the blade's curve (the edge side +x).
        /// Written into whatever section the caller has opened.</summary>
        public static void BuildSaya(WeaponSculpt k)
        {
            var lacquer=Paint(0x14161c,.1f);var gloss=Paint(0x2a2e3a,.2f);var horn=Paint(0x30333c,.08f);var gold=Paint(Gold,.14f);var goldDark=Paint(GoldDark,.08f);
            var silk=Paint(0xd9d2bf,.05f);var silkDark=Paint(0x8d8672,.03f);var hollow=Paint(0x07080a);var line=WeaponPaint.Accent(1.7f,.4f);
            const float inlay0=.2f,inlay1=.965f,cap=.992f;int n=k.Fine?18:k.Mid?10:5;
            var rings=new List<Vector3[]>();var kind=new List<int>();
            void Add(float z,float scale,int paint){rings.Add(SayaRing(z,scale));kind.Add(paint);}
            // The koiguchi: a horn mouth with a gilt band; then the lacquer, the inlaid line, and the kojiri.
            // (The mouth is hollow: the habaki seats in it.)
            Add(.046f,.86f,5);Add(0,.86f,1);Add(0,1.07f,1);Add(.014f,1.07f,2);Add(.014f,1.1f,2);Add(.02f,1.1f,0);Add(.02f,1,0);
            for(int i=0;i<=n;i++)Add(inlay0+(inlay1-inlay0)*i/n,1,i<n?3:0);
            Add(cap,1,2);Add(cap,1.07f,2);Add(cap+.006f,1.07f,4);Add(SayaLength-.016f,1.02f,4);Add(SayaLength-.005f,.8f,4);Add(SayaLength,.45f,4);
            k.Loft(rings,(i,s)=>
            {
                int p=kind[i];if(p==5)return hollow;if(p==1)return horn;if(p==2)return gold;if(p==4)return goldDark;
                if(p==3&&(s==3||s==10))return line;
                return s==2||s==4||s==9||s==11?gloss:lacquer;
            },0,true,true,hollow,false,false,goldDark);
            // The sageo, wound turn on turn below the mouth, and the kurigata it ties through on the outer face.
            int turns=k.Fine?7:k.Mid?4:1;var cord=new List<Vector3[]>();float c0=.074f,c1=.146f;
            for(int i=0;i<=turns*2;i++){float z=c0+(c1-c0)*i/(turns*2);cord.Add(SayaRing(z,1,i==0||i==turns*2?.0012f:i%2==1?.0046f:.0032f));}
            k.Loft(cord,(i,s)=>i%2==0?silk:silkDark,0,true,true);
            float kz=(c0+c1)/2,kx=SayaBend(kz),ky=.0195f*(1-.13f*kz/SayaLength);
            k.Box(horn,V(kx,ky+.0085f,kz),V(.021f,.013f,.034f),.004f);
            if(k.Mid)
            {
                k.Box(silk,V(kx,ky+.0125f,kz),V(.027f,.008f,.012f),.003f);k.Box(gold,V(kx,ky+.0152f,kz+.0115f),V(.009f,.002f,.005f),0);k.Box(gold,V(kx,ky+.0152f,kz-.0115f),V(.009f,.002f,.005f),0);
            }
        }
    }
}
