using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The one Auditor halo drawer (Core/NativeAuditorHalo.cs, Resources/NativeAuditorHalo.shader), shared by the arena
    /// (third person for host, guests and bots, the killcam replay, corpses and death scenes), the intro rig, the chip draft
    /// stage and the menu portraits, so every place shows the same animated halo. Six meshes are built once (arcs, glow
    /// under the flames, flame slots, sparks over them, and the reduced halo's flat flames and inner glow); a halo is four
    /// draws (reduced: three) with one reused property block: nothing is allocated per frame. Draw order: the opaque arcs,
    /// the additive shells and haze, the premultiplied flames (black against the glow), then the crackle and embers.
    /// The flames and sparks keep their own later queues (Transparent+5, +10, like the blast glow and NativeGlow halos) so
    /// that order holds within a halo: Unity does not promise an order for equal-distance draws in one queue. Known
    /// limitation: they draw over plain Transparent effects (smoke, field domes) even when those are nearer.
    /// The bank's static hat model stands in only when the shader is missing.
    /// </summary>
    public sealed class NativeAuditorHaloDraw:IDisposable
    {
        private readonly Mesh arcs,flames,glow,sparks,fins,simpleGlow;private readonly Material opaque,premultiplied,additive,sparkle;
        private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        private readonly AuditorHaloFrame frame=new AuditorHaloFrame();private readonly AuditorHaloUniforms uniforms=new AuditorHaloUniforms();
        private readonly Vector4[] bolts=new Vector4[NativeAuditorHalo.Arcs*2],flameSlots=new Vector4[NativeAuditorHalo.Flames*2],finSlots=new Vector4[NativeAuditorHalo.Flames],embers=new Vector4[NativeAuditorHalo.Embers];
        private static readonly int AId=Shader.PropertyToID("_HaloA"),SpinId=Shader.PropertyToID("_HaloSpin"),PulseId=Shader.PropertyToID("_HaloPulse"),BoltsId=Shader.PropertyToID("_HaloBolts"),
            FlamesId=Shader.PropertyToID("_HaloFlames"),FinsId=Shader.PropertyToID("_HaloFins"),EmbersId=Shader.PropertyToID("_HaloEmbers");
        /// <summary>The group's world centre and the halo light's strength (0..1) after the last Draw.</summary>
        public Vector3 Centre{get;private set;}public float Light{get;private set;}
        /// <summary>A drawer, or null (logged) when the shader is missing or unsupported here. `fog`: follow the scene fog (the portraits do not).</summary>
        public static NativeAuditorHaloDraw Create(bool fog)
        {
            var shader=Resources.Load<Shader>("NativeAuditorHalo");
            if(shader==null||!shader.isSupported){Debug.LogWarning("Auditor halo shader missing or unsupported: the static halo model is shown");return null;}
            return new NativeAuditorHaloDraw(shader,fog);
        }
        private NativeAuditorHaloDraw(Shader shader,bool fog)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Material Make(string name,BlendMode src,BlendMode dst,bool write,int queue,bool transparent)
            {
                var m=new Material(shader){name=name,renderQueue=queue,hideFlags=HideFlags.HideAndDontSave};m.SetFloat("_Src",(float)src);m.SetFloat("_Dst",(float)dst);m.SetFloat("_ZWrite",write?1:0);m.SetFloat("_HaloFog",fog?1:0);
                if(transparent)m.SetOverrideTag("RenderType","Transparent");
                void Colour(string id,int hex){var c=new Color((hex>>16&255)/255f,(hex>>8&255)/255f,(hex&255)/255f,1);m.SetVector(id,linear?(Vector4)c.linear:(Vector4)c);}
                Colour("_HaloDeep",NativeAuditorHalo.Deep);Colour("_HaloRed",NativeAuditorHalo.Red);Colour("_HaloHot",NativeAuditorHalo.Hot);Colour("_HaloWhite",NativeAuditorHalo.White);Colour("_HaloGlow",NativeAuditorHalo.GlowColor);
                Colour("_HaloBolt",NativeAuditorHalo.BoltColor);Colour("_HaloEmber",NativeAuditorHalo.EmberColor);Colour("_HaloSoot",NativeAuditorHalo.Soot);Colour("_HaloInner",NativeAuditorHalo.Inner);Colour("_HaloRim",NativeAuditorHalo.Rim);
                return m;
            }
            opaque=Make("Auditor halo arcs",BlendMode.One,BlendMode.Zero,true,(int)RenderQueue.Geometry,false);
            additive=Make("Auditor halo glow",BlendMode.One,BlendMode.One,false,(int)RenderQueue.Transparent,true);
            premultiplied=Make("Auditor halo flames",BlendMode.One,BlendMode.OneMinusSrcAlpha,false,(int)RenderQueue.Transparent+5,true);
            sparkle=Make("Auditor halo sparks",BlendMode.One,BlendMode.One,false,(int)RenderQueue.Transparent+10,true);
            var m0=new HaloMesh();
            NativeAuditorHalo.BuildArcs(m0);arcs=Build(m0,"Auditor halo arcs",.6f);m0.Clear();
            NativeAuditorHalo.BuildFlames(m0);flames=Build(m0,"Auditor halo flames",.7f);m0.Clear();
            NativeAuditorHalo.BuildGlow(m0,false);glow=Build(m0,"Auditor halo glow",NativeAuditorHalo.HazeSize);m0.Clear();
            NativeAuditorHalo.BuildSparks(m0);sparks=Build(m0,"Auditor halo sparks",.9f);m0.Clear();
            NativeAuditorHalo.BuildFins(m0);fins=Build(m0,"Auditor halo flat flames",.7f);m0.Clear();
            NativeAuditorHalo.BuildGlow(m0,true);simpleGlow=Build(m0,"Auditor halo inner glow",.6f);
        }
        // A source-frame halo mesh into Unity: x mirrored, the winding turned over. The pieces placed by the shader (flames,
        // crackle, sprites) sit at the origin, so the bounds are the halo's reach in group space (`reach` on each side).
        private static Mesh Build(HaloMesh m,string name,float reach)
        {
            int n=m.Positions.Count;var p=new Vector3[n];var normals=new Vector3[n];var data=new List<Vector4>(n);var extra=new List<Vector4>(n);
            for(int i=0;i<n;i++){p[i]=CapturedAnimation.Vec(CapturedRig.Mirror(m.Positions[i]));normals[i]=CapturedAnimation.Vec(CapturedRig.Mirror(m.Normals[i]));var d=m.Data[i];var e=m.Extra[i];data.Add(new Vector4(d.X,d.Y,d.Z,d.W));extra.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
            var t=new int[m.Indices.Count];for(int i=0;i+2<t.Length;i+=3){t[i]=m.Indices[i];t[i+1]=m.Indices[i+2];t[i+2]=m.Indices[i+1];}
            var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=p;mesh.normals=normals;mesh.SetUVs(0,data);mesh.SetUVs(1,extra);mesh.triangles=t;
            mesh.bounds=new Bounds(new Vector3(0,.25f,0),new Vector3(2*reach,2*reach,2*reach));mesh.UploadMeshData(true);return mesh;
        }
        /// <summary>
        /// A halo on `head` (the head joint's world matrix: the cosmetic socket) at `time`, faded by `opacity`. `simple`: the
        /// reduced halo. `eye`: the camera the flames are ordered for (null: unordered). Drawn on `layer` for `camera` (null:
        /// every camera drawing the layer), casting no shadow.
        /// </summary>
        public void Draw(Matrix4x4 head,float time,float opacity,bool simple,Camera eye,int layer,Camera camera)
        {
            Light=0;if(opacity<=.001f)return;
            NativeAuditorHalo.Evaluate(time,simple,frame);
            var group=head*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(frame.GroupPosition)),CapturedAnimation.Quat(CapturedRig.Mirror(frame.GroupRotation)),Vector3.one*frame.GroupScale);
            // A collapsed corpse part (zero scale) draws nothing rather than NaN billboards.
            if(((Vector3)group.GetColumn(1)).sqrMagnitude<1e-8f)return;Centre=group.GetColumn(3);Light=frame.Light*opacity;
            if(!simple&&eye!=null){var local=group.inverse.MultiplyPoint3x4(eye.transform.position);NativeAuditorHalo.Sort(frame,new NVector(-local.x,local.y,local.z));}
            NativeAuditorHalo.Pack(frame,opacity,uniforms);
            Copy(uniforms.Bolts,bolts);Copy(uniforms.Flames,flameSlots);Copy(uniforms.Fins,finSlots);Copy(uniforms.Embers,embers);
            block.Clear();block.SetVector(AId,V(uniforms.A));block.SetVector(SpinId,V(uniforms.Spin));block.SetVector(PulseId,V(uniforms.Pulse));
            block.SetVectorArray(BoltsId,bolts);block.SetVectorArray(FlamesId,flameSlots);block.SetVectorArray(FinsId,finSlots);block.SetVectorArray(EmbersId,embers);
            Graphics.DrawMesh(arcs,group,opaque,layer,camera,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);
            if(simple)
            {
                Graphics.DrawMesh(fins,group,opaque,layer,camera,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);
                Graphics.DrawMesh(simpleGlow,group,additive,layer,camera,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);return;
            }
            Graphics.DrawMesh(glow,group,additive,layer,camera,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);
            Graphics.DrawMesh(flames,group,premultiplied,layer,camera,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);
            Graphics.DrawMesh(sparks,group,sparkle,layer,camera,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);
        }
        private static Vector4 V(System.Numerics.Vector4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);
        private static void Copy(System.Numerics.Vector4[] from,Vector4[] to){for(int i=0;i<to.Length;i++)to[i]=V(from[i]);}
        public void Dispose(){foreach(var o in new UnityEngine.Object[]{arcs,flames,glow,sparks,fins,simpleGlow,opaque,premultiplied,additive,sparkle})if(o!=null)CapturedAnimation.Release(o);}
    }

    // The arena's halos: the shared drawer, the clock each place animates it on, and a faint red light per halo on the
    // wearer's head and shoulders (a vertex light: no extra pass; off for reduced effects and turned off in any frame that
    // drew no halo).
    public sealed partial class ArenaPresentation
    {
        private NativeAuditorHaloDraw haloDraw;private bool haloFailed;
        private const int HaloLights=4;private readonly Light[] haloLights=new Light[HaloLights];private readonly int[] haloLit=new int[HaloLights];private int haloLightsUsed,haloLightFrame=-1;
        /// <summary>Reduced effects (the game sets WeaponEffectsQuality before each arena or killcam render).</summary>
        private bool HaloReduced=>WeaponEffectsQuality<.5f;
        /// <summary>
        /// Whether `hat` is the Auditor halo and it was drawn on `head` (else the caller queues the bank's hat model) at
        /// `clock`, faded by `opacity`, reduced when `simple`, on `layer` for `camera` (null: all), flames ordered for `eye`.
        /// </summary>
        private bool AuditorHalo(string hat,Matrix4x4 head,float clock,float opacity,bool simple,int layer,Camera camera,Camera eye=null)
        {
            if(hat!=NativeAuditorHalo.Hat||haloFailed)return false;
            if(haloDraw==null){haloDraw=NativeAuditorHaloDraw.Create(true);if(haloDraw==null){haloFailed=true;return false;}}
            haloDraw.Draw(head,clock,opacity,simple,eye!=null?eye:HaloEye,layer,camera);
            // Not for the view-model layer or a single camera's private shot (the light would reach every camera).
            if(!simple&&layer==0&&camera==null&&haloDraw.Light>.01f)HaloLight(haloDraw.Centre,haloDraw.Light);
            return true;
        }
        /// <summary>The camera the flames are ordered for: the arena camera the last frame used (DevEffects), the stage camera, else the main one.</summary>
        private Camera HaloEye=>sirenLookCamera!=null?sirenLookCamera:Camera.main;
        private void HaloLight(Vector3 at,float strength)
        {
            if(haloLightFrame!=Time.frameCount){haloLightFrame=Time.frameCount;haloLightsUsed=0;}
            if(haloLightsUsed>=HaloLights)return;int i=haloLightsUsed++;
            var light=haloLights[i];
            if(light==null){var go=new GameObject("Auditor halo light "+i);go.transform.SetParent(transform,false);light=go.AddComponent<Light>();light.type=LightType.Point;light.renderMode=LightRenderMode.ForceVertex;light.shadows=LightShadows.None;light.range=1.25f;light.color=NativeArena.ColorOf(NativeAuditorHalo.LightColor);haloLights[i]=light;}
            light.transform.position=at+Vector3.down*.05f;light.intensity=.8f*strength;haloLit[i]=Time.frameCount;if(!light.enabled)light.enabled=true;
        }
        // A light set in an earlier frame and not this one is turned off (no halo drew, or the presentation stopped rendering).
        private void LateUpdate(){for(int i=0;i<HaloLights;i++){var light=haloLights[i];if(light!=null&&light.enabled&&haloLit[i]<Time.frameCount)light.enabled=false;}}
        private void DisposeHalos(){haloDraw?.Dispose();haloDraw=null;haloFailed=false;for(int i=0;i<HaloLights;i++){if(haloLights[i]!=null)CapturedAnimation.Release(haloLights[i].gameObject);haloLights[i]=null;}}
    }
}
