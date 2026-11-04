# The REVIVE-O-MATIC: the hub's revive machine

Owner request, 2026-11-04: "Lets add a revive machine in the hub to revive players that have died (Even if they are dead in an instance). You will press E to interact with the machine, the screen will show the dead players,
location of their body, and cause of death. You can select the player to revive and it will be a fixed cost of 250$. It will comically spit them out of the machine when revived with full hp. Machine will be a highly detailed
design with cool effects (comical design for spitting players out)."

Status: presentation by Claude, reviewed in Unity through the Dev Sandbox preview. **In a real run today the machine stands in the hub and its screen lists who is down, but pressing REVIVE does nothing yet: it says so.** Whether a revive is
allowed, the $250, restoring the pilot across hub and instance, replicating it, and recording cause of death and the body's spot are Astra's (AGENTS.md role split): the contract is below.

## The machine

A pot-bellied riveted boiler on four coil springs that happens to have a face. Two pressure gauges for eyes, steel brows, a red rubber bulb horn for a nose, bellows for cheeks, and a mouth: a snout with a chrome-toothed grille that says SAY AHH
and NEXT PATIENT. A flywheel and side rod on its left, a smokestack whose cap comes off, a marquee on top (REVIVE-O-MATIC, $250 NO REFUNDS, chasing bulbs, a red cross). Beside it, a battered console with a green CRT, a big red button, a lever, and the
glass tube your money goes up; a safety board that says DAYS SINCE LAST INCIDENT 0. In front of it, a painted lane and a blue gym crash mat with a bullseye. On the chin, WARRANTY VOID IF DEAD.

## The show

Seconds from the money landing (`Core/NativeReviveMachine.cs`, every moment a pure function of that clock, so a late joiner draws the same machine as everyone):

| t | What happens |
| --- | --- |
| 0 | The lever throws itself. Coins whoosh up the glass tube and clatter into the hopper. The nose honks. Its eyes snap to attention |
| .3 | The flywheel starts turning, the side rod and piston with it, the motor climbing |
| .55 to 2.4 | It shakes, more and more, on its springs. Brows lower. Needles climb into the red and rattle. Steam jets from the ears, the stack belches, the marquee dims as it drinks the power, sparks fly off the wheel's brake, the cheeks swell |
| 2.42 | **It holds its breath.** The motor cuts dead. It rears back on its rear springs, cheeks full, steam gone. A glug. One honk |
| 3 | **PTOOEY.** Jaw and lip burst open, the furnace roars out of its throat, the tongue unrolls like a party blower, the machine lunges forward. A ring of steam, sparks, confetti, the stack's cap flying up on its spring in a ball of soot, the comic word over its mouth, bolts pinging off. The pilot leaves on a slide whistle |
| 3 to 3.85 | The pilot flies, tumbling two and a quarter turns, trailing steam |
| 3.85, 4.31 | Splat on the mat, which dents; a bounce and one more turn; splat. Rings of dust |
| 4.31 to 4.73 | They skid to a stop on their back |
| 4.91 to 5.63 | They roll and get up, with stars round their head for a moment |
| 5.5 to 5.95 | The tongue slurps back, the doors slam, a final clang, a ding |
| 6.6 | Over |

## Presentation (Claude)

| Piece | File |
| --- | --- |
| The record, the timeline, the spots, the preview | `Core/NativeReviveMachine.cs` |
| The models, sculpted in code like the other machines (65k triangles, all of it; no licensed assets or bank) | `Core/NativeReviveMachineModels.cs` |
| Steam, sparks, confetti, soot, dust, the trail, the stars | `Core/NativeReviveMachineFx.cs` |
| The sound: original, synthesized as the Deployment Station's is | `Core/NativeReviveMachineSounds.cs` |
| The draw, the pilot's drawn flight, their camera, the collider | `Runtime/NativeReviveMachinePresentation.cs` |
| The E prompt, the screen, the comic word, the sound's playback | `Runtime/NativeReviveMachineScreen.cs` |
| Checks | `Tests/ReviveMachineChecks.cs` (`CoreTests --revive-machine`) |

To see it: Dev Sandbox, DEV panel, **Preview Revive Machine (E at it)**: it stands in front of you with three stand-in dead; walk up and press E. **Run Revive Machine by itself** runs through every state of the screen: three dead and paying, again, too poor, nobody dead.
Offline captures: `gallery=T,ReviveMachineAuto`.

**The screen** (E, or the controller's interact, at the console): marquee header and your wallet; a row for each dead pilot with where the body is and what killed them; REVIVE · $250 on each. Built from the menus' kit, so the controller's focus moves between the buttons and A presses them, B or E or Escape closes.
Its states: nobody dead (a bored face), YOU'RE $n SHORT, THEY HAVE TO BE CONNECTED, blocked outright (PERMADEATH), and NOT CONNECTED YET while the rules aren't bound. A cause or place the rules haven't reported reads "Not reported yet", never an invented one.

**Key.** E as the owner asked. It can't clash with the E ability where this stands: the sanctuary holsters weapons and abilities, and opening the screen takes the press before play sees it. Everything else in the game interacts on F; the machine doesn't take F, so the old reprint still answers there (below).

**Animation impact.** The pilot's flight is the drawn rig's root only. Hit timing, hitboxes, damage, cooldowns and movement are unchanged; the capsule stands on the landing throughout. The hub gains one thing simulation can feel: a box collider for the machine and one for the console, on host and guests alike
(`NativeArena.ReviveMachineCollision`). Navigation is untouched: no zombie comes in here.

## Rules contract (Astra)

`NativeReviveMachine.Board` is what the presentation reads. It builds one each frame from the live view (`ConversionGame.ReviveMachineLiveBoard`): the dead from `ExpeditionView.members`, the wallet, PERMADEATH. `NativeReviveMachinePreview` in `Core/NativeReviveMachine.cs` writes the same record the way the host would, so it is the worked example. Three hooks, none of which touch the presentation:

**1. `ConversionGame.ReviveMachineRequest`**, `Func<string,string>`: called with the dead pilot's account when the viewer presses REVIVE. Return null when the request went off, or why not (shown on the screen). Unbound, the screen shows NOT CONNECTED YET. Send it to the host however suits: `ExpeditionIntent.Reprint` with the target already exists, and so do the evidence and the guest channel.

**2. `NativeReviveMachine.LiveSpits`**, `NativeReviveSpit[]`: every revive in progress, replicated, on the **arena clock of the hub the machine stands in**. This is what starts the machine on every peer.
- `Id` once per revive. `Seat` the pilot (`Actor.Id`). `Account`. `At` when the money landed. `Land` where the capsule ends up (world, feet).
- **Machine, one at a time.** Refuse (or queue) a revive while `NativeReviveMachine.Current` is running: two spits overlap badly. Drop an entry after `At + NativeReviveMachine.End`.
- **At `At + NativeReviveMachine.Spit` (3 s)**: the pilot is alive at **full HP** (today's `RevivePve` restores half), standing at `Land`. **Input held until `At + NativeReviveMachine.HoldUntil` (5.63 s)**. The presentation flies the drawn body there from the mouth, draws the knockdown's poses over it and hides the pilot until the spit.
- `Land`: the mat's centre is `NativeReviveMachine.DefaultLand` in the machine's frame (`Frame(at,yaw)`, `Spot(mapId,...)`). Keep it on the mat, a pilot-width from anyone standing there.
- `RevivedAt` as usual is fine: the holographic revive stands aside while a spit is running (`IsReviving`).
- A dead pilot in another area: your `SplitReprint`/`ReprintFrom` already moves them, cancelling the corpse. Their client will load in late and join the show wherever it has got to; nothing in it depends on them having seen the start.

**3. `NativeReviveMachine.Fill`**, `Action<NativeReviveBoard,ExpeditionView>`: called once a frame after the board is built from the view. Add what isn't on the wire yet to each row (`NativeReviveDead`):
- `Cause`: `ExpeditionViewMember` has no death on it. Replicate what the authority already knows when it records the death (the enemy's kind if an add did it, `NativeDeath.damageKind`, the killer's name if a pilot did), and set `Cause=NativeReviveMachine.CauseLine(kind,killer,damage)`: the wording is here, with a line for every enemy kind.
- `Body` and `Located` the body's point in its area, `Place` a room or street if you have one, `DiedAt`. `Area` and `AreaName` already come from `member.area`.
- `Blocked` for the whole list if something else forbids it (out of the sanctuary, say). `Cost`: leave it 250.

**Rules to change.**
- `reprintCost` 250. `BeginExpeditionReprint` picks the first dead member and channels for 3 s at an anchor with an interaction: this replaces that with a chosen target and no channel, charged once, exactly as `Reprint(payer,victim,cost,remote:true)` does now. It needs the payer in the sanctuary: the spot is inside it.
- **The generated hub has no machine anchor**, so `Expedition.CanPurchase`, `MachineEvidence` and F have nothing to find there; the authored town does (`exp-city:machine:0`). The machine stands at `NativeReviveMachine.Spot`, its use point `UsePoint` within `UseRange`: use those, add an anchor, or check the payer's distance however you like. F at the authored town's anchor still runs the old reprint until you retire it.
- Check the payer is at the machine: the prompt's range is presentation; the host must measure it.
- `second-wind` (reprints free) and `permadeath` (none): the screen already says PERMADEATH. If second-wind stays free, set `Cost` to 0 in `Fill`; the button reads the board.

### What is not live until then

| Live today | Waits on Astra |
| --- | --- |
| The machine, its idle, its collider, in the generated hub and the authored town | Anything happening when REVIVE is pressed |
| E opens the screen; the list of the dead, their areas and connection, your wallet; the can't-afford and PERMADEATH states | The charge, the revive, full HP, leaving the instance |
| The whole show, sound and flight, in the Dev Sandbox preview | The show in a real run (needs `LiveSpits`) |
| | Cause of death, body position, place (rows read "Not reported yet") |

## Questions for the owner

- A pilot **in a run** paying to revive a teammate from the hub: charged the same $250 as anyone, with the existing wallets. Assumed yes. **PERMADEATH** switches the machine off, as it does reprints: confirm that's wanted.
- **Second Wind** makes reprints free today. Should the machine be free under it too, or always $250 ("fixed cost")? Contract leaves it to the rules; I'd keep it free, since that's what the mutator's card promises.
- The machine stands in the sanctuary only, so nobody can be revived from inside an instance. I read "in the hub" that way.
- F opens nothing here: it asks for E, as you said, though every other machine in the game is F. Say if you'd rather it answered to both.
