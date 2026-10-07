using System;
using System.Collections.Generic;
using UnityEngine;
using NVector = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // Converts the game's committed retargeted captures into shared native Unity AnimationClips.
    // SampleAnimation owns the joints; snapshots own action selection, timestamps and root travel.
    public sealed class CapturedAnimation : IDisposable
    {
        private readonly Dictionary<string,AnimationClip> clips=new Dictionary<string,AnimationClip>();
        public int ClipCount => clips.Count;
        public CapturedAnimation()
        {
            foreach(var set in CapturedClips.Sets) foreach(var clip in set) clips.Add(clip.Name,Bake(clip,Array.IndexOf(CapturedClips.Sets,set)));
            Debug.Log("CAPTURED_ANIMATIONS_READY: "+clips.Count+" native clips from Chip Arena 05950ea; horizontal root motion disabled.");
        }
        public static Vector3 Vec(NVector v) => new Vector3(v.X,v.Y,v.Z);
        public static Quaternion Quat(NQuaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static NVector Num(Vector3 v) => new NVector(v.x,v.y,v.z);
        public static NQuaternion Num(Quaternion q) => new NQuaternion(q.x,q.y,q.z,q.w);
        public static CapturedClip Find(int set,string state)
        {
            foreach(var c in CapturedClips.Sets[set]) if(c.StateName==state) return c;
            return CapturedClips.Sets[set][0];
        }
        private static AnimationClip Bake(CapturedClip source,int set)
        {
            var clip=new AnimationClip { name=source.Name,legacy=true,frameRate=source.Fps,wrapMode=WrapMode.ClampForever };
            int count=CapturedRig.Bones.Length; var rows=new float[source.Frames,count*7];
            var pose=new CapturedRig.Pose(); var row=new float[CapturedClip.Channels];
            for(int frame=0;frame<source.Frames;frame++)
            {
                source.Sample(frame/(float)source.Fps,row); CapturedRig.Sample(row,set,pose);
                for(int b=0;b<count;b++)
                {
                    NVector p=CapturedRig.Mirror(pose.Position[b]); NQuaternion q=CapturedRig.Mirror(pose.Rotation[b]);
                    int i=b*7;
                    if(frame>0&&q.X*rows[frame-1,i+3]+q.Y*rows[frame-1,i+4]+q.Z*rows[frame-1,i+5]+q.W*rows[frame-1,i+6]<0) q=new NQuaternion(-q.X,-q.Y,-q.Z,-q.W);
                    rows[frame,i]=p.X; rows[frame,i+1]=p.Y; rows[frame,i+2]=p.Z;
                    rows[frame,i+3]=q.X; rows[frame,i+4]=q.Y; rows[frame,i+5]=q.Z; rows[frame,i+6]=q.W;
                }
            }
            string[] properties={"localPosition.x","localPosition.y","localPosition.z","localRotation.x","localRotation.y","localRotation.z","localRotation.w"};
            for(int b=0;b<count;b++) for(int c=0;c<7;c++)
            {
                var keys=new Keyframe[source.Frames]; int index=b*7+c;
                for(int f=0;f<keys.Length;f++)
                {
                    float value=rows[f,index],before=f>0?(value-rows[f-1,index])*source.Fps:0,after=f+1<keys.Length?(rows[f+1,index]-value)*source.Fps:0;
                    keys[f]=new Keyframe(f/(float)source.Fps,value,before,after);
                }
                clip.SetCurve(CapturedRig.Bones[b].Path,typeof(Transform),properties[c],new AnimationCurve(keys));
            }
            clip.EnsureQuaternionContinuity(); return clip;
        }
        public static void Release(UnityEngine.Object item) { if(Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item); }
        public void Dispose() { foreach(var clip in clips.Values) Release(clip); clips.Clear(); }
        public void ValidatePlayback()
        {
            var parent=new GameObject("Animation validation"); var rig=Create(parent.transform,-1);
            var expected=new CapturedRig.Pose(); var row=new float[64]; int count=0;
            try
            {
                for(int set=0;set<4;set++) foreach(var source in CapturedClips.Sets[set])
                {
                    foreach(float t in new[]{0f,(source.Frames/2)/(float)source.Fps,source.Duration})
                    {
                        rig.Sample(set,source.StateName,t); source.Sample(t,row); CapturedRig.Sample(row,set,expected);
                        for(int b=0;b<rig.Bones.Length;b++)
                        {
                            if(Vector3.Distance(rig.Bones[b].localPosition,Vec(CapturedRig.Mirror(expected.Position[b])))>.001f ||
                                Quaternion.Angle(rig.Bones[b].localRotation,Quat(CapturedRig.Mirror(expected.Rotation[b])))>.15f)
                                throw new InvalidOperationException("Native animation curve binding differs from captured rig: "+source.Name+" / "+CapturedRig.Bones[b].Name);
                        }
                        count++;
                    }
                }
                // Actual Unity clip sampling must not feed the previous rendered pose back into the base layer.
                foreach(var actor in new[]{new Actor{MagicVisual=1,AttackAt=1.9f},new Actor{Weapon=WeaponId.MachineGun,AbilityId="rocket",AbilityAt=1.7f,AbilityRelease=1.97f}})
                using(var repeat=Create(parent.transform,-2))
                {
                    var positions=new Vector3[repeat.Bones.Length];var rotations=new Quaternion[repeat.Bones.Length];
                    for(int frame=0;frame<12;frame++)
                    {
                        repeat.ActorPose(actor,Vector3.zero,2,true);
                        for(int b=0;b<repeat.Bones.Length;b++)
                        {
                            if(frame>0&&(Vector3.Distance(repeat.Bones[b].position,positions[b])>.00001f||Quaternion.Angle(repeat.Bones[b].rotation,rotations[b])>.05f))
                                throw new InvalidOperationException("Repeated first-person frame drifted: "+CapturedRig.Bones[b].Name);
                            positions[b]=repeat.Bones[b].position;rotations[b]=repeat.Bones[b].rotation;
                        }
                    }
                }
                Debug.Log("PASS native first-person repeat-frame playback: magic and rocket poses remain stable between fixed ticks.");
                Debug.Log("PASS native animation playback: "+count+" poses / "+ClipCount+" clips; position and quaternion bindings match the source conversion.");
            }
            finally { rig.Dispose(); Release(parent); }
        }
        public Player Create(Transform parent,int id) => new Player(this,parent,id);
        public sealed class Player : IDisposable
        {
            public readonly GameObject Root;
            public readonly Transform[] Bones=new Transform[CapturedRig.Bones.Length];
            private readonly CapturedAnimation library;
            private readonly Vector3[] blendP=new Vector3[CapturedRig.Bones.Length];
            private readonly NativePoseHistory baseHistory=new NativePoseHistory();
            private readonly Quaternion[] blendQ=new Quaternion[CapturedRig.Bones.Length];
            private readonly CapturedRig.Pose pose=new CapturedRig.Pose();
            private float lastTime=-100,phase,lastStun=-100,stunAt,gunAimWeight=1;
            private readonly NativeGuardMotion magicGuard=new NativeGuardMotion();
            public float MagicGuardWeight=>magicGuard.Weight;
            private bool fresh=true;private readonly float[] magicChannels=new float[13];
            public string State { get; private set; }="idle";
            public int Set { get; private set; }
            public WeaponId Weapon { get; private set; }
            public Player(CapturedAnimation library,Transform parent,int id)
            {
                this.library=library; Root=new GameObject("Captured player "+id); Root.transform.SetParent(parent,false);
                for(int i=0;i<Bones.Length;i++)
                {
                    var b=CapturedRig.Bones[i]; var t=new GameObject(b.Name).transform;
                    t.SetParent(b.Parent<0?Root.transform:Bones[b.Parent],false); t.localPosition=Vec(CapturedRig.Mirror(b.Position)); Bones[i]=t;
                }
            }
            private void Save(Vector3[] p,Quaternion[] q) { for(int i=0;i<Bones.Length;i++) { p[i]=Bones[i].localPosition; q[i]=Bones[i].localRotation; } }
            private void Blend(Vector3[] p,Quaternion[] q,float weight)
            {
                for(int i=0;i<Bones.Length;i++) { Bones[i].localPosition=Vector3.Lerp(p[i],Bones[i].localPosition,weight); Bones[i].localRotation=Quaternion.Slerp(q[i],Bones[i].localRotation,weight); }
            }
            public void Sample(int set,string state,float time)
            {
                var data=Find(set,state); Set=set; State=state;
                float t=data.Loop?CapturedClip.Wrap(time,data.Duration):Mathf.Clamp(time,0,data.Duration);
                library.clips[data.Name].SampleAnimation(Root,t);
            }
            private void ReadPose()
            {
                for(int i=0;i<Bones.Length;i++) { pose.Position[i]=CapturedRig.Mirror(Num(Bones[i].localPosition)); pose.Rotation[i]=CapturedRig.Mirror(Num(Bones[i].localRotation)); }
            }
            private void WritePose()
            {
                for(int i=0;i<Bones.Length;i++) { Bones[i].localPosition=Vec(CapturedRig.Mirror(pose.Position[i])); Bones[i].localRotation=Quat(CapturedRig.Mirror(pose.Rotation[i])); }
            }
            public void ActorPose(Actor a,Vector3 position,float time,bool firstPerson=false)
            {
                Weapon=a.Weapon; int set=Mathf.Clamp(a.VisualSet,0,3);
                float dt=Mathf.Clamp(time-lastTime,0,.1f); bool reset=fresh||time<lastTime||time-lastTime>.35f;
                if(reset) phase=0; lastTime=time;
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,Quaternion.Euler(0,a.Yaw,0));
                float speed=new Vector2(a.Velocity.x,a.Velocity.z).magnitude;
                bool attack=a.MagicVisual==0&&a.Attacking(time),roll=a.Rolling(time),stun=a.StunUntil>time,ground=a.Grounded;
                bool moving=speed>.08f&&!attack&&!roll&&!stun&&ground;
                if(moving) phase+=dt*Mathf.Min(3.7f,speed/Mathf.Min(2.1f,.62f+.22f*speed));
                Sample(set,"idle",time);
                if(moving)
                {
                    Save(blendP,blendQ); var walk=Find(set,"walk"); Sample(set,"walk",walk.Marks[0]+phase*walk.Duration); Blend(blendP,blendQ,Mathf.Clamp01(speed/2.8f));
                    Save(blendP,blendQ); var run=Find(set,"run"); Sample(set,"run",run.Marks[0]+phase*run.Duration); Blend(blendP,blendQ,CapturedClip.Ease((speed-3.2f)/2.8f));
                    State=speed>4?"run":"walk";
                }
                if(attack)
                {
                    string cut="cut"+(char)('A'+a.Combo%(set==1?4:set==0?3:2)); var c=Find(set,cut);
                    float age=time-a.AttackAt,recovery=a.VisualDuration-a.VisualWindup-a.VisualActive;
                    Save(blendP,blendQ);
                    Sample(set,cut,c.AttackTime(age,a.VisualWindup,a.VisualActive,recovery));
                    Blend(blendP,blendQ,CapturedClip.Ease(age/.04f));
                    if(age>a.VisualWindup+a.VisualActive)
                    {
                        Sample(set,cut,c.Marks[2]); Save(blendP,blendQ); Sample(set,"idle",time);
                        Blend(blendP,blendQ,CapturedClip.Ease((age-a.VisualWindup-a.VisualActive)/recovery)); State=cut+" recovery";
                    }
                }
                if(a.Weapon==WeaponId.Sword&&a.MagicVisual==0&&time<a.BlockUntil)
                {
                    Save(blendP,blendQ); float age=time-(a.BlockUntil-Rules.BlockDuration); var block=Find(set,"block");
                    Sample(set,"block",CapturedClip.Retime(new[]{0f,.13f,.36f},block.Marks,age)); Blend(blendP,blendQ,CapturedClip.Ease(age/.05f));
                }
                if(a.Weapon==WeaponId.Sword&&a.MagicVisual==0&&stun)
                {
                    if(a.StunUntil!=lastStun) { stunAt=time; lastStun=a.StunUntil; }
                    Save(blendP,blendQ); var hit=Find(set,"stagger"); float span=Mathf.Max(.05f,a.StunUntil-stunAt);
                    Sample(set,"stagger",CapturedClip.Retime(new[]{0f,span*.4f,span},hit.Marks,time-stunAt)); Blend(blendP,blendQ,.7f); State="hit reaction";
                }
                // Smooth snapshot transitions, but leave contact frames and the roll turn at their authoritative times.
                ReadPose();
                baseHistory.BlendBase(pose,time,attack||roll);
                if(GunRules.Ranged(a.Weapon))
                {
                    Vector3 velocity=Quaternion.Inverse(Root.transform.rotation)*new Vector3(a.Velocity.x,0,a.Velocity.z);
                    float want=!moving||velocity.z/Mathf.Max(.01f,speed)<.6f||a.Aiming||a.ReloadUntil>time||time<a.BlockUntil||time-a.LastShot<.4f?1:0;
                    gunAimWeight=reset?want:Mathf.Lerp(gunAimWeight,want,1-Mathf.Exp(-24*dt));
                    RangedPose.Apply(pose,a,time,firstPerson?1:gunAimWeight);
                    RangedAbilityPose.Apply(pose,a,time,firstPerson);
                    State=a.ReloadUntil>time?"reload":time<a.BlockUntil?"block":time-a.LastShot<.2f?"fire":a.Aiming?"aim":moving?"carry":"ready";
                }
                if(moving)
                {
                    // The source game also layers procedural travel-aware feet under captured upper-body carry.
                    Vector3 local=Quaternion.Inverse(Root.transform.rotation)*new Vector3(a.Velocity.x,0,a.Velocity.z);
                    NVector direction=CapturedRig.Unit(CapturedRig.Mirror(Num(local)),CapturedRig.Z);
                    pose.Position[CapturedRig.Body]=CapturedRig.V(0,-.04f,0);
                    for(int i=0;i<2;i++)
                    {
                        float u=CapturedClip.Wrap(phase+i*.5f,1),stance=.42f;
                        float stride=Mathf.Min(.42f,speed/Mathf.Max(.5f,speed/Mathf.Min(2.1f,.62f+.22f*speed))*stance*.5f);
                        float travel=u<stance?Mathf.Lerp(stride,-stride,u/stance):Mathf.Lerp(-stride,stride,CapturedClip.Ease((u-stance)/(1-stance)));
                        float lift=u<stance?0:Mathf.Sin((u-stance)/(1-stance)*Mathf.PI)*.12f;
                        NVector foot=CapturedRig.V(i==0?.13f:-.13f,-.795f+lift)+direction*travel;
                        CapturedRig.Leg(pose,i==0,foot);
                    }
                }
                if(a.MagicVisual>0)
                {MagicPose.Apply(pose,a,time,firstPerson,magicChannels,magicGuard.Sample(time,time<a.BlockUntil));State=time<a.BlockUntil?"staff guard":time-a.AbilityAt<1?"ability cast":time-a.AttackAt<.8f?"magic cast":"staff carry";}
                if(!ground&&!roll)
                {
                    pose.Rotation[CapturedRig.LHip]=CapturedRig.Euler(-.65f); pose.Rotation[CapturedRig.RHip]=CapturedRig.Euler(-.35f);
                    pose.Rotation[CapturedRig.LKnee]=CapturedRig.Euler(1.2f); pose.Rotation[CapturedRig.RKnee]=CapturedRig.Euler(.8f); State="jump (procedural)";
                }
                if(roll)
                {
                    Root.transform.rotation=Quaternion.LookRotation(ArenaPresentation.Vec(a.RollDirection));
                    CapturedRig.Roll(pose,(time-a.RollAt)/a.VisualRollDuration); State="roll (adapted)";
                }
                if(a.Weapon==WeaponId.Sword&&a.MagicVisual==0&&ground&&!roll) CapturedRig.BladeFloor(pose,false,1.28f);
                if(firstPerson)NativeFirstPersonPose.SettleTorso(pose);
                WritePose(); fresh=false;
            }
            public void Preview(int set,string state,float time,Vector3 position)
            {
                Weapon=WeaponId.Sword; Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,Quaternion.identity); Sample(set,state,time);
                if(set!=3)
                {
                    ReadPose(); CapturedRig.BladeFloor(pose,false,set==2?1.69f:set==1?.84f:1.28f);
                    if(set==1) CapturedRig.BladeFloor(pose,true,.84f); WritePose();
                }
            }
            public void PreviewGun(WeaponId weapon,int state,float time)
            {
                var a=new Actor { Id=-1,Weapon=weapon,Ammo=GunRules.Get(weapon).Magazine,Aiming=state==1,LastShot=state==2?time-CapturedClip.Wrap(time,GunRules.Get(weapon).Interval):-100 };
                if(state==3) { a.ReloadAt=time-CapturedClip.Wrap(time,GunRules.Get(weapon).Reload+.6f); a.ReloadUntil=a.ReloadAt+GunRules.Get(weapon).Reload; }
                if(state==4) a.BlockUntil=time+.15f;
                Weapon=weapon; Sample(0,"idle",time); ReadPose(); RangedPose.Apply(pose,a,time,1); WritePose();
                Root.transform.SetPositionAndRotation(Vector3.up*.865f,Quaternion.identity); State=new[]{"ready","aim","fire","reload","block"}[state];
            }
            public void Dispose() { if(Root!=null) Release(Root); }
        }
    }
}
