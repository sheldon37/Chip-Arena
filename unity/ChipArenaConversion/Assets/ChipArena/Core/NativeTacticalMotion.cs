using System;

namespace ChipArena.UnityConversion
{
    // Third person on the Tactical Shooter Pack's clips (owner 2026-11-21: "Can we fix the animations to use their shooting animations
    // and walking/running for guns?"; docs/TP-TACTICAL.md). The pack's full-body clips, baked into the motion bank on the body (its
    // Operator skeleton read as a humanoid pose: the local bake, never pack code), take over a gun's upper body in third person:
    //  - the aim (each gun's ready hold, shouldered: the pack's idle pose; pitched by the spine, Runtime VisualTacticalPitch),
    //  - each shot (the pistol's kick, the rifle's shot and bolt; the machine gun's procedural kick, as the pack's own),
    //  - the reload (the pack's tactical reload, fitted to the rules' reload),
    //  - the carry on the move and in the air (the pack walk's ready hold, the pack sprint's port carry by the sprint's share).
    // Measured on the bake (docs/TP-TACTICAL.md): the pack's walk, sprint and jump clips move no leg (their feet and pelvis hold still
    // over every frame: they are first-person clips), so the legs stay the game's own 8-way strides, jumps and landings; the draws and
    // holsters key the gun bone alone (the pack's runtime IK carries the hands), so they show hanging arms and are not used. Standing
    // at rest keeps the match idles (owner 2026-11-10/11-21: the long guns' low carry, the pistol's low ready).
    // Presentation only: every clip is fitted to the rules (the interval, ReloadAt..ReloadUntil, the gun's Settle); no rule, timing,
    // hitbox, movement or network field is read differently or written. A bank without the pack's clips keeps the old gun sets.
    public static class NativeTacticalMotion
    {
        /// <summary>A gun's clips on the pack and their moments (clip seconds, measured on the bake from the clips' own events).</summary>
        public sealed class Gun
        {
            public string Spec, Idle, Fire, Reload;
            /// <summary>The shot's clip: where the gun is ready again (the rifle's bolt home: the pack's OnActionEnded; the pistol's kick
            /// settled), placed on the rules' interval; the rest plays at its own pace.</summary>
            public float FireReady;
            /// <summary>The reload's clip: where it is back on the gun (the pack's OnActionEnded), placed on ReloadUntil.</summary>
            public float ReloadEnd;
            /// <summary>The carry on the move: the walk's (its ready hold) and the sprint's (null: the game's own sprint carry).</summary>
            public string Walk, Sprint;
            /// <summary>The machine gun: no shot clip in the pack (its full-auto recoil is procedural there too): a kick per shot.</summary>
            public bool Kick;
            /// <summary>The draw (the bake puts the arms back on its gun bone: the pack keys the gun alone), its end in the clip (the pack's
            /// OnActionEnded) and how long it plays on the game's clock, ending on the fight's start (no rule draws a gun: the round's GO).</summary>
            public string Draw; public float DrawEnd, DrawTime;
            /// <summary>How the support hand holds the model (Runtime VisualTacticalGrip; owner 2026-11-22: "think we need to fix this here").</summary>
            public Grip Support;
        }
        /// <summary>The support hand's hold in the weapon frame (+z the bore, +y its top, +x its right): the palm frame's finger line
        /// (wrist to the middle knuckle) and its thumb side (pinky to index), and the hand's point that sits on the model's measured
        /// Support (a share of the way from the wrist to the middle knuckle, then Depth m out of the palm).</summary>
        public sealed class Grip { public System.Numerics.Vector3 Fingers, Thumb; public float Along, Depth; public float[] Hand; }
        /// <summary>The left hand's finger muscles a Grip's Hand gives, in its order (each "LeftHand." + name, Unity's humanoid muscles:
        /// negative curls a joint, positive stretches it; a spread near the game's own open hand keeps the fingers together).</summary>
        public static readonly string[] HandMuscles={"Thumb.1 Stretched","Thumb.Spread","Thumb.2 Stretched","Thumb.3 Stretched","Index.1 Stretched","Index.Spread","Index.2 Stretched","Index.3 Stretched",
            "Middle.1 Stretched","Middle.Spread","Middle.2 Stretched","Middle.3 Stretched","Ring.1 Stretched","Ring.Spread","Ring.2 Stretched","Ring.3 Stretched","Little.1 Stretched","Little.Spread","Little.2 Stretched","Little.3 Stretched"};
        /// <summary>A long gun's fore-end, held from below: the palm under the handguard facing up, the fingers across under it and closed up
        /// its far (right) side, the thumb along its near side pointing forward, the closed fist's hollow on the handguard's axis. Its fingers
        /// (Hand): the knuckles bent up the far side, the middle and end joints curled over its top edge, together; the thumb straight.</summary>
        public static readonly Grip ForeEnd=new Grip{Fingers=new System.Numerics.Vector3(1,0,.35f),Thumb=new System.Numerics.Vector3(0,0,1),Along=1,Depth=.025f,
            Hand=new[]{-.3f,0,.2f,.3f, -.9f,-.05f,-1f,-.7f, -1f,-.45f,-1f,-.7f, -1.05f,-.05f,-1f,-.7f, -1.1f,.17f,-.95f,-.6f}};
        /// <summary>The pistol's thumbs-forward wrap: the support palm on the grip's left side facing it, the fingers wrapped round the front
        /// over the gun hand's, the thumb up along the frame pointing forward. Its fingers (Hand): curled round the grip's front, together;
        /// the thumb straight.</summary>
        public static readonly Grip Wrap=new Grip{Fingers=new System.Numerics.Vector3(.25f,-.35f,1),Thumb=new System.Numerics.Vector3(0,1,0),Along=.55f,Depth=.015f,
            Hand=new[]{.2f,.2f,.4f,.4f, -.6f,-.05f,-.9f,-.6f, -.65f,-.45f,-.95f,-.6f, -.7f,-.05f,-.95f,-.6f, -.75f,.17f,-.9f,-.5f}};
        public const string PackWalk="A_FP_Walk", PackSprint="A_FP_Sprint";
        // The guns as first person pairs them (docs/FPS-TACTICAL.md): the pistol on the WK-11 Viper, the rifle on the SRM-12 (its shot
        // works the bolt), the machine gun on the TR15.
        public static readonly Gun Pistol=new Gun{Spec="pistol",Idle="A_FP_WK-11_Viper_Idle_Pose",Fire="A_FP_WK-11_Viper_Fire",Reload="A_FP_WK-11_Viper_Reload_Tactical",
            FireReady=.42f,ReloadEnd=3.083f,Walk="A_FP_WK-11_Viper_Idle_Pose",Sprint=null,Draw="A_FP_WK-11_Viper_Quick_Draw",DrawEnd=.667f,DrawTime=.6f,Support=Wrap};
        public static readonly Gun Rifle=new Gun{Spec="rifle",Idle="A_FP_SRM-12_Idle_Pose",Fire="A_FP_SRM-12_Fire",Reload="A_FP_SRM-12_Reload_Tactical",
            FireReady=1.167f,ReloadEnd=2.75f,Walk=PackWalk,Sprint=PackSprint,Draw="A_FP_SRM-12_Draw",DrawEnd=1.5f,DrawTime=1.1f,Support=ForeEnd};
        public static readonly Gun MachineGun=new Gun{Spec="machinegun",Idle="A_FP_TR15_Idle_Pose_Non_Grip",Fire=null,Reload="A_FP_TR15_Reload_Tactical",
            FireReady=0,ReloadEnd=3.0f,Walk=PackWalk,Sprint=PackSprint,Kick=true,Draw="A_FP_TR15_Draw",DrawEnd=1.833f,DrawTime=1.2f,Support=ForeEnd};
        /// <summary>The gun a weapon spec plays on the pack (null: not a gun the pack covers).</summary>
        public static Gun For(string spec)=>spec=="pistol"?Pistol:spec=="rifle"?Rifle:spec=="machinegun"?MachineGun:null;
        /// <summary>Every clip the guns name (the bake's list; the local bake writes them into the motion bank).</summary>
        public static readonly string[] Clips=Names();
        private static string[] Names()
        {
            var l=new System.Collections.Generic.List<string>();
            foreach(var g in new[]{Pistol,Rifle,MachineGun})foreach(var c in new[]{g.Idle,g.Fire,g.Reload,g.Walk,g.Sprint,g.Draw})if(c!=null&&!l.Contains(c))l.Add(c);
            return l.ToArray();
        }
        /// <summary>Whether the bank carries every clip gun `g` plays.</summary>
        public static bool Installed(Gun g,NativeVisualMotion.IClips clips)
        {
            if(g==null||clips==null)return false;
            foreach(var c in new[]{g.Idle,g.Fire,g.Reload,g.Walk,g.Sprint})if(c!=null&&!clips.Has(c))return false;
            return true;
        }

        // ---- timing on the rules ----
        /// <summary>A shot's clip time `age` s after the shot: its ready moment (FireReady) on the rules' interval, at most MaxFireRate
        /// times its pace and never slower than it; past that, on at its own pace.</summary>
        public const float MaxFireRate=2f;
        public static float FireTime(Gun g,float age,float interval)
        {
            if(g==null||age<=0)return 0;
            float rate=interval>1e-3f?Math.Max(1,Math.Min(MaxFireRate,g.FireReady/interval)):1,ready=g.FireReady/rate;
            return age<=ready?age*rate:g.FireReady+(age-ready);
        }
        /// <summary>The reload's clip time at `time`: its start on ReloadAt, its end (ReloadEnd: back on the gun) on ReloadUntil.</summary>
        public static float ReloadTime(Gun g,float time,float at,float until)
            =>g==null?0:g.ReloadEnd*Clamp01((time-at)/Math.Max(.05f,until-at));
        /// <summary>The draw's clip time `age` s into it (DrawEnd at DrawTime), or NaN outside it (age from DrawTime before the fight's
        /// start: Runtime passes the spawn-in's age plus DrawTime).</summary>
        public static float DrawClipTime(Gun g,float age)=>g?.Draw==null||float.IsNaN(age)||age<0||age>=g.DrawTime?float.NaN:g.DrawEnd*age/g.DrawTime;
        /// <summary>The raise's time (s): aiming, the gun's Settle (the rules' aim settle: pistol .15, rifle .3, machine gun .2); a shot
        /// from the hip, `quick`.</summary>
        public static float RaiseTime(Actor a,float quick)
        {
            if(a==null||!a.Aiming||!GunRules.Valid(a.Weapon)||GunRules.Get(a.Weapon)==null)return quick;
            return Math.Max(quick,GunRules.Get(a.Weapon).Settle);
        }
        /// <summary>The machine gun's kick (0..1) `age` s after a shot: up at once, gone by KickTime (its .085 s interval keeps it
        /// shaking while the trigger is held).</summary>
        public const float KickTime=.11f, KickPitch=2.4f;
        public static float Kick(Gun g,float age)=>g==null||!g.Kick||age<0||age>=KickTime?0:1-age/KickTime;

        // ---- the support hand ----
        /// <summary>The support hand lets go of the model over ReloadLetGo s after ReloadAt (the reload's free hand), takes it again over the
        /// ReloadRegrip s before ReloadUntil (the clip is back on the gun there), and joins a draw over DrawJoin of it.</summary>
        public const float ReloadLetGo=.15f, ReloadRegrip=.25f, DrawJoinFrom=.55f, DrawJoinTo=.9f;
        /// <summary>The support hand's hold on the model (0..1): the pack's share of the upper body (`packShare`: its carry, its raise), less
        /// the reload's free hand and a draw's first part (`drawAge`: s into the draw, NaN outside one). Every state the pack's layers hold
        /// the gun in (the aim standing or walking, the run's and sprint's carries, a jump, a shot, a reload's start and end) grips it.</summary>
        public static float GripWeight(Gun g,float packShare,Actor a,float time,float drawAge)
        {
            if(g?.Support==null||a==null)return 0;float w=Clamp01(packShare);
            if(a.ReloadUntil>a.ReloadAt&&time>=a.ReloadAt&&time<a.ReloadUntil)
                w*=1-Math.Min(Smooth((time-a.ReloadAt)/ReloadLetGo),Smooth((a.ReloadUntil-time)/ReloadRegrip));
            if(!float.IsNaN(drawAge)&&drawAge>=0&&drawAge<g.DrawTime&&g.DrawTime>0)w*=Smooth((drawAge/g.DrawTime-DrawJoinFrom)/(DrawJoinTo-DrawJoinFrom));
            return w;
        }
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}

        // ---- the carry on the move ----
        /// <summary>The carry's clips and their shares at `time`: the walk's ready hold on its own clock (A), the sprint's port carry by
        /// `sprint` (0..1, the stride's sprint share; B). A gun without a sprint carry fades its carry out as the sprint comes in.</summary>
        public static void Carry(Gun g,float time,float sprint,NativeVisualMotion.IClips clips,out string a,out float ta,out string b,out float tb,out float mix,out float weight)
        {
            a=b=null;ta=tb=mix=0;weight=0;if(g?.Walk==null||!clips.Has(g.Walk))return;
            float la=Math.Max(.05f,clips.Length(g.Walk));a=g.Walk;ta=time%la;if(ta<0)ta+=la;weight=1;sprint=Clamp01(sprint);
            if(g.Sprint!=null&&clips.Has(g.Sprint)){float lb=Math.Max(.05f,clips.Length(g.Sprint));b=g.Sprint;tb=time%lb;if(tb<0)tb+=lb;mix=sprint;}
            else weight=1-sprint;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}
