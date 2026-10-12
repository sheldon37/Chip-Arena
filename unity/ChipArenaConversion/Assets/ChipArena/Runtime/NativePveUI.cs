using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool PvePickOpen=>arena?.Pve!=null&&arena.Pve.Rows.TryGetValue(arena.Local.Seat.id,out var row)&&row.Open!=null;
        private int pvePending;private float pvePendingAt=-10;private readonly NativeHudTrail bossTrail=new NativeHudTrail();
        private void PveHud(float width,float height)
        {
            var v=arena.Pve;if(v==null)return;var p=arena.Local;PveBanner();
            float W=NativeUi.W,H=NativeUi.H;
            // pve/hud.ts waveHeading and bossBar: a top-centre pill, then the Siren's bar with her phase thresholds.
            string status=v.Stage=="boss"?"THE SIREN  ·  PHASE "+v.Phase+" / 4":v.Stage=="wave"?"WAVE "+v.Wave+" / "+(v.Content?.waves?.Length??3):v.Stage=="intermission"?"INTERMISSION  ·  WAVE "+v.Wave+" CLEARED":v.Stage=="opening"?"WARD ZERO  ·  GET READY":v.Stage.ToUpperInvariant();
            if(v.Stage!="ended")SetTag(status,v.Stage=="boss"?NativeCyberTheme.Violet():NativeCyberTheme.Cyan(),W);
            if(v.Stage=="boss"&&!v.Hidden&&v.Boss!=null)
            {
                // The Siren's bar: a holographic strip, her four phases (thresholds 75 / 50 / 25%) as segments, violet into
                // magenta across the bar, with the damage trail (NativeHudTrail) and a phase tick over each threshold.
                float hp=v.MaxHp>0?Mathf.Clamp01(v.Boss.Hp/v.MaxHp):0,trail=bossTrail.Step(arena.Time,hp),bw=Mathf.Min(560,W*.4f),bx=W/2-bw/2,by=58;
                var plate=new Rect(bx-12,by-6,bw+24,44);NativeHudChrome.Panel(plate,1,NativeCyberTheme.Violet(),NativeHudCyber.TagCut);
                NativeUi.Text(new Rect(bx,by,bw,16),"THE SIREN",12,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:new Color(0,0,0,.8f));
                NativeUi.Text(new Rect(bx,by,bw,16),Mathf.CeilToInt(hp*100)+"%",12,NativeCyberTheme.CyanSoft(),TextAnchor.MiddleRight,mono:true);var bar=new Rect(bx,by+20,bw,10);
                for(int i=0;i<4;i++)
                {
                    NativeHudCyber.SegmentSpan(i,.25f,1,out float a,out float b);var cell=new Rect(bar.x+bar.width*a+(i>0?1.5f:0),bar.y,bar.width*(b-a)-(i>0?1.5f:0)-(i<3?1.5f:0),bar.height);NativeUi.Fill(cell,NativeCyberTheme.Void(.85f));
                    float g=NativeHudCyber.SegmentFill(i,.25f,1,trail),f=NativeHudCyber.SegmentFill(i,.25f,1,hp);if(g>f)NativeUi.Fill(new Rect(cell.x,cell.y,cell.width*g,cell.height),NativeCyberTheme.Ink(.75f));
                    if(f>0){var fill=new Rect(cell.x,cell.y,cell.width*f,cell.height);NativeUi.HGradient(fill,NativeUi.Mix(NativeCyberTheme.Violet(),NativeCyberTheme.Magenta(),a),NativeUi.Mix(NativeCyberTheme.Violet(),NativeCyberTheme.Magenta(),a+(b-a)*f));NativeUi.Fill(new Rect(fill.x,fill.y,fill.width,NativeHudChrome.Snap(1)),NativeCyberTheme.Ink(.35f));}
                }
                NativeHudChrome.Outline(new Rect(bar.x-2,bar.y-2,bar.width+4,bar.height+4),NativeCyberTheme.Violet(.55f),3,NativeHudChrome.Corners.Diagonal);
                foreach(float k in new[]{.75f,.5f,.25f})NativeUi.Fill(new Rect(bar.x+bar.width*k-.5f,bar.y-3,1,bar.height+6),NativeCyberTheme.Ink(.55f));
            }
            if(v.Rows.TryGetValue(p.Seat.id,out var row))
            {
                // pve/hud.ts moneyHud (top left) and the pending chips pill above the cooldown cards, pulsing after a new pick.
                var mr=new Rect(24,24,220,58);NativeHudChrome.Panel(mr,1,NativeCyberTheme.Warn());NativeUi.Text(new Rect(mr.x+14,mr.y+8,mr.width-28,26),"$"+row.Money,22,NativeCyberTheme.Warn(),TextAnchor.MiddleLeft,bold:true,mono:true,glow:NativeCyberTheme.Warn(.3f));
                NativeUi.Text(new Rect(mr.x+14,mr.y+34,mr.width-28,16),row.Kills+" kills  ·  F interact / revive",11,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft);
                if(row.Picks.Count!=pvePending){if(row.Picks.Count>pvePending)pvePendingAt=Time.unscaledTime;pvePending=row.Picks.Count;}
                // The pending chips tag (magenta: a reward waiting), pulsing for 6 s after a new pick; still with Reduced effects.
                if(row.Picks.Count>0&&!PvePickOpen){float since=Time.unscaledTime-pvePendingAt,pulse=since<6&&!prefs.reducedEffects?1+.07f*(.5f-.5f*Mathf.Cos(since*2*Mathf.PI)):1;string chips="CHIPS "+row.Picks.Count;float cw=NativeUi.Measure(chips,12,true,true,.1f).x+44;var cr=new Rect(W-24-cw,H-104-30,cw,30);
                    NativeUi.Push(cr.center,Vector2.zero,pulse);if(since<6&&pulse>1)NativeUi.Glow(cr,NativeCyberTheme.Magenta(.4f*(pulse-1)/.07f),24);NativeHudChrome.Tag(cr,NativeCyberTheme.Magenta());NativeUi.Text(new Rect(cr.x+14,cr.y,cw,cr.height),chips,12,NativeCyberTheme.Magenta(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
                    NativeHudChrome.Key(new Rect(cr.xMax-26,cr.y+6,18,18),"C");NativeUi.Pop();}
                if(p.Hp<=0)Prompt(H/2+5,arena.InShaft(p)?"FALLING THROUGH THE VENT":row.SelfAt>=0?"Self-revive in "+Math.Ceiling(row.SelfAt-arena.Time)+"s":"DOWNED  ·  A teammate can revive you",16,NativeCyberTheme.Danger());
            }
        }
    }
}
