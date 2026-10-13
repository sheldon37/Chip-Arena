using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The split-in-two death (owner 2026-10-12: the Factory buzzsaw death splits the body in the middle). Core NativeBisect
    // (the split), NativeSawBisect (which saw, and its plane) and NativeBodyModels.CutLook (each skin's cut face); Runtime
    // NativeBisectModels / NativeBisectCorpse / NativeBisectPresentation (text wiring only: no Unity here). Every skin and
    // tier, with its hat and through a wound: deterministic, sealed (no ray from outside first meets a back face), both halves
    // together the original's volume and silhouette, each cap the section's exact area, the plane the killing blade's axle
    // through the pelvis for both saws, gore off falling back to the current death, and nothing allocating after the split.
    internal static class BisectChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Bisect: "+what);count++;}
        internal sealed class Model{public string Name;public int Bone;public WeaponSection Mesh;public bool Clear;}

        public static void Run(string root)
        {
            count=0;var report=new List<string>();var options=new JsonSerializerOptions{IncludeFields=true};
            var bank=Bank(root);var woundParts=JsonSerializer.Deserialize<NativeWoundParts>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/WoundParts.json")),options).parts;
            FactoryHazard[] hazards=null;
            using(var maps=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"))))
                foreach(var m in maps.RootElement.GetProperty("maps").EnumerateArray())if(m.GetProperty("id").GetString()=="factory")hazards=JsonSerializer.Deserialize<FactoryHazard[]>(m.GetProperty("hazards").GetRawText(),options);
            Check(hazards!=null&&hazards.Count(NativeSawBisect.Saw)==4,"the Factory's four saws load ("+(hazards?.Count(NativeSawBisect.Saw)??0)+")");

            // ---- 1. Which saw killed, and its plane: the blade's axle through the pelvis, for both saw types ----
            var kills=new Dictionary<string,(Vector3 feet,double clock)>();
            for(int i=0;i<hazards.Length;i++)
            {
                var h=hazards[i];if(!NativeSawBisect.Saw(h))continue;bool slot=h.kind==NativeFactoryHazards.SawSlot;
                // A pilot standing in the blade's path (a slot's lane on the floor, a rail's catwalk at 3.6 m), killed on the first
                // tick the live volume reaches it, as the authority's hazard step would.
                var mid=(new Vector3(h.a.x,h.a.y,h.a.z)+new Vector3(h.b.x,h.b.y,h.b.z))*.5f;var feet=slot?new Vector3(mid.X+1,0,mid.Z):new Vector3(mid.X,3.6f,mid.Z-Math.Sign(mid.Z)*.6f);
                var body=new V3(feet.X,feet.Y+NativeMeleeSlice.RootHeight,feet.Z);double clock=double.NaN;
                for(int t=0;t<60*20&&double.IsNaN(clock);t++){double c=h.start+h.offset+t/60.0;var s=NativeFactoryHazards.State(h,0,c);if(NativeSawBisect.Box(h,s,out var bc,out var bs)&&NativeStageHazards.Touch(body,bc,bs))clock=c;}
                Check(!double.IsNaN(clock),h.id+" reaches a pilot in its path");
                double fightAt=12.5,at=clock+fightAt+NativeFactoryHazards.ArmDelay;
                Check(NativeSawBisect.Find(hazards,0,fightAt,at,body)==i,h.id+": the death record's time and place find the saw that killed it");
                Check(NativeSawBisect.Find(hazards,0,fightAt,(float)at,body)==i,h.id+": also from the record's float time");
                Check(NativeSawBisect.Find(hazards,0,fightAt,at,new V3(body.x+9,body.y,body.z+9))==-1,h.id+": no saw for a body nowhere near one");
                // Odd rounds run the slot blades the other way: still found.
                double odd=double.NaN;for(int t=0;t<60*20&&double.IsNaN(odd);t++){double c=h.start+h.offset+t/60.0;var s=NativeFactoryHazards.State(h,1,c);if(NativeSawBisect.Box(h,s,out var bc,out var bs)&&NativeStageHazards.Touch(body,bc,bs))odd=c;}
                Check(!double.IsNaN(odd)&&NativeSawBisect.Find(hazards,1,fightAt,odd+fightAt+NativeFactoryHazards.ArmDelay,body)==i,h.id+": found in an odd round too");
                var pelvis=feet+new Vector3(.03f,.62f,-.02f);var r=NativeSawBisect.Request(h,0,clock,pelvis,7);var axle=NativeSawBisect.Axle(h);
                Check(Math.Abs(Math.Abs(Vector3.Dot(r.Normal,axle))-1)<1e-6f&&Math.Abs(r.Side(pelvis))<1e-6f,h.id+": the plane is the blade's (normal its axle "+axle+") through the pelvis");
                Check(r.Sparks&&r.Radius==h.radius&&Vector3.Distance(r.Axle,NativeSawBisect.RigAxle(h))<1e-6f&&Math.Abs(r.Rate)>40&&r.Hitch>.1f&&r.Hitch<.2f,h.id+": a spinning metal blade holding the body "+r.Hitch.ToString("0.00")+" s");
                Check(Vector3.Dot(r.Away1-r.Away0,r.Normal)>2.5f&&Vector3.Dot(r.Spin1,Vector3.Cross(Vector3.UnitY,r.Normal))>0&&Vector3.Dot(r.Spin0,Vector3.Cross(Vector3.UnitY,r.Normal))<0,h.id+": the halves part along the normal and tip away from the plane");
                if(slot)Check(Math.Abs(Vector3.Dot(r.Travel,Vector3.Normalize(new Vector3(h.b.x-h.a.x,0,h.b.z-h.a.z))))>5.5f,h.id+": the slot blade's travel ("+r.Travel.Length().ToString("0.0")+" m/s along its lane)");
                kills[h.id]=(feet,clock);
            }
            // The first lethal hazard in the authority's order kills: a body under the crane's landing load is not a saw kill.
            var crane=Array.Find(hazards,x=>x.kind==NativeFactoryHazards.Crane);
            if(crane!=null)
            {
                for(int t=0;t<60*40;t++){double c=crane.start+crane.offset+t/60.0;var s=NativeFactoryHazards.State(crane,0,c);if(!NativeSawBisect.Box(crane,s,out var bc,out var bs))continue;
                    Check(NativeSawBisect.Find(hazards,0,0,c+NativeFactoryHazards.ArmDelay,new V3(bc.x,NativeMeleeSlice.RootHeight,bc.z))==-1,"a crane kill is no saw kill");break;}
            }
            foreach(var h0 in hazards.Where(NativeSawBisect.Saw))
            {
                var st=NativeFactoryHazards.State(h0,0,kills[h0.id].clock);var map=new MapHazard{id=h0.id,kind=h0.kind,a=h0.a,b=h0.b,axis=h0.axis,size=h0.size,radius=h0.radius,lethal=h0.lethal};
                Check(NativeSawBisect.Box(h0,st,out var c1,out var s1)&&NativeStageHazards.FactoryBox(map,st,out var c2,out var s2)&&c1.x==c2.x&&c1.y==c2.y&&c1.z==c2.z&&s1.x==s2.x&&s1.y==s2.y&&s1.z==s2.z,h0.id+": the saw's kill volume is the authority's (NativeStageHazards.FactoryBox)");
            }
            report.Add("saws: each of the 4 found from its victim's record (float time, odd rounds), never a crane kill; plane normal = axle through the pelvis");

            // ---- 2. Applies: gore off, a purchased kill effect, a Siren and a revivable body keep the current death ----
            Check(NativeBisect.Applies(true,"impact-ragdoll",false,"pilot")&&NativeBisect.Applies(true,"ragdoll",false,null)&&NativeBisect.Applies(true,null,false,"pilot"),"a pilot's stock death splits");
            Check(!NativeBisect.Applies(false,"impact-ragdoll",false,"pilot"),"gore off: the current death");
            Check(!NativeBisect.Applies(true,"voxel-burst",false,"pilot")&&!NativeBisect.Applies(true,"ragdoll-soul",false,"pilot")&&!NativeBisect.Applies(true,"dev-gibber",false,"pilot"),"a killer's kill effect keeps its own look");
            Check(!NativeBisect.Applies(true,"impact-ragdoll",true,"pilot")&&!NativeBisect.Applies(true,"siren-dissipate",false,"siren")&&!NativeBisect.Applies(true,"impact-ragdoll",false,"siren"),"a revivable PvE body and a Siren keep theirs");

            // ---- 3. The bodies: every skin and tier, a hat, a wound ----
            var cases=WoundSkinCases.All().ToDictionary(c=>c.Name);var head=WoundSkinCases.Open(cases["head-front-pistol"].Wounds);
            var slotSaw=hazards.First(x=>x.kind==NativeFactoryHazards.SawSlot);var railSaw=hazards.First(x=>x.kind==NativeFactoryHazards.SawRail);
            int bodies=0;double worstVolume=0,worstSilhouette=0,worstExtra=0,worstArea=0;int sweeps=0,rays=0;long capTris=0;double slowest=0;
            var timer=new Stopwatch();
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex,NativeBodySkins.PrototypeIndex})
            for(int tier=0;tier<(skin==NativeBodySkins.PrototypeIndex?1:NativeBodySkins.Tiers);tier++)
            foreach(var (cutName,saw,yaw,wounded) in new[]{("sagittal slot",slotSaw,90f,false),("sagittal slot",slotSaw,90f,true),("coronal rail",railSaw,90f,tier==0),("oblique slot",slotSaw,45f,tier==0)})
            {
                string hat=skin==NativeBodySkins.StandardIndex?"top":skin==NativeBodySkins.SheldonIndex?NativeSheldonHead.Hat:"cowboy";
                string label=NativeBodySkins.Labels[skin]+" "+new[]{"Full","Reduced","Low"}[tier]+" "+cutName+(wounded?" + headshot":"");
                var sources=Sources(bank,woundParts,skin,tier,wounded?head:null,hat,out var looks);
                var (feet,clock)=kills[saw.id];var bones=Pose(feet,yaw);var pelvis=Vector3.Transform(Vector3.Zero,bones[CapturedRig.Pelvis]);
                var r=NativeSawBisect.Request(saw,0,clock,pelvis,11);var plane=new BisectPlane(r.Point,r.Normal);
                timer.Restart();var cut=NativeBisect.Split(sources,bones,plane,looks,tier,0x5a17u);double ms=timer.Elapsed.TotalMilliseconds;if(bodies>0)slowest=Math.Max(slowest,ms);
                var again=NativeBisect.Split(sources,bones,plane,looks,tier,0x5a17u);
                Check(Digest(cut,sources)==Digest(again,sources),label+": the split is deterministic");
                bodies++;capTris+=cut.CapTriangles;
                Check(cut.Bodies[0][CapturedRig.Pelvis]&&cut.Bodies[1][CapturedRig.Pelvis],label+": both halves keep the pelvis");
                var plan=NativeBisect.Plan(sources,bones,plane);
                for(int h=0;h<2;h++)foreach(int b in NativeBisect.Bodies)Check(plan.Bodies[h][b]==cut.Bodies[h][b]&&(!cut.Bodies[h][b]||Within(cut.Min[h][b],cut.Max[h][b],plan.Min[h][b],plan.Max[h][b])),label+": the plan keeps the split's bodies, its bounds holding the split's (half "+h+", joint "+b+": plan "+plan.Bodies[h][b]+" "+plan.Min[h][b]+" "+plan.Max[h][b]+", split "+cut.Bodies[h][b]+" "+cut.Min[h][b]+" "+cut.Max[h][b]+")");
                Check(cut.Pieces.Any(p=>p.Half==0&&p.IsCap)&&cut.Pieces.Any(p=>p.Half==1&&p.IsCap),label+": both halves are capped");
                if(cutName=="sagittal slot")
                {
                    // A limb wholly on one side goes to that half alone.
                    int left=cut.Bodies[0][CapturedRig.LArm]?0:1;
                    foreach(int b in new[]{CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHip,CapturedRig.LKnee})Check(cut.Bodies[left][b]&&!cut.Bodies[1-left][b],label+": "+CapturedRig.Bones[b].Name+" goes to its side's half only");
                    foreach(int b in new[]{CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHip,CapturedRig.RKnee})Check(cut.Bodies[1-left][b]&&!cut.Bodies[left][b],label+": "+CapturedRig.Bones[b].Name+" goes to its side's half only");
                    // The hat is cut too, with a solid cap in its own paint.
                    Check(cut.Pieces.Any(p=>p.Group==1&&p.Half==0)&&cut.Pieces.Any(p=>p.Group==1&&p.Half==1)&&cut.Pieces.Any(p=>p.Group==1&&p.IsCap),label+": the hat is split and capped");
                    foreach(var p in cut.Pieces.Where(p=>p.Group==1&&p.IsCap))Check(p.Mesh.Colors.All(c=>!Flesh(new Vector3(c.X,c.Y,c.Z))),label+": the hat's cap is its own paint, not flesh");
                }
                // Every piece's winding faces out: the caps along their outward normal (half 0 along the plane normal).
                foreach(var p in cut.Pieces.Where(p=>p.IsCap))Check(Outward(p.Mesh),label+": cap of joint "+p.Body+" half "+p.Half+" winds outward");
                // The look: a body cap's cells go from the shell's paint at the edge, through the lining (none on the Prototype), to flesh.
                var caps=cut.Pieces.Where(p=>p.IsCap&&p.Group==0).ToList();
                Check(caps.Any(p=>p.Mesh.Colors.Any(c=>Flesh(new Vector3(c.X,c.Y,c.Z)))),label+": the cut shows flesh");
                if(skin==NativeBodySkins.StandardIndex)Check(caps.Any(p=>p.Mesh.Emission.Any(e=>e.W>1.5f&&e.W<2.5f&&e.X+e.Y+e.Z>.5f)),label+": the Standard's undersuit shows accent conduit glints");
                if(skin==NativeBodySkins.SheldonIndex)Check(caps.Any(p=>p.Mesh.Emission.Any(e=>e.W>.5f&&e.W<1.5f)),label+": the Sheldon Clothes' cut keeps its tinted coat and lining");
                if(skin==NativeBodySkins.PrototypeIndex){var look=NativeBodyModels.CutLook(skin);var suit=NativeBodyModels.CutLook(NativeBodySkins.StandardIndex).InnerColor;Check(look.Inner==0&&look.ConduitShare==0&&caps.All(p=>p.Mesh.Colors.All(c=>Vector3.Distance(new Vector3(c.X,c.Y,c.Z),suit)>1e-4f)),label+": the Prototype's cut goes from its shell straight to flesh (no undersuit, no conduits)");}

                // Sealed: rays across the cut region from nine directions; no half shows a back face first more than the whole body does.
                var whole=World(sources,bones,null,-1);int baseBack=Sweep(whole,r.Normal,pelvis,out int n0);
                for(int h=0;h<2;h++){int back=Sweep(World(sources,bones,cut,h),r.Normal,pelvis,out int n);rays+=n;sweeps++;Check(back<=baseBack+2,label+": half "+h+" is sealed ("+back+" rays of "+n+" meet a back face first; the whole body "+baseBack+")");}
                // Volume: both halves together hold the original's (less the cut's hairline recess).
                double v=Volume(whole,pelvis),v0=Volume(World(sources,bones,cut,0),pelvis),v1=Volume(World(sources,bones,cut,1),pelvis),err=Math.Abs(v0+v1-v)/v;worstVolume=Math.Max(worstVolume,err);
                Check(v0>0&&v1>0&&err<.01,label+": halves "+(v0*1000).ToString("0.00")+" + "+(v1*1000).ToString("0.00")+" L = the body's "+(v*1000).ToString("0.00")+" L (error "+(err*100).ToString("0.00")+"%)");
                // Silhouette: seen from the front, the side and along the cut, the halves cover exactly what the body covered.
                foreach(var d in new[]{r.Normal,Vector3.Normalize(Vector3.Cross(Vector3.UnitY,r.Normal)),Vector3.UnitY})
                {
                    var (extra,missing)=Silhouette(whole,World(sources,bones,cut,0).Concat(World(sources,bones,cut,1)).ToList(),d,pelvis);worstSilhouette=Math.Max(worstSilhouette,extra+missing);worstExtra=Math.Max(worstExtra,extra);
                    // Nothing sticks out (a cap overhanging its section shows along the normal); the cut's hairline recess may open a line of
                    // pixels sideways.
                    Check(extra<.0002&&missing<.006,label+": the halves' silhouette along "+d+" is the body's ("+(extra*100).ToString("0.0000")+"% extra, "+(missing*100).ToString("0.000")+"% missing pixels)");
                }
                // Each cap fills its body's section exactly (the union of its closed meshes' sections, sampled independently).
                foreach(var g in caps.GroupBy(p=>(p.Half,p.Body)))
                {
                    double area=g.Sum(p=>Area(p.Mesh,bones[p.Bone])),want=Section(sources,bones,r,cut,g.Key.Half,g.Key.Body,0,g.SelectMany(p=>p.Mesh.Positions.Select(q=>Vector3.Transform(q,bones[p.Bone]))).ToList());
                    double e=Math.Abs(area-want)/Math.Max(want,1e-6);if(want>.002)worstArea=Math.Max(worstArea,e);
                    Check(e<.035||Math.Abs(area-want)<4e-5,label+": joint "+g.Key.Body+" half "+g.Key.Half+" cap "+(area*1e4).ToString("0.0")+" cm2 = its section "+(want*1e4).ToString("0.0")+" cm2");
                }
            }
            Check(slowest<400,"a split takes "+slowest.ToString("0")+" ms at most here (built once per death, never per frame)");
            report.Add(bodies+" splits (Standard and Sheldon Clothes at 3 tiers, Prototype; sagittal, coronal, oblique; hats; a headshot): deterministic, "+sweeps+" sealed halves ("+rays+" rays), volume within "+(worstVolume*100).ToString("0.00")+"%, silhouette within "+(worstSilhouette*100).ToString("0.000")+"% ("+(worstExtra*100).ToString("0.0000")+"% sticking out), caps within "+(worstArea*100).ToString("0.0")+"% of the section; "+capTris+" cap triangles; slowest "+slowest.ToString("0")+" ms");

            // ---- 4. After the split nothing allocates: the hold, sparks, gushes and the blade's blood are pure functions ----
            {
                var (feet,clock)=kills[slotSaw.id];var bones=Pose(feet,90);var r=NativeSawBisect.Request(slotSaw,0,clock,Vector3.Transform(Vector3.Zero,bones[1]),3);
                var sparks=new BisectSpark[NativeBisect.MaxSparks];var rig=new NativeFactoryRig(new NativeFactoryKit.Looks());var blood=new double[hazards.Length];for(int i=0;i<blood.Length;i++)blood[i]=double.NaN;blood[Array.IndexOf(hazards,slotSaw)]=clock;
                float Frames(){float sum=0;for(int f=0;f<600;f++){float age=f/120f;sum+=NativeBisect.Shudder(r,age);sum+=NativeBisect.Sparks(r,age,1,sparks);for(int k=0;k<NativeBisect.Gushes;k++)if(NativeBisect.Gush(r,k,0,1.75f,out float at,out var o,out var n,out var a,out bool b))sum+=at;rig.Build(hazards,0,clock+age,blood);sum+=rig.Parts.Count;}return sum;}
                Frames();long before=GC.GetAllocatedBytesForCurrentThread();float total=Frames();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(bytes==0&&total>0,"600 frames of the hold, sparks, gushes and the bloodied blade allocate nothing ("+bytes+" bytes)");
                int shown=0;for(int f=0;f<60;f++)shown+=NativeBisect.Sparks(r,f/120f,1,sparks);
                Check(shown>0&&NativeBisect.Sparks(r,r.Hitch+.7f,1,sparks)==0&&NativeBisect.Sparks(r,.05f,.3f,sparks)<=NativeBisect.Sparks(r,.05f,1,sparks),"sparks over the hold and just after, thinned by reduced effects");
                Check(NativeBisect.Shudder(r,r.Hitch*.5f)>0&&NativeBisect.Shudder(r,r.Hitch+.01f)==0&&NativeBisect.Shudder(r,-1)==0,"the halves shudder on the blade only while they hold");
                float last=-1;int gushes=0;for(int k=0;k<NativeBisect.Gushes;k++){if(!NativeBisect.Gush(r,k,0,1.75f,out float at,out var o,out var n,out var a,out bool b))continue;Check(at>=0&&at<.5f&&(b||at>=last),"gush "+k+" at "+at.ToString("0.00")+" s");if(!b)last=at;gushes++;Check(Math.Abs(n.Length()-1)<1e-3f,"gush "+k+" has a direction");}
                Check(gushes==NativeBisect.Gushes,"every gush sprays (along the cut, and off the blade's rim)");
                // The blade carries its blood only from the kill on.
                rig.Build(hazards,0,clock-.5,blood);int beforeKill=rig.Parts.Count;rig.Build(hazards,0,clock-.5,null);Check(beforeKill==rig.Parts.Count,"no blood on the blade before its kill");
                rig.Build(hazards,0,clock+.5,blood);int bloodied=rig.Parts.Count;rig.Build(hazards,0,clock+.5,null);Check(bloodied==rig.Parts.Count+7&&bloodied<NativeFactoryRig.Capacity,"seven smears on the bloodied blade (within the rig's capacity)");
                report.Add("no allocation after the split (600 frames: "+bytes+" bytes); "+gushes+" gushes, sparks over "+r.Hitch.ToString("0.00")+" s and after; blade blood from the kill on");
            }

            // ---- 5. Wiring (Runtime text): presentation only, replicated data only, the fallback ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r","");
            string death=R("NativeDeathPresentation.cs"),bisect=R("NativeBisectPresentation.cs"),corpse=R("NativeBisectCorpse.cs"),models=R("NativeBisectModels.cs"),factory=R("NativeFactoryPresentation.cs");
            var bonesList=Regex.Match(death,@"static readonly int\[\] Bones=\{([0-9,]+)\}").Groups[1].Value;
            Check(bonesList==string.Join(",",NativeBisect.Bodies),"NativeBisect.Bodies are the corpse's ragdoll bodies ("+bonesList+")");
            Check(bisect.Contains("death.damageKind!=\"hazard\"||!NativeFactoryHazards.Active(arena.MapId)||!NativeBisect.Applies(Gore,death.style,death.kept,death.model)")
                &&bisect.Contains("NativeSawBisect.Find(hazards,round,arena.Match.FightAt,death.at,NativeArena.Vec(death.position))")&&bisect.Contains("NativeSawBisect.Request(saw,round,clock,CutNum(pelvis)"),"a split needs a Factory hazard death, gore on and a stock ending; the saw and plane come from the record");
            Check(death.Contains("corpse=pilot&&slice==null?SawBisect(arena,death,rig):null;")&&death.Contains("if(corpse==null)corpse=new NativeCorpse(death,rig,arena,slice);"),"no split (gore off, any other death): the corpse is made exactly as before");
            Check(death.Contains("if(corpse.Bisect!=null)recolored=BisectDraw(corpse,death,model,bodySkin,alpha,clock);")&&death.Contains("if(corpse.Twin!=null)KillSpray(corpse.Twin,shown,alpha,clock);")&&death.Contains("if(corpse.Bisect!=null)BisectBlood(corpse,death,KillClock(arena));"),"a split body draws its halves instead of the whole, with both fountains and the cut's blood");
            Check(death.Contains("DriveCollapse(clock);ReleaseHalves();")&&death.Contains("Twin?.Forces();")&&death.Contains("if(Twin!=null){Twin.StepSpray();Twin.clock=clock;}")&&death.Contains("if(Host!=null)scene=Host.scene;else")&&death.Contains("Twin?.Dispose();Twin=null;OnRelease?.Invoke();"),"one scene and one step for both halves; released together");
            Check(death.Contains("if(Halved)foreach(var part in Parts.Values){part.linearVelocity=part.angularVelocity=Vector3.zero;part.isKinematic=true;}")&&corpse.Contains("if(parted||at+1e-6f<Request.Hitch)return;parted=true;"),"the halves hold on the blade until the hitch, then part");
            Check(death.Contains("&&death.actor.SourceSpec!=\"siren\"&&HoldsWeapon(at))")&&corpse.Contains("private bool HoldsWeapon(Vector3[] at)=>!Halved||(Request.Side(N(at[WeaponHand(Death)]))>=0?1:0)==Half;"),"the weapon drops whole, with the half its hand is on");
            Check(corpse.Contains("foreach(var a in first.Parts.Values)foreach(var b in second.Parts.Values)Physics.IgnoreCollision("),"the halves never collide with each other");
            Check(models.Contains("var transient=new HashSet<Part>();")&&models.Contains("Cut=NativeBisect.Split(Sources,Draw,Plane,new[]{Look,BisectLook.Hat},Tier,Seed);")&&models.Contains("job.Task=System.Threading.Tasks.Task.Run(job.Split);"),"the halves are cut once from the body model and hat the corpse draws (on a worker while the body holds); wound meshes are copied");
            Check(bisect.Contains("var plan=NativeBisect.Plan(fx.Job.Sources,fx.Job.Draw,fx.Job.Plane);")&&corpse.Contains("AwaitSplit?.Invoke();PartHalf(clock);")&&bisect.Contains("if(!fx.Settled){if(fx.Job!=null&&fx.Job.Done)Settle(corpse);if(!fx.Settled)return false;}"),"the corpses come from the plan; the whole body draws until the split is in, which the halves wait for before they part");
            Check(factory.Contains("factoryStage.Blood=SawBlood(arena,NativeFactoryStage.Hazards(arena.MapId));")&&factory.Contains("rigBuilder.Build(hz,round,clock,Blood);")&&bisect.Contains("if(!Gore||hazards==null)return null;"),"the blade keeps its blood from replicated deaths, none with gore off");
            Check(R("NativeSandbox.cs").Contains("\"Preview Saw Split\"")&&bisect.Contains("if(!SawSplitGallery||!arena.Sandbox)"),"the Dev Sandbox previews the split (presentation only)");
            foreach(var f in new[]{"Runtime/NativeArena.cs","Runtime/NativeDeaths.cs","Runtime/NativeStageHazardsRuntime.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeFrameCodec.cs","Core/NativeFactoryHazards.cs","Core/NativeStageHazards.cs"})
                Check(!Regex.IsMatch(File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)),@"Bisect|SawSplit"),f+" (hits, damage, hazards, the wire) never reads the split");
            Check(!Regex.IsMatch(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeBisect.cs"))+File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeSawBisect.cs")),@"NativeBodySkins|NativeBodyModels|UnityEngine"),"the split is engine free and skin agnostic (the looks come in)");
            report.Add("wiring: replicated record -> saw -> plane; fallback unchanged; one scene, hold, part; weapon whole; blade blood; sandbox preview; no simulation file reads it");

            foreach(var line in report)Console.WriteLine("PASS bisect: "+line);
            Console.WriteLine("PASS bisect: "+count+" checks");
        }

        // ---- the bodies as the runtime banks them ----
        internal static Dictionary<string,List<Model>> Bank(string root)
        {
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json")));
            using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
            using var bank=JsonDocument.Parse(reader.ReadToEnd());var o=new Dictionary<string,List<Model>>();
            float[] F(JsonElement e)=>e.EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();
            foreach(var m in bank.RootElement.GetProperty("models").EnumerateArray())
            {
                var id=m.GetProperty("id").GetString();if(id!=NativeBodySkins.PrototypeModel&&!id.StartsWith("hat-",StringComparison.Ordinal))continue;var list=new List<Model>();
                foreach(var s in m.GetProperty("sections").EnumerateArray())
                {
                    var p=F(s.GetProperty("positions"));if(p.Length==0)continue;var n=F(s.GetProperty("normals"));var c=F(s.GetProperty("colors"));var e=F(s.GetProperty("emission"));var w=new WeaponSection();
                    for(int i=0;i<p.Length/3;i++){w.Positions.Add(new Vector3(p[i*3],p[i*3+1],p[i*3+2]));w.Normals.Add(new Vector3(n[i*3],n[i*3+1],n[i*3+2]));w.Colors.Add(new Vector4(c[i*4],c[i*4+1],c[i*4+2],c[i*4+3]));w.Emission.Add(new Vector4(e[i*4],e[i*4+1],e[i*4+2],e[i*4+3]));}
                    foreach(var x in s.GetProperty("indices").EnumerateArray())w.Indices.Add(x.GetInt32());
                    bool clear=s.TryGetProperty("transparent",out var t)&&t.ValueKind==JsonValueKind.True||s.TryGetProperty("doubleSide",out var d)&&d.ValueKind==JsonValueKind.True;
                    list.Add(new Model{Name=s.GetProperty("part").GetString(),Bone=s.GetProperty("bone").GetInt32(),Mesh=w,Clear=clear});
                }
                o[id]=list;
            }
            return o;
        }
        // The Runtime's CarveMesh (NativeWoundGeometry.cs) in System.Numerics: a carved Prototype part's boxes as uploaded.
        internal static WeaponSection CarveMesh(NativeWoundPart d,List<WoundSolid> solids)
        {
            var m=new Matrix4x4(d.matrix[0],d.matrix[1],d.matrix[2],d.matrix[3],d.matrix[4],d.matrix[5],d.matrix[6],d.matrix[7],d.matrix[8],d.matrix[9],d.matrix[10],d.matrix[11],d.matrix[12],d.matrix[13],d.matrix[14],d.matrix[15]);
            m=Matrix4x4.CreateScale((float)(1/d.size.x),(float)(1/d.size.y),(float)(1/d.size.z))*m;Matrix4x4.Invert(m,out var inv);var nm=Matrix4x4.Transpose(inv);var w=new WeaponSection{Bone=d.bone};
            foreach(var box in solids)
            {
                var mid=(box.min+box.max)*.5;var ext=(box.max-box.min)*.5;var tint=new Vector3((float)box.color.x,(float)box.color.y,(float)box.color.z);
                var paint=box.flesh?tint:new Vector3(d.color[0]*tint.X,d.color[1]*tint.Y,d.color[2]*tint.Z);var em=box.flesh?new Vector4(tint*.3f,0):new Vector4(d.emission[0]*tint.X,d.emission[1]*tint.Y,d.emission[2]*tint.Z,d.tag);
                for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
                {
                    var n=Vector3.Zero;if(axis==0)n.X=sign;else if(axis==1)n.Y=sign;else n.Z=sign;var u=axis==0?Vector3.UnitY:axis==1?Vector3.UnitZ:Vector3.UnitX;var v=Vector3.Cross(n,u);int start=w.Positions.Count;
                    foreach(var c in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
                    {
                        var q=n+u*c.X+v*c.Y;var p=Vector3.Transform(new Vector3((float)(mid.x+q.X*ext.x),(float)(mid.y+q.Y*ext.y),(float)(mid.z+q.Z*ext.z)),m);p.X=-p.X;var nn=Vector3.Normalize(Vector3.TransformNormal(n,nm));nn.X=-nn.X;
                        w.Positions.Add(p);w.Normals.Add(nn);w.Colors.Add(new Vector4(paint,1));w.Emission.Add(em);
                    }
                    w.Indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
                }
            }
            return w;
        }
        private static List<BisectSource> Sources(Dictionary<string,List<Model>> bank,NativeWoundPart[] woundParts,int skin,int tier,NativeWound[] wounds,string hat,out BisectLook[] looks)
        {
            var o=new List<BisectSource>();var groups=wounds==null||wounds.Length==0?new WoundCarveGroup[0]:NativeWoundAnchors.Carve(wounds.SelectMany(w=>NativeWoundAnchors.Of(woundParts,w,false,WoundSkinCases.Body)));
            if(skin==NativeBodySkins.PrototypeIndex)
            {
                var replaced=new HashSet<string>(groups.Select(g=>g.Part.name));
                foreach(var m in bank[NativeBodySkins.PrototypeModel])if(!replaced.Contains(m.Name))o.Add(new BisectSource{Bone=m.Bone,Mesh=m.Mesh,Cap=!m.Clear});
                foreach(var g in groups)o.Add(new BisectSource{Bone=g.Part.bone,Mesh=CarveMesh(g.Part,g.Solids)});
            }
            else
            {
                var built=NativeBodyModels.Build(skin,tier);var cuts=groups.Length==0?new List<WoundSkinMesh>():NativeWoundSkin.Cut(built,groups,NativeBodyModels.WoundLook(skin)).ToList();var byPart=cuts.Where(c=>c.Part>=0).ToDictionary(c=>c.Part);
                for(int i=0;i<built.Parts.Length;i++){var p=built.Parts[i];if(p.Mesh.Positions.Count==0)continue;if(byPart.TryGetValue(i,out var c)){if(c.Mesh!=null&&c.Mesh.Positions.Count>0)o.Add(new BisectSource{Bone=p.Bone,Mesh=c.Mesh,Fill=p.Mesh});}else o.Add(new BisectSource{Bone=p.Bone,Mesh=p.Mesh});}
                foreach(var c in cuts.Where(c=>c.Part<0))o.Add(new BisectSource{Bone=c.Bone,Mesh=c.Mesh});
            }
            if(hat==NativeSheldonHead.Hat)
            {
                // The Sheldon Head as the bank uploads it: x mirrored, winding turned.
                foreach(var s in NativeSheldonHead.Build())
                {
                    var w=new WeaponSection();for(int i=0;i<s.Positions.Count;i++){var v=s.Positions[i];var n=s.Normals[i];w.Positions.Add(new Vector3(-v.X,v.Y,v.Z));w.Normals.Add(new Vector3(-n.X,n.Y,n.Z));w.Colors.Add(s.Colors[i]);w.Emission.Add(s.Emission[i]);}
                    for(int i=0;i+2<s.Indices.Count;i+=3){w.Indices.Add(s.Indices[i]);w.Indices.Add(s.Indices[i+2]);w.Indices.Add(s.Indices[i+1]);}
                    o.Add(new BisectSource{Bone=s.Bone,Mesh=w,Group=1,Cap=!s.Transparent&&!s.DoubleSide});
                }
            }
            else if(hat!=null)foreach(var m in bank["hat-"+hat])o.Add(new BisectSource{Bone=m.Bone,Mesh=m.Mesh,Group=1,Cap=!m.Clear});
            looks=new[]{NativeBodyModels.CutLook(skin),BisectLook.Hat};return o;
        }
        private static Vector3 Mi(Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);
        private static Quaternion Mi(Quaternion q)=>new Quaternion(q.X,-q.Y,-q.Z,q.W);
        /// <summary>The rest pose (arms eased out) standing at `feet`, turned by `yaw` (Unity degrees): each joint's draw matrix.</summary>
        private static Matrix4x4[] Pose(Vector3 feet,float yaw)
        {
            var p=new CapturedRig.Pose();p.Rotation[CapturedRig.LArm]=CapturedRig.Rot(CapturedRig.Z,.16f);p.Rotation[CapturedRig.RArm]=CapturedRig.Rot(CapturedRig.Z,-.16f);
            p.Rotation[CapturedRig.LElbow]=CapturedRig.Rot(CapturedRig.X,-.35f);p.Rotation[CapturedRig.RElbow]=CapturedRig.Rot(CapturedRig.X,-.2f);p.Rotation[CapturedRig.LKnee]=CapturedRig.Rot(CapturedRig.X,.2f);p.World();
            var place=Matrix4x4.CreateTranslation(0,NativeMeleeSlice.RootHeight,0)*Matrix4x4.CreateRotationY(yaw*(float)Math.PI/180)*Matrix4x4.CreateTranslation(feet);
            var bones=new Matrix4x4[22];for(int b=0;b<22;b++)bones[b]=Matrix4x4.CreateFromQuaternion(Mi(p.WorldRotation[b]))*Matrix4x4.CreateTranslation(Mi(p.WorldPosition[b]))*place;return bones;
        }

        // ---- geometry ----
        private static readonly Vector3[] FleshColours={NativePortraitRules.Linear(0xa8141f),NativePortraitRules.Linear(0x780a14),NativePortraitRules.Linear(0x4f0610),NativePortraitRules.Linear(0xbd1a24),NativePortraitRules.Linear(0x2c0307)};
        private static bool Flesh(Vector3 c)=>FleshColours.Any(f=>Vector3.Distance(f,c)<1e-4f);
        private static string Digest(BisectCut cut,List<BisectSource> sources)
        {
            var sb=new StringBuilder();
            foreach(var p in cut.Pieces){sb.Append(p.Half).Append(',').Append(p.Source).Append(',').Append(p.Bone).Append(',').Append(p.Whole).Append(';');if(p.Mesh==null)continue;foreach(var q in p.Mesh.Positions)sb.Append(q.X.ToString("R")).Append(q.Y.ToString("R")).Append(q.Z.ToString("R"));foreach(var c in p.Mesh.Colors)sb.Append(c.X.ToString("R"));foreach(int i in p.Mesh.Indices)sb.Append(i).Append(' ');}
            using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())).Take(12).Select(b=>b.ToString("x2")));
        }
        /// <summary>Box [lo, hi] inside box [outLo, outHi] (1 mm slack), and no more than 6 cm bigger on any side.</summary>
        private static bool Within(Vector3 lo,Vector3 hi,Vector3 outLo,Vector3 outHi){var a=lo-outLo;var b=outHi-hi;return Math.Min(Math.Min(a.X,a.Y),a.Z)>-.001f&&Math.Min(Math.Min(b.X,b.Y),b.Z)>-.001f&&Math.Max(Math.Max(a.X,a.Y),a.Z)<.06f&&Math.Max(Math.Max(b.X,b.Y),b.Z)<.06f;}
        private static bool Outward(WeaponSection m)
        {
            for(int k=0;k+2<m.Indices.Count;k+=3){var a=m.Positions[m.Indices[k]];var b=m.Positions[m.Indices[k+1]];var c=m.Positions[m.Indices[k+2]];var x=Vector3.Cross(b-a,c-a);if(x.LengthSquared()<1e-16f)continue;if(Vector3.Dot(x,m.Normals[m.Indices[k]])<=0)return false;}
            return true;
        }
        /// <summary>World triangles: the whole body (`cut` null) or one half's pieces.</summary>
        private static List<(Vector3 a,Vector3 b,Vector3 c)> World(List<BisectSource> sources,Matrix4x4[] bones,BisectCut cut,int half)
        {
            var o=new List<(Vector3,Vector3,Vector3)>();
            void Add(WeaponSection m,int bone){var M=bones[bone];for(int k=0;k+2<m.Indices.Count;k+=3)o.Add((Vector3.Transform(m.Positions[m.Indices[k]],M),Vector3.Transform(m.Positions[m.Indices[k+1]],M),Vector3.Transform(m.Positions[m.Indices[k+2]],M)));}
            if(cut==null){foreach(var s in sources)Add(s.Mesh,s.Bone);return o;}
            foreach(var p in cut.Pieces)if(p.Half==half)Add(p.Whole?sources[p.Source].Mesh:p.Mesh,p.Bone);return o;
        }
        private static double Volume(List<(Vector3 a,Vector3 b,Vector3 c)> t,Vector3 origin){double v=0;foreach(var (a,b,c) in t)v+=Vector3.Dot(a-origin,Vector3.Cross(b-origin,c-origin))/6.0;return v;}
        private static double Area(WeaponSection m,Matrix4x4 M){double s=0;for(int k=0;k+2<m.Indices.Count;k+=3){var a=Vector3.Transform(m.Positions[m.Indices[k]],M);var b=Vector3.Transform(m.Positions[m.Indices[k+1]],M);var c=Vector3.Transform(m.Positions[m.Indices[k+2]],M);s+=Vector3.Cross(b-a,c-a).Length()*.5;}return s;}
        /// <summary>Rays from nine directions across the cut (a 1.5 cm grid): how many meet a back face first (see-through).</summary>
        private static int Sweep(List<(Vector3 a,Vector3 b,Vector3 c)> t,Vector3 n,Vector3 c,out int rays)
        {
            const float Step=.0153f;const int Half=60,G=2*Half+1;NativeBisect.Basis(n,out var u,out var v);int back=0;rays=0;
            foreach(var d in new[]{n,-n,Vector3.Normalize(n+u*.6f),Vector3.Normalize(-n+v*.5f),u,v,-u,-v,Vector3.Normalize(n*.3f+u+v)})
            {
                NativeBisect.Basis(d,out var e1,out var e2);var bins=new List<int>[G*G];
                for(int k=0;k<t.Count;k++)
                {
                    var (a,b,cc)=t[k];float x0=float.MaxValue,x1=float.MinValue,y0=float.MaxValue,y1=float.MinValue;
                    foreach(var q in new[]{a,b,cc}){float x=Vector3.Dot(q-c,e1)/Step,y=Vector3.Dot(q-c,e2)/Step;x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
                    for(int i=Math.Max(-Half,(int)Math.Floor(x0));i<=Math.Min(Half,(int)Math.Ceiling(x1));i++)for(int j=Math.Max(-Half,(int)Math.Floor(y0));j<=Math.Min(Half,(int)Math.Ceiling(y1));j++)(bins[(i+Half)*G+j+Half]??=new List<int>()).Add(k);
                }
                for(int i=-Half;i<=Half;i++)for(int j=-Half;j<=Half;j++)
                {
                    var list=bins[(i+Half)*G+j+Half];if(list==null)continue;var o=c+e1*(i*Step+.00037f)+e2*(j*Step+.00051f)-d*3;rays++;float best=float.MaxValue;bool front=true;
                    foreach(int k in list){var (a,b,cc)=t[k];if(Hit(o,d,a,b,cc,out float tt,out bool f)&&tt<best){best=tt;front=f;}}
                    if(best<float.MaxValue&&!front)back++;
                }
            }
            return back;
        }
        private static bool Hit(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c,out float t,out bool front)
        {
            t=0;front=true;var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-14f)return false;float inv=1/det;var s=o-a;float uu=Vector3.Dot(s,p)*inv;if(uu<0||uu>1)return false;
            var q=Vector3.Cross(s,e1);float vv=Vector3.Dot(d,q)*inv;if(vv<0||uu+vv>1)return false;t=Vector3.Dot(e2,q)*inv;if(t<=0)return false;front=Vector3.Dot(Vector3.Cross(e1,e2),d)<0;return true;
        }
        /// <summary>The shares of the body's pixels (2 mm, orthographic along `d`) the halves cover beyond it, and leave bare.</summary>
        private static (double extra,double missing) Silhouette(List<(Vector3 a,Vector3 b,Vector3 c)> x,List<(Vector3 a,Vector3 b,Vector3 c)> y,Vector3 d,Vector3 c)
        {
            const float Px=.002f;const int H=520;NativeBisect.Basis(d,out var e1,out var e2);
            bool[] Cover(List<(Vector3 a,Vector3 b,Vector3 c)> t)
            {
                var m=new bool[(2*H)*(2*H)];
                foreach(var (a,b,cc) in t)
                {
                    var p=new[]{a,b,cc}.Select(q=>new Vector2(Vector3.Dot(q-c,e1)/Px+H,Vector3.Dot(q-c,e2)/Px+H)).ToArray();float area=(p[1].X-p[0].X)*(p[2].Y-p[0].Y)-(p[2].X-p[0].X)*(p[1].Y-p[0].Y);if(Math.Abs(area)<1e-9f)continue;
                    int i0=Math.Max(0,(int)Math.Floor(p.Min(q=>q.X))),i1=Math.Min(2*H-1,(int)Math.Ceiling(p.Max(q=>q.X))),j0=Math.Max(0,(int)Math.Floor(p.Min(q=>q.Y))),j1=Math.Min(2*H-1,(int)Math.Ceiling(p.Max(q=>q.Y)));
                    for(int i=i0;i<=i1;i++)for(int j=j0;j<=j1;j++)
                    {
                        // Pixel centres off the 2 mm lattice of the authored boxes (a sole flush with the floor would sit on a row of them).
                        float px=i+.5137f,py=j+.4719f;float w0=(p[1].X-p[0].X)*(py-p[0].Y)-(p[1].Y-p[0].Y)*(px-p[0].X),w1=(p[2].X-p[1].X)*(py-p[1].Y)-(p[2].Y-p[1].Y)*(px-p[1].X),w2=(p[0].X-p[2].X)*(py-p[2].Y)-(p[0].Y-p[2].Y)*(px-p[2].X);
                        if(area>0?w0>=0&&w1>=0&&w2>=0:w0<=0&&w1<=0&&w2<=0)m[i*2*H+j]=true;
                    }
                }
                return m;
            }
            var mx=Cover(x);var my=Cover(y);int extra=0,missing=0,covered=0;for(int i=0;i<mx.Length;i++){if(mx[i])covered++;if(my[i]&&!mx[i])extra++;if(mx[i]&&!my[i])missing++;}return covered==0?(1,1):((double)extra/covered,(double)missing/covered);
        }
        /// <summary>
        /// A body's section on its half's cut plane, measured independently of the cap: the plane sampled on a 2 mm grid, a point
        /// inside when it lies inside any of the body's closed meshes (or a wound cut's stand-in) by its own section's parity.
        /// </summary>
        private static double Section(List<BisectSource> sources,Matrix4x4[] bones,BisectRequest r,BisectCut cut,int half,int body,int group,List<Vector3> capPoints)
        {
            var n=r.Normal;float d=Vector3.Dot(n,r.Point)+(half==1?1:-1)*cut.Recess[body];NativeBisect.Basis(n,out var u,out var v);
            var loops=new List<List<(Vector2 a,Vector2 b)>>();
            foreach(var s in sources)
            {
                if(!s.Cap||s.Group!=group||NativeBisect.Anchor(s.Bone)!=body)continue;
                foreach(var mesh in new[]{s.Mesh,s.Fill})
                {
                    if(mesh==null)continue;
                    foreach(var comp in ClosedComponents(mesh))
                    {
                        var segs=new List<(Vector2,Vector2)>();var M=bones[s.Bone];
                        foreach(int k in comp)
                        {
                            var a=Vector3.Transform(mesh.Positions[mesh.Indices[k]],M);var b=Vector3.Transform(mesh.Positions[mesh.Indices[k+1]],M);var c=Vector3.Transform(mesh.Positions[mesh.Indices[k+2]],M);
                            float da=Vector3.Dot(n,a)-d,db=Vector3.Dot(n,b)-d,dc=Vector3.Dot(n,c)-d;var pts=new List<Vector3>();
                            void E(Vector3 p,Vector3 q,float dp,float dq){if((dp>0)!=(dq>0))pts.Add(p+(q-p)*(dp/(dp-dq)));}
                            E(a,b,da,db);E(b,c,db,dc);E(c,a,dc,da);if(pts.Count==2)segs.Add((new Vector2(Vector3.Dot(pts[0],u),Vector3.Dot(pts[0],v)),new Vector2(Vector3.Dot(pts[1],u),Vector3.Dot(pts[1],v))));
                        }
                        if(segs.Count>=3)loops.Add(segs);
                    }
                }
            }
            if(loops.Count==0)return 0;Vector2 lo=new Vector2(float.MaxValue),hi=new Vector2(float.MinValue);
            foreach(var q in capPoints){var p=new Vector2(Vector3.Dot(q,u),Vector3.Dot(q,v));lo=Vector2.Min(lo,p);hi=Vector2.Max(hi,p);}
            foreach(var l in loops)foreach(var (a,b) in l){lo=Vector2.Min(lo,Vector2.Min(a,b));hi=Vector2.Max(hi,Vector2.Max(a,b));}
            const float G=.002f;int inside=0;
            for(float y=lo.Y+G*.5f+.00013f;y<hi.Y;y+=G)
            {
                var spans=new List<(float from,float to)>();
                foreach(var l in loops)
                {
                    var xs=new List<float>();foreach(var (a,b) in l)if((a.Y>y)!=(b.Y>y))xs.Add(a.X+(y-a.Y)*(b.X-a.X)/(b.Y-a.Y));xs.Sort();for(int i=0;i+1<xs.Count;i+=2)spans.Add((xs[i],xs[i+1]));
                }
                for(float x=lo.X+G*.5f+.00017f;x<hi.X;x+=G)foreach(var (f,t) in spans)if(x>f&&x<t){inside++;break;}
            }
            return inside*G*G;
        }
        /// <summary>A mesh's closed components (welded by position; every edge on an even count of triangles: boxes may touch), as
        /// triangle starts. Each one's section is read by parity, so touching boxes (a carved part) merge across their shared faces.</summary>
        private static List<List<int>> ClosedComponents(WeaponSection m)
        {
            var key=new Dictionary<(long,long,long),int>();var w=new int[m.Positions.Count];
            for(int i=0;i<w.Length;i++){var p=m.Positions[i];var k=((long)Math.Round(p.X*1e5),(long)Math.Round(p.Y*1e5),(long)Math.Round(p.Z*1e5));if(!key.TryGetValue(k,out int id)){id=key.Count;key[k]=id;}w[i]=id;}
            int n=m.Indices.Count/3;var root=Enumerable.Range(0,n).ToArray();int F(int x){while(root[x]!=x)x=root[x]=root[root[x]];return x;}
            var edges=new Dictionary<(int,int),List<int>>();
            for(int t=0;t<n;t++)for(int e=0;e<3;e++){int a=w[m.Indices[t*3+e]],b=w[m.Indices[t*3+(e+1)%3]];if(a==b)continue;var k=a<b?(a,b):(b,a);if(!edges.TryGetValue(k,out var l))edges[k]=l=new List<int>();l.Add(t);}
            foreach(var l in edges.Values)for(int i=1;i<l.Count;i++)root[F(l[i])]=F(l[0]);
            var open=new HashSet<int>();foreach(var l in edges.Values)if((l.Count&1)!=0)foreach(int t in l)open.Add(F(t));
            var o=new Dictionary<int,List<int>>();for(int t=0;t<n;t++){int r=F(t);if(open.Contains(r))continue;if(!o.TryGetValue(r,out var l))o[r]=l=new List<int>();l.Add(t*3);}
            return o.Values.ToList();
        }
    }
}
