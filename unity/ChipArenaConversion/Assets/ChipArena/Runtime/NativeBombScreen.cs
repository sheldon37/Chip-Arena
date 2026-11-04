using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;

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
