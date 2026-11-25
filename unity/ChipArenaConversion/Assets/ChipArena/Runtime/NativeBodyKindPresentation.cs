using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The female pilot (Core/NativeBodyKind; Astra's brief 2026-11-24 section 1): the character pack's authored female preset
    // (Default_Girl) on the bake's one body, set when a body is dressed (VisualFemale: its body and face shapes on every worn
    // mesh, so the clothes follow the body as the pack's own clothes do, and its Chest Size bone modifier), and what rides over the
    // cloth fitted to it: the chest core (VisualFemaleCore) and the robot armour's plates (SuitFemaleEnsure), each carried with
    // the surface under it by that surface's own shape deltas, so nothing worn over the cloth sinks into it. The hair, hats,
    // helmet and face gear ride the head, which keeps its size; the wrist device rides the forearm, scaled with it. Every body
    // made for the pilot wears it: the live, draft and first-person bodies, corpses, revive bodies, death scenes and portraits.
    // Presentation only: the game rig, hit volumes, reach, shot origins, movement and timing are the same for both bodies.
    public sealed partial class ArenaPresentation
    {
        // ---- the preset on a body (VisualDress) ----
        // Per worn mesh: its shapes the preset sets (NativeBodyKind's body shapes, then its face shapes; -1 where it has none).
        private static readonly Dictionary<Mesh,int[]> femaleShapes=new Dictionary<Mesh,int[]>();
        // The bodies made female (instance ids of their roots: the robot suit's plates take the female fit, SuitRigOf).
        private static readonly HashSet<int> femaleRoots=new HashSet<int>();
        private static int[] FemaleShapeIndex(Mesh mesh)
        {
            if(femaleShapes.TryGetValue(mesh,out var index))return index;
            int nb=NativeBodyKind.BodyShapes.Length;index=new int[nb+NativeBodyKind.FaceShapes.Length];
            for(int k=0;k<index.Length;k++)
            {
                index[k]=-1;string want=k<nb?NativeBodyKind.BodyShapes[k]:NativeBodyKind.FaceShapes[k-nb];
                for(int i=0;i<mesh.blendShapeCount;i++)if(NativeZombieLooks.ShapeMatches(mesh.GetBlendShapeName(i),want)){index[k]=i;break;}
            }
            femaleShapes[mesh]=index;return index;
        }
        private static float FemaleWeight(int k)=>k<NativeBodyKind.BodyShapes.Length?NativeBodyKind.BodyWeights[k]:NativeBodyKind.FaceWeights[k-NativeBodyKind.BodyShapes.Length];
        // The preset on a body being made (before its binding reads the rest pose): the shapes by name on every skinned mesh that has
        // them (the pack's clothes carry the body's shapes), then the pack's Chest Size on the skeleton: spine_04 scaled, the head
        // counter-scaled to its own size (the hair, hats and face gear with it). Runs when a body is made, never per frame.
        private static void VisualFemale(GameObject root)
        {
            // Its face pieces (the pack's female lashes and brows, baked off as the look data's "face" pieces): each worn in place of
            // the piece of its kind the head wears (the renderer with the same slot prefix). None in an older bake.
            var d=VisualLookData();
            if(d?.pieces!=null)
            {
                Dictionary<string,Renderer> named=null;
                foreach(var p in d.pieces)
                {
                    if(p?.renderers==null||p.slot!=NativeBodyKind.FaceSlot||Array.IndexOf(NativeBodyKind.FacePieces,p.id)<0)continue;
                    if(named==null){named=new Dictionary<string,Renderer>(StringComparer.Ordinal);foreach(var r in root.GetComponentsInChildren<Renderer>(true))named[r.name]=r;}
                    foreach(var n in p.renderers)
                    {
                        if(!named.TryGetValue(n,out var mine))continue;int dot=n.IndexOf('.');if(dot<=0)continue;
                        foreach(var r in named.Values)if(r!=mine&&r.gameObject.activeSelf&&string.CompareOrdinal(r.name,0,n,0,dot+1)==0)r.gameObject.SetActive(false);
                        mine.gameObject.SetActive(true);
                    }
                }
            }
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=skin.sharedMesh;if(mesh==null||mesh.blendShapeCount==0)continue;var index=FemaleShapeIndex(mesh);
                for(int k=0;k<index.Length;k++)if(index[k]>=0)skin.SetBlendShapeWeight(index[k],FemaleWeight(k));
            }
            femaleRoots.Add(root.GetInstanceID());
            var skeleton=root.transform.Find("VisualSkeleton");if(skeleton==null)return;
            Transform chest=null,head=null;
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true)){if(chest==null&&t.name==NativeBodyKind.ChestBone)chest=t;else if(head==null&&t.name==NativeBodyKind.HeadBone)head=t;}
            if(chest==null)return;chest.localScale*=NativeBodyKind.ChestScale;
            if(head!=null&&head.IsChildOf(chest))head.localScale/=NativeBodyKind.ChestScale;
        }

        // ---- the female surface's deltas ----
        // A mesh's vertices and the preset's displacement of each (mesh space: its body shapes at their weights); null without one.
        private static readonly Dictionary<Mesh,(Vector3[] At,Vector3[] Delta)> femaleDeltas=new Dictionary<Mesh,(Vector3[],Vector3[])>();
        private static bool FemaleDeltas(Mesh mesh,out Vector3[] at,out Vector3[] delta)
        {
            if(femaleDeltas.TryGetValue(mesh,out var known)){at=known.At;delta=known.Delta;return at!=null;}
            at=null;delta=null;int n=mesh.vertexCount;
            if(mesh.isReadable&&n>0)
            {
                var index=FemaleShapeIndex(mesh);Vector3[] lo=null,hi=null;
                for(int k=0;k<NativeBodyKind.BodyShapes.Length;k++)
                {
                    int s=index[k];if(s<0)continue;int frames=mesh.GetBlendShapeFrameCount(s);if(frames<1)continue;float w=FemaleWeight(k);
                    // (Unity's blend: the first frame scaled up from 0, then between the frames either side of the weight.)
                    int f=0;while(f<frames-1&&mesh.GetBlendShapeFrameWeight(s,f)<w)f++;
                    float top=mesh.GetBlendShapeFrameWeight(s,f),bottom=f>0?mesh.GetBlendShapeFrameWeight(s,f-1):0,t=top>bottom?(w-bottom)/(top-bottom):1;
                    hi??=new Vector3[n];mesh.GetBlendShapeFrameVertices(s,f,hi,null,null);
                    if(f>0){lo??=new Vector3[n];mesh.GetBlendShapeFrameVertices(s,f-1,lo,null,null);}
                    delta??=new Vector3[n];for(int i=0;i<n;i++)delta[i]+=f>0?Vector3.LerpUnclamped(lo[i],hi[i],t):hi[i]*t;
                }
                if(delta!=null)at=mesh.vertices;else delta=null;
            }
            femaleDeltas[mesh]=(at,at!=null?delta:null);if(at==null)delta=null;return at!=null;
        }
        // The weighted mean of a mesh's deltas round `p` (mesh space) within `reach`: how far the surface there moved.
        private static bool FemaleDeltaAt(Vector3[] at,Vector3[] delta,Vector3 p,float reach,ref Vector3 sum,ref float total)
        {
            bool any=false;float r2=reach*reach;
            for(int i=0;i<at.Length;i++){var d=at[i]-p;float q=d.sqrMagnitude;if(q>=r2)continue;float w=1-Mathf.Sqrt(q)/reach;w*=w;sum+=w*delta[i];total+=w;any=true;}
            return any;
        }

        // ---- the chest core ----
        // The core sits where the bake measured it on today's body (its top's surface over the sternum, beside the heart); on the
        // female body it rides out with that surface, by the worn meshes' deltas within CoreReach of it. Per top and hands, once.
        private const float CoreReach=.035f;
        private static readonly Dictionary<string,Vector3> femaleCores=new Dictionary<string,Vector3>(StringComparer.Ordinal);
        private static void VisualFemaleCore(VisualBody body)
        {
            if(body.GearCore==null||!NativeBodyKind.IsFemale(body.Look)||NativeHotDog.Worn(body.Look)||NativeVoxelCostume.Worn(body.Look))return;
            var bone=body.GearCore.parent;if(bone==null)return;
            string key=NativeLook.Of(body.Look,NativeLook.Upper)+"|"+NativeLook.Of(body.Look,NativeLook.Hands);
            if(!femaleCores.TryGetValue(key,out var offset))
            {
                // (A corpse's anchors before any live body made them: as measured.)
                if(body.All==null)return;
                Vector3 sum=Vector3.zero;float total=0;var local=body.GearCore.localPosition;
                foreach(var r in body.All)
                {
                    if(!(r is SkinnedMeshRenderer skin)||skin.sharedMesh==null||!FemaleDeltas(skin.sharedMesh,out var at,out var delta))continue;
                    int b=Array.IndexOf(skin.bones,bone);var binds=skin.sharedMesh.bindposes;if(b<0||b>=binds.Length)continue;
                    Vector3 s=Vector3.zero;float t=0;
                    if(FemaleDeltaAt(at,delta,binds[b].inverse.MultiplyPoint3x4(local),CoreReach,ref s,ref t)){sum+=binds[b].MultiplyVector(s);total+=t;}
                }
                offset=total>0?sum/total:Vector3.zero;femaleCores[key]=offset;
            }
            body.GearCore.localPosition+=offset;
        }

        // ---- the robot armour's plates ----
        // The plates (Core NativeRobotSuit) are authored on today's body over the garments each robot piece is worn over (its
        // Under: the sweater, the skinny jeans, the gloves, the work boots). For the female body each plate vertex is carried with
        // its piece's garment there: the mean of that garment's deltas within SuitReach (found on a grid), moved from the garment's
        // bind frame into the plate's. Made once, when a female body first wears a robot piece; shared by every female body.
        private const float SuitReach=.07f;
        private bool suitFemaleTried;
        private sealed class FemaleGrid
        {
            public Vector3[] At,Delta;public readonly Dictionary<long,List<int>> Cells=new Dictionary<long,List<int>>();public Matrix4x4[] Binds;public Transform[] Bones;
            public static long Key(int x,int y,int z)=>((long)(x&0x1fffff)<<42)|((long)(y&0x1fffff)<<21)|(long)(z&0x1fffff);
            public static int Cell(float v)=>Mathf.FloorToInt(v/SuitReach);
            public FemaleGrid(Vector3[] at,Vector3[] delta,Matrix4x4[] binds,Transform[] bones)
            {
                At=at;Delta=delta;Binds=binds;Bones=bones;
                for(int i=0;i<at.Length;i++){long k=Key(Cell(at[i].x),Cell(at[i].y),Cell(at[i].z));if(!Cells.TryGetValue(k,out var list))Cells[k]=list=new List<int>();list.Add(i);}
            }
            public Vector3 At3(Vector3 p)
            {
                Vector3 sum=Vector3.zero;float total=0,r2=SuitReach*SuitReach;int cx=Cell(p.x),cy=Cell(p.y),cz=Cell(p.z);
                for(int x=cx-1;x<=cx+1;x++)for(int y=cy-1;y<=cy+1;y++)for(int z=cz-1;z<=cz+1;z++)
                {
                    if(!Cells.TryGetValue(Key(x,y,z),out var list))continue;
                    foreach(int i in list){var d=At[i]-p;float q=d.sqrMagnitude;if(q>=r2)continue;float w=1-Mathf.Sqrt(q)/SuitReach;w*=w;sum+=w*Delta[i];total+=w;}
                }
                return total>0?sum/total:Vector3.zero;
            }
        }
        private void SuitFemaleEnsure(GameObject root,SuitRig suit)
        {
            if(suitFemaleTried||sourceModels==null||suit==null)return;suitFemaleTried=true;
            var d=VisualLookData();if(d?.pieces==null)return;
            var skins=new Dictionary<string,SkinnedMeshRenderer>(StringComparer.Ordinal);foreach(var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))skins[s.name]=s;
            var grids=new FemaleGrid[NativeLook.Slots.Length];
            for(int slot=1;slot<NativeLook.Slots.Length;slot++)
                foreach(var p in d.pieces)
                {
                    if(p?.renderers==null||p.slot!=NativeLook.Slots[slot]||p.id!=NativeRobotSuit.Under[slot])continue;
                    foreach(var n in p.renderers)if(grids[slot]==null&&skins.TryGetValue(n,out var skin)&&skin.sharedMesh!=null&&FemaleDeltas(skin.sharedMesh,out var at,out var delta))
                        grids[slot]=new FemaleGrid(at,delta,skin.sharedMesh.bindposes,skin.bones);
                }
            try
            {
                sourceModels.SuitFemaleModels((bone,v)=>
                {
                    var g=bone>=0&&bone<NativeRobotSuit.BoneSlot.Length?grids[NativeRobotSuit.BoneSlot[bone]]:null;if(g==null)return v;
                    // The plate's frame (its rig's bind) to the garment's (its own bind for that bone; the same space when it has none).
                    int b=Array.IndexOf(g.Bones,suit.Bones[bone]);var toGarment=b>=0&&b<g.Binds.Length?g.Binds[b].inverse*suit.Bind[bone]:Matrix4x4.identity;
                    return v+toGarment.inverse.MultiplyVector(g.At3(toGarment.MultiplyPoint3x4(v)));
                });
            }
            catch(Exception e){Debug.LogWarning("Visual body: the robot suit's female fit failed: "+e.Message);}
        }
        // The suit model a body's plates draw from: the female fit on a female body (once it is made), else today's.
        private string SuitModelOf(GameObject root,SuitRig suit)
        {
            if(!femaleRoots.Contains(root.GetInstanceID()))return NativeSourceModels.Suit;
            if(!sourceModels.Has(NativeSourceModels.SuitFemale))SuitFemaleEnsure(root,suit);
            return sourceModels.Has(NativeSourceModels.SuitFemale)?NativeSourceModels.SuitFemale:NativeSourceModels.Suit;
        }

        // ---- this client's own body pick ----
        // The local pilot's seat look carries its own body pick (prefs.look through LookShown, LocalLook), which the seat's
        // normalisation drops until NativeLook carries the key (Astra's part): this client sees its own pilot in it; a peer draws
        // today's pilot until then. Nothing to do once the seat carries it. Made again only when the seat's look or the pick changes.
        private string localBodyFrom,localBodyLook;private int localBodyOf=-1;
        private bool LocalBodyOwn(out string look)
        {
            look=null;var seat=WeaponLoadout?.Match?.Seat(LocalId);if(seat==null)return false;
            int body=NativeBodyKind.Of(LocalLook);var from=seat.look??"";if(NativeBodyKind.Of(from)==body)return false;
            if(!ReferenceEquals(from,localBodyFrom)||body!=localBodyOf){localBodyFrom=from;localBodyOf=body;localBodyLook=NativeBodyKind.With(from,body);}
            look=localBodyLook;return true;
        }
    }
}
