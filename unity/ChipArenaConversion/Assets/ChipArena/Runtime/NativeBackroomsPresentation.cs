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
        /// <summary>Where the pieces are planned round (null: the world camera), for a fixed capture camera.</summary>
        public static Vector3? Eye;
        private static int args=-1;
        /// <summary>On, by the flag or the command line.</summary>
        public static bool Active{get{if(args<0){args=0;try{foreach(var a in Environment.GetCommandLineArgs())if(a==Argument)args=1;}catch(Exception){}}return On||args==1;}}
    }

    // Draws a Backrooms layout (Core BackroomsLayout) from the owner's kit bank (ChipArenaPackBackrooms, docs/PACK-BANK.md):
    // each kit piece placed by its bounds on the 3 m grid, instanced per mesh, submesh and material through NativeBlockLit's pack
    // path, the cells within reach of the eye only; the dead lamps unlit and the flickering ones lit by their circuit's level; the
    // nearest lit panels feed the scene's practical light slots. Without the bank nothing is drawn (the preview is DEV only).
    public sealed partial class ArenaPresentation
    {
        public const string BackroomsBank="ChipArenaPackBackrooms";
        private BackroomsLayout backroomsLayout;private readonly NativeBackrooms.Piece[] backroomsPieces=new NativeBackrooms.Piece[16000];
        private sealed class BackroomsBatch{public Mesh Mesh;public int Sub;public Material Material;public readonly List<Matrix4x4> M=new List<Matrix4x4>(64);}
        private readonly Dictionary<long,BackroomsBatch> backroomsBatches=new Dictionary<long,BackroomsBatch>();private readonly List<BackroomsBatch> backroomsUsed=new List<BackroomsBatch>();
        private Material[] backroomsMaterials;private Material backroomsDark;private readonly Matrix4x4[] backroomsChunk=new Matrix4x4[NativeBlockLooks.Chunk];private MaterialPropertyBlock backroomsBlock;
        private static readonly int BrRestId=Shader.PropertyToID("_BlockRest"),BrLookId=Shader.PropertyToID("_BlockLook"),BrBlockColorId=Shader.PropertyToID("_BlockColor");
        private readonly float[] backroomsPos=new float[NativeMapLooks.MaxLights*4],backroomsRgb=new float[NativeMapLooks.MaxLights*3];
        /// <summary>The Backrooms pieces drawn last frame (budget lines).</summary>
        public int BackroomsPieces{get;private set;}
        // The kit piece for each Core piece kind (NativeBackrooms.Kind order).
        private static readonly string[] BackroomsProps={"backrooms-floor-tile-flat","backrooms-ceiling-panel","backrooms-ceiling-lamp","backrooms-ceiling-vent","backrooms-wall2-1","backrooms-wall2-1","backrooms-pillar1-1","backrooms-pillar2-1","backrooms-pillar1-2","backrooms-ceiling-panel2-002"};
        public const float BackroomsReach=39,BackroomsLampRange=6.5f;public static readonly Vector3 BackroomsLampColor=new Vector3(.95f,.9f,.72f);

        private void BackroomsWorld()
        {
            BackroomsPieces=0;if(!NativeBackroomsPreview.Active)return;var eye=WorldEye;if(eye==null)return;
            var bank=NativePackBanks.Bank(BackroomsBank);if(bank==null)return;
            var p=NativeBackroomsPreview.Origin;
            if(backroomsLayout==null||backroomsLayout.Seed!=NativeBackroomsPreview.Seed||backroomsLayout.Width!=NativeBackroomsPreview.Width||backroomsLayout.Depth!=NativeBackroomsPreview.Depth)
                backroomsLayout=NativeBackrooms.Preview(NativeBackroomsPreview.Seed,NativeBackroomsPreview.Width,NativeBackroomsPreview.Depth);
            var e=NativeBackroomsPreview.Eye??eye.transform.position;var origin=new NVector(p.x,p.y,p.z);var at=new NVector(e.x,e.y,e.z);double time=Time.timeAsDouble;
            int n=NativeBackrooms.Plan(backroomsLayout,origin,at,BackroomsReach,backroomsPieces);BackroomsPieces=n;
            foreach(var b in backroomsUsed)b.M.Clear();backroomsUsed.Clear();
            for(int i=0;i<n;i++)
            {
                var piece=backroomsPieces[i];var prop=bank.Find(BackroomsProps[(int)piece.Kind]);if(prop==null)continue;
                // A lamp of a dead circuit (or a flickering one in a dip) draws unlit.
                bool dark=piece.Kind==NativeBackrooms.Kind.Lamp&&NativeBackrooms.Level(backroomsLayout.Circuits[backroomsLayout.Circuit[piece.Cell]],piece.Cell,time)<.5f;
                float y=piece.Y;
                if(piece.Kind==NativeBackrooms.Kind.Lamp)y-=.06f;else if(piece.Kind==NativeBackrooms.Kind.Vent)y-=.02f;else if(piece.Kind==NativeBackrooms.Kind.Tile)y+=.005f;
                var scale=piece.Kind==NativeBackrooms.Kind.Post?new Vector3(.26f,1,.26f):new Vector3(piece.Length,1,1);
                var centre=new Vector3((prop.Min.X+prop.Max.X)*.5f,prop.Min.Y,(prop.Min.Z+prop.Max.Z)*.5f);
                var place=Matrix4x4.TRS(new Vector3(piece.X,y,piece.Z),Quaternion.Euler(0,piece.Yaw,0),scale)*Matrix4x4.Translate(-centre);
                foreach(var part in prop.Parts)
                {
                    var mesh=NativePackBanks.Mesh(BackroomsBank,part.Mesh);if(mesh==null)continue;var src=bank.Meshes[part.Mesh];var m=place*NativePackBanks.Place(part.Place);
                    for(int s=0;s<src.Submeshes.Length&&s<mesh.subMeshCount;s++)
                    {
                        int mat=src.Submeshes[s].Material;long key=((long)part.Mesh<<20)|((long)s<<8)|(dark?1L:0L);
                        if(!backroomsBatches.TryGetValue(key,out var batch)){batch=new BackroomsBatch{Mesh=mesh,Sub=s,Material=dark?BackroomsDark():BackroomsMaterial(bank,mat)};backroomsBatches[key]=batch;}
                        if(batch.Material==null)continue;if(batch.M.Count==0)backroomsUsed.Add(batch);batch.M.Add(m);
                    }
                }
            }
            // NativeBlockLit's pack path takes each instance's colour (white), rest (its base on the floor) and look (whole, no
            // dust) from the instanced block (as NativePackStage's), and the lights atlas's glow from _ArenaPackGlow.
            bool instancing=SystemInfo.supportsInstancing;var block=BackroomsBlock(instancing);
            foreach(var b in backroomsUsed)
            {
                if(!instancing){foreach(var m in b.M)Graphics.DrawMesh(b.Mesh,m,b.Material,0,null,b.Sub,block,ShadowCastingMode.On,true);continue;}
                for(int k=0;k<b.M.Count;k+=backroomsChunk.Length){int c=Math.Min(backroomsChunk.Length,b.M.Count-k);b.M.CopyTo(k,backroomsChunk,0,c);Graphics.DrawMeshInstanced(b.Mesh,b.Sub,b.Material,backroomsChunk,c,block,ShadowCastingMode.On,true,0,null);}
            }
            // The fluorescent panels nearest the eye light the scene (the practical slots), at their circuit's level.
            NativeBackrooms.NearLamps(backroomsLayout,origin,at,time,NativeMapLooks.MaxLights,backroomsPos,backroomsRgb,new NVector(BackroomsLampColor.x,BackroomsLampColor.y,BackroomsLampColor.z),BackroomsLampRange);
            NativeLighting.PickedLights(backroomsPos,backroomsRgb);
            // DEV: inside the preview no sun reaches and the air is Level 0's flat, warm fluorescent wash (the level's own map look
            // replaces this once its map exists).
            float w=backroomsLayout.Width*BackroomsLayout.Cell,d=backroomsLayout.Depth*BackroomsLayout.Cell;
            if(e.x>p.x&&e.x<p.x+w&&e.z>p.z&&e.z<p.z+d&&e.y<p.y+BackroomsLayout.Height)
            {
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=BackroomsAmbient*.8f;RenderSettings.ambientEquatorColor=BackroomsAmbient*.9f;RenderSettings.ambientGroundColor=BackroomsAmbient*1.1f;
                if(RenderSettings.sun!=null)RenderSettings.sun.intensity=0;NativePackStage.Glow(1);
            }
        }
        private static readonly Color BackroomsAmbient=new Color(.66f,.6f,.4f);
        private MaterialPropertyBlock BackroomsBlock(bool instancing)
        {
            if(backroomsBlock!=null)return backroomsBlock;backroomsBlock=new MaterialPropertyBlock();var rest=new Vector4(0,0,0,0);var look=new Vector4(0,1,0,0);var white=new Vector4(1,1,1,1);
            if(instancing){int n=NativeBlockLooks.Chunk;var r=new Vector4[n];var l=new Vector4[n];var c=new Vector4[n];for(int i=0;i<n;i++){r[i]=rest;l[i]=look;c[i]=white;}backroomsBlock.SetVectorArray(BrRestId,r);backroomsBlock.SetVectorArray(BrLookId,l);backroomsBlock.SetVectorArray(BrBlockColorId,c);}
            else{backroomsBlock.SetVector(BrRestId,rest);backroomsBlock.SetVector(BrLookId,look);backroomsBlock.SetVector(BrBlockColorId,white);}
            return backroomsBlock;
        }
        private static readonly int BrTexId=Shader.PropertyToID("_PackTex"),BrColorId=Shader.PropertyToID("_PackColor"),BrEmitId=Shader.PropertyToID("_PackEmit"),BrCutoffId=Shader.PropertyToID("_PackCutoff");
        // A bank material on NativeBlockLit's pack path (as NativePackStage.MaterialFor, for this bank).
        private Material BackroomsMaterial(NativePackBank.Bank bank,int index)
        {
            if(index<0||index>=bank.Materials.Count)return null;if(backroomsMaterials==null||backroomsMaterials.Length!=bank.Materials.Count)backroomsMaterials=new Material[bank.Materials.Count];
            if(backroomsMaterials[index]!=null)return backroomsMaterials[index];
            var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)return null;var bm=bank.Materials[index];
            var m=new Material(shader){name="Backrooms "+bm.Name,enableInstancing=true};m.EnableKeyword(NativePackStage.Keyword);
            m.SetTexture(BrTexId,NativePackBanks.Texture(BackroomsBank,bm.Albedo)??Texture2D.whiteTexture);
            var c=new Color(Mathf.Min(1,bm.Color.X),Mathf.Min(1,bm.Color.Y),Mathf.Min(1,bm.Color.Z),1);m.SetColor(BrColorId,QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c);
            m.SetFloat(BrEmitId,Mathf.Max(bm.EmissionColor.X,Mathf.Max(bm.EmissionColor.Y,bm.EmissionColor.Z))*2.2f);m.SetFloat(BrCutoffId,-1);
            return backroomsMaterials[index]=m;
        }
        // A dead fluorescent panel: the diffuser grey and unlit.
        private Material BackroomsDark()
        {
            if(backroomsDark!=null)return backroomsDark;var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)return null;
            backroomsDark=new Material(shader){name="Backrooms dead lamp",enableInstancing=true};backroomsDark.EnableKeyword(NativePackStage.Keyword);
            backroomsDark.SetTexture(BrTexId,Texture2D.whiteTexture);var c=new Color(.55f,.55f,.52f,1);backroomsDark.SetColor(BrColorId,QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c);backroomsDark.SetFloat(BrEmitId,0);backroomsDark.SetFloat(BrCutoffId,-1);
            return backroomsDark;
        }
    }
}
