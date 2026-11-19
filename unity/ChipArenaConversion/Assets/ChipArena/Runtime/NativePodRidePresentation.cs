using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The pod run's drop-in on my own screen (docs/EXPEDITION-OVERHAUL.md R7, R8 and W3), presentation only:
    ///  - PodRideView: the ride, one braced second in first person inside my falling pod during the opening countdown (Core
    ///    NativePodRide), my camera at my seat in my pod's own frame (the frame PodDraw draws it in, Runtime/NativePodPresentation.cs),
    ///    judder with camera shake on, steady with it off, none with reduced effects; the view model hidden while it holds. A
    ///    directed countdown camera (NativeCinematicCamera's) yields to it while PodRideActive says so.
    ///  - PodRideScreen: the cabin's red at the edges of my screen while the ride holds.
    ///  - PodLobbyHidden: a pod run's pilots stay hidden while the hub waits in Lobby for the party (an early guest's frames show
    ///    no one standing on bare slots); the drop shows them from the countdown.
    /// Nothing here changes a rule, the clock, the network or the pilot's position.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>Whether my ride holds this frame (a directed countdown camera yields to it), and its share.</summary>
        public bool PodRideActive{get;private set;}public float PodRideShare{get;private set;}
        /// <summary>
        /// ConversionGame.LateFrame, after SpawnCamera: in a pod run's countdown, during the ride's window, my camera rides in my own
        /// pod's cabin (its eye at my seat, looking at the door's porthole). True when it placed the camera.
        /// </summary>
        public bool PodRideView(Transform camera,NativeArena arena,float time,bool shake,bool reduced)
        {
            PodRideActive=false;PodRideShare=0;
            if(!NativePodRide.Enabled||camera==null||arena==null||reduced||!PodRun(arena)||arena.Match==null||arena.Match.Phase!=NativePhase.Countdown)return false;
            var me=arena.Local;if(me?.Seat==null||!pods.TryGetValue(me.Seat.id,out var pod)||double.IsNaN(pod.End)||!double.IsNaN(pod.LaunchAt))return false;
            float age=(float)(PresentationClock(arena)-pod.End);if(!NativePodRide.Active(age,NativePodRide.Opening,out float u))return false;
            var f=NativePodDrop.Evaluate(age,pod.Order,pod.Seed);if(!f.Dropping)return false;
            var root=PodRideRoot(pod,f);NativePodRide.Judder(age,u,shake,out var jo,out float pitch,out float roll);
            var e=NativePodRide.Eye+jo;var l=NativePodRide.Look;
            camera.position=root.MultiplyPoint3x4(new Vector3(e.X,e.Y,e.Z));
            camera.rotation=root.rotation*Quaternion.LookRotation(new Vector3(l.X,l.Y,l.Z),Vector3.up)*Quaternion.Euler(pitch,0,roll);
            PodRideActive=true;PodRideShare=u;return true;
        }
        // My pod's frame this moment, as PodDraw draws it (its spot plus the drop's offset, leaning into its approach, facing its door).
        private static Matrix4x4 PodRideRoot(PodView pod,in PodFrame f)
        {
            var up=Vector3.up;var at=pod.Spot+new Vector3(f.Offset.X,f.Offset.Y,f.Offset.Z);var approach=new Vector3(f.Offset.X,0,f.Offset.Z);
            var lean=f.Lean>0&&approach.sqrMagnitude>1e-4f?Quaternion.AngleAxis(-f.Lean*Mathf.Rad2Deg,Vector3.Cross(up,approach.normalized)):Quaternion.identity;
            return Matrix4x4.TRS(at,lean*Quaternion.LookRotation(pod.Facing.sqrMagnitude>1e-4f?pod.Facing:Vector3.forward,up),Vector3.one);
        }
        // ---- a pod run's Lobby: nobody stands on the slots before the drop ----
        /// <summary>SpawnEvaluate in a pod run's hub while it waits in Lobby: the pilot hidden (as it rides in its pod: no beam, no
        /// hologram, no HUD), so the slots stand empty until the countdown's drop.</summary>
        private SpawnView PodLobbyHidden(NativePilot pilot)
        {
            int id=pilot.Seat.id;if(!spawnViews.TryGetValue(id,out var v)){v=new SpawnView();spawnViews.Add(id,v);}
            v.Pod=true;v.End=double.NaN;v.Built=false;v.Frame=new SpawnInFrame{Active=true,Front=-1e4f,Held=true,Land=-1};return v;
        }
    }

    public sealed partial class ConversionGame
    {
        private static readonly float[] RideRedAt={0f,.55f,.85f,1f},RideRedAlpha={0f,0f,.6f,1f};
        /// <summary>LateFrame, after SpawnCamera: my pod ride's camera (ArenaPresentation.PodRideView), the view model hidden while it
        /// holds. A directed camera (NativeCinematicCamera: a cutscene, or a directed drop of my pod) owns the view: the ride steps aside.</summary>
        private void PodRideFrame(float renderTime)
        {
            bool off=menu||Spectator!=null||arena==null||cinematic.Directing;
            if(presentation.PodRideView(cameraView.transform,off?null:arena,renderTime,prefs.cameraShake,prefs.reducedEffects))viewModelCamera.enabled=false;
        }
        /// <summary>My screen during the ride, under the HUD: the cabin's red lamp at its edges.</summary>
        private void PodRideScreen()
        {
            if(menu||presentation==null||!presentation.PodRideActive||Spectator!=null)return;float k=NativePodRide.Red(presentation.PodRideShare);if(k<=0)return;
            U.RadialStops(new Rect(-U.W*.1f,-U.H*.12f,U.W*1.2f,U.H*1.24f),new Color(.55f,.05f,.04f,.85f*k),RideRedAt,RideRedAlpha);
        }
    }
}
