# Meridian size B owner walk

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
