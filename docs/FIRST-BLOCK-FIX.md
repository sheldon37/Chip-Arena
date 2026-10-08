# First-block effects

The reported Supernova Guard issue was checked against actual swept projectiles.
The successful-block hook already executed for bullets. At 2 m, one Supernova
copy dealt approximately 18.53 player damage after distance falloff; shooters
outside its local blast radius took no pulse damage. The blast had no dedicated
rendered shockwave, which made successful activations hard to distinguish.

Changes:

- Supernova Guard now sends its chip identity with the authoritative pulse event
  and renders three gold shockwave rings plus a white core, with a stronger blast
  sound. Counter Pulse receives three teal shockwave rings.
- First-block chip activations show a short local confirmation naming the
  activated chips. The event is emitted once per successful block activation,
  never just for pressing Block, and is shared in multiplayer snapshots.
- Block pulse checks against enemy summons now exclude the blocker's own
  collider from the line-of-sight ray. Actual intervening cover still blocks it.
- Supernova's description explicitly says it triggers from bullets/spells and
  explodes around the blocker, with distance falloff and cover.

Verification: 19 focused tests pass, including all nine first-block chips
against pistol, rifle, machine-gun and fire projectiles; once-per-activation
behavior and reactivation; outside-range behavior; summon damage and cover;
shockwave geometry and cleanup; existing reflection/cooldown/element guards.
A local two-client WebSocket match verified identical snapshots for all nine
activated chips, one Supernova pulse, reflection, three mines, one time bubble
and one zombie. This is a local networking test, not a public internet test.

No launcher, runtime or update format changes are needed. Restart the existing
auto-updating launcher, or run Update-Chip-Arena.bat while the server is closed.
