using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public static class NativeCityLayout
    {
        public const string Version = "meridian-b-candidate-3";
        // Candidate data for the owner walk. Slot order is always north, east, south, west.
        public sealed class Plan
        {
            public readonly string Name;
            public readonly uint Seed;
            internal Plan(string name, uint seed)
            {
                Name = name;
                Seed = seed;
            }
        }

        public static readonly IReadOnlyList<Plan> Plans = Array.AsReadOnly(new[] {
            new Plan("Crown Hill", 647), new Plan("The Navigation", 784),
            new Plan("The Arcades", 1058), new Plan("The Stride", 1332), new Plan("The Yards", 1606)
        });

        // Each pair is a single bay with two surveyed positions. Slot zero is on a roof.
        public static ExpeditionWorldAnchor[] CacheAnchors(Plan plan,uint worldSeed)
        {
            var bays=CacheBays(plan.Seed);
            var rng=new NativeExpeditionDeployment.Roller(NativePveRandom.Hash(worldSeed,"meridian-caches"));
            var anchors=new ExpeditionWorldAnchor[bays.Length/2];
            for(int i=0;i<anchors.Length;i++)
            {
                var point=bays[2*i+rng.Index(2)];
                anchors[i]=new ExpeditionWorldAnchor{id="exp-city:cache:"+(i==0?"roof":i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    kind="chest",area="exp-city",x=point.x,y=point.y,z=point.z,yaw=point.yaw};
            }
            return anchors;
        }
        private static ExpeditionPoint[] CacheBays(uint plan)
        {
            ExpeditionPoint P(float x,float y,float z,float yaw)=>new ExpeditionPoint{x=x,y=y,z=z,yaw=yaw};
            switch(plan)
            {
                case 647: return new[]{
                    P(58.3f,14f,80f,90f),P(58.3f,14f,76f,90f),
                    P(74f,28f,173.7f,180f),P(71.7f,28f,176f,270f),
                    P(72f,-7f,-81.7f,0f),P(68f,-7f,-81.7f,0f),
                    P(-4f,-7f,-81.7f,0f),P(0f,-7f,-79.7f,0f),
                    P(-100f,-7f,-81.7f,0f),P(-96f,-7f,-81.7f,0f),
                };
                case 784: return new[]{
                    P(-32f,0f,-76f,180f),P(-28f,0f,-76f,180f),
                    P(126f,0f,78.3f,0f),P(130f,0f,80.3f,0f),
                    P(126f,0f,-78.3f,180f),P(130f,0f,-78.3f,180f),
                    P(126f,0f,-141.7f,0f),P(130f,0f,-141.7f,0f),
                    P(-188f,0f,-76.3f,180f),P(-192f,0f,-78.3f,180f),
                };
                case 1058: return new[]{
                    P(-10f,7f,68.3f,0f),P(-14f,7f,68.3f,0f),
                    P(3.7f,0f,160f,270f),P(3.7f,0f,164f,270f),
                    P(114.3f,0f,0f,90f),P(112.3f,0f,-8f,90f),
                    P(148f,0f,-66.3f,180f),P(152f,0f,-68.3f,180f),
                    P(-198f,0f,72.3f,0f),P(-202f,0f,72.3f,0f),
                };
                case 1332: return new[]{
                    P(21.7f,7f,88f,270f),P(21.7f,7f,84f,270f),
                    P(-6f,7f,51.7f,180f),P(-10f,7f,51.7f,180f),
                    P(100f,7f,55.7f,180f),P(96f,7f,55.7f,180f),
                    P(-52f,7f,51.7f,180f),P(-56f,7f,51.7f,180f),
                    P(-100f,7f,51.7f,180f),P(-104f,7f,51.7f,180f),
                };
                case 1606: return new[]{
                    P(46f,7f,168.3f,0f),P(50f,7f,168.3f,0f),
                    P(-42.3f,0f,100f,270f),P(-42.3f,0f,96f,270f),
                    P(80.3f,0f,-2f,90f),P(80.3f,0f,2f,90f),
                    P(14f,0f,-78.3f,180f),P(18f,0f,-78.3f,180f),
                    P(-100f,7f,27.7f,180f),P(-96f,7f,27.7f,180f),
                };
                default: throw new ArgumentException("Unknown Meridian plan.");
            }
        }

        // The host holds the last accepted index for this party. Ticket reconstruction
        // consumes the chosen index directly, without advancing the party's selection.
        public static int Choose(uint runSeed, int previous)
        {
            if (previous < -1 || previous >= Plans.Count)
                throw new ArgumentOutOfRangeException(nameof(previous));
            var random = new SourceRandom(runSeed ^ 0x95d3b597u);
            int index = (int)(random.Next() * (Plans.Count - (previous < 0 ? 0 : 1)));
            return previous >= 0 && index >= previous ? index + 1 : index;
        }

        // Slot zero is the final court. Instance assignments use only the run seed,
        // so cache, survivor and spawn draws never affect geometry or hall identity.
        public static string[] DealHalls(uint runSeed, IReadOnlyList<string> instances)
        {
            if (instances == null || instances.Count != 3 || instances.Any(string.IsNullOrEmpty) ||
                instances.Distinct(StringComparer.Ordinal).Count() != 3)
                throw new ArgumentException("Expected three different instance ids.", nameof(instances));
            var slots = new[] { "Final court", instances[0], instances[1], instances[2] };
            var random = new SourceRandom(runSeed ^ 0x362f90cbu);
            for (int i = slots.Length - 1; i > 1; i--)
            {
                int pick = 1 + (int)(random.Next() * i);
                (slots[i], slots[pick]) = (slots[pick], slots[i]);
            }
            return slots;
        }
    }
}
