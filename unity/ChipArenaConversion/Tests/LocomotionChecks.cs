using System;
using System.Linq;
using ChipArena.UnityConversion;

internal static class LocomotionChecks
{
    public static void Run(ConversionContent content)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Locomotion: "+why);}
        // Reproduce the native gate's one-pilot reset, then prove a held match can run beyond the entire fixture suite.
        var match=new NativeMatch(content,1);match.Seats.Add(new NativeSeat{id=1,name="Stair check",spec="pistol"});
        int resets=0;match.ResetRound=()=>resets++;
        match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});
        for(int i=0;i<302;i++)match.Tick(1d/60);
        Check(resets==1&&match.Phase==NativePhase.Countdown,"unheld one-pilot fixture did not reproduce the round reset");
        match.Seats[0].roundWins=0;resets=0;match.HoldResolution=()=>true;
        match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});
        for(int i=0;i<180000;i++)match.Tick(1d/60);
        Check(resets==0&&match.Phase==NativePhase.Fight&&Math.Abs(match.Time-3010)<.001&&match.Seats[0].roundWins==0,"held locomotion fixture stopped its clock or resolved the round");
        match.HoldResolution=null;for(int i=0;i<302;i++)match.Tick(1d/60);
        Check(resets==1&&match.Phase==NativePhase.Countdown,"round resolution did not resume after the fixture hold");
        Console.WriteLine("PASS locomotion fixture lifetime: original one-pilot reset reproduced, 180000 held ticks preserve Fight and clock, normal resolution resumes afterward");
        int ramps=0;float steepest=0;
        foreach(var map in content.maps)foreach(var b in map.blocks)
        {
            var q=b.rotation;var n=new V3(2*(q.x*q.y-q.z*q.w),1-2*(q.x*q.x+q.z*q.z),2*(q.y*q.z+q.x*q.w));
            if(!b.id.Contains("stair")||n.y>=.999f)continue;
            ramps++;float angle=(float)(Math.Acos(n.y)*180/Math.PI);steepest=Math.Max(steepest,angle);
            Check(NativeLocomotionRules.Walkable(n),map.id+" stair rejected: "+angle);
            var uphill=new V3(-n.x,0,-n.z).Normalized;
            foreach(float speed in new[]{-9.18f,-6.8f,0,6.8f,9.18f})
            {
                var desired=uphill*speed;var velocity=NativeLocomotionRules.AlongGround(desired,n);
                Check(Math.Abs(V3.Dot(velocity,n))<.00001f,"ground velocity enters the ramp");
                Check((new V3(velocity.x,0,velocity.z)-desired).Length<.00001f,"horizontal movement speed changed");
                Check(speed==0?velocity.Length==0:velocity.y*speed>0,"wrong uphill/downhill direction");
            }
        }
        Check(ramps==228&&steepest>47&&steepest<49,"actual old-map stair inventory changed; review traversal coverage");
        // Analytic frictionless ramp contact using the production knockback/steering rules. The old AfterPhysics
        // fed every solver velocity delta back into knockback; demonstrate its downhill feedback at a real 48° grade.
        var normal=new V3(0,(float)Math.Cos(48*Math.PI/180),-(float)Math.Sin(48*Math.PI/180));
        float Trial(bool fixedMotor)
        {
            V3 body=default,knockback=default;float last=0;
            for(int i=0;i<600;i++)
            {
                var external=NativeMeleeRules.External(ref knockback,1f/60,out float steer);
                var move=NativeMeleeRules.CounterSteer(new V3(0,0,6.8f),external,steer);
                var before=new V3(move.x,body.y,move.z)+external;
                if(fixedMotor)before=NativeLocomotionRules.AlongGround(before,normal);
                var falling=before+(fixedMotor?default:new V3(0,-23f/60,0));float into=Math.Min(0,V3.Dot(falling,normal));body=falling-normal*into;
                knockback+=fixedMotor?NativeLocomotionRules.ContactPush(before,body,default,normal,false):new V3(body.x-before.x,0,body.z-before.z);
                last=body.z;
            }
            return last;
        }
        float broken=Trial(false),fixedSpeed=Trial(true);Check(broken<0&&Math.Abs(fixedSpeed-6.8f)<.001f,"slope feedback regression: old="+broken+", new="+fixedSpeed);
        Check(NativeLocomotionRules.ContactPush(new V3(0,0,6.8f),default,default,new V3(0,0,-1),false).Length==0,"wall braking became knockback");
        Check(NativeLocomotionRules.ContactPush(default,new V3(0,0,3),new V3(0,0,5),new V3(0,0,1),true).z==3,"moving debris impulse lost");
        Check(NativeLocomotionRules.ContactPush(new V3(0,0,6),new V3(0,0,2),default,new V3(0,0,-1),true).Length==0,"stationary crate braking became knockback");
        Check(!NativeLocomotionRules.Walkable(new V3(0,.5f,.866f))&&!NativeLocomotionRules.Walkable(new V3(0,0,1)),"wall/steep face is climbable");
        Check(NativeLocomotionRules.Attach(true,true,5,false,false,false,0)&&!NativeLocomotionRules.Attach(true,true,0,true,false,false,0)&&!NativeLocomotionRules.Attach(true,true,0,false,false,true,0)&&!NativeLocomotionRules.Attach(true,true,0,false,false,false,4)&&!NativeLocomotionRules.Attach(true,true,0,false,true,false,0),"jump/launch/swimming exemptions");
        var state=new NativePredictionState{Position=new V3(0,.87f,0),Grounded=true,CanMove=true,CanSprint=true,Speed=6.8f,Gravity=23,JumpSpeed=8.5f};
        var walking=NativePrediction.Step(state,new InputFrame{Z=1},(a,b)=>b,p=>false);
        Check(!walking.Grounded,"prediction stays grounded after walking off an edge");
        walking=NativePrediction.Step(walking,new InputFrame{Z=1},(a,b)=>b,p=>false);Check(walking.Position.y<state.Position.y,"prediction does not fall off edge");
        var jumping=NativePrediction.Step(state,new InputFrame{Jump=true},(a,b)=>b,p=>true);Check(!jumping.Grounded&&jumping.Velocity.y>8,"ground query cancelled a jump");
        var landing=state;landing.Grounded=false;landing.Velocity.y=-3;landing=NativePrediction.Step(landing,new InputFrame(),(a,b)=>new V3(b.x,.87f,b.z),p=>true);Check(landing.Grounded&&landing.Velocity.y==0,"predicted landing lost support");
        Console.WriteLine("PASS locomotion: "+ramps+" authored ramps (max "+steepest.ToString("F2")+"°), walk/sprint/up/down/idle, analytic slide reproduction "+broken.ToString("F3")+" -> "+fixedSpeed.ToString("F3")+" m/s, stationary vs dynamic contact impulses, jump/launch/swimming and prediction ledges/landing");
    }
}
