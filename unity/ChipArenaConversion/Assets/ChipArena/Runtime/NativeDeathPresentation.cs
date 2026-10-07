using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    // Cosmetic bodies have their own physics scene. They never become authority colliders.
    internal sealed class NativeCorpse : IDisposable
    {
        public readonly NativeDeath Death;public readonly Dictionary<int,Rigidbody> Parts=new Dictionary<int,Rigidbody>();
        public readonly Matrix4x4[] Bind=new Matrix4x4[22];public readonly int[] Anchor=new int[22];
        private readonly Matrix4x4[] inverse=new Matrix4x4[22];private readonly Scene scene;private readonly PhysicsScene physics;private readonly GameObject root;private readonly PhysicsMaterial surface;
        private float clock;private bool frozen;private static readonly int[] Bones={1,2,5,7,8,11,12,14,15,18,19};
        public NativeCorpse(NativeDeath death,CapturedAnimation.Player rig,NativeArena arena)
        {
            Death=death;scene=SceneManager.CreateScene("Cosmetic body "+death.id+" "+death.seed,new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();root=new GameObject("Cosmetic body");SceneManager.MoveGameObjectToScene(root,scene);
            surface=new PhysicsMaterial("Body surface"){dynamicFriction=.7f,staticFriction=.7f,bounciness=death.style=="ragdoll-pinball"?.82f:.15f};
            for(int i=0;i<22;i++)Bind[i]=rig.Bones[i].localToWorldMatrix;
            foreach(int b in Bones)
            {
                var go=new GameObject(CapturedRig.Bones[b].Name);go.transform.SetParent(root.transform);go.transform.SetPositionAndRotation(rig.Bones[b].position,rig.Bones[b].rotation);
                var body=go.AddComponent<Rigidbody>();body.useGravity=false;body.linearDamping=.27f;body.angularDamping=b<=2?.6f:1.1f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var box=go.AddComponent<BoxCollider>();box.sharedMaterial=surface;
                var points=new List<Vector3>();foreach(var shape in CapturedRig.Boxes){int ancestor=shape.Bone;while(ancestor>=0&&Array.IndexOf(Bones,ancestor)<0)ancestor=CapturedRig.Bones[ancestor].Parent;if(ancestor!=b)continue;Matrix4x4 local=go.transform.worldToLocalMatrix*Bind[shape.Bone];Vector3 pos=CapturedAnimation.Vec(CapturedRig.Mirror(shape.Position)),size=CapturedAnimation.Vec(shape.Size)*.5f;for(int k=0;k<8;k++)points.Add(local.MultiplyPoint3x4(pos+new Vector3((k&1)==0?-size.x:size.x,(k&2)==0?-size.y:size.y,(k&4)==0?-size.z:size.z)));}
                var bounds=new Bounds(Vector3.zero,Vector3.one*.1f);foreach(var point in points)bounds.Encapsulate(point);box.center=bounds.center;box.size=new Vector3(Mathf.Clamp(bounds.size.x,.1f,.8f),Mathf.Clamp(bounds.size.y,.1f,.9f),Mathf.Clamp(bounds.size.z,.1f,.8f));body.mass=Math.Max(.01f,2*box.size.x*box.size.y*box.size.z);
                inverse[b]=go.transform.worldToLocalMatrix;Parts[b]=body;
                Vector3 dir=death.direction.sqrMagnitude>.01f?death.direction.normalized:Quaternion.Euler(0,death.yaw,0)*Vector3.back;
                if(death.style=="impact-ragdoll"||death.kept)body.linearVelocity=Vector3.ClampMagnitude(death.velocity,100);
                else{float force=death.melee?8+Math.Min(7,death.force/100):2.2f+Math.Min(2,death.force/160);body.linearVelocity=dir*force+Vector3.ClampMagnitude(death.velocity,5)*.3f+Vector3.up*(death.melee?3.8f:1);body.angularVelocity=Vector3.Cross(death.position-go.transform.position,dir)*(death.melee?15:5);}
            }
            for(int i=0;i<22;i++){int a=i;while(a>=0&&!Parts.ContainsKey(a))a=CapturedRig.Bones[a].Parent;Anchor[i]=a<0?1:a;}
            foreach(var pair in Parts)
            {
                int b=pair.Key,parent=CapturedRig.Bones[b].Parent;while(parent>=0&&!Parts.ContainsKey(parent))parent=CapturedRig.Bones[parent].Parent;if(parent<0)continue;
                bool detached=death.style=="dev-gibber"&&b==(death.wound?.part??5)||death.wound?.destroyed==true&&death.wound.part==b;if(detached)continue;
                var joint=pair.Value.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=Parts[parent];joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=Parts[parent].transform.InverseTransformPoint(pair.Value.position);joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                bool elbow=b==8||b==12,knee=b==15||b==19,head=b==5;
                joint.lowAngularXLimit=new SoftJointLimit{limit=elbow?-149:knee?-3:head?-32:-75};joint.highAngularXLimit=new SoftJointLimit{limit=elbow?3:knee?149:head?37:75};joint.angularYLimit=new SoftJointLimit{limit=elbow||knee?2:head?20:65};joint.angularZLimit=new SoftJointLimit{limit=elbow||knee?2:head?20:65};joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.08f;joint.projectionAngle=12;
            }
            var colliders=Parts.Values.Select(b=>b.GetComponent<Collider>()).ToArray();for(int a=0;a<colliders.Length;a++)for(int b=a+1;b<colliders.Length;b++)Physics.IgnoreCollision(colliders[a],colliders[b]);
            foreach(var prop in arena.Props.Where(p=>p!=null&&(p.Indestructible||Vector3.Distance(p.transform.position,death.position)<8)).Take(48))
            {var floor=new GameObject("Body obstacle");floor.transform.SetParent(root.transform);floor.transform.SetPositionAndRotation(prop.transform.position,prop.transform.rotation);floor.transform.localScale=prop.transform.lossyScale;var box=floor.AddComponent<BoxCollider>();var original=prop.GetComponent<BoxCollider>();if(original!=null){box.center=original.center;box.size=original.size;}box.sharedMaterial=surface;}
            Physics.SyncTransforms();
        }
        public Matrix4x4 Bone(int b)=>Parts[Anchor[b]].transform.localToWorldMatrix*inverse[Anchor[b]]*Bind[b];
        public Vector3 Center=>Parts[1].position;
        public void Step(float age)
        {
            if(frozen||age<clock)return;int steps=Math.Min(8,(int)((age-clock)*60));
            for(int i=0;i<steps;i++)
            {
                foreach(var body in Parts.Values)
                {Vector3 force=Vector3.down*18;if(Death.style=="ragdoll-gravity")force=Vector3.up*2;if(Death.style=="ragdoll-fireworks"&&clock>.7f&&clock<1.3f)force+=Vector3.up*40;body.AddForce(force,ForceMode.Acceleration);}
                physics.Simulate(1f/60);clock+=1f/60;
            }
            if(age>2.8f&&Death.kept){foreach(var b in Parts.Values)b.isKinematic=true;frozen=true;}
        }
        public void Dispose(){CapturedAnimation.Release(root);CapturedAnimation.Release(surface);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);}
    }
    public sealed partial class ArenaPresentation
    {
        private readonly Dictionary<int,NativeCorpse> corpses=new Dictionary<int,NativeCorpse>();
        private void ClearCorpses(){foreach(var c in corpses.Values)c.Dispose();corpses.Clear();}
        private void DeathPresentation(NativeArena arena)
        {
            var deaths=arena.Pilots.Where(p=>p.Death!=null).Select(p=>p.Death).Concat(arena.MinionDeaths).ToArray();
            foreach(var item in corpses.ToArray())if(!deaths.Any(d=>d.id==item.Key&&d.seed==item.Value.Death.seed)||arena.Time-item.Value.Death.at>5&&!item.Value.Death.kept||arena.Pilots.Any(p=>p.Seat.id==item.Key&&p.Hp>0&&arena.Time-p.RevivedAt>1.1f)){item.Value.Dispose();corpses.Remove(item.Key);}
            foreach(var death in deaths)
            {
                float age=arena.Time-death.at;var live=arena.Pilots.Find(p=>p.Seat.id==death.id);bool revived=live?.Hp>0;float up=revived?arena.Time-live.RevivedAt:-1;if(age<0||!death.kept&&age>5||revived&&up>1.1f)continue;
                if(!corpses.TryGetValue(death.id,out var corpse))
                {
                    if(corpses.Count>=32)continue;CapturedAnimation.Player rig=null;bool temporary=false;
                    if(!rigs.TryGetValue(death.id,out rig)&&!minionRigs.TryGetValue(death.id,out rig)){rig=captured.Create(transform,death.id);var actor=death.actor??new Actor();actor.Hp=Math.Max(1,actor.Hp);rig.ActorPose(actor,death.position-Vector3.up*.865f,death.at);temporary=true;}
                    corpse=new NativeCorpse(death,rig,arena);corpses[death.id]=corpse;if(temporary)rig.Dispose();
                }
                corpse.Step(age);string style=death.style??"impact-ragdoll";float alpha=death.kept?1:Mathf.Clamp01((5-age)/.65f);Vector3 center=corpse.Center;Matrix4x4 overlay=Matrix4x4.identity;float scale=1;
                if(style=="shrink"||style=="dev-singularity")scale=Mathf.Max(.002f,1-Mathf.Clamp01(age/1.8f));
                if(style=="ascension"||style=="ghost"||style=="ragdoll-soul")overlay=Matrix4x4.Translate(Vector3.up*age*(style=="ascension"?2:1));
                if(style=="launch")overlay=Matrix4x4.Translate(Vector3.up*Mathf.Min(20,age*age*9));
                if(style=="voxel-burst"||style=="confetti"||style=="siren-dissipate"||style=="siren-crush")alpha=0;
                if(revived){alpha*=1-Mathf.Clamp01(up/.8f);Ring(center,.6f+up,2,up*45);VoxelCloud(center,up,2,death.seed,30);}
                Matrix4x4 Bone(int b)=>overlay*Matrix4x4.Translate(center)*Matrix4x4.Scale(Vector3.one*scale)*Matrix4x4.Translate(-center)*corpse.Bone(b);
                bool Visible(int b)=>death.wound?.kind!="crush"||!BoneWithin(b,death.wound.part);
                if(alpha>.001f){sourceModels.Queue(death.model??"pilot",Matrix4x4.identity,Bone,Visible,body:BodyTint(death.actor?.BodyColor),accent:AccentTint(death.actor?.AccentColor),opacity:alpha);if(death.model=="pilot"&&!string.IsNullOrEmpty(death.actor?.Hat))sourceModels.Queue("hat-"+death.actor.Hat,Matrix4x4.identity,Bone,null,opacity:alpha);}
                if(style=="ragdoll-ember")VoxelCloud(center,age,7,death.seed,28,true);
                if(style=="ragdoll-frost")VoxelCloud(center,age,8,death.seed,20);
                if(style=="ragdoll-static")for(int j=0;j<4;j++)Beam(corpse.Bone(5).GetColumn(3),corpse.Bone(new[]{8,12,15,19}[j]).GetColumn(3),.018f*(1+Mathf.Sin(age*90+j)),9);
                if(style=="ragdoll-fireworks"||style=="confetti"||style=="voxel-burst")VoxelCloud(death.position,age,style=="voxel-burst"?4:6,death.seed,60,style=="ragdoll-fireworks");
                if(style=="ragdoll-petals")VoxelCloud(center,age,6,death.seed,24,true);
                if(style=="dev-singularity"){Ring(center,.3f+Mathf.Sin(age*2)*.3f,10,age*180);VoxelCloud(center,Math.Max(0,2-age),10,death.seed,44);}
                if(style=="siren-dissipate"||style=="siren-crush"){VoxelCloud(death.position,age,10,death.seed,50,true);Ring(death.position-Vector3.up*.75f,Mathf.Clamp01(age)*2,7,age*60);}
                if(style=="dev-raging-demon")for(int j=0;j<12;j++){float t=j*.13f;if(age>=t&&age<t+.12f){Vector3 p=death.position+new Vector3(Mathf.Sin(j*13)*.45f,Mathf.Cos(j*9)*.7f,Mathf.Cos(j*11)*.45f);Beam(p-Vector3.up*.24f,p+Vector3.up*.24f,.08f,7);}}
                if(style=="dev-clown"||style=="dev-clown-love"||style=="dev-bass-van")DeathCinematic(style,death,age,center);
            }
        }
        private static bool BoneWithin(int bone,int root){while(bone>=0){if(bone==root)return true;bone=CapturedRig.Bones[bone].Parent;}return false;}
        private void Beam(Vector3 from,Vector3 to,float width,int color){Vector3 d=to-from;if(d.sqrMagnitude>.00001f)Add(color,Matrix4x4.TRS((from+to)*.5f,Quaternion.LookRotation(d),new Vector3(width,width,d.magnitude)));}
        private void VoxelCloud(Vector3 center,float age,int color,int seed,int count,bool rising=false)
        {for(int j=0;j<count;j++){float angle=(j+seed%101)*2.399963f,life=Mathf.Clamp01(1-age/5),r=(.5f+j%7*.12f)*age;Vector3 p=center+new Vector3(Mathf.Sin(angle)*r,(rising?age:Mathf.Max(-.65f,(1+j%4*.3f)*age-4*age*age)),Mathf.Cos(angle)*r);Add(color==6?6+j%5:color,Matrix4x4.TRS(p,Quaternion.Euler(j*13+age*80,j*27,age*100),Vector3.one*(.04f+j%3*.025f)*life));}}
        private void DeathCinematic(string style,NativeDeath death,float age,Vector3 center)
        {
            // Native staged props share the original five-second elimination lifetime.
            if(style=="dev-bass-van")
            {float x=age<.7f?Mathf.Lerp(-8,-1,age/.7f):age>4.1f?-1+(age-4.1f)*14:-1;var root=Matrix4x4.TRS(death.position+Vector3.right*x+Vector3.up*.1f,Quaternion.Euler(0,90,0),Vector3.one);Part(root,new Vector3(0,0,0),new Vector3(2,1.3f,3.6f),Quaternion.identity,10);Part(root,new Vector3(0,.8f,.4f),new Vector3(1.9f,.5f,2.3f),Quaternion.identity,5);for(int i=0;i<4;i++)Part(root,new Vector3(i%2==0?-1:1,-.6f,i<2?-1.1f:1.1f),Vector3.one*.5f,Quaternion.identity,3);if(age>2.35f)Ring(death.position,1+.25f*Mathf.Sin(age*22),9,age*70);return;}
            Vector3 at=center+Vector3.forward*.9f;float sway=Mathf.Sin(age*10)*.18f;Add(4,Matrix4x4.TRS(at+Vector3.up*(.25f+sway),Quaternion.Euler(0,180,15*Mathf.Sin(age*6)),new Vector3(.48f,.75f,.3f)));Add(4,Matrix4x4.TRS(at+Vector3.up*(.9f+sway),Quaternion.identity,Vector3.one*.38f));Add(7,Matrix4x4.TRS(at+new Vector3(0,.89f+sway,-.22f),Quaternion.identity,Vector3.one*.12f));
            if(style=="dev-clown-love"&&age>=1.3f&&age<=3.6f)VoxelCloud(at+Vector3.up,Mathf.Repeat(age-1.3f,.4f),6,death.seed,8,true);
        }
    }
}
