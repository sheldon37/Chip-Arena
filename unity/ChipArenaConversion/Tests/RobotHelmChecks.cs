#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The robot's helmet and half mask (Core/NativeRobotHelm; owner 2026-10-30: "Can we develop essentially a full helmet and a
    // half mask similar to what we had on voxel?"): built with their faces turned out, within their budgets; the helmet clear of
    // the model's head (the head measured in Unity on its head bone, by bearing and height) and every model hat on the helmet
    // clear of its shell; the half mask clear of the face it covers; their picks in the skin map's "face" entry and the Locker's
    // MASKS row; and the runtime's wiring (the hair hidden under the helmet, a hat on it, the suit's ear modules left off,
    // corpses and death scenes). The look was checked on the owner's PC (DEV_LOG).
    internal static class RobotHelmChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Robot helmet: "+what);count++;}
        // The model's head (Head_AnimeYoung with its eyes, lashes and brows) round a vertical axis at z .02 in its head bone's
        // frame: its reach (m) every 15 degrees from ahead (0) to behind (12) at each height from -.04 to .21 (1 cm rows; 0 where
        // nothing of it lies), measured in Unity on 2026-10-30.
        private const float HeadZ=.02f,HeadY0=-.04f;
        private static readonly float[] HeadReach={
            .102f,.022f,.024f,.024f,.022f,.022f,.025f,.029f,.034f,.047f,.049f,.050f,.050f,
            .105f,.098f,.072f,.032f,.032f,.028f,.027f,.030f,.034f,.047f,.049f,.050f,.050f,
            .107f,.103f,.089f,.076f,.045f,.037f,.030f,.032f,.037f,.040f,.042f,.044f,.042f,
            .113f,.107f,.097f,.088f,.057f,.048f,.036f,.036f,.038f,.040f,.042f,.041f,.040f,
            .118f,.110f,.104f,.091f,.073f,.048f,.044f,.042f,.042f,.044f,.044f,.044f,.043f,
            .120f,.112f,.106f,.094f,.087f,.057f,.051f,.050f,.051f,.051f,.051f,.051f,.051f,
            .126f,.115f,.107f,.100f,.088f,.074f,.059f,.060f,.061f,.061f,0,.061f,.061f,
            .131f,.113f,.106f,.102f,.089f,.083f,.087f,0,.074f,.074f,0,.074f,.074f,
            .131f,.105f,.104f,.103f,.095f,.085f,.099f,.068f,.074f,0,0,0,.087f,
            .111f,.096f,.101f,.101f,.096f,.090f,.100f,.077f,.085f,.088f,0,.089f,.087f,
            .107f,.098f,.098f,.105f,.101f,.090f,.107f,.104f,0,.100f,0,.095f,.095f,
            .108f,.103f,.108f,.107f,.104f,.093f,.109f,.090f,.096f,.100f,.101f,.100f,.100f,
            .109f,.106f,.108f,.110f,.107f,.096f,.106f,.094f,0,0,0,0,0,
            .111f,.111f,.112f,.113f,.107f,.094f,.094f,.094f,.104f,.109f,.108f,.105f,.103f,
            .113f,.112f,.113f,.113f,.109f,.100f,.093f,0,0,0,0,0,0,
            .114f,.113f,.112f,.112f,.109f,.097f,.096f,.099f,.108f,.111f,.110f,.106f,.104f,
            0,0,.111f,.112f,.103f,.103f,0,0,0,0,0,.106f,.104f,
            .111f,.109f,.111f,.109f,.106f,.098f,.096f,.098f,.106f,.108f,0,0,0,
            .111f,0,.106f,.108f,.104f,.091f,0,0,0,.108f,.107f,.103f,.101f,
            0,0,.103f,.101f,.094f,.094f,.090f,.094f,.102f,.102f,0,0,0,
            .099f,.099f,0,.096f,.095f,.087f,0,0,.096f,.096f,.100f,.095f,.093f,
            0,0,.091f,.086f,0,0,.081f,.086f,.092f,.094f,0,0,0,
            .081f,.083f,.087f,.077f,.075f,.069f,0,0,.084f,.084f,.088f,.083f,.080f,
            .081f,.068f,.068f,.077f,.061f,.069f,0,.069f,.075f,.075f,.066f,.066f,0,
            .066f,0,.046f,0,.061f,.050f,0,.052f,.061f,0,.066f,.066f,.061f,
            .039f,0,0,0,.028f,0,0,0,.029f,0,.045f,0,.038f};
        // The head's measured points, both sides.
        private static System.Collections.Generic.IEnumerable<Vector3> Head()
        {
            for(int r=0;r<HeadReach.Length/13;r++)for(int c=0;c<13;c++)
            {
                float reach=HeadReach[r*13+c];if(reach<=0)continue;float th=c*MathF.PI/12;
                foreach(float side in new[]{-1f,1f})yield return new Vector3(side*MathF.Sin(th)*reach,HeadY0+r*.01f,HeadZ+MathF.Cos(th)*reach);
            }
        }
        public static void Run(string root)
        {
            foreach(var (name,parts,budget) in new[]{("helmet",NativeRobotHelm.Helm(),9000),("half mask",NativeRobotHelm.Half(),3000)})
            {
                int faces=0,wrong=0;bool finite=true;
                foreach(var s in parts)
                {
                    finite&=s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>float.IsFinite(n.X)&&float.IsFinite(n.Y)&&float.IsFinite(n.Z));
                    for(int i=0;i<s.Indices.Count;i+=3){int a=s.Indices[i],b=s.Indices[i+1],c=s.Indices[i+2];var n=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]);faces++;if(Vector3.Dot(n,s.Normals[a]+s.Normals[b]+s.Normals[c])<0)wrong++;}
                }
                Check(parts.Length==1&&parts[0].Bone==CapturedRig.Head&&finite&&wrong==0&&faces>1000&&faces<budget,name+": one part on the head, built ("+faces+" triangles, "+wrong+" turned in), within "+budget);
                // Mirrored across: every vertex has its twin at -x.
                var ps=parts[0].Positions;
                Check(ps.All(p=>ps.Any(q=>Vector3.DistanceSquared(q,new Vector3(-p.X,p.Y,p.Z))<1e-10f)),name+": even side to side");
                var tags=parts[0].Emission.Select(e=>(int)MathF.Round(e.W)).Distinct().ToList();
                Check(tags.Contains(0)&&tags.Contains(1)&&tags.Contains(2)&&!tags.Contains(3),name+": its shell in the body colour, its light in the accent, its slots and metal plain");
            }
            // The helmet round the head: every measured point above its foot 2 mm or more inside its inner face (the dumped head's
            // own vertices clear it by 3.2 mm or more; the table puts each bearing's farthest point at its middle).
            int inside=0,covered=0;
            foreach(var p in Head())
            {
                if(p.Y<NativeRobotHelm.Foot(MathF.Atan2(p.X,p.Z-NativeRobotHelm.AxisZ)))continue;covered++;
                if(NativeRobotHelm.Inside(p,-NativeRobotHelm.Thick-.002f)<1)inside++;
            }
            Check(covered>250&&inside==covered,"the helmet clears the head by 2 mm or more ("+inside+" of "+covered+" points)");
            Check(NativeRobotHelm.Foot(0)<-.06f&&NativeRobotHelm.Foot(MathF.PI)>-.02f&&NativeRobotHelm.Width(.065f)>.118f,"open under the jaw (its foot under the chin ahead, at the nape behind), past the ears");
            // The visor's glass lies over the eyes (y .044-.08).
            var helm=NativeRobotHelm.Helm()[0];var lit=Enumerable.Range(0,helm.Positions.Count).Where(i=>helm.Emission[i].W==2&&helm.Positions[i].Z>.1f).Select(i=>helm.Positions[i]).ToList();
            Check(lit.Count>50&&lit.Min(p=>p.Y)>.04f&&lit.Max(p=>p.Y)<.09f&&lit.Max(p=>p.X)>.09f,"the visor's glass across the eyes");
            // Every model hat on the helmet clears its shell by 2 mm (a cone's elastic aside).
            foreach(var hat in NativeModelHatMeshes.Hats)
            {
                int under=0;
                foreach(var s in NativeModelHatMeshes.Build(hat,false))foreach(var p in s.Positions){if(p.Y<(hat=="cone"?.187f:.06f))continue;if(NativeRobotHelm.Inside(NativeRobotHelm.HatOn(hat,p),.002f)<1)under++;}
                Check(under==0,hat+" on the helmet clears it ("+under+" points within 2 mm)");
            }
            NativeRobotHelm.HatFit("cap",out float capScale,out _);NativeRobotHelm.HatFit("halo",out float haloScale,out _);
            Check(capScale>1.05f&&capScale<1.2f&&haloScale==1,"a closed hat a little larger on the helmet, the halo as it is");
            // The half mask over the face it covers: 5 mm or more off it.
            int close=0,cov=0;
            foreach(var p in Head())
            {
                var plate=NativeRobotHelm.Plate(MathF.PI/2,p.Y);float A=plate.X,F=NativeRobotHelm.Plate(0,p.Y).Z-NativeRobotHelm.HalfZ;
                float dx=p.X,dz=p.Z-NativeRobotHelm.HalfZ,r=MathF.Sqrt(dx*dx+dz*dz),th=MathF.Atan2(dx,dz),R=1/MathF.Sqrt(MathF.Sin(th)*MathF.Sin(th)/(A*A)+MathF.Cos(th)*MathF.Cos(th)/(F*F)),t=MathF.Atan2(R*MathF.Sin(th)/A,R*MathF.Cos(th)/F);
                if(MathF.Abs(t)>NativeRobotHelm.HalfReach*MathF.PI/180||p.Y<NativeRobotHelm.HalfFoot(t)||p.Y>NativeRobotHelm.HalfTop(t))continue;cov++;
                if(R-r-NativeRobotHelm.HalfThick*.5f<.005f)close++;
            }
            Check(cov>40&&close==0,"the half mask clears the face by 5 mm or more ("+close+" of "+cov+" points closer)");
            Check(NativeRobotHelm.HalfTop(MathF.PI*20/180)<.044f&&NativeRobotHelm.HalfFoot(0)<-.055f&&NativeRobotHelm.HalfEar.X-NativeRobotHelm.EarHalf.X>.108f,"under the eyes, under the chin, its ear modules clear of the ears");
            // The picks: the face entry's two more keys, no longer than the visor's (the longest map holds).
            Check(NativeWeaponSkins.FaceKeys.SequenceEqual(new[]{"mask-visor","visor","mask","helm","half"})&&NativeWeaponSkins.FaceKeys.Skip(1).All(k=>k.Length<="visor".Length)
                &&NativeWeaponSkins.Normalize("face:helm,body:prototype")=="body:prototype,face:helm"&&NativeWeaponSkins.Index("face:half",NativeWeaponSkins.FaceEntry)==NativeWeaponSkins.FaceHalf
                &&NativeWeaponSkins.Index("face:helmet",NativeWeaponSkins.FaceEntry)==NativeWeaponSkins.FaceBoth&&NativeWeaponSkins.With("body:prototype",NativeWeaponSkins.FaceEntry,"helm")=="body:prototype,face:helm","the helmet and half mask picked in the face entry");
            // The runtime.
            string Src(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime",f)).Replace("\r\n","\n");
            string pres=Src("NativeRobotHelmPresentation.cs"),body=Src("NativeVisualBodyPresentation.cs"),bank=Src("NativeSourceModels.cs"),suit=Src("NativeRobotSuitPresentation.cs"),locker=Src("NativeLockerSkinsUI.cs");
            Check(bank.Contains("ModelHats();SuitModels();HelmModels();")&&pres.Contains("models[Helm]=HelmParts(NativeRobotHelm.Helm(),material,\"Robot helmet\");models[HalfMask]=HelmParts(NativeRobotHelm.Half(),material,\"Robot half mask\");"),"both in the bank");
            Check(body.Contains("if(gear==NativeWeaponSkins.FaceHelm||gear==NativeWeaponSkins.FaceHalf)")&&body.Contains("if(sourceModels.Has(model))sourceModels.Queue(model,gearMatrix,gearBone,null,layer,camera,BodyTint(bodyColor),accent,1,reveal,tint,lit,null,flash);"),
                "drawn as the face gear, in the body colour with the accent's light");
            Check(body.Contains("body.Helm=!body.Hood&&WearsHelm(a.Id);")&&body.Contains("if(hat==NativeRobotHelm.Hair)")&&body.Contains("made.SetTriangles(Array.Empty<int>(),i,false);")
                &&body.Contains("string under=hood==null&&death.actor!=null&&WearsHelm(death.id)?NativeRobotHelm.Hair:"),"the hair hidden under the helmet (the live body's, a corpse's)");
            Check(body.Contains("var frame=helm?head.localToWorldMatrix*HelmHatFit(hat):head.localToWorldMatrix;")&&body.Contains("if(head!=null)return VisualHatFit(head,hat,body.Helm);")&&body.Contains("corpseHat=VisualHatFit(c.Bones[c.Head],hat,!c.Hood&&WearsHelm(death.id));"),"a hat on the helmet");
            Check(body.Contains("&&WearsHelm(corpse.Death.id)&&VisualCorpseHead(corpse))")&&body.Contains("VisualSceneHood(s,death,alpha);VisualSceneFace(s,death,alpha);")&&body.Contains("VisualSceneHood(s,death,alpha);if(visible(CapturedRig.Head))VisualSceneFace(s,death,alpha);"),
                "the helmet on a split corpse's head, the face gear on death scenes and revives");
            Check(suit.Contains("FaceGearOf(id)>=NativeWeaponSkins.FaceHelm?SuitNoEars:null"),"the suit's ear modules left off under either");
            Check(locker.Contains("\"Helmet\",\"Half mask\"}")&&locker.Contains("if(gear!=NativeWeaponSkins.FaceHelm&&gear!=NativeWeaponSkins.FaceHalf)return;"),"the Locker's MASKS row offers both, with their thumbnails");
            Console.WriteLine("PASS robot helmet ("+count+" checks)");
        }
    }
}
#endif
