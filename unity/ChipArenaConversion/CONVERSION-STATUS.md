# Native conversion status — camera, casting and abilities checkpoint

Target approved by the owner: **native Unity/C#**, with gameplay simulation and physics converted too. Branch: `codex/unity-conversion`. Source remains `05950ea`; the accepted lab and original game are preserved separately.

**The full-game conversion is incomplete.** Imported definitions do not establish implemented mechanics. This checkpoint continues the native foundation with first-person presentation, corrected magic poses, standard gun/magic abilities and scene lighting. Native Windows visual/combat acceptance is still required.

| System | This checkpoint | Remaining work |
| --- | --- | --- |
| Catalogue | Exact exported definitions: 15 weapons, 236 Chips, 14 Q/E entries, rarity/exclusion tables | Remaining special mechanics and integrations |
| Stats and draft eligibility | Native C# matches 60 source cases / 4,620 values, eligibility, seeded offers and roll profiles | Broader gameplay interaction parity |
| Match flow | Native countdown, round/set wins, draw replay, losing-side draft, bot skip, rematch | Specialization screen, intros, exact draft staging, disconnect/rejoin and complete options |
| Source maps | 15 collision layouts / 3,451 blocks; rotations, masses and support IDs retained; moon/rim lighting, fog, shadows and wood/metal trim | Full scene dressing/material detail, weather/music and Ward Zero machinery/vents |
| Gameplay physics | Unity capsules/rigid bodies, CCD, swept shots, support release, breakable props | Native Windows acceptance; Rapier response comparison, stepping, debris damage and complete destruction |
| Standard melee | Four primary weapons with captured sets and source base values | Full contact sweeps/footwork/riposte parity, shield guard, E moves, knockdown/juggle rules |
| Guns | Three primaries, magazines, reload, ADS, gravity, reflection and head/body hits; Frag/Rocket/Orbital Mark E handlers | Native ability contacts, source collision/aim and animation polish |
| Magic | Four primaries, source cast phrases, left-hand staff/guard, eight Q/E handlers, mana/cooldowns, basic fields/status effects | Native verification, exact collision/impulse integration and production VFX |
| Chips in combat | 89 stat-only entries enabled; unimplemented effects are not offered | Remaining 147 entries, special effects, summons, proc ownership/recursion and combinations |
| Stage hazards | Source C# schedules for saws, spikes and lava; basic native visuals/contact/swimming | Native acceptance, production effects, Siren song/vent hazards |
| DEV weapons | Definitions are reference-only; unavailable for match selection | Ansatsuken, Emperor, L33T H4x, Siren, associated exclusive mechanics and entitlement gates |
| PvE / Siren | Source inventory only | Director, waves/horde, boss phases, economy, machines, picks, downed/revive, recruits, completion cleanup |
| Animation | Captured melee / procedural guns, source magic phrase math, adapted staff IK and gun ability gestures | Full source spring/IK/gait fidelity, intros, cinematics, body wounds, ragdolls and kill effects |
| Camera | Saved V/menu toggle, dedicated first-person forearms/weapons, source eye height/FOV/distance, ADS sight alignment | Native framing/obstruction acceptance, source sway and scope-mask polish |
| Multiplayer | Not connected to this native authority | Native host/guest transport, eight-player tests, prediction, reconnect and isolated service configuration |
| Accounts / customization | Separate local test name/settings only | Native identity adapter, entitlement verification, cosmetics, persistence and safe migration design |
| UI / audio | Foundation setup/HUD/scoreboard/draft; six original gun/reload/hit WAVs | Production menu/card UI, audio library/music, intros, killcam and accessibility/options |
| Packaging | Separate Unity project, executable name, save identity and CA icon | Activated Windows build and end-to-end acceptance |

The original 35 managed lab checks are retained as regressions of the inherited code. They do **not** prove the new `NativeArena` physics authority behaves correctly. The new native rule fixtures and match checks test their own code; the Editor physics gate is separate.

One intentional contact change: lava is checked against its individual channel, preventing the source's two channel entries from applying duplicate damage to a pilot in either channel. Clock schedules still match the original fixtures; overall hazard damage/physics parity is not claimed.

## Conversion sequence

1. **Implemented; Windows gate pending:** content parity, native layouts/physics and first match loop. Confirm walking/climbing, collision, dynamic supports and round reset on the owner's activated Unity installation.
2. Complete standard combat parity: melee sweep/contact/recovery, directional blocks/ripostes, native acceptance and source parity of gun/magic Q/E handlers, health/status/impulse rules, projectile modifiers and native network ownership.
3. Implement every Chip handler against source-derived scenarios; retain exclusions and proc-recursion rules. Do not enable a catalogue entry merely because its data imported.
4. Port DEV styles, Siren and all PvE contracts/director/economy/AI. Carry the current wave cleanup and impact-effect lifetime behavior.
5. Port the remaining models, animation layers, maps/scenery, VFX, audio, cosmetics, intros, body damage, kill effects and killcam.
6. Complete the native lobby, online transport/prediction, identity and persistence against an isolated test configuration. No live save/account migration or updater cutover is authorized by this checkpoint.
7. Full regression and Windows/two-or-more-client acceptance, then owner evaluation of performance and game feel. Merge/publication remain separate decisions.

## Known boundary of this pass

The eleven standard Q/E handlers are implemented, including resources, short buffering, grenade bounce/fuse, rocket arming, mark tracking, lance piercing, wave slow, blink, shackles healing, meteor ground fire, three geysers, ball-lightning zaps and black-hole pull/collapse. Source pure-function fixtures do **not** establish native projectile/contact parity. Ability status/proc combinations and impulses need Windows gameplay comparison. Modifier Chips remain excluded, keeping the existing 89-entry draft filter.

Magic phrase channels match original functions; Unity bone blending/staff IK, guard transitions, gun ability gestures and first-person framing are adaptations. First-person excludes local head/body from the world pass and draws forearms/weapons through a depth-only camera; its geometry never determines hitboxes or muzzle obstruction. Scope masks, camera sway and full production effects remain pending. Arena detail is procedural colored voxel trim, not a texture-map conversion.

No deadline or full-game performance improvement is claimed. The first required external result is the native map/physics gate and playtest; this workspace can compile against Unity APIs but cannot run an activated Windows Editor or observe the user's game.
