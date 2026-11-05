using System;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // THE HUM in the game (docs/EXPEDITION-BOSS-BACKROOMS.md section 3). Presentation only: it reads the boss's record (HumBossViewOf, the one binding point) and the presentation clock, and draws; no hit,
    // collider, timing, damage, cooldown, movement or network state is read back or changed.
    //  - Model: Runtime NativeHumBossBody.cs (the bank's smooth skinned body and its lit parts on Core HumBossRig's pose).
    //  - The room: Runtime NativeHumBossRoom.cs (Core HumBossStage: each lamp, the partitions, the air; Core NativeHumSurface: the tears).
    //  - Effects: Core NativeHumBossFx: its telegraphs and floor lights through one NativeFxDraw, its particles through the shared effects renderer (NativeBossVfxSubmit).
    //  - Audio: the boss.hum.* keys queued here, played by NativeAudio's bank path (silent until clips exist).
    //  - DEV preview (DEV panel · Camera · "Level 0 boss preview", on the Level 0 map): Core NativeHumBossPreview's script: the intro with its card, every move, both phase changes, breaks, a stagger, the ceiling, the death, in the carved room.
    // Normal play: HumBossViewOf returns None, so nothing is drawn and the level is exactly as it is. Allocation free per frame.
    public sealed partial class ArenaPresentation
    {
        // ================================================================================================================
        // The binding (Astra): the boss's replicated record with the actor's root, yaw and health and its arrival's `spawned`, in the Core HumBossView (times on the arena clock, NaN for the wire's -100). One place.
        // Until then every arena has none: nothing draws.
        // ================================================================================================================
        internal static HumBossView HumBossViewOf(NativeArena arena)=>HumBossView.None;

        private readonly HumBossRig humBoss=new HumBossRig();private readonly NativeHumBossFx humBossFx=new NativeHumBossFx();private readonly HumBossStage humStage=new HumBossStage();
        private HumBossView humView=HumBossView.None;private float humLast=-1,humHp=float.NaN;private NativeArena humArena;private int humGeneration=-1;private NativeFxDraw humDraw;private NativeVfxDraw humVfx;private bool humVfxFailed,humShown;
        private NVector humPrev;private float humLastBlow=float.NegativeInfinity,humFlash;private readonly float[] humBlowAt=new float[8],humBlowSize=new float[8];private readonly NVector[] humBlowPush=new NVector[8];
        private readonly float[] humLightPos=new float[NativeMapLooks.MaxLights*4],humLightRgb=new float[NativeMapLooks.MaxLights*3];
        /// <summary>The boss's draws last frame (budget lines), its tier.</summary>
        public int HumBossDraws{get;private set;}public int HumBossTier{get;private set;}
        private static readonly BossFlatGround HumGround=new BossFlatGround(0);

        // SourceWorld, right after the level's own world (BackroomsWorld).
        private void HumBossScenery(NativeArena arena)
        {
            bool map=arena!=null&&NativeBackroomsGenerator.IsMap(arena.MapId);var eye=WorldEye;
            if(!map){HumBossHide();return;}
            float now=PresentationClock(arena);
            if(!ReferenceEquals(arena,humArena)||arena.Generation!=humGeneration){humArena=arena;humGeneration=arena.Generation;humLast=-1;humHp=float.NaN;humBossFx.ClearPrints();}
            NVector velocity=NVector.Zero;bool hasTarget=false;NVector target=NVector.Zero;HumRoom room=null;
            if(NativeHumBossPreview.On)
            {
                if(double.IsNaN(NativeHumBossPreview.Start))NativeHumBossPreview.Start=now-HumPreviewSkip();
                double since=now-NativeHumBossPreview.Start,loops=Math.Floor(since/NativeHumBossPreview.Loop);float p=(float)(since-loops*NativeHumBossPreview.Loop),baseAt=(float)(NativeHumBossPreview.Start+loops*NativeHumBossPreview.Loop);
                NativeHumBossPreview.View(p,baseAt,ref humView,out velocity,out var pt);hasTarget=true;target=NativeHumBossPreview.TargetPoint(pt);
                NativeHumBossPreview.Shown=humView;NativeHumBossPreview.ShownClock=now;room=NativeHumBossPreview.Room;
                int n=NativeHumBossPreview.Blows(p,baseAt,humBlowAt,humBlowPush,humBlowSize);for(int i=0;i<n;i++)if(!(humBlowAt[i]<=humLastBlow)){humBoss.Hit(humBlowAt[i],humBlowPush[i],humBlowSize[i]);humLastBlow=humBlowAt[i];humFlash=humBlowSize[i];}
            }
            else
            {
                humView=HumBossViewOf(arena);
                if(humView.live)
                {
                    var pilot=TargetPilot(arena,humView.actionTarget);if(pilot!=null){hasTarget=true;var q=pilot.Body.position;target=new NVector(q.x,q.y+1.2f,q.z);}
                    room=humRoomLive;
                    if(!float.IsNaN(humHp)&&humView.hp<humHp-.5f&&humView.maxHp>0){var from=arena.Local?.Body!=null?arena.Local.Body.position:new Vector3(humView.x,0,humView.z-1);humBoss.Hit(now,new NVector(humView.x-from.x,0,humView.z-from.z),Mathf.Clamp((humHp-humView.hp)/humView.maxHp*60,.25f,1.4f));humFlash=1;}
                    humHp=humView.hp;
                }
            }
            if(!humView.live){HumBossHide();return;}
            float dt=humLast<0?0:Mathf.Clamp(now-humLast,0,.1f);
            if(!NativeHumBossPreview.On&&humLast>=0&&dt>0)velocity=(new NVector(humView.x,humView.y,humView.z)-humPrev)/dt;
            humPrev=new NVector(humView.x,humView.y,humView.z);
            bool reduced=WeaponEffectsQuality<.5f;
            // The room's state first (the rig stutters with it), then the body, then what it does to the room.
            humStage.Evaluate(humView,now,HumBossBeats.Current,room,reduced);
            humBoss.Hitch=humStage.Hitch;humBoss.ReducedEffects=reduced;
            humBoss.Update(now,dt,humView,HumGround,velocity,false,hasTarget,target);
            humShown=true;
            var fx=humBossFx;fx.Room=room;
            if(eye!=null){var t=eye.transform;Vector3 e=t.position,r=t.right,u=t.up;fx.Build(humBoss,humView,humStage,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(EffectsQuality),LowGraphics,reduced);}
            HumBossDraws=0;
            HumBodyDraw(eye);
            if(room!=null)HumRoomDraw(room,eye);
            HumFxDraw(eye,now);
            if(humLast>=0)NativeHumBossCues.Queue(humView,humBoss,room,humLast,now);
            humLast=now;
        }
        // The Editor's capture tool can start the preview part way in (DEV only: a number in Temp/hum-preview-skip.txt).
        private static float HumPreviewSkip()
        {
#if UNITY_EDITOR
            try{const string f="Temp/hum-preview-skip.txt";if(System.IO.File.Exists(f)&&float.TryParse(System.IO.File.ReadAllText(f).Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float v))return v;}catch(System.IO.IOException){}
#endif
            return 0;
        }
        private void HumBossHide()
        {
            if(!humShown)return;humShown=false;humLast=-1;HumBodyHide();HumRoomHide();
        }
        private void HumFxDraw(Camera camera,float time)
        {
            if(camera==null)return;if(humDraw==null)humDraw=new NativeFxDraw();var q=humBossFx.Quads;humDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?humDraw.Solid:humDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            humDraw.Draw(camera,null,time);
            if(humVfx==null&&!humVfxFailed){try{humVfx=new NativeVfxDraw();}catch(Exception e){humVfxFailed=true;Debug.LogWarning("THE HUM: effects renderer unavailable ("+e.Message+")");}}
            if(humVfx!=null&&humBossFx.Vfx.Count>0){humVfx.Low=LowGraphics;humVfx.Begin();NativeBossVfxSubmit.Submit(humBossFx.Vfx,humVfx);humVfx.Submit(camera);HumBossDraws+=2;}
        }
        // ---------------- the DEV preview's camera and card ----------------
        internal bool HumBossPreviewShot(out Vector3 eye,out Vector3 look,out float fov)
        {
            eye=look=default;fov=60;if(!NativeHumBossPreview.On||double.IsNaN(NativeHumBossPreview.Start)||!humShown)return false;
            float clock=(float)NativeHumBossPreview.ShownClock;double since=clock-NativeHumBossPreview.Start;float p=(float)(since-Math.Floor(since/NativeHumBossPreview.Loop)*NativeHumBossPreview.Loop);float age=p-NativeHumBossPreview.SpawnAt;
            NVector ne,nl;
            if(age>=0&&age<NativeBackroomsBossIntro.Hold+.25f)
            {
                NativeHumArena.Spawn(NativeHumBossPreview.Room,out var root,out float yaw);
                if(!NativeBackroomsBossIntro.Sample(NativeHumBoss.Shots,age,root,yaw,out ne,out nl,out fov))return false;
            }
            else if(age<0){NativeHumArena.Spawn(NativeHumBossPreview.Room,out var root,out float yaw);NativeBackroomsBossIntro.Sample(NativeHumBoss.Shots,0,root,yaw,out ne,out nl,out fov);}
            else
            {
                NativeHumBossPreview.Camera(p,humBoss.HeadPos,humBoss.Root,humView.yaw,out ne,out nl,out fov);
                NativeHumArena.PushEye(NativeHumBossPreview.Room,humView.shiftMask,ref ne,nl);
            }
            eye=new Vector3(ne.X,ne.Y,ne.Z);look=new Vector3(nl.X,nl.Y,nl.Z);
            // The room's shudder on the directed camera.
            float sh=humStage.Shake;if(sh>.002f){eye+=new Vector3(Mathf.Sin(clock*37.1f)+.5f*Mathf.Sin(clock*71.3f+1.3f),Mathf.Sin(clock*43.7f+.7f)+.5f*Mathf.Sin(clock*83.9f+2.9f),Mathf.Sin(clock*29.3f))*(.035f*sh);}
            return true;
        }
        internal bool HumBossPreviewCard(out BossIntroCard card,out float age)
        {
            card=null;age=-1;if(!NativeHumBossPreview.On||double.IsNaN(NativeHumBossPreview.Start)||!humShown)return false;
            double since=NativeHumBossPreview.ShownClock-NativeHumBossPreview.Start;float p=(float)(since-Math.Floor(since/NativeHumBossPreview.Loop)*NativeHumBossPreview.Loop);float a=p-NativeHumBossPreview.SpawnAt;
            if(a<0||a>=NativeBackroomsBossIntro.Hold)return false;card=NativeHumBoss.Card;age=a;return true;
        }
        private void DisposeHumBoss()
        {
            humDraw?.Dispose();humDraw=null;humVfx?.Dispose();humVfx=null;humVfxFailed=false;humArena=null;humLast=-1;HumBodyDispose();HumRoomDispose();
        }
    }
    public sealed partial class ConversionGame
    {
        private void HumBossPreviewToggle(){NativeHumBossPreview.On=!NativeHumBossPreview.On;NativeHumBossPreview.Start=double.NaN;DevSay(NativeHumBossPreview.On?"Level 0 boss preview · on (THE HUM's intro, every move, both phase changes, the ceiling and its death; presentation only)":"Level 0 boss preview · off",false);}
        private float HumBossPreviewPanel(float x,float y,float w){DevToggleAct(new Rect(x,y,w,32),"preview-hum-boss",NativeHumBossPreview.On);return y+38;}
        private static readonly ExpeditionBossModel humBossBanner=new ExpeditionBossModel();
        private const string HumBossPreviewLabel="DEV PREVIEW · THE HUM · NOT GAMEPLAY";
        /// <summary>The preview's label and, after the intro, the boss's plate as the expedition HUD draws it.</summary>
        private void HumBossPreviewHud(float width,float height)
        {
            if(!NativeHumBossPreview.On||Event.current.type!=EventType.Repaint||arena==null||!NativeBackroomsGenerator.IsMap(arena.MapId))return;
            var v=NativeHumBossPreview.Shown;if(!v.live)return;double age=NativeHumBossPreview.ShownClock-v.spawned;if(age<NativeBackroomsBossIntro.Hold)return;
            var size=U.Measure(HumBossPreviewLabel,11,true,true,.12f);var r=new Rect(width/2-size.x/2-14,92,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,HumBossPreviewLabel,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            var m=humBossBanner;m.Name=NativeHumBoss.Name;m.Area="BOSS · LEVEL 0";m.PlateCount=1;m.PlateNames[0]=m.Name;m.PlateDown[0]=v.Dead((float)NativeHumBossPreview.ShownClock);m.PlateHealth[0]=v.maxHp>0?Mathf.Clamp01(v.hp/v.maxHp):-1;m.PlatePercent[0]=null;m.PlateAge[0]=age-NativeBackroomsBossIntro.Hold;
            NativeExpeditionStatesHud.BossPlates(m,width,128,Time.unscaledTime,false);
        }
    }
    public sealed partial class NativeAudio
    {
        // ---------- THE HUM's cues (Core NativeHumBossCues: the boss.hum.* keys) ----------
        private readonly HumBossCue[] humBossCues=new HumBossCue[NativeHumBossCues.QueueSize];private float humBossHurt=float.NegativeInfinity;
        private void HumBossCues(NativeArena arena,bool menu)
        {
            int n=NativeHumBossCues.Drain(humBossCues);if(menu||arena==null)return;
            for(int i=0;i<n;i++)
            {
                var c=humBossCues[i];if(c.Key==NativeHumBossCues.Hurt){if(c.At-humBossHurt<1.2f)continue;humBossHurt=c.At;}
                string key=NativeHumBossCues.Keys[c.Key];Vector3? place=NativeHumBossCues.Flat(c.Key)?(Vector3?)null:new Vector3(c.At3.X,c.At3.Y,c.At3.Z);
                Cue(key,null,place,c.Gain);
            }
        }
    }
}
