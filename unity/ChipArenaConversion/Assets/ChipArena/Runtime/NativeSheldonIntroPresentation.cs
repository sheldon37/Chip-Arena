using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The Sheldon Intro on screen (Core/NativeSheldonIntro.cs): the pilot staged in the Sheldon Head look with its scripted
    // face, the full-screen glitch pass over the finished frame, and the corrupted intro card with the name glitch-typing in.
    public sealed partial class ArenaPresentation
    {
        // The intro pilot's scripted face (read by SheldonHead while DirectedIntro draws the pilot; off everywhere else).
        private bool sheldonScripted;private SheldonFace sheldonScriptFace;
        /// <summary>The frame (Time.frameCount) the Sheldon Intro last staged, and its glitch and glow colour (linear) for the screen pass.</summary>
        public int GlitchFrame{get;private set;}=-1;
        public NativeSheldonGlitch Glitch{get;private set;}
        public Color GlitchTint{get;private set;}=Color.cyan;
        /// <summary>
        /// Starts a Sheldon Intro frame: the script's effects tier, the pilot copied in the Sheldon Head look (the hood whatever
        /// hat is equipped, the suit dark, the glow and accents in its hat colour, cyan on Auto), the face for this moment and
        /// the screen glitch. Returns the actor to draw (the pilot's own actor is untouched).
        /// </summary>
        private Actor SheldonIntroBegin(NativeSheldonIntroScript script,Actor actor,NativePilot pilot,float t)
        {
            bool reduced=HaloReduced;script.Reduced=reduced;
            var look=actor.ViewCopy();look.Hat=NativeSheldonHead.Hat;look.HatColor=pilot.Seat.hatColor;look.BodyColor=NativeSheldonIntro.Suit;look.AccentColor=NativeSheldonIntro.Accent(pilot.Seat.hatColor);
            sheldonScriptFace=NativeSheldonIntro.Face(t,reduced);
            Glitch=NativeSheldonIntro.Glitch(t,reduced,LowGraphics);GlitchTint=HatBody(NativeSheldonHead.Hat,pilot.Seat.hatColor)??Color.cyan;GlitchFrame=Time.frameCount;
            return look;
        }
    }

    public sealed partial class ConversionGame
    {
        // The glitch pass follows the intro: on for a frame the Sheldon Intro staged (match or Locker preview), off otherwise.
        private void GlitchScreen()
        {
            bool on=presentation!=null&&presentation.GlitchFrame==Time.frameCount;
            NativeGlitchScreenPass.Frame(on?presentation.Glitch:default,on?presentation.GlitchTint:Color.black,(float)Screen.width/Mathf.Max(1,Screen.height),Screen.height);
        }

        // ---- the corrupted intro card (IntroCard for the Sheldon Intro) ----
        private readonly char[] sheldonType=new char[96];
        private string SheldonTyped(string text,float since,int seed,out bool done)
        {
            int n=NativeSheldonIntro.Typed(text,since,prefs.reducedEffects,seed,sheldonType,out done);return n<=0?"":new string(sheldonType,0,n);
        }
        // An RGB-split copy of a line: magenta and cyan, pulled apart and sliced on glitch steps (none with reduced effects).
        private void SheldonSplit(Rect line,string text,float size,float alpha,float split,int step,bool bold,bool mono,float tracking)
        {
            if(prefs.reducedEffects||split<.02f||Event.current.type!=EventType.Repaint||string.IsNullOrEmpty(text))return;
            for(int copy=0;copy<2;copy++)
            {
                float top=NativeSheldonIntro.Hash(step*7+copy,201)*.6f,bottom=Mathf.Min(1,top+.25f+.5f*NativeSheldonIntro.Hash(step*7+copy,203)),dx=(copy==0?-1:1)*(2+10*split)*(.5f+NativeSheldonIntro.Hash(step,205));
                U.BeginBand(new Rect(line.x-line.width,line.y+line.height*top,line.width*3,line.height*(bottom-top)));
                U.Text(new Rect(line.x+dx,line.y,line.width,line.height),text,size,copy==0?C.Magenta(.75f*alpha):C.Cyan(.75f*alpha),TextAnchor.MiddleCenter,bold:bold,mono:mono,tracking:tracking);U.EndBand();
            }
        }
        private void SheldonCard(NativePilot pilot,float rawAge,int index,int total,bool team)
        {
            const string style=NativeSheldonIntro.Style;float duration=(float)NativeIntroRules.OverlayDuration(style),nameAt=(float)NativeIntroRules.NameAt(style),age=Mathf.Min(rawAge,duration),W=U.W,H=U.H;
            bool reduced=prefs.reducedEffects;var g=NativeSheldonIntro.Glitch(age,reduced,presentation!=null&&presentation.LowGraphics);int step=NativeSheldonIntro.Step(age,reduced);float glitch=Mathf.Max(g.Tear,g.Split);
            // The cinema bands, as every entrance; the card goes dark with the blackout.
            float lit=1-g.Black;var ink=U.Hex(0x040610);
            U.Fade(new Rect(0,0,W,H*.17f),U.A(ink,.867f));U.Fade(new Rect(0,H*.7f,W,H*.3f+1),U.A(ink,.94f),false);
            float shade=M.EaseOut(M.Progress(age,nameAt,.7f)),ry=H*.46f*.92f;U.RadialStops(new Rect(-W*.1f,H-ry,W*1.2f,ry*2),U.A(ink,shade),new[]{0,.46f,.76f},new[]{.96f,.70f,0});
            // The topline: a hijacked feed typing in, its rule and progress bar in cyan, torn on the glitch hits.
            float top=M.EaseOut(M.Progress(age,0,.45f))*lit,x0=W*.06f,tw=W*.88f,y=88,jolt=reduced?0:(NativeSheldonIntro.Hash(step,207)-.5f)*24*g.Tear;
            string head=SheldonTyped("SIGNAL HIJACKED // "+(index+1).ToString("00")+" — "+total.ToString("00"),age,11,out _);
            U.Text(new Rect(x0+jolt,y,tw,15),head,12,C.Cyan(top),TextAnchor.UpperLeft,mono:true,tracking:.2f);
            float rule=y+29;U.Fill(new Rect(x0,rule,tw,1),C.Cyan(.25f*top));float fill=Mathf.Clamp01(age/duration);
            if(fill>0){U.HGradient(new Rect(x0,rule,tw*fill,1),C.CyanDeep(top),C.Ink(top));if(!reduced&&glitch>.3f)U.Fill(new Rect(x0+tw*fill*NativeSheldonIntro.Hash(step,209),rule-1,tw*.06f,3),C.Magenta(.7f*top));}
            if(age<nameAt)return;
            // The name block: each line glitch-types in (junk glyphs ahead of the cursor), RGB-split on the hits; it tears away at the end.
            float exit=M.Bezier(.4f,0,1,1,M.Progress(age,duration-.35f,.34f)),alpha=(1-exit)*lit,bottom=H*.95f,cx=W*.05f,cw=W*.9f,since=age-nameAt;
            float tear=reduced?0:(NativeSheldonIntro.Hash(step,211)-.5f)*(40*exit+18*g.Tear);
            var seat=pilot.Seat;string weapon=(content.weapons.FirstOrDefault(w=>w.id==seat.spec)?.name??seat.spec).ToUpperInvariant(),text=(string.IsNullOrWhiteSpace(seat.introText)?"Ready to make an impact":seat.introText).ToUpperInvariant();
            float F=Mathf.Clamp(W*.065f,30,82);float fit=U.Measure(seat.name,F,true,false,.02f).x;if(fit>cw)F*=cw/fit;
            float ySpan=bottom-18,yStrong=ySpan-9-1.15f*F,yTeam=yStrong-13-18,ySmall=(team?yTeam-8:yStrong-9)-15;var shadow=new Color(0,0,0,.8f*alpha);
            U.Text(new Rect(cx+tear*.5f,ySmall,cw,15),SheldonTyped(weapon,since,13,out _),12,C.CyanSoft(alpha),TextAnchor.UpperCenter,bold:true,tracking:.3f,shadow:shadow);
            if(team){var tc=TeamColor(seat.team);U.Text(new Rect(cx,yTeam,cw,18),SheldonTyped(NativeMatch.TeamNames[TeamIndex(seat.team)].ToUpperInvariant()+" TEAM",since-.07f,17,out _),14,U.A(tc,alpha),TextAnchor.UpperCenter,bold:true,tracking:.12f,shadow:shadow);}
            var nameLine=new Rect(cx+tear,yStrong,cw,1.15f*F);string name=SheldonTyped(seat.name??"",since-.12f,19,out bool typed);
            float split=Mathf.Max(g.Split,typed?0:.35f);SheldonSplit(nameLine,name,F,alpha,split,step,true,false,.02f);
            U.Text(nameLine,name,F,C.Ink(alpha),TextAnchor.MiddleCenter,bold:true,tracking:.02f,glow:C.Cyan(.6f*alpha),shadow:shadow);
            U.Text(new Rect(cx-tear*.5f,ySpan,cw,18),SheldonTyped(text,since-.3f,23,out _),12,C.Muted(alpha),TextAnchor.UpperCenter,mono:true,tracking:.3f);
        }
    }

    /// <summary>
    /// The Sheldon Intro's full-screen glitch (Resources/NativeGlitchScreen.shader): while a frame carries a visible glitch,
    /// NativeGlitchScreenImage is enabled on the last camera that draws to the screen (as the damage screen picks it), so its
    /// OnRenderImage runs the finished frame through the shader (Graphics.Blit, Unity's single-source image-effect path);
    /// otherwise it is disabled and costs nothing. Without the shader nothing is drawn. Release destroys the components;
    /// Release(true) also drops the material (ConversionGame.OnDestroy).
    /// </summary>
    public static class NativeGlitchScreenPass
    {
        private static NativeGlitchScreenImage image;private static Camera attached;private static Material material;private static bool missing;
        private static readonly List<NativeGlitchScreenImage> added=new List<NativeGlitchScreenImage>();private static Camera[] cameras=new Camera[8];
        private static readonly int idA=Shader.PropertyToID("_GlA"),idB=Shader.PropertyToID("_GlB"),idC=Shader.PropertyToID("_GlC"),idTint=Shader.PropertyToID("_GlTint");
        public static void Frame(NativeSheldonGlitch g,Color tint,float aspect,float height)
        {
            if(!g.Visible){Detach();return;}
            if(material==null){if(missing)return;var shader=Resources.Load<Shader>("NativeGlitchScreen");if(shader==null||!shader.isSupported){missing=true;return;}material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};}
            var camera=LastScreenCamera();if(camera==null){Detach();return;}
            if(attached!=camera||image==null){Detach();image=Image(camera);attached=camera;}
            if(!image.enabled)image.enabled=true;
            material.SetVector(idA,new Vector4(g.Tear,g.Split,g.Scan,g.Blocks));material.SetVector(idB,new Vector4(g.Black,g.Chroma,g.Noise,g.Seed%1000));
            material.SetVector(idC,new Vector4(aspect,g.Passes,Mathf.Max(1,height),0));material.SetColor(idTint,tint);
        }
        // The camera Unity renders last into the back buffer (the highest enabled depth, as NativeDamageScreenPass picks it).
        private static Camera LastScreenCamera()
        {
            if(cameras.Length<Camera.allCamerasCount)cameras=new Camera[Mathf.NextPowerOfTwo(Camera.allCamerasCount)];
            int n=Camera.GetAllCameras(cameras);Camera best=null;
            for(int i=0;i<n;i++){var c=cameras[i];cameras[i]=null;if(c==null||c.targetTexture!=null||!c.isActiveAndEnabled)continue;if(best==null||c.depth>=best.depth)best=c;}
            return best;
        }
        private static NativeGlitchScreenImage Image(Camera camera)
        {
            var c=camera.GetComponent<NativeGlitchScreenImage>();
            if(c==null){for(int i=added.Count-1;i>=0;i--)if(added[i]==null)added.RemoveAt(i);c=camera.gameObject.AddComponent<NativeGlitchScreenImage>();added.Add(c);}
            c.Material=material;return c;
        }
        private static void Drop(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        private static void Detach(){if(image!=null)image.enabled=false;image=null;attached=null;}
        public static void Release(bool all)
        {
            Detach();for(int i=0;i<added.Count;i++)Drop(added[i]);added.Clear();
            if(all){Drop(material);material=null;missing=false;}
        }
    }
}
