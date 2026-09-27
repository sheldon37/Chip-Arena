# Windows launcher and animation update — 2026-09-27

## Portable Windows package

Extract `Chip-Arena-Windows.zip`, then double-click `Start-Chip-Arena.bat`. It includes the official Node.js 24.21.0 Windows x64 runtime, prebuilt server, browser client, audio, schema migrations, editable source ZIP and licenses. No npm install/build/terminal commands or game account are required on the laptop. Windows 10/11 x64 is the target; an ARM-native release is not included.

`Start-Online.bat` additionally obtains a temporary Cloudflare Quick Tunnel HTTPS URL. The host and guest both open that URL, then use the existing room code/invite controls. The connector downloads once from the pinned official 2026.9.3 release and its SHA256 is verified before execution; subsequent launches reuse the verified file. The game server disables gameplay debug rooms regardless of client requests. Close the window or press Ctrl+C to stop the game and tunnel. Nothing is installed as a service. Local/LAN play does not require the connector.

The usual published Site does not automatically discover a server on the user's laptop. Both players must use the launcher's local/LAN or temporary online game URL. These rooms are separate from the Site's HTTPS fallback rooms. A temporary tunnel is for testing, with no uptime guarantee. Public internet tunnel operation and the Windows batch files were not executed on Windows in this environment.

Source and checksums:
- Node: https://nodejs.org/dist/v24.21.0/SHASUMS256.txt
- Runtime archive `node-v24.21.0-win-x64.zip`: `158f7685b44de51f6c0df1d153526cbcd3e1bc739a8dfc607721cef75de9e541`
- Connector: https://github.com/cloudflare/cloudflared/releases/tag/2026.9.3
- Connector `cloudflared-windows-amd64.exe`: `f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2`
- Temporary tunnel documentation: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/trycloudflare/

Rebuild on a development machine:

```sh
npm ci
npm run build
# Download the official pinned runtime ZIP, then set these paths for your machine:
CHIP_NODE_ZIP=/path/node-v24.21.0-win-x64.zip CHIP_RELEASE_DIR=/path/release npm run package:windows
CHIP_BUNDLE_DIR=/path/release/Chip-Arena-Windows node tests/run-portable.mjs
```

Packaging verifies the runtime checksum and bundles dependencies into `server.mjs`. Runtime launch code is `launcher/start.mjs`; Windows entry files and plain-language instructions are in `launcher/`. No secrets, production database, or external project assets are packaged.

## Character animation

`src/client/character-animation.ts` contains the shared presentation pose sampler and tuning constants. The rig has shoulder/elbow and hip/knee joints, torso/head motion and a named `weapon-socket` under the right hand. All weapons remain automatically attached; no manual per-frame alignment.

- Velocity-driven strides blend idle, walking/running and dash lean, with bent knees and airborne leg tuck.
- Guns brace both arms, recoil on authoritative shot timestamps (including secondary burst shots), and move through reload.
- Magic raises and extends the casting hand with an off-hand gesture and small colored motes.
- Melee winds up and swings through the authoritative attack window, scaled by attack rate. Greatsword, dagger and shield poses vary; reach still scales the visible weapon/trail.
- First person uses the same attack pose phases for swing, recoil, reload and spell follow-through. A dead first-person player switches to the third-person view to see the breakup.

These are procedural prototype animations, not imported motion-capture clips or inverse-kinematic foot placement. Magic release remains immediate under the existing combat rules; its animation is a release/follow-through, not a newly added gameplay windup.

## Knockout presentation and future kill effects

The authority emits one persistent `DeathPresentation` record per killed player: ID, time, effect identifier, seed, position, yaw, launch direction, inherited velocity, damage kind and attributed source. Late/repeated snapshots therefore do not restart the effect. Records reset with the round.

`src/client/death-effects.ts` owns a `KILL_EFFECTS` factory registry. The current `voxel-burst` implementation samples the actual posed rig's boxes/colors, then animates bounded, seeded fragments with a simple visual floor bounce and fade. Unknown effect IDs fall back to voxel breakup. Future kill animations can add a factory and an authoritative effect-selection policy without changing match progression. No custom effect selection menu/content is implemented yet.

Death fragments are strictly cosmetic (100 maximum per pilot, 40 with reduced effects); they create no colliders, mass or damage. Existing gameplay destruction/debris remains authoritative and damaging during combat. Cosmetic bounce uses a simplified floor, so visual fragments can pass through cover.

Every resolved round—including the final match-winning round—enters `result` for `CONFIG.resultSeconds = 5`. The winner/score lock immediately; new damage and attacks stop. Existing physics props settle while the unobtrusive result banner leaves the arena visible. Draft/reset/match controls appear only after the delay. Draws also hold, then replay without awarding a win/Chip. Disconnect forfeits still resolve at their existing timeout. Rooms remain 1v1; future larger matches need roster/team changes, but a death alone is not the presentation trigger when more than one survivor remains.

Developer Sandbox adds **Preview Knockout** and **Preview My Death**. Both go through the authoritative damage/result path and are rejected in normal network rooms. Restore/Arena Reset after a completed outcome clears sandbox counters so repeated previews cannot inflate set wins.
