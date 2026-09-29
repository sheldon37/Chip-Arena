# Weapon entrances, knockdowns, and Gibber

- Greatsword E now checks close frontal contact during the airborne Wolf Knight spin. Successful direct hits cause an articulated ragdoll until the recovery roll. The capsule, damage, ground protection and recovery remain authoritative. Direct contact and the landing shockwave share an attack ID and hit list: each target is hit at most once per E, including blocked contacts. The existing 1.12× damage multiplier, stamina cost, cooldown, and landing shockwave remain.
- Shield basic attacks retain knockback and hitstun and now knock opponents down. Existing legs-up pickup limits and roll-away recovery still apply.
- The default intro option is now labelled Weapon Signature and resolves to the selected weapon. Explicitly selected legacy entrances remain available. The three existing dev entrances remain intact.

| Weapon | Signature entrance |
|---|---|
| Sword | Flash Draw — hip draw, body-driven cut, light arc, blade salute |
| Daggers | Twin Phantom — low turn, paired twirls, afterimages, crossed blades |
| Greatsword | Colossus Oath — heavy lift and impact, floor cracks, shoulder carry |
| Shield | Bastion Drop — low brace, shield plant, hexagonal barrier, ready stance |
| Pistol | Deadeye — wrist twirl, catch and aim, barrel glint |
| Rifle | Longwatch — low weapon check, support-hand rack, sight line |
| Machine gun | Belt Rattle — shoulder heft, short cosmetic burst, falling casings |
| Fire | Cinder Crown — palm ignition, circling flames, burning ground ring |
| Water | Tidal Serpent — flowing hand sweep, two spiral ribbons |
| Lightning | Thunderstep — elevated arrival, ground compression, forked strike |
| Darkness | Eventide Gate — opening portal, ritual gesture, closing gate |

Each new entrance uses an absolute-time full-body pose sequence, close/action/hero camera framing, and an original 4.8-second synthesized sound cue. The visual direction uses anime draw/afterimage/elemental motifs and fighting-game entrance framing; no game animation or audio assets were copied. Official presentation references consulted: https://www.bandainamcoent.com/news/fist-meets-fate-tekken-8-story-gameplay-reveal-trailer and https://news.capcomusa.com/2022/06/02/street-fighter-6-redefines-the-genre-in-2023/.

Gibber is a developer-only kill effect. The authority classifies the fatal impact into a head, torso, arm or leg region relative to the victim's facing. The matching ragdoll joint is severed, the detached part is propelled away, and a bounded blood spray emits from the moving wound with ground splatters. The effect remains cosmetic, expires with the normal death presentation, and cannot be enabled by an item grant. Original blank faces and cosmetics remain intact.

Validation: TypeScript and focused combat, recovery, network, account, intro, audio, ragdoll, and access-control checks. 64 checks passed together; a further lethal-thorns cancellation regression passed with the 19-check presentation group. Offline Three.js renders of all eleven entrances and Gibber were inspected. No browser playtest was available.
