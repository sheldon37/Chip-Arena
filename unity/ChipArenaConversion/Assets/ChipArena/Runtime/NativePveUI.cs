using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool PvePickOpen=>arena?.Pve!=null&&arena.Pve.Rows.TryGetValue(arena.Local.Seat.id,out var row)&&row.Open!=null;
        private int pvePending;private float pvePendingAt=-10;
        private void PveHud(float width,float height)
        {
            var v=arena.Pve;if(v==null)return;var p=arena.Local;PveBanner();
            float W=NativeUi.W,H=NativeUi.H;
            // pve/hud.ts waveHeading and bossBar: a top-centre pill, then the Siren's bar with her phase thresholds.
            string status=v.Stage=="boss"?"THE SIREN  ·  PHASE "+v.Phase+" / 4":v.Stage=="wave"?"WAVE "+v.Wave+" / "+(v.Content?.waves?.Length??3):v.Stage=="intermission"?"INTERMISSION  ·  WAVE "+v.Wave+" CLEARED":v.Stage=="opening"?"WARD ZERO  ·  GET READY":v.Stage.ToUpperInvariant();
            if(v.Stage!="ended"){float sw=NativeUi.Measure(status,12,false,true,.08f).x+28;var sr=new Rect(W/2-sw/2,20,sw,26);NativeUi.Pill(sr,NativeUi.Hex(0x10121b,.6f),v.Stage=="boss"?NativeUi.Hex(0xb7a8ff,.4f):Color.clear);NativeUi.Text(sr,status,12,v.Stage=="boss"?NativeUi.Hex(0xd9d0ff):NativeUi.Hex(0xd6d7e1),TextAnchor.MiddleCenter,mono:true,tracking:.08f);}
            if(v.Stage=="boss"&&!v.Hidden&&v.Boss!=null)
            {
                float hp=v.MaxHp>0?Mathf.Clamp01(v.Boss.Hp/v.MaxHp):0,bw=Mathf.Min(560,W*.4f),bx=W/2-bw/2,by=58;NativeUi.Text(new Rect(bx,by,bw,16),"THE SIREN",12,NativeUi.Hex(0xf3efff),TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:new Color(0,0,0,.8f));
                NativeUi.Text(new Rect(bx,by,bw,16),Mathf.CeilToInt(hp*100)+"%",12,NativeUi.Hex(0xc9bfff),TextAnchor.MiddleRight,mono:true);var bar=new Rect(bx,by+20,bw,10);NativeUi.Round(bar,NativeUi.Hex(0x0b0910,.85f),5,NativeUi.Hex(0xb7a8ff,.35f));
                if(hp>0)NativeUi.HGradient(new Rect(bar.x+1,bar.y+1,(bar.width-2)*hp,bar.height-2),NativeUi.Hex(0x7d5cff),NativeUi.Hex(0xff2d55));foreach(float k in new[]{.75f,.5f,.25f})NativeUi.Fill(new Rect(bar.x+bar.width*k,bar.y-2,1,bar.height+4),new Color(1,1,1,.45f));
            }
            if(v.Rows.TryGetValue(p.Seat.id,out var row))
            {
                // pve/hud.ts moneyHud (top left) and the pending chips pill above the cooldown cards, pulsing after a new pick.
                var mr=new Rect(24,24,220,58);NativeUi.Round(mr,NativeUi.Hex(0x101219,.7f),10,new Color(1,1,1,.094f));NativeUi.Text(new Rect(mr.x+14,mr.y+8,mr.width-28,26),"$"+row.Money,22,NativeUi.Hex(0xffd27a),TextAnchor.MiddleLeft,bold:true,mono:true,glow:NativeUi.Hex(0xffb84a,.35f));
                NativeUi.Text(new Rect(mr.x+14,mr.y+34,mr.width-28,16),row.Kills+" kills  ·  F interact / revive",11,NativeUi.Hex(0xa6a7ad),TextAnchor.MiddleLeft);
                if(row.Picks.Count!=pvePending){if(row.Picks.Count>pvePending)pvePendingAt=Time.unscaledTime;pvePending=row.Picks.Count;}
                if(row.Picks.Count>0&&!PvePickOpen){float since=Time.unscaledTime-pvePendingAt,pulse=since<6?1+.07f*(.5f-.5f*Mathf.Cos(since*2*Mathf.PI)):1;string chips="CHIPS "+row.Picks.Count;float cw=NativeUi.Measure(chips,12,true,true,.1f).x+44;var cr=new Rect(W-24-cw,H-104-30,cw,30);
                    NativeUi.Push(cr.center,Vector2.zero,pulse);if(since<6)NativeUi.Glow(cr,NativeUi.Hex(0xff6b35,.4f*(pulse-1)/.07f),24);NativeUi.Pill(cr,NativeUi.Hex(0x101219,.85f),NativeUi.Hex(0xff6b35,.7f));NativeUi.Text(new Rect(cr.x+14,cr.y,cw,cr.height),chips,12,NativeUi.Hex(0xffb38f),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
                    var key=new Rect(cr.xMax-26,cr.y+6,18,18);NativeUi.Round(key,new Color(1,1,1,.12f),4);NativeUi.Text(key,"C",10,NativeUi.Hex(0xf4f4f5),TextAnchor.MiddleCenter,bold:true,mono:true);NativeUi.Pop();}
                if(p.Hp<=0)Prompt(H/2+5,arena.InShaft(p)?"FALLING THROUGH THE VENT":row.SelfAt>=0?"Self-revive in "+Math.Ceiling(row.SelfAt-arena.Time)+"s":"DOWNED  ·  A teammate can revive you",16,NativeUi.Hex(0xffb3ba));
            }
        }
    }
}
