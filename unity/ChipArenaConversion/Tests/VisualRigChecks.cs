#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The visual rig adapter (Core/NativeVisualRig.cs, Runtime/NativeVisualRigBinding.cs; docs/BOZO-INTEGRATION.md step 3).
    // Driven by every captured clip on a synthetic Unreal-mannequin-named skeleton built here (A-pose arms 50 degrees down,
    // bone frames turned off identity, long legs): no pack data. Checked: the bind straightens the limbs to the game's rest
    // and keeps the spine, head and feet as authored; limbs follow the game's bone directions exactly, except that a hand
    // inside the game's shoulder line reaches its chest-relative place (sideways and, below the head, in height, keeping the
    // copied arm's extension) by swinging the whole arm at the shoulder, so the elbow keeps the game's bend; split chains end on
    // the game joint's turn, hands carry the game wrist turn; the root height scales by the leg ratio; planted feet reach the
    // game ankle's ground point and bend toward the game knee; the support hand keeps the game's wrist offset; twist bones
    // take their share of the roll; the grip is the game socket on the visual wrist (P1); closed fists seat on the Standard
    // weapons' handles with the straightest wrist the handle allows; the floor fit; the locals rebuild
    // the pose; deterministic; nothing allocated per frame; and the binding names no pack types. The real pack skeleton's
    // drift is measured in Unity on the owner's PC (the local inventory tool), not here.
    internal static class VisualRigChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Visual rig: "+what);count++;}
        private static bool Near(Vector3 a,Vector3 b,float e)=>Vector3.Distance(a,b)<=e;
        private static bool Near(Quaternion a,Quaternion b,float e)=>Math.Abs(Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))-1)<=e;
        private static float Angle(Vector3 a,Vector3 b)=>(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1f,1f));

        // ---- the synthetic skeleton: world rest positions (Pose body space, floor at -.865) and bone frames off identity ----
        private static readonly string[] Names={"root","pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02","head",
            "clavicle_l","upperarm_l","shoulder_twist_l","upperarm_twist_01_l","lowerarm_l","lowerarm_twist_01_l","hand_l","index_01_l","index_02_l","index_03_l","thumb_01_l","thumb_02_l","thumb_03_l",
            "clavicle_r","upperarm_r","upperarm_twist_01_r","lowerarm_r","lowerarm_twist_01_r","hand_r","index_01_r","index_02_r","index_03_r","thumb_01_r","thumb_02_r","thumb_03_r",
            "thigh_l","thigh_twist_01_l","calf_l","calf_twist_01_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r","ik_hand_gun"};
        private static readonly string[] Parents={"","root","pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02",
            "spine_05","clavicle_l","upperarm_l","upperarm_l","upperarm_l","lowerarm_l","lowerarm_l","hand_l","index_01_l","index_02_l","hand_l","thumb_01_l","thumb_02_l",
            "spine_05","clavicle_r","upperarm_r","upperarm_r","lowerarm_r","lowerarm_r","hand_r","index_01_r","index_02_r","hand_r","thumb_01_r","thumb_02_r",
            "pelvis","thigh_l","thigh_l","calf_l","calf_l","foot_l","pelvis","thigh_r","calf_r","foot_r","root"};
        public static NativeVisualRig.Rig Synthetic(out Vector3[] world)
        {
            const float floor=-.865f,down=50*(float)Math.PI/180;
            float c=(float)Math.Cos(down),s=(float)Math.Sin(down);
            Vector3 P(float x,float y,float z=0)=>new Vector3(x,y+floor,z);
            int n=Names.Length; var at=new Vector3[n]; world=at; var parent=new int[n];
            for(int i=0;i<n;i++) parent[i]=Array.IndexOf(Names,Parents[i]);
            void At(string name,Vector3 p) { at[Array.IndexOf(Names,name)]=p; }
            At("root",P(0,0)); At("pelvis",P(0,.98f,.02f)); At("spine_01",P(0,1.01f,.02f)); At("spine_02",P(0,1.07f,.035f));
            At("spine_03",P(0,1.14f,.04f)); At("spine_04",P(0,1.22f,.035f)); At("spine_05",P(0,1.39f,0)); At("neck_01",P(0,1.49f,-.015f));
            At("neck_02",P(0,1.53f,-.01f)); At("head",P(0,1.57f,0)); At("ik_hand_gun",P(-.4f,1.05f,.05f));
            foreach(bool left in new[]{true,false})
            {
                float x=left?1:-1; string side=left?"_l":"_r";
                Vector3 shoulder=P(x*.14f,1.43f,-.03f),arm=new Vector3(x*c,-s,0);
                At("clavicle"+side,P(x*.01f,1.45f,-.02f)); At("upperarm"+side,shoulder); At("lowerarm"+side,shoulder+arm*.26f);
                At("hand"+side,shoulder+arm*.49f); At("upperarm_twist_01"+side,shoulder+arm*.09f); At("lowerarm_twist_01"+side,shoulder+arm*(.26f+.14f));
                At("thigh"+side,P(x*.09f,.96f,.035f)); At("calf"+side,P(x*.10f,.535f,.03f)); At("foot"+side,P(x*.115f,.11f,0)); At("ball"+side,P(x*.115f,.014f,.115f));
                if(left) { At("shoulder_twist_l",shoulder); At("thigh_twist_01_l",P(.095f,.75f,.033f)); At("calf_twist_01_l",P(.105f,.32f,.015f)); }
                // Fingers curl toward the palm (+z here) a few degrees at rest, as exported hands do.
                Vector3 hand=shoulder+arm*.49f,palm=new Vector3(0,0,1),i0=hand+arm*.09f,i1=i0+Vector3.Normalize(arm+palm*.09f)*.035f;
                At("index_01"+side,i0); At("index_02"+side,i1); At("index_03"+side,i1+Vector3.Normalize(arm+palm*.18f)*.025f);
                Vector3 t0=hand+arm*.03f+palm*.02f,td=Vector3.Normalize(arm+palm*.5f),t1=t0+td*.03f;
                At("thumb_01"+side,t0); At("thumb_02"+side,t1); At("thumb_03"+side,t1+Vector3.Normalize(td+palm*.12f)*.025f);
            }
            // Bone frames: +x down each bone toward its first child (as exported humanoids do), rolled by a per-bone amount.
            var rot=new Quaternion[n];
            for(int i=0;i<n;i++)
            {
                int child=Array.IndexOf(Parents,Names[i]); Vector3 dir=child<0?new Vector3(0,1,0):world[child]-world[i];
                if(dir.LengthSquared()<1e-8f) dir=new Vector3(0,1,0);
                rot[i]=CapturedRig.Rot(Vector3.Normalize(dir),.37f*i)*CapturedRig.FromTo(CapturedRig.X,Vector3.Normalize(dir));
            }
            var lp=new Vector3[n]; var lr=new Quaternion[n];
            for(int i=0;i<n;i++)
            {
                int p=parent[i];
                lr[i]=p<0?rot[i]:Quaternion.Inverse(rot[p])*rot[i];
                lp[i]=p<0?world[i]:Vector3.Transform(world[i]-world[p],Quaternion.Inverse(rot[p]));
            }
            return new NativeVisualRig.Rig(NativeVisualRig.Mannequin(Names,parent,lp,lr));
        }

        public static void Run(string root)
        {
            var rig=Synthetic(out var restWorld);
            Bind(rig,restWorld);Console.WriteLine("PASS visual rig: the bind maps 21 joints (spine 2, chest 3, neck 2 bones), straightens arms and legs to the game's rest, keeps spine, head and feet as authored; leg ratio "+rig.LegRatio.ToString("0.000")+" ("+count+" checks)");
            int poses=Clips(rig,out float support,out float leg,out float grip);
            Console.WriteLine("PASS visual rig: "+poses+" captured poses: limb directions equal the game's (hands inside the shoulder line reach their chest-relative place), chains end on the game turn, wrists keep the roll and ease the bend (max "+(maxWrist*57.3f).ToString("0")+" degrees), the root scales by the leg ratio, twist shares, locals rebuild the pose; planted feet reach the ground point (max leg reach "+leg.ToString("0.00")+"), the support hand keeps the wrist offset (max reach "+support.ToString("0.00")+"), grip "+grip.ToString("0.000")+" m from the game's at most ("+count+" checks)");
            Console.WriteLine("INFO visual rig (synthetic skeleton): hands inside the game's shoulder line "+last.Inside+" ("+last.InsideFull+" fully, "+last.Crossed+" past the middle, at most "+last.Across.ToString("0.00")+" of the game's crossing); support shoulder reached forward "+last.Shrugged+" times, at most "+(last.Shoulder*57.3f).ToString("0")+" degrees; weapon hand gave way "+last.GaveWay+" times, at most "+last.GiveWay.ToString("0.000")+" m; seated fists "+last.Seated+", wrist bend at most "+(last.Bend*57.3f).ToString("0")+" degrees, off the straightest at most "+(last.Straight*57.3f).ToString("0.0")+"; planted feet "+last.Planted+", "+last.LegShort+" at full reach; two-handed support grips (guns, greatsword, hands together) "+last.Supported+", "+last.SupportShort+" at full reach; grip offset mean "+(last.GripSum/Math.Max(1,last.Gripped)).ToString("0.000")+" m; short grips by take: "+string.Join(", ",System.Linq.Enumerable.Select(last.Short,kv=>kv.Key+" "+kv.Value)));
            Floor(rig);Console.WriteLine("PASS visual rig: the floor fit keeps every visual bone's contact over the floor through the roll ("+count+" checks)");
            Allocation(rig);Console.WriteLine("PASS visual rig: posing, both IKs and the grip allocate nothing per frame and repeat bit for bit ("+count+" checks)");
            Binding(root);Console.WriteLine("PASS visual rig: the Unity binding mirrors at the boundary, writes local turns and the pelvis only, and names no pack types ("+count+" checks)");
        }

        private static void Bind(NativeVisualRig.Rig rig,Vector3[] restWorld)
        {
            for(int j=1;j<CapturedRig.Bones.Length;j++) Check(rig.Of[j]>=0,"joint "+CapturedRig.Bones[j].Name+" has a visual bone");
            Check(rig.Of[CapturedRig.Body]<0,"the motion-root has no visual bone (the pelvis carries it)");
            Check(rig.Names[rig.Of[CapturedRig.Spine]]=="spine_02"&&rig.Names[rig.Of[CapturedRig.Chest]]=="spine_05"&&rig.Names[rig.Of[CapturedRig.Neck]]=="neck_02","split chains end on their last bone");
            Check(Math.Abs(NativeVisualRig.GameHeadRise-.24f)<1e-5f&&Math.Abs(rig.HeadRise-(restWorld[rig.Find("head")].Y-restWorld[rig.Find("upperarm_r")].Y))<1e-4f,"head rise: the game's head joint .24 over its shoulders, the visual head bone's over its shoulders");
            float leg=Vector3.Distance(restWorld[rig.Find("thigh_r")],restWorld[rig.Find("calf_r")])+Vector3.Distance(restWorld[rig.Find("calf_r")],restWorld[rig.Find("foot_r")]);
            Check(Math.Abs(rig.LegRatio-leg/.5f)<1e-4f,"the leg ratio is the visual hip-to-ankle (straightened) over the game's .50");
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();pose.World();
            NativeVisualRig.Pose(state,pose,0,false,false);
            foreach(string limb in new[]{"upperarm_l","lowerarm_l","upperarm_r","lowerarm_r","thigh_l","calf_l","thigh_r","calf_r"})
            {
                int v=rig.Find(limb),child=-1;
                for(int k=v+1;k<rig.Count;k++) if(rig.Parent[k]==v&&rig.Joint[k]>=0&&rig.Joint[k]!=rig.Joint[v]) { child=k; break; }
                bool arm=limb.Contains("arm");float outward=limb.EndsWith("_l")?1:-1;
                Vector3 rest=arm?new Vector3(outward*(float)Math.Sin(NativeVisualRig.ArmSpread),-(float)Math.Cos(NativeVisualRig.ArmSpread),0):-CapturedRig.Y;
                Check(Angle(state.Position[child]-state.Position[v],rest)<1e-4f,limb+(arm?" hangs 15 degrees out from the body":" points straight down")+" at the game's rest");
                Check(Math.Abs(Vector3.Distance(state.Position[child],state.Position[v])-Vector3.Distance(restWorld[child],restWorld[v]))<1e-5f,limb+" keeps its length");
            }
            foreach(string kept in new[]{"pelvis","spine_01","spine_03","spine_05","neck_02","head","clavicle_l","foot_l","foot_r","ball_l"})
            {
                int v=rig.Find(kept); Quaternion rest=Quaternion.Identity;
                for(int k=v;k>=0;k=rig.Parent[k]) rest=rig.RestRotation[k]*rest;
                Check(Near(state.World[v],rest,1e-5f),kept+" keeps its authored world turn at rest");
            }
            Check(Math.Abs(state.Position[rig.Find("hand_l")].X)>.26f&&Math.Abs(state.Position[rig.Find("hand_r")].X)>.26f,"hanging hands clear the hips (shoulders .14 out)");
            Check(Near(state.Position[rig.Find("pelvis")],restWorld[rig.Find("pelvis")],1e-5f)&&Near(state.Position[rig.Find("head")],restWorld[rig.Find("head")],1e-5f),"the trunk and head stay where authored at rest");
            int t=rig.Find("upperarm_twist_01_l"),st=rig.Find("shoulder_twist_l"),lt=rig.Find("lowerarm_twist_01_l"),ct=rig.Find("calf_twist_01_l"),tt=rig.Find("thigh_twist_01_l");
            Check(rig.TwistJoint[t]==CapturedRig.LArm&&Math.Abs(rig.TwistShare[t]+(1-.09f/.26f))<1e-3f&&Math.Abs(rig.TwistShare[st]+1)<1e-4f,"upper-arm helpers counter the arm's roll, fully at the shoulder");
            Check(rig.TwistJoint[lt]==CapturedRig.LHand&&Math.Abs(rig.TwistShare[lt]-.14f/.23f)<1e-3f,"forearm helpers take the hand's roll by their place along the forearm");
            Check(rig.TwistJoint[ct]==CapturedRig.LFoot&&rig.TwistShare[ct]>0&&rig.TwistJoint[tt]==CapturedRig.LHip&&rig.TwistShare[tt]<0,"calf helpers take the foot's roll, thigh helpers counter the hip's");
            Check(rig.Joint[rig.Find("index_01_l")]<0&&rig.TwistJoint[rig.Find("index_01_l")]<0&&rig.Joint[rig.Find("ik_hand_gun")]<0,"fingers and helpers ride their parents");
            // The eased wrist: rest and small bends unchanged, a pure roll kept exactly, deep bends eased past the knee.
            Check(Near(NativeVisualRig.Wrist(Quaternion.Identity),Quaternion.Identity,1e-7f),"the wrist at rest is unchanged");
            var roll=CapturedRig.Rot(CapturedRig.Y,1.3f);Check(Near(NativeVisualRig.Wrist(roll),roll,1e-6f),"a pure wrist roll is kept");
            var small=CapturedRig.Rot(CapturedRig.X,.3f)*roll;Check(Near(NativeVisualRig.Wrist(small),small,1e-6f),"a bend under the knee is kept");
            var deep=CapturedRig.Rot(CapturedRig.X,107*(float)Math.PI/180);float eased=Angle(-CapturedRig.Y,Vector3.Transform(-CapturedRig.Y,NativeVisualRig.Wrist(deep)));
            Check(Math.Abs(eased-(25+.25f*82)*(float)Math.PI/180)<1e-3f,"a 107 degree rifle-hold bend eases to 45.5 degrees");
            Fingers(rig);
            bool threw=false;try{var bad=NativeVisualRig.Mannequin(new[]{"pelvis","root"},new[]{1,-1},new Vector3[2],new[]{Quaternion.Identity,Quaternion.Identity});new NativeVisualRig.Rig(bad);}catch(ArgumentException){threw=true;}
            Check(threw,"a bone before its parent is refused");
        }

        // Finger grips: each chain curls the way its rest curls, by the relaxed or closed angles, joint by joint.
        private static void Fingers(NativeVisualRig.Rig rig)
        {
            foreach(string side in new[]{"_l","_r"})
            {
                int hand=rig.Find("hand"+side),i0=rig.Find("index_01"+side),i1=rig.Find("index_02"+side),i2=rig.Find("index_03"+side),t0=rig.Find("thumb_01"+side),t1=rig.Find("thumb_02"+side),t2=rig.Find("thumb_03"+side);
                Check(rig.Finger[i0]==1&&rig.Finger[i1]==2&&rig.Finger[i2]==3&&rig.Finger[t0]==11&&rig.Finger[t2]==13&&rig.Side[i0]==(side=="_l"?0:1),"finger roles"+side);
                Check(Math.Abs(Vector3.Dot(rig.FingerAxis[i0],rig.FingerAxis[i2])-1)<1e-5f&&rig.FingerAxis[i0].LengthSquared()>.99f,"one curl axis per finger chain"+side);
                var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();pose.World();
                float RestAngle(int a,int b,int c)=>Angle(rig.AlignedPosition[b]-rig.AlignedPosition[a],rig.AlignedPosition[c]-rig.AlignedPosition[b]);
                float Bent(int a,int b,int c)=>Angle(state.Position[b]-state.Position[a],state.Position[c]-state.Position[b]);
                float rest0=RestAngle(hand,i0,i1),rest1=RestAngle(i0,i1,i2),restT=RestAngle(t0,t1,t2);
                float[] grips={0,1,.5f};
                float tipOpen=0;
                foreach(float g in grips)
                {
                    NativeVisualRig.Pose(state,pose,0,false,false,side=="_r"?g:0,side=="_l"?g:0);
                    Check(Math.Abs(Bent(hand,i0,i1)-(rest0+NativeVisualRig.Curl(1,g)))<2e-3f,"index base curls by "+NativeVisualRig.Curl(1,g)+" past its rest"+side);
                    Check(Math.Abs(Bent(i0,i1,i2)-(rest1+NativeVisualRig.Curl(2,g)))<2e-3f,"index middle joint curls"+side);
                    Check(Math.Abs(Bent(t0,t1,t2)-(restT+NativeVisualRig.Curl(12,g)))<2e-3f,"thumb middle joint curls"+side);
                    float tip=Vector3.Distance(state.Position[i2],state.Position[hand]);
                    if(g==0) tipOpen=tip; else if(g==1) Check(tip<tipOpen-.005f,"a closed hand draws the fingertip in"+side);
                }
            }
            Check(NativeVisualRig.Curl(1,0)==NativeVisualRig.Relaxed[0]&&NativeVisualRig.Curl(13,1)==NativeVisualRig.Closed[5]&&NativeVisualRig.Curl(0,1)==0,"the curl table");
            // The closed fist: a handle runs through it across the palm (+z here), a few centimetres out along the hand, toward the thumb.
            foreach(string side in new[]{"_l","_r"})
            {
                int s=side=="_l"?0:1,hand=rig.Find("hand"+side);Vector3 arm=Vector3.Normalize(rig.AlignedPosition[rig.Find("index_01"+side)]-rig.AlignedPosition[hand]);
                Check(rig.HasFist[s]&&Math.Abs(Vector3.Dot(rig.FistAxis[s],rig.FistAhead[s]))<1e-5f&&Math.Abs(rig.FistAxis[s].Length()-1)<1e-5f,"the fist's axis and forward are square"+side);
                Check(Vector3.Dot(rig.FistCenter[s],new Vector3(0,0,1))>.005f&&Vector3.Dot(rig.FistCenter[s],arm)>.03f&&rig.FistCenter[s].Length()<.15f,"the fist's centre lies on the palm side, out along the hand"+side);
                Check(Vector3.Dot(rig.FistAxis[s],rig.AlignedPosition[rig.Find("thumb_01"+side)]-rig.AlignedPosition[rig.Find("index_01"+side)])>0,"the fist's axis points to the thumb's side"+side);
                Check(Angle(rig.FistAhead[s],arm)<35f/57.3f,"the fist's forward runs along the hand"+side);
            }
        }

        private sealed class Stats { public int Poses, Planted, LegShort, Supported, SupportShort, Gripped, Inside, InsideFull, Crossed, GaveWay, Shrugged, Seated; public float Support, Leg, Grip, GripSum, Across, GiveWay, Shoulder, Bend, Straight; public readonly System.Collections.Generic.Dictionary<string,int> Short=new System.Collections.Generic.Dictionary<string,int>(); }
        private static Stats last;
        private static float maxWrist;
        private static readonly int[][] Limbs={new[]{CapturedRig.LArm,CapturedRig.LElbow},new[]{CapturedRig.LElbow,CapturedRig.LHand},new[]{CapturedRig.RArm,CapturedRig.RElbow},new[]{CapturedRig.RElbow,CapturedRig.RHand},
            new[]{CapturedRig.LHip,CapturedRig.LKnee},new[]{CapturedRig.LKnee,CapturedRig.LFoot},new[]{CapturedRig.RHip,CapturedRig.RKnee},new[]{CapturedRig.RKnee,CapturedRig.RFoot}};

        // Every captured melee clip, and the gun gait (pistol, rifle, machine gun: idle, aimed, run, sprint, roll) as
        // CapturedAnimation drives it.
        private static int Clips(NativeVisualRig.Rig rig,out float maxSupport,out float maxLeg,out float maxGrip)
        {
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var f=new float[CapturedClip.Channels];var stats=new Stats();
            for(int set=0;set<CapturedClips.Sets.Length;set++) foreach(var clip in CapturedClips.Sets[set])
                for(int k=0;k<8;k++)
                {
                    clip.Sample(clip.Duration*k/8,f);CapturedRig.Sample(f,set,pose);
                    Verify(rig,state,pose,clip.Name,true,set==2,stats,MeleeGrasp(set));
                }
            int melee=stats.Poses;
            foreach(var w in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
                foreach(string scene in new[]{"idle","aim","run","sprint","roll"})
                {
                    var a=new Actor{Id=31,SourceSpec=GunMotion.Spec(w),Weapon=w,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),Ammo=GunRules.Get(w).Magazine};
                    if(scene=="aim") a.Aiming=true;
                    if(scene=="run"||scene=="sprint"||scene=="roll") a.Velocity=new V3(0,0,scene=="sprint"?8.5f:6.8f);
                    a.Sprinting=scene=="sprint";
                    var motion=new GunMotion();var juggle=new NativeJuggleTrack();Vector3 at=Vector3.Zero;float t0=20,dt=1/60f;
                    for(int i=0;i<150;i++)
                    {
                        float t=t0+i*dt; if(scene=="roll"&&i==30) a.RollAt=t;
                        at+=new Vector3(a.Velocity.x,0,a.Velocity.z)*dt;
                        motion.Rig.GroupPosition=new Vector3(-at.X,.865f,at.Z);motion.Rig.GroupYaw=0;
                        motion.Pose(a,t,pose,false,juggle,out _);motion.Finish(pose,t);pose.World();
                        if(i%6==0) Verify(rig,state,pose,w+" "+scene,!a.Rolling(t),true,stats,new NativeVisualRig.Grasp{RightGrip=1,Support=1,Right=NativeVisualRig.PistolGrip(w),SupportHold=NativeVisualRig.GunSupport(w,pose)});
                        if(scene=="aim"&&i>60) Check(NativeVisualRig.HandsTogether(pose)==1,w+" aimed: the game's hands hold together, so the support grip is on");
                    }
                }
            Check(melee>200&&stats.Poses-melee>300,"enough captured and gun poses ("+melee+" + "+(stats.Poses-melee)+")");
            maxSupport=stats.Support;maxLeg=stats.Leg;maxGrip=stats.Grip;last=stats;
            return stats.Poses;
        }

        // Each melee set's hands as the game draws them: sword in the right; daggers in both, either way round; the greatsword
        // two-handed; the shield (drawn on the right hand, its clips mirrored) by its grip bar.
        private static NativeVisualRig.Grasp MeleeGrasp(int set)
        {
            var held=Quaternion.Identity;var g=new NativeVisualRig.Grasp{RightGrip=1,Right=set==3?NativeVisualRig.ShieldBar(held):NativeVisualRig.Blade(held,set==1)};
            if(set==1){g.LeftGrip=1;g.Left=NativeVisualRig.Blade(held,true);}
            if(set==2){g.Support=1;g.SupportHold=NativeVisualRig.GreatswordSupport(held);}
            return g;
        }

        private static void Verify(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,string name,bool plant,bool twoHanded,Stats stats,NativeVisualRig.Grasp grasp)
        {
            int pelvis=rig.Of[CapturedRig.Pelvis];int[][] limbs=Limbs;stats.Poses++;
            {
                {
                    NativeVisualRig.Pose(state,pose,0,false,false);
                    Vector3 fkLeftElbow=state.Position[rig.Of[CapturedRig.LElbow]],fkLeftShoulder=state.Position[rig.Of[CapturedRig.LArm]];
                    Vector3[] fkKnee={state.Position[rig.Of[CapturedRig.LKnee]],state.Position[rig.Of[CapturedRig.RKnee]]};
                    Inside(rig,state,pose,name,stats);
                    foreach(var l in limbs)
                    {
                        if(l[0]>=CapturedRig.LArm&&l[0]<=CapturedRig.LHand&&state.LeftInside>0||l[0]>=CapturedRig.RArm&&l[0]<=CapturedRig.RHand&&state.RightInside>0) continue; // checked by Inside
                        Vector3 game=pose.WorldPosition[l[1]]-pose.WorldPosition[l[0]],vis=state.Position[rig.Of[l[1]]]-state.Position[rig.Of[l[0]]];
                        int clav=l[0]<=CapturedRig.LHand?CapturedRig.LClav:CapturedRig.RClav;
                        if(l[0]>=CapturedRig.LArm&&l[0]<=CapturedRig.RHand) // arms: the game's direction, turned by the hanging spread at the shoulder
                            game=Vector3.Transform(game,pose.WorldRotation[clav]*NativeVisualRig.Spread(pose,clav==CapturedRig.LClav)*Quaternion.Inverse(pose.WorldRotation[clav]));
                        Check(Angle(game,vis)<2e-3f,name+" "+CapturedRig.Bones[l[0]].Name+" follows the game's bone direction");
                    }
                    foreach(int j in new[]{CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LFoot,CapturedRig.RFoot})
                        Check(Near(state.Turn[rig.Of[j]],pose.WorldRotation[j],2e-4f),name+" "+CapturedRig.Bones[j].Name+" carries the game joint's turn");
                    foreach(int h in new[]{CapturedRig.LHand,CapturedRig.RHand})
                    {
                        // The visual wrist: the game's roll in full, the bend eased past the knee (NativeVisualRig.Wrist).
                        Quaternion want=state.Turn[rig.Of[h-1]]*NativeVisualRig.Wrist(pose.Rotation[h]); // on the visual forearm (which carries the spread)
                        Check(Near(state.Turn[rig.Of[h]],want,2e-4f),name+" "+CapturedRig.Bones[h].Name+" takes the eased wrist");
                        float gameBend=Angle(Vector3.Transform(-CapturedRig.Y,pose.WorldRotation[h-1]),Vector3.Transform(-CapturedRig.Y,pose.WorldRotation[h]));
                        float visBend=Angle(Vector3.Transform(-CapturedRig.Y,state.Turn[rig.Of[h-1]]),Vector3.Transform(-CapturedRig.Y,state.Turn[rig.Of[h]]));
                        Check(Math.Abs(visBend-NativeVisualRig.WristBend(gameBend))<1e-2f,name+" "+CapturedRig.Bones[h].Name+" bends "+(visBend*57.3f).ToString("0")+" for the game's "+(gameBend*57.3f).ToString("0"));
                        Check(Math.Abs(NativeVisualRig.Twist(Quaternion.Inverse(state.Turn[rig.Of[h-1]])*state.Turn[rig.Of[h]])-NativeVisualRig.Twist(pose.Rotation[h]))<2e-3f,name+" "+CapturedRig.Bones[h].Name+" keeps the game wrist's roll");
                        maxWrist=Math.Max(maxWrist,visBend);
                    }
                    Check(Near(state.Position[pelvis]-rig.PelvisRest,(pose.WorldPosition[CapturedRig.Pelvis]-rig.GamePelvisRest)*rig.LegRatio,1e-4f),name+" the root moves by the leg ratio");
                    for(int v=0;v<rig.Count;v++) if(rig.TwistJoint[v]>=0)
                    {
                        Quaternion rel=Quaternion.Inverse(state.Turn[rig.Parent[v]])*state.Turn[v];
                        Check(Near(rel,CapturedRig.Rot(CapturedRig.Y,rig.TwistShare[v]*NativeVisualRig.Twist(pose.Rotation[rig.TwistJoint[v]])),2e-4f),name+" "+rig.Names[v]+" takes its share of the roll");
                    }
                    // Planted feet and the support grip.
                    Vector3 rightBefore=state.Position[rig.Of[CapturedRig.RHand]],leftShoulder=state.Position[rig.Of[CapturedRig.LArm]];
                    Vector3 gripBefore=rightBefore+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand];
                    NativeVisualRig.Pose(state,pose,1,plant,false,1,0);
                    {
                        float most=(rig.ArmUpper+rig.ArmLower)*NativeVisualRig.SupportReach,far=Vector3.Distance(gripBefore,leftShoulder),moved=Vector3.Distance(state.Position[rig.Of[CapturedRig.RHand]],rightBefore);
                        bool over=far>most;
                        Check(state.ShoulderTurn>=0&&state.ShoulderTurn<=NativeVisualRig.ShoulderReach*state.SupportWeight+1e-5f,name+" the support shoulder reaches forward at most "+(NativeVisualRig.ShoulderReach*57.3f).ToString("0")+" degrees");
                        Check(state.GiveWay>=0&&state.GiveWay<=(over?(far-most)*state.SupportWeight:0)+1e-5f,name+" the weapon hand gives way at most by the support grip's excess reach");
                        if(state.GiveWay>0) Check(Math.Abs(state.ShoulderTurn-NativeVisualRig.ShoulderReach*state.SupportWeight)<1e-4f,name+" the weapon hand gives way only once the shoulder has reached as far as it may");
                        if(over){ if(state.ShoulderTurn>0){stats.Shrugged++;stats.Shoulder=Math.Max(stats.Shoulder,state.ShoulderTurn);} if(state.GiveWay>0){stats.GaveWay++;stats.GiveWay=Math.Max(stats.GiveWay,state.GiveWay);}
                            if(state.SupportWeight>=.9999f) Check(state.LeftArmReach<=NativeVisualRig.SupportReach+1e-3f,name+" with the shoulder's reach and any give-way, the support hand reaches its grip");}
                        else Check(state.ShoulderTurn==0&&moved<1e-4f,name+" the shoulder and the weapon hand stay where the copied arm put them");
                    }
                    Check(state.RightGrip==1&&state.LeftGrip>=state.SupportWeight,name+" the weapon hand closes, and the left closes while it supports");
                    foreach(bool left in new[]{true,false})
                    {
                        int hip=left?CapturedRig.LHip:CapturedRig.RHip; float w=left?state.LeftFootWeight:state.RightFootWeight;
                        float above=pose.WorldPosition[hip+2].Y-NativeVisualRig.AnkleRest(hip);
                        Check(!plant?w==0:above>NativeVisualRig.LiftedHeight?w==0:above<=NativeVisualRig.PlantedHeight?w==1:w>0&&w<1,name+" foot weight follows the game ankle's height");
                        if(w==1)
                        {
                            float reach=left?state.LeftLegReach:state.RightLegReach; stats.Leg=Math.Max(stats.Leg,reach); stats.Planted++; if(reach>=.999f) stats.LegShort++;
                            Vector3 g=pose.WorldPosition[hip+2],want=new Vector3(g.X,rig.AlignedPosition[rig.Of[hip+2]].Y+above,g.Z);
                            if(reach<.999f) Check(Near(state.Position[rig.Of[hip+2]],want,1e-3f),name+" a planted foot reaches the game ankle's ground point");
                            Vector3 vh=state.Position[rig.Of[hip]],vk=state.Position[rig.Of[hip+1]],va=state.Position[rig.Of[hip+2]];
                            Vector3 gh=pose.WorldPosition[hip],gk=pose.WorldPosition[hip+1],ga=pose.WorldPosition[hip+2];
                            Vector3 gb=gk-(gh+ga)*.5f,vb=vk-(vh+va)*.5f;
                            Vector3 fb=fkKnee[left?0:1]-(vh+va)*.5f,line=Vector3.Normalize(va-vh);fb-=line*Vector3.Dot(fb,line);
                            if(fb.Length()>.02f) Check(Vector3.Dot(fb,vb)>0,name+" the knee bends to its unadjusted side");
                        }
                    }
                    if(twoHanded&&state.SupportWeight>0){stats.Support=Math.Max(stats.Support,state.LeftArmReach); stats.Supported++;
                    {Vector3 ve=state.Position[rig.Of[CapturedRig.LElbow]],vs=state.Position[rig.Of[CapturedRig.LArm]],vt=state.Position[rig.Of[CapturedRig.LHand]],ln=Vector3.Normalize(vt-vs);
                     Vector3 fe=fkLeftElbow-(vs+vt)*.5f;fe-=ln*Vector3.Dot(fe,ln);Vector3 ie=ve-(vs+vt)*.5f;
                     if(fe.Length()>.02f) Check(Vector3.Dot(fe,ie)>0,name+" the support elbow bends to its unadjusted side");} if(state.LeftArmReach>=.999f){stats.SupportShort++;string key=name.StartsWith("Pistol")||name.StartsWith("Rifle")||name.StartsWith("MachineGun")?name:"greatsword";stats.Short[key]=(stats.Short.TryGetValue(key,out int c)?c:0)+1;}}
                    Vector3 hand=state.Position[rig.Of[CapturedRig.LHand]],right=state.Position[rig.Of[CapturedRig.RHand]];
                    if(state.SupportWeight>=.9999f&&state.LeftArmReach<.999f) Check(Near(hand,right+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],1e-3f),name+" the support hand keeps the game's wrist offset");
                    Check(Near(state.World[rig.Of[CapturedRig.LHand]],state.Turn[rig.Of[CapturedRig.LHand]]*rig.AlignedWorld[rig.Of[CapturedRig.LHand]],2e-4f),name+" the support hand keeps the game wrist turn");
                    NativeVisualRig.Grip(state,pose,out var gp,out var gq);
                    var socket=NativeKillBoxes.RightHandSocket;Vector3 offset=Vector3.Transform(new Vector3(socket[0],socket[1],socket[2]),pose.WorldRotation[CapturedRig.RHand]);
                    Check(Near(gp,right+offset,1e-5f)&&Near(gq,pose.WorldRotation[CapturedRig.RHand],1e-6f),name+" the grip is the game socket on the visual wrist");
                    float gd=Vector3.Distance(gp,pose.WorldPosition[CapturedRig.RHand]+offset); stats.Grip=Math.Max(stats.Grip,gd); stats.GripSum+=gd; stats.Gripped++;
                    // The locals rebuild the world pose.
                    Quaternion[] w2=new Quaternion[rig.Count];Vector3[] p2=new Vector3[rig.Count];
                    for(int v=0;v<rig.Count;v++){int p=rig.Parent[v];w2[v]=p<0?state.Local[v]:w2[p]*state.Local[v];p2[v]=p<0?state.LocalPosition[v]:p2[p]+Vector3.Transform(state.LocalPosition[v],w2[p]);}
                    for(int v=0;v<rig.Count;v++) Check(Near(w2[v],state.World[v],2e-4f)&&Near(p2[v],state.Position[v],2e-4f),name+" "+rig.Names[v]+" locals rebuild the pose");
                    // Seated fists on the weapon's handles.
                    NativeVisualRig.Pose(state,pose,grasp,plant,false);
                    Seats(rig,state,pose,grasp,name,stats);
                    for(int v=0;v<rig.Count;v++){int p=rig.Parent[v];w2[v]=p<0?state.Local[v]:w2[p]*state.Local[v];p2[v]=p<0?state.LocalPosition[v]:p2[p]+Vector3.Transform(state.LocalPosition[v],w2[p]);}
                    for(int v=0;v<rig.Count;v++) Check(Near(w2[v],state.World[v],2e-4f)&&Near(p2[v],state.Position[v],2e-4f),name+" "+rig.Names[v]+" locals rebuild the seated pose");
                }
            }

        }

        // A seated fist: its centre on the handle's grip point (the weapon drawn at its anchor, the game wrist's turn), its axis
        // along the handle, the hand pointing as nearly along the forearm as the handle leaves (the straightest wrist, where the
        // handle stands 30 degrees or more off the forearm's line; nearer, the game wrist's direction decides), and its
        // fingers carried with it. The support fist closes on the right weapon's support hold.
        private static void Seats(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,in NativeVisualRig.Grasp g,string name,Stats stats)
        {
            NativeVisualRig.WeaponFrame(state,pose,out var wp,out var wq);
            Check(Near(wp,state.RightAnchor,1e-6f)&&Near(wq,pose.WorldRotation[CapturedRig.RHand],1e-6f),name+" the weapon frame is the anchor with the game wrist turn");
            Check(state.RightSeated==g.Right.Seated,name+" the right fist seats when its weapon has a handle");
            for(int side=0;side<2;side++)
            {
                bool left=side==0;int ga=left?CapturedRig.LArm:CapturedRig.RArm,va=rig.Of[ga],ve=rig.Of[ga+1],vh=rig.Of[ga+2];
                Vector3 anchor;Quaternion w;NativeVisualRig.Hold hold;bool support=false;
                if(left&&state.SupportWeight>=.9999f&&g.SupportHold.Seated){anchor=state.RightAnchor;w=pose.WorldRotation[CapturedRig.RHand];hold=g.SupportHold;support=true;}
                else if(left?state.LeftSeated:state.RightSeated){anchor=left?state.LeftAnchor:state.RightAnchor;w=pose.WorldRotation[ga+2];hold=left?g.Left:g.Right;}
                else continue;
                stats.Seated++;
                Vector3 fist=state.Position[vh]+Vector3.Transform(rig.FistCenter[side],state.Turn[vh]),grip=anchor+Vector3.Transform(hold.Center,w);
                Vector3 axis=Vector3.Transform(hold.Axis,w),fistAxis=Vector3.Transform(rig.FistAxis[side],state.Turn[vh]);
                float wrist=Vector3.Distance(state.Position[vh],state.Position[va]),shortest=Math.Abs(rig.ArmUpper-rig.ArmLower)+.01f;
                if(wrist>shortest&&(!support||state.LeftArmReach<.999f)) Check(Vector3.Distance(fist,grip)<2e-3f,name+(left?" left":" right")+(support?" support":"")+" fist closes on its handle's grip point");
                float dot=Vector3.Dot(axis,fistAxis);
                Check(hold.Either?Math.Abs(dot)>.9999f:dot>.9999f,name+(left?" left":" right")+" fist's axis lies along the handle"+(hold.Either?" (either way)":" toward the blade or barrel"));
                Vector3 forearm=Vector3.Normalize(state.Position[vh]-state.Position[ve]),ahead=Vector3.Transform(rig.FistAhead[side],state.Turn[vh]);
                Vector3 straight=forearm-fistAxis*Vector3.Dot(forearm,fistAxis);
                if(straight.Length()>.5f) stats.Straight=Math.Max(stats.Straight,Angle(ahead,straight));
                if(straight.Length()>.5f) Check(Angle(ahead,straight)<3f/57.3f,name+(left?" left":" right")+" wrist is as straight as the handle allows (off by "+(Angle(ahead,straight)*57.3f).ToString("0.0")+")");
                stats.Bend=Math.Max(stats.Bend,Angle(ahead,forearm));
                for(int v=vh;v<rig.Count;v++) if(v==vh||(rig.Finger[v]!=0&&rig.Side[v]==side)) Check(Near(state.World[v],state.Turn[v]*rig.AlignedWorld[v],2e-4f),name+" "+rig.Names[v]+" turns with its seated hand");
            }
        }

        // A hand inside the game's shoulder line: the copied arm (the game's directions, turned by the hanging spread, from the
        // visual shoulder) shifted sideways to the game hand's place from the chest's middle, scaled by the shoulder widths;
        // lowered by the head drop; then moved in depth until the arm is as straight as the copied one.
        private static void Inside(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,string name,Stats stats)
        {
            foreach(bool left in new[]{true,false})
            {
                int ga=left?CapturedRig.LArm:CapturedRig.RArm,clav=left?CapturedRig.LClav:CapturedRig.RClav;
                Quaternion spread=pose.WorldRotation[clav]*NativeVisualRig.Spread(pose,left)*Quaternion.Inverse(pose.WorldRotation[clav]);
                Vector3 shoulder=state.Position[rig.Of[ga]],hand=state.Position[rig.Of[ga+2]];
                Vector3 elbowCopy=shoulder+Vector3.Normalize(Vector3.Transform(pose.WorldPosition[ga+1]-pose.WorldPosition[ga],spread))*rig.ArmUpper;
                Vector3 handCopy=elbowCopy+Vector3.Normalize(Vector3.Transform(pose.WorldPosition[ga+2]-pose.WorldPosition[ga+1],spread))*rig.ArmLower;
                Vector3 side=Vector3.Transform(left?CapturedRig.X:-CapturedRig.X,pose.WorldRotation[CapturedRig.Chest]);
                Vector3 gm=(pose.WorldPosition[CapturedRig.LClav]+pose.WorldPosition[CapturedRig.RClav])*.5f,vm=(state.Position[rig.Of[CapturedRig.LClav]]+state.Position[rig.Of[CapturedRig.RClav]])*.5f;
                float gs=Vector3.Dot(pose.WorldPosition[ga]-gm,side),gh=Vector3.Dot(pose.WorldPosition[ga+2]-gm,side),vs=Vector3.Dot(shoulder-vm,side);
                float w=gs<.05f?0:CapturedClip.Ease((gs-gh)/NativeVisualRig.InsideBand),got=left?state.LeftInside:state.RightInside;
                Check(Math.Abs(got-w)<1e-6f,name+(left?" left":" right")+" hand's inside weight follows the game hand's place from the shoulder line");
                if(w==0) continue;
                stats.Inside++;
                Vector3 copy=handCopy-shoulder,up=Vector3.Transform(CapturedRig.Y,pose.WorldRotation[CapturedRig.Chest]),ahead=Vector3.Transform(CapturedRig.Z,pose.WorldRotation[CapturedRig.Chest]);
                float across=Vector3.Dot(copy,side)+(gh*vs/gs-Vector3.Dot(handCopy-vm,side))*w;
                float gameRise=Vector3.Dot(pose.WorldPosition[ga+2]-pose.WorldPosition[ga],up),G=NativeVisualRig.GameHeadRise;
                float drop=Math.Clamp(Math.Min(gameRise,2*G-gameRise),0,G)*(1-rig.HeadRise/G)*(rig.ArmUpper+rig.ArmLower)/NativeVisualRig.GameArm;
                Check(Math.Abs(NativeVisualRig.HeadDrop(rig,gameRise)-drop)<1e-6f,name+" the head drop peaks at the game head's height and is gone by twice it");
                float rise=Vector3.Dot(copy,up)-drop*w,depth=Vector3.Dot(copy,ahead),keep=copy.LengthSquared()-across*across-rise*rise;
                if(keep>0) depth=Math.Sign(depth<0?-1:1)*(float)Math.Sqrt(keep);
                Vector3 want=shoulder+side*across+up*rise+ahead*depth;
                if(keep>0) Check(Math.Abs(Vector3.Distance(want,shoulder)-copy.Length())<1e-4f,name+" the reach target keeps the copied arm's extension");
                if(Vector3.Distance(want,shoulder)<(rig.ArmUpper+rig.ArmLower)*.999f) Check(Near(hand,want,1e-3f),name+(left?" left":" right")+" hand inside the shoulder line reaches its chest-relative place");
                Quaternion swing=CapturedRig.FromTo(Vector3.Normalize(copy),Vector3.Normalize(want-shoulder));
                Check(Near(state.Position[rig.Of[ga+1]],shoulder+Vector3.Transform(elbowCopy-shoulder,swing),1e-3f),name+(left?" left":" right")+" arm swings at the shoulder by the shortest turn to its hand's place");
                Check(Math.Abs(Angle(state.Position[rig.Of[ga+1]]-shoulder,hand-state.Position[rig.Of[ga+1]])-Angle(elbowCopy-shoulder,handCopy-elbowCopy))<2e-3f,name+(left?" left":" right")+" elbow keeps the game's bend");
                if(w<1) continue;
                stats.InsideFull++;
                float past=-Vector3.Dot(hand-vm,side),gamePast=-gh; // past the middle toward the other side
                if(gamePast>0){stats.Crossed++;stats.Across=Math.Max(stats.Across,past/gamePast);}
                Check(past<=Math.Max(0,gamePast)*vs/gs+1e-3f||Vector3.Distance(want,shoulder)>=(rig.ArmUpper+rig.ArmLower)*.999f,name+(left?" left":" right")+" hand crosses the middle no further than the game's, scaled");
            }
        }

        private static void Floor(NativeVisualRig.Rig rig)
        {
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();
            for(int k=0;k<=40;k++)
            {
                pose.Reset();CapturedRig.Roll(pose,k/40f);
                NativeVisualRig.Pose(state,pose,0,false,true);
                float low=float.MaxValue;for(int v=0;v<rig.Count;v++) low=Math.Min(low,state.Position[v].Y-rig.Contact[v]);
                Check(low>=NativeVisualRig.Floor-1e-4f,"roll "+k+": nothing below the floor");
            }
            pose.Reset();pose.World();NativeVisualRig.Pose(state,pose,0,true,true);
            float rest=float.MaxValue;for(int v=0;v<rig.Count;v++) rest=Math.Min(rest,state.Position[v].Y-rig.Contact[v]);
            Check(Math.Abs(rest-NativeVisualRig.Floor)<1e-4f,"standing, the soles touch the floor exactly");
        }

        private static void Allocation(NativeVisualRig.Rig rig)
        {
            var state=new NativeVisualRig.State(rig);var other=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var f=new float[CapturedClip.Channels];
            var clip=CapturedClips.Sets[0][0];clip.Sample(.3f,f);CapturedRig.Sample(f,0,pose);
            for(int i=0;i<50;i++){NativeVisualRig.Pose(state,pose,1,true,true);NativeVisualRig.Grip(state,pose,out _,out _);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            var grasp=MeleeGrasp(2);
            for(int i=0;i<1000;i++){if(i%2==0)NativeVisualRig.Pose(state,pose,i%4,i%3!=0,i%5==0,i%4*.25f,i%3*.5f);else{grasp.Support=i%3*.5f;NativeVisualRig.Pose(state,pose,grasp,i%3!=0,i%5==0);}NativeVisualRig.Grip(state,pose,out _,out _);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing allocates nothing per frame");
            NativeVisualRig.Pose(state,pose,1,true,false);NativeVisualRig.Pose(other,pose,1,true,false);
            for(int v=0;v<rig.Count;v++) Check(state.Local[v].Equals(other.Local[v])&&state.LocalPosition[v].Equals(other.LocalPosition[v]),"the same pose gives the same bones bit for bit");
        }

        private static void Binding(string root)
        {
            string code=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualRigBinding.cs"));
            string core=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeVisualRig.cs"));
            Check(code.Contains("CapturedRig.Mirror(new N.Vector3(")&&code.Contains("CapturedRig.Mirror(new N.Quaternion(")&&code.Contains("CapturedRig.Mirror(State.Local[v])"),"the binding mirrors into and out of the source frame");
            Check(code.Contains("Bones[v].localRotation=")&&code.Contains("if(v==pelvis)")&&!code.Contains(".position=")&&!code.Contains("localScale"),"the binding writes local turns and the pelvis position only");
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC"})
                Check(!code.Contains(pack)&&!core.Contains(pack),"no pack types or names in the adapter: "+pack);
        }
    }
}
#endif
