using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The blade classes in the arena (docs/KATANA.md, docs/FANGS.md): the Katana's saya at the left hip (the blade resting in it while sheathed:
    // the iai's draw and its click), the Fangs' thrown knives as their own blades in flight, a veiled Fangs pilot hidden from its enemies
    // (its smoke at the feet stays: Core NativeBladeFx), their effects, sounds and cards. The moves on the model are the pack's clips (Core
    // NativeBladeMotion). Presentation only: nothing here reaches the authority, the network frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The saya's model id (built with the weapon models, NativeWeaponSkinModels.cs).</summary>
        public const string SayaModel="katana-saya";
        /// <summary>Where the saya hangs in the pelvis's drawn frame (+x the body's left, +y up, +z ahead), and which way it points (from its
        /// mouth at the front of the left hip, back and down).</summary>
        private static readonly Vector3 SayaAt=new Vector3(.17f,-.03f,.1f),SayaAxis=new Vector3(.08f,-.42f,-.9f),SayaUp=new Vector3(.95f,.25f,0);
        /// <summary>Whether a Katana's blade is in its saya now: its record's iai, or its entrance's script.</summary>
        internal bool KatanaSheathed(Actor a,float time)=>a!=null&&a.Id==bladeIntroActor&&bladeIntro!=null?bladeIntro.Sheathed(bladeIntroClock):NativeKatana.Sheathed(NativeBladeView.Katana(a),time);
        private NativeBladeIntro bladeIntro;private int bladeIntroActor=-1;private float bladeIntroClock;
        private int bladeViewerTeam=-1;
        /// <summary>Whether `a` is veiled and the viewer is its enemy (then its body is not drawn).</summary>
        private bool BladeHidden(Actor a,float time)=>a!=null&&a.Id!=LocalId&&NativeBladeView.Veiled(a,time)&&(bladeViewerTeam<=0||a.Team!=bladeViewerTeam);
        /// <summary>SourceCharacter: a veiled Fangs pilot's body hidden from its enemies.</summary>
        private Func<int,float> BladeReveal(Actor a,float time,Func<int,float> reveal){if(!BladeHidden(a,time))return reveal;return bladeHiddenReveal??=_=>0;}
        private Func<int,float> bladeHiddenReveal;
        /// <summary>SourceCharacter: the saya at the hip, and the blade in it while sheathed (the hand empty).</summary>
        private void KatanaSaya(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera,Func<int,Color> tint,float flash)
        {
            if(first||a?.SourceSpec!=NativeKatana.Spec||sourceModels==null||rig==null)return;
            var pelvis=visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.PosedFrame==Time.frameCount?VisualJointFrame(rig,CapturedRig.Pelvis):rig.Bones[CapturedRig.Pelvis].localToWorldMatrix;
            var frame=pelvis*Matrix4x4.TRS(SayaAt,Quaternion.LookRotation(SayaAxis.normalized,SayaUp),Vector3.one);
            sourceModels.Queue(SayaModel,frame,null,null,layer,camera,accent:AccentTint(a.AccentColor),hitFlash:flash,partTint:tint);
            // Sheathed: the katana in the saya on the saya's own axes, its habaki seated in the mouth (NativeWeaponModels.KatanaSayaSeat) and
            // the hilt out of it.
            if(KatanaSheathed(a,time)){var sheath=frame*Matrix4x4.Translate(new Vector3(0,0,-NativeWeaponModels.KatanaSayaSeat));sourceModels.Queue("weapon-"+NativeKatana.Spec,sheath,_=>sheath,null,layer,camera,accent:AccentTint(a.AccentColor),hitFlash:flash,partTint:tint,glow:HeldGlow);}
        }
        /// <summary>The Fangs' thrown knives in flight (reconstructed from the throw, as the effects draw their streaks), each its own blade.</summary>
        private void RenderBlades(Snapshot s,float time)
        {
            if(s?.Actors==null)return;
            // The viewer's team for the veil (the local pilot's).
            bladeViewerTeam=-1;foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){bladeViewerTeam=a.Team;break;}
            if(sourceModels==null)return;
            foreach(var a in s.Actors)
            {
                var k=NativeBladeView.Fangs(a);if(k?.Hits==null)continue;bool hydra=NativeFangsChips.Holds(k,NativeFangsChips.Hydra);
                foreach(var h in k.Hits)
                {
                    if(h==null||h.Kind!=NativeFangs.HitThrow)continue;float age=time-h.At,travel=NativeFangs.FanSpeed*age;if(age<0||travel>NativeFangs.FanRange)continue;
                    int n=Math.Max(1,h.Amount);var from=NativeBlades.N(h.Pos);var dir=NativeBlades.N(h.Dir);float spread=NativeFangs.FanSpread*(hydra?1.6f:1);
                    for(int i=0;i<n&&knifeFrames.Count<64;i++)
                    {
                        var d=NativeFangs.KnifeDir(dir,i,n,spread);var p=from+d*travel;var fwd=new Vector3(d.X,0,d.Z);
                        // Tip first, spinning about its own length a little as it flies.
                        var m=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),Quaternion.LookRotation(fwd,Vector3.up)*Quaternion.AngleAxis(time*900%360,Vector3.forward),Vector3.one*.8f);
                        knifeFrames.Add(m);
                        sourceModels.Queue("loose-"+NativeFangs.Spec,m,null,null,0,null,accent:AccentTint(a.AccentColor),glow:HeldGlow);
                    }
                }
            }
            knifeFrames.Clear();
        }
        private readonly List<Matrix4x4> knifeFrames=new List<Matrix4x4>(64);
        private void BladeFx(float now)
        {
            if(kitScene==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            techniqueFx.DrawBlades(techSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
    }
    public sealed partial class ConversionGame
    {
        /// <summary>The blade classes' cards: the Katana's Q (the iai) and E (the storm); the Fangs' Q, E and 1-3.</summary>
        private void BladeCards(NativePilot p)
        {
            var k=p.Blade;float t=arena.Time;bool katana=p.Seat.spec==NativeKatana.Spec;int slots=NativeBlades.Slots(p.Seat.spec);
            for(int i=0;i<slots;i++)
            {
                string id=katana?(i==0?NativeKatana.Iai:NativeKatana.Storm):NativeFangs.SlotMoves[i];string key=(katana?NativeKatana.SlotKeys[i]:NativeFangs.SlotKeys[i])+" · "+(katana?NativeKatana.AbilityName(id):NativeFangs.AbilityName(id)).ToUpperInvariant();
                float ready=k?.Ready!=null&&i<k.Ready.Length?k.Ready[i]:0,left=ready-t;float cd=(katana?NativeKatana.Cooldown[i]:NativeFangs.Cooldown[i])*(float)(p.Stats?["abilityCooldown"]??1);
                bool live=k!=null&&k.CastKind==id&&t<k.CastUntil||!katana&&i==1&&k!=null&&t>=k.VeilAt&&t<k.VeilUntil;
                cards.Add(new Card{Key=key,State=live?"ACTIVE":left>0&&left<60?left.ToString("0.0")+"s":"READY",Icon=katana?NativeKatana.Icon(id):NativeFangs.Icon(id),Ready=!live&&left<=0,Active=live,Dial=left>0&&left<60?1-Mathf.Clamp01(left/Mathf.Max(.1f,cd)):1});
            }
            // The Katana's counter: a card while its window is open.
            if(katana&&k!=null&&t<k.CounterUntil)cards.Add(new Card{Key="LMB · "+NativeKatana.AbilityName(NativeKatana.Counter).ToUpperInvariant(),State="NOW",Icon=NativeKatana.Icon(NativeKatana.Counter),Ready=true,Active=true,Dial=Mathf.Clamp01((k.CounterUntil-t)/NativeKatana.CounterWindow)});
        }
    }
    public sealed partial class NativeAudio
    {
        // The blade classes' moves and hits, each once as the record first shows it.
        private void BladeMoments(NativePilot p,Vector3 pos,float t,bool mine)
        {
            var k=p.Blade;if(!NativeBlades.Is(p.Seat.spec)||k==null)return;bool katana=p.Seat.spec==NativeKatana.Spec;
            if(k.CastKind!=null&&t>=k.CastRelease&&t-k.CastRelease<.15f&&Once(p.Seat.id+"blrel"+k.CastKind+k.CastStep,k.CastRelease))
            {
                string id=null;float gain=.85f,rate=1;
                if(katana)switch(k.CastKind){case NativeKatana.Cut:id="blade-cut";rate=1+.06f*k.CastStep;if(k.CastStep==NativeKatana.Steps-1){gain=1;rate=.85f;}break;case NativeKatana.Rush:id="blade-dash";break;case NativeKatana.Iai:id="blade-draw";gain=1;break;case NativeKatana.Storm:id="blade-wind";break;case NativeKatana.Counter:id="blade-clash";gain=1;break;}
                else switch(k.CastKind){case NativeFangs.Cut:id="fang-cut";rate=1+.05f*k.CastStep;break;case NativeFangs.Pounce:id="fang-throw";rate=.8f;break;case NativeFangs.Step:id="fang-vanish";break;case NativeFangs.Veil:id="fang-smoke";gain=1;break;case NativeFangs.Fan:id="fang-throw";break;case NativeFangs.Bloom:id="fang-hiss";break;case NativeFangs.Assassinate:id="fang-hiss";rate=.7f;break;}
                if(id!=null)Toon(id,pos,gain,mine,rate);
            }
            // The storm's cuts: a swish each.
            if(katana&&k.CastKind==NativeKatana.Storm&&t>=k.CastRelease&&t<k.CastUntil-NativeKatana.StormRecover){float step=Mathf.Floor((t-k.CastRelease)/NativeKatana.StormEvery);if(Once(p.Seat.id+"blstorm"+k.CastAt,step))Toon("blade-cut",pos,.6f,mine,1.15f+.04f*(step%4));}
            if(k.Hits!=null)foreach(var h in k.Hits)
            {
                if(h==null||t<h.At||t-h.At>=.15f||!Once(p.Seat.id+"blhit"+h.Id,h.At))continue;var at=new Vector3(h.Pos.x,h.Pos.y,h.Pos.z);
                switch(h.Kind)
                {
                    case NativeKatana.HitSplit:Toon("blade-split",at,1,mine,h.Amount>0?1.2f:1);break;
                    case NativeKatana.HitSheathe:Toon("blade-click",at,.9f,mine,h.Amount>0?1.15f:1);Toon("blade-sheathe",at,.6f,mine);break;
                    case NativeKatana.HitMark:Toon("blade-ring",at,.35f,mine,1.3f);break;
                    case NativeKatana.HitDraw:break;
                    case NativeKatana.HitFinisher:case NativeKatana.HitCounter:Toon("blade-slice",at,1,mine,.8f);Toon("blade-ring",at,.5f,mine);break;
                    case NativeKatana.HitLaunch:Toon("blade-cut",at,.8f,mine,.75f);break;
                    case NativeKatana.HitWave:if(h.Victim==0)Toon("blade-wave",at,.9f,mine);else Toon("blade-slice",at,.7f,mine,1.2f);break;
                    case NativeKatana.HitFocus:Toon("blade-drop",at,.6f,mine);break;
                    case NativeKatana.HitStorm:if(h.Victim!=0)Toon("blade-slice",at,.55f,mine,1.1f+.05f*(h.Amount%4));break;
                    case NativeKatana.HitBleed:case NativeFangs.HitDrip:case NativeFangs.HitDrain:break;
                    case NativeFangs.HitStab:case NativeFangs.HitPounce:Toon("fang-stab",at,.9f,mine);break;
                    case NativeFangs.HitVanish:if(h.Amount>0)Toon("fang-vanish",at,.8f,mine,1.25f);break;
                    case NativeFangs.HitSmoke:case NativeFangs.HitCloud:break;
                    case NativeFangs.HitThrow:break;
                    case NativeFangs.HitKnife:Toon("fang-stab",at,.6f,mine,1.3f);break;
                    case NativeFangs.HitBloom:Toon("fang-bloom",at,h.Victim==0?.8f:.9f,mine,1+.03f*Math.Min(10,h.Amount));break;
                    case NativeFangs.HitTakedown:Toon("fang-stab",at,1,mine,.8f);break;
                    case NativeFangs.HitKill:Toon("fang-stab",at,1,mine,.7f);Toon("fang-toll",at,.7f,mine);break;
                    case NativeFangs.HitSpread:Toon("fang-hiss",at,.5f,mine,1.3f);break;
                    default:Toon(katana?"blade-slice":"fang-stab",at,.75f,mine,.95f+.1f*((h.Id%5)/4f));break;
                }
            }
        }
        private int bladePickCue;private string bladePickId;private float bladePickLast=-1;
        /// <summary>A blade class chip's pick: its animation's cues (Core NativeKatanaChips/NativeFangsChips.Picks) on the spotlight's clock.</summary>
        public void BladePick(string chip,float age)
        {
            bool katana=NativeKatanaChips.Is(chip);int i=katana?NativeKatanaChips.Index(chip):NativeFangsChips.Index(chip);if(i<0)return;float s=age-NativeDraftPlant.DetailsAt-NativeBladeChipArt.Start;
            if(chip!=bladePickId||age<bladePickLast-.2f){bladePickId=chip;bladePickCue=0;}bladePickLast=age;var list=katana?NativeKatanaChips.Picks[i]:NativeFangsChips.Picks[i];
            while(bladePickCue<list.Length&&s>=list[bladePickCue].At){var c=list[bladePickCue++];if(s-c.At<.25f)Toon(c.Id,null,c.Gain,true,c.Rate);}
        }
    }
}
