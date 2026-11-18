using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NQuat=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition drop pods crash in (Core NativePodDrop, NativePodModels, NativePodSounds; docs/EXPEDITION-OVERHAUL.md R7,
    /// W4) in a generated run's hub, in place of the spawn-in beam:
    ///  - NativeSpawnInPresentation.SpawnEvaluate hands a pod run's pilots here (PodSpawn): each pilot rides hidden in its pod
    ///    (no hologram, no beam, no HUD) until the bolts blow the door, then is drawn strapped in on the pod's full offset and lean
    ///    (PodFeet, PodRideTilt), unbuckles and stands up on its capsule by GO; posed standing throughout and held standing through
    ///    GO (no Fall loop in the doorway, no Land take or landing thud at GO: the GO fallback until A0 grounds the countdown).
    ///  - ExpeditionPods (NativeSourcePresentation, beside the PvE machines) queues each pod, its door and its harness bar from the
    ///    source-model bank in its look (paint, trim, lights; the cabin lamp, the retro throats, the ablative tiles cooling from
    ///    white hot to char, the bolts' flash), lays the LED name sign over the marquee, and draws its crater and furrow for the
    ///    run (flat, from the record: a late joiner and the killcam see the same marks and the same cooling hull).
    ///  - PodFx (RenderGlow) emits the crash's effects into the shared effects renderer, its burning streak as an unfogged
    ///    meteor ribbon (NativeGlow), the impact's chunks through the destruction kit (Slam) and its flash through the blast
    ///    lights (PodFlashes: they light the city and survive DARKNESS); PodCues are the sounds NativeAudio plays (NativePodAudio).
    ///  - PodDropDirect (NativeCinematicCamera's Countdown branch) directs my own camera over my pod's drop, with its own ride and
    ///    shake (none with Camera shake off; Reduced effects: a still wide shot), and hands back to the game's camera at GO
    ///    (PodHandOff blends the last of it into the game's own pose).
    /// Each pod's crash is its record's (NativePodDrop.Plan: seat, order, x, z, end, yaw in degrees) and the opening it spans
    /// (today's 3 s countdown, or the 5 s PodOpening once A0 lands: OpeningFor reads the countdown it sees). Each pod stays parked
    /// where it landed for the run (the pilot's way out: leaving by pod is Astra's rule; NativePodDrop.Launch is its look,
    /// PodLaunch the hook). Presentation only: no rule, timing, collision or network field reads it; the capsule never moves.
    /// Nothing allocates per frame once a pod is made.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private sealed class PodView
        {
            public bool Authority;public NativeSeat AuthoritySeat;public int Seat,Order;public double End=double.NaN;public Vector3 Spot,Facing;public float Floor,Yaw,X,Z;
            public float Earliest=float.NaN;public bool Planned;public PodPlan Plan;public float PlanOpening=float.NaN;
            public string Text;public Texture2D Sign;public bool Scrolls;public float Columns;
            public PodFrame Frame;public double LaunchAt=double.NaN;public Color Paint,Trim,Light;public string LookKey,LookBody,LookAccent,SignName;public bool Looked;
            public bool Slammed;public readonly NativePodSounds.Cue[] Cues=new NativePodSounds.Cue[NativePodSounds.MaxCues];public int CueCount;public float CueClock=float.NaN;
        }
        /// <summary>A pod sound due this frame (NativePodAudio plays it): which, where, its gain at the source, the distance it
        /// carries to, its rate, and whether it travels at the speed of sound (the cloud punch's boom).</summary>
        public struct PodSound{public int Sound;public Vector3 At;public float Gain,Range,Rate;public bool Travels;}
        private readonly Dictionary<int,PodView> pods=new Dictionary<int,PodView>();private object podRun;
        private Material podSignMaterial;private Mesh podQuad;private bool podFailed;private MaterialPropertyBlock podBlock;
        private PodEntranceFx podFx;private PodView podNow;private Func<int,Color> podTint,podMarkTint;private Func<int,Matrix4x4> podBone;private Matrix4x4 podRoot,podHarness;
        private readonly List<int> podGone=new List<int>();
        private readonly SpawnGeometry podGlow=new SpawnGeometry();private Mesh podGlowMesh;private Material podGlowMaterial;private bool podGlowFailed;
        private readonly List<PodSound> podCues=new List<PodSound>(32);
        /// <summary>The pod sounds due this frame (null when no pod is in the arena), for NativeAudio.PodFrame.</summary>
        public List<PodSound> PodCues=>pods.Count>0?podCues:null;
        private static readonly int PodTexId=Shader.PropertyToID("_MainTex"),PodTintId=Shader.PropertyToID("_Tint"),PodRepId=Shader.PropertyToID("_Rep"),PodScrollId=Shader.PropertyToID("_Scroll"),
            PodBlurId=Shader.PropertyToID("_Blur"),PodGainId=Shader.PropertyToID("_Gain"),PodSweepId=Shader.PropertyToID("_Sweep"),PodChromaId=Shader.PropertyToID("_Chroma"),PodHoloId=Shader.PropertyToID("_Holo");
        /// <summary>Fallback for a preview without a seat look; live pods use the authoritative seat value.</summary>
        public string LocalPodLook="";
        /// <summary>The player's own camera pick (Settings · first person), which the directed drop follows (ConversionGame sets it
        /// every frame: FirstPerson itself is the director's while it directs).</summary>
        public bool FirstPersonPick;
        /// <summary>The LED dot size in texture pixels; how long after a countdown-less start a pilot's pod is still parked where
        /// the pilot stands; how long after GO the drawn pilot is held standing on its capsule while the capsule settles.</summary>
        private const int PodDot=8;private const float ParkWindow=1.5f,PodSettle=.45f;

        /// <summary>Pods fall in this arena: a generated run's hub (its world's map).</summary>
        private bool PodRun(NativeArena arena)=>arena?.Map!=null&&GeneratedRun?.World?.Map!=null&&arena.Map.id==GeneratedRun.World.Map.id;

        // ---- the spawn-in's pod path ----
        /// <summary>SpawnEvaluate in a pod run: the pilot in its pod over the drop (hidden until the door blows, then strapped in,
        /// then standing), held standing on its capsule a moment after GO; null once it is over.</summary>
        private SpawnView PodSpawn(NativeArena arena,NativePilot pilot,Vector3 feet,float age,double end,out float raise)
        {
            raise=0;SyncAuthorityPods(arena);int id=pilot.Seat.id;
            if(pods.TryGetValue(id,out var authority)&&authority.Authority)
            {
                end=authority.End;age=(float)(PresentationClock(arena)-end);
                if(!double.IsNaN(authority.LaunchAt))
                {
                    if(!spawnViews.TryGetValue(id,out var boarding)){boarding=new SpawnView();spawnViews.Add(id,boarding);}
                    if(boarding.Arena!=arena||boarding.End!=end)SpawnPlaceView(boarding,arena,pilot,feet,end);
                    boarding.Pod=true;boarding.Frame=new SpawnInFrame{Active=true,Age=age,Front=-1e4f,Held=true,Land=-1};return boarding;
                }
            }
            else if(arena.Match!=null&&arena.Match.Phase==NativePhase.Countdown)age=(float)(PresentationClock(arena)-end);
            if(float.IsNaN(age))return null;
            // A preview that opens without a countdown still parks each pilot's pod where it starts, seen in its first seconds;
            // the drop itself plays only over a countdown.
            if(age>=PodSettle)
            {
                if(age<ParkWindow+PodSettle&&(!pods.TryGetValue(id,out var seen)||double.IsNaN(seen.End)||Math.Abs(seen.End-end)>=.25))
                {if(!spawnViews.TryGetValue(id,out var pv)){pv=new SpawnView();spawnViews.Add(id,pv);}if(pv.Arena!=arena||double.IsNaN(pv.End)||Math.Abs(pv.End-end)>.25)SpawnPlaceView(pv,arena,pilot,feet,end);PodPlace(arena,pilot,pv,end,age);}
                return null;
            }
            if(!spawnViews.TryGetValue(id,out var v)){v=new SpawnView();spawnViews.Add(id,v);}
            if(v.Arena!=arena||double.IsNaN(v.End)||Math.Abs(v.End-end)>.25)SpawnPlaceView(v,arena,pilot,feet,end);
            var pod=PodPlace(arena,pilot,v,end,age);var f=pod.Frame=NativePodDrop.Evaluate(age,PodPlanOf(pod));v.Pod=true;
            v.Frame=new SpawnInFrame{Active=true,Age=age,Built=f.Shown?1:0,Front=f.Shown?1e4f:-1e4f,Held=true,Land=-1};
            return v;
        }
        // The pilot's pod for this drop: where it lands (the spawn, on the floor the spawn-in found), which way its door faces
        // (the pilot's yaw, in degrees), its place in the landing order; and the earliest countdown seen for it (its opening).
        private PodView PodPlace(NativeArena arena,NativePilot pilot,SpawnView v,double end,float age)
        {
            PodSync();int id=pilot.Seat.id;if(!pods.TryGetValue(id,out var pod)){pod=new PodView();pods.Add(id,pod);}
            if(!pod.Authority&&(double.IsNaN(pod.End)||Math.Abs(pod.End-end)>=.25))
            {
                int order=0;foreach(var s in arena.Match.Seats)if(s.connected&&!s.AuthorityBoss&&s.id<id)order++;
                var face=Quaternion.Euler(0,pilot.Yaw,0)*Vector3.forward;face.y=0;face=face.sqrMagnitude>1e-4f?face.normalized:Vector3.forward;
                pod.Seat=id;pod.Order=order;pod.End=end;pod.LaunchAt=double.NaN;pod.Yaw=Mathf.Atan2(face.x,face.z)*Mathf.Rad2Deg;pod.X=v.Place.Centre.X;pod.Z=v.Place.Centre.Z;
                var c=NativePodDrop.Centre(pod.X,v.Place.Floor,pod.Z,pod.Yaw);pod.Spot=new Vector3(c.X,c.Y,c.Z);pod.Floor=v.Place.Floor;pod.Facing=face;pod.Earliest=float.NaN;pod.Planned=false;pod.Slammed=false;pod.CueClock=float.NaN;
            }
            if(arena.Match!=null&&arena.Match.Phase==NativePhase.Countdown&&!float.IsNaN(age)&&!(age>=pod.Earliest))pod.Earliest=age;
            return pod;
        }
        // The pod's crash plan, made (again) when its opening is first known or a longer countdown is seen.
        private PodPlan PodPlanOf(PodView pod)
        {
            float opening=NativePodDrop.OpeningFor(pod.Earliest);
            if(!pod.Planned||pod.PlanOpening!=opening)
            {
                pod.Plan=NativePodDrop.Plan(pod.Seat,pod.Order,pod.X,pod.Z,pod.End,pod.Yaw,opening);pod.PlanOpening=opening;pod.Planned=true;
                pod.CueCount=NativePodSounds.Plan(pod.Plan,pod.Cues);
            }
            return pod.Plan;
        }
        // A new run (or none) clears the pods.
        private void PodSync(){if(!ReferenceEquals(podRun,GeneratedRun)){podRun=GeneratedRun;foreach(var p in pods.Values)if(p.Sign!=null)CapturedAnimation.Release(p.Sign);pods.Clear();}}
        private void DisposePods()
        {
            podRun=new object();PodSync();if(podSignMaterial!=null)CapturedAnimation.Release(podSignMaterial);if(podQuad!=null)CapturedAnimation.Release(podQuad);podSignMaterial=null;podQuad=null;
            if(podGlowMesh!=null)CapturedAnimation.Release(podGlowMesh);if(podGlowMaterial!=null)CapturedAnimation.Release(podGlowMaterial);podGlowMesh=null;podGlowMaterial=null;
            if(blasts!=null)blasts.PodFlashes=null;
        }
        /// <summary>SpawnPlace in a pod run: the drawn pilot's feet on the pod's full offset (strapped in, then standing on its
        /// capsule), held there after GO until the capsule has settled onto the floor (or PodSettle passes); then the presented
        /// position again.</summary>
        private Vector3 PodFeet(Actor a,SpawnView v,Vector3 position)
        {
            if(!pods.TryGetValue(a.Id,out var pod)){v.Shown=false;return position;}
            // boarding to leave: the pilot is hidden where the authority holds it (the boarding view's front is under its feet)
            if(!double.IsNaN(pod.LaunchAt)){v.Shown=true;v.Raise=0;return position;}
            if(!pod.Planned){v.Shown=false;return position;}
            var f=pod.Frame;
            if(f.Age>=0)
            {
                // GO: the capsule falls to the floor's rest (today it floats .23 m through the countdown); the drawn pilot waits on it
                var rest=pod.Spot+PodV(NativePodDrop.Capsule(pod.Plan));if(f.Age>=PodSettle||a.Grounded&&Mathf.Abs(position.y-rest.y)<.015f){v.Shown=false;return position;}
                v.Shown=true;v.Raise=0;return new Vector3(position.x,rest.y,position.z);
            }
            v.Shown=true;v.Raise=0;return pod.Spot+PodV(f.Pilot);
        }
        /// <summary>PrepareCapturedPose: the drawn pilot leans with its pod while strapped in, upright once it stands (about its feet).</summary>
        private void PodRideTilt(CapturedAnimation.Player rig,Actor a)
        {
            if(pods.Count==0||!spawnViews.TryGetValue(a.Id,out var v)||!v.Pod||!v.Shown||!pods.TryGetValue(a.Id,out var pod)||!visualPositions.TryGetValue(a.Id,out var feet))return;
            var q=pod.Frame.PilotTilt;if(Mathf.Abs(q.W)>.99999f)return;var turn=new Quaternion(q.X,q.Y,q.Z,q.W);var root=rig.Root.transform;
            root.SetPositionAndRotation(feet+turn*(root.position-feet),turn*root.rotation);
        }
        /// <summary>The footfalls wait while a pod holds its pilot: no steps while it rides, no landing thud at GO.</summary>
        private bool PodHoldsSteps(int id)=>pods.Count>0&&spawnViews.TryGetValue(id,out var v)&&v.Pod&&v.Shown;
        private static Vector3 PodV(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        private static NVector PodN(Vector3 v)=>new NVector(v.x,v.y,v.z);
        /// <summary>Leaving by pod (Astra's rule calls it when pilot `id` boards its pod to leave at presentation time `at`;
        /// NaN cancels): the door closes and the pod lifts away (NativePodDrop.Launch).</summary>
        public void PodLaunch(int id,double at){if(pods.TryGetValue(id,out var p))p.LaunchAt=at;}

        // Bind authoritative anchors/timestamps to the existing presentation. Late joins and area returns do not replay a drop.
        private void SyncAuthorityPods(NativeArena arena)
        {
            if(!PodRun(arena))return;PodSync();
            foreach(var record in arena.PodRecords)
            {
                if(!pods.TryGetValue(record.seat,out var p)){p=new PodView();pods.Add(record.seat,p);}
                if(!p.Authority||p.End!=record.end||p.X!=record.x||p.Z!=record.z){p.Planned=false;p.Earliest=float.NaN;p.Slammed=false;p.CueClock=float.NaN;p.Floor=PodFloor(arena,record.x,record.y,record.z);}
                p.Authority=true;p.Seat=record.seat;p.Order=record.order;p.End=record.end;p.Yaw=record.yaw;p.X=record.x;p.Z=record.z;
                var c=NativePodDrop.Centre(record.x,p.Floor,record.z,record.yaw);p.Spot=new Vector3(c.X,c.Y,c.Z);
                p.Facing=new Vector3(Mathf.Sin(record.yaw*Mathf.Deg2Rad),0,Mathf.Cos(record.yaw*Mathf.Deg2Rad));
                if(p.AuthoritySeat==null)p.AuthoritySeat=new NativeSeat{id=record.seat};
                p.AuthoritySeat.name=record.name;p.AuthoritySeat.bodyColor=record.bodyColor;p.AuthoritySeat.accentColor=record.accentColor;p.AuthoritySeat.pod=record.look;
                PodLaunch(record.seat,record.launchAt>=0?record.launchAt:double.NaN);
            }
        }

        // The floor under a record's capsule (its feet float over it through today's countdown): the arena's colliders, else the
        // map's authored blocks, else the record's own height.
        private static float PodFloor(NativeArena arena,float x,float y,float z)
        {
            if(arena.Root!=null){var physics=arena.Root.scene.GetPhysicsScene();if(physics.IsValid()&&physics.Raycast(new Vector3(x,y+1.2f,z),Vector3.down,out var ground,4,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return ground.point.y;}
            return NativeSpawnIn.AuthoredFloor(arena.Map,x,z,y+.5f,out float floor)?floor:y;
        }

        // ---- the pods each frame ----
        /// <summary>NativeSourcePresentation: every recorded pod at this moment (burning in, crashing, parked or leaving) from the
        /// bank, its LED sign, and its crater and furrow; the sounds its plan has due.</summary>
        private void ExpeditionPods(NativeArena arena)
        {
            podCues.Clear();PodSync();SyncAuthorityPods(arena);if(pods.Count==0||sourceModels==null||!PodRun(arena)||arena.Match==null)return;
            var m=arena.Match;float time=PresentationClock(arena),age=m.Phase==NativePhase.Countdown?(float)(time-m.PhaseEnd):NativeSpawnIn.Age(m.Phase,m.PhaseEnd,m.FightAt,time);podGone.Clear();
            foreach(var pair in pods)
            {
                NativeSeat seat=null;foreach(var s in m.Seats)if(s.id==pair.Key){seat=s;break;}if(seat==null)seat=pair.Value.AuthoritySeat;if(seat==null){podGone.Add(pair.Key);continue;}
                var pod=pair.Value;if(double.IsNaN(pod.End))continue;
                if(pod.Authority&&m.Phase==NativePhase.Countdown){float seen=(float)(time-pod.End);if(!(seen>=pod.Earliest))pod.Earliest=seen;}
                var plan=PodPlanOf(pod);float since=(float)(time-pod.End);
                pod.Frame=!double.IsNaN(pod.LaunchAt)?NativePodDrop.Launch((float)(time-pod.LaunchAt),plan):pod.Authority?NativePodDrop.Evaluate(since,plan):Math.Abs(NativeSpawnIn.End(m.Phase,m.PhaseEnd,m.FightAt)-pod.End)<.25?NativePodDrop.Evaluate(age,plan):NativePodDrop.Evaluate(float.NaN,plan);
                // the marks last the run, the pod gone or not (an unknown time: long since)
                float land=pod.Authority||Math.Abs(NativeSpawnIn.End(m.Phase,m.PhaseEnd,m.FightAt)-pod.End)<.25?since-plan.Impact:NativePodDrop.ParkedSince;
                if(land>=0)PodMarks(pod,land);
                PodSounds(pod,since);
                if(pod.Frame.Gone)continue;
                bool mine=seat.id==arena.LocalId;PodLook(pod,seat,mine);PodDraw(pod,time,mine?PodOpacity(pod,arena.LocalId):1);
            }
            foreach(int id in podGone){if(pods[id].Sign!=null)CapturedAnimation.Release(pods[id].Sign);pods.Remove(id);}
        }
        // The pod's colours (linear) from its look and the seat's body and accent; its sign texture when the name changes.
        private void PodLook(PodView pod,NativeSeat seat,bool mine)
        {
            // (compared field by field and the name only respelled when it changes: nothing allocates while the look holds)
            string look=seat.pod??(mine?LocalPodLook??"":"");
            if(pod.LookKey!=look||pod.LookBody!=seat.bodyColor||pod.LookAccent!=seat.accentColor||!pod.Looked)
            {
                pod.LookKey=look;pod.LookBody=seat.bodyColor;pod.LookAccent=seat.accentColor;pod.Looked=true;NativePodDrop.Colors(look,seat.bodyColor,seat.accentColor,out int paint,out int trim,out int light);
                pod.Paint=PodLinear(paint);pod.Trim=PodLinear(trim);pod.Light=PodLinear(light);
            }
            if(pod.Sign!=null&&pod.SignName==seat.name)return;pod.SignName=seat.name;string text=NativePodDrop.SignText(seat.name);
            if(pod.Text!=text||pod.Sign==null)
            {
                if(pod.Sign!=null)CapturedAnimation.Release(pod.Sign);var px=NativePodDrop.SignPixels(text,PodDot,out int w,out int h);
                var t=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name="Pod sign "+text,wrapModeU=TextureWrapMode.Clamp,wrapModeV=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4,hideFlags=HideFlags.HideAndDontSave};
                var c=new Color32[w*h];for(int i=0;i<c.Length;i++)c[i]=new Color32(px[i*4],px[i*4+1],px[i*4+2],px[i*4+3]);t.SetPixels32(c);t.Apply(true,true);pod.Sign=t;pod.Text=text;pod.Scrolls=NativePodDrop.Scrolls(text);pod.Columns=h/(float)PodDot;
            }
        }
        private static Color PodLinear(int rgb){var c=new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
        // My own pod in third person once its door is open: drawn in see-through glass while it stands between my camera and my
        // pilot (stepping out of it with the camera behind), so it never walls off my view.
        private float PodOpacity(PodView pod,int me)
        {
            var f=pod.Frame;if(FirstPerson||!f.Shown)return 1;var eye=WorldEye;if(eye==null||!visualPositions.TryGetValue(me,out var pilot))return 1;
            var cam=eye.transform.position;Vector2 a=new Vector2(cam.x,cam.z),b=new Vector2(pilot.x,pilot.z),c=new Vector2(pod.Spot.x,pod.Spot.z),ab=b-a;
            float t=ab.sqrMagnitude>1e-4f?Mathf.Clamp01(Vector2.Dot(c-a,ab)/ab.sqrMagnitude):0,d=Vector2.Distance(a+ab*t,c);
            float near=1-Mathf.SmoothStep(0,1,(d-.75f)/.5f);return 1-.72f*near;
        }
        private void PodDraw(PodView pod,float time,float opacity)
        {
            var f=pod.Frame;var at=pod.Spot+PodV(f.Offset);var tilt=new Quaternion(f.Tilt.X,f.Tilt.Y,f.Tilt.Z,f.Tilt.W);
            var rot=tilt*Quaternion.LookRotation(pod.Facing,Vector3.up);var root=Matrix4x4.TRS(at,rot,Vector3.one);
            podNow=pod;podTint??=PodTintOf;podBone??=PodBoneOf;podRoot=root;
            var hh=NativePodModels.HarnessHinge;var hinge=new Vector3(hh.X,hh.Y,hh.Z);podHarness=root*Matrix4x4.Translate(hinge)*Matrix4x4.Rotate(Quaternion.Euler(f.Harness*NativePodModels.HarnessUp*Mathf.Rad2Deg,0,0))*Matrix4x4.Translate(-hinge);
            bool ghost=opacity<.99f;var dh=NativePodModels.Hinge;var doorHinge=new Vector3(dh.X,dh.Y,dh.Z);
            sourceModels.Queue(ghost?NativePodModels.GhostId:NativePodModels.PodId,root,podBone,opacity:ghost?opacity:1,partTint:podTint);
            sourceModels.Queue(ghost?NativePodModels.DoorGhostId:NativePodModels.DoorId,root*Matrix4x4.TRS(doorHinge,Quaternion.Euler(f.Door*Mathf.Rad2Deg,0,0),Vector3.one),opacity:ghost?opacity:1,partTint:podTint);
            podNow=null;
            // the LED name over the door (dark until the landing powers it; long names scroll)
            if(f.Sign<=.01f||pod.Sign==null||opacity<.99f||!EnsurePodSign())return;
            var c=new NVector(NativePodModels.SignCentre.X,NativePodModels.SignCentre.Y,NativePodModels.SignCentre.Z);
            var sign=root*Matrix4x4.TRS(new Vector3(c.X,c.Y,c.Z),Quaternion.identity,new Vector3(NativePodModels.SignWidth,NativePodModels.SignHeight,1));
            float rep=pod.Scrolls?NativePodDrop.SignColumns/pod.Columns:1,scroll=pod.Scrolls?Mathf.Repeat(time*14/pod.Columns,1):0;
            podBlock??=new MaterialPropertyBlock();podBlock.Clear();podBlock.SetTexture(PodTexId,pod.Sign);podBlock.SetVector(PodTintId,new Vector4(pod.Light.r*2.6f*f.Sign,pod.Light.g*2.6f*f.Sign,pod.Light.b*2.6f*f.Sign,1));
            podBlock.SetFloat(PodRepId,rep);podBlock.SetFloat(PodScrollId,scroll);podBlock.SetFloat(PodBlurId,0);podBlock.SetFloat(PodGainId,1);podBlock.SetFloat(PodSweepId,-1);podBlock.SetFloat(PodChromaId,1);podBlock.SetFloat(PodHoloId,0);
            Graphics.DrawMesh(podQuad,sign,podSignMaterial,0,null,0,podBlock,ShadowCastingMode.Off,false);
        }
        // The pod's parts' frames: the harness bar about its hinge, everything else on the pod.
        private Matrix4x4 PodBoneOf(int bone)=>bone==NativePodModels.Harness?podHarness:podRoot;
        // The crater on the pod centre for the run, its shape scaled per pod and its ploughed tail turned back down the approach;
        // the pit's ember cracks glow with the hull while it cools. Pods standing closer than two craters (today's 2 m row, until
        // A0 spreads the spots) shrink theirs to fit and stack them a few millimetres apart by landing order, lower, so no two
        // flat marks ever lie in one plane (no z-fight) and none stands over FlatHeight.
        private void PodMarks(PodView pod,float land)
        {
            var p=pod.Plan;var travel=new Vector3(p.Travel.X,0,p.Travel.Z);if(travel.sqrMagnitude<1e-4f)return;podNow=pod;podMarkTint??=PodMarkTintOf;
            float near=float.MaxValue;foreach(var other in pods.Values)if(!ReferenceEquals(other,pod)&&other.Planned){float dx=other.Spot.x-pod.Spot.x,dz=other.Spot.z-pod.Spot.z;near=Mathf.Min(near,Mathf.Sqrt(dx*dx+dz*dz));}
            float fit=Mathf.Clamp((near-.2f)/(2*NativePodModels.CraterRadius*1.06f),.5f,1);bool crowded=fit<.999f;
            var turn=Quaternion.LookRotation(travel,Vector3.up)*Quaternion.Euler(0,p.CraterTurn*Mathf.Rad2Deg,0);var at=pod.Spot+Vector3.up*(crowded?.005f*Mathf.Clamp(p.Order,0,3):0);
            sourceModels.Queue(NativePodModels.CraterIds[p.Crater],Matrix4x4.TRS(at,turn,new Vector3(p.CraterX*fit,crowded?.74f:1,p.CraterZ*fit)),partTint:podMarkTint);
            podNow=null;
        }
        private Color PodMarkTintOf(int bone)
        {
            var p=podNow;if(p==null||bone!=NativePodModels.Heat)return Color.white;var c=NativePodDrop.HullColor(NativePodDrop.Cool(p.Frame.Land>=0?p.Frame.Land:NativePodDrop.ParkedSince)*.8f);return new Color(c.X,c.Y,c.Z,1);
        }
        // The pod's parts: its paint and trim, the lights in its colour (white-hot while it burns in), the cabin lamp (red on the
        // way down, warm once landed), the retro throats, the ablative tiles' heat, the bolts' flash.
        private Color PodTintOf(int bone)
        {
            var p=podNow;if(p==null)return Color.white;var f=p.Frame;
            switch(bone)
            {
                case NativePodModels.Paint:return p.Paint;
                case NativePodModels.Trim:return p.Trim;
                case NativePodModels.Lights:{float level=f.Dropping&&f.Land<0?1.4f:1.1f+.4f*f.Seam;var c=p.Light*level;var hot=new Color(3f,2.1f,1.3f,1);c=Color.Lerp(c,hot,f.Heat*.75f);
                    if(f.Beacon<.5f&&f.Land>=0)c*=.9f;c.a=1;return c;}
                case NativePodModels.Cabin:{float k=f.Dropping&&f.Land<0?.35f:f.Cabin;var c=f.Dropping&&f.Land<0?new Color(2.2f,.25f,.2f,1):new Color(2.4f,1.9f,1.3f,1);c*=k;c.a=1;return c;}
                case NativePodModels.Retro:{var c=p.Plan.Fails?new Color(3.2f,1.9f,1.1f,1):new Color(1.6f,2.6f,3.4f,1);c*=.05f+3*f.Retro;c.a=1;return c;}
                case NativePodModels.Heat:{var c=NativePodDrop.HullColor(f.Hull);return new Color(c.X,c.Y,c.Z,1);}
                case NativePodModels.Bolts:{float k=f.Bolts;var c=k>0?new Color(6,4.2f,2.2f,1)*k:f.Age>p.Plan.DoorAt?new Color(.07f,.065f,.06f,1):new Color(.2f,.2f,.22f,1);c.a=1;return c;}
                default:return Color.white;
            }
        }
        private bool EnsurePodSign()
        {
            if(podSignMaterial!=null&&podQuad!=null)return true;if(podFailed)return false;
            var shader=Resources.Load<Shader>("NativeDeployScreen");if(shader==null||!shader.isSupported){podFailed=true;return false;}
            podSignMaterial=new Material(shader){name="Pod sign",hideFlags=HideFlags.HideAndDontSave,renderQueue=3010};
            podSignMaterial.SetFloat("_SrcBlend",(float)BlendMode.One);podSignMaterial.SetFloat("_DstBlend",(float)BlendMode.One);podSignMaterial.SetFloat("_ZWrite",0);podSignMaterial.SetFloat("_Cull",0);podSignMaterial.SetFloat("_Additive",1);
            // A unit quad facing +z: u down the sign from its top, v along it from the viewer's left (+x seen from the front).
            podQuad=new Mesh{name="Pod sign quad",hideFlags=HideFlags.HideAndDontSave};
            podQuad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            podQuad.uv=new[]{new Vector2(1,1),new Vector2(1,0),new Vector2(0,0),new Vector2(0,1)};
            podQuad.colors=new[]{Color.white,Color.white,Color.white,Color.white};podQuad.triangles=new[]{0,2,1,0,3,2};podQuad.RecalculateBounds();podQuad.UploadMeshData(false);
            return true;
        }
        // The sounds each pod's plan has due since the last frame (a rewind or a first sight starts the count afresh, so a late
        // joiner hears nothing old).
        private void PodSounds(PodView pod,float since)
        {
            if(float.IsNaN(pod.CueClock)||since<pod.CueClock-.5f||since>pod.CueClock+.5f){pod.CueClock=since;return;}
            if(!(since>pod.CueClock))return;var p=pod.Plan;
            for(int i=0;i<pod.CueCount;i++)
            {
                var c=pod.Cues[i];if(c.At<=pod.CueClock||c.At>since)continue;
                var at=c.Sky?pod.Spot+PodV(NativePodDrop.FlightAt(p,Mathf.Clamp(c.At,p.Appear,p.Impact),out _)):pod.Spot+Vector3.up*.8f;
                if(podCues.Count<64)podCues.Add(new PodSound{Sound=c.Sound,At=at,Gain=c.Gain,Range=c.Range,Rate=c.Rate,Travels=c.Sound==NativePodSounds.Boom});
            }
            pod.CueClock=since;
        }

        // ---- the effects ----
        /// <summary>RenderGlow: each pod's crash effects (NativePodDrop.Effects), its meteor streak (unfogged), the impact's chunks
        /// through the destruction kit.</summary>
        private void PodFx(NativeArena arena,Camera camera)
        {
            podGlow.Clear();if(pods.Count==0||!PodRun(arena))return;float q=Mathf.Clamp01(WeaponEffectsQuality);
            if(blasts!=null)blasts.PodFlashes??=PodFlashesOf;
            var eye=camera!=null?PodN(camera.transform.position):NVector.Zero;var kit=structureFx?.Fx;
            foreach(var pod in pods.Values)
            {
                if(!pod.Planned||pod.Frame.Gone&&pod.Frame.Land<0)continue;var f=pod.Frame;var p=pod.Plan;var centre=PodN(pod.Spot);
                if(vfx!=null){podFx??=new PodEntranceFx(vfx);NativePodDrop.Effects(podFx,f,p,centre,q,pod.Floor);}
                if(camera!=null)NativePodDrop.Meteor(podGlow,f,p,centre,eye,q<.5f?.6f:1);
                // the impact's chunks of the floor, thrown along the plough (once, on the kit's clock; no kit marks: the crater is ours)
                if(!pod.Slammed&&f.Dropping&&f.Land>=0&&f.Land<.3f&&kit!=null)
                {
                    pod.Slammed=true;var touch=pod.Spot+PodV(p.P1);
                    NativeStructureFxPresentation.Slam(arena,kit,touch,pod.Floor,p.Fails?1:.85f,new Vector3(p.Travel.X,0,p.Travel.Z),(uint)p.Seed*2654435761u^0x9047u,(float)(pod.End+p.Impact),false);
                }
            }
            if(podGlow.Count==0||camera==null||!EnsurePodGlow())return;
            podGlowMesh.Clear();podGlowMesh.SetVertices(podGlow.Points);podGlowMesh.SetColors(podGlow.Colors);podGlowMesh.SetUVs(0,podGlow.Uv);podGlowMesh.SetTriangles(podGlow.Indices,0);podGlowMesh.RecalculateBounds();
            Graphics.DrawMesh(podGlowMesh,Matrix4x4.identity,podGlowMaterial,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private bool EnsurePodGlow()
        {
            if(podGlowMaterial!=null&&podGlowMesh!=null)return true;if(podGlowFailed)return false;
            var shader=Resources.Load<Shader>("NativeGlow");if(shader==null||!shader.isSupported){podGlowFailed=true;return false;}
            podGlowMaterial=new Material(shader){name="Pod meteor",hideFlags=HideFlags.HideAndDontSave};podGlowMesh=new Mesh{name="Pod meteors",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};podGlowMesh.MarkDynamic();return true;
        }
        // The blast lights this frame with the crashes' flashes in them (NativeBlastPresentation.PodFlashes): each impact's warm
        // flash, and the bolts' small one, take the dimmest light they outshine; softer with Reduced effects.
        private readonly NativeBlastFlash[] podLit={new NativeBlastFlash(),new NativeBlastFlash(),new NativeBlastFlash()};
        private Func<NativeBlastFlash[],NativeBlastFlash[]> podFlashes;
        private Func<NativeBlastFlash[],NativeBlastFlash[]> PodFlashesOf=>podFlashes??=PodFlashMerge;
        private NativeBlastFlash[] PodFlashMerge(NativeBlastFlash[] flashes)
        {
            if(pods.Count==0||flashes==null||flashes.Length!=podLit.Length)return flashes;bool any=false;
            for(int i=0;i<podLit.Length;i++){var s=flashes[i];var d=podLit[i];d.X=s.X;d.Y=s.Y;d.Z=s.Z;d.Distance=s.Distance;d.At=s.At;d.Peak=s.Peak;d.Life=s.Life;d.Intensity=s.Intensity;d.Color=s.Color;}
            float soft=WeaponEffectsQuality<.5f?.5f:1;
            foreach(var pod in pods.Values)
            {
                if(!pod.Planned)continue;var f=pod.Frame;float land=f.Land,intensity=0;int color=0xffc890;float range=16;
                if(f.Dropping&&land>=0&&land<.4f){float u=land/.4f;intensity=(pod.Plan.Fails?6:4.5f)*(1-u)*(1-u)*soft;}
                else if(f.Bolts>0){intensity=1.6f*f.Bolts*soft;color=0xfff1d6;range=7;}
                if(!(intensity>.01f))continue;
                int dim=0;for(int i=1;i<podLit.Length;i++)if(podLit[i].Intensity<podLit[dim].Intensity)dim=i;if(podLit[dim].Intensity>=intensity)continue;
                var at=pod.Spot+PodV(f.Land>=0?pod.Plan.P1*.5f:f.Offset)+Vector3.up*1.5f;var o=podLit[dim];o.X=at.x;o.Y=at.y;o.Z=at.z;o.Distance=range;o.Intensity=intensity;o.Color=color;any=true;
            }
            return any?podLit:flashes;
        }
        /// <summary>The entrance emitters into the shared effects renderer, in the world frame as it is.</summary>
        private sealed class PodEntranceFx:INativeEntranceFx
        {
            private readonly NativeVfxDraw vfx;public PodEntranceFx(NativeVfxDraw vfx){this.vfx=vfx;}
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),new Color(color.X,color.Y,color.Z,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),new Color(color.X,color.Y,color.Z,1),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),new Color(albedo.X,albedo.Y,albedo.Z,0),opacity,size,u,seed,floor);
        }

        // ---- my own camera: the directed drop (NativeCinematicCamera's Countdown branch) ----
        private Vector3 podHandEye;private Quaternion podHandTurn;private float podHandFov;
        /// <summary>
        /// My camera over my own pod's drop this frame (a pod run's countdown, my pilot in its pod, no capture eye): the directed
        /// shot (NativePodDrop.Shot) with its shake (my ride and every crash by distance; none with Camera shake off, half with
        /// Reduced effects, which takes the still wide shot instead). True while it directs the whole frame; in the hand-off before
        /// GO it is false with `hand` the directed share, which PodHandOff blends over the game's own camera.
        /// </summary>
        public bool PodDropDirect(NativeArena arena,Camera view,out float hand)
        {
            hand=0;if(view==null||!PodRun(arena)||arena.Match==null||arena.Local==null||Viewer||ConversionGame.CaptureEye.HasValue)return false;
            var m=arena.Match;if(m.Phase!=NativePhase.Countdown)return false;SyncAuthorityPods(arena);
            if(!pods.TryGetValue(arena.Local.Seat.id,out var pod)||double.IsNaN(pod.End)||!double.IsNaN(pod.LaunchAt))return false;
            float age=(float)(PresentationClock(arena)-pod.End);if(pod.Authority&&!(age>=pod.Earliest))pod.Earliest=age;if(!pod.Planned&&!pod.Authority)return false;
            var p=PodPlanOf(pod);if(!(age>=p.Appear)||age>=0)return false;
            bool reduced=WeaponEffectsQuality<.5f,shake=CameraShake&&!reduced;
            int mode=reduced?NativePodDrop.ShotStill:FirstPersonPick?NativePodDrop.ShotFirst:NativePodDrop.ShotChase;
            var body=arena.Local.Body!=null?arena.Local.Body.transform.position:pod.Spot+Vector3.up*NativeSpawnIn.RestCentre;
            var s=NativePodDrop.Shot(p,PodN(pod.Spot),age,mode,PodN(body+Vector3.up*NativeViewRules.EyeHeight));
            Vector3 eye=PodV(s.Eye),look=PodV(s.Look),up=PodV(s.Up);
            if(shake)
            {
                // my ride's rumble and every crash's slam by its distance (NativeStructureShake's fall-off), on smooth noise
                float amp=0;foreach(var other in pods.Values)if(other.Planned)amp+=NativePodDrop.Shake(other.Plan,(float)(PresentationClock(arena)-other.End),Vector3.Distance(eye,other.Spot+PodV(other.Plan.P1)),other==pod);
                amp=Mathf.Min(NativeStructureShake.MaxOffset,amp);
                if(amp>0){float t=age;int seed=p.Seed&0xffff;eye+=new Vector3(NativePodDrop.ShakeNoise(seed,0,t*21),.75f*NativePodDrop.ShakeNoise(seed,1,t*27),.5f*NativePodDrop.ShakeNoise(seed,2,t*17))*amp;}
            }
            var forward=look-eye;if(forward.sqrMagnitude<1e-6f)forward=view.transform.forward;var turn=Quaternion.LookRotation(forward,up.sqrMagnitude>1e-4f?up:Vector3.up);
            // out of walls: a solid between the subject and the eye pulls the eye in (the fence, a machine), as the cutscene's
            if(!s.Cabin&&arena.Root!=null){var ray=eye-look;float dist=ray.magnitude;if(dist>.3f&&arena.Root.scene.GetPhysicsScene().Raycast(look,ray/dist,out var hit,dist,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))eye=look+ray/dist*Mathf.Max(.3f,hit.distance-.25f);}
            hand=s.Directed;
            if(hand<.999f){podHandEye=eye;podHandTurn=turn;podHandFov=s.Fov;return false;}
            view.transform.SetPositionAndRotation(eye,turn);view.fieldOfView=s.Fov;
            // in the cabin and once the arc has brought the eye home in first person my own body stays hidden; otherwise it is drawn
            FirstPerson=s.Cabin||mode==NativePodDrop.ShotFirst&&age>=p.ArcTo-.05f;AimHeld=false;
            return true;
        }
        /// <summary>The hand-off at GO: the last of the directed shot blended over the game's own camera this frame (`share` the
        /// directed part, falling to 0 at GO), so control comes back with no snap.</summary>
        public void PodHandOff(Camera view,float share)
        {
            if(view==null||!(share>0))return;var t=view.transform;
            t.SetPositionAndRotation(Vector3.Lerp(t.position,podHandEye,share),Quaternion.Slerp(t.rotation,podHandTurn,share));view.fieldOfView=Mathf.Lerp(view.fieldOfView,podHandFov,share);
        }
    }
}
