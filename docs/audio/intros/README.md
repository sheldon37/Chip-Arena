# Intros v2: requesting sounds

The owner approved ElevenLabs generation for the new intros (2026-11-20: "Attempt to generate them if possible, if not,
use existing sounds and ensure things sync properly"). Every cue must sound right before anything is generated, so each
one names fallbacks.

## 1. Every cue has a fallback (do this first)

`NativeIntroCue(at, key, alt, clip, gain, rate)` (Core `NativeIntroV2.cs`) plays the first of these that exists:

1. **`key`:** the generated sound requested for this cue: `intro2.<intro>.<cue>`, lowercase letters, digits and dots.
2. **`alt`:** an existing effects-bank key that fits. The contract is `Core/NativeSoundBank.cs`, `NativeSoundKeys.All`:
   for example `melee.sword.swing`, `impact.energy`, `blast.quake`, `structure.thud`, `gun.rifle.shot`,
   `status.shock-zap`. These play only where the LocalAssets effects bank is installed (the builder, the Editor).
3. **`clip`:** a built-in clip that every build has (`Resources/Audio/audio/<clip>`): `sword`, `metal`, `lightning`,
   `stone`, `dash`, `explosion`, `ui`, `install`, `win`, `shield`, `countdown` and the rest in that folder.

The cues follow your intro's speed ramps, and each is heard exactly once (the managed checks verify both). Check the
sync in a capture with sound.

## 2. Requesting a generated sound

1. **Rows:** add them to your own rows file, one per author, in your batch's folder (section 2a):
   `docs/audio/intros-b2/<author>.json` for batch 2, as `{"author": "I3", "sounds": [ ... ]}`. Each entry uses the manifest's sound schema (see
   `docs/audio/sound-requests-install.json`): `key`, `category`, `priority` (1–3), `count` (takes, usually 2),
   `space` (`2d`), `generateSeconds`, `targetSeconds {min, max}`, `levelDb`, `fadeOutMs` and `prompt`. End each prompt
   with: isolated, no reverb, no music, no background noise.
2. **Contract:** add one line per key to `Core/NativeIntroSoundKeys.cs`, on your slot: `K("intro2.<intro>.<cue>", cap)`,
   with the cap at least `targetSeconds.max`. Without it, an imported clip is refused by the bank.
3. **Cue:** set that key as the cue's `key`, keeping its `alt` and `clip`.

## 2a. Batches: one manifest each (I0e, 2026-11-20)

`tools/sound_queue.py` (`plan()`) re-checks every approved job, finished ones too, against its manifest's SHA-256 on every
builder run. So a manifest that an approved job pins is **sealed**: changed bytes there stop the builder at the sound queue.
Each batch therefore has its own rows folder and its own manifest:

| Batch | Rows | Manifest (and its byte-identical bundle copy) | State |
| --- | --- | --- | --- |
| `b1` (the default) | `docs/audio/intros/*.json` | `docs/audio/sound-requests-intros.json` | shipped, sealed by its approved jobs `intros-v2-2026-11-20-*` (sha256 `68c49c12`) |
| `b2` | `docs/audio/intros-b2/*.json` (I3, I4, I5) | `docs/audio/sound-requests-intros-b2.json` | batch 2, the 15 weapon intros |

- **Never add rows to `docs/audio/intros/`.** The compiler refuses to write a sealed manifest with other bytes and says so; it also
  refuses a key that another batch already requests (every batch feeds the one effects bank).
- `Tests/test_intro_sound_manifests.py` pins batch 1's bytes and bundle copy, checks every approved job against its manifest (the
  bundle copies too), checks batch 2's manifest when it exists (bundle copy, keys in the contract, none shared with batch 1), and
  runs a second intro manifest through the queue's `plan()` and the importer's contract check.
- The keys of every batch go in the one contract file, `Core/NativeIntroSoundKeys.cs`, on your slot. The installer
  (`install_local_assets.py` `contract()`) and the importer read every `intro2.*` line there, whatever its batch.

## 3. Generating

This is the existing pipeline (`docs/audio/SOUND-GENERATOR.md`, "Automatic builder queue").

1. `python tools/intros/compile_sound_requests.py --batch b2 --job intros-v2b2-<date>-v1` merges the batch's rows into its
   manifest (`docs/audio/sound-requests-intros-b2.json`) and its bundle copy. It also checks the keys against the contract and
   prints the approved builder job, whose `manifest` is that batch's. `--batch b2 --check` says whether the manifest and its
   bundle copy are current (and, once a job pins it, whether it is as pinned). Without `--batch` it is batch 1, which only
   rebuilds the bytes its jobs pin. On this PC run Python with `PYTHONUTF8=1` (its code page is cp932).
2. That job goes into `docs/audio/sound-queue.json` and its byte-identical bundle copy
   (`unity/ChipArenaConversion/tools/sound_bundle/docs/audio/`).
3. On the next builder run, the builder PC's asset install (`install_local_assets.py`, `tools/sound_queue.py`)
   generates the takes with the DPAPI-stored key (never in the repo or in logs), imports them into the LocalAssets
   zip, and writes `%LOCALAPPDATA%\ChipArena\SoundQueue\Latest-Sound-Status.json`.

**Status (I0, 2026-11-20 ~04:00 EST):**
- **The pipeline is live on this PC.** Its queue folder holds the setup and earlier receipts, and the status file read
  `complete` at 06:25 UTC on 2026-11-20: the boss and NOCTIEL jobs, nothing pending.
- **Generation runs only inside a builder run of the builder branch**, which only the integrator pushes. So the intros'
  sounds can be generated unattended tonight if the integrator merges the compiled manifest, its contract keys and its
  queue job before a builder push.
- **No author rows existed** when the framework shipped, so no job is queued.
- **Batch 1 shipped** (I1's and I2's rows, Slipstream's keys included; its approved jobs `intros-v2-2026-11-20-*`). Batch 2's
  rows go in `docs/audio/intros-b2/` (I0e, 2026-11-20); the integrator writes its job at release.

Until a batch is imported, every cue plays its `alt` or `clip`, so the intros ship synced either way.
