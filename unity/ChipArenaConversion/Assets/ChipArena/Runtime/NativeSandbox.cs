using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public bool Sandbox,SafeMode;public float DummyRespawn=5;private float sandboxCastUntil,sandboxRepeatAt;public float SandboxRepeat;private string sandboxKey="ai";private bool sandboxTap;private NativePilot sandboxDummy;
        private bool SandboxAllowed=>Sandbox&&!Replica&&Local?.Seat.access?.Developer==true&&Local.Seat.access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private bool SandboxHolds()=>SandboxAllowed&&(Pilots.Any(p=>InShaft(p))||DummyRespawn>0&&Local.Hp>0);
        private void SandboxStep()
        {
            if(!SandboxAllowed)return;StepWardSandbox();SandboxNextChip();foreach(var p in Pilots)if(p.Seat.bot&&p.Hp<=0&&p.Death!=null&&DummyRespawn>0&&!InShaft(p)&&Time-p.Death.at>=DummyRespawn)SandboxRestore(p);
            if(SandboxRepeat>0&&Time>=sandboxRepeatAt&&sandboxDummy!=null){SandboxCast(sandboxDummy.Seat.spec,sandboxKey);sandboxRepeatAt=Time+SandboxRepeat;}
        }
        private void SandboxRestore(NativePilot p)
        {
            if(!TrySandboxSpawn(p,out var spawn))return;CancelSiren(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);CancelAbilities(p);ReleaseControl(p);p.Down=null;p.Juggle=null;p.Dots.Clear();p.Death=null;p.Wounds.Clear();p.FrozenUntil=p.ScareUntil=0;p.ScareBy=null;p.RevivedAt=Time;p.AirJumps=0;p.AirJumpAt=-100;p.Seat.alive=true;p.Body.isKinematic=false;p.Body.position=spawn;p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Collider.enabled=true;p.Hp=(float)p.Stats["hp"];SandboxRefresh(p);
        }
        private void SandboxRefresh(NativePilot p)
        {p.Cyber=new NativeCyberKit();p.CyberTrailTick=p.CyberWallTick=p.CyberLinkTick=0;CancelAbilities(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);p.Ready=p.QReady=p.EReady=p.RollReady=p.BlockReady=p.ReloadEnd=p.StunEnd=p.RollEnd=p.AttackEnd=0;p.Stamina=(float)p.Stats["maxStamina"];p.Mana=(float)p.Stats["maxMana"];p.Ammo=Magazine(p);p.ECharges=NativeAbilityRules.Charges(p.Stats);if(p.Siren!=null)p.Siren.Ready.Clear();foreach(string key in chipCooldowns.Keys.Where(k=>k.StartsWith(p.Seat.id+":")).ToArray())chipCooldowns.Remove(key);}
        public void SandboxChip(int id,string chip,int change)
        {if(!SandboxAllowed)return;var p=Pilots.Find(p=>p.Seat.id==id);var c=Content.chips.FirstOrDefault(c=>c.id==chip);if(p==null||c==null)return;p.Seat.chips.TryGetValue(chip,out int n);if(change>0&&!NativeContentRules.Eligible(Content,p.Seat.spec,p.Seat.chips).Any(c=>c.id==chip))return;n=Math.Max(0,Math.Min(c.max,n+Math.Sign(change)));if(n==0)p.Seat.chips.Remove(chip);else p.Seat.chips[chip]=n;p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);p.Hp=(float)p.Stats["hp"];SandboxRefresh(p);}
        public void SandboxCast(string spec,string key)
        {
            if(!SandboxAllowed||!Content.weapons.Any(w=>w.id==spec&&(!w.devOnly||Local.Seat.access.Allows("weapon:"+spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))))return;var p=Pilots.FirstOrDefault(p=>p.Seat.bot);if(p==null)return;SandboxRestore(p);if(p.Seat.spec!=spec)p.Seat.chips.Clear();p.Seat.spec=spec;p.Seat.access=NativeAccess.ScopedWeapon(Local.Seat.access,spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());p.Weapon=Content.Weapon(spec);p.Stats=NativeContentRules.Stats(Content,spec,p.Seat.chips);p.Hp=(float)p.Stats["hp"];SandboxRefresh(p);sandboxDummy=p;sandboxKey=key;sandboxTap=true;sandboxCastUntil=Time+(key=="ai"?4:key=="1"&&spec=="ansatsuken"&&Has(p,"arcade-shinku")?1.5f:key=="q"?1.2f:.65f);sandboxRepeatAt=Time+Math.Max(2,SandboxRepeat);
        }
        private InputFrame SandboxInput(NativePilot p,InputFrame input)
        {
            if(!SandboxAllowed||p!=sandboxDummy||Time>=sandboxCastUntil)return input;Vector3 d=Local.Body.position-p.Body.position;float angle=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;if(sandboxKey=="ai")return Bot(p,true);
            var f=new InputFrame{Yaw=angle,Pitch=Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg,AimTarget=Vec(Local.Body.position),AbilityTarget=Vec(Local.Body.position-Vector3.up*.85f)};
            f.Shoot=sandboxKey=="primary"&&Time<sandboxCastUntil-.1f;f.Attack=sandboxKey=="primary"&&sandboxTap;f.AbilityQ=(sandboxKey=="q"||sandboxKey=="mode")&&sandboxTap;f.Aim=sandboxKey=="q"&&NativeRangedRules.Scope(p.Seat.spec);f.AbilityE=sandboxKey=="e"&&sandboxTap;f.AbilityEHeld=sandboxKey=="e";f.Block=sandboxKey=="block"&&sandboxTap;f.Roll=sandboxKey=="dash"&&sandboxTap;f.Jump=sandboxKey=="jump"&&sandboxTap;f.Hadoken=sandboxKey=="1"&&sandboxTap;f.HadokenHeld=sandboxKey=="1";f.Uppercut=sandboxKey=="2"&&sandboxTap;if((p.Seat.spec=="siren"||p.Seat.spec=="cyberlord")&&byte.TryParse(sandboxKey,out byte key)&&sandboxTap)f.SirenAbility=key;sandboxTap=false;return f;
        }
        public void SandboxAction(string action,bool self=false)
        {
            if(!SandboxAllowed)return;var target=self?Local:Pilots.FirstOrDefault(p=>p.Seat.bot);if(target==null)return;
            if(action=="restore")SandboxRestore(target);else if(action=="hit")Damage(target,self?Pilots.FirstOrDefault(p=>p.Seat.bot):Local,18,Vector3.back,900,false);else if(action=="down")Damage(target,self?Pilots.FirstOrDefault(p=>p.Seat.bot):Local,100000,Vector3.back,160,false,true);else if(action=="summons")foreach(var m in Minions.Where(m=>m.Owner!=Local).ToArray())HitMinion(m,Local,100000,Vector3.back,300);else if(action=="vent"&&MapId=="siren")
            {int index=Enumerable.Range(0,StageVents.Length).OrderBy(i=>(target.Body.position-StageVents[i]).sqrMagnitude).First();Entries.Add(new NativeEntryView{entry=index,kind="vent",mode="dive",openAt=Time+1.5f,closeAt=Time+6});target.Body.position=StageVents[index]+Vector3.up*.9f;target.Body.linearVelocity=Vector3.zero;}
        }
    }
    public sealed partial class ConversionGame
    {
        private int wardSandboxEntry;private bool sandboxWanted;private string dummySpec="pistol",dummyKey="ai";private float sandboxSpeed=1;
        // main.ts drawSandbox's arena row, previews and DEBRIS LAUNCHER (8 kg at 15 m/s by default, up to 150 kg, wood /
        // stone / metal); the impact choice is NativeSandboxTools' face / edge / corner orientation.
        private string previewRarity="common",debrisMaterial="wood",debrisImpact="face";private float debrisMass=8,debrisSpeed=15;private string[] previewRarities,previewRarityNames;
        // The DEV panel's sandbox routes (NativeDevPanel.cs draws the panel; Core NativeDevActions lists every action). Each
        // case is the call the old pause-menu Sandbox Tools list made, with the same arguments and limits; the panel reaches it
        // only when NativeDevActions says it runs in the sandbox (a developer's offline Developer Sandbox, SandboxAllowed on
        // the authority). Returns the status line's verdict: the bool calls report a refusal, the void ones that they ran.
        private const string DevRan="ran in the sandbox",DevRefused="refused by the sandbox";
        private static readonly string[] DummyInputs={"ai","primary","e","q","block","dash","jump","1","2","3","4","mode"};
        private static readonly string[] DebrisMaterials={"wood","stone","metal"},DebrisImpacts={"face","edge","corner"};
        // The presentation galleries (presentation only, sandbox only, as before): the gallery action, and its button when off / on.
        private static readonly string[] DevGalleries={"gallery-minions","gallery-cyber-jump","gallery-saw","gallery-revive","gallery-chip-select","gallery-hazard-kit","gallery-hazard-music","gallery-cyber-kit2-ranged","gallery-cyber-kit2-melee"};
        private static readonly string[] DevGalleryShow={"Preview Siren Minions","Preview Cyber Double Jump","Preview Saw Split","Preview Revive","Preview Chip Selecting","Preview Hazard Kit","Lock Hazard Kit to music","Preview Cyber Kit v2 ranged","Preview Cyber Kit v2 melee"};
        private static readonly string[] DevGalleryHide={"Hide Siren Minions","Hide Cyber Double Jump","Hide Saw Split","Hide Revive","Hide Chip Selecting","Hide Hazard Kit","Unlock Hazard Kit music","Hide Cyber Kit v2 ranged","Hide Cyber Kit v2 melee"};
        private bool DevGalleryOn(int i)=>presentation!=null&&(i==0?presentation.MinionGallery:i==1?presentation.CyberBoostPreviewOn:i==2?presentation.SawSplitGallery:i==3?presentation.ReviveGallery:i==4?presentation.ChipSelectPreview:i==5?presentation.HazardKitGallery:i==6?presentation.HazardKitMusic:i==7?presentation.CyberKit2RangedPreviewOn:presentation.CyberKit2MeleePreviewOn);
        private string DevRun(string id)
        {
            var kit=presentation?.Structures;var entries=NativeArena.StageContent.entries;
            switch(id)
            {
                // Pilot (self) and the dummy: restore, hit (18), down, enemy summons, the Siren vent dive.
                case "me-restore":arena.SandboxAction("restore",true);return DevRan;
                case "me-hit":arena.SandboxAction("hit",true);return DevRan;
                case "me-down":arena.SandboxAction("down",true);return DevRan;
                case "me-summons":arena.SandboxAction("summons",true);return DevRan;
                case "me-vent":arena.SandboxAction("vent",true);return DevRan;
                case "dummy-restore":arena.SandboxAction("restore");return DevRan;
                case "dummy-hit":arena.SandboxAction("hit");return DevRan;
                case "dummy-down":arena.SandboxAction("down");return DevRan;
                case "dummy-summons":arena.SandboxAction("summons");return DevRan;
                case "dummy-vent":arena.SandboxAction("vent");return DevRan;
                // My weapon (the sandbox's own configure: it resets the arena), the dummy's cast and the bots' difficulty.
                case "me-weapon":return arena.SandboxConfigure(weapon:devWeapon)?DevRan:DevRefused;
                case "dummy-cast":arena.SandboxCast(dummySpec,dummyKey);return DevRan;
                case "dummy-difficulty":return arena.SandboxConfigure(difficulty:devDifficulty)?DevRan:DevRefused;
                case "dummy-respawn":arena.DummyRespawn=Mathf.Round(Mathf.Clamp(devRespawn,0,30));return DevRan;
                case "dummy-repeat":arena.SandboxRepeat=Mathf.Round(Mathf.Clamp(devRepeat,0,60));if(arena.SandboxRepeat>0)arena.SandboxRepeat=Math.Max(2,arena.SandboxRepeat);return DevRan;
                // Chips on the Chips tab's target, one at a time (the sandbox keeps the content's max and eligibility).
                case "chip-add":arena.SandboxChip(devChipSeat,devChipId,1);return DevRan;
                case "chip-remove":arena.SandboxChip(devChipSeat,devChipId,-1);return DevRan;
                case "chip-cast":return arena.SandboxCastChip(devChipId)?DevRan:DevRefused;
                case "chip-cycle":arena.SandboxCastEveryChip();return DevRan;
                // Arena: Restore (every pilot, as the source's heal), Arena Reset, Test Block, the map, Ward Zero's entries.
                case "restore-all":arena.SandboxAction("restore",true);arena.SandboxAction("restore");return DevRan;
                case "arena-reset":arena.SandboxReset();return DevRan;
                case "test-block":arena.SandboxTestBlock();return DevRan;
                case "load-map":return arena.SandboxConfigure(map:devMap)?DevRan:DevRefused;
                case "ward-spawn":if(arena.Map.look!="quarantine"||entries.Length==0)return DevRefused;wardSandboxEntry=Math.Max(0,Math.Min(entries.Length-1,wardSandboxEntry));return arena.SandboxWardEntry(entries[wardSandboxEntry].e)?DevRan:DevRefused;
                // DEBRIS LAUNCHER: 1-150 kg, 1-100 m/s, wood / stone / metal, face / edge / corner.
                case "debris":debrisMass=Mathf.Round(Mathf.Clamp(debrisMass,1,150));debrisSpeed=Mathf.Round(Mathf.Clamp(debrisSpeed,1,100));return arena.SandboxLaunchDebris(debrisMass,debrisSpeed,debrisMaterial,debrisImpact)?DevRan:DevRefused;
                // Destruction effects preview (NativeStructureFxPresentation.cs): presentation only, nothing is added to the match.
                case "fx-collapse":if(kit==null)return DevRefused;PreviewCollapse(kit);return DevRan;
                case "fx-bursts":if(kit==null)return DevRefused;{Vector3 spot=StructureSpot();kit.PreviewBursts(arena,spot,Quaternion.Euler(0,yaw,0)*Vector3.right);}return DevRan;
                case "fx-clear":if(kit==null)return DevRefused;kit.Clear();return DevRan;
                // Preview Draft, Preview Rarities (a draft of the chosen tier), Preview Knockout (the dummy) and Preview My Death.
                case "preview-draft":arena.SandboxPreview("draft");return DevRan;
                case "preview-rarity":arena.SandboxPreview("rarity",previewRarity);return DevRan;
                case "preview-knockout":arena.SandboxPreview("knockout");return DevRan;
                case "preview-death":arena.SandboxPreview("my-death");return DevRan;
                // The galleries: the Siren minion kinds (NativeHordeKindPresentation.cs), the Cyber Lord's second-jump boost
                // (NativeCyberLordPresentation.cs), the saw split (NativeBisectPresentation.cs), the holographic revive
                // (NativeReviveHoloPresentation.cs) and PvE chip selection (NativeChipSelectPresentation.cs). Presentation only.
                case "gallery-minions":presentation.MinionGallery=!presentation.MinionGallery;return DevRan;
                case "gallery-cyber-jump":presentation.CyberBoostPreviewOn=!presentation.CyberBoostPreviewOn;return DevRan;
                case "gallery-saw":presentation.SawSplitGallery=!presentation.SawSplitGallery;return DevRan;
                case "gallery-revive":presentation.ReviveGallery=!presentation.ReviveGallery;return DevRan;
                case "gallery-chip-select":presentation.ChipSelectPreview=!presentation.ChipSelectPreview;return DevRan;
                // The hazard kit gallery (NativeHazardKitPresentation.cs, docs/HAZARD-KIT.md): every kit piece in front of you
                // cycling its states, and its music lock (the boss song plays locally; the alarms keep its beat). Presentation only.
                case "gallery-hazard-kit":presentation.HazardKitGallery=!presentation.HazardKitGallery;return DevRan;
                case "gallery-hazard-music":presentation.HazardKitMusic=!presentation.HazardKitMusic;return DevRan;
                // Kit v2's ranged set on the local Cyber Lord (NativeCyberLordKit2Preview.cs). Presentation only. The ranged and melee
                // previews write different records and each turns the other off.
                case "gallery-cyber-kit2-ranged":presentation.CyberKit2RangedPreviewOn=!presentation.CyberKit2RangedPreviewOn;if(presentation.CyberKit2RangedPreviewOn)presentation.CyberKit2MeleePreviewOn=false;return DevRan;
                // Cyber Lord kit v2's melee and shared abilities on your own Cyber Lord from preview records
                // (NativeCyberKit2MeleePresentation.cs, docs/CYBER-LORD.md). Presentation only.
                case "gallery-cyber-kit2-melee":presentation.CyberKit2MeleePreviewOn=!presentation.CyberKit2MeleePreviewOn;if(presentation.CyberKit2MeleePreviewOn)presentation.CyberKit2RangedPreviewOn=false;return DevRan;
                // The reveal moment on my own pilot, locally (no seat is revealed: devRevealed is the host's, docs/DEV-TOOLS.md).
                case "preview-reveal":DevRevealStart(arena.Local.Seat.id,arena.Local.Seat.name,true);return DevRan;
                // Hazards: the vent fall's splat delay (sandbox only, 1-30 s).
                case "splat-delay":arena.SandboxSplatDelay=Mathf.Round(Mathf.Clamp(devSplat,1,30));return DevRan;
                // Simulation speed (.1-2, NativeSandboxTools) and safe mode (the player keeps 1 HP).
                case "time-scale":SetSandboxTimeScale(devSpeed);Time.fixedDeltaTime=1f/60;return DevRan;
                case "safe-mode":arena.SafeMode=!arena.SafeMode;return DevRan;
            }
            return DevRefused;
        }
    }
}
