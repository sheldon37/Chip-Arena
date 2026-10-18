# Movement chips and Mods (schema 1)

Settings → Mods contains built-in gameplay modifiers and external chip packs.
Choose before creating/joining a room, then restart. The active fingerprint is
shown separately from the saved selection. Everyone must enable the same rules
and have the exact same selected JSON files. The host chooses the intended setup;
clients configure it locally. Mismatches are refused before a seat is admitted.
Host promotion works only between matching installations. These are process-wide
settings for player-hosted and solo games, not mid-match switches. Dedicated
servers remain vanilla in this first release.

## Built-in chips

| Chip | Tier | Effect per copy | Max copies |
|---|---|---|---:|
| Stride Circuit | Common | ×1.08 movement speed | 5 |
| Momentum Cell | Uncommon | ×1.10 speed, ×1.15 stamina regeneration | 3 |
| Air Step | Rare | +1 mid-air jump | 3 |
| Acrobat | Epic | +1 mid-air jump, ×1.12 dodge distance | 1 |
| Skybound | Legendary | +2 mid-air jumps, ×0.85 gravity | 1 |

No health penalty on these speed chips. Multipliers stack multiplicatively:
five Stride Circuits give 1.08^5 = 1.4693, about +46.9% speed before other
chips. The existing final speed cap stays ×2.5. Air jumps add to the natural
one on daggers, bow and Cyber Lord. All weapons can gain air jumps. Maximum
is six mid-air jumps, plus the grounded jump. Landing restores the budget;
held jump does not spend repeated charges. Swimming and action locks retain
their existing rules. Each air jump replaces vertical speed with 90% of the
chip-scaled grounded jump speed. Existing jump animations are reused.

## Optional party rules

- Airborne: +2 starting air jumps for every pilot, including bots.
- Overclocked: ×1.25 starting movement speed for every pilot.
- Pinball: ×2 outgoing impulse and ×1.5 knockback taken for every pilot.

Rules compose with chips and existing stat caps. NPC minions and environmental
physics are not globally rescaled. All rules default off.

## External packs

Use **Open Mods folder** in Settings. It is in the persistent player-data
folder, outside versioned update files. **Create example chip pack** writes a
new disabled example without overwriting an existing file. Enable it and restart.
Share the file manually with friends. The loader never downloads or executes
code, DLLs, scripts, asset bundles or URLs.

Schema 1 adds chips using existing effects; it does not implement arbitrary new
abilities, maps, weapons or art. Packs are not automatically trusted from a room.
UI polish and custom chip visuals remain Claude's presentation work.

```json
{
  "schema": 1,
  "id": "example-mobility",
  "name": "Example Mobility",
  "version": "1.0.0",
  "requires": [],
  "chips": [{
    "id": "hop-drive",
    "name": "Hop Drive",
    "description": "+1 extra mid-air jump and +8% movement speed.",
    "rarity": "rare",
    "max": 2,
    "tags": ["all"],
    "effects": [
      {"stat": "extraAirJumps", "op": "add", "value": 1},
      {"stat": "speed", "op": "mul", "value": 1.08}
    ]
  }]
}
```

Pack and local chip IDs: 2–24 lowercase ASCII letters/digits/hyphens, starting
with a letter. Runtime chip IDs become `pack-id:chip-id`. Versions are three
numeric components. `requires` may name exact selected dependencies such as
`another-pack@1.0.0`. Duplicate pack IDs, missing dependencies, unsupported
schemas, invalid effects and ID collisions reject the entire active selection.
Invalid selections block hosting/joining/starting a normal game; disable the
problem pack in Settings and restart. The unmodified built-in content remains
available to the menu.

Limits: 16 enabled packs, 64 external chips total, 128 KiB per JSON file;
1–10 copies per chip, 1–6 effects. Ordinary rarities only. Tags: `all`, `melee`,
`ranged`, `magic`, `projectile`, `roll`. Supported stats:
`extraAirJumps`, `speed`, `jumpPower`, `playerGravity`, `rollDistance`,
`rollSpeed`, `rollCost`, `staminaRegen`, `maxStamina`, `dashCooldown`, `bounces`,
`impulse`, `projectileSpeed`, `projectileSize`, `attackRate`, `damage`, `hp`,
`knockbackTaken`.

Operations: `mul` with 0.25–3, or `add` with 0–100. `extraAirJumps` and `bounces`
only accept integer `add` values 0–6. No `set`, scripted `mechanic`, exclusions
or exclusive/DEV rarity. Custom icons are ignored; packs use a built-in icon.
Stats accumulate in deterministic sorted pack order, bounded to ±1,000,000
before existing per-stat clamps to prevent overflow. Gravity is 0.1–3 and jump
power 0.1–2.5. The compatibility digest includes schema/rules plus pack ID,
version and full decoded UTF-8 JSON text hash. Even whitespace edits change the
digest. Filenames do not affect compatibility; file contents do.

Changing any effect implementation requires a schema/revision bump. Save files
store the selected filenames/rules; replay/network compatibility uses the final
content revision. Do not enable or replace packs during a running session and
expect them to reload; restart after changes.

## Bundled Cards+ adaptations and author credit

Settings > Mods now has an optional 20-chip Cards+ adaptation pack, credited to original concept author willis81808. Restart required; same selection on all peers. See [ROUNDS-ADAPTATIONS.md](ROUNDS-ADAPTATIONS.md) for each original/adapted card and deliberate changes. Full MIT notice ships with the game.

External packs may add optional `author` at pack level or on each chip (chip wins). Up to 48 plain-text characters, no control characters or angle brackets. A chip may also supply `sourceCard`; `sourcePack` is assigned from the pack display name. Author appears in its own bottom-left footer row. Legacy packs without attribution remain valid.

## Additional bundled adaptations

Eight optional pack toggles add 118 reviewed adaptations. See [ROUNDS-MORE-PACKS.md](ROUNDS-MORE-PACKS.md) for exact scope and differences. Partial packs are labelled. Saved `roundsPacks` defaults to empty; the existing Cards+ toggle remains compatible. The `rounds-` pack-ID prefix is reserved. Invalid bundled selections block network play until disabled and restarted.
