using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    internal sealed partial class NativeCorpse
    {
        private NativeCollapse collapse;private bool collapseReleased;private readonly Dictionary<int,Matrix4x4> ankleFrames=new Dictionary<int,Matrix4x4>();private readonly Dictionary<string,int> collapseBones=new Dictionary<string,int>();
        private static System.Numerics.Vector3 Source(Vector3 p)=>CapturedRig.Mirror(CapturedAnimation.Num(p));
        private static System.Numerics.Quaternion Source(Quaternion q)=>CapturedRig.Mirror(CapturedAnimation.Num(q));
        private void SetupCollapse(NativeArena arena,CapturedAnimation.Player rig)
        {
            if(!Death.kneel||Death.model!="pilot"||Style!="impact-ragdoll")return;
            var sourceParts=NativeSourceModels.WoundParts;var parts=new List<CollapsePart>();var reflection=Matrix4x4.Scale(new Vector3(-1,1,1));
            foreach(var pair in Parts)
            {
                int b=pair.Key;var body=pair.Value;int parent=CapturedRig.Bones[b].Parent;while(parent>=0&&!Parts.ContainsKey(parent))parent=CapturedRig.Bones[parent].Parent;
                var p=new CollapsePart{Name=CapturedRig.Bones[b].Name,Parent=parent<0?null:CapturedRig.Bones[parent].Name,Position=Source(body.position),Rotation=Source(body.rotation),Free=b!=1&&body.GetComponent<ConfigurableJoint>()==null,Anchor=parent<0?System.Numerics.Vector3.Zero:Source(Parts[parent].transform.InverseTransformPoint(body.position))};
                var boxes=new List<CollapseBox>();foreach(var d in sourceParts.Where(d=>d.root==p.Name))
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
            });collapseReleased=release;
        }
        private Matrix4x4 CollapseFoot(int b){int a=Anchor[b];return ankleFrames.TryGetValue(a,out var m)&&((a==15&&BoneWithin(b,16))||(a==19&&BoneWithin(b,20)))?m:Matrix4x4.identity;}
    }
}
