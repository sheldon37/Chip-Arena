using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;
// The sleek face gear (owner 2026-11-27: "think we need to update the "masks" section now, they look out of place now with the
// latest robot armor", "They look to bulky / wide"; Core/NativeSleekFace.cs, docs/PILOT-BODY-HAIR.md section 6): the bank's plates
// named by their gear become the helmet, half mask, mask and visor models and the suit's ear pods, never suit plates; a hat on the
// sleek helmet clears its panels; without the bank everything is as before.
internal static class SleekFaceChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Sleek face: "+why);}
        string Src(string rel)=>File.ReadAllText(Path.Combine(root,rel));
        // a plate of a quad (two triangles) at height y, painted p
        NativeHairBank.Plate Plate(string name,int bone,float y,byte p)=>new NativeHairBank.Plate{Name=name,Bone=bone,Positions=new[]{0,y,0f,.01f,y,0,.01f,y,.01f,0,y,.01f},Normals=new[]{0,1f,0,0,1,0,0,1,0,0,1,0},Paint=new[]{p,p,p,p},Triangles=new[]{0,1,2,0,2,3}};
        var chest=Plate("chest",0,1.3f,NativeHairBank.PaintShell);
        var gear=new[]{chest,Plate("Helmet: crown",3,.2f,NativeHairBank.PaintShell),Plate("Helmet: visor",3,.08f,NativeHairBank.PaintSoft),Plate("Half mask: muzzle",3,0,NativeHairBank.PaintShell),
            Plate("Visor: glass",3,.1f,NativeHairBank.PaintSoft),Plate("Mask: shell",3,0,NativeHairBank.PaintDark),Plate("Ears: R",3,1.63f,NativeHairBank.PaintLight),Plate("Ears: L",3,1.63f,NativeHairBank.PaintShell)};
        Check(NativeSleekFace.IsFace("Helmet: crown")&&NativeSleekFace.IsFace("Ears: L")&&!NativeSleekFace.IsFace("chest")&&!NativeSleekFace.IsFace("helmet")&&!NativeSleekFace.IsFace(null),"a gear's plates by their names");
        Check(NativeSleekFace.HasAll(gear)&&!NativeSleekFace.HasAll(new[]{chest})&&!NativeSleekFace.HasAll(gear.Where(p=>!p.Name.StartsWith("Mask: ")).ToArray())&&!NativeSleekFace.HasAll(null),"every gear, or none of it");
        // the gear's models: each gear's plates one part on the head, the triangles of each plate after the one before
        var helm=NativeSleekFace.Sections(gear,NativeSleekFace.Helmet,"helmet");
        Check(helm.Length==1&&helm[0].Part=="helmet"&&helm[0].Bone==CapturedRig.Head&&helm[0].Positions.Count==8&&helm[0].Indices.Count==12,"the helmet's plates as one part on the head bone");
        Check(helm[0].Indices.Skip(6).All(i=>i>=4)&&helm[0].Indices.Take(6).All(i=>i<4),"each plate's triangles on its own vertices");
        Check(helm[0].Emission.Take(4).All(e=>e.W==1)&&helm[0].Emission.Skip(4).All(e=>e.W==2),"its shell in the body colour, its glass in the accent's light");
        Check(NativeSleekFace.Sections(gear,NativeSleekFace.Mask,"mask")[0].Emission.All(e=>e.W==0)&&NativeSleekFace.Sections(new[]{chest},NativeSleekFace.Mask,"mask").Length==0,"the mask's dark plain; no gear, no part");
        // the suit's parts: never the gear; the bank's ear pods in place of the ear modules
        var suit=NativeRobotSuit.Build(gear);
        Check(suit.Count(s=>s.Part=="chest")==1&&!suit.Any(s=>NativeSleekFace.IsFace(s.Part)),"the suit wears its plates, never the face gear's");
        var ears=suit.Single(s=>s.Part=="ears");
        Check(ears.Bone==NativeRobotSuit.Head&&ears.Positions.Count==8&&ears.Positions.All(p=>p.Y==1.63f),"its ear pods the bank's, on the head");
        var old=NativeRobotSuit.Build(new[]{chest}).Single(s=>s.Part=="ears");
        Check(old.Positions.Count>8&&old.Positions.Any(p=>p.Y!=1.63f),"an older bank: today's ear modules");
        // a hat on the sleek helmet: clear of its panels by 2 mm (a cone's elastic aside); the robot helmet's fit otherwise
        bool was=NativeSleekFace.On;
        try
        {
            NativeSleekFace.On=false;NativeRobotHelm.HatFit("cap",out float oldCap,out _);Check(oldCap==1.12f,"without the bank: the robot helmet's fit");
            NativeSleekFace.On=true;
            foreach(var hat in NativeModelHatMeshes.Hats)
            {
                int under=0,count=0;
                foreach(var s in NativeModelHatMeshes.Build(hat,false))foreach(var p in s.Positions){if(p.Y<(hat=="cone"?.187f:.06f))continue;count++;if(NativeSleekFace.Inside(NativeRobotHelm.HatOn(hat,p),NativeSleekFace.Panels+.002f)<1)under++;}
                Check(count>100&&under==0,hat+" on the sleek helmet clears it ("+under+" points within 2 mm)");
            }
            NativeRobotHelm.HatFit("cap",out float cap,out var capMove);NativeRobotHelm.HatFit("halo",out float halo,out _);
            Check(cap<oldCap&&cap>1&&capMove.Y<.03f&&halo==1,"a closed hat smaller and lower on the slimmer helmet, the halo as it is");
        }
        finally{NativeSleekFace.On=was;}
        // the shell: close round the head (the robot helmet's measured head: ears 10.8 cm out, the nose 15.1 cm ahead, the skull's
        // back 8.4 cm behind, the crown 21.2 cm up) and well inside the robot helmet's
        Check(NativeSleekFace.Inside(new Vector3(0,.212f,.02f),-.005f)<1&&NativeSleekFace.Inside(new Vector3(0,.02f,.151f),-.005f)<1&&NativeSleekFace.Inside(new Vector3(0,.1f,-.084f),-.005f)<1,"the shell 5 mm or more over the crown, the nose and the skull's back");
        Check(NativeSleekFace.Inside(new Vector3(.108f,.12f,.02f),0)>1&&NativeRobotHelm.Inside(new Vector3(NativeSleekFace.Side+NativeSleekFace.Panels,.07f,NativeSleekFace.AxisZ),0)<1,"slimmer than the robot helmet at the sides (the ears under their cups)");
        // the presentation (source)
        var hb=Src("Assets/ChipArena/Runtime/NativeHairBankPresentation.cs");var helmSrc=Src("Assets/ChipArena/Runtime/NativeRobotHelmPresentation.cs");var gearSrc=Src("Assets/ChipArena/Runtime/NativePilotGearModels.cs");var body=Src("Assets/ChipArena/Runtime/NativeVisualBodyPresentation.cs");
        Check(hb.Contains("NativeSleekFace.On=hairBank!=null&&NativeSleekFace.HasAll(hairBank.Plates);"),"the sleek gear only when the bank carries all of it");
        Check(helmSrc.Contains("if(NativeSleekFace.On){models[Helm]=HelmParts(NativeSleekFace.Sections(plates,NativeSleekFace.Helmet,\"helmet\"),material,\"Robot helmet\");")&&helmSrc.Contains("else{models[Helm]=HelmParts(NativeBootSculpts.Take(NativeBootSculpts.Helm,NativeRobotHelm.Helm),material,\"Robot helmet\");"),"the helmet and half mask the bank's, else the robot's");
        Check(gearSrc.Contains("if(NativeSleekFace.On){models[GearMask]=HelmParts(NativeSleekFace.Sections(plates,NativeSleekFace.Mask,\"mask\"),gearMaterial,\"Pilot mask\");"),"the mask and visor the bank's, else the concept's");
        Check(body.Contains("var shell=NativeSleekFace.On?BodyTint(bodyColor):Color.white;"),"the sleek mask's and visor's shells in the body colour");
        // the tools: the same names, the bank's version 4 with them
        var face=Src("tools/gear/facegear.py");var tool=Src("tools/gear/hair.py");
        foreach(var g in new[]{NativeSleekFace.Helmet,NativeSleekFace.HalfMask,NativeSleekFace.Visor,NativeSleekFace.Mask,NativeSleekFace.Ears})Check(face.Contains("Part('"+g)&&tool.Contains("'"+g+"'"),"the tools name the gear as the game does: "+g);
        Check(tool.Contains("data = b'HAIR' + struct.pack('<i', 4 if face else 3) + z.compress(out.getvalue()) + z.flush()")&&NativeHairBank.Version==4&&NativeHairBank.Suit==3,"the face gear in a version 4 bank");
        Console.WriteLine("PASS sleek face");
    }
}
