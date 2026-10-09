using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What the projectile visuals read from the arena. The host has each shot's own data; a replica (guest, killcam)
    /// receives only id, owner, position, velocity and radius for bullets (Snapshot.Shots) and ability shots, so it
    /// falls back to the owner's seat chips and replicated stats (see the Astra notes in docs/UNITY-PARITY-GAPS.md C10).
    /// Allocation free: views are structs and seats are found by a plain loop.
    /// </summary>
    internal static class NativeShotViews
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
        /// <summary>
        /// The chip stacks a bullet carries (source p.mods): the host's copy of the owner's chips; the seat's chips for a
        /// hadouken (Unity leaves its Mods empty; arcade.ts copies them) and on a replica; none for the sword beam.
        /// </summary>
        public static Dictionary<string,int> BulletMods(NativeArena arena,NativeBullet b)
        {
            bool hadouken=Hadouken(arena,b);if(SwordBeam(Weapon(arena,b.Spec),hadouken))return null;
            return arena.Replica||hadouken?Seat(arena,b.Owner)?.chips:b.Mods;
        }
        public static void Bullet(NativeArena arena,NativeBullet b,out NativeShotView v)
        {
            var w=Weapon(arena,b.Spec);bool replica=arena.Replica;
            v=new NativeShotView{Kind=NativeShotKind.Bullet,Id=b.Id,X=b.Position.x,Y=b.Position.y,Z=b.Position.z,VX=b.Velocity.x,VY=b.Velocity.y,VZ=b.Velocity.z,Radius=b.Radius,Spec=b.Spec,Class=w?.@class,WeaponColor=w!=null?(int)w.color:NativeShotRules.White,FuseLeft=1e9};
            // Siren shots are drawn by the Siren's own effects in the source (sirenEffects.owns); they keep a placeholder (C12).
            if(b.SirenKind!=null||b.Spec=="siren"){v.Siren=true;return;}
            bool hadouken=Hadouken(arena,b);
            if(hadouken)v.Arcade=Count(Seat(arena,b.Owner)?.chips,"arcade-red")>0?1:2;
            v.SwordBeam=SwordBeam(w,hadouken);
            var mods=BulletMods(arena,b);v.VelocityChips=Count(mods,"velocity");v.Bowling=w!=null&&w.@class=="ranged"&&Count(mods,"bowling-ball")>0;
            if(!replica){v.Fire=b.Burn>0;v.Poison=b.Poison>0;}
            else if(!v.SwordBeam)
            {
                // attackElements(w.burn + stats.burn, stats.poison) from the owner's replicated stats; a hadouken always burns.
                var owner=Owner(arena,b.Owner);
                v.Fire=hadouken||(w?.burn??0)+Stat(owner,"burn")>0;v.Poison=Stat(owner,"poison")>0;
            }
        }
        public static void Ability(NativeArena arena,NativeAbilityShot s,out NativeShotView v)
        {
            var owner=s.Owner;var w=owner?.Weapon??Weapon(arena,owner?.Seat?.spec);string id=s.Definition?.id;
            v=new NativeShotView{Kind=NativeShotKind.Ability,Id=s.Id,X=s.Position.x,Y=s.Position.y,Z=s.Position.z,VX=s.Velocity.x,VY=s.Velocity.y,VZ=s.Velocity.z,Radius=s.Radius,Spec=owner?.Seat?.spec,Class=w?.@class,WeaponColor=w!=null?(int)w.color:NativeShotRules.White,Ability=id,
                // The frag fuse is host data (NativeAbilities); a replica has none and shows no blink.
                FuseLeft=s.Fuse>0?s.Fuse-arena.Time:1e9,Fire=id=="flame-lance"};
        }
        public static void Melee(NativeArena arena,NativeMeleeShot s,out NativeShotView v)
        {
            var original=s.Original??s.Owner;var w=Weapon(arena,s.Spec)??original?.Weapon;
            v=new NativeShotView{Kind=NativeShotKind.Melee,Id=s.Id,X=s.Position.x,Y=s.Position.y,Z=s.Position.z,VX=s.Velocity.x,VY=s.Velocity.y,VZ=s.Velocity.z,Radius=s.Radius,Spec=s.Spec??original?.Seat?.spec,Class=w?.@class,WeaponColor=w!=null?(int)w.color:NativeShotRules.White,FuseLeft=1e9,Shield=s.Shield};
            if(!arena.Replica){v.Fire=s.Burn>0;v.Poison=s.Poison>0;}else{v.Fire=Stat(original,"burn")>0;v.Poison=Stat(original,"poison")>0;}
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
        public static Dictionary<string,int> MeleeMods(NativeArena arena,NativeMeleeShot s)=>(s.Original??s.Owner)?.Seat?.chips;
    }

    /// <summary>
    /// Draws the pure NativeShotScene (source projectile models, chip modules, sparks and beams) in the arena: unlit
    /// opaque and translucent parts and the voxel sparks go into dynamic NativeBlast meshes, the lit beams into one
    /// NativeSourceLit mesh, all refilled from reused buffers each frame. Host and replica arenas both work; the
    /// prototype ArenaGame never creates this. Cosmetic only: nothing here touches the match.
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
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private readonly NativeShotScene scene=new NativeShotScene();
        private readonly Layer solid,translucent,beams;
        private readonly Dictionary<long,NativeShotModules> modules=new Dictionary<long,NativeShotModules>();
        private readonly NativeShotMesh box=NativeShotGeometry.Box(1,1,1);
        private NativeArena last;private int generation=-1;private float lastTime=-1;
        public int Parts=>scene.Draws.Count;public int Sparks=>scene.Bursts.Count;public int Beams=>scene.Bursts.Beams;public int Live=>scene.Live;
        public NativeShotPresentation()
        {
            var unlit=Resources.Load<Shader>("NativeBlast");if(unlit==null)throw new InvalidOperationException("NativeBlast shader is missing");
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)throw new InvalidOperationException("NativeSourceLit shader is missing");
            // Source MeshBasicMaterial({toneMapped:false}): unlit, opaque, front faces, fog.
            var opaque=new Material(unlit){name="Native shot models",renderQueue=(int)RenderQueue.Geometry};
            opaque.SetFloat("_Src",(float)BlendMode.One);opaque.SetFloat("_Dst",(float)BlendMode.Zero);opaque.SetFloat("_ZWrite",1);opaque.SetFloat("_Additive",0);
            // The tidal wave: transparent, opacity .5, depthWrite false.
            var fade=new Material(unlit){name="Native shot translucent",renderQueue=(int)RenderQueue.Transparent};fade.SetOverrideTag("RenderType","Transparent");
            fade.SetFloat("_Src",(float)BlendMode.SrcAlpha);fade.SetFloat("_Dst",(float)BlendMode.OneMinusSrcAlpha);fade.SetFloat("_ZWrite",0);fade.SetFloat("_Additive",0);
            // EventVfx.beam: MeshLambertMaterial with emissive = colour at .7 (albedo from vertex colour, emission from uv1).
            var lambert=new Material(lit){name="Native shot beams"};lambert.SetInt("_Cull",2);lambert.SetFloat("_Ink",0);
            solid=new Layer("Native shot models",opaque);translucent=new Layer("Native shot translucent",fade);beams=new Layer("Native shot beams",lambert);
        }
        public void Clear(){scene.Clear();modules.Clear();lastTime=-1;solid.Mesh.Clear();translucent.Mesh.Clear();beams.Mesh.Clear();}
        /// <summary>Source BurstPool.spawn for other effects (event bursts): count·quality voxel sparks at a point.</summary>
        public void Burst(Vector3 at,int hex,int count,float power,float quality)=>scene.Bursts.Spawn(at.x,at.y,at.z,hex,count,power,quality);
        /// <summary>Source EventVfx.beam: a 0.09 s lit streak from a to b.</summary>
        public void Beam(Vector3 a,Vector3 b,int hex,float quality)=>scene.Bursts.Beam(a.x,a.y,a.z,b.x,b.y,b.z,hex,quality);
        /// <summary>now: the presentation clock. quality: effects quality (Full 1, Reduced .3). paused: the match is stopped.</summary>
        public void Render(NativeArena arena,Camera camera,float now,float quality,bool paused)
        {
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            lastTime=Math.Max(lastTime,now);double dt=NativeBlastFrame.Step(Time.unscaledDeltaTime,paused);
            scene.Begin(now,arena.Time,dt,Math.Max(0,quality));
            foreach(var b in arena.Bullets)
            {
                // Siren shots keep their placeholder cube (ArenaPresentation.RenderAbilities) until the Siren effects port (C12).
                NativeShotViews.Bullet(arena,b,out var v);if(v.Siren)continue;
                if(!scene.Knows(NativeShotKind.Bullet,b.Id))v.Modules=NativeShotViews.Modules(arena,NativeShotViews.BulletMods(arena,b),modules);
                scene.Shot(v);
            }
            foreach(var s in arena.AbilityShots){NativeShotViews.Ability(arena,s,out var v);scene.Shot(v);}
            foreach(var s in arena.MeleeShots){NativeShotViews.Melee(arena,s,out var v);if(!scene.Knows(NativeShotKind.Melee,s.Id))v.Modules=NativeShotViews.Modules(arena,NativeShotViews.MeleeMods(arena,s),modules);scene.Shot(v);}
            scene.End(dt);Fill(camera);
        }
        private void Fill(Camera camera)
        {
            solid.Clear();translucent.Clear();beams.Clear();
            var draws=scene.Draws;
            for(int i=0;i<draws.Count;i++)
            {
                var d=draws[i];var layer=d.A<.999f?translucent:solid;var mesh=NativeShotShapes.Mesh(d.Shape);var p=mesh.Positions;int start=layer.Vertices.Count;var color=new Color(d.R,d.G,d.B,d.A);
                for(int k=0;k<p.Length;k+=3){d.M.Apply(p[k],p[k+1],p[k+2],out float x,out float y,out float z);layer.Vertices.Add(new Vector3(x,y,z));layer.Colors.Add(color);}
                var tris=mesh.Indices;for(int k=0;k<tris.Length;k++)layer.Indices.Add(start+tris[k]);
            }
            // Sparks: unlit axis-aligned cubes (NativeBlastSim's corner cube and Unity front-face winding, as the blast voxels).
            var pool=scene.Bursts;var s=pool.Sparks;var corners=NativeBlastSim.CubeCorners;var pattern=NativeBlastSim.CubeTriangles;
            for(int i=0;i<pool.Count;i++)
            {
                int k=i*NativeBurstPool.SparkStride;float h=(float)s[k+6]*.5f;var center=new Vector3((float)s[k],(float)s[k+1],(float)s[k+2]);var color=new Color((float)s[k+8],(float)s[k+9],(float)s[k+10],1);int start=solid.Vertices.Count;
                for(int c=0;c<8;c++){solid.Vertices.Add(center+new Vector3(corners[c*3]*h,corners[c*3+1]*h,corners[c*3+2]*h));solid.Colors.Add(color);}
                for(int t=0;t<pattern.Length;t++)solid.Indices.Add(start+pattern[t]);
            }
            // Beams: lit .022 m boxes with the source BoxGeometry normals turned by the beam rotation.
            var bd=pool.BeamData;var bp=box.Positions;var bn=box.Normals;
            for(int i=0;i<pool.Beams;i++)
            {
                int k=i*NativeBurstPool.BeamStride;var m=NativeShotAffine.Compose(bd[k],bd[k+1],bd[k+2],bd[k+9],bd[k+10],bd[k+11],bd[k+12],bd[k+6],bd[k+7],bd[k+8]);var r=new Quaternion((float)bd[k+9],(float)bd[k+10],(float)bd[k+11],(float)bd[k+12]);
                var albedo=new Color((float)bd[k+14],(float)bd[k+15],(float)bd[k+16],1);var glow=new Vector4(albedo.r*.7f,albedo.g*.7f,albedo.b*.7f,0);int start=beams.Vertices.Count;
                for(int v=0;v<bp.Length;v+=3){m.Apply(bp[v],bp[v+1],bp[v+2],out float x,out float y,out float z);beams.Vertices.Add(new Vector3(x,y,z));beams.Normals.Add(r*new Vector3(bn[v],bn[v+1],bn[v+2]));beams.Colors.Add(albedo);beams.Emission.Add(glow);}
                var tris=box.Indices;for(int t=0;t<tris.Length;t++)beams.Indices.Add(start+tris[t]);
            }
            Draw(solid,camera);Draw(translucent,camera);Draw(beams,camera);
        }
        private static void Draw(Layer layer,Camera camera)
        {
            var mesh=layer.Mesh;mesh.Clear();if(layer.Vertices.Count==0||camera==null)return;
            mesh.SetVertices(layer.Vertices);mesh.SetColors(layer.Colors);if(layer.Normals.Count==layer.Vertices.Count)mesh.SetNormals(layer.Normals);if(layer.Emission.Count==layer.Vertices.Count)mesh.SetUVs(1,layer.Emission);
            mesh.SetTriangles(layer.Indices,0,false);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,layer.Material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){Clear();foreach(var layer in new[]{solid,translucent,beams}){CapturedAnimation.Release(layer.Mesh);CapturedAnimation.Release(layer.Material);}}
    }

    public sealed partial class ArenaPresentation
    {
        private NativeShotPresentation shots;
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
