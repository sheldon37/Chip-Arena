using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public static class NativeCityLayout
    {
        public const string Version = "meridian-b-candidate-2";
        // Candidate data for the owner walk. Slot order is always north, east, south, west.
        public sealed class Plan
        {
            public readonly string Name;
            public readonly uint Seed;
            internal readonly int ParkX, ParkZ, ViaductSide;
            internal readonly Lane[] Lanes;
            internal Plan(string name, uint seed, int parkX, int parkZ, int viaductSide, params Lane[] lanes)
            {
                Name = name; Seed = seed; ParkX = parkX; ParkZ = parkZ;
                ViaductSide = viaductSide; Lanes = lanes;
            }
        }

        internal readonly struct Lane
        {
            // First bend's radial station and signed offset, second bend's signed displacement,
            // outer court's radial station, and heights at the first terrace and court, in metres.
            public readonly int Bend, Turn, Jog, End, Terrace, Court;
            public readonly int Angle, Width, Approach, Rise, Run;
            public readonly bool Covered;
            public Lane(int bend, int turn, int jog, int end, int terrace, int court, int angle, int width, int approach, int rise, int run, bool covered = false)
            {
                Angle = angle; Width = width; Approach = approach; Rise = rise; Run = run; Covered = covered;
                Bend = bend; Turn = turn; Jog = jog; End = end; Terrace = terrace; Court = court;
            }
        }

        public static readonly IReadOnlyList<Plan> Plans = Array.AsReadOnly(new[] {
            new Plan("Crown Steps", 647, 62,-95, 2, new Lane(65,54,12,228,14,14, 35,18,16,14,22), new Lane(90,24,0,272,-7,-14, -32,6,9,-7,24,true),
                new Lane(85,-56,8,222,-7,-21, -40,10,14,-10,21), new Lane(100,16,-18,267,-7,-14, 25,8,7,-7,23)),
            new Plan("Switchback Market", 784, -99,62, 1, new Lane(78,40,-34,215,7,0, 42,7,8,10,21), new Lane(65,-36,32,228,-7,-21, -41,12,18,-9,21,true),
                new Lane(95,45,-35,209,7,-7, 30,16,8,7,24), new Lane(78,-40,25,236,-7,0, -34,6,12,-7,23,true)),
            new Plan("Sunken Exchange", 1058, 80,78, 0, new Lane(92,-52,22,213,-7,-21, -38,14,18,-12,22), new Lane(82,15,0,280,-7,-14, 28,8,6,-7,23),
                new Lane(68,28,34,221,-7,-14, 36,18,12,-10,23), new Lane(98,-23,-15,268,7,0, -30,7,10,7,22,true)),
            new Plan("Viaduct Mile", 1332, -108,-55, 3, new Lane(96,62,0,223,7,0, 44,9,7,9,23), new Lane(125,12,0,279,7,14, -24,18,18,12,24),
                new Lane(74,-52,-14,226,-7,0, -33,6,7,-7,23,true), new Lane(68,24,0,280,-7,-21, 32,12,14,-9,24)),
            new Plan("Split-Level Yards", 1606, 96,-88, 2, new Lane(80,-22,-46,228,-7,7, -43,8,12,-8,21), new Lane(94,40,-13,251,7,14, 34,14,8,10,22),
                new Lane(65,42,-35,210,-7,-21, 39,7,6,-12,22,true), new Lane(110,-16,0,274,7,0, -27,16,14,7,24))
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
