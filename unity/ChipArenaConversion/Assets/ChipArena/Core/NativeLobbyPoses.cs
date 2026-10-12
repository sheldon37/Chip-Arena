using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Where a lobby pose puts one held weapon: shown or not, the wrist it hangs from, and its frame relative to that wrist
    // (source frame). The source weapon meshes are exported in that frame (tools/export_models.mjs zeroes the hand socket),
    // so the Unity boundary draws a weapon at wrist * (Position, Rotation), mirrored once.
    public struct LobbyHeld
    {
        public bool Visible;public int Bone;public Vector3 Position;public Quaternion Rotation;
    }
    // The lobby idle poses (client/lobby-poses.ts animateLobbyPose at 05950ea, with the rig-ik and fighter-motion limb
    // solves it calls) over the captured rig's 22 joints in the source frame (+x the rig's left). Presentation only: the
    // portraits never sample live attacks or damage, and nothing here is read by the authority or the network frame.
    //  weapon  'Standing with weapon': the hero stance per weapon over a contrapposto weight shift (heroStance). The fighters
    //          (Emperor Style, Ansatsuken) keep their combat idle; the Siren stands in her own idle (menu-portraits.ts).
    //  chair   'Sitting on chair', sleep 'Sleeping' (curled on the right side, the lower hand under the head by IK),
    //  crossed 'Arms crossed' (contrapposto, forearms stacked by IK).
    // Every pose except the fighters' and the Siren's sets all 22 joints from rest (the source's base sample is fully
    // overwritten; checked against the source in Tests/LobbyPoseChecks.cs), so Sample starts from the rest pose.
    public static class NativeLobbyPoses
    {
        public const int Weapon=0,Chair=1,Sleep=2,Crossed=3;
        // shared/lobby-presentation.ts LOBBY_POSES (keys and labels, in the source order) and main.ts:131 lobbyPoseOptions icons.
        public static readonly string[] Keys={"weapon","chair","sleep","crossed"};
        public static readonly string[] Labels={"Standing with weapon","Sitting on chair","Sleeping","Arms crossed"};
        public static readonly string[] Icons={"person-standing","armchair","moon","hand"};
        // lobbyPose(): a known key, else 'weapon'.
        public static int Index(string value){for(int i=0;i<Keys.Length;i++)if(value==Keys[i])return i;return Weapon;}
        public static string Normalize(string value)=>Keys[Index(value)];
        // menu-portraits.ts render(): the Siren always stands.
        public static int Effective(string spec,string pose)=>spec=="siren"?Weapon:Index(pose);
        public static bool Fighter(string spec)=>spec=="emperor"||spec=="ansatsuken";
        public static bool OffHand(string spec)=>spec=="daggers"||spec=="emperor"||spec=="ansatsuken";
        public static bool Magic(string spec)=>spec=="fire"||spec=="water"||spec=="lightning"||spec=="darkness"||spec=="leet";
        public static bool Gun(string spec)=>spec=="pistol"||spec=="rifle"||spec=="machinegun";
        // The pose keeps the weapon's own idle (the caller samples it into the rig first): the fighters' standing pose is
        // their combat idle (heroStance returns for them), and the Siren is animated as herself.
        public static bool UsesIdle(string spec,int pose)=>pose==Weapon&&(Fighter(spec)||spec=="siren");
        // character-rig.ts: the hand sockets sit at (0,-.065,.055) in the wrist frame; rig-ik's grip (0,.065,-.055) cancels them.
        public static readonly Vector3 HandSocket=new Vector3(0,-.065f,.055f);
        // weapon-visuals.ts:45: the rifle and machine gun 'support-grip' socket (the left hand's brace point).
        public static readonly Vector3 GunSupportGrip=new Vector3(.025f,-.03f,.10f);
        // rig-ik.ts STANDING_REACH: the locked standing leg of the lobby contrapposto.
        public const float StandingReach=.497f;
        private const int Body=CapturedRig.Body,Pelvis=CapturedRig.Pelvis,Spine=CapturedRig.Spine,Chest=CapturedRig.Chest,Neck=CapturedRig.Neck,Head=CapturedRig.Head,
            LClav=CapturedRig.LClav,LArm=CapturedRig.LArm,LElbow=CapturedRig.LElbow,LHand=CapturedRig.LHand,RClav=CapturedRig.RClav,RArm=CapturedRig.RArm,RElbow=CapturedRig.RElbow,RHand=CapturedRig.RHand,
            LHip=CapturedRig.LHip,LKnee=CapturedRig.LKnee,LFoot=CapturedRig.LFoot,LToe=CapturedRig.LToe,RHip=CapturedRig.RHip,RKnee=CapturedRig.RKnee,RFoot=CapturedRig.RFoot,RToe=CapturedRig.RToe;
        private static readonly Vector3 Down=new Vector3(0,-1,0);
        // lobby-poses.ts SLEEP_PILLOW / SLEEP_PILLOW_POLE (rig-group space).
        private static readonly Vector3 SleepPillow=new Vector3(-.24f,-.80f,.2f),SleepPillowPole=new Vector3(.3f,.4f,1);

        // Poses `r` for `spec` in lobby pose `pose` at portrait time `time` (0 with reduced motion) and reports the held
        // weapons. For UsesIdle the caller has sampled the weapon's idle into `r`; it is kept. Otherwise `r` starts at rest.
        public static void Sample(MeleeRig r,string spec,int pose,float time,out LobbyHeld weapon,out LobbyHeld offhand)
        {
            bool magic=Magic(spec),idle=UsesIdle(spec,pose);
            // lobby-poses.ts:15: the weapon shows only in the standing pose; the off hand only for daggers and the fighters.
            weapon=new LobbyHeld{Visible=pose==Weapon&&spec!="siren",Bone=magic?LHand:RHand,Position=magic||Fighter(spec)?HandSocket:Vector3.Zero,Rotation=Quaternion.Identity};
            offhand=new LobbyHeld{Visible=pose==Weapon&&OffHand(spec),Bone=LHand,Position=Fighter(spec)?HandSocket:Vector3.Zero,Rotation=Quaternion.Identity};
            if(idle)return;
            r.Reset();r.WeaponReach=1;
            float breath=(float)Math.Sin(time*1.65),sway=(float)Math.Sin(time*.72);
            if(pose==Weapon){HeroStance(r,spec,breath,sway);return;}
            r.P[Body]=Vector3.Zero;r.SetE(Body,0,0,0);r.SetE(Pelvis,0,0,0);r.SetE(Spine,0,0,0);r.SetE(Chest,0,0,0);r.SetE(Neck,0,0,0);r.SetE(Head,0,0,0);
            r.SetE(LClav,0,0,.03f);r.SetE(RClav,0,0,-.03f);r.SetE(LHand,0,0,0);r.SetE(RHand,0,0,0);
            r.SetE(LHip,0,0,.08f);r.SetE(RHip,0,0,-.08f);r.SetE(LKnee,.12f,0,0);r.SetE(RKnee,.12f,0,0);r.SetE(LFoot,-.12f,0,0);r.SetE(RFoot,-.12f,0,0);r.SetE(LToe,0,0,0);r.SetE(RToe,0,0,0);
            if(pose==Chair)
            {
                r.P[Body]=new Vector3(r.P[Body].X,-.19f+breath*.003f,r.P[Body].Z);X(r,Body,-.08f);X(r,Spine,.06f+breath*.014f);r.SetE(Head,.03f+breath*.012f,sway*.065f,-.025f);
                r.SetE(LHip,-1.44f,.04f,.12f);r.SetE(RHip,-1.39f,-.04f,-.14f);X(r,LKnee,1.52f);X(r,RKnee,1.48f);X(r,LFoot,.02f);X(r,RFoot,.02f);
                r.SetE(LArm,-.18f,0,.16f);r.SetE(RArm,-.22f,0,-.16f);r.SetE(LElbow,-1.28f,0,0);r.SetE(RElbow,-1.20f,0,0);X(r,LHand,-.16f);X(r,RHand,-.16f);
            }
            else if(pose==Sleep)
            {
                // Curled on the right side facing the camera; the lower hand is placed under the head by IK.
                r.P[Body]=new Vector3(.24f,-.52f,-.05f);r.SetE(Body,0,0,1.28f);r.SetE(Pelvis,.05f,0,.04f);r.SetE(Spine,.16f+breath*.014f,0,-.03f);r.SetE(Chest,.08f+breath*.015f,0,-.04f);r.SetE(Neck,.12f,0,.40f);r.SetE(Head,.06f,.1f+breath*.02f+sway*.015f,.36f);
                r.SetE(LHip,-.95f,0,.22f);r.SetE(RHip,-.62f,0,.14f);X(r,LKnee,1.35f);X(r,RKnee,1.05f);X(r,LFoot,.35f);X(r,RFoot,.25f);
                r.SetE(RArm,-2.55f,0,-.35f);r.SetE(RElbow,-1.75f,0,0);r.SetE(RHand,0,0,0);r.SetE(LArm,-.42f,0,.05f+breath*.01f);r.SetE(LElbow,-.55f,0,0);r.SetE(LHand,0,0,-.1f);
                PlaceHand(r,false,SleepPillow,SleepPillowPole);
            }
            else
            {
                // Weight on one leg, forearms stacked with the hands resting on the opposite biceps.
                r.SetE(Body,.015f,-.10f,sway*.009f);Contrapposto(r,breath);X(r,Spine,r.E[Spine].X+breath*.012f);r.SetE(Head,-.055f,sway*.075f+.1f,.03f);
                r.SetE(LArm,-.75f,0,-.6f);r.SetE(RArm,-.72f,0,.6f);r.SetE(LElbow,-1.5f,0,0);r.SetE(RElbow,-1.5f,0,0);
                PlaceHand(r,true,new Vector3(-.235f,.04f+breath*.003f,.19f),new Vector3(.45f,-1,.2f));
                PlaceHand(r,false,new Vector3(.235f,.10f+breath*.003f,.235f),new Vector3(-.45f,-1,.25f));
                r.SetE(LHand,.1f,0,-.25f);r.SetE(RHand,-.08f,0,.25f);
            }
        }
        // The same for a pilot with its hat, the portrait's reduced effects and a per-pilot seed: the DEV Cyber Lord stands in
        // its idle stance (Core/CyberLordIdle.cs, after the Sheldon Intro's ending: the hand on the hood or the temple, the
        // breath, the stare) with its slight glitch, unarmed; every other pilot as Sample. `glitch` is the frame's glitch (the
        // renderer's RGB-split ghosts and scanline band), zero for anyone else.
        public static void Sample(MeleeRig r,string spec,int pose,float time,string hat,bool reduced,int seed,out LobbyHeld weapon,out LobbyHeld offhand,out CyberIdleGlitch glitch)
        {
            glitch=default;
            if(pose==Weapon&&CyberLordIdlePose.Handles(spec))
            {
                weapon=new LobbyHeld{Bone=RHand,Rotation=Quaternion.Identity};offhand=new LobbyHeld{Bone=LHand,Rotation=Quaternion.Identity};
                glitch=CyberLordIdlePose.Stand(r,time,CyberLordIdlePose.Hood(hat),seed,reduced);return;
            }
            Sample(r,spec,pose,time,out weapon,out offhand);
        }
        // three's `node.rotation.x = v` (the Euler's other angles kept).
        private static void X(MeleeRig r,int i,float x)=>r.SetE(i,x,r.E[i].Y,r.E[i].Z);
        // contrapposto(): the pelvis settles over a straight weight leg and rolls toward the relaxed one, the chest counter-rolls,
        // the weight foot is planted and the free foot rests forward on its toes (foot IK at standing reach).
        private static void Contrapposto(MeleeRig r,float breath,float shift=1)
        {
            r.P[Body]=new Vector3(-.022f*shift,-.006f+breath*.002f,0);r.SetE(Pelvis,0,.06f*shift,-.035f*shift);r.SetE(Spine,.02f,-.03f*shift,.03f*shift);r.SetE(Chest,-.01f+breath*.01f,-.02f*shift,.012f*shift);r.SetE(Neck,.02f,0,-.02f*shift);
            r.Foot(false,new Vector3(-.13f,-.795f,-.02f),-.12f,0,1,null,StandingReach);r.Foot(true,new Vector3(.19f,-.795f+.03f*shift,.1f),.3f,.32f*shift,1,null,StandingReach);
        }
        private static void Relax(MeleeRig r,bool left,float breath)
        {float s=left?1:-1;r.SetE(left?LArm:RArm,-.06f+breath*.01f,0,s*.13f);r.SetE(left?LElbow:RElbow,-.32f,0,0);r.SetE(left?LHand:RHand,0,0,-s*.06f);}
        // aim(): the rotation from +z to (x,y,z), rolled about its own z, through three's Euler (the hand IK takes an Euler).
        public static Quaternion Aim(float x,float y,float z,float roll=0)
        {
            var q=MeleeMath.FromTo(MeleeMath.Z,Vector3.Normalize(new Vector3(x,y,z)));if(roll!=0)q*=MeleeMath.AxisAngle(MeleeMath.Z,roll);
            return MeleeMath.Euler(MeleeMath.ToEuler(q));
        }
        // heroStance(): presentation-only hero stances for 'Standing with weapon'.
        private static void HeroStance(MeleeRig r,string spec,float breath,float sway)
        {
            r.SetE(Body,0,.12f+sway*.015f,0);r.SetE(Head,-.04f+breath*.012f,sway*.07f-.1f,.02f);r.SetE(LClav,0,0,.02f+breath*.006f);r.SetE(RClav,0,0,-.02f-breath*.006f);
            Contrapposto(r,breath);r.SetE(LHand,0,0,0);r.SetE(RHand,0,0,0);
            float lift=breath*.004f;
            if(spec=="greatsword"){r.BraceWeapon(new Vector3(-.27f,-.04f+lift,.30f),Aim(-.2f,.62f,-.76f,2.1f),1);Relax(r,true,breath);}
            else if(spec=="sword"){r.BraceWeapon(new Vector3(-.31f,-.34f+lift,.19f),Aim(-.38f,-.38f,.84f,-.25f),1);Relax(r,true,breath);}
            else if(spec=="daggers"){r.Hand(false,new Vector3(-.27f,-.33f+lift,.13f),Aim(-.3f,-.55f,.78f,-.4f),1);r.Hand(true,new Vector3(.27f,-.33f+lift,.13f),Aim(.3f,-.55f,.78f,.4f),1);}
            else if(spec=="shield"){r.Hand(false,new Vector3(-.3f,-.33f+lift,.12f),Aim(-.3f,-.12f,.95f),1);Relax(r,true,breath);}
            else if(spec=="pistol"){r.Hand(false,new Vector3(-.3f,-.36f+lift,.09f),Aim(-.32f,-.8f,.5f,-.5f),1);Relax(r,true,breath);}
            else if(Gun(spec)){r.BraceWeapon(new Vector3(-.06f,-.04f+lift,.21f),Aim(.4f,-.5f,.76f),1,null,false);SupportHand(r,GunSupportGrip,1);}
            else if(Magic(spec)){r.Hand(true,new Vector3(.3f,-.2f+lift,.15f),Aim(.05f,1,.08f),1);Relax(r,false,breath);}
        }
        // rig-ik.ts supportHand() for a weapon whose support grip is `grip` (weapon frame = the right wrist, the weapon held
        // at the grip): the left arm reaches it and the left wrist takes the weapon's orientation.
        private static void SupportHand(MeleeRig r,Vector3 grip,float weight)
        {
            if(weight<=.001f)return;Quaternion hq=r.ChainQuat(RHand,-1);Vector3 point=r.ChainPoint(RHand,-1)+Vector3.Transform(r.Reached(grip),hq);
            Vector3 e=Vector3.Transform(point-r.ChainPoint(LClav,-1),MeleeMath.Inverse(r.ChainQuat(LClav,-1)));
            SolveArm(r,true,e,new Vector3(1,-.8f,-.25f),weight,.10f);
            r.Slerp(LHand,MeleeMath.Inverse(r.ChainQuat(LElbow,-1))*hq,weight);
        }
        // rig-ik.ts solveArm (MeleeRig keeps its own private): two-bone arm in the clavicle's space.
        private static void SolveArm(MeleeRig r,bool left,Vector3 end,Vector3 pole,float weight,float min)
        {
            const float UPPER=.27f,FORE=.25f,ARM_MAX=.515f;int arm=left?LArm:RArm,elbow=arm+1;Vector3 s=r.P[arm],d=end-s;float raw=d.Length();d=raw<1e-6f?Down:d/raw;float dd=MeleeMath.Clamp(raw,min,ARM_MAX);
            float along=(UPPER*UPPER-FORE*FORE+dd*dd)/(2*dd),height=(float)Math.Sqrt(Math.Max(0,UPPER*UPPER-along*along));Vector3 p=Bend(d,MeleeMath.Unit(pole));
            float cos=along/UPPER,sin=height/UPPER;Vector3 u=d*cos+p*sin,w=d*sin-p*cos,y=-u,x=Vector3.Cross(y,w);
            Quaternion upper=MeleeMath.Basis(x,y,w);Vector3 f=d*dd-u*UPPER;Quaternion lower=MeleeMath.AxisAngle(MeleeMath.X,-(float)Math.Atan2(Vector3.Dot(f,w),Vector3.Dot(f,u)));
            if(weight>=1){r.SetQ(arm,upper);r.SetQ(elbow,lower);}else{r.Slerp(arm,upper,weight);r.Slerp(elbow,lower,weight);}
        }
        private static Vector3 Bend(Vector3 dir,Vector3 p)
        {
            p-=dir*Vector3.Dot(p,dir);
            if(p.LengthSquared()<1e-10f){p=new Vector3(0,0,1)-dir*dir.Z;if(p.LengthSquared()<1e-8f)p=new Vector3(1,0,0)-dir*dir.X;}
            return Vector3.Normalize(p);
        }
        // fighter-motion.ts placeHand / limb(): an idle hand to a rig-group target with a group-space elbow pole, solved in
        // the clavicle's frame; the wrist keeps its own rotation.
        private static void PlaceHand(MeleeRig r,bool left,Vector3 target,Vector3 pole)
        {
            const float a=.27f,b=.25f;int arm=left?LArm:RArm,elbow=arm+1,clav=arm-1;
            Quaternion inverse=MeleeMath.Inverse(r.ChainQuat(clav,-1));
            Vector3 goal=Vector3.Transform(target-r.ChainPoint(clav,-1),inverse)-r.P[arm],direction=MeleeMath.Unit(goal);float distance=MeleeMath.Clamp(goal.Length(),.035f,a+b-.002f);
            Vector3 bend=Vector3.Transform(pole,inverse);bend=MeleeMath.Unit(bend-direction*Vector3.Dot(bend,direction));
            float along=(a*a-b*b+distance*distance)/(2*distance),height=(float)Math.Sqrt(Math.Max(0,a*a-along*along));
            Vector3 joint=direction*along+bend*height;
            r.SetQ(arm,MeleeMath.FromTo(Down,MeleeMath.Unit(joint)));
            r.SetQ(elbow,MeleeMath.FromTo(Down,Vector3.Transform(MeleeMath.Unit(direction*distance-joint),MeleeMath.Inverse(r.Q[arm]))));
        }
        // character-rig.ts settleJointCovers(): the elbow and knee covers turn half their joint's hinge angle (the joint's
        // Euler x) about the joint, so they bisect the bend. Returns the cover's extra rotation about x for a pilot part.
        public static bool Cover(string part,out int joint)
        {
            joint=part=="left-elbow-joint"?LElbow:part=="right-elbow-joint"?RElbow:part=="left-knee-joint"||part=="left-kneecap"?LKnee:part=="right-knee-joint"||part=="right-kneecap"?RKnee:-1;
            return joint>=0;
        }
        public static float CoverAngle(MeleeRig r,int joint)=>-.5f*r.E[joint].X;
    }
}
