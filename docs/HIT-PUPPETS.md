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

## Direction and location (P2, 2026-11-23)

Owner: "Currently; we have essentially the same hit reaction for all hits. I want to really use the new ragdoll asset ... I
want the animation to actually display hit direction (Like reactively)". Branch `claude/puppet-hits-2`.

**Why every hit read the same (measured on release 15 in the Editor, the bridge's per-hit log):**

- **The model's hit clip ignored the blow.** On a hit the model plays one of its set's four directional GetHit clips (front,
  back, left, right) for about a quarter second. It was picked by `Actor.ReactionYaw`, which is the struck pilot's own facing
  (Core `MeleeReaction.Hit`), not the blow's bearing. A bot that did not turn played the same clip for all 16 hits of the sheet,
  and that whole-body clip is most of what the eye reads. Now `NativeVisualMotion.HitSide` takes the side from
  `Actor.ReactionDir` (the push in the pilot's facing frame).
- **Every pilot hit point rose .865 m.** The authority's point was measured from `Actor.Position` (the feet) and added to the
  rig root (the body centre). Chest and arm hits landed above the head and went to the upper chest; leg hits landed at the hips
  on the gun hand, capped to 2-7 N·s; a headshot struck the head bone (its pivot on the neck). Now the offset is from the
  body centre (`NativeHitPuppetRules.CentreHeight`); adds were already measured floor to floor.
- **A blade's blow carried no swing.** The authority's melee point is the hit volume's point nearest the attacker's waist and
  its push is straight from the attacker (Runtime `NativeMelee`), so a left-to-right slash and a right-to-left one were the same
  push at the same point. Now the swing the attacker played is read as a kill's cut reads it (Core `NativeMeleeSlice.Plan`):
  the push turns along the blade's travel and lands on the side it comes in from (`NativeHitPuppetRules.Swing`).
- **The puppet had no legs and fixed hips**, so a leg hit could not buckle anything, and the unpin stayed local (BehaviourPuppet's
  falloff from the struck muscle) whatever the weight.

**What it does now** (bridge version 2; the hook's optional `Strike` and `Bounds`; game code: points, swings, reach, the clip):

- The model's hit clip is the blow's side (`HitSide`), and only a heavy blow off the head plays one (`ReactionStrength` at its
  full 1, 38 damage or more: a greatsword's blow, the Siren's claws, a heavy fighter strike; `NativeVisualMotion.HitClip`).
  Rounds, cuts and headshots play no clip: the physical reaction shows them, so the struck part leads, a rifle round is a jolt
  rather than the clip's lunge, and a burst's rounds add up on the body instead of restarting one clip a round.
- The part struck is the first of the trunk, an upper arm or a leg that the shot's line passes through, nearest the shooter. A
  forearm or hand (held out with a gun) is struck only when the line passes through nothing else. The push lands on the part's
  surface facing the shot. A headshot's point is on the head's side the shot came from.
- A round within .15 s of the last (a machine-gun burst) pushes half, so a burst shudders and builds to about one chest
  round's rock instead of folding the trunk.
- Each zone adds the body's answer in the same simulation: the head snaps away with the chest a little behind; an arm swings
  back and turns its shoulder; a gut hit from the front folds the chest over and pushes the hips back; a hit from behind
  lurches the trunk forward; a side hit leans the trunk away and turns the struck shoulder back; a leg hit drops and rolls the
  hips on that side and the knee bends under them.
- Weight (`NativeHitPuppetRules.Spread`): a pistol round's unpin stays local, a rifle's reaches the spine, a greatsword's heavy
  blow the whole body and drops the hips. The pins come back slower the further it reached. Hits before the pins are back add up.
- Adds react the same way; `Loose` (shambler 1 .. Hulk .1) softens their unpinned muscles, slows their return and lets them
  turn a third further.
- **Bounds on the bend.** Each bone turns at most so far off the drawn pose (spine 16/18/20 degrees, neck 20, head 30, upper arm
  45, forearm 50, hand 30). A greatsword's blow turned the chest 160 degrees in the first tune; it now bends the trunk at most
  about 55 degrees.
- **What the hips may do:** the drawn pelvis is moved by a spring the hits kick (a leg hit: drop, roll toward the struck side,
  shift along the push; a whole-body blow: drop and shift; a gut hit: back), never by the simulated pelvis: that one is the root
  every push jolts, and mapped it shook the whole body for a frame after each hit. It may drop at most 11 cm, shift at most 6 cm
  level and turn at most 14 degrees from where the game drew it, and never rise more than 5 mm. Each foot is put back exactly
  where the game planted it (a two-bone solve of thigh and calf, the knee bent in the plane the reaction left it in); measured
  slip at most 0.6 mm. The model's root never moves.
- **The hitbox.** The bent trunk (hips, spine, neck, head) never takes a point further out of the capsule (a pilot's 0.42 m round
  its root; an add's own) than the capsule or the drawn pose had it: the trunk's bend is eased back as far as needed. Each arm is
  eased back on its own (an arm holding a gun is past the capsule as drawn; only reaching further out than that is eased back).
- First person: the local pilot's body is not drawn, so its view never moves with its own reaction (third person: the camera
  follows the authority's body, not the drawn bones).

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
- **Rotations, and the hips within limits.** Every bone but the pelvis keeps the game's local position; the pelvis moves only
  within the limits above, the feet stay where the game planted them and the model's root where the game put it (bridge 1
  mapped no hips at all). Nothing writes a rule, a hit volume, a timing, a body or the wire (`HitPuppetChecks`).
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
- `ChipArenaPuppetBridge/ChipArenaPuppetBridge.cs`: registers the seven hooks by name (reflection), and the optional `Strike`,
  `Bounds` and `Trace` where the game has them, so it compiles, and registers nothing, on a checkout without the hook. Version 2
  (P2, 2026-11-23; master copy in `F:/Chip Arena Development/scratch/puppet-hits-2/local/`) logs `Hit puppets bridge: version
  2 ...` when it registers. In the Editor, `Temp/hitpuppets-off.flag` leaves the hooks empty for a play (review captures'
  "before" on the same build) and `Temp/hitpuppets-log.flag` logs every hit (`HPQ` the game's read, `HPH` the part struck and
  the push) and every step for a second after it (`HPS`: pins, muscle weights, turns off the drawn pose, the hips, the feet,
  the hitbox's ease). A game without `Strike` (release 15) still works with it, and the first bridge still works with this
  game (it reacts through `Hit` with the fixed points and swings; no hips, legs or bounds).

The bridge builds twelve muscles (pelvis, spine_01, spine_03, spine_05, neck_01, head, and each arm's upperarm, lowerarm,
hand) under a PuppetMaster with a BehaviourPuppet set never to lose balance or get up, steps them through PuppetMaster's
manual simulation (`OnPreSimulate`, the scene's `Simulate`, `OnPostSimulate`: the game runs Physics in Script mode) and
sleeps them with `DisableImmediately`. The legs (thigh, calf, foot) are found but not simulated: they are planted. The bodies'
bones are the bake's (the UE mannequin names); a body missing a muscle's bone gets no puppet, one with a lost leg no hips.

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

**Bridge version 2 (P2):** the same 106 paths (only the bridge's `.cs` content changed; its `.meta` and GUID are the same), so the
allowlist is unchanged (copy beside the master: `F:/Chip Arena Development/scratch/puppet-hits-2/local/private-addons-allowlist.json`).
After merging the branch: copy `F:/Chip Arena Development/scratch/puppet-hits-2/local/ChipArenaPuppetBridge.cs` over the Editor
checkout's `Assets/LocalAssets/PrivateAddons/ChipArenaPuppetBridge/ChipArenaPuppetBridge.cs` (under the Editor lock, then a
refresh), then repack and check as in steps 2 and 3. Until the zip is repacked the build reacts through the first bridge
(directional points, swings and hit clips from the game; no hips, legs or bounds).
