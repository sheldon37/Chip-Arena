using System;
using System.IO;
using System.Linq;
using UnityEngine;

using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ConversionPreferences
    { public string[] roundsPacks=Array.Empty<string>();public bool roundsCardsPlus;public int modRules;public string[] chipPacks=Array.Empty<string>();public bool fullscreen;public int team;public string botDifficulty="normal";public NativeBotOption[] botOptions=Array.Empty<NativeBotOption>();public bool skipPlayerIntros,randomizeMap;public int choices=6,selectionSeconds=18;public bool bothDraft;public string relayOrigin="";public string graphics="auto",sirenMode="Q";public float sensitivity=1,audio=.3f,music=.35f,menuMusic=.6f,uiScale=1;public bool trails=true,reducedEffects,cameraShake;public string lobbyPose="weapon";public NativePendingEffect[] pendingEffects=Array.Empty<NativePendingEffect>(),pendingIntros=Array.Empty<NativePendingEffect>();public string bodyColor="#e5e7eb",accentColor="#70e1d0",hat="none",killEffect="impact-ragdoll",intro=NativeIntroRules.Default,introText="Ready to make an impact",accountOrigin=NativeAccount.DefaultOrigin;public int accountServiceVersion;public bool overrideIntro;public string name="Pilot",map=NativeMapRules.DefaultArena,weapon="pistol";public int bots=3,sets=5,rounds=2;public string combatGlow="Full",damageScreen="Full",alarmLights="Full",playerModel="New";public bool pve,expedition,skipWaves;public bool mirrorWeapons,teams,friendlyFire,stationary,skipBotDrafts,sound=true,firstPerson;
        // Older Unity installs shipped an unconfigured account URL. Upgrade it once;
        // thereafter an empty URL is the player's explicit offline choice.
        public bool MigrateAccountService()
        {if(accountServiceVersion>=1)return false;if(string.IsNullOrWhiteSpace(accountOrigin))accountOrigin=NativeAccount.DefaultOrigin;accountServiceVersion=1;return true;}
        // The portrait backdrop picked in the Locker (NativePortraitBacks key; absent in older saves: the tick ring).
        public string lobbyBack=NativePortraitBacks.Default;
        // Settings · Camera (Core/NativeCameraModes): the third-person rig and its shoulder (absent in older saves: the right
        // shoulder, the new default); firstPerson above stays the first-person choice that V toggles.
        public string cameraMode=NativeCameraModes.Default;public bool shoulderLeft;
        // The Locker's per-weapon skins (NativeWeaponSkins map; absent in older saves: every weapon Standard).
        public string weaponSkins="";
        // The model's outfit (Core/NativeLook.cs): kept here and shown on this client's own pilot until the look contract
        // carries it to the seat (Astra).
        public string look="";
        public bool developmentConsole;
        // The headwear and backdrop colours picked in the Locker (NativeHatLooks: "" Auto, else #rrggbb; absent in older saves: Auto).
        public string hatColor="",backColor="";
        // DEV tools (NativeDevPanel.cs, docs/DEV-TOOLS.md): Settings · Developer · DEV Abuse (off by default) and the panel's chip favourites.
        public bool devAbuse;public string devChipFavourites="";
    }
    public sealed partial class ConversionGame : MonoBehaviour
    {
        private NativeSingleInstance singleInstance;private ConversionContent content;private ConversionPreferences prefs;private NativeMatch match;private NativeArena arena;private ArenaPresentation presentation;
        private NativeAudio nativeAudio;private Camera cameraView,viewModelCamera;private InputFrame pending;private SimulationMode previousSimulation;private Vector3 previousGravity;private bool ownsPhysics;private int generation;
        private bool menu=true;private float yaw,pitch=12,lastW=-1;private bool sprint;
        private string search="",error="";private GUIStyle heading,label,small,button;
        private readonly NativeVisualClock visualClock=new NativeVisualClock();
        private NativeBloom bloom;private NativeToonOutline outline;
        private double stepMs;private readonly System.Diagnostics.Stopwatch timer=new System.Diagnostics.Stopwatch();
        private string PreferencePath=>Path.Combine(NativeUserDataPath,"native-preferences-v1.json");
        internal static string NativeUserDataPath
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return Path.Combine(Path.GetDirectoryName(Application.persistentDataPath),"Chip Arena Native Conversion TEST");
#else
                return Application.persistentDataPath;
#endif
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){if(FindFirstObjectByType<ConversionGame>()==null)new GameObject("Chip Arena Native Conversion").AddComponent<ConversionGame>();}
        private void Awake()
        {
            try
            {
                var asset=Resources.Load<TextAsset>("GameContent");if(asset==null)throw new InvalidOperationException("GameContent.json is missing");
                content=JsonUtility.FromJson<ConversionContent>(asset.text);NativeUnityMapLoader.Load(content);content.Validate();if(InitializeDedicated())return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                singleInstance=new NativeSingleInstance("Local\\ChipArena.Native.Player");if(!singleInstance.Acquired){Application.Quit(0);return;}
#endif
                prefs=NativeFileStore.Read(PreferencePath,s=>JsonUtility.FromJson<ConversionPreferences>(s),()=>new ConversionPreferences(),out var preferenceWarning);if(preferenceWarning!=null)networkStatus=preferenceWarning;
                LoadMods();
                if(prefs.MigrateAccountService())Save();prefs.cameraMode=NativeCameraModes.Sanitize(prefs.cameraMode);
                // A retired entrance pick (neon-drop, vector-slide, signal-spin, arc-salute, pulse-wave) becomes the weapon signature.
                {string intro=prefs.intro;bool pick=prefs.overrideIntro;NativeIntroRules.Migrate(ref intro,ref pick);if(intro!=prefs.intro||pick!=prefs.overrideIntro){prefs.intro=intro;prefs.overrideIntro=pick;Save();}}
                {string map=NativeMapRules.Selection(content,prefs.map);if(map!=prefs.map){prefs.map=map;Save();}}
                if(!content.weapons.Any(w=>w.id==prefs.weapon&&!w.devOnly))prefs.weapon="pistol";prefs.bots=Mathf.Clamp(prefs.bots,prefs.pve?0:1,prefs.pve?3:7);
                InitializeAccount();Application.runInBackground=true;Application.targetFrameRate=144;QualitySettings.vSyncCount=0;Time.fixedDeltaTime=1f/60;Time.maximumDeltaTime=.1f;
                previousSimulation=Physics.simulationMode;previousGravity=Physics.gravity;ownsPhysics=true;Physics.simulationMode=SimulationMode.Script;Physics.gravity=new Vector3(0,-23,0);
                // These compressed banks expand to ~14 MB and ~94 MB of JSON. Never first-load them on a kill.
                long motionAt=System.Diagnostics.Stopwatch.GetTimestamp();NativeSourceMotion.Prewarm();NativeSceneMotion.Prewarm();
                UnityEngine.Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"MOTION_BANKS_READY source={0} scene={1} ms={2:F1}",NativeSourceMotion.ClipCount,NativeSceneMotion.Count,HitchMs(motionAt));
                nativeAudio=gameObject.AddComponent<NativeAudio>();presentation=gameObject.AddComponent<ArenaPresentation>();presentation.Initialize(false);nativeAudio.Footfalls=presentation.Footfalls;presentation.DebrisEnabled=false;Physics.IgnoreLayerCollision(8,11,true);Physics.IgnoreLayerCollision(11,11,true);
                var cam=new GameObject("Native match camera");cam.transform.SetParent(transform);cameraView=cam.AddComponent<Camera>();cam.AddComponent<AudioListener>();
                // HDR explicitly (Unity's default, which the additive effects and the damage screen already assume) and the bloom,
                // added with the camera so it is its first image effect, ahead of the damage screen's (NativeBloom.cs).
                cameraView.allowHDR=true;bloom=NativeBloom.Attach(cameraView);
                // The anime map style's outline pass (NativeToonOutline: a command buffer, not an image effect; off unless a map's look turns it on).
                outline=NativeToonOutline.Attach(cameraView);
                cameraView.cullingMask=~(1<<10);NativeLighting.Create(transform);
                var model=new GameObject("First-person view");model.transform.SetParent(cam.transform,false);viewModelCamera=model.AddComponent<Camera>();
                viewModelCamera.clearFlags=CameraClearFlags.Depth;viewModelCamera.cullingMask=1<<10;viewModelCamera.depth=cameraView.depth+1;viewModelCamera.nearClipPlane=.01f;viewModelCamera.farClipPlane=10;viewModelCamera.enabled=false;presentation.ViewCamera=viewModelCamera;
                cameraView.nearClipPlane=.06f;cameraView.farClipPlane=200;NativeLighting.Apply(content.Map(PreviewMapId),cameraView);
                cameraView.transform.SetPositionAndRotation(new Vector3(0,22,-35),Quaternion.Euler(30,0,0));ApplySettings();InitializeUpdater();
            }
            catch(Exception e){error=e.ToString();Debug.LogException(e);if(dedicated)Application.Quit(2);}
        }
        private void Save()
        {
            try{NativeFileStore.Write(PreferencePath,JsonUtility.ToJson(prefs,true));}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){networkStatus="Could not save pilot preferences: "+e.Message;Notice(networkStatus);}
        }
        private void StartMatch()
        {
            if(PromotionBusy||!ModsReady)return;
            if(trainingRoom&&!trainingWanted){StartTraining();return;}
            if(!trainingWanted&&prefs.pve&&prefs.expedition){StartExpeditionM1();return;}
            expeditionDirector=null;ResetExpeditionNetwork();Time.timeScale=1;sandboxSpeed=1;endFlow.Reset();
            if(trainingWanted){prefs.pve=false;prefs.bots=0;}
            if(prefs.pve){prefs.map="siren";prefs.bots=Mathf.Clamp(prefs.bots,0,3);}else prefs.bots=Mathf.Clamp(prefs.bots,Online||dedicated?0:1,7);
            if(server!=null)return;if(prefs.pve&&peers.Count(p=>p.Seat!=null&&p.Connection?.Closed==false)>(dedicated?4:3)){networkStatus="Siren co-op supports up to four party members.";return;}if(Hosting&&!GuestsReady){networkStatus="Wait for the other pilots to be ready.";return;}
            // Refuse a match that cannot start (a host alone with no bots, or every pilot on one team) before the running arena
            // is touched, so the reason shows in the menu instead of the fatal error screen.
            {var sides=new System.Collections.Generic.HashSet<int>();int n=0;if(!dedicated){sides.Add(prefs.pve?0:prefs.teams?prefs.team:1);n++;}
             foreach(var p in peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false)){sides.Add(prefs.pve?0:prefs.teams?p.Seat.team:p.Seat.id);n++;}
             foreach(var bot in LobbyBots(n)){sides.Add(prefs.pve?0:prefs.teams?bot.team:bot.id);n++;}if(prefs.pve){sides.Add(1);n++;}
             if(!trainingWanted&&(n<2||sides.Count<2)){networkStatus=n>=2?"Both teams need a pilot: add a bot or change teams.":"Add a bot or wait for a pilot to join.";return;}}
            if(!trainingWanted&&!prefs.pve&&prefs.map=="breakwater"){var seats=new System.Collections.Generic.List<NativeSeat>();if(!dedicated)seats.Add(new NativeSeat{id=1,team=prefs.team});seats.AddRange(peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).Select(p=>p.Seat));seats.AddRange(LobbyBots(seats.Count));if(!NativeTrainingRules.RoomFits("breakwater",seats,prefs.teams)){networkStatus="Breakwater supports up to 4 pilots: FFA, 1v1 or 2v2.";return;}}
            if(!dedicated)Save();EndPreview();inspectSeat=0;ClearReplay();arena?.Dispose();presentation?.ResetPresentation();networkEpoch++;
            match=new NativeMatch(content){Training=trainingWanted,FinalKillcams=Online&&!trainingWanted,ShowIntros=!trainingWanted&&!prefs.skipPlayerIntros&&!sandboxWanted,Cooperative=prefs.pve,Teams=trainingWanted||prefs.pve||prefs.teams,RoundsPerSet=prefs.rounds,SetsToWin=prefs.sets,SkipBotDrafts=prefs.skipBotDrafts,BothDraft=prefs.bothDraft,Choices=prefs.choices,SelectionSeconds=prefs.selectionSeconds};
            if(!dedicated){var local=SanitizedSeat(1,Profile(),account.Token);local.team=prefs.pve?0:prefs.team;match.Seats.Add(local);}
            foreach(var peer in peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false)){peer.Seat.devWeaponAccess=null;peer.Seat.devRevealed=peer.Seat.devCommandsRan=false;peer.Seat.devScale=1;peer.Seat.team=prefs.pve?0:peer.Seat.team;match.Seats.Add(peer.Seat);peer.Ready=false;}
            if(trainingWanted){foreach(var s in match.Seats)s.team=0;NativeTrainingRules.AddDummies(match,content.Map("training"));}else match.Seats.AddRange(LobbyBots(match.Seats.Count));
            if(prefs.pve)match.Seats.Add(new NativeSeat{id=900,name="The Siren",spec="siren",bot=true,team=1,AuthorityBoss=true});
            arena=new NativeArena(content,match,trainingWanted?"training":SelectMatchMap()){RandomizeMapEachSet=!trainingWanted&&prefs.randomizeMap,FriendlyFire=!trainingWanted&&prefs.friendlyFire,LocalId=dedicated?0:1,StationaryBots=prefs.stationary,Sandbox=trainingWanted||sandboxWanted&&account.Developer&&!Online&&!prefs.pve};
            if(!match.Start()){arena.Dispose();arena=null;match=null;foreach(var peer in peers)peer.Ready=true;networkStatus="A match needs at least two opposing sides.";LobbyBroadcast();return;}
            if(prefs.pve)arena.ConfigurePve(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text),prefs.skipWaves,NativeMatch.NewSeed());
            if(dedicated){menu=false;return;}generation=arena.Generation;visualClock.Reset(match.Time);pending=new InputFrame();yaw=arena.Local.Yaw;pitch=12;sprint=false;menu=false;NativeLighting.Apply(content.Map(arena.MapId),cameraView);
        }
        private float shakeAmount;private int shakeEvent,shakeGeneration=-1;
        private void Update(){long at=System.Diagnostics.Stopwatch.GetTimestamp();try{UpdateFrame();}finally{hitchUpdate+=HitchMs(at);}}
        private void UpdateFrame()
        {
            SyncDevelopmentConsole();
            if(content==null)return;if(dedicated){DedicatedTick();return;}if(cameraView==null)return;SyncFullscreen();NetworkTick();AccountXpTick();
            if(SplitLocalLoading){pending=new InputFrame();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
            // The main menu is always open without a match (an Escape there steps back instead), so the updater's install gate holds.
            if(arena==null)menu=true;if(!menu)pauseScreen=PauseScreen.Menu;
            // A bound Expedition session's screen: J toggles it, Escape closes it (NativeExpeditionLiveUI.cs).
            if(ExpeditionLiveKeys())return;
            // Escape waits while the stage loading screen closes or is closed (NativeStageLoadingUI.cs).
            bool escape=Input.GetKeyDown(KeyCode.Escape)&&!StageInputHeld;
            if(escape&&previewArena!=null){EndPreview();return;}if(escape&&inspectSeat!=0){inspectSeat=0;return;}if(escape&&ReplayActive){EndReplay();return;}if(escape){if(arena==null)MenuBack();else if(PvePickOpen)PveOpen(false);else if(menu&&pauseScreen!=PauseScreen.Menu){if(pauseScreen==PauseScreen.Settings){ApplySettings();Save();}pauseScreen=PauseScreen.Menu;}else menu=!menu;pending=new InputFrame();pending.CancelAbilities=true;if(server==null)arena?.CancelLocalAbilities();}
            // M mutes anywhere and V switches the camera during a match (main.ts keydown), neither while a text field has focus
            // or the board is open; both confirm with a notice. V toggles first person and the chosen third-person camera; X
            // swaps the shoulder (NativeShoulderCamera.cs).
            bool typing=GUIUtility.keyboardControl!=0;
            if(Input.GetKeyDown(KeyCode.M)&&!typing&&!BoardOpen){prefs.sound=!prefs.sound;if(nativeAudio!=null)nativeAudio.Muted=!prefs.sound;Save();Notice(prefs.sound?"Sound on · M to mute":"Sound muted · M to unmute");}
            if(Input.GetKeyDown(KeyCode.V)&&arena!=null&&!typing&&!BoardOpen&&!(arena.ViewPilot?.Seat.spec=="siren"&&prefs.sirenMode=="V"&&!menu)){prefs.firstPerson=!prefs.firstPerson;Save();Notice(CameraName+" · V to switch");}
            CameraKeys(typing);DevUpdate(typing);
            // C in an Expedition opens its chip screen instead (ExpeditionLiveKeys, NativeExpeditionChipScreen.cs).
            if(Input.GetKeyDown(KeyCode.C)&&arena?.Pve!=null&&LiveExpedition==null){if(PvePickOpen)PveOpen(false);else PveOpen(true);}
            if(Input.GetKeyDown(KeyCode.F9))presentation.ForceCompatibilityRendering=!presentation.ForceCompatibilityRendering;
            bool play=arena!=null&&!menu&&!BoardOpen&&!ExpeditionScreenOpen&&(match.Phase==NativePhase.Fight||match.ResultMovement&&!ReplayActive)&&arena.Local.Hp>0&&!PvePickOpen&&arena.Pve?.Holding!=true&&!DevHoldsPilot;
            // The Expedition chip pick frees the cursor to click a card for the whole hold, no Alt (NativeExpeditionChipScreen.cs).
            bool look=!BoardOpen&&!ExpeditionScreenOpen&&DevLook(play||arena?.Local?.Hp<=0&&!menu&&!ReplayActive&&match.Phase==NativePhase.Fight)&&!ExpeditionPickPointer;Cursor.lockState=look?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!look;
            if(arena==null)return;
            if(generation!=arena.Generation){presentation.ResetPresentation();generation=arena.Generation;visualClock.Reset(match.Time);}
            SpectatorControls();var p=arena.ViewPilot;
            UpdateLock();if(play)
            {
                var ads=content.Ads(p.Seat.spec);bool aiming=NativeRangedRules.Scope(p.Seat.spec)&&ads!=null&&Input.GetKey(KeyCode.Q);
                // Choosing in an Expedition holds the camera (ExpeditionCameraHeld, PR #241) and takes the mouse as the chip cursor (no Alt);
                // mouse look waits a couple of frames after the pointer locks again (NativeExpeditionChipScreen.cs).
                if(!lockPoint.HasValue&&!ExpeditionLookSettling&&!cinematic.Directing&&!ExpeditionCameraHeld&&!ExpeditionPickPointer){yaw+=Input.GetAxisRaw("Mouse X")*2*prefs.sensitivity*(aiming?(float)ads.sens:1);pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*prefs.sensitivity*(aiming?(float)ads.sens:1),-70,70);}
                if(Input.GetKeyDown(KeyCode.W)){if(Time.unscaledTime-lastW<.3f)sprint=true;lastW=Time.unscaledTime;}if(!Input.GetKey(KeyCode.W))sprint=false;
                var move=Quaternion.Euler(0,yaw,0)*new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),0,(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)).normalized;
                pending.X=move.x;pending.Z=move.z;pending.Yaw=yaw;pending.Pitch=-pitch;pending.Sprint=sprint;pending.Aim=aiming;
                pending.Shoot=Input.GetMouseButton(0);pending.Attack|=Input.GetMouseButtonDown(0);pending.LockTarget=lockPoint.HasValue?lockTarget:0;
                pending.AbilityQ|=NativeRangedRules.KeyboardQ(content,p.Seat.spec,Input.GetKeyDown(KeyCode.Q));pending.AbilityE|=Input.GetKeyDown(KeyCode.E);pending.AbilityEReleased|=Input.GetKeyUp(KeyCode.E);pending.AbilityEHeld=Input.GetKey(KeyCode.E);
                pending.Hadoken|=Input.GetKeyDown(KeyCode.Alpha1);pending.HadokenHeld=Input.GetKey(KeyCode.Alpha1);pending.Uppercut|=Input.GetKeyDown(KeyCode.Alpha2);
                for(int key=0;key<(arena.Local?.Seat.spec=="cyberlord"?5:4);key++)if(Input.GetKeyDown(KeyCode.Alpha1+key))pending.SirenAbility=(byte)(key+1);
                pending.AbilityQ|=p.Seat.spec=="emperor"&&Input.GetKeyDown(KeyCode.Q)||p.Seat.spec=="siren"&&Input.GetKeyDown(prefs.sirenMode=="F"?KeyCode.F:prefs.sirenMode=="V"?KeyCode.V:KeyCode.Q);
                pending.Interact|=Input.GetKeyDown(KeyCode.F)&&!(p.Seat.spec=="siren"&&prefs.sirenMode=="F");pending.Block|=Input.GetMouseButtonDown(1);pending.Reload|=Input.GetKeyDown(KeyCode.R);pending.Roll|=Input.GetKeyDown(KeyCode.LeftShift);pending.Jump|=Input.GetKeyDown(KeyCode.Space);
                // The Expedition chip tray takes the digit keys (and the mouse buttons while Alt is held): client routing, no lock.
                ExpeditionPickRoute();
                if(match.ResultMovement||arena.BossVictoryWalk)pending=new InputFrame{X=pending.X,Z=pending.Z,Yaw=yaw,Pitch=-pitch,Sprint=sprint,Jump=pending.Jump};
            }
            else pending=new InputFrame{Yaw=yaw,Pitch=-pitch};
        }
        // The damage screen reads the frame after the live view is placed, so it runs after every early return of LateFrame.
        // The stage loading screen steps last, even when the frame threw (NativeStageLoadingUI.cs): it never sticks on an error.
        private void LateUpdate(){long at=System.Diagnostics.Stopwatch.GetTimestamp();try{try{LateFrame();DamageScreen();GlitchScreen();}finally{FinishHitchFrame(HitchMs(at));}}finally{StageLoadingStep();}}
        private void LateFrame()
        {
            // A bound Expedition session's world pieces (NativeExpeditionWorldPresentation.cs): one mesh drawn for this frame.
            // Settings · Player model: Classic draws the voxel pilot body in place of the new one, set before any path draws a
            // pilot (previews, replays, the draft and the intros return before the arena renders).
            NativeUiBlur.Frame();ExpeditionWorldFrame();if(cameraView==null)return;if(SplitLocalLoading){viewModelCamera.enabled=false;return;}cinematic.BeginFrame();presentation.ClassicBodies=prefs.playerModel=="Classic";presentation.LocalLook=LookShown();presentation.VisualEye=cameraView;if(PreviewFrame())return;long replayAt=System.Diagnostics.Stopwatch.GetTimestamp();ObserveReplay();hitchReplay+=HitchMs(replayAt);if(arena!=null&&RenderReplay())return;nativeAudio.Muted=!prefs.sound;nativeAudio.Entrance(arena!=null&&!menu&&IntroActive?arena.Generation+":"+IntroPilot?.Seat.id+":"+IntroStyle:null,arena!=null&&IntroActive?IntroStyle:null,arena!=null&&IntroActive?IntroAge:0);PveFollowAudio();ExpeditionPlantAudio();nativeAudio.HulkWarning=presentation.HulkWarning;nativeAudio.Render(arena,menu,cameraView);SlenderFrame();if(arena==null){MenuBackdrop();return;}RooftopLeave();backdropMap=null;
            if(DraftFrame())return;if(!menu&&IntroActive){arena.Root.SetActive(false);viewModelCamera.enabled=false;presentation.WeaponEffectsQuality=prefs.reducedEffects?.3f:1;presentation.RenderIntro(arena,IntroPilot,IntroStyle,IntroClockAge(),cameraView);return;}introClock.Reset();NativeLighting.EndIntro(content.Map(arena.MapId),cameraView);arena.Root.SetActive(true);
            var p=arena.ViewPilot;bool play=!menu&&match.Phase==NativePhase.Fight&&p.Hp>0&&!PvePickOpen&&arena.Pve?.Holding!=true;
            UpdateCamera();
            if(play)
            {
                pending.AimTarget=NativeArena.Vec(AimPoint((float)p.Weapon.range));
                var ability=content.Ability(p.Seat.spec,pending.AbilityEHeld||pending.AbilityE||pending.AbilityEReleased?"e":"q");
                pending.AbilityTarget=NativeArena.Vec(AimPoint(ability!=null?(float)ability.range:p.Seat.spec=="shield"?25:p.Seat.spec=="daggers"?48:60,p.Seat.spec=="shield"||p.Seat.spec=="daggers"));
                // The shoulder cameras send the look that points the pilot at the crosshair point (NativeShoulderCamera.cs).
                ConvergeAim(p);
            }
            // Held choosing a chip in an Expedition, the camera swings round to face my pilot (NativeChooserCameraPresentation.cs): after
            // the aim above, so the look sent and AimTarget are the gameplay camera's, and before the shakes, which shake the camera shown.
            ChooserCamera();
            // renderer.ts:136/:149 camera shake with event-vfx.ts:29 (NativeShakeRules): a fresh (.4 s) hit, impact, break, block or
            // collapse aimed at the view pilot, never a burn/poison tick, sets .12 while Settings · camera shake is on. The amount
            // falls by the frame time (at most .05 s) in every frame, so a shake held back never plays late; only a live local
            // view (not the pause menu, not a spectated pilot) is nudged on world X by a random ±half of it. Events are consumed
            // in every frame, as the source's renderer does. The killing hit shakes the death cam only when no other connected
            // pilot is alive: otherwise SpectatorControls has already moved the view to one in that frame.
            {
                if(arena.Generation!=shakeGeneration){shakeGeneration=arena.Generation;shakeEvent=0;shakeAmount=0;}
                int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest>0&&newest<shakeEvent)shakeEvent=0;
                foreach(var e in arena.Events)if(e.Id>shakeEvent&&prefs.cameraShake&&NativeShakeRules.Fresh(arena.Time,e.Time)&&NativeShakeRules.Triggers(e.Kind,e.Target,e.Amount,p.Seat.id))shakeAmount=NativeShakeRules.Amount;
                shakeEvent=Math.Max(shakeEvent,newest);
                if(shakeAmount>0){if(!menu&&Spectator==null&&!cinematic.Directing)cameraView.transform.position+=Vector3.right*NativeShakeRules.Offset(shakeAmount,UnityEngine.Random.value);shakeAmount=NativeShakeRules.Decay(shakeAmount,Time.unscaledDeltaTime);}
                // Structure stress, falls and landings shake by distance (Core NativeStructureShake, within 25 m) under the same rules.
                if(prefs.cameraShake&&!menu&&Spectator==null&&!cinematic.Directing)cameraView.transform.position+=presentation.StructureShake(cameraView.transform.position,prefs.reducedEffects?.5f:1);
                // The Siren brutes' slams and jump-down landings shake a nearby view, and kick one in the smash's disc (NativeHordeZombiePresentation.cs).
                if(prefs.cameraShake&&!menu&&Spectator==null&&!cinematic.Directing)presentation.HordeShake(cameraView.transform,prefs.reducedEffects?.5f:1);
                if(prefs.cameraShake&&!menu&&Spectator==null&&!cinematic.Directing)presentation.StationBossShake(cameraView.transform,prefs.reducedEffects?.5f:1);
                SprintSway();
            }
            // The DEV fly camera (NativeDevPanel.cs) replaces the view after the pilot camera and its shakes; presentation only.
            DevCamera();
            presentation.SoundEnabled=false;presentation.LocalId=arena.ViewPilot.Seat.id;
            float renderTime=arena.Replica?arena.ReplicaRenderTime:visualClock.Sample(match.Time,Time.timeAsDouble-Time.fixedTimeAsDouble,menu);
            long snapshotAt=System.Diagnostics.Stopwatch.GetTimestamp();var frame=arena.Snapshot(true);hitchSnapshot+=HitchMs(snapshotAt);
            // Melee hit kick (Core/NativeHitReact): first person, a live local view and Settings · camera shake on (reduced
            // with reduced effects); applied after the aim point above, and the view is rebuilt from the look angles next frame.
            if(!menu&&Spectator==null&&!cinematic.Directing&&presentation.FirstPerson&&prefs.cameraShake){presentation.ObserveHits(frame);presentation.HitKick(cameraView.transform,frame,arena.ViewPilot.Seat.id,renderTime,prefs.reducedEffects?.4f:1);}
            // The block effect's clash hitch (Core/NativeBlockFx.Hitch): the blocker's own view, first or third person, camera shake on.
            if(!menu&&Spectator==null&&!cinematic.Directing&&prefs.cameraShake)presentation.BlockHitch(cameraView.transform,frame,arena.ViewPilot.Seat.id,renderTime,prefs.reducedEffects?.4f:1);
            // The gun Q kit's first-person kick (Core/NativeGunKitMotion.CameraKick): fanned rounds, Overdrive's buzz; camera shake on.
            if(!menu&&Spectator==null&&!cinematic.Directing&&prefs.cameraShake)presentation.GunKitKick(cameraView.transform,frame,arena.ViewPilot.Seat.id,renderTime,prefs.reducedEffects?.4f:1);
            // The match-start spawn-in (NativeSpawnInPresentation.cs): my first-person view rides the beam's hold and drop, and the touchdown kicks.
            if(!menu&&Spectator==null&&!cinematic.Directing)presentation.SpawnCamera(cameraView.transform,arena,renderTime,prefs.cameraShake,prefs.reducedEffects);
            presentation.WeaponLoadout=arena;presentation.WeaponEffectsQuality=prefs.reducedEffects?.3f:1;presentation.CameraShake=prefs.cameraShake;
            // A PvE revive's first-person view (NativeReviveHoloPresentation.ReviveView): a gentle rise, none with reduced effects or
            // camera shake off, in a scanline vignette; after the aim above, so the aim and the authority eye never move.
            if(!menu&&Spectator==null&&!cinematic.Directing)presentation.ReviveView(cameraView,arena.ViewPilot.Seat.id,renderTime,prefs.reducedEffects||!prefs.cameraShake);
            presentation.Render(frame,renderTime,false);ShoulderSprint(renderTime);
            Vector3? preview=null;bool previewClamped=false;
            // previewOn: E held .2 s and casting, or ready (a charge or no cooldown left) and, for magic, affordable. Casting is
            // read from the pose (PressAbility sets it, CancelWindup clears it), which guests also receive.
            var held=play&&p.HoldAt>=0&&arena.Time-p.HoldAt>=NativeFieldRules.HoldAimSeconds?content.Ability(p.Seat.spec,"e"):null;
            if(held!=null&&NativeFieldRules.PreviewOn(p.Pose.AbilityId==held.id&&arena.Time<p.Pose.AbilityRelease,p.ECharges>0?0:p.EReady-arena.Time,p.Weapon.@class=="magic",p.Mana,NativeAbilityRules.Cost(held,p.Stats),p.Seat.chips.TryGetValue("necronomicon",out int tome)&&tome>0,arena.Time-p.HoldAt))
            {
                Vector3 point=NativeArena.Vec(pending.AbilityTarget);
                // An out-of-range (sky) or arena-clamped aim draws the source's gapped preview.
                previewClamped=(point-p.Body.position).sqrMagnitude>(float)(held.range*held.range);
                point=p.Body.position+Vector3.ClampMagnitude(point-p.Body.position,(float)held.range);
                var clamped=arena.ClampMap(point);previewClamped|=clamped!=point;point=clamped;
                if(held.kind!="throw"&&arena.PhysicsWorld.Raycast(point+Vector3.up*.08f,Vector3.down,out var ground,40,NativeArena.WorldMask))point=ground.point;
                preview=point;
            }
            presentation.EffectsQuality=prefs.reducedEffects?.3f:1;presentation.KitFlash=prefs.alarmLights=="Calm"?NativeKitFlash.Calm:prefs.alarmLights=="Reduced"||prefs.reducedEffects?NativeKitFlash.Reduced:NativeKitFlash.Full;
            long effectsAt=System.Diagnostics.Stopwatch.GetTimestamp();presentation.RenderAbilities(arena,preview,previewClamped);hitchAbilities+=HitchMs(effectsAt);
            long statusAt=System.Diagnostics.Stopwatch.GetTimestamp();presentation.RenderStatus(arena,cameraView,prefs.reducedEffects?.3f:1);hitchStatus+=HitchMs(statusAt);
            long glowAt=System.Diagnostics.Stopwatch.GetTimestamp();
            presentation.GlowQuality=prefs.reducedEffects?.3f:prefs.combatGlow=="Off"?0:prefs.combatGlow=="Low"?.4f:1;presentation.BlastQuality=prefs.reducedEffects?.3f:1;presentation.RenderGlow(arena,cameraView,menu&&!Online);presentation.FinishSourceFrame();hitchGlow+=HitchMs(glowAt);hitchEffects+=HitchMs(effectsAt);
        }
        // No match: the home screen's rooftop over the cyber city (NativeRooftopPresentation.cs; owner 2026-10-12: the home
        // screen only), else the selected map behind the menu with the source's slow overview orbit (renderer.ts idle camera),
        // as Play vs Bots, the room screens, Boss Test and the others keep. The rooftop writes its own camera framing and night
        // look; leaving it gives the camera back its field of view and near plane and re-applies the map look.
        private string backdropMap;private bool rooftopShown;private float rooftopFov,rooftopNear;
        private void MenuBackdrop()
        {
            if(menuScreen==MenuScreen.Home&&!InRoom&&presentation!=null&&presentation.RooftopSupported)
            {
                if(!rooftopShown){rooftopShown=true;rooftopFov=cameraView.fieldOfView;rooftopNear=cameraView.nearClipPlane;}
                backdropMap=null;viewModelCamera.enabled=false;float quality=presentation.LowGraphics?.34f:prefs.reducedEffects?.6f:1;
                string bodySkin=BodySkinShown();var pilot=new RooftopPilot{Spec=prefs.weapon,BodySkin=NativeBodySkins.Index(bodySkin),Hat=prefs.hat,Body=prefs.bodyColor,Skins=RooftopSkins(bodySkin),Accent=prefs.accentColor,HatColor=prefs.hatColor};
                if(presentation.RenderRooftop(cameraView,(float)(Time.unscaledTimeAsDouble%4800.0),pilot,quality,prefs.reducedEffects))return;
            }
            RooftopLeave();
            string map=PreviewMapId;var definition=content.Map(map);if(definition==null||presentation==null)return;
            if(backdropMap!=map){backdropMap=map;NativeLighting.Apply(definition,cameraView);}
            viewModelCamera.enabled=false;ArenaPresentation.OverviewCamera(cameraView,Time.unscaledTime);presentation.RenderBackdrop(content.sourceCommit,map,Time.unscaledTime,cameraView);
        }
        private void RooftopLeave(){if(!rooftopShown)return;rooftopShown=false;cameraView.fieldOfView=rooftopFov;cameraView.nearClipPlane=rooftopNear;backdropMap=null;if(presentation!=null)presentation.RooftopHide();}
        // The seated pilot's skin map on the model (your skins with the body skin the menu shows), made again only when either changes.
        private string rooftopSkinsIn,rooftopSkinsBody,rooftopSkinsOut;
        private string RooftopSkins(string body){if(!ReferenceEquals(rooftopSkinsIn,prefs.weaponSkins)||rooftopSkinsBody!=body){rooftopSkinsIn=prefs.weaponSkins;rooftopSkinsBody=body;rooftopSkinsOut=NativeWeaponSkins.With(prefs.weaponSkins,NativeBodySkins.Entry,body);}return rooftopSkinsOut;}
        // Where the seated pilot's feet hang over the lip, on the virtual canvas (the home screen's nameplate follows them), while
        // the rooftop is shown and the point is in front of the camera.
        private bool RooftopFeet(out Vector2 point)
        {
            point=default;if(!rooftopShown||cameraView==null)return false;var s=cameraView.WorldToScreenPoint(new Vector3(0,.25f,-2f));if(s.z<=0)return false;
            point=new Vector2(s.x/NativeUi.S,(Screen.height-s.y)/NativeUi.S);return true;
        }
        private void FixedUpdate(){long at=System.Diagnostics.Stopwatch.GetTimestamp();try{FixedFrame();}finally{hitchFixed+=HitchMs(at);hitchTicks++;}}
        private void FixedFrame()
        {
            if(arena==null||menu&&!Online)return;if(arena.Replica){if(StageGate.Waiting||ExpeditionGuestBlocked){pending=pending.HeldOnly();return;}if(server!=null){pending.Sequence=++inputSequence;SendServer(new NativeNetMessage{type="input",input=pending});arena.PredictReplica(pending);pending=pending.HeldOnly();}return;}if(ExpeditionSplit){SplitFixedFrame();return;}if(!StageAuthorityReady()||!ExpeditionM1BeforeStep())return;timer.Restart();bool held=arena.Pve?.Holding==true;
            double nearestBefore=arena.NavigationNearestMs,linksBefore=arena.NavigationLinkMs;int linkChecksBefore=arena.NavigationLinkChecks;
            double navBefore=arena.NavigationMs,deathBefore=arena.DeathRecordMs,hordeBefore=arena.HordeMs,spawnBefore=arena.HordeSpawnMs;int searchesBefore=arena.NavigationSearches;
            long stepAt=System.Diagnostics.Stopwatch.GetTimestamp();
            try{arena.Step(pending);}finally{hitchArenaStep+=HitchMs(stepAt);hitchNavigation+=arena.NavigationMs-navBefore;hitchNavNearest+=arena.NavigationNearestMs-nearestBefore;hitchNavLinks+=arena.NavigationLinkMs-linksBefore;hitchNavLinkChecks+=arena.NavigationLinkChecks-linkChecksBefore;hitchNavSearches+=arena.NavigationSearches-searchesBefore;hitchDeathRecord+=arena.DeathRecordMs-deathBefore;hitchHorde+=arena.HordeMs-hordeBefore;hitchHordeSpawn+=arena.HordeSpawnMs-spawnBefore;}
            if(!held&&arena.Pve?.Holding!=true){long physicsAt=System.Diagnostics.Stopwatch.GetTimestamp();try{arena.BeforePhysics();arena.PhysicsWorld.Simulate(1f/60);arena.AfterPhysics();}finally{hitchPhysics+=HitchMs(physicsAt);}}
            ExpeditionM1AfterStep();timer.Stop();stepMs=timer.Elapsed.TotalMilliseconds;
            pending=pending.HeldOnly();
        }
        private Vector3 AimPoint(float range,bool thrownMelee=false)
        {
            Ray ray=cameraView.ViewportPointToRay(new Vector3(.5f,.5f));float nearest=range+(thrownMelee?0:Vector3.Distance(ray.origin,arena.ViewPilot.Body.position));
            Vector3 target=ray.origin+ray.direction*(thrownMelee?48:nearest);
            foreach(var hit in NativeSceneQueries.RaycastAll(arena.PhysicsWorld,ray,nearest,NativeArena.ShotMask,QueryTriggerInteraction.Ignore))
            {var pilot=hit.collider.GetComponent<NativePilotBody>();if(pilot!=null&&pilot.Pilot==arena.ViewPilot||hit.distance<aimSkip)continue;if(hit.distance<nearest){target=hit.point;nearest=hit.distance;}}
            return target;
        }
        private void UpdateCamera()
        {
            // The gameplay field of view under last frame's chooser shot comes back first (NativeChooserCameraPresentation.cs).
            ChooserCameraRestore();
            // C16: the Last Laugh cutscene directs this camera for the pilots in it (the demon victim screen has its own).
            if(cinematic.Direct(arena,presentation,cameraView,match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result)){viewModelCamera.enabled=false;sprintKick=0;shoulderView=convergeAim=false;aimSkip=0;return;}
            var p=Spectator??arena.ViewPilot;Vector3 focus=p.Body.transform.position+Vector3.up*NativeViewRules.EyeHeight;var ads=content.Ads(p.Seat.spec);
            // Camera intent survives stagger/knockdown. The simulation independently gates ADS benefits and shots.
            bool alive=arena.Local.Hp>0&&p.Hp>0;
            bool aim=NativeRangedRules.Scope(p.Seat.spec)&&ads!=null&&pending.Aim&&alive&&!menu;
            // ArenaPresentation.Render already hides the viewed pilot's world body in first person.
            bool first=NativeViewRules.FirstPerson(prefs.firstPerson||aim,alive,menu);
            // Settings · Camera (NativeShoulderCamera.cs): Classic, once settled, is the original camera below, unchanged.
            bool classic=CameraClassic(first);
            cameraView.fieldOfView=SprintFov(first,aim,Mathf.Lerp(cameraView.fieldOfView-sprintKick,aim?(float)ads.fov:first?NativeViewRules.FirstFov:classic?NativeViewRules.ThirdFov:cameraRig.Fov(Time.unscaledTime),1-Mathf.Exp(-16*Time.unscaledDeltaTime)));
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);Vector3 location=focus;
            if(!first&&!classic)location=ShoulderCamera(p,focus);
            else if(!first)
            {
                Vector3 offset=rotation*Vector3.back*NativeViewRules.ThirdDistance+Vector3.up*NativeViewRules.ThirdLift;
                location=focus+offset;
                if(arena.PhysicsWorld.SphereCast(focus,.12f,offset.normalized,out var hit,offset.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))location=focus+offset.normalized*Mathf.Max(.15f,hit.distance-NativeViewRules.CollisionPadding);
            }
            cameraView.transform.SetPositionAndRotation(location,rotation);CameraAim(p);
            presentation.FirstPerson=first;presentation.AimHeld=aim;viewModelCamera.enabled=first;viewModelCamera.fieldOfView=cameraView.fieldOfView-sprintKick;
        }
        private string AbilityStatus(NativePilot p,string slot)
        {
            var d=content.Ability(p.Seat.spec,slot);
            if(d==null)
            {
                if(p.Weapon.@class!="melee")return "";
                if(slot=="q")return "LMB  "+(p.Weapon.cost*p.Stats["resourceCost"]).ToString("0.#")+" stamina  ·  Shift cancels primary";
                string specialState=p.Special!=null?"active":p.EReady>arena.Time?(p.EReady-arena.Time).ToString("0.0")+"s":p.Stamina<p.Weapon.secondaryCost*p.Stats["resourceCost"]?"low stamina":"ready";
                string name=p.Seat.spec=="sword"?"Rising slash":p.Seat.spec=="daggers"?"Dagger throw":p.Seat.spec=="shield"?"Shield throw":"Wolf leap";
                return "E  "+name+" · "+specialState;
            }
            // The pistol / machine-gun Q kit (Core/NativeGunKitHud): fanning, active time, venting, cooldown, ammo.
            if(slot=="q"&&NativeGunKit.Covers(d.id))return NativeGunKitHud.Status(d.name,NativeGunKitHud.Of(d.id,NativeGunKit.Of(p.Pose,arena.Time),arena.Time,p.QReady-arena.Time,(float)NativeAbilityRules.Cooldown(d,p.Stats),p.Ammo,p.ReloadEnd>arena.Time));
            if(d.kind=="ads")return "Q  Aim (hold)";
            if(NativeMeleeRules.Standard(p.Seat.spec))
            {
                var card=NativeMeleeKitHud.Of(slot,d.id,p.MeleeKit,(slot=="q"?p.QReady:p.EReady)-arena.Time,NativeAbilityRules.Cooldown(d,p.Stats),p.Stamina,NativeAbilityRules.Cost(d,p.Stats),arena.Time);
                return slot.ToUpper()+"  "+d.name+" · "+card.State.ToLowerInvariant();
            }
            float left=slot=="q"?p.QReady-arena.Time:p.ECharges>0?0:p.EReady-arena.Time;
            string state=p.AbilityCast?.Definition==d?"casting":left>0?left.ToString("0.0")+"s":p.Mana<NativeAbilityRules.Cost(d,p.Stats)?"low mana":"ready";
            return slot.ToUpper()+"  "+d.name+" · "+state;
        }
        private void Styles()
        {
            GUI.skin=NativeMenuSkin.Skin;if(heading!=null)return;var skin=NativeMenuSkin.Skin;heading=new GUIStyle(skin.label){fontSize=24,fontStyle=FontStyle.Bold,wordWrap=true};heading.normal.textColor=NativeUi.Hex(0xf4f4f5);
            label=new GUIStyle(skin.label){fontSize=17,wordWrap=true};small=new GUIStyle(skin.label){fontSize=14,wordWrap=true};small.normal.textColor=NativeUi.Hex(0xbfc0c9);
            // Buttons grow with their label (the old fixed 36 px height clipped two-line labels).
            button=new GUIStyle(skin.button){fontSize=16,wordWrap=true,fixedHeight=0,stretchHeight=false};button.padding=new RectOffset(14,14,9,9);
        }
        private void OnGUI()
        {
            // The stage loading screen holds the GUI's input while it closes and while it is closed; it draws over every screen.
            if(dedicated)return;if(SlenderGUI())return;Styles();StageInputGate();GuiFrame();if(error.Length==0)NoticeToast();BuildCorner();ExpeditionTransferGui();StageLoadingGui();
        }
        private void GuiFrame()
        {
            // One canvas for every overlay and the menus (NativeUiMotion.Measure): 900 units tall at 16:9, wider at 21:9 (2150 at 3440x1440).
            var ui=NativeUi.Begin(prefs?.uiScale??1);float scale=ui.Scale,width=ui.Width,height=ui.Height;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            if(SplitLocalLoading)return;
            if(error.Length>0){GUI.Box(new Rect(30,30,width-60,height-60),"");GUI.Label(new Rect(45,45,width-90,height-90),error,label);return;}
            // DEV ONLY: the Expedition screens preview (--ca-expedition-preview), in place of the main menu (NativeExpeditionPreviewUI.cs).
            if(ExpeditionPreviewGui(width,height))return;
            // A bound Expedition session's screen (J), in place of the frame while open; nothing draws without a binding.
            if(ExpeditionLiveGui(width,height))return;
            if(match?.Phase!=NativePhase.Match)matchEndAt=-1;if(arena!=null)menuFromMatch=true;
            // DEV hide-HUD / screenshot mode (F11, NativeDevPanel.cs): nothing over the match.
            if(DevScreenshot())return;
            // The intro overlay replaces the whole HUD (main.ts drawHud), so nothing else draws while it runs.
            if(arena!=null&&!menu&&previewArena==null&&IntroActive){IntroOverlay();return;}
            // The killcam is a broadcast frame of the past: the live HUD steps aside while it plays (killcam.css).
            if(arena!=null&&!menu&&previewArena==null&&ReplayActive){ReplayHud(width,height);return;}
            // The chip art's layers warm on a worker once a match is on screen (NativeChipArt.Warm), ready before the first draft.
            if(arena!=null&&Event.current.type==EventType.Repaint)NativeChipArt.Warm();
            // Chip selection is a full screen: the PvP draft, and the Siren personal pick (main HUD hidden while it is open).
            if(arena!=null&&!menu&&previewArena==null&&match.Phase==NativePhase.Draft){DraftScreen();return;}
            if(previewArena==null&&PvePickScreen){PveChipScreen();return;}
            if(previewArena==null&&SirenRevealActive){SirenReveal();return;}
            // The Last Laugh cutscene hides the HUD behind its letterbox (style.css data-cutscene); the board and pause menu stay.
            if(arena!=null&&previewArena==null&&cinematic.Directing){cinematic.Letterbox(width,height);if(cinematic.ZoneArrival)ZoneBossArrivalHud(width,height);else KernelPanicSplash(width,height);if(BoardOpen)Board(width);if(menu){PauseMenu();TrainingControls();}return;}
            if(!cinematic.Private)CombatWorldHud(scale,width,height);
            PveBeaconHud();
            if(arena!=null&&!menu&&match.Phase==NativePhase.Fight&&presentation.ScopeActive)DrawScope(width,height);
            if(previewArena!=null){PreviewHud(width,height);return;}
            if(arena!=null)
            {
                // My own first-person spawn-in screen, under the HUD (NativeSpawnInPresentation.cs).
                if(!menu)SpawnInScreen();
                var p=arena.Local;bool fighting=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Countdown;
                if(!menu&&(fighting||match.Phase==NativePhase.Result))FightHud(p);
                if(!menu&&fighting)KernelPanicBanner();
                if(!menu&&fighting&&p.Hp>0&&!presentation.ScopeActive&&!ChooserFramed)Crosshair();
                if(!menu&&match.Phase==NativePhase.Fight)
                {
                    if(arena.Downed(p))Prompt(U.H/2+80,"RECOVERING  ·  WASD chooses roll direction",14,NativeCyberTheme.Ink());
                    if(arena.RiposteOpen(p))Prompt(U.H/2+40,"RIPOSTE  ·  LMB counter",17,NativeCyberTheme.Warn());
                    // A Kunai mark on me (the marker can teleport behind me while it lasts): every pilot's replicated kit.
                    float marked=0;foreach(var q in arena.Pilots)marked=Math.Max(marked,NativeMeleeKitHud.MarkLeft(q.MeleeKit,p.Seat.id,arena.Time));
                    if(marked>0&&p.Hp>0)Prompt(U.H/2+120,"MARKED  ·  "+marked.ToString("0.0")+"s",15,NativeCyberTheme.Danger());
                }
                if(!menu&&match.Phase==NativePhase.Countdown)CountdownOverlay();
                if(!menu&&match.Phase==NativePhase.Result)ResultOverlay();
                if(!menu&&match.Phase==NativePhase.Match&&arena.Pve==null)MatchEndPanel();
                else if(!menu&&match.Phase==NativePhase.Match)PveResultsPanel();
                if(BoardOpen)Board(width);if(inspectSeat!=0)Inspect(width,height);LockHud(scale);
            }
            // The DEV panel, its view tools and the DEV reveal (NativeDevPanel.cs, NativeDevOverlays.cs).
            if(arena!=null)DevGui(width,height);
            if(!menu&&arena!=null){ReplayHud(width,height);PveHud(width,height);ExpeditionLiveHud(width,height);return;}
            // The menus (NativeHomeMenu): the home screen and its panels without a match, the pause menu during one.
            if(arena!=null){PauseMenu();TrainingControls();}else HomeMenu();
        }
        // Play vs Bots (main.ts:147 practice screen) in the source order: the set line, YOUR CLASS (and the weapon cards),
        // matchOptions() in its box, botLoadouts, the team setup in teams mode, and the "Draft & intro rules" fold (BETWEEN
        // SETS, draftOptions, introOptions); rows from NativeRoomOptionsUI.cs. In a room lobby the host's rules instead.
        private void MatchRulesBody(bool lobby)
        {
            if(lobby){LobbyRulesBody();return;}
            BeginRules(false,578);var o=rules;
            Fine(FirstToText(prefs.sets,prefs.rounds));ClassRow("Your class",ClassCardStyle.Stacked);WeaponCards("Your weapon");
            MatchOptions(RulesPart.All,true,false);int bots=Mathf.Clamp(o.bots,1,7);BotLoadouts(bots,24);if(o.teams)TeamSetup(bots);
            if(RulesFoldBegin(0,"Draft & intro rules"))
            {
                RuleField(7760,"BETWEEN SETS");RuleSelect(7762,RoomOpt.BothDraft,null,BetweenSetsLabels,o.bothDraft?1:0);
                DraftOptions();IntroOptions();RulesFoldEnd(0);
            }
            RuleFine(7766,"Melee E: special attack. Gun Q: hold to aim. Magic Q / E: cast abilities. Hold E to preview grenade, meteor, geysers or black hole; release to cast.",14,6);
        }
        // The room column's rules in the source order (main.ts:156): matchOptions(true,'essential') in its box, then the
        // Advanced Options fold (roomAdvanced) with matchOptions(true,'advanced'), botDraftOption, the host's botLoadouts,
        // the draft summary and the INVITE LINK. A guest gets the same rows read-only from RoomOptions. Skip player intros is
        // here as well (the source sets it only when the room is created; Unity's Create offers it too).
        private void LobbyRulesBody()
        {
            BeginRules(true,lobbyRoomInner+8);var o=rules;if(o==null)return;
            MatchOptions(RulesPart.Essential,false,true);
            if(!AdvancedFold(LobbyRulesNote(o,LobbyBotCount(o))))return;
            rulesL=rulesR=0;rulesEstimate=Mathf.Max(120,lobbyRoomInner-28);
            MatchOptions(RulesPart.Advanced,false,true);
            RuleCheck(7570,RoomOpt.SkipBotDrafts,"Skip bot chip choices",o.skipBotDrafts);RuleFine(7572,"Bots still receive a Chip instantly. Their selection screens and reveals are skipped.",0,6);
            RuleCheck(7574,RoomOpt.SkipIntros,"Skip player intros",o.skipPlayerIntros);RuleFine(7576,"Intros play once per match, before the opening countdown.",0,6);
            if(!rulesLocked)BotLoadouts(LobbyBotCount(o),16);
            RuleFine(7578,DraftNote(o),10,4);
            RoomInviteLink();
            ConnectionReportButton();
            AdvancedEnd();
        }
        // Boss Test (pve/menu.ts testBossBody): Party bots, its note and Start at the Siren (with Unity's "Party bots use my
        // weapon"), then the weapon. In the PvE room lobby the same rows sit under Advanced Options with the invite link
        // (pveLobbyBody), read-only for guests.
        private void BossBody(bool lobby=false)
        {
            if(lobby)
            {
                BeginRules(true,lobbyRoomInner+8);var g=rules;if(g==null)return;
                if(!AdvancedFold(PartyNote(g,rulesLocked)))return;
                rulesL=rulesR=0;rulesEstimate=Mathf.Max(120,lobbyRoomInner-28);
                RuleSelect(7580,RoomOpt.Bots,"Party bots",PartyBotLabels,Mathf.Clamp(g.bots,0,3));
                RuleFine(7584,"AI pilots fill the party (up to 4). A friend who joins replaces the newest bot.",0,6);
                RuleCheck(7586,RoomOpt.Mirror,rulesLocked?"Party bots use the host's weapon":"Party bots use my weapon",g.mirrorWeapons);RuleCheck(7588,RoomOpt.SkipWaves,"Start at the Siren",g.skipWaves);
                RuleFine(7589,"Earn money from minions for chips, heals, revives and recruits. F interacts · C opens your chips.",4,4);
                RoomInviteLink();
                ConnectionReportButton();
                AdvancedEnd();return;
            }
            BeginRules(false,578);var o=rules;
            RuleSelect(7580,RoomOpt.Bots,"Party bots",PartyBotLabels,Mathf.Clamp(o.bots,0,3));
            RuleFine(7584,"AI pilots fill the party (up to 4). A friend who joins replaces the newest bot.",0,6);
            RuleCheck(7586,RoomOpt.Mirror,"Party bots use my weapon (otherwise mixed loadouts)",o.mirrorWeapons);RuleCheck(7588,RoomOpt.SkipWaves,"Start at the Siren",o.skipWaves);
            ClassRow("Your class",ClassCardStyle.Stacked);WeaponCards("Your weapon");
            Fine("Intros play as normal. Earn money from minions to buy chips, heals, revives and recruits. F interacts · C opens your chips; chips you take wait there until you choose.");
        }
        private void WeaponGrid()
        {
            var weapons=content.weapons.Where(AllowedWeapon).ToArray();string pick=ChoiceGrid(weapons.Select(w=>w.id).ToArray(),prefs.weapon,weapons.Select(w=>w.name).ToArray());if(pick!=prefs.weapon){prefs.weapon=pick;if(Hosting&&arena==null)LobbyBroadcast();}
        }
        // ARENA (main.ts matchOptions arena): Random, or an arena (Ward Zero included, its PvP variant; picking one also turns
        // the menu backdrop to it, main.ts previewMap), and the description under it (map-description).
        private void MapGrid()
        {
            var o=rules;RuleField(7464,"ARENA");RuleSelect(7466,RoomOpt.Map,null,mapLabels,MapIndex(o));
            int k=Array.IndexOf(mapIds,o.map);string desc=o.map=="random"?"Choose from all arenas when the match is created.":k>=0?content.Map(o.map).tag:null;RuleFine(7470,desc,rulesNarrow?0:4,rulesNarrow?4:6);
        }
        private void Catalogue()
        {
            Fine("Reference catalogue: the source's weapons and Chips. Importing a definition does not enable its mechanic.");Field("Search");search=GUILayout.TextField(search);
            foreach(var w in content.weapons.Where(w=>(w.name+" "+w.id).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){GUILayout.Label(w.name+(w.devOnly?" — signed unlock required":""),label);GUILayout.Label(NativeLoadoutText.Text(w),small);}
            foreach(var c in content.chips.Where(c=>(c.name+" "+c.description).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){GUILayout.Label(c.name+"  /  "+c.rarity+(NativeArena.ImplementedChip(c)?"  /  draft enabled":"  /  pending"),label);GUILayout.Label(c.description,small);}
        }
        // The pilot board (G / Tab) at the top right, where the source keeps scores: each row opens that pilot's build.
        private void Board(float width)
        {
            if(inspectSeat!=0)return;float W=NativeUi.W,w=360,x=W-24-w,y=62;var seats=match.Seats.Where(s=>!s.AuthorityBoss).ToList();float h=14+20+10+seats.Count*46+10;
            // The pilot board in the cyber HUD chrome: a holographic panel, chamfered rows lifting into a cyan edge on hover.
            NativeHudChrome.Panel(new Rect(x,y,w,h),1,default,NativeHudCyber.Cut,NativeHudChrome.Corners.Diagonal,true,.94f);NativeUi.Text(new Rect(x+14,y+14,w-28,20),arena?.Pve!=null?"PARTY":"PILOTS",12,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            NativeUi.Text(new Rect(x+14,y+14,w-28,20),"Click to inspect a build",11,NativeCyberTheme.Muted(),TextAnchor.MiddleRight);float cy=y+44;
            foreach(var p in seats)
            {
                var r=new Rect(x+10,cy,w-20,40);float hv=NativeUi.HoverAmount(7100+p.id,r);bool you=p.id==arena?.Local?.Seat.id;var pilot=arena?.Pilots.Find(q=>q.Seat==p);bool alive=pilot!=null&&pilot.Hp>0&&p.connected;
                var row=new Rect(r.x,r.y-hv,r.width,r.height);NativeHudChrome.Cut(row,NativeUi.Mix(NativeCyberTheme.Indigo(.9f),NativeCyberTheme.Indigo(),hv),NativeHudCyber.TagCut);NativeHudChrome.Outline(row,NativeUi.Mix(NativeCyberTheme.Edge(.8f),NativeCyberTheme.Cyan(),hv),NativeHudCyber.TagCut);
                NativeUi.Fill(new Rect(r.x,r.y+6-hv,2,r.height-12),match.Teams?TeamColor(p.team):SeatColor(p));
                NativeUi.Text(new Rect(r.x+12,r.y-hv,r.width-24,22),p.name+(you?"  · YOU":""),13,NativeCyberTheme.Ink(alive?1:.6f),TextAnchor.MiddleLeft,bold:you);
                NativeUi.Text(new Rect(r.x+12,r.y+19-hv,r.width-24,18),(content.weapons.FirstOrDefault(c=>c.id==p.spec)?.name??p.spec)+"  ·  "+p.chips.Values.Sum()+" chips",11,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft);
                NativeUi.Text(new Rect(r.x+12,r.y-hv,r.width-24,22),arena?.Pve!=null?"":"SETS "+p.score+" · ROUNDS "+p.roundWins,11,NativeCyberTheme.CyanSoft(),TextAnchor.MiddleRight,mono:true);
                NativeUi.Text(new Rect(r.x+12,r.y+19-hv,r.width-24,18),!p.connected?"OFFLINE":alive?"ALIVE":"OUT",10,alive?NativeCyberTheme.Good():NativeCyberTheme.Muted(),TextAnchor.MiddleRight,mono:true,tracking:.1f);
                if(NativeUi.Button(r))inspectSeat=p.id;cy+=46;
            }
        }
        private void OnDestroy(){StageLoadingRelease();NativeUiBlur.Release(true);NativeDamageScreenPass.Release(true);NativeGlitchScreenPass.Release(true);singleInstance?.Dispose();cinematic.Dispose();CapturedAnimation.Release(slenderNoise);Time.timeScale=1;EndPreview();ClearReplay();CloseNetwork();if(scopeMask!=null)CapturedAnimation.Release(scopeMask);arena?.Dispose();if(ownsPhysics){Physics.simulationMode=previousSimulation;Physics.gravity=previousGravity;}Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
