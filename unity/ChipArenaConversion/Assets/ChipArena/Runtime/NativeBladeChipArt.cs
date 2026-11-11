using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The blade classes' chips on the screen (docs/KATANA.md, docs/FANGS.md "The chips"). Face: the Katana's card of crimson night (a moon
    // behind the emblem, petals drifting across, a slash of light along the top, a glint running round its edge like a drawn blade's) and
    // the Fangs' card of venom dark (smoke along its foot, drips running from the emblem, two eyes glinting, a green glint round the edge),
    // each chip's emblem large behind the text with its own small loop. Reveal: what happens when one is taken, a piece of the class's own
    // look for each of the twenty, over the INSTALLED card, the card knocked, turned and pulsed in time with it (Pose); the cues in Core
    // NativeKatanaChips/NativeFangsChips.Picks. Pure functions of the clock; nothing allocated per frame; one soft glow under Reduced.
    public static class NativeBladeChipArt
    {
        private static readonly Color Crimson=U.Hex(0xff3d6e),Blossom=U.Hex(0xffb3c6),Ivory=U.Hex(0xfff1e6),Steel=U.Hex(0xc9cdd6),SteelDark=U.Hex(0x3b3f4a),Night=U.Hex(0x14101e),
            Venom=U.Hex(0x7dff3a),Lime=U.Hex(0xd4ff9e),Smoke=U.Hex(0x2a2e30),Bone=U.Hex(0xe9f0dc),Blood=U.Hex(0xd0203a),Ink=U.Hex(0x050608);
        private static float R(int seed,int i){float x=Mathf.Sin(seed*12.9898f+i*78.233f)*43758.5453f;return x-Mathf.Floor(x);}
        private static float S(int seed,int i)=>R(seed,i)*2-1;
        private static float Out(float x){x=Mathf.Clamp01(x);float o=1-x;return 1-o*o*o;}
        private static float Ramp(float x,float a,float b)=>Mathf.Clamp01((x-a)/Mathf.Max(1e-4f,b-a));
        private static float Bell(float x,float a,float b){float k=Ramp(x,a,b);return 4*k*(1-k);}
        private static Vector2 Polar(float a,float r)=>new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
        private static Vector2 Dir(int seed,int i){float a=R(seed,i)*Mathf.PI*2;return new Vector2(Mathf.Cos(a),Mathf.Sin(a));}
        private static void Streak(Vector2 a,Vector2 b,float w,Color c)
        {
            var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=0||w<=0)return;var m=(a+b)*.5f;U.Push(m,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(m.x-len/2,m.y-w/2,len,w),c);U.Pop();
        }
        private static void Soft(Vector2 p,float r,Color c){if(r>0&&c.a>0)U.Radial(new Rect(p.x-r,p.y-r,2*r,2*r),c);}
        private static void Blob(Vector2 p,float w,float h,Color c,float deg=0){if(c.a<=0||w<=0||h<=0)return;U.Push(p,Vector2.zero,1,deg);U.Round(new Rect(p.x-w/2,p.y-h/2,w,h),c,Mathf.Min(w,h)/2);U.Pop();}
        private static void Ring(Vector2 p,float r,float w,Color c,int seg=26){if(c.a<=0||r<=0)return;Vector2 last=p+new Vector2(r,0);for(int i=1;i<=seg;i++){var q=p+Polar(i*Mathf.PI*2/seg,r);Streak(last,q,w,c);last=q;}}
        private static void Arc(Vector2 p,float r,float from,float to,float w,Color c,int seg=18,bool taper=false)
        {
            if(c.a<=0||r<=0)return;var last=p+Polar(from,r);
            for(int i=1;i<=seg;i++){float u=i/(float)seg,mid=(i-.5f)/seg;var q=p+Polar(from+(to-from)*u,r);Streak(last,q,taper?w*Mathf.Sin(Mathf.PI*mid)+.6f:w,c);last=q;}
        }
        private static void Glint(Vector2 p,float r,Color c,float a,float deg=0)
        {
            if(a<=0||r<=0)return;var d=Polar(deg*Mathf.Deg2Rad,r);var n=new Vector2(-d.y,d.x)*.55f;Soft(p,r*.55f,U.A(c,.6f*a));
            Streak(p-d,p+d,2.2f,U.A(c,a));Streak(p-n,p+n,1.8f,U.A(c,.8f*a));Soft(p,r*.18f,U.A(Color.white,a));
        }
        private static void Burst(Vector2 p,float age,float size,Color c,float a,int seed=1,int n=12)
        {
            if(age<0||age>.5f)return;float w=age/.5f;if(age<.08f)Soft(p,size*(.5f+2*age),U.A(Color.white,(1-age/.08f)*a));
            for(int i=0;i<n;i++){var d=Dir(seed,i);float len=size*(.4f+.6f*R(seed,i+20));Streak(p+d*(size*.25f+len*Out(w)*.55f),p+d*(size*.25f+len*Out(w)),2.4f*(1-w)+.6f,U.A(i%2==0?c:Color.white,(1-w)*a));}
        }
        // A petal: a soft leaf of light, turning.
        private static void Petal(Vector2 p,float size,float deg,Color c){Blob(p,size,size*.45f,c,deg);Soft(p,size*.3f,U.A(Color.white,c.a*.4f));}
        // A crescent moon: a soft disc with a darker disc over its edge.
        private static void Moon(Vector2 p,float r,Color c,float a){if(a<=0)return;Soft(p,r*2.2f,U.A(c,.18f*a));Blob(p,r*2,r*2,U.A(c,.85f*a));Blob(p+new Vector2(r*.45f,-r*.2f),r*1.7f,r*1.7f,U.A(Night,.92f*a));}
        // A katana's silhouette from pommel to tip.
        private static void Katana(Vector2 from,Vector2 to,float w,Color edge,float a)
        {
            if(a<=0)return;var d=to-from;float l=d.magnitude;if(l<1)return;var u=d/l;var n=new Vector2(-u.y,u.x);var guard=from+u*(l*.26f);
            Vector2 prev=guard;for(int i=1;i<=10;i++){float k=i/10f;var p=guard+u*(l*.74f*k)+n*(Mathf.Sin(Mathf.PI*k*.9f)*l*.05f);Streak(prev,p,w*(k<.9f?1:(1-(k-.9f)/.1f*.8f)),U.A(Steel,a));Streak(prev,p,1.4f,U.A(edge,a));prev=p;}
            Streak(guard-n*w*1.4f,guard+n*w*1.4f,w*.8f,U.A(SteelDark,a));Streak(from,guard,w*.9f,U.A(Ink,a));
        }
        // A fang: a curved tooth of a blade from root to point.
        private static void Fang(Vector2 root,Vector2 point,float w,Color edge,float a)
        {
            if(a<=0)return;var d=point-root;float l=d.magnitude;if(l<1)return;var u=d/l;var n=new Vector2(-u.y,u.x);Vector2 prev=root;
            for(int i=1;i<=10;i++){float k=i/10f;var p=root+u*(l*k)+n*(l*.16f*k*k);Streak(prev,p,w*(1-k*.85f),U.A(SteelDark,a));Streak(prev,p,1.3f,U.A(edge,a));prev=p;}
        }
        // A drip falling from `from`, `age` s after it let go.
        private static void Drip(Vector2 from,float age,float size,Color c,float a){if(age<0)return;var p=from+new Vector2(0,120*age*age);Blob(p,size,size*1.4f,U.A(c,a*(1-Ramp(age,.6f,.9f))));}

        // ---- the card ----
        public static void Face(Rect r,float radius,string id,float t,float a,bool plain,bool calm)
        {
            if(plain||a<=0)return;bool katana=NativeKatanaChips.Is(id);float bw=NativeChipArt.BezelOf(radius),tc=NativeChipArt.TopCut(radius),bc=NativeChipArt.BottomCut(radius);var face=Rect.MinMaxRect(r.x+bw,r.y+tc*.55f,r.xMax-bw,r.yMax-bc*.55f);
            int index=katana?NativeKatanaChips.Index(id):NativeFangsChips.Index(id);var tint=katana?Crimson:Venom;
            U.BeginBand(face);
            float g=Mathf.Min(face.width*.56f,face.height*.72f);var gr=new Rect(face.xMax-g*.9f,face.yMax-g*.92f,g,g);var gc=gr.center;
            if(katana)
            {
                // A crimson moon behind the emblem, petals drifting across, a slash of light along the top.
                Soft(gc+new Vector2(-g*.1f,-g*.15f),g*.9f,U.A(Crimson,.1f*a));Moon(gc+new Vector2(-g*.18f,-g*.2f),g*.22f,Crimson,.25f*a);
                for(int i=0;i<6;i++){float k=Mathf.Repeat(t*.08f+i/6f,1);Petal(new Vector2(face.x+face.width*(k*1.2f-.1f),face.y+face.height*(.2f+.6f*R(7,i)+.1f*Mathf.Sin(t+i))),5,t*60+i*40,U.A(Blossom,.35f*Mathf.Sin(Mathf.PI*k)*a));}
                Streak(new Vector2(face.x+face.width*.08f,face.y+face.height*.16f),new Vector2(face.xMax-face.width*.06f,face.y+face.height*.08f),1.6f,U.A(Crimson,.3f*a));
            }
            else
            {
                // Smoke along the foot, drips running from the emblem, two eyes in the dark.
                for(int i=0;i<5;i++){float k=Mathf.Repeat(t*.15f+i/5f,1);Soft(new Vector2(face.x+face.width*(.1f+.2f*i),face.yMax-6-8*k),18+10*k,U.A(Smoke,.35f*(1-k)*a));}
                for(int i=0;i<3;i++)Drip(gc+new Vector2(-g*.1f+g*.1f*i,g*.1f),Mathf.Repeat(t*.6f+i*.33f,1.2f),3,Venom,.45f*a);
                float blink=Mathf.Repeat(t,4.2f)<.12f?.2f:1;Soft(new Vector2(face.x+face.width*.18f,face.y+face.height*.3f),3,U.A(Venom,.5f*blink*a));Soft(new Vector2(face.x+face.width*.26f,face.y+face.height*.3f),3,U.A(Venom,.5f*blink*a));
            }
            string glyph=katana?NativeKatanaChips.Glyph(id):NativeFangsChips.Glyph(id);
            if(glyph!=null)NativeIcons.DrawMarkup(gr,(katana?"katana-face:":"fangs-face:")+id,glyph,NativeKatanaChips.GlyphBox,U.A(tint,.22f*a),Mathf.CeilToInt(g));
            if(!calm&&index>=0)Scene(gr,katana,index,t,a);
            U.EndBand();
            if(calm)return;
            // A glint running round the edge like light along a drawn edge.
            float per=2*(r.width+r.height);Vector2 At(float d){d=Mathf.Repeat(d,per);float w=r.width-4,h=r.height-4,x0=r.x+2,y0=r.y+2;if(d<w)return new Vector2(x0+d,y0);d-=w;if(d<h)return new Vector2(x0+w,y0+d);d-=h;if(d<w)return new Vector2(x0+w-d,y0+h);d-=w;return new Vector2(x0,y0+h-Mathf.Min(h,d));}
            float lead=t*per*.3f;var prev=At(lead);for(int i=1;i<=10;i++){var q=At(lead-i*9);Streak(prev,q,3*(1-i/11f),U.A(tint,.7f*(1-i/11f)*a));prev=q;}
            Glint(At(lead),8,katana?Ivory:Lime,.9f*a,Mathf.Repeat(t*90,360));
        }
        // Each chip's own small loop on the card.
        private static void Scene(Rect g,bool katana,int index,float t,float a)
        {
            Vector2 C(float x,float y)=>new Vector2(g.x+g.width*x,g.y+g.height*y);float u=g.width/100;var tint=katana?Crimson:Venom;var hi=katana?Ivory:Lime;
            switch(index)
            {
                case 0:{float k=Mathf.Repeat(t/2.2f,1);if(k<.5f){float w=k/.5f;Glint(Vector2.Lerp(C(.14f,.8f),C(.86f,.2f),w),6*u,hi,Mathf.Sin(Mathf.PI*w)*.9f*a,-40);}break;}
                case 1:{float k=Mathf.Repeat(t/1.8f,1);Arc(C(.4f+.4f*k,.5f),(20+10*k)*u,-1,1,3*u*(1-k),U.A(tint,.6f*(1-k)*a),12,true);break;}
                case 2:{for(int i=0;i<4;i++){float ang=t*1.6f+i*Mathf.PI/2;Petal(C(.5f,.5f)+Polar(ang,32*u),5*u,ang*57,U.A(katana?Blossom:Lime,.6f*a));}break;}
                case 3:{float k=.5f+.5f*Mathf.Sin(t*1.8f);Soft(C(.3f,.3f),6*u*k,U.A(hi,.5f*a));Soft(C(.7f,.7f),6*u*(1-k),U.A(hi,.5f*a));break;}
                case 4:{float k=Mathf.Repeat(t/2.4f,1);Streak(C(.1f,.9f),Vector2.Lerp(C(.1f,.9f),C(.9f,.1f),Mathf.Clamp01(k*2)),1.6f,U.A(hi,.7f*(1-Ramp(k,.6f,1))*a));break;}
                case 5:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t*1.2f+i*.15f,1);Streak(C(.1f+.6f*k,.3f+.2f*i),C(.2f+.6f*k,.3f+.2f*i),2*u,U.A(hi,.5f*(1-k)*a));}break;}
                case 6:{for(int i=0;i<3;i++){float k=Mathf.Repeat(t*.5f+i*.33f,1);Ring(C(.5f,.75f),(4+18*k)*u,1,U.A(hi,.5f*(1-k)*a),20);}break;}
                case 7:{float k=.5f+.5f*Mathf.Sin(t*2.2f);Soft(C(.5f,.45f),18*u*k,U.A(tint,.2f*a));break;}
                case 8:{float k=Mathf.Repeat(t/2.6f,1);Petal(C(.5f,.1f+.7f*k),6*u,t*90,U.A(katana?Blossom:Lime,.7f*(1-Ramp(k,.8f,1))*a));break;}
                default:{float k=Mathf.Repeat(t/2,1);Glint(C(.5f+.3f*Mathf.Sin(k*Mathf.PI*2),.5f),5*u,hi,.8f*a,t*120);break;}
            }
        }

        // ---- the moment it goes in ----
        public const float Start=.55f;
        public static void Pose(string id,float t,bool calm,out Vector2 offset,out float sx,out float sy,out float degrees)
        {
            offset=Vector2.zero;sx=sy=1;degrees=0;if(calm)return;float s=t-Start;if(s<0)return;float shake=0;bool katana=NativeKatanaChips.Is(id);int i=katana?NativeKatanaChips.Index(id):NativeFangsChips.Index(id);
            // Each knocked in time with its own strikes (the pick cues' moments).
            var cues=katana?NativeKatanaChips.Picks[i<0?0:i]:NativeFangsChips.Picks[i<0?0:i];
            foreach(var c in cues){float w=s-c.At;if(w<0||w>.4f)continue;float k=Mathf.Exp(-w*10)*Mathf.Cos(w*22)*c.Gain;sx+=.02f*k;sy+=.02f*k;shake+=3*c.Gain*(1-w/.4f)*(c.Id.Contains("cut")||c.Id.Contains("split")||c.Id.Contains("clash")||c.Id.Contains("bloom")||c.Id.Contains("stab")?1:.3f);}
            if(katana&&(i==3||i==4)){float w=s-(i==3?.8f:1);if(w>=0&&w<.5f){offset+=new Vector2(0,6*Mathf.Exp(-w*8));}}
            if(!katana&&i==3){sx*=1-.04f*Bell(s,.2f,1.6f);sy=sx;}
            offset+=new Vector2(Mathf.Sin(t*71)*shake,Mathf.Sin(t*83+1)*shake);
        }
        public static void Reveal(Rect card,string id,float t,float env,bool calm)
        {
            float s=t-Start;if(env<=0||s<0)return;bool katana=NativeKatanaChips.Is(id);int index=katana?NativeKatanaChips.Index(id):NativeFangsChips.Index(id);if(index<0)return;float a=env;var c=card.center;float W=card.width,H=card.height;var tint=katana?Crimson:Venom;
            if(calm){float w=Mathf.Clamp01(s/.5f);Soft(c,W*.7f,U.A(tint,.22f*(1-w)*a));return;}
            Vector2 P(float x,float y)=>new Vector2(card.x+W*x,card.y+H*y);
            if(katana)switch(index)
            {
                case 0:{// Honed Edge: the whetstone's two strokes along the diagonal, sparks behind, then the edge's glint and a shine.
                    for(int k=0;k<2;k++){float w=s-(.05f+.35f*k);if(w<0||w>.4f)continue;float u=w/.4f;var p=Vector2.Lerp(P(.12f,.88f),P(.88f,.12f),u);Blob(p,W*.14f,W*.05f,U.A(SteelDark,a*(1-u*.3f)),-42);for(int j=0;j<6;j++){var d=Dir(11+k,j);Streak(p,p+d*W*.06f*(.5f+R(13,j)),1.4f,U.A(Ivory,(1-u)*a));}}
                    float g=s-.9f;if(g>=0&&g<.45f){float u=g/.45f;Streak(P(.1f,.9f),P(.9f,.1f),2.2f,U.A(Ivory,Mathf.Sin(Mathf.PI*u)*a));Glint(Vector2.Lerp(P(.1f,.9f),P(.9f,.1f),u),W*.06f,Color.white,a,-42);}
                    float sh=s-1.2f;if(sh>=0&&sh<.7f)Soft(c,W*.6f,U.A(Crimson,.25f*(1-sh/.7f)*a));break;}
                case 1:{// Crescent Wave: the blade sweeps across, its crescent flies off growing.
                    float w=s-.2f;if(w>=0&&w<.25f){float u=w/.25f;Katana(P(.15f,.75f),Vector2.Lerp(P(.25f,.35f),P(.85f,.4f),u),W*.025f,Crimson,a);}
                    float f=s-.34f;if(f>=0&&f<1.1f){float u=f/1.1f;var p=Vector2.Lerp(P(.55f,.5f),P(1.3f,.45f),Out(u));Arc(p,H*(.18f+.25f*u),-1.1f,1.1f,W*.04f*(1-u)+1,U.A(Crimson,(1-u)*a),18,true);Arc(p,H*(.18f+.25f*u),-1,1,2,U.A(Ivory,(1-u)*a),18,true);}
                    break;}
                case 2:{// Thousand Petals: a whirl of petals, four quick cuts, the last cut and the petals flung out.
                    int n=24;for(int k=0;k<n;k++){float ang=k*Mathf.PI*2/n+s*4,rad=W*(.18f+.22f*R(21,k))*(1-.4f*Ramp(s,.0f,.9f)+(s>.9f?2.5f*(s-.9f):0));Petal(c+Polar(ang,rad),W*.03f,ang*57,U.A(Blossom,(1-Ramp(s,1.4f,1.9f))*a));}
                    for(int k=0;k<4;k++){float w=s-(.3f+.12f*k);if(w<0||w>.2f)continue;float u=w/.2f;Streak(P(.1f+.05f*k,.2f+.18f*k),P(.9f-.05f*k,.35f+.15f*k),2*(1-u)+.5f,U.A(Ivory,(1-u)*a));}
                    float l=s-.9f;if(l>=0&&l<.35f){float u=l/.35f;Streak(P(.05f,.7f),P(.95f,.3f),4*(1-u)+1,U.A(Color.white,(1-u)*a));Burst(c,l,W*.3f,Crimson,a,23,16);}break;}
                case 3:{// Twin Moons: two moons rise; the line dashes across; it splits twice; the click.
                    float m=Ramp(s,0,.6f);Moon(P(.25f,.28f),W*.07f,Crimson,m*a);Moon(P(.75f,.72f),W*.07f,Crimson,m*a);
                    float d=s-.3f;if(d>=0&&d<1.2f){float u=Mathf.Clamp01(d/.12f);Streak(P(.08f,.62f),Vector2.Lerp(P(.08f,.62f),P(.92f,.38f),u),1.8f,U.A(Ivory,(1-Ramp(d,.9f,1.2f))*a));}
                    foreach(var at in new[]{.8f,1.12f}){float w=s-at;if(w<0||w>.4f)continue;float u=w/.4f;Streak(P(.08f,.62f),P(.92f,.38f),6*(1-u),U.A(Color.white,(1-u)*a));Burst(c,w,W*.22f,Blossom,a,31+(int)(at*10),12);}
                    float k2=s-1.4f;if(k2>=0&&k2<.4f)Ring(c,W*(.05f+.3f*Out(k2/.4f)),2.5f*(1-k2/.4f),U.A(Crimson,(1-k2/.4f)*a),28);break;}
                case 4:{// Severing Line: the line drawn, the hush (it trembles), the split (the halves part), the click.
                    float u=Mathf.Clamp01(s/.25f);Streak(P(.06f,.8f),Vector2.Lerp(P(.06f,.8f),P(.94f,.2f),u),1.4f+.8f*Mathf.Sin(s*40)*Ramp(s,.3f,1),U.A(Ivory,(1-Ramp(s,1.1f,1.4f))*a));
                    float w=s-1;if(w>=0&&w<.6f){float k=Mathf.Sin(Mathf.PI*Mathf.Clamp01(w/.6f));Soft(c,W*.5f*k,U.A(Color.white,.4f*k*a));Streak(P(.06f,.8f),P(.94f,.2f),5*k,U.A(Crimson,k*a));}
                    float cl=s-1.3f;if(cl>=0&&cl<.4f)Ring(c,W*(.04f+.25f*Out(cl/.4f)),2.4f*(1-cl/.4f),U.A(Ivory,(1-cl/.4f)*a),24);break;}
                case 5:{// Flash Step: three afterimages dash across, then the flash.
                    for(int k=0;k<3;k++){float w=s-k*.04f;if(w<0||w>.35f)continue;float u=w/.35f;var p=Vector2.Lerp(P(-.1f,.55f),P(1.1f,.5f),Out(u));Blob(p,W*.06f,H*.4f,U.A(k==0?Ivory:Crimson,(1-u)*(.9f-.25f*k)*a));}
                    float f=s-.5f;if(f>=0&&f<.4f)Soft(c,W*(.3f+.4f*f),U.A(Color.white,.6f*(1-f/.4f)*a));break;}
                case 6:{// Still Water: a drop, the ripples, the stillness, one cut.
                    float d=s-.1f;if(d>=0&&d<.4f)Blob(P(.5f,.1f+.55f*(d/.4f)*(d/.4f)),W*.04f,W*.06f,U.A(Ivory,a));
                    for(int k=0;k<3;k++){float w=s-(.5f+.2f*k);if(w<0||w>1)continue;float u=w/1;Ring(P(.5f,.7f),W*(.03f+.4f*Out(u)),1.6f*(1-u)+.4f,U.A(Ivory,(1-u)*.8f*a),30);}
                    float cut=s-1.4f;if(cut>=0&&cut<.3f){float u=cut/.3f;Streak(P(.05f,.45f),P(.95f,.4f),3*(1-u)+.6f,U.A(Color.white,(1-u)*a));}break;}
                case 7:{// Mirror Moon: two blades clash, the moon rises, the counter.
                    float cl=s-.1f;if(cl>=0&&cl<.5f){Katana(P(.1f,.85f),P(.55f,.45f),W*.02f,Crimson,(1-Ramp(cl,.3f,.5f))*a);Katana(P(.9f,.85f),P(.45f,.45f),W*.02f,Crimson,(1-Ramp(cl,.3f,.5f))*a);Burst(P(.5f,.45f),cl,W*.2f,Ivory,a,41,14);}
                    float m=Ramp(s,.5f,.8f)*(1-Ramp(s,1.6f,2));Moon(P(.5f,.4f),W*.12f,Crimson,m*a);
                    float c2=s-.9f;if(c2>=0&&c2<.35f){float u=c2/.35f;Streak(P(.15f,.7f),P(.85f,.25f),4*(1-u)+.8f,U.A(Color.white,(1-u)*a));}break;}
                case 8:{// Falling Blossom: the rising arc, a blossom falling, the cut that scatters it.
                    float r=s;if(r<.3f)Arc(P(.5f,1.2f),H*.8f,-2.4f,-.7f,3*(1-r/.3f),U.A(Crimson,(1-r/.3f)*a),18,true);
                    if(s>=.2f&&s<1.1f){float u=Ramp(s,.2f,1.1f);Petal(P(.5f+.05f*Mathf.Sin(s*5),.1f+.5f*u),W*.06f,s*80,U.A(Blossom,a));}
                    float cut=s-1.1f;if(cut>=0&&cut<.6f){Streak(P(.2f,.65f),P(.8f,.55f),3*(1-cut/.6f),U.A(Color.white,(1-cut/.6f)*a));for(int k=0;k<10;k++){var d=Dir(51,k);Petal(P(.5f,.6f)+d*W*.3f*Out(cut/.6f),W*.025f,k*36+cut*200,U.A(Blossom,(1-cut/.6f)*a));}}break;}
                default:{// Sheathed Fury: the blade slides home, the click, the draw's flash.
                    float sl=Mathf.Clamp01(s/.35f);Streak(P(.15f,.8f),P(.85f,.3f),W*.05f,U.A(Ink,a*(1-Ramp(s,1.2f,1.6f))));Katana(Vector2.Lerp(P(.0f,.9f),P(.2f,.75f),sl),Vector2.Lerp(P(.55f,.5f),P(.85f,.3f),sl),W*.02f,Crimson,a*(1-Ramp(s,.7f,.8f)));
                    float cl=s-.35f;if(cl>=0&&cl<.35f)Ring(P(.2f,.75f),W*(.03f+.15f*Out(cl/.35f)),2*(1-cl/.35f),U.A(Ivory,(1-cl/.35f)*a),20);
                    float dr=s-.75f;if(dr>=0&&dr<.35f){float u=dr/.35f;Streak(P(.05f,.6f),P(.95f,.35f),6*(1-u),U.A(Color.white,(1-u)*a));Burst(c,dr,W*.25f,Crimson,a,61,14);}break;}
            }
            else switch(index)
            {
                case 0:{// Neurotoxin: three drips into a vial, its bubbling, the hiss.
                    Fang(P(.5f,.05f),P(.5f,.3f),W*.05f,Venom,a*(1-Ramp(s,1.5f,1.9f)));for(int k=0;k<3;k++)Drip(P(.5f,.3f),s-(.1f+.3f*k),W*.025f,Venom,a);
                    Blob(P(.5f,.78f),W*.16f,H*.2f,U.A(Smoke,.8f*a));Blob(P(.5f,.8f),W*.13f,H*.12f*Ramp(s,.2f,1),U.A(Venom,.7f*a));
                    for(int k=0;k<5;k++){float w=Mathf.Repeat(s*1.4f+k*.2f,1);Soft(P(.46f+.02f*k,.84f-.1f*w),2.5f,U.A(Lime,(1-w)*Ramp(s,.5f,.8f)*a));}
                    float h=s-1;if(h>=0&&h<.7f)Soft(P(.5f,.65f),W*(.15f+.4f*h),U.A(Venom,.3f*(1-h/.7f)*a));break;}
                case 1:{// Hydra: one knife flies in and splits into three heads that fan out.
                    float f=s;var tip=Vector2.Lerp(P(-.1f,.9f),P(.45f,.5f),Mathf.Clamp01(f/.45f));if(f<.45f)Fang(tip-new Vector2(W*.15f,-W*.1f),tip,W*.03f,Venom,a);
                    float sp=s-.45f;if(sp>=0&&sp<1){for(int k=-1;k<=1;k++){var dir=Polar(-.6f+k*.45f,1);var p=P(.45f,.5f)+dir*W*.55f*Out(sp/.6f);Fang(p-dir*W*.13f,p,W*.03f,Venom,a*(1-Ramp(sp,.7f,1)));}Burst(P(.45f,.5f),sp,W*.18f,Venom,a,71,12);}break;}
                case 2:{// Long Shadow: the shadow stretches across the floor; two vanishes; a cut.
                    float st=Ramp(s,0,.6f);Blob(Vector2.Lerp(P(.2f,.85f),P(.55f,.85f),st*.5f),W*(.1f+.7f*st),H*.06f,U.A(Ink,.8f*a*(1-Ramp(s,1.4f,1.8f))));
                    foreach(var at in new[]{.6f,1f}){float w=s-at;if(w<0||w>.6f)continue;var p=at<.8f?P(.25f,.6f):P(.75f,.6f);for(int k=0;k<8;k++){var d=Dir(81+(int)(at*10),k);Soft(p+d*W*.12f*Out(w/.6f),W*.06f,U.A(Smoke,.7f*(1-w/.6f)*a));}Soft(p,W*.08f,U.A(Venom,.5f*(1-w/.6f)*a));}
                    float c2=s-1.1f;if(c2>=0&&c2<.3f)Streak(P(.6f,.3f),P(.95f,.7f),3*(1-c2/.3f),U.A(Lime,(1-c2/.3f)*a));break;}
                case 3:{// Silent Veil: smoke billows over the card, a hush, then it clears.
                    for(int k=0;k<14;k++){var d=Dir(91,k);float grow=Out(Ramp(s,0,.8f));Soft(c+d*W*.4f*grow*(.4f+.6f*R(93,k)),W*(.12f+.1f*grow),U.A(Smoke,.75f*(1-Ramp(s,1.4f,2))*a));}
                    float ch=s-1.5f;if(ch>=0&&ch<.4f)Glint(c,W*.08f,Lime,(1-ch/.4f)*a);break;}
                case 4:{// Chain Bloom: a burst, venom leaping on, more bursts.
                    Burst(P(.2f,.6f),s-.1f,W*.2f,Venom,a,101,14);Vector2[] to={P(.6f,.25f),P(.8f,.65f),P(.55f,.85f)};
                    for(int k=0;k<3;k++){float w=s-(.45f+.04f*k);if(w>=0&&w<.25f){var p=Vector2.Lerp(P(.2f,.6f),to[k],w/.25f);Streak(P(.2f,.6f),p,2,U.A(Lime,a));}Burst(to[k],s-(.7f+.12f*k),W*.15f,Venom,a,103+k,10);}break;}
                case 5:{// Bloodletting: drips, a heart beating twice, a stream drawn into the emblem.
                    for(int k=0;k<2;k++)Drip(P(.4f+.2f*k,.2f),s-.1f-.1f*k,W*.02f,Blood,a);
                    foreach(var at in new[]{.5f,.95f}){float w=s-at;if(w<0||w>.4f)continue;float k=Mathf.Exp(-w*8);Blob(c,W*(.14f+.05f*k),W*(.13f+.05f*k),U.A(Blood,.6f*k*a));}
                    float st=s-1.3f;if(st>=0&&st<.5f)for(int k=0;k<6;k++){float u=Mathf.Clamp01(st/.5f*1.3f-k*.05f);Soft(Vector2.Lerp(P(.1f,.9f),P(.72f,.62f),u),3,U.A(Blood,(1-st/.5f)*a));}break;}
                case 6:{// Predator: darkness falls, two eyes open, the strike's three claws.
                    float dk=Ramp(s,0,.6f)*(1-Ramp(s,1.6f,2));U.Fill(card,U.A(Ink,.45f*dk*a));
                    float e=Ramp(s,.6f,.7f)*(1-Ramp(s,1.5f,1.8f));Blob(P(.38f,.35f),W*.07f,W*.03f,U.A(Venom,e*a));Blob(P(.62f,.35f),W*.07f,W*.03f,U.A(Venom,e*a));
                    float st=s-1.1f;if(st>=0&&st<.35f)for(int k=0;k<3;k++)Streak(P(.25f+.15f*k,.45f),P(.4f+.15f*k,.9f),3*(1-st/.35f),U.A(Lime,(1-st/.35f)*a));break;}
                case 7:{// Death Mark: a crosshair closes in, the mark burns, the toll.
                    float cl=Out(Ramp(s,0,.5f));Ring(c,W*(.6f-.4f*cl),2.4f,U.A(Venom,(1-Ramp(s,1.6f,2))*a),32);for(int k=0;k<4;k++){var d=Polar(k*Mathf.PI/2,1);Streak(c+d*W*(.32f-.12f*cl),c+d*W*(.18f-.05f*cl),2.4f,U.A(Venom,(1-Ramp(s,1.6f,2))*a));}
                    foreach(var at in new[]{.5f,1.3f}){float w=s-at;if(w<0||w>.6f)continue;Soft(c,W*(.12f+.2f*w),U.A(Blood,.7f*(1-w/.6f)*a));Ring(c,W*(.05f+.3f*Out(w/.6f)),2*(1-w/.6f),U.A(Lime,(1-w/.6f)*a),26);}break;}
                case 8:{// Smokescreen: the bomb, the billow, the swirl.
                    float b=Mathf.Clamp01(s/.3f);if(s<.3f)Blob(Vector2.Lerp(P(.1f,.1f),P(.45f,.7f),b),W*.06f,W*.06f,U.A(SteelDark,a));
                    float bl=s-.3f;if(bl>=0)for(int k=0;k<16;k++){var d=Dir(121,k);float ang=bl*1.5f+k;var p=P(.45f,.7f)+Polar(ang,W*.45f*Out(Mathf.Clamp01(bl/.6f))*(.3f+.7f*R(123,k)));Soft(p,W*.14f,U.A(Smoke,.7f*(1-Ramp(bl,1.2f,1.7f))*a));}break;}
                default:{// Twin Venom: two fangs cross ringing, two drips, the hiss.
                    float x=Out(Ramp(s,0,.3f));Fang(P(.1f,.15f),Vector2.Lerp(P(.1f,.15f),P(.8f,.85f),x),W*.04f,Venom,a*(1-Ramp(s,1.6f,2)));Fang(P(.9f,.15f),Vector2.Lerp(P(.9f,.15f),P(.2f,.85f),x),W*.04f,Venom,a*(1-Ramp(s,1.6f,2)));
                    Burst(c,s-.25f,W*.18f,Lime,a,131,12);Drip(P(.2f,.85f),s-.8f,W*.02f,Venom,a);Drip(P(.8f,.85f),s-.95f,W*.02f,Venom,a);
                    float h=s-1.3f;if(h>=0&&h<.6f)Soft(c,W*(.2f+.3f*h),U.A(Venom,.25f*(1-h/.6f)*a));break;}
            }
            float end=s-2.2f;if(end>=0&&end<.6f)Soft(c,W*.65f,U.A(tint,.12f*(1-end/.6f)*a));
        }
    }
}
