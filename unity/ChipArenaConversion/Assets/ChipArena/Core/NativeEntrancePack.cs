using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Presentation only: a weapon signature's pilot motion from the owner's Explosive pack (owner 2026-10-12: "Sword and
    /// greatsword intro animations need to change (The player animations)"): one continuous retargeted track over the 4.8 s
    /// phase (tools/export_pack_intros.mjs, NativePackIntros.Generated), sampled on the entrance's presentation clock. The
    /// torso, legs and root come from the capture (the root carries the capture's heading and hips travel); the arms reach
    /// the captured grip with the weapon along the captured blade through the rig's own IK, the support hand on the
    /// two-handed grip as in play (CapturedRig.Sample), the blade kept out of the body (MeleeClearance) and the floor, the
    /// forearms and hands out of the torso by a correction planned over the whole entrance, a hand pressed to the floor (a
    /// cast's slam) resting on it; the magic schools' staff rides the left hand (the pack's casts read mirrored), the right
    /// hand casting; the feet are planted with leg IK
    /// on the floor spots they land on through each contact the exporter found, so nothing slides. Every pose is a pure
    /// function of time; allocation-free per frame once the plan (spots, body sink, arm correction) is built on the first
    /// pose (main thread only).
    /// </summary>
    public sealed class NativePackIntro
    {
        public readonly string Style;public readonly CapturedClip Track;public readonly float Hit;
        /// <summary>The support hand's grip in the weapon hand's frame (the gameplay socket) and whether it holds it throughout
        /// (otherwise while the capture holds the weapon in both hands); TwoHand when there is one.</summary>
        public readonly Vector3 Support;public readonly bool SupportAlways,TwoHand;
        /// <summary>Foot contacts (entrance s): [land, lift] pairs per foot.</summary>
        public readonly float[] PlantsLeft,PlantsRight;
        /// <summary>The held hero pose breathes from the name card (NativeEntrancePose.Idle; a hold the capture keeps still).</summary>
        public readonly bool Breathe;
        public NativePackIntro(string style,CapturedClip track,float[] support,float hit,float[] plantsLeft,float[] plantsRight,bool breathe)
        {
            Style=style;Track=track;Hit=hit;PlantsLeft=plantsLeft;PlantsRight=plantsRight;Breathe=breathe;
            if(support!=null&&support.Length>=4){TwoHand=true;Support=new Vector3(support[0],support[1],support[2]);SupportAlways=support[3]>.5f;}
        }
        /// <summary>The entrance's pack motion, or null (it keeps its baked clip).</summary>
        public static NativePackIntro For(string style){foreach(var p in NativePackIntros.All)if(p.Style==style)return p;return null;}
        /// <summary>The root's height: with straight legs and the body at 0 the ankle is .795 under it, its sole .07 under the ankle.</summary>
        public const float RootHeight=NativeEntranceStage.FloorY+NativeEntrancePose.Ankle+.795f;
        /// <summary>How long a foot takes to settle onto (and come off) its planted spot (s).</summary>
        public const float PlantRamp=.06f;
        private readonly float[] row=new float[CapturedClip.Channels];
        private float[] spotsLeft,spotsRight;private readonly CapturedRig.Pose planPose=new CapturedRig.Pose();
        private static readonly int[] Bones={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LFoot,CapturedRig.RFoot};
        private static Vector3 Row(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rotation(float[] f,int i){var v=Row(f,i);float a=v.Length();return a<1e-8f?Quaternion.Identity:Quaternion.CreateFromAxisAngle(v/a,a);}
        /// <summary>The capture's own pose at `t` (feet unplanted; the arm correction once planned) and its root.</summary>
        public void Captured(float t,CapturedRig.Pose p,out Vector3 root,out Quaternion rotation,float drop=0)
        {
            if(!MathUtil.Finite(t))t=0;Track.Sample(t,row);var f=row;Arms(t,out var fixRight,out var fixLeft);
            p.Reset();p.Position[CapturedRig.Body]=new Vector3(0,f[MeleeCapture.By]-drop,0);p.Rotation[CapturedRig.Pelvis]=Rotation(f,MeleeCapture.Pelvis);
            for(int i=0;i<Bones.Length;i++)p.Rotation[Bones[i]]=Rotation(f,MeleeCapture.Spine+i*3);
            p.Rotation[CapturedRig.LKnee]=Quaternion.CreateFromAxisAngle(Vector3.UnitX,Math.Max(0,f[MeleeCapture.LKnee]));p.Rotation[CapturedRig.RKnee]=Quaternion.CreateFromAxisAngle(Vector3.UnitX,Math.Max(0,f[MeleeCapture.RKnee]));
            // A still hero hold breathes from the name card (heroIdle, as the procedural entrances; the arms ride the chest).
            if(Breathe)NativeEntrancePose.Idle(p,t,NativeEntranceScript.NameAt);
            // The arms as CapturedRig.Sample poses them: the weapon hand on the captured grip, the weapon along the captured blade.
            Vector3 blade=CapturedRig.Unit(Row(f,MeleeCapture.Blade),Vector3.UnitZ),edge=Row(f,MeleeCapture.Edge);edge=CapturedRig.Unit(edge-blade*Vector3.Dot(blade,edge),Vector3.UnitX);var grip=Row(f,MeleeCapture.Grip)+fixRight;
            // The blade and hilt kept out of the voxel head, pauldrons and torso exactly as in play (MeleeCapture.Apply's clearance).
            var set=Melee;if(set!=null&&set.Fit.Clear!=MeleeClear.None){melee.ReadPose(p);MeleeClearance.Shared.Clear(melee,ref grip,ref blade,ref edge,-set.Fit.Pommel,set.BladeEnd,true,set.Fit.HalfWidth,false);}
            var right=CapturedRig.Basis(edge,Vector3.Cross(blade,edge),blade);
            // Two-handed, the grip comes toward the support shoulder by whatever the support hand falls short of it (the
            // retarget's own rule, again after the clearance and the floor guard have moved the blade).
            for(int pass=0;pass<(TwoHand?3:1);pass++)
            {
                CapturedRig.Arm(p,false,grip,right,Row(f,MeleeCapture.RPole));HandOffFloor(p,false,ref grip,right,Row(f,MeleeCapture.RPole));
                if(Blade>0)BladeOffFloor(p);
                Vector3 left=Row(f,MeleeCapture.LHand)+fixLeft;var leftQ=CapturedRig.Look(Row(f,MeleeCapture.LFwd),Row(f,MeleeCapture.LUp));
                if(!TwoHand){CapturedRig.Arm(p,true,left,leftQ,Row(f,MeleeCapture.LPole));HandOffFloor(p,true,ref left,leftQ,Row(f,MeleeCapture.LPole));break;}
                // The support hand on the reached weapon hand's support grip, as the gameplay socket holds it.
                p.World();var chest=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);var reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],chest);
                var held=Quaternion.Normalize(chest*p.WorldRotation[CapturedRig.RHand]);
                float weight=SupportAlways?1:CapturedClip.Clamp(f[MeleeCapture.TwoHand],0,1);left=Vector3.Lerp(left,reached+Vector3.Transform(Support,held),weight);leftQ=Quaternion.Slerp(leftQ,held,weight);
                CapturedRig.Arm(p,true,left,leftQ,Row(f,MeleeCapture.LPole));
                p.World();var miss=left-Vector3.Transform(p.WorldPosition[CapturedRig.LHand]-p.WorldPosition[CapturedRig.Chest],chest);if(miss.LengthSquared()<1e-6f||weight<.5f)break;grip-=miss*1.05f;
            }
            root=new Vector3(f[MeleeCapture.Yaw+1],RootHeight,f[MeleeCapture.Yaw+3]);rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,f[MeleeCapture.Yaw]);
        }
        /// <summary>The gameplay melee set of the weapon (its blade, pommel and clearance), or null.</summary>
        public MeleeSet Melee{get{if(!meleeKnown){meleeSet=MeleeSet.For(Style.StartsWith("weapon-",StringComparison.Ordinal)?Style.Substring(7):"");meleeKnown=true;}return meleeSet;}}
        private MeleeSet meleeSet;private bool meleeKnown;private readonly MeleeRig melee=new MeleeRig();
        /// <summary>The melee blade's length past the wrist (MeleeSet blade end; 0: no floor guard, as for the shield and guns).</summary>
        public float Blade=>Melee!=null&&Melee.Fit.Clear!=MeleeClear.None?Melee.BladeEnd:0;
        /// <summary>The floor top in the rig frame (the root stands RootHeight above it, turned only about +y).</summary>
        public const float RigFloor=NativeEntranceStage.FloorY-RootHeight;
        // A hand pressed to the floor (a slam, a touch) rests on it instead of sinking in: the arm reaches higher by what the
        // hand's box sinks under the floor top.
        private static readonly int RightHandBox=HandBox(CapturedRig.RHand),LeftHandBox=HandBox(CapturedRig.LHand);
        private static int HandBox(int bone){for(int i=0;i<CapturedRig.Boxes.Length;i++)if(CapturedRig.Boxes[i].Bone==bone)return i;return -1;}
        private static void HandOffFloor(CapturedRig.Pose p,bool left,ref Vector3 target,Quaternion wrist,Vector3 pole)
        {
            int box=left?LeftHandBox:RightHandBox;if(box<0)return;
            for(int pass=0;pass<2;pass++)
            {
                p.World();var b=CapturedRig.Boxes[box];var q=p.WorldRotation[b.Bone];var c=p.WorldPosition[b.Bone]+Vector3.Transform(b.Position,q);
                float reach=(Math.Abs(Vector3.Transform(Vector3.UnitX,q).Y)*b.Size.X+Math.Abs(Vector3.Transform(Vector3.UnitY,q).Y)*b.Size.Y+Math.Abs(Vector3.Transform(Vector3.UnitZ,q).Y)*b.Size.Z)*.5f;
                float sink=RigFloor+.004f-(c.Y-reach);if(sink<=0)return;
                target+=Vector3.Transform(Vector3.UnitY,Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]))*sink;CapturedRig.Arm(p,left,target,wrist,pole);
            }
        }
        // The blade never dips under the floor: the wrist turns up about the horizontal axis across the blade just enough to
        // rest the tip on it (CapturedRig.BladeFloor at the entrance floor), so a slam bites the floor instead of sinking in.
        private void BladeOffFloor(CapturedRig.Pose p)
        {
            p.World();int h=CapturedRig.RHand;var wrist=p.WorldPosition[h];var d=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[h]);const float floor=RigFloor+.015f;
            float len=Blade;if(wrist.Y+d.Y*len>=floor||wrist.Y<=floor)return;
            float want=CapturedClip.Clamp((floor-wrist.Y)/len,-1,1),angle=(float)(Math.Asin(want)-Math.Asin(CapturedClip.Clamp(d.Y,-1,1)));
            var axis=CapturedRig.Unit(Vector3.Cross(d,Vector3.UnitY),Vector3.UnitX);var world=Quaternion.CreateFromAxisAngle(axis,angle)*p.WorldRotation[h];
            p.Rotation[h]=Quaternion.Normalize(Quaternion.Inverse(p.WorldRotation[CapturedRig.Bones[h].Parent])*world);
        }
        /// <summary>The pilot at presentation time `t`: the capture with its feet planted on their landing spots.</summary>
        public void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            if(spotsLeft==null){spotsLeft=Spots(PlantsLeft,true);spotsRight=Spots(PlantsRight,false);drops=Drops();PlanArms();}
            if(!MathUtil.Finite(t))t=0;Planted(t,pose,out root,out rotation,Drop(t));
        }
        // The capture with the body lowered by `drop`, a free foot kept above the floor and the planted feet on their spots.
        private void Planted(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation,float drop)
        {
            Captured(t,pose,out root,out rotation,drop);pose.World();
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float w=Weight(left?PlantsLeft:PlantsRight,t,out int k);
                if(w>.001f)
                {
                    var s=left?spotsLeft:spotsRight;NativeEntrancePose.PlantFoot(pose,root,rotation,left,s[k*3],s[k*3+1],s[k*3+2],w);pose.World();
                    // A deep crouch over a planted foot rises onto its toes rather than pressing the voxel shin into the floor:
                    // the heel lifts by what the knee and shin boxes sink and the foot pitches down onto its toe.
                    int knee=left?CapturedRig.LKnee:CapturedRig.RKnee;float sink=NativeEntranceStage.FloorY+.003f-Lowest(pose,root,knee,knee);
                    if(sink>0)
                    {
                        float heel=Math.Min(MaxHeel,sink*1.15f);NativeEntrancePose.PlantFoot(pose,root,rotation,left,s[k*3],s[k*3+1],s[k*3+2],w,heel);
                        int foot=left?CapturedRig.LFoot:CapturedRig.RFoot;pose.Rotation[foot]=pose.Rotation[foot]*Quaternion.CreateFromAxisAngle(Vector3.UnitX,w*ToePitch(heel));pose.World();
                    }
                }
                if(w<.999f)
                {
                    // A swinging (or lifting) foot that the voxel leg carries lower than the capture's never digs into the floor: as
                    // its sole or toe reaches the floor it is laid flat on it where it is.
                    // A knee carried into the floor the same way (a lunge's back leg) puts that foot down on its toes under it.
                    int foot=left?CapturedRig.LFoot:CapturedRig.RFoot;float depth=NativeEntranceStage.FloorY-Sole(pose,root,left),knee=NativeEntranceStage.FloorY+.003f-KneeLow(pose,root,left);
                    float c=NativeEntranceMath.Smooth(Math.Max((depth+.01f)/.02f,knee/.015f));
                    if(c>0)
                    {
                        var ankle=root+Vector3.Transform(pose.WorldPosition[foot],rotation);var f=Vector3.Transform(Vector3.UnitZ,rotation*pose.WorldRotation[foot]);float heel=knee>0?Math.Min(MaxHeel,knee*1.15f):0;
                        NativeEntrancePose.PlantFoot(pose,root,rotation,left,ankle.X,ankle.Z,(float)Math.Atan2(f.X,f.Z),c,heel);if(heel>0)pose.Rotation[foot]=pose.Rotation[foot]*Quaternion.CreateFromAxisAngle(Vector3.UnitX,c*ToePitch(heel));pose.World();
                    }
                }
            }
        }
        private const float MaxHeel=.1f;
        // The pitch (rad, toe down) that keeps the toe's front edge on the floor with the heel `heel` m up: the toe's front
        // bottom edge sits .245 m ahead of and .07 m under the ankle (CapturedRig toe box), so -.07 cos a - .245 sin a = -(.07 + heel).
        private static float ToePitch(float heel)
        {
            float a=0;for(int i=0;i<6;i++){float f=-.07f*(float)Math.Cos(a)-.245f*(float)Math.Sin(a)+.07f+heel,d=.07f*(float)Math.Sin(a)-.245f*(float)Math.Cos(a);a-=f/d;}
            return a;
        }
        // ---- arm clearance: a correction planned once over the whole entrance, smooth in time (NativeIntroMotion's plan) ----
        // The voxel torso and head are blockier than the captured human's: where a forearm, hand or the held weapon sinks into
        // them, the hand target (chest space; two-handed, the grip, which the support hand follows) moves out along the way out
        // of the body. Six passes at 60 Hz, each smoothed over +-5 samples and capped at .2 m.
        private Vector3[] armRight,armLeft;private const int ArmRate=60;
        private void Arms(float t,out Vector3 right,out Vector3 left)
        {
            right=left=Vector3.Zero;var r=armRight;if(r==null)return;float x=Math.Max(0,Math.Min(r.Length-1.001f,t*ArmRate));int i=(int)x;float u=x-i;
            right=Vector3.Lerp(r[i],r[i+1],u);left=Vector3.Lerp(armLeft[i],armLeft[i+1],u);
        }
        private void PlanArms()
        {
            int n=(int)(NativeEntranceScript.Duration*ArmRate)+2;var right=new Vector3[n];var left=new Vector3[n];var rawRight=new Vector3[n];var rawLeft=new Vector3[n];
            for(int pass=0;pass<6;pass++)
            {
                armRight=right;armLeft=left;
                for(int i=0;i<n;i++)
                {
                    float t=i/(float)ArmRate;Captured(t,planPose,out _,out _,Drop(t));planPose.World();var chest=Quaternion.Inverse(planPose.WorldRotation[CapturedRig.Chest]);
                    var pr=Vector3.Transform(NativeIntroMotion.BodyPush(Style,planPose,false),chest);var pl=Vector3.Transform(NativeIntroMotion.BodyPush(Style,planPose,true),chest);
                    if(TwoHand){rawRight[i]=right[i]+.9f*(pr+pl);rawLeft[i]=left[i];}else{rawRight[i]=right[i]+.9f*pr;rawLeft[i]=left[i]+.9f*pl;}
                }
                right=Blur(rawRight);left=Blur(rawLeft);
            }
            armRight=right;armLeft=left;
        }
        private static Vector3[] Blur(Vector3[] from)
        {
            int n=from.Length;var into=new Vector3[n];
            for(int i=0;i<n;i++){var sum=Vector3.Zero;float w=0;for(int k=-5;k<=5;k++){int j=Math.Max(0,Math.Min(n-1,i+k));float g=(float)Math.Exp(-k*k/4.5);sum+=from[j]*g;w+=g;}var v=sum/w;float l=v.Length();into[i]=l>.2f?v*(.2f/l):v;}
            return into;
        }
        /// <summary>The lowest point (world y) of foot `left` (its foot and toe boxes; `pose` world current).</summary>
        public static float Sole(CapturedRig.Pose p,Vector3 root,bool left)=>Lowest(p,root,left?CapturedRig.LFoot:CapturedRig.RFoot,left?CapturedRig.LToe:CapturedRig.RToe);
        /// <summary>The lowest point (world y) of knee `left`'s boxes (the shin; `pose` world current).</summary>
        public static float KneeLow(CapturedRig.Pose p,Vector3 root,bool left){int k=left?CapturedRig.LKnee:CapturedRig.RKnee;return Lowest(p,root,k,k);}
        /// <summary>The lowest point (world y) of every body box but the feet (`pose` world current).</summary>
        public static float LowestAboveFeet(CapturedRig.Pose p,Vector3 root)=>Lowest(p,root,-1,-1);
        // The lowest box point of bones `a` and `b` (or, with -1, of every bone but the feet and toes).
        private static float Lowest(CapturedRig.Pose p,Vector3 root,int a,int b2)
        {
            float min=float.MaxValue;
            foreach(var b in CapturedRig.Boxes)
            {
                bool foot=b.Bone==CapturedRig.LFoot||b.Bone==CapturedRig.RFoot||b.Bone==CapturedRig.LToe||b.Bone==CapturedRig.RToe;
                if(a<0?foot:b.Bone!=a&&b.Bone!=b2)continue;
                var q=p.WorldRotation[b.Bone];var c=p.WorldPosition[b.Bone]+Vector3.Transform(b.Position,q);
                float r=(Math.Abs(Vector3.Transform(Vector3.UnitX,q).Y)*b.Size.X+Math.Abs(Vector3.Transform(Vector3.UnitY,q).Y)*b.Size.Y+Math.Abs(Vector3.Transform(Vector3.UnitZ,q).Y)*b.Size.Z)*.5f;
                min=Math.Min(min,root.Y+c.Y-r);
            }
            return min;
        }
        /// <summary>How planted a foot is at `t` (0..1) and which contact (`k`) holds it.</summary>
        public static float Weight(float[] plants,float t,out int k)
        {
            k=0;float best=0;
            for(int i=0;i+1<plants.Length;i+=2)
            {
                float land=plants[i],lift=plants[i+1];float w=t<land?NativeEntranceMath.Ramp(t,land-PlantRamp,land):t>lift?1-NativeEntranceMath.Ramp(t,lift,lift+PlantRamp):1;
                if(land<=0&&t<=land)w=1;if(lift>=NativeEntranceScript.Duration-1e-3f&&t>=lift)w=1;
                if(w>best){best=w;k=i/2;}
            }
            return best;
        }
        /// <summary>How far the body sinks at `t` so every planted leg reaches its spot (the voxel legs are shorter than a lunge asks).</summary>
        public float Drop(float t){var d=drops;if(d==null)return 0;float x=Math.Max(0,Math.Min(d.Length-1.001f,t*DropRate));int i=(int)x;return d[i]+(d[i+1]-d[i])*(x-i);}
        private float[] drops;private const int DropRate=120;private const float Reach=.49f,MaxDrop=.06f;
        // The sink at 120 Hz: per planted foot the drop that brings its hip within reach of the spot (weighted by how planted it
        // is), widened by .08 s each way and then smoothed over the same span, so it never falls short of a need and never jolts.
        private float[] Drops()
        {
            int n=(int)(NativeEntranceScript.Duration*DropRate)+2;var need=new float[n];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)DropRate;Captured(t,planPose,out var root,out var rotation);planPose.World();
                for(int side=0;side<2;side++)
                {
                    bool left=side==0;float w=Weight(left?PlantsLeft:PlantsRight,t,out int k);if(w<=.001f)continue;var s=left?spotsLeft:spotsRight;
                    var hip=root+Vector3.Transform(planPose.WorldPosition[left?CapturedRig.LHip:CapturedRig.RHip],rotation);var target=new Vector3(s[k*3],NativeEntranceStage.FloorY+NativeEntrancePose.Ankle,s[k*3+1]);
                    float hd=new Vector2(hip.X-target.X,hip.Z-target.Z).Length(),vd=hip.Y-target.Y;if(hd>=Reach)hd=Reach-.001f;
                    float dy=vd-(float)Math.Sqrt(Reach*Reach-hd*hd);if(dy>0)need[i]=Math.Max(need[i],dy*w);
                }
            }
            for(int i=0;i<n;i++)need[i]=Math.Min(need[i],MaxDrop);return Envelope(need);
        }
        // A need over time made smooth without falling short of it: widened by .08 s each way, then averaged over that span.
        private static float[] Envelope(float[] need)
        {
            int n=need.Length;const int span=10;var wide=new float[n];var smooth=new float[n];
            for(int i=0;i<n;i++){float m=0;for(int j=Math.Max(0,i-span);j<=Math.Min(n-1,i+span);j++)m=Math.Max(m,need[j]);wide[i]=m;}
            for(int i=0;i<n;i++){float sum=0,wsum=0;for(int j=-span;j<=span;j++){int k=Math.Max(0,Math.Min(n-1,i+j));float w=1-Math.Abs(j)/(span+1f);sum+=wide[k]*w;wsum+=w;}smooth[i]=sum/wsum;}
            return smooth;
        }
        // Each contact's spot: where the capture's ankle lands (world x, z) and the foot's heading there.
        private float[] Spots(float[] plants,bool left)
        {
            var s=new float[plants.Length/2*3];
            for(int i=0;i+1<plants.Length;i+=2)
            {
                float at=plants[i]+Math.Min(.02f,(plants[i+1]-plants[i])*.5f);Captured(at,planPose,out var root,out var rotation);planPose.World();
                int foot=left?CapturedRig.LFoot:CapturedRig.RFoot;var p=root+Vector3.Transform(planPose.WorldPosition[foot],rotation);var f=Vector3.Transform(Vector3.UnitZ,rotation*planPose.WorldRotation[foot]);
                s[i/2*3]=p.X;s[i/2*3+1]=p.Z;s[i/2*3+2]=(float)Math.Atan2(f.X,f.Z);
            }
            return s;
        }
    }
}
