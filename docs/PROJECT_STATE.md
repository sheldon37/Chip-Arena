# Chip Arena — current state

Migration imported to private GitHub; not merged or released (2026-09-29).

- Destination: `sheldon37/Chip-Arena`, private. Owner pushed the verified baseline
  and migration branch; remote SHAs match the history bundle. Browser audit found
  no publishing hooks, environments or Pages; only the Codex Connector app.
  Ruleset API returns a private-plan restriction; no admin policies changed.
- Source baseline: `18231698c7c79a7a604e567b933eb2d0155d7a3e`, clean `main`,
  68 existing commits; matches latest successfully published Site source (version 64).
  Work branch: `migration/shared-development`; full history retained.
- Runtime: TypeScript/Three.js/Rapier; Node authority; Electron shell
  `desktop/main.cjs`. Launcher 0.3.6; pinned Electron 44.4.5, Node 24.21.0,
  cloudflared 2026.9.3. Migration checks use Node 24.19.0/npm 11.9.0.
- Baseline game build `9b6caf05644a73c2cf5929da`; content `0.2.4cad5cbfa762`.
  Available Windows ZIP contains older content `0.2.44211e218269` with the same
  launcher. The owner's installed, possibly updated, copy is not accessible.
- Existing Site still serves updater downloads, identity, room discovery and relay.
  Its database and endpoints remain unchanged. Production still uses its original
  updater feed; TEST installations use the separate GitHub channel below.
- Settings use `chip-arena-preferences`; desktop uses persistent
  `arena://game` storage and an encrypted account-session file under userData.
- Baseline: typecheck passed; unit tests 406/419 passed (13 failed);
  identical after clean restore. WebSocket rematch/server build remain failing;
  HTTPS integration and Windows ZIP construction passed. Native Windows untested.

- Shared test-development branch: `claude/visual-revision` (owner confirmed
  2026-10-01). Visual revision PR #4 remains separate from the migration to main;
  this branch is already distributed through the test channel.
- Test builds update online from the public test channel
  sheldon37/chip-arena-test-updates (tooling PR #5); live feed untouched. See DEVELOPMENT.md.
- DEV combo-ready holds and Demon Step's 0.20 s i-frame window (PR #6), plus
  individually controlled primary hits (PR #7), merged into the shared branch
  at `66451cd`. One hit per activation, per-hit stamina, 2.4 s continuation memory.
  Included in the current Siren test update below. DEV_LOG records checks and
  the 12 unchanged baseline unit failures.

Next: review the migration PR against `main`; shared policy and CI remain on
`migration/shared-development` until the owner merges. Resolve recorded baseline
failures before enforcing a green merge/release gate. No automatic merge.
Windows validation and desktop distribution cutover remain separate work.

- Siren: PR #8 merged into `claude/visual-revision` at `9f010f5` (2026-10-01),
  combining systems `1a8a5d0`, Claude visuals `2e986f4`, DEV-only `???` category
  and original music. The merge tree matches the tested integration exactly.
  Test content `0.2.149269b37b5b` published from `abb0bbb` at channel `3e9d5d9`;
  existing test installations press Update. Real network upgrade, all 102 files,
  settings preservation and menu status verified. Windows visual/audio acceptance
  and optional voice clips remain. See `docs/SIREN-SYSTEMS.md` and DEV_LOG.
