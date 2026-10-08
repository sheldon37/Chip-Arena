using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Draws the pure NativeBlastSim (source chip-blasts.ts) in the arena: one dynamic mesh per pool (unlit opaque voxels and
    // additive glow voxels) refilled from reused buffers each frame, and three pooled point lights. Blasts are keyed to what
    // host and guests both hold: the ability/chip effect list, ability zones, chip fields, ability shots and surface impacts.
    // Cosmetic only; nothing here touches the match.
    public sealed class NativeBlastPresentation : IDisposable
    {
        private sealed class Layer
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};public readonly Material Material;
            public readonly List<Vector3> Vertices=new List<Vector3>();public readonly List<Color> Colors=new List<Color>();public readonly List<int> Indices=new List<int>();
            public Layer(string name,Material material){Mesh.name=name;Mesh.MarkDynamic();Material=material;}
        }
        private struct Marker{public Vector3 Position;public string Tag;public float Reach;}
        private readonly NativeBlastSim sim=new NativeBlastSim();
        private readonly Layer solid,glow;private readonly GameObject root;private readonly Light[] lights=new Light[NativeBlastSim.LightCapacity];
        private HashSet<long> seen=new HashSet<long>(),seenBefore=new HashSet<long>();
        private readonly HashSet<long> liveFields=new HashSet<long>();private readonly Dictionary<long,float> zoneBorn=new Dictionary<long,float>();private readonly List<long> stale=new List<long>();
        private List<Marker> sites=new List<Marker>(),sitesBefore=new List<Marker>(),shots=new List<Marker>(),shotsBefore=new List<Marker>();
        private readonly List<Vector3> landings=new List<Vector3>();
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private int generation=-1,lastEvent;private float lastTime=-1;
        public NativeBlastPresentation(Transform parent)
        {
            var shader=Resources.Load<Shader>("NativeBlast");if(shader==null)throw new InvalidOperationException("NativeBlast shader is missing");
            var opaque=new Material(shader){name="Native blast voxels",renderQueue=(int)RenderQueue.Geometry};
            opaque.SetFloat("_Src",(float)BlendMode.One);opaque.SetFloat("_Dst",(float)BlendMode.Zero);opaque.SetFloat("_ZWrite",1);opaque.SetFloat("_Additive",0);
            // Source renderOrder 3 for the additive pool.
            var additive=new Material(shader){name="Native blast glow",renderQueue=(int)RenderQueue.Transparent+3};additive.SetOverrideTag("RenderType","Transparent");
            additive.SetFloat("_Src",(float)BlendMode.SrcAlpha);additive.SetFloat("_Dst",(float)BlendMode.One);additive.SetFloat("_ZWrite",0);additive.SetFloat("_Additive",1);
            solid=new Layer("Native blast voxels",opaque);glow=new Layer("Native blast glow",additive);
            root=new GameObject("Native blast light pool");root.transform.SetParent(parent,false);
            for(int i=0;i<lights.Length;i++){var go=new GameObject("Blast flash "+i);go.transform.SetParent(root.transform,false);var light=go.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.cullingMask=~(1<<10);light.enabled=false;lights[i]=light;}
        }
        public void Clear()
        {
            sim.Clear();seen.Clear();seenBefore.Clear();liveFields.Clear();zoneBorn.Clear();sites.Clear();sitesBefore.Clear();shots.Clear();shotsBefore.Clear();
            lastEvent=0;lastTime=-1;solid.Mesh.Clear();glow.Mesh.Clear();foreach(var light in lights)if(light!=null)light.enabled=false;
        }
        private static long Mix(long h,int v){unchecked{h^=(uint)v;h*=0x100000001b3L;return h;}}
        private static int Bits(float v)=>BitConverter.SingleToInt32Bits(v);
        private static long Key(NativeAbilityFx fx)
        {long h=unchecked((long)0xcbf29ce484222325UL);h=Mix(h,Bits(fx.Until));h=Mix(h,Bits(fx.Position.x));h=Mix(h,Bits(fx.Position.y));h=Mix(h,Bits(fx.Position.z));h=Mix(h,Bits(fx.Radius));return Mix(h,fx.Element*2+(fx.Beam?1:0));}
        // Zones carry no id; owner, ability, kind and expiry stay fixed for a zone's life on host and guests (meteor-strike's
        // burning ground changes kind and expiry, which makes it a new field exactly as in the source).
        private static long ZoneKey(NativeAbilityZone z)
        {long h=Mix(unchecked((long)0x9e3779b97f4a7c15UL),z.Owner?.Seat.id??0);h=Mix(h,z.Definition?.id?.GetHashCode()??0);h=Mix(h,z.Kind?.GetHashCode()??0);h=Mix(h,Bits(z.Until));return h|(1L<<62);}
        private static bool Explodes(string kind)=>kind=="bomb"||kind=="mine"||kind=="implosion";
        private static string Fx(ContentAbility d)=>string.IsNullOrEmpty(d.fx)?null:d.fx;
        private static string Site(List<Marker> list,Vector3 at){foreach(var s in list)if((s.Position-at).sqrMagnitude<.0025f)return s.Tag;return null;}
        private static void Nearest(List<Marker> list,Vector3 at,ref float best,ref string tag){foreach(var s in list){float d=(s.Position-at).sqrMagnitude;if(d<s.Reach*s.Reach&&d<best){best=d;tag=s.Tag;}}}
        // The source tag of the field, zone or ability shot that ended at this effect, this frame or the frame before.
        private string TagAt(Vector3 at)
        {
            // Fields and zones detonate at their position lifted .15 m (NativeChips.StepChipFields, NativeAbilities.StepAbilityWorld).
            string tag=Site(sites,at)??Site(sitesBefore,at);if(tag!=null)return tag;
            float best=float.MaxValue;Nearest(shots,at,ref best,ref tag);Nearest(shotsBefore,at,ref best,ref tag);return tag;
        }
        private void Remember(NativeArena arena)
        {
            var swap=sitesBefore;sitesBefore=sites;sites=swap;sites.Clear();swap=shotsBefore;shotsBefore=shots;shots=swap;shots.Clear();
            foreach(var z in arena.AbilityZones)if(z.Definition!=null)sites.Add(new Marker{Position=z.Position+Vector3.up*.15f,Tag=Fx(z.Definition)});
            // ChipField.Effect (the chip id the source keys styles on) exists on the host only; guests fall back to the kind.
            foreach(var f in arena.ChipFields)if(Explodes(f.Kind))sites.Add(new Marker{Position=f.Position+Vector3.up*.15f,Tag=string.IsNullOrEmpty(f.Effect)?f.Kind:f.Effect});
            foreach(var s in arena.AbilityShots)if(s.Definition!=null)shots.Add(new Marker{Position=s.Position,Tag=Fx(s.Definition),Reach=Math.Max(1,s.Velocity.magnitude*.1f)+s.Radius});
        }
        private void Effects(NativeArena arena,float now,double quality)
        {
            var swap=seenBefore;seenBefore=seen;seen=swap;seen.Clear();landings.Clear();
            foreach(var fx in arena.AbilityEffects)if(fx.Beam&&NativeBlastCues.Classify(true,fx.Radius,fx.Element,null)==NativeBlastCues.Cue.Teleport)landings.Add(fx.Position);
            foreach(var fx in arena.AbilityEffects)
            {
                long key=Key(fx);seen.Add(key);if(seenBefore.Contains(key)||now-(fx.Until-.22f)>.4f)continue;
                string tag=null;if(!fx.Beam){foreach(var p in landings)if((p-fx.Position).sqrMagnitude<.0025f){tag=NativeBlastCues.Colocated(fx.Element);break;}tag??=TagAt(fx.Position);}
                Vector3 at=fx.Position;
                switch(NativeBlastCues.Classify(fx.Beam,fx.Radius,fx.Element,tag))
                {
                    case NativeBlastCues.Cue.Blast:sim.Blast(NativeBlastCues.Style(tag,fx.Element),at.x,at.y,at.z,fx.Radius,quality*NativeBlastCues.Quality(tag),false,NativeBlastSim.TintOf(tag));break;
                    case NativeBlastCues.Cue.Vacuum:sim.Blast(NativeBlastStyle.Void,at.x,at.y,at.z,fx.Radius,quality);break;
                    case NativeBlastCues.Cue.Dud:sim.Impact(at.x,at.y,at.z,fx.Radius,0x9a9a9a,quality);break;
                    case NativeBlastCues.Cue.Teleport:
                    {
                        Vector3 from=fx.From;
                        sim.Blast(NativeBlastStyle.Void,at.x,at.y,at.z,NativeBlastCues.TeleportRadius,quality*NativeBlastCues.TeleportQuality);
                        sim.Impact(from.x,from.y,from.z,NativeBlastCues.TeleportImpactRadius,NativeBlastCues.TeleportColor,quality);
                        sim.Line(from.x,from.y,from.z,at.x,at.y,at.z,0xffffff,NativeBlastCues.TeleportColor,quality,NativeBlastCues.TeleportLineJitter,NativeBlastCues.TeleportLineLife,NativeBlastCues.TeleportLineSweep);break;
                    }
                }
            }
        }
        // event-vfx.ts event(): surface impacts from a water pilot splash, kinetic and demolition chips add their procs.
        private void Impacts(NativeArena arena,float now,double quality)
        {
            foreach(var e in arena.Events)
            {
                if(e.Id<=lastEvent)continue;lastEvent=e.Id;if(e.Kind!=EventKind.Impact||e.Source<=0||now-e.Time>.4f)continue;
                NativeSeat seat=null;foreach(var s in arena.Match.Seats)if(s.id==e.Source){seat=s;break;}if(seat==null)continue;
                double x=e.Position.x,y=e.Position.y,z=e.Position.z;
                if(seat.spec=="water")sim.Impact(x,y,z,.6,0x8de9ee,quality);
                if(seat.chips.TryGetValue("kinetic",out int kinetic)&&kinetic>0)sim.Impact(x,y,z,.65+Math.Min(3,kinetic)*.25,0xffaa64,quality);
                if(seat.chips.TryGetValue("demolition",out int demolition)&&demolition>0)sim.Impact(x,y,z,.5+.12*demolition,0xffd45f,quality);
            }
        }
        private void Fields(NativeArena arena,float now,double quality)
        {
            liveFields.Clear();
            foreach(var f in arena.ChipFields)
            {
                liveFields.Add(f.Id);
                sim.Field(new NativeBlastField{Id=f.Id,Kind=f.Kind,Fx=string.IsNullOrEmpty(f.Effect)?null:f.Effect,X=f.Position.x,Y=f.Position.y,Z=f.Position.z,Radius=f.Radius,Born=f.Born,Until=f.Until},now,quality);
            }
            foreach(var z in arena.AbilityZones)
            {
                if(z.Definition==null)continue;long key=ZoneKey(z);liveFields.Add(key);if(!zoneBorn.TryGetValue(key,out float born))zoneBorn[key]=born=now;
                // The mark locks when its fuse has duration left to run (NativeAbilities orbital-mark: LockAt=born+duration, Until=born+fuse).
                double? lockAt=z.Definition.id=="orbital-mark"?z.Until-z.Definition.fuse+z.Definition.duration:(double?)null;
                sim.Field(new NativeBlastField{Id=key,Kind=z.Kind,Fx=Fx(z.Definition),X=z.Position.x,Y=z.Position.y,Z=z.Position.z,Radius=z.Radius,Born=born,Until=z.Until,LockAt=lockAt},now,quality);
            }
            sim.Prune(liveFields);stale.Clear();foreach(var key in zoneBorn.Keys)if(!liveFields.Contains(key))stale.Add(key);foreach(var key in stale)zoneBorn.Remove(key);
        }
        public void Render(NativeArena arena,Camera camera,float now,float quality)
        {
            // Same clock as the other arena presentation; a menu pause stops it, a rewind (killcam, new round) clears.
            if(generation!=arena.Generation||now<lastTime-.25f){Clear();generation=arena.Generation;}
            float dt=lastTime<0?0:Mathf.Clamp(now-lastTime,0,.1f);lastTime=Math.Max(lastTime,now);
            double q=Math.Max(0,Math.Min(1,quality));
            // Source renderer order: event blasts, then fields(state), then update(dt), then draw.
            Remember(arena);Effects(arena,now,q);Impacts(arena,now,q);Fields(arena,now,q);sim.Update(dt);
            Fill(solid,sim.Solid,camera);Fill(glow,sim.Glow,camera);
            for(int i=0;i<lights.Length;i++)
            {
                var f=sim.Flashes[i];var light=lights[i];float intensity=NativeBlastSim.UnityLightIntensity(f.Intensity);light.enabled=intensity>.001f;if(!light.enabled)continue;
                light.transform.position=new Vector3((float)f.X,(float)f.Y,(float)f.Z);light.color=NativeArena.ColorOf(f.Color);light.intensity=intensity;light.range=(float)f.Distance;
            }
        }
        private static void Fill(Layer layer,NativeBlastPool pool,Camera camera)
        {
            var mesh=layer.Mesh;mesh.Clear();int n=pool.Drawn;if(n==0||camera==null)return;
            var vertices=layer.Vertices;var colors=layer.Colors;var indices=layer.Indices;vertices.Clear();colors.Clear();
            var o=pool.Output;var corners=NativeBlastSim.CubeCorners;
            for(int i=0;i<n;i++)
            {
                int w=i*NativeBlastPool.Stride;float qx=o[w+6],qy=o[w+7],qz=o[w+8],qw=o[w+9],hx=o[w+3]*.5f,hy=o[w+4]*.5f,hz=o[w+5]*.5f;
                float xx=qx*qx,yy=qy*qy,zz=qz*qz,xy=qx*qy,xz=qx*qz,yz=qy*qz,wx=qw*qx,wy=qw*qy,wz=qw*qz;
                var center=new Vector3(o[w],o[w+1],o[w+2]);
                var right=new Vector3(1-2*(yy+zz),2*(xy+wz),2*(xz-wy))*hx;var up=new Vector3(2*(xy-wz),1-2*(xx+zz),2*(yz+wx))*hy;var forward=new Vector3(2*(xz+wy),2*(yz-wx),1-2*(xx+yy))*hz;
                var color=new Color(o[w+10],o[w+11],o[w+12],1);
                for(int c=0;c<8;c++){vertices.Add(center+right*corners[c*3]+up*corners[c*3+1]+forward*corners[c*3+2]);colors.Add(color);}
            }
            // Indices only grow; the same 36-index pattern repeats per voxel.
            var pattern=NativeBlastSim.CubeTriangles;for(int i=indices.Count/pattern.Length;i<n;i++)foreach(int t in pattern)indices.Add(i*8+t);
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0,n*pattern.Length,0,false);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,layer.Material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){Clear();CapturedAnimation.Release(solid.Mesh);CapturedAnimation.Release(glow.Mesh);CapturedAnimation.Release(solid.Material);CapturedAnimation.Release(glow.Material);CapturedAnimation.Release(root);}
    }
}
