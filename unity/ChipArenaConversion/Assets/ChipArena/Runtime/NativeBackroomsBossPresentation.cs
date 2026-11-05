using System;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // What the two Backrooms bosses share in the game (docs/EXPEDITION-BOSS-BACKROOMS.md): the scene hook, the DEV previews' camera and the
    // intro's card. Each boss's own presenter is Runtime NativeHumBossPresentation.cs / NativeDeepEndBossPresentation.cs.
    public sealed partial class ArenaPresentation
    {
        private void BackroomsBossScenery(NativeArena arena){HumBossScenery(arena);DeepEndBossScenery(arena);}
        /// <summary>The boss's bank and body built ahead of the level (PrewarmStage), so its arrival builds nothing mid-fight. Nothing until the rules name this map's boss: a level with no boss reads neither bank.</summary>
        private bool BackroomsBossPrewarm(NativeArena arena)
        {
            string boss=arena==null?null:NativeZoneBossRules.Archetype(arena.MapId);
            if(boss==NativeHumBoss.Archetype){if(HumBankEnsure()){HumBodyEnsure();HumBodyHide();}return true;}
            if(boss==NativeDeepEndBoss.Archetype){DeepEndBodyBuild();DeepEndBodyHide();return true;}
            return false;
        }
        /// <summary>A DEV preview's camera this frame (NativeCinematicCamera.Direct), when one is running.</summary>
        internal bool BackroomsBossPreviewShot(out Vector3 eye,out Vector3 look,out float fov)=>HumBossPreviewShot(out eye,out look,out fov)||DeepEndBossPreviewShot(out eye,out look,out fov);
        /// <summary>The intro card to draw this frame and its age: a preview's, or a live boss's through the intro hold.</summary>
        internal bool BackroomsBossCardNow(NativeArena arena,out BossIntroCard card,out float age)
        {
            if(HumBossPreviewCard(out card,out age)||DeepEndBossPreviewCard(out card,out age))return true;
            var kind=arena?.ZoneBoss?.archetype;card=NativeBackroomsBossIntro.Card(kind);age=(float)(arena?.BossSongTime??-1);
            return card!=null&&arena.Pve?.Holding==true&&arena.Pve.Cinematic=="intro"&&age>=0;
        }
    }
    public sealed partial class ConversionGame
    {
        /// <summary>The card, in the Siren's manner (NativeMatchOverlays IntroCard, boss): the shades, the top line and progress bar over the hold,
        /// then the eyebrow, the title rising letter by letter and the line from the name time, gone just before the fight.</summary>
        private void BackroomsBossCard(BossIntroCard c,float rawAge,bool reduced)
        {
            float duration=NativeBackroomsBossIntro.Hold,nameAt=NativeBackroomsBossIntro.NameAt,age=Mathf.Min(rawAge,duration),W=U.W,H=U.H;var ink=U.Hex(c.Ink);
            float shade=Mathf.Lerp(c.Shades,1,M.Progress(age,nameAt-.2f,.6f));U.Fade(new Rect(0,0,W,H*.2f),U.A(ink,.867f*shade));U.Fade(new Rect(0,H*.6f,W,H*.4f+1),U.A(ink,.96f*shade),false);
            float top=M.EaseOut(M.Progress(age,0,.45f)),x0=W*.06f,tw=W*.88f,y=88;
            U.Text(new Rect(x0,y,tw,15),"BOSS",12,U.A(U.Hex(c.TopLine),top),TextAnchor.UpperLeft,mono:true,tracking:.2f);
            U.Text(new Rect(x0,y,tw,15),c.Title,12,U.A(U.Hex(c.TopLine),top),TextAnchor.UpperRight,mono:true,tracking:.42f);
            float rule=y+29;U.Fill(new Rect(x0,rule,tw,1),U.A(U.Hex(c.Rule),.25f*top));float fill=Mathf.Clamp01(age/duration);
            if(fill>0)U.HGradient(new Rect(x0,rule,tw*fill,1),U.A(U.Hex(c.BarFrom),top),U.A(U.Hex(c.BarTo),top));
            if(age<nameAt)return;
            float exit=M.Bezier(.4f,0,1,1,M.Progress(age,duration-.35f,.34f)),alpha=1-exit,bottom=H*.95f-10*exit,cx=W*.05f,cw=W*.9f;
            float F=Mathf.Clamp(W*.086f,48,132),lh=.95f*F,yLine=bottom-21,yTitle=yLine-10-lh,ySmall=yTitle-6-15;var shadow=new Color(0,0,0,.8f*alpha);
            float e=Line(age,nameAt);U.Text(new Rect(cx,ySmall+14*(1-e),cw,15),c.Eyebrow,12,U.A(U.Hex(c.TopLine),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.5f,shadow:U.A(shadow,e),blur:6*(1-e));
            string title=c.Title;float gap=.06f*F,total=0;var widths=new float[title.Length];
            for(int i=0;i<title.Length;i++){widths[i]=title[i]==' '?.3f*F:U.Measure(title[i].ToString(),F,true).x;total+=widths[i]+(i>0?gap:0);}
            float Cycle(float start)=>reduced||age<start?-1:Mathf.Repeat(age-start,2.4f)/2.4f;float red=Cycle(nameAt+.9f),cyan=Cycle(nameAt+1);
            float x=W/2-total/2;var block=new Rect(x,yTitle,total,lh);
            for(int i=0;i<title.Length;i++)
            {
                if(title[i]==' '){x+=widths[i]+gap;continue;}
                float u=Line(age,nameAt+i*.038f,.5f),seg=Mathf.Clamp01(u/.55f),op=Mathf.Clamp01(u/.4f)*alpha;var box=new Rect(x,yTitle,widths[i]+2,lh);string ch=title[i].ToString();
                U.Push(box.center,new Vector2(0,.4f*lh*(1-seg)),1,0,1.8f-.8f*seg);
                U.SplitCopy(block,box,red,false,ch,F,op,TextAnchor.MiddleLeft);U.SplitCopy(block,box,cyan,true,ch,F,op,TextAnchor.MiddleLeft);
                U.Text(box,ch,F,U.A(U.Hex(c.Letters),op),TextAnchor.MiddleLeft,bold:true,glow:U.A(U.Hex(c.Glow),.8f*op),shadow:U.A(Color.black,op));U.Pop();
                x+=widths[i]+gap;
            }
            e=Line(age,nameAt+.24f);U.Text(new Rect(cx,yLine+14*(1-e),cw,21),c.Line,14,U.A(U.Hex(c.TopLine),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.24f,shadow:U.A(shadow,e),italic:true,blur:6*(1-e));
        }
    }
    public sealed partial class NativeAudio
    {
        private void BackroomsBossCues(NativeArena arena,bool menu){HumBossCues(arena,menu);DeepEndBossCues(arena,menu);}
    }
}
