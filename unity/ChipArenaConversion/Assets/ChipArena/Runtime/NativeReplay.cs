using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    // A presentation-only scene. It never advances physics or replaces the live match/input state.
    public sealed partial class ConversionGame
    {
        private readonly List<NativeGameFrame> replayFrames=new List<NativeGameFrame>();private int replayGeneration=-1,replayEntities,replaySeen,replayView,spectatorId;private float replayNext,replayDeath=-1,replayStart,replayEnd,replayStarted,replayWait;private bool replayWasAlive,replayRunning,replayBody;
        private NativeArena replayArena;private Scene replayScene;private NativeGameFrame replaySource;private readonly Dictionary<Renderer,bool> liveVisibility=new Dictionary<Renderer,bool>();
        private bool ReplayActive=>replayRunning;
        private NativePilot Spectator=>arena?.Local?.Hp<=0?arena.Pilots.FirstOrDefault(p=>p.Seat.id==spectatorId&&p.Hp>0&&p.Seat.connected):null;
        private void ObserveReplay()
        {
            if(arena?.Local==null)return;if(replayGeneration!=arena.Generation){ClearReplay();replayGeneration=arena.Generation;}
            var p=arena.Local;bool allowed=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result||match.Phase==NativePhase.Match;if(!allowed){EndReplay();return;}
            if(p.Hp>0){EndReplay();spectatorId=0;}var d=p.Hp<=0?p.Death:null;bool fresh=d!=null&&replaySeen!=d.seed;
            if(fresh){replaySeen=d.seed;if(replayWasAlive&&replayFrames.Count>1&&arena.Time-replayFrames[0].time>=.2f){replayDeath=d.at;replayWait=Time.unscaledTime+1.6f;replayView=d.owner>0?d.owner:p.Seat.id;}}
            replayWasAlive=p.Hp>0;if(arena.Time<replayNext&&!fresh)return;replayNext=arena.Time+.05f;
            // JsonUtility provides a deep copy of mutable authority data; entity and time budgets bound history.
            var frame=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(arena.NetworkFrame()));replayFrames.Add(frame);replayEntities+=ReplaySize(frame);
            while(replayFrames.Count>1&&(replayFrames.Count>132||replayEntities>150000||arena.Time-replayFrames[0].time>6)){replayEntities-=ReplaySize(replayFrames[0]);replayFrames.RemoveAt(0);}
        }
        private static int ReplaySize(NativeGameFrame f)=>f.pilots.Length+f.props.Length+f.minions.Length+f.effects.Length+f.snapshot.Shots.Length+f.snapshot.Events.Length;
        private void ClearReplay(){EndReplay();replayFrames.Clear();replayEntities=0;replayNext=0;replaySeen=0;replayWasAlive=false;spectatorId=0;}
        private void EndReplay()
        {
            replayDeath=-1;if(!replayRunning&&replayArena==null)return;replayRunning=false;replaySource=null;replayArena?.Dispose();replayArena=null;if(replayScene.IsValid())SceneManager.UnloadSceneAsync(replayScene);
            foreach(var pair in liveVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;liveVisibility.Clear();presentation?.ResetPresentation();nativeAudio?.ResetCursor();
        }
        private void SpectatorControls()
        {
            if(arena?.Local==null||arena.Local.Hp>0||menu)return;if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape))EndReplay();
            var candidates=arena.Pilots.Where(p=>p!=arena.Local&&p.Hp>0&&p.Seat.connected&&!(p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true)).OrderBy(p=>match.SameSide(arena.Local.Seat,p.Seat)?0:1).ThenBy(p=>p.Seat.id).ToArray();if(candidates.Length==0){spectatorId=0;return;}
            int at=Array.FindIndex(candidates,p=>p.Seat.id==spectatorId);if(at<0)at=0;else if(Input.GetKeyDown(KeyCode.RightArrow))at=(at+1)%candidates.Length;else if(Input.GetKeyDown(KeyCode.LeftArrow))at=(at+candidates.Length-1)%candidates.Length;spectatorId=candidates[at].Seat.id;
            if(!replayRunning){yaw+=Input.GetAxisRaw("Mouse X")*2*prefs.sensitivity;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*prefs.sensitivity,-70,70);}
        }
        private bool RenderReplay()
        {
            if(menu||arena.Local.Hp>0){EndReplay();return false;}
            if(!replayRunning&&replayDeath>=0&&(arena.Time>=replayDeath+.6f||Time.unscaledTime>=replayWait))
            {
                if(replayFrames.Count<2){replayDeath=-1;return false;}replayStart=Math.Max((float)replayFrames[0].time,replayDeath-3);replayEnd=replayDeath+1.25f;replayStarted=Time.unscaledTime;replayRunning=true;replayBody=false;
                replayScene=SceneManager.CreateScene("Killcam presentation",new CreateSceneParameters(LocalPhysicsMode.Physics3D));replayArena=new NativeArena(content,new NativeMatch(content),arena.MapId){Replica=true,LocalId=arena.LocalId};presentation.ResetPresentation();nativeAudio.ResetCursor();

            }
            if(!replayRunning)return false;foreach(var r in arena.Root.GetComponentsInChildren<Renderer>()){if(!liveVisibility.ContainsKey(r))liveVisibility[r]=r.enabled;r.enabled=false;}float time=replayStart+Time.unscaledTime-replayStarted;if(time>replayEnd||time>replayFrames[replayFrames.Count-1].time+.5){EndReplay();return false;}
            var a=replayFrames[0];var b=a;foreach(var f in replayFrames){if(f.time>time){b=f;break;}a=b=f;}
            if(replaySource!=a){var copy=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(a));replayArena.ApplyFrame(copy,arena.LocalId);SceneManager.MoveGameObjectToScene(replayArena.Root,replayScene);replaySource=a;}
            float blend=Mathf.Clamp01((time-(float)a.time)/Math.Max(.00001f,(float)(b.time-a.time)));
            foreach(var p in replayArena.Pilots){var x=a.pilots.First(v=>v.id==p.Seat.id);var y=b.pilots.FirstOrDefault(v=>v.id==p.Seat.id);if(y!=null&&(x.hp>0)==(y.hp>0)){p.Body.position=Vector3.Lerp(x.position,y.position,blend);p.Yaw=Mathf.LerpAngle(x.yaw,y.yaw,blend);p.Pitch=Mathf.Lerp(x.pitch,y.pitch,blend);}}
            foreach(var p in replayArena.Props){var x=a.props.FirstOrDefault(v=>v.id==p.Definition.id);var y=b.props.FirstOrDefault(v=>v.id==p.Definition.id);if(x!=null&&y!=null&&p.Body!=null)p.transform.SetPositionAndRotation(Vector3.Lerp(x.position,y.position,blend),Quaternion.Slerp(x.rotation,y.rotation,blend));}
            var target=replayArena.Pilots.Find(p=>p.Seat.id==replayView);if(time>replayDeath&&(target==null||target.Hp<=0||Vector3.Distance(target.Body.position,replayArena.Local.Body.position)>24))replayBody=true;if(replayBody||target==null)target=replayArena.Local;
            Vector3 focus=target.Body.position+Vector3.up*(NativeViewRules.EyeHeight+.22f)+Quaternion.Euler(0,target.Yaw,0)*Vector3.right*.85f;Quaternion rotation=Quaternion.Euler(-target.Pitch,target.Yaw,0);Vector3 offset=rotation*Vector3.back*NativeViewRules.ThirdDistance+Vector3.up*NativeViewRules.ThirdLift;
            if(replayScene.GetPhysicsScene().SphereCast(focus,.12f,offset.normalized,out var hit,offset.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))offset=offset.normalized*Math.Max(.15f,hit.distance-.1f);cameraView.transform.SetPositionAndRotation(focus+offset,rotation);cameraView.fieldOfView=NativeViewRules.ThirdFov;viewModelCamera.enabled=false;presentation.FirstPerson=false;presentation.AimHeld=false;presentation.LocalId=target.Seat.id;
            presentation.Render(replayArena.Snapshot(),time,false);presentation.RenderAbilities(replayArena,null);presentation.RenderGlow(replayArena,cameraView);presentation.FinishSourceFrame();nativeAudio.Render(replayArena,false,cameraView);return true;
        }
        private void ReplayHud(float width,float height)
        {
            if(replayRunning){GUI.Box(new Rect(width/2-240,65,480,82),"");GUI.Label(new Rect(width/2-220,76,440,35),"ELIMINATION REPLAY",heading);GUI.Label(new Rect(width/2-220,113,440,25),"Space to skip · the live match continues",small);}
            else if(arena?.Local?.Hp<=0){var p=Spectator;GUI.Label(new Rect(width/2-220,height-120,440,60),p==null?"No surviving players":"SPECTATING "+p.Seat.name+"\n← / → switch player",label);}
        }
    }
}
