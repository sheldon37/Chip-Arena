# Zone boss music: Station Chime of Doom and Hydraulic Hell

Owner request (2026-10-17): "I have the two boss stage themes here, Please sync their intro with the stage. And sync the
stages to be affected by the music :)" and "Station chime for station, and hydraulic hell for foundry".

Status: on branch `claude/zone-boss-music-sync`; presentation only. No simulation, timing, hitbox, damage, movement or
network change.

## What plays when

| Map | Boss track (owner MP3, bytes unchanged) | Beat map |
|---|---|---|
| `expedition-transit` (Central Station) | `Resources/Audio/music/central-station-boss.mp3`, *Station Chime of Doom* | `Resources/ZoneBossMusic.json` |
| `expedition-foundry`, draft `expedition-ironworks` (City Ironworks) | `Resources/Audio/music/city-ironworks-boss.mp3`, *Hydraulic Hell* | same file |

- **Before the boss:** Astra's normal-play zone music (PR #263) is unchanged.
- **When the boss arrives:** the simulation starts its boss intro hold (`NativePve.cs` `BeginPveHold("intro")`, **9.0678 s**).
  - The boss clock is `NativeArena.BossSongTime`. It is shared, so every peer computes the same position.
  - The zone track plays its **intro edit**: a short silent preroll, then the song's opening from the top. The opening is cut
    on a bar line and resumes on a later bar line, so the song's **drop lands exactly when the hold ends** and the fight
    begins.
  - After the drop, a **bar-aligned loop** plays for as long as the fight lasts. It starts at the drop and ends before the
    outro. The intro plays once.
- **Missing file:** if a zone file is missing, the Siren's song plays as before, and that stage does not react.
- **Missing map:** if the map is missing, the zone track loops plainly.

Why an edit and not a plain offset: Station's drop is 16.1 s into the file, longer than the 9.07 s hold. A plain offset
would start the song at 7.0 s, mid-groove, and the opening PA chime would never be heard. The edit keeps the chime and
splices on downbeats. Changing the hold is a simulation change, so the edit absorbs the difference.

## Station Chime of Doom (Central Station)

- **Tempo:** about 179 BPM. The tempo drifts from 178.0 to 182.5 (5 to 95 %), so the beats are tracked, not computed from
  one BPM.
- **Beats:** 261 beats; a bar is 4 beats, about 1.34 s.
- **Bars:** 65. The first downbeat is beats[3], 1.282 s.
- **Drop:** bar 12, clip 16.096 s.
- **Intro edit:**
  - 1.056 s of silent preroll.
  - Clip 0 to 5.318 s plays (bars 0 to 3).
  - The song resumes at clip 13.402 s (bar 10).
  - The drop sounds at boss time 9.068.
- **Loop:** bars 12 to 61, clip 16.096 to 82.943 s (66.85 s).
  - It wraps from the last-call blast back to the drop.
  - The loop seam measured a 3.5 dB mel jump, less than the drop's own 13.7 dB entry.

| Section | Bars | Clip (s) | First heard at boss time (s) |
|---|---|---|---|
| chime | 1 (with the pickup) | 0 to 2.629 | 1.056 |
| arrival | 2-3 | 2.629-5.318 | 3.684 |
| platform | 4-10 | 5.318-14.749 | 6.374 (resumed at bar 10) |
| doors | 11 | 14.749-16.096 | 7.720 |
| express | 12-23 | 16.096-32.249 | 9.068 (the drop, the fight starts) |
| signal | 24-35 | 32.249-48.367 | 25.220 |
| rush | 36-53 | 48.367-72.382 | 41.339 |
| stopstart | 54-57 | 72.382-77.671 | 65.353 |
| lastcall | 58-61 | 77.671-82.943 | 70.643 (loop wraps after it) |
| terminus | 62-65 | 82.943-87.200 | never (outro) |

**Intro accents (boss time, s):**
- **Chimes:** 1.327, 1.664, 2.001, 2.338, 2.675, 3.011, 3.348, 3.684. This is the PA chime, one per beat.
- **Slam:** 3.011.
- **Bass cut:** 4.862.
- **Slam and bass back:** 6.206.
- **Splice:** 6.374.
- **Second chime statement:** 6.879, 7.384, 7.720, 8.057, 8.394, 8.731, 9.068.
- **Bass cut:** 7.720.
- **Slams:** 7.384, 8.731.
- **Drop:** 9.068.

**In the loop:**
- The stop-start bass cut at clip 76.104 and its return at 78.001. In the first pass these are boss times 69.076 and 70.972.
- 15 slams.

## Hydraulic Hell (City Ironworks)

- **Tempo:** about 140 BPM. The tempo drifts from 138.1 to 143.7.
- **Beats:** 335 beats; a bar is about 1.72 s.
- **Bars:** 84. The first downbeat is beats[0], 0.107 s.
- **Drop:** bar 9, clip 14.007 s.
- **Intro edit:**
  - 0.271 s of preroll.
  - Clip 0 to 7.064 s plays. This is the whole ignition build, ending on the first full entry.
  - The song resumes at clip 12.274 s (bar 8, the bar before the drop).
  - The drop sounds at boss time 9.068.
- **Loop:** bars 9 to 82, clip 14.007 to 140.277 s (126.27 s).
  - It wraps from the closing slams back to the drop.
  - The seam's 7.7 dB mel jump is no bigger than the drop's own 8.3 dB entry.

| Section | Bars | Clip (s) | First heard at boss time (s) |
|---|---|---|---|
| ignition | 1-4 | 0.107-7.064 | 0.378 |
| stoke | 5-8 | 7.064-14.007 | 7.334 (resumed at bar 8) |
| forge | 9-34 | 14.007-58.954 | 9.068 (the drop) |
| quench | 35-42 | 58.954-72.663 | 54.014 |
| press | 43-66 | 72.663-113.484 | 67.723 |
| venting | 67-68 | 113.484-116.850 | 108.544 |
| meltdown | 69-76 | 116.850-130.219 | 111.910 |
| blowdown | 77-78 | 130.219-133.579 | 125.280 |
| slam | 79-82 | 133.579-140.277 | 128.640 (loop wraps after it) |
| cooling | 83-84 | 140.277-142.800 | never (outro) |

**Intro accents (boss time, s):**
- **Downbeats:** 0.378, 2.128, 3.865, 5.603, 7.334, then the drop at 9.068.
- **Slams:** 3.423 and 5.271.
- **Drop:** 9.068.

**In the loop:** 45 slams. There are no bass cuts; the breaks are high-cut sections.

## How the stage reacts (presentation only)

Each frame, `NativeZoneBossSong.Evaluate(BossSongTime, flash mode)` gives a `NativeZoneBossBeat`. It is a pure,
allocation-free function of the shared boss clock. The flash mode follows the hazard kit's setting (Settings · Alarm lights
or Reduced effects):

| Mode | Big-area flashes | Changes |
|---|---|---|
| Full | at most 3 per second | crisp pulses |
| Reduced | at most 2 per second | softer, lower contrast |
| Calm | none | slow changes only |

- **Moods:** sections ease their mood over a bar (two in Calm) from what was heard before them. This holds across the
  splice and the loop wrap.
- **House light:** the house's ambient and key light (`House()`, through `NativeLighting.ZoneHouse`) follow only the section
  level, the cuts and the drop's flare. They never pulse on the beat.
- **Beat pulses:** the beat pulses go to the practical lights, glows and effects.

### Central Station

**Intro:**
- The power sags in the preroll.
- Each PA chime steps on the next group of platform lamps, west to east.
- The board's first row reads LAST CALL and lights on the chimes.
- The red signal lamps come on at the first slam.
- The house cuts where the bass cuts. The cut covers the lamps, the look's ambient and the moon.
- The slam brings the house only half back, so the boss approaches in the dark.
- The chime returns and the house cuts once more.
- The drop flares everything back as the fight begins. The authority's boss flag turns the board to ALL SERVICES CANCELLED
  at the same moment.

**Fight:**
- The house lights, lamps, troffers and signs follow the section's level and cast. The signal section is cold and blue; the
  rush and last call are hot and red.
- The lights pump on the downbeat and beat 3.
- The board brightens on the pulse and pages its ticker every two bars.
- The red signal lamps kick on the downbeats, only while the signal shows red.
- The platforms' red edge strips chase a quarter per beat.
- The stalled train's lit interior breathes with the bar.
- The stop-start cuts dim the house.

### City Ironworks

**Intro:**
- The furnaces stoke up bar by bar through the ignition. They flare on the downbeats and slams.
- The coke standpipes and furnace hoods puff steam on the downbeats and slams.
- At the drop, sparks rain from the roof over the casting floor while the furnaces and lights surge.

**Fight:**
- The furnaces and the steady light field breathe with the bar and flare on every beat (strongest on the downbeat).
- The work lights follow the section.
- Spark spits and steam puffs follow the section.
- The parked ladle crane rocks on its hook with the bar.

**Never touched by the music:**
- Station: the danger lamps and beacons, the live rail, the flood, the cabinets, the express and its headlight, the green
  aspect, APPROACHING, the levers and the track diagram.
- Ironworks: the hazard machines' motion, the beacons and warning lamps, the tapholes, runners and pour, the slab and slag,
  and the telegraph decals.

Every moving Ironworks machine is a hazard, so none of them pumps to the music. Warnings stay on Astra's schedule, so they
still match the damage windows. Nothing changes before the boss arrives.

## API for the boss designs (`claude/boss-central-station`, `claude/boss-city-ironworks`)

```csharp
// Core (engine-free): the parsed map. Runtime: NativeZoneBossSongs.Music (read once from Resources/ZoneBossMusic.json).
NativeZoneBossSong song = NativeZoneBossSongs.ForMap(arena.MapId);   // null: no zone song (file missing or other map)
double boss = arena.BossSongTime;                                    // < 0 before the boss arrives
double clip = song.Clip(boss);                 // the clip position NativeAudio plays (-1 in the preroll)
var pos = song.Track.Position(clip);           // (bar, beat 1..4, phase 0..1) on the tracked beat grid
string section = song.Track.sections[song.SectionAt(pos.bar)].id;
double t = song.FirstBoss(16.096);             // the boss time a clip second is first heard (9.068: the drop)
double d = song.Downbeat(12);                  // bar 12's downbeat in the clip
NativeMusicAccent[] accents = song.Accents;    // {T (clip s), Kind: AccentChime/Slam/Cut/Drop, S 0..1}
NativeZoneBossBeat beat = song.Evaluate(boss, mode);  // the stage's pulses, cut, flare, intro steps, mood
// Loop pass k (k >= 0): clip c in [loop.start, loop.end) sounds at boss time song.Hold + k*song.LoopLength + (c - loop.start).
```

For Central Station, the THE 6:47 design asks for a chime hit, a light cut, intro accents and the drop. All of them exist
here:
- **Chime hit:** 1.327.
- **Light cuts:** 4.862, then 7.720.
- **Accent slams:** 3.011, 6.206 and 8.731.
- **Drop:** 9.068.

The approach between the cuts is half lit. Row 0 reads LAST CALL during the intro. The design's "ARRIVING" wording would
be a new board variant, which the boss presentation can add.

A boss intro can key its moves to these boss times on the shared clock:
- Station: the chimes, the slam, the cuts and the drop.
- Ironworks: the downbeats, the slams and the drop.

The boss intro hold must stay 9.0678 s. `Tests/ZoneBossMusicChecks.cs` pins it to `NativePve.cs`. Changing the hold means
regenerating the map.

## Regenerating the maps

`python3 tools/zone_boss_beatmap.py` needs ffmpeg, numpy and scipy (matplotlib for `--plot`). CI does not run it. It does
the following:
- decodes each MP3;
- tracks the eighth-note grid (HPSS percussive onsets, then dynamic programming against a local tempo curve, smoothed);
- aligns it to the waveform's attacks;
- finds the accents;
- writes the JSON with the editorial sections, loop and intro edit from its `SONGS` table.

Options:
- `--check` compares the result with the committed map.
- `--plot DIR --clicks DIR` writes the review plots, click-track mixes and the boss-intro previews.

Measured checks of the grid against the onset envelope:
- **Station:** 98 % of the 410 strongest onset peaks lie within 25 ms of an eighth (chance is about 30 %). The onset is
  1.30x stronger on beats than off beats.
- **Ironworks:** 99 % of the strongest onset peaks lie within 25 ms of a sixteenth.

## Limits

- **MP3 decoder delay:** Unity's importer may keep the MP3 encoder delay that ffmpeg trims, about 24 to 35 ms. The stage
  would then lead the audio by about two frames.
- **Music sync tolerance:** NativeAudio corrects music drift only beyond 0.12 s. The Siren's song has the same limit.
- **Seeks:** the intro splice and the loop wrap are seeks. Each can land up to one frame late and may click faintly.
- **Not verified:** none of this has been heard or seen in Unity or Windows.
- **Beat-locked hazards:** express on the drop, presses on downbeats and similar are rules changes. They are proposed to
  Astra (prompt 18).

## Astra prompt 18: beat-locked hazards (optional rules change)

```
Claude's zone boss music is on claude/zone-boss-music-sync (docs/ZONE-BOSS-MUSIC.md). Central Station's boss now plays
Station Chime of Doom and City Ironworks' boss plays Hydraulic Hell. Each song plays an intro edit whose drop lands exactly
when your boss intro hold (NativePve.cs, 9.0678 s) ends, then a bar-aligned loop. Both run on NativeArena.BossSongTime, so
every peer hears the same bar. The stages react to the beat (lights, board, furnaces, steam, sparks), but presentation only:
every hazard still runs on your clock, and its warnings still match its damage. If you want the hazards themselves on the
beat, as the Siren's vents use her map (NativeBossTrack.Vent), the map is ready. This is a rules change, so it is your call.

Keep the intro hold at 9.0678 s. Tests/ZoneBossMusicChecks.cs pins the map to that literal. If you change it, rerun
python3 tools/zone_boss_beatmap.py (set HOLD) and the checks.

API (Core, engine-free; Runtime loads it once):
  var music = NativeZoneBossMusic.Parse(Resources.Load<TextAsset>("ZoneBossMusic").text);
  var song  = music.Song("central-station-boss");          // or "city-ironworks-boss"; not null in any build
  double clip = song.Clip(BossSongTime);                   // -1 before the arrival / in the preroll
  var pos = song.Track.Position(clip);                     // (bar, beat 1..4, phase 0..1), drift-tracked grid
  string section = song.Track.sections[song.SectionAt(pos.bar)].id;
  double t = song.FirstBoss(c);                            // boss time a clip second is first heard
  // Loop pass k >= 0: clip c in [loop.start, loop.end) sounds at boss time song.Hold + k*song.LoopLength + (c - loop.start).
  song.Accents                                             // {T clip s, Kind Chime/Slam/Cut/Drop, S 0..1}
Use the map file even when the MP3 is missing. Hazards must not depend on whether a presentation file is installed.
Keep each hazard's warn and live lengths as they are, so the telegraphs match. Beat-locking only changes when a cycle
starts.

Suggested timings. Boss time 9.068 is the drop, when the fight starts:
- Station (about 179 BPM, bar about 1.344 s, loop 66.85 s):
  - Express: pass on the downbeat of 'lastcall' (clip 77.671, boss 70.643 in the first pass, then every 66.85 s). Its 4 s
    warning starts during the stop-start bass cuts (clip 73.67). The other option is the 'rush' downbeat (clip 48.367, boss
    41.339). Either fits inside today's 70-110 s cadence if the first pass is kept clear of the fight's first 4 s.
  - Falling glass (every 4 s today): impact on every third downbeat (4.03 s); the 1.2 s telegraph starts about a beat
    earlier.
  - Spark cabinets (11.5 s cycle): every 8 bars (10.75 s), live on the downbeat.
  - Steam main (15 s cycle): every 12 bars (16.13 s), burst on the downbeat.
  - Optional power flicker on the stop-start cut: clip 76.104 to 78.001, boss 69.076 to 70.972 in the first pass. It would
    be your rail and flood rule.
- Ironworks (about 140 BPM, bar about 1.72 s, loop 126.27 s; the pour stays off in the boss encounter):
  - Drop hammer (6 s period, warn 1.2 s): live on every 4th downbeat (about 6.9 s); the warning starts about 2.8 beats
    before.
  - Runners (34 s): every 20 bars (34.4 s), live window from a downbeat.
  - Hot slab (18 s): every 10 bars (17.2 s).
  - Slag spill (40 s): every 24 bars (41.3 s).
  - Quench steam (22 s): every 13 bars (22.4 s). Optionally burst on the 'quench', 'venting' and 'blowdown' section starts
    (clip 58.954, 113.484, 130.219).
  - Converter tilt (the boss's): the 'meltdown' downbeat (clip 116.850, boss 111.910 in the first pass).

The presentation reads your hazard views as before, so beat-locked hazards keep their telegraphs with no visual change.
```
