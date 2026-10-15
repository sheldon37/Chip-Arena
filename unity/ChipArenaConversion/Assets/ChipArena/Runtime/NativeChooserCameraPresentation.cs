using UnityEngine;
using N=System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition chooser camera (Claude, 2026-10-15; owner: "Lets swing the camera to us when choosing,"; Core/NativeChooserCamera.cs,
    // docs/EXPEDITION-PRESENTATION.md "Chip picks"). While my pilot is held choosing a chip (Astra's hold, PR #237), the camera eases round
    // from the gameplay camera (any Settings · Camera mode, first person included) to a front three-quarter shot of my pilot from the side
    // its wrist hologram floats on (NativeChooserCamera.SideOf), so the raised wrist, the hologram and the plant read over the tray, and eases back as the hold releases. Presentation only:
    //  - It is placed in LateFrame after the aim (AimTarget, AbilityTarget and the shoulder rigs' convergence use the gameplay camera, so
    //    the look sent to the authority is exactly what it would be) and before the shakes (one shake, on the camera shown). UpdateCamera
    //    rebuilds the gameplay camera from the look every frame and first restores its field of view, so nothing of the swing lasts.
    //  - No input is gated or changed here: mouse look still turns the held pilot (the shot orbits with its facing), the tray keeps its
    //    own keys (ExpeditionPickRoute), and the hold itself is the authority's.
    //  - First person: the view model is off while swung and my body is drawn once the camera is clear of the head; V's choice returns
    //    with the swing. Any other camera (a cutscene, a spectated pilot, the killcam or a new area) drops the swing at once.
    //  - The HUD: no crosshair and nothing over my own pilot (its health, SELECTING CHIP) while the shot is shown.
    public sealed partial class ConversionGame
    {
        private readonly NativeChooserCamera chooserCamera=new NativeChooserCamera();private NativeCameraRig.Cast chooserCast;
        // The gameplay field of view under a swung frame (UpdateCamera restores it first, so its ease never starts from the chooser
        // shot's), the arena the swing belongs to, and whether this frame shows the chooser shot to the HUD.
        private float chooserFov=float.NaN;private int chooserGeneration=-1;private bool chooserFramed;
        /// <summary>The HUD sees the chooser shot: no crosshair, nothing over my own pilot.</summary>
        private bool ChooserFramed=>chooserFramed;

        /// <summary>UpdateCamera, first: the gameplay field of view comes back before anything eases or reads it.</summary>
        private void ChooserCameraRestore(){if(float.IsNaN(chooserFov))return;if(cameraView!=null)cameraView.fieldOfView=chooserFov;chooserFov=float.NaN;}

        /// <summary>My pilot is held choosing: my tray is wanted open (C opens it; it stays across queued picks), a pick's follow-through or
        /// my wrist plant runs, or the view's public flag still shows me choosing (the authority's hold runs to the final plant's end, a
        /// round trip past my own clock). C or Escape closing the tray, and the release, end it; nothing while the tray could not show.</summary>
        private bool ExpeditionChooserHeld
        {
            get
            {
                var v=LiveExpedition;if(v==null||arena?.Local==null||!ExpeditionPickCanShow)return false;
                return expeditionPick.Open||ExpeditionFollowing||ExpeditionMyPlant(Time.unscaledTime-expeditionPlantAt)||ExpeditionPickFlow.ChoosingNow(v);
            }
        }

        /// <summary>LateFrame, after the aim and before the shakes: replaces the gameplay camera with the chooser shot while it is swung.</summary>
        private void ChooserCamera()
        {
            chooserFramed=false;var p=arena?.ViewPilot;if(cameraView==null||p?.Body==null)return;
            bool cut=Spectator!=null||cinematic.Directing||arena.Generation!=chooserGeneration;chooserGeneration=arena.Generation;
            var t=cameraView.transform;Vector3 cam=t.position,body=p.Body.transform.position;
            // The point the gameplay camera is fitted from: a shoulder rig's shoulder point (the boom runs back from it), else the eye.
            Vector3 pivot=shoulderView&&!presentation.FirstPerson?cam+t.forward*cameraRig.Boom:body+Vector3.up*NativeViewRules.EyeHeight;
            bool head=presentation.CinematicHead(p.Seat.id,out var headAt),panel=presentation.ChipSelectPanel(p.Seat.id,out var panelAt);
            var input=new ChooserCameraInput{Want=p==arena.Local&&ExpeditionChooserHeld,Cut=cut,Pilot=p.Seat.id,Now=Time.unscaledTime,Dt=Time.unscaledDeltaTime,
                Body=V(body),Scale=p.Seat.spec=="siren"?p.Pose.RigScale:p.Seat.devScale,HasHead=head,Head=V(headAt),HasPanel=panel,Panel=V(panelAt),Facing=yaw,Side=NativeChooserCamera.SideOf(p.Seat.spec),Aspect=cameraView.aspect,
                Camera=V(cam),Pivot=V(pivot),Yaw=yaw,Pitch=pitch,Fov=cameraView.fieldOfView,FirstPerson=presentation.FirstPerson};
            var shot=chooserCamera.Frame(input,chooserCast??=ChooserCast);if(!shot.Applied)return;
            chooserFov=cameraView.fieldOfView;t.SetPositionAndRotation(V(shot.Position),Quaternion.Euler(shot.Pitch,shot.Yaw,0));cameraView.fieldOfView=shot.Fov;
            // The view model never shows from a camera off the eye; my body does once the camera is clear of the head.
            viewModelCamera.enabled=false;presentation.AimHeld=false;if(shot.Body)presentation.FirstPerson=false;
            chooserFramed=shot.Weight>=NativeChooserCamera.HudFrom;
        }
        // The world sphere cast, as the shoulder rigs cast (the same mask and query).
        private float ChooserCast(N.Vector3 from,N.Vector3 direction,float distance,float radius)
            =>Physics.SphereCast(V(from),radius,V(direction),out var hit,distance,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)?hit.distance:distance;
    }

    public sealed partial class ArenaPresentation
    {
        /// <summary>Pilot `id`'s wrist hologram panel centre (world) as last posed, for the chooser camera's framing; false while its look
        /// is down or not posed lately.</summary>
        internal bool ChipSelectPanel(int id,out Vector3 world)
        {
            world=default;if(!chipSelect.TryGetValue(id,out var slot)||slot.Frame<Time.frameCount-2||slot.Weight<=.05f)return false;
            var v=slot.Pose.PanelCentre;world=slot.Root.MultiplyPoint3x4(new Vector3(-v.X,v.Y,v.Z));return true;
        }
    }
}
