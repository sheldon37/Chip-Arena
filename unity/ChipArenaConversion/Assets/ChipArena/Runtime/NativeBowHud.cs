using System;
using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Bow's HUD (Core/NativeBowHud on NativeBowWire): the draw meter round the crosshair, drawn in physical pixels on the
    // aim ray's centre like the crosshair: a segmented bar under it filling with the draw (the quick-shot mark where a tap
    // ends), two brackets closing in on the crosshair as the string comes back and locking with a magenta flare at full draw,
    // shaking and turning amber then red as the arm tires; the released fill fades out after a shot. Reduced effects keeps it
    // still (no flare pulse or shake). The Q (Volley) and E (Rain of Arrows) cards are in NativeFightHud.Cooldowns.
    public sealed partial class ConversionGame
    {
        private void BowMeter(NativePilot p)
        {
            if(p?.Pose==null||!NativeBowWire.Is(p.Seat.spec)||Event.current.type!=EventType.Repaint||presentation.ScopeActive)return;
            var r=NativeBowWire.Of(p.Pose);var m=NativeBowHud.MeterOf(r,arena.Time);if(!m.Shown||m.Alpha<=.01f)return;bool reduced=prefs.reducedEffects;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;float k=Mathf.Max(1,Mathf.Round(Screen.height/1080f)),cx=Mathf.Round(Screen.width/2f),cy=Mathf.Round(Screen.height/2f),a=m.Alpha;
            void Px(float x,float y,float w,float h,Color c){var o=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(Mathf.Round(x),Mathf.Round(y),Mathf.Max(1,Mathf.Round(w)),Mathf.Max(1,Mathf.Round(h))),Texture2D.whiteTexture);GUI.color=o;}
            Color tone=m.Fatigue?Color.Lerp(NativeCyberTheme.Warn(),NativeCyberTheme.Danger(),m.Tremor):m.Full?NativeCyberTheme.Magenta():NativeCyberTheme.Cyan();tone.a*=a;
            var shadow=NativeCyberTheme.Void(.75f*a);var edge=NativeCyberTheme.Edge(.9f*a);
            float shake=reduced||!m.Fatigue?0:Mathf.Round(Mathf.Sin(Time.unscaledTime*47)*1.5f*k*m.Tremor);
            // The bar: 12 segments under the crosshair, the quick-shot mark at a tap's end.
            float w=96*k,h=Mathf.Max(2,Mathf.Round(3*k)),x0=cx-w/2+shake,y0=cy+28*k;int segs=12;float gap=2*k,sw=(w-gap*(segs-1))/segs;
            Px(x0-k,y0-k,w+2*k,h+2*k,shadow);
            for(int i=0;i<segs;i++){float sx=x0+i*(sw+gap),fill=Mathf.Clamp01(m.Fill*segs-i);Px(sx,y0,sw,h,edge);if(fill>0)Px(sx,y0,sw*fill,h,tone);}
            if(!m.Release){float qx=x0+w*m.QuickMark;Px(qx-k*.5f,y0-3*k,Mathf.Max(1,k),h+6*k,m.Quick?NativeCyberTheme.Ink(a):NativeCyberTheme.Muted(.7f*a));}
            // The brackets: from 34 px out to the crosshair's arms as the draw fills, locked (and flaring) at full draw.
            float gapX=Mathf.Lerp(34,15,NativeBow.PullOf(m.Fill))*k,bh=10*k,t=Mathf.Max(1,Mathf.Round(1.5f*k));
            for(int s=-1;s<=1;s+=2)
            {
                float bx=cx+s*(gapX+(s<0?0:0))+shake;float outer=s<0?bx-4*k:bx+4*k-t;
                Px(outer-k*.5f,cy-bh/2-k*.5f,t+k,bh+k,shadow);Px(outer,cy-bh/2,t,bh,tone);
                Px(s<0?outer:bx,cy-bh/2,4*k,t,tone);Px(s<0?outer:bx,cy+bh/2-t,4*k,t,tone);
            }
            if(m.Full&&!m.Release&&m.Pulse>0&&!reduced){float g=m.Pulse;var flare=NativeCyberTheme.Magenta(.55f*g*a);float r0=(16+10*(1-g))*k;Px(cx-r0,cy-k*.5f,2*r0,k,flare);Px(cx-k*.5f,cy-r0,k,2*r0,flare);}
            if(m.Full&&!m.Release){var lockTone=NativeCyberTheme.Magenta(a);U.Text(new Rect(cx/U.S-60,(y0+h+4*k)/U.S,120,12),m.Fatigue?"STEADY · TIRING":"FULL DRAW",9,lockTone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f);}
            GUI.matrix=old;
        }
    }
    // The bow's sounds (Core/NativeBowSounds, existing bank keys): the draw's creak once a draw has lasted past a tap, the
    // release's thwip by the replicated release time (heavier for a fuller draw; Volley and Rain loose theirs at their casts),
    // and the arrow's thunk where it struck the world: a prop's Impact event, or an arrow gone from the shots with no hit near
    // where it vanished (the map's static walls raise no event). Hits on pilots keep the existing body-hit cue.
    public sealed partial class NativeAudio
    {
        private struct BowFlight{public Vector3 At;public int Owner,Frame;}
        private readonly Dictionary<int,BowFlight> bowFlights=new Dictionary<int,BowFlight>();private readonly List<int> bowGone=new List<int>();private int bowFrame,bowGeneration=-1;
        private readonly (int owner,float at,Vector3 point)[] bowStrikes=new (int,float,Vector3)[16];private int bowStrikeNext;
        private void BowCues(NativePilot p,string id,float t,Vector3 pos,bool mine)
        {
            if(p?.Pose==null||!NativeBowWire.Is(p.Seat.spec))return;var r=NativeBowWire.Of(p.Pose);
            if(r.Drawing(t)&&t-r.DrawAt>=NativeBowSounds.CreakAfter&&t-r.DrawAt<NativeBowSounds.CreakAfter+.2f&&Once(id+"bow-creak",r.DrawAt))Cue(NativeBowSounds.Creak.Key,NativeBowSounds.Creak.Clip,pos,NativeBowSounds.Creak.Gain,NativeBowSounds.CreakRate(r.DrawTime),mine:mine);
            if(r.ReleaseAt>0&&t>=r.ReleaseAt&&t-r.ReleaseAt<.15f&&Once(id+"bow-thwip",r.ReleaseAt))Cue(NativeBowSounds.Thwip.Key,NativeBowSounds.Thwip.Clip,pos,NativeBowSounds.Thwip.Gain,NativeBowSounds.ThwipRate(r.ReleaseDraw),mine:mine);
            float cast=Math.Max(r.VolleyRelease,r.VolleyAt);if(r.VolleyAt>0&&t>=cast&&t-cast<.15f&&Once(id+"bow-volley",cast))Cue(NativeBowSounds.Thwip.Key,NativeBowSounds.Thwip.Clip,pos,NativeBowSounds.Thwip.Gain*1.25f,.95f,mine:mine);
            float sky=r.RainRelease>r.RainAt?r.RainRelease:r.RainAt+NativeBow.RainPre;if(r.RainAt>0&&t>=sky&&t-sky<.15f&&Once(id+"bow-sky",sky))Cue(NativeBowSounds.Thwip.Key,NativeBowSounds.Thwip.Clip,pos,NativeBowSounds.Thwip.Gain*1.2f,.85f,mine:mine);
        }
        private void BowEventCue(NativeArena arena,in ArenaEvent e,NativePilot source,Vector3 pos)
        {
            if(source==null||!NativeBowWire.Is(source.Seat.spec))return;
            if(e.Kind==EventKind.Impact){Cue(NativeBowSounds.Thunk.Key,NativeBowSounds.Thunk.Clip,pos,NativeBowSounds.Thunk.Gain);return;}
            if(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot){bowStrikes[bowStrikeNext]=(e.Source,e.Time,pos);bowStrikeNext=(bowStrikeNext+1)%bowStrikes.Length;}
        }
        /// <summary>Once a frame: the arrows in flight, and a thunk where one vanished with no hit near it.</summary>
        private void BowFlightCues(NativeArena arena)
        {
            if(arena==null)return;if(arena.Generation!=bowGeneration){bowGeneration=arena.Generation;bowFlights.Clear();}bowFrame++;float t=arena.Replica?arena.ReplicaRenderTime:arena.Time;
            foreach(var b in arena.Bullets)if(NativeBowWire.Is(b.Spec))bowFlights[b.Id]=new BowFlight{At=b.Position,Owner=b.Original!=0?b.Original:b.Owner,Frame=bowFrame};
            bowGone.Clear();foreach(var pair in bowFlights)if(pair.Value.Frame!=bowFrame)bowGone.Add(pair.Key);
            foreach(int id in bowGone)
            {
                var f=bowFlights[id];bowFlights.Remove(id);bool hit=false;
                for(int i=0;i<bowStrikes.Length;i++){var s=bowStrikes[i];if(s.owner==f.Owner&&Mathf.Abs(s.at-t)<.3f&&(s.point-f.At).sqrMagnitude<4){hit=true;break;}}
                if(!hit&&Physics.CheckSphere(f.At,.9f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))Cue(NativeBowSounds.Thunk.Key,NativeBowSounds.Thunk.Clip,f.At,NativeBowSounds.Thunk.Gain*.85f);
            }
        }
    }
}
