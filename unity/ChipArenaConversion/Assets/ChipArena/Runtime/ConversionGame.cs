using System;
using System.IO;
using System.Linq;
using UnityEngine;

using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ConversionPreferences
    { public int choices=6,selectionSeconds=18;public bool bothDraft;public string relayOrigin="";public string graphics="auto",sirenMode="Q";public float sensitivity=1,audio=.3f,music=.35f,menuMusic=.6f,uiScale=1;public bool trails=true,reducedEffects,cameraShake;public string bodyColor="#e5e7eb",accentColor="#70e1d0",hat="none",killEffect="impact-ragdoll",intro="neon-drop",introText="Ready to make an impact",accountOrigin="";public bool overrideIntro;public string name="Pilot",map="foundry",weapon="pistol";public int bots=3,sets=3,rounds=2;public string combatGlow="Full";public bool pve,skipWaves;public bool mirrorWeapons,teams,friendlyFire,stationary,skipBotDrafts=true,sound=true,firstPerson; }
    public sealed partial class ConversionGame : MonoBehaviour
    {
        private ConversionContent content;private ConversionPreferences prefs;private NativeMatch match;private NativeArena arena;private ArenaPresentation presentation;
        private NativeAudio nativeAudio;private Camera cameraView,viewModelCamera;private InputFrame pending;private SimulationMode previousSimulation;private Vector3 previousGravity;private bool ownsPhysics;private int generation;
        private bool menu=true;private float yaw,pitch=12,lastW=-1;private bool sprint;
        private string search="",error="";private GUIStyle heading,label,small,button;
        private readonly NativeVisualClock visualClock=new NativeVisualClock();
        private double stepMs;private readonly System.Diagnostics.Stopwatch timer=new System.Diagnostics.Stopwatch();
        private string PreferencePath=>Path.Combine(Application.persistentDataPath,"native-preferences-v1.json");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){if(FindFirstObjectByType<ConversionGame>()==null)new GameObject("Chip Arena Native Conversion").AddComponent<ConversionGame>();}
        private void Awake()
        {
            try
            {
                var asset=Resources.Load<TextAsset>("GameContent");if(asset==null)throw new InvalidOperationException("GameContent.json is missing");
                content=JsonUtility.FromJson<ConversionContent>(asset.text);content.Validate();if(InitializeDedicated())return;
                prefs=File.Exists(PreferencePath)?JsonUtility.FromJson<ConversionPreferences>(File.ReadAllText(PreferencePath)):new ConversionPreferences();
                if(prefs==null)prefs=new ConversionPreferences();if(!content.maps.Any(m=>m.id==prefs.map))prefs.map="foundry";
                if(!content.weapons.Any(w=>w.id==prefs.weapon&&!w.devOnly))prefs.weapon="pistol";prefs.bots=Mathf.Clamp(prefs.bots,prefs.pve?0:1,prefs.pve?3:7);
                InitializeAccount();Application.runInBackground=true;Application.targetFrameRate=144;QualitySettings.vSyncCount=0;Time.fixedDeltaTime=1f/60;Time.maximumDeltaTime=.1f;
                previousSimulation=Physics.simulationMode;previousGravity=Physics.gravity;ownsPhysics=true;Physics.simulationMode=SimulationMode.Script;
                nativeAudio=gameObject.AddComponent<NativeAudio>();presentation=gameObject.AddComponent<ArenaPresentation>();presentation.Initialize(false);presentation.DebrisEnabled=false;Physics.IgnoreLayerCollision(8,11,true);Physics.IgnoreLayerCollision(11,11,true);
                var cam=new GameObject("Native match camera");cam.transform.SetParent(transform);cameraView=cam.AddComponent<Camera>();cam.AddComponent<AudioListener>();
                cameraView.cullingMask=~(1<<10);NativeLighting.Create(transform);
                var model=new GameObject("First-person view");model.transform.SetParent(cam.transform,false);viewModelCamera=model.AddComponent<Camera>();
                viewModelCamera.clearFlags=CameraClearFlags.Depth;viewModelCamera.cullingMask=1<<10;viewModelCamera.depth=cameraView.depth+1;viewModelCamera.nearClipPlane=.01f;viewModelCamera.farClipPlane=10;viewModelCamera.enabled=false;presentation.ViewCamera=viewModelCamera;
                cameraView.nearClipPlane=.06f;cameraView.farClipPlane=200;NativeLighting.Apply(content.Map(prefs.map),cameraView);
                cameraView.transform.SetPositionAndRotation(new Vector3(0,22,-35),Quaternion.Euler(30,0,0));ApplySettings();InitializeUpdater();
            }
            catch(Exception e){error=e.ToString();Debug.LogException(e);}
        }
        private void Save()
        {
            try{Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(PreferencePath,JsonUtility.ToJson(prefs,true));}catch(IOException e){error="Could not save test preferences: "+e.Message;}
        }
        private void StartMatch()
        {
            Time.timeScale=1;sandboxSpeed=1;
            if(prefs.pve){prefs.map="siren";prefs.bots=Mathf.Clamp(prefs.bots,0,3);}else prefs.bots=Mathf.Clamp(prefs.bots,Online||dedicated?0:1,7);
            if(server!=null)return;if(prefs.pve&&peers.Count(p=>p.Seat!=null&&p.Connection?.Closed==false)>(dedicated?4:3)){networkStatus="Siren co-op supports up to four party members.";return;}if(Hosting&&peers.Any(p=>p.Seat!=null&&p.Connection?.Closed==false&&!p.Ready)){networkStatus="Wait for the other pilots to be ready.";return;}
            // Refuse a match that cannot start (a host alone with no bots, or every pilot on one team) before the running arena
            // is touched, so the reason shows in the menu instead of the fatal error screen.
            {var sides=new System.Collections.Generic.HashSet<int>();int n=0;if(!dedicated){sides.Add(prefs.pve||prefs.teams?0:1);n++;}
             foreach(var p in peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false)){sides.Add(prefs.pve?0:prefs.teams?p.Seat.team:p.Seat.id);n++;}
             int fill=Math.Min(prefs.bots,(prefs.pve?4:8)-n);for(int i=0;i<fill;i++){sides.Add(prefs.pve?0:prefs.teams?(i+1)%2:100+i);n++;}if(prefs.pve){sides.Add(1);n++;}
             if(n<2||sides.Count<2){networkStatus=n>=2?"Both teams need a pilot: add a bot or change teams.":"Add a bot or wait for a pilot to join.";return;}}
            if(!dedicated)Save();EndPreview();inspectSeat=0;ClearReplay();arena?.Dispose();presentation?.ResetPresentation();networkEpoch++;
            match=new NativeMatch(content){ShowIntros=true,Cooperative=prefs.pve,Teams=prefs.pve||prefs.teams,RoundsPerSet=prefs.rounds,SetsToWin=prefs.sets,SkipBotDrafts=prefs.skipBotDrafts,BothDraft=prefs.bothDraft,Choices=prefs.choices,SelectionSeconds=prefs.selectionSeconds};
            if(!dedicated){var local=SanitizedSeat(1,Profile(),account.Token);local.team=0;match.Seats.Add(local);}
            foreach(var peer in peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false)){peer.Seat.team=prefs.pve?0:peer.Seat.team;match.Seats.Add(peer.Seat);peer.Ready=false;}
            var weapons=content.weapons.Where(w=>!w.devOnly).Select(w=>w.id).ToArray();
            int botCount=Math.Min(prefs.bots,(prefs.pve?4:8)-match.Seats.Count);for(int i=0;i<botCount;i++)match.Seats.Add(new NativeSeat{id=100+i,name="Pilot "+(i+2),spec=prefs.mirrorWeapons?prefs.weapon:weapons[i%weapons.Length],bot=true,team=prefs.pve?0:prefs.teams?(i+1)%2:i+1});
            if(prefs.pve)match.Seats.Add(new NativeSeat{id=900,name="The Siren",spec="siren",bot=true,team=1,AuthorityBoss=true});
            arena=new NativeArena(content,match,prefs.map){FriendlyFire=prefs.friendlyFire,LocalId=dedicated?0:1,StationaryBots=prefs.stationary,Sandbox=sandboxWanted&&account.Developer&&!Online&&!prefs.pve};
            if(!match.Start()){error="A match needs at least two opposing sides.";return;}
            if(prefs.pve)arena.ConfigurePve(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text),prefs.skipWaves);
            if(dedicated){menu=false;return;}generation=arena.Generation;visualClock.Reset(match.Time);pending=new InputFrame();yaw=arena.Local.Yaw;pitch=12;sprint=false;menu=false;NativeLighting.Apply(content.Map(prefs.map),cameraView);
        }
        private float shakeAmount;private int shakeEvent;
        private void Update()
        {
            if(content==null)return;if(dedicated){DedicatedTick();return;}if(cameraView==null)return;NetworkTick();
            // The main menu is always open without a match (an Escape there steps back instead), so the updater's install gate holds.
            if(arena==null)menu=true;if(!menu)pauseScreen=PauseScreen.Menu;
            if(Input.GetKeyDown(KeyCode.Escape)&&previewArena!=null){EndPreview();return;}if(Input.GetKeyDown(KeyCode.Escape)&&inspectSeat!=0){inspectSeat=0;return;}if(Input.GetKeyDown(KeyCode.Escape)&&ReplayActive){EndReplay();return;}if(Input.GetKeyDown(KeyCode.Escape)){if(arena==null)MenuBack();else if(PvePickOpen)PveOpen(false);else if(menu&&pauseScreen!=PauseScreen.Menu){if(pauseScreen==PauseScreen.Settings){ApplySettings();Save();}pauseScreen=PauseScreen.Menu;}else menu=!menu;pending=new InputFrame();pending.CancelAbilities=true;if(server==null)arena?.CancelLocalAbilities();}
            // M mutes anywhere and V switches the camera during a match (main.ts keydown), neither while a text field has focus
            // or the board is open; both confirm with a notice.
            bool typing=GUIUtility.keyboardControl!=0;
            if(Input.GetKeyDown(KeyCode.M)&&!typing&&!BoardOpen){prefs.sound=!prefs.sound;if(nativeAudio!=null)nativeAudio.Muted=!prefs.sound;Save();Notice(prefs.sound?"Sound on · M to mute":"Sound muted · M to unmute");}
            if(Input.GetKeyDown(KeyCode.V)&&arena!=null&&!typing&&!BoardOpen&&!(arena.ViewPilot?.Seat.spec=="siren"&&prefs.sirenMode=="V"&&!menu)){prefs.firstPerson=!prefs.firstPerson;Save();Notice(prefs.firstPerson?"First person · V to switch":"Third person · V to switch");}
            if(Input.GetKeyDown(KeyCode.C)&&arena?.Pve!=null){if(PvePickOpen)PveOpen(false);else PveOpen(true);}
            if(Input.GetKeyDown(KeyCode.F9))presentation.ForceCompatibilityRendering=!presentation.ForceCompatibilityRendering;
            bool play=arena!=null&&!menu&&!BoardOpen&&match.Phase==NativePhase.Fight&&arena.Local.Hp>0&&!PvePickOpen&&arena.Pve?.Holding!=true;
            bool look=!BoardOpen&&(play||arena?.Local?.Hp<=0&&!menu&&!ReplayActive&&match.Phase==NativePhase.Fight);Cursor.lockState=look?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!look;
            if(arena==null)return;
            if(generation!=arena.Generation){presentation.ResetPresentation();generation=arena.Generation;visualClock.Reset(match.Time);}
            SpectatorControls();var p=arena.ViewPilot;
            UpdateLock();if(play)
            {
                var ads=content.Ads(p.Seat.spec);bool aiming=ads!=null&&Input.GetKey(KeyCode.Q);
                if(!lockPoint.HasValue){yaw+=Input.GetAxisRaw("Mouse X")*2*prefs.sensitivity*(aiming?(float)ads.sens:1);pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*prefs.sensitivity*(aiming?(float)ads.sens:1),-70,70);}
                if(Input.GetKeyDown(KeyCode.W)){if(Time.unscaledTime-lastW<.3f)sprint=true;lastW=Time.unscaledTime;}if(!Input.GetKey(KeyCode.W))sprint=false;
                var move=Quaternion.Euler(0,yaw,0)*new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),0,(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)).normalized;
                pending.X=move.x;pending.Z=move.z;pending.Yaw=yaw;pending.Pitch=-pitch;pending.Sprint=sprint;pending.Aim=aiming;
                if(p.Weapon.@class=="melee")pending.Shoot|=Input.GetMouseButtonDown(0);else pending.Shoot=Input.GetMouseButton(0);
                pending.AbilityQ|=p.Weapon.@class=="magic"&&Input.GetKeyDown(KeyCode.Q);pending.AbilityE|=Input.GetKeyDown(KeyCode.E);pending.AbilityEReleased|=Input.GetKeyUp(KeyCode.E);pending.AbilityEHeld=Input.GetKey(KeyCode.E);
                pending.Hadoken|=Input.GetKeyDown(KeyCode.Alpha1);pending.HadokenHeld=Input.GetKey(KeyCode.Alpha1);pending.Uppercut|=Input.GetKeyDown(KeyCode.Alpha2);
                for(int key=0;key<4;key++)if(Input.GetKeyDown(KeyCode.Alpha1+key))pending.SirenAbility=(byte)(key+1);
                pending.AbilityQ|=p.Seat.spec=="emperor"&&Input.GetKeyDown(KeyCode.Q)||p.Seat.spec=="siren"&&Input.GetKeyDown(prefs.sirenMode=="F"?KeyCode.F:prefs.sirenMode=="V"?KeyCode.V:KeyCode.Q);
                pending.Interact|=Input.GetKeyDown(KeyCode.F)&&!(p.Seat.spec=="siren"&&prefs.sirenMode=="F");pending.Block|=Input.GetMouseButtonDown(1);pending.Reload|=Input.GetKeyDown(KeyCode.R);pending.Roll|=Input.GetKeyDown(KeyCode.LeftShift);pending.Jump|=Input.GetKeyDown(KeyCode.Space);
            }
            else pending=new InputFrame{Yaw=yaw,Pitch=-pitch};
        }
        private void LateUpdate()
        {
            if(cameraView==null)return;if(PreviewFrame())return;ObserveReplay();if(arena!=null&&RenderReplay())return;nativeAudio.Muted=!prefs.sound;nativeAudio.Entrance(arena!=null&&!menu&&IntroActive?arena.Generation+":"+IntroPilot?.Seat.id+":"+IntroStyle:null,arena!=null&&IntroActive?IntroStyle:null,arena!=null&&IntroActive?IntroAge:0);nativeAudio.Render(arena,menu,cameraView);SlenderFrame();if(arena==null){MenuBackdrop();return;}backdropMap=null;
            if(DraftFrame())return;if(!menu&&IntroActive){arena.Root.SetActive(false);viewModelCamera.enabled=false;presentation.RenderIntro(arena,IntroPilot,IntroStyle,IntroAge,cameraView);return;}NativeLighting.EndIntro(content.Map(arena.MapId),cameraView);arena.Root.SetActive(true);
            var p=arena.ViewPilot;bool play=!menu&&match.Phase==NativePhase.Fight&&p.Hp>0&&!PvePickOpen&&arena.Pve?.Holding!=true;
            UpdateCamera();
            if(play)
            {
                pending.AimTarget=NativeArena.Vec(AimPoint((float)p.Weapon.range));
                var ability=content.Ability(p.Seat.spec,pending.AbilityEHeld||pending.AbilityE||pending.AbilityEReleased?"e":"q");
                pending.AbilityTarget=NativeArena.Vec(AimPoint(ability!=null?(float)ability.range:p.Seat.spec=="shield"?25:p.Seat.spec=="daggers"?48:60,p.Seat.spec=="shield"||p.Seat.spec=="daggers"));
            }
            // renderer.ts camera shake: a hit, headshot, block or parry aimed at the local pilot sets .12, which decays by 1 per
            // second while each frame nudges the camera's world X by a random ±half of it (Settings · camera shake).
            if(play){int newest=0;foreach(var e in arena.Events){newest=Math.Max(newest,e.Id);if(e.Id>shakeEvent&&e.Target==p.Seat.id&&(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot||e.Kind==EventKind.Block)&&prefs.cameraShake)shakeAmount=.12f;}shakeEvent=newest<shakeEvent?newest:Math.Max(shakeEvent,newest);}
            if(shakeAmount>0){cameraView.transform.position+=Vector3.right*((UnityEngine.Random.value-.5f)*shakeAmount);shakeAmount=Math.Max(0,shakeAmount-Time.deltaTime);}
            presentation.SoundEnabled=false;presentation.LocalId=arena.ViewPilot.Seat.id;
            float renderTime=visualClock.Sample(match.Time,Time.timeAsDouble-Time.fixedTimeAsDouble,menu);
            presentation.Render(arena.Snapshot(true),renderTime,false);
            Vector3? preview=null;
            if(play&&p.HoldAt>=0&&arena.Time-p.HoldAt>=.2f)
            {
                Vector3 point=NativeArena.Vec(pending.AbilityTarget);var e=content.Ability(p.Seat.spec,"e");
                point=p.Body.position+Vector3.ClampMagnitude(point-p.Body.position,(float)e.range);
                point.x=Mathf.Clamp(point.x,-31,31);point.z=Mathf.Clamp(point.z,-27,27);
                if(e.kind!="throw"&&Physics.Raycast(point+Vector3.up*.08f,Vector3.down,out var ground,40,NativeArena.WorldMask))point=ground.point;
                preview=point;
            }
            presentation.EffectsQuality=prefs.reducedEffects?.3f:1;
            presentation.RenderAbilities(arena,preview);
            presentation.GlowQuality=prefs.reducedEffects?.3f:prefs.combatGlow=="Off"?0:prefs.combatGlow=="Low"?.4f:1;presentation.RenderGlow(arena,cameraView);presentation.FinishSourceFrame();
        }
        // No match: the selected map behind the menu with the source's slow overview orbit (renderer.ts idle camera).
        private string backdropMap;
        private void MenuBackdrop()
        {
            string map=prefs.pve?"siren":prefs.map;var definition=content.Map(map);if(definition==null||presentation==null)return;
            if(backdropMap!=map){backdropMap=map;NativeLighting.Apply(definition,cameraView);}
            viewModelCamera.enabled=false;ArenaPresentation.OverviewCamera(cameraView,Time.unscaledTime);presentation.RenderBackdrop(content.sourceCommit,map,Time.unscaledTime);
        }
        private void FixedUpdate()
        {
            if(arena==null||menu&&!Online)return;if(arena.Replica){if(server!=null){pending.Sequence=++inputSequence;SendServer(new NativeNetMessage{type="input",input=pending});pending=pending.HeldOnly();if(arena.ViewPilot.Weapon.@class=="melee")pending.Shoot=false;}return;}timer.Restart();bool held=arena.Pve?.Holding==true;arena.Step(pending);if(!held&&arena.Pve?.Holding!=true){arena.BeforePhysics();Physics.Simulate(1f/60);arena.AfterPhysics();}timer.Stop();stepMs=timer.Elapsed.TotalMilliseconds;
            bool melee=arena.ViewPilot?.Weapon.@class=="melee";pending=pending.HeldOnly();if(melee)pending.Shoot=false;
        }
        private Vector3 AimPoint(float range,bool thrownMelee=false)
        {
            Ray ray=cameraView.ViewportPointToRay(new Vector3(.5f,.5f));float nearest=range+(thrownMelee?0:Vector3.Distance(ray.origin,arena.ViewPilot.Body.position));
            Vector3 target=ray.origin+ray.direction*(thrownMelee?48:nearest);
            foreach(var hit in Physics.RaycastAll(ray,nearest,NativeArena.ShotMask,QueryTriggerInteraction.Ignore))
            {var pilot=hit.collider.GetComponent<NativePilotBody>();if(pilot!=null&&pilot.Pilot==arena.ViewPilot)continue;if(hit.distance<nearest){target=hit.point;nearest=hit.distance;}}
            return target;
        }
        private void UpdateCamera()
        {
            var p=Spectator??arena.ViewPilot;Vector3 focus=p.Body.transform.position+Vector3.up*NativeViewRules.EyeHeight;var ads=content.Ads(p.Seat.spec);
            bool aim=ads!=null&&pending.Aim&&arena.Time>=p.ReloadEnd&&arena.Time>=p.RollEnd&&arena.Time>=p.BlockEnd&&!arena.Incapacitated(p);
            bool first=NativeViewRules.FirstPerson(prefs.firstPerson||aim,arena.Local.Hp>0&&p.Hp>0&&!arena.Downed(p),menu);
            cameraView.fieldOfView=Mathf.Lerp(cameraView.fieldOfView,aim?(float)ads.fov:first?NativeViewRules.FirstFov:NativeViewRules.ThirdFov,1-Mathf.Exp(-16*Time.unscaledDeltaTime));
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);Vector3 location=focus;
            if(!first)
            {
                Vector3 offset=rotation*Vector3.back*NativeViewRules.ThirdDistance+Vector3.up*NativeViewRules.ThirdLift;
                location=focus+offset;
                if(Physics.SphereCast(focus,.12f,offset.normalized,out var hit,offset.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))location=focus+offset.normalized*Mathf.Max(.15f,hit.distance-NativeViewRules.CollisionPadding);
            }
            cameraView.transform.SetPositionAndRotation(location,rotation);
            presentation.FirstPerson=first;presentation.AimHeld=aim;viewModelCamera.enabled=first;viewModelCamera.fieldOfView=cameraView.fieldOfView;
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
            if(d.kind=="ads")return "Q  Aim (hold)";
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
            if(dedicated)return;if(SlenderGUI())return;Styles();GuiFrame();if(error.Length==0)NoticeToast();
        }
        private void GuiFrame()
        {
            // One canvas for every overlay and the menus (NativeUiMotion.Measure): 900 units tall at 16:9, wider at 21:9 (2150 at 3440x1440).
            var ui=NativeUi.Begin(prefs?.uiScale??1);float scale=ui.Scale,width=ui.Width,height=ui.Height;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            if(error.Length>0){GUI.Box(new Rect(30,30,width-60,height-60),"");GUI.Label(new Rect(45,45,width-90,height-90),error,label);return;}
            if(match?.Phase!=NativePhase.Match)matchEndAt=-1;if(arena!=null)menuFromMatch=true;
            // The intro overlay replaces the whole HUD (main.ts drawHud), so nothing else draws while it runs.
            if(arena!=null&&!menu&&previewArena==null&&IntroActive){IntroOverlay();return;}
            // The killcam is a broadcast frame of the past: the live HUD steps aside while it plays (killcam.css).
            if(arena!=null&&!menu&&previewArena==null&&ReplayActive){ReplayHud(width,height);return;}
            // Chip selection is a full screen: the PvP draft, and the Siren personal pick (main HUD hidden while it is open).
            if(arena!=null&&!menu&&previewArena==null&&match.Phase==NativePhase.Draft){DraftScreen();return;}
            if(previewArena==null&&PvePickScreen){PveChipScreen();return;}
            if(previewArena==null&&SirenRevealActive){SirenReveal();return;}
            CombatWorldHud(scale,width,height);
            PveBeaconHud();
            if(arena!=null&&!menu&&match.Phase==NativePhase.Fight&&presentation.ScopeActive)DrawScope(width,height);
            if(previewArena!=null){PreviewHud(width,height);return;}
            if(arena!=null)
            {
                var p=arena.Local;bool fighting=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Countdown;
                if(!menu&&(fighting||match.Phase==NativePhase.Result))FightHud(p);
                if(!menu&&fighting&&p.Hp>0&&!presentation.ScopeActive)Crosshair();
                if(!menu&&match.Phase==NativePhase.Fight)
                {
                    if(arena.Downed(p))Prompt(U.H/2+80,"RECOVERING  ·  WASD chooses roll direction",14,NativeUi.Hex(0xd7d8de));
                    if(arena.RiposteOpen(p))Prompt(U.H/2+40,"RIPOSTE  ·  LMB counter",17,NativeUi.Hex(0xff996f));
                }
                if(!menu&&match.Phase==NativePhase.Countdown)CountdownOverlay();
                if(!menu&&match.Phase==NativePhase.Result)ResultOverlay();
                if(!menu&&match.Phase==NativePhase.Match&&arena.Pve==null)MatchEndPanel();
                else if(!menu&&match.Phase==NativePhase.Match)PveResultsPanel();
                if(BoardOpen)Board(width);if(inspectSeat!=0)Inspect(width,height);LockHud(scale);
            }
            if(!menu&&arena!=null){ReplayHud(width,height);PveHud(width,height);return;}
            // The menus (NativeHomeMenu): the home screen and its panels without a match, the pause menu during one.
            if(arena!=null)PauseMenu();else HomeMenu();
        }
        // Play vs Bots (main.ts practice screen) over the native match options; also the host's rules in a room lobby,
        // where the weapon is picked under the roster and the draft rules sit under Advanced Options (room-options.ts).
        private void MatchRulesBody(bool lobby)
        {
            if(lobby){LobbyRulesBody();return;}
            Fine("First to "+prefs.sets+(prefs.sets==1?" set":" sets")+". Win "+prefs.rounds+(prefs.rounds==1?" round":" rounds")+" to take a set.");Field("Your weapon");WeaponGrid();
            Field("Arena");MapGrid();
            Field("Bots");GUILayout.Label(prefs.bots==1?"1 bot":prefs.bots+" bots",label);prefs.bots=Mathf.RoundToInt(GUILayout.HorizontalSlider(Mathf.Clamp(prefs.bots,1,7),1,7));
            Fine("Up to 8 pilots total. Bots use player slots.");prefs.mirrorWeapons=GUILayout.Toggle(prefs.mirrorWeapons,"Bots use my weapon (otherwise mixed loadouts)");prefs.stationary=GUILayout.Toggle(prefs.stationary,"Stationary targets");
            Field("Match mode");prefs.teams=ChoiceRow(new[]{"Free-for-all","Teams"},prefs.teams?1:0)==1;if(prefs.teams){prefs.friendlyFire=GUILayout.Toggle(prefs.friendlyFire,"Friendly fire");Fine("Teams alternate red and blue sides.");}
            Field("Sets and rounds");GUILayout.Label("Sets to win the match: "+prefs.sets,label);prefs.sets=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.sets,1,10));GUILayout.Label("Rounds to win a set: "+prefs.rounds,label);prefs.rounds=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.rounds,1,5));
            Field("Draft and intro rules");prefs.bothDraft=GUILayout.Toggle(prefs.bothDraft,"All players draft after each set (otherwise the set's losers)");
            GUILayout.Label("Chip choices per draft: "+prefs.choices,label);prefs.choices=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.choices,1,12));GUILayout.Label("Chip selection timer: "+prefs.selectionSeconds+"s",label);prefs.selectionSeconds=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.selectionSeconds,5,120));
            prefs.skipBotDrafts=GUILayout.Toggle(prefs.skipBotDrafts,"Skip bot draft screens");
            Fine("Melee E: special attack. Gun Q: hold to aim. Magic Q / E: cast abilities. Hold E to preview grenade, meteor, geysers or black hole; release to cast.");
        }
        // The room column's rules in the source order (main.ts matchOptions(true,'essential') then roomAdvanced): the match
        // mode and arena box, then Advanced Options with friendly fire, sets and rounds, bots and the draft rules, under a note
        // that summarises them. Labels are short enough for the 300 px column.
        private void LobbyRulesBody()
        {
            LobbyEssentials();
            if(!AdvancedFold("First to "+prefs.sets+" set"+(prefs.sets==1?"":"s")+" · "+prefs.rounds+" round"+(prefs.rounds==1?"":"s")+" per set · "+prefs.bots+" bot"+(prefs.bots==1?"":"s")+(prefs.teams?" · friendly fire "+(prefs.friendlyFire?"on":"off"):"")))return;
            if(prefs.teams){prefs.friendlyFire=GUILayout.Toggle(prefs.friendlyFire,"Friendly fire");Fine(prefs.friendlyFire?"Teammates can damage each other.":"Allied attacks and harmful effects pass through teammates.");}
            GUILayout.Label("Sets to win the match: "+prefs.sets,label);prefs.sets=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.sets,1,10));GUILayout.Label("Rounds to win a set: "+prefs.rounds,label);prefs.rounds=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.rounds,1,5));
            Field("Bots");GUILayout.Label(prefs.bots==1?"1 bot":prefs.bots+" bots",label);prefs.bots=Mathf.RoundToInt(GUILayout.HorizontalSlider(Mathf.Clamp(prefs.bots,0,7),0,7));
            Fine("Up to 8 pilots total. Bots use player slots.");prefs.mirrorWeapons=GUILayout.Toggle(prefs.mirrorWeapons,"Bots use my weapon");prefs.stationary=GUILayout.Toggle(prefs.stationary,"Stationary targets");prefs.skipBotDrafts=GUILayout.Toggle(prefs.skipBotDrafts,"Skip bot draft screens");
            Field("Draft rules");prefs.bothDraft=GUILayout.Toggle(prefs.bothDraft,"All players draft");Fine(prefs.bothDraft?"Everyone drafts after each set.":"Otherwise the set's losers draft.");
            GUILayout.Label("Chip choices per draft: "+prefs.choices,label);prefs.choices=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.choices,1,12));GUILayout.Label("Chip selection timer: "+prefs.selectionSeconds+"s",label);prefs.selectionSeconds=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.selectionSeconds,5,120));
            Fine(prefs.choices+" choices per draft · "+prefs.selectionSeconds+"s to pick · "+(prefs.bothDraft?"All players draft":"Set losers draft")+" · Intros on");
            AdvancedEnd();
        }
        // Boss Test (pve/menu.ts testBossBody): party bots, the skip to the Siren, and the weapon (picked under the roster
        // in a room lobby).
        private void BossBody(bool lobby=false)
        {
            // The room column (pve/menu.ts lobby rules): the party settings under Advanced Options, with labels that fit it.
            if(lobby)
            {
                if(!AdvancedFold(prefs.bots+" party bot"+(prefs.bots==1?"":"s")+" · "+(prefs.skipWaves?"starts at the Siren":"opening waves on")+" · "+(prefs.mirrorWeapons?"bots use your weapon":"mixed bot loadouts")))return;
                Field("Party bots");GUILayout.Label(prefs.bots==1?"1 party bot":prefs.bots+" party bots",label);prefs.bots=Mathf.RoundToInt(GUILayout.HorizontalSlider(Mathf.Clamp(prefs.bots,0,3),0,3));
                Fine("Friends who join take party slots; bots fill the rest (up to 4).");prefs.mirrorWeapons=GUILayout.Toggle(prefs.mirrorWeapons,"Party bots use my weapon");prefs.skipWaves=GUILayout.Toggle(prefs.skipWaves,"Start at the Siren");
                Fine("Earn money from minions for chips, heals, revives and recruits. F interacts · C opens your chips.");
                AdvancedEnd();return;
            }
            Field("Party bots");GUILayout.Label(prefs.bots==1?"1 party bot":prefs.bots+" party bots",label);prefs.bots=Mathf.RoundToInt(GUILayout.HorizontalSlider(Mathf.Clamp(prefs.bots,0,3),0,3));
            Fine("AI pilots fill the party (up to 4). In a hosted room, friends who join take the party slots.");prefs.mirrorWeapons=GUILayout.Toggle(prefs.mirrorWeapons,"Party bots use my weapon (otherwise mixed loadouts)");prefs.skipWaves=GUILayout.Toggle(prefs.skipWaves,"Start at the Siren (skip the opening waves)");
            Field("Your weapon");WeaponGrid();
            Fine("Intros play as normal. Earn money from minions to buy chips, heals, revives and recruits. F interacts · C opens your chips; chips you take wait there until you choose.");
        }
        private void WeaponGrid()
        {
            var weapons=content.weapons.Where(AllowedWeapon).ToArray();string pick=ChoiceGrid(weapons.Select(w=>w.id).ToArray(),prefs.weapon,weapons.Select(w=>w.name).ToArray());if(pick!=prefs.weapon){prefs.weapon=pick;if(Hosting&&arena==null)LobbyBroadcast();}
        }
        // Every arena, Ward Zero included (its PvP variant); picking one also turns the menu backdrop to it (main.ts previewMap).
        private void MapGrid(){var maps=content.maps;prefs.map=ChoiceGrid(maps.Select(m=>m.id).ToArray(),prefs.map,maps.Select(m=>m.name).ToArray());}
        private void Catalogue()
        {
            Fine("Reference catalogue: the source's weapons and Chips. Importing a definition does not enable its mechanic.");Field("Search");search=GUILayout.TextField(search);
            foreach(var w in content.weapons.Where(w=>(w.name+" "+w.id).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){GUILayout.Label(w.name+(w.devOnly?" — signed unlock required":""),label);GUILayout.Label(w.description,small);}
            foreach(var c in content.chips.Where(c=>(c.name+" "+c.description).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){GUILayout.Label(c.name+"  /  "+c.rarity+(NativeArena.ImplementedChip(c)?"  /  draft enabled":"  /  pending"),label);GUILayout.Label(c.description,small);}
        }
        // The pilot board (G / Tab) at the top right, where the source keeps scores: each row opens that pilot's build.
        private void Board(float width)
        {
            if(inspectSeat!=0)return;float W=NativeUi.W,w=360,x=W-24-w,y=62;var seats=match.Seats.Where(s=>!s.AuthorityBoss).ToList();float h=14+20+10+seats.Count*46+10;
            NativeUi.Round(new Rect(x,y,w,h),NativeUi.Hex(0x13141c,.92f),10,new Color(1,1,1,.14f));NativeUi.Text(new Rect(x+14,y+14,w-28,20),arena?.Pve!=null?"PARTY":"PILOTS",12,NativeUi.Hex(0xedf2fc),TextAnchor.MiddleLeft,bold:true,tracking:.08f);
            NativeUi.Text(new Rect(x+14,y+14,w-28,20),"Click to inspect a build",11,NativeUi.Hex(0xaeb6c8),TextAnchor.MiddleRight);float cy=y+44;
            foreach(var p in seats)
            {
                var r=new Rect(x+10,cy,w-20,40);float hv=NativeUi.HoverAmount(7100+p.id,r);bool you=p.id==arena?.Local?.Seat.id;var pilot=arena?.Pilots.Find(q=>q.Seat==p);bool alive=pilot!=null&&pilot.Hp>0&&p.connected;
                NativeUi.Round(new Rect(r.x,r.y-hv,r.width,r.height),NativeUi.Mix(NativeUi.Hex(0x18191f,.9f),NativeUi.Hex(0x22232a),hv),6,NativeUi.Mix(new Color(1,1,1,.06f),NativeUi.Hex(0xff784b),hv));
                NativeUi.Fill(new Rect(r.x,r.y+6-hv,2,r.height-12),match.Teams?TeamColor(p.team):SeatColor(p));
                NativeUi.Text(new Rect(r.x+12,r.y-hv,r.width-24,22),p.name+(you?"  · YOU":""),13,NativeUi.Hex(0xf4f4f5,alive?1:.6f),TextAnchor.MiddleLeft,bold:you);
                NativeUi.Text(new Rect(r.x+12,r.y+19-hv,r.width-24,18),(content.weapons.FirstOrDefault(c=>c.id==p.spec)?.name??p.spec)+"  ·  "+p.chips.Values.Sum()+" chips",11,NativeUi.Hex(0xa6a7ad),TextAnchor.MiddleLeft);
                NativeUi.Text(new Rect(r.x+12,r.y-hv,r.width-24,22),arena?.Pve!=null?"":"SETS "+p.score+" · ROUNDS "+p.roundWins,11,NativeUi.Hex(0xd6d7e1),TextAnchor.MiddleRight,mono:true);
                NativeUi.Text(new Rect(r.x+12,r.y+19-hv,r.width-24,18),!p.connected?"OFFLINE":alive?"ALIVE":"OUT",10,alive?NativeUi.Hex(0xa9f4ca):NativeUi.Hex(0xa4a9b6),TextAnchor.MiddleRight,mono:true,tracking:.1f);
                if(NativeUi.Button(r))inspectSeat=p.id;cy+=46;
            }
        }
        private void OnDestroy(){CapturedAnimation.Release(slenderNoise);Time.timeScale=1;EndPreview();ClearReplay();CloseNetwork();if(scopeMask!=null)CapturedAnimation.Release(scopeMask);arena?.Dispose();if(ownsPhysics){Physics.simulationMode=previousSimulation;Physics.gravity=previousGravity;}Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
