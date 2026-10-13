using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private NativePveState wardSandboxState;
        public bool SandboxWardEntry(int entry)
        {
            if(!SandboxAllowed||Map.look!="quarantine")return false;
            var e=StageContent.entries.FirstOrDefault(e=>e.e==entry);if(e==null)return false;
            if(Pve==null&&wardSandboxState==null)wardSandboxState=new NativePveState{Content=StageContent,Boss=Pilots.FirstOrDefault(p=>p!=Local)??Local,Stage="wave",Wave=1,Cap=8,SpawnRandom=new NativePveRandom(12345)};
            return SpawnPveAdd(e.kind=="breach"||e.hulk?"hulk":e.kind=="drop"?"brute":"shambler",true,entry)!=null;
        }
        private void StepWardSandbox()
        {
            if(!SandboxAllowed||Pve!=null)return;
            foreach(var m in Minions.Where(m=>m.Pve!=null))
            {if(Time<m.Pve.ActiveAt)WardEntryPose(m);else if(m.Body.isKinematic){m.Body.isKinematic=false;m.Collider.enabled=true;}}
        }
        private readonly List<NativeBreachRecord> wardBreaches=new List<NativeBreachRecord>();
        public IEnumerable<NativeBreachRecord> WardBreaches=>wardBreaches;
        private void NameWardPanels()
        {
            if(Map.look!="quarantine")return;
            foreach(var e in StageContent.entries.Where(e=>e.kind=="breach"))
            {var prop=StaticBlocks.FirstOrDefault(p=>p.Definition.id==e.block);if(prop!=null)prop.gameObject.name=NativeBreach.ObjectName(e.id);}
        }
        private bool BreachAvailable(PveEntry e)=>!wardBreaches.Any(b=>b.entry==e.e&&Time<NativeBreach.SolidAt(b.restoreAt));
        private void BeginWardBreach(PveEntry e,float active)
        {
            wardBreaches.RemoveAll(b=>b.entry==e.e);
            float broken=NativeBreach.BreakAt(active);
            wardBreaches.Add(new NativeBreachRecord{entry=e.e,breakAt=broken,restoreAt=NativeBreach.DefaultRestoreAt(broken)});
        }
        private void PoseWardBreaches()
        {
            if(Map.look!="quarantine")return;
            foreach(var b in wardBreaches)
            {
                var e=StageContent.entries.FirstOrDefault(e=>e.e==b.entry&&e.kind=="breach");if(e==null)continue;
                var prop=StaticBlocks.FirstOrDefault(p=>p.Definition.id==e.block);if(prop==null)continue;
                bool Overlap()=>Pilots.Any(p=>p.Hp>0&&p.Seat.connected&&NativeStageHazards.Touch(Vec(p.Body.position),e.center,e.size))||
                    Minions.Any(m=>m.Hp>0&&NativeStageHazards.Touch(Vec(m.Body.position),e.center,e.size,m.Collider.radius,m.Collider.height/2));
                // Extend before rebuilding and again at solidity: no capsule can be sealed into the panel.
                if(!Replica&&Time>=b.restoreAt&&Time<NativeBreach.SolidAt(b.restoreAt)+1f/60&&Overlap())b.restoreAt=Math.Max(b.restoreAt+.5f,Time+.5f);
                prop.GetComponent<Collider>().enabled=Time<b.breakAt||Time>=NativeBreach.SolidAt(b.restoreAt);
            }
        }
    }
}
