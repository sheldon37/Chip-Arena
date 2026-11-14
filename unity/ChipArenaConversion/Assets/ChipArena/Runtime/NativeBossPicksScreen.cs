using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Boss Battle's waiting screen (Core NativeBossPicks; the owner, 2026-11-07): a pilot who has their five chips waits here while the others
    /// choose. "Waiting for 2/4 players to select their chips", "Still selecting: Ana, Bo", a pip per player (lit once they're in, breathing while they
    /// choose), and the shared countdown draining in the boss's colours. Repaint-only drawing over the frozen scene; reads the replicated picks.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly List<string> bossPickNames=new List<string>();private readonly List<bool> bossPickDone=new List<bool>();private float bossPickSince=-1;
        private bool BossPicksWaiting=>!menu&&arena?.Pve!=null&&arena.BossOpening&&!ReplayActive&&!PveFollowing&&match.Phase==NativePhase.Fight&&arena.Local!=null
            &&arena.Pve.Rows.TryGetValue(arena.Local.Seat.id,out var row)&&row.Picks.Count==0;
        private void BossPicksWaitScreen(float W,float H)
        {
            var v=arena.Pve;int total=0,still=0;bossPickNames.Clear();bossPickDone.Clear();
            foreach(var r in v.Rows.Values){var s=r.Pilot?.Seat;if(s==null||s.bot||!s.connected)continue;total++;bool done=r.Picks.Count==0;bossPickDone.Add(done);if(!done){still++;bossPickNames.Add(s.name);}}
            if(Event.current.type==EventType.Repaint&&bossPickSince<0)bossPickSince=Time.unscaledTime;float age=Time.unscaledTime-bossPickSince,enter=U.PanelIn(age);bool reduced=prefs.reducedEffects;
            NativeBossGuide.Tone(arena.ZoneBoss?.archetype,out int deepHex,out int lightHex);var deep=NativeArena.ColorOf(deepHex);var light=NativeArena.ColorOf(lightHex);
            U.Fill(new Rect(0,0,W,H),U.Hex(0x06070b,.6f*enter));
            float pw=Mathf.Min(620,W-48),ph=232;var panel=new Rect(Mathf.Round(W/2-pw/2),Mathf.Round(H/2-ph/2+(1-enter)*18),pw,ph);U.Panel(panel,enter);
            var shade=new Color(0,0,0,.8f*enter);float x=panel.x+28,w=panel.width-56,y=panel.y+22;
            U.Text(new Rect(x,y,w,15),"BOSS BATTLE · YOUR 5 CHIPS ARE IN",12,new Color(light.r,light.g,light.b,enter),TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:shade);y+=15+10;
            string title=still>0?NativeBossPicks.Waiting(still,total):"Everyone's in";
            var lines=U.Wrap(title,24,w);foreach(var line in lines){U.Text(new Rect(x,y,w,28),line,24,U.Hex(0xf4f4f5,enter),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:shade);y+=28;}y+=6;
            if(still>0){string who=NativeBossPicks.Still(bossPickNames);U.Text(new Rect(x,y,w,20),who,14,U.Hex(0xc3c5ce,enter),TextAnchor.MiddleLeft,shadow:shade);}
            // A pip per player: lit once they're in, breathing while they choose.
            float pip=12,gap=8,px=x,py=panel.yMax-88;
            for(int i=0;i<bossPickDone.Count;i++)
            {
                var r=new Rect(px,py,pip,pip);float breath=reduced?.6f:.45f+.35f*Mathf.Sin(Time.unscaledTime*3.4f+i*.9f);
                if(bossPickDone[i]){U.Glow(r,new Color(light.r,light.g,light.b,.35f*enter),6);U.Pill(r,new Color(light.r,light.g,light.b,enter),Color.clear);}
                else U.Pill(r,new Color(deep.r,deep.g,deep.b,.18f*enter),new Color(deep.r,deep.g,deep.b,breath*enter));
                px+=pip+gap;
            }
            // The shared countdown, draining in the boss's colours (its figures are the big clock at the top, NativeBossPicksClock.cs).
            float left=v.PickLeft,fill=NativeBossPicks.Fill(left);var bar=new Rect(x,panel.yMax-34,w,8);
            U.Pill(bar,U.Hex(0xffffff,.08f*enter),Color.clear);
            if(fill>0){var f=new Rect(bar.x,bar.y,Mathf.Max(bar.height,bar.width*fill),bar.height);bool urgent=NativeBossPicks.IsUrgent(left);var tone=urgent?U.Hex(0xff5a4e):deep;
                if(!reduced)U.Glow(f,new Color(tone.r,tone.g,tone.b,.3f*enter),8);U.Pill(f,new Color(tone.r,tone.g,tone.b,enter),Color.clear);}
            U.Text(new Rect(x,bar.y-22,w,16),"THE FIGHT STARTS WHEN EVERYONE IS IN, OR AT 0:00",10,U.Hex(0x9a9daa,enter),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f,shadow:shade);
        }
        private void BossPicksWaitReset(){if(!BossPicksWaiting)bossPickSince=-1;}
    }
}
