using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The pistol's, rifle's and machine gun's recorded sound (docs/GUN-SOUNDS.md; owner 2026-11-24): each shot plays a close layer (the
    /// pack's close shot with its body) and a distant one blended by range, and a tail whose share grows with range, from the event's
    /// position after the sound's travel time; the machine gun's tail plays once its stream stops. Reloads play in parts on the reload
    /// clips' retimed spans (first person: NativeTacticalTiming's warp; third person: NativeTacticalMotion's ReloadEnd), the rifle's bolt
    /// on its fire action, and shell casings land on the shooter's floor (the footsteps' surface). Presentation only: every moment is
    /// read from the Shot events and the replicated reload and shot times every client already has; no rule, timing or wire field.
    /// Every key is a literal here (Tests/EffectsAudioChecks.cs). With no clips for a gun's near key its shots keep today's gun.*.shot
    /// route; with none for its first reload part, today's reload start/end cues.
    /// </summary>
    public static class NativeGunSounds
    {
        /// <summary>Every key, in the contract order (NativeSoundKeys).</summary>
        public static readonly string[] Keys={
            "gun.pistol.near","gun.pistol.far","gun.pistol.tail","gun.rifle.near","gun.rifle.far","gun.rifle.tail","gun.machinegun.near","gun.machinegun.far","gun.machinegun.tail",
            "gun.pistol.reload.out","gun.pistol.reload.in","gun.rifle.reload.out","gun.rifle.reload.in","gun.machinegun.reload.out","gun.machinegun.reload.in","gun.machinegun.reload.bolt","gun.rifle.bolt",
            "gun.casing.pistol.concrete","gun.casing.pistol.metal","gun.casing.pistol.wood","gun.casing.pistol.gravel",
            "gun.casing.rifle.concrete","gun.casing.rifle.metal","gun.casing.rifle.wood","gun.casing.rifle.gravel",
            "gun.casing.machinegun.concrete","gun.casing.machinegun.metal","gun.casing.machinegun.wood","gun.casing.machinegun.gravel"};

        /// <summary>The routed shot's gain (NativeEventSounds.Route), shared by the layers before their range weights.</summary>
        public const float Gain=.65f;
        /// <summary>The close-to-distant blend (metres; linear in log distance), the speed of sound, the longest travel delay, the
        /// weight under which a layer is not played, and the tail's largest share.</summary>
        public const float BlendFrom=10,BlendTo=60,SoundSpeed=343,MaxDelay=.35f,Audible=.03f,TailMax=3;
        public static bool Gun(string spec)=>spec=="pistol"||spec=="rifle"||spec=="machinegun";
        public static WeaponId Weapon(string spec)=>spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
        public static string Near(string spec)=>spec=="pistol"?"gun.pistol.near":spec=="rifle"?"gun.rifle.near":spec=="machinegun"?"gun.machinegun.near":null;
        public static string Far(string spec)=>spec=="pistol"?"gun.pistol.far":spec=="rifle"?"gun.rifle.far":spec=="machinegun"?"gun.machinegun.far":null;
        public static string Tail(string spec)=>spec=="pistol"?"gun.pistol.tail":spec=="rifle"?"gun.rifle.tail":spec=="machinegun"?"gun.machinegun.tail":null;
        /// <summary>The machine gun streams one-shots at its interval; its tail (the pack's release: body and tail) plays once the stream
        /// stops. The pistol and rifle ring a tail with each shot.</summary>
        public static bool Stream(string spec)=>spec=="machinegun";

        /// <summary>0 at <see cref="BlendFrom"/> m or nearer, 1 at <see cref="BlendTo"/> m or farther, linear in log distance.</summary>
        public static float Blend(float d)=>d<=BlendFrom?0:d>=BlendTo?1:(float)(Math.Log(d/BlendFrom)/Math.Log(BlendTo/BlendFrom));
        /// <summary>The close and distant layers' equal-power weights (before the bank's distance falloff).</summary>
        public static float NearWeight(float d)=>(float)Math.Cos(Blend(MathUtil.Finite(d)?d:0)*Math.PI/2);
        public static float FarWeight(float d)=>(float)Math.Sin(Blend(MathUtil.Finite(d)?d:0)*Math.PI/2);
        /// <summary>The tail's weight before the bank's distance falloff: it falls off at half the shot's dB rate (the reverberant
        /// field) and grows with the blend, so far away it rings longer under a fainter shot.</summary>
        public static float TailWeight(float d){d=MathUtil.Finite(d)?Math.Max(0,d):0;return Math.Min(TailMax,(.4f+.6f*Blend(d))/(float)Math.Sqrt(NativeEventSounds.Falloff(d)));}
        /// <summary>The sound's travel time from <paramref name="d"/> m (at most <see cref="MaxDelay"/>).</summary>
        public static float Delay(float d)=>MathUtil.Finite(d)?MathUtil.Clamp(d/SoundSpeed,0,MaxDelay):0;
        /// <summary>A stream's release: this long after its last shot (1.6 intervals) its tail plays, started where it would be had it
        /// begun with that shot.</summary>
        public static float ReleaseAfter(float interval)=>Math.Max(.1f,(interval>0?interval:.085f)*1.6f);
        /// <summary>A semi-automatic shooter rings at most one tail this often (the pistol's .42 s interval rings every shot).</summary>
        public const float TailGap=.38f;

        // ---------- reloads ----------
        /// <summary>A reload part: its key and its start (s) in the gun's tactical reload recording (the Tactical Shooter Pack's, which
        /// runs on its reload clip's clock from 0).</summary>
        public struct Part{public string Key;public float At;}
        private static readonly Part[] pistolParts={new Part{Key="gun.pistol.reload.out",At=.42f},new Part{Key="gun.pistol.reload.in",At=1.85f}};
        private static readonly Part[] rifleParts={new Part{Key="gun.rifle.reload.out",At=.725f},new Part{Key="gun.rifle.reload.in",At=1.925f}};
        private static readonly Part[] machineGunParts={new Part{Key="gun.machinegun.reload.out",At=.82f},new Part{Key="gun.machinegun.reload.in",At=1.575f},new Part{Key="gun.machinegun.reload.bolt",At=2.365f}};
        public static Part[] Reload(string spec)=>spec=="pistol"?pistolParts:spec=="rifle"?rifleParts:spec=="machinegun"?machineGunParts:Array.Empty<Part>();
        /// <summary>The first-person reload clips' pack lengths (docs/FPS-TACTICAL.md): each whole clip fills ReloadAt..ReloadUntil
        /// through its gun's ReloadWarp.</summary>
        public const float PistolClip=3.467f,RifleClip=3.083f,MachineGunClip=3.483f;
        /// <summary>The share of the reload's span at which a part recorded <paramref name="at"/> s into the reload plays: in first
        /// person through the inverse of the clip's warp (NativeTacticalTiming.ReloadClipTime), in third person on the clip's
        /// ReloadEnd (NativeTacticalMotion.ReloadTime).</summary>
        public static float ReloadShare(string spec,float at,bool firstPerson)
        {
            if(firstPerson)
            {
                var g=NativeTacticalGun.For(Weapon(spec));float clip=spec=="pistol"?PistolClip:spec=="rifle"?RifleClip:MachineGunClip;
                return g==null?0:Unwarp(g.ReloadWarp,at/clip);
            }
            var tp=spec=="pistol"?NativeTacticalMotion.Pistol:spec=="rifle"?NativeTacticalMotion.Rifle:spec=="machinegun"?NativeTacticalMotion.MachineGun:null;
            return tp==null||!(tp.ReloadEnd>0)?0:MathUtil.Clamp(at/tp.ReloadEnd,0,1);
        }
        /// <summary>The inverse of NativeTacticalTiming.Warp (a monotone piecewise-linear map through (u, v) knots): the u whose v is
        /// <paramref name="v"/>; identity without knots.</summary>
        public static float Unwarp(float[] knots,float v)
        {
            v=MathUtil.Clamp(MathUtil.Finite(v)?v:0,0,1);if(knots==null||knots.Length<4)return v;
            for(int i=2;i<knots.Length;i+=2)if(v<=knots[i+1]){float u0=knots[i-2],u1=knots[i],v0=knots[i-1],v1=knots[i+1];return v1>v0?u0+(u1-u0)*(v-v0)/(v1-v0):u0;}
            return knots[knots.Length-2];
        }
        /// <summary>The reload parts', bolt's and casings' gains, and their reach for other pilots, who hear them on the foley falloff.</summary>
        public const float ReloadGain=.6f,BoltGain=.7f,CasingGain=.55f,FoleyReach=25;
        /// <summary>A foley sound's extra falloff for another pilot's: the bank's falloff applied once more, so a reload or a casing is
        /// close-range detail.</summary>
        public static float Foley(float d)=>NativeEventSounds.Falloff(MathUtil.Finite(d)?Math.Max(0,d):0);

        // ---------- the rifle's bolt ----------
        public const string Bolt="gun.rifle.bolt";
        /// <summary>The bolt recording's start and the case leaving the action (the bolt drawn back), s into the pack's fire clip.</summary>
        public const float BoltAt=.445f,BoltEject=.6f;
        /// <summary>The age (s after a rifle shot) of a moment <paramref name="at"/> s into the pack's fire clip: first person fills the
        /// interval with the action (NativeTacticalTiming.FireActionTime), third person runs it at up to twice its pace
        /// (NativeTacticalMotion.FireTime); the bolt is home when the next shot is allowed in both.</summary>
        public static float BoltAge(float at,float interval,bool firstPerson)
        {
            float action=NativeTacticalMotion.Rifle.FireReady;if(!(interval>0))interval=GunRules.Get(WeaponId.Rifle).Interval;
            if(firstPerson)return at/action*interval;
            float rate=Math.Max(1,Math.Min(NativeTacticalMotion.MaxFireRate,action/interval));return at<=action?at/rate:action/rate+(at-action);
        }

        // ---------- casings ----------
        private static readonly string[] casings={
            "gun.casing.pistol.concrete","gun.casing.pistol.metal","gun.casing.pistol.wood","gun.casing.pistol.gravel",
            "gun.casing.rifle.concrete","gun.casing.rifle.metal","gun.casing.rifle.wood","gun.casing.rifle.gravel",
            "gun.casing.machinegun.concrete","gun.casing.machinegun.metal","gun.casing.machinegun.wood","gun.casing.machinegun.gravel"};
        /// <summary>A casing's key on the floor under the shooter (<paramref name="step"/>: its footsteps' surface key, null when none is
        /// known: concrete): stone takes concrete; snow swallows it (null).</summary>
        public static string Casing(string spec,string step)
        {
            int g=spec=="pistol"?0:spec=="rifle"?1:spec=="machinegun"?2:-1;if(g<0||step==NativeFootsteps.Snow)return null;
            int s=step==NativeFootsteps.Metal?1:step==NativeFootsteps.Wood?2:step==NativeFootsteps.Gravel?3:0;
            return casings[g*4+s];
        }
        /// <summary>The case's fall to the floor (s) and its spread either way.</summary>
        public const float CasingFall=.42f,CasingSpread=.08f;
        /// <summary>When the case lands, s after the shot: the pistol and machine gun eject with the shot, the rifle when its bolt is drawn.</summary>
        public static float CasingAge(string spec,float interval,bool firstPerson,float random01)
            =>(spec=="rifle"?BoltAge(BoltEject,interval,firstPerson):0)+CasingFall+(MathUtil.Clamp(random01,0,1)*2-1)*CasingSpread;
    }
}
