using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The pistol and machine-gun Q kit's pilot pose (Core/NativeGunKit; Claude's presentation of the rules proposed to Astra),
    /// laid over the gun hold on every gun rig route through RangedAbilityPose.Apply: the third-person GunMotion and the
    /// first-person view model. Fan the Hammer drops the pistol to a hip fan (third person; first person keeps it in view,
    /// lower and canted) and the off hand slaps the hammer once per fanned round: the strike lands on each round's time
    /// (FanAt + i x FanGap), follows through down and back off the spur and rises for the next; each round adds the derived
    /// recoil kick (tools/fan-hammer-provenance.json: the pack's one-handed pistol shots' gun-shoulder recoil, normalised;
    /// the strike moves at the dual-pistol volleys' measured support-hand speed), so the muzzle climbs through the volley and
    /// settles after it. Overdrive braces the machine gunner (forward lean, lower stance, the gun pulled in), shakes the hold
    /// on every round harder as the barrels heat, and through the vent tilts the gun onto its side, muzzle up, while the steam
    /// blows off. The support hand keeps its grip (or its reload choreography) relative to the moved gun. Replicated Actor
    /// fields only; no allocation per frame. Presentation only: shots, hit timing, hitboxes and movement are the simulation's.
    /// </summary>
    public static class NativeGunKitMotion
    {
        // Derived from the owner's Explosive pack (tools/derive_fan_hammer.mjs -> tools/fan-hammer-provenance.json; checked by
        // Tests/GunKitChecks): the per-round recoil kick over time since the round, and the support hand's strike speed (m/s).
        public static readonly float[] KickTimes={0,.0167f,.0333f,.05f,.0833f,.1333f,.2f,.25f,.45f};
        public static readonly float[] KickValues={.4743f,.9486f,.9484f,1,.9257f,.8362f,.6871f,.5774f,0};
        public const float StrikeSpeed=8.1233f;
        /// <summary>One round's kick at `age` seconds after it (0 before it and after the release).</summary>
        public static float Kick(float age)
        {
            if(age<0||age>=KickTimes[KickTimes.Length-1])return 0;
            for(int i=1;i<KickTimes.Length;i++)if(age<=KickTimes[i]){float u=(age-KickTimes[i-1])/(KickTimes[i]-KickTimes[i-1]);return KickValues[i-1]+(KickValues[i]-KickValues[i-1])*u;}
            return 0;
        }
        /// <summary>The fan's stacked recoil at `time` (every fired round's kick, soft-limited so a full volley climbs about 1.5).</summary>
        public static float FanRecoil(in NativeGunKit.State s,float time)
        {
            if(!s.HasFan)return 0;float sum=0;
            for(int i=0;i<s.FanShots;i++){float age=time-s.FanShot(i);if(age<0)break;sum+=Kick(age);}
            return 1.3f*(float)Math.Tanh(sum/1.3f);
        }
        // The slap in the gun's frame (source frame: +x the gun's left, +y up, +z along the barrel), metres: the palm's contact on
        // the hammer spur, the follow-through off it (down and back) and the raised hand before the next strike.
        public static readonly Vector3 Hammer=new Vector3(0,.10f,-.10f),Follow=new Vector3(0,.06f,-.2f),Raised=new Vector3(.01f,.2f,.0f);
        // The left wrist relative to the palm's contact (palm down across the slide, fingers to the gun's right, thumb forward).
        public static readonly Vector3 Wrist=new Vector3(.055f,.065f,0);
        /// <summary>The strike's share of a round's cycle: the raised-to-hammer path at the derived strike speed.</summary>
        public static float StrikeShare=>Math.Min(.45f,Vector3.Distance(Raised,Hammer)/StrikeSpeed/NativeGunKit.FanGap);
        /// <summary>
        /// The palm's contact point in the gun frame at `time`: on the hammer at each round, then the follow-through, the rise and
        /// the next strike; after the last round it rests at the follow-through (the pose weight then returns the hand).
        /// </summary>
        public static Vector3 SlapPoint(in NativeGunKit.State s,float time)
        {
            var phase=NativeGunKit.FanAt(s,time,out int shot,out float since);if(phase==NativeGunKit.FanPhase.None||shot<0)return Follow;
            float u=since/NativeGunKit.FanGap,strike=StrikeShare;bool last=shot>=s.FanShots-1;
            if(u<.3f||last)return Vector3.Lerp(Hammer,Follow,Ease(u/.3f));
            if(u<1-strike){float k=(u-.3f)/(1-strike-.3f);var p=Vector3.Lerp(Follow,Raised,Ease(k));p.Y+=.04f*(float)Math.Sin(k*Math.PI);return p;}
            float v=(u-(1-strike))/strike;return Vector3.Lerp(Raised,Hammer,v*v);
        }

        /// <summary>The layer over the posed gun hold (RangedAbilityPose.Apply); no-op without a kit cast.</summary>
        public static void Apply(CapturedRig.Pose pose,Actor a,float time,bool firstPerson)
        {
            if(a==null||a.Hp<=0)return;var s=NativeGunKit.Of(a,time);
            if(a.Weapon==WeaponId.Pistol&&s.HasFan){float w=NativeGunKit.FanWeight(s,time);if(w>1e-4f)Fan(pose,a,s,time,w,firstPerson);}
            else if(a.Weapon==WeaponId.MachineGun&&s.HasOverdrive){float w=BraceWeight(s,time),v=VentWeight(s,time);if(w>1e-4f||v>1e-4f)Drive(pose,a,s,time,w,v,firstPerson);}
        }
        /// <summary>Overdrive's brace: in over the spin-up, held while firing, out through the vent.</summary>
        public static float BraceWeight(in NativeGunKit.State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveAt)return 0;if(time<s.OverdriveUntil)return NativeGunKit.Smooth((time-s.OverdriveAt)/NativeGunKit.SpinUp);
            return 1-NativeGunKit.Smooth((time-s.OverdriveUntil)/Math.Max(.1f,s.VentUntil-s.OverdriveUntil+.3f));
        }
        /// <summary>The vent tilt: in over .12 s at the vent, held, out over .28 s after it.</summary>
        public static float VentWeight(in NativeGunKit.State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveUntil)return 0;
            if(time<s.VentUntil)return NativeGunKit.Smooth((time-s.OverdriveUntil)/.12f);
            return 1-NativeGunKit.Smooth((time-s.VentUntil)/.28f);
        }
        /// <summary>One round's shake at `age` seconds after it, and its direction seed (Overdrive's heavier hold).</summary>
        public static float Shake(float age)=>age<0||age>.2f?0:(float)Math.Exp(-age/.045f);

        // The gun hand and support hand in chest space before the layer moves them, and the gun's orientation in world.
        private static void Read(CapturedRig.Pose p,out Vector3 gun,out Quaternion gunWorld,out Vector3 supportLocal,out Quaternion supportLocalQ)
        {
            p.World();var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);
            gun=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],inv);gunWorld=p.WorldRotation[CapturedRig.RHand];
            var gq=inv*gunWorld;var gi=Quaternion.Inverse(gq);var left=Vector3.Transform(p.WorldPosition[CapturedRig.LHand]-p.WorldPosition[CapturedRig.Chest],inv);
            supportLocal=Vector3.Transform(left-gun,gi);supportLocalQ=gi*(inv*p.WorldRotation[CapturedRig.LHand]);
        }
        private static void Fan(CapturedRig.Pose p,Actor a,in NativeGunKit.State s,float time,float w,bool first)
        {
            Read(p,out var gun,out var gunWorld,out var support,out var supportQ);
            float recoil=FanRecoil(s,time),snap=0;var phase=NativeGunKit.FanAt(s,time,out int shot,out float since);if(shot>=0&&phase==NativeGunKit.FanPhase.Fan)snap=(float)Math.Exp(-since/.03f);
            // Third person: the torso squares a little behind the hip fan and rocks back with the recoil.
            if(!first)
            {
                p.Rotation[CapturedRig.Spine]*=CapturedRig.Euler(.04f*w,-.1f*w,0);
                p.Rotation[CapturedRig.Chest]*=CapturedRig.Euler(-.045f*recoil*w,-.06f*w,.02f*recoil*w);
                p.Rotation[CapturedRig.RClav]*=CapturedRig.Euler(0,-.05f*recoil*w,0);
                p.World();
            }
            Quaternion chest=p.WorldRotation[CapturedRig.Chest],inv=Quaternion.Inverse(chest),aim=inv*gunWorld;
            // The fanning hold: third person at the hip, first person lower and nearer than the sights, canted to show the spur.
            Vector3 hold=first?gun+new Vector3(0,-.035f,-.01f):new Vector3(-.11f,.1f,.32f);
            var held=aim*CapturedRig.Euler(0,0,first?-.32f:-.22f);
            var q=Quaternion.Slerp(aim,held,w)*CapturedRig.Rot(CapturedRig.X,-(first?.045f:.05f)*recoil*w-.03f*snap*w);
            Vector3 at=Vector3.Lerp(gun,hold,w),back=Vector3.Transform(Vector3.UnitZ,q),up=Vector3.Transform(Vector3.UnitY,q);
            at-=back*((first?.035f:.025f)*recoil+.015f*snap)*w;at+=up*(.012f*recoil*w);
            CapturedRig.Arm(p,false,at,q,new Vector3(-1,-.7f,-.2f));p.World();
            // The off hand: its grip on the moved gun blended to the slap (palm down across the slide, fingers to the gun's right).
            chest=p.WorldRotation[CapturedRig.Chest];inv=Quaternion.Inverse(chest);
            Vector3 reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],inv);var gq=inv*p.WorldRotation[CapturedRig.RHand];
            Vector3 grip=reached+Vector3.Transform(support,gq),contact=SlapPoint(s,time);
            Vector3 slap=reached+Vector3.Transform(contact+Wrist,gq);
            Vector3 gx=Vector3.Transform(Vector3.UnitX,gq),gy=Vector3.Transform(Vector3.UnitY,gq),gz=Vector3.Transform(Vector3.UnitZ,gq);
            var palm=CapturedRig.Basis(-gz,gx,-gy);
            var lq=Quaternion.Slerp(gq*supportQ,palm,w);
            CapturedRig.Arm(p,true,Vector3.Lerp(grip,slap,w),lq,new Vector3(1,.2f,-.6f));p.World();
        }
        private static void Drive(CapturedRig.Pose p,Actor a,in NativeGunKit.State s,float time,float w,float vent,bool first)
        {
            Read(p,out var gun,out var gunWorld,out var support,out var supportQ);
            float heat=NativeGunKit.Heat(s,time),age=time-a.LastShot,firing=time<s.OverdriveUntil?Shake(age):0;
            long seed=NativeWeaponFx.Seed(a.LastShot,a.Id);
            float nx=(float)NativeWeaponFx.Hash(seed,1)*2-1,ny=(float)NativeWeaponFx.Hash(seed,2)*2-1,nz=(float)NativeWeaponFx.Hash(seed,3);
            float amp=firing*(.6f+.8f*heat)*w*(first?1.3f:1);
            if(!first)
            {
                // The braced stance: the hips sink, the spine and chest lean into the gun; each round jolts the chest.
                p.Position[CapturedRig.Body]+=new Vector3(0,-.06f*w,-.025f*w);
                p.Rotation[CapturedRig.Spine]*=CapturedRig.Euler(.13f*w,0,0);
                p.Rotation[CapturedRig.Chest]*=CapturedRig.Euler(.05f*w-.03f*amp,.025f*nx*amp,.02f*ny*amp);
                p.World();
            }
            Quaternion chest=p.WorldRotation[CapturedRig.Chest],inv=Quaternion.Inverse(chest),aim=inv*gunWorld;
            // The gun pulled into the shoulder while braced; the vent tilts it onto its side, muzzle up, and draws it back.
            var q=aim*CapturedRig.Rot(CapturedRig.X,-.035f*amp-.32f*vent)*CapturedRig.Rot(CapturedRig.Z,.62f*vent);
            Vector3 back=Vector3.Transform(Vector3.UnitZ,aim),up=Vector3.Transform(Vector3.UnitY,aim),side=Vector3.Transform(Vector3.UnitX,aim);
            Vector3 at=gun-back*(.03f*w+.06f*vent+.02f*amp*nz)-up*(.012f*w)+up*(.012f*ny*amp)+side*(.012f*nx*amp)+up*(.05f*vent);
            CapturedRig.Arm(p,false,at,q,new Vector3(-1,-.7f,-.2f));p.World();
            chest=p.WorldRotation[CapturedRig.Chest];inv=Quaternion.Inverse(chest);
            Vector3 reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],inv);var gq=inv*p.WorldRotation[CapturedRig.RHand];
            CapturedRig.Arm(p,true,reached+Vector3.Transform(support,gq),gq*supportQ,new Vector3(1,-.8f,-.25f));p.World();
        }

        /// <summary>
        /// The first-person camera kick (degrees: pitch up, yaw) of the local pilot's kit, for the camera shake setting: every
        /// fanned round snaps the view up and it settles; Overdrive's rounds buzz it, harder as the barrels heat.
        /// </summary>
        public static void CameraKick(Actor a,float time,out float pitch,out float yaw)
        {
            pitch=yaw=0;if(a==null||a.Hp<=0)return;var s=NativeGunKit.Of(a,time);
            if(a.Weapon==WeaponId.Pistol&&s.HasFan)
            {
                for(int i=0;i<s.FanShots;i++){float age=time-s.FanShot(i);if(age<0)break;pitch+=age<.02f?age/.02f:(float)Math.Exp(-(age-.02f)/.07f);}
                pitch=1.5f*(float)Math.Tanh(pitch/1.5f)*1.6f;return;
            }
            if(a.Weapon==WeaponId.MachineGun&&s.HasOverdrive&&time>=s.OverdriveAt&&time<s.OverdriveUntil+.1f)
            {
                float k=Shake(time-a.LastShot)*(.5f+.7f*NativeGunKit.Heat(s,time));long seed=NativeWeaponFx.Seed(a.LastShot,a.Id);
                pitch=.45f*k;yaw=.35f*k*((float)NativeWeaponFx.Hash(seed,7)*2-1);
            }
        }
        private static float Ease(float u){u=NativeGunKit.Clamp01(u);return u*u*(3-2*u);}
    }
}
