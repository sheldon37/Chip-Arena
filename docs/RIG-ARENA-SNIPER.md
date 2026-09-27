# Articulated rig, readable Chips, expanded arena — 2026-09-27

## Model and animation

The blank mannequin has no permanent face, visor or helmet. `src/client/character-rig.ts` owns the joint hierarchy and named cosmetic/weapon sockets. Pelvis, spine, chest, neck, head, clavicles, shoulders, elbows, wrists, hips, knees, ankles and toes animate independently. Geometry can be replaced beneath these joints without rewriting attack animations.

`character-animation.ts` layers distance-driven locomotion with authoritative shot/swing timing. Arms swing during travel for every weapon; guns blend into braced poses after firing. Rifle/Machine Gun and Greatsword use analytic two-link arm IK to keep the support hand attached to the weapon grip. Walking/running counter-rotate pelvis and chest; knees, ankles, wrists and head follow. Separate sword, dual-dagger, shield and greatsword actions use torso rotation, weight shifts, windup, strike and recovery. Gun recoil/reloads and elemental casting have upper-body gestures. First person reuses the actual articulated arms and equipped weapons, including the second dagger.

Developer Sandbox → **Animation Inspector** lets you view Idle, Walk, Run, Attack, Reload, Block or Airborne from an orbiting camera. It pauses only local sandbox simulation and does not issue attacks or alter health. Return to **Play** to resume. Specialization and Chip controls work in the inspector.

Animations are authored procedural poses, not motion capture. Feet do not perform terrain-specific IK; some foot sliding remains on unusual slopes and while strafing. Weapon attachment sockets are reusable. Future custom death effects still use the `KILL_EFFECTS` registry; cosmetic voxel deaths and the five-second result hold remain.

## Noticeable modifiers

`weapon-visuals.ts` gives Chips physical equipment silhouettes: extra barrels or spell nodes, gold ricochet rings, blue velocity coils, tracking fins, splitter forks, extended magazine, rate coil, kinetic rails, resource/armor attachments, elemental blades and duplicate daggers. Stacks add modules or intensity within visual budgets.

`chip-effects.ts` adds projectile flame plumes, poison bubbles, ricochet rings, homing fins, splitter prongs, piercing tips, velocity streaks and impact rings. Melee trails follow the actual blade-tip socket. Existing numeric Chip mechanics are retained. Visual attachments are not extra hitboxes. Projectile modifier metadata is captured by the authority at launch and preserved on reflection. They do not reveal enemies through cover. Core readability geometry remains with reduced effects; decorative particle counts stay bounded.

## Arena and Rifle

- Arena: **64 × 56 m**, up from 27 × 25 m (5.31× floor area).
- Standard spawns: 42 m apart; sandbox retains close testing spawns.
- Central destructible cover/support collapse remains, with outer firing lanes, staggered cover, side decks and short stair approaches.
- 92 initial prop bodies; 150-body cap remains. Near the cap, broken props detach whole instead of multiplying fragments. Bot edge avoidance follows the new boundaries; Rifle bots prefer roughly 32 m separation.
- Rifle: **58 direct damage, 3 rounds, 1.15 s interval, 2.6 s reload, 150 m/s initial speed, 12 m/s² projectile gravity**.
- E: one braced shot, **78.3 direct damage**, costs 2 rounds, 3.8 s secondary cooldown.
- Hold **Q**: scoped first-person view (28° vertical FOV; approximately 3× versus normal first person); reduced aiming sensitivity. RMB is still Block, with the unchanged independent four-second base cooldown.
- A level Rifle shot from 1.6 m reaches approximately 76 m before hitting the floor. Elevated aim and Velocity Coils extend reach. Projectiles remain swept, authoritative and subject to gravity, cover and the shared travel/lifetime bounds.

Configuration: `src/shared/arena.ts`, `content.ts`, `config.ts`; model/pose code: `src/client/character-rig.ts`, `character-animation.ts`; modifier visuals: `weapon-visuals.ts`, `chip-effects.ts`; first/third/scoped cameras: `renderer.ts`, `main.ts`.

## Validation

128 automated tests passed, including a real 44 m sniper hit, three-round ammo/reload enforcement, braced-shot cost, arm travel across all 11 specializations, support-grip alignment at three aim pitches, wide greatsword tip travel, captured modifier metadata/reflection, and inspector isolation. The existing destruction, DoT, Block, match, prediction, relay and launcher tests remain included. Fifteen combined-Chip stress cases completed 12,392 simulation steps without exceptions.

Browser inspection used the compatibility SVG renderer because this environment cannot create WebGL. It verified the new rig, travel/action inspector and equipped fire effect. This is not a hardware WebGL frame-rate benchmark. Windows batch execution and temporary public tunnel behavior still require the laptop environment.

Two real WebSocket clients completed a five-set Rifle match and rematch with 100 ms artificial delay in each direction. All 2,822 shared snapshots matched exactly, including ten round results, four drafts and destruction. Disconnect timeout passed. In this Linux server test, median simulation tick was 0.14 ms and p95 was 0.47 ms (not a browser FPS measurement). An input-driven Rifle-versus-Normal-bot one-set match on the expanded arena also completed in 623 simulation ticks.
