using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public float SandboxSplatDelay=10;public string SandboxPreviewRarity{get;private set;}
        private string[] sandboxChipQueue;private int sandboxChipIndex;private float sandboxChipAt;
        public bool SandboxConfigure(string map=null,string weapon=null,string difficulty=null)
        {
            if(!SandboxAllowed)return false;
            if(map!=null&&!Content.maps.Any(m=>m.id==map&&NativeMapRules.ArenaSelectable(m))||weapon!=null&&!Content.weapons.Any(w=>w.id==weapon&&(!w.devOnly||Local.Seat.access.Allows("weapon:"+weapon,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))))return false;
            if(difficulty!=null)foreach(var p in Pilots.Where(p=>p.Seat.bot))p.Seat.difficulty=NativeLobbyRules.Difficulty(difficulty);
            if(weapon!=null){Local.Seat.spec=weapon;Local.Seat.chips.Clear();}if(map!=null)MapId=map;if(map!=null||weapon!=null){sandboxDummy=null;Reset();}return true;
        }
        public void SandboxReset(){if(!SandboxAllowed)return;sandboxChipQueue=null;SandboxPreviewRarity=null;procLedger.Clear();Reset();Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Time,fightAt=Time});}
        public void SandboxTestBlock(){if(!SandboxAllowed)return;SandboxRestore(Local);var other=Pilots.FirstOrDefault(p=>p.Seat.bot);if(other==null)return;SandboxCast("sword","ai");other.Body.position=Local.Body.position+Vector3.forward*2;Local.Input=new InputFrame{Yaw=0};TryBlock(Local);}
        public void SandboxPreview(string kind,string rarity="common")
        {
            if(!SandboxAllowed)return;if(kind=="my-death")SandboxAction("down",true);else if(kind=="knockout")SandboxAction("down");else if(kind=="draft"||kind=="rarity"){SandboxPreviewRarity=rarity;Match.PreviewDraft(Local.Seat.id,kind=="rarity"?rarity:null);}
        }
        public bool SandboxLaunchDebris(float mass,float speed,string material,string impact="face")
        {
            if(!SandboxAllowed||float.IsNaN(mass)||float.IsNaN(speed)||float.IsInfinity(mass)||float.IsInfinity(speed)||(impact!="face"&&impact!="edge"&&impact!="corner")||Props.Count>=360||!Content.materials.Any(m=>m.id==material&&!m.indestructible))return false;
            var dummy=Pilots.FirstOrDefault(p=>p.Seat.bot);if(dummy==null)return false;var dir=(dummy.Body.position-Local.Body.position).normalized;Vector3 at=Local.Body.position+dir*1.5f+Vector3.up;
            Quaternion rotation=Quaternion.Euler(impact=="corner"?45:0,impact=="face"?0:45,0);var def=new ContentBlock{id="sandbox-debris-"+nextChunk++,pos=Vec(at),size=new V3(.8f,.8f,.8f),rotation=new ContentRotation{x=rotation.x,y=rotation.y,z=rotation.z,w=rotation.w},material=material,dynamic=true,mass=Mathf.Clamp(mass,1,500)};
            var prop=AddProp(def,true,source:Local);prop.Body.linearVelocity=dir*Mathf.Clamp(speed,1,100);return true;
        }
        public bool SandboxCastChip(string id,string key="ai")
        {
            if(!SandboxAllowed)return false;var chip=Content.chips.FirstOrDefault(c=>c.id==id);var dummy=Pilots.FirstOrDefault(p=>p.Seat.bot);if(chip==null||dummy==null)return false;
            var weapon=Content.weapons.OrderBy(w=>w.id==dummy.Seat.spec?0:1).ThenBy(w=>w.devOnly).FirstOrDefault(w=>(!w.devOnly||Local.Seat.access.Allows("weapon:"+w.id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))&&NativeContentRules.Eligible(Content,w.id,new System.Collections.Generic.Dictionary<string,int>()).Any(c=>c.id==id));if(weapon==null)return false;
            dummy.Seat.chips.Clear();SandboxCast(weapon.id,key);SandboxChip(dummy.Seat.id,id,1);return true;
        }
        public void SandboxCastEveryChip(){if(!SandboxAllowed)return;sandboxChipQueue=Content.chips.Select(c=>c.id).ToArray();sandboxChipIndex=0;sandboxChipAt=Time;}
        private void SandboxNextChip(){if(sandboxChipQueue==null||Time<sandboxChipAt)return;while(sandboxChipIndex<sandboxChipQueue.Length){var id=sandboxChipQueue[sandboxChipIndex++];if(!SandboxCastChip(id))continue;sandboxChipAt=Time+5;return;}sandboxChipQueue=null;}
        public void SetPilotConnected(int id,bool connected)
        {var p=Pilots.Find(p=>p.Seat.id==id);if(p==null)return;if(!connected)p.Seat.disconnectedAt=Time;else{if(Expedition==null&&p.Seat.disconnectedAt>=0&&Time-p.Seat.disconnectedAt>=12)p.Hp=0;p.Seat.disconnectedAt=-1;p.Seat.alive=p.Hp>0;}p.Seat.connected=connected;p.Collider.enabled=connected&&p.Hp>0;p.Body.isKinematic=!connected||p.Hp<=0;ClearRemoteInput(id);if(!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;}
    }
    public sealed partial class ConversionGame
    {
        public void SetSandboxTimeScale(float scale){if(arena?.Sandbox!=true||Online||float.IsNaN(scale)||float.IsInfinity(scale))return;sandboxSpeed=Mathf.Clamp(scale,.1f,2);Time.timeScale=sandboxSpeed;}
    }
}
