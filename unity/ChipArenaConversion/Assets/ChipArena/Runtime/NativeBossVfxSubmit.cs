using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zone bosses' particles (Core NativeBossVfx: THE CONVERTER's and THE 6:47's sparks, spatter, smoke, steam, dust, fire,
    /// debris and glows) handed to the shared effects renderer (NativeVfxDraw: motion-stretched sparks with a hot core, soft lit
    /// atlas puffs faded against the scene's depth, soft fire puffs, lit rubble, soft HDR glows), item by item in the order
    /// Core built them (the renderer sorts its billboards itself). Allocation free.
    /// </summary>
    internal static class NativeBossVfxSubmit
    {
        private static readonly Color HotRubble=new Color(1.6f,.62f,.16f,0);
        public static void Submit(NativeBossVfx fx,NativeVfxDraw vfx)
        {
            if(fx==null||vfx==null)return;var items=fx.Items;
            for(int i=0;i<fx.Count;i++)
            {
                ref var it=ref items[i];var at=new Vector3(it.At.X,it.At.Y,it.At.Z);var axis=new Vector3(it.Axis.X,it.Axis.Y,it.Axis.Z);
                switch(it.Kind)
                {
                    case BossVfxKind.Glow:vfx.Glow(at,new Color(it.Rgb.X,it.Rgb.Y,it.Rgb.Z,it.A),it.Size,it.P0,it.U,it.Seed);break;
                    case BossVfxKind.Spark:vfx.Streak(at,axis,new Color(it.Rgb.X,it.Rgb.Y,it.Rgb.Z,it.A),it.Size,it.P0,it.P1,it.P2,it.U,it.Seed);break;
                    case BossVfxKind.Smoke:vfx.Vapor(at,new Color(it.Rgb.X,it.Rgb.Y,it.Rgb.Z,0),it.A,it.Size,it.U,it.Seed,it.Floor,it.P0);break;
                    case BossVfxKind.Fire:vfx.Fireball(at,new Color(it.Rgb.X,it.Rgb.Y,it.Rgb.Z,0),it.A,it.Size,it.U,it.Seed,it.Floor);break;
                    case BossVfxKind.Rubble:
                    {
                        float angle=axis.magnitude;var turn=angle>1e-5f?Quaternion.AngleAxis(angle*Mathf.Rad2Deg,axis/angle):Quaternion.identity;
                        vfx.Rubble(at,turn,it.Size,(int)it.Seed,new Color(it.Rgb.X,it.Rgb.Y,it.Rgb.Z,0),HotRubble*it.A);break;
                    }
                    case BossVfxKind.Drop:vfx.Drop(at,axis,new Color(it.Rgb.X,it.Rgb.Y,it.Rgb.Z,it.A),it.Size,it.U,it.Seed);break;
                }
            }
        }
    }

    /// <summary>
    /// THE CONVERTER's molten metal mesh (Core NativeMoltenFlow: the pour's tube, the pools, the splash crown, droplets, lumps and
    /// strips) as one dynamic mesh drawn with Resources/NativeMoltenFlow.shader in the opaque queue, layer 0, no shadows: the vertex
    /// array is uploaded as Core built it (Core MoltenVertex: position, normal, three float4s) with the advanced Mesh API, only the
    /// used range each frame. One draw. Allocation free per frame.
    /// </summary>
    internal sealed class NativeMoltenFlowDraw:IDisposable
    {
        private static readonly VertexAttributeDescriptor[] Layout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4)
        };
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private readonly Mesh mesh;private readonly Material material;private int vertexCapacity=-1,indexCapacity=-1;
        private NativeMoltenFlowDraw(Shader shader)
        {
            material=new Material(shader){name="THE CONVERTER / molten flow",hideFlags=HideFlags.HideAndDontSave};
            mesh=new Mesh{name="THE CONVERTER / molten flow",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
        }
        /// <summary>The draw, or null when the shader is missing or unsupported (the molten metal is then not drawn).</summary>
        public static NativeMoltenFlowDraw Create(){var shader=Resources.Load<Shader>("NativeMoltenFlow");return shader!=null&&shader.isSupported?new NativeMoltenFlowDraw(shader):null;}
        /// <summary>Draws this frame's molten mesh (nothing when it is empty); true when it drew.</summary>
        public bool Draw(NativeMoltenFlow melt)
        {
            if(melt==null||melt.VertexCount==0||melt.IndexCount==0)return false;
            if(vertexCapacity!=melt.V.Length){mesh.SetVertexBufferParams(melt.V.Length,Layout);vertexCapacity=melt.V.Length;}
            if(indexCapacity!=melt.T.Length){mesh.SetIndexBufferParams(melt.T.Length,IndexFormat.UInt32);indexCapacity=melt.T.Length;mesh.subMeshCount=1;}
            mesh.SetVertexBufferData(melt.V,0,0,melt.VertexCount,0,Quiet);mesh.SetIndexBufferData(melt.T,0,0,melt.IndexCount,Quiet);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,melt.IndexCount){bounds=Everywhere,firstVertex=0,vertexCount=melt.VertexCount},Quiet);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,true);return true;
        }
        public void Dispose(){CapturedAnimation.Release(mesh);CapturedAnimation.Release(material);}
    }
}
