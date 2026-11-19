#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch=System.Diagnostics.Stopwatch;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        internal int DevLoadLobbyCount {get;private set;}
        internal double DevCrowdFlowMs {get;private set;}
        internal double DevPerceptionMs {get;private set;}
        private static double LoadElapsed(long start)=>(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;

        internal void PrimeDevLoad(int lobby, bool layered = false)
        {
            DevLoadLobbyCount=lobby;
            Pve.Cap=ExpeditionHubRules.Cap(lobby,1);
            var centre=new Vector3(0,0,-31);
            var clearance=NativeLocomotionRules.HalfHeight;
            // Choose distinct, supported street nodes. Avoid the entry volumes so the regular
            // spawn path can allocate and initialize every body before warm-up begins.
            var feet = Map.nav.nodes.Select(n => Vec(n.pos) - Vector3.up * clearance);
            if (layered)
            {
                // The walk's sampled centre lines have a 15 m wide road under them.
                // Give the same near-gate cohort a supported 2 m lattice across that road.
                feet = feet.Where(p => p.z < -25 && Mathf.Abs(p.x) < .01f)
                    .SelectMany(p => new[] { -6, -4, -2, 0, 2, 4, 6 }.Select(x => p + Vector3.right * x));
            }
            var points = feet.Where(p => !HubInside(p) && (p-centre).sqrMagnitude < 42*42 && (p-centre).sqrMagnitude > 10*10)
                .OrderBy(p => (p-centre).sqrMagnitude).ThenBy(p => p.x).ThenBy(p => p.z);
            int count=0;
            foreach(var point in points)
            {
                if(!ExpeditionSupported(Vec(point),.45f,1.8f)||
                    Pve.Content.entries.Any(e=>(Vec(e.@exit.Position())-point).sqrMagnitude<25))continue;
                string kind=count%11==10&&count<44?"spitter":count%3==0?"runner":"shambler";
                var mob=SpawnPveAddAt(kind,point);
                if(mob==null)throw new InvalidOperationException("Load fixture could not initialize "+kind+" at population "+count);
                mob.Pve.BossSpawn=false;
                var zone=hubAuthority.Layout.Zones.OrderBy(z=>(new Vector3(z.Volume.x,0,z.Volume.z)-point).sqrMagnitude).First();
                RegisterHubMob(mob,zone,Vec(Pve.Content.entries.First(e=>e.e==zone.Spawns[0]).@exit.Position(.85f)));
                if(++count==Pve.Cap)break;
            }
            if(count!=Pve.Cap)throw new InvalidOperationException("Load fixture lacks street positions: "+count+"/"+Pve.Cap);
            Physics.SyncTransforms();
        }
    }

    // A private simulation with synthetic session members, never attached to a room or account.
    public sealed class NativeExpeditionLoadFixture : IDisposable
    {
        public const int Warmup=120,Samples=600;
        public readonly NativeExpeditionAreaRuntime Runtime;
        public readonly ExpeditionRunBundle Bundle;
        public readonly int Pilots;
        public double CandidateGenerationMs { get; private set; }
        public int Tick {get;private set;}
        private readonly ExpeditionSession session;
        private readonly NativePilot[] members;
        private readonly Vector3[] firstPositions;
        private readonly float[] maxTravel;
        private readonly double[] sum=new double[4];
        private int measured;
        private readonly bool pilotHorde,hordeHorde;
        private readonly bool[] wardLayers=new bool[32];
        public NativeArena Arena=>Runtime.Arena;

        public NativeExpeditionLoadFixture(int pilots,Func<string,Scene> create=null,Action<Scene> release=null, NativeCityLayout.Plan candidate=null)
        {
            if(pilots<1||pilots>4)throw new ArgumentOutOfRangeException(nameof(pilots));
            Pilots=pilots;
            var content=JsonUtility.FromJson<ConversionContent>(Resources.Load<TextAsset>("GameContent").text);
            NativeUnityMapLoader.Load(content);content.Validate();
            string json=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;
            var authored=NativeExpeditionLayout.Parse(json);
            authored.compatibility=NativeExpeditionLayout.Compatibility(json,content);
            var room=new ExpeditionDeploymentRoom("load-fixture","p1",17,"black-siren",authored,content);
            if(!room.Execute("p1",0,ExpeditionDeploymentIntent.RollInstances))throw new InvalidOperationException("Load fixture roll failed");
            Bundle=ExpeditionRunBundle.Build(room.Ticket,content,json);
            var simulationContent = Bundle.Content;
            NativeLayeredCityWalk walk = null;
            if (candidate != null)
            {
                var generation = Stopwatch.StartNew();
                walk = new NativeLayeredCityWalk(candidate);
                PrepareSurveyWorld(Bundle.World, walk);
                simulationContent = simulationContent.WithGeneratedMap(walk.Map);
                CandidateGenerationMs = generation.Elapsed.TotalMilliseconds;
            }
            var layout=Bundle.Layout;
            var run=NativeExpedition.Create("load-fixture",layout.world,"p1","pistol",17,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=12}).ToArray());
            for(int id=2;id<=pilots;id++)if(!run.Join("p"+id,"pistol"))throw new InvalidOperationException("Load fixture seat refused");
            Runtime=new NativeExpeditionAreaRuntime(layout.world,simulationContent,Bundle.World.Map.id,match=>
            {
                match.Cooperative=match.Teams=true;
                match.ShowIntros=match.FinalKillcams=false;
                for(int id=1;id<=pilots;id++)match.Seats.Add(new NativeSeat{id=id,name="Load pilot "+id,spec="pistol"});
                match.Seats.Add(new NativeSeat{id=900,name="Hidden boss",spec="siren",bot=true,team=1,AuthorityBoss=true});
            },create,release);
            try
            {
                pilotHorde=Physics.GetIgnoreLayerCollision(8,11);
                hordeHorde=Physics.GetIgnoreLayerCollision(11,11);
                for(int i=0;i<32;i++)wardLayers[i]=Physics.GetIgnoreLayerCollision(28,i);
                Physics.IgnoreLayerCollision(8,11,true);
                Physics.IgnoreLayerCollision(11,11,true);
                Arena.GeneratedRun=Bundle;
                members=Arena.Pilots.Where(p=>!p.Seat.AuthorityBoss).ToArray();
                session=new ExpeditionSession(run,simulationContent);
                session.Sources.Seat=who=>int.Parse(who.Substring(1));
                var director=new ExpeditionDirector(session,layout,who=>NativeArena.Vec(members[session.Sources.Seat(who)-1].Body.position-Vector3.up*NativeLocomotionRules.HalfHeight),Arena.ExpeditionVisible,100,100,60);
                Runtime.Ready(a=>
                {
                    a.Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,cooperative=true,teams=true});
                    a.Match.HoldResolution=()=>true;
                    a.BindExpeditionCombat(director,Bundle.World.Area,JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));
                });
                Runtime.SetOccupants(pilots);
                foreach(var p in members)
                {
                    int seat=p.Seat.id;
                    var slot=new ExpeditionPoint{x=seat*2-5,z=-27.5f-seat*1.5f};
                    if (walk != null)
                    {
                        var road = walk.Routes[2].Points;
                        slot.y = Mathf.Lerp(road[0].y, road[1].y, (slot.z-road[0].z)/(road[1].z-road[0].z));
                    }
                    if(!Arena.ExpeditionPlace("p"+seat,slot))throw new InvalidOperationException("Load fixture start blocked for seat "+seat);
                }
                firstPositions=members.Select(p=>p.Body.position).ToArray();
                maxTravel=new float[members.Length];
                Arena.PrimeDevLoad(4, walk != null);
            }
            catch{Dispose();throw;}
        }

        // Populate the isolated fixture from the surveyed streets. The ordinary deployment
        // generator and its wire compatibility never consume the candidate's world.
        private static void PrepareSurveyWorld(ExpeditionGeneratedWorld world, NativeLayeredCityWalk walk)
        {
            world.Map = walk.Map;
            var area = world.Area;
            area.map = walk.Map.id;
            var assigned = NativeCityLayout.DealHalls(world.Blueprint.worldSeed, world.Blueprint.instances);
            var doors = new System.Collections.Generic.List<ExpeditionDoorLayout>();
            var anchors = new System.Collections.Generic.List<ExpeditionWorldAnchor>();
            var caches = new System.Collections.Generic.List<ExpeditionInteractionLayout>();
            var cacheDraw = new SourceRandom(world.Blueprint.worldSeed ^ 0x896503dfu);
            var spawnDraw = new SourceRandom(world.Blueprint.worldSeed ^ 0x902b43c1u);
            for (int side = 1; side < walk.Routes.Count; side++)
            {
                string instance = assigned[side], id = "exp-city:door:" + instance;
                var mouth = walk.Routes[side].Points.Last();
                var forward = new[] { new V3(0,0,1), new V3(1,0,0), new V3(0,0,-1), new V3(-1,0,0) }[side];
                var right = new V3(forward.z,0,-forward.x);
                ExpeditionPoint Spot(float inward, float across)
                {
                    var at = mouth - forward * inward + right * across;
                    return new ExpeditionPoint { x=at.x, y=at.y, z=at.z, yaw=side*90 };
                }
                ExpeditionVolume Volume(ExpeditionPoint centre, float width, float depth) => new ExpeditionVolume {
                    x=centre.x, y=centre.y+2, z=centre.z, sx=width, sy=4, sz=depth, yaw=side*90 };
                var join = Spot(8,0);
                anchors.Add(new ExpeditionWorldAnchor { id=id, area=area.id, kind="door", building=instance,
                    x=mouth.x, y=mouth.y, z=mouth.z, yaw=side*90 });
                doors.Add(new ExpeditionDoorLayout { anchor=id, approach=Spot(4,0), closure=Volume(Spot(0,0),8,.5f),
                    interact=Volume(Spot(2,0),5,5), join=new ExpeditionJoinCylinder { x=join.x,y=join.y,z=join.z,r=3,h=3 },
                    entries=Array.Empty<ExpeditionPoint>(), returns=Enumerable.Range(0,8).Select(i => Spot(6+i/4*2,i%4*2-3)).ToArray() });
                var locker = Spot(13+(int)(cacheDraw.Next()*8), cacheDraw.Next()<.5 ? -9 : 9);
                string chest = "exp-city:cache:" + side;
                anchors.Add(new ExpeditionWorldAnchor { id=chest,area=area.id,kind="chest",x=locker.x,y=locker.y,z=locker.z });
                caches.Add(new ExpeditionInteractionLayout { anchor=chest,x=locker.x,y=locker.y,z=locker.z,range=2.5f });
            }
            area.doors = doors.ToArray();
            area.gates = Array.Empty<ExpeditionGateLayout>();
            area.anchors = anchors.ToArray();
            area.interactions = caches.ToArray();
            var spawnRows = new System.Collections.Generic.List<ExpeditionSpawnLayout>();
            var zones = new System.Collections.Generic.List<ExpeditionHubZone>();
            string[] kinds = { "shambler", "runner", "spitter", "brute", "hulk" };
            foreach (var route in walk.Routes)
            {
                // Sample the gate-to-hall centre line at measured distances.
                for (int station = 40 + (int)(spawnDraw.Next()*11); station < route.Length - 24; station += 28)
                {
                    float remaining = station;
                    for (int j = 1; j < route.Points.Length; j++)
                    {
                        var a = route.Points[j-1];
                        var d = route.Points[j] - a;
                        if (remaining > d.Length) { remaining -= d.Length; continue; }
                        var pos = a + d * (remaining / d.Length);
                        int id = spawnRows.Count;
                        var point = new PvePoint { x=pos.x, y=pos.y, z=pos.z };
                        spawnRows.Add(new ExpeditionSpawnLayout { allow=kinds, entry=new PveEntry {
                            e=id, id="survey-entry-"+id, kind="door", from=point, @exit=point, clear=3, allowed=kinds } });
                        zones.Add(new ExpeditionHubZone { Id="survey-zone-"+id, Ring=station<55?"near":station<95?"mid":"far",
                            Near=station-5, Far=station+5, Spawns=new[] { id },
                            Volume=new ExpeditionVolume { x=pos.x, y=pos.y+2, z=pos.z, sx=11, sy=6, sz=11 } });
                        break;
                    }
                }
            }
            world.Hub.Zones = zones.ToArray();
            var court = walk.Routes[0].Points.Last();
            area.bossPoints = new[] { new ExpeditionPoint { x=court.x, y=court.y, z=court.z } };
            area.spawns = spawnRows.ToArray();
        }

        public double[] Means()
        {
            Report();
            return sum.Select(total => total / measured).ToArray();
        }

        public void Step()
        {
            Tick++;
            foreach(var p in members)
            {
                // Opposing short patrols on parallel streets just outside the south gate.
                int seat=p.Seat.id;
                float extent=Bundle.World.Map.id==NativeExpeditionGeneratedWorld.MapId?7:6;
                float end=((Tick/150+seat)%2==0)?extent:-extent;
                var delta=new Vector3(end-p.Body.position.x,0,-27.5f-seat*1.5f-p.Body.position.z);
                var dir=Vector3.ClampMagnitude(delta,1);
                var input=new InputFrame{Sequence=Tick,X=dir.x,Z=dir.z,Yaw=Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg};
                if(seat==Arena.LocalId)Runtime.Input=input;else Arena.AcceptInput(seat,input);
                // Keep all four targets alive for the entire measurement, including repeated runs.
                p.Hp=100000;
            }
            double crowd=Arena.DevCrowdFlowMs,sense=Arena.DevPerceptionMs;
            session.Tick(1d/60);
            Runtime.Step(Tick);
            for(int i=0;i<members.Length;i++)
                maxTravel[i]=Math.Max(maxTravel[i],(members[i].Body.position-firstPositions[i]).sqrMagnitude);
            if(Tick>Warmup&&Tick<=Warmup+Samples)
            {
                if(members.Any(p=>p.Hp<=0)||Arena.Minions.Count(m=>m.Pve?.Hub!=null&&m.Hp>0)!=88||Arena.Pve.Cap!=88)
                    throw new InvalidOperationException("Load changed during the budget sample at tick "+Tick);
                sum[0]+=Runtime.NavigationMs;
                sum[1]+=Arena.DevCrowdFlowMs-crowd;
                sum[2]+=Arena.DevPerceptionMs-sense;
                sum[3]+=Runtime.PhysicsMs;
                measured++;
            }
        }
        public string Report()
        {
            if(measured!=Samples||maxTravel.Any(distance=>distance<1)||sum.Any(ms=>ms<=0))
                throw new InvalidOperationException("Incomplete load benchmark: verify patrol movement and all four native counters.");
            return FormattableString.Invariant($"EXPEDITION_SIM_BUDGET world={Bundle.World.Map.id} seed=17 unity={Application.unityVersion} cpu={SystemInfo.processorType} pilots={Pilots} zombies=88 ticks={measured} navMs={sum[0]/measured:F4} crowdFlowMs={sum[1]/measured:F4} perceptionMs={sum[2]/measured:F4} physicsMs={sum[3]/measured:F4}");
        }
        public void Dispose()
        {
            try{Runtime.Dispose();}
            finally
            {
                Physics.IgnoreLayerCollision(8,11,pilotHorde);
                Physics.IgnoreLayerCollision(11,11,hordeHorde);
                for(int i=0;i<32;i++)Physics.IgnoreLayerCollision(28,i,wardLayers[i]);
            }
        }
    }
}
#endif
