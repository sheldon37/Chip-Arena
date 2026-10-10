using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// client/block-cooldown-tint.ts at 05950ea: while a pilot's block is cooling down (blockCd &gt; 0 and no block active) every
    /// rig part's colour and emissive pulse dimmer at 1.5 Hz, by up to 30% and eased in over the last .25 s of the cooldown; an
    /// Ansatsuken parry flashes the whole rig toward 0x9fe9ff with a 0x63cfff glow over .22 s (arcade parryAt, ARCADE.parryFlash).
    /// Applied to every pilot rig and the first-person rig (renderer.ts:155), never to the team identity, block rings, enchant,
    /// spell charge or body chips. Presentation only.
    /// </summary>
    public static class NativeBlockTint
    {
        public const int FlashColor=0x9fe9ff,FlashGlow=0x63cfff;public const float FlashSeconds=.22f,GlowGain=1.8f;
        /// <summary>The parry flash, 1 at parryAt falling to 0 over .22 s (0 for the dead).</summary>
        public static float Flash(bool alive,float parryAt,float time)=>alive?Math.Max(0,1-(time-parryAt)/FlashSeconds):0;
        /// <summary>
        /// The colour factor every tinted part is multiplied by (1 when neither cooling nor flashing, as the source releases its
        /// material copies). blockCd/blockTime are the seconds of cooldown and block left.
        /// </summary>
        public static float Factor(bool alive,float blockCd,float blockTime,float flash,float time)
        {
            bool cooling=alive&&blockCd>0&&blockTime<=0;if(!cooling&&!(flash>0))return 1;
            float pulse=.70f+.30f*(float)Math.Cos(time*Math.PI*2*1.5);return 1-(1-pulse)*Math.Min(1,Math.Max(0,blockCd)/.25f);
        }
        /// <summary>How far the flash pulls a part's colour toward FlashColor (color.lerp(0x9fe9ff, min(1, .6 + flash*.4))).</summary>
        public static float FlashLerp(float flash)=>flash>0?Math.Min(1,.6f+flash*.4f):0;
        /// <summary>three's sRGB hex to its linear working colour (Color.setHex under ColorManagement).</summary>
        public static Vector3 Linear(int hex)
        {
            float C(int v){float c=v/255f;return c<=.04045f?c/12.92f:(float)Math.Pow((c+.055)/1.055,2.4);}
            return new Vector3(C((hex>>16)&255),C((hex>>8)&255),C(hex&255));
        }
        /// <summary>
        /// The flat overlay colour (linear) the native rig draws over itself at opacity FlashLerp(flash): the source moves every
        /// part's colour toward 0x9fe9ff by that much and adds a 0x63cfff × 1.8 × flash glow; an overlay of colour c at opacity k
        /// gives base·(1-k) + c·k, so c = 0x9fe9ff + glow / k (lighting on the flash colour taken as 1).
        /// </summary>
        public static Vector3 OverlayColor(float flash)
        {
            float k=FlashLerp(flash);if(!(k>0))return Vector3.Zero;return Linear(FlashColor)+Linear(FlashGlow)*(GlowGain*flash/k);
        }
        /// <summary>The replicated block clocks as the source's countdowns: cooldown and block seconds left at `time`.</summary>
        public static float Cooldown(Actor a,float time)=>a==null?0:Math.Max(0,a.BlockReady-time);
        public static float Blocking(Actor a,float time)=>a==null?0:Math.Max(0,a.BlockUntil-time);
    }
    /// <summary>
    /// The class bases of the camera-attached first-person view (first-person-view.ts:69, FP_TUNE), in Unity camera space: the
    /// source view root sits in front of a camera looking down -z and is turned by π, so its z is negated here. Guns and the DEV
    /// fighters sit at z .2; melee is raised and pushed to .3; the fighters' fists are centred and low.
    /// </summary>
    public static class NativeViewBase
    {
        public static readonly Vector3 Gun=new Vector3(.24f,-.48f,.20f),Melee=new Vector3(.16f,-.22f,.30f),Fighter=new Vector3(0,-.48f,.20f);
        public static bool IsFighter(string spec)=>spec=="emperor"||spec=="ansatsuken";
    }
}
