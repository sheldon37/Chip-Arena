#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class MagicFixture
    { public int element;public float rate,pitch,time,attack,shot,abilityAt,releaseAt,weight;public string ability;public bool first;public float[] pose; }
    [Serializable] public sealed class AbilityRuleFixture
    { public string spec,slot;public NumberValue[] stats;public float windup,cooldown,cost,radius,scale,echo;public int charges; }
    [Serializable] public sealed class PresentationFixtures
    { public string commit;public MagicFixture[] cases;public AbilityRuleFixture[] rules; }
    public static class NativePresentationChecks
    {
        private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Near(float actual,float expected,string field)
        {if(!MathUtil.Finite(actual)||Math.Abs(actual-expected)>.0003f)throw new Exception(field+": "+actual+" != "+expected);}
        public static List<string> Run(ConversionContent content,PresentationFixtures fixtures)
        {
            Check(content.sourceCommit==fixtures.commit,"Presentation source commit mismatch");
            var results=new List<string>();int poses=0;
            foreach(var sample in fixtures.cases)
            {
                var actor=new Actor{MagicVisual=sample.element,CastRate=sample.rate,AttackAt=sample.attack,LastShot=sample.shot,AbilityId=sample.ability,AbilityAt=sample.abilityAt,AbilityRelease=sample.releaseAt};
                var output=new float[13];float weight=MagicPose.Sample(actor,sample.time,sample.pitch,sample.first,output);
                Near(weight,sample.weight,"Magic pose weight "+poses);
                for(int i=0;i<13;i++)Near(output[i],sample.pose[i],"Magic channel "+poses+" / "+i);
                // Rig adaptation must remain finite and normalized through extreme pitch and accelerated casts.
                var rig=new CapturedRig.Pose();actor.Pitch=sample.pitch*180/(float)Math.PI;
                MagicPose.Apply(rig,actor,sample.time,sample.first,output);
                for(int i=0;i<CapturedRig.Bones.Length;i++)
                {Near(rig.Rotation[i].Length(),1,"Magic joint quaternion");Check(MathUtil.Finite(rig.WorldPosition[i].X)&&MathUtil.Finite(rig.WorldPosition[i].Y)&&MathUtil.Finite(rig.WorldPosition[i].Z),"Magic joint not finite");}
                poses++;
            }
            results.Add(poses+" original-source magic phrases: four elements, Q/E, first/third person, pitch, rate, streak and release frames");
            foreach(var f in fixtures.rules)
            {
                var d=content.Ability(f.spec,f.slot);var s=new ChipStats(f.stats);
                Near(NativeAbilityRules.CastTime(d.windup,s),f.windup,"Ability windup");Near(NativeAbilityRules.Cooldown(d,s),f.cooldown,"Ability cooldown");
                Near(NativeAbilityRules.Cost(d,s),f.cost,"Ability cost");Near(NativeAbilityRules.Radius(d,s),f.radius,"Ability radius");
                Near(NativeAbilityRules.Scale(s,true),f.scale,"Ability power");Near(NativeAbilityRules.Scale(s,true,true),f.echo,"Ability echo scale");Check(NativeAbilityRules.Charges(s)==f.charges,"Ability charge cap");
            }
            results.Add(fixtures.rules.Length+" original-source ability rule cases: all eleven handlers and stat clamps");
            Check(NativeAbilityRules.Decide(true,true,false,16,16,0,false)==NativeAbilityRules.Press.Cast,"Exact mana must allow cast");
            Check(NativeAbilityRules.Decide(true,true,false,15.99f,16,0,false)==NativeAbilityRules.Press.Refused,"Insufficient mana accepted");
            Check(NativeAbilityRules.Decide(true,true,false,100,0,.3f,false)==NativeAbilityRules.Press.Buffered,"Cooldown buffer boundary");
            Check(NativeAbilityRules.Decide(true,true,false,100,0,.301f,false)==NativeAbilityRules.Press.Refused,"Cooldown beyond buffer accepted");
            Check(NativeAbilityRules.Decide(true,true,false,100,0,0,true)==NativeAbilityRules.Press.Buffered,"Busy primary must buffer");
            Check(NativeAbilityRules.Decide(true,true,true,100,0,0,false)==NativeAbilityRules.Press.Refused,"Block must refuse");
            Check(NativeAbilityRules.Decide(true,false,false,100,0,0,false)==NativeAbilityRules.Press.Refused,"Dead cast accepted");
            Check(NativeAbilityRules.Decide(false,true,false,100,0,0,false)==NativeAbilityRules.Press.Refused,"ADS accepted as cast");
            var input=new InputFrame{AbilityQ=true,AbilityE=true,AbilityEReleased=true,AbilityEHeld=true,Aim=true};var held=input.HeldOnly();
            Check(!held.AbilityQ&&!held.AbilityE&&!held.AbilityEReleased&&held.AbilityEHeld&&held.Aim,"Fixed ticks replay ability edges");
            input.AbilityTarget=new V3(float.NaN,0,0);Check(!input.Valid,"Invalid ability point accepted");
            Near(NativeAbilityRules.BlastFalloff(0,3),1,"Blast centre");Near(NativeAbilityRules.BlastFalloff(3.4f,3),0,"Blast edge");
            results.Add("Ability refusal/buffer/resource boundaries, held input edges, invalid aim rejection and blast boundary");
            Check(NativeViewRules.FirstPerson(true,true,false)&&!NativeViewRules.FirstPerson(true,false,false)&&!NativeViewRules.FirstPerson(true,true,true)&&!NativeViewRules.FirstPerson(false,true,false),"Camera lifecycle");
            Check(!NativeViewRules.ViewModelBone(CapturedRig.Head)&&!NativeViewRules.ViewModelBone(CapturedRig.Chest)&&NativeViewRules.ViewModelBone(CapturedRig.LHand)&&NativeViewRules.ViewModelBone(CapturedRig.RElbow),"First-person visibility");
            var original=new Actor{Pitch=35,Yaw=72};var copy=original.ViewCopy();copy.Pitch=0;copy.Yaw=0;Check(original.Pitch==35&&original.Yaw==72,"View model changed authority");
            foreach(int e in new[]{1,2,3,4})
            {
                var a=new Actor{MagicVisual=e};var pose=new CapturedRig.Pose();MagicPose.Apply(pose,a,0,false,new float[13]);
                Check(Vector3.Transform(CapturedRig.Z,pose.WorldRotation[CapturedRig.LHand]).Y>.9f,"Staff idle not upright in left hand");
                a.BlockUntil=1;MagicPose.Apply(pose,a,0,false,new float[13]);
                Check(Vector3.Transform(CapturedRig.Z,pose.WorldRotation[CapturedRig.LHand]).X<-.9f,"Staff guard not across chest");
            }
            results.Add("First-person lifecycle/visibility, authority isolation, left-hand upright staff and horizontal guard");
            return results;
        }
    }
}
#endif
