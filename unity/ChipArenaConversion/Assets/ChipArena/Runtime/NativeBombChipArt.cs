using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The Bombadier's chips on the screen (docs/BOMBADIER.md). Face: the card art, the set's scorched body, hazard tape and a fuse burning round its edge, with each chip's emblem large behind the text and its own
    // small live scene over it. Reveal: what happens when one is taken, a different piece of comedy for each of the ten, over the "INSTALLED" card. All pure functions of the clock; thinned to a glow under Reduced.
    public static class NativeBombChipArt
    {
        private static readonly Color Flame=U.Hex(0xff7a1f),Hot=U.Hex(0xffd27a),Ember=U.Hex(0xff4a10),Tape=U.Hex(0xf2b90f),Soot=U.Hex(0x0b0a09),Led=U.Hex(0xff2015),Stick=U.Hex(0xb3241b),Wrap=U.Hex(0x17181b),Steel=U.Hex(0x8a8f96);
        private static float R(int seed,int i){float x=Mathf.Sin(seed*12.9898f+i*78.233f)*43758.5453f;return x-Mathf.Floor(x);}
        private static float S(int seed,int i)=>R(seed,i)*2-1;
        private static float Out(float x){x=Mathf.Clamp01(x);float o=1-x;return 1-o*o*o;}
        private static float Ramp(float x,float a,float b)=>Mathf.Clamp01((x-a)/Mathf.Max(1e-4f,b-a));
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
        // A stick of TNT in UI pixels: paper, tape, its fuse.
        private static void DrawStick(Vector2 at,float len,float degrees,float alpha,bool lit=true)
        {
            float w=len*.26f;U.Push(at,Vector2.zero,1,degrees);var a=new Vector2(at.x,at.y);U.Fill(new Rect(a.x-w/2,a.y-len/2,w,len),U.A(Stick,alpha));U.Fill(new Rect(a.x-w/2,a.y-len/2,w*.24f,len),U.A(U.Mix(Stick,Color.white,.25f),alpha));U.Fill(new Rect(a.x-w/2-.5f,a.y-len*.32f,w+1,len*.12f),U.A(Wrap,alpha));U.Fill(new Rect(a.x-w/2-.5f,a.y+len*.2f,w+1,len*.12f),U.A(Wrap,alpha));
            U.Fill(new Rect(a.x-1,a.y-len*.72f,2,len*.22f),U.A(Soot,alpha));U.Pop();
            if(lit){float rad=degrees*Mathf.Deg2Rad;var tip=at+new Vector2(Mathf.Sin(rad),-Mathf.Cos(rad))*(len*.74f);Soft(tip,len*.22f,U.A(Hot,alpha));Soft(tip,len*.08f,U.A(Color.white,alpha));}
        }
        // A blast of size `s` px at `p`, `age` s old: the flash, a rolling fireball, hard sparks, soot and a ring.
        private static void Boom(Vector2 p,float age,float s,int seed,float a=1,bool calm=false)
        {
            if(age<0||age>1.4f)return;float u=Mathf.Clamp01(age/.55f);
            int smoke=calm?2:7;if(age>.1f)for(int i=0;i<smoke;i++){var d=Dir(seed,i+40);float v=Ramp(age,.1f,1.4f);var q=p+d*(s*.5f*Out(v))+new Vector2(0,-s*1.1f*v);Soft(q,s*(.42f+.5f*v),U.A(Soot,.62f*(1-v)*a));}
            if(age<.55f)
            {
                int fire=calm?3:11;for(int i=0;i<fire;i++){var d=Dir(seed,i);var q=p+d*(s*.75f*Out(u)*(.45f+.55f*R(seed,i+11)))+new Vector2(0,-s*.25f*u);Color c=u<.3f?Color.Lerp(Color.white,Hot,u/.3f):Color.Lerp(Hot,Flame,(u-.3f)/.45f);c=u>.75f?Color.Lerp(Flame,Ember,(u-.75f)/.25f):c;Soft(q,s*(.26f+.42f*u)*(.8f+.4f*R(seed,i+22)),U.A(c,(1-u*u)*a));}
            }
            if(calm)return;
            if(age<.14f)Soft(p,s*(.9f+3*age),U.A(Color.white,(1-age/.14f)*a));
            if(age<.36f){float w=age/.36f;Ring(p,s*(.35f+1.5f*Out(w)),2.2f*(1-w)+.6f,U.A(Hot,(1-w)*a));}
            if(age<.7f){float w=age/.7f;for(int i=0;i<16;i++){var d=Dir(seed,i+60);float sp=s*(1.2f+1.9f*R(seed,i+70));var q=p+d*(sp*Out(w))+new Vector2(0,s*.9f*w*w);Streak(q-d*(s*.18f*(1-w)),q,1.6f*(1-w)+.5f,U.A(Hot,(1-w)*a));}}
        }
        // ---- the card ----
        // The face, over the body and the traces and under the core, the text and the bezel's light.
        public static void Face(Rect r,float radius,string id,float t,float a,bool plain,bool calm)
        {
            if(plain||a<=0)return;float bw=NativeChipArt.BezelOf(radius),tc=NativeChipArt.TopCut(radius),bc=NativeChipArt.BottomCut(radius);var face=Rect.MinMaxRect(r.x+bw,r.y+tc*.55f,r.xMax-bw,r.yMax-bc*.55f);int index=NativeBombChips.Index(id);
            U.BeginBand(face);
            // The scorch: burnt in from the corner under the emblem, a little heat left in it.
            Soft(new Vector2(face.xMax-face.width*.1f,face.yMax-face.height*.1f),face.width*.62f,U.A(Soot,.8f*a));float flick=calm?.6f:.5f+.2f*Mathf.Sin(t*7.3f)+.15f*Mathf.Sin(t*17.1f);Soft(new Vector2(face.xMax-face.width*.08f,face.yMax),face.width*.34f,U.A(Ember,.13f*flick*a));
            // The hazard tape along the foot, slightly off true, torn at one end.
            float ty=face.yMax-8;U.Push(new Vector2(face.center.x,ty),Vector2.zero,1,-1.1f);U.Fill(new Rect(face.x-6,ty-3.5f,face.width*.86f,7),U.A(Tape,.2f*a));for(float x=face.x-6;x<face.x+face.width*.86f-6;x+=11){U.Push(new Vector2(x,ty),Vector2.zero,1,-35);U.Fill(new Rect(x-2.4f,ty-6,4.8f,12),U.A(Soot,.5f*a));U.Pop();}U.Pop();
            // The emblem, large behind the text, and its own small scene.
            float g=Mathf.Min(face.width*.56f,face.height*.72f);var gr=new Rect(face.xMax-g*.9f,face.yMax-g*.92f,g,g);string glyph=NativeBombChips.Glyph(id);if(glyph!=null)NativeIcons.DrawMarkup(gr,"bomb-face:"+id,glyph,NativeBombChips.GlyphBox,U.A(Flame,(.1f+.025f*flick)*a));
            if(!calm&&index>=0)Scene(gr,index,t,a);
            U.EndBand();
            // The fuse, burning round the card's edge: the char behind it, the flame, what it spits.
            if(calm){return;}
            float per=2*(r.width+r.height),head=Mathf.Repeat(t/5.2f+R(index+3,1),1)*per;Vector2 At(float d){d=Mathf.Repeat(d,per);float w=r.width-3,h=r.height-3,x0=r.x+1.5f,y0=r.y+1.5f;if(d<w)return new Vector2(x0+d,y0);d-=w;if(d<h)return new Vector2(x0+w,y0+d);d-=h;if(d<w)return new Vector2(x0+w-d,y0+h);d-=w;return new Vector2(x0,y0+h-Mathf.Min(h,d));}
            for(int i=0;i<16;i++){float back=i*5.5f,u=i/16f;Streak(At(head-back),At(head-back-5.5f),2.2f,U.A(Color.Lerp(Flame,Soot,u),(1-u)*.85f*a));}
            var p=At(head);Soft(p,12,U.A(Flame,.75f*a));Soft(p,5.5f,U.A(Hot,a));Soft(p,2.2f,U.A(Color.white,a));
            for(int i=0;i<5;i++){float ph=Mathf.Repeat(t*2.4f+i*.21f,1);int seed=(int)(t*2.4f+i*.21f)+i*17;var d=Dir(seed,i);Streak(p+d*(16*ph)+new Vector2(0,10*ph*ph),p+d*(16*ph+2.2f)+new Vector2(0,10*ph*ph),1.1f,U.A(Hot,(1-ph)*a));}
        }
        // Each chip's own small loop on the card.
        private static void Scene(Rect g,int index,float t,float a)
        {
            Vector2 C(float x,float y)=>new Vector2(g.x+g.width*x,g.y+g.height*y);float u=g.width/100;
            switch(index)
            {
                case 0:{float k=Mathf.Repeat(t/2.6f,1);if(k<.5f){var p=Vector2.Lerp(C(.92f,.1f),C(.7f,.3f),k*2);Soft(p,5*u,U.A(Flame,.7f*a));Soft(p,2*u,U.A(Hot,a));}else if(k<.72f){float w=(k-.5f)/.22f;Ring(C(.66f,.34f),(6+24*w)*u,1.2f,U.A(Hot,(1-w)*.8f*a),14);Soft(C(.66f,.34f),14*u*(1-w),U.A(Flame,(1-w)*a));}break;}
                case 1:{for(int i=0;i<10;i++){float k=Mathf.Repeat(t*.5f+R(4,i),1);var p=C(.2f+.6f*R(5,i),.04f+.22f*k);U.Fill(new Rect(p.x,p.y,1.4f,1.4f),U.A(Hot,.5f*(1-k)*a));}float l=Mathf.Repeat(t/3.4f,1);if(l>.8f){float w=(l-.8f)/.2f;Soft(C(.2f+.6f*w,.92f),10*u,U.A(Flame,(1-w)*.8f*a));}break;}
                case 2:{int on=(int)Mathf.Repeat(t*2.2f,6);for(int i=0;i<4;i++){var tip=C(.17f+.21f*i,.12f+(i==2?-.06f:0));float b=i==on?1:0;if(b>0){Soft(tip,5*u,U.A(Flame,.8f*a));Soft(tip,2*u,U.A(Hot,a));}}break;}
                case 3:{float k=Mathf.Repeat(t/1.3f,1),h=4*k*(1-k);var at=C(.5f,.54f-.3f*h);Soft(at,8*u,U.A(Flame,.26f*a));if(k<.12f)Ring(C(.5f,.68f),(4+22*k/.12f)*u,1,U.A(Hot,(1-k/.12f)*.7f*a),14);bool blink=Mathf.Repeat(t*3,1)<.3f;if(blink)Soft(C(.5f,.33f-.3f*h),3*u,U.A(Led,a));break;}
                case 4:{float k=Mathf.Repeat(t/2.2f,1)*3;for(int i=0;i<3;i++){Vector2 p=i==0?C(.18f,.76f):i==1?C(.5f,.46f):C(.84f,.18f);float w=Mathf.Clamp01(1-Mathf.Abs(k-i-.5f)*1.6f);Soft(p,(6+9*w)*u,U.A(Flame,.75f*w*a));Soft(p,3*u,U.A(Hot,w*a));}if(k<2){int i=(int)k;float w=k-i;Vector2 p0=i==0?C(.18f,.76f):C(.5f,.46f),p1=i==0?C(.5f,.46f):C(.84f,.18f);var head=Vector2.Lerp(p0,p1,w);Streak(Vector2.Lerp(p0,p1,Mathf.Max(0,w-.25f)),head,1.5f,U.A(Hot,a));}break;}
                case 5:{for(int i=0;i<7;i++){float x=.08f+.84f*i/6f,fl=.55f+.45f*Mathf.Sin(t*(5+i)+i*1.7f),h=(.08f+.13f*fl)*g.height;var b=C(x,.74f);Soft(b+new Vector2(0,-h*.5f),h*.8f,U.A(Flame,.38f*a));Soft(b+new Vector2(0,-h*.2f),h*.42f,U.A(Hot,.5f*a));}break;}
                case 6:{for(int i=0;i<3;i++){float x=.37f+.2f*i,fl=.7f+.3f*Mathf.Sin(t*23+i*2);var from=C(x,.72f);var to=C(x,.72f+(.2f-.04f*i)*fl);Streak(from,to,(5-i)*u,U.A(Flame,.62f*a));Streak(from,Vector2.Lerp(from,to,.6f),(2.2f-.4f*i)*u,U.A(Hot,.9f*a));}break;}
                case 7:{float k=Mathf.Repeat(t/2.4f,1);if(k<.65f){float w=k/.65f;for(int i=0;i<5;i++){float ang=(i-2)*.55f;var p=C(.5f,.32f)+new Vector2(Mathf.Sin(ang),.2f)*(46*u*w)+new Vector2(0,26*u*w*w);Soft(p,2.8f*u,U.A(Hot,(1-w*.3f)*a));}}else{float w=(k-.65f)/.35f;for(int i=0;i<5;i++){float ang=(i-2)*.55f;var p=C(.5f,.32f)+new Vector2(Mathf.Sin(ang),.2f)*(46*u)+new Vector2(0,26*u);Soft(p,(3+9*w)*u,U.A(Flame,(1-w)*.8f*a));}}break;}
                case 8:{float k=Mathf.Repeat(t/2.2f,1);if(k<.5f){float w=k/.5f;Ring(C(.5f,.54f),(8+40*w)*u,1.2f,U.A(Hot,(1-w)*.8f*a),18);}float hit=k>.42f&&k<.7f?1-Mathf.Abs(k-.56f)/.14f:0;if(hit>0)Soft(C(.5f,.54f),30*u,U.A(Flame,.3f*hit*a));break;}
                default:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t/2.1f+i*.33f,1);int s=(int)(t/2.1f+i*.33f)+i*9;var p=C(.18f+.64f*R(s,1),.08f+.28f*R(s,2));Color c=i==0?Hot:i==1?Flame:U.Hex(0xff7ac0);if(k<.3f){float w=k/.3f;for(int j=0;j<8;j++){var d=Dir(s,j);Streak(p+d*(10*u*w),p+d*(15*u*w),1.1f,U.A(c,(1-w)*a));}}}break;}
            }
        }
        // ---- the moment it goes in: the spotlight's card ----
        /// <summary>How far the card is knocked at reveal time `t` (the effect's own clock runs from <see cref="Start"/>), under the same clock as <see cref="Reveal"/>.</summary>
        public const float Start=.55f;
        public static Vector2 Shake(string id,float t,bool calm)
        {
            if(calm)return Vector2.zero;float s=t-Start;if(s<0)return Vector2.zero;float kick=0;Vector2 lift=Vector2.zero;
            switch(NativeBombChips.Index(id))
            {
                case 0:kick=Pulse(s,.45f,.3f,9);break;case 1:kick=Pulse(s,.55f,.5f,10);break;case 2:for(int i=0;i<7;i++)kick+=Pulse(s,.75f+i*.09f,.14f,4);break;
                case 3:kick=Pulse(s,.25f,.12f,3)+Pulse(s,.95f,.34f,11);lift=new Vector2(0,10*Mathf.Clamp01(Mathf.Min(s-.78f,.95f-s)*12));break;case 4:for(int i=0;i<6;i++)kick+=Pulse(s,.1f+.18f*i,.16f,3+1.4f*i);break;
                case 5:kick=Pulse(s,.05f,.8f,3);break;case 6:{float up=Ramp(s,.05f,.2f)*(1-Ramp(s,.72f,.9f));lift=new Vector2(0,-34*up);kick=2.4f*up+Pulse(s,.9f,.3f,9);break;}
                case 7:kick=Pulse(s,.45f,.3f,8);break;case 8:kick=Pulse(s,.2f,.22f,6);break;default:kick=Pulse(s,.1f,.2f,4)+Pulse(s,.6f,.2f,3);break;
            }
            return lift+new Vector2(Mathf.Sin(t*71)*kick,Mathf.Sin(t*83+1)*kick);
        }
        private static float Pulse(float s,float at,float life,float size)=>s<at||s>at+life?0:size*(1-(s-at)/life);
        /// <summary>The chip's effect over the spotlight's `card` at reveal time `t` (s), at the block's `env` alpha. Each is its own joke.</summary>
        public static void Reveal(Rect card,string id,float t,float env,bool calm)
        {
            float s=t-Start;if(env<=0||s<0)return;int index=NativeBombChips.Index(id);if(index<0)return;float a=env;var c=card.center;float W=card.width,H=card.height;Vector2 At(float x,float y)=>new Vector2(card.x+W*x,card.y+H*y);
            // Under Reduced, one quiet glow and the card; the jokes want movement and flashes.
            if(calm){float w=Mathf.Clamp01(s/.5f);Soft(c,W*.7f,U.A(Flame,.25f*(1-w)*a));return;}
            switch(index)
            {
                // Short Fuse: it burns round the whole card in under half a second, and the card goes up.
                case 0:
                {
                    const float Run=.45f;float per=2*(W+H);Vector2 Edge(float d){d=Mathf.Min(d,per);if(d<W)return new Vector2(card.x+d,card.y);d-=W;if(d<H)return new Vector2(card.xMax,card.y+d);d-=H;if(d<W)return new Vector2(card.xMax-d,card.yMax);d-=W;return new Vector2(card.x,card.yMax-d);}
                    if(s<Run){float d=per*(s/Run);for(int i=0;i<34;i++){float b=i*(per/70);if(d-b<0)break;Streak(Edge(d-b),Edge(d-b-per/70),3,U.A(Color.Lerp(Hot,Soot,i/34f),a));}var head=Edge(d);Soft(head,22,U.A(Flame,a));Soft(head,9,U.A(Color.white,a));for(int i=0;i<10;i++){var q=head+Dir((int)(t*40),i)*(26*R((int)(t*40),i+12));Streak(q,q+new Vector2(0,5),1.4f,U.A(Hot,a));}}
                    Boom(At(.5f,.3f),s-Run,W*.42f,3,a);Boom(At(.12f,.8f),s-Run-.08f,W*.2f,4,a);Boom(At(.88f,.72f),s-Run-.14f,W*.24f,5,a);
                    break;
                }
                // Black Powder: a sack's worth pours down the card and heaps under it, then one spark, left to right, and a black mushroom.
                case 1:
                {
                    int n=70;for(int i=0;i<n;i++){float at=R(11,i)*.5f,k=Ramp(s,at,at+.35f);if(k<=0)continue;float x=card.x+W*(.05f+.9f*R(12,i)),y=Mathf.Lerp(card.y-30,card.yMax+12-16*R(13,i),k*k);if(s<.62f)U.Fill(new Rect(x,y,2.2f,2.2f),U.A(Soot,a));}
                    if(s<.62f)U.Fill(new Rect(card.x+W*.04f,card.yMax+9,W*.92f*Ramp(s,.05f,.55f),5),U.A(Soot,a));
                    for(int i=0;i<6;i++)Boom(At(.08f+.168f*i,1.03f),s-.55f-i*.05f,W*.2f,20+i,a);
                    {float k=Ramp(s,.7f,2.4f),o=(1-k)*Ramp(s,.65f,.85f);for(int i=0;i<16;i++){var d=Dir(31,i);Soft(c+new Vector2(d.x*W*.5f*(.35f+k),-H*(.3f+1.2f*k)+d.y*H*.3f),W*(.14f+.18f*k),U.A(Soot,.75f*o*a));}}
                    break;
                }
                // Bandolier: seven sticks slam into a row under the card, one by one, lit, and go off in order.
                case 2:
                {
                    for(int i=0;i<7;i++)
                    {
                        float at=i*.075f,k=Ramp(s,at,at+.11f),gone=.75f+i*.09f,x=card.x+W*(.1f+.8f*i/6f);if(k<=0)continue;var home=new Vector2(x,card.yMax+36);
                        if(s<gone){var p=Vector2.Lerp(home+new Vector2(0,70),home,k*k);DrawStick(p,46,(i-3)*3,a,s>.6f);if(k>=1&&s-at<.28f){float w=(s-at-.11f)/.17f;for(int j=0;j<3;j++)Soft(home+new Vector2((j-1)*12*w,20),9*(1-w)+2,U.A(U.Hex(0x8a7f70),.5f*(1-w)*a));}}
                        Boom(home,s-gone,W*.2f,40+i,a);
                    }
                    break;
                }
                // Bouncing Betty: a mine lands on the card with a clank, blinks three times, the card ducks, and it springs up and bursts.
                case 3:
                {
                    var top=new Vector2(c.x,card.y-10);float fall=Ramp(s,0,.25f);const float Spring=.95f;
                    if(s<Spring+.22f)
                    {
                        float up=Ramp(s,Spring,Spring+.22f);var p=Vector2.Lerp(top+new Vector2(0,-160),top,fall*fall)+new Vector2(0,-150*Out(up));if(s>=.25f&&s<.42f)Ring(top,(s-.25f)*220,1.4f,U.A(Hot,(1-(s-.25f)/.17f)*a),20);
                        // Its spring first, so it sits behind the plate.
                        if(up>0)for(int i=0;i<7;i++){float y=Mathf.Lerp(top.y,p.y,i/7f),o=i%2==0?-20:20;Streak(new Vector2(c.x+o,y),new Vector2(c.x-o,Mathf.Lerp(top.y,p.y,(i+1)/7f)),3.2f,U.A(Steel,a));}
                        U.Round(new Rect(p.x-72,p.y-9,144,18),U.A(Steel,a),7);U.Fill(new Rect(p.x-72,p.y+2,144,3),U.A(Soot,.5f*a));U.Round(new Rect(p.x-56,p.y-23,112,16),U.A(Soot,a),6);U.Fill(new Rect(p.x-56,p.y-18,112,5),U.A(Tape,a));
                        for(int i=-1;i<=1;i+=2)DrawStick(new Vector2(p.x+i*26,p.y-31),38,90,a,false);U.Fill(new Rect(p.x-6,p.y-41,12,20),U.A(Wrap,a));
                        for(int i=0;i<4;i++){float x=p.x-60+i*40;U.Fill(new Rect(x-3,p.y-14,6,6),U.A(U.Hex(0xd6a24a),a));}
                        var led=new Vector2(p.x,p.y-44);Soft(led,7,U.A(U.Hex(0x4a0a08),a));bool on=s>.25f&&s<Spring&&Mathf.Repeat((s-.25f)*4.3f,1)<.4f;if(on){Soft(led,44,U.A(Led,.85f*a));Soft(led,11,U.A(Color.white,a));}
                    }
                    Boom(top+new Vector2(0,-150),s-Spring-.22f,W*.5f,50,a);
                    break;
                }
                // Daisy Chain: blasts go round the card, each lighting the next along a burning line, bigger every time.
                case 4:
                {
                    Vector2[] p={At(-.08f,.85f),At(.06f,-.1f),At(.5f,-.2f),At(.95f,-.08f),At(1.08f,.8f),At(.5f,1.15f)};
                    for(int i=0;i<p.Length;i++)
                    {
                        float at=.1f+.18f*i;if(i>0){float k=Ramp(s,at-.18f,at);if(k>0&&k<1){Streak(Vector2.Lerp(p[i-1],p[i],Mathf.Max(0,k-.35f)),Vector2.Lerp(p[i-1],p[i],k),3.4f,U.A(Color.Lerp(Hot,Color.white,.4f),a));}}
                        Boom(p[i],s-at,W*(.16f+.035f*i),60+i,a);float w=(s-at)/.32f;if(w>=0&&w<1){float sz=(12+4*i)*(1-w*.3f);for(int j=0;j<4;j++){float ang=j*Mathf.PI/2+.4f;var d=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));Streak(p[i]-d*sz,p[i]+d*sz,1.6f,U.A(Color.white,(1-w)*a));}}
                    }
                    break;
                }
                // Scorched Earth: fire climbs out of the floor under the card and the bottom of the card chars black.
                case 5:
                {
                    float rise=Out(s/.35f),die=1-Ramp(s,1.2f,2);int n=26;for(int i=0;i<n;i++){float x=card.x-W*.1f+W*1.2f*i/(n-1),fl=.6f+.4f*Mathf.Sin(t*(6+R(70,i)*9)+i*1.3f),h=H*(.25f+.5f*R(71,i))*fl*rise*die;var b=new Vector2(x,card.yMax+18);Soft(b+new Vector2(0,-h*.72f),W*.08f+h*.2f,U.A(Ember,.5f*a));Soft(b+new Vector2(0,-h*.42f),W*.07f+h*.16f,U.A(Flame,.72f*a));Soft(b+new Vector2(0,-h*.14f),W*.05f+h*.1f,U.A(Hot,.85f*a));}
                    U.Fade(new Rect(card.x,card.yMax-H*.42f*rise,W,H*.42f*rise),U.A(Soot,.85f*Mathf.Max(die,.35f)*a),false);
                    for(int i=0;i<34;i++){float ph=Mathf.Repeat(s*(.5f+.5f*R(72,i))+R(73,i),1);var q=new Vector2(card.x-W*.1f+W*1.2f*R(74,i)+Mathf.Sin(t*2+i)*9,card.yMax+10-H*1.5f*ph);U.Fill(new Rect(q.x,q.y,2.4f,2.4f),U.A(Hot,(1-ph)*die*a));}
                    break;
                }
                // Nitro Boots: the card lights two jets and tries to leave, gets a foot up, runs out, and lands in its own smoke.
                case 6:
                {
                    float burn=Ramp(s,.05f,.14f)*(1-Ramp(s,.72f,.88f));for(int side=-1;side<=1;side+=2){var o=new Vector2(c.x+side*W*.34f,card.yMax+2);float fl=.8f+.2f*Mathf.Sin(t*51+side);Soft(o+new Vector2(0,72*burn),60*burn,U.A(Ember,.5f*a));Streak(o,o+new Vector2(0,165*burn*fl),44*burn,U.A(Flame,.8f*a));Streak(o+new Vector2(0,120*burn*fl),o+new Vector2(0,190*burn*fl),18*burn,U.A(Flame,.45f*a));Streak(o,o+new Vector2(0,100*burn*fl),20*burn,U.A(Hot,.95f*a));Soft(o,40*burn,U.A(Color.white,.8f*a));for(int i=0;i<9;i++){float ph=Mathf.Repeat(s*2.6f+R(80,i),1);var q=o+new Vector2(S(81,i)*24*ph,140*ph);Soft(q,8+24*ph,U.A(Soot,.5f*(1-ph)*burn*a));}}
                    {float land=s-.9f;if(land>=0&&land<.9f){float w=land/.9f;for(int i=0;i<14;i++){float ang=i*Mathf.PI*2/14;var q=new Vector2(c.x,card.yMax+18)+new Vector2(Mathf.Cos(ang)*W*.75f*Out(w),Mathf.Sin(ang)*10*Out(w)-16*w);Soft(q,10+22*w,U.A(U.Hex(0x7a7068),.55f*(1-w)*a));}}}
                    break;
                }
                // Cluster Fun: the big one sails over the card, opens, and nine firecrackers rain down and go off in a stutter.
                case 7:
                {
                    const float Open=.45f;var burst=At(.5f,-.34f);if(s<Open){float k=s/Open;var p=Vector2.Lerp(At(-.5f,.4f),burst,k)+new Vector2(0,-60*4*k*(1-k));U.Push(p,Vector2.zero,1,s*520);U.Fill(new Rect(p.x-20,p.y-30,40,60),U.A(Stick,a));U.Fill(new Rect(p.x-21,p.y-8,42,10),U.A(Tape,a));U.Pop();Soft(p+new Vector2(0,-30),12,U.A(Hot,a));}
                    Boom(burst,s-Open,W*.34f,90,a);
                    for(int i=0;i<9;i++){float x=-.1f+1.2f*i/8f,land=Open+.34f+.03f*i,k=Ramp(s,Open,land);if(k<=0)continue;var end=At(x,1.1f);if(s<land){var q=Vector2.Lerp(burst,end,k)+new Vector2(0,-80*k*(1-k));DrawStick(q,20,s*700+i*40,a,true);}Boom(end,s-land-.02f*(i%3),W*.13f,100+i,a);}
                    break;
                }
                // Blast Shield: the blast goes off in its face, hits a dome of light round the card, and goes round it. Not a scratch.
                case 8:
                {
                    const float Hit=.2f;float rd=Mathf.Max(W,H)*.68f;Boom(c,s,W*.22f,110,a);float w=Mathf.Clamp01(s/Hit);if(s<Hit)Ring(c,rd*w,4,U.A(Hot,a));
                    float dome=Ramp(s,Hit-.03f,Hit+.03f)*(1-Ramp(s,Hit+.5f,Hit+1));
                    if(dome>0){const int N=10;for(int i=0;i<N*2;i++){float ang=i*Mathf.PI/N;var d=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));float rip=Mathf.Max(0,1-Mathf.Abs(Mathf.Repeat(ang/(2*Mathf.PI)-s*.9f,1)-.5f)*6);var a0=c+d*rd;var d1=new Vector2(Mathf.Cos(ang+Mathf.PI/N),Mathf.Sin(ang+Mathf.PI/N));var a1=c+d1*rd;Streak(a0,a1,2+2.6f*rip,U.A(Color.Lerp(U.Hex(0x70e1ff),Color.white,rip),(.55f+.45f*rip)*dome*a));Streak(a0,c+d*(rd*.86f),1,U.A(U.Hex(0x70e1ff),.3f*dome*a));}Soft(c,rd,U.A(U.Hex(0x70c8ff),.1f*dome*a));}
                    if(s>Hit&&s<Hit+.8f){float k=(s-Hit)/.8f;for(int i=0;i<40;i++){var d=Dir(120,i);var q=c+d*(rd+(90+130*R(121,i))*Out(k));Streak(q,q+d*(9*(1-k)+2),1.6f,U.A(Hot,(1-k)*a));}}
                    break;
                }
                // Encore: the curtain call. Fireworks up and all round the card, confetti down, and its name in lights.
                default:
                {
                    Color[] hues={U.Hex(0xffd27a),U.Hex(0xff5a4a),U.Hex(0xff7ac0),U.Hex(0x70e1ff),U.Hex(0xfff0c8),U.Hex(0xff9a2a)};
                    for(int i=0;i<9;i++){float at=.05f+.14f*i,rise=.28f,x=-.25f+1.5f*R(130,i),y=-.5f+.5f*R(131,i);var end=At(x,y);var from=At(x+.04f*S(132,i),1.3f);float k=Ramp(s,at,at+rise);if(k>0&&k<1){var q=Vector2.Lerp(from,end,Out(k));Streak(q,q+new Vector2(0,20*(1-k)),2.2f,U.A(Hot,a));}float b=s-at-rise;if(b>=0&&b<1.2f){var hue=hues[i%hues.Length];float w=b/1.2f;if(b<.12f)Soft(end,W*.14f*(1-b/.12f),U.A(Color.white,a));for(int j=0;j<26;j++){var d=Dir(140+i,j);float sp=W*(.2f+.14f*R(150+i,j)),mv=sp*Out(w*1.4f);var q=end+d*mv+new Vector2(0,H*.5f*w*w);Streak(q,q-d*(10*(1-w)+1.5f),1.8f*(1-w)+.5f,U.A(hue,(1-w)*a));}}}
                    for(int i=0;i<46;i++){float k=Ramp(s,.2f+.5f*R(160,i),2.6f);if(k<=0||k>=1)continue;var q=new Vector2(card.x-W*.5f+W*2*R(161,i)+Mathf.Sin(t*3+i)*14,card.y-H*.9f+H*2.6f*k);U.Push(q,Vector2.zero,1,t*(180+R(162,i)*300));U.Fill(new Rect(q.x-3,q.y-1.5f,6,3),U.A(hues[i%hues.Length],(1-k*k)*a));U.Pop();}
                    {float k=Ramp(s,.25f,.55f),o=k*(1-Ramp(s,2,2.6f)),sc=1.5f-.5f*Out(k);var mid=new Vector2(c.x,card.y-96);U.Push(mid,Vector2.zero,sc,-3);U.Text(new Rect(mid.x-200,mid.y-30,400,60),"ENCORE!",46,U.A(U.Hex(0xfff0c8),o*a),TextAnchor.MiddleCenter,bold:true,glow:U.Hex(0xff7a1f,.9f*o*a),shadow:U.A(Color.black,.7f*o*a));U.Pop();}
                    break;
                }
            }
        }
    }
}
