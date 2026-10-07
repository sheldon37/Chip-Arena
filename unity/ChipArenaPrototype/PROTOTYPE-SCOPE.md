# Scope and comparison limits

Base: `sheldon37/Chip-Arena`, `claude/visual-revision` at `05950eaef80ff2483a9c44d898cad98be1a76bbc`, including the Oct 6 wave cleanup and persistent-impact updates. This branch adds an independent Unity project. It does not integrate the Unity project into the Electron launcher or any feed.

## Source-grounded constants

The following values were read from that exact source, not inferred from animation footage:

| Values | Source |
| --- | --- |
| 60 Hz simulation, 20 Hz snapshots; 6.8 movement speed, 35 acceleration, 8.5 jump speed, 23 gravity magnitude, .42 actor radius | `src/shared/config.ts` |
| 24/s stamina regeneration, .45 s delay, .30 s block, 4 s block cooldown, up to 3 projectile reflections | `src/shared/config.ts` |
| 1.35× sprint speed, 12/s drain, 300 ms double-tap, .6 forward dot threshold | `src/shared/sprint.ts` |
| Sword 30 damage, 14 stamina, .14 s windup, .62 s interval, 2.5 reach | `src/shared/content.ts`, `src/shared/souls-combat.ts` |
| Sword roll .4625 s, 5.3 distance, 24 stamina, .75 s cooldown; i-frames .075–.255 s | `src/shared/souls-combat.ts`, weight 5 with base stats |

These constants do not make the prototype a faithful port. Its rule implementation is independently written and simplified.

## Implemented in this slice

- Local combat and 2-player host-authoritative LAN synchronization.
- Sword attacks, one buffered follow-up, three captured sword cuts, a resource bar, timed defense, roll cancellation, partial dodge invulnerability and respawn.
- Swept energy bolts, block reflection, breakable static cover and small visual effects.
- Pistol, Rifle and Machine Gun primary fire, magazines/reloads, Q aim/zeroing, gravity, spread and headshots;
  source constants and limitations are listed in RANGED.md. Gun models, procedural poses and six original sounds are included.
- Source rig-v2 body proportions, 34 captured animation clips, native Unity clip playback and a four-set inspection viewer (see ANIMATIONS.md).
- Instanced voxel characters and source-only custom shaders in the built-in render pipeline.
- Up to 192 bots, with mixed gun or original sword/bolt workloads and stationary targets. The workload is reproducible by count, not a production Siren horde port.
- Native Unity PhysX for decorative, temporary breakage/death fragments. They do not cause gameplay damage. A 512-fragment creation budget and 6-second natural lifetime prevent unbounded diagnostic memory growth. Existing fragments are not replaced early.
- 20,000-particle emitter budget; bounded transient network-event history. These are prototype budgets, not changes to the owner's existing game limits.
- On-screen measurements and CSV recording; separate product/storage identity.

## Deliberate omissions and differences

- Movement/contact are a small flat-arena C# kinematic model, **not** a Rapier-to-PhysX gameplay port. Cover collision is expanded AABB; projectiles use sphere sweeps. Actors do not physically collide with each other; platforms, steps and complex levels are not supported.
- Sword hit geometry, stagger, knockback, parry follow-through, combo buffers and regeneration details are simplified. The game’s committed retargeted mocap is included, but procedural footwork, recovery and blade clearance are only partially adapted. This does not preserve the exact feel, balance, animation contacts or collision shapes of the live game.
- Sword's energy bolts are diagnostic, unlimited-ammo shots with prototype damage/cadence. Guns use source primary/aim constants and unlimited reserve ammunition. Gun E abilities, magic classes, Chips, dev styles, Siren boss behavior, rounds/drafts and economy are omitted.
- The 34 existing encoded Explosive-derived captures are reused privately; no source FBX, pack manifests, commercial textures/music or original pack models are added. Native Humanoid/FBX retargeting and 9CG compatibility remain untested. This update samples legacy clips on a custom articulated rig, not a production Animator Controller.
- No production prediction/reconciliation, lag compensation, room service, accounts, saves, matchmaking, eight-player validation, updater, installer or release-channel integration.
- Built-in render pipeline was selected to minimize dependencies for the first import. URP/HDRP-specific effects are not automatically compatible. A render-pipeline decision and native asset evaluation are next-stage work.

## Acceptance before a larger port

1. Import, compile shaders, play, and build in the pinned Unity Editor; record actual editor output.
2. Compare movement/camera/combat feel against the current game, not just a blank Unity scene.
3. Test host/guest input, defense, damage, reset and disconnect using two actual Unity windows.
4. Record 8/32/96/192-bot workloads at the same resolution and settings. Profile CPU, GPU, allocations and PhysX separately.
5. Visually validate the imported captures, rig, blending, floor clearance and strike synchronization in the viewer and live sword arena. Raw FBX/Humanoid evaluation remains a separate test.
6. Choose the production physics/network/asset architecture before porting the full catalog.
