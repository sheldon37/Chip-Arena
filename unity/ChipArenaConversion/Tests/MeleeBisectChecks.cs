using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // Melee kills cut the body's own mesh along the swing (Core NativeMeleeBisect over NativeMeleeSlice's plan and NativeBisect's
    // split; Runtime NativeBisectPresentation.MeleeBisect). Lethal blows of every blade at random places: each request lies on
    // the body and on the swing's plane (a part cut kept across its limb), the Standard body splits sealed, whole and capped
    // along it for horizontal, diagonal and vertical cuts, a part cut takes only its part, the seam's blood runs along the cut,
    // the host, a guest a frame or two late and the replay agree, the ledger reads the authority's direction, and nothing in the
    // simulation reads any of it.
    internal static class MeleeBisectChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Melee bisect: "+what);count++;}
        private const float D=(float)Math.PI/180;
        private static float Tilt(Vector3 n)=>(float)(Math.Acos(Math.Min(1,Math.Abs(n.Y)))/D);
        private static string Shape(float tilt)=>tilt<30?"horizontal":tilt<75?"diagonal":"vertical";
        private sealed class Blow{public string Spec;public MeleeBlow B;public NativeWound Wound;public V3 Victim;public float Face;public int Seed;public SlicePlan Plan;public BisectRequest Request;public Matrix4x4[] Bones;public Vector3[] Joints;}

        // A lethal blow as the authority lands it: the attacker's feet at `from`, its committed yaw, the victim's body centre
        // `victim` facing `face`, and the Hit event's direction from body centre to body centre (NativeArena.Damage's normal).
        private static MeleeBlow Strike(string spec,int index,float yaw,V3 from,V3 victim,bool air,string kit)
        {
            var c=new Vector3(from.x,from.y+NativeMeleeSlice.RootHeight,from.z);var to=new Vector3(victim.x,victim.y,victim.z)-c;to=Vector3.Normalize(to);
            return new MeleeBlow{At=1,Spec=spec,CommitYaw=yaw,Index=index,SwingAt=.95f,SwingUntil=1.1f,From=from,Air=air,Kit=kit,Detail=kit!=null?"ability":"direct",Toward=new V3(to.X,to.Y,to.Z),Aimed=true};
        }
        private static NativeWound Wound(string spec)
        {
            float damage=spec=="greatsword"?52:spec=="daggers"?15:spec=="shield"?23:30;var local=new V3(0,.15f,.4f);
            var w=NativeBodyDamage.Resolve(NativeBodyDamage.Melee(spec,1),NativeBodyDamage.Part(local,0),damage,true);w.point=local;w.direction=new V3(0,0,-1);return w;
        }
        private static Vector3[] Joints(Matrix4x4[] bones){var j=new Vector3[22];for(int b=0;b<22;b++)j[b]=Vector3.Transform(Vector3.Zero,bones[b]);return j;}

        public static void Run(string root)
        {
            count=0;var report=new List<string>();var options=new JsonSerializerOptions{IncludeFields=true};
            var bank=BisectChecks.Bank(root);var woundParts=JsonSerializer.Deserialize<NativeWoundParts>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/WoundParts.json")),options).parts;

            // ---- 1. Every blade's lethal blows: the request is on the body, on the swing's plane, and the right kind ----
            var picks=new Dictionary<string,Blow>();var tally=new List<string>();
            foreach(var spec in new[]{"sword","greatsword","daggers","shield"})
            {
                var rnd=new Random(1014+spec.Length*31);var kinds=new Dictionary<string,int>();int n=500,none=0;
                for(int k=0;k<n;k++)
                {
                    float yaw=(float)rnd.NextDouble()*360-180,dist=(spec=="greatsword"?1.3f:1f)+(float)rnd.NextDouble()*(spec=="daggers"?.6f:1f),face=yaw+180+(float)(rnd.NextDouble()*60-30);int index=1+rnd.Next(8);
                    bool air=rnd.NextDouble()<.12;string kit=rnd.NextDouble()<.1?(spec=="greatsword"?"whirlwind":spec=="sword"?"flash-step":spec=="daggers"?"kunai-recast":"bulwark-charge"):null;
                    var q=MeleeMath.AxisAngle(MeleeMath.Y,-yaw*D);var at=Vector3.Transform(new Vector3(0,0,dist),q);var victim=new V3(-at.X,NativeMeleeSlice.RootHeight+(float)rnd.NextDouble()*.08f,at.Z);
                    var b=new Blow{Spec=spec,B=Strike(spec,index,yaw,new V3(0,0,0),victim,air,kit),Wound=Wound(spec),Victim=victim,Face=face,Seed=k};
                    b.Plan=new SlicePlan();NativeMeleeSlice.Plan(b.B,true,spec,b.Wound,victim,face,k,b.Plan);
                    b.Bones=BisectChecks.Pose(new Vector3(victim.x,victim.y-NativeMeleeSlice.RootHeight,victim.z),face);b.Joints=Joints(b.Bones);
                    b.Request=NativeMeleeBisect.Request(b.Plan,b.Joints,new Vector3(0,0,0),(uint)k*2654435761u);
                    var r=b.Request;
                    Check((r!=null)==NativeMeleeBisect.Cuts(b.Plan),spec+": a request exactly when the plan cuts the mesh");
                    if(r==null)
                    {
                        none++;string why=b.Plan.Throat?"throat":b.Plan.Crush>=0?"crush":"none";kinds[why]=kinds.TryGetValue(why,out int c0)?c0+1:1;
                        Check(spec=="shield"||spec=="daggers"&&b.Plan.Throat,spec+": only a dagger's slit throat and the shield keep the joint presentation ("+why+")");continue;
                    }
                    Check(spec!="shield","the shield never cuts the mesh");
                    int piece=NativeMeleeBisect.Piece(b.Plan);var centre=new Vector3(victim.x,victim.y,victim.z);var pelvis=b.Joints[CapturedRig.Pelvis];
                    Check(Math.Abs(r.Normal.Length()-1)<1e-4f&&r.Hitch>0&&r.Hitch<.2f&&r.Travel.Length()>5&&Math.Abs(Vector3.Dot(Vector3.Normalize(r.Travel),r.Normal))<1e-3f,spec+": a unit plane, a short hold, the blade's travel along it");
                    float flat=new Vector2(r.Point.X-centre.X,r.Point.Z-centre.Z).Length(),rise=r.Point.Y-(centre.Y-NativeMeleeSlice.RootHeight);
                    Check(flat<.55f&&rise>.25f&&rise<1.75f,spec+": the cut meets the body ("+flat.ToString("0.00")+" m off its centre line, "+rise.ToString("0.00")+" m up)");
                    string shape=Shape(Tilt(b.Plan.Normal)),key;
                    if(piece<0)
                    {
                        key=spec+" through "+shape;
                        Check(Math.Abs(Math.Abs(Vector3.Dot(r.Normal,b.Plan.Normal))-1)<1e-5f&&Vector3.Distance(r.Point,b.Plan.Point)<1e-5f,key+": the plane is the swing's own");
                        Check(r.Side(pelvis)<=1e-4f&&r.Mask==uint.MaxValue,key+": half 0 keeps the pelvis; everything is cut");
                    }
                    else
                    {
                        key=spec+" "+(piece==CapturedRig.Head?"head":"limb")+" "+shape;int from=piece==CapturedRig.Head?CapturedRig.Neck:piece,to=piece==CapturedRig.Head?CapturedRig.Head:piece+1;
                        var axis=Vector3.Normalize(b.Joints[to]-b.Joints[from]);float lean=(float)(Math.Acos(Math.Min(1,Vector3.Dot(r.Normal,axis)))/D),limit=piece==CapturedRig.Head?NativeMeleeBisect.NeckTilt:NativeMeleeBisect.LimbTilt;
                        Check(lean<=limit+.05f,key+": the cut is within "+limit+"° of square across the part ("+lean.ToString("0.0")+"°), facing it");
                        // The swing's bevel survives: the plane's normal is the swing's, turned toward the limb's axis only.
                        var plane=Vector3.Cross(b.Plan.Normal,axis);if(plane.LengthSquared()>1e-4f)Check(Math.Abs(Vector3.Dot(r.Normal,Vector3.Normalize(plane)))<1e-3f,key+": the bevel leans the swing's way");
                        var seg=b.Joints[to]-b.Joints[from];float u=Vector3.Dot(r.Point-b.Joints[from],seg)/seg.LengthSquared();Check(u>.29f&&u<.86f&&Vector3.Distance(r.Point,b.Joints[from]+seg*u)<1e-3f,key+": the cut falls inside the part ("+u.ToString("0.00")+" along it)");
                        Check(r.Mask==NativeMeleeBisect.MaskOf(piece)&&r.Mask!=uint.MaxValue,key+": only the part's bodies are cut");
                    }
                    kinds[key]=kinds.TryGetValue(key,out int c1)?c1+1:1;if(!picks.ContainsKey(key))picks[key]=b;
                    var again=NativeMeleeBisect.Request(b.Plan,b.Joints,Vector3.Zero,(uint)k*2654435761u);
                    Check(again.Point==r.Point&&again.Normal==r.Normal&&again.Travel==r.Travel&&again.Away0==r.Away0&&again.Away1==r.Away1&&again.Spin1==r.Spin1&&again.Mask==r.Mask&&again.Hitch==r.Hitch,spec+": the request is deterministic");
                }
                tally.Add(spec+" ["+string.Join(", ",kinds.OrderByDescending(p=>p.Value).Select(p=>p.Key.Replace(spec+" ","")+" "+(p.Value*100/n)+"%"))+"]");
                if(spec=="shield")Check(none==n,"shield kills keep the crush and the thrown edge's joint");
            }
            foreach(var want in new[]{"sword through horizontal","sword head horizontal","sword limb horizontal","sword through vertical","greatsword through horizontal","greatsword through vertical","daggers limb diagonal"})
                Check(picks.ContainsKey(want),"the blows include a "+want+" cut ("+string.Join(", ",picks.Keys)+")");
            Check(!picks.Keys.Any(k=>k.StartsWith("greatsword head")||k.StartsWith("greatsword limb")),"a greatsword always cleaves through");
            Check(!picks.Keys.Any(k=>k.StartsWith("daggers through")),"a dagger never goes through the body: a limb, or the throat");
            report.Add("lethal blows (500 each, random reach, facing, index, jump and kit): "+string.Join("; ",tally));

            // ---- 2. The cuts on the real body: sealed, whole, capped on the plane, only the part, blood along the seam ----
            double slowestFull=0,slowestPart=0;long bytesFull=0,bytesPart=0;int full=0,part=0;var lines=new List<string>();
            foreach(var pair in picks.OrderBy(p=>p.Key))
            {
                var b=pair.Value;var r=b.Request;int piece=NativeMeleeBisect.Piece(b.Plan);
                foreach(var (skin,tier,hat) in new[]{(NativeBodySkins.StandardIndex,0,"top"),(NativeBodySkins.SheldonIndex,1,NativeSheldonHead.Hat),(NativeBodySkins.PrototypeIndex,0,"cowboy")})
                {
                    if(skin!=NativeBodySkins.StandardIndex&&!pair.Key.EndsWith("vertical")&&!pair.Key.Contains("head"))continue;
                    string label=pair.Key+" ("+NativeBodySkins.Labels[skin]+")";
                    var sources=BisectChecks.Sources(bank,woundParts,skin,tier,null,hat,out var looks);var plane=new BisectPlane(r.Point,r.Normal);
                    GC.Collect();long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
                    var cut=NativeBisect.Split(sources,b.Bones,plane,looks,tier,0x5a17u,r.Mask);double ms=timer.Elapsed.TotalMilliseconds;long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                    if(skin==NativeBodySkins.StandardIndex){if(piece<0){full++;slowestFull=Math.Max(slowestFull,ms);bytesFull=Math.Max(bytesFull,bytes);}else{part++;slowestPart=Math.Max(slowestPart,ms);bytesPart=Math.Max(bytesPart,bytes);}}
                    Check(BisectChecks.Digest(cut,sources)==BisectChecks.Digest(NativeBisect.Split(sources,b.Bones,plane,looks,tier,0x5a17u,r.Mask),sources),label+": the split is deterministic");
                    Check(cut.Pieces.Any(p=>p.Half==0)&&cut.Pieces.Any(p=>p.Half==1)&&cut.Pieces.Any(p=>p.Half==0&&p.IsCap)&&cut.Pieces.Any(p=>p.Half==1&&p.IsCap),label+": two capped halves");
                    var plan=NativeBisect.Plan(sources,b.Bones,plane,r.Mask);
                    foreach(int body in NativeBisect.Bodies)for(int h=0;h<2;h++)Check(plan.Bodies[h][body]==cut.Bodies[h][body],label+": the plan keeps the split's bodies (half "+h+", joint "+body+")");
                    Check(plan.Rooted[0]==cut.Rooted[0]&&plan.Rooted[1]==cut.Rooted[1]&&cut.Rooted[0],label+": half 0 keeps the pelvis's own geometry, as planned");
                    if(piece>=0)
                    {
                        foreach(int body in NativeBisect.Bodies)if(cut.Bodies[1][body]&&body!=CapturedRig.Pelvis)Check(NativeBisect.Masked(r.Mask,body),label+": half 1 is the part alone (joint "+body+")");
                        Check(!cut.Rooted[1]&&cut.Bodies[1][piece],label+": the part's half has the part and only a stand-in pelvis");
                        foreach(var p in cut.Pieces.Where(p=>p.Half==1&&!NativeBisect.Masked(r.Mask,p.Bone)))Check(false,label+": a piece outside the part went with it");
                    }
                    // Every cap lies on the plane (its body's hairline recess off it at most).
                    foreach(var p in cut.Pieces.Where(p=>p.IsCap))foreach(var v in p.Mesh.Positions){float s=r.Side(Vector3.Transform(v,b.Bones[p.Bone]));if(Math.Abs(s)>NativeBisect.Recess*NativeBisect.Colours+2e-4f){Check(false,label+": cap of joint "+p.Body+" off the plane by "+s);break;}}
                    var caps=cut.Pieces.Where(p=>p.IsCap&&p.Group==0).ToList();Check(caps.Any(p=>p.Mesh.Colors.Any(c=>BisectChecks.Flesh(new Vector3(c.X,c.Y,c.Z)))),label+": the cut shows flesh");
                    if(skin==NativeBodySkins.StandardIndex)
                    {
                        // The sweep's grid sits a few millimetres along the plane off the cut's point, so no line of rays runs exactly
                        // along a seam between a cap and the wall it meets (shared vertices: watertight, but a ray on the seam ties).
                        NativeBisect.Basis(r.Normal,out var su,out var sv);var whole=BisectChecks.World(sources,b.Bones,null,-1);var at=r.Point+su*.00413f+sv*.00291f;int baseBack=BisectChecks.Sweep(whole,r.Normal,at,out _,2e-4f);
                        for(int h=0;h<2;h++){int back=BisectChecks.Sweep(BisectChecks.World(sources,b.Bones,cut,h),r.Normal,at,out int rays,2e-4f);Check(back<=baseBack+2,label+": half "+h+" is sealed ("+back+" of "+rays+" rays meet a back face first; the body "+baseBack+")");}
                        double v=BisectChecks.Volume(whole,at),v0=BisectChecks.Volume(BisectChecks.World(sources,b.Bones,cut,0),at),v1=BisectChecks.Volume(BisectChecks.World(sources,b.Bones,cut,1),at),err=Math.Abs(v0+v1-v)/v;
                        Check(v0>0&&v1>0&&err<.01,label+": halves "+(v0*1000).ToString("0.00")+" + "+(v1*1000).ToString("0.00")+" L = the body's "+(v*1000).ToString("0.00")+" L");
                        lines.Add(pair.Key+" "+Tilt(r.Normal).ToString("0")+"° ("+(v1*1000).ToString("0.0")+" L off, "+ms.ToString("0")+" ms)");
                    }
                }
                // The seam's blood: out of the cut, in order along the blade's pass, flicked on the way it went.
                float last=float.MinValue;var seam=Vector3.Normalize(r.Seam);
                for(int k=0;k<NativeBisect.CutGushes;k++)
                {
                    Check(NativeBisect.Gush(r,k,0,1.8f,out float at,out var o,out var nn,out var along,out bool blade)&&!blade,pair.Key+": gush "+k+" sprays from the seam");
                    float off=Math.Abs(r.Side(o)),run=Vector3.Dot(o-r.Point,seam);Check(Math.Abs(off-.01f)<1e-4f&&Math.Abs(run)<=r.SeamHalf+1e-4f&&Vector3.Distance(o,r.Point+seam*run+r.Normal*r.Side(o))<1e-4f,pair.Key+": gush "+k+" leaves the seam ("+run.ToString("0.00")+" m along it)");
                    Check(run>last&&at>=0&&at<=r.Hitch&&Vector3.Dot(along,Vector3.Normalize(r.Travel))>.999f,pair.Key+": gush "+k+" follows the blade's pass");last=run;
                }
                Check(!NativeBisect.Gush(r,NativeBisect.CutGushes,0,1.8f,out _,out _,out _,out _,out _)&&NativeBisect.Sparks(r,.02f,1,new BisectSpark[NativeBisect.MaxSparks])==0,pair.Key+": no saw rim flings and no sparks off a blade through flesh");
            }
            Check(slowestFull<400&&slowestPart<400,"a melee split takes "+slowestFull.ToString("0")+" ms through the body, "+slowestPart.ToString("0")+" ms for a part (on a worker during the hold)");
            report.Add(full+" through-cuts and "+part+" part cuts on the Standard body (and Sheldon Clothes / Prototype for chops and heads), sealed, whole, capped on the plane: "+string.Join("; ",lines));
            report.Add("cost on the Standard Full body: through "+slowestFull.ToString("0")+" ms / "+(bytesFull/1048576.0).ToString("0.0")+" MB at most, part "+slowestPart.ToString("0")+" ms / "+(bytesPart/1048576.0).ToString("0.0")+" MB (once per death, on a worker)");

            // ---- 3. Host, a guest and the replay agree; moving bodies carry on ----
            {
                var rnd=new Random(77);int samples=0,same=0,oldSame=0;float worstTurn=0,worstShift=0,oldTurn=0,oldShift=0,bound=0;
                foreach(var spec in new[]{"sword","greatsword","daggers"})for(int k=0;k<300;k++)
                {
                    float yaw=(float)rnd.NextDouble()*360-180,dist=(spec=="greatsword"?1.3f:1f)+(float)rnd.NextDouble()*(spec=="daggers"?.5f:.9f),face=yaw+180+(float)(rnd.NextDouble()*60-30);int index=1+rnd.Next(8);bool air=rnd.NextDouble()<.1;
                    var q=MeleeMath.AxisAngle(MeleeMath.Y,-yaw*D);var at=Vector3.Transform(new Vector3(0,0,dist),q);var a0=new V3((float)rnd.NextDouble()*4-2,0,(float)rnd.NextDouble()*4-2);
                    var victim=new V3(a0.x-at.X,NativeMeleeSlice.RootHeight,a0.z+at.Z);var host=Strike(spec,index,yaw,a0,victim,air,null);
                    // The guest's frame comes a tick or two after the hit: the attacker has carried on along its lunge (and drifted).
                    float lag=.03f+(float)rnd.NextDouble()*.12f,drift=(float)rnd.NextDouble()*.06f-.03f;var fwd=new Vector3((float)Math.Sin(yaw*D),0,(float)Math.Cos(yaw*D));var side=new Vector3(fwd.Z,0,-fwd.X);
                    var late=host;late.From=new V3(a0.x+fwd.X*lag+side.X*drift,0,a0.z+fwd.Z*lag+side.Z*drift);
                    var wound=Wound(spec);var bones=BisectChecks.Pose(new Vector3(victim.x,0,victim.z),face);var joints=Joints(bones);
                    SlicePlan P(MeleeBlow blow){var p=new SlicePlan();NativeMeleeSlice.Plan(blow,true,spec,wound,victim,face,k,p);return p;}
                    var ph=P(host);var pg=P(late);var oh=host;oh.Aimed=false;var og=late;og.Aimed=false;var oph=P(oh);var opg=P(og);
                    var rh=NativeMeleeBisect.Request(ph,joints,Vector3.Zero,7);var rg=NativeMeleeBisect.Request(pg,joints,Vector3.Zero,7);if(rh==null&&rg==null)continue;samples++;
                    bool kind=ph.Kind==pg.Kind&&ph.Count==pg.Count&&(ph.Count==0||ph.Joints[0]==pg.Joints[0]);if(kind)same++;
                    if(oph.Kind==opg.Kind&&oph.Count==opg.Count&&(oph.Count==0||oph.Joints[0]==opg.Joints[0]))oldSame++;
                    float Turn(SlicePlan x,SlicePlan y)=>(float)(Math.Atan2(Vector3.Cross(x.Normal,y.Normal).Length(),Math.Abs(Vector3.Dot(x.Normal,y.Normal)))/D);
                    float Shift(SlicePlan x,SlicePlan y)=>Math.Abs(Vector3.Dot(x.Normal,y.Point-x.Point));
                    worstTurn=Math.Max(worstTurn,Turn(ph,pg));oldTurn=Math.Max(oldTurn,Turn(oph,opg));
                    // The attacker's distance is the frame's: a plane through the blade moves by that error times its slope at most.
                    if(kind){float s=Shift(ph,pg);worstShift=Math.Max(worstShift,s);bound=Math.Max(bound,s-(lag+Math.Abs(drift))*(float)Math.Sin(Tilt(ph.Normal)*D)-.002f);}
                    if(oph.Kind==opg.Kind)oldShift=Math.Max(oldShift,Shift(oph,opg));
                    // With the authority's attacker position (the documented contract), the guest's cut is the host's exactly.
                    var struck=new V3(a0.x,NativeMeleeSlice.RootHeight,a0.z);var exact=late;exact.Exact=true;exact.Struck=struck;var hostExact=host;hostExact.Exact=true;hostExact.Struck=struck;var pe=P(exact);var peh=P(hostExact);
                    Check(pe.Kind==peh.Kind&&pe.Count==peh.Count&&(pe.Count==0||pe.Joints[0]==peh.Joints[0])&&pe.Point==peh.Point&&pe.Normal==peh.Normal,spec+": with the attacker's position at the hit the guest cuts exactly as the host");
                    if(peh.Kind==ph.Kind&&peh.Count==ph.Count&&(ph.Count==0||peh.Joints[0]==ph.Joints[0]))Check(Vector3.Distance(peh.Point,ph.Point)<1e-3f&&Turn(peh,ph)<.01f,spec+": the host's own frame already puts the attacker where the authority had it");
                    var wrong=late;wrong.Exact=true;wrong.Struck=new V3(a0.x,0,a0.z);var pw=P(wrong);Check(Vector3.Distance(pw.Point,pg.Point)<1e-5f,spec+": a position the event's direction contradicts (feet, not centre) is ignored");
                    // The replay replays the same records and the round keeps each death's first-seen blow: the same request.
                    var replay=NativeMeleeBisect.Request(P(host),joints,Vector3.Zero,7);Check(replay==null?rh==null:rh!=null&&replay.Point==rh.Point&&replay.Normal==rh.Normal&&replay.Mask==rh.Mask,spec+": the replay cuts as live");
                }
                Check(worstTurn<.01f,"the cut's plane is the same for the host and a late guest ("+worstTurn.ToString("0.000")+"°; the frame's own bearing: up to "+oldTurn.ToString("0.0")+"°)");
                Check(bound<=0&&same>=oldSame&&same>=samples*.8f,"a late guest's cut sits within the attacker's lag times the blade's slope of the host's ("+(worstShift*100).ToString("0.0")+" cm at most) and takes the same part in "+same+"/"+samples+" (the frame's bearing: "+oldSame+")");
                report.Add("host vs a guest 30-150 ms late ("+samples+" blows): planes identical (frame bearing alone: "+oldTurn.ToString("0.0")+"°), cuts within "+(worstShift*100).ToString("0.0")+" cm (frame bearing: "+(oldShift*100).ToString("0.0")+" cm), same part "+same+"/"+samples+" (frame bearing: "+oldSame+"); exact with the attacker's position at the hit; the replay identical");
                // A moving body: both halves keep its momentum (60% here, PartHalf adds 30% more), the part rides the swing on top.
                var mv=picks["sword head horizontal"];var vel=new Vector3(5.5f,0,-2);var rm=NativeMeleeBisect.Request(mv.Plan,mv.Joints,vel,9);var r0=NativeMeleeBisect.Request(mv.Plan,mv.Joints,Vector3.Zero,9);
                Check(Vector3.Distance(rm.Away0-r0.Away0,vel*.6f)<1e-4f&&Vector3.Distance(rm.Away1-r0.Away1,vel*.6f)<1e-4f&&rm.Point==r0.Point&&rm.Normal==r0.Normal,"a moving body's halves carry its momentum; the cut is where it was struck");
                var fast=NativeMeleeBisect.Request(mv.Plan,mv.Joints,new Vector3(90,0,0),9);Check(Vector3.Distance(fast.Away0,r0.Away0)<=12*.6f+1e-3f,"a launched body's momentum is capped");
                var gs=picks["greatsword through horizontal"].Request;Check(Vector3.Dot(gs.Away1-gs.Away0,Vector3.Normalize(gs.Travel))>1&&gs.Spin1.Length()>2,"a level cleave slides the top off along the swing and topples it");
                var ch=picks["sword through vertical"].Request;Check(Vector3.Dot(ch.Away1-ch.Away0,ch.Normal)>2&&Math.Abs(ch.Normal.Y)<.35f,"a top-down chop parts the halves to the sides");
            }

            // ---- 4. The ledger reads the authority's direction and the swing's own air flag ----
            {
                var ledger=new NativeHitLedger();var dir=Vector3.Normalize(new Vector3(.3f,-.05f,1));
                var attacker=new Actor{Id=1,SourceSpec="sword",CommitYaw=12,Combo=2,SwingAt=.95f,SwingUntil=1.1f,AirAttack=true,Grounded=true,Position=new V3(0,0,0),Hp=100};
                var target=new Actor{Id=2,SourceSpec="pistol",Position=new V3(.4f,0,1.4f),ReactionAt=1,Hp=100};
                var e=new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=1,Target=2,Time=1,Amount=30,Detail="direct",Position=new V3(.3f,1,1.1f),From=new V3(.3f,1,1.1f),Normal=new V3(dir.X,dir.Y,dir.Z)};
                ledger.Observe(new Snapshot{Actors=new[]{attacker,target},Events=new[]{e}});
                Check(ledger.Blow(2,1,out var blow)&&blow.Aimed&&Math.Abs(blow.Toward.x-dir.X)<1e-6f&&blow.Air&&!blow.Exact&&blow.Index==3&&blow.CommitYaw==12,"the ledger keeps the hit's direction and the swing's air flag (a landing attack hits grounded)");
                var ledger2=new NativeHitLedger();var e2=e;e2.Projectile=41;e2.From=new V3(0,NativeMeleeSlice.RootHeight,0);ledger2.Observe(new Snapshot{Actors=new[]{attacker,target},Events=new[]{e2}});
                Check(ledger2.Blow(2,1,out var b2)&&b2.Exact&&b2.Struck.y==NativeMeleeSlice.RootHeight,"a melee hit carrying its swing's id gives the attacker's position at the hit");
            }

            // ---- 4b. The killing swing by the id its hit carries (PR #205), the attacker at contact; the time rule without one ----
            {
                int samples=0,lagged=0,thrownOld=0;float worstContact=0;
                bool Same(SlicePlan x,SlicePlan y,float tol)
                {
                    if(x.Kind!=y.Kind||x.Count!=y.Count||x.Crush!=y.Crush||x.Throat!=y.Throat)return false;for(int i=0;i<x.Count;i++)if(x.Joints[i]!=y.Joints[i])return false;
                    return Vector3.Distance(x.Point,y.Point)<=tol&&Vector3.Distance(x.Normal,y.Normal)<=tol;
                }
                foreach(var spec in new[]{"sword","greatsword","daggers"})for(int index=1;index<=4;index++)
                {
                    // The authority's lethal blow: the attacker's feet a0 (body centre `centre`) at contact, the victim's body centre
                    // `victim`, the Hit's direction centre to centre and its From the attacker's centre. A guest's frame shows the
                    // attacker 12 cm further along its lunge (2 cm aside); the next tick another pilot's swing (57) lands a blow
                    // inside the old 0.02 s window.
                    float yaw=12+index*31,dist=spec=="greatsword"?1.5f:1.15f,face=yaw+180-index*7;var wound=Wound(spec);
                    var fwd=new Vector3((float)Math.Sin(yaw*D),0,(float)Math.Cos(yaw*D));var a0=new V3(.4f,0,-.3f);
                    var victim=new V3(a0.x+fwd.X*dist,NativeMeleeSlice.RootHeight,a0.z+fwd.Z*dist);var centre=new V3(a0.x,NativeMeleeSlice.RootHeight,a0.z);
                    var to=Vector3.Normalize(new Vector3(victim.x-centre.x,victim.y-centre.y,victim.z-centre.z));var toward=new V3(to.X,to.Y,to.Z);
                    var late=new V3(a0.x+fwd.X*.12f+fwd.Z*.02f,0,a0.z+fwd.Z*.12f-fwd.X*.02f);var point=new V3(victim.x-to.X*.4f,victim.y,victim.z-to.Z*.4f);
                    Actor Attacker(V3 feet,int combo,string kit=null)=>new Actor{Id=1,SourceSpec=spec,CommitYaw=yaw,Combo=combo,SwingAt=.95f,SwingUntil=1.1f,Grounded=true,Position=feet,Hp=100,MeleeKit=kit!=null?new NativeMeleeKit{Kind=kit}:null};
                    var dead=new Actor{Id=2,SourceSpec="pistol",Position=new V3(victim.x,0,victim.z),Hp=0};
                    var other=new Actor{Id=3,SourceSpec="greatsword",CommitYaw=-80,Grounded=true,Position=new V3(victim.x+1.3f,0,victim.z),Hp=100};
                    // `ids`: a current host (the swing's id in Projectile, the attacker's centre in From); else an older one (From
                    // left at the hit point, no id).
                    ArenaEvent Hit(int id,float t,int source,int swing,bool ids,string detail="direct",float amount=30)=>new ArenaEvent{Id=id,Kind=EventKind.Hit,Source=source,Target=2,Time=t,Amount=amount,Detail=detail,Position=point,
                        From=ids?(source==1?centre:new V3(other.Position.x,NativeMeleeSlice.RootHeight,other.Position.z)):point,Normal=source==1?toward:new V3(-1,0,0),Projectile=ids?swing:0};
                    var killed=new ArenaEvent{Id=11,Kind=EventKind.Death,Source=1,Target=2,Time=1,Position=victim};
                    NativeHitLedger Feed(V3 feet,bool ids,bool otherSwing=true,bool deathRead=true,bool attackerSeen=true)
                    {
                        var l=new NativeHitLedger();var actors=attackerSeen?new[]{Attacker(feet,2),dead,other}:new[]{dead,other};
                        l.Observe(new Snapshot{Actors=actors,Events=deathRead?new[]{Hit(10,1,1,41,ids),killed}:new[]{Hit(10,1,1,41,ids)}});
                        if(otherSwing)l.Observe(new Snapshot{Actors=new[]{Attacker(feet,2),dead,other},Events=new[]{Hit(12,1+1/60f,3,57,ids,amount:40)}});
                        return l;
                    }
                    SlicePlan P(in MeleeBlow b){var p=new SlicePlan();NativeMeleeSlice.Plan(b,true,spec,wound,victim,face,index,p);return p;}
                    string tag=spec+" cut "+index;samples++;

                    // The id beats the newer blow by the other swing; the time rule alone (an older host) takes that one.
                    var host=Feed(a0,true);var guest=Feed(late,true);
                    bool hostSaw=host.Blow(2,1,out var hb),guestSaw=guest.Blow(2,1,out var gb);
                    Check(hostSaw&&guestSaw&&hb.Swing==41&&gb.Source==1&&gb.Swing==41&&gb.Spec==spec&&gb.Index==3&&gb.At==1&&gb.Exact&&gb.Struck.x==centre.x&&gb.Struck.y==centre.y&&gb.Struck.z==centre.z,tag+": the killing hit's swing id finds its blow, not the newer one by another swing");
                    Check(Feed(late,false).Blow(2,1,out var ob)&&ob.Source==3&&!ob.Exact&&ob.Swing==0,tag+": without ids the time rule is unchanged (the latest blow within 0.02 s)");
                    Check(!Feed(late,true,attackerSeen:false).Blow(2,1,out _)&&Feed(late,false,attackerSeen:false).Blow(2,1,out var ob2)&&ob2.Source==3,tag+": the killing swing's blow unread (its attacker not in the frame): none, not another swing's");
                    // Host, a late guest and the killcam replay (the ledger cleared and fed the same frames) cut the same.
                    var replay=Feed(late,true);replay.Clear();replay.Observe(new Snapshot{Actors=new[]{Attacker(late,2),dead,other},Events=new[]{Hit(10,1,1,41,true),killed}});
                    replay.Observe(new Snapshot{Actors=new[]{Attacker(late,2),dead,other},Events=new[]{Hit(12,1+1/60f,3,57,true,amount:40)}});
                    Check(replay.Blow(2,1,out var rb)&&rb.Swing==41&&rb.Source==1,tag+": the replay finds the same swing");
                    var ph=P(hb);var pg=P(gb);var pr=P(rb);Check(Same(ph,pg,0)&&Same(ph,pr,0),tag+": host, a late guest and the replay cut identically with the swing's id");
                    // From at contact: the cut is the one through the attacker's feet at the hit (its own frame on time, no id).
                    var contact=hb;contact.Exact=false;contact.Swing=0;contact.Struck=default;var pc=P(contact);
                    Check(Same(pg,pc,1e-4f),tag+": the guest cuts through the attacker at contact (the hit's From, its body centre less "+NativeMeleeSlice.RootHeight+" m), not the frame's feet");
                    worstContact=Math.Max(worstContact,Vector3.Distance(pg.Point,pc.Point));
                    // Without an id the late frame's distance is kept, exactly as before (no other swing in the window).
                    Check(Feed(late,false,otherSwing:false).Blow(2,1,out var fb)&&fb.Source==1&&!fb.Exact&&fb.From.x==late.x&&fb.From.z==late.z,tag+": the fallback reads the frame's attacker");
                    var expect=new MeleeBlow{At=1,Source=1,Spec=spec,CommitYaw=yaw,Index=3,SwingAt=.95f,SwingUntil=1.1f,From=late,Detail="direct",Amount=30,Toward=toward,Aimed=true,Struck=point};
                    var pf=P(fb);Check(Same(pf,P(expect),0),tag+": the fallback plans from the frame's swing and feet as before");if(!Same(pf,pc,1e-4f))lagged++;
                    // A frame that shows the attacker far off after the blow (a dash on) does not turn a contact swing into a throw.
                    var far=new V3(a0.x-fwd.X*3.8f,0,a0.z-fwd.Z*3.8f);Check(Feed(far,true).Blow(2,1,out var db)&&Same(P(db),pc,1e-4f),tag+": the thrown test reads the attacker at contact");
                    if(Feed(far,false,otherSwing:false).Blow(2,1,out var dob)&&!Same(P(dob),pc,1e-4f))thrownOld++;
                    // An extra strike shares its swing's id: the strike in the kill's tick (index 3), not the swing's first (index 2).
                    var echo=new NativeHitLedger();echo.Observe(new Snapshot{Actors=new[]{Attacker(late,1),dead,other},Events=new[]{Hit(8,.9f,1,41,true,amount:20)}});
                    echo.Observe(new Snapshot{Actors=new[]{Attacker(late,2),dead,other},Events=new[]{Hit(10,1,1,41,true),killed,Hit(12,1+1/60f,3,57,true,amount:40)}});
                    Check(echo.Blow(2,1,out var eb)&&eb.Index==3&&eb.At==1&&eb.Swing==41,tag+": an extra strike's kill takes the strike that landed it");
                    // A kit hit (no id) that kills after a swing's blow keeps the time rule; so does a kill whose Death was not read.
                    var kit=new NativeHitLedger();kit.Observe(new Snapshot{Actors=new[]{Attacker(late,2,"flash-step"),dead,other},Events=new[]{Hit(9,1-1/60f,1,41,true,amount:20),Hit(10,1,1,0,true,"ability"),killed}});
                    Check(kit.Blow(2,1,out var kb)&&kb.Kit=="flash-step"&&kb.Swing==0&&!kb.Exact,tag+": a Q/E kit kill (id 0) keeps the time rule");
                    Check(Feed(late,true,deathRead:false).Blow(2,1,out var nb)&&nb.Source==3,tag+": no Death event read: the time rule");
                }
                Check(lagged>0&&thrownOld>0,"the swing's id corrects what the late frame got wrong ("+lagged+"/"+samples+" late-frame cuts moved, "+thrownOld+" far frames read as a throw)");
                report.Add("killing swing by its id ("+samples+" blows, sword/greatsword/daggers, cuts 1-4): the id beats a newer blow by another swing in the old window, an extra strike's kill takes its own strike, host/late guest/replay identical, the cut through the attacker at contact (within "+(worstContact*1000).ToString("0.000")+" mm; the late frame moved "+lagged+", a far frame threw "+thrownOld+"); without an id (kit, older host, no Death read) the time rule is unchanged");
            }

            // ---- 5. After the split nothing allocates: the hold and the seam's gushes are pure functions ----
            {
                var r=picks["sword through horizontal"].Request;
                float Frames(){float sum=0;for(int f=0;f<600;f++){float age=f/120f;sum+=NativeBisect.Shudder(r,age);for(int k=0;k<NativeBisect.Gushes;k++)if(NativeBisect.Gush(r,k,0,1.75f,out float at,out var o,out var n,out var a,out bool bl))sum+=at+o.X;sum+=r.HalfOf(CapturedRig.RHand,new Vector3(age,1,0));}return sum;}
                Frames();long before=GC.GetAllocatedBytesForCurrentThread();float total=Frames();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(bytes==0&&!float.IsNaN(total),"600 frames of a melee cut's hold, gushes and half lookups allocate nothing ("+bytes+" bytes)");
                var head=picks["sword head horizontal"].Request;var j=picks["sword head horizontal"].Joints;
                Check(head.HalfOf(CapturedRig.Head,j[CapturedRig.Head])==1&&head.HalfOf(CapturedRig.RHand,j[CapturedRig.Head]+head.Normal)==0,"a part cut's head goes with the part; a hand raised past the plane stays with the body (and so does its weapon)");
            }

            // ---- 6. Wiring (Runtime text) and the simulation untouched ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r","");
            string death=R("NativeDeathPresentation.cs"),bisect=R("NativeBisectPresentation.cs"),corpse=R("NativeBisectCorpse.cs"),world=R("NativeCorpseWorld.cs"),arena=R("ArenaPresentation.cs");
            Check(death.Contains("corpse=!pilot?null:slice!=null?MeleeBisect(arena,death,rig,slice):SawBisect(arena,death,rig);")&&death.Contains("BleedDeath(death,corpse.Bisect!=null?null:slice,KillClock(arena));"),"a pilot's melee kill is cut along its swing; no cut keeps the joint slice and its bleed");
            Check(bisect.Contains("if(sourceModels==null||death==null||!NativeMeleeBisect.Cuts(slice)||!NativeBisect.Applies(Gore,death.style,death.kept,death.model))return null;")&&bisect.Contains("return NativeMeleeBisect.Request(slice,joints,velocity,seed);"),"gore off, a kill effect's own ending, a Siren or a revivable body keep the death as it was");
            Check(bisect.Contains("corpse.SplitReady=()=>fx.Settled||fx.Job==null||fx.Job.Done;")&&corpse.Contains("if(SplitReady!=null&&!SplitReady()&&clock<Request.Hitch+HoldLimit)return;")&&!bisect.Contains("background&=arena.Time-death.at"),"the halves hold until the worker's split is in instead of blocking the frame, late bodies too");
            Check(death.Contains("if(Halved){StandIn();LoosePieces();}")&&corpse.Contains("root.GetComponent<Collider>().enabled=false;root.isKinematic=true;")&&corpse.Contains("standIn&&p==CapturedRig.Pelvis")&&world.Contains("foreach(var pair in Parts)if(!Still(pair.Key))pair.Value.isKinematic=false;"),"a half without pelvis geometry keeps a still, collisionless stand-in nothing hangs from");
            Check(corpse.Contains("Request.HalfOf(WeaponHand(Death),N(at[WeaponHand(Death)]))==Half")&&bisect.Contains("r.HalfOf(bone,CutNum(p))==1?corpse.Twin:corpse"),"the weapon, halo and team bands go with the half their joint is on (a part cut keeps them on the body)");
            Check(world.Contains("if(!near&&Twin!=null)foreach(var pair in Twin.Parts)"),"a thrown part keeps its floor");
            Check(death.Contains("if(killBlows.TryGetValue(key,out var seen)){has=seen.has;blow=seen.blow;}")&&death.Contains("ClearBlood(true);killBlows.Clear();return;}")&&arena.Contains("if(!keepWorldMarks)killBlows.Clear();"),"each death's first-seen blow is kept across the killcam's reset (the rebuilt body and the replay cut the same) and cleared with the round");
            Check(R("NativeHordeKindPresentation.cs").Contains("Air=actor.AirAttack,")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeHitReact.cs")).Contains("Air=a.AirAttack,"),"a landing attack's blow is read from the swing's replicated air flag");
            foreach(var f in new[]{"Runtime/NativeMelee.cs","Runtime/NativeMeleeAbilities.cs","Runtime/NativeMeleeSpecials.cs","Runtime/NativeArena.cs","Runtime/NativeDeaths.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeFrameCodec.cs","Core/NativeMeleeRules.cs","Core/NativeAirAttackSimulation.cs","Core/NativeSprintAttack.cs","Core/NativeBodyDamage.cs"})
            {
                var p=Path.Combine(root,"Assets/ChipArena",f);if(!File.Exists(p))continue;Check(!Regex.IsMatch(File.ReadAllText(p),@"NativeMeleeBisect|killBlows|SplitReady|\.Mask\b|MeleeBisect"),f+" (hits, damage, interruption, kills, the wire) never reads the cut");
            }
            Check(!Regex.IsMatch(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeMeleeBisect.cs")),@"UnityEngine|NativeBodySkins|NativeArena|NativePilot|Random\("),"the cut is engine free, skin agnostic and reads no authority state");
            report.Add("wiring: melee kill -> plan -> cut, joint slice kept for gore off, kill effects, slit throats and shields; hold until split; stand-in pelvis; weapon/halo/bands by half; killcam-safe blows; no simulation file reads it");

            foreach(var line in report)Console.WriteLine("PASS melee bisect: "+line);
            Console.WriteLine("PASS melee bisect: "+count+" checks");
        }
    }
}
