using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
using Smile=ChipArena.UnityConversion.NativeSirenSmile;
namespace ChipArena.UnityConversion
{
    // pve/siren-reveal.ts: for every player, the Siren's smile (1.4 s), then the fixed four-card track with the current chip
    // booting and "???" for the chips not reached yet (3.2 s), then a .4 s fade. Timed by the hold's own clock. The source
    // rewrites the overlay only when its window changes (smile, tray, fade), so the backdrop's own animations (its flames,
    // the backdrop grin, tears, the title glitch, the locked pulse, the card glitches) restart at each write; the spotlight
    // and the booting card carry the time since the tray began.
    public sealed partial class ConversionGame
    {
        // Asked by the HUD every GUI event of a Siren match: the repaint also warms the smile's textures in the background, so
        // they are ready long before the first reveal.
        private bool SirenRevealActive{get{if(arena?.Pve==null)return false;if(Event.current!=null&&Event.current.type==EventType.Repaint)NativeSirenSmileArt.Warm(U.W,U.H);return arena.Pve.Holding&&arena.Pve.Cinematic=="reveal"&&!menu;}}
        private const float SirenSmile=1.4f,SirenFade=.4f;
        private void SirenReveal()
        {
            var v=arena.Pve;float W=U.W,H=U.H,age=v.HoldDuration-v.HoldLeft,end=v.HoldDuration;int chip=Mathf.Clamp(v.Reveal,1,4);var track=NativeChipCard.Text.sirenTrack;if(track==null||track.Length==0)return;bool still=prefs.reducedEffects;
            if(age<SirenSmile){NativeSirenSmileArt.Scene(W,H,age,SirenSmile,still);return;}   // .siren-selection.pve-smile
            float since=age-SirenSmile,written=age>=end-SirenFade?end-SirenFade:SirenSmile,clock=age-written,fade=1-M.Clamp01((age-(end-SirenFade))/SirenFade);
            NativeSirenSmileArt.Backdrop(W,H,clock,fade,still);
            var current=track[chip-1];bool found=chip==3;
            // .pve-reveal: a centred flex column (gap 18, padding 24): the eyebrow, the spotlight (name, then the description at
            // 62ch), the four-card tray. Locked slots stretch to the row; revealed cards keep their own height (min 236).
            float F=Mathf.Clamp(W*.056f,34,84),ds=Mathf.Clamp(W*.015f,15,20),pw=Mathf.Min(62*.556f*ds,Mathf.Min(880,W*.92f));var lines=U.Wrap(current.description,ds,pw);
            float spotH=F+12+lines.Count*ds*1.45f,cw=Mathf.Min(228,(W-48-3*16)/4);var size=ChipSize.Tray(false,4);size.Name=19;float rowH=236;
            for(int i=0;i<track.Length&&i<chip;i++)rowH=Mathf.Max(rowH,NativeChipCard.Height(track[i],cw,size));
            float y=(H-(18+18+spotH+18+rowH))/2;
            string eyebrow=NativeSirenSmileArt.Label(found?1:0,chip,track.Length);
            U.Text(new Rect(0,y,W,18),eyebrow,12,U.Hex(0xc9bfff,fade),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.32f,glow:U.Hex(0x7d5cff,.6f*fade));y+=18+18;
            // .pve-reveal-spotlight: pve-spot-in (.7 s from the tray's start) about the block's centre; the name's glitch split.
            float op=fade,sx=1,sy=1,tx=0;if(!still){Smile.SpotIn(since/.7f,out float o,out sx,out sy,out tx);op*=o;}
            U.Push(new Vector2(W/2,y+spotH/2),new Vector2(tx,0),sx,0,sy);var title=new Rect(0,y,W,F);
            if(!still){float g=Mathf.Repeat(clock+.2f,1.8f)/1.8f,h=Mathf.Repeat(clock+.3f,1.8f)/1.8f;U.SplitCopy(title,title,g,false,current.name,F,op,TextAnchor.MiddleCenter);U.SplitCopy(title,title,h,true,current.name,F,op,TextAnchor.MiddleCenter);}
            // text-shadow: 0 0 30px #9b87ffcc, 0 4px 0 #000.
            U.Text(title,current.name,F,U.Hex(0x9b87ff,.8f*op),TextAnchor.MiddleCenter,bold:true,tracking:.01f,blur:15);
            U.Text(new Rect(title.x,title.y+4,title.width,title.height),current.name,F,new Color(0,0,0,op),TextAnchor.MiddleCenter,bold:true,tracking:.01f);U.Text(title,current.name,F,U.Hex(0xf6f3ff,op),TextAnchor.MiddleCenter,bold:true,tracking:.01f);
            float ly=y+F+12;foreach(var line in lines){U.Text(new Rect(0,ly,W,ds*1.45f),line,ds,U.Hex(0xddd6fb,op),TextAnchor.MiddleCenter,shadow:new Color(0,0,0,op));ly+=ds*1.45f;}
            U.Pop();y+=spotH+18;
            // .pve-reveal-tray: revealed chips (the current one booting with siren-boot, the earlier ones at .8), the rest locked.
            float tx0=(W-(cw*4+48))/2;
            for(int i=0;i<track.Length;i++)
            {
                if(i<chip)
                {
                    var r=new Rect(tx0+i*(cw+16),y,cw,Mathf.Max(236,NativeChipCard.Height(track[i],cw,size)));
                    // The booting chip: siren-boot's bar, unfold and jolt, and the die's power-on flicker (Core).
                    bool boot=i==chip-1;float ba=1,bsx=1,bsy=1,btx=0,bright=1;if(boot&&!still){Smile.SirenBoot(since,out ba,out bsx,out bsy,out btx);bright=Smile.BootBright(since);}
                    var look=ChipLook.Idle(still?0:clock);look.Alpha=ba*fade*(boot?1:.8f);look.Bright=bright;
                    U.Push(r.center,new Vector2(btx,0),bsx,0,bsy);
                    // .pve-boot: 0 0 0 1px #b7a8ff99, 0 0 60px #7d5cff99, 0 20px 60px #000.
                    if(boot){U.Glow(new Rect(r.x,r.y+20,r.width,r.height),new Color(0,0,0,look.Alpha),60);U.Glow(r,U.Hex(0x7d5cff,.6f*look.Alpha),60);NativeChipArt.PackageEdge(r,size.Radius,1,U.Hex(0xb7a8ff,.6f*look.Alpha),1);}
                    NativeChipCard.Draw(r,track[i],0,size,look);U.Pop();continue;
                }
                NativeSirenSmileArt.Locked(new Rect(tx0+i*(cw+16),y,cw,rowH),i+1,track.Length,still?-1:clock,fade);
            }
        }
    }

    // The smile's stage as textures: the flame tongues (one per side, row and scale bucket), the eyes, the grin and the
    // drop-shadow glow. Core/NativeSirenSmile.cs rasterises them at the drawn pixel size on a worker thread, started as soon
    // as a Siren match is on screen (and again when the window's scale changes), and the textures are made one per call
    // here, so the first smile does not stall; each keeps the density it was baked at, so a stale set still draws at the
    // right size until the new one lands. The glow is re-blurred into its kept texture while the grin opens (its width
    // quantised to 1/64). Nothing allocates per frame.
    internal static class NativeSirenSmileArt
    {
        private static Texture2D eyes,glow,scan,stripes,voidTex;private static float faceDensity;
        // The eyebrow ("FOUND HIM · " for the third chip) and the locked cards' "n / 4", made once each (no per-frame strings).
        private static readonly Dictionary<int,string> labels=new Dictionary<int,string>();
        public static string Label(int kind,int n,int count)
        {
            int key=(kind*16+n)*16+count;if(labels.TryGetValue(key,out var text))return text;
            text=kind==2?n+" / "+count:(kind==1?"FOUND HIM · ":"")+"THE SIREN CHOOSES · CHIP "+n+" / "+count;labels[key]=text;return text;
        }
        // The grin and its horizontal levels (1/2 .. 1/16 wide), picked by its scaleX so the slit is drawn from a filtered copy.
        private const int MouthLevels=5;private static readonly Texture2D[] mouth=new Texture2D[MouthLevels];
        private static readonly Smile.Glow glowBake=new Smile.Glow();
        // Tongue buckets: scales within a factor of 1.15 share one texture (its .9 px rim line stays within ~12%); a bucket
        // rasterises at most MaxTonguePx per path unit (the largest tongues at 3440 x 1440 and 4K upscale a little).
        private const float MaxTonguePx=14;
        private static int[] bucketOf;private static float[] bucketScale;private static bool[] bucketFront,bucketLeft;private static Texture2D[] tongues=new Texture2D[0];private static float[] tongueDensity=new float[0];
        private sealed class Baked{public float Face,Flame;public byte[][] Rgba;public int[] W,H;public float[] Density;}
        private static System.Threading.Tasks.Task<Baked> job;private static Texture2D[] pending;private static int uploaded;private static float liveFace=-1,liveFlame=-1;

        // .siren-selection.pve-smile over .pve-reveal[data-window=smile]: the void, the flames, the face, scanlines, tears.
        public static void Scene(float W,float H,float age,float duration,bool still)
        {
            float k=age/duration;U.Fill(new Rect(0,0,W,H),U.Hex(0x020103));Void(W,H,1);Flames(W,H,still?0:age,still?0:Smile.Rise(k),still,1);
            float stare=still?Smile.BackdropStareOpacity:Smile.StareOpacity(k),mo=1,ms=1;if(!still)Smile.GrinOpen(k,out mo,out ms);
            Face(W,H,Smile.StareTop,stare,mo,ms);Scan(W,H,1);
            if(!still){Tear(W,H,Smile.TearA,k,1);Tear(W,H,Smile.TearB,(age+Smile.TearBLead)/duration,1);}
        }
        // .pve-reveal's own background, then .pve-reveal-backdrop (opacity .55): the same stage on a 6 s clock written 1.4 s
        // in (so its grin is still opening), the stare fixed at .35 and 30% down. `clock` runs from the overlay's last write.
        public static void Backdrop(float W,float H,float clock,float fade,bool still)
        {
            const float group=Smile.BackdropOpacity;float k=(clock+1.4f)/6;Void(W,H,fade);
            float rise=still?0:Smile.Rise(k);
            // The group's opacity over an opaque flame row: draw the row whole and lay the backdrop's own background back over
            // the flame box at .45 (exact while the overlay is opaque; back tongues stay hidden behind front ones). While the
            // overlay fades out over the frozen frame that would veil the frame under the box, so the row fades as it is.
            if(fade>=1)
            {
                Flames(W,H,still?0:clock,rise,still,1);Smile.FlameBox(W,H,out _,out float by,out _,out float bh,out _,out _);float top=by+rise*bh;
                if(top<H){U.BeginBand(new Rect(0,top,W,H-top));Void(W,H,1-group);U.EndBand();}
            }
            else Flames(W,H,still?0:clock,rise,still,group*fade);
            float mo=1,ms=1;if(!still)Smile.GrinOpen(k,out mo,out ms);Face(W,H,Smile.BackdropStareTop,Smile.BackdropStareOpacity*group*fade,mo,ms);Scan(W,H,group*fade);
            if(!still){Tear(W,H,Smile.TearA,k,group*fade);Tear(W,H,Smile.TearB,(clock+1.4f+Smile.TearBLead)/6,group*fade);}
        }
        // radial-gradient(ellipse at 50% 100%, #2b1652f2, #07050cf7 58%, #020103fb): an ellipse through the far corners (radii
        // W/2 and H times sqrt 2), baked once as one texture (colour and alpha interpolated premultiplied, as CSS does) so a
        // translucent copy of it is a single layer.
        private static void Void(float W,float H,float alpha)
        {
            if(alpha<=0)return;if(voidTex==null)voidTex=VoidTexture();
            U.Image(new Rect(W/2-W*.70710678f,H-H*1.41421356f,W*1.41421356f,H*2.82842712f),voidTex,new Color(1,1,1,alpha));
        }
        private static Texture2D VoidTexture()
        {
            const int n=256;var px=new Color[n*n];Color c0=U.Hex(0x2b1652),c1=U.Hex(0x07050c),c2=U.Hex(0x020103);const float a0=.949f,a1=.969f,a2=.984f;
            for(int j=0;j<n;j++)for(int i=0;i<n;i++)
            {
                float dx=(i+.5f)/n*2-1,dy=(j+.5f)/n*2-1,r=Mathf.Sqrt(dx*dx+dy*dy);bool inner=r<.58f;float u=inner?r/.58f:Mathf.Clamp01((r-.58f)/.42f);
                Color ca=inner?c0:c1,cb=inner?c1:c2;float aa=inner?a0:a1,ab=inner?a1:a2,wa=aa*(1-u),wb=ab*u,a=wa+wb;
                px[j*n+i]=new Color((ca.r*wa+cb.r*wb)/a,(ca.g*wa+cb.g*wb)/a,(ca.b*wa+cb.b*wb)/a,a);
            }
            var t=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(px);t.Apply(false,true);return t;
        }
        // The flame row: back tongues, then front, each on its own lick, cut at the flame box's top edge (the svg viewport).
        private static void Flames(float W,float H,float t,float rise,bool still,float alpha)
        {
            if(alpha<=0)return;Warm(W,H);if(tongues.Length==0)return;var white=new Color(1,1,1,alpha);
            for(int i=0;i<Smile.TongueCount;i++)
            {
                Smile.Tongue(i,W,H,t,rise,still,out _,out _,out _,out float xx,out float xy,out float yy,out float ox,out float oy,out float clipY);
                int b=bucketOf[i];var tex=tongues[b];if(tex==null)continue;float d=tongueDensity[b];TongueBox(d,out float tongueX0,out float tongueY0);float tongueW=tex.width/d,tongueH=tex.height/d;
                float cut=Mathf.Max(0,clipY-tongueY0);if(cut>=tongueH)continue;
                Smile.Decompose(xx,xy,0,yy,out float theta,out float sx,out float sy,out float phi);
                U.Push(Vector2.zero,new Vector2(ox,oy),sx,theta,sy);U.Push(Vector2.zero,Vector2.zero,1,phi);
                U.Tile(new Rect(tongueX0,tongueY0+cut,tongueW,tongueH-cut),tex,white,new Vector2(tongueW,tongueH),new Vector2(0,cut));   // the texture's lower part
                U.Pop();U.Pop();
            }
        }
        // .siren-stare: the drop-shadow glow, the eyes, then the grin scaled about its origin, all at the stare's opacity.
        private static void Face(float W,float H,float top,float opacity,float mouthOpacity,float mouthScale)
        {
            if(opacity<=0)return;Warm(W,H);if(eyes==null||mouth[0]==null)return;Smile.Face(W,H,top,out float fx,out float fy,out float unit);var c=new Color(1,1,1,opacity);
            if(glowBake.Build(unit,Mathf.Round(mouthScale*64)/64,mouthOpacity,U.LinearBlend?LightCurve():null))glow=Upload(glow,glowBake.Width,glowBake.Height,glowBake.Rgba,true);
            float gu=glowBake.UnitsPerTexel*unit;U.Image(new Rect(fx+glowBake.X0*unit,fy+glowBake.Y0*unit,glowBake.Width*gu,glowBake.Height*gu),glow,c);
            float d=faceDensity,m=2/d;U.Image(new Rect(fx+(Smile.EyesX0-m)*unit,fy+(Smile.EyesY0-m)*unit,eyes.width/d*unit,eyes.height/d*unit),eyes,c);
            if(mouthOpacity<=0)return;U.Push(new Vector2(fx+Smile.MouthOriginX*unit,fy+Smile.MouthOriginY*unit),Vector2.zero,mouthScale,0,1);
            int level=Mathf.Clamp((int)Math.Floor(Math.Log(1/Math.Max(mouthScale,1e-3f),2)),0,MouthLevels-1);var mt=mouth[level];if(mt==null){mt=mouth[0];level=0;}
            U.Image(new Rect(fx+(Smile.MouthX0-m)*unit,fy+(Smile.MouthY0-m)*unit,mt.width*(1<<level)/d*unit,mt.height/d*unit),mt,new Color(1,1,1,opacity*mouthOpacity));U.Pop();
        }
        // The glow's alpha through NativeUi.LightAlpha, for a target that blends in linear light (as the kit's ramp textures).
        private static byte[] lightCurve;
        private static byte[] LightCurve()
        {if(lightCurve==null){lightCurve=new byte[256];for(int i=0;i<256;i++)lightCurve[i]=(byte)Mathf.Clamp(Mathf.RoundToInt(U.LightAlpha(i/255f)*255),0,255);}return lightCurve;}
        // .siren-cinema-scan: repeating-linear-gradient(0deg, #ffffff08 0 1px, transparent 1px 3px), counted from the bottom.
        private static void Scan(float W,float H,float alpha)
        {
            if(alpha<=0)return;if(scan==null){scan=new Texture2D(1,3,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};scan.SetPixels(new[]{Color.white,new Color(1,1,1,0),new Color(1,1,1,0)});scan.Apply(false,true);}
            U.Tile(new Rect(0,0,W,H),scan,new Color(1,1,1,8/255f*alpha),new Vector2(1,3),new Vector2(0,((-H%3)+3)%3));
        }
        // .siren-cinema-tear: 9vh, linear-gradient(90deg, transparent, #b7a8ff55 25%, #ffffff7a 50%, #2de2ff44 75%, transparent).
        private static void Tear(float W,float H,float top,float k,float alpha)
        {
            float op=Smile.Tear(k,out float dx)*alpha;if(op<=0)return;float y=H*top,h=H*Smile.TearHeight,q=W/4;
            U.HGradient(new Rect(dx,y,q,h),Color.clear,U.Hex(0xb7a8ff,.333f*op));U.HGradient(new Rect(dx+q,y,q,h),U.Hex(0xb7a8ff,.333f*op),U.Hex(0xffffff,.478f*op));
            U.HGradient(new Rect(dx+2*q,y,q,h),U.Hex(0xffffff,.478f*op),U.Hex(0x2de2ff,.267f*op));U.HGradient(new Rect(dx+3*q,y,q,h),U.Hex(0x2de2ff,.267f*op),Color.clear);
        }
        // .pve-locked-card: 1 px dashed #b7a8ff55 at radius 9 over repeating-linear-gradient(135deg, #0d0a14 0 10px, #0a0810 10px
        // 20px); "???" (900 54px, .08em, #4e4470, pve-locked pulse) over "n / 4" (600 11px mono, .2em, #6f6790), gap 8.
        // `clock` < 0 holds the pulse (reduced motion: the span's own colour at full opacity).
        public static void Locked(Rect r,int n,int count,float clock,float fade)
        {
            U.Round(r,U.Hex(0x0a0810,fade),9);const float period=20*1.41421356f;
            if(stripes==null)stripes=Stripes();float inset=9*(1-.70710678f);U.Tile(new Rect(r.x+inset,r.y+inset,r.width-2*inset,r.height-2*inset),stripes,new Color(1,1,1,fade),new Vector2(period,period),new Vector2(inset,inset));
            InsetShadow(r,9,fade);DashedRound(r,U.Hex(0xb7a8ff,.333f*fade),9);
            float pulse=1,mix=0;if(clock>=0)pulse=Smile.LockedPulse(clock,out mix);float cy=r.center.y-37.5f;var q=new Rect(r.x,cy,r.width,54);
            U.Text(q,"???",54,U.Hex(0x7d5cff,.333f*fade*pulse),TextAnchor.MiddleCenter,bold:true,tracking:.08f,blur:9);   // text-shadow 0 0 18px #7d5cff55
            U.Text(q,"???",54,U.Mix(U.Hex(0x4e4470),U.Hex(0x6d5fb0),mix)*new Color(1,1,1,fade*pulse),TextAnchor.MiddleCenter,bold:true,tracking:.08f);
            U.Text(new Rect(r.x,cy+62,r.width,14),Label(2,n,count),11,U.Hex(0x6f6790,fade),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);
        }
        // box-shadow: inset 0 0 30px #000 (a Gaussian of standard deviation 15 px falling from .5 at the edge): four edge
        // ramps (the top and bottom ones turned) and the corner squares as radial ramps cut to their quadrant.
        private static readonly Vector2[] UnitSquare={new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
        private static readonly float[] InsetAt={0,.2f,.4f,.6f,.8f,1},InsetFall={.5f,.309f,.159f,.067f,.023f,0},InsetRise={0,.023f,.067f,.159f,.309f,.5f},CornerAt={0,.9f,1},CornerFall={.45f,.62f,0};
        private static void InsetShadow(Rect r,float radius,float alpha)
        {
            const float d=37.5f;var c=new Color(0,0,0,alpha);float l=r.width-2*radius,h=r.height-2*radius;if(l<=0||h<=0)return;
            U.Polygon(new Rect(r.x,r.y+radius,d,h),UnitSquare,InsetAt,InsetFall,c);
            U.Polygon(new Rect(r.xMax-d,r.y+radius,d,h),UnitSquare,InsetAt,InsetRise,c);
            U.Push(new Vector2(r.xMax-radius,r.y),Vector2.zero,1,90);U.Polygon(new Rect(r.xMax-radius,r.y,d,l),UnitSquare,InsetAt,InsetFall,c);U.Pop();
            U.Push(new Vector2(r.x+radius,r.yMax),Vector2.zero,1,-90);U.Polygon(new Rect(r.x+radius,r.yMax,d,l),UnitSquare,InsetAt,InsetFall,c);U.Pop();
            for(int k=0;k<4;k++)
            {
                float cx=k%2==0?r.x+radius:r.xMax-radius,cy=k<2?r.y+radius:r.yMax-radius;
                U.BeginBand(new Rect(k%2==0?r.x:cx,k<2?r.y:cy,radius,radius));U.RadialStops(new Rect(cx-radius,cy-radius,2*radius,2*radius),c,CornerAt,CornerFall);U.EndBand();
            }
        }
        // A 1 px dashed outline as Chromium draws thin dashed borders (3 px dashes, 3 px gaps) along the straight edges, and the
        // corner arcs as a faint solid ring.
        private static Texture2D dashH,dashV;
        private static void DashedRound(Rect r,Color c,float radius)
        {
            if(dashH==null){dashH=new Texture2D(6,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};dashV=new Texture2D(1,6,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
                var p=new Color[6];for(int i=0;i<6;i++)p[i]=new Color(1,1,1,i<3?1:0);dashH.SetPixels(p);dashH.Apply(false,true);var q=new Color[6];for(int i=0;i<6;i++)q[i]=new Color(1,1,1,i>=3?1:0);dashV.SetPixels(q);dashV.Apply(false,true);}
            float l=r.width-2*radius,h=r.height-2*radius;
            U.Tile(new Rect(r.x+radius,r.y,l,1),dashH,c,new Vector2(6,1));U.Tile(new Rect(r.x+radius,r.yMax-1,l,1),dashH,c,new Vector2(6,1));
            U.Tile(new Rect(r.x,r.y+radius,1,h),dashV,c,new Vector2(1,6));U.Tile(new Rect(r.xMax-1,r.y+radius,1,h),dashV,c,new Vector2(1,6));
            U.Round(r,Color.clear,radius,U.A(c,.35f));
        }
        private static Texture2D Stripes()
        {
            // One 20*sqrt2 square period of the 135deg stripes: colour by (x + y) / sqrt2 mod 20 from the box's top-left corner.
            const int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};var px=new Color[n*n];
            Color a=U.Hex(0x0d0a14),b=U.Hex(0x0a0810);float period=20*1.41421356f;
            for(int j=0;j<n;j++)for(int i=0;i<n;i++){float x=(i+.5f)/n*period,y=period-(j+.5f)/n*period,s=(x+y)/1.41421356f%20;px[j*n+i]=s<10?a:b;}
            t.SetPixels(px);t.Apply(false,true);return t;
        }

        // ---- textures ----
        // The window's densities (pixels per view-box unit) of the face and of the flame row.
        private static void Densities(float W,float H,out float face,out float flame)
        {face=Mathf.Round(Smile.StareWidth(W)/Smile.ViewWidth*U.S*100)/100;Smile.FlameBox(W,H,out _,out _,out _,out _,out float u,out _);flame=Mathf.Round(u*U.S*100)/100;}
        // Starts (or finishes) the bake for this window: a worker rasterises every texture, then one is made per call.
        public static void Warm(float W,float H)
        {
            Buckets();Densities(W,H,out float face,out float flame);if(face==liveFace&&flame==liveFlame)return;
            if(job==null){float line=Smile.RimStrokeWidth*U.S;job=System.Threading.Tasks.Task.Run(()=>BakeAll(face,flame,line));uploaded=0;return;}
            if(!job.IsCompleted)return;
            if(job.IsFaulted||job.Result==null){Debug.LogWarning("Siren smile textures failed to bake: "+job.Exception?.GetBaseException().Message);Discard();liveFace=face;liveFlame=flame;return;}
            var b=job.Result;if(b.Face!=face||b.Flame!=flame){Discard();return;}   // the window changed while it baked (or uploaded)
            int n=b.Rgba.Length;if(pending==null||pending.Length!=n)pending=new Texture2D[n];
            if(uploaded<n){pending[uploaded]=Upload(null,b.W[uploaded],b.H[uploaded],b.Rgba[uploaded],false);uploaded++;if(uploaded<n)return;}
            // All made: swap them in.
            if(eyes!=null)UnityEngine.Object.Destroy(eyes);foreach(var t in mouth)if(t!=null)UnityEngine.Object.Destroy(t);foreach(var t in tongues)if(t!=null)UnityEngine.Object.Destroy(t);
            const int first=1+MouthLevels;eyes=pending[0];for(int l=0;l<MouthLevels;l++)mouth[l]=pending[1+l];faceDensity=b.Density[0];tongues=new Texture2D[n-first];tongueDensity=new float[n-first];
            for(int i=first;i<n;i++){tongues[i-first]=pending[i];tongueDensity[i-first]=b.Density[i];}
            pending=null;job=null;liveFace=face;liveFlame=flame;
        }
        // Drops an unfinished bake, destroying any of its textures already made so a resize mid-upload leaks nothing.
        private static void Discard()
        {
            if(pending!=null)for(int i=0;i<pending.Length;i++)if(pending[i]!=null)UnityEngine.Object.Destroy(pending[i]);
            pending=null;uploaded=0;job=null;
        }
        /// <summary>Blocks until this window's textures are in (the offline renders).</summary>
        internal static void Finish(float W,float H){for(int guard=0;guard<64;guard++){Warm(W,H);Densities(W,H,out float face,out float flame);if(job==null&&face==liveFace&&flame==liveFlame)return;job?.Wait();}}
        // Worker: the eyes, the grin (and teeth) with its horizontal levels, then every tongue bucket, as RGBA bytes.
        private static Baked BakeAll(float face,float flame,float line)
        {
            const int first=1+MouthLevels;int n=first+bucketScale.Length;var b=new Baked{Face=face,Flame=flame,Rgba=new byte[n][],W=new int[n],H=new int[n],Density=new float[n]};float m=2/face;
            Raster(b,0,Smile.EyesX0-m,Smile.EyesY0-m,Smile.EyesX1+m,Smile.EyesY1+m,face,Smile.FaceRgb,new[]{Smile.Paint(Smile.Eyes,Smile.FaceRgb,1,0,0,0,0)});
            Raster(b,1,Smile.MouthX0-m,Smile.MouthY0-m,Smile.MouthX1+m,Smile.MouthY1+m,face,Smile.FaceRgb,new[]{Smile.Paint(Smile.Grin,Smile.FaceRgb,1,0,0,0,0),Smile.Paint(Smile.TeethStroke,Smile.TeethRgb,1,0,0,0,0)});
            for(int l=1;l<MouthLevels;l++){b.Rgba[1+l]=Smile.HalveWidth(b.Rgba[l],b.W[l],b.H[l],out int half,Smile.FaceRgb);b.W[1+l]=half;b.H[1+l]=b.H[l];b.Density[1+l]=face;}
            for(int k=0;k<bucketScale.Length;k++)
            {
                // The rim line is .9 px on screen whatever the tongue's scale (non-scaling stroke): at this bucket's density.
                float ppu=Math.Min(flame*bucketScale[k],MaxTonguePx),w=line*ppu/(flame*bucketScale[k]);bool front=bucketFront[k],left=bucketLeft[k];
                var rim=left?Smile.LeftTongue:Smile.RightTongue;var core=left?Smile.LeftCore:Smile.RightCore;
                var layers=front?new[]{Smile.Paint(rim,Smile.RimRgb,1,0,0,0,0),Smile.Paint(rim,Smile.RimStrokeRgb,Smile.RimStrokeOpacity,0,0,0,0,w),Smile.Paint(core,Smile.CoreRgb,1,0,0,0,0)}
                    :new[]{Smile.Paint(rim,Smile.BackRimRgb,1,0,0,0,0),Smile.Paint(rim,Smile.BackRimStrokeRgb,Smile.RimStrokeOpacity,0,0,0,0,w)};
                TongueBox(ppu,out float x0,out float y0);float mm=2/ppu;Raster(b,first+k,x0,y0,Smile.TongueX1+mm,Smile.TongueY1+mm,ppu,front?Smile.RimRgb:Smile.BackRimRgb,layers);
            }
            return b;
        }
        // A tongue texture's top-left in path units: the paths' bounds less 2 px of margin at its density.
        private static void TongueBox(float density,out float x0,out float y0){float m=2/density;x0=Smile.TongueX0-m;y0=Smile.TongueY0-m;}
        // Rasterises `layers` (their mapping set to the box [x0,x1] x [y0,y1] at `pxPerUnit`) into slot `i`.
        private static void Raster(Baked b,int i,float x0,float y0,float x1,float y1,float pxPerUnit,int bleed,Smile.Layer[] layers)
        {
            int w=Math.Max(1,(int)Math.Ceiling((x1-x0)*pxPerUnit)),h=Math.Max(1,(int)Math.Ceiling((y1-y0)*pxPerUnit));
            for(int k=0;k<layers.Length;k++){layers[k].Ax=layers[k].Ay=pxPerUnit;layers[k].Bx=-x0*pxPerUnit;layers[k].By=-y0*pxPerUnit;}
            var rgba=new byte[w*h*4];Smile.Render(rgba,w,h,layers,8,bleed);b.Rgba[i]=rgba;b.W[i]=w;b.H[i]=h;b.Density[i]=pxPerUnit;
        }
        private static Texture2D Upload(Texture2D t,int w,int h,byte[] rgba,bool keep)
        {
            if(t!=null&&(t.width!=w||t.height!=h)){UnityEngine.Object.Destroy(t);t=null;}
            if(t==null)t=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            t.LoadRawTextureData(rgba);t.Apply(false,!keep);return t;
        }
        // Groups the 27 tongues (front/back, left/right path, scale) into buckets once.
        private static void Buckets()
        {
            if(bucketOf!=null)return;int count=Smile.TongueCount;var of=new int[count];var keys=new List<(bool,bool,int)>();
            for(int i=0;i<count;i++){Smile.Tongue(i,1600,900,0,0,true,out bool front,out bool left,out float s,out _,out _,out _,out _,out _,out _);var key=(front,left,(int)Math.Round(Math.Log(s)/Math.Log(1.15)));int at=keys.IndexOf(key);if(at<0){at=keys.Count;keys.Add(key);}of[i]=at;}
            bucketScale=keys.Select(q=>(float)Math.Pow(1.15,q.Item3)).ToArray();bucketFront=keys.Select(q=>q.Item1).ToArray();bucketLeft=keys.Select(q=>q.Item2).ToArray();bucketOf=of;
        }
    }
}
