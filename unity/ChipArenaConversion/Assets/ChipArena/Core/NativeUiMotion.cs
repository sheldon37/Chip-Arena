using System;

namespace ChipArena.UnityConversion
{
    // CSS-equivalent animation timing for the native IMGUI overlays (the source's DOM/CSS keyframes and transitions).
    // Engine-free so the curves and layout scale are checked in the managed tests. Every overlay samples these from an
    // authoritative clock (intro age, phase remaining, replay time), as the source's negative CSS delays do.
    public static class NativeUiMotion
    {
        public static float Clamp01(float v)=>v<0?0:v>1?1:v;
        // CSS cubic-bezier(x1,y1,x2,y2): solve x(s)=t by Newton steps with bisection fallback, return y(s).
        public static float Bezier(float x1,float y1,float x2,float y2,float t)
        {
            t=Clamp01(t);if(t<=0||t>=1)return t;
            float X(float s)=>((1-3*x2+3*x1)*s+(3*x2-6*x1))*s*s+3*x1*s;float Y(float s)=>((1-3*y2+3*y1)*s+(3*y2-6*y1))*s*s+3*y1*s;float D(float s)=>3*(1-3*x2+3*x1)*s*s+2*(3*x2-6*x1)*s+3*x1;
            float u=t;for(int i=0;i<8;i++){float e=X(u)-t;if(Math.Abs(e)<1e-6f)return Y(u);float d=D(u);if(Math.Abs(d)<1e-6f)break;u-=e/d;}
            float lo=0,hi=1;u=t;for(int i=0;i<30;i++){float x=X(u);if(Math.Abs(x-t)<1e-6f)break;if(x<t)lo=u;else hi=u;u=(lo+hi)/2;}
            return Y(u);
        }
        public static float Ease(float t)=>Bezier(.25f,.1f,.25f,1,t);        // CSS `ease`
        public static float EaseOut(float t)=>Bezier(0,0,.58f,1,t);          // CSS `ease-out`
        public static float EaseInOut(float t)=>Bezier(.42f,0,.58f,1,t);     // CSS `ease-in-out`
        public static float OutCubic(float t){t=Clamp01(t);float u=1-t;return 1-u*u*u;}
        // A CSS keyframe track: stops at 0..1 offsets, linear-in-segment values eased by `ease` per segment
        // (CSS applies the animation's timing function between each pair of keyframes).
        public static float Keys(float t,float[] at,float[] values,Func<float,float> ease=null)
        {
            if(t<=at[0])return values[0];int n=at.Length-1;if(t>=at[n])return values[n];int i=0;while(at[i+1]<t)i++;
            float u=(t-at[i])/(at[i+1]-at[i]);if(ease!=null)u=ease(u);return values[i]+(values[i+1]-values[i])*u;
        }
        // Progress of an animation of `duration` that started at `start` on clock `now` (CSS fill-mode both).
        public static float Progress(float now,float start,float duration)=>duration<=0?(now>=start?1:0):Clamp01((now-start)/duration);
        // One UI scale for every overlay: the virtual canvas keeps the conversion's 900-unit height at 16:9 and wider (21:9 is
        // 2150x900), and windows narrower than 16:9 shrink instead of overflowing. The HUD anchors to the true window edges,
        // as the source's does; SafeX/SafeWidth give a centred 16:9 band for elements that read as one group.
        public struct Layout{public float Scale,Width,Height,SafeX,SafeWidth;}
        public static Layout Measure(float screenWidth,float screenHeight,float uiScale=1)
        {
            if(!(uiScale>0))uiScale=1;float scale=Math.Max(.6f,Math.Min(3,Math.Min(screenWidth/1600f,screenHeight/900f)*uiScale)),w=screenWidth/scale,h=screenHeight/scale,safe=Math.Min(w,h*16f/9f);
            return new Layout{Scale=scale,Width=w,Height=h,SafeX=(w-safe)/2,SafeWidth=safe};
        }
        // CSS clamp(min, preferred, max) in reference px.
        public static float ClampPx(float min,float preferred,float max)=>Math.Max(min,Math.Min(max,preferred));
    }
}
