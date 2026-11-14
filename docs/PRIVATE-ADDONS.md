# Private Unity add-ons

The public source has no licensed pack dependency. Owner-supplied code stays in
`Assets/LocalAssets/PrivateAddons`, an ignored tree installed from the private
LocalAssets zip. Confirm ownership of each pack before downloading it. No VLB,
UniStorm, Weather Maker or Bakery source was downloaded for this implementation.

## Packing and installation

On the licensed-asset machine, create a local JSON allowlist (do not commit pack
source) with `{"schema":1,"files":[...]}`. List every path relative to LocalAssets,
including each asset's `.meta` and all containing folder `.meta` files. Paths must
be under `PrivateAddons/` (plus `PrivateAddons.meta`). No wildcards, discovery or
recursive dependency copying is performed. Demo, sample and example paths are
rejected. Review the source before listing it; hashes prove integrity, not trust.

```
python tools/pack_visual_body.py LocalAssets.zip LocalAssets-next.zip --addons-allowlist private-allowlist.json
python tools/install_local_assets.py LocalAssets-next.zip --check
```

Run these from `unity/ChipArenaConversion`. Keep `--with-cloth` when intentionally
refreshing that separate bundle. Existing add-ons are preserved unless an add-on
allowlist is supplied; supplying one replaces the previous zip listing completely,
removing previously listed scripts omitted from the new list. A failed validation
never replaces the output zip. The installer verifies before copying any content.
Use a **fresh builder worktree** when removing/renaming code: the existing installer
copies over files and does not delete old local development assets.

Allowed types: `.cs`, `.asmdef`, `.shader`, `.cginc`, `.hlsl`, `.compute`, `.meta`,
`.asset`, `.mat`, `.prefab`, `.png`, `.bytes`. Limit: 5,000 files, 64 MiB total, and
the installer's existing per-file limit. Metas, unique GUIDs within the bundle,
case-insensitive path uniqueness, symlinks and SHA-256 are checked. DLLs and
executables are excluded. Required pack dependencies of other types need a
reviewed extension to this list; do not rename them to evade it.

## Runtime contract

`NativePrivateAddons` is in the public Unity runtime. Register on
`RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)`:

- `MapBuilt(MapContext)`: after a NativeArena constructs its colliders and pilots.
  Context retains that map's ID, ContentMap, resolved MapLook and authority root.
- `MapTearingDown(MapContext)`: before that same root's colliders are disabled and
  destroyed. It gets the original context even if the arena has selected a new map.
- `LightingApplied(ContentMap, MapLook, Camera)`: after the built-in map lighting.

Callbacks are optional and exceptions are logged without aborting other callbacks.
Subsystem registration clears handlers when entering play with domain reload off.
Without subscribers the map hooks do not allocate contexts or resolve looks.

An add-on must keep resources per context/root, clean up on teardown, and avoid
changing simulation or global lighting for invisible authority/replica scenes.
There can be several arenas simultaneously during Expedition. `MapBuilt` is not a
signal that all rendered kits have been uploaded: many visuals use instanced draws
and have no GameObject renderer under this root. Camera/global effects should use
the active lighting callback. Register once; repeated Apply calls must be idempotent.
Menu/entrance/draft presentation is outside the map lifecycle contract.

The game currently uses Unity's default assemblies. Put the small bridge that
references `NativePrivateAddons` in the default Assembly-CSharp (no asmdef above
that bridge); separately compiled vendor asmdefs cannot reference Assembly-CSharp.
Vendor code can retain its own asmdefs. Preserve licensed source GUID references
when relocating it into the private tree.

## Pack integration boundaries

VLB and either weather pack can run live through an owner-written private bridge.
The hooks do not themselves replace the current beam/weather renderers, guarantee
pack compatibility, or provide a finished weather adapter. The bridge must manage
fallback ownership and validate each graphics tier before enabling replacement.

Bakery still needs an Editor export of each JSON map's static geometry, UV unwrap,
bake, and per-map light bank. Runtime application additionally requires stable
geometry/lightmap mapping and lightmap sampling in the world shaders. Dynamic and
breakable geometry needs a defined probe/fallback strategy. These are separate
integration work, not provided by permitting Bakery scripts in the zip. See
LIGHTING-ADVANCED.md for the current runtime light-volume fallback.

## Validation

`Tests/test_private_addons.py` packs an original stand-in containing every required
code/shader extension, extracts and installs it, compares bytes, tests determinism,
rejects tampering/missing metas/bad paths/budget overflow and verifies removed code
is absent on repack. This proves packaging, not Unity import or pack execution.
Real Unity import, callback lifecycle under multi-area Expedition, shader rendering
and Windows host/guest acceptance remain required before a real licensed adapter
is released.
