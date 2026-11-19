# Meridian size B owner walk

Five candidates share the 600 × 480 m blockout and four cardinal gates. North is the final court. The other three halls accept the run's three instances. Every hall has a signed terrace approach, a stair up the near wing, a rooftop crossing and a second stair down the opposite wing. All five use fixed orientation.

| Developer walk name | Source walk seed | Distinguishing route and landmark | N / E / S / W gate-to-hall metres |
| --- | ---: | --- | --- |
| Crown Steps | 647 | Four stair flights to the +14 m northern court. The southern route descends beneath the viaduct to −21 m. Car park southeast. | 272 / 272 / 264 / 277 |
| Switchback Market | 784 | All four streets double back laterally. The southern route rises to a +7 m market terrace before descending to −7 m. The viaduct crosses the eastern −21 m hall approach. Car park northwest. | 265 / 273 / 269 / 277 |
| Sunken Exchange | 1058 | A deep northern interchange under the viaduct, an almost straight east descent and a two-turn southern approach. The western terrace sits 28 m above the northern court. Car park northeast. | 266 / 271 / 259 / 282 |
| Viaduct Mile | 1332 | Long outward boulevards, the eastern first bend at 125 m from the city centre and an elevated +14 m east hall. The viaduct spans the western −21 m trench. Car park southwest. | 261 / 271 / 268 / 281 |
| Split-Level Yards | 1606 | North descends to −7 m, turns twice in the same direction and climbs to +7 m. South reverses direction into a −21 m yard. The high east hall sits 35 m above the southern yard. Car park southeast. | 273 / 280 / 265 / 266 |

## Verification before the art freeze

Measured on the managed .NET 8 Release harness. The attachment column measures 88 local node attachments. Flow measures four full reverse-field rebuilds and 88 path traces per sample. Both use the actual preview graph with four roof loops, 120 warm-up and 600 measured samples. Physics, perception and wall queries are outside these timings.

| Layout | Collision blocks | Nav nodes | Attachment ms/sample | Flow ms/sample |
| --- | ---: | ---: | ---: | ---: |
| Crown Steps | 896 | 962 | 0.417 | 0.234 |
| Switchback Market | 885 | 962 | 0.434 | 0.245 |
| Sunken Exchange | 896 | 958 | 0.423 | 0.206 |
| Viaduct Mile | 858 | 958 | 0.498 | 0.285 |
| Split-Level Yards | 906 | 960 | 0.390 | 0.196 |

The native comparison entry point is **Chip Arena → Benchmark Meridian candidates against A0** or batch method `ChipArena.UnityConversion.Editor.NativeBuildChecks.CheckMeridianCandidateBudgets`. It measures baseline and candidates in one editor invocation, drives four pilots and checks 88 living hub zombies during each sampled tick. It compares each counter to baseline ×1.25, checks peak UTF-8 frame size against 1 MiB and reports generation time. Slowest-supported-guest generation still requires a measurement on that machine.

The native simulation has no recorded result for this revision. The five plans remain owner-walk candidates. The shared branch's live Expedition ticket and party history have not switched to the new contract. Once the budget passes, the host needs to retain the last deployed layout for the party, bind the chosen index and contract version to deployment compatibility and reconnect, and connect A2 survivors and cache tiers and A5a district population. The developer walk exercises the selection rule and hall deal locally.
