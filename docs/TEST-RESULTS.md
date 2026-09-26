# Executed test results — 2026-09-26

## Summary

- **32 automated tests passed**, 0 failed, in the final unit/integration run.
- **15 combined-Chip stress cases passed** (2,361 simulation steps in the final run).
- Browser client production build and Node server TypeScript build passed.
- A complete input-driven Pistol-versus-Normal-Sword-bot match ended **5–4**, followed by a successful rematch/reset. Final run: 42.4 seconds of simulated time.
- Two independent Chrome tabs connected to the same local/supervised backend, chose Ranged and Melee, readied, fought through Chip drafting, and showed matching **0–5 / 5–0** match results. Both voted for rematch; the restarted game reached its first round result with reset scores and Chips.
- A separate pair of independent WebSocket clients completed a first-to-five match and rematch, with exact state agreement at **828 shared snapshot ticks**. Both observed destruction. The disconnect timeout passed.
- At **100 ms added in each direction** (~200 ms artificial RTT), another pair completed the match, rematch and disconnect test, with exact agreement at **941 shared ticks**.
- Two clients in an explicitly authorized test room received the same 8 kg physical impact: **39.405 m/s normal closing speed, 6,211.08 J, 65 damage**, target health 35. Non-owner debug access was rejected.
- In a browser sandbox, an 8 kg / 15 m/s launch slowed before contact and produced **8.99 m/s measured normal closing speed, 323.3 J, 9.7 damage**. Installing Vital Core twice visibly changed maximum health to 150 and showed `×2`.

## Automated assertions actually run

- Finite-mass energy scaling, relative approaching normal speed, rotational point velocity, safe thresholds and per-impact cap.
- Pair/contact deduplication, separation requirement, cooldown and shared-fracture damage cap.
- Fifteen unique Chips, compatibility filters, stacking operations and caps.
- Real collision damage for every one of the 11 primary specializations.
- Burn, electrical discharge, reach, impulse, demolition and debris-resistance effects.
- One hit per target per melee swing; bounded secondary-projectile metadata; piercing cannot bypass cover.
- A primary pistol shot launches a still-intact crate into the other player, with source attribution and physical damage.
- Destroyed wall collider removal; fragment mass conservation; both-support beam collapse.
- Fast debris collision; falling heavy debris; less damage on the configured glancing trajectory.
- Settled debris and ordinary movement against settled props do not cause repeated damage.
- Specialization timeout, draw replay, loser draft, final match with no extra draft, round reset and rematch.
- Rejection of NaN/invalid input, inherited-property class/spec IDs, normal-room debug mutation and non-owner private-test-room debug mutation.
- Leaving a disconnected match for the lobby releases the abandoned player slot.
- Body budget degradation retains a finite-mass physical body rather than deleting it.

## Bugs caught and fixed

1. Support fragments could prop up the bridge longer than expected. Fracture now splits their vertical extent and uses symmetric separation motion; the collapse test gives the physical assembly time to tip/fall.
2. A melee hit with Shrapnel could remove a later prop in that swing's candidate list, causing a stale Rapier body access and server exit. The loop now skips removed candidates; a permanent regression test and all-Chip stress cases pass.
3. Prototype-property names could bypass an `in`-based selection check. Allowed selections now require own properties.
4. At the fragment budget, the destroyed parent now becomes a single loose gameplay body instead of losing mass/collision because only some pieces fit.

## Measured performance and conditions

One active room, two scripted WebSocket clients, 60 Hz fixed-step server, 20 Hz full JSON snapshots. Linux container, Node.js 24.19.0, Rapier 0.19.3. These measurements sample the simulation's own per-tick timer; they are not a load-capacity certification.

| Run | Match + rematch elapsed | Median sampled tick | P95 | Maximum sampled tick |
|---|---:|---:|---:|---:|
| No artificial latency | 44.081 s | 0.096 ms | 0.459 ms | 10.000 ms |
| 100 ms delay each direction | 50.156 s | 0.096 ms | 0.405 ms | 8.207 ms |

The cloud Chrome environment reported WebGL disabled. The actual 3D scene/camera and game were inspected using the SVG compatibility renderer at a 1363 × 936 viewport. The sandbox HUD showed about **27–30 FPS**, 48–49 bodies, and about 0.1–0.3 ms simulation ticks. This is **not a measurement of the intended WebGL renderer** or the user's GPU. SVG rendering has visible painter-order artifacts. See `physics-sandbox-compatibility.jpg` for the observed QA view.

## Remaining limits and unverified areas

- **No deployed public multiplayer server was tested or provisioned.** All online validation above used local/supervised Node backends.
- Hardware WebGL visual quality and frame rate were not verified in this environment. The normal renderer compiled, but the browser could not create its GPU context.
- Simulated-latency testing proved server-state agreement and continued match flow. It did **not** quantitatively certify local prediction accuracy. The predictor uses a kinematic approximation of the server's dynamic capsule; debris contacts, ramps, dashes and knockback can visibly correct. There is no historical hit rewind.
- Secondary actions are implemented and exercised in automated bot/test matches and stress cases, but not every secondary has an isolated assertion.
- Bot navigation is heuristic. Environmental occlusion/physics behavior, jump timing and class balance still need human playtesting.
- VFX/audio are prototype assets. Water and darkness effects remain simpler than production-quality fluid or volumetric effects.
- Docker packaging was supplied but no Docker image was built/run here. The production Node build was typechecked.
- WebMCP was feature-detected and registered in source; this browser reported `modelContext` unavailable, so tool-context execution validation was unavailable.
- No touch combat, reconnection/resume, multi-server scaling, bandwidth optimization, ranked play or persistence is claimed.
