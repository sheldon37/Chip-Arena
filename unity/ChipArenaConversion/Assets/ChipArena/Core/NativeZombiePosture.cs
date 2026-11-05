using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The zombies' motion on the player's model (owner 2026-10-29: "the idle; they stand "unnaturally" like bending their lower
    // back weird", "What we were using for the voxel ones were working, but it doesn't seem to translate well for the player
    // models"; docs/ZOMBIE-MODELS.md "Motion on the model"). The gait (Core/NativeHordeMotion) poses the voxel rig: one rigid block
    // per joint, a torso .615 m from pelvis to neck on .50 m legs. The model (Core/NativeVisualRig) is a human: a .50 m torso on
    // .85 m legs, its spine five bones. Three of the gait's voxel-era choices read wrong there, and this fits them for the model
    // only, on a copy of the gait's pose (the rig, the voxel tiers, the gore, the hit volumes and the authority keep the gait's):
    // - Fit, the spine: the capture's lumbar and lower thoracic bend both sit on the rig's Spine joint (the retarget maps Spine and
    //   Spine1 there), which the model spreads over spine_01 and spine_02, a hand's width above the pelvis; a hunched idle hinged
    //   at the belt. SpineUp of that bend moves up onto the Chest joint (spine_03 to spine_05), so the back rounds where a
    //   hunched back rounds; the chest, neck, head and arms keep their world turn, so the gaze and the reach are the gait's.
    // - Fit, the arms: the gait sets the narrow voxel bodies' clavicles in (NativeHordeGait.ShoulderInset) and then reaches the
    //   hands from there, which angles each arm out by the inset; the model copies arm directions from its own shoulders, so the
    //   arms splayed. Each upper arm turns back to the capture's direction (from the shoulder the reach was planned from).
    // - Stance, the hips: the gait sits the hips back to keep the face over the capsule (Balance) and the binding scales the
    //   pelvis's whole offset by the legs' ratio (1.71), so the model's hips sat 1.7 times as far back and its knees bent near
    //   50 degrees at rest (the capture's hips .025 under its standing height, and the gait's floor .015 over the model's soles,
    //   both scaled the same). In a stride the gait also sinks the hips so the short voxel legs reach the feet (Sunk); the
    //   model's legs reach a stride 1.7 times their length short of them (its feet are the gait's, its legs longer), so on the
    //   model that sink only folds the knees. The model takes the offset across at the game's metres (Across), its hips raised
    //   by those three (Stand) as far as each planted leg reaches with room to bend, and a legless crawl at the game pelvis's own
    //   height (Level).
    // - Stance, the legs (on the PC, 2026-10-29: the walks still read as a squat): the takes were acted on a person's legs and
    //   bound to the voxel's as a share of their length, idle, walk and run alike, and the gait sits a near add lower still (its
    //   alert); on the model's legs those shares bent the planted knees 34 to 45 degrees in a stride (a person's is 5 to 20).
    //   The model's hips also stand up out of the takes' own crouch (NativeHordeGait.Crouch) and by Lift more, as far as the
    //   planted legs reach (Room), except where a blow, a reel, a roar or a keyed move owns the pose (NativeHordeGait.Busy):
    //   those crouches are the gait's to show.
    // Presentation only: no timing, reach, volume or rule changes; the blows still land on the gait's contact frames.
    public static class NativeZombiePosture
    {
        /// <summary>The share of the gait's Spine-joint bend the model carries in its upper back (spine_03 to spine_05).</summary>
        public const float SpineUp=.6f;

        /// <summary>The gait's pose `from` (world positions current) fitted for the model into `to`; `arms` off keeps the gait's own
        /// arms (the Hound's: its hands are planted where the gait put them, NativeVisualRig.State.Hands).</summary>
        public static void Fit(CapturedRig.Pose from,CapturedRig.Pose to,bool arms=true)
        {
            int n=CapturedRig.Bones.Length;
            for(int i=0;i<n;i++){to.Position[i]=from.Position[i];to.Rotation[i]=from.Rotation[i];}
            // The spine's bend up the back: the chest's world turn is the gait's (Spine' Chest' = Spine Chest).
            var spine=from.Rotation[CapturedRig.Spine];
            to.Rotation[CapturedRig.Spine]=Quaternion.Normalize(Quaternion.Slerp(Quaternion.Identity,spine,1-SpineUp));
            to.Rotation[CapturedRig.Chest]=Quaternion.Normalize(Quaternion.Slerp(Quaternion.Identity,spine,SpineUp)*from.Rotation[CapturedRig.Chest]);
            to.World();
            if(arms)for(int side=0;side<2;side++)Arm(to,side==0);
        }
        /// <summary>An upright kind's stance (the Smiler's, the Lifeguard's: tall, still figures, not the takes' hunched zombie): the
        /// spine, chest, neck and head turned `share` of the way back to the rig's rest (upright, the gaze level); the arms ride
        /// the chest. The caller eases it off while a blow owns the pose.</summary>
        public static void Upright(CapturedRig.Pose p,float share)
        {
            if(share<=0)return;share=Math.Min(1,share);
            foreach(int j in new[]{CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head})p.Rotation[j]=Quaternion.Normalize(Quaternion.Slerp(p.Rotation[j],Quaternion.Identity,share));
            p.World();
        }
        /// <summary>A staff carried upright at the right side (the Lifeguard's hook at rest), weighted `k`: the right hand beside
        /// the hip and a little ahead (StaffHand from the pelvis, rig frame), its grip's axis (the hand's -x, along which the
        /// drawn prop runs) straight up, the fingers round it toward the front; the elbow out and back.</summary>
        public static void StaffCarry(CapturedRig.Pose p,float k)
        {
            if(k<=0)return;k=Math.Min(1,k);p.World();
            var target=p.WorldPosition[CapturedRig.Pelvis]+StaffHand;var r0=p.WorldRotation[CapturedRig.RHand];var up=Vector3.UnitY;
            var q=CapturedRig.FromTo(Vector3.Normalize(Vector3.Transform(-Vector3.UnitX,r0)),up)*r0;
            var fingers=Vector3.Transform(-Vector3.UnitY,q);var flat=new Vector3(fingers.X,0,fingers.Z);
            if(flat.LengthSquared()>1e-4f){flat=Vector3.Normalize(flat);float a=MathF.Atan2(Vector3.Cross(flat,Vector3.UnitZ).Y,Vector3.Dot(flat,Vector3.UnitZ));q=Quaternion.CreateFromAxisAngle(up,a)*q;}
            for(int i=0;i<3;i++)staffWas[i]=p.Rotation[CapturedRig.RArm+i];
            var inv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            CapturedRig.Arm(p,false,Vector3.Transform(target-p.WorldPosition[CapturedRig.Chest],inv),Quaternion.Normalize(inv*q),new Vector3(-.8f,-.4f,-.3f));
            for(int i=0;i<3;i++)p.Rotation[CapturedRig.RArm+i]=Quaternion.Normalize(Quaternion.Slerp(staffWas[i],p.Rotation[CapturedRig.RArm+i],k));
            p.World();
        }
        /// <summary>Where the staff hand rests from the pelvis (rig frame: +x the body's left, +z ahead, m).</summary>
        public static readonly Vector3 StaffHand=new Vector3(-.31f,.08f,.16f);
        [ThreadStatic] private static Quaternion[] staffWasStore;
        private static Quaternion[] staffWas=>staffWasStore??=new Quaternion[3];
        // An arm reached from an inset clavicle, turned about its shoulder to the direction from the shoulder its reach was
        // planned from (the clavicle at the rig's ClavicleX); its elbow and hand keep their own bend.
        private static void Arm(CapturedRig.Pose p,bool left)
        {
            int clav=left?CapturedRig.LClav:CapturedRig.RClav,arm=clav+1,hand=clav+3;
            float inset=NativeHordeGait.ClavicleX-Math.Abs(p.Position[clav].X);if(inset<=1e-4f)return;
            Vector3 shoulder=p.WorldPosition[arm],at=p.WorldPosition[hand],planned=shoulder+Vector3.Transform(new Vector3(left?inset:-inset,0,0),p.WorldRotation[CapturedRig.Bones[clav].Parent]);
            Vector3 now=at-shoulder,was=at-planned;if(now.LengthSquared()<.0025f||was.LengthSquared()<.0025f)return;
            var turn=CapturedRig.FromTo(Vector3.Normalize(now),Vector3.Normalize(was));
            p.Rotation[arm]=Quaternion.Normalize(Quaternion.Inverse(p.WorldRotation[clav])*turn*p.WorldRotation[arm]);
            p.World();
        }

        /// <summary>How far past the gait's own lowering the model's hips may stand (m) while no blow, reel, roar or keyed move owns
        /// the pose: the planted legs' reach is what stops them (NativeVisualRig.State.Room), so standing and in a stride the knees
        /// keep about 16 degrees of bend; the Hulk's room keeps about 30 (its heavy stance).</summary>
        public const float Lift=.08f,HulkRoom=.965f;
        /// <summary>The Hulk's hips carried on a spring of this time (seconds; NativeVisualRig.State.Ease): its heavy stride and stamp
        /// drop its gait's hips with each foot, and riding its legs' reach they jolted (owner 2026-10-30: "too wiggly").</summary>
        public const float HulkCarry=.07f;

        /// <summary>The model's stance for an add of `kind` (crawling: its legs are gone) from its gait this pose (NativeHordeGait
        /// Sunk, Crouch, Busy and LegsNow, `stride`, and Outreach, `reach`; none for a pose the gait did not make) on its binding's state.</summary>
        public static void Stance(NativeVisualRig.State s,int kind,bool crawl,float sunk,float crouch,float busy,float stride=1,float reach=0)
        {
            // The Hound on all fours: its hips at the gait's height times the legs' ratio (not raised over its planted feet), its
            // feet and hands planted where the gait has them (NativeVisualRig.State.Hands).
            s.Hands=NativeBackroomsMotion.Quad(kind);s.GameFloor=-NativeHordeKinds.Root[NativeHordeKinds.Index(kind)];s.HandReach=s.Hands?reach:0;
            if(s.Hands){s.Across=true;s.Level=false;s.Follow=true;s.Ease=0;s.Stride=1;s.Room=NativeVisualRig.KneeRoom;s.Stand=0;return;}
            s.Across=true;s.Level=crawl;s.Follow=!crawl;s.Ease=kind==NativeHordeKinds.Hulk?HulkCarry:0;s.Stride=crawl||!(stride>1)?1:stride;s.Room=kind==NativeHordeKinds.Hulk?HulkRoom:NativeVisualRig.KneeRoom;
            // (The gait's floor sits Root under the rig root; the model's soles NativeVisualRig.Floor under it.)
            float gap=Math.Max(0,-NativeVisualRig.Floor-NativeHordeKinds.Root[NativeHordeKinds.Index(kind)]);
            // (On the model's own stride, LegsNow past 1, the hips stand where the capture's feet reach the floor, its own crouch kept:
            // `crouch` is then that rise, NativeZombieMotion.FloorRise. The rest stand up out of the take's crouch as before: the Hulk.)
            if(stride>1){s.Stand=crawl?0:Math.Max(0,(Math.Max(0,sunk)+crouch)*s.Rig.LegRatio+gap);return;}
            s.Stand=crawl?0:(NativeZombieClips.HipsBelow+Math.Max(0,sunk)+Math.Max(0,crouch))*s.Rig.LegRatio+gap+Lift*(1-Math.Max(0,Math.Min(1,busy)));
        }
    }
}
