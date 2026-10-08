# Unity conversion review — 2026-10-08

Reviewed: `codex/unity-conversion` at `a8fa796` (Unity 6000.3.24f1), against the original
Electron game at `05950ea`. Reviewer: Claude, at the owner's request. Draft PR #59
(`codex/unity-build-fix`) touches none of the findings below.

The conversion covers nearly the whole game, and its checks against the original are careful.
It is not ready to play online yet:

- three separate defects each stop a networked match before it starts;
- sign-in and DEV unlocks will very likely fail in the built game;
- several combat rules behave differently from the current game.

Most of these are contained fixes. Nothing in the project has run in Unity yet, so the owner's
first Windows build is the real test.

**Tally:** 146 confirmed findings out of 156 raised; 10 were disproved. By severity: 3 critical,
14 high, 70 medium and 59 low. Some defects were found from two angles, so the 3 critical
findings are 2 distinct problems and the 14 high are about 9.

## How this was checked

- Eight reviewers each read one area of the C# against the pinned original source, and ran
  small probes where they could.
- A separate verifier tried to disprove every finding. A final pass looked for gaps.
- `tools/check_relay.py` was re-run here (.NET 8): the 35 managed checks, the source-parity
  fixtures and the real loopback relay all pass, with zero compiler warnings.
- No Unity Editor was available. Four findings rest on documented Unity behaviour that could not
  be run here:
  - JsonUtility and nulls;
  - ECDsa on Mono;
  - main-thread async resumption;
  - Rigidbody-to-Transform syncing.

  A few-line Editor test confirms each one before anyone fixes it.
- Evidence sometimes names a probe script (`review-u/...`). Those were scratch files in the
  reviewer's session and are not in the repository; the evidence quotes their output.

## What already looks good

- **Coverage.** All 15 weapons, 236 Chips, 15 maps, Ward Zero, the DEV characters, menus,
  accounts and replays are ported.
- **Method.** Thousands of samples compare the C# with outputs of the original TypeScript
  functions.
- **Repository.** No licensed animation-pack files, secrets or tokens are committed.
- **Docs.** The status docs say plainly what has not been run.

## Fix first

1. **Every guest is disconnected when a match starts** (critical; [#1](#f1), [#3](#f3)).
   - Cause: the host sends match frames through Unity's JsonUtility, which cannot send null for
     a nested object. The guest receives empty placeholder objects instead, such as a map block
     with no material or a death with zero radius.
   - Effect: the guest's `Valid()` check rejects the frame and drops the connection. This
     happens on every map, LAN and relay alike.
   - Fix: send explicit presence flags, or use a serializer that keeps nulls. Add an Editor test
     that round-trips a real frame.
2. **Network message rate is tied to the frame rate** (critical; [#2](#f2), [#16](#f16)).
   - Cause: the socket loops resume on Unity's main-thread queue, which runs once per frame, and
     each message takes two of those steps.
   - Effect: a guest below about 120 fps fills its 32-message send queue within 1–3 seconds and
     disconnects itself. A 60 fps dedicated host processes only about half of each player's
     inputs.
   - Fix: run socket reads and writes off the main thread, and hand messages over through
     thread-safe queues.
3. **Guests see a frozen world** (high; [#11](#f11), [#17](#f17)).
   - Cause: on a guest, positions are written to physics bodies, but physics never steps there,
     so the visible positions never update.
   - Effect: the camera stays at spawn, and health bars and lock-on use stale positions. Ward
     Zero zombies draw stacked at the arena centre.
   - Fix: move the Transforms directly on guests.
4. **No movement prediction or smoothing for guests** (high; [#12](#f12) and its Unity-engine
   twin).
   - Effect: a guest's own character only moves after the host replies, so movement lags a full
     round trip. Animations step at the 20 Hz snapshot rate.
   - The current game predicts local movement and interpolates everyone else on a buffered
     timeline. Fix: port both.
5. **Match frames are far too large** (high; [#14](#f14) and two medium findings in the same
   root issue).
   - Effect: each frame is about 190 KB of JSON, sent 20 times a second. It is compressed
     separately for every player on the main thread.
   - In Ward Zero, every horde hit and damage tick goes into every frame with no limit. The
     current game caps horde events at 8 per tick and coalesces the rest.
   - Fix: send the static map once, use a compact format, and port the event budget.
6. **Sign-in and DEV unlocks will very likely fail in the built game** (high; [#4](#f4),
   [#9](#f9)).
   - Cause: account and DEV tokens are checked with .NET's ECDsa, which Unity's Mono runtime
     does not implement. The resulting exception is not caught.
   - Effect: sign-in hangs on "Connecting…". Emperor, Ansatsuken, Leet, Siren and the sandbox
     never unlock. Any guest can also send a crafted token that stops the host's message
     handling.
   - Fix: use a portable P-256 verifier (for example BouncyCastle), catch every exception in the
     host loop, and test with a real token in the Editor.
7. **The death replay breaks in versus matches, even offline** (high; [#10](#f10),
   [#15](#f15)).
   - Cause: the killcam copies frames through the same JsonUtility path.
   - Effect: in a versus match it throws an error every frame for up to about 4 seconds, while a
     duplicate, collidable copy of the arena sits in the live physics scene.
   - Fix: fix 1 removes the cause. The replay should also build its copy in its own scene first.
8. **The relay's abuse limits will lock everyone out** (high; [#5](#f5), [#13](#f13)).
   - Cause: the relay limits requests per address. Behind the HTTPS proxy its README calls for,
     every player shares the proxy's address, so the whole relay gets a single
     60-requests-per-minute allowance.
   - Nothing caps rooms per player either; the original capped each player at 12 rooms.
   - Fix: read the client address from forwarded headers, and add per-client room caps.
9. **Combat rules differ from the current game** (high).
   - Every gun, magic or ability hit stuns for 0.25 s and cancels swings and casts, with the
     mana already spent ([#6](#f6)).
   - Blocking makes you immune to all ability damage ([#7](#f7)).
   - Q/E presses made during the fire interval or a reload are silently dropped ([#8](#f8)).
   - Also different:
     - no 100 s overtime ([#41](#f41));
     - every match deals the same draft Chips ([#36](#f36));
     - Black Hole barely pulls ([#47](#f47));
     - shoulders count as headshots ([#48](#f48));
     - ground-targeted abilities can be placed through walls ([#55](#f55));
     - bots never miss ([#46](#f46)).
10. **Animation and feedback are simplified** (medium).
    - Melee animation:
      - captured melee plays without its footwork, so feet skate 25–81 cm on every attack
        ([#21](#f21));
      - the blade clearance is missing, so the greatsword sits inside the head in guard and
        block ([#22](#f22));
      - specials push the legs through the floor and magic casts sink the feet ([#23](#f23),
        [#24](#f24));
      - hit reactions are missing ([#32](#f32)).
    - Feedback:
      - guns have no muzzle flash ([#30](#f30));
      - stereo is reversed ([#31](#f31));
      - burning, slowed or marked pilots look unaffected ([#62](#f62));
      - explosions and jumps make no sound ([#61](#f61)).
    - The owner has asked Claude to port the captured-melee runtime layer; see the tracking
      issue.

## All confirmed findings
Severity is the verifier's rating after checking. Paths are under `unity/ChipArenaConversion/` at `a8fa796`; "Original" paths are the Electron game at `05950ea`.

<a id="f1"></a>
### #1 🔴 critical — JsonUtility cannot send null, so every match frame fails Valid() and every guest is disconnected when the match starts
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetworkFrame.cs` (38, 40, 60-61, 73, 80 (frame build/validate); NativeNetwork.cs:58, 62, 113)
- **Root issue:** Every guest is disconnected when a match starts

**What happens.** The host starts any online match (PvP or PvE, LAN or relay). The host leaves NativePilotFrame.death (and so death.wound), fighter and down null for living pilots, sets NativePropFrame.definition=null for every non-fragment map block, and leaves pve null in PvP. It then serialises the frame with JsonUtility.ToJson (NativeNetwork.cs:58). Unity's serializer writes a default instance for each null [Serializable] class field and "" for null strings. On the guest, FromJson gives a default NativeWound (radius 0, kind ""), so NativeBodyDamage.Valid fails at line 38. A default ContentBlock (material "", size 0,0,0) fails line 40. Valid() returns false, ClientMessage throws ArgumentException (line 113), and NetworkTick disposes the connection: "Invalid or incompatible host state." Each rejoin hits the same first frame. Online multiplayer cannot run at all.

**Evidence.** Host: `death=p.Death` / `fighter=p.Fighter` / `down=p.Down` (line 60), `definition=p.Fragment?p.Definition:null` (line 61), `if(Pve!=null)f.pve=...` (line 73), then `JsonUtility.ToJson(new NativeNetMessage{type="frame",...})` (NativeNetwork.cs:58). Guest Valid(): `p.death!=null&&(...||p.death.wound!=null&&!NativeBodyDamage.Valid(p.death.wound)...)` and `p.definition!=null&&(!content.materials.Any(m=>m.id==p.definition.material)||...||p.definition.size.x<=0...)`. Unity manual (Script serialization, "No support for null for custom classes"): "The serializer doesn't support null. If it serializes an object, and a field is null, Unity instantiates a new object of that type, and serializes that." JsonUtility.ToJson docs say it uses the Unity serializer. There is no Newtonsoft, ISerializationCallbackReceiver or SerializeReference in Assets. A probe compiled against Core prints `NativeBodyDamage.Valid(new NativeWound()) = False` and a default ContentBlock with null material and size 0,0,0. The author's relay test (Tests/RelayChecks.cs) sends raw strings like "first-pilot", so the JsonUtility message layer was never exercised. That undercuts the CONVERSION-STATUS claim of Multiplayer "compressed bounded frames ... 90-second reconnect seats and headless Unity authority". Side effects even if Valid were relaxed: `if(f.pve!=null&&Pve==null)ConfigurePve(...)` (line 80) throws "PvE requires a reserved authority boss seat" in every PvP replica. Every living pilot also gets a default NativeDeath at the origin.

**Verifier's correction.** The failure scenario is correct. One detail is wrong: the final status the player sees is "Disconnected. Rejoin this host within 90 seconds to reclaim your seat." because NativeNetwork.cs:63 runs right after the Dispose() at line 62. The default ContentBlock from props (line 40) fails Valid on every frame and on every map, independent of the pilot death check at line 38. The PvP pve side effect is real too: a default NativePveFrame passes Valid, then ApplyFrame line 80 hits ConfigurePve's InvalidOperationException at NativePve.cs:23, which also disconnects the guest.

<details><summary>Verifier's reasoning</summary>

I could not refute this. The host builds every match frame with null custom-class fields and serialises it with JsonUtility. Unity's serializer does not keep null for [Serializable] classes: it writes a default instance in their place, writes "" for null strings and [] for null arrays. On the guest, NativeGameFrame.Valid treats a non-null nested object as real data, and those default instances fail its checks. I found nothing in the code that handles this: no SerializeReference, no ISerializationCallbackReceiver, no Newtonsoft, no has-flags, and no test anywhere in Tests/ or tools/ that round-trips a NetworkFrame through JsonUtility.

1) Map props: AddProp(block) is called with fragment=false for every map block (NativeArena.cs:76). NetworkFrame then sends definition=null for those props (NativeNetworkFrame.cs:61). The guest receives a default ContentBlock with material "". The materials are only wood, stone, metal and foundation, so the check `!content.materials.Any(m=>m.id==p.definition.material)` makes Valid return false at line 40. Size 0,0,0 fails the same line as well. Every map has 72 to 300 blocks, so this happens on every map and on every frame.

2) Pilots: a living pilot has p.Death == null, which becomes a default NativeDeath whose default NativeWound has radius 0 and kind "". That fails NativeBodyDamage.Valid, so line 38 also returns false. A dead pilot with no wounds has Death.wound = LastOrDefault() = null, and the vent-fall path sets wound=null explicitly (NativeVents.cs:32). Both cases fail the same way.

Valid returning false makes ClientMessage throw ArgumentException (NativeNetwork.cs:113). NetworkTick catches it and calls server.Dispose() (line 62). The Closed check that follows (line 63) then drops the guest to the menu.

One small correction to the finding: the status message the player ends up with is "Disconnected. Rejoin this host within 90 seconds...", not "Invalid or incompatible host state.". Line 63 overwrites line 62's message right after Dispose. The outcome is the same: every guest is disconnected on the first frame, and every rejoin hits the same frame.

The secondary point also holds. In PvP, pve becomes a default NativePveFrame, which passes Valid (its arrays are empty, not null). ApplyFrame then calls ConfigurePve (line 80), which throws InvalidOperationException("PvE requires a reserved authority boss seat") (NativePve.cs:23), and that is also caught as a disconnect.

The author's VALIDATION.md says deployed rooms and headless authority were not run, and the relay test only sends raw strings, so nothing ever exercised this path. The CONVERSION-STATUS.md:22 Multiplayer row ("compressed bounded frames ... 90-second reconnect seats and headless Unity authority") is therefore not backed by working code.

Limitation: no Unity runtime is available here, so the JsonUtility null-to-default behaviour comes from Unity's documented serializer rules ("No support for null for custom classes"), not from running it.

</details>

**Suggested fix.** Do not rely on null with JsonUtility. Send explicit has-flags or empty sentinels (for example definition only in a separate fragments array, hasDeath/hasPve booleans), or switch to a serializer that keeps null. Add a managed or Editor test that round-trips a real NetworkFrame through JsonUtility and runs Valid() and ApplyFrame.

<a id="f2"></a>
### #2 🔴 critical — Socket async loops resume on Unity's main-thread sync context, so throughput is capped at about fps/2 messages per connection: guests below ~120 fps overflow and drop, and a 60 fps dedicated host falls behind on input
- **Area:** Networking
- **Where:** `Assets/ChipArena/Core/NativeTransport.cs` (24-25, 44, 47, 50, 55, 66 (also NativeRelay.cs:30-39, ConversionGame.cs:121, NativeDedicated.cs:15))
- **Root issue:** Network message rate is tied to the frame rate

**What happens.** NativeConnection, NativeListener.Accept, NativeConnection.Connect and NativeRelayHost are all started from Update/OnGUI/Awake. Their awaits (no ConfigureAwait(false)) capture UnitySynchronizationContext, which runs posted continuations once per frame. Mono/IL2CPP socket I/O always completes asynchronously. So each ReadAsync/WriteAsync costs at least one frame, and each message needs two (8-byte header, then body). A guest in a match sends a reliable "input" every FixedUpdate (60/s) plus a ping. At 60 fps the writer can send about 30/s, the 32-entry reliable queue overflows in about 1 s, and the guest disconnects itself with "Reliable queue overflow". A dedicated authority (targetFrameRate 60) can read only about 30 msgs/s per guest, so the guest's input latency grows without bound.

**Evidence.** Constructors start `reader=ReadLoop();writer=WriteLoop();`. Exact does `await stream.ReadAsync(...)`, WriteLoop does `await stream.WriteAsync(header...);await stream.WriteAsync(data...)`, and Send does `if(Interlocked.Increment(ref reliableCount)>32){Fail("Reliable queue overflow")`. ConversionGame.cs:121 sends `SendServer(new NativeNetMessage{type="input",input=pending})` every FixedUpdate. Probe (review-u/net/pace): real NativeConnection over loopback TCP, a SynchronizationContext that runs queued work once per frame like UnitySynchronizationContext.Exec, and a stream whose reads/writes complete off-thread like Mono. Results: client 60 fps/host 60 fps gives "Reliable queue overflow at 1.03s". Client 100/host 60 overflows at 2.95 s. Client 144/host 60: host processes 29.9 msg/s of 61 and mean input age reaches 2046 ms after 8 s. Only at 144/144 does it keep up (60.8/s, 18 ms). The author's loopback tests run in a console with no SynchronizationContext, so they cannot show this.

**Verifier's correction.** All of the reported details are accurate. One clarification: on the relay path, the host's reads can sometimes finish without a frame hop when ClientWebSocket already holds buffered frames. Sends are not affected by this, so relay guests still overflow the same way. For the dedicated host: the lag compounds in kernel buffers, then TCP backpressure stalls the guest's writer, and the guest finally fails with "Reliable queue overflow".

<details><summary>Verifier's reasoning</summary>

I tried to refute this and could not. The mechanism and the numbers both hold up.

1. Where the loops start. Every NativeConnection is constructed on Unity's main thread with UnitySynchronizationContext current:
- LAN guest: JoinRoom runs from OnGUI (NativeNetwork.cs:34). NativeConnection.Connect does `await Task.WhenAny(...)` with no ConfigureAwait(false), so its continuation is posted back to the main thread, and that is where `new NativeConnection(socket)` runs (NativeTransport.cs:28).
- LAN host: HostRoom runs from OnGUI, or from Awake via InitializeDedicated (NativeDedicated.cs:16). NativeListener.Accept awaits AcceptTcpClientAsync and then constructs the connection on the main thread (NativeTransport.cs:66).
- Relay: Host, Listen and Accept all await without ConfigureAwait(false) and construct the connection on the main thread (NativeRelay.cs:30-39).

The constructor calls ReadLoop() and WriteLoop() synchronously (NativeTransport.cs:24-25). Every await inside them therefore captures the Unity context. ConfigureAwait, Task.Run and SetSynchronizationContext appear nowhere in Assets/ChipArena (grep confirmed).

2. Two frames per message. UnitySynchronizationContext.Post always queues the work. Exec runs once per frame, at PlayerLoop Update.ScriptRunDelayedTasks, and works through a snapshot of the queue, so anything posted while Exec is running waits for the next frame. Mono's NetworkStream goes through Socket.BeginReceive/BeginSend and the IOSelector, which always completes on the threadpool and never inline. So every socket await costs at least one frame. Each message needs two reads (the 8-byte header in Exact, then the body) and two writes (header, then body), which caps each connection at fps/2 messages per second in each direction. An idle writer also pays one more hop to wake from the SemaphoreSlim.

3. Send rate. ConversionGame.cs:121 sends one reliable "input" message every FixedUpdate (60/s), and Send fails the connection once more than 32 reliable messages are queued (NativeTransport.cs:44). AcceptInput only merges inputs on the host after they are read, so there is no coalescing on the send side.

4. Independent probe (review-u/pace-verify2). It uses the real NativeConnection, a context that copies Unity's Exec logic (snapshot list, posts deferred to the next frame), a stream whose reads and writes always complete on the threadpool like Mono, and Unity's loop order (FixedUpdate, Update, ScriptRunDelayedTasks). It reproduces the reported numbers:
- guest 60 / host 144: "Reliable queue overflow at 1.02s"
- guest 100 / host 144: overflow at 2.95 s
- guest 144 / host 60 (the dedicated host's target frame rate): host reads 29.9 msg/s and the mean input age reaches 2063 ms after 8 s, still growing
- guest 144 / host 144: 60.6/s and 25 ms, which keeps up only just
- Control with no sync context (how the author's console tests run): 60.7/s and 14-17 ms. This shows why the author's loopback tests could not catch it.

5. Relay path. NativeWebSocketStream.WriteAsync awaits ClientWebSocket.SendAsync inside an async method, and that send ends in a Mono NetworkStream/SslStream write, so relay guests also need two hops per message. Relay reads can sometimes finish without a hop when ClientWebSocket has frames already buffered, but that does not help the guest's send side.

Limits on this verification: no Unity runtime is available here. Two points rest on knowledge of Unity and Mono internals rather than on execution: that UnitySynchronizationContext.Exec runs once per frame and defers posts made while it runs, and that Mono's socket I/O never completes synchronously.

Why critical: in normal play, online multiplayer fails on common hardware.
- Any guest that cannot hold about 122 fps disconnects itself within 1-3 s of match start (about 1 s at 60 fps).
- Short dips below that build up a backlog that drains at no more than about 11 messages/s, even at 144 fps.
- The dedicated authority (targetFrameRate 60) can read only about 30 of each guest's 61 messages per second, so input lag grows without limit for every guest until TCP backpressure makes the guest's queue overflow.

Offline play with bots is unaffected. If multiplayer is treated as just one major feature rather than the core of the game, the rubric would put this at high.

</details>

**Suggested fix.** Run transport loops off the main thread (ConfigureAwait(false) everywhere, or Task.Run the loops / dedicated threads) and exchange data only through the concurrent queues. Send header+body as one write. Coalesce inputs (latest held state plus tap bits) instead of 60 reliable messages/s, or raise and pace the queue limit.

<a id="f3"></a>
### #3 🔴 critical — JsonUtility turns every null nested object into a default object, so all replicated frames fail validation and every network client disconnects on the first frame
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeNetworkFrame.cs` (38, 40, 60-61, 80 (also NativeNetwork.cs:62,113; NativePve.cs:23; NativeDraftPresentation.cs:19))
- **Root issue:** Every guest is disconnected when a match starts

**What happens.** Host any LAN, relay or --ca-server room and start a match. On the host, a pilot that has not died has Death==null, a non-fragment prop has definition==null (line 61: `definition=p.Fragment?p.Definition:null`), and in PvP Pve==null. The host serializes this with JsonUtility.ToJson (NativeNetwork.cs:58). Unity's serializer does not support null for custom [Serializable] classes and writes null strings as empty strings, so the client receives a default NativeDeath with a default NativeWound (radius 0), a default ContentBlock with material "", and a default NativePveFrame. Valid() then rejects every frame. Line 38 calls `NativeBodyDamage.Valid(p.death.wound)`, which is false for radius 0. Line 40 checks `!content.materials.Any(m=>m.id==p.definition.material)`, which is true for "". ClientMessage throws ArgumentException, and NativeNetwork.cs:62 disposes the connection with "Invalid or incompatible host state." Even with Valid() relaxed, line 80 `if(f.pve!=null&&Pve==null)ConfigurePve(...)` throws in every PvP room (NativePve.cs:23 `if(Pve.Boss==null)throw`). Chosen arrives as "", so the draft UI's `content.Chip(match.Chosen)` (NativeDraftPresentation.cs:19) throws every frame. Every pilot would also get a non-null Death, Down and Fighter.

**Evidence.** Probe (review-u/engine/jsonnull, compiled against Core): `NativeBodyDamage.Valid(default NativeWound) = False (radius=0)`; default ContentBlock material is not among wood/stone/metal/foundation; `content.Chip("")` throws `ArgumentException: Unknown chip:`. No test exercises this path. The managed tests parse with System.Text.Json, which preserves null, and NativeNetMessage/NativeGameFrame live only in Runtime. The relay test sends a synthetic string (RelayChecks.cs:24 `new string('v',130000)`). This contradicts CONVERSION-STATUS: "Multiplayer | Native TCP LAN; standalone WebSocket relay/discovery and private rooms; compressed bounded frames ... 90-second reconnect seats and headless Unity authority" and README "Native LAN rooms, standalone WebSocket relay/discovery, reconnect seats ... and headless authority".

**Verifier's correction.** The finding is accurate as written. One more consequence it does not list: the single-player killcam replay (NativeReplay.cs:23 and :51) uses the same JsonUtility round-trip and calls ApplyFrame on the copy. In a PvP match that copy has a non-null `pve`, so ConfigurePve should throw InvalidOperationException at NativePve.cs:23 when the elimination replay starts, even offline. I traced this by reading the code; it was not run.

<details><summary>Verifier's reasoning</summary>

I tried to refute this and could not.

1. Frames really go through JsonUtility in both directions. The host builds them with `JsonUtility.ToJson(new NativeNetMessage{type="frame",...,frame=arena.NetworkFrame()})` (NativeNetwork.cs:58). The client reads them with `JsonUtility.FromJson<NativeNetMessage>(json)` and then `ClientMessage` (NativeNetwork.cs:62). The code has no other serializer: no Newtonsoft, no System.Text.Json and no [SerializeReference] anywhere in Assets/ChipArena, and the manifest only has com.unity.modules.jsonserialize.

2. All the nested types are plain [Serializable] classes with public fields: NativeDeath (NativeDeaths.cs:7), NativeWound (Core/NativeBodyDamage.cs:6), NativeFighter (NativeFighters.cs:8), NativeRecovery (Core/NativeSpecialRules.cs:11), ContentBlock (Core/ConversionContent.cs:21), NativePveFrame (NativeNetworkFrame.cs:20) and Snapshot/Actor (Core/ArenaModel.cs). Unity's serializer, which JsonUtility uses, does not support null for these. When it serializes a null field of such a type it creates a new default instance and serializes that. It also writes null strings as "". So the client always receives non-null `death`, `death.wound`, `definition`, `fighter`, `down` and `pve`, and `chosen` arrives as "" instead of null.

3. Why the first frame fails Valid():
   - Line 38. Every pilot that has not died has `Death==null`; it is only set in RecordDeath (NativeDeaths.cs:25). That pilot arrives with a default NativeDeath whose default NativeWound has radius 0. The check `p.death!=null&&(...||p.death.wound!=null&&!NativeBodyDamage.Valid(p.death.wound)...)` is true, so Valid returns false. A real death with no wound (`wound=p.Wounds.LastOrDefault()` is null) fails the same way.
   - Line 40, independently. Map blocks are not fragments (Fragment is only set at NativeDestruction.cs:40), so line 61 sends `definition=null`. The client gets a default ContentBlock with material "", and `!content.materials.Any(m=>m.id==p.definition.material)` makes Valid return false. Every map has 72 to 300 blocks.
   - Result: line 113 throws ArgumentException, and line 62 catches it, disposes the connection and shows "Invalid or incompatible host state." This hits every LAN, relay and headless room on its first frame.

4. Secondary claims also hold if Valid() were relaxed:
   - In PvP the client receives a default `pve`, so ApplyFrame line 80 calls ConfigurePve. NativePve.cs:23 then throws InvalidOperationException("PvE requires a reserved authority boss seat").
   - `chosen` arrives as "", so `match.Chosen!=null` is true and `content.Chip(match.Chosen)` (NativeDraftPresentation.cs:19) throws.
   - The same JsonUtility round-trip is used offline for the killcam (NativeReplay.cs:23 and :51 call `ApplyFrame` on the copy). A PvP killcam would hit the same ConfigurePve throw even in single player. I traced this by reading the code; it was not run.

5. Nothing tests this path. No Tests, tools or Relay file references NativeGameFrame, NetworkFrame or JsonUtility. Managed checks parse with System.Text.Json, which keeps null, and the relay test sends a synthetic string. This contradicts the CONVERSION-STATUS.md claim: "Multiplayer | Native TCP LAN; standalone WebSocket relay/discovery and private rooms; compressed bounded frames ... 90-second reconnect seats and headless Unity authority".

Limitation: no Unity runtime is available here, so I could not run JsonUtility itself. The null-to-default-instance behaviour is Unity's documented serializer rule ("The serializer does not support null. If it serializes an object and a field is null, Unity instantiates a new object of that type and serializes that"; JsonUtility "internally uses the Unity serializer"). The failing validator results were confirmed with a probe compiled against Core.

</details>

**Suggested fix.** Do not rely on null for [Serializable] classes in JsonUtility. Add explicit presence flags or ids (hasDeath, hasDefinition, hasPve, chosen=="" meaning none), or switch the wire format to a null-preserving serializer. Add an in-Editor test of NetworkFrame→ToJson→FromJson→Valid→ApplyFrame for PvP and PvE.

<a id="f4"></a>
### #4 🟠 high — Token verification depends on ECDsa, which Unity's Mono runtime does not implement, so sign-in and all DEV/grant access fail in the built game. A crafted token can also freeze a host.
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Core/NativeAccess.cs` (27-28, 31 (callers: Runtime/NativeAccount.cs:49, Runtime/NativeNetwork.cs:84, 98; host loop catch filter NativeNetwork.cs:54; Mono2x forced at Editor/PrototypeBuild.cs:99))
- **Original:** src/shared/account-auth.ts:7-18 and src/shared/dev-auth.ts:5 (WebCrypto ECDSA P-256 verify)
- **Root issue:** Sign-in and DEV unlocks will very likely fail in the built game

**What happens.** Windows player built with Mono2x. A player enters valid credentials and the account service returns 200 with a valid ES256 token. In the NativeAccount.Request coroutine, NativeAccess.Verify calls ECDsa.Create(new ECParameters{nistP256,...}). In Mono's class library (which Unity 6000.3 uses for both Mono and IL2CPP) ECDsa.Create/ECDsaCng are stubs that throw NotImplementedException, or TargetInvocationException/NullReferenceException depending on how the factory resolves. None of these is in Verify's catch list, so the exception escapes the coroutine: Account is never set, Status stays 'Connecting…', and nobody can sign in, including the owner's developer account. Emperor, Ansatsuken, Leet, Siren, granted cosmetics, the sandbox and the admin UI all become unreachable. If a Mono build throws PlatformNotSupportedException instead (that one is caught), every login still ends at 'Account response could not be verified.' Security side effect: any guest can send {type:'auth-refresh', token:<two base64url parts, a 64-byte junk signature and a well-formed claim with kind/accountId/user/iat/exp>}. HostMessage calls Verify, which throws. The host loop only filters ArgumentException and InvalidOperationException, so the exception leaves NetworkTick and Update. One such message per frame stops frame broadcasts and the host's local simulation. This is a room/dedicated-server DoS that needs no signing key.

**Evidence.** NativeAccess.cs:27-31: `using(var key=ECDsa.Create(new ECParameters{Curve=ECCurve.NamedCurves.nistP256,Q=...}))if(!key.VerifyData(...))return Guest; ... catch(ArgumentException){...}catch(CryptographicException){...}catch(FormatException){...}catch(NotSupportedException){return Guest;}`. NativeNetwork.cs:54: `catch(Exception e)when(e is ArgumentException||e is InvalidOperationException)`. PrototypeBuild.cs:99: `SetScriptingBackend(...,ScriptingImplementation.Mono2x)`. The managed suite runs on CoreCLR, where the logic is correct. My probe built a copy of NativeAccess.cs with a test key and fed it WebCrypto-signed tokens: 'good' was accepted, 'dev' gave Developer, 'tampered' gave Guest. That is why the passing checks hide the problem. The only Verify test is `NativeAccess.Verify("invalid",...)` (Tests/NativeSystemsChecks.cs:48), which returns at `parts.Length!=2` before ECDsa runs. No Editor/Unity gate calls Verify. The author compiled against reference assemblies, which proves the API exists, not that it is implemented. I could not run this here (no Mono or Unity); it rests on Mono's ECDsa being unimplemented. A one-line play-mode call to ECDsa.Create(ECCurve.NamedCurves.nistP256) settles it. Docs that this contradicts: CONVERSION-STATUS.md:21 "Accounts | Configured HTTPS login/register/recover/profile/logout, signed unlock verification, Windows encrypted sessions and in-room token refresh" and :11 "Signed DEV content | ... grant verification".

**Verifier's correction.** The host-loop catch filter is at NativeNetwork.cs:55, not 54. A crafted token in a hello message (SanitizedSeat, NativeNetwork.cs:98, reached via :79) triggers the same escape before the peer has a seat. The DoS does not stop the host's FixedUpdate simulation (ConversionGame.cs:119-121). It stops message processing for the remaining peers, the 20 Hz frame broadcast (NativeNetwork.cs:58), the listen-host's Update input handling (ConversionGame.cs:65 onward) and the dedicated server's ready/StartMatch logic (NativeDedicated.cs:20-22). Remote clients still get a frozen room. The DoS part only applies if Mono throws something other than PlatformNotSupportedException. The sign-in and grant failure applies whichever exception Mono throws.

**Suggested fix.** Replace ECDsa with a managed P-256/SHA-256 verifier that runs on Mono and IL2CPP (e.g. BouncyCastle's ECDsaSigner over P1363 r||s) and test it inside the Unity Editor gate with a known-good and a tampered token. Make Verify end with catch(Exception){return Guest;}. In the host loop, wrap each HostMessage in a catch-all that disposes only the offending peer.

<a id="f5"></a>
### #5 🟠 high — The relay's 'per-address' limit becomes one global 60 requests/min bucket behind the documented reverse proxy, and nothing caps rooms per address
- **Area:** Accounts & security
- **Where:** `Relay/Program.cs` (11-12, 16)
- **Root issue:** The relay's abuse limits will lock everyone out

**What happens.** Clients require wss:// for any non-loopback relay (NativeWebSocketStream.Endpoint), and the relay serves plain HTTP, so it is deployed as Relay/README says: behind an HTTPS reverse proxy with the upstream on loopback. Every request's RemoteIpAddress is then the proxy, so all players worldwide share one fixed window of 60 requests per minute. Hosting costs 1 request, a join costs 2 (the guest's /join plus the host's /accept), and each room-list refresh costs 1. Normal traffic above about 30 joins per minute gets 429 ('Could not connect'), and one person looping GET /native/v3/rooms blocks hosting and joining for everyone. Without a proxy, one address can still open 60 host sockets per minute with no concurrent cap. Each stays alive on keep-alive, so the global 1000-room limit (line 16) fills in about 17 minutes and all further hosting returns 400.

**Evidence.** Program.cs:11: `PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}))`. No UseForwardedHeaders and no per-address room or socket cap. Line 16: `rooms.Count>=1000` → 400. Probe: I built an unmodified copy of Program.cs in my scratch dir and ran it on 127.0.0.1. After one /health request, 62 GET /native/v3/rooms requests, each with a different X-Forwarded-For (simulating proxied distinct clients), returned 59×200 then 429. Claim this contradicts, Relay/README.md: "The relay enforces room capacity, one-use tickets, per-address connection limits, bounded binary bandwidth..."; the only limit is a request rate, keyed on the proxy address in the recommended deployment.

**Verifier's correction.** The finding is accurate as written. Two refinements:
1. The cited lines are right (Program.cs:11 for the limiter, line 12 for the pipeline without forwarded headers, line 16 for the global room cap). The parity source is src/relay/worker.ts:42-44, plus directory.ts:26-34, where rooms were capped at 12 per creator, keyed on CF-Connecting-IP. The finding left source_ref empty.
2. An undocumented mitigation exists: setting ASPNETCORE_FORWARDEDHEADERS_ENABLED=true on the upstream host makes ASP.NET Core honour X-Forwarded-For. The README never mentions it, and it adds no room or socket cap per address.

**Suggested fix.** Add ForwardedHeaders middleware with an explicit KnownProxies/KnownNetworks list and partition on the real client IP. Add per-IP concurrent host and join socket caps and a room-per-IP cap. Give /rooms polling its own limit, and document the proxy requirement in code.

<a id="f6"></a>
### #6 🟠 high — Every direct gun/magic/ability hit applies a 0.25 s stun; the original has no hit stun for these
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (184 (StunMelee at NativeMelee.cs:79-85))
- **Original:** src/shared/simulation.ts:225-238 (damage() sets only the cosmetic hitReaction for non-melee hits); hit-reaction.ts:11-17; standard-melee.ts:44-45 (hitStunUntil is set only by melee stun/stagger)

**What happens.** A pistol, MG, rifle, magic bolt, tidal wave or ability blast hits a sword user mid-swing, or a mage during cast windup. Unity calls StunMelee(target,.25): the swing, E special, ability windup and in-flight magic cast are cancelled (the cast's mana is already spent), movement is locked, the target cannot shoot, reload or aim, and ADS settle restarts. Under fire this repeats once per second (0.25 s stun plus 0.75 s immunity). In the original these hits change none of that.

**Evidence.** Unity: `if(!SirenArmor(target)&&direct&&!melee&&actual>0&&Time>=target.StaggerImmune){StunMelee(target,.25f);target.StaggerImmune=target.StunEnd+.75f;}`. `direct` defaults to true for bullets (BulletImpact passes no `direct:`) and for AbilityHit. StunMelee does `CancelFighter;CancelMelee;CancelSpecial;CancelAbilities;p.Roll=null;...p.StunEnd=Math.Max(p.StunEnd,Time+seconds)`. TS probe (review-u/core-sim/ts-block-probe.ts): after a direct ranged hit, `TS direct ranged hit: target hp 77.0 hitStunUntil 0 ... arcade.stunned= false`. Not listed as a deliberate adapter in CONVERSION-STATUS.md.

**Verifier's correction.** The stun also hits reflected and split bullets: BulletImpact never passes direct:, so `direct` stays true while primary:b.Direct is false. It also hits fighter echo damage (NativeFighters.cs:123). So the effect is a little wider than the finding says. One part of the failure scenario needs correcting: ADS is blocked while stunned (ConversionGame.cs:135 and NativeArena.cs:120 require !Incapacitated). I did not confirm that the ADS settle timer actually restarts.

**Suggested fix.** Remove the generic non-melee stun. Keep stagger only on the melee/primaryImpact paths, as in standard-melee.ts.

<a id="f7"></a>
### #7 🟠 high — Block makes a pilot immune to all ability damage (blasts, tidal wave, blink, ball-lightning zaps)
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (177; NativeAbilities.cs:143-145, 158, 178, 195, 288)
- **Original:** src/shared/ability-system.ts:191-200 (blast/hit call sim.explode/sim.damage, which have no block check); magic-abilities.ts:42 (tidal-wave reflect:false so it is applied to blockers); simulation.ts:225-238

**What happens.** A pilot presses Block (0.3 s window, 4 s cooldown) as a Meteor (70), Orbital strike (85), frag, rocket, geyser or black-hole collapse lands, or as a Tidal Wave reaches them. Unity: 0 damage, no knockback, no slow, and first-block Chips proc. TS: full damage. Only real ability projectiles are reflected in TS.

**Evidence.** Damage(): `if(source!=null&&!forced&&direct&&Time<target.BlockEnd){BlockProc(target,source,target.Body.position);...return 0;}`. AbilityHit defaults `bool direct=true` and calls `Damage(target,owner,damage,dir,impulse,head,false,direct,false)`. AbilityBlast calls `AbilityHit(p,owner,damage*fall,...)` with direct left at true. Tidal Wave contact passes `...,head,true,...`. TS probe: `TS ability blast on a blocking pilot: damage 70.0 blockTime still 0.3`.

**Verifier's correction.** The Tidal Wave call is at NativeAbilities.cs:289, not 288. The listed direct-impact calls (frag :271, rocket dud :277, orbital tag :280) never reach a blocker in either version, because Block reflects those shots first (Unity :208, TS ability-system.ts:226). They are not part of the divergence.

The fix hint needs a caveat. Passing direct:false would also change other behaviour, because Damage gates more than Block on direct: roll i-frame avoidance (`direct&&Invulnerable(target)`, line 176), stagger, roll cancel and the hit-chip path. In TS, rollAvoids('ability') is true, so blasts are still dodged by rolling. A separate "blockable" flag (false for blasts, zaps, Blink and Tidal Wave) is therefore the correct fix.

ChipBlast's direct:false has the same side effect today: chip blasts are not avoided by roll i-frames, while in TS rollAvoids('static'/'shrapnel'/'blockPulse') is true. This is a separate, minor issue.

**Suggested fix.** Pass direct:false (or a separate 'blockable' flag) for blast, zone, blink, zap and tidal-wave damage. Keep block handling for melee directHits and projectile reflection only.

<a id="f8"></a>
### #8 🟠 high — Q/E presses are silently dropped during the primary-fire interval and while reloading
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeAbilities.cs` (42, 50-53, 57-63)
- **Original:** src/shared/ability-system.ts:112-122 (press buffers only on cast/abilityCast/rolling/committed/stunned/ability cooldown; it never checks p.cooldown or reloadTime)

**What happens.** A rifle player fires (1.15 s interval) and presses E (Orbital Mark) 0.2 s later. Unity: AbilityBusy is true because Time<p.Ready, so the press is buffered. The buffer expires after 0.3 s and the press is discarded, so the mark never casts. Pressing Q/E during a reload (2.6 s rifle) is also dropped. The same applies to fire/darkness/pistol presses inside the first part of each interval, and after an ability lockout longer than 0.3 s (attackRate<1). TS casts immediately.

**Evidence.** `private bool AbilityBusy(NativePilot p)=>p.AbilityCast!=null||p.CastAt>0||Time<p.RollEnd||Time<p.AttackEnd||Incapacitated(p)||Time<p.Ready||p.ReloadEnd>Time;` plus `if(Time-p.BufferedAt>.3f)p.BufferedSlot=null`. TS probe (review-u/core-sim/ts-press-probe.ts): `E press 0.2 s after a rifle shot (cooldown left 0.95): cast` and `E press while reloading (reloadTime 2.60): cast`.

**Verifier's correction.** Location is NativeAbilities.cs:42 (AbilityBusy), with the buffer expiry at lines 51-52 and the non-refreshing retry at line 72. The lockout write at line 90 also makes AbilityBusy block the other slot for the lockout duration (0.2-0.4 s divided by attackRate). As a result Q-to-E combos are dropped too, not only presses after a primary. TS lockout feeds p.cooldown, which gates only the primary.

Dropped windows at default stats, per interval:
- rifle: first 0.85 s of 1.15 s
- pistol: first 0.12 s of 0.42 s
- fire: first 0.2 s of 0.5 s
- darkness: first 0.36 s of 0.66 s
- lightning: never (0.28 s interval)
- reloads: everything except the last 0.3 s

Fix: drop `Time<p.Ready` and `p.ReloadEnd>Time` only from the standard Q/E gate. Keep them for NativeSummons.cs:89, because the TS necronomicon secondary does check `p.reloadTime>0||p.cooldown>0` at simulation.ts:379.

**Suggested fix.** Remove `Time<p.Ready` and `p.ReloadEnd>Time` from AbilityBusy. Keep the cast/abilityCast/roll/commitment/stun gates.

<a id="f9"></a>
### #9 🟠 high — Signed account and DEV-unlock verification uses ECDsa P-256, likely unimplemented in Unity's Mono, and no test checks a valid token
- **Area:** Delivery & docs
- **Where:** `Assets/ChipArena/Core/NativeAccess.cs` (27-31 (callers: Assets/ChipArena/Runtime/NativeAccount.cs:44-51, NativeNetwork.cs:84,98))
- **Original:** src/client/accounts.ts:12 (TS verifies with WebCrypto, which supports ES256)
- **Root issue:** Sign-in and DEV unlocks will very likely fail in the built game

**What happens.** In the Unity player or Editor, a player logs in. NativeAccount calls NativeAccess.Verify, which calls `ECDsa.Create(new ECParameters{...})`. Mono's class library (Unity's runtime) implements ECDsa.Create(...) by throwing NotImplementedException. Verify catches only ArgumentException, CryptographicException, FormatException and NotSupportedException, so the exception escapes. The account coroutine dies after `Busy=false`, Status stays 'Connecting…' and login never completes. Saved-session refresh fails the same way. The owner's DEV account can never unlock Emperor, Ansatsuken, Leet, Siren or the sandbox, and hosts can never verify signed-in joiners. If Mono throws PlatformNotSupportedException instead, every login ends with 'Account response could not be verified.' Either way the claimed account feature does not work. The .NET 8 CoreTests cannot catch this, and nothing anywhere calls Verify with a valid signature.

**Evidence.** NativeAccess.cs:27 `using(var key=ECDsa.Create(new ECParameters{Curve=ECCurve.NamedCurves.nistP256,Q=...}))`; :31 `catch(ArgumentException)...catch(NotSupportedException){return Guest;}`, with no NotImplementedException handler. A search for `NativeAccess.Verify` finds only the runtime callers, no test or Editor gate. CONVERSION-STATUS.md:21 claims 'signed unlock verification'; VALIDATION lists 'live account flows' as not run. Not executed here (no Mono or Unity runtime available). This is PLAUSIBLE, based on Mono's known lack of ECDsa, a common Unity limitation for ES256 JWTs.

**Verifier's correction.** Additional impact beyond the finding: Assets/ChipArena/Runtime/NativeNetwork.cs:54 does not catch NotImplementedException. Any seated guest can therefore send "auth-refresh" (line 84, accepted even mid-match) with a forged-shape token (any 64-byte signature and a claim with kind=chip-arena-player and a current iat/exp). On Mono this throws out of NetworkTick, the first call in ConversionGame.Update:65 and NativeDedicated.DedicatedTick:20, on every frame. Snapshot broadcast (line 57), later peers' input and the host's own Update logic are skipped, and the attacker is never disconnected, so the room is frozen remotely. hello (line 77) and profile (line 86) trigger the same thing in the lobby through SanitizedSeat (line 98). Fix options: catch all exceptions in Verify; add a NativeBuildChecks gate that calls ECDsa.Create(ECParameters) and VerifyData; ship a managed P-256 verifier if needed; add a positive-path fixture signed with a disposable test key behind an injectable public key.

**Suggested fix.** Add a NativeBuildChecks gate that runs ECDsa.Create(ECParameters) with the embedded public key and VerifyData on dummy bytes, expecting false rather than an exception. If it throws, ship a managed P-256 verifier (for example BouncyCastle's ECDSASigner) and catch all exceptions in Verify. Add a positive-path fixture: a token signed with a disposable test key, verified through an injectable public key.

<a id="f10"></a>
### #10 🟠 high — Killcam in any non-PvE match throws in ApplyFrame every frame and drops a full collidable copy of the arena into the live physics scene for ~4 s
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeReplay.cs` (23, 46, 49-51 (with NativeNetworkFrame.cs:79-80, NativePve.cs:22))
- **Root issue:** The death replay breaks in versus matches, even offline

**What happens.** Local player dies in a PvP round (offline vs bots, or the player host online). ObserveReplay stores `JsonUtility.FromJson(JsonUtility.ToJson(arena.NetworkFrame()))`, so copy.pve becomes a non-null default. RenderReplay calls replayArena.ApplyFrame(copy). Reset() builds root, ~230-300 props and all pilots in the active (live) scene. Then ConfigurePve throws InvalidOperationException because there is no AuthorityBoss seat. This happens before the pilots are made kinematic and before MoveGameObjectToScene. replaySource is never set, so every LateUpdate repeats Reset plus the throw until replayEnd. Meanwhile the live FixedUpdate Physics.Simulate includes those duplicate colliders and dynamic bodies: live dynamic crates and pilots near spawn points get depenetration pushes, and live shots hit invisible copies. On a player-hosted room this corrupts the authoritative match for every guest. It also logs an exception per frame and causes a big hitch.

**Evidence.** NativeReplay.cs:51 `if(replaySource!=a){var copy=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(a));replayArena.ApplyFrame(copy,arena.LocalId);SceneManager.MoveGameObjectToScene(replayArena.Root,replayScene);replaySource=a;}`. ApplyFrame: `if(reset){Reset();...}` then `if(f.pve!=null&&Pve==null)ConfigurePve(...)`. ConfigurePve: `Pve.Boss=Pilots.Find(p=>p.Seat.AuthorityBoss);if(Pve.Boss==null)throw new InvalidOperationException(...)`. AddProp gives dynamic blocks a non-kinematic Rigidbody (NativeDestruction.cs:35). Live renderers are disabled at line 49 before the throw, so the player sees no replay at all.

**Verifier's correction.** The throw is at NativePve.cs:23, not 22. NativeNetworkFrame.cs:79 is Reset and :80 is the ConfigurePve call. The duplicate props are visible to the dying local player, whose camera freezes on them; they are invisible only to remote viewers. The scenario where a player-hosted room corrupts every guest's match is not reachable today: the same null-pve JsonUtility round trip makes each guest's ApplyFrame throw on the first frame, and NativeNetwork.cs:62 then disconnects it. The reachable failure is any offline PvP match (vs bots) when the local player dies.

**Suggested fix.** Fix the null-transport issue (pve presence flag). Create the replay root directly in replayScene (SceneManager.SetActiveScene or move before populating), wrap ApplyFrame so a failure ends the replay, and set replay bodies kinematic before anything can throw.

<a id="f11"></a>
### #11 🟠 high — Guest replica moves kinematic bodies with Rigidbody.position but never simulates, so transforms used by the camera, health bars, lock-on and horde rendering stay at spawn
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetworkFrame.cs` (85, 99, 126-127 (consumers: ConversionGame.cs:134, NativeCombatHud.cs:25-26, NativeLockOn.cs:13-18, NativeSourcePresentation.cs:31, NativeSummonPresentation.cs:18))
- **Root issue:** Guests see a frozen world

**What happens.** On a guest (arena.Replica), FixedUpdate returns before Physics.Simulate. Pilot and minion positions are written only through `p.Body.position=...` / `m.Body.position=...`. Pilots also use RigidbodyInterpolation.Interpolate under SimulationMode.Script. Unity documents that a Rigidbody.position write reaches the Transform only after the next simulation step, which never comes on a replica. UpdateCamera follows `p.Body.transform.position`, so the guest camera stays at its spawn point. Health bars and lock-on use transforms. Remote PvE adds are created at the root origin and drawn at `m.Body.transform.position`, so they render at the arena centre. Characters drawn from ReplicaSnapshot (which uses Body.position) move away from the camera.

**Evidence.** `p.Body.position=Vector3.Lerp(p.Body.position,pf.position,blend);...Physics.SyncTransforms();` (line 127). Physics.SyncTransforms only pushes Transform to physics, not the other way. `m.Body.position=mf.position` (line 99). ConversionGame.cs:134 `Vector3 focus=p.Body.transform.position+...`. Unity Rigidbody.position docs: "If you change the position of a Rigidbody using Rigidbody.position, the transform will be updated after the next physics simulation step." Reviewed by reading only; needs play-mode confirmation, which is masked today by the frame-rejection finding.

**Verifier's correction.** The file and line references are correct. Two things to add:
1. The intro `arena.Root.SetActive(false)`/`(true)` toggle in ConversionGame.LateUpdate makes it worse. Re-enabling rebuilds each physics body from its stale Transform, so after each round intro the guest's Body.position also resets to spawn for a moment.
2. A fix that only moves the Transform on replicas also has to handle Interpolate on pilot bodies. The simplest fix is to set `body.interpolation=None` on replica bodies and write `body.transform.SetPositionAndRotation` together with `Body.position`.

**Suggested fix.** On replicas, write Transform directly (body.transform.SetPositionAndRotation, or Rigidbody.position plus transform) and set interpolation to None for replica bodies, or read Body.position consistently in the camera, HUD and minion presentation.

<a id="f12"></a>
### #12 🟠 high — No client-side prediction or snapshot-timeline interpolation: your own movement lags a full round trip, and guest animation time advances in 20 Hz steps
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetworkFrame.cs` (85, 89, 99, 126-127; ConversionGame.cs:121; Core/NativePoseHistory.cs:36)
- **Original:** src/client/prediction.ts:21; src/client/snapshots.ts:7-47; src/shared/network-config.ts:2
- **Root issue:** No movement prediction or smoothing for guests

**What happens.** Over the internet relay at ~80 ms RTT, a guest pressing W sees its own character and facing move only after host Step plus the next 50 ms snapshot plus the RTT, because the local pilot is just lerped toward host frames. Match.Time on the replica changes only when a frame arrives. NativeVisualClock caps extrapolation at one 1/60 s tick, so presentation time freezes for ~33 ms of every 50 ms and animations and effects visibly step at 20 Hz. Minions, props and bullets are snapped to each frame without interpolation. The original predicted local movement with a Rapier Predictor and interpolated players, props, projectiles and PvE on an adaptive 75-350 ms timeline. The conversion docs do not list this as a deliberate adapter.

**Evidence.** Replica FixedUpdate only sends input and returns (ConversionGame.cs:121). InterpolateReplica only smooths pilot positions with `1-Mathf.Exp(-24*delta)`. Props `p.transform.SetPositionAndRotation(pf.position,pf.rotation)` (line 89) and minions `m.Body.position=mf.position` (line 99) are snapped. NativePoseHistory.cs:36 `value=Math.Max(value,simulationTime+Math.Max(0,Math.Min(Step,sinceFixedStep)))`. A grep of CONVERSION-STATUS/README/VALIDATION finds no mention of prediction or network interpolation.

**Verifier's correction.** The finding is accurate. One refinement: remote pilots are not stepped. They are exponentially smoothed (about a 42 ms time constant) toward the latest 20 Hz frame, with no velocity extrapolation and no buffer delay. Only props, minions, bullets, effects and the presentation clock are snapped or stepped.

A possible related issue I could not confirm without Unity: the guest runs with Physics.simulationMode=Script (ConversionGame.cs:31) and never calls Physics.Simulate. Unity documents that a Rigidbody.position write reaches the Transform only after the next simulation step. Both the minion render position (NativeSourcePresentation.cs:31 `m.Body.transform.position`) and the camera focus (UpdateCamera `p.Body.transform.position`) read the Transform. If so, they may not follow the replicated Rigidbody.position on the guest at all. This should be checked in play mode.

**Suggested fix.** Port the Predictor (local capsule replay and reconciliation) and SnapshotBuffer (buffered server timeline with interpolation and extrapolation for pilots, props, projectiles, minions and the animation clock), or document the gap explicitly as unfinished.

<a id="f13"></a>
### #13 🟠 high — Relay rate limiter keys on the socket peer IP; behind the required TLS reverse proxy all players share one 60-requests/minute bucket
- **Area:** Networking
- **Where:** `Relay/Program.cs` (11 (and Relay/README.md:7, Core/NativeRelay.cs:15))
- **Original:** src/relay/party.ts:17-18
- **Root issue:** The relay's abuse limits will lock everyone out

**What happens.** The Unity client refuses ws:// for non-loopback relays (NativeRelay.cs:15). The relay has no TLS configuration, and the README says to put it behind an HTTPS/WebSocket reverse proxy with the upstream on loopback. No ForwardedHeaders middleware is configured, so c.Connection.RemoteIpAddress is the proxy address for everyone. Each host costs 1 request, each guest join 2 (join plus the host's accept), and each room-list refresh 1. A few rooms forming in one minute exhaust the shared bucket, and every player gets 429 until the window resets. Any single client can also send 60 requests/minute and lock everyone out continuously. The original relay keyed its limits on CF-Connecting-IP (the real client).

**Evidence.** `PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}))`. README: "place the relay behind an HTTPS/WebSocket reverse proxy ... Keep the upstream bound to loopback". Probe against a locally built copy of the relay: 59 /health requests OK, then 11 × 429, and the next host WebSocket attempt from the same address was refused with 429.

**Verifier's correction.** Two details in the finding need correcting:
- The TLS proxy is not strictly required. Kestrel can terminate TLS through configuration alone. But the proxy is the only deployment Relay/README.md:7 documents.
- The original party.ts:17-18 limit was a per-real-client cap on active rooms (12 per CF-Connecting-IP, 512 total), not a request-rate limit.

Two additions:
- The README:7 claim of "per-address connection limits" is wrong as written. The code enforces a per-address request rate (Program.cs:11), and behind the documented proxy that address is the proxy's.
- Setting ASPNETCORE_FORWARDEDHEADERS_ENABLED=true at deploy time fixes the keying, as probed. It is neither documented nor configured, and if the upstream is directly reachable, trusting all proxies would let clients spoof X-Forwarded-For.

An undebounced "Refresh room list" button (NativeRelayUI.cs:19) lets ordinary users drain the shared bucket too.

**Suggested fix.** Configure UseForwardedHeaders with KnownProxies/KnownNetworks for the deployment proxy (or key on a trusted forwarded header). Exempt /health. Size the limits per action rather than one global permit pool.

<a id="f14"></a>
### #14 🟠 high — Every horde hit and DoT tick is retained 1 s and serialized into every 20 Hz frame with no budget (the original caps and coalesces horde events)
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (97 (Emit per hit), 40 (DoT ticks); NativeArena.cs:97, 248, 268 (Events kept 1 s, Events.ToArray() in Snapshot); NativeNetwork.cs:58)
- **Original:** src/shared/pve/horde.ts:27-28 (HORDE_EVENTS_PER_TICK=8), 366-386 (20 hit events/s per attacker, coalesced; fields/DoTs aggregated per 0.25 s), 411-416
- **Root issue:** Match frames are far too large
- **Raised as:** medium

**What happens.** In the Find-me or boss phase (72-192 adds), 4 players with burn and poison weapons tag 100 adds. Each source ticks every 0.25 s, so that is about 1,600 Hit events/s, all kept in Events for 1 s. Each 20 Hz frame then JSON-serializes about 1,600 ArenaEvents (about 200 B each, roughly 300 KB per frame before deflate) on the host's main thread, for every peer. The kill-replay recorder deep-copies the whole frame through JsonUtility every 50 ms as well. The original emits at most 8 horde events per tick, coalesces direct hits through a 20/s token bucket and aggregates DoT and field damage per add every 0.25 s.

**Evidence.** HitPveAdd: `Emit(EventKind.Hit,source,m.Id,point??m.Body.position,actual);` on every hit, including each DoT tick from `HitMinion(m,...,(float)(dot.Dps*.25),...)`. NativeArena.cs:97 `Events.RemoveAll(e=>Time-e.Time>1);` is the only bound. Snapshot() returns `Events=Events.ToArray()` and NetworkFrame embeds `snapshot=Snapshot()`, sent every 0.05 s (NativeNetwork.cs:58).

**Verifier's correction.** The Snapshot line is NativeArena.cs:270, not 268. The worst emitter is not DoTs but chip fields, which emit a Hit event at 60 Hz for every add inside every field (NativeChips.cs:121). The original aggregates field damage per add to at most 4 events per second (horde.ts:370-373). With a projectile field chip on a fast weapon (machine gun with black-hole or acid-rain, about 41-47 live fields) and only 3-4 adds per field, more than 8,192 events are retained within 1 s. Every client then rejects the frame (NativeNetworkFrame.cs:32 → NativeNetwork.cs:113/62) and disconnects. At about 9,900 or more events the host's NativeConnection.Encode throws, uncaught, in NativeNetwork.cs:58, and the host's Update is cut short. Floating damage numbers are also spammed (NativeCombatHud.cs:27). Severity rises from medium to high because multiplayer breaks, not just performance.

**Suggested fix.** Port the horde event budget: per-attacker token bucket with coalescing, DoT/field aggregation per 0.25 s, and a per-tick emit cap. Bound Events independently of time.

<a id="f15"></a>
### #15 🟠 high — Elimination replay throws every frame in non-PvE matches and leaks replica arenas into the live physics scene
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeReplay.cs` (23, 46-51 (NativeNetworkFrame.cs:79-80, NativePve.cs:23))
- **Root issue:** The death replay breaks in versus matches, even offline

**What happens.** Play a local Versus match against bots and die. Every local match copies frames via `JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(arena.NetworkFrame()))` (line 23), so each copy carries a non-null default `pve`. Around 0.6 s after death, RenderReplay builds a replica arena and calls `replayArena.ApplyFrame(copy)` before `MoveGameObjectToScene(replayArena.Root,replayScene)` (line 51). ApplyFrame runs Reset(), which creates the root, map cubes and dynamic layer-8 pilot capsules in the active live scene. It then calls ConfigurePve, which throws "PvE requires a reserved authority boss seat". replicaFrame is never set, so this repeats every LateUpdate for the whole replay window (about 4 s). Unity logs an exception each frame, which also triggers the Development-build console. The live renderers are disabled, the camera is not updated and audio is skipped. A fresh replica map and capsules stay in the default physics scene during FixedUpdate's Physics.Simulate. While the dead player spectates, live bullets are absorbed by phantom props (DamageProp ignores props not in Props) and by phantom pilots at the spawn points, so the bots' round outcome can change.

**Evidence.** NativeReplay.cs:51 `if(replaySource!=a){var copy=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(a));replayArena.ApplyFrame(copy,arena.LocalId);SceneManager.MoveGameObjectToScene(replayArena.Root,replayScene);...}`. NativeArena.Reset creates `root=new GameObject(...)` in the active scene, and ConfigurePve throws after assigning Pve. The file's own comment says "A presentation-only scene. It never advances physics or replaces the live match/input state." CONVERSION-STATUS claims an "isolated kill replay".

**Verifier's correction.** "Leaks replica arenas" is overstated. Each retry's Reset() destroys the previous replica root and turns off its colliders, so exactly one phantom replica (map, dynamic props and invisible dynamic pilot capsules) sits in the default physics scene. It is rebuilt every LateUpdate until the replay window ends, about replayEnd-replayStart ≤ 4.25 s of game time, or until Space/Escape, and EndReplay/Dispose then removes it. The rest of the scenario holds: an exception every frame, live renderers disabled, the camera frozen, presentation and audio skipped, phantom colliders in Physics.Simulate, and it affects every non-PvE match (Versus, Teams, Sandbox). Same root cause elsewhere: NativeNetwork.cs:62/107-116 decode network frames with JsonUtility, so living pilots' null `death` becomes a default NativeDeath with an invalid default `wound`. NativeGameFrame.Valid (NativeNetworkFrame.cs:38) likely rejects every frame, or ConfigurePve throws for non-PvE frames, and the client disconnects. That should be filed separately as a probable critical finding.

**Suggested fix.** Construct the replica directly in replayScene: call SetActiveScene(replayScene) around construction, or move the root immediately after every Reset. Never throw from ApplyFrame for presentation copies. Copy frames without JsonUtility, or fix the null semantics.

<a id="f16"></a>
### #16 🟠 high — Socket read/write loops resume on Unity's main-thread SynchronizationContext, so throughput is about fps/2 messages per second; clients below ~120 fps overflow and the 60 fps --ca-server falls behind
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Core/NativeTransport.cs` (24-25, 43-57 (NativeRelay.cs:18-20,36-38; ConversionGame.cs:121; NativeDedicated.cs:15))
- **Root issue:** Network message rate is tied to the frame rate

**What happens.** NativeConnection loops are started from the Unity main thread: listener Accept continuations, the Connect continuation and the relay Join. None of the awaits use ConfigureAwait(false). Every socket completion therefore posts back to UnitySynchronizationContext, which runs posted work once per frame. Each message costs about two frame hops: header plus body on read, header plus data on write. The client sends one reliable input per FixedUpdate (60/s) plus a ping, and Send() fails the connection after 32 queued reliable packets (line 44 "Reliable queue overflow"). A client rendering at 90 fps is disconnected after about 2 s, and at 60 fps after about 1 s. A host below ~122 fps reads each peer's inputs more slowly than they arrive, so input lag grows without bound. The headless authority caps itself at 60 fps (NativeDedicated.cs:15), so every dedicated room degrades. The relay WebSocket stream has the same structure.

**Evidence.** Probe (review-u/engine/netprobe) compiled the real NativeTransport.cs with a frame-pumped SynchronizationContext that matches UnitySynchronizationContext.Exec (snapshot of the queue per frame). It wraps sockets so completions are asynchronous, as Mono sockets are. Results: `fps=144: host received 61.8/s, closed=False`; `fps=90: received 14.3/s; client closed=True 'Reliable queue overflow' at 1.92s`; `fps=60: received 5.0/s; closed at 1.02s`; `client 144 fps -> host 60 fps: client sent 477, host processed 237; newest input seen = #237 (lag 4.00s)` after 8 s, still growing. The author's relay and TCP tests run in a .NET console app, which has no SynchronizationContext, so they cannot show this.

**Verifier's correction.** The client disconnect threshold is about 120 fps or below. At 120 fps the backlog grows only slowly (+0.13 s over 8 s). At 100 fps the client disconnects after about 2.9 s, and at 60 fps after about 1 s. A dedicated server at 60 fps processes about 30 inputs per second per peer, about half the 61 per second each client sends. On the relay path the same limit applies to writes (each NativeWebSocketStream.WriteAsync awaits SendAsync on the Unity context, two per message). Reads there may be partly masked because Mono's managed ClientWebSocket can sometimes return already-received data immediately. Root cause: Mono2x backend (PrototypeBuild.cs:99) plus await continuations captured on UnitySynchronizationContext, which runs them once per frame.

**Suggested fix.** Use ConfigureAwait(false) on every await in NativeTransport/NativeRelay, which touch only thread-safe queues and Interlocked state, or start the loops with Task.Run. Send inputs as a latest-only slot with tap merging (as the source does with coalesceInput/inputs batches) instead of a 32-deep reliable queue.

<a id="f17"></a>
### #17 🟠 high — Replica clients never step physics, so Rigidbody.position writes never reach Transforms while camera, HUD, lock-on and minion rendering read Transforms
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeNetworkFrame.cs` (85, 98-99, 127 (ConversionGame.cs:121,134; NativeSourcePresentation.cs:31; NativeSummonPresentation.cs:18; NativeCombatHud.cs:25-26; NativeLockOn.cs:13-18))
- **Root issue:** Guests see a frozen world

**What happens.** On a network client, FixedUpdate returns for `arena.Replica` before `Physics.Simulate` (ConversionGame.cs:121), so the default physics scene never steps. ApplyMinions creates each remote summon or horde add as `new GameObject("Remote summon")` at the origin, with a kinematic Rigidbody and no interpolation, and then only sets `m.Body.position=mf.position`. Pilots are likewise moved only with `p.Body.position` (lines 85, 127). Unity documents that a Rigidbody.position change reaches the Transform only after the next physics simulation step. Presentation reads `m.Body.transform.position` (NativeSourcePresentation.cs:31), so in Ward Zero co-op every zombie and summon draws stacked at (0,0,0). Their colliders are at the real positions, so shots hit invisible targets. Health bars and lock-on points also use the stale transforms. The client camera focus `p.Body.transform.position` (ConversionGame.cs:134) has the same dependency and is likely anchored at the spawn pad.

**Evidence.** NativeNetworkFrame.cs:98 `var go=new GameObject("Remote summon");...var body=go.AddComponent<Rigidbody>();body.isKinematic=true;...`, line 99 `m.Body.position=mf.position;`. ConversionGame.cs:121 `if(arena.Replica){...return;}` comes before `Physics.Simulate(1f/60)`. Physics.SyncTransforms in InterpolateReplica pushes Transform→PhysX only, not the reverse.

**Verifier's correction.** The failure scenario holds, with two refinements. (1) On clients, hit resolution belongs to the host, so "shots hit invisible targets" means the host's zombies are at their real positions and can attack the player while the client draws them at the origin. The client-side colliders, which are at the correct positions, still affect AimPoint raycasts and the lock-on Linecast, while the lock-on points and the eye use the stale Transforms. (2) The same defect also affects killcam replays, including on the host. replayArena is a Replica that is never simulated, so ApplyMinions there also creates "Remote summon" objects whose Transforms stay at the origin. Only the replay's pilot camera is safe, because it uses Body.position. Fix: on replicas, write Transforms (transform.SetPositionAndRotation followed by Physics.SyncTransforms) instead of Rigidbody.position, or switch every presentation and HUD read to Rigidbody.position.

**Suggested fix.** On replicas, write Transforms (`transform.SetPositionAndRotation`, then Physics.SyncTransforms) instead of Rigidbody.position, or read Rigidbody.position everywhere presentation needs replicated poses.

<a id="f18"></a>
### #18 🟡 medium — Signing in to an account with no saved pilot overwrites the local pilot with server defaults instead of uploading it
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Runtime/NativeProfileUI.cs` (26)
- **Original:** src/client/main.ts:391, 407; src/relay/accounts.ts:22-23

**What happens.** A player sets name 'Nova', accent #00ff00, hat 'crown', kill effect 'confetti' and weapon Rifle, then registers (or signs in to an account that never saved a pilot). The server row's profile is '{}', so the response is profile {name:'Pilot', color:'#ff6b35', cosmetics:{hat:'none', killEffect:'impact-ragdoll', intro:'neon-drop', ...}} with hasProfile:false. Accepted(load=true) applies it unconditionally and calls Save(). Name becomes Pilot, accent turns orange, hat and effect are cleared, the custom entrance and override are reset, and the weapon becomes Pistol (preferredSpec is absent, so the NativeCosmetics default 'pistol' is used). Nothing is uploaded, so hasProfile stays false. The same overwrite repeats on every launch with a remembered session until the player manually clicks 'Save current profile to account'.

**Evidence.** NativeProfileUI.cs:26: `if(!load)return;var p=data.profile;if(p==null)return;prefs.name=NativeCosmeticRules.Name(p.name);prefs.accentColor=NativeCosmeticRules.Color(p.color,...);... prefs.hat=...;prefs.killEffect=...;prefs.intro=...;prefs.introText=c.introText;prefs.overrideIntro=c.overrideWeaponIntro;if(content.weapons.Any(w=>w.id==c.preferredSpec&&AllowedWeapon(w)))prefs.weapon=c.preferredSpec;...Save();`. grep shows NativeAccountData.hasProfile is declared (NativeAccount.cs:13) but never read. The original only loads when a profile exists and otherwise uploads the local pilot: main.ts:391 `if(loadPilot&&a.hasProfile){Object.assign(pref,{...a.profile,...})}` and main.ts:407 `applyAccount(true);if(!accounts.account!.hasProfile){...await accounts.save({name:pref.name,class:pref.class,color:pref.color,cosmetics:pref.cosmetics});...}`. A tsx run of the relay's savedProfile on a new row ('{}', drizzle/0004 default) gives {"name":"Pilot","color":"#ff6b35","cosmetics":{"intro":"neon-drop","bodyColor":"#e5e7eb","hat":"none","killEffect":"impact-ragdoll",...}},"hasProfile":false.

**Verifier's correction.** The core claim is accurate (Unity NativeProfileUI.cs:26 and NativeAccount.cs:51; TS main.ts:378, :391, :407; relay accounts.ts:23). One caveat: "weapon becomes Pistol" depends on JsonUtility keeping the field initializer preferredSpec="pistol" for a missing key. If it does not, preferredSpec is null and the weapon is left unchanged.

Related point for the fix: NativeAccount.cs:51 sets `RecoveryCode=response.recoveryCode??""` on every successful response. A profile save straight after registration therefore wipes the recovery code shown on screen, which the TS register path keeps on purpose (`accounts.recoveryCode=code`). Today that happens whenever a newly registered player presses either save button. If the hasProfile upload is added without keeping the code, it will happen automatically before the player can copy it.

**Suggested fix.** In Accepted, apply data.profile only when data.hasProfile. When authenticating with hasProfile false, call account.Save(Profile()) and keep the recovery code, as the TS client does.

<a id="f19"></a>
### #19 🟡 medium — A corrupt or briefly unwritable preferences file replaces the whole game UI with an error screen that never clears
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (27, 40, 44, 172 (also NativeAccount.cs:28 + NativeProfileUI.cs:27))

**What happens.** (a) native-preferences-v1.json is left partially written or zero-filled by a crash or power loss. File.WriteAllText truncates in place, and Save() runs on every V press (line 67), match start, glow, settings and profile change. On the next launch JsonUtility.FromJson throws ArgumentException. Awake's catch-all sets `error`, so InitializeAccount, the camera, audio and settings are skipped. OnGUI then shows only the stack trace and returns, with no reset path; the player has to find and delete the file under LocalLow. (b) A transient IOException in Save, such as a sharing violation from antivirus or a backup tool while V is pressed mid-match, sets `error`. Nothing ever clears it, so OnGUI hides the HUD and all menus for the rest of the session while the match keeps running underneath. (c) UnauthorizedAccessException is not caught by Save. It is also not caught when reading conversion-account.bin in NativeAccount.Configure (line 28 catches only IOException, CryptographicException and NotSupportedException), and InitializeAccount catches only ArgumentException, so a permissions problem on the session file reaches the same Awake catch and full-screen error.

**Evidence.** Line 27: `prefs=File.Exists(PreferencePath)?JsonUtility.FromJson<ConversionPreferences>(File.ReadAllText(PreferencePath)):new ConversionPreferences();` sits inside the Awake try whose catch is `catch(Exception e){error=e.ToString();...}` (line 40). Line 44: `File.WriteAllText(PreferencePath,...)}catch(IOException e){error="Could not save test preferences: "+e.Message;}`. Line 172: `if(error.Length>0){GUI.Box(...);GUI.Label(...,error,label);return;}`. grep shows `error` is only ever assigned and never reset to "".

**Verifier's correction.** A zero-length preferences file is handled: JsonUtility.FromJson returns null for "", and line 28 substitutes defaults. Only non-empty invalid content (NUL-filled, truncated JSON, a hand edit) causes the permanent error screen. An UnauthorizedAccessException thrown by Save (ConversionGame.cs:44) does not set `error`. It propagates uncaught, so with a read-only preferences file StartMatch (line 51, before the match is created) throws on every Start/Rematch press and no match can start. From Update (line 67) it only aborts the frame. The session-file path in NativeAccount.Configure:28 does reach the Awake full-screen error as described. Line 59 (`error="A match needs at least two opposing sides."`) is a further case of the same never-cleared fatal `error` field.

**Suggested fix.** Load the preferences in their own try/catch, rename a bad file to .bak and fall back to defaults. Write to a temp file and swap it in with File.Replace. Report save failures as a dismissible notice instead of the fatal `error` field, and catch UnauthorizedAccessException in Save and in the session-file read.

<a id="f20"></a>
### #20 🟡 medium — DEV and grant access expires mid-session: the sandbox dummy's scoped grant is never refreshed, and in-match access depends on 15-minute token refreshes
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Runtime/NativeSandbox.cs` (27 (also 10; Core/NativeAccess.cs:14-16; Runtime/NativeFighters.cs:23))
- **Original:** src/shared/sandbox-tools.ts dummy-cast (d.unlocks=['weapon:'+spec]); src/client/host-worker.ts:5; src/server/index.ts:46

**What happens.** A dev opens the local sandbox, sets the dummy to Emperor with key 'ai' and repeat 0, and practises against it. ScopedWeapon copies the dev token's exp, which has 10 to 15 minutes left because tokens are 15-minute claims refreshed every 5 minutes. NativeAccount refreshes Local.Seat.access, but nothing replaces the dummy's seat access: SandboxRestore and the respawn path do not touch it. 10 to 15 minutes after the cast, Dev(dummy,'emperor') returns false, NativeFighters calls CancelFighter, and the dummy stands there with Emperor equipped but no Emperor kit. The same expiry hits real players: if account refreshes fail for about 10 minutes (service blip or offline), Allows() turns false mid-match. Emperor, Ansatsuken, Leet and Siren kits drop, SandboxAllowed goes false (dummy respawn and round hold stop), and DEV PvE chips are filtered. The original verifies access once at join and keeps it for the room.

**Evidence.** NativeAccess.cs:14 `Allows(key,now)=>Expires>now&&(...)`, :16 `ScopedWeapon(...){...var access=new NativeAccess{Expires=owner.Expires};...}`. NativeSandbox.cs:27 calls ScopedWeapon only inside SandboxCast. NativeFighters.cs:23 `Dev(...)=>...p.Seat.access.Allows("weapon:"+spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())`. Probe on .NET 8 with a WebCrypto-signed dev token and test key: `scoped now=True scoped+16min=False`; `emperor=True emperorAfter16min=False`. Original: sandbox-tools.ts dummy-cast `d.unlocks=WEAPONS[spec].devOnly?['weapon:'+spec]:[]` (never expires); host-worker.ts:5 / server/index.ts:46 `verifyPlayerToken(...)` once at join, and the Access has no expiry. CONVERSION-STATUS does not document this as deliberate.

**Verifier's correction.** The real-player expiry window is 10 to 15 minutes after account refreshes start failing (the last good token had up to 15 minutes left, and failures retry every 60 s). It is not "about 10 minutes". The dummy symptom only appears with SandboxRepeat=0 (the default) and no manual recast. Pressing "Make dummy cast" again, or setting a repeat interval, re-derives the grant from the refreshed local access.

**Suggested fix.** Give the dummy a non-expiring scoped grant, or re-derive it from Local.Seat.access whenever local access is refreshed. Decide explicitly whether in-room access is pinned at join (TS behaviour) or gets a grace period, and document the choice.

<a id="f21"></a>
### #21 🟡 medium — Captured cuts have no phrase footwork: feet skate 25-81 cm across the floor on every melee attack
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (156, 165-177, 201-216)
- **Original:** src/client/mocap/mocap-layer.ts:76-97 (mocapCut legs:0 + phraseFootwork); src/client/primary-melee-motion.ts:253-322 (phraseFootwork, feet, seatHips, reachable)
- **Raised as:** high

**What happens.** Any primary melee attack. The authoritative step moves the body forward (sword 0.65 m, daggers 0.85 m, shield 0.55 m, greatsword 0.45 m over StepAt..StepUntil). Meanwhile Unity plays the cut clip's own captured legs with the capture's root travel removed, so the legs hang from the moving root. In the source the cut poses only the upper body (legs:0). The feet come from phraseFootwork: steps planted against the authoritative travel, seatHips lowering the hips so a planted foot reaches the floor, and a reach limit on lifted steps. None of this exists in Unity.

**Evidence.** `moving` excludes `attack` (line 156), so the procedural feet never run during a cut. Sample(set,cut,...) at line 170 writes every bone, legs included. A grep of Assets/ChipArena finds no footwork, seatHips, replant or reachable. Probe (Core CapturedRig + CapturedClip, real clip data, Unity retiming, NativeMeleeRules step distances): the foot that moves least still slides over the floor during the step window. sword cutA 42 cm, cutB 66 cm; daggers cutA 81 cm, cutC 76 cm; shield cutA 45 cm; greatsword cutA 36 cm, cutB 25 cm. Not documented in CONVERSION-STATUS.md, which says 'captured animation are retained'. Only the historical lab section of ANIMATIONS.md mentions that 'attack footwork' was not ported.

**Verifier's correction.** Severity should be medium, not high: the defect is presentation only and does not affect gameplay outcomes. The cited lines and the failure scenario are correct. One clarification: a planted foot skates by the full authoritative travel (about 0.45-0.9 m while in floor contact), not only the "foot that moves least" figure; e.g. the daggers cutA right foot stays on the floor for the whole step window and slides about 0.8-0.9 m.

**Suggested fix.** Port phraseFootwork/feet/seatHips/reachable. Pose the cut with legs:0 and solve both legs with CapturedRig.Leg against the swing's authoritative step travel, the way the source does.

<a id="f22"></a>
### #22 🟡 medium — Blade/body clearance solver not ported: greatsword blade sits inside the head during idle guard and block
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Core/CapturedRig.cs` (117-140)
- **Original:** src/client/mocap/mocap-pose.ts:66 (clearBlade on every applyMocapPose); src/client/mocap/blade-clearance.ts:88-134 (clearBlade); mocap-layer.ts:33-36 (fit options)
- **Raised as:** high

**What happens.** A greatsword player stands in guard or blocks; a sword player throws cut 2. The source runs clearBlade on every captured pose: a full clear for idle, carries and block, and a turn-only clear for cuts with continuous normals, medial fade, the pommel skipped and per-part broad phase. That keeps the human-capture blade out of the larger voxel head, chest and shoulders. Unity copies the raw captured blade direction straight into the wrist.

**Evidence.** CapturedRig.Sample builds the wrist from rows 42-47 and calls Arm() with no clearance step. There is no clearBlade equivalent anywhere in Assets/ChipArena. Probe: blade centreline from 0.26 m to the tip, signed distance against the rig's own Spine/Chest/Neck/Head/clavicle/upper-arm boxes over the shown clip ranges. greatsword/idle is more than 3 cm inside the head in 135/201 samples (deepest 9.4 cm). greatsword/block: 201/201 samples (6.8 cm). greatsword/cutB: 11.3 cm. greatsword/stagger: 9.5 cm. sword/cutB passes 5.3 cm through the left clavicle in 42/201 samples. CONVERSION-STATUS.md claims 'Source geometry, textures, timing and captured animation are retained' and lists no animation adapter. Only the historical ANIMATIONS.md section says 'The original full blade/body-clearance solver ... [is] not ported'.

**Verifier's correction.** Severity should be medium: this is visible self-clipping only, with no gameplay or network effect, and ANIMATIONS.md and DEV_LOG partly document it, though from when greatsword was viewer-only. The main impact is the greatsword guard idle (blade >3 cm inside the head in 142/201 samples, worst 9.4 cm), block (156/201, 7.2 cm) and stagger (70/201, 9.6 cm). The source's full clear removes all three completely. Greatsword cuts lose a smaller improvement (cutA -9.4 to -2.5 cm, cutB -11.6 to -4.4 cm). The sword/cutB example is not a meaningful parity loss, because the source's turn-only clear still leaves it 4.0 cm inside the left shoulder cap. The fix belongs in CapturedRig.Sample before Arm() at CapturedRig.cs:124-126, or as a runtime pass in CapturedAnimation.ActorPose, using source obstacles that match CapturedRig.Boxes on the Chest, Neck, Head and clavicle bones (excluding the chest back-light).

**Suggested fix.** Port clearBlade, including its turn-only mode for cuts, against the source obstacle parts, and apply it before Arm() in CapturedRig.Sample or at runtime with the per-set halfWidth/pommel fit.

<a id="f23"></a>
### #23 🟡 medium — Melee E specials push the legs 23-38 cm through the floor; VALIDATION's 'voxel floor clearance' claim does not cover specials
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Core/NativeSpecialPose.cs` (36-43, 57-65)
- **Original:** src/client/melee-motion.ts:43-71 (specialMotion) followed by src/client/character-animation.ts:78 (groundPass re-plants the legs)
- **Raised as:** high

**What happens.** A player uses Sword launcher, Shield throw, Dagger throw or the Greatsword wolf-spin. Special() lowers Body by up to 0.23 m (-.07*coil and -.16*coil) and blends the hips and knees to fixed angles. Unity has no ground pass after this layer; the source runs groundPass after it. The rig's legs and shell end up under the floor.

**Evidence.** Probe: captured idle of the matching set, then NativeSpecialPose.Special. sword-launch: lowest voxel -1.246 against ground -0.865 (38 cm down), both ankles at -1.10 (normal is about -0.795). shield-throw: -1.164. dagger-throw: -1.095. Wolf-spin windup: -1.143. Wolf-spin 36 ms after landing: -1.238. CapturedAnimation.ActorPose (lines 230-234) applies Special and then writes the pose with no floor fix. VALIDATION.md says '2,400 native special/recovery pose samples check ... voxel floor clearance'. In NativeSpecialChecks.cs:62 the special samples call Pose(x,y,false), so the floor assertion (line 79, `if(floor)`) only runs for the recovery samples, which also start from a default pose rather than the captured idle.

**Verifier's correction.** Severity should be medium: a visible presentation divergence with no gameplay effect. The wolf-spin post-landing window is about 0.55 s/attackRate, because NativeMeleeSpecials.cs:73 sets Until=Time+.55/attackRate on landing. Within it, sinking eases from 38 cm to about 17 cm; it does not persist up to the 3.5 s cap. The source's protection comes from motion-quality.ts:313, the groundPass branch that clamps each layered ankle to FLOOR=-.795 and re-solves the leg. A fix that mirrors it: after Special (and Recover), when grounded and not first-person, read each ankle's world position, clamp y to -0.795, and re-solve with CapturedRig.Leg. Then enable the floor assertion for special samples taken from captured idle poses.

**Suggested fix.** After Special/Recover, re-solve grounded legs with CapturedRig.Leg to their pre-special ankle positions (the source groundPass behaviour) or at least clamp with Lowest(), and assert floor clearance for special samples starting from captured poses.

<a id="f24"></a>
### #24 🟡 medium — Magic casts sink the feet about 8.5 cm into the floor and slide them 7-9 cm
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Core/MagicPose.cs` (86-87, 97-102)
- **Original:** src/client/magic-motion.ts:109-111 ('feet stay planted; the ground pass bends the knees'); character-animation.ts:78 groundPass

**What happens.** Any fire, water, lightning or darkness primary cast while standing. Apply resets the pelvis, then sets pelvis twist, Body pitch o[8], forward shift o[9] and drop o[10]. The legs are captured idle FK legs hanging from the pelvis, and nothing re-plants them. The source relies on groundPass to keep the feet planted.

**Evidence.** Probe: sword idle (VisualSet 0 is used for magic actors), then MagicPose.Apply over a cast. Idle feet lowest -0.845. During the cast the lowest point reaches -0.949 for fire (age .245 s), -0.948 water, -0.952 lightning, -0.945 darkness, against ground -0.865. Ankles shift horizontally up to 7.3 / 8.4 / 6.8 / 9.2 cm. At rest (no cast) the shift is 1.5 cm and there is no sinking. ActorPose calls MagicPose.Apply (line 218) after the procedural legs and does no floor or plant pass afterwards.

**Verifier's correction.** In this probe the idle lowest point is about -0.865, not -0.845. The ~8.4 cm floor penetration is at the toe box, caused by the body pitch (o[8], up to 0.2 rad) carrying the whole leg chain. The ankle joints drop about 7.5-8 cm from their idle height, to about -0.82, which is ~2.5 cm below the source's planted -0.795. Ankles slide up to 7-9 cm horizontally; the pelvis twist makes the two feet move asymmetrically, about -2.4 cm on one and +7.1 cm on the other. The same applies while moving, because the procedural travel legs (CapturedAnimation.cs:206-215) are solved before MagicPose.Apply. Files: Assets/ChipArena/Core/MagicPose.cs:97-102 and Assets/ChipArena/Runtime/CapturedAnimation.cs:218 (no re-plant afterwards). Source: src/client/magic-motion.ts:109-111 together with src/client/motion-quality.ts:294-317 (groundPass, invoked at character-animation.ts:78).

**Suggested fix.** Re-plant both legs after MagicPose (store the ankle targets before the layer and solve CapturedRig.Leg), or apply the body weight-shift above the hips only.

<a id="f25"></a>
### #25 🟡 medium — Locomotion feet jump 15-28 cm whenever a player starts or stops, and skate about 20 cm per step when sprinting
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (156-164, 190-191, 201-216)
- **Original:** src/client/mocap/mocap-layer.ts:55-58, 112-128 (gait owns the legs, replant); src/client/motion-quality.ts gait/gaitFoot

**What happens.** (1) A player walks then stops, or starts walking, or attacks out of a run. The legs switch in one frame between the procedural stance (ankles at x = ±0.13) and the captured idle, cut or block legs. NativePoseHistory smooths only the base, before the procedural feet overwrite it, and it is disabled entirely on attack frames. The source keeps the gait's legs under the guard, block and stagger (gait:true, legs:0, replant) precisely to avoid this. (2) At sprint speed (6.8 × 1.35 = 9.18 m/s) the cadence clamp of 3.7/s combined with the stride clamp of 0.42 m lets the planted foot travel back at only 7.4 m/s.

**Evidence.** Probe on the captured stances: sword idle ankles L(0.32,0.20) R(-0.26,-0.07) give a jump of 28/15 cm from the gait rest. Block: 24/25 cm. daggers block: 27/28 cm. greatsword idle: 26/22 cm. shield block: 25/28 cm. Formula probe of lines 157 and 209-213: no slip at 2.8 or 4 m/s; 0.32 m/s at 6.8 m/s; 1.78 m/s (20.2 cm per stance) at 9.18 m/s; 3.62 m/s (41 cm) at 11 m/s with a speed chip. On attack frames BlendBase(pose,time,attack||roll) uses weight 1, so the walk carry also drops to the idle base in one frame at the start of a cut.

**Verifier's correction.** Unity file: Assets/ChipArena/Runtime/CapturedAnimation.cs, lines 156-164 (moving gate, cadence cap), 169-171 and 191 (an attack enters from the idle sample with history weight 1), 201-216 (procedural feet applied only while moving, after BlendBase), and Core/CapturedRig.cs:169 (reach clamp of 0.497).

Two corrections to the finding's detail:
1. The stop jump does not go straight to the captured idle. On the first standing frame the legs pop to the history-blended base (walk/run legs slerped about 31% toward idle), then settle into the idle over about 0.1 s. The one-frame pop is still 10-21 cm.
2. Skating is not limited to sprinting. The leg IK cannot reach the ±0.42 m stride targets (the hip is 0.46 m above the ankle target, so horizontal reach is about 0.19 m). Planted feet therefore hover up to about 6 cm at contact and slide about 21 cm per stance at the normal 6.8 m/s run, and about 37 cm per stance when sprinting at 9.18 m/s, not about 20 cm.

**Suggested fix.** Keep the procedural gait legs under standing guard, block and stagger (replant), blend the leg layer across the moving/standing transition, and derive cadence and stride from speed without clamps that break the plant.

<a id="f26"></a>
### #26 🟡 medium — Echo Strike follow-ups: contact pose shown up to 0.34 s after the hit, and a one-frame snap of up to 2.85 m at the blade tip
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (165-171 (with NativeArena.cs:268, NativeMelee.cs:58))
- **Original:** src/shared/simulation.ts:387 (follow-up swing.at = time + followupGap/rate); character-animation.ts:29 (windup = swingAt - lastAttack); mocap-layer.ts:71-74 (mocapFollowupBase); melee-motion.ts:24-25

**What happens.** A melee player with Echo Strike, or Hurricane Edge, which adds 2 echoes. NativeMelee starts each follow-up at the previous swing's HitUntil, with HitAt = start + .08/rate. The snapshot still reports VisualWindup = weapon.windup/rate, so the captured cut is retimed as if the windup were 0.42 s (greatsword) or 0.14 s (sword). The follow-up also blends in over 40 ms from the idle base. Attack frames disable history smoothing, so the previous frame's contact pose snaps to idle.

**Evidence.** NativeArena.cs:268 `a.VisualWindup=(float)(p.Weapon.windup/p.Stats["attackRate"])` is set for every swing, follow-ups included. Probe with CapturedClip.AttackTime: for a greatsword follow-up the hit window (age .08-.24 s) shows clip time .117-.349, which is still the windup; the contact marks .475-.525 appear at age .42-.58 s. Sword shows .176-.324 against contact .300-.342. Jump from the contact-end pose (m[2]) to idle in one frame: sword cutC blade tip 235 cm, greatsword cutA 285 cm, daggers cutA 168 cm.

**Verifier's correction.** The core claim is accurate. Refinements: (1) For greatsword with Hurricane Edge, follow-up 1 never reaches its contact pose: follow-up 2 replaces it at age .24 while the clip is still in the windup. (2) The follow-up recovery is also retimed wrong: VisualDuration-derived .16 s for the greatsword where TS uses meleeRecovery/rate = .5 s. (3) The affected window is weapon-dependent: greatsword .34 s, shield .12 s, sword .06 s, daggers about .01 s. The one-frame snap (no follow-up base, no history smoothing on attack frames) affects all four melee sets: up to about 2.7-2.9 m at the blade tip for sword and greatsword, 1.6 m for daggers and 0.8 m for shield. The snapshot line is NativeArena.cs:268, and the attack block is CapturedAnimation.cs:165-176.

**Suggested fix.** Transmit the swing's actual HitAt/HitUntil (windup = HitAt - AttackAt, window = HitUntil - HitAt) and, for follow-ups, blend from the previous cut's m[2] pose (mocapFollowupBase).

<a id="f27"></a>
### #27 🟡 medium — Daggers' 4th-cut spin and Hurricane Edge's full turn are never shown, although gameplay sweeps 360°
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (165-177)
- **Original:** src/client/mocap/mocap-layer.ts:92-95; src/client/primary-melee-motion.ts:71 (spin = daggers index%4===3), 251, 289-299

**What happens.** Daggers cut 4: in the source the body turns a full circle over strike and recovery, with the hopped footwork. Hurricane Edge chip (meleeArc set 8, a full 360° sweep in NativeMeleeRules.Angle): the source adds a 2π body turn over the strike window. Unity adds no turn, so a Hurricane player hits all around while visibly doing a normal forward cut. Daggers cutD uses the same capture as cutB ('Dagger-Attack-L1'), so without the spin the 4th cut looks identical to the 2nd.

**Evidence.** There is no body-yaw term in ActorPose, and grep finds no 'hurricane' or spin handling in Assets/ChipArena presentation. CapturedClips.Generated.cs: daggers/cutD and daggers/cutB have identical data strings.

**Verifier's correction.** The file and lines are correct. One addition for the fix: Hurricane needs a presentation field (for example a chip flag on Actor, populated in NativeArena.Snapshot and carried by the native protocol for remote clients), because ActorPose currently has no way to know the chip is equipped.

**Suggested fix.** Add the source turn to the Body bone rotation for daggers index%4==3 (over window+recovery) and for chips.hurricane (over the window), with the spin footwork.

<a id="f28"></a>
### #28 🟡 medium — Runtime blade floor guard uses a 1.28 m sword length for every set: dagger wrists bent up to 63°, left dagger and shield unguarded
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (232)
- **Original:** src/client/mocap/mocap-layer.ts:45-51 (mocapFloorGuard); src/client/mocap/mocap-pose.ts:81-101 (keepBladesOffFloor uses each weapon's real blade-tip, both hands, kept axis near vertical)

**What happens.** Every grounded frame for every non-gun, non-magic actor runs `CapturedRig.BladeFloor(pose,false,1.28f)`. That covers daggers (drawn blade ~0.83 m), greatsword (~1.68 m), shield (a plate) and the DEV fighters. Preview() and Tests/AnimationChecks.cs use the correct lengths 1.69/.84/1.28, so the tests pass while the game path is wrong.

**Evidence.** Probe over the shown clip ranges. daggers right wrist is rotated when the real dagger tip is already above the plane in 542/1802 samples: up to 49.8° in run, 62.9° in cutA, 28.1° for the whole block, 32.3° in stagger. The left dagger is never guarded and dips to -0.878 (idle) and -0.881 (stagger), below the ground at -0.865. The guard also switches off abruptly at `wrist.Y<=floor`, which adds a 7.4° step within 0.58 ms of clip time in daggers cutA. The source applies keepBladesOffFloor only to sets with fit.floor, using each weapon's own tip and the off-hand weapon too.

**Verifier's correction.** **Location:** Assets/ChipArena/Runtime/CapturedAnimation.cs:232 diverges from src/client/mocap/mocap-pose.ts:81-100 and src/client/mocap/mocap-layer.ts:45-47.

**Corrected failure scenario**
- **Right dagger.** For a daggers player (VisualSet 1), the guard acts on a 1.28 m blade that isn't there instead of the 0.84 m drawn dagger. The right wrist is rotated unnecessarily in about 35% of displayed samples: more than 10° in 16% and more than 20° in 5%, up to about 33° (cutC, blockHit, block, cutA). Where a lift is needed, it lifts too far.
- **Left dagger.** It is never guarded. Its tip sits below the 8.5 cm clearance plane, with the wrist above it, in about 18% of samples. In some samples it goes up to 5 cm into the ground (minimum -0.918 in blockHit; idle -0.884; stagger -0.888).
- **Siren.** It also goes through the guard, which the source never does: right wrist bent up to 20.7° in slam and rush.
- **Not affected.** For greatsword and shield the wrong length has no observable effect, since the guard never fires in their clips. Emperor and ansatsuken never trigger it either.
- **Not a divergence.** The cutoff at `wrist.Y<=floor` matches the source.

**Fix**
- Pass the per-set length (VisualSet 2 → 1.69, 1 → .84, 0 → 1.28).
- Call `BladeFloor(pose,true,.84f)` for daggers.
- Limit the guard to the four mocap specs (sword, daggers, shield, greatsword) so Siren and the fighters are excluded.
- Optionally guard the shield with its (0,.4,.29) tip.

**Suggested fix.** Use the per-set blade length (as Preview does), guard the left dagger, skip the shield/fighters, and port the kept-axis continuity of keepBladesOffFloor.

<a id="f29"></a>
### #29 🟡 medium — First-person melee uses the third-person captured clips; the dagger view model is off-screen most of the idle loop
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeViewPresentation.cs` (56-60, 84)
- **Original:** src/client/mocap/mocap-layer.ts:41 (mocapSet returns undefined for isFirstPersonRig: 'The first-person view model keeps its procedural poses, which are framed for the camera'); first-person-view.ts:17

**What happens.** A melee player in first person (V toggle or ADS). viewRig.ActorPose(shown,...,true) samples the same captured idle/walk/cut/block clips and then places the rig with the source's procedural-pose offsets (.16,-.22,.30) and scale .78. Those offsets were tuned for the procedural first-person poses.

**Evidence.** Projection probe (75° vertical FOV, 16:9, offset and scale exactly as in RenderFirstPerson): for daggers/idle, any forearm, hand or dagger point is on screen in only 22/101 samples. The blades are 2.8 NDC off screen at the closest point, so the player sees no weapon at all while standing. The source excludes first-person rigs from the mocap layer for this reason.

**Verifier's correction.** The scenario applies only in V/menu first person, not ADS: melee has no ADS profile. With daggers idle, the dagger blades are never on screen (0/101 samples). The forearms, hands and dagger grips/pommels are still partly visible along the bottom edge (up to NDC y -0.70) in every sample. The finding's "22/101, no weapon at all" overstates this. The source's procedural first-person daggers sit at the same screen height (max y -0.71) but show their blades in 121/121 frames. The weapon draw that matters in the runtime is SourceCharacter at NativeViewPresentation.cs:78 (weapon-daggers source model on MeleeHand). Line 84 is only the box fallback. Root cause: NativeViewPresentation.cs:58 → CapturedAnimation.cs:147-235 samples the captured clips for the view rig, whereas source mocap-layer.ts:41 excludes it.

**Suggested fix.** Port the source's procedural first-person melee poses, or exclude the view rig from the captured clips as the source does.

<a id="f30"></a>
### #30 🟡 medium — No muzzle flash, slide recoil or magazine choreography for anyone in the native game: GunPresentation is unreachable once source models are active
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/ArenaPresentation.cs` (186, 192-201 (and NativeViewPresentation.cs:78-82, GunPresentation.cs:47-72))
- **Original:** src/client/weapon-visuals.ts:59, 109 (muzzle-flash shown for 75 ms per shot); src/client/ranged-motion.ts:196-215 (magazine strip/drop/seat)

**What happens.** In ConversionGame, Initialize(false) creates NativeSourceModels and every snapshot actor has SourceSpec set. SourceCharacter() therefore returns true, and CapturedPose and RenderFirstPerson return before GunPresentation.Draw. The exported weapon model merges the magazine into the hand-anchored mesh, and the source's muzzle-flash mesh is invisible at export time, so it is skipped.

**Evidence.** ArenaPresentation.cs:186 `if(SourceCharacter(rig,a,time))return;` comes before line 200 `GunPresentation.Draw(...)`. NativeViewPresentation.cs:78 is `if(!ScopeActive&&!SourceCharacter(viewRig,shown,time,true)){... if(gun)GunPresentation.Draw(...)}`. export_models.mjs groups sections by [anchor,tex,...] and skips invisible meshes. A grep finds no other muzzle-flash code (NativeCombatGlow has none).

**Verifier's correction.** In the original, the pistol slide moves only during the reload charge (ranged-motion.ts:222, u .83-.94), not as per-shot recoil. GunPresentation's `-.04f*kick` per-shot slide offset is the Unity code's own addition, so the "slide recoil" part of the finding should read "pistol slide rack during reload". The missing muzzle flash (75 ms per shot, in first and third person) and the missing magazine strip/drop/seat choreography are confirmed for all players, local and replica, in ConversionGame.

**Suggested fix.** Draw the muzzle flash, slide and magazine from GunPresentation (or separate source-model parts) in addition to the source weapon model.

<a id="f31"></a>
### #31 🟡 medium — Stereo panning is reversed: sounds on the right play in the left ear
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeAudio.cs` (25, 51)
- **Original:** src/client/audio.ts:86

**What happens.** An enemy fires or hits cover to the player's right. The pan formula `(-cos(yaw)*dx + sin(yaw)*dz)` was copied from three.js, where the camera right vector is (-cos yaw, 0, sin yaw). Unity is left-handed: ConversionGame.UpdateCamera uses Quaternion.Euler(pitch,yaw,0), whose right vector is (cos yaw, 0, -sin yaw). The formula therefore computes -dot(d, right).

**Evidence.** For d = 5*cameraRight = 5*(cos y,0,-sin y): pan = (-cos y*5cos y + sin y*(-5 sin y))/5 = -1, clamped to -0.85, which is the left speaker (AudioSource.panStereo -1 = left). Gun direction (GunRules.Direction) and camera forward are both (sin yaw, cos yaw), so the world is not mirrored back to compensate.

**Verifier's correction.** The same mirrored pan also applies during kill-cam replays: NativeReplay.cs:58 calls nativeAudio.Render with a camera built by Quaternion.Euler(-Pitch, Yaw, 0). A smaller, separate divergence: Unity uses the camera position as the listener (NativeAudio.cs:51), while the original uses the local player's position (audio.ts:122, `this.listener=self.pos`). In third person this shifts distance attenuation and panning slightly, but it is not the main defect.

**Suggested fix.** Use pan = Vector3.Dot(d, camera.transform.right)/max(2,|d|) (or negate the formula).

<a id="f32"></a>
### #32 🟡 medium — Directional hit reactions (impulse flinch, light-hit recoil, parry reaction) are not ported
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (183-188 (Actor model: Core/ArenaModel.cs:38-66))
- **Original:** src/client/hit-reaction-motion.ts:22-60; character-animation.ts:60, 74

**What happens.** A player is shot, hit by magic, or hit without stun. The source layers a damped, direction-dependent pelvis/chest/head/arm reaction on every hit (scaled .35 without stun), plus a parry reaction. Unity only shows the captured stagger, and only for melee actors while stunned, at a constant 0.7 weight without the source's frontal scaling. Gun and magic victims show at most a spine/head tilt during stun (RangedPose line 77) and nothing for ordinary hits. The Actor model has no hit-reaction data (direction, zone, strength, turn) to drive one. The only other per-hit cue in Unity, the FlashUntil white tint, exists only in the box path that SourceCharacter bypasses (ArenaPresentation.cs:134,189).

**Evidence.** grep -i 'flinch|hitreaction' over Assets/ChipArena finds only the State label 'hit reaction' at CapturedAnimation.cs:187. CONVERSION-STATUS.md does not list this as a deliberate omission.

**Verifier's correction.** Files and lines are correct: CapturedAnimation.cs:183-188 and ArenaModel.cs:38-66, against source hit-reaction-motion.ts:22-71, character-animation.ts:60/62/63/74, mocap-layer.ts:145-150 and hit-reaction.ts:11-19. Corrections:
1. Magic victims get no stun motion at all; only gun weapons get the RangedPose.cs:77 tilt.
2. A stunned shield actor (no stagger capture) blends toward its idle clip instead of staggering.
3. Unity has other per-hit cues (audio, victim camera shake, damage numbers, impact sprays), but the native authority never sets FlashUntil, so the white tint never occurs in native play.
4. The source's hit-yaw lock during stun (motion-quality.ts:120) is also missing.

**Suggested fix.** Replicate hit-reaction direction/strength/zone/at in the actor snapshot and port hitReactionMotion as a layer after the base.

<a id="f33"></a>
### #33 🟡 medium — Horde minions play gait clips baked at fixed speeds, in lockstep per variant, with a hard walk/run switch: feet skate
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeSourcePresentation.cs` (36-37 (with NativeSourceMotion.cs:18, tools/export_motion.mjs:22))
- **Original:** src/client/siren/minions/motion.ts:101-134 (speed-driven cadence, ground-anchored feet, walk/run cross-fade between runFrom 2.0 and runTo 3.0, per-minion gait phase)

**What happens.** Ward Zero hordes move at varied speeds: walk and run stats, crawl ×.35, limp ×.55, wave speed scaling. Unity selects 'walk' (baked at vel 1.6) or 'run' (baked at vel 3.5) on a single 2.5 m/s threshold and samples the loop at Mathf.Repeat(arena.Time). The cadence never follows the actual speed, and every minion of the same variant (Id%6) moves its legs in sync.

**Evidence.** export_motion.mjs:22 `['walk',1.5,true,{vel:{x:0,y:0,z:1.6}}],['run',1.2,true,{vel:{x:0,y:0,z:3.5}}]`. NativeSourcePresentation.cs:36 is `...magnitude>2.5f?"run":"walk"`. Sample(...,age=arena.Time,...) means a minion at 2.4 m/s plants feet moving at 1.6 m/s (0.8 m/s skate) and at 2.6 m/s uses the 3.5 m/s run, a 0.9 m/s skate in the other direction, with a pop at the threshold.

**Verifier's correction.** Confirmed as reported, with one addition the reviewer did not raise. The looping horde walk/run clips (and probably crawl) are not whole gait cycles: 3.6 and 3.528 cycles respectively. So on top of the speed-mismatch skating, legs snap 19 to 72 degrees (feet jump 0.1 to 0.5 m) every 1.5 s or 1.2 s at the Mathf.Repeat wrap in NativeSourceMotion.cs:18, for every moving minion. A fix that only scales playback rate would not remove that snap. The loop needs a whole number of cycles, or the gait needs to be evaluated procedurally per minion as in the source.

**Suggested fix.** Port the source minion gait (speed-driven phase per minion, ground-anchored feet, cross-fade) or at least scale clip playback by actual speed with a per-minion phase.

<a id="f34"></a>
### #34 🟡 medium — Cosmetic corpse physics scene receives only the first 48 foundation blocks, so bodies fall through upper floors and stairs
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeDeathPresentation.cs` (46)

**What happens.** A player is killed on an elevated foundation platform or stairway, for example at (-25.5, 7.2, 12) on several maps. `arena.Props.Where(p=>p!=null&&(p.Indestructible||distance<8)).Take(48)` takes the first 48 matching props in list order. Every map has 77-128 indestructible 'foundation' blocks, so the platform under the body (and nearby cover listed later) is not copied into the corpse's physics scene, and the ragdoll drops through it.

**Evidence.** GameContent.json: foundry 101 foundation blocks, neon 109, canal 128, rooftops 94, and so on. Each map has 12-30 walkable-looking foundation slabs (top 1.3-14.4 m, at least 1.5 m wide) after the first 48, e.g. foundry (-22.7,1.79,12), (-25.5,3.6,12); neon (-22.7,8.99,-12). AddProp appends in map order, and Take(48) is applied after the OR filter, so nearer props are also cut off.

**Verifier's correction.** Mostly accurate. Two additions: (1) Nearby destructible cover is excluded as well, not just foundation slabs. Up to about 70% of cover blocks are missing even for a death right beside them. (2) The original also cuts some top-tier tower floors through its own slice(0,32), for example the canal 14.4 m floor. The clear divergence is the mid-level floors (3.6 m, 7.2 m, and on several towers 10.8 m): the original keeps them, Unity drops them. The neon example (-22.7, 8.99, -12) is on the first tower's stair-29, which is cut in Unity. There the body falls onto the included stair-5 at 1.79 m, not all the way to the ground.

**Suggested fix.** Select obstacles by distance (nearest N, including indestructible ones) rather than taking the first N of 'indestructible OR near'.

<a id="f35"></a>
### #35 🟡 medium — Leet (a magic-class weapon) is animated with the sword capture set and has no casting animation
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (89, 261, 134)
- **Original:** src/shared/content.ts:22 (leet class 'magic'); src/client/character-animation.ts:69-70 (magicLayer for magic class)

**What happens.** A DEV Leet player idles, walks, casts or blocks. MagicVisual is 0 for spec 'leet', so MagicPose never runs. The rig plays the captured sword idle, walk and run with the staff in the free left hand, and the sword block/stagger. A Leet primary sets AttackAt but not AttackEnd (line 134), so neither a cast phrase nor a cut plays. The sword floor guard (CapturedAnimation.cs:232) also bends the right wrist.

**Evidence.** NativeArena.cs:89 `p.Pose.MagicVisual=seat.spec=="fire"?1:...:"darkness"?4:0;` excludes leet. CapturedAnimation.cs:217 applies MagicPose only when MagicVisual>0. CONVERSION-STATUS claims 'Emperor, Ansatsuken, Leet and Siren, their abilities, styles'.

**Verifier's correction.** The core claim is correct. Additional details:
- The Leet E ability (NativeLeet.cs:44) also sets only AttackAt and has no motion.
- Sword stagger plays for Leet during hitstun, while the original uses the generic hit reaction for non-melee classes.
- The fallback primitive renderer (ArenaPresentation.cs:202-209) draws a sword mesh for Leet.

A complete fix also needs NativeAbilityRules.PrimaryWindup, MagicPose's element-keyed tables and NativeStaffGeometry.Parts to support a Leet element. The original's default fire-like coil/ready keys would apply, and the twist multiplier would be 1.

**Suggested fix.** Give leet a magic visual (with its own cast windup .18/rate) so MagicPose and the staff guard run.

<a id="f36"></a>
### #36 🟡 medium — Draft offers come from a fixed-seed RNG used only for drafts, so every match (and every rematch) deals the same Chips
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Core/NativeMatch.cs` (39, 68 (caller ConversionGame.cs:52; also NativeArena.cs:54, ConversionGame.cs:60 -> NativePve.cs:21))
- **Original:** src/shared/simulation.ts:102 (random() shared with all combat RNG), 511 (offerChips(...,()=>this.random(),...)); src/shared/host-room.ts:16 and src/server/index.ts:46 (PvE seed from crypto/randomBytes)
- **Raised as:** high

**What happens.** Start a match, lose the first set as pistol, look at the draft. Quit or Rematch and lose the first set again: the six offers are identical every time, and later drafts repeat too if you pick the same Chips. Ward Zero runs always use seed 12345, so horde spawns and pick offers repeat for the same actions. Arena crit, chaos and coin-toss rolls also start from the same System.Random(12345) sequence each match.

**Evidence.** `public NativeMatch(ConversionContent data,uint seed=12345){...random=new SourceRandom(seed);}`, and StartMatch calls `new NativeMatch(content){...}` with no seed. This `random` is used only by `NativeContentRules.Offers(data,p.spec,p.chips,random.Next,...)`. Probe (Core compiled with dotnet, three independent matches, seat 2 pistol loses set 1): `match 0/1/2 first draft offers seat2: vital,long-roll,executioner,skirmisher,hardened,efficient-roll`, the same all three times. In the source, offers draw from sim.random(), which also drives every shot's spread, crit and chaos roll, and the stream continues across rematches, so offers vary from match to match. PvE in the source is seeded from crypto random values.

**Verifier's correction.** Confirmed for draft offers (NativeMatch.cs:37/39/68, created at ConversionGame.cs:52 and again on each Rematch through ConversionGame.cs:195) and for PvE (NativePve.cs:21-23 default seed 12345, ConversionGame.cs:60; the opening picks and horde spawns repeat every run, while the original uses a crypto seed per room plus runIndex per run). Not a divergence: the arena combat RNG (NativeArena.cs:54). The original Simulation also always starts from the fixed seed 23819, and in both versions gameplay (shot spread and so on) changes how much of the stream is used, so crit, chaos and coin-toss rolls do not repeat in practice. Severity is medium rather than high: the draft and PvE picks still work but are predictable and repeat between matches.

**Suggested fix.** Seed NativeMatch (and NativeArena.random and ConfigurePve) from a per-match cryptographic or time-based seed on the authority, and keep the seed stable within the match for replay.

<a id="f37"></a>
### #37 🟡 medium — A disconnected player's pilot respawns every round as an invisible, collidable, damageable capsule on the host
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (77 (Reset -> SpawnPilot for every seat), 86; NativeProjectiles.cs:71-75; NativeChips.cs:35, 99, 105)
- **Original:** src/shared/simulation.ts:201 (resetRound: `if(!p.connected)p.hp=0`), 515-517 (disconnect death after 12 s)
- **Raised as:** high

**What happens.** In a 3+ pilot online room, one guest drops (connection closed or 12 s silence). From the next round to the end of the match, the host spawns that seat's pilot at full HP with an enabled CapsuleCollider at its spawn point. It is not drawn (Snapshot and HUD filter connected seats) but it stops bullets, absorbs melee, attracts homing shots, can be the target of Thunder Step or Now You See Me blinks, and takes chip blasts and emits hit/death events at an empty spot.

**Evidence.** Reset: `for(int i=0;i<Match.Seats.Count;i++)SpawnPilot(Match.Seats[i],i);`. SpawnPilot does not check `seat.connected` and sets `Hp=(float)stats["hp"]`. The bullet sweep accepts any pilot with `p.Hp>0&&Enemy(owner,p)&&!Invulnerable(p)`, homing loops `foreach(var target in Pilots)` with no connected check, and ChipBlast and thunderstep/blink-cut targeting check only `Hp>0&&Enemy`. Only the replica disables the collider (`p.Collider.enabled=pf.hp>0&&p.Seat.connected`, NativeNetworkFrame.cs:83). The authority never does, while NativePve.cs:28 shows the author knew that `connected=false` alone leaves the collider active (`b.Seat.connected=!hide;b.Collider.enabled=!hide;`). Seats are never removed from match.Seats during a match.

**Verifier's correction.** The "stops bullets" and physical-obstacle parts match the original: in TS a disconnected seat's hp=0 Rapier capsule stays at its spawn point and shot castShape (simulation.ts:397) still hits it. The real divergence is that Unity spawns that pilot with full HP, so it can be damaged and targeted. Damage, lifesteal and drain heals, chip procs, homing (first-in-seat-order), Pong rally, thunderstep, blink-cut, ChipBlast, Vacuum, trampoline and freezer-block all act on it, and its death produces a ragdoll and a kill broadcast. "Absorbs melee" is inaccurate: melee damages every contacted target, so the hidden pilot does not shield anyone. Two more issues in the same area: Unity never applies the TS 12 s disconnect death (a mid-fight drop leaves an invisible, damageable pilot for the rest of the round), and a guest who rejoins mid-round gets their full-HP pilot back and fights while seat.alive=false, so Resolve ignores them; in TS they stay dead until the next round. Fix: in SpawnPilot/Reset set Hp=0, disable the collider and make the body kinematic for disconnected seats, mirroring simulation.ts:201. Optionally add the 12 s disconnect death.

**Suggested fix.** In Reset/SpawnPilot, set Hp=0, disable the collider and make the body kinematic for seats that are not connected (or skip spawning them), and re-enable on reconnect. Add `Seat.connected` checks to homing and chip target selection.

<a id="f38"></a>
### #38 🟡 medium — Disconnects forfeit the round instantly, and a player who reconnects during the countdown is counted as eliminated while still playable
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Core/NativeMatch.cs` (53, 81-82)
- **Original:** src/shared/simulation.ts:197-199 (disconnect keeps body/hp), 494 (alive = hp>0), 515-517 (12 s grace, then death; match ends only if fewer than 2 connected)

**What happens.** (1) In a 1v1 or team round, a guest's socket closes: on the next tick `Resolve` drops them from the alive set and the other side wins the round, with no 12 s grace. While they stay disconnected, every following round auto-resolves after the 3 s countdown, so a roughly 50 s outage can lose a whole match. (2) A guest who reconnects during the countdown has `alive=false` (set at BeginRound while disconnected) but full HP, moves and fights normally, yet `Resolve` ignores them, so their side loses the round the moment the fight starts.

**Evidence.** `BeginRound(){...foreach(var p in Seats)p.alive=p.connected;...}`, and Resolve uses `Seats.Where(p=>p.alive&&p.connected)`. Nothing sets `alive=true` on resume (NativeNetwork.cs:75 sets only `peer.Seat.connected=true`). NativeArena.Step processes any pilot with `p.Hp>0&&p.Seat.connected` regardless of `alive`. In the source, a disconnected player stays in the round (hp>0) and is only killed after `C.disconnectSeconds` (12).

**Verifier's correction.** Scenario (2) is exact, and its window is larger than stated: in team or 3+ FFA rounds, reconnecting at any point during the fight after being disconnected at BeginRound also gives a ghost player (loss with HP remaining, or a false 'Simultaneous knockout' when they kill the last enemy).

Scenario (1) needs rewording. The divergence is the instant round forfeit (versus the source's 12 s idle body with HP) plus the missing 'fewer than 2 connected sides -> match over' rule. It is not that the source gives a 12 s grace to return: the source has no PvP reconnect and ends a 1v1 match after a 12 s outage. Unity is actually more lenient at match level (about 40 s at the default 3 sets).

There is an additional consequence of the missing end-of-match rule: when all seats are disconnected on a dedicated authority, draws repeat forever.

On the fix: derive 'alive' from pilot HP, or set alive=true on resume. Then decide deliberately how the 12 s forfeit timer interacts with the 90 s reconnect seat, and document the choice in CONVERSION-STATUS.

**Suggested fix.** Mirror the source: keep disconnected seats in the round until a 12 s timeout (then set Hp=0), and derive 'alive' from pilot HP rather than a flag latched at BeginRound.

<a id="f39"></a>
### #39 🟡 medium — Teams mode cannot put two human players on the same side: guest team = seat id - 1 and there is no team picker
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (79, 87, 100; ConversionGame.cs:53-56, 222)
- **Original:** src/shared/simulation.ts:111 (red/blue auto-balance on join), 149 (setTeam); src/client/main.ts lobby team selection

**What happens.** Host enables 'Teams — alternating red / blue sides' with two friends joining. Host is team 0, guest id 2 gets team 1 and guest id 3 gets team 2, so the second guest is a team of one. Two humans who want to team up against bots are always split: host on 0, first guest on 1. On the dedicated server the ids start at 1, so every human gets a distinct team (FFA). CONVERSION-STATUS claims 'Match flow | Rounds, sets, teams, ...'.

**Evidence.** `var seat=new NativeSeat{id=id,...,team=id-1,...}` and `local.team=0`. A host handler exists, `if(message.type=="team"&&arena==null){seat.team=Math.Max(0,Math.Min(7,message.team));...}`, but nothing in the client ever sends type "team" (grep finds only the handler and the lobby label `"team "+(s.team+1)`). Bots use `team=prefs.teams?(i+1)%2:i+1`, so only bots alternate. The dedicated server has no teams option at all (NativeDedicated.cs:13 builds prefs without teams).

**Verifier's correction.** Accurate as reported. One addition: even if a team message were added, NativeNetwork.cs:86 would reset the team to id-1 every time a guest presses "Send my loadout", so the fix must keep the team when the seat is re-sanitized. Bots in the Unity local/host flow also only alternate 0/1, while the original lets each bot be assigned to one of 8 teams (team-ui.ts:6 practiceTeamSetup). That is a related gap but less important.

**Suggested fix.** Assign guests to team 0/1 by balance (as the source does) when Teams is on, add a team toggle that sends the existing "team" message, and clamp teams to 0/1 in Teams mode.

<a id="f40"></a>
### #40 🟡 medium — Roll / Demon Step i-frames only avoid 'direct' damage, so chip bombs, mines, fields, shrapnel, static, summons and hazards hit rolling pilots
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (176, 185; NativeChips.cs:35, 120; NativeSummons.cs:71)
- **Original:** src/shared/simulation.ts:225 (`if((rollInvulnerable\|\|demonStepInvulnerable)&&rollAvoids(kind))return 0`), 231 (roll cancelled by any avoidable damage); src/shared/souls-combat.ts:51 (rollAvoids excludes only fall/overtime/burn/poison/leet-scare)

**What happens.** An enemy's Pop Quiz bubble, Wait For It bomb, proximity mine, Counter Pulse, Blast Dash, Static Discharge, Shrapnel, acid pool, zombie or toy-soldier strike, or a saw or lava hazard hits a pilot during their roll i-frames. In the source the roll avoids all of these, and damage taken during the roll ends it. In Unity the pilot takes full damage, and the roll is not cancelled by these hits. Ghost Timing and Tumble Engine lose most of their value against chip builds.

**Evidence.** The damage gate is `...||direct&&Invulnerable(target))return 0;` and the roll cancel is `if(direct&&actual>0&&Time<target.RollEnd)`. Every chip explosion and field tick calls `Damage(...,direct:false,primary:false)` (ChipBlast line 35, StepChipFields line 120), summon strikes pass `direct:false`, and hazards pass `direct:false,stage:true`. In the source these kinds ('chaosBomb', 'blast', 'shrapnel', 'static', 'field', 'dashBlast', 'blockPulse', 'minion', 'stage-*', 'thorns') all satisfy rollAvoids.

**Verifier's correction.** The finding is accurate. One addition: the `fire` weapon's built-in splash also goes through ChipBlast with direct:false (NativeProjectiles.cs:96 and :134; GameContent.json `fire` blast 1.5), and so does prop debris (NativeDestruction.cs:26). Both hit rolling pilots in Unity but are avoided in the source (kinds 'blast' and 'debris'). So the gap is not limited to chip builds. The roll-cancel half of the finding applies to hits outside the i-frame window but still within the roll (source: any avoidable damage clears p.roll). In Unity only direct hits do this.

**Suggested fix.** Pass an explicit damage kind into Damage and apply the source's rollAvoids list (Invulnerable for all kinds except fall/overtime/burn/poison/leet-scare), separately from the 'direct' flag that controls block/primary procs.

<a id="f41"></a>
### #41 🟡 medium — Round overtime (sudden death after 100 s) is missing, so rounds can last indefinitely
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Core/NativeMatch.cs` (56-61, 79-92)
- **Original:** src/shared/config.ts:3 (maxRoundSeconds: 100); src/shared/simulation.ts:522 (after 100 s every player takes 18 DPS 'overtime', not avoidable or savable)

**What happens.** Two campers, a Stationary-targets practice match, or a pilot hiding behind cover at range: in the source, after 100 s of fight everyone loses 18 HP/s until the round resolves. In Unity the Fight phase never times out, so a round never ends unless someone dies.

**Evidence.** grep for overtime/maxRound in Assets/ChipArena finds nothing. `FightAt` is used only for the hazard 2 s grace (NativeArena.cs:227). NativeMatch.Tick in Fight only calls `Resolve()`, which waits for one side to remain. Not listed as a deliberate adapter in CONVERSION-STATUS.md.

**Verifier's correction.** The scenario's 'Stationary-targets practice match' is not a real normal mode in the source; 'stationary' is only a test-driver dev toggle in src/client/main.ts. The correct scenario is a normal PvP round, not sandbox and not PvE. Two players who never land lethal damage, or a leader who stays out of reach, would see a source round end at about 100-106 s: the higher-HP player wins, or a tied round becomes a simultaneous-knockout replay. In Unity, that round stays in NativePhase.Fight forever. Fix location: NativeArena.Step, before Match.Resolve(), when Match.Phase==Fight && Match.Time-Match.FightAt>100 && !sandbox && Pve==null. The fix should apply 18*dt damage that ignores roll/down protection, the chaos shield and second-chance, while still respecting the Leet save and the arcade invulnerable checks, as the source does for the 'overtime' kind.

**Suggested fix.** In NativeArena.Step, when Fight time exceeds 100 s (not sandbox, not PvE), apply forced-but-avoid-saves 18*dt damage to every living pilot, mirroring the source 'overtime' kind (no second-chance or shield save).

<a id="f42"></a>
### #42 🟡 medium — Every bot gets an entrance intro (about 4.8 s each) at match start, there is no skip option, and the DEV Siren is not ordered last
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Core/NativeMatch.cs` (49, 51; ConversionGame.cs:52)
- **Original:** src/shared/simulation.ts:186-189 (beginIntros: `filter(p=>p.connected&&(!p.bot\|\|this.siren.active(p))).sort(siren last)`, skipIntros/sandbox -> countdown); src/client/main.ts:121 ('Skip player intros' option)

**What happens.** Default solo match, 1 human + 3 bots: the source plays one intro (4.8 s) and then the 3 s countdown. Unity plays four intros, and with 7 bots it plays eight (38 s) before every match and rematch. Sandbox matches also play intros.

**Evidence.** `private void NextIntro(){var order=Seats.Where(p=>p.connected&&!p.AuthorityBoss).ToArray();...}` includes bots and has no sort. StartMatch hard-codes `ShowIntros=true`, and ConversionPreferences has no skip-intros field. Probe (1 human + 3 bots): `intro seats: 1,100,101,102  seconds until fight: 22.25` (source would be 7.8 s).

**Verifier's correction.** The details are accurate. The 7-bot case is 8 intros x 4.8 s = 38.4 s of intros plus the 3 s countdown, so the fight starts at 41.5 s; the source's fight starts at 7.8 s. The skip-intros preference in the source is a client preference that every room type honours, not only solo matches. The Siren ordering divergence only applies when a human DEV Siren is in a PvP room. In PvE, Unity leaves the AuthorityBoss out of the start-of-match intros entirely; the boss gets its cinematic later.

**Suggested fix.** Exclude ordinary bots (keep an authorized Siren and order it last), and add a 'Skip player intros' preference wired to ShowIntros (also off in sandbox).

<a id="f43"></a>
### #43 🟡 medium — Draft and inspect cards show only the raw description: no stat lines (gains/penalties) and no owned/max count
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeDraftPresentation.cs` (37 (also NativeInspect.cs:14, 19; ConversionContent.cs:14))
- **Original:** src/client/chip-card.ts:15-25 (chipStatsHtml, rank have/max); src/shared/chip-stats.ts (chipStatLines); src/shared/chip-description.ts:5-15

**What happens.** A drafting player sees 'Power Cell: A small, steady boost to every hit.' with no '+8% Damage'. 'Stout Plating' shows no '+35 Health / -3% Move speed', and 'Rune Clock' no '-10% Ability cooldown'. 65 of the 236 Chips have no number in their description; the 28 damage/health and 16 magic Chips carry their numbers only in stat lines. Trade-off penalties (shown red in the source) and how many copies you hold of a stackable Chip are not shown either.

**Evidence.** The card renders `GUI.Label(...,chip.name,label);GUI.Label(...,chip.description,small);`. ContentChip has no `stats` or `prose` field (`{ public string id,name,description,rarity,icon,mechanic;public string[] tags,excludes;public int max;public ChipEffect[] effects; }`), so the exported `stats` arrays in GameContent.json are dropped by JsonUtility. tsx run of the source: `power-cell ... lines: [{"text":"+8% Damage","tone":"good"}]`, `stout ... ["+35 Health" good, "-3% Move speed" bad]`, and `chips whose description has no digit: 65`.

**Verifier's correction.** Files and lines:
- Core/ConversionContent.cs:16 (missing fields)
- Runtime/NativeDraftPresentation.cs:37-38, 40
- Runtime/NativeInspect.cs:14, 19
- Also Runtime/NativePveUI.cs:26 and ConversionGame.cs:236
- The finding cited "Runtime/ConversionContent.cs:14"; the actual file is Core/ConversionContent.cs:15-16.

Inspect does show the owned count (xN). What it lacks is /max, the stat lines and the combined-effect rows. The draft card shows neither the owned count nor max.

Counts: 29 power Chips and 17 prose Chips, not 28 and 16. 36 of the 65 Chips without a digit in their description have numbers only in stat lines. 65 Chips have red penalty lines.

Unity also shows the raw `description` instead of `chipDescription()` for the 138 Chips where those differ.

**Suggested fix.** Deserialize `stats`, port chipStatLines (derived lines from effects) and chipDescription, and render good/bad lines plus `have/max` on draft, reveal and inspect cards.

<a id="f44"></a>
### #44 🟡 medium — Ansatsuken's 'no-cooldown parry' gets the normal 4 s Block cooldown in Unity
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeMelee.cs` (17)
- **Original:** src/shared/simulation.ts:312 (`p.blockCd=this.arcade.active(p)?0:blockCooldown(p.stats)`); 539 snapshot blockCooldown 0 for arcade; arcade-content.ts ARCADE_INCOMPATIBLE includes 'block-recharge'

**What happens.** An authorized Ansatsuken presses RMB and the parry whiffs. In the source they can parry again immediately ('RMB: no-cooldown parry', which is why Rebound Capacitor is excluded for this spec). In Unity, BlockReady is set to Time+4 s (minimum 1.5 s), so only a successful parry (BlockProc resets BlockReady to 0) lets them parry again.

**Evidence.** TryBlock: `p.BlockAt=Time;p.BlockEnd=Time+NativeMeleeRules.BlockDuration(p.Stats);p.BlockReady=Time+NativeMeleeRules.BlockCooldown(p.Stats);` has no `Dev(p,"ansatsuken")` case. Only BlockProc (NativeChips.cs:22) zeroes it after a successful parry.

**Verifier's correction.** The fix goes in TryBlock at NativeMelee.cs:17. Unity's Dev(p,"ansatsuken") (NativeFighters.cs:23) matches TS arcade.active, so for that case set BlockReady to Time (or leave it unchanged) instead of Time+BlockCooldown. The existing `Time<p.BlockEnd` check still enforces the 0.3 s window, and the edge-triggered Input.Block prevents auto-repeat, as in the source. The HUD Bar at ArenaGame.cs:335 divides by the constant Rules.BlockCooldown. It will show READY once BlockReady<=t, so it needs no change.

**Suggested fix.** In TryBlock, set BlockReady=Time (no cooldown) when Dev(p,"ansatsuken").

<a id="f45"></a>
### #45 🟡 medium — Build inspection throws ArgumentException for any pilot holding a Ward Zero money Chip
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeInspect.cs` (14)

**What happens.** In PvE, a human buys a pick that includes a money Chip (30% chance; e.g. 'bounty-scanner') and installs it. Pressing G/Tab and clicking that pilot in the board calls `content.Chip("bounty-scanner")`, which throws 'Unknown chip' in OnGUI every event. The inspect panel never draws, GUILayout groups are left unbalanced and the rest of the HUD after it (LockHud) is skipped until Esc.

**Evidence.** ChoosePve stores money Chips in the seat build: `p.Seat.chips[chip]=count+1;` (NativePve.cs:56). Money Chips live only in `Pve.Content.moneyChips`, not `content.chips` (PveChip looks in both, NativePve.cs:50). Inspect uses `seat.chips.OrderBy(c=>content.Chip(c.Key).name)`, and `ConversionContent.Chip` is `Array.Find(chips,...)??throw new ArgumentException("Unknown chip: "+id)`.

**Verifier's correction.** Unity file: Assets/ChipArena/Runtime/NativeInspect.cs:14, with the throw at Assets/ChipArena/Core/ConversionContent.cs:43. The Close button is drawn before the throw (NativeInspect.cs:12), so the user can close the panel with it as well as with Esc. While the panel is open, the uncaught OnGUI exception skips LockHud and also the PvE HUD and Replay HUD (ConversionGame.cs:197-199). Parity reference: src/client/player-board.ts:36 and src/client/pve/board.ts:71 use CARD_CHIPS (src/shared/pve/money-chips.ts:34), which includes the money Chips.

**Suggested fix.** Resolve definitions through arena.PveChip (or a combined lookup) in Inspect, and skip unknown ids instead of throwing.

<a id="f46"></a>
### #46 🟡 medium — Bot AI has no difficulty levels and aims perfectly (rifle bots never miss stationary targets)
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (249-255)
- **Original:** src/shared/simulation.ts:433-444 (botInput: dummy/easy/normal/hard, think intervals [.32,.18,.1], aim error [1.8,.8,.4] m, cover retreat, threat blocks, rolls); simulation.ts:115-135 (botDifficulty option)
- **Raised as:** high

**What happens.** In a solo match against rifle bots, every Unity bot sets AimTarget to the exact capsule centre, keeps ADS on (rifle ADS spread multiplier 0) and fires whenever it has line of sight, out to 140 m. The result is a zero-spread, ballistic-corrected 58-damage shot every 1.15 s. There is no easy, normal, hard or dummy setting anywhere in the Runtime. TS bots re-aim at a randomly offset point each think interval, scaled by difficulty, retreat to cover to reload, roll and block threats.

**Evidence.** `return new InputFrame{...,AimTarget=Vec(target.Body.position),AbilityTarget=Vec(target.Body.position),...,Shoot=clear&&(melee?distance<desired+.3f:true),Aim=!melee,...}`. Fire uses AimTarget plus BallisticPitch when Aiming, and spread `*=1+(ads.spread-1)*settle` (rifle ads.spread=0). `grep -i difficulty` over Runtime/Core finds nothing.

**Verifier's correction.** Severity lowered from high to medium, and the headline needs correcting. Unity rifle bots hitting stationary targets nearly every shot matches TS normal (the default) and hard, because TS bots also use ADS with spread 0, authority zeroing and sub-capsule aim error. The real defects:
- No difficulty selection. The TS lobby offers dummy, easy, normal and hard (main.ts:136, simulation.ts:131). Unity only has the "Stationary targets" toggle, roughly equivalent to dummy, in ConversionGame.cs:222.
- NativeArena.cs:249-255 replaces botInput and botAbilities with a simplified AI:
  - no think-interval aim error or lag;
  - no strafing;
  - one 12 m standoff for every gun (TS rifle 32 m, pistol and machine gun 10 m, magic 8 m or 3.5 m);
  - ADS always on instead of only within the kit's ranges (pistol bots always fire at ×1.12 interval and move slower);
  - no cover retreat on reload or low mana, no melee dash, no summon focus;
  - random single-frame blocks at under 3 m instead of threat-based blocks with a chance set by difficulty.

**Suggested fix.** Port botInput: difficulty, think timer, aim error, botAds ranges, cover and threat logic, and dash. Expose botDifficulty in prefs and lobby.

<a id="f47"></a>
### #47 🟡 medium — Black Hole (singularity) barely pulls: AddForce velocity is overwritten by StepPilot every tick
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeAbilities.cs` (241 (with NativeArena.cs:144-165))
- **Original:** src/shared/chaos.ts:120 (q.ext += -norm(d)*force*dt*knockbackTaken, persistent and decaying with drag 3.1)

**What happens.** A standing pilot inside a 6 m Black Hole for its 2.2 s life: TS pulls about 9.5 m (ext reaches about 5 m/s), dragging them to the centre. Unity pulls about 0.6 m because each tick's AddForce(VelocityChange) lasts one physics step. The next StepPilot then rewrites linearVelocity.x/z from MoveVelocity plus Knockback. Walking out is trivial.

**Evidence.** Unity: `p.Body.AddForce(delta.normalized*(float)d.force*dt,ForceMode.VelocityChange);` while StepPilot does `velocity.x=p.MoveVelocity.x+step.x;...p.Body.linearVelocity=velocity+external;`. The chip black-hole field correctly uses `p.Knockback+=d.normalized*(-35)*dt*...` (NativeChips.cs:120). Probe review-u/core-sim/blackhole.py: `TS pull ~9.49 m ... Unity pull ~0.59 m`.

**Verifier's correction.** The finding's source_ref is right that chaos.ts:120 applies the pull. The direct path from the singularity ability to that code is magic-abilities.ts:71 (singularity release -> y.zone 'black-hole', force:c.def.force) -> ability-system.ts:202 (zone -> chaos.field), with force 16 / radius 6 / duration 2.2 at magic-kit.ts:22. The ext decay is at movement.ts:34 and its use as velocity at movement.ts:48. In Unity, the overwrite happens at NativeArena.cs:155-163.

**Suggested fix.** Use `p.Knockback += delta.normalized*force*dt*knockbackTaken*(slowFactor<.6?.5:1)`, as chaos.ts does.

<a id="f48"></a>
### #48 🟡 medium — Headshots are any contact above capsule centre +0.4 m (shoulders count) instead of the source head box
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeProjectiles.cs` (122; NativeMeleeSpecials.cs:153; NativeAbilities.cs:287)
- **Original:** src/shared/headshots.ts:5-12 (projectileHeadshot: 0.34 m yaw-aligned head box centred +0.64, false while knocked down or juggled)

**What happens.** A horizontal rifle shot passing at x=0.30, y=+0.45 (shoulder) or x=0.35, y=+0.5. TS: body hit (58). Unity: headshot ×1.5 (87), which makes two-shot kills more frequent. Unity also grants headshots on airborne knocked-down or juggled targets.

**Evidence.** Unity: `bool head=hit.Point.y>target.Body.position.y+.4f;`. TS probe review-u/core-sim/headshot-probe.ts: `shot x=0.3 y=+0.45: TS headshot=false Unity=HEAD`, `x=0.35 y=+0.5: TS false Unity HEAD`, `x=0.3 y=+0.6: TS false Unity HEAD`. The source comment reads 'The capsule's curved shoulder is not sufficient.' Not listed as an adapter in CONVERSION-STATUS.md. An older VALIDATION checkpoint lists 'Full source body/head volumes' as pending, but the current section supersedes that note.

**Verifier's correction.** The core claim is correct at the cited lines (NativeProjectiles.cs:122, NativeMeleeSpecials.cs:153, NativeAbilities.cs:287), against src/shared/headshots.ts:5-12. Any contact above +0.40 counts as a headshot, which doubles the frontal headshot area (12.5% → 25%). That includes shoulders, the sides of the upper capsule and centred hits at 0.40–0.425. Juggled and knocked-down targets that are still airborne are not excluded.

The "two-shot kills more frequent" claim needs a qualifier. Base HP is 100, so two stock-rifle body shots (116) already kill. The extra 1.5× damage changes kill breakpoints only for targets with more HP or for other weapons. The wrong headshot flag also changes the wound part, the kneel-fall death choice and the HEADSHOT marker in the HUD.

**Suggested fix.** Port projectileHeadshot (trajectory closest approach to +0.64 box in target yaw frame, padding min(.06,r), knockdown/juggle exclusion) into Core and use it for bullets, thrown daggers and flame lance.

<a id="f49"></a>
### #49 🟡 medium — Roll i-frames only stop 'direct' damage: explosions, hazards, debris, chip fields and thorns hit rolling pilots
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (176, 185; NativeChips.cs:35; NativeArena.cs:233; NativeDestruction.cs:26)
- **Original:** src/shared/simulation.ts:225 (`(rollInvulnerable\|\|demonStep)&&rollAvoids(kind)` returns 0 for every kind except fall/overtime/burn/poison/leet-scare); souls-combat.ts:51

**What happens.** A pilot rolls through a fire bolt's 1.5 m splash, a static/shrapnel/dash/block-pulse blast, a saw/spike/lava contact, a thrown crate or a chip field during their i-frame window. TS takes 0. Unity applies full damage, because these paths call Damage(...,direct:false) and the invulnerability test is `direct&&Invulnerable(target)`. Non-direct hits in the vulnerable part of a roll also no longer cancel the roll (line 185 requires direct).

**Evidence.** Unity: `...||direct&&Invulnerable(target)))return 0;`, ChipBlast `Damage(target,owner,damage*fall,...,direct:false,primary:false)`, hazards `Damage(p,null,...,direct:false,stage:true)`, debris `Damage(p,...,direct:false,primary:false)`. TS probe: `TS fire-bolt splash on a rolling (i-frame) pilot: hp 100.0`.

**Suggested fix.** Separate 'roll-avoidable' from 'direct': apply Invulnerable to every kind except DoT ticks, fall/forced and leet-scare, and cancel the roll on any avoidable damage.

<a id="f50"></a>
### #50 🟡 medium — Round overtime (100 s limit, 18 HP/s to everyone) is not implemented
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (94-111 (Step); Core/NativeMatch.cs:79-92)
- **Original:** src/shared/simulation.ts:522 (`if(this.time-this.fightStart>C.maxRoundSeconds&&!sandbox&&!pve)damage(p,dt*18,null,'overtime')`); config.ts maxRoundSeconds:100

**What happens.** Two players (or a player and a passive bot) avoid each other or hide behind cover. TS forces a resolution after 100 s by draining everyone 18 HP/s. Unity rounds never end. The exported config even contains maxRoundSeconds=100, but no code reads it.

**Evidence.** No reference to overtime, maxRoundSeconds or a fight-time limit in Runtime/Core (`grep -i overtime|maxRound` finds nothing; Match.FightAt is only used for the 2 s hazard grace and the network frame).

**Verifier's correction.** The cited Unity locations are right: Runtime/NativeArena.cs Step (lines 94-111; the fall check and Match.Resolve are at 110-111) and Core/NativeMatch.cs Tick/Resolve (lines 56-92). The original is src/shared/simulation.ts:522 plus config.ts:3. Overtime damage uses kind 'overtime', so a fix must bypass the roll avoidance (souls-combat.ts:51 rollAvoids), the second-chance save (chaos.ts:104) and the chaos shield (simulation.ts:231). It must also skip sandbox and PvE (Pve != null), and use Match.Time - Match.FightAt > 100.

**Suggested fix.** In Step during Fight (not sandbox/PvE), after 100 s of fight time apply Damage(p,null,18*dt,...,forced-like but roll-proof) each tick.

<a id="f51"></a>
### #51 🟡 medium — Melee primary requires a fresh click per swing; holding LMB no longer chains attacks
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (82, 122; NativeNetworkFrame.cs:53-55)
- **Original:** src/client/main.ts:345 (`attack:left`, the held mouse state); simulation.ts:458 (primary(p) is attempted every tick while input.attack is held)

**What happens.** A sword player holds LMB. TS swings again the tick the commitment ends (continuous cross cut, backhand, overhead). Unity sends Shoot only on GetMouseButtonDown and clears it after one FixedUpdate. Clicks during the swing are discarded, so the player must re-click exactly after AttackEnd. Remote melee clients get the same treatment (`held.Shoot=false`).

**Evidence.** `if(p.Weapon.@class=="melee")pending.Shoot|=Input.GetMouseButtonDown(0);else pending.Shoot=Input.GetMouseButton(0);` and `pending=pending.HeldOnly();if(melee)pending.Shoot=false;`. TryMelee returns false while `Time<p.AttackEnd||Time<p.Ready`.

**Verifier's correction.** This applies to all seven melee-class kits: sword, daggers, shield, greatsword, siren, emperor and ansatsuken. The emperor/ansatsuken case is the one dev-click-combos.test.ts pins in the original. The sword is not the only case. A press released during a swing is also lost in TS, so the actual divergence is held-LMB auto-repeat, not discarded mid-swing clicks. Minor side effect: in Unity, holding LMB with a melee weapon no longer suppresses sprint, while TS sprintIntent does suppress it (Unity NativeArena.cs:138 `!i.Shoot` vs TS movement.ts:13 `input.attack`).

**Suggested fix.** Send the held LMB state for melee too, or buffer a press until the swing can start.

<a id="f52"></a>
### #52 🟡 medium — Collision-induced velocity on pilots is discarded every tick (no finishMotion), so debris and body pushes vanish
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (144-165; NativeDestruction.cs:19-30)
- **Original:** src/shared/movement.ts:51-53 (finishMotion adds the post-physics velocity delta × knockbackTaken to ext); simulation.ts:466-475

**What happens.** A 44 kg metal crate or a collapsing beam hits a pilot, or pilots shove each other. TS carries the impact as decaying external velocity (shove or ring-out, damped by Stabilizer). Unity applies it for one PhysX step: the next StepPilot overwrites linearVelocity.x/z with MoveVelocity+step+Knockback. Debris damage applies, but the push and the Stabilizer behaviour are lost.

**Evidence.** StepPilot: `Vector3 velocity=p.Body.linearVelocity; ... velocity.x=p.MoveVelocity.x+step.x;velocity.z=p.MoveVelocity.z+step.z; ... p.Body.linearVelocity=velocity+external;`. AfterPhysics only computes debris damage and never folds velocity deltas into p.Knockback. CONVERSION-STATUS mentions only unspecified 'native adapters' for collision.

**Verifier's correction.** Location is correct: NativeArena.cs:151-165 (StepPilot overwrites x/z) and NativeDestruction.cs:13-30 (BeforePhysics/AfterPhysics never write p.Knockback); TS reference is simulation.ts:479-480 plus movement.ts:51-53.

Correction to the scenario: body pushes do not vanish entirely. The PhysX solver still pushes a pilot during each step of contact. What is lost is the carried momentum after contact, which in the crate probe is about 2.1 m vs 0.2 m of shove. Stabilizer/knockbackTaken scaling of physics impulses and TS's knock-back-off-walls effect are lost as well.

The fix hint stands: record the pilot's xz velocity after StepPilot (BeforePhysics already does this) and add (after-before).xz × knockbackTaken to p.Knockback in AfterPhysics. Validate it in PhysX, because TS's wall knock-back comes from the same mechanism and may need an owner decision.

**Suggested fix.** Record the pilot velocity before Physics.Simulate; in AfterPhysics add (after-before).xz × knockbackTaken to p.Knockback and decay the y component with exp(-5dt), as in finishMotion.

<a id="f53"></a>
### #53 🟡 medium — At the 100-projectile cap a ranged pilot loses one round of ammo per tick (60/s) without firing
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeProjectiles.cs` (35 vs 43 (caller NativeArena.cs:130-133))
- **Original:** src/shared/simulation.ts:372-375 (primary sets p.cooldown before spawnProjectile hits the cap, so at most one round per interval is lost)

**What happens.** In an 8-player match with machine guns or multishot/split, Bullets+AbilityShots+MeleeShots reach 100. Every ranged pilot holding fire then runs `p.Ammo--;Fire(p);` each tick. Fire returns at the cap before `p.Ready` is set, so a 48-round MG magazine empties in about 0.8 s with no shots, then a 2.4 s reload is forced.

**Evidence.** StepPilot: `if(p.Weapon.@class=="ranged"){p.Ammo--;Fire(p);}`. Fire: line 35 `if(ProjectileCount>=100)return;` and line 43 (afterwards) `...p.Ready=Time+(float)(w.interval/p.Stats["attackRate"])...`.

**Verifier's correction.** The finding is accurate as stated: NativeProjectiles.cs line 35 (cap return) runs before line 43 (p.Ready/LastShot), and the caller is NativeArena.cs:128-133. Original order: simulation.ts:376 sets cooldown, then spawnProjectile at :292 hits the cap.

**Suggested fix.** Set p.Ready/LastShot before the cap check, or check the cap before decrementing ammo.

<a id="f54"></a>
### #54 🟡 medium — Ability shots damage props on contact (Flame Lance, Shackles, Tidal Wave); the original does not
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeAbilities.cs` (301)
- **Original:** src/shared/magic-abilities.ts:39-41 (flame-lance onSurface: fx only), 43-44 (tidal-wave onSurface: glide or launchProp dynamic only), 49-51 (shackles: default 'stop', no prop damage)

**What happens.** A Flame Lance (30×scale) hits wooden cover (34 HP). Unity removes 30 HP, and with damage Chips one lance breaks it. Tidal Wave chips cover for 22. In TS these hits leave static cover intact; Tidal Wave only launches dynamic props.

**Evidence.** Default branch of AbilityContact: `else DamageProp(hit.collider.GetComponent<NativePropBody>(),s.Damage,s.Velocity.normalized*(float)(d.id=="tidal-wave"?600:d.impulse),hit.point,s.Owner);`

**Suggested fix.** For flame-lance/shackles do fx only. For tidal-wave apply an impulse to dynamic props only, with no Health damage.

<a id="f55"></a>
### #55 🟡 medium — Authority trusts the client's AbilityTarget point: ground-targeted E can be placed through walls
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeAbilities.cs` (73-76 (GroundPoint 131-135))
- **Original:** src/shared/ability-system.ts:183-186 and abilities.ts:groundAimPoint (the point comes from the authority's own camera ray, so walls stop it)

**What happens.** A modified client or relay peer sends an AbilityTarget behind a wall or inside an enclosed room, within range of its pilot. The host accepts it (clamped only by distance), snaps it to the ground below and detonates Meteor, Geyser or Black Hole there. The honest client derives it from a camera raycast, but the host never re-validates line of sight.

**Evidence.** `Vector3 point=Vec(p.Input.AbilityTarget); ... point=p.Body.position+Vector3.ClampMagnitude(point-p.Body.position,(float)d.range); if(d.kind=="strike"||d.kind=="line"||d.kind=="zone")point=GroundPoint(point);`. GroundPoint only raycasts downward. InputFrame.Valid only limits the length to 10000.

**Verifier's correction.** The core claim is accurate. One addition: NativeSiren.cs:30 has the same trust pattern (`point=...Vec(p.Input.AbilityTarget)...; m.Aim=GroundPoint(p.Body.position+Vector3.ClampMagnitude(point-p.Body.position,28))`), so Siren's ground-aimed abilities can also be placed through walls. The original's own range rule is range plus the camera's distance from the pilot, measured along the camera ray (abilities.ts:75), not a sphere around the body, but that difference is minor next to the missing wall check. The probe is at /tmp/claude-0/-home-user/4cd8d659-77bb-56f7-9323-e1639fb4be49/scratchpad/review-u/verify-abilitytarget/probe.ts.

**Suggested fix.** On the host, recompute or validate the point with a ray from the pilot's camera/eye along the reported yaw/pitch, as aimPoint does, and reject or clip at the first hit.

<a id="f56"></a>
### #56 🟡 medium — Primary gun/magic muzzle is 0.22 m lower and 0.2 m shorter than the source (and than Unity's own ability muzzle)
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeProjectiles.cs` (36)
- **Original:** src/shared/view.ts:11-15 (weaponMuzzle: eye +0.52, forward 0.68, side 0.18)
- **Raised as:** low

**What happens.** A player behind a 1.3 m ledge (foundation `ledge`) fires at a target beyond it. TS's muzzle (about 1.37 m above the feet) clears the lip. Unity's (about 1.17 m) hits the ledge via the eye-to-muzzle sweep or the first contact, so shooting over waist-high cover diverges. Ability shots use the TS values.

**Evidence.** Primary: `Vector3 eye=p.Body.position+Vector3.up*.3f,start=eye+Quaternion.Euler(0,p.Yaw,0)*new Vector3(.2f,0,.48f);` versus ability `Muzzle(p)=>p.Body.position+Vector3.up*.52f+Quaternion.Euler(0,p.Yaw,0)*new Vector3(.18f,0,.68f)` (NativeAbilities.cs:136). The .3/.48 values are inherited from the lab ArenaWorld/GunRules.Muzzle.

**Verifier's correction.** Unity's primary muzzle is 1.15 m above the feet (centre +.30), not about 1.17. The divergence covers both the height of the eye-to-muzzle obstruction sweep and the muzzle position itself, for all gun and magic primaries (NativeProjectiles.cs:36 and :48). Affected cover is anything topping out between about 1.15 and 1.37 m: foundry ledges (1.3), sniper decks (1.2), outer crates (1.2), wall row 0 (1.17). The side-offset sign difference is a coordinate mapping shared with the ability muzzle and is not part of this defect.

**Suggested fix.** Use the same weaponMuzzle (eye .52, .68 forward, .18 side) with a world-ray clamp for primaries.

<a id="f57"></a>
### #57 🟡 medium — ADS is dropped by reload, block and stun, so settle restarts; TS keeps ADS while Q is held
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (120-121, 171)
- **Original:** src/shared/ability-system.ts:69-70 (aiming = ads && input.ability && alive && !rolling && !frozen); abilities.ts adsMove uses input.ability
- **Raised as:** low

**What happens.** A pistol player holding Q reloads. TS keeps adsSince, so the first shot after reload has settled spread and ADS walk speed persists. Unity clears Aiming during the reload and restarts the 0.15 to 0.3 s settle, giving hip-like spread on the first shot. A block or a stun also resets it.

**Evidence.** `bool aim=p.Weapon.@class=="ranged"&&i.Aim&&!rolling&&p.ReloadEnd==0&&Time>=p.BlockEnd&&!Incapacitated(p); if(aim&&!p.Aiming)p.AimAt=Time;` and Reload() sets `p.Aiming=false`.

**Verifier's correction.** Cited lines: NativeArena.cs:120-121 (aim gate), 143 (speed factor), 170-171 (Reload sets Aiming=false), and NativeProjectiles.cs:42 (spread uses AimAt). The bigger player-visible effect is movement: with Q held during a reload, Unity walks at full 6.8 m/s, where TS walks at 0.5-0.75x (twice as fast for the rifle). The settle reset after reload, block or stun adds to that. A frozen pilot shows the opposite divergence: Unity keeps Aiming/AimAt through the freeze, TS clears adsSince. The fix hint is right: gate Aiming/AimAt on Q held, alive, not rolling and not frozen, and apply Ads.move whenever Q is held, as TS adsMove does.

**Suggested fix.** Base Aiming/AimAt on Aim held, alive, not rolling and not frozen only.

<a id="f58"></a>
### #58 🟡 medium — Siren attacker is given the human riposte stun, and rebound knockback ignores Siren armor
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeMelee.cs` (95-101)
- **Original:** src/shared/simulation.ts:316-322 (`if(attacker&&attacker.hp>0&&!this.siren.armored(attacker))` and `riposte=!p.bot&&standardWeapon(p.spec)&&attacker.spec!=='siren'`)
- **Raised as:** low

**What happens.** In a Ward/Siren fight a human sword player blocks a Siren swipe. Unity stuns the boss for up to the 1.4 s+ riposte punish instead of the 0.25 to 0.95 s block stun, and clears its stagger immunity. It also pushes an armored Siren with rebound knockback.

**Evidence.** `bool riposte=!blocker.Seat.bot&&NativeMeleeRules.Standard(blocker.Seat.spec);` with no attacker spec check. `attacker.Knockback+=away*(force/65)*...` is applied even when SirenArmor(attacker).

**Verifier's correction.** The finding is accurate. Two small refinements:
1. Against swipe the stun goes from 0.277 s to about 1.18 s for a sword blocker and 1.4 s for a greatsword blocker, plus travel. Unity also lets the human counter-attack inside its still-active Block window, which the original does not allow here.
2. In the armored case (rend or rush), the stun is correctly skipped by StunMelee. The wrong parts are the 3.3 to 3.8 m/s rebound push and the riposte bookkeeping: blocker.RiposteAt, attacker.RiposteBy and StaggerImmune=0.

Fix: return early from SuccessfulMeleeBlock when `attacker.Hp<=0||SirenArmor(attacker)`, and add `&&attacker.Seat.spec!="siren"` to `riposte`.

**Suggested fix.** Add `attacker.Seat.spec!="siren"` to riposte and skip the rebound when SirenArmor(attacker).

<a id="f59"></a>
### #59 🟡 medium — A corrupt or unsaveable preferences file locks the whole game UI on an error screen; the error is never cleared
- **Area:** Delivery & docs
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (27, 40, 44, 59, 172)
- **Original:** src/client/main.ts:88

**What happens.** Save() writes native-preferences-v1.json with a non-atomic File.WriteAllText on every settings change and match start. If the game crashes or the PC loses power mid-write, the JSON is truncated. On the next launch JsonUtility.FromJson throws inside Awake. The catch sets `error`, so camera and audio setup never runs, and OnGUI draws only the exception text forever. The player has no way to reset and must find and delete %USERPROFILE%\AppData\LocalLow\ChipArenaConversion\Chip Arena Native Conversion TEST\native-preferences-v1.json. The same permanent lock-out (for the rest of the session) follows a transient Save IOException, such as antivirus or OneDrive holding the file, or a failed match.Start(). `error` is never reset anywhere. The Electron game ignores corrupt preferences and falls back to defaults.

**Evidence.** :27 `prefs=File.Exists(PreferencePath)?JsonUtility.FromJson<ConversionPreferences>(File.ReadAllText(PreferencePath)):new ConversionPreferences();` inside Awake's try; :40 `catch(Exception e){error=e.ToString();...}`; :44 `File.WriteAllText(PreferencePath,...)}catch(IOException e){error="Could not save test preferences: "...`; :172 `if(error.Length>0){GUI.Box(...);GUI.Label(...,error,label);return;}`. Searching all Runtime files finds no assignment that clears `error`. Source main.ts:88: `try{pref={...pref,...JSON.parse(localStorage.getItem('chip-arena-preferences')||'{}')};}catch{}`.

**Verifier's correction.** The defect holds for ConversionGame.cs:27/40 (parse inside the global Awake try), :44 (Save IOException) and :172 (`error` is never cleared). Three corrections to the scenario:
1. A process crash in the middle of WriteAllText most likely leaves a 0-byte file, not truncated JSON, because the ~1.3 KB payload is one buffered write. That case is probably handled by FromJson returning null plus the line-28 null check (not checked here; no Unity). True corruption comes from power loss (NUL-filled file on NTFS), a partial write on a full disk, a manual edit, or a read or sharing failure.
2. OneDrive does not normally sync LocalLow.
3. The :59 match.Start() lock is reachable only in edge cases, such as an all-human teams lobby with one side full. Normal solo, Versus and PvE setups always produce two sides.

**Suggested fix.** Parse prefs in their own try/catch and fall back to defaults (keep a .bak of the bad file). Write via temp file plus File.Replace, as NativeAccount already does. Make non-fatal errors dismissible: clear `error` on a button, or show save and start failures in networkStatus instead.

<a id="f60"></a>
### #60 🟡 medium — Event sounds stay muted after a Ward Zero rematch, or after a match that had only one round, because the audio event cursor is never reset for a new arena
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/NativeAudio.cs` (53, 66 (with ConversionGame.cs:50-52; NativeArena.cs:49, 71; NativeReplay.cs:30; NativeSceneAudio.cs:25))
- **Original:** src/client/audio.ts:33-35 (AudioCursor relies on server event ids that never restart, because the Simulation persists across rematches)

**What happens.** Play a Ward Zero run, which produces thousands of events (shots, horde hits, DoT ticks, deaths). Then press Rematch, or press Esc and Start. StartMatch builds a new NativeArena: nextEvent starts again at 1, and Generation is again 1, because a co-op match calls BeginRound/Reset only once. NativeAudio.generation is still 1, so `cursor` keeps the previous run's last event id. Every Shot, Hit, Hurt, Block, Impact, Break, Death and Reload sound is skipped until the new ids pass the old value, or until a killcam ends and calls ResetCursor. The wave-start cue `Once("pve-stage:wave:N",0)` never plays again, because `cues` is cleared only when the generation changes. PvP has the same fault when the previous match ended at Generation 1 (1 round to win, 1 set). Guests are also affected, because they copy the host's generation.

**Evidence.** NativeAudio.cs:53 `if(generation!=arena.Generation){generation=arena.Generation;cursor=0;...cues.Clear();...}`; :66 `if(e.Id<=cursor)continue;cursor=e.Id;`. NativeArena.cs:49 `nextEvent=1` is per instance; :71 `Generation++` happens only in Reset. ResetCursor() is called only from NativeReplay.cs:31/45; ClearReplay()->EndReplay() returns early (`if(!replayRunning&&replayArena==null)return;`), so StartMatch never resets audio. Probe review-u/completeness/audio-cursor (real Core NativeMatch, arena/audio glue modelled exactly): `Ward Zero co-op: first-match end Generation=1, rematch Generation=1; ... rematch first 1000 events played=0/1000`; `PvP 1 round / 1 set: ... 0/1000`; `PvP 2 rounds / 3 sets: ... 1000/1000`.

**Verifier's correction.** The core finding holds as stated. Clarifications:
1. The mute ends early if the local player dies in the new match and the killcam finishes, because EndReplay calls ResetCursor (NativeReplay.cs:31). It also ends early if the user presses Rematch while a killcam is still running.
2. The wave-cue suppression (NativeSceneAudio.cs:25) only happens if the >1024 `cues.Clear()` at NativeAudio.cs:88 did not run after that wave in the previous run.
3. Default PvP settings (rounds=2, sets=3) are not affected. Only Ward Zero, and PvP with exactly 1 round and 1 set, are affected.

**Suggested fix.** Call nativeAudio.ResetCursor() in StartMatch and wherever a guest creates a new replica arena (NativeNetwork.cs:115). Alternatively, key the cursor on the arena instance or on networkEpoch instead of Generation.

<a id="f61"></a>
### #61 🟡 medium — Explosions, jumps, stage hazards, DoT ticks, slams and ability blasts make no sound: the 10-kind event model cannot represent them
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Core/ArenaModel.cs` (85 (consumer Runtime/NativeAudio.cs:67-75))
- **Original:** src/client/audio.ts:31-32, 120-157

**What happens.** Rockets, frag grenades, Meteor Strike, Ball Lightning and Chip bombs play only their release cue and nothing when they explode. In the source, the explosion sound plays at the blast, layered with stone/water/lightning per ability (BLAST_CUE). Razorworks buzzsaws and Thornfall spikes are silent, and so are jumps, the Greatsword landing slam, block start, block pulse, ceiling hits, melee rebound and nova/arc procs. Each burn or poison tick (every 0.25 s) plays the full 'hit' (0.9) for the attacker and 'hurt' for the victim, where the source plays the quiet burn-tick/poison-tick (0.24).

**Evidence.** `enum EventKind { Hit, Block, Dodge, Shot, Death, Break, ReloadStart, ReloadEnd, Headshot, Impact }`. Every authority Emit/Events.Add uses only Shot, Hit/Headshot, Death, Break, Block, ReloadStart/End and Impact. DoT ticks go through Damage(), which always emits Hit (NativeArena.cs:197, 207). Five shipped clips are never referenced anywhere in Runtime: audio/jump.wav, saw-cut.wav, spike-rise.wav, burn-tick.wav, poison-tick.wav. The source plays them in audio.ts:130-131 (`stage-saw-hit`→saw-cut, `stage-spikes-rise`→spike-rise), :149 (burn/poison tick), :152 (`explosion`/`collapse` → BLAST_CUE, default explosion .65), :157 (jump), :124 (melee-slam stone+greatsword). CONVERSION-STATUS.md row: "Audio | 83 original audio files; combat, map/menu/boss music, machine/round/entrance cues".

**Verifier's correction.** One correction: hazards are not fully silent for the victim. A saw or spike victim still hears 'hurt' through the generic Hit event, which matches the source. What is missing is the saw-cut (1.1, positional, audible to everyone) and every spike-rise cue. Also, the Unity cast-release cue at NativeAudio.cs:82 does play a release sound for abilities; only the blast/impact layer is missing. One more problem in the same code: Unity's Damage() emits Hit even when actual==0, while the source only emits a hit when actual>.05. A zero-damage hit therefore still plays the hit/hurt cue in Unity.

**Suggested fix.** Add authority event kinds for explosion/blast (with ability or fx id), jump, hazard hit/rise, DoT tick, slam, block start and reflect. Replicate them in the frame and map them to the source ABILITY_CUE/BLAST_CUE tables. Emit DoT damage with its own kind so it does not trigger the hit and hurt cues.

<a id="f62"></a>
### #62 🟡 medium — No visual status effects: burning, poisoned, slowed, shackled and Orbital-Marked pilots and adds look unaffected
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/NativeWorldEffects.cs` (40-47)
- **Original:** src/client/status-effects.ts:21-47; src/client/pve/horde-view.ts:116-123

**What happens.** A pilot hit by fire weapons or burn Chips, by poison Chips, by a water/frost/Time Warp slow, by Shadow Shackles, or by Orbital Mark shows no flames, poison bubbles, slow droplets, shackle cuffs or mark reticle. This applies in third person, on your own first-person hands, and on Ward Zero adds. Players cannot see that they or an enemy are burning or slowed, although the authority applies the DoT or slow (NativeArena.cs:201-207, Slow()).

**Evidence.** The per-pilot loop draws only `FrozenUntil` bars, the `Controlling` beam, the Siren `mark` ring and the `ShieldUntil` ring. A grep of ArenaPresentation, NativeSourcePresentation, NativeCombatGlow, NativeSummonPresentation, CapturedAnimation and NativeAbilityPresentation finds no read of `Dots`, `SlowEnd`/`SlowFactor`, burn, poison or shackles (0 matches). The source StatusRig (status-effects.ts:21-47) renders flames, embers, poison bubbles, slow droplets, shackle cuffs and the orbital-mark reticle from `p.statuses`/`p.slowed`. Each player gets one rig and the first-person hands get another (`this.hands=new StatusRig(true)`). horde-view.ts:116-123 puts the same rigs on burning/poisoned/slowed adds.

**Verifier's correction.** Core defect confirmed: NativeWorldEffects.cs:40-47 and all other presentation code draw nothing for burn, poison, slow or shackles on pilots or Ward Zero adds. Also, NativeNetworkFrame.cs:11 does not replicate DoT or slow state, so network clients have no data to render these statuses from. Two corrections. (1) Orbital Mark has a crude indicator: the attached "bomb" AbilityZone (NativeAbilities.cs:225, NativeAbilityPresentation.cs:40-43) draws a pulsing cube at the marked pilot's torso centre, mostly hidden inside the body. The source reticle is missing. (2) In the source, first-person hands show only fire and poison; slow, shackles and mark are third-person only (status-effects.ts:27-28).

**Suggested fix.** Replicate per-pilot status flags (DoT kinds, slow kind/until, mark) in the actor snapshot and the add frames. Port StatusRig as pooled instanced voxels in WorldEffects, with a first-person variant.

<a id="f63"></a>
### #63 🟡 medium — Teams mode has no team identity on pilots: the red/blue bands are never drawn
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (261 (also ConversionGame.cs:222; NativeCombatHud.cs:25))
- **Original:** src/client/team-identity.ts:6-14; src/client/renderer.ts:81,122

**What happens.** Enable "Teams — alternating red / blue sides". Pilots are tinted only with their personal body/accent colours, and every bot uses the same default colours (see the bot roster finding). Nothing red or blue appears on the rig, the first-person arms, the draft stage, entrances or death scenes. Allies can be told apart only by health-bar colour or an "ALLY ·" label, which appears within 32 m and only when not occluded. Telling friend from foe at range or behind cover fails.

**Evidence.** Snapshot sets `a.BodyColor=p.Seat.bodyColor;a.AccentColor=p.Seat.accentColor` with no team input. A C-locale grep of Runtime finds no team-based tint in any presentation file; the only team-dependent output is NativeCombatHud.cs:25 `match.Teams?(ally?"ALLY · ":"ENEMY · ")+p.Seat.name`. The source team-identity.ts:6-14 `teamArmor` adds unlit TEAMS-coloured bands to chest, spine and both arms. It is applied in renderer.ts:81 (first-person rig) and :122, draft-stage.ts:164, player-intros.ts:52 and cinematic-victim.ts:10 whenever `gameMode==='teams'`.

**Verifier's correction.** The original also overrides each player's accent colour with the team colour in the teams-mode snapshot (src/shared/simulation.ts:539), so all accent parts of the rig turn red or blue, not only the four bands. Unity has neither the accent override nor the bands. A fix should carry a team colour into Actor or the seat presentation and use it as the accent and band colour whenever match.Teams is set. That covers the third-person, first-person, draft, intro and death rigs, and optionally the team-coloured health-bar fill.

**Suggested fix.** Add the team id to Actor and draw the four unlit band boxes in SourceCharacter, the first-person rig, the draft rig, intro rigs and death-scene victims when match.Teams is set.

<a id="f64"></a>
### #64 🟡 medium — Bot roster diverges: mixed loadouts never include a melee bot, and all bots share one default look and generic names
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (55-56)
- **Original:** src/shared/simulation.ts:133-134

**What happens.** With the default 3 bots and "Bots use my weapon" off, the bots are pistol, rifle and machinegun. With the maximum of 7 bots they are pistol through darkness. Sword, Daggers, Shield and Greatsword sit at catalog indices 7-10, and at most 7 bots exist, so a melee bot can never appear; Ward Zero party bots are always pistol, rifle and machinegun. Block/riposte play against bots therefore needs mirroring. Every bot keeps the NativeSeat defaults (body #e5e7eb, accent #70e1d0, no hat), the same look as a default player, and is named "Pilot 2" to "Pilot 8".

**Evidence.** `var weapons=content.weapons.Where(w=>!w.devOnly).Select(w=>w.id).ToArray();` then `new NativeSeat{id=100+i,name="Pilot "+(i+2),spec=prefs.mirrorWeapons?prefs.weapon:weapons[i%weapons.Length],bot=true,team=...}`. GameContent.json non-dev order: pistol, rifle, machinegun, fire, water, lightning, darkness, sword, daggers, shield, greatsword. The source simulation.ts:133-134 uses `classes=['ranged','magic','melee']`, `spec=selected==='random'?...:selected||CLASS_INFO[classes[n%3]].default` (pistol, fire, sword, ...), `colors=['#70e1d0','#e7ac78','#b591ef','#e27999','#9ede79','#79aeeb','#eed775']` and `names=['Comet','Circuit','Blitz',...]` + ' Bot', plus per-bot host weapon choice and 'random'.

**Verifier's correction.** This is an addition, not a correction. Two different source behaviours apply depending on the path. Local practice (main.ts:113/169) defaults every bot to 'random', a pick from all 11 non-dev weapons. A room created without botWeapons cycles pistol/fire/sword. Ward Zero party bots (src/shared/pve/system.ts:127-133) pick randomly from ranged+melee and are named Comet/Circuit/Blitz with #70e1d0/#e7ac78/#b591ef. In Unity, bot 1's accent happens to match the source's first bot colour. Bots 2+ all share it.

**Suggested fix.** Assign bot weapons as the source does (cycle ranged/magic/melee defaults, or random per bot). Give each bot a distinct accent colour and a source name.

<a id="f65"></a>
### #65 🟡 medium — Relay has no per-address cap on concurrent rooms or pipes: one client can fill all 1000 rooms or squat every slot of a listed room
- **Area:** Networking
- **Where:** `Relay/Program.cs` (16-19, 25, 14)
- **Original:** src/relay/party.ts:17-18
- **Root issue:** The relay's abuse limits will lock everyone out

**What happens.** (a) One address opens 60 host WebSockets per minute and simply leaves them idle. The relay pings keep them open forever, so after ~17 minutes `rooms.Count>=1000` and every new host gets 400. (b) Listed room codes are public via /native/v3/rooms. Seven anonymous joins that never send a hello fill `room.Pipes.Count>=7` and real players get 409. The Unity host drops unseated peers only after 5 s, so 1-2 addresses re-joining keep a room full. The README claims "per-address connection limits", but only a request-rate limit exists. The original relay capped active rooms at 12 per creator and 512 total.

**Evidence.** `if(...||rooms.Count>=1000||...){c.Response.StatusCode=400;return;}` with no per-creator count. `lock(room.Gate){if(room.Pipes.Count>=7){c.Response.StatusCode=409;return;}...}`. Probe: discovery returned the room code; 7 anonymous ClientWebSocket joins were all auto-accepted by NativeRelayHost (`host accepted 7 anonymous pipes; {"rooms":1,"pipes":7}`), and the 8th legitimate NativeRelayHost.Join failed with "status code '409'". README.md:7: "The relay enforces room capacity, one-use tickets, per-address connection limits..."

**Verifier's correction.** The README claim is in Relay/README.md:7, not the top-level README.md. Part (a) is the substantive defect: Program.cs:16 has no per-address or liveness cap on concurrent rooms, where the original had 12 per creator, 512 total, a 12 s host liveness check and a 2 h TTL. A related defect makes it worse: Program.cs:11 keys the limiter on Connection.RemoteIpAddress with no forwarded-header support, so behind the reverse proxy the README recommends, every client shares one 60 requests/min bucket. One client can then exhaust it and get hosting, joining, discovery and the hosts' /accept calls 429'd for everyone, and normal traffic from several 8-player rooms can exceed it too. Part (b) is mostly parity: a squatter who sends a valid hello takes a real seat in both Unity and the original. The new exposure comes only from the opt-in public listing of room codes.

**Suggested fix.** Track concurrent rooms and pipes per client address (forwarded-aware) with small caps (as the original's 12). Add an idle/unauthenticated-pipe timeout at the relay. Correct the README claim.

<a id="f66"></a>
### #66 🟡 medium — Any seated guest can disconnect every other guest by triggering many lobby broadcasts (reliable-queue amplification)
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (55, 85-87, 94, 102-106 (Core/NativeTransport.cs:44))
- **Original:** src/shared/host-room.ts:20-22

**What happens.** In the lobby, a modified client sends 40 "ready"/"team"/"profile" messages (or "rematch" after a match). The host handles up to 120 per peer per frame. Each one calls LobbyBroadcast, which queues one reliable "lobby" message on every peer's connection. Unity's writer cannot drain within the frame, so a victim's reliable queue passes 32 and the host's own Send() calls Fail("Reliable queue overflow"), closing that victim. All other guests drop. They can rejoin within 90 s, but the griefer repeats. "profile" also runs an ECDSA verification per message.

**Evidence.** `if(message.type=="ready"&&arena==null){peer.Ready=message.ready;LobbyBroadcast();return;}`, the same for profile/team/rematch. LobbyBroadcast does `foreach(var p in peers)if(...)p.Connection.Send(json);`. Send: `if(Interlocked.Increment(ref reliableCount)>32){Fail("Reliable queue overflow");return;}`. Probe (review-u/net/amp): 40 Send() calls on a real host-side NativeConnection gave `closed: True error: Reliable queue overflow`.

**Verifier's correction.** File and lines are as reported: Runtime/NativeNetwork.cs:85-87, 94 and 102-106, and Core/NativeTransport.cs:44.

How the attack works depends on Unity's socket I/O completion. NativeConnection's ReadLoop and WriteLoop run on the Unity main thread through the captured UnitySynchronizationContext, so the writer never drains while NetworkTick runs.
- **Reads that complete inline:** one burst of 33 or more lobby-changing messages from one connection drops every guest immediately.
- **Reads that always complete asynchronously (Mono-like):** one connection reads at most one message every two frames. The attacker then needs two seated connections (about 0.5 s) or about 30 s of continuous spam from one connection; the 1 Hz pings tip the balance.

"rematch" also works mid-match, in any phase, so victims can be disconnected during a fight, not only in the lobby. The griefer's own host-side connection overflows too, but it can rejoin at once with its resume token.

**Suggested fix.** Mark the lobby dirty and broadcast at most once per frame or tick. Send lobby state as a latest-value (coalesced) channel. Rate-limit lobby-changing messages per peer.

<a id="f67"></a>
### #67 🟡 medium — A stale match frame sent after "Return room to lobby" puts the guest back into a frozen ghost match with no Ready button
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (112-115, 126-127 (Core/NativeTransport.cs:55))

**What happens.** The host clicks "Return room to lobby": networkEpoch++ and a reliable lobby broadcast with epoch N+1. The last snapshot (epoch N) is often still in the connection's `latest` slot. WriteLoop always sends reliable messages first, so the guest gets lobby(N+1), disposes its arena and sets networkEpoch=N+1, then gets frame(N). ClientMessage accepts it because arena==null and rewinds `networkEpoch=message.epoch`, rebuilding a replica that never updates, with menu=false. The Ready button is shown only when `arena==null`, so the guest cannot ready up and the host's StartMatch waits forever until some other lobby change arrives.

**Evidence.** WriteLoop: `if(reliable.TryDequeue(out data))...else data=Interlocked.Exchange(ref latest,null)`. Client: `if(arena==null||!arena.Replica||message.epoch!=networkEpoch||arena.MapId!=f.map){...arena=new NativeArena(...){Replica=true...};networkEpoch=message.epoch;...menu=false;...}` with no check that the epoch is not older. Return to lobby: `networkEpoch++;foreach(var p in peers)p.Ready=false;LobbyBroadcast();` without clearing pending snapshots. NetworkPanel: `if(!Hosting&&arena==null){..."Ready"...}`.

**Verifier's correction.** The mechanism and the cited lines are correct. Overstated wording: the epoch-N snapshot is not "often" still in `latest`. It is left there only when the connection's writer is busy at the moment of the click: under Unity's main-thread sync context this is mainly right after a 1 s reliable ping, or when writes complete slowly over the relay or a congested link. That came to about 2% of click frames at 60 fps in emulation. The same `networkEpoch++` plus LobbyBroadcast pattern appears in NativeDedicated.cs:21. That case is harmless, because it only fires when no peers are connected. Recovery is possible through any other lobby broadcast, or by the guest using Leave room and rejoining.

**Suggested fix.** Clear each connection's pending snapshot on epoch change, and on the client ignore frames whose epoch is lower than the last lobby or welcome epoch (keep epochs monotonic).

<a id="f68"></a>
### #68 🟡 medium — A disconnected player stays on the host as an invisible but collidable and damageable body, respawned every round
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (54 (with NativeArena.cs:77, 270; NativeProjectiles.cs:75; NativeArena.cs:176))
- **Original:** src/shared/simulation.ts:199, 515-517

**What happens.** A guest disconnects mid-match. The host sets only Seat.connected=false. The pilot's capsule collider and dynamic Rigidbody stay active and Reset() respawns a pilot for that seat every round. The pilot is left out of Snapshot actors (`Pilots.Where(p=>p.Seat.connected)`), so nobody sees it, and guests disable its collider. On the authority, though, bullets are accepted against it (the accept predicate checks only Hp/Enemy/Invulnerable) and Damage() does not check connected. Shots stop mid-air at an invisible body, can kill the absent player, and live pilots collide with an invisible obstacle at that spawn point for up to the 90 s seat lease.

**Evidence.** Disconnect: `peer.Seat.connected=false;arena?.AcceptInput(peer.Seat.id,new InputFrame{Sequence=int.MaxValue});LobbyBroadcast();` with no collider change. Reset: `for(int i=0;i<Match.Seats.Count;i++)SpawnPilot(Match.Seats[i],i);`. Projectile accept: `(p==null||p.Hp>0&&Enemy(owner,p)&&!Invulnerable(p))`. Damage guard: `if(target.Hp<=0||...)return 0;` with no connected check. Guests: `p.Collider.enabled=pf.hp>0&&p.Seat.connected`. Compare the hidden-boss path, which disables the collider and makes the body kinematic (NativePve.cs:28).

**Verifier's correction.** 1. Duration: the problem lasts for the rest of the match, not up to 90 s. The 90 s window (NativeNetwork.cs:74) only controls whether the player can resume. The seat stays in `Match.Seats`, and NativeNetwork.cs:57 removes peers only when `arena==null`.
2. Not a divergence: an invisible capsule that blocks movement and stops shots at the spawn point also exists in the original TS after its 12 s forfeit. TS never disables player colliders, and its projectile filter at simulation.ts:397 has no hp check.
3. The actual divergences:
   - Unity respawns the departed pilot at full HP every round. TS keeps hp=0 (simulation.ts:201 and 515-517; asserted in tests/party.test.ts).
   - The pilot is therefore damageable and killable, and works as a lifesteal and proc source.
   - Killing it produces a ragdoll and a kill broadcast for an absent player.
   - Unity hides it immediately. TS shows it for the 12 s grace.
   - Unity never forfeits the departed player.

The fix needs to do more than disable the collider. To match the original, Unity should forfeit the player (Hp=0 after the grace period) and spawn disconnected seats dead, or despawn them.

**Suggested fix.** On disconnect (and in Reset for disconnected seats), disable the pilot's collider and make the body kinematic, or despawn it, and re-enable on resume.

<a id="f69"></a>
### #69 🟡 medium — Snapshots are ~190 KB of JSON (all static props and default-object padding every 50 ms), deflated separately per peer on the main thread
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (58 (frame built in NativeNetworkFrame.cs:56-73; Encode in Core/NativeTransport.cs:30-34, 44))
- **Root issue:** Match frames are far too large

**What happens.** An 8-player foundry match at 20 Hz. Each frame carries all 262 map props (each with a default ContentBlock), 77 named stats per pilot, and default NativeDeath+Actor, fighter and down objects per pilot. The host calls JsonUtility.ToJson once, then Send() runs UTF-8 conversion and Deflate again for each of up to 7 peers. That is about 1.3 MB of deflate per snapshot burst on the host's main thread every 50 ms, ~40 MB/s of garbage, and host upload of several to ~50 Mbit/s depending on how well the data compresses. The original's p95 raw snapshot was 34.7 KB.

**Evidence.** Size model (review-u/net/size/est2.py) built from the C# field lists: per-pilot frame ~7.0 KB, actor ~1.8 KB, prop ~445 B. 8-pilot foundry frame 189,717 B JSON. Deflate level 1 ranges from 4.8 KB (identical values) to 46 KB (random values), i.e. 97-926 KB/s per guest and 5-52 Mbit/s for 7 guests. Client inputs are ~1.5 KB JSON at 60 Hz because each carries a default NativeGameFrame and profile. `foreach(var peer in peers)if(...)peer.Connection.Send(json,true);` and Send: `byte[] packet=Encode(json);`. DEV_LOG (original game): "raw snapshot p95 34,708 B".

**Verifier's correction.** Confirmed:
- The snapshot JSON is encoded and deflated once per peer, synchronously on the main thread (NativeNetwork.cs:58 calling NativeTransport.cs:44/30-34).
- JsonUtility null-padding inflates the raw frame by about 70%.
- Realistic numbers for an 8-pilot foundry match: 148-169 KB of JSON per frame (not ~190 KB). For 7 peers, 3.4-4.4 ms of CPU every 50 ms on this machine, possibly about 9 ms if Mono uses zlib's default level, and 26-30 MB/s of garbage from Encode alone, about 32-40 MB/s in total.

Refuted:
- The bandwidth and original-comparison claims. The original's 8-pilot foundry snapshot is about 122 KB of raw JSON, also carries every prop at 20 Hz, and was also deflated per client (about 20 KB compressed). The 34.7 KB figure comes from a two-client PvE test.
- Unity's compressed frames (about 13-16 KB) mean host upload is 15-18 Mbit/s for 7 guests, comparable to or below the original. It is not 5-52 Mbit/s.
- The real difference from the original is that the original did its compression on the ws library's thread pool with snapshot dropping, while the port does it on Unity's main thread.

The fix is unchanged: encode once and share the bytes (or move encoding off the main thread), and remove the default padding.

**Suggested fix.** Encode once per snapshot and share the bytes across peers. Send static props once and only changes after that. Drop default padding via explicit optional encoding. Consider a binary delta format like WireCodec.

<a id="f70"></a>
### #70 🟡 medium — Dedicated authority never recovers after a relay disconnect or a failed relay/LAN startup; it keeps running with no room
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeRelayUI.cs` (33-34 (with NativeDedicated.cs:16, 22; NativeNetwork.cs:27-28))

**What happens.** `--ca-server --ca-relay wss://...`: the relay restarts or the control socket drops. RelayTick disposes relayHost and every peer, so Hosting becomes false. DedicatedTick's `if(Hosting&&...)StartMatch()` and NetworkTick do nothing from then on, and nothing ever calls HostRelay again. The same happens if the first relay connect fails ("Relay connection failed") or the LAN port is already in use (SocketException caught into networkStatus). An invalid --ca-bots/--ca-min-players throws inside Awake after `dedicated=true`. In every case the headless process stays alive and idle instead of retrying or exiting, which defeats the README promise of "an unattended server for a room that should outlive an individual player's client".

**Evidence.** `if(relayHost?.Closed==true){networkStatus="Relay disconnected. The local match remains available.";relayHost.Dispose();relayHost=null;foreach(var p in peers){p.Connection?.Dispose();...}menu=true;}`. `if(relayConnecting.IsCanceled||relayConnecting.IsFaulted)networkStatus="Relay connection failed..."`. HostRoom `catch(...SocketException...){networkStatus=e.Message;}`. In dedicated mode none of these paths retries or calls Application.Quit.

**Verifier's correction.** The defect and its location are accurate. Line references: NativeRelayUI.cs:33-34, NativeDedicated.cs:11-16 and :22, NativeNetwork.cs:21 and :27-28, ConversionGame.cs:26, :38, :65 and :171.

Two corrections to the finding's framing. First, the README promise it quotes is about outliving a player's client, which still holds; what actually breaks is recovering from a relay or network interruption. Second, the invalid-argument and port-in-use sub-cases are low severity on their own: the error is logged and the cause is operator misconfiguration. Their real harm is that the process never exits, so a supervisor cannot restart it, whereas the original Node server crashes on EADDRINUSE.

Two related issues make the relay case worse:
- .NET ClientWebSocket KeepAliveInterval has no timeout, so a half-open control socket may never set Closed.
- InvalidDataException from Control/Listen escapes the catch filter, leaving relayHost non-null but no longer accepting players.

**Suggested fix.** In dedicated mode, retry HostRelay/HostRoom with backoff (logging the new room code), and Application.Quit(non-zero) on invalid arguments or unrecoverable bind failures.

<a id="f71"></a>
### #71 🟡 medium — Killcam history deep-copies the whole network frame with JsonUtility 20 times a second in every match, including offline
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeReplay.cs` (21-24)
- **Root issue:** The death replay breaks in versus matches, even offline

**What happens.** In any Fight/Result/Match phase, LateUpdate stores `JsonUtility.FromJson(JsonUtility.ToJson(arena.NetworkFrame()))` every 50 ms and keeps up to 132 frames (~6 s). With ~190 KB frames that is roughly 0.4 MB of string plus the object graph per copy (~15 MB/s of allocation) and tens of MB retained, which Unity's GC must keep scanning. Expect periodic GC hitches with 8 players even when nobody dies. The entity budget (150,000) never trims, because 262 props × 132 frames is only ~35k.

**Evidence.** `if(arena.Time<replayNext&&!fresh)return;replayNext=arena.Time+.05f;var frame=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(arena.NetworkFrame()));replayFrames.Add(frame);` with trimming only by `replayFrames.Count>132||replayEntities>150000||...>6`.

**Verifier's correction.** The scenario is right. Two additions: (a) the original pinned killcam.ts:69-72 shares unchanged props and events between history frames, specifically to fix measured GC pauses (DEV_LOG.md:768-772), so this is a regression of an original fix and not only a new performance issue. (b) The probe puts the per-frame JSON for 8 players on foundry at about 157-193 KB, which is about 0.6-0.8 MB of allocation per 50 ms sample (string plus two object graphs) and about 20+ MB retained across about 120 frames.

**Suggested fix.** Record only what the killcam needs (pilot poses and transient effects, not static props) into pooled structs, or store the already-encoded network bytes on hosts and guests.

<a id="f72"></a>
### #72 🟡 medium — Paid revives and solo self-revives teleport the downed player to a spawn pad instead of reviving them where they fell
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (69, 78-82, 127)
- **Original:** src/shared/pve/economy.ts:239-244 (reviveNow -> revive(t,t.death.pos)), 204-206 (self-revive at p.death.pos), 173-176

**What happens.** A teammate goes down at (5,0,5). A player stands next to the body and presses F, paying $150. In the original the teammate stands up on that spot at 50% HP. In Unity, RevivePve always picks the nearest add-free pad from Content.pads, e.g. (0,0,21), so the teammate disappears from beside the reviver and reappears up to about 20 m away, often in another part of the hall. Solo self-revive after 5 s does the same. Two players revived together can also land on the same pad, because there is no 'taken' list like respawnPad has. RevivePve also refills mana, stamina and ammo; revivePlayer in the original keeps them.

**Evidence.** NativePve.cs:69 `...RevivePve(dead);` (F revive) and :127 `RevivePve(row.Pilot)` (self-revive) both call RevivePve, and line 81-82 does `var pad=safe.Length>0?safe.OrderBy(...).First():...; ... p.Body.position=Vec(pad.Position(.9f)); ... p.Mana=(float)p.Stats["maxMana"];p.Stamina=...;p.Ammo=Magazine(p);`. The TS code is `this.revive(t,t.death.pos,PVE_EVENT.revive,...)` for the paid revive and `this.revive(p,p.death?.pos??p.body.translation(),...)` for the self-revive. Only respawnAll (wave/phase) uses respawnPad.

**Verifier's correction.** The finding is accurate as stated: NativePve.cs lines 69, 78-83 and 127, against economy.ts:173-176, 204-206, 239-244 and 178-186.

Two minor additions. First, the same RevivePve is also used for the boss-phase respawn (line 106) and the wave-clear respawn (line 136), and neither keeps a `taken` list. Second, the self-revive at line 127 skips the original's `!inVentShaft` wait (economy.ts:204), so a solo player who falls into a vent self-revives at death+5 s, mid-shaft, onto a pad, instead of at the landing spot after death+10 s.

Fix: give RevivePve a target position. Use the body or death position for the F revive and the self-revive, and gate the self-revive on !InShaft. Keep pad selection, with a taken list, only for the wave and phase respawns. Stop refilling mana, stamina and ammo.

**Suggested fix.** Pass a target position to RevivePve. Use the body or death position for F revives and self-revives (standAt), and keep pad selection, with a taken list, only for wave-clear and phase respawns. Do not refill mana, stamina or ammo.

<a id="f73"></a>
### #73 🟡 medium — 'Find me...' hunt ends automatically after 60 s even when an add is possessed, leaving a harmless possessed add that pays the bounty again
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (141 (with 103, 116; NativePveHorde.cs:49,57,106))
- **Original:** src/shared/pve/director.ts:98-105; src/shared/pve/add-ai.ts:71-78 (possessedMode returns 'chase' at age>=60)

**What happens.** Phase 2 begins and add #N is possessed. The players fail to kill it within 60 s. In the original, forceRevealAfter fires only when nothing could be possessed; otherwise the possessed add switches to 'chase' at 60 s and the hunt goes on until it dies. In Unity, the boss reappears at the south spawn at exactly 60 s whatever happens. The add keeps Pve.Possessed=true, so it never attacks (the attack gate requires !a.Possessed) and keeps 3x HP. When someone later kills it, PveAddKilled pays the possession bounty ($300 to the killer, $100 to everyone else) a second time.

**Evidence.** NativePve.cs:141 `if(v.Hidden&&Time-v.PossessionAt>=60)FoundPveBoss(new Vector3(0,0,24.2f));`. This has no check on v.Possessed. FoundPveBoss sets `v.Possessed=0` but never clears `add.Pve.Possessed`. NativePveHorde.cs:106 `if(a.Possessed)foreach(var p in Members())GrantPveMoney(p,p==paid?300:100,"possessed")`. The TS code is `if(this.possessed===undefined){ ... else if(time>=possessionAt+forceRevealAfter) this.found(...)}`.

**Verifier's correction.** The bounty is not paid twice. The 60 s forced reveal pays nothing, and the $300/$100 'possessed' bounty is paid once, later, when the stale possessed add dies in phase 3 or 4. In the TS the bounty is tied to finding the Siren. The add never attacking matches the TS (possessed adds never attack there either). The Unity-only defect is that the add stays possessed (x3 HP, keep-away/chase AI, possessed flag sent to clients) after the boss has been force-revealed.

Fix: force the reveal only when v.Possessed==0 (or no add has Pve.Possessed). After 60 s let the possessed add chase, as line 57 already does. If a forced reveal is still wanted, clear add.Pve.Possessed and undo the HP scale, as TS Horde.release does.

**Suggested fix.** Force the reveal only when v.Possessed==0. After 60 s let the possessed add chase, as possessedMode does. If a forced reveal does happen, clear the add's Possessed flag and HP scale.

<a id="f74"></a>
### #74 🟡 medium — Possession picks the oldest living add (even one still entering or crawling) and keeps its damage, instead of a far, active add at full 3x HP
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (103)
- **Original:** src/shared/pve/horde.ts:448-464 (possess)

**What happens.** When the chip-2 reveal ends, Unity possesses `Minions.FirstOrDefault(m=>m.Pve!=null&&m.Hp>0)`. That is whichever add was created first. It is usually the one already fighting the party, and it may still be climbing out of a vent, crawling, or have taken damage. The original seeds a choice among the five active, non-crawling adds farthest from every human, resets them to full HP x3 and clears their limb pools. With no active add, it sends a fresh shambler through the farthest door. In Unity the hidden Siren often ends up in an add right next to the players, at partial HP (for example 30/60 becomes 90/180 instead of 180/180). The fallback shambler uses a random entry.

**Evidence.** NativePve.cs:103 `var add=Minions.FirstOrDefault(m=>m.Pve!=null&&m.Hp>0);if(add==null)add=SpawnPveAdd("shambler",true);if(add!=null){...add.Hp*=3;add.MaxHp*=3;}`. The TS code: `pool=[...list].filter(a=>a.phase==='active'&&!a.crawl&&a.minion).map(a=>({a,d:far(...)})).sort((x,y)=>y.d-x.d||x.a.n-y.a.n).slice(0,5)` ... `a.maxHp*=hpScale;a.hp=a.maxHp;a.parts={}`. The fallback is `doors...sort((x,y)=>y.d-x.d)` and `this.enter(a,door.e)`.

**Verifier's correction.** The finding is accurate as stated. Two smaller related problems in the same code:
1. Unity does not cancel the chosen add's windup, so a possessed add can still land one strike (NativePveHorde.cs:46 does not check Possessed). The original never lets a possessed add attack.
2. If SpawnPveAdd returns null (every entry weight is 0), nothing is possessed and the boss stays hidden until the forced reveal at 60 s (NativePve.cs:141). TS always queues the shambler via `??doors[0]`.

**Suggested fix.** Port possess(): choose among the five active, non-crawling adds farthest from living humans (seeded), set Hp to MaxHp*3, clear Parts and cancel the windup. Fall back to the farthest door entry.

<a id="f75"></a>
### #75 🟡 medium — Possession hints are missing: no glow, grin, 'FIND ME...' whisper or screen-edge arrow toward the possessed add
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (57 (possessed flag only drives AI; NativeNetworkFrame.cs:14/62 replicates `possessed` but no presentation reads it))
- **Original:** src/client/siren/minions/hints.ts:1-15 (levels 0/15/35 s, glyph within 25 m); src/client/pve/hud.ts:98-100,125-133 (banner, whisper, arrow every 8 s for 1.2 s after 35 s); balance.ts PVE_SIREN.possession.hints

**What happens.** During 'Find me...' the original makes the possessed add pulse violet from the head. From 15 s a translucent Siren grin floats over it within 25 m. From 35 s the HUD flashes a 'FIND ME...' screen-edge arrow toward it every 8 s. In Unity the only cue is the 'FIND THE POSSESSED' title during the hold. The possessed add looks like any of the 72-192 other adds, so the hunt has no visual way to identify the target.

**Evidence.** A case-insensitive grep for 'possess|hint|whisper|grin' across Assets/ChipArena/Runtime finds only the AI (NativePve.cs, NativePveHorde.cs), the network frame field and the UI title string `v.Reveal==2?"FIND THE POSSESSED"`. No presentation code reads Pve.Possessed or the possessed minion. CONVERSION-STATUS lists 'boss phases' as implemented, and DEV_LOG says 'Other phases, hints, possessed HP and rewards retain their rules'.

**Verifier's correction.** The missing presentation is in Assets/ChipArena/Runtime/NativeSourcePresentation.cs:28-43 (SourceWorld never reads m.Pve.Possessed or PossessedAt) and Assets/ChipArena/Runtime/NativePveUI.cs:10-38 (no post-hold "FIND ME..." banner, whisper or arrow). Also missing: the impostor violet marker (horde-view.ts:64-66,110) and the level-2 black-flame trail (horde-view.ts:209). The DEV_LOG quote is from a TS-era 2026-10-06 entry, not from the Unity author. The relevant unbacked Unity claim is CONVERSION-STATUS.md's "boss phases". Impact is softened because the 60 s forced reveal (NativePve.cs:141) and the keep-away/stare AI remain, so the hunt still ends. Medium is the right severity.

**Suggested fix.** Render the hint levels from PossessedAt on the possessed add (head glow, then the grin glyph within 25 m, then the HUD arrow ping), using the replicated `possessed` flag.

<a id="f76"></a>
### #76 🟡 medium — In PvE, adds block line of sight for the Siren's attacks and for other adds' strikes; the original ignores adds
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeSiren.cs` (22, 42, 46, 77 (MeleeVisible); NativePveHorde.cs:83 (SummonVisible))
- **Original:** src/shared/siren.ts:43,66,67,90 (lineOfSight(...,!!s.pve) = ignoreAdds in PvE); src/shared/pve/horde.ts:273,279 (lineOfSight(eye,qp,a.id,q.id,true))

**What happens.** In the boss phase with dozens of adds around the party, an add stands between the Siren and a player. In the original, Grave Slam, Eruption, Rend/Swipe, the Last Laugh catch and target acquisition (visibleTarget) all pass ignoreAdds in PvE and hit. In Unity, MeleeVisible raycasts ShotMask, which includes layer 11 (adds), so the Siren's own horde soaks her attacks and SirenTarget can return null. In the same way, an add standing behind another add cannot land its swipe or brute smash, because SummonVisible treats the front add as an obstacle. In TS horde strikes ignore adds. This lowers both boss and horde damage in crowded fights.

**Evidence.** NativeArena.cs:42 `ShotMask=(1<<PropLayer)|(1<<ActorLayer)|(1<<11)` and adds use `obj.layer=11` (NativePveHorde.cs:21). NativeMelee.cs:49-53 MeleeVisible returns false when the nearest hit is not the destination. NativeSummons.cs:52-53 SummonVisible only excludes `from.Collider`. In TS: `this.sim.lineOfSight(add(pos,v(0,.12,0)),t.body.translation(),p.id,t.id,!!this.sim.pve)`.

**Verifier's correction.** The finding is accurate as stated. A few details sharpen it:
1. The Siren hellfire/trail patch check (NativeSiren.cs:94) and the damnation prop check (:107) correctly keep blocking by adds. Both match TS siren.ts:100 and :111, which do not pass ignoreAdds. The fix should therefore cover only SirenTarget, SirenArea, SirenMelee, the laugh catch at :77 and PveAddStrike, not every MeleeVisible caller.
2. NativePveHorde.cs:132 SirenBot needs SirenTarget(p,60) to pass a visibility check before it acquires a chase target. TS bossTarget (boss.ts:77-80) has no LOS requirement at all. So with adds or props in the way, the Unity boss can go without a target where the TS boss would not. That makes the divergence larger than the finding describes.
3. Two smaller parity gaps in PveAddStrike/SummonVisible:
   - The eye height is +0.3, where TS horde.ts:265 uses +0.45, or +0.1 when crawling.
   - SummonVisible lacks the `timeOfImpact>=len(d)-.12` end tolerance that TS lineOfSight has.

**Suggested fix.** In PvE, exclude layer 11 from the visibility mask used for Siren checks (SirenTarget, SirenMelee, SirenArea, laugh catch) and for PveAddStrike, matching ignoreAdds.

<a id="f77"></a>
### #77 🟡 medium — Medic recruits heal one target with distance falloff and half the cap; the original pulses heal every party member and recruit in range
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (112, 117-119)
- **Original:** src/shared/pve/recruit-ai.ts:221-235 (medicPulse), 183-207; src/shared/pve/recruits.ts:16-17; balance.ts RECRUIT.medic

**What happens.** A medic stands within 4 m of three teammates, each at 50% of 100 HP. The original heals all three every 0.5 s: min(rate 6 x 0.5 = 3, capFraction 0.04 x 100 = 4) = 3 HP each, about 6 HP/s per person. Additional medics stack at 0.5^k. Recruits are healed too. Unity heals only the focus target (the hirer, or a member below 35%) by min(3 x (dist<=4.5 ? 1 : 0.5) x underFire, 0.04 x 0.5 x hp) = 2 HP per pulse, 4 HP/s. Everyone else gets nothing, and the 9 m beyond-4.5 m falloff is invented. Recruit loadouts also diverge: DPS recruits are only rifle or sword (the original draws from 7 ranged/melee weapons), and medics carry a water staff instead of being weaponless pistol medics.

**Evidence.** NativePveHorde.cs:117-119 `var target=recruit.Owner.Hp>0?recruit.Owner:...; ... Heal(target,Math.Min(3*(distance<=4.5f?1:.5f)*(Time-target.LastDamage<1?.6f:1),(float)target.Stats["hp"]*.04f*.5f));`. TS: `const targets=[...host.members(),...host.party().filter(q=>host.isRecruit(q))]; for(const t of targets){... total=c.reduce((n,x,k)=>n+x.amount*M.falloff**k,0); total=Math.min(total,M.capFraction*t.stats.hp); if(underFire) total*=.6; s.heal(t,total,...)}`. Line 112 `spec=role=="tank"?"shield":role=="medic"?"water":...<.5?"rifle":"sword"`.

**Verifier's correction.** The finding is accurate. In Unity, the under-fire 0.6 is applied before the cap; the original applies it after. This changes nothing at 100 HP (1.8 either way). Unity medics also never heal themselves or other recruits, because `Members()` excludes recruits (NativePve.cs:26). Unity per-medic pulses start immediately (HealAt=0) rather than on a shared 0.5 s schedule, which is a minor difference. There is also a likely extra issue: a Unity medic keeps `Shoot` from `Bot(p)` (NativeArena.cs:254) because `PveBot` (NativePveHorde.cs:126) only nulls `target`. With its water staff it may attack enemy pilots, while the original medic is unarmed.

**Suggested fix.** Port medicPulse: every 0.5 s heal every living member and recruit within 9 m with LOS, with per-medic 0.5^k stacking, a 4% max-HP cap per pulse and x0.6 under fire. Use the TS role weapon lists.

<a id="f78"></a>
### #78 🟡 medium — Player hits knock adds back twice as far as the original
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (96)
- **Original:** src/shared/pve/horde.ts:352-353 (k=min(extMax, impulse/collider.mass*.5))

**What happens.** A pistol shot (impulse 175) on a shambler. The original adds min(6, 175/40 x 0.5) = 2.19 m/s of external velocity, only to active adds, and caps the accumulated ext at 6. Unity adds min(6, 175/40) = 4.38 m/s, so displacement under exp(-6t) decay is about 0.73 m instead of 0.36 m. Every weapon below impulse 480 (pistol, machine gun, daggers, magic, sword) pushes the horde back twice as far. Unity also accumulates External on entering adds and applies it when they activate.

**Evidence.** NativePveHorde.cs:96 `a.External+=new Vector3(dir.x,0,dir.z)*Math.Min(6,impulse/40);`. TS: `if(a.phase==='active'){const k=Math.min(HORDE.extMax,impulse/HORDE.collider.mass*.5);...}`.

**Verifier's correction.** The push is exactly 2x only for impulse below 240 (pistol, machine gun, daggers and other low-impulse hits). From 240 to 480 Unity hits the cap of 6 early: sword 300 is about 1.6x, rifle 350 about 1.37x. At 480 and above the two match. Separately: knockback from hits while an add is entering (from ExposedAt to ActiveAt, up to 1.15 s for vent re-emergence) is stored without decay and applied in full on activation, and the stored External has no cap. Fix in NativePveHorde.cs:96: `if(Time>=a.ActiveAt){a.External+=new Vector3(dir.x,0,dir.z)*Math.Min(6,impulse/40*.5f);a.External=Vector3.ClampMagnitude(a.External,6);}`

**Suggested fix.** Use Math.Min(6, impulse/40*0.5f), apply it only to active adds (Time>=ActiveAt), and clamp the accumulated External magnitude to 6.

<a id="f79"></a>
### #79 🟡 medium — Horde navigation allocates a full BFS (about 185 KB) whenever a target changes cell, and rebuilds the grid with 3,465 physics overlaps on any prop-count change
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (142-151)
- **Original:** src/shared/pve/nav.ts:62-75 (buildFlowField reuses an Int32Array `out` and shared buckets; grid built from block data, not physics queries)

**What happens.** 4 humans, 3 party bots and 3 recruits move at about 6 m/s, so each crosses about 6 one-metre cells per second. Every add think calls Distance() for each enemy, which recomputes that target's flow field after each cell change. Each Flow() allocates `Enumerable.Repeat(...).ToArray()` (3,465 ints) plus a `new[]{...}` array for every dequeued cell (about 3,000), giving roughly 60 BFS/s and about 10 MB/s of garbage in the main simulation loop. Unity's GC will spike frames. Separately, any fracture or debris removal changes Props.Count and triggers 3,465 OverlapCapsule queries in a single tick (within 0.5 s) and clears every cached flow.

**Evidence.** Line 149 `int[] steps=Enumerable.Repeat(int.MaxValue,blocked.Length).ToArray();var queue=new Queue<int>();`. Line 150 `foreach(int to in new[]{x>0?at-1:-1,x<62?at+1:-1,z>0?at-63:-1,z<54?at+63:-1})`. Line 144-145 `if(propCount==arena.Props.Count)return;...for(int i=0;i<blocked.Length;i++){...scene.OverlapCapsule(...)}`. Line 44 calls `pveNavigation.Distance(...)` for every enemy on every think.

**Verifier's correction.** The defect holds, with these numbers adjusted:
- Each recompute allocates about 122 KB measured on .NET 8 for the real Ward Zero grid (2,676 reachable cells), roughly 140-160 KB on Unity Mono/IL2CPP. The finding's 185 KB is a little high.
- There are at most 7 non-boss targets (hiring stops at Pilots.Count>=8, which includes the boss pilot), not 10. The worst case is about 56 BFS/s, roughly 8 MB/s; 4 moving humans give about 4 MB/s.
- The 3,465-OverlapCapsule grid rebuild only runs on the first PvE tick and when Props.Count changes: a non-fragment prop fracturing (about 18 destructible originals on the stage, each fracturing once) or a prop falling below y=-15. That is a few one-off 5-10 ms hitches per match, not a recurring cost.
The main issue is NativePveHorde.cs:149-150: no buffer reuse and no per-tick build budget. The original keeps both (nav.ts:68, 71, 117, 152-156).

**Suggested fix.** Keep per-target int[] buffers and a reusable queue, unroll the 4-neighbour loop, and rate-limit recomputes per target. Rebuild only cells overlapping changed props, or spread the rebuild across ticks.

<a id="f80"></a>
### #80 🟡 medium — DEV players can enter Ward Zero co-op as the Siren; that kit cannot damage adds (the original forces the class default)
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (96-99 (SanitizedSeat keeps preferredSpec 'siren'); ConversionGame.cs:48-60 (no PvE spec fallback))
- **Original:** src/shared/pve/system.ts:120-123 (onPlayerAdded: a non-boss 'siren' spec falls back to CLASS_INFO default)

**What happens.** The owner (DEV) has the Siren selected and starts Ward Zero co-op. Unity seats them as a team-0 Siren, a second Siren next to the boss. SirenMelee, SirenArea and SirenTarget iterate only Pilots, so the player's claws, slams and eruptions never touch any add during waves. They can only hurt the boss. Their dev-siren intro also starts the boss song early (see the separate finding). The original swaps any non-boss Siren to the class default weapon on join.

**Evidence.** SanitizedSeat: `if(!content.weapons.Any(w=>w.id==spec&&(!w.devOnly||access.Allows("weapon:"+spec,...))))spec="pistol";`. This has no PvE exception. NativeSiren.cs:42/45 `foreach(var q in Pilots)` are the only damage targets. TS: `if(!boss&&p.spec==='siren'){p.spec=CLASS_INFO[p.class].default;...}`.

**Verifier's correction.** Fallback target: the original uses CLASS_INFO[p.class].default, which is sword for a melee-class profile and emperor for a 'dev'-class profile. Unity has no separate class and derives @class from the weapon (melee), so sword is the right default. Additional impact the finding missed: the Unity DEV Siren party member also gets 15000 HP (Core/ConversionContent.cs:81), against 100 in the original. A related path seats bots as Sirens when mirrorWeapons is set with prefs.weapon=="siren" (ConversionGame.cs:56); with Guest access they get 15000 HP and no attacks. Relevant lines: NativeNetwork.cs:96-100, ConversionGame.cs:53-56, NativeSiren.cs:42/45-46/94/103.

**Suggested fix.** When prefs.pve is set, replace a human or bot 'siren' spec with the melee class default before StartMatch, as onPlayerAdded does.

<a id="f81"></a>
### #81 🟡 medium — Online clients render raw 20 Hz host frames with no interpolation buffer or local prediction (source interpolates and predicts)
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeNetworkFrame.cs` (104-119, 126-127 (ConversionGame.cs:105; Core/NativePoseHistory.cs:36))
- **Original:** src/client/snapshots.ts:18-40; src/client/main.ts:347,385 (predictor.renderPosition); src/shared/network-config.ts:2
- **Root issue:** No movement prediction or smoothing for guests

**What happens.** Once networking connects, ApplyEffects rebuilds Bullets, ability shots, melee shots and fields from the newest frame only (20 Hz), and minions jump to frame positions. Only remote pilot positions get exponential smoothing (InterpolateReplica). The render clock on clients is `NativeVisualClock.Sample(match.Time,...)`, capped at the last applied frame time + 1/60 s. All animation clocks therefore hold for about 33 ms and then jump 50 ms. The local player's own movement waits a full host round trip plus up to 50 ms because there is no prediction. Players see projectiles, zombies and swings stepping at 20 Hz and laggy self-movement. The original buffers 75–350 ms with extrapolation and predicts the local pilot. CONVERSION-STATUS does not list this as a deliberate adapter.

**Evidence.** NativePoseHistory.cs:36 `value=Math.Max(value,simulationTime+Math.Max(0,Math.Min(Step,sinceFixedStep)))`, where simulationTime advances only in ApplyNetwork. snapshots.ts samples a cursor `newest.state.time+(now-newest.at-this.delayMs)/1000` and interpolates players and PvE, and main.ts uses `predictor?.renderPosition(dt)` for the local pilot.

**Verifier's correction.** Small corrections:
- The visual clock does not hold 33 ms and then jump 50 ms. It advances about 14-17 ms, holds about 33-40 ms, then jumps about 35-41 ms (probe above).
- Minion animation ages on clients use arena.Time (NativeSourcePresentation.cs:31-35), which is the raw last-frame time, not the visual clock. Their animation therefore steps at a pure 20 Hz.

Related but unverified: on the replica, pilot and minion positions are written only through Rigidbody.position (NativeNetworkFrame.cs:85, 99, 127). Physics.simulationMode is Script (ConversionGame.cs:31), and the replica never calls Physics.Simulate. Physics.SyncTransforms only pushes Transform changes into physics, not the other way. If Unity behaves as its docs describe and updates the Transform only after a simulation step, then the camera (`p.Body.transform.position`, ConversionGame.cs:134) and minion rigs (`m.Body.transform.position`, NativeSourcePresentation.cs:31) could stay frozen on clients. That would be worse than this finding, but it needs a Unity run to confirm.

**Suggested fix.** Keep a small frame buffer on replicas and render at a delayed cursor, interpolating actors, shots and minions (as snapshots.ts does). Add local movement prediction or at least velocity extrapolation.

<a id="f82"></a>
### #82 🟡 medium — Fog shader variants are stripped from the Windows player, so the EXE renders without the fog seen in the Editor
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeLighting.cs` (36-37 (Editor/PrototypeBuild.cs:57-67; Scenes/Prototype.unity; Resources/NativeLit.shader, NativeSourceLit.shader, NativeSourceTransparent.shader))

**What happens.** Fog is enabled only at runtime (`RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear`). The only scene in the build (Prototype.unity) has no RenderSettings block, so fog is off there. ConfigureRendering changes only `m_InstancingStripping` and leaves GraphicsSettings Fog Modes at Automatic. Unity then strips FOG_LINEAR variants that no included scene uses. All lit materials are surface shaders whose fog comes from the implicit multi_compile_fog: props, map models and characters (NativeLit/SourceLit/SourceTransparent). Play mode in the Editor, which has every variant, shows fog from 30–118 m / 65–210 m, but the built EXE shows none.

**Evidence.** PrototypeBuild.cs ConfigureRendering only does `graphics.FindProperty("m_InstancingStripping").intValue=2`. Prototype.unity contains only a GameObject, a Transform and SceneRoots.

**Suggested fix.** In ConfigureRendering also set m_FogStripping=1 (Custom) and m_FogKeepLinear=1, or enable linear fog in the scene's RenderSettings.

<a id="f83"></a>
### #83 🟡 medium — Synchronous main-thread decode of ~82-86 MB embedded JSON at startup and at the first intro/death of a match
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeSourceModels.cs` (25-27 (ArenaPresentation.cs:73 via ConversionGame.Awake:32; NativeSceneMotion.cs:13,25; NativeSourceMotion.cs:14))

**What happens.** Awake builds NativeSourceModels. Decode() takes `TextAsset.text` (6.5 MB), base64-decodes it, gunzips it to an 82M-character string with StreamReader.ReadToEnd, and parses it with JsonUtility, all on the main thread. The first SourceStage call, which every match makes because ShowIntros=true, lazily loads SceneMotion: an 85.7M-character JSON plus LINQ validation of about 10M floats. SourceMotion (14M chars) follows. The game freezes for seconds at launch and again at the first entrance of the first match. A hosting player's freeze stalls every peer. Mono's non-moving heap stays inflated after several hundred MB of transient strings.

**Evidence.** Probe (review-u/engine/decode, CoreCLR, excluding the JsonUtility parse): `SceneMotion: decoded chars=85,664,110 managed alloc=446 MB time=1786 ms`; `SourceModels: decoded chars=81,912,544 managed alloc=371 MB`; `SourceMotion: 14,357,060 chars, 85 MB`.

**Verifier's correction.** The cost is once per process, not once per match: the SceneMotion/SourceMotion caches are static. It lands on the first entrance frame of the first match, or earlier if an entrance preview is opened. At launch it is SourceModels (~82M chars) plus building 5,422 meshes and materials. The 6.5 MB TextAsset figure applies to SourceModels; SceneMotion's TextAsset is 13.5 MB. The sim pauses rather than skipping (maximumDeltaTime=.1), and no network timeout fires. The visible effects are a multi-second hang at the first match start, peers stalled while the host freezes, and a slower client missing the start of the first 4.8 s entrance. The "heap stays inflated" claim was not verified.

**Suggested fix.** Convert once in the Editor to binary assets (Mesh/AnimationClip/ScriptableObject or a compact binary blob), or decode on a worker thread during the menu with a Unity-free parser and hand the results to the main thread. Avoid the base64-in-JSON wrapper.

<a id="f84"></a>
### #84 🟡 medium — Horde rendering allocates about 0.5 MB of garbage per frame with 192 adds (array initialisers inside per-part visibility callbacks)
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeSourcePresentation.cs` (28, 40-41 (NativeSummonPresentation.cs:43))

**What happens.** In Ward Zero's possession phase the cap is 192 adds (NativePve.cs PveFlow). For each add, every frame, SourceWorld creates a local function `bool Visible(int b){int[] bits={1,2,4,...},bones={5,7,11,...};...}`. NativeSourceModels.Queue calls it once per model part, and horde models have 21–24 parts. That is about 4,200 calls per frame, each allocating two int[9] arrays, plus a closure and two delegates per add. At 144 fps this is roughly 65–70 MB/s of garbage, causing frequent Boehm GC pauses over a heap already holding the motion banks. The same pattern is in MinionPartVisible. Line 28 also does an O(n²) `minionRigs.Keys.Where(id=>!arena.Minions.Any(...)&&!arena.MinionDeaths.Any(...)).ToArray()` every frame.

**Evidence.** Section counts from SourceModels.json: horde-0..5 have 24/23/22/21/22/23 parts. NativeSourcePresentation.cs:40 `bool Visible(int b){int[] bits={1,2,4,8,16,32,64,128,256},bones={5,7,11,8,12,14,18,15,19};...}` is passed to `sourceModels.Queue(...,Visible)`, which invokes `visible(part.Bone)` per part.

**Verifier's correction.** The magnitude is a little higher than stated. In Unity (Mono or IL2CPP), it is about 0.6-0.7 MB per frame at 192 adds, not about 0.5 MB, because Mono's int[9] is 72 bytes. The 192 cap needs PartySize 4, but bots count toward it, so one player with three bots reaches it. Below that, the cap is 72, 120 or 168 adds for parties of 1-3, and the garbage scales with the add count. Fix: hoist bits and bones to static readonly arrays, or better, precompute a visible-bone bitmask from `lost`, so Visible becomes a mask test with no per-part allocation. Cache the per-rig delegates. Replace the line-28 LINQ with a HashSet of live and dying ids, reused across frames.

**Suggested fix.** Make bits/bones static readonly arrays, precompute a per-add visible-bone mask once per change of `Lost`, and avoid per-frame closures and LINQ in SourceWorld.

<a id="f85"></a>
### #85 🟡 medium — Host compresses the same 20 Hz snapshot separately for every peer on the main thread
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (58 (Core/NativeTransport.cs:30-34,44))
- **Root issue:** Match frames are far too large

**What happens.** Every 50 ms the host serializes one frame JSON and then calls `peer.Connection.Send(json,true)` for each peer. Send() runs `Encode(json)` (UTF-8 + Deflate) inside the call, so 7 peers mean 7 identical compressions per snapshot on the main thread, on top of the JsonUtility serialization.

**Evidence.** Probe (review-u/engine/encode) with the real NativeConnection.Encode on a 130 KB JSON payload (the snapshot size VALIDATION.md cites): `Encode(130 KB) = 1.26 ms on CoreCLR; host with 7 peers @20 Hz re-encodes per peer: 176 ms of main-thread time per second`.

**Verifier's correction.** Line references are correct: Runtime/NativeNetwork.cs:58, Core/NativeTransport.cs:30-35 and :44. The finding's measured cost (1.26 ms per encode, 176 ms/s) used a non-representative slice of GameContent.json. With a realistic ~150 KB snapshot, CoreCLR at Fastest gives about 0.57 ms per encode, about 4 ms per tick and about 80 ms/s for 7 peers, plus about 1.5 MB of avoidable garbage per tick. Under Unity Mono it is probably about 1.3 ms per encode and about 9 ms per tick, if Mono applies zlib's default level as I believe; that part is unverified. Relay rooms are affected the same way, since each relayed peer has its own NativeConnection. Fix: encode once per snapshot, for example with a Send(byte[] packet, bool snapshot) overload, and share that byte[] across all peers.

**Suggested fix.** Encode once per snapshot and enqueue the same byte[] to every connection (add Send(byte[] packet,bool snapshot)), or move encoding off the main thread.

<a id="f86"></a>
### #86 🟡 medium — Every local match deep-copies the whole world through JsonUtility 20 times per second for the replay buffer, on top of other per-frame LINQ allocations
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeReplay.cs` (23 (NativeArena.cs:260-270 via ConversionGame.cs:106; NativeCombatHud.cs:16-26; NativeAudio.cs:79))

**What happens.** ObserveReplay runs in every match, including offline single-player and Ward Zero with up to 192 adds. Every 0.05 s of match time it builds NetworkFrame() with LINQ: 77 stats per pilot, every prop, minion, effect and event. It serializes this to JSON and parses it back, even if no replay is ever shown, and keeps up to 132 frames. In addition, each LateUpdate calls Snapshot(true): Wounds.ToArray, ViewCopy per actor, a Shots array with a closure-based Array.Find per bullet, and Events.ToArray. NativeAudio sorts all events with OrderBy every frame. CombatWorldHud runs once per IMGUI event (at least Layout+Repaint). Each run raycasts once for every damaged minion and allocates a new GUIStyle for every name label.

**Evidence.** NativeReplay.cs:23 `var frame=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(arena.NetworkFrame()));`. NativeCombatHud.cs:16 `bool ScreenPoint(...){...return !physics.Raycast(...)}` is called per minion in OnGUI, and line 23 has `var style=new GUIStyle(small){...}` inside Health().

**Verifier's correction.** The NativeAudio OrderBy is at NativeAudio.cs:64, not :79 (:79 is the per-pilot loop that builds strings like p.Seat.id+":"+"roll" every frame). The Snapshot builder spans NativeArena.cs:256-270. This is also a parity regression: it diverges from src/client/killcam.ts:62-73 (record shares unchanged props and events between frames, the 2026-10-04 lag-spike fix in commit 764b9d5, part of baseline 05950ea). The Unity port deep-copies every prop and event in every frame. On a host, NetworkFrame() is built a second time every 50 ms in addition to the peer broadcast at NativeNetwork.cs:58. The "up to 192 adds" figure applies only to the hidden boss stage with 4 or more human players; normal waves cap at 10-18 alive.

**Suggested fix.** Record replay history as compact structs (positions, yaw, hp, death, effects) without JSON. Reuse snapshot buffers. Cache GUIStyles. Compute HUD visibility once per frame in LateUpdate and only draw in OnGUI (Repaint).

<a id="f87"></a>
### #87 🟡 medium — Build inspection panel throws for Ward Zero money Chips
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeInspect.cs` (14)

**What happens.** In Ward Zero a human pick can offer a PvE money Chip (RollPveOffers adds one 30% of the time; ChoosePve installs it via `p.Seat.chips[chip]=count+1`). Holding G and clicking that pilot runs `seat.chips.OrderBy(c=>content.Chip(c.Key).name)`. ConversionContent.Chip throws `ArgumentException: Unknown chip: bounty-scanner` because the six money chips exist only in PveContent.moneyChips. The exception is thrown inside OnGUI after GUILayout.BeginArea/BeginScrollView. It repeats on every IMGUI event, leaves the layout groups unbalanced and skips the rest of the HUD (lock-on, replay/PvE HUD). The open panel also keeps the cursor unlocked and blocks play until Escape.

**Evidence.** Probe: money chip ids from PveContent.json are absent from GameContent.chips: `('bounty-scanner', False), ('payday-loop', False), ('headhunter', False), ('boss-contract', False), ('gold-rush', False), ('midas-core', False)`. Chip() is `Array.Find(chips,...)??throw new ArgumentException("Unknown chip: "+id)`.

**Verifier's correction.** Parity reference that the finding left out: src/client/player-board.ts:36 (and src/client/pve/board.ts:71) in the original use CARD_CHIPS, which includes PVE_MONEY_CHIPS, so the original board does show money Chips. The defect also reaches network clients, because NativeNetworkFrame.cs:78 copies the host's money Chip ids into replica seats. Unverified (no Editor): whether the Close button still responds after the exception. Escape (ConversionGame.cs:66) does close the panel.

**Suggested fix.** Use arena.PveChip(id), which falls back to the money chips, and guard against unknown ids in UI code.

<a id="f88"></a>
### #88 ⚪ low — The sandbox 'rarity' sort orders chips alphabetically by rarity id instead of by tier
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Runtime/NativeSandbox.cs` (53)
- **Original:** src/shared/sandbox-tools.ts:20-29
- **Raised as:** medium

**What happens.** A dev picks sort 'rarity' in the sandbox chip list. Chips come out grouped ansatsuken, common, epic, l44t, legendary, mishima, mythic, rare, siren, uncommon. The owner-requested tier order is common, uncommon, rare, epic, legendary, mythic, then the DEV kits mishima, ansatsuken, l44t, siren. DEV-exclusive chips land first and between ordinary tiers, and rare/uncommon land after legendary. The original's 'rarity-desc' and catalogue-order 'default' sorts are also missing.

**Evidence.** NativeSandbox.cs:53: `content.chips.OrderBy(c=>sandboxSort=="rarity"?c.rarity:...:c.name).ThenBy(c=>c.name)` sorts on the raw rarity string. A .NET probe sorting the ten rarity ids GameContent.json uses gives `ansatsuken, common, epic, l44t, legendary, mishima, mythic, rare, siren, uncommon`. Original sandbox-tools.ts:20-29: `RARITY_ORDER=['common','uncommon','rare','epic','legendary','mythic','mishima','ansatsuken','l44t','siren']` with the comment "owner 2026-10-04: the sandbox list sorts strictly by tier". Claim in CONVERSION-STATUS.md:20: "Developer tools | Signed local sandbox with sorted Chip grants...".

**Verifier's correction.** The defect is at NativeSandbox.cs:53, with the choice list at :48 and the default value at :45. Besides the alphabetical rarity order and the missing 'rarity-desc' and catalog-order options, the default sort also diverges: the original opens on 'rarity' and Unity opens on 'name'. Severity should be low, not medium, because this affects only the developer-only local sandbox tool and has no effect on gameplay or networking.

**Suggested fix.** Sort by the index in a RARITY_ORDER array (unknown rarities last), then by name, and add the descending and default orders.

<a id="f89"></a>
### #89 ⚪ low — Remembered sign-in is fragile: never retried after a startup failure, wiped by a mistyped password, and account saves are silently dropped while a request is in flight
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Runtime/NativeAccount.cs` (37, 48, 58 (and NativeProfileUI.cs:41))
- **Original:** src/client/main.ts:413; src/client/accounts.ts:14; desktop/accounts.cjs:32, 43

**What happens.** (1) At launch the remembered-session 'me' request fails, for example because Wi-Fi is not up within the 5 s timeout. Account stays null, and Update only refreshes when Account!=null, so the session is never retried and the player must type the password again. The TS client retries every 5 minutes whenever a session exists. (2) With that login form showing, one mistyped password (or a wrong recovery code) returns 401. Request then calls Clear(), which deletes conversion-account.bin even though the remembered session was still valid. The desktop broker only clears on 401 for non-authenticating actions. (3) Clicking 'Save appearance' while the 5-minute background refresh or an admin request is in flight calls account.Save → Start, which returns silently because Busy is set. The cloud profile is not saved and nothing is shown; the TS desktop broker queues requests.

**Evidence.** Line 58: `private void Update(){if(Account!=null&&!Busy&&Time.unscaledTime>=refreshAt)Refresh();}`. Line 48: `if(request.result!=UnityWebRequest.Result.Success){if(request.responseCode==401)Clear();...` applies to login/register/recover too, and Clear(erase=true) deletes SessionPath. Line 37: `if(Busy)return;`. NativeProfileUI.cs:41 calls account.Save outside the `GUI.enabled=!account.Busy` guard. Original: main.ts:413 `setInterval(()=>{if(accounts.session)void accounts.refresh()...},5*60*1000)`; desktop/accounts.cjs:43 `if(response.status===401&&!authenticating)this.clear();`; accounts.cjs:32 queues requests.

**Verifier's correction.** The desktop/accounts.cjs line references are 13 (request queue) and 24 (`if(response.status===401&&!authenticating)this.clear();`), not 32 and 43. Admin requests set Busy in NativeAdmin.cs:16/21. A further consequence of (3): the next launch's remembered-session refresh loads the stale cloud profile with loadProfile=true and overwrites the locally saved appearance.

**Suggested fix.** Retry the refresh whenever session.Length>0, not only when Account!=null. Clear on 401 only for non-authenticating actions. Queue pending account actions instead of dropping them, or report 'busy, try again'.

<a id="f90"></a>
### #90 ⚪ low — An empty or whitespace entrance message produces a blank intro banner instead of the default text
- **Area:** Accounts & security
- **Where:** `Assets/ChipArena/Runtime/NativeProfileUI.cs` (13)
- **Original:** src/shared/cosmetics.ts:15

**What happens.** A player clears the 'Entrance message' field, or types only spaces or control characters. SanitizedSeat (NativeNetwork.cs:100) stores "" and the intro shows an empty line under the name (NativeIntros.cs:14). The original falls back to 'Ready to make an impact'. Internal whitespace runs are also not collapsed, and the 64-character cut is by UTF-16 unit rather than code point, so an emoji at position 64 can be split into a lone surrogate.

**Evidence.** NativeProfileUI.cs:13: `value=Regex.Replace(value??"","[<>\\p{Cc}\\p{Cf}]"," ").Trim();return value.Substring(0,Math.Min(64,value.Length));`. Original cosmetics.ts:15: `Array.from(value.replace(...).replace(/\s+/g,' ').trim()).slice(0,64).join('')||DEFAULT_COSMETICS.introText!`. A tsx run of introText('   ') returns "Ready to make an impact".

**Verifier's correction.** The primary defect is at Runtime/NativeProfileUI.cs:13, as reported. Two related spots are also affected:
- NativePreview.cs:22 shows the raw prefs.introText in the preview banner without any sanitising or fallback.
- NativeProfileUI.cs:26 copies the account's c.introText into prefs as is.

The lone-surrogate case needs a remote or crafted profile, or a paste that IMGUI's 64-unit maxLength cuts mid-pair. It is cosmetic only.

**Suggested fix.** Collapse whitespace, cut by text elements or code points, and return "Ready to make an impact" when the result is empty.

<a id="f91"></a>
### #91 ⚪ low — Melee recovery, anticipation, hit-stop and block-impact captures are simplified: hilt hand leaves the greatsword, daggers lose their captured recovery
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (168-182)
- **Original:** mocap-layer.ts:79-81 (anticipation sword [1,.6,1], daggers .35), 89-90 (daggers keep the clip recovery), 135-140 (blockHit); mocap-pose.ts:113-126 (chest-space wrist recovery + supportHand re-solve), 143-154 (18 rad/s wrist limiter); primary-melee-motion.ts:192-201 (HIT_STOP)

**What happens.** (a) Recovery: every set, daggers included, blends the local rotation of every bone from the m[2] pose to the live idle. The source fits both endpoints, interpolates wrists in chest space, re-solves the two-handed support grip, keeps the daggers' captured m2→m3 recovery and limits wrist speed. (b) The partial-anticipation blends and the hit-stop pause are absent. (c) The captured blockHit (block impact) is never played at runtime. (d) Recovery length is VisualDuration-W-A, i.e. stretched to the weapon interval: shield 0.39 s vs 0.3 s, greatsword 0.60 s vs 0.5 s.

**Evidence.** Probe: in the Unity local-slerp recovery from greatsword cutA m[2] to idle, the left hand is up to 9.3 cm from the hilt grip (cutB 7.3 cm), although both endpoints hold it at 0.0 cm (twohand = 1). No anticipation, hitStop or blockHit references exist in Assets/ChipArena.

**Verifier's correction.** The support-hand gap during the greatsword recovery depends on the live idle time Unity samples. It reaches up to 14.3 cm on cutA and 7.9 cm on cutB at the mid-blend (eased k = 0.5), above the 9.3 / 7.3 cm reported. The 18 rad/s wrist-limiter part is already documented as not ported in ANIMATIONS.md:68, so the actionable divergences are:
1. Daggers' captured recovery replaced by a slerp to idle.
2. Greatsword support grip not re-solved.
3. Local-space instead of chest-space wrist interpolation.
4. Missing partial anticipation and hit-stop.
5. blockHit never played.
6. Recovery stretched to the weapon interval.
Affected lines: CapturedAnimation.cs:168-176 and 178-182.

**Suggested fix.** Port applyMocapRecovery and finishMocapRecovery (including supportHand), the anticipation blend and HIT_STOP, and use meleeRecovery(spec)/rate for the cut's recovery span.

<a id="f92"></a>
### #92 ⚪ low — Reload hand and magazine choreography ignore reload-speed Chips
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Core/RangedPose.cs` (19-25 (also GunPresentation.cs:54))
- **Original:** src/client/ranged-motion.ts:48-59 (reloadDuration = max(.2, w.reload*reloadSpeed))

**What happens.** A pistol player with 5× Quick Loader (reload ×0.84^5 ≈ 0.42) or Drum Magazine (×1.2). ReloadPhase divides elapsed time by the base GunRules reload. With Quick Loader the authoritative reload ends at u ≈ 0.42, while the hand is still at the pouch: the magazine is never seated and the envelope fades out. With Drum, u reaches 1 at 83% of the reload and the hands rest at the start pose for the remaining time.

**Evidence.** `float elapsed=Math.Max(0,time-a.ReloadAt),duration=GunRules.Get(a.Weapon).Reload;` whereas NativeArena.cs:171 sets ReloadEnd=Time+max(.2, reload*reloadSpeed).

**Verifier's correction.** The finding is accurate as stated. It also applies to Medic Magazine (x1.12), Single Chamber (x0.6) and Gunsmith Tuning (x1.06). The fix applies in both places: use a.ReloadUntil-a.ReloadAt in RangedPose.ReloadPhase (it is at least 0.2, so the division is safe while a reload is active). In GunPresentation.cs:54, compute fall from the same real duration, i.e. elapsed - .3*(ReloadUntil-ReloadAt), as ranged-motion.ts:211 does.

**Suggested fix.** Use duration = a.ReloadUntil - a.ReloadAt.

<a id="f93"></a>
### #93 ⚪ low — Documented mocap regeneration command produces a file that does not compile
- **Area:** Animation & presentation
- **Where:** `tools/import_mocap.py` (17)

**What happens.** ANIMATIONS.md says: 'Run python tools/import_mocap.py /path/to/Chip-Arena to regenerate from the pinned source files.' The script writes `namespace ChipArena.UnityPrototype`, but CapturedClip and every consumer live in ChipArena.UnityConversion.

**Evidence.** Running a scratch copy against the pinned source gives output identical to the committed CapturedClips.Generated.cs except for the namespace. Compiling Core with it fails: `CapturedClips.Generated.cs(5,24): error CS0246: The type or namespace name 'CapturedClip' could not be found`. The committed file was hand-edited after generation.

**Verifier's correction.** Defect: tools/import_mocap.py line 17. The documentation claim it breaks is in ANIMATIONS.md line 80. The fix is to emit `namespace ChipArena.UnityConversion {`. After that change, the regenerated output is byte-identical to the committed CapturedClips.Generated.cs.

**Suggested fix.** Emit `namespace ChipArena.UnityConversion {` in import_mocap.py.

<a id="f94"></a>
### #94 ⚪ low — First-person view layer omits the source sway, stride bob, landing kick, recoil push and reload dip
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeViewPresentation.cs` (60-75)
- **Original:** src/client/first-person-view.ts:51-73, 69 (fighter offsets 0/-.48/-.2)

**What happens.** Turning, running, landing, firing or reloading in first person. Unity places the view rig at a fixed offset plus the ADS spring, so the view model never sways, bobs, kicks or dips. Emperor and Ansatsuken also get the melee offset and torso settling instead of the source's fighter offsets.

**Evidence.** RenderFirstPerson computes `offset` only from class and the ADS weight. There are no look-sway, landing or recoil springs and no reload or run-low terms.

**Verifier's correction.** This adds one point to the original finding. L33T H4x (`leet`, source `class:'magic'`) also gets the melee offset (.16, -.22, .30) instead of the magic offset in Unity, because `MagicVisual` is 0 for leet (NativeArena.cs:261). Of the missing terms, the most visible are the reload dip/roll (6 cm and about 14° at full envelope) and the recoil push. The landing kick is only about 1.4 mm, and look sway is a few mm, up to the 3 cm clamp.

**Suggested fix.** Port poseFirstPersonView's spring layer and per-class offsets.

<a id="f95"></a>
### #95 ⚪ low — Every new wound re-carves all wounded parts on the main thread (10-82 ms in .NET 8 for 8 wounds on one part)
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeWoundGeometry.cs` (28-35)
- **Original:** src/client/body-wounds.ts:316-340 (reconcile rebuilds only dirty parts; carvePart keyed cache)

**What happens.** Wounds are capped at 8 per pilot with the oldest dropped (NativeDeaths.cs:19). Once a pilot has 8, every further wound changes the signature, and WoundedPilot frees every carved part and re-carves every wound group. The source rebuilds only the parts whose wound set changed.

**Evidence.** Probe on the ribcage wound part (warm JIT, .NET 8): 8 sequential cavity wounds 10.2 ms, puncture 37.4 ms, cut 82.4 ms, producing 1,229-1,679 solids that then become meshes. Under Unity Mono this is a per-hit frame spike for each heavily wounded pilot.

**Verifier's correction.** The defect is that Runtime/NativeWoundGeometry.cs:31-34 throws away and re-carves/re-meshes every wounded part whenever any wound changes. The source's body-wounds.ts:310-321 rebuilds only the dirty parts: the new wound's parts plus the parts of the wound dropped by the cap of 8. The carve cost for 8 wounds on a single part (the finding's 10-82 ms) is the same in the source, which also re-carves that part from the base. What the port adds is the cost of the unchanged parts: in .NET 8, about 0.5-5 ms extra per hit with wounds spread across the body, about 1.4-2.9 times the source's carve time, and 1.6-4 times as many solids turned into Meshes per hit. Unity Mono would be slower in absolute terms. The source has no keyed carve cache (carvePart recomputes every time), so the fix is per-part dirty tracking only, not a carve-result cache.

**Suggested fix.** Track carved parts per wound group and rebuild only the changed part; cache carve results by key as the source does.

<a id="f96"></a>
### #96 ⚪ low — Per-frame GC allocations in presentation hot loops
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeSourcePresentation.cs` (28, 40 (also NativeCombatHud.cs:23,34; NativeCombatGlow.cs:60,69,79,92; NativeFirstPersonPose.cs:63; MagicPose.cs:124))

**What happens.** Ward Zero hordes. NativeSourcePresentation's per-minion Visible() local function allocates two int[9] arrays on every call, and it is called for every model part of every minion every frame (on the order of 48 minions × ~30 parts × 2 arrays). Other allocations: a LINQ minion-rig sweep, a new GUIStyle per health-bar name and per machine label on every OnGUI event, a new List/OrderBy/ToArray/Concat in NativeCombatGlow.Render, and new arrays in SettleTorso and the MagicPose guard.

**Evidence.** Line 40 `bool Visible(int b){int[] bits={1,2,4,...},bones={5,7,11,...};...}`. NativeCombatHud.cs:23 `var style=new GUIStyle(small){...}` inside Health(). NativeCombatGlow.cs:79 `glowShots.OrderBy(...).Take(...).ToArray()`.

**Verifier's correction.** The main hotspot is NativeSourcePresentation.cs:40-41: two `int[9]` arrays per model part per minion per frame, plus a closure and two delegates per minion. Horde and summon models have 21–24 parts, not about 30. Horde size is capped at 40 offline and 28 on the relay, not 48. Measured cost is about 229 KB/frame, or about 13.7 MB/s at 60 fps, with 40 minions. The SettleTorso allocation is at Core/NativeFirstPersonPose.cs:23, not line 63, and it runs once per frame for the view rig only, so it is negligible. The MagicPose.cs:124 allocation happens only while a magic actor is guarding. The NativeCombatGlow/CombatHud allocations are real but small: a few hundred bytes per frame, plus native GUIStyle objects on each OnGUI event.

**Suggested fix.** Hoist constant arrays to static readonly fields, cache GUIStyles, reuse lists/buffers.

<a id="f97"></a>
### #97 ⚪ low — Break impacts always play the 'stone' sound
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeAudio.cs` (73)
- **Original:** src/client/audio.ts:151 (uses e.material)

**What happens.** Wood or metal cover is destroyed. NativeDestruction.cs:42 removes the prop from arena.Props before emitting the Break event, so the lookup `arena.Props.Find(p=>p.Definition.id==e.SurfaceId)` finds nothing, mat is null, and 'stone' plays.

**Evidence.** NativeDestruction.cs:42 `Props.Remove(prop);...Emit(EventKind.Break,source,0,position,0);` (no surface or material carried on the event). NativeAudio.cs:73 `string mat=prop?.Definition.material;Play(mat=="wood"?"wood":mat=="metal"?"metal":"stone",...)`.

**Suggested fix.** Carry the material on the Break event, as the source does.

<a id="f98"></a>
### #98 ⚪ low — Reach Chips do not lengthen the drawn blade
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/NativeSourcePresentation.cs` (20-22)
- **Original:** src/client/character-animation.ts:57 (r.weapon.scale.z = p.reach/w.range for melee)

**What happens.** A player takes Hurricane Edge (+35% reach) or Bloodblade (+20%). The source scales the weapon's length by reach/range, so the visible blade matches the longer hit range. Unity draws the fixed weapon model, and the Actor carries no reach value.

**Evidence.** WeaponBone returns MeleeHand(...) with no scale, and ArenaPresentation.Weapon uses constant lengths (line 225).

**Verifier's correction.** The most common trigger is the common Extended Reach chip (+20% per stack, max 5, so up to 2x blade length in the source), more than Hurricane Edge or Bloodblade. The same gap also hits the first-person view model (Runtime/NativeViewPresentation.cs:84) and the daggers' off-hand blade, which the source scales through offWeapon.scale.copy. A related piece of the same chip visual is also missing in Unity: the source's weapon-visuals.ts:105 `if(s.reach>1){...box(group,0x92f5e5,.12,0,.26+length/2,.025,.06,length,true);}` stripe. A search found no chip-modules or weaponModifiers equivalent anywhere in Unity.

**Suggested fix.** Send reach in the actor snapshot and scale the weapon matrix along the blade.

<a id="f99"></a>
### #99 ⚪ low — Replicated actor presentation fields are unvalidated; a bad host frame can throw every render frame
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (167-170 (validator: NativeNetworkFrame.cs:39))

**What happens.** The replica validator checks actor Id, Position and Wounds only. If a host (or a mismatched build) sends Combo < 0, then `'A'+a.Combo%n` produces a nonexistent cut name, Find() falls back to the idle clip, and AttackTime reads `Marks[0]` from an empty array. The IndexOutOfRangeException aborts Render for every client every frame while that actor is 'attacking'. NaN values in VisualWindup, VisualDuration or RigScale similarly propagate into transforms.

**Evidence.** NativeNetworkFrame.cs:39 validates `a.Wounds`, `seats.Any(s=>s.id==a.Id)` and Position only. CapturedClip.AttackTime line 57 `attackMarks[0]=Marks[0];`.

**Verifier's correction.** Can only be triggered by a deliberately modified or malicious host. A mismatched build is rejected by the version and sourceCommit checks at NativeNetworkFrame.cs:32, and an honest host clamps Combo≥0 at NativeArena.cs:263. The exception path is: negative Combo plus host-controlled AttackAt/AttackUntil (also unvalidated) -> CapturedAnimation.cs:167 builds "cut@"/"cut?" -> Find falls back to the idle clip (empty Marks) -> CapturedClip.cs:57 throws IndexOutOfRangeException every LateUpdate (ConversionGame.cs:106). NaN timing or RigScale values do not throw; Unity rejects NaN transform assignments with an error log, so they cause only visual glitches and log spam. Impact is client-side render failure and log spam, not a process crash. Fix: in Valid(), reject Combo<0 (as WireCodec.cs:71 does), VisualSet/MagicVisual out of range, and non-finite or negative VisualWindup/VisualActive/VisualDuration/RigScale/AttackAt/AttackUntil. Alternatively, clamp in ActorPose (for example, take the remainder of a non-negative Combo, and make Find never return a clip without marks for cut states).

**Suggested fix.** Validate Combo ≥ 0, VisualSet/MagicVisual ranges and finite timing fields for replicated actors, or clamp them in ActorPose.

<a id="f100"></a>
### #100 ⚪ low — Gun carry returns 0.4 s after a shot while moving; the source holds aim for max(0.38, interval+0.15)
- **Area:** Animation & presentation
- **Where:** `Assets/ChipArena/Runtime/CapturedAnimation.cs` (195)
- **Original:** src/client/ranged-motion.ts:102

**What happens.** A rifle player (interval 1.15 s) runs and fires. Unity drops the rifle into the carry pose 0.4 s after each shot. The source keeps the aim pose for 1.3 s, until the next shot is available.

**Evidence.** `time-a.LastShot<.4f` versus the source `pose.shotAge<Math.max(.38,w.interval+.15)`.

**Verifier's correction.** The divergence affects the pistol (0.4 s vs 0.57 s) as well as the rifle (0.4 s vs 1.30 s). The machine gun is effectively at parity (0.4 vs 0.38). For exact parity, the fix should use the raw weapon interval as the source does, `Mathf.Max(.38f, GunRules.Get(a.Weapon).Interval+.15f)`, not interval/attackRate.

**Suggested fix.** Use max(.38, interval/rate+.15).

<a id="f101"></a>
### #101 ⚪ low — Hosted-room Rematch is silently refused until every guest has pressed Rematch, and never starts automatically
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (50, 195; NativeNetwork.cs:94)
- **Original:** src/shared/host-room.ts:32 and src/server/index.ts:59 (rematch votes from all connected humans incl. host, auto-start, 'n/m' notice)

**What happens.** After a LAN or relay match, the host clicks Rematch: StartMatch returns with networkStatus 'Wait for the other pilots to be ready.', which is only shown inside the menu's network panel, so nothing visibly happens. Guests clicking Rematch get no feedback, and once all have clicked, the host must click again. Guests can also pre-arm Ready by sending 'rematch' at any phase.

**Evidence.** `if(Hosting&&peers.Any(p=>p.Seat!=null&&p.Connection?.Closed==false&&!p.Ready)){networkStatus="Wait for the other pilots to be ready.";return;}`. The guest handler is `else if(message.type=="rematch"){peer.Ready=true;LobbyBroadcast();}` with no phase check. networkStatus is rendered only in NetworkPanel (NativeNetwork.cs:120).

**Verifier's correction.** One detail needs narrowing. "Pre-arm at any phase" applies only while an arena exists with matching epoch and generation (NativeNetwork.cs:88), meaning Countdown, Fight or Draft of the current match. Only a modified client can do it, because the guest button only appears in Match phase. The impact is negligible. The rest of the scenario is accurate. Lines: ConversionGame.cs:50 (refusal), 195 (button), 196 (menu panel suppressed on the match-end screen); NativeNetwork.cs:94 (guest handler), 120 (only display of networkStatus); NativeDedicated.cs:22 (auto-start exists only for the dedicated authority).

**Suggested fix.** Treat Rematch as a vote (host included), show the vote count on the match-end screen, accept votes only in Match phase, and start automatically when all connected humans have voted (as the dedicated path already does).

<a id="f102"></a>
### #102 ⚪ low — L44T Y33t: the ×1000 knockback is applied to pilots but not to cover
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (181; NativeProjectiles.cs:129; NativeMelee.cs:76)
- **Original:** src/shared/simulation.ts:271 (directHit: `if(this.leet.active(p,'yeet'))impulse*=1000;` before both the player and damageProp branches); leet-content.ts:147 ('Hits launch enemies and cover with a thousand times the knockback')

**What happens.** A Leet pilot with Y33t shoots a loose crate or wall chunk. In the source the prop is launched with 1000× impulse ('Goodbye, arena'). In Unity it gets the normal impulse.

**Evidence.** The only yeet multiplier is inside pilot Damage: `if(source!=null&&primary&&direct&&Leet(source,"yeet")){impulse*=1000;...}`. The prop path is `DamageProp(prop,b.Damage*b.Demolition,b.Velocity.normalized*b.Impulse,hit.Point,owner)` with no Leet check (grep 'yeet' finds only NativeArena.cs:181).

**Verifier's correction.** The defect is only at Assets/ChipArena/Runtime/NativeProjectiles.cs:129, the BulletImpact prop branch, when the bolt is direct (b.Direct, no reflections, owner Leet with l44t-yeet). The NativeMelee.cs:76 citation is not a real divergence: L33T is a magic projectile weapon, so a yeet owner never swings melee, and the source melee prop path does not apply yeet either. The fix is to scale the impulse by 1000 in that branch when b.Direct && Leet(owner,"yeet"). This also scales the fracture impulse for static cover, which matches the source.

**Suggested fix.** Multiply the prop impulse by 1000 for direct hits from a yeet owner in BulletImpact and the melee prop branch.

<a id="f103"></a>
### #103 ⚪ low — Several chip handlers are partial: Siren-armor checks and summon paths missing (coin-toss, trampoline, swap-shop, vampire-dash, sticky-bomb, storm-recall)
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeChips.cs` (58, 59, 63, 98, 102; NativeAbilities.cs:181)
- **Original:** src/shared/chaos.ts:55-56, 59, 64, 65, 76, 77; src/shared/magic-abilities.ts:53

**What happens.** Heads Or Tails knockback and Bounce House's upward kick move an armored Siren (the source skips armored targets). Trading Places does nothing at all against an armored target (the source still swaps and only skips the freeze), and applies a melee stun instead of the leet freeze. Coin-toss heal never triggers on summon hits, Blood Trail never drains summons, Sticky Situation does not attach to a zombie/toy it hits, and Storm Recall ignores the +1 refund for hitting summons.

**Evidence.** Coin-toss: `if(random.NextDouble()<.5)target.Knockback+=dir*...` with no `SirenArmor(target)`. ChipJump: `q.Knockback.y+=8*...` with no armor check. Swap requires `!SirenArmor(target)&&...` and then `StunMelee(target,.5f)`. vampire-dash loops only `Pilots`. The sticky attach passes `rule.id=="sticky-bomb"?target:null` where target is null for minion hits. storm-recall uses `struck.Count*2` (pilots only), while the source uses `2*(hit.size+ +(r.minions>0))`.

**Verifier's correction.** Storm-recall (NativeAbilities.cs:180-181) does count summons hit by the blink line segment (`struck.Add(m.Id)`), matching the source. It drops only the source's +1 (2 s) for the end blast damaging any summon, because AbilityBlast returns pilots only. All other sub-claims hold as stated: NativeChips.cs lines 58 (coin-toss armor and summon heal), 59 (swap is blocked entirely on an armored target and uses StunMelee instead of the FrozenUntil leet freeze), 63 (sticky bomb cannot attach to summons; NativeChipField has no minion slot), 98 (vampire-dash ignores summons) and 102 (trampoline armor).

**Suggested fix.** Add the armor guards, include summons in vampire-dash and coin-toss as in chaos.ts, let fields attach to minions, and count minion hits for storm-recall.

<a id="f104"></a>
### #104 ⚪ low — Late Bloomer + Splitter fragments and Sidewinder extras diverge because CopyBullet drops motion state
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeProjectiles.cs` (145-149; NativeChips.cs:136, 147)
- **Original:** src/shared/simulation.ts:299 (splitShot spreads `...shot`, keeping chaos.scale); src/shared/chaos.ts:88, 97 (extras/satellites spread `...base`, keeping zigzag)

**What happens.** (1) A Late Bloomer shot that splits at age 0.1 s: the fragment keeps the 'accelerating-shot' mod but MotionScale resets to 1, so the next step multiplies its speed by scale(age) instead of scale(age)/parentScale. Fragments fly about 0.43× (early split) to 2× (late split) the source speed for their whole life. (2) With Sidewinder, Rear Guard/Four-Way/Double Helix extras and Satellite shots do not weave in Unity; in the source they inherit zigzag.

**Evidence.** CopyBullet copies Mods and BaseVelocity but not MotionScale or Zigzag (`new NativeBullet{...Homing=b.Homing,Burn=b.Burn,...Mods=new Dictionary<string,int>(b.Mods)}`). ChipShotMotion then does `s.Velocity*=scale/s.MotionScale;s.MotionScale=scale;`.

**Verifier's correction.** Locations: CopyBullet is at NativeProjectiles.cs:149 (used by SplitBullet at :145 and by Extra at NativeChips.cs:136), the acceleration step is at NativeChips.cs:147, and the zigzag step is at NativeProjectiles.cs:68. TS references: simulation.ts:291 (splitShot), :395 (zigzag), and chaos.ts `spawned`/`satellites`/`shotStep`.

The finding describes TS fragments as each keeping chaos.scale. In fact the TS spread shares a single `chaos` object across the parent and all fragments, so only the first fragment (in map order) accelerates and the rest keep the split speed. Unity diverges from both cases:
- **Late split:** all fragments are 2× too fast.
- **Earlier split:** the accelerating fragment is scale(split age)× too slow, while the others accelerate when the original's do not.

So the suggested fix (copy MotionScale) would match the TS first fragment but not the others. Exact parity would need to model the shared scale, and the owner may prefer the evidently intended per-fragment behaviour instead.

Adjacent, separate issue (not part of this finding): Unity homes shots that are `(b.Direct||b.WeaponFx)` (NativeProjectiles.cs:70), and Extra keeps basis.Homing. TS homes only `shot.direct` shots (simulation.ts:396). So with the homing chip, Unity extras home and TS extras do not.

**Suggested fix.** Copy MotionScale (and Zigzag for extras/satellites) in CopyBullet, matching the source's object spread.

<a id="f105"></a>
### #105 ⚪ low — Return Address (revenge-mail) also triggers on ability, echo, storm-trail and horde damage
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeChips.cs` (91 (called from NativeArena.cs:196))
- **Original:** src/shared/chaos.ts:73 (`kind==='direct'` only); ability damage kind 'ability' (ability-system.ts:196), echoes 'arcade-echo', horde 'minion'

**What happens.** A pilot with Return Address is hit by an enemy's Meteor Strike, Tidal Wave, ball lightning or other Q/E ability, an Ansatsuken afterimage, or a Ward horde claw. Unity marks the attacker for a 25-damage bomb; the source only does so for direct weapon hits.

**Evidence.** `if(Has(p,"revenge-mail")&&direct&&attacker!=null&&ChipReady(p,"revenge",4))`, where `direct` is the Damage parameter, which defaults to true. AbilityHit calls `Damage(target,owner,damage,dir,impulse,head,false,direct,false)` with direct=true by default (NativeAbilities.cs:143-145), and FighterEchoes/StormTrails/horde also leave direct=true.

**Verifier's correction.** The finding is accurate as stated, and its scope is somewhat wider. Unity also fires Return Address on non-direct projectile hits: extras from backfire, crossfire and helix, plus sword-beam and reflected shots (NativeProjectiles.cs:122). It also fires on reflected thrown weapons (NativeMeleeSpecials.cs:154). For horde claws the attacker resolves to the Siren boss pilot (Owner=v.Boss), so the bomb lands on the boss. Gating on `direct&&primary` would not be a correct fix on its own: Unity's fighter strikes (NativeFighters.cs:93) and the reflected path pass different primary values from what the TS kind implies. Passing an explicit damage kind, or a dedicated flag for the directHit-equivalent call sites (weapon melee/specials, direct projectiles/throws, arcade/emperor strikes), is the correct fix.

**Suggested fix.** Trigger revenge-mail only for primary weapon hits (direct && primary), or pass an explicit damage kind.

<a id="f106"></a>
### #106 ⚪ low — Once-per-round Chips can re-trigger in the same round after 4096 other procs (ledger eviction)
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeChips.cs` (17, 84, 89-90; NativeArena.cs:179; NativeLeet.cs:66)
- **Original:** src/shared/chaos.ts:41 (same eviction rule; inherited from the source)

**What happens.** A long round with a Machine Gun + Parallel Shot + several field Chips adds one ledger key per projectile impact per field Chip (hundreds per second). After 4096 keys the oldest entry, e.g. 'seatId:exit', is evicted, so Emergency Exit, Do Not Press, Round Two or CTRL+Z can fire a second time in the same round.

**Evidence.** `private bool ProcOnce(string key){if(!procLedger.Add(key))return false;procOrder.Enqueue(key);if(procOrder.Count>4096)procLedger.Remove(procOrder.Dequeue());return true;}`. The once-per-round keys share this ledger: `ProcOnce(p.Seat.id+":exit")`, `":panic"`, `":round-two"`, `"undo:"+id`. This is inherited from the source, but it contradicts the 'Once per round' card text.

**Verifier's correction.** For Emergency Exit (':exit') and Do Not Press (':panic'), the eviction is inherited from src/shared/chaos.ts:42. For Round Two (NativeArena.cs:179) and CTRL+Z (NativeLeet.cs:65, not 66), the eviction is Unity-only: the TS uses separate uncapped sets (arcade.ts:80, leet.ts:47), so for these two it is a parity divergence, limited to access-gated dev weapons. Unity also routes the vacuum, shrapnel, catch and leet keys into the capped ledger (TS: the uncapped procAttacks set and leet's own handling), so eviction comes slightly sooner than in the TS. Fix as hinted: store the per-round flags in a separate set that is never evicted and is cleared on Reset, and optionally move vacuum, shrapnel and catch to their own set as the TS does.

**Suggested fix.** Keep per-round flags (exit/panic/round-two/undo) in a separate set that is never evicted.

<a id="f107"></a>
### #107 ⚪ low — StepChipFields allocates per field per tick and fields are unbounded (GC hot spot with 8 players)
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/NativeChips.cs` (110, 114-123, 149)
- **Original:** src/shared/chaos.ts:116-122 (same algorithm; no cap for chip fields)

**What happens.** 8 pilots with fast weapons and per-projectile field Chips (black-hole 3.5 s, acid 4 s, etc.) can keep hundreds of fields alive. Each tick, every field allocates `ChipFields.ToArray()`, a `Pilots.Any(lambda)` closure for mines, `Minions.ToArray()` and `Props.ToArray()` (about 300 references) for black-hole/repulsion/fire/acid. ChipShotMotion and ChipFieldSpeed also scan all fields for every bullet and pilot, which multiplies cost and GC at 60 Hz.

**Evidence.** Code: `foreach(var f in ChipFields.ToArray())`, `trigger=mine&&...&&Pilots.Any(p=>...)`, `foreach(var m in Minions.ToArray())`, `foreach(var prop in Props.ToArray())` inside the per-field loop. Field() adds without any cap: `ChipFields.Add(f)`.

**Verifier's correction.** Hot spot: Assets/ChipArena/Runtime/NativeChips.cs lines 114-123, in StepChipFields.
- Line 114: ChipFields.ToArray() runs once per tick.
- Line 117: the closure display class is allocated for every field each tick; the Any() delegate and enumerator only for armed mines.
- Line 121: Minions.ToArray() runs for every lingering field.
- Line 122: Props.ToArray() (230 to 360 references, about 2 KB) runs for every black-hole, repulsion, fire or acid field.

Lines 110 and 149 add CPU cost only, with no allocation, matching the original. Fields are added at line 48, not 149.

Having no cap on chip fields matches the original (chaos.ts:44; the 64 cap at chaos.ts:45 applies only to ability fields), so a cap would be a gameplay divergence. Fix it by reusing scratch buffers and hoisting the mine lambda into a non-capturing loop.

**Suggested fix.** Iterate backwards over the list without ToArray, reuse buffers, and consider a per-owner/global cap like the source's 64-field ability pool.

<a id="f108"></a>
### #108 ⚪ low — Match setup defaults, ranges and options differ from the source (sets 3 vs 5, rounds max 5, skip-bot-drafts on, no random map, dedicated has no sets/rounds/teams options, result text)
- **Area:** Chips & match
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (9, 222, 227-228; NativeDedicated.cs:13; NativeMatch.cs:48, 88)
- **Original:** src/shared/config.ts:2 (wins 5, roundsPerSet 2); src/client/main.ts:88 (skipBotDrafts:false), 105-106, 136 (1-10 selects, random map / each set); src/shared/simulation.ts:81-82, 497-500 (team names in result text)

**What happens.** A new install starts 'first to 3 sets' instead of 5 and skips bot drafts by default. Rounds per set cannot exceed 5 (the source allows 10). There is no 'random arena' or 'new arena each set'. A dedicated server always runs 3 sets x 2 rounds, FFA, 6 choices, 18 s. In Teams mode the result shows the individual's name ('Pilot 2 wins set 1') instead of 'Red team wins set 1 · 2 round wins'.

**Evidence.** `public int bots=3,sets=3,rounds=2;...skipBotDrafts=true`. `prefs.rounds=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.rounds,1,5))`. NativeMatch clamps `SetsToWin` to 1..20 (source 1..10). Result text: `Result=win.name+(SetWinner!=0?..." wins set "+Set):" wins round "+Round)`. The dedicated server builds `new ConversionPreferences{name="Dedicated server",pve=...,map=...,bots=0}` with no other options parsed.

**Verifier's correction.** The NativeMatch.cs:48 point (SetsToWin clamped to 1..20 instead of 1..10) has no effect on players, because the UI slider already caps sets at 10 and the dedicated server uses 3. The relevant cap is the rounds slider at ConversionGame.cs:227 (1..5). Two additions: the result text also drops the source's round and set counts ("· 1/2 rounds in this set", "· 5 sets"), and the default arena differs (source 'moonlit', main.ts:106; Unity "foundry", ConversionGame.cs:9).

**Suggested fix.** Use the source defaults (5 sets, skip-bot-drafts off), allow 1-10 rounds, add random-map options and CLI flags for dedicated sets/rounds/teams/draft, and use team names in team-mode results.

<a id="f109"></a>
### #109 ⚪ low — Sprint is blocked during reload, block window and ability/cast windups (TS allows it) and allowed while swimming
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (138)
- **Original:** src/shared/movement.ts:13 (sprintIntent excludes attack/block/ability inputs only), 28 (prepareMotion excludes locked/stunned/guarding/swinging/swimming)

**What happens.** A gun user double-taps W and sprints during a 2.4 s reload. TS sprints at 1.35×. Unity walks. In lava channels Unity allows sprint (×1.35×0.6) where TS does not.

**Evidence.** `bool sprint=i.Sprint&&p.AbilityCast==null&&p.CastAt<=0&&...&&p.ReloadEnd==0&&Time>=p.BlockEnd&&...` with no swimming term.

**Verifier's correction.** Unity file: Assets/ChipArena/Runtime/NativeArena.cs:138, with `swimming` defined at line 116. TS sources: src/shared/movement.ts lines 13 and 28.

The pistol reload is 1.1 s; the 2.4 s in the scenario is the machinegun, and the rifle is 2.6 s. Cast and ability windups allow sprint in TS only after the attack or ability input is released; while held, `sprintIntent` blocks it there too.

Fix: drop the ReloadEnd, BlockEnd and CastAt terms. Keep `!i.Shoot` and `!aim` as the attack-input exclusions, add `!swimming`, and replace `AbilityCast==null` with an ability-input-held check (`i.AbilityEHeld` or equivalent) to match `sprintIntent`.

**Suggested fix.** Mirror sprintIntent and prepareMotion conditions exactly.

<a id="f110"></a>
### #110 ⚪ low — 4th reflection of an ability shot resolves its contact (a rocket explodes) instead of being absorbed
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeAbilities.cs` (208-210)
- **Original:** src/shared/simulation.ts:334-335 (reflections>=maxReflections: delete shot, emit blockAbsorb)

**What happens.** A rocket or ball lightning ping-pongs between two blockers three times. On the next block TS absorbs it silently. Unity falls through to AbilityContact, which detonates a rocket or ball lightning blast at the blocker and damages nearby players.

**Evidence.** `if(body!=null&&Time<body.Pilot.BlockEnd&&d.id!="tidal-wave"&&s.Reflections<3){...reflect...}` and otherwise `remove=AbilityContact(s,h);`.

**Verifier's correction.** The location and divergence are correct (NativeAbilities.cs:208-210 against simulation.ts:334-335 and ability-system.ts:226). Ball lightning does not detonate at the blocker. It passes through (AbilityContact returns false for ball-lightning when a pilot is the target), keeps zapping, and bursts later on a surface or at expiry. The orbital mark variant has the biggest effect: the strike attaches to the blocker and lands 85 damage about 1 s later, after the block window has ended. Unity's regular-projectile and melee-shot reflect paths already absorb at Reflections>=3 (NativeProjectiles.cs:110, NativeMeleeSpecials.cs:145), so only ability shots are affected. Fix: when the target is blocking (and the shot is not tidal-wave) and Reflections>=3, call BlockProc, emit the absorb event, set remove=true and skip AbilityContact.

**Suggested fix.** When blocked with Reflections>=3, BlockProc and remove the shot without contact effects.

<a id="f111"></a>
### #111 ⚪ low — Debris damage is applied outside the Fight phase (countdown/result)
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeDestruction.cs` (19-26)
- **Original:** src/shared/simulation.ts:225 (damage returns 0 unless phase==='fight')

**What happens.** After the round is decided (Result phase) a toppling crate or a collapsing bridge still falls on the surviving winner. Unity's AfterPhysics calls Damage, which has no phase check, and can kill them during the result screen. TS ignores it.

**Evidence.** FixedUpdate runs `arena.BeforePhysics();Physics.Simulate(1f/60);arena.AfterPhysics();` every tick regardless of phase. AfterPhysics: `if(capped>0)Damage(p,...)`. Damage() has no Match.Phase guard.

**Verifier's correction.** The defect spans `NativeDestruction.cs:24-26`, which has no phase gate on the debris pass, and `NativeArena.cs:174-176`, where `Damage()` has no `Match.Phase != Fight` early return like TS `simulation.ts:225`. The runner is `ConversionGame.cs:121`, which calls `BeforePhysics`/`Simulate`/`AfterPhysics` in every phase. The realistic window is the 5 s Result phase (and the post-match Match phase). Countdown is unlikely because `Reset()` rebuilds props each round. The outcome and score are not affected, only a visible, replicated death of the winner.

**Suggested fix.** Skip debris damage (or return 0 from Damage) when Match.Phase!=Fight.

<a id="f112"></a>
### #112 ⚪ low — Pilots bounce off an invisible 19 m ceiling that the original does not have
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (166)
- **Original:** src/shared/ceiling-field.ts:3 ('Projectile-only field ... no player collision')

**What happens.** A pilot launched by Rubber Soul jumps, L33T yeet (×1000 impulse) or a strong blast rises above 19 m. TS lets them fly and fall (ring-out). Unity reflects their vertical velocity at y>19 and keeps them in the arena.

**Evidence.** `if(p.Body.position.y>19&&p.Body.linearVelocity.y>0){var ceiling=p.Body.linearVelocity;ceiling.y=-Math.Abs(ceiling.y)*.7f;p.Body.linearVelocity=ceiling;}`

**Verifier's correction.** Same file and line: Assets/ChipArena/Runtime/NativeArena.cs:166, against src/shared/ceiling-field.ts:3 and the TS player motion in src/shared/movement.ts:33-47, which has no height cap. Corrected scenario: on any non-Siren map, a pilot with Rubber Soul jumps from the 14.4 m roof of a 4-storey building, or a pilot with Moon Boots + Rubber Soul + Spring Soles jumps from the ground. In TS they rise to about 19.5-21.5 m and come back down normally. In Unity they bounce off an invisible ceiling at 19 m. Horizontal ring-outs, including L33T yeet launches, still happen in Unity because x/z velocity is untouched, so the "keeps them in the arena" part of the original scenario is overstated.

**Suggested fix.** Remove, or document it as a deliberate adapter.

<a id="f113"></a>
### #113 ⚪ low — Per-frame GC churn: Snapshot(true) every LateUpdate allocates actor clones, Shot objects and event arrays; per-tick LINQ in Bot()
- **Area:** Combat
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (256-271, 249-255; caller ConversionGame.cs:106)

**What happens.** An 8-player match with sustained fire at 144 fps: each frame allocates a ViewCopy (MemberwiseClone) per pilot, Wounds.ToArray per pilot, a new Shot per bullet (up to 100), Events.ToArray (all events of the last second, often hundreds) and LINQ iterators. That is roughly MB/s of garbage. In addition, Bot() runs Where/OrderBy/FirstOrDefault per bot per tick, StepBullets allocates a closure per bullet per tick, and every dynamic prop raises OnCollisionStay each step. This risks GC spikes at 60 fps.

**Evidence.** `Actors=Pilots.Where(...).Select(p=>{...var shown=p.Pose.ViewCopy();...}).ToArray(),Shots=Bullets.Select(b=>new Shot{...}).ToArray(),Events=Events.ToArray()` and `var target=Pilots.Where(...).OrderBy(...).FirstOrDefault();`.

**Verifier's correction.** The allocation claims hold. Two corrections to the scenario:
1. The OnCollisionStay part is mainly CPU work per contact pair per step, not much garbage: Collision objects are reused by Unity's default reuseCollisionCallbacks, and null GetComponent results do not allocate in player builds.
2. Unity's default incremental GC is not disabled here, which likely softens the spikes. The impact is an unmeasured risk, not a demonstrated frame drop.

Measured garbage: about 0.2 to 4.6 MB/s depending on bullet and event count and frame rate.

**Suggested fix.** Reuse snapshot buffers and pooled Shot/Actor views, avoid LINQ in per-tick authority code, and pass a cached accept predicate.

<a id="f114"></a>
### #114 ⚪ low — Unity CI only runs on pushes to codex/unity-conversion; PR checks skip Unity entirely and nothing compiles Runtime/Editor code
- **Area:** Delivery & docs
- **Where:** `.github/workflows/unity-conversion.yml` (2-6, 17-19, 23-29 (plus scripts/ci-scope.mjs:6-7))
- **Raised as:** high

**What happens.** The owner wants to keep developing the Unity version. Suppose someone opens a PR or merges to main or claude/visual-revision with a change that breaks Assets/ChipArena/Runtime (for example a typo in NativeArena.cs). The Unity workflow does not run, because its branch filter only matches codex/unity-conversion and it has no pull_request trigger. checks.yml labels the change 'standard' and runs only the Electron `npm run verify:standard`. Required checks report nothing about Unity; they are red anyway because of the 12 Electron baseline failures. Even on the conversion branch, CI compiles only Core/*.cs (CoreTests.csproj) and Relay. The 64 Runtime files and 2 Editor files (about 630 KB of C#, the actual game) are never compiled in CI. The Unity-reference compile cited in VALIDATION has no script in the repo, so nobody else can reproduce it.

**Evidence.** unity-conversion.yml: `on: push: branches: [codex/unity-conversion]` + workflow_dispatch only; steps are check_relay.py (builds Relay + Tests/CoreTests.csproj whose `<Compile Include="../Assets/ChipArena/Core/*.cs" />`), unittest, package_source.py. ci-scope.mjs `broad` regex has no `unity/`; simulating it on ['unity/ChipArenaConversion/Assets/ChipArena/Runtime/NativeArena.cs','unity/ChipArenaConversion/Build-Windows.cmd'] prints `standard`. Measured: Core 53 files/403 KB, Runtime 64 files/614 KB, Editor 2 files/19 KB. A search for UnityEngine.CoreModule, Roslyn or csc in tools, workflows and scripts found nothing. VALIDATION.md:9 claims 'Runtime, Windows-player and Editor C# API compilation uses genuine Unity 6000.3.24f1 reference assemblies with warnings as errors' and VALIDATION.md says 'CI uses the normal .NET runner'; that is true only for Core.

**Verifier's correction.** Severity is low, not high. Corrected findings:
(1) In .github/workflows/unity-conversion.yml lines 17-29, CI on codex/unity-conversion compiles only Core/*.cs (through Tests/CoreTests.csproj) and Relay. A Runtime/Assets/ChipArena/Runtime or Editor compile error still gives a green CI run.
(2) No committed script or csproj reproduces the 'Unity 6000.3.24f1 reference assemblies, warnings as errors' Runtime/Editor compile cited in VALIDATION.md:9 and DEV_LOG, so that gate rests only on the author's say-so.

The branch-only trigger and the missing PR/ci-scope integration are documented, deliberate isolation for an unmerged branch. Wiring them in belongs to the owner's future merge, not this defect. VALIDATION.md does not claim that CI compiles Runtime. A realistic fix is to commit a csproj that takes the Unity Managed directory as a property (no DLLs committed), so the owner or a licensed runner can reproduce the compile.

**Suggested fix.** Add pull_request and push triggers for main and the shared branch with a `unity/**` path filter, and make the Unity job part of Required checks (teach ci-scope.mjs a 'unity' mode). Commit the reference-assembly compile (a csproj pointing at Unity managed DLLs, or GameCI with a license secret) so Runtime/Editor compile in CI.

<a id="f115"></a>
### #115 ⚪ low — Every Unity build presents the same compatibility identity, so mismatched tester builds join each other and desync
- **Area:** Delivery & docs
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (73 (also Core/NativeTransport.cs:16, Runtime/NativeNetworkFrame.cs:32, Editor/PrototypeBuild.cs:43,104,113))
- **Raised as:** medium

**What happens.** The room guard compares only `content.sourceCommit` (the TypeScript baseline 05950ea, which is the same in every Unity build) and `Protocol=3`. Builds go out by hand as ZIPs, so testers will run different Unity commits. A host on a newer build with changed authority or replication code admits a guest on last week's build, as long as nobody remembered to bump Protocol, and they desync silently. Players cannot report which build they run: bundleVersion is a stale hard-coded string, nothing in the player embeds the Unity commit, and the shipped READ-ME text contradicts the 'code-complete' status. Builds also use BuildOptions.Development (watermark, an error console that pops up, slower player), so testers' performance impressions are skewed.

**Evidence.** NativeNetwork.cs:73 `if(message.type!="hello"||message.source!=content.sourceCommit||message.room!=roomCode){..."Room code or conversion version does not match."`; NativeTransport.cs:16 `public const byte Protocol=3;`; PrototypeBuild.cs:43 `PlayerSettings.bundleVersion="0.4.1-native-melee-specials";` (ProjectSettings has `0.1.0-native-foundation`); :104 `options=BuildOptions.Development`; :113 READ-ME 'Full conversion remains in active completion.' versus CONVERSION-STATUS 'code-complete conversion candidate'.

**Verifier's correction.** Adjust the scenario. Mismatched Unity builds that share Protocol=3 do join each other, because the gates check only the protocol constant and the TS sourceCommit. History shows the protocol was not bumped across schema changes from 000af4d to a787a77. Because guests are host-authoritative replicas, the effect is either silently missing newer replicated features or presentation on the older side, or a loud Valid() rejection and disconnect. Rules and outcomes do not diverge. The join gate itself matches the original's static protocol strings. What the conversion lacks is any visible build identity: the original's GAME_BUILD update indicator was replaced by manual ZIPs (documented), bundleVersion has been stale since e268aa8 and is shown nowhere, and the READ-ME text at PrototypeBuild.cs:113 contradicts CONVERSION-STATUS.md:5. BuildOptions.Development at line 104 is a tester-experience concern, not a defect. Severity should be low, not medium.

**Suggested fix.** Generate a build identity at build time (git commit and a hash of Assets/ChipArena plus Conversion/Resources), show it in the menu and READ-ME, and require an exact match in hello, the relay `source` and frames. Set bundleVersion from it. Offer a non-Development tester build.

<a id="f116"></a>
### #116 ⚪ low — import_mocap.py emits the wrong namespace; following the documented regeneration step breaks the build
- **Area:** Delivery & docs
- **Where:** `tools/import_mocap.py` (17)
- **Raised as:** medium

**What happens.** Following ANIMATIONS.md, someone runs `python tools/import_mocap.py /path/to/Chip-Arena` to regenerate CapturedClips.Generated.cs, for example after the owner re-exports melee clips. The generator writes `namespace ChipArena.UnityPrototype`, but every other file and the committed output use ChipArena.UnityConversion. Core, the Unity project and CI stop compiling with CS0246.

**Evidence.** import_mocap.py:17 `'namespace ChipArena.UnityPrototype {'`; committed file line 3 `namespace ChipArena.UnityConversion {`; 52 other Core/Runtime files use ChipArena.UnityConversion. Probe (scratch copy): ran the importer against the pinned source; the only diff was the namespace line; then compiling Core with dotnet 8 gave `CapturedClips.Generated.cs(5,24): error CS0246: The type or namespace name 'CapturedClip' could not be found`. ANIMATIONS.md:80: 'Run `python tools/import_mocap.py /path/to/Chip-Arena` to regenerate from the pinned source files.'

**Verifier's correction.** File/line are correct (tools/import_mocap.py:17). Scenario correction: re-exported or changed clip sources never reach the namespace bug, because the pinned-blob check (lines 13, 24-25) raises before writing. The build break happens only when someone re-runs the documented reproduction step against the exact pinned source 05950ea. The regenerated file then differs from the committed one only in the namespace line, and Core, Runtime and Unity compilation fail with CS0246. The shipped code is unaffected. Fix: emit `namespace ChipArena.UnityConversion {`, and optionally add a check that the importer reproduces the committed file byte for byte.

**Suggested fix.** Change line 17 to `namespace ChipArena.UnityConversion {` and add a CI check that the importer reproduces the committed file byte-for-byte.

<a id="f117"></a>
### #117 ⚪ low — The data-export pipeline cannot be reproduced as documented (wrong usage, hidden ordering, CWD-relative output, unscripted lock files, byte-exact locks)
- **Area:** Delivery & docs
- **Where:** `tools/export_models.mjs` (2, 8 (also tools/export_cinematics.mjs:37, tools/export_wound_fixtures.mjs:10, tools/export_motion.mjs:26, tools/asset-lock.json))
- **Raised as:** medium

**What happens.** Someone regenerating the Unity data, for example to pick up new Electron content, follows the usage comment. (1) `NODE_PATH=<three> node tools/export_models.mjs <src> <tmp>` fails with ERR_MODULE_NOT_FOUND, because ESM ignores NODE_PATH. (2) With node_modules linked, export_models alone writes a 102-model SourceModels.json. The committed 133-model file only comes from running export_cinematics.mjs, which re-imports export_models, and package_source.py then fails the asset lock. (3) export_wound_fixtures writes `Tests/wound-carve-parity.json` relative to the current directory, so it lands in the wrong place unless run from the project root. (4) export_models silently skips inventory files that are missing, so models-provenance.json depends on which checkout is used: my run added 10 src/relay and src/server receipts. (5) No tool writes asset-lock.json, source-lock.json, conversion-inventory.json, presentation-provenance.json or ranged-provenance.json, or copies the 83 audio files. (6) The asset lock pins exact git blobs, but on Node 22.22 WoundParts.json, SceneMotion.json and wound-carve-parity.json differ from the committed files in the last floating-point digit (one ULP). The asset-lock check then fails although the content is the same. The exporter Node version is not documented (the repo pins 24.19.0).

**Evidence.** All exporters were re-run in a scratch copy against 05950ea. GameContent, source-parity, presentation, melee, specials, pve, native-systems, collapse, extra-presentation, SourceMotion and SourceModels (via cinematics) were byte-identical. WoundParts and SceneMotion differ by at most 1.1e-16 (39,696 values); wound-carve sums differ by 2.3e-13. After export_models alone, `package_source.py --check` fails with `AssertionError: Asset differs from export/source receipt: Assets/Conversion/Resources/SourceModels.json`. NODE_PATH run: `Cannot find package 'three' imported from .../stage2/src/client/character-rig.js`. export_models.mjs:8 `if(!fs.existsSync(input))continue;`. export_wound_fixtures.mjs:10 `fs.writeFileSync('Tests/wound-carve-parity.json',...)`.

**Verifier's correction.** Confirmed defects: (1) the usage comment in tools/export_models.mjs:2 is wrong (NODE_PATH does not work for ESM; node_modules must be linked into the stage); (2) the run order is undocumented (export_cinematics.mjs, which re-runs export_models, must run last so SourceModels.json gets 133 models; export_models alone breaks the asset lock); (4) export_models.mjs:8 skips missing inventory files silently, and the committed models-provenance.json was made from a checkout that lacked 9 src/relay and src/server files; (5, partly) no tool regenerates tools/asset-lock.json. Not confirmed or overstated: (3) running export_wound_fixtures.mjs:10 from the wrong directory throws ENOENT instead of writing to the wrong place, and that file is not lock-checked; (6) the one-ULP drift comes from running on off-pin Node 22's Math.pow. The repository pins Node 24.19.0 in .node-version, and the committed values match a correctly rounded pow. Severity should be low: build-time tooling and docs only, no runtime effect, and the asset-lock check prevents silent mis-packaging.

**Suggested fix.** Commit one `tools/export_all.mjs` (or a .sh) that creates the stage with node_modules, runs the exporters in the required order, writes the lock and provenance files, copies audio, and checks the Node version. Make every output path root-relative and fail on missing inventory files. Round all exported floats (as export_motion does), or lock semantic hashes instead of raw blobs.

<a id="f118"></a>
### #118 ⚪ low — Several current docs are stale or contradict the code and the builder's real output
- **Area:** Delivery & docs
- **Where:** `VALIDATION.md` (84-85 (also ANIMATIONS.md:43,95, README.md:10, tools/conversion-inventory.json status, Editor/PrototypeBuild.cs:113, docs/PROJECT_STATE.md:127))

**What happens.** The owner and testers read conflicting status. VALIDATION's 'Remaining acceptance' list sits at the same heading level as the current section and says multiplayer is not implemented and points to pending CONVERSION-STATUS rows that no longer exist. ANIMATIONS says only Sword is playable and no native networking exists. The inventory says 'full conversion incomplete'. README says to wait for UNITY_TEST_BUILD_READY, but the console prints only 'Ready: …'; the marker is in Builds/build.log. The READ-ME shipped inside the EXE ZIP says the conversion 'remains in active completion'.

**Evidence.** VALIDATION.md:84 'New multiplayer is not implemented yet; old TCP tests are not a substitute.' :85 'all pending rows in CONVERSION-STATUS.md' (CONVERSION-STATUS has none). ANIMATIONS.md:43 'Of these four melee sets, only Sword rules are playable.' ANIMATIONS.md:95 'no native network implementation is added'. conversion-inventory.json `status: 'foundation milestone; full conversion incomplete'`. Build-Windows.cmd prints `Ready: "%~dp0Builds\...zip"`; only `findstr` reads the marker from the log. PROJECT_STATE.md:127 '31 scenes / 124 models' (the code checks 133).

**Verifier's correction.** The defects that are current and unmitigated:
- README.md:10 and CONVERSION-STATUS.md:38 claim a console marker that Build-Windows.cmd never prints (it prints `Ready:`/`Play:`).
- The READ-ME.txt shipped in the ZIP (PrototypeBuild.cs:113-114) says "Full conversion remains in active completion" and points to CONVERSION-STATUS.md/VALIDATION.md, which are not in the ZIP.
- conversion-inventory.json:5 status is stale.
- VALIDATION.md:84-85 contradicts CONVERSION-STATUS and refers to pending rows that do not exist. VALIDATION.md:7 and the "Historical checkpoints" heading do partly scope this list as historical.

Mostly mitigated, so weaker evidence:
- ANIMATIONS.md:43 sits in a section labelled historical (line 11).
- ANIMATIONS.md:95 describes only that dated change.
- PROJECT_STATE.md:127 is a checkpoint entry superseded by the "current" section at line 129.

**Suggested fix.** Move the historical sections under a clearly labelled appendix or delete the superseded lists. Update the inventory status, README step 4 and the READ-ME text.

<a id="f119"></a>
### #119 ⚪ low — package_source.py enforces asset locks, GUID and meta checks with assert, so they vanish under python -O / PYTHONOPTIMIZE
- **Area:** Delivery & docs
- **Where:** `tools/package_source.py` (50, 53, 55, 58, 61-66)

**What happens.** If anyone runs packaging with `python -O`, or in an environment with PYTHONOPTIMIZE set (some Windows Python installs and CI images), every receipt, large-file, meta, GUID and icon check is skipped. A tampered or oversized asset is zipped and the script still prints 'Project structure passed'.

**Evidence.** `assert len(data) == receipt['bytes'] and digest == receipt['gitBlob'], f'Asset differs...'`; `assert path.stat().st_size < 1024 * 1024 or ...`; `assert match and match[1] not in guids`. Only validate_dependencies raises ValueError.

**Verifier's correction.** The defect is real in tools/package_source.py at lines 50, 53, 55, 58, 61, 62, 64 and 66. It only shows up when the script is run outside its documented path, with `python -O` or PYTHONOPTIMIZE set. The CI gate (`python3 .../package_source.py` on ubuntu-24.04) is not affected, and Build-Windows.cmd does not use the script. The claim that common Windows Python installs set PYTHONOPTIMIZE is unsubstantiated. The impact is limited to the integrity check of the source ZIP, with no effect on game runtime. Severity stays low.

**Suggested fix.** Replace the asserts with explicit `if not ...: raise ValueError(...)`.

<a id="f120"></a>
### #120 ⚪ low — Third-party copyrighted audio is carried into the Unity distributable with no credits or notices
- **Area:** Delivery & docs
- **Where:** `tools/asset-lock.json` (77-86, 449-452)
- **Original:** docs/ASSET-CREDITS.md

**What happens.** Owner-supplied third-party recordings now ship in a second, separately distributed Windows ZIP: the Tekken 5 'Moonlite Wilderness' transcode and the AkBank battle and voice samples mixed into electric-hook.wav. No ASSET-CREDITS or notice file goes into the Unity source or player package. This was already the case for the Electron game, but the Unity ZIPs will be handed out by hand, so wider distribution becomes more likely.

**Evidence.** asset-lock.json includes `Assets/Conversion/Resources/Audio/music/moonlit-wilderness.mp3` (source public/music/moonlit-wilderness.mp3) and `.../audio/electric-hook.wav`. Source docs/ASSET-CREDITS.md: 'moonlit-wilderness.mp3 is a full-length transcode of that supplied recording ... identifies it as Tekken 5's Moonlite Wilderness'; Electric Hook layers 'AkBankBattle#115' and 'AkBankVoiceJin#25'. PrototypeBuild.cs writes only READ-ME.txt into the player folder.

**Verifier's correction.** Main files: Assets/ChipArena/Editor/PrototypeBuild.cs:108-118 (player folder gets only READ-ME.txt) and tools/package_source.py FILES/TOP (source ZIP has no credits file). tools/asset-lock.json only shows which assets ship. Correction: the original Electron package did carry ASSET-CREDITS, inside chip-arena-source.zip (scripts/package-windows.mjs:17 with scripts/package-source.mjs:4 including 'docs'). So this is a regression from the original packaging and from the docs/RELEASES.md:88 rule, not a gap that already existed. Fix: copy the source docs/ASSET-CREDITS.md into the Unity project, add it to package_source.py FILES, and have PrototypeBuild write it into Builds/Windows next to READ-ME.txt.

**Suggested fix.** Before any distribution beyond private testers, confirm rights or replace these tracks. Ship the ASSET-CREDITS (and any license texts) in the player folder.

<a id="f121"></a>
### #121 ⚪ low — "Bots use my weapon" with a DEV weapon creates bots that cannot attack, and each Siren bot's entrance throws every frame
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (56 (also NativeFighters.cs:23; Core/NativeIntroRules.cs:9-11; NativeIntros.cs:25; NativeArena.cs:249-255))
- **Original:** src/shared/simulation.ts:128-134; src/shared/intros.ts (effectiveIntro); src/shared/bot-abilities.ts:17-31

**What happens.** A verified DEV picks Siren, Emperor or Ansatsuken and ticks "Bots use my weapon". The bots get that spec with Guest access, so `Dev(p,spec)` is false. They have no kit moves, and TryMelee refuses non-standard specs, so they walk up and never attack. Up to 7 Siren bots can spawn; the source allows one Siren per room. At match start, each Siren bot's intro style resolves to "weapon-siren". SceneMotion has no "stage-weapon-siren", so RenderIntro throws InvalidOperationException on every LateUpdate for that bot's 4.8 s intro, which shows nothing and spams the log.

**Evidence.** Bot seats are created without `access` (NativeSeat default `NativeAccess.Guest`). NativeFighters.cs:23 `Dev(p,spec)=>...p.Seat.access.Allows("weapon:"+spec,...)`. NativeIntroRules.Style: siren without access and not AuthorityBoss falls through to `return "weapon-"+seat.spec;`. NativeIntros.cs:25 `if(!SourceStage(style,age,camera))throw new System.InvalidOperationException("Missing source entrance stage: "+style);`. Decoded SceneMotion.json keys: stage-arc-salute ... stage-weapon-water, with no stage-weapon-siren. Bot() (NativeArena.cs:249-255) never sets SirenAbility, Hadoken or Uppercut. Source: simulation.ts:128-134 `devBots=...actor.developer`, `unlocks:WEAPONS[spec].devOnly?['weapon:'+spec]:[]`, `hasSiren` limit; intros.ts effectiveIntro falls back to `weaponIntroFor(spec)||'neon-drop'`; bot-abilities.ts:17-31 drives Siren, Emperor and Ansatsuken bot moves.

**Suggested fix.** Either exclude DEV specs from mirrored bot seats, or grant the bot a scoped weapon claim when the host is a verified DEV, with at most one Siren. Make NativeIntroRules fall back to neon-drop when no weapon stage exists. Add kit inputs to Bot().

<a id="f122"></a>
### #122 ⚪ low — Floating combat numbers show only the local player's outgoing hits; damage taken, other players' hits and all heals never appear
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/NativeCombatHud.cs` (27)
- **Original:** src/client/combat-numbers.ts:10-20

**What happens.** Damage you take, damage between other pilots, and heals (life steal, regeneration, reload heal, med station, block heal) show no numbers. Each DoT tick shows a separate number instead of merging. The CONVERSION-STATUS claim of "damage numbers" holds only for the local player's own hits; no Heal event kind exists.

**Evidence.** `foreach(var e in arena.Events){...if(age<0||age>.8f||e.Source!=arena.LocalId||(e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)||e.Amount<=0)continue;...}`. EventKind has no Heal (ArenaModel.cs:85). Source combat-numbers.ts:10-20 consumes every 'hit' and 'heal' event for every target, merges same-target numbers within .16 s, shows heals as '+N', and places your own numbers under the crosshair in first person.

**Verifier's correction.** The core finding holds. Unity shows numbers only for hits the local player deals (on pilots and minions), and never for damage taken, hits between other pilots, or any heal, because Heal() at NativeChips.cs:19 emits no event and EventKind has no Heal. The DoT sub-claim should be narrowed. A single DoT does not merge in the original either (its 0.25 s tick is outside the .16 s window). The real merge gap is for events landing on one target within .16 s, such as pellets, fire plus poison ticking together, or regen heals. Unity draws these as separate, overlapping labels. Smaller label differences: TS rounds and prefixes '−' or '+', while Unity uses Math.Ceiling with no sign. TS shows numbers for 1.15 s, Unity for 0.8 s.

**Suggested fix.** Emit a Heal event from Heal(). Show numbers for all on-screen hit and heal events, merged within 0.16 s, as CombatNumberTrack does.

<a id="f123"></a>
### #123 ⚪ low — Mouse look is 66% faster horizontally and 45% faster vertically than the original at the same sensitivity, with a wider pitch limit
- **Area:** Gaps found later
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (78-79 (also NativeReplay.cs:37; ProjectSettings/InputManager.asset Mouse X/Y sensitivity 0.1))
- **Original:** src/client/main.ts:338

**What happens.** At the default sensitivity 1, Unity turns 0.2° per mouse count horizontally and 0.15° vertically; the original turns 0.120° and 0.103°. Muscle memory from the Electron game overshoots, and the vertical-to-horizontal ratio changes from 0.86 to 0.75. Pitch reaches ±70° (sanitized at ±75°) instead of ±1.1 rad (63°).

**Evidence.** `yaw+=Input.GetAxisRaw("Mouse X")*2*prefs.sensitivity*...;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*prefs.sensitivity*...,-70,70)`. InputManager.asset `m_Name: Mouse X ... sensitivity: 0.1` (same for Mouse Y), so 0.1×2 = 0.2°/count and 0.1×1.5 = 0.15°/count. Source main.ts:338: `yaw-=e.movementX*.0021*pref.sensitivity*aim().sens;pitch=Math.max(-1.1,Math.min(1.1,pitch-e.movementY*.0018*pref.sensitivity*aim().sens))` (0.0021 rad = 0.120°, 0.0018 rad = 0.103°).

**Verifier's correction.** The 66%/45% speed-up assumes one Unity raw-input count equals one Chromium movementX pixel (100% DPI, no Windows pointer acceleration). Chromium's movementX is accelerated and DIP-scaled, so the exact factor depends on the setup. Two parts hold regardless of units: the vertical-to-horizontal ratio drops from 0.857 to 0.75, and the client pitch clamp is ±70° instead of ±63.03°. The authority clamp being ±75° rather than the source's ±77.3° (1.35 rad) is a separate, minor difference. The same constants also appear in ArenaGame.cs:140, the legacy path.

**Suggested fix.** Use 0.1203/0.1 and 0.1031/0.1 multipliers (about 1.203 and 1.031), or read the raw delta and apply the source radian factors. Clamp pitch to ±63°.

<a id="f124"></a>
### #124 ⚪ low — StartMatch failure leaves a non-started arena in place, wedging a dedicated server (e.g. --ca-min-players 1 with no bots)
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (58-59 (with NativeDedicated.cs:14, 22; NativeNetwork.cs:78))

**What happens.** `--ca-server --ca-min-players 1` (accepted by the argument check) with one PvP player ready. StartMatch assigns `arena=new NativeArena(...)`, then match.Start() fails ("A match needs at least two opposing sides") and returns with the arena still set and Phase=Lobby. DedicatedTick requires `arena==null||match.Phase==Match` to try again, and HostMessage rejects newcomers with "The room is full or the match has started" while arena!=null. The server stays stuck until every player leaves and 90 s pass.

**Evidence.** `arena=new NativeArena(content,match,prefs.map){...};if(!match.Start()){error="A match needs at least two opposing sides.";return;}`. NativeMatch.Start: `if(Seats.Count<2||...)return false;`. DedicatedTick: `...&&(arena==null||match.Phase==NativePhase.Match))StartMatch();`.

**Verifier's correction.** The scenario holds as written, with one more detail: StartMatch clears `peer.Ready` before the failed Start (ConversionGame.cs:55), and the `ready` message is ignored while `arena!=null` (NativeNetwork.cs:85). So even the first condition of DedicatedTick can never become true again, not just the Phase check. A related, hard-to-reach problem exists on the non-dedicated host path: on the same failure, `error` is never cleared (only the initializer at ConversionGame.cs:15 sets it). OnGUI's early return then replaces the whole UI with the error box, and the host must quit. Reaching it in PvP needs 8 humans all on one team in teams mode, because the bot slider minimum is 1.

**Suggested fix.** Validate before creating the arena, or dispose and null the arena when Start() fails. Reject min-players 1 in PvP without bots.

<a id="f125"></a>
### #125 ⚪ low — In a 1v1, a disconnect makes the remaining player win every round in ~48 s while the 90-second seat is still reserved
- **Area:** Networking
- **Where:** `Assets/ChipArena/Core/NativeMatch.cs` (53, 81-82)
- **Original:** src/shared/simulation.ts:515-517

**What happens.** In a 2-player PvP room the guest's connection closes. Resolve counts only `alive&&connected`, so the host wins the current round immediately and then every following round (3 s countdown plus 5 s result each). The disconnected loser's draft is skipped and the next set starts at once. With the default 2 rounds × 3 sets the match ends in about 48 s, while the player still has ~40 s of the advertised 90-second reconnect seat. The original gave a 12 s grace (body kept, hp>0), then a forfeit with an explicit result ("X wins · other players disconnected").

**Evidence.** `var alive=Seats.Where(p=>p.alive&&p.connected).ToArray();if(alive.Select(p=>Teams?p.team:p.id).Distinct().Count()>1)return;Winner=...`. BeginRound: `foreach(var p in Seats)p.alive=p.connected;`. The draft queue uses `Seats.Where(p=>p.connected&&...)`, so it is empty and NextSet runs.

**Verifier's correction.** With the default prefs (sets=3, rounds=2 in ConversionPreferences, ConversionGame.cs:9), the match auto-completes about 45 s after the disconnect (measured +45.28 s), not about 48 s. That leaves about 45 s of the 90 s resume window, not about 40 s. Each extra round still needed adds about 8 s, so with higher settings the time is longer. Unity files: Assets/ChipArena/Core/NativeMatch.cs lines 53, 67, 81-82, and Assets/ChipArena/Runtime/NativeNetwork.cs:54. Original: src/shared/simulation.ts:197-199, 491-492 and 515-517.

**Suggested fix.** Hold round resolution (or pause) while a seat's reconnect lease is active, or end the match with an explicit disconnect result as the original does.

<a id="f126"></a>
### #126 ⚪ low — Resuming a lobby seat after the match started without it puts the guest in a disconnect loop
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (74-75, 58, 113)

**What happens.** A guest is seated in the lobby, drops, and the host starts the match without them (StartMatch includes only open connections). The guest rejoins within 90 s with its resume token. The resume lookup has no arena check, so the seat is restored and frames are sent to it, but the seat is not in f.seats. The guest throws ArgumentException ("Invalid or incompatible host state.") and disconnects. Every retry repeats this.

**Evidence.** `NativePeer previous=!string.IsNullOrEmpty(message.resume)?peers.Find(p=>...p.Connection==null&&(DateTime.UtcNow-p.Disconnected).TotalSeconds<=90):null;` then `peer.Seat=previous.Seat;...`. Frames go to `if(peer.Seat!=null&&peer.Connection?.Closed==false)`. Client: `if(f==null||!f.Valid(content)||!f.seats.Any(s=>s.id==networkId))throw new ArgumentException();`.

**Verifier's correction.** Affected lines: NativeNetwork.cs:74-75 (resume accepted with no `arena`/`match.Seats` check), :64 (frames sent to a non-participant seat), :115 (client throws), :68-69 (disconnect). The status the guest finally sees is "Disconnected. Rejoin this host within 90 seconds to reclaim your seat." because line 69 overwrites line 68's message. In dedicated mode the trigger is more likely, since NativeDedicated.cs:22 auto-starts when all *connected* peers are ready. The loop continues for the whole match because line 63 never purges the stale peer while `arena!=null`, and each failed attempt resets `Disconnected`.

**Suggested fix.** On resume while arena!=null, accept only seats present in match.Seats; otherwise answer with an error or spectator state, and do not send frames to non-participants.

<a id="f127"></a>
### #127 ⚪ low — Fast rejoin before the host's 12 s timeout fails or creates a duplicate seat
- **Area:** Networking
- **Where:** `Assets/ChipArena/Runtime/NativeNetwork.cs` (74, 78-79)

**What happens.** The guest's side errors first (e.g. a network change surfaces as a write error) and the user clicks Join right away while the host still holds the old open connection. The resume needs `p.Connection==null`, so in the lobby the hello gets a brand-new seat and resume token. The old seat then lingers as connected for up to 12 s and disconnected for 90 s, using capacity and showing twice. During a match the guest gets "The room is full or the match has started" and must wait and retry.

**Evidence.** `peers.Find(p=>p!=peer&&p.Resume==message.resume&&p.Connection==null&&...)`, otherwise `int id=dedicated?1:2;while(peers.Any(p=>p.Seat?.id==id))id++;peer.Seat=SanitizedSeat(...);peer.Resume=RandomToken(32);`.

**Verifier's correction.** Scope: the bug triggers only on a one-sided transport break, where the guest's socket fails but the host gets no FIN or RST (a network or IP change, a NAT or middlebox reset, or a guest-side reset on a relay path), followed by a rejoin within the host's 12 s silence timeout. It does not trigger when the guest disposes its own connection (Leave, an invalid-state error, queue overflow), when both sides go silent together, or when the host stalls. In those cases the host marks the old connection null before handling the new hello. One effect beyond the finding: during the 12 s window the ghost seat counts as a live peer in ConversionGame.cs:50/54. It blocks StartMatch if it was not ready, and joins the match as an idle pilot if it was. This is not a parity divergence: the original also required the old seat to be disconnected (pve/system.ts:142-145).

**Suggested fix.** On a matching resume token whose old connection is still open, dispose the old connection and transfer the seat.

<a id="f128"></a>
### #128 ⚪ low — Relay endpoints let InvalidDataException/ObjectDisposedException escape, logging an unhandled error with stack trace per request
- **Area:** Networking
- **Where:** `Relay/Program.cs` (19-20, 27-28, 39)

**What happens.** A host sends any data frame on its control socket, or a guest exceeds the 128 KB/s budget or sends a text frame. Copy or the host loop throw InvalidDataException, which is not in the `when(e is WebSocketException or OperationCanceledException or IOException)` filters (InvalidDataException is not an IOException). The exception escapes to Kestrel, which logs "An unhandled exception was thrown by the application" at Error level. ObjectDisposedException from the accept socket being disposed while Copy runs does the same. Cleanup still runs in finally, but any client can flood the error log (up to the rate limit).

**Evidence.** Probe log from a locally built copy: `fail: Microsoft.AspNetCore.Server.Kestrel[13] ... An unhandled exception was thrown by the application. System.IO.InvalidDataException: Host control only accepts close frames` after sending one text frame on /native/v3/host.

**Verifier's correction.** The InvalidDataException part holds as stated. Locations: Relay/Program.cs:19 (host control), the filter at line 20, line 39 (Copy), and the filters at lines 27 (WhenAll) and 28. The ObjectDisposedException part is not supported: ManagedWebSocket turns use-after-dispose into a WebSocketException (InvalidState), and in-flight stream errors into WebSocketException or OperationCanceledException, both of which are already filtered. The fix only needs InvalidDataException added to the filters at lines 20, 27 and 28, ideally with a CloseOutputAsync(PolicyViolation) before the sockets are disposed.

**Suggested fix.** Catch InvalidDataException and ObjectDisposedException in the endpoint filters and close with a policy-violation status.

<a id="f129"></a>
### #129 ⚪ low — NativeRelayHost.Listen does not catch InvalidDataException, so a malformed control message kills the accept loop while the host still shows as hosting
- **Area:** Networking
- **Where:** `Assets/ChipArena/Core/NativeRelay.cs` (35, 37)

**What happens.** The relay sends a control message longer than 512 bytes, a JOIN with a ticket that is not 64 characters, or an unexpected field count (e.g. a relay version change). Control()/Listen throw InvalidDataException, which the `when(IOException||WebSocketException||OperationCanceledException||ObjectDisposedException||ArgumentException)` filter does not match. The unobserved Listen task faults, Dispose is never called and Closed stays false. RelayTick keeps reporting "Hosting internet room" while no one can join.

**Evidence.** `if(fields.Length!=2||fields[0]!="JOIN"||fields[1].Length!=64)throw new InvalidDataException("Invalid relay invitation")` and `throw new InvalidDataException("Control message too long")` inside `try{...}catch(Exception e)when(e is IOException||...||e is ArgumentException){...Dispose();}`. `loop=Listen()` is never observed.

**Verifier's correction.** The file and lines are right (Core/NativeRelay.cs:35 and :37; the UI symptom is in Runtime/NativeRelayUI.cs:34). The bundled Relay/Program.cs cannot produce the malformed messages; only a relay that doesn't follow the format can. One addition: because the control socket is never disposed, the relay also keeps the room listed and Ready, so guests see the room but their joins time out after 10 s.

**Suggested fix.** Add InvalidDataException to the filter (or catch all exceptions and Dispose with Error set).

<a id="f130"></a>
### #130 ⚪ low — Private room codes are 8 hex characters (32 bits), weaker than the original 40-bit codes, and are the only join credential
- **Area:** Networking
- **Where:** `Relay/Program.cs` (17, 24 (also Runtime/NativeNetwork.cs:27))
- **Original:** src/shared/relay-protocol.ts:5-7

**What happens.** Joining a private relay room needs only the room code plus the public source hash. Token(4) gives 32 bits of hex. The LAN host uses RandomToken(4) too and allows repeated wrong-code hellos on one connection. The original used 8 characters from a 32-letter alphabet (40 bits). Practical brute force still needs many addresses because of the rate limit, but once the rate limiter is fixed to see real client IPs, distributed guessing against hundreds of active private rooms becomes feasible.

**Evidence.** `Code=Token(4).ToUpperInvariant()` with `static string Token(int bytes)=>Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes))...`. Original: `ROOM_ALPHABET='ABCDEFGHJKLMNPQRSTUVWXYZ23456789'` and `newRoomCode()` producing 8 characters.

**Verifier's correction.** Only the entropy loss (40 bits down to 32) differs from the original. Using the code as the only join credential matches the original party relay. The Unity relay's 60/min/IP limit is stricter than the original, which had no join rate limit. On LAN, the open-after-wrong-code behaviour is bounded by the 5-second unseated-peer timeout, the 180 messages/s cap and the 15-connection cap.

**Suggested fix.** Use at least 40-64-bit codes (the original alphabet) and close the connection after a wrong room code in hello.

<a id="f131"></a>
### #131 ⚪ low — Wave clear (PR #57), victory and defeat make adds vanish without deaths or corpses; victory and defeat leave the horde standing
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (136 (wave-clear), 97 (victory/defeat))
- **Original:** src/shared/pve/horde.ts:509-517 (killAll -> kill(a,null,'burn',true): corpse + kill event); director.ts:88, 196, 217

**What happens.** A wave clears with 2 adds left (or is forced at 150 s with many left). In the original, every active or exposed add dies through kill(), with an uncapped corpse and a kill event, so players see the leftovers drop. Unity calls RemoveMinion while Hp>0, RecordMinionDeath returns early, and the adds simply pop out of existence. On boss death or defeat the original also kills the horde ('boss-dead'/'defeat'). Unity leaves every add frozen in place on the result screen.

**Evidence.** NativePve.cs:136 `foreach(var m in Minions.Where(m=>m.Pve!=null).ToArray())RemoveMinion(m,false);`. NativeSummons.cs:35 calls RecordMinionDeath before `m.Hp=0`, and NativeDeaths.cs:30 `if(m.Hp>0||m.Kind=="slender")return;`. The victory/defeat path at NativePve.cs:97 only sets Stage/Outcome.

**Verifier's correction.** Line references are accurate: NativePve.cs:136 (wave clear), :141 and :142 (where victory and defeat start), :97 (end of victory or defeat, no cleanup). One addition to the scenario: on the result screen the leftover adds are not only standing. Physics resumes (ConversionGame.cs:121), and on zero-friction contacts with velocities that were never cleared (only pilot velocities are zeroed, NativeArena.cs:96), any add that was moving keeps sliding with a walk or run animation. Fix: give each removed add with an exposed hitbox a 'burn' death, i.e. set Hp=0 before RecordMinionDeath, or record the death directly so the 24-entry MinionDeaths cap does not drop the batch. Also emit a Death event with amount 0, and run the same cleanup when the boss dies (victory) and when defeat finishes.

**Suggested fix.** Set Hp to 0 (or record a burn death) before removal so corpses and death events are produced. Run the same cleanup on victory and defeat.

<a id="f132"></a>
### #132 ⚪ low — Solo self-revive fires while the body is still in a vent shaft (the original waits for the splat)
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (127)
- **Original:** src/shared/pve/economy.ts:203-207 (`!inVentShaft(p.death,t)`)

**What happens.** A solo player falls into an open vent. PveDied sets SelfAt=Time+5, and at 5 s Unity revives them on a pad while the HUD still says 'FALLING THROUGH THE VENT'. In the original the timer waits until the body lands (10 s), then revives at the splat spot. DEV_LOG claims 'pit/fall/revive timing' was ported.

**Evidence.** `if(row.SelfAt>=0&&Time>=row.SelfAt&&row.Pilot.Hp<=0&&row.Pilot.Seat.connected&&row.SelfRevives>0){row.SelfRevives--;RevivePve(row.Pilot);}`. This has no InShaft check, while the F-revive path (line 69) does use `!InShaft(q)`.

**Verifier's correction.** The failure scenario holds as stated. To match the original, the fix needs two parts. First, gate the self-revive with `&&!InShaft(row.Pilot)`; the original keeps selfReviveAt set until landing, and Unity's defeat check (NativePve.cs:142, `SelfAt>=0&&Hp<=0`) already keeps the run alive while the timer is pending. Second, revive at p.Death.position (the splat spot) instead of a pad. More broadly, Unity's RevivePve always uses a pad, while the original's F-revive and self-revive use death.pos. That position difference also affects ordinary non-vent revives and is not documented.

**Suggested fix.** Add `&&!InShaft(row.Pilot)` to the self-revive condition and revive at the death or splat position.

<a id="f133"></a>
### #133 ⚪ low — Opening ends early only if every human actually installs the start Chip; 'Choose later' should count as resolved
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (131)
- **Original:** src/shared/pve/director.ts:284-288 (allResolved: not choosing and no unopened start pick)

**What happens.** A human opens the start pick and clicks 'Choose later (C)'. The original treats that as resolved, so the opening ends 3 s later (minimum 4 s). Unity requires Picks.Count==0, so the opening always runs the full 10 s unless the Chip is installed.

**Evidence.** `...All(r=>r.Picks.Count==0))v.ResolvedAt=Time;`. TS: `every(p=>!h.economy.choosing(p)&&!h.economy.partyView(p.id).picks.some(k=>k.source==='start'&&!k.offers))`.

**Verifier's correction.** The proposed fix is right. During the opening, treat a connected human as resolved when `row.Open==null` and every pick in `row.Picks` has `Offers!=null`. Unity picks have no Source field, but the only pick that exists during the opening is the start pick.

**Suggested fix.** Treat a human as resolved when no pick is open and the start pick has had its offers rolled.

<a id="f134"></a>
### #134 ⚪ low — Developer sandbox 'rarity' sort is alphabetical by rarity id, not by rarity tier
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeSandbox.cs` (53)
- **Original:** src/shared/sandbox-tools.ts:19-30 (RARITY_ORDER common..mythic, mishima, ansatsuken, l44t, siren)

**What happens.** A developer selects sort 'rarity'. Unity orders the list ansatsuken, common, epic, l44t, legendary, mishima, mythic, rare, siren, uncommon, because it compares the rarity strings. The owner-requested and original order is common < uncommon < rare < epic < legendary < mythic < DEV tiers. CONVERSION-STATUS claims 'Signed local sandbox with sorted Chip grants'.

**Evidence.** `content.chips.OrderBy(c=>sandboxSort=="rarity"?c.rarity:...).ThenBy(c=>c.name)`. The catalogue's rarity ids are ['ansatsuken','common','epic','l44t','legendary','mishima','mythic','rare','siren','uncommon'].

**Verifier's correction.** The finding is correct as stated, and the Unity panel diverges from the original in two more ways. First, it offers the sorts name/rarity/installed with 'name' as the default, while the original offers default/rarity/rarity-desc/name with 'rarity' as the default (src/client/sandbox-panel.ts:15,37). Second, the original groups the list under tier headers when sorting by rarity (sandbox-panel.ts:24), and Unity has no such headers. The fix is to sort by the index in RARITY_ORDER (unknown tiers last), then by name. Optionally, also make 'rarity' the default sort and add 'rarity-desc'.

**Suggested fix.** Sort by the index in RARITY_ORDER (unknown last), then by name.

<a id="f135"></a>
### #135 ⚪ low — A player's dev-siren entrance starts the boss song and vent eruptions during Ward Zero waves
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (96)
- **Original:** src/shared/simulation.ts:180 (in PvE bossSong is set only by the boss intro)

**What happens.** A DEV human whose entrance resolves to 'dev-siren' (Siren spec, or the dev-siren override intro) starts a co-op run. During that player's intro, BossSongAt is set. From the opening on, the boss music plays, StepSirenStage vent eruptions deal 14 damage plus a launch, and VentHazard blocks vent entries, all through waves 1-3. In the original none of this happens until the Siren's own intro.

**Evidence.** `if(Match.Phase==NativePhase.Intro&&Match.IntroStyle=="dev-siren"&&BossSongAt<0)BossSongAt=(float)Match.IntroStarted;` has no Pve check. NativeSirenStage.cs:79 gates hazards only on `BossSongTime<0`. TS: `if(this.pve){if(this.phase==='fight'&&this.intro?.boss&&...)this.bossSong=...;return;}`.

**Verifier's correction.** The failure holds in Unity for both the Siren-spec and the override-intro variants. In the original, only the override variant can happen, because TS PvE turns a human Siren into the class-default spec (src/shared/pve/system.ts:123; simulation.ts:155 rejects siren in PvE). Unity lacks that fallback, which is a separate divergence.

Affected Unity lines:
- NativeArena.cs:96 (trigger)
- NativeAudio.cs:56 (music)
- NativeSirenStage.cs:17-24 (14 damage plus launch)
- NativeVents.cs:14 and NativePveHorde.cs:18 (vent entries suppressed)
- NativePve.cs:88: the only reset, at the boss intro

The suggested fix (skip the assignment when Pve!=null) is right. For full parity, the PvP trigger should also require Seat.spec=="siren", not just IntroStyle=="dev-siren" (TS simulation.ts:181). Otherwise a non-Siren dev-siren override intro starts the boss song in PvP as well.

**Suggested fix.** Skip the intro-based BossSongAt assignment when Pve!=null.

<a id="f136"></a>
### #136 ⚪ low — Machine denial notice is global: every player sees it, and it never clears on its own
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePve.cs` (69-76)
- **Original:** src/shared/pve/economy.ts:59 (deny emits a targeted event to the denied player only)

**What happens.** Player A presses F at a chip machine without enough money. Pve.Notice='Not enough money' is replicated in every frame, so players B-D see A's denial under their own HUD. It stays on screen indefinitely, until someone completes a successful machine use (the only place it is reset).

**Evidence.** `Pve.Notice="Not enough money";` (lines 69, 71, 73-75) is cleared only by `Pve.Notice="";` at line 76. NativeNetworkFrame.cs:70 `notice=Pve.Notice`. NativePveUI.cs:19 shows it to any viewer with a row.

**Verifier's correction.** The cited lines are right (NativePve.cs:69-76; also line 14 for the field declaration). One more case belongs in the finding: the revive denial at line 69 sets the same global notice, and a successful revive (or pickup) does not clear it. Only a successful paid machine use at line 76 does.

**Suggested fix.** Store the notice per row with a timestamp, show it only to the owning player, and expire it after a couple of seconds.

<a id="f137"></a>
### #137 ⚪ low — Entry view lookup returns the most-open view, so spawns just after an entry reopens are all treated as 'first' and overlap
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeVents.cs` (11, 17-20)
- **Original:** src/shared/pve/entries.ts:27-38 (one slot per entry; isOpen uses its single closeAt)

**What happens.** Vent 3's previous opening closed at T1 (its view stays in Entries until T1+1). An add spawns there at T1+0.5 and creates view V2, whose Open stays 0 for the first 0.35 s (the vent push). A second add picked for the same vent within those 0.35 s (common at the 12-36 adds/s possession rate, up to 8 per tick) gets Entry() returning the old closed view: it is a tie at Open=0 or the old view is still fading. So `first=Time>=view.closeAt` is true again, the follower gap is skipped and both adds climb the same vent at the same moment, overlapping. Doors and cells behave the same within one tick.

**Evidence.** `return Entries.Where(e=>e.entry==index).OrderByDescending(e=>NativeVentRules.Open(e,Time)).FirstOrDefault();` and PlanEntry `bool first=view==null||Time>=view.closeAt;`.

**Verifier's correction.** The file and lines are correct (NativeVents.cs:11 and 17-20; DiveEntry at line 23 uses the same lookup). The trigger window is: the entry's previous view closed less than 1 s ago (it is not pruned until closeAt+1), and a second add picks the same entry while the new view's Open is not above the old view's. For vents that is about the first 0.35 s or more after reopening. For doors and cells it is at least the reopening tick, longer while the old view is still fading. It is intermittent rather than "common" at possession rates, because sustained high spawn rates keep entries open continuously.

**Suggested fix.** For scheduling, use the latest view (max closeAt) per entry, or keep one schedule slot per entry like EntryScheduler. Use the max-Open view only for drawing.

<a id="f138"></a>
### #138 ⚪ low — Possessed add always moves at run speed
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (57)
- **Original:** src/shared/pve/horde.ts:256-265 (possessedMode away/toward use addSpeed(a,Infinity) = walk unless below 50% HP)

**What happens.** A possessed shambler flees or approaches to keep 8-14 m away. In the original it walks at 1.5 m/s, faster only below half HP. In Unity it always runs at 3.4 m/s (runner 4.6), so the keep-away hunt is noticeably harder to close.

**Evidence.** `if(a.Possessed){...speed= a.Stats.run;}`.

**Verifier's correction.** Accurate as reported, with two additions. The line 57 override also drops the crawl x0.35 and limp x0.55 multipliers for a possessed add. And after 60 s (original 'chase' mode) Unity still forces run speed, while the original runs only within 7 m or below 50% HP. Fix: for a possessed add, use the line-56 speed computed with distance treated as infinite (walk unless Hp < MaxHp/2, keeping the crawl/limp factors) during keep-away, and the unmodified line-56 speed once age >= 60.

**Suggested fix.** Use walk speed for away/toward unless Hp<MaxHp/2, and the normal chase speed after 60 s.

<a id="f139"></a>
### #139 ⚪ low — Emperor hits on adds/summons use the player jab table instead of the original minion values (electric hooks no longer stagger adds)
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeFighters.cs` (99)
- **Original:** src/shared/emperor.ts:137 (minions: (electric?8:10)*damage, impulse electric?550:150, range+.5, dot>.2)

**What happens.** In Ward Zero an Emperor's electric hook hits a shambler. The original applies impulse 550, which crosses the horde stagger threshold (impulse>=400) and cancels the add's windup. Unity reuses the player impulse of 120, so there is no stagger, and knockback is half. Jab damage on adds becomes 8/12/18 by index instead of a flat 10. Ansatsuken is unaffected.

**Evidence.** `foreach(var m in Minions.ToArray()){...Heal(p,HitMinion(m,p,damage*(float)p.Stats["damage"],d.normalized,impulse)*...)}` uses the `damage`/`impulse` from lines 85-86, `(emperor?(electric?120:65)...)`, for both kits. TS: `s.chaos.hitMinion(m,(electric?EMPEROR.electricDamage:10)*p.stats.damage,p.id,electric?550:150,dir)`.

**Verifier's correction.** The finding is accurate. Two smaller divergences in the same loop:
- Unity multiplies the impulse by the impulse stat, while TS uses a flat 550/150.
- Unity pushes along d.normalized instead of the forward `dir`, and it drops the `p.hp>0` guard.

The fix belongs at NativeFighters.cs:99. Add an emperor branch with:
- damage (electric?8:10)*damage stat
- impulse electric?550:150 (no stat multiplier)
- direction `dir`
- gates d.magnitude < radius+.5 and dot(d.normalized,dir) > .2
- the p.Hp > 0 guard

Keep the current arcade values for Ansatsuken.

**Suggested fix.** Branch on emperor in the minion loop: damage (electric?8:10)*damage stat, impulse electric?550:150, and the range+.5 / dot>.2 gates.

<a id="f140"></a>
### #140 ⚪ low — Horde strikes credit the Siren boss as attacker (Return Address bombs the boss, deaths attributed to the Siren)
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativePveHorde.cs` (84)
- **Original:** src/shared/pve/horde.ts:289 (source HORDE_ID); src/shared/chaos.ts:73 (revenge-mail needs a player attacker and kind 'direct'); system.ts:202 (kill feed 'horde')

**What happens.** A player with Return Address (revenge-mail) is clawed by an add during the boss phase. Damage() is called with source=m.Owner (the Siren pilot) and direct=true. ChipDamaged then drops a 25-damage bomb on the Siren's current position, wherever she is, giving free boss damage and boss money every 4 s. Deaths and LastAttacker caused by adds are also credited to 'The Siren', even while she is hidden during waves.

**Evidence.** `else Damage(q,m.Owner,damage,new Vector3(d.x,0,d.z).normalized,impulse,false,primary:false,melee:true);` with direct defaulting to true. NativeChips.cs:91 `if(Has(p,"revenge-mail")&&direct&&attacker!=null&&ChipReady(...))Field(p,"bomb",attacker.Body.position,...)`.

**Verifier's correction.** The finding's scenario is correct, with two clarifications.
1. The bomb only damages the boss in the visible boss phases (1, 3 and 4). During waves and possession (phase 2) she is hidden at (0,-100,0) and PveDamageRoom zeroes the damage. The bomb still uses up the 4 s revenge cooldown there, which the original never does.
2. The kill attribution is cosmetic only. The death broadcast shows "The Siren" instead of the original's "Siren minion". No score or economy effect follows, since PveDied only counts the victim's deaths.
For the fix: `direct:false` alone would stop add hits from cancelling a roll (NativeArena.cs:186), which the original does for kind 'minion' (simulation.ts:231). Handle roll cancellation explicitly, or suppress revenge-mail for horde sources, and attribute the kill to a horde identity.

**Suggested fix.** Pass direct:false (or a null/horde source) for add strikes, and attribute kills to the horde.

<a id="f141"></a>
### #141 ⚪ low — Boss fall and spawn handling missing: a boss below y=-6 is floor-clamped into an instant phase skip, and nobody is pushed off the spawn
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeArena.cs` (109 (with NativePve.cs:98, 118, 141))
- **Original:** src/shared/pve/system.ts:225-229 (rescueFall returns the boss to spawn without damage); boss.ts:61-73 (pushOut 4 m)

**What happens.** The Siren is knocked out of the hall, for example by an L44T Y33t (x1000 knockback). The generic fall rule applies `Damage(p,null,p.Hp+1,...,forced)`. PveDamageRoom clamps that to Hp-Floor, so boss HP lands exactly on the floor and the next reveal starts while the boss stays under the map. In phase 3 this goes straight to phase 4 and then death (victory). The original teleports the boss back to the spawn. Separately, at the boss spawn the original pushes party members within 4 m outward; Unity drops the boss onto anyone standing at (0,24.2).

**Evidence.** `foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&p.Body.position.y<-6)Damage(p,null,p.Hp+1,Vector3.zero,0,false,true);` has no boss exception. NativePve.cs:118 `if(p==Pve.Boss)amount=Math.Min(amount,Math.Max(0,p.Hp-Pve.Floor));`, and line 98 sets the position without pushOut.

**Verifier's correction.** The defect and the phase-skip chain hold as described: phase 3 goes to phase 4 then victory, and a phase-4 fall is an immediate victory. In phase 1 the fall only removes HP down to the 75% floor before possession hides the boss anyway. The trigger is narrower than "knocked out of the hall": the Ward Zero PvE hall is sealed (walls to the roof plus a roof), pilots use ContinuousDynamic collision, the boss is excluded from vent pits, and L44T is developer-only. The fall branch therefore only fires after a physics escape or tunnelling glitch, which is the case the original's rescueFall exists to cover. The missing 4 m spawn push-out (NativePve.cs:98 vs boss.ts:33/38-44) is reachable in normal play whenever a member stands within 4 m of (0, 24.2) when reveal 1 ends. In that case PhysX depenetration separates the overlapping capsules when physics resumes after the 9 s intro hold, instead of the original's deterministic push.

**Suggested fix.** For the PvE boss, reset it to the spawn instead of damaging it. Push members out to 4 m when the boss spawns.

<a id="f142"></a>
### #142 ⚪ low — Boss music pauses during every reveal and outro hold
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeAudio.cs` (56 (NativeSirenStage.cs:75))
- **Original:** src/shared/simulation.ts:180 (song time = sim.time - startedAt; sim.time keeps running through director holds, which shift timers instead)

**What happens.** At each phase threshold the 5 s reveal card plays. Unity freezes Time, so BossSongTime stops and the track is explicitly paused, then resumes. In the original the Black Siren Protocol keeps playing through reveal cards, and its hazard and loop timeline continues from the song start.

**Evidence.** `Track("music/black-siren-protocol",Music*.55f,arena.Pve?.Holding==true&&arena.Pve.Cinematic!="intro",arena.BossTrack.Looped(arena.BossSongTime));`. BossSongTime only advances during the intro hold. The freeze is not listed among the documented adapters.

**Verifier's correction.** The finding's lines are correct: NativeAudio.cs:56 holds the pause condition and NativeSirenStage.cs:13 (the BossSongTime property; the cited :75 is wrong) builds the frozen clock. NativeArena.cs:96 is the root cause: the hold path skips Match.Tick. Music goes silent at the phase 2, 3 and 4 reveals (5 s each, the possession-found reveal included) and the outro (4 s or 3 s). The first reveal, before the intro, is unaffected because the boss song has not started then in either version.

**Suggested fix.** Accumulate hold time into BossSongLead for reveal and outro holds and keep the track playing, or document the deliberate change.

<a id="f143"></a>
### #143 ⚪ low — DEV kits and the sandbox switch off mid-match when the 15-minute signed claim expires
- **Area:** PvE & DEV
- **Where:** `Assets/ChipArena/Runtime/NativeFighters.cs` (23 (Core/NativeAccess.cs:14, 25))
- **Original:** src/shared/access.ts canUse (no expiry; access verified at join and kept for the session)

**What happens.** Account claims are limited to exp-iat<=15 min. Dev() calls Allows(), which requires Expires>now on every tick. If the account refresh (every 300 s) fails, for example because the account service is briefly unreachable or a remote client's auth-refresh is lost, an Emperor, Ansatsuken, Leet or Siren pilot silently loses all special abilities mid-round, and SandboxAllowed turns off. The original never re-checks expiry within a session.

**Evidence.** `Dev(...)=>...p.Seat.access.Allows("weapon:"+spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())` and `Allows(...)=>Expires>now&&(Developer||grants.Contains(key))`. Verify rejects claims with `p.exp-p.iat>900000`.

**Verifier's correction.** The failure needs sustained refresh failure, not a single missed 300 s refresh. After a failure, NativeAccount.cs:48 retries every 60 s, so the claim expires only if the account service stays unreachable for about 10 minutes after the last successful refresh. Other triggers:
- A 401 or revoked session: Clear() stops refreshing and the seat claim lapses up to 15 minutes later.
- Two refresh deliveries in a row lost by a remote client that was disconnected at those moments. The resume path at NativeNetwork.cs:75 ignores the hello token.
- A host wall clock more than 10 minutes fast. This causes repeating drop-outs, each about Δ−10 minutes long, in every 5-minute refresh cycle. TS accepts any skew under 15 minutes because it verifies only at join.

A separate observation, not the reported defect: the "could not be verified" path at NativeAccount.cs:50 and the oversized-response path at :46 leave refreshAt unchanged. That makes Update re-request /me continuously, and no new token is accepted, so either path also leads to this expiry.

**Suggested fix.** Check expiry when a seat joins and on refresh, but keep the session's verified grants for the running match.

<a id="f144"></a>
### #144 ⚪ low — NativeAccount declares a private Start(string,string,bool), which collides with Unity's Start message
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/NativeAccount.cs` (36)

**What happens.** NativeAccount is a MonoBehaviour. Unity resolves magic methods by name, finds `Start` with three parameters and reports "Script error (NativeAccount): Start() can not take parameters." when the component is used. BuildWindows makes a Development build, where logged errors open the on-screen developer console at launch.

**Evidence.** `private void Start(string action,string body,bool authenticating){...}` on `public sealed partial class NativeAccount:MonoBehaviour`.

**Suggested fix.** Rename it, for example to Begin or StartRequest.

<a id="f145"></a>
### #145 ⚪ low — Preferences are saved non-atomically, and an unreadable file permanently blocks startup
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Runtime/ConversionGame.cs` (27, 44)

**What happens.** Save() runs `File.WriteAllText(PreferencePath,...)` directly. It is called on many toggles (V, glow, first-person, StartMatch). If the game crashes or power is lost mid-write, native-preferences-v1.json is truncated. On the next launch, `JsonUtility.FromJson<ConversionPreferences>` throws ArgumentException inside Awake's try. The catch-all only stores the text. The camera, audio and presentation are never created, OnGUI shows the error full-screen, and the game is unusable until the user finds and deletes the file under AppData\LocalLow.

**Evidence.** Awake: `prefs=File.Exists(PreferencePath)?JsonUtility.FromJson<ConversionPreferences>(File.ReadAllText(PreferencePath)):new ConversionPreferences();` sits inside `try{...}catch(Exception e){error=e.ToString();...}`. Save: `File.WriteAllText(PreferencePath,JsonUtility.ToJson(prefs,true))`. The account session code already uses a .new file plus a move.

**Verifier's correction.** Trigger: a power loss or OS crash during Save (leaving a NUL-filled or partial file), on-disk corruption, or a hand edit. A plain process crash is not a realistic trigger, because the roughly 1.5 KB file goes out in one write and an empty file already falls back to defaults at line 28. The failing lines are ConversionGame.cs 27 (parse inside the catch-all), 40 (the catch stores a sticky error), 44 (in-place write) and 172 (the error screen replaces all UI). The parity reference is src/client/main.ts:88, where a parse failure falls back to defaults. Line 44's own catch (and line 59) also leave the UI stuck on an error screen until restart.

**Suggested fix.** Write to a temp file and File.Replace it. On a parse failure, fall back to defaults (and keep a .bak) instead of aborting Awake.

<a id="f146"></a>
### #146 ⚪ low — Windows packager zips whatever is already in Builds/Windows
- **Area:** Unity engine
- **Where:** `Assets/ChipArena/Editor/PrototypeBuild.cs` (101-118)

**What happens.** BuildWindows creates Builds/Windows if it is missing but never empties it. It then builds into the folder and runs ZipFile.CreateFromDirectory over the whole folder. Files left from earlier builds end up in the shipped TEST ZIP: an old executable or _Data folder after a rename, crash dumps, or files a tester dropped in the folder.

**Evidence.** `string folder=Path.Combine(root,"Builds","Windows"); Directory.CreateDirectory(folder); ... ZipFile.CreateFromDirectory(folder,zip,...)`, with no delete of the old folder contents.

**Verifier's correction.** Unity's player does not write crash dumps into Builds/Windows; they go to %TEMP%. The game does not write next to the EXE either, since all its writes use persistentDataPath. The stale-content cases that hold are: a manual Build Profiles build into the same folder (an extra EXE and _Data folder under the productName-derived name, or an IL2CPP *_BackUpThisFolder_ButDontShipItWithYourGame folder), files a tester puts in the folder, or a future EXE or product rename. The fix still stands: delete and recreate Builds/Windows before BuildPlayer, or zip only the files listed in the BuildReport.

**Suggested fix.** Delete and recreate Builds/Windows before BuildPlayer, or zip only the files listed in the BuildReport.

## Disproved findings

Raised by a reviewer, then disproved by the verifier. Listed so nobody re-raises them.

- **DEV fighter powers are re-checked against wall-clock token expiry every tick; mid-match expiry leaves an inert pilot** (Combat). The mechanism in the finding is accurate. Dev() re-checks `access.Allows(...)` against wall-clock `Expires` every tick, and if that check ever fails, StepFighter cancels the fighter. The TS original does verify the token once at join (server/index.ts:46, host-worker.ts:4-5) and then uses a static `canUse(p,'weapon:emperor')` (emperor.ts:13, arcade.ts:14). The scenario, however, assumes the signed access runs out during normal play, and the Unity code prevents that with a refresh path the finding misses: - Account tokens last 15 min (GAME_CLAIM_MS). NativeAccount.Update re-requests `me` every 300 s (NativeAccount.cs:52,58), and the `me` endpoint returns a freshly signed token (relay/accounts.ts:73). - On every accepted refresh, NativeProfileUI.cs:26 writes the new access onto the host's own seat (`arena.Local.Seat.access=account.Access`). A joined client sends `auth-refresh` to the host. - The host handles `auth-refresh` before the arena/epoch gate, at any time including mid-match. It replaces `seat.access` after re-verifying the token and matching the AccountId (NativeNetwork.cs:84). - Seats are shared by reference: peer.Seat goes into match.Seats (ConversionGame.cs:54) and then into NativePilot.Seat (NativeArena.cs:86). So the refreshed access is what Dev() reads. - Even if one refresh is missed, about 10 min of validity is left. A resumed peer keeps its old seat, but the resume window is only 90 s, so it does not open a gap either. - CONVERSION-STATUS.md:21 documents this as deliberate: "signed unlock verification, Windows encrypted sessions and in-room token refresh". - The legacy 8 h Sheldon dev token has no producer in the Unity client, since tokens come only from NativeAccount. So in a normal match a DEV pilot never goes inert. What remains is a narrow failure-mode difference from TS, low at most: - the account service is unreachable for 10 min or more during a match (failures retry every 60 s), or - the 12 h non-remembered session expires mid-match. The 401 calls Clear(), Accepted never fires, and the seat keeps its old access only until that token's `exp`. In TS those cases leave powers intact until the room ends. In Unity, powers can end up to about 15 min later. That follows from the documented time-bounded access design and is not a defect in normal play. The suggested fix (resolve access once at match start) would remove the deliberate refresh and expiry property.

- **Relay-room horde caps are not applied (28 alive in waves, 112 during possession)** (PvE & DEV). The code reading in the finding is correct. NativePve.cs:112 calls `NativePveRules.Cap(data,h,false)`, and line 113 uses `Math.Min(v.Hidden?192:40,...)`, so Unity always uses the non-relay caps. The part that does not hold is the claim that the original would use the 28/112 caps for the room Unity hosts. 1. **What the original's `relay` flag means.** It is only set to true in shared/host-room.ts (`relay:settings.local!==true`). That class runs inside a browser web worker (client/host-worker.ts, reached through BrowserRoom) and talks through the PartyKit relay. The original's own reason for the lower cap is about that worker. docs/PVE.md says relay rooms keep 28 because "the host PC's worker is unmeasured". The reason is the browser-worker host's unmeasured CPU, not relay bandwidth in general. 2. **Every other host uses the full caps.** Dedicated servers call `pveOption(...)` with only seed and debug (server/index.ts:46), so relay is false. The Windows desktop runtime (Electron), which is what Unity replaces, also uses relay=false. When `desktopHosting()` is true, main.ts:198 goes to `createDesktopRoom`. desktop/main.cjs `hostRoom(online)` then starts `launcher/start.mjs`, which runs `server.mjs`. scripts/package-windows.mjs builds `server.mjs` from `src/server/index.ts`. Online desktop hosting only adds a Cloudflare tunnel on top. So on Windows, both LAN and internet hosts in the original get 40/192. 3. **Unity's equivalent host.** Unity's "Host internet room via conversion relay" and its LAN/headless hosts all run native Unity authority on the host. That matches the original's Node-server desktop host, not its browser-worker host. Unity has no browser-worker host, so the condition the original's relay caps protect against does not exist. 4. **The numbers match.** For h=4, Unity gives wave 3 = min(40,18+15) = 33 and possession = min(192,(6+12)*12) = 192. These are exactly what a Windows desktop host gets in the original. A player would see no difference from the desktop game. 5. **No resource failure is shown.** The Unity relay limits each guest's downstream traffic to 8 MiB/s (Relay/Program.cs:27, `Copy(host,socket,...,8*1024*1024)`). Snapshots go out at 20 Hz (NativeNetwork.cs:58, `nextSnapshot=...+.05f`) and are compressed with Deflate. Hitting the limit would need about 419 KB per compressed frame. The author's tests already passed 130 KB uncompressed snapshots, and nothing in the finding shows 192 adds getting near the limit. The "raises bandwidth and CPU" claim is a guess, not a demonstrated failure. At most this is a design note: Unity could choose lower caps for player-hosted internet rooms. It is not a parity defect against the Windows runtime the conversion replaces.

- **Ward Zero party bots can roll magic weapons** (PvE & DEV). The scenario cannot happen with the default settings. In PvE, ConversionGame.StartMatch limits bots to 0-3 (`prefs.bots=Mathf.Clamp(prefs.bots,0,3)`) and to `Math.Min(prefs.bots,4-match.Seats.Count)`, so the loop index i is only ever 0, 1 or 2. `weapons` keeps the order of GameContent.json (JsonUtility array, then LINQ Where/Select, which keep order). The non-DEV list starts [pistol, rifle, machinegun, fire, water, lightning, darkness, sword, ...]. So `weapons[i%weapons.Length]` always gives pistol, rifle and machinegun, all ranged, and never a staff. This also holds for a dedicated host, where there is no local seat: there are still at most 3 bots, with indices up to 2. ConversionGame.cs:56 is the only place that creates party bots. The only way a party bot gets a magic weapon is the Unity-only opt-in toggle "Bots use my weapon (otherwise mixed loadouts)" (prefs.mirrorWeapons, default false). The player turns it on deliberately, and when they pick a staff, mirroring that staff is what the toggle promises. The cited code does differ from the original, just not in the way the finding says. In the original, system.ts:130 does `rng.pick(PARTY_WEAPONS)`: a random pick from 7 ranged/melee weapons, seeded per bot. In Unity the party bots are always pistol/rifle/machinegun, in that fixed order, and never melee. For recruits, the finding's claim about DPS recruits is also wrong: Unity's DPS recruit gets rifle or sword (NativePveHorde.cs:112), both on the allowed list. A related real case does exist. The original medic recruit uses a pistol (recruits.ts:17 `RECRUIT_SPEC={tank:'shield',medic:'pistol'}`), but Unity gives the medic `water`, which is a magic staff (NativePveHorde.cs:112 `spec=role=="tank"?"shield":role=="medic"?"water":...`). That is the only path where Ward Zero produces a magic-weapon bot with default settings. It is a recruit, not a party bot, and it is not what the finding describes.

- **Version guard checks only the frozen TS baseline commit and the protocol byte, not the Unity build, so different Unity builds join each other and silently mis-deserialise** (Networking). This is a possible future process slip, not a defect in commit a8fa796. At this commit there is exactly one Unity build, so no two builds can disagree about the message layout. The failure in the finding only happens if a later change to the message layout ships without anyone bumping NativeConnection.Protocol. The code has an explicit wire-version mechanism, and the author already uses it as intended: - Every transport frame carries a protocol byte, and the receiver drops the connection when it does not match ("Incompatible conversion protocol"). - Every JSON message and every game frame also carries `version` and is rejected on mismatch. - The relay endpoints are path-versioned (/native/v3/...). - In this very commit, the frame layout change was paired with a bump from protocol 2 to 3, as the DEV_LOG records ("Native protocol advances to 3 ... older native clients cannot join"). CONVERSION-STATUS.md describes the feature as a "protocol/source lock". The code implements exactly that, so the docs do not over-claim. The UI text "Guests need the same conversion build" tells players what to do; it does not claim the game enforces build identity. It is also not a parity divergence. The original TypeScript rooms (src/shared/host-room.ts, src/relay/party.ts, src/server/index.ts) have no build or commit check on join at all. The only related check is the PvE "Update the game to join Boss Test rooms" hello gate, and the build/version check in worker.ts is for the updater, not for rooms. So the Unity guard is stricter than the original, not weaker. Adding Application.version or a hash of the message layout would be a reasonable hardening idea. It is not a concrete bug a player would hit in this build, so I consider the finding refuted (at most a low-severity maintainability note).

- **No path gets Unity builds to testers; the existing updater cannot carry a Unity player and only the owner can build one** (Delivery & docs). The facts in the finding are mostly accurate. CI builds only a source ZIP. Build-Windows.cmd needs a locally installed, activated Unity 6000.3.24f1 Editor. The baseline launcher's `allowedPath` whitelist accepts only Electron payload paths. No native relay is deployed. None of this is a defect under the review criteria, though. It is not a bug, not a rules or timing divergence a player would notice, not a claim the code fails to back, not a Unity import/build/play defect, and not a security hole. It is an infrastructure and planning gap, and the author documents it as a deliberate boundary: - CONVERSION-STATUS.md:32 says the web updater is "replaced by the separate Unity player, preferences and manual test-build ZIP. The existing updater/feed is untouched." - CONVERSION-STATUS.md:30 says "A public native relay requires a separate deployment." - Relay/README.md:7 and :19 say the same about the relay. - VALIDATION.md:87 says "No production update, live endpoint, save migration or automatic release was performed." - The newest DEV_LOG entry says "No updater/feed publication, service deployment, account mutation or production change," and names the manual Build-Windows.cmd step as the next step. No doc claims that testers receive Unity builds through the Update button, that CI produces an EXE, or that an internet relay is live. CONVERSION-STATUS.md:23 ("EXE and complete-folder ZIP produced only after gates pass") matches Build-Windows.cmd:21-33. The project rules also bar the conversion agent from building this delivery path on its own initiative. AGENTS.md says: "No production release, public distribution, live-data migration, infrastructure removal ... without explicit owner authorization." The standing test-channel instruction covers only "completed game updates from the verified, approved shared-branch commit". This conversion is an unaccepted candidate on a separate branch (CONVERSION-STATUS.md:5: "not a claim that native gameplay has passed acceptance"). Publishing it through the Electron channel or standing up a new channel would itself break policy. Parts of the finding are overstated: - "Only the owner can build one" is wrong. Build-Windows.cmd:3 honours a `UNITY_EDITOR` override, and anyone with Unity 6000.3.24f1 and their own (for example Personal) license can run it. - "A native relay that does not exist yet" is wrong. Relay/Program.cs exists and passes the loopback tests; only the deployment is missing, and that is documented. - "100+ MB ZIP" is unverified. The owner does need a Unity delivery plan (CI Windows build, a Unity test channel, a hosted relay) before Unity replaces Electron for testers. That is a valid roadmap note, low at most, but not a high-severity defect in this branch.

- **Unity and Electron will drift: baseline 05950ea is hard-coded in about 15 files and nothing detects or ports Electron changes** (Delivery & docs). This is a worry about future process, not a defect in the code. None of the review's defect categories apply (bug, divergence a player would notice, unbacked doc claim, Unity failure, security). 1. There is no drift today. `origin/claude/visual-revision` is byte-identical to the baseline 05950ea (`git diff --quiet` succeeds). I checked every remote branch with commits after 05950ea (release/candidate-workflow, chip-arena-typescript-errors, test-update-channel, sleepy-ritchie, codex/unity-prototype, codex/unity-conversion). None changes anything under `src/`. Unity is therefore at parity with the current shared game branch, and the scenario only applies to Electron changes that have not happened. 2. The pinned baseline is deliberate and documented. CONVERSION-STATUS.md:3-4 says "Source baseline: `05950eaef...`" and "The Electron game and accepted Unity lab remain separate." No doc says the Unity port tracks or syncs Electron changes, so no author claim is contradicted. Every hand port has to be pinned to some baseline. Whether Electron gameplay is frozen now that the owner chose Unity is an owner and AGENTS.md policy question, not a code fault. 3. "Nothing detects" drift is overstated. tools/export_source.mjs:10 hashes each of the 108 locked source files against its pinned git blob SHA. If any of them changed, it throws "Source differs from pinned commit". Re-exporting from a newer checkout therefore fails loudly. It is a manual check rather than CI, but it is a check. 4. "About 15 places" is inflated, and the hard-coded values fail safe. The literal SHA is actually compared in code only in Runtime/NativeSourceModels.cs:24, Runtime/NativeWoundGeometry.cs:14, Core/NativeMeleeChecks.cs:21, Core/NativeSpecialChecks.cs:22, Tests/test_cinematics.py:7, and as a constant in tools/import_mocap.py:12. tools/export_wound_fixtures.mjs:10 and tools/export_motion.mjs:33 only write it into output files. The mentions in GunRules.cs, NativeMeleeRules.cs, ArenaModel.cs and NativeAbilityRules.cs are comments, and CapturedAnimation.cs only puts the short SHA in a log message. The other runtime guards cited (NativeWardPresentation.cs:12, NativeSceneryMotion.cs:15, NativeSummonPresentation.cs:15) and the network handshake (NativeNetwork.cs:45/73, NativeNetworkFrame.cs:32) compare against the single source `arena.Content.sourceCommit` from GameContent.json. Those guards are consistency checks between exported data files. If a re-export is partial, they throw at load and never run silently on mixed data, so they cannot produce a wrong game outcome. What remains is maintainability: moving the baseline means editing a handful of literals and re-running the exporters. That is a refactoring preference, not a concrete failure, so the finding is refuted. At most it is a low-severity note for the owner.

- **Repository rules and process docs still describe the Electron engine and contradict the owner's Unity decision** (Delivery & docs). The quotes are accurate, but reading them as a defect in the conversion overstates the case. What remains is a policy follow-up that only the owner can authorize. 1. AGENTS.md does not block the Unity work. The "do not change engines" clause covers "repository migration" only. AGENTS.md:7 already says explicit current owner instructions decide the requested change and that conflicts should be documented, and the author did document them. The first DEV_LOG conversion entry says the owner "explicitly selected native Unity/C# including gameplay/physics" and kept it separate on its own branch. An agent given the owner's Unity decision has a clear rule for which instruction wins. 2. The repository has no record of the owner's decision to adopt Unity; it appears only in the review prompt. At a8fa796 the docs correctly describe an isolated, unmerged candidate with no cutover: "Source remains pinned to 05950ea; no original-game cutover" and "No merge, test updater publication or production deployment". So PROJECT_STATE.md:12 ("Runtime: TypeScript/Three.js/Rapier") is still true of the game being shipped. The Electron test channel and claude/visual-revision are also still the live paths for that game. Rewriting AGENTS.md (shared policy) needs owner authorization, as the finding's own fix hint admits ("With explicit owner authorization"). The conversion author was right not to change it on an isolated branch: git log shows AGENTS.md, DEVELOPMENT.md and ci-scope.mjs were last touched by f03123a, not by the conversion. 3. The Unity commands and CI are documented, just not in DEVELOPMENT.md. The Unity README.md:5-12 covers open, build, Build-Windows.cmd and UNITY_TEST_BUILD_READY. VALIDATION.md covers the checks, and a dedicated .github/workflows/unity-conversion.yml runs on unity/ChipArenaConversion/** paths: check_relay.py, the unittest discover run and package_source.py. ci-scope.mjs only picks a mode for checks.yml, so the Unity workflow does not need it. 4. The PROJECT_STATE "stale counts" sit in dated checkpoint bullets. Line 127 ("31 scenes / 124 models") matches the cinematic checkpoint's DEV_LOG entry ("Source model bank now has 124 models"). Line 124 ("Remaining parity/coverage is documented in CONVERSION-STATUS") belongs to the earlier checkpoint. The section at line 129 is headed "current; supersedes earlier checkpoint gaps" and points to CONVERSION-STATUS for adapters and acceptance boundaries, which that file does contain (lines 25-38). That is redundant clutter against the "keep PROJECT_STATE short" rule, not a false claim about the code. None of this is a bug, a player-visible divergence, an unbacked claim in CONVERSION-STATUS, VALIDATION or DEV_LOG, a Unity defect or a security issue. At most there is a low-priority process note: once the owner formally adopts Unity, update AGENTS.md, DEVELOPMENT.md, RELEASES.md and PROJECT_STATE, and remove the superseded checkpoint bullets.

- **Settings and remembered sign-in live under a TEST-named company/product folder that Prepare() rewrites; renaming orphans them and Electron settings never migrate** (Delivery & docs). The cited code is accurate, but nothing in it fails as written. The scenario needs a future change that the code does not make. 1) Rename trap. Prepare() sets companyName and productName to the same values already stored in ProjectSettings.asset (lines 7-8). The build is therefore consistent: the TEST build always reads and writes its preferences and sign-in file under one stable persistentDataPath. Data is lost only if someone later renames the product. That loss is standard Unity behaviour for any project and would come from that rename, not from this code. A TEST-named storage folder is arguably intended: it keeps owner-test data apart from a future production install, just as the Electron TEST installs use a separate channel. Picking the final product name and planning a data cut-over are release decisions for the future. The project docs say this is not a release (PROJECT_STATE: "owner-test candidate, not a tested binary or release"). There is also no real installed base to lose yet: no Windows build has been accepted, and VALIDATION.md:3 says no executable was built in the authoring workspace. Even if a rename happened, the impact is small and easy to fix. The DPAPI vault's salt is "ChipArenaUnity/account/v1/"+origin and does not depend on the file path, so copying conversion-account.bin to the new folder would still decrypt. Losing the file only forces one fresh sign-in. Profile data lives on the server. 2) Electron settings are never imported. This is a documented, deliberate adapter. CONVERSION-STATUS.md:32 says: "Electron's Chromium shell, browser storage and web updater are replaced by the separate Unity player, preferences and manual test-build ZIP." README.md:45 says: "Preferences are separate from the original game." The finding admits "The separation is documented." Under the review rules a documented adapter is not a finding unless it breaks something, and nothing breaks: on first launch the Unity build falls back to new ConversionPreferences() (ConversionGame.cs Awake). Importing data would also mean reading Chromium LevelDB localStorage and Electron safeStorage blobs, which is a new feature, not a parity defect. No doc claims a migration exists; VALIDATION.md:87 explicitly says "No production update, live endpoint, save migration or automatic release was performed." No current bug, crash, divergence or false doc claim holds. What remains is advice for future release planning: choose the final names, or add a one-time import or reset notice, at cut-over.

- **CI never checks committed parity fixtures and exported data against the pinned source** (Delivery & docs). The finding's facts about CI are right, but they don't add up to a defect. The category is "unsupported-claim", yet none of the quoted claims is false, and no doc says CI regenerates or hash-checks the fixtures. 1. The CI facts hold. The workflow sparse-checks out only unity/ChipArenaConversion, has no Node step and runs no export_*.mjs. asset-lock.json covers 83 audio files plus 9 Resources files, and GameContent.json and Tests/*-parity.json are not among them. GameContent.json (734,800 B) is under package_source.py's 1 MiB guard. Every Tests/*.json fixture is under 1 MiB too, the largest being 742,484 B. Neither Tests/*.cs nor the provenance files hash the fixture outputs; the provenance files lock only the source inputs. 2. The quoted claims are true and are about local runs, not CI. VALIDATION.md:41 says "expected outputs come from the original functions, not C#". That is how export_source.mjs works: it checks each source file's Git blob hash against source-lock.json, then imports the original TS functions (content.statsFor, eligibleChips, offerChips, rollProfile, stageHazards) and writes their outputs. VALIDATION.md:57 says "Export reproduced identical content/fixture hashes". I re-ran export_source.mjs from a scratch copy against the pinned source and got byte-identical GameContent.json and Tests/source-parity.json (cmp: identical). The finding itself concedes "Today the data reproduces". 3. No doc says CI does this. The only CI mentions in CONVERSION-STATUS, VALIDATION and DEV_LOG are: CI uses the normal .NET runner, CI runs the package regressions, and the CI source-package gate must check the 83 audio receipts. The CI step name "Native source parity" fairly describes comparing C# against fixtures generated from the source. 4. It is not a bug, a gameplay divergence, a Unity defect or a security issue. It only says nothing stops a future fixture or export drift. That is a reasonable CI hardening idea, but the rubric asks for concrete defects, not process suggestions. Also, the team has committed to the Unity version, so GameContent.json is expected to change on purpose later. A hard lock to the pinned TS source would then be a conversion-phase guard, not a correctness rule for the game.

- **A stalled guest keeps moving and firing on the host for 0.75 s (source: 0.3 s)** (Gaps found later). The finding reads only the fallback in simulation.ts:340, `this.options.inputHoldSeconds ?? C.staleInputSeconds`, and assumes the source uses 0.3 s. In the source, every player-hosted room, whether a browser or desktop host behind the relay, is a HostedRoom. HostedRoom builds its Simulation with `inputHoldSeconds: RELAY.inputHoldSeconds`, and relay-protocol.ts sets that to 0.75. The 0.3 s default applies only to the dedicated Node server (src/server/index.ts), which builds its Simulation without that option. The source's own TEST-RESULTS.md says so: "Browser-hosted input expiry is now 0.75 seconds instead of the dedicated server's 0.3 seconds." Unity's player host (HostRoom/HostRelay in ConversionGame, via NativeArena.RemoteInput) is the counterpart of the source HostedRoom. Its 0.75 s window is exact parity, so the finding's scenario (a stalled guest on a player host) behaves the same in both. The Unity hold logic also matches the source in what it keeps: taps (jump, roll, reload, abilities) clear after use, while movement, sprint, aim and ranged Shoot stay held. Both then go idle except for yaw and pitch. One narrow difference remains, and it is not the one claimed. Unity's unattended `--ca-server` authority (NativeDedicated.cs) uses the same RemoteInput, so it also holds for 0.75 s, while the source's dedicated Node server used 0.3 s. That affects only headless-server rooms. It is at most a low, arguably deliberate choice, since a Unity dedicated server can also run through the relay (`--ca-relay`), where the source uses 0.75.
