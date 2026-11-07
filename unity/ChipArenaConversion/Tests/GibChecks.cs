using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The smash gib (Core NativeSmashGib: Cyber City's train, the Factory's press rams and crane load) and arrow gibbing (Core
    // NativeArrowGib: lodge halfway or pass through, blood from the arrow points), owner 2026-10-14. Classification from the real
    // map hazards and death records, determinism, the squash and sprawl, the thrown smash, the stale-wound guard, the owner's
    // kill-effect rule, the lodge depth, the pass-through decision (replicated flag and the older-frame fallback), which hits
    // stick an arrow, the split body's half, the bleed cap, no allocation per frame, and the runtime wiring (text only: no Unity).
    internal static class GibChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Gib: "+what);count++;}

        public static void Run(string root)
        {
            count=0;var report=new List<string>();
            var opt=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            var set=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),opt);
            var factory=set.maps.Single(m=>m.id=="factory").hazards;var city=set.maps.Single(m=>m.id=="cybercity").hazards;
            const double fightAt=12.5;const float yaw=30;
            int Index(string kind)=>Array.FindIndex(factory,h=>h.kind==kind);

            // ---- 1. a death where a hazard's live volume first reaches a body standing at `feet` ----
            double FirstTouch(MapHazard h,int round,Vector3 feet,double from=0)
            {
                var body=new V3(feet.X,feet.Y+.865f,feet.Z);
                for(int t=0;t<60*40;t++){double c=h.start+h.offset+from+t/60.0;var s=NativeFactoryHazards.State(h,round,c);if(NativeSawBisect.Box(h,s,out var bc,out var bs)&&NativeStageHazards.Touch(body,bc,bs))return c;}
                return double.NaN;
            }
            double At(double clock)=>clock+fightAt+NativeFactoryHazards.ArmDelay;
            SmashPlan Factory(Vector3 body,double at,string style="impact-ragdoll",string id=null,bool gore=true,bool kept=false,string model="pilot",int seed=4321)
                =>NativeSmashGib.Plan(gore,style,kept,model,"hazard","factory",factory,id,0,fightAt,at,body,Vector3.Zero,yaw,seed);

            // The press: a pilot standing on its bed, killed as the ram slams through it.
            var press=factory[Index("press")];var bed=press.b.y-press.size.y/2;var pressFeet=new Vector3(press.b.x+.3f,bed,press.b.z+.1f);
            double pressClock=FirstTouch(press,0,pressFeet);Check(!double.IsNaN(pressClock),"the press ram reaches a pilot on its bed");
            var pressBody=pressFeet+Vector3.UnitY*.865f;var pp=Factory(pressBody,At(pressClock));
            Check(pp!=null&&pp.Cause==SmashCause.Press&&pp.Pinned&&Math.Abs(pp.Floor-bed)<1e-4f&&pp.Contact>0&&pp.Contact<.3f&&pp.Speed>3,"a press death is a pinned smash on the die bed (down "+pp?.Contact.ToString("0.000")+" s after, ram at "+pp?.Speed.ToString("0.0")+" m/s)");
            Check(NativeSmashGib.Saw(factory,null,0,fightAt,At(pressClock),new V3(pressBody.X,pressBody.Y,pressBody.Z))==-1,"a press death is not a saw's split");
            // Named by the record (NativeDeath.hazardId): the name wins over the reconstruction, both ways.
            var named=Factory(pressBody+new Vector3(9,0,9),At(pressClock),id:press.id);Check(named!=null&&named.Cause==SmashCause.Press&&named.Hazard==press,"the record's hazard id names the press even where no reconstruction finds it");
            Check(Factory(pressBody,At(pressClock),id:factory[Index("vent")].id)==null&&Factory(pressBody,At(pressClock),id:"factory-nothing")==null,"a record naming a vent or no known hazard is no smash");
            // The crane: under the load's middle, killed as it falls.
            var crane=factory[Index("crane")];var zone=crane.zones[0];var craneFeet=new Vector3(zone.center.x+.2f,zone.center.y,zone.center.z-.3f);
            double craneClock=FirstTouch(crane,0,craneFeet);Check(!double.IsNaN(craneClock)&&NativeFactoryHazards.State(crane,0,craneClock).Phase==NativeFactoryHazards.Falling,"the crane's falling load reaches a pilot in its zone");
            var craneBody=craneFeet+Vector3.UnitY*.865f;var cp=Factory(craneBody,At(craneClock));
            Check(cp!=null&&cp.Cause==SmashCause.Crane&&cp.Pinned&&Math.Abs(cp.Floor-zone.center.y)<1e-4f&&cp.Speed>10&&Math.Abs(cp.Half.X-crane.size.x/2)<1e-5f&&Math.Abs(cp.Half.Z-crane.size.z/2)<1e-5f,"a crane death under the load is a pinned smash on the zone's floor (load at "+cp?.Speed.ToString("0.0")+" m/s)");
            // A pilot at the zone's edge, outside the load's footprint: thrown out from under it, a part or two crushed.
            var edgeFeet=new Vector3(zone.center.x+zone.size.x/2-.02f,zone.center.y,zone.center.z);double edgeClock=FirstTouch(crane,0,edgeFeet);
            var ep=double.IsNaN(edgeClock)?null:Factory(edgeFeet+Vector3.UnitY*.865f,At(edgeClock));
            Check(ep!=null&&ep.Cause==SmashCause.Crane&&!ep.Pinned&&ep.Crushed>=1&&ep.Crushed<=2&&ep.Travel.X>.9f,"a crane death outside the load's footprint is thrown out from under its edge with "+ep?.Crushed+" part(s) crushed");
            // The saws keep the split; a vent is no gib.
            int slotIndex=Index("saw-slot");var slot=factory[slotIndex];var slotMid=(new Vector3(slot.a.x,slot.a.y,slot.a.z)+new Vector3(slot.b.x,slot.b.y,slot.b.z))*.5f;var slotFeet=new Vector3(slotMid.X+1,0,slotMid.Z);
            double slotClock=FirstTouch(slot,0,slotFeet);var slotBody=slotFeet+Vector3.UnitY*.865f;
            Check(!double.IsNaN(slotClock)&&Factory(slotBody,At(slotClock))==null&&NativeSmashGib.Saw(factory,null,0,fightAt,At(slotClock),new V3(slotBody.X,slotBody.Y,slotBody.Z))==slotIndex,"a saw death is no smash: the saw's split takes it");
            Check(NativeSmashGib.Saw(factory,slot.id,0,fightAt,0,new V3(99,0,99))==slotIndex&&NativeSmashGib.Saw(factory,press.id,0,fightAt,At(slotClock),new V3(slotBody.X,slotBody.Y,slotBody.Z))==-1,"the record's hazard id names the saw (a named press is no saw)");
            var vent=factory.First(h=>h.kind=="vent");var ventFeet=new Vector3(vent.a.x,vent.a.y,vent.a.z);double ventClock=FirstTouch(vent,0,ventFeet);var ventBody=ventFeet+Vector3.UnitY*.865f;
            Check(!double.IsNaN(ventClock)&&Factory(ventBody,At(ventClock))==null&&NativeSmashGib.Saw(factory,null,0,fightAt,At(ventClock),new V3(ventBody.X,ventBody.Y,ventBody.Z))==-1&&NativeSmashGib.CauseOf(vent)==SmashCause.None,"a vent death is no gib");
            Check(NativeSmashGib.Killer(factory,0,fightAt,At(ventClock),new V3(ventBody.X,ventBody.Y,ventBody.Z))==Array.IndexOf(factory,vent),"the general finder names the bursting vent");
            // The general finder agrees with the saws' own wherever a saw is the first live hazard touching.
            var random=new Random(5);int agree=0;var saws=factory.Where(NativeSawBisect.Saw).ToArray();
            for(int i=0;i<4000;i++)
            {
                // Bodies round each saw's blade at random moments of the fight.
                var h=saws[i%saws.Length];double clock=random.NextDouble()*60;var sh=NativeFactoryHazards.State(h,i&1,clock);
                var b=new V3(sh.Center.x+(float)(random.NextDouble()*2-1),(h.kind==NativeFactoryHazards.SawSlot?0:3.6f)+.865f+(float)(random.NextDouble()*.6-.3),sh.Center.z+(float)(random.NextDouble()*2-1));double at=At(clock);
                int k=NativeSmashGib.Killer(factory,i&1,fightAt,at,b);if(k>=0&&NativeSawBisect.Saw(factory[k])){Check(NativeSawBisect.Find(factory,i&1,fightAt,at,b)==k,"the general finder agrees with the saws' finder");agree++;}
            }
            Check(agree>0,"random saw deaths were sampled");
            report.Add($"factory: press pinned (down {pp.Contact:0.000} s), crane pinned, crane edge thrown ({ep.Crushed} crushed), saws keep the split, a vent none, the record's hazard id first; the general finder agrees with the saws' in {agree} sampled saw deaths");

            // The train: a maglev death in the cut.
            var maglev=city.Single(NativeCyberCityHazards.IsMaglev);double trainAt=fightAt+maglev.first+1;var st=NativeCyberCityHazards.Evaluate(maglev,0,fightAt,trainAt);
            Check(st.Phase==NativeHazardPhase.Active,"the train is passing");var trainBody=new Vector3((float)st.Head-st.Direction*.5f,maglev.center.y-maglev.size.y/2+.865f,.4f);
            SmashPlan Train(string style="impact-ragdoll",int seed=777,int round=0,double at=double.NaN)=>NativeSmashGib.Plan(true,style,false,"pilot","maglev","cybercity",city,maglev.id,round,fightAt,double.IsNaN(at)?trainAt:at,trainBody,new Vector3(st.Direction*30,6,0),yaw,seed);
            var tp=Train();
            Check(tp!=null&&tp.Cause==SmashCause.Train&&!tp.Pinned&&Math.Abs(tp.Travel.X-st.Direction)<1e-5f&&tp.Speed==maglev.speed&&tp.Contact==NativeSmashGib.TrainHitch,"a maglev death is a thrown smash along the train's travel with a "+tp?.Contact+" s hit-stop");
            Check(tp.Crushed>=3&&tp.Crushed<=4&&tp.Crush.Take(tp.Crushed).Distinct().Count()==tp.Crushed&&tp.Crush.Take(tp.Crushed).All(b=>new[]{5,7,11,14,18}.Contains(b))&&(!tp.Crushes(5)||tp.Crush[0]==5),"the train crushes "+tp.Crushed+" distinct parts (the head first when it is one)");
            var odd=NativeSmashGib.Plan(true,"impact-ragdoll",false,"pilot","maglev","cybercity",city,null,1,fightAt,trainAt,trainBody,Vector3.Zero,yaw,777);
            Check(odd!=null&&Math.Abs(odd.Travel.X+st.Direction)<1e-5f,"the next round's train runs the other way, and so does its smash (no hazard id: the map's maglev)");
            var trainSets=new HashSet<string>();for(int s=0;s<200;s++){var p=Train(seed:s);trainSets.Add(string.Join(",",p.Crush.Take(p.Crushed)));}
            Check(trainSets.Count>=4,"the crushed parts vary with the seed ("+trainSets.Count+" sets in 200)");
            report.Add($"train: thrown along {tp.Travel.X:+0;-0}x at {tp.Throw} m/s, {tp.Crushed} parts crushed, {trainSets.Count} crush sets over 200 seeds");

            // ---- 2. the gate: the owner's kill-effect rule, gore, the body ----
            foreach(var style in new[]{"confetti","voxel-burst","ragdoll-frost","dev-raging-demon","launch",null,"ragdoll","impact-ragdoll"})
            {
                Check(Factory(pressBody,At(pressClock),style)?.Cause==SmashCause.Press&&Train(style)?.Cause==SmashCause.Train,"a "+(style??"null")+" death still smashes (owner 2026-10-14)");
                Check(NativeSmashGib.Applies(true,style,false,"pilot"),"the saws' split takes a "+(style??"null")+" death too (owner 2026-10-14)");
            }
            Check(!NativeBisect.Applies(true,"confetti",false,"pilot")&&!NativeBisect.Applies(true,"voxel-burst",false,"pilot")&&NativeBisect.Applies(true,"impact-ragdoll",false,"pilot"),"a melee cut keeps its own stock-ending gate");
            foreach(var style in new[]{"siren-dissipate","siren-crush","vent-fall","vent-splat"})Check(Factory(pressBody,At(pressClock),style)==null&&Train(style)==null&&!NativeSmashGib.Applies(true,style,false,"pilot"),style+" keeps its own ending");
            Check(Factory(pressBody,At(pressClock),gore:false)==null&&NativeSmashGib.Plan(false,"impact-ragdoll",false,"pilot","maglev","cybercity",city,maglev.id,0,fightAt,trainAt,trainBody,Vector3.Zero,yaw,1)==null&&!NativeSmashGib.Applies(false,"confetti",false,"pilot"),"gore off: no smash and no split");
            Check(Factory(pressBody,At(pressClock),kept:true)==null&&Factory(pressBody,At(pressClock),model:"siren")==null,"a revivable PvE body and a Siren keep their deaths");
            Check(NativeSmashGib.Plan(true,"impact-ragdoll",false,"pilot","hazard","cybercity",city,null,0,fightAt,trainAt,trainBody,Vector3.Zero,yaw,1)==null&&NativeSmashGib.Plan(true,"impact-ragdoll",false,"pilot","grid","cybercity",city,"cybercity-grid-w",0,fightAt,trainAt,trainBody,Vector3.Zero,yaw,1)==null
                &&NativeSmashGib.Plan(true,"impact-ragdoll",false,"pilot",null,"factory",factory,null,0,fightAt,At(pressClock),pressBody,Vector3.Zero,yaw,1)==null,"a grid, a non-Factory hazard or an ordinary death is no smash");
            report.Add("gate: any style smashes and splits (Siren and vent endings excepted), gore off none, kept and Siren bodies none; melee keeps its stock gate");

            // ---- 3. determinism ----
            string Print(SmashPlan p)
            {
                var s=new System.Text.StringBuilder();s.Append(p.Cause).Append(p.Seed).Append(p.Pinned).Append(p.Body).Append(p.Travel).Append(p.Speed.ToString("R")).Append(p.Floor.ToString("R")).Append(p.Top.ToString("R")).Append(p.Contact.ToString("R")).Append(p.Center).Append(p.Half).Append(p.Fall).Append(p.Throw).Append(p.Carry).Append(string.Join(",",p.Crush.Take(p.Crushed)));
                for(int k=0;k<NativeSmashGib.Gushes;k++)if(NativeSmashGib.Gush(p,k,out float a,out var o,out var n,out var l,out var c,out string kind,out float sev))s.Append(a.ToString("R")).Append(o).Append(n).Append(c).Append(kind).Append(sev);
                for(int k=0;k<NativeSmashGib.Pools;k++)if(NativeSmashGib.Pool(p,k,out float a,out var o,out var l,out float len,out float w))s.Append(a.ToString("R")).Append(o).Append(len.ToString("R")).Append(w.ToString("R"));
                for(float t=0;t<1.5f;t+=.05f){s.Append(NativeSmashGib.Squash(p,t).ToString("R"));for(int k=0;k<NativeSmashGib.Mists;k++)if(NativeSmashGib.Mist(p,k,t,out var o,out float z,out float op,out float u))s.Append(o).Append(z.ToString("R"));}
                return s.ToString();
            }
            foreach(var p in new[]{pp,cp,ep,tp})
            {
                var again=p.Cause==SmashCause.Train?Train():p==pp?Factory(pressBody,At(pressClock)):p==cp?Factory(craneBody,At(craneClock)):Factory(edgeFeet+Vector3.UnitY*.865f,At(edgeClock));
                Check(Print(p)==Print(again),p.Cause+(p.Pinned?" pinned":" thrown")+": the same record plans the same smash, moment for moment");
            }
            Check(Print(Train(seed:1))!=Print(Train(seed:2))&&Print(Factory(pressBody,At(pressClock),seed:1))!=Print(Factory(pressBody,At(pressClock),seed:2)),"another death's seed varies it");
            report.Add("determinism: every plan, gush, splat, squash and mist the same from the same record; the seed varies them");

            // ---- 4. pinned: the squash in the crusher's timing, the sprawl ----
            var joints=Rest(pressFeet,yaw);var moves=new NativeSmashGib.Move[22];
            foreach(var p in new[]{pp,cp})
            {
                float last=1;bool under=true;
                for(float t=0;t<=p.Contact+.6f;t+=1f/240)
                {
                    float s=NativeSmashGib.Squash(p,t),u=NativeSmashGib.Underside(p,t);Check(s<=last+1e-6f&&s>=NativeSmashGib.Flat-1e-6f&&s<=1,"the squash only ever flattens");last=s;
                    NativeSmashGib.Sprawl(p,NativeSmashGib.Progress(p,t),Joints(joints,p),moves);float top=float.MinValue;
                    for(int b=0;b<22;b++){var w=NativeSmashGib.Apply(moves[b],Joints(joints,p)[b]);top=Math.Max(top,p.Floor+s*(w.Y-p.Floor));}
                    if(t<p.Contact&&top>u+.06f)under=false;
                }
                Check(under,p.Cause+": the flattening body never stands above the crusher's underside as it comes down");
                Check(NativeSmashGib.Squash(p,p.Contact)==NativeSmashGib.Flat&&NativeSmashGib.Squash(p,30)==NativeSmashGib.Flat&&NativeSmashGib.Squash(p,0)==1,p.Cause+": standing at the death, flat the moment the crusher is down, flat for good");
                Check(NativeSmashGib.Overlay(p,30,out var pivot,out float spread,out float height,out float lift)&&spread>1.2f&&spread<=NativeSmashGib.SpreadMax+1e-5f&&height==NativeSmashGib.Flat&&lift>0&&lift<=NativeSmashGib.Gap&&Math.Abs(pivot.Y-p.Floor)<1e-6f,p.Cause+": flattened about the floor, spread "+NativeSmashGib.Overlay(p,30,out _,out float sp,out _,out _).ToString()+" "+sp.ToString("0.00"));
                // The sprawl at the end: rigid and joined at every joint, down on the floor, the head the fall's way.
                var j=Joints(joints,p);NativeSmashGib.Sprawl(p,1,j,moves);
                foreach(int b in NativeBisect.Bodies){int parent=b==CapturedRig.Pelvis?-1:NativeBisect.Anchor(CapturedRig.Bones[b].Parent);if(parent<0)continue;Check(Vector3.Distance(NativeSmashGib.Apply(moves[b],j[b]),NativeSmashGib.Apply(moves[parent],j[b]))<1e-3f,p.Cause+": the sprawl keeps joint "+b+" joined to its parent");}
                Check(Math.Abs(Vector3.Distance(NativeSmashGib.Apply(moves[3],j[3]),NativeSmashGib.Apply(moves[4],j[4]))-Vector3.Distance(j[3],j[4]))<1e-4f,p.Cause+": a body's joints keep their spacing (rigid)");
                float low=float.MaxValue,high=float.MinValue;for(int b=0;b<22;b++){var w=NativeSmashGib.Apply(moves[b],j[b]);low=Math.Min(low,w.Y);high=Math.Max(high,w.Y);}
                Check(low>p.Floor-.45f&&high<p.Floor+.5f,p.Cause+": the sprawled body lies on the floor ("+(low-p.Floor).ToString("0.00")+" .. "+(high-p.Floor).ToString("0.00")+" m)");
                var f=new Vector3((float)Math.Sin(yaw*Math.PI/180),0,(float)Math.Cos(yaw*Math.PI/180));var headWay=NativeSmashGib.Apply(moves[5],j[5])-NativeSmashGib.Apply(moves[1],j[1]);
                Check(Vector3.Dot(headWay,f)*p.Fall<-.4f,p.Cause+": it goes down "+(p.Fall>0?"on its back":"on its face")+", the head the fall's way");
                NativeSmashGib.Sprawl(p,0,j,moves);Check(moves.All(m=>m.Turn==Quaternion.Identity&&m.Offset==Vector3.Zero),p.Cause+": no sprawl at the death");
                // Blood squirts out from under the edges at the floor, outward; the pool lies round the body.
                int squirts=0;for(int k=0;k<NativeSmashGib.Gushes;k++)if(NativeSmashGib.Gush(p,k,out float at,out var o,out var n,out var along,out var carry,out string kind,out _)){squirts++;bool edge=NativeSmashGib.Outside(p,o)>.02f||Vector3.Distance(new Vector3(o.X,0,o.Z),new Vector3(p.Body.X,0,p.Body.Z))>1.55f;Check(kind=="squirt"&&at>=p.Contact&&at<p.Contact+.2f&&Math.Abs(o.Y-p.Floor)<.05f&&Vector3.Dot(new Vector3(o.X-p.Body.X,0,o.Z-p.Body.Z),along)>=-1e-4f&&Vector3.Dot(carry,along)>0&&edge,p.Cause+": squirt "+k+" leaves from under the crusher's edge (or 1.6 m out under a big load), outward, at the floor");}
                Check(squirts==NativeSmashGib.Gushes,p.Cause+": "+squirts+" squirts");
                for(int k=0;k<NativeSmashGib.Pools;k++){Check(NativeSmashGib.Pool(p,k,out float at,out var o,out _,out float len,out float w)&&o.Y==p.Floor&&at>=p.Contact&&len>0&&w>0&&Vector3.Distance(new Vector3(o.X,0,o.Z),new Vector3(p.Body.X,0,p.Body.Z))<1.65f,p.Cause+": splat "+k+" lies on the floor round the body");}
            }
            Check(NativeSmashGib.PoolCount(1,false)==NativeSmashGib.Pools&&NativeSmashGib.PoolCount(.3f,false)<NativeSmashGib.Pools&&NativeSmashGib.PoolCount(1,true)<NativeSmashGib.PoolCount(.3f,false),"reduced effects and the low tier lay fewer splats");
            report.Add($"pinned: squashed under the crusher in its timing, flat at {NativeSmashGib.Flat} for good, spread to {NativeSmashGib.SpreadMax}; the sprawl rigid, joined and on the floor; {NativeSmashGib.Gushes} squirts from under the edges, {NativeSmashGib.Pools} splats");

            // ---- 5. thrown: the flight, the fragments, the blood along the travel ----
            foreach(var p in new[]{tp,ep})
            {
                var centre=p.Body;
                foreach(int b in NativeBisect.Bodies){NativeSmashGib.Throw(p,b,centre+new Vector3(.1f*b%3,.05f*b,0),centre,out var v,out var w);Check(Vector3.Dot(v,p.Travel)>p.Throw*.6f&&v.Y>0&&w.Length()>0,p.Cause+": body "+b+" flies along the travel, tumbling");}
                NativeSmashGib.Fragments(p,out var inherit,out float kick,out float sev);Check(Vector3.Dot(inherit,p.Travel)>0&&inherit.Length()+kick+6<=NativeSmashGib.MaxCarry+1e-3f,p.Cause+": the fragments are carried along the travel, clamped ("+inherit.Length().ToString("0.0")+" m/s)");
                int gushes=0;for(int k=0;k<NativeSmashGib.Gushes;k++)if(NativeSmashGib.Gush(p,k,out float at,out var o,out var n,out var along,out var carry,out string kind,out _)){gushes++;Check(at<.1f&&Vector3.Dot(carry,p.Travel)>0&&Vector3.Dot(along,p.Travel)>.99f&&(kind=="sever"||kind=="crush"),p.Cause+": gush "+k+" bursts along the travel at the strike");}
                Check(gushes==(p.Cause==SmashCause.Train?NativeSmashGib.Gushes:4),p.Cause+": "+gushes+" gushes");
                for(int k=0;k<NativeSmashGib.Pools;k++)Check(NativeSmashGib.Pool(p,k,out _,out var o,out var along,out _,out _)&&o.Y==p.Floor&&Vector3.Dot(o-p.Body,p.Travel)>0&&Vector3.Dot(along,p.Travel)>.9f,p.Cause+": streak "+k+" on the floor ahead, along the travel");
                Check(NativeSmashGib.Squash(p,.5f)==1&&!NativeSmashGib.Overlay(p,.5f,out _,out _,out _,out _),p.Cause+": a thrown body is not flattened");
            }
            Check(NativeSmashGib.Flash(tp,0,out _,out float f0,out float l0)&&l0==1&&NativeSmashGib.Flash(tp,.06f,out _,out float f1,out float l1)&&l1<l0&&f1>f0&&!NativeSmashGib.Flash(tp,.13f,out _,out _,out _)&&!NativeSmashGib.Flash(pp,0,out _,out _,out _),"the train's impact flash: bright at the strike, gone by .12 s; none for a crusher");
            int mists=0;for(float t=0;t<1.4f;t+=.05f)for(int k=0;k<NativeSmashGib.Mists;k++)if(NativeSmashGib.Mist(tp,k,t,out var o,out _,out float op,out _)){mists++;Check(op>0&&op<=.6f&&Vector3.Dot(o-tp.Body,tp.Travel)>-.4f,"the train's mist rides along the travel");}
            Check(mists>0,"the train leaves a blood mist");
            report.Add($"thrown: every body along the travel, fragments carried at {tp.Carry:0.#} m/s (clamped to {NativeSmashGib.MaxCarry}), {NativeSmashGib.Gushes} gushes and streaks along the track, a {NativeSmashGib.Mists}-puff mist and the impact flash");

            // ---- 6. the stale-wound guard ----
            var old=new NativeWound{at=8};var fresh=new NativeWound{at=10};
            Check(NativeSmashGib.Stale("hazard",10,old)&&NativeSmashGib.Stale("maglev",10,old)&&!NativeSmashGib.Stale("hazard",10,fresh)&&!NativeSmashGib.Stale(null,10,old)&&!NativeSmashGib.Stale("hazard",10,null),"a stage death never re-bleeds an older wound as its own (the authority now clears it; the guard stays)");
            report.Add("stale wound: a stage death's older wound does not bleed again");

            // ---- 7. the arrow: halfway in, through, which hits stick ----
            var chestFront=new Vector3(0,.1f,.6f);var back=-Vector3.UnitZ;
            Check(NativeArrowGib.Lodge(CapturedRig.Chest,chestFront,back,11,out var entry,out float depth),"a chest shot meets the chest's boxes");
            Check(NativeArrowGib.Extent(CapturedRig.Chest,chestFront,back,out float enter,out float exit)&&Math.Abs(entry.Z-(chestFront.Z-enter))<1e-5f&&Math.Abs(entry.Z-.169f)<.002f,"the arrow enters at the chest's front surface (z "+entry.Z.ToString("0.000")+")");
            float chestThick=exit-enter;Check(depth/chestThick>.38f&&depth/chestThick<.62f,"a chest arrow stops about the middle ("+depth.ToString("0.000")+" of "+chestThick.ToString("0.000")+" m)");
            var depths=new List<float>();for(uint s=0;s<200;s++){NativeArrowGib.Lodge(CapturedRig.Head,new Vector3(0,.165f,.6f),back,s,out _,out float d);depths.Add(d);}
            Check(depths.All(d=>d/.32f>.38f&&d/.32f<.62f)&&depths.Max()-depths.Min()>.02f,"a head shot stops about halfway into the head ("+depths.Min().ToString("0.000")+" .. "+depths.Max().ToString("0.000")+" of .32 m), seeded");
            NativeArrowGib.Lodge(CapturedRig.LKnee,new Vector3(.5f,-.14f,0),-Vector3.UnitX,3,out _,out float shin);Check(shin>NativeArrowGib.MinDepth&&shin<.12f,"a shin arrow stops about its middle ("+shin.ToString("0.000")+" m)");
            Check(!NativeArrowGib.Lodge(CapturedRig.Head,new Vector3(3,3,3),back,1,out var missed,out float bury)&&bury==NativeBowStuck.Bury&&missed==new Vector3(3,3,3),"an arrow that misses the boxes keeps the hit point and the old bury");
            Check(NativeArrowGib.Through(CapturedRig.Chest,chestFront,back,out var tin,out var tout)&&Math.Abs(tin.Z-.169f)<.002f&&tout.Z< -.14f,"a pass-through enters at the front and leaves at the back ("+tout.Z.ToString("0.000")+")");
            foreach(var detail in new[]{"direct","secondary"})Check(NativeBowWire.Sticks(new ArenaEvent{Kind=EventKind.Hit,Detail=detail},"bow")&&NativeBowWire.Sticks(new ArenaEvent{Kind=EventKind.Headshot,Detail=detail},"bow"),"an arrow's "+detail+" hit sticks");
            foreach(var detail in new[]{"thorns","chain","blast","reflected","field","ability","kernel-panic-wave",null})Check(!NativeBowWire.Sticks(new ArenaEvent{Kind=EventKind.Hit,Detail=detail},"bow"),"a bow pilot's "+(detail??"null")+" hit is no arrow and sticks none");
            report.Add($"arrow: a chest arrow {depth:0.000} m into {chestThick:0.000}, a head arrow about halfway, a miss keeps the old bury; only direct and secondary arrow hits stick");

            // ---- 8. through or not ----
            var shots=new ArrowShot[4];
            Check(NativeArrowGib.Fate(12,true,7,Vector3.Zero,0,null,0,out int sid)==ArrowFate.Pass&&sid==12&&NativeArrowGib.Fate(12,false,7,Vector3.Zero,0,shots,0,out _)==ArrowFate.Lodge,"a hit's own flag decides with its projectile id");
            var hit1=new Vector3(0,1,0);ArrowShot Arrow(int pierces,Vector3 at,float when)=>new ArrowShot{Id=41,Owner=7,Pierces=pierces,At=when,Gravity=14,Position=at,Velocity=new Vector3(60,0,0)};
            shots[0]=Arrow(0,new Vector3(1.28f,1,0),10.02f);Check(NativeArrowGib.Fate(0,false,7,hit1,10,shots,1,out _)==ArrowFate.Lodge,"an older frame: a pierce-0 arrow always lodges, even seen past the target");
            shots[0]=Arrow(1,new Vector3(1.28f,1,0),10.02f);Check(NativeArrowGib.Fate(0,false,7,hit1,10,shots,1,out sid)==ArrowFate.Pass&&sid==41,"an older frame: a pierce-1 arrow seen past its first target went through it");
            shots[0]=Arrow(0,new Vector3(6.1f,1,0),10.1f);Check(NativeArrowGib.Fate(0,false,7,new Vector3(5,1,0),10.08f,shots,1,out _)==ArrowFate.Lodge,"its pierce spent, it lodges in the second");
            shots[0]=Arrow(1,new Vector3(1.28f,2.4f,0),10.02f);Check(NativeArrowGib.Fate(0,false,7,hit1,10,shots,1,out _)==ArrowFate.Lodge,"an arrow whose line misses the hit is not the hit's");
            shots[0]=Arrow(1,new Vector3(1.28f,1,0),10.02f);shots[0].Owner=8;Check(NativeArrowGib.Fate(0,false,7,hit1,10,shots,1,out _)==ArrowFate.Lodge,"another shooter's arrow is not the hit's");
            shots[0]=Arrow(1,new Vector3(-.2f,1,0),10);Check(NativeArrowGib.Fate(0,false,7,hit1,10,shots,1,out _)==ArrowFate.Lodge,"an arrow not yet past the hit lodges (in doubt)");
            Check(NativeArrowGib.Fate(0,false,7,hit1,10,shots,0,out sid)==ArrowFate.Lodge&&sid==0,"no arrow seen: it lodges");
            // A volley's neighbours (3.5 degrees apart) never take each other's hit.
            for(int k=0;k<4;k++){float a=(k-1.5f)*3.5f*(float)Math.PI/180;var v=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a))*90;shots[k]=new ArrowShot{Id=100+k,Owner=7,Pierces=1,At=10.03f,Position=new Vector3(0,1,0)+v*.13f,Velocity=v};}
            var struck=shots[2].Position-shots[2].Velocity*.03f;Check(NativeArrowGib.Fate(0,false,7,struck,10,shots,4,out sid)==ArrowFate.Pass&&sid==102,"a volley's hit is matched to its own arrow");
            report.Add("fate: the replicated flag with the projectile id; older frames pass only a matched live arrow past the target with a pierce left, else lodge");

            // ---- 9. the split body's half, the bleed cap, the trickle ----
            var cut=NativeBisect.Request("check",new Vector3(0,1,0),Vector3.UnitX,Vector3.Zero,1);
            Check(NativeArrowGib.Half(cut,CapturedRig.Head,new Vector3(.2f,1.6f,0))==1&&NativeArrowGib.Half(cut,CapturedRig.Head,new Vector3(-.2f,1.6f,0))==0&&NativeArrowGib.Half(null,CapturedRig.Head,Vector3.One)==0,"an arrow rides the half of a split body its point sat on");
            cut.Mask=NativeBisect.MaskOf(CapturedRig.Head);Check(NativeArrowGib.Half(cut,CapturedRig.LKnee,new Vector3(.5f,.4f,0))==0,"a joint the cut did not cross rides the body's half");
            var stuck=new NativeBowStuck();for(int i=0;i<30;i++)stuck.AddPilot(500+i,1+i%3,CapturedRig.Chest,Vector3.Zero,Vector3.UnitZ,i*.1f,1,true,false,.15f,1);
            stuck.AddPilot(600,4,CapturedRig.Chest,Vector3.Zero,Vector3.UnitZ,3,1,true,false,.15f,0);
            int bleeding=0;int newest=-1;for(int i=0;i<stuck.Count;i++)if(NativeArrowGib.Bleeds(stuck,i,3.05f,NativeArrowGib.MaxBleeds)){bleeding++;if(stuck[i].Born<2.9f-NativeArrowGib.MaxBleeds*.1f)newest=i;}
            Check(bleeding==NativeArrowGib.MaxBleeds&&newest<0,"at most "+NativeArrowGib.MaxBleeds+" arrow points bleed at once, the newest ("+bleeding+")");
            int lowBleeding=0;for(int i=0;i<stuck.Count;i++)if(NativeArrowGib.Bleeds(stuck,i,3.05f,NativeArrowGib.BleedCap(.3f,false)))lowBleeding++;Check(lowBleeding==NativeArrowGib.LowBleeds&&NativeArrowGib.BleedCap(1,true)==NativeArrowGib.LowBleeds,"reduced effects and the low tier bleed "+NativeArrowGib.LowBleeds);
            int lastDrops=0;bool decays=true;for(float t=0;t<NativeArrowGib.DripLife+3;t+=.25f){int n=NativeArrowGib.Drips(t,1,1,false);if(n<lastDrops)decays=false;lastDrops=n;}
            Check(decays&&NativeArrowGib.Drips(0,1,1,false)==0&&NativeArrowGib.Drips(1,1,1,false)>NativeArrowGib.Drips(4,1,1,false)-NativeArrowGib.Drips(3,1,1,false)&&lastDrops==NativeArrowGib.Drips(NativeArrowGib.DripLife,1,1,false)&&lastDrops>=4&&lastDrops<=10,"an arrow point drips a short, decaying trickle ("+lastDrops+" drops)");
            Check(NativeArrowGib.Drips(3,1,.3f,false)<NativeArrowGib.Drips(3,1,1,false)&&NativeArrowGib.Drips(3,1,1,true)<NativeArrowGib.Drips(3,1,1,false)&&NativeArrowGib.Drips(3,0,1,false)==0,"reduced effects, the low tier and no blood drip less");
            Check(NativeArrowGib.Strength(false,1,true,true)==0&&NativeArrowGib.Strength(true,1,true,false)>NativeArrowGib.Strength(true,1,false,false)&&NativeArrowGib.Strength(true,.5f,false,true)>NativeArrowGib.Strength(true,.5f,false,false),"gore off bleeds none; a killing full-draw arrow and an exit bleed more");
            NativeArrowGib.Exit(1,true,out float exitSev,out int exitExtra);NativeArrowGib.Exit(0,false,out float quickSev,out int quickExtra);Check(exitSev>quickSev&&exitExtra>quickExtra&&exitSev<=1,"a killing full-draw pass-through sprays harder from its exit");
            report.Add($"blood: a split body's arrows ride their half; {NativeArrowGib.MaxBleeds} points bleed at once ({NativeArrowGib.LowBleeds} reduced/low), a {lastDrops}-drop trickle over {NativeArrowGib.DripLife} s; none with gore off");

            // ---- 10. nothing allocates per frame ----
            var j0=Joints(joints,pp);var holder=new ArrowShot[1]{Arrow(1,new Vector3(1.28f,1,0),10.02f)};var framePlans=new[]{pp,tp};
            void Frame(float t)
            {
                foreach(var p in framePlans)
                {
                    NativeSmashGib.Squash(p,t);NativeSmashGib.Overlay(p,t,out _,out _,out _,out _);NativeSmashGib.Sprawl(p,NativeSmashGib.Progress(p,t),j0,moves);NativeSmashGib.Flash(p,t,out _,out _,out _);
                    for(int k=0;k<NativeSmashGib.Gushes;k++)NativeSmashGib.Gush(p,k,out _,out _,out _,out _,out _,out _,out _);for(int k=0;k<NativeSmashGib.Pools;k++)NativeSmashGib.Pool(p,k,out _,out _,out _,out _,out _);for(int k=0;k<NativeSmashGib.Mists;k++)NativeSmashGib.Mist(p,k,t,out _,out _,out _,out _);
                }
                NativeArrowGib.Lodge(CapturedRig.Head,new Vector3(0,.165f,.6f),back,7,out _,out _);NativeArrowGib.Fate(0,false,7,hit1,10,holder,1,out _);NativeArrowGib.Drips(t,1,1,false);for(int i=0;i<stuck.Count;i++)NativeArrowGib.Bleeds(stuck,i,t,NativeArrowGib.MaxBleeds);
            }
            for(float t=0;t<1;t+=1/60f)Frame(t);long before=GC.GetAllocatedBytesForCurrentThread();for(float t=0;t<4;t+=1/60f)Frame(t);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"a frame of the smash and the arrow gib allocates "+bytes+" bytes");
            report.Add("allocation: 240 frames of the squash, sprawl, blood, mist, flash, lodge, fate, trickle and bleed cap allocate 0 bytes");

            // ---- 11. wiring (text) and isolation ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f)).Replace("\r","");string C(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f)).Replace("\r","");
            var death=R("NativeDeathPresentation.cs");var smash=R("NativeSmashPresentation.cs");var kill=R("NativeKillEffectPresentation.cs");var bow=R("NativeBowPresentation.cs");var arrow=R("NativeArrowGibPresentation.cs");
            Check(death.Contains("var gib=pilot?HazardGibOf(arena,death):null;")&&death.Contains("!remains&&gib==null&&SourceDeathScene(death,age,up)")&&death.Contains("if(corpse==null&&gib?.Smash!=null)corpse=new NativeCorpse(death,rig,arena,null,gib.Smash);"),"a hazard death gibs instead of its kill effect's scene; no gib, the corpse as before");
            Check(death.Contains("Style=Smash!=null||stock&&release<0?\"impact-ragdoll\":")&&R("NativeBisectCorpse.cs").Contains("stock:stock")&&R("NativeBisectPresentation.cs").Contains(",true);"),"a smash and a saw's halves are the stock ending whatever the death's style (owner 2026-10-14)");
            Check(death.Contains("if(Halved||Smash!=null)wound=null;")&&death.Contains("NativeSmashGib.Stale(death.damageKind,death.at,w)"),"a smash ignores the recorded wound; no stage death bleeds an old one");
            Check(death.Contains("DriveCollapse(clock);ReleaseHalves();DriveSmash(clock);")&&death.Contains("SmashSetup();")&&death.Contains("overlay=SmashOverlay(corpse.Smash,shown)*overlay;")&&death.Contains("if(corpse.Smash!=null)SmashBlood(corpse,death,KillClock(arena));"),"a smash is driven on the corpse clock, flattened as drawn and bleeds on the kill clock");
            Check(kill.Contains("if(crush&&Smash!=null)")&&kill.Contains("NativeSmashGib.Fragments(Smash,out var inherit,out float kick,out float severity);")&&kill.Contains("private bool Shown(int b)=>!IsCrushed(Anchor[b]);"),"a thrown smash crushes its parts to fragments along its travel (one crushed part as before otherwise)");
            Check(smash.Contains("NativeSmashGib.Plan(Gore,death.style,death.kept,death.model,death.damageKind,arena.MapId,hazards,death.hazardId,")&&smash.Contains("NativeSmashGib.Saw(hazards,death.hazardId,"),"the plan comes from the death record, its hazard id and the map's hazards");
            Check(R("NativeBloodPresentation.cs").Contains("BisectSparks(now);SmashMist(now);")&&R("NativeCinematicCamera.cs").Contains("presentation.HazardGibbed(arena,d)"),"the mist and flash draw with the blood; a gibbed death plays no kill-effect screen");
            Check(R("NativeCorpseWorld.cs").Contains("if(weapon!=null&&!Pinned)weapon.isKinematic=false;")&&R("NativeBisectCorpse.cs").Contains("IsCrushed(part)||standIn&&part==CapturedRig.Pelvis||Pinned"),"a pinned body never wakes into a ragdoll");
            Check(bow.Contains("ArrowStrike(arena,e,rig,bone,at,dir,draw,std);")&&bow.Contains("bowStuck.Draw(bowSink,time,bowBone);ArrowBleeds(time);")&&bow.Contains("var half=ArrowHalf(corpse,bone,local);if(!half.ShownBone(bone))return false;world=BowN(half.PresentedBone(bone));"),"the arrow lodges or passes, bleeds, and rides the drawn body's half");
            Check(arrow.Contains("NativeArrowGib.Fate(e.Projectile,e.Pierced,e.Source,")&&arrow.Contains("blood.Spray(\"cavity\"")&&!arrow.Contains("\"puncture\""),"the replicated id and flag decide; the exit sprays, the wound's own spray stays the wound path's");
            Check(!R("NativeWoundPresentation.cs").Contains("Arrow"),"the wound path is untouched (its puncture or cavity spray is the arrow wound's)");
            foreach(var f in new[]{"NativeSmashGib.cs","NativeArrowGib.cs"}){var text=C(f);Check(!text.Contains("UnityEngine")&&!text.Contains("new Random(")&&!text.Contains("DateTime"),f+" stays engine free and deterministic");}
            foreach(var f in new[]{"Runtime/NativeArena.cs","Runtime/NativeDeaths.cs","Runtime/NativeProjectiles.cs","Runtime/NativeStageHazardsRuntime.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeFrameCodec.cs","Runtime/NativeBow.cs","Core/NativeFactoryHazards.cs","Core/NativeStageHazards.cs","Core/NativeCyberCityHazards.cs","Core/NativeBodyDamage.cs","Core/NativeCombatRules.cs"})
            {var path=Path.Combine(root,"Assets/ChipArena",f);if(File.Exists(path))Check(!Regex.IsMatch(File.ReadAllText(path),@"SmashGib|SmashPlan|HazardGib|ArrowGib|ArrowFate|ArrowShot"),f+" (simulation, rules, the wire) never reads the gibs");}
            report.Add("wiring: hazard deaths gib whatever their style; the corpse, overlay, blood, fragments and mist hooked; arrows lodge, pass, bleed and ride the drawn half; no simulation file reads any of it");

            foreach(var line in report)Console.WriteLine("PASS gib: "+line);
            Console.WriteLine("PASS gib: "+count+" checks");
        }
        // A standing body's joints (the rig at rest, Unity world) with its feet at `feet`, facing `yaw`.
        private static Vector3[] Rest(Vector3 feet,float yaw)
        {
            var pose=new CapturedRig.Pose();pose.World();var q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*(float)Math.PI/180);var j=new Vector3[22];float lowest=float.MaxValue;
            for(int b=0;b<22;b++){j[b]=Vector3.Transform(CapturedRig.Mirror(pose.WorldPosition[b]),q);lowest=Math.Min(lowest,j[b].Y);}
            for(int b=0;b<22;b++)j[b]+=feet-new Vector3(0,lowest,0);return j;
        }
        // The same body moved to a plan's place (its feet on the plan's floor under its body centre).
        private static Vector3[] Joints(Vector3[] rest,SmashPlan p)
        {
            var j=new Vector3[22];float lowest=rest.Min(v=>v.Y);var shift=new Vector3(p.Body.X-rest[CapturedRig.Pelvis].X,p.Floor-lowest,p.Body.Z-rest[CapturedRig.Pelvis].Z);for(int b=0;b<22;b++)j[b]=rest[b]+shift;return j;
        }
    }
}
