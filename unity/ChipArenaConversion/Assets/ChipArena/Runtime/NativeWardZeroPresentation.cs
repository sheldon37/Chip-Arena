using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The revamped Ward Zero's presentation (docs/arenas/WARDZERO_PROPOSAL.md section 10), drawn only while the map is the
    /// revamp (NativeWardZeroLooks.Active: map id "siren" carrying the draft's look "quarantine"). Today's Ward Zero has no look,
    /// so until Astra imports the draft nothing here runs and its hall (NativeWardPresentation and the exported "map-siren"
    /// model) draws as before; once the revamp is the map, those two stay off (NativeSourcePresentation, the menu backdrop).
    ///  - Dressing kit (Core NativeWardZeroKit): parts posed with their parent props from arena.AllBlocks each frame, driven by
    ///    the hazard states, the entries' open amounts (NativeVentRules.Open, read the way arena.Entry reads them) and the mood;
    ///    moving parts (doors, cell gates, vent lids, hatches, shutter slats, the ink) are offset by their Move share; parts
    ///    between the camera and the pilot are hidden; the low tier leaves the detail parts out. A breach panel's parts go with
    ///    its block, and hide while the breach presentation reports the breach open (<see cref="SetWardBreach"/>); the burst and
    ///    the holographic restore are that presentation's, not this one's.
    ///  - The mood (Core NativeWardZeroMood) from the replicated match and PvE state and the boss song, eased per frame.
    ///  - The eight practical lights picked for the camera (Core NativeWardZeroLights) through NativeLighting.PickedLights.
    ///  - Effects (Core NativeWardZeroFx) through one NativeFxDraw: a solid and an effects batch.
    ///  - The floor vents' shafts (today's NativeWardPit shader) at the floor vents the data marks as pits.
    /// Hazards: the map's `hazards` (ContentMap.hazards); their state is the pure Core function of (round index, FightAt,
    /// presentation clock), so host, guests and the killcam agree. Entries: PveContent.json's `entries` read with the draft's
    /// fields (NativeWardEntry); the index is the entry's position, as arena.Entry(e) uses it. Nothing here writes sim data,
    /// collides, damages or enters the network frame. Allocation free after the one-time builds.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private Material wzMaterial;private Mesh wzMesh;private bool wzFailed;private NativeFxDraw wzDraw;private NativeWardZeroFx wzFx;
        private readonly List<MetroBatch> wzBatches=new List<MetroBatch>();private int wzUsed;private MaterialPropertyBlock wzOne,wzPit;private Material wzPitMaterial;
        private ContentMap wzMap;private NativeWardZeroKit wzKit;private NativeWardFxScene wzScene;private NativeWardZeroLights wzLights;private MapHazard[] wzHazards=Array.Empty<MapHazard>();private NativeHazardState[] wzStates=Array.Empty<NativeHazardState>();
        private NativeWardEntry[] wzEntries;private float[] wzOpen=Array.Empty<float>();private bool[] wzBreached=Array.Empty<bool>();private readonly bool[] wzSongOn=new bool[2];private readonly float[] wzSongWarn=new float[2];
        private NativePropBody[] wzParents=Array.Empty<NativePropBody>();private float[] wzSeeds=Array.Empty<float>();private readonly Dictionary<string,int> wzIndex=new Dictionary<string,int>();
        private NativeEntryView[] wzWard;private NativeWardMood wzMood;private float wzLast=-1;private bool wzMoodSet;private NativeArena wzArena;private int wzGeneration=-1;

        // SourceWorld: the arena or the killcam replay arena.
        private void WardZeroScenery(NativeArena arena)
        {
            var map=arena.Map;if(!NativeWardZeroLooks.Active(map)||!EnsureWardZero())return;
            if(!ReferenceEquals(map,wzMap))WardZeroBuild(map);
            if(!ReferenceEquals(arena,wzArena)||arena.Generation!=wzGeneration){wzArena=arena;wzGeneration=arena.Generation;wzMoodSet=false;wzLast=-1;if(!arena.ReplicaScene.IsValid())Array.Clear(wzBreached,0,wzBreached.Length);}
            float now=PresentationClock(arena);var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            for(int i=0;i<wzStates.Length;i++)wzStates[i]=NativeWardZeroHazards.At(wzHazards[i],match.Phase,round,match.FightAt,now);
            WardZeroOpen(arena);WardZeroSong(arena);WardZeroMood(arena,now);
            Array.Clear(wzParents,0,wzParents.Length);foreach(var p in arena.AllBlocks){if(p==null||p.Definition?.id==null)continue;if(wzIndex.TryGetValue(p.Definition.id,out int i))wzParents[i]=p;}
            var eye=WorldEye;DrawWardKit(arena,now,eye);WardZeroPits();
            if(eye!=null){var c=eye.transform.position;float dt=wzLast<0?0:Mathf.Clamp(now-wzLast,0,.1f);wzLights.Update(c.x,c.y,c.z,wzMood,wzStates,now,dt);NativeLighting.PickedLights(wzLights.Pos,wzLights.Rgb);}
            DrawWardFx(now,eye);wzLast=now;
        }
        /// <summary>The breach presentation's report that breach entry `entry` (its index in the PvE entries, kind "breach") is
        /// open (the panel gone, its hologram rebuilding) or shut again: the kit's panel dressing hides while it is open. Other
        /// kinds are ignored; a new round or arena clears every report.</summary>
        public void SetWardBreach(int entry,bool open)
        {
            if(wzEntries==null||entry<0||entry>=wzBreached.Length||wzEntries[entry]?.kind!="breach")return;wzBreached[entry]=open;
            // The panel block itself is drawn by the arena dressing (the look is dressed), so it leaves that draw too.
            var block=wzEntries[entry].block;if(string.IsNullOrEmpty(block))return;if(open)NativeArenaDressing.Hidden.Add(block);else NativeArenaDressing.Hidden.Remove(block);
        }
        /// <summary>The same by the entry's id (NativeHordeBreachPresentation knows the PveEntry): call with true when it hides
        /// the segment and false when the segment's own look returns. Nothing happens off the revamp.</summary>
        public void SetWardBreach(string entryId,bool open){if(wzEntries==null||entryId==null)return;for(int i=0;i<wzEntries.Length;i++)if(wzEntries[i]?.id==entryId){SetWardBreach(i,open);return;}}
        // RenderBackdrop: behind the main menu, on the render clock: a cut-away of the map's blocks and kit (the vault and the
        // outer walls toward the camera left out), at rest in the calm mood, no effects. False when the map is not the revamp.
        private bool WardZeroBackdrop(string mapId,float time,Camera camera)
        {
            var map=NativeLighting.CurrentMap;if(mapId!=NativeWardZeroLooks.MapId||map==null||map.id!=mapId||!NativeWardZeroLooks.Active(map)||!EnsureWardZero())return false;
            if(!ReferenceEquals(map,wzMap))WardZeroBuild(map);wzArena=null;wzMoodSet=false;wzLast=-1;
            wzMood=NativeWardZeroMood.Preset("calm");for(int i=0;i<wzStates.Length;i++)wzStates[i]=NativeCyberCityHazards.Idle();Array.Clear(wzOpen,0,wzOpen.Length);
            var look=NativeLighting.Look(map);wzUsed=0;Vector3 cam=camera!=null?camera.transform.position:new Vector3(40,30,40);var blocks=map.blocks;
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];if(b?.id==null||Cutaway(b,cam))continue;var c=NativeBlockLooks.Resolve(look?.blockLooks,null,b.id,b.material,0x8A9496);var lin=MetroLinear(c.Color);
                WzAdd(Matrix4x4.TRS(Vec(b.pos),Rot(b.rotation),Vector3.one),new Vector4(b.size.x/2,b.size.y/2,b.size.z/2,NativeBlockLooks.Chamfer),new Vector4(b.pos.x,b.pos.y,b.pos.z,0),new Vector4(lin.r,lin.g,lin.b,1),new Vector4(c.Look,1,NativeBlockLooks.Seed(b.id),c.Flags|NativeBlockLooks.Spin(b.id,0)));
            }
            var parts=wzKit.Parts;bool low=LowGraphics;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];var b=blocks[part.Parent];if(part.Detail&&low||Cutaway(b,cam))continue;float f=NativeWardZeroKit.Move(part,wzStates,wzOpen,wzMood,out bool hidden);if(hidden)continue;
                var q=Rot(b.rotation);Vector3 at=Vec(b.pos)+q*new Vector3(part.X+part.Mx*f,part.Y+part.My*f,part.Z+part.Mz*f);
                NativeWardZeroKit.Drive(part,wzStates,wzOpen,wzMood,time,out float r,out float g,out float bl,out float level);
                WzAdd(Matrix4x4.TRS(at,q,Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(b.pos.x+part.X,b.pos.y+part.Y,b.pos.z+part.Z,0),MetroColor(r,g,bl,level),new Vector4(part.Look,1,wzSeeds[i],part.Flags));
            }
            for(int i=0;i<wzUsed;i++)WzDraw(wzBatches[i]);
            // No arena dressing publishes the look's haze behind the menu, so it goes out here (as Cyber City's backdrop does).
            var hz=look?.blocks;if(hz!=null){var h=MetroLinear(hz.hazeColor);Shader.SetGlobalVector(MetroHazeId,new Vector4(h.r,h.g,h.b,hz.haze));Shader.SetGlobalFloat(MetroHazeTopId,hz.hazeTop>0?hz.hazeTop:1);Shader.SetGlobalFloat(MetroDustId,hz.dust);}
            if(camera!=null){var p=camera.transform.position;wzLights.Settle(p.x*.4f,6,p.z*.4f,wzMood,wzStates,time);NativeLighting.PickedLights(wzLights.Pos,wzLights.Rgb);}
            return true;
        }
        // The menu's cut-away: the roof and the outer walls on the camera's side are left out.
        private static bool Cutaway(ContentBlock b,Vector3 cam)
        {
            var s=NativeWardZeroKit.Stem(b?.id);if(s==null)return false;if(s=="vault")return true;
            return s.StartsWith("shell-",StringComparison.Ordinal)&&b.pos.x*cam.x+b.pos.z*cam.z>0;
        }
        private static Quaternion Rot(ContentRotation r)=>r==null?Quaternion.identity:new Quaternion(r.x,r.y,r.z,r.w);
        private bool EnsureWardZero()
        {
            if(wzMaterial!=null)return true;if(wzFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                wzMaterial=new Material(shader){name="Ward Zero dressing",enableInstancing=true};wzMesh=MetroMeshOf(NativeBlockLooks.Build(0));wzDraw=new NativeFxDraw();wzFx=new NativeWardZeroFx();return true;
            }
            catch(Exception e){Debug.LogWarning("Ward Zero presentation disabled: "+e.Message);DisposeWardZero();wzFailed=true;return false;}
        }
        // Once per map content (the killcam's replay arena shares it).
        private void WardZeroBuild(ContentMap map)
        {
            if(wzEntries==null){var asset=Resources.Load<TextAsset>("PveContent");wzEntries=asset!=null?JsonUtility.FromJson<NativeWardEntrySet>(asset.text)?.entries??Array.Empty<NativeWardEntry>():Array.Empty<NativeWardEntry>();}
            wzMap=map;var blocks=map.blocks??Array.Empty<ContentBlock>();wzHazards=map.hazards??Array.Empty<MapHazard>();wzStates=new NativeHazardState[wzHazards.Length];
            wzKit=NativeWardZeroKit.Build(blocks,wzHazards,wzEntries);wzScene=NativeWardFxScene.Build(blocks,wzHazards,wzKit,wzEntries);wzLights=NativeWardZeroLights.Build(wzKit);
            wzOpen=new float[wzEntries.Length];wzBreached=new bool[wzEntries.Length];wzParents=new NativePropBody[blocks.Length];wzIndex.Clear();for(int i=0;i<blocks.Length;i++)if(blocks[i]?.id!=null)wzIndex[blocks[i].id]=i;
            wzSeeds=new float[wzKit.Parts.Length];for(int i=0;i<wzSeeds.Length;i++)wzSeeds[i]=NativeBlockLooks.Seed(blocks[wzKit.Parts[i].Parent].id+"#"+i);
            if(wzWard==null){wzWard=new NativeEntryView[8];for(int i=0;i<8;i++)wzWard[i]=new NativeEntryView{entry=i,kind="vent",mode="dive"};}
        }
        // Each entry's open amount as arena.Entry(e) and NativeWardPresentation read it (drawn: a PvP dive opens half a second
        // early and late), without the LINQ: in PvE the most open view of the entry; in PvP a forced view, else the first eight
        // vents' dive schedule (NativeVentRules.Ward, filled into kept views).
        private void WardZeroOpen(NativeArena arena)
        {
            float t=arena.Time;bool pvp=arena.Pve==null;var views=arena.Entries;Array.Clear(wzOpen,0,wzOpen.Length);
            if(!pvp){for(int k=0;k<views.Count;k++){var v=views[k];if(v!=null&&v.entry>=0&&v.entry<wzOpen.Length)wzOpen[v.entry]=Math.Max(wzOpen[v.entry],NativeVentRules.Open(v,t));}return;}
            for(int e=0;e<wzOpen.Length;e++)
            {
                NativeEntryView forced=null;for(int k=views.Count-1;k>=0;k--){var v=views[k];if(v!=null&&v.entry==e&&t>=v.openAt-1.5f&&t<=v.closeAt+1){forced=v;break;}}
                if(forced==null&&e<8){var w=wzWard[e];float offset=e*1.5f,start=offset+((float)Math.Floor((t-offset-5.5f)/12)+1)*12;w.openAt=start;w.closeAt=start+4.5f;forced=w;}
                wzOpen[e]=NativeVentRules.Open(forced,t,true);
            }
        }
        // The boss song's two vent sides (today's schedule, NativeBossTrack.Vent), for the song vents' looks.
        private void WardZeroSong(NativeArena arena)
        {
            double song=arena.BossSongTime;for(int s=0;s<2;s++){wzSongOn[s]=false;wzSongWarn[s]=0;}if(song<0)return;
            for(int s=0;s<2;s++){var v=arena.BossTrack.Vent(song,s);wzSongOn[s]=v.active;wzSongWarn[s]=v.warning;}
        }
        private void WardZeroMood(NativeArena arena,float now)
        {
            var pve=arena.Pve;var match=arena.Match;double song=arena.BossSongTime;var i=new NativeWardMoodInput{Phase=match.Phase,Pve=pve!=null,FightAge=now-match.FightAt,Song=song>=0};
            if(pve!=null){i.Stage=pve.Stage;i.Wave=pve.Wave;i.BossPhase=pve.Phase;i.Hidden=pve.Hidden&&pve.Stage=="boss";i.Holding=pve.Holding;i.Cinematic=pve.Cinematic;}
            if(song>=0){var track=arena.BossTrack;double looped=track.Looped(song);var pos=track.Position(looped);i.Section=track.Section(looped);i.BeatInBar=pos.beat;i.BeatPhase=pos.phase;}
            // The loudest alarm: a shutter warning or closing (the whole ward hears the klaxon).
            float alarm=0;for(int k=0;k<wzStates.Length;k++){var h=wzHazards[k];if(h==null||h.kind!=NativeWardZeroHazards.Shutter)continue;var ph=wzStates[k].Phase;if(ph==NativeHazardPhase.Warn)alarm=Math.Max(alarm,.5f);else if(ph==NativeHazardPhase.Danger)alarm=Math.Max(alarm,.35f);}
            i.Alarm=alarm;var target=NativeWardZeroMood.Target(i);float dt=wzLast<0?0:Mathf.Clamp(now-wzLast,0,.1f);
            if(!wzMoodSet){wzMood=target;wzMoodSet=true;}else NativeWardZeroMood.Ease(ref wzMood,target,dt);
        }
        private void DrawWardKit(NativeArena arena,float now,Camera eye)
        {
            wzUsed=0;Vector3 from=eye!=null?eye.transform.position:Vector3.zero,to=from;bool fade=eye!=null&&!FirstPerson&&arena.ViewPilot?.Body!=null;if(fade)to=arena.ViewPilot.Body.position+Vector3.up*.9f;
            var parts=wzKit.Parts;var blocks=arena.Map.blocks;bool low=LowGraphics;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];if(part.Detail&&low)continue;var prop=wzParents[part.Parent];if(prop==null)continue;if(part.Entry>=0&&part.Entry<wzBreached.Length&&wzBreached[part.Entry])continue;
                float f=NativeWardZeroKit.Move(part,wzStates,wzOpen,wzMood,out bool hidden);if(hidden)continue;
                var tr=prop.transform;Quaternion q=tr.rotation;Vector3 at=tr.position+q*new Vector3(part.X+part.Mx*f,part.Y+part.My*f,part.Z+part.Mz*f);
                if(fade&&part.Fade&&MetroBetween(from,to,at,new Vector3(part.Hx+.2f,part.Hy+.2f,part.Hz+.2f)))continue;
                NativeWardZeroKit.Drive(part,wzStates,wzOpen,wzMood,now,out float r,out float g,out float bl,out float level);var rest=blocks[part.Parent].pos;
                WzAdd(Matrix4x4.TRS(at,q,Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(rest.x+part.X,rest.y+part.Y,rest.z+part.Z,0),MetroColor(r,g,bl,level),new Vector4(part.Look,1,wzSeeds[i],part.Flags));
            }
            for(int i=0;i<wzUsed;i++)WzDraw(wzBatches[i]);
        }
        // Today's vent shaft (NativeWardPit: a per-pixel ray into a 2.1 m box) at each pit floor vent, by its open amount.
        private void WardZeroPits()
        {
            for(int i=0;i<wzEntries.Length;i++)
            {
                var e=wzEntries[i];if(e==null||!e.pit||wzOpen[i]<=.001f)continue;
                if(wzPitMaterial==null){var s=Resources.Load<Shader>("NativeWardPit");if(s==null)return;wzPitMaterial=new Material(s);}if(wzPit==null)wzPit=new MaterialPropertyBlock();
                var at=e.From;wzPit.Clear();wzPit.SetFloat("_Open",wzOpen[i]);Graphics.DrawMesh(cube,Matrix4x4.TRS(new Vector3(at.x,at.y+.012f,at.z),Quaternion.identity,new Vector3(2.1f,.005f,2.1f)),wzPitMaterial,0,null,0,wzPit,ShadowCastingMode.Off,false);
            }
        }
        private void WzAdd(Matrix4x4 m,Vector4 size,Vector4 rest,Vector4 color,Vector4 look)
        {
            if(wzUsed==0||wzBatches[wzUsed-1].Count==NativeBlockLooks.Chunk){if(wzUsed==wzBatches.Count)wzBatches.Add(new MetroBatch());wzBatches[wzUsed].Count=0;wzUsed++;}
            Put(wzBatches[wzUsed-1],m,size,rest,color,look);
        }
        private void WzDraw(MetroBatch b)
        {
            if(b.Count==0)return;MetroUpload(b);
            // Indoors the look's key light casts no shadows, so the dressing casts none either (it still receives).
            if(UsesInstancing){Graphics.DrawMeshInstanced(wzMesh,0,wzMaterial,b.M,b.Count,b.Block,ShadowCastingMode.Off,true,0,null);return;}
            // Compatibility rendering: one by one, reusing one block made on this main-thread draw path (never a field initializer).
            var one=wzOne??=new MaterialPropertyBlock();
            for(int i=0;i<b.Count;i++){one.Clear();one.SetVector(MetroSizeId,b.Size[i]);one.SetVector(MetroRestId,b.Rest[i]);one.SetVector(MetroColorId,b.Color[i]);one.SetVector(MetroLookId,b.Look[i]);Graphics.DrawMesh(wzMesh,b.M[i],wzMaterial,0,null,0,one,ShadowCastingMode.Off,true);}
        }
        private void DrawWardFx(float time,Camera camera)
        {
            if(camera==null)return;ReadCyberLight();var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            wzFx.Build(wzScene,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,metroLight,Mathf.Clamp01(EffectsQuality),LowGraphics,wzMood,wzStates,wzOpen,wzSongOn,wzSongWarn);
            var q=wzFx.Quads;wzDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?wzDraw.Solid:wzDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), as Cyber City's effects do.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            wzDraw.Draw(camera,null,time);
        }
        private void DisposeWardZero()
        {
            CapturedAnimation.Release(wzMesh);CapturedAnimation.Release(wzMaterial);if(wzPitMaterial!=null)CapturedAnimation.Release(wzPitMaterial);wzMesh=null;wzMaterial=null;wzPitMaterial=null;wzDraw?.Dispose();wzDraw=null;
            wzBatches.Clear();wzMap=null;wzKit=null;wzScene=null;wzLights=null;wzArena=null;wzGeneration=-1;wzMoodSet=false;wzLast=-1;
        }
    }
}
