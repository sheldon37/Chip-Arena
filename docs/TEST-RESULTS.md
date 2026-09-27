# Executed test results — 2026-09-26

## First-person, audio, fire and poison update verification

- **66 automated tests passed**, 0 failed. Sixteen new cases cover validated camera-mode input, reconstructed camera/muzzle positions, actual shots and blocking cover in both views, fire/poison compatibility and stacking, exact duration and tick cadence, real attacks from all three guns and all four melee specializations, nonrecursive secondary effects, kill credit, reset/restore, audio deduplication, and PCM asset integrity.
- Found and fixed the previous burn-refresh behavior: rapid hits reset the next tick and could postpone all burn damage. Both statuses now refresh duration while preserving cadence and process the final due tick before expiry.
- Input-driven Pistol-versus-Normal-Sword bot match completed **5–3** in **31.5 simulated seconds**; rematch/reset passed. Fifteen all-Chip stress cases completed **385 steps** with all 26 Chips.
- Two independent local WebSocket clients, one sending first-person aim intentions and one third-person, completed match/rematch in **47.609 s** under **100 ms delay in each direction**. They agreed exactly at **890 shared snapshot ticks**, both observed destruction, and the disconnect forfeit passed. Sampled server tick median **0.088 ms**, P95 **0.563 ms**, maximum **12.902 ms**. Browser/build/stress work overlapped this run; it is not an isolated capacity benchmark.
- A dedicated two-client elemental test passed. One first-person Pistol hit applied **3 burn DPS / 2 s** plus **2.5 poison DPS / 4 s**. Both received identical authoritative statuses and health: **77 immediately after the direct hit**, then **72.875** after the sampled DoT ticks. Reset cleared both statuses and restored health. A non-owner Chip grant was rejected.
- Browser QA: saved first-person preference survived a preview reload; V switched both ways; the gun/hands and enchanted Sword rendered; both Chip installation indicators appeared; M mute/unmute worked; the sound test reported **30/30 loaded effects**. A real input-driven Pistol hit visibly applied Burning and Poisoned with separate countdown/DPS badges; combat increased the audio playback counter. Sound decoding and playback scheduling were verified; subjective listening quality was not certified.
- The 30 original mono PCM WAVs total **437,926 bytes**. Automated checks confirm distinct sample content, non-silent RMS, 22,050 Hz / 16-bit headers, and peaks below 0.73 full scale.
- Browser and server TypeScript/production builds passed. Browser QA still used the SVG 3D compatibility renderer because WebGL is disabled. The first-person idle sandbox displayed roughly **50–60 FPS**, while a captured attack showed **42 FPS** at 1363 × 936; these are short HUD observations, not a GPU performance benchmark. Hardware WebGL/trail appearance still needs human playtesting. Screenshot: `first-person-enchantments-compatibility.jpg`.
- Public hosting remains account-free local practice. The persistent public multiplayer backend remains undeployed; all network results above used local Node servers.

## Projectile-drop update verification

- **50 automated tests passed**, 0 failed, including seven new ballistic checks. Every gun and spell hit the floor before expiry; one Velocity Coil extended the controlled flat-shot landing distance by approximately 25%. At the same 10 m horizontal distance, the upgraded Pistol had exactly 36% less gravitational drop within numerical tolerance.
- Landing tests use a projectile center 1.6 m above an 80 × 80 m flat floor, no homing or bounces, and 60 Hz stepping. Measured distances for all seven projectile specializations are in README. Real arena pitch, elevation and cover change those distances.
- Fast downward shots still hit players through swept collision. Ricochets retain gravity, split projectiles inherit gravity, and homing without an acquired target still falls.
- A pre-existing Splitter collision-mask issue was exposed by the new falling trajectories: children ignored the intact surface they had just struck. They now ignore only an already-hit player, so intact cover blocks them. Both existing and new regression checks pass.
- Input-driven Pistol-versus-Normal-Sword match finished **4–5** in **38.5 simulated seconds**, followed by a passing rematch/reset. Bots compensate for drop with imperfect aim inputs.
- **15 all-Chip stress cases completed**, 411 simulation steps. These heavily stacked builds can finish rapidly; this is a crash check, not a balance certification.
- Two independent local WebSocket clients completed a match and rematch in **57.389 s** with **100 ms added each direction** (~200 ms artificial RTT). Both observed destruction and agreed exactly at **1,085 shared snapshot ticks**. Disconnect forfeit passed. Sampled tick median **0.120 ms**, P95 **0.392 ms**, maximum **1.803 ms**. The browser and Node production builds passed.
- Browser visual QA was not repeated for this update. The renderer consumes the authoritative projectile positions/velocities already exercised by the existing renderer; GPU performance remains unverified.

## Modifier update verification (before projectile drop)

Executed after the ammo/mana/stamina and weapon-compatible modifier changes:

- **43 automated tests passed**, 0 failed. The 11 new assertions cover eligibility for every specialization, expanded gun magazines/reload, six-round Machine Gun secondary, actual multishot damage/cost/budget, one hit per Echo Strike sweep, resource blocking/regeneration, Shield stamina costs, rate stacking, projectile speed/range, homing/cover/secondary metadata, and resource/combo resets.
- Input-driven Pistol-versus-Normal-Sword match finished **5–2** in **29.25 simulated seconds**; rematch/reset passed.
- **15 all-Chip stress cases completed**, 424 simulation steps. This run includes all 25 Chips, so stacked attacks end many cases quickly. This is a crash/regression check, not a balance certification.
- Two independent local WebSocket clients completed a first-to-five match and rematch in **41.727 s**, with exact state agreement at **781 shared ticks** and destruction observed by both. Disconnect timeout passed. Sampled tick median **0.084 ms**, P95 **0.545 ms**, maximum **2.733 ms**; stress tests ran concurrently, so these timings are not directly comparable to the earlier isolated run.
- Browser UI check: Machine Gun displayed **48/48 Ammo**, then **72/72** after two Magazine Banks. Switching to Sword refreshed the picker to melee-compatible Chips, excluding projectile/ammo Chips; two Stamina Cells displayed **150/150 Stamina** and the installed `×2` marker. These checks used the existing SVG 3D compatibility renderer because WebGL is disabled in the test browser. Screenshot: `stamina-update-compatibility.jpg`.
- The browser and Node production builds passed. The hosting service confirmed public access (policy revision 2). A live anonymous browser session is unavailable from the preview environment, so access verification uses the hosting service's confirmed public policy and successful deployment status.

## Initial prototype verification (before the modifier update)

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
