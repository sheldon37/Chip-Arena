using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Which zombie mocap clips (Core/NativeZombieClips) each Siren minion kind moves on, and how a clip follows the add's real
    /// ground speed (owner 2026-10-12: "Sirens minions need to look more zombie like ... Their animations also look a lot more
    /// simple then what we used to have? We do have the zombie mo-cap ones we could utilize ... (Especially there walk
    /// animations, its not great)"). Presentation only: nothing here reads or writes the authority.
    /// <list type="bullet">
    /// <item>Each kind has walk, run and idle sets; an add takes one of each by its seed, so a crowd lurches on a mix of the
    /// pack's takes (<see cref="Pick"/>).</item>
    /// <item>A cycle plays at <see cref="Rate"/>: its natural speed on the rig scaled toward the add's speed by the square
    /// power .7 (cadence takes most of the change and the stride the rest, so the planted feet stay under the hips), so at the authority's 1.5-4.6 m/s the feet still plant
    /// (the gait's planted feet take the stride: Core/NativeHordeMotion).</item>
    /// <item>Attacks: the swipe on the striking arm's takes, the runner's frenzied claws, the brute's and the Hulk's two-handed
    /// hammer; the spitter rears back on the start of a knock-back reel (<see cref="SpitRear"/>) before its procedural spit, the
    /// bloater's belly and the crawler's pounce stay procedural (no capture fits them).</item>
    /// <item>The Hulk plods on the slow walks at a heavy tempo and roars on the flung-arms start of another reel (<see cref="Roar"/>).</item>
    /// <item>Deaths fall the way the killing blow pushes (back, front, to either side), a take per add.</item>
    /// <item>The town zombies' wander (Core/NativeHordeRoam) shambles on the pack's slow forward walk whatever the kind's chase
    /// takes are (<see cref="Wander"/>): the runner's sprints and the shambler's chases never play at a wander's pace.</item>
    /// </list>
    /// </summary>
    public static class NativeZombieMotion
    {
        private static int[][] walks,runs,idles,wanders;private static int[] swipeR,swipeL,lunge,smash,knock,deathBack,deathFront,deathLeft,deathRight,lands;
        public static int Wake,CrawlStand,StandCrawl,JumpStart,JumpAir,SpitRear,Roar;
        private static float[] ground;private static bool ready;
        /// <summary>Builds the tables and decodes every clip once (so no frame ever decodes one; the first gait calls it).</summary>
        public static void Warm()
        {
            if(ready)return;
            int[] N(params string[] keys){var r=new int[keys.Length];for(int i=0;i<keys.Length;i++)r[i]=NativeZombieClips.Need(keys[i]);return r;}
            // (The locomotion pass, docs/ZOMBIE-LOCOMOTION.md: the takes whose trunk and arms stay smoothest at each kind's cadence.
            // The shambler reaches on the chases, the full chase loop among them; the runner sprints reaching or low-armed (not
            // sprint-4, which throws its shoulders furthest); the brute, spitter and bloater walk on the full loop of the heavy walk;
            // sprint-1, the jerkiest, and the shambler's bouncing chase-5 are left to the kinds that still name them.)
            // (Every kind that runs at a real speed runs on the clean sprints, sprint-2 and sprint-3, about 3.7 and 4.5 m/s as captured
            // on the model's legs, at their own pace and on their own stride: owner 2026-11-03, "they are still doing awkward lunges
            // when running", NativeZombieMotion.Rate, NativeHordeGait.LandAhead and OwnSwing. The walking chases were captured under
            // 1 m/s and only the slow bloater still runs on them, its speed theirs; the Hulk keeps its own heavy stride.)
            // (The walks nearest the authority's speeds (owner 2026-11-04, "preserve the original animation pack's natural movement"): on the
            // model's legs the chase-2 and chase-5 takes make .87 to .92 m/s as captured, the chase-1, walk-4 and walk-6 takes .46 to .65, and
            // the shambler, brute and spitter walk at 1.3 to 1.6: a slow take has to be sped up or stretched twice as far to keep up. Of the walks
            // chase-2 holds its shape best at those speeds, and walk-6 well enough up to the shambler's 1.5. Seen in Unity on the comparison, chase-5-full's
            // long stride drops into the lunge the owner reported at this pace; the others have to be stretched past half as far again.
            // docs/ZOMBIE-LOCOMOTION.md has the stage measurements.)
            walks=new[]{N("chase-2","walk-6"),N("sprint-3","sprint-2"),N("walk-6","chase-2"),N("chase-2"),new int[0],N("walk-6-full","walk-4"),N("walk-4","walk-6")};
            runs=new[]{N("sprint-2","sprint-3"),N("sprint-3","sprint-2"),N("sprint-2"),N("sprint-2","sprint-3"),new int[0],N("chase-1","chase-2"),N("chase-5")};
            idles=new[]{N("shamble-1","shamble-2","idle-3"),N("shamble-2","idle-3"),N("idle-3","shamble-1"),N("shamble-1","idle-3"),new int[0],N("idle-3"),N("idle-3","shamble-1")};
            // (The slow forward walk-4 is the take that plants cleanly at the wander's .4-1.1 m/s for every kind: the walk-6 and the
            // chases drag a planted ankle up to 5-7 cm there.)
            wanders=new[]{N("walk-4"),N("walk-4"),N("walk-4"),N("walk-4"),new int[0],N("walk-4"),N("walk-4")};
            // The zone kinds' own takes after the Siren's (Core/NativeHordeZones; the Siren's sets unchanged).
            walks=NativeHordeZones.Takes(walks,z=>z.Walks);runs=NativeHordeZones.Takes(runs,z=>z.Runs);idles=NativeHordeZones.Takes(idles,z=>z.Idles);wanders=NativeHordeZones.Takes(wanders,z=>z.Wanders);
            swipeR=N("swipe-r1","swipe-r2");swipeL=N("swipe-l1","swipe-l2");lunge=N("lunge-1","lunge-2");smash=N("smash-1","smash-2");knock=N("knock-1","knock-2","knock-3","knock-4","knock-5");
            deathBack=N("death-back-1","death-back-2","death-back-3");deathFront=N("death-front-1","death-front-2","death-front-3");deathLeft=N("death-left-1");deathRight=N("death-right-1","death-right-2");
            lands=N("jump-land");Wake=NativeZombieClips.Need("wake");CrawlStand=NativeZombieClips.Need("crawl-stand");StandCrawl=NativeZombieClips.Need("stand-crawl");
            JumpStart=NativeZombieClips.Need("jump-start");JumpAir=NativeZombieClips.Need("jump-air");SpitRear=NativeZombieClips.Need("knock-1");Roar=NativeZombieClips.Need("knock-3");
            for(int i=0;i<NativeZombieClips.Count;i++)NativeZombieClips.Clip(i).Decode();
            // Each cycle's mean row (32 samples over its loop): the pose a calmed take swings about.
            means=new float[NativeZombieClips.Count][];
            {var sample=new float[NativeZombieClips.Channels];for(int i=0;i<means.Length;i++){var c=NativeZombieClips.Clip(i);if(!c.Loop)continue;var m=new float[NativeZombieClips.Channels];const int n=32;
                for(int k=0;k<n;k++){c.Sample(c.Duration*k/n,sample);for(int ch=0;ch<m.Length;ch++)m[ch]+=sample[ch]/n;}means[i]=m;}}
            // Each clip's planted ankle height (rig frame, the root at the body): where its feet stand, for the swing's lift.
            ground=new float[NativeZombieClips.Count];var row=new float[NativeZombieClips.Channels];var p=new CapturedRig.Pose();
            for(int i=0;i<ground.Length;i++)
            {
                var c=NativeZombieClips.Clip(i);int n=8;float sum=0;
                for(int k=0;k<n;k++){c.Sample(c.Duration*k/n,row);NativeZombieClips.Pose(row,p,false);p.World();sum+=Math.Min(p.WorldPosition[CapturedRig.LFoot].Y,p.WorldPosition[CapturedRig.RFoot].Y);}
                ground[i]=sum/n;
            }
            Feet();
            ready=true;
        }
        /// <summary>Whether a kind walks on the capture (all but the crawler, whose four-limbed trot stays procedural).</summary>
        public static bool Captured(int kind){Warm();return kind>=0&&kind<walks.Length&&walks[kind].Length>0;}
        private static int K(int kind)=>NativeHordeKinds.Index(kind);
        /// <summary>One of a set by a seed's hash (stable for the add's life).</summary>
        public static int Pick(int[] set,uint seed,int salt)=>set.Length==0?-1:set[Math.Min(set.Length-1,(int)(NativeHordeKinds.Hash(seed,salt)*set.Length))];
        public static int Walk(int kind,uint seed){Warm();return Pick(walks[K(kind)],seed,100);}
        public static int Run(int kind,uint seed){Warm();return Pick(runs[K(kind)],seed,101);}
        public static int Idle(int kind,uint seed){Warm();return Pick(idles[K(kind)],seed,102);}
        /// <summary>The slow walk a town zombie wanders on (-1: none, the crawler).</summary>
        public static int Wander(int kind,uint seed){Warm();return Pick(wanders[K(kind)],seed,109);}
        public static int[] Wanders(int kind){Warm();return wanders[K(kind)];}
        /// <summary>The kind's walk and run sets (the checks and the gallery read them).</summary>
        public static int[] Walks(int kind){Warm();return walks[K(kind)];}
        public static int[] Runs(int kind){Warm();return runs[K(kind)];}
        public static int[] Idles(int kind){Warm();return idles[K(kind)];}
        /// <summary>The clip a blow plays (-1: the procedural attack): `side` 1 the right arm, `pick` hashed from its start.</summary>
        public static int Attack(NativeHordeKinds.Attack attack,int side,uint pick)
        {
            Warm();
            switch(attack)
            {
                case NativeHordeKinds.Attack.Swipe:return Pick(side>=0?swipeR:swipeL,pick,103);
                case NativeHordeKinds.Attack.Lunge:return Pick(lunge,pick,104);
                case NativeHordeKinds.Attack.Smash:return Pick(smash,pick,105);
            }
            return -1;
        }
        /// <summary>The knockback reel an authority stagger plays (by the hit's hash).</summary>
        public static int Knock(uint pick){Warm();return Pick(knock,pick,106);}
        /// <summary>The death take for a killing push (source frame, horizontal: +x the add's left, +z its forward).</summary>
        public static int Death(uint seed,Vector2 push)
        {
            Warm();var set=deathBack;
            if(push.LengthSquared()>1e-4f){if(Math.Abs(push.X)>Math.Abs(push.Y)*1.2f)set=push.X>0?deathLeft:deathRight;else set=push.Y>0?deathFront:deathBack;}
            return Pick(set,seed,107);
        }
        /// <summary>The brute's landing take for an add.</summary>
        public static int Land(uint seed){Warm();return Pick(lands,seed,108);}
        /// <summary>The planted ankle height of a clip (rig frame).</summary>
        public static float Ground(int clip){Warm();return ground[clip];}

        // ---- following the add's ground speed ----
        /// <summary>A kind's tempo: the brute and the bloater take longer, heavier strides at a slower cadence, the Hulk slowest.</summary>
        public static readonly float[] Tempo={1,1,.9f,.95f,1,.85f,.62f};
        public const float MinRate=.55f,MaxRate=2.8f;
        /// <summary>How far a sprint's playback strays from its captured pace on the model's legs: past these the stride takes the rest.</summary>
        public const float OwnPaceLow=.75f;
        /// <summary>The share of a speed past a take's own its cadence takes (the stride the rest), and the longest a stride stretches
        /// over the capture's before the cadence takes everything further.</summary>
        public const float OwnPaceShare=.5f,OwnStretchMost=1.55f;
        /// <summary>
        /// The playback rate of cycle `clip` at ground speed `v` (rig m/s) for `kind`: the body's cadence (NativeHordeGait.StepRate,
        /// two steps a cycle) over the take's own, so each take is time-warped to the kind's step and the stride takes the rest of
        /// the speed. (The shipped mapping scaled each take's own pace by the speed's .7 power, which played the pack's .3-.5 m/s
        /// shambles at 2-2.8 times their speed at the authority's 1.3-1.6 m/s walks.)
        /// </summary>
        public static float Rate(int clip,float v,int kind,float legs=1)
        {
            // A take on the model's legs plays at its own captured pace (owner 2026-11-03: "they are still doing awkward lunges when running ... The
            // animation pack doesn't look like that at all"; 2026-11-04: "preserve the original animation pack's natural movement"): the rate that makes
            // its captured ground speed, at the model's scale, the add's. The body's cadence below (StepRate, by the speed alone) stretched
            // strides to up to 2.5 times the capture's. The voxel rig's short legs stay on it: they cannot carry these speeds on a take's own stride.
            // Past its own speed a take shares what is left between its cadence and its stride (OwnPaceShare), as a person walking faster
            // does: neither a take played at twice its pace nor one stretched to twice its stride reads as the capture.
            if(OwnStride(clip,legs))
            {
                float own=v/(NativeZombieClips.Speed(clip)*legs);if(own<=1)return own<OwnPaceLow?OwnPaceLow:own;
                float pace=(float)Math.Pow(own,OwnPaceShare);pace=Math.Max(pace,own/OwnStretchMost);return pace>MaxRate?MaxRate:pace;
            }
            float r=NativeHordeGait.StepRate(kind,Math.Max(.12f,v),legs)*.5f*Math.Max(.1f,NativeZombieClips.Duration(clip));
            return r<MinRate?MinRate:r>MaxRate?MaxRate:r;
        }
        /// <summary>Whether a take runs on its own pace, its feet on its own paths, on legs `legs` times the rig's: every loop take with a stride, on the model's.</summary>
        public static bool OwnStride(int clip,float legs)=>legs>1.001f&&clip>=0&&NativeZombieClips.Clip(clip).Loop&&NativeZombieClips.Speed(clip)>0;
        /// <summary>A cycle's mean row over its loop (the pose it swings about; NativeHordeGait.Calm), null for a one-shot.</summary>
        public static float[] Mean(int clip){Warm();return clip>=0&&clip<means.Length?means[clip]:null;}
        private static float[][] means;
        /// <summary>A cycle's cadence at ground speed `v` (cycles per second) on legs `legs` times the rig's (NativeHordeGait.Legs).</summary>
        public static float Cadence(int clip,float v,int kind,float legs=1)=>Rate(clip,v,kind,legs)/Math.Max(.1f,NativeZombieClips.Duration(clip));
        /// <summary>The share of the gait's cycle a foot is planted (0 left, 1 right), kept in a range the planted feet can walk on:
        /// a sprint's its own stance (its foot from its strike to most back) on the warped cycle, a walk's the exporter's lift marks.
        /// (The sprints' marks lift each foot a fifth of a cycle after it lands: a run in the air 60% of the time, bounding rather
        /// than running. The walks' stances stand as they were: the legs' reach shortens them at speed, and a longer one measured
        /// off the limping takes kept a foot down behind a stopping add while its other foot stepped.)</summary>
        public static float Duty(int clip,int foot)
        {
            Warm();bool sprint=NativeZombieClips.Speed(clip)>1;
            float d=sprint?stance[clip*2+foot]:foot==0?NativeZombieClips.LeftLift(clip):Frac(NativeZombieClips.RightLift(clip)-NativeZombieClips.RightLand(clip));return Math.Max(sprint?.2f:.5f,Math.Min(sprint?.42f:.7f,d));
        }
        /// <summary>The share of the cycle a take on its own stride keeps foot `foot` still in the world: the stretch it stands still in the
        /// capture (Planted), no longer than the hips take over the ground it covers about the hip. (A body travelling at the take's speed
        /// outruns the capture's foot late in that stretch: held to the end the trailing foot goes further back than the capture's ever does.)</summary>
        public static float OwnDuty(int clip,int foot)
        {
            Warm();int at=clip*2+foot;float d=Math.Min(Frac(contactOff[at]-contactOn[at]),(touchFoot[at]-liftFoot[at])/Math.Max(.05f,OwnStrideOf(clip)));
            return d<OwnDutyLow?OwnDutyLow:d>OwnDutyHigh?OwnDutyHigh:d;
        }
        public const float OwnDutyLow=.12f,OwnDutyHigh=.8f;
        /// <summary>The take's own stride a cycle (rig metres).</summary>
        public static float OwnStrideOf(int clip)=>NativeZombieClips.Speed(clip)*NativeZombieClips.Duration(clip);
        /// <summary>How far a take's foot paths about the hip are stretched at `stride` rig metres a cycle: the stride over the capture's
        /// past it; 1 down to OwnPaceLow of it; less below, a body setting off keeping its short steps under it.</summary>
        public static float OwnSpan(int clip,float stride){float k=stride/Math.Max(.01f,OwnStrideOf(clip));return k>=1?k:Math.Min(1,k/OwnPaceLow);}
        /// <summary>The take phase (0..1) foot `foot` comes down and leaves the floor, as captured.</summary>
        public static float ContactOn(int clip,int foot){Warm();return contactOn[clip*2+foot];}
        public static float ContactOff(int clip,int foot){Warm();return contactOff[clip*2+foot];}
        /// <summary>How far ahead of the body (rig metres: its hip, with the hips' sway about their own middle) foot `foot` comes down and leaves the floor, as captured.</summary>
        public static float TouchOf(int clip,int foot){Warm();return touchFoot[clip*2+foot];}
        public static float LiftOf(int clip,int foot){Warm();return liftFoot[clip*2+foot];}
        /// <summary>The furthest behind the body the take ever carries foot `foot` (rig metres, negative).</summary>
        public static float OwnBack(int clip,int foot){Warm();return ownBack[clip*2+foot];}
        /// <summary>How far outside its hip (rig metres) a take stands foot `foot` through its stance.</summary>
        public static float StandOut(int clip,int foot){Warm();return standOut[clip*2+foot];}
        /// <summary>How far (rad) the take's foot has rolled onto its ball as it leaves the floor.</summary>
        public static float RollOf(int clip,int foot){Warm();return rollFoot[clip*2+foot];}
        /// <summary>A take's foot at phase `phase` of its cycle, as captured: its height over the take's planted ankle and how far ahead of
        /// its hip (rig metres). Between its stances the gait follows these phase for phase.</summary>
        public static float OwnLift(int clip,int foot,float phase)
        {
            Warm();float x=Frac(phase)*OwnSteps;int j=Math.Min(OwnSteps-1,(int)x),at=(clip*2+foot)*OwnSteps;
            return ownLift[at+j]+(ownLift[at+(j+1)%OwnSteps]-ownLift[at+j])*(x-j);
        }
        public static float OwnAhead(int clip,int foot,float phase)
        {
            Warm();float x=Frac(phase)*OwnSteps;int j=Math.Min(OwnSteps-1,(int)x),at=(clip*2+foot)*OwnSteps;
            return ownAhead[at+j]+(ownAhead[at+(j+1)%OwnSteps]-ownAhead[at+j])*(x-j);
        }
        private const int OwnSteps=60;
        /// <summary>How far the capture's planted ankle stands from the rig's floor point (rig metres; + the take's ankle stands lower than
        /// the floor's, so its hips stand this much higher over its floor): what puts the take's feet on the floor at its own hips.</summary>
        public static float FloorRise(int clip,float floorAnkle){Warm();return floorAnkle-ground[clip];}
        /// <summary>The phase offset of a foot on the gait's cycle: the left strikes at 0, the right half a cycle later on every
        /// take (<see cref="Warp"/> times the take's own strikes to them).</summary>
        public static float Offset(int clip,int foot)=>foot==0?0:.5f;
        private static float Frac(float x)=>x-(float)Math.Floor(x);

        // ---- the takes' own feet, and the warp that steps them in time ----
        // (Owner 2026-10-30: "Zombie run looks a little weird at the moment, sometimes their legs are completely out of sync and
        // move weird directions." Measured on the takes' own legs: the exporter's right-foot marks miss on several takes (chase-1-full
        // strikes its right foot at .49 of the cycle, marked .19), and the limping takes strike it at .7 (walk-4) and .75 (chase-5,
        // chase-5-full). The gait plants the right foot half a cycle after the left, clamped to .4-.6 of the marks, so the trunk,
        // arms and the swing's lift played up to a third of a cycle out of step with the feet.)
        private static float[] rightAt,warpGain,stance,swingLift,swingAhead,contactOn,contactOff,touchFoot,liftFoot,standOut,rollFoot,ownLift,ownAhead,ownBack;
        /// <summary>The farthest the warp moves the right foot's strike (cycles): the warped take never plays slower than about a
        /// third of its pace through the cycle.</summary>
        public const float WarpMost=.28f;
        private const int WarpSteps=64,FootSamples=120,SwingSteps=24;
        // The warp's shape: its rate over the cycle 1 + g·sq(P) (sq a soft square wave, +1 through the first half and -1 the second),
        // cumulated; WarpHalf, its area over the first half, so a gain k/WarpHalf moves the half cycle by k.
        private static readonly float[] warpShape=Shape();public static readonly float WarpHalf=warpShape[WarpSteps/2];
        private static float[] Shape()
        {
            var c=new float[WarpSteps+1];const int sub=8;double sum=0;
            for(int j=0;j<WarpSteps;j++){for(int q=0;q<sub;q++){double x=(j+(q+.5)/sub)/WarpSteps;sum+=Math.Tanh(3*Math.Sin(2*Math.PI*x))/Math.Tanh(3)/(WarpSteps*sub);}c[j+1]=(float)sum;}
            c[WarpSteps]=0;return c;
        }
        private static float ShapeAt(float p){p=Frac(p)*WarpSteps;int j=Math.Min(WarpSteps-1,(int)p);return warpShape[j]+(warpShape[j+1]-warpShape[j])*(p-j);}
        /// <summary>The take's own phase for the gait's phase `phase` (cycles): the left foot's strike (the cycle's start) at the
        /// gait's 0, the right's at its .5, the take's pace eased between them (a symmetric take plays as captured). Continuous
        /// over the cycles.</summary>
        public static float Warp(int clip,float phase)=>clip<0||warpGain==null?phase:phase+warpGain[clip]*ShapeAt(phase);
        /// <summary>The take's own phase with its right foot's strike at `right` of the gait's cycle instead of the half: a take on its own
        /// plays as captured (its own strike asked of it: no warp); two takes blending meet between their own.</summary>
        public static float WarpTo(int clip,float phase,float right){if(clip<0||rightAt==null)return phase;float g=Math.Max(-WarpMost,Math.Min(WarpMost,rightAt[clip]-right))/WarpHalf;return phase+g*ShapeAt(phase);}
        /// <summary>The take's right-foot strike after its left's (cycles).</summary>
        public static float RightAt(int clip){Warm();return rightAt[clip];}
        /// <summary>The gait's phase (0..1) the take's own phase `take` plays at (<see cref="Warp"/>'s inverse).</summary>
        public static float Unwarp(int clip,float take)
        {
            float x=Frac(take),g=warpGain[clip],lo=0,hi=1;
            for(int n=0;n<24;n++){float m=(lo+hi)*.5f;if(m+g*ShapeAt(m)<x)lo=m;else hi=m;}
            return (lo+hi)*.5f;
        }
        /// <summary>How high (rig metres over the take's planted ankle) a loop take carries foot `foot` at `s` (0..1) through its own
        /// swing, most back to most forward: the gait lifts its swinging foot on the take's own arc, whatever share of the gait's
        /// cycle the swing takes. (A walking take run fast plants its feet for less of the gait's cycle than of its own, and its
        /// foot read at the gait's phase still stood low for the first third of the gait's swing: a dragged, late-kicking stride.)
        /// The arc eases off the floor and back onto it over the swing's first and last SwingEase (a sprint's foot is up already
        /// at its most back and still up at its most forward).</summary>
        public static float SwingLift(int clip,int foot,float s)
        {
            Warm();if(clip<0)return 0;s=Math.Max(0,Math.Min(1,s));float x=s*(SwingSteps-1);int j=Math.Min(SwingSteps-2,(int)x),at=(clip*2+foot)*SwingSteps+j;
            float up=Smooth(s/SwingEase)*Smooth((1-s)/SwingEase);
            return (swingLift[at]+(swingLift[at+1]-swingLift[at])*(x-j))*up;
        }
        public const float SwingEase=.25f;
        /// <summary>How far ahead of its hip (rig metres, negative behind) a loop take carries foot `foot` at `s` (0..1) through its own
        /// swing, most back to most forward. A sprint's foot follows this about the body: it turns forward the moment it leaves the floor,
        /// rather than easing off the spot it stood on while the hips run on and leave it trailing a whole leg behind.</summary>
        public static float SwingAhead(int clip,int foot,float s)
        {
            Warm();s=Math.Max(0,Math.Min(1,s));float x=s*(SwingSteps-1);int j=Math.Min(SwingSteps-2,(int)x),at=(clip*2+foot)*SwingSteps+j;
            return swingAhead[at]+(swingAhead[at+1]-swingAhead[at])*(x-j);
        }
        /// <summary>The share of a sprint's own swing path a stride `stride` (metres a cycle) on legs `legs` carries: all of it down to
        /// OwnPaceLow of the take's pace, less below (a body setting off on a sprint keeps its short steps under it).</summary>
        /// <summary>A planted foot on its take's own stride rolls onto its ball as the take's does (rad, at `q` 0..1 through the stance, to `most`): flat to OwnRollFrom
        /// of the stance, then the heel peeling up to OwnRollMost by the lift. (Measured on the sprints: the ankle stands about .08 over the
        /// toe as it leaves the floor.) So the knee stays bent through the push, where a flat foot straightens the leg behind.</summary>
        public static float OwnRoll(float q,float most){float x=(q-OwnRollFrom)/(1-OwnRollFrom);x=x<0?0:x>1?1:x;return most*x*x*(3-2*x);}
        public const float OwnRollFrom=.3f;
        private static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        /// <summary>The take phase (0..1) of a loop take's foot strike: the left's the cycle's start (the exporter cuts the loops
        /// there), the right's the left's plus the measured time between the feet coming most forward under the hips.</summary>
        public static float Strike(int clip,int foot){Warm();return foot==0?0:rightAt[clip];}
        // Each loop take's feet sampled over its cycle as captured (the take's own legs, no planting): when the right foot strikes
        // after the left (the time between the two coming most forward, which reads cleanly on every take; the feet's heights do
        // not on the dragging walks, and the exporter's right-foot marks miss on several), the warp that times it to the gait's half
        // cycle as far as WarpMost moves it, and each foot's stance (from its strike to most back) on the gait's cycle.
        private static void Feet()
        {
            int count=NativeZombieClips.Count;rightAt=new float[count];warpGain=new float[count];stance=new float[count*2];swingLift=new float[count*2*SwingSteps];swingAhead=new float[count*2*SwingSteps];
            contactOn=new float[count*2];contactOff=new float[count*2];touchFoot=new float[count*2];liftFoot=new float[count*2];standOut=new float[count*2];rollFoot=new float[count*2];ownLift=new float[count*2*OwnSteps];ownAhead=new float[count*2*OwnSteps];ownBack=new float[count*2];
            var row=new float[NativeZombieClips.Channels];var p=new CapturedRig.Pose();var z=new float[2,FootSamples];
            for(int i=0;i<count;i++)
            {
                stance[i*2]=stance[i*2+1]=.6f;if(!NativeZombieClips.Clip(i).Loop)continue;
                for(int k=0;k<FootSamples;k++){NativeZombieClips.SamplePhase(i,k/(float)FootSamples,row);NativeZombieClips.Pose(row,p,false);p.World();z[0,k]=p.WorldPosition[CapturedRig.LFoot].Z;z[1,k]=p.WorldPosition[CapturedRig.RFoot].Z;}
                float lFore=Extreme(z,0,1),rFore=Extreme(z,1,1),lBack=Extreme(z,0,-1),rBack=Extreme(z,1,-1);
                rightAt[i]=Frac(rFore-lFore);warpGain[i]=Math.Max(-WarpMost,Math.Min(WarpMost,rightAt[i]-.5f))/WarpHalf;
                stance[i*2]=Unwarp(i,lBack);stance[i*2+1]=Frac(Unwarp(i,rBack)-.5f);
                // Each foot's swing, most back to most forward, at SwingSteps points: its height over the take's planted ankle.
                for(int foot=0;foot<2;foot++)
                {
                    float from=foot==0?lBack:rBack,span=Frac((foot==0?lFore:rFore)-from);
                    for(int k=0;k<SwingSteps;k++)
                    {
                        NativeZombieClips.SamplePhase(i,from+span*k/(SwingSteps-1),row);NativeZombieClips.Pose(row,p,false);p.World();
                        swingLift[(i*2+foot)*SwingSteps+k]=Math.Max(0,p.WorldPosition[foot==0?CapturedRig.LFoot:CapturedRig.RFoot].Y-ground[i]);
                        swingAhead[(i*2+foot)*SwingSteps+k]=p.WorldPosition[foot==0?CapturedRig.LFoot:CapturedRig.RFoot].Z-p.WorldPosition[foot==0?CapturedRig.LHip:CapturedRig.RHip].Z;
                    }
                }
                if(NativeZombieClips.Speed(i)>0)Planted(i,row,p);
            }
        }
        // Each loop take's feet in the world as the take moves at its own speed: where each stands still is its contact (its world speed
        // within ContactSpeed of the take's, its ankle low; a brief lapse, the capture's noise, bridged), the longest such stretch of its
        // cycle. Past that the capture's foot slides on with the body, on its toe or dragged: the gait follows it there as it does through the
        // swing. (Read from the ankle's height alone a stance ran on through that slide: held still in the world to the same place, the
        // trailing foot was left half a leg further behind the hips than the capture's ever is.)
        private const float ContactSpeed=.6f,ContactLeast=.45f,ContactHigh=.09f,ContactBridge=.08f;
        private static void Planted(int clip,float[] row,CapturedRig.Pose p)
        {
            var rel=new float[FootSamples];var up=new float[FootSamples];var still=new bool[FootSamples];float speed=NativeZombieClips.Speed(clip),time=NativeZombieClips.Duration(clip);
            for(int foot=0;foot<2;foot++)
            {
                int hip=foot==0?CapturedRig.LHip:CapturedRig.RHip,at=clip*2+foot;float lo=float.MaxValue;
                for(int k=0;k<FootSamples;k++)
                {
                    NativeZombieClips.SamplePhase(clip,k/(float)FootSamples,row);NativeZombieClips.Pose(row,p,false);p.World();
                    rel[k]=p.WorldPosition[hip+2].Z-p.WorldPosition[hip].Z+row[3]-means[clip][3];up[k]=p.WorldPosition[hip+2].Y;lo=Math.Min(lo,up[k]);
                }
                float most=Math.Max(ContactLeast,ContactSpeed*speed);
                for(int k=0;k<FootSamples;k++)
                {
                    int a=(k+FootSamples-2)%FootSamples,b=(k+2)%FootSamples;float world=(rel[b]-rel[a])/(4*time/FootSamples)+speed;
                    still[k]=Math.Abs(world)<=most&&up[k]<lo+ContactHigh;
                }
                // (Brief lapses bridged, then the longest stretch round the cycle.)
                int bridge=(int)(ContactBridge*FootSamples);
                for(int k=0;k<FootSamples;k++)if(still[k]&&!still[(k+1)%FootSamples]){for(int g=2;g<=bridge+1;g++)if(still[(k+g)%FootSamples]){for(int h=1;h<g;h++)still[(k+h)%FootSamples]=true;break;}}
                int from=0,length=0;
                for(int k=0;k<FootSamples;k++)
                {
                    if(!still[k]||still[(k+FootSamples-1)%FootSamples])continue;
                    int n=0;while(n<FootSamples&&still[(k+n)%FootSamples])n++;if(n>length){length=n;from=k;}
                }
                if(length==0||length>=FootSamples-1){int fore=0;for(int k=1;k<FootSamples;k++)if(rel[k]>rel[fore])fore=k;from=fore;length=(int)(.55f*FootSamples);}
                int to=(from+length)%FootSamples;
                contactOn[at]=from/(float)FootSamples;contactOff[at]=to/(float)FootSamples;touchFoot[at]=rel[from];liftFoot[at]=rel[to];
                // Where it stands out of its hip (the stretch's middle): the capture's stance width.
                float side=0;for(int k=0;k<4;k++){NativeZombieClips.SamplePhase(clip,(from+k*length/4)/(float)FootSamples,row);NativeZombieClips.Pose(row,p,false);p.World();float dx=p.WorldPosition[hip+2].X-p.WorldPosition[hip].X;side+=.25f*(p.WorldPosition[hip].X>0?dx:-dx);}
                standOut[at]=side;
                // Its roll as it leaves: the foot's pitch there over the pitch it stands at through the middle of the stance.
                float Pitch(int k){NativeZombieClips.SamplePhase(clip,k/(float)FootSamples,row);NativeZombieClips.Pose(row,p,false);p.World();var fw=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[hip+2]);return (float)Math.Atan2(-fw.Y,Math.Sqrt(fw.X*fw.X+fw.Z*fw.Z));}
                rollFoot[at]=Math.Max(0,Math.Min(1,Pitch(to)-Pitch((from+length/2)%FootSamples)));
                // The capture's foot about its hip through the cycle.
                for(int k=0;k<OwnSteps;k++)
                {
                    NativeZombieClips.SamplePhase(clip,k/(float)OwnSteps,row);NativeZombieClips.Pose(row,p,false);p.World();
                    ownLift[at*OwnSteps+k]=Math.Max(0,p.WorldPosition[hip+2].Y-ground[clip]);ownAhead[at*OwnSteps+k]=p.WorldPosition[hip+2].Z-p.WorldPosition[hip].Z+row[3]-means[clip][3];ownBack[at]=Math.Min(ownBack[at],ownAhead[at*OwnSteps+k]);
                }
            }
        }
        // The phase (0..1) of a sampled foot's most forward (`sign` 1) or most back (-1) place, refined between its samples.
        private static float Extreme(float[,] z,int foot,int sign)
        {
            int n=z.GetLength(1),best=0;for(int k=1;k<n;k++)if(sign*z[foot,k]>sign*z[foot,best])best=k;
            float a=z[foot,(best+n-1)%n],b=z[foot,best],c=z[foot,(best+1)%n],den=a-2*b+c,shift=Math.Abs(den)>1e-9f?Math.Max(-.5f,Math.Min(.5f,.5f*(a-c)/den)):0;
            return Frac((best+shift)/n);
        }
    }
}
