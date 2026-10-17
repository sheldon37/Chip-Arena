using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The instance exit's laser door in the live scene (Claude, 2026-10-17; docs/EXPEDITION-PRESENTATION.md "The laser door"):
    /// the door itself is drawn by the state pieces (Core NativeExpeditionStateScene, Laser.cs) from the bound view; this adds its
    /// cue sounds (lock, the SEALED snap, unlock: keys with today's clips as fallbacks) and its hum, the HUD notice (the door locks
    /// behind the party, out of sight), and the DEV preview that cycles lock, sealed, unlock and open on the current instance's
    /// exit (or, without an Expedition, on an instance map loaded in the sandbox). Presentation only: the closure, its collider
    /// and every rule are the director's (Astra).
    /// </summary>
    public sealed partial class ConversionGame
    {
        /// <summary>Cue sound keys (the owner generates the clips later; until then each plays the fallback clip named beside it).</summary>
        public const string DoorLock="expedition.door.lock",DoorLockSnap="expedition.door.lock.snap",DoorUnlock="expedition.door.unlock";
        private double laserLockHeard=double.NaN,laserSnapHeard=double.NaN,laserUnlockHeard=double.NaN;
        private readonly ExpeditionClosureView laserPreview=new ExpeditionClosureView();
        private ExpeditionView laserPreviewView;private ExpeditionStateRig laserPreviewRig;private string laserPreviewMap;
        private readonly Dictionary<string,string> laserSealedSub=new Dictionary<string,string>(),laserOpenSub=new Dictionary<string,string>();

        private void EnsureExpeditionLayout()
        {
            if(expeditionLayoutRead||content==null)return;expeditionLayoutRead=true;
            try{var asset=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName);if(asset!=null){expeditionLayout=NativeExpeditionLayout.Parse(asset.text);expeditionLayoutFingerprint=NativeExpeditionLayout.Compatibility(asset.text,content);}}
            catch(System.Exception e)when(e is System.FormatException||e is System.ArgumentException||e is System.InvalidOperationException){Debug.LogWarning("Expedition layout presentation disabled: "+e.Message);expeditionLayout=null;}
        }
        /// <summary>The DEV preview's stand-in for the exit of `rig` at t (null when the preview is off or the area has no exit).</summary>
        private ExpeditionClosureView LaserDoorPreviewFor(ExpeditionStateRig rig,double t)
        {
            if(!NativeLaserDoorPreview.On||rig==null||rig.World)return null;
            foreach(var d in rig.Doors)if(d.Look==ExpeditionDoorLook.Laser){NativeLaserDoorPreview.Fill(laserPreview,d.Anchor,rig.Area,t);return laserPreview;}
            return null;
        }
        /// <summary>Without an Expedition: the DEV preview on an instance map loaded in the sandbox (its exit from the manifest).</summary>
        private void LaserDoorPreviewFrame()
        {
            if(LiveExpedition!=null||!NativeLaserDoorPreview.On||arena==null||cameraView==null||menu||IntroActive||ReplayActive){laserPreviewMap=null;return;}
            EnsureExpeditionLayout();if(expeditionLayout?.areas==null)return;
            if(laserPreviewMap!=arena.MapId)
            {
                laserPreviewMap=arena.MapId;laserPreviewRig=null;laserPreviewView=null;
                foreach(var a in expeditionLayout.areas)if(a!=null&&a.role=="instance"&&a.map==arena.MapId){laserPreviewRig=ExpeditionStateRig.For(expeditionLayout,a.id);break;}
                if(laserPreviewRig!=null)laserPreviewView=new ExpeditionView{run="dev-door-preview",area=laserPreviewRig.Area,closures=new[]{laserPreview}};
            }
            if(laserPreviewRig==null)return;double t=Time.unscaledTimeAsDouble;var c=LaserDoorPreviewFor(laserPreviewRig,t);if(c==null)return;
            laserPreviewView.elapsed=t;expeditionStates.DoorPreview=null;
            var cam=cameraView.transform;Vector3 e=cam.position,r=cam.right,u=cam.up;var eye=new ExpeditionEye{X=e.x,Y=e.y,Z=e.z,Rx=r.x,Ry=r.y,Rz=r.z,Ux=u.x,Uy=u.y,Uz=u.z};
            expeditionStates.HideSanctuaryEdge=true;expeditionStates.Build(laserPreviewView,laserPreviewRig,null,t,eye,prefs.reducedEffects,presentation.LowGraphics);
            if(expeditionStatesDraw==null&&!expeditionStatesFailed){expeditionStatesDraw=NativeExpeditionStatesDraw.Create();expeditionStatesFailed=expeditionStatesDraw==null;}
            if(expeditionStatesDraw!=null)expeditionStatesDraw.Draw(expeditionStates,cameraView,presentation.UsesInstancing,Time.unscaledTime);
            ExpeditionLaserSounds(t);
        }
        /// <summary>The door's sounds at presentation time t: each lock, its SEALED snap and each unlock once, from the start times the
        /// state pieces recorded (a door first seen long after either plays nothing), and the hum while its beams stand.</summary>
        private void ExpeditionLaserSounds(double t)
        {
            if(nativeAudio==null)return;var s=expeditionStates;
            if(!s.LaserDrawn){nativeAudio.DoorHum(false,default);return;}
            var at=new Vector3(s.LaserX,s.LaserY,s.LaserZ);double lk=s.LaserLockStart,ul=s.LaserUnlockStart,snap=lk+NativeExpeditionLaserDoor.SealAt*NativeExpeditionLaserDoor.LockSeconds;
            if(!double.IsNaN(lk)&&lk!=laserLockHeard&&t>=lk&&t-lk<.3){laserLockHeard=lk;nativeAudio.Cue(DoorLock,"shield",at,.6f,1.25f);}
            if(!double.IsNaN(lk)&&lk!=laserSnapHeard&&t>=snap&&t-snap<.3){laserSnapHeard=lk;nativeAudio.Cue(DoorLockSnap,"electric-hook-hit",at,.55f,.9f);}
            if(!double.IsNaN(ul)&&ul!=laserUnlockHeard&&t>=ul&&t-ul<.3){laserUnlockHeard=ul;nativeAudio.Cue(DoorUnlock,"electric-hook",at,.55f,.75f);}
            nativeAudio.DoorHum(s.LaserBeamsLit>0,at);
        }
        /// <summary>The HUD notice for the laser door of the view's area: ENTRY SEALED as it locks, EXIT UNSEALED as it opens.</summary>
        private void ExpeditionLaserNotice(ExpeditionView v,float width,float height,double t)
        {
            var s=expeditionStates;if(!s.LaserDrawn||v==null||s.LaserArea==null)return;
            double lock_=t-s.LaserLockStart,open=t-s.LaserUnlockStart;var zone=NativeExpeditionJoinZones.Of(s.LaserArea);
            if(open>=0&&open<NativeExpeditionLaserDoor.NoticeSeconds&&(s.LaserStage==ExpeditionLaserStage.Unlock||s.LaserStage==ExpeditionLaserStage.Open))
                NativeExpeditionStatesHud.DoorNotice(width,height,(float)open,true,Sub(laserOpenSub,s.LaserArea,zone.Name," CLEARED"),zone.Accent,prefs.reducedEffects);
            else if(lock_>=0&&lock_<NativeExpeditionLaserDoor.NoticeSeconds&&(s.LaserStage==ExpeditionLaserStage.Lock||s.LaserStage==ExpeditionLaserStage.Sealed))
                NativeExpeditionStatesHud.DoorNotice(width,height,(float)lock_,false,Sub(laserSealedSub,s.LaserArea,zone.Name," · SEALED UNTIL CLEARED"),zone.Accent,prefs.reducedEffects);
        }
        private static string Sub(Dictionary<string,string> cache,string area,string name,string tail)
        {if(!cache.TryGetValue(area,out var s)){if(cache.Count>16)cache.Clear();s=cache[area]=string.IsNullOrEmpty(name)?null:name+tail;}return s;}
    }
}
