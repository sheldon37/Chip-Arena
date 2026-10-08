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
        }
        // .healthbox: weapon and HP (rounded up), the HP bar, the weapon resource and, off melee, stamina.
        private void HealthBox(NativePilot p,float x,float bottom)
        {
            bool melee=p.Weapon.@class=="melee",magic=p.Weapon.@class=="magic",ranged=p.Weapon.@class=="ranged";float w=224,pad=14,h=12+17+7+5+9+14+6+3+(melee?0:9+14+6+3)+12;var r=new Rect(x,bottom-h,w,h);
            if(p.Pose.Sprinting&&match.Phase==NativePhase.Fight){var tag=new Rect(r.x,r.y-24,88,18);U.Round(tag,U.Hex(0x0c1b22,.87f),3,U.Hex(0xf8db94));U.Text(tag,"SPRINTING",10,U.Hex(0xf8db94),TextAnchor.MiddleCenter,mono:true);}
            U.Round(r,Ink(.69f),10,new Color(1,1,1,.094f));float y=r.y+12,ix=r.x+pad,iw=w-2*pad;
            U.Text(new Rect(ix,y,iw,17),p.Weapon.name,12,U.Hex(0xe8e9ef),TextAnchor.MiddleLeft);U.Text(new Rect(ix,y,iw,17),Mathf.CeilToInt(Math.Max(0,p.Hp)).ToString(),17,U.Hex(0xf3f3f5),TextAnchor.MiddleRight,mono:true);y+=17+7;
            float hp=Mathf.Clamp01(p.Hp/Mathf.Max(1,(float)p.Stats["hp"]));U.Round(new Rect(ix,y,iw,5),new Color(1,1,1,.09f),4);if(hp>0)U.Round(new Rect(ix,y,iw*hp,5),U.Hex(0x59dd83),4);y+=5+9;
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
        // style.css .crosshair (a 5 px dot with 12 px arms 10 px either side) and hitmarker.ts (a 42 px X that pops and fades,
        // red for headshots), at the pixel-rounded centre where ViewportPointToRay(.5,.5) aims. Crisp: identity matrix.
        private void Crosshair()
        {
            if(Event.current.type!=EventType.Repaint)return;var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;float k=Mathf.Max(1,Mathf.Round(Screen.height/1080f)),cx=Mathf.Round(Screen.width/2f),cy=Mathf.Round(Screen.height/2f);
            void Px(float x,float y,float w,float h,Color c){var o=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=o;}
            var shadow=U.Hex(0x081b25,.8f);var ink=U.Hex(0xe8fff3);float d=Mathf.Round(5*k),arm=12*k,gap=10*k,line=Mathf.Max(1,Mathf.Round(k));
            Px(cx-d/2-1,cy-d/2-1,d+2,d+2,shadow);Px(cx-d/2,cy-d/2,d,d,ink);Px(cx-d/2-gap-arm,cy-line/2,arm,line,ink);Px(cx+d/2+gap,cy-line/2,arm,line,ink);
            float age=Time.unscaledTime-(presentation.LastHitUntil-.25f);
            if(age>=0&&age<.34f)
            {
                float a=Mathf.Min(1,(.34f-age)/.16f),s=(1+Mathf.Max(0,.08f-age)*2.1f)*42*k;Color c=presentation.LastHeadshot?U.Hex(0xff394d,a):new Color(1,1,1,a);float w=Mathf.Max(1.5f,1.26f*k*(s/(42*k)));
                foreach(var dir in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(1,1)})
                {   // m34 34 -17 -17: each stroke runs from 16% to 33% of the box's half-diagonal outward.
                    Vector2 a0=new Vector2(cx,cy)+dir*s*.16f,a1=new Vector2(cx,cy)+dir*s*.33f;float len=(a1-a0).magnitude;var mid=(a0+a1)/2;
                    GUIUtility.RotateAroundPivot(dir.x*dir.y>0?45:-45,mid);Px(mid.x-len/2,mid.y-w/2-1,len,w+2,U.Hex(0,.45f*a));Px(mid.x-len/2,mid.y-w/2,len,w,c);GUI.matrix=Matrix4x4.identity;
                }
            }
            GUI.matrix=old;
        }
        // Centred fight prompts with the HUD's shadowed type.
        private static void Prompt(float y,string text,float size,Color c)=>U.Text(new Rect(0,y,U.W,size*1.4f),text,size,c,TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,.85f));
    }
}
