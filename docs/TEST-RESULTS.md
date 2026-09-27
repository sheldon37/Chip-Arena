# Portable server, animation and knockout presentation — 2026-09-27

Executed with Node.js 24.19.0 on Linux, Rapier 0.19.3, Three.js 0.180.0; one room/two clients. Windows runtime in the release is official Node.js 24.21.0 x64, checksum verified.

- `npm test`: **122/122 passed**, including all prior tests and new Windows/POSIX path traversal/asset checks, five-second ordinary/final result states, simultaneous deaths, protected preview commands, all-specialization finite poses and bounded/deduplicated cosmetic voxel effects. The input-driven full bot match/rematch test is included (shortened presentation timings in that automated fixture).
- `npm run build`: passed TypeScript, Vite client build, source packaging and Worker relay build.
- `TEST_PORT=8795 npm run test:network:local`: two real WebSocket clients completed a full first-to-five-set match plus rematch in **119.497 seconds**. **2,336** shared snapshots agreed exactly; 10 round results, 5 set results, drafts only after sets 1–4, destruction agreed, disconnect timeout passed. Sampled simulation tick median **0.097 ms**, P95 **0.386 ms**, maximum **2.953 ms**. This is not a server-capacity claim.
- `TEST_PORT=8796 TEST_LATENCY_MS=80 npm run test:motion:network`: 80 ms delay plus 40 ms jitter each direction, 20 seconds moving; 46.06 m guest travel, 1,200 floor checks, 469 identical snapshots. Raw correction median 0.0000014 m, P95 **0.082 m**, maximum **1.421 m**; largest immediate visual correction **0.000981 m**. Moving-debris corrections and no historical hit rewind remain limitations.
- Portable bundle smoke test: started **the generated bundled server through its launcher** from an extracted folder containing spaces, using Linux Node in place of Windows node.exe. Client/assets/audio loaded; path traversal and unauthorized debug commands were rejected. Two independent clients produced the same authoritative death ID/winner, stayed in the five-second result, and received a clean next round. Stopping the launcher stopped its server child.
- Browser preview: observed articulated running/melee and Fire casting, and the live voxel self-death/5-second banner. Cloud Chrome has no WebGL; the compatibility renderer was used. Hardware rendering, Windows .bat execution and the optional internet tunnel were **not** end-to-end tested. Browser compatibility HUD observations were approximately 56–60 FPS in this simple sandbox, not a desktop GPU benchmark.

The downloadable release includes the complete editable source and runtime license. Public Site rooms still use the existing HTTPS fallback; no permanent public WebSocket deployment was provisioned. The optional online launcher is an on-demand laptop tunnel, not a deployed always-on server.

Earlier sections below are historical test reports; the newest results above supersede old implementation descriptions.

# Movement replay, smoothing and automatic WebSocket routing — 2026-09-27

Implemented shared dynamic-capsule locomotion for authority and prediction, matching friction/restitution, ordered input batches with per-tick consumption and acknowledgements, round-generation rejection, one-shot tap handling across gaps, render-only local correction smoothing and adaptive remote interpolation. Dedicated Node deployments now automatically expose their WebSocket endpoint through `/api/network`; Sites can select one through `MULTIPLAYER_URL`. The public Site has no connected persistent backend and therefore still uses the improved HTTPS fallback.

Actual verification (Node 24.19):

- `npm test`: **116 passed, 0 failed**, 3.26 seconds. Eight new tests cover ordered/duplicate/invalid/stale input batches, 50/200 ms input batches with 50 ms delay each way, jump/Block tap deduplication, correction smoothing/reset, interpolation and endpoint validation.
- The reproducible moving/jumping-client probe initially measured **1.631 m maximum correction** with 200 ms updates and 50 ms delay each way. After shared physics and input replay, the same scenario measured **0.00405 m maximum / 0.00404 m P95**. This is a controlled predictable-motion test; it does not establish internet combat performance.
- `TEST_LATENCY_MS=100 TEST_JITTER_MS=40 npm run test:motion:network`: **passed**, two real WebSocket clients, 20.02 seconds of moving/jumping guest input, **49.97 m travelled**, **1,201 floor checks**, **471 identical shared snapshots**. Raw correction median **0.0000025 m**, P95 **0.1026 m**, max **0.1696 m**. Largest instantaneous correction-induced visual jump **0.000981 m**. Snapshot gap median **48.21 ms**, P95 **102.64 ms**. Final interpolation buffer **134.07 ms**. The test adds 100 ms ±40 ms on each direction and preserves ordered WebSocket delivery; it is not a public-host measurement.
- An earlier motion run exposed a one-frame jump being reused during an empty input queue (1.534 m maximum correction). The gap path now clears jump/dash/Block/reload/secondary taps; the passing motion run above is the rerun.
- `TEST_LATENCY_MS=100 TEST_JITTER_MS=40 npm run test:https`: **passed**, moving guest against idle host, five sets/four drafts/rematch, **480 identical snapshots**, **2,937 guest floor checks**, shared destruction and guest leave, plus one intentionally lost response/retry. Wall time **113.72 seconds**. Snapshot interval median **223.22 ms**, P95 **392.10 ms**; input-to-ack median **1,052.46 ms** across four relay legs. Raw guest correction P95 **0.319 m**, max **1.658 m**, including combat/collision/reset transitions. This run preceded the final one-shot gap fix, which is covered by the subsequent unit and WebSocket reruns. HTTP latency is still substantial under this adverse test.

- `TEST_PORT=8793 TEST_LATENCY_MS=100 npm run test:network:local`: **passed**, full dedicated-server match/rematch and disconnect forfeit with new input batches, **2,054 identical shared snapshots**, destruction on both clients and four drafts after two round wins each. Match/rematch portion **105.91 seconds**; sampled simulation tick median **0.102 ms**, P95 **0.407 ms**, max **2.95 ms** while other tests ran. This full-match process began before the final one-shot gap fix; its follow-up motion/unit tests ran after that fix.
- Two actual supervised-browser clients created/joined the automatic same-origin WebSocket room by invite, synchronized ready/spec selections, and the guest input pilot moved, jumped and won three sets against the idle host. The combat screenshot (`docs/chip-arena-smooth-network.jpg`) showed 66 ms P95 updates, an 83 ms interpolation buffer and 0.00 m raw correction. This local preview uses the SVG compatibility renderer, not a GPU benchmark or public-internet measurement.

Remaining limits: prediction freezes prop collision geometry at the last authoritative snapshot, does not independently resolve debris damage, and does not rewind hits/Block. Moving debris, combat impulses, missing input beyond the bounded replay history and major network stalls can still cause correction. The visual smoothing is not additional authority or hit validation. Browser-hosted fallback authority remains dependent on the host's device/tab. A deployable Node backend is included, but public WebSocket deployment still requires a persistent server and configuration; no paid infrastructure was provisioned.

---

# Guest floor and relay-latency correction — 2026-09-27

The reported guest floor bug was reproduced before the fix: starting from an airborne authoritative Y=2, one second of prediction reached Y=-9.692 while the collision capsule still remained at Y=2. The old predictor updated the rigid body without propagating its transform to its collider between sweeps.

The fix propagates collider transforms every predicted/replayed movement step. Reconciliation restores movement speed and external knockback separately rather than resetting movement to zero. There is no artificial floor-height clamp; legitimate ring-outs still fall. Browser-hosted input expiry is now 0.75 seconds instead of the dedicated server's 0.3 seconds. HTTPS exchanges target 50 ms and use one authenticated D1 batch instead of a separate pre-read; the supplied seat role never replaces token validation. Legacy clients remain accepted.

Actual verification:

- `npm test`: **108 passed, 0 failed**. Five new predictor cases cover airborne landing with a long update gap, jumping and replay, cover collision, legitimate edge falls, and retained locomotion/knockback. New relay tests verify the one-batch path cannot impersonate the host, and the input hold expires safely.
- `TEST_LATENCY_MS=100 npm run test:https`: **passed**, five sets/four drafts/rematch, **455 identical wire snapshots** and **2,393 guest floor assertions**, including a lost response/retry. Destruction agreement and guest leave also passed. Wall time **99.79 seconds**.
- Two supervised browser clients also completed a full five-set match with the **connecting guest** running the ordinary input-only test pilot (movement, jumps and attacks) against the idle host. The guest stayed above the foundation and reached the 5–0 Match won screen at 100 HP; the captured HUD showed 177 ms RTT and 0.00 m correction. Evidence: `docs/chip-arena-guest-movement.jpg`. This preview uses the SVG compatibility renderer because WebGL is disabled; its FPS is not a GPU benchmark or public-internet latency measurement.
- In that controlled test, with 100 ms added to each HTTP request and response leg, guest snapshot intervals measured **215.34 ms median / 220.85 ms P95**, compared with 234.95 / 239.92 ms in the preceding run. Median input-to-acknowledgement was **872.04 ms**, versus 923.23 ms previously. This round trip crosses all four relay legs; it is not one-way latency. The SQLite test does not model D1 query latency, so the reduction from eliminating a database round trip is not included in these numbers.

HTTPS still has more latency than a dedicated WebSocket transport. This correction fixes floor penetration and reduces avoidable overhead; it does not claim lag-free internet combat or add lag-compensated hit/block rewind.

---

# HTTPS room relay — 2026-09-27

This update replaces the failed PeerJS-only path with a Chip Arena-owned HTTPS/D1 relay. Embertrail invasion source was inspected read-only as an architectural reference; its checkout remained clean and no source, deployment, database or assets were modified.

Actual runs on Node 24.19 in this workspace:

- `npm test`: **101 passed, 0 failed** (2.72 seconds in the last run). Includes new SQL-backed relay tests: session/seat isolation, atomic concurrent joins, race-free mailbox writes, stale retry ordering, forbidden guest messages, origin/body limits, host expiry, guest leave, and coalesced Block taps.
- `TEST_LATENCY_MS=100 npm run test:https`: **passed** through a real HTTP server with the deployed Worker handler and SQLite-backed mailboxes. Two independent HttpsRoom clients completed five sets/four drafts and rematched in **95.17 seconds**. **397 serialized authoritative snapshots** agreed exactly, including health, physics/destruction and results. One intentionally lost response was retried after the server committed it; welcome/commands were not duplicated. Guest leave reached the host.
- With 100 ms added on *each* request/response leg, guest snapshot intervals measured **234.95 ms median / 239.92 ms P95**, with **923.23 ms median input-to-acknowledgement**. That acknowledgement crosses guest→service→host→service→guest and includes mailbox cadence; it is not one-way movement latency. This deliberately adverse test exposes substantial guest latency. Local prediction is present; timed Block has no rewind. Maximum snapshot in this match: **20,604 bytes**.
- `npm run test:block:network`: passed again after the transport change: defender 100 HP, shooter 77 HP, 23 reflected damage, fixed 4-second cooldown, identical shared snapshot tick 204.
- Two actual supervised-browser clients successfully registered, joined via invite, displayed the same two-player roster, and synchronized ready and specialization states over the new HTTP service. The two browsers then completed all five sets with four installed guest Chips, showed matching 5–0 results, and successfully rematched with fresh specialization selection. The host input-only test pilot played against the idle human guest client; draft timeouts also ran. A real mass-based debris impact appeared in the authority debug HUD (2 kg, 8.05 m/s, 64.8 J, 0.5 HP). Closing the host ended the guest session with a working Return to Main Menu control. See `docs/chip-arena-https-room.jpg` and `docs/chip-arena-https-match.jpg`. The test browser disables WebGL and uses the existing 3D compatibility renderer; this is not a GPU frame-rate benchmark.

Initial test-harness assertions compared raw JS objects with JSON-decoded packets (undefined fields differ). Those checks were corrected to compare serialized wire state; the successful run above is the rerun. An earlier repeated-selection flood revealed redundant queued commands; transport deduplication now prevents that backlog. Short action taps remain latched during input coalescing.

The Site deployment succeeded and its isolated `DB` binding / `relay_rooms` table were confirmed through Sites. Node fetch requests to the public endpoint timed out, including a proxy-configured retry. A curl POST reached the deployed API and correctly returned 401 for a missing room token. The optional authenticated public test runner also timed out and was not counted as a pass. However, subsequent read-only production diagnostics showed an occupied two-seat room with advancing host/guest sequence numbers and successful HTTP 200 exchanges from live browser clients (sample Worker wall time 100–128 ms). That confirms live relay traffic, not a completed public match or a measured public input latency. Public deployment internet performance is not established by local/supervised tests. No claim is made that two browser clients completed a match through the public deployed backend. Browser-hosted authority remains a trusted-host prototype. HTTPS reliability comes with more latency than a dedicated WebSocket server. Host migration and refresh recovery are not implemented.

---

# Executed test results — 2026-09-26

## Timed Block and browser-room update — 2026-09-27

- **94 automated tests passed**, zero failed. The 18 new cases cover fixed Block cooldown for all weapons and attack rates, validated input/rising-edge behavior, explicit Chip stacking/caps, real reflection/damage/attribution, preserved elemental payload, no recursive direct-hit procs, melee damage/impulse/status negation, order-independent activation, cooldown/late presses, reflection lifetime/count/speed/damage bounds, cover, once-per-activation Counter Pulse, DoT/debris exceptions and resets. Browser-host authority tests cover code/profile normalization, two-player limits, debug/damage/Chip grant rejection, finite inputs, rate limits, selections and disconnects.
- A full input-driven host-wrapper match completed **five sets / four drafts in 89.667 simulated seconds**, followed by passing two-vote rematch/reset. This validates the host authority module, **not PeerJS transport**.
- A real two-WebSocket-client Block test passed: defender **100 HP**, reflected shooter **77 HP**, **23 damage** credited to the blocker, and **4.00 s** Block cooldown. Both clients received identical player/projectile state at shared tick 204.
- The full dedicated-server match/rematch test passed with **100 ms delay each way**, **2,014 identical shared snapshot ticks**, destruction on both clients, two wins before each draft and passing disconnect forfeit. Elapsed **103.802 s**; sampled tick median **0.101 ms**, P95 **0.415 ms**, maximum **11.734 ms**. Concurrent browser/build work means this is not an isolated capacity benchmark.
- Browser sandbox displayed base **0.30 s / 4.00 s**, then **0.46 s / 2.89 s** after two Phase Buffers and two Rebound Capacitors. Test Block showed the active HUD, open mint diamond, defensive first-person pose and sound playback. Screenshot: `chip-arena-timed-block.jpg`; the captured compatibility renderer showed 58 FPS / 0.10 ms sim, not a hardware GPU benchmark.
- Browser room QA: creator registered with the public broker, ran its worker and displayed HOST plus a code/invite; an invite opened a separate guest client with its code prefilled. Clipboard writing was denied in the HTTP preview, so a selectable invite field was added. Guest and host exchanged a WebRTC offer/answer through the public broker, but **no data channel opened**. The connection timed out and the UI returned an explicit failure. No successful browser-hosted match or public internet gameplay is claimed. The precise environment/network cause was not proven; direct-only ICE cannot guarantee connectivity.
- No TURN relay, paid service or dedicated public backend was provisioned. The first-person/WebGL hardware limitation remains unchanged.


## Sets, visible statuses and player board — 2026-09-27

- **76 automated tests passed**, 0 failed. Ten new cases cover two wins before a draft, 1–1 deciding rounds, draws preserving progress, both-player draft timeouts, final-set handling, rematch/lobby reset, exhausted Chip pools, bounded status geometry, first-person status cues, numerical stack summaries and selecting a duplicate Chip.
- Input-driven Pistol-versus-Normal-Sword bot match completed **5–1 sets** in **70.133 simulated seconds**; rematch passed.
- Two independent local WebSocket clients completed match/rematch in **96.445 seconds**, with **100 ms delay each direction**. Both agreed exactly at **1,867 shared snapshot ticks** and observed destruction. The runner saw nine nonfinal round results, four nonfinal set results and drafts after sets 1–4 only; every draft required two round wins. Final set and rematch state assertions passed. Disconnect forfeit passed. Sampled server tick median **0.086 ms**, P95 **0.440 ms**, maximum **11.438 ms**; browser QA/build work overlapped, so this is not an isolated capacity benchmark.
- Browser preview: both players’ board entries, specializations and counts rendered. Two Ember plus two Venom Chips displayed four copies/two types. The Ember tooltip showed +6 burn DPS and 2/6 copies; its description retained the duration/refresh rules. The HUD button, × close and quick Tab press/release worked. A sustained physical Tab hold and pointer-lock recapture were not manually certified in this automation environment.
- An ordinary input-driven Pistol hit visibly applied **Burning 6 DPS** and **Poisoned 5 DPS**, with orange/yellow flames on the target and green bubbles above it. Screenshot captured both effects together. Tests also verify expiry, death/reset cleanup, and that merely owning enchantments does not activate victim effects.
- Browser QA used the SVG 3D compatibility renderer at **1363 × 936**, because this browser disables WebGL. The captured enchanted hit displayed **52 FPS / 0.30 ms simulation tick**; these are short HUD observations, not a GPU benchmark. Hardware WebGL remains unverified. Evidence: `chip-arena-stacked-board.jpg` and `chip-arena-status-effects.jpg`.
- Server TypeScript build passed. Public hosting remains account-free local practice; the persistent public multiplayer backend is still undeployed. Network checks above used the included local Node backend.


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

## Articulated rig / sniper update

See [RIG-ARENA-SNIPER.md](RIG-ARENA-SNIPER.md) for the latest validation scope. Automated suite: 128 passed; combined-Chip stress: 15 cases, 12,392 steps. Browser visual checks used the compatibility renderer.

Expanded-arena network regression: two Rifle clients, 100 ms each direction, 2,822 identical snapshots, full five-set match, four drafts, rematch and disconnect handling passed.
