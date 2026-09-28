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
until they leave. Online grants are not a live moderation/kick system. Logins use
12-hour revocable sessions in sessionStorage, so a closed game requires signing
in again. Recovery rotates its code and revokes existing account sessions. It does
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
| LMB | Left straight, right straight, knee finisher; holding repeats the chain |
| RMB | Burning Hadoken; replaces Block |
| E | Tatsumaki Senpukyaku, spinning multi-hit kick |
| 1 | Shoryuken, forward/rising uppercut |
| Hold/release RMB with Shinku Chip | Charge for 1.4 seconds; release a full charge for Shinku Hadoken |

Normal Hadoken costs 12 stamina; Shinku costs 30. A normal combo costs 12, ten-hit
combo 22, Tatsu 22 and Shoryuken 25, before resource modifiers. Shinku pierces players
but cover stops it. Its follow-up hits remain at the collision point: moving out
of the burst escapes subsequent hits. Blocking can reflect both Hadoken variants;
reflected burn belongs to the blocker. Melee obeys cover, active Block and range.

| Exclusive Chip | Effect |
| --- | --- |
| Shinku Hadoken | Charge and release a piercing five-hit fireball |
| Ten-Hit Rush | Ten attacks ending with a flying knee |
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
straights, knee lift, spin and uppercut use the existing articulated pilot.
Arcade hit sparks have a fixed pool; projectile shells are bounded by the existing
projectile limit. Impact text and synthesized sound give fighting-game feedback.
There is no server-wide hit-stop, and no full-screen flashing effect.

Rain Warrior lasts 7.2 seconds: sky-to-back pan, closer view of the glowing 天 mark,
turn into stance, and final name/text reveal. Rain and ripples use instancing.
The music is an original synthesized rain/arcade sting, not sampled game audio.
Move names and inspiration: [Capcom Ryu move list](https://game.capcom.com/cfn/sfv/character/ryu/movelist?lang=en)
and [Capcom's Shinku Hadoken discussion](https://game.capcom.com/cfn/sfv/column/132046?lang=en).
The mechanics/timings here are custom Chip Arena rules.

## Verification

- 260 automated tests passed, including new account and combat coverage.
- Password KDF output cross-checked against Node's independent scrypt implementation.
- Registration, reserved DEV migration, safe profile persistence, version conflicts,
  unauthorized/forged grants, token tampering/expiry, logout and recovery verified.
- Eight real WebSocket clients saw 69 identical shared snapshots, charged/released
  Shinku, key 1 uppercut, ten-hit chain, and a granted non-DEV account.
- Final intro poses fit 4:3, 16:9 and 21:9 framing in geometry projection checks.
- Original music file, migrations, source package and Windows archive integrity checked.
- Windows startup, GPU rendering, and real Internet latency require local playtesting.
  Browser visual QA was unavailable in this session.
