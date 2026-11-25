# Pilot body and hair (Astra's brief 2026-11-24, sections 1 and 2)

Owner, 2026-11-25: "lets get work started" on Astra's brief. Claude B1, branch `claude/pilot-body-hair`. Presentation only:
both bodies and every hairstyle keep the same hit volumes, reach, shot origins, movement and timing; no rule reads them.

## 1. The body choice

- **Key:** the look map's optional `body:female` (`Core/NativeBodyKind.cs`). Omitted (or `body:male`, never written) is
  today's pilot, so every existing look and account draws as before. Written after the five slots and before the costume and
  the skin tone: `upper:tee,body:female,skin:c48a62`.
- **Today's pilot**, measured from the bake: the pack's base body with every shape at 0 and no bone modifiers (the bake never
  applied Default_Boy's BodyType 45 or its modifiers). That stays the Male body exactly.
- **The female preset** is the pack's authored female reference, `Default_Girl`, on the same rig and bake:
  - body shapes Chest 60 and Curvy 100 (BodyType 0, as today's pilot), set by name on every worn mesh that has them, so the
    pack's clothes follow the body as they do in the pack;
  - face shape EyeRoundness 57 (head, lashes, brows);
  - the pack's Chest Size bone modifier: `spine_04` scaled .971 with the head counter-scaled to its own size (shoulders and
    arms 3 % narrower, the head, hair, hats and face gear unchanged);
  - its long lashes and thin brows, worn in place of the head's own when the bake carries them (look data slot `face`, ids
    `female-lashes`, `female-brows`; never a wardrobe slot);
  - the pack's stance (.95) belongs to its own Animator; the game's motion bank poses both bodies alike.
- **Fitted over the cloth:** the chest core rides out with the surface under it, and the robot armour's plates are carried
  with the garment under each one, both by those meshes' own shape deltas (`Runtime/NativeBodyKindPresentation.cs`). The
  wrist device rides the forearm (scaled with it); hats, helmet, visor and hair ride the head.
- **Where it shows:** every body made for the pilot (live, draft, first person, corpses, revive bodies, death scenes,
  portraits, the Locker stage) dresses through `VisualDress`, which applies the preset. Under the Hot Dog or the voxel costume
  the body is not reshaped.
- **The Locker:** Appearance opens with BODY (Male / Female cards with figures). Hovering a card tries the body on the stage;
  a pick writes the key and turns the stage once round on its turntable (none with Reduced effects or once dragged). Revert
  and the unsaved-changes baseline cover it.

### Until Astra's half of the contract lands

`NativeLook` normalisation drops keys it does not know, so the presentation keeps the body at its own edges
(`NativeBodyKind.Keep`): the Locker's writers, Save Pilot's local prefs, the shown look and this client's own pilot's bodies
(its seat look). The account profile and a room take the normalised look without the key, so peers draw today's pilot and an
account load on another PC brings back today's pilot. Astra's needs are in DEV_LOG (2026-11-25, B1).

## 2. The first hair sample

- **Styles (proposed Locker ids, Astra's to add to `NativeLook`):** `bob` (Bob: the pack's `HairFront_MinorFringe` +
  `HairBack_RoundBob`) and `high-ponytail` (High ponytail: `HairFront_SweaptBack` + `HairBack_LongPonyTail`). The same pieces
  on both bodies (the hair rides the head, which keeps its size).
- **Bake mapping** (the local tool's Wardrobe table, wardrobe bake): `hair|bob|HairFront/HairFront_MinorFringe,HairBack/HairBack_RoundBob`,
  `hair|high-ponytail|HairFront/HairFront_SweaptBack,HairBack/HairBack_LongPonyTail`, and the female face pieces
  `face|female-lashes|EyeLashes/EyeLashes_LongLashes`, `face|female-brows|EyeBrows/Brows_ThinBrows`. The bake keeps each chain
  under the head (`HairBack` > `Hair_Back` > `Hair_Back1` for the bob; `HairBack` > `Hair_Back` > `Hair_Back1` > `Hair_Back2` for the
  ponytail; `HairFront` > `Hair_Front` > `Hair_Front2` for the fringe).
- **Cloth (the local bridge, `--with-cloth`):** BoneCloth on each chain, the root fixed. The bob runs on the ShortHair preset
  with restrained tips (restoration .3 falling to .15 at the tips, a 30 degree limit, damping .16); the ponytail on the LongHair
  preset with a longer recovery (restoration .1, an 85 degree limit, damping .04). The colliders are the bridge's own: head,
  upper back, upper arms, collarbones and neck.
- **Headgear:** a visor leaves the hair as styled; a beanie tucks what is under its band (the ponytail's high tie takes the
  whole tail in); the robot's full helmet hides it all and its hair cloth is held (`NativeVisualCloth.EnableHair`, an optional
  bridge hook); taking any of them off restores the style.
- **Rules:** no hair simulation in first person (that body has no cloth); remote pilots hold their cloth past 30 m from the
  camera, while not drawn and on the low tier; a gap in a body's frames or a jump of over 2 m (respawn, teleport) resets it.
- **Until the ids land:** a look naming a hairstyle the bake lacks draws the default hair, and the Locker lists a hairstyle
  only once the bake carries it. `ArenaPresentation.HairPreview` (set only by the local capture tool) dresses every model body
  in a baked hair piece for filming.
