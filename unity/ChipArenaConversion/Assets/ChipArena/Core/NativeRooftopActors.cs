using System;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The pilots of the home screen's rooftop scene (NativeRooftopCity), posed by the game's own pose code, presentation only:
    //  seated  the player's pilot on the lip: NativeLobbyPoses 'Sitting on chair' (legs over the edge, hands on the lap) with
    //          NativeDraftRules.Look turning chest, neck and head: a slow scan across the city (24 s), a breath of pitch (16 s)
    //          and, each loop at 28-34 s, a glance that follows a car crossing in front (the near lane is timed to it).
    //  runner  CapturedClips run, a crouch into the pack jump take across a roof-to-roof gap and its land take; it appears
    //          from and vanishes into a hut, so its cycle restarts unseen. Over a level gap it front-flips in the Jump-Flip's
    //          tuck (the dagger air flip's turn, NativeJumpRules.FlipTurn); off a drop it lands into a forward roll.
    //  blade   the duel (owner 2026-10-29: "Can make the background characters animations better designed"): an opening
    //          cutA, then the block under the burst, flinching to every deflected tracer (blockHit) and driven back a step by
    //          each; then it dashes in with cutB into cutC and hops back to its mark (DuelOffset). Moves crossfade.
    //  gunner  an idle sample under RangedPose.Apply: a rifle aim with recoil from LastShot, firing bursts (Events: shot times);
    //          it backpedals from the blade's dash and walks back to its mark before the next burst.
    //  watcher NativeLobbyPoses 'Standing with weapon' hero stance on the duel roof, flinching back at the dash.
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
            public readonly MeleeRig Rig=new MeleeRig();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly float[] Row=new float[64],Row2=new float[64];
            public Vector3 Offset;public bool Hidden;public LobbyHeld Weapon,Offhand;
        }
        private static int SetOf(string spec)=>spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0;
        private static CapturedClip Clip(int set,string state)=>CapturedClips.Sets[set].First(c=>c.StateName==state);
        private static float Smooth01(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        private static float Wrap(float t,float period)=>t-(float)Math.Floor(t/period)*period;
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        // The duel's footwork in each fighter's own frame (+z toward the other), at `time`: the blade driven back by each tracer
        // it deflects, its dash and its hop back; the gunner's backpedal from the dash and its walk back to the mark. The
        // tracers and sparks follow it (NativeRooftopPresentation.Dynamic). Periodic in DuelCycle.
        public static Vector3 DuelOffset(string id,float time)
        {
            float tc=Wrap(time,DuelCycle);
            if(id=="blade")
            {
                if(tc<.7f)return new Vector3(0,0,.9f);
                if(tc<2.6f){float z=.9f;foreach(var shot in Shots)z-=.2f*Smooth01((tc-shot-TracerFlight)/.14f);return new Vector3(0,0,z);}
                float back=.9f-.2f*Shots.Length;
                if(tc<3.3f){float u=(tc-2.6f)/.7f;return new Vector3(0,0,Lerp(back,2.2f,1-(1-u)*(1-u)*(1-u)));}
                if(tc<3.75f)return new Vector3(0,0,2.2f);
                return new Vector3(0,.25f*(float)Math.Sin(Math.PI*(tc-3.75f)/.25f),Lerp(2.2f,.9f,Smooth01((tc-3.75f)/.25f)));
            }
            if(id=="gunner")
            {
                if(tc>=2.6f&&tc<3.4f)return new Vector3(0,0,-1.2f*Smooth01((tc-2.6f)/.7f));
                float since=tc>=3.4f?tc-3.4f:tc+DuelCycle-3.4f;return new Vector3(0,0,-1.2f*(1-Smooth01(since/1.4f)));
            }
            return Vector3.Zero;
        }
        // Turns the whole pose by `angle` about its side (+x) axis through `pivot` (feet space: the pose's own origin sits
        // GroupY above the feet): a flip, a roll, a lean.
        private static void Turn(CapturedRig.Pose p,float angle,Vector3 pivot)
        {
            if(Math.Abs(angle)<1e-5f)return;var q=Quaternion.CreateFromAxisAngle(Vector3.UnitX,angle);pivot.Y-=NativePortraitRules.GroupY;
            p.Rotation[CapturedRig.Body]=q*p.Rotation[CapturedRig.Body];p.Position[CapturedRig.Body]=pivot+Vector3.Transform(p.Position[CapturedRig.Body]-pivot,q);
        }
        // `clip` at `at` blended over the row already in s.Row by `w` (turning channels slerped).
        private static void Over(State s,CapturedClip clip,float at,float w)
        {
            if(w<=1e-4f)return;clip.Sample(at,s.Row2);MeleeCapture.BlendRowsTurning(s.Row,s.Row2,Math.Min(1,w),s.Row);
        }
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
            s.Weapon=new LobbyHeld{Visible=look.Script!=Seated,Bone=NativeLobbyPoses.HeldBone(look.Spec),Position=Vector3.Zero,Rotation=Quaternion.Identity};
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
                    float tc=Wrap(time+look.Phase,look.Cycle),x=tc*look.Speed;var jc=NativeJumpRules.Clip(set,NativeJumpRules.Jump);float m0=jc.Marks[0],m1=jc.Marks[1],m3=jc.Marks[3];
                    bool flip=look.Drop<=0;float pivotY=.85f;
                    if(x<look.L1)
                    {
                        Clip(set,"run").Sample(tc,s.Row);
                        // the last strides gather into the take-off crouch
                        float gather=Smooth01((x-(look.L1-look.Speed*.22f))/(look.Speed*.22f));Over(s,jc,m0+(m1-m0)*gather,gather);
                        CapturedRig.Sample(s.Row,set,p);s.Offset=new Vector3(0,0,x);
                    }
                    else if(x<look.L1+look.Gap)
                    {
                        float u=(x-look.L1)/look.Gap;jc.Sample(m1+(m3-m1)*u,s.Row);
                        if(flip){var fc=NativeJumpRules.Clip(set,NativeJumpRules.Flip);float fu=Smooth01((u-.08f)/.84f);if(fc!=null)Over(s,fc,fu*fc.Duration,NativeJumpRules.FlipTuck(fu));}
                        CapturedRig.Sample(s.Row,set,p);s.Offset=new Vector3(0,4*look.Arc*u*(1-u)-look.Drop*u,x);
                        if(flip)Turn(p,(float)(2*Math.PI)*NativeJumpRules.FlipTurn(Smooth01((u-.08f)/.84f)),new Vector3(0,pivotY,0));
                    }
                    else if(x<look.L1+look.Gap+look.L2)
                    {
                        float since=(x-look.L1-look.Gap)/look.Speed;var lc=NativeJumpRules.Clip(set,NativeJumpRules.Land);
                        Clip(set,"run").Sample(tc,s.Row);s.Offset=new Vector3(0,-look.Drop,x);
                        if(flip){if(since<.32f)Over(s,lc,since/.32f*lc.Duration*.8f,1-Smooth01((since-.2f)/.12f));CapturedRig.Sample(s.Row,set,p);}
                        else
                        {
                            // off the drop: absorb, then a forward roll out of it, its lowest point kept on the bridge all the way round
                            float r=(since-.08f)/.62f;Over(s,lc,Math.Min(since,.3f)/.3f*lc.Duration*.8f,1-Smooth01((since-.62f)/.16f));CapturedRig.Sample(s.Row,set,p);
                            if(r>0&&r<1)
                            {
                                Turn(p,(float)(2*Math.PI)*Smooth01(r),new Vector3(0,.55f,0));p.World();float low=float.MaxValue;
                                for(int b=0;b<CapturedRig.Bones.Length;b++)low=Math.Min(low,p.WorldPosition[b].Y);
                                s.Offset.Y+=(float)Math.Sin(Math.PI*r)*(.10f-(low+NativePortraitRules.GroupY));
                            }
                        }
                    }
                    else{s.Hidden=true;Clip(set,"idle").Sample(tc,s.Row);CapturedRig.Sample(s.Row,set,p);}
                    break;
                }
                case Blade:
                {
                    float tc=Wrap(time,DuelCycle);s.Offset=DuelOffset("blade",time);
                    var cutA=Clip(set,"cutA");var block=Clip(set,"block");var cutB=Clip(set,"cutB");var cutC=CapturedClips.Sets[set].FirstOrDefault(c=>c.StateName=="cutC")??cutA;var idle=Clip(set,"idle");
                    float hold=block.Duration*.5f;
                    if(tc<.7f){cutA.Sample(tc/.7f*cutA.Duration,s.Row);Over(s,idle,tc,1-Smooth01(tc/.12f));}
                    else if(tc<2.6f)
                    {
                        float bt=tc-.7f;block.Sample(Math.Min(bt*1.6f,hold),s.Row);Over(s,cutA,cutA.Duration,1-Smooth01(bt/.12f));
                        // each deflected tracer jolts the guard (blockHit), easing back into the block
                        float last=-1;foreach(var shot in Shots){float at=shot+TracerFlight;if(at<=tc)last=at;}
                        var hit=CapturedClips.Sets[set].FirstOrDefault(c=>c.StateName=="blockHit");float age=tc-last;
                        if(hit!=null&&last>0&&age<.3f)Over(s,hit,age/.3f*hit.Duration*.7f,1-Smooth01(age/.3f));
                    }
                    else if(tc<3.3f){float u=(tc-2.6f)/.7f;cutB.Sample(u*cutB.Duration,s.Row);Over(s,block,hold,1-Smooth01((tc-2.6f)/.1f));}
                    else if(tc<3.75f){float u=(tc-3.3f)/.45f;cutC.Sample(u*cutC.Duration,s.Row);Over(s,cutB,cutB.Duration,1-Smooth01((tc-3.3f)/.1f));}
                    else{idle.Sample(tc,s.Row);Over(s,cutC,cutC.Duration,1-Smooth01((tc-3.75f)/.15f));}
                    CapturedRig.Sample(s.Row,set,p);
                    // the dash leans into the cut
                    if(tc>=2.6f&&tc<3.4f)Turn(p,.22f*(float)Math.Sin(Math.PI*Math.Min(1,(tc-2.6f)/.8f)),new Vector3(0,0,0));
                    break;
                }
                case Gunner:
                {
                    float tc=Wrap(time,DuelCycle),last=-100;foreach(var shot in Shots)if(shot<=tc)last=shot;
                    var o=DuelOffset("gunner",time);var o2=DuelOffset("gunner",time+.05f);float speed=(o2.Z-o.Z)/.05f;s.Offset=o;
                    var idle=Clip(0,"idle");var walk=Clip(0,"walk");idle.Sample(tc,s.Row);
                    // the feet walk the backpedal and the return, the clip's phase following the ground covered (backwards while
                    // it backs off, so the steps never slide), the rifle kept on the blade
                    float stride=Math.Min(1,Math.Abs(speed)/1.2f);Over(s,walk,o.Z/1.4f*walk.Duration,stride);
                    CapturedRig.Sample(s.Row,0,p);
                    RangedPose.Apply(p,new Actor{Weapon=WeaponId.Rifle,Pitch=-4,AimYaw=0,LastShot=last-tc+time,Aiming=true},time,1);break;
                }
                case Watcher:
                {
                    s.Rig.Reset();NativeLobbyPoses.Sample(s.Rig,look.Spec,NativeLobbyPoses.Weapon,time,out s.Weapon,out s.Offhand);s.Rig.WritePose(p);
                    // it flinches back as the blade dashes, then settles
                    float tc=Wrap(time,DuelCycle);float flinch=Smooth01((tc-2.62f)/.12f)*(1-Smooth01((tc-2.9f)/.6f));Turn(p,-.16f*flinch,Vector3.Zero);
                    break;
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
