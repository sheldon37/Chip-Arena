using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // The "SELECTING CHIP" tag over every selecting pilot (Core NativeChipSelectTag: the typing, the cursor, the dots, the
        // glow, the fades; the state is the presentation's, NativeChipSelectPresentation.cs, from the replicated open pick). It
        // sits over the overhead name the health bars draw (NativeCombatHud.cs: the bar at the body + 1.3 m, the name 27 px over
        // it), with the bars' occlusion ray (smoothed, NativeChipSelectTag.Visible) and range, faded with distance, kept on screen,
        // never for my own pilot in first person. A small chamfered navy plate with a cyan edge and tick, a chip glyph, the mono
        // text and three dots; danger red, blinking, while the pilot is hit. Repaint only; no allocation once a pilot has an entry.
        private readonly Dictionary<int,float> chipTagSeen=new Dictionary<int,float>();private float chipTagClock=float.NaN;private float chipTagWidth;
        private void ChipSelectTags(float scale,float height)
        {
            if(Event.current.type!=EventType.Repaint||arena?.Pve==null&&!(arena?.Sandbox==true&&presentation.ChipSelectPreview))return;
            var physics=arena.Root.scene.GetPhysicsScene();var eye=cameraView.transform.position;float now=Time.unscaledTime,dt=float.IsNaN(chipTagClock)?0:Mathf.Min(.1f,now-chipTagClock);chipTagClock=now;
            bool reduced=prefs.reducedEffects;if(chipTagWidth<=0)chipTagWidth=U.Measure(NativeChipSelectTag.Text,NativeChipSelectTag.Size,true,true,NativeChipSelectTag.Tracking).x;
            foreach(var p in arena.Pilots)
            {
                if(!p.Seat.connected||FirstPersonView&&p==arena.ViewPilot||!presentation.ChipSelectTagState(p.Seat.id,out float weight,out float age,out bool attacked))continue;
                var at=p.Body.transform.position+Vector3.up*1.3f*(p.Seat.spec=="siren"?p.Pose.RigScale:1);var screen=cameraView.WorldToScreenPoint(at)/scale;float metres=Vector3.Distance(eye,at);
                bool inFront=screen.z>0&&metres<55;Vector3 delta=at-eye;bool seen=inFront&&!physics.Raycast(eye,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
                chipTagSeen.TryGetValue(p.Seat.id,out float visible);visible=NativeChipSelectTag.Visible(visible,seen,dt);chipTagSeen[p.Seat.id]=visible;
                float alpha=NativeChipSelectTag.Alpha(weight,age,metres,visible);if(!inFront||alpha<=.01f)continue;
                ChipSelectTag(new Vector2(screen.x,height-screen.y),age,alpha,attacked,reduced);
            }
        }
        // One tag centred over `bar` (the overhead bar's top centre, canvas units): above the name row.
        private void ChipSelectTag(Vector2 bar,float age,float alpha,bool attacked,bool reduced)
        {
            float size=NativeChipSelectTag.Size,glyph=9,gap=6,dots=16,h=18,w=8+glyph+gap+chipTagWidth+gap+dots+8;
            var r=new Rect(Mathf.Round(Mathf.Clamp(bar.x-w/2,8,U.W-8-w)),Mathf.Round(bar.y-27-4-h),w,h);
            bool red=NativeChipSelectHolo.Red(attacked,Time.unscaledTime,reduced);Color tone=red?C.Danger():C.Cyan(),ink=red?U.Hex(0xffd6dc):C.CyanSoft();
            // The soft glow behind it, breathing; the plate; the edge and the left tick.
            U.Glow(r,U.A(tone,NativeChipSelectTag.Glow(Time.unscaledTime,reduced)*alpha),10);
            NativeHudChrome.Cut(r,C.Navy(.82f*alpha),NativeHudCyber.TagCut);NativeHudChrome.Outline(r,U.A(tone,.6f*alpha),NativeHudCyber.TagCut);
            U.Fill(new Rect(r.x+2,r.y+r.height*.28f,2,r.height*.44f),U.A(tone,alpha));
            // The chip glyph: a die with two pins on each side.
            float gx=r.x+8,gy=r.center.y-glyph/2;var die=new Rect(gx+2,gy+2,glyph-4,glyph-4);NativeHudChrome.Outline(die,U.A(ink,alpha),0,NativeHudChrome.Corners.None);U.Fill(new Rect(die.x+2,die.y+2,die.width-4,die.height-4),U.A(tone,.8f*alpha));
            for(int k=0;k<2;k++){float y=gy+3+k*3;U.Fill(new Rect(gx,y,2,1),U.A(ink,alpha));U.Fill(new Rect(gx+glyph-2,y,2,1),U.A(ink,alpha));}
            // The text, typed in (left-aligned in the full text's box so the letters never shift), and the cursor while it types.
            float tx=gx+glyph+gap;int shown=NativeChipSelectTag.Shown(age,reduced);var text=new Rect(tx,r.y,chipTagWidth+4,h);
            U.Text(text,NativeChipSelectTag.Typed[shown],size,U.A(ink,alpha),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:NativeChipSelectTag.Tracking,shadow:new Color(0,0,0,.7f*alpha));
            if(NativeChipSelectTag.Cursor(age,reduced)){float cx=tx+(shown>0?U.Measure(NativeChipSelectTag.Typed[shown],size,true,true,NativeChipSelectTag.Tracking).x+2:0);U.Fill(new Rect(cx,r.y+4,5,h-8),U.A(tone,.9f*alpha));}
            // The dots, lighting in turn once the text is in.
            float dx=tx+chipTagWidth+gap;for(int k=0;k<3;k++){float b=NativeChipSelectTag.Dot(k,age,reduced);if(b>0)U.Fill(new Rect(dx+k*5,r.center.y+2,3,3),U.A(k==0?ink:tone,b*alpha));}
        }
    }
}
