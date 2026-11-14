using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;

namespace ChipArena.UnityConversion
{
    // The Top Dog on the HUD (docs/TOP-DOG.md): its cards (the mallet's combo, Q, E, 1-5 and All You Can Eat on 6), the comic words over each
    // hit, the cut-in for the one who casts the feast, the victim's "STUFFED!", everyone else's banner, and cream over the eyes of anyone pied.
    // Presentation only.
    public sealed partial class ConversionGame
    {
        private void DogCards(NativePilot p)
        {
            var k=NativeDogView.Source?.Of(p.Seat.id);float t=arena.Time;
            for(int i=0;i<NativeTopDog.Slots;i++)
            {
                string id=NativeTopDog.Slot[i];float ready=k?.Ready!=null&&i<k.Ready.Length?k.Ready[i]:0,left=ready-t;bool busy=k?.CastKind==id&&t>=k.CastAt&&t<k.CastUntil;float cd=NativeTopDog.Cooldown[i]*(float)(p.Stats?["abilityCooldown"]??1);
                string key=NativeTopDog.SlotKeys[i]+" · "+NativeTopDog.AbilityName(id).ToUpperInvariant();if(id==NativeTopDog.Peel&&k!=null&&k.Peels>1)key+=" ×"+k.Peels;if(id==NativeTopDog.Boing&&k!=null&&k.Bounce)key+=" ×2";
                cards.Add(new Card{Key=key,State=busy?"ACTIVE":left>0?left.ToString("0.0")+"s":"READY",Icon=NativeTopDog.Icon(id),Ready=!busy&&left<=0,Active=busy,Dial=left>0?1-Mathf.Clamp01(left/Mathf.Max(.1f,cd)):1});
            }
            float meter=k?.Meter??0;bool full=meter>=1,feast=k?.Feast!=null&&t>=k.Feast.At&&t<k.Feast.Until;
            cards.Add(new Card{Key=NativeTopDog.UltimateKey+" · "+NativeTopDog.AbilityName(NativeTopDog.Feast).ToUpperInvariant(),State=feast?"ACTIVE":full?"READY":Mathf.FloorToInt(meter*100)+"%",Icon=NativeTopDog.Icon(NativeTopDog.Feast),Ready=full&&!feast,Active=feast,Dial=meter});
        }
        /// <summary>Drawn with the HUD: the comic words, the feast's cut-in and banner, the victim's line, the cream of a pie.</summary>
        private void DogScreen(float width,float height)
        {
            if(arena==null||Event.current.type!=EventType.Repaint||NativeDogView.Source==null)return;float t=arena.Time;var local=arena.Local;bool reduced=prefs.reducedEffects;
            foreach(var p in arena.Pilots)
            {
                if(p==null)continue;var k=NativeDogView.Source.Of(p.Seat.id);if(k==null)continue;
                if(k.Hits!=null&&cameraView!=null)foreach(var h in k.Hits)if(h!=null)DogWord(h,t,height,reduced);
                var f=k.Feast;
                if(f!=null&&t>=f.At&&t<f.Until)
                {
                    if(p==local)DogCutIn(f,t,width,height,reduced);
                    else if(local!=null&&f.Victim==local.Seat.id&&f.Catch>-50)DogVictimLine(f,t,width,height,reduced);
                    else DogBanner(p,f,t,width,height);
                }
                if(local!=null&&k.Marks!=null)foreach(var m in k.Marks)if(m!=null&&m.Id==local.Seat.id&&m.Kind==NativeTopDog.Pied&&t>=m.At&&t<m.Until)DogCream(m,t,width,height,reduced);
            }
        }
        // ---- the comic words ----
        private static string DogWordOf(string kind)
        {
            switch(kind){case NativeTopDog.HitBonk:return "BONK!";case NativeTopDog.HitSlam:return "WHAM!";case NativeTopDog.HitClang:return "KA-THUNK!";case NativeTopDog.HitThud:return "THUD!";case NativeTopDog.HitQuake:return "KA-BLAM!";case NativeTopDog.HitDash:return "ZOOM!";case NativeTopDog.HitSwat:return "WHAP!";
                case NativeTopDog.HitAnvil:return "CLANG!";case NativeTopDog.HitSlip:return "WHOOPS!";case NativeTopDog.HitPow:return "POW!";case NativeTopDog.HitPie:return "SPLAT!";case NativeTopDog.HitGulp:return "GULP!";
                case NativeTopDog.HitPop:return "KA-POP!";case NativeTopDog.HitSnap:return "SNAP!";case NativeTopDog.HitBoing:return "BOING!";case NativeTopDog.HitPiano:return "PLUNK!";case NativeTopDog.HitWall:return "SPLAT!";case NativeTopDog.HitMunch:return "MUNCH!";case NativeTopDog.HitToonFlat:return "SPLUT!";case NativeTopDog.HitToonPop:return "TA-DA!";case NativeTopDog.Zip:return "ZOOM!";default:return null;}
        }
        private static readonly Color[] DogInks={new Color(1,.86f,.2f),new Color(1,.38f,.25f),new Color(.45f,.85f,1),new Color(1,1,1),new Color(.6f,1,.45f)};
        private void DogWord(NativeDogHit h,float t,float height,bool reduced)
        {
            string word=DogWordOf(h.Kind);if(word==null)return;float age=t-h.At,life=h.Kind==NativeTopDog.HitPop?1.2f:.75f;if(age<0||age>life)return;
            var world=new Vector3(h.Pos.x,h.Pos.y+.55f+.6f*age,h.Pos.z);if(Vector3.Distance(cameraView.transform.position,world)>45)return;
            var at=cameraView.WorldToScreenPoint(world)/U.S;if(at.z<=.1f)return;float x=at.x,y=height-at.y;
            float pop=reduced?1:Mathf.Clamp01(age/.07f),grow=pop<1?Mathf.Lerp(.4f,1.25f,pop):Mathf.Lerp(1.25f,1,Mathf.Clamp01((age-.07f)/.12f)),a=1-Mathf.Clamp01((age-life*.6f)/(life*.4f));
            float size=(h.Kind==NativeTopDog.HitPop||h.Kind==NativeTopDog.HitClang||h.Kind==NativeTopDog.HitAnvil?46:h.Kind==NativeTopDog.HitGulp?24:34)*grow*Mathf.Clamp(12/Mathf.Max(3,at.z),.55f,1.3f);
            var ink=DogInks[(h.Id&0x7fffffff)%DogInks.Length];float tilt=reduced?0:((h.Id%7)-3)*3.5f;var r=new Rect(x-220,y-size,440,size*1.6f);
            U.Push(new Vector2(x,y),Vector2.zero,1,tilt);
            // A burst behind it, an ink outline, the word.
            U.Glow(new Rect(x-size*1.1f,y-size*.55f,size*2.2f,size*1.1f),U.A(ink,.35f*a),size*.5f);
            for(int i=0;i<8;i++){float ang=i*Mathf.PI/4;U.Text(new Rect(r.x+Mathf.Cos(ang)*3,r.y+Mathf.Sin(ang)*3,r.width,r.height),word,size,new Color(.05f,.03f,.08f,a),TextAnchor.MiddleCenter,bold:true,italic:true);}
            U.Text(r,word,size,U.A(ink,a),TextAnchor.MiddleCenter,bold:true,italic:true);U.Pop();
        }
        // ---- All You Can Eat ----
        // The caster's splash: a red and white diner check slams across, ALL YOU CAN EAT! stamped on it with a wobble, a hot dog either side.
        private void DogCutIn(NativeDogFeast f,float t,float W,float H,bool reduced)
        {
            float age=t-f.At,a=age<.75f?1:1-Mathf.Clamp01((age-.75f)/.25f);if(a<=0)return;float slide=reduced?0:Mathf.Clamp01(1-age/.13f)*W,th=H*.2f;
            var band=new Rect(-slide,H*.5f-th/2,W,th);U.Fill(band,new Color(.96f,.94f,.88f,.94f*a));
            float cell=th/4;for(int row=0;row<4;row++)for(float x=band.x+((row%2)*cell)-((age*90)%(cell*2));x<band.xMax;x+=cell*2)U.Fill(new Rect(x,band.y+row*cell,cell,cell),new Color(.86f,.16f,.14f,.92f*a));
            U.Fill(new Rect(band.x,band.y-6,W,6),new Color(.95f,.75f,.12f,a));U.Fill(new Rect(band.x,band.yMax,W,6),new Color(.95f,.75f,.12f,a));
            float stamp=reduced?1:Mathf.Clamp01((age-.05f)/.1f),size=Mathf.Lerp(132,84,stamp),wob=reduced?0:Mathf.Sin(age*30)*Mathf.Exp(-age*6)*4;
            U.Push(new Vector2(W/2,H/2),Vector2.zero,1,-3+wob);
            var r=new Rect(0,H*.5f-70,W,140);for(int i=0;i<8;i++){float ang=i*Mathf.PI/4;U.Text(new Rect(r.x+Mathf.Cos(ang)*5,r.y+Mathf.Sin(ang)*5,r.width,r.height),"ALL YOU CAN EAT!",size,new Color(.08f,.03f,.02f,a),TextAnchor.MiddleCenter,bold:true,italic:true);}
            U.Text(r,"ALL YOU CAN EAT!",size,new Color(1,.83f,.18f,a),TextAnchor.MiddleCenter,bold:true,italic:true,glow:new Color(1,.4f,.1f,.6f*a));U.Pop();
            U.Text(new Rect(0,H*.5f+th/2+10,W,22),"ULTIMATE // LASSO, FEED, POP",15,new Color(.95f,.25f,.15f,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f);
            for(int s=-1;s<=1;s+=2){float bob=reduced?0:Mathf.Sin(age*12+s)*6;NativeIcons.Draw(new Rect(W*.5f+s*W*.36f-34,H*.5f-34+bob,68,68),NativeTopDog.Icon(NativeTopDog.Feast),new Color(1,.86f,.3f,a));}
        }
        // The victim: what is happening to them, then the one thing left to see.
        private void DogVictimLine(NativeDogFeast f,float t,float W,float H,bool reduced)
        {
            bool snapped=f.Snap>-50&&t>=f.Snap;string line=snapped?"FREE!":t<f.Release?"FED "+f.Fed+" / "+NativeTopDog.Feeds:"STUFFED!";float beat=reduced?0:.5f+.5f*Mathf.Sin(t*(t>=f.Release?24:10));
            float size=t>=f.Release&&!snapped?44+8*beat:30;var r=new Rect(0,H*.5f+70,W,60);for(int i=0;i<8;i++){float ang=i*Mathf.PI/4;U.Text(new Rect(r.x+Mathf.Cos(ang)*3,r.y+Mathf.Sin(ang)*3,r.width,r.height),line,size,new Color(.06f,.03f,.02f,1),TextAnchor.MiddleCenter,bold:true,italic:true);}
            U.Text(r,line,size,snapped?new Color(.5f,1,.5f):new Color(1,.86f,.2f),TextAnchor.MiddleCenter,bold:true,italic:true);
            if(!snapped&&t<f.Release)U.Text(new Rect(0,H*.5f+124,W,20),"ALLIES: HIT THE TOP DOG TO SNAP THE LINKS",13,new Color(1,.6f,.4f,.9f),TextAnchor.MiddleCenter,mono:true,tracking:.14f);
            if(!snapped&&t>=f.Release){float u=Mathf.Clamp01((t-f.Release)/Mathf.Max(.1f,f.Pop-f.Release));U.Fill(new Rect(0,0,W,H),new Color(1,.85f,.75f,.12f*u*u));}
        }
        private void DogBanner(NativePilot caster,NativeDogFeast f,float t,float W,float H)
        {
            float a=Mathf.Clamp01((f.Until-t)/.4f);if(a<=0)return;var band=new Rect(W*.5f-250,92,500,34);U.Round(band,C.Navy(.88f*a),6,new Color(1,.78f,.2f,.8f*a));
            string who=f.Victim!=0&&f.Catch>-50?" FEEDS "+(arena.Pilots.Find(q=>q.Seat.id==f.Victim)?.Seat.name??"SOMEONE").ToUpperInvariant():"";
            U.Text(band,caster.Seat.name.ToUpperInvariant()+"  ·  ALL YOU CAN EAT"+who,15,new Color(1,.86f,.42f,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);
        }
        // Cream over the eyes: big soft blobs that slide down and thin out as they are wiped.
        private void DogCream(NativeDogMark m,float t,float W,float H,bool reduced)
        {
            float age=t-m.At,life=Mathf.Max(.1f,m.Until-m.At),u=Mathf.Clamp01(age/life),a=(1-u*u)*Mathf.Clamp01(age/.05f);
            for(int i=0;i<9;i++)
            {
                float sx=Mathf.Repeat(i*.618f+m.Id*.13f,1),sy=Mathf.Repeat(i*.414f+.2f,1),size=H*(.28f+.18f*Mathf.Repeat(i*.37f,1))*(1-.3f*u);var c=new Vector2(W*(.15f+.7f*sx),H*(.12f+.6f*sy)+(reduced?0:H*.25f*u*u*(.5f+.5f*sx)));
                U.Glow(new Rect(c.x-size*.5f,c.y-size*.4f,size,size*.8f),new Color(1,.99f,.94f,.85f*a),size*.25f);U.Round(new Rect(c.x-size*.32f,c.y-size*.26f,size*.64f,size*.52f),new Color(1,1,.96f,.9f*a),size*.26f);
            }
            if(!reduced)for(int i=0;i<6;i++){float x=W*(.1f+.8f*Mathf.Repeat(i*.29f+m.Id*.07f,1)),len=H*(.08f+.25f*u)*(.6f+.4f*Mathf.Repeat(i*.53f,1));U.Round(new Rect(x-7,H*.35f,14,len),new Color(1,.98f,.92f,.8f*a),7);}
        }
        // ---- the entrance's card (IntroCard for Order Up!): the old cartoon's iris, and a diner sign for the name ----
        private static Texture2D dogIris;
        // A disc of clear in black, soft at its rim, stretched over the iris's circle.
        private static Texture DogIrisTexture()
        {
            if(dogIris!=null)return dogIris;const int n=256;var px=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1,r=Mathf.Sqrt(dx*dx+dy*dy);byte a=(byte)(255*Mathf.Clamp01((r-.985f)/.015f));px[y*n+x]=new Color32(0,0,0,a);}
            dogIris=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Cartoon iris",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};dogIris.SetPixels32(px);dogIris.Apply(false,true);return dogIris;
        }
        private static void DogIrisAt(Vector2 c,float r,float W,float H)
        {
            if(r>=Mathf.Sqrt(W*W+H*H))return;var hole=new Rect(c.x-r,c.y-r,2*r,2*r);var ink=Color.black;
            U.Fill(new Rect(0,0,W,Mathf.Max(0,hole.y)),ink);U.Fill(new Rect(0,hole.yMax,W,Mathf.Max(0,H-hole.yMax)),ink);U.Fill(new Rect(0,hole.y,Mathf.Max(0,hole.x),hole.height),ink);U.Fill(new Rect(hole.xMax,hole.y,Mathf.Max(0,W-hole.xMax),hole.height),ink);
            if(r>.5f)U.Image(hole,DogIrisTexture(),Color.white);else U.Fill(hole,ink);
        }
        private void DogCard(NativePilot pilot,float rawAge,int index,int total,bool team)
        {
            string style=NativeTopDog.Entrance;float duration=(float)NativeIntroRules.OverlayDuration(style),nameAt=(float)NativeIntroRules.NameAt(style),age=Mathf.Min(rawAge,duration),W=U.W,H=U.H;bool reduced=prefs.reducedEffects;
            // The iris: open from a point at the start, closing on the pilot at the end (to a small circle, then black).
            float open=Mathf.Clamp01((age-NativeTopDogIntro.IrisOpen)/.55f),shut=Mathf.Clamp01((age-NativeTopDogIntro.IrisClose)/.6f),full=Mathf.Sqrt(W*W+H*H)*.6f;
            float r=age<NativeTopDogIntro.IrisClose?Mathf.Lerp(4,full,open*open*(3-2*open)):shut<.75f?Mathf.Lerp(full,H*.16f,Mathf.SmoothStep(0,1,shut/.75f)):Mathf.Lerp(H*.16f,0,(shut-.75f)/.25f);
            var centre=age<NativeTopDogIntro.IrisClose?new Vector2(W*.52f,H*.52f):new Vector2(W*.5f,H*.42f);if(!reduced||age<.6f||age>=NativeTopDogIntro.IrisClose)DogIrisAt(centre,r,W,H);
            // The topline: an order ticket, the progress a strip of mustard.
            float top=Mathf.Clamp01(age/.4f)*(1-Mathf.Clamp01((age-NativeTopDogIntro.IrisClose)/.3f)),x0=W*.06f,tw=W*.88f,y=88,rule=y+29,fill=Mathf.Clamp01(age/duration);
            U.Text(new Rect(x0,y,tw,15),"MATCH INTRO / "+(index+1).ToString("00")+" \u2014 "+total.ToString("00")+"   \u00b7   ORDER #"+(100+pilot.Seat.id),12,U.A(U.Hex(0xf3e6c8),top),TextAnchor.UpperLeft,mono:true,tracking:.2f);
            U.Fill(new Rect(x0,rule,tw,1),U.A(U.Hex(0x4a2a18),.7f*top));U.Round(new Rect(x0,rule-2,tw*fill,4),U.A(U.Hex(0xffc230),top),2);
            if(age<nameAt)return;
            // The sign drops in on its chains, swings, settles; its bulbs come on round it; the name in cream on red, TOP DOG on a mustard ribbon over it.
            float since=age-nameAt,leave=Mathf.Clamp01((age-(duration-.35f))/.3f);float drop=reduced?1:Mathf.Clamp01(since/.22f),swing=reduced?0:Mathf.Sin(since*9)*Mathf.Exp(-since*3.2f)*5;float a=(reduced?Mathf.Clamp01(since/.2f):1)*(1-leave);
            var seat=pilot.Seat;string text=(string.IsNullOrWhiteSpace(seat.introText)?"Order up!":seat.introText).ToUpperInvariant();
            float F=Mathf.Clamp(W*.06f,30,78),fit=U.Measure(seat.name,F,true,false,.02f).x,sw=Mathf.Clamp(fit+F*1.6f,W*.34f,W*.84f);if(fit>sw-F)F*=(sw-F)/fit;float sh=F*2.2f;
            float cx=W*.5f,cy=H*.78f-(1-drop*drop)*H*.6f+leave*leave*H*.5f;var sign=new Rect(cx-sw/2,cy-sh/2,sw,sh);
            U.Push(new Vector2(cx,cy-sh*1.4f),Vector2.zero,1,swing);
            // Chains up out of frame.
            for(int s=-1;s<=1;s+=2)for(int i=0;i<8;i++)U.Round(new Rect(cx+s*sw*.32f-3,cy-sh/2-14-i*14,6,11),U.A(U.Hex(0x9aa0a8),a),3);
            U.Round(new Rect(sign.x-6,sign.y-6,sign.width+12,sign.height+12),U.A(U.Hex(0xffc230),a),18);U.Round(sign,U.A(U.Hex(0xc8231c),a),14);U.Round(new Rect(sign.x+8,sign.y+8,sign.width-16,sign.height-16),U.A(U.Hex(0xa81a15),a),10,U.A(U.Hex(0xffe3a0),.6f*a),2);
            // Bulbs chasing round the edge.
            int bulbs=Mathf.Max(12,Mathf.RoundToInt((sw*2+sh*2)/34));float per=(sw*2+sh*2)/bulbs;
            for(int i=0;i<bulbs;i++){float d=i*per,bx,by;if(d<sw){bx=sign.x+d;by=sign.y;}else if(d<sw+sh){bx=sign.xMax;by=sign.y+d-sw;}else if(d<2*sw+sh){bx=sign.xMax-(d-sw-sh);by=sign.yMax;}else{bx=sign.x;by=sign.yMax-(d-2*sw-sh);}
                bool on=reduced||((int)(since*10)+i)%3!=0;U.Glow(new Rect(bx-3,by-3,6,6),U.A(on?U.Hex(0xfff2b0):U.Hex(0x6a4a20),a*(on?1:.5f)),on?8:2);U.Round(new Rect(bx-4,by-4,8,8),U.A(on?U.Hex(0xfff6d0):U.Hex(0x80602a),a),4);}
            var ribbon=new Rect(sign.x+sw*.06f,sign.y-F*.42f,F*3.6f,F*.62f);U.Round(ribbon,U.A(U.Hex(0xffc230),a),6);U.Text(ribbon,"TOP DOG",F*.38f,U.A(U.Hex(0x3a1608),a),TextAnchor.MiddleCenter,bold:true,italic:true,tracking:.12f);
            // The name, stamped a beat after the sign, with a flash.
            float stamp=reduced?1:Mathf.Clamp01((since-.12f)/.1f),nameA=a*(reduced?1:since>=.12f?1:0);
            if(nameA>0)
            {
                U.Push(new Vector2(cx,cy),Vector2.zero,Mathf.Lerp(1.35f,1,stamp*stamp));var nr=new Rect(sign.x,cy-F*.62f,sign.width,F*1.24f);
                for(int i=0;i<8;i++){float ang=i*Mathf.PI/4;U.Text(new Rect(nr.x+Mathf.Cos(ang)*3,nr.y+Mathf.Sin(ang)*3,nr.width,nr.height),seat.name,F,U.A(U.Hex(0x3a0a06),nameA),TextAnchor.MiddleCenter,bold:true,italic:true,tracking:.02f);}
                U.Text(nr,seat.name,F,U.A(U.Hex(0xfff3d6),nameA),TextAnchor.MiddleCenter,bold:true,italic:true,tracking:.02f,glow:new Color(1,.6f,.2f,.55f*nameA));U.Pop();
                float flash=reduced?0:Mathf.Clamp01(1-(since-.12f)/.12f);if(flash>0)U.Fill(sign,new Color(1,.95f,.75f,.45f*flash*a));
            }
            // A mustard squiggle under it.
            if(nameA>0){float grow=reduced?1:Mathf.Clamp01((since-.2f)/.35f),sy=cy+F*.62f,sx0=sign.x+sw*.18f,len=sw*.64f*grow;for(float x=0;x<len;x+=3){float yy=sy+Mathf.Sin(x/sw*28)*4;U.Round(new Rect(sx0+x-2.5f,yy-2.5f,5,5),U.A(U.Hex(0xffc230),a),2.5f);}}
            U.Pop();
            float small=(1-leave)*(reduced?1:Mathf.Clamp01((since-.35f)/.3f));
            if(team)U.Text(new Rect(0,sign.yMax+14,W,18),NativeMatch.TeamNames[TeamIndex(seat.team)].ToUpperInvariant()+" TEAM",14,U.A(TeamColor(seat.team),small),TextAnchor.UpperCenter,bold:true,tracking:.12f,shadow:new Color(0,0,0,.8f*small));
            U.Text(new Rect(0,H*.95f-6,W,18),text,12,U.A(U.Hex(0xf3e6c8),small),TextAnchor.UpperCenter,tracking:.3f,shadow:new Color(0,0,0,.8f*small));
        }
    }
    public sealed partial class NativeAudio
    {
        private readonly Dictionary<string,AudioClip> toonClips=new Dictionary<string,AudioClip>(System.StringComparer.Ordinal);
        /// <summary>One of the Top Dog's toon sounds: the owner's clip under "topdog.<id>" when installed, else the built-in synthesized one
        /// (Core NativeToonSynth), with the usual falloff and pan from the listener.</summary>
        public void Toon(string id,Vector3? position,float gain,bool mine=false,float rate=1)
        {
            if(id==null||!NativeToonSynth.Known(id))return;if(!bankLoaded)LoadBank();if(BankPlay(NativeToonSynth.Prefix+id,position,gain,0,null,mine,rateScale:rate))return;
            if(Muted||Effects<=0||voices==null)return;
            if(!toonClips.TryGetValue(id,out var clip)||clip==null){var data=NativeToonSynth.Samples(id);clip=AudioClip.Create("Toon "+id,data.Length,1,NativeToonSynth.Rate,false);clip.SetData(data,0);toonClips[id]=clip;}
            int free=-1;for(int i=0;i<voices.Length;i++)if(!voices[i].isPlaying){free=i;break;}if(free<0)return;
            var voice=voices[free];keyedVoices[free]=false;float pan=0;
            if(position.HasValue){var d=position.Value-listener;gain*=NativeEventSounds.Falloff(d.magnitude);pan=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);}
            voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=rate;voice.Play();
        }
        private void ClearToonClips(){foreach(var c in toonClips.Values)CapturedAnimation.Release(c);toonClips.Clear();}
        private readonly (string id,float gain)[] dogSounds=new (string,float)[4];private int dogIntroCue;private float dogIntroLast=-1;
        // The Top Dog's hits, casts and feast, each once as the record first shows it.
        private void DogMoments(NativePilot p,Vector3 pos,float t,bool mine)
        {
            var k=NativeDogView.Source?.Of(p.Seat.id);if(k==null)return;
            if(k.Hits!=null)foreach(var h in k.Hits){if(h==null||t<h.At||t-h.At>=.15f||!Once(p.Seat.id+"dog"+h.Id,h.At))continue;int n=NativeTopDogSounds.Hit(h.Kind,dogSounds);var at=new Vector3(h.Pos.x,h.Pos.y,h.Pos.z);for(int i=0;i<n;i++)Toon(dogSounds[i].id,at,dogSounds[i].gain,mine,NativeTopDogSounds.Rate(h.Id,i));}
            if(k.CastKind!=null&&t>=k.CastAt&&t-k.CastAt<.15f&&Once(p.Seat.id+"dogcast"+k.CastKind,k.CastAt)){int n=NativeTopDogSounds.Cast(k.CastKind,k.CastStep,dogSounds);for(int i=0;i<n;i++)Toon(dogSounds[i].id,pos,dogSounds[i].gain,mine);}
            if(k.CastKind!=null&&t>=k.CastRelease&&t-k.CastRelease<.15f&&Once(p.Seat.id+"dogrel"+k.CastKind,k.CastRelease)){int n=NativeTopDogSounds.Release(k.CastKind,k.CastStep,dogSounds);for(int i=0;i<n;i++)Toon(dogSounds[i].id,pos,dogSounds[i].gain,mine);}
            if(k.Props!=null)foreach(var pr in k.Props)
            {
                if(pr==null)continue;var at=new Vector3(pr.Pos.x,pr.Pos.y,pr.Pos.z);
                if((pr.Kind==NativeTopDog.PropAnvil||pr.Kind==NativeTopDog.PropPiano)&&t>=pr.At&&t-pr.At<.15f&&Once(p.Seat.id+"dogfall"+pr.Id,pr.At))Toon("whistle-fall",at+Vector3.up*6,.9f,mine);
                if(pr.Kind==NativeTopDog.PropSquirt&&t>=pr.At&&t<pr.Until){float step=Mathf.Floor((t-pr.At)/1.5f);if(Once(p.Seat.id+"dogsquirt"+pr.Id,pr.At+step*1000))Toon("squirt",pos,.75f,mine);}
                if(pr.Kind==NativeTopDog.PropGlove&&t>=pr.At&&t-pr.At<.15f&&Once(p.Seat.id+"dogglove"+pr.Id,pr.At))Toon("sproing",pos,.85f,mine);
            }
            var f=k.Feast;
            if(f!=null&&t>=f.At&&t<f.Throw){float step=Mathf.Floor((t-f.At)/.66f);if(Once(p.Seat.id+"dogwhirl",f.At+step*1000))Toon("whirl",pos,.8f,true);}
            if(f!=null&&t>=f.At&&t-f.At<.15f&&Once(p.Seat.id+"dogbell",f.At))Toon("bell",pos,.95f,true);
            if(f!=null&&f.Catch>-50&&f.Snap<-50&&t>=f.Release&&t<f.Pop){float step=Mathf.Floor((t-f.Release)/.5f);if(Once(p.Seat.id+"dogswell",f.Release+step*1000))Toon(step==0?"inflate":step%2==0?"stretch":"wobble",pos,.8f,true,1+.08f*step);}
            if(f!=null&&f.Catch>-50&&f.Snap<-50&&t>=f.Release+.6f&&t-f.Release-.6f<.15f&&Once(p.Seat.id+"doghic",f.Release))Toon("hiccup",pos,.9f,true);
        }
        private int dogPickCue;private string dogPickId;private float dogPickLast=-1;
        /// <summary>A Top Dog chip's pick: its gag's toon cues (Core NativeTopDogSounds.Picks) on the spotlight's clock, `age` s after the pick.</summary>
        public void DogPick(string chip,float age)
        {
            int i=NativeDogChips.Index(chip);if(i<0)return;float s=age-NativeDraftPlant.DetailsAt-NativeDogChipArt.Start;
            if(chip!=dogPickId||age<dogPickLast-.2f){dogPickId=chip;dogPickCue=0;}dogPickLast=age;var list=NativeTopDogSounds.Picks[i];
            while(dogPickCue<list.Length&&s>=list[dogPickCue].At){var c=list[dogPickCue++];if(s-c.At<.25f)Toon(c.Id,null,c.Gain,true,c.Rate);}
        }
        /// <summary>The Top Dog's entrance cues on its clock, each once as `age` passes it.</summary>
        public void DogIntro(float age)
        {
            if(age<dogIntroLast-.2f)dogIntroCue=0;dogIntroLast=age;var list=NativeTopDogSounds.Intro;while(dogIntroCue<list.Length&&age>=list[dogIntroCue].At){var c=list[dogIntroCue++];if(age-c.At<.25f)Toon(c.Id,null,c.Gain,true,c.Rate);}
        }
    }
}
