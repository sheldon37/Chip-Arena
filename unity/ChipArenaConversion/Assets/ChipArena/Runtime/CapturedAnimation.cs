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
                foreach(var actor in new[]{new Actor{MagicVisual=1,AttackAt=1.9f},new Actor{Weapon=WeaponId.MachineGun,AbilityId="rocket",AbilityAt=1.7f,AbilityRelease=1.97f},new Actor{SpecialKind="sword-launch",SpecialAt=1.9f,SpecialRelease=2.16f,SpecialUntil=2.66f},new Actor{SpecialKind="wolf-spin",SpecialAt=1.5f,SpecialRelease=1.78f,SpecialUntil=5,SpecialReleased=true}})
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
                Debug.Log("PASS native first-person repeat-frame playback: magic, rocket and melee-special poses remain stable between fixed ticks.");
                ValidateMelee(parent);ValidateFighters(parent);ValidateLeet(parent);ValidateGuns(parent);
                Debug.Log("PASS native animation playback: "+count+" poses / "+ClipCount+" clips; position and quaternion bindings match the source conversion.");
            }
            finally { rig.Dispose(); Release(parent); }
        }
        // Captured melee through the native boundary (mirroring, root yaw and travel): a sword cut with its authoritative step
        // keeps the planted lead foot still in the world from contact until it steps home, the ankles and blade tip above
        // the floor and every bone finite. The Core port itself is checked against the source in Tests/MeleeMotionChecks.cs.
        private void ValidateMelee(GameObject parent)
        {
            using(var rig=Create(parent.transform,-3))
            {
                const float at=2,windup=.14f,active=.16f,yaw=30,distance=.65f;
                var a=new Actor{Id=7,SourceSpec="sword",Weapon=WeaponId.Sword,Grounded=true,CastRate=1,Yaw=yaw,Combo=0,AttackAt=at,AttackUntil=at+.72f,VisualWindup=windup,VisualActive=active,VisualDuration=.72f,
                    SwingAt=at+windup,SwingUntil=at+windup+active,CommitAt=at,CommitUntil=at+.72f,CommitYaw=yaw,StepAt=at+windup*.55f,StepUntil=at+windup+active*.8f,StepDistance=distance};
                Vector3 forward=Quaternion.Euler(0,yaw,0)*Vector3.forward,planted=Vector3.zero;bool hasPlant=false;float drift=0,lowest=float.MaxValue,lowTip=float.MaxValue;
                for(int f=0;f<=96;f++)
                {
                    float t=1.4f+f/60f,travel=distance*MathUtil.Smooth((t-a.StepAt)/(a.StepUntil-a.StepAt));rig.ActorPose(a,forward*travel,t);
                    foreach(var b in rig.Bones)if(float.IsNaN(b.position.x)||float.IsInfinity(b.position.y)||b.position.magnitude>20)throw new InvalidOperationException("Captured melee produced a non-finite bone: "+b.name);
                    lowest=Mathf.Min(lowest,Mathf.Min(rig.Bones[CapturedRig.LFoot].position.y,rig.Bones[CapturedRig.RFoot].position.y));
                    lowTip=Mathf.Min(lowTip,rig.Bones[CapturedRig.RHand].TransformPoint(0,0,1.28f).y);
                    // The first cut leads with the left foot: world-fixed from its plant (65% of the contact) to its step home.
                    Vector3 lead=rig.Bones[CapturedRig.LFoot].position;
                    if(t>=at+windup+active*.7f&&t<=at+windup+active+.06f){if(!hasPlant){planted=lead;hasPlant=true;}drift=Mathf.Max(drift,Vector3.Distance(new Vector3(lead.x,0,lead.z),new Vector3(planted.x,0,planted.z)));}
                }
                if(drift>.015f)throw new InvalidOperationException("Captured melee lead foot skated "+(drift*100).ToString("0.0")+" cm while planted");
                if(lowest<.07f-.006f)throw new InvalidOperationException("Captured melee ankle dropped through the floor: "+lowest.ToString("0.000"));
                if(lowTip<.085f-.01f)throw new InvalidOperationException("Captured melee blade tip went through the floor: "+lowTip.ToString("0.000"));
                Debug.Log("PASS native captured melee: sword cut with step travel, planted lead foot drift "+(drift*1000).ToString("0.0")+" mm, ankles and blade above the floor");
            }
        }
        // The DEV fighting styles through the native boundary: travelling plays the style's own guard on the gait (never the
        // sword walk or run), the Emperor's Shift keeps its dodge roll, the Ansatsuken's plays its Demon Step, and a juggled
        // fighter is juggled, with every bone finite. Core/NativeFighterPose is checked in Tests/FighterMotionChecks.cs.
        private void ValidateFighters(GameObject parent)
        {
            using(var rig=Create(parent.transform,-4))
            {
                float t=3;
                foreach(var spec in new[]{"emperor","ansatsuken"})
                {
                    var a=new Actor{Id=spec=="emperor"?8:9,SourceSpec=spec,Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,Velocity=new V3(0,0,6.8f),RollDirection=new V3(0,0,1)};
                    void Pose(string want,int frames)
                    {
                        for(int f=0;f<frames;f++,t+=1/60f)
                        {
                            rig.ActorPose(a,new Vector3(0,0,t*a.Velocity.z),t);
                            foreach(var b in rig.Bones)if(float.IsNaN(b.position.x)||float.IsInfinity(b.position.y)||b.position.magnitude>1e4f)throw new InvalidOperationException(spec+" fighter pose produced a non-finite bone: "+b.name);
                        }
                        if(rig.State!=want)throw new InvalidOperationException(spec+" fighter shows "+rig.State+", expected "+want);
                    }
                    Pose(spec+"-idle",40);
                    a.RollAt=t;Pose(spec=="emperor"?"roll (adapted)":"ansatsuken-step",8);a.RollAt=-100;Pose(spec+"-idle",30);
                    a.JuggleUntil=t+1.25f;Pose("juggle",10);a.JuggleUntil=0;Pose(spec+"-idle",10);t+=1;
                    // The Emperor's wavedash (E), cancelled into the Electric Hook (Q) mid-wave: the wave clip with its foot edits on
                    // the dashing gait, then the hook from the frame the action changes (Tests/WavedashChecks.cs checks the poses).
                    if(spec=="emperor"){a.SourceTotal=1;a.SourceAction="wave";a.SourceAt=t;a.SourceUntil=t+.18f;Pose("emperor-wave-1-0",5);a.SourceAction="electric";a.SourceAt=t;a.SourceUntil=t+.4f;Pose("emperor-electric-1-0",12);a.SourceAction=null;Pose("emperor-idle",20);}
                }
            }
            Debug.Log("PASS native DEV fighters: Emperor and Ansatsuken travel in their own guard on the gait, show their Shift and the juggle; the Emperor's wavedash cancels into the Electric Hook");
        }
        // L33t H4x (a magic-class staff pilot whose replicated MagicVisual stays 0) through the native boundary: in third
        // person it runs, casts, guards, rolls, is stunned and is juggled on the magic gait; its first-person view model casts
        // and guards with the staff and keeps it under a hit stun (no sword cut, block or stagger); a corpse rig posed once at
        // the death (NativeDeathPresentation) stands on the magic gait. Never a sword-set state, every bone finite.
        // Core/MagicMotion is checked in Tests/LeetMotionChecks.cs, the other paths in Tests/LeetPathChecks.cs.
        private void ValidateLeet(GameObject parent)
        {
            using(var rig=Create(parent.transform,-5))
            using(var view=Create(parent.transform,-6))
            using(var corpse=Create(parent.transform,-7))
            {
                float t=3;var a=new Actor{Id=10,SourceSpec="leet",Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,Velocity=new V3(0,0,6.8f),RollDirection=new V3(0,0,1)};
                void Pose(Player r,bool first,string want,int frames)
                {
                    for(int f=0;f<frames;f++,t+=1/60f)
                    {
                        r.ActorPose(a,new Vector3(0,0,t*a.Velocity.z),t,first);
                        foreach(var b in r.Bones)if(float.IsNaN(b.position.x)||float.IsInfinity(b.position.y)||b.position.magnitude>1e4f)throw new InvalidOperationException("L33t H4x pose produced a non-finite bone: "+b.name);
                    }
                    if(r.State!=want)throw new InvalidOperationException("L33t H4x "+(first?"first-person ":"")+"shows "+r.State+", expected "+want);
                }
                Pose(rig,false,MagicMotion.Carry,40);
                a.AttackAt=t;a.AttackUntil=t+.6f;Pose(rig,false,MagicMotion.Cast,20);a.AttackUntil=-100;
                a.BlockAt=t;a.BlockUntil=t+.5f;Pose(rig,false,MagicMotion.Guard,10);a.BlockUntil=0;
                a.RollAt=t;Pose(rig,false,MagicMotion.Roll,8);a.RollAt=-100;
                a.JuggleUntil=t+1.25f;Pose(rig,false,MagicMotion.Juggle,10);a.JuggleUntil=0;
                t+=1;Pose(rig,false,MagicMotion.Carry,10);
                a.StunUntil=t+.3f;Pose(rig,false,MagicMotion.Carry,10);a.StunUntil=0;
                a.AttackAt=t;a.AttackUntil=t+.6f;Pose(view,true,"magic cast",20);a.AttackAt=a.AttackUntil=-100;
                a.BlockAt=t;a.BlockUntil=t+.5f;Pose(view,true,MagicMotion.Guard,10);a.BlockUntil=0;
                a.StunUntil=t+.3f;Pose(view,true,MagicMotion.Carry,10);a.StunUntil=0;
                var dead=a.ViewCopy();dead.Hp=1;corpse.ActorPose(dead,Vector3.zero,t);
                foreach(var b in corpse.Bones)if(float.IsNaN(b.position.x)||float.IsInfinity(b.position.y)||b.position.magnitude>1e4f)throw new InvalidOperationException("L33t H4x corpse pose produced a non-finite bone: "+b.name);
                if(corpse.State!=MagicMotion.Carry)throw new InvalidOperationException("L33t H4x corpse rig shows "+corpse.State+", expected "+MagicMotion.Carry);
            }
            Debug.Log("PASS native L33t H4x: runs, casts, guards, rolls, is stunned and is juggled on the magic gait with its staff, casts, guards and is stunned in first person with the staff, and its corpse rig stands on the magic gait");
        }
        // The guns through the native boundary (mirroring, root yaw and travel): running forward carries the lowered gun on the
        // gait with the feet striding (the two ankles cross over, one planted on the floor), aiming, firing and reloading
        // raise it, the roll and the juggle pose over it, the gun never dips into the floor and every bone stays finite. The
        // Core port (Core/GunMotion) is checked in Tests/GunMotionChecks.cs.
        private void ValidateGuns(GameObject parent)
        {
            using(var rig=Create(parent.transform,-8))
            {
                float t=3;
                foreach(var weapon in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
                {
                    var a=new Actor{Id=20+(int)weapon,SourceSpec=GunMotion.Spec(weapon),Weapon=weapon,Grounded=true,Hp=100,Velocity=new V3(0,0,6.8f),RollDirection=new V3(0,0,1),Ammo=GunRules.Get(weapon).Magazine};
                    float z=0,lowTip=float.MaxValue,lead=0,trail=0;
                    void Pose(string want,int frames)
                    {
                        for(int f=0;f<frames;f++,t+=1/60f)
                        {
                            z+=a.Velocity.z/60;rig.ActorPose(a,new Vector3(0,0,z),t);
                            foreach(var b in rig.Bones)if(float.IsNaN(b.position.x)||float.IsInfinity(b.position.y)||b.position.magnitude>1e4f)throw new InvalidOperationException(weapon+" gun pose produced a non-finite bone: "+b.name);
                            lowTip=Mathf.Min(lowTip,rig.Bones[CapturedRig.RHand].TransformPoint(0,0,weapon==WeaponId.Rifle?1.21f:weapon==WeaponId.MachineGun?.9f:.47f).y);
                            float stride=rig.Bones[CapturedRig.LFoot].position.z-rig.Bones[CapturedRig.RFoot].position.z;lead=Mathf.Max(lead,stride);trail=Mathf.Min(trail,stride);
                        }
                        if(rig.State!=want)throw new InvalidOperationException(weapon+" shows "+rig.State+", expected "+want);
                    }
                    Pose(GunMotion.Carry,90);
                    if(lead<.25f||trail>-.25f)throw new InvalidOperationException(weapon+" run does not stride: ankles "+lead.ToString("0.00")+" / "+trail.ToString("0.00")+" m apart");
                    a.Aiming=true;Pose(GunMotion.Aim,20);a.Aiming=false;
                    a.LastShot=t;Pose(GunMotion.Fire,6);
                    a.ReloadAt=t;a.ReloadUntil=t+GunRules.Get(weapon).Reload;Pose(GunMotion.Reload,20);a.ReloadUntil=0;
                    a.RollAt=t;Pose(GunMotion.Roll,8);a.RollAt=-100;
                    a.JuggleUntil=t+1.25f;Pose(GunMotion.Juggle,10);a.JuggleUntil=0;t+=1;Pose(GunMotion.Carry,40);
                    if(lowTip<.03f)throw new InvalidOperationException(weapon+" muzzle dipped into the floor: "+lowTip.ToString("0.000"));
                }
            }
            Debug.Log("PASS native guns: pistol, rifle and machine gun run on the gait with the gun carried, aim, fire, reload, roll and juggle over it, muzzles above the floor");
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
                if(!MathUtil.Finite(weight)) weight=1; // a NaN weight passes Vector3.Lerp's clamp and would write NaN positions
                for(int i=0;i<Bones.Length;i++) { Bones[i].localPosition=Vector3.Lerp(p[i],Bones[i].localPosition,weight); Bones[i].localRotation=Quaternion.Slerp(q[i],Bones[i].localRotation,weight); }
            }
            public void Sample(int set,string state,float time)
            {
                var data=Find(set,state); Set=set; State=state;
                float t=data.Loop?CapturedClip.Wrap(time,data.Duration):Mathf.Clamp(time,0,data.Duration); if(!MathUtil.Finite(t)) t=0; // SampleAnimation writes bones unchecked
                library.clips[data.Name].SampleAnimation(Root,t);
            }
            private void ReadPose()
            {
                for(int i=0;i<Bones.Length;i++) { pose.Position[i]=CapturedRig.Mirror(Num(Bones[i].localPosition)); pose.Rotation[i]=CapturedRig.Mirror(Num(Bones[i].localRotation)); }
            }
            private void WritePose()
            {
                NativePoseWriter.Write(Bones,pose,"a captured pilot rig");
            }
            // Captured melee (sword, daggers, shield, greatsword) runs the source's full melee pipeline (Core/MeleeMotion):
            // gait feet, captured guard and carries, cut footwork and hip seating, blade clearance, recoveries, blocks with
            // their impacts, staggers and hit reactions, the ground pass, pose transitions and the final floor guard.
            // The native roll, E special and knockdown recovery layers ride on it where the source runs its own.
            private MeleeMotion melee;private readonly MeleeView meleeView=new MeleeView();private float meleeTime=-100;
            public MeleeImpacts Impacts;
            // The arena's melee hit ledger (null for intro, portrait, corpse and viewer rigs) and this rig's hit layer (Core/NativeHitReact).
            public NativeHitLedger Hits;private readonly NativeHitLayer hitLayer=new NativeHitLayer();
            // The jump layer (Core/NativeJumpMotion) for the arena rigs (host, guests, killcam, spectators: where Hits is set),
            // one per rig across its routes; intro, portrait, corpse and viewer rigs keep their own motion.
            private NativeJumpMotion jump;private NativeJumpMotion JumpLayer=>Hits!=null?jump??=new NativeJumpMotion():null;
            // The pack's directional melee travel carry (Core/NativeLocomotionCarry), on the same arena rigs.
            private NativeLocomotionCarry locomotion;private NativeLocomotionCarry Locomotion=>Hits!=null?locomotion??=new NativeLocomotionCarry():null;
            // The per-weapon sprint carry (Core/NativeSprintMotion), on the same arena rigs.
            private NativeSprintMotion sprint;private NativeSprintMotion SprintLayer=>Hits!=null?sprint??=new NativeSprintMotion():null;
            // The pack's directional gun and magic travel carry (Core/NativeRangedLocomotion), on the same arena rigs.
            private NativeRangedLocomotion rangedLocomotion;private NativeRangedLocomotion RangedLocomotion=>Hits!=null?rangedLocomotion??=new NativeRangedLocomotion():null;
            // The held weapons' reach stretch, set by the presentation before posing (the melee layers measure the stretched blade).
            public float WeaponReach=1;
            // The first-person camera's pitch (degrees, the view pilot's Actor.Pitch; the view copy's own is zeroed): the plunging
            // stab's view blade points at the ground below it (NativePlungePose.ViewDown).
            public float ViewPitch;
            public static bool CapturedMelee(Actor a)=>NativePoseRoute.For(a,false)==PoseRoute.CapturedMelee;
            private void MeleePose(Actor a,Vector3 position,float time)
            {
                Weapon=a.Weapon;Set=Mathf.Clamp(a.VisualSet,0,3);if(melee==null)melee=new MeleeMotion();melee.Impacts=Impacts;melee.JumpLayer=JumpLayer;melee.Locomotion=Locomotion;melee.SprintLayer=SprintLayer;
                // The Whirlwind's wide, stepping stance under its spinning upper body (zero for anything else).
                MeleeKitMotion.Stance(a,time,melee.Gait);
                float dt=meleeTime<-90||time<meleeTime||time-meleeTime>.35f?1/60f:Mathf.Min(.1f,time-meleeTime);meleeTime=time;
                var v=MeleeViewOf(a,time,meleeView);v.Reach=WeaponReach;bool roll=a.Rolling(time),special=NativeSpecialPose.Active(a,time),down=NativeSpecialPose.Down(a,time),juggle=Juggled(a,time);v.Juggle=juggle;
                float yaw=-melee.PresentedYaw(v,time,v.Yaw)*Mathf.Rad2Deg;
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,roll?Quaternion.LookRotation(ArenaPresentation.Vec(a.RollDirection)):Quaternion.Euler(0,yaw,0));Root.transform.localScale=Vector3.one*a.RigScale;
                Vector3 at=Root.transform.position;melee.Rig.GroupPosition=new NVector(-at.x,at.y,at.z);melee.Rig.GroupYaw=-Root.transform.eulerAngles.y*Mathf.Deg2Rad;
                hitLayer.Bind(melee,a,Hits,HitVictim.Melee);
                melee.Sample(v,time,dt,r=>
                {
                    r.WritePose(pose);
                    if(roll)CapturedRig.Roll(pose,(time-a.RollAt)/a.VisualRollDuration);
                    if(special)NativeSpecialPose.Special(pose,a,time,false);
                    if(down)NativeSpecialPose.Recover(pose,a,time);
                    if(juggle)NativeJugglePose.Apply(pose,time,juggleTrack,a.Velocity.y);
                    r.ReadPose(pose);
                    // The Whirlwind re-lays the gait's own ankles under its spin: the ground pass keeps them as layered legs.
                    if(special&&MeleeKitMotion.RidesGait(a,time))r.LegsLayered=true;
                });
                melee.Rig.WritePose(pose);WritePose();fresh=false;lastTime=time;
                State=juggle?"juggle":roll?"roll (adapted)":special?a.SpecialKind:down?"recovery "+NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time):melee.State;
            }
            // A juggle (any spec, emperor-animation.ts animateJuggle) observed from the replicated JuggleUntil.
            private readonly NativeJuggleTrack juggleTrack=new NativeJuggleTrack();
            private bool Juggled(Actor a,float time)=>juggleTrack.Observe(a.JuggleUntil,time)&&NativeJugglePose.Active(a.JuggleUntil,time);
            // The DEV fighting styles (Core/NativeFighterPose) in sampleCharacter's order over the gait: a juggle, a knockdown,
            // the Emperor's dodge roll (Shift), else the style's own clip (guard while standing or travelling, block, action,
            // the Ansatsuken's Demon Step on Shift), with the hit stun keeping the guard under its reaction. Never the sword set.
            // Its own view (the wavedash's dash time and the style action key never reach the captured-melee view) and wave track.
            private readonly CapturedRig.Pose fighterClip=new CapturedRig.Pose();private readonly MeleeView fighterView=new MeleeView();private readonly NativeWaveDash waveDash=new NativeWaveDash();
            private void FighterPose(Actor a,Vector3 position,float time)
            {
                Weapon=a.Weapon;Set=0;if(melee==null)melee=new MeleeMotion();melee.Impacts=Impacts;melee.JumpLayer=JumpLayer;
                float dt=meleeTime<-90||time<meleeTime||time-meleeTime>.35f?1/60f:Mathf.Min(.1f,time-meleeTime);meleeTime=time;
                bool emperor=a.SourceSpec=="emperor",cyber=CyberLordMotion.Handles(a.SourceSpec),juggle=Juggled(a,time),down=!juggle&&NativeSpecialPose.Down(a,time),stun=!juggle&&!down&&a.StunUntil>time;
                bool dodge=(emperor||cyber)&&a.Rolling(time)&&!juggle&&!down&&!stun;
                var v=MeleeViewOf(a,time,fighterView);v.Reach=1;v.Juggle=juggle;v.Down=down;v.HitStunUntil=stun?a.StunUntil:0;NativeFighterPose.View(v,a,waveDash,time);
                // Only the Emperor's Shift is a dodge roll (souls-combat.ts usesRoll); the Demon Step is the Ansatsuken's own layer.
                if(!dodge){v.RollAt=v.RollUntil=-100;}
                float yaw=-melee.PresentedYaw(v,time,v.Yaw)*Mathf.Rad2Deg;
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,dodge?Quaternion.LookRotation(ArenaPresentation.Vec(a.RollDirection)):Quaternion.Euler(0,yaw,0));Root.transform.localScale=Vector3.one*a.RigScale;
                Vector3 at=Root.transform.position;melee.Rig.GroupPosition=new NVector(-at.x,at.y,at.z);melee.Rig.GroupYaw=-Root.transform.eulerAngles.y*Mathf.Deg2Rad;
                hitLayer.Bind(melee,a,Hits,HitVictim.Fighter);
                melee.FighterBase(v,time,dt);var r=melee.Rig;
                if(juggle){r.WritePose(pose);NativeJugglePose.Apply(pose,time,juggleTrack,a.Velocity.y);r.ReadPose(pose);State="juggle";}
                else if(down){r.WritePose(pose);NativeSpecialPose.Recover(pose,a,time);r.ReadPose(pose);State="recovery";}
                else if(dodge){r.WritePose(pose);CapturedRig.Roll(pose,(time-a.RollAt)/a.VisualRollDuration);r.ReadPose(pose);State="roll (adapted)";}
                // The DEV Cyber Lord's unarmed style (Core/CyberLordMotion: the pack's stance, combo and abilities on its replicated windows).
                else if(cyber){cyberMotion??=new CyberLordMotion();cyberMotion.Reduced=ReducedEffects;State=cyberMotion.Style(melee,a,time,stun);}
                else if(NativeSourceMotion.Fighter(fighterClip,a,time,stun,out string key,true)){NativeFighterPose.Style(melee,fighterClip,a,stun,time);State=stun?"hit reaction":key;}
                melee.FighterFinish(v,time,dt);
                r.WritePose(pose);WritePose();fresh=false;lastTime=time;
            }
            private CyberLordMotion cyberMotion;
            /// <summary>The DEV Cyber Lord's motion on this rig (its idle stance's weight and glitch), null for any other pilot so far;
            /// and whether effects are reduced (the presentation sets it before posing: the idle glitch's softer version).</summary>
            public CyberLordMotion CyberMotion=>cyberMotion;public bool ReducedEffects;
            /// <summary>Re-poses this rig's last pose through `layer` (a presentation overlay: the Kernel Panic victim's tumble,
            /// Core/CyberLordMotion.VictimPose); the bones are left as they were when it declines.</summary>
            public bool Overlay(Func<CapturedRig.Pose,bool> layer){ReadPose();if(layer==null||!layer(pose))return false;WritePose();return true;}
            // The native actor in the source frame (+x the rig's left, radians), as the source's PlayerView.
            private static MeleeView MeleeViewOf(Actor a,float time,MeleeView v)
            {
                const float D=Mathf.Deg2Rad;
                if(!(int.TryParse(v.Id,out int seen)&&seen==a.Id))v.Id=a.Id.ToString();v.Spec=a.SourceSpec;v.Yaw=-a.Yaw*D;v.Pitch=a.Pitch*D;v.Vel=new NVector(-a.Velocity.x,a.Velocity.y,a.Velocity.z);v.Grounded=a.Grounded;v.AirJumpAt=a.AirJumps>0?a.AirJumpAt:-100;v.Hp=a.Hp;v.Sprinting=a.Sprinting;
                v.LastAttack=a.AttackAt;v.Windup=a.VisualWindup;v.AttackRate=a.CastRate>0?a.CastRate:1;v.AttackIndex=a.Combo+1;
                v.SwingAt=a.SwingAt>-90?a.SwingAt:float.NaN;v.SwingUntil=a.SwingAt>-90?a.SwingUntil:float.NaN;
                v.Commit=a.CommitAt>-90;v.CommitAt=a.CommitAt;v.CommitUntil=a.CommitUntil;v.CommitYaw=-a.CommitYaw*D;v.StepAt=a.StepAt;v.StepUntil=a.StepUntil;v.StepDistance=a.StepDistance;v.Followup=a.Followup;v.Hurricane=a.Hurricane;
                v.AirAttack=a.AirAttack?1:0;v.OpenerLunge=a.OpenerLunge;v.SprintAttack=a.SprintAttack;v.Plunge=NativePlungeFields.Of(a);
                v.Guarding=false;v.BlockTime=time<a.BlockUntil?a.BlockUntil-time:0;v.LastBlock=a.BlockAt;v.HitStunUntil=a.StunUntil;
                v.RollAt=a.RollAt;v.RollUntil=a.RollAt>-90?a.RollAt+a.VisualRollDuration:-100;
                v.ActionKind=a.SpecialKind;v.ActionAt=a.SpecialAt;v.ActionUntil=a.SpecialUntil;
                v.Reaction=a.ReactionAt>-90;v.Parry=a.ReactionParry;v.ReactionAt=a.ReactionAt;v.ReactionUntil=a.ReactionUntil;v.ReactionYaw=a.ReactionYaw;v.ReactionTurn=a.ReactionTurn;v.ReactionStrength=a.ReactionStrength;
                v.ReactionDir=new NVector(a.ReactionDir.x,a.ReactionDir.y,a.ReactionDir.z);v.ReactionPoint=new NVector(a.ReactionPoint.x,a.ReactionPoint.y,a.ReactionPoint.z);v.ReactionZone=a.ReactionZone;
                v.Down=NativeSpecialPose.Down(a,time);v.DownPhase=v.Down?NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time):null;v.WeaponAway=a.WeaponAway;
                return v;
            }
            public void ActorPose(Actor a,Vector3 position,float time,bool firstPerson=false)
            {
                switch(NativePoseRoute.For(a,firstPerson))
                {
                    case PoseRoute.CapturedMelee:MeleePose(a,position,time);return;
                    case PoseRoute.Magic:MagicGaitPose(a,position,time);return;
                    case PoseRoute.Fighter:FighterPose(a,position,time);return;
                    case PoseRoute.Gun:GunGaitPose(a,position,time);return;
                    case PoseRoute.Bow:BowGaitPose(a,position,time);return;
                }
                // The DEV Cyber Lord's first-person fists (Core/CyberLordMotion.FirstPerson): never the sword set.
                if(firstPerson&&CyberLordMotion.Handles(a.SourceSpec)){Weapon=a.Weapon;cyberMotion??=new CyberLordMotion();State=cyberMotion.FirstPerson(pose,a,time);WritePose();fresh=false;lastTime=time;return;}
                // Generic: the Siren and the first-person view models (guns, magic and the rest). A magic-class pilot (the four
                // schools and the L33t H4x staff, MagicPose.School) never plays the sword set's cuts, block or stagger here.
                Weapon=a.Weapon; int set=Mathf.Clamp(a.VisualSet,0,3);bool magic=MagicPose.School(a)>0,sword=NativePoseRoute.SwordLayers(a),bow=NativeBowWire.Is(a);
                float dt=Mathf.Clamp(time-lastTime,0,.1f); bool reset=fresh||time<lastTime||time-lastTime>.35f;
                if(reset) phase=0; lastTime=time;
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,Quaternion.Euler(0,a.Yaw,0));Root.transform.localScale=Vector3.one*a.RigScale;
                float speed=new Vector2(a.Velocity.x,a.Velocity.z).magnitude;
                bool attack=!magic&&!bow&&a.Attacking(time),roll=a.Rolling(time),stun=a.StunUntil>time,ground=a.Grounded;
                bool special=NativeSpecialPose.Active(a,time),down=NativeSpecialPose.Down(a,time),juggle=!firstPerson&&Juggled(a,time);
                bool moving=speed>.08f&&!attack&&!roll&&!stun&&!special&&!down&&!juggle&&ground;
                if(moving) phase+=dt*Mathf.Min(3.7f,speed/Mathf.Min(2.1f,.62f+.22f*speed));
                Sample(set,"idle",time);
                if(moving)
                {
                    // A first-person melee view keeps only part of the captured pump (NativeLocoRules.FirstPersonCarry): at the gait's
                    // cadence it wiggled the view weapon; the view layer's stride bob carries the steps.
                    float keep=firstPerson&&MeleeMotion.Handles(a.SourceSpec)?NativeLocoRules.FirstPersonCarry:1;
                    Save(blendP,blendQ); var walk=Find(set,"walk"); Sample(set,"walk",walk.Marks[0]+phase*walk.Duration); Blend(blendP,blendQ,Mathf.Clamp01(speed/2.8f)*keep);
                    Save(blendP,blendQ); var run=Find(set,"run"); Sample(set,"run",run.Marks[0]+phase*run.Duration); Blend(blendP,blendQ,CapturedClip.Ease((speed-3.2f)/2.8f)*keep);
                    State=speed>4?"run":"walk";
                }
                // A swing begun in the air plays the weapon's plunging stab (Core/NativePlungePose) or jump attack (Core/NativeAirAttack)
                // instead of the cut below.
                bool plunge=!magic&&!bow&&!GunRules.Ranged(a.Weapon)&&PlungeView(a,position,time,firstPerson,attack);
                bool air=plunge||!magic&&!bow&&!GunRules.Ranged(a.Weapon)&&AirView(a,position,time,set,firstPerson,attack);
                // A grounded swing entered from a sprint plays the weapon's sprint attack (Core/NativeSprintAttackVisuals) instead.
                bool sprinting=!air&&!magic&&!bow&&!GunRules.Ranged(a.Weapon)&&SprintView(a,time,set,attack);
                if(attack&&!air&&!sprinting)
                {
                    string cut="cut"+(char)('A'+a.Combo%(set==1?4:set==0?3:2)); var c=Find(set,cut);
                    float age=time-a.AttackAt,recovery=Mathf.Max(.001f,a.VisualDuration-a.VisualWindup-a.VisualActive);
                    Save(blendP,blendQ);
                    // The third-person rigs' swing timing (Core/MeleeSwing): the authored sweep across the hit window, then the
                    // follow-through carrying on past it while the view model settles into the guard over the real recovery.
                    Sample(set,cut,MeleeSwing.ClipTime(c,age,a.VisualWindup,a.VisualActive,recovery));
                    Blend(blendP,blendQ,CapturedClip.Ease(age/.04f));
                    if(age>a.VisualWindup+a.VisualActive)
                    {
                        Save(blendP,blendQ); Sample(set,"idle",time);
                        Blend(blendP,blendQ,MeleeSwing.Settle(age,a.VisualWindup,a.VisualActive,recovery)); State=cut+" recovery";
                    }
                }
                if(sword&&time<a.BlockUntil)
                {
                    Save(blendP,blendQ); float age=time-(a.BlockAt>-90?a.BlockAt:a.BlockUntil-Rules.BlockDuration); var block=Find(set,"block");
                    Sample(set,"block",CapturedClip.Retime(new[]{0f,.13f,.36f},block.Marks,age)); Blend(blendP,blendQ,CapturedClip.Ease(age/.05f));
                }
                if(sword&&stun)
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
                // The first-person bow (Core/NativeBowPose.View): the arms on the arrow line straight down the view.
                if(bow){BowFrame=NativeBowPose.View(pose,a,time);BowView=NativeBow.At(NativeBowWire.Of(a),time);BowAt=time;State=NativeBowMotion.StateOf(BowView);}
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
                if(magic)
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
                if(!firstPerson&&!roll&&!special&&!down&&a.ReactionAt>-90){if(melee==null)melee=new MeleeMotion();melee.ReactOver(pose,MeleeViewOf(a,time,meleeView),time);}
                NativeSourceMotion.Pilot(pose,a,time);
                // The Siren's baked clip stands still: her drift lean, trailing limbs, air tuck, aim pitch, flinch and guard ride on it.
                if(!firstPerson&&a.SourceSpec=="siren"&&a.Hp>0&&!down&&!juggle){if(sirenDrift==null)sirenDrift=new NativeSirenDrift();sirenDrift.Apply(pose,a,time);}
                if(special){NativeSpecialPose.Special(pose,a,time,firstPerson);State=a.SpecialKind;}
                if(down){NativeSpecialPose.Recover(pose,a,time);State="recovery "+NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time);}
                if(juggle){NativeJugglePose.Apply(pose,time,juggleTrack,a.Velocity.y);State="juggle";}
                if(sword&&ground&&!roll&&!special&&!down&&!juggle&&!plunge&&!NativeFighterPose.Handles(a.SourceSpec)) CapturedRig.BladeFloor(pose,false,1.28f);
                if(firstPerson)NativeFirstPersonPose.SettleTorso(pose);
                WritePose(); fresh=false;
            }
            // ---- the first-person jump attack (Core/NativeAirAttack; the third-person rigs pose it in MeleeMotion) ----
            // The view rig keeps its own jump track and air-attack watch over the presented root (the rule the rig and the impact
            // effects use). A swing begun in the air poses the weapon's air take on a scratch rig, its chest bowed so the raised
            // weapon hangs in view and chops down through it, blended in over .04 s and settling into the guard after the window.
            private readonly NativeJumpTrack airTrack=new NativeJumpTrack();private readonly NativeAirAttackWatch airWatch=new NativeAirAttackWatch();
            private MeleeRig airRig;private readonly float[] airRow=new float[CapturedClip.Channels];private readonly CapturedRig.Pose airPose=new CapturedRig.Pose();
            private bool AirView(Actor a,Vector3 position,float time,int set,bool firstPerson,bool attack)
            {
                bool blocked=a.Hp<=0||a.Rolling(time)||a.StunUntil>time||a.JuggleUntil>time||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time);
                airTrack.Observe(time,a.Grounded,position.y,blocked);
                airWatch.Observe(airTrack,a.AttackAt>-90?a.AttackAt:-100,a.CommitUntil>-90?a.CommitUntil:a.AttackAt+a.VisualDuration,NativePlungeRules.ForwardFlag(a.AirAttack?1:0,a.SprintAttack,NativePlungeFields.Of(a).Down),time,blocked);
                var clip=NativeAirAttackRules.Clip(a.SourceSpec);var melee=MeleeSet.For(a.SourceSpec);
                if(!attack||clip==null||melee==null||!airWatch.Air||airWatch.SwingAt!=a.AttackAt)return false;
                float windup=a.VisualWindup,active=a.VisualActive,recovery=Mathf.Max(.001f,a.VisualDuration-windup-active),landed=airWatch.Landed(time),age=time-a.AttackAt;
                age-=NativeAirAttackRules.Frozen(age,float.IsNaN(landed)?float.NaN:airWatch.ImpactAt-a.AttackAt,windup,active,airWatch.Strength);
                clip.Sample(NativeAirAttackRules.ClipTime(clip,age,windup,active,recovery),airRow);
                airRig??=new MeleeRig();airRig.Reset();airRig.WeaponVisible=!a.WeaponAway;airRig.WeaponReach=WeaponReach;
                MeleeCapture.Apply(airRig,melee,airRow,CaptureOptions.Of(1,melee.Fit,MeleeClear.Turn,legs:0));airRig.WritePose(airPose);
                if(firstPerson)NativeAirAttackRules.ViewPose(airPose,NativeAirAttackRules.ViewPitch(age,windup,active,recovery));
                Save(blendP,blendQ);NativePoseWriter.Write(Bones,airPose,"a jump attack view model");Blend(blendP,blendQ,CapturedClip.Ease((time-a.AttackAt)/.04f));
                if(age>windup+active){Save(blendP,blendQ);Sample(set,"idle",time);Blend(blendP,blendQ,MeleeSwing.Settle(age,windup,active,recovery));}
                State="air attack"+(float.IsNaN(landed)?"":" landed");return true;
            }
            // ---- the first-person plunging stab (Core/NativePlungePose.ViewPose; the third-person rigs pose it in MeleeMotion) ----
            // The view rig keeps its own plunge watch over the replicated data (NativePlungeFields.Of, the one field mapping) and poses
            // the view model on a scratch rig: the grips low in view, the blades pointing at the ground below the camera's pitch (the
            // pilot looks down the blade as it falls), jolted down at the plant, drawn back up over the pull-out and settled into the
            // guard; blended in over .04 s. The speed lines and vignette are ArenaPresentation's (NativeSprintFxPresentation).
            private readonly NativePlungeWatch plungeWatch=new NativePlungeWatch();private MeleeRig plungeRig;private readonly float[] plungeRow=new float[CapturedClip.Channels];private readonly CapturedRig.Pose plungePose=new CapturedRig.Pose();
            private bool PlungeView(Actor a,Vector3 position,float time,bool firstPerson,bool attack)
            {
                bool blocked=a.Hp<=0||a.Rolling(time)||a.StunUntil>time||a.JuggleUntil>time||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time);
                plungeWatch.Observe(NativePlungeFields.Of(a),a.SourceSpec,position.y,a.Grounded,time,blocked);
                var melee=MeleeSet.For(a.SourceSpec);
                if(!attack||melee==null||NativePlungePose.For(a.SourceSpec)==null||!plungeWatch.Is(a.AttackAt)||!plungeWatch.Clock(time,out var c))return false;
                melee.Idle.Sample(a.AttackAt,plungeRow);plungeRig??=new MeleeRig();plungeRig.Reset();plungeRig.WeaponVisible=!a.WeaponAway;plungeRig.WeaponReach=WeaponReach;
                float guard=NativePlungePose.ViewPose(plungeRig,melee,a.SourceSpec,c,plungeRow,firstPerson?ViewPitch:a.Pitch);plungeRig.WritePose(plungePose);
                Save(blendP,blendQ);NativePoseWriter.Write(Bones,plungePose,"a plunging stab view model");Blend(blendP,blendQ,CapturedClip.Ease((time-a.AttackAt)/.04f)*(1-guard));
                State=NativePlungeRules.Label(c.Phase);return true;
            }
            // ---- the first-person sprint attack (Core/NativeSprintAttackVisuals; the third-person rigs pose it in MeleeMotion) ----
            // A swing the authority flags as entered from a sprint (it flags only grounded openers, so the greatsword's hop keeps
            // it) poses the weapon's running take on a scratch rig on the swing's own timing (NativeSprintAttackRules.ClipTime: the
            // daggers' twin cuts out and back), the shield driven out at the hit, blended in over .04 s and settling into the guard
            // after the window; the view layer keeps its own carry and bob.
            private MeleeRig sprintRig;private readonly float[] sprintRow=new float[CapturedClip.Channels];private readonly CapturedRig.Pose sprintPose=new CapturedRig.Pose();
            private bool SprintView(Actor a,float time,int set,bool attack)
            {
                var clip=NativeSprintAttackRules.Clip(a.SourceSpec);var melee=MeleeSet.For(a.SourceSpec);
                if(!attack||clip==null||melee==null||!NativeSprintAttackRules.Plays(a.SourceSpec,a.SprintAttack,false))return false;
                float windup=a.VisualWindup,active=a.VisualActive,recovery=Mathf.Max(.001f,a.VisualDuration-windup-active),age=time-a.AttackAt;
                clip.Sample(NativeSprintAttackRules.ClipTime(clip,a.SourceSpec,age,windup,active,recovery),sprintRow);
                if(a.SourceSpec=="shield"){float k=NativeSprintAttackRules.Bash(age,windup,active,recovery);sprintRow[MeleeCapture.Grip+2]+=NativeSprintAttackRules.BashReach*k;sprintRow[MeleeCapture.Grip+1]+=NativeSprintAttackRules.BashLift*k;}
                sprintRig??=new MeleeRig();sprintRig.Reset();sprintRig.WeaponVisible=!a.WeaponAway;sprintRig.WeaponReach=WeaponReach;
                MeleeCapture.Apply(sprintRig,melee,sprintRow,CaptureOptions.Of(1,melee.Fit,MeleeClear.Turn,legs:0));sprintRig.WritePose(sprintPose);
                Save(blendP,blendQ);NativePoseWriter.Write(Bones,sprintPose,"a sprint attack view model");Blend(blendP,blendQ,CapturedClip.Ease(age/.04f));
                if(age>windup+active){Save(blendP,blendQ);Sample(set,"idle",time);Blend(blendP,blendQ,MeleeSwing.Settle(age,windup,active,recovery));}
                State="sprint attack"+(age>windup+active?" recovery":"");return true;
            }
            // Third-person magic pilots (host, guests, killcam), the four schools and L33t H4x, walk on the source's gait
            // (Core/MagicMotion): the magic layer rides the gait's body bob, torso counter-twist and arm swing instead of
            // resetting the torso over the captured sword walk, which froze it. A juggle (observed from the replicated
            // JuggleUntil) owns the frame; otherwise the native roll, reaction, special and recovery layers pose before the
            // ground pass.
            private MagicMotion magicMotion;private NativeSirenDrift sirenDrift;
            private void MagicGaitPose(Actor a,Vector3 position,float time)
            {
                Weapon=a.Weapon;Set=Mathf.Clamp(a.VisualSet,0,3);if(magicMotion==null)magicMotion=new MagicMotion();magicMotion.JumpLayer=JumpLayer;magicMotion.SprintLayer=SprintLayer;magicMotion.Locomotion=RangedLocomotion;
                bool juggle=Juggled(a,time),roll=MagicMotion.Rolls(a,time,juggle);
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,roll?Quaternion.LookRotation(ArenaPresentation.Vec(a.RollDirection)):Quaternion.Euler(0,a.Yaw,0));Root.transform.localScale=Vector3.one*a.RigScale;
                Vector3 at=Root.transform.position;magicMotion.Rig.GroupPosition=new NVector(-at.x,at.y,at.z);magicMotion.Rig.GroupYaw=-Root.transform.eulerAngles.y*Mathf.Deg2Rad;
                string state=magicMotion.Pose(a,time,pose,magicChannels,magicGuard.Sample(time,time<a.BlockUntil),juggle,juggleTrack,out bool react);
                if(react){if(melee==null)melee=new MeleeMotion();melee.ReactOver(pose,MeleeViewOf(a,time,meleeView),time);}
                if(Hits!=null)hitLayer.Over((melee??=new MeleeMotion()).Rig,pose,a,Hits,HitVictim.Magic,time,magicMotion.Rig.GroupYaw,magicMotion.Rig.GroupPosition);
                State=ReferenceEquals(state,MagicMotion.Recovery)?"recovery "+NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time):state;
                magicMotion.Finish(pose,time);if(Hits!=null)hitLayer.Replant(melee.Rig,pose);
                WritePose();fresh=false;lastTime=time;
            }
            // Third-person gun pilots (host, guests, bots and the killcam) run on the source's gait in the gun's stance
            // (Core/GunMotion) with the native gun hold over it, instead of the captured sword walk/run under a reset torso and
            // a stiff procedural leg cycle. A juggle (observed from the replicated JuggleUntil) owns the frame; otherwise the
            // native roll, reaction, special and recovery layers pose before the ground pass.
            // ---- the bow (Core/NativeBowMotion: the gait, the pack 2Hand-Bow carry and the bow hold), third person ----
            /// <summary>The posed bow of the last frame (rig space, source frame) and its view, at BowAt (the bow models read them).</summary>
            public NativeBowPose.Frame BowFrame;public NativeBow.View BowView;public float BowAt=-100;private NativeBowMotion bowMotion;
            private void BowGaitPose(Actor a,Vector3 position,float time)
            {
                Weapon=a.Weapon;Set=0;if(bowMotion==null)bowMotion=new NativeBowMotion();bowMotion.JumpLayer=JumpLayer;bowMotion.SprintLayer=SprintLayer;bowMotion.Locomotion=RangedLocomotion;
                bool juggle=Juggled(a,time),roll=!juggle&&a.Rolling(time);
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,roll?Quaternion.LookRotation(ArenaPresentation.Vec(a.RollDirection)):Quaternion.Euler(0,a.Yaw,0));Root.transform.localScale=Vector3.one*a.RigScale;
                Vector3 at=Root.transform.position;bowMotion.Rig.GroupPosition=new NVector(-at.x,at.y,at.z);bowMotion.Rig.GroupYaw=-Root.transform.eulerAngles.y*Mathf.Deg2Rad;
                State=bowMotion.Pose(a,time,pose,juggle,juggleTrack,out bool react);
                if(react){if(melee==null)melee=new MeleeMotion();melee.ReactOver(pose,MeleeViewOf(a,time,meleeView),time);}
                if(Hits!=null)hitLayer.Over((melee??=new MeleeMotion()).Rig,pose,a,Hits,HitVictim.Gun,time,bowMotion.Rig.GroupYaw,bowMotion.Rig.GroupPosition);
                bowMotion.Finish(pose,time);if(Hits!=null)hitLayer.Replant(melee.Rig,pose);
                BowFrame=bowMotion.Shot;BowView=bowMotion.Bow;BowAt=time;
                WritePose();fresh=false;lastTime=time;
            }
            private GunMotion gunMotion;
            private void GunGaitPose(Actor a,Vector3 position,float time)
            {
                Weapon=a.Weapon;Set=0;if(gunMotion==null)gunMotion=new GunMotion();gunMotion.JumpLayer=JumpLayer;gunMotion.SprintLayer=SprintLayer;gunMotion.Locomotion=RangedLocomotion;
                bool juggle=Juggled(a,time),roll=!juggle&&a.Rolling(time);
                Root.transform.SetPositionAndRotation(position+Vector3.up*.865f,roll?Quaternion.LookRotation(ArenaPresentation.Vec(a.RollDirection)):Quaternion.Euler(0,a.Yaw,0));Root.transform.localScale=Vector3.one*a.RigScale;
                Vector3 at=Root.transform.position;gunMotion.Rig.GroupPosition=new NVector(-at.x,at.y,at.z);gunMotion.Rig.GroupYaw=-Root.transform.eulerAngles.y*Mathf.Deg2Rad;
                State=gunMotion.Pose(a,time,pose,juggle,juggleTrack,out bool react);
                if(react){if(melee==null)melee=new MeleeMotion();melee.ReactOver(pose,MeleeViewOf(a,time,meleeView),time);}
                if(Hits!=null)hitLayer.Over((melee??=new MeleeMotion()).Rig,pose,a,Hits,HitVictim.Gun,time,gunMotion.Rig.GroupYaw,gunMotion.Rig.GroupPosition);
                gunMotion.Finish(pose,time);if(Hits!=null)hitLayer.Replant(melee.Rig,pose);
                WritePose();fresh=false;lastTime=time;
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
