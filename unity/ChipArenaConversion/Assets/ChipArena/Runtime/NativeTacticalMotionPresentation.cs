using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Third person on the Tactical Shooter Pack's clips (Core NativeTacticalMotion; docs/TP-TACTICAL.md; owner 2026-11-21): a pack gun's
    // carry on the move and in the air (the pack walk's ready hold, its sprint's port carry), and its raised aim pitched onto the look
    // with the machine gun's kick. The motion's own layers (NativeVisualMotionPresentation.cs) do the rest: the aim, the shot and the
    // reload on the upper layer, the weapon on the right hand (the pack's ready hold by the pack's share), the long guns' support fist
    // on their own handguard. Presentation only.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The carry's ease in and out (s), the most the spine pitches the aim (degrees) and the pitch's ease off for a reload.</summary>
        private const float CarryEase=.18f, TacticalPitchMax=60f, TacticalPitchEase=.15f;
        /// <summary>How far (m) the pistol's hands reach out past the pack's hold: its first-person ready hold keeps the pistol a hand's
        /// breadth before the eye (framed for its camera), which in third person read as the gun at the visor.</summary>
        public const float PistolReach=.17f;
        // The spine's share of the pitch from the pelvis up (Spine, Chest, UpperChest).
        private static readonly float[] TacticalPitchShare={.3f,.35f,.35f};

        // The carry's eased weight this frame: a pack gun on the move or in the air (the arms free: the stride, a jump's rise or fall, a
        // landing); not standing at rest (the match idles keep their own carry: owner 2026-11-10, 11-21).
        // (Also the aim's pitch and the pistol's reach easing off through a reload and back: PitchKeep.)
        private static float VisualTacticalCarryWeight(VisualMotion m,Actor a,float time,in NativeVisualMotion.Pick pick,bool covered,bool ready,float dt)
        {
            m.PitchKeep=Mathf.MoveTowards(m.PitchKeep,time>=a.ReloadAt&&time<a.ReloadUntil?0:1,dt>0?dt/TacticalPitchEase:1);
            bool on=m.Set.Tactical!=null&&covered&&ready&&(pick.Locomotion||pick.Clip!=m.Set.Idle);
            m.Carry=Mathf.MoveTowards(m.Carry,on?1:0,dt/CarryEase);
            if(m.Set.Tactical==null||m.Carry<=0){m.CarryShare=0;return 0;}
            if(pick.Locomotion)m.CarrySprint=m.LocoWeights[NativeVisualStride.Sprint];
            return m.Carry;
        }
        // The carry onto the mix's upper body (every muscle but the legs'), by its eased weight (NativeTacticalMotion.Carry: the walk's
        // hold, the sprint's by the sprint's share).
        private static void VisualTacticalCarry(VisualMotion m,in NativeVisualMotion.Pick pick,float time,float carry)
        {
            m.CarryShare=0;if(carry<=0||m.Set.Tactical==null)return;var bank=VisualMotionBank;
            NativeTacticalMotion.Carry(m.Set.Tactical,time,m.CarrySprint,bank,out string a,out float ta,out string b,out float tb,out float mix,out float weight);
            if(a==null||weight<=0||!bank.Clips.TryGetValue(a,out var ca))return;
            if(m.CarryMix==null||m.CarryMix.Length!=m.Mix.Length)m.CarryMix=new float[m.Mix.Length];
            System.Array.Clear(m.CarryMix,0,m.CarryMix.Length);float total=0;
            if(mix<1){NativeVisualMotionClips.Add(ca,ta,1-mix,m.CarryMix);total+=1-mix;}
            if(b!=null&&mix>0&&bank.Clips.TryGetValue(b,out var cb)){NativeVisualMotionClips.Add(cb,tb,mix,m.CarryMix);total+=mix;}
            if(total<1e-4f)return;NativeVisualMotionClips.Finish(m.CarryMix,total);
            float w=Ease(Mathf.Clamp01(carry))*weight;var mask=UpperMuscles;
            for(int i=0;i<mask.Length&&NativeVisualMotionClips.Head+i<m.Mix.Length;i++)if(mask[i]){int k=NativeVisualMotionClips.Head+i;m.Mix[k]+=(m.CarryMix[k]-m.Mix[k])*w;}
            m.CarryShare=w;
        }
        // A pack gun's support hand closed (owner 2026-11-21: "no open, splayed hands"; 11-22: "fingers closed and wrapping it"): the left
        // hand's finger muscles take its grip's own (NativeTacticalMotion.Grip.Hand: the long guns' fore-end wrap, the pistol's) by the grip's
        // weight (Grip: the pack's share, let go through a reload's free hand); VisualTacticalGrip then seats the hand on the model. (The
        // pack's own support hands retarget open and splayed on this body: their finger spreads run far past the game's own hands'.)
        private static int[] leftHandMuscles;
        private static void VisualTacticalFist(VisualMotion m)
        {
            if(m.Set.Tactical==null||m.Grip<=0)return;var hand=m.Set.Tactical.Support?.Hand;if(hand==null)return;
            if(leftHandMuscles==null){var n=HumanTrait.MuscleName;var names=NativeTacticalMotion.HandMuscles;leftHandMuscles=new int[names.Length];for(int i=0;i<names.Length;i++)leftHandMuscles[i]=System.Array.IndexOf(n,"LeftHand."+names[i]);}
            float w=Ease(Mathf.Clamp01(m.Grip));
            for(int i=0;i<leftHandMuscles.Length&&i<hand.Length;i++){int k=leftHandMuscles[i];if(k<0)continue;k+=NativeVisualMotionClips.Head;if(k<m.Mix.Length)m.Mix[k]+=(hand[i]-m.Mix[k])*w;}
        }
        // The support hand seated on the model (owner 2026-11-22: "think we need to fix this here"; the pack's carry left it open on the near
        // face): by `w`, the palm's frame (wrist to the middle knuckle; pinky to index) turned onto the gun's hold (NativeTacticalMotion.Grip in
        // the weapon frame: a long gun's fore-end from below, the pistol's thumbs-forward wrap), the hand's anchor point on the model's
        // measured Support (each model's and each Arsenal model's own), the arm reaching it (elbow down and out), half the wrist's new twist
        // taken by the forearm.
        private static void VisualTacticalGrip(VisualMotion m,Transform weapon,Vector3 support,Transform root,float w)
        {
            var g=m.Set.Tactical?.Support;
            if(g==null||w<=1e-4f||m.LeftHand==null||m.LeftArm==null||m.LeftForearm==null||m.LeftFist==null||m.LeftIndex==null||m.LeftLittle==null)return;
            var hand=m.LeftHand;Vector3 f=m.LeftFist.position-hand.position;float len=f.magnitude;if(len<1e-4f)return;f/=len;
            Vector3 across=m.LeftIndex.position-m.LeftLittle.position;across-=Vector3.Dot(across,f)*f;if(across.sqrMagnitude<1e-8f)return;across.Normalize();
            Vector3 tf=weapon.rotation*new Vector3(g.Fingers.X,g.Fingers.Y,g.Fingers.Z),ta=weapon.rotation*new Vector3(g.Thumb.X,g.Thumb.Y,g.Thumb.Z);
            tf.Normalize();ta-=Vector3.Dot(ta,tf)*tf;if(ta.sqrMagnitude<1e-8f)return;ta.Normalize();
            var onto=Quaternion.LookRotation(tf,ta)*Quaternion.Inverse(Quaternion.LookRotation(f,across));
            var anchor=hand.position+f*(len*g.Along)+Vector3.Cross(across,f)*g.Depth;var local=Quaternion.Inverse(hand.rotation)*(anchor-hand.position);
            var goal=Quaternion.Slerp(hand.rotation,onto*hand.rotation,w);var at=weapon.position+weapon.rotation*support;
            ArmReachTo(m.LeftArm,m.LeftForearm,hand,Vector3.Lerp(hand.position,at-goal*local,w),-root.up-root.right*.8f);
            // Half the wrist's twist about the forearm into the forearm (no wrung wrist), then the hand on its hold.
            var bone=hand.position-m.LeftForearm.position;
            if(bone.sqrMagnitude>1e-8f)
            {
                bone.Normalize();var d=goal*Quaternion.Inverse(hand.rotation);var v=new Vector3(d.x,d.y,d.z);var p=Vector3.Dot(v,bone)*bone;
                var twist=new Quaternion(p.x,p.y,p.z,d.w);float mag=Mathf.Sqrt(twist.x*twist.x+twist.y*twist.y+twist.z*twist.z+twist.w*twist.w);
                if(mag>1e-6f){twist=new Quaternion(twist.x/mag,twist.y/mag,twist.z/mag,twist.w/mag);twist.ToAngleAxis(out float deg,out var ax);if(deg>180)deg-=360;if(Vector3.Dot(ax,bone)<0)deg=-deg;
                    if(!float.IsNaN(deg))m.LeftForearm.rotation=Quaternion.AngleAxis(deg*.5f,bone)*m.LeftForearm.rotation;}
            }
            hand.rotation=goal;
        }
        // The draw's age (NativeTacticalMotion.DrawClipTime) on an arena body: from DrawTime before the fight's start (the round's GO, as the
        // spawn-in times it: NativeSpawnIn.Age) to it; NaN elsewhere (a menu body, no match).
        private float VisualTacticalDrawAge(VisualMotion m,VisualBody body,float time)
        {
            var g=m.Set.Tactical;if(g?.Draw==null||!body.Arena)return float.NaN;var match=WeaponLoadout?.Match;if(match==null)return float.NaN;
            float age=NativeSpawnIn.Age(match.Phase,match.PhaseEnd,match.FightAt,time);return float.IsNaN(age)?float.NaN:age+g.DrawTime;
        }
        // A raised pack gun onto the look's pitch, by `w` (the raise and the motion's weight): its ready hold is level (the pack pitches
        // its camera, not its hands), so the spine takes the pitch from the pelvis up (TacticalPitchShare), the head and arms with it;
        // eased off through a reload (the clip's own tilt plays). The machine gun's kick climbs the same way on each shot.
        private static void VisualTacticalPitch(VisualMotion m,Transform root,Actor a,float time,float w)
        {
            float deg=Mathf.Clamp(a.Pitch,-TacticalPitchMax,TacticalPitchMax)*m.PitchKeep*w+NativeTacticalMotion.Kick(m.Set.Tactical,time-a.LastShot)*NativeTacticalMotion.KickPitch*w;
            if(Mathf.Abs(deg)<1e-3f)return;
            var right=root.right;Transform[] chain={m.Spine,m.Chest,m.UpperChest};
            for(int i=0;i<chain.Length;i++)if(chain[i]!=null)chain[i].rotation=Quaternion.AngleAxis(-deg*TacticalPitchShare[i],right)*chain[i].rotation;
        }
        // The pistol held out (PistolReach) while the pack's hold has it (raised or carried), both hands together along the look (raised) or
        // the body's facing (carried), the elbows down and out; eased off through a reload (its hands go to the belt). By `w`.
        private static void VisualTacticalReach(VisualMotion m,Transform root,Actor a,float w)
        {
            if(m.Set.Tactical!=NativeTacticalMotion.Pistol||w<=1e-4f||m.RightArm==null||m.RightForearm==null||m.LeftArm==null||m.LeftForearm==null||m.LeftHand==null)return;
            float raised=Mathf.Clamp01(m.Upper);var along=Vector3.Slerp(root.forward,Quaternion.Euler(-a.Pitch,a.Yaw,0)*Vector3.forward,raised);
            var push=along.normalized*(PistolReach*w*m.PitchKeep);if(push.sqrMagnitude<1e-8f)return;var up=root.up;var side=root.right;
            Quaternion rh=m.RightHand.rotation,lh=m.LeftHand.rotation;
            ArmReachTo(m.RightArm,m.RightForearm,m.RightHand,m.RightHand.position+push,-up+side*.6f);m.RightHand.rotation=rh;
            ArmReachTo(m.LeftArm,m.LeftForearm,m.LeftHand,m.LeftHand.position+push,-up-side*.6f);m.LeftHand.rotation=lh;
        }
    }
}
