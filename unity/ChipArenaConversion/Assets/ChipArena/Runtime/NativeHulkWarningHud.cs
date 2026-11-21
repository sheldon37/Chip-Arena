using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The charge warning for the hunted pilot (owner 2026-10-14: "The charge is avoidable if they move out of the way in time ...
    // They literally can charge from across the map to the player breaking the walls"). Presentation only, from
    // ArenaPresentation.HulkWarning (Core/NativeHulkCharge: only the pilot the charge hunts gets one, even through walls):
    //  - on screen: corner brackets round the Hulk's body, "CHARGE" over them, in the danger tone while the pilot stands in its
    //    lane and the warn tone once out of it; a corner over the crosshair's clear circle is left out (with none left, two short
    //    arcs just outside it), so nothing sits on the aim in first person;
    //  - off screen or behind: chevron arrows on an ellipse near the screen's edge pointing toward it, one to three deep and
    //    larger, brighter and faster as the urgency rises (the distance closing, the closing speed), and a soft glow along that edge
    //    when it is near;
    //  - Reduced effects: no pulse (steady), the glow kept faint. Nothing is drawn in menus, replays, spectating or when dead.
    // No strings built per frame (constant labels only).
    public sealed partial class ConversionGame
    {
        private const string HulkChargeLabel="CHARGE";
        private void HulkWarningHud(NativePilot p)
        {
            var w=presentation.HulkWarning;
            if(!w.Active||w.Frame<Time.frameCount-2||cameraView==null||p==null||p.Hp<=0||w.Target!=p.Seat.id||ReplayActive||Event.current.type!=EventType.Repaint)return;
            float W=U.W,H=U.H,aspect=W/Mathf.Max(1,H);
            var foot=new Vector3(w.Hulk.X,w.Hulk.Y,w.Hulk.Z);var chest=new Vector3(w.Chest.X,w.Chest.Y,w.Chest.Z);
            var vp=cameraView.WorldToViewportPoint(chest);var vb=cameraView.WorldToViewportPoint(foot);var vt=cameraView.WorldToViewportPoint(foot+Vector3.up*NativeHordeKinds.WorldHeight(NativeHordeKinds.Hulk));
            float half=vp.z>0?Mathf.Abs(vt.y-vb.y)*.5f:0;bool reduced=prefs.reducedEffects;
            var m=NativeHulkCharge.Layout(vp.x,vp.y,vp.z,half,aspect,w.Urgency,Time.unscaledTime,reduced);
            Color tone=w.InLane?NativeCyberTheme.Danger():NativeCyberTheme.Warn();float a=Mathf.Clamp01(m.Alpha*w.Fade);if(a<=.01f)return;
            if(m.OnScreen)
            {
                float cx=m.X*W,cy=(1-m.Y)*H,sx=m.Size*H,sy=m.Size*NativeHulkCharge.BracketTall*H,leg=m.Size*NativeHulkCharge.Leg*H,t=Mathf.Lerp(2,3.5f,w.Urgency);
                if(m.Ring)
                {
                    // Two short arcs just outside the crosshair's clear circle, either side.
                    float r=NativeHulkCharge.AimClear*1.12f*H;var c=new Vector2(W*.5f,H*.5f);
                    for(int side=-1;side<=1;side+=2)for(int k=-2;k<=2;k++){float ang=(side<0?180:0)+k*9;var at=c+new Vector2(Mathf.Cos(ang*Mathf.Deg2Rad),-Mathf.Sin(ang*Mathf.Deg2Rad))*r;U.Push(at,Vector2.zero,1,-ang+90);U.Fill(new Rect(at.x-t*.5f,at.y-7,t,14),U.A(tone,a));U.Pop();}
                    return;
                }
                for(int i=0;i<4;i++)
                {
                    if((m.Corners&(1<<i))==0)continue;float dx=(i&1)==0?-1:1,dy=(i&2)==0?1:-1;float x=cx+dx*sx,y=cy+dy*sy;
                    // Each corner's two legs, drawn inward.
                    U.Fill(new Rect(dx<0?x:x-leg,y-t*.5f,leg,t),U.A(tone,a));U.Fill(new Rect(x-t*.5f,dy<0?y:y-leg,t,leg),U.A(tone,a));
                }
                if((m.Corners&12)==12)U.Text(new Rect(cx-70,cy-sy-22,140,16),HulkChargeLabel,11,U.A(tone,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f,shadow:new Color(0,0,0,.8f*a));
                return;
            }
            // Off screen: the chevrons toward it.
            var tip=new Vector2(m.X*W,(1-m.Y)*H);var dir=new Vector2(Mathf.Cos(m.Angle),-Mathf.Sin(m.Angle));var perp=new Vector2(-dir.y,dir.x);float L=m.Size*H,th=Mathf.Lerp(3,5,w.Urgency);
            for(int k=0;k<m.Chevrons;k++)
            {
                var p0=tip-dir*(k*L*.9f);float ka=a*(1-.28f*k);
                for(int s=-1;s<=1;s+=2)
                {
                    var end=p0-dir*L+perp*(s*L*.72f);var mid=(p0+end)*.5f;var arm=p0-end;float len=arm.magnitude,deg=Mathf.Atan2(arm.y,arm.x)*Mathf.Rad2Deg;
                    U.Push(mid,Vector2.zero,1,deg);U.Fill(new Rect(mid.x-len*.5f,mid.y-th*.5f,len,th),U.A(tone,ka));U.Pop();
                }
            }
            // The label beside the arrow (above it in the screen's lower half, below it in the upper), clear of the chevrons.
            var lab=tip-dir*L*(m.Chevrons*.9f+.5f);lab.y+=tip.y>H*.5f?-L-14:L+14;
            U.Text(new Rect(lab.x-60,lab.y-8,120,16),HulkChargeLabel,10,U.A(tone,a*.85f),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f,shadow:new Color(0,0,0,.8f*a));
            // Near, a soft glow along the screen's edge it comes from.
            float glow=Mathf.Clamp01((w.Urgency-.45f)/.55f)*(reduced?.5f:m.Pulse)*.28f;
            if(glow>.01f)
            {
                float ex=Mathf.Abs(dir.x)*aspect,ey=Mathf.Abs(dir.y),band=70;
                if(ex>=ey){if(dir.x<0)U.HGradient(new Rect(0,0,band,H),U.A(tone,glow),U.A(tone,0));else U.HGradient(new Rect(W-band,0,band,H),U.A(tone,0),U.A(tone,glow));}
                else U.Fade(dir.y<0?new Rect(0,0,W,band):new Rect(0,H-band,W,band),U.A(tone,glow),dir.y<0);
            }
        }
    }
}
