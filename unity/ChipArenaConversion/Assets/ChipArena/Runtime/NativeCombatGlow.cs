using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Source-style additive halos/ribbons and seven reused spill lights. No fullscreen bloom dependency.
    public sealed class NativeCombatGlow : IDisposable
    {
        private sealed class SurfaceMark
        {public NativePropBody Prop;public Vector3 LocalPosition;public Quaternion LocalRotation;public float Born;public Color Color;}
        private sealed class Geometry
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};
            private readonly List<Vector3> points=new List<Vector3>(),uv=new List<Vector3>();
            private readonly List<Color> colors=new List<Color>();private readonly List<int> indices=new List<int>();
            public Geometry(string name){Mesh.name=name;Mesh.MarkDynamic();}
            public void Clear(){points.Clear();uv.Clear();colors.Clear();indices.Clear();}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color start,Color end,bool ribbon=false)
            {
                int n=points.Count;points.Add(a);points.Add(b);points.Add(c);points.Add(d);
                colors.Add(start);colors.Add(start);colors.Add(end);colors.Add(end);
                float mode=ribbon?1:0;uv.Add(new Vector3(-1,-1,mode));uv.Add(new Vector3(1,-1,mode));uv.Add(new Vector3(-1,1,mode));uv.Add(new Vector3(1,1,mode));
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n+2);indices.Add(n+1);indices.Add(n+3);
            }
            public void Halo(Vector3 center,Vector3 right,Vector3 up,float size,Color color,float alpha)
            {color.a=alpha;right*=size*.5f;up*=size*.5f;Quad(center-right-up,center+right-up,center-right+up,center+right+up,color,color);}
            public void Draw(Material material,Camera camera,int layer)
            {
                Mesh.Clear();if(points.Count==0)return;
                Mesh.SetVertices(points);Mesh.SetColors(colors);Mesh.SetUVs(0,uv);Mesh.SetTriangles(indices,0);Mesh.RecalculateBounds();
                Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,layer,camera,0,null,ShadowCastingMode.Off,false);
            }
        }
        private readonly Geometry world=new Geometry("Native world glow"),view=new Geometry("Native view glow");
        private readonly Material material;private readonly GameObject root;
        private readonly Light[] projectileLights=new Light[3],impactLights=new Light[2],armorLights=new Light[2];
        private readonly Dictionary<int,NativeTrace> traces=new Dictionary<int,NativeTrace>();
        private readonly List<SurfaceMark> marks=new List<SurfaceMark>();private int generation=-1,lastEvent;
        private float lastTime=-1;public float Quality=1;
        public int LiveMarks=>marks.Count;public int LiveTraces=>traces.Count;
        public NativeCombatGlow(Transform parent)
        {
            var shader=Resources.Load<Shader>("NativeGlow");if(shader==null)throw new InvalidOperationException("NativeGlow shader is missing");
            material=new Material(shader);root=new GameObject("Native combat light pool");root.transform.SetParent(parent,false);
            void Create(Light[] pool,string name)
            {for(int i=0;i<pool.Length;i++){var go=new GameObject(name+" "+i);go.transform.SetParent(root.transform,false);var light=go.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.cullingMask=~(1<<10);light.enabled=false;pool[i]=light;}}
            Create(projectileLights,"Projectile spill");Create(impactLights,"Impact spill");Create(armorLights,"Armor spill");
        }
        private static Color Tint(int value)=>NativeArena.ColorOf(value);
        private static Color School(int element)=>element==1?Tint(0xff743d):element==2?Tint(0x58cfff):element==3?Tint(0xb6c8ff):element==4?Tint(0xbb77ff):Tint(0xffe8a3);
        private static void SetLight(Light light,Vector3 position,Color color,float intensity,float range)
        {
            light.enabled=intensity>.001f;if(!light.enabled)return;light.transform.position=position;light.color=color;
            // Three's physical light units differ from Unity built-in intensity; preserve palette/range with a native gain.
            light.intensity=intensity*.35f;light.range=range;
        }
        private void DisableLights(){foreach(var light in projectileLights.Concat(impactLights).Concat(armorLights))if(light!=null)light.enabled=false;}
        public void Clear()
        {traces.Clear();marks.Clear();lastEvent=0;lastTime=-1;world.Clear();view.Clear();DisableLights();}
        public void Render(NativeArena arena,Camera camera,Camera viewCamera,bool first,Dictionary<int,CapturedAnimation.Player> rigs,CapturedAnimation.Player viewRig,Matrix4x4? viewStaff,bool submitDraws=true)
        {
            float now=arena.Time;if(generation!=arena.Generation||now<lastTime){Clear();generation=arena.Generation;}lastTime=now;
            world.Clear();view.Clear();DisableLights();
            if(Quality<=0){traces.Clear();marks.Clear();if(arena.Events.Count>0)lastEvent=arena.Events[arena.Events.Count-1].Id;return;}
            Vector3 right=camera.transform.right,up=camera.transform.up;
            var glowShots=new List<(int id,Vector3 position,float radius,int color,bool magic)>();
            foreach(var shot in arena.Bullets)glowShots.Add((shot.Id,shot.Position,shot.Radius,(int)arena.Content.Weapon(shot.Spec).color,arena.Content.Weapon(shot.Spec).@class=="magic"));
            foreach(var shot in arena.AbilityShots)glowShots.Add((shot.Id,shot.Position,shot.Radius,(int)shot.Owner.Weapon.color,shot.Owner.Weapon.@class=="magic"));
            foreach(var shot in arena.MeleeShots)glowShots.Add((shot.Id,shot.Position,shot.Radius,(int)shot.Original.Weapon.color,false));
            foreach(var shot in glowShots)
            {
                world.Halo(shot.position,right,up,NativeGlowRules.HaloSize(shot.radius,Quality),Tint(shot.color),.88f);
                if(!traces.TryGetValue(shot.id,out var trace)){trace=new NativeTrace();traces.Add(shot.id,trace);}
                trace.Sample(NativeArena.Vec(shot.position),now,shot.color);
            }
            var nearest=glowShots.OrderBy(s=>(s.position-camera.transform.position).sqrMagnitude).Take(Quality<.5f?1:3).ToArray();
            for(int i=0;i<nearest.Length;i++){var s=nearest[i];SetLight(projectileLights[i],s.position,Tint(s.color),s.magic?5:2.8f,s.magic?5:3.8f);}
            foreach(var e in arena.Events)
            {
                if(e.Id<=lastEvent)continue;lastEvent=e.Id;
                if(e.Kind!=EventKind.Impact||string.IsNullOrEmpty(e.SurfaceId)||now-e.Time>=NativeGlowRules.ImpactLife)continue;
                var prop=arena.Props.Find(p=>p!=null&&p.Definition.id==e.SurfaceId);if(prop==null)continue;
                Vector3 normal=NativeArena.Vec(e.Normal).normalized;if(normal.sqrMagnitude<.5f)continue;
                Vector3 point=NativeArena.Vec(e.Position)+normal*.018f;
                marks.Add(new SurfaceMark{Prop=prop,LocalPosition=prop.transform.InverseTransformPoint(point),LocalRotation=Quaternion.Inverse(prop.transform.rotation)*Quaternion.LookRotation(normal),Born=e.Time,Color=Color.Lerp(Tint(e.Color),Tint(0x95eaff),.25f)});
                // A bounce event can predate the current render sample after several fixed ticks.
                if(traces.TryGetValue(e.Projectile,out var trail)&&e.Time>=trail.Seen)trail.Sample(e.Position,e.Time,e.Color);
            }
            foreach(var pair in traces.ToArray())
            {
                var trace=pair.Value;if(trace.Expired(now)){traces.Remove(pair.Key);continue;}
                for(int i=1;i<trace.Points.Count;i++)
                {
                    var a=trace.Points[i-1];var b=trace.Points[i];Vector3 va=NativeArena.Vec(a.Position),vb=NativeArena.Vec(b.Position),mid=(va+vb)*.5f;
                    Vector3 side=Vector3.Cross(vb-va,camera.transform.position-mid);if(side.sqrMagnitude<1e-8f)side=right;
                    side=side.normalized*NativeGlowRules.TrailWidth(Vector3.Distance(mid,camera.transform.position),1);
                    Color ca=Tint(trace.Color),cb=ca;float tail=.85f*NativeGlowRules.TrailFade(now-trace.Seen);ca.a=NativeGlowRules.TrailFade(now-a.At)*tail;cb.a=NativeGlowRules.TrailFade(now-b.At)*tail;
                    world.Quad(va-side,va+side,vb-side,vb+side,ca,cb,true);
                }
            }
            marks.RemoveAll(m=>m.Prop==null||now<m.Born||now-m.Born>=NativeGlowRules.ImpactLife);
            foreach(var mark in marks)
            {
                Vector3 point=mark.Prop.transform.TransformPoint(mark.LocalPosition);Quaternion rotation=mark.Prop.transform.rotation*mark.LocalRotation;
                world.Halo(point,rotation*Vector3.right,rotation*Vector3.up,NativeGlowRules.ImpactSize(now-mark.Born),mark.Color,NativeGlowRules.ImpactFade(now-mark.Born));
            }
            if(Quality>=.5f)for(int i=0;i<Math.Min(2,marks.Count);i++)
            {
                var m=marks[marks.Count-1-i];Quaternion rotation=m.Prop.transform.rotation*m.LocalRotation;
                SetLight(impactLights[i],m.Prop.transform.TransformPoint(m.LocalPosition)+rotation*Vector3.forward*.14f,m.Color,NativeGlowRules.ImpactLight(now-m.Born),2.7f);
            }
            foreach(var zone in arena.AbilityZones)
            {
                Color color=School(zone.Owner.Pose.MagicVisual);float pulse=.85f+.15f*Mathf.Sin(now*5);
                world.Halo(zone.Position+Vector3.up*.035f,Vector3.right,Vector3.forward,zone.Radius*2,color,(zone.Kind=="bomb"?.30f:.45f)*pulse);
                if(zone.Kind=="black-hole")world.Halo(zone.Position+Vector3.up*.7f,right,up,2,color,.7f);
            }
            foreach(var fx in arena.AbilityEffects)if(!fx.Beam)
            {
                float fade=Mathf.Clamp01((fx.Until-now)/.22f);Color color=School(fx.Element);
                world.Halo(fx.Position,right,up,Mathf.Max(.3f,fx.Radius*2*(1-fade*.5f)),color,fade*.85f);
                world.Halo(fx.Position+Vector3.up*.025f,Vector3.right,Vector3.forward,fx.Radius*2,color,fade*.5f);
            }
            int lightIndex=0;
            foreach(var pilot in arena.Pilots)
            {
                if(pilot.Hp<=0||first&&pilot.Seat.id==arena.Local.Seat.id||!rigs.TryGetValue(pilot.Seat.id,out var rig))continue;
                Color color=Color.Lerp(pilot.Seat.bot?Tint(0xeb4d61):Tint(0x2bd9d4),Tint(0xd1edff),.26f);
                float pulse=NativeGlowRules.ArmorPulse(now,pilot.BlockReady-now,now<pilot.BlockEnd),alpha=(Quality<.5f?.43f:.70f)*pulse;
                void Joint(int bone,Vector3 local,float size)=>world.Halo(rig.Bones[bone].TransformPoint(local),right,up,size,color,alpha);
                Joint(CapturedRig.Chest,new Vector3(0,.13f,.19f),.75f);Joint(CapturedRig.Chest,new Vector3(0,.12f,-.20f),.62f);
                Joint(CapturedRig.LClav,new Vector3(-.08f,0,0),.52f);Joint(CapturedRig.RClav,new Vector3(.08f,0,0),.52f);
                Joint(CapturedRig.LKnee,new Vector3(0,-.06f,.15f),.38f);Joint(CapturedRig.RKnee,new Vector3(0,-.06f,.15f),.38f);
                Joint(CapturedRig.LHand,new Vector3(0,0,.10f),.26f);Joint(CapturedRig.RHand,new Vector3(0,0,.10f),.26f);
                if(Quality>=.5f&&lightIndex<2)SetLight(armorLights[lightIndex++],rig.Bones[CapturedRig.Chest].TransformPoint(new Vector3(0,.06f,.20f)),color,8*pulse,3.6f);
            }
            if(first&&viewRig!=null&&viewCamera!=null)
            {
                Color color=Tint(0x70e1d0);float pulse=NativeGlowRules.ArmorPulse(now,arena.Local.BlockReady-now,now<arena.Local.BlockEnd);
                foreach(int bone in new[]{CapturedRig.LHand,CapturedRig.RHand})view.Halo(viewRig.Bones[bone].TransformPoint(new Vector3(0,0,.1f)),right,up,.13f,color,.45f*pulse);
                if(viewStaff.HasValue)view.Halo(viewStaff.Value.MultiplyPoint3x4(new Vector3(0,0,.72f)),right,up,.28f,School(arena.Local.Pose.MagicVisual),.55f);
                if(submitDraws)view.Draw(material,viewCamera,10);
            }
            if(submitDraws)world.Draw(material,camera,0);
        }
        public void Dispose(){Clear();CapturedAnimation.Release(world.Mesh);CapturedAnimation.Release(view.Mesh);CapturedAnimation.Release(material);CapturedAnimation.Release(root);}
    }
}
