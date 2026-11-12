using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A holo screen's animation at a moment (NativeRooftopCity.ScreenAnim): its brightness, the glitch of a cut (0..1), the
    /// holo sweep's height (-1 between sweeps), the vertical signs' chase; on the rotating screens the slide showing, its age and
    /// length (s); on the old TV set the Ricochet card's place (0..1 across and up its free travel), how far it is on (0..1), the
    /// colour it took at its last bounce (0..5) and the seconds since that bounce.</summary>
    public struct RooftopScreenAnim {public float Pulse,Cut,Sweep,Chase,Age,Seconds;public int Slide;public float EggX,EggY,EggOn,EggTone,EggHit;}
    // The rooftop's holo screens as mounted signs, animated (owner 2026-10-29: "Can make the screens better designed on
    // buildings + animated"; 2026-11-11: "Changing animations happening on the TV screens as well, since things on there are
    // out dated"). Presentation only, deterministic. Each landmark screen hangs in a frame: a bezel, a dark casing behind the
    // panel, brackets back to its tower's face and a row of flood lamps along its top (ScreenFrame). The panels animate on the
    // scene clock (ScreenAnim): their brightness breathes, a holo sweep runs down them every 6 s, the vertical CHIP and ARENA
    // signs light letter by letter, and the three big screens rotate NativeRooftopAds' slides, cutting through a glitch: the
    // public classes on the largest (`classes`), the remodelled stages and the Expedition on the 4:3 one (`stages`), the game's
    // logo between two teasers on the far one (`brand`). The screen shader (NativeRooftopScreen) draws them from these values;
    // the screens' lights (Lights) breathe with them.
    // The easter egg (owner 2026-11-11: "Maybe reference the "Ricochet" thats on there currently as an easter egg lol"): the old
    // RICOCHET "RARE" chip card left the rotation; an old TV set on our roof (`retro`) shows static, and for EggSeconds of every
    // EggCycle the card bounces round it like a screensaver's logo, changing colour at every edge. Its travel is timed so that
    // half way through each showing it hits a corner dead on.
    public static partial class NativeRooftopCity
    {
        /// <summary>A screen kind's index for the screen shader (0 classes, 1 brand, 2 stages, 3 banner, 4 sign, 5 retro; -1 unknown).</summary>
        public static int ScreenKind(string kind)=>kind=="classes"?0:kind=="brand"?1:kind=="stages"?2:kind=="banner"?3:kind=="sign"?4:kind=="retro"?5:-1;
        /// <summary>The letters a vertical sign chases (CHIP on the banner, ARENA on our corner sign; 0 for the others).</summary>
        public static int ScreenLetters(string kind)=>kind=="banner"?4:kind=="sign"?5:0;
        public const float ScreenSweep=6,ScreenBreath=4,ScreenChase=4,SlideCut=.3f;
        /// <summary>The easter egg's timing (s): its cycle, when in it the card shows and for how long, and the time the card
        /// takes across and up the screen (their common multiple is the showing, so its middle is a corner hit), and the card's
        /// share of the screen's width and height (the screen shader's own .5).</summary>
        public const float EggCycle=96,EggFrom=70,EggSeconds=16,EggAcross=3.2f,EggUp=2,EggCard=.5f;
        /// <summary>The old TV set's screen on our roof (source frame): its centre, size and heading (toward the camera).</summary>
        public static readonly Vector3 RetroCenter=new Vector3(-1.2f,.78f,-.45f);public const float RetroWidth=.8f,RetroHeight=.6f,RetroYaw=1.02f;
        private static float ScreenPhase(int k)=>k==0?0:k==1?9:k==2?3:k==3?6:k==4?15:11;
        private static float Smooth(float a,float b,float x){float t=Clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);}
        private static float Wrap(float x,float p)=>x-p*(float)Math.Floor(x/p);
        // 0 at even whole numbers, 1 at odd ones (a bounce between two walls).
        private static float Tri(float x){float m=Wrap(x,2);return m>1?2-m:m;}
        /// <summary>A rotating screen's seconds a slide (0 for the others) and its start offset, so the three never cut together.</summary>
        public static float SlideSeconds(string kind)=>kind=="classes"?NativeRooftopAds.ClassSeconds:kind=="stages"?NativeRooftopAds.StageSeconds:kind=="brand"?NativeRooftopAds.BrandSeconds:0;
        private static float SlideOffset(int k)=>k==1?3:k==2?1.5f:0;
        /// <summary>The screen's animation at `time` (the scene clock, wrapped to NativeRooftopAds.Wrap by the caller); `slides`
        /// the count of its rotation (0: none).</summary>
        public static RooftopScreenAnim ScreenAnim(string kind,float time,int slides=0)
        {
            int k=ScreenKind(kind);float t=time+ScreenPhase(k);var a=new RooftopScreenAnim();
            a.Pulse=.92f+.08f*(float)Math.Sin(2*Math.PI*t/ScreenBreath);
            float s=Wrap(t,ScreenSweep);a.Sweep=s<1.6f?s/1.6f:-1;
            int letters=ScreenLetters(kind);a.Chase=letters>0?Wrap(t,ScreenChase)/ScreenChase*letters*1.8f:0;
            float sec=SlideSeconds(kind);
            if(sec>0&&slides>0)
            {
                float x=(time+SlideOffset(k))/sec;float whole=(float)Math.Floor(x);a.Slide=(int)Wrap(whole,slides);a.Age=(x-whole)*sec;a.Seconds=sec;
                a.Cut=Clamp(Math.Max(1-a.Age/SlideCut,(a.Age-(sec-SlideCut))/SlideCut),0,1);
            }
            if(k==5)
            {
                float u=Wrap(time,EggCycle)-EggFrom,tau=u-EggSeconds*.5f;
                a.EggOn=Smooth(0,.3f,u)*(1-Smooth(EggSeconds-.3f,EggSeconds,u));
                a.EggX=Tri(tau/EggAcross);a.EggY=Tri(tau/EggUp);
                float bx=tau/EggAcross,by=tau/EggUp;a.EggTone=Wrap((float)Math.Floor(bx)+(float)Math.Floor(by),6);
                a.EggHit=Math.Min((bx-(float)Math.Floor(bx))*EggAcross,(by-(float)Math.Floor(by))*EggUp);
                // the set cuts through a burst of static as the card comes and goes; it glows brighter while it shows
                a.Cut=Clamp(Math.Max(1-Math.Abs(u)/.35f,1-Math.Abs(u-EggSeconds)/.35f),0,1);a.Pulse*=.72f+.36f*a.EggOn;
            }
            return a;
        }

        // An oriented box about `c` with half extents along three orthogonal vectors (any order: made right-handed here), six
        // faces counter-clockwise about their normals, metric face coordinates.
        public static void Block(World w,Vector3 c,Vector3 ax,Vector3 ay,Vector3 az,Vector4 attr,int mode=Prop)
        {
            if(Vector3.Dot(Vector3.Cross(ax,ay),az)<0)ax=-ax;
            void Face(Vector3 n,Vector3 u,Vector3 v)
            {
                var o=c+n;var nn=Vector3.Normalize(n);
                w.Quads.Add(new RooftopQuad{A=o-u-v,B=o+u-v,C=o+u+v,D=o-u+v,Normal=nn,Uv=new Vector4(0,0,2*u.Length(),2*v.Length()),Mode=mode,Attr=attr});
            }
            Face(az,ax,ay);Face(-az,ay,ax);Face(ax,ay,az);Face(-ax,az,ay);Face(ay,az,ax);Face(-ay,ax,az);
        }
        // The frame of a landmark screen centred at `c`, facing the camera along yaw `ry`, `width` by `height`, its tower's face
        // `back` metres behind it along -z: the bezel round the panel (in front of nothing the camera aims at: the panel's
        // centre stays clear), the casing behind it, four brackets to the tower, flood lamps on the top rail.
        private static void ScreenFrame(World w,Vector3 c,float ry,float width,float height,float back)
        {
            var n=new Vector3((float)Math.Sin(ry),0,(float)Math.Cos(ry));var t=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,n));var y=Vector3.UnitY;
            if(w.Notes!=null)Note(w,"frame",c.X,c.Y,c.Z,ry,width,height,back);
            float size=Math.Max(width,height),b=Clamp(size*.022f,.28f,1.1f),depth=Math.Max(.5f,size*.02f);
            float hw=width*.5f,hh=height*.5f;var rail=n*(depth*.5f-depth*.35f);
            // the bezel: top and bottom rails over the full width, the stiles between them
            Block(w,c+y*(hh+b*.5f)+rail,t*(hw+b),y*(b*.5f),n*(depth*.5f),DarkSteel);
            Block(w,c-y*(hh+b*.5f)+rail,t*(hw+b),y*(b*.5f),n*(depth*.5f),DarkSteel);
            Block(w,c+t*(hw+b*.5f)+rail,t*(b*.5f),y*hh,n*(depth*.5f),DarkSteel);
            Block(w,c-t*(hw+b*.5f)+rail,t*(b*.5f),y*hh,n*(depth*.5f),DarkSteel);
            // the casing behind the panel and the brackets from its corners back to the tower's face
            float cd=depth*1.6f;Block(w,c-n*(.06f+cd*.5f),t*hw,y*hh,n*(cd*.5f),Mat(.020f,.022f,.030f,Metal));
            foreach(float sx in new[]{-1f,1f})foreach(float sy in new[]{-1f,1f})
            {
                var p=c+t*(sx*(hw-b))+y*(sy*(hh-b))-n*(.06f+cd);float face=c.Z-back+.05f;
                if(p.Z-face>.3f)Strut(w,p,new Vector3(p.X,p.Y,face),Math.Max(.25f,b*.7f),DarkSteel);
            }
            // flood lamps on the top rail, tipped at the panel; their glow
            int lamps=Math.Max(2,(int)Math.Round(width/9));
            for(int k=0;k<lamps;k++)
            {
                float f=(k+.5f)/lamps*2-1;var at=c+t*(f*hw*.92f)+y*(hh+b)+n*(depth*.35f+b*.3f);
                Block(w,at+y*(b*.35f),t*(b*.45f),y*(b*.35f),n*(b*.4f),Mat(1.6f,1.3f,1.0f,Lamp));
                w.Glows.Add(new RooftopSprite{Position=at+n*(b*.6f),Color=new Vector4(1.4f,1.2f,1.0f,.55f),Size=b*5,Kind=Glow,Seed=k*.37f});
            }
        }
    }
}
