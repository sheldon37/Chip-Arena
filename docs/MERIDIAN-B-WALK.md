# Meridian size B owner walk

Settings → Developer → Walk Meridian greybox opens the five `meridian-b-candidate-3` layouts. Hold Tab for the street plan. Each keeps the camp at the origin, its own 600 × 480 m bounds and the final court at the north route’s destination. E, S and W take the run’s three instances. The authored JSON and generator are in `docs/expedition/meridian-plans/`. After editing a spec, run `python tools/bake_meridian_walks.py` and commit the resulting Core file.

| Walk | North / east / south / west, metres along grade | Blocks including treads | Survey nodes | Camp-connected Hulk cells | High / Low greybox block triangles |
| --- | --- | ---: | ---: | ---: | ---: |
| Crown Hill | 299.2 / 299.2 / 291.7 / 255.4 | 2,735 | 633 | 55,929 | 1,214,340 / 251,620 |
| The Navigation | 271.4 / 274.7 / 260.3 / 288.2 | 4,456 | 606 | 53,540 | 1,978,464 / 409,952 |
| The Arcades | 255.0 / 262.0 / 263.6 / 264.7 | 4,360 | 583 | 51,088 | 1,935,840 / 401,120 |
| The Stride | 262.0 / 267.7 / 275.3 / 291.7 | 3,215 | 609 | 54,387 | 1,427,460 / 295,780 |
| The Yards | 262.0 / 284.0 / 259.3 / 259.3 | 1,978 | 595 | 57,205 | 878,232 / 181,976 |

The managed Release harness (`--layered-walk`) samples built exit heights every 2 m over 60 m, compares all 190 pairs, checks destinations and traces each horde corridor at full Hulk width and height. A separate 2 m layered graph checks reachability from camp over the built boxes and wedges. All 192 specified pilot fire escapes have built flights and pass the pilot-width and headroom sweep against the surrounding solids. The Navigation uses the upper terrace for a return landing. The Galleria stair has a narrow opening through its roof lid. Pilot fire escapes stay out of the horde graph. Direction changes round three arcade posts and two grade transitions use offsets inside the authored streets.

The triangle columns count drawing every greybox block once using the actual High and Low styled meshes. Rendered frames also include the pilot, shadows, effects and presentation. The owner’s complete-view caps are 2,177,224 High and 821,492 Low. The Developer walk shows mean frame time and peak rendered triangles when the Unity render counter is available. Compare the same views on both tiers before the art freeze.

On the Windows builder, deployment acceptance runs the four-pilot, 88-zombie native simulation on a changed geometry revision and logs the layout name and counters. The Editor menu **Chip Arena → Benchmark Meridian candidates against A0** reruns the comparison and enforces the freeze thresholds. These are Developer walk candidates. The live expedition ticket continues to use its existing world contract.

For changed geometry, the local Windows build also opens its packaged Development player in the isolated load harness, at 1600 × 900. It measures six views per layout on each graphics tier with four pilots and 88 zombies. Each `MERIDIAN_VIEW` line carries peak triangles and mean and 95th-percentile frame time after a 24-frame camera warm-up. The editor build log copies the samples, and the per-layout player logs remain in `Builds/meridian-candidate-*.log`. A missing graphics device or missing render counter leaves that measurement open.
