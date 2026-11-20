using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>An intro's grip at a moment (docs/INTROS-V2.md 9a "Grip IK"): the off hand on its two-handed weapon.</summary>
    public struct NativeIntroGripAt
    {
        /// <summary>0: the off arm as keyed .. 1: the off hand on the weapon (blend it in and out over a few frames, as a key would).</summary>
        public float Weight;
        /// <summary>Where the off elbow points, in the chest's frame (+x the pilot's left, +y up the chest, +z ahead); zero: the weapon's own
        /// (<see cref="NativeIntroGrip.Pole"/>: down and out, the bow's string elbow back and high).</summary>
        public Vector3 Pole;
        /// <summary>Metres along the weapon from its support point (+ toward the muzzle, the tip or the staff's head): a hand further up a staff,
        /// a shorter hold on a handguard.</summary>
        public float Along;
        /// <summary>The bow's draw, 0 at rest .. 1 full: the string hand on the nock (NativeBow.NockAt). Keep it the pull your Bow view gives.</summary>
        public float Pull;
        /// <summary>A long gun shouldered on the drawn model, 0..1: its stock's butt in the model's shoulder pocket while the keyed butt is at the
        /// keyed shoulder (an aim; NativeVisualRig's shouldering, as the match's aim). 0: held where the keyed hand has it (a carry).</summary>
        public float Shoulder;
        public NativeIntroGripAt(float weight,Vector3 pole=default,float along=0,float pull=0,float shoulder=0){Weight=weight;Pole=pole;Along=along;Pull=pull;Shoulder=shoulder;}
    }

    /// <summary>
    /// Opt-in for a v2 weapon intro (docs/INTROS-V2.md 9a): the off hand on a two-handed weapon. The keyed pose takes it after Arms
    /// (NativeProceduralEntrance.Pose calls <see cref="NativeIntroGrip.Solve"/>; an intro that overrides Pose calls Grip itself), and the
    /// runtime puts the drawn off fist on the drawn weapon's support point (<see cref="NativeIntroGrip.Grasp"/>). Pure in `t`.
    /// </summary>
    public interface INativeIntroGrip{bool GripAt(float t,out NativeIntroGripAt grip);}

    /// <summary>
    /// Intros v2's weapon grip IK (owner 2026-11-20 on One Breath: "arms looked really odd": the front hand hung limp under the barrel, the
    /// arms tangled on the aim and the end crossed them holding nothing). Two levels, as the match holds its weapons:
    /// 1. **The keyed pose** (Core, checked by `--intro-limits`): the off arm solved onto the weapon held in the other hand, in the match's own
    ///    convention for that weapon (RangedPose.Arms's support hand, the two-hander's second grip, the staff's guard grip, NativeBowPose's
    ///    string hand), the elbow toward a pole in the chest's frame, the hand's roll carried by the forearm (<see cref="Forearm"/>), never
    ///    the wrist or the upper arm.
    /// 2. **The drawn model** (Runtime VisualBodyBind through <see cref="Grasp"/>): the model's off fist closed on the drawn weapon's support
    ///    point (a long gun's handguard, a two-hander's lower grip, the shield's rim, a staff's shaft, the bow's string at the nock) from the
    ///    drawn weapon frame, its elbow on the side the keyed elbow gave it. Without this the model copied the keyed off arm turn for turn and
    ///    its hand, on the model's shorter arms and narrower shoulders, missed the weapon by 10-20 cm.
    /// The main hand stays the author's (keys, Reach, or <see cref="Hold"/>). Allocation-free; main thread (no shared scratch).
    /// </summary>
    public static class NativeIntroGrip
    {
        /// <summary>The two-handed weapons' families.</summary>
        public enum Kind:byte{None,LongGun,Pistol,TwoHand,Staff,Bow,Shield}
        public static Kind Of(string spec)
        {
            switch(spec){case "rifle":case "machinegun":return Kind.LongGun;case "pistol":return Kind.Pistol;case "greatsword":return Kind.TwoHand;case "shield":return Kind.Shield;}
            if(spec==null)return Kind.None;if(spec==NativeKatana.Spec)return Kind.TwoHand;if(NativeWeaponDressing.Staff(spec))return Kind.Staff;if(NativeBowWire.Is(spec))return Kind.Bow;
            return Kind.None;
        }
        /// <summary>Whether `spec`'s off hand is the left (the guns, the two-handers and the shield are held in the right; a staff and the bow
        /// in the left, so their off hand is the right).</summary>
        public static bool SupportLeft(Kind k)=>k==Kind.LongGun||k==Kind.Pistol||k==Kind.TwoHand||k==Kind.Shield;
        /// <summary>A staff's second grip up its shaft from the main fist (NativeWeaponModels: the staff guard-grip at .40).</summary>
        public const float StaffGrip=.40f;
        /// <summary>The shield's rim where the off fist braces it (its left edge, upper third, just behind the face; source weapon frame).</summary>
        public static readonly Vector3 ShieldRim=new Vector3(.25f,.18f,.17f);
        /// <summary>The weapon's own elbow pole for the off arm (chest frame): down and out, as RangedPose.Arms poses the support elbow; the
        /// bow's string elbow back and high (NativeBowPose.Arms at full draw).</summary>
        public static Vector3 Pole(Kind k)=>k==Kind.Bow?Vector3.Normalize(new Vector3(-.15f,.55f,-1)):SupportLeft(k)?new Vector3(1,-.8f,-.25f):new Vector3(-1,-.8f,-.25f);

        /// <summary>
        /// Where the keyed off wrist goes in the main wrist's frame (the keyed rig's weapon frame) and its turn there (false: no support): the
        /// match's own conventions, so the drawn support (Core NativeVisualRig's support grip, tuned on them) meets the same input as in play.
        /// The long guns' support wrist .10 m up the bore and the pistol's under the shooting fist (RangedPose.Support), turned as the gun;
        /// the two-hander's .11 m down its hilt (CapturedRig.Sample's set 2); the staff's up the shaft; the bow's string hand on the nock
        /// (NativeBowPose: the nock less its hook). The shield's fist is placed on its rim by the forearm (Solve).
        /// </summary>
        public static bool Keyed(Kind k,string spec,float along,float pull,out Vector3 at,out Quaternion turn)
        {
            turn=Quaternion.Identity;at=Vector3.Zero;
            switch(k)
            {
                case Kind.LongGun:at=RangedPose.Support(spec=="machinegun"?WeaponId.MachineGun:WeaponId.Rifle)+new Vector3(0,0,along);return true;
                case Kind.Pistol:at=RangedPose.Support(WeaponId.Pistol)+new Vector3(0,0,along);return true;
                case Kind.TwoHand:at=new Vector3(0,0,-.11f+along);return true;
                case Kind.Staff:at=new Vector3(0,0,StaffGrip+along);return true;
                case Kind.Bow:at=NativeBow.NockAt(pull)-NativeBowModels.Hook;return true;
                case Kind.Shield:at=ShieldRim+new Vector3(0,along,0);return true;
            }
            return false;
        }
        /// <summary>
        /// The hold the drawn off fist closes on, in the main hand's drawn weapon frame (Core NativeVisualRig.Hold): the long gun's handguard or
        /// shroud cupped from below and the pistol's fist (NativeVisualRig.GunSupport), the two-hander's lower grip (GreatswordSupport; the
        /// Katana's at its own support socket), the shield's rim, a staff's shaft, the bow's string at the nock.
        /// </summary>
        public static bool Model(Kind k,string spec,float along,float pull,CapturedRig.Pose pose,out NativeVisualRig.Hold hold)
        {
            hold=default;
            switch(k)
            {
                case Kind.LongGun:hold=NativeVisualRig.GunSupport(spec=="machinegun"?WeaponId.MachineGun:WeaponId.Rifle,pose);hold.Center+=hold.Axis*along;return true;
                case Kind.Pistol:hold=NativeVisualRig.GunSupport(WeaponId.Pistol,pose);return true;
                case Kind.TwoHand:hold=spec==NativeKatana.Spec?new NativeVisualRig.Hold(NativeWeaponModels.KatanaSupport+new Vector3(0,0,along),CapturedRig.Z):NativeVisualRig.GreatswordSupport(Quaternion.Identity);if(spec!=NativeKatana.Spec)hold.Center+=new Vector3(0,0,along);return true;
                case Kind.Staff:hold=new NativeVisualRig.Hold(new Vector3(0,0,StaffGrip+along),CapturedRig.Z);return true;
                case Kind.Bow:hold=new NativeVisualRig.Hold(NativeBow.NockAt(pull),CapturedRig.Y);return true;
                case Kind.Shield:hold=new NativeVisualRig.Hold(ShieldRim+new Vector3(0,along,0),CapturedRig.Y);return true;
            }
            return false;
        }

        /// <summary>
        /// The keyed rig's weapon convention: the anatomical hand under a keyed wrist that holds (or supports) `k`'s weapon, keyed = anatomical
        /// x Natural. A gun's, the bow's and the shield's frames lie with their +z (bore, arrow, face) where the hand points and their +y up its
        /// grip, riser or bar (the fist's axis): a quarter turn about x from the hand (the drawn hand eases it: NativeVisualRig.Wrist). A blade's
        /// and a staff's run through the fist along the hand's +z: none. The shield's bracing fist is placed anatomically: none.
        /// </summary>
        public static Quaternion Natural(Kind k,bool supporting)
        {
            // (measured on the match's own holds, RangedPose.Arms and NativeBowPose.Arms: the support hands turned as the gun cup it from below
            // and the side; the bow fist wraps the riser, its knuckles to the right; the string hand hooks the string, its back out)
            if((k==Kind.LongGun||k==Kind.Pistol)&&supporting)return Cup;
            if(k==Kind.Bow)return supporting?String:Riser;
            if(k==Kind.LongGun||k==Kind.Pistol||k==Kind.Shield&&!supporting)return QuarterX;
            return Quaternion.Identity;
        }
        private static readonly Quaternion QuarterX=CapturedRig.Rot(CapturedRig.X,(float)Math.PI/2),Cup=CapturedRig.Basis(CapturedRig.Y,CapturedRig.Z,CapturedRig.X),
            Riser=CapturedRig.Basis(CapturedRig.Y,CapturedRig.X,-CapturedRig.Z),String=CapturedRig.Basis(-CapturedRig.Z,CapturedRig.X,-CapturedRig.Y);

        /// <summary>The forearm's own roll range the keyed hand may carry (rad): the rest stays at the wrist (and `--intro-limits` reports it).</summary>
        public const float ForearmMost=95*(float)Math.PI/180;
        /// <summary>
        /// Moves arm `left`'s wrist roll into its forearm (the keyed elbow's own twist about the forearm, which the model's forearm and its twist
        /// bones carry), up to `most`: a palm turned by the forearm, as a body turns it, not by a twisted wrist or upper arm. The hand's place
        /// and turn are unchanged. `natural`: the hand's weapon convention (<see cref="Natural"/>). Returns the roll moved (rad).
        /// </summary>
        public static float Forearm(CapturedRig.Pose pose,bool left,Quaternion natural,float most=ForearmMost)
        {
            int e=left?CapturedRig.LElbow:CapturedRig.RElbow,h=e+1;
            var wrist=pose.Rotation[h];var anatomical=Quaternion.Normalize(wrist*Quaternion.Inverse(natural));
            float roll=2*(float)Math.Atan2(anatomical.Y,anatomical.W);while(roll>Math.PI)roll-=2*(float)Math.PI;while(roll<-Math.PI)roll+=2*(float)Math.PI;
            roll=Math.Max(-most,Math.Min(most,roll));if(Math.Abs(roll)<1e-6f)return 0;
            var turn=CapturedRig.Rot(CapturedRig.Y,roll);
            pose.Rotation[e]=Quaternion.Normalize(pose.Rotation[e]*turn);pose.Rotation[h]=Quaternion.Normalize(Quaternion.Inverse(turn)*wrist);
            return roll;
        }

        /// <summary>
        /// The main hand onto its weapon in the chest's frame, as the match keys its own holds (RangedPose.Arms): the wrist (the weapon frame's
        /// origin) at `at` (from the chest joint: +x the chest's left, +y up it, +z ahead of it), the weapon turned `turn` (chest frame: its +z
        /// the bore, blade or shaft), the elbow toward `pole` (chest frame: down and out is (-1, -.7, -.2) for the right), blended in by
        /// `weight`, the hand's roll carried by the forearm. Key in the keyed rig's proportions (its shoulders .18 m over the chest joint, its
        /// head joint .42 m), never at heights measured on the model: the model copies the turns. `spec` names the weapon's convention.
        /// </summary>
        public static void Hold(CapturedRig.Pose pose,bool left,string spec,Vector3 at,Quaternion turn,Vector3 pole,float weight=1)
        {
            if(weight<=.001f)return;pose.World();int arm=left?CapturedRig.LArm:CapturedRig.RArm;
            var a0=pose.Rotation[arm];var e0=pose.Rotation[arm+1];var h0=pose.Rotation[arm+2];
            CapturedRig.Arm(pose,left,at,Quaternion.Normalize(turn),pole);Forearm(pose,left,Natural(Of(spec),false));Blend(pose,arm,a0,e0,h0,weight);pose.World();
        }
        /// <summary>A world point (source frame) in the chest's frame, for Hold: what a target in the world is to the chest this frame.</summary>
        public static Vector3 ChestPoint(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,Vector3 world)
        {
            pose.World();var local=Vector3.Transform(world-root,Quaternion.Inverse(rotation));
            return Vector3.Transform(local-pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]));
        }
        /// <summary>A turn in the chest's frame whose +z points `yaw` (rad, + toward the pilot's left) and `pitch` (rad, + up) off the pilot's own
        /// facing (the keyed root's), its +y as near up as that allows, rolled `roll` (rad) about its +z: an aim, a blade's line, a shield's face.</summary>
        public static Quaternion Aim(CapturedRig.Pose pose,float yaw,float pitch,float roll=0)
        {
            pose.World();var q=CapturedRig.Rot(CapturedRig.Y,yaw)*CapturedRig.Rot(CapturedRig.X,-pitch)*CapturedRig.Rot(CapturedRig.Z,roll);
            return Quaternion.Normalize(Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest])*q);
        }

        /// <summary>
        /// The keyed off arm onto the weapon in the other hand (rig frame; Arms has placed the main hand): `grip` at its weight, its pole, its
        /// place along the weapon and the bow's pull. Returns how far the wrist is from where the weapon wants it (m: past the arm's reach, the
        /// hand stops short on the line from the shoulder; the drawn model, whose arm takes the drawn weapon, closes the rest).
        /// </summary>
        public static float Solve(CapturedRig.Pose pose,string spec,in NativeIntroGripAt grip)
        {
            var k=Of(spec);float w=Math.Max(0,Math.Min(1,grip.Weight));if(k==Kind.None||!(w>.001f))return 0;
            bool left=SupportLeft(k);int main=left?CapturedRig.RHand:CapturedRig.LHand,arm=left?CapturedRig.LArm:CapturedRig.RArm;
            pose.World();var rm=pose.WorldRotation[main];var pm=pose.WorldPosition[main];
            var pole=grip.Pole.LengthSquared()>1e-6f?grip.Pole:Pole(k);
            var a0=pose.Rotation[arm];var e0=pose.Rotation[arm+1];var h0=pose.Rotation[arm+2];
            Keyed(k,spec,grip.Along,grip.Pull,out var at,out var rel);var target=pm+Vector3.Transform(at,rm);Quaternion turn;
            if(k==Kind.Shield)
            {
                // The bracing fist closes round the rim: its axis along the rim, the hand pointed as near the forearm as that allows (the
                // straightest wrist), its grip point (the voxel fist's socket) on the rim; three passes as the elbow settles.
                var axis=Vector3.Normalize(Vector3.Transform(CapturedRig.Y,rm));turn=Look(axis,Vector3.UnitY);
                for(int pass=0;pass<3;pass++)
                {
                    var forearm=pose.WorldPosition[arm+2]-pose.WorldPosition[arm+1];var y=-(forearm-axis*Vector3.Dot(forearm,axis));
                    if(y.LengthSquared()<1e-8f)y=Vector3.Cross(axis,Vector3.UnitX);y=Vector3.Normalize(y);turn=CapturedRig.Basis(Vector3.Cross(y,axis),y,axis);
                    Reach(pose,left,target-Vector3.Transform(NativeIntroRules.HandSocket,turn),turn,pole);pose.World();
                }
            }
            else{turn=Quaternion.Normalize(rm*rel);Reach(pose,left,target,turn,pole);}
            Forearm(pose,left,Natural(k,true));
            pose.World();float miss=Vector3.Distance(pose.WorldPosition[arm+2],k==Kind.Shield?target-Vector3.Transform(NativeIntroRules.HandSocket,turn):target);
            Blend(pose,arm,a0,e0,h0,w);pose.World();return miss;
        }

        /// <summary>
        /// The drawn model's grasp for a v2 weapon intro with its grip on (Runtime VisualBodyBind, and the managed checks): the main hand's hold
        /// from the intro's weapon (not the pilot's own: any pilot may pick any weapon's intro), and the off fist on the drawn weapon's support
        /// point whatever the hands' distance (Bound): the left through the match's support grip, the right (a staff, the bow) through its
        /// mirror (NativeVisualRig.Grasp.RightSupport). No grip, or none on: the grasp untouched.
        /// </summary>
        public static void Grasp(string spec,in NativeIntroGripAt grip,CapturedRig.Pose pose,ref NativeVisualRig.Grasp g)
        {
            var k=Of(spec);float w=Math.Max(0,Math.Min(1,grip.Weight));if(k==Kind.None||!(w>.001f)||!Model(k,spec,grip.Along,grip.Pull,pose,out var hold))return;
            switch(k)
            {
                case Kind.LongGun:case Kind.Pistol:
                {
                    var id=spec=="pistol"?WeaponId.Pistol:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Rifle;
                    g.RightGrip=1;g.Right=NativeVisualRig.PistolGrip(id);g.Stock=NativeVisualRig.Stock(id);g.Unshoulder=1-Math.Max(0,Math.Min(1,grip.Shoulder));break;
                }
                case Kind.TwoHand:g.RightGrip=1;g.Right=spec==NativeKatana.Spec?NativeVisualRig.Blade(Quaternion.Identity):NativeVisualRig.Melee(spec,Quaternion.Identity);break;
                case Kind.Shield:g.RightGrip=1;g.Right=NativeVisualRig.Melee(spec,Quaternion.Identity);break;
                case Kind.Staff:g.LeftGrip=1;g.Left=NativeVisualRig.StaffShaft;g.LeftCarries=true;break;
                case Kind.Bow:g.LeftGrip=1;g.Left=NativeVisualRig.BowRiser;g.LeftCarries=true;break;
            }
            if(SupportLeft(k)){g.Support=w;g.SupportHold=hold;g.Bound=true;g.LeftGrip=Math.Max(g.LeftGrip,w);}
            else{g.RightSupport=w;g.RightSupportHold=hold;g.RightGrip=Math.Max(g.RightGrip,w);}
            // (the drawn elbow to the pole's side, as the keyed one: a nearly straight keyed arm leaves it to the model's own choice)
            g.SupportPole=grip.Pole.LengthSquared()>1e-6f?grip.Pole:Pole(k);
        }

        // ---- the keyed arm IK ----
        // Arm `left`'s wrist to `wrist` (rig frame) with the world turn `turn` (rig frame), the elbow toward `pole` (chest frame): CapturedRig.Arm
        // in the chest's frame.
        private static void Reach(CapturedRig.Pose pose,bool left,Vector3 wrist,Quaternion turn,Vector3 pole)
        {
            pose.World();var chest=pose.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);
            CapturedRig.Arm(pose,left,Vector3.Transform(wrist-pose.WorldPosition[CapturedRig.Chest],inv),Quaternion.Normalize(inv*turn),pole);
        }
        private static void Blend(CapturedRig.Pose pose,int arm,Quaternion a0,Quaternion e0,Quaternion h0,float w)
        {
            if(w>=.999f)return;
            pose.Rotation[arm]=Quaternion.Slerp(a0,pose.Rotation[arm],w);pose.Rotation[arm+1]=Quaternion.Slerp(e0,pose.Rotation[arm+1],w);pose.Rotation[arm+2]=Quaternion.Slerp(h0,pose.Rotation[arm+2],w);
        }
        /// <summary>A turn whose +z is `forward` and +y as near `up` as that allows.</summary>
        public static Quaternion Look(Vector3 forward,Vector3 up)
        {
            var z=forward.LengthSquared()>1e-10f?Vector3.Normalize(forward):Vector3.UnitZ;var x=Vector3.Cross(up,z);
            if(x.LengthSquared()<1e-8f)x=Vector3.Cross(Math.Abs(z.Y)<.9f?Vector3.UnitY:Vector3.UnitX,z);x=Vector3.Normalize(x);return CapturedRig.Basis(x,Vector3.Cross(z,x),z);
        }
    }
}
