using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Time = UnityEngine.Time;

namespace ChipArena.UnityPrototype
{
    public sealed class ArenaGame : MonoBehaviour
    {
        private enum Session { Menu, Solo, Host, Guest }
        private Session session;
        private ArenaWorld world;
        private Snapshot snapshot;
        private ArenaPresentation presentation;
        private Camera view;
        private LanLink link;
        private InputFrame pending, remote;
        private int sequence, lastRemoteSequence = -1;
        private float yaw, pitch = 20, lastW = -1, lastRemoteAt, lastStateAt, lastStatsAt;
        private bool sprint, menu = true, invulnerablePractice;
        private string address = "127.0.0.1", portText = "27880", message = "", lanAddresses = "", capturePath = "";
        private int botCount = 8;
        private double stepMs;
        private readonly float[] frames = new float[600], acknowledgments = new float[512];
        private int frameAt, frameCount;
        private float meanMs, p95Ms, ackMs;
        private readonly Stopwatch timer = new Stopwatch();
        private readonly List<string> captureRows = new List<string>();
        private float captureUntil;
        private GUIStyle title, text, small, button;
        private readonly Color accent = new Color(.2f,.9f,.8f);
        private Texture2D white;
        private Vector2 menuScroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<ArenaGame>() == null) new GameObject("Chip Arena Unity Prototype").AddComponent<ArenaGame>();
        }
        private void Awake()
        {
            Application.runInBackground=true; Application.targetFrameRate=144; QualitySettings.vSyncCount=0;
            Time.fixedDeltaTime=Rules.Dt; Time.maximumDeltaTime=.1f;
            presentation=gameObject.AddComponent<ArenaPresentation>(); presentation.Initialize();
            var cameraObject=new GameObject("Prototype camera"); cameraObject.transform.SetParent(transform);
            view=cameraObject.AddComponent<Camera>(); view.tag="MainCamera"; view.clearFlags=CameraClearFlags.SolidColor;
            view.backgroundColor=new Color(.018f,.025f,.048f); view.nearClipPlane=.08f; view.farClipPlane=120; view.fieldOfView=65;
            world=new ArenaWorld(); world.SetBots(botCount); snapshot=world.Capture();
            lastStateAt=Time.unscaledTime;
            try
            {
                foreach(var a in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if(a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!System.Net.IPAddress.IsLoopback(a)) lanAddresses+=a+"  ";
            }
            catch(System.Net.Sockets.SocketException) { lanAddresses="Use the host PC's IPv4 address."; }
        }
        private Actor Local()
        {
            if(snapshot==null) return null;
            int id=session==Session.Guest?2:1;
            foreach(var a in snapshot.Actors) if(a.Id==id) return a; return null;
        }
        private void Begin(Session next)
        {
            int port;
            if(!int.TryParse(portText,out port)||port<1024||port>65535) { message="Use a port from 1024 to 65535."; return; }
            StopLink(); presentation.ResetPresentation();
            sequence=0; lastRemoteSequence=-1; pending=remote=new InputFrame(); sprint=false; ackMs=0;
            captureUntil=0; captureRows.Clear(); frameCount=frameAt=0;
            world=new ArenaWorld(); world.SetBots(botCount); world.BotAttacks=!invulnerablePractice;
            snapshot=world.Capture();
            try
            {
                if(next==Session.Host) link=LanLink.Host(port);
                if(next==Session.Guest) { link=LanLink.Join(address,port); snapshot=null; }
                session=next; menu=false; message=""; yaw=0; lastStateAt=Time.unscaledTime;
                Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
            }
            catch(Exception e) { session=Session.Menu; menu=true; message=e.Message; StopLink(); }
        }
        private void Update()
        {
            PollNetwork();
            if(Input.GetKeyDown(KeyCode.Escape)) ToggleMenu();
            if(Input.GetKeyDown(KeyCode.F8)&&session!=Session.Menu) StartCapture();
            if(!menu&&session!=Session.Menu)
            {
                yaw+=Input.GetAxisRaw("Mouse X")*2; pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f,5,65);
                if(Input.GetKeyDown(KeyCode.W)) { if(Time.unscaledTime-lastW<.3f) sprint=true; lastW=Time.unscaledTime; }
                if(!Input.GetKey(KeyCode.W)) sprint=false;
                float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
                float z=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
                Vector3 move=Quaternion.Euler(0,yaw,0)*new Vector3(x,0,z).normalized;
                pending.X=move.x; pending.Z=move.z; pending.Yaw=yaw;
                pending.Attack|=Input.GetMouseButtonDown(0); pending.Block|=Input.GetMouseButtonDown(1);
                pending.Roll|=Input.GetKeyDown(KeyCode.LeftShift); pending.Jump|=Input.GetKeyDown(KeyCode.Space);
                pending.Shoot=Input.GetKey(KeyCode.E); pending.Sprint=sprint;
            }
            else pending=new InputFrame { Yaw=yaw };
            if(snapshot!=null)
            {
                float t=snapshot.Tick*Rules.Dt+(session==Session.Guest?Mathf.Min(.1f,Time.unscaledTime-lastStateAt):0);
                presentation.Render(snapshot,t,session==Session.Guest);
            }
            UpdateCamera(); UpdateMetrics();
        }
        private void FixedUpdate()
        {
            if(session==Session.Menu||menu&&session==Session.Solo) return;
            pending.Sequence=++sequence;
            if(session==Session.Guest)
            {
                if(link!=null&&link.Connected) { acknowledgments[sequence%acknowledgments.Length]=Time.unscaledTime; link.Send(WireCodec.Input(pending)); }
                pending=pending.HeldOnly(); return;
            }
            bool connected=link!=null&&link.Connected;
            if(session==Session.Host)
            {
                if(connected) world.AddHuman(2); else { world.RemoveGuest(); remote=new InputFrame(); }
            }
            if(Time.unscaledTime-lastRemoteAt>.3f) remote=new InputFrame { Yaw=remote.Yaw };
            timer.Restart(); world.Step(pending,remote); timer.Stop(); stepMs=timer.Elapsed.TotalMilliseconds;
            pending=pending.HeldOnly(); remote=remote.HeldOnly(); snapshot=world.Capture();
            if(connected&&world.Tick%3==0) link.Send(WireCodec.State(snapshot));
        }
        private void PollNetwork()
        {
            if(link==null) return;
            try
            {
                byte[] data; int processed=0;
                while(processed++<120&&(data=link.Receive())!=null)
                {
                    if(session==Session.Host)
                    {
                        var input=WireCodec.ReadInput(data); if(input.Sequence<=lastRemoteSequence) continue;
                        lastRemoteSequence=input.Sequence;
                        input.Attack|=remote.Attack; input.Block|=remote.Block; input.Roll|=remote.Roll; input.Jump|=remote.Jump;
                        remote=input; lastRemoteAt=Time.unscaledTime;
                    }
                    else
                    {
                        var state=WireCodec.ReadState(data); if(snapshot!=null&&state.Tick<=snapshot.Tick) continue;
                        snapshot=state; lastStateAt=Time.unscaledTime; var actor=Local();
                        if(actor!=null&&actor.LastInput>0&&sequence-actor.LastInput>=0&&sequence-actor.LastInput<acknowledgments.Length)
                            ackMs=(Time.unscaledTime-acknowledgments[actor.LastInput%acknowledgments.Length])*1000;
                    }
                }
            }
            catch(Exception e) { message="Network packet rejected: "+e.Message; StopLink(); menu=true; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
            if(session==Session.Guest&&link!=null&&!link.Connected&&link.Status.StartsWith("Disconnected",StringComparison.Ordinal))
            { message=link.Status; menu=true; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        }
        private void UpdateCamera()
        {
            Actor a=Local(); Vector3 target=a==null?new Vector3(0,1,0):presentation.Position(a)+Vector3.up*1.15f;
            if(session==Session.Menu)
            {
                view.transform.position=new Vector3(Mathf.Sin(Time.unscaledTime*.07f)*27,22,-27); view.transform.LookAt(new Vector3(0,0,2)); return;
            }
            Vector3 offset=Quaternion.Euler(pitch,yaw,0)*new Vector3(.7f,.4f,-6);
            Vector3 desired=target+offset;
            RaycastHit hit;
            if(Physics.SphereCast(target,.15f,offset.normalized,out hit,offset.magnitude,~0,QueryTriggerInteraction.Ignore)) desired=target+offset.normalized*Mathf.Max(.5f,hit.distance-.12f);
            view.transform.position=Vector3.Lerp(view.transform.position,desired,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            view.transform.rotation=Quaternion.LookRotation(target+Quaternion.Euler(0,yaw,0)*Vector3.forward*3-view.transform.position);
        }
        private void ToggleMenu()
        {
            if(session==Session.Menu) { menu=true; return; }
            menu=!menu; Cursor.lockState=menu?CursorLockMode.None:CursorLockMode.Locked; Cursor.visible=menu;
            if(menu&&captureUntil>0) { captureUntil=0; captureRows.Clear(); message="Capture cancelled because the menu opened. Start a new uninterrupted capture."; }
            pending=new InputFrame { Yaw=yaw };
        }
        private void UpdateMetrics()
        {
            if(session==Session.Menu||menu&&session==Session.Solo) return;
            float frame=Time.unscaledDeltaTime*1000;
            frames[frameAt++%frames.Length]=frame; frameCount=Mathf.Min(frameCount+1,frames.Length);
            if(Time.unscaledTime-lastStatsAt>1&&frameCount>0)
            {
                lastStatsAt=Time.unscaledTime; float[] sorted=new float[frameCount]; Array.Copy(frames,sorted,frameCount); Array.Sort(sorted);
                float sum=0; foreach(float f in sorted) sum+=f; meanMs=sum/frameCount; p95Ms=sorted[Mathf.Clamp(Mathf.CeilToInt(frameCount*.95f)-1,0,frameCount-1)];
            }
            if(captureUntil>0)
            {
                if(Time.unscaledTime>=captureUntil) { FinishCapture(); return; }
                if(captureRows.Count<20000) captureRows.Add(string.Join(",",Time.unscaledTime.ToString("F3",CultureInfo.InvariantCulture),frame.ToString("F3",CultureInfo.InvariantCulture),
                    session==Session.Guest?"":stepMs.ToString("F3",CultureInfo.InvariantCulture),snapshot?.Actors.Length??0,snapshot?.Shots.Length??0,presentation.DebrisCount,presentation.ParticleCount));
            }
        }
        private void StartCapture()
        {
            if(captureUntil>0) return;
            captureRows.Clear(); captureUntil=Time.unscaledTime+30; message="Recording 30 seconds of frame times. F8 starts another capture after completion.";
            menu=false; Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
        }
        private void FinishCapture()
        {
            captureUntil=0;
            try
            {
                string folder=Path.Combine(Application.persistentDataPath,"Benchmarks"); Directory.CreateDirectory(folder);
                capturePath=Path.Combine(folder,"unity-prototype-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".csv");
                var data=new StringBuilder(); data.AppendLine("# Prototype workload; NOT a full-game benchmark. Frame time is wall time, not GPU duration.");
                data.AppendLine("# Unity "+Application.unityVersion+" | "+SystemInfo.operatingSystem+" | "+SystemInfo.processorType+" | "+SystemInfo.graphicsDeviceName);
                data.AppendLine("# Mode: "+session+" | Resolution: "+Screen.width+"x"+Screen.height+" | Target FPS: "+Application.targetFrameRate+" | VSync: "+QualitySettings.vSyncCount);
                data.AppendLine("elapsed_seconds,frame_ms,authority_step_ms,actors,projectiles,physx_debris,particles");
                foreach(string row in captureRows) data.AppendLine(row); File.WriteAllText(capturePath,data.ToString()); message="Benchmark saved. Open menu to copy its path.";
            }
            catch(Exception e) { message="Could not save benchmark: "+e.Message; }
        }
        private void Styles()
        {
            if(title!=null) return; white=Texture2D.whiteTexture;
            title=new GUIStyle(GUI.skin.label) { fontSize=28,fontStyle=FontStyle.Bold,normal={textColor=Color.white} };
            text=new GUIStyle(GUI.skin.label) { fontSize=15,wordWrap=true,normal={textColor=new Color(.82f,.87f,.94f)} };
            small=new GUIStyle(text) { fontSize=12 }; button=new GUIStyle(GUI.skin.button) { fontSize=15,fixedHeight=35 };
        }
        private void Panel(Rect r,Color color) { Color before=GUI.color; GUI.color=color; GUI.DrawTexture(r,white); GUI.color=before; }
        private void Bar(float y,string label,float amount,Color color)
        {
            GUI.Label(new Rect(22,y,230,22),label,small); Panel(new Rect(22,y+22,220,5),new Color(.2f,.24f,.31f)); Panel(new Rect(22,y+22,220*Mathf.Clamp01(amount),5),color);
        }
        private void OnGUI()
        {
            Styles(); float scale=Mathf.Max(.65f,Screen.height/900f); GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            float width=Screen.width/scale,height=Screen.height/scale;
            Panel(new Rect(0,0,width,56),new Color(.025f,.035f,.060f,.94f));
            GUI.Label(new Rect(22,10,500,35),"CHIP ARENA  /  UNITY LAB",title);
            GUI.Label(new Rect(width-310,21,295,25),"ISOLATED PROTOTYPE  •  NO GAME UPDATES",small);
            if(session!=Session.Menu)
            {
                var a=Local(); Panel(new Rect(10,70,248,167),new Color(.025f,.035f,.06f,.85f));
                if(a!=null)
                {
                    float t=snapshot.Tick*Rules.Dt;
                    Bar(82,"HEALTH  "+Mathf.CeilToInt(a.Hp)+" / 100",a.Hp/100,new Color(.4f,1,.65f));
                    Bar(126,"STAMINA  "+Mathf.CeilToInt(a.Stamina)+" / 100"+(a.Sprinting?"  SPRINT":""),a.Stamina/100,accent);
                    Bar(170,"BLOCK  "+(a.BlockReady<=t?"READY":Mathf.Max(0,a.BlockReady-t).ToString("F1")+"s"),1-Mathf.Max(0,a.BlockReady-t)/Rules.BlockCooldown,new Color(.5f,.7f,1));
                    if(!a.Alive) GUI.Label(new Rect(width/2-140,height/2,280,35),"RESPAWNING...",title);
                }
                Panel(new Rect(width-300,70,285,172),new Color(.025f,.035f,.06f,.85f));
                GUI.Label(new Rect(width-287,82,260,155),$"{session}  ·  {snapshot?.Actors.Length??0} actors\nFrame: {meanMs:F1} ms mean / {p95Ms:F1} ms p95\n"+
                    (session==Session.Guest?$"Input acknowledgment: {ackMs:F0} ms\n":"Authority step: "+stepMs.ToString("F2")+" ms\n")+
                    $"Shots: {snapshot?.Shots.Length??0}  ·  Debris: {presentation.DebrisCount}\nParticles: {presentation.ParticleCount}  ·  Parts: {presentation.DrawnParts}\n"+(link?.Status??"Local simulation"),small);
                GUI.Label(new Rect(22,height-48,width-44,30),"WASD move   ·   Double-tap W sprint   ·   Mouse look   ·   LMB sword   ·   RMB block   ·   Shift dodge   ·   Space jump   ·   E bolts   ·   F8 record   ·   Esc menu",small);
                Panel(new Rect(width/2-1,height/2-6,2,12),Color.white); Panel(new Rect(width/2-6,height/2-1,12,2),Color.white);
                if(captureUntil>0) GUI.Label(new Rect(width/2-120,73,300,28),"RECORDING  "+Mathf.CeilToInt(captureUntil-Time.unscaledTime)+"s",text);
            }
            if(!menu) return;
            float panelWidth=570,panelX=(width-panelWidth)/2;
            Panel(new Rect(panelX,88,panelWidth,height-156),new Color(.025f,.035f,.062f,.97f));
            GUILayout.BeginArea(new Rect(panelX+26,108,panelWidth-52,height-200));
            menuScroll=GUILayout.BeginScrollView(menuScroll);
            GUILayout.Label(session==Session.Menu?"Test the next foundation":"Test session",title);
            GUILayout.Label("One arena. Sword, block, dodge, bolts and breakable cover. Procedural placeholder animation; no purchased packs included.",text);
            GUILayout.Space(12);
            if(session!=Session.Menu&&GUILayout.Button("Resume",button)) ToggleMenu();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Play solo",button)) Begin(Session.Solo);
            if(GUILayout.Button("Host LAN",button)) Begin(Session.Host);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Host IPv4",small,GUILayout.Width(75)); address=GUILayout.TextField(address,45,GUILayout.Width(240));
            GUILayout.Label("Port",small,GUILayout.Width(35)); portText=GUILayout.TextField(portText,5); GUILayout.EndHorizontal();
            if(GUILayout.Button("Join LAN / second local window",button)) Begin(Session.Guest);
            GUILayout.Label("Host addresses: "+lanAddresses,small);
            GUILayout.Label("LAN is a synchronization test: 2 players, trusted network, no prediction or internet matchmaking. Host must create a new session after disconnect.",small);
            GUILayout.Space(12); GUILayout.Label("Crowd workload",text);
            GUI.enabled=session!=Session.Guest;
            GUILayout.BeginHorizontal();
            foreach(int n in new[] { 0,8,32,96,192 }) if(GUILayout.Button(n+(n==botCount?" ✓":""),button)) { botCount=n; world.SetBots(n); snapshot=world.Capture(); }
            GUILayout.EndHorizontal();
            bool harmless=GUILayout.Toggle(invulnerablePractice,"Movement-only crowd (bots do not attack)");
            if(harmless!=invulnerablePractice) { invulnerablePractice=harmless; if(world!=null) world.BotAttacks=!harmless; }
            GUI.enabled=true;
            presentation.DebrisEnabled=GUILayout.Toggle(presentation.DebrisEnabled,"PhysX debris on kills and broken cover");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("60 FPS",button)) Application.targetFrameRate=60;
            if(GUILayout.Button("144 FPS",button)) Application.targetFrameRate=144;
            if(GUILayout.Button("Uncapped",button)) Application.targetFrameRate=-1;
            GUILayout.EndHorizontal();
            GUI.enabled=session!=Session.Menu&&captureUntil<=0;
            if(GUILayout.Button("Record 30-second benchmark (F8)",button)) StartCapture(); GUI.enabled=true;
            if(capturePath.Length>0&&GUILayout.Button("Copy last benchmark path",button)) GUIUtility.systemCopyBuffer=capturePath;
            GUILayout.Label("Frame time includes frame limiting. Authority timing excludes rendering, serialization and PhysX. This is a simplified prototype workload, not production parity.",small);
            if(message.Length>0) GUILayout.Label(message,text);
            GUILayout.Space(6);
            if(session!=Session.Menu&&GUILayout.Button("End session",button)) { StopLink(); session=Session.Menu; menu=true; presentation.ResetPresentation(); world=new ArenaWorld(); world.SetBots(botCount); snapshot=world.Capture(); }
            if(GUILayout.Button("Quit",button)) Application.Quit();
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void StopLink() { link?.Dispose(); link=null; }
        private void OnDestroy() { StopLink(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        private void OnApplicationQuit() { StopLink(); }
    }
}
