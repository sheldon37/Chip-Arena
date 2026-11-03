using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV: the Backrooms stand-in layout (Core NativeBackrooms.Preview) drawn at <see cref="Origin"/> until Astra's generator and
    /// the level's map land (docs/BACKROOMS.md). Off by default; `--ca-backrooms-preview` or the local capture tool turns it on.
    /// </summary>
    public static class NativeBackroomsPreview
    {
        public const string Argument="--ca-backrooms-preview";
        public static bool On;public static int Seed=7,Width=24,Depth=24;public static Vector3 Origin=new Vector3(1000,0,1000);
        /// <summary>Where the blocks are drawn round (null: the world camera), for a fixed capture camera.</summary>
        public static Vector3? Eye;
        private static int args=-1;
        /// <summary>On, by the flag or the command line.</summary>
        public static bool Active{get{if(args<0){args=0;try{foreach(var a in Environment.GetCommandLineArgs())if(a==Argument)args=1;}catch(Exception){}}return On||args==1;}}
    }

    // Draws a Backrooms layout as Level 0 (the owner's reference photo, 2026-11-02): the layout built in blocks of cells (Core
    // BackroomsMesh: carpet, the drop ceiling's tiles, papered walls to the ceiling with their doorways and baseboards, slim
    // pillars, the fluorescent fixtures set in the ceiling), one static mesh per surface per block, drawn through NativeBlockLit's
    // pack path with the Level 0 textures of the Backrooms bank (ChipArenaPackBackrooms, docs/PACK-BANK.md); the blocks near the
    // eye only. Flickering fixtures are drawn one by one at their circuit's level; the nearest lit ones light the scene through
    // the practical slots. Every surface casts shadows on both sides, so the sealed ceiling and walls keep any outside light
    // out. Without the bank's textures nothing is drawn (the preview is DEV only).
    public sealed partial class ArenaPresentation
    {
        public const string BackroomsBank="ChipArenaPackBackrooms";public const int BackroomsBlockCells=8;
        public const float BackroomsReach=56,BackroomsLampRange=8f;public static readonly Vector3 BackroomsLampColor=new Vector3(.5f,.47f,.36f);
        private static readonly string[] BackroomsTextures={"backrooms-l0-carpet","backrooms-l0-ceiling","backrooms-l0-wallpaper","backrooms-l0-trim",null,null};
        // Each surface's tint (multipliers over its texture: the add-on's baked colours as they are) and glow.
        private static readonly Color[] BackroomsTint={Color.white,Color.white,Color.white,Color.white,new Color(1f,1f,.96f),new Color(.62f,.62f,.58f)};
        private static readonly float[] BackroomsGlow={0,.22f,0,0,2.4f,0};
        private sealed class BackroomsBlock{public Vector3 Centre;public readonly Mesh[] Meshes=new Mesh[(int)BackroomsMesh.Surface.Count];public readonly List<(Vector3 at,int cell)> Flickers=new List<(Vector3,int)>();}
        private readonly List<BackroomsBlock> backroomsBlocks=new List<BackroomsBlock>();private BackroomsLayout backroomsLayout;private Vector3 backroomsOrigin;
        private Material[] backroomsMats;private Mesh backroomsFixture;private MaterialPropertyBlock backroomsProps;
        private readonly float[] backroomsPos=new float[NativeMapLooks.MaxLights*4],backroomsRgb=new float[NativeMapLooks.MaxLights*3];
        /// <summary>The Backrooms blocks drawn last frame (budget lines).</summary>
        public int BackroomsBlocksDrawn{get;private set;}
        private static readonly int BrTexId=Shader.PropertyToID("_PackTex"),BrColorId=Shader.PropertyToID("_PackColor"),BrEmitId=Shader.PropertyToID("_PackEmit"),BrCutoffId=Shader.PropertyToID("_PackCutoff");
        private static readonly int BrRestId=Shader.PropertyToID("_BlockRest"),BrLookId=Shader.PropertyToID("_BlockLook"),BrBlockColorId=Shader.PropertyToID("_BlockColor");

        private NativeArena backroomsArena;private int backroomsGeneration=-1;private static BackroomsLayout arenaBackrooms;
        internal static bool BackroomsKitReady=>NativePackBanks.Texture(BackroomsTextures[0])!=null;
        internal static bool BackroomsKitBlock(string map,string block)=>NativeBackroomsGenerator.IsMap(map)&&block!=null&&block.StartsWith(map+":",StringComparison.Ordinal)&&!block.Contains(":pit-")&&!block.Contains(":door-");
        private void BackroomsWorld(NativeArena arena)
        {
            BackroomsBlocksDrawn=0;bool live=NativeBackroomsGenerator.IsMap(arena?.MapId);if(!live&&!NativeBackroomsPreview.Active)return;var eye=WorldEye;if(eye==null)return;
            if(NativePackBanks.Texture(BackroomsTextures[0])==null)return;
            var p=live?Vector3.zero:NativeBackroomsPreview.Origin;
            if(live)
            {
                BackroomsLayout layout;
                if(arena.MapId==NativeBackroomsGenerator.ArenaId){if(arenaBackrooms==null)arenaBackrooms=NativeBackroomsGenerator.Build(NativeBackroomsGenerator.ArenaSeed,false).Layout;layout=arenaBackrooms;}
                else layout=arena.GeneratedRun?.Backrooms?.Layout;
                if(layout==null)return;if(!ReferenceEquals(layout,backroomsLayout)||backroomsOrigin!=p)BackroomsBuild(layout,p);
                if(!ReferenceEquals(backroomsArena,arena)||backroomsGeneration!=arena.Generation)
                {
                    backroomsArena=arena;backroomsGeneration=arena.Generation;
                    foreach(var prop in arena.AllBlocks)if(prop!=null&&BackroomsKitBlock(arena.MapId,prop.Definition?.id))
                        foreach(var renderer in prop.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=true;
                }
            }
            if(!live&&(backroomsLayout==null||backroomsLayout.Seed!=NativeBackroomsPreview.Seed||backroomsLayout.Width!=NativeBackroomsPreview.Width||backroomsLayout.Depth!=NativeBackroomsPreview.Depth||backroomsOrigin!=p))
                BackroomsBuild(NativeBackrooms.Preview(NativeBackroomsPreview.Seed,NativeBackroomsPreview.Width,NativeBackroomsPreview.Depth),p);
            if(!BackroomsMaterials())return;
            var e=live?eye.transform.position:NativeBackroomsPreview.Eye??eye.transform.position;double time=Time.timeAsDouble;float r2=BackroomsReach*BackroomsReach;
            foreach(var b in backroomsBlocks)
            {
                var dc=b.Centre-e;dc.y=0;if(dc.sqrMagnitude>r2)continue;BackroomsBlocksDrawn++;
                for(int s=0;s<b.Meshes.Length;s++)if(b.Meshes[s]!=null)Graphics.DrawMesh(b.Meshes[s],Matrix4x4.identity,backroomsMats[s],0,null,0,backroomsProps,s==(int)BackroomsMesh.Surface.Light||s==(int)BackroomsMesh.Surface.DeadLight?ShadowCastingMode.Off:ShadowCastingMode.TwoSided,true);
                foreach(var f in b.Flickers)
                {
                    bool lit=NativeBackrooms.Level(backroomsLayout.Circuits[backroomsLayout.Circuit[f.cell]],f.cell,time)>=.5f;
                    Graphics.DrawMesh(backroomsFixture,Matrix4x4.Translate(f.at),backroomsMats[lit?(int)BackroomsMesh.Surface.Light:(int)BackroomsMesh.Surface.DeadLight],0,null,0,backroomsProps,ShadowCastingMode.Off,false);
                }
            }
            var origin=new NVector(p.x,p.y,p.z);var at=new NVector(e.x,e.y,e.z);
            NativeBackrooms.NearLamps(backroomsLayout,origin,at,time,NativeMapLooks.MaxLights,backroomsPos,backroomsRgb,new NVector(BackroomsLampColor.x,BackroomsLampColor.y,BackroomsLampColor.z),BackroomsLampRange);
            NativeLighting.PickedLights(backroomsPos,backroomsRgb);
            // DEV: inside the preview no sun reaches; Level 0's flat, warm fluorescent wash and its haze (the level's own map look
            // replaces this once its map exists). The toon style is off there: its shade band turned every face away from the
            // map's sun (the whole ceiling) a cool blue.
            float w=backroomsLayout.Width*BackroomsLayout.Cell,d=backroomsLayout.Depth*BackroomsLayout.Cell;
            if(e.x>p.x&&e.x<p.x+w&&e.z>p.z&&e.z<p.z+d&&e.y<p.y+BackroomsLayout.Height)
            {
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=BackroomsAmbient*.9f;RenderSettings.ambientEquatorColor=BackroomsAmbient;RenderSettings.ambientGroundColor=BackroomsAmbient*1.5f;
                if(RenderSettings.sun!=null){RenderSettings.sun.intensity=0;RenderSettings.sun.shadows=LightShadows.None;}
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=BackroomsHaze;RenderSettings.fogStartDistance=6;RenderSettings.fogEndDistance=70;
                NativePackStage.Glow(1);if(NativeLighting.StyleActive)NativeLighting.StyleOff();
            }
        }
        private static readonly Color BackroomsAmbient=new Color(.52f,.48f,.32f),BackroomsHaze=new Color(.55f,.51f,.35f);
        // Builds every block of the layout once (static meshes).
        private void BackroomsBuild(BackroomsLayout l,Vector3 origin)
        {
            BackroomsRelease();backroomsLayout=l;backroomsOrigin=origin;var builder=new BackroomsMesh();var o=new NVector(origin.x,origin.y,origin.z);int n=BackroomsBlockCells;float c=BackroomsLayout.Cell;
            for(int z0=0;z0<l.Depth;z0+=n)for(int x0=0;x0<l.Width;x0+=n)
            {
                builder.Build(l,o,x0,z0,x0+n,z0+n);var b=new BackroomsBlock{Centre=origin+new Vector3((x0+n*.5f)*c,0,(z0+n*.5f)*c)};
                for(int s=0;s<b.Meshes.Length;s++)b.Meshes[s]=BackroomsMeshOf(builder.Parts[s],"Backrooms "+(BackroomsMesh.Surface)s+" "+x0+","+z0);
                foreach(var f in builder.Flickers)b.Flickers.Add((new Vector3(f.at.X,f.at.Y,f.at.Z),f.cell));
                backroomsBlocks.Add(b);
            }
            builder.Parts[(int)BackroomsMesh.Surface.Light].Clear();builder.Fixture(BackroomsMesh.Surface.Light,NVector.Zero);backroomsFixture=BackroomsMeshOf(builder.Parts[(int)BackroomsMesh.Surface.Light],"Backrooms fixture");
        }
        private static Mesh BackroomsMeshOf(BackroomsMesh.Part p,string name)
        {
            if(p.Indices.Count==0)return null;int n=p.Positions.Count;var v=new Vector3[n];var nr=new Vector3[n];var uv=new Vector2[n];
            for(int i=0;i<n;i++){var q=p.Positions[i];v[i]=new Vector3(q.X,q.Y,q.Z);var m=p.Normals[i];nr[i]=new Vector3(m.X,m.Y,m.Z);uv[i]=new Vector2(p.Uv[i].X,p.Uv[i].Y);}
            var mesh=new Mesh{name=name};if(n>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=v;mesh.normals=nr;mesh.uv=uv;mesh.SetTriangles(p.Indices,0,true);mesh.UploadMeshData(true);return mesh;
        }
        private void BackroomsRelease(){foreach(var b in backroomsBlocks)foreach(var m in b.Meshes)if(m!=null)CapturedAnimation.Release(m);backroomsBlocks.Clear();if(backroomsFixture!=null)CapturedAnimation.Release(backroomsFixture);backroomsFixture=null;}
        // One NativeBlockLit pack material per surface (its Level 0 texture, tint and glow) and the draws' constant block values.
        private bool BackroomsMaterials()
        {
            if(backroomsMats!=null)return true;var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)return false;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            backroomsMats=new Material[(int)BackroomsMesh.Surface.Count];
            for(int s=0;s<backroomsMats.Length;s++)
            {
                var m=new Material(shader){name="Backrooms "+(BackroomsMesh.Surface)s};m.EnableKeyword(NativePackStage.Keyword);
                var tex=BackroomsTextures[s]!=null?NativePackBanks.Texture(BackroomsTextures[s]):null;if(tex!=null)tex.wrapMode=TextureWrapMode.Repeat;
                m.SetTexture(BrTexId,tex!=null?(Texture)tex:Texture2D.whiteTexture);m.SetColor(BrColorId,linear?BackroomsTint[s].linear:BackroomsTint[s]);m.SetFloat(BrEmitId,BackroomsGlow[s]);m.SetFloat(BrCutoffId,-1);
                backroomsMats[s]=m;
            }
            backroomsProps=new MaterialPropertyBlock();backroomsProps.SetVector(BrRestId,Vector4.zero);backroomsProps.SetVector(BrLookId,new Vector4(0,1,0,0));backroomsProps.SetVector(BrBlockColorId,new Vector4(1,1,1,1));
            return true;
        }
    }
}
