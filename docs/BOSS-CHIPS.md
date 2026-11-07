# Boss relics: each boss's own chips

Owner, 2026-11-07: "Give the bosses 'exclusive' chip drops (maybe like 2-3 unique boss chips from bosses with unique effects) the chip
is themed around the boss, it has a special design and pick effect. Make these visuals beautiful. These unique chips are ONLY possible
from the boss drop chip selection and a rare chance."

**Status: live (Claude, 2026-11-07; the owner asked Claude to make the rules too).** The cards, art, 3D looks and pick; the rows in the
live content; never an ordinary offer; the rare boss drop; all twelve rules; each one's moment drawn in the world. DEV Sandbox, Preview,
Rarity, a `boss-*` tier still shows the cards and the pick.

## The twelve

Two per boss, one copy each, in the boss's own tier. Code: `Core/NativeBossChips.cs` (the rows, the card's words, the tiers and their
bosses); `Runtime/NativeBossChipArt.cs` (each boss's card scene and its pick); the tiers' looks in `Core/NativeChipCartridge.cs`.

| Boss | Tier | Relic | Id | What it does (the rule's words, a proposal) |
| --- | --- | --- | --- | --- |
| THE SIREN | `boss-siren` | Last Verse | `boss-siren-last-verse` | A kill sends out a ring of her song: every enemy within 6 m is slowed 40% for 2 s. |
| | | Lullaby | `boss-siren-lullaby` | Every 6th hit on the same enemy drags them 2.5 m toward you. |
| THE 6:47 | `boss-647` | Express Service | `boss-647-express` | Sprinting for 2 s brings you to full speed (+25% move speed). Your first hit at full speed deals +60% and throws them. |
| | | Mind the Gap | `boss-647-mind-the-gap` | A roll leaves a live rail along its path for 1.5 s. An enemy who crosses it takes 30 and is stunned .4 s, once per rail. |
| THE CONVERTER | `boss-converter` | Slag Heart | `boss-converter-slag-heart` | Damage you take is stored as heat. Your next hit releases it: +1 damage for every 2 heat, at most +40. |
| | | The Pour | `boss-converter-pour` | A kill spills molten slag 2.5 m wide for 3 s. Enemies standing in it take 10 a second. |
| THE HUM | `boss-hum` | Max Hum-Buzz | `boss-hum-max-hum` | Each hit builds a hum in its target. The fifth bursts it: 30 damage to every enemy within 3 m of them. |
| | | Noclip | `boss-hum-noclip` | Your roll passes through enemies. The first one it passes through takes 20 and is shown to you through walls for 3 s. |
| THE DEEP END | `boss-deepend` | Undertow | `boss-deepend-undertow` | Your hits pull their target 1 m toward you, and every 4th hit on one enemy drags them 3 m. |
| | | The Water Is Warm | `boss-deepend-warm` | Standing still for 1 s heals you 4% of your max health a second, until you move or are hit. |
| NOCTIEL | `boss-noctiel` | Feather Fall | `boss-noctiel-feather-fall` | You fall slowly. Landing from 3 m or higher sends out a ring of light: 25 damage to enemies within 4 m. |
| | | Lux Cecidit | `boss-noctiel-lux-cecidit` | Once a round, a blow that would kill you leaves you at 1 health instead, and you burst with light: 40 damage to enemies within 5 m. |

Every number is a proposal; Astra owns them. The card's short line and its green and red rows are in `NativeBossChips.Relics`.

## What they look like

- **The card** (the draft tray, the Siren's pick, the INSTALLED spotlight): the tier's colours, metal and crest; the grade reads
  `BOSS RELIC · <BOSS>`; sparks off the edges; and the boss's own scene living on the card, kept to the edge and the header so the
  rows stay readable: the Siren's song in rings; THE 6:47's departure board burning `6:47`; THE CONVERTER's pour into a glowing pool,
  sparks rising; THE HUM's fixture buzzing over Level 0's wallpaper, stuttering now and then; THE DEEP END's tile with light moving
  over it and a pool lamp; NOCTIEL's feather falling through the dark under a halo.
- **The pick** (over the INSTALLED card, with the boss's own line: SHE IS STILL SINGING, NOW DEPARTING, POURED AND SET, IT HAS HEARD
  YOU, THE WATER IS ALWAYS WARM, LUX CECIDIT): the Siren's rings rolling across the screen with a split; a headlight sweeping past and
  the board's rows flipping down the card; a white-hot flash and molten sparks thrown off and falling; the screen dropping dark for a
  beat, then the room's lights slamming on row by row; a drop and rings across the screen with a splash; a beam of light down onto the
  card, its halo opening and feathers coming down through it. Reduced effects keeps one soft glow.
- **The tray's entrance:** a relic falls in from above, too bright, and settles.
- **The 3D chip** (the Expedition reward screen) takes the tier's look from `NativeChipCartridge.Of`.

## How they work (built)

1. **Rows and rarities.** `NativeBossChips.Apply(content)` adds the twelve rows and six rarities (weight 0) where the live content loads
   (Runtime `NativeStructures`, beside the other overlays).
2. **Never in an ordinary offer.** `NativeContentRules.Offers` leaves every relic out of its pool before any roll (so every ordinary roll is
   exactly as it was without them): drafts, PvE picks, chests, machines, monster drops, the Boss Battle's opening picks.
3. **The drop.** An Expedition boss reward (`source` "boss", `ExpeditionSession.Offers`): its boss is its zone's (`RewardBoss`: the zone in
   the reward's id, that zone's map, `NativeZoneBossRules.Archetype`, else the Siren); with `DropChance` (12%) one of that boss's relics the
   pilot can take replaces the screen's last offer (`Drop`, `Place`). NOCTIEL's own draft (a PvE pick with source "boss") rolls the same.
   The frozen offers keep it once rolled. At most one per screen.
4. **The rules** (Runtime `NativeBossRelicRules`, hooked beside the wild chips'): every id is in `SupportedMechanics`. Zone bosses can't be
   moved or stunned by them. Lux Cecidit's "once a round" also comes back after a death (an Expedition is one long round). Their counters
   and rails are the authority's own (not in the host handoff frame, as the wild chips' timers aren't).
5. **In the world:** each proc is a ChipProc with the relic's id; Core `NativeBossRelicFx` draws it (the Siren's rings and ribbon, the
   express's headlight and strike, the live rail, molten slag and its pool, the hum building and bursting, Noclip's glitch, the undertow,
   the warm light, the ring of light on landing, the beam that saves). Noclip's reveal: the quota marker's kite in the HUM's white over the
   one the roll went through, through walls for 3 s (Runtime `NativeBossRelicHud`).

## The old contract (for reference)

1. **Rows and rarities.** `NativeBossChips.Apply(content)` adds the twelve rows and six rarities (weight 0). Call it where the live
   content loads, beside `NativeBombChips.Apply`.
2. **Never in an ordinary offer.** Every draft, shop, PvE pick and ordinary drop filters `NativeBossChips.IsTier(c.rarity)` out, the way
   `c.rarity!="siren"` is filtered today.
3. **The drop.** A boss reward screen (`ExpeditionReward` with `source="boss"`, `NativeExpeditionIntegration.Offers`): with chance
   `NativeBossChips.DropChance` (12%, a proposal) one of its three offers becomes one of *that* boss's relics (`NativeBossChips.Of(archetype)`)
   that the pilot doesn't hold. At most one per screen. The Siren's are the same from her reward in Ward Zero.
4. **The mechanics.** Each relic's `mechanic` is its id. Add each to `SupportedMechanics` as its rule lands; until then
   `ImplementedChip` keeps it out (the preview adds them itself).
5. **Proc events** for the pick in the world, if wanted: an `Install` event for a relic id already reaches the presentation.

The checks: `Tests/BossChipChecks.cs` (`--boss-chips`): twelve, two per boss, one copy, own tier and look, copy and icons; in the live
content and never in an ordinary offer (and the ordinary rolls unchanged); the drop's rate, only its boss's, never one it can't take, placed
last; a reward's boss by its zone; every relic's moment drawn finite through its life and thinner with Reduced effects.
