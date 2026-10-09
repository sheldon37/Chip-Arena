using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What the projectile visuals read from the arena. The host has each shot's own data. A replica (guest, killcam)
    /// receives only id, owner, position, velocity and radius for bullets (Snapshot.Shots) and ability shots, so it falls
    /// back to the owner's seat chips and replicated stats (see the Astra notes in docs/UNITY-PARITY-GAPS.md C10). Each
    /// view keeps the identity its id had when first seen (NativeShotTracker): a reflect changes only the owner, as in the
    /// source. Allocation free: views are structs and seats are found by a plain loop.
    /// </summary>
    public static class NativeShotViews
    {
        internal static NativeSeat Seat(NativeArena arena,int id){var seats=arena.Match.Seats;for(int i=0;i<seats.Count;i++)if(seats[i].id==id)return seats[i];return null;}
        internal static ContentWeapon Weapon(NativeArena arena,string spec){if(spec==null)return null;var all=arena.Content.weapons;for(int i=0;i<all.Length;i++)if(all[i].id==spec)return all[i];return null;}
        private static int Count(Dictionary<string,int> chips,string id)=>chips!=null&&chips.TryGetValue(id,out int n)?n:0;
        // burn and poison are base stats, so every pilot's ChipStats has them.
        private static double Stat(NativePilot p,string name)=>p?.Stats==null?0:p.Stats[name];
        private static long Mix(string id,int count){unchecked{ulong h=(uint)id.GetHashCode()|(ulong)(uint)count<<32;h^=h>>33;h*=0xff51afd7ed558ccdUL;h^=h>>33;h*=0xc4ceb9fe1a85ec53UL;h^=h>>33;return (long)h;}}
        // A replica has no Hadoken flag; there every ansatsuken bullet is taken for one (Blade Runner's beam excepted on the host).
        private static bool Hadouken(NativeArena arena,NativeBullet b)=>arena.Replica?b.Spec=="ansatsuken":b.Hadoken;
        // Blade Runner's beam is the only other melee-class bullet (NativeChips sword-beam; source mods {'sword-beam':1}).
        private static bool SwordBeam(ContentWeapon w,bool hadouken)=>w!=null&&w.@class=="melee"&&!hadouken;
        // The seat that fired the bullet: Original is set at spawn and kept on a reflect (Owner becomes the reflector). A
        // replica bullet carries only its owner.
        private static int Firer(NativeBullet b)=>b.Original!=0?b.Original:b.Owner;
        /// <summary>
        /// The chip stacks a bullet carries (source p.mods): the host's copy of the firer's chips; the firer's seat chips for
        /// a hadouken (Unity leaves its Mods empty; arcade.ts copies them) and on a replica; none for the sword beam.
        /// </summary>
        public static Dictionary<string,int> BulletMods(NativeArena arena,NativeBullet b)
        {
            bool hadouken=Hadouken(arena,b);if(SwordBeam(Weapon(arena,b.Spec),hadouken))return null;
            return arena.Replica||hadouken?Seat(arena,Firer(b))?.chips:b.Mods;
        }
        /// <summary>
        /// One bullet this frame: its live motion with the identity it had when first seen; on a replica, moved to the
        /// snapshot render clock (NativeShotTracker.Present).
        /// </summary>
        public static void Bullet(NativeArena arena,NativeBullet b,NativeShotTracker tracker,double clock,out NativeShotView v)
        {
            v=new NativeShotView{Kind=NativeShotKind.Bullet,Id=b.Id,X=b.Position.x,Y=b.Position.y,Z=b.Position.z,VX=b.Velocity.x,VY=b.Velocity.y,VZ=b.Velocity.z,Radius=b.Radius,FuseLeft=1e9};
            if(!tracker.Recall(ref v)){BulletIdentity(arena,b,ref v,tracker.ModuleCache);tracker.Remember(v);}
            tracker.Present(ref v,clock);
        }
        private static void BulletIdentity(NativeArena arena,NativeBullet b,ref NativeShotView v,Dictionary<long,NativeShotModules> cache)
        {
            var w=Weapon(arena,b.Spec);v.Spec=b.Spec;v.Class=w?.@class;v.WeaponColor=w!=null?(int)w.color:NativeShotRules.White;
            // Siren shots are drawn by the Siren's own effects in the source (sirenEffects.owns); they keep a placeholder (C12).
            if(b.SirenKind!=null||b.Spec=="siren"){v.Siren=true;return;}
            bool hadouken=Hadouken(arena,b);int firer=Firer(b);
            if(hadouken)v.Arcade=Count(Seat(arena,firer)?.chips,"arcade-red")>0?1:2;
            v.SwordBeam=SwordBeam(w,hadouken);
            var mods=BulletMods(arena,b);v.VelocityChips=Count(mods,"velocity");v.Bowling=w!=null&&w.@class=="ranged"&&Count(mods,"bowling-ball")>0;
            if(!arena.Replica){v.Fire=b.Burn>0;v.Poison=b.Poison>0;}
            else if(!v.SwordBeam)
            {
                // attackElements(w.burn + stats.burn, stats.poison) from the firer's replicated stats; a hadouken always burns.
                var owner=Owner(arena,firer);
                v.Fire=hadouken||(w?.burn??0)+Stat(owner,"burn")>0;v.Poison=Stat(owner,"poison")>0;
            }
            v.Modules=Modules(arena,mods,cache);
        }
        public static void Ability(NativeArena arena,NativeAbilityShot s,NativeShotTracker tracker,double clock,out NativeShotView v)
        {
            // The frag fuse is host data (NativeAbilities); a replica has none and shows no blink.
            v=new NativeShotView{Kind=NativeShotKind.Ability,Id=s.Id,X=s.Position.x,Y=s.Position.y,Z=s.Position.z,VX=s.Velocity.x,VY=s.Velocity.y,VZ=s.Velocity.z,Radius=s.Radius,FuseLeft=s.Fuse>0?s.Fuse-arena.Time:1e9};
            if(!tracker.Recall(ref v))
            {
                // The caster's spec, class and weapon colour (source p.spec): s.Owner becomes the reflector on a block, so they
                // are read the first time the id is seen only.
                var owner=s.Owner;var w=owner?.Weapon??Weapon(arena,owner?.Seat?.spec);string id=s.Definition?.id;
                v.Spec=owner?.Seat?.spec;v.Class=w?.@class;v.WeaponColor=w!=null?(int)w.color:NativeShotRules.White;v.Ability=id;v.Fire=id=="flame-lance";
                tracker.Remember(v);
            }
            tracker.Present(ref v,clock);
        }
        public static void Melee(NativeArena arena,NativeMeleeShot s,NativeShotTracker tracker,double clock,out NativeShotView v)
        {
            v=new NativeShotView{Kind=NativeShotKind.Melee,Id=s.Id,X=s.Position.x,Y=s.Position.y,Z=s.Position.z,VX=s.Velocity.x,VY=s.Velocity.y,VZ=s.Velocity.z,Radius=s.Radius,FuseLeft=1e9};
            if(!tracker.Recall(ref v))
            {
                var original=s.Original??s.Owner;var w=Weapon(arena,s.Spec)??original?.Weapon;
                v.Spec=s.Spec??original?.Seat?.spec;v.Class=w?.@class;v.WeaponColor=w!=null?(int)w.color:NativeShotRules.White;v.Shield=s.Shield;
                if(!arena.Replica){v.Fire=s.Burn>0;v.Poison=s.Poison>0;}else{v.Fire=Stat(original,"burn")>0;v.Poison=Stat(original,"poison")>0;}
                v.Modules=Modules(arena,original?.Seat?.chips,tracker.ModuleCache);tracker.Remember(v);
            }
            tracker.Present(ref v,clock);
        }
        internal static NativePilot Owner(NativeArena arena,int seat){var all=arena.Pilots;for(int i=0;i<all.Count;i++)if(all[i].Seat.id==seat)return all[i];return null;}
        /// <summary>The module inputs: statsFor(mods) values chip-effects.ts reads. Cached per distinct chip set.</summary>
        public static NativeShotModules Modules(NativeArena arena,Dictionary<string,int> mods,Dictionary<long,NativeShotModules> cache)
        {
            if(mods==null||mods.Count==0)return default;
            long key=17;foreach(var pair in mods)if(pair.Value!=0)unchecked{key+=Mix(pair.Key,pair.Value);}
            if(cache.TryGetValue(key,out var m))return m;
            m=NativeShotRules.ModulesOf(arena.Content,mods);if(cache.Count>256)cache.Clear();cache[key]=m;return m;
        }
    }

    /// <summary>
    /// Draws the pure NativeShotScene (source projectile models, chip modules, sparks and beams) in the arena. Opaque parts
    /// and the voxel sparks go into one dynamic unlit NativeBlast mesh and the lit beams into one NativeSourceLit mesh,
    /// all refilled from reused buffers each frame. Each translucent part (the tidal wave) is its own draw of a cached mesh
    /// at its own transform, so Unity sorts it among the other transparent draws by where it is. Host and replica arenas
    /// both work; the prototype ArenaGame never creates this. Cosmetic only: nothing here touches the match.
    /// </summary>
    public sealed class NativeShotPresentation : IDisposable
    {
        private sealed class Layer
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};public readonly Material Material;
            public readonly List<Vector3> Vertices=new List<Vector3>(),Normals=new List<Vector3>();public readonly List<Color> Colors=new List<Color>();public readonly List<Vector4> Emission=new List<Vector4>();public readonly List<int> Indices=new List<int>();
            public Layer(string name,Material material){Mesh.name=name;Mesh.MarkDynamic();Material=material;}
            public void Clear(){Vertices.Clear();Normals.Clear();Colors.Clear();Emission.Clear();Indices.Clear();}
        }
        private sealed class Translucent{public Mesh Mesh;public Color Color;}
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private readonly NativeShotScene scene=new NativeShotScene();
        private readonly Layer solid,beams;private readonly Material fade;
        private readonly Dictionary<int,Translucent> translucent=new Dictionary<int,Translucent>();private readonly List<Color> translucentColors=new List<Color>();
        // Used when no shared tracker is passed (Edit-mode checks); ArenaPresentation shares one with NativeCombatGlow.
        private readonly NativeShotTracker own=new NativeShotTracker();
        private readonly NativeShotMesh box=NativeShotGeometry.Box(1,1,1);
        // Vertex colours reach the target unconverted. The scene's colours are linear (three.js working space), right for a
        // linear-space project; a gamma-space project gets them re-encoded to sRGB.
        private readonly bool gamma;
        private NativeArena last;private int generation=-1;private float lastTime=-1;
        public int Parts=>scene.Draws.Count;public int Sparks=>scene.Bursts.Count;public int Beams=>scene.Bursts.Beams;public int Live=>scene.Live;
        public NativeShotPresentation()
        {
            var unlit=Resources.Load<Shader>("NativeBlast");if(unlit==null)throw new InvalidOperationException("NativeBlast shader is missing");
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)throw new InvalidOperationException("NativeSourceLit shader is missing");
            gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;
            // Source MeshBasicMaterial({toneMapped:false}): unlit, opaque, front faces, fog.
            var opaque=new Material(unlit){name="Native shot models",renderQueue=(int)RenderQueue.Geometry};
            opaque.SetFloat("_Src",(float)BlendMode.One);opaque.SetFloat("_Dst",(float)BlendMode.Zero);opaque.SetFloat("_ZWrite",1);opaque.SetFloat("_Additive",0);
            // The tidal wave: transparent, opacity .5, depthWrite false.
            fade=new Material(unlit){name="Native shot translucent",renderQueue=(int)RenderQueue.Transparent};fade.SetOverrideTag("RenderType","Transparent");
            fade.SetFloat("_Src",(float)BlendMode.SrcAlpha);fade.SetFloat("_Dst",(float)BlendMode.OneMinusSrcAlpha);fade.SetFloat("_ZWrite",0);fade.SetFloat("_Additive",0);
            // EventVfx.beam: MeshLambertMaterial with emissive = colour at .7 (albedo from vertex colour, emission from uv1).
            var lambert=new Material(lit){name="Native shot beams"};lambert.SetInt("_Cull",2);lambert.SetFloat("_Ink",0);
            solid=new Layer("Native shot models",opaque);beams=new Layer("Native shot beams",lambert);
        }
        public void Clear(){scene.Clear();own.Clear();lastTime=-1;solid.Mesh.Clear();beams.Mesh.Clear();}
        /// <summary>Source BurstPool.spawn for other effects (event bursts): count·quality voxel sparks at a point.</summary>
        public void Burst(Vector3 at,int hex,int count,float power,float quality)=>scene.Bursts.Spawn(at.x,at.y,at.z,hex,count,power,quality);
        /// <summary>Source EventVfx.beam: a 0.09 s lit streak from a to b.</summary>
        public void Beam(Vector3 a,Vector3 b,int hex,float quality)=>scene.Bursts.Beam(a.x,a.y,a.z,b.x,b.y,b.z,hex,quality);
        /// <summary>
        /// now: the presentation clock (on a replica the snapshot render clock, ArenaPresentation.FieldClock). quality:
        /// effects quality (Full 1, Reduced .3). paused: the match is stopped. tracker: the per-id memory shared with
        /// NativeCombatGlow, begun once this frame; null uses this drawer's own.
        /// </summary>
        public void Render(NativeArena arena,Camera camera,float now,float quality,bool paused,NativeShotTracker tracker=null)
        {
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            if(tracker==null){tracker=own;own.Begin(arena,arena.Generation,arena.Replica,arena.Time);}
            lastTime=Math.Max(lastTime,now);double dt=NativeBlastFrame.Step(Time.unscaledDeltaTime,paused);
            // The darkness pulse and tail wobble follow the source's state.time: the match clock on the host, the
            // interpolated snapshot time (this clock) on a replica.
            scene.Begin(now,arena.Replica?now:arena.Time,dt,Math.Max(0,quality));
            // Siren shots keep their placeholder cube (ArenaPresentation.SirenShotPlaceholders) until the Siren effects port (C12).
            foreach(var b in arena.Bullets){NativeShotViews.Bullet(arena,b,tracker,now,out var v);if(!v.Siren)scene.Shot(v);}
            foreach(var s in arena.AbilityShots){NativeShotViews.Ability(arena,s,tracker,now,out var v);scene.Shot(v);}
            foreach(var s in arena.MeleeShots){NativeShotViews.Melee(arena,s,tracker,now,out var v);scene.Shot(v);}
            scene.End(dt);Fill(camera);
        }
        private Color Shade(float r,float g,float b,float a)=>gamma?new Color(Mathf.LinearToGammaSpace(r),Mathf.LinearToGammaSpace(g),Mathf.LinearToGammaSpace(b),a):new Color(r,g,b,a);
        private void Fill(Camera camera)
        {
            solid.Clear();beams.Clear();
            var draws=scene.Draws;
            for(int i=0;i<draws.Count;i++)
            {
                var d=draws[i];var color=Shade(d.R,d.G,d.B,d.A);if(d.A<.999f){DrawTranslucent(d,color,camera);continue;}
                var mesh=NativeShotShapes.Mesh(d.Shape);var p=mesh.Positions;int start=solid.Vertices.Count;
                for(int k=0;k<p.Length;k+=3){d.M.Apply(p[k],p[k+1],p[k+2],out float x,out float y,out float z);solid.Vertices.Add(new Vector3(x,y,z));solid.Colors.Add(color);}
                var tris=mesh.Indices;for(int k=0;k<tris.Length;k++)solid.Indices.Add(start+tris[k]);
            }
            // Sparks: unlit axis-aligned cubes (NativeBlastSim's corner cube and Unity front-face winding, as the blast voxels).
            var pool=scene.Bursts;var s=pool.Sparks;var corners=NativeBlastSim.CubeCorners;var pattern=NativeBlastSim.CubeTriangles;
            for(int i=0;i<pool.Count;i++)
            {
                int k=i*NativeBurstPool.SparkStride;float h=(float)s[k+6]*.5f;var center=new Vector3((float)s[k],(float)s[k+1],(float)s[k+2]);var color=Shade((float)s[k+8],(float)s[k+9],(float)s[k+10],1);int start=solid.Vertices.Count;
                for(int c=0;c<8;c++){solid.Vertices.Add(center+new Vector3(corners[c*3]*h,corners[c*3+1]*h,corners[c*3+2]*h));solid.Colors.Add(color);}
                for(int t=0;t<pattern.Length;t++)solid.Indices.Add(start+pattern[t]);
            }
            // Beams: lit .022 m boxes with the source BoxGeometry normals turned by the beam rotation.
            var bd=pool.BeamData;var bp=box.Positions;var bn=box.Normals;
            for(int i=0;i<pool.Beams;i++)
            {
                int k=i*NativeBurstPool.BeamStride;var m=NativeShotAffine.Compose(bd[k],bd[k+1],bd[k+2],bd[k+9],bd[k+10],bd[k+11],bd[k+12],bd[k+6],bd[k+7],bd[k+8]);var r=new Quaternion((float)bd[k+9],(float)bd[k+10],(float)bd[k+11],(float)bd[k+12]);
                float lr=(float)bd[k+14],lg=(float)bd[k+15],lb=(float)bd[k+16];var albedo=Shade(lr,lg,lb,1);var e=Shade(lr*.7f,lg*.7f,lb*.7f,0);var glow=new Vector4(e.r,e.g,e.b,0);int start=beams.Vertices.Count;
                for(int v=0;v<bp.Length;v+=3){m.Apply(bp[v],bp[v+1],bp[v+2],out float x,out float y,out float z);beams.Vertices.Add(new Vector3(x,y,z));beams.Normals.Add(r*new Vector3(bn[v],bn[v+1],bn[v+2]));beams.Colors.Add(albedo);beams.Emission.Add(glow);}
                var tris=box.Indices;for(int t=0;t<tris.Length;t++)beams.Indices.Add(start+tris[t]);
            }
            Draw(solid,camera);Draw(beams,camera);
        }
        // A translucent part (opacity .5, no depth write): one draw of a cached mesh at the part's own transform, so its
        // bounds, and so its place in Unity's back-to-front transparent order, are where the part is.
        private void DrawTranslucent(in NativeShotDraw d,Color color,Camera camera)
        {
            if(camera==null)return;
            if(!translucent.TryGetValue((int)d.Shape,out var cached))
            {
                var source=NativeShotShapes.Mesh(d.Shape);var p=source.Positions;var vertices=new Vector3[p.Length/3];for(int k=0;k<vertices.Length;k++)vertices[k]=new Vector3(p[k*3],p[k*3+1],p[k*3+2]);
                var mesh=new Mesh{name="Native shot "+d.Shape,indexFormat=vertices.Length>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.triangles=source.Indices;mesh.RecalculateBounds();
                cached=new Translucent{Mesh=mesh,Color=new Color(-1,-1,-1,-1)};translucent.Add((int)d.Shape,cached);
            }
            if(cached.Color!=color){translucentColors.Clear();int n=cached.Mesh.vertexCount;for(int k=0;k<n;k++)translucentColors.Add(color);cached.Mesh.SetColors(translucentColors);cached.Color=color;}
            var m=d.M;var matrix=new Matrix4x4{m00=m.M00,m01=m.M01,m02=m.M02,m03=m.M03,m10=m.M10,m11=m.M11,m12=m.M12,m13=m.M13,m20=m.M20,m21=m.M21,m22=m.M22,m23=m.M23,m33=1};
            Graphics.DrawMesh(cached.Mesh,matrix,fade,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private static void Draw(Layer layer,Camera camera)
        {
            var mesh=layer.Mesh;mesh.Clear();if(layer.Vertices.Count==0||camera==null)return;
            mesh.SetVertices(layer.Vertices);mesh.SetColors(layer.Colors);if(layer.Normals.Count==layer.Vertices.Count)mesh.SetNormals(layer.Normals);if(layer.Emission.Count==layer.Vertices.Count)mesh.SetUVs(1,layer.Emission);
            mesh.SetTriangles(layer.Indices,0,false);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,layer.Material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose()
        {
            Clear();foreach(var layer in new[]{solid,beams}){CapturedAnimation.Release(layer.Mesh);CapturedAnimation.Release(layer.Material);}
            foreach(var cached in translucent.Values)CapturedAnimation.Release(cached.Mesh);translucent.Clear();CapturedAnimation.Release(fade);
        }
    }

    public sealed partial class ArenaPresentation
    {
        private NativeShotPresentation shots;
        // The projectiles' first-seen identity and replica frame positions, shared by the models and the combat glow;
        // begun once per RenderGlow and cleared with the presentation.
        private readonly NativeShotTracker shotTracker=new NativeShotTracker();
        /// <summary>Source BurstPool.spawn (renderer.burst) for event effects: voxel sparks drawn with the projectile visuals.</summary>
        public void SourceBurst(Vector3 at,int hex,int count=8,float power=3)=>shots?.Burst(at,hex,count,power,BlastQuality);
        /// <summary>Source EventVfx.beam (renderer.beam): a 0.09 s lit streak.</summary>
        public void SourceBeam(Vector3 a,Vector3 b,int hex)=>shots?.Beam(a,b,hex,BlastQuality);
        // The Siren's shots keep the earlier placeholder cube until client/siren/fx.ts is ported (C12); NativeShotPresentation
        // draws every other projectile.
        private void SirenShotPlaceholders(NativeArena arena)
        {
            foreach(var b in arena.Bullets)if(b.SirenKind!=null||b.Spec=="siren")
                Add(4,Matrix4x4.TRS(b.Position,Quaternion.Euler(hazardClock*140,hazardClock*90,0),Vector3.one*Mathf.Max(.18f,b.Radius*2)));
        }
    }
}
