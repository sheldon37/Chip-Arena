using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The Glaive's chips on the screen (docs/GLAIVE.md "The chips"). Face: a card of cold light, faint orbits round the emblem, a crescent
    // sweeping along its top and a glint circling its edge like the halo, each chip's emblem large behind the text and its own small loop.
    // Reveal: what happens when one is taken, a different piece of the ring's light for each of the ten, over the "INSTALLED" card, the card
    // itself knocked, turned and pulsed in time with it (Pose). All pure functions of the clock; nothing is allocated per frame; one soft
    // glow under Reduced.
    public static class NativeGlaiveChipArt
    {
        private static readonly Color Cyan=U.Hex(0x4fe8ff),Ice=U.Hex(0xb8f6ff),Steel=U.Hex(0x9aa6b2),SteelDark=U.Hex(0x3a444f),Ink=U.Hex(0x05090c),
            Water=U.Hex(0x1f6fa8),Foam=U.Hex(0xd8f6ff),Hostile=U.Hex(0xff5a4a);
        private static readonly string[] Counts={"×1","×2","×3","×4"};
        // Shatterpoint's shard (a sliver, in its box's fractions) and its flat profile.
        private static readonly Vector2[] ShardShape={new Vector2(0,.5f),new Vector2(.7f,0),new Vector2(1,.55f),new Vector2(.55f,1)};
        private static readonly float[] ShardAt={0,1},ShardAlpha={1,1};
        private static float R(int seed,int i){float x=Mathf.Sin(seed*12.9898f+i*78.233f)*43758.5453f;return x-Mathf.Floor(x);}
        private static float S(int seed,int i)=>R(seed,i)*2-1;
        private static float Out(float x){x=Mathf.Clamp01(x);float o=1-x;return 1-o*o*o;}
        private static float InOut(float x){x=Mathf.Clamp01(x);return x<.5f?4*x*x*x:1-Mathf.Pow(-2*x+2,3)/2;}
        private static float Ramp(float x,float a,float b)=>Mathf.Clamp01((x-a)/Mathf.Max(1e-4f,b-a));
        private static float Bell(float x,float a,float b){float k=Ramp(x,a,b);return 4*k*(1-k);}
        private static Vector2 Dir(int seed,int i){float a=R(seed,i)*Mathf.PI*2;return new Vector2(Mathf.Cos(a),Mathf.Sin(a));}
        private static Vector2 Polar(float a,float r)=>new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
        private static void Streak(Vector2 a,Vector2 b,float w,Color c)
        {
            var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=0||w<=0)return;var m=(a+b)*.5f;U.Push(m,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(m.x-len/2,m.y-w/2,len,w),c);U.Pop();
        }
        private static void Soft(Vector2 p,float r,Color c){if(r>0&&c.a>0)U.Radial(new Rect(p.x-r,p.y-r,2*r,2*r),c);}
        private static void Blob(Vector2 p,float w,float h,Color c,float deg=0){if(c.a<=0||w<=0||h<=0)return;U.Push(p,Vector2.zero,1,deg);U.Round(new Rect(p.x-w/2,p.y-h/2,w,h),c,Mathf.Min(w,h)/2);U.Pop();}
        private static void Ring(Vector2 p,float r,float w,Color c,int seg=26)
        {
            if(c.a<=0||r<=0)return;Vector2 last=p+new Vector2(r,0);for(int i=1;i<=seg;i++){float a=i*Mathf.PI*2/seg;var q=p+Polar(a,r);Streak(last,q,w,c);last=q;}
        }
        // An ellipse (x radius `rx`, y radius `ry`) turned `deg`.
        private static void Ellipse(Vector2 p,float rx,float ry,float w,Color c,int seg=32,float deg=0)
        {
            if(c.a<=0||rx<=0||ry<=0)return;float cs=Mathf.Cos(deg*Mathf.Deg2Rad),sn=Mathf.Sin(deg*Mathf.Deg2Rad);Vector2 At(float a){float x=Mathf.Cos(a)*rx,y=Mathf.Sin(a)*ry;return p+new Vector2(x*cs-y*sn,x*sn+y*cs);}
            var last=At(0);for(int i=1;i<=seg;i++){var q=At(i*Mathf.PI*2/seg);Streak(last,q,w,c);last=q;}
        }
        // An arc of a circle (radians, y down) as a band `w` wide; `taper` thins both ends to points, as a crescent's horns.
        private static void Arc(Vector2 p,float r,float from,float to,float w,Color c,int seg=18,bool taper=false)
        {
            if(c.a<=0||r<=0)return;var last=p+Polar(from,r);
            for(int i=1;i<=seg;i++){float u=i/(float)seg,mid=(i-.5f)/seg;var q=p+Polar(from+(to-from)*u,r);Streak(last,q,taper?w*Mathf.Sin(Mathf.PI*mid)+.6f:w,c);last=q;}
        }
        // A glint: two thin crossing flares and a white heart.
        private static void Glint(Vector2 p,float r,Color c,float a,float deg=0)
        {
            if(a<=0||r<=0)return;var d=Polar(deg*Mathf.Deg2Rad,r);var n=new Vector2(-d.y,d.x)*.55f;Soft(p,r*.55f,U.A(c,.6f*a));
            Streak(p-d,p+d,2.2f,U.A(c,a));Streak(p-n,p+n,1.8f,U.A(c,.8f*a));Soft(p,r*.18f,U.A(Color.white,a));
        }
        // Sparks flung out of a point (`age` s after), in two colours.
        private static void Burst(Vector2 p,float age,float size,Color c,float a,int seed=1,int n=12)
        {
            if(age<0||age>.5f)return;float w=age/.5f;if(age<.08f)Soft(p,size*(.5f+2*age),U.A(Color.white,(1-age/.08f)*a));
            for(int i=0;i<n;i++){var d=Dir(seed,i);float len=size*(.4f+.6f*R(seed,i+20));Streak(p+d*(size*.25f+len*Out(w)*.55f),p+d*(size*.25f+len*Out(w)),2.4f*(1-w)+.6f,U.A(i%2==0?c:Color.white,(1-w)*a));}
        }
        private static void Hex(Vector2 p,float r,float w,Color c){if(c.a<=0||r<=0)return;var last=p+Polar(Mathf.PI/6,r);for(int k=1;k<=6;k++){var q=p+Polar(Mathf.PI/6+k*Mathf.PI/3,r);Streak(last,q,w,c);last=q;}}
        // The ring blade, small and flat-on: its glow, a steel ring with a lit edge and inner rim, three crescent fangs swept off it with
        // the spin (radians), the grip across its middle.
        private static void Blade(Vector2 p,float r,float spin,float a,Color edge)
        {
            if(a<=0||r<=0)return;Soft(p,r*2.1f,U.A(edge,.28f*a));
            for(int k=0;k<3;k++){float f=spin+k*Mathf.PI*2/3;Arc(p,r*1.18f,f,f+1.25f,r*.34f,U.A(Steel,a),8,true);Arc(p,r*1.34f,f+.15f,f+1.3f,1.6f,U.A(edge,a),8,true);}
            Ring(p,r*.78f,r*.34f,U.A(Steel,a),20);Ring(p,r*.96f,1.6f,U.A(edge,a),20);Ring(p,r*.6f,1.2f,U.A(edge,.75f*a),16);
            var g=Polar(spin*.25f+.6f,r*.62f);Streak(p-g,p+g,r*.2f,U.A(SteelDark,a));Soft(p,r*.3f,U.A(Color.white,.5f*a));
        }

        // ---- the card ----
        /// <summary>The face, over the body and under the core, the text and the bezel's light.</summary>
        public static void Face(Rect r,float radius,string id,float t,float a,bool plain,bool calm)
        {
            if(plain||a<=0)return;float bw=NativeChipArt.BezelOf(radius),tc=NativeChipArt.TopCut(radius),bc=NativeChipArt.BottomCut(radius);var face=Rect.MinMaxRect(r.x+bw,r.y+tc*.55f,r.xMax-bw,r.yMax-bc*.55f);int index=NativeGlaiveChips.Index(id);
            U.BeginBand(face);
            float g=Mathf.Min(face.width*.56f,face.height*.72f);var gr=new Rect(face.xMax-g*.9f,face.yMax-g*.92f,g,g);var gc=gr.center;
            // Faint orbits round the emblem, tipped like the halo seen from the side, and a crescent sweeping along the top.
            for(int i=0;i<3;i++)Ellipse(gc,g*(.55f+.2f*i),g*(.55f+.2f*i)*.4f,1.1f,U.A(Cyan,(.11f-.03f*i)*a),36,-12);
            Arc(new Vector2(face.center.x,face.y+face.height*1.7f),face.height*1.82f,-Mathf.PI*.7f,-Mathf.PI*.3f,3.4f,U.A(Cyan,.2f*a),26,true);
            string glyph=NativeGlaiveChips.Glyph(id);
            if(glyph!=null)NativeIcons.DrawMarkup(gr,"glaive-face:"+id,glyph,NativeGlaiveChips.GlyphBox,U.A(Cyan,.2f*a),Mathf.CeilToInt(g));
            if(!calm&&index>=0)Scene(gr,index,t,a);
            U.EndBand();
            if(calm)return;
            // A glint circling the edge like the halo, its trail fading behind it.
            float per=2*(r.width+r.height);Vector2 At(float d){d=Mathf.Repeat(d,per);float w=r.width-4,h=r.height-4,x0=r.x+2,y0=r.y+2;if(d<w)return new Vector2(x0+d,y0);d-=w;if(d<h)return new Vector2(x0+w,y0+d);d-=h;if(d<w)return new Vector2(x0+w-d,y0+h);d-=w;return new Vector2(x0,y0+h-Mathf.Min(h,d));}
            float lead=t*per*.32f;var prev=At(lead);for(int i=1;i<=10;i++){var q=At(lead-i*9);Streak(prev,q,3.2f*(1-i/11f),U.A(Cyan,.7f*(1-i/11f)*a));prev=q;}
            Glint(At(lead),9,Ice,.9f*a,Mathf.Repeat(t*90,360));
        }
        // Each chip's own small loop on the card.
        private static void Scene(Rect g,int index,float t,float a)
        {
            Vector2 C(float x,float y)=>new Vector2(g.x+g.width*x,g.y+g.height*y);float u=g.width/100;
            switch(index)
            {
                // A spark hopping between the zigzag's turns.
                case 0:{float k=Mathf.Repeat(t/1.6f,1)*4;int i=(int)k;float f=k-i;Vector2 P(int n)=>n==0?C(.26f,.56f):n==1?C(.42f,.16f):n==2?C(.56f,.54f):C(.76f,.62f);var p=Vector2.Lerp(P(i),P(Mathf.Min(3,i+1)),f);Glint(p,5*u,Ice,.8f*a,t*200);break;}
                // A glint running along the edge.
                case 1:{float k=Mathf.Repeat(t/2.2f,1);if(k<.5f){float w=k/.5f;Glint(Vector2.Lerp(C(.14f,.86f),C(.88f,.14f),w),7*u,Color.white,Mathf.Sin(Mathf.PI*w)*.9f*a,-45);}break;}
                // Two motes round the orbit the opposite ways.
                case 2:{for(int i=0;i<2;i++){float ang=(i==0?1:-1)*t*2.4f+i*Mathf.PI-Mathf.PI/2;Glint(C(.5f,.5f)+Polar(ang,38*u),4*u,Ice,.85f*a,t*150);}break;}
                // The shards drifting out and back.
                case 3:{float k=.5f+.5f*Mathf.Sin(t*2.4f);for(int i=0;i<6;i++){var d=Polar(-Mathf.PI/2+i*Mathf.PI/3,1);Soft(C(.5f,.5f)+d*(26+10*k)*u,2.5f*u,U.A(Ice,.6f*a));}break;}
                // Motes pulled in to the ring.
                case 4:{for(int i=0;i<5;i++){float k=Mathf.Repeat(t*.7f+i*.2f,1);var p=Vector2.Lerp(C(.04f,.15f+.17f*i),C(.76f,.5f),k*k);Soft(p,(2.6f-1.4f*k)*u,U.A(Ice,.7f*Mathf.Sin(Mathf.PI*k)*a));}break;}
                // The ring out on its loop and back.
                case 5:{float k=Mathf.Repeat(t/1.8f,1);float s=Mathf.Sin(Mathf.PI*k);var p=C(.28f,.56f)+new Vector2(52*u*s,-30*u*Mathf.Sin(2*Mathf.PI*k));Blade(p,5*u,t*9,.7f*a,Cyan);break;}
                // A slow spiral of motes winding in.
                case 6:{for(int i=0;i<8;i++){float k=Mathf.Repeat(t*.5f+i/8f,1),rr=(40-34*k)*u,ang=i*.8f+t*2+k*5;Soft(C(.5f,.5f)+Polar(ang,rr),2.2f*u,U.A(Ice,.65f*Mathf.Sin(Mathf.PI*k)*a));}break;}
                // A glint travelling the long loop.
                case 7:{float k=Mathf.Repeat(t/2.6f,1);float ang=(170+250*k)*Mathf.Deg2Rad;Glint(C(.46f,.52f)+Polar(ang,40*u),5*u,Ice,.85f*a,t*120);break;}
                // Ripples spreading on the water line.
                case 8:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t*.6f+i*.33f,1);Ellipse(C(.22f+.24f*i,.82f),(4+14*k)*u,(1.4f+4*k)*u,1.1f,U.A(Ice,.5f*(1-k)*a),20);}break;}
                // A crescent turning round the hexes.
                default:{float ang=t*1.3f;Arc(C(.5f,.52f),44*u,ang,ang+1.6f,3*u,U.A(Ice,.5f*a),12,true);break;}
            }
        }

        // ---- the moment it goes in: the spotlight's card ----
        /// <summary>When the animation starts after the card shows (s).</summary>
        public const float Start=.55f;
        /// <summary>How the card itself moves with its animation at reveal time `t`: knocked (px), scaled across and up about its foot, turned (degrees).</summary>
        public static void Pose(string id,float t,bool calm,out Vector2 offset,out float sx,out float sy,out float degrees)
        {
            offset=Vector2.zero;sx=sy=1;degrees=0;if(calm)return;float s=t-Start;if(s<0)return;float shake=0;
            switch(NativeGlaiveChips.Index(id))
            {
                // Fourth Bounce: knocked a little toward each corner the ring strikes, settling as it comes home.
                case 0:{for(int i=0;i<4;i++){float at=.18f+.28f*i;if(s<at)continue;float w=s-at,k=Mathf.Exp(-w*12)*7;offset+=new Vector2(i==1||i==2?k:-k,i<2?-k:k);}float home=s-1.36f;if(home>=0){float k=Mathf.Exp(-home*7)*Mathf.Cos(home*20);sx=sy=1+.04f*k;}break;}
                // Keen Edge: a sharp shiver as the cut opens, a breath as it seals.
                case 1:{shake=Pulse(s,.36f,.25f,8);sx=1+.015f*Bell(s,.36f,1.1f);sy=1+.02f*Bell(s,1.05f,1.7f);break;}
                // Twin Orbit: breathing with the rings, a pulse where they meet.
                case 2:{float meet=s-1.82f;sx=sy=1+.012f*Mathf.Sin(s*6)*(1-Ramp(s,1.6f,1.8f))+(meet>=0?.05f*Mathf.Exp(-meet*6)*Mathf.Cos(meet*18):0);break;}
                // Shatterpoint: trembling as it cracks, jolted by the burst.
                case 3:{shake=4*Ramp(s,.15f,.5f)*(s<.5f?1:0)+Pulse(s,.5f,.35f,14);if(s>=.5f){float w=s-.5f;sx=sy=1+.06f*Mathf.Exp(-w*7)*Mathf.Cos(w*16);}break;}
                // Magnet Return: drawn in toward the ring, then the snap.
                case 4:{float pull=Ramp(s,.2f,1.5f)*(s<1.55f?1:0);sx=sy=1-.03f*pull;float w=s-1.55f;if(w>=0){sx=sy=1+.05f*Mathf.Exp(-w*7)*Mathf.Cos(w*18);}shake=Pulse(s,1.55f,.25f,6);break;}
                // Quick Catch: the catch yanks it back, then it springs forward.
                case 5:{float w=s-1;if(w>=0){float k=Mathf.Exp(-w*5)*Mathf.Cos(w*12);offset=new Vector2(-22*k,0);degrees=-3*k;sx=1+.04f*k;}shake=Pulse(s,1,.2f,5);break;}
                // Whirlwind: twisted by the vortex, jolted by the cut.
                case 6:{degrees=5*Mathf.Sin(s*14)*Ramp(s,.2f,.9f)*(1-Ramp(s,1.3f,1.5f));shake=Pulse(s,1.36f,.3f,10);float w=s-1.36f;if(w>=0){sx=sy=1+.05f*Mathf.Exp(-w*8)*Mathf.Cos(w*18);}break;}
                // Long Arc: leaning after the ring as it goes round.
                case 7:{float u=InOut(Ramp(s,.05f,1.7f));degrees=3*Mathf.Sin(2*Mathf.PI*u)*(1-Ramp(s,1.7f,2.1f));offset=new Vector2(10*Mathf.Sin(Mathf.PI*u),0);shake=Pulse(s,1.72f,.2f,4);break;}
                // Skimming Stone: a bob at each skip.
                case 8:{for(int i=0;i<3;i++){float at=i==0?.32f:i==1?.72f:1.05f;if(s<at)continue;float w=s-at;offset+=new Vector2(0,(5-1.2f*i)*Mathf.Exp(-w*9)*Mathf.Sin(w*24));}break;}
                // Crescent Guard: the dome rises with it; the shot pushes it back.
                default:{float w=s-1.4f;if(w>=0){float k=Mathf.Exp(-w*6)*Mathf.Cos(w*14);offset=new Vector2(12*k,-3*k);degrees=2*k;}sy=1+.02f*Bell(s,.85f,1.3f);shake=Pulse(s,1.4f,.25f,7);break;}
            }
            offset+=new Vector2(Mathf.Sin(t*71)*shake,Mathf.Sin(t*83+1)*shake);
        }
        private static float Pulse(float s,float at,float life,float size)=>s<at||s>at+life?0:size*(1-(s-at)/life);
        /// <summary>The chip's animation over the spotlight's `card` at reveal time `t` (s), at the block's `env` alpha.</summary>
        public static void Reveal(Rect card,string id,float t,float env,bool calm)
        {
            float s=t-Start;if(env<=0||s<0)return;int index=NativeGlaiveChips.Index(id);if(index<0)return;float a=env;var c=card.center;float W=card.width;
            if(calm){float w=Mathf.Clamp01(s/.5f);Soft(c,W*.7f,U.A(Cyan,.22f*(1-w)*a));return;}
            switch(index)
            {
                case 0:FourthBounce(card,s,a);break;
                case 1:KeenEdge(card,s,a);break;
                case 2:TwinOrbit(card,s,a);break;
                case 3:Shatterpoint(card,id,s,a);break;
                case 4:MagnetReturn(card,s,a);break;
                case 5:QuickCatch(card,s,a);break;
                case 6:Whirlwind(card,s,a);break;
                case 7:LongArc(card,s,a);break;
                case 8:SkimmingStone(card,s,t,a);break;
                default:CrescentGuard(card,s,a);break;
            }
            // Every one ends with a soft breath of the edge's light over the card.
            float end=s-2.2f;if(end>=0&&end<.6f)Soft(c,W*.65f,U.A(Cyan,.12f*(1-end/.6f)*a));
        }
        // Fourth Bounce: thrown in from off the left, the ring ricochets off the four corners in turn (a ping, a ring of light, sparks and the
        // count at each), its wake behind it, then comes home into the emblem in a flash.
        private static readonly float[] BounceAt={0,.18f,.46f,.74f,1.02f,1.36f};
        private static void FourthBounce(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;Vector2 P(int i){switch(i){case 0:return new Vector2(card.x-W*.35f,card.y+H*.55f);case 1:return new Vector2(card.x+W*.02f,card.y+H*.04f);case 2:return new Vector2(card.x+W*.98f,card.y+H*.06f);case 3:return new Vector2(card.x+W*.97f,card.y+H*.96f);case 4:return new Vector2(card.x+W*.03f,card.y+H*.94f);default:return new Vector2(card.x+W*.72f,card.y+H*.56f);}}
            Vector2 Path(float x){if(x<=0)return P(0);for(int i=0;i<5;i++)if(x<BounceAt[i+1])return Vector2.Lerp(P(i),P(i+1),(x-BounceAt[i])/(BounceAt[i+1]-BounceAt[i]));return P(5);}
            float r=W*.05f;
            if(s<BounceAt[5])
            {
                var pos=Path(s);var prev=pos;for(int j=1;j<=14;j++){float sb=s-j*.011f;if(sb<0)break;var q=Path(sb);Streak(prev,q,7*(1-j/15f),U.A(Cyan,.55f*(1-j/15f)*a));Streak(prev,q,2.4f*(1-j/15f),U.A(Color.white,.6f*(1-j/15f)*a));prev=q;}
                Blade(pos,r,s*16,a,Cyan);
            }
            for(int i=1;i<=4;i++)
            {
                float w=s-BounceAt[i];if(w<0)continue;var p=P(i);Burst(p,w,W*.11f,Cyan,a,i*7,14);
                if(w<.5f){Ring(p,8+W*.16f*Out(w/.5f),2.2f*(1-w/.5f)+.5f,U.A(Ice,(1-w/.5f)*a),24);Glint(p,W*.05f*(1-w/.5f),Color.white,a,i*30);}
                float o=(1-Ramp(w,.7f,1.05f))*a;if(o>0){var inward=(card.center-p).normalized*W*.1f;var q=p+inward;U.Text(new Rect(q.x-50,q.y-16,100,32),Counts[i-1],18+4*i,U.A(Ice,o),TextAnchor.MiddleCenter,bold:true,mono:true,glow:U.A(Cyan,.6f*o),shadow:U.A(Ink,.9f*o));}
            }
            float hw=s-BounceAt[5];
            if(hw>=0)
            {
                var home=P(5);float k=1-Ramp(hw,0,.5f);Soft(home,W*.28f*k+4,U.A(Color.white,.85f*(1-Ramp(hw,0,.35f))*a));Ring(home,10+W*.32f*Out(hw/.6f),3.2f*(1-Ramp(hw,0,.6f)),U.A(Cyan,(1-Ramp(hw,0,.6f))*a),32);
                Blade(home,r*(1+.35f*Bell(hw,0,.25f)),s*16*(1-.8f*Ramp(hw,0,.6f)),(1-Ramp(hw,.8f,1.2f))*a,Cyan);Burst(home,hw,W*.16f,Ice,a,41,16);
            }
        }
        // Keen Edge: the edge's glint sweeps along the diagonal, a hair-thin cut opens behind it and splits the face, light bleeds out of the
        // gap, then it seals from end to end in a run of shimmer and a last glint.
        private static void KeenEdge(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var A=new Vector2(card.x-W*.04f,card.y+H*.92f);var B=new Vector2(card.x+W*1.04f,card.y+H*.08f);var d=B-A;var along=d.normalized;var n=new Vector2(-along.y,along.x);
            if(s<.42f){float u=Out(Ramp(s,0,.34f));var p=Vector2.Lerp(A,B,u);float o=(1-Ramp(s,.32f,.42f))*a;Streak(Vector2.Lerp(A,B,Mathf.Max(0,u-.4f)),p,4,U.A(Ice,.75f*o));Glint(p,W*.14f,Color.white,o,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg+90);Soft(p,W*.12f,U.A(Cyan,.35f*o));}
            if(s>=.34f&&s<1.95f)
            {
                float open=Out(Ramp(s,.34f,.52f)),seal=InOut(Ramp(s,1.05f,1.6f)),gap=1.2f+6.5f*open*(1-seal);var from=Vector2.Lerp(A,B,seal);float o=(1-Ramp(s,1.6f,1.95f))*a;
                if(seal<1)
                {
                    Streak(from,B,gap+3,U.A(Ink,.9f*a));Streak(from+n*(gap*.5f+1.2f),B+n*(gap*.5f+1.2f),1.7f,U.A(Cyan,a));Streak(from-n*(gap*.5f+1.2f),B-n*(gap*.5f+1.2f),1.7f,U.A(Cyan,a));
                    for(int i=0;i<7;i++){float k=(i+.5f)/7;if(k<seal)continue;Soft(Vector2.Lerp(A,B,k),9+gap*2,U.A(Cyan,.22f*open*a));}
                    if(seal>0)Glint(from,W*.06f,Color.white,a,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
                }
                // Light spilling out of the cut and falling away.
                for(int i=0;i<12;i++){float k=Ramp(s,.4f+.05f*i,1.25f+.05f*i);if(k<=0||k>=1)continue;var p=Vector2.Lerp(A,B,.08f+.84f*R(5,i))+n*(S(6,i)*6)+new Vector2(S(7,i)*14*k,H*.55f*k*k);Soft(p,3.2f*(1-k)+1,U.A(Ice,(1-k)*a));}
                // The shimmer of the seal: a thin bright line over where it closed.
                if(seal>0)Streak(A,from,1.3f,U.A(Color.white,.7f*o));
            }
            if(s>=1.6f&&s<2.1f){float w=Ramp(s,1.6f,2.1f);Glint(Vector2.Lerp(A,B,.5f),W*.22f*(1-w)+2,Color.white,(1-w)*a,-Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg+90);}
        }
        // Twin Orbit: two rings come round the card from the foot in opposite directions, each laying a circle of light behind it; they meet
        // at the top, the circles flash and fade.
        private static void TwinOrbit(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var c=card.center;float rx=W*.6f,ry=H*.64f;const float Total=3*Mathf.PI,Meet=1.82f;
            float u=InOut(Ramp(s,.05f,Meet)),swept=Total*u;Vector2 E(float ang)=>c+new Vector2(Mathf.Cos(ang)*rx,Mathf.Sin(ang)*ry);
            float flash=s>=Meet?Mathf.Exp(-(s-Meet)*3):0,gone=1-Ramp(s,Meet+.3f,Meet+.9f);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?1:-1,head=Mathf.PI/2+sign*swept,back=Mathf.Min(swept,2*Mathf.PI);const int Seg=40;
                // Its circle of light, brightest at the head.
                for(int i=0;i<Seg;i++){float f0=i/(float)Seg,f1=(i+1f)/Seg;if(f0*2*Mathf.PI>back)break;float a0=head-sign*f0*2*Mathf.PI,a1=head-sign*Mathf.Min(back,f1*2*Mathf.PI);float o=((1-f0)*.6f+.15f+.7f*flash)*gone*a;Streak(E(a0),E(a1),2.4f+3*(1-f0),U.A(side==0?Cyan:Ice,o));}
                if(s<Meet+.1f)Blade(E(head),W*.045f,sign*s*18,a,side==0?Cyan:Ice);
            }
            if(s>=Meet){float w=s-Meet;var top=E(Mathf.PI*1.5f);Soft(top,W*.2f*(1-Ramp(w,0,.5f))+3,U.A(Color.white,.9f*(1-Ramp(w,0,.4f))*a));Burst(top,w,W*.14f,Ice,a,23,16);Ring(top,8+W*.25f*Out(w/.6f),3*(1-Ramp(w,0,.6f)),U.A(Cyan,(1-Ramp(w,0,.6f))*a),28);}
        }
        // Shatterpoint: the emblem appears in the middle and glows, cracks spread out of it, it bursts in a flash and a shockwave and its
        // shards spin out off the card and fade.
        private static void Shatterpoint(Rect card,string id,float s,float a)
        {
            float W=card.width,H=card.height;var c=card.center;float g=Mathf.Min(W,H)*.5f;const float Burst0=.5f;
            if(s<Burst0)
            {
                float glow=Ramp(s,0,.45f);Soft(c,g*(.7f+.3f*glow),U.A(Cyan,(.2f+.35f*glow)*a));string glyph=NativeGlaiveChips.Glyph(id);
                if(glyph!=null)NativeIcons.DrawMarkup(new Rect(c.x-g/2,c.y-g/2,g,g),"glaive-face:"+id,glyph,NativeGlaiveChips.GlyphBox,U.A(Color.Lerp(Ice,Color.white,glow),Ramp(s,0,.12f)*a),Mathf.CeilToInt(g));
                // The cracks: jagged lines out of the middle, lit from within.
                float grow=Out(Ramp(s,.18f,.48f));for(int i=0;i<7;i++){float ang=i*Mathf.PI*2/7+S(11,i)*.3f;var p=c;for(int j=0;j<3;j++){float seg=g*.22f*grow;var q=p+Polar(ang+S(12+j,i)*.5f,seg);Streak(p,q,2.6f-.6f*j,U.A(Color.white,.95f*a));Soft(q,5,U.A(Cyan,.5f*grow*a));p=q;}}
            }
            else
            {
                float w=s-Burst0;Soft(c,W*.6f*(1-Ramp(w,0,.35f))+4,U.A(Color.white,(1-Ramp(w,0,.3f))*a));Ring(c,10+W*.75f*Out(w/.7f),5*(1-Ramp(w,0,.7f)),U.A(Cyan,(1-Ramp(w,0,.7f))*a),36);
                Ring(c,6+W*.45f*Out(w/.5f),2.5f*(1-Ramp(w,0,.5f)),U.A(Ice,(1-Ramp(w,0,.5f))*a),28);Burst(c,w,W*.3f,Ice,a,31,18);
                // The shards: slivers spinning out on their own ways, a little gravity, fading.
                for(int i=0;i<18;i++)
                {
                    float k=Ramp(w,0,1.3f+.3f*R(33,i));if(k>=1)continue;var d=Dir(32,i);float sp=.6f+.5f*R(34,i);var p=c+d*(W*.75f*sp*Out(k))+new Vector2(0,H*.35f*k*k);float size=10+16*R(35,i),spin=(300+500*R(36,i))*k*(i%2==0?1:-1)+R(37,i)*360;
                    U.Push(p,Vector2.zero,1,spin);var box=new Rect(p.x-size/2,p.y-size*.22f,size,size*.44f);U.Polygon(box,ShardShape,ShardAt,ShardAlpha,U.A(i%3==0?Color.white:Ice,(1-k)*a));U.Pop();
                    if(k<.6f)Soft(p,size*.5f,U.A(Cyan,.35f*(1-k/.6f)*a));
                }
            }
        }
        // Magnet Return: the ring spins in the middle, field lines curl in from the card's edges toward it and motes are caught up and drawn in
        // faster and faster; it takes them in with a snap and a ring of light closing on it.
        private static void MagnetReturn(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var c=card.center;float R0=Mathf.Max(W,H)*.72f,rIn=W*.07f;const float Snap=1.55f;
            float shown=Ramp(s,0,.15f)*(1-Ramp(s,2.2f,2.5f));Blade(c,W*.07f*(1+.18f*Bell(s,Snap,Snap+.3f)),s*10,shown*a,Cyan);
            // The field lines: spirals drawn in from the edges, the head bright.
            float fade=1-Ramp(s,1.25f,1.6f);
            for(int i=0;i<10;i++)
            {
                float h=Ramp(s,.08f+.045f*i,.95f+.045f*i);if(h<=0||fade<=0)continue;float a0=i*Mathf.PI*2/10+.3f;Vector2 F(float k){float rr=Mathf.Lerp(R0,rIn,k);return c+Polar(a0+1.5f*k*k,rr*(i%2==0?1:.9f));}
                var prev=F(0);const int Seg=14;for(int j=1;j<=Seg;j++){float k=h*j/Seg;var q=F(k);Streak(prev,q,1.4f+1.6f*k,U.A(Ice,(.25f+.45f*k)*fade*a));prev=q;}Soft(prev,4.5f,U.A(Color.white,.8f*fade*a));
            }
            // The motes, drawn in and swallowed.
            for(int i=0;i<24;i++)
            {
                float d0=.05f*(i%12)+.25f*R(40,i),k=Ramp(s,d0,d0+.95f);if(k<=0||k>=1)continue;float rr=Mathf.Lerp(R0*(.85f+.3f*R(41,i)),rIn,k*k*k);float ang=R(42,i)*Mathf.PI*2+2.2f*k*k;var p=c+Polar(ang,rr);
                Soft(p,3.4f-1.6f*k,U.A(i%3==0?Color.white:Ice,(.4f+.6f*k)*a));if(k>.93f)Soft(c,W*.04f,U.A(Color.white,.5f*a));
            }
            // The ring of light closing on it, then the snap.
            if(s>=Snap-.3f&&s<Snap){float k=Ramp(s,Snap-.3f,Snap);Ring(c,W*.5f*(1-k)+rIn,2.5f,U.A(Cyan,(.3f+.6f*k)*a),32);}
            if(s>=Snap){float w=s-Snap;Soft(c,W*.22f*(1-Ramp(w,0,.4f))+3,U.A(Color.white,.85f*(1-Ramp(w,0,.35f))*a));Burst(c,w,W*.16f,Cyan,a,44,14);Ring(c,rIn+W*.2f*Out(w/.45f),2.5f*(1-Ramp(w,0,.45f)),U.A(Ice,(1-Ramp(w,0,.45f))*a),24);}
        }
        // Quick Catch: the ring spins up on the emblem, is flicked off the card on a boomerang's loop out past the right edge and round, and
        // comes back into the emblem with a flash; speed lines whip off the card's far side.
        private static void QuickCatch(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var home=new Vector2(card.x+W*.74f,card.y+H*.58f);var outDir=new Vector2(1,-.32f).normalized;var side=new Vector2(outDir.y,-outDir.x);float L=W*.85f,Wd=H*.5f;const float Off=.15f,Catch=1;
            Vector2 P(float u)=>home+outDir*(L*Mathf.Sin(Mathf.PI*u))+side*(Wd*Mathf.Sin(2*Mathf.PI*u));float r=W*.05f;
            if(s<Off){float k=Ramp(s,0,Off);Soft(home,W*.12f*k,U.A(Cyan,.5f*k*a));Blade(home,r,s*30*k,a,Cyan);}
            else if(s<Catch)
            {
                float u=InOut(Ramp(s,Off,Catch));var prev=P(u);for(int j=1;j<=18;j++){float ub=u-j*.018f;if(ub<0)break;var q=P(ub);Streak(prev,q,6*(1-j/19f),U.A(Cyan,.6f*(1-j/19f)*a));Streak(prev,q,2*(1-j/19f),U.A(Color.white,.6f*(1-j/19f)*a));prev=q;}
                Blade(P(u),r,s*22,a,Cyan);if(s<Off+.12f)Burst(home,s-Off,W*.1f,Ice,a,51,10);
            }
            float w=s-Catch;
            if(w>=0)
            {
                Soft(home,W*.26f*(1-Ramp(w,0,.45f))+3,U.A(Color.white,.9f*(1-Ramp(w,0,.3f))*a));Ring(home,8+W*.24f*Out(w/.5f),3*(1-Ramp(w,0,.5f)),U.A(Cyan,(1-Ramp(w,0,.5f))*a),28);Burst(home,w,W*.14f,Ice,a,52,16);
                Blade(home,r*(1+.4f*Bell(w,0,.2f)),s*22*(1-.85f*Ramp(w,0,.5f)),(1-Ramp(w,.9f,1.3f))*a,Cyan);
                // Speed lines streaming off the left of the card: the step it puts in you.
                for(int i=0;i<10;i++){float k=Ramp(w,.03f*i,.45f+.03f*i);if(k<=0||k>=1)continue;float y=card.y+H*(.12f+.76f*R(53,i));float x=card.x-W*.02f-W*.55f*Out(k);Streak(new Vector2(x,y),new Vector2(x+W*(.18f+.14f*R(54,i))*(1-k),y),2.6f*(1-k)+.6f,U.A(i%2==0?Cyan:Ice,(1-k)*a));}
            }
        }
        // Whirlwind: a vortex of light spins up round the card, arms of light and motes spiralling in tighter and faster, and collapses into a
        // whirl of the ring that cuts all round in a flash.
        private static void Whirlwind(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var c=card.center;const float Cut=1.36f;float spin=1.2f*s+2.6f*s*s,shrink=1-.7f*Ramp(s,.85f,Cut),R0=W*.78f*shrink,rIn=W*.05f;
            float arms=Ramp(s,0,.3f)*(1-Ramp(s,Cut,Cut+.2f));
            if(arms>0)for(int arm=0;arm<5;arm++)
            {
                float a0=arm*Mathf.PI*2/5+spin;Vector2 F(float k){float rr=Mathf.Lerp(R0,rIn,k);return c+new Vector2(Mathf.Cos(a0+3.2f*k)*rr,Mathf.Sin(a0+3.2f*k)*rr*.62f);}
                var prev=F(0);const int Seg=16;for(int j=1;j<=Seg;j++){float k=j/(float)Seg;var q=F(k);Streak(prev,q,1+4.5f*k,U.A(Cyan,(.15f+.55f*k)*arms*a));Streak(prev,q,.8f+1.4f*k,U.A(Ice,(.1f+.5f*k)*arms*a));prev=q;}
            }
            if(s<Cut+.1f)for(int i=0;i<30;i++){float k=Mathf.Repeat(s*(.8f+.6f*R(60,i))+R(61,i),1);float rr=Mathf.Lerp(R0*(.8f+.4f*R(62,i)),rIn,k);float ang=R(63,i)*Mathf.PI*2+spin*1.3f+4*k;Soft(c+new Vector2(Mathf.Cos(ang)*rr,Mathf.Sin(ang)*rr*.62f),2.8f,U.A(i%4==0?Color.white:Ice,.8f*Mathf.Sin(Mathf.PI*k)*arms*a));}
            float w=s-Cut;
            if(w>=-.3f&&w<1.2f)
            {
                float on=Ramp(w,-.3f,0)*(1-Ramp(w,.8f,1.2f));Blade(c,W*.08f,spin*3,on*a,Cyan);
                if(w>=0)
                {
                    // The whirl's cut all round: two crescents sweeping a full turn, the flash, sparks.
                    float k=Out(Ramp(w,0,.28f)),o=(1-Ramp(w,.25f,.6f))*a;float sweep=k*Mathf.PI*2;Arc(c,W*.24f,spin*3,spin*3+sweep,9,U.A(Ice,o),24,true);Arc(c,W*.28f,spin*3+Mathf.PI,spin*3+Mathf.PI+sweep,5,U.A(Cyan,o),24,true);
                    Soft(c,W*.3f*(1-Ramp(w,0,.4f))+3,U.A(Color.white,.8f*(1-Ramp(w,0,.3f))*a));Burst(c,w,W*.25f,Ice,a,64,18);Ring(c,W*.08f+W*.4f*Out(w/.6f),3*(1-Ramp(w,0,.6f)),U.A(Cyan,(1-Ramp(w,0,.6f))*a),32);
                }
            }
        }
        // Long Arc: the ring takes the long way round: a long boomerang loop traced across and beyond the card with its trail of light drawn
        // behind it; the loop closes in a glint, then the trail fades from its start to its end.
        private static void LongArc(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var start=new Vector2(card.x+W*.1f,card.y+H*.86f);var far=new Vector2(card.x+W*1.28f,card.y+H*.22f);var d=far-start;float L=d.magnitude;var dir=d/L;var side=new Vector2(dir.y,-dir.x);float Wd=H*.62f;
            Vector2 P(float u)=>start+dir*(L*Mathf.Sin(Mathf.PI*u))+side*(Wd*Mathf.Sin(2*Mathf.PI*u));const float Close=1.72f;
            float head=InOut(Ramp(s,.05f,Close)),tail=Ramp(s,1.85f,2.45f);
            if(head>tail)
            {
                const int Seg=60;int i0=Mathf.FloorToInt(tail*Seg),i1=Mathf.CeilToInt(head*Seg);var prev=P(Mathf.Max(tail,i0/(float)Seg));
                for(int i=i0+1;i<=i1;i++){float u=Mathf.Min(head,i/(float)Seg);var q=P(u);float near=1-Mathf.Clamp01((head-u)*2.2f);Streak(prev,q,9,U.A(Cyan,(.12f+.12f*near)*a));Streak(prev,q,2.2f+2.6f*near,U.A(Color.Lerp(Cyan,Ice,near),(.5f+.5f*near)*a));prev=q;}
            }
            if(s<Close+.05f)Blade(P(head),W*.048f,s*18,a,Cyan);
            if(s>=.85f&&s<1.3f){float w=Ramp(s,.85f,1.3f);Glint(P(.5f),W*.08f*(1-w)+2,Color.white,(1-w)*a,30);}
            if(s>=Close){float w=s-Close;Soft(start,W*.18f*(1-Ramp(w,0,.4f))+3,U.A(Color.white,.85f*(1-Ramp(w,0,.3f))*a));Ring(start,8+W*.2f*Out(w/.5f),2.6f*(1-Ramp(w,0,.5f)),U.A(Cyan,(1-Ramp(w,0,.5f))*a),26);Burst(start,w,W*.12f,Ice,a,71,14);
                Blade(start,W*.048f*(1+.3f*Bell(w,0,.2f)),s*18*(1-Ramp(w,0,.6f)),(1-Ramp(w,.6f,1))*a,Cyan);}
        }
        // Skimming Stone: water across the foot of the card, rippling; the ring skims in flat from the left and skips three times, each touch a
        // ripple and a splash, and leaves a slick of light glowing on the water behind it.
        private static readonly float[] SkipAt={0,.32f,.72f,1.05f,1.4f},SkipX={-.18f,.18f,.52f,.78f,1.12f},SkipPeak={.0f,.32f,.18f,.08f};
        private static void SkimmingStone(Rect card,float s,float t,float a)
        {
            float W=card.width,H=card.height;float y0=card.y+H*.8f;float water=Ramp(s,0,.25f)*(1-Ramp(s,2.2f,2.6f));
            // The water: a dark band with moving wave lines.
            if(water>0)
            {
                U.Fade(new Rect(card.x-W*.06f,y0-4,W*1.12f,H*.3f),U.A(Water,.45f*water*a),true);
                for(int line=0;line<3;line++){float yy=y0+4+line*H*.07f;Vector2 prev=new Vector2(card.x-W*.06f,yy);for(int i=1;i<=24;i++){float x=card.x-W*.06f+W*1.12f*i/24f;var q=new Vector2(x,yy+2.2f*Mathf.Sin(x*.05f+t*(2.2f+line)+line));Streak(prev,q,1.2f,U.A(Foam,(.28f-.07f*line)*water*a));prev=q;}}
            }
            Vector2 Stone(float x)
            {
                if(x<SkipAt[1]){float u=x/SkipAt[1];return new Vector2(card.x+W*Mathf.Lerp(SkipX[0],SkipX[1],u),Mathf.Lerp(y0-H*.55f,y0,u*u));}
                for(int i=1;i<4;i++)if(x<SkipAt[i+1]){float u=(x-SkipAt[i])/(SkipAt[i+1]-SkipAt[i]);float y=y0-H*SkipPeak[i]*4*u*(1-u)+(i==3?H*.08f*u*u:0);return new Vector2(card.x+W*Mathf.Lerp(SkipX[i],SkipX[i+1],u),y);}
                return new Vector2(card.x+W*SkipX[4],y0+H*.08f);
            }
            // The slick of light laid on the water from the first touch to where the stone is.
            if(s>=SkipAt[1])
            {
                float x1=Mathf.Min(Stone(Mathf.Min(s,SkipAt[4])).x,card.x+W*1.05f),x0=card.x+W*SkipX[1];float o=(1-Ramp(s,1.9f,2.5f))*water;
                if(x1>x0){Streak(new Vector2(x0,y0+1),new Vector2(x1,y0+1),7,U.A(Cyan,.35f*o*a));Streak(new Vector2(x0,y0+1),new Vector2(x1,y0+1),2.2f,U.A(Ice,.8f*o*a));for(int i=0;i<6;i++){float k=Mathf.Repeat(t*.7f+i/6f,1);Soft(new Vector2(Mathf.Lerp(x0,x1,k),y0+1),8,U.A(Ice,.4f*o*Mathf.Sin(Mathf.PI*k)*a));}}
            }
            // Each touch: rings spreading on the water, a splash thrown up.
            for(int i=1;i<=3;i++)
            {
                float w=s-SkipAt[i];if(w<0||w>1.3f)continue;var p=new Vector2(card.x+W*SkipX[i],y0);
                for(int j=0;j<3;j++){float k=Ramp(w,.08f*j,.9f+.08f*j);if(k<=0||k>=1)continue;Ellipse(p,(10+W*.12f*Out(k))*(1-.12f*j),(3+W*.035f*Out(k))*(1-.12f*j),1.6f,U.A(Foam,(1-k)*.8f*water*a),22);}
                for(int j=0;j<9;j++){float k=Ramp(w,0,.55f);if(k>=1)continue;var v=new Vector2(S(80+i,j)*.6f,-.6f-.6f*R(81+i,j));var q=p+new Vector2(v.x*W*.12f*k,v.y*H*.5f*k+H*.6f*k*k);Soft(q,3*(1-k)+1,U.A(j%2==0?Foam:Ice,(1-k)*a));}
                if(w<.2f)Glint(p,W*.05f*(1-w/.2f),Color.white,a,15);
            }
            // The stone itself, flat (the ring seen edge-on), skipping.
            if(s<SkipAt[4]){var p=Stone(s);float fade=1-Ramp(s,1.2f,1.4f);U.Push(p,Vector2.zero,1,-8,.36f);Blade(p,W*.05f,s*20,fade*a,Cyan);U.Pop();}
        }
        // Crescent Guard: a great crescent sweeps once round the card; behind it a dome of hexes flashes up cell by cell from the foot; a hostile
        // shot streaks in, strikes the dome and is cut down in a burst of sparks, the cells round the strike flaring.
        private static void CrescentGuard(Rect card,float s,float a)
        {
            float W=card.width,H=card.height;var c=card.center;float Rc=Mathf.Max(W,H)*.6f;const float Dome=.85f,Hit=1.4f;
            // The sweep: a thick crescent going once round, its faint wake behind.
            if(s<Dome+.25f)
            {
                float k=Out(Ramp(s,0,Dome)),th=-Mathf.PI/2+2*Mathf.PI*k,o=(1-Ramp(s,Dome,Dome+.25f))*a;
                Arc(c,Rc,-Mathf.PI/2,th,2,U.A(Cyan,.18f*o),40);Arc(c,Rc,th-1.5f,th,16,U.A(Cyan,.85f*o),18,true);Arc(c,Rc+5,th-1.4f,th,2.2f,U.A(Color.white,o),18,true);Glint(c+Polar(th,Rc),W*.05f,Color.white,o,th*Mathf.Rad2Deg);
            }
            // The dome: hexes in an ellipse over the card, lit from the foot up, flaring round the strike, then fading.
            var strike=new Vector2(card.x+W*1.02f,card.y+H*.34f);float hr=W*.075f,fade=1-Ramp(s,2.2f,2.7f);
            if(s>=Dome-.05f&&fade>0)
            {
                float dx=hr*1.5f,dy=hr*1.732f;var foot=new Vector2(c.x,card.yMax);
                for(int row=-4;row<=4;row++)for(int col=-6;col<=6;col++)
                {
                    var p=c+new Vector2(col*dx,row*dy+(col%2!=0?dy*.5f:0));float ex=(p.x-c.x)/(W*.62f),ey=(p.y-c.y)/(H*.64f);if(ex*ex+ey*ey>1)continue;
                    float delay=Dome+.35f*Vector2.Distance(p,foot)/(H*1.2f),k=s-delay;if(k<0)continue;float lit=.3f+.7f*Mathf.Exp(-k*5);
                    float near=s>=Hit?Mathf.Exp(-(s-Hit)*4)*Mathf.Clamp01(1-Vector2.Distance(p,strike)/(W*.45f)):0;float o=Mathf.Min(1,lit*.6f+near)*fade*a;
                    Hex(p,hr*.92f,1.4f+1.4f*near,U.A(near>.3f?Color.white:Ice,o));if(k<.25f||near>.1f)Soft(p,hr*.7f,U.A(Cyan,(.25f*(1-k/.25f)+.4f*near)*fade*a));
                }
            }
            // The shot: a red streak coming in, struck down at the dome's edge.
            if(s>=Hit-.25f&&s<Hit){float k=Ramp(s,Hit-.25f,Hit);var from=new Vector2(card.x+W*1.7f,card.y+H*.05f);var p=Vector2.Lerp(from,strike,k);Streak(Vector2.Lerp(from,strike,Mathf.Max(0,k-.35f)),p,4,U.A(Hostile,.85f*a));Soft(p,8,U.A(Hostile,.6f*a));}
            if(s>=Hit)
            {
                float w=s-Hit;Burst(strike,w,W*.16f,Hostile,a,91,16);Burst(strike,w*.9f,W*.12f,Ice,a,92,12);Glint(strike,W*.08f*(1-Ramp(w,0,.35f)),Color.white,(1-Ramp(w,0,.35f))*a,40);
                if(w<.25f){float k=Out(w/.25f);Arc(strike,W*.06f,-2.4f,-2.4f+2.6f*k,6,U.A(Color.white,(1-w/.25f)*a),12,true);}
            }
        }
    }
}
