#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using System.Text;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Real Unity gate. Compiling this is not executing JsonUtility, PhysX or a Windows multiplayer test.
        public void ValidateBrickfallRuntime()
        {
            if(MapId!="brickfall")throw new InvalidOperationException("Brickfall fixture map required");
            void Check(bool ok,string why){if(!ok)throw new Exception("Brickfall runtime: "+why);}
            while(Match.Seats.Count<8){int i=Match.Seats.Count;var s=new NativeSeat{id=i+1,name="Brickfall check "+i,spec="pistol",team=i%2};Match.Seats.Add(s);SpawnPilot(s,i);}
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});
            var start=NativeFrameCodec.Clone(NetworkFrame());Check(start.Valid(Content),"round-start frame invalid");
            Check(start.props.Length==Map.blocks.Count(b=>b.role!="static"&&b.role!="carried"),"static/carried blocks on wire");
            Check(start.segments.Length==10,"segment inventory");
            var bad=NativeFrameCodec.Clone(start);bad.contentRevision="bad";Check(!bad.Valid(Content),"revision mismatch accepted");bad=NativeFrameCodec.Clone(start);bad.version=5;Check(!bad.Valid(Content),"old protocol accepted");
            var pier=Props.Single(p=>p.Definition.id=="hotel-pier-nw");float hp=pier.Health;
            DamageProp(pier,10000,Vector3.zero,pier.transform.position,Local,"bullet");DamageProp(pier,10000,Vector3.zero,pier.transform.position,Local,"melee");Check(pier.Health==hp,"non-blast pier damage");
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=3.9,fightAt=0});DamageProp(pier,10000,Vector3.zero,pier.transform.position,Local,"blast",36);Check(pier.Health==hp,"arm lock");
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});DamageProp(pier,10,Vector3.zero,pier.transform.position,Local,"blast",36);Check(Math.Abs(pier.Health-(hp-15))<.001,"demolition cap");
            var self=Local;triggeringMembers["hotel-nw"]=new System.Collections.Generic.HashSet<string>{"hotel-pier-nw"};self.Stats["damageTaken"]=1.59;float initial=self.Hp;
            CollapseDamage(self,"hotel-nw",45,Vector3.zero,0);CollapseDamage(self,"hotel-nw",25,Vector3.zero,0);
            Check(Math.Abs(initial-self.Hp-Math.Min(60,.6*initial))<.01&&self.Hp>0,"self-crush shared final allowance");
            var teammate=Pilots[1];
            Check(teammate.Hp>45&&teammate.Seat.alive&&teammate.Seat.connected&&teammate.Death==null,"friendly-fire fixture must be a fresh live pilot");
            Check(Credit(Map.segments.First(s=>s.id=="hotel-nw"))==self.Seat.id,"collapse credited to fixture attacker");
            Match.Teams=true;FriendlyFire=false;teammate.Seat.team=self.Seat.team;initial=teammate.Hp;
            float blocked=CollapseDamage(teammate,"hotel-nw",45,Vector3.zero,0);
            Check(blocked==0&&teammate.Hp==initial,"friendly-fire off");
            FriendlyFire=true;float dealt=CollapseDamage(teammate,"hotel-nw",45,Vector3.zero,0);
            Check(Math.Abs(dealt-45)<.001f&&Math.Abs(initial-teammate.Hp-45)<.001f,"friendly-fire on: expected 45 actual damage, got "+dealt);
            Match.Teams=false;
            // Real codec/transport packet sizes at start and at the 80-fragment peak (8 pilots).
            void Budget(string name){var f=NetworkFrame();string json=NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=f});var packet=NativeFrameBudget.Encode(f,1);Check(packet!=null&&packet.Length<=NativeFrameBudget.MaxPacketBytes,"frame budget "+name);Debug.Log("PASS Brickfall frame "+name+": JSON="+Encoding.UTF8.GetByteCount(json)+" bytes, packet="+packet.Length+" bytes, props="+f.props.Length+", fragments="+Props.Count(p=>p.Fragment));}
            Budget("round start");foreach(var p in Props.Where(p=>p.Definition.role=="cover").Take(20).ToArray())Fracture(p,Vector3.zero,Local);Check(Props.Count(p=>p.Fragment)<=80,"fragment cap");var peak=Structures.Snapshot();foreach(var s in peak.Take(3)){s.state=2;s.t0=Time;}Structures.Restore(peak,Time);Budget("80 fragments / 3 falls");Structures.Restore(new NativeStructureState(Map).Snapshot(),Time);
            var before=NativeFrameCodec.Clone(NetworkFrame());var landed=Structures.Snapshot();foreach(var s in landed){s.state=3;s.t0=1;}RestoreStructures(landed,10,before.props);Physics.SyncTransforms();int exits=0;
            foreach(var b in Map.segments.SelectMany(s=>s.rubble))foreach(float x in new[]{-.4f,0,.4f})foreach(float z in new[]{-.4f,0,.4f})
            {
                var origin=new Vector3(b.pos.x+b.size.x*x,.88f,b.pos.z+b.size.z*z);if(!NativeStructureRules.Contains(Vec(origin),b.pos,b.size,.42f))continue;
                Check(FindRubbleExit(origin,out var exit)&&Vector3.Distance(origin,exit)<=3.001f,"rubble ejection "+b.id+" at "+origin);exits++;
            }
            RestoreStructures(before.segments,(float)before.time,before.props);Debug.Log("PASS Brickfall capsule ejection: "+exits+" sampled all-rubble overlaps");
            Debug.Log("PASS Brickfall runtime damage, compatibility and frame budgets");
        }
    }
}
#endif
