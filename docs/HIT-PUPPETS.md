# Physical hit reactions (PuppetMaster)

Owner (2026-11-21): "Utilize Ragdoll Asset for hit reactions" (RootMotion's PuppetMaster, bought that day).

Branch `claude/puppet-hits` (P1). Code: `Core/NativeHitPuppetRules.cs` (caps, reach, the push), `Runtime/NativeHitPuppets.cs`
(the hook), `Runtime/NativeHitPuppetPresentation.cs` (hits in, steps, sweep). Checks: `Tests/HitPuppetChecks.cs`
(`--hit-puppets`). The licensed code and its bridge are local only (below).

## What it does

A pilot (player or bot) or an add drawn on the model that takes a non-lethal hit has its drawn pose pushed by an active
ragdoll: the muscles near the hit unpin, an impulse lands at the hit point along the blow, and the pose regains (about a
second, then the mapping fades out and the puppet sleeps). The push grows with the blow (`NativeHitPuppetRules.Push`): the
felt weight (Core NativeStagger: Flinch, Step, Heavy), its strength within that weight and, for a blade, the weapon's heft. A
greatsword's heavy blow is about 79 N·s, a sword's 35, a rifle round 26, a pistol round 11, a headshot under half of a body
shot. The bridge caps the start it gives a part at 3.2 m/s over what that part carries (a hand or the head is never flung), and
never strikes the neck itself: a blow there lands on the chest. Damage ticks, fields, thorns, hazards and stage hits push nothing.

## Presentation only

- **Hits in.** A pilot's hits are read from the frame's replicated events (`ObserveHits`, beside the felt-hit ledger), an add's
  from `HordeHit`. Every client reacts to the events it already receives. A killing hit pushes nothing: the death's own
  presentation takes the body.
- **Each on its body's clock.** A hit waits for its time on the rig's render time (a pilot) or the horde's field clock (an add),
  so it lines up with the existing flinch, stagger and flash; a stale one is dropped.
- **Stepped right after the pose.** Each puppet steps at the end of its body's pose (`VisualBodyPose`, `ZombieModelBind`) and
  before anything reads the bones: hats, gear, cloth and effects follow the bent pose. The held weapon is drawn at the game
  rig's hand, so the rig's hands are carried with the bent drawn hands for that frame (`VisualBodyRestore` puts them back
  before the next pose; the voxel ratchet pins this read).
- **Rotations only.** Every bone keeps the game's local position; the hips are never mapped, so the feet and the model's root
  stay where the game put them. Nothing writes a rule, a hit volume, a timing, a body or the wire (`HitPuppetChecks`).
- **Its own physics.** Each puppet is in a physics scene of its own (`LocalPhysicsMode.Physics3D`) and has no collider, so it
  never meets the game's world or another body.
- **Death.** A pilot killed while its puppet bends it falls from that drawn pose: `VisualRigFromDrawn` copies the drawn bones
  into the corpse, so the hand-over does not snap; gibbing, bisects and severs are unchanged. A dying add's puppet blends back
  while its fall plays.

## Cost

`NativeHitPuppetRules`: at most 8 bodies react at once on High, 4 on Balanced (half with Reduced effects), none on the low
graphics tier (Performance keeps today's look); within 28 m (18 m) of the camera; one puppet made a frame; 16 kept made (a
sleeping puppet's muscles are off: no cost a frame). A reacting add is posed every frame (the crowd pose clock never holds a
bent pose). The local bridge logs its cost once a second in the Editor (`Hit puppets: N frames stepped, step X ms/frame...`).

## The local install (never in git)

On the owner's PC, in the git-ignored `Assets/LocalAssets/PrivateAddons` (docs/PRIVATE-ADDONS.md):

- `RootMotion/PuppetMaster/Scripts/**` (37 runtime scripts: PuppetMasterLite left out, and `PuppetMasterSetup.cs`, the
  editor-time ragdoll setup, which names `UnityEngine.Cloth`: the project has no Cloth module) and 11 of `RootMotion/Shared Scripts`
  (BipedNaming, Comments, Hierarchy, Interp, LayerMaskExtensions, PropertyAttributes, QuaTools, Singleton, SolverManager,
  V3Tools, Warning), from `AssetStoreCache/RootMotion/ScriptingPhysics/PuppetMaster.unitypackage` with the pack's GUIDs. No
  demos, editor inspectors, models or scenes. No asmdef: they compile into Assembly-CSharp. (`tools/check_unity_compile.py`
  references every Unity module, so it cannot catch a missing one: a local add-on is compile-checked against the Editor's own
  `Library/Bee/artifacts/*/Assembly-CSharp.rsp` references.)
- `ChipArenaPuppetBridge/ChipArenaPuppetBridge.cs`: registers the seven hooks by name (reflection), so it compiles, and
  registers nothing, on a checkout without the hook. In the Editor, `Temp/hitpuppets-off.flag` leaves the hooks empty for a
  play (review captures' "before" on the same build).

The bridge builds twelve muscles (pelvis, spine_01, spine_03, spine_05, neck_01, head, and each arm's upperarm, lowerarm,
hand) under a PuppetMaster with a BehaviourPuppet set never to lose balance or get up, steps them through PuppetMaster's
manual simulation (`OnPreSimulate`, the scene's `Simulate`, `OnPostSimulate`: the game runs Physics in Script mode) and
sleeps them with `DisableImmediately`. The bodies' bones are the bake's (the UE mannequin names); a body missing one gets no
puppet.

## Shipping it to the builder

The builder installs private add-ons from the Drive zip through the existing, tested path (`tools/private_addons.py`,
`pack_visual_body.py --addons-allowlist`, `install_local_assets.py`); no tooling change. The integrator:

1. Puts the add-on tree in the Editor checkout's `Assets/LocalAssets/PrivateAddons` (P1's hold leaves it there) and keeps the
   reviewed allowlist (`private-addons-allowlist.json`, 106 paths: 49 scripts, their metas and 8 folder metas; master copies in
   `F:/Chip Arena Development/scratch/puppet-hits/local/`).
2. Repacks as usual with the allowlist added:
   `python -I tools/pack_visual_body.py <Drive zip> <new zip> --project <Editor checkout>/unity/ChipArenaConversion --with-cloth --addons-allowlist <allowlist>`
3. Checks it: `python -I tools/install_local_assets.py <new zip> --check --require-cloth`, keeps the old zip beside it, swaps it.

The Drive zip had no `private-addons.json` before this, so the allowlist replaces nothing. Without the add-ons the build plays
as today and logs "Hit puppets: bridge unavailable or incomplete; posed-body fallback."
