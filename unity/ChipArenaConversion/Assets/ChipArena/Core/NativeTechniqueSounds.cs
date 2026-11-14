using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The techniques' sound (docs/WEAPON-TECHNIQUES.md), from the record: each moment (the release, the armed or stuck time, the triggered hit,
    /// the recast, the end) once, on the bank's existing cues. <see cref="At"/> says which moments a kind has; <see cref="Moment"/> what each plays,
    /// and whether it sounds at the effect's point rather than the pilot.
    /// </summary>
    public static partial class NativeTechniqueSounds
    {
        public const int Moments=5,Release=0,Arm=1,Hit=2,Recast=3,End=4;
        /// <summary>When moment `m` of the record falls (float.NaN: this cast has none).</summary>
        public static float At(NativeTechnique k,int m)
        {
            switch(m)
            {
                case Release:return ThirdFromPress(k.Kind)?k.At:k.Release;
                case Arm:if(ThirdFromPress(k.Kind))return k.Arm>k.At?k.Arm:float.NaN;return k.Kind==NativeTechniques.Bastion||k.Kind==NativeTechniques.WildfireRun||k.Arm<=k.Release?float.NaN:k.Arm;
                case Hit:return k.HitAt>-50?k.HitAt:float.NaN;
                case Recast:return k.RecastAt>-50?k.RecastAt:float.NaN;
                default:return ThirdEnd(k);
            }
        }
        private static int S(NativeKitSound[] o,int n,string key,string clip,float gain){if(n<o.Length)o[n]=new NativeKitSound{Key=key,Clip=clip,Gain=gain};return Math.Min(o.Length,n+1);}
        /// <summary>The cues of moment `m` into `o`; returns how many. `atPoint`: at the record's Point.</summary>
        public static int Moment(string kind,int m,NativeKitSound[] o,out bool atPoint)
        {
            int n=0;atPoint=false;
            switch(kind)
            {
                case NativeTechniques.Riposte:if(m==Hit){n=S(o,n,"melee.shield.block","parry",.9f);n=S(o,n,"melee.sword.swing","sword",.7f);}else if(m==Release)n=S(o,n,"melee.sword.swing","sword",.25f);break;
                case NativeTechniques.SmokeVeil:if(m==Release){atPoint=true;n=S(o,n,"melee.daggers.throw","daggers",.4f);n=S(o,n,"blast.transient","dash",.6f);}break;
                case NativeTechniques.Bastion:if(m==Release){atPoint=true;n=S(o,n,"melee.shield.bash","shield",.9f);n=S(o,n,"impact.metal","metal",.6f);}else if(m==Hit){atPoint=true;n=S(o,n,"melee.shield.block","parry",.7f);}break;
                case NativeTechniques.AnchorThrow:if(m==Release)n=S(o,n,"melee.greatsword.swing","greatsword",.8f);else if(m==Arm){atPoint=true;n=S(o,n,"impact.metal","metal",.9f);n=S(o,n,"blast.quake","stone",.5f);}else if(m==Recast)n=S(o,n,"move.jump","jump",.8f);else if(m==End){atPoint=true;n=S(o,n,"move.land","explosion",.6f);}break;
                case NativeTechniques.RicochetCoin:if(m==Release)n=S(o,n,"gun.reload.end","metal",.35f);else if(m==Hit){atPoint=true;n=S(o,n,"gun.ricochet","metal",1);}break;
                case NativeTechniques.SentryDrop:if(m==Release){atPoint=true;n=S(o,n,"impact.metal","metal",.5f);}else if(m==Arm){atPoint=true;n=S(o,n,"gun.reload.end","metal",.7f);}else if(m==End){atPoint=true;n=S(o,n,"gun.dryfire","metal",.7f);}break;
                case NativeTechniques.GrappleLine:if(m==Release)n=S(o,n,"gun.frag.throw","dash",.6f);else if(m==Arm){atPoint=true;n=S(o,n,"impact.metal","metal",.7f);n=S(o,n,"move.roll","dash",.5f);}break;
                case NativeTechniques.GaleArrow:if(m==Arm){atPoint=true;n=S(o,n,"blast.kinetic","dash",.85f);}else if(m==Release)n=S(o,n,"move.roll","dash",.3f);break;
                case NativeTechniques.IgnitionLeap:if(m==Release){atPoint=true;n=S(o,n,"blast.fire","explosion",.85f);n=S(o,n,"magic.flame-lance","fire",.5f);}break;
                case NativeTechniques.HealingSpring:if(m==Release){atPoint=true;n=S(o,n,"magic.geyser","water",.55f);n=S(o,n,"impact.water","water",.6f);}break;
                case NativeTechniques.StaticRecall:if(m==Release){n=S(o,n,"blast.static","lightning",.7f);}else if(m==Recast){n=S(o,n,"magic.blink-strike","demon-step",.9f);n=S(o,n,"blast.shock","lightning",.6f);}break;
                case NativeTechniques.ShadeSwap:if(m==Release)n=S(o,n,"magic.darkness.shot","darkness",.8f);else if(m==Hit){atPoint=true;n=S(o,n,"magic.darkness.hit","darkness",.9f);n=S(o,n,"magic.blink-strike","demon-step",.7f);}break;
                case NativeTechniques.PartingLine:if(m==Release){n=S(o,n,"melee.sword.swing","sword",.5f);n=S(o,n,"move.roll","dash",.5f);}else if(m==Arm){atPoint=true;n=S(o,n,"blast.kinetic","dash",.8f);n=S(o,n,"melee.sword.hit","sword",.5f);}break;
                case NativeTechniques.Tripwire:if(m==Release)n=S(o,n,"melee.daggers.throw","daggers",.8f);else if(m==Arm){atPoint=true;n=S(o,n,"impact.metal","metal",.45f);}else if(m==Hit){n=S(o,n,"melee.daggers.hit","daggers",.8f);n=S(o,n,"gun.reload.end","metal",.4f);}break;
                case NativeTechniques.ShieldSurf:if(m==Release){n=S(o,n,"impact.metal","metal",.6f);n=S(o,n,"move.roll","dash",.6f);}else if(m==Recast)n=S(o,n,"move.jump","jump",.8f);else if(m==End)n=S(o,n,"melee.shield.block","parry",.3f);break;
                case NativeTechniques.IronVow:if(m==Release){atPoint=true;n=S(o,n,"blast.quake","stone",.7f);n=S(o,n,"melee.greatsword.hit","greatsword",.7f);}else if(m==Hit)n=S(o,n,"melee.shield.block","parry",.7f);else if(m==End)n=S(o,n,"blast.kinetic","dash",.8f);break;
                case NativeTechniques.BountyRound:if(m==Release)n=S(o,n,"gun.pistol.shot","pistol",.9f);else if(m==Hit)n=S(o,n,"gun.ricochet","metal",.7f);break;
                case NativeTechniques.RecoilBurst:if(m==Release){n=S(o,n,"gun.machinegun.shot","machinegun",1);n=S(o,n,"blast.fire","explosion",.6f);}else if(m==End)n=S(o,n,"gun.dryfire","metal",.7f);break;
                case NativeTechniques.SonarDart:if(m==Release)n=S(o,n,"gun.frag.throw","dash",.5f);else if(m==Arm){atPoint=true;n=S(o,n,"gun.reload.end","metal",.4f);}else if(m==Hit){atPoint=true;n=S(o,n,"impact.metal","metal",.6f);}break;
                case NativeTechniques.SpiritHawk:if(m==Release)n=S(o,n,"move.roll","dash",.5f);else if(m==Hit)n=S(o,n,"melee.daggers.hit","daggers",.7f);break;
                case NativeTechniques.WildfireRun:if(m==Release){n=S(o,n,"blast.fire","explosion",.5f);n=S(o,n,"magic.flame-lance","fire",.4f);}break;
                case NativeTechniques.Slipstream:if(m==Release){n=S(o,n,"magic.geyser","water",.5f);n=S(o,n,"impact.water","water",.5f);}break;
                case NativeTechniques.LightningRod:if(m==Release)n=S(o,n,"melee.daggers.throw","daggers",.4f);else if(m==Arm){atPoint=true;n=S(o,n,"blast.static","lightning",.8f);n=S(o,n,"impact.metal","metal",.6f);}else if(m==Hit){n=S(o,n,"blast.shock","lightning",.5f);}break;
                case NativeTechniques.GraveWalk:if(m==Release)n=S(o,n,"magic.darkness.shot","darkness",.6f);else if(m==End)n=S(o,n,"magic.darkness.hit","darkness",.7f);break;
            }
            if(n==0)n=ThirdMoment(kind,m,o,ref atPoint);
            return n;
        }
    }
}
