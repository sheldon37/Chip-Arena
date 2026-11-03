using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV: the Poolrooms stand-in layout (Core NativePoolrooms.Preview) drawn at <see cref="Origin"/> until Astra's generator and
    /// the level's map land (docs/BACKROOMS.md "The Poolrooms"). Off by default; `--ca-poolrooms-preview` or the local capture tool
    /// turns it on.
    /// </summary>
    public static class NativePoolroomsPreview
    {
        public const string Argument="--ca-poolrooms-preview";
        public static bool On;public static int Seed=7,Width=30,Depth=30;public static Vector3 Origin=new Vector3(-1000,0,1000);
        /// <summary>Where the blocks are drawn round (null: the world camera), for a fixed capture camera.</summary>
        public static Vector3? Eye;
        private static int args=-1;
        /// <summary>On, by the flag or the command line.</summary>
        public static bool Active{get{if(args<0){args=0;try{foreach(var a in Environment.GetCommandLineArgs())if(a==Argument)args=1;}catch(Exception){}}return On||args==1;}}
    }

    // Draws a Poolrooms layout (the owner's Backrooms add-on's look): the layout built in blocks of cells (Core PoolroomsMesh: tiled
    // decks, turquoise pools and steps, tiled walls and arches, plaster ceilings, round pillars and lights), one static mesh per
    // surface per block, the solid ones through NativeBlockLit's pack path with the add-on's textures in the Backrooms bank
    // (ChipArenaPackBackrooms, docs/PACK-BANK.md), the water through NativePoolWater; the blocks near the eye only. The nearest
    // lights light the scene through the practical slots. Without the bank's textures nothing is drawn (the preview is DEV only).
    public sealed partial class ArenaPresentation
    {
        public const int PoolroomsBlockCells=8;public const float PoolroomsReach=46,PoolroomsLampRange=9f;
        public static readonly Vector3 PoolroomsLampColor=new Vector3(.36f,.34f,.3f);
        private static readonly string[] PoolroomsTextures={"backrooms-pool-floor","backrooms-pool-wall","backrooms-pool-basin","backrooms-pool-plaster",null,null};
        private const string PoolroomsWaterNormal="backrooms-pool-water";
        // Each solid surface's tint (multipliers over its texture: the add-on's baked colours as they are) and glow.
        private static readonly Color[] PoolroomsTint={Color.white,Color.white,Color.white,Color.white,Color.white,new Color(1f,.97f,.92f)};
        private static readonly float[] PoolroomsGlow={0,0,0,.12f,0,2.6f};
        private sealed class PoolroomsBlock{public Vector3 Centre;public readonly Mesh[] Meshes=new Mesh[(int)PoolroomsMesh.Surface.Count];}
        private readonly List<PoolroomsBlock> poolroomsBlocks=new List<PoolroomsBlock>();private PoolroomsLayout poolroomsLayout;private Vector3 poolroomsOrigin;
        private Material[] poolroomsMats;private MaterialPropertyBlock poolroomsProps;
        private readonly float[] poolroomsPos=new float[NativeMapLooks.MaxLights*4],poolroomsRgb=new float[NativeMapLooks.MaxLights*3];
        /// <summary>The Poolrooms blocks drawn last frame (budget lines).</summary>
        public int PoolroomsBlocksDrawn{get;private set;}

        private void PoolroomsWorld()
        {
            PoolroomsBlocksDrawn=0;if(!NativePoolroomsPreview.Active)return;var eye=WorldEye;if(eye==null)return;
            if(NativePackBanks.Texture(PoolroomsTextures[0])==null)return;
            var p=NativePoolroomsPreview.Origin;
            if(poolroomsLayout==null||poolroomsLayout.Seed!=NativePoolroomsPreview.Seed||poolroomsLayout.Width!=NativePoolroomsPreview.Width||poolroomsLayout.Depth!=NativePoolroomsPreview.Depth||poolroomsOrigin!=p)
                PoolroomsBuild(NativePoolrooms.Preview(NativePoolroomsPreview.Seed,NativePoolroomsPreview.Width,NativePoolroomsPreview.Depth),p);
            if(!PoolroomsMaterials())return;
            var e=NativePoolroomsPreview.Eye??eye.transform.position;float r2=PoolroomsReach*PoolroomsReach;
            foreach(var b in poolroomsBlocks)
            {
                var dc=b.Centre-e;dc.y=0;if(dc.sqrMagnitude>r2)continue;PoolroomsBlocksDrawn++;
                for(int s=0;s<b.Meshes.Length;s++)
                {
                    if(b.Meshes[s]==null)continue;bool solid=s!=(int)PoolroomsMesh.Surface.Water&&s!=(int)PoolroomsMesh.Surface.Light;
                    Graphics.DrawMesh(b.Meshes[s],Matrix4x4.identity,poolroomsMats[s],0,null,0,poolroomsProps,solid?ShadowCastingMode.TwoSided:ShadowCastingMode.Off,solid);
                }
            }
            var origin=new NVector(p.x,p.y,p.z);var at=new NVector(e.x,e.y,e.z);
            NativePoolrooms.NearLamps(poolroomsLayout,origin,at,NativeMapLooks.MaxLights,poolroomsPos,poolroomsRgb,new NVector(PoolroomsLampColor.x,PoolroomsLampColor.y,PoolroomsLampColor.z),PoolroomsLampRange);
            float w=poolroomsLayout.Width*PoolroomsLayout.Cell,d=poolroomsLayout.Depth*PoolroomsLayout.Cell;
            if(e.x>p.x&&e.x<p.x+w&&e.z>p.z&&e.z<p.z+d&&e.y<p.y+PoolroomsLayout.Height+PoolroomsLayout.Rise*2)
            {
                NativeLighting.PickedLights(poolroomsPos,poolroomsRgb);
                // DEV: inside the preview no sun reaches; the Poolrooms' even, cool-white wash off the tile and a pale haze down the
                // halls (the level's own map look replaces this once its map exists). The toon style is off there, as in Level 0.
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=PoolroomsAmbient;RenderSettings.ambientEquatorColor=PoolroomsAmbient*.95f;RenderSettings.ambientGroundColor=PoolroomsAmbient*1.25f;
                if(RenderSettings.sun!=null){RenderSettings.sun.intensity=0;RenderSettings.sun.shadows=LightShadows.None;}
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=PoolroomsHaze;RenderSettings.fogStartDistance=12;RenderSettings.fogEndDistance=130;
                NativePackStage.Glow(1);if(NativeLighting.StyleActive)NativeLighting.StyleOff();
            }
        }
        private static readonly Color PoolroomsAmbient=new Color(.6f,.6f,.57f),PoolroomsHaze=new Color(.72f,.74f,.72f);
        // Builds every block of the layout once (static meshes).
        private void PoolroomsBuild(PoolroomsLayout l,Vector3 origin)
        {
            PoolroomsRelease();poolroomsLayout=l;poolroomsOrigin=origin;var builder=new PoolroomsMesh();var o=new NVector(origin.x,origin.y,origin.z);int n=PoolroomsBlockCells;float c=PoolroomsLayout.Cell;
            for(int z0=0;z0<l.Depth;z0+=n)for(int x0=0;x0<l.Width;x0+=n)
            {
                builder.Build(l,o,x0,z0,x0+n,z0+n);var b=new PoolroomsBlock{Centre=origin+new Vector3((x0+n*.5f)*c,0,(z0+n*.5f)*c)};
                for(int s=0;s<b.Meshes.Length;s++)b.Meshes[s]=PoolroomsMeshOf(builder.Parts[s],"Poolrooms "+(PoolroomsMesh.Surface)s+" "+x0+","+z0);
                poolroomsBlocks.Add(b);
            }
        }
        private static Mesh PoolroomsMeshOf(PoolroomsMesh.Part p,string name)
        {
            if(p.Indices.Count==0)return null;int n=p.Positions.Count;var v=new Vector3[n];var nr=new Vector3[n];var uv=new Vector2[n];
            for(int i=0;i<n;i++){var q=p.Positions[i];v[i]=new Vector3(q.X,q.Y,q.Z);var m=p.Normals[i];nr[i]=new Vector3(m.X,m.Y,m.Z);uv[i]=new Vector2(p.Uv[i].X,p.Uv[i].Y);}
            var mesh=new Mesh{name=name};if(n>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=v;mesh.normals=nr;mesh.uv=uv;mesh.SetTriangles(p.Indices,0,true);mesh.UploadMeshData(true);return mesh;
        }
        private void PoolroomsRelease(){foreach(var b in poolroomsBlocks)foreach(var m in b.Meshes)if(m!=null)CapturedAnimation.Release(m);poolroomsBlocks.Clear();}
        // One NativeBlockLit pack material per solid surface (its texture, tint and glow), NativePoolWater for the water, and the
        // draws' constant block values.
        private bool PoolroomsMaterials()
        {
            if(poolroomsMats!=null)return true;var shader=Resources.Load<Shader>("NativeBlockLit");var water=Resources.Load<Shader>("NativePoolWater");if(shader==null||water==null)return false;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;poolroomsMats=new Material[(int)PoolroomsMesh.Surface.Count];
            for(int s=0;s<poolroomsMats.Length;s++)
            {
                if(s==(int)PoolroomsMesh.Surface.Water)
                {
                    var wm=new Material(water){name="Poolrooms water",renderQueue=3000};var ripples=NativePackBanks.Texture(PoolroomsWaterNormal);
                    if(ripples!=null){ripples.wrapMode=TextureWrapMode.Repeat;wm.SetTexture("_PoolNormal",ripples);}
                    wm.SetFloat("_PoolRepeat",PoolroomsMesh.WaterRepeat);poolroomsMats[s]=wm;continue;
                }
                var m=new Material(shader){name="Poolrooms "+(PoolroomsMesh.Surface)s};m.EnableKeyword(NativePackStage.Keyword);
                var tex=PoolroomsTextures[s]!=null?NativePackBanks.Texture(PoolroomsTextures[s]):null;if(tex!=null)tex.wrapMode=TextureWrapMode.Repeat;
                m.SetTexture(BrTexId,tex!=null?(Texture)tex:Texture2D.whiteTexture);m.SetColor(BrColorId,linear?PoolroomsTint[s].linear:PoolroomsTint[s]);m.SetFloat(BrEmitId,PoolroomsGlow[s]);m.SetFloat(BrCutoffId,-1);
                poolroomsMats[s]=m;
            }
            poolroomsProps=new MaterialPropertyBlock();poolroomsProps.SetVector(BrRestId,Vector4.zero);poolroomsProps.SetVector(BrLookId,new Vector4(0,1,0,0));poolroomsProps.SetVector(BrBlockColorId,new Vector4(1,1,1,1));
            return true;
        }
    }
}
