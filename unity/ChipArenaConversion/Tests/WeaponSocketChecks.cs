#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The weapon sockets effects hang on (owner 2026-10-15: blood "rendering off of the weapons like they're floating"; "the
    // bullet doesn't come from the tip of barrel"; "weapon trails are too far / too early for melee weapons"):
    // - NativeBladeProfile follows the blades as drawn (the Standard models as built, the Prototype Series bank boxes), and every
    //   piece of NativeBladeBlood lies on that surface for both skins, both daggers (the Standard left one mirrored) and every
    //   amount; the base tree's slivers hung 4 cm under the blade.
    // - NativeShotLaunch draws a gun round from the posed muzzle (the gun route's rig at several aims) and hands it to the
    //   simulated path within Converge m: the drawn round never reaches back into the barrel, the round and its model leave
    //   along the path and everything downrange is the simulation's.
    // - NativeSlashTrail sweeps the crescent from the posed blade on its own clock (MeleeMotion.CutAge): its head is the blade
    //   tip, it never runs ahead of the blade through a hit-stop, and it stays on the blade's radius; the old fixed arc did not.
    internal static class WeaponSocketChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Weapon sockets: "+what);count++;}
        private const float Dt=1/60f;
        public static void Run(string root,ConversionContent data)
        {
            count=0;var log=new List<string>();
            Profiles(root,log);Blood(log);Shots(data,log);Slashes(data,log);
            Console.WriteLine("PASS weapon sockets ("+count+" checks: "+string.Join("; ",log)+")");
        }

        // ---- the blade as drawn ----
        // The section's outline at z through a triangle soup (source frame): the largest and smallest x, and the largest |y|.
        private static bool Outline(IEnumerable<(List<Vector3> p,List<int> i)> soup,float z,out float maxX,out float minX,out float maxY)
        {
            maxX=-1e9f;minX=1e9f;maxY=0;bool any=false;
            foreach(var (p,idx) in soup)for(int k=0;k+2<idx.Count;k+=3)for(int e=0;e<3;e++)
            {
                var a=p[idx[k+e]];var b=p[idx[k+(e+1)%3]];if((a.Z-z)*(b.Z-z)>0||Math.Abs(a.Z-b.Z)<1e-7f)continue;var c=a+(b-a)*((z-a.Z)/(b.Z-a.Z));
                maxX=Math.Max(maxX,c.X);minX=Math.Min(minX,c.X);maxY=Math.Max(maxY,Math.Abs(c.Y));any=true;
            }
            return any;
        }
        private static JsonElement BankModel(string text,string id)
        {
            int at=text.IndexOf("\"id\":\""+id+"\"",StringComparison.Ordinal);if(at<0)throw new Exception("Weapon sockets: no "+id+" in the bank");
            int start=text.LastIndexOf('{',at),depth=0,end=start;
            for(int i=start;i<text.Length;i++){if(text[i]=='{')depth++;else if(text[i]=='}'&&--depth==0){end=i;break;}}
            using var doc=JsonDocument.Parse(text.Substring(start,end-start+1));return doc.RootElement.Clone();
        }
        private static void Profiles(string root,List<string> log)
        {
            float worstStd=0,worstProto=0;string text;
            using(var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json"))))
            using(var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())))using(var zip=new GZipStream(bytes,CompressionMode.Decompress))using(var reader=new StreamReader(zip))text=reader.ReadToEnd();
            foreach(var spec in new[]{"sword","daggers","greatsword"})
            {
                Check(NativeBladeProfile.Bladed(spec)&&!NativeBladeProfile.Bladed("shield")&&!NativeBladeProfile.Bladed("pistol"),"bladed specs");
                // Standard: the built full-detail blade (the right hand's blade sections).
                var mesh=NativeWeaponModels.Build(spec,NativeWeaponModels.Full);var soup=mesh.Sections.Where(s=>s.Bone==NativeWeaponModels.RightHand&&s.Stretch==WeaponStretch.Blade).Select(s=>(s.Positions,s.Indices)).ToList();
                Check(NativeBladeProfile.Range(spec,true,out float from,out float tip),spec+" Standard blade run");
                for(float z=from+.01f;z<tip-.03f;z+=.013f)
                {
                    bool hasS=NativeBladeProfile.At(spec,true,z,out var s);Check(Outline(soup,z,out float maxX,out float minX,out _)&&hasS,spec+" Standard section at "+z);
                    // The dagger's spine (past the jimping notches at z .2-.25) is its back.
                    float off=Math.Max(Math.Abs(maxX-s.Edge),spec=="daggers"&&z>.26f?Math.Abs(minX-s.Back):0);worstStd=Math.Max(worstStd,off);
                    Check(off<.0015f,spec+" Standard profile edge "+s.Edge+" / back "+s.Back+" against the built blade "+maxX+" / "+minX+" at z "+z);
                }
                // Prototype Series: the bank's 'weapon-' boxes (Unity frame: x mirrored), away from the tip-box steps.
                var model=BankModel(text,"weapon-"+spec);var psoup=new List<(List<Vector3>,List<int>)>();
                foreach(var sec in model.GetProperty("sections").EnumerateArray())
                {
                    if(sec.GetProperty("bone").GetInt32()!=NativeWeaponModels.RightHand)continue;var f=sec.GetProperty("positions").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();
                    var pts=new List<Vector3>();for(int i=0;i+2<f.Length;i+=3)pts.Add(new Vector3(-f[i],f[i+1],f[i+2]));psoup.Add((pts,sec.GetProperty("indices").EnumerateArray().Select(v=>v.GetInt32()).ToList()));
                }
                Check(NativeBladeProfile.Range(spec,false,out float pfrom,out float ptip),spec+" Prototype blade run");
                for(float z=pfrom+.01f;z<ptip-.02f;z+=.011f)
                {
                    bool nearStep=false;for(int k=0;NativeBladeProfile.Step(spec,false,k,0,2,out float step);k++)if(Math.Abs(z-step)<.012f)nearStep=true;if(nearStep)continue;
                    bool hasS=NativeBladeProfile.At(spec,false,z,out var s);Check(Outline(psoup,z,out float maxX,out _,out _)&&hasS,spec+" Prototype section at "+z);
                    worstProto=Math.Max(worstProto,Math.Abs(maxX-s.Edge));Check(Math.Abs(maxX-s.Edge)<.0015f,spec+" Prototype profile edge "+s.Edge+" against the bank "+maxX+" at z "+z);
                }
            }
            log.Add($"blade profiles on the drawn edges within {worstStd*1000:F1} mm (Standard, as built) and {worstProto*1000:F1} mm (Prototype bank)");
        }

        // ---- blood on the blade ----
        // The blade's half-thickness at x in a section (the edge band sloping from the edge to the flat; the flat beyond it).
        private static float Half(in BladeSection s,float x)=>x>=s.Inner?s.EdgeHalf+(s.InnerHalf-s.EdgeHalf)*MeleeMath.Clamp((s.Edge-x)/Math.Max(1e-5f,s.Edge-s.Inner),0,1):s.InnerHalf;
        // The distance from (x, y) to the section's edge band surfaces (both faces) and edge face.
        private static float Surface(in BladeSection s,float x,float y)
        {
            float best=float.MaxValue;
            for(int face=-1;face<=1;face+=2)best=Math.Min(best,Segment(new Vector2(x,y),new Vector2(s.Edge,face*s.EdgeHalf),new Vector2(s.Inner,face*s.InnerHalf)));
            return Math.Min(best,Segment(new Vector2(x,y),new Vector2(s.Edge,-s.EdgeHalf),new Vector2(s.Edge,s.EdgeHalf)));
        }
        private static float Segment(Vector2 p,Vector2 a,Vector2 b){var d=b-a;float t=d.LengthSquared()>1e-12f?MeleeMath.Clamp(Vector2.Dot(p-a,d)/d.LengthSquared(),0,1):0;return Vector2.Distance(p,a+d*t);}
        private static void Blood(List<string> log)
        {
            var pieces=new WeaponPiece[NativeBladeBlood.MaxPieces];var again=new WeaponPiece[NativeBladeBlood.MaxPieces];float worstOut=0,worstGap=0;int most=0;
            foreach(var spec in new[]{"sword","daggers","greatsword"})foreach(bool standard in new[]{true,false})foreach(bool mirror in standard&&spec=="daggers"?new[]{false,true}:new[]{false})
            foreach(float amount in new[]{.05f,.4f,1f})
            {
                int n=NativeBladeBlood.Pieces(spec,standard,amount,mirror,pieces);most=Math.Max(most,n);string what=spec+(standard?" Standard":" Prototype")+(mirror?" left":"")+" at "+amount;
                Check(n>=10&&n<=pieces.Length,what+": "+n+" pieces");
                Check(NativeBladeBlood.Pieces(spec,standard,amount,mirror,again)==n&&Enumerable.Range(0,n).All(i=>pieces[i].Position==again[i].Position&&pieces[i].Scale==again[i].Scale&&pieces[i].Rotation==again[i].Rotation),what+" is deterministic");
                NativeBladeProfile.Range(spec,standard,out float from,out float tip);var design=NativeWeaponModels.Get(spec);
                for(int i=0;i<n;i++)
                {
                    var p=pieces[i];Check(p.Orbit==-1&&!p.Glow&&(p.Shape==WeaponShape.Cube||p.Shape==WeaponShape.Sphere),what+" piece "+i+" is a lit cube or drop");
                    // Mirrored for the Standard left dagger: read it back in the right blade's frame.
                    var c=p.Position;if(mirror)c.X=-c.X;
                    Check(c.Z>=from-.002f&&c.Z<=tip,what+" piece "+i+" on the blade run ("+c.Z+")");
                    if(standard)Check(c.Z>=design.StretchBase,what+" piece "+i+" rides the stretching blade, not the hilt");
                    NativeBladeProfile.At(spec,standard,c.Z,out var s);
                    // Touching: its centre within lift + half its thickness of the band, the edge face or (a drop) the edge.
                    float gap=Surface(s,c.X,c.Y)-(p.Shape==WeaponShape.Sphere?p.Scale.X:Math.Min(p.Scale.X,p.Scale.Y)*.5f);worstGap=Math.Max(worstGap,gap);
                    Check(gap<.0025f,what+" piece "+i+" lies on the blade (centre "+c+" is "+gap*1000+" mm off its surface)");
                    // Inside the blade's envelope (6 mm: the coat, the bead over the edge, a drop), every corner.
                    var q=p.Rotation;if(mirror)q=CapturedRig.Mirror(q);
                    bool drop=p.Shape==WeaponShape.Sphere;
                    for(int k=0;k<(drop?6:8);k++)
                    {
                        // A box's eight corners (BoxGeometry(1,1,1): +-.5 of its scale); a drop's six poles (SphereGeometry(1): +-1).
                        var local=drop?new Vector3(k/2==0?(k%2==0?-1:1):0,k/2==1?(k%2==0?-1:1):0,k/2==2?(k%2==0?-1:1):0)*p.Scale:new Vector3((k&1)==0?-.5f:.5f,(k&2)==0?-.5f:.5f,(k&4)==0?-.5f:.5f)*p.Scale;var w=c+Vector3.Transform(local,q);
                        NativeBladeProfile.At(spec,standard,w.Z-.004f,out var s0);NativeBladeProfile.At(spec,standard,w.Z+.004f,out var s1);
                        float edge=Math.Max(s0.Edge,s1.Edge),half=Math.Max(Half(s0,w.X),Half(s1,w.X)),outX=w.X-edge,outY=Math.Abs(w.Y)-half;worstOut=Math.Max(worstOut,Math.Max(outX,outY));
                        Check(outX<.006f&&outY<.006f&&w.X>Math.Min(s0.Back,s1.Back)-.006f,what+" piece "+i+" corner "+w+" stays on the blade (edge "+edge+", half "+half+")");
                    }
                }
                // More blood runs further back from the point.
                if(amount==1f){int fewer=NativeBladeBlood.Pieces(spec,standard,.05f,mirror,again);float back(WeaponPiece[] a,int m){float z=9;for(int i=0;i<m;i++)z=Math.Min(z,a[i].Position.Z);return z;}Check(back(pieces,n)<back(again,fewer)-.05f,what+" runs further down the blade than a trace");}
            }
            Check(NativeBladeBlood.Pieces("sword",true,0,false,pieces)==0&&NativeBladeBlood.Pieces("shield",true,1,false,pieces)==0&&NativeBladeBlood.Pieces("rifle",false,1,false,pieces)==0,"no blood without any, nor on a shield or a gun");
            // The base tree's sliver (Core/NativeBlood.Sliver at 7009887: x edge - depth/2, y -.065) fails the surface check.
            NativeBladeProfile.At("sword",true,.9f,out var sw);NativeBladeProfile.At("sword",false,.9f,out var sp);
            float oldStd=Surface(sw,.05f-.01f,-.065f)-.018f,oldProto=Surface(sp,.05f-.01f,-.065f)-.018f;
            Check(oldStd>.02f&&oldProto>.015f,"the base tree's slivers would fail (they floated "+oldStd*100+" / "+oldProto*100+" cm off the sword)");
            log.Add($"blade blood on both skins, both daggers and every amount: up to {most} pieces, centres within {worstGap*1000:F1} mm of the drawn surface and every corner within {Math.Max(0,worstOut)*1000:F1} mm (the base slivers floated {oldStd*100:F0} cm off the Standard sword, {oldProto*100:F0} cm off the Prototype)");
        }

        // ---- gun rounds ----
        private static NativeShotView Round(string spec,Vector3 at,Vector3 vel)=>new NativeShotView{Kind=NativeShotKind.Bullet,Id=4,X=at.X,Y=at.Y,Z=at.Z,VX=vel.X,VY=vel.Y,VZ=vel.Z,Radius=.045,Spec=spec,Class="ranged"};
        private static Vector3 P(in NativeShotView v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
        private static void Shots(ConversionContent data,List<string> log)
        {
            // The math: held at the crown inside the barrel, leaving it along the path, joining it by Converge, dropped on a turn.
            {
                var m=new Vector3(.1f,1.3f,1.2f);var o=new Vector3(.2f,1.165f,.48f);var dir=Vector3.Normalize(new Vector3(0,.2f,1));float speed=52;
                Check(NativeShotLaunch.Begin(m.X,m.Y,m.Z,o.X,o.Y,o.Z,dir.X*speed,dir.Y*speed,dir.Z*speed,out var st),"a launch begins");
                float lead=Vector3.Dot(m-o,dir);var prev=m;float prevOff=float.MaxValue;
                for(float s=0;s<8;s+=.05f)
                {
                    var v=Round("pistol",o+dir*s,dir*speed);var keep=st;NativeShotLaunch.Apply(ref keep,ref v);var at=P(v);
                    Check(v.Launched&&Math.Abs(v.Flown-Math.Max(0,s-lead))<1e-4,"the round knows how far past the crown it is at "+s);
                    if(s<=lead)Check(Vector3.Distance(at,m)<1e-4f,"a round still in the barrel is held at the crown ("+s+")");
                    var line=at-(o+dir*s);float off=line.Length();
                    if(s>lead){Check(off<=prevOff+1e-5f,"the offset only shrinks along the path");prevOff=off;}
                    if(s-lead>=NativeShotLaunch.Converge)Check(off<1e-5f&&Vector3.Distance(new Vector3((float)v.VX,(float)v.VY,(float)v.VZ),dir*speed)<1e-3f,"past Converge the round is the simulation's");
                    if(s>lead&&s-lead<.06f)Check(Vector3.Distance(at,m)<.07f&&Vector3.Dot(Vector3.Normalize(new Vector3((float)v.VX,(float)v.VY,(float)v.VZ)),dir)>.999f,"it leaves the crown along the path");
                    // The model is never longer than twice its flown distance: centred on the round, it never reaches back past the crown.
                    var scene=new NativeShotScene();scene.Begin(1,1,Dt,1);scene.Shot(v);var draw=scene.Draws[0].M;float length=new Vector3(draw.M02,draw.M12,draw.M22).Length();
                    Check(length<=Math.Max(.09f,2*(float)v.Flown)+1e-4f&&length<=.65f+1e-4f,"the round's model grows out of the crown ("+length+" at "+v.Flown+")");
                    prev=at;
                }
                var turned=Round("pistol",o+dir*1.5f,Vector3.Normalize(new Vector3(1,0,.3f))*speed);var k2=st;NativeShotLaunch.Apply(ref k2,ref turned);
                Check(!k2.Live&&!turned.Launched&&P(turned)==o+dir*1.5f,"a ricochet or a reflect drops the launch");
                var plain=Round("pistol",o+dir*2,dir*speed);var scene2=new NativeShotScene();scene2.Begin(1,1,Dt,1);scene2.Shot(plain);var d2=scene2.Draws[0].M;
                Check(Math.Abs(new Vector3(d2.M02,d2.M12,d2.M22).Length()-.65f)<1e-4f,"a round without a launch keeps the source model");
                Check(!NativeShotLaunch.Gun(new NativeShotView{Kind=NativeShotKind.Bullet,Class="ranged",Spec="bow"})&&!NativeShotLaunch.Gun(new NativeShotView{Kind=NativeShotKind.Bullet,Class="magic",Spec="fire"})&&
                    NativeShotLaunch.Gun(new NativeShotView{Kind=NativeShotKind.Bullet,Class="ranged",Spec="machinegun"}),"only the guns' rounds leave a barrel");
            }
            // The tracker: a round first seen with its firer's muzzle recorded is launched; Recall keeps it; others are untouched.
            {
                var tracker=new NativeShotTracker();tracker.Begin(tracker,1,false,1);tracker.Muzzles[7]=new Vector3(.1f,1.3f,1.2f);
                var v=Round("rifle",new Vector3(.2f,1.165f,3),new Vector3(0,0,150));tracker.Remember(v);Check(tracker.Launch(v,7,.2f,1.165f,.48f),"the tracker launches a round from its firer's muzzle");
                tracker.Launched(ref v);Check(v.Launched&&v.Flown>0,"the tracker moves the round onto its drawn path");
                var w=Round("rifle",new Vector3(.2f,1.165f,3),new Vector3(0,0,150));w.Id=5;tracker.Remember(w);Check(!tracker.Launch(w,9,.2f,1.165f,.48f),"no muzzle recorded, no launch");tracker.Launched(ref w);Check(!w.Launched&&w.Z==3,"an unlaunched round stays where the simulation has it");
                var r=Round("rifle",new Vector3(.2f,1.165f,4),new Vector3(0,0,150));Check(tracker.Recall(ref r)&&!r.Launched,"Recall keeps the first identity");tracker.Launched(ref r);Check(r.Launched,"the launch is kept for the round's id");
            }
            // The defect and the fix on the gun route's posed rigs: the simulation's start against the drawn crown, at several aims.
            float worstBefore=0,worstAfter=0,worstStart=0;string worstAt="";
            foreach(var spec in new[]{"pistol","rifle","machinegun"})
            {
                var weapon=spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Pistol;var gun=GunRules.Get(weapon);
                Check(Math.Abs(NativeWeaponDressing.MuzzleZ(spec)-NativeWeaponModels.Get(spec).Muzzle.Z)<.015f,spec+" muzzle socket on the Standard crown");
                foreach(float pitch in new[]{-35f,-15f,0,15,35})
                {
                    var a=new Actor{Id=1,SourceSpec=spec,Weapon=weapon,Grounded=true,Hp=100,CastRate=1,Pitch=pitch,Ammo=gun.Magazine};var motion=new GunMotion();var pose=new CapturedRig.Pose();var jt=new NativeJuggleTrack();float t=0;
                    for(int f=0;f<100;f++){t=f*Dt;if(f%20==0)a.LastShot=t;motion.Rig.GroupPosition=new Vector3(0,.865f,0);motion.Rig.GroupYaw=0;motion.Pose(a,t,pose,false,jt,out _);motion.Finish(pose,t);}
                    var hand=pose.WorldPosition[CapturedRig.RHand];var rot=pose.WorldRotation[CapturedRig.RHand];
                    var crown=CapturedRig.Mirror(hand+Vector3.Transform(new Vector3(0,0,NativeWeaponDressing.MuzzleZ(spec)),rot))+new Vector3(0,.865f,0);
                    // NativeArena.Fire: the body's eye (.3 over the body centre), .2 m right and .48 m ahead along the yaw.
                    var start=new Vector3(.2f,1.165f,.48f);float p=pitch*(float)Math.PI/180;var aim=new Vector3(0,.865f+NativeViewRules.EyeHeight,0)+new Vector3(0,(float)Math.Sin(p),(float)Math.Cos(p))*25;var dir=Vector3.Normalize(aim-start);
                    worstStart=Math.Max(worstStart,Vector3.Distance(crown,start));
                    Check(NativeShotLaunch.Begin(crown.X,crown.Y,crown.Z,start.X,start.Y,start.Z,dir.X*gun.Speed,dir.Y*gun.Speed,dir.Z*gun.Speed,out var st),spec+" launches at "+pitch);
                    // The host's first drawn frame: one tick out. Off the crown-to-aim line before, on it after.
                    var at=start+dir*gun.Speed*Dt;var v=Round(spec,at,dir*gun.Speed);var line=Vector3.Normalize(aim-crown);
                    float Off(Vector3 q){var d=q-crown;return (d-line*Vector3.Dot(d,line)).Length();}
                    float before=Off(at);NativeShotLaunch.Apply(ref st,ref v);float after=Off(P(v));
                    if(before>worstBefore){worstBefore=before;worstAt=spec+" at "+pitch;}worstAfter=Math.Max(worstAfter,after);
                    Check(after<.06f,spec+" at "+pitch+": the first drawn round is on the line out of the crown ("+after*100+" cm; it was "+before*100+" cm)");
                    // (A round not yet past the crown on its first tick, the machine gun aiming down, is held at the crown.)
                    Check(v.Launched&&v.Flown>=0&&v.Flown<=gun.Speed*Dt&&(v.Flown>0||Vector3.Distance(P(v),crown)<1e-4f),spec+" at "+pitch+": its tracer reaches back no further than the crown");
                }
            }
            Check(worstBefore>.2f,"the base tree drew the first round well off the barrel ("+worstBefore+")");
            log.Add($"gun rounds: the simulation's start is up to {worstStart:F2} m from the drawn crown; the first drawn round sat up to {worstBefore*100:F0} cm off the crown's line ({worstAt}), now within {worstAfter*100:F0} cm, joining the simulated path within {NativeShotLaunch.Converge} m");
        }

        // ---- slash crescents ----
        private sealed class Pilot
        {
            public readonly MeleeMotion Motion=new MeleeMotion{Impacts=new MeleeImpacts()};public readonly MeleeView View;private float last=-100;
            public Pilot(string spec){View=new MeleeView{Id="socket-"+spec,Spec=spec};}
            public void Pose(float t){float dt=last<-90?Dt:t-last;last=t;var r=Motion.Rig;r.GroupPosition=Vector3.Zero;r.GroupYaw=0;Motion.Sample(View,t,dt);}
            // The posed grip and blade tip in the native frame (the source frame's x mirrored).
            public void Blade(int hand,out Vector3 grip,out Vector3 tip)
            {
                var r=Motion.Rig;var set=MeleeSet.For(View.Spec);grip=CapturedRig.Mirror(r.ChainPoint(hand,-1));tip=CapturedRig.Mirror(r.ChainPoint(hand,-1)+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(hand,-1)));
            }
        }
        private static void Slashes(ConversionContent data,List<string> log)
        {
            var inner=new Vector3[NativeSlashTrail.Capacity];var outer=new Vector3[NativeSlashTrail.Capacity];var share=new float[NativeSlashTrail.Capacity];
            float oldWorst=0,oldAhead=0,newWorst=0,maxLag=0;int strikes=0;
            foreach(var spec in new[]{"sword","daggers","greatsword","shield"})foreach(bool hit in new[]{false,true})
            {
                var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());NativeSlashFx.Look(spec,out var look);int cuts=spec=="daggers"?4:spec=="sword"?3:2;
                for(int index=1;index<=cuts;index++)
                {
                    strikes++;var p=new Pilot(spec);var v=p.View;float at=1;var sw=NativeMeleeRules.Begin(w,stats,at,index,0);var right=new NativeSlashTrail();var left=new NativeSlashTrail();
                    var slash=new NativeMeleeSwing{Spec=spec,Index=index,Yaw=0,HitAt=sw.HitAt,HitUntil=sw.HitUntil,At=at,Arc=sw.Arc};float lag=0;bool sampledEarly=false;
                    for(int f=0;f<=(int)((sw.Until-at+.3f)/Dt*2);f++)
                    {
                        float t=.8f+f*Dt*.5f;bool on=t>=at;
                        v.LastAttack=on?at:-100;v.Windup=sw.HitAt-at;v.AttackRate=1;v.AttackIndex=index;v.SwingAt=on&&t<=sw.HitUntil?sw.HitAt:float.NaN;v.SwingUntil=on&&t<=sw.HitUntil?sw.HitUntil:float.NaN;
                        v.Commit=on;v.CommitAt=at;v.CommitUntil=sw.Until;v.CommitYaw=0;v.StepAt=sw.StepAt;v.StepUntil=sw.StepUntil;v.StepDistance=0;
                        if(hit&&t>=sw.HitAt+.012f)p.Motion.Impacts.RecordHit(v.Id,sw.HitAt+.012f);
                        p.Pose(t);var m=p.Motion;if(!on){Check(float.IsNaN(m.CutAge),"no cut clock before the swing");continue;}
                        // Once the swing is over no cut is posed (its clock reads NaN): the crescent keeps the last lag.
                        if(float.IsNaN(m.CutAge))Check(t>=sw.Until-1e-3f,spec+" cut "+index+": a cut is posed through the swing ("+(t-at)+")");
                        else
                        {
                            Check(m.CutAt==at&&m.CutAge<=t-at+1e-4f,spec+" cut "+index+": the blade's clock runs at or behind the swing's ("+m.CutAge+" at "+(t-at)+")");
                            lag=t-at-m.CutAge;maxLag=Math.Max(maxLag,lag);if(!hit)Check(Math.Abs(lag)<1e-4f,"no hit, no lag");
                        }
                        if(NativeSlashTrail.Sweeping(m.CutAge,m.CutWindup,m.CutWindow))
                        {
                            if(m.CutAge<m.CutWindup)sampledEarly=true;
                            for(int h=0;h<(spec=="daggers"?2:1);h++){p.Blade(h==0?CapturedRig.RHand:CapturedRig.LHand,out var g,out var tp);(h==0?right:left).Add(g,tp,t);}
                        }
                        p.Blade(CapturedRig.RHand,out _,out var tipR);p.Blade(CapturedRig.LHand,out _,out var tipL);
                        // The swept crescent: the newest band point is the blade tip whenever the blade is in front in its window.
                        if(NativeSlashFx.Frame(slash,look,t-lag,false,out _,out _,out float alpha,out _)&&alpha>.05f&&t-lag<=sw.HitUntil)
                            foreach(var (trail,tipNow) in spec=="daggers"?new[]{(right,tipR),(left,tipL)}:new[]{(right,tipR)})
                            {
                                int n=trail.Band(Vector3.Zero,0,NativeSlashTrail.Inner(look),1,inner,outer,share);if(n<2)continue;
                                Check(share[0]==0&&Math.Abs(share[n-1]-1)<1e-4f&&Enumerable.Range(1,n-1).All(i=>share[i]>=share[i-1]),spec+" cut "+index+": the crescent runs tail to head along the tip's path");
                                if(Math.Abs(NativeSlashTrail.Bearing(tipNow,Vector3.Zero,0))<NativeSlashTrail.Front&&NativeSlashTrail.Sweeping(m.CutAge,m.CutWindup,m.CutWindow))
                                {float d=Vector3.Distance(outer[n-1],tipNow);newWorst=Math.Max(newWorst,d);Check(d<.001f,spec+" cut "+index+(hit?" with a hit":"")+": the crescent's head is the blade tip ("+d+" m)");}
                                // Never beyond the blade: every outer point was a blade tip.
                                float reach=0;for(int i=0;i<trail.Count;i++)reach=Math.Max(reach,new Vector2(trail[i].Tip.X,trail[i].Tip.Z).Length());
                                for(int i=0;i<n;i++)Check(new Vector2(outer[i].X,outer[i].Z).Length()<=reach+1e-4f,spec+" cut "+index+": the crescent stays on the blade's radius");
                            }
                        // The base tree's fixed arc, for the record: its head against the blade, and how far ahead of the blade's clock it ran.
                        if(NativeSlashFx.Frame(slash,look,t,false,out float tail,out float head,out float oa,out float grow)&&oa>.05f&&t<=sw.HitUntil)
                        {
                            float R=NativeSlashFx.Radius(look,sw.Range)*grow,tilt=NativeSlashFx.Tilt(look,slash);var fp=Vector3.UnitZ*(float)Math.Cos(look.Pitch)+Vector3.UnitY*(float)Math.Sin(look.Pitch);
                            var up=Vector3.UnitY*(float)Math.Cos(look.Pitch)-Vector3.UnitZ*(float)Math.Sin(look.Pitch);var rt=Vector3.UnitX*(float)Math.Cos(tilt)+up*(float)Math.Sin(tilt);
                            var hp=new Vector3(0,look.Height,look.Forward)+(fp*(float)Math.Cos(head)+rt*(float)Math.Sin(head))*R;
                            var feetTip=spec=="daggers"&&Vector3.Distance(hp,tipL)<Vector3.Distance(hp,tipR)?tipL:tipR;feetTip.Y+=.865f;oldWorst=Math.Max(oldWorst,Vector3.Distance(hp,feetTip));
                            if(hit)oldAhead=Math.Max(oldAhead,lag);
                        }
                    }
                    Check(!sampledEarly,spec+" cut "+index+": nothing is sampled before the blade's fast arc");
                    Check(right.Count>2||spec=="daggers"&&left.Count>2,spec+" cut "+index+": the blade was swept ("+right.Count+"/"+left.Count+" samples)");
                }
            }
            // The trail's buffer: a time running back restarts it; a full one keeps its newest samples.
            {
                var trail=new NativeSlashTrail();for(int i=0;i<200;i++)trail.Add(Vector3.Zero,new Vector3((float)Math.Sin(i*.05f),1,(float)Math.Cos(i*.05f)),i*.004f);
                Check(trail.Count<=NativeSlashTrail.Capacity&&Math.Abs(trail.Last-199*.004f)<1e-5f,"a full trail keeps its head ("+trail.Count+")");
                trail.Add(Vector3.Zero,Vector3.UnitZ,.1f);Check(trail.Count==1,"a rewind restarts the trail");
                Check(NativeSlashTrail.Inner(new SlashLook{Thickness=.32f})>.5f&&NativeSlashTrail.Inner(new SlashLook{Thickness=.5f})<NativeSlashTrail.Inner(new SlashLook{Thickness=.22f}),"thicker looks reach further down the blade");
            }
            Check(maxLag>.03f,"a hit's hit-stop holds the blade behind the swing's clock ("+maxLag+")");
            log.Add($"slash crescents swept from the posed blade over {strikes} strikes (four weapons, every cut, with and without a hit): the head is the blade tip within {newWorst*1000:F1} mm, on the blade's radius, never ahead of it through a {maxLag*1000:F0} ms hit-stop (the base tree's fixed arc sat up to {oldWorst:F1} m from the blade and ran up to {oldAhead*1000:F0} ms ahead of it)");
        }
    }
}
#endif
