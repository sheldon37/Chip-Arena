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
        Check(Math.Abs(NativeMatch.DeathmatchReplayStart(90,100,2.4)-98.35f)<.001f,"normal respawn leaves time to see the killing hit");
        Check(NativeMatch.DeathmatchReplayStart(90,100,.4)==100,"late replay starts at the hit");
        Check(NativeMatch.DeathmatchReplayStart(99.9f,100,2.4)==99.9f,"short history starts at the oldest frame");
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
        // Replay captures at 20 Hz. A chip or a temporary stat change must not edit earlier frames.
        var layout=content.baseStats;
        var stats=new ChipStats(layout);
        var original=stats.FrameStats(layout);
        Check(original.Select(row=>row.name).SequenceEqual(layout.Select(row=>row.name))&&
            original.All(row=>row.value==stats[row.name]),"captured stat order and values");
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<600;i++)stats.FrameStats(layout);
        Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"stable replay stats allocate nothing");
        string statName=original[0].name;
        double baseValue=original[0].value;
        stats[statName]=baseValue+2;
        var buffed=stats.FrameStats(layout);
        Check(original[0].value==baseValue&&buffed[0].value==baseValue+2&&ReferenceEquals(original[1],buffed[1]),"buff preserves past frames and shares unchanged rows");
        stats[statName]=baseValue;
        Check(stats.FrameStats(layout)[0].value==baseValue&&buffed[0].value==baseValue+2,"buff expiry preserves its replay frame");
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
        var missing=new NativeSeat{id=4,spec="pistol",team=1};
        var interrupted=new NativeOpeningDraft(new[]{a,b,missing},true,true,0,p=>new[]{"vital"},(s,id)=>true);
        foreach(var seat in new[]{a,b})
        {
            interrupted.Submit(seat.id,OpeningStep.Weapon,"sword",true,1);
            interrupted.Submit(seat.id,OpeningStep.Chip,"vital",true,2);
        }
        Check(!interrupted.Ready&&interrupted.Disconnect(missing.id),"disconnect removes outstanding pick");
        Check(interrupted.Ready&&interrupted.Pilots.Count==2&&!interrupted.Pilots.ContainsKey(missing.id),"reveal immediately includes only the remaining pilots");
        Check(!interrupted.Submit(missing.id,OpeningStep.Weapon,"sword",true,2),"late pick cannot restore a removed seat");
        Check(!interrupted.Disconnect(missing.id),"repeated disconnect is harmless");
        Check(NativeOpeningDraft.ProtocolRefusal(1,false)==null&&NativeOpeningDraft.ProtocolRefusal(1,true)!=null&&
            NativeOpeningDraft.ProtocolRefusal(0,false)!=null&&NativeOpeningDraft.ProtocolRefusal(2,true)==null,"mode-specific version gate");
        var legacy=new NativeOpeningDraft(new[]{a,b},true,false,0,p=>Array.Empty<string>(),(seat,id)=>true);
        Check(legacy.CardsFor(1,false,id=>id).Length==0,"original weapon step still sends an empty cards array");
        var ids=new[]{"pistol","sword","bow","bow","dev"};
        bool granted=true;
        bool Allow(NativeSeat seat,string id)=>id!="dev"||seat.id==1&&granted;
        var rng=new SourceRandom(123);
        var dealt=new NativeOpeningDraft(new[]{a,b,c},true,true,5,p=>new[]{"vital"},Allow,99,ids,rng.Next);
        Check(dealt.Pilots.Values.All(p=>p.WeaponOffers.Length==3 && p.WeaponOffers.Distinct().Count()==3),"uniform room deal capped at smallest personal pool");
        var hand=dealt.Pilots[2].WeaponOffers;
        Check(!hand.Contains("dev")&&!dealt.Pilots[c.id].WeaponOffers.Contains("dev"),"grant and bot eligibility");
        Check(dealt.Pilots[c.id].Step==OpeningStep.Ready&&dealt.Pilots[c.id].WeaponOffers.Contains(dealt.Pilots[c.id].Weapon),"bot finishes with offered weapon");
        var limited=new NativeOpeningDraft(new[]{a,b},true,false,0,p=>Array.Empty<string>(),Allow,2,ids,()=>.99);
        var mine=limited.Pilots[1];
        Check(limited.CardsFor(1,true,id=>id).SequenceEqual(mine.WeaponOffers)&&
            limited.CardsFor(2,false,id=>id).SequenceEqual(limited.Pilots[2].WeaponOffers)&&
            !limited.CardsFor(2,false,id=>id).Contains("dev"),"each seat gets only its own cards before reveal");
        Check(draft.CardsFor(1,false,id=>id).Length==0,"ready view does not leak a hand");
        string outside=ids.First(id=>!mine.WeaponOffers.Contains(id));
        Check(!limited.Submit(1,OpeningStep.Weapon,outside,false,1)&&!limited.Submit(1,OpeningStep.Weapon,outside,true,1),"reject unoffered highlight and lock");
        Check(limited.Submit(1,OpeningStep.Weapon,mine.WeaponOffers[0],false,19.9),"offered highlight allowed");
        limited.Tick(20);
        Check(limited.Ready&&mine.Weapon==mine.WeaponOffers[1],"deadline makes a fresh random choice ignoring highlight");
        Check(limited.CardsFor(2,true,id=>id).SequenceEqual(new[]{"A   ·   "+mine.Weapon,"B   ·   "+limited.Pilots[2].Weapon}),"public reveal contains locked choices only");
        Check(!limited.Submit(2,OpeningStep.Weapon,"pistol",true,20),"expired input refused");
        Check(dealt.Disconnect(2)&&!dealt.Submit(2,OpeningStep.Weapon,hand[0],true,6),"disconnected hand invalidated");
        var expiring=new NativeOpeningDraft(new[]{a},true,false,0,p=>Array.Empty<string>(),Allow,3,ids,()=>.99);
        Check(expiring.Submit(1,OpeningStep.Weapon,"dev",true,1),"entitled offer accepted");
        granted=false;expiring.Tick(2);
        Check(expiring.Refusal!=null,"expiry before launch invalidates the private pick");
        Check(new NativeOpeningDraft(new[]{a},true,false,0,p=>Array.Empty<string>(),Allow,3,new[]{"pistol","dev"},rng.Next).Refusal!=null,"undersized eligible pool refuses");
        Check(NativeOpeningDraft.OfferCount(1,10)==2&&NativeOpeningDraft.OfferCount(0,10)==0&&NativeOpeningDraft.DefaultBlindChoices==3,"legacy sentinel, minimum, and dealt default");
        var grantBot=new NativeSeat{id=9,spec="dev",bot=true};
        bool temp=true;
        var expiredAtDeal=new NativeOpeningDraft(new[]{grantBot},true,false,0,p=>Array.Empty<string>(),
            (seat,id)=>id=="bow" || id=="dev"&&temp,2,new[]{"dev","bow"},()=>{temp=false;return 0;});
        Check(expiredAtDeal.Refusal!=null && expiredAtDeal.Pilots[9].Weapon!="pistol","an expiring bot hand cannot substitute an unoffered fallback");
        string Refusal(bool teams,string map,params NativeSeat[] seats)=>NativeOpeningDraft.RoomRefusal(seats,false,teams,map);
        Check(Refusal(false,"breakwater",a)!=null,"single pilot refused before pick");
        Check(Refusal(true,"breakwater",a,c)!=null,"single team refused");
        Check(Refusal(true,"breakwater",a,b,c,missing)==null,"two versus two allowed");
        Check(Refusal(false,"breakwater",a,b,c,missing,new NativeSeat{id=5})!=null,"over-capacity map refused");
        Check(Refusal(true,"breakwater",a,b,c,new NativeSeat{id=5,team=0})!=null,"three versus one refused");
        Check(Refusal(false,"breakwater",a,b,c,missing)==null,"four-pilot FFA allowed");
        Check(NativeOpeningDraft.RoomRefusal(new[]{a},true,true,"siren")==null,"solo co-op allowed");
        Check(NativeOpeningDraft.RoomRefusal(new[]{a,b,c,missing,new NativeSeat{id=5}},true,true,"siren")!=null,"co-op capacity enforced");
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
