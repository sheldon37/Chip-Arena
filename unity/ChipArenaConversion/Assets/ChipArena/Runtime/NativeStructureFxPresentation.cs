using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws Brickfall's destruction VFX kit (Core/NativeStructureFx.cs) in native arenas: one opaque mesh of face-shaded debris
    /// cubes and one premultiplied mesh of decals, dust billboards (sorted back to front), wires and additive sparks and glints,
    /// both on the shared NativeFxDraw primitives (Resources/NativeSirenFx.shader), so the kit costs two draws, no Unity lights
    /// and no per-frame allocation once warm. It is fed from the arena's event stream with its own cursor, so host, guests and
    /// the killcam replay (RenderGlow on the replay arena) build the same records; every record is pure in its age on the
    /// presentation clock and the intake is deduplicated by (seed, birth). It runs inside ArenaPresentation.RenderGlow, which
    /// intros, the draft, menus' previews and the Customize preview never reach. A new round (generation) or map, or a full
    /// ResetPresentation, clears it; a rewind (a guest's clock correction) only re-reads the event buffer, and the killcam's
    /// ResetPresentation(keepWorldMarks) at its start and end keeps the decals and the structure records, which the 1 s event
    /// buffer could not rebuild, so the replay shows the marks already made and the round keeps them afterwards.
    /// <list type="bullet">
    /// <item>Break (today's maps too): the material burst replaces NativeImpactCues' 18-spark burst (NativeBlastPresentation
    /// skips it while <see cref="ArenaPresentation.Structures"/> exists). Brickfall adds chip and crack decals, scorch marks
    /// under explosions and the collapse dust drift; elsewhere those stay dormant.</item>
    /// <item>Structure events (docs/arenas/ARENA_PROPOSAL.md §5.4) are not emitted yet. When Core/ArenaModel.cs gains the kinds,
    /// the hook is three lines in <see cref="Intake"/> (marked there) plus three in NativeAudio.EventSounds (see
    /// NativeStructureAudio.cs); until then <see cref="Stress"/>, <see cref="Fall"/>, <see cref="Land"/> and
    /// <see cref="Structure"/> are the API, and the dev Sandbox previews the whole sequence. Each of them, and the preview,
    /// also hands the event to ArenaPresentation.StructureHeard: the Brickfall atmosphere (pigeons, drifting dust) and the
    /// round's dust coat (landings) hear every structure event through this one intake.</item>
    /// <item>Fall timing (a hook dependency): the kit poses no blocks. Its streamers, falling debris and trailing wire ends drop at
    /// the fall's acceleration, the contract's pancake maxAccel (6.9 m/s²) unless the hook passes the segment pose's from Core
    /// NativeStructureRules (at most g for peel and topple pieces), and the rubble, dust wall and the window and sag release wait
    /// for StructureLand, which must be emitted when the segment's last piece lands.</item>
    /// <item>The dressed blocks' telegraph: NativeArenaDressing reads <see cref="Fx"/> each frame on the same field clock
    /// (NativeStructureFx.StressHolding, then TelegraphAt per block: the 8 Hz window strobe then black and the sag) and
    /// <see cref="NativeStructureFx.LampStrobe"/> for the band lamps. <see cref="Window"/>, <see cref="Sag"/> and
    /// <see cref="Freshness"/> (rubble fresh to dusty over 10 s) answer the same at a point on this kit's last clock.</item>
    /// <item><see cref="KillFloor"/>: the per-map kill-effect floor (§5.5 Gibs), a raycast down that skips fracture fragments and
    /// dynamic crates on every client alike.</item>
    /// </list>
    /// </summary>
    public sealed class NativeStructureFxPresentation : IDisposable
    {
        private readonly NativeStructureFx fx=new NativeStructureFx();private readonly NativeFxDraw draw=new NativeFxDraw();
        /// <summary>The presentation that hears every structure event this kit takes in (ArenaPresentation.StructureHeard).</summary>
        internal ArenaPresentation Owner;
        private int generation=-1,lastEvent;private float lastNow=-1;private string blockMap;private ContentBlock[] blocks;
        private readonly int[] order=new int[NativeStructureFx.PuffCap];private readonly float[] depth=new float[NativeStructureFx.PuffCap];
        private readonly Color[] faceLight=new Color[6];private Color topLight,bottomLight;
        private static readonly RaycastHit[] hits=new RaycastHit[24];
        private static readonly Vector3[] Normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        private const float Authored=NativeFxDraw.Authored,Glowing=NativeFxDraw.Authored+NativeFxDraw.Additive;
        public NativeStructureFx Fx=>fx;
        /// <summary>The presentation clock of the last frame (records, the preview and the shake share it).</summary>
        public float Clock{get;private set;}
        /// <summary>Forgets everything (the dev preview's Clear Effects, a new round or map, a full ResetPresentation).</summary>
        public void Clear(){fx.Clear();draw.Clear();lastEvent=0;lastNow=-1;}
        /// <summary>Keeps every record and decal and re-reads the event buffer (the killcam's start and end): the intake is
        /// deduplicated and every record pure in its age, so nothing doubles and the replay shows what was already there.</summary>
        public void Reset(){draw.Clear();lastEvent=0;lastNow=-1;}
        public void Dispose(){fx.Clear();draw.Dispose();}
        private static V3 V(Vector3 v)=>new V3(v.x,v.y,v.z);private static Vector3 U(V3 v)=>new Vector3(v.x,v.y,v.z);

        /// <summary>now: the presentation clock (FieldClock: continuous on guests). quality: BlastQuality (1, .3 reduced); the
        /// low graphics tier (3 pixel lights) also halves debris and atmosphere. Telegraphs ignore both.</summary>
        public void Render(NativeArena arena,Camera camera,float now,float quality)
        {
            if(arena==null||camera==null)return;
            if(generation!=arena.Generation||blockMap!=arena.MapId){Clear();generation=arena.Generation;blockMap=arena.MapId;blocks=arena.Content?.maps?.FirstOrDefault(m=>m.id==arena.MapId)?.blocks;}
            else if(now<lastNow-.25f)lastEvent=0;lastNow=now;Clock=now;
            fx.Wind=NativeStructureFx.WindFor(arena.MapId);Intake(arena,now);if(QualitySettings.pixelLightCount<=3)quality=Math.Min(quality,.45f);
            fx.Frame(now,V(camera.transform.position),quality);draw.Clear();Lights();Fill(camera);draw.Draw(camera,null,now);
        }
        private void Intake(NativeArena arena,float now)
        {
            var events=arena.Events;int newest=0;for(int i=0;i<events.Count;i++)newest=Math.Max(newest,events[i].Id);if(newest>0&&newest<lastEvent)lastEvent=0;
            bool brickfall=NativeStructureFx.MapMarks(arena.MapId);
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=lastEvent)continue;float age=now-e.Time;
                switch(e.Kind)
                {
                    case EventKind.Break:if(age<NativeStructureFx.BurstLife)Break(arena,e,brickfall);break;
                    case EventKind.Explosion:if(brickfall&&age<1)Scorch(arena,e);break;
                    // Structure events (§5.4). When Core/ArenaModel.cs has the kinds, uncomment these three lines:
                    // case EventKind.StructureStress:Structure(arena,NativeStructureCue.Stress,e);break;
                    // case EventKind.StructureFall:Structure(arena,NativeStructureCue.Fall,e);break;
                    // case EventKind.StructureLand:Structure(arena,NativeStructureCue.Land,e);break;
                }
            }
            lastEvent=Math.Max(lastEvent,newest);
        }
        private ContentBlock Block(string id){if(blocks==null||string.IsNullOrEmpty(id))return null;for(int i=0;i<blocks.Length;i++)if(blocks[i].id==id)return blocks[i];return null;}
        private static uint Seed(NativeArena arena,string id)=>NativeStructureFx.Seed(arena.MapId,id,NativeStructureFx.RoundIndex(arena.Match.Set,arena.Match.Round));
        private void Break(NativeArena arena,in ArenaEvent e,bool decals)
        {
            var def=Block(e.SurfaceId);V3 size=def!=null?def.size:new V3(1,1,1);Vector3 at=NativeArena.Vec(e.Position);
            float floor=SurfaceBelow(arena,at+Vector3.down*(size.y*.5f-.05f),at.y-size.y*.5f);
            fx.Break(NativeStructureFx.Classify(e.Detail),NativeImpactCues.MaterialColor(arena.Content?.materials,e.Detail),e.Position,size,floor,Seed(arena,e.SurfaceId??e.Detail),e.Time,decals);
        }
        // Only blasts and fire scorch (NativeStructureFx.ScorchScale): trampolines, static procs, wormholes and fields leave none.
        private void Scorch(NativeArena arena,in ArenaEvent e)
        {
            float scale=NativeStructureFx.ScorchScale(e.Detail);if(scale<=0)return;
            Vector3 at=NativeArena.Vec(e.Position);float ground=SurfaceBelow(arena,at+Vector3.up*.3f,float.NaN,3.3f);if(float.IsNaN(ground))return;
            fx.AddMark(NativeStructureMark.Scorch,new V3(at.x,ground,at.z),new V3(0,1,0),Mathf.Clamp(e.Radius*.8f,.6f,3)*scale,Seed(arena,"scorch")^(uint)BitConverter.SingleToInt32Bits(e.Time)^(uint)BitConverter.SingleToInt32Bits(e.Position.x),e.Time);
        }
        /// <summary>The highest surface below <paramref name="from"/> that is not <see cref="Movable"/>, else the fallback.</summary>
        private static float SurfaceBelow(NativeArena arena,Vector3 from,float fallback,float reach=40)
        {
            var physics=arena.Root.scene.GetPhysicsScene();int n=physics.Raycast(from,Vector3.down,hits,reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
            float best=float.NegativeInfinity;for(int i=0;i<n;i++){if(Movable(hits[i].collider))continue;best=Math.Max(best,hits[i].point.y);}return float.IsNegativeInfinity(best)?fallback:best;
        }
        /// <summary>The ground a structure lands on: the lowest surface under the point that is not <see cref="Movable"/> (through
        /// its own floors).</summary>
        private static float Ground(NativeArena arena,Vector3 at,float fallback)
        {
            var physics=arena.Root.scene.GetPhysicsScene();int n=physics.Raycast(new Vector3(at.x,at.y+1,at.z),Vector3.down,hits,60,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
            float best=float.PositiveInfinity;for(int i=0;i<n;i++){if(Movable(hits[i].collider))continue;best=Math.Min(best,hits[i].point.y);}return float.IsPositiveInfinity(best)?fallback:best;
        }
        /// <summary>Fracture fragments and dynamic crates: a rule on the prop's content, the same on every client. The body's kinematic
        /// state is not (only the host simulates; guests and the killcam replay make every prop kinematic), so floors, marks and gibs
        /// would differ between them.</summary>
        private static bool Movable(Collider c){var prop=c!=null?c.GetComponent<NativePropBody>():null;return prop!=null&&(prop.Fragment||prop.Definition!=null&&prop.Definition.dynamic);}

        // ---------- structure API (§5.4) ----------
        /// <summary>The one-line event switch target: maps an authoritative structure event's §5.4 fields onto the kit.</summary>
        public void Structure(NativeArena arena,NativeStructureCue cue,in ArenaEvent e)
        {
            switch(cue)
            {
                case NativeStructureCue.Stress:Stress(arena,e.Detail,NativeArena.Vec(e.Position),e.Radius,e.Amount,e.Time);break;
                case NativeStructureCue.Fall:Fall(arena,e.Detail,NativeArena.Vec(e.Position),e.Radius,NativeArena.Vec(e.Normal),e.Amount,e.SurfaceId,e.Time);break;
                default:Land(arena,e.Detail,NativeArena.Vec(e.Position),e.Radius,e.Amount,e.Time);break;
            }
        }
        /// <summary>StructureStress: segment id, centroid, extent, telegraph seconds. <paramref name="crush"/>: the sim's crush
        /// volume when the caller has it (Core NativeStructureRules); otherwise a box around the centroid.</summary>
        public void Stress(NativeArena arena,string segment,Vector3 centroid,float radius,float telegraph,float time,Bounds? crush=null)
        {
            float floor=Ground(arena,crush?.center??centroid,centroid.y-radius);V3 center=default,half=default;if(crush.HasValue){center=V(crush.Value.center);half=V(crush.Value.extents);}
            fx.Stress(V(centroid),radius,telegraph,center,half,floor,Seed(arena,segment),time);Owner?.StructureHeard(arena,NativeStructureCue.Stress,centroid,radius,telegraph,time);
        }
        /// <summary>StructureFall: segment id, position, extent, fall direction, mass in tonnes, dominant material id.
        /// <paramref name="accel"/>: the segment's fall acceleration from its pose (Core NativeStructureRules once it exists; at most
        /// g); 0, as the event carries none, takes the contract's pancake maxAccel, 6.9 m/s².</summary>
        public void Fall(NativeArena arena,string segment,Vector3 position,float radius,Vector3 direction,float tonnes,string material,float time,float accel=0)
        {fx.Fall(V(position),radius,V(direction),tonnes,NativeStructureFx.Classify(material),Ground(arena,position,position.y-radius),Seed(arena,segment),time,accel);Owner?.StructureHeard(arena,NativeStructureCue.Fall,position,radius,tonnes,time);}
        /// <summary>StructureLand: segment id, position, extent, energy 0..1 (its material is the one its fall named).</summary>
        public void Land(NativeArena arena,string segment,Vector3 position,float radius,float energy,float time)
        {uint seed=Seed(arena,segment);fx.Land(V(position),radius,energy,fx.FallMaterial(seed,NativeStructureMaterial.Masonry),Ground(arena,position,position.y-radius*.5f),seed,time);Owner?.StructureHeard(arena,NativeStructureCue.Land,position,radius,energy,time);}
        /// <summary>Camera shake offset heard at <paramref name="eye"/> (§3.10; the caller applies it only with camera shake on).</summary>
        public Vector3 Shake(Vector3 eye)=>U(fx.ShakeOffset(Clock,V(eye)));
        public float Window(Vector3 p)=>fx.WindowAt(V(p),Clock);
        public float Sag(Vector3 p)=>fx.SagAt(V(p),Clock);
        public float Freshness(Vector3 p)=>fx.FreshnessAt(V(p),Clock);

        // ---------- dev preview (Sandbox, dev accounts) ----------
        /// <summary>Stress → fall → land on the stand-in quadrant at <paramref name="spot"/>, falling along <paramref name="away"/>.</summary>
        public void PreviewCollapse(NativeArena arena,Vector3 spot,Vector3 away,NativeStructureMaterial material)
        {
            float now=Clock,floor=SurfaceBelow(arena,spot+Vector3.up*.6f,spot.y);var s=new V3(spot.x,floor,spot.z);uint seed=Seed(arena,"preview-"+(int)material)^(uint)Mathf.FloorToInt(now*16);
            NativeStructurePreview.Box(s,floor,out V3 center,out V3 half);V3 centroid=NativeStructurePreview.Centroid(s,floor);float radius=NativeStructurePreview.Radius;
            fx.Preview(s,material,floor,seed,now);fx.Stress(centroid,radius,NativeStructurePreview.Telegraph,center,half,floor,seed,now);
            fx.Fall(centroid,radius,V(away),NativeStructurePreview.Mass,material,floor,seed,now+NativeStructurePreview.Telegraph,NativeStructurePreview.Accel);
            fx.Land(new V3(s.x,floor+1.2f,s.z),radius*.8f,NativeStructurePreview.Energy,material,floor,seed,now+NativeStructurePreview.LandAt);
            // The atmosphere and the dust coat hear the preview as they would the real events (each at its scheduled time).
            if(Owner!=null){Owner.StructureHeard(arena,NativeStructureCue.Stress,U(centroid),radius,NativeStructurePreview.Telegraph,now);Owner.StructureHeard(arena,NativeStructureCue.Fall,U(centroid),radius,NativeStructurePreview.Mass,now+NativeStructurePreview.Telegraph);Owner.StructureHeard(arena,NativeStructureCue.Land,new Vector3(s.x,floor+1.2f,s.z),radius*.8f,NativeStructurePreview.Energy,now+NativeStructurePreview.LandAt);}
        }
        /// <summary>One break of each material in a row across <paramref name="right"/>, a third of a second apart.</summary>
        public void PreviewBursts(NativeArena arena,Vector3 spot,Vector3 right)
        {
            for(int i=0;i<5;i++)
            {
                Vector3 at=spot+right*((i-2)*2.2f)+Vector3.up*1.1f;float floor=SurfaceBelow(arena,at,at.y-.5f);var m=(NativeStructureMaterial)i;
                fx.Break(m,0,V(at),new V3(1,1,1),floor,Seed(arena,"burst-preview-"+i)^(uint)Mathf.FloorToInt(Clock*16),Clock+i*.35f,NativeStructureFx.MapMarks(arena.MapId));
            }
        }

        /// <summary>
        /// §5.5 Gibs: the kill-effect floor under a source-axes point (Unity x mirrored): the highest surface within 40 m below it
        /// that is not <see cref="Movable"/>, or NaN where there is none (the effect keeps its own rule there). Upper floors now hold
        /// gibs on every map, and host, guests and the killcam find the same floor.
        /// </summary>
        public static NativeKillFloor KillFloor(NativeArena arena)
        {
            if(arena?.Root==null)return null;
            return (x,y,z)=>{float h=SurfaceBelow(arena,new Vector3(-(float)x,(float)y+.25f,(float)z),float.NaN);return float.IsNaN(h)?double.NaN:h;};
        }

        // ---------- mesh ----------
        // Baked Lambert in the scene's light (NativeLit's albedo x (trilight ambient + sun N·L)), kept as an ambient cube in
        // display values so a rotated face blends the six axis lights by its normal's squares.
        private void Lights()
        {
            var sun=RenderSettings.sun;Vector3 l=sun!=null?-sun.transform.forward:new Vector3(.35f,.85f,.55f).normalized;Color key=sun!=null&&sun.isActiveAndEnabled?sun.color.linear*sun.intensity:Color.white*.8f;
            bool tri=RenderSettings.ambientMode==UnityEngine.Rendering.AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,mid=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,low=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            for(int f=0;f<6;f++){var n=Normals[f];Color amb=n.y>0?Color.Lerp(mid,sky,n.y):n.y<0?Color.Lerp(mid,low,-n.y):mid;Color c=amb+key*Mathf.Max(0,Vector3.Dot(n,l));faceLight[f]=new Color(Mathf.LinearToGammaSpace(c.r),Mathf.LinearToGammaSpace(c.g),Mathf.LinearToGammaSpace(c.b),1);}
            Color side=(faceLight[0]+faceLight[1]+faceLight[4]+faceLight[5])*.25f;topLight=faceLight[2]*.55f+side*.45f;bottomLight=side*.6f+faceLight[3]*.4f;
        }
        private Color Light(Vector3 n){Vector3 s=Vector3.Scale(n,n);return faceLight[n.x>=0?0:1]*s.x+faceLight[n.y>=0?2:3]*s.y+faceLight[n.z>=0?4:5]*s.z;}
        private static Vector4 Uv(float u,float v,float mode,float flags)=>new Vector4(u,v,mode,flags);
        private void Fill(Camera camera)
        {
            var solid=draw.Solid;var layer=draw.Effects;var cam=camera.transform;Vector3 eye=cam.position,right=cam.right,up=cam.up;
            for(int i=0;i<fx.CubeCount;i++)ShadedCube(solid,fx.Cubes[i]);
            // Decals and the crush footprint first, then the dust far to near, then wires, then the additive sparks and glints.
            Color upLight=faceLight[2];
            for(int i=0;i<fx.FlatCount;i++)
            {
                var f=fx.Flats[i];Color c=new Color(f.R,f.G,f.B,f.Alpha);if(f.Lit){c.r*=upLight.r;c.g*=upLight.g;c.b*=upLight.b;}Vector3 o=U(f.Center),a=U(f.U),b=U(f.V);float mode=f.Soft?NativeFxDraw.Radial:NativeFxDraw.Flat;int s=layer.Count;
                layer.Vertex(o-a-b,Vector3.up,c,Uv(-1,-1,mode,Authored),Vector4.zero,Vector4.zero);layer.Vertex(o+a-b,Vector3.up,c,Uv(1,-1,mode,Authored),Vector4.zero,Vector4.zero);
                layer.Vertex(o+a+b,Vector3.up,c,Uv(1,1,mode,Authored),Vector4.zero,Vector4.zero);layer.Vertex(o-a+b,Vector3.up,c,Uv(-1,1,mode,Authored),Vector4.zero,Vector4.zero);layer.Triangle(s,s+1,s+2);layer.Triangle(s,s+2,s+3);
            }
            int n=fx.PuffCount;for(int i=0;i<n;i++){order[i]=i;depth[i]=(U(fx.Puffs[i].Center)-eye).sqrMagnitude;}
            for(int i=1;i<n;i++){int k=order[i];float d=depth[k];int j=i-1;while(j>=0&&depth[order[j]]<d){order[j+1]=order[j];j--;}order[j+1]=k;}
            for(int i=0;i<n;i++)
            {
                var p=fx.Puffs[order[i]];Vector3 c=U(p.Center),w=right*(p.Width*.5f),h=up*(p.Height*.5f);
                Color top=new Color(p.R*topLight.r*p.Top,p.G*topLight.g*p.Top,p.B*topLight.b*p.Top,p.Alpha),bottom=new Color(p.R*bottomLight.r*p.Bottom,p.G*bottomLight.g*p.Bottom,p.B*bottomLight.b*p.Bottom,p.Alpha);int s=layer.Count;
                layer.Vertex(c-w-h,Vector3.up,bottom,Uv(-1,-1,NativeFxDraw.Radial,Authored),Vector4.zero,Vector4.zero);layer.Vertex(c+w-h,Vector3.up,bottom,Uv(1,-1,NativeFxDraw.Radial,Authored),Vector4.zero,Vector4.zero);
                layer.Vertex(c+w+h,Vector3.up,top,Uv(1,1,NativeFxDraw.Radial,Authored),Vector4.zero,Vector4.zero);layer.Vertex(c-w+h,Vector3.up,top,Uv(-1,1,NativeFxDraw.Radial,Authored),Vector4.zero,Vector4.zero);layer.Triangle(s,s+1,s+2);layer.Triangle(s,s+2,s+3);
            }
            for(int pass=0;pass<2;pass++)for(int i=0;i<fx.LineCount;i++){var l=fx.Lines[i];if(l.Additive!=(pass==1))continue;Ribbon(layer,U(l.From),U(l.To),l.Width,new Color(l.R,l.G,l.B,l.Alpha),eye,l.Additive?Glowing:Authored);}
            for(int i=0;i<fx.GlowCount;i++){var g=fx.Glows[i];draw.Sprite(layer,U(g.Center),right,up,g.Size,new Color(g.R,g.G,g.B,g.Alpha),Glowing);}
        }
        private void ShadedCube(NativeFxDraw.Layer layer,in NativeStructureCube c)
        {
            var q=new Quaternion(c.Qx,c.Qy,c.Qz,c.Qw);Vector3 o=U(c.Center),ax=q*new Vector3(c.Size.x*.5f,0,0),ay=q*new Vector3(0,c.Size.y*.5f,0),az=q*new Vector3(0,0,c.Size.z*.5f);Vector4 uv=Uv(0,0,NativeFxDraw.Flat,Authored);
            for(int f=0;f<6;f++)
            {
                Vector3 n=f<2?ax:f<4?ay:az,a=f<2?ay:f<4?az:ax,b=f<2?az:f<4?ax:ay;if(f%2==1)n=-n;Color light=Light(n.normalized),col=new Color(c.R*light.r,c.G*light.g,c.B*light.b,1);int s=layer.Count;Vector3 m=o+n;
                layer.Vertex(m-a-b,n,col,uv,Vector4.zero,Vector4.zero);layer.Vertex(m+a-b,n,col,uv,Vector4.zero,Vector4.zero);layer.Vertex(m+a+b,n,col,uv,Vector4.zero,Vector4.zero);layer.Vertex(m-a+b,n,col,uv,Vector4.zero,Vector4.zero);
                layer.Triangle(s,s+1,s+2);layer.Triangle(s,s+2,s+3);
            }
        }
        // A camera-facing ribbon from a to b.
        private static void Ribbon(NativeFxDraw.Layer layer,Vector3 a,Vector3 b,float width,Color c,Vector3 eye,float flags)
        {
            Vector3 d=b-a,side=Vector3.Cross(d,eye-(a+b)*.5f);if(side.sqrMagnitude<1e-10f)side=Vector3.Cross(d,Vector3.up);if(side.sqrMagnitude<1e-10f)return;side=side.normalized*(width*.5f);
            Vector4 uv=Uv(0,0,NativeFxDraw.Flat,flags);int s=layer.Count;layer.Vertex(a-side,Vector3.up,c,uv,Vector4.zero,Vector4.zero);layer.Vertex(a+side,Vector3.up,c,uv,Vector4.zero,Vector4.zero);
            layer.Vertex(b+side,Vector3.up,c,uv,Vector4.zero,Vector4.zero);layer.Vertex(b-side,Vector3.up,c,uv,Vector4.zero,Vector4.zero);layer.Triangle(s,s+1,s+2);layer.Triangle(s,s+2,s+3);
        }
    }

    public sealed partial class ArenaPresentation
    {
        private NativeStructureFxPresentation structureFx;private bool structureFxFailed;
        /// <summary>The destruction kit (null before the first native arena frame, or if NativeSirenFx.shader is missing).</summary>
        public NativeStructureFxPresentation Structures=>structureFx;
        // Native arenas only (the prototype lab keeps its own burst). Created on the first frame, never in startup checks.
        private void StructureFx(NativeArena arena,Camera camera,float now)
        {
            if(sourceModels==null||arena==null)return;
            if(structureFx==null&&!structureFxFailed){try{structureFx=new NativeStructureFxPresentation{Owner=this};}catch(Exception e){structureFxFailed=true;Debug.LogWarning("Destruction effects disabled: "+e.Message);}}
            if(blasts!=null)blasts.MaterialBursts=structureFx!=null;structureFx?.Render(arena,camera,now,BlastQuality);
        }
        /// <summary>The structure shake offset at <paramref name="eye"/>, times <paramref name="scale"/> (reduced effects .5).</summary>
        public Vector3 StructureShake(Vector3 eye,float scale)=>structureFx==null?Vector3.zero:structureFx.Shake(eye)*scale;
        // keep: the killcam starts or ends (ResetPresentation(keepWorldMarks)): keep the records and decals, re-read the events.
        private void ClearStructureFx(bool keep){if(keep)structureFx?.Reset();else structureFx?.Clear();}
        private void DisposeStructureFx(){structureFx?.Dispose();structureFx=null;structureFxFailed=false;}
    }

    public sealed partial class ConversionGame
    {
        private static readonly string[] StructureMaterials={"masonry","concrete","timber","metal","glass"},StructureSpots={"aim","dummy","ahead"};
        private string structureMaterial="masonry",structureSpot="aim";
        // Dev Sandbox rows (dev accounts, offline sandbox only, like the other Sandbox previews): see the destruction kit before
        // Brickfall exists. The stand-in quadrant plays stress → fall → land with its sounds and the camera shake; Material
        // Bursts break one block of each material. Presentation only: nothing is added to the match or the killcam.
        private void StructureFxRows()
        {
            var kit=presentation?.Structures;if(arena?.Sandbox!=true||!account.Developer||Online||kit==null)return;
            Field("Destruction effects preview");structureMaterial=ChoiceGrid(StructureMaterials,structureMaterial);structureSpot=ChoiceGrid(StructureSpots,structureSpot);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Play Collapse",button)){PreviewCollapse(kit);menu=false;}
            if(GUILayout.Button("Material Bursts",button)){Vector3 spot=StructureSpot();kit.PreviewBursts(arena,spot,Quaternion.Euler(0,yaw,0)*Vector3.right);menu=false;}
            if(GUILayout.Button("Clear Effects",button))kit.Clear();
            GUILayout.EndHorizontal();
        }
        private Vector3 StructureSpot()
        {
            Vector3 me=arena.Local.Body.position,ahead=me+Quaternion.Euler(0,yaw,0)*Vector3.forward*12;
            if(structureSpot=="dummy"){var dummy=arena.Pilots.FirstOrDefault(p=>p.Seat.bot&&p.Hp>0);if(dummy!=null)return dummy.Body.position;}
            return structureSpot=="aim"?AimPoint(60):ahead;
        }
        private void PreviewCollapse(NativeStructureFxPresentation kit)
        {
            var material=(NativeStructureMaterial)Math.Max(0,Array.IndexOf(StructureMaterials,structureMaterial));Vector3 spot=StructureSpot(),away=spot-arena.Local.Body.position;away.y=0;
            kit.PreviewCollapse(arena,spot,away.sqrMagnitude>.01f?away.normalized:Vector3.forward,material);
            // The later cues wait in NativeAudio's queue until they are due, so the voice cap counts each from its start and the
            // collapse takes over this preview's groan.
            Vector3 centroid=spot+Vector3.up*(NativeStructurePreview.Height*.5f);uint owner=NativeStructureSounds.Owner("preview-"+Time.frameCount);
            nativeAudio.StructureCue(NativeStructureSound.Groan,centroid,material,.8f,0,owner);nativeAudio.StructureCue(NativeStructureSound.Collapse,centroid,material,.8f,NativeStructurePreview.Telegraph,owner);
            nativeAudio.StructureCue(NativeStructureSound.Land,spot,material,NativeStructurePreview.Energy,NativeStructurePreview.LandAt,owner);
        }
    }
}
