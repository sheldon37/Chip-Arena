#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The one body predicate and the joint-frame resolver (docs/VOXEL-RETIREMENT.md 2.2 and V1; Core/NativeBodyStyle.cs,
    // Runtime ArenaPresentation.ModelBody and JointFrame). Checked: every case of 2.2's table; the runtime's draw paths ask the
    // predicate instead of the old scattered gates (Classic, the bake, the Siren), Classic itself read only by the predicate and
    // the deploy cast (W3's file, until it merges) and the draft's test (beside the chip install's lines); the resolver answers
    // the model's joint while the model is drawn, the rig's otherwise; the model draws with the voxel pilot set unbuilt (the
    // bank stays the drawer until V12).
    internal static class BodyStyleChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Body style: "+what);count++;}
        public static void Run(string root)
        {
            count=0;var costume=NativeVoxelCostume.With("",true);
            // 2.2's table.
            Check(NativeBodyStyle.Pilot(true,false,NativeBodyStyle.MapBodies.None,"sword","")==NativeBodyStyle.Draw.Model,"a pilot with the bake: the model in its outfit");
            Check(NativeBodyStyle.Pilot(true,false,NativeBodyStyle.MapBodies.None,"sword",costume)==NativeBodyStyle.Draw.Costume,"a pilot in the costume: the model wearing it");
            Check(NativeBodyStyle.Pilot(true,false,NativeBodyStyle.MapBodies.Costume,"sword","")==NativeBodyStyle.Draw.Costume,"a costume map: the costume, forced");
            Check(NativeBodyStyle.Pilot(true,false,NativeBodyStyle.MapBodies.Classic,"sword",costume)==NativeBodyStyle.Draw.Voxel,"a classic map: the voxel style on the rig");
            Check(NativeBodyStyle.Pilot(true,false,NativeBodyStyle.MapBodies.Costume,NativeTopDog.Spec,costume)==NativeBodyStyle.Draw.ClassBody&&NativeBodyStyle.Pilot(true,false,NativeBodyStyle.MapBodies.Costume,"siren","")==NativeBodyStyle.Draw.Voxel,"a class body wins over the costume and the map (the Top Dog's Hot Dog; the Siren's own, voxel until V11)");
            Check(NativeBodyStyle.Pilot(true,true,NativeBodyStyle.MapBodies.None,"sword",costume)==NativeBodyStyle.Draw.Voxel,"Classic (this PC, until O3): the voxel style");
            Check(NativeBodyStyle.Pilot(false,false,NativeBodyStyle.MapBodies.None,"sword",costume)==NativeBodyStyle.Draw.Voxel&&NativeBodyStyle.Pilot(false,false,NativeBodyStyle.MapBodies.Costume,NativeTopDog.Spec,"")==NativeBodyStyle.Draw.Voxel,"no bake (or --ca-voxel-bodies): the voxel style, in its variant");
            Check(NativeBodyStyle.ModelBody(true,false,"sword")&&NativeBodyStyle.ModelBody(true,false,null)&&!NativeBodyStyle.ModelBody(true,false,"siren")&&!NativeBodyStyle.ModelBody(false,false,"sword")&&!NativeBodyStyle.ModelBody(true,true,"sword")&&NativeBodyStyle.ModelBody(true,false,NativeTopDog.Spec),"ModelBody over every input");
            Check(NativeBodyStyle.ZombieModel(true,false,false)&&!NativeBodyStyle.ZombieModel(false,false,false)&&!NativeBodyStyle.ZombieModel(true,true,false)&&!NativeBodyStyle.ZombieModel(true,false,true),"zombies: the bake, not Classic, not --ca-voxel-zombies");
            // The runtime asks the predicate.
            string A(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            var vbp=A("Runtime/NativeVisualBodyPresentation.cs");
            Check(vbp.Contains("public bool ModelBody(Actor a)=>a!=null&&ModelBody(a.SourceSpec);")&&vbp.Contains("private bool ModelLook(string spec)=>NativeBodyStyle.ModelBody(VisualBodiesOn,ClassicBodies,spec);")&&vbp.Contains("private bool ModelBody(string spec)=>!Viewer&&sourceModels!=null&&ModelLook(spec);"),"ModelBody: the predicate over the bake, Classic and the class");
            Check(vbp.Contains("if(!ModelBody(a)||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig))return;")&&vbp.Contains("bool on=ModelBody(a)&&!scoped&&view!=null;")&&vbp.Contains("private bool VisualDeathFor(NativeDeath death)=>(death.model??\"pilot\")==\"pilot\"&&ModelLook(death.actor?.SourceSpec);"),"the live, first-person and death paths ask it");
            // Classic is read by the predicate, the setter and its declaration; the deploy cast keeps its own until W3 merges (one
            // writer a branch) and the draft's test sits beside the chip install's lines (V4 moves both).
            var users=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Select(f=>(Path.GetFileName(f),Regex.Matches(File.ReadAllText(f),@"\bClassicBodies\b").Count)).Where(x=>x.Item2>0).ToDictionary(x=>x.Item1,x=>x.Item2);
            int total=users.Values.Sum();
            // (A0's native hub load preview, merged with release 1, only writes Classic off to force the model for its frame budget.)
            bool previewForces=!users.ContainsKey("NativeExpeditionLoadPreview.cs")||users["NativeExpeditionLoadPreview.cs"]==1&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionLoadPreview.cs")).Contains("view.ClassicBodies=false;");
            Check(total<=8&&previewForces&&users.Keys.All(k=>k=="NativeVisualBodyPresentation.cs"||k=="ConversionGame.cs"||k=="NativeDeployCast.cs"||k=="NativeExpeditionLoadPreview.cs"),"Classic read in "+total+" places ("+string.Join(", ",users.Select(kv=>kv.Key+" "+kv.Value))+"; 25 before; A0's load preview only writes it off)");
            // The joint-frame resolver.
            Check(vbp.Contains("public Matrix4x4 JointFrame(Actor a,int j)=>a==null?Matrix4x4.identity:JointFrame(a.Id,j);")&&vbp.Contains("return rigs.TryGetValue(id,out var rig)?VisualJointFrame(rig,j):Matrix4x4.identity;"),"JointFrame: the model's joint when drawn, the rig's otherwise (VisualJointFrame per actor)");
            // Its first users: the status markers on the model (V3 Stage 1, P13).
            Check(A("Runtime/NativeStatusPresentation.cs").Contains("Vector3 mid=JointAt(id,CapturedRig.Head)+Vector3.up*ModelHeadMiddle;")&&A("Runtime/NativeStatusPresentation.cs").Contains("StatusV(JointAt(id,CapturedRig.LFoot))")&&vbp.Contains("return body.Binding.Bones[body.JointBone[j]].position;"),"the status markers read the model's own bones through the resolver (JointAt)");
            Check(vbp.Contains("if(rig!=null&&visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.JointFrom!=null&&body.PosedFrame==Time.frameCount&&j>=0&&j<body.JointBone.Length&&body.JointBone[j]>=0)"),"drawn this frame: the model's");
            // The model needs no voxel pilot built: the costume's and the model's paths read the voxel bank only for its drawer
            // (gear, hats, weapons, effects), never for the voxel pilot's models (BodyModel, WoundedPilot) to exist.
            var wear=A("Runtime/NativeVoxelCostumePresentation.cs");
            Check(!vbp.Contains("sourceModels.Has(NativeBodySkins")&&!Regex.IsMatch(vbp,@"BodyModel\(")&&wear.Contains("if(!costumeBank.CostumeParts(skin,tier,parts)){CostumeFailed(key,\"the bank has no parts for it yet\");return null;}"),"the model draws with the voxel pilot set unbuilt (only the costume reads the voxel parts: the bank's kept arrays and load-time sculpts)");
            Console.WriteLine("PASS body style: the one predicate's table (outfit, costume, costume map, classic map, class bodies, Classic, no bake; zombies), the draw paths asking it, Classic read in "+total+" places (25 before), and the joint-frame resolver ("+count+" checks)");
        }
    }
}
#endif
