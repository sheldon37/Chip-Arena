# Brickfall simulation contract — H0

2026-10-11, Astra. Task `codex/brickfall-simulation-20261011`, exact base
`ce3c5065abdb1a9d9eb3982904892fbe9d45d345`. This implements the owner's Brickfall
proposal, not the different, unmerged experimental-lab contract in PR #105.

## Agreements and necessary additions to proposal §5

- `Assets/Conversion/Resources/UnityMaps.json`, schema 1. JsonUtility objects,
  arrays and integer colours; never an export_source output. Runtime SHA-256
  of exact UTF-8 file text is `ConversionContent.contentRevision`; no authored
  revision field. Both hello and frame require it, with protocol 6. Source SHA,
  relay service and LAN discovery wire format remain unchanged.
- Old maps stay in GameContent, retaining parity receipts and defaults. New maps
  have `unity=true` after merge, explicit `rotation`, bounds, spawns and nav.
  Brickfall is selectable, excluded from Random. All 14 old maps and Ward Zero stay.
- Only `role=static` blocks move to `arena.StaticBlocks`. `arena.AllBlocks`
  exposes both for presentation and cover/navigation queries. Rubble joins the
  static collection on landing, never the prop frame or 360 prop cap.
- Stable ID families: `frontage-*`, `hotel-core-*`, `hotel-stair-*`,
  `hotel-stairhouse-*`, `hotel-pier-{nw,ne,sw,se}`, `hotel-{nw,ne,sw,se}-*`,
  `ashby-*`, `bray-*`, `bridge-{ashby,bray}-*`, `stack-{n,s}-*`,
  `market-*`, `cover-*`, `shop-*`, `yard-*`, `rubble-<segment>-*`.
  Member IDs include `ashby-pier-1..3`, `bray-pier-1..3`,
  `bridge-ashby-bearing`, `bridge-bray-bearing`, `stack-n-base`, `stack-s-base`.
  Claude should map these prefixes; geometry is simulation-owned.
- Segment definition retains the proposed fields. Adds `duration`, `pivot`,
  `axis`, `angle`, `direction`, `mass`, `material`, `sweeps[]` (center/size),
  and `poses[] {block, pivot, axis, angle, drop, delay}`. These explicit authored
  poses remove inference from block names and make host/guest/killcam identical.
  `rubble[]` is an array of full ContentBlock objects, not references.
- Replicated `segments[] {id,state,t0}` uses states 0 intact, 1 stressed,
  2 falling, 3 rubble. **t0 is the scheduled start of falling**, including while
  stressed; intact t0=0. Queued segments become stressed immediately (therefore
  cascade immediately), then wait for a slot; t0 is fixed to a finite future
  start when scheduled. At least the authored telegraph elapses before motion.
  Frame restore is reversible, including rubble -> intact for replay rewinds.
- Every moved block is evaluated from its immutable content pose and t0. Carried
  blocks are omitted from prop frames; breakable segment masonry transmits health
  but its transform is ignored while locally posed. Static blocks never disappear
  because they are absent from a frame. No interpolation toward 20 Hz poses.
- Pancake drops use a bounded-acceleration curve (<=6.9 m/s²); other modes carry
  no riders. Crush/debris share a per-pilot, per-segment lifetime damage allowance
  of min(60, .6 * maxHP), consumed **after** all damage multipliers. Self-crush is
  allowed; team crush follows friendly fire. This is a separate damage path,
  not forced damage that bypasses the normal mitigation rules.
- Structural credit: highest summed actual damage to triggering supports in the
  last 10 seconds, then newest hit, then stable pilot ID. Cascade attribution follows
  the failed supporting segment. Triggering member IDs are frozen at stress;
  damage expires after 10 seconds even while a segment queues. Without
  structural credit, use the victim's
  most recent pilot damager within 6 seconds, otherwise environmental credit.
  This separate collapse rule is retained by the 2026-10-14 stage-hazard update;
  authored hazards and the generic no-source death fallback now use 10 seconds.
- `StructureStress`, `StructureFall`, `StructureLand` retain §5.4 payloads.
  Hit/Death events add `DamageKind=collapse`; Hit.Detail is the segment ID.
  First lost member is chosen in stable support-ID order. Seed is a stable UTF-8
  FNV-1a hash of map ID, segment ID and round, not string.GetHashCode.
- Nav links gain `id`, `requires[]` (intact segment IDs), `after[]` (landed
  segment IDs). Segment `nav.remove/add` lists link IDs. Stressed/falling swept
  volumes disable intersecting routes. Static blocks and rubble remain obstacles.
- `pilotBlocks[]` are infinite upward exclusion columns with a bottom height.
  They affect pilots only (including predictions and teleports), never shots,
  and cannot become new standable ceilings for stacked jump chips.
- Tank, tenement floor collapse and Wrecker remain deferred. These are ten M1
  segments. The map reconstructs on every round reset.

## Grey-box handoff to Claude

The checked-in generator is `unity/ChipArenaConversion/tools/generate_brickfall.py`.
It consumes the draft footprint; it does not modify the proposal or exporter.
The generated map has 507 initial blocks: 363 static, 26 carried, 74 masonry,
14 structural members and 30 cover props. Initial prop frame: 118. Ten segments
swap to 106 total static rubble boxes. Nav: 2,364 nodes / 3,983 links.

Existing prefix families above are retained. The integration adds `MapLooks.json`
aliases for the generated IDs, reusing Claude's palette. Additional static IDs are `ashby-stair-landing-*` and
`bray-stair-landing-*`. Hotel panel IDs include quadrant, draft name, footprint
coordinates and storey; look matching should use the quadrant/family prefix,
not infer a floor from a final numeric suffix. Debris carries `parent` and
`segment`; rubble uses `rubble-<segment>-*`.

Simulation-driven layout adjustments from the draft:

- The hotel core has real landing doors, a filled ground stair underside and
  thin upper flights. Roof stair-house sides stop at z=4.2, leaving the northern
  1.6 m side exits clear for capsules. Two tenement switchback landings are added.
- Second-floor bridge portals are widened in the hotel and cut above y=6.96 in
  the tenement rear panels. Ground opening LOS is unchanged. Bridge tear-hinge
  poses use the authored tenement-end pivot regardless of which support fails;
  choosing a dynamic surviving end would need another replicated field.
- Hotel rubble has 0.2 m steps, a low seam apron and extra room at the southwest
  stair foot. Bridge rubble is 1.2 m high. Facade saddles are two metres wide;
  frontage routes stay open in the 1 m capsule-width raster for all 1,024 states.
- Sweep boxes conservatively include all authored corner poses, including the
  initial chimney rear edge and bridge rails. Closest spawn-to-sweep clearance
  is 3.082 m. Telegraphs must use the authored `crush` and `sweeps` data.
- `AllBlocks` is the combined collection for presentation. `Props` alone omits
  static scenery and rubble. Carried blocks remain in `Props` for look parenting
  but do not appear in prop frames. Guests and killcam pose those blocks locally.

`DamageKind` is the serialized C# field on Hit/Death; its value is `collapse`.
Hit/Death `Detail` is the segment ID. Structure event fields remain exactly the
§5.4 contract. The owner-authorized integration connects the existing visual/audio
kit, bands, warning decals, AllBlocks dressing and COLLAPSE feed label. Telegraphs
use segment membership and the union of authored crush/sweep bounds; queued
warnings preserve their full schedule and live warnings reconstruct from segment
frames after join/reconnect/replay. Damage stages read optional block `hp`/`stages`
with legacy material defaults. Member lamps share `NativeStructureRules.Armed`.
New architectural kit geometry and visual polish remain Claude's work; this does
not claim that every proposed window, sign or trim has been authored.

## Gates and publication

H0 is schema review, not a playtest. G1/G2/G3 require owner Windows acceptance.
No old-map removal, production feed, shared-branch merge or build publication is
part of this contract commit. Test-channel publication requires the compatibility
change to ship and a verified build; this branch alone is not a shipped revision.
Actual checks and unrun Windows/Unity/multiplayer limits will be recorded in the
implementation DEV_LOG entry and PR.
