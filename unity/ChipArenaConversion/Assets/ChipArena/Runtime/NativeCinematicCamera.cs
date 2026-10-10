using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// C16 private cinematic cameras, seen only by the pilots in them (renderer.ts:121/:141/:155); everyone else, and every
    /// spectator, keeps the arena camera and the baked in-world scenes. The paths and timings are Core/NativeCinematicRules.cs.
    /// <para>Raging Demon victim screen (demon-cinematic.ts): from contact to the end of the combo the dead pilot sees the
    /// in-world baked scene (NativeSceneMotion "death-dev-raging-demon") alone, through its own camera on the source's backdrop.
    /// The camera is placed in the baked scene's fixture frame and carried by the same root as the scene, so it frames what is
    /// drawn; that scene is routed to this camera's layer while the screen is up.</para>
    /// <para>Last Laugh (siren/cutscene.ts): the held pilot and the Siren's pilot get the authored shots with letterbox bars and
    /// no HUD; the arena camera is directed, then UpdateCamera's normal path takes over again. Both peers read the victim's
    /// replicated holder and catch/crush/release timeline; no distance-based victim inference.</para>
    /// Both stop for the killcam, previews, intros, drafts and a new arena (the frame is not continuous), keep running under
    /// the pause menu on the match clock (so an offline pause holds them), and leave no camera state behind: BeginFrame turns the
    /// screen off at the start of every LateUpdate and the arena camera is rebuilt by UpdateCamera when not directed.
    /// </summary>
    public sealed class NativeCinematicCamera
    {
        /// <summary>The only layer the victim screen renders; nothing else uses it (8, 9, 10, 11 and 30 are taken).</summary>
        public const int Layer=29;
        private static readonly V3 Fixture=new V3(0,.865f,0);
        private readonly NativeSirenCutscene siren=new NativeSirenCutscene();
        private Camera screen;private int lastFrame=-10,generation=-1;private float bars;
        /// <summary>The Last Laugh cutscene owns the arena camera this frame (no HUD, no mouse look, no hit shake).</summary>
        public bool Directing{get;private set;}
        /// <summary>The Raging Demon victim screen covers the arena this frame (no world-anchored HUD).</summary>
        public bool Private{get;private set;}
        public void BeginFrame(){Directing=Private=false;bars=0;if(screen!=null)screen.enabled=false;}
        /// <summary>Called first in UpdateCamera; true when the Last Laugh cutscene placed the arena camera.</summary>
        public bool Direct(NativeArena arena,ArenaPresentation presentation,Camera view,bool phaseOk)
        {
            int frame=Time.frameCount;bool continuous=lastFrame==frame-1;lastFrame=frame;
            if(arena==null||presentation==null||view==null){siren.Stop();return false;}
            if(!continuous||arena.Generation!=generation){generation=arena.Generation;siren.Stop();}
            // arena.ViewPilot without its per-call lambda: the local pilot, or the one it controls (renderer.ts viewId).
            var local=Find(arena.Pilots,arena.LocalId);var me=local?.Controlling??local;if(me?.Seat==null){siren.Stop();return false;}
            VictimScreen(arena,presentation,view,me);
            return Cutscene(arena,presentation,view,me,presentation.CinematicClock(arena),phaseOk);
        }
        private void VictimScreen(NativeArena arena,ArenaPresentation presentation,Camera view,NativePilot me)
        {
            var d=me.Death;
            // demonScreen: the local pilot's own death by the Raging Demon (a PvE kept body has no baked demon scene).
            if(me.Hp>0||d==null||d.kept||d.style!="dev-raging-demon")return;
            // The baked scene ages on the match clock (PresentDeath); the camera follows the same age.
            if(!NativeDemonScreen.Screen(arena.Time-d.at,Fixture,0,false,default,view.aspect,out var f))return;
            if(screen==null)
            {
                var go=new GameObject("Raging Demon victim screen");go.transform.SetParent(view.transform.parent,false);screen=go.AddComponent<Camera>();
                screen.clearFlags=CameraClearFlags.SolidColor;screen.nearClipPlane=.05f;screen.farClipPlane=60;screen.depth=view.depth+.5f;screen.enabled=false;
            }
            Matrix4x4 root=Matrix4x4.TRS(d.position-Vector3.up*.865f,Quaternion.Euler(0,d.yaw,0),Vector3.one);
            Vector3 eye=root.MultiplyPoint3x4(Mirror(f.Eye)),look=root.MultiplyPoint3x4(Mirror(f.Look));
            screen.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye,Vector3.up));screen.fieldOfView=f.Fov;
            // Core gives three's linear backdrop; a Unity camera's clear colour is authored in sRGB (converted in Linear).
            screen.backgroundColor=new Color(f.R,f.G,f.B).gamma;screen.cullingMask=f.Black?0:1<<Layer;screen.enabled=true;
            presentation.RouteCinematicDeath(d.id,d.seed,screen);Private=true;
        }
        private bool Cutscene(NativeArena arena,ArenaPresentation presentation,Camera view,NativePilot me,float time,bool phaseOk)
        {
            NativePilot owner=null;int victimId=0;float at=0,crushAt=0,releaseAt=0;var pilots=arena.Pilots;
            for(int i=0;i<pilots.Count&&owner==null;i++)
            {
                var q=pilots[i];if(q?.Seat==null||!q.Seat.connected)continue;
                if(!arena.Replica){var m=q.Siren?.Move;if(m?.Caught?.Seat==null||q!=me&&m.Caught!=me)continue;owner=q;victimId=m.Caught.Seat.id;at=m.CaughtAt;crushAt=m.CrushAt;releaseAt=m.Until;}
                else{var a=q.Pose;if(!NativeSirenVictimPose.Active(a,time)||q!=me&&a.GrabOwner!=me.Seat.id)continue;owner=Find(pilots,a.GrabOwner);victimId=q.Seat.id;at=a.GrabAt;crushAt=a.GrabCrushAt;releaseAt=a.GrabUntil;}
            }
            if(!siren.Check(owner!=null,owner?.Seat.id??0,victimId,owner!=null&&owner!=me,at,crushAt,releaseAt,time,phaseOk))return false;
            var o=Find(pilots,siren.Owner);var v=Find(pilots,siren.Victim);
            float scale=o?.Pose!=null&&o.Pose.RigScale>0?o.Pose.RigScale:1;if(o?.Siren!=null&&o.Siren.Scale>0)scale=o.Siren.Scale;
            Vector3 head=default;bool victimHead=v!=null&&v.Hp>0&&presentation.CinematicHead(v.Seat.id,out head);
            // Unity's world is the source's mirrored in X (models are flipped to keep their handedness): mirror in and out.
            siren.Track(time,o?.Body!=null,o?.Body!=null?Mirror(o.Body.transform.position):default,o!=null?-o.Yaw*Mathf.Deg2Rad:0,scale,false,default,victimHead,Mirror(head));
            var shot=siren.ShotAt(time);Vector3 position=Mirror(shot.Position),look=Mirror(shot.Look),ray=position-look;float distance=ray.magnitude;
            // Out of walls (renderer.ts:141): the first prop surface past .12 m along the ray from the subject pulls the camera in.
            if(distance>.05f&&arena.Root!=null)
            {
                ray/=distance;
                if(arena.Root.scene.GetPhysicsScene().Raycast(look+ray*.12f,ray,out var hit,Mathf.Max(0,distance-.12f),NativeArena.WorldMask,QueryTriggerInteraction.Ignore))
                {var moved=NativeSirenCutscene.Occlude(new V3(position.x,position.y,position.z),new V3(look.x,look.y,look.z),hit.distance+.12f);position=new Vector3(moved.x,moved.y,moved.z);}
            }
            Vector3 forward=look-position;if(forward.sqrMagnitude<1e-8f)forward=view.transform.forward;
            NativeSirenCutscene.Shake(time,shot.Shake,out float dx,out float dy,out float roll);
            view.transform.SetPositionAndRotation(position+new Vector3(-dx,dy,0),Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(-roll*Mathf.Rad2Deg,Vector3.forward));
            view.fieldOfView=shot.Fov;presentation.FirstPerson=false;presentation.AimHeld=false;bars=siren.Letterbox(time);Directing=true;return true;
        }
        /// <summary>The cutscene's letterbox (two black bars sliding in at the catch and out at the end), on the GUI canvas.</summary>
        public void Letterbox(float width,float height)
        {
            if(!Directing||bars<=0||Event.current.type!=EventType.Repaint)return;
            float bar=NativeSirenCutscene.Bars*height,shift=(1-bars)*bar;var old=GUI.color;GUI.color=Color.black;
            GUI.DrawTexture(new Rect(-4,-shift,width+8,bar),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(-4,height-bar+shift,width+8,bar),Texture2D.whiteTexture);GUI.color=old;
        }
        public void Dispose(){siren.Stop();Directing=Private=false;if(screen!=null)Object.Destroy(screen.gameObject);screen=null;}
        private static NativePilot Find(System.Collections.Generic.List<NativePilot> pilots,int id)
        {if(id==0)return null;for(int i=0;i<pilots.Count;i++)if(pilots[i]?.Seat!=null&&pilots[i].Seat.id==id)return pilots[i];return null;}
        private static Vector3 Mirror(V3 v)=>new Vector3(-v.x,v.y,v.z);
        private static V3 Mirror(Vector3 v)=>new V3(-v.x,v.y,v.z);
    }
    public sealed partial class ConversionGame {private readonly NativeCinematicCamera cinematic=new NativeCinematicCamera();}
    public sealed partial class ArenaPresentation
    {
        private int cinematicId,cinematicSeed=-1,cinematicFrame=-1;private Camera cinematicCamera;
        internal float CinematicClock(NativeArena arena)=>FieldClock(arena);
        /// <summary>The drawn rig's head (last presented frame), as siren/cutscene.ts reads rig.head.</summary>
        internal bool CinematicHead(int id,out Vector3 head)
        {
            head=default;if(!rigs.TryGetValue(id,out var rig)||rig?.Bones==null||rig.Bones.Length<=CapturedRig.Head||rig.Bones[CapturedRig.Head]==null)return false;
            head=rig.Bones[CapturedRig.Head].position;return true;
        }
        /// <summary>This frame, draw that death's baked scene only on the victim screen's layer and camera.</summary>
        internal void RouteCinematicDeath(int id,int seed,Camera camera){cinematicId=id;cinematicSeed=seed;cinematicCamera=camera;cinematicFrame=Time.frameCount;}
        private void CinematicRoute(NativeDeath death,out int layer,out Camera camera)
        {
            bool on=cinematicFrame==Time.frameCount&&cinematicCamera!=null&&death!=null&&death.id==cinematicId&&death.seed==cinematicSeed;
            layer=on?NativeCinematicCamera.Layer:0;camera=on?cinematicCamera:null;
        }
    }
}
