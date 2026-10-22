#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The visual rig adapter (Core/NativeVisualRig.cs, Runtime/NativeVisualRigBinding.cs; docs/BOZO-INTEGRATION.md step 3).
    // Driven by every captured clip on a synthetic Unreal-mannequin-named skeleton built here (A-pose arms 50 degrees down,
    // bone frames turned off identity, long legs): no pack data. Checked: the bind straightens the limbs to the game's rest
    // and keeps the spine, head and feet as authored; limbs follow the game's bone directions exactly, split chains end on
    // the game joint's turn, hands carry the game wrist turn; the root height scales by the leg ratio; planted feet reach the
    // game ankle's ground point and bend toward the game knee; the support hand keeps the game's wrist offset; twist bones
    // take their share of the roll; the grip is the game socket on the visual wrist (P1); the floor fit; the locals rebuild
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
            "clavicle_l","upperarm_l","shoulder_twist_l","upperarm_twist_01_l","lowerarm_l","lowerarm_twist_01_l","hand_l","index_01_l",
            "clavicle_r","upperarm_r","upperarm_twist_01_r","lowerarm_r","lowerarm_twist_01_r","hand_r",
            "thigh_l","thigh_twist_01_l","calf_l","calf_twist_01_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r","ik_hand_gun"};
        private static readonly string[] Parents={"","root","pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02",
            "spine_05","clavicle_l","upperarm_l","upperarm_l","upperarm_l","lowerarm_l","lowerarm_l","hand_l",
            "spine_05","clavicle_r","upperarm_r","upperarm_r","lowerarm_r","lowerarm_r",
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
                if(left) { At("shoulder_twist_l",shoulder); At("index_01_l",shoulder+arm*.58f); At("thigh_twist_01_l",P(.095f,.75f,.033f)); At("calf_twist_01_l",P(.105f,.32f,.015f)); }
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
            Console.WriteLine("PASS visual rig: "+poses+" captured poses: limb directions equal the game's, chains end on the game turn, hands carry the wrist turn, the root scales by the leg ratio, twist shares, locals rebuild the pose; planted feet reach the ground point (max leg reach "+leg.ToString("0.00")+"), the support hand keeps the wrist offset (max reach "+support.ToString("0.00")+"), grip "+grip.ToString("0.000")+" m from the game's at most ("+count+" checks)");
            Console.WriteLine("INFO visual rig (synthetic skeleton): planted feet "+last.Planted+", "+last.LegShort+" at full reach; two-handed support grips (guns, greatsword, hands together) "+last.Supported+", "+last.SupportShort+" at full reach; grip offset mean "+(last.GripSum/Math.Max(1,last.Gripped)).ToString("0.000")+" m; short grips by take: "+string.Join(", ",System.Linq.Enumerable.Select(last.Short,kv=>kv.Key+" "+kv.Value)));
            Floor(rig);Console.WriteLine("PASS visual rig: the floor fit keeps every visual bone's contact over the floor through the roll ("+count+" checks)");
            Allocation(rig);Console.WriteLine("PASS visual rig: posing, both IKs and the grip allocate nothing per frame and repeat bit for bit ("+count+" checks)");
            Binding(root);Console.WriteLine("PASS visual rig: the Unity binding mirrors at the boundary, writes local turns and the pelvis only, and names no pack types ("+count+" checks)");
        }

        private static void Bind(NativeVisualRig.Rig rig,Vector3[] restWorld)
        {
            for(int j=1;j<CapturedRig.Bones.Length;j++) Check(rig.Of[j]>=0,"joint "+CapturedRig.Bones[j].Name+" has a visual bone");
            Check(rig.Of[CapturedRig.Body]<0,"the motion-root has no visual bone (the pelvis carries it)");
            Check(rig.Names[rig.Of[CapturedRig.Spine]]=="spine_02"&&rig.Names[rig.Of[CapturedRig.Chest]]=="spine_05"&&rig.Names[rig.Of[CapturedRig.Neck]]=="neck_02","split chains end on their last bone");
            float leg=Vector3.Distance(restWorld[rig.Find("thigh_r")],restWorld[rig.Find("calf_r")])+Vector3.Distance(restWorld[rig.Find("calf_r")],restWorld[rig.Find("foot_r")]);
            Check(Math.Abs(rig.LegRatio-leg/.5f)<1e-4f,"the leg ratio is the visual hip-to-ankle (straightened) over the game's .50");
            var state=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();pose.World();
            NativeVisualRig.Pose(state,pose,0,false,false);
            foreach(string limb in new[]{"upperarm_l","lowerarm_l","upperarm_r","lowerarm_r","thigh_l","calf_l","thigh_r","calf_r"})
            {
                int v=rig.Find(limb),child=-1;
                for(int k=v+1;k<rig.Count;k++) if(rig.Parent[k]==v&&rig.Joint[k]>=0&&rig.Joint[k]!=rig.Joint[v]) { child=k; break; }
                Check(Angle(state.Position[child]-state.Position[v],-CapturedRig.Y)<1e-4f,limb+" points straight down at the game's rest");
                Check(Math.Abs(Vector3.Distance(state.Position[child],state.Position[v])-Vector3.Distance(restWorld[child],restWorld[v]))<1e-5f,limb+" keeps its length");
            }
            foreach(string kept in new[]{"pelvis","spine_01","spine_03","spine_05","neck_02","head","clavicle_l","foot_l","foot_r","ball_l"})
            {
                int v=rig.Find(kept); Quaternion rest=Quaternion.Identity;
                for(int k=v;k>=0;k=rig.Parent[k]) rest=rig.RestRotation[k]*rest;
                Check(Near(state.World[v],rest,1e-5f),kept+" keeps its authored world turn at rest");
            }
            Check(Near(state.Position[rig.Find("pelvis")],restWorld[rig.Find("pelvis")],1e-5f)&&Near(state.Position[rig.Find("head")],restWorld[rig.Find("head")],1e-5f),"the trunk and head stay where authored at rest");
            int t=rig.Find("upperarm_twist_01_l"),st=rig.Find("shoulder_twist_l"),lt=rig.Find("lowerarm_twist_01_l"),ct=rig.Find("calf_twist_01_l"),tt=rig.Find("thigh_twist_01_l");
            Check(rig.TwistJoint[t]==CapturedRig.LArm&&Math.Abs(rig.TwistShare[t]+(1-.09f/.26f))<1e-3f&&Math.Abs(rig.TwistShare[st]+1)<1e-4f,"upper-arm helpers counter the arm's roll, fully at the shoulder");
            Check(rig.TwistJoint[lt]==CapturedRig.LHand&&Math.Abs(rig.TwistShare[lt]-.14f/.23f)<1e-3f,"forearm helpers take the hand's roll by their place along the forearm");
            Check(rig.TwistJoint[ct]==CapturedRig.LFoot&&rig.TwistShare[ct]>0&&rig.TwistJoint[tt]==CapturedRig.LHip&&rig.TwistShare[tt]<0,"calf helpers take the foot's roll, thigh helpers counter the hip's");
            Check(rig.Joint[rig.Find("index_01_l")]<0&&rig.TwistJoint[rig.Find("index_01_l")]<0&&rig.Joint[rig.Find("ik_hand_gun")]<0,"fingers and helpers ride their parents");
            bool threw=false;try{var bad=NativeVisualRig.Mannequin(new[]{"pelvis","root"},new[]{1,-1},new Vector3[2],new[]{Quaternion.Identity,Quaternion.Identity});new NativeVisualRig.Rig(bad);}catch(ArgumentException){threw=true;}
            Check(threw,"a bone before its parent is refused");
        }

        private sealed class Stats { public int Poses, Planted, LegShort, Supported, SupportShort, Gripped; public float Support, Leg, Grip, GripSum; public readonly System.Collections.Generic.Dictionary<string,int> Short=new System.Collections.Generic.Dictionary<string,int>(); }
        private static Stats last;
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
                    Verify(rig,state,pose,clip.Name,true,set==2,stats);
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
                        if(i%6==0) Verify(rig,state,pose,w+" "+scene,!a.Rolling(t),true,stats);
                        if(scene=="aim"&&i>60) Check(NativeVisualRig.HandsTogether(pose)==1,w+" aimed: the game's hands hold together, so the support grip is on");
                    }
                }
            Check(melee>200&&stats.Poses-melee>300,"enough captured and gun poses ("+melee+" + "+(stats.Poses-melee)+")");
            maxSupport=stats.Support;maxLeg=stats.Leg;maxGrip=stats.Grip;last=stats;
            return stats.Poses;
        }

        private static void Verify(NativeVisualRig.Rig rig,NativeVisualRig.State state,CapturedRig.Pose pose,string name,bool plant,bool twoHanded,Stats stats)
        {
            int pelvis=rig.Of[CapturedRig.Pelvis];int[][] limbs=Limbs;stats.Poses++;
            {
                {
                    NativeVisualRig.Pose(state,pose,0,false,false);
                    foreach(var l in limbs)
                    {
                        Vector3 game=pose.WorldPosition[l[1]]-pose.WorldPosition[l[0]],vis=state.Position[rig.Of[l[1]]]-state.Position[rig.Of[l[0]]];
                        Check(Angle(game,vis)<2e-3f,name+" "+CapturedRig.Bones[l[0]].Name+" follows the game's bone direction");
                    }
                    foreach(int j in new[]{CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LHand,CapturedRig.RHand,CapturedRig.LFoot,CapturedRig.RFoot})
                        Check(Near(state.Turn[rig.Of[j]],pose.WorldRotation[j],2e-4f),name+" "+CapturedRig.Bones[j].Name+" carries the game joint's turn");
                    Check(Near(state.Position[pelvis]-rig.PelvisRest,(pose.WorldPosition[CapturedRig.Pelvis]-rig.GamePelvisRest)*rig.LegRatio,1e-4f),name+" the root moves by the leg ratio");
                    for(int v=0;v<rig.Count;v++) if(rig.TwistJoint[v]>=0)
                    {
                        Quaternion rel=Quaternion.Inverse(state.Turn[rig.Parent[v]])*state.Turn[v];
                        Check(Near(rel,CapturedRig.Rot(CapturedRig.Y,rig.TwistShare[v]*NativeVisualRig.Twist(pose.Rotation[rig.TwistJoint[v]])),2e-4f),name+" "+rig.Names[v]+" takes its share of the roll");
                    }
                    // Planted feet and the support grip.
                    NativeVisualRig.Pose(state,pose,1,plant,false);
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
                            if(gb.Length()>.02f) Check(Vector3.Dot(gb,vb)>0,name+" the knee bends to the game knee's side");
                        }
                    }
                    if(twoHanded&&state.SupportWeight>0){stats.Support=Math.Max(stats.Support,state.LeftArmReach); stats.Supported++; if(state.LeftArmReach>=.999f){stats.SupportShort++;string key=name.StartsWith("Pistol")||name.StartsWith("Rifle")||name.StartsWith("MachineGun")?name:"greatsword";stats.Short[key]=(stats.Short.TryGetValue(key,out int c)?c:0)+1;}}
                    Vector3 hand=state.Position[rig.Of[CapturedRig.LHand]],right=state.Position[rig.Of[CapturedRig.RHand]];
                    if(state.SupportWeight>=.9999f&&state.LeftArmReach<.999f) Check(Near(hand,right+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],1e-3f),name+" the support hand keeps the game's wrist offset");
                    Check(Near(state.World[rig.Of[CapturedRig.LHand]],pose.WorldRotation[CapturedRig.LHand]*rig.AlignedWorld[rig.Of[CapturedRig.LHand]],2e-4f),name+" the support hand keeps the game wrist turn");
                    NativeVisualRig.Grip(state,pose,out var gp,out var gq);
                    var socket=NativeKillBoxes.RightHandSocket;Vector3 offset=Vector3.Transform(new Vector3(socket[0],socket[1],socket[2]),pose.WorldRotation[CapturedRig.RHand]);
                    Check(Near(gp,right+offset,1e-5f)&&Near(gq,pose.WorldRotation[CapturedRig.RHand],1e-6f),name+" the grip is the game socket on the visual wrist");
                    float gd=Vector3.Distance(gp,pose.WorldPosition[CapturedRig.RHand]+offset); stats.Grip=Math.Max(stats.Grip,gd); stats.GripSum+=gd; stats.Gripped++;
                    // The locals rebuild the world pose.
                    Quaternion[] w2=new Quaternion[rig.Count];Vector3[] p2=new Vector3[rig.Count];
                    for(int v=0;v<rig.Count;v++){int p=rig.Parent[v];w2[v]=p<0?state.Local[v]:w2[p]*state.Local[v];p2[v]=p<0?state.LocalPosition[v]:p2[p]+Vector3.Transform(state.LocalPosition[v],w2[p]);}
                    for(int v=0;v<rig.Count;v++) Check(Near(w2[v],state.World[v],2e-4f)&&Near(p2[v],state.Position[v],2e-4f),name+" "+rig.Names[v]+" locals rebuild the pose");
                }
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
            for(int i=0;i<1000;i++){NativeVisualRig.Pose(state,pose,i%2,i%3!=0,i%5==0);NativeVisualRig.Grip(state,pose,out _,out _);}
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
