#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ParityCase
    { public string spec;public uint seed;public ChipCount[] chips;public NumberValue[] stats;public string[] eligible,offers;public RollValues roll; }
    [Serializable] public sealed class HazardCase { public string map;public double time;public NativeHazard[] hazards; }
    [Serializable] public sealed class ParityFixtures { public string sourceCommit;public ParityCase[] cases;public HazardCase[] hazards; }
    public static class NativeParityChecks
    {
        private static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
        public static List<string> Run(ConversionContent data,ParityFixtures fixtures)
        {
            data.Validate();Check(data.sourceCommit==fixtures.sourceCommit,"Source receipts differ");
            // The source's inventory, plus the Unity-only Bow (Astra, PR #141: one weapon, its Q and E).
            Check(data.weapons.Count(w=>w.id!="bow")==15&&data.weapons.Length==16&&data.chips.Length==236&&data.maps.Count(m=>!m.unity)==15&&data.maps.Where(m=>!m.unity).Sum(m=>m.blocks.Length)==3451&&data.abilities.Count(a=>!NativeMeleeRules.Standard(a.spec)&&a.spec!="bow")==14&&data.abilities.Count(a=>a.spec=="bow")==2,"Content inventory differs from pinned source");
            int values=0;
            foreach(var c in fixtures.cases)
            {
                var held=c.chips.ToDictionary(x=>x.id,x=>x.count);var stats=NativeContentRules.Stats(data,c.spec,held);
                foreach(var v in c.stats){Check(Math.Abs(stats[v.name]-v.value)<1e-8*Math.Max(1,Math.Abs(v.value)),"Stat mismatch "+c.spec+" / "+v.name);values++;}
                Check(c.eligible.SequenceEqual(NativeContentRules.Eligible(data,c.spec,held).Select(x=>x.id)),"Eligibility mismatch "+c.spec);
                Check(c.offers.SequenceEqual(NativeContentRules.Offers(data,c.spec,held,new SourceRandom(c.seed).Next)),"Draft mismatch "+c.spec);
                var r=NativeContentRules.Roll(data,c.spec,stats);
                foreach(var f in typeof(RollValues).GetFields())Check(Math.Abs((double)f.GetValue(r)-(double)f.GetValue(c.roll))<1e-8,"Roll mismatch "+c.spec+" / "+f.Name);
            }
            foreach(var c in fixtures.hazards)
            {
                var actual=NativeHazards.At(c.map,c.time);Check(actual.Count==c.hazards.Length,"Hazard count");
                for(int i=0;i<actual.Count;i++){var a=actual[i];var b=c.hazards[i];Check(a.id==b.id&&a.kind==b.kind&&a.active==b.active&&a.warning==b.warning&&Math.Abs(a.x-b.x)<.00001&&Math.Abs(a.extension-b.extension)<.00001,"Hazard mismatch "+c.map+" / "+c.time);}
            }
            var result=new List<string>{"Native content: 15 weapons, 236 Chips, 15 maps, 3451 blocks, 14 ability definitions", "Original-source parity: "+fixtures.cases.Length+" cases / "+values+" stat values, draft eligibility/order and roll profiles", "Original-source parity: "+fixtures.hazards.Length+" hazard schedules"};
            var lava=NativeHazards.At("lavaworks",0);
            foreach(float x in new[]{-5f,5f})Check(lava.Count(h=>NativeHazards.InLavaChannel(h,new V3(x,-1,0)))==1,"Lava contact double-counted");
            foreach(var position in new[]{new V3(0,-1,0),new V3(-5,0,0),new V3(5,-1,18),new V3(5,-5,0)})
                Check(!lava.Any(h=>NativeHazards.InLavaChannel(h,position)),"Dry bridge/outside volume received lava contact");
            result.Add("Native hazards: channel contacts occur once and exclude bridges/outside volume");
            void StepUntil(NativeMatch m,NativePhase phase){for(int i=0;i<2000&&m.Phase!=phase;i++)m.Tick(1d/60);Check(m.Phase==phase,"Phase timeout "+phase);}
            NativeMatch Match(bool teams=false)
            {var m=new NativeMatch(data){RoundsPerSet=2,SetsToWin=2,Teams=teams,SkipBotDrafts=false};m.Seats.Add(new NativeSeat{id=1,name="One",spec="pistol",team=0});m.Seats.Add(new NativeSeat{id=2,name="Two",spec="sword",team=1});Check(m.Start(),"Match did not start");StepUntil(m,NativePhase.Fight);return m;}
            var match=Match();match.Seat(2).alive=false;match.Resolve();Check(match.Seat(1).roundWins==1&&match.Seat(1).score==0,"First round awarded a set");
            StepUntil(match,NativePhase.Fight);match.Seat(2).alive=false;match.Resolve();Check(match.Seat(1).score==1,"Second round did not award set");
            StepUntil(match,NativePhase.Draft);Check(match.DraftSeat==2&&!match.Choose(1,match.Seat(2).offers[0]),"Winner stole losing player's pick");
            Check(!match.Choose(2,match.Seat(2).offers[0]),"Premature draft choice");while(match.Time<match.ReadyAt)match.Tick(1d/60);
            string chip=match.Seat(2).offers[0];Check(match.Choose(2,chip)&&!match.Choose(2,chip)&&match.Seat(2).chips[chip]==1,"Duplicate draft award");
            StepUntil(match,NativePhase.Countdown);Check(match.Set==2&&match.Round==1&&match.Seat(1).roundWins==0,"Set did not reset round wins");
            result.Add("Native match: two rounds per set, loser draft, readiness, single award and next-set reset");
            match=Match();match.Seat(1).alive=match.Seat(2).alive=false;match.Resolve();StepUntil(match,NativePhase.Countdown);Check(match.Round==1&&match.Seat(1).score==0&&match.Seat(2).score==0,"Draw changed score");
            result.Add("Native match: simultaneous knockout replays without awarding progress");
            match=Match(true);match.Seats.Add(new NativeSeat{id=3,name="Three",team=0,spec="rifle",alive=false});match.Seat(2).alive=false;match.Resolve();Check(match.Seat(3).roundWins==1,"Team result omitted dead teammate");
            result.Add("Native match: team round credit includes eliminated teammates");
            match=Match();match.RoundsPerSet=1;match.Seat(2).bot=true;match.SkipBotDrafts=true;
            match.Seat(2).alive=false;match.Resolve();StepUntil(match,NativePhase.Countdown);
            Check(match.Set==2&&match.Seat(2).chips.Values.Sum()==1,"Skipped bot draft did not award exactly one Chip");
            StepUntil(match,NativePhase.Fight);match.Seat(2).alive=false;match.Resolve();StepUntil(match,NativePhase.Match);
            Check(match.Seat(1).score==2&&match.Seat(2).chips.Values.Sum()==1,"Match finish awarded extra draft or wrong score");
            for(int i=0;i<300;i++)match.Tick(1d/60);
            Check(match.Phase==NativePhase.Match&&match.Seat(1).score==2,"Finished match advanced twice");
            Check(match.Start()&&match.Phase==NativePhase.Countdown&&match.Set==1&&match.Seats.All(p=>p.score==0&&p.roundWins==0&&p.alive&&p.chips.Count==0),"Rematch leaked previous progress");
            result.Add("Native match: skipped bot draft still grants a Chip, final win is stable, rematch clears progress");
            return result;
        }
    }
}
#endif
