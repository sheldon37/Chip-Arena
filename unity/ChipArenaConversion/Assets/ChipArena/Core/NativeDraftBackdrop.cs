using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One frame's inputs to the chip draft's backdrop (NativeDraftBackdrop.Step). The tint is the stage tint as sRGB 0..1 (the
    /// picker's colour, blending to the chosen chip's rarity after the pick; the PvE pick's tier); the camera is the draft
    /// camera in Unity world space; rows are fractions of the screen height from the top.
    /// </summary>
    public struct DraftBackdropInput
    {
        /// <summary>Dt: the stage's frame step (the presentation clock's, NativeDraftStage.Begin: unscaled, at most .1 s). Age: the
        /// pick's smoothed age (-1 before the pick). Pulse: the plant pulse's progress (NativeDraftPlant.Pulse, -1 outside it and
        /// with Reduced effects).</summary>
        public float Dt,Age,Pulse;
        public Vector3 Tint;
        /// <summary>Entering: the first frame of a draft view (the frame before drew something else: the arena, a killcam).
        /// Ward: the PvE / Siren personal pick. Siren: the Siren drafter. Prism: the rainbow pick (eight max-rarity choices).</summary>
        public bool Entering,Reduced,Low,Ward,Siren,Prism;
        /// <summary>HeadRow: the heading's bottom edge; TrayRow: the tray's lower shade line; DeepRow: where that shade turns opaque
        /// (the backdrop quad ends a little under it: nothing behind the opaque shade is drawn). Negative until known.</summary>
        public float HeadRow,TrayRow,DeepRow;
        public Vector3 CamPos,CamForward,CamUp,CamRight;public float TanY,Aspect,Near,Far;
        /// <summary>The soft glow's centre behind the pilot (Unity world: between the solar plexus and the head, a little behind).</summary>
        public Vector3 Glow;
    }

    /// <summary>
    /// The backdrop's eased state and this frame's packed uniforms (one per draft stage, allocated once; Step writes it in
    /// place). Vectors are in Unity world space; colours are linear.
    /// </summary>
    public sealed class DraftBackdropState
    {
        /// <summary>Tint: the eased stage tint (sRGB). Presence 0..1: the fade in from a cold start. Boot: the power-on front's
        /// reach (m down the corridor behind the pilot). Time: the calm clock (the breathing, the panels, the drift; Motion-scaled). Flow: the
        /// flow clock (packets, streams, the scan, the twinkle): it runs faster with the pick's inflow and stops with Reduced
        /// effects, integrated so a change of speed never jumps. Both are rebased only while nothing is visible. Gains: the eased
        /// packets / scan / streams / glyph levels (Reduced and the low tier ease them down instead of cutting).</summary>
        public Vector3 Tint;public float Presence,Boot,Time,Flow;public Vector4 Gains;public bool Started;
        public Vector4 Cam,Clock,Fwd,Up,Right,Accent,Frame,Counter,Deep,Void,Pulse,Style,Fx,Safe,Cap,Glow;
        /// <summary>The backdrop quad: its distance in front of the camera and the screen rows of its top and bottom edges.</summary>
        public float Distance,Top,Bottom;
    }

    // The chip draft's animated cyber backdrop (owner 2026-10-12: "Make the chip selection have an animated cyber themed
    // background"; presentation only: nothing here is read by the simulation, the authority, the network or a save). One quad
    // in front of the draft camera, behind everything (Resources/NativeDraftBackdrop.shader), ray-casts a data corridor running
    // away behind the seated pilot, so every line of it leads to the pilot: a floor of circuit grid with data lanes, server-panel
    // walls (the PvE / Siren pick's ward of hexes) with two light rails and a lit trim along their foot, packets running down the
    // lanes and rails, glyph streams falling down a few wall columns, two frames of light (the scan) running down the corridor,
    // binary motes drifting up between the pilot and the far end, a slow breathing wave, and a navy haze brightening toward the
    // far end behind the pilot's head plus a soft glow behind the chest and head, so the silhouette reads. The frame (grid,
    // seams, ribs) is the cyber cyan of NativeCyberTheme; the light (trims, rails, packets, gates, glow, far haze) is the stage
    // tint (the picker's colour, the chosen chip's rarity after the pick, the PvE pick's tier), eased so a new picker's colour
    // or the pick's rarity blend never cut. The pick draws the packets in (Inflow: the flow clock runs faster); the plant pulse
    // sends a ring of light running away down the corridor and briefly brightens the hall. The Siren drafter gets the violet
    // ink look, the rainbow pick a slow prism down the corridor. Reduced effects: no packets, scan, streams' fall, twinkle,
    // ring, flash or power-on front; the hall barely breathes. The low graphics tier drops the motes and streams (a shader
    // keyword) for a cheaper pass.
    // Legibility: under the heading (HeadRow) and over the tray (TrayRow) the shader keeps only HeadDetail / TrayDetail of the
    // detail and caps the brightest channel at HeadCap / TrayCap (SafeAt / CapAt are its formulas), so the heading, the tray and
    // the highlighted card's text keep their contrast; elsewhere the hall stays under Ceiling (never reaching the bloom's
    // threshold of 1). Its brightest things are small (packets, trims); the pilot, lit by the stage key, stays the brightest
    // thing on screen. Nothing here changes the fog, the lights or the shadows (NativeLighting.Draft owns them).
    public static class NativeDraftBackdrop
    {
        // The NativeCyberTheme palette this backdrop uses (0xRRGGBB; Tests check them against Runtime/NativeCyberTheme.cs).
        public const int CyanHex=0x22e6ff,CyanDeepHex=0x0a8fb3,MagentaHex=0xff3dcf,VioletHex=0x8b5cff,VoidHex=0x05070f,NavyHex=0x0a1024,IndigoHex=0x131a3d;
        // The corridor (Unity world, the stage rig's root at y .88, the pilot at x 0 facing +z): the floor at FloorY, the walls
        // Half either side, rays reaching at most Reach (the shader's BACK_FLOOR, BACK_HALF and BACK_FAR).
        public const float FloorY=0,Half=4.6f,Reach=400;
        // Two colours: the light (trims, packets, gates, lit panels, the glow and the far haze) is the stage tint itself, its
        // brightest channel at 1, so a picker's colour or a rarity reads at once; the frame (grid, seams, ribs, hexes) is the
        // cyber cyan taking FrameShare of the tint, so the hall stays cyber whatever the tint (cyan and an orange tint mixed
        // evenly would go grey). The Siren drafter's ink: InkHaze of violet in the haze, InkFrame in the frame and InkLight in the
        // light, magenta packets.
        public const float FrameShare=.25f,InkHaze=.55f,InkFrame=.5f,InkLight=.45f;
        // Easing: the tint follows the stage tint at TintRate (1/s: 95% in .37 s); presence fades in over FadeIn (ReducedFadeIn);
        // the power-on front runs down the corridor from the pilot at BootSpeed m/s to BootReach; the gains ease at GainRate.
        public const float TintRate=8,FadeIn=.6f,ReducedFadeIn=.3f,BootSpeed=42,BootReach=34,GainRate=4;
        // The calm clock runs at Motion (ReducedMotion with Reduced effects: the hall barely breathes), the flow clock at Motion
        // times (1 + FlowBoost x the inflow) times the packets' gain; both go back to 0 on a cold start once the calm clock
        // passes RebaseAfter, while nothing is visible, so the shader's clocks stay precise.
        public const float Motion=1,ReducedMotion=.12f,RebaseAfter=900;
        // The breathing wave's depth (Reduced: ReducedBreath), the glow's level and size (screen heights; x is aspect-corrected).
        public const float Breath=.32f,ReducedBreath=.12f,GlowLevel=.06f,GlowWide=.30f,GlowTall=.42f;
        // The plant pulse: a ring of light runs away down the corridor from WaveStart to WaveStart + WaveReach metres behind the
        // pilot over the pulse (NativeDraftPlant.PulseStart..PulseEnd), easing out and fading (1 - p)^2, and the whole hall
        // brightens by Flash (1 - p)^2, NativeLighting.Draft's rim flash curve.
        public const float WaveStart=2,WaveReach=46,Flash=.55f;
        /// <summary>The plant ring's distance behind the pilot (m) at pulse progress p (0..1).</summary>
        public static float WaveAt(float p)=>WaveStart+WaveReach*(1-(1-p)*(1-p));
        // The pick's inflow: from the pick the packets rush down the corridor (the flow clock speeds up, InflowIn s), easing off as the
        // plant pulse starts (InflowOut s).
        public const float InflowIn=.3f,InflowOut=.45f,FlowBoost=1.6f;
        // Text-safe bands: under the heading the detail keeps HeadDetail and the brightest channel is capped at HeadCap; over the
        // tray TrayDetail and TrayCap. The heading band eases out from HeadRow - HeadIn to HeadRow + HeadOut; the tray band in from
        // TrayRow - TrayIn to TrayRow. Ceiling: the cap everywhere else. DefaultHead: the heading's row until the screen reports it.
        // HeadGap: the column behind the pilot's head the heading band leaves alone (screen heights either side of the centre, a
        // flat-topped exp(-x^4)), so the corridor's far glow still rims the head; it narrows away on screens narrower than 16:10
        // (GapAspect: none at 5:4), so it never reaches the heading's text (which ends about .47 screen heights from the left).
        public const float HeadDetail=.22f,TrayDetail=.15f,HeadCap=.02f,TrayCap=.014f,Ceiling=.7f,HeadIn=.01f,HeadOut=.07f,TrayIn=.06f,DefaultHead=.16f,HeadGap=.18f;
        public static readonly Vector2 GapAspect=new Vector2(1.25f,1.6f);
        /// <summary>The head column's half width (screen heights) for a screen of this aspect.</summary>
        public static float Gap(float aspect)=>HeadGap*SmoothStep(GapAspect.X,GapAspect.Y,aspect);
        // The quad: QuadReach of the far plane (at most QuadMax m) in front of the camera; it starts a little above the screen's
        // top (QuadTop) and ends QuadSlack under the tray shade's opaque row.
        public const float QuadReach=.5f,QuadMax=60,QuadTop=-.02f,QuadSlack=.03f;

        /// <summary>The exact sRGB decode.</summary>
        public static float Linear(float c)=>c<=.04045f?c/12.92f:(float)Math.Pow((c+.055f)/1.055f,2.4f);
        public static Vector3 Linear(Vector3 c)=>new Vector3(Linear(c.X),Linear(c.Y),Linear(c.Z));
        public static Vector3 Hex(int hex)=>new Vector3(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f);
        public static Vector3 LinearHex(int hex)=>Linear(Hex(hex));
        static float Clamp01(float v)=>v<0?0:v>1?1:v;
        static float Max3(Vector3 v)=>Math.Max(v.X,Math.Max(v.Y,v.Z));
        /// <summary>The shader's smoothstep.</summary>
        public static float SmoothStep(float a,float b,float x){float t=Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        // The shader's bands at a screen point (fractions from the left and the top; aspect the screen's): hs 1 out of the heading
        // band (under it, or in the column behind the head), ts 1 out of the tray band (above it).
        static void Bands(float col,float row,float aspect,Vector4 safe,Vector4 cap,out float hs,out float ts)
        {float x=(col-.5f)*aspect/Math.Max(cap.W,1e-3f);x*=x;hs=Math.Max(SmoothStep(safe.X-HeadIn,safe.X+HeadOut,row),(float)Math.Exp(-x*x));ts=1-SmoothStep(safe.Y-TrayIn,safe.Y,row);}
        /// <summary>The share of the backdrop's detail the shader keeps at a screen point (Safe: x head row, y tray row, z head
        /// detail, w tray detail; Cap.w the head column's width).</summary>
        public static float SafeAt(float col,float row,float aspect,Vector4 safe,Vector4 cap){Bands(col,row,aspect,safe,cap,out float hs,out float ts);return (safe.Z+(1-safe.Z)*hs)*(safe.W+(1-safe.W)*ts);}
        /// <summary>The cap on the brightest linear channel the shader applies at a screen point (Cap: x head, y tray, z elsewhere).</summary>
        public static float CapAt(float col,float row,float aspect,Vector4 safe,Vector4 cap){Bands(col,row,aspect,safe,cap,out float hs,out float ts);return Math.Min(cap.X+(cap.Z-cap.X)*hs,cap.Y+(cap.Z-cap.Y)*ts);}
        /// <summary>The pick's inflow 0..1 at the smoothed pick age (0 before the pick).</summary>
        public static float Inflow(float age)=>age<0?0:MathUtil.Smooth(age/InflowIn)*(1-MathUtil.Smooth((age-NativeDraftPlant.PulseStart)/InflowOut));
        // The glow's centre: GlowRise over the resting solar plexus (the upper chest, under the chin) and GlowBack behind it, so
        // it sits behind the chest and head whatever the gaze and the arms do (the Siren's chest is where the pilot's is).
        public const float GlowRise=.22f,GlowBack=.4f;
        /// <summary>The glow's centre in the stage's root frame (the Runtime and the offline renders convert it to Unity world).</summary>
        public static Vector3 GlowPoint(NativeDraftStage stage)=>stage.RestPlexus+new Vector3(0,GlowRise,-GlowBack);
        /// <summary>A Unity-world point as screen fractions (x from the left, y from the top) for the given camera basis.</summary>
        public static Vector2 Screen(Vector3 p,Vector3 eye,Vector3 forward,Vector3 up,Vector3 right,float tanY,float aspect)
        {
            var d=p-eye;float z=Math.Max(1e-4f,Vector3.Dot(d,forward));
            return new Vector2(.5f+.5f*Vector3.Dot(d,right)/(z*tanY*aspect),.5f-.5f*Vector3.Dot(d,up)/(z*tanY));
        }

        /// <summary>
        /// Advances the state one frame and packs the shader's uniforms. Allocation-free. A cold start (Entering, or the first
        /// frame ever) snaps the tint, fades the hall in from 0 and runs the power-on front down the corridor (Reduced: a plain
        /// fade); the clocks are rebased only then.
        /// </summary>
        public static void Step(DraftBackdropState s,in DraftBackdropInput i)
        {
            float dt=MathUtil.Finite(i.Dt)?Math.Max(0,Math.Min(.1f,i.Dt)):0;var tint=Finite(i.Tint)?new Vector3(Clamp01(i.Tint.X),Clamp01(i.Tint.Y),Clamp01(i.Tint.Z)):s.Tint;bool reduced=i.Reduced;
            if(i.Entering||!s.Started)
            {
                s.Started=true;s.Tint=tint;s.Presence=0;s.Boot=reduced?BootReach:0;
                if(s.Time>RebaseAfter||!MathUtil.Finite(s.Time)||!MathUtil.Finite(s.Flow)){s.Time=0;s.Flow=0;}s.Gains=Targets(i);
            }
            else
            {
                float k=1-(float)Math.Exp(-TintRate*dt);s.Tint+=(tint-s.Tint)*k;
                s.Presence=Math.Min(1,s.Presence+dt/(reduced?ReducedFadeIn:FadeIn));s.Boot=reduced?BootReach:Math.Min(BootReach,s.Boot+dt*BootSpeed);
                float g=1-(float)Math.Exp(-GainRate*dt);s.Gains+=(Targets(i)-s.Gains)*g;
            }
            // The pick's inflow speeds the flow clock up (packets rush down the corridor), the packets' gain stops it with Reduced effects.
            float inflow=reduced?0:Inflow(i.Age);s.Time+=dt*(reduced?ReducedMotion:Motion);s.Flow+=dt*Motion*(1+FlowBoost*inflow)*s.Gains.X;
            // The palette: the light is the stage tint (its brightest channel at 1, so every tint lights the hall alike), the
            // frame the cyber cyan with a little of it; the rainbow pick's prism is the shader's (its tint is white).
            var cyan=LinearHex(CyanHex);var accent=Linear(s.Tint);float peak=Max3(accent);accent=peak>1e-3f?accent/peak:cyan;
            var frame=cyan+(accent-cyan)*FrameShare;frame/=Math.Max(1e-3f,Max3(frame));
            float ink=i.Siren?1:0;var magenta=LinearHex(MagentaHex);var violet=LinearHex(VioletHex);var counter=magenta+(violet-magenta)*.3f*ink;var vn=violet/Max3(violet);frame+=(vn-frame)*InkFrame*ink;accent+=(vn-accent)*InkLight*ink;accent/=Math.Max(1e-3f,Max3(accent));
            var navy=LinearHex(NavyHex);var deep=navy*1.5f+accent*.004f;deep+=(violet*.014f-deep)*InkHaze*ink;var voidc=LinearHex(VoidHex);voidc+=(violet*.0035f-voidc)*.4f*ink;
            s.Accent=new Vector4(accent,s.Boot);s.Frame=new Vector4(frame,0);s.Counter=new Vector4(counter,ink);s.Deep=new Vector4(deep,reduced?ReducedBreath:Breath);s.Void=new Vector4(voidc,0);
            // The plant pulse (never with Reduced effects: the stage passes -1) and the pick's inflow.
            float p=reduced||!(i.Pulse>=0)?-1:Math.Min(1,i.Pulse),fall=p>=0?(1-p)*(1-p):0;
            s.Pulse=new Vector4(p>=0?WaveAt(p):-10,fall,Flash*fall,inflow);
            s.Style=new Vector4(i.Ward?1:0,i.Prism?1:0,1,GlowLevel);s.Fx=s.Gains;
            // The text-safe bands (the shader's Safe and its cap) on the heading's and the tray shade's rows.
            float head=i.HeadRow>0&&MathUtil.Finite(i.HeadRow)?Math.Min(.5f,i.HeadRow):DefaultHead,tray=i.TrayRow>0&&MathUtil.Finite(i.TrayRow)?MathUtil.Clamp(i.TrayRow,head+.1f,1):NativeDraftStage.DefaultTray;
            s.Safe=new Vector4(head,tray,HeadDetail,TrayDetail);
            // The camera, as the shader reads it, and the glow's centre on screen.
            float tanY=i.TanY>1e-4f&&MathUtil.Finite(i.TanY)?i.TanY:(float)Math.Tan(NativeDraftStage.Fov*Math.PI/360),aspect=i.Aspect>.1f&&MathUtil.Finite(i.Aspect)?i.Aspect:16f/9;
            s.Cap=new Vector4(HeadCap,TrayCap,Ceiling,Gap(aspect));s.Cam=new Vector4(i.CamPos,s.Time);s.Clock=new Vector4(s.Flow,0,0,0);s.Fwd=new Vector4(i.CamForward,tanY);s.Up=new Vector4(i.CamUp,aspect);s.Right=new Vector4(i.CamRight,s.Presence);
            var glow=Screen(i.Glow,i.CamPos,i.CamForward,i.CamUp,i.CamRight,tanY,aspect);s.Glow=new Vector4(glow.X,glow.Y,GlowWide,GlowTall);
            // The quad: half way to the far plane (inside the near plane's reach), from just above the top to under the opaque shade.
            float far=i.Far>0&&MathUtil.Finite(i.Far)?i.Far:200,near=i.Near>0&&MathUtil.Finite(i.Near)?i.Near:.06f;s.Distance=Math.Max(near*4,Math.Min(QuadMax,far*QuadReach));
            s.Top=QuadTop;s.Bottom=i.DeepRow>0&&MathUtil.Finite(i.DeepRow)?Math.Min(1.02f,Math.Max(tray,i.DeepRow)+QuadSlack):1.02f;
        }
        // The effect levels: packets, scan, streams and glyph motes. Reduced: no packets or scan, dimmer streams and motes held
        // still (the flow clock that moves them stops); the low tier: no streams or motes (the shader's low variant skips them).
        static Vector4 Targets(in DraftBackdropInput i)=>new Vector4(i.Reduced?0:1,i.Reduced?0:1,i.Low?0:i.Reduced?.45f:1,i.Low?0:i.Reduced?.5f:1);
        static bool Finite(Vector3 v)=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z);
        /// <summary>The quad's corners' local frame for the camera (x half-width, y centre, z half-height in camera units at the
        /// quad's distance), so the Runtime places a unit quad with one matrix.</summary>
        public static Vector3 QuadFrame(DraftBackdropState s,float tanY,float aspect)
        {
            float d=s.Distance,yTop=d*tanY*(1-2*s.Top),yBottom=d*tanY*(1-2*s.Bottom);
            return new Vector3(d*tanY*aspect*1.02f,(yTop+yBottom)*.5f,(yTop-yBottom)*.5f);
        }
    }
}
