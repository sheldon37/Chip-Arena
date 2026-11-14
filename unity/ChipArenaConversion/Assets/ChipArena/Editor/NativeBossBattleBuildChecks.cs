using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckBossBattle(ConversionContent content)
        {
            string json=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;
            foreach(int party in new[]{1,4})
            {
                var match=new NativeMatch(content){Cooperative=true,Teams=true,ShowIntros=false};
                for(int i=1;i<=party;i++)match.Seats.Add(new NativeSeat{id=i,name="Pilot "+i,spec="pistol",team=0});
                var boss=new NativeSeat{id=900,name="Boss anchor",spec="siren",team=1,bot=true};
                typeof(NativeSeat).GetField("AuthorityBoss",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(boss,true);match.Seats.Add(boss);
                using(var arena=new NativeArena(content,match,"siren"){LocalId=1})
                {
                    if(!match.Start())throw new Exception("Siren start refused");
                    arena.ConfigurePve(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text),true);
                    match.SynchronizeExpeditionClock(match.PhaseEnd);match.Tick(0);arena.Step(new InputFrame());
                    arena.ValidateBossOpening();
                }
            }
            foreach(string id in new[]{"station","converter","hum","deepend","noctiel"})
            {
                var bundle=ExpeditionRunBundle.BossBattle(id,content,json);
                foreach(int party in new[]{1,4})foreach(bool wipe in new[]{false,true})
                {
                    var match=new NativeMatch(bundle.Content){Cooperative=true,Teams=true,ShowIntros=false};
                    for(int i=1;i<=party;i++)match.Seats.Add(new NativeSeat{id=i,name="Pilot "+i,spec="pistol",team=0});
                    var boss=new NativeSeat{id=900,name="Boss anchor",spec="siren",team=1,bot=true};
                    typeof(NativeSeat).GetField("AuthorityBoss",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(boss,true);match.Seats.Add(boss);
                    using(var arena=new NativeArena(bundle.Content,match,NativeBossBattle.Map(id)){LocalId=1})
                    {
                        if(!match.Start())throw new Exception("Direct boss start refused");
                        arena.ConfigureBossBattle(id,bundle,JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));
                        arena.ValidateBossOpening();
                        if(id=="noctiel"){arena.ValidateFallenBattle(frame=>{using(var guest=new NativeArena(bundle.Content,new NativeMatch(bundle.Content),frame.map){Replica=true,LocalId=party,GeneratedRun=bundle}){guest.ApplyFrame(frame,party);if(guest.ZoneBoss?.fallen?.hush.Length!=party)throw new Exception("NOCTIEL guest lives missing");}},wipe);continue;}
                        if(wipe){arena.ValidateBossBattleWipe();continue;}
                        arena.ValidateBossBattle(frame=>
                        {
                            using(var guest=new NativeArena(bundle.Content,new NativeMatch(bundle.Content),frame.map){Replica=true,LocalId=party,GeneratedRun=bundle})
                            {guest.ApplyFrame(frame,party);if(guest.ZoneBoss?.archetype!=frame.pve.zoneBoss.archetype||guest.Pve?.Holding!=true)throw new Exception("Direct boss guest arrival differs");}
                        });
                    }
                }
            }
        }
    }
}
