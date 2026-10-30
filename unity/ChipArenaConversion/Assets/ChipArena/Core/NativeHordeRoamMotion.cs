using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The town zombies' roaming over the horde gait (Core/NativeHordeRoam; docs/EXPEDITION-HUB.md "Town zombie roaming and
    /// perception"). Presentation only: never read by the authority, no change to hit timing, hitboxes (Balance still holds the
    /// trunk over the capsule), damage, cooldowns, movement or the network frame. With HordeMotionInput.Roam off (every add but
    /// the town's) nothing here runs and the gait is today's.
    /// <list type="bullet">
    /// <item>Wander: the walk part of the stride blends to a slow forward take (NativeZombieMotion.Wander: its cadence, feet and
    /// pose), the trunk lolls with the stride, the head glances about now and then and an occasional stumble dips the trunk.</item>
    /// <item>Idle: the kind's standing take under a per-add schedule of five variants (sway, twitch, look around, slump, rock;
    /// NativeHordeRoam.Variants), crossfading, hashed from the add's seed against the clock so a crowd is never in step and every
    /// peer draws the same variant at the same moment.</item>
    /// <item>Alert: the head snaps to what it noticed (the host's look direction), the chest turns after it while the host turns
    /// the body, a scream throws the head back and flings the arms on a reel's start (the Hulk's roar take), then the burst
    /// throws the trunk into the chase. All from the alert's replicated start (hubAlertAt), so a guest that joins mid-scream
    /// draws the scream's same moment.</item>
    /// <item>Search: a wide scan of the head with the chest following, and sniffing (head up, quick nods, the chest lifting).</item>
    /// <item>The states blend over NativeHordeRoam.Blend from each change, so a search or a chase eases back into the wander.</item>
    /// </list>
    /// Fixed fields: no allocation per frame.
    /// </summary>
    public sealed partial class NativeHordeGait
    {
        private int wanderClip=-1;private float roamTake,roamPhase,roamHang=1;
        private readonly float[] rowV=new float[CapturedClip.Channels],variantW=new float[NativeHordeRoam.IdleVariants];
        /// <summary>The slow take this add wanders on (NativeZombieClips index; -1 off the capture).</summary>
        public int WanderClip=>wanderClip;
        /// <summary>The last frame's idle variant weights (NativeHordeRoam.Sway..Rock), for the checks and the gallery.</summary>
        public float IdleVariantWeight(int v)=>v>=0&&v<variantW.Length?variantW[v]:0;
        /// <summary>The wander take's share of the walk the last frame (0: the kind's own walk).</summary>
        public float WanderTakeShare=>roamTake;
        // This frame's roaming values (RoamPrepare), applied to the body, trunk and head.
        private float rIdleK,rLookYaw,rLookPitch,rTwitch,rTwitchSide,rGlance,rGlanceSide,rStumble,rSniff,rScanYaw,rScanPitch,rSnap,rScream,rTurn,rBurst,rNoticeYaw,rNoticePitch;private bool rNotice;

        private void RoamReset(uint s)
        {
            wanderClip=NativeZombieMotion.Captured(kind)&&kind!=NativeHordeKinds.Hulk?NativeZombieMotion.Wander(kind,s):-1;
            roamPhase=NativeHordeKinds.Hash(s,95)*6.283f;roamHang=NativeHordeKinds.Hash(s,96)<.5f?-1:1;for(int i=0;i<variantW.Length;i++)variantW[i]=0;
        }
        // The wander take's share: calm (wandering, idling, the blend between them), on the capture, never the heavy kinds'.
        private float RoamTakeOf(in HordeMotionInput f)=>f.Roam.On&&wanderClip>=0&&!heavy&&!quad?Clamp01(f.Roam.Wander+f.Roam.Idle):0;
        // The walk part of the stride: the kind's walk take blended toward the wander take.
        private float WalkCadence(float v)=>roamTake>0?Lerp(NativeZombieMotion.Cadence(walkClip,v,kind),NativeZombieMotion.Cadence(wanderClip,v,kind),roamTake):NativeZombieMotion.Cadence(walkClip,v,kind);
        private float WalkDuty(int foot)=>roamTake>0?Lerp(NativeZombieMotion.Duty(walkClip,foot),NativeZombieMotion.Duty(wanderClip,foot),roamTake):NativeZombieMotion.Duty(walkClip,foot);
        private float WalkSwing(int foot,float s)=>roamTake>0?Lerp(NativeZombieMotion.SwingLift(walkClip,foot,s),NativeZombieMotion.SwingLift(wanderClip,foot,s),roamTake):NativeZombieMotion.SwingLift(walkClip,foot,s);
        private void RoamWalkRow(){if(roamTake>0){SampleCalm(wanderClip,Phase,rowV,RateOf(wanderClip,Math.Max(gaitSpeed,.8f*Speed)));NativeZombieClips.Blend(rowW,rowV,roamTake,rowW);}}

        // A Unity world yaw (degrees, +Z = 0, + to the right) as the rig's turn from the presented facing (rad, + toward its left).
        private float YawFrom(float worldYaw)=>-Math.Max(-1.2f,Math.Min(1.2f,Wrap(worldYaw-Yaw)/57.2958f));
        /// <summary>This frame's roaming values (the clock, the seed and the read only).</summary>
        private void RoamPrepare(in HordeMotionInput f,float moving,float lookYaw,float lookPitch)
        {
            var r=f.Roam;rNotice=false;rIdleK=rTwitch=rGlance=rStumble=rSniff=rSnap=rScream=rTurn=rBurst=rScreamAge=0;
            if(!r.On||quad){for(int i=0;i<variantW.Length;i++)variantW[i]=0;return;}
            float t=f.Time;
            // ---- idle: the variant schedule, its look around and twitches (the record's look is the body's facing here) ----
            NativeHordeRoam.Variants(seed,t,variantW,out _);rIdleK=r.Idle*(1-.5f*moving);
            NativeHordeRoam.Scan(seed,120,t,1.8f,1.0f,.16f,out rLookYaw,out rLookPitch);
            rTwitch=NativeHordeRoam.Pulse(seed,130,t,1.1f,.05f,.06f,.32f,out rTwitchSide);
            // ---- wander: a glance now and then, an occasional stumble while walking ----
            rGlance=NativeHordeRoam.Pulse(seed,150,t,4.2f,.32f,1.0f,.45f,out rGlanceSide);
            rStumble=NativeHordeRoam.Pulse(seed,155,t,9.5f,.12f,.14f,.5f,out _)*moving;
            // ---- search: the wide scan over the host's slow turn in place, and the sniffs ----
            NativeHordeRoam.Scan(seed,160,t,1.25f,1.2f,.12f,out rScanYaw,out rScanPitch);
            rSniff=NativeHordeRoam.Pulse(seed,170,t,2.2f,.2f,.9f,.3f,out _);
            // ---- alert: what it noticed (the record's look direction, the pilot's height when one is there; else the pilot) ----
            if(!float.IsNaN(r.AlertAge))
            {
                if(r.HasLookYaw){rNoticeYaw=YawFrom(r.LookYaw);rNoticePitch=f.HasTarget?lookPitch:0;rNotice=true;}else if(f.HasTarget){rNoticeYaw=lookYaw;rNoticePitch=lookPitch;rNotice=true;}
                rScreamAge=r.AlertAge-NativeHordeRoam.ScreamAt;rSnap=rNotice?NativeHordeRoam.SnapWeight(r.AlertAge):0;rTurn=rNotice?NativeHordeRoam.TurnWeight(r.AlertAge):0;rScream=NativeHordeRoam.Scream(r.AlertAge);
            }
            rBurst=NativeHordeRoam.Burst(r.BurstAge);
        }
        /// <summary>The scream over the trunk and arms: the arms flung and the chest out on a reel's start (the Hulk's roar take).</summary>
        private void RoamUpper(float[] into,float moving)
        {
            if(rScream<=0)return;float a=Math.Max(0,rScreamAge);
            NativeZombieClips.Sample(NativeZombieMotion.Roar,.12f+.26f*Sm(a/.55f),rowA);Upper(into,rowA,.72f*rScream*(1-.45f*moving),0,.3f);
        }
        private float rScreamAge;
        private void RoamBody(ref Vector3 b,in HordeMotionInput f,float moving)
        {
            var r=f.Roam;if(!r.On||quad)return;float t=f.Time;
            if(rIdleK>0)
            {
                float k=rIdleK*(1-moving),sw=(float)Math.Sin(t*1.96f+roamPhase),rk=(float)Math.Sin(t*2.6f+roamPhase);
                b.Y-=.05f*variantW[NativeHordeRoam.Slump]*rIdleK;b.X+=.03f*sw*variantW[NativeHordeRoam.Sway]*k;b.Z+=.02f*rk*variantW[NativeHordeRoam.Rock]*k;
            }
            if(r.Wander>0&&rStumble>0){b.Y-=.06f*rStumble*r.Wander;b.Z+=.05f*rStumble*r.Wander;}
            b.Y-=.025f*rScream+.04f*rBurst;
        }
        private void RoamTrunk(CapturedRig.Pose p,in HordeMotionInput f,float moving)
        {
            var r=f.Roam;if(!r.On||quad)return;float t=f.Time;
            if(rIdleK>0)
            {
                float k=rIdleK,sw=(float)Math.Sin(t*1.96f+roamPhase),heave=.025f*(float)Math.Sin(t*1.3f+roamPhase),rk=(float)Math.Sin(t*2.6f+roamPhase);
                float wS=variantW[NativeHordeRoam.Sway]*k,wD=variantW[NativeHordeRoam.Slump]*k,wR=variantW[NativeHordeRoam.Rock]*k,wL=variantW[NativeHordeRoam.LookAround]*k,wT=variantW[NativeHordeRoam.Twitch]*k;
                Bend(p,CapturedRig.Spine,(.24f+heave)*wD+.1f*rk*wR,.05f*sw*wS+.12f*rLookYaw*wL,.08f*sw*wS+.05f*roamHang*wD);
                Bend(p,CapturedRig.Chest,(.18f+heave)*wD+.05f*rk*wR,.18f*rLookYaw*wL,-.04f*sw*wS);
                if(wT>0&&rTwitch>0)Bend(p,rTwitchSide>0?CapturedRig.LClav:CapturedRig.RClav,0,0,.22f*rTwitch*wT*rTwitchSide);
            }
            if(r.Wander>0)
            {
                float k=r.Wander*moving,loll=(float)Math.Sin(Phase*6.283f+roamPhase);
                Bend(p,CapturedRig.Spine,.06f*k+.3f*rStumble*r.Wander,0,.05f*loll*k);Bend(p,CapturedRig.Chest,.18f*rStumble*r.Wander,.1f*rGlance*rGlanceSide*k,0);
            }
            if(r.Search>0)Bend(p,CapturedRig.Chest,-.1f*rSniff*r.Search,.35f*rScanYaw*r.Search,0);
            if(rTurn>0){Bend(p,CapturedRig.Spine,0,.2f*rNoticeYaw*rTurn,0);Bend(p,CapturedRig.Chest,0,.4f*rNoticeYaw*rTurn,0);}
            if(rScream>0){Bend(p,CapturedRig.Spine,-.1f*rScream,0,0);Bend(p,CapturedRig.Chest,-.2f*rScream,0,0);}
            if(rBurst>0){Bend(p,CapturedRig.Spine,.24f*rBurst,0,0);Bend(p,CapturedRig.Chest,.14f*rBurst,0,0);}
        }
        private void RoamHead(in HordeMotionInput f,float moving,ref float pitch,ref float yaw,ref float roll)
        {
            var r=f.Roam;if(!r.On||quad)return;float t=f.Time;
            if(rIdleK>0)
            {
                float k=rIdleK,wL=variantW[NativeHordeRoam.LookAround]*k,wT=variantW[NativeHordeRoam.Twitch]*k,wD=variantW[NativeHordeRoam.Slump]*k,wS=variantW[NativeHordeRoam.Sway]*k,wR=variantW[NativeHordeRoam.Rock]*k;
                yaw+=rLookYaw*wL+.5f*rTwitch*rTwitchSide*wT;pitch+=rLookPitch*wL+.18f*rTwitch*wT+.34f*wD-.09f*(float)Math.Sin(t*2.6f+roamPhase)*wR;
                roll+=.42f*rTwitch*rTwitchSide*wT+.28f*roamHang*wD+.14f*(float)Math.Sin(t*1.96f+roamPhase-.6f)*wS;
            }
            if(r.Wander>0)
            {
                float k=r.Wander;yaw+=.7f*rGlance*rGlanceSide*k;pitch+=(.1f+.25f*rStumble)*k;roll+=.1f*(float)Math.Sin(t*1.1f+roamPhase)*k;
            }
            if(r.Search>0){float k=r.Search;yaw+=rScanYaw*k;pitch+=(rScanPitch-.32f*rSniff+.06f*rSniff*(float)Math.Sin(t*11))*k;}
            if(rSnap>0)
            {
                // The snap lands on what it noticed (a small overshoot past it), then hands back to the chase's tracking.
                float s=Math.Min(1,rSnap),over=Math.Max(0,rSnap-1);yaw+=(rNoticeYaw-yaw)*s+rNoticeYaw*over;pitch+=(rNoticePitch*.6f-pitch)*s;
            }
            if(rScream>0){float a=rScreamAge;pitch+=(-.5f+.04f*(float)Math.Sin(a*37))*rScream;roll*=1-.6f*rScream;}
            if(rBurst>0)pitch-=.2f*rBurst;
        }
        /// <summary>The snap's aim: after the head's bends, what is left between the head's facing (the take's own head turn and
        /// the trunk's included) and what it noticed is turned out at the head, so the face lands on it.</summary>
        private void RoamAim(CapturedRig.Pose p)
        {
            if(rSnap<=0||!rNotice)return;p.World();var fw=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.Head]);float flat=(float)Math.Sqrt(fw.X*fw.X+fw.Z*fw.Z);if(flat<.2f)return;
            float s=Math.Min(1,rSnap),over=Math.Max(0,rSnap-1),yaw=(float)Math.Atan2(fw.X,fw.Z),pitch=(float)Math.Atan2(-fw.Y,flat);
            float dy=Wrap((rNoticeYaw*(1+over)-yaw)*57.2958f)/57.2958f,dp=rNoticePitch*.6f-pitch;
            Bend(p,CapturedRig.Head,Math.Max(-.5f,Math.Min(.5f,dp))*s*(1-.7f*rScream),Math.Max(-.9f,Math.Min(.9f,dy))*s,0);
        }
    }
}
