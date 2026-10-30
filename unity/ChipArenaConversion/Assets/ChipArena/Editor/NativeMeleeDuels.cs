using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    // Separate measurement pass, not a flaky win-rate build gate. Uses the shipping bots, maps and PhysX handlers.
    public static class NativeMeleeDuels
    {
        [Serializable] public sealed class Duel
        {public string map,melee,opponent;public int seed,side,winner;public float seconds,meleeHp,opponentHp;public bool timeout;}
        [Serializable] public sealed class Rate
        {public string map,melee;public int games,wins,draws,timeouts;public float winRate;public bool inTarget;}
        [Serializable] public sealed class Report
        {public string engine="Unity NativeArena + PhysX",utc,source,contentRevision,difficulty="normal";public int seedPairs;public Duel[] duels;public Rate[] rates;}
        public static void Run()
        {
            var asset=Resources.Load<TextAsset>("GameContent");if(asset==null)throw new InvalidOperationException("GameContent missing");
            var content=JsonUtility.FromJson<ConversionContent>(asset.text);NativeUnityMapLoader.Load(content);content.Validate();
            int pairs=int.TryParse(Environment.GetEnvironmentVariable("CHIP_DUEL_SEEDS"),out int count)?count:4;
            if(pairs<1||pairs>64)throw new ArgumentOutOfRangeException("CHIP_DUEL_SEEDS","Use 1–64 paired seeds");
            var maps=(Environment.GetEnvironmentVariable("CHIP_DUEL_MAPS")??"brickfall,factory,cybercity").Split(',').Select(s=>s.Trim()).ToArray();
            foreach(string map in maps)if(!content.maps.Any(m=>m.id==map)||content.Map(map).blocks.Length<10)throw new ArgumentException("Unknown/non-cover duel map: "+map);
            var difficulty=Environment.GetEnvironmentVariable("CHIP_DUEL_DIFFICULTY")??"normal";
            if(difficulty!="easy"&&difficulty!="normal"&&difficulty!="hard")throw new ArgumentException("Invalid duel difficulty");
            string output=Path.GetFullPath(Environment.GetEnvironmentVariable("CHIP_DUEL_OUTPUT")??"Temp/MeleeDuels.json");
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),source=Environment.GetEnvironmentVariable("CHIP_DUEL_COMMIT")??"unrecorded",difficulty=difficulty,seedPairs=pairs,contentRevision=content.contentRevision};
            var rows=new List<Duel>();var previousMode=Physics.simulationMode;var previousGravity=Physics.gravity;var previousRandom=UnityEngine.Random.state;
            var scene=EditorSceneManager.NewPreviewScene();var physics=scene.GetPhysicsScene();
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                foreach(string map in maps)foreach(string melee in new[]{"sword","daggers","shield","greatsword"})
                foreach(string opponent in new[]{"pistol","machinegun","rifle","fire","water","lightning","darkness"})
                for(int seed=1;seed<=pairs;seed++)for(int side=0;side<2;side++)
                {
                    UnityEngine.Random.InitState(seed);var match=new NativeMatch(content,(uint)seed);
                    match.Seats.Add(new NativeSeat{id=1,name="Duel 1",spec=side==0?melee:opponent,bot=true,difficulty=difficulty});
                    match.Seats.Add(new NativeSeat{id=2,name="Duel 2",spec=side==0?opponent:melee,bot=true,difficulty=difficulty});
                    using(var arena=new NativeArena(content,match,map,seed){ReplicaScene=scene,LocalId=0})
                    {
                        if(!match.Start())throw new InvalidOperationException("Duel did not start");
                        // Run the countdown, gravity, destruction callbacks and actual ability contacts exactly as the game.
                        while(match.Time<183&&match.Phase!=NativePhase.Result&&match.Phase!=NativePhase.Match)
                        {arena.Step(new InputFrame());arena.BeforePhysics();physics.Simulate(Rules.Dt);arena.AfterPhysics();}
                        var a=arena.Pilots[side];var b=arena.Pilots[1-side];bool timeout=match.Phase==NativePhase.Fight;
                        rows.Add(new Duel{map=map,melee=melee,opponent=opponent,seed=seed,side=side,winner=match.Winner==a.Seat.id?1:match.Winner==b.Seat.id?2:0,seconds=arena.Time-(float)match.FightAt,meleeHp=a.Hp,opponentHp=b.Hp,timeout=timeout});
                    }
                    // Incremental receipt survives an interrupted long measurement pass.
                    report.duels=rows.ToArray();report.rates=Rates(rows);Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
                    Debug.Log($"MELEE_DUEL {map} {melee}/{opponent} seed={seed} side={side} winner={rows[rows.Count-1].winner}");
                }
                foreach(var rate in report.rates)Debug.Log($"MELEE_RATE {rate.map} {rate.melee}: {rate.wins}/{rate.games} ({rate.winRate:P1}), draws={rate.draws}, timeouts={rate.timeouts}, target25–55={rate.inTarget}");
                Debug.Log("MELEE_DUELS_COMPLETE "+output);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);Physics.simulationMode=previousMode;Physics.gravity=previousGravity;UnityEngine.Random.state=previousRandom;}
        }
        private static Rate[] Rates(List<Duel> rows)=>rows.GroupBy(d=>d.map+":"+d.melee).Select(g=>
        {int wins=g.Count(d=>d.winner==1);float rate=(float)wins/g.Count();return new Rate{map=g.First().map,melee=g.First().melee,games=g.Count(),wins=wins,draws=g.Count(d=>d.winner==0),timeouts=g.Count(d=>d.timeout),winRate=rate,inTarget=rate>=.25f&&rate<=.55f};}).ToArray();
    }
}
