# Chip Arena controllers

Options / Settings → Controller enables gamepads, chooses Auto / Xbox / PlayStation / Steam button names, and changes look speed, deadzone, inverted Y and swapped sticks. Click an action, release all buttons, then press its replacement. Hold View / Create / Back with the replacement for a chord. A conflicting assignment swaps with the old button. Escape, the Cancel button or a ten-second timeout cancels capture.

Save layout writes the current layout to this PC's existing preferences file. Save Settings / leaving Settings also uses the existing preferences save flow. Restore controller defaults resets controller settings; save to retain them. Keyboard/mouse settings and other preferences are preserved. One custom layout follows the physical button positions across supported devices; changing labels does not change assignments. Menu controls remain fixed so a gameplay remap cannot trap the player in Options.

| Action | Xbox / Steam gamepad | PlayStation |
| --- | --- | --- |
| Move / look | Left / right stick | Left / right stick |
| Attack / fire | RT | R2 |
| Aim / Q / mode | LT | L2 |
| Ability E | RB | R1 |
| Block / parry | LB | L1 |
| Jump / dodge | A / B | Cross / Circle |
| Reload / interact | X / Y | Square / Triangle |
| Sprint (hold) / lock target | Left / right stick click | L3 / R3 |
| Abilities 1–4 | D-pad up, right, down, left | Same |
| Ability 5 | View/Back + D-pad up | Create/Share + D-pad up |
| Chips / Expedition board | View/Back + Y / X | Create/Share + Triangle / Square |
| Camera view | View/Back + right stick click | Create/Share + R3 |
| Pause | Menu | Options |

Menu pointer: left stick moves, A/Cross activates, B/Circle or Menu/Options goes back, right stick scrolls the form. Sliders respond to holding confirm and moving the pointer. Account names, passwords and other text fields still use keyboard entry (or Steam's on-screen keyboard).

Uses Unity Input System 1.17.0's standardized Gamepad mappings, with the legacy backend retained for existing keyboard/mouse input. Xbox and supported PlayStation gamepads connect through Unity's native device support. Steam Controller / Steam Deck require Steam Input's **Gamepad** template; right pad should output right stick. Select Steam names manually when emulation reports an Xbox device. Native Steam action sets, independent rear paddles, gyro aiming and rumble are outside this implementation. One active gamepad controls this client's local pilot; this does not add local split-screen multiplayer.

Disconnect, focus loss, loading and menu transitions release controller actions and require buttons to return to neutral. Chords latch on the physical press, preventing a held chord from firing its base action when the modifier is released. Input maps into the existing InputFrame; no damage, timing, movement rules, network schema or account data change.

## Validation and Windows acceptance

Managed `CoreTests --controller` covers defaults, conflicting remaps, JSON round trips, invalid settings, deadzones, held/edge/release behavior, reconnect neutrality and modifier-release safety. The native builder gate additionally exercises Unity JsonUtility legacy migration and synthetic Input System button/stick input. The semantic compile gate downloads and checksums the pinned Input System source package and compiles its real API before the game assemblies.

Physical Xbox USB/Bluetooth, DualShock/DualSense USB/Bluetooth and Steam Input playtests remain required. Check Options entry, click/scroll/slider behavior at different UI scales, remap/save/restart/reset, unplug while holding fire/E, focus loss, and keyboard switching. Then test rifle scope, melee lock/cycle/hold-to-unlock, held-E release, all five Cyber Lord abilities, Siren mode, Expedition chips/interact/board, and host/guest input. No hardware or rendered-menu acceptance is claimed from compilation alone.
