using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Source-style additive halos/ribbons and seven reused spill lights. No fullscreen bloom dependency.
    // Owner cut (2026-10-11 effects overhaul, "that pancake ring effect is still there"; review 2026-10-11): impact-glows.ts laid
    // a flat .7-1.3 m glowing disc on every surface a shot hit (the floor included, so machine-gun fire left a row of them) for
    // .85 s. It is no longer drawn: each impact gets a .12 s camera-facing contact flash through the shared effects renderer
    // (NativeVfxDraw) instead, beside the event's burst sparks; the impact marks still drive the two spill lights for their full
    // life (NativeGlowRules.ImpactLight) and keep their lifetime (ImpactLife, the build checks' count).
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
            /// <summary>NativeGlow.shader modes: a gaussian halo, a ribbon with a (1-|u|)² profile, a flat ribbon.</summary>
            public const float ModeHalo=0,ModeRibbon=1,ModeFlat=2;
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color start,Color end,float mode=ModeHalo)
            {
                int n=points.Count;points.Add(a);points.Add(b);points.Add(c);points.Add(d);
                colors.Add(start);colors.Add(start);colors.Add(end);colors.Add(end);uv.Add(new Vector3(-1,-1,mode));uv.Add(new Vector3(1,-1,mode));uv.Add(new Vector3(-1,1,mode));uv.Add(new Vector3(1,1,mode));
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
        private readonly Material material;private readonly GameObject root;private readonly NativeVfxDraw vfx;
        /// <summary>The impact contact flash: seconds alive, peak HDR gain (linear) and sprite size (m), and how far off the
        /// surface it sits (clear of the soft-particle fade against the face it hit).</summary>
        public const float ContactLife=.12f,ContactGain=2.4f,ContactSize=.45f,ContactLift=.12f;
        private readonly Light[] projectileLights=new Light[3],impactLights=new Light[2],armorLights=new Light[2];
        private readonly Dictionary<long,NativeTrace> traces=new Dictionary<long,NativeTrace>();private readonly Stack<NativeTrace> spareTraces=new Stack<NativeTrace>();private readonly List<long> expired=new List<long>();
        // One projectile's halo, spill light and trail this frame (projectile-glow.ts and projectile-trails.ts via NativeShotRules).
        private struct ShotGlow{public long Key;public Vector3 Position;public float Radius,Intensity,Range,Width;public int Halo,Trail;public bool Traced;}
        private readonly List<ShotGlow> glowShots=new List<ShotGlow>();private readonly List<double> glowDistances=new List<double>();private readonly int[] nearest=new int[3];
        private static readonly int[] viewHands={CapturedRig.LHand,CapturedRig.RHand};
        private readonly List<SurfaceMark> marks=new List<SurfaceMark>();private int generation=-1,lastEvent;
        private float lastTime=-1;public float Quality=1;public bool Trails=true;
        /// <summary>
        /// Armor halo size and opacity factor, the pooled armor spill intensity (three units) and its range (m; armor-glow.ts: 1,
        /// 8 and 3.6). The pool reaches 2.4 m, at a strength that keeps the floor under the pilot as lit as at 3.6 m, so a
        /// prop or wall beside another pilot does not take this pilot's hue through it (the light is unshadowed). Owner
        /// rollback of the halos and pools: ArmorHaloScale 1, ArmorSpillIntensity 8 and ArmorSpillRange 3.6 restore their size,
        /// opacity, strength and reach (the chest halos' facing fade, the pixel band, the hue-true pool colour and the steady
        /// nearest-pilot pick stay); the armour's own light is the shader's (NativeArmorLights).
        /// </summary>
        public const float ArmorHaloScale=.62f,ArmorSpillIntensity=4.2f,ArmorSpillRange=2.4f;
        /// <summary>
        /// The presentation clock the projectile models use (element colours switch every 50 ms); on a replica also the
        /// clock its shots are drawn and its trails sampled at (ArenaPresentation.FieldClock). NaN: the arena clock.
        /// </summary>
        public float Clock=float.NaN;
        /// <summary>The per-id memory shared with NativeShotPresentation, begun once this frame; null uses this glow's own.</summary>
        public NativeShotTracker Tracker;
        private readonly NativeShotTracker own=new NativeShotTracker();
        // The two pooled armor spill lights' holders: nearest the camera, with hysteresis and a fade (Core NativeArmorSpillPicker).
        private readonly NativeArmorSpillPicker spill=new NativeArmorSpillPicker();
        // NativeGlow writes vertex colours unconverted. Projectile halos and trails are linear in a linear-space project
        // (as the models, C6 blasts and C11 ribbons), sRGB in a gamma-space one; Light.color is always sRGB.
        private readonly bool linear;
        public int LiveMarks=>marks.Count;public int LiveTraces=>traces.Count;
        // Muzzle light pulses (owner 2026-10-11 weapons milestone, NativeMuzzleFx.Light): the arena offers each live muzzle star
        // before Render; a pulse lights only a projectile spill light the nearest projectiles left free this frame (no light
        // beyond the seven pooled ones), and the offers are used once.
        private readonly Vector3[] pulseAt=new Vector3[3];private readonly float[] pulsePower=new float[3];private int pulses;
        public void Pulse(Vector3 at,float intensity){if(pulses<pulseAt.Length&&intensity>.001f){pulseAt[pulses]=at;pulsePower[pulses]=intensity;pulses++;}}
        /// <summary>vfx: the shared effects renderer the impact contact flashes queue into (the owner submits it); null draws none.</summary>
        public NativeCombatGlow(Transform parent,NativeVfxDraw vfx=null)
        {
            this.vfx=vfx;
            var shader=Resources.Load<Shader>("NativeGlow");if(shader==null)throw new InvalidOperationException("NativeGlow shader is missing");
            linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            material=new Material(shader);root=new GameObject("Native combat light pool");root.transform.SetParent(parent,false);
            void Create(Light[] pool,string name)
            {for(int i=0;i<pool.Length;i++){var go=new GameObject(name+" "+i);go.transform.SetParent(root.transform,false);var light=go.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.cullingMask=~(1<<10|1<<NativeSceneryBuilder.BackdropLayer);light.enabled=false;pool[i]=light;}}
            Create(projectileLights,"Projectile spill");Create(impactLights,"Impact spill");Create(armorLights,"Armor spill");
        }
        private static Color Tint(int value)=>NativeArena.ColorOf(value);
        private Color ShotTint(int value)=>linear?NativeArena.ColorOf(value).linear:NativeArena.ColorOf(value);
        // armor-glow.ts:23 via NativeGlowRules.ArmorTint: the pilot's own accent. Halo vertex colours follow the project's
        // colour space (as ShotTint); the spill light gets sRGB.
        private Color ArmorTint(string accent,out Color spill){var c=NativeGlowRules.ArmorTint(accent);var tint=new Color(c.X,c.Y,c.Z);spill=tint.gamma;return linear?tint:spill;}
        private static Color School(int element)=>element==1?Tint(0xff743d):element==2?Tint(0x58cfff):element==3?Tint(0xb6c8ff):element==4?Tint(0xbb77ff):Tint(0xffe8a3);
        private static void SetLight(Light light,Vector3 position,Color color,float intensity,float range)
        {
            light.enabled=intensity>.001f;if(!light.enabled)return;light.transform.position=position;light.color=color;
            // Three's physical light units differ from Unity built-in intensity; preserve palette/range with a native gain.
            light.intensity=intensity*.35f;light.range=range;
        }
        private static void Disable(Light[] pool){foreach(var light in pool)if(light!=null)light.enabled=false;}
        private void DisableLights(){Disable(projectileLights);Disable(impactLights);Disable(armorLights);}
        private void ClearTraces(){foreach(var trace in traces.Values){trace.Points.Clear();trace.Seen=0;spareTraces.Push(trace);}traces.Clear();}
        public void Clear()
        {ClearTraces();own.Clear();marks.Clear();lastEvent=0;lastTime=-1;world.Clear();view.Clear();DisableLights();spill.Clear();pulses=0;}
        private void AddShot(in NativeShotView v,float clock,Vector3 camera)
        {
            if(v.Siren)return;var position=new Vector3((float)v.X,(float)v.Y,(float)v.Z);
            glowDistances.Add((position-camera).sqrMagnitude);
            glowShots.Add(new ShotGlow{Key=NativeShotScene.Key(v.Kind,v.Id),Position=position,Radius=(float)v.Radius,Halo=NativeShotRules.HaloColor(v,clock),Trail=NativeShotRules.TrailColor(v,clock),Width=(float)NativeShotRules.TrailWidth(v),Traced=NativeShotRules.HasTrail(v),Intensity=(float)NativeShotRules.LightIntensity(v),Range=(float)NativeShotRules.LightRange(v)});
        }
        public void Render(NativeArena arena,Camera camera,Camera viewCamera,bool first,Dictionary<int,CapturedAnimation.Player> rigs,CapturedAnimation.Player viewRig,Matrix4x4? viewStaff,bool submitDraws=true)
        {
            NativeArmorLights.Apply(Quality);int pulseCount=pulses;pulses=0;
            float now=arena.Time;if(generation!=arena.Generation||now<lastTime){Clear();generation=arena.Generation;}float spillDt=lastTime<0?0:now-lastTime;lastTime=now;
            world.Clear();view.Clear();DisableLights();
            // Combat glow off (a Unity setting; the source has none) drops halos, lights and marks but not the projectile
            // trails, which follow the trails setting alone as in the source.
            bool glow=Quality>0;if(!glow){marks.Clear();spill.Clear();if(arena.Events.Count>0)lastEvent=arena.Events[arena.Events.Count-1].Id;}
            Vector3 right=camera.transform.right,up=camera.transform.up,eye=camera.transform.position;float clock=float.IsNaN(Clock)?now:Clock;
            var tracker=Tracker;if(tracker==null){tracker=own;own.Begin(arena,arena.Generation,arena.Replica,arena.Time);}
            // Trails: the host samples at the match clock its shots move on; a replica at the render clock its shots are
            // drawn at (positions between frames, NativeShotTracker.Present).
            float trailNow=arena.Replica?clock:now;
            // Every projectile but the Siren's (source sirenEffects.owns): bullets, ability shots (a missing definition is the
            // white fallback) and thrown melee weapons, with the source colour, size, light and trail rules.
            glowShots.Clear();glowDistances.Clear();
            foreach(var shot in arena.Bullets){NativeShotViews.Bullet(arena,shot,tracker,clock,out var v);AddShot(v,clock,eye);}
            foreach(var shot in arena.AbilityShots){NativeShotViews.Ability(arena,shot,tracker,clock,out var v);AddShot(v,clock,eye);}
            foreach(var shot in arena.MeleeShots){NativeShotViews.Melee(arena,shot,tracker,clock,out var v);AddShot(v,clock,eye);}
            for(int n=0;n<glowShots.Count;n++)
            {
                var shot=glowShots[n];if(glow)world.Halo(shot.Position,right,up,NativeGlowRules.HaloSize(shot.Radius,Quality),ShotTint(shot.Halo),.88f);
                if(!Trails||!shot.Traced)continue;
                if(!traces.TryGetValue(shot.Key,out var trace))
                {
                    // projectile-trails.ts budget: at most 120 live trails; later projectiles get none.
                    if(traces.Count>=NativeShotRules.TrailLimit)continue;
                    trace=spareTraces.Count>0?spareTraces.Pop():new NativeTrace();traces.Add(shot.Key,trace);
                }
                trace.Sample(NativeArena.Vec(shot.Position),trailNow,shot.Trail,shot.Width);
            }
            // The nearest three (one on low quality) light the scene in their halo colour.
            int lit=glow?NativeShotRules.Nearest(glowDistances,Quality<.5f?1:3,nearest):0;
            for(int i=0;i<lit;i++){var s=glowShots[nearest[i]];SetLight(projectileLights[i],s.Position,Tint(s.Halo),s.Intensity,s.Range);}
            if(glow&&Quality>=.5f)for(int i=lit,k=0;i<projectileLights.Length&&k<pulseCount;i++,k++)SetLight(projectileLights[i],pulseAt[k],Tint(NativeMuzzleFx.LightColor),pulsePower[k],NativeMuzzleFx.LightRange);
            if(glow)foreach(var e in arena.Events)
            {
                if(e.Id<=lastEvent)continue;lastEvent=e.Id;
                if(e.Kind!=EventKind.Impact||string.IsNullOrEmpty(e.SurfaceId)||now-e.Time>=NativeGlowRules.ImpactLife)continue;
                var prop=arena.Props.Find(p=>p!=null&&p.Definition.id==e.SurfaceId);if(prop==null)continue;
                Vector3 normal=NativeArena.Vec(e.Normal).normalized;if(normal.sqrMagnitude<.5f)continue;
                Vector3 point=NativeArena.Vec(e.Position)+normal*.018f;
                marks.Add(new SurfaceMark{Prop=prop,LocalPosition=prop.transform.InverseTransformPoint(point),LocalRotation=Quaternion.Inverse(prop.transform.rotation)*Quaternion.LookRotation(normal),Born=e.Time,Color=Color.Lerp(Tint(e.Color),Tint(0x95eaff),.25f)});
                // A bounce event can predate the current render sample after several fixed ticks. A replica's trails run on
                // the render clock, which is behind the event; its interpolated frames carry the path instead.
                if(!arena.Replica&&(traces.TryGetValue(NativeShotScene.Key(NativeShotKind.Bullet,e.Projectile),out var trail)||traces.TryGetValue(NativeShotScene.Key(NativeShotKind.Melee,e.Projectile),out trail)||traces.TryGetValue(NativeShotScene.Key(NativeShotKind.Ability,e.Projectile),out trail))&&e.Time>=trail.Seen)trail.Sample(e.Position,e.Time,e.Color,trail.Width);
            }
            if(!Trails)ClearTraces();expired.Clear();V3 eye3=NativeArena.Vec(eye),right3=NativeArena.Vec(right);
            foreach(var pair in traces)
            {
                var trace=pair.Value;if(trace.Expired(trailNow)){expired.Add(pair.Key);continue;}
                Color ca=ShotTint(trace.Color),cb=ca;
                // projectile-trails.ts ribbons are flat across their width (NativeGlow flat mode).
                for(int i=1;i<trace.Points.Count;i++){trace.Segment(i,eye3,right3,trailNow,out var a0,out var a1,out var b0,out var b1,out ca.a,out cb.a);world.Quad(NativeArena.Vec(a1),NativeArena.Vec(a0),NativeArena.Vec(b1),NativeArena.Vec(b0),ca,cb,Geometry.ModeFlat);}
            }
            foreach(long key in expired){var trace=traces[key];traces.Remove(key);trace.Points.Clear();trace.Seen=0;spareTraces.Push(trace);}
            if(!glow){if(submitDraws)world.Draw(material,camera,0);return;}
            for(int i=marks.Count-1;i>=0;i--){var m=marks[i];if(m.Prop==null||now<m.Born||now-m.Born>=NativeGlowRules.ImpactLife)marks.RemoveAt(i);}
            // No flat halo on the surface (the owner cut above): a short camera-facing contact flash just off the face, HDR so the
            // bloom catches it, linear for the shared renderer.
            if(vfx!=null)foreach(var mark in marks)
            {
                float f=Mathf.Clamp01(1-(now-mark.Born)/ContactLife);if(f<=0)continue;
                Vector3 point=mark.Prop.transform.TransformPoint(mark.LocalPosition);Quaternion rotation=mark.Prop.transform.rotation*mark.LocalRotation;
                var color=mark.Color.linear*(ContactGain*f*f);color.a=0;vfx.Glow(point+rotation*Vector3.forward*ContactLift,color,ContactSize,NativeVfxDraw.CoreGlow);
            }
            if(Quality>=.5f)for(int i=0;i<Math.Min(2,marks.Count);i++)
            {
                var m=marks[marks.Count-1-i];Quaternion rotation=m.Prop.transform.rotation*m.LocalRotation;
                SetLight(impactLights[i],m.Prop.transform.TransformPoint(m.LocalPosition)+rotation*Vector3.forward*.14f,m.Color,NativeGlowRules.ImpactLight(now-m.Born),2.7f);
            }
            // Ability zones have no additive halo in the source: NativeFieldPresentation draws their normal-blend ground glow.
            // Ability and chip blasts are volumetric voxels (NativeBlastPresentation), and the Siren's own attacks draw only their
            // siren/fx.ts effects (NativeDevEffects): no effect record keeps the first port's two flat halos.
            // The armour now lights itself in the shader (NativeArmorLight.cginc), so its halos are a tighter glare on the
            // trims (.62 of armor-glow.ts's size and opacity) and the two pooled spill lights a soft pool in the trims' own
            // ember colour from the waist, given to the two lit pilots nearest the camera; a light changes hands only to a
            // clearly nearer pilot, fading out and back in (NativeArmorSpillPicker), so its pool and hue never flicker.
            // The chest halos fade as their side turns from the camera (the back light stops fogging a chest seen from the
            // front); every armour halo keeps a 4.5-70 px projected diameter (NativeArmorLight.HaloPixelClamp).
            Vector3 forward=camera.transform.forward;float fov=camera.fieldOfView,height=camera.pixelHeight;
            spill.Begin();
            for(int i=0;i<arena.Pilots.Count;i++)
            {
                var pilot=arena.Pilots[i];
                // The Siren never wears the armor glow (siren/fx.ts:274-275; renderer.ts excludes it).
                if(pilot.Hp<=0||pilot.Seat.spec=="siren"||first&&pilot.Seat.id==arena.ViewPilot.Seat.id||!rigs.TryGetValue(pilot.Seat.id,out var rig))continue;
                Color color=ArmorTint(pilot.Seat.accentColor,out _);
                float pulse=NativeGlowRules.ArmorPulse(now,pilot.BlockReady-now,now<pilot.BlockEnd),alpha=(Quality<.5f?.43f:.70f)*pulse*ArmorHaloScale;
                void Joint(int bone,Vector3 local,float size,float side=0)
                {
                    var t=rig.Bones[bone];Vector3 at=t.TransformPoint(local);float a=alpha;
                    if(side!=0)a*=NativeArmorLight.HaloFacing(Vector3.Dot(t.TransformDirection(0,0,side),(eye-at).normalized));
                    if(a>.001f)world.Halo(at,right,up,NativeArmorLight.HaloPixelClamp(size*ArmorHaloScale,Vector3.Dot(at-eye,forward),fov,height),color,a);
                }
                Joint(CapturedRig.Chest,new Vector3(0,.13f,.19f),.75f,1);Joint(CapturedRig.Chest,new Vector3(0,.12f,-.20f),.62f,-1);
                Joint(CapturedRig.LClav,new Vector3(-.08f,0,0),.52f);Joint(CapturedRig.RClav,new Vector3(.08f,0,0),.52f);
                Joint(CapturedRig.LKnee,new Vector3(0,-.06f,.15f),.38f);Joint(CapturedRig.RKnee,new Vector3(0,-.06f,.15f),.38f);
                Joint(CapturedRig.LHand,new Vector3(0,0,.10f),.26f);Joint(CapturedRig.RHand,new Vector3(0,0,.10f),.26f);
                spill.Offer(pilot.Seat.id,i,(rig.Bones[CapturedRig.Chest].position-eye).magnitude);
            }
            spill.Resolve(spillDt);
            if(Quality>=.5f)for(int k=0;k<NativeArmorSpillPicker.Slots;k++)
            {
                int i=spill.Index(k);if(i<0)continue;var pilot=arena.Pilots[i];var rig=rigs[pilot.Seat.id];
                var hue=NativeArmorLight.SpillColor(NativePortraitRules.Ember(NativePortraitRules.Parse(pilot.Seat.accentColor,NativePortraitRules.ExportAccent)));
                float pulse=NativeGlowRules.ArmorPulse(now,pilot.BlockReady-now,now<pilot.BlockEnd);
                SetLight(armorLights[k],rig.Bones[CapturedRig.Chest].TransformPoint(new Vector3(0,-.05f,.16f)),new Color(hue.X,hue.Y,hue.Z).gamma,ArmorSpillIntensity*pulse*spill.Weight(k),ArmorSpillRange);
            }
            if(first&&viewRig!=null&&viewCamera!=null&&arena.ViewPilot.Seat.spec!="siren")
            {
                Color color=ArmorTint(arena.ViewPilot.Seat.accentColor,out _);float pulse=NativeGlowRules.ArmorPulse(now,arena.ViewPilot.BlockReady-now,now<arena.ViewPilot.BlockEnd);
                Vector3 viewEye=viewCamera.transform.position,viewForward=viewCamera.transform.forward;
                foreach(int bone in viewHands){Vector3 at=viewRig.Bones[bone].TransformPoint(new Vector3(0,0,.1f));view.Halo(at,right,up,NativeArmorLight.HaloPixelClamp(.13f*ArmorHaloScale,Vector3.Dot(at-viewEye,viewForward),viewCamera.fieldOfView,viewCamera.pixelHeight),color,.45f*pulse*ArmorHaloScale);}
                if(viewStaff.HasValue)view.Halo(viewStaff.Value.MultiplyPoint3x4(new Vector3(0,0,.72f)),right,up,.28f,School(arena.ViewPilot.Pose.MagicVisual),.55f);
                if(submitDraws)view.Draw(material,viewCamera,10);
            }
            if(submitDraws)world.Draw(material,camera,0);
        }
        public void Dispose(){Clear();CapturedAnimation.Release(world.Mesh);CapturedAnimation.Release(view.Mesh);CapturedAnimation.Release(material);CapturedAnimation.Release(root);}
    }
}
