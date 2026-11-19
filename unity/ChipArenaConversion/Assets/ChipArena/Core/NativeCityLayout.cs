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
