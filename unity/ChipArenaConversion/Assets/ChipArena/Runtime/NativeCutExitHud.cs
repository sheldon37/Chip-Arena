using System;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // Cyber City: my own pilot's way-out hint (docs/arenas/CYBERCITY-SIMULATION-CONTRACT.md §11). Only while the local pilot is in
    // the Cut (Core NativeCyberCityExits.InCut: under the street, inside the trench and its recesses; not on the stairs beyond the
    // door, not on the bridges) during the Fight, a small cyber tag under the SET / ROUND tag: an arrow turned toward the nearest
    // reachable exit's door relative to the camera, "EXIT" and the door's distance ("12 m"). Its tone follows the maglev's clock
    // from the replicated match frame (NativeCyberCityExits.Tone: calm white, amber warning, red danger), the same as the floor
    // chevrons, so a guest sees what the host sees. It fades in and out over .15 s; Reduced effects keeps the danger from pulsing.
    // The exits are read once per map from its data (never hard-coded). Nothing here is sent or simulated; no allocation per frame.
    public sealed partial class ConversionGame
    {
        private NativeCyberCityExits cutExits;private string cutExitsMap;private MapHazard cutMaglev;private float cutHintAlpha;private NativeCutHint cutHint;private int cutHintTone;
        private static string[] cutMetres;
        private void CutExitHint(NativePilot p)
        {
            if(arena==null||p?.Body==null||Event.current.type!=EventType.Repaint)return;
            if(!NativeCyberCityLooks.Active(arena.MapId)){cutHintAlpha=0;return;}
            if(cutExits==null||!string.Equals(cutExitsMap,arena.MapId,StringComparison.Ordinal))
            {var map=arena.Map;cutExitsMap=arena.MapId;cutExits=NativeCyberCityExits.Build(map);cutMaglev=cutExits.Maglev>=0?map.hazards[cutExits.Maglev]:null;cutHintAlpha=0;}
            Vector3 at=p.Body.position,forward=cameraView!=null?cameraView.transform.forward:p.Body.transform.forward;
            var hint=cutExits.HintFor(at.x,at.y-NativeLocomotionRules.HalfHeight,at.z,forward.x,forward.z);
            bool show=hint.Shown&&p.Hp>0&&match.Phase==NativePhase.Fight;
            if(show)
            {
                cutHint=hint;
                var state=cutMaglev!=null?NativeCyberCityHazards.At(cutMaglev,match.Phase,NativeStructureFx.RoundIndex(match.Set,match.Round),match.FightAt,arena.Time):NativeCyberCityHazards.Idle();
                cutHintTone=NativeCyberCityExits.Tone(state);
            }
            cutHintAlpha=Mathf.MoveTowards(cutHintAlpha,show?1:0,Mathf.Min(.1f,Time.unscaledDeltaTime)/.15f);if(cutHintAlpha<=.01f)return;
            float a=cutHintAlpha;bool reduced=prefs.reducedEffects;int tone=cutHintTone;
            Color ink=tone==1?NativeCyberTheme.Warn():tone==2?NativeCyberTheme.Danger():NativeCyberTheme.Ink();
            float pulse=tone==2&&!reduced?.7f+.3f*Mathf.Cos(Time.unscaledTime*Mathf.PI*4):1;
            string dist=CutMetres(cutHint.Distance);float ew=U.Measure("EXIT",12,true,true,.16f).x,dw=U.Measure(dist,12,false,true).x,w=12+16+8+ew+12+dw+12,h=24;
            var r=new Rect(U.W/2-w/2,52,w,h);
            if(tone>0)U.Glow(r,U.A(ink,.2f*a*pulse),10);
            NativeHudChrome.Tag(r,tone>0?ink:NativeCyberTheme.CyanSoft(),a,false);
            var c=new Vector2(r.x+12+8,r.center.y);CutArrow(c,cutHint.Bearing,U.A(ink,a*pulse));
            U.Text(new Rect(c.x+8+8,r.y,ew+4,h),"EXIT",12,U.A(ink,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f,glow:U.A(ink,.3f*a*pulse));
            U.Text(new Rect(r.xMax-12-dw-4,r.y,dw+4,h),dist,12,NativeCyberTheme.Ink(a),TextAnchor.MiddleRight,mono:true);
        }
        // An arrow 12 units tall pointing up, turned `degrees` clockwise about its centre: a stem and a two-stroke head.
        private static void CutArrow(Vector2 c,float degrees,Color col)
        {
            U.Push(c,Vector2.zero,1,degrees);
            U.Fill(new Rect(c.x-1.1f,c.y-5.2f,2.2f,10.8f),col);
            for(int s=-1;s<=1;s+=2){var m=new Vector2(c.x+s*2.6f,c.y-3.6f);U.Push(m,Vector2.zero,1,s*45);U.Fill(new Rect(m.x-4,m.y-1.1f,8,2.2f),col);U.Pop();}
            U.Pop();
        }
        // "12 m" for a distance, built once per whole metre (0..99).
        private static string CutMetres(float metres)
        {
            cutMetres??=new string[100];int m=Mathf.Clamp(Mathf.RoundToInt(metres),0,99);
            return cutMetres[m]??=m.ToString(System.Globalization.CultureInfo.InvariantCulture)+" m";
        }
    }
}
