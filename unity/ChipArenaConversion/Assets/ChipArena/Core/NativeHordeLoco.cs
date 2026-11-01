using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde's locomotion pass (owner 2026-10-19: "I think I want to change the zombie animations for their movement, their
    /// arms also seem to detach slightly. Can you take a look into the movement for them again? Their may be a better animation to
    /// use. Also, it needs to be more fluid. Currently, it's still like rapid back and forth movement that doesn't look
    /// realistic."; docs/ZOMBIE-LOCOMOTION.md). Presentation only: it reads the presented root's speed and the add's kind, look
    /// and seed, and writes the pose; no rule, hit volume, timing, movement or network value changes.
    /// <list type="bullet">
    /// <item>The cadence is the body's, not the take's (<see cref="StepRate"/>): steps per second grow with the root's speed by a
    /// power of .35, scaled by the kind's tempo, so the stride takes most of a speed change and every take of a kind steps at the
    /// same rate at a given speed. The shipped mapping played the pack's slow shambles at 2 to 2.8 times their speed (4 to 6
    /// steps a second) to keep up with the authority's 1.3 to 4.6 m/s, so the trunk, arms and head jittered back and forth.</item>
    /// <item>A take played faster than it was captured is calmed (<see cref="Calm"/>): its trunk and arms swing about the cycle's
    /// mean pose by less, so their speed stays near the capture's own instead of multiplying with the playback rate.</item>
    /// <item>The cadence, the walk-to-run blend and the stance read a gait speed eased off the presented root's
    /// (<see cref="GaitHalfLife"/>), the blend limited in rate (<see cref="BlendSlew"/>): a crowd's separation jostle no longer
    /// flips an add between its walk and run takes. The feet still land on the root's real speed, so nothing slides.</item>
    /// <item>A foot's stance never carries the hips farther than the legs reach (<see cref="StanceMost"/>): at speed the stance
    /// shortens (a run's flight) instead of the trailing foot being torn off early out of step.</item>
    /// <item>The shoulders sit on the chest (<see cref="ShoulderInset"/>): the zombie bodies' chests are narrower than the
    /// rig's clavicles, so the shoulder block and the arm hung off the torso with a gap (2 to 10 cm at rest, wider as the
    /// capture's shoulders moved). Each kind's clavicles are set in so the shoulder block meets the chest, before the arms
    /// are solved (so the hands still reach the capture's targets).</item>
    /// </list>
    /// Fixed fields; no allocation per frame.
    /// </summary>
    public sealed partial class NativeHordeGait
    {
        // ================================================================================================================
        // The cadence (steps per second) of a kind's body at a speed
        // ================================================================================================================
        /// <summary>Steps per second at 1 rig m/s and the speed's power: 1.5 m/s walks at about 2.95 steps a second (steps of
        /// half a metre, the rig's legs at full stride), 3.4 m/s runs at 3.9.</summary>
        public const float CadenceAt1=2.56f,CadenceExp=.35f;
        /// <summary>The steps per second a kind takes at rig speed `v` (m/s): the body's cadence (its tempo: the brute, the bloater
        /// and the big zone kinds step slower and longer), whatever take it walks on. `legs`: the legs the stride is planned for, as
        /// a multiple of the rig's (<see cref="Legs"/>; the model's are about 1.7 times as long).</summary>
        public static float StepRate(int kind,float v,float legs=1)
        {
            // (Longer legs step as the rig's do at the speed that is the same pace for them (the Froude number: the speed over the
            // root of the legs), their time the root of their length slower: rate falls by legs^-.675 and the steps lengthen by
            // legs^.675. The model at 1.5 m/s takes 2.05 steps of .73 m a second (a person walking, about 1.9 to 2), not the voxel
            // rig's 2.95 of .51 m, which put its takes at up to 2.2 times their pace: the owner's "sped up animations".)
            if(legs>1.001f){float k=(float)Math.Sqrt(Math.Min(LegsMost,legs));return StepRate(kind,v/k)/k;}
            v=Math.Max(.12f,v);float rate=NativeHordeZones.Tempo(NativeHordeKinds.Index(kind),NativeZombieMotion.Tempo)*CadenceAt1*(float)Math.Pow(v,CadenceExp);
            // (Never a step longer than the legs carry: a slow-tempo kind walking fast steps quicker rather than floating. A walk
            // (to 1 m/s) keeps its feet down together between steps; by 2.6 m/s every body runs, a flight between them.)
            return Math.Max(rate,v/Lerp(StepMost,RunStepMost,Math.Max(0,Math.Min(1,(v-1)/1.6f))));
        }
        /// <summary>The longest step (rig metres) walking (feet down together between steps) and running (a flight between them).</summary>
        public const float StepMost=.5f,RunStepMost=.9f;

        // ================================================================================================================
        // The legs the stride is planned for: the rig's, or the model's longer ones
        // ================================================================================================================
        /// <summary>
        /// The legs the add's stride is planned for this pose, as a multiple of the rig's (1; the presentation sets it each pose to
        /// the model's ratio, NativeVisualRig.Rig.LegRatio, while the add draws on the model, owner 2026-10-30: "I think a lot of
        /// the issue is the sped up animations on the zombie"). An upright kind's cadence, stance reach and steps follow it
        /// (<see cref="StepRate"/>): the footprints are a person's. The rig's legs cannot reach them, so its legs are posed on a
        /// copy of each footprint drawn in fore and aft of its hip by the same ratio (<see cref="LegsNow"/>; the rig is a short-
        /// legged copy of the model's stride), and the model's binding spreads its ankles back out (NativeVisualRig.State.Stride),
        /// so the model's feet land on the footprints and nothing the rig's reach rules do changes. The Hulk keeps its own heavy
        /// stride and the crawl its own; the voxel tiers draw on 1.
        /// </summary>
        public float Legs=1;
        /// <summary>The longest legs the stride is planned for.</summary>
        public const float LegsMost=2.2f;
        private float legsNow=1;
        /// <summary>The legs this pose's stride was planned for (1: the rig's): each rig ankle sits fore or aft of its hip by the
        /// footprint's offset over this.</summary>
        public float LegsNow=>legsNow;
        // A footprint (rig frame) drawn in fore and aft of `hip` for the rig's legs, and a rig point spread back out to the footprint.
        private Vector3 Squeeze(Vector3 at,Vector3 hip){if(legsNow!=1)at.Z=hip.Z+(at.Z-hip.Z)/legsNow;return at;}
        private Vector3 Spread(Vector3 at,Vector3 hip){if(legsNow!=1)at.Z=hip.Z+(at.Z-hip.Z)*legsNow;return at;}

        // ================================================================================================================
        // The gait speed: the presented root's speed eased, and the walk-to-run blend on it
        // ================================================================================================================
        /// <summary>The half-life (s) of the speed the cadence and the take blend read, and the blend's most change per second.</summary>
        public const float GaitHalfLife=.18f,BlendSlew=2.2f;
        /// <summary>The speeds (rig m/s) an add sets off and comes to a stand at (hysteresis: a jostled add does not flicker).</summary>
        public const float MoveOn=.24f,MoveOff=.14f;
        private BossSpring gaitEase;private float gaitSpeed,runNow;private bool movingNow;
        /// <summary>The eased speed the cadence reads (rig m/s) and the walk-to-run blend this frame (0 walk, 1 run), for the checks.</summary>
        public float GaitSpeed=>gaitSpeed;public float RunNow=>runNow;
        private void GaitSpeedStep(bool fresh,float dt)
        {
            if(fresh){gaitEase.Reset(Speed);gaitSpeed=Speed;runNow=RunBlend(kind,Speed);movingNow=Speed>MoveOn;return;}
            gaitSpeed=Math.Max(0,gaitEase.Step(Speed,GaitHalfLife,dt));
            float want=RunBlend(kind,gaitSpeed),most=BlendSlew*dt;runNow+=Math.Max(-most,Math.Min(most,want-runNow));
            if(movingNow?Speed<MoveOff:Speed>MoveOn)movingNow=!movingNow;
        }

        // ================================================================================================================
        // The stance: never longer than the legs reach
        // ================================================================================================================
        /// <summary>The farthest the hips travel over a planted foot (rig metres): about twice the leg's reach in front of and
        /// behind the hip with the knees bent.</summary>
        public const float StanceMost=.56f;
        /// <summary>A stance share `duty` kept within the reach of legs `legs` times the rig's at cycle rate `hz` (cycles a second)
        /// and speed `v`.</summary>
        private static float Reachable(float duty,float hz,float v,float legs)=>v<=.05f?duty:Math.Max(.2f,Math.Min(duty,StanceMost*legs*hz/v));

        // ================================================================================================================
        // Calm: a take played faster than it was captured swings less about its cycle's mean
        // ================================================================================================================
        /// <summary>The swing's gain at playback rate r is r^-CalmExp (never under <see cref="CalmFloor"/>): at twice the capture's
        /// pace the trunk and arms swing about two thirds as far, so their speed grows by a third instead of doubling.</summary>
        public const float CalmExp=.6f,CalmFloor=.45f;
        /// <summary>The swing's gain at playback rate `rate`.</summary>
        public static float CalmGain(float rate)=>rate<=1?1:Math.Max(CalmFloor,(float)Math.Pow(rate,-CalmExp));
        // The channels calmed: the heading, the hips' sway, pelvis to head and the clavicles, both arms' reach. The hips' bob
        // keeps its depth (the weight landing on each step), and the legs and feet are the planted gait's.
        private static readonly int[] CalmFrom={0,3,39},CalmTo={2,25,63};
        /// <summary>
        /// How steadily a kind carries its trunk and arms (1: as captured): the gain on their swing about the cycle's mean at the
        /// capture's own pace. The heavy kinds do not throw their shoulders about as the pack's frenzied sprints do; the runner
        /// keeps its frenzy. By the kind's body (Siren order; a zone kind its body's).
        /// </summary>
        private static readonly float[] steady={.82f,.9f,.72f,.85f,1,.72f,1};
        public static float Steady(int kind)=>steady[NativeHordeKinds.Base(NativeHordeKinds.Index(kind))];
        /// <summary>Calms `row` (take `clip` sampled) toward its cycle's mean by the gain at `rate` and the kind's steadiness.</summary>
        public static void Calm(int clip,float[] row,float rate,float steadiness=1)
        {
            float g=CalmGain(rate)*steadiness;if(g>=1)return;var mean=NativeZombieMotion.Mean(clip);if(mean==null)return;
            for(int s=0;s<CalmFrom.Length;s++)for(int c=CalmFrom[s];c<CalmTo[s];c++)row[c]=mean[c]+(row[c]-mean[c])*g;
        }
        /// <summary>The phase blur (cycles each side) a take played at twice its pace or more gets (the Full tier): a binomial
        /// three-tap average over the cycle that keeps the stride's own swing (its first harmonic loses about 4 %) and takes the
        /// capture's quick wobbles off the trunk and arms (its fourth harmonic loses about half), which a faster playback would
        /// otherwise turn into a shiver.</summary>
        public const float BlurMost=.06f;
        private readonly float[] blurA=new float[CapturedClip.Channels],blurB=new float[CapturedClip.Channels];
        /// <summary>Samples take `clip` at the gait's `phase` (cycles; the take's own phase is its warp, NativeZombieMotion.Warp: its
        /// feet strike when the gait's do) into `row`, its trunk and arms blurred over the cycle and calmed about its mean by its
        /// playback `rate` (neither at or under its own pace) and the kind's steadiness.</summary>
        private void SampleCalm(int clip,float phase,float[] row,float rate)
        {
            NativeZombieClips.SamplePhase(clip,NativeZombieMotion.Warp(clip,phase),row);
            // (Only the nearest adds' (the Full tier's) are blurred: two more samples a take; the far crowd is calmed alone.)
            float d=lifeTier==NativeHordeModels.Full?BlurMost*Math.Max(0,Math.Min(1,rate-1)):0;
            if(d>1e-4f){NativeZombieClips.SamplePhase(clip,NativeZombieMotion.Warp(clip,phase-d),blurA);NativeZombieClips.SamplePhase(clip,NativeZombieMotion.Warp(clip,phase+d),blurB);
                for(int s=0;s<CalmFrom.Length;s++)for(int c=CalmFrom[s];c<CalmTo[s];c++)row[c]=.5f*row[c]+.25f*(blurA[c]+blurB[c]);}
            Calm(clip,row,rate,Steady(kind));
        }
        /// <summary>The playback rate a cycle runs at this frame (its cadence over its own), for the calm.</summary>
        private float RateOf(int clip,float v)=>clip<0?1:NativeZombieMotion.Rate(clip,v,kind,legsNow)*cadence;

        // ================================================================================================================
        // The shoulders on the chest
        // ================================================================================================================
        /// <summary>
        /// How far in (rig metres, toward the chest's middle) each kind's clavicles sit from the rig's (±.275), by look: the
        /// zombie bodies (Core/NativeZombieAnatomy, the zone kinds' classic bodies) are narrower across the chest than the pilot the
        /// rig was built for, and their shoulder blocks start at the clavicle, so without this the arms hang off the torso with a
        /// gap. Each value puts the shoulder block's inner face half a centimetre into the chest of the narrowest add the per-add
        /// proportions make (Tests/ZombieLocoChecks measures it on the built meshes). 0: the body already meets its shoulders.
        /// </summary>
        private static readonly float[][] insets={
            new[]{.078f,.078f,.078f},// shambler
            new[]{.067f,.067f},      // runner
            new[]{0f,0f},            // brute
            new[]{.072f,.105f},      // spitter
            new[]{.1f,.1f},          // crawler (its trot stance: CrawlerPose)
            new[]{.012f,.012f},      // bloater
            new[]{0f,0f},            // Hulk
            // the zone kinds (Core/NativeHordeZones order): the City Ironworks' cinderling, stoker, slagback, ladleman and rigger,
            // the Central Station's linesman, conductor, undertow and pileup
            new[]{.084f,.064f},new[]{0f},new[]{0f},new[]{0f},new[]{.032f},new[]{0f},new[]{.045f},new[]{.096f},new[]{0f}};
        /// <summary>The clavicles' inset for a kind and look (-1: the kind's deepest, for poses that do not know the look).</summary>
        public static float ShoulderInset(int kind,int look=-1)
        {
            kind=NativeHordeKinds.Index(kind);if(kind>=insets.Length)return 0;
            var k=insets[kind];if(look>=0&&look<k.Length)return k[look];float most=0;foreach(float v in k)most=Math.Max(most,v);return most;
        }
        /// <summary>The rig's clavicle (x, rig metres from the chest's middle) and its height on the chest.</summary>
        public const float ClavicleX=.275f,ClavicleY=.18f;
        /// <summary>Sets a pose's clavicles in by `inset` (call after a reset, before the arms are solved).</summary>
        public static void Shoulders(CapturedRig.Pose p,float inset)
        {
            if(inset<=0)return;float x=ClavicleX-inset;p.Position[CapturedRig.LClav]=new Vector3(x,ClavicleY,0);p.Position[CapturedRig.RClav]=new Vector3(-x,ClavicleY,0);
        }
    }
}
