using System;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The fight HUD as main.ts / match-hud.css lay it out: vitals bottom left and cooldown cards bottom right (24 px from
    // the true window edges), the SET / ROUND tag top centre, prompts centred, and a crosshair and hit marker drawn in
    // physical pixels on the exact screen centre the aim ray uses. Values are read from the simulation as before. Styled
    // in the cyber theme (NativeHudChrome, NativeCyberTheme palette, rules in Core/NativeHudCyber): holographic chamfered
    // panels, a segmented HP bar with its damage trail, cooldown cards with a cyan sweep and a magenta ready flare, and the
    // glitch accents only on events (a hit, dropping into low health, a card turning ready), none with Reduced effects.
    public sealed partial class ConversionGame
    {
        private readonly NativeHudEvents hudEvents=new NativeHudEvents();private int hudEventGeneration=-1;
        // Event slots: 0 HP drop, 1 low health, 2.. the cooldown cards (in their fixed order).
        private const int HpSlot=0,LowSlot=1,CardSlot=2;
        private void FightHud(NativePilot p)
        {
            float W=U.W,H=U.H;if(hudEventGeneration!=arena.Generation){hudEvents.Clear();hudEventGeneration=arena.Generation;}
            if(arena.Pve==null)SetTag("SET "+match.Set+"  ·  ROUND "+match.Round,NativeCyberTheme.Cyan(),W);
            var box=HealthBox(p,24,H-24);Cooldowns(p,W-24,H-24);
            if(Event.current.type!=EventType.Repaint)return;
            HealReadout(box);
            if(match.Phase==NativePhase.Fight&&(p.Seat.spec=="ansatsuken"||p.Seat.spec=="emperor"))FighterHud(p,W,H);
            if(p.Seat.spec=="siren")SirenHud(p,W);
            Hitmarker();BowMeter(p);
        }
        // The top-centre status tag (SET / ROUND, the PvE stage): a chamfered navy tag 26 high at y 20, mono 12 tracked,
        // with a tone diamond either side.
        private static void SetTag(string text,Color tone,float W,float alpha=1)
        {
            float tw=U.Measure(text,12,false,true,.08f).x,sw=tw+44;var sr=new Rect(W/2-sw/2,20,sw,26);
            NativeHudChrome.Cut(sr,NativeCyberTheme.Navy(NativeHudCyber.PanelAlpha*alpha),NativeHudCyber.TagCut,NativeHudChrome.Corners.All);NativeHudChrome.Outline(sr,U.A(tone,.5f*alpha),NativeHudCyber.TagCut,NativeHudChrome.Corners.All);
            for(int side=-1;side<=1;side+=2){var d=new Vector2(sr.center.x+side*(sw/2-12),sr.center.y);U.Push(d,Vector2.zero,1,45);U.Fill(new Rect(d.x-2.5f,d.y-2.5f,5,5),U.A(tone,alpha));U.Pop();}
            U.Text(sr,text,12,NativeCyberTheme.Ink(alpha),TextAnchor.MiddleCenter,mono:true,tracking:.08f);
        }
        // .healthbox: weapon (its 15 px spec icon, 7 px gap, the name), the HP readout (rounded up, over the max), the
        // segmented HP bar with its damage trail, the weapon resource and, off melee, stamina. Low health (30%) turns the
        // vitals to the danger tone with a breathing edge; a hit glitches the readout and slices the panel for .18 s.
        // Returns the panel's rect (the first-person heal readout sits over it).
        private Rect HealthBox(NativePilot p,float x,float bottom)
        {
            bool melee=p.Weapon.@class=="melee",magic=p.Weapon.@class=="magic",ranged=p.Weapon.@class=="ranged";float w=256,pad=14,h=11+24+6+8+10+14+5+3+(melee?0:9+14+5+3)+12;var r=new Rect(x,bottom-h,w,h);
            SprintTag(p,r);
            float t=arena.Time,maxHp=Mathf.Max(1,(float)p.Stats["hp"]),hp=Mathf.Clamp01(p.Hp/maxHp);bool low=NativeHudCyber.Low(hp),reduced=prefs.reducedEffects;
            float hit=hudEvents.Drop(HpSlot,t,p.Hp),dropped=hudEvents.Rise(LowSlot,t,low),burst=Mathf.Min(hit,dropped);
            Color tone=low?NativeCyberTheme.Danger():NativeCyberTheme.Cyan();float breath=low?NativeHudCyber.Breath(Time.unscaledTime,reduced):1;
            if(low)U.Glow(r,NativeCyberTheme.Danger(.22f*breath),14);
            NativeHudChrome.Panel(r,1,U.A(tone,low?breath:1));if(low)NativeHudChrome.Outline(r,NativeCyberTheme.Danger(.55f*breath),NativeHudCyber.Cut);
            NativeHudChrome.Slice(r,hit,1,reduced,tone);
            float y=r.y+11,ix=r.x+pad,iw=w-2*pad;
            // Header: icon and name on the left, the HP readout (20 px mono, then "/ max") on the right.
            string hpText=NativeHudText.Int(Mathf.CeilToInt(Math.Max(0,p.Hp))),maxText="/ "+NativeHudText.Int(Mathf.RoundToInt(maxHp));float mw=U.Measure(maxText,11,false,true).x,room=iw-22-(U.Measure(hpText,20,true,true).x+mw+5)-8;
            NativeIcons.Draw(new Rect(ix,y+5,15,15),NativeIcons.Spec(p.Weapon.id),NativeCyberTheme.CyanSoft());U.Text(new Rect(ix+22,y+3,room,18),Fit(p.Weapon.name.ToUpperInvariant(),11,room,true,.03f),11,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.03f);
            var hpLine=new Rect(ix,y,iw-mw-5,24);NativeHudChrome.Glitch(hpLine,hpText,20,burst,1,TextAnchor.MiddleRight,reduced);
            U.Text(hpLine,hpText,20,low?NativeCyberTheme.Danger():NativeCyberTheme.Ink(),TextAnchor.MiddleRight,bold:true,mono:true,glow:low?NativeCyberTheme.Danger(.35f*breath):NativeCyberTheme.Cyan(.18f));
            U.Text(new Rect(ix,y+5,iw,18),maxText,11,NativeCyberTheme.Muted(),TextAnchor.MiddleRight,mono:true);y+=24+6;
            // The HP bar: segments of NativeHudCyber.SegmentStep (10 HP at 100), the damage trail, a cyan (danger when low) fill.
            var bar=new Rect(ix,y,iw,8);float trail=DamageTrail(p,hp);
            NativeHudChrome.Bar(bar,hp*maxHp,maxHp,10,tone,NativeCyberTheme.Edge(.55f),trail*maxHp,NativeCyberTheme.Ink(.78f));y+=8+10;
            string label,value;float fill;Color resource;
            if(ranged){int cap=(int)Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]);label=p.ReloadEnd>arena.Time?"RELOADING "+(p.ReloadEnd-arena.Time).ToString("0.0")+"s":"MAGAZINE";value=p.Ammo+" / "+cap;fill=cap>0?p.Ammo/(float)cap:0;resource=NativeCyberTheme.Warn();
                ResourceRow(ix,y,iw,label,value,resource);y+=14+5;
                if(p.ReloadEnd>arena.Time)NativeHudChrome.Line(new Rect(ix,y,iw,3),NativeHudCyber.Reload(arena.Time,p.ReloadAt,p.ReloadEnd),NativeCyberTheme.CyanSoft(),NativeCyberTheme.Edge(.55f));
                else NativeHudChrome.Bar(new Rect(ix,y,iw,3),p.Ammo,cap,16,resource,NativeCyberTheme.Edge(.55f),gap:2);y+=3;}
            else
            {
                float max=magic?(float)p.Stats["maxMana"]:(float)p.Stats["maxStamina"],now=magic?p.Mana:p.Stamina;label=magic?"MANA":"STAMINA";value=Mathf.FloorToInt(now)+" / "+Mathf.RoundToInt(max);fill=max>0?now/max:0;
                resource=magic?NativeCyberTheme.Violet():p.Pose.Sprinting?NativeCyberTheme.Warn():NativeCyberTheme.Good();
                ResourceRow(ix,y,iw,label,value,magic?NativeCyberTheme.CyanSoft():resource);y+=14+5;NativeHudChrome.Line(new Rect(ix,y,iw,3),fill,resource,NativeCyberTheme.Edge(.55f));y+=3;
            }
            if(!melee){float max=(float)p.Stats["maxStamina"];y+=9;ResourceRow(ix,y,iw,"STAMINA",Mathf.FloorToInt(p.Stamina)+" / "+Mathf.RoundToInt(max),p.Pose.Sprinting?NativeCyberTheme.Warn():NativeCyberTheme.Good());y+=14+5;
                NativeHudChrome.Line(new Rect(ix,y,iw,3),max>0?p.Stamina/max:0,p.Pose.Sprinting?NativeCyberTheme.Warn():NativeCyberTheme.Good(),NativeCyberTheme.Edge(.55f));}
            return r;
        }
        // The first-person own-heal total (NativeHealReadout, fed by the combat numbers in place of the view pilot's heal
        // numbers): small heal-green mono right-aligned over the HP readout, at reduced opacity with the numbers' shadow and
        // outline; it counts up while heals come, then drifts up and fades (no motion with Reduced effects).
        private void HealReadout(Rect box)
        {
            var heals=combatTrack.Heals;float t=arena.Time;bool reduced=prefs.reducedEffects;float a=heals.Alpha(t,reduced);if(a<=0)return;
            NativeHealReadout.Place(box.x,box.y,box.width,heals.Lift(t,reduced),out float x,out float y,out float w,out float h);
            U.Text(new Rect(x,y,w,h),heals.Label,NativeHealReadout.Size,U.Hex(NativeCombatNumbers.HealColor,a),TextAnchor.MiddleRight,bold:true,mono:true,
                glow:U.Hex(NativeCombatNumbers.HealOutline,.8f*a),shadow:U.Hex(NativeCombatNumbers.HealShadow,a));
        }
        // A resource label (10 px mono, tracked, muted) and its value (13 px mono in the resource's tone).
        private static void ResourceRow(float x,float y,float w,string label,string value,Color tone)
        {U.Text(new Rect(x,y,w,14),label,10,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft,mono:true,tracking:.12f);U.Text(new Rect(x,y,w,14),value,13,tone,TextAnchor.MiddleRight,mono:true);}
        // A card's state (an optional line icon ahead of the name; the ready, active, recast and low flags are what a new ability's states set: the Fan the Hammer and
        // Overdrive cards plug in by filling them; the drawing reads nothing else). Dial: the cooldown done, 0..1, or -1.
        private struct Card {public string Key,State,Sub,Icon;public bool Ready,Active,Recast,Low;public float Dial;}
        private readonly NativeMeleeKitHud.Span qSpan=new NativeMeleeKitHud.Span(),eSpan=new NativeMeleeKitHud.Span(),blockSpan=new NativeMeleeKitHud.Span(),rollSpan=new NativeMeleeKitHud.Span();
        // The dial of a cooldown the HUD knows only by its ready time (block, roll): the span it rose to (Span), 1 once ready.
        private static float SpanDial(NativeMeleeKitHud.Span span,float remaining){float total=span.Update(remaining,1);return remaining>0?1-Mathf.Clamp01(remaining/Mathf.Max(.01f,total)):1;}
        private NativeMeleeKitHud.Card MeleeCard(NativePilot p,string slot,ContentAbility d)
        {
            float t=arena.Time,remaining=(slot=="q"?p.QReady:p.EReady)-t,span=(slot=="q"?qSpan:eSpan).Update(remaining,(float)NativeAbilityRules.Cooldown(d,p.Stats));
            return NativeMeleeKitHud.Of(slot,d.id,p.MeleeKit,remaining,span,p.Stamina,(float)NativeAbilityRules.Cost(d,p.Stats),t);
        }
        private readonly System.Collections.Generic.List<Card> cards=new System.Collections.Generic.List<Card>();
        // .cooldowns: RMB block, SHIFT roll and the Q / E cards, right to left from the corner.
        private void Cooldowns(NativePilot p,float right,float bottom)
        {
            float t=arena.Time;cards.Clear();
            bool away=NativeMeleeKitHud.ShieldAway(p.Seat.spec,p.MeleeKit);
            cards.Add(new Card{Key="RMB · "+(p.Seat.spec=="ansatsuken"?"PARRY":"BLOCK"),State=away?"SHIELD OUT":t<p.BlockEnd?"ACTIVE":p.BlockReady>t?(p.BlockReady-t).ToString("0.0")+"s":"READY",Ready=!away&&t>=p.BlockEnd&&p.BlockReady<=t,Active=!away&&t<p.BlockEnd,Low=away,Dial=away?-1:SpanDial(blockSpan,p.BlockReady-t)});
            bool roll=p.Weapon.@class=="melee";string rollState=t<p.RollEnd?"ROLLING":p.RollReady>t?(p.RollReady-t).ToString("0.0")+"s":"READY";cards.Add(new Card{Key="SHIFT · "+(roll?"ROLL":"DODGE"),State=rollState,Ready=rollState=="READY",Active=t<p.RollEnd,Dial=SpanDial(rollSpan,p.RollReady-t)});
            foreach(var slot in new[]{"q","e"})
            {
                var d=content.Ability(p.Seat.spec,slot);
                // Standard melee Q/E (Core/NativeMeleeKitHud): the recast window, the cast, the cooldown actually started, stamina.
                if(d!=null&&NativeMeleeRules.Standard(p.Seat.spec)){var kc=MeleeCard(p,slot,d);cards.Add(new Card{Key=slot.ToUpperInvariant()+" · "+d.name.ToUpperInvariant(),State=kc.State,Ready=kc.Ready,Active=kc.Active,Dial=kc.Dial,Recast=kc.Recast,Low=kc.Low&&!kc.Recast});continue;}
                // The pistol / machine-gun Q kit (Core/NativeGunKitHud): its icon, fanning, Overdrive's active time and vent, cooldown, ammo.
                if(d!=null&&slot=="q"&&NativeGunKit.Covers(d.id))
                {
                    float qLeft=p.QReady-t,span=qSpan.Update(qLeft,(float)NativeAbilityRules.Cooldown(d,p.Stats));var gc=NativeGunKitHud.Of(d.id,NativeGunKit.Of(p.Pose,t),t,qLeft,span,p.Ammo,p.ReloadEnd>t);
                    cards.Add(new Card{Key="Q · "+d.name.ToUpperInvariant(),State=gc.State,Icon=gc.Icon,Ready=gc.Ready,Active=gc.Active,Low=gc.Venting||gc.Low,Dial=gc.Dial});continue;
                }
                // The bow's Volley and Rain of Arrows (Core/NativeBowHud): LOOSING, MARKED / RAINING, the cooldown, their icons.
                if(d!=null&&NativeBowHud.Covers(d.id)){float bowLeft=(slot=="q"?p.QReady:p.ECharges>0?t:p.EReady)-t,span=(slot=="q"?qSpan:eSpan).Update(bowLeft,(float)NativeAbilityRules.Cooldown(d,p.Stats));var bc=NativeBowHud.Of(d.id,NativeBowWire.Of(p.Pose),t,bowLeft,span);
                    cards.Add(new Card{Key=slot.ToUpperInvariant()+" · "+d.name.ToUpperInvariant(),State=bc.State,Icon=bc.Icon,Ready=bc.Ready,Active=bc.Active,Low=bc.Low,Dial=bc.Dial});continue;}
                string status=AbilityStatus(p,slot);if(string.IsNullOrEmpty(status)||d==null&&slot=="q")continue;
                int dot=status.IndexOf(" · ",StringComparison.Ordinal);string name,state;
                if(dot>0){name=status.Substring(0,dot).Replace("  "," · ").ToUpperInvariant();state=status.Substring(dot+3);}
                else
                {   // A mode without a cooldown ("Q  Aim (hold)"): its name as the key, the parenthesised hint as the state.
                    int paren=status.IndexOf(" (",StringComparison.Ordinal);name=(paren>0?status.Substring(0,paren):status).Replace("  "," · ").ToUpperInvariant();state=paren>0?status.Substring(paren+2).TrimEnd(')'):"";
                }
                float left=d==null?Math.Max(0,p.EReady-t):slot=="q"?p.QReady-t:p.ECharges>0?0:p.EReady-t,cd=d!=null&&d.cooldown>0?(float)d.cooldown:1;
                cards.Add(new Card{Key=name,State=state.Length>0&&char.IsDigit(state[0])?state:state.ToUpperInvariant(),Ready=state=="ready",Active=state=="casting"||state=="active",Low=state=="low mana"||state=="low stamina",Dial=d!=null&&d.kind!="ads"?1-Mathf.Clamp01(left/cd):-1});
            }
            float x=right;for(int i=cards.Count-1;i>=0;i--){var c=cards[i];float since=hudEvents.Rise(CardSlot+i,t,c.Ready);x=CardAt(c,x,bottom,since)-6;}
        }
        // One card ending at `right`: the keycap and name, the state in its tone (bright cyan READY with the magenta tick, dial
        // and edge, cyan active, amber recast, danger low, ink counting), the radial dial and the bottom sweep of the cooldown done, and the ready flare. Returns
        // its left edge.
        private float CardAt(Card c,float right,float bottom,float since)
        {
            int dot=c.Key.IndexOf(" · ",StringComparison.Ordinal);string key=dot>0?c.Key.Substring(0,dot):null,name=dot>0?c.Key.Substring(dot+3):c.Key;
            float kh=14,kw=key!=null?NativeHudChrome.KeyWidth(key,kh):0,ic=c.Icon!=null?kh+4:0,nw=U.Measure(name,10,false,true,.06f).x+ic,vw=U.Measure(c.State,14,true,true,c.Ready?.1f:0).x;
            bool dial=c.Dial>=0;float w=Mathf.Max(84,Mathf.Max(kw+(kw>0?6:0)+nw,vw)+22+(dial?14:0)),h=8+14+6+16+8;var r=new Rect(right-w,bottom-h,w,h);bool reduced=prefs.reducedEffects;
            Color tone=c.Recast?NativeCyberTheme.Warn():c.Low?NativeCyberTheme.Danger():c.Active?NativeCyberTheme.Cyan():c.Ready?NativeCyberTheme.Magenta():NativeCyberTheme.Edge();
            if(c.Active&&!c.Recast)U.Glow(r,NativeCyberTheme.Cyan(.16f),10);NativeHudChrome.Flare(r,since,1,reduced,true);
            NativeHudChrome.Cut(r,NativeCyberTheme.Navy(NativeHudCyber.PanelAlpha),NativeHudCyber.CardCut);NativeHudChrome.Scan(r,NativeCyberTheme.Cyan(NativeHudCyber.ScanAlpha),NativeHudCyber.CardCut);
            NativeHudChrome.Outline(r,c.Active||c.Recast||c.Low?U.A(tone,.8f):c.Ready?NativeCyberTheme.Magenta(.45f):NativeCyberTheme.Edge(.95f),NativeHudCyber.CardCut);
            if(c.Ready)U.Fill(new Rect(r.x+NativeHudChrome.Snap(1),r.y+12,NativeHudChrome.Snap(2),r.height-24),NativeCyberTheme.Magenta());
            float lx=r.x+11,ly=r.y+8;if(key!=null){NativeHudChrome.Key(new Rect(lx,ly,kw,kh),key);lx+=kw+6;}
            // A kit card's line icon (Lucide, NativeIcons) ahead of its name, in the card's tone.
            if(c.Icon!=null){NativeIcons.Draw(new Rect(lx,ly,kh,kh),c.Icon,c.Low||c.Active||c.Recast?tone:c.Ready?NativeCyberTheme.Ink():NativeCyberTheme.Muted());lx+=kh+4;}
            U.Text(new Rect(lx,ly,r.xMax-lx-(dial?18:8),kh),name,10,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft,mono:true,tracking:.06f);
            var line=new Rect(r.x+11,ly+kh+6,w-22,16);Color ink=c.Recast||c.Low||c.Active?tone:c.Ready?NativeCyberTheme.CyanSoft():NativeCyberTheme.Ink();
            NativeHudChrome.Glitch(line,c.State,14,since,1,TextAnchor.MiddleLeft,reduced,tracking:c.Ready?.1f:0);
            U.Text(line,c.State,14,ink,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:c.Ready?.1f:0,glow:c.Ready?NativeCyberTheme.Magenta(.3f):default);
            if(dial)
            {
                var d=new Rect(r.xMax-19,r.y+7,12,12);U.Disc(d,NativeCyberTheme.Edge(.9f));Color dc=c.Low?NativeCyberTheme.Danger():c.Recast?NativeCyberTheme.Warn():c.Ready?NativeCyberTheme.Magenta():NativeCyberTheme.Cyan();
                U.Pie(d,dc,Mathf.Max(.01f,c.Dial));U.Disc(new Rect(d.x+3.5f,d.y+3.5f,5,5),NativeCyberTheme.Navy());
                // The sweep: the cooldown done along the card's bottom edge (full once ready).
                float sw=w-2*NativeHudCyber.CardCut-6;var s=new Rect(r.x+NativeHudCyber.CardCut+3,r.yMax-NativeHudChrome.Snap(2)-2,sw,NativeHudChrome.Snap(2));U.Fill(s,NativeCyberTheme.Edge(.7f));U.Fill(new Rect(s.x,s.y,sw*Mathf.Clamp01(c.Dial),s.height),U.A(dc,.9f));
            }
            NativeHudChrome.Flare(r,since,1,reduced,false);
            return r.x;
        }
        // style.css .crosshair (a 5 px dot with 12 px arms 10 px either side) at the pixel-rounded centre where
        // ViewportPointToRay(.5,.5) aims. Crisp: identity matrix. Hitmarker draws the hit marker.
        private void Crosshair()
        {
            if(Event.current.type!=EventType.Repaint)return;var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;float k=Mathf.Max(1,Mathf.Round(Screen.height/1080f)),cx=Mathf.Round(Screen.width/2f),cy=Mathf.Round(Screen.height/2f);
            void Px(float x,float y,float w,float h,Color c){var o=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=o;}
            var shadow=NativeCyberTheme.Void(.8f);var ink=NativeCyberTheme.Ink();float d=Mathf.Round(5*k),arm=12*k,gap=10*k,line=Mathf.Max(1,Mathf.Round(k));
            Px(cx-d/2-1,cy-d/2-1,d+2,d+2,shadow);Px(cx-d/2,cy-d/2,d,d,ink);Px(cx-d/2-gap-arm,cy-line/2,arm,line,ink);Px(cx+d/2+gap,cy-line/2,arm,line,ink);
            GUI.matrix=old;
        }
        // ---- Hit marker (client/hitmarker.ts, rules in NativeHitmarker) ----------------------------------------------------
        // In the fight and result phases (main.ts: active, not paused, not the killcam), in physical pixels like the crosshair.
        private readonly NativeHitmarker hitmarker=new NativeHitmarker();private NativeHitmarker.Classify hitTarget;
        private static readonly Vector2[] HitDirections={new Vector2(-1,-1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(1,1)};
        private static readonly string FlameMarkup=HitIcon(NativeHitmarker.FlamePath),PoisonMarkup=HitIcon(NativeHitmarker.PoisonPath);
        private static string HitIcon(string d)=>"<path d=\""+d+"\" fill=\"none\" stroke=\"#fff\" stroke-width=\""+NativeHitmarker.IconStroke.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\" stroke-linejoin=\"round\"/>";
        private NativeHitmarker.Target HitTarget(int id)
        {
            var me=arena.Local;foreach(var q in arena.Pilots)if(q.Seat.id==id)return q==me||match.SameSide(q.Seat,me.Seat)?NativeHitmarker.Target.Ally:NativeHitmarker.Target.Enemy;
            foreach(var m in arena.Minions)if(m.Id==id)return m.Pve!=null?NativeHitmarker.Target.Add:NativeHitmarker.Target.Other;
            return NativeHitmarker.Target.Other;
        }
        private void Hitmarker()
        {
            if(match.Phase!=NativePhase.Fight&&match.Phase!=NativePhase.Result||arena.Local==null){hitmarker.Clear();return;}
            hitTarget??=HitTarget;var m=hitmarker.Update(arena.Events,arena.Time,arena.Generation,arena.Local.Seat.id,arena.Pve!=null,hitTarget);if(!m.Shown)return;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;float k=Mathf.Max(1,Mathf.Round(Screen.height/1080f)),cx=Mathf.Round(Screen.width/2f),cy=Mathf.Round(Screen.height/2f),box=NativeHitmarker.Box*k;
            if(m.Visible)
            {
                // .hitmark-cross (scaled about its centre): the four strokes and, on the crisp marker, the corner tips; square
                // caps lengthen each by its width. A stroke under a pixel wide (the .68x dot: .86 px) draws one pixel wide at
                // its coverage, as the browser antialiases it. drop-shadow(0 1px 2px #0009) as a wider dark copy 1 px lower.
                float s=m.Scale*box,w=NativeHitmarker.Stroke*s,a=m.Opacity*Mathf.Min(1,w);w=Mathf.Max(1,w);Color c=U.Hex(m.Headshot?NativeHitmarker.Headshot:NativeHitmarker.Ink,a),shade=U.Hex(NativeHitmarker.ShadowColor,NativeHitmarker.ShadowAlpha*.75f*a);
                foreach(var dir in HitDirections)
                {
                    HitStroke(cx,cy,dir,s*NativeHitmarker.StrokeIn,s*NativeHitmarker.StrokeOut,w,k,c,shade);
                    if(!m.Dot)HitStroke(cx,cy,dir,s*NativeHitmarker.TipIn,s*NativeHitmarker.TipOut,w,k,c,shade);
                }
            }
            GUI.matrix=old;
            if(m.Elements==0)return;
            // .hitmark-elements: 15 px icons 4 px apart, centred 3 px under the 42 px box (not scaled with the cross).
            float icon=NativeHitmarker.Icon*k,gap=NativeHitmarker.IconGap*k,row=m.Elements*icon+(m.Elements-1)*gap,x=cx-row/2,y=cy-box/2+box+NativeHitmarker.IconTop*k,S=U.S;
            for(int i=0;i<m.Elements;i++)
            {
                int element=i==0?m.First:m.Second;bool fire=element==NativeHitmarker.Fire;string markup=fire?FlameMarkup:PoisonMarkup,key=fire?"hitmark-fire":"hitmark-poison";
                var r=new Rect(x/S,y/S,icon/S,icon/S);NativeIcons.DrawMarkup(new Rect(r.x,r.y+k/S,r.width,r.height),key,markup,24,U.Hex(NativeHitmarker.ShadowColor,NativeHitmarker.ShadowAlpha*.6f));
                NativeIcons.DrawMarkup(r,key,markup,24,U.Hex(fire?NativeHitmarker.FireColor:NativeHitmarker.PoisonColor));x+=icon+gap;
            }
        }
        // One diagonal stroke from `from` to `to` (offsets along each axis) out of the centre, in physical pixels.
        private static void HitStroke(float cx,float cy,Vector2 dir,float from,float to,float w,float k,Color c,Color shade)
        {
            Vector2 a0=new Vector2(cx,cy)+dir*from,a1=new Vector2(cx,cy)+dir*to,mid=(a0+a1)/2;float len=(a1-a0).magnitude+w,deg=dir.x*dir.y>0?45:-45;var o=GUI.color;
            HitBar(mid+new Vector2(0,k),deg,len+2*k,w+2*k,shade);HitBar(mid,deg,len,w,c);GUI.color=o;
        }
        private static void HitBar(Vector2 mid,float degrees,float len,float w,Color c)
        {GUIUtility.RotateAroundPivot(degrees,mid);GUI.color=c;GUI.DrawTexture(new Rect(mid.x-len/2,mid.y-w/2,len,w),Texture2D.whiteTexture);GUI.matrix=Matrix4x4.identity;}

        // ---- Sprint tag (client/sprint-hud.ts) ----------------------------------------------------------------------------
        // .healthbox .status-tags: bottom 8 px over the box's padding edge, left at its border. SPRINTING with the » chevron
        // swaying ±2 px (held still with reduced effects, as prefers-reduced-motion stops it), or SPRINT DEPLETED · RELEASE W.
        // The host's pilot (and offline) knows it is exhausted; a guest shows it once its pose carries SprintExhausted.
        private void SprintTag(NativePilot p,Rect box)
        {
            if(Event.current.type!=EventType.Repaint)return;
            int state=NativeSprintTag.State(match.Phase==NativePhase.Fight,p.Hp,p.Pose.Sprinting,p.SprintExhausted||p.Pose.SprintExhausted);if(state==NativeSprintTag.None)return;
            bool tired=state==NativeSprintTag.Exhausted;string text=NativeSprintTag.Text(state);float size=tired?NativeSprintTag.ExhaustedSize:NativeSprintTag.Size,track=tired?NativeSprintTag.ExhaustedTracking:NativeSprintTag.Tracking;
            // Cyber tones: amber while sprinting (the stamina bar's sprint tone), danger once depleted.
            Color ink=tired?NativeCyberTheme.Danger():NativeCyberTheme.Warn(),glow=U.A(ink,NativeSprintTag.GlowAlpha);
            // letter-spacing follows every glyph (the last included), as in the browser.
            float tw=U.Measure(text,size,true,true,track).x+track*size,chevron=tired?0:U.Measure(NativeSprintTag.Chevron,NativeSprintTag.ChevronSize,true,true).x+track*size;
            float line=tired?size*NativeSprintTag.LineHeight:Mathf.Max(size*NativeSprintTag.LineHeight,NativeSprintTag.ChevronLine);
            float b=NativeSprintTag.Border,w=tw+(tired?0:chevron+NativeSprintTag.Gap)+2*NativeSprintTag.PadX+2*b,h=line+2*NativeSprintTag.PadY+2*b;
            var r=new Rect(box.x+1,box.y+1-NativeSprintTag.Above-h,w,h);NativeHudChrome.Cut(r,NativeCyberTheme.Navy(NativeHudCyber.PanelAlpha),NativeHudCyber.TagCut);NativeHudChrome.Outline(r,U.A(ink,.85f),NativeHudCyber.TagCut,NativeHudChrome.Corners.Diagonal,b);
            float x=r.x+b+NativeSprintTag.PadX,y=r.y+b+NativeSprintTag.PadY;
            if(!tired)
            {
                bool still=prefs.reducedEffects;float phase=still?.5f:NativeSprintTag.ChevronPhase(Time.unscaledTime),shift=still?0:Mathf.Lerp(-NativeSprintTag.ChevronShift,NativeSprintTag.ChevronShift,phase),alpha=still?1:Mathf.Lerp(NativeSprintTag.ChevronDim,1,phase);
                U.Text(new Rect(x+shift,y,chevron,line),NativeSprintTag.Chevron,NativeSprintTag.ChevronSize,U.A(ink,alpha),TextAnchor.MiddleLeft,bold:true,mono:true,glow:U.A(glow,alpha));x+=chevron+NativeSprintTag.Gap;
            }
            U.Text(new Rect(x,y,tw,line),text,size,ink,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:track,glow:glow);
        }
        // Width of a monospace space at `size` (CalcSize may drop a trailing one), for the source's "<label> <b>" gaps.
        private static float SpaceWidth(float size,bool bold=false)=>U.Measure("a a",size,bold,true).x-U.Measure("aa",size,bold,true).x;

        // ---- DEV fighter HUD (main.ts drawHud, rules in NativeArcadeHud) -------------------------------------------------
        // Ansatsuken: 1 HADOUKEN and 2 UPPERCUT, the Shinku hold bar on its own line while charging, and the combo meter.
        // Emperor: Q ELECTRIC (the attack cooldown, as the source's p.cooldown) and its red combo meter. The fighter state
        // is replicated (NativePilotFrame.fighter), so host, guests and offline read the same values.
        private readonly float[] arcadeWidths=new float[2];private readonly string[] arcadeStates=new string[2];
        private static readonly string[] ArcadeLabels={"1 "+NativeArcadeHud.Hadouken,"2 "+NativeArcadeHud.Uppercut},EmperorLabels={"Q "+NativeArcadeHud.Electric};
        private void FighterHud(NativePilot p,float W,float H)
        {
            float t=arena.Time;var f=p.Fighter;bool emperor=p.Seat.spec=="emperor";
            if(emperor){float cd=p.Ready-t;arcadeStates[0]=cd>0?NativeHudText.Seconds(cd):NativeArcadeHud.Ready;}
            else{float hd=f!=null?f.HadokenReady-t:0,up=f!=null?f.UppercutReady-t:0;arcadeStates[0]=hd>0?NativeHudText.Seconds(hd):NativeArcadeHud.Ready;arcadeStates[1]=up>0?NativeHudText.Seconds(up):NativeArcadeHud.Ready;}
            var labels=emperor?EmperorLabels:ArcadeLabels;int n=labels.Length;float space=SpaceWidth(NativeArcadeHud.SpecialSize),row=0,cardH=2*NativeArcadeHud.SpecialPadY+NativeArcadeHud.SpecialLine+2*NativeArcadeHud.Border;
            for(int i=0;i<n;i++){arcadeWidths[i]=U.Measure(labels[i],NativeArcadeHud.SpecialSize,false,true).x+space+NativeArcadeHud.StateGap+U.Measure(arcadeStates[i],NativeArcadeHud.StateSize,true,true).x+2*NativeArcadeHud.SpecialPadX+2*NativeArcadeHud.Border;row+=arcadeWidths[i]+(i>0?NativeArcadeHud.ItemGap:0);}
            // The flex container is as wide as all its items on one line; the charge bar then wraps to a line of that width
            // and the cards are centred above it.
            bool charging=!emperor&&f?.Kind=="charge";string chargeText=charging?NativeArcadeHud.ChargeText(f.Charge):null;float chargeH=2*NativeArcadeHud.ChargePadY+NativeArcadeHud.ChargeLine+2*NativeArcadeHud.ChargeBorder;
            float width=charging?row+NativeArcadeHud.ItemGap+Mathf.Max(NativeArcadeHud.ChargeMin,U.Measure(chargeText,NativeArcadeHud.ChargeSize,true,true).x+2*NativeArcadeHud.ChargePadX+2*NativeArcadeHud.ChargeBorder):row;
            float right=W-NativeArcadeHud.Right,bottom=H-NativeArcadeHud.Bottom,top=bottom-cardH-(charging?NativeArcadeHud.ItemGap+NativeArcadeHud.ChargeTop+chargeH:0),x=right-width+(width-row)/2;
            Color edge=emperor?NativeCyberTheme.Danger(.7f):NativeCyberTheme.Cyan(.8f),ink=NativeCyberTheme.Ink(),stateInk=NativeCyberTheme.Warn();
            for(int i=0;i<n;i++)
            {
                var r=new Rect(x,top,arcadeWidths[i],cardH);NativeHudChrome.Cut(r,NativeCyberTheme.Navy(NativeHudCyber.PanelAlpha),NativeHudCyber.TagCut);NativeHudChrome.Outline(r,edge,NativeHudCyber.TagCut);
                float lx=r.x+NativeArcadeHud.Border+NativeArcadeHud.SpecialPadX,lw=U.Measure(labels[i],NativeArcadeHud.SpecialSize,false,true).x;var inner=new Rect(lx,r.y+NativeArcadeHud.Border,r.width,r.height-2*NativeArcadeHud.Border);
                U.Text(new Rect(lx,inner.y,lw+2,inner.height),labels[i],NativeArcadeHud.SpecialSize,ink,TextAnchor.MiddleLeft,mono:true);
                U.Text(new Rect(lx+lw+space+NativeArcadeHud.StateGap,inner.y,r.xMax-lx,inner.height),arcadeStates[i],NativeArcadeHud.StateSize,arcadeStates[i]==NativeArcadeHud.Ready?NativeCyberTheme.Magenta():stateInk,TextAnchor.MiddleLeft,bold:true,mono:true);x=r.xMax+NativeArcadeHud.ItemGap;
            }
            if(charging)
            {
                var r=new Rect(right-width,bottom-chargeH,width,chargeH);float bw=NativeArcadeHud.ChargeBorder;NativeHudChrome.Cut(r,NativeCyberTheme.Navy(NativeHudCyber.PanelAlpha),NativeHudCyber.TagCut);
                var inner=new Rect(r.x+bw,r.y+bw,r.width-2*bw,r.height-2*bw);U.Fill(new Rect(inner.x,inner.y,inner.width*NativeArcadeHud.ChargePercent(f.Charge)/100f,inner.height),NativeCyberTheme.CyanDeep(.75f));NativeHudChrome.Outline(r,NativeCyberTheme.CyanSoft(),NativeHudCyber.TagCut,NativeHudChrome.Corners.Diagonal,bw);
                U.Text(inner,chargeText,NativeArcadeHud.ChargeSize,ink,TextAnchor.MiddleCenter,bold:true,mono:true);
            }
            if(f!=null&&NativeArcadeHud.ComboShown(f.Hits,t,f.LastHit,emperor))Combo(f,emperor,t,W,H);
        }
        private long comboDamage=-1;private string comboDamageText;
        // .arcade-combo: the count and HIT(S) on one line, the damage, the draining bar; scale(.8) about the left centre with
        // a 3 px hard shadow under every line of text.
        private void Combo(NativeFighter f,bool emperor,float t,float W,float H)
        {
            long damage=NativeArcadeHud.Round(f.ComboDamage);if(damage!=comboDamage||comboDamageText==null){comboDamage=damage;comboDamageText=NativeHudText.Int(damage)+NativeArcadeHud.DamageText;}
            // " HIT(S)" drawn as its word after one 22 px space (the word constants, so nothing is built per frame).
            string count=NativeHudText.Int(f.Hits),suffix=NativeArcadeHud.HitsSuffix(f.Hits,emperor)==NativeArcadeHud.Hit?NativeArcadeHud.HitWord:NativeArcadeHud.HitsWord;float size=NativeArcadeHud.CountSize(W),cw=U.Measure(count,size,true,true).x,lead=SpaceWidth(NativeArcadeHud.SmallSize,true);
            float sw=lead+U.Measure(suffix,NativeArcadeHud.SmallSize,true,true).x,dw=U.Measure(comboDamageText,NativeArcadeHud.DamageSize,false,true).x,dh=NativeArcadeHud.DamageLine;
            float w=Mathf.Max(cw+sw,dw),h=size+NativeArcadeHud.DamageMargin*2+dh+NativeArcadeHud.BarHeight,x=W*(1-NativeArcadeHud.ComboRight)-w,y=U.H*NativeArcadeHud.ComboTop;
            Color ink=U.Hex(emperor?NativeArcadeHud.EmperorInk:NativeArcadeHud.ComboInk),shadow=U.Hex(emperor?NativeArcadeHud.EmperorShadow:NativeArcadeHud.ComboShadow),damageInk=U.Hex(NativeArcadeHud.DamageInk);float o=NativeArcadeHud.ShadowOffset;
            U.Push(new Vector2(x,y+h/2),Vector2.zero,NativeArcadeHud.ComboScale);
            // The 22 px HIT(S) shares the count's baseline: its line starts .557 of the count's size down (70 px: 39 px).
            var countRect=new Rect(x,y,cw+4,size);var smallRect=new Rect(x+cw+lead,y+size*NativeArcadeHud.SmallDrop,sw-lead+4,NativeArcadeHud.SmallSize*1.16f);var damageRect=new Rect(x,y+size+NativeArcadeHud.DamageMargin,dw+4,dh);
            ComboText(countRect,count,size,ink,shadow,o,true);ComboText(smallRect,suffix,NativeArcadeHud.SmallSize,ink,shadow,o,true);ComboText(damageRect,comboDamageText,NativeArcadeHud.DamageSize,damageInk,shadow,o,false);
            var bar=new Rect(x,y+h-NativeArcadeHud.BarHeight,w*NativeArcadeHud.ComboFill(t,f.LastHit,emperor),NativeArcadeHud.BarHeight);
            if(bar.width>0){U.Glow(bar,U.Hex(NativeArcadeHud.ComboBarGlow,.55f),NativeArcadeHud.BarGlow);U.Fill(bar,U.Hex(emperor?NativeArcadeHud.EmperorBar:NativeArcadeHud.ComboBar));}
            U.Pop();
        }
        private static void ComboText(Rect r,string text,float size,Color ink,Color shadow,float o,bool bold)
        {U.Text(new Rect(r.x+o,r.y+o,r.width,r.height),text,size,shadow,TextAnchor.MiddleLeft,bold:bold,mono:true);U.Text(r,text,size,ink,TextAnchor.MiddleLeft,bold:bold,mono:true);}

        // ---- Siren HUD (client/siren-hud.ts, rules in NativeSirenHud) ----------------------------------------------------
        // The mode and the four moves' cooldowns. The host (and offline) reads its NativeSirenState; a guest is not sent the
        // Ready times, so it counts each of its moves' cooldown from that move's Siren event (NativeSirenCooldowns).
        private readonly NativeSirenCooldowns sirenCooldowns=new NativeSirenCooldowns();private readonly float[] sirenWidths=new float[5];private readonly string[] sirenStates=new string[5];
        private string sirenModeKey,sirenModeLabel;private bool sirenModeRanged;
        private static readonly string[] SirenMeleeLabels=SirenLabels(false),SirenRangedLabels=SirenLabels(true);
        private static string[] SirenLabels(bool ranged){var moves=NativeSirenHud.Moves(ranged);var labels=new string[moves.Length];for(int i=0;i<moves.Length;i++)labels[i]=(i+1)+" "+NativeSirenHud.Name(moves[i]);return labels;}
        private void SirenHud(NativePilot p,float W)
        {
            float t=arena.Time;bool host=!arena.Replica&&p.Siren!=null,ranged=host?p.Siren.Ranged:p.Pose.SourceRanged;string key=NativeSirenHud.Key(prefs.sirenMode);
            if(arena.Replica)sirenCooldowns.Update(arena.Events,arena.Generation,p.Seat.id);
            if(sirenModeLabel==null||key!=sirenModeKey||ranged!=sirenModeRanged){sirenModeKey=key;sirenModeRanged=ranged;sirenModeLabel=key+" "+(ranged?NativeSirenHud.RangedMode:NativeSirenHud.MeleeMode);}
            var moves=NativeSirenHud.Moves(ranged);var labels=ranged?SirenRangedLabels:SirenMeleeLabels;float size=NativeSirenHud.Size,space=SpaceWidth(size),edge=2*NativeSirenHud.PadX+2*NativeSirenHud.Border;
            sirenStates[0]=null;sirenWidths[0]=U.Measure(sirenModeLabel,size,false,true).x+edge;
            for(int i=0;i<moves.Length;i++)
            {
                float ready=host?(p.Siren.Ready.TryGetValue(moves[i],out float r)?r:0):arena.Replica?sirenCooldowns.Ready(moves[i]):0;
                sirenStates[i+1]=ready>t?NativeHudText.Seconds(ready-t):NativeArcadeHud.Ready;
                sirenWidths[i+1]=U.Measure(labels[i],size,false,true).x+space+NativeSirenHud.StateGap+U.Measure(sirenStates[i+1],size,true,true).x+edge;
            }
            // flex-wrap rows inside min(860px, 80vw), each row centred; the block's bottom 93 px up.
            float box=Mathf.Min(NativeSirenHud.Width,W*NativeSirenHud.WidthVw),right=W-NativeArcadeHud.Right,h=2*NativeSirenHud.PadY+NativeSirenHud.Line+2*NativeSirenHud.Border,gap=NativeArcadeHud.ItemGap;
            int rows=1;float used=0;for(int i=0;i<5;i++){if(used>0&&used+gap+sirenWidths[i]>box){rows++;used=0;}used+=(used>0?gap:0)+sirenWidths[i];}
            float y=U.H-NativeArcadeHud.Bottom-rows*h-(rows-1)*gap;int start=0;
            while(start<5)
            {
                int end=start;float line=sirenWidths[start];while(end+1<5&&line+gap+sirenWidths[end+1]<=box){end++;line+=gap+sirenWidths[end];}
                float x=right-box+(box-line)/2;
                for(int i=start;i<=end;i++){SirenSpan(new Rect(x,y,sirenWidths[i],h),i==0?sirenModeLabel:labels[i-1],sirenStates[i],space);x+=sirenWidths[i]+gap;}
                y+=h+gap;start=end+1;
            }
        }
        private static void SirenSpan(Rect r,string label,string state,float space)
        {
            float size=NativeSirenHud.Size;NativeHudChrome.Cut(r,NativeCyberTheme.Navy(NativeHudCyber.PanelAlpha),NativeHudCyber.TagCut);NativeHudChrome.Outline(r,NativeCyberTheme.Violet(.8f),NativeHudCyber.TagCut);
            float lx=r.x+NativeSirenHud.Border+NativeSirenHud.PadX,lw=U.Measure(label,size,false,true).x;var inner=new Rect(lx,r.y+NativeSirenHud.Border,r.width,r.height-2*NativeSirenHud.Border);
            U.Text(new Rect(lx,inner.y,lw+2,inner.height),label,size,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,mono:true);
            if(state!=null)U.Text(new Rect(lx+lw+space+NativeSirenHud.StateGap,inner.y,r.xMax-lx,inner.height),state,size,state==NativeArcadeHud.Ready?NativeCyberTheme.Magenta():NativeCyberTheme.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true);
        }
        // Centred fight prompts with the HUD's shadowed type.
        private static void Prompt(float y,string text,float size,Color c)=>U.Text(new Rect(0,y,U.W,size*1.4f),text,size,c,TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,.85f));
    }
}
