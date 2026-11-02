using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition drop pods (Core NativePodDrop, NativePodModels) in a generated run's hub, in place of the spawn-in beam:
    ///  - NativeSpawnInPresentation.SpawnEvaluate hands a pod run's pilots here (PodSpawn): the pilot rides hidden in its pod
    ///    (no hologram, no beam, no HUD) until the door opens, my own first-person view riding the fall; my camera kicks at the
    ///    landing.
    ///  - ExpeditionPods (NativeSourcePresentation, beside the PvE machines) queues each pod and its door from the source-model
    ///    bank in its look (paint, trim, lights; the cabin lamp, the retro throats), and lays the LED name sign over the marquee
    ///    (an additive dot texture per name, scrolling when the name is long).
    ///  - PodFx (RenderGlow) emits the fall, landing and door effects into the shared effects renderer.
    /// Each pod stays parked where it landed for the run (the pilot's way out: leaving by pod is Astra's rule; NativePodDrop.Launch
    /// is its look, PodLaunch the hook). A pilot's pod is recorded when its drop is seen; a late joiner sees no pods yet.
    /// Presentation only: no rule, timing, collision or network field reads it. Nothing allocates per frame once a pod is made.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private sealed class PodView
        {
            public int Order,Seed,FxSeed;public double End=double.NaN;public Vector3 Spot,Facing;public float Floor;
            public string Text;public Texture2D Sign;public bool Scrolls;public float Columns;
            public PodFrame Frame;public double LaunchAt=double.NaN;public Color Paint,Trim,Light;public string LookKey;
        }
        private readonly Dictionary<int,PodView> pods=new Dictionary<int,PodView>();private object podRun;
        private Material podSignMaterial;private Mesh podQuad;private bool podFailed;private MaterialPropertyBlock podBlock;
        private PodEntranceFx podFx;private PodView podNow;private Func<int,Color> podTint;private readonly List<int> podGone=new List<int>();
        private static readonly int PodTexId=Shader.PropertyToID("_MainTex"),PodTintId=Shader.PropertyToID("_Tint"),PodRepId=Shader.PropertyToID("_Rep"),PodScrollId=Shader.PropertyToID("_Scroll"),
            PodBlurId=Shader.PropertyToID("_Blur"),PodGainId=Shader.PropertyToID("_Gain"),PodSweepId=Shader.PropertyToID("_Sweep"),PodChromaId=Shader.PropertyToID("_Chroma"),PodHoloId=Shader.PropertyToID("_Holo");
        /// <summary>My own pod's look (prefs.pod, NativePodDrop's map): the seats do not carry pod looks yet (Astra), so every
        /// other pilot's pod draws in its body colour and accent.</summary>
        public string LocalPodLook="";
        /// <summary>The LED dot size in texture pixels.</summary>
        private const int PodDot=8;

        /// <summary>Pods fall in this arena: a generated run's hub (its world's map).</summary>
        private bool PodRun(NativeArena arena)=>arena?.Map!=null&&GeneratedRun?.World?.Map!=null&&arena.Map.id==GeneratedRun.World.Map.id;

        // ---- the spawn-in's pod path ----
        /// <summary>SpawnEvaluate in a pod run: the pilot hidden in its pod until the door opens, its presented feet riding the
        /// pod's floor while it falls; null once the drop is over.</summary>
        private SpawnView PodSpawn(NativeArena arena,NativePilot pilot,Vector3 feet,float age,double end,out float raise)
        {
            raise=0;if(float.IsNaN(age)||age>=NativePodDrop.End)return null;int id=pilot.Seat.id;
            if(!spawnViews.TryGetValue(id,out var v)){v=new SpawnView();spawnViews.Add(id,v);}
            if(v.Arena!=arena||double.IsNaN(v.End)||Math.Abs(v.End-end)>.25)SpawnPlaceView(v,arena,pilot,feet,end);
            var pod=PodPlace(arena,pilot,v,end);var f=pod.Frame=NativePodDrop.Evaluate(age,pod.Order,pod.Seed);v.Pod=true;
            v.Frame=new SpawnInFrame{Active=true,Age=age,Built=f.Shown?1:0,Front=f.Shown?1e4f:-1e4f,Lift=f.Shown?0:f.Lift,Held=!f.Shown,Land=-1};
            float target=v.Place.Floor+NativeSpawnIn.RestFeet+v.Frame.Lift;raise=v.Frame.Lift>0&&feet.y<target?target-feet.y:0;
            return v;
        }
        // The pilot's pod for this drop: where it lands (the spawn, on the floor the spawn-in found), which way its door faces
        // (the pilot's), its place in the landing order and the run's shared approach.
        private PodView PodPlace(NativeArena arena,NativePilot pilot,SpawnView v,double end)
        {
            PodSync();int id=pilot.Seat.id;if(!pods.TryGetValue(id,out var pod)){pod=new PodView();pods.Add(id,pod);}
            // the same drop (the fight start reads the countdown's end within a tick)
            if(!double.IsNaN(pod.End)&&Math.Abs(pod.End-end)<.25)return pod;
            int order=0;foreach(var s in arena.Match.Seats)if(s.connected&&!s.AuthorityBoss&&s.id<id)order++;
            var face=Quaternion.Euler(0,pilot.Yaw*Mathf.Rad2Deg,0)*Vector3.forward;face.y=0;face=face.sqrMagnitude>1e-4f?face.normalized:Vector3.forward;
            pod.Order=order;pod.Seed=NativeSpawnIn.Seed(0,end);pod.FxSeed=NativeSpawnIn.Seed(id,end);pod.End=end;pod.LaunchAt=double.NaN;
            pod.Spot=new Vector3(v.Place.Centre.X,v.Place.Floor,v.Place.Centre.Z);pod.Floor=v.Place.Floor;pod.Facing=face;
            return pod;
        }
        // A new run (or none) clears the pods.
        private void PodSync(){if(!ReferenceEquals(podRun,GeneratedRun)){podRun=GeneratedRun;foreach(var p in pods.Values)if(p.Sign!=null)CapturedAnimation.Release(p.Sign);pods.Clear();}}
        private void DisposePods(){podRun=new object();PodSync();if(podSignMaterial!=null)CapturedAnimation.Release(podSignMaterial);if(podQuad!=null)CapturedAnimation.Release(podQuad);podSignMaterial=null;podQuad=null;}
        /// <summary>
        /// SpawnCamera in a pod run: my view rides my pod down (its offset along the approach: in third person behind it, in
        /// first person in its cabin) until the landing, then kicks with it (camera shake on; Reduced .4).
        /// </summary>
        private void PodCamera(Transform camera,int id,bool shake,bool reduced)
        {
            if(!pods.TryGetValue(id,out var pod))return;var f=pod.Frame;
            if(f.Dropping&&f.Land<0&&f.Offset.Y<NativePodDrop.Altitude*2)
            {
                camera.position+=new Vector3(f.Offset.X,f.Offset.Y,f.Offset.Z)+(FirstPerson?Vector3.up*NativePodModels.Floor:Vector3.zero);
                // third person: pulled back and up off the pod on the way down, easing in over its last .4 s
                if(!FirstPerson){float pull=Mathf.SmoothStep(0,1,Mathf.Clamp01(-f.Land/.4f));camera.position+=(-camera.forward*3.2f+Vector3.up*1.1f)*pull;}
            }
            if(!shake)return;float kick=NativePodDrop.Kick(f,out float pitch)*(reduced?.4f:1);camera.position-=Vector3.up*kick;if(pitch>0)camera.rotation*=Quaternion.Euler(pitch*(reduced?.4f:1),0,0);
        }
        /// <summary>Leaving by pod (Astra's rule calls it when pilot `id` boards its pod to leave at presentation time `at`;
        /// NaN cancels): the door closes and the pod lifts away (NativePodDrop.Launch).</summary>
        public void PodLaunch(int id,double at){if(pods.TryGetValue(id,out var p))p.LaunchAt=at;}

        // ---- the pods each frame ----
        /// <summary>NativeSourcePresentation: every recorded pod at this moment (falling, landing, parked or leaving) from the
        /// bank, and its LED sign.</summary>
        private void ExpeditionPods(NativeArena arena)
        {
            PodSync();if(pods.Count==0||sourceModels==null||!PodRun(arena)||arena.Match==null)return;
            var m=arena.Match;float time=PresentationClock(arena),age=NativeSpawnIn.Age(m.Phase,m.PhaseEnd,m.FightAt,time);podGone.Clear();
            foreach(var pair in pods)
            {
                NativeSeat seat=null;foreach(var s in m.Seats)if(s.id==pair.Key){seat=s;break;}if(seat==null){podGone.Add(pair.Key);continue;}
                var pod=pair.Value;if(double.IsNaN(pod.End))continue;
                pod.Frame=!double.IsNaN(pod.LaunchAt)?NativePodDrop.Launch((float)(time-pod.LaunchAt)):Math.Abs(NativeSpawnIn.End(m.Phase,m.PhaseEnd,m.FightAt)-pod.End)<.25?NativePodDrop.Evaluate(age,pod.Order,pod.Seed):NativePodDrop.Evaluate(float.NaN,pod.Order,pod.Seed);
                if(pod.Frame.Offset.Y>NativePodDrop.Altitude*2)continue;
                bool mine=seat.id==arena.LocalId;PodLook(pod,seat,mine);PodDraw(pod,time,mine?PodOpacity(pod,arena.LocalId):1);
            }
            foreach(int id in podGone){if(pods[id].Sign!=null)CapturedAnimation.Release(pods[id].Sign);pods.Remove(id);}
        }
        // The pod's colours (linear) from its look and the seat's body and accent; its sign texture when the name changes.
        private void PodLook(PodView pod,NativeSeat seat,bool mine)
        {
            string look=mine?LocalPodLook??"":"";string key=look+"|"+seat.bodyColor+"|"+seat.accentColor;
            if(pod.LookKey!=key)
            {
                pod.LookKey=key;NativePodDrop.Colors(look,seat.bodyColor,seat.accentColor,out int paint,out int trim,out int light);
                pod.Paint=PodLinear(paint);pod.Trim=PodLinear(trim);pod.Light=PodLinear(light);
            }
            string text=NativePodDrop.SignText(seat.name);
            if(pod.Text!=text||pod.Sign==null)
            {
                if(pod.Sign!=null)CapturedAnimation.Release(pod.Sign);var px=NativePodDrop.SignPixels(text,PodDot,out int w,out int h);
                var t=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name="Pod sign "+text,wrapModeU=TextureWrapMode.Clamp,wrapModeV=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4,hideFlags=HideFlags.HideAndDontSave};
                var c=new Color32[w*h];for(int i=0;i<c.Length;i++)c[i]=new Color32(px[i*4],px[i*4+1],px[i*4+2],px[i*4+3]);t.SetPixels32(c);t.Apply(true,true);pod.Sign=t;pod.Text=text;pod.Scrolls=NativePodDrop.Scrolls(text);pod.Columns=h/(float)PodDot;
            }
        }
        private static Color PodLinear(int rgb){var c=new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
        // My own pod in third person, landed: dithered down while it stands between my camera and my pilot (inside it, or
        // walking out of it with the camera behind), so it never walls off my view.
        private float PodOpacity(PodView pod,int me)
        {
            var f=pod.Frame;if(FirstPerson||f.Dropping&&f.Land<0)return 1;var eye=WorldEye;if(eye==null||!visualPositions.TryGetValue(me,out var pilot))return 1;
            var cam=eye.transform.position;Vector2 a=new Vector2(cam.x,cam.z),b=new Vector2(pilot.x,pilot.z),c=new Vector2(pod.Spot.x,pod.Spot.z),ab=b-a;
            float t=ab.sqrMagnitude>1e-4f?Mathf.Clamp01(Vector2.Dot(c-a,ab)/ab.sqrMagnitude):0,d=Vector2.Distance(a+ab*t,c);
            float near=1-Mathf.SmoothStep(0,1,(d-.75f)/.5f);return 1-.75f*near;
        }
        private void PodDraw(PodView pod,float time,float opacity)
        {
            var f=pod.Frame;var up=Vector3.up;var at=pod.Spot+new Vector3(f.Offset.X,f.Offset.Y,f.Offset.Z);
            var approach=new Vector3(f.Offset.X,0,f.Offset.Z);var lean=f.Lean>0&&approach.sqrMagnitude>1e-4f?Quaternion.AngleAxis(-f.Lean*Mathf.Rad2Deg,Vector3.Cross(up,approach.normalized)):Quaternion.identity;
            var rot=lean*Quaternion.LookRotation(pod.Facing,up);var root=Matrix4x4.TRS(at,rot,Vector3.one);
            podNow=pod;podTint??=PodTintOf;
            sourceModels.Queue(NativePodModels.PodId,root,opacity:opacity,partTint:podTint);
            var hinge=new Vector3(NativePodModels.Hinge.X,NativePodModels.Hinge.Y,NativePodModels.Hinge.Z);
            sourceModels.Queue(NativePodModels.DoorId,root*Matrix4x4.TRS(hinge,Quaternion.Euler(f.Door*Mathf.Rad2Deg,0,0),Vector3.one),opacity:opacity,partTint:podTint);
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
        // The pod's parts: its paint and trim, the lights in its colour (white-hot while it burns in), the cabin lamp (red on the
        // way down, warm once landed), the retro throats.
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
                case NativePodModels.Retro:{var c=new Color(1.6f,2.6f,3.4f,1)*(.05f+3*f.Retro);c.a=1;return c;}
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

        // ---- the effects ----
        /// <summary>RenderGlow: each falling, landing or opening pod's effects (NativePodDrop.Effects).</summary>
        private void PodFx(NativeArena arena,Camera camera)
        {
            if(vfx==null||pods.Count==0||!PodRun(arena))return;podFx??=new PodEntranceFx(vfx);float q=Mathf.Clamp01(WeaponEffectsQuality);
            foreach(var pod in pods.Values)
            {
                if(!pod.Frame.Dropping)continue;
                NativePodDrop.Effects(podFx,pod.Frame,new NVector(pod.Spot.x,pod.Spot.y,pod.Spot.z),new NVector(pod.Facing.x,0,pod.Facing.z),pod.FxSeed,q,pod.Floor);
            }
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
    }
}
