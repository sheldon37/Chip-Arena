using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The weapons milestone's drawer (owner 2026-10-11 effects overhaul): every muzzle star, tracer, impact shard and spark,
    /// energy trail, slash crescent and charge streak of a frame in one premultiplied dynamic mesh per camera
    /// (Resources/NativeWeaponFx.shader): the match camera on layer 0 and, in first person, the view-model camera on layer
    /// 10, so a first-person muzzle star and spell charge sit on the view weapon. Covering quads (slash underlays, smoke
    /// wisps) are queued ahead of the additive ones in each mesh. Billboards and axis cards face the camera in the vertex
    /// shader; the petals, crescents and ribbons arrive as world corners. Arrays only grow; nothing allocates per frame once
    /// warm. Cosmetic only: it draws what it is handed and reads nothing of the match.
    /// </summary>
    public sealed class NativeWeaponFxDraw : IDisposable
    {
        /// <summary>Shader modes (NativeWeaponFx.shader TEXCOORD0.z) and glow profiles (TEXCOORD0.w).</summary>
        public const float Petal=0,Crescent=1,Ribbon=2,GlowMode=3,WispMode=4,StreakMode=5,FlameMode=6,ShardMode=7;
        public const float Soft=0,HotCore=1,RingProfile=2,Sparkle=3;
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex{public Vector3 Position;public Color Color;public Vector4 Uv,Extra;}
        private static readonly VertexAttributeDescriptor[] Layout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4)
        };
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        // One camera's mesh: covering quads (under) then additive ones (over), four vertices a quad, a fixed index pattern.
        private sealed class Layer
        {
            public readonly Mesh Mesh;public Vertex[] Under=new Vertex[256],Over=new Vertex[1024],Merged=new Vertex[1280];public int UnderCount,OverCount;
            private int[] indices=new int[0];private int vertexCapacity=-1,indexCapacity=-1;
            public Layer(string name){Mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};Mesh.MarkDynamic();}
            public void Clear(){UnderCount=0;OverCount=0;}
            public int Quads=>(UnderCount+OverCount)/4;
            public int Add(bool under)
            {
                if(under){if(UnderCount+4>Under.Length)Array.Resize(ref Under,Under.Length*2);int at=UnderCount;UnderCount+=4;return at;}
                if(OverCount+4>Over.Length)Array.Resize(ref Over,Over.Length*2);int o=OverCount;OverCount+=4;return o;
            }
            public void Draw(Material material,int layer,Camera camera,MaterialPropertyBlock properties)
            {
                int count=UnderCount+OverCount;if(count==0||material==null||camera==null)return;
                if(Merged.Length<count)Array.Resize(ref Merged,Math.Max(count,Merged.Length*2));
                Array.Copy(Under,0,Merged,0,UnderCount);Array.Copy(Over,0,Merged,UnderCount,OverCount);
                if(vertexCapacity<Merged.Length){vertexCapacity=Merged.Length;Mesh.SetVertexBufferParams(vertexCapacity,Layout);}
                int need=vertexCapacity/4*6;
                if(indexCapacity<need)
                {
                    indices=new int[need];for(int q=0;q<need/6;q++){int i=q*6,v=q*4;indices[i]=v;indices[i+1]=v+1;indices[i+2]=v+2;indices[i+3]=v;indices[i+4]=v+2;indices[i+5]=v+3;}
                    indexCapacity=need;Mesh.SetIndexBufferParams(need,IndexFormat.UInt32);Mesh.SetIndexBufferData(indices,0,0,need,Quiet);Mesh.subMeshCount=1;
                }
                Mesh.SetVertexBufferData(Merged,0,0,count,0,Quiet);
                Mesh.SetSubMesh(0,new SubMeshDescriptor(0,count/4*6){bounds=Everywhere,firstVertex=0,vertexCount=count},Quiet);Mesh.bounds=Everywhere;
                Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,layer,camera,0,properties,ShadowCastingMode.Off,false);
            }
        }
        private readonly Layer world=new Layer("Native weapon fx (world)"),view=new Layer("Native weapon fx (view model)");
        private readonly Material material;private readonly MaterialPropertyBlock worldProperties=new MaterialPropertyBlock(),viewProperties=new MaterialPropertyBlock();
        private static readonly int SoftId=Shader.PropertyToID("_WeaponFxSoft");
        /// <summary>The low graphics tier: no soft particles (ArenaPresentation.LowGraphics).</summary>
        public bool Low;
        /// <summary>Quads queued this frame for the match camera and the view-model camera (checks).</summary>
        public int WorldQuads=>world.Quads;public int ViewQuads=>view.Quads;
        public NativeWeaponFxDraw()
        {
            var shader=Resources.Load<Shader>("NativeWeaponFx");if(shader==null)throw new InvalidOperationException("NativeWeaponFx shader is missing");
            material=new Material(shader){name="Native weapon fx",renderQueue=(int)RenderQueue.Transparent+6,hideFlags=HideFlags.HideAndDontSave};
        }
        public void Begin(){world.Clear();view.Clear();}
        private Layer L(bool first)=>first?view:world;
        private static void Put(Vertex[] v,int at,Vector3 p,Color c,Vector4 uv,Vector4 extra){v[at]=new Vertex{Position=p,Color=c,Uv=uv,Extra=extra};}
        /// <summary>A camera-facing glow sprite (profile: Soft, HotCore, RingProfile or Sparkle). color: linear, premultiplied (alpha 0: additive).</summary>
        public void Glow(bool first,Vector3 at,Color color,float size,float profile,float roll=0)
        {
            if(!(size>1e-4f))return;var l=L(first);bool under=color.a>0;int i=l.Add(under);var v=under?l.Under:l.Over;
            Put(v,i,at,color,new Vector4(0,0,GlowMode,profile),new Vector4(-1,-1,size,roll));Put(v,i+1,at,color,new Vector4(0,0,GlowMode,profile),new Vector4(1,-1,size,roll));
            Put(v,i+2,at,color,new Vector4(0,0,GlowMode,profile),new Vector4(1,1,size,roll));Put(v,i+3,at,color,new Vector4(0,0,GlowMode,profile),new Vector4(-1,1,size,roll));
        }
        /// <summary>A soft smoke wisp (color premultiplied, alpha its coverage).</summary>
        public void Wisp(bool first,Vector3 at,Color color,float size,float seed,float roll)
        {
            if(!(size>1e-4f)||!(color.a>1e-4f))return;var l=L(first);int i=l.Add(true);var v=l.Under;
            Put(v,i,at,color,new Vector4(0,0,WispMode,seed),new Vector4(-1,-1,size,roll));Put(v,i+1,at,color,new Vector4(0,0,WispMode,seed),new Vector4(1,-1,size,roll));
            Put(v,i+2,at,color,new Vector4(0,0,WispMode,seed),new Vector4(1,1,size,roll));Put(v,i+3,at,color,new Vector4(0,0,WispMode,seed),new Vector4(-1,1,size,roll));
        }
        /// <summary>A camera-facing card along tail to head (StreakMode: a capsule with round ends; FlameMode, ShardMode: exactly the segment).</summary>
        public void Axis(bool first,Vector3 tail,Vector3 head,Color color,float width,float mode,float param)
        {
            Vector3 d=head-tail;if(!(width>1e-4f)||d.sqrMagnitude<1e-10f&&mode>StreakMode+.5f)return;var mid=(tail+head)*.5f;var extra=new Vector4(d.x,d.y,d.z,width);
            var l=L(first);bool under=color.a>0;int i=l.Add(under);var v=under?l.Under:l.Over;
            Put(v,i,mid,color,new Vector4(-1,-1,mode,param),extra);Put(v,i+1,mid,color,new Vector4(1,-1,mode,param),extra);Put(v,i+2,mid,color,new Vector4(1,1,mode,param),extra);Put(v,i+3,mid,color,new Vector4(-1,1,mode,param),extra);
        }
        /// <summary>An oriented quad from world corners a, b, c, d (in order round it) with their shape coordinates.</summary>
        public void Quad(bool first,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud,Color color,float mode,float param,float edge=0)
        {
            var l=L(first);bool under=color.a>0;int i=l.Add(under);var v=under?l.Under:l.Over;var extra=new Vector4(edge,0,0,0);
            Put(v,i,a,color,new Vector4(ua.x,ua.y,mode,param),extra);Put(v,i+1,b,color,new Vector4(ub.x,ub.y,mode,param),extra);Put(v,i+2,c,color,new Vector4(uc.x,uc.y,mode,param),extra);Put(v,i+3,d,color,new Vector4(ud.x,ud.y,mode,param),extra);
        }
        /// <summary>Draws the frame: the world mesh to the match camera (layer 0; soft where the camera renders a depth texture,
        /// which the shared effects renderer asks for on the normal tiers) and the view mesh to the view-model camera (layer 10,
        /// first person only, never soft); then the queues are empty.</summary>
        public void Submit(Camera camera,Camera viewCamera)
        {
            if(camera!=null){worldProperties.SetFloat(SoftId,!Low&&(camera.depthTextureMode&DepthTextureMode.Depth)!=0?1:0);world.Draw(material,0,camera,worldProperties);}
            if(viewCamera!=null){viewProperties.SetFloat(SoftId,0);view.Draw(material,10,viewCamera,viewProperties);}
            Begin();
        }
        public void Dispose(){Begin();CapturedAnimation.Release(world.Mesh);CapturedAnimation.Release(view.Mesh);CapturedAnimation.Release(material);}
    }


    /// <summary>
    /// The geometry of each weapons-milestone effect from the Core rules (Core/NativeWeaponFx.cs) into a NativeWeaponFxDraw:
    /// pure functions of their inputs (positions, axes, seeds, ages, quality), shared by the arena hooks
    /// (NativeWeaponFxPresentation.cs) and the offline render harness.
    /// </summary>
    public static class NativeWeaponFxShapes
    {
        public static Color Fx(in FxColor c)=>new Color(c.R,c.G,c.B,c.A);
        /// <summary>A linear colour from an sRGB hex, times gain, additive (alpha 0).</summary>
        public static Color Lin(int hex,float gain=1){var c=NativeWeaponFx.Linear(hex,gain);return new Color(c.R,c.G,c.B,0);}
        public static Vector3 V(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        private static Color Add(Color c){c.a=0;return c;}
        /// <summary>
        /// A gun's muzzle star at `muzzle` with the barrel frame x, y (across) and z (along), `scale` the weapon's world scale
        /// (the view model's size in first person), `age` since the shot and `dim` the rig's block-cooldown factor. Returns the
        /// light pulse's intensity (0: none).
        /// </summary>
        public static float Muzzle(NativeWeaponFxDraw draw,bool first,Vector3 muzzle,Vector3 x,Vector3 y,Vector3 z,float scale,in MuzzleLook look,long seed,double age,float quality,bool low,float dim)
        {
            if(!(age>=0&&age<NativeMuzzleFx.Span))return 0;
            if(NativeMuzzleFx.Glow(look,age,out float size,out var glow))draw.Glow(first,muzzle+z*(.05f*scale),Fx(glow)*dim,size*scale,NativeWeaponFxDraw.HotCore);
            float petalGain=3.2f*look.Gain*dim;
            for(int k=0;k<NativeMuzzleFx.Petals;k++)
            {
                if(!NativeMuzzleFx.Petal(look,seed,k,age,out float roll,out float length,out float width,out float alpha))continue;
                Vector3 radial=x*Mathf.Cos(roll)+y*Mathf.Sin(roll),tangent=Vector3.Cross(z,radial);Vector3 along=(radial*Mathf.Cos(look.Tilt)+z*Mathf.Sin(look.Tilt)).normalized;
                Vector3 pivot=muzzle+z*(.01f*scale),tip=pivot+along*(length*scale),half=tangent*(width*.5f*scale);
                draw.Quad(first,pivot-half,tip-half,tip+half,pivot+half,new Vector2(0,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(0,1),Lin(NativeMuzzleFx.PetalColor,petalGain*alpha),NativeWeaponFxDraw.Petal,0);
            }
            if(NativeMuzzleFx.Side(look,seed,age,out float side,out float sideWidth,out float sideAlpha))
            {
                var c=Add(Color.Lerp(Lin(NativeMuzzleFx.PetalColor),Lin(NativeMuzzleFx.EdgeColor),(float)(age/look.Life))*(2.8f*look.Gain*dim*sideAlpha));
                draw.Axis(first,muzzle,muzzle+z*(side*scale),c,sideWidth*scale,NativeWeaponFxDraw.FlameMode,(float)NativeWeaponFx.Hash(seed,9));
            }
            int sparks=NativeMuzzleFx.SparkCount(look,quality,low);
            for(int k=0;k<sparks;k++)
            {
                if(!NativeMuzzleFx.Spark(seed,k,age,out var local,out var velocity,out float drop,out float alpha))continue;
                Vector3 head=muzzle+(x*local.X+y*local.Y+z*local.Z)*scale+Vector3.down*(drop*scale),dir=x*velocity.X+y*velocity.Y+z*velocity.Z;
                var c=Add(Color.Lerp(new Color(1,1,1,0),Lin(NativeMuzzleFx.SparkColor),(float)(age/NativeMuzzleFx.SparkLifeMax))*(2.6f*alpha*dim));
                draw.Axis(first,head-dir*(.014f*scale)-z*(.02f*scale),head,c,.016f*scale,NativeWeaponFxDraw.StreakMode,.8f);
            }
            int wisps=NativeMuzzleFx.WispCount(quality,low);
            for(int k=0;k<wisps;k++)
            {
                if(!NativeMuzzleFx.Wisp(seed,k,age,out var local,out float rise,out float wispSize,out float alpha))continue;
                float cover=alpha*(first?.55f:1);
                draw.Wisp(first,muzzle+(x*local.X+y*local.Y+z*local.Z)*scale+Vector3.up*(rise*scale),new Color(.62f*cover,.64f*cover,.7f*cover,cover),wispSize*scale,(float)NativeWeaponFx.Hash(seed,80+k)*10,(float)NativeWeaponFx.Hash(seed,90+k)*6.28f);
            }
            return NativeMuzzleFx.LightIntensity*NativeMuzzleFx.Light(age)*dim;
        }
        /// <summary>
        /// A slash crescent swept from a blade's samples (Core/NativeSlashTrail): the dark underlay, then the additive band from
        /// the look's inner share of the blade out to its tip, tail to head along the tip's path (the crescent shader's x), with
        /// the bright outer edge on the tip's path. `alpha`: the strike's opacity (NativeSlashFx.Frame). Scratch arrays are the
        /// caller's (Capacity long). False when the blade has not swept through the front.
        /// </summary>
        public static bool Swept(NativeWeaponFxDraw draw,NativeSlashTrail trail,NVector3 centre,float yaw,int color,in SlashLook look,float alpha,float quality,NVector3[] inner,NVector3[] outer,float[] share)
        {
            if(!(alpha>.002f))return false;float k=NativeSlashTrail.Inner(look),gain=(quality<.5f?.8f:1)*alpha;
            if(look.Under>0)
            {
                int m=trail.Band(centre,yaw,k*.96f,1.02f,inner,outer,share);if(m<2)return false;float cover=look.Under*alpha;var dark=new Color(.012f*cover,.016f*cover,.03f*cover,cover);
                for(int j=0;j+1<m;j++)draw.Quad(false,V(inner[j]),V(inner[j+1]),V(outer[j+1]),V(outer[j]),new Vector2(share[j],0),new Vector2(share[j+1],0),new Vector2(share[j+1],1),new Vector2(share[j],1),dark,NativeWeaponFxDraw.Crescent,1,0);
            }
            int n=trail.Band(centre,yaw,k,1,inner,outer,share);if(n<2)return false;var c=Add(Lin(color,2.6f*gain));
            for(int j=0;j+1<n;j++)draw.Quad(false,V(inner[j]),V(inner[j+1]),V(outer[j+1]),V(outer[j]),new Vector2(share[j],0),new Vector2(share[j+1],0),new Vector2(share[j+1],1),new Vector2(share[j],1),c,NativeWeaponFxDraw.Crescent,0,look.Edge);
            return true;
        }
        /// <summary>The charge streaks, rings and core round an orb at `centre` (`scale`: the orb frame's world scale).</summary>
        public static void Charge(NativeWeaponFxDraw draw,bool first,Vector3 centre,float scale,Color tint,long seed,double time,float quality,bool low)
        {
            float radius=NativeChargeFx.Radius*scale;var white=new Color(1,1,1,0);tint.a=0;
            double rate=NativeChargeFx.StreakRate(quality,low);NativeChargeFx.Window(rate,time,out long first0,out long last);
            for(long k=first0;k<=last;k++)
            {
                if(!NativeChargeFx.Streak(seed,k,rate,time,out var dir,out float u,out float alpha,out float whiteShare))continue;
                Vector3 d=V(dir);float r0=radius*(1-u)+radius*.12f;
                draw.Axis(first,centre+d*r0,centre+d*Mathf.Max(radius*.1f,r0-radius*.7f),Add(Color.Lerp(tint,white,whiteShare)*(3.2f*alpha)),radius*.055f,NativeWeaponFxDraw.StreakMode,.9f);
            }
            long ring=(long)Math.Floor(time/NativeChargeFx.RingEvery);
            for(long k=ring-3;k<=ring;k++)if(NativeChargeFx.Ring(k,time,out float ringScale,out float ringAlpha))draw.Glow(first,centre,Add(tint*(2.4f*ringAlpha)),radius*2.6f*ringScale,NativeWeaponFxDraw.RingProfile);
            draw.Glow(first,centre,Add(Color.Lerp(tint,white,.5f)*1.1f),radius*.75f,NativeWeaponFxDraw.HotCore);
        }
        /// <summary>A bullet tracer: a thin hot streak from the round back along its flight, and a soft warm sheath.</summary>
        public static void Tracer(NativeWeaponFxDraw draw,Vector3 at,Vector3 velocity,float travelled,string spec)
        {
            float speed=velocity.magnitude;if(!(speed>.01f))return;float length=Math.Max(.08f,NativeTracerFx.Length(speed,travelled)),width=NativeTracerFx.Width(spec);Vector3 dir=velocity/speed;
            draw.Axis(false,at-dir*length,at,Lin(NativeTracerFx.EdgeColor,NativeTracerFx.CoreGain),width,NativeWeaponFxDraw.StreakMode,.85f);
            draw.Axis(false,at-dir*(length*.6f),at,Lin(NativeTracerFx.EdgeColor,.5f),width*4,NativeWeaponFxDraw.StreakMode,.95f);
        }
        /// <summary>An energy shot's white-hot core and school glow (pulse: its slow breathing, about 1). The core sits just in
        /// front of the shot's opaque model, toward `eye`, so the model reads as glowing from inside rather than hiding it.</summary>
        public static void EnergyCore(NativeWeaponFxDraw draw,Vector3 at,Vector3 eye,Color halo,float radius,string spec,float pulse)
        {
            halo.a=0;Vector3 toward=eye-at;float d=toward.magnitude;Vector3 front=d>radius*2?at+toward*(radius*1.05f/d):at;
            draw.Glow(false,front,Add(Color.Lerp(halo,new Color(1,1,1,0),NativeTrailFx.CoreWhite(spec))*2.6f),NativeTrailFx.CoreSize(radius),NativeWeaponFxDraw.HotCore);
            draw.Glow(false,at,Add(halo*(.6f*pulse)),NativeTrailFx.GlowSize(radius)*pulse,NativeWeaponFxDraw.Soft);
        }
        /// <summary>A trail sprite's colour at birth: the halo, a little whitened.</summary>
        public static Color SpriteColor(Color halo)=>Add(Color.Lerp(halo*1.1f,new Color(1.2f,1.2f,1.2f,0),.08f));
        /// <summary>One trail sprite at `age` of its life.</summary>
        public static void Sprite(NativeWeaponFxDraw draw,Vector3 at,Vector3 drift,Color color,float size,float life,float age)
        {
            if(!(age>=0&&age<life))return;float u=age/life;draw.Glow(false,at+drift*age,Add(color*NativeTrailFx.SpriteAlpha(u)),size*NativeTrailFx.SpriteSize(u),NativeWeaponFxDraw.Soft);
        }
        /// <summary>The short HDR ribbon through samples [first, count) (newest last), tapering to the tail, facing `eye`.</summary>
        public static void Ribbon(NativeWeaponFxDraw draw,Vector3[] points,float[] times,int first,int count,float now,Vector3 eye,Color halo,float radius)
        {
            float width=radius*2f,life=(float)NativeTrailFx.RibbonLife;var c=Add(halo*1.4f);
            for(int i=first+1;i<count;i++)
            {
                Vector3 a=points[i-1],b=points[i],mid=(a+b)*.5f,side=Vector3.Cross(b-a,eye-mid);if(side.sqrMagnitude<1e-10f)continue;side=side.normalized;
                float ua=1-Mathf.Clamp01((now-times[i-1])/life),ub=1-Mathf.Clamp01((now-times[i])/life);
                Vector3 sa=side*(width*.5f*(.25f+.75f*ua)),sb=side*(width*.5f*(.25f+.75f*ub));
                draw.Quad(false,a-sa,b-sb,b+sb,a+sa,new Vector2(ua,-1),new Vector2(ub,-1),new Vector2(ub,1),new Vector2(ua,1),c,NativeWeaponFxDraw.Ribbon,0);
            }
        }
        /// <summary>A hard-surface impact at `age`: the quick glow, the curved shards and the cooling sparks.</summary>
        public static void Impact(NativeWeaponFxDraw draw,Vector3 point,Vector3 normal,long seed,Color tint,double age,int shards,int sparks)
        {
            if(!(age>=0&&age<NativeImpactFx.Life))return;var n=new NVector3(normal.x,normal.y,normal.z);Vector3 at=point+normal*.02f;tint.a=0;
            if(NativeImpactFx.Glow(age,out float size,out var glow))draw.Glow(false,at+normal*.15f,Fx(glow),size,NativeWeaponFxDraw.Soft);
            for(int k=0;k<shards;k++)
            {
                if(!NativeImpactFx.Shard(seed,k,age,n,out var offset,out var dir,out float length,out float width,out float alpha))continue;
                Vector3 head=at+V(offset)+V(dir)*(length*.5f);draw.Axis(false,head-V(dir)*length,head,Add(Color.Lerp(tint,new Color(1,1,1,0),.4f)*(2.2f*alpha)),width,NativeWeaponFxDraw.ShardMode,0);
            }
            for(int k=0;k<sparks;k++)
            {
                if(!NativeImpactFx.Spark(seed,k,age,n,out var offset,out var velocity,out var color))continue;
                Vector3 head=at+V(offset),vel=V(velocity);float speed=vel.magnitude;
                draw.Axis(false,head-(speed>1e-3f?vel/speed:normal)*(.02f+speed*.018f),head,Fx(color),.016f,NativeWeaponFxDraw.StreakMode,.85f);
            }
        }
    }
}
