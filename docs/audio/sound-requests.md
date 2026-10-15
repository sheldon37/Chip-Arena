# Chip Arena — sounds to generate (ElevenLabs Sound Effects)

## How to export

- **Name each file `<key>_NN`.** For example `pve.zombie.moan_01.mp3` or `pve.zombie.moan_02.mp3`. MP3 or WAV both work.
- **Make the full count per key, from separate generations.** Each one should be a distinct take. The game picks among them without repeats.
- **Keep every take dry.** Add this to every prompt: "isolated, dry close-mic recording, no reverb, no music, no background noise". The game adds distance and room itself.
- **Trim the start.** The sound should begin at once. A short tail is fine.
- **Duration.** Set ElevenLabs' duration to the value given, or leave it on auto for short sounds.
- **Prompt influence.** Around 0.6–0.7 keeps takes on-brief but still varied.
- **Getting them to me.** Upload the files here in one batch or in several. I convert, loudness-match and add them to the game's sound bank.

## Zombie voices (the most needed)

Zombies here are infected people in torn clothes, not monsters. Their voices are human throats gone wrong: raspy, wet, broken.

| Key | Count | Length | Prompt |
| --- | ---: | --- | --- |
| `pve.zombie.moan` | 6 | 2–3 s | Low, slow zombie moan from a damaged human throat, raspy and wet, long exhale that cracks and trails off, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.groan` | 6 | 1 s | Short guttural zombie groan, a single grunted breath with a wet rattle, human but wrong, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.shriek` | 4 | 1–1.5 s | Sudden feral zombie shriek when it spots its prey, a raw tearing human scream that rises sharply then breaks, aggressive, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.attack` | 6 | 0.5 s | Vicious zombie snarl as it lunges and swipes, short explosive growl with teeth and spit, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.hurt` | 6 | 0.4 s | Zombie getting hit, a short choked grunt of pain, wet and raspy, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.death` | 5 | 1–1.5 s | Zombie dying, a final gurgling death rattle that collapses into a wet exhale, isolated, dry close-mic recording, no reverb, no music |

## Special zombie types

| Key | Count | Length | Prompt |
| --- | ---: | --- | --- |
| `pve.zombie.runner.snarl` | 4 | 0.8 s | Fast aggressive zombie panting snarl, quick ragged breaths and a hissing growl, frantic, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.spitter.gurgle` | 3 | 0.8 s | Zombie throat filling with thick acid fluid, a bubbling wet gurgle building up before it spits, disgusting, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.spitter.spit` | 3 | 0.5 s | Zombie hocking and spitting a big glob of acid, a forceful wet hork and splatter release, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.brute.grunt` | 4 | 0.8 s | Huge hulking zombie brute grunt, very deep chesty bellow, heavy and threatening, isolated, dry close-mic recording, no reverb, no music |

## Zombie footsteps

| Key | Count | Length | Prompt |
| --- | ---: | --- | --- |
| `pve.zombie.step.shuffle` | 8 | 0.4 s | Single shuffling footstep of a limping zombie on concrete, one torn shoe scuffing and dragging along the ground, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.step.run` | 6 | 0.2 s | Single fast running footstep on concrete, worn shoe slapping the ground hard, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.step.heavy` | 5 | 0.5 s | Single heavy stomping footstep of a huge creature on concrete, deep thud with a little debris rattle, isolated, dry close-mic recording, no reverb, no music |
| `pve.zombie.step.crawl` | 4 | 0.5 s | A body dragging itself across concrete, hands slapping and clothing scraping, one pull, isolated, dry close-mic recording, no reverb, no music |

## Player footsteps (optional: the game already has some)

Pilots are armored cyber soldiers in hard boots. Make these only if you want them to sound more armored than the current stock steps.

| Key | Count | Length | Prompt |
| --- | ---: | --- | --- |
| `move.step.concrete` | 6 | 0.25 s | Single footstep of an armored soldier boot on concrete, firm heel strike with a faint metallic armor clink, isolated, dry close-mic recording, no reverb, no music |
| `move.step.metal` | 6 | 0.25 s | Single footstep of an armored soldier boot on a metal floor grate, hard metallic clank, isolated, dry close-mic recording, no reverb, no music |
| `move.step.gravel` | 6 | 0.3 s | Single footstep of an armored soldier boot on loose gravel and rubble, crunchy, isolated, dry close-mic recording, no reverb, no music |
