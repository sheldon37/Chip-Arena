using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Field telegraphs in the arena (source client/chaos-visuals.ts, the Siren floor telegraphs of client/siren/fx.ts and the
    // ability aim preview of client/ability-preview.ts), drawn from the pure NativeFieldRules. Builds, once per frame, the
    // source's FieldViews from chip fields and ability zones; NativeBlastPresentation runs its field emitters (the volumes,
    // strike cues and arrival bursts) from the same views, so both keep one key, birth time and fx per field.
    // Layers, each one dynamic mesh refilled from reused buffers: the Siren scorch decals (Transparent-1, as renderOrder -1),
    // the time/repulsion domes and the Siren warnings (Transparent; fills, then rings). The field cores and the aim preview
    // motes go to the shared effects renderer (NativeVfxDraw; owner 2026-10-11 effects overhaul): the chip bomb, implosion
    // bomb and mine as the lit, modelled bomb (NativeFieldRules.Bomb) with its band and fuse glowing on the fuse pulse and a
    // hot glow on the fuse tip, the implosion's orbiting motes and the black hole's accretion as HDR streaks, its core as a
    // dark lit sphere with a rim and a halo ring, the preview motes as soft glows. Owner 2026-10-11 ("that pancake ring effect
    // is still there"): the source's flat ground glow disc (NativeFieldRules.GlowDisc, alpha .22 out to the gameplay radius
    // under every field for its whole life) is no longer drawn; nothing gameplay reads it, and the field's volume (its
    // NativeBlastSim emitters) shows where it is. Host and guests draw from replicated data. Every view carries the raycast
    // floor under it (NativeBlastField.Floor): the blast emitters sit its arrival impacts on that floor. Cosmetic only.
    public sealed class NativeFieldPresentation : IDisposable
    {
        private sealed class Layer
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};public readonly Material Material;
            public readonly List<Vector3> Vertices=new List<Vector3>();public readonly List<Color> Colors=new List<Color>();public readonly List<Vector2> Uv=new List<Vector2>();public readonly List<int> Indices=new List<int>();
            public Layer(string name,Material material){Mesh.name=name;Mesh.MarkDynamic();Material=material;}
            public void Clear(){Vertices.Clear();Colors.Clear();Uv.Clear();Indices.Clear();}
            public void Draw(Camera camera)
            {
                Mesh.Clear();if(Vertices.Count==0||camera==null)return;
                Mesh.SetVertices(Vertices);Mesh.SetColors(Colors);Mesh.SetUVs(0,Uv);Mesh.SetTriangles(Indices,0,false);Mesh.bounds=Everywhere;
                Graphics.DrawMesh(Mesh,Matrix4x4.identity,Material,0,camera,0,null,ShadowCastingMode.Off,false);
            }
        }
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        /// <summary>This frame's live fields as the source's FieldView (chip fields by id, then ability zones), after <see cref="Collect"/>.</summary>
        public readonly List<NativeBlastField> Views=new List<NativeBlastField>();
        /// <summary>Keys of <see cref="Views"/> (NativeBlastSim.Prune).</summary>
        public readonly HashSet<long> Live=new HashSet<long>();
        private readonly List<int> colors=new List<int>();
        private readonly Dictionary<long,int> colorOf=new Dictionary<long,int>();private readonly Dictionary<long,float> zoneBorn=new Dictionary<long,float>();
        private readonly List<long> stale=new List<long>();
        // The floor under each field and where it was cast from: cast again only when the field moves (attached fields follow
        // their pilot, prop or add), so a static field casts once.
        private const float FloorMove=.02f;private readonly Dictionary<long,(Vector3 at,double floor)> floorOf=new Dictionary<long,(Vector3,double)>();private readonly RaycastHit[] floorHits=new RaycastHit[16];
        private readonly Layer decal,dome,warn;private readonly Layer[] layers;private readonly Material[] owned;
        private readonly NativeVfxDraw vfx;private readonly bool ownsVfx;
        private readonly NativeFieldCube[] cubes=new NativeFieldCube[NativeFieldRules.MaxCubes],before=new NativeFieldCube[NativeFieldRules.MaxCubes];private readonly float[] preview=new float[NativeFieldRules.PreviewCap*4];
        private int generation=-1;private float lastTime=-1;private bool linear;private NativeArena lastArena;
        /// <summary>The clock of the last <see cref="Collect"/>; NativeBlastPresentation runs the field emitters on it.</summary>
        public float Clock{get;private set;}
        private string previewSpec;private ContentAbility previewAbility;
        /// <summary>vfx: the shared effects renderer the owner (ArenaPresentation) begins and submits each frame; null makes
        /// this presentation own one and submit it at the end of its own Render.</summary>
        public NativeFieldPresentation(NativeVfxDraw vfx=null)
        {
            ownsVfx=vfx==null;this.vfx=vfx??new NativeVfxDraw();
            var shader=Resources.Load<Shader>("NativeFieldGlow");if(shader==null)throw new InvalidOperationException("NativeFieldGlow shader is missing");
            Material Transparent(string name,int queue,CullMode cull,float offset=0,float radial=0)
            {var m=new Material(shader){name=name,renderQueue=queue};m.SetFloat("_Cull",(float)cull);m.SetFloat("_Offset",offset);m.SetFloat("_Radial",radial);return m;}
            int t=(int)RenderQueue.Transparent;
            // Source draw order: decals (renderOrder -1), dome and warning ring (0); the effects renderer's sprites and puffs share
            // the Transparent queue, sorted with the domes by their batch's bounds, and NativeCombatGlow's halos are Transparent+10.
            var decalMaterial=Transparent("Native Siren scorch",t-1,CullMode.Back,-2,(float)NativeFieldRules.ScorchFalloff);
            var domeMaterial=Transparent("Native field dome",t,CullMode.Back);var warnMaterial=Transparent("Native Siren warning",t,CullMode.Off);
            decal=new Layer("Native Siren scorch",decalMaterial);dome=new Layer("Native field dome",domeMaterial);warn=new Layer("Native Siren warning",warnMaterial);
            layers=new[]{decal,dome,warn};owned=new[]{decalMaterial,domeMaterial,warnMaterial};
        }
        public void Clear()
        {
            Views.Clear();Live.Clear();colors.Clear();colorOf.Clear();zoneBorn.Clear();floorOf.Clear();lastTime=-1;lastArena=null;
            foreach(var layer in layers){layer.Clear();layer.Mesh.Clear();}if(ownsVfx)vfx.Clear();
        }
        // A zone's replicated Id names it (meteor-strike's burning ground gets a fresh one, a new field exactly as in the
        // source). A zone without one (Id 0) is named by its owner, ability, kind and expiry, fixed for its life.
        private static long Mix(long h,int v){unchecked{h^=(uint)v;h*=0x100000001b3L;return h;}}
        private static long ZoneKey(NativeAbilityZone z)
        {if(z.Id!=0)return (1L<<62)|(uint)z.Id;long h=Mix(unchecked((long)0x9e3779b97f4a7c15UL),z.Owner?.Seat.id??0);h=Mix(h,z.Definition?.id?.GetHashCode()??0);h=Mix(h,z.Kind?.GetHashCode()??0);h=Mix(h,BitConverter.SingleToInt32Bits(z.Until));return h|(1L<<62);}
        private static string Fx(ContentAbility d)=>string.IsNullOrEmpty(d.fx)?null:d.fx;
        /// <summary>The chip id that made a field (the source's fx), replicated in the `chip` effect frame. A JsonUtility
        /// round trip turns an absent one into "", which is no fx (the field then styles by its kind).</summary>
        public string Fx(NativeChipField f)=>string.IsNullOrEmpty(f.Effect)?null:f.Effect;
        private void Add(NativeBlastField v,NativePilot owner,NativePilot viewer,bool pve,bool teams)
        {
            Live.Add(v.Id);
            // The colour is picked when the field is first drawn and kept for its life (chaos-visuals.ts).
            if(!colorOf.TryGetValue(v.Id,out int color))
            {
                bool self=owner!=null&&viewer!=null&&owner.Seat.id==viewer.Seat.id,same=owner!=null&&viewer!=null&&owner.Seat.team==viewer.Seat.team;
                color=NativeFieldRules.Color(v.Kind,v.Fx,viewer!=null,NativeFieldRules.IsAlly(self,pve,teams,same));colorOf[v.Id]=color;
            }
            Views.Add(v);colors.Add(color);
        }
        private static bool Has(NativePilot p,string chip)=>p!=null&&p.Seat.chips.TryGetValue(chip,out int count)&&count>0;
        private bool world;private PhysicsScene physics;
        // The floor under a field (NativeBlastPresentation.FloorUnder); null (the source's floor) without a physics scene.
        private double? FloorOf(long id,Vector3 at)
        {
            if(!world)return null;if(floorOf.TryGetValue(id,out var known)&&(known.at-at).sqrMagnitude<FloorMove*FloorMove)return known.floor;
            double floor=NativeBlastPresentation.FloorUnder(physics,at,floorHits);floorOf[id]=(at,floor);return floor;
        }
        /// <summary>
        /// Builds this frame's field views. now is the field clock (ArenaPresentation.FieldClock): the presentation clock on the
        /// host, the continuous snapshot clock on guests, so the telegraphs animate every rendered frame.
        /// </summary>
        public void Collect(NativeArena arena,float now)
        {
            // A rewind (killcam, new round) or another arena (the replay's) clears: field ids and colours are per arena.
            if(arena!=lastArena||generation!=arena.Generation||now<lastTime-.25f){Clear();generation=arena.Generation;lastArena=arena;}
            lastTime=Math.Max(lastTime,now);Clock=now;Views.Clear();colors.Clear();Live.Clear();
            // arena.Local, not ViewPilot: Leet mind control does not flip the tint. Dedicated hosts have no local pilot.
            var viewer=arena.Local;bool pve=arena.Pve!=null,teams=arena.Match.Teams;world=NativeBlastPresentation.World(arena,out physics);
            var fields=arena.ChipFields;
            for(int i=0;i<fields.Count;i++)
            {
                var f=fields[i];
                Add(new NativeBlastField{Id=f.Id,Kind=f.Kind,Fx=Fx(f),X=f.Position.x,Y=f.Position.y,Z=f.Position.z,Radius=f.Radius,Born=f.Born,Until=f.Until,Floor=FloorOf(f.Id,f.Position)},f.Owner,viewer,pve,teams);
            }
            var zones=arena.AbilityZones;
            for(int i=0;i<zones.Count;i++)
            {
                var z=zones[i];if(z.Definition==null)continue;long key=ZoneKey(z);
                if(!zoneBorn.TryGetValue(key,out float born))
                {
                    // A zone whose expiry was just cut to now (a newer cast replaced its group) hashes to a new key for its
                    // last frame; it is not a new field, so it gets no glow flash and no arrival burst.
                    if(z.Until<=arena.Time)continue;
                    // The replicated birth; a zone without one (Born 0) falls back to its expiry (NativeFieldRules.ZoneBorn).
                    var d=z.Definition;
                    zoneBorn[key]=born=z.Born>0?z.Born:(float)NativeFieldRules.ZoneBorn(d.id,z.Kind,z.Until,d.duration,d.fuse,Has(z.Owner,"cinder-field"),now);
                }
                // The mark locks when its fuse has duration left to run (NativeAbilities orbital-mark: LockAt=born+duration, Until=born+fuse).
                double? lockAt=z.Definition.id=="orbital-mark"?z.Until-z.Definition.fuse+z.Definition.duration:(double?)null;
                Add(new NativeBlastField{Id=key,Kind=z.Kind,Fx=Fx(z.Definition),X=z.Position.x,Y=z.Position.y,Z=z.Position.z,Radius=z.Radius,Born=born,Until=z.Until,LockAt=lockAt,Floor=FloorOf(key,z.Position)},z.Owner,viewer,pve,teams);
            }
            stale.Clear();foreach(var key in zoneBorn.Keys)if(!Live.Contains(key))stale.Add(key);foreach(var key in stale)zoneBorn.Remove(key);
            stale.Clear();foreach(var key in colorOf.Keys)if(!Live.Contains(key))stale.Add(key);foreach(var key in stale)colorOf.Remove(key);
            stale.Clear();foreach(var key in floorOf.Keys)if(!Live.Contains(key))stale.Add(key);foreach(var key in stale)floorOf.Remove(key);
        }
        // Vertex colours are not colour-space converted: write linear values under Linear colour space, sRGB under Gamma.
        private Color Vertex(int hex,float alpha){var c=NativeArena.ColorOf(hex);if(linear)c=c.linear;c.a=alpha;return c;}
        // A Siren glow colour that must reach the screen as the given sRGB value (NativeFieldRules.SirenDisplay).
        private Color Display(int hex,double intensity,float alpha)
        {NativeFieldRules.SirenDisplay(hex,intensity,out double r,out double g,out double b);var c=new Color((float)r,(float)g,(float)b);if(linear)c=c.linear;c.a=alpha;return c;}
        private static void Append(Layer layer,NativeFieldMesh mesh,Vector3 at,float scale,Color color,bool uv=false)
        {
            int start=layer.Vertices.Count;var p=mesh.Positions;
            for(int i=0;i<mesh.Count;i++)
            {
                layer.Vertices.Add(new Vector3(at.x+p[i*3]*scale,at.y+p[i*3+1]*scale,at.z+p[i*3+2]*scale));
                var c=color;c.a*=mesh.Alpha[i];layer.Colors.Add(c);layer.Uv.Add(uv?new Vector2(p[i*3],p[i*3+2]):Vector2.zero);
            }
            var t=mesh.Triangles;for(int i=0;i<t.Length;i++)layer.Indices.Add(start+t[i]);
        }
        /// <summary>
        /// Draws the views from the last <see cref="Collect"/>, the Siren floor telegraphs and the aim preview. preview is the
        /// local E aim point (null when not aiming); clamped: the aim was out of range or clamped to the arena.
        /// </summary>
        public void Render(NativeArena arena,Camera camera,float now,Vector3? aim,bool clamped)
        {
            linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            foreach(var layer in layers)layer.Clear();if(ownsVfx)vfx.Begin();
            for(int i=0;i<Views.Count;i++)
            {
                var v=Views[i];int hex=colors[i];float R=(float)v.Radius;
                // No flat ground glow disc under any field (owner 2026-10-11: the "pancake"); the dome looks keep their dome.
                var look=NativeFieldRules.LookOf(v.Kind,v.Fx);
                if(look==NativeFieldLook.Dome){Append(dome,NativeFieldRules.Dome,new Vector3((float)v.X,(float)NativeFieldRules.Origin(v.Y),(float)v.Z),R,Vertex(hex,(float)NativeFieldRules.DomeAlpha));continue;}
                if(look!=NativeFieldLook.Glow)Cores(v,hex,now,look);
            }
            SirenTelegraphs(arena,now);
            if(aim.HasValue)Preview(arena,aim.Value,clamped,now);
            foreach(var layer in layers)layer.Draw(camera);
            if(ownsVfx)vfx.Submit(camera);
        }
        // Seconds between the two core plans that give the orbiting motes their velocity (for their streaks).
        private const float OrbitStep=1/60f;
        private static Vector3 At(in NativeFieldCube c)=>new Vector3((float)c.X,(float)c.Y,(float)c.Z);
        // The field cores (ChaosVisuals.update's cubes, NativeFieldRules.Cubes, keep the timing): the chip bomb, implosion bomb
        // and mine as the modelled bomb; the implosion's six orbiting cubes and the black hole's ten accretion cubes as HDR
        // streaks along their orbit (the planner's last six or ten, at now and a step before); the black hole's dark core as a
        // lit sphere with a rim in the field's colour and a soft halo ring.
        private void Cores(in NativeBlastField v,int hex,float now,NativeFieldLook look)
        {
            int n=NativeFieldRules.Cubes(v,hex,now,cubes),m=NativeFieldRules.Cubes(v,hex,now-OrbitStep,before),orbit=look==NativeFieldLook.Implosion?6:look==NativeFieldLook.BlackHole?10:0;
            var glow=NativeVfxDraw.Linear(hex);
            if(NativeFieldRules.Bomb(v,now,out var pose))vfx.Bomb(pose,hex);
            else if(look==NativeFieldLook.BlackHole&&n>0)
            {
                var core=At(cubes[0]);
                vfx.Sphere(core,(float)cubes[0].Sx*.5f,new Color(.004f,.002f,.008f,0),glow*2.4f,Color.clear);
                vfx.Glow(core,glow*.55f,(float)cubes[0].Sx*2.4f,NativeVfxDraw.Ring);
            }
            if(orbit==0||n<orbit||m<orbit)return;
            for(int j=0;j<orbit;j++)
            {
                var c=cubes[n-orbit+j];var at=At(c);var velocity=(at-At(before[m-orbit+j]))/OrbitStep;
                vfx.Streak(at,velocity,NativeVfxDraw.Linear(c.Color)*2.2f,(float)c.Sx*1.2f,(float)c.Sx,.06f,.9f);
            }
        }
        // siren/fx.ts: a black scorch decal under every fire patch; an eruption's pulsing warning ring and the dark fill that
        // grows to its radius until the burst. The black flames, the eruption column and the rain stay with C12.
        private void SirenTelegraphs(NativeArena arena,float now)
        {
            var list=arena.SirenFields;Color scorch=Vertex(NativeFieldRules.Scorch,(float)NativeFieldRules.ScorchAlpha),fill=Display(NativeFieldRules.WarnDeep,1,(float)NativeFieldRules.FillAlpha);
            Color ringColor=Display(NativeFieldRules.Warn,NativeFieldRules.Flicker(now),(float)NativeFieldRules.WarnAlpha);
            // The warning layer is one mesh, drawn in index order: every fill first, then every ring. In the source they are
            // separate meshes that three.js depth-sorts, and the ring sits 1 cm above its fill, so the ring lands on top once
            // the fill grows under it (the last ~0.1 s before the burst).
            for(int pass=0;pass<2;pass++)
                for(int i=0;i<list.Count;i++)
                {
                    var f=list[i];Vector3 at=f.Position;
                    if(NativeFieldRules.SirenPatch(f.Kind)){if(pass==0&&NativeFieldRules.ScorchDecal(f.At,f.Until,f.Radius,now,out double scale))Append(decal,NativeFieldRules.SirenDisc,at+Vector3.up*(float)NativeFieldRules.ScorchLift,(float)scale,scorch,true);}
                    else if(f.Kind=="eruption"&&NativeFieldRules.EruptionWarn(f.At,f.At+NativeFieldRules.EruptionWarning,f.Radius,now,out double ring,out double grown))
                    {
                        if(pass==0)Append(warn,NativeFieldRules.SirenDisc,at+Vector3.up*(float)NativeFieldRules.FillLift,(float)grown,fill);
                        else Append(warn,NativeFieldRules.WarnRing,at+Vector3.up*(float)NativeFieldRules.RingLift,(float)ring,ringColor);
                    }
                }
        }
        // ability-preview.ts: motes rising on the gameplay radius of a point-targeted E ability (Geyser Wall: three circles 3 m
        // apart across the aim, as NativeAbilities places them; Singularity: x1.25 with Abyssal Core).
        private void Preview(NativeArena arena,Vector3 point,bool clamped,float now)
        {
            var p=arena.ViewPilot;if(p==null)return;
            if(previewSpec!=p.Seat.spec){previewSpec=p.Seat.spec;previewAbility=arena.Content.Ability(p.Seat.spec,"e");}
            var d=previewAbility;if(d==null||!NativeFieldRules.Previews(d.id,out _))return;
            float radius=NativeAbilityRules.Radius(d,p.Stats);if(d.id=="singularity"&&p.Seat.chips.TryGetValue("abyssal-core",out int core)&&core>0)radius*=1.25f;
            int used=0;
            if(d.id=="geyser-wall")
            {
                Vector3 side=Vector3.Cross(NativeArena.Vec(GunRules.Direction(p.Yaw,p.Pitch)),Vector3.up).normalized;
                for(int k=-1;k<=1;k++){Vector3 at=point+side*(float)(NativeFieldRules.GeyserSpacing*k);if(arena.PhysicsWorld.Raycast(at+Vector3.up*2,Vector3.down,out var hit,6,NativeArena.WorldMask))at=hit.point;used=NativeFieldRules.PreviewMotes(at.x,at.y,at.z,radius,clamped,now,preview,used);}
            }
            else used=NativeFieldRules.PreviewMotes(point.x,point.y,point.z,radius,clamped,now,preview,used);
            // Soft glows in place of the source's translucent cubes (owner 2026-10-11 effects overhaul), as bright as their opacity.
            Color color=NativeVfxDraw.Linear(NativeFieldRules.PreviewColor)*(float)(NativeFieldRules.PreviewAlpha*1.6);float size=(float)NativeFieldRules.PreviewSize*2.6f;
            for(int i=0;i<used;i++){int w=i*4;vfx.Glow(new Vector3(preview[w],preview[w+1],preview[w+2]),color,size*preview[w+3],NativeVfxDraw.Mote);}
        }
        public void Dispose()
        {
            Clear();foreach(var layer in layers)CapturedAnimation.Release(layer.Mesh);foreach(var m in owned)CapturedAnimation.Release(m);if(ownsVfx)vfx.Dispose();
        }
    }
}
