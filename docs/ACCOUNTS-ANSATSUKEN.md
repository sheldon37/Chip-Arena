# Accounts and Ansatsuken

Home → Log in / Register offers username/password registration, login and recovery.
Save the one-time recovery code. There is no email recovery provider. Guest play
remains available; local guest play needs no account service. Customize → Save Pilot
writes the preferred class, body/accent colors, name, hat, intro, intro text and
kill effect. Account → Reload saved pilot restores the saved version on another PC.
Device graphics/audio settings remain local.

The existing configured DEV credentials bootstrap the reserved DEV username on its
first account login. After migration the legacy DEV login endpoint requires a game
update, so the old verifier cannot bypass a recovered password. Previously issued
legacy DEV tokens keep their original eight-hour expiry. A random account cannot claim this name through registration.
Home → DEV Menu searches registered usernames and grants/revokes individual weapons,
intros, kill effects and the Playtester/VIP flags. Every grant write is audited.
Granting a weapon does not grant a DEV role. DEV role creation is not exposed in
the menu or API. L33T grants include its exclusive L44T Chip pool; Ansatsuken grants
include its arcade pool. Intros can be granted independently.

After a grant/revoke, reload the account and rejoin the room. The account service
issues signed gameplay claims valid for 15 minutes, refreshed every five minutes.
The host verifies claims at room entry; existing seats retain their permissions
until they leave. Online grants are not a live moderation/kick system. Remember me is enabled by default and issues a revocable 30-day session.
Without it, sessions expire after 12 hours. Browsers store remembered sessions
per origin; desktop 0.3.4 stores its session with OS-backed encryption, independent
of changing host ports or invite URLs. The desktop account broker contacts only
the fixed HTTPS account origin and never sends the long-lived credential into
a game renderer. Logout forgets it even when offline. No password is saved.
Native DEV grant management is available in a game you host or the official Site,
not in a remote host page. Install the desktop patch once for this feature; the
game-content updater does not replace desktop application code. Recovery rotates its code and revokes existing account sessions. It does
not terminate an already-running match seat.

## Data and authorization

`/api/accounts/*` is served by the existing central Site Worker and isolated D1
binding. Native game hosts do not store account records or receive passwords or
account session tokens. Clients send credential requests directly to the fixed
HTTPS account origin, with redirects disabled. A host receives only a short-lived,
ECDSA P-256 signed claim containing account ID, DEV status, item grants and flags.
The same public key verification is used by native WebSocket hosts and browser
host workers. This retains the game's existing trusted-host model: a modified
host/client can change its own game, but cannot grant central account privileges.

Passwords use unique 128-bit salts and scrypt (N=32768, r=8, p=3, 32 MiB), using
pinned `@noble/hashes` 2.0.1. Hash concurrency is capped at two per Worker isolate.
Session and recovery tokens use 256 bits of cryptographic randomness; only their
SHA-256 digests are stored. Authentication is rate limited by IP and normalized
username. Signed-in API requests have independent per-IP/session limits. The API
uses bearer tokens, no cookies or ambient credentials; CORS permits game origins
without credentialed cookies. JSON bodies are bounded. SQL is parameterized.
Saved profiles and grants use revision checks to prevent silent overwrites.
D1 migration 0004 appends account, session and audit tables, preserving room data.

References: [OWASP password storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
and [noble-hashes](https://github.com/paulmillr/noble-hashes).

## Ansatsuken controls

| Input | Move |
| --- | --- |
| LMB | Right straight, left straight, high-kick finisher; holding repeats the chain |
| RMB | Tap to parry: no cooldown, blue flash and brief invulnerability on success |
| 1 | Burning Hadoken |
| E | Tatsumaki Senpukyaku, spinning multi-hit kick |
| 2 | Shoryuken, forward/rising uppercut |
| Hold/release 1 with Shinku Chip | Charge for 1.4 seconds; release a full charge for Shinku Hadoken |

Normal Hadoken costs 12 stamina; Shinku costs 30. A normal combo costs 12, ten-hit
combo 22, Tatsu 22 and Shoryuken 25, before resource modifiers. Shinku pierces players
but cover stops it. Its follow-up hits remain at the collision point: moving out
of the burst escapes subsequent hits. Blocking can reflect both Hadoken variants;
reflected burn belongs to the blocker. Melee obeys cover, active Block and range.

| Exclusive Chip | Effect |
| --- | --- |
| Shinku Hadoken | Charge and release a piercing five-hit fireball |
| Ten-Hit Rush | Ten attacks ending with a high kick |
| Shakunetsu | Crimson Hadokens with much stronger burn |
| Hurricane Tatsu | Longer spin, extra kicks and an inward pull |
| Shin Shoryuken | Three uppercut hits and cover-shattering finisher |
| Special Cancel | Cancel landed combos into specials; shorter special cooldowns |
| Focus Armor | Reduced damage while charging |
| Perfect Rhythm | Finishers restore stamina and some health |
| Air Juggle | Increased damage against airborne targets |
| Afterimage Assault | Delayed location-based echoes of punches |
| Counter Hit | Extra damage and knockback during an enemy attack |
| Round Two | Survive one lethal hit per round at 1 HP and recover stamina |

The guarded idle is based on the supplied stance GIF. Red gloves, alternating
straights, high kick, spin and uppercut use the existing articulated pilot.
Arcade hit sparks have a fixed pool; projectile shells are bounded by the existing
projectile limit. Impact text and synthesized sound give fighting-game feedback.
There is no server-wide hit-stop, and no full-screen flashing effect.

Rain Warrior lasts 7.2 seconds: sky-to-back pan, closer view of the glowing 天 mark,
turn into stance, and final name/text reveal. Rain and ripples use instancing.
The music is an original synthesized rain/arcade sting, not sampled game audio.
Move names and inspiration: [Capcom Ryu move list](https://game.capcom.com/cfn/sfv/character/ryu/movelist?lang=en)
and [Capcom's Shinku Hadoken discussion](https://game.capcom.com/cfn/sfv/column/132046?lang=en).
The mechanics/timings here are custom Chip Arena rules.

## Parry and hit feedback update

A parry uses the existing 0.30-second timed Block window (Block-window Chips
can extend it), with zero cooldown even on a miss. Release and tap RMB again to
rearm; holding does not auto-repeat. A successful parry keeps projectile reflection,
melee recoil and compatible first-block procs, clears attack recovery and grants
0.10 seconds / six simulation ticks of damage immunity. Falling and the scripted
L44T scare kill still bypass it. The blue character flash lasts 0.22 seconds.
Hadoken now has its own cooldown independent of the parry.

Confirmed strikes interrupt the target's attacks, block, movement, jump and dash.
They preserve physical knockback. At base attack rate, normal punches stun for
0.30 seconds, rapid-chain punches 0.15, Tatsu kicks 0.22 and finishers 0.38.
Hadoken stuns for 0.26 seconds; Shinku's repeated impacts keep shorter 0.14–0.15
windows. Attack-rate modifiers shorten these with a 0.09-second floor. Authority
and client movement prediction share the lock; remote snapshots include it.
The counter tracks confirmed hits/damage on one opponent across attacks, resets
when the target changes or after a 1.1-second gap, and excludes blocked/missed hits.
Punches, kicks, finishers and parries use four original PCM sounds; no game audio
was extracted. The open-palm pose uses the supplied parry reference.

Each Ansatsuken draft guarantees two exclusive Chips if enough remain. Each
remaining slot has an 85% exclusive-pool roll, then the ordinary compatible roll.
Choices stay unique and exhausted exclusive pools fall back to normal Chips.
Normal weapons retain their previous odds; L33T retains its own pool. Block
cooldown reduction is excluded because the parry is already cooldown-free.

Visual reference: [Capcom's parry effects discussion](https://www.streetfighter.com/6/column/detail/effects003)
and [official Fighting Ground guide](https://www.streetfighter.com/6/en-us/mode/fightingground).
These are custom DEV timings, not a claim to reproduce the current SF6 balance.
Desktop storage follows [Electron safeStorage](https://www.electronjs.org/docs/latest/api/safe-storage).

## Verification

- 269 automated tests passed, covering accounts, combat, movement, audio
  and desktop transport.
- Password KDF output cross-checked against Node's independent scrypt implementation.
- Registration, reserved DEV migration, safe profile persistence, version conflicts,
  unauthorized/forged grants, token tampering/expiry, logout and recovery verified.
- Eight real WebSocket clients saw 100 identical shared snapshots, charged/released
  Shinku, key 2 uppercut, ten-hit chain, parry flash and authoritative hit stun.
- Native session tests restart the broker, verify encrypted persistence, token
  isolation, sender checks and logout. Windows DPAPI needs native playtesting.
- Final intro poses fit 4:3, 16:9 and 21:9 framing in geometry projection checks.
- Original music file, migrations, source package and Windows archive integrity checked.
- Windows startup, GPU rendering, and real Internet latency require local playtesting.
  Browser visual QA was unavailable in this session.

## Reference animation and presentation update

The supplied animated references are used for pose and timing guidance. The
original game rig performs a right straight, left straight and high kick;
Shoryuken coils low, raises the fist and tucked knee, and turns once in flight.
Its final confirmed hit applies authority-owned knockdown. Movement, attack,
block and dash remain locked through landing and a 0.9-second get-up; prediction
uses the same timestamps. Blocked hits do not knock down. Reset/heal clear it.

Every `arcade-*` Chip has rarity `ansatsuken`. The 85% exclusive pool rule and two
guaranteed offers remain. Red ink, headband ribbons and ki rings decorate the
card. Selection uses a shared 4.8-second slash / seal / card-reveal timeline.
The host waits for that cinematic before moving to the next player.

`effectiveIntro` resolves weapon grants before the saved intro: L33T uses the
throne and Ansatsuken uses Rain Warrior, including authority duration, camera,
name timing and music. The saved preference stays intact and Skip Intros applies.

`hat:auditor-halo` and `effect:dev-raging-demon` are individually grantable DEV
items. Normalization rejects ungranted selections. The halo uses three broken
red arcs and twelve animated black flames. Raging Demon snapshots the attacker's
appearance at the knockout and batches up to three silhouettes. Only the victim
gets the blackout and seal; observers see the approach and flat body. The effect
is cosmetic and does not move, damage, or take control of the attacker.
