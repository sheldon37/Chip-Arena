using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The rooftop's holo screens as mounted signs, animated (owner 2026-10-29: "Can make the screens better designed on
    // buildings + animated"). Presentation only, deterministic. Each landmark screen hangs in a frame: a bezel, a dark casing
    // behind the panel, brackets back to its tower's face and a row of flood lamps along its top (ScreenFrame). The panel
    // animates on the scene clock (ScreenState, every period dividing the 48 s loop): its brightness breathes, a holo sweep
    // runs down it every 6 s, the Siren and Chip Arena screens cut through a glitch to their own loop (a heartbeat, an
    // equalizer) for 4 s of every 24, and the vertical CHIP and ARENA signs light letter by letter. The screen shader
    // (NativeRooftopScreen) draws it from these values; the screens' light (Lights) breathes with them, so the spill on the
    // towers and our roof follows what the panel shows.
    public static partial class NativeRooftopCity
    {
        /// <summary>A screen kind's index for the screen shader (0 siren, 1 arena, 2 chip, 3 banner, 4 sign; -1 unknown).</summary>
        public static int ScreenKind(string kind)=>kind=="siren"?0:kind=="arena"?1:kind=="chip"?2:kind=="banner"?3:kind=="sign"?4:-1;
        /// <summary>The letters a vertical sign chases (CHIP on the banner, ARENA on our corner sign; 0 for the others).</summary>
        public static int ScreenLetters(string kind)=>kind=="banner"?4:kind=="sign"?5:0;
        public const float ScreenCycle=24,ScreenCut=4,ScreenSweep=6,ScreenBreath=4,ScreenChase=4;
        private static float ScreenPhase(int k)=>k==0?0:k==1?9:k==2?3:k==3?6:15;
        private static float Smooth(float a,float b,float x){float t=Clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);}
        private static float Wrap(float x,float p)=>x-p*(float)Math.Floor(x/p);
        /// <summary>
        /// The screen's animation at `time`: `pulse` its brightness (about .9-1.1), `mix` how far it has cut to its own loop (0
        /// the ad, 1 the loop; the Siren and Chip Arena only), `sweep` the holo sweep's height down the panel (0 top .. 1 bottom,
        /// -1 between sweeps), `chase` the vertical signs' lit letter (0 .. letters; beyond it every letter lit).
        /// </summary>
        public static void ScreenState(string kind,float time,out float pulse,out float mix,out float sweep,out float chase)
        {
            int k=ScreenKind(kind);float t=time+ScreenPhase(k);
            pulse=.92f+.08f*(float)Math.Sin(2*Math.PI*t/ScreenBreath);
            float u=Wrap(t,ScreenCycle),cut=ScreenCycle-ScreenCut-2;
            mix=k<=1?Smooth(cut,cut+.4f,u)*(1-Smooth(cut+ScreenCut-.4f,cut+ScreenCut,u)):0;
            float s=Wrap(t,ScreenSweep);sweep=s<1.6f?s/1.6f:-1;
            int letters=ScreenLetters(kind);chase=letters>0?Wrap(t,ScreenChase)/ScreenChase*letters*1.8f:0;
            // the loop's beat: the heartbeat's thump and the equalizer's kick lift the screen as it plays
            if(mix>0)pulse+=mix*.12f*(float)Math.Pow(Math.Max(0,Math.Sin(2*Math.PI*t/(k==0?1.2f:.75f))),8);
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
