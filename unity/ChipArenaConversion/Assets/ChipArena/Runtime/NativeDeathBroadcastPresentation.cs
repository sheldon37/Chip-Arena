using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Material hologramMaterial;
        public string Hologram(string model)
        {
            string id="hologram-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return model;if(hologramMaterial==null){hologramMaterial=new Material(Resources.Load<Shader>("NativeHologram")){enableInstancing=true};materials.Add(hologramMaterial);}models[id]=source.Select(p=>new Part{Name=p.Name,Bone=p.Bone,Mesh=p.Mesh,Material=hologramMaterial}).ToArray();return id;
        }
    }
    public sealed partial class ArenaPresentation
    {
        /// <summary>One broadcast's hologram rig and what it draws, built once (no per-frame allocation).</summary>
        private sealed class BroadcastStage
        {
            public CapturedAnimation.Player Rig;public Func<int,Matrix4x4> Bone;public string Pilot,Hat;public int HatRow;public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
            // The victim's model bust (NativeVisualBodyPresentation.cs VisualBust; null: the voxel hologram), its cut meshes, the
            // frame it last drew.
            public VisualBody Bust;public List<Mesh> Owned;public int Frame=-1;
            public void Dispose(){Rig?.Dispose();if(Owned!=null)foreach(var m in Owned)if(m!=null)UnityEngine.Object.Destroy(m);Owned=null;}
        }
        private readonly NativeDeathBroadcast broadcasts=new NativeDeathBroadcast();private readonly Dictionary<int,BroadcastStage> broadcastStages=new Dictionary<int,BroadcastStage>();
        private readonly List<int> staleStages=new List<int>();private readonly List<NativePilot> broadcastDeaths=new List<NativePilot>();
        private static readonly Comparison<NativePilot> ByDeath=(a,b)=>a.Death.at!=b.Death.at?a.Death.at.CompareTo(b.Death.at):a.Death.seed.CompareTo(b.Death.seed);
        private static readonly Func<int,bool> PortraitBone=NativeBroadcastFx.PortraitBone;
        public IEnumerable<string> DeathMessages(float time)=>broadcasts.Items.Where(i=>time>=i.At+1.05f&&time<i.At+5.15f).Select(i=>i.Text);
        // When the newest visible death line appeared (the source's .is-new entrance), or -1 when none is shown.
        public float LatestDeathMessage(float time)=>broadcasts.Items.Where(i=>time>=i.At+1.05f&&time<i.At+5.15f).Select(i=>i.At+1.05f).DefaultIfEmpty(-1).Max();
        // The death presentation's shared primitives (NativeDeathFxDraw): filled by DeathPresentation and DeathBroadcasts each
        // frame and drawn once at the end of DeathBroadcasts. Null when the shader is unavailable.
        private NativeDeathFxDraw deathFx;private bool deathFxFailed;
        private NativeDeathFxDraw DeathFx(){if(deathFx==null&&!deathFxFailed){deathFx=NativeDeathFxDraw.Create();deathFxFailed=deathFx==null;}return deathFx;}
        /// <summary>
        /// death-announcements.ts: per death a rocket with a line trail climbs to the stage over the northern skyline
        /// (skyBroadcastFrame, mirrored into Unity's x), bursts into point-sprite stars, and the victim's cyan hologram unfolds
        /// with its part-box edge lines, the drifting scanlines, the corner/tick frame and the nameplate (victim, verb, killer).
        /// The stage is fixed in the world; only the newest three launched broadcasts show.
        /// </summary>
        private void DeathBroadcasts(NativeArena arena)
        {
            float t=arena.Time;bool clear=arena.Match.Phase==NativePhase.Lobby||arena.Match.Phase==NativePhase.Intro;broadcasts.Tick(t,clear);
            if(!clear)
            {
                broadcastDeaths.Clear();foreach(var p in arena.Pilots)if(p.Hp<=0&&p.Death!=null)broadcastDeaths.Add(p);broadcastDeaths.Sort(ByDeath);
                foreach(var p in broadcastDeaths)if(!broadcasts.Seen(p.Death.seed))broadcasts.Add(p.Death.seed,p.Seat.id,p.Death.owner,p.Death.at,t,p.Seat.name,arena.Match.Seat(p.Death.owner)?.name,p.Death.damageKind);
            }
            staleStages.Clear();foreach(int id in broadcastStages.Keys){bool live=false;foreach(var a in broadcasts.Items)if(a.Seed==id){live=true;break;}if(!live)staleStages.Add(id);}
            foreach(int id in staleStages){broadcastStages[id].Dispose();broadcastStages.Remove(id);}
            var fx=DeathFx();var items=broadcasts.Items;const float k=NativeBroadcastFx.Scale;
            for(int index=0;index<items.Count;index++)
            {
                var item=items[index];if(!broadcasts.Shown(index,t))continue;float age=t-item.At;var s=NativeBroadcastFx.Sample(age);
                NativeBroadcastFx.Frame(item.Slot,out float fx0,out float fy0,out float fz0);Vector3 center=new Vector3(-fx0,fy0,fz0);
                // Stage-root units to Unity: x mirrored, scale 5.5; the hologram's children are also scaled (1, reveal, 1).
                Vector3 L(float x,float y,float z)=>center+new Vector3(-x,y,z)*k;Vector3 H(float x,float y,float z)=>center+new Vector3(-x,y*s.ScaleY,z)*k;
                if(fx!=null&&s.Rocket)
                {
                    fx.Sphere(Matrix4x4.TRS(L(s.RocketX,s.RocketY,0),Quaternion.identity,Vector3.one*NativeBroadcastFx.RocketRadius*k),fx.Hex(NativeBroadcastFx.RocketColor,1));
                    fx.Line(L(s.TrailX,s.TrailY,0),L(0,s.TrailEndY,0),fx.Hex(NativeBroadcastFx.LineColor,NativeBroadcastFx.TrailOpacity));
                }
                if(fx!=null&&s.Stars){var star=fx.Hex(NativeBroadcastFx.StarColor,s.StarOpacity);for(int i=0;i<NativeBroadcastFx.Stars;i++){NativeBroadcastFx.Star(i,s.Burst,out float x,out float y,out float z);fx.Point(L(x,y,z),NativeBroadcastFx.StarSize*k,star);}}
                if(!s.Holo)continue;
                if(!broadcastStages.TryGetValue(item.Seed,out var stage))
                {
                    stage=new BroadcastStage{Rig=captured.Create(transform,-item.Seed)};var rig=stage.Rig;stage.Bone=b=>rig.Bones[b].localToWorldMatrix;var seat=arena.Match.Seat(item.Victim);
                    stage.Pilot=sourceModels.Hologram(BodyModel(BodySkin(item.Victim),BodyCloseTier));stage.HatRow=NativeBroadcastEdges.HatIndex(seat?.hat);stage.Hat=stage.HatRow>0?sourceModels.Hologram("hat-"+seat.hat):null;broadcastStages[item.Seed]=stage;
                    stage.Bust=VisualBust(rig,item.Victim,ref stage.Owned);
                }
                // The portrait: head and shoulders fitted into the frame (create()), its head drifting (update()).
                stage.Pose.Reset();stage.Pose.Rotation[CapturedRig.Head]=CapturedRig.Euler(s.HeadX,s.HeadY);NativePoseWriter.Write(stage.Rig.Bones,stage.Pose,"a death broadcast rig");
                NativeBroadcastFx.RigFit(NativeBroadcastEdges.Hats[stage.HatRow],out float fit,out float ox,out float oy,out float oz);
                stage.Rig.Root.transform.SetPositionAndRotation(H(ox,oy,oz),Quaternion.identity);stage.Rig.Root.transform.localScale=new Vector3(k*fit,k*fit*s.ScaleY,k*fit);
                var root=stage.Rig.Root.transform.localToWorldMatrix;
                // The model's bust (it wears no hat), else the voxel hologram and its hat.
                bool model=stage.Bust?.Root!=null&&!ClassicBodies;
                if(model){VisualBustPose(stage.Bust,stage.Pose,s.HologramAlpha);stage.Frame=Time.frameCount;}
                else{sourceModels.Queue(stage.Pilot,root,stage.Bone,PortraitBone,opacity:s.HologramAlpha);if(stage.Hat!=null)sourceModels.Queue(stage.Hat,root,stage.Bone,opacity:s.HologramAlpha);}
                if(fx==null)continue;
                // Edges of every visible portrait mesh (EdgesGeometry, 24 degrees), riding their bones.
                var edge=fx.Hex(NativeBroadcastFx.LineColor,s.EdgeAlpha);
                void Edges(float[] list){for(int i=0;i+6<list.Length;i+=7){var m=stage.Rig.Bones[(int)list[i]].localToWorldMatrix;fx.Line(m.MultiplyPoint3x4(new Vector3(-list[i+1],list[i+2],list[i+3])),m.MultiplyPoint3x4(new Vector3(-list[i+4],list[i+5],list[i+6])),edge);}}
                // (The voxel portrait's edges; the model's fresnel draws its own.)
                if(!model){Edges(NativeBroadcastEdges.Body);Edges(NativeBroadcastEdges.HatSegments[stage.HatRow]);}
                var scan=fx.Hex(NativeBroadcastFx.LineColor,s.ScanAlpha);foreach(float y in NativeBroadcastFx.ScanLines)fx.Line(H(-NativeBroadcastFx.ScanHalfWidth,y+s.ScanOffset,NativeBroadcastFx.ScanZ),H(NativeBroadcastFx.ScanHalfWidth,y+s.ScanOffset,NativeBroadcastFx.ScanZ),scan);
                var frame=fx.Hex(NativeBroadcastFx.LineColor,s.FrameAlpha);var f=NativeBroadcastFx.FrameSegments;for(int i=0;i+5<f.Length;i+=6)fx.Line(H(f[i],f[i+1],f[i+2]),H(f[i+3],f[i+4],f[i+5]),frame);
                // The nameplate canvas: one pixel across and down the plane from its top-left corner.
                NativeBroadcastFx.NamePixel(0,0,out float nx,out float ny);NativeBroadcastFx.NamePixel(1,1,out float rx,out float dy);Vector3 origin=H(nx,ny,NativeBroadcastFx.NameZ);
                Vector3 right=H(rx,ny,NativeBroadcastFx.NameZ)-origin,down=H(nx,dy,NativeBroadcastFx.NameZ)-origin;var ink=fx.Hex(NativeBroadcastFx.NameColor,s.NameAlpha);var font=fx.TextFont;float half=NativeBroadcastFx.CanvasWidth*.5f;
                fx.Text(fx.Quads,font,item.Upper,32,FontStyle.Normal,NativeBroadcastFx.NameSizes[0],half,NativeBroadcastFx.NameRows[0],NativeBroadcastFx.MaxTextWidth,origin,right,down,ink);
                fx.Text(fx.Quads,font,item.DamageKind=="collapse"?"COLLAPSE":NativeBroadcastFx.Verb(item.Killer!=null),32,FontStyle.Normal,NativeBroadcastFx.NameSizes[1],half,NativeBroadcastFx.NameRows[1],NativeBroadcastFx.MaxTextWidth,origin,right,down,ink);
                if(item.Killer!=null)fx.Text(fx.Quads,font,item.Killer,32,FontStyle.Normal,NativeBroadcastFx.NameSizes[2],half,NativeBroadcastFx.NameRows[2],NativeBroadcastFx.MaxTextWidth,origin,right,down,ink);
            }
            fx?.Draw();
            // A bust not drawn this frame hides (its broadcast between beats, Classic chosen).
            foreach(var stage in broadcastStages.Values)if(stage.Bust?.Root!=null&&stage.Frame!=Time.frameCount&&stage.Bust.Root.activeSelf)stage.Bust.Root.SetActive(false);
        }
        private void ClearBroadcasts(){broadcasts.Tick(0,true);foreach(var stage in broadcastStages.Values)stage.Dispose();broadcastStages.Clear();deathFx?.Dispose();deathFx=null;deathFxFailed=false;}
    }
}
