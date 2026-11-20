# The melee weapon intros (intros v2, 2026-11-20)

Owner, 2026-11-20: "I want to move the intros to a much higher quality standard, great animations and effects, non-block based
background and designs, etc. With great camera work as well!" Seven new weapon signatures replace the archived ones: the sword,
the daggers, the shield, the greatsword, the Katana, the Glaive and the Fangs. The framework and its contract are I0's
(`docs/INTROS-V2.md`); this file is the melee family's design and its status. Presentation only.

Each intro is an authored scene: a story beat, a modelled set (scripted Blender, local bank `ChipArenaIntroMelee`, never
committed), the model's real pack motions layered in code, an authored camera, timed light, effects, a sound cue timeline, and the
live name landing on a hero pose. Each differs from the others in set, mood and camera.

| Weapon | Intro | Length / name | Set | Mood | Camera idea |
| --- | --- | --- | --- | --- | --- |
| Sword | Severed Dawn | 7.4 s / 5.9 s | The Lightwell: a ruined vaulted hall, one shaft of dawn | umber dark, white-gold shaft | macro beats, a cut too fast to see, then the slow reveal |
| Daggers | Afterimage | 7.2 s / 5.8 s | Lantern Alley: a narrow night market lane, paper lanterns, wet cobbles | teal and magenta neon | whip pans that chase the echoes |
| Shield | Hold the Line | 7.8 s / 6.3 s | The Breach: a broken fortress gate at dusk, rubble and banners | smoke orange, steel blue | behind the shield into the barrage, then the front |
| Greatsword | Faultline | 8.0 s / 6.4 s | The Rift: a basalt quarry floor over a molten seam | ember on charcoal | ground-level drag, crane to the sky, top-down on the crack |
| Katana | Still Rain | 8.2 s / 6.6 s | The Rain Garden: a flooded stone courtyard, a wooden gate, bamboo | indigo night, silver rain, one crimson | locked off and still; one cut in a frozen moment |
| Glaive | Orrery | 7.6 s / 6.0 s | The Observatory: a brass armillary floor under a twilight dome | violet and teal, stars | orbits round the orbiting rings |
| Fangs | Apex | 7.4 s / 5.9 s | The Canopy: overgrown jungle ruins at night, vines and roots | toxic green in blue-black mist | the prey's eye, the pounce at the lens |

## Severed Dawn (sword)

Story: the draw is so fast it cuts the light. Clips: `Armed-Idle`, `Armed-Unsheath-R-FromArmed-Hips`, `Sword-Attack-R1`,
`Sword-Attack-R6`, `Armed-Idle`.

1. 0.0-1.5 s. Dark hall. A single hard shaft of dawn light falls from a cleft in the vault onto the floor; dust turns in it. The
   camera cranes down the shaft from the vault to the pilot, who stands just outside it, head bowed, sword low at the side.
2. 1.5-2.6 s. Close: the visor; the hand on the grip; the light creeping across the flagstones toward the boots (the shaft slides
   as a cloud passes). Stillness. A single drop of dust falls through the shaft.
3. 2.65 s, the cut. One step into the light and a forehand cut (Sword-Attack-R1 run at 2.4x to its strike): the frame shows the
   start and the end of it, nothing between (two frames of a white smear). A hairline hangs in the air where the edge went.
4. 2.8-4.2 s. Slow (0.25x) side view: the shaft of light parts along the hairline; its lower half slides off the cut plane and
   breaks into falling flakes of light, the dust in it scattered; the hall goes dark but for the blade, which now holds the light
   (a glow running up the edge).
5. 4.2-5.6 s. A second, slower backhand (Sword-Attack-R6) traces a ribbon of the captured light; the flakes swirl into it.
6. 5.6-7.4 s. Hero: the sword brought up before the visor, light pooling on the floor round the boots, the name at 5.9 s.

Set (The Lightwell): flagstone floor with worn joints and cracks, two rows of massive columns receding into darkness, broken
vault ribs, the roof cleft, a raised dais behind, fallen masonry (chipped and bevelled), iron chains hanging from the vault.

## Afterimage (daggers)

Story: the pilot is faster than its own image: two echoes peel off and strike from both sides, then fold back into one. Clips:
`Armed-Idle`, `Armed-Dash-Forward`, `Dagger-Attack-R1`, `Dagger-Attack-L3`, `Armed-Run-Attack-Dual`, `Armed-Block-Dual`.

1. 0.0-1.4 s. A night market lane: paper lanterns swaying, wet cobbles reflecting them, steam from a vent. The pilot walks in from
   the far end; each lantern it passes flickers.
2. 1.4-2.2 s. It stops; a beat; then it is gone (a dash): a ghost of it is left standing, fading.
3. 2.2-4.4 s. The echoes: the pilot reappears left of the lens and cuts (R1); its echo, a step behind in time, cuts from the right
   (L3) a moment later; each echo is a translucent teal or magenta copy of the body on a delay. Lantern cords are cut; lanterns
   drop and swing; the camera whips between the two.
4. 4.4-5.4 s. The two echoes and the pilot converge on one spot in a cross cut (Run-Attack-Dual): three images land as one.
5. 5.4-7.2 s. Hero: daggers crossed low, the last echo sliding into the body; the lanterns settle, the name at 5.8 s.

Needs from the framework: echo bodies (the model drawn again at an earlier pose, tinted and translucent). Without them the echoes
are drawn as blade afterimages only.

## Hold the Line (shield)

Story: an immovable wall taking a barrage. Clips: `Armed-Shield-Idle~mirror`, `Armed-Shield-Activate` (the plant),
`Armed-Shield-Block~mirror`, `Armed-Shield-Block-GetHit1~mirror`, `Armed-Shield-Attack2~mirror`.

1. 0.0-1.3 s. Dusk at a broken fortress gate. Banners hang torn; smoke drifts. The pilot walks into the breach and plants the
   shield with a thud (dust ring of puffs, never a disc).
2. 1.3-1.8 s. From behind the shield: on the far ridge, a line of lights rise into the sky: the barrage.
3. 1.8-4.6 s. It lands: bolts of fire and steel streak in and burst on the shield face in sequence (each a Block-GetHit1 recoil,
   sparks, a ring of embers, scorch), near misses tear up the rubble round the pilot and throw debris; the feet do not move. The
   camera goes from behind, to the front low with bolts streaking past the lens, to the side.
4. 4.6-5.4 s. Silence. The smoke drifts; the shield's face glows hot; the pilot is where it was.
5. 5.4-6.0 s. A bash forward (Attack2): the smoke is blown back in a wall.
6. 6.0-7.8 s. Hero: the shield up beside the visor, its emblem lit, embers falling; the name at 6.3 s.

## Faultline (greatsword)

Story: the weight that cracks the ground. Clips: `2Hand-Sword-Run-Forward` / `M_Big_Sword@Idle`, `M_Big_Sword@Intro`,
`M_Big_Sword@Skill_H` (the lunging slam), `M_Big_Sword@Idle`.

1. 0.0-1.8 s. A basalt quarry floor at night, the cut walls rising into haze. The pilot walks in dragging the greatsword, its tip
   scoring the stone (sparks, a hot line left behind). The camera rides low just ahead of the tip.
2. 1.8-2.8 s. It stops at the centre. The heave: the blade up over the shoulder (Intro); the camera cranes up past it to the sky.
3. 2.8-3.6 s. The slam (Skill_H, slowed into the strike): the blade meets the floor; the ground cracks outward in a branching
   faultline; the cracks glow molten; slabs lift and tilt; a shock of dust.
4. 3.6-5.0 s. Top-down: the cracks run to the quarry walls; light wells up through them; grit falls.
5. 5.0-8.0 s. Hero: the sword on the shoulder, the pilot standing on a tilted slab, the faultline glowing beneath; the name at 6.4 s.

## Still Rain (Katana)

Story: rain, stillness and one cut. Clips: `Frank_RPG_Katana_Unequip_Idle` (sheathed), `Frank_RPG_Katana_S3_Skill01` (the iai),
`Frank_RPG_Katana_Unequip` (the sheathing), `Frank_RPG_Katana_Stance1_Idle`.

1. 0.0-2.0 s. Night. A flooded stone courtyard in rain, a wooden gate behind, bamboo moving. Locked-off wide shot: the pilot stands
   in the rain, hand on the saya. Rain rings in the puddles. Nothing else moves.
2. 2.0-3.4 s. A slow push in. Lightning far off lights the gate for a moment (one flash; Reduced: a soft rise).
3. 3.4 s, the cut. The iai (S3_Skill01 through its strike): the rain stops in the air. A frozen moment: the camera glides along the
   cut through the hanging drops, every drop on the cut's plane split in two.
4. 4.4-5.6 s. Time comes back: the rain falls; the split drops fall apart; behind the pilot, three bamboo stalks slide on their cut
   and fall. The sheathing (Unequip): the click at the saya's mouth and a ring of rain blown out.
5. 5.6-8.2 s. Hero: still again, in the rain, a crimson glint on the saya; the name at 6.6 s.

## Orrery (Glaive)

Story: ring-blades orbiting like worlds in an orrery, gathered into one. Clips: `Armed-Idle` and the Glaive's keyed moves (Core
`NativeGlaiveMotion`).

1. 0.0-1.6 s. Twilight under an open observatory dome: a brass armillary floor, rings within rings. Above the pilot, ring-blades
   orbit on tilted paths like planets; the camera orbits with them.
2. 1.6-3.0 s. The pilot raises a hand; the orbits tighten and fall into one plane round it.
3. 3.0-4.0 s. One by one they fold into the one in the hand (each a flash at the catch).
4. 4.0-5.2 s. The throw: the ring flies out round the dome's columns and back (the camera follows it), carving a ring of light.
5. 5.2-7.6 s. The catch and the hero: the ring upright beside the visor, the brass floor's rings turning; the name at 6.0 s.

Needs from the framework: the Glaive's ring drawn where the script says (several copies).

## Apex (Fangs)

Story: a predator's pounce. Clips: `Frank_Stealth_Kill_Sneak_Idle`, `Frank_RPG_Assassin@Velocity_8Way_Walk_F`,
`Frank_RPG_Assassin_Skill02_IP` (the leap), `Frank_RPG_Assassin_Equip_Idle`.

1. 0.0-1.6 s. Overgrown jungle ruins at night, mist low among roots, fireflies. The camera drifts like a prey looking round.
2. 1.6-2.6 s. Two green eyes in the dark above. Found: crouched on a high broken lintel, the pilot.
3. 2.6-3.6 s. It creeps along the lintel, low (walk), the fangs out.
4. 3.6-4.6 s. The pounce: straight down at the lens (slowed at its height), venom streaming off the fangs.
5. 4.6-5.4 s. The landing: both fangs driven into the earth, a green bloom of venom and leaves blown out.
6. 5.4-7.4 s. Hero: rising from the crouch, venom dripping from both tips; the name at 5.9 s.

## Sounds

Each intro's cues are listed in its Core file with their beats. Generation (ElevenLabs, owner-approved) goes through the request
manifest and the builder's queue; until the takes are imported each cue plays an existing sound (named in the cue list).

## Status

Design. Implementation follows I0's contract (`docs/INTROS-V2.md`).
