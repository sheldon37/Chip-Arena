using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The Top Dog's chips on the screen (docs/TOP-DOG.md "The chips"). Face: a diner's card, a red and white check along the top, a mustard
    // zigzag across the foot, bulbs chasing round its edge like the intro's sign, each chip's emblem large behind the text and its own small
    // cartoon loop over it. Reveal: what happens when one is taken, a different gag for each of the ten, over the "INSTALLED" card, the card
    // itself squashed, stretched, knocked and bounced in time with it (Pose). All pure functions of the clock; one soft glow under Reduced.
    public static class NativeDogChipArt
    {
        private static readonly Color Ketchup=U.Hex(0xe8322b),Mustard=U.Hex(0xffcc22),Bun=U.Hex(0xe8a35a),Cream=U.Hex(0xfff4dc),Ink=U.Hex(0x1a1012),
            Steel=U.Hex(0x8f97a3),SteelDark=U.Hex(0x4a5059),Sky=U.Hex(0x70e1ff),Banana=U.Hex(0xffe14a),Peel=U.Hex(0xc9a227),Sausage=U.Hex(0xc8442a),Glove=U.Hex(0xd8231f),Chrome=U.Hex(0xdfe6ee);
        private static float R(int seed,int i){float x=Mathf.Sin(seed*12.9898f+i*78.233f)*43758.5453f;return x-Mathf.Floor(x);}
        private static float S(int seed,int i)=>R(seed,i)*2-1;
        private static float Out(float x){x=Mathf.Clamp01(x);float o=1-x;return 1-o*o*o;}
        private static float Back(float x){x=Mathf.Clamp01(x);const float c=1.9f;float o=x-1;return 1+(c+1)*o*o*o+c*o*o;}
        private static float Ramp(float x,float a,float b)=>Mathf.Clamp01((x-a)/Mathf.Max(1e-4f,b-a));
        private static float Bell(float x,float a,float b){float k=Ramp(x,a,b);return 4*k*(1-k);}
        private static Vector2 Dir(int seed,int i){float a=R(seed,i)*Mathf.PI*2;return new Vector2(Mathf.Cos(a),Mathf.Sin(a));}
        private static void Streak(Vector2 a,Vector2 b,float w,Color c)
        {
            var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=0)return;var m=(a+b)*.5f;U.Push(m,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(m.x-len/2,m.y-w/2,len,w),c);U.Pop();
        }
        private static void Soft(Vector2 p,float r,Color c){if(r>0&&c.a>0)U.Radial(new Rect(p.x-r,p.y-r,2*r,2*r),c);}
        private static void Ring(Vector2 p,float r,float w,Color c,int seg=26)
        {
            if(c.a<=0||r<=0)return;Vector2 last=p+new Vector2(r,0);for(int i=1;i<=seg;i++){float a=i*Mathf.PI*2/seg;var q=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;Streak(last,q,w,c);last=q;}
        }
        private static void Blob(Vector2 p,float w,float h,Color c,float deg=0){if(c.a<=0)return;U.Push(p,Vector2.zero,1,deg);U.Round(new Rect(p.x-w/2,p.y-h/2,w,h),c,Mathf.Min(w,h)/2);U.Pop();}
        // An outlined toon shape: the ink first, a little larger, then the colour.
        private static void Toon(Vector2 p,float w,float h,float radius,Color c,float a,float deg=0,float line=2.4f)
        {
            if(a<=0)return;U.Push(p,Vector2.zero,1,deg);U.Round(new Rect(p.x-w/2-line,p.y-h/2-line,w+2*line,h+2*line),U.A(Ink,a),radius+line);U.Round(new Rect(p.x-w/2,p.y-h/2,w,h),U.A(c,a),radius);
            U.Round(new Rect(p.x-w/2+w*.08f,p.y-h/2+h*.12f,w*.4f,Mathf.Max(1.5f,h*.16f)),U.A(Color.white,.35f*a),radius*.5f);U.Pop();
        }
        // A toon shape lined in cream (for dark things on the dark).
        private static void Lined(Vector2 p,float w,float h,float radius,Color c,float a,float deg,float line=2.2f)
        {if(a<=0)return;U.Push(p,Vector2.zero,1,deg);U.Round(new Rect(p.x-w/2-line,p.y-h/2-line,w+2*line,h+2*line),U.A(Cream,.9f*a),radius+line);U.Round(new Rect(p.x-w/2,p.y-h/2,w,h),U.A(c,a),radius);U.Pop();}
        // A cartoon star: two squares turned on each other, its outline, a white heart.
        private static void ToonStar(Vector2 p,float r,Color c,float a,float spin=0)
        {
            if(a<=0||r<=0)return;for(int k=0;k<2;k++){U.Push(p,Vector2.zero,1,spin+k*45);float o=r*.74f+1.6f;U.Fill(new Rect(p.x-o,p.y-o,2*o,2*o),U.A(Ink,a));U.Pop();}
            for(int k=0;k<2;k++){U.Push(p,Vector2.zero,1,spin+k*45);float o=r*.74f;U.Fill(new Rect(p.x-o,p.y-o,2*o,2*o),U.A(c,a));U.Pop();}Soft(p,r*.45f,U.A(Color.white,.8f*a));
        }
        // A comic word: big, tilted, its ink outline and a pop in.
        private static void Word(string text,Vector2 at,float size,Color c,float age,float life,float deg,float a)
        {
            if(age<0||age>life||a<=0)return;float pop=age<.12f?Back(age/.12f):1,fade=1-Ramp(age,life-.25f,life),o=fade*a;float sc=.35f+.65f*pop;
            U.Push(at,Vector2.zero,sc,deg);var r=new Rect(at.x-260,at.y-size,520,size*2);
            U.Text(r,text,size,U.A(c,o),TextAnchor.MiddleCenter,bold:true,glow:U.A(Ink,.95f*o),shadow:U.A(Ink,.9f*o));U.Pop();
        }
        // Burst lines round a hit.
        private static void Burst(Vector2 p,float age,float size,Color c,float a,int seed=1)
        {
            if(age<0||age>.45f)return;float w=age/.45f;if(age<.08f)Soft(p,size*(.6f+2*age),U.A(Color.white,(1-age/.08f)*a));
            for(int i=0;i<12;i++){float ang=i*Mathf.PI/6+R(seed,i)*.3f;var d=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));float len=size*(.5f+.6f*R(seed,i+20));Streak(p+d*(size*.35f+len*Out(w)*.6f),p+d*(size*.35f+len*Out(w)),3.2f*(1-w)+.8f,U.A(i%2==0?c:Color.white,(1-w)*a));}
        }
        // Circling dazed stars over a point.
        private static void Dazed(Vector2 p,float t,float rx,float a,int n=4){for(int i=0;i<n;i++){float ang=t*4.6f+i*Mathf.PI*2/n;var q=p+new Vector2(Mathf.Cos(ang)*rx,Mathf.Sin(ang)*rx*.3f);ToonStar(q,10f,Mustard,a*(Mathf.Sin(ang)>-.2f?1:.6f),t*200+i*30);}}
        // ---- the props, in UI pixels ----
        private static void Mallet(Vector2 grip,float len,float deg,float head,float a)
        {
            U.Push(grip,Vector2.zero,1,deg);
            Toon(grip+new Vector2(0,-len*.45f),len*.11f,len*.9f,len*.05f,Bun,a);
            var h=grip+new Vector2(0,-len*.92f);Toon(h,head*1.9f,head,head*.35f,Ketchup,a);Toon(h+new Vector2(-head*.95f,0),head*.24f,head*1.06f,head*.1f,Cream,a);Toon(h+new Vector2(head*.95f,0),head*.24f,head*1.06f,head*.1f,Cream,a);
            U.Pop();
        }
        private static void DrawAnvil(Vector2 p,float s,float a)
        {
            Toon(p+new Vector2(0,-s*.3f),s*1.5f,s*.34f,s*.08f,Steel,a);Toon(p+new Vector2(s*.78f,-s*.36f),s*.5f,s*.2f,s*.1f,Steel,a,-8);
            Toon(p+new Vector2(0,s*.02f),s*.5f,s*.4f,s*.05f,SteelDark,a);Toon(p+new Vector2(0,s*.34f),s*1.1f,s*.26f,s*.06f,Steel,a);
        }
        private static void DrawPeel(Vector2 p,float s,float a,float deg=0)
        {
            U.Push(p,Vector2.zero,1,deg);for(int i=0;i<3;i++){float ang=-60+i*60;Toon(p+new Vector2(Mathf.Sin(ang*Mathf.Deg2Rad)*s*.42f,s*.18f),s*.28f,s*.78f,s*.14f,Banana,a,ang*.8f,1.8f);}
            Toon(p+new Vector2(0,-s*.18f),s*.34f,s*.42f,s*.17f,Banana,a,0,1.8f);Blob(p+new Vector2(0,-s*.42f),s*.12f,s*.12f,U.A(Peel,a));U.Pop();
        }
        private static void DrawPie(Vector2 p,float s,float a,float deg=0)
        {
            U.Push(p,Vector2.zero,1,deg);Toon(p+new Vector2(0,s*.12f),s*1.2f,s*.3f,s*.12f,Bun,a);Toon(p+new Vector2(0,-s*.06f),s*1.12f,s*.34f,s*.17f,Cream,a);
            for(int i=0;i<3;i++)Blob(p+new Vector2((i-1)*s*.3f,-s*.2f),s*.2f,s*.16f,U.A(Cream,a));Blob(p+new Vector2(0,-s*.3f),s*.12f,s*.12f,U.A(Ketchup,a));U.Pop();
        }
        private static void DrawGlove(Vector2 p,float s,float a,float deg=0)
        {
            U.Push(p,Vector2.zero,1,deg);Toon(p,s,s*.82f,s*.38f,Glove,a);Toon(p+new Vector2(-s*.12f,-s*.38f),s*.42f,s*.26f,s*.12f,Glove,a,-20);Toon(p+new Vector2(-s*.62f,0),s*.3f,s*.52f,s*.08f,Cream,a);U.Pop();
        }
        private static void Scissor(Vector2 from,Vector2 to,float s,float a)
        {
            var d=to-from;float len=d.magnitude;if(len<1)return;int n=Mathf.Max(2,(int)(len/(s*1.4f)));var side=new Vector2(-d.y,d.x)/len;
            for(int i=0;i<n;i++){var a0=Vector2.Lerp(from,to,i/(float)n);var a1=Vector2.Lerp(from,to,(i+1f)/n);Streak(a0+side*s*.4f,a1-side*s*.4f,s*.22f+2,U.A(Ink,a));Streak(a0-side*s*.4f,a1+side*s*.4f,s*.22f+2,U.A(Ink,a));Streak(a0+side*s*.4f,a1-side*s*.4f,s*.22f,U.A(Chrome,a));Streak(a0-side*s*.4f,a1+side*s*.4f,s*.22f,U.A(Chrome,a));}
        }
        private static void Coil(Vector2 bottom,Vector2 top,float w,float a)
        {
            const int N=7;for(int i=0;i<N;i++){var p0=Vector2.Lerp(bottom,top,i/(float)N);var p1=Vector2.Lerp(bottom,top,(i+.5f)/N);var p2=Vector2.Lerp(bottom,top,(i+1f)/N);
                Streak(p0+new Vector2(-w,0),p1+new Vector2(w,0),5.2f,U.A(Ink,a));Streak(p1+new Vector2(w,0),p2+new Vector2(-w,0),5.2f,U.A(Ink,a));Streak(p0+new Vector2(-w,0),p1+new Vector2(w,0),3,U.A(Chrome,a));Streak(p1+new Vector2(w,0),p2+new Vector2(-w,0),3,U.A(Chrome,a));}
        }
        private static void HotDog(Vector2 p,float s,float a,float deg=0,float wiggle=0)
        {
            U.Push(p,Vector2.zero,1,deg+wiggle);Toon(p+new Vector2(0,s*.1f),s*1.2f,s*.36f,s*.18f,Bun,a);Toon(p+new Vector2(0,-s*.02f),s*1.36f,s*.24f,s*.12f,Sausage,a);
            for(int i=0;i<4;i++)Streak(p+new Vector2(-s*.45f+i*s*.3f,-s*.06f),p+new Vector2(-s*.3f+i*s*.3f,s*.02f),2.2f,U.A(Mustard,a));U.Pop();
        }
        private static void Bottle(Vector2 p,float s,float a,float deg)
        {
            U.Push(p,Vector2.zero,1,deg);Toon(p,s*.5f,s,s*.12f,Ketchup,a);Toon(p+new Vector2(0,-s*.6f),s*.22f,s*.22f,s*.05f,Cream,a);Toon(p+new Vector2(0,-s*.74f),s*.1f,s*.14f,s*.04f,Cream,a);
            Toon(p+new Vector2(0,s*.05f),s*.36f,s*.3f,s*.06f,Cream,a);ToonStar(p+new Vector2(0,s*.05f),s*.1f,Ketchup,a);U.Pop();
        }
        private static void Flame(Vector2 b,float h,float t,float a,int seed)
        {
            float fl=.75f+.25f*Mathf.Sin(t*(9+R(seed,1)*6)+seed);h*=fl;if(h<1)return;
            Blob(b+new Vector2(0,-h*.42f),h*.62f,h*.95f,U.A(Ketchup,.9f*a),S(seed,2)*8);Blob(b+new Vector2(0,-h*.3f),h*.42f,h*.66f,U.A(U.Hex(0xff8a1f),a),S(seed,3)*6);Blob(b+new Vector2(0,-h*.2f),h*.24f,h*.4f,U.A(Mustard,a));
        }
        private static void Puff(Vector2 p,float age,float life,float size,Color c,float a,int seed)
        {
            if(age<0||age>life)return;float w=age/life;for(int i=0;i<6;i++){var d=Dir(seed,i);var q=p+d*(size*.5f*Out(w))+new Vector2(0,-size*.5f*w);float r=size*(.35f+.25f*R(seed,i+9))*(1+.6f*w);Soft(q,r*1.2f,U.A(c,.55f*(1-w)*a));}
        }

        // ---- the card ----
        /// <summary>The face, over the body and under the core, the text and the bezel's light.</summary>
        public static void Face(Rect r,float radius,string id,float t,float a,bool plain,bool calm)
        {
            if(plain||a<=0)return;float bw=NativeChipArt.BezelOf(radius),tc=NativeChipArt.TopCut(radius),bc=NativeChipArt.BottomCut(radius);var face=Rect.MinMaxRect(r.x+bw,r.y+tc*.55f,r.xMax-bw,r.yMax-bc*.55f);int index=NativeDogChips.Index(id);
            U.BeginBand(face);
            // The check along the top (a diner's tablecloth), faint, and the mustard zigzag along the foot.
            float cell=9;for(int row=0;row<2;row++)for(float x=face.x;x<face.xMax;x+=cell){int k=(int)((x-face.x)/cell)+row;if(k%2==0)U.Fill(new Rect(x,face.y+row*cell,cell,cell),U.A(Ketchup,.16f*a));else U.Fill(new Rect(x,face.y+row*cell,cell,cell),U.A(Cream,.05f*a));}
            {float y=face.yMax-10;Vector2 last=new Vector2(face.x-4,y);for(float x=face.x;x<face.xMax+8;x+=8){var q=new Vector2(x,y+(((int)((x-face.x)/8))%2==0?-4:4));Streak(last,q,3.2f,U.A(Mustard,.32f*a));last=q;}}
            // The emblem, large behind the text, and its own small scene.
            float g=Mathf.Min(face.width*.56f,face.height*.72f);var gr=new Rect(face.xMax-g*.9f,face.yMax-g*.92f,g,g);string glyph=NativeDogChips.Glyph(id);
            if(glyph!=null)NativeIcons.DrawMarkup(gr,"dog-face:"+id,glyph,NativeDogChips.GlyphBox,U.A(Ketchup,.2f*a),Mathf.CeilToInt(g));
            if(!calm&&index>=0)Scene(gr,index,t,a);
            U.EndBand();
            if(calm)return;
            // The bulbs chasing round the edge, as on the intro's diner sign.
            float per=2*(r.width+r.height);int n=Mathf.Max(18,(int)(per/22));Vector2 At(float d){d=Mathf.Repeat(d,per);float w=r.width-4,h=r.height-4,x0=r.x+2,y0=r.y+2;if(d<w)return new Vector2(x0+d,y0);d-=w;if(d<h)return new Vector2(x0+w,y0+d);d-=h;if(d<w)return new Vector2(x0+w-d,y0+h);d-=w;return new Vector2(x0,y0+h-Mathf.Min(h,d));}
            int lead=(int)(t*7);for(int i=0;i<n;i++){var p=At(per*i/n);bool on=(i+lead)%3==0;Soft(p,on?7:3,U.A(on?Mustard:U.Hex(0x6a4a20),(on?.75f:.5f)*a));if(on)Soft(p,2.4f,U.A(Color.white,a));}
        }
        // Each chip's own small loop on the card.
        private static void Scene(Rect g,int index,float t,float a)
        {
            Vector2 C(float x,float y)=>new Vector2(g.x+g.width*x,g.y+g.height*y);float u=g.width/100;
            switch(index)
            {
                // The mallet thumps and the head swells a little each time.
                case 0:{float k=Mathf.Repeat(t/1.6f,1);float hit=k>.42f&&k<.6f?1-(k-.42f)/.18f:0;if(hit>0){Ring(C(.5f,.92f),(10+30*(1-hit))*u,1.4f,U.A(Mustard,hit*.8f*a),16);ToonStar(C(.3f,.78f),4*u*hit,Mustard,hit*a,t*300);ToonStar(C(.72f,.8f),3*u*hit,Mustard,hit*a,-t*300);}break;}
                // A note or two drifting off the piano.
                case 1:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t/2.4f+i*.33f,1);var p=C(.7f+.18f*Mathf.Sin(k*6+i),.5f-.5f*k);U.Text(new Rect(p.x-10,p.y-10,20,20),i%2==0?"♪":"♫",13*u/1.4f+8,U.A(Cream,(1-k)*.8f*a),TextAnchor.MiddleCenter,bold:true);}break;}
                // A shadow grows under the middle anvil, then a clang ring.
                case 2:{float k=Mathf.Repeat(t/2.2f,1);if(k<.7f)Blob(C(.5f,.95f),(10+36*k/.7f)*u,(3+6*k/.7f)*u,U.A(Ink,.45f*a));else{float w=(k-.7f)/.3f;Ring(C(.5f,.8f),(10+34*w)*u,1.4f,U.A(Chrome,(1-w)*.8f*a),18);}break;}
                // A little peel spinning across.
                case 3:{float k=Mathf.Repeat(t/2.6f,1);if(k<.6f){var p=C(-.05f+1.1f*k/.6f,.88f-.1f*Mathf.Sin(k/.6f*Mathf.PI));DrawPeel(p,10*u,.8f*a,k*900);}break;}
                // Flames licking up the bottle.
                case 4:{for(int i=0;i<4;i++)Flame(C(.32f+.12f*i,.94f),(10+6*Mathf.Sin(t*3+i))*u,t,.55f*a,i+3);break;}
                // Steam off the pies.
                case 5:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t*.6f+i*.33f,1);Soft(C(.3f+.22f*i,.5f-.4f*k),(3+5*k)*u,U.A(Cream,.35f*(1-k)*a));}break;}
                // The glove pumping out and back, a POW star at the end.
                case 6:{float k=Mathf.Repeat(t/1.4f,1),out1=k<.3f?Out(k/.3f):1-Ramp(k,.5f,.9f);if(k>.3f&&k<.5f)ToonStar(C(.94f,.4f),6*u*(1-(k-.3f)/.2f),Mustard,a,t*400);break;}
                // A tiny hot dog boinging off the springs.
                case 7:{float k=Mathf.Repeat(t/1.2f,1),h=4*k*(1-k);bool second=Mathf.Repeat(t/2.4f,1)>=.5f;HotDog(C(second?.72f:.28f,(second?.12f:.38f)-.3f*h),9*u,.85f*a,0,Mathf.Sin(t*14)*10);break;}
                // Steam off the plate, and one hot dog wiggling on top.
                case 8:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t*.5f+i*.33f,1);Soft(C(.35f+.15f*i,.25f-.25f*k),(3+5*k)*u,U.A(Cream,.3f*(1-k)*a));}break;}
                // The pancake breathes up and down; stars over it.
                default:{for(int i=0;i<3;i++){float ang=t*3+i*2.1f;ToonStar(C(.5f+.3f*Mathf.Cos(ang),.2f+.06f*Mathf.Sin(ang)),3.2f*u,Mustard,.8f*a,t*200);}break;}
            }
        }

        // ---- the moment it goes in: the spotlight's card ----
        /// <summary>When the gag starts after the card shows (s).</summary>
        public const float Start=.55f;
        /// <summary>How the card itself moves with its gag at reveal time `t`: knocked (px), scaled across and up about its foot, turned (degrees).</summary>
        public static void Pose(string id,float t,bool calm,out Vector2 offset,out float sx,out float sy,out float degrees)
        {
            offset=Vector2.zero;sx=sy=1;degrees=0;if(calm)return;float s=t-Start;if(s<0)return;float shake=0;
            switch(NativeDogChips.Index(id))
            {
                // Bigger Mallet: flattened by the bonk, then springing back tall and settling.
                case 0:{const float Hit=.42f;if(s>=Hit){float w=s-Hit;sy=w<.08f?1-.55f*w/.08f:1-.55f*Mathf.Exp(-(w-.08f)*6)*Mathf.Cos((w-.08f)*22);sx=1/Mathf.Sqrt(Mathf.Max(.3f,sy));shake=Pulse(s,Hit,.25f,10);}break;}
                // Hammerspace: the piano lands on it, it crumples a bit, then pops up.
                case 1:{const float Land=1.55f;if(s>=Land){float w=s-Land;sy=1-.45f*Mathf.Exp(-w*4.5f)*Mathf.Cos(w*16);sx=1+.2f*Mathf.Exp(-w*4.5f);shake=Pulse(s,Land,.4f,14);}break;}
                // Anvil Shower: knocked down three times, a little more each time.
                case 2:{for(int i=0;i<3;i++){float at=.45f+.3f*i;if(s>=at){float w=s-at;float k=.18f+.06f*i;sy-=k*Mathf.Exp(-w*9)*Mathf.Cos(w*20);}}sx=1/Mathf.Sqrt(Mathf.Max(.4f,sy));for(int i=0;i<3;i++)shake+=Pulse(s,.45f+.3f*i,.2f,6+3*i);break;}
                // Banana Bonanza: it slips: whips sideways, turns over nearly flat, scrambles up.
                case 3:{float slip=Ramp(s,.35f,.6f),up=Ramp(s,1.05f,1.5f);degrees=(-80*Out(slip))*(1-Back(up));offset=new Vector2(140*Out(slip)*(1-Out(up)),18*Bell(s,.35f,.9f));shake=Pulse(s,.62f,.25f,7);break;}
                // Hot Sauce: it shivers with the heat and glows.
                case 4:{float heat=Ramp(s,.3f,.7f)*(1-Ramp(s,2,2.5f));shake=3.5f*heat;sy=1+.03f*heat*Mathf.Sin(t*30);break;}
                // Pie Volley: a pie in its face each time: a jolt back.
                case 5:{for(int i=0;i<3;i++){float at=.3f+.22f*i;shake+=Pulse(s,at,.18f,5);if(s>=at){float w=s-at;sy-=.06f*Mathf.Exp(-w*10)*Mathf.Cos(w*25);}}break;}
                // Knockout Glove: punched off to the right, tilted, wobbling back.
                case 6:{const float Hit=.4f;if(s>=Hit){float w=s-Hit;float away=Mathf.Exp(-w*3.2f)*Mathf.Cos(w*7);offset=new Vector2(220*away,-20*away);degrees=18*away;sx=1-.1f*Mathf.Exp(-w*8);}shake=Pulse(s,Hit,.3f,12);break;}
                // Double Boing: launched up on the spring, down, boing, boing.
                case 7:{float h=0;if(s>=.25f){float w=s-.25f;h=w<.7f?280*4*(w/.7f)*(1-w/.7f):w<1.05f?90*4*((w-.7f)/.35f)*(1-(w-.7f)/.35f):w<1.25f?30*4*((w-1.05f)/.2f)*(1-(w-1.05f)/.2f):0;
                    float land(float at)=>s>=at&&s<at+.12f?1-(s-at)/.12f:0;float sq=Mathf.Max(land(.95f),Mathf.Max(land(1.3f)*.7f,land(1.5f)*.4f));sy=1-.3f*sq+.12f*Ramp(s,.25f,.35f)*(1-Ramp(s,.35f,.6f));sx=1/Mathf.Sqrt(Mathf.Max(.5f,sy));}
                    else{sy=1-.18f*Ramp(s,0,.25f);sx=1+.08f*Ramp(s,0,.25f);}offset=new Vector2(0,-h);break;}
                // Second Helpings: a happy hop when the cloche lifts.
                case 8:{offset=new Vector2(0,-30*Bell(s,.95f,1.35f));float land=s>=1.35f&&s<1.5f?1-(s-1.35f)/.15f:0;sy=1-.15f*land;sx=1+.08f*land;break;}
                // Toon Logic: flattened by the anvil, pumped back up, popping round.
                default:
                {
                    const float Flat=.42f,Pump=.95f,Popped=1.85f;if(s>=Flat&&s<Popped){float w=s-Flat;float flat=Mathf.Min(1,w/.06f);float pumped=Ramp(s,Pump,Popped);float strokes=pumped*(1+.08f*Mathf.Sin((s-Pump)*Mathf.PI*2*3.3f));sy=Mathf.Lerp(1,.12f,flat)+.88f*strokes*.92f;sx=1+.6f*flat*(1-pumped);shake=Pulse(s,Flat,.25f,10);}
                    else if(s>=Popped){float w=s-Popped;sy=1+.25f*Mathf.Exp(-w*5)*Mathf.Cos(w*18);sx=1/Mathf.Sqrt(Mathf.Max(.6f,sy));}
                    break;
                }
            }
            offset+=new Vector2(Mathf.Sin(t*71)*shake,Mathf.Sin(t*83+1)*shake);
        }
        private static float Pulse(float s,float at,float life,float size)=>s<at||s>at+life?0:size*(1-(s-at)/life);
        /// <summary>The chip's gag over the spotlight's `card` at reveal time `t` (s), at the block's `env` alpha.</summary>
        public static void Reveal(Rect card,string id,float t,float env,bool calm)
        {
            float s=t-Start;if(env<=0||s<0)return;int index=NativeDogChips.Index(id);if(index<0)return;float a=env;var c=card.center;float W=card.width,H=card.height;Vector2 At(float x,float y)=>new Vector2(card.x+W*x,card.y+H*y);
            if(calm){float w=Mathf.Clamp01(s/.5f);Soft(c,W*.7f,U.A(Ketchup,.22f*(1-w)*a));return;}
            switch(index)
            {
                // Bigger Mallet: a mallet as big as the card swings over and BONKS it flat; stars; it lifts away bigger still.
                case 0:
                {
                    // Raised behind, over the top and down on the card's head; a little bounce off it, then up and away, bigger.
                    const float Hit=.42f;var grip=At(1.14f,1.08f);var d=At(.46f,.1f)-grip;float hitAngle=Mathf.Atan2(d.x,-d.y)*Mathf.Rad2Deg,len=d.magnitude/.92f;
                    float k=Ramp(s,0,Hit),swing=s<Hit?Mathf.Lerp(hitAngle+105,hitAngle,k*k*k):s<Hit+.5f?hitAngle+9*Bell(s,Hit,Hit+.22f):Mathf.Lerp(hitAngle,hitAngle+62,Out(Ramp(s,Hit+.5f,Hit+.95f)));
                    float grow=1+.35f*Out(Ramp(s,Hit+.5f,Hit+.95f)),fade=1-Ramp(s,Hit+.75f,Hit+1.05f);Mallet(grip+new Vector2(0,-H*.5f*Out(Ramp(s,Hit+.6f,Hit+1.05f))),len*grow,swing,H*.42f*grow,fade*a);
                    if(s<Hit&&k>.45f)for(int i=0;i<4;i++){float back=swing+8+7*i;var dir=new Vector2(Mathf.Sin(back*Mathf.Deg2Rad),-Mathf.Cos(back*Mathf.Deg2Rad));Streak(grip+dir*len*.7f,grip+dir*len*1.02f,3,U.A(Color.white,(.5f-.1f*i)*a));}
                    Burst(At(.5f,0),s-Hit,W*.5f,Mustard,a,3);Word("BONK!",At(.25f,-.25f),44,Mustard,s-Hit,1.3f,-10,a);
                    if(s>Hit+.1f&&s<Hit+2)Dazed(At(.5f,-.08f),t,W*.32f,(1-Ramp(s,Hit+1.6f,Hit+2))*a,5);
                    break;
                }
                // Hammerspace: a hole opens beside it; a gloved hand pulls a mallet, an anvil, then a grand piano out of nothing, and drops it on the card.
                case 1:
                {
                    var hole=At(-.32f,.5f);float open=Out(Ramp(s,0,.2f))*(1-Ramp(s,2.2f,2.5f));if(open>0){Blob(hole,W*.34f*open,H*.5f*open,U.A(Ink,a));Ring(hole,W*.17f*open,2,U.A(Sky,.6f*open*a),20);}
                    // Each thing pulled out and tossed aside, bigger each time.
                    for(int i=0;i<2;i++){float at=.2f+.35f*i,k=Ramp(s,at,at+.35f);if(k<=0||k>=1)continue;var p=Vector2.Lerp(hole,hole+new Vector2(-W*.3f,-H*.9f),Out(k))+new Vector2(0,H*1.4f*k*k);if(i==0)Mallet(p,H*.5f,k*540,H*.14f,(1-k)*a);else DrawAnvil(p,W*.16f,(1-k)*a);}
                    // The hand.
                    {float reach=Bell(s,.15f,.55f)+Bell(s,.5f,.9f)+Bell(s,.85f,1.25f);var hand=hole+new Vector2(W*.13f*reach,-H*.05f);float g=W*.05f;Toon(hand+new Vector2(-g*1.1f,0),g*.9f,g*1.1f,g*.3f,Cream,open*a);Toon(hand,g*1.6f,g*1.3f,g*.6f,Color.white,open*a);for(int i=0;i<3;i++)Toon(hand+new Vector2(g*.85f,-g*.45f+i*g*.42f),g*.7f,g*.34f,g*.17f,Color.white,open*a);Toon(hand+new Vector2(-g*.1f,-g*.75f),g*.36f,g*.6f,g*.18f,Color.white,open*a,-25);}
                    // The piano: up out of the hole, over the card, and down.
                    const float Up=.95f,Land=1.55f;if(s>=Up&&s<Land+.05f){float k=Ramp(s,Up,Land);var p=new Vector2(Mathf.Lerp(hole.x,c.x,Out(k)),Mathf.Lerp(hole.y,card.y-H*1.1f,Out(k*1.6f))+H*1.2f*Ramp(k,.55f,1)*Ramp(k,.55f,1));Piano(p,W*.78f*(.5f+.5f*Out(k*2)),a,k*40-20);}
                    else if(s>=Land&&s<Land+.35f){var p=At(.5f,.06f)+new Vector2(0,H*.08f*Ramp(s,Land,Land+.12f));Piano(p,W*.78f,(1-Ramp(s,Land+.15f,Land+.35f))*a,4);}
                    else if(s>=Land)
                    {
                        float w=s-Land;Burst(At(.5f,0),w,W*.6f,Cream,a,7);Puff(At(.5f,.05f),w,.9f,W*.5f,U.Hex(0xc8bca8),a,8);
                        for(int i=0;i<14;i++){float k=Ramp(w,0,1.1f);if(k>=1)continue;var d=new Vector2(S(30,i),-.6f-.6f*R(31,i));var q=At(.5f,0)+new Vector2(d.x*W*.8f*k,d.y*H*1.2f*k+H*1.6f*k*k);U.Push(q,Vector2.zero,1,k*(300+200*R(32,i)));bool black=i%3==0;U.Fill(new Rect(q.x-4,q.y-(black?8:12),8,black?16:24),U.A(Ink,(1-k)*a));if(!black)U.Fill(new Rect(q.x-3,q.y-11,6,22),U.A(Cream,(1-k)*a));U.Pop();}
                        for(int i=0;i<5;i++){float k=Ramp(w,.1f*i,.1f*i+1.2f);if(k<=0||k>=1)continue;var q=At(.2f+.15f*i,-.1f-.5f*k)+new Vector2(Mathf.Sin(k*8+i)*12,0);U.Text(new Rect(q.x-15,q.y-15,30,30),i%2==0?"♪":"♫",26,U.A(Cream,(1-k)*a),TextAnchor.MiddleCenter,bold:true,shadow:U.A(Ink,(1-k)*a));}
                        Word("PLUNK!",At(.72f,-.3f),46,Cream,w,1.4f,8,a);
                    }
                    break;
                }
                // Anvil Shower: three shadows grow on the card, three anvils fall one after another, CLANG, CLANG, CLANG.
                case 2:
                {
                    for(int i=0;i<3;i++)
                    {
                        float land=.45f+.3f*i,x=i==0?.5f:i==1?.18f:.82f;var at=At(x,.02f);float sz=W*(i==0?.3f:.22f);
                        if(s<land){float k=Ramp(s,land-.45f,land);Blob(at+new Vector2(0,H*.02f),sz*1.3f*k,sz*.24f*k,U.A(Ink,.5f*k*a));Ring(at,sz*.7f*(1.4f-.4f*k),2,U.A(Ketchup,.8f*k*a),18);DrawAnvil(at+new Vector2(0,-H*1.4f*(1-k*k)-sz*.4f),sz,a);}
                        else{float w=s-land;var rest=at+new Vector2(0,-sz*.38f);DrawAnvil(rest+new Vector2(0,-6*Bell(w,0,.2f)),sz,(1-Ramp(w,1.4f,1.7f))*a);Burst(at,w,sz*1.3f,Chrome,a,40+i);Word("CLANG!",at+new Vector2(0,-sz*1.2f),30+6*i,Chrome,w,.9f,(i-1)*10,a);
                            if(w<.4f)for(int j=0;j<8;j++){var d=Dir(50+i,j);d.y=-Mathf.Abs(d.y);var q=at+d*(sz*1.6f*Out(w/.4f));Streak(q,q+d*6,2,U.A(Mustard,(1-w/.4f)*a));}}
                    }
                    if(s>1.2f&&s<2.6f)Dazed(At(.5f,-.2f),t,W*.3f,(1-Ramp(s,2.2f,2.6f))*a,4);
                    break;
                }
                // Banana Bonanza: peels rain down under the card, it steps on one and goes over: WHOOPS!, peels flying.
                case 3:
                {
                    for(int i=0;i<9;i++){float at=.04f*i,k=Ramp(s,at,at+.3f);if(k<=0)continue;var end=At(-.2f+1.4f*i/8f,1.08f);var p=Vector2.Lerp(end+new Vector2(S(60,i)*40,-H*1.4f),end,k*k);DrawPeel(p,W*.09f,(1-Ramp(s,2.2f,2.6f))*a,k<1?k*720:S(61,i)*20);}
                    float slip=s-.35f;if(slip>=0&&slip<1.2f){var under=At(.5f,1.08f);for(int i=0;i<5;i++){var q=under+new Vector2(-30*i*Out(slip/.3f),0);Streak(q,q+new Vector2(-22,0),3,U.A(Cream,(1-slip/1.2f)*.8f*a));}}
                    for(int i=0;i<4;i++){float k=Ramp(s,.4f+.05f*i,1.3f+.05f*i);if(k<=0||k>=1)continue;var q=At(.5f,1)+new Vector2((i-1.5f)*60*k,-H*1.2f*Bell(k,0,1));DrawPeel(q,W*.08f,(1-k*k)*a,k*1000+i*80);}
                    Word("WHOOPS!",At(.3f,-.2f),44,Banana,s-.45f,1.4f,-14,a);
                    break;
                }
                // Hot Sauce: a bottle swings in and squirts a red-hot stream along the card; flames burst up under it; steam whistles off it; HOT HOT HOT!
                case 4:
                {
                    var bottle=At(1.18f,-.12f);float inn=Out(Ramp(s,0,.25f))*(1-Ramp(s,1.6f,1.9f));Bottle(bottle+new Vector2(120*(1-inn),-40*(1-inn)),W*.22f,inn*a,-120+40*Bell(s,.25f,1.4f));
                    if(s>.25f&&s<1.5f){var nozzle=bottle+new Vector2(-W*.13f,W*.1f);float flow=Ramp(s,.25f,.45f)*(1-Ramp(s,1.3f,1.5f));int n=Mathf.Max(2,(int)(26*flow));for(int i=0;i<n;i++){float u=i/25f,wob=Mathf.Sin(s*30+i*.9f)*3;var q=Vector2.Lerp(nozzle,At(-.05f,.62f),u)+new Vector2(0,-H*.35f*Mathf.Sin(u*Mathf.PI)+wob);float r=18-7*u;Blob(q,r+2.5f,r+2.5f,U.A(Ink,.8f*a));Blob(q,r,r,U.A(i%4==0?U.Hex(0xff6a1f):Ketchup,a));if(i%3==0)Blob(q+new Vector2(-r*.2f,-r*.2f),r*.35f,r*.25f,U.A(Color.white,.6f*a));}
                        for(int i=0;i<6;i++){float k=Mathf.Repeat(s*2.2f+i*.17f,1);var q=At(.1f+.15f*i,.62f)+new Vector2(0,H*.5f*k*k);Blob(q,7,10,U.A(Ketchup,(1-k)*flow*a));}}
                    float heat=Ramp(s,.3f,.7f)*(1-Ramp(s,2,2.5f));for(int i=0;i<16;i++)Flame(At(-.08f+1.16f*i/15f,1.04f),H*(.3f+.35f*R(70,i))*heat,t+i,a,70+i);
                    U.Fade(new Rect(card.x,card.y+H*.4f,W,H*.6f),U.A(Ketchup,.35f*heat*a),false);
                    for(int i=0;i<4;i++){float k=Mathf.Repeat(s*1.4f+i*.25f,1);var p=At(.15f+.23f*i,-.05f-.6f*k);Soft(p,10+22*k,U.A(Cream,.55f*(1-k)*heat*a));}
                    Word("HOT HOT HOT!",At(.5f,-.32f),40,U.Hex(0xff6a1f),s-.55f,1.6f,-6,a);
                    break;
                }
                // Pie Volley: three pies fly in and SPLAT across the screen in front of the card, cream sliding down, then a squeegee wipes it clean.
                case 5:
                {
                    Vector2[] at={At(.2f,.3f),At(.78f,.55f),At(.45f,.85f)};Vector2[] from={At(-1.3f,-.6f),At(2.3f,-.2f),At(.4f,2.5f)};float wipe=Ramp(s,1.5f,1.95f);
                    for(int i=0;i<3;i++)
                    {
                        float hit=.3f+.22f*i;if(s<hit){float k=Ramp(s,hit-.3f,hit);var p=Vector2.Lerp(from[i],at[i],k)+new Vector2(0,-60*4*k*(1-k));DrawPie(p,W*.22f*(.6f+.4f*k),a,k*360);}
                        else
                        {
                            float w=s-hit;float wiped=Mathf.Clamp01((wipe*(W*1.4f)-(at[i].x-card.x+W*.2f))/(W*.4f));float o=(1-wiped)*a;var p=at[i];float sz=W*.36f;
                            // A rim under the cream (its shadow on the card), then the splat and its lobes, a little shading on the lower side.
                            var rim=U.Hex(0xc9b48a);Blob(p+new Vector2(2,3),sz*1.26f,sz*1.06f,U.A(rim,o));for(int j=0;j<9;j++){var d=Dir(80+i,j);Blob(p+d*sz*.55f+new Vector2(2,3),sz*.36f,sz*.36f,U.A(rim,o));}
                            Blob(p,sz*1.2f,sz,U.A(Cream,o));for(int j=0;j<9;j++){var d=Dir(80+i,j);Blob(p+d*sz*.55f,sz*.3f,sz*.3f,U.A(Cream,o));}Blob(p+new Vector2(0,sz*.22f),sz*.9f,sz*.34f,U.A(U.Hex(0xefe0bd),.8f*o));
                            for(int j=0;j<4;j++){float drip=Out(Ramp(w,.15f,1.5f))*(30+40*R(90+i,j));var q=p+new Vector2((j-1.5f)*sz*.28f,sz*.35f);Blob(q+new Vector2(2,drip*.5f+3),11,drip+12,U.A(rim,o));Blob(q+new Vector2(0,drip*.5f),9,drip+10,U.A(Cream,o));Blob(q+new Vector2(0,drip+4),12,12,U.A(Cream,o));}
                            Blob(p+new Vector2(-sz*.15f,-sz*.1f),sz*.3f,sz*.12f,U.A(Color.white,.7f*o));Blob(p+new Vector2(sz*.15f,0),sz*.1f,sz*.1f,U.A(Ketchup,o));
                            Word("SPLAT!",p+new Vector2(0,-sz*.7f),30,Cream,w,.8f,(i-1)*14,a);
                        }
                    }
                    if(wipe>0&&wipe<1){float x=card.x-W*.2f+W*1.4f*wipe;Toon(new Vector2(x,c.y),14,H*1.5f,4,Sky,a);Toon(new Vector2(x+18,c.y),24,10,3,SteelDark,a);}
                    break;
                }
                // Knockout Glove: a scissor arm springs out of the screen's left, the glove POWs the card off to the right; stars round it as it wobbles back.
                case 6:
                {
                    const float Hit=.4f;var origin=new Vector2(card.x-W*.9f,c.y+H*.1f);float reach=s<Hit?Back(Ramp(s,.05f,Hit)):1-Out(Ramp(s,Hit+.35f,Hit+.75f));
                    var glove=Vector2.Lerp(origin,At(.02f,.45f),reach);if(reach>.02f){Scissor(origin,glove-new Vector2(W*.08f,0),W*.07f,a);DrawGlove(glove,W*.2f,a,0);}
                    Burst(At(.05f,.45f),s-Hit,W*.55f,Mustard,a,9);Word("POW!",At(.15f,-.15f),52,Mustard,s-Hit,1.3f,-12,a);
                    if(s>Hit+.1f&&s<Hit+2.2f)Dazed(At(.5f+.0f,-.1f),t,W*.3f,(1-Ramp(s,Hit+1.8f,Hit+2.2f))*a,5);
                    break;
                }
                // Double Boing: a giant spring pops out under the card and throws it; it lands, boings, boings again.
                case 7:
                {
                    var floor=At(.5f,1.04f);float out1=s<.25f?Ramp(s,0,.25f):1-Ramp(s,.5f,.7f),out2=Bell(s,.88f,1.1f)+Bell(s,1.25f,1.42f)*.7f;float h=Mathf.Max(H*.55f*out1,H*.3f*out2);
                    if(h>2){Coil(floor+new Vector2(0,10),floor+new Vector2(0,10-h),W*.16f,a);Toon(floor+new Vector2(0,10-h),W*.4f,8,3,Steel,a);}
                    Word("BOING!",At(.18f,.2f),46,Sky,s-.25f,1,-12,a);Word("BOING!",At(.82f,.5f),34,Sky,s-.95f,.8f,10,a);Word("boing",At(.3f,.8f),22,Sky,s-1.3f,.6f,-6,a);
                    for(int i=0;i<3;i++){float at=i==0?.95f:i==1?1.3f:1.5f;Puff(floor,s-at,.6f,W*(.3f-.07f*i),U.Hex(0xc8bca8),a,100+i);}
                    break;
                }
                // Second Helpings: a dinner bell, a silver cloche on a plate under the card, DING!, the cloche lifts in a puff of steam on a heap
                // of hot dogs, and they shower up over the card. SECONDS!
                case 8:
                {
                    var plate=At(.5f,1.42f);float dw=W*.56f,dh=dw*.5f,down=Out(Ramp(s,0,.35f)),lift=Ramp(s,.95f,1.3f),inn=Out(Ramp(s,0,.25f));
                    Toon(plate+new Vector2(0,4),dw*1.25f,10,5,Chrome,inn*a);Toon(plate+new Vector2(0,10),dw*.7f,6,3,Steel,inn*a);
                    // The heap under it, wiggling, once it is off.
                    if(lift>0)for(int i=0;i<6;i++){float row=i<3?0:i<5?1:2;var q=plate+new Vector2((i<3?(i-1)*dw*.32f:i<5?(i-3.5f)*dw*.32f:0),-10-row*dw*.13f);HotDog(q,dw*.36f,a*(1-Ramp(s,1.25f+.04f*i,1.35f+.04f*i)),(i%2==0?-8:8),Mathf.Sin(t*18+i)*8);}
                    if(lift<1)
                    {
                        var at=plate+new Vector2(0,-H*1.5f*(1-down)-H*1.3f*lift*lift);float o=(1-lift)*inn*a;
                        // The dome: thin slices of a half ellipse, lined, a shine down one side, the knob.
                        const int N=22;for(int pass=0;pass<2;pass++)for(int i=0;i<N;i++){float y0=i/(float)N,y1=(i+1f)/N,wd=dw*Mathf.Sqrt(Mathf.Max(0,1-y1*y1))+(pass==0?5:0);var r=new Rect(at.x-wd/2,at.y-dh*y1-(pass==0?2.5f:0),wd,dh*(y1-y0)+1.2f+(pass==0?2.5f:0));U.Fill(r,U.A(pass==0?Ink:Color.Lerp(Chrome,Steel,y0*.6f),o));}
                        Streak(at+new Vector2(-dw*.28f,-dh*.25f),at+new Vector2(-dw*.12f,-dh*.8f),5,U.A(Color.white,.6f*o));Toon(at+new Vector2(0,-dh-5),dw*.11f,dw*.07f,dw*.035f,Chrome,o);Toon(at,dw*1.04f,7,3,Steel,o);
                    }
                    Word("DING!",At(.88f,.08f),36,Mustard,s-.35f,.9f,12,a);if(s>.35f&&s<.75f)Ring(At(.88f,.08f),(s-.35f)*260,2.2f,U.A(Mustard,(1-(s-.35f)/.4f)*a),22);
                    Puff(plate+new Vector2(0,-dh*.6f),s-.95f,1.2f,dw*.9f,Cream,.7f*a,110);
                    for(int i=0;i<16;i++){float at2=1.05f+.04f*i,k=Ramp(s,at2,at2+.95f);if(k<=0||k>=1)continue;var d=new Vector2(S(120,i)*1.1f,-.9f-.7f*R(121,i));var q=plate+new Vector2(0,-dh*.4f)+new Vector2(d.x*W*.75f*k,d.y*H*2.4f*k+H*3.2f*k*k);HotDog(q,W*.13f,(1-Ramp(k,.8f,1))*a,k*(400+300*R(122,i)),Mathf.Sin(t*20+i)*12);}
                    Word("SECONDS!",At(.5f,-.35f),46,Mustard,s-1.05f,1.6f,-5,a);
                    break;
                }
                // Toon Logic: an anvil flattens the card to a pancake; a bicycle pump puffs it back up, three strokes; it pops round with stars and a halo. TA-DA!
                default:
                {
                    const float Flat=.42f,Pump=.95f,Popped=1.85f;
                    if(s<Flat+.6f){float k=Ramp(s,0,Flat);var p=At(.5f,-.2f)+new Vector2(0,-H*1.5f*(1-k*k));if(s>=Flat)p=At(.5f,.75f)+new Vector2(0,-H*.4f*Ramp(s,Flat,Flat+.6f));DrawAnvil(p,W*.32f,(1-Ramp(s,Flat+.3f,Flat+.6f))*a);}
                    Burst(At(.5f,.9f),s-Flat,W*.6f,Chrome,a,13);Word("SPLUT!",At(.5f,.5f),40,Cream,s-Flat,.7f,-4,a);
                    if(s>=Pump-.2f&&s<Popped+.2f){float on=Ramp(s,Pump-.2f,Pump)*(1-Ramp(s,Popped,Popped+.2f));var pump=At(1.2f,.85f);float stroke=.5f+.5f*Mathf.Cos((s-Pump)*Mathf.PI*2*3.3f);Toon(pump,W*.08f,H*.45f,4,Ketchup,on*a);Toon(pump+new Vector2(0,-H*.25f-H*.12f*stroke),W*.04f,H*.25f,2,Chrome,on*a);Toon(pump+new Vector2(0,-H*.38f-H*.12f*stroke),W*.16f,7,3,SteelDark,on*a);
                        Streak(pump+new Vector2(-W*.04f,H*.2f),At(.95f,.95f),3,U.A(Ink,on*a));for(int i=0;i<3;i++){float at=Pump+i/3.3f;Word("PFF",pump+new Vector2(-W*.15f,-H*.2f),18,Cream,s-at,.3f,-10,a);}}
                    if(s>=Popped){float w=s-Popped;Burst(c,w,W*.7f,Mustard,a,17);for(int i=0;i<10;i++){var d=Dir(130,i);ToonStar(c+d*(W*.65f*Out(w/.8f)),9*(1-Ramp(w,.5f,.9f)),Mustard,a,t*300+i*20);}
                        float halo=Ramp(w,.1f,.35f)*(1-Ramp(w,1.6f,2.1f));Ring(At(.5f,-.1f),W*.22f,4,U.A(Mustard,halo*a),24);Soft(At(.5f,-.1f),W*.3f,U.A(Mustard,.2f*halo*a));Word("TA-DA!",At(.5f,-.4f),48,Mustard,w,1.7f,0,a);}
                    break;
                }
            }
        }
        // A grand piano, side on: its body and lid, the keys, the legs.
        private static void Piano(Vector2 p,float s,float a,float deg)
        {
            U.Push(p,Vector2.zero,1,deg);var body=U.Hex(0x3a2440);
            // The lid propped open behind, the case, the keyboard along the front, three legs: all lined in cream so it reads on the dark.
            Lined(p+new Vector2(s*.16f,-s*.26f),s*.74f,s*.07f,s*.03f,body,a,-22);Streak(p+new Vector2(s*.02f,-s*.12f),p+new Vector2(s*.12f,-s*.36f),3,U.A(Bun,a));
            for(int i=0;i<3;i++)Lined(p+new Vector2(-s*.38f+i*s*.36f,s*.27f),s*.06f,s*.26f,s*.02f,body,a,0);
            Lined(p,s,s*.34f,s*.12f,body,a,0);U.Round(new Rect(p.x-s*.42f,p.y-s*.13f,s*.55f,s*.05f),U.A(Color.white,.18f*a),s*.025f);
            U.Fill(new Rect(p.x-s*.5f,p.y-s*.03f,s*.4f,s*.09f),U.A(Cream,a));for(int i=0;i<8;i++)if(i%7!=2)U.Fill(new Rect(p.x-s*.48f+i*s*.05f,p.y-s*.03f,s*.024f,s*.05f),U.A(Ink,a));
            U.Pop();
        }
    }
}
