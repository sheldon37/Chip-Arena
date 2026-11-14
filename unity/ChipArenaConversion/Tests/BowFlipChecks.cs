using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// The Bow held through the double jump's flip (Claude, presentation; the Bow's air jump and flip are Astra's PR #159:
// NativeJumpSimulation.CanAirJump, NativeJumpRules.Flips): Core/NativeBowMotion keeps a raised bow's torso out of the flip's
// tucked take and solves the stance and the arms on the unturned body, then turns the pose and its bow frame with the flip.
//  1. the pieces: the flip's take weight is the layer's own envelope; the turn taken off a rig and put back on its pose is
//     the identity; a bow frame turned with the body lands on the turned body's points;
//  2. the route (once the Bow flips, after PR #159; on a base without it this part reports that it is dormant): a pilot
//     drawing through its double jump keeps the string hand on the nock, both arms within reach and the bow clear of the
//     torso through the whole flip; a carried bow still flips; nothing allocates.
internal static class BowFlipChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Bow flip: "+what);count++;}
    private struct Arc{public float J,AJ,Land,Y1,V1;}
    private static Arc Jump(float j,float aj){float v0=Rules.JumpSpeed,v1=Rules.JumpSpeed*NativeJumpSimulation.AirMultiplier,g=Rules.Gravity,u=aj-j,y1=v0*u-.5f*g*u*u;return new Arc{J=j,AJ=aj,Y1=y1,V1=v1,Land=aj+(v1+(float)Math.Sqrt(v1*v1+2*g*y1))/g};}
    private static void Place(Actor a,in Arc c,float t)
    {
        float y=0,vy=0;bool air=t>=c.J&&t<c.Land;
        if(air){if(t<c.AJ){float u=t-c.J;y=Rules.JumpSpeed*u-.5f*Rules.Gravity*u*u;vy=Rules.JumpSpeed-Rules.Gravity*u;}else{float u=t-c.AJ;y=c.Y1+c.V1*u-.5f*Rules.Gravity*u*u;vy=c.V1-Rules.Gravity*u;}}
        a.Grounded=!air;a.AirJumps=air&&t>=c.AJ?1:0;a.AirJumpAt=a.AirJumps>0?c.AJ:-100;a.Position=new V3(0,Math.Max(0,y),0);a.Velocity=new V3(0,air?vy:0,0);
    }
    private static float Segment(Vector3 x,Vector3 a,Vector3 b){var d=b-a;float u=d.LengthSquared()<1e-10f?0:Math.Clamp(Vector3.Dot(x-a,d)/d.LengthSquared(),0,1);return Vector3.Distance(x,a+d*u);}

    public static IEnumerable<string> Run()
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The pieces
        // ------------------------------------------------------------------------------------------------------------------
        int c0=count;
        for(float u=-.1f;u<1.1f;u+=.01f)
        {
            float want=u>=0&&u<1?MeleeMath.Ease(u/.1f)*(1-MeleeMath.Ease((u-.8f)/.2f)):0;
            Check(Math.Abs(NativeJumpRules.FlipTuck(u)-want)<1e-6f,"the flip's take weight is the layer's envelope (u "+u.ToString("0.00")+")");
        }
        Check(NativeJumpRules.FlipTuck(float.NaN)==0,"no take without a flip");
        var rig=new MeleeRig();var pose=new CapturedRig.Pose();var turned=new CapturedRig.Pose();var rnd=new Random(7);float worstTrip=0,worstFrame=0;
        foreach(float angle in new[]{.3f,1.7f,3.1f,4.6f,6.0f})
        {
            rig.Reset();for(int i=0;i<CapturedRig.Bones.Length;i++)rig.SetE(i,(float)rnd.NextDouble()-.5f,(float)rnd.NextDouble()-.5f,(float)rnd.NextDouble()-.5f);
            rig.P[CapturedRig.Body]=new Vector3(.03f,-.12f,.05f);rig.WritePose(turned);turned.World();
            // Unspin, write, Respin: the original body (the pose's turn is the flip's).
            NativeJumpMotion.Unspin(rig,angle);var about=NativeJumpMotion.About(rig.P[CapturedRig.Body]);rig.WritePose(pose);pose.World();
            var bodyFrame=new NativeBowPose.Frame{Anchor=NativeBowPose.ChestAnchor(pose),BowWrist=pose.WorldPosition[CapturedRig.LHand],DrawWrist=pose.WorldPosition[CapturedRig.RHand],Nock=pose.WorldPosition[CapturedRig.Head],
                BowRotation=pose.WorldRotation[CapturedRig.LHand],DrawRotation=pose.WorldRotation[CapturedRig.RHand],Aim=Vector3.UnitZ,Up=Vector3.UnitY,Left=Vector3.UnitX};
            NativeJumpMotion.Respin(pose,angle);
            for(int b=0;b<CapturedRig.Bones.Length;b++)worstTrip=Math.Max(worstTrip,Math.Max(Vector3.Distance(pose.WorldPosition[b],turned.WorldPosition[b]),1-Math.Abs(Quaternion.Dot(pose.WorldRotation[b],turned.WorldRotation[b]))));
            var f=NativeBowMotion.Turned(bodyFrame,NativeJumpMotion.Turn(angle),about);
            worstFrame=Math.Max(worstFrame,Math.Max(Vector3.Distance(f.Anchor,NativeBowPose.ChestAnchor(pose)),Math.Max(Vector3.Distance(f.BowWrist,pose.WorldPosition[CapturedRig.LHand]),Vector3.Distance(f.DrawWrist,pose.WorldPosition[CapturedRig.RHand]))));
            worstFrame=Math.Max(worstFrame,Math.Max(Vector3.Distance(f.Nock,pose.WorldPosition[CapturedRig.Head]),Math.Max(1-Math.Abs(Quaternion.Dot(f.BowRotation,pose.WorldRotation[CapturedRig.LHand])),1-Math.Abs(Quaternion.Dot(f.DrawRotation,pose.WorldRotation[CapturedRig.RHand])))));
            Check(Math.Abs(f.Aim.Length()-1)<1e-5f&&Math.Abs(Vector3.Dot(f.Aim,f.Up))<1e-5f,"the turned frame's directions stay orthonormal");
        }
        Check(worstTrip<1e-5f,"the turn taken off a rig and put back on its pose is the identity ("+worstTrip.ToString("0e0")+")");
        Check(worstFrame<1e-5f,"a bow frame turned with the body lands on the turned body ("+worstFrame.ToString("0e0")+")");
        yield return $"Bow flip pieces: {count-c0} checks (the flip's take weight is the layer's own envelope; the turn off and back on is the identity; the turned bow frame lands on the turned body)";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. The route
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        if(!NativeJumpRules.Flips(NativeBowWire.Spec)){yield return "Bow flip route: dormant on this base (the Bow does not flip here; NativeJumpRules.Flips follows NativeJumpSimulation.CanAirJump after PR #159)";yield break;}
        float worstHook=0,worstReach=0,worstClear=float.MaxValue,flipped=0;long bytes=0;var along=new[]{-.5f,-.25f,0f,.25f,.5f};
        foreach(bool draw in new[]{true,false})
        {
            var a=new Actor{Id=41,SourceSpec=NativeBowWire.Spec,Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,MeleeKit=new NativeMeleeKit(),LastShot=-100,RollDirection=new V3(0,0,1)};if(draw)a.BowDrawAt=.7f;
            var m=new NativeBowMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};var p=new CapturedRig.Pose();var juggle=new NativeJuggleTrack();
            // Two double jumps on the same rig: the first warms the route (its first flip loads the takes), the second is measured
            // for allocation; both for the hold.
            foreach(float start in new[]{1f,4f})
            {
                var arc=Jump(start,start+.38f);
                void Step(float t){Place(a,arc,t);m.Rig.GroupPosition=new Vector3(0,a.Position.y+.865f,0);m.Rig.GroupYaw=0;m.Pose(a,t,p,false,juggle,out _);m.Finish(p,t);}
                for(float t=start-.7f;t<arc.AJ-.05f;t+=1/60f)Step(t);
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(float t=arc.AJ-.05f;t<arc.Land+.2f;t+=1/60f)
                {
                    Step(t);var f=m.Shot;var v=m.Bow;flipped=Math.Max(flipped,m.JumpLayer.FlipAngle);
                    if(!draw||a.Grounded||v.Raised<.999f||f.DrawWeight<.5f)continue;
                    var hook=p.WorldPosition[CapturedRig.RHand]+Vector3.Transform(NativeBowModels.Hook,p.WorldRotation[CapturedRig.RHand]);var nock=p.WorldPosition[CapturedRig.LHand]+Vector3.Transform(NativeBow.NockAt(f.Pull),p.WorldRotation[CapturedRig.LHand]);
                    worstHook=Math.Max(worstHook,Vector3.Distance(hook,nock));worstReach=Math.Max(worstReach,Math.Max(f.BowReach,f.DrawReach));
                    var c=p.WorldPosition[CapturedRig.Chest];var s=p.WorldPosition[CapturedRig.Spine];var pe=p.WorldPosition[CapturedRig.Pelvis];
                    foreach(float h in along){var w=p.WorldPosition[CapturedRig.LHand]+Vector3.Transform(new Vector3(0,h,0),p.WorldRotation[CapturedRig.LHand]);worstClear=Math.Min(worstClear,Math.Min(Segment(w,pe,s),Segment(w,s,c)));}
                }
                if(start>1)bytes+=GC.GetAllocatedBytesForCurrentThread()-before;
            }
        }
        Check(flipped>6f,"the Bow's air jump flips ("+flipped.ToString("0.0")+" rad)");
        Check(worstHook<.012f,"drawing through the flip, the string hand stays on the nock ("+(worstHook*100).ToString("0.0")+" cm at most)");
        Check(worstReach<=.522f,"drawing through the flip, both arms reach ("+worstReach.ToString("0.000")+" m of .515)");
        Check(worstClear>.1f,"drawing through the flip, the bow stays clear of the torso ("+(worstClear*100).ToString("0.0")+" cm)");
        Check(bytes==0,"the flip allocates nothing ("+bytes+" bytes)");
        yield return $"Bow flip route: {count-c0} checks (a pilot drawing through its double jump: the string hand on the nock within {worstHook*100:0.0} cm, the arms within {worstReach:0.000} m, the bow {worstClear*100:0.0} cm clear of the torso; a carried bow flips; no allocation)";
    }
}
