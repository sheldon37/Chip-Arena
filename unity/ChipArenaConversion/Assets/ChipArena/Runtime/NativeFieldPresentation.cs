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
    // Layers, each one dynamic mesh refilled from reused buffers: the edge-less ground glow (Transparent-1, as renderOrder
    // -1) and the Siren scorch decals, the time/repulsion domes and the Siren warnings (Transparent; fills, then rings), the
    // aim preview motes (Transparent+3) and the opaque unlit cores. Host and guests draw from replicated data. Cosmetic only.
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
        private readonly Dictionary<int,string> guestFx=new Dictionary<int,string>();private readonly List<long> stale=new List<long>();private readonly List<int> staleIds=new List<int>();
        private readonly Layer glow,decal,dome,warn,motes,cores;private readonly Layer[] layers;private readonly Material[] owned;
        private readonly NativeFieldCube[] cubes=new NativeFieldCube[NativeFieldRules.MaxCubes];private readonly float[] preview=new float[NativeFieldRules.PreviewCap*4];
        private int generation=-1;private float lastTime=-1;private bool linear;private NativeArena lastArena;
        /// <summary>The clock of the last <see cref="Collect"/>; NativeBlastPresentation runs the field emitters on it.</summary>
        public float Clock{get;private set;}
        private string previewSpec;private ContentAbility previewAbility;
        public NativeFieldPresentation()
        {
            var shader=Resources.Load<Shader>("NativeFieldGlow");if(shader==null)throw new InvalidOperationException("NativeFieldGlow shader is missing");
            var blast=Resources.Load<Shader>("NativeBlast");if(blast==null)throw new InvalidOperationException("NativeBlast shader is missing");
            Material Transparent(string name,int queue,CullMode cull,float offset=0,float radial=0)
            {var m=new Material(shader){name=name,renderQueue=queue};m.SetFloat("_Cull",(float)cull);m.SetFloat("_Offset",offset);m.SetFloat("_Radial",radial);return m;}
            int t=(int)RenderQueue.Transparent;
            // Source draw order: glow disc and decals (renderOrder -1), dome and warning ring (0), preview motes (3); the additive
            // blast glow is Transparent+3 and NativeCombatGlow's halos Transparent+10.
            var glowMaterial=Transparent("Native field glow",t-1,CullMode.Back);var decalMaterial=Transparent("Native Siren scorch",t-1,CullMode.Back,-2,(float)NativeFieldRules.ScorchFalloff);
            var domeMaterial=Transparent("Native field dome",t,CullMode.Back);var warnMaterial=Transparent("Native Siren warning",t,CullMode.Off);var moteMaterial=Transparent("Native aim preview",t+3,CullMode.Back);
            var coreMaterial=new Material(blast){name="Native field cores",renderQueue=(int)RenderQueue.Geometry};
            coreMaterial.SetFloat("_Src",(float)BlendMode.One);coreMaterial.SetFloat("_Dst",(float)BlendMode.Zero);coreMaterial.SetFloat("_ZWrite",1);coreMaterial.SetFloat("_Additive",0);
            glow=new Layer("Native field glow",glowMaterial);decal=new Layer("Native Siren scorch",decalMaterial);dome=new Layer("Native field dome",domeMaterial);
            warn=new Layer("Native Siren warning",warnMaterial);motes=new Layer("Native aim preview",moteMaterial);cores=new Layer("Native field cores",coreMaterial);
            layers=new[]{glow,decal,dome,warn,motes,cores};owned=new[]{glowMaterial,decalMaterial,domeMaterial,warnMaterial,moteMaterial,coreMaterial};
        }
        public void Clear()
        {
            Views.Clear();Live.Clear();colors.Clear();colorOf.Clear();zoneBorn.Clear();guestFx.Clear();lastTime=-1;lastArena=null;
            foreach(var layer in layers){layer.Clear();layer.Mesh.Clear();}
        }
        // Zones carry no id; owner, ability, kind and expiry stay fixed for a zone's life on host and guests (meteor-strike's
        // burning ground changes kind and expiry, which makes it a new field exactly as in the source).
        private static long Mix(long h,int v){unchecked{h^=(uint)v;h*=0x100000001b3L;return h;}}
        private static long ZoneKey(NativeAbilityZone z)
        {long h=Mix(unchecked((long)0x9e3779b97f4a7c15UL),z.Owner?.Seat.id??0);h=Mix(h,z.Definition?.id?.GetHashCode()??0);h=Mix(h,z.Kind?.GetHashCode()??0);h=Mix(h,BitConverter.SingleToInt32Bits(z.Until));return h|(1L<<62);}
        private static string Fx(ContentAbility d)=>string.IsNullOrEmpty(d.fx)?null:d.fx;
        /// <summary>The chip id that made a field (the source's fx). Guests recover it from the field's own Field event.</summary>
        public string Fx(NativeChipField f)=>!string.IsNullOrEmpty(f.Effect)?f.Effect:guestFx.TryGetValue(f.Id,out var fx)?fx:null;
        private string Effect(NativeArena arena,NativeChipField f)
        {
            if(!string.IsNullOrEmpty(f.Effect))return f.Effect;
            if(guestFx.TryGetValue(f.Id,out var fx))return fx;
            // NativeArena.Field numbers the field, then emits its Field event (Detail = chip id) with the next number. The
            // effect frame does not carry Effect yet (needs Astra); a guest that missed the event falls back to the kind.
            var events=arena.Events;
            for(int i=events.Count-1;i>=0;i--){var e=events[i];if(e.Kind==EventKind.Field&&e.Id==f.Id+1&&Math.Abs(e.Time-f.Born)<.01f){fx=string.IsNullOrEmpty(e.Detail)?null:e.Detail;break;}}
            guestFx[f.Id]=fx;return fx;
        }
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
            var viewer=arena.Local;bool pve=arena.Pve!=null,teams=arena.Match.Teams;
            var fields=arena.ChipFields;
            for(int i=0;i<fields.Count;i++)
            {
                var f=fields[i];
                Add(new NativeBlastField{Id=f.Id,Kind=f.Kind,Fx=Effect(arena,f),X=f.Position.x,Y=f.Position.y,Z=f.Position.z,Radius=f.Radius,Born=f.Born,Until=f.Until},f.Owner,viewer,pve,teams);
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
                    // Exact where the expiry gives it (Singularity, burning ground, Orbital Mark); bombs no later than their fuse.
                    var d=z.Definition;
                    zoneBorn[key]=born=(float)NativeFieldRules.ZoneBorn(d.id,z.Kind,z.Until,d.duration,d.fuse,Has(z.Owner,"cinder-field"),now);
                }
                // The mark locks when its fuse has duration left to run (NativeAbilities orbital-mark: LockAt=born+duration, Until=born+fuse).
                double? lockAt=z.Definition.id=="orbital-mark"?z.Until-z.Definition.fuse+z.Definition.duration:(double?)null;
                Add(new NativeBlastField{Id=key,Kind=z.Kind,Fx=Fx(z.Definition),X=z.Position.x,Y=z.Position.y,Z=z.Position.z,Radius=z.Radius,Born=born,Until=z.Until,LockAt=lockAt},z.Owner,viewer,pve,teams);
            }
            stale.Clear();foreach(var key in zoneBorn.Keys)if(!Live.Contains(key))stale.Add(key);foreach(var key in stale)zoneBorn.Remove(key);
            stale.Clear();foreach(var key in colorOf.Keys)if(!Live.Contains(key))stale.Add(key);foreach(var key in stale)colorOf.Remove(key);
            staleIds.Clear();foreach(var id in guestFx.Keys)if(!Live.Contains(id))staleIds.Add(id);foreach(var id in staleIds)guestFx.Remove(id);
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
        private static void Cube(Layer layer,Vector3 center,Quaternion rotation,Vector3 half,Color color)
        {
            int start=layer.Vertices.Count;var corners=NativeBlastSim.CubeCorners;
            for(int c=0;c<8;c++){layer.Vertices.Add(center+rotation*new Vector3(corners[c*3]*half.x,corners[c*3+1]*half.y,corners[c*3+2]*half.z));layer.Colors.Add(color);layer.Uv.Add(Vector2.zero);}
            foreach(int t in NativeBlastSim.CubeTriangles)layer.Indices.Add(start+t);
        }
        /// <summary>
        /// Draws the views from the last <see cref="Collect"/>, the Siren floor telegraphs and the aim preview. preview is the
        /// local E aim point (null when not aiming); clamped: the aim was out of range or clamped to the arena.
        /// </summary>
        public void Render(NativeArena arena,Camera camera,float now,Vector3? aim,bool clamped)
        {
            linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            foreach(var layer in layers)layer.Clear();
            for(int i=0;i<Views.Count;i++)
            {
                var v=Views[i];int hex=colors[i];float R=(float)v.Radius;
                // Every field: the soft ground glow, alpha .22 at the centre falling linearly to exactly 0 at the gameplay radius.
                Append(glow,NativeFieldRules.GlowDisc,new Vector3((float)v.X,(float)NativeFieldRules.GlowY(v.Y),(float)v.Z),R,Vertex(hex,(float)NativeFieldRules.GlowAlpha));
                if(NativeFieldRules.LookOf(v.Kind,v.Fx)==NativeFieldLook.Dome){Append(dome,NativeFieldRules.Dome,new Vector3((float)v.X,(float)NativeFieldRules.Origin(v.Y),(float)v.Z),R,Vertex(hex,(float)NativeFieldRules.DomeAlpha));continue;}
                int n=NativeFieldRules.Cubes(v,hex,now,cubes);
                for(int c=0;c<n;c++){var q=cubes[c];Cube(cores,new Vector3((float)q.X,(float)q.Y,(float)q.Z),new Quaternion((float)q.Qx,(float)q.Qy,(float)q.Qz,(float)q.Qw),new Vector3((float)q.Sx,(float)q.Sy,(float)q.Sz)*.5f,Vertex(q.Color,1));}
            }
            SirenTelegraphs(arena,now);
            if(aim.HasValue)Preview(arena,aim.Value,clamped,now);
            foreach(var layer in layers)layer.Draw(camera);
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
                for(int k=-1;k<=1;k++){Vector3 at=point+side*(float)(NativeFieldRules.GeyserSpacing*k);if(Physics.Raycast(at+Vector3.up*2,Vector3.down,out var hit,6,NativeArena.WorldMask))at=hit.point;used=NativeFieldRules.PreviewMotes(at.x,at.y,at.z,radius,clamped,now,preview,used);}
            }
            else used=NativeFieldRules.PreviewMotes(point.x,point.y,point.z,radius,clamped,now,preview,used);
            Color color=Vertex(NativeFieldRules.PreviewColor,(float)NativeFieldRules.PreviewAlpha);float half=(float)NativeFieldRules.PreviewSize*.5f;
            for(int i=0;i<used;i++){int w=i*4;Cube(motes,new Vector3(preview[w],preview[w+1],preview[w+2]),Quaternion.identity,Vector3.one*(half*preview[w+3]),color);}
        }
        public void Dispose()
        {
            Clear();foreach(var layer in layers)CapturedAnimation.Release(layer.Mesh);foreach(var m in owned)CapturedAnimation.Release(m);
        }
    }
}
