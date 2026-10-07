using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ConversionPreferences
    { public string name="Pilot",map="foundry",weapon="pistol";public int bots=3,sets=3,rounds=2;public string combatGlow="Full";public bool mirrorWeapons,teams,friendlyFire,stationary,skipBotDrafts=true,sound=true,firstPerson; }
    public sealed partial class ConversionGame : MonoBehaviour
    {
        private ConversionContent content;private ConversionPreferences prefs;private NativeMatch match;private NativeArena arena;private ArenaPresentation presentation;
        private Camera cameraView,viewModelCamera;private InputFrame pending;private SimulationMode previousSimulation;private Vector3 previousGravity;private bool ownsPhysics;private int generation;
        private bool menu=true,showCatalogue;private float yaw,pitch=12,lastW=-1;private bool sprint;
        private Vector2 scroll,catalogueScroll;private string search="",error="";private GUIStyle heading,label,small,button;
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
                content=JsonUtility.FromJson<ConversionContent>(asset.text);content.Validate();
                prefs=File.Exists(PreferencePath)?JsonUtility.FromJson<ConversionPreferences>(File.ReadAllText(PreferencePath)):new ConversionPreferences();
                if(prefs==null)prefs=new ConversionPreferences();if(!content.maps.Any(m=>m.id==prefs.map))prefs.map="foundry";
                if(!content.weapons.Any(w=>w.id==prefs.weapon&&!w.devOnly))prefs.weapon="pistol";prefs.bots=Mathf.Clamp(prefs.bots,1,7);
                Application.runInBackground=true;Application.targetFrameRate=144;QualitySettings.vSyncCount=0;Time.fixedDeltaTime=1f/60;Time.maximumDeltaTime=.1f;
                previousSimulation=Physics.simulationMode;previousGravity=Physics.gravity;ownsPhysics=true;Physics.simulationMode=SimulationMode.Script;
                presentation=gameObject.AddComponent<ArenaPresentation>();presentation.Initialize(false);presentation.DebrisEnabled=false;
                var cam=new GameObject("Native match camera");cam.transform.SetParent(transform);cameraView=cam.AddComponent<Camera>();cam.AddComponent<AudioListener>();
                cameraView.cullingMask=~(1<<10);NativeLighting.Create(transform);
                var model=new GameObject("First-person view");model.transform.SetParent(cam.transform,false);viewModelCamera=model.AddComponent<Camera>();
                viewModelCamera.clearFlags=CameraClearFlags.Depth;viewModelCamera.cullingMask=1<<10;viewModelCamera.depth=cameraView.depth+1;viewModelCamera.nearClipPlane=.01f;viewModelCamera.farClipPlane=10;viewModelCamera.enabled=false;presentation.ViewCamera=viewModelCamera;
                cameraView.nearClipPlane=.06f;cameraView.farClipPlane=200;cameraView.backgroundColor=NativeArena.ColorOf(content.Map(prefs.map).sky);cameraView.clearFlags=CameraClearFlags.SolidColor;
                cameraView.transform.SetPositionAndRotation(new Vector3(0,22,-35),Quaternion.Euler(30,0,0));
            }
            catch(Exception e){error=e.ToString();Debug.LogException(e);}
        }
        private void Save()
        {
            try{Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(PreferencePath,JsonUtility.ToJson(prefs,true));}catch(IOException e){error="Could not save test preferences: "+e.Message;}
        }
        private void StartMatch()
        {
            Save();arena?.Dispose();presentation.ResetPresentation();
            match=new NativeMatch(content){Teams=prefs.teams,RoundsPerSet=prefs.rounds,SetsToWin=prefs.sets,SkipBotDrafts=prefs.skipBotDrafts};
            match.Seats.Add(new NativeSeat{id=1,name=string.IsNullOrWhiteSpace(prefs.name)?"Pilot":prefs.name,spec=prefs.weapon,team=0});
            var weapons=content.weapons.Where(w=>!w.devOnly).Select(w=>w.id).ToArray();
            for(int i=0;i<prefs.bots;i++)match.Seats.Add(new NativeSeat{id=100+i,name="Pilot "+(i+2),spec=prefs.mirrorWeapons?prefs.weapon:weapons[i%weapons.Length],bot=true,team=prefs.teams?(i+1)%2:i+1});
            arena=new NativeArena(content,match,prefs.map){FriendlyFire=prefs.friendlyFire,StationaryBots=prefs.stationary};
            if(!match.Start()){error="A match needs at least two opposing sides.";return;}
            generation=arena.Generation;visualClock.Reset(match.Time);pending=new InputFrame();yaw=arena.Local.Yaw;pitch=12;sprint=false;menu=false;showCatalogue=false;cameraView.backgroundColor=NativeArena.ColorOf(content.Map(prefs.map).sky);
        }
        private void Update()
        {
            if(content==null||cameraView==null)return;
            if(Input.GetKeyDown(KeyCode.Escape)){menu=!menu;pending=new InputFrame();arena?.CancelLocalAbilities();}
            if(Input.GetKeyDown(KeyCode.V)){prefs.firstPerson=!prefs.firstPerson;Save();}
            if(Input.GetKeyDown(KeyCode.F9))presentation.ForceCompatibilityRendering=!presentation.ForceCompatibilityRendering;
            bool play=arena!=null&&!menu&&match.Phase==NativePhase.Fight&&arena.Local.Hp>0;
            Cursor.lockState=play?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!play;
            if(arena==null)return;
            if(generation!=arena.Generation){presentation.ResetPresentation();generation=arena.Generation;visualClock.Reset(match.Time);}
            var p=arena.Local;
            if(play)
            {
                var ads=content.Ads(p.Seat.spec);bool aiming=ads!=null&&Input.GetKey(KeyCode.Q);
                yaw+=Input.GetAxisRaw("Mouse X")*2*(aiming?(float)ads.sens:1);pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*(aiming?(float)ads.sens:1),-70,70);
                if(Input.GetKeyDown(KeyCode.W)){if(Time.unscaledTime-lastW<.3f)sprint=true;lastW=Time.unscaledTime;}if(!Input.GetKey(KeyCode.W))sprint=false;
                var move=Quaternion.Euler(0,yaw,0)*new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),0,(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)).normalized;
                pending.X=move.x;pending.Z=move.z;pending.Yaw=yaw;pending.Pitch=-pitch;pending.Sprint=sprint;pending.Aim=aiming;
                if(p.Weapon.@class=="melee")pending.Shoot|=Input.GetMouseButtonDown(0);else pending.Shoot=Input.GetMouseButton(0);
                pending.AbilityQ|=p.Weapon.@class=="magic"&&Input.GetKeyDown(KeyCode.Q);pending.AbilityE|=Input.GetKeyDown(KeyCode.E);pending.AbilityEReleased|=Input.GetKeyUp(KeyCode.E);pending.AbilityEHeld=Input.GetKey(KeyCode.E);
                pending.Block|=Input.GetMouseButtonDown(1);pending.Reload|=Input.GetKeyDown(KeyCode.R);pending.Roll|=Input.GetKeyDown(KeyCode.LeftShift);pending.Jump|=Input.GetKeyDown(KeyCode.Space);
            }
            else pending=new InputFrame{Yaw=yaw,Pitch=-pitch};
        }
        private void LateUpdate()
        {
            if(arena==null||cameraView==null)return;
            var p=arena.Local;bool play=!menu&&match.Phase==NativePhase.Fight&&p.Hp>0;
            UpdateCamera();
            if(play)
            {
                pending.AimTarget=NativeArena.Vec(AimPoint((float)p.Weapon.range));
                var ability=content.Ability(p.Seat.spec,pending.AbilityEHeld||pending.AbilityE||pending.AbilityEReleased?"e":"q");
                pending.AbilityTarget=NativeArena.Vec(AimPoint(ability!=null?(float)ability.range:p.Seat.spec=="shield"?25:p.Seat.spec=="daggers"?48:60,p.Seat.spec=="shield"||p.Seat.spec=="daggers"));
            }
            presentation.SoundEnabled=prefs.sound;presentation.LocalId=1;
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
            presentation.RenderAbilities(arena,preview);
            presentation.GlowQuality=prefs.combatGlow=="Off"?0:prefs.combatGlow=="Low"?.4f:1;presentation.RenderGlow(arena,cameraView);
        }
        private void FixedUpdate()
        {
            if(arena==null||menu)return;timer.Restart();arena.Step(pending);Physics.Simulate(1f/60);timer.Stop();stepMs=timer.Elapsed.TotalMilliseconds;
            bool melee=arena.Local.Weapon.@class=="melee";pending=pending.HeldOnly();if(melee)pending.Shoot=false;
        }
        private Vector3 AimPoint(float range,bool thrownMelee=false)
        {
            Ray ray=cameraView.ViewportPointToRay(new Vector3(.5f,.5f));float nearest=range+(thrownMelee?0:Vector3.Distance(ray.origin,arena.Local.Body.position));
            Vector3 target=ray.origin+ray.direction*(thrownMelee?48:nearest);
            foreach(var hit in Physics.RaycastAll(ray,nearest,NativeArena.ShotMask,QueryTriggerInteraction.Ignore))
            {var pilot=hit.collider.GetComponent<NativePilotBody>();if(pilot!=null&&pilot.Pilot==arena.Local)continue;if(hit.distance<nearest){target=hit.point;nearest=hit.distance;}}
            return target;
        }
        private void UpdateCamera()
        {
            var p=arena.Local;Vector3 focus=p.Body.transform.position+Vector3.up*NativeViewRules.EyeHeight;var ads=content.Ads(p.Seat.spec);
            bool aim=ads!=null&&pending.Aim&&arena.Time>=p.ReloadEnd&&arena.Time>=p.RollEnd&&arena.Time>=p.BlockEnd&&!arena.Incapacitated(p);
            bool first=NativeViewRules.FirstPerson(prefs.firstPerson||aim,p.Hp>0&&!arena.Downed(p),menu);
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
            if(heading!=null)return;heading=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,wordWrap=true};
            label=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};small=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};button=new GUIStyle(GUI.skin.button){fontSize=16,wordWrap=true,fixedHeight=36};
        }
        private void OnGUI()
        {
            Styles();float scale=Mathf.Max(.65f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float width=Screen.width/scale,height=Screen.height/scale;
            if(error.Length>0){GUI.Box(new Rect(30,30,width-60,height-60),"");GUI.Label(new Rect(45,45,width-90,height-90),error,label);return;}
            if(arena!=null&&!menu&&match.Phase==NativePhase.Fight&&presentation.ScopeActive)DrawScope(width,height);
            GUI.Label(new Rect(22,15,width-44,35),"CHIP ARENA  /  NATIVE CONVERSION",heading);
            if(arena!=null)
            {
                var p=arena.Local;
                GUI.Box(new Rect(15,65,300,146),"");GUI.Label(new Rect(28,78,274,127),p.Seat.name+"  ·  "+p.Weapon.name+"\nHP "+p.Hp.ToString("0")+" / "+p.Stats["hp"].ToString("0")+"   STAMINA "+p.Stamina.ToString("0")+"\n"+(p.Weapon.@class=="magic"?"MANA "+p.Mana.ToString("0")+" / "+p.Stats["maxMana"].ToString("0"):p.Weapon.@class=="ranged"?"AMMO "+p.Ammo+" / "+Math.Ceiling(p.Weapon.ammo*p.Stats["ammoCapacity"]):"LMB attack  ·  RMB block")+"\n"+(p.ReloadEnd>arena.Time?"Reloading "+(p.ReloadEnd-arena.Time).ToString("0.0")+"s":"Block "+(p.BlockReady>arena.Time?(p.BlockReady-arena.Time).ToString("0.0")+"s":"ready")),label);
                GUI.Label(new Rect(26,216,355,70),AbilityStatus(p,"q")+"\n"+AbilityStatus(p,"e"),small);
                GUI.Label(new Rect(width-295,70,280,150),content.Map(prefs.map).name+"\nSET "+match.Set+"  ·  ROUND "+match.Round+"\n"+match.Phase+"  ·  "+arena.Bullets.Count+" projectiles\nV: "+(prefs.firstPerson?"First-person":"Third-person")+"\nNative step + PhysX "+stepMs.ToString("0.00")+" ms\n"+presentation.RenderingMode,small);
                GUI.Label(new Rect(20,height-42,width-40,30),"WASD move  ·  Double-tap W sprint  ·  LMB fire/attack  ·  Q/E abilities  ·  R reload  ·  V camera  ·  RMB block  ·  Shift roll  ·  Space jump  ·  Esc menu",small);
                if(!menu&&match.Phase==NativePhase.Fight)
                {
                    if(!presentation.ScopeActive)GUI.Label(new Rect(width/2-10,height/2-14,30,30),"+",heading);
                    if(arena.Downed(p))GUI.Label(new Rect(width/2-155,height/2+80,310,42),"RECOVERING  ·  WASD chooses roll direction",small);
                    if(arena.RiposteOpen(p))GUI.Label(new Rect(width/2-155,height/2+40,310,42),"RIPOSTE  ·  LMB counter",label);
                    if(p.Hp<=0)GUI.Label(new Rect(width/2-160,height/2-80,320,60),"ELIMINATED — round in progress",heading);
                    if(Time.unscaledTime<presentation.LastHitUntil)GUI.Label(new Rect(width/2+20,height/2-35,160,50),(presentation.LastHeadshot?"HEADSHOT ":"HIT ")+presentation.LastHitDamage.ToString("0.#"),label);
                }
                if(!menu&&(match.Phase==NativePhase.Countdown||match.Phase==NativePhase.Result))GUI.Label(new Rect(width/2-280,height/2-60,560,100),match.Phase==NativePhase.Countdown?"READY  "+Math.Ceiling(match.PhaseEnd-match.Time):match.Result,heading);
                if(!menu&&match.Phase==NativePhase.Match)
                {GUI.Label(new Rect(width/2-280,height/2-90,560,65),match.Result,heading);if(GUI.Button(new Rect(width/2-130,height/2,260,45),"Rematch",button))StartMatch();}
                if(!menu&&match.Phase==NativePhase.Draft)Draft(width,height);
                if(Input.GetKey(KeyCode.Tab))Board(width);
            }
            if(!menu&&arena!=null)return;
            float panel=Mathf.Min(850,width-60);GUI.Box(new Rect((width-panel)/2,68,panel,height-120),"");
            GUILayout.BeginArea(new Rect((width-panel)/2+22,86,panel-44,height-155));scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Native conversion · Melee specials & recovery",heading);
            GUILayout.Label("Native physics, first/third-person cameras, standard weapons and Q/E abilities, rounds and a stat-Chip draft. Full conversion is in progress.",label);
            if(arena!=null&&GUILayout.Button("Resume",button))menu=false;
            if(GUILayout.Button(showCatalogue?"Back to match setup":"Browse source catalogue (15 weapons / 236 Chips)",button))showCatalogue=!showCatalogue;
            if(showCatalogue)Catalogue();else Setup();
            GUILayout.Space(14);GUILayout.Label("Still pending: special Chip mechanics, DEV styles, Siren/PvE, production visuals/audio, multiplayer, accounts and cosmetics. Source definitions are included for reference; they are not all playable.",small);
            GUILayout.Label("The accepted ranged lab remains a separate project. This conversion uses its own settings and executable.",small);
            if(GUILayout.Button("Quit",button))Application.Quit();GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void Setup()
        {
            GUILayout.Label("Pilot name",label);prefs.name=GUILayout.TextField(prefs.name,20);
            GUILayout.Label("Weapon",label);int at=0;foreach(var w in content.weapons.Where(w=>!w.devOnly))
            {if(at%3==0)GUILayout.BeginHorizontal();if(GUILayout.Button((prefs.weapon==w.id?"✓ ":"")+w.name,button))prefs.weapon=w.id;at++;if(at%3==0)GUILayout.EndHorizontal();}if(at%3!=0)GUILayout.EndHorizontal();
            GUILayout.Label("Arena",label);at=0;foreach(var map in content.maps)
            {if(at%3==0)GUILayout.BeginHorizontal();if(GUILayout.Button((prefs.map==map.id?"✓ ":"")+map.name,button))prefs.map=map.id;at++;if(at%3==0)GUILayout.EndHorizontal();}if(at%3!=0)GUILayout.EndHorizontal();
            GUILayout.Label("Bots: "+prefs.bots+"  (up to 8 total pilots)",label);prefs.bots=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.bots,1,7));
            prefs.mirrorWeapons=GUILayout.Toggle(prefs.mirrorWeapons,"Bots use my weapon (otherwise mixed loadouts)");
            prefs.teams=GUILayout.Toggle(prefs.teams,"Teams — alternating red / blue sides");prefs.friendlyFire=GUILayout.Toggle(prefs.friendlyFire,"Friendly fire");prefs.stationary=GUILayout.Toggle(prefs.stationary,"Stationary targets");prefs.skipBotDrafts=GUILayout.Toggle(prefs.skipBotDrafts,"Skip bot draft screens");prefs.sound=GUILayout.Toggle(prefs.sound,"Gun/reload/hit sounds");
            bool first=GUILayout.Toggle(prefs.firstPerson,"First-person camera (V switches during play)");if(first!=prefs.firstPerson){prefs.firstPerson=first;Save();}
            GUILayout.BeginHorizontal();GUILayout.Label("Combat glow",label);
            foreach(string quality in new[]{"Off","Low","Full"})if(GUILayout.Button((prefs.combatGlow==quality||string.IsNullOrEmpty(prefs.combatGlow)&&quality=="Full"?"✓ ":"")+quality,button)){prefs.combatGlow=quality;Save();}GUILayout.EndHorizontal();
            GUILayout.Label("Melee E: special attack. Gun Q: hold to aim. Magic Q / E: cast abilities. Hold E to preview grenade, meteor, geysers or black hole; release to cast.",small);
            GUILayout.Label("Rounds to win a set: "+prefs.rounds,label);prefs.rounds=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.rounds,1,5));GUILayout.Label("Sets to win the match: "+prefs.sets,label);prefs.sets=Mathf.RoundToInt(GUILayout.HorizontalSlider(prefs.sets,1,10));
            if(GUILayout.Button("Start native match",button))StartMatch();
        }
        private void Catalogue()
        {
            GUILayout.Label("Reference catalogue — importing a definition does not enable its mechanic",label);search=GUILayout.TextField(search);
            catalogueScroll=GUILayout.BeginScrollView(catalogueScroll,GUILayout.Height(360));
            foreach(var w in content.weapons.Where(w=>(w.name+" "+w.id).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){GUILayout.Label(w.name+(w.devOnly?" — DEV implementation pending":" — primary test"),label);GUILayout.Label(w.description,small);}
            foreach(var c in content.chips.Where(c=>(c.name+" "+c.description).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){GUILayout.Label(c.name+"  /  "+c.rarity+(NativeArena.ImplementedChip(c)?"  /  stat draft enabled":"  /  pending"),label);GUILayout.Label(c.description,small);}
            GUILayout.EndScrollView();
        }
        private void Draft(float width,float height)
        {
            var seat=match.Seat(match.DraftSeat);if(seat==null)return;float cardWidth=Mathf.Min(260,(width-70)/3);
            GUI.Box(new Rect(20,230,width-40,height-300),"");GUI.Label(new Rect(38,245,width-76,55),seat.name+" chooses a Chip  ·  "+Math.Ceiling(match.PhaseEnd-match.Time)+"s",heading);
            GUI.Label(new Rect(38,294,width-76,35),"This milestone drafts implemented stat Chips. Special mechanics are tracked in the catalogue.",small);
            for(int i=0;i<seat.offers.Count;i++)
            {
                var chip=content.Chip(seat.offers[i]);float x=40+(i%3)*(cardWidth+12),y=335+(i/3)*180;
                GUI.Box(new Rect(x,y,cardWidth,170),"");GUI.Label(new Rect(x+10,y+8,cardWidth-20,40),chip.name,label);GUI.Label(new Rect(x+10,y+48,cardWidth-20,78),chip.description,small);
                GUI.enabled=!seat.bot&&match.Time>=match.ReadyAt&&match.Chosen==null;if(GUI.Button(new Rect(x+10,y+132,cardWidth-20,30),match.Chosen==chip.id?"Installed":"Install",button))match.Choose(seat.id,chip.id);GUI.enabled=true;
            }
        }
        private void Board(float width)
        {GUI.Box(new Rect(width/2-240,80,480,40+match.Seats.Count*30),"");int row=0;foreach(var p in match.Seats)GUI.Label(new Rect(width/2-220,94+row++*30,440,28),p.name+"  ·  "+p.spec+"  ·  sets "+p.score+" / rounds "+p.roundWins+"  ·  "+(p.alive?"alive":"out"),label);}
        private void OnDestroy(){if(scopeMask!=null)CapturedAnimation.Release(scopeMask);arena?.Dispose();if(ownsPhysics){Physics.simulationMode=previousSimulation;Physics.gravity=previousGravity;}Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
