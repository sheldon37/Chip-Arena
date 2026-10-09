#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ExtraPresentationChecks
    {
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/extra-presentation-parity.json")));int count=0;var p=new CapturedRig.Pose();
            void Near(float a,float b,string name){if(!MathUtil.Finite(a)||Math.Abs(a-b)>.00002f)throw new Exception("Source presentation "+count+" "+name+": "+a+" != "+b);}
            foreach(var s in document.RootElement.GetProperty("summons").EnumerateArray())
            {NativeSummonPose.Sample(p,s.GetProperty("kind").GetString()=="toy",s.GetProperty("t").GetSingle(),s.GetProperty("speed").GetSingle(),s.GetProperty("age").GetSingle(),s.GetProperty("hit").GetSingle(),5);var v=s.GetProperty("values");for(int i=0;i<22;i++){int n=i*7;Near(Vector3.Distance(p.Position[i],new Vector3(v[n].GetSingle(),v[n+1].GetSingle(),v[n+2].GetSingle())),0,"summon joint "+i);Near(1-Math.Abs(Quaternion.Dot(p.Rotation[i],new Quaternion(v[n+3].GetSingle(),v[n+4].GetSingle(),v[n+5].GetSingle(),v[n+6].GetSingle()))),0,"summon rotation "+i);}count++;}
            foreach(var s in document.RootElement.GetProperty("machines").EnumerateArray())
            {float age=s.GetProperty("age").GetSingle();Near(NativeMachineRules.Flap(age),s.GetProperty("flap").GetSingle(),"flap");Near(NativeMachineRules.Door(age),s.GetProperty("door").GetSingle(),"pod door");Near(NativeMachineRules.Deny(age),s.GetProperty("deny").GetSingle(),"denial");Near(NativeMachineRules.Activity(age,false),s.GetProperty("act").GetSingle(),"purchase light");Near(NativeMachineRules.Activity(age,true),s.GetProperty("med").GetSingle(),"medical light");var pickup=NativeMachineRules.Pickup(new V3(0,1.02f,.27f),new V3(0,.9f,1.5f),.6f,age);var q=s.GetProperty("pickup");if(q.GetProperty("visible").GetBoolean()!=pickup.visible)throw new Exception("Early pickup reveal");Near(pickup.spin,q.GetProperty("spin").GetSingle(),"pickup spin");Near(pickup.position.y,q.GetProperty("pos").GetProperty("y").GetSingle(),"pickup arc");Near(pickup.position.z,q.GetProperty("pos").GetProperty("z").GetSingle(),"pickup travel");count++;}
            foreach(var s in document.RootElement.GetProperty("drafts").EnumerateArray())
            {NativeDraftRules.Pose(p,s.GetProperty("time").GetSingle());var target=s.GetProperty("target");if(target.ValueKind!=JsonValueKind.Null)NativeDraftRules.Look(p,new Vector3(target[0].GetSingle(),target[1].GetSingle(),target[2].GetSingle()),.8f);var v=s.GetProperty("values");for(int i=0;i<22;i++){int n=i*7;Near(Vector3.Distance(p.Position[i],new Vector3(v[n].GetSingle(),v[n+1].GetSingle(),v[n+2].GetSingle())),0,"draft joint "+i);Near(1-Math.Abs(Quaternion.Dot(p.Rotation[i],new Quaternion(v[n+3].GetSingle(),v[n+4].GetSingle(),v[n+5].GetSingle(),v[n+6].GetSingle()))),0,"draft rotation "+i);}count++;}
            foreach(var s in document.RootElement.GetProperty("grabs").EnumerateArray()){float a=s.GetProperty("age").GetSingle();Near(NativeDraftRules.Reach(a),s.GetProperty("reach").GetSingle(),"draft reach");Near(NativeDraftRules.Fly(a),s.GetProperty("fly").GetSingle(),"draft fly");Near(NativeDraftRules.Fade(a),s.GetProperty("fade").GetSingle(),"draft fade");Near(NativeDraftRules.Pull(a),s.GetProperty("pull").GetSingle(),"draft pull");count++;}
            // The held chip (draft-stage.ts chipModel, capped at .13 m under the .145 m palm) drawn with the .8 m pickup model
            // stays hand-sized while it rides the hand (NativeDraftPlant; DraftPlantChecks covers the plant).
            Near(NativeDraftPlant.PickupWidth*NativeDraftPlant.HeldChipScale(.48f,3,true),.065f,"held chip at contact");Near(NativeDraftPlant.PickupWidth*NativeDraftPlant.HeldChipScale(.7f,0,false),.13f,"held chip");
            for(float t=0;t<2;t+=.05f){float w=NativeDraftPlant.PickupWidth*NativeDraftPlant.HeldChipScale(.75f,t,false);if(w>.13f*1.08f+1e-5f||w<.13f*.92f-1e-5f)throw new Exception("held chip width "+w);}
            Console.WriteLine("PASS "+count+" original summon joint, machine/pickup, draft pose/look and grab timing samples");
        }
    }
}
#endif
