using System;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The DEV Cyber Lord on the HUD (Core/NativeCyberLordHud, Core/KernelPanicCutIn): its Q, E and 1-4 cards in the cyber card
    // drawer (CardAt) after RMB and SHIFT, and Kernel Panic's skill cut-in: the caster's live portrait (C14, NativePortraits)
    // splashed full screen on diagonal hardlight panels with the glitch title and a speed-line burst for the caster and the
    // victim (while their camera directs, so the HUD is down), and a slim banner along the top for everyone else that never
    // covers the crosshair. Reduced effects: no flash, strobe or RGB jitter. Shown only for a Cyber Lord's sequence: the class
    // is DEV-only, so DEV access gates it with the class.
    public sealed partial class ConversionGame
    {
        // ---- the cards (Cooldowns: after RMB and SHIFT, in place of the content Q / E cards) ----
        private void CyberLordCards(NativePilot p)
        {
            var v=presentation?.CyberLordViewOf(p.Pose?.Id??0);if(v==null)return;float t=arena.Time;
            foreach(int slot in NativeCyberLordHud.Order)
            {
                var c=NativeCyberLordHud.Of(v,slot,t,p.Mana);
                cards.Add(new Card{Key=NativeCyberLordHud.Labels[slot],State=c.State,Icon=c.Icon,Ready=c.Ready,Active=c.Active,Low=c.Low,Dial=c.Dial});
            }
        }

        // ---- Kernel Panic's skill cut-in (participants, full screen) and banner (everyone else) ----
        private readonly char[] panicTitle=new char[KernelPanicCutIn.Title.Length];private string panicNames;private int panicNamesKey=-1;
        private static readonly float[] SpeedAngles=SpeedLines();
        private static float[] SpeedLines(){var a=new float[28];for(int i=0;i<a.Length;i++){uint h=(uint)(i*2654435761u+97);a[i]=(h>>8)%3600/10f;}return a;}
        private static int Rgb(string hex,int fallback){int c=NativeHatLooks.Rgb(hex);return c<0?fallback:c;}
        private string panicTitleText;private int panicTitleKey=-1;
        private string PanicTitle(int typed,int seed){int key=typed*100000+(typed>=KernelPanicCutIn.Title.Length?0:seed);if(key!=panicTitleKey||panicTitleText==null){int n=KernelPanicCutIn.Typed(typed,seed,panicTitle);panicTitleText=new string(panicTitle,0,n);panicTitleKey=key;}return panicTitleText;}
        private string PanicNames(CyberLordView v)
        {
            int key=v.Id*100003+v.PanicVictim;if(key==panicNamesKey&&panicNames!=null)return panicNames;string caster="",victim="";
            foreach(var q in arena.Pilots){if(q?.Seat==null)continue;if(q.Seat.id==v.Id)caster=q.Seat.name;if(q.Seat.id==v.PanicVictim)victim=q.Seat.name;}
            panicNamesKey=key;return panicNames=(caster??"").ToUpperInvariant()+"  ›  "+(victim??"").ToUpperInvariant();
        }
        /// <summary>The caster's and the victim's splash, over the directed frame (the HUD is down while it directs).</summary>
        private void KernelPanicSplash(float width,float height)
        {
            if(arena==null||presentation==null||Event.current.type!=EventType.Repaint)return;var v=presentation.KernelPanicCutInView(arena.Time,out float age);if(v==null||!KernelPanicCutIn.Splashes(v,arena.LocalId))return;
            bool reduced=prefs.reducedEffects;var s=KernelPanicCutIn.At(age,reduced);if(s.Alpha<=0&&s.Flash<=0)return;float W=U.W,H=U.H,a=s.Alpha;
            U.Fill(new Rect(-4,-4,W+8,H+8),C.Void(.62f*a));
            // The diagonal hardlight panels: the portrait band between two thin bands, sliding in and out along their line.
            var centre=new Vector2(W/2,H/2);
            for(int i=0;i<3;i++)
            {
                float off=KernelPanicCutIn.BandAt[i]*H,th=KernelPanicCutIn.BandThick[i]*H,slide=(i%2==0?1:-1)*s.Slide*W*1.2f;
                U.Push(centre,new Vector2(slide,0),1,KernelPanicCutIn.Angle);
                var band=new Rect(centre.x-W*1.2f,centre.y+off-th/2,W*2.4f,th);
                if(i==1){U.Fill(band,C.Navy(.92f*a));U.Fill(new Rect(band.x,band.y,band.width,3),C.Cyan(a));U.Fill(new Rect(band.x,band.yMax-3,band.width,3),C.Magenta(.9f*a));
                    for(float x=band.x;x<band.xMax;x+=46)U.Fill(new Rect(x,band.y+8,1,band.height-16),C.Cyan(.08f*a));}
                else{U.Fill(band,(i==0?C.Cyan(.82f*a):C.Magenta(.82f*a)));for(float x=band.x;x<band.xMax;x+=22)U.Fill(new Rect(x,band.y,9,band.height),C.Void(.25f*a));}
                U.Pop();
            }
            // The speed-line burst from behind the portrait.
            var burst=new Vector2(W*.3f,H*.52f);
            for(int i=0;i<SpeedAngles.Length;i++)
            {
                float len=(220+180*((i*37)%7)/7f)*s.Speed,r0=120+60*s.Speed;U.Push(burst,Vector2.zero,1,SpeedAngles[i]);
                U.Fill(new Rect(burst.x+r0,burst.y-1.2f,len,i%3==0?3:1.5f),i%4==0?C.Magenta(.55f*a):C.CyanSoft(.6f*a));U.Pop();
            }
            // The caster's live portrait (the C14 renderer: hat and colours), in the middle band.
            Actor caster=null;foreach(var q in arena.Pilots)if(q?.Seat!=null&&q.Seat.id==v.Id){caster=q.Pose;break;}
            var stage=new Rect(W*.06f-s.Slide*W*.3f,H*.16f,H*.62f,H*.7f);
            if(caster!=null)
            {
                NativePilot seat=null;foreach(var q in arena.Pilots)if(q?.Seat?.id==v.Id){seat=q;break;}
                var look=new PortraitLook{Spec=CyberLordSpec.Id,Pose=seat?.Seat.lobbyPose??"weapon",Hat=seat?.Seat.hat??"none",HatColor=seat?.Seat.hatColor??"",Body=Rgb(seat?.Seat.bodyColor,0xe5e7eb),Accent=Rgb(seat?.Seat.accentColor,0x22e6ff)};
                var tex=Portrait(9_100_000+v.Id,look,stage,out _);U.Glow(stage,C.Cyan(.25f*a),26);
                if(tex!=null)U.Image(stage,tex,new Color(1,1,1,a));else U.Text(stage,"◆",120,C.Cyan(a),TextAnchor.MiddleCenter);
            }
            // The title glitch-typing in with its RGB-split copies, the tag and the names.
            string title=PanicTitle(s.Typed,s.Seed);
            var line=new Rect(W*.42f+s.Slide*W*.25f,H*.38f,W*.56f,H*.2f);float size=Mathf.Min(118,W*.07f);
            if(s.Split>.02f){float dx=8+22*s.Split;U.Text(new Rect(line.x-dx,line.y,line.width,line.height),title,size,C.Magenta(.75f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.04f);U.Text(new Rect(line.x+dx,line.y,line.width,line.height),title,size,C.Cyan(.75f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.04f);}
            U.Text(line,title,size,C.Ink(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.04f,glow:C.Cyan(.45f*a));
            U.Text(new Rect(line.x+4,line.y-28,line.width,24),KernelPanicCutIn.Tag,15,C.CyanSoft(a),TextAnchor.MiddleLeft,mono:true,tracking:.18f);
            U.Text(new Rect(line.x+4,line.yMax+2,line.width,26),PanicNames(v),17,C.Ink(.9f*a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
            // The out tear: horizontal slices shoved sideways as it ends; the white connect flash.
            if(s.Tear>0)for(int i=0;i<8;i++){float y=H*((i*.13f+s.Seed*.071f)%1f),h=H*.03f;U.Fill(new Rect(((i%2==0)?-1:1)*s.Tear*W*.2f,y,W,h),C.Void(.7f*s.Tear));}
            if(s.Flash>.01f)U.Fill(new Rect(-4,-4,W+8,H+8),new Color(1,1,1,.85f*s.Flash));
        }
        /// <summary>Everyone else: the slim banner under the round tag for 1.5 s from the connect (never near the crosshair).</summary>
        private void KernelPanicBanner()
        {
            if(arena==null||presentation==null||Event.current.type!=EventType.Repaint)return;var v=presentation.KernelPanicCutInView(arena.Time,out float age);if(v==null||KernelPanicCutIn.Splashes(v,arena.LocalId))return;
            float k=KernelPanicCutIn.Banner(age);if(k<=0)return;float w=KernelPanicCutIn.BannerWidth,h=KernelPanicCutIn.BannerHeight;var r=new Rect(U.W/2-w/2,KernelPanicCutIn.BannerTop-(1-k)*(h+12),w,h);
            NativeHudChrome.Cut(r,C.Navy(NativeHudCyber.PanelAlpha*k),NativeHudCyber.TagCut,NativeHudChrome.Corners.All);NativeHudChrome.Outline(r,C.Magenta(.8f*k),NativeHudCyber.TagCut,NativeHudChrome.Corners.All);
            U.Fill(new Rect(r.x+NativeHudCyber.TagCut,r.yMax-2,(r.width-2*NativeHudCyber.TagCut)*Mathf.Clamp01(age/KernelPanicCutIn.Seconds),2),C.Cyan(.9f*k));
            U.Text(new Rect(r.x+12,r.y,120,h),KernelPanicCutIn.Title,12,C.Magenta(k),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            U.Text(new Rect(r.x+132,r.y,w-144,h),PanicNames(v),11,C.Ink(k),TextAnchor.MiddleRight,mono:true,tracking:.06f);
        }
    }
    public sealed partial class ArenaPresentation
    {
        /// <summary>The HUD's view of a Cyber Lord pilot this frame (null for any other).</summary>
        internal CyberLordView CyberLordViewOf(int id)=>cyberViews.TryGetValue(id,out var v)?v:null;
        /// <summary>The Kernel Panic sequence whose cut-in (and banner) is up at match time `time`, and the age since its connect.</summary>
        internal CyberLordView KernelPanicCutInView(float time,out float age)
        {
            age=0;CyberLordView best=null;
            foreach(var v in cyberViews.Values){if(!(v.PanicConnect>-90)||v.PanicOutcome!=CyberLordSpec.Connect&&v.PanicOutcome!=CyberLordSpec.EndedEarly)continue;float a=time-v.PanicConnect;if(a<0||a>=KernelPanicCutIn.BannerSeconds)continue;if(best==null||a<age){best=v;age=a;}}
            return best;
        }
    }
}
