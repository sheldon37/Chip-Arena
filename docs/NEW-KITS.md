# New kits: plunging stab, Bow, Cyber Lord (owner 2026-10-12)

Owner request: "Can we add a downward jump attack as well? If the player is facing downwards? ... I want to
add two more classes, one for ranged and one for dev. For ranged i want to add a "Bow" class. For Dev, a class
called "Cyber Lord". This is an unarmed class, using cyber themed abilities. (1-4 hotkeys used along with Q
and E) it will be a melee spellcasting hybrid."

Owner choices (2026-10-12): Astra builds the rules (simulation, networking, prediction, bots, accounts if
needed); Claude builds the visuals (models, animation, VFX, audio routing, HUD, icons, Locker). Bow fires "tap
or hold-to-draw". Cyber Lord is a "hardlight brawler". The downward attack is a "plunging stab".

Everything below is a proposal with starting numbers. Astra owns the rule implementation and may tune numbers
for balance or engine fit; please record any change here so the presentation follows it. Field and event names
are suggestions; if a name differs, note the real one in this file and in DEV_LOG.

## 1. Plunging stab (all four standard melee weapons)

The existing jump attack (forward arc + ring on landing, NativeAirAttackSimulation) stays as the default air
attack. The plunging stab replaces it only when the pilot is looking down.

- **Trigger:** same gate as the jump attack (LMB, airborne for at least .15 s, once per airtime, also after the
  dagger air jump, not while rolling, stunned, juggled, knocked down or in a Q/E special), plus camera pitch at
  or below -45 degrees at the press. Above -45 degrees the forward jump attack plays as today.
- **Motion:** .08 s windup (no hang), then a straight drop at -24 m/s. Horizontal speed is multiplied by .15 at
  the start of the plunge.
- **Hit:**
  - Direct: the pilot the plunge lands on, or any pilot whose capsule the blade passes through during the drop,
    takes the direct hit and a .4 s stun.
  - Landing ring: radius daggers 0.9, sword 1.0, shield 1.2 (shield-rim slam), greatsword 1.4 m. Ring victims
    get a light stagger.
  - One hit per victim per plunge (shared ledger with the ring).
- **Damage:** weapon damage x1.5 (greatsword x1.7) for the ring; the direct victim gets a further x1.25. Height
  bonus: +10% per metre fallen beyond 2 m, capped at +60% (8 m).
- **Cost and recovery:** stamina x1.4. The weapon sticks in the ground: recovery daggers .3, sword .45,
  shield .5, greatsword .7 s. No combo continues from it (daggers included).
- **Replication:** reuse the jump-attack fields (`Swing.Air`, `PlungeAt`, `ImpactAt`) with an air-attack kind
  that distinguishes `down` from the forward attack (for example `AirKind = "down"`), the fall height at impact,
  and the impact event (`Land`, Detail `melee-down`, radius, weapon). Guest prediction as for the jump attack.
- **Bots:** may use it when a target is within 3 m horizontally and at least 2 m below.

Presentation (Claude): blade-down stab pose per weapon (two-handed reverse grip for the greatsword, shield rim
first for the shield), a speed streak on the drop, a crater ring and debris sized by fall height, a stuck-blade
recovery and pull-out, first-person view looking down the blade, and a heavier camera shake for big falls
(camera-shake setting only).

### Plunging stab implementation contract (Astra, 2026-10-12)

`NativeMeleeSwing.AirKind` and snapshot `Actor.AirKind` are `"down"` for the stab and empty for the existing forward attack. `Actor.AirStartY` / `FallHeight` carry centre-height fall distance; `PlungeAt` / `ImpactAt` remain the shared clocks. `Land` uses Detail `melee-down` and the specified radius. Windup retains ordinary gravity (no hover); down carry is 15% of commitment velocity. No echoes or dagger combo follow a down stab.

Direct contacts sweep a narrow .08 m blade from the previous feet to .35 m below the current feet against the existing projectile body geometry, with world visibility and a shared direct/ring victim ledger. The existing target-body overlap allowance is used at ring edges; the ring radius itself remains .9/1/1.2/1.4 m. Height scales each direct hit at its contact height, and the ring at landing, capped at +60%. Direct stun uses the existing boss/anti-stunlock gates. Runtime/PhysX acceptance remains pending.

## 2. Bow (ranged weapon, id `bow`)

Ranged class, standard (not DEV). Joins pistol, rifle and machine gun.

- **Primary, tap or hold-to-draw:**
  - Pressing LMB starts the draw; releasing fires. A release before .25 s of draw is a quick shot.
  - Full draw takes .8 s (scaled by `attackRate`).
  - Damage lerps 18 (quick) to 62 (full). Arrow speed 45 to 110 m/s. Gravity 30 to 14. Spread .012 to .0015.
    Impulse 120 to 300.
  - Full-draw headshots get x1.25 on top of the normal headshot rule.
  - Holding at full draw is steady for 2.5 s, then spread rises linearly to .01 by 4 s (arm fatigue).
  - RMB while drawing cancels the draw with no shot.
  - Move speed while drawing x0.7. Sprinting cancels the draw.
  - Nock time .35 s after each release before the next draw can start.
  - Arrows are unlimited (no magazine, no reload). If the ammo model needs a value, use a 1-arrow magazine with
    the .35 s nock as its reload.
  - Projectile chips (bounces, split, pierces, homing, extraProjectiles, fanDegrees and so on) apply as for the
    other ranged weapons, scaled by draw where they scale with damage.
- **Q, Volley** (kind `burst`): looses 5 arrows at once in a 14 degree horizontal fan at 70% draw strength
  (22 damage each, 90 m/s). Cooldown 8 s. No mana or stamina cost (like the other ranged Q kits).
- **E, Rain of Arrows** (kind `strike`, range 40 m): marks a 3.5 m circle at the aim point; after .6 s,
  12 arrows fall over 1.2 s, 9 damage each and a 20% slow for 1.5 s on anyone hit. Cooldown 12 s.
- **Replication:** draw start time and draw fraction at release (for the pose and HUD meter), the per-arrow
  draw fraction on shot events (Detail `bow`, `bow-full`, `volley`, `rain`), the Volley and Rain cast times and
  the Rain target point and radius.
- **Bots:** draw to at least 70% at range over 15 m; quick shots under 6 m; Volley at 6-15 m; Rain on groups
  or a stationary target.
- **Weapon stats for GameContent:** class `ranged`, `secondary` `rain-of-arrows`, colour proposal
  0x7fd36b (a green distinct from the guns). The quick-shot values stand in for the table's
  damage/speed/gravity/spread.

Presentation (Claude):
- A procedural recurve bow in the new Standard weapon style, with a Prototype Series version.
- A quiver on the back, a nocked arrow and string that pull back with the draw fraction, and draw, hold, release
  and nock poses in third and first person.
- An arrow model and trail, a full-draw glint, arrows that stick in walls (6 s) and in pilots (until death or
  round end), and Volley and Rain effects.
- A HUD draw meter, cooldown cards, a class/weapon icon, a Locker tile, and an entrance (intro) animation.

## 3. Cyber Lord (DEV-only, id `cyberlord`)

An unarmed hardlight brawler and spellcaster. DEV class like Siren, Emperor, Ansatsuken and L33T H4x: live DEV
access required, no grant can unlock it. Keys: LMB, RMB, Q, E and 1-4 (the same input path the other DEV
fighters use).

- **Resource:** mana (100). Regenerates at the normal mana rate, and every LMB hit on a pilot restores 6 mana
  (the melee/spell loop).
- **LMB, hardlight combo** (melee, range 2.2 m, arc 1.4):
  - Jab: 12 damage, .28 s.
  - Cross: 14 damage, .30 s.
  - Pulse Palm: 22 damage, .45 s. Adds a 1.6 m forward cone pulse that hits for the same 22 (one hit per victim)
    and light-launches.
  - The combo resets after .6 s idle. Standard melee hit stun applies.
- **RMB:** standard melee block (hardlight guard).
- **Q, Data Dash:** 7 m dash through enemies, 18 damage to each pilot passed. It leaves a 1.2 s trail that
  ticks 6 damage per .3 s. Cooldown 6 s, 20 mana.
- **E, Firewall:** a hardlight wall 5 m wide and 2.5 m tall, placed .8 m in front, lasting 4 s. It absorbs
  projectiles, and enemies touching it take 10 damage per second. Cooldown 12 s, 35 mana.
- **1, Packet Burst:** a 6 m, 50 degree cone of 8 glitch shards, 5 damage each (40 point blank). Cooldown 4 s,
  15 mana.
- **2, Siphon Link:** a tether to the aimed pilot within 12 m for 2.5 s. Drains 8 damage per second and heals
  the caster for 50% of it. It breaks on lost line of sight or beyond 15 m. Cooldown 10 s, 25 mana.
- **3, System Crash:** a .5 s windup ground slam that makes a 4 m field: 25 damage and a 1.0 s "glitched"
  stun. Cooldown 14 s, 40 mana.
- **4, Kernel Panic (ultimate, a cinematic connect-combo).** The owner revised the ultimate on 2026-10-12:
  "since we're adding an "Ultimate" im think using a "skill cut-in" animation. If the first hit connects, it
  does a combo. Hits them far, teleport behind hit them again, in the air teleport above, make this creative".
  It replaces the earlier Kernel Strike AoE.
  - **Opener:**
    - A .25 s windup.
    - Then a 6 m hardlight lunge-punch along the aim (capsule sweep, radius .6 m, one target: the first pilot
      touched).
    - **Whiff:** .6 s recovery, and half the cooldown is refunded.
    - **Connect:** the sequence below starts at once.
    - Cooldown 30 s, 60 mana, both spent on cast.
  - **Cut-in:**
    - On connect, a .6 s "skill cut-in" plays: a full-screen splash for the caster and the victim, and a slim
      top banner for everyone else.
    - Caster and victim are frozen in place for those .6 s. Everyone else keeps playing.
  - **Sequence:** about 3.4 s after the cut-in, host-authoritative and deterministic from its start time,
    anchor, facing and the seeded arena-clamped waypoints, so every client plays the same choreography.

    | Step | Starts at | What happens | Damage |
    |---|---|---|---|
    | 1. Breaker | 0 s | Punch launches the victim 8 m back along the facing, rising 1.5 m | 12 |
    | 2. Blink-behind | .45 s | Caster glitch-teleports behind the flying victim and kicks them straight up 5 m | 10 |
    | 3. Blink-above | .95 s | Caster teleports above the victim's apex and hammer-fists them downward | 10 |
    | 4. Fork bomb | 1.35 s | As the victim falls, three hardlight clones of the caster strike from three sides | 3 × 6 |
    | 5. Kernel Panic | 1.9 s | The caster reappears at the landing point; the victim is slammed into a column of falling code | 25 |
    | Recovery | from 2.6 s | Caster recovers for .8 s | |

    - Step 5 also sends out a 5 m shockwave: 20 damage and knockback to other enemies.
    - Victim total about 75. The usual damage scaling and chips apply per hit.
  - **Rules during the sequence:**
    - Caster and victim are intangible to everyone else and cannot be interrupted. The victim has no control and
      takes only the sequence's hits.
    - The sequence ends early and cleanly if either player disconnects or the round ends.
    - The victim cannot die before step 5. Lethal damage is held to the final hit, so the kill lands on the
      finisher.
  - **Walls and edges:** every waypoint is clamped against arena collision and bounds. If the launch hits a wall,
    the victim wall-splats and the sequence continues from there. The victim never ends outside the arena or in
    a hazard; teleports skip to the nearest valid point.
  - **Replication:** sequence start time, anchor, facing, seed, the resolved waypoints (or enough data to
    resolve them identically), the victim id, an outcome flag (whiff, connect, ended early), and per-step hit
    events (Detail `kernel-panic-1` to `-5`).
  - **Implementation contract — Astra, 2026-10-12 (`codex/unity-kernel-panic-20261012`, base `da5b9c1`):**
    - Kernel Panic simulation and the existing `InputFrame.SirenAbility == 4` dispatch are implemented on this
      branch. Cyber Lord itself is still spec-only at this base: this change does **not** register an incomplete
      weapon, add its other abilities, or expose a selectable class. Claude's cut-in, choreography, cameras and
      HUD remain presentation work. The full kit/roster integration must land before player acceptance or release.
    - Proposed base numbers above are retained: .25 s opener, .6 s cut-in, five steps at 0/.45/.95/1.35/1.9 s,
      recovery at 2.6 through 3.4 s, 60 mana and 30 s cooldown. The opener captures but adds no extra damage:
      12 + 10 + 10 + 3*6 + 25 = **75** before normal scaling/defenses. Resource-cost chips scale the 60 mana.
      Whiff uses .6 s recovery and a cooldown deadline of cast time + 15 s; mana is not refunded. The wave's
      previously unspecified impulse is **260** (4 m/s before knockback modifiers), with the proposed 5 m/20 damage.
    - A real .6 m-radius capsule sweep follows the input aim, limited by world collision and arena bounds.
      The nearest enemy pilot wins (id breaks ties). Block, roll immunity or a spawn shield prevent capture and
      count as a whiff. Already captured pilots, Siren-held/scared pilots and the PvE authority boss are not
      capture targets. Only a live DEV claim can cast; weapon grants do not authorize it.
    - `Actor.Kernel` (with `HasKernel` for Unity JSON presence) is a deep-copied `NativeKernelPanic` on both
      participants. It carries `Id`, `Caster`, `Victim`, `Seed`, `At` (cast), `ConnectedAt` (cut-in start),
      `Until`, `Ready`, `Anchor`, unit `Facing`, `CasterStart`, `VictimStart`, two **six-point** arrays
      `CasterPoints`/`VictimPoints`, safe `CasterExit`/`VictimExit`, `WallSplat`, `Next` and `Outcome`.
      Positions are **Rigidbody centres in native metres** (subtract .865 m for Actor foot positions).
      `NativePilotFrame.kernelReady` carries the cooldown separately. Outcome: 0 windup, 1 connect,
      2 whiff, 3 ended early, 4 complete. The `Id` distinguishes later casts; do not infer a cast from events.
    - Use `NativeKernelPanicRules.Sample(state, presentationTime, out caster, out victim)` for the resolved
      choreography. It holds the start positions during the cut-in, interpolates the victim between host points,
      teleports the caster at each step, and settles both at recovery. Late join/killcam do not replay old events.
      `Next` is the count of committed steps, not a pose clock. The seed selects the side offset and is also
      available for deterministic clones/VFX. Wall-clamped launches set `WallSplat` explicitly.
    - Each damaging beat emits `Hit` with Detail `kernel-panic-1` ... `kernel-panic-5`; step 4 emits three
      six-damage hits with the same Detail. A fully held hit still emits a zero-actual-damage event. The wave
      uses `kernel-panic-wave`. Damage scaling, normal direct-hit modifiers and impact chips apply on paid hits.
      After normal mitigation, damage below 1 HP is stored as `DamageDebt` and paid once on step 5; it is
      discarded on early cancellation. Reactive escape/panic chips are deferred to the finisher, and their
      teleport cannot break the lock. Normal lethal-save chips can still save against the finisher.
    - During connect, both colliders are disabled and bodies are kinematic; input and guest prediction are
      locked. Third-party attacks, stage damage and DoTs cannot affect the pair; only the explicitly authorized
      sequence hits can damage the victim. Existing outgoing projectiles/chips still affect other pilots under
      ordinary rules. The wave does not double-hit the captured victim. No mid-combo kill/interrupt.
    - Host waypoints use capsule sweeps, arena clamps and clear supported landing searches on a deterministic
      .5 m grid (up to 6 m around a candidate). Hazard columns are conservatively excluded, including inactive
      spikes. If no safe initial route is found, it whiffs. Geometry/hazard changes invalidating a sample abort
      before moving there. Cleanup tries nearby support, the prevalidated exit, then spawn support before the
      original bounded exit; supported maps require a permanent safe floor. Survivors regain their original
      collision/physics state; a dead victim remains dead. Disconnects and round changes use the same cleanup.
    - Verification on this branch: managed determinism/serialization, HP 1–100 lethal-debt conservation,
      isolation, catch-up/exactly-once steps and malformed-state checks. A genuine Unity build-gate fixture
      covers capture, DEV rejection, spend/refund, block, JSON path, damage isolation, lethal finisher,
      disconnect and round cleanup. That fixture is compile-checked here; actual PhysX/Windows execution,
      two-client latency, tight geometry and Claude's presentation acceptance remain required.
  - **Cameras:** for the caster and the victim, a cinematic camera that follows the choreography (the existing
    C16 cinematic camera rules). Everyone else keeps their own camera.
- **Stats:** base HP 100; class `melee` (unarmed) for the block, stamina and roll rules, with mana as the
  ability resource.
- **Replication:** the combo step, per-ability cast/active/end times, the dash path, the wall transform and
  expiry, the link target and end, the crash field position and end, the Kernel Panic sequence data above,
  and events for each hit (Detail per ability: `cyber-jab`, `cyber-cross`, `cyber-palm`, `data-dash`,
  `firewall`, `packet-burst`, `siphon-link`, `system-crash`, `kernel-panic-1` to `-5`).
- **Bots:** sandbox dummy and bot pickers follow the existing DEV rules. If bots can play it: combo in close
  range, Packet Burst under 6 m, Data Dash to close, Firewall when being shot, System Crash with two or more
  enemies within 4 m, Kernel Panic when a target is inside 6 m and not blocking.

Presentation (Claude):
- A DEV look on the pilot body: hardlight fists and forearm gauntlets that form from light, glowing seams and a
  holographic halo.
- An unarmed fighting stance and locomotion.
- Combo poses and effects for all six abilities, in third and first person.
- For Kernel Panic:
  - the skill cut-in, a stylised splash of the caster's live portrait with diagonal hardlight panels, glitch text
    "KERNEL PANIC" and the pilot's colours;
  - the slim banner for other players;
  - choreography poses and effects for every step: launch streak, glitch-teleport tears, clone afterimages, the
    falling code column and the shockwave;
  - the cinematic camera path for caster and victim, within the camera-shake and reduced-effects settings.
- HUD cards for Q, E and 1-4, a class icon, a Locker tile, an entrance animation and audio routing on existing
  keys.

## Owner-facing defaults

- Everything above is tunable after playtest.
- The Bow is available to all players.
- Cyber Lord is DEV-only.
- The plunging stab is for the four standard melee weapons. DEV melee fighters keep their own air moves.
