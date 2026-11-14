using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A reaction clip of a pack set (Core/NativePackClips.Generated, tools/export_pack_reactions.mjs SLOTS order).</summary>
    public enum PackSlot{None=-1,HitF=0,HitF2=1,HitL=2,HitR=3,Stun=4,Block=5,Block2=6,Guard=7}

    /// <summary>
    /// Which of the owner's Explosive pack reaction clips (retargeted offline by tools/export_pack_reactions.mjs) a struck
    /// pilot plays, and when. Presentation only: pure functions of the replicated reaction, stun end and block, never read by
    /// authority, no allocation. The native hit layer (Core/NativeHitReact) lays the chosen clip over the route's pose and
    /// keeps its procedural accent as the fallback and as the weight and exact-direction modulation on top.
    /// </summary>
    public static class NativePackRules
    {
        public const int None=-1,Sword=0,Daggers=1,Greatsword=2,Shield=3,Unarmed=4;
        /// <summary>
        /// The reaction set closest to the victim's weapon style: the Armed (one-handed sword) set for the sword, the same set
        /// in a reverse grip for the daggers, 2Hand-Sword for the greatsword, the Armed body with the shield set's own block
        /// hits for the shield, Unarmed for the DEV fighters' fists. Gun and magic pilots keep the procedural layer (None):
        /// their routes hold the weapon in a native aim pose that the pack's body reactions bend off its sight line.
        /// </summary>
        public static int SetFor(string spec,HitVictim kind)
        {
            if(kind==HitVictim.Fighter)return Unarmed;
            if(kind!=HitVictim.Melee)return None;
            switch(spec){case "sword":return Sword;case "daggers":return Daggers;case "greatsword":return Greatsword;case "shield":return Shield;default:return None;}
        }
        /// <summary>Whether a hit clip's weapon hand follows the capture while stunned: the weapon hands and the fists; the
        /// shield keeps its guard arm (the Armed body reactions hold a sword in that hand), only its free hand follows.</summary>
        public static bool MainHand(int set)=>set==Sword||set==Daggers||set==Greatsword||set==Unarmed;
        /// <summary>
        /// The directional clip for a blow pushing the body along `push` (the victim's facing frame: +x its left, +z forward),
        /// by the dominant axis: from the front GetHit-F1 (the head snapped back as the body doubles over) or, for a heavy blow
        /// (heft from HeavyHeft: the greatsword, the Siren's claws, a heavy fighter strike), F2 (spun away from it), from a side
        /// L1 (struck on its left, pushed to its right) or R1. From behind None: the pack's B1 arches the voxel torso upright,
        /// which reads as a flinch, so the procedural forward lurch stays.
        /// </summary>
        public static PackSlot Direction(Vector3 push,float heft)
        {
            float x=NativeHitRules.Finite(push.X,0),z=NativeHitRules.Finite(push.Z,-1);
            if(x*x+z*z<1e-8f)z=-1;
            if(Math.Abs(z)>=Math.Abs(x))return z>0?PackSlot.None:heft>=HeavyHeft?PackSlot.HitF2:PackSlot.HitF;
            return x>0?PackSlot.HitR:PackSlot.HitL;
        }
        public const float HeavyHeft=1.3f;
        /// <summary>
        /// The Stunned loop's share of the daze for a continuous stun of `span` seconds (measured from the stun's blow):
        /// none for the short melee hit stuns (.08-.32 s, where the capture's upright dazed posture would pop up and back
        /// inside a few frames; the procedural daze shows those), full from .55 s, smooth in between. In play only the
        /// swap-shop stun (.5 s from its blow: about .9 here, the loop showing at about a third under the blow's GetHit
        /// clip) reaches it: melee hit stuns are at most .32 s and are not extended while stunned, and a freezer-block
        /// freeze lands with no blow of its own (NativeStunClock then dazes only its last .3 s, so this is 0; a blocked
        /// attacker's freeze is its parry reel, the route's own).
        /// </summary>
        public static float LongStun(float span)=>MeleeMath.SmoothStep(NativeHitRules.Finite(span,0),.3f,.55f);
        public static CapturedClip Clip(int set,PackSlot slot)
        {
            if(set<0||set>=NativePackClips.Sets.Length)return null;var s=NativePackClips.Sets[set];int i=(int)slot;return i>=0&&i<s.Length?s[i]:null;
        }
        /// <summary>
        /// A reaction clip's blend weight `age` seconds into a window of `span` seconds: a crisp ease in over the first 35 ms (a
        /// quarter of a short window) and an ease out over the last clamp(35%, 50 ms, 150 ms), exactly 0 before the blow and
        /// from the window's end on (a stunned blow's window ends at the authoritative stun end).
        /// </summary>
        public static float Weight(float age,float span)
        {
            if(!(age>0)||!(age<span))return 0;float rise=Math.Min(.035f,span*.25f),o=MeleeMath.Clamp(span*.35f,.05f,.15f);if(o>span-rise)o=Math.Max(1e-4f,span-rise);
            return MeleeMath.Clamp(MeleeMath.Ease(age/rise)*(1-MeleeMath.Ease((age-(span-o))/o)),0,1);
        }
        /// <summary>
        /// A hit clip's window: a light blow's light span; a stunned blow's stun up to the clip's captured length (the Stunned
        /// loop holds a longer stun), never shorter than the light span, so a short stun's reaction still reads (its
        /// whole-body part, the hands and hips, is gone at the stun end: StunFade; the upper body finishes as a light blow).
        /// </summary>
        public static float HitSpan(CapturedClip c,bool stunned,float stunSpan,float lightSpan)
        {
            if(c==null)return 0;float natural=c.Duration,light=Math.Max(.05f,NativeHitRules.Finite(lightSpan,.4f));
            return stunned?Math.Max(light,Math.Min(NativeHitRules.Finite(stunSpan,0),natural)):light;
        }
        /// <summary>The stun's whole-body share of a reaction (hands, hips travel, Stunned loop arms): 1 inside the stun, an ease
        /// out over its last clamp(40%, 20 ms, 80 ms), exactly 0 at the authoritative stun end and after.</summary>
        public static float StunFade(float time,float at,float until)
        {
            if(!(time<until)||!(time>=at))return 0;float o=MeleeMath.Clamp((until-at)*.4f,.02f,.08f);return 1-MeleeMath.Ease((time-(until-o))/o);
        }
        /// <summary>
        /// The clip time `age` seconds into a window of `span`: the captured impact peak lands at min(its captured time, 40% of
        /// the window) and the recovery fills the rest (CapturedClip.Retime, monotone). `at` and `marks` are 3-element scratch.
        /// </summary>
        public static float ClipTime(CapturedClip c,float age,float span,float[] at,float[] marks)
        {
            if(c==null)return 0;float end=c.Duration,peak=c.Marks!=null&&c.Marks.Length>=3?MeleeMath.Clamp(c.Marks[1],.01f,end-.01f):end*.3f;
            span=Math.Max(.02f,span);at[0]=0;at[2]=span;at[1]=MeleeMath.Clamp(Math.Min(peak,span*.4f),span*.05f,span*.95f);marks[0]=0;marks[1]=peak;marks[2]=end;
            return CapturedClip.Retime(at,marks,MeleeMath.Clamp(age,0,span));
        }
        /// <summary>The block-hit window (the captured melee route's own Block-GetHit timing): .47 s, its impact at .1 s.</summary>
        public const float BlockSpan=.47f;
        public static float BlockWeight(float age)=>!(age>0)||!(age<BlockSpan)?0:MeleeMath.Clamp(MeleeMath.Ease(age/.04f)*(1-MeleeMath.Ease((age-.35f)/.12f)),0,1);
        public static float BlockTime(CapturedClip c,float age,float[] at,float[] marks)
        {
            if(c==null)return 0;float end=c.Duration,peak=c.Marks!=null&&c.Marks.Length>=3?MeleeMath.Clamp(c.Marks[1],.01f,end-.01f):.15f;
            at[0]=0;at[1]=.1f;at[2]=.45f;marks[0]=0;marks[1]=peak;marks[2]=end;return CapturedClip.Retime(at,marks,MeleeMath.Clamp(age,0,.45f));
        }
    }

    /// <summary>
    /// Lays one pack reaction clip over a rig, additively: each joint takes the clip's turn away from its reference frame (a
    /// hit or block clip's first frame, which is the guard it starts from; the Stunned loop's set guard), so the route's own
    /// pose (the guard, a gait, a fighter stance, an attack) carries on under it and nothing pops when the weight rises from
    /// zero. Torso joints always; with `hips` the pelvis turn, the hips height and (while the feet are held) a little hips
    /// travel over them; with `arms` the clavicles and both hands, moved in chest space by the clip's hand travel and turn
    /// through the rig's IK, a two-handed hilt keeping its support hand and a blade kept clear of the head, pauldrons and
    /// torso (Core/MeleeClearance). No allocation.
    /// </summary>
    public sealed class NativePackPose
    {
        private readonly float[] row=new float[CapturedClip.Channels],row0=new float[CapturedClip.Channels];
        private static readonly int[] Torso={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head},TorsoChannel={MeleeCapture.Spine,MeleeCapture.Chest,MeleeCapture.Neck,MeleeCapture.Head};
        public const float MaxTravel=.06f,MaxTurn=1.2f;
        /// <summary>The trunk bend (rad, spine and chest together) laid in full, and the soft limit it eases toward.</summary>
        public const float BendKnee=.6f,MaxBend=.9f;
        /// <summary>The share of a clip's hand turn the held weapons show: the captures swing the blades through wide arcs that, on the voxel
        /// rig's big head and shoulders, cross the face; the hands still travel with the capture.</summary>
        public const float HandTurn=.45f;
        /// <summary>How close (chest space, forward) a clip may draw a two-handed hilt to the chest.</summary>
        public const float HiltReach=.22f;
        /// <summary>The share of the clip's blade turn a two-handed hilt shows (its long blade stands up past the face).</summary>
        public const float HiltTurn=.25f;
        private static Vector3 Row(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rot(float[] f,int i){Vector3 v=Row(f,i);float a=v.Length();return a<1e-9f||!(a<1e3f)?Quaternion.Identity:MeleeMath.AxisAngle(v/a,a);}
        private static Quaternion Delta(float[] a,float[] b,int i)=>MeleeMath.Inverse(Rot(a,i))*Rot(b,i);
        private static Quaternion Blade(float[] f)
        {
            Vector3 blade=MeleeMath.Unit(Row(f,MeleeCapture.Blade)),edge=Row(f,MeleeCapture.Edge);edge-=blade*Vector3.Dot(edge,blade);
            if(blade.LengthSquared()<.5f)blade=Vector3.UnitZ;if(edge.LengthSquared()<1e-8f)edge=new Vector3(1,0,0)-blade*blade.X;edge=Vector3.Normalize(edge);
            return MeleeMath.Basis(edge,Vector3.Cross(blade,edge),blade);
        }
        private static Quaternion Free(float[] f)
        {
            Vector3 a=MeleeMath.Unit(Row(f,MeleeCapture.LFwd)),c=Row(f,MeleeCapture.LUp);if(a.LengthSquared()<.5f)a=Vector3.UnitZ;c-=a*Vector3.Dot(c,a);if(c.LengthSquared()<1e-8f)c=Vector3.UnitY;c=Vector3.Normalize(c);
            return MeleeMath.Basis(Vector3.Cross(c,a),c,a);
        }
        private static readonly Vector3 PoleR=Vector3.Normalize(new Vector3(-1,-.7f,-.2f)),PoleL=Vector3.Normalize(new Vector3(1,-.8f,-.25f));
        // The route's own bend plane (in the clavicle's frame): the elbow's offset from the shoulder-to-wrist line, easing to
        // rig-ik's default pole as the arm straightens (where that offset has no direction), so the arm re-solves through the
        // same plane, the elbow never flips over and a zero offset gives back the route's own arm.
        private static Vector3 ElbowPole(MeleeRig r,bool left)
        {
            int clav=left?CapturedRig.LClav:CapturedRig.RClav;Vector3 s=r.P[left?CapturedRig.LArm:CapturedRig.RArm],fallback=left?PoleL:PoleR;
            Vector3 e=r.ChainPoint(left?CapturedRig.LElbow:CapturedRig.RElbow,clav)-s,w=r.ChainPoint(left?CapturedRig.LHand:CapturedRig.RHand,clav)-s;
            if(!(w.LengthSquared()>1e-8f)||float.IsNaN(e.X+w.X))return fallback;
            Vector3 d=Vector3.Normalize(w),o=e-d*Vector3.Dot(e,d);float k=MeleeMath.SmoothStep(o.Length(),.01f,.05f);
            Vector3 p=(k>0?Vector3.Normalize(o)*k:Vector3.Zero)+fallback*(1-k);return p.LengthSquared()>1e-8f?Vector3.Normalize(p):fallback;
        }
        // The main hand's grip and blade turn (chest space) after a turn-only clearance from the head, pauldrons and torso.
        private static float Cleared(MeleeRig r,MeleeSet fit,Vector3 grip,Quaternion orient,out Quaternion turn)
        {
            Vector3 cg=grip,b0=Vector3.Transform(Vector3.UnitZ,orient),blade=b0,edge=Vector3.Transform(Vector3.UnitX,orient);turn=Quaternion.Identity;
            MeleeClearance.Shared.Clear(r,ref cg,ref blade,ref edge,-fit.Fit.Pommel*r.WeaponReach,fit.BladeEnd*r.WeaponReach,false,fit.Fit.HalfWidth,fit.Fit.Floor);
            if(!(blade.LengthSquared()>1e-8f)||float.IsNaN(blade.X+blade.Y+blade.Z))return 0;
            blade=Vector3.Normalize(blade);turn=MeleeMath.FromTo(b0,blade);return (float)Math.Acos(MeleeMath.Clamp(Vector3.Dot(b0,blade),-1,1));
        }
        /// <summary>1 where the rig's current blade is clear of the body, easing to 0 as its own pose crowds it (the clearance
        /// would turn it by .02 to .1 rad): how much of the clearance a layered offset takes over a route's own pose.</summary>
        public float RouteClear(MeleeRig r,MeleeSet fit)
        {
            if(r==null||fit==null||!r.WeaponVisible||fit.Fit.Clear==MeleeClear.None)return 0;
            return 1-MeleeMath.SmoothStep(Cleared(r,fit,r.ChainPoint(CapturedRig.RHand,CapturedRig.Chest),r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest),out _),.02f,.1f);
        }
        /// <summary>
        /// The held blade's last clearance after everything layered over a route this frame (the procedural accent on top of a
        /// clip may nudge the head into it): the main hand turned clear by `weight` (0..1) about its grip, re-solved through
        /// its own elbow bend, a two-handed hilt keeping its support hand. No allocation.
        /// </summary>
        public void ClearBlade(MeleeRig r,MeleeSet fit,bool twoHand,float weight)
        {
            float w=MeleeMath.Clamp(NativeHitRules.Finite(weight,0),0,1);if(r==null||fit==null||!r.WeaponVisible||fit.Fit.Clear==MeleeClear.None||w<=1e-4f)return;
            Vector3 grip=r.ChainPoint(CapturedRig.RHand,CapturedRig.Chest);Quaternion orient=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest);
            if(!(Cleared(r,fit,grip,orient,out var turn)>1e-3f))return;
            r.BraceWeapon(grip,MeleeMath.Slerp(Quaternion.Identity,turn,w)*orient,1,ElbowPole(r,false),false);if(twoHand)r.SupportHand(1);
        }
        /// <summary>
        /// Lays `clip` at `clipTime` over the rig at `weight`, as its offset from `reference` at `referenceTime` (null: the
        /// clip's own first frame). `hips`, `arms` and `travel` (0..1, of `weight`): the pelvis turn and hips height, the
        /// clavicles and hands (`main` off: only the free hand's side), and the hips travel over held feet. `turn` (0..1): the share of the clip's own turn about the
        /// vertical (measured from its own first frame, so a capture facing elsewhere never turns the pilot off its facing).
        /// `fit`: the weapon whose blade is kept clear (null: fists). `twoHand`: the support hand stays on the hilt.
        /// </summary>
        public void Layer(MeleeRig r,CapturedClip clip,float clipTime,CapturedClip reference,float referenceTime,float weight,float hips,float arms,bool main,MeleeSet fit,bool twoHand,float travel,float turn)
        {
            float w=MeleeMath.Clamp(NativeHitRules.Finite(weight,0),0,1);if(r==null||clip==null||w<=1e-4f)return;
            clip.Sample(clipTime,row);(reference??clip).Sample(reference!=null?referenceTime:0,row0);
            LayerRows(r,row,row0,clip.Decode()[MeleeCapture.Yaw],w,hips,arms,main,fit,twoHand,travel,turn);
        }
        /// <summary>
        /// <see cref="Layer"/> over rows the caller already sampled (and may have blended): `row` the pose, `row0` its reference,
        /// `yaw0` the heading the clip's own turn is measured from (the clip's first frame). The jump layer (Core/NativeJumpMotion)
        /// blends its rise, fall and landing rows before laying them. No allocation.
        /// </summary>
        public void LayerRows(MeleeRig r,float[] row,float[] row0,float yaw0,float weight,float hips,float arms,bool main,MeleeSet fit,bool twoHand,float travel,float turn)
        {
            float w=MeleeMath.Clamp(NativeHitRules.Finite(weight,0),0,1);if(r==null||row==null||row0==null||w<=1e-4f)return;
            // The hands as the route holds them, in chest space, before the torso turns.
            Vector3 gR=Vector3.Zero,gL=Vector3.Zero;Quaternion qR=Quaternion.Identity,qL=Quaternion.Identity;float wa=MeleeMath.Clamp(NativeHitRules.Finite(arms,0),0,1)*w;
            bool clear=main&&fit!=null&&r.WeaponVisible&&fit.Fit.Clear!=MeleeClear.None;float own=wa;
            if(wa>1e-4f)
            {
                gR=r.ChainPoint(CapturedRig.RHand,CapturedRig.Chest);qR=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest);gL=r.ChainPoint(CapturedRig.LHand,CapturedRig.Chest);qL=r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest);
                // Where the route's own blade is clear the clip's offset is cleared in full (the blade never crosses the face);
                // where the route's pose already crowds the body the clearance eases in with the clip, so the route stays its own.
                if(clear)own=wa+(1-wa)*RouteClear(r,fit);
            }
            // The trunk's bend (spine and chest together) eases into a soft limit: the side and heavy captures fold the pack
            // character by up to 1.7 rad, which on the voxel rig's big head and blade reads as a collapse.
            Quaternion trunk=Delta(row0,row,MeleeCapture.Spine)*Delta(row0,row,MeleeCapture.Chest);
            float bend=2*(float)Math.Acos(Math.Min(1,Math.Abs(trunk.W)))*w,wt=bend>BendKnee?w*(BendKnee+(MaxBend-BendKnee)*(float)Math.Tanh((bend-BendKnee)/(MaxBend-BendKnee)))/bend:w;
            for(int k=0;k<Torso.Length;k++)r.SetQ(Torso[k],r.Q[Torso[k]]*MeleeMath.Slerp(Quaternion.Identity,Delta(row0,row,TorsoChannel[k]),wt));
            float h=MeleeMath.Clamp(hips,0,1)*w;
            if(h>1e-4f)
            {
                float yaw=MeleeMath.Clamp(NativeHitRules.Finite(row[MeleeCapture.Yaw]-yaw0,0),-MaxTurn,MaxTurn)*MeleeMath.Clamp(turn,0,1)*h;
                r.SetQ(CapturedRig.Pelvis,MeleeMath.AxisAngle(MeleeMath.Y,yaw)*r.Q[CapturedRig.Pelvis]*MeleeMath.Slerp(Quaternion.Identity,Delta(row0,row,MeleeCapture.Pelvis),h));
                Vector3 b=r.P[CapturedRig.Body];b.Y+=MeleeMath.Clamp(row[MeleeCapture.By]-row0[MeleeCapture.By],-.15f,.08f)*h;
                float t=MeleeMath.Clamp(travel,0,1)*h;
                if(t>1e-4f){b.X+=MeleeMath.Clamp(row[1]-row0[1],-MaxTravel,MaxTravel)*t;b.Z+=MeleeMath.Clamp(row[3]-row0[3],-MaxTravel,MaxTravel)*t;}
                r.P[CapturedRig.Body]=b;
            }
            if(!(wa>1e-4f))return;
            r.SetQ(CapturedRig.LClav,r.Q[CapturedRig.LClav]*MeleeMath.Slerp(Quaternion.Identity,Delta(row0,row,MeleeCapture.LClav),wa));
            if(main)r.SetQ(CapturedRig.RClav,r.Q[CapturedRig.RClav]*MeleeMath.Slerp(Quaternion.Identity,Delta(row0,row,MeleeCapture.RClav),wa));
            // The main hand: the clip's grip travel and blade turn (chest space) over the held weapon, weighted on the targets and
            // solved fully through the route's own elbow bend (a weighted joint blend of two solves can flip the forearm).
            if(main)
            {
                Vector3 grip=gR+(Row(row,MeleeCapture.Grip)-Row(row0,MeleeCapture.Grip))*wa;Quaternion orient=MeleeMath.Slerp(Quaternion.Identity,Blade(row)*MeleeMath.Inverse(Blade(row0)),(twoHand?HiltTurn:HandTurn)*wa)*qR;
                // A two-handed hilt stays where the support hand can reach its grip (.11 down the blade; the converter's own rule),
                // and in front of the chest: the captures draw the hands in under the chin, where on the voxel rig's big head the
                // upright blade would cross the face.
                if(twoHand){grip.Z=Math.Max(grip.Z,Math.Min(gR.Z,HiltReach));Vector3 L=new Vector3(.345f,.18f,0),s=grip-Vector3.Transform(Vector3.UnitZ,orient)*(.11f*r.WeaponReach);float need=Vector3.Distance(s,L)-.44f;if(need>0)grip+=Vector3.Normalize(L-s)*(need*wa);}
                if(clear)
                {
                    // The blade kept clear of the head, pauldrons and torso (`own`: in full over a clear route pose). Only the blade's
                    // swing is taken from the clearance (turn only); the grip keeps its place and its roll about the blade.
                    if(Cleared(r,fit,grip,orient,out var away)>0)orient=MeleeMath.Slerp(Quaternion.Identity,away,own)*orient;
                }
                if(fit!=null){r.BraceWeapon(grip,orient,1,ElbowPole(r,false),false);if(twoHand)r.SupportHand(1);}
                else r.Hand(false,grip,orient,1,ElbowPole(r,false));
            }
            // The free (or second weapon) hand by its own travel and turn, unless the hilt holds it.
            if(!twoHand)
            {
                Vector3 hand=gL+(Row(row,MeleeCapture.LHand)-Row(row0,MeleeCapture.LHand))*wa;Quaternion lo=MeleeMath.Slerp(Quaternion.Identity,Free(row)*MeleeMath.Inverse(Free(row0)),HandTurn*wa)*qL;
                r.Hand(true,hand,lo,1,ElbowPole(r,true));
            }
        }
    }
}
