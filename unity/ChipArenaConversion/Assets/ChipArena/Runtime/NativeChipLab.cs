using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public NativeLabSample LabSample {get; private set;}
        public void ApplyPracticeBuild(NativeLabBuild build)
        {
            var p=Local;
            p.Seat.spec=build.Weapon;
            p.Seat.weaponTechniques=build.Techniques;
            p.Seat.chips.Clear();
            foreach(var pair in build.Counts())p.Seat.chips.Add(pair.Key,pair.Value);
            p.Weapon=Content.Weapon(build.Weapon);
            p.Stats=NativeContentRules.Stats(Content,build.Weapon,p.Seat.chips);
            p.Hp=(float)p.Stats["hp"];
            SandboxRefresh(p);
        }
        public void ResetLab(NativeLabBuild build)
        {
            if(!Training || Replica || !Sandbox)return;
            ApplyPracticeBuild(build);
            ResetTraining();
            LabSample=new NativeLabSample();
        }
    }

    public sealed partial class ConversionGame
    {
        private NativeLabBuild labBuild;
        private NativeLabSample labBaseline;
        private string labBaselineName,labFilter="",labMessage="";
        private int labPage;
        private NativeLabBuild labListBuild;
        private string labListFilter;
        private ContentChip[] labChoices=Array.Empty<ContentChip>();
        private bool labPanel;
        private const string LabKey="ChipArena.ChipLab.v1";

        private NativeLabBuild SelectedLabBuild()
        {
            var access=account?.Access??NativeAccess.Guest;
            string code=labBuild?.Code??PlayerPrefs.GetString(LabKey,"");
            var saved=NativeLabBuild.Decode(code,content,access);
            if(saved!=null)return saved;
            var weapon=content.weapons.FirstOrDefault(w=>w.id==prefs.weapon&&AllowedWeapon(w));
            return new NativeLabBuild(weapon?.id??"pistol",prefs.weaponTechniques,Array.Empty<string>());
        }

        private void CommitLab(NativeLabBuild build)
        {
            if(build==null){labMessage="Build unavailable. Check the weapon, techniques and chips.";return;}
            labBuild=build;
            PlayerPrefs.SetString(LabKey,build.Code);PlayerPrefs.Save();
            arena.ResetLab(build);labMessage="";
        }

        private void ChipLabPanel()
        {
            GUILayout.BeginArea(new Rect(12,12,Mathf.Min(525,Screen.width/GUI.matrix.m00-24),Mathf.Max(200,Screen.height/GUI.matrix.m11-24)),GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("CHIP LAB");
            if(GUILayout.Button("Resume")){labPanel=false;menu=false;}
            GUILayout.EndHorizontal();
            var sample=arena.LabSample;
            string timer=sample.Started<0?"First hit starts the clock":Mathf.Min(10,arena.Time-sample.Started).ToString("0.0")+" / 10 s";
            GUILayout.Label(timer+"    Total "+sample.Total.ToString("0.0")+"    Last "+sample.Last.ToString("0.0")+"    Hits "+sample.Hits);
            GUILayout.Label("Chip proc events: "+string.Join(", ",sample.Procs.Select(p=>p.Key+" ×"+p.Value)));
            if(labBaseline!=null)GUILayout.Label("Baseline "+labBaselineName+"  "+labBaseline.Total.ToString("0.0")+"   Difference "+(sample.Total-labBaseline.Total).ToString("+0.0;-0.0;0.0"));
            GUILayout.BeginHorizontal();
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&sample.Finished(arena.Time);
            if(GUILayout.Button("Save baseline")){labBaseline=sample;labBaselineName=labBuild.Weapon+" · "+string.Join(", ",labBuild.Chips);}
            GUI.enabled=enabled;
            if(GUILayout.Button("Reset trial"))arena.ResetLab(labBuild);
            if(GUILayout.Button("Copy code"))GUIUtility.systemCopyBuffer=labBuild.Code;
            if(GUILayout.Button("Paste build"))CommitLab(NativeLabBuild.Decode(GUIUtility.systemCopyBuffer,content,account?.Access));
            GUILayout.EndHorizontal();
            var weapons=content.weapons.Where(AllowedWeapon).ToArray();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Previous weapon"))CycleLabWeapon(weapons,-1);
            GUILayout.Label(content.Weapon(labBuild.Weapon).name);
            if(GUILayout.Button("Next weapon"))CycleLabWeapon(weapons,1);
            GUILayout.EndHorizontal();
            GUILayout.Label("Equipped "+labBuild.Chips.Length+" / 5");
            for(int i=0;i<labBuild.Chips.Length;i++)if(GUILayout.Button("Remove "+labBuild.Chips[i])){
                CommitLab(NativeLabBuild.Decode(new NativeLabBuild(labBuild.Weapon,labBuild.Techniques,labBuild.Chips.Where((_,index)=>index!=i).ToArray()).Code,content,account?.Access));break;}
            GUILayout.Label("Find compatible chips");
            labFilter=GUILayout.TextField(labFilter,80);
            if(labMessage.Length>0)GUILayout.Label(labMessage);
            if(labListBuild!=labBuild || labListFilter!=labFilter)
            {
                labListBuild=labBuild;labListFilter=labFilter;labPage=0;
                labChoices=NativeContentRules.Eligible(content,labBuild.Weapon,labBuild.Counts(),labBuild.Techniques)
                    .Where(chip=>(chip.name??chip.id).IndexOf(labFilter,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            }
            if(labBuild.Chips.Length<NativeLabBuild.Slots)
            {
                const int perPage=8;
                int pages=Math.Max(1,(labChoices.Length+perPage-1)/perPage);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Previous"))labPage=(labPage+pages-1)%pages;
                GUILayout.Label((labPage+1)+" / "+pages);
                if(GUILayout.Button("Next"))labPage=(labPage+1)%pages;
                GUILayout.EndHorizontal();
                for(int i=labPage*perPage;i<Math.Min(labChoices.Length,(labPage+1)*perPage);i++)
                {
                    var chip=labChoices[i];
                    if(GUILayout.Button(chip.name+" · "+chip.rarity))
                    {CommitLab(new NativeLabBuild(labBuild.Weapon,labBuild.Techniques,labBuild.Chips.Concat(new[]{chip.id}).ToArray()));break;}
                }
            }
            GUILayout.EndArea();
        }
        private void CycleLabWeapon(ContentWeapon[] weapons,int direction)
        {
            int i=Array.FindIndex(weapons,w=>w.id==labBuild.Weapon);
            var w=weapons[(i+direction+weapons.Length)%weapons.Length];
            CommitLab(new NativeLabBuild(w.id,prefs.weaponTechniques,Array.Empty<string>()));
        }
    }
}
