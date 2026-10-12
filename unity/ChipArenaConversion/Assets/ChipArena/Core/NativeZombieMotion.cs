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
    /// <item>Attacks: the swipe on the striking arm's takes, the runner's frenzied claws, the brute's two-handed hammer; the
    /// spitter's spit, the bloater's belly and the crawler's pounce stay procedural (no capture fits them).</item>
    /// <item>Deaths fall the way the killing blow pushes (back, front, to either side), a take per add.</item>
    /// </list>
    /// </summary>
    public static class NativeZombieMotion
    {
        private static int[][] walks,runs,idles;private static int[] swipeR,swipeL,lunge,smash,knock,deathBack,deathFront,deathLeft,deathRight,lands;
        public static int Wake,CrawlStand,StandCrawl,JumpStart,JumpAir;
        private static float[] ground;private static bool ready;
        /// <summary>Builds the tables and decodes every clip once (so no frame ever decodes one; the first gait calls it).</summary>
        public static void Warm()
        {
            if(ready)return;
            int[] N(params string[] keys){var r=new int[keys.Length];for(int i=0;i<keys.Length;i++)r[i]=NativeZombieClips.Need(keys[i]);return r;}
            walks=new[]{N("chase-1","chase-2","chase-5"),N("sprint-1","sprint-3"),N("walk-4","walk-6"),N("chase-1","walk-6"),new int[0],N("walk-6","walk-4")};
            runs=new[]{N("sprint-1","sprint-3"),N("sprint-3","sprint-4"),N("chase-5","chase-2"),N("chase-2","sprint-1"),new int[0],N("chase-1")};
            idles=new[]{N("shamble-1","shamble-2","idle-3"),N("shamble-2","idle-3"),N("idle-3","shamble-1"),N("shamble-1","idle-3"),new int[0],N("idle-3")};
            swipeR=N("swipe-r1","swipe-r2");swipeL=N("swipe-l1","swipe-l2");lunge=N("lunge-1","lunge-2");smash=N("smash-1","smash-2");knock=N("knock-1","knock-2","knock-3","knock-4","knock-5");
            deathBack=N("death-back-1","death-back-2","death-back-3");deathFront=N("death-front-1","death-front-2","death-front-3");deathLeft=N("death-left-1");deathRight=N("death-right-1","death-right-2");
            lands=N("jump-land");Wake=NativeZombieClips.Need("wake");CrawlStand=NativeZombieClips.Need("crawl-stand");StandCrawl=NativeZombieClips.Need("stand-crawl");
            JumpStart=NativeZombieClips.Need("jump-start");JumpAir=NativeZombieClips.Need("jump-air");
            for(int i=0;i<NativeZombieClips.Count;i++)NativeZombieClips.Clip(i).Decode();
            // Each clip's planted ankle height (rig frame, the root at the body): where its feet stand, for the swing's lift.
            ground=new float[NativeZombieClips.Count];var row=new float[NativeZombieClips.Channels];var p=new CapturedRig.Pose();
            for(int i=0;i<ground.Length;i++)
            {
                var c=NativeZombieClips.Clip(i);int n=8;float sum=0;
                for(int k=0;k<n;k++){c.Sample(c.Duration*k/n,row);NativeZombieClips.Pose(row,p,false);p.World();sum+=Math.Min(p.WorldPosition[CapturedRig.LFoot].Y,p.WorldPosition[CapturedRig.RFoot].Y);}
                ground[i]=sum/n;
            }
            ready=true;
        }
        /// <summary>Whether a kind walks on the capture (all but the crawler, whose four-limbed trot stays procedural).</summary>
        public static bool Captured(int kind){Warm();return kind>=0&&kind<walks.Length&&walks[kind].Length>0;}
        private static int K(int kind)=>Math.Max(0,Math.Min(NativeHordeKinds.Count-1,kind));
        /// <summary>One of a set by a seed's hash (stable for the add's life).</summary>
        public static int Pick(int[] set,uint seed,int salt)=>set.Length==0?-1:set[Math.Min(set.Length-1,(int)(NativeHordeKinds.Hash(seed,salt)*set.Length))];
        public static int Walk(int kind,uint seed){Warm();return Pick(walks[K(kind)],seed,100);}
        public static int Run(int kind,uint seed){Warm();return Pick(runs[K(kind)],seed,101);}
        public static int Idle(int kind,uint seed){Warm();return Pick(idles[K(kind)],seed,102);}
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
        /// <summary>A kind's tempo: the brute and the bloater take longer, heavier strides at a slower cadence.</summary>
        public static readonly float[] Tempo={1,1,.9f,.95f,1,.85f};
        public const float MinRate=.55f,MaxRate=2.8f,Exponent=.7f;
        /// <summary>The playback rate of cycle `clip` at ground speed `v` (m/s) for `kind`.</summary>
        public static float Rate(int clip,float v,int kind)
        {
            float v0=Math.Max(.08f,NativeZombieClips.Speed(clip)),r=Tempo[K(kind)]*(float)Math.Pow(Math.Max(.25f,v)/v0,Exponent);
            return r<MinRate?MinRate:r>MaxRate?MaxRate:r;
        }
        /// <summary>A cycle's cadence at ground speed `v` (cycles per second).</summary>
        public static float Cadence(int clip,float v,int kind)=>Rate(clip,v,kind)/Math.Max(.1f,NativeZombieClips.Duration(clip));
        /// <summary>The share of the cycle a foot is planted (0 left, 1 right), kept in a range the planted feet can walk on.</summary>
        public static float Duty(int clip,int foot)
        {
            float d=foot==0?NativeZombieClips.LeftLift(clip):Frac(NativeZombieClips.RightLift(clip)-NativeZombieClips.RightLand(clip));
            bool sprint=NativeZombieClips.Speed(clip)>1;return Math.Max(sprint?.2f:.5f,Math.Min(sprint?.42f:.7f,d));
        }
        /// <summary>The phase offset of a foot (the left strikes at 0, the right at its own strike, kept within a tenth of the
        /// half cycle: a capture's dragged foot reads as a late strike, which planted feet would turn into a hop).</summary>
        public static float Offset(int clip,int foot)=>foot==0?0:Math.Max(.4f,Math.Min(.6f,Frac(1-NativeZombieClips.RightLand(clip))));
        private static float Frac(float x)=>x-(float)Math.Floor(x);
    }
}
