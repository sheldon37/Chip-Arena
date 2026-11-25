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
(`NativeBodyKind.Keep`): the Locker's writers, the shown look and this client's own pilot's bodies (its seat look). Save
Pilot, the account and a room drop the pick, and peers draw today's pilot. Astra's needs are in DEV_LOG (2026-11-25, B1).

## 2. The first hair sample

See DEV_LOG 2026-11-25 (B1) for the inventory, the bake mapping, the cloth tuning and what is still to do.
