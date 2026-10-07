#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    public static class NativeMotionChecks
    {
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        private static void Capture(CapturedRig.Pose p,float t)
        {var row=new float[64];CapturedClips.Sets[0][0].Sample(t,row);CapturedRig.Sample(row,0,p);}
        private static void Layers(CapturedRig.Pose p,Actor a,float t)
        {
            if(GunRules.Ranged(a.Weapon)){RangedPose.Apply(p,a,t,1);RangedAbilityPose.Apply(p,a,t,true);}
            if(a.MagicVisual>0)MagicPose.Apply(p,a,t,true,new float[13],0);
            NativeFirstPersonPose.SettleTorso(p);
        }
        public static List<string> Run()
        {
            var results=new List<string>();var magic=new Actor{MagicVisual=1,AttackAt=1.9f,CastRate=1};
            var legacy=new CapturedRig.Pose();Capture(legacy,2);Layers(legacy,magic,2);Vector3 before=legacy.WorldPosition[CapturedRig.LHand];
            for(int i=0;i<5;i++)Layers(legacy,magic,2);
            float oldDrift=Vector3.Distance(before,legacy.WorldPosition[CapturedRig.LHand]);Check(oldDrift>.02f,"Feedback reproducer no longer exercises the reported bug");
            int repeated=0;
            var actors=new List<Actor>{new Actor(),new Actor{Weapon=WeaponId.Pistol,LastShot=1.97f},new Actor{Weapon=WeaponId.Rifle,LastShot=1.97f},new Actor{Weapon=WeaponId.MachineGun,AbilityId="rocket",AbilityAt=1.7f,AbilityRelease=1.97f}};
            for(int e=1;e<=4;e++)actors.Add(new Actor{MagicVisual=e,AttackAt=1.9f,CastRate=1});
            foreach(var actor in actors)
            {
                var history=new NativePoseHistory();var p=new CapturedRig.Pose();var positions=new Vector3[CapturedRig.Bones.Length];var rotations=new Quaternion[positions.Length];
                for(int frame=0;frame<120;frame++)
                {
                    Capture(p,2);history.BlendBase(p,2,false);Layers(p,actor,2);
                    for(int i=0;i<positions.Length;i++)
                    {
                        if(frame>0){Check(Vector3.Distance(positions[i],p.WorldPosition[i])<.000001f,"Repeated frame moved a joint");Check(Math.Abs(Quaternion.Dot(rotations[i],p.WorldRotation[i]))>.999999f,"Repeated frame rotated a joint");}
                        positions[i]=p.WorldPosition[i];rotations[i]=p.WorldRotation[i];
                    }
                    repeated++;
                }
                // Timeline reset and a long suspension must discard old base poses.
                Capture(p,0);var expected=p.Position[0];history.BlendBase(p,0,false);Check(Vector3.Distance(p.Position[0],expected)<1e-6f,"Rewind retains old base pose");
                Capture(p,5);expected=p.Position[0];history.BlendBase(p,5,false);Check(Vector3.Distance(p.Position[0],expected)<1e-6f,"Suspension retains old base pose");
            }
            results.Add($"First-person feedback reproduced ({oldDrift*100:0.00} cm wrist drift); {repeated} corrected repeated frames have stable joint positions/rotations, with rewind/gap resets");
            int clockSamples=0;
            foreach(int hz in new[]{30,60,120,144,240})
            {
                var clock=new NativeVisualClock();double previous=-1;
                for(int frame=0;frame<=hz*3;frame++)
                {
                    double now=frame/(double)hz,sim=Math.Floor((now+1e-9)/NativeVisualClock.Step)*NativeVisualClock.Step;
                    float shown=clock.Sample(sim,now-sim,false);Check(Math.Abs(shown-now)<.000001,"Visual clock still quantized at "+hz+" Hz");Check(shown>=previous,"Visual clock rewound");previous=shown;clockSamples++;
                }
            }
            var held=new NativeVisualClock();held.Reset(3);float pause=held.Sample(3,.012,false);
            for(int i=0;i<100;i++)Check(held.Sample(3,i*.01,true)==pause,"Paused view drifts");Check(held.Sample(3,.001,false)==pause,"Resume rewinds presentation");Check(held.Sample(3+NativeVisualClock.Step,.01,false)>pause,"Resume fails to advance");
            held.Reset(0);Check(held.Sample(0,10,false)<=NativeVisualClock.Step+1e-6,"Presentation extrapolates past one physics tick");Check(held.Sample(-1,0,false)==-1,"Clock reset ignored");
            results.Add(clockSamples+" render-clock samples at 30/60/120/144/240 Hz; continuous between 60 Hz ticks, bounded, monotonic and pause/reset safe");
            var reference=new float[2];int rateIndex=0;
            foreach(int hz in new[]{60,240})
            {
                var ads=new NativeAimMotion();ads.Sample(0,false);float previous=0;
                for(int i=1;i<=hz/5;i++){float weight=ads.Sample(i/(float)hz,true);Check(weight>=previous&&weight<1,"ADS transition snaps or overshoots");previous=weight;}
                reference[rateIndex++]=ads.Weight;Check(ads.Weight>.9f,"ADS too slow");
                float frozen=ads.Weight;ads.Sample(.2f,true);Check(ads.Weight==frozen,"Same-time ADS sample moves");
                for(int i=1;i<=hz/2;i++)ads.Sample(.2f+i/(float)hz,false);Check(ads.Weight<.002f,"ADS does not return to hip");
                ads.Reset();Check(ads.Weight==0,"Scope state survived reset");
            }
            Check(Math.Abs(reference[0]-reference[1])<.015f,"ADS depends excessively on frame rate");
            Check(!NativeAimPresentation.Scope(true,true,WeaponId.Rifle,.6f)&&NativeAimPresentation.Scope(true,true,WeaponId.Rifle,.61f),"Scope threshold");
            Check(!NativeAimPresentation.Scope(false,true,WeaponId.Rifle,1)&&!NativeAimPresentation.Scope(true,false,WeaponId.Rifle,1)&&!NativeAimPresentation.Scope(true,true,WeaponId.Pistol,1),"Scope leaks to another camera or weapon");
            Check(NativeAimPresentation.ScopeAlpha(.329f)==0&&Math.Abs(NativeAimPresentation.ScopeAlpha(.34f)-.75f)<.00001f,"Source scope mask dimensions");
            Check(Math.Abs(NativeAimPresentation.Scale(75,1)-.78f)<.000001f&&Math.Abs(NativeAimPresentation.Scale(28,1)/.78f-.324957f)<.0001f,"Scope FOV counter-scale");
            results.Add("Source ADS spring/settling, return/reset, scope eligibility, 33vh mask and FOV counter-scale");return results;
        }
    }
}
#endif
