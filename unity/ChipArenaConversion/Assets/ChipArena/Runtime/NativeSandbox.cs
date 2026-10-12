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
            if(!SandboxAllowed)return;SandboxNextChip();foreach(var p in Pilots)if(p.Seat.bot&&p.Hp<=0&&p.Death!=null&&DummyRespawn>0&&!InShaft(p)&&Time-p.Death.at>=DummyRespawn)SandboxRestore(p);
            if(SandboxRepeat>0&&Time>=sandboxRepeatAt&&sandboxDummy!=null){SandboxCast(sandboxDummy.Seat.spec,sandboxKey);sandboxRepeatAt=Time+SandboxRepeat;}
        }
        private void SandboxRestore(NativePilot p)
        {
            if(!TrySandboxSpawn(p,out var spawn))return;CancelSiren(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);CancelAbilities(p);ReleaseControl(p);p.Down=null;p.Juggle=null;p.Dots.Clear();p.Death=null;p.Wounds.Clear();p.FrozenUntil=p.ScareUntil=0;p.ScareBy=null;p.RevivedAt=Time;p.AirJumps=0;p.AirJumpAt=-100;p.Seat.alive=true;p.Body.isKinematic=false;p.Body.position=spawn;p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Collider.enabled=true;p.Hp=(float)p.Stats["hp"];SandboxRefresh(p);
        }
        private void SandboxRefresh(NativePilot p)
        {CancelAbilities(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);p.Ready=p.QReady=p.EReady=p.RollReady=p.BlockReady=p.ReloadEnd=p.StunEnd=p.RollEnd=p.AttackEnd=0;p.Stamina=(float)p.Stats["maxStamina"];p.Mana=(float)p.Stats["maxMana"];p.Ammo=Magazine(p);p.ECharges=NativeAbilityRules.Charges(p.Stats);if(p.Siren!=null)p.Siren.Ready.Clear();foreach(string key in chipCooldowns.Keys.Where(k=>k.StartsWith(p.Seat.id+":")).ToArray())chipCooldowns.Remove(key);}
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
        private bool sandboxWanted;private string sandboxSearch="",dummySpec="pistol",dummyKey="ai",sandboxTarget="dummy",sandboxSort="rarity";private float sandboxSpeed=1;
        // main.ts drawSandbox's arena row, previews and DEBRIS LAUNCHER (8 kg at 15 m/s by default, up to 150 kg, wood /
        // stone / metal); the impact choice is NativeSandboxTools' face / edge / corner orientation.
        private string previewRarity="common",debrisMaterial="wood",debrisImpact="face";private float debrisMass=8,debrisSpeed=15;private string[] previewRarities,previewRarityNames;
        // Sandbox tools (pause menu of a developer sandbox match, started from the home screen's Developer Sandbox link).
        private void SandboxBody()
        {
            if(arena?.Sandbox!=true||!account.Developer||Online)return;
            Field("Simulation");sandboxSpeed=Slider("Simulation speed",sandboxSpeed,.1f,2);Time.timeScale=sandboxSpeed;Time.fixedDeltaTime=1f/60;
            arena.SafeMode=GUILayout.Toggle(arena.SafeMode,"Safe mode — player retains 1 HP");arena.DummyRespawn=Mathf.Round(Slider("Dummy respawn seconds (0 disables)",arena.DummyRespawn,0,30));arena.SandboxRepeat=Mathf.Round(Slider("Repeat cast seconds (0 disables)",arena.SandboxRepeat,0,60));if(arena.SandboxRepeat>0)arena.SandboxRepeat=Math.Max(2,arena.SandboxRepeat);
            Field("Dummy weapon");dummySpec=ChoiceGrid(content.weapons.Where(AllowedWeapon).Select(w=>w.id).ToArray(),dummySpec);Field("Dummy input");dummyKey=ChoiceGrid(new[]{"ai","primary","e","q","block","dash","jump","1","2","3","4","mode"},dummyKey);if(GUILayout.Button("Make dummy cast",button)){arena.SandboxCast(dummySpec,dummyKey);menu=false;}
            Field("Target");sandboxTarget=ChoiceGrid(new[]{"self","dummy"},sandboxTarget);GUILayout.BeginHorizontal();foreach(string action in new[]{"restore","hit","down","summons","vent"})if(GUILayout.Button(action,button)){arena.SandboxAction(action,sandboxTarget=="self");menu=false;}GUILayout.EndHorizontal();
            // Restore (every pilot, as the source's heal), Arena Reset and Test Block.
            Field("Arena");GUILayout.BeginHorizontal();
            if(GUILayout.Button("Restore",button)){arena.SandboxAction("restore",true);arena.SandboxAction("restore");menu=false;}
            if(GUILayout.Button("Arena Reset",button)){arena.SandboxReset();menu=false;}
            if(GUILayout.Button("Test Block",button)){arena.SandboxTestBlock();menu=false;}
            GUILayout.EndHorizontal();
            // Preview Draft, Preview Rarities (a draft of the chosen tier), Preview Knockout (the dummy) and Preview My Death.
            Field("Previews");if(previewRarities==null){var tiers=content.rarities.OrderBy(r=>NativeChipRanks.RarityRank(r.id)).ToArray();previewRarities=tiers.Select(r=>r.id).ToArray();previewRarityNames=tiers.Select(r=>r.name).ToArray();}
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Preview Draft",button)){arena.SandboxPreview("draft");menu=false;}
            if(GUILayout.Button("Preview Rarities",button)){arena.SandboxPreview("rarity",previewRarity);menu=false;}
            if(GUILayout.Button("Preview Knockout",button)){arena.SandboxPreview("knockout");menu=false;}
            if(GUILayout.Button("Preview My Death",button)){arena.SandboxPreview("my-death");menu=false;}
            // The Siren minion gallery (NativeHordeKindPresentation.cs): every kind looping its set before you, presentation only.
            if(GUILayout.Button(presentation.MinionGallery?"Hide Siren Minions":"Preview Siren Minions",button)){presentation.MinionGallery=!presentation.MinionGallery;menu=false;}
            GUILayout.EndHorizontal();previewRarity=ChoiceGrid(previewRarities,previewRarity,previewRarityNames);
            Field("Debris launcher");debrisMass=Mathf.Round(Slider("Mass (kg)",debrisMass,1,150));debrisSpeed=Mathf.Round(Slider("Speed (m/s)",debrisSpeed,1,100));
            debrisMaterial=ChoiceGrid(new[]{"wood","stone","metal"},debrisMaterial);debrisImpact=ChoiceGrid(new[]{"face","edge","corner"},debrisImpact);
            if(GUILayout.Button("Launch at Opponent",button)&&arena.SandboxLaunchDebris(debrisMass,debrisSpeed,debrisMaterial,debrisImpact))menu=false;
            StructureFxRows();
            Field("Chips");sandboxSort=ChoiceGrid(new[]{"name","rarity","installed"},sandboxSort);sandboxSearch=GUILayout.TextField(sandboxSearch,100);var target=sandboxTarget=="self"?arena.Local:arena.Pilots.FirstOrDefault(p=>p.Seat.bot);if(target==null)return;
            foreach(var chip in content.chips.OrderBy(c=>sandboxSort=="rarity"?NativeChipRanks.RarityRank(c.rarity).ToString("D2"):sandboxSort=="installed"?(target.Seat.chips.ContainsKey(c.id)?"0":"1"):c.name).ThenBy(c=>c.name).Where(c=>sandboxSearch.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).All(term=>(c.name+" "+c.id+" "+c.description).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)))
            {target.Seat.chips.TryGetValue(chip.id,out int count);GUILayout.BeginHorizontal();GUILayout.Label(chip.name+" ×"+count,small);if(GUILayout.Button("+",GUILayout.Width(32)))arena.SandboxChip(target.Seat.id,chip.id,1);if(GUILayout.Button("−",GUILayout.Width(32)))arena.SandboxChip(target.Seat.id,chip.id,-1);GUILayout.EndHorizontal();}
        }
    }
}
