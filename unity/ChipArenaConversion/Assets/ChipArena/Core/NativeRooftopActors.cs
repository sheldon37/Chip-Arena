using System;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The pilots of the home screen's rooftop scene (NativeRooftopCity), posed by the game's own pose code, presentation only:
    //  seated  the player's pilot on the lip: NativeLobbyPoses 'Sitting on chair' (legs over the edge, hands on the lap) with
    //          NativeDraftRules.Look turning chest, neck and head: a slow scan across the city (24 s), a breath of pitch (16 s)
    //          and, each loop at 28-34 s, a glance that follows a car crossing in front (the near lane is timed to it).
    //  runner  CapturedClips run, then the pack jump take across a roof-to-roof gap and its land take; it appears from and
    //          vanishes into a hut, so its cycle restarts unseen.
    //  blade   the captured cutA / block / cutB / idle cycle, deflecting the gunner's tracers (Events: spark times).
    //  gunner  an idle sample under RangedPose.Apply: a rifle aim with recoil from LastShot, firing bursts (Events: shot times).
    //  watcher NativeLobbyPoses 'Standing with weapon' hero stance on the duel roof.
    // Poses are in the source frame relative to the actor's feet (Root adds its course offset); the Unity boundary mirrors them.
    public static class NativeRooftopActors
    {
        public const int Seated=0,Runner=1,Blade=2,Gunner=3,Watcher=4;
        public sealed class Look {public string Id,Spec,Hat,Body,Accent;public int Script;public float L1,Gap,L2,Arc,Drop,Speed,Cycle,Phase;}
        // The distant pilots' looks: fixed, deterministic, in the cyber palette.
        public static readonly Look[] Crowd={
            new Look{Id="runA",Spec="sword",Hat="cap",Body="#2f3644",Accent="#22e6ff",Script=Runner,L1=16,Gap=9,L2=9.3f,Arc=2.6f,Drop=3,Speed=7.2f,Cycle=8},
            new Look{Id="runB",Spec="daggers",Hat="beanie",Body="#e5e7eb",Accent="#ff3dcf",Script=Runner,L1=22,Gap=7,L2=24,Arc=2.2f,Drop=0,Speed=7.8f,Cycle=8,Phase=3.3f},
            new Look{Id="blade",Spec="sword",Hat="antenna",Body="#3a4256",Accent="#ffd84a",Script=Blade},
            new Look{Id="gunner",Spec="rifle",Hat="cowboy",Body="#cfd5e2",Accent="#ff3dcf",Script=Gunner},
            new Look{Id="watch",Spec="shield",Hat="halo",Body="#1f2533",Accent="#22e6ff",Script=Watcher},
        };
        // The gunner's shots in its 4 s cycle; each tracer reaches the blade .12 s later and is deflected in sparks.
        public static readonly float[] Shots={.8f,.95f,1.1f,1.25f,1.6f,1.75f,1.9f,2.05f,2.2f};
        public const float DuelCycle=4,TracerFlight=.12f;
        public sealed class State
        {
            public readonly MeleeRig Rig=new MeleeRig();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly float[] Row=new float[64];
            public Vector3 Offset;public bool Hidden;public LobbyHeld Weapon,Offhand;
        }
        private static int SetOf(string spec)=>spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0;
        private static CapturedClip Clip(int set,string state)=>CapturedClips.Sets[set].First(c=>c.StateName==state);
        private static float Smooth01(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        private static float Wrap(float t,float period)=>t-(float)Math.Floor(t/period)*period;
        // The seated pilot's gaze in its own frame (+z ahead, +x its left): yaw and pitch at `time`.
        public static void Gaze(float time,out float yaw,out float pitch)
        {
            float tl=Wrap(time,NativeRooftopCity.Loop);float glance=Smooth01((tl-28)/1.2f)*(1-Smooth01((tl-33.2f)/1.4f)),car=.85f-1.5f*Smooth01((tl-28.4f)/4.4f);
            yaw=(.42f*(float)Math.Sin(2*Math.PI*time/24)+.12f*(float)Math.Sin(2*Math.PI*time/16+1))*(1-glance)+car*glance;
            pitch=-.30f+.06f*(float)Math.Sin(2*Math.PI*time/16)+.12f*glance;
        }
        // Poses `s` for `look` at `time`; world-space Pose (pose.World() has run) relative to the feet, Offset along the course.
        public static void Sample(Look look,State s,float time)
        {
            var p=s.Pose;p.Reset();s.Offset=Vector3.Zero;s.Hidden=false;int set=SetOf(look.Spec);
            s.Weapon=new LobbyHeld{Visible=look.Script!=Seated,Bone=NativeLobbyPoses.Magic(look.Spec)?CapturedRig.LHand:CapturedRig.RHand,Position=Vector3.Zero,Rotation=Quaternion.Identity};
            s.Offhand=new LobbyHeld{Visible=look.Script!=Seated&&NativeLobbyPoses.OffHand(look.Spec),Bone=CapturedRig.LHand,Position=Vector3.Zero,Rotation=Quaternion.Identity};
            switch(look.Script)
            {
                case Seated:
                {
                    s.Rig.Reset();NativeLobbyPoses.Sample(s.Rig,look.Spec,NativeLobbyPoses.Chair,time,out _,out _);s.Rig.WritePose(p);
                    Gaze(time,out float yaw,out float pitch);p.World();var eye=p.WorldPosition[CapturedRig.Head]+Vector3.UnitY*.165f;
                    NativeDraftRules.Look(p,eye+new Vector3((float)(Math.Sin(yaw)*Math.Cos(pitch)),(float)Math.Sin(pitch),(float)(Math.Cos(yaw)*Math.Cos(pitch)))*40,.9f);break;
                }
                case Runner:
                {
                    float tc=Wrap(time+look.Phase,look.Cycle),x=tc*look.Speed;
                    if(x<look.L1){Clip(set,"run").Sample(tc,s.Row);CapturedRig.Sample(s.Row,set,p);s.Offset=new Vector3(0,0,x);}
                    else if(x<look.L1+look.Gap)
                    {
                        float u=(x-look.L1)/look.Gap;var jc=NativeJumpRules.Clip(set,NativeJumpRules.Jump);float m1=jc.Marks[1],m3=jc.Marks[3];
                        jc.Sample(m1+(m3-m1)*u,s.Row);CapturedRig.Sample(s.Row,set,p);s.Offset=new Vector3(0,4*look.Arc*u*(1-u)-look.Drop*u,x);
                    }
                    else if(x<look.L1+look.Gap+look.L2)
                    {
                        float since=(x-look.L1-look.Gap)/look.Speed;
                        if(since<.32f){var lc=NativeJumpRules.Clip(set,NativeJumpRules.Land);lc.Sample(since/.32f*lc.Duration*.8f,s.Row);}else Clip(set,"run").Sample(tc,s.Row);
                        CapturedRig.Sample(s.Row,set,p);s.Offset=new Vector3(0,-look.Drop,x);
                    }
                    else{s.Hidden=true;Clip(set,"idle").Sample(tc,s.Row);CapturedRig.Sample(s.Row,set,p);}
                    break;
                }
                case Blade:
                {
                    float tc=Wrap(time,DuelCycle);string st=tc<.7f?"cutA":tc<2.6f?"block":tc<3.3f?"cutB":"idle";float local=tc<.7f?tc:tc<2.6f?tc-.7f:tc<3.3f?tc-2.6f:tc-3.3f;
                    var cl=Clip(set,st);cl.Sample(st=="block"?Math.Min(local,cl.Duration*.5f):st=="idle"?tc:local/.7f*cl.Duration,s.Row);CapturedRig.Sample(s.Row,set,p);break;
                }
                case Gunner:
                {
                    float tc=Wrap(time,DuelCycle),last=-100;foreach(var shot in Shots)if(shot<=tc)last=shot;
                    Clip(0,"idle").Sample(tc,s.Row);CapturedRig.Sample(s.Row,0,p);
                    RangedPose.Apply(p,new Actor{Weapon=WeaponId.Rifle,Pitch=-4,AimYaw=0,LastShot=last-tc+time,Aiming=true},time,1);break;
                }
                default:
                    s.Rig.Reset();NativeLobbyPoses.Sample(s.Rig,look.Spec,NativeLobbyPoses.Weapon,time,out s.Weapon,out s.Offhand);s.Rig.WritePose(p);break;
            }
            p.World();
        }
        // The gunner's tracer in flight at `time` (0..1 along muzzle to blade), or -1; the blade's deflection spark age, or -1.
        public static float Tracer(float time,int shot){float age=Wrap(time,DuelCycle)-Shots[shot];return age>=0&&age<=TracerFlight?age/TracerFlight:-1;}
        public static float Deflection(float time,int shot){float age=Wrap(time,DuelCycle)-Shots[shot]-TracerFlight;return age>=0&&age<=.35f?age:-1;}
        // The seated pilot's root height so its pelvis rests on the lip: the pose's pelvis less the hip shell under it.
        public static float SeatHeight(State seated)=>NativeRooftopCity.LipTop-(seated.Pose.WorldPosition[CapturedRig.Pelvis].Y+NativePortraitRules.GroupY-.10f);
    }
}
