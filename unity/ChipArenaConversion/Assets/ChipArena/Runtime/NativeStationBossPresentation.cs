using System;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // THE 6:47, the Central Station's boss, in the game (docs/EXPEDITION-BOSS-CENTRAL-STATION.md "Presentation build"; owner
    // 2026-10-17: "Go with your suggestions"). Presentation only: it reads the boss's record (StationBossViewOf, the one binding
    // point) and the presentation clock, and draws; no hit, collider, timing, damage, cooldown, movement or network state is read
    // back or changed.
    //  - Model: Core NativeStationBossLook (the concept's skeleton and geometry, three tiers, with the presentation's joints) as one
    //    mesh per joint and tier, drawn with NativeSourceLit (the game's lit source material: emission, the hit flash, the station's
    //    practical lights through the actor-light path), each joint's light from the effects (the windows, the lamp, the Heart).
    //  - Motion: Core StationBossRig on the shared boss motion library (root follow, gait, actions on the record's hit, chains,
    //    second order, look-at, flinch, stagger, inertializer).
    //  - Effects: Core NativeStationBossFx: its telegraphs, beam and floor light through one NativeFxDraw (a solid and an effects
    //    batch), its particles (realism pass 2026-10-19: sparks, arcs, dust, steam, debris, glows) through the shared effects
    //    renderer (a NativeVfxDraw of its own, NativeBossVfxSubmit), up to three practical lights merged into the station's slots
    //    (NativeLighting.KitLights).
    //  - The boss song (docs/ZONE-BOSS-MUSIC.md) times the arrival: its accents are the beats and NativeArena.BossSongTime the
    //    clock through the intro hold. The song's stage owns the house lights' cut and flare and the board's LAST CALL; the boss
    //    adds its own parts on top: the headlight in the west mouth, the brake sparks, the burst through the gate (its effects)
    //    and, through Core NativeStationBossStage.Shown, row 0's ARRIVING and DEPARTED, the clocks' 6:48, T1's green signal and
    //    the barrier's and board's falls (the hook the station presentation reads; nothing in the station changes until it does).
    //  - Audio: the boss.station.* keys (Core NativeStationBossCues) queued here, played by NativeAudio's bank path (silent until the
    //    owner's clips exist).
    //  - DEV preview (DEV panel · Camera · "Station boss preview", on the station map): a scripted record (Core
    //    NativeStationBossPreview) plays the arrival, every move, both phase changes, the staggers, the breaks and the death, with
    //    the banner and plate, without the authority.
    // Normal play: StationBossViewOf returns None, so nothing is drawn and the placeholder Siren fights on. Allocation free per frame.
    public sealed partial class ArenaPresentation
    {
        // ================================================================================================================
        // The binding (Astra): the boss's replicated record (`pve.zoneBoss`, doc section 10 item 2) with the actor's root, yaw and
        // health and its arrival's `spawned`, in the Core StationBossView (times on the arena clock, NaN for the wire's -100). One
        // place, e.g.  internal static StationBossView StationBossViewOf(NativeArena a)=>a.Pve?.ZoneBoss?.View??StationBossView.None;
        // Until then every arena has none: nothing draws and the placeholder Siren fights as today.
        // ================================================================================================================
        internal static StationBossView StationBossViewOf(NativeArena arena)=>arena?.StationZoneBossView()??StationBossView.None;

        private readonly StationBossRig stationBoss=new StationBossRig();private readonly NativeStationBossFx stationBossFx=new NativeStationBossFx();
        private Mesh[][] stationBossMeshes;private int[][] stationBossJoints;private Material stationBossMaterial;private bool stationBossFailed;private NativeFxDraw stationBossDraw;
        private MaterialPropertyBlock stationBossBlock;private StationBossView stationBossView=StationBossView.None;private float stationBossLast=-1,stationBossHp=float.NaN;
        private NativeArena stationBossArena;private int stationBossGeneration=-1;private bool stationBossLights;private float stationBossFlash;
        private static readonly int GlowTintId=Shader.PropertyToID("_GlowTint"),AccentTintId=Shader.PropertyToID("_AccentTint"),ActorLitId=Shader.PropertyToID("_ArenaActorLit");
        /// <summary>The boss's draws last frame (budget lines), and its tier.</summary>
        public int StationBossDraws{get;private set;}public int StationBossTier{get;private set;}

        // SourceWorld (the arena or the killcam's replay arena), right after the station's own scenery.
        private void StationBossScenery(NativeArena arena)
        {
            var eye=WorldEye;
            if(!NativeExpeditionStation.Active(arena.MapId)){StationBossHide();return;}
            float now=PresentationClock(arena);
            if(!ReferenceEquals(arena,stationBossArena)||arena.Generation!=stationBossGeneration){stationBossArena=arena;stationBossGeneration=arena.Generation;stationBossLast=-1;stationBossHp=float.NaN;stationBossSongSpawn=float.NaN;stationBossSongLast=-1;}
            // The record: the DEV preview's script while it runs, else the authority's (None until bound).
            NVector velocity=NVector.Zero;bool hasTarget=false;NVector target=NVector.Zero;
            if(NativeStationBossPreview.On)
            {
                if(double.IsNaN(NativeStationBossPreview.Start))NativeStationBossPreview.Start=now;
                NativeStationBossPreview.Fill(now,ref stationBossView,out velocity,out var pt);hasTarget=true;target=new NVector(pt.X,1.6f,pt.Y);
                NativeStationBossPreview.Shown=stationBossView;NativeStationBossPreview.ShownClock=now;
            }
            else
            {
                stationBossView=StationBossViewOf(arena);
                if(stationBossView.live)
                {
                    // The root's velocity from the record (the follow smooths it; on a guest the arena clock already interpolates).
                    var pilot=TargetPilot(arena,stationBossView.actionTarget);if(pilot!=null){hasTarget=true;var p=pilot.Body.position;target=new NVector(p.x,p.y+1.2f,p.z);}
                }
            }
            if(!stationBossView.live||!EnsureStationBoss()){StationBossHide();return;}
            // The boss song binds the arrival (docs/ZONE-BOSS-MUSIC.md): its accents give the beats (the doc's table when its file
            // is missing), and its clock times them. The arena clock waits through the intro hold while NativeArena.BossSongTime
            // runs, so the boss's own clock trails the arena's by what is left of the hold and meets it at the hold's end; the
            // arrival starts at song time 0 on it. Contacts and every later time stay on the arena clock.
            NativeStationBossBeats.UseSong(NativeZoneBossSongs.ForMap(arena.MapId));
            if(!NativeStationBossPreview.On)
            {
                double song=arena.BossSongTime;
                if(song>=0&&song<1e6)
                {
                    float lag=Math.Max(0f,NativeStationBossBeats.Current.Hold-(float)song);now-=lag;
                    // Latched once per arrival, so the rig sees one `spawned` (a new song start restarts it).
                    if(float.IsNaN(stationBossSongSpawn)||song<stationBossSongLast)stationBossSongSpawn=now-(float)song;
                    stationBossSongLast=song;stationBossView.spawned=stationBossSongSpawn;
                }
                else{stationBossSongSpawn=float.NaN;stationBossSongLast=-1;}
            }
            float dt=stationBossLast<0?0:Mathf.Clamp(now-stationBossLast,0,.1f);
            if(!NativeStationBossPreview.On&&stationBossLast>=0&&dt>0)velocity=(new NVector(stationBossView.x,stationBossView.y,stationBossView.z)-stationBossPrev)/dt;
            stationBossPrev=new NVector(stationBossView.x,stationBossView.y,stationBossView.z);
            // Blows: the preview's scripted ones, or a drop in the record's health (from the nearest pilot), flinch the body and
            // flash it as the game flashes a struck pilot.
            if(NativeStationBossPreview.On){double since=now-NativeStationBossPreview.Start;float p=(float)(since-Math.Floor(since/NativeStationBossPreview.Loop)*NativeStationBossPreview.Loop),baseAt=(float)(now-p);
                int n=NativeStationBossPreview.Blows(p,baseAt,blowAt,blowPush,blowSize);for(int i=0;i<n;i++)if(!(blowAt[i]<=stationBossLastBlow))StationBossBlow(blowAt[i],blowPush[i],blowSize[i]);}
            else if(!float.IsNaN(stationBossHp)&&stationBossView.hp<stationBossHp-.5f&&stationBossView.maxHp>0)
            {
                var from=arena.Local?.Body!=null?arena.Local.Body.position:new Vector3(stationBossView.x,0,stationBossView.z-1);var push=new NVector(stationBossView.x-from.x,0,stationBossView.z-from.z);
                StationBossBlow(now,push,Mathf.Clamp((stationBossHp-stationBossView.hp)/stationBossView.maxHp*60,.25f,1.4f));
            }
            stationBossHp=stationBossView.hp;
            stationBoss.Update(now,dt,stationBossView,StationBossGround.Station,velocity,false,hasTarget,target);
            // The stage hook: what the station's board, clocks and lights should show (the station presentation reads it).
            NativeStationBossStage.Shown=NativeStationBossStage.Evaluate(stationBossView,now,NativeStationBossBeats.Current);NativeStationBossStage.ShownClock=now;
            StationBossDressing(NativeStationBossStage.Shown.BarrierFall,NativeStationBossPreview.On);
            // Effects, the joints' light, the practical lights.
            var fx=stationBossFx;fx.DrawGlass=NativeStationBossPreview.On;fx.GlassCount=NativeStationBossPreview.On?NativeStationBossPreview.Glass(stationBossView,fx.GlassXZ):LiveGlass(fx.GlassXZ);
            if(eye!=null){var t=eye.transform;Vector3 e=t.position,r=t.right,u=t.up;fx.Build(stationBoss,stationBossView,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(EffectsQuality),LowGraphics);}
            if(eye!=null&&fx.LightCount>0){NativeLighting.KitLights(fx.LightPos,fx.LightRgb,fx.LightCount,eye.transform.position,fx.LightBeam);stationBossLights=true;}
            else if(stationBossLights){NativeLighting.KitLights(null,null,0,Vector3.zero);stationBossLights=false;}
            DrawStationBoss(eye);DrawStationBossFx(eye,now);
            // The cues falling in this frame (played by NativeAudio's bank path).
            if(stationBossLast>=0)NativeStationBossCues.Queue(stationBossView,stationBoss,stationBossLast,now);
            stationBossLast=now;
        }
        private float stationBossSongSpawn=float.NaN;private double stationBossSongLast=-1;
        private NVector stationBossPrev;private float stationBossLastBlow=float.NegativeInfinity;
        private readonly float[] blowAt=new float[8],blowSize=new float[8];private readonly NVector[] blowPush=new NVector[8];
        private void StationBossBlow(float at,NVector push,float size){stationBoss.Hit(at,push,size);stationBossLastBlow=Math.Max(stationBossLastBlow,at);stationBossFlash=size;}
        private void StationBossHide()
        {
            if(stationBossLights){NativeLighting.KitLights(null,null,0,Vector3.zero);stationBossLights=false;}
            if(NativeStationBossStage.Shown.On)NativeStationBossStage.Shown=StationBossStage.Idle;stationBossLast=-1;StationBossDressing(0,false);
        }

        // ================================================================================================================
        // The station reacting to the boss (the hook as the station presentation draws it; presentation only, the colliders are
        // the authority's): the centre barrier thrown down in PLATFORM ALTERATION (its blocks through NativeArenaDressing.Posed,
        // their kit parts through StationBossPose), the departure board's fall in TERMINUS (its kit parts), the shudder on the
        // view. The DEV preview clears the concourse benches it walks through (drawn hidden; their colliders stay).
        // ================================================================================================================
        private NativeExpeditionStationKit stationBossPoseKit;private byte[] stationBossPoseOf=Array.Empty<byte>();
        private string stationBossBarrierW,stationBossBarrierE;private readonly string[] stationBossBenches=new string[8];private int stationBossBenchCount;private bool stationBossBenchesHidden;
        private float stationBossBoardAt=-1,stationBossBarrierAt=-1;private Matrix4x4 stationBossBoardM=Matrix4x4.identity,stationBossBarrierWM=Matrix4x4.identity,stationBossBarrierEM=Matrix4x4.identity;
        private const byte PoseNone=0,PoseBoard=1,PoseBarrierW=2,PoseBarrierE=3;
        // Which kit parts follow which fall: the board's (on the booth, inside the board's box) and the barrier segments' (their parent).
        private void StationBossPoseIndex()
        {
            if(stationKit==null||ReferenceEquals(stationKit,stationBossPoseKit))return;stationBossPoseKit=stationKit;var parts=stationKit.Parts;var blocks=stationMap?.blocks??Array.Empty<ContentBlock>();
            int w=-1,e=-1,booth=-1;stationBossBenchCount=0;
            for(int b=0;b<blocks.Length;b++)
            {
                var id=blocks[b]?.id;if(id==null)continue;
                if(id.EndsWith("barrier-1")||id.EndsWith("barrier-2")){if(blocks[b].pos.x<0)w=b;else e=b;}
                else if(id.EndsWith("-booth"))booth=b;
                else if((id.Contains("-benches-e")||id.Contains("-benches-w"))&&stationBossBenchCount<stationBossBenches.Length)stationBossBenches[stationBossBenchCount++]=id;
            }
            stationBossBarrierW=w>=0?blocks[w].id:null;stationBossBarrierE=e>=0?blocks[e].id:null;stationBossPoseOf=new byte[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                var p=parts[i];if(p.Parent==w){stationBossPoseOf[i]=PoseBarrierW;continue;}if(p.Parent==e){stationBossPoseOf[i]=PoseBarrierE;continue;}if(p.Parent!=booth)continue;
                var r=stationRestSpot[i];
                if(Math.Abs(r.x-NativeStationBossArena.BoardX)<NativeStationBossArena.BoardHalfX+.5f&&r.y>NativeStationBossArena.BoardY-NativeStationBossArena.BoardHalfY-.45f&&r.y<NativeStationBossArena.BoardY+NativeStationBossArena.BoardHalfY+.3f&&Math.Abs(r.z-NativeStationBossArena.BoardZ)<NativeStationBossArena.BoardHalfZ+.5f)stationBossPoseOf[i]=PoseBoard;
            }
        }
        private static Matrix4x4 PivotPose(NVector pivot,System.Numerics.Quaternion q,NVector move)
        {
            var pv=new Vector3(pivot.X,pivot.Y,pivot.Z);return Matrix4x4.TRS(pv+new Vector3(move.X,move.Y,move.Z),new Quaternion(q.X,q.Y,q.Z,q.W),Vector3.one)*Matrix4x4.Translate(-pv);
        }
        private Matrix4x4 BarrierMatrix(int side)
        {
            float fall=stationState.BarrierFall;
            if(!(fall==stationBossBarrierAt)){stationBossBarrierAt=fall;NativeStationBossStage.BarrierPose(-1,fall,out var pw,out var qw,out var mw);stationBossBarrierWM=PivotPose(pw,qw,mw);NativeStationBossStage.BarrierPose(1,fall,out var pe,out var qe,out var me);stationBossBarrierEM=PivotPose(pe,qe,me);}
            return side<0?stationBossBarrierWM:stationBossBarrierEM;
        }
        /// <summary>The station's view (StationScenery): the DEV preview's (the express for Mind the Gap, the boss's arrival) while it
        /// runs and the station's own preview does not.</summary>
        private static bool StationBossStageView(NativeStationView v,float now)=>NativeStationBossPreview.On&&NativeStationBossPreview.Station(v,now);
        /// <summary>The station's state (StationScenery, after the authority's and the song's): the boss's stage hook on top.</summary>
        private void StationBossStageApply()=>NativeStationBossStage.Apply(stationState,NativeStationBossStage.Shown);
        /// <summary>The station kit's draw (DrawStationKit): a part on the falling board or a thrown barrier segment drawn posed.</summary>
        private void StationBossFallPose(int i,ref Matrix4x4 m)
        {
            StationBossPoseIndex();if(i>=stationBossPoseOf.Length)return;byte k=stationBossPoseOf[i];if(k==PoseNone)return;
            if(k==PoseBoard){float fall=stationState.BoardFall;if(!(fall>0))return;if(!(fall==stationBossBoardAt)){stationBossBoardAt=fall;NativeStationBossStage.BoardPose(fall,out var q,out var mv);stationBossBoardM=PivotPose(NativeStationBossStage.BoardPivot,q,mv);}m=stationBossBoardM*m;return;}
            if(stationState.BarrierFall>0)m=BarrierMatrix(k==PoseBarrierW?-1:1)*m;
        }
        // The barrier's own blocks posed with it (NativeArenaDressing draws the blocks), and the preview's benches cleared.
        private void StationBossDressing(float barrierFall,bool preview)
        {
            StationBossPoseIndex();
            if(stationBossBarrierW!=null)
            {
                if(barrierFall>0){NativeStationBossStage.BarrierPose(-1,barrierFall,out var pw,out var qw,out var mw);NativeArenaDressing.Posed[stationBossBarrierW]=PivotPose(pw,qw,mw);}
                else if(NativeArenaDressing.Posed.Count>0)NativeArenaDressing.Posed.Remove(stationBossBarrierW);
            }
            if(stationBossBarrierE!=null)
            {
                if(barrierFall>0){NativeStationBossStage.BarrierPose(1,barrierFall,out var pe,out var qe,out var me);NativeArenaDressing.Posed[stationBossBarrierE]=PivotPose(pe,qe,me);}
                else if(NativeArenaDressing.Posed.Count>0)NativeArenaDressing.Posed.Remove(stationBossBarrierE);
            }
            // Re-asserted every preview frame (a new arena clears Hidden); taken back once when the preview ends.
            if(preview){for(int i=0;i<stationBossBenchCount;i++)NativeArenaDressing.Hidden.Add(stationBossBenches[i]);stationBossBenchesHidden=true;}
            else if(stationBossBenchesHidden){stationBossBenchesHidden=false;for(int i=0;i<stationBossBenchCount;i++)NativeArenaDressing.Hidden.Remove(stationBossBenches[i]);}
        }
        /// <summary>ConversionGame, with the horde's shake: the station's shudder on THE 6:47's impacts (the stage hook's Shake) on
        /// the live local view; `scale` the setting's (camera shake off: never called; reduced effects: half).</summary>
        public void StationBossShake(Transform view,float scale)
        {
            var st=NativeStationBossStage.Shown;if(view==null||!(scale>0)||!st.On||!(st.Shake>.002f))return;
            float t=(float)NativeStationBossStage.ShownClock,k=st.Shake*scale;
            var off=new Vector3(Mathf.Sin(t*37.1f)+.5f*Mathf.Sin(t*71.3f+1.3f),Mathf.Sin(t*43.7f+.7f)+.5f*Mathf.Sin(t*83.9f+2.9f),0)*(.03f*k);
            view.position+=view.rotation*off;view.rotation*=Quaternion.Euler(0,0,Mathf.Sin(t*29.3f+2.1f)*.45f*k);
        }
        private static NativePilot TargetPilot(NativeArena arena,int seat)
        {
            NativePilot best=null;
            foreach(var p in arena.Pilots){if(p?.Body==null||p.Hp<=0)continue;if(p.Seat!=null&&p.Seat.id==seat)return p;if(best==null)best=p;}
            return best;
        }
        // Alteration's glass in play: the station's zone frame's falling glass (drawn by the station), ringed by the boss's telegraph.
        private static int LiveGlass(float[] xz)
        {
            var v=NativeStationLive.Shown;if(v==null)return 0;int n=0;
            for(int i=0;i<v.GlassCount&&i<v.Glass.Length&&n*2<xz.Length;i++){xz[n*2]=v.Glass[i].X;xz[n*2+1]=v.Glass[i].Z;n++;}
            return n;
        }
        private bool EnsureStationBoss()
        {
            if(stationBossMaterial!=null)return true;if(stationBossFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new InvalidOperationException("NativeSourceLit shader is missing");
                stationBossMaterial=new Material(shader){name="THE 6:47",enableInstancing=true};stationBossMaterial.SetInt("_Cull",2);stationBossMaterial.SetFloat("_Ink",0);
                // The station's practical lights reach the boss as they reach the walls round it (NativeSourceLit's actor lights).
                stationBossMaterial.SetFloat(ActorLitId,1);
                stationBossMeshes=new Mesh[3][];stationBossJoints=new int[3][];
                for(int tier=0;tier<3;tier++)
                {
                    var built=NativeStationBossLook.Build(tier);var meshes=new Mesh[built.Sections.Count];var joints=new int[built.Sections.Count];
                    for(int i=0;i<built.Sections.Count;i++){var s=built.Sections[i];joints[i]=s.Bone;meshes[i]=StationBossMesh(s,"THE 6:47 / tier "+tier+" / "+NativeStationBossLook.Names[s.Bone]);}
                    stationBossMeshes[tier]=meshes;stationBossJoints[tier]=joints;
                }
                stationBossDraw=new NativeFxDraw();stationBossBlock=new MaterialPropertyBlock();
                var life=gameObject.GetComponent<NativeStationLifetime>();if(life==null)life=gameObject.AddComponent<NativeStationLifetime>();life.Gone+=DisposeStationBoss;return true;
            }
            catch(Exception e){Debug.LogWarning("THE 6:47 disabled: "+e.Message);DisposeStationBoss();stationBossFailed=true;return false;}
        }
        // A joint's geometry as a mesh (Core already in Unity's frame: no mirror, no winding turn), emission in uv1 as the bank's.
        private static Mesh StationBossMesh(WeaponSection s,string name)
        {
            int count=s.Positions.Count;if(count==0)return null;
            var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var uv=new Vector2[count];var emission=new System.Collections.Generic.List<Vector4>(count);
            for(int i=0;i<count;i++){var p=s.Positions[i];var n=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(n.X,n.Y,n.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
            var mesh=new Mesh{name=name,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.SetTriangles(s.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private void DrawStationBoss(Camera eye)
        {
            StationBossDraws=0;var pose=stationBoss.Shown;var fx=stationBossFx;
            // The tier by distance (full within 30 m, reduced to 60, low beyond; the low graphics tier never draws full).
            float d=eye!=null?Vector3.Distance(eye.transform.position,NativeBossPoseApply.ToUnity(stationBoss.Root)):0;int tier=d<30?0:d<60?1:2;if(LowGraphics)tier=Math.Max(1,tier);StationBossTier=tier;
            var meshes=stationBossMeshes[tier];var joints=stationBossJoints[tier];var block=stationBossBlock;
            float flash=stationBossFlash;stationBossFlash=Mathf.Max(0,stationBossFlash-Time.unscaledDeltaTime/.14f);float code=flash>0?1+Mathf.Clamp01(flash):1;
            for(int i=0;i<meshes.Length;i++)
            {
                var mesh=meshes[i];int j=joints[i];if(mesh==null||fx.Hidden[j])continue;
                var m=Matrix4x4.TRS(NativeBossPoseApply.ToUnity(pose.Position[j]),NativeBossPoseApply.ToUnity(pose.Rotation[j]),Vector3.one);
                float g=fx.Glow[j];block.Clear();block.SetVector(GlowTintId,new Vector4(g,g,g,0));block.SetVector(AccentTintId,new Vector4(1,1,1,code));
                bool small=j>=NativeStationBossModel.ChainFirst&&j!=NativeStationBossLook.BreachL&&j!=NativeStationBossLook.BreachR||NativeStationBossLook.GlowJoint(j);
                Graphics.DrawMesh(mesh,m,stationBossMaterial,0,null,0,block,small||tier==2?ShadowCastingMode.Off:ShadowCastingMode.On,true);StationBossDraws++;
            }
        }
        private void DrawStationBossFx(Camera camera,float time)
        {
            if(camera==null||stationBossDraw==null)return;var q=stationBossFx.Quads;stationBossDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?stationBossDraw.Solid:stationBossDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            stationBossDraw.Draw(camera,null,time);
            // The particles (sparks, arcs, dust, steam, debris, glows) through the shared effects renderer (its own instance).
            if(stationBossVfx==null&&!stationBossVfxFailed){try{stationBossVfx=new NativeVfxDraw();}catch(Exception e){stationBossVfxFailed=true;Debug.LogWarning("THE 6:47: effects renderer unavailable ("+e.Message+")");}}
            if(stationBossVfx!=null&&stationBossFx.Vfx.Count>0){stationBossVfx.Low=LowGraphics;stationBossVfx.Begin();NativeBossVfxSubmit.Submit(stationBossFx.Vfx,stationBossVfx);stationBossVfx.Submit(camera);StationBossDraws+=2;}
        }
        private NativeVfxDraw stationBossVfx;private bool stationBossVfxFailed;
        private void DisposeStationBoss()
        {
            if(stationBossMeshes!=null)foreach(var tier in stationBossMeshes)if(tier!=null)foreach(var m in tier)if(m!=null)CapturedAnimation.Release(m);
            if(stationBossMaterial!=null)CapturedAnimation.Release(stationBossMaterial);stationBossMeshes=null;stationBossJoints=null;stationBossMaterial=null;stationBossDraw?.Dispose();stationBossDraw=null;stationBossVfx?.Dispose();stationBossVfx=null;stationBossVfxFailed=false;
            stationBossArena=null;stationBossLast=-1;
        }
    }

    public sealed partial class ConversionGame
    {
        /// <summary>DEV panel · Camera: THE 6:47's preview on the station map (presentation only; nothing spawns, hurts or collides).</summary>
        private void StationBossPreviewToggle()
        {
            NativeStationBossPreview.On=!NativeStationBossPreview.On;NativeStationBossPreview.Start=double.NaN;
            DevSay(NativeStationBossPreview.On?"Station boss preview · on (THE 6:47's arrival, eight moves, phase changes, staggers, breaks and death on a 96 s loop; presentation only)":"Station boss preview · off",false);
        }
        private float StationBossPreviewPanel(float x,float y,float w)
        {
            DevToggleAct(new Rect(x,y,w,32),"preview-station-boss",NativeStationBossPreview.On);y+=38;
            return DevNote(x,y,w,"On the station map: THE 6:47 arrives out of the west tunnel and plays every move, both phase changes, its staggers, breaks and death on a 96 s loop. Presentation only: the placeholder Siren still fights, nothing hurts, collides or reaches the network until Astra's boss record exists.",C.Muted());
        }
        private static readonly ExpeditionBossModel stationBossBanner=new ExpeditionBossModel();
        private const string StationBossPreviewLabel="DEV PREVIEW · THE 6:47 · NOT GAMEPLAY";
        /// <summary>The preview's label, and the boss's banner and plate as the expedition HUD draws them for the zone boss.</summary>
        private void StationBossPreviewHud(float width,float height)
        {
            if(!NativeStationBossPreview.On||Event.current.type!=EventType.Repaint||arena==null||!NativeExpeditionStation.Active(arena.MapId))return;
            var size=U.Measure(StationBossPreviewLabel,11,true,true,.12f);var r=new Rect(width/2-size.x/2-14,92,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,StationBossPreviewLabel,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            var v=NativeStationBossPreview.Shown;if(!v.live)return;double age=NativeStationBossPreview.ShownClock-v.spawned;var m=stationBossBanner;
            m.Name=NativeExpeditionStates.BossName(NativeStationBoss.Archetype);m.Area=NativeStationBossPreview.BannerArea;
            m.PlateCount=1;m.PlateNames[0]=m.Name;m.PlateDown[0]=v.Dead((float)NativeStationBossPreview.ShownClock);m.PlateHealth[0]=v.maxHp>0?Mathf.Clamp01(v.hp/v.maxHp):-1;m.PlatePercent[0]=null;
            double banner=age-NativeZoneBossNames.BannerDelay(NativeStationBoss.Archetype);m.PlateAge[0]=banner;
            NativeExpeditionStatesHud.BossPlates(m,width,128,Time.unscaledTime,false);
            if(banner>=0&&banner<NativeExpeditionStates.IntroSeconds)NativeExpeditionStatesHud.BossIntro(m,width,height,(float)banner,false);
        }
    }

    public sealed partial class NativeAudio
    {
        // ---------- THE 6:47's cues (Core NativeStationBossCues: the doc's boss.station.* keys) ----------
        // Bank only: a key plays its clips when the owner's bank has them, else nothing (no stand-in). 2D keys play flat.
        private readonly StationBossCue[] stationBossCues=new StationBossCue[NativeStationBossCues.QueueSize];
        private float stationBossHurt=float.NegativeInfinity;
        private void StationBossCues(NativeArena arena,bool menu)
        {
            int n=NativeStationBossCues.Drain(stationBossCues);if(menu||arena==null)return;
            for(int i=0;i<n;i++)
            {
                var c=stationBossCues[i];if(c.Key==NativeStationBossCues.Hurt){if(c.At-stationBossHurt<1.2f)continue;stationBossHurt=c.At;}
                string key=NativeStationBossCues.Keys[c.Key];Vector3? place=NativeStationBossCues.Flat(c.Key)?(Vector3?)null:new Vector3(c.At3.X,c.At3.Y,c.At3.Z);
                Cue(key,null,place,c.Gain);
            }
        }
    }
}
