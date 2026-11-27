# Pilot body and hair (Astra's brief 2026-11-24, sections 1 and 2)

Owner, 2026-11-25: "lets get work started" on Astra's brief. Claude B1, branch `claude/pilot-body-hair`. Presentation only:
both bodies and every hairstyle keep the same hit volumes, reach, shot origins, movement and timing; no rule reads them.

## 1. The body choice

- **Key:** the look map's optional `body:female` (`Core/NativeBodyKind.cs`). Omitted (or `body:male`, never written) is
  today's pilot, so every existing look and account draws as before. Written after the five slots and before the costume and
  the skin tone: `upper:tee,body:female,skin:c48a62`.
- **Today's pilot**, measured from the bake: the pack's base body with every shape at 0 and no bone modifiers (the bake never
  applied Default_Boy's BodyType 45 or its modifiers). That stays the Male body exactly.
- **The female body** (owner 2026-11-25: "I think the body needs to look more feminine for the female body"). The first
  preset, the pack's `Default_Girl` (Chest 60, Curvy 100, EyeRoundness 57, Chest Size .971), barely moved the silhouette:
  today's pilot is the pack's neutral middle, not its male (the male presets push BodyType +26..+67, NeckThickness +19..+100,
  Sharpness +65..+85, the clavicles 1.07, the root 1.03), and the project clamps blend shape weights to 0..100
  (`legacyClampBlendShapeWeights`, Unity's default, also in builds), so a negative BodyType does nothing. It now comes in three
  levels (`NativeBodyKind.Levels`; the default is Strong, the owner's pick on 2026-11-25):

  | Level | BodyType | Weight | Chest | Curvy | WaistSize | ButtSize | NeckThickness | EyeRoundness | Roundness | Chest Size | shoulder span | upper arm (y, z) | hands | height (root) | chin (x, z) |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
  | Subtle | -10 | -10 | 60 | 100 | 25 | 20 | -15 | 57 | 15 | .971 | .96 | .96, .88 | .96 | .98 | .95 |
  | Medium | -20 | -20 | 70 | 115 | 45 | 35 | -30 | 62 | 30 | .971 | .93 | .92, .80 | .93 | .96 | .90 |
  | Strong | -32 | -28 | 85 | 130 | 65 | 55 | -45 | 68 | 45 | .96 | .90 | .90, .73 | .90 | .95 | .86 |

  - The shapes are set by name on every worn mesh that has them (the clothes follow the body). The part of a weight outside
    0..100 is baked into a copy of that mesh, once per mesh and level (`FemaleMesh`; the copy keeps every shape, so the face's
    expressions still play); the renderer draws the rest.
  - Measured on the pack's body at +100 (metres, rest shoulders .427, waist .249, hips .317): BodyType widens the shoulders
    +.025 and narrows the hips .019 (so it goes negative); Curvy takes the waist in .058 and the hips out .031; WaistSize
    narrows the waist .026; ButtSize deepens the hips. On the head, Roundness widens the jaw (softer), NeckThickness below 0
    thins the neck; Sharpness stays 0 (every pack female has 0, its males 65-85; below 0 it barely moves the face).
  - The bones: Chest Size (`spine_04`, the head counter-scaled to its own size), the shoulders' span (each upper arm in along
    its collarbone: the arms keep their length), the upper arm's girth (its shoulder twist leaf, as the pack's Hana), the hands,
    the whole body (root, never under .95) and the chin's width.
  - Face: the pack's long lashes and thin brows, worn in place of the head's own when the bake carries them (look data slot
    `face`, ids `female-lashes`, `female-brows`; never a wardrobe slot).
- **Stance:** the pack's female idle (`BMAC_F_Idle`) against its male one stands with the thighs closer (Upper Leg In-Out
  -.14..-.17), the knees turned in (Twist In-Out -.36..-.52), the upper chest lifted (+.1) and the hips rolled toward the
  weight leg. That difference is laid over the Locker stage, lobby and portrait poses as bone rotations
  (`VisualFemaleLobbyStance`; legs 6, knees 10, chest 4, hip roll 3 degrees); no clip or motion bank changes. The match idles
  stay the pack's (owner); a lighter match stance (legs 4, knees 6, chest 7, standing still only) is the owner's option,
  `ArenaPresentation.FemaleMatchStance`, off.
- **Fitted over the cloth:** the chest core rides out with the surface under it, and the robot armour's plates are carried
  with the garment under each one, both by those meshes' own shape deltas (`Runtime/NativeBodyKindPresentation.cs`). The
  wrist device rides the forearm (scaled with it); hats, helmet, visor and hair ride the head.
- **Where it shows:** every body made for the pilot (live, draft, first person, corpses, revive bodies, death scenes,
  portraits, the Locker stage) dresses through `VisualDress`, which applies the level; a level change makes the body again. Under the Hot Dog or the voxel costume
  the body is not reshaped.
- **The Locker:** Appearance opens with BODY (Male / Female cards with figures). Hovering a card tries the body on the stage;
  a pick writes the key and turns the stage once round on its turntable (none with Reduced effects or once dragged). Revert
  and the unsaved-changes baseline cover it. Picking Female while the hair is the default Spiky wears Fringe tail
  (bare-headed it reads as hers; Tied back much less); any other hairstyle is kept.

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

## 3. The hair bank (owner 2026-11-26: "a lot more hairstyles, some anime ones as well ... Add magica cloth into these")

Claude, branch `claude/hair-styles`. Presentation only, like sections 1 and 2: every hairstyle keeps the same hit volumes,
reach, shot origins, movement, timing and network messages; no rule reads it.

- **Why a bank:** the pack's hair pieces carry one short bone chain for the whole mass (or none: MessyHair, PineappleCut, ShotaCut,
  TiedBun, TwinBuns, ShinryuCut), so cloth on them swings the back of the hair as one block. The bank rebuilds each piece with
  many chains, one per lock or per sector of a curtain, for motion that reads as hair.
- **Styles** (`Core/NativeLook.cs` hair ids after the service's seven, every one on either body): two generated anime styles,
  `raven-spikes` (broad spikes leaving the scalp along it and flaring up and back in layers, short ones over the ears and nape,
  two long bangs framing the face to the jaw, three over the brow) and `hero-spikes` (spikes standing up and out all round the
  crown, three broad bangs over the brow), both on the pack's ShotaCut cap; and fifteen from the pack's pieces: `wolf`, `mane`,
  `messy`, `emo`, `shag`, `dreads`, `short-pony`, `twin-tails`, `pigtails`, `twin-buns`, `bun`, `hime`, `long-straight`, `shoujo`,
  `lob` (pieces: `tools/gear/hair.py` STYLES). The generated styles are original designs in the requested spirit, not a copy of
  any character.
- **The bank** (`Resources/ChipArenaHair.bytes`, local: made from the licensed pack's meshes; shipped in the licensed-assets zip
  like the bake): textures (each piece's used 512 px block of its pack texture at 256 px, or the generated locks' colour-weight
  gradient), pieces (renderer name, mesh in the head bone's frame, chain bones, four weights a vertex, tag shapes) and styles.
  `Core/NativeHairBank.cs` reads it and refuses a malformed one whole; `tools/gear/hair.py` writes it from the local hair lab's
  dump of the pack pieces (Unity's import, so the game's frame and vertex order).
- **Chains:** a vertex's looseness is how far it stands off the head (2 to 5 cm) or the pack's own chain weights, whichever is
  more; its flow is the distance over the hair's own surface from where it leaves the scalp. A chain sits in each 20 degree slot
  round the head that holds enough loose hair, its joints every ~3.5 cm of flow in the middle of the hair's thickness; slots
  next to each other are one mass, their chains ordered round the head. Weights: the fixed part on the head (never on a chain's
  root: the cloth turns a root toward its next joint, which swung the crown off the scalp in the first test), the loose part
  between the two nearest chains and the two joints either side of its flow. Generated locks are each their own chain.
- **Cloth (the local bridge):** a bank chain root is named `Hair_B<kind><group>_...`; the bridge makes one BoneCloth per piece
  and group, tuned by kind: `S` spikes hold their shape (gravity 1.2, restoration .65, a 22 degree limit), `L` locks and bangs
  sway and settle, `F` a free fall, `C` a curtain whose chains are one sheet (sequential non-loop mesh), `T` a tied tail with the
  ponytail's long recovery. Buns and the short cap ride the head whole.
- **On the body:** the bank's styles join the bake's look data (so the Locker lists them, `VisualDress` turns them on by name and
  their tag shapes tuck under headgear); a body dressed in one gets its pieces built on the head before dressing
  (`Runtime/NativeHairBankPresentation.cs`), and the cloth bridge attaches after dressing as for the bake's hair. Meshes,
  textures and materials are made once and shared; the material is the bake's hair material (the pilot's hair colour) on the
  piece's own texture.
- **Account:** the service lists the first seven hair ids (schema 4). Until it lists the bank's (Astra: `src/relay/accounts.ts`
  `LOOK_SLOTS`, then `NativeLook.ServiceKeepsHairStyles`), a login keeps this PC's pick while the account's look has the default
  hair. Peers on this client draw the style; older clients draw the default hair.
