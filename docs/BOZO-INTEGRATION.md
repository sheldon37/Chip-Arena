# BoZo characters + Magica Cloth 2: integration plan

Status (2026-10-22, Claude): plan only. Owner decision: player characters come from the **BoZo: Stylized Modular
Characters – Anime Pack** (Unity Asset Store, v2.6.x), with **Magica Cloth 2** for hair and cloth motion. Both are bought by
the owner. The custom Blender base (`claude/lowpoly-base-model`, last checkpoint `5c3d6ae`, not merged) is stopped; its chest
core and right-wrist device (with the closed / opening / insert / accepted / active states) are kept as attachments for BoZo
characters. `docs/CHARACTER-LOWPOLY.md` (Phase A) still holds for the rig adapter, cosmetic schema, death compatibility,
performance budget and the Astra contract; this file records what changes for an asset-pack body. Map restyle to match:
`docs/MAP-STYLE-ANIME.md` (branch `claude/anime-map-style`).

## Rules
- **Licensed, never committed.** Both packs live under `unity/ChipArenaConversion/Assets/LocalAssets/` (already git-ignored).
  After an Asset Store import, move the pack folders there before doing anything else, and check `git status` shows nothing
  from them.
- **Optional at compile time.** CI, the cloud checks and any machine without the packs must still compile and run, falling
  back to today's characters. All code that names BoZo or Magica types sits in an optional assembly (asmdef with
  `defineConstraints`), enabled by a local scripting define that an editor check sets only when the pack folders exist.
- **Presentation only.** Hit volumes, colliders, weapon frames, shot origins and the wire are unchanged; cloth and hair are
  simulated per client and never networked; first person never runs cloth.
- **Package manifest.** BoZo's shader needs Shader Graph; Magica Cloth 2 needs Burst, Collections and Mathematics. Changes to
  `Packages/manifest.json` are a build-tooling change: coordinate with Astra before committing them.

## Where the work runs
Unity-dependent steps (import, inventory, rig mapping, cloth, captures, profiling) run in a Claude Code session on the owner's
PC, which has the editor, the licence, a GPU and the packs. Docs, the repo check suite (`run5b`), Blender work and parallel
agents run in the cloud session. Both push to GitHub branches.

## Steps
1. **Import and verify** (owner's PC). Import Magica Cloth 2, then BoZo; accept their package dependencies; move both folders
   under `Assets/LocalAssets/`; open BoZo's character-creator scene once in the Built-in pipeline (pink materials mean Shader
   Graph is missing). Record versions.
2. **Inventory** (write into this file): BoZo skeleton (bone names, hierarchy, rest pose, height), body and outfit meshes per
   slot (head, eyes, tops, bottoms, footwear, gloves, hats, front and back hair, head and face accessories), blendshapes (body
   type, chest, weight, face, expressions), shader (BMAC_Toon properties, the 9 colour channels, outline), the merge-to-one-mesh
   path, the equip and save systems, triangle counts. Magica Cloth 2: version, runtime-build API, BoneCloth vs MeshCloth,
   colliders, culling and LOD.
3. **Rig adapter.** Map BoZo's humanoid bones onto the 22 `CapturedRig` joints (CHARACTER-LOWPOLY section c). Extra bones
   (spine segments, twist, fingers) are derived or held. BoZo's proportions are the visual skeleton; foot IK to the authority
   contacts and support-hand IK as designed; weapon on the visual hand (proposal P1) unless the owner picks P2. Drift table vs
   the authority volumes, measured in Unity.
4. **First character.** A male preset (body blend, a hair, a default outfit) plus the chest core and the right-wrist device,
   player colours through BoZo's colour channels, and our lighting (picked lights, actor lights, muzzle lights) reaching its
   shader (bridge or port). Neutral turnaround and gameplay poses captured in Unity.
5. **Magica Cloth.** BoneCloth on BoZo's hair bones and cloth on outfit pieces that need it (coats, hoods, scarves, straps):
   presets, body colliders including the core and device, distance and quality culling, a budget for 8 pilots, off in first
   person; killcams, corpses and intros re-simulate locally.
6. **Wider integration.** Cosmetic slots mapped to BoZo categories; Astra's look-map contract updated with BoZo ids; death and
   dismemberment on skinned BoZo meshes (regions from bone weights); legacy voxel fallback kept until parity; performance vs
   the voxel renderer (draws, skinning, cloth) on the owner's PC.

## Open owner decisions
- Default outfit for a new pilot; which BoZo pieces are DEV-only (Sheldon adaptation).
- Weapon placement P1 (weapon rides the visual hand, recommended) or P2 (hand pulled to today's weapon frame).
