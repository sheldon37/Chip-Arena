using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    internal sealed partial class NativeCorpse
    {
        private NativeCollapse collapse;private bool collapseReleased;private readonly Dictionary<int,Matrix4x4> ankleFrames=new Dictionary<int,Matrix4x4>();private readonly Dictionary<string,int> collapseBones=new Dictionary<string,int>();
        /// <summary>The model's own boxes per part, in the part's frame (ArenaPresentation.VisualCorpseFit): the part's bones, and for a
        /// shin its foot's apart (the collapse turns the foot at the ankle). Empty for a body without a model.</summary>
        internal readonly Dictionary<int,Bounds> FitBox=new Dictionary<int,Bounds>(),FitAnkle=new Dictionary<int,Bounds>();
        private readonly Dictionary<int,BoxCollider> ankleBoxes=new Dictionary<int,BoxCollider>();
        private static Vector3 FitSize(Vector3 s)=>new Vector3(Mathf.Clamp(s.x,.06f,1f),Mathf.Clamp(s.y,.06f,1.1f),Mathf.Clamp(s.z,.06f,1f));
        /// <summary>The cosmetic body made to the model's shape (owner 2026-11-09: "clipping into floor"): each part's box its FitBox, each
        /// shin a second box for its foot, and a collapse begun again on those boxes. Before the first step.</summary>
        internal void Refit(NativeArena arena)
        {
            if(Halved||Smash!=null&&Smash.Pinned)return;
            foreach(var kv in FitBox){if(!Parts.TryGetValue(kv.Key,out var body)||body==null)continue;var box=body.GetComponent<BoxCollider>();if(box==null||!box.enabled)continue;box.center=kv.Value.center;box.size=FitSize(kv.Value.size);}
            foreach(var kv in FitAnkle)
            {
                if(!Parts.TryGetValue(kv.Key,out var body)||body==null||ankleBoxes.ContainsKey(kv.Key))continue;var main=body.GetComponent<BoxCollider>();if(main==null||!main.enabled)continue;
                var foot=body.gameObject.AddComponent<BoxCollider>();foot.sharedMaterial=surface;foot.center=kv.Value.center;foot.size=FitSize(kv.Value.size);ankleBoxes[kv.Key]=foot;
                foreach(var other in Parts.Values)if(other!=null)foreach(var col in other.GetComponents<Collider>())if(col!=foot)Physics.IgnoreCollision(foot,col);
            }
            if(collapse!=null&&!collapseReleased){collapse=null;ankleFrames.Clear();collapseBones.Clear();SetupCollapse(arena,null);}
        }
        /// <summary>The cosmetic body's parts moved onto the model's own joints (`at`, world, at the death pose), each joint re-anchored,
        /// so its limbs are the model's length: built on the classic rig's joints, a model's longer or lifted limbs (the Hot Dog's legs)
        /// reached past their parts and through the floor. Before the first step; Refit after.</summary>
        internal void Retarget(Dictionary<int,Vector3> at)
        {
            if(Halved||Smash!=null&&Smash.Pinned||DropAt>=0||at==null||at.Count==0)return;
            foreach(var kv in at){if(!Parts.TryGetValue(kv.Key,out var body)||body==null||IsCrushed(kv.Key))continue;body.transform.position=kv.Value;body.position=kv.Value;inverse[kv.Key]=body.transform.worldToLocalMatrix;}
            foreach(var body in Parts.Values){var joint=body!=null?body.GetComponent<ConfigurableJoint>():null;if(joint==null||joint.connectedBody==null)continue;joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(body.transform.position);}
        }
        // A foot's box once the collapse lets go: its fitted box turned as the ankle left it.
        private void FootBoxesAtRelease()
        {
            foreach(var kv in ankleBoxes)
            {
                if(kv.Value==null||!FitAnkle.TryGetValue(kv.Key,out var b)||!ankleFrames.TryGetValue(kv.Key,out var m))continue;Bounds? turned=null;
                for(int k=0;k<8;k++){var c=new Vector3(b.center.x+((k&1)!=0?b.extents.x:-b.extents.x),b.center.y+((k&2)!=0?b.extents.y:-b.extents.y),b.center.z+((k&4)!=0?b.extents.z:-b.extents.z));var q=m.MultiplyPoint3x4(c);if(turned is Bounds t){t.Encapsulate(q);turned=t;}else turned=new Bounds(q,Vector3.zero);}
                if(turned is Bounds tb){kv.Value.center=tb.center;kv.Value.size=FitSize(tb.size);}
            }
        }
        private static System.Numerics.Vector3 Source(Vector3 p)=>CapturedRig.Mirror(CapturedAnimation.Num(p));
        private static System.Numerics.Quaternion Source(Quaternion q)=>CapturedRig.Mirror(CapturedAnimation.Num(q));
        private void SetupCollapse(NativeArena arena,CapturedAnimation.Player rig)
        {
            if(Halved)return;
            if(!Death.kneel||Death.model!="pilot"||Style!="impact-ragdoll")return;
            var sourceParts=NativeSourceModels.WoundParts;var parts=new List<CollapsePart>();var reflection=Matrix4x4.Scale(new Vector3(-1,1,1));
            foreach(var pair in Parts)
            {
                int b=pair.Key;var body=pair.Value;int parent=CapturedRig.Bones[b].Parent;while(parent>=0&&!Parts.ContainsKey(parent))parent=CapturedRig.Bones[parent].Parent;
                var p=new CollapsePart{Name=CapturedRig.Bones[b].Name,Parent=parent<0?null:CapturedRig.Bones[parent].Name,Position=Source(body.position),Rotation=Source(body.rotation),Free=b!=1&&body.GetComponent<ConfigurableJoint>()==null,Anchor=parent<0?System.Numerics.Vector3.Zero:Source(Parts[parent].transform.InverseTransformPoint(body.position))};
                var boxes=new List<CollapseBox>();
                if(FitBox.TryGetValue(b,out var fb))
                {
                    boxes.Add(new CollapseBox{Center=Source(fb.center),Rotation=Source(Quaternion.identity),Half=CapturedAnimation.Num(FitSize(fb.size)*.5f)});
                    if(FitAnkle.TryGetValue(b,out var fa))boxes.Add(new CollapseBox{Center=Source(fa.center),Rotation=Source(Quaternion.identity),Half=CapturedAnimation.Num(FitSize(fa.size)*.5f),Ankle=true});
                }
                else foreach(var d in sourceParts.Where(d=>d.root==p.Name))
                {var m=new Matrix4x4();for(int i=0;i<16;i++)m[i%4,i/4]=d.matrix[i];m=body.transform.worldToLocalMatrix*Bind[d.bone]*reflection*m*reflection;
                    boxes.Add(new CollapseBox{Center=Source(m.GetColumn(3)),Rotation=Source(m.rotation),Half=CapturedAnimation.Num(m.lossyScale*.5f),Ankle=(b==15&&BoneWithin(d.bone,16))||(b==19&&BoneWithin(d.bone,20))});}
                p.Boxes=boxes.ToArray();if(b==15||b==19){int foot=b+1;p.HasAnkle=true;p.AnklePivot=Source(body.transform.InverseTransformPoint(Bind[foot].GetColumn(3)));p.AnkleFrame=Source(Quaternion.Inverse(body.rotation)*Bind[foot].rotation);}parts.Add(p);collapseBones[p.Name]=b;
            }
            if(!NativeCollapse.Supports(parts))return;float ground=Death.position.y-.865f;if(arena.Root.scene.GetPhysicsScene().Raycast(Death.position,Vector3.down,out var hit,2,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))ground=hit.point.y;
            var props=arena.AllBlocks.Where(p=>p!=null).Select(p=>new CollapseProp{Position=Source(p.transform.position),Rotation=Source(p.transform.rotation),Size=CapturedAnimation.Num(p.transform.lossyScale)}).ToArray();var direction=Death.wound.direction;
            collapse=new NativeCollapse(parts.ToArray(),-Death.yaw*Mathf.Deg2Rad,Source(Death.velocity),ground,new System.Numerics.Vector3(direction.x,direction.y,direction.z),Death.wound.severity,props);
            DriveCollapse(0);
        }
        private static bool BoneWithin(int bone,int root){while(bone>=0){if(bone==root)return true;bone=CapturedRig.Bones[bone].Parent;}return false;}
        private void DriveCollapse(float age)
        {
            if(collapse==null||collapseReleased)return;bool release=age>=collapse.Release-1e-6f;
            collapse.Each(Math.Min(age,collapse.Release),(name,pose,linear,angular)=>
            {
                int b=collapseBones[name];var body=Parts[b];body.isKinematic=!release;body.position=CapturedAnimation.Vec(CapturedRig.Mirror(pose.Position));body.rotation=CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation));
                if(b==15||b==19){var pivot=(inverse[b]*Bind[b+1]).GetColumn(3);var q=CapturedAnimation.Quat(CapturedRig.Mirror(pose.Ankle));ankleFrames[b]=Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(q)*Matrix4x4.Translate(-pivot);}
                if(release){body.linearVelocity=CapturedAnimation.Vec(CapturedRig.Mirror(linear));body.angularVelocity=new Vector3(angular.X,-angular.Y,-angular.Z);}
            });collapseReleased=release;if(release)FootBoxesAtRelease();
        }
        private Matrix4x4 CollapseFoot(int b){int a=Anchor[b];return ankleFrames.TryGetValue(a,out var m)&&((a==15&&BoneWithin(b,16))||(a==19&&BoneWithin(b,20)))?m:Matrix4x4.identity;}
    }
}
