using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Boss Battle opening's countdown (Core NativeBossPicks; the owner, 2026-11-07: "make the text a lot more visible and well designed with
    /// glow. Like top center of screen, and show urgency when down to last 10 seconds"), drawn over the pick screen and the waiting screen alike:
    /// a dark plate at the top centre with the clock in big glowing figures in the boss's light, a bar draining under it. In the last
    /// NativeBossPicks.Urgent seconds it turns red: the figures beat on every new second (and the match countdown's tick sounds), a ring flies off
    /// the plate, the screen's edge breathes red, and the last three burn brighter. Reduced effects keep the colour and drop the motion.
    /// After it, in the fight, chips still to choose are a prompt with C (BossLeftoverHud). Repaint-only drawing; reads the replicated clock.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private int bossClockSecond=-1;private float bossLeftoverSince=-1;
        private void BossPicksClock()
        {
            if(arena?.Pve==null||!arena.BossOpening||menu||ReplayActive){bossClockSecond=-1;return;}
            float W=U.W,H=U.H,left=arena.Pve.PickLeft;bool urgent=NativeBossPicks.IsUrgent(left),reduced=prefs.reducedEffects,repaint=Event.current.type==EventType.Repaint;
            int second=NativeBossPicks.Second(left);
            // The tick of each urgent second (the match countdown's own sound), once as the number changes.
            if(repaint&&second!=bossClockSecond){if(urgent&&bossClockSecond>second)nativeAudio?.Cue("ui.countdown","countdown",null,second<=3?.8f:.55f,mine:true);bossClockSecond=second;}
            float beat=reduced?0:NativeBossPicks.Beat(left),since=NativeBossPicks.SinceTick(left),fill=NativeBossPicks.Fill(left);
            NativeBossGuide.Tone(arena.ZoneBoss?.archetype,out _,out int lightHex);var calm=NativeArena.ColorOf(lightHex);
            bool last=urgent&&second<=3;var hot=last?U.Hex(0xff2e45):U.Hex(0xff5a4e);var tone=urgent?hot:calm;
            Color A(Color c,float a)=>new Color(c.r,c.g,c.b,a);
            // The screen's edge breathes red on each urgent beat.
            if(urgent&&!reduced)U.Glow(new Rect(-30,-30,W+60,H+60),U.Hex(0xff1f33,(last?.1f:.05f)+(last?.2f:.13f)*beat),-60);
            float pw=360,ph=128;var plate=new Rect(Mathf.Round(W/2-pw/2),16,pw,ph);
            // A ring flies off the plate on each urgent second.
            if(urgent&&!reduced&&since<.6f){float g=since/.6f,grow=6+26*g;U.Round(new Rect(plate.x-grow,plate.y-grow,plate.width+grow*2,plate.height+grow*2),Color.clear,16+grow,A(tone,.55f*(1-g)*(1-g)),2);}
            U.Glow(plate,A(tone,(urgent?.24f:.15f)+.3f*beat),30);
            U.Round(plate,U.Hex(0x06070c,.88f),16,A(tone,urgent?.55f+.45f*beat:.4f),urgent?2:1);
            // Its label, the figures (big, glowing, beating while urgent) and the draining bar.
            string label=urgent?(last?"LAST SECONDS · CHOOSE NOW":"HURRY · CHOOSE YOUR CHIPS"):"CHOOSE YOUR CHIPS";
            U.Text(new Rect(plate.x,plate.y+12,pw,14),label,11,A(tone,.95f),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.26f);
            var digits=new Rect(plate.x,plate.y+28,pw,66);
            U.Push(digits.center,Vector2.zero,1+(last?.2f:.13f)*beat);
            U.Glow(new Rect(digits.center.x-86,digits.y+14,172,38),A(tone,.22f+.38f*beat),24);
            var ink=urgent?Color.Lerp(U.Hex(0xffe0e0),Color.white,beat):U.Hex(0xf6f7fb);
            U.Text(digits,NativeBossPicks.Clock(left),62,ink,TextAnchor.MiddleCenter,bold:true,mono:true,glow:A(tone,.95f),shadow:new Color(0,0,0,.7f));
            U.Pop();
            var bar=new Rect(plate.x+30,plate.yMax-24,pw-60,6);U.Pill(bar,U.Hex(0xffffff,.08f),Color.clear);
            // The last ten seconds' stretch of the bar, marked a second at a time.
            float zone=bar.width*NativeBossPicks.Urgent/NativeBossPicks.Seconds;
            for(int i=1;i<(int)NativeBossPicks.Urgent;i++){float x=bar.x+zone*i/NativeBossPicks.Urgent;U.Fill(new Rect(Mathf.Round(x),bar.y-3,1,bar.height+6),A(hot,urgent&&i<second?.7f:.22f));}
            if(fill>0){var f=new Rect(bar.x,bar.y,Mathf.Max(bar.height,bar.width*fill),bar.height);if(!reduced)U.Glow(f,A(tone,.4f+.3f*beat),9);U.Pill(f,A(tone,1),Color.clear);}
        }
        /// <summary>
        /// In the fight, chips the countdown left (the owner: "players can select rest when pressing C in boss fight"): for its first six seconds a
        /// prompt under the boss's bar (y 154: below THE HUM's bar and NOCTIEL's plate, above the arrival banner), magenta like the pending-chips tag; NativeBossPicks.Leftover with the C key, pulsing in. The tag itself stays
        /// (PveChipsTag) until they're chosen. Not while the pick screen is open, a hold plays, or the opening still runs.
        /// </summary>
        private void BossLeftoverHud(float W,float H)
        {
            if(arena?.Pve==null||arena.BossOpening||arena.Pve.Holding||PvePickOpen||arena.Local==null||!arena.Pve.Rows.TryGetValue(arena.Local.Seat.id,out var row)){if(arena?.Pve==null||arena.BossOpening)bossLeftoverSince=-1;return;}
            int n=0;foreach(var pick in row.Picks)if(pick.Source==NativeBossPicks.Source)n++;
            if(n==0){bossLeftoverSince=-1;return;}
            if(bossLeftoverSince<0)bossLeftoverSince=Time.unscaledTime;float age=Time.unscaledTime-bossLeftoverSince;if(age>=6)return;
            float a=Mathf.Clamp01(age/.25f)*Mathf.Clamp01((6-age)/.6f),pop=prefs.reducedEffects?1:1+.08f*Mathf.Exp(-age*6);
            string text=NativeBossPicks.Leftover(n);float tw=U.Measure(text,14,true,true,.14f).x,w=tw+28+26+70,h=40;var r=new Rect(Mathf.Round(W/2-w/2),154,w,h);
            var tone=NativeCyberTheme.Magenta();U.Push(r.center,Vector2.zero,pop);
            U.Glow(r,new Color(tone.r,tone.g,tone.b,.3f*a),22);NativeHudChrome.Tag(r,tone,a);
            U.Text(new Rect(r.x+18,r.y,tw+4,h),text,14,new Color(tone.r,tone.g,tone.b,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f,glow:new Color(tone.r,tone.g,tone.b,.6f*a));
            float kx=r.xMax-70;NativeHudChrome.Key(new Rect(kx,r.y+10,20,20),"C",a);U.Text(new Rect(kx+26,r.y,44,h),"OPEN",11,U.Hex(0xc3c5ce,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            U.Pop();
        }
    }
}
