using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // Vote to skip a cutscene, on screen (Core NativeCutsceneSkip; owner 2026-11-08: "lets add a Vote to skip option on cutscenes
    // like boss intros. Once all players agree, the scene transitions to the next thing."). While a skippable cutscene plays,
    // F (the pad's Interact) casts this pilot's vote. A key-cap prompt at the bottom right says what it does and, with others in
    // the room, how many have voted (a dot each, filled as they vote). A pilot still to vote is invited with a soft pulse once
    // someone has. When the host skips, every screen cuts through a short black dip, so the jump from the cutscene to the fight is
    // never seen. Kit-drawn on the virtual canvas, drawn over the intro overlay and the letterbox; no strings built per frame.
    public sealed partial class ConversionGame
    {
        private NativeArena skipArena;private string skipKeySeen;private int skipSerialSeen=-1;private float skipShownAt=-100,skipDipAt=-100,skipVotedAt=-100;
        private const float SkipPromptH=38,SkipKeyW=26,SkipDot=8,SkipDotGap=5,SkipMargin=28;
        private const string SkipSolo="SKIP",SkipVote="VOTE TO SKIP",SkipVoted="VOTED · WAITING",SkipKeyLabel="F";
        private static readonly string[] SkipCounts=BuildSkipCounts();
        private static string[] BuildSkipCounts()
        {
            int n=NativeCutsceneSkip.MaxVoters+1;var a=new string[n*n];
            for(int v=0;v<n;v++)for(int r=0;r<n;r++)a[v*n+r]=v+"/"+r;return a;
        }
        private static string SkipCount(int votes,int need){int n=NativeCutsceneSkip.MaxVoters+1;return SkipCounts[Mathf.Clamp(votes,0,n-1)*n+Mathf.Clamp(need,0,n-1)];}
        private bool SkipPromptOn=>arena!=null&&!menu&&previewArena==null&&!ReplayActive&&arena.Local!=null&&arena.CutsceneSkip.Key!=null;
        private void CutsceneSkipKeys(bool typing)
        {
            if(!SkipPromptOn||typing||BoardOpen||StageInputHeld)return;
            float now=Time.unscaledTime;if(arena.CutsceneSkip.Voted(arena.Local.Seat.id)||now-skipVotedAt<.5f)return;
            if(Input.GetKeyDown(KeyCode.F)||PadDown(PadAction.Interact)){skipVotedAt=now;VoteSkipCutscene();U.Cue(NativeUiSounds.Click);}
        }
        private void CutsceneSkipGui()
        {
            if(dedicated||prefs==null)return;
            float now=Time.unscaledTime;var s=arena?.CutsceneSkip;
            // A skip the host made since the last look starts the dip (never on joining, or on a new arena's first sight).
            if(arena!=skipArena){skipArena=arena;skipSerialSeen=s?.Serial??-1;skipDipAt=-100;skipKeySeen=null;}
            if(s!=null&&s.Serial!=skipSerialSeen){if(skipSerialSeen>=0)skipDipAt=now;skipSerialSeen=s.Serial;}
            bool on=SkipPromptOn&&!devHideHud;
            if(!on)skipKeySeen=null;else if(s.Key!=skipKeySeen){skipKeySeen=s.Key;skipShownAt=now;}
            float dip=now-skipDipAt;bool dipping=dip>=0&&dip<NativeCutsceneSkip.Dip;
            if(!on&&!dipping)return;
            var saved=GUI.matrix;
            try
            {
                var ui=U.Begin(prefs.uiScale);GUI.matrix=Matrix4x4.Scale(Vector3.one*ui.Scale);
                if(dipping){float a=1-Mathf.Clamp01(dip/NativeCutsceneSkip.Dip);a=a*a*(3-2*a);U.Fill(new Rect(-4,-4,ui.Width+8,ui.Height+8),new Color(0,0,0,a));}
                if(on)SkipPrompt(s,ui.Width,ui.Height,now);
            }
            finally{GUI.matrix=saved;}
        }
        private void SkipPrompt(NativeCutsceneSkip s,float width,float height,float now)
        {
            bool reduced=prefs.reducedEffects;int need=s.Required,votes=s.Votes;bool mine=s.Voted(arena.Local.Seat.id),many=need>1;
            // It eases in a beat after the cutscene starts (F counts from the first frame).
            float a=Mathf.Clamp01((now-skipShownAt-.35f)/.35f);if(a<=0)return;
            string label=mine?SkipVoted:many?SkipVote:SkipSolo;
            float labelW=U.Measure(label,13,true,false,1.5f).x,dotsW=many?need*SkipDot+(need-1)*SkipDotGap+10+U.Measure(SkipCount(votes,need),12,false,true).x:0;
            float w=12+SkipKeyW+10+labelW+(many?14+dotsW:0)+14;
            var r=new Rect(width-SkipMargin-w,height-SkipMargin-SkipPromptH,w,SkipPromptH);
            // Waiting on this pilot, with someone already in: a soft pulse on the border.
            float pulse=!mine&&votes>0?(reduced?.6f:.5f+.5f*Mathf.Sin(now*5)):0;
            U.Round(r,C.Night(.82f*a),SkipPromptH/2,mine?C.Cyan(.75f*a):Color.Lerp(C.Edge(.7f*a),C.Cyan(.9f*a),pulse));
            var key=new Rect(r.x+12,r.y+(SkipPromptH-SkipKeyW)/2,SkipKeyW,SkipKeyW);
            U.Round(key,mine?C.Cyan(.18f*a):C.Panel(.95f*a),6,mine?C.Cyan(.6f*a):C.Ink(.55f*a));
            U.Text(key,SkipKeyLabel,14,mine?C.Cyan(.8f*a):C.Ink(a),TextAnchor.MiddleCenter,true,true);
            float x=key.xMax+10;
            U.Text(new Rect(x,r.y,labelW+4,SkipPromptH),label,13,mine?C.Cyan(a):C.Ink(a),TextAnchor.MiddleLeft,true,false,1.5f);x+=labelW+14;
            if(!many)return;
            for(int i=0;i<need;i++)
            {
                var d=new Rect(x,r.y+(SkipPromptH-SkipDot)/2,SkipDot,SkipDot);
                if(i<votes)U.Disc(d,C.Cyan(a));else U.Round(d,C.Night(.4f*a),SkipDot/2,C.Muted(.8f*a));
                x+=SkipDot+SkipDotGap;
            }
            U.Text(new Rect(x+5,r.y,60,SkipPromptH),SkipCount(votes,need),12,C.Muted(a),TextAnchor.MiddleLeft,false,true);
        }
    }
}
