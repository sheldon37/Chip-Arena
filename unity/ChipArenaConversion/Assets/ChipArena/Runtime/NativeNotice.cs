using System.Collections;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // main.ts notice(): a toast under the top bar (76 px; 150 px over a PvE match) for 6.5 s. It enters with notice-in
    // (.22 s, 8 px drop), a new message while one shows swaps the text and restarts the timer without replaying the
    // entrance, and it leaves instantly. Drawn over the menus and HUD, under the killcam, chip screens and the board.
    public sealed partial class ConversionGame
    {
        private string noticeText="";private float noticeShown=-10,noticeLast=-10;
        private void Notice(string text){float now=Time.unscaledTime;if(now-noticeLast>6.5f)noticeShown=now;noticeText=text??"";noticeLast=now;}
        private void NoticeToast()
        {
            float now=Time.unscaledTime;if(noticeText.Length==0||now-noticeLast>6.5f)return;
            if(arena!=null&&!menu&&(ReplayActive||PvePickScreen||SirenRevealActive||BoardOpen))return;
            // .notice: #17181d, 1 px #45464e with a 3 px #ff6b35 left edge, radius 4, 13 / 20 px padding, 14 px #f4f4f5 text,
            // 0 8px 32px #0007 shadow; shrink-to-fit up to min(600 px, half the canvas).
            float e=M.Bezier(.2f,.8f,.2f,1,M.Clamp01((now-noticeShown)/.22f)),max=Mathf.Min(600,U.W*.5f);var lines=U.Wrap(noticeText,14,max-44,false,4);float tw=0;foreach(var l in lines)tw=Mathf.Max(tw,U.Measure(l,14).x);
            float w=Mathf.Min(max,tw+44),h=28+lines.Count*17,top=arena?.Pve!=null&&!menu?150:76;var r=new Rect(U.W/2-w/2,top-8*(1-e),w,h);
            U.Glow(new Rect(r.x,r.y+8,r.width,r.height),new Color(0,0,0,.47f*e),18);U.Round(r,U.Hex(0x17181d,e),4,U.Hex(0x45464e,e));
            U.Round(new Rect(r.x,r.y,6,r.height),U.Hex(0xff6b35,e),4);U.Fill(new Rect(r.x+3,r.y+1,3,r.height-2),U.Hex(0x17181d,e));
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(r.x+23,r.y+14+i*17,w-44,17),lines[i],14,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft);
        }
        // Settings · Test Sound (audio.ts testSound): pistol now, fire at 230 ms, parry at 600 ms, then the status line.
        private string soundStatus="Click Test Sound to check audio";private int soundClips=-1;
        private IEnumerator TestSound()
        {
            ApplySettings();if(nativeAudio!=null)nativeAudio.Muted=!prefs.sound;if(soundClips<0)soundClips=Resources.LoadAll<AudioClip>("Audio/audio").Length;
            nativeAudio?.Play("pistol",null,.55f);yield return new WaitForSecondsRealtime(.23f);nativeAudio?.Play("fire",null,.45f);yield return new WaitForSecondsRealtime(.37f);nativeAudio?.Play("parry",null,.4f);
            soundStatus=AudioSettings.outputSampleRate<=0?"Audio unavailable":(prefs.sound?"Sound ready":"Muted")+" · "+soundClips+" effects loaded";
        }
    }
}
