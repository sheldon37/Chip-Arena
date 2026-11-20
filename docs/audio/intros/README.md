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

1. **Rows:** add them to your own `docs/audio/intros/<author>.json` (one file per author):
   `{"author": "I3", "sounds": [ ... ]}`. Each entry uses the manifest's sound schema (see
   `docs/audio/sound-requests-install.json`): `key`, `category`, `priority` (1–3), `count` (takes, usually 2),
   `space` (`2d`), `generateSeconds`, `targetSeconds {min, max}`, `levelDb`, `fadeOutMs` and `prompt`. End each prompt
   with: isolated, no reverb, no music, no background noise.
2. **Contract:** add one line per key to `Core/NativeIntroSoundKeys.cs`, on your slot: `K("intro2.<intro>.<cue>", cap)`,
   with the cap at least `targetSeconds.max`. Without it, an imported clip is refused by the bank.
3. **Cue:** set that key as the cue's `key`, keeping its `alt` and `clip`.

## 3. Generating

This is the existing pipeline (`docs/audio/SOUND-GENERATOR.md`, "Automatic builder queue").

1. `python tools/intros/compile_sound_requests.py --job intros-v2-<date>-v1` merges every author's rows into
   `docs/audio/sound-requests-intros.json` and its bundle copy. It also checks the keys against the contract and prints
   the approved builder job. `--check` says whether the manifest is current.
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

Until a batch is imported, every cue plays its `alt` or `clip`, so the intros ship synced either way.
