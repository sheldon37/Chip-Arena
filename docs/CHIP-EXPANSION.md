# Chip expansion: ability chips, wild chips and readable cards

Owner, 2026-11-04: "Add a good portion of chips that directly effect weapons abilities (provided they have that specific ability slotted). Lets also add a good portion of fun to use chips in the game (Say like... 50ish chips), rounds inspired. Some of the chips ABSOLUTELY can be super strong, but I also want the chips to be easier to read. A lot of them are currently cluttered. So we may need to do some text revisions or improve design again (I do like green/red stat text though)"

**Status:** design, rows and card by Claude; mechanics are Astra's. `Core/NativeWildChips.cs` and `Core/NativeAbilityChips.cs` are generated with the tables here. Neither is applied to the content yet, so neither set can be drafted, and adding them changes nothing on the wire until the rules are in.

## How a chip reads now

1. **The name.** What you call it.
2. **At most one short line**, only if the chip does something the numbers cannot say. "Rolling drops a mine." A chip whose rows say it all has no sentence: "Increases attack speed." over "+15% Attack speed" was the clutter.
3. **Green and red rows.** Every number lives here, green for you and red against you, grey where it is neither. Conditions and limits are rows too.

Rules for the text: no "per stack", "additive" or "recursion"; no sentence over about two lines on a tray card; one idea a row; say who it affects when it is not you ("-15% Their speed"). The footer says who can take it. The copy lives in `Core/NativeChipCopy.cs` and overrides the pinned `ChipText.json` export on the card only: the rows of rules text in `docs/CHIP-CATALOG.md` still hold the exact behaviour.

## Ability chips

Forty-eight chips, four a weapon: its Q, its E, and two of its techniques. A chip is drafted only while its ability is in the pilot's Q or E, so every one offered is one you can use. The footer says `RIPOSTE ONLY`. The four chips already tied to one ability (Cinder Field, Undertow, Abyssal Core, Storm Recall) are gated the same way once this is in.

| Ability | Chip (`ab-`) | Rarity | On the card | The rows | The rule |
| --- | --- | --- | --- | --- | --- |
| Flash Step | Afterimage Cut (`afterimage-cut`) | rare | Flash Step leaves a cut along its path. | **+12 Damage along the path, after .4 s** | The path bursts .4 s after the step: 12 in a 1 m capsule. |
| Rising Dragon | Sky Splitter (`sky-splitter`) | rare | Rising Dragon climbs higher and lands hard. | **+30% Height**; **+10 Damage in 3 m on landing** | Height x1.3; the first landing within 2 s after it bursts for 10 in 3 m. |
| Riposte | Perfect Form (`perfect-form`) | rare | Riposte is easier, and a parry pays you back. | **+.2 s Guard**; **+15 Stamina on a parry** | Guard .5 to .7 s; a parry returns 15 stamina. |
| Iaido | Moonlit Edge (`moonlit-edge`) | epic | Iaido charges faster and cuts farther. | **-33% Charge time**; **+3 m Line** | Full charge at 1 s, not 1.5; the dash 5 to 8 m. |
| Shadow Step | Knife in the Dark (`knife-in-the-dark`) | rare | Your stab after Shadow Step always crits. | **Guaranteed crit within 1 s** | The first hit within 1 s after the step is a critical. |
| Kunai Mark | Twin Kunai (`twin-kunai`) | rare | Kunai Mark throws two, and the mark lasts longer. | **+1 Kunai**; **+2 s Mark** | Two kunai in a 6 degree fan; a mark lasts 2 s longer. |
| Smoke Veil | Choking Smoke (`choking-smoke`) | rare | Enemies in your smoke choke and slow down. | **+2 Damage a second**; **-20% Their speed** | Enemies in the cloud take 2 a second and move x.8. |
| Chain Hook | Barbed Chain (`barbed-chain`) | rare | Chain Hook reaches farther, and bites. | **+4 m Range**; **+6 Damage**; **Slows them for 1 s** | Range 16 m, damage 12, x.7 speed for 1 s after the yank. |
| Bulwark Charge | Battering Ram (`battering-ram`) | rare | Slam them into a wall with Bulwark Charge. | **+12 Damage if they hit a wall** | An enemy carried into a wall takes 12. |
| Aegis Ricochet | Triple Bank (`triple-bank`) | epic | Aegis Ricochet bounces to two more enemies. | **+2 Bounces** | Two more bounces at the current bounce rule. |
| Bastion | Spiked Wall (`spiked-wall`) | rare | Bastion is tougher and hurts to touch. | **+60 Wall health**; **+8 Damage on melee hits** | Wall 180 health; melee hitting it takes 8. |
| Ground Slam | Aftershock (`aftershock-slam`) | rare | Ground Slam lands twice. | **Second ring after .5 s**; At half damage | A second ring, .5 s later, 6 m, half damage, no knock-up. |
| Earthsplitter | Fissure (`fissure`) | rare | Earthsplitter leaves a crack that slows. | **+3 s Crack**; **-30% Speed on it** | The line stays 3 s; enemies on it move x.7. |
| Whirlwind | Eye of the Storm (`eye-of-the-storm`) | epic | Whirlwind drags enemies in. | **Pulls within 5 m** | Enemies within 5 m drawn inward at 4 m/s during the spin. |
| Anchor Throw | Deep Anchor (`deep-anchor`) | rare | Where your blade lands, enemies are pulled in. | **Pulls within 4 m when it sticks** | On sticking, a .4 s pull within 4 m toward the blade. |
| Blade Drag | Molten Trail (`molten-trail`) | rare | Blade Drag leaves fire on the floor. | **+4 Damage a second, for 3 s** | Each Path point burns for 3 s, 1 m wide, 4 a second. |
| Fan the Hammer | Six Aces (`six-aces`) | rare | The last shot of Fan Hammer hits twice as hard. | **+100% Last-shot damage** | The last shot of the fan deals double. |
| Frag Grenade | Cluster Frag (`cluster-frag`) | epic | Frag Grenade bursts into three more. | **+3 Bomblets**; 15 Damage each | On detonation 3 bomblets in 3 m, .3 s fuse, 15 in 2 m. |
| Ricochet Coin | Lucky Coin (`lucky-coin`) | rare | The coin comes back faster and turns twice. | **-3 s Cooldown**; **+1 Target** | Cooldown 6 s; the shot turns once more, to a second enemy. |
| Flash Round | Blinder (`blinder`) | rare | Flash Round blinds longer and wider. | **+.5 s Blind**; **+2 m Radius** | Whiteout 1.4 s, 8 m, and you are spared if you were not facing it. |
| Overdrive | Cooling Jacket (`cooling-jacket`) | rare | Overdrive lasts longer and kicks less. | **+2 s Overdrive**; **-50% Spread bloom** | 2 s longer; spread growth halved during it. |
| Underbarrel Rocket | Twin Rockets (`twin-rockets`) | epic | Your rocket launches two. | **+1 Rocket**; At 70% damage each | Two rockets in a 5 degree fan at 70% each. |
| Sentry Drop | Second Gun (`second-gun`) | rare | Your sentry is tougher and fires faster. | **+40 Sentry health**; **+50% Sentry fire rate** | 80 health; 3 shots a second. |
| Bipod | Belt Feed (`belt-feed`) | rare | Bipod stays up longer and patches you up. | **+2 s Planted**; **+3 Health a second** | 7 s; heals 3 a second while planted. |
| Orbital Mark | Danger Close (`danger-close`) | epic | Orbital Mark hits harder and wider. | **+30% Radius**; **+15 Damage** | 3 to 3.9 m; 85 to 100. |
| Grapple Line | Hook Shot (`hook-shot`) | rare | Your next shot after Grapple Line hits harder. | **+50% Damage for 2 s after landing** | The next shot within 2 s after a reel deals x1.5. |
| Sonar Dart | Deep Scan (`deep-scan`) | rare | Sonar Dart pings more, and farther. | **+2 Pings**; **+4 m Radius** | Five pings; 16 m. |
| Breacher Round | Hollow Point (`hollow-point`) | rare | Breacher Round slows whoever it hits. | **-30% Their speed for 2 s** | Enemies hit move x.7 for 2 s. |
| Volley | Wider Volley (`wider-volley`) | rare | Volley fires two more arrows. | **+2 Arrows** | Two more arrows, the fan's angle kept per arrow. |
| Rain Of Arrows | Burning Rain (`burning-rain`) | epic | Rain of Arrows sets the ground alight. | **+4 Damage a second, for 3 s** | The rain's circle burns for 3 s after it. |
| Spirit Hawk | Twin Hawks (`twin-hawks`) | epic | Spirit Hawk flies as a pair. | **+1 Hawk**; 8 Damage each | Two hawks, 8 each, diverging 8 degrees. |
| Tether Arrow | Short Leash (`short-leash`) | rare | Tether Arrow holds tighter and snaps harder. | **-1.5 m Leash**; **+6 Snap damage** | 3.5 m; a snap deals 12. |
| Flame Lance | White Heat (`white-heat`) | rare | Flame Lance burns twice as hot. | **+100% Lance burn** | The burn the lance applies is doubled. |
| Meteor Strike | Twin Meteor (`twin-meteor`) | epic | A second meteor follows the first. | **+1 Meteor after .6 s**; At half damage | A second at the same point, .6 s later, half damage, half radius. |
| Ignition Leap | Phoenix Dive (`phoenix-dive`) | rare | Press again in the air to dive, bursting on landing. | **Recast dives down**; **+16 Damage in 3 m** | A recast drops you at 24 m/s; landing bursts for 16 in 3 m. |
| Dragon Breath | Deep Lungs (`deep-lungs`) | rare | Dragon Breath lasts longer and goes wider. | **+.8 s Breath**; **+10° Cone** | 2.4 s; 40 degrees. |
| Tidal Wave | Big Wave (`big-wave`) | rare | Tidal Wave comes in taller and wider. | **+40% Wave size** | Width and height x1.4; damage unchanged. |
| Geyser Wall | Scalding Steam (`scalding-steam`) | rare | Geyser Wall leaves scalding steam. | **+5 Burn a second, for 3 s** | Each geyser's column leaves steam 3 s, burning 5 a second. |
| Healing Spring | Sacred Spring (`sacred-spring`) | epic | Healing Spring heals more and washes away burns and poison. | **+50% Healing**; **Cleanses burn and poison** | 9 a second; standing in it clears burn and poison. |
| Bubble Trap | Double Bubble (`double-bubble`) | rare | Bubble Trap bursts for damage when it pops. | **+12 Damage when it pops**; **+.4 s Bubble** | 1.8 s; popping deals 12 to whoever is inside, in 2 m. |
| Blink Strike | Double Blink (`double-blink`) | epic | Blink Strike can blink once more right after. | **+1 Blink within 2 s** | A second press within 2 s blinks again; the cooldown starts after. |
| Ball Lightning | Ball Bearing (`ball-bearing`) | rare | Ball Lightning moves faster and bounces. | **+40% Speed**; **+1 Bounce** | The ball's speed x1.4; it bounces once off a wall. |
| Static Recall | Full Circuit (`full-circuit`) | rare | Static Recall heals you when you bolt back. | **+20 Health on recall** | Arriving at the anchor heals 20. |
| Thunderclap | Sonic Boom (`sonic-boom`) | rare | Thunderclap reaches farther and stuns longer. | **+40% Radius**; **+.2 s Stun** | 7 m; .7 s. |
| Shadow Shackles | Heavy Chains (`heavy-chains`) | rare | Shackles jump to a second enemy. | **+1 Target within 5 m** | The nearest other enemy within 5 m is shackled too, half as long. |
| Shade Swap | Trade Up (`trade-up`) | rare | Shade Swap dazes them and heals you. | **+.6 s Stun on them**; **+15 Health** | The swapped enemy is stunned .6 s; you heal 15. |
| Grave Walk | Undertaker (`undertaker`) | rare | Rising from Grave Walk bursts up through the floor. | **+14 Damage in 3 m on rising** | The rise bursts for 14 in 3 m and throws enemies up. |
| Soul Siphon | Soul Feast (`soul-feast`) | epic | Soul Siphon reaches farther and drains their resource. | **+4 m Reach**; **Drains mana and stamina** | 14 m; also drains 10 of the victim's mana or stamina a second, to you. |

### Rules contract (Astra)

- Call `NativeAbilityChips.Apply(content)` beside `NativeBombChips.Apply`, when the mechanics are in. Their stats are empty: each is a mechanic, by chip id (`mechanic` equals `id`), so `ImplementedChip` keeps it out until `SupportedMechanics` has it.
- **Eligibility:** `NativeAbilityChips.Requires(chipId)` is the ability id, null for any other chip. In the draft's filter, refuse a chip whose ability is not in the pilot's current Q or E, default or technique. Keep a chip already installed if the pilot later swaps the ability: it just does nothing until it is back. Do not let a swap between rounds fish for offers: the offers are rolled when the draft opens.
- **Odds:** the pool is small, so with the ordinary weights a pilot would rarely see one. Proposal: when at least one is eligible, one offer in a draft has a 35% chance of being rolled from the eligible ability chips, at its own rarity's weights. Change it if it feels wrong; state what you used.
- **Bots** hold default abilities, so they may be offered the Q and E chips. Fine.
- **Presentation** reads `Has(pilot,chipId)` for the upgraded look and gets no new field, except where a chip makes a new thing appear that the record cannot describe (a second rocket, a second hawk): those use the same record or event as the first, one each.

## Wild chips

Fifty chips in the ordinary rarities and the ordinary draft: 8 common, 13 uncommon, 13 rare, 10 epic, 4 legendary, 2 mythic. Built to change how you play, in the spirit of ROUNDS. No names, art or text from it. Nothing here repeats a chip the game already has; several are meant to be silly, and some are meant to be frightening.

| Chip (`wild-`) | Rarity | Weapons | Max | On the card | The rows (bold green, italic red) | The rule |
| --- | --- | --- | --- | --- | --- | --- |
| Top Shape (`top-shape`) | common | all | 3 | Hit harder while you're healthy. | **+20% Damage above 90% health** | +20% damage per stack while above 90% health. |
| Cornered (`cornered`) | common | all | 3 | Fight faster when you're nearly dead. | **+25% Attack speed below 35% health** | +25% attack speed per stack while below 35% health. |
| Hangtime (`hangtime`) | common | all | 3 | Hit harder from the air. | **+25% Damage while airborne** | +25% damage per stack while the pilot is off the floor. |
| Planted (`planted`) | common | all | 3 | Stand still, hit harder. | **+25% Damage after 1 s standing still**; Gone the moment you move | +25% damage per stack after 1 s under .5 m/s; ends when above it. |
| Frenzy Scent (`frenzy-scent`) | common | all | 3 | Hurting someone makes you quick. | **+20% Move speed for 2 s after a hit** | +20% speed per stack for 2 s after dealing damage to a pilot or add; refreshes, does not stack by time. |
| Opening Move (`opening-move`) | common | all | 3 | Your first hit lands heavy. | **+50% Damage on your first hit**; After 4 s without hitting | +50% damage per stack on a hit if the pilot landed no hit in the last 4 s. |
| Thick Skull (`thick-skull`) | common | all | 2 | Headshots barely count. | **-40% Headshot damage taken** | The headshot bonus against this pilot is multiplied by .6 per stack (the whole hit, not only the bonus, if simpler: state which). |
| Spin Up (`spin-up`) | common | ranged | 2 | Hold the trigger, fire faster. | **+50% Attack speed after 2 s of fire**; Winds down when you stop | Attack speed climbs linearly to +50% per stack over 2 s of continuous fire; falls back in .5 s after the last shot. |
| Bloodhound (`bloodhound`) | uncommon | all | 2 | Run faster at the nearest enemy. | **+25% Move speed toward them** | +25% speed per stack while moving within 45 degrees of the nearest enemy in sight. |
| Radar Block (`radar-block`) | uncommon | all | 1 | A Block shows you everyone. | **Enemies seen through walls for 4 s** | A successful Block reveals every enemy to the pilot and allies for 4 s, by the same channel as Sonar Dart's reveal. |
| Charged Reply (`charged-reply`) | uncommon | all | 2 | Your next attack after a Block hits hard. | **+80% Damage on that attack**; Within 5 s | A successful Block arms +80% damage per stack on the next attack within 5 s. |
| Tapeworm (`tapeworm`) | uncommon | all | 2 | Your hits weaken their hits. | **-4% Their damage per hit, up to 5**; For 3 s after your last hit | Each hit stacks -4% (per chip stack) on the victim's damage, at most 5 hits, refreshed to 3 s. |
| Cold Shoulder (`cold-shoulder`) | uncommon | all | 2 | Enemies near you slow down. | **-15% Their speed within 5 m** | Enemies within 5 m of the pilot move x.85 (x.72 at two stacks). |
| Hand to Mouth (`hand-to-mouth`) | uncommon | ranged | 1 | Hits put rounds back in the magazine. | **+1 Round per hit**; *-20% Magazine size* | A hit on a pilot or add returns one round, up to the magazine's size. |
| Trick Shot (`trick-shot`) | uncommon | projectile | 2 | Every bounce makes it hit harder. | **+1 Bounce**; **+35% Damage per bounce** | A shot's damage gains +35% per stack, additive, for each bounce it has made (replacing bounce damage loss). |
| Snowball (`snowball`) | uncommon | projectile | 2 | Shots grow the farther they fly. | **+30% Damage and size per 10 m**; Up to three times | A shot's damage and hit radius gain +30% per stack for each full 10 m flown, at most three steps. |
| Bullet Hose (`bullet-hose`) | uncommon | ranged | 1 | Spray and pray. | **+150% Attack speed**; **+100% Magazine size**; *-60% Damage*; +12° Spread | Stats only: no rule |
| Hot Swap (`hot-swap`) | uncommon | ranged | 2 | Reloading sends out a shock. | **+15 Damage in 5 m on reload**; *+10% Reload time* | Beginning a reload bursts 5 m: 15 damage per stack and a shove. At most once per 2 s. |
| Afterimage (`afterimage`) | uncommon | all | 1 | Your dodge leaves a decoy behind. | **Decoy for 2 s**; **+8 Damage when it pops** | A dodge leaves a still copy for 2 s at its start: 1 HP, bots target it, homing shots may lock it; it pops in 2 m for 8. |
| Mana Tap (`mana-tap`) | uncommon | magic | 3 | Hits refill your mana. | **+4 Mana per hit** | A hit restores 4 mana per stack. |
| Follow Through (`follow-through`) | uncommon | melee | 3 | Hits give your stamina back. | **+8 Stamina per hit** | A melee hit restores 8 stamina per stack. |
| Steering Wheel (`steering-wheel`) | rare | projectile | 1 | Shots steer toward your crosshair. | **Shots follow your aim**; *-30% Projectile speed* | Each flying shot turns toward the point under the owner's crosshair at up to 240 degrees a second. |
| Curveball (`curveball`) | rare | projectile | 1 | Shots bend the way you were turning. | **Curve follows your aim's turn**; **+20% Damage on a curved shot** | A shot takes a constant turn from the aim's yaw rate at firing (at most 120 degrees a second, for 1.5 s). A shot turned 20 degrees or more deals +20%. |
| Anti-Gravity (`anti-gravity`) | rare | all | 1 | Every fourth hit makes them fall upward. | **+.8 s of reversed gravity**; Every 4th hit | Every 4th hit on one pilot reverses their gravity for .8 s (they rise at most 3 m). Bosses immune. |
| Battering Block (`battering-block`) | rare | all | 1 | A Block throws you forward. | **+4 m lunge on Block**; **+15 Damage and a shove** | Starting a Block lunges 4 m along the look in .18 s, stopping at walls; the first enemy met takes 15 and a shove. The Block still works. |
| Blink Block (`blink-block`) | rare | all | 1 | A Block teleports you where you aim. | **+6 m blink on Block**; *-30% Block window* | Starting a Block blinks up to 6 m along the look, stopped by cover, never into a wall. |
| Hush (`hush`) | rare | all | 1 | A Block silences everyone near you. | **No abilities for 1.5 s, 6 m** | A successful Block stops enemies within 6 m in sight using Q or E for 1.5 s (as Disarm). |
| Overkill (`overkill`) | rare | all | 1 | Extra damage jumps to the next enemy. | **Spare damage carries over**; Up to 12 m | Damage past a kill passes to the nearest enemy within 12 m in sight (no procs, no further jump). |
| Buzz Saws (`buzz-saws`) | rare | all | 2 | Saw blades circle you. | **+10 Damage a second within 2.5 m**; *-5% Move speed* | Enemies within 2.5 m take 10 a second per stack, ticking 5 times a second. Two blades, four at two stacks. |
| Double Tap (`double-tap`) | rare | all | 1 | Every attack repeats as a ghost. | **Repeats after .3 s**; At 35% damage | Each attack repeats from the same spot along the same aim .3 s later at 35%, triggering no chips and costing nothing. |
| Spite (`spite`) | rare | all | 1 | When you die, you explode. | **+60 Damage in 5 m**; After a 1 s warning | On death a 1 s warned blast at the body: 60 in 5 m. It can kill. |
| Splat (`splat`) | rare | all | 2 | Walls hurt. | **Up to 30 Damage on a wall hit**; **+50% Knockback dealt** | An enemy you knocked back who meets a wall within .6 s takes damage by speed, 2 per m/s per stack, at most 30. |
| Shrink Ray (`shrink-ray`) | rare | all | 1 | Your hits shrink them. | **-20% Their damage for 3 s**; *They are 20% smaller to hit* | A hit shrinks the victim to x.8 for 3 s, hitbox and model, dealing x.8. Refreshes; bosses immune. |
| Jet Rounds (`jet-rounds`) | rare | projectile | 1 | Hits strap a rocket to them. | **Pushed along the shot for 1 s** | A hit pushes the victim 8 m/s along the shot's direction for 1 s, at most once per 2 s per victim. Bosses immune. |
| Slow Bleed (`slow-bleed`) | epic | all | 1 | Damage reaches you over 4 s. | **Every hit lands slowly**; *-10% Health* | Damage on this pilot is paid over 4 s, hit by hit, as ticks. Healing still heals. Lethal only when a tick reaches zero. |
| Blood Magazine (`blood-magazine`) | epic | ranged | 1 | Never reload. Every shot costs you health. | **Endless ammo**; *-2 Health a shot*; Stops at 1 health | No rounds used and no reload; each volley costs 2 health, never below 1. At 1 health the gun still fires. |
| Wall Hack (`wall-hack`) | epic | projectile | 1 | Shots pass through the first wall. | **Through 1.5 m of wall**; *-25% Damage* | A shot passes its first wall if thinner than 1.5 m, and is otherwise unchanged. |
| Bank Error (`bank-error`) | epic | projectile | 1 | Bounces aim at the nearest enemy. | **+1 Bounce**; **Bounces turn toward enemies** | On a bounce a shot turns to the nearest enemy in sight within 25 m, if any. |
| Flanker (`flanker`) | epic | projectile | 1 | A second shot hits them from behind. | **Shot from behind after .3 s**; At 35% damage | A direct hit spawns a shot 3 m behind the victim .3 s later, aimed at them, 35%, triggering no chips. |
| Bodyguard Drone (`bodyguard-drone`) | epic | all | 1 | A drone shoots down incoming shots. | **One shot every 3 s, within 6 m** | Every 3 s at most, the nearest enemy shot within 6 m of the pilot is destroyed. |
| Doomsday Clock (`doomsday-clock`) | epic | all | 1 | Hold still for 3 s and everything near you pays. | **+60 Damage in 7 m**; **+20 Health back**; Then it starts over | After 3 s under .5 m/s, a burst: 60 in 7 m in sight, heals 20. Moving resets the clock. |
| Domino (`domino`) | epic | all | 1 | Enemies near your target share the hit. | **+30% of each hit within 6 m** | A hit also deals 30% to other enemies within 6 m of the victim in sight (no procs). |
| Head Stomp (`head-stomp`) | epic | all | 1 | Land on someone. | **+30 Damage on landing on an enemy**; **Bounces you 6 m up** | Falling onto an enemy's head from over 1 m: 30 and a bounce 6 m up. At most once per 1 s. |
| Triple Tap (`triple-tap`) | epic | ranged | 1 | Every shot is a three-round burst. | **+2 Rounds in each burst**; *-50% Damage per round*; *-40% Attack speed* | Each shot fires 3 rounds .07 s apart for one round's ammo. |
| Mint Condition (`mint-condition`) | legendary | all | 1 | Untouched, you are nearly unkillable. | **-60% Damage taken above 90% health** | Damage taken x.4 while above 90% health, working out the hit from the health before it. |
| All In (`all-in`) | legendary | ranged | 1 | One shot. The whole magazine. | **Every round fires as one**; *+60% Reload time* | A shot spends the whole magazine and deals one round's damage per round spent, at a size that grows with the rounds. It reads as `+` in the row because it is the point; the reload is the cost. |
| Spare Body (`spare-body`) | legendary | all | 1 | Once a round, die and come back. | **Back after 2 s at 50% health**; **+25 Damage blast when you return** | Once a round, on death: down 2 s, then standing on the spot at 50% health with 1 s of immunity and a 25 damage, 4 m blast. A kill credit waits until the round's real end. |
| Time Thief (`time-thief`) | legendary | all | 1 | Hits steal cooldown. | **+.25 s off your Q and E per hit**; **+.25 s onto theirs** | A direct hit on a pilot moves .25 s between QReady/EReady deadlines, both slots. At most 4 hits a second count. |
| Haymaker (`haymaker`) | mythic | all | 1 | Every 12 s your next hit deals six times the damage. | **x6 Damage on the charged hit**; Your weapon glows when it is ready | Every 12 s the next hit deals x6, at most 200 on a pilot. A miss does not spend it. |
| Evil Twin (`evil-twin`) | mythic | all | 1 | A copy of you fights at your side. | **Copies your attacks at 50%**; 60 HP, back after 8 s | A twin stands 3 m to your side, following you; it copies your attacks at 50% from where it stands (no chips), 60 HP, returning 8 s after dying. Bots may target it. |

### Rules contract (Astra)

- Call `NativeWildChips.Apply(content)` beside `NativeBombChips.Apply`. A chip with a rule has `mechanic` equal to its `id` (`wild-...`) and stays out until `SupportedMechanics` has it, so you can land them in any order, a few at a time. Bullet Hose is stats only and can go first.
- **Stack** numbers are per stack where the row says so and `max` above 1. **Fairness** beats the table: if something is unplayable, tune it and write down what you changed.
- **Marks** (`NativeTechniqueMark`, docs/WEAPON-TECHNIQUES.md): `wild-tapeworm`, `wild-anti-gravity`, `wild-hush`, `wild-shrink-ray`, `wild-jet-rounds`, `wild-slow-bleed` (the ticks still to come), on the victim.
- **Procs:** please raise one event for each moment something visible happens, with `Detail` the chip's id, the point, and a direction or second point where it has one: `wild-battering-block`, `wild-blink-block`, `wild-hot-swap`, `wild-spite` (warning, then blast), `wild-splat`, `wild-doomsday-clock`, `wild-head-stomp`, `wild-overkill` (from, to), `wild-domino` (from, to), `wild-flanker`, `wild-bank-error`, `wild-bodyguard-drone` (the shot it took), `wild-spare-body` (down, return), `wild-haymaker` (the hit), `wild-time-thief` (from, to), `wild-radar-block`, `wild-hand-to-mouth`, `wild-blood-magazine`. Tell me the event's name, and whether it can reuse an existing kind.
- **State the look needs** on the pilot's replicated chip state: `wild-planted`, `wild-doomsday-clock` (0 to 1), `wild-spin-up` (0 to 1), `wild-haymaker` (ready), `wild-charged-reply` (armed), `wild-opening-move` (ready), `wild-spare-body` (down, returns at). Whatever is cheapest to carry; a per-pilot `ChipGauge` float by id would do.
- Shot changes (`steering-wheel`, `curveball`, `snowball`, `trick-shot`, `wall-hack`, `bank-error`, `triple-tap`, `all-in`): the shot's own replicated position, size and damage are enough for the presentation.
- Evil Twin, Afterimage and the drone: the twin and the decoy are minions; the drone is only a look, orbiting from `Has`.

## Presentation (Claude)

The card and the copy are done; each chip's icon, its stat rows and its card line are generated. Procs, marks and gauges get their effects once the events exist. The two tiers' mounted weapon modules follow the rarity's generic set until a chip earns its own.
