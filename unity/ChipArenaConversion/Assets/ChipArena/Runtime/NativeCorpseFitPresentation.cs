using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The ragdoll's boxes fitted to the model it carries (owner 2026-11-09: "We need to fix ragdolling; A lot of deaths have caused
    // things like this too happen (stretched limbs, clipping into floor, etc.)"). The cosmetic body's parts are boxes from the classic
    // rig's shapes (NativeCorpse), so wherever the model is bigger than them (the Hot Dog's crown and girth, the boots past the classic
    // feet) it lay through the floor while the boxes rested on it. Once a pilot's visual corpse is made (VisualCorpseMake, at the death
    // pose and before the first physics step), each part's box becomes the box of the model's own skin on that part's bones.
    public sealed partial class ArenaPresentation
    {
        // Per bone of a mesh, the box (in that bone's bind frame) of the vertices weighted most to it; null where none are. Read once.
        private static readonly Dictionary<Mesh,Bounds?[]> corpseBoneBoxes=new Dictionary<Mesh,Bounds?[]>();
        private static Bounds?[] CorpseBoneBoxes(Mesh mesh)
        {
            if(corpseBoneBoxes.TryGetValue(mesh,out var have))return have;Bounds?[] boxes=null;
            if(mesh.isReadable)
            {
                var bind=mesh.bindposes;var vs=mesh.vertices;var bw=mesh.boneWeights;
                if(bind.Length>0&&bw.Length==vs.Length)
                {
                    boxes=new Bounds?[bind.Length];
                    for(int i=0;i<vs.Length;i++)
                    {
                        var w=bw[i];int b=w.boneIndex0;float most=w.weight0;if(w.weight1>most){b=w.boneIndex1;most=w.weight1;}if(w.weight2>most){b=w.boneIndex2;most=w.weight2;}if(w.weight3>most)b=w.boneIndex3;
                        if(b<0||b>=bind.Length)continue;var p=bind[b].MultiplyPoint3x4(vs[i]);
                        if(boxes[b] is Bounds bb){bb.Encapsulate(p);boxes[b]=bb;}else boxes[b]=new Bounds(p,Vector3.zero);
                    }
                }
            }
            corpseBoneBoxes[mesh]=boxes;return boxes;
        }
        /// <summary>After VisualCorpseMake: each of the cosmetic body's part boxes made the box, in that part's frame, of the model's skin
        /// whose bones ride it (every skinned piece worn, its bones' boxes at the death pose). A split body's halves keep their own.</summary>
        private void VisualCorpseFit(NativeCorpse corpse,NativeArena arena)
        {
            if(corpse==null||corpse.Halved||!visualCorpses.TryGetValue(corpse,out var c)||c?.All==null||c.Bones==null)return;
            var index=new Dictionary<Transform,int>();for(int v=0;v<c.Bones.Length;v++)if(c.Bones[v]!=null)index[c.Bones[v]]=v;
            // The parts first moved onto the model's joints: each part's joint, the highest visual bone on it (a thigh's, not its twist
            // bone), the skeleton's root at the feet aside (the pelvis's own).
            if(c.Joint!=null&&c.Joint.Length==c.Bones.Length)
            {
                bool Root(Transform t)=>string.Equals(t.name,"root",System.StringComparison.OrdinalIgnoreCase);
                var at=new Dictionary<int,Vector3>();
                for(int v=0;v<c.Bones.Length;v++)
                {
                    int j=c.Joint[v];if(c.Bones[v]==null||Root(c.Bones[v])||j<0||!corpse.Parts.ContainsKey(j)||at.ContainsKey(j))continue;
                    bool top=true;for(var t=c.Bones[v].parent;t!=null&&top;t=t.parent)if(!Root(t)&&index.TryGetValue(t,out int u)&&c.Joint[u]==j)top=false;
                    if(top)at[j]=c.Bones[v].position;
                }
                // The pelvis's joint on the model's own pelvis bone (above it sit the skeleton's holders, at the feet).
                for(int v=0;v<c.Bones.Length;v++)if(c.Bones[v]!=null&&string.Equals(c.Bones[v].name,"pelvis",System.StringComparison.OrdinalIgnoreCase)){at[CapturedRig.Pelvis]=c.Bones[v].position;break;}
                corpse.Retarget(at);
            }
            var fit=new Dictionary<int,Bounds>();var ankle=new Dictionary<int,Bounds>();var corner=new Vector3();
            foreach(var r in c.All)
            {
                // (The corpse is still pooled away when this runs: the worn pieces are those active in themselves.)
                if(!(r is SkinnedMeshRenderer skin)||!skin.enabled||!skin.gameObject.activeSelf||skin.sharedMesh==null||skin.name==HotDogFaceName)continue;
                var boxes=CorpseBoneBoxes(skin.sharedMesh);if(boxes==null)continue;var bones=skin.bones;
                for(int i=0;i<boxes.Length&&i<bones.Length;i++)
                {
                    if(!(boxes[i] is Bounds bb)||bones[i]==null)continue;
                    int v=-1;for(var t=bones[i];t!=null&&v<0;t=t.parent)if(!index.TryGetValue(t,out v))v=-1;if(v<0||v>=c.Part.Length)continue;
                    int part=c.Part[v];if(corpse.IsCrushed(part)||!corpse.Parts.TryGetValue(part,out var body)||body==null)continue;
                    // A shin's foot apart (from the foot bone down), as the collapse turns it at the ankle.
                    bool foot=false;if(part==15||part==19)for(var t=c.Bones[v];t!=null&&!foot;t=t.parent){if(t.name.StartsWith("foot_",System.StringComparison.Ordinal))foot=true;else if(t.name.StartsWith("calf",System.StringComparison.Ordinal))break;}
                    var into=foot?ankle:fit;var m=body.transform.worldToLocalMatrix*bones[i].localToWorldMatrix;
                    for(int k=0;k<8;k++)
                    {
                        corner.Set(bb.center.x+((k&1)!=0?bb.extents.x:-bb.extents.x),bb.center.y+((k&2)!=0?bb.extents.y:-bb.extents.y),bb.center.z+((k&4)!=0?bb.extents.z:-bb.extents.z));
                        var p=m.MultiplyPoint3x4(corner);if(into.TryGetValue(part,out var f)){f.Encapsulate(p);into[part]=f;}else into[part]=new Bounds(p,Vector3.zero);
                    }
                }
            }
            foreach(var kv in fit)corpse.FitBox[kv.Key]=kv.Value;foreach(var kv in ankle)if(fit.ContainsKey(kv.Key))corpse.FitAnkle[kv.Key]=kv.Value;
            corpse.Refit(arena);

        }
    }
}
