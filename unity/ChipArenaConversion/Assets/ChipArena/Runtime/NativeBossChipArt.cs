using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The boss relics on the screen (Core NativeBossChips; docs/BOSS-CHIPS.md). Face: each boss's own living scene on the card, behind its text: the Siren's song in rings, THE 6:47's departure board, THE CONVERTER's
    // pour, THE HUM's fixture buzzing over its wallpaper, THE DEEP END's tile under moving light, NOCTIEL's feather falling through the dark. Reveal: the pick, over the INSTALLED card, the boss arriving for a moment.
    // Pure functions of the clock, allocation free; Reduced keeps a single soft glow.
    public static class NativeBossChipArt
    {
        private static float R(int seed,int i){float x=Mathf.Sin(seed*12.9898f+i*78.233f)*43758.5453f;return x-Mathf.Floor(x);}
        private static float Out(float x){x=Mathf.Clamp01(x);float o=1-x;return 1-o*o*o;}
        private static float Ramp(float x,float a,float b)=>Mathf.Clamp01((x-a)/Mathf.Max(1e-4f,b-a));
        private static float Bump(float x,float a,float b,float c)=>x<a||x>c?0:x<b?Ramp(x,a,b):1-Ramp(x,b,c);
        private static void Streak(Vector2 a,Vector2 b,float w,Color c)
        {
            var d=b-a;float len=d.magnitude;if(len<.05f||c.a<=0)return;var m=(a+b)*.5f;U.Push(m,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(m.x-len/2,m.y-w/2,len,w),c);U.Pop();
        }
        private static void Soft(Vector2 p,float r,Color c){if(r>0&&c.a>0)U.Radial(new Rect(p.x-r,p.y-r,2*r,2*r),c);}
        private static void Ring(Vector2 p,float r,float w,Color c,int seg=30)
        {
            if(c.a<=0||r<=0)return;Vector2 last=p+new Vector2(r,0);for(int i=1;i<=seg;i++){float a=i*Mathf.PI*2/seg;var q=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;Streak(last,q,w,c);last=q;}
        }
        private static Color Hex(int rgb,float a)=>U.Hex(rgb,a);
        // THE 6:47's board: 5 x 7 dot glyphs (each row a bit mask, the left column the high bit).
        private static readonly int[][] Glyphs={new[]{0x0e,0x10,0x10,0x1e,0x11,0x11,0x0e},new[]{0,0x04,0x04,0,0x04,0x04,0},new[]{0x02,0x06,0x0a,0x12,0x1f,0x02,0x02},new[]{0x1f,0x01,0x02,0x04,0x08,0x08,0x08}};
        private static readonly int[] Board={0,1,2,3};
        private static void Dots(Vector2 at,float dot,float a,float flick,Color on,Color off)
        {
            float x=at.x;for(int g=0;g<Board.Length;g++){var rows=Glyphs[Board[g]];const int cols=5;for(int r=0;r<7;r++)for(int c=0;c<cols;c++){bool lit=(rows[r]>>(4-c)&1)!=0;var d=new Rect(x+c*dot*1.25f,at.y+r*dot*1.25f,dot,dot);U.Fill(d,lit?U.A(on,a*flick):U.A(off,a*.5f));if(lit)U.Glow(d,U.A(on,.35f*a*flick),dot*.9f);}x+=(cols+1)*dot*1.25f;}
        }
        // A feather: a curved quill with its vanes, `len` px, turned `deg`.
        private static void Feather(Vector2 at,float len,float deg,Color c)
        {
            U.Push(at,Vector2.zero,1,deg);Vector2 Quill(float u)=>at+new Vector2(Mathf.Sin(u*1.4f)*len*.08f,(u-.5f)*len);
            for(int i=0;i<12;i++){float u=i/12f,v=(i+1)/12f;Streak(Quill(u),Quill(v),1.2f,c);}
            for(int i=1;i<16;i++){float u=.12f+.8f*i/16f;var q=Quill(u);float w=len*.2f*Mathf.Sin(u*Mathf.PI)*(.85f+.15f*Mathf.Sin(i*2.1f));Streak(q,q+new Vector2(-w,-w*.55f),1,U.A(c,c.a*.8f));Streak(q,q+new Vector2(w*.9f,-w*.5f),1,U.A(c,c.a*.8f));}
            U.Pop();
        }
        // ---- the card ----
        public static void Face(Rect r,float radius,string id,float t,float a,bool plain,bool calm)
        {
            if(plain||a<=0)return;int k=NativeBossChips.Find(id);if(k<0)return;int set=NativeBossChips.Relics[k].Set;var s=NativeBossChips.Sets[set];
            float bw=NativeChipArt.BezelOf(radius),tc=NativeChipArt.TopCut(radius),bc=NativeChipArt.BottomCut(radius);var face=Rect.MinMaxRect(r.x+bw,r.y+tc*.55f,r.xMax-bw,r.yMax-bc*.55f);
            var tone=U.Hex(s.Rarity);var light=U.Hex(s.Label);float W=face.width,H=face.height;var right=new Vector2(face.xMax-W*.2f,face.yMax-H*.3f);
            U.BeginBand(face);
            switch(set)
            {
                case 0:// The Siren: her song leaving in rings from the corner; a dark undertone.
                    Soft(right,W*.55f,U.A(U.Hex(0x1b0f3a),.75f*a));
                    if(!calm)for(int i=0;i<5;i++){float u=Mathf.Repeat(t*.32f+i*.2f,1);Ring(right,W*(.06f+.55f*u),1.3f*(1-u)+.4f,U.A(light,.42f*(1-u)*a),40);}
                    Soft(right,W*.12f,U.A(tone,.35f*a));break;
                case 1:{// THE 6:47: the departure board, its time burning amber, a line of text running under it.
                    float bwide=Mathf.Min(W*.26f,84);var board=new Rect(face.xMax-bwide-W*.04f,face.y+H*.1f,bwide,Mathf.Min(H*.16f,bwide*.42f));U.Fill(board,U.A(U.Hex(0x0a0806),.85f*a));U.Fill(new Rect(board.x,board.y,board.width,1),U.A(tone,.25f*a));
                    float dot=Mathf.Clamp(board.width/30f,1,3.2f);float flick=calm?1:.85f+.15f*Mathf.Sin(t*31f)*Mathf.Sin(t*7.7f);Dots(new Vector2(board.x+dot*1.4f,board.y+dot*1.2f),dot,.6f*a,flick,tone,U.Hex(0x2a1c08));
                    if(!calm){float sx=Mathf.Repeat(t*28,board.width+40)-20;for(int i=0;i<10;i++){float x=board.x+sx+i*dot*2.4f;if(x<board.xMax-dot&&x>board.x)U.Fill(new Rect(x,board.yMax-dot*2.2f,dot*1.2f,dot),U.A(tone,.5f*a));}}
                    Soft(board.center,board.width*.7f,U.A(tone,.08f*a));break;}
                case 2:{// THE CONVERTER: the pour, a white-hot thread into a glowing pool; slag cracks; sparks going up.
                    var top=new Vector2(face.xMax-W*.07f,face.y);var pool=new Vector2(face.xMax-W*.09f,face.yMax-H*.1f);float sway=calm?0:Mathf.Sin(t*2.3f)*2;
                    Soft(pool,W*.42f,U.A(U.Hex(0x4a0e00),.7f*a));Soft(pool,W*.2f,U.A(tone,.32f*a));Streak(top+new Vector2(sway,0),pool,3.2f,U.A(U.Hex(0xffd2a0),.45f*a));Streak(top+new Vector2(sway,0),pool,1.2f,U.A(Color.white,.5f*a));
                    for(int i=0;i<6;i++){var c0=pool+new Vector2((R(7,i)-.5f)*W*.6f,(R(8,i)-.6f)*H*.4f);Streak(c0,c0+new Vector2((R(9,i)-.5f)*W*.2f,(R(10,i)-.5f)*H*.2f),1,U.A(tone,.3f*a));}
                    if(!calm)for(int i=0;i<10;i++){float u=Mathf.Repeat(t*.55f+R(11,i),1);var p=pool+new Vector2((R(12,i)-.5f)*W*.3f+Mathf.Sin(u*6+i)*4,-u*H*.85f);U.Fill(new Rect(p.x,p.y,1.6f,1.6f),U.A(light,(1-u)*.8f*a));}
                    break;}
                case 3:{// THE HUM: Level 0's wallpaper, a fixture across the top buzzing its light down the card; now and then it stutters.
                    for(float x=face.x-H;x<face.xMax;x+=9){Streak(new Vector2(x,face.yMax),new Vector2(x+H*.5f,face.y+H*.5f),1,U.A(U.Hex(0x6b6230),.25f*a));Streak(new Vector2(x+H*.5f,face.y+H*.5f),new Vector2(x+H,face.yMax),1,U.A(U.Hex(0x6b6230),.25f*a));}
                    int beat=(int)(t*10);float stutter=calm?1:(R(beat,3)<.08f?.15f:R(beat,4)<.15f?.6f:1);
                    float fwide=Mathf.Min(W*.21f,66);var bar=new Rect(face.xMax-fwide-W*.06f,face.y+H*.17f,fwide,Mathf.Max(2.5f,H*.035f));U.Fill(new Rect(bar.x-2,bar.y-2,bar.width+4,bar.height+4),U.A(U.Hex(0x2a2716),.9f*a));U.Fill(bar,U.A(Color.Lerp(U.Hex(0x3a3820),U.Hex(0xfffbe8),stutter),.75f*a));
                    U.Glow(bar,U.A(light,.45f*stutter*a),10);Soft(bar.center,W*.22f,U.A(light,.1f*stutter*a));
                    break;}
                case 4:{// THE DEEP END: white tile with teal grout, light moving across it as if through water, one round pool lamp.
                    float cell=Mathf.Max(5,W/20f);for(float x=face.x;x<face.xMax;x+=cell)U.Fill(new Rect(x,face.y,1,H),U.A(U.Hex(0x2a8c88),.18f*a));for(float y=face.y;y<face.yMax;y+=cell)U.Fill(new Rect(face.x,y,W,1),U.A(U.Hex(0x2a8c88),.18f*a));
                    if(!calm)for(int i=0;i<7;i++){float ph=t*(.6f+.15f*i)+i*1.7f;Vector2 last=default;for(int j=0;j<=16;j++){float u=j/16f;var p=new Vector2(face.x+u*W,face.y+H*(.15f+.12f*i)+Mathf.Sin(u*5+ph)*H*.06f+Mathf.Sin(u*11-ph*1.3f)*H*.03f);if(j>0)Streak(last,p,1.4f,U.A(light,.16f*a));last=p;}}
                    var lamp=new Vector2(face.xMax-W*.11f,face.yMax-H*.15f);float lr=Mathf.Min(W,H)*.085f;Soft(lamp,lr*4,U.A(tone,.22f*a));Ring(lamp,lr,1.6f,U.A(U.Hex(0xdfe6e8),.8f*a),28);Soft(lamp,lr*.9f,U.A(light,.85f*a));break;}
                case 5:{// NOCTIEL: the dark, motes of light going down through it, the feather falling like a leaf; a faint halo behind.
                    Soft(right,W*.6f,U.A(U.Hex(0x050508),.85f*a));Ring(new Vector2(face.xMax-W*.24f,face.y+H*.32f),W*.16f,1,U.A(light,.18f*a),36);
                    if(!calm)for(int i=0;i<12;i++){float u=Mathf.Repeat(t*.08f+R(21,i),1);var p=new Vector2(face.x+W*(.4f+.6f*R(22,i))+Mathf.Sin(u*5+i)*3,face.y+u*H);U.Fill(new Rect(p.x,p.y,1.3f,1.3f),U.A(light,.6f*Mathf.Sin(u*Mathf.PI)*a));}
                    float fall=calm?.4f:Mathf.Repeat(t*.11f,1);var fp=new Vector2(face.xMax-W*.1f+Mathf.Sin(fall*9)*W*.04f,face.y+H*(.12f+.76f*fall));Feather(fp,H*.32f,Mathf.Sin(fall*9+1)*28,U.A(light,.5f*a));break;}
            }
            U.EndBand();
        }
        public static float RevealSeconds=>1.7f;
        // ---- the pick: over the INSTALLED card, `t` s into the spotlight, under its envelope ----
        public static void Reveal(Rect card,string id,float t,float env,bool calm)
        {
            int k=NativeBossChips.Find(id);if(k<0||env<=0)return;int set=NativeBossChips.Relics[k].Set;var s=NativeBossChips.Sets[set];var tone=U.Hex(s.Rarity);var light=U.Hex(s.Label);
            float st=t-.18f;if(st<0)return;var c=card.center;float W=card.width,H=card.height,a=env,SW=U.W,SH=U.H;
            if(calm){Soft(c,W*.8f,U.A(tone,.25f*(1-Mathf.Clamp01(st/.8f))*a));return;}
            switch(set)
            {
                case 0:// Her song through the room: rings rolling out from the card across the screen, the picture split as they pass.
                    for(int i=0;i<4;i++){float u=Ramp(st,i*.16f,i*.16f+1.2f);if(u<=0||u>=1)continue;Ring(c,W*.3f+SW*.8f*Out(u),3*(1-u)+.6f,U.A(light,.55f*(1-u)*a),56);}
                    if(st<.5f){float u=st/.5f;U.Fill(new Rect(0,c.y-H*.6f+H*1.2f*u,SW,2),U.A(U.Hex(0xff5a8a),.4f*(1-u)*a));U.Fill(new Rect(0,c.y-H*.55f+H*1.2f*u,SW,2),U.A(U.Hex(0x5affff),.4f*(1-u)*a));}
                    Soft(c,W*.9f,U.A(tone,.3f*Bump(st,0,.15f,1)*a));break;
                case 1:{// NOW DEPARTING: a headlight sweeping across, the board's rows of amber flipping down the card.
                    float u=Ramp(st,0,.9f);float hx=-SW*.2f+SW*1.4f*Out(u);Soft(new Vector2(hx,c.y),SH*.5f,U.A(U.Hex(0xfff0c4),.35f*(1-u)*a));Streak(new Vector2(hx-SW*.3f,c.y),new Vector2(hx,c.y),SH*.04f,U.A(U.Hex(0xfff0c4),.25f*(1-u)*a));
                    int rows=9;for(int i=0;i<rows;i++){float f=Ramp(st,.15f+i*.06f,.3f+i*.06f);float on=f>0&&f<1?1:0;if(on>0){var row=new Rect(card.x+8,card.y+H*(i+.5f)/rows-2,W-16,4);for(float x=row.x;x<row.xMax;x+=6)U.Fill(new Rect(x,row.y,4,4),U.A(tone,.75f*a));}}
                    break;}
                case 2:{// It pours: a white flash, molten sparks thrown up off the card and falling, its edge glowing hot and cooling.
                    if(st<.12f)Soft(c,W*1.2f,U.A(U.Hex(0xffe0b0),(1-st/.12f)*.6f*a));
                    for(int i=0;i<36;i++){float u=Ramp(st,R(31,i)*.25f,R(31,i)*.25f+.95f);if(u<=0||u>=1)continue;var v=new Vector2((R(32,i)-.5f)*W*1.6f,-(.5f+R(33,i))*H*1.4f);var p=new Vector2(c.x,card.y+H*.2f)+v*u+new Vector2(0,H*1.6f*u*u);Streak(p,p-v*.05f,1.6f,U.A(Color.Lerp(Color.white,tone,u),(1-u)*a));}
                    U.Glow(card,U.A(tone,.6f*(1-Ramp(st,.1f,1.4f))*a),26);break;}
                case 3:{// The lights: the screen drops dark for a beat (silence), then the room's fixtures slam on row by row across it, buzzing.
                    float dark=Bump(st,0,.08f,.42f);U.Fill(new Rect(0,0,SW,SH),new Color(0,0,0,.6f*dark*a));
                    for(int i=0;i<5;i++){float on=Ramp(st,.34f+i*.07f,.36f+i*.07f)*(1-Ramp(st,1.1f,1.6f));if(on<=0)continue;float y=SH*(.12f+.19f*i);float flick=R((int)(st*30),i)<.2f?.4f:1;for(int j=0;j<5;j++){var bar=new Rect(SW*(.08f+.2f*j),y,SW*.1f,4);U.Fill(bar,U.A(U.Hex(0xfffbe8),.8f*on*flick*a));U.Glow(bar,U.A(light,.35f*on*flick*a),16);}}
                    Soft(c,W*.8f,U.A(light,.3f*Ramp(st,.34f,.4f)*(1-Ramp(st,.4f,1.4f))*a));break;}
                case 4:{// The water: a drop on the card, rings across the screen, a splash, the light coming up teal.
                    for(int i=0;i<5;i++){float u=Ramp(st,i*.12f,i*.12f+1.3f);if(u<=0||u>=1)continue;U.Push(c,Vector2.zero,1,0);Ring(c,W*.1f+SW*.7f*Out(u),2.2f*(1-u)+.5f,U.A(light,.5f*(1-u)*a),56);U.Pop();}
                    for(int i=0;i<24;i++){float u=Ramp(st,.05f,.05f+.6f+R(41,i)*.3f);if(u<=0||u>=1)continue;var v=new Vector2((R(42,i)-.5f)*W,-(.4f+R(43,i))*H*1.2f);var p=c+v*u+new Vector2(0,H*1.4f*u*u);U.Fill(new Rect(p.x,p.y,2.2f,2.2f),U.A(light,(1-u)*.9f*a));}
                    Soft(c,W,U.A(tone,.25f*Bump(st,0,.2f,1.2f)*a));break;}
                case 5:{// The light falls: a beam down onto the card, its halo opening, feathers coming down through it.
                    float beam=Bump(st,0,.18f,1.4f);U.Fill(new Rect(c.x-W*.18f,0,W*.36f,card.y+H*.2f),U.A(light,.18f*beam*a));Soft(new Vector2(c.x,card.y),W*.5f,U.A(light,.5f*beam*a));
                    float hu=Ramp(st,.1f,1.2f);Ring(new Vector2(c.x,card.y-H*.05f),W*.25f+W*.35f*Out(hu),2*(1-hu)+.6f,U.A(light,.7f*(1-hu)*a),48);
                    for(int i=0;i<7;i++){float u=Ramp(st,R(51,i)*.4f,R(51,i)*.4f+1.6f);if(u<=0||u>=1)continue;var p=new Vector2(c.x+(R(52,i)-.5f)*W*1.6f+Mathf.Sin(u*7+i)*12,card.y-H*.4f+u*H*1.5f);Feather(p,H*.18f,Mathf.Sin(u*7+i)*35,U.A(light,.75f*Mathf.Sin(u*Mathf.PI)*a));}
                    break;}
            }
        }
        /// <summary>The 3D-less entrance of a relic's card in a tray: it falls in from above, too bright, and settles.</summary>
        public static void Entrance(float e,ref NativeChipCard.Reveal v){v.Dy=-46*(1-e);v.Scale=1.12f-.12f*e;v.Bright*=1+1.6f*(1-Mathf.Clamp01(e/.4f));v.Alpha=Mathf.Clamp01(e*2.5f);}
    }
}
