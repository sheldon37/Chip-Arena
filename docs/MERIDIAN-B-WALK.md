# Meridian size B owner walk

Windows native-test revision `639307261968786427` published 19 November 2026 at 18:06 Eastern. The build used `da330b2a789a`, the same source tree as shared commit `85c372b1a00b`.

Settings → Developer → Walk Meridian greybox opens the five `meridian-b-candidate-3` layouts. Hold Tab for the street plan. Each keeps the camp at the origin, its own 600 × 480 m bounds and the final court at the north route’s destination. E, S and W take the run’s three instances. The authored JSON and generator are in `docs/expedition/meridian-plans/`. After editing a spec, run `python tools/bake_meridian_walks.py` and commit the resulting Core file.

| Walk | North / east / south / west, metres along grade | Blocks including treads | Survey nodes | Camp-connected Hulk cells | High / Low greybox block triangles |
| --- | --- | ---: | ---: | ---: | ---: |
| Crown Hill | 299.2 / 299.2 / 291.7 / 255.4 | 2,735 | 633 | 55,929 | 32,820 / 32,820 |
| The Navigation | 271.4 / 274.7 / 260.3 / 288.2 | 4,456 | 606 | 53,540 | 53,472 / 53,472 |
| The Arcades | 255.0 / 262.0 / 263.6 / 264.7 | 4,360 | 583 | 51,088 | 52,320 / 52,320 |
| The Stride | 262.0 / 267.7 / 275.3 / 291.7 | 3,215 | 609 | 54,387 | 38,580 / 38,580 |
| The Yards | 262.0 / 284.0 / 259.3 / 259.3 | 1,978 | 595 | 57,205 | 23,736 / 23,736 |

The managed Release harness (`--layered-walk`) samples built exit heights every 2 m over 60 m, compares all 190 pairs, checks destinations and traces each horde corridor at full Hulk width and height. A separate 2 m layered graph checks reachability from camp over the built boxes and wedges. All 192 specified pilot fire escapes have built flights and pass the pilot-width and headroom sweep against the surrounding solids. The Navigation uses the upper terrace for a return landing. The Galleria stair has a narrow opening through its roof lid. Pilot fire escapes stay out of the horde graph. Direction changes round three arcade posts and two grade transitions use offsets inside the authored streets.

The triangle columns count drawing every greybox block once using the flat 12-triangle survey mesh on both tiers. Rendered frames also include the pilot, shadows, effects and presentation. The owner’s complete-view caps are 2,177,224 High and 821,492 Low. The Developer walk shows mean frame time and peak rendered triangles when the Unity render counter is available. Compare the same views on both tiers before the art freeze.

On the Windows builder, deployment acceptance runs the four-pilot, 88-zombie native simulation on a changed geometry revision and logs the layout name and counters. The Editor menu **Chip Arena → Benchmark Meridian candidates against A0** reruns the comparison and enforces the freeze thresholds. These are Developer walk candidates. The live expedition ticket continues to use its existing world contract.

For changed geometry, the local Windows build also opens its packaged Development player in the isolated load harness, at 1600 × 900. It measures six views per layout on each graphics tier with four pilots and 88 zombies. Each `MERIDIAN_VIEW` line carries peak triangles and mean and 95th-percentile frame time after a 24-frame camera warm-up. The editor build log copies the samples, and the per-layout player logs remain in `Builds/meridian-candidate-*.log`. A missing graphics device or missing render counter leaves that measurement open.

## Windows simulation, 19 November 2026

Unity 6000.3.24f1 on an i9-13900K, source `24c894d12079`. Each row retained four pilots and 88 zombies for all 600 measured ticks after 120 warm-up ticks. All four per-tick costs cleared the A0 × 1.25 threshold. Generation measures the construction of the candidate inside this fixture.

| Layout | Navigation ms/tick | Crowd ms/tick | Perception ms/tick | Physics ms/tick | Generation ms | Peak frame bytes on wire |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A0 | 0.4371 | 0.1803 | 0.0177 | 0.1980 | — | 258894 |
| Crown Hill | 0.1505 | 0.0087 | 0.0129 | 0.1899 | 21.95 | 260636 |
| The Navigation | 0.1360 | 0.0119 | 0.0151 | 0.2021 | 21.50 | 272190 |
| The Arcades | 0.1397 | 0.0071 | 0.0141 | 0.1869 | 28.05 | 266635 |
| The Stride | 0.1319 | 0.0120 | 0.0155 | 0.1873 | 17.55 | 270385 |
| The Yards | 0.1341 | 0.0088 | 0.0129 | 0.1609 | 22.92 | 263491 |

## Rendered Windows player, 19 November 2026

The packaged Unity 6000.3.24f1 Development player ran at 1600 × 900 on an RTX 4090 with an i9-13900K. The six cameras sampled the pilot, north court, east door, south door, west door and elevated overview. Each view had 24 warm-up frames and 36 measured frame intervals. All 60 views cleared the complete-frame triangle limits of 2,177,224 High and 821,492 Low. Frame time is elapsed time between frames. This run covers this machine and these camera positions.

| Layout | Camera | High peak triangles | High mean / p95 ms | Low peak triangles | Low mean / p95 ms |
| --- | --- | ---: | ---: | ---: | ---: |
| Crown Hill | Pilot | 886,102 | 10.71 / 15.60 | 196,490 | 9.04 / 10.13 |
| Crown Hill | North court | 198,912 | 9.68 / 12.99 | 34,506 | 9.69 / 10.90 |
| Crown Hill | East door | 193,312 | 8.80 / 9.70 | 31,686 | 9.96 / 10.91 |
| Crown Hill | South door | 126,894 | 9.49 / 10.71 | 22,506 | 9.10 / 10.10 |
| Crown Hill | West door | 126,878 | 9.29 / 12.37 | 22,506 | 9.22 / 10.50 |
| Crown Hill | Overview | 725,222 | 9.55 / 10.35 | 253,310 | 9.31 / 10.39 |
| The Navigation | Pilot | 1,009,992 | 11.51 / 15.31 | 257,016 | 12.20 / 14.16 |
| The Navigation | North court | 286,828 | 10.39 / 11.27 | 49,158 | 10.09 / 11.12 |
| The Navigation | East door | 374,394 | 10.33 / 11.35 | 31,686 | 10.64 / 11.81 |
| The Navigation | South door | 169,000 | 10.45 / 11.36 | 37,158 | 10.19 / 11.05 |
| The Navigation | West door | 262,858 | 10.49 / 14.11 | 49,158 | 10.74 / 14.12 |
| The Navigation | Overview | 972,132 | 10.86 / 12.11 | 348,198 | 10.68 / 11.46 |
| The Arcades | Pilot | 931,070 | 12.37 / 20.65 | 203,990 | 10.17 / 11.01 |
| The Arcades | North court | 217,928 | 10.05 / 10.73 | 37,686 | 10.31 / 11.33 |
| The Arcades | East door | 243,952 | 10.21 / 14.10 | 42,006 | 9.89 / 10.69 |
| The Arcades | South door | 315,896 | 10.29 / 11.14 | 54,006 | 10.09 / 11.05 |
| The Arcades | West door | 182,046 | 10.28 / 13.13 | 31,686 | 9.82 / 10.68 |
| The Arcades | Overview | 812,066 | 10.56 / 11.31 | 273,818 | 10.61 / 12.48 |
| The Stride | Pilot | 920,632 | 10.97 / 11.49 | 202,250 | 9.32 / 10.26 |
| The Stride | North court | 233,486 | 10.06 / 11.60 | 40,266 | 10.02 / 11.22 |
| The Stride | East door | 233,506 | 9.63 / 10.40 | 40,266 | 9.77 / 11.69 |
| The Stride | South door | 472,154 | 10.79 / 12.18 | 40,266 | 9.96 / 11.16 |
| The Stride | West door | 197,464 | 9.57 / 10.51 | 34,266 | 9.64 / 10.62 |
| The Stride | Overview | 834,118 | 10.24 / 11.18 | 318,678 | 10.22 / 11.38 |
| The Yards | Pilot | 831,600 | 10.69 / 23.08 | 187,406 | 9.17 / 10.41 |
| The Yards | North court | 144,430 | 9.07 / 10.69 | 25,422 | 8.76 / 9.60 |
| The Yards | East door | 544,362 | 8.57 / 10.02 | 13,686 | 9.45 / 10.53 |
| The Yards | South door | 74,026 | 8.72 / 9.85 | 13,686 | 8.77 / 9.56 |
| The Yards | West door | 144,472 | 9.14 / 10.10 | 25,422 | 8.93 / 9.97 |
| The Yards | Overview | 796,546 | 9.42 / 10.35 | 272,090 | 9.04 / 9.94 |

## Landmarks and doors

With the local bank `ChipArenaCityLandmarks` (built by `tools/stages/meridian_landmarks.py`; the Drive zip carries it), the walk also draws Meridian's landmarks (Core `NativeMeridianLandmarks`, Runtime `NativeMeridianLandmarksDraw`): one exit landmark per gate per layout, City Hall's court with its 62 m tower at 6:47, the dealt instances' door fronts on their halls, and the far layer (each dealt instance's landmark beyond the Cordon on the camp's line through its door, and the WMRD 64.7 mast). Every open rebuilds them for the deal. The blocks they replace are left out of the greybox while they draw; without the bank the greybox is unchanged. `--landmark-reads` checks the reads from the camp at High and Low, and the bank script checks the art rules (0.25 m, 3.1 m) on every layout and hall. Their triangles stay under about 77,000 from any camp eye counted without culling, the court's tower and halls on the far material past the fog's 65%.
