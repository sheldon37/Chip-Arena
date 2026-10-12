using UnityEngine;
using N=System.Numerics;

namespace ChipArena.UnityConversion
{
    // Settings · Camera (Core/NativeCameraRig.cs): the shoulder rigs, their aim convergence and lock-on framing, the swap key,
    // and their sprint FOV kick and bob. Classic, once settled, runs UpdateCamera's original code and sends the look unchanged;
    // first person is unchanged. Presentation and the client's input look only: the authority and the wire format are untouched.
    public sealed partial class ConversionGame
    {
        private readonly NativeCameraRig cameraRig=new NativeCameraRig();private readonly NativeCameraSprint cameraSprint=new NativeCameraSprint();
        private NativeCameraRig.Cast cameraCast;
        // shoulderView: a shoulder rig (or a blend through one) placed this frame; convergeAim: it is the live local view.
        private bool shoulderView,convergeAim;private float aimSkip,lockLift;
        private static N.Vector3 V(Vector3 v)=>new N.Vector3(v.x,v.y,v.z);
        private static Vector3 V(N.Vector3 v)=>new Vector3(v.X,v.Y,v.Z);
        // Selects the saved mode on the rig and reports whether Classic is settled (UpdateCamera then runs the original camera).
        private bool CameraClassic(bool first)
        {
            cameraRig.Select(prefs.cameraMode,prefs.shoulderLeft,Time.unscaledTime);bool classic=cameraRig.Classic(Time.unscaledTime);shoulderView=!first&&!classic;return classic;
        }
        // The shoulder rig's camera position: the eye focus with its smoothed height, sphere casts against the world (the same
        // mask and query as the original camera), so Brickfall blocks and fragments push it in like any wall.
        private Vector3 ShoulderCamera(NativePilot p,Vector3 focus)
        {
            cameraCast??=(from,direction,distance,radius)=>Physics.SphereCast(V(from),radius,V(direction),out var hit,distance,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)?hit.distance:distance;
            float height=cameraRig.Focus(focus.y,p.Seat.id,Time.unscaledDeltaTime);
            var at=cameraRig.Place(new N.Vector3(focus.x,height,focus.z),yaw,pitch,Time.unscaledTime,Time.unscaledDeltaTime,cameraCast);
            lockLift=cameraRig.PivotUp+(height-focus.y);return V(at);
        }
        // After the camera is placed: the depth of the pilot's eye along the crosshair ray (hits nearer than that are behind its
        // shot origin and are skipped by AimPoint), and whether this view converges the sent look.
        private void CameraAim(NativePilot p)
        {
            convergeAim=shoulderView&&Spectator==null&&!menu;
            aimSkip=convergeAim?NativeAimConvergence.Skip(V(cameraView.transform.position),V(cameraView.transform.forward),V(p.Body.position+Vector3.up*NativeViewRules.EyeHeight)):0;
        }
        // The point under the crosshair (Core/NativeAimConvergence): world or a pilot or minion hit volume, the view pilot
        // excluded, beyond the pilot's own depth on the ray; `body` when it is a pilot or minion.
        private Vector3 CrosshairPoint(float range,out bool body)
        {
            Ray ray=cameraView.ViewportPointToRay(new Vector3(.5f,.5f));float nearest=range+Vector3.Distance(ray.origin,arena.ViewPilot.Body.position);
            Vector3 target=ray.origin+ray.direction*nearest;body=false;
            foreach(var hit in Physics.RaycastAll(ray,nearest,NativeArena.ShotMask,QueryTriggerInteraction.Ignore))
            {
                if(hit.distance<aimSkip||hit.distance>=nearest)continue;var pilot=hit.collider.GetComponent<NativePilotBody>();if(pilot!=null&&pilot.Pilot==arena.ViewPilot)continue;
                target=hit.point;nearest=hit.distance;body=pilot!=null||hit.collider.GetComponent<NativeMinionBody>()!=null;
            }
            return target;
        }
        // Aim convergence: the sent Yaw/Pitch point the pilot's eye (the authority's ability aim origin) at the crosshair point,
        // within 25 degrees of the look; AimTarget and AbilityTarget stay the camera-ray points the authority fires the muzzle
        // at. Melee turns only toward a pilot or minion under the crosshair (the floor or a wall keeps the camera's look).
        private void ConvergeAim(NativePilot p)
        {
            if(!convergeAim||cinematic.Directing)return;
            var point=CrosshairPoint(Mathf.Max((float)p.Weapon.range,NativeAimConvergence.FarRange),out bool body);
            if(p.Weapon.@class=="melee"&&!body){var ray=cameraView.ViewportPointToRay(new Vector3(.5f,.5f));point=ray.origin+ray.direction*(NativeAimConvergence.FarRange+Vector3.Distance(ray.origin,p.Body.position));}
            NativeAimConvergence.Angles(V(p.Body.position+Vector3.up*NativeViewRules.EyeHeight),V(point),yaw,pitch,out float y,out float up);
            pending.Yaw=y;pending.Pitch=up;
        }
        // The lock-on framing: first person and Classic keep the original look from the eye (3.438 degrees under the line);
        // a shoulder rig puts its crosshair ray itself on the target (NativeCameraRig.LookAt from the rig's shoulder point).
        private void LockLook(Vector3 eye,Vector3 point,out float wantYaw,out float wantPitch)
        {
            Vector3 d=point-eye;
            if(!shoulderView||presentation.FirstPerson){wantYaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;wantPitch=Mathf.Clamp(-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg+3.438f,-63,63);return;}
            NativeCameraRig.LookAt(V(eye),V(point),cameraRig.PivotSide,lockLift,out wantYaw,out wantPitch);wantPitch=Mathf.Clamp(wantPitch,-63,63);
        }
        // X swaps the shoulder of the shoulder rigs (and Close action) in a match, never while typing or with the board open.
        private void CameraKeys(bool typing)
        {
            if(!Input.GetKeyDown(KeyCode.X)||arena==null||typing||BoardOpen||!NativeCameraModes.Swaps(prefs.cameraMode,prefs.firstPerson))return;
            prefs.shoulderLeft=!prefs.shoulderLeft;Save();Notice(NativeCameraModes.Name(prefs.cameraMode,prefs.shoulderLeft)+" · "+NativeCameraModes.SwapKey+" to swap");
        }
        private string CameraName=>prefs.firstPerson?"First person":NativeCameraModes.Name(prefs.cameraMode,prefs.shoulderLeft);
        // The shoulder rigs' sprint feedback: the first-person FOV kick and bob values (camera shake only, halved by Reduced
        // effects) from the view pilot's replicated sprint; none in Classic, for a spectated pilot or in the menu.
        private void ShoulderSprint(float time){cameraSprint.Step(arena?.ViewPilot?.Pose,time,Time.unscaledDeltaTime);}
        private float ShoulderSprintFov()=>shoulderView&&Spectator==null&&!menu?NativeSprintRules.Fov(cameraSprint.Weight,prefs.cameraShake,prefs.reducedEffects):0;
        private void ShoulderSway()
        {
            if(!shoulderView||presentation.FirstPerson)return;NativeSprintRules.CameraSway(cameraSprint.Weight,cameraSprint.Stride,prefs.cameraShake,prefs.reducedEffects,out float up,out float roll);
            if(up==0&&roll==0)return;var camera=cameraView.transform;camera.SetPositionAndRotation(camera.position+camera.up*up,camera.rotation*Quaternion.Euler(0,0,roll));
        }
    }
}
