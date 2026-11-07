using System;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;

namespace ChipArena.UnityConversion
{
    // THE DEEP END in the game (docs/EXPEDITION-BOSS-BACKROOMS.md section 4). Presentation only: it reads the boss's record (DeepEndBossViewOf, the one binding point) and the presentation clock, and draws; no hit, collider,
    // timing, damage, cooldown, movement or network state is read back or changed. Normal play: the view is None, so nothing is drawn and the live Poolrooms looks as it does today.
    //  - Model: the bank's skinned body and rigid parts (Runtime NativeDeepEndBossBody.cs; a plain stand-in without the bank).
    //  - Motion: Core DeepEndBossRig (the head on a route, every joint on its trail). Effects: Core NativeDeepEndBossFx through Core NativeBossVfx and the water meshes. Room: Core NativeDeepEndBossStage through Runtime NativeDeepEndBossWater.cs.
    //  - DEV preview (DEV panel · Camera · "Poolrooms boss preview", on the Poolrooms map): a scripted record (Core NativeDeepEndBossPreview) plays the arrival, every move, both phase changes, a break and the death, on the room carved in place of the map's layout.
    public sealed partial class ArenaPresentation
    {
        // ================================================================================================================
        // The binding (Astra): the boss's replicated record, in the Core DeepEndBossView (times on the arena clock, NaN for the wire's -100). One place. Until then every arena has none.
        // ================================================================================================================
        internal static DeepEndBossView DeepEndBossViewOf(NativeArena arena)=>arena?.DeepEndZoneBossView()??DeepEndBossView.None;

        private readonly DeepEndBossRig deepEndRig=new DeepEndBossRig();private readonly NativeDeepEndBossFx deepEndFx=new NativeDeepEndBossFx();private DeepEndBossView deepEndView=DeepEndBossView.None;
        private float deepEndLast=float.NaN,deepEndHp=float.NaN,deepEndFlash,deepEndLastBlow=float.NegativeInfinity;private bool deepEndDrawnLast;private NVector deepEndOrigin;private NativeArena deepEndArena;private int deepEndGeneration=-1;
        private readonly NVector2[] deepEndPilots=new NVector2[8];private float deepEndHurt=float.NegativeInfinity;private float deepEndSongSpawn=float.NaN;private double deepEndSongLast=-1;private readonly float[] deepEndBlowAt=new float[8],deepEndBlowSize=new float[8];
        /// <summary>The rig the local tool's bosscam frames (public Root and Yaw).</summary>
        public DeepEndBossRig DeepEndBossRigShown=>deepEndRig;
        /// <summary>The boss's draws last frame (budget lines), and its tier.</summary>
        public int DeepEndBossDraws{get;private set;}public int DeepEndBossTier{get;private set;}

        // ---- the room in the map's place while the DEV preview runs (PoolroomsWorld asks) ----
        private PoolroomsLayout deepEndCarved,deepEndCarvedFrom;
        /// <summary>The layout the level draws: the carved room in the map's own place while the preview is on (collision doesn't matter: nothing plays), else the map's.</summary>
        private PoolroomsLayout DeepEndBossLayout(PoolroomsLayout live)
        {
            if(!NativeDeepEndBossPreview.On||live==null)return live;
            if(!ReferenceEquals(live,deepEndCarvedFrom)){deepEndCarvedFrom=live;NativeDeepEndBossPreview.CarveAt(live,out int cx,out int cz);deepEndCarved=NativeDeepEndArena.Carve(live,cx,cz);}
            return deepEndCarved;
        }
        /// <summary>Whether the boss's own water and lamps stand in for the level's sheet and discs this frame.</summary>
        private bool DeepEndBossRoomOn=>NativeDeepEndBossStage.Shown.On;

        // BackroomsBossScenery, right after PoolroomsWorld.
        private void DeepEndBossScenery(NativeArena arena)
        {
            if(arena==null||!NativePoolroomsGenerator.IsMap(arena.MapId)){DeepEndBossHide();return;}
            var eye=WorldEye;float wall=PresentationClock(arena);
            if(!ReferenceEquals(arena,deepEndArena)||arena.Generation!=deepEndGeneration){deepEndArena=arena;deepEndGeneration=arena.Generation;deepEndLast=float.NaN;deepEndHp=float.NaN;deepEndSongSpawn=float.NaN;deepEndSongLast=-1;deepEndRig.Clear();deepEndWaves?.Clear();}
            // The record: the DEV preview's script while it runs, else the authority's (None until bound). Everything below runs on `now`: the preview's own clock, or the presentation clock.
            bool preview=NativeDeepEndBossPreview.On;float now=wall;NVector2 target=default;bool hasLook=false;
            if(preview)
            {
                NativeDeepEndBossBeats.UseSong(NativeZoneBossSongs.Installed(NativeZoneMusic.PoolroomsBoss)?NativeZoneBossSongs.Music?.Song(NativeZoneMusic.PoolroomsBoss):null);deepEndFx.Pilots=NativeDeepEndBossPreview.Pilots;deepEndFx.PilotCount=NativeDeepEndBossPreview.Pilots.Length;
                if(double.IsNaN(NativeDeepEndBossPreview.Start))NativeDeepEndBossPreview.Start=wall;
                var localPilot=arena.Local;NativeDeepEndBossPreview.HasLocal=false;if(localPilot?.Body!=null&&poolroomsLayout!=null&&DeepEndBossCentre(out var roomAt)){var lp=localPilot.Body.position;var lr=new NVector2(lp.x-roomAt.X,lp.z-roomAt.Z);if(NativeDeepEndArena.InRoom(lr.X,lr.Y)){NativeDeepEndBossPreview.HasLocal=true;NativeDeepEndBossPreview.Local=lr;}}
                NativeDeepEndBossPreview.FillLampTimes();
                now=NativeDeepEndBossPreview.Clock(wall);NativeDeepEndBossPreview.View(now,ref deepEndView,out target);hasLook=true;
                NativeDeepEndBossPreview.Shown=deepEndView;NativeDeepEndBossPreview.ShownP=now;NativeDeepEndBossPreview.ShownClock=wall;
            }
            else
            {
                deepEndView=DeepEndBossViewOf(arena);
                if(deepEndView.live)
                {
                    // The effects' pilots, in the room's frame: the Jet bursts on whoever stands in its line, the Whistle's rings rise round them.
                    if(DeepEndBossCentre(out var pilotsAt)){int np=0;foreach(var q in arena.Pilots){if(q?.Body==null||q.Seat==null||q.Seat.AuthorityBoss||q.Hp<=0||np>=deepEndPilots.Length)continue;var qp=q.Body.position;deepEndPilots[np++]=new NVector2(qp.x-pilotsAt.X,qp.z-pilotsAt.Z);}deepEndFx.Pilots=deepEndPilots;deepEndFx.PilotCount=np;}
                    NativeDeepEndBossBeats.UseSong(NativeZoneBossSongs.ForMap(arena.MapId));double song=arena.BossSongTime;
                    if(song>=0&&song<1e6)
                    {
                        float lag=Math.Max(0f,NativeDeepEndBossBeats.Current.Hold-(float)song);now-=lag;
                        if(float.IsNaN(deepEndSongSpawn)||song<deepEndSongLast)deepEndSongSpawn=now-(float)song;
                        deepEndSongLast=song;deepEndView.spawned=deepEndSongSpawn;
                    }
                    else{deepEndSongSpawn=float.NaN;deepEndSongLast=-1;}
                }
            }
            if(!deepEndView.live||poolroomsLayout==null||eye==null||!DeepEndWaterReady()){DeepEndBossHide();return;}
            // The room: found by its abyss.
            if(!DeepEndBossCentre(out deepEndOrigin)){DeepEndBossHide();return;}
            float dt=float.IsNaN(deepEndLast)?0:Mathf.Clamp(now-deepEndLast,0,.1f);if(now<deepEndLast)dt=0;
            var stage=NativeDeepEndBossStage.Evaluate(deepEndView,now,NativeDeepEndBossBeats.Current);NativeDeepEndBossStage.Shown=stage;NativeDeepEndBossStage.ShownClock=now;
            // The record's blows flash the body.
            if(preview){int n=NativeDeepEndBossPreview.Blows(now,deepEndBlowAt,deepEndBlowSize);for(int i=0;i<n;i++)if(!(deepEndBlowAt[i]<=deepEndLastBlow)){deepEndLastBlow=Math.Max(deepEndLastBlow,deepEndBlowAt[i]);deepEndFlash=deepEndBlowSize[i];}if(now<deepEndLast)deepEndLastBlow=float.NegativeInfinity;}
            else if(!float.IsNaN(deepEndHp)&&deepEndView.hp<deepEndHp-.5f&&deepEndView.maxHp>0){deepEndFlash=Mathf.Clamp((deepEndHp-deepEndView.hp)/deepEndView.maxHp*60,.25f,1.4f);if(now-deepEndHurt>1.2f){deepEndHurt=now;NativeDeepEndBossCues.QueueOne(NativeDeepEndBossCues.Hurt,now,deepEndRig.MouthAt);}}
            deepEndHp=deepEndView.hp;
            // Live, the pilots in the room's frame (the Whistle's rings), and whom it's after.
            if(!preview)
            {
                int np=0;foreach(var p in arena.Pilots){if(p?.Body==null||p.Hp<=0||np>=deepEndPilots.Length)continue;var q=p.Body.position;deepEndPilots[np++]=new NVector2(q.x-deepEndOrigin.X,q.z-deepEndOrigin.Z);if(p.Seat!=null&&p.Seat.id==deepEndView.actionTarget){target=deepEndPilots[np-1];hasLook=true;}}
                if(np>0&&!hasLook){target=deepEndPilots[0];hasLook=true;}deepEndFx.Pilots=deepEndPilots;deepEndFx.PilotCount=np;
            }
            else{deepEndFx.Pilots=NativeDeepEndBossPreview.Pilots;deepEndFx.PilotCount=NativeDeepEndBossPreview.Pilots.Length;}
            var lookAt=new NVector(target.X,stage.Tide+1.2f,target.Y);
            deepEndRig.Update(now,dt,deepEndView,preview?NativeDeepEndBossPreview.Route:null,deepEndOrigin,stage.Tide,hasLook,lookAt);
            // The water: what the body and the moves did to it, its steps, the composed heights; then the effects read those.
            var waves=deepEndWaves;waves.Place();if(!float.IsNaN(deepEndLast)&&now>deepEndLast)deepEndFx.Couple(deepEndRig,deepEndView,waves,stage,deepEndLast,now);if(now<deepEndLast)waves.Clear();
            waves.Step(dt,stage.Tide>.02f);waves.Compose(stage);
            var t=eye.transform;var ep=t.position;var er=t.right;var eu=t.up;
            deepEndFx.Build(deepEndRig,deepEndView,stage,waves,now,new NVector(ep.x,ep.y,ep.z),new NVector(er.x,er.y,er.z),new NVector(eu.x,eu.y,eu.z),Mathf.Clamp01(EffectsQuality),LowGraphics,EffectsQuality<.5f);
            // Its attack warnings through the shared renderer (Runtime NativeTelegraphPresentation; docs/TELEGRAPHS.md).
            for(int i=0;i<deepEndFx.Telegraphs.Count;i++)Telegraph(deepEndFx.Telegraphs.Items[i]);
            DeepEndWaterDraw(eye,stage,now);DeepEndLightsDraw(eye,stage);
            DeepEndBossDraw(eye,now);DeepEndFxDraw(eye,stage,now);
            if(!float.IsNaN(deepEndLast)&&now>deepEndLast)NativeDeepEndBossCues.Queue(deepEndView,deepEndRig,deepEndLast,now);
            deepEndLast=now;deepEndDrawnLast=true;
        }
        private void DeepEndBossHide()
        {
            if(!deepEndDrawnLast)return;deepEndDrawnLast=false;NativeDeepEndBossStage.Shown=DeepEndBossStage.Idle;deepEndLast=float.NaN;DeepEndBodyHide();DeepEndWaterHide();
        }
        // The room's centre: the middle of the layout's abyss cells (once per layout).
        private PoolroomsLayout deepEndCentreOf;private bool deepEndCentreOk;private NVector deepEndCentre;
        private bool DeepEndBossCentre(out NVector centre)
        {
            var l=poolroomsLayout;
            if(!ReferenceEquals(l,deepEndCentreOf))
            {
                deepEndCentreOf=l;int n=0;float sx=0,sz=0;for(int z=0;z<l.Depth;z++)for(int x=0;x<l.Width;x++)if(l.Floor[l.Index(x,z)]==PoolroomsLayout.Abyss){sx+=x+.5f;sz+=z+.5f;n++;}
                deepEndCentreOk=n>0;if(n>0)deepEndCentre=new NVector(poolroomsOrigin.x+sx/n*PoolroomsLayout.Cell,poolroomsOrigin.y,poolroomsOrigin.z+sz/n*PoolroomsLayout.Cell);
            }
            centre=deepEndCentre;return deepEndCentreOk;
        }
        /// <summary>The preview's directed camera (NativeCinematicCamera): the intro's shots, then the slow orbit that reframes for each move.</summary>
        internal bool DeepEndBossPreviewShot(out Vector3 eye,out Vector3 look,out float fov)
        {
            eye=look=default;fov=60;if(!NativeDeepEndBossPreview.On||!deepEndDrawnLast)return false;
            var head=deepEndRig.Root-deepEndOrigin;var mid=deepEndRig.Shown.Position[9]-deepEndOrigin;
            var crown=deepEndRig.MouthDir;
            if(!NativeDeepEndBossPreview.Camera(NativeDeepEndBossPreview.ShownP,deepEndOrigin,head,mid,crown,out var e,out var l,out fov))return false;
            float sh=NativeDeepEndBossStage.Shown.Shake;if(sh>.002f&&EffectsQuality>=.5f){float k=(float)NativeDeepEndBossStage.ShownClock;e+=new NVector(Mathf.Sin(k*37.1f)+.5f*Mathf.Sin(k*71.3f),Mathf.Sin(k*43.7f+.7f),Mathf.Sin(k*53.3f+1.1f))*(.05f*sh);}
            eye=new Vector3(e.X,e.Y,e.Z);look=new Vector3(l.X,l.Y,l.Z);return true;
        }
        /// <summary>The card through the preview's arrival (BackroomsBossCardNow).</summary>
        internal bool DeepEndBossPreviewCard(out BossIntroCard card,out float age)
        {
            card=null;age=-1;if(!NativeDeepEndBossPreview.On||!deepEndDrawnLast)return false;float a=NativeDeepEndBossPreview.ShownP-NativeDeepEndBossPreview.SpawnAt;
            if(a<0||a>NativeBackroomsBossIntro.Hold)return false;card=NativeDeepEndBoss.Card;age=a;return true;
        }
    }
    public sealed partial class ConversionGame
    {
        private void DeepEndBossPreviewToggle(){NativeDeepEndBossPreview.On=!NativeDeepEndBossPreview.On;NativeDeepEndBossPreview.Start=double.NaN;DevSay(NativeDeepEndBossPreview.On?"Poolrooms boss preview · on (THE DEEP END's arrival, every move, its phase changes and death; presentation only)":"Poolrooms boss preview · off",false);}
        private float DeepEndBossPreviewPanel(float x,float y,float w){DevToggleAct(new Rect(x,y,w,32),"preview-deepend-boss",NativeDeepEndBossPreview.On);y+=38;return DevNote(x,y,w,"On the Poolrooms map: THE DEEP END's arrival with its card, every move, its phase changes and death, on the carved room in the map's place. Presentation only: nothing hurts, collides or reaches the network until Astra's record exists.",C.Muted());}
        private static readonly ExpeditionBossModel deepEndBanner=new ExpeditionBossModel();private const string DeepEndPreviewLabel="DEV PREVIEW · THE DEEP END · NOT GAMEPLAY";
        private void DeepEndBossPreviewHud(float width,float height)
        {
            if(!NativeDeepEndBossPreview.On||Event.current.type!=EventType.Repaint||arena==null||!NativePoolroomsGenerator.IsMap(arena.MapId))return;var v=NativeDeepEndBossPreview.Shown;if(!v.live)return;
            float p=NativeDeepEndBossPreview.ShownP;if(p<NativeDeepEndBossPreview.ArrivalEnd)return;
            var size=U.Measure(DeepEndPreviewLabel,11,true,true,.12f);var r=new Rect(width/2-size.x/2-14,92,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,DeepEndPreviewLabel,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            var m=deepEndBanner;m.Name=NativeDeepEndBoss.Name;m.Area="BOSS · THE POOLROOMS";m.PlateCount=1;m.PlateNames[0]=m.Name;m.PlateDown[0]=v.Dead(p);m.PlateHealth[0]=v.maxHp>0?Mathf.Clamp01(v.hp/v.maxHp):-1;m.PlatePercent[0]=null;m.PlateAge[0]=p-NativeDeepEndBossPreview.ArrivalEnd;
            NativeExpeditionStatesHud.BossPlates(m,width,128,Time.unscaledTime,false);
        }
    }
    public sealed partial class NativeAudio
    {
        // ---------- THE DEEP END's cues (Core NativeDeepEndBossCues: the doc's boss.deepend.* keys). Bank only: a key plays its clips when the owner's bank has them, else nothing. ----------
        private readonly DeepEndBossCue[] deepEndCues=new DeepEndBossCue[NativeDeepEndBossCues.QueueSize];
        private void DeepEndBossCues(NativeArena arena,bool menu)
        {
            int n=NativeDeepEndBossCues.Drain(deepEndCues);if(menu||arena==null)return;
            for(int i=0;i<n;i++){var c=deepEndCues[i];string key=NativeDeepEndBossCues.Keys[c.Key];Vector3? place=NativeDeepEndBossCues.Flat(c.Key)?(Vector3?)null:new Vector3(c.Place.X,c.Place.Y,c.Place.Z);Cue(key,null,place,c.Gain);}
        }
    }
}
