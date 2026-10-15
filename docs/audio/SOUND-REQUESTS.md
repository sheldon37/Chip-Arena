# Sound requests: generator and importer contract

Owner request, 2026-10-15: footsteps and zombie sounds, generated with ElevenLabs. Astra builds the Windows batch
generator. Claude owns this manifest and the importer.

## Contract

- **Manifest:** `docs/audio/sound-requests.json`, schema `chip-arena.sound-requests/1`. The generator reads only this file.
  Keys, counts and durations change only through a new commit to it. A breaking change bumps the schema string.
- **Fields:**
  - `file.pattern`: `<key>_<NN>.<ext>`, with NN = 01..`count` (two digits) and ext `mp3` (preferred) or `wav`.
  - `generation`: service ElevenLabs Sound Effects; `promptInfluence` 0.65; `outputFormat` `mp3_44100_192`; one API generation
    per file; `promptIsComplete` (send `prompt` verbatim, nothing appended).
  - `sounds[]`:
    - `key`: the bank event key;
    - `category`: `voice` or `step`, which picks the importer's loudness;
    - `priority`: 1 is most needed, 3 is optional;
    - `count`: variants to generate;
    - `generateSeconds`: the API `duration_seconds`, 0.5–22;
    - `targetSeconds {min,max}`: the clip length after the importer edits it;
    - `prompt`.
  - `edit`: what the importer does (below).
- **Output folder (generator):** one folder per batch, for example
  `%USERPROFILE%\Documents\ChipArena\SoundRequests\<batch>\`, where `<batch>` is the UTC time `yyyyMMdd-HHmmss`. It holds:
  - the files, flat, named by the pattern;
  - `receipt.json`:
    `{"schema":"chip-arena.sound-receipt/1","manifestSha256":"<sha256 of the manifest bytes used>","generatedAt":"<UTC ISO>","generator":"<name>/<version>","files":[{"file","key","variant","prompt","durationSeconds","promptInfluence","outputFormat"}]}`.

  No API key, account id or token is ever written to the folder, the receipt, logs or the repo. The key comes from an environment
  variable on the owner's machine only.

  Rules for the generator:
  - a partial batch is fine;
  - a re-run may regenerate selected keys or variants into a new batch folder;
  - the importer takes the newest file for each name across the folders it is given.
- **Importer (Claude):**
  - Where it runs: `unity/ChipArenaConversion/tools/import_sound_requests.py`, from the repo root, with Python 3.11+ and ffmpeg on PATH.
  - Dry run:
    `py -3 unity/ChipArenaConversion/tools/import_sound_requests.py --manifest docs/audio/sound-requests.json --in "<batch folder or .zip>" --check`.
    It validates names, counts and the receipt hash and reports missing or extra files. It writes nothing.
  - Import:
    `py -3 unity/ChipArenaConversion/tools/import_sound_requests.py --manifest docs/audio/sound-requests.json --in "<batch folder or .zip>" --base "<current LocalAssets zip>" --out "<new LocalAssets zip>" --report import-report.json`.
  - What import does:
    - decodes each file;
    - converts to mono 44.1 kHz;
    - trims leading and trailing silence below −50 dBFS;
    - keeps the first sound event;
    - cuts to `targetSeconds.max` with a 20 ms fade;
    - rejects a clip shorter than `targetSeconds.min` or near-silent;
    - matches loudness to the bank by `category`;
    - encodes Vorbis to `Resources/EffectsAudio/<key>/<n>.ogg` with its `.meta`;
    - adds the clips to that key's `clips` in `Resources/EffectsAudio/manifest.json`, replacing the key's previous clips when a
      full set arrives;
    - writes the new zip;
    - runs `tools/install_local_assets.py --check` on it.

    The report lists every accepted, trimmed and rejected file with its reason. Clips go only into the private LocalAssets zip:
    never into the repo.
  - Status: the importer lands with branch `claude/footstep-zombie-audio` (in progress). Until it merges, the owner can upload the
    batch folder (zipped) in the Claude conversation, and Claude runs it and returns the new LocalAssets zip.

## Requested sounds

`generateSeconds` is the API request. `targetSeconds` is the edited result and stays within the bank key's length cap
(`Core/NativeSoundBank.cs`).

| Key | Category | Priority | Count | Generate (s) | Target (s) | Prompt |
| --- | --- | ---: | ---: | ---: | --- | --- |
| `pve.zombie.moan` | voice | 1 | 6 | 3 | 1.4–2.4 | Low, slow zombie moan from a damaged human throat, raspy and wet, long exhale that cracks and trails off, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.groan` | voice | 1 | 6 | 1.5 | 0.5–1.1 | Short guttural zombie groan, a single grunted breath with a wet rattle, human but wrong, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.shriek` | voice | 1 | 4 | 2 | 0.8–1.5 | Sudden feral zombie shriek when it spots its prey, a raw tearing human scream that rises sharply then breaks, aggressive, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.attack` | voice | 1 | 6 | 1 | 0.3–0.7 | Vicious zombie snarl as it lunges and swipes, short explosive growl with teeth and spit, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.hurt` | voice | 1 | 6 | 0.8 | 0.2–0.6 | Zombie getting hit, a short choked grunt of pain, wet and raspy, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.death` | voice | 1 | 5 | 2 | 0.8–1.6 | Zombie dying, a final gurgling death rattle that collapses into a wet exhale, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.runner.snarl` | voice | 2 | 4 | 1.5 | 0.5–1 | Fast aggressive zombie panting snarl, quick ragged breaths and a hissing growl, frantic, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.spitter.gurgle` | voice | 2 | 3 | 1.5 | 0.6–1 | Zombie throat filling with thick acid fluid, a bubbling wet gurgle building up before it spits, disgusting, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.spitter.spit` | voice | 2 | 3 | 1 | 0.3–0.6 | Zombie hocking and spitting a big glob of acid, a forceful wet hork and splatter release, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.brute.grunt` | voice | 2 | 4 | 1.5 | 0.5–1 | Huge hulking zombie brute grunt, very deep chesty bellow, heavy and threatening, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.step.shuffle` | step | 2 | 8 | 1 | 0.2–0.45 | Single shuffling footstep of a limping zombie on concrete, one torn shoe scuffing and dragging along the ground, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.step.run` | step | 2 | 6 | 0.6 | 0.12–0.3 | Single fast running footstep on concrete, worn shoe slapping the ground hard, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.step.heavy` | step | 2 | 5 | 1 | 0.3–0.6 | Single heavy stomping footstep of a huge creature on concrete, deep thud with a little debris rattle, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.zombie.step.crawl` | step | 2 | 4 | 1 | 0.3–0.6 | A body dragging itself across concrete, hands slapping and clothing scraping, one pull, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `move.step.concrete` | step | 3 | 6 | 0.6 | 0.12–0.33 | Single footstep of an armored soldier boot on concrete, firm heel strike with a faint metallic armor clink, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `move.step.metal` | step | 3 | 6 | 0.6 | 0.12–0.33 | Single footstep of an armored soldier boot on a metal floor grate, hard metallic clank, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `move.step.gravel` | step | 3 | 6 | 0.6 | 0.12–0.33 | Single footstep of an armored soldier boot on loose gravel and rubble, crunchy, isolated, dry close-mic recording, no reverb, no music, no background noise |

Totals: 17 keys, 88 files.
