using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Blind Pick weapon cards (owner 2026-11-17: "the same kind of screens we use for chip selection ... unique looks for
    // weapon selection ... like chips, but animated and styled with very high quality"). Drawn with the chip tray's kit
    // (NativeUi, the cyber palette, NativeHoloScan's highlight levels) in each weapon's own colour:
    //  - Grid: a compact armament card (class line, the weapon's icon on a turning reticle, its name). It boots in behind a
    //    scan wipe, lifts and scans on the highlight, wears the tray's brackets while it is the pilot's current choice.
    //  - Hero: the large card beside the grid: the icon on a holo emitter (beam, floor ring, turning reticle, pulse), the
    //    name with the event glitch when the choice changes, the role line, relative power / rate / reach bars and the exact
    //    stat line the loadout strip shows. Its content re-scans in whenever the shown weapon changes.
    //  - Back: the face-down card the reveal flips over; Stamp: LOCKED IN, struck over a card.
    // Presentation only: nothing here reads or writes rules, timers or the network. Repaint only, no transforms left pushed,
    // textures built on first use.
    public static partial class NativeWeaponCard
    {
        public struct Look
        {
            // Alpha: overall opacity. Build 0..1: the boot (scan wipe from the top). Hover 0..1: the highlight level (eased,
            // NativeHoloScan); HoverAge seconds since it engaged. Selected 0..1: the pilot's current choice; SelectAge since it
            // was chosen. Locked 0..1 and LockAge: the lock stamp. Recede 0..1: veiled while another card is highlighted.
            // Time: the screen clock. Index: the card's place (phase offsets). Calm: Reduced effects (no motion, no flicker).
            public float Alpha,Build,Hover,HoverAge,Selected,SelectAge,Locked,LockAge,Recede,Time;public int Index;public bool Calm;
            public static Look Idle(float time)=>new Look{Alpha=1,Build=1,Time=time,HoverAge=-10,SelectAge=-10,LockAge=-10};
        }
        // Relative bars, 0..1 against the weapons on offer (Measure).
        public struct Bars{public float Power,Rate,Reach;}

        private const float R=8,HeroR=12;
        private static Texture2D lines;
        private static readonly float[] RingAt={0,.86f,.92f,.97f,1},RingA={0,0,1,.3f,0},PulseAt={0,.8f,.9f,.96f,1},PulseA={0,0,1,.25f,0};
        private static readonly Color Void=U.Hex(0x05070d),PlateTop=U.Hex(0x111936),PlateHot=U.Hex(0x1a2554),PlateBottom=U.Hex(0x060a16),Keyline=U.Hex(0x03050b);
        private static void Ensure()
        {
            if(lines!=null)return;
            // Holo lines: a 1 px line every 3 px (as the loadout tiles').
            lines=new Texture2D(1,12,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            var px=new Color[12];for(int y=0;y<12;y++)px[y]=new Color(1,1,1,y>=8?1:0);lines.SetPixels(px);lines.Apply(false,true);
        }
        private static float Px(float v)=>Mathf.Max(1,v*U.S)/Mathf.Max(.01f,U.S);
        private static Rect Inset(Rect r,float d)=>new Rect(r.x+d,r.y+d,r.width-2*d,r.height-2*d);
        private static Rect Square(Vector2 c,float d)=>new Rect(c.x-d/2,c.y-d/2,d,d);

        // The weapon's colour, lifted so a dark one (Darkness, Water) still reads as light on navy.
        public static Color Accent(ContentWeapon w)
        {
            if(w==null||w.color<=0)return C.Cyan();Color.RGBToHSV(U.Hex((int)w.color),out float h,out float s,out float v);
            return Color.HSVToRGB(h,Mathf.Min(s,.82f),Mathf.Max(v,.88f));
        }
        public static string ClassLine(ContentWeapon w)=>w==null?"":w.devOnly?"DEV":NativeLoadoutText.Name(w.@class).ToUpperInvariant();
        public static string Tagline(ContentWeapon w)=>w==null?"":w.devOnly?"DEV CLASS":NativeLoadoutText.Tagline(w.@class);

        // Power is a hit's damage, Rate hits per second, Reach the range: each against the strongest on offer, Power and Reach on
        // a compressed scale so a melee reach of metres still shows beside a rifle's hundred. Indicators only: the stat line
        // under them carries the exact numbers.
        public static Bars Measure(ContentWeapon w,IList<ContentWeapon> set)
        {
            if(w==null)return default;double dmg=0,rate=0,reach=0;
            foreach(var o in set){if(o==null)continue;dmg=Math.Max(dmg,o.damage);rate=Math.Max(rate,o.interval>0?1/o.interval:0);reach=Math.Max(reach,o.range);}
            float p=dmg>0?Mathf.Sqrt((float)(Math.Max(0,w.damage)/dmg)):0,r=rate>0&&w.interval>0?(float)(1/w.interval/rate):0,re=reach>0?Mathf.Log(1+Mathf.Max(0,(float)w.range))/Mathf.Log(1+(float)reach):0;
            return new Bars{Power=Mathf.Clamp01(p),Rate=Mathf.Clamp01(r),Reach=Mathf.Clamp01(re)};
        }

        // ---- grid card ---------------------------------------------------------------------------------------------------
        public static void Draw(Rect r,ContentWeapon w,in Look k)
        {
            if(w==null||k.Alpha<=.002f||Event.current.type!=EventType.Repaint)return;Ensure();
            var acc=Accent(w);float a=k.Alpha,h=k.Hover,s=k.Selected,build=M.Clamp01(k.Build);
            float wipe=build>=1?r.yMax+40:r.y-4+(r.height+8)*M.OutCubic(build);
            // Footing and glow (outside the wipe: they grow with the boot).
            U.Glow(new Rect(r.x+8,r.yMax-6,r.width-16,8),new Color(0,0,0,.5f*a*build),12+6*h);
            if(s>0)U.Glow(r,U.A(acc,.2f*s*a*Breath(k)*build),14);if(h>0)U.Glow(r,U.A(acc,.13f*h*(1-.5f*s)*a),10);
            // While it boots the card is clipped above the wipe; rotated layers (the reticle) are drawn after every clip, since
            // IMGUI maps a clip through the matrix current at draw time (NativeLobbyUI's fold chevron).
            bool wiping=build<1;if(wiping)U.BeginBand(new Rect(r.x-24,r.y-24,r.width+48,Mathf.Max(0,wipe-r.y+24)));
            Plate(r,R,acc,a,h,s,k);
            var face=Inset(r,1);
            // Class line and the card's number.
            float pad=Mathf.Clamp(r.width*.075f,7,11),fs=Mathf.Clamp(r.width*.068f,8,10);
            U.Text(new Rect(r.x+pad,r.y+pad-2,r.width*.7f,fs+6),ClassLine(w),fs,U.A(U.Mix(C.Muted(),acc,.55f+.45f*Mathf.Max(h,s)),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.22f);
            U.Text(new Rect(r.xMax-pad-30,r.y+pad-2,30,fs+6),(k.Index+1).ToString("00"),fs,U.A(C.Dim(),a),TextAnchor.MiddleRight,mono:true);
            // The icon over its reticle.
            float size=r.width*.48f,lift=-5*h-2*s,bob=k.Calm?0:Mathf.Sin(k.Time*1.7f+k.Index*.9f)*1.1f*(.35f+.65f*h);
            var ic=new Rect(r.center.x-size/2,r.y+r.height*.43f-size/2+lift+bob,size,size);
            U.BeginBand(face);
            U.Radial(Inset(ic,-size*.6f),U.A(acc,(.14f+.2f*h+.22f*s)*a));
            if(!k.Calm&&h>0&&k.HoverAge>=0&&k.HoverAge<.55f)ScanLine(face,k.HoverAge/.55f,.5f*h*a,acc,22);
            U.EndBand();
            // The name, at most two lines, over the bottom accent line.
            float ns=Mathf.Clamp(r.width*.1f,11,14),lh=ns*1.18f;var rows=U.Wrap(w.name,ns,r.width-2*pad,true,2);float ny=r.yMax-pad-6-rows.Count*lh;
            foreach(var line in rows){U.Text(new Rect(r.x+4,ny,r.width-8,lh),line,ns,U.A(U.Mix(U.Mix(C.Muted(),C.Ink(),h),C.Ink(),s),a),TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,.5f*a));ny+=lh;}
            float bar=r.width*(.22f+.56f*Mathf.Max(h,s));var lineR=new Rect(r.center.x-bar/2,r.yMax-pad,bar,Px(2));U.Glow(lineR,U.A(acc,.35f*Mathf.Max(h,s)*a),4);U.Fill(lineR,U.A(U.Mix(acc,Color.white,.25f),(.35f+.65f*Mathf.Max(h,s))*a));
            Edge(r,R,acc,a,h,s,k,w.devOnly);
            if(wiping){U.EndBand();WipeLine(r,wipe,acc,a);}
            // Unclipped: the reticle and the icon, faded in as the wipe passes them; then the veil over the whole card.
            float rad=size*.66f,shown=wiping?M.Clamp01((wipe-(ic.center.y-rad))/(2*rad)):1;
            Reticle(ic.center,rad,k.Calm?0:k.Time*(18+30*h)+k.Index*23,U.A(acc,(.22f+.35f*h+.3f*s)*a*shown),6);
            Icon(ic,NativeIcons.Spec(w.id),acc,a*shown,h,s,k.HoverAge,k.Calm);
            if(k.Recede>0)U.Round(r,U.A(Void,.5f*k.Recede*a),R);
            if(k.Locked>0)Stamp(new Rect(r.x,r.y+r.height*.25f,r.width,r.height*.36f),k.Locked*a,k.LockAge,acc,k.Calm,.8f);
        }

        // ---- hero card ---------------------------------------------------------------------------------------------------
        // `change`: seconds since the shown weapon changed (the content re-scans in). `bars`: Measure's.
        public static void Hero(Rect r,ContentWeapon w,in Look k,Bars bars,float change)
        {
            if(w==null||k.Alpha<=.002f||Event.current.type!=EventType.Repaint)return;Ensure();
            var acc=Accent(w);float a=k.Alpha,s=Mathf.Max(k.Selected,.6f),h=k.Hover,swap=k.Calm?1:M.Clamp01(change/.42f),inA=a*M.OutCubic(swap);
            U.Glow(new Rect(r.x+10,r.y+30,r.width-20,r.height),new Color(0,0,0,.55f*a),44);U.Glow(r,U.A(acc,(.16f+.08f*Breath(k))*a),24);
            Plate(r,HeroR,acc,a,h,s,k);var face=Inset(r,1);float pad=Mathf.Clamp(r.width*.065f,16,26),x=r.x+pad,cw=r.width-2*pad,y=r.y+pad;
            // Header: the class icon and line, the class tagline on the right.
            var ci=new Rect(x,y,16,16);NativeIcons.Draw(ci,w.devOnly?"shuffle":NativeIcons.Class(w.@class),U.A(acc,a));
            U.Text(new Rect(x+22,y-2,cw*.5f,20),ClassLine(w),11,U.A(acc,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            U.Text(new Rect(x+cw*.4f,y-2,cw*.6f,20),Tagline(w),10,U.A(C.Muted(),a),TextAnchor.MiddleRight,mono:true,tracking:.16f);
            y+=26;U.HGradient(new Rect(x,y,cw*.5f,Px(1)),U.A(acc,0),U.A(acc,.45f*a));U.HGradient(new Rect(x+cw*.5f,y,cw*.5f,Px(1)),U.A(acc,.45f*a),U.A(acc,0));
            // The emitter: beam, floor ring, reticle, pulse and the icon.
            float em=Mathf.Min(r.height*.42f,cw*.95f),size=Mathf.Min(em*.62f,cw*.55f);var ec=new Vector2(r.center.x,y+12+em*.47f);
            float bob=k.Calm?0:Mathf.Sin(k.Time*1.4f)*2.2f;var ic=new Rect(ec.x-size/2,ec.y-size/2+bob-(1-swap)*10,size,size);float baseY=y+12+em*.93f;
            U.BeginBand(face);
            U.Radial(Square(ec,size*2.3f),U.A(acc,.2f*a));
            var beam=new Rect(ec.x-size*.42f,ec.y,size*.84f,baseY-ec.y);U.Fade(beam,U.A(acc,.13f*a),false);U.HGradient(new Rect(beam.x,beam.y,beam.width*.5f,beam.height),U.A(Void,0),U.A(Void,0));
            U.Push(new Vector2(ec.x,baseY),Vector2.zero,1,0,.26f);
            U.RadialStops(Square(new Vector2(ec.x,baseY),size*1.5f),U.A(acc,.7f*a),RingAt,RingA);U.Radial(Square(new Vector2(ec.x,baseY),size*1.2f),U.A(acc,.18f*a));
            U.Pop();
            if(!k.Calm){float p=Mathf.Repeat(k.Time/2.4f,1);U.RadialStops(Square(ec,size*(1.1f+1.3f*p)),U.A(acc,.45f*(1-p)*a),PulseAt,PulseA);}
            U.EndBand();
            // Rotated layers after the clip (they stay inside the face): the floor ring's ticks in perspective, two reticles.
            if(!k.Calm){U.Push(new Vector2(ec.x,baseY),Vector2.zero,1,0,.26f);Reticle(new Vector2(ec.x,baseY),size*.62f,k.Time*40,U.A(acc,.55f*a),12);U.Pop();}
            Reticle(ec,size*.78f,k.Calm?0:k.Time*-14,U.A(acc,.32f*a),8);Reticle(ec,size*.9f,k.Calm?0:k.Time*9+22,U.A(acc,.16f*a),24);
            Icon(ic,NativeIcons.Spec(w.id),acc,inA,Mathf.Max(h,.35f),s,k.Calm?-10:change,k.Calm);
            // A scan pass over the emitter as the content changes.
            if(!k.Calm&&change<.6f){U.BeginBand(new Rect(face.x,y,face.width,baseY-y+8));ScanLine(new Rect(face.x,y,face.width,baseY-y),change/.6f,.55f*a,acc,34);U.EndBand();}
            y=baseY+18;
            // The name (the event glitch on a change), the role.
            float ns=Mathf.Clamp(r.width*.075f,20,30);var nameR=new Rect(x,y,cw,ns*1.15f);
            NativeHudChrome.Glitch(nameR,w.name.ToUpperInvariant(),ns,change,inA,TextAnchor.MiddleLeft,k.Calm,true,false,.02f);
            U.Text(nameR,w.name.ToUpperInvariant(),ns,U.A(C.Ink(),inA),TextAnchor.MiddleLeft,bold:true,tracking:.02f,glow:U.A(acc,.25f*inA),shadow:new Color(0,0,0,.6f*inA));y+=ns*1.15f+4;
            foreach(var line in U.Wrap(NativeLoadoutText.Role(w),13,cw,false,2)){U.Text(new Rect(x,y,cw,18),line,13,U.A(U.Mix(C.Muted(),C.Ink(),.35f),inA),TextAnchor.MiddleLeft);y+=18;}
            y+=10;
            // Bars: they fill as the content scans in.
            float grow=k.Calm?1:M.Bezier(.2f,.8f,.2f,1,M.Clamp01((change-.08f)/.55f)),bh=Mathf.Clamp(r.height*.012f,5,7);
            y=Bar(x,y,cw,bh,"POWER",bars.Power*grow,acc,inA,k.Time,k.Calm);y=Bar(x,y,cw,bh,"RATE",bars.Rate*grow,acc,inA,k.Time,k.Calm);y=Bar(x,y,cw,bh,"REACH",bars.Reach*grow,acc,inA,k.Time,k.Calm);
            y+=6;U.Text(new Rect(x,y,cw+8,16),NativeLoadoutText.Stats(w),10.5f,U.A(U.Mix(C.Dim(),C.CyanSoft(),.7f),inA),TextAnchor.MiddleLeft,mono:true,tracking:.08f);y+=22;
            // The description's first lines, where the card has room.
            float room=r.yMax-pad-y;if(room>=34){int max=Mathf.Min(4,Mathf.FloorToInt(room/17));foreach(var line in U.Wrap(NativeLoadoutText.Description(w),12,cw,false,max)){U.Text(new Rect(x,y,cw,17),line,12,U.A(C.Dim(),inA),TextAnchor.MiddleLeft);y+=17;}}
            Edge(r,HeroR,acc,a,Mathf.Max(h,.4f),s,k,w.devOnly);
            if(k.Locked>0)Stamp(new Rect(r.x,ec.y-size*.32f,r.width,size*.64f),k.Locked*a,k.LockAge,acc,k.Calm,1.25f);
        }
        private static float Bar(float x,float y,float w,float h,string label,float v,Color acc,float a,float time,bool calm)
        {
            const int cells=14;float lw=58,bx=x+lw,bw=w-lw,gap=2,cell=(bw-(cells-1)*gap)/cells;
            U.Text(new Rect(x,y-4,lw,h+8),label,9.5f,U.A(C.Muted(),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            float filled=v*cells;
            for(int i=0;i<cells;i++)
            {
                var c=new Rect(bx+i*(cell+gap),y,cell,h);U.Fill(c,U.A(C.Edge(),.35f*a));float f=Mathf.Clamp01(filled-i);if(f<=0)continue;
                float shine=calm?0:Mathf.Max(0,Mathf.Sin(time*3-i*.45f))*.25f;U.Fill(new Rect(c.x,c.y,c.width*f,c.height),U.A(U.Mix(acc,Color.white,.1f+shine),(.55f+.45f*i/cells)*a));
            }
            if(filled>0){float hx=bx+Mathf.Min(filled,cells)*(cell+gap)-gap;U.Glow(new Rect(hx-2,y,2,h),U.A(acc,.6f*a),5);}
            return y+h+9;
        }

        // ---- back and stamp ----------------------------------------------------------------------------------------------
        // The face-down card: navy, holo lines, a turning reticle round a question mark, CLASSIFIED under it.
        public static void Back(Rect r,float alpha,float time,Color tone,bool calm)
        {
            if(alpha<=.002f||Event.current.type!=EventType.Repaint)return;Ensure();
            U.Glow(new Rect(r.x+8,r.yMax-6,r.width-16,8),new Color(0,0,0,.5f*alpha),14);
            U.RoundGradient(r,U.A(U.Hex(0x101633),.96f*alpha),U.A(PlateBottom,.98f*alpha),HeroR);var face=Inset(r,1);U.BeginBand(face);
            U.Tile(face,lines,U.A(tone,.05f*alpha),new Vector2(1,3));U.Radial(Square(face.center,face.width*1.1f),U.A(tone,.12f*alpha));
            for(int i=-3;i<=3;i++){float d=face.width*.22f*i;U.Fill(new Rect(face.x,face.center.y+d,face.width,Px(1)),U.A(tone,.05f*alpha));}
            U.EndBand();
            float size=Mathf.Min(r.width,r.height)*.42f;Reticle(face.center,size*.7f,calm?0:time*30,U.A(tone,.5f*alpha),10);U.RadialStops(Square(face.center,size*1.4f),U.A(tone,.55f*alpha),RingAt,RingA);
            U.Text(Square(face.center,size),"?",size*.62f,U.A(U.Mix(tone,Color.white,.4f),alpha),TextAnchor.MiddleCenter,bold:true,glow:U.A(tone,.4f*alpha));
            U.Text(new Rect(r.x,r.yMax-30,r.width,16),"CLASSIFIED",10,U.A(tone,.8f*alpha),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.3f);
            U.Round(r,Color.clear,HeroR,U.A(U.Mix(C.Edge(),tone,.5f),alpha),1.5f);Ticks(r,U.A(tone,.6f*alpha),10);
        }
        // LOCKED IN, struck at an angle across `band`: it slams in from 1.7x with a white flash, then rests with a slow glint.
        public static void Stamp(Rect band,float alpha,float age,Color acc,bool calm,float scale=1)
        {
            if(alpha<=.002f||Event.current.type!=EventType.Repaint)return;float t=calm?1:M.Clamp01(age/.2f),sc=Mathf.Lerp(1.7f,1,M.OutCubic(t))*scale,a=alpha*Mathf.Clamp01(t*2.5f);
            string text="LOCKED IN";float fs=13,tw=U.Measure(text,fs,true,true,.3f).x+28,th=30;var c=band.center;var box=new Rect(c.x-tw/2,c.y-th/2,tw,th);
            U.Push(c,Vector2.zero,sc,-7);
            U.Glow(box,U.A(acc,.35f*a),12);U.Round(box,U.A(Void,.82f*a),4,U.A(U.Mix(acc,Color.white,.3f),a),2);U.Round(Inset(box,3),Color.clear,2,U.A(acc,.45f*a));
            U.Text(box,text,fs,U.A(U.Mix(acc,Color.white,.55f),a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.3f,glow:U.A(acc,.4f*a));
            float flash=calm?0:1-M.Clamp01((age-.12f)/.3f);if(flash>0&&age>=0)U.Round(Inset(box,-2),new Color(1,1,1,.55f*flash*alpha),5);
            if(!calm){float g=Mathf.Repeat(age/2.6f,1);if(g<.3f){float gx=Mathf.Lerp(box.x+20,box.xMax-20,g/.3f);var gr=new Rect(gx-16,box.y+2,16,box.height-4);U.HGradient(gr,new Color(1,1,1,0),new Color(1,1,1,.22f*a));U.HGradient(new Rect(gx,gr.y,16,gr.height),new Color(1,1,1,.22f*a),new Color(1,1,1,0));}}
            U.Pop();
        }

        // ---- layers ------------------------------------------------------------------------------------------------------
        private static float Breath(in Look k)=>k.Calm?1:.82f+.18f*Mathf.Sin(k.Time*2.4f);
        // The plate: gradient glass in the weapon's tint, holo lines, a wash of its colour from the top, the slow sheen.
        private static void Plate(Rect r,float radius,Color acc,float a,float h,float s,in Look k)
        {
            Color top=U.Mix(U.Mix(PlateTop,PlateHot,h),U.Mix(PlateTop,acc,.28f),s*.7f);
            U.RoundGradient(r,U.A(top,.94f*a),U.A(PlateBottom,.97f*a),radius);
            U.HGradient(new Rect(r.x+radius,r.y+1,(r.width-2*radius)/2,Px(1)),Color.clear,new Color(1,1,1,(.08f+.08f*h)*a));U.HGradient(new Rect(r.center.x,r.y+1,(r.width-2*radius)/2,Px(1)),new Color(1,1,1,(.08f+.08f*h)*a),Color.clear);
            var face=Inset(r,1);U.BeginBand(face);
            U.Tile(face,lines,U.A(acc,(.04f+.03f*h+.02f*s)*a),new Vector2(1,3));
            U.Fade(new Rect(face.x,face.y,face.width,face.height*.6f),U.A(acc,(.05f+.07f*h+.08f*s)*a));
            if(!k.Calm)
            {   // A soft sheen crossing every few seconds, each card on its own phase (upright: no rotation inside the clip).
                float p=Mathf.Repeat(k.Time/5.2f+k.Index*.137f,1);if(p<.35f)
                {
                    float u=p/.35f,sw=face.width*.35f,cx=face.x-sw+(face.width+2*sw)*u;var band=new Rect(cx-sw/2,face.y,sw,face.height);
                    U.HGradient(new Rect(band.x,band.y,band.width/2,band.height),new Color(1,1,1,0),new Color(1,1,1,(.035f+.03f*h)*a));U.HGradient(new Rect(band.center.x,band.y,band.width/2,band.height),new Color(1,1,1,(.035f+.03f*h)*a),new Color(1,1,1,0));
                }
            }
            U.EndBand();
        }
        // The edge: rest, lit on the highlight, the weapon's colour when chosen; the chromatic flicker as the highlight lands,
        // corner ticks at rest, the DEV pip, the chosen card's brackets.
        private static void Edge(Rect r,float radius,Color acc,float a,float h,float s,in Look k,bool dev)
        {
            Color rest=C.Edge(),hot=U.Mix(rest,U.Mix(acc,Color.white,.35f),.65f);U.Round(r,Color.clear,radius,U.A(U.Mix(U.Mix(rest,hot,h),acc,s),a),s>.5f?1.5f:1);
            float f=k.Calm||k.HoverAge<0?0:Mathf.Clamp01(1-k.HoverAge/.22f)*Mathf.Clamp01(3*h);
            if(f>0){U.Round(new Rect(r.x-1.5f,r.y,r.width,r.height),Color.clear,radius,C.Magenta(.5f*f*a));U.Round(new Rect(r.x+1.5f,r.y,r.width,r.height),Color.clear,radius,C.Cyan(.5f*f*a));}
            float tick=(.3f+.45f*h)*(1-s)*a;if(tick>0)Ticks(r,U.A(acc,tick),Mathf.Min(9,r.width*.08f));
            if(dev){var d=new Rect(r.xMax-12,r.yMax-12,4,4);U.Glow(d,C.Violet(.5f*a),4);U.Fill(d,U.A(U.Mix(C.Violet(),Color.white,.35f),a));}
            if(s>0)
            {
                float settle=k.Calm||k.SelectAge<0?1:M.OutCubic(M.Clamp01(k.SelectAge/.22f)),off=Mathf.Lerp(12,4,settle)+(k.Calm?0:Mathf.Sin(k.Time*3)*.8f);
                Brackets(r,off,s*a*Mathf.Clamp01(.3f+settle),acc);
                float flash=k.Calm||k.SelectAge<0?0:1-M.Clamp01(k.SelectAge/.35f);if(flash>0)U.Round(r,new Color(1,1,1,.12f*flash*a),radius,U.A(U.Mix(acc,Color.white,.6f),flash*a),1.5f);
            }
        }
        private static void Ticks(Rect r,Color c,float arm)
        {
            float th=Px(1.5f),i=5;
            U.Fill(new Rect(r.x+i,r.y+i,arm,th),c);U.Fill(new Rect(r.x+i,r.y+i+th,th,arm-th),c);
            U.Fill(new Rect(r.xMax-i-arm,r.yMax-i-th,arm,th),c);U.Fill(new Rect(r.xMax-i-th,r.yMax-i-arm,th,arm-th),c);
        }
        private static void Brackets(Rect r,float off,float op,Color tone)
        {
            if(op<=.002f)return;op=Mathf.Min(1,op);const float th=2;float ax=Mathf.Min(16,r.width*.16f),ay=Mathf.Min(16,r.height*.14f),key=Px(1);var col=U.Mix(tone,Color.white,.35f);
            for(int c=0;c<4;c++)
            {
                bool right=c==1||c==2,bottom=c>=2;float px=right?r.xMax+off:r.x-off,py=bottom?r.yMax+off:r.y-off;
                var arm=new Rect(right?px-ax:px,bottom?py-th:py,ax,th);var leg=new Rect(right?px-th:px,bottom?py-ay:py+th,th,ay-th);
                U.Fill(new Rect(arm.x-key,arm.y-key,arm.width+2*key,arm.height+2*key),U.A(Keyline,.55f*op));U.Fill(new Rect(leg.x-key,leg.y,leg.width+2*key,leg.height),U.A(Keyline,.55f*op));
                U.Glow(arm,U.A(tone,.32f*op),5);U.Glow(leg,U.A(tone,.32f*op),5);U.Fill(arm,U.A(col,op));U.Fill(leg,U.A(col,op));
            }
        }
        // `n` short ticks on a circle of radius `rad` round `c`, turned `deg`, every other one longer.
        private static void Reticle(Vector2 c,float rad,float deg,Color col,int n)
        {
            if(col.a<=.002f)return;
            for(int i=0;i<n;i++)
            {
                bool major=i%2==0;float len=major?Mathf.Max(4,rad*.12f):Mathf.Max(2,rad*.06f);
                U.Push(c,Vector2.zero,1,deg+i*360f/n);U.Fill(new Rect(c.x-Px(1),c.y-rad-len/2,Px(major?2:1.5f),len),U.A(col,major?1:.6f));U.Pop();
            }
        }
        // The icon: a drop shadow, the chromatic split while the highlight lands, the icon pale-tinted at rest, in its
        // colour when chosen.
        private static void Icon(Rect ic,string icon,Color acc,float a,float h,float s,float hoverAge,bool calm)
        {
            if(a<=.002f)return;NativeIcons.Draw(new Rect(ic.x,ic.y+3,ic.width,ic.height),icon,new Color(0,0,0,.5f*a));
            float f=calm||hoverAge<0?0:Mathf.Clamp01(1-hoverAge/.28f)*Mathf.Clamp01(3*Mathf.Max(h,s)),dx=2*f;
            if(dx>0){NativeIcons.Draw(new Rect(ic.x-dx,ic.y,ic.width,ic.height),icon,C.Magenta(.55f*f*a));NativeIcons.Draw(new Rect(ic.x+dx,ic.y,ic.width,ic.height),icon,C.Cyan(.55f*f*a));}
            Color rest=U.A(U.Mix(C.Ink(),acc,.3f),.82f),hot=U.Mix(C.Ink(),acc,.22f),on=U.Mix(acc,Color.white,.4f);
            NativeIcons.Draw(ic,icon,U.A(U.Mix(U.Mix(rest,hot,h),on,s),a));
        }
        private static void ScanLine(Rect face,float p,float a,Color tone,float trail)
        {
            float y=face.y-6+(face.height+12)*p,th=Px(1),w=face.width;var head=U.A(U.Mix(tone,Color.white,.55f),a);
            U.Fade(new Rect(face.x,y-trail,w,trail),U.A(tone,.45f*a),false);
            U.HGradient(new Rect(face.x,y,w*.3f,th),Color.clear,head);U.Fill(new Rect(face.x+w*.3f,y,w*.4f,th),head);U.HGradient(new Rect(face.x+w*.7f,y,w*.3f,th),head,Color.clear);
        }
        // The boot's leading edge: a bright line with a glow just under the revealed part.
        private static void WipeLine(Rect r,float y,Color acc,float a)
        {
            if(y<r.y||y>r.yMax)return;var line=new Rect(r.x-2,y-Px(1),r.width+4,Px(2));U.Glow(line,U.A(acc,.6f*a),8);U.Fill(line,U.A(U.Mix(acc,Color.white,.6f),a));
            U.Fade(new Rect(r.x,y-18,r.width,18),U.A(acc,.18f*a),false);
        }
    }
}
