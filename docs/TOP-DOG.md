# Top Dog: a DEV class for the Hot Dog

The owner's request, 2026-11-08: "Id like to make another dev class with an intro (aimed towards this new hotdog skin) VERY high quality and
detailed. It will essentially use toon power, with multiple abilities 1-5, An ultimate ability, they will trap a player and put wiggling
hotdogs in their mouth. the player will then be released, their stomach fully expanded and then blow up (Gibbing)"

The owner's choices the same day:

- Claude builds all of it, the rules as well as the look (not the usual split with Astra).
- Q, E and 1 to 5 are abilities; the ultimate is on 6.
- The class always wears the Hot Dog skin (docs/HOT-DOG-SKIN.md).
- The left click is a cartoon mallet.

Status: on `claude/top-dog`, DEV access only, like the Bombadier and the Cyber Lord. It has not been played by a person or between two
real clients.

## The idea

A cartoon. Everything the Top Dog does is a gag you can see coming: a mallet as big as its body, a spring under its feet, a dust cloud left
standing where it was, an anvil whose shadow grows on the floor, a banana peel, a squeeze bottle of mustard, a boxing glove on a scissor
arm, a cream pie. The ultimate is the punchline.

## The kit

| Key | Ability (`id`) | What it does | Damage | Cooldown |
| --- | --- | --- | --- | --- |
| LMB | Big Mallet (`big-mallet`) | Three swings, two-handed: a side swing, a back swing, then an overhead slam that lands 1.7 m ahead and **flattens** everyone within 2.1 m of it for 0.8 s. A swing hits 2.9 m out in a 115 degree arc and staggers. The combo carries on if you swing again within 0.45 s | 14, 14, 26 | |
| RMB, Shift | Block, Roll | As any melee weapon | | |
| Q | Boing! (`boing`) | A spring pops out under the feet and throws you 9 m up and 8 m/s along your look. The landing thuds: 2.6 m round you, a small knock-up | 8 | 8 s |
| E | Zip (`zip`) | 0.3 s running on the spot (rooted), then you zip 12 m ahead in 0.3 s (a wall stops you), leaving your dust cloud standing behind. Anyone within 1.3 m of the path is swatted aside | 10 | 7 s |
| 1 | Anvil (`anvil`) | Point at a spot up to 22 m away. Its shadow and a red ring grow there for 0.9 s while it falls, then it lands: 2.1 m round, **flattened** for 1.1 s. It sits there 3.5 s, then goes in a puff | 28 | 10 s |
| 2 | Banana Peel (`banana-peel`) | Toss a peel up to 10 m. An enemy whose feet come within 0.75 m of it slips: legs up, onto the back (knocked down), slid along. Two charges, three peels out at most, each lasts 20 s | 8 | 6 s a charge |
| 3 | Mustard Blaster (`mustard`) | Squirt mustard for 1.6 s at half speed, aiming as you go: a 9 m stream, 22 degrees each side. It slows by 45% for 1.8 s and leaves puddles that slow anyone standing in them | 20 a second | 11 s |
| 4 | Spring Glove (`spring-glove`) | A boxing glove on a scissor arm shoots 13 m along your aim. The first one it reaches is punched away at 15 m/s and sees stars for 0.7 s. Blockable, like a punch | 18 | 9 s |
| 5 | Cream Pie (`cream-pie`) | Throw a pie on a light arc. Whoever it hits has cream over their eyes for 2.6 s; anyone within 1.2 m gets a smaller splash | 8 (4 splash) | 12 s |
| 6 | **All You Can Eat** (`all-you-can-eat`) | The ultimate, below | the kill | meter |

The meter fills 1% a second, 6% a mallet hit and 8% an ability hit.

## All You Can Eat

1. **The twirl, 0 to 0.6 s.** A rope of sausage links whirled overhead. The caster's screen: a red and white diner check, "ALL YOU
   CAN EAT!". Everyone else: a banner and a dinner bell. The caster is rooted from here until it lets go.
2. **The throw.** The links fly along the aim at 32 m/s, 0.8 m wide, up to 18 m. A dodge roll goes through them. A **block snaps
   them**. A boss (the Siren, a zone boss, NOCTIEL) takes 30 and the links snap. A miss gives half the meter back.
3. **The catch.** The victim is wrapped in the links, reeled in over 0.5 s to 1.25 m in front of the Top Dog, and held: no moving,
   no acting, their head thrown back.
4. **The feeding.** Six wiggling hot dogs, one every 0.5 s, each brought to the mouth and pushed in. Each hot dog does 3 (it never
   kills). The belly grows with each one.
5. **The snap.** While it feeds, the Top Dog is rooted and can be hit. **If it takes 40 damage, or is stunned, knocked down or killed,
   the links snap**: the victim is free, takes 6 for each hot dog already down, and the belly goes down with a burp.
6. **The let-go**, 0.2 s after the last hot dog: a kick sends the victim staggering 1.8 m back, "STUFFED!". The belly keeps ballooning
   and trembling faster.
7. **The pop**, 1.5 s later: the victim bursts. It is an execution of their current health, as the Siren's crush is. The other enemies
   of the Top Dog within 3.2 m take 22 and are thrown. With gore on the body bursts (below); hot dogs, confetti, mustard and ketchup fly
   out either way. Once it has let go, the pop comes even if the Top Dog dies, and the round waits for it.

## The intro: "DEV · Order Up!" (`dev-topdog`)

8.6 s, the name at 6.6 s, an old cartoon from the first frame.

| Time | Beat |
| --- | --- |
| 0 | An iris opens on a hot dog cart in the dark arena, steaming under its striped umbrella, the grill sizzling |
| 0.85 | A bell rings for service |
| 1.45 | A drumroll: the cart rattles, the lid rattles, more steam (close on the lid) |
| 2.45 | The lid bursts open in a white puff, hot dogs fly out, and the Top Dog springs out on a giant spring (whip out wide, looking up) |
| 2.95 | At the top, spread like a star |
| 3.45 | It lands in front of the cart in a deep squash and springs back up, dust round it |
| 4.0 | It reaches behind its back and pulls out the mallet, which grows out of nothing with a twinkle (over the shoulder) |
| 5.0 | Overhead and down: the slam, a white ring tearing over the floor, dust, stars, the hot dogs on the floor hopping |
| 5.7 | It leans on the mallet |
| 6.6 | A diner sign swings down on its chains, bulbs chasing round it, the name stamped on it in cream, TOP DOG on a mustard ribbon, confetti |
| 7.35 | A thumbs up |
| 7.7 | The iris closes on it |

Its sound is its own toon cues, no music bed.

## The looks

- **The Hot Dog, always:** the body takes the Hot Dog (`upper:hot-dog`) whenever the seat's weapon is the Top Dog, and through its
  intro whatever the pilot carries (Runtime `NativeTopDogPresentation.cs` `TopDogSeat`). Without the local bank the default top shows,
  as for the skin itself.
- **The mallet** is a held weapon model (Core `NativeWeaponModels.Dog.cs`), held two-handed as the greatsword is. It goes into
  hammerspace while the hands hold a prop or the feast's hot dogs.
- **The props** are built in C# (Core `NativeTopDogModels`): the mallet's art aside, the wiggling hot dog (bun, sausage, a mustard
  zigzag), the anvil, the banana peel, the cream pie, the spring, the glove and its scissor arm, the mustard bottle, the sausage links,
  the stars and the cart. They draw in the pilots' own toon shader with its outline, so they light and line as the characters do.
- **The body:** the feast's victim's belly balloons: a warp about the belly in the body's own shader (`_Belly`, Resources
  `NativeVisualBodyFx.cginc` `BellyVert`), so the skin, the clothes, everything worn and the links round them swell together (each piece
  is weighted to the spine differently, so scaling a bone pushed the skin through the shirt). Each point moves out along its ray from a
  centre inside the torso, most at the front (the way the face looks), never folding, so a layer outside another stays outside it. Its
  head is thrown back and its mouth wide (the face's Gobble mood, then Bloat once stuffed). A flattened pilot is squashed to a fifth of its
  height.
- **Effects:** cartoon impact bursts and stars, comic words over each hit (BONK!, WHAM!, CLANG!, SPLAT!, POW!, GULP!, KA-POP!...), the
  anvil's shadow, the mustard stream and drips, the zip's dust silhouette, cream over a pied pilot's eyes.
- **The burst:** a new kind of the smash gib (Core `NativeSmashGib.Burst`): the trunk goes to fragments, the head, arms and legs come
  away and fly out every way from the belly, blood gushing all round. Gore off: a normal fall, with the hot dogs and confetti still.
- **The cameras:** the caster's twirl and throw, then side-on to both through the feeding and the swelling to the pop; the victim's
  own feeding and swelling, face on.

## Sound

Every cue is a toon sound synthesized in C# (Core `NativeToonSynth`, 34 of them: bonk, slam, boing, slide whistles, the anvil's whistle
and clang, splats, gulp, burp, pop, the dinner bell, a fanfare...), so the class needs no clip and is never silent. Core
`NativeTopDogSounds` maps the hits, casts and the entrance to them. The playback asks the effects bank for `topdog.<id>` first, so
recorded versions can replace them later: those keys are not in the bank's key table or a request list yet.

## The record (`Actor.Dog`, Core `NativeTopDogKit`)

Replicated in snapshots, late join, death records and the killcam like the Bombadier's kit, checked with `Valid()`:

- `Props`: what is out (anvil, peel, pie, glove, squirt, puddle, spring, zip), sampled flights.
- `Hits`: the last 1.5 s of contacts, for effects, words and sound.
- `Marks`: toon states on victims (pancake, slip, pied, mustard, dazed, stuffed).
- `Feast`: All You Can Eat's clocks, its victim, the lasso and how many hot dogs are down.
- `Meter`, `Peels`, `Combo`, `Ready` (Q, E, 1-5), and the cast (`CastKind`, `CastAt`, `CastRelease`, `CastUntil`, `CastStep`).

## Files

| Piece | File |
| --- | --- |
| Names, numbers, timeline, belly, glyphs | `Core/NativeTopDog.cs` |
| The record | `Core/NativeTopDogKit.cs` |
| The rules (host) | `Runtime/NativeTopDogAuthority.cs`, wired in `NativeArena.cs` |
| Rules validation (build checks) | `Runtime/NativeTopDogValidation.cs` |
| Effects, the camera, where things are | `Core/NativeTopDogFx.cs` |
| Props | `Core/NativeTopDogModels.cs`, the mallet `Core/NativeWeaponModels.Dog.cs` |
| Sound | `Core/NativeToonSynth.cs`, `Core/NativeTopDogSounds.cs` |
| The intro | `Core/NativeTopDogIntro.cs`, staged in `Runtime/NativeIntros.cs` |
| Presentation | `Runtime/NativeTopDogPresentation.cs` |
| HUD, card, words, audio | `Runtime/NativeTopDogScreen.cs` |
| Poses | `Core/NativeVisualMotion.cs` (`TopDog`, `DogCast`) |
| The burst | `Core/NativeSmashGib.cs` (`Burst`), `Runtime/NativeSmashPresentation.cs` |
| Sandbox preview | `Core/NativeTopDogPreview.cs` |
| Checks | `Tests/TopDogChecks.cs` (`CoreTests --top-dog`) |

## To see it

- With DEV access: pick Top Dog in the weapon picker (???). Customize, Entrance, "DEV · Order Up!", Preview Intro.
- DEV no-cooldowns also fills its meter and gives the peels back.
- The controller has no key 6 yet: the ultimate is keyboard only.
