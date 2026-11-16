using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class DeathmatchChecks
{
    public static void Run(string root)
    {
        int checks=0;
        void Check(bool passed,string message){if(!passed)throw new Exception(message);checks++;}
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
        var a=new NativeSeat{id=1,team=0,spec="pistol",name="A"};
        var b=new NativeSeat{id=2,team=1,spec="sword",name="B"};
        var c=new NativeSeat{id=3,team=0,spec="pistol",name="C",bot=true};
        var draft=new NativeOpeningDraft(new[]{a,b,c},true,true,0,p=>new[]{"vital"},(s,id)=>id=="pistol"||id=="sword");
        Check(draft.Pilots[c.id].Step==OpeningStep.Ready,"bots finish both picks");
        Check(!draft.Submit(1,OpeningStep.Weapon,"invalid",true,1),"invalid weapon");
        Check(draft.Submit(1,OpeningStep.Weapon,"sword",false,1),"highlight stored");
        Check(a.spec=="pistol","highlight stays outside public seat");
        Check(draft.Submit(2,OpeningStep.Weapon,"sword",true,2),"lock second pilot");
        Check(!draft.Submit(2,OpeningStep.Weapon,"pistol",true,3),"locked weapon is final");
        Check(!draft.Submit(2,OpeningStep.Chip,"unoffered",true,3),"unoffered chip rejected");
        Check(draft.Submit(2,OpeningStep.Chip,"vital",true,3)&&!draft.Ready,"wait for everyone");
        draft.Tick(20);
        Check(draft.Pilots[1].Weapon=="sword"&&draft.Pilots[1].Step==OpeningStep.Chip,"timeout uses highlight; duplicates permitted");
        draft.Tick(40);
        Check(draft.Ready&&draft.Pilots[1].Chip=="vital"&&a.chips.Count==0,"chip timeout with private picks");
        var options=new NativeRoomOptions();
        Check(options.EffectiveTarget==15,"FFA target");options.teams=true;
        Check(options.EffectiveTarget==30,"team target");options.killTarget=120;options.Normalize(content);
        Check(options.EffectiveTarget==100,"custom target bound");

        foreach(bool teams in new[]{false,true})
        {
            var m=new NativeMatch(content,42){Teams=teams,DeathmatchTarget=10,ComebackChips=true,StartingChip=p=>p.chips["vital"]=1};
            m.Seats.AddRange(new[]{a,b,c});
            Check(m.Start(),"start");while(m.Phase==NativePhase.Countdown)m.Tick(.25);
            void Kill(NativeSeat source,int seed){b.alive=false;m.RecordDeathmatchDeath(b,source,seed);}
            void Respawn(){
                Check(!m.TryDeathmatchRespawn(b),"no early respawn");
                for(int i=0;i<12;i++)m.Tick(.25);
                Check(m.TryDeathmatchRespawn(b),"scheduled respawn");b.alive=true;
            }
            Kill(null,1);Check(b.enemyDeaths==0&&a.score==0,"environment gives no credit");Respawn();
            Kill(b,2);Check(b.enemyDeaths==0&&b.score==0,"self gives no credit");Respawn();
            if(teams){a.alive=false;m.RecordDeathmatchDeath(a,c,3);Check(a.enemyDeaths==0&&c.score==0,"friendly fire");a.alive=true;a.respawnAt=-1;}
            for(int i=1;i<=9;i++)
            {
                Kill(teams&&i%2==0?c:a,10+i);m.RecordDeathmatchDeath(b,a,99);
                Check(a.score==i&&c.score==(teams?i:0),"exactly one point per death, side total");
                Check(b.enemyDeaths==i,"personal deaths");
                Check(b.owedPicks==Math.Min(2,i/3),"rewards only on third and sixth opponent death");
                Respawn();
            }
            int ticket=b.pickTicket;string first=b.offers.First();
            Check(m.TakeComebackChip(b,ticket,first),"choose later after respawn");
            Check(!m.TakeComebackChip(b,ticket,b.offers.First()),"old command cannot consume second reward");
            Check(m.TakeComebackChip(b,b.pickTicket,b.offers.First())&&b.chips.Values.Sum()==3,"three total chips retained");
            Kill(a,123);m.RecordDeathmatchDeath(a,b,124);
            Check(m.Phase==NativePhase.Result&&m.FinalVictim==b.id&&m.FinalSeed==123&&m.Winner==a.id&&b.score==0,"winning kill fixes result and replay");
            while(m.Phase==NativePhase.Result)m.Tick(.25);
            Check(m.Phase==NativePhase.Match&&!m.TryDeathmatchRespawn(b),"no subsequent respawns or round draft");
        }
        Console.WriteLine("PASS blind pick and deathmatch: "+checks+" checks");
    }
}
