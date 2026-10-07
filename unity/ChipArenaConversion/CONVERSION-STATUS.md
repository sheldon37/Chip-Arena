# Native conversion status — melee specials and recovery

Target approved by the owner: **native Unity/C#**, with gameplay simulation and physics converted too. Branch: `codex/unity-conversion`. Source remains `05950ea`; the accepted lab and original game are preserved separately.

**The full-game conversion is incomplete.** Imported definitions do not establish implemented mechanics. This checkpoint adds all four standard melee E actions, thrown weapons, Shield knockdown and bounded pickup/juggle/roll-away recovery. Primary sweeps, footwork, timed blocks/ripostes and corrected external impulses remain included. The first-person, staff/glow and gun/magic ability work remains included. Native Windows visual/combat acceptance is still required.

| System | This checkpoint | Remaining work |
| --- | --- | --- |
| Catalogue | Exact exported definitions: 15 weapons, 236 Chips, 14 Q/E entries, rarity/exclusion tables | Remaining special mechanics and integrations |
| Stats and draft eligibility | Native C# matches 60 source cases / 4,620 values, eligibility, seeded offers and roll profiles | Broader gameplay interaction parity |
| Match flow | Native countdown, round/set wins, draw replay, losing-side draft, bot skip, rematch | Specialization screen, intros, exact draft staging, disconnect/rejoin and complete options |
| Source maps | 15 collision layouts / 3,451 blocks; rotations, masses and support IDs retained; moon/rim lighting, fog, shadows and wood/metal trim | Full scene dressing/material detail, weather/music and Ward Zero machinery/vents |
| Gameplay physics | Unity capsules/rigid bodies, CCD, swept shots, support release, breakable props | Source external-velocity decay/counter-steering and one-shot lift now ported; native Windows acceptance, collision feedback/stepping, debris damage and complete destruction remain |
| Standard melee | Four primaries with source sweep filters, once-per-swing contacts, footwork, block rebound/riposte and human roll-cancels; four E actions, Shield knockdown, one pickup window, capped air follow-ups and protected roll recovery | Native collision/feel acceptance, body/head hit fidelity, modifier Chips and full production animation; inactive legacy held-guard path intentionally not activated |
| Guns | Three primaries, magazines, reload, ADS, gravity, reflection and head/body hits; Frag/Rocket/Orbital Mark E handlers | Native ability contacts, source collision/aim and animation polish |
| Magic | Four primaries, source cast phrases, left-hand staff/guard, eight Q/E handlers, mana/cooldowns, basic fields/status effects | Native verification, exact collision/impulse integration and production VFX |
| Chips in combat | 89 stat-only entries enabled; unimplemented effects are not offered | Remaining 147 entries, special effects, summons, proc ownership/recursion and combinations |
| Stage hazards | Source C# schedules for saws, spikes and lava; basic native visuals/contact/swimming | Native acceptance, production effects, Siren song/vent hazards |
| DEV weapons | Definitions are reference-only; unavailable for match selection | Ansatsuken, Emperor, L33T H4x, Siren, associated exclusive mechanics and entitlement gates |
| PvE / Siren | Source inventory only | Director, waves/horde, boss phases, economy, machines, picks, downed/revive, recruits, completion cleanup |
| Animation | Captured melee / procedural guns, source magic phrase math, adapted staff IK, gun ability gestures and standard melee special/recovery poses | Full source spring/IK/gait fidelity, intros, cinematics, body wounds, ragdolls and kill effects |
| Camera | Saved V/menu toggle, isolated first-person rig, interpolated body following, continuous animation clock, smooth ADS/sight alignment and source-style rifle scope | Native frame-pacing/framing/obstruction acceptance, full source sway/bob/recoil layers |
| Combat effects | Source-style additive projectile/armor halos, 0.18 s trails, 0.85 s attached surface marks, seven pooled spill lights, soft zone/explosion fills and saved quality setting | Native visual/performance acceptance, elemental modifiers, full spell geometry/particles and production effects |
| Multiplayer | Not connected to this native authority | Native host/guest transport, eight-player tests, prediction, reconnect and isolated service configuration |
| Accounts / customization | Separate local test name/settings only | Native identity adapter, entitlement verification, cosmetics, persistence and safe migration design |
| UI / audio | Foundation setup/HUD/scoreboard/draft; six original gun/reload/hit WAVs | Production menu/card UI, audio library/music, intros, killcam and accessibility/options |
| Packaging | Separate Unity project, executable name, save identity and CA icon | Activated Windows build and end-to-end acceptance |

The original 35 managed lab checks are retained as regressions of the inherited code. They do **not** prove the new `NativeArena` physics authority behaves correctly. The new native rule fixtures and match checks test their own code; the Editor physics gate is separate.

One intentional contact change: lava is checked against its individual channel, preventing the source's two channel entries from applying duplicate damage to a pilot in either channel. Clock schedules still match the original fixtures; overall hazard damage/physics parity is not claimed.

## Conversion sequence

1. **Implemented; Windows gate pending:** content parity, native layouts/physics and first match loop. Confirm walking/climbing, collision, dynamic supports and round reset on the owner's activated Unity installation.
2. Complete standard combat parity: verify primary sweeps/footwork/riposte plus the new melee specials and knockdown/juggle/recovery; continue native acceptance and source parity of gun/magic Q/E handlers, health/status/impulse rules, projectile modifiers and native network ownership.
3. Implement every Chip handler against source-derived scenarios; retain exclusions and proc-recursion rules. Do not enable a catalogue entry merely because its data imported.
4. Port DEV styles, Siren and all PvE contracts/director/economy/AI. Carry the current wave cleanup and impact-effect lifetime behavior.
5. Port the remaining models, animation layers, maps/scenery, VFX, audio, cosmetics, intros, body damage, kill effects and killcam.
6. Complete the native lobby, online transport/prediction, identity and persistence against an isolated test configuration. No live save/account migration or updater cutover is authorized by this checkpoint.
7. Full regression and Windows/two-or-more-client acceptance, then owner evaluation of performance and game feel. Merge/publication remain separate decisions.

## Known boundary of this pass

The eleven standard Q/E handlers are implemented, including resources, short buffering, grenade bounce/fuse, rocket arming, mark tracking, lance piercing, wave slow, blink, shackles healing, meteor ground fire, three geysers, ball-lightning zaps and black-hole pull/collapse. Source pure-function fixtures do **not** establish native projectile/contact parity. Ability status/proc combinations and impulses need Windows gameplay comparison. Modifier Chips remain excluded, keeping the existing 89-entry draft filter.

Magic phrase channels match original functions; Unity bone blending/staff IK, guard transitions, gun ability gestures and first-person framing are adaptations. The staff uses Claude’s 0.85 scale, outward 20° roll and torso settling. Native geometry needs a 5 cm view-model lift and a brief transition dip to retain a clear reticle while entering/leaving the guard. These offsets affect presentation only. First-person excludes local head/body from the world pass and draws forearms/weapons through a depth-only camera; its geometry never determines hitboxes or muzzle obstruction. The rifle scope mask is present; full camera sway/bob and remaining production effects are pending. Arena detail is procedural colored voxel trim, not a texture-map conversion.

No deadline or full-game performance improvement is claimed. The first required external result is the native map/physics gate and playtest; this workspace can compile against Unity APIs but cannot run an activated Windows Editor or observe the user's game.

Glow uses the original radial falloff and effect lifetimes with native batched geometry. Unity built-in light intensity uses a 0.35 gain because its units differ from the source renderer. Surface marks are not capped or evicted while alive; shader import, brightness, transparent ordering and GPU cost require the Windows playtest. No fullscreen bloom has been added.

## Stability boundary

The confirmed feedback bug was reproduced in pure rig math: five extra renders at the same simulation time moved the magic wrist 0.0343 m in rig space. Base-layer history now excludes procedural/first-person adjustments. The camera reads the interpolated Transform, while physics, hitboxes and muzzles still use authoritative Rigidbody state. Render snapshots copy pilot scalar state before changing visual positions. The final camera also supplies the next aim ray.

Animation time may advance by at most one fixed step (16.7 ms) between simulation ticks; this is presentation only. Camera positional interpolation normally introduces one physics-step delay, as documented by Unity. No input rotation smoothing was added. The default scene handles Rigidbody interpolation automatically; the conversion does not call InterpolateBodies on it. These code changes and numeric tests do not establish that every source of the owner's observed Windows jitter is resolved.

## Melee boundary of this pass

The primary sweep's source expressions and pure commitment/block/movement functions are pinned and independently evaluated by tools/export_melee.mjs. Fixtures cover 64 swings at four attack rates, 448 timing samples, 5,376 candidate contacts, 90 block cases and nine external-motion cases. They verify C# math, not native collision or controller feel. NativeMeleeValidation adds actual Unity ray/contact/defense integration gates to the Windows builder; they have not run in this workspace.

Base damage/costs, cooldown values and collider dimensions are unchanged. Actual damage timing now follows each tick of the active sweep, with one contact per target; movement uses attack steps and corrected external impulses. This intentionally changes the previous native placeholder behavior. Block cannot cancel a committed attack. A human roll-cancel does not refund stamina; only a cancel at least half a tick before contact shortens the remaining attack cooldown. Counter protection lasts only the remainder of the existing block window, not the entire attacker stun. Standard combo indices continue through the round, matching the source instead of resetting after a DEV-style 2.4 s timeout.

The current source sets Shield E to shield-throw, leaving its earlier held-guard branch inactive. That obsolete guard path was not made playable. The inherited wire protocol is unchanged and does not network the native authority.

## Special/recovery boundary of this pass

Sword launch, Dagger throw/reflection, Shield outbound hit/harmless return/catch and Greatsword leap/direct strike/landing slam are implemented. The leap shares one target ledger with its slam. Throws use the scene sphere sweep plus explicit starting-overlap checks, an arena-wide projectile cap and the source ballistic/ceiling rules. Headshots retain the native approximate capsule-height test; source body-wound/head volumes and unsupported bounce/proc/summon handlers are not ported by this pass.

Knockdowns land only after grounded contact, expose one 0.22-second pickup window, permit at most two early air follow-ups, then protect ground recovery. The recovery roll latches WASD or backward aim direction and runs at the source 6 m/s for 0.4 seconds. Death and round reset clear state. Stagger escape immunity remains active. These are standard-melee knockdowns, not DEV juggle, PvE downed/revive or physical ragdolls.

Source special envelope math and recovery timestamps are ported; joint tables are adapted to the captured native guard, wrist geometry and voxel floor clearance. Recovery uses stateless landing estimates rather than the source per-rig air memory/planting solver. First-person Greatsword keeps braced arms without rotating the camera; knockdown temporarily uses third person and restores the saved preference. No rendered-pose feedback is added to animation history. Exact production IK, landing continuity, weapon framing and full visual fidelity still need Windows comparison.

Source fixtures cover 96 start gates, 324 strike filters, 216 recovery samples, 36 ballistic steps, 24 ceiling cases and 108 animation envelopes; 2,400 native pose samples check finite/repeatable transforms and floor clearance. These establish math contracts only. The new NativeSpecialValidation integration gate is compiled into the builder but has not executed here. It checks real query/handler paths, launch/pickup, protection, real landing, reflection and shield retrieval.
