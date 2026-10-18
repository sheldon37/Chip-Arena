using System;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
using NVector=System.Numerics.Vector3;
using NQuat=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // THE CONVERTER, the City Ironworks' boss, in the game (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md "Presentation build"; owner
    // 2026-10-17: "Go with your suggestions with the convertor too"). Presentation only: it reads the boss's record
    // (IronworksBossViewOf, the one binding point), the boss song clock and the victory hold, and draws; no hit, collider, timing,
    // damage, cooldown, movement or network state is read back or changed.
    //  - Model: Core NativeIronworksBossLook (the concept's skeleton and geometry at three tiers with the presentation's joints) as
    //    one mesh per joint and tier, drawn with NativeSourceLit (the game's lit source material: emission, the hit flash, the
    //    Ironworks' practical lights and light field through the actor-light path); each joint's emission from the rig (the mouth,
    //    the tuyères, the seams, the lamps, the visor, the crust's cracks). The chains' links, hooks and shackles instanced.
    //  - Motion: Core IronworksBossRig on the shared boss motion library.
    //  - Molten metal (the spill strip, the pour, the pools, the lumps, the pit's cracks): Core NativeIronworksBossFx's surfaces
    //    through the Ironworks' own NativeMolten material and cube (NativeIronworksPresentation's); its effects through one
    //    NativeFxDraw; its heat haze through the Ironworks' haze material; up to three lights merged into the farthest warm slots of
    //    the Ironworks' eight (the two cool mercury slots are never taken).
    //  - The stage: Core NativeIronworksBossStage on top of the music's choreography (the foundry stops in the death), read by
    //    NativeIronworksPresentation through IronworksBossMusic (one call); the music ducks under the blow's roar and the call's
    //    bellow and fades before the shift whistle (NativeIronworksBossAudio.cs: on the hazard kit's music level, this frame only).
    //  - Audio: the boss.ironworks.* keys (Core NativeIronworksBossCues, queued here) through NativeAudio's bank path (silent until
    //    the owner's clips exist), the five loops on bank voices (NativeIronworksBossAudio.cs).
    //  - DEV preview (DEV panel · Camera · "Ironworks boss preview", on the Ironworks map): a scripted record (Core
    //    NativeIronworksBossPreview) plays the arrival, every action, both phase changes, the moments and the death, with the banner
    //    and plate, without the authority; the hall's song choreography runs on the preview's clock (silent).
    // Normal play: IronworksBossViewOf returns None, so nothing is drawn and the placeholder Siren fights on. Allocation free per frame.
    public sealed partial class ArenaPresentation
    {
        // ================================================================================================================
        // The binding (Astra): the boss's replicated record (`pve.zoneBoss`, doc section 10.2 item 3) with the actor's root, yaw and
        // health and its arrival's `spawned`, in the Core IronworksBossView (times on the arena clock, NaN for the wire's -100). One
        // place, e.g.  internal static IronworksBossView IronworksBossViewOf(NativeArena a)=>a.Pve?.ZoneBoss?.Ironworks??IronworksBossView.None;
        // Until then every arena has none: nothing draws and the placeholder Siren fights as today.
        // ================================================================================================================
        internal static IronworksBossView IronworksBossViewOf(NativeArena arena)=>arena?.IronworksZoneBossView()??IronworksBossView.None;
        /// <summary>The clocks the arrival and the death play on: the shared boss song clock (it runs through the intro hold, when the
        /// arena clock stands still) and the death's age (the record's deathAt on the arena clock plus the victory hold's elapsed).</summary>
        internal static void IronworksBossClocks(NativeArena arena,in IronworksBossView v,float now,out double arrival,out double death)
        {
            double song=arena.BossSongTime;arrival=song>=0?song+(now-arena.Time):double.NaN;
            if(!v.Dead(now)){death=double.NaN;return;}var pve=arena.Pve;
            double held=pve!=null&&pve.Holding&&pve.Cinematic=="victory"?pve.HoldDuration-pve.HoldLeft:pve?.Outcome=="victory"?4:0;death=now-v.deathAt+held;
        }

        private readonly IronworksBossRig ironworksBoss=new IronworksBossRig();private readonly NativeIronworksBossFx ironworksBossFx=new NativeIronworksBossFx();
        private Mesh[][] iwbMeshes;private int[][] iwbJoints;private Mesh iwbLink,iwbHook,iwbShackle;private Material iwbMaterial;private bool iwbFailed;private NativeFxDraw iwbDraw;private MaterialPropertyBlock iwbBlock;
        private IronworksBossView iwbView=IronworksBossView.None;private float iwbLast=-1,iwbHp=float.NaN,iwbFlash;private NativeArena iwbArena;private int iwbGeneration=-1;
        private IronworksBossClock iwbArrivalClock,iwbDeathClock;private double iwbArrivalLast=double.NaN,iwbDeathLast=double.NaN;private NVector iwbPrev;private float iwbLastBlow=float.NegativeInfinity;
        private readonly float[] iwbBlowAt=new float[8],iwbBlowSize=new float[8];private readonly NVector[] iwbBlowPush=new NVector[8];
        private readonly Matrix4x4[] iwbLinks=new Matrix4x4[160];private readonly Matrix4x4[] iwbEnds=new Matrix4x4[8];private int iwbLinkCount;
        private IronMoltenBatch iwbMelt;private bool iwbLights;
        private readonly float[] iwbMergePos=new float[NativeMapLooks.MaxLights*4],iwbMergeRgb=new float[NativeMapLooks.MaxLights*3],iwbMergeBeam=new float[NativeMapLooks.MaxLights];
        private Mesh iwbHazeMesh;private readonly System.Collections.Generic.List<Vector3> iwbHazeV=new System.Collections.Generic.List<Vector3>(NativeIronworksBossFx.HeatMax*4);
        private readonly System.Collections.Generic.List<Color> iwbHazeC=new System.Collections.Generic.List<Color>(NativeIronworksBossFx.HeatMax*4);private readonly System.Collections.Generic.List<Vector2> iwbHazeUv=new System.Collections.Generic.List<Vector2>(NativeIronworksBossFx.HeatMax*4);
        private double iwbPreviewSong=double.NaN;
        private static readonly int IwbGlowId=Shader.PropertyToID("_GlowTint"),IwbAccentId=Shader.PropertyToID("_AccentTint"),IwbActorLitId=Shader.PropertyToID("_ArenaActorLit");
        /// <summary>The boss's draws last frame and its tier (the budget lines).</summary>
        public int IronworksBossDraws{get;private set;}public int IronworksBossTier{get;private set;}

        // SourceWorld (the arena or the killcam's replay arena), right after the Ironworks' own scenery.
        private void IronworksBossScenery(NativeArena arena)
        {
            var eye=WorldEye;if(!NativeIronworksKit.Active(arena.MapId)){IronworksBossHide();return;}
            float now=PresentationClock(arena),frame=Mathf.Clamp(Time.unscaledDeltaTime,0,.1f);
            if(!ReferenceEquals(arena,iwbArena)||arena.Generation!=iwbGeneration){iwbArena=arena;iwbGeneration=arena.Generation;iwbLast=-1;iwbHp=float.NaN;iwbArrivalClock.Reset();iwbDeathClock.Reset();}
            // The record: the DEV preview's script while it runs, else the authority's (None until bound).
            NVector velocity=NVector.Zero,target=NVector.Zero;bool hasTarget=false;double arrival,death;
            if(NativeIronworksBossPreview.On)
            {
                if(double.IsNaN(NativeIronworksBossPreview.Start))NativeIronworksBossPreview.Start=now;
                NativeIronworksBossPreview.Fill(now,ref iwbView,out velocity,out var pt,out arrival,out death);hasTarget=true;target=new NVector(pt.X,1.6f,pt.Y);
                NativeIronworksBossPreview.Shown=iwbView;NativeIronworksBossPreview.ShownClock=now;iwbPreviewSong=arrival;
            }
            else
            {
                iwbView=IronworksBossViewOf(arena);iwbPreviewSong=double.NaN;IronworksBossClocks(arena,iwbView,now,out arrival,out death);
                if(iwbView.live){var pilot=IronworksBossTarget(arena,iwbView.targetSeat);if(pilot!=null){hasTarget=true;var p=pilot.Body.position;target=new NVector(p.x,p.y+1.2f,p.z);}}
            }
            if(!iwbView.live||!EnsureIronworksBoss()){IronworksBossHide();return;}
            // The clocks smoothed onto the frame's own time (they step as the host's hold ticks arrive), never backwards.
            arrival=iwbArrivalClock.Step(arrival,frame);death=iwbDeathClock.Step(death,frame);
            float dt=iwbLast<0?0:Mathf.Clamp(now-iwbLast,0,.1f);
            if(!NativeIronworksBossPreview.On&&iwbLast>=0&&dt>0)velocity=(new NVector(iwbView.x,iwbView.y,iwbView.z)-iwbPrev)/dt;
            iwbPrev=new NVector(iwbView.x,iwbView.y,iwbView.z);
            // Blows: the preview's, or a drop in the record's health (from the local pilot), flinch the body and flash it.
            if(NativeIronworksBossPreview.On)
            {
                double since=now-NativeIronworksBossPreview.Start;float p=(float)(since-Math.Floor(since/NativeIronworksBossPreview.Loop)*NativeIronworksBossPreview.Loop),baseAt=(float)(now-p);
                int n=NativeIronworksBossPreview.Blows(p,baseAt,iwbBlowAt,iwbBlowPush,iwbBlowSize);for(int i=0;i<n;i++)if(!(iwbBlowAt[i]<=iwbLastBlow))IronworksBossBlow(iwbBlowAt[i],iwbBlowPush[i],iwbBlowSize[i]);
            }
            else if(!float.IsNaN(iwbHp)&&iwbView.hp<iwbHp-.5f&&iwbView.maxHp>0)
            {
                var from=arena.Local?.Body!=null?arena.Local.Body.position:new Vector3(iwbView.x,0,iwbView.z-1);var push=new NVector(iwbView.x-from.x,0,iwbView.z-from.z);
                IronworksBossBlow(now,push,Mathf.Clamp((iwbHp-iwbView.hp)/iwbView.maxHp*60,.25f,1.4f));
            }
            iwbHp=iwbView.hp;
            var rig=ironworksBoss;rig.Update(now,frame,iwbView,IronworksBossGround.Hall,velocity,false,hasTarget,target,arrival,death);
            // The stage hook (read by the Ironworks presentation next frame), the effects, the lights, the draws.
            var stage=NativeIronworksBossStage.Evaluate(iwbView,now,rig.DeathAge,rig);NativeIronworksBossStage.Shown=stage;
            var fx=ironworksBossFx;
            if(eye!=null){var t=eye.transform;Vector3 e=t.position,r=t.right,u=t.up;fx.Build(rig,iwbView,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(EffectsQuality),LowGraphics);}
            IronworksBossLightsMerge(eye);
            DrawIronworksBoss(eye);DrawIronworksBossLinks();DrawIronworksBossMolten();DrawIronworksBossFx(eye,now);DrawIronworksBossHaze(eye);
            // Sound (NativeAudio plays it next frame): the cues falling this frame (the presentation clock, the song clock, the death's
            // clock) queued, the loops' levels, the music's duck.
            if(iwbLast>=0)
            {
                float af=(float)(double.IsNaN(iwbArrivalLast)?-1:iwbArrivalLast),at=(float)(double.IsNaN(arrival)?-1:arrival),df=(float)(double.IsNaN(iwbDeathLast)?-1:iwbDeathLast),de=(float)(double.IsNaN(death)?-1:death);
                NativeIronworksBossCues.Queue(iwbView,rig,fx,iwbLast,now,af,at,df,de);
            }
            NativeIronworksBossCues.Loops(iwbView,rig,now,NativeIronworksBossCues.LoopLevel);var mo=rig.Mouth;var ro=rig.Root;var la=NativeIronworksBossCues.LoopAt;
            la[0]=la[1]=la[4]=mo;la[2]=la[3]=new NVector(ro.X,1,ro.Z);NativeIronworksBossCues.MusicDuck=stage.MusicDuck;NativeIronworksBossCues.Frame=Time.frameCount;
            iwbArrivalLast=arrival;iwbDeathLast=death;iwbLast=now;
        }
        // The pilot a record aims at (its seat), else the nearest living pilot to look at.
        private static NativePilot IronworksBossTarget(NativeArena arena,int seat)
        {
            NativePilot best=null;
            foreach(var p in arena.Pilots){if(p?.Body==null||p.Hp<=0)continue;if(p.Seat!=null&&p.Seat.id==seat)return p;if(best==null)best=p;}
            return best;
        }
        private void IronworksBossBlow(float at,NVector push,float size){ironworksBoss.Hit(at,push,size);iwbLastBlow=Math.Max(iwbLastBlow,at);iwbFlash=size;}
        private void IronworksBossHide()
        {
            if(iwbLights)iwbLights=false;if(NativeIronworksBossStage.Shown.On)NativeIronworksBossStage.Shown=IronworksBossStage.Idle;iwbLast=-1;iwbPreviewSong=double.NaN;iwbArrivalLast=iwbDeathLast=double.NaN;
        }
        /// <summary>The song's beat for the Ironworks' stage with the boss on top (NativeIronworksPresentation's hook): the boss's stage
        /// (the foundry stopping in the death) and, while the DEV preview runs, Hydraulic Hell's choreography on the preview's arrival
        /// clock (the hall performs its intro and fight to the preview, silently: the music plays only for a real boss).</summary>
        private NativeZoneBossBeat IronworksBossMusic(NativeZoneBossBeat music)
        {
            bool changed=false;
            if(!music.On&&NativeIronworksBossPreview.On&&!double.IsNaN(iwbPreviewSong)&&iwbPreviewSong>=0)
            {var song=NativeZoneBossSongs.Music?.Song(NativeZoneMusic.IronworksBoss);if(song!=null){music=song.Evaluate(iwbPreviewSong+Mathf.Clamp(Time.unscaledDeltaTime,0,.1f),KitFlash);changed=true;}}
            var stage=NativeIronworksBossStage.Shown;if(stage.On&&music.On){music=NativeIronworksBossStage.Apply(music,stage);changed=true;}
            // The house lights follow the boss's stage (the death's foundry running down) only while it shows: untouched otherwise.
            if(changed)NativeLighting.ZoneHouse(music.House());
            return music;
        }
        // The boss's lights in the farthest warm slots of the Ironworks' eight (the two cool mercury slots stay), re-sent this frame.
        private void IronworksBossLightsMerge(Camera eye)
        {
            var fx=ironworksBossFx;var lights=iwLights;if(eye==null||lights==null||fx.LightCount==0)return;
            Array.Copy(lights.Pos,iwbMergePos,iwbMergePos.Length);Array.Copy(lights.Rgb,iwbMergeRgb,iwbMergeRgb.Length);Array.Copy(lights.Beam,iwbMergeBeam,iwbMergeBeam.Length);
            var e=eye.transform.position;int slots=Math.Min(NativeMapLooks.MaxLights,NativeIronworksLights.Slots);
            for(int k=0;k<fx.LightCount;k++)
            {
                int best=-1;float far=-1;
                for(int s=0;s<slots;s++)
                {
                    int c=lights.Slot[s];if(c>=0&&lights.Candidates[c].Cool)continue;bool taken=false;for(int q=0;q<k;q++)if(iwbTaken[q]==s){taken=true;break;}if(taken)continue;
                    float d=c<0?1e9f:new Vector3(iwbMergePos[s*4]-e.x,iwbMergePos[s*4+1]-e.y,iwbMergePos[s*4+2]-e.z).sqrMagnitude;if(d>far){far=d;best=s;}
                }
                if(best<0)break;iwbTaken[k]=best;
                for(int i=0;i<4;i++)iwbMergePos[best*4+i]=fx.LightPos[k*4+i];for(int i=0;i<3;i++)iwbMergeRgb[best*3+i]=fx.LightRgb[k*3+i];iwbMergeBeam[best]=fx.LightBeam[k];
            }
            NativeLighting.PickedLights(iwbMergePos,iwbMergeRgb,iwbMergeBeam);iwbLights=true;
        }
        private readonly int[] iwbTaken=new int[NativeIronworksBossFx.LightMax];
        private bool EnsureIronworksBoss()
        {
            if(iwbMaterial!=null)return true;if(iwbFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new InvalidOperationException("NativeSourceLit shader is missing");
                iwbMaterial=new Material(shader){name="THE CONVERTER",enableInstancing=true};iwbMaterial.SetInt("_Cull",2);iwbMaterial.SetFloat("_Ink",0);
                // The hall's practical lights and light field reach the boss as they reach the walls round it (NativeSourceLit's actor lights).
                iwbMaterial.SetFloat(IwbActorLitId,1);
                // The arrival's beats from Hydraulic Hell's map (the same map NativeAudio plays the intro edit from); the fallback otherwise.
                if(!NativeIronworksBossBeats.Bound){var song=NativeZoneBossSongs.Music?.Song(NativeZoneMusic.IronworksBoss);if(song!=null&&!NativeIronworksBossBeats.UseSong(song))Debug.LogWarning("Ironworks boss song map has no intro slams; the arrival plays on its fallback beats");}
                iwbMeshes=new Mesh[3][];iwbJoints=new int[3][];
                for(int tier=0;tier<3;tier++)
                {
                    var built=NativeIronworksBossLook.Build(tier);var body=built.Body;var meshes=new Mesh[body.Sections.Count];var joints=new int[body.Sections.Count];
                    for(int i=0;i<body.Sections.Count;i++){var s=body.Sections[i];joints[i]=s.Bone;meshes[i]=IronworksBossMesh(s,"THE CONVERTER / tier "+tier+" / "+NativeIronworksBossLook.Names[s.Bone]);}
                    iwbMeshes[tier]=meshes;iwbJoints[tier]=joints;
                    if(tier==1){iwbLink=IronworksBossMesh(built.Link.Sections[0],"THE CONVERTER / link");iwbHook=IronworksBossMesh(built.Hook.Sections[0],"THE CONVERTER / hook");iwbShackle=IronworksBossMesh(built.Shackle.Sections[0],"THE CONVERTER / shackle");}
                }
                iwbDraw=new NativeFxDraw();iwbBlock=new MaterialPropertyBlock();
                var life=gameObject.GetComponent<NativeIronworksBossLifetime>();if(life==null)life=gameObject.AddComponent<NativeIronworksBossLifetime>();life.Gone=DisposeIronworksBoss;return true;
            }
            catch(Exception e){Debug.LogWarning("THE CONVERTER disabled: "+e.Message);DisposeIronworksBoss();iwbFailed=true;return false;}
        }
        // A joint's geometry as a mesh (Core already in Unity's frame: no mirror, no winding turn), emission in uv1 as the bank's.
        private static Mesh IronworksBossMesh(WeaponSection s,string name)
        {
            int count=s.Positions.Count;if(count==0)return null;
            var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var uv=new Vector2[count];var emission=new System.Collections.Generic.List<Vector4>(count);
            for(int i=0;i<count;i++){var p=s.Positions[i];var n=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(n.X,n.Y,n.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
            var mesh=new Mesh{name=name,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.SetTriangles(s.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private void DrawIronworksBoss(Camera eye)
        {
            IronworksBossDraws=0;var rig=ironworksBoss;var pose=rig.Shown;
            // The tier by distance (full within 30 m, reduced to 60, low beyond; the low graphics tier never draws full).
            float d=eye!=null?Vector3.Distance(eye.transform.position,NativeBossPoseApply.ToUnity(rig.Root)):0;int tier=d<30?0:d<60?1:2;if(LowGraphics)tier=Math.Max(1,tier);IronworksBossTier=tier;
            var meshes=iwbMeshes[tier];var joints=iwbJoints[tier];var block=iwbBlock;
            float flash=iwbFlash;iwbFlash=Mathf.Max(0,iwbFlash-Time.unscaledDeltaTime/.14f);float code=flash>0?1+Mathf.Clamp01(flash):1;
            for(int i=0;i<meshes.Length;i++)
            {
                var mesh=meshes[i];int j=joints[i];if(mesh==null||rig.Hidden[j])continue;
                var m=Matrix4x4.TRS(NativeBossPoseApply.ToUnity(pose.Position[j]),NativeBossPoseApply.ToUnity(pose.Rotation[j]),Vector3.one);
                float g=rig.Glow[j];block.Clear();block.SetVector(IwbGlowId,new Vector4(g,g,g,0));block.SetVector(IwbAccentId,new Vector4(1,1,1,code));
                bool small=j>=NativeIronworksBossLook.ChunkBase||j==NativeIronworksBossModel.Lever||j==NativeIronworksBossModel.VesselHeat||j==NativeIronworksBossModel.Seams||j==NativeIronworksBossModel.WindBox;
                Graphics.DrawMesh(mesh,m,iwbMaterial,0,null,0,block,small||tier==2?ShadowCastingMode.Off:ShadowCastingMode.On,true);IronworksBossDraws++;
            }
        }
        // The yoke chains' links (alternate links turned a quarter; every third, stretched, at low detail), their hook and shackle ends, the levers' pull chains, the belt chain
        // and the torn pulpit's two chains, instanced.
        private void DrawIronworksBossLinks()
        {
            var rig=ironworksBoss;iwbLinkCount=0;int ends=0;bool low=IronworksBossTier==2;
            for(int c=0;c<NativeIronworksBossModel.Chains;c++)
            {
                var ch=rig.Chain(c);var pts=ch.Point;
                for(int i=0;i+1<pts.Length;i++){if(low&&i%3!=0)continue;Link(pts[i],pts[i+1],i,low?3:1);}
                var last=pts[pts.Length-1];var prev=pts[pts.Length-2];var down=last-prev;if(down.LengthSquared()<1e-8f)down=new NVector(0,-1,0);
                if(ends<iwbEnds.Length)iwbEnds[ends++]=EndMatrix(last,down,NativeIronworksBossModel.ChainEnd[c]);
            }
            if(!low)
            {
                foreach(var ch in rig.LeverChains)for(int i=0;i+1<ch.Point.Length;i++)Link(ch.Point[i],ch.Point[i+1],i,.6f);
                if(rig.PulpitTear>.25f&&float.IsNaN(rig.DeathAge)||rig.PulpitTear>.25f)for(int i=0;i+1<rig.Belt.Point.Length;i++)Link(rig.Belt.Point[i],rig.Belt.Point[i+1],i,2.1f);
            }
            if(rig.PulpitTear>.02f)for(int s=0;s<2;s++)
            {
                // A sagging chain between the arch's top and the pulpit's rail (a catenary-ish parabola under its slack).
                var a=rig.TearTop[s];var b=rig.TearEnd[s];float len=NVector.Distance(a,b),slack=Math.Max(0,1.9f-len);int n=IronworksBossRig.TearLinks(IronworksBossTier);NVector last=a;
                for(int i=1;i<=n;i++){float u=i/(float)n;var p=NVector.Lerp(a,b,u)+new NVector(0,-slack*1.6f*u*(1-u),0);Link(last,p,i,len/n/ .13f);last=p;}
            }
            if(iwbLinkCount>0&&iwbLink!=null){if(UsesInstancing){Graphics.DrawMeshInstanced(iwbLink,0,iwbMaterial,iwbLinks,iwbLinkCount,null,ShadowCastingMode.Off,true,0,null);IronworksBossDraws++;}
                else for(int i=0;i<iwbLinkCount;i++){Graphics.DrawMesh(iwbLink,iwbLinks[i],iwbMaterial,0,null,0,null,ShadowCastingMode.Off,true);IronworksBossDraws++;}}
            for(int i=0;i<ends;i++){var mesh=NativeIronworksBossModel.ChainEnd[i]==0?iwbHook:iwbShackle;if(mesh!=null){Graphics.DrawMesh(mesh,iwbEnds[i],iwbMaterial,0,null,0,null,ShadowCastingMode.Off,true);IronworksBossDraws++;}}
        }
        // One link from a to b: the link prop (along +z, 0.15 m) at the segment's middle, alternate links a quarter turn, stretched by `scale`.
        private void Link(NVector a,NVector b,int i,float scale)
        {
            if(iwbLinkCount>=iwbLinks.Length)return;var d=b-a;float l=d.Length();if(l<1e-5f)return;var f=new Vector3(d.X,d.Y,d.Z)/l;var up=Mathf.Abs(f.y)>.95f?Vector3.forward:Vector3.up;
            var r=Quaternion.LookRotation(f,up);if((i&1)==1)r*=Quaternion.Euler(0,0,90);var mid=(a+b)*.5f;
            iwbLinks[iwbLinkCount++]=Matrix4x4.TRS(new Vector3(mid.X,mid.Y,mid.Z),r,new Vector3(Mathf.Min(1,scale),Mathf.Min(1,scale),scale));
        }
        private static Matrix4x4 EndMatrix(NVector at,NVector down,byte kind)
        {
            var d=new Vector3(down.X,down.Y,down.Z).normalized;var r=Quaternion.FromToRotation(Vector3.down,d);return Matrix4x4.TRS(new Vector3(at.X,at.Y,at.Z),r,Vector3.one);
        }
        private void DrawIronworksBossMolten()
        {
            var fx=ironworksBossFx;if(fx.MoltenCount==0||iwMolten==null||iwCube==null)return;var b=iwbMelt??=new IronMoltenBatch();b.Count=0;
            for(int i=0;i<fx.MoltenCount&&b.Count<NativeBlockLooks.Chunk;i++)
            {
                var m=fx.Molten[i];int k=b.Count++;b.M[k]=Matrix4x4.TRS(new Vector3(m.At.X,m.At.Y,m.At.Z),new Quaternion(m.Rot.X,m.Rot.Y,m.Rot.Z,m.Rot.W),Vector3.one);
                b.Size[k]=new Vector4(m.Half.X,m.Half.Y,m.Half.Z,0);b.Flow[k]=new Vector4(m.Speed,m.Seed,0,0);b.Heat[k]=new Vector4(m.HeatA,m.HeatB,m.Crust,m.Front);b.Style[k]=new Vector4(m.Kind,m.Axis,m.Live,m.Gain);
            }
            if(UsesInstancing){b.Block.Clear();b.Block.SetVectorArray(MoltenSizeId,b.Size);b.Block.SetVectorArray(MoltenFlowId,b.Flow);b.Block.SetVectorArray(MoltenHeatId,b.Heat);b.Block.SetVectorArray(MoltenStyleId,b.Style);Graphics.DrawMeshInstanced(iwCube,0,iwMolten,b.M,b.Count,b.Block,ShadowCastingMode.Off,true,0,null);IronworksBossDraws++;return;}
            var one=iwOne??=new MaterialPropertyBlock();
            for(int k=0;k<b.Count;k++){one.Clear();one.SetVector(MoltenSizeId,b.Size[k]);one.SetVector(MoltenFlowId,b.Flow[k]);one.SetVector(MoltenHeatId,b.Heat[k]);one.SetVector(MoltenStyleId,b.Style[k]);Graphics.DrawMesh(iwCube,b.M[k],iwMolten,0,null,0,one,ShadowCastingMode.Off,true);IronworksBossDraws++;}
        }
        private void DrawIronworksBossFx(Camera camera,float time)
        {
            if(camera==null||iwbDraw==null)return;var q=ironworksBossFx.Quads;iwbDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?iwbDraw.Solid:iwbDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            iwbDraw.Draw(camera,null,time);
        }
        // The boss's heat haze: its sources as camera-facing quads in one dynamic mesh through the Ironworks' haze material; none on
        // the low tier.
        private void DrawIronworksBossHaze(Camera camera)
        {
            if(camera==null||iwHaze==null||LowGraphics||!UsesInstancing)return;var fx=ironworksBossFx;const int max=NativeIronworksBossFx.HeatMax;
            if(iwbHazeMesh==null)
            {
                iwbHazeMesh=new Mesh{name="THE CONVERTER / heat haze"};iwbHazeMesh.MarkDynamic();var idx=new int[max*6];
                for(int i=0;i<max;i++){idx[i*6]=i*4;idx[i*6+1]=i*4+2;idx[i*6+2]=i*4+1;idx[i*6+3]=i*4;idx[i*6+4]=i*4+3;idx[i*6+5]=i*4+2;}
                for(int i=0;i<max*4;i++){iwbHazeV.Add(Vector3.zero);iwbHazeC.Add(Color.clear);iwbHazeUv.Add(Vector2.zero);}
                iwbHazeMesh.SetVertices(iwbHazeV);iwbHazeMesh.SetColors(iwbHazeC);iwbHazeMesh.SetUVs(0,iwbHazeUv);iwbHazeMesh.SetIndices(idx,MeshTopology.Triangles,0);iwbHazeMesh.bounds=new Bounds(Vector3.zero,Vector3.one*2000);
            }
            if(fx.HeatCount==0)return;var t=camera.transform;Vector3 r=t.right,u=t.up;
            for(int i=0;i<max;i++)
            {
                int o=i*4;if(i>=fx.HeatCount){for(int k=0;k<4;k++){iwbHazeV[o+k]=Vector3.zero;iwbHazeC[o+k]=Color.clear;}continue;}
                var h=fx.Heat[i];var c=new Vector3(h.X,h.Y,h.Z);var a=r*(h.W/2);var b=u*(h.H/2);var col=new Color(1,1,1,h.Strength);
                iwbHazeV[o]=c-a-b;iwbHazeV[o+1]=c+a-b;iwbHazeV[o+2]=c+a+b;iwbHazeV[o+3]=c-a+b;iwbHazeUv[o]=new Vector2(-1,-1);iwbHazeUv[o+1]=new Vector2(1,-1);iwbHazeUv[o+2]=new Vector2(1,1);iwbHazeUv[o+3]=new Vector2(-1,1);
                for(int k=0;k<4;k++)iwbHazeC[o+k]=col;
            }
            iwbHazeMesh.SetVertices(iwbHazeV);iwbHazeMesh.SetColors(iwbHazeC);iwbHazeMesh.SetUVs(0,iwbHazeUv);
            Graphics.DrawMesh(iwbHazeMesh,Matrix4x4.identity,iwHaze,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private void DisposeIronworksBoss()
        {
            if(iwbMeshes!=null)foreach(var tier in iwbMeshes)if(tier!=null)foreach(var m in tier)if(m!=null)CapturedAnimation.Release(m);
            foreach(var m in new[]{iwbLink,iwbHook,iwbShackle,iwbHazeMesh})if(m!=null)CapturedAnimation.Release(m);
            if(iwbMaterial!=null)CapturedAnimation.Release(iwbMaterial);iwbMeshes=null;iwbJoints=null;iwbMaterial=null;iwbLink=iwbHook=iwbShackle=iwbHazeMesh=null;iwbDraw?.Dispose();iwbDraw=null;
            iwbArena=null;iwbLast=-1;
        }
    }

    public sealed partial class ConversionGame
    {
        /// <summary>DEV panel · Camera: THE CONVERTER's preview on the Ironworks map (presentation only; nothing spawns, hurts or collides).</summary>
        private void IronworksBossPreviewToggle()
        {
            NativeIronworksBossPreview.On=!NativeIronworksBossPreview.On;NativeIronworksBossPreview.Start=double.NaN;
            DevSay(NativeIronworksBossPreview.On?"Ironworks boss preview · on (THE CONVERTER's arrival, eight actions, phase changes, moments and death on a 118 s loop; presentation only)":"Ironworks boss preview · off",false);
        }
        private float IronworksBossPreviewPanel(float x,float y,float w)
        {
            DevToggleAct(new Rect(x,y,w,32),"preview-ironworks-boss",NativeIronworksBossPreview.On);y+=38;
            return DevNote(x,y,w,"On the Ironworks map: THE CONVERTER rises out of the pouring pit to Hydraulic Hell's intro and plays every action, both phase changes, a choked blow, a kneel, a crash and the death on a 118 s loop. Presentation only: the placeholder Siren still fights, nothing hurts, collides or reaches the network until Astra's boss record exists.",C.Muted());
        }
        private static readonly ExpeditionBossModel ironworksBossBanner=new ExpeditionBossModel();
        private const string IronworksBossPreviewLabel="DEV PREVIEW · THE CONVERTER · NOT GAMEPLAY";
        /// <summary>The preview's label, and the boss's banner and plate as the expedition HUD draws them for the zone boss.</summary>
        private void IronworksBossPreviewHud(float width,float height)
        {
            if(!NativeIronworksBossPreview.On||Event.current.type!=EventType.Repaint||arena==null||!NativeIronworksKit.Active(arena.MapId))return;
            var size=U.Measure(IronworksBossPreviewLabel,11,true,true,.12f);var r=new Rect(width/2-size.x/2-14,120,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,IronworksBossPreviewLabel,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            var v=NativeIronworksBossPreview.Shown;if(!v.live)return;double age=NativeIronworksBossPreview.ShownClock-v.spawned;var m=ironworksBossBanner;
            m.Name=NativeExpeditionStates.BossName(NativeIronworksBoss.Archetype);m.Area=NativeIronworksBossPreview.BannerArea;
            m.PlateCount=1;m.PlateNames[0]=m.Name;m.PlateDown[0]=v.Dead((float)NativeIronworksBossPreview.ShownClock);m.PlateHealth[0]=v.maxHp>0?Mathf.Clamp01(v.hp/v.maxHp):-1;m.PlatePercent[0]=null;
            double banner=age-NativeZoneBossNames.BannerDelay(NativeIronworksBoss.Archetype);m.PlateAge[0]=banner;
            NativeExpeditionStatesHud.BossPlates(m,width,128,Time.unscaledTime,false);
            if(banner>=0&&banner<NativeExpeditionStates.IntroSeconds)NativeExpeditionStatesHud.BossIntro(m,width,height,(float)banner,false);
        }
    }
}
