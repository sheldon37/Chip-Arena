using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Glaive in the arena (docs/GLAIVE.md): the ring out of the hand drawn as the held model itself, flat and spinning where the record has
    // it (thrown, coming back, circling, on the flick's loop); its effects (Core NativeGlaiveFx); its sounds; its two cards. Its moves on the
    // model are keyed by hand (Core NativeGlaiveMotion, laid on in NativeDogMalletPresentation.cs). Presentation only: nothing here reaches the
    // authority, the network frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The ring's share in the hand this frame (none while it is out or burst).</summary>
        internal static float GlaiveShown(Actor a,float time)=>a?.SourceSpec!=NativeGlaive.Spec?1:NativeGlaive.InHand(NativeGlaiveView.Of(a),time);
        /// <summary>Every Glaive's ring out of the hand, as its own held model flat and spinning (NativeSourceModels, the pilot's accent).</summary>
        private void RenderGlaive(Snapshot s,float time)
        {
            if(s?.Actors==null||sourceModels==null)return;
            foreach(var a in s.Actors)
            {
                var k=NativeGlaiveView.Of(a);if(k==null)continue;var feet=visualPositions.TryGetValue(a.Id,out var vf)?vf:Vec(a.Position);var centre=new NVector(feet.x,feet.y+.865f,feet.z);
                if(!NativeGlaive.DiscAt(k,time,centre,out var at))continue;var pos=new Vector3(at.X,at.Y,at.Z);
                // Its travel this moment (for the tilt): from just before.
                NativeGlaive.DiscAt(k,time-.02f,centre,out var was);var travel=pos-new Vector3(was.X,was.Y,was.Z);travel.y=0;if(travel.sqrMagnitude<1e-6f)travel=Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;travel.Normalize();
                // Flat, tipped a little into its flight, spinning fast about its axle.
                var normal=Quaternion.AngleAxis(14,Vector3.Cross(Vector3.up,travel))*Vector3.up;var spin=Quaternion.AngleAxis(time*1440%360,normal)*travel;spin-=normal*Vector3.Dot(spin,normal);spin.Normalize();
                var turn=Quaternion.LookRotation(spin,Vector3.Cross(spin,normal));var frame=Matrix4x4.TRS(pos-turn*(Vector3.forward*NativeWeaponModels.GlaiveCentre),turn,Vector3.one);
                sourceModels.Queue("weapon-"+NativeGlaive.Spec,Matrix4x4.identity,_=>frame,null,0,null,accent:AccentTint(a.AccentColor),glow:HeldGlow);
            }
        }
        /// <summary>Crescent Return's ring while it is out of the hand (Core NativeGlaiveIntro.Ring): its held model flat, tipped into its
        /// flight and spinning, in the pilot's accent.</summary>
        private void GlaiveIntroRing(NativeGlaiveIntro intro,float t)
        {
            if(sourceModels==null||!NativeGlaiveIntro.Ring(t,out var at,out var dir))return;
            var pos=CapturedAnimation.Vec(CapturedRig.Mirror(at));var travel=CapturedAnimation.Vec(CapturedRig.Mirror(dir));travel.y=0;if(travel.sqrMagnitude<1e-6f)travel=Vector3.forward;travel.Normalize();
            var normal=Quaternion.AngleAxis(14,Vector3.Cross(Vector3.up,travel))*Vector3.up;var spin=Quaternion.AngleAxis(NativeGlaiveIntro.Spin(t)*Mathf.Rad2Deg%360,normal)*travel;spin-=normal*Vector3.Dot(spin,normal);spin.Normalize();
            var turn=Quaternion.LookRotation(spin,Vector3.Cross(spin,normal));glaiveIntroFrame=Matrix4x4.TRS(pos-turn*(Vector3.forward*NativeWeaponModels.GlaiveCentre),turn,Vector3.one);
            glaiveIntroAt??=_=>glaiveIntroFrame;sourceModels.Queue("weapon-"+NativeGlaive.Spec,Matrix4x4.identity,glaiveIntroAt,null,0,null,accent:introAccent,glow:HeldGlow);
        }
        private Matrix4x4 glaiveIntroFrame;private Func<int,Matrix4x4> glaiveIntroAt;
        private void GlaiveFx(float now)
        {
            if(kitScene==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            techniqueFx.DrawGlaives(techSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
    }
    public sealed partial class ConversionGame
    {
        /// <summary>The Glaive's cards: Q (the throw, or the burst while the ring is out), E (the halo).</summary>
        private void GlaiveCards(NativePilot p)
        {
            var k=p.Glaive;float t=arena.Time;bool outNow=k!=null&&(k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back||k.Disc==NativeGlaive.Orbit);
            for(int i=0;i<NativeGlaive.Slots;i++)
            {
                string id=i==0?NativeGlaive.Throw:NativeGlaive.Halo;float ready=k?.Ready!=null&&i<k.Ready.Length?k.Ready[i]:0,left=ready-t;float cd=NativeGlaive.Cooldown[i]*(float)(p.Stats?["abilityCooldown"]??1);
                bool live=i==0?k!=null&&(k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back):k!=null&&k.Disc==NativeGlaive.Orbit;
                string key=NativeGlaive.SlotKeys[i]+" · "+(i==0&&outNow?"BURST":NativeGlaive.AbilityName(id).ToUpperInvariant());
                cards.Add(new Card{Key=key,State=live?"OUT":i==0&&outNow?"READY":left>0&&left<30?left.ToString("0.0")+"s":left>0?"OUT":"READY",Icon=NativeGlaive.Icon(i==0&&outNow?NativeGlaive.Burst:id),Ready=i==0&&outNow||!live&&left<=0,Active=live,Dial=left>0&&left<30?1-Mathf.Clamp01(left/Mathf.Max(.1f,cd)):1});
            }
        }
    }
    public sealed partial class NativeAudio
    {
        // The Glaive's cuts, throws, ricochets, burst and catches, each once as the record first shows it; the halo's hum while it circles.
        private void GlaiveMoments(NativePilot p,Vector3 pos,float t,bool mine)
        {
            var k=p.Glaive;if(p.Seat.spec!=NativeGlaive.Spec||k==null)return;
            if(k.CastKind!=null&&t>=k.CastRelease&&t-k.CastRelease<.15f&&Once(p.Seat.id+"glrel"+k.CastKind+k.CastStep,k.CastRelease))
            {
                string id=k.CastKind==NativeGlaive.Throw?"ring-throw":k.CastKind==NativeGlaive.Swing&&k.CastStep==3?"ring-flick":k.CastKind==NativeGlaive.Swing||k.CastKind==NativeGlaive.Skim?"ring-cut":k.CastKind==NativeGlaive.Halo?"ring-throw":null;
                if(id!=null)Toon(id,pos,k.CastKind==NativeGlaive.Swing&&k.CastStep==2?1:.85f,mine,k.CastKind==NativeGlaive.Swing&&k.CastStep==2?.8f:1);
            }
            if(k.Hits!=null)foreach(var h in k.Hits)
            {
                if(h==null||t<h.At||t-h.At>=.15f||!Once(p.Seat.id+"glhit"+h.Id,h.At))continue;var at=new Vector3(h.Pos.x,h.Pos.y,h.Pos.z);
                switch(h.Kind)
                {
                    case NativeGlaive.HitBurst:Toon("ring-burst",at,1,mine);break;
                    case NativeGlaive.HitThrow:case NativeGlaive.HitRicochet:Toon("ring-ping",at,.85f,mine,1+.06f*k.Bounces);Toon("ring-slice",at,.7f,mine);break;
                    case NativeGlaive.HitCatch:Toon("ring-catch",at,.7f,mine);break;
                    // The chips' (Core NativeGlaiveChips): a shard's nick, a shot cut down, a drag; a bleed's drip is silent.
                    case NativeGlaive.HitShard:Toon("ring-slice",at,.55f,mine,1.25f+.1f*((h.Id%4)/3f));break;
                    case NativeGlaive.HitGuard:Toon("ring-ping",at,.8f,mine,1.3f);Toon("ring-chime",at,.35f,mine,1.2f);break;
                    case NativeGlaive.HitPull:Toon("ring-hum",at,.45f,mine,1.4f);break;
                    case NativeGlaive.HitBleed:break;
                    default:Toon("ring-slice",at,.75f,mine,.95f+.1f*((h.Id%5)/4f));break;
                }
            }
            if(k.Disc==NativeGlaive.Orbit&&t>=k.DiscAt){float step=Mathf.Floor((t-k.DiscAt)/.8f);if(step*.8f<NativeGlaive.HaloTime&&Once(p.Seat.id+"glhum",k.DiscAt+step*1000))Toon("ring-hum",pos,.55f,mine);}
        }
        private int glaivePickCue;private string glaivePickId;private float glaivePickLast=-1;
        /// <summary>A Glaive chip's pick: its animation's cues (Core NativeGlaiveChips.Picks) on the spotlight's clock, `age` s after the pick.</summary>
        public void GlaivePick(string chip,float age)
        {
            int i=NativeGlaiveChips.Index(chip);if(i<0)return;float s=age-NativeDraftPlant.DetailsAt-NativeGlaiveChipArt.Start;
            if(chip!=glaivePickId||age<glaivePickLast-.2f){glaivePickId=chip;glaivePickCue=0;}glaivePickLast=age;var list=NativeGlaiveChips.Picks[i];
            while(glaivePickCue<list.Length&&s>=list[glaivePickCue].At){var c=list[glaivePickCue++];if(s-c.At<.25f)Toon(c.Id,null,c.Gain,true,c.Rate);}
        }
    }
}
