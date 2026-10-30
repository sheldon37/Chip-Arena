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
  - Status: implemented on branch `claude/footstep-zombie-audio` (pushed, not merged yet), with `Tests/test_import_sound_requests.py`.
    Until it merges, the owner can upload the batch folder (zipped) in the Claude conversation, and Claude runs it and returns
    the new LocalAssets zip.
  - Rules the importer applies (beyond the list above):
    - Hard errors (exit 1, nothing written): a name not `<key>_<NN>.mp3|wav`, a key not requested, NN outside 01..`count`, the
      same variant as both .mp3 and .wav, a receipt with another schema or manifest hash, missing `generatedAt` / `generator` /
      `files[]`, or any API key, token or account field; a requested key missing from the bank contract or with a cap under its
      `targetSeconds.max`; an output zip inside the repository that git does not ignore.
    - Warnings: no receipt, a partial set, files that are not sound files (ignored), a name found in several `--in` (the newest by
      modification time wins; a tie goes to the later `--in`).
    - The first sound event ends at 0.12 s (step) or 0.3 s (voice) under the trim threshold; a clip whose peak is under −40 dBFS
      is near-silent.
    - Loudness: each category's target is the median momentary maximum of the base zip's clips of that category's keys (the steps
      also the `move.step.*` keys, the voices also `pve.add.death`); −24 / −18 LUFS when the base has none. The current bank
      (`LocalAssets-footsteps-zombies.zip`) measures −24.1 (steps) and −18.0 (voices).
    - A partial set appends after the key's existing clips, up to the bank's 16 clips; the files over the limit are rejected.
      A full set replaces the key's clips (their `.meta` GUIDs are kept where the clip name is reused). A new step key gets jitter
      0.06; every other entry field is kept.
    - The report (`chip-arena.sound-import-report/1`) lists each file's status, reason, notes, length, gain, loudness and true peak,
      each key's requested, arrived, accepted and rejected counts with its mode (replace, append, none), and the install check.

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
| `pve.station.linesman.idle` | voice | 3 | 3 | 2.5 | 1.4–2 | Raspy zombie groan from a damaged human throat mixed with a crackling electrical buzz and small spark pops, as if the body is live with current, slow and droning, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.linesman.charge` | voice | 3 | 3 | 1.5 | 0.9–1.2 | Rising electric whine building over one second, a high-voltage charge-up with crackling arcs and sparks that intensifies to a peak, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.linesman.arc` | voice | 3 | 3 | 1 | 0.4–0.7 | Sharp lightning-bolt crack followed by a quick chain of electric zaps jumping from target to target, short and violent, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.linesman.ground` | voice | 3 | 2 | 1.2 | 0.6–1 | A heavy hand slapping down on a steel rail with a dull metallic clang, then a deep humming electrical surge through the metal, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.linesman.hurt` | voice | 3 | 3 | 0.8 | 0.2–0.5 | Zombie getting hit, a short choked grunt with a sharp static pop and spark, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.linesman.death` | voice | 3 | 3 | 2 | 1.1–1.7 | Electrocution death, a zombie scream buzzing with electricity, then a battery bursting with a hard pop and a fading electrical sizzle, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.conductor.inhale` | voice | 3 | 2 | 1.2 | 0.5–0.9 | A rattling dead breath drawn in sharply through a ruined throat before blowing a whistle, wet and wheezing, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.conductor.whistle` | voice | 3 | 3 | 1.5 | 0.7–1.1 | A shrill railway guard's pea whistle blown hard, distorted and held too long with a trilling rattle, harsh and piercing, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.conductor.bell` | voice | 3 | 2 | 1.5 | 0.8–1.3 | An old brass handbell rung urgently, rapid clanging strokes, slightly cracked and tinny, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.conductor.mutter` | voice | 3 | 3 | 2.5 | 1.4–2.1 | A hoarse zombie voice muttering a train timetable under its breath, slurred unintelligible words and numbers, rasping and broken, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.conductor.lantern` | voice | 3 | 2 | 1 | 0.35–0.7 | A small glass and tin railway lantern shattering, glass bursting and a thin metal frame clattering, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.conductor.death` | voice | 3 | 3 | 2 | 0.9–1.5 | A zombie's wheezing last breath and body collapse, then a tin lantern clattering on the floor, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.undertow.bubble` | voice | 3 | 3 | 2.5 | 1.5–2.1 | Slow bubbles rising and popping in still dark water, deep and ominous, steady and loopable, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.undertow.surface` | voice | 3 | 3 | 1.2 | 0.5–0.9 | Something bursting up out of water with a big splash and a wet gurgling snarl, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.undertow.grab` | voice | 3 | 3 | 1 | 0.35–0.7 | A wet slap of a hand gripping a body, then a choking gurgle, short, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.undertow.drag` | voice | 3 | 2 | 2 | 1.1–1.6 | A body dragged through shallow water, sloshing and scraping, one steady pull, loopable, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.undertow.death` | voice | 3 | 3 | 2 | 0.9–1.5 | A drowning zombie's bubbling gurgle that cuts off, then a heavy body sinking into water with a splash, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.pileup.moan` | voice | 3 | 3 | 3 | 1.8–2.6 | Several zombie voices moaning together, layered and overlapping, muffled behind a heavy steel door, low and hollow, isolated, no music, no background noise |
| `pve.station.pileup.brace` | voice | 3 | 2 | 1 | 0.45–0.8 | A heavy steel door scraping across a concrete floor, a short grinding drag, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.pileup.shove` | voice | 3 | 3 | 1 | 0.35–0.7 | A big metal slam of a steel door shoved forward, with a whoosh of a body thrown through the air, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.pileup.ricochet` | voice | 3 | 3 | 0.5 | 0.15–0.35 | A single bullet ricochet off thick steel plate, a hard metallic ping and a short whine, isolated, dry close-mic recording, no reverb, no music, no background noise |
| `pve.station.pileup.split` | voice | 3 | 3 | 2 | 1–1.6 | Wet tearing flesh as a heap of bodies rips apart, then a steel door clanging down onto concrete, isolated, dry close-mic recording, no reverb, no music, no background noise |

Totals: 39 keys, 148 files (the zombies and steps: 17 keys, 88 files; the Central Station: 22 keys, 60 files).

The 22 Central Station enemy cues (`pve.station.*`, `docs/EXPEDITION-CENTRAL-STATION.md` section 10; each cap in
`Core/NativeStationKinds.cs` `CueCaps` and the bank contract) are voices, so the importer matches them to the zombie voices'
loudness. They are priority 3 (optional to generate). A batch designed from the owner's licensed packs covers all 60 files
(`station-sounds-batch.zip`, kept outside the repository; sources in `docs/ASSET-CREDITS.md`, Central Station enemy cues).
The prompts stay, so ElevenLabs takes can replace or join those later (`--include-player-steps --key <key>`,
`docs/audio/SOUND-GENERATOR.md`).
