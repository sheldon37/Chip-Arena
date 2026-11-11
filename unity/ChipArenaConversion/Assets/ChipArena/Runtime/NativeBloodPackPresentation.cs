using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blood pack (owner 2026-11-10; Core/NativeBloodPack, docs/BLOOD-PACK.md), presentation only. The local bake's
    /// VisualBlood (the visual body's prefab; the licensed textures and meshes stay out of the repository) holds, per entry, a
    /// renderer whose material is on one of the game's own shaders:
    /// <list type="bullet">
    /// <item>Splash: the pack's six simulated sheets on ChipArena/NativeBloodSplash. Every spray of Core/NativeBloodSim (a
    /// wound on a living pilot, a death's gush, a bisect's, a smash's, the horde's, an arrow's exit) also throws its splashes
    /// (<see cref="NativeBloodSplashes"/>): camera-facing quads in one dynamic mesh, a sub-mesh a sheet, rebuilt while any
    /// plays. They replace the wounds' vapour mist.</item>
    /// <item>Decal: the pack's four spreading sprite sheets on ChipArena/NativeBloodDecal. Each splat of the pool is one quad
    /// (NativeBloodDecalLook), spreading in on the GPU; a kill lays a pool under its wound. The pool of splats, its colours,
    /// drying, fading and surfaces are NativeBloodPresentation's, which falls back to the bank's splats and then the fans.</item>
    /// <item>Gut: the pack's five flesh lumps on ChipArena/NativeBloodGut, drawn in place of some of a gib's fragments
    /// (NativeBloodGut).</item>
    /// </list>
    /// Gore off draws none of it (and no blood at all: Bleed); reduced effects halve the splashes and the lumps; the low
    /// graphics tier keeps one splash a wound and today's fan splats, with no pools and no lumps. Each part needs only its
    /// own entries: without the bake the blood is as before. Pooled: fixed rings and reused lists, no allocation once warm.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private bool bloodPackRead;private Material[] splashMaterials,decalMaterials,gutMaterials;private Mesh[] gutMeshes;private Vector3[] gutCentre;private float[] gutUnit;
        private readonly NativeBloodSplashes splashes=new NativeBloodSplashes();
        private Mesh splashMesh,decalMesh;private bool decalFilled;
        private readonly List<Vector3> packPoints=new List<Vector3>(),packNormals=new List<Vector3>();private readonly List<Vector4> packTangents=new List<Vector4>(),packUv=new List<Vector4>(),packMix=new List<Vector4>();
        private readonly List<Color> packColors=new List<Color>();private readonly List<int>[] splashIndices=PackLists(NativeBloodSplash.Sheets);
        private readonly List<Vector3> decalPoints=new List<Vector3>(),decalNormals=new List<Vector3>();private readonly List<Vector4> decalTangents=new List<Vector4>(),decalUv=new List<Vector4>(),decalCore=new List<Vector4>();
        private readonly List<Color> decalColors=new List<Color>();private readonly List<int>[] decalIndices=PackLists(NativeBloodDecalLook.Sheets);
        private struct GutDraw{public Matrix4x4 Matrix;public int Mesh;}
        private readonly List<GutDraw> gutDraws=new List<GutDraw>(64);
        private static readonly int BloodNowId=Shader.PropertyToID("_BloodNow");
        private static List<int>[] PackLists(int n){var lists=new List<int>[n];for(int k=0;k<n;k++)lists[k]=new List<int>();return lists;}
        /// <summary>Live splashes (the checks and the dev overlay).</summary>
        public int BloodSplashes=>splashes.Alive(bloodPackClock);
        private float bloodPackClock;

        // The bake's entries, read once: each group is whole (every entry on its own shader) or unused.
        private void BloodPack()
        {
            if(bloodPackRead)return;bloodPackRead=true;
            var prefab=Resources.Load<GameObject>(VisualBodyResource);var holder=prefab!=null?prefab.transform.Find("VisualBlood"):null;if(holder==null)return;
            splashMaterials=PackMaterials(holder.Find("Splash"),NativeBloodSplash.Names,"ChipArena/NativeBloodSplash",(int)RenderQueue.Transparent+1);
            decalMaterials=PackMaterials(holder.Find("Decal"),NativeBloodDecalLook.Names,"ChipArena/NativeBloodDecal",(int)RenderQueue.Transparent-60);
            gutMaterials=PackMaterials(holder.Find("Gut"),NativeBloodGut.Names,"ChipArena/NativeBloodGut",-1);
            if(gutMaterials!=null)
            {
                var gut=holder.Find("Gut");gutMeshes=new Mesh[NativeBloodGut.Meshes];gutCentre=new Vector3[NativeBloodGut.Meshes];gutUnit=new float[NativeBloodGut.Meshes];
                for(int k=0;k<gutMeshes.Length;k++)
                {
                    var filter=gut.Find(NativeBloodGut.Names[k]).GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;if(mesh==null){gutMaterials=null;gutMeshes=null;break;}
                    var b=mesh.bounds;gutMeshes[k]=mesh;gutCentre[k]=b.center;gutUnit[k]=1/Mathf.Max(1e-4f,Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)));
                }
            }
            if(splashMaterials!=null){splashMesh=new Mesh{name="Blood splashes (pack)",subMeshCount=NativeBloodSplash.Sheets};splashMesh.MarkDynamic();}
            if(decalMaterials!=null){decalMesh=new Mesh{name="Blood decals (pack)",indexFormat=IndexFormat.UInt32,subMeshCount=NativeBloodDecalLook.Sheets};decalMesh.MarkDynamic();}
        }
        private static Material[] PackMaterials(Transform group,string[] names,string shader,int queue)
        {
            if(group==null)return null;var list=new Material[names.Length];
            for(int k=0;k<names.Length;k++)
            {
                var entry=group.Find(names[k]);var r=entry!=null?entry.GetComponent<Renderer>():null;var m=r!=null?r.sharedMaterial:null;
                if(m==null||m.shader==null||m.shader.name!=shader||!m.shader.isSupported)return null;
                list[k]=new Material(m){name="Blood pack "+names[k]};if(queue>=0)list[k].renderQueue=queue;
            }
            return list;
        }
        /// <summary>BloodFx, each frame before the drops step: whether the sprays throw splashes this frame.</summary>
        private void BloodPackBegin(float now)
        {
            BloodPack();bloodPackClock=now;blood.Splashes=Gore&&splashMaterials!=null?splashes:null;
        }
        /// <summary>Whether the wounds' vapour mist still draws (not where the pack's splashes do).</summary>
        private bool BloodMistShown=>blood.Splashes==null;
        /// <summary>A kill's pool under its wound (Core NativeBloodDecalLook.PoolSize): cast down onto the floor, once per death.</summary>
        private void BloodPool(long key,string kind,float severity,uint seed,Vector3 origin,Vector3 along,float time)
        {
            if(!Gore||LowGraphics||decalMaterials==null||bloodArena==null)return;float half=NativeBloodDecalLook.PoolSize(kind,severity,seed);if(!(half>0))return;
            if(!BloodCast(new NVector(origin.x,origin.y+.1f,origin.z),new NVector(origin.x,origin.y-3.2f,origin.z),out var point,out var normal,out int surface)||surface==0||normal.Y<.7f)return;
            var flat=new NVector(along.x,0,along.z);if(flat.LengthSquared()<1e-4f)flat=NVector.UnitX;
            blood.Splats.Add(key^(1L<<58),point+normal*(NativeBloodRules.Lift*1.5f),normal,NVector.Normalize(flat),half,half*.9f,seed^0x9001u,surface,time+NativeBloodDecalLook.PoolDelay,false,true);
        }
        /// <summary>The splashes of this frame: one quad each, facing the camera, in the sheet's sub-mesh.</summary>
        private void DrawSplashes(Camera camera,float now)
        {
            if(splashMaterials==null)return;
            if(!Gore||splashes.Update(now)==0){if(splashMesh.vertexCount>0)splashMesh.Clear();return;}
            var view=camera!=null?camera:Camera.main;if(view==null)return;var e=view.transform.position;var eye=new NVector(e.x,e.y,e.z);
            packPoints.Clear();packNormals.Clear();packTangents.Clear();packUv.Clear();packMix.Clear();packColors.Clear();foreach(var list in splashIndices)list.Clear();
            var items=splashes.Items;
            for(int i=0;i<items.Length;i++)
            {
                var p=items[i];if(!p.Live)continue;float age=now-p.Born;if(age<0||age>=p.Life)continue;float u=age/p.Life,opacity=NativeBloodSplash.Opacity(u);if(opacity<=.002f)continue;
                NativeBloodSplash.Quad(p.Origin,p.Dir,p.Size,p.Life,age,p.Flip,eye,out var pivot,out var ux,out var uy,out var n);
                NativeBloodSplash.Frame(p.Sheet,u,out int fa,out int fb,out float blend);int columns=NativeBloodSplash.Columns[p.Sheet];
                NativeBloodSplash.Cell(columns,fa,out float ua,out float va,out float side);NativeBloodSplash.Cell(columns,fb,out float ub,out float vb,out _);
                var tint=NativeVfxDraw.Linear(p.Ink?NativeBloodSplash.InkTint:NativeBloodSplash.Tint);tint.a=opacity;
                float pu=NativeBloodSplash.PivotU[p.Sheet],pv=1-NativeBloodSplash.PivotV[p.Sheet];int start=packPoints.Count;
                Vector3 o=new Vector3(pivot.X,pivot.Y,pivot.Z),ax=new Vector3(ux.X,ux.Y,ux.Z),ay=new Vector3(uy.X,uy.Y,uy.Z),nn=new Vector3(n.X,n.Y,n.Z);var tangent=new Vector4(ax.x,ax.y,ax.z,p.Flip?-1:1);
                // Half a texel in from the cell's edge, so a neighbouring frame never bleeds in.
                float inset=side*.006f;
                for(int k=0;k<4;k++)
                {
                    float x=k==1||k==2?1:0,y=k>=2?1:0;packPoints.Add(o+ax*((x-pu)*p.Size)+ay*((y-pv)*p.Size));packNormals.Add(nn);packTangents.Add(tangent);packColors.Add(tint);
                    float cx=Mathf.Lerp(inset,side-inset,x),cy=Mathf.Lerp(inset,side-inset,y);packUv.Add(new Vector4(ua+cx,va+cy,ub+cx,vb+cy));packMix.Add(new Vector4(blend,0,0,0));
                }
                var indices=splashIndices[p.Sheet];indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
            splashMesh.Clear();if(packPoints.Count==0)return;splashMesh.subMeshCount=NativeBloodSplash.Sheets;
            splashMesh.SetVertices(packPoints);splashMesh.SetNormals(packNormals);splashMesh.SetTangents(packTangents);splashMesh.SetColors(packColors);splashMesh.SetUVs(0,packUv);splashMesh.SetUVs(1,packMix);
            for(int k=0;k<splashIndices.Length;k++)splashMesh.SetTriangles(splashIndices[k],k,false);splashMesh.bounds=new Bounds(Vector3.zero,Vector3.one*4000);
            for(int k=0;k<splashIndices.Length;k++)if(splashIndices[k].Count>0)Graphics.DrawMesh(splashMesh,Matrix4x4.identity,splashMaterials[k],0,camera,k,null,ShadowCastingMode.Off,false);
        }
        // The splat mesh's rebuild: whether the pack's decals draw the splats (then DecalSplat fills, DecalEnd uploads).
        private bool DecalBegin()
        {
            if(decalMaterials==null||!Gore)return false;
            decalPoints.Clear();decalNormals.Clear();decalTangents.Clear();decalUv.Clear();decalCore.Clear();decalColors.Clear();foreach(var list in decalIndices)list.Clear();return true;
        }
        // One splat as a pack decal (Core NativeBloodDecalLook.Pick): a quad on its surface with the sheet's landing point on the
        // splat and +u along its travel, turned and mirrored by its seed, in today's lit rim and wet-core colours.
        private void DecalSplat(in NativeBloodSplats.Splat s,float alpha,float now)
        {
            Vector3 n=new Vector3(s.Normal.X,s.Normal.Y,s.Normal.Z),o=new Vector3(s.Center.X,s.Center.Y,s.Center.Z),u=new Vector3(s.Along.X,s.Along.Y,s.Along.Z);
            u=(u-n*Vector3.Dot(u,n)).normalized;if(u.sqrMagnitude<.5f)u=Vector3.Cross(n,Vector3.right).normalized;
            float age=now-s.Born,dry=NativeBloodRules.Darken(age);Color light=n.y>.5f?splatUp:n.y<-.5f?splatDown:splatSide;
            Color Tone(int hex,float k){var c=NativeArena.ColorOf(hex);return new Color(c.r*k*light.r,c.g*k*light.g,c.b*k*light.b,alpha);}
            Color rim=Tone(s.Ink?NativeBloodRules.InkRim:NativeBloodRules.Rim,dry*.95f),core=Tone(s.Ink?NativeBloodRules.InkCore:NativeBloodRules.Core,dry*(.75f+.25f*dry));
            NativeBloodDecalLook.Pick(s.Seed,s.Length,s.Width,s.Pool,out int sheet,out float spin,out bool mirror,out float size);
            Vector3 a=Quaternion.AngleAxis(spin*Mathf.Rad2Deg,n)*u,b=Vector3.Cross(n,a)*(mirror?-1:1);float pu=NativeBloodDecalLook.PivotU[sheet],pv=1-NativeBloodDecalLook.PivotV[sheet];
            var tangent=new Vector4(a.x,a.y,a.z,mirror?-1:1);var coreUv=new Vector4(core.r,core.g,core.b,NativeBloodDecalLook.Spread(s.Pool));float wet=NativeBloodSplatLook.Wet(age);int start=decalPoints.Count;
            for(int k=0;k<4;k++)
            {
                float x=k==1||k==2?1:0,y=k>=2?1:0;decalPoints.Add(o+a*((x-pu)*size)+b*((y-pv)*size));decalNormals.Add(n);decalTangents.Add(tangent);decalColors.Add(rim);
                decalUv.Add(new Vector4(x,y,wet,s.Born));decalCore.Add(coreUv);
            }
            var indices=decalIndices[sheet];indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
        }
        private void DecalEnd()
        {
            if(decalMesh==null)return;decalMesh.Clear();decalFilled=decalPoints.Count>0;if(!decalFilled)return;decalMesh.subMeshCount=NativeBloodDecalLook.Sheets;
            decalMesh.SetVertices(decalPoints);decalMesh.SetNormals(decalNormals);decalMesh.SetTangents(decalTangents);decalMesh.SetColors(decalColors);decalMesh.SetUVs(0,decalUv);decalMesh.SetUVs(1,decalCore);
            for(int k=0;k<decalIndices.Length;k++)decalMesh.SetTriangles(decalIndices[k],k,false);decalMesh.bounds=new Bounds(Vector3.zero,Vector3.one*4000);
        }
        private void DecalClear(){if(decalMesh!=null&&decalFilled){decalMesh.Clear();decalFilled=false;}}
        private void DecalDraw(Camera camera,float now)
        {
            if(decalMesh==null||!decalFilled)return;
            for(int k=0;k<decalIndices.Length;k++)
            {
                if(decalIndices[k].Count==0)continue;decalMaterials[k].SetFloat(BloodNowId,now);
                Graphics.DrawMesh(decalMesh,Matrix4x4.identity,decalMaterials[k],0,camera,k,null,ShadowCastingMode.Off,false);
            }
        }
        /// <summary>KillWound: whether fragment `i` of a gib is one of the pack's flesh lumps (then queued here, drawn by BloodFx),
        /// in the fragment's place, turned and thrown as it is.</summary>
        private bool GutChunk(NativeCorpse corpse,int i,int chunks,Matrix4x4 chunk,float alpha)
        {
            if(!Gore||alpha<.5f)return false;BloodPack();if(gutMeshes==null||gutDraws.Count>=64)return false;int seed=corpse.Death.seed;
            if(!NativeBloodGut.Is(i,chunks,seed,BloodQuality,LowGraphics,out int mesh))return false;float ratio=NativeBloodGut.Ratio(i,seed);
            // The lump is larger than the cube it replaces: lifted so it rests on the floor where the cube did.
            float cube=chunk.GetColumn(0).magnitude;var lift=Matrix4x4.Translate(Vector3.up*(cube*Mathf.Max(0,.3f*ratio-.5f)));
            gutDraws.Add(new GutDraw{Matrix=lift*chunk*Matrix4x4.Scale(Vector3.one*(ratio*gutUnit[mesh]))*Matrix4x4.Translate(-gutCentre[mesh]),Mesh=mesh});return true;
        }
        private void DrawGuts(Camera camera)
        {
            for(int i=0;i<gutDraws.Count;i++){var g=gutDraws[i];Graphics.DrawMesh(gutMeshes[g.Mesh],g.Matrix,gutMaterials[g.Mesh],0,camera,0,null,ShadowCastingMode.On,true);}
            gutDraws.Clear();
        }
        private void ClearBloodPack(){splashes.Clear();gutDraws.Clear();}
    }
}
