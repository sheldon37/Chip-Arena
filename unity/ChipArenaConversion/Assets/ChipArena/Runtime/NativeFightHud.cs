using System;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The fight HUD as main.ts / match-hud.css lay it out: vitals bottom left and cooldown cards bottom right (24 px from
    // the true window edges), the SET / ROUND pill top centre, prompts centred, and a crosshair and hit marker drawn in
    // physical pixels on the exact screen centre the aim ray uses. Values are read from the simulation as before.
    public sealed partial class ConversionGame
    {
        private static Color Ink(float a=1)=>U.Hex(0x101219,a);
        private void FightHud(NativePilot p)
        {
            float W=U.W,H=U.H;
            if(arena.Pve==null){string set="SET "+match.Set+"  ·  ROUND "+match.Round;float sw=U.Measure(set,12,false,true).x+26;var sr=new Rect(W/2-sw/2,20,sw,26);U.Pill(sr,U.Hex(0x10121b,.54f),Color.clear);U.Text(sr,set,12,U.Hex(0xd6d7e1),TextAnchor.MiddleCenter,mono:true);}
            HealthBox(p,24,H-24);Cooldowns(p,W-24,H-24);
            if(Event.current.type!=EventType.Repaint)return;
            if(match.Phase==NativePhase.Fight&&(p.Seat.spec=="ansatsuken"||p.Seat.spec=="emperor"))FighterHud(p,W,H);
            if(p.Seat.spec=="siren")SirenHud(p,W);
            Hitmarker();
        }
        // .healthbox: weapon and HP (rounded up), the HP bar, the weapon resource and, off melee, stamina.
        private void HealthBox(NativePilot p,float x,float bottom)
        {
            bool melee=p.Weapon.@class=="melee",magic=p.Weapon.@class=="magic",ranged=p.Weapon.@class=="ranged";float w=224,pad=14,h=12+17+7+5+9+14+6+3+(melee?0:9+14+6+3)+12;var r=new Rect(x,bottom-h,w,h);
            SprintTag(p,r);
            U.Round(r,Ink(.69f),10,new Color(1,1,1,.094f));float y=r.y+12,ix=r.x+pad,iw=w-2*pad;
            U.Text(new Rect(ix,y,iw,17),p.Weapon.name,12,U.Hex(0xe8e9ef),TextAnchor.MiddleLeft);U.Text(new Rect(ix,y,iw,17),Mathf.CeilToInt(Math.Max(0,p.Hp)).ToString(),17,U.Hex(0xf3f3f5),TextAnchor.MiddleRight,mono:true);y+=17+7;
            float hp=Mathf.Clamp01(p.Hp/Mathf.Max(1,(float)p.Stats["hp"]));U.Round(new Rect(ix,y,iw,5),new Color(1,1,1,.09f),4);DamageLagChip(new Rect(ix,y,iw,5),p,hp);if(hp>0)U.Round(new Rect(ix,y,iw*hp,5),U.Hex(0x59dd83),4);y+=5+9;
            string label,value;float fill;
            if(ranged){int cap=(int)Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]);label=p.ReloadEnd>arena.Time?"Reloading "+(p.ReloadEnd-arena.Time).ToString("0.0")+"s":"Magazine";value=p.Ammo+" / "+cap;fill=cap>0?p.Ammo/(float)cap:0;}
            else if(magic){float max=(float)p.Stats["maxMana"];label="Mana";value=Mathf.FloorToInt(p.Mana)+" / "+Mathf.RoundToInt(max);fill=max>0?p.Mana/max:0;}
            else{float max=(float)p.Stats["maxStamina"];label="Stamina";value=Mathf.FloorToInt(p.Stamina)+" / "+Mathf.RoundToInt(max);fill=max>0?p.Stamina/max:0;}
            U.Text(new Rect(ix,y,iw,14),label,11,U.Hex(0xb4b6c5),TextAnchor.MiddleLeft);U.Text(new Rect(ix,y,iw,14),value,13,melee?U.Hex(0x9fd3ff):U.Hex(0xe8e9ef),TextAnchor.MiddleRight,mono:true);y+=14+6;
            U.Round(new Rect(ix,y,iw,3),new Color(1,1,1,.09f),2);if(fill>0)U.Round(new Rect(ix,y,iw*Mathf.Clamp01(fill),3),melee?U.Hex(0x7cc4ff):magic?U.Hex(0xbaaff5):U.Hex(0xbb9d79),2);y+=3;
            if(!melee){float max=(float)p.Stats["maxStamina"];y+=9;U.Text(new Rect(ix,y,iw,14),"Stamina",11,U.Hex(0xb4b6c5),TextAnchor.MiddleLeft);U.Text(new Rect(ix,y,iw,14),Mathf.FloorToInt(p.Stamina)+" / "+Mathf.RoundToInt(max),13,U.Hex(0x9fd3ff),TextAnchor.MiddleRight,mono:true);y+=14+6;
                U.Round(new Rect(ix,y,iw,3),new Color(1,1,1,.09f),2);if(max>0)U.Round(new Rect(ix,y,iw*Mathf.Clamp01(p.Stamina/max),3),p.Pose.Sprinting?U.Hex(0xf8db94):U.Hex(0x7cc4ff),2);}
        }
        private struct Card {public string Key,State,Sub;public bool Ready,Active;public float Dial;}
        // .cooldowns: RMB block, SHIFT roll and the Q / E cards; ready reads as a word in the good colour.
        private void Cooldowns(NativePilot p,float right,float bottom)
        {
            float t=arena.Time;var cards=new System.Collections.Generic.List<Card>();
            cards.Add(new Card{Key="RMB · "+(p.Seat.spec=="ansatsuken"?"PARRY":"BLOCK"),State=t<p.BlockEnd?"ACTIVE":p.BlockReady>t?(p.BlockReady-t).ToString("0.0")+"s":"READY",Ready=t>=p.BlockEnd&&p.BlockReady<=t,Active=t<p.BlockEnd});
            bool roll=p.Weapon.@class=="melee";string rollState=t<p.RollEnd?"ROLLING":p.RollReady>t?(p.RollReady-t).ToString("0.0")+"s":"READY";cards.Add(new Card{Key="SHIFT · "+(roll?"ROLL":"DODGE"),State=rollState,Ready=rollState=="READY"});
            foreach(var slot in new[]{"q","e"})
            {
                var d=content.Ability(p.Seat.spec,slot);string status=AbilityStatus(p,slot);if(string.IsNullOrEmpty(status)||d==null&&slot=="q")continue;
                int dot=status.IndexOf(" · ",StringComparison.Ordinal);string name=dot>0?status.Substring(0,dot).Replace("  "," · ").ToUpperInvariant():slot.ToUpperInvariant(),state=dot>0?status.Substring(dot+3):"";
                float left=d==null?Math.Max(0,p.EReady-t):slot=="q"?p.QReady-t:p.ECharges>0?0:p.EReady-t,cd=d!=null&&d.cooldown>0?(float)d.cooldown:1;
                cards.Add(new Card{Key=name,State=state.ToUpperInvariant(),Ready=state=="ready",Active=state=="casting"||state=="active",Dial=d!=null?1-Mathf.Clamp01(left/cd):-1});
            }
            float x=right;for(int i=cards.Count-1;i>=0;i--)
            {
                var c=cards[i];float kw=U.Measure(c.Key,10,false,true).x,vw=U.Measure(c.State,c.Ready?12:13,c.Ready,true,c.Ready?.08f:0).x,w=Mathf.Max(78,Mathf.Max(kw,vw)+20+(c.Dial>=0?12:0)),h=8+14+5+16+8;var r=new Rect(x-w,bottom-h,w,h);
                U.Round(r,Ink(.65f),9,c.Active?U.Hex(0x59dd83,.6f):new Color(1,1,1,.094f));U.Text(new Rect(r.x+10,r.y+8,w-20,14),c.Key,10,U.Hex(0xb0b3c2),TextAnchor.MiddleLeft,mono:true);
                U.Text(new Rect(r.x+10,r.y+8+14+5,w-20,16),c.State,c.Ready?12:13,c.Ready?U.Hex(0x59dd83):c.State=="LOW MANA"||c.State=="LOW STAMINA"?U.Hex(0xff8a7a):U.Hex(0xf3f3f5),TextAnchor.MiddleLeft,bold:c.Ready,mono:true,tracking:c.Ready?.08f:0);
                if(c.Dial>=0){var dial=new Rect(r.xMax-17,r.y+6,11,11);U.Round(dial,new Color(1,1,1,.12f),6);U.Pie(dial,c.State=="LOW MANA"?U.Hex(0xff7a6b):U.Hex(0x59dd83),Mathf.Max(.01f,c.Dial));}
                x=r.x-5;
            }
        }
        // style.css .crosshair (a 5 px dot with 12 px arms 10 px either side) at the pixel-rounded centre where
        // ViewportPointToRay(.5,.5) aims. Crisp: identity matrix. Hitmarker draws the hit marker.
        private void Crosshair()
        {
            if(Event.current.type!=EventType.Repaint)return;var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;float k=Mathf.Max(1,Mathf.Round(Screen.height/1080f)),cx=Mathf.Round(Screen.width/2f),cy=Mathf.Round(Screen.height/2f);
            void Px(float x,float y,float w,float h,Color c){var o=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=o;}
            var shadow=U.Hex(0x081b25,.8f);var ink=U.Hex(0xe8fff3);float d=Mathf.Round(5*k),arm=12*k,gap=10*k,line=Mathf.Max(1,Mathf.Round(k));
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
            Color ink=U.Hex(tired?NativeSprintTag.ExhaustedColor:NativeSprintTag.SprintColor),glow=U.Hex(NativeSprintTag.GlowColor,NativeSprintTag.GlowAlpha);
            // letter-spacing follows every glyph (the last included), as in the browser.
            float tw=U.Measure(text,size,true,true,track).x+track*size,chevron=tired?0:U.Measure(NativeSprintTag.Chevron,NativeSprintTag.ChevronSize,true,true).x+track*size;
            float line=tired?size*NativeSprintTag.LineHeight:Mathf.Max(size*NativeSprintTag.LineHeight,NativeSprintTag.ChevronLine);
            float b=NativeSprintTag.Border,w=tw+(tired?0:chevron+NativeSprintTag.Gap)+2*NativeSprintTag.PadX+2*b,h=line+2*NativeSprintTag.PadY+2*b;
            var r=new Rect(box.x+1,box.y+1-NativeSprintTag.Above-h,w,h);TagBox(r,U.Hex(NativeSprintTag.Fill,NativeSprintTag.FillAlpha),ink,b);
            float x=r.x+b+NativeSprintTag.PadX,y=r.y+b+NativeSprintTag.PadY;
            if(!tired)
            {
                bool still=prefs.reducedEffects;float phase=still?.5f:NativeSprintTag.ChevronPhase(Time.unscaledTime),shift=still?0:Mathf.Lerp(-NativeSprintTag.ChevronShift,NativeSprintTag.ChevronShift,phase),alpha=still?1:Mathf.Lerp(NativeSprintTag.ChevronDim,1,phase);
                U.Text(new Rect(x+shift,y,chevron,line),NativeSprintTag.Chevron,NativeSprintTag.ChevronSize,U.A(ink,alpha),TextAnchor.MiddleLeft,bold:true,mono:true,glow:U.A(glow,alpha));x+=chevron+NativeSprintTag.Gap;
            }
            U.Text(new Rect(x,y,tw,line),text,size,ink,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:track,glow:glow);
        }
        // A square-cornered box: background under a `b`-unit border (CSS border-box painting).
        private static void TagBox(Rect r,Color fill,Color edge,float b)
        {
            U.Fill(r,fill);if(edge.a<=0||b<=0)return;
            U.Fill(new Rect(r.x,r.y,r.width,b),edge);U.Fill(new Rect(r.x,r.yMax-b,r.width,b),edge);U.Fill(new Rect(r.x,r.y+b,b,r.height-2*b),edge);U.Fill(new Rect(r.xMax-b,r.y+b,b,r.height-2*b),edge);
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
            Color edge=U.Hex(emperor?NativeArcadeHud.EmperorEdge:NativeArcadeHud.SpecialEdge),ink=U.Hex(NativeArcadeHud.Ink),stateInk=U.Hex(NativeArcadeHud.StateInk);
            for(int i=0;i<n;i++)
            {
                var r=new Rect(x,top,arcadeWidths[i],cardH);U.Round(r,U.Hex(NativeArcadeHud.SpecialFill,NativeArcadeHud.SpecialFillAlpha),NativeArcadeHud.SpecialRadius,edge);
                float lx=r.x+NativeArcadeHud.Border+NativeArcadeHud.SpecialPadX,lw=U.Measure(labels[i],NativeArcadeHud.SpecialSize,false,true).x;var inner=new Rect(lx,r.y+NativeArcadeHud.Border,r.width,r.height-2*NativeArcadeHud.Border);
                U.Text(new Rect(lx,inner.y,lw+2,inner.height),labels[i],NativeArcadeHud.SpecialSize,ink,TextAnchor.MiddleLeft,mono:true);
                U.Text(new Rect(lx+lw+space+NativeArcadeHud.StateGap,inner.y,r.xMax-lx,inner.height),arcadeStates[i],NativeArcadeHud.StateSize,stateInk,TextAnchor.MiddleLeft,bold:true,mono:true);x=r.xMax+NativeArcadeHud.ItemGap;
            }
            if(charging)
            {
                var r=new Rect(right-width,bottom-chargeH,width,chargeH);float bw=NativeArcadeHud.ChargeBorder;TagBox(r,U.Hex(NativeArcadeHud.ChargeFill,NativeArcadeHud.ChargeFillAlpha),U.Hex(NativeArcadeHud.ChargeEdge),bw);
                var inner=new Rect(r.x+bw,r.y+bw,r.width-2*bw,r.height-2*bw);U.Fill(new Rect(inner.x,inner.y,inner.width*NativeArcadeHud.ChargePercent(f.Charge)/100f,inner.height),U.Hex(NativeArcadeHud.ChargeBar,NativeArcadeHud.ChargeBarAlpha));
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
            float size=NativeSirenHud.Size;U.Round(r,U.Hex(NativeSirenHud.Fill,NativeSirenHud.FillAlpha),NativeSirenHud.Radius,U.Hex(NativeSirenHud.Edge));
            float lx=r.x+NativeSirenHud.Border+NativeSirenHud.PadX,lw=U.Measure(label,size,false,true).x;var inner=new Rect(lx,r.y+NativeSirenHud.Border,r.width,r.height-2*NativeSirenHud.Border);
            U.Text(new Rect(lx,inner.y,lw+2,inner.height),label,size,U.Hex(NativeSirenHud.Ink),TextAnchor.MiddleLeft,mono:true);
            if(state!=null)U.Text(new Rect(lx+lw+space+NativeSirenHud.StateGap,inner.y,r.xMax-lx,inner.height),state,size,U.Hex(NativeSirenHud.StateInk),TextAnchor.MiddleLeft,bold:true,mono:true);
        }
        // Centred fight prompts with the HUD's shadowed type.
        private static void Prompt(float y,string text,float size,Color c)=>U.Text(new Rect(0,y,U.W,size*1.4f),text,size,c,TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,.85f));
    }
}
