using System.Linq;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using M=ChipArena.UnityConversion.NativeUiMotion;

namespace ChipArena.UnityConversion
{
    // The Bombadier on the HUD (docs/BOMBADIER.md): its cards, the chain counter by the crosshair, the Grand Finale's cut-in for the caster, and for everyone else the warning that tells them to move.
    public sealed partial class ConversionGame
    {
        private void BombCards(NativePilot p)
        {
            var k=NativeBombs.Source?.Of(p.Seat.id);float t=arena.Time;
            for(int i=0;i<NativeBombs.Slots;i++)
            {
                string id=NativeBombs.Slot[i];float ready=k?.Ready!=null&&i<k.Ready.Length?k.Ready[i]:0,left=ready-t;bool busy=k?.CastKind==id&&t>=k.CastAt&&t<k.CastUntil;float cd=BombCooldown(id);
                string key=NativeBombs.SlotKeys[i]+" · "+NativeBombs.Name(id).ToUpperInvariant();if(i==2&&k!=null&&k.MineCharges>1)key+=" ×"+k.MineCharges;
                cards.Add(new Card{Key=key,State=busy?"ACTIVE":left>0?left.ToString("0.0")+"s":"READY",Icon=NativeBombs.Icon(id),Ready=!busy&&left<=0,Active=busy,Dial=left>0?1-Mathf.Clamp01(left/Mathf.Max(.1f,cd)):1});
            }
            // The Finale's card: the meter, then the key.
            float meter=k?.Meter??0;bool full=meter>=1,finale=k?.Finale!=null&&t>=k.Finale.At&&t<k.Finale.Until;
            cards.Add(new Card{Key="5 · "+NativeBombs.Name(NativeBombs.GrandFinale).ToUpperInvariant(),State=finale?"ACTIVE":full?"READY":Mathf.FloorToInt(meter*100)+"%",Icon=NativeBombs.Icon(NativeBombs.GrandFinale),Ready=full&&!finale,Active=finale,Dial=meter});
        }
        private float BombCooldown(string id){if(content?.abilities!=null)foreach(var a in content.abilities)if(a!=null&&a.id==id)return (float)a.cooldown;return 1;}
        private int bombChainShown;private float bombChainAt=-9;
        /// <summary>Drawn with the HUD: the chain counter, the Finale's splash for its caster and the warning for everyone else.</summary>
        private void BombScreen(float width,float height)
        {
            if(arena==null||Event.current.type!=EventType.Repaint||NativeBombs.Source==null)return;float t=arena.Time;var local=arena.Local;
            foreach(var p in arena.Pilots)
            {
                if(p==null)continue;var k=NativeBombs.Source.Of(p.Seat.id);if(k==null)continue;
                if(p==local)BombChain(k,t,width,height);
                var f=k.Finale;if(f==null||t<f.At||t>=f.Until)continue;
                if(p==local)BombCutIn(f,t,width,height);else BombWarning(p,f,t,width,height);
            }
        }
        // CHAIN ×3 beside the crosshair: it punches in on each link, and goes when the chain lapses.
        private void BombChain(NativeBombKit k,float t,float width,float height)
        {
            bool live=k.Chain>0&&t<k.ChainUntil;if(!live){bombChainShown=0;return;}if(k.Chain!=bombChainShown){bombChainShown=k.Chain;bombChainAt=Time.unscaledTime;}
            float age=Time.unscaledTime-bombChainAt,punch=prefs.reducedEffects?0:Mathf.Clamp01(1-age/.22f),left=Mathf.Clamp01((k.ChainUntil-t)/1.2f);float x=width/2+74,y=height/2-22,size=22+punch*14;
            Color hot=k.Chain>=3?U.Hex(0xfff0b0):U.Hex(0xffa23a);U.Text(new Rect(x+2,y+2,220,34),"CHAIN ×"+k.Chain,Mathf.RoundToInt(size),C.Void(.7f),TextAnchor.MiddleLeft,bold:true,mono:true);U.Text(new Rect(x,y,220,34),"CHAIN ×"+k.Chain,Mathf.RoundToInt(size),hot,TextAnchor.MiddleLeft,bold:true,mono:true);
            U.Fill(new Rect(x,y+34,110,3),C.Void(.6f));U.Fill(new Rect(x,y+34,110*left,3),hot);
        }
        // The caster's splash: hazard bands slam across, the live portrait's place left to the world, GRAND FINALE stamped.
        private void BombCutIn(NativeBombFinale f,float t,float W,float H)
        {
            float age=t-f.At,a=age<.5f?1:1-Mathf.Clamp01((age-.5f)/.2f);if(a<=0)return;bool reduced=prefs.reducedEffects;float slide=reduced?0:Mathf.Clamp01(1-age/.12f)*W,th=H*.16f;
            for(int s=-1;s<=1;s+=2){var band=new Rect(s<0?-slide:slide,H*.5f+s*H*.17f-th/2,W,th);U.Fill(band,C.Void(.82f*a));float x0=band.x-((age*220)%44);for(float x=x0;x<band.xMax+44;x+=44)U.Fill(new Rect(x,band.y,22,8),new Color(.95f,.72f,.06f,a));for(float x=x0+22;x<band.xMax+44;x+=44)U.Fill(new Rect(x,band.yMax-8,22,8),new Color(.95f,.72f,.06f,a));}
            float stamp=reduced?1:Mathf.Clamp01(age/.09f),size=Mathf.Lerp(120,76,stamp);var r=new Rect(0,H*.5f-60,W,120);U.Text(new Rect(r.x+3,r.y+3,r.width,r.height),"GRAND FINALE",Mathf.RoundToInt(size),C.Void(.8f*a),TextAnchor.MiddleCenter,bold:true,mono:true);U.Text(r,"GRAND FINALE",Mathf.RoundToInt(size),new Color(1,.86f,.42f,a),TextAnchor.MiddleCenter,bold:true,mono:true);
            U.Text(new Rect(0,H*.5f+36,W,22),"ULTIMATE // PUSH THE PLUNGER",15,new Color(1,.45f,.2f,a),TextAnchor.MiddleCenter,mono:true,tracking:.18f);
        }
        // ---- the Last Warning's card (IntroCard for this entrance) ----
        private static Texture2D bombTape;
        // Black and yellow at 45 degrees, scuffed: one period of the tape, tiled along it.
        private static Texture BombTape()
        {
            if(bombTape!=null)return bombTape;const int n=64;var px=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                bool yellow=(x+y)%n<n/2;float scuff=NativeEntranceMath.Rand(x*7+y*131,3);float wear=scuff>.93f?.72f:scuff>.8f?.9f:1;
                px[y*n+x]=yellow?new Color32((byte)(242*wear),(byte)(184*wear),(byte)(16*wear),255):new Color32((byte)(16/wear),(byte)(15/wear),(byte)(14/wear),255);
            }
            bombTape=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Hazard tape",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};bombTape.SetPixels32(px);bombTape.Apply(false,true);return bombTape;
        }
        private const string BombCaution="CAUTION  //  LIVE ORDNANCE  //  CAUTION  //  DO NOT STAND HERE  //  CAUTION  //  LIVE ORDNANCE  //  CAUTION  //  DO NOT STAND HERE  //  CAUTION  //  LIVE ORDNANCE  //  CAUTION  //  DO NOT STAND HERE  //  ";
        private void BombCard(NativePilot pilot,float rawAge,int index,int total,bool team)
        {
            string style=NativeBombs.Entrance;float duration=(float)NativeIntroRules.OverlayDuration(style),nameAt=(float)NativeIntroRules.NameAt(style),age=Mathf.Min(rawAge,duration),W=U.W,H=U.H;bool reduced=prefs.reducedEffects;var ink=U.Hex(0x040610);
            // The cinema bands, as every entrance; everything else goes out with the charges' lights, for the one beat of nothing.
            U.Fade(new Rect(0,0,W,H*.17f),U.A(ink,.867f));U.Fade(new Rect(0,H*.7f,W,H*.3f+1),U.A(ink,.94f),false);
            float shade=M.EaseOut(M.Progress(age,nameAt,.7f)),ry=H*.46f*.92f;U.RadialStops(new Rect(-W*.1f,H-ry,W*1.2f,ry*2),U.A(ink,shade),new[]{0,.46f,.76f},new[]{.96f,.70f,0});
            bool black=age>=NativeBombadierIntro.BlackAt&&age<NativeBombadierIntro.FirstBlast;if(black)return;
            // The topline; the progress bar is a fuse, burning along its rule.
            float top=M.EaseOut(M.Progress(age,0,.45f)),x0=W*.06f,tw=W*.88f,y=88,rule=y+29,fill=Mathf.Clamp01(age/duration),tip=x0+tw*fill,burn=1-M.Progress(age,duration-.35f,.3f);
            U.Text(new Rect(x0,y,tw,15),"MATCH INTRO / "+(index+1).ToString("00")+" — "+total.ToString("00"),12,U.A(U.Hex(0xd7d8de),top),TextAnchor.UpperLeft,mono:true,tracking:.2f);
            U.Fill(new Rect(x0,rule,tip-x0,1),U.A(U.Hex(0x3a322c),.7f*top));U.Fill(new Rect(tip,rule-.5f,x0+tw-tip,2),U.A(U.Hex(0x8a6a44),.85f*top));
            if(burn>0)
            {
                float fl=.8f+.2f*Mathf.Sin(age*53);U.Glow(new Rect(tip-2,rule-1.5f,4,4),new Color(1,.45f,.12f,.9f*fl*top*burn),12);U.Fill(new Rect(tip-1.5f,rule-1,3,3),new Color(1,.9f,.6f,top*burn));
                if(!reduced)for(int i=0;i<7;i++){float a=(age*5+i*.143f)%1;int seed=Mathf.FloorToInt(age*5+i*.143f)*31+i*97;float dx=-a*(10+26*NativeEntranceMath.Rand(seed,1)),dy=a*(NativeEntranceMath.Signed(seed,2)*14+a*16);U.Fill(new Rect(tip+dx,rule+dy,1.5f,1.5f),new Color(1,.7f,.25f,(1-a)*top*burn));}
            }
            if(age<nameAt)return;
            // Two strips of tape whip across and shudder as they land; a stamped name on the wider one; they tear off the way they were going.
            float since=age-nameAt,leave=M.Progress(age,duration-.32f,.3f),leaveEase=leave*leave;float inA=reduced?1:M.EaseOut(M.Progress(age,nameAt,.16f)),inB=reduced?1:M.EaseOut(M.Progress(age,nameAt+.05f,.18f)),fade=reduced?Mathf.Clamp01(since/.2f)*(1-leave):1;
            float shudder=reduced?0:Mathf.Sin(since*46)*Mathf.Exp(-since*9);
            var seat=pilot.Seat;string weapon=(content.weapons.FirstOrDefault(w=>w.id==seat.spec)?.name??seat.spec).ToUpperInvariant(),text=(string.IsNullOrWhiteSpace(seat.introText)?"Ready to make an impact":seat.introText).ToUpperInvariant();
            float cx=W*.05f,cw=W*.9f,F=Mathf.Clamp(W*.065f,30,82),fit=U.Measure(seat.name,F,true,false,.02f).x;if(fit>cw)F*=cw/fit;
            const float strip=9;float ySpan=H*.95f-10,yStrong=ySpan-strip-10-1.15f*F,plateTop=yStrong-6,plateH=1.15f*F+12,yTeam=plateTop-strip-8-18,ySmall=(team?yTeam-6:plateTop-strip-8)-15;
            // The thin strip, behind: stencilled, running on.
            float yB=ySmall-30,wide=W*1.3f,xB=-W*.15f;
            U.Push(new Vector2(W/2,yB+10),new Vector2((1-inB)*W*1.3f-leaveEase*W*1.4f,0),1,-3.4f+.5f*shudder);
            U.Fill(new Rect(xB,yB,wide,20),U.A(new Color(.95f,.72f,.06f),fade));U.Fill(new Rect(xB,yB,wide,1.5f),U.A(U.Hex(0x1a1408),.8f*fade));U.Fill(new Rect(xB,yB+18.5f,wide,1.5f),U.A(U.Hex(0x1a1408),.8f*fade));
            U.Text(new Rect(xB-since*34,yB,wide*2,20),BombCaution+BombCaution,12,U.A(U.Hex(0x141008),fade),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);U.Pop();
            // The wide one: a dark plate between two lengths of hazard stripe.
            var band=new Rect(-W*.15f,plateTop,W*1.3f,plateH);var pivot=new Vector2(W/2,plateTop+plateH/2);float tilt=-1.1f+.35f*shudder,slide=(inA-1)*W*1.3f+leaveEase*W*1.4f;
            U.Push(pivot,new Vector2(slide,0),1,tilt);
            U.Fill(band,U.A(U.Hex(0x0b0a0c),.96f*fade));var period=new Vector2(strip*2.4f,strip*2.4f);U.Tile(new Rect(band.x,band.y-strip,band.width,strip),BombTape(),U.A(Color.white,fade),period,new Vector2(since*22,0));U.Tile(new Rect(band.x,band.yMax,band.width,strip),BombTape(),U.A(Color.white,fade),period,new Vector2(-since*22,0));
            // The name lands on the plate a beat after it: hard, with a flash, a size too big for one frame.
            float hit=M.Progress(age,nameAt+.12f,.09f),stamp=reduced?1:hit*hit,nameA=(reduced?Mathf.Clamp01((since-.05f)/.2f):since>=.12f?1:0)*fade;
            if(nameA>0)
            {
                U.Push(pivot,reduced?Vector2.zero:new Vector2(Mathf.Sin(since*71)*Mathf.Exp(-(since-.12f)*14)*5,Mathf.Sin(since*89)*Mathf.Exp(-(since-.12f)*14)*5),Mathf.Lerp(1.34f,1,stamp));
                var nameLine=new Rect(cx,yStrong,cw,1.15f*F);U.Text(nameLine,seat.name,F,U.A(U.Hex(0xfff0c8),nameA),TextAnchor.MiddleCenter,bold:true,tracking:.02f,glow:new Color(1,.42f,.1f,.7f*nameA),shadow:new Color(0,0,0,.85f*nameA));U.Pop();
                float flash=reduced?0:Mathf.Clamp01(1-(since-.12f)/.14f);if(flash>0)U.Fill(band,new Color(1,.92f,.7f,.5f*flash*fade));
            }
            U.Pop();
            // The small lines stay still under it all.
            float small=(1-leave)*(reduced?1:Line(age,nameAt+.16f,.3f)),line=(1-leave)*(reduced?1:Line(age,nameAt+.3f,.4f));var shadow=new Color(0,0,0,.8f*small);
            U.Text(new Rect(cx,ySmall,cw,15),weapon,12,U.A(U.Hex(0xffc24a),small),TextAnchor.UpperCenter,bold:true,tracking:.3f,shadow:shadow);
            if(team)U.Text(new Rect(cx,yTeam,cw,18),NativeMatch.TeamNames[TeamIndex(seat.team)].ToUpperInvariant()+" TEAM",14,U.A(TeamColor(seat.team),small),TextAnchor.UpperCenter,bold:true,tracking:.12f,shadow:shadow);
            U.Text(new Rect(cx,ySpan,cw,18),text,12,U.A(U.Hex(0xb9bac4),line),TextAnchor.UpperCenter,tracking:.3f);
        }
        // Everyone else: a banner saying whose it is and, when the circle is on you, the one word you need.
        private void BombWarning(NativePilot caster,NativeBombFinale f,float t,float W,float H)
        {
            var me=arena.Local;if(me==null)return;float toStrike=f.Strike-t;bool mine=false;if(f.Circles!=null)foreach(var c in f.Circles)if(c!=null&&c.Id==me.Seat.id){mine=true;var feet=me.Body.position;float dx=feet.x-c.Pos.x,dz=feet.z-c.Pos.z;mine=dx*dx+dz*dz<NativeBombs.CircleRadius*NativeBombs.CircleRadius;break;}
            float beat=.5f+.5f*Mathf.Sin(t*14),a=toStrike>0?1:Mathf.Clamp01(1+toStrike/.6f);if(a<=0)return;
            var band=new Rect(W*.5f-240,92,480,34);U.Round(band,C.Navy(.88f*a),6,new Color(1,.2f,.12f,(.5f+.5f*beat)*a));U.Text(band,caster.Seat.name+"  ·  GRAND FINALE",15,new Color(1,.86f,.42f,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);
            if(toStrike>0)for(int s=0;s<2;s++){var edge=new Rect(s==0?0:W-14,0,14,H);U.Fill(edge,new Color(1,.1f,.06f,(mine?.5f:.18f)*(.4f+.6f*beat)*a));}
            if(mine&&toStrike>0){float size=30+(prefs.reducedEffects?0:6*beat);U.Text(new Rect(2,H*.5f+44,W,44),"RUN",Mathf.RoundToInt(size),C.Void(.8f),TextAnchor.MiddleCenter,bold:true,mono:true);U.Text(new Rect(0,H*.5f+42,W,44),"RUN",Mathf.RoundToInt(size),new Color(1,.25f,.15f,1),TextAnchor.MiddleCenter,bold:true,mono:true);}
        }
    }
    public sealed partial class NativeAudio
    {
        private readonly NativeKitSound[] bombSounds=new NativeKitSound[4];private int bombIntroCue;private float bombIntroLast=-1;
        // A blast's sound once, when the record first shows it; the Finale's beeps; the entrance's own cues (it has no music: see NativeIntroRules.IntroSting).
        private void BombMoments(NativePilot p,Vector3 pos,float t,bool mine)
        {
            var k=NativeBombs.Source?.Of(p.Seat.id);if(k==null)return;
            if(k.Blasts!=null)foreach(var b in k.Blasts){if(b==null||t<b.At||t-b.At>=.15f||!Once(p.Seat.id+"bomb"+b.Id,b.At))continue;int n=NativeBombSounds.Blast(b,bombSounds);for(int i=0;i<n;i++)Cue(bombSounds[i].Key,bombSounds[i].Clip,new Vector3(b.Pos.x,b.Pos.y,b.Pos.z),bombSounds[i].Gain,mine:mine);}
            if(k.CastKind!=null&&t>=k.CastRelease&&t-k.CastRelease<.15f&&Once(p.Seat.id+"bombcast"+k.CastKind,k.CastRelease)){int n=NativeBombSounds.Cast(k.CastKind,bombSounds);for(int i=0;i<n;i++)Cue(bombSounds[i].Key,bombSounds[i].Clip,pos,bombSounds[i].Gain,mine:mine);}
            var f=k.Finale;if(f!=null&&t>=f.At&&t<f.Strike){float beats=t<f.Lock?5:14,step=Mathf.Floor((t-f.At)*beats);if(Once(p.Seat.id+"bombbeep",f.At+step*1000))Cue("ui.countdown","ui",new Vector3(f.Box.x,f.Box.y,f.Box.z),t<f.Lock?.6f:.9f,mine:true);}
            if(f!=null&&t>=f.Lock&&t-f.Lock<.15f&&Once(p.Seat.id+"bomblock",f.Lock))Cue("impact.metal","metal",new Vector3(f.Box.x,f.Box.y,f.Box.z),1,mine:true);
        }
        /// <summary>The Last Warning's cues on the entrance clock, each once as `age` passes it.</summary>
        public void BombIntro(float age)
        {
            if(age<bombIntroLast-.2f)bombIntroCue=0;bombIntroLast=age;var list=NativeBombSounds.Intro;while(bombIntroCue<list.Length&&age>=list[bombIntroCue].At){var c=list[bombIntroCue++];if(age-c.At<.25f)UiCue(c.Key,c.Clip,c.Gain);}
        }
    }
}
