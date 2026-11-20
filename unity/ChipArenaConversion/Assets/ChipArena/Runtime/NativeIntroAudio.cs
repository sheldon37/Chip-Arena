using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Intros v2's sound (docs/INTROS-V2.md "Sound"): a registered intro's cue timeline (INativeIntroV2.Cues) on its presentation clock,
    // each cue heard once through the entrance cue clock (a late join or a guest's 20 Hz steps never double one), playing the first that
    // exists of its generated key, its existing bank key and its built-in clip. The intro's bed (INativeIntroV2.Music) plays through
    // the entrance sting path (NativeIntroRules.IntroSting). Presentation only.
    public sealed partial class NativeAudio
    {
        /// <summary>The v2 cues for this frame; false when the entrance is not a registered v2 intro (the caller plays its own).</summary>
        private bool IntroV2Foley(float drawnAge)
        {
            if(foleyInstance==null||!NativeIntroCatalog.Registered(foleyStyle))return false;
            var script=NativeEntranceDirector.Script(foleyStyle);if(!(script is INativeIntroV2 v2))return true;var cues=v2.Cues;if(cues==null||cues.Length==0)return true;
            entranceCues.Step(foleyInstance,foleyStyle,script.Clock(drawnAge));if(!bankLoaded)LoadBank();
            playingEntranceCue=true;
            try{for(int i=0;i<cues.Length;i++)if(entranceCues.Crossed(cues[i].At))IntroCue(cues[i]);}
            finally{playingEntranceCue=false;}
            return true;
        }
        /// <summary>The intro bed's playback time (s) while it plays, else -1 (muted, none, not started): what a v2 intro with a bed is drawn on.</summary>
        public double IntroBedPlayback()=>introVoice!=null&&introVoice.clip!=null&&introVoice.isPlaying?introVoice.time:-1;
        /// <summary>One v2 cue: its generated key, else its existing bank key, else its built-in clip (null: silent).</summary>
        private void IntroCue(in NativeIntroCue c)
        {
            float rate=c.Rate>0?c.Rate:1;
            if(c.Key!=null&&BankPlay(c.Key,null,c.Gain,0,null,true,rateScale:rate))return;
            if(c.Alt!=null&&BankPlay(c.Alt,null,c.Gain,0,null,true,rateScale:rate))return;
            if(c.Clip!=null)Play(c.Clip,null,c.Gain,rate);
        }
    }
}
