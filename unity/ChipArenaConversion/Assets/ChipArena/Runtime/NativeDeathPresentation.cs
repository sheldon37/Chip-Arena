using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    // Cosmetic bodies have their own physics scene. They never become authority colliders.
    internal sealed partial class NativeCorpse : IDisposable
    {
        public readonly NativeDeath Death;public readonly string Style;private Rigidbody weapon;public readonly Dictionary<int,Rigidbody> Parts=new Dictionary<int,Rigidbody>();
        public readonly Matrix4x4[] Bind=new Matrix4x4[22];public readonly int[] Anchor=new int[22];
        private readonly Matrix4x4[] inverse=new Matrix4x4[22];private readonly Scene scene;private readonly PhysicsScene physics;private readonly GameObject root;private readonly PhysicsMaterial surface;
        private float clock;private bool frozen,stepped;private int weaponPart;private static readonly int[] Bones={1,2,5,7,8,11,12,14,15,18,19};
        public NativeCorpse(NativeDeath death,CapturedAnimation.Player rig,NativeArena arena)
        {
            Death=death;Style=death.kept&&!new[]{"impact-ragdoll","ragdoll","dev-gibber","siren-crush","vent-fall","vent-splat"}.Contains(death.style)?"impact-ragdoll":death.style;scene=SceneManager.CreateScene("Cosmetic body "+death.id+" "+death.seed,new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();root=new GameObject("Cosmetic body");SceneManager.MoveGameObjectToScene(root,scene);
            surface=new PhysicsMaterial("Body surface"){dynamicFriction=.7f,staticFriction=.7f,bounciness=Style=="ragdoll-pinball"?.82f:.15f};
            Vector3 offset=death.position-rig.Root.transform.position;for(int i=0;i<22;i++)Bind[i]=Matrix4x4.Translate(offset)*rig.Bones[i].localToWorldMatrix;
            foreach(int b in Bones)
            {
                var go=new GameObject(CapturedRig.Bones[b].Name);go.transform.SetParent(root.transform);go.transform.SetPositionAndRotation(rig.Bones[b].position+offset,rig.Bones[b].rotation);
                var body=go.AddComponent<Rigidbody>();body.useGravity=false;body.linearDamping=.27f;body.angularDamping=b<=2?.6f:1.1f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var box=go.AddComponent<BoxCollider>();box.sharedMaterial=surface;
                var points=new List<Vector3>();foreach(var shape in CapturedRig.Boxes){int ancestor=shape.Bone;while(ancestor>=0&&Array.IndexOf(Bones,ancestor)<0)ancestor=CapturedRig.Bones[ancestor].Parent;if(ancestor!=b)continue;Matrix4x4 local=go.transform.worldToLocalMatrix*Bind[shape.Bone];Vector3 pos=CapturedAnimation.Vec(CapturedRig.Mirror(shape.Position)),size=CapturedAnimation.Vec(shape.Size)*.5f;for(int k=0;k<8;k++)points.Add(local.MultiplyPoint3x4(pos+new Vector3((k&1)==0?-size.x:size.x,(k&2)==0?-size.y:size.y,(k&4)==0?-size.z:size.z)));}
                var bounds=new Bounds(Vector3.zero,Vector3.one*.1f);foreach(var point in points)bounds.Encapsulate(point);box.center=bounds.center;box.size=new Vector3(Mathf.Clamp(bounds.size.x,.1f,.8f),Mathf.Clamp(bounds.size.y,.1f,.9f),Mathf.Clamp(bounds.size.z,.1f,.8f));body.mass=Math.Max(.01f,2*box.size.x*box.size.y*box.size.z);
                inverse[b]=go.transform.worldToLocalMatrix;Parts[b]=body;
                Vector3 dir=death.direction.sqrMagnitude>.01f?death.direction.normalized:Quaternion.Euler(0,death.yaw,0)*Vector3.back;
                if(Style=="impact-ragdoll"||death.kept)body.linearVelocity=Vector3.ClampMagnitude(death.velocity,100);
                else
                {
                    float force=death.melee?8+Math.Min(7,death.force/100):2.2f+Math.Min(2,death.force/160);body.linearVelocity=dir*force+Vector3.ClampMagnitude(death.velocity,5)*.3f+Vector3.up*(death.melee?3.8f:1);body.angularVelocity=Vector3.Cross(death.position-go.transform.position,dir)*(death.melee?15:5);
                    // ragdoll.ts: the Gibber's part pops up and spins away (velocity * 1.65 + (0,5,0), spin (11,8,-9), x mirrored).
                    if(Style=="dev-gibber"&&b!=CapturedRig.Pelvis&&b==(death.wound?.part??5)){body.linearVelocity=body.linearVelocity*1.65f+Vector3.up*5;body.angularVelocity=new Vector3(11,-8,9);}
                }
            }
            if(death.wound?.kind=="crush"&&Parts.TryGetValue(death.wound.part,out var crushed)){crushed.GetComponent<Collider>().enabled=false;crushed.isKinematic=true;}
            for(int i=0;i<22;i++){int a=i;while(a>=0&&!Parts.ContainsKey(a))a=CapturedRig.Bones[a].Parent;Anchor[i]=a<0?1:a;}
            foreach(var pair in Parts)
            {
                int b=pair.Key,parent=CapturedRig.Bones[b].Parent;while(parent>=0&&!Parts.ContainsKey(parent))parent=CapturedRig.Bones[parent].Parent;if(parent<0)continue;
                bool detached=Style=="dev-gibber"&&b==(death.wound?.part??5)||death.wound?.destroyed==true&&death.wound.part==b;if(detached||death.wound?.kind=="crush"&&parent==death.wound.part)continue;
                var joint=pair.Value.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=Parts[parent];joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=Parts[parent].transform.InverseTransformPoint(pair.Value.position);joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                bool elbow=b==8||b==12,knee=b==15||b==19,head=b==5;
                joint.lowAngularXLimit=new SoftJointLimit{limit=elbow?-149:knee?-3:head?-32:-75};joint.highAngularXLimit=new SoftJointLimit{limit=elbow?3:knee?149:head?37:75};joint.angularYLimit=new SoftJointLimit{limit=elbow||knee?2:head?20:65};joint.angularZLimit=new SoftJointLimit{limit=elbow||knee?2:head?20:65};joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.08f;joint.projectionAngle=12;
            }
            if(!string.IsNullOrEmpty(death.actor?.SourceSpec)&&death.actor.SourceSpec!="siren")
            {var go=new GameObject("Dropped weapon");go.transform.SetParent(root.transform);int hand=death.actor.SourceSpec=="fire"||death.actor.SourceSpec=="water"||death.actor.SourceSpec=="lightning"||death.actor.SourceSpec=="darkness"||death.actor.SourceSpec=="leet"?9:13;go.transform.SetPositionAndRotation(rig.Bones[hand].position+offset,rig.Bones[hand].rotation);weapon=go.AddComponent<Rigidbody>();weapon.mass=.7f;weapon.useGravity=false;weapon.linearVelocity=Vector3.ClampMagnitude(death.velocity,8);weapon.angularVelocity=new Vector3(1,2,1);weapon.linearDamping=.3f;var box=go.AddComponent<BoxCollider>();box.size=new Vector3(.15f,.15f,.45f);box.sharedMaterial=surface;
             // The source's weapon rides in its hand part (ragdoll.ts collect, weapon zone), so the style forces push it as that part.
             int k=0;foreach(int key in Parts.Keys){if(key==Anchor[hand]){weaponPart=k;break;}k++;}}
            var colliders=Parts.Values.Select(b=>b.GetComponent<Collider>()).ToArray();for(int a=0;a<colliders.Length;a++)for(int b=a+1;b<colliders.Length;b++)Physics.IgnoreCollision(colliders[a],colliders[b]);
            foreach(var prop in arena.Props.Where(p=>p!=null&&(p.Indestructible||Vector3.Distance(p.transform.position,death.position)<8)).Take(48))
            {var floor=new GameObject("Body obstacle");floor.transform.SetParent(root.transform);floor.transform.SetPositionAndRotation(prop.transform.position,prop.transform.rotation);floor.transform.localScale=prop.transform.lossyScale;var box=floor.AddComponent<BoxCollider>();var original=prop.GetComponent<BoxCollider>();if(original!=null){box.center=original.center;box.size=original.size;}box.sharedMaterial=surface;}
            SetupCollapse(arena,rig);Physics.SyncTransforms();
        }
        public Matrix4x4 Bone(int b)=>Parts[Anchor[b]].transform.localToWorldMatrix*CollapseFoot(b)*inverse[Anchor[b]]*Bind[b];
        public Vector3 Center=>Parts[1].position;
        /// <summary>Fixed 1/60 s physics up to <paramref name="age"/> (ragdoll.ts update: min(5, age), a kept body min(age,
        /// DOWNED.keepAge)). A body built late (a guest's first sight of a death, a corpse rebuilt after the killcam) catches up
        /// in one go, as the source steps without a cap, so its fall and blood are already where they are at that age; later
        /// frames step at most 8 at a time.</summary>
        public void Step(float age)
        {
            float target=Math.Min(age,Death.kept?(float)NativeKillEffectRules.KeepAge:(float)NativeKillEffectRules.DeathSeconds);
            if(frozen||target<clock){if(!frozen&&age>4.2f&&Death.kept)Freeze();return;}int steps=(int)((target-clock)*60+1e-3f);if(stepped)steps=Math.Min(8,steps);stepped=true;
            for(int step=0;step<steps;step++)
            {
                DriveCollapse(clock);int index=0;foreach(var body in Parts.Values){int i=index++;if(!body.isKinematic)Force(body,i);}
                if(weapon!=null)Force(weapon,weaponPart);
                physics.Simulate(1f/60);StepSpray();clock+=1f/60;
            }
            if(age>4.2f&&Death.kept)Freeze();
        }
        private void Freeze(){foreach(var b in Parts.Values)b.isKinematic=true;frozen=true;}
        private bool Cue(float at)=>clock+1e-6f>=at&&clock<at+1f/60-1e-6f;
        /// <summary>ragdoll-vfx.ts ragdollForce on one body (<paramref name="i"/>: its index in the source's part list; the
        /// dropped weapon takes its hand part's) and the world's 18 m/s² gravity.</summary>
        private void Force(Rigidbody body,int i)
        {
            Vector3 velocity=body.linearVelocity,pos=body.position;float gravity=18;
            if(Style=="ragdoll-frost"&&clock>=1.1f){gravity=0;velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            if(Style=="ragdoll-static"&&(Cue(.35f)||Cue(.7f)||Cue(1.05f))){velocity=new Vector3(velocity.x+(i%2==1?2:-2),3.5f,velocity.z);body.angularVelocity=new Vector3(i%2==1?4:-4,6,2);}
            if(Style=="ragdoll-pinball"&&(Cue(.8f)||Cue(1.65f)))velocity=new Vector3((i%2==1?1:-1)*1.4f,7,velocity.z*.5f);
            if(Style=="ragdoll-fireworks"&&Cue(.45f))velocity=new Vector3(velocity.x*.25f,10,velocity.z*.25f);
            if(Style=="ragdoll-soul"){gravity*=.13f;if(clock>.8f)velocity=new Vector3(velocity.x*.985f,Math.Min(2,velocity.y+.08f),velocity.z*.985f);}
            if(Style=="ragdoll-gravity"&&clock>.3f){gravity=0;Vector3 d=Death.position-pos;velocity=new Vector3(d.x*2.8f-d.z*2,(d.y+1.2f)*3,d.z*2.8f+d.x*2);}
            if(Style=="dev-singularity"&&clock>.22f){gravity=0;Vector3 d=Death.position-pos;velocity=new Vector3(d.x*3-d.z*2.6f,(d.y+2.4f)*3,d.z*3+d.x*2.6f);body.angularVelocity=new Vector3(.8f,3,.6f);}
            body.linearVelocity=velocity;body.AddForce(Vector3.down*gravity,ForceMode.Acceleration);
        }
        public void Dispose(){CapturedAnimation.Release(root);CapturedAnimation.Release(surface);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);}
    }
    public sealed partial class ArenaPresentation
    {
        private readonly Dictionary<int,NativeCorpse> corpses=new Dictionary<int,NativeCorpse>();private readonly List<int> staleCorpses=new List<int>();
        private void ClearCorpses(){foreach(var c in corpses.Values)c.Dispose();corpses.Clear();carriedDeaths.Clear();ReleaseKillEffects();}
        // The clock kill effects are presented on (see PresentDeath): FieldClock, the continuous snapshot clock a guest renders at
        // (PresentationClock on the host and in the killcam replay).
        private float KillClock(NativeArena arena)=>FieldClock(arena);
        private static bool StockEnding(string style)=>style=="impact-ragdoll"||style=="ragdoll"||style=="dev-gibber"||NativeKillEffectRules.Swarm(style);
        private static bool DeathListed(NativeArena arena,int id,int seed)
        {
            foreach(var p in arena.Pilots)if(p.Death!=null&&p.Death.id==id&&p.Death.seed==seed)return true;
            foreach(var d in arena.MinionDeaths)if(d.id==id&&d.seed==seed)return true;return false;
        }
        private static NativePilot PilotOf(NativeArena arena,int id){foreach(var p in arena.Pilots)if(p.Seat.id==id)return p;return null;}
        private void DeathPresentation(NativeArena arena)
        {
            staleCorpses.Clear();
            foreach(var item in corpses){var d=item.Value.Death;var live=PilotOf(arena,item.Key);if(!DeathListed(arena,item.Key,d.seed)||arena.Time-d.at>5&&!d.kept||live!=null&&live.Hp>0&&arena.Time-live.RevivedAt>1.1f)staleCorpses.Add(item.Key);}
            foreach(int id in staleCorpses){corpses[id].Dispose();corpses.Remove(id);}
            // DeathEffects: live (not kept) pilot effects share ANIMATION.maxDeathVoxels pieces; a new one gets what is left.
            killUsed=0;foreach(var c in corpses.Values)if(c.Counted)killUsed+=c.FxCount;
            BeginKillFrame();
            foreach(var p in arena.Pilots)if(p.Death!=null)PresentDeath(arena,p.Death,true);
            foreach(var d in arena.MinionDeaths)PresentDeath(arena,d,false);
            EndKillFrame();
        }
        private void PresentDeath(NativeArena arena,NativeDeath death,bool pilot)
        {
            float age=arena.Time-death.at;var live=PilotOf(arena,death.id);bool revived=live?.Hp>0;float up=revived?arena.Time-live.RevivedAt:-1;if(age<0||!death.kept&&age>5||revived&&up>1.1f)return;
            if(SourceMinionDeath(death,age)||SourceDeathScene(death,age,up))return;
            if(!corpses.TryGetValue(death.id,out var corpse))
            {
                if(corpses.Count>=32)return;CapturedAnimation.Player rig=null;bool temporary=false;
                if(!rigs.TryGetValue(death.id,out rig)&&!minionRigs.TryGetValue(death.id,out rig)){rig=captured.Create(transform,death.id);var actor=death.actor??new Actor();actor.Hp=Math.Max(1,actor.Hp);rig.ActorPose(actor,death.position-Vector3.up*.865f,death.at);temporary=true;}
                corpse=new NativeCorpse(death,rig,arena);corpses[death.id]=corpse;if(temporary)rig.Dispose();
                bool counted=pilot&&!death.kept;int budget=death.kept?NativeKillEffectRules.KeptBudget:counted?NativeKillEffectRules.MaxDeathVoxels-killUsed:NativeKillEffectRules.MaxDeathVoxels;
                bool hatModel=!string.IsNullOrEmpty(death.actor?.Hat)&&death.actor.Hat!="none"&&sourceModels.Has("hat-"+death.actor.Hat);
                int band=pilot&&live!=null&&(death.model??"pilot")=="pilot"?NativeStatusVisuals.BandTeam(arena.Match.Teams,arena.Match.Cooperative,live.Seat.AuthorityBoss,live.Seat.team):-1;
                corpse.SetupKill(budget,Mathf.Clamp01(EffectsQuality),counted,hatModel,band);if(counted)killUsed+=corpse.FxCount;
            }
            corpse.Step(age);string style=corpse.Style??"impact-ragdoll";bool stock=StockEnding(style);
            // What the effect looks like (swarm, burst, fragments, spray fade and splats, the stock sink and shrink, the fades)
            // is a function of age alone, so it follows the presentation clock: arena.Time only moves when a 20 Hz host frame
            // lands on a guest and in the killcam, and at the 60 Hz fixed rate on the host (the source feeds DeathEffects the
            // continuous snapshot cursor). The bodies keep their fixed 1/60 s physics on arena.Time above. Never runs backwards.
            float clock=KillClock(arena),fx=corpse.FxAge(clock-death.at),upFx=revived?Math.Max(0,clock-live.RevivedAt):-1;
            // A PvE kept body holds the frame at DOWNED.keepAge (DeathEffects keep: update(min(age, keepAge))).
            float shown=death.kept?Math.Min(fx,(float)NativeKillEffectRules.KeepAge):fx;
            float alpha=death.kept||stock?1:Mathf.Clamp01((5-fx)/.65f);Matrix4x4 overlay=Matrix4x4.identity;
            if(style=="ascension"||style=="ghost")overlay=Matrix4x4.Translate(Vector3.up*fx*(style=="ascension"?2:1));
            if(style=="vent-fall"){overlay=Matrix4x4.Translate(Vector3.down*Mathf.Min(15,fx*fx*9));if(fx>.6f)alpha=0;}
            if(style=="vent-splat"){overlay=Matrix4x4.Translate(Vector3.up*Math.Max(0,1-fx/.6f)*10);if(fx>.6f&&fx<1.5f)VoxelCloud(death.position,fx-.6f,4,death.seed,30);}
            if(style=="launch")overlay=Matrix4x4.Translate(Vector3.up*Mathf.Min(20,fx*fx*9));
            if(style=="voxel-burst"||style=="confetti"||style=="siren-dissipate"||style=="siren-crush")alpha=0;
            // ragdoll.ts update(): the stock sink and shrink, then RagdollVfx (centred on the presented pelvis) and its collapse.
            float sink=stock?(float)NativeKillEffectRules.Sink(shown):0,scale=stock?(float)NativeKillEffectRules.PartScale(shown):1;corpse.Present(overlay,sink,scale);
            var swarm=corpse.Swarm;
            if(swarm!=null)
            {
                int n=0;foreach(var part in corpse.Parts.Keys){if(corpse.Crushed&&part==death.wound.part)continue;Vector3 at=corpse.PresentedPosition(part);corpse.SwarmParts[n*3]=-at.x;corpse.SwarmParts[n*3+1]=at.y;corpse.SwarmParts[n*3+2]=at.z;n++;}
                Vector3 pelvis=corpse.PresentedPosition(1);swarm.Update(shown,-pelvis.x,pelvis.y,pelvis.z,corpse.SwarmParts,n);corpse.Present(overlay,sink,scale*(float)swarm.Collapse);
            }
            Vector3 center=corpse.PresentedPosition(1);
            if(revived){alpha*=1-Mathf.Clamp01(upFx/.8f);Ring(center,.6f+upFx,2,upFx*45);VoxelCloud(center,upFx,2,death.seed,30);}
            bool soul=swarm!=null&&swarm.BodyColor>=0&&style=="ragdoll-soul"&&corpse.RigCount>0,frost=swarm!=null&&swarm.BodyColor>=0&&style=="ragdoll-frost";
            // The hat rides on the head unless it flies loose; a crushed head that threw no hat shows none (it had no part). The
            // dropped weapon is presented like its hand part (sink, shrink, collapse; NativeCorpse.PresentedWeapon).
            var drop=corpse.PresentedWeapon;
            bool hat=death.model=="pilot"&&corpse.HatModel!=null&&corpse.LooseHat<0&&!(corpse.Crushed&&death.wound.part==CapturedRig.Head),weapon=drop.HasValue&&corpse.LooseModel!=null;
            if(soul)
            {
                // Soul Drift: the body and what it carried as translucent cyan plain boxes (round hats and weapons from their models).
                NativeKillEffectRules.Linear(swarm.BodyColor,out double r,out double g,out double b);soul=KillGhost(corpse,new Color((float)r,(float)g,(float)b,1),alpha*(float)swarm.BodyOpacity,hat,weapon);
            }
            if(!soul&&alpha>.001f)
            {
                // ragdoll.ts copies the rig with copyVoxelPart (castShadow off): the body, hat and weapon cast no shadow.
                string model=(death.model??"pilot")=="pilot"?sourceModels.WoundedPilot(death.id,death.actor?.Wounds,death.actor?.BodyColor,ForceCompatibilityRendering):death.model;bool recolored=false;Color ice=Color.white;
                if(frost)
                {
                    // Frostbreak after 1.1 s: every mesh icy with the source's emissive.
                    NativeKillEffectRules.Linear(swarm.BodyColor,out double r,out double g,out double b);NativeKillEffectRules.Linear(swarm.BodyEmissive,out double er,out double eg,out double eb);
                    ice=new Color((float)r,(float)g,(float)b,1);var glow=new Color((float)er,(float)eg,(float)eb,1);
                    if(sourceModels.QueueRecolor(model,Matrix4x4.identity,corpse.PresentedBone,corpse.ShownBone,ice,glow,alpha))
                    {
                        recolored=true;if(hat)sourceModels.QueueRecolor(corpse.HatModel,Matrix4x4.identity,corpse.PresentedBone,null,ice,glow,alpha);
                        if(weapon)sourceModels.QueueRecolor(corpse.LooseModel,drop.Value,null,null,ice,glow,alpha);
                    }
                }
                if(!recolored)
                {
                    sourceModels.QueueShadowless(model,Matrix4x4.identity,corpse.PresentedBone,corpse.ShownBone,BodyTint(death.actor?.BodyColor),AccentTint(death.actor?.AccentColor),alpha);
                    if(hat)sourceModels.QueueShadowless(corpse.HatModel,Matrix4x4.identity,corpse.PresentedBone,null,null,null,alpha);if(weapon)sourceModels.QueueShadowless(corpse.LooseModel,drop.Value,null,null,null,null,alpha);
                }
                // team-identity.ts bands ride in the part copies (unlit team colour; Frostbreak turns them ice, lit here). They are
                // opaque, so a fading body drops them halfway.
                if(corpse.Band>=0&&alpha>.5f&&StatusReady())for(int k=0;k<bandLocal.Length;k++)
                {
                    int bone=NativeStatusVisuals.BandBones[k];if(!corpse.ShownBone(bone))continue;var m=corpse.PresentedBone(bone)*bandLocal[k];
                    if(recolored)sourceModels.KillCube(m,ice);else teamBands[corpse.Band].Add(m);
                }
            }
            KillWound(corpse,shown,alpha);KillSpray(corpse,shown,alpha);KillSwarm(corpse,shown);KillBurst(corpse,fx);
            if(style=="confetti")VoxelCloud(death.position,fx,6,death.seed,60);
            if(style=="siren-dissipate"||style=="siren-crush"){VoxelCloud(death.position,fx,10,death.seed,50,true);Ring(death.position-Vector3.up*.75f,Mathf.Clamp01(fx)*2,7,fx*60);}
            if(style=="dev-raging-demon")for(int j=0;j<12;j++){float t=j*.13f;if(fx>=t&&fx<t+.12f){Vector3 p=death.position+new Vector3(Mathf.Sin(j*13)*.45f,Mathf.Cos(j*9)*.7f,Mathf.Cos(j*11)*.45f);Beam(p-Vector3.up*.24f,p+Vector3.up*.24f,.08f,7);}}
        }
        private static bool BoneWithin(int bone,int root){while(bone>=0){if(bone==root)return true;bone=CapturedRig.Bones[bone].Parent;}return false;}
        private void Beam(Vector3 from,Vector3 to,float width,int color){Vector3 d=to-from;if(d.sqrMagnitude>.00001f)Add(color,Matrix4x4.TRS((from+to)*.5f,Quaternion.LookRotation(d),new Vector3(width,width,d.magnitude)));}
        private void VoxelCloud(Vector3 center,float age,int color,int seed,int count,bool rising=false)
        {for(int j=0;j<count;j++){float angle=(j+seed%101)*2.399963f,life=Mathf.Clamp01(1-age/5),r=(.5f+j%7*.12f)*age;Vector3 p=center+new Vector3(Mathf.Sin(angle)*r,(rising?age:Mathf.Max(-.65f,(1+j%4*.3f)*age-4*age*age)),Mathf.Cos(angle)*r);Add(color==6?6+j%5:color,Matrix4x4.TRS(p,Quaternion.Euler(j*13+age*80,j*27,age*100),Vector3.one*(.04f+j%3*.025f)*life));}}
    }
}
