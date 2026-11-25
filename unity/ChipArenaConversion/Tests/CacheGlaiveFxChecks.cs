#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // Astra's revamp brief §4 (owner 2026-11-25: "lets get work started"; Claude C2, claude/cache-glaive-fx):
    // - the caches' licensed effects layer (Core NativeCachePackFx, Runtime NativeCachePackPresentation.cs): the four levels on the four
    //   tiers, where the glow and the burst stand on the locker, the gates (Reduced effects, the hologram, the chip machines), one
    //   burst per opening on the host and on a guest, of the tier the offer awarded, none for a late joiner, the DEV preview, and the
    //   runtime's hook, fallback and presentation-only sources;
    // - the Glaive's recorded throw, ricochet and catch: contract keys the bank is asked for first, today's stand-ins as the fallback,
    //   and the cues on the record's throw, ricochet and catch.
    internal static class CacheGlaiveFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Cache and Glaive effects: "+what);count++;}
        private static readonly NativeCacheTier[] Tiers=NativeCacheTiers.All;

        public static void Run(string root)
        {
            count=0;bool was=NativeCachePackFx.On;
            try{Levels();Placement();Gates();Bursts();HostAndGuest();Preview();Sources(root);GlaiveCues(root);}
            finally{NativeCachePackFx.On=was;}
            Console.WriteLine("PASS cache and Glaive effects: "+count+" checks: the pack's four levels on the four tiers, the glow at the latch and the burst at the mouth, the gates, one burst per opening on host and guest of the awarded tier, the preview, hooks and fallbacks; the Glaive's throw, ricochet and catch keys and stand-ins");
        }

        // The pack's Lv1-4 on STANDARD to LEGENDARY: every number rises (or holds) with the tier, LEGENDARY above STANDARD on each; a
        // distinct sparkle tile each, inside the 3 x 3 atlas.
        private static void Levels()
        {
            Check(NativeCachePackFx.Levels.Length==NativeCacheTiers.Count,"four levels");
            Check(NativeCachePackFx.Resource=="ChipArenaCacheFx"&&NativeCachePackFx.Glow!=NativeCachePackFx.Open,"one prefab, two effects");
            var names=new[]{NativeCachePackFx.Halo,NativeCachePackFx.Sparkles,NativeCachePackFx.Rays,NativeCachePackFx.Splash,NativeCachePackFx.Linger};
            Check(names.Distinct().Count()==names.Length&&names.All(n=>n.Length>0),"the systems are named apart");
            for(int i=0;i<Tiers.Length;i++)
            {
                var v=NativeCachePackFx.LevelOf(Tiers[i]);Check(NativeCachePackFx.LevelNumber(Tiers[i])==i+1,"tier "+i+" plays level "+(i+1));
                Check(v.SparkLifeMin>0&&v.SparkLifeMax>=v.SparkLifeMin&&v.RaysLifeMin>0&&v.RaysLifeMax>=v.RaysLifeMin&&v.RaysAlpha>0&&v.RaysAlpha<=1&&v.RaysSize>=1&&v.SplashSpeed>1,"tier "+i+" numbers are sane");
                Check(v.Tile>=0&&v.Tile<NativeCachePackFx.TilesX*NativeCachePackFx.TilesY,"tier "+i+"'s tile is in the atlas");
                if(i==0)continue;var u=NativeCachePackFx.LevelOf(Tiers[i-1]);
                Check(v.SparkRate>u.SparkRate&&v.Splash>=u.Splash&&v.Linger>=u.Linger&&v.RaysAlpha>=u.RaysAlpha&&v.RaysSize>=u.RaysSize&&v.RaysLifeMax>=u.RaysLifeMax&&v.SplashSpeed>=u.SplashSpeed,"tier "+i+" reads at least as rich as the tier below");
            }
            var s=NativeCachePackFx.LevelOf(NativeCacheTier.Standard);var l=NativeCachePackFx.LevelOf(NativeCacheTier.Legendary);
            Check(l.SparkRate>=3*s.SparkRate&&l.Splash>=3*s.Splash&&l.RaysAlpha>s.RaysAlpha&&l.RaysSize>s.RaysSize,"LEGENDARY reads far richer than STANDARD (the pack's Lv4 against its Lv1)");
            Check(Tiers.Select(t=>NativeCachePackFx.LevelOf(t).Tile).Distinct().Count()==4,"each tier its own sparkle tile (its colour)");
        }
        // The glow sits at the latch line, just proud of the front (a localized cue; nothing on the floor); the burst at the mouth.
        private static void Placement()
        {
            Check(NativeCachePackFx.GlowY==NativeCacheOpenFx.LatchY&&NativeCachePackFx.GlowZ<NativeCacheOpenFx.LatchZ&&NativeCacheOpenFx.LatchZ-NativeCachePackFx.GlowZ<=.1f,"the glow at the latches' height, just in front of them");
            Check(Math.Abs(NativeCachePackFx.GlowX)<NativeCacheOpenFx.LatchX[1],"the glow between the two latches");
            Check(NativeCachePackFx.GlowY>=NativeCacheOpenFx.SparkFloor,"the glow is not on the floor");
            Check(NativeCachePackFx.OpenY==NativeCacheOpenFx.MouthY&&NativeCachePackFx.OpenX==0&&Math.Abs(NativeCachePackFx.OpenZ)<.1f,"the burst rises from the middle of the tray's mouth");
            Check(NativeCachePackFx.Scale>.4f&&NativeCachePackFx.Scale<1,"the pack's chest-sized effect scaled to the locker");
            Check(NativeCachePackFx.BurstAt==NativeCacheOpenFx.LidAt,"the burst on R9's lid beat");
            Check(NativeCachePackFx.Late>0&&NativeCachePackFx.BurstAt+NativeCachePackFx.Late<NativeCacheOpenFx.End,"the burst window closes inside the opening");
            Check(NativeCachePackFx.OpenLife>0&&NativeCachePackFx.OpenLife<NativeCachePreview.Period-NativeCachePreview.SaleFor,"a burst is put away before the preview's next opening");
            Check(NativeCachePackFx.MaxGlows>=4&&NativeCachePackFx.MaxBursts>=4&&NativeCachePackFx.GlowReach>=30,"the preview's four lockers glow and burst together, at street distance");
        }
        private static NativeCacheShow Show(NativeCacheState state,float since,NativeCacheTier tier=NativeCacheTier.Rare,uint seed=7,string id="c",bool locker=true,bool machine=false)
            =>new NativeCacheShow{Id=id,State=state,Since=since,Tier=tier,Next=tier,Seed=seed,Locker=locker,Machine=machine,Sweep=float.NaN};
        private static void Gates()
        {
            NativeCachePackFx.On=true;
            Check(NativeCachePackFx.Glowing(Show(NativeCacheState.ForSale,0),false),"a locker for sale glows");
            Check(!NativeCachePackFx.Glowing(Show(NativeCacheState.ForSale,0),true),"Reduced effects: no glow (only the lid moves)");
            Check(!NativeCachePackFx.Glowing(Show(NativeCacheState.ForSale,0,locker:false),false),"the hologram cache keeps its own look");
            Check(!NativeCachePackFx.Glowing(Show(NativeCacheState.ForSale,0,id:null,machine:true),false),"no glow on a chip machine");
            foreach(var st in new[]{NativeCacheState.Opening,NativeCacheState.Restocking,NativeCacheState.Sold})Check(!NativeCachePackFx.Glowing(Show(st,0),false),"no glow while "+st);
            Check(!NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,NativeCachePackFx.BurstAt-.01f),false)&&NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,NativeCachePackFx.BurstAt),false)&&!NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,NativeCachePackFx.BurstAt+NativeCachePackFx.Late),false),"the burst window: from the lid's beat, for Late s");
            Check(!NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,.3f),true)&&!NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,.3f,locker:false),false)&&!NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,.3f,id:null,machine:true),false),"no burst with Reduced effects, on the hologram or a chip machine");
            NativeCachePackFx.On=false;Check(!NativeCachePackFx.Glowing(Show(NativeCacheState.ForSale,0),false)&&!NativeCachePackFx.InWindow(Show(NativeCacheState.Opening,.3f),false),"the switch turns the layer off");NativeCachePackFx.On=true;
        }
        // One burst per opening, however many frames or cameras show it; a new opening (another seed) bursts again; none first seen late.
        private static void Bursts()
        {
            var b=new NativeCacheBursts();int fired=0;
            for(float t=0;t<NativeCacheOpenFx.End;t+=1/60f){var s=Show(NativeCacheState.Opening,t);if(b.Fire(s,false))fired++;if(b.Fire(s,false))fired++;}
            Check(fired==1,"one burst for an opening at 60 Hz with two cameras ("+fired+")");
            fired=0;for(float t=0;t<NativeCacheOpenFx.End;t+=.1f)if(b.Fire(Show(NativeCacheState.Opening,t,seed:8),false))fired++;
            Check(fired==1,"the next opening (another generation) bursts once more");
            fired=0;for(float t=.9f;t<NativeCacheOpenFx.End;t+=1/60f)if(b.Fire(Show(NativeCacheState.Opening,t,seed:9),false))fired++;
            Check(fired==0,"an opening first seen late plays no burst");
            fired=0;for(float t=0;t<NativeCacheOpenFx.End;t+=1/60f)if(b.Fire(Show(NativeCacheState.Opening,t,seed:10),true))fired++;
            Check(fired==0,"Reduced effects: no burst");
        }
        // The host (the buyer) and a guest (a watcher) each read their own replicated view of one purchase: each plays exactly one
        // burst, at the same moment of the opening, of the tier the offer awarded; a late joiner plays none; a second purchase of
        // another tier one more each.
        private static void HostAndGuest()
        {
            var run=NativeExpedition.Create("cache-fx","city","p0","sword",91,new[]{new ExpeditionInstance{id="ward-zero",target=3}});Check(run.Join("p1","sword"),"join");
            Check(run.Credit("p0","cash",900),"credit");
            Check(run.SetWorld(new[]{new ExpeditionWorldAnchor{id="c1",area="city",kind="chest",x=2,yaw=90}},new[]{new ExpeditionChestOffer{id="c1",generation="g1",price=100,tier="legendary"}}),"world");
            var host=new NativeCacheClock{Solid=a=>true};var guest=new NativeCacheClock{Solid=a=>true};var hb=new NativeCacheBursts();var gb=new NativeCacheBursts();
            var hFired=new List<(float since,NativeCacheTier tier)>();var gFired=new List<(float since,NativeCacheTier tier)>();
            void Frames(double from,double to,double hostStep,double guestStep,double guestLag)
            {
                var hv=NativeExpeditionView.Build(run,"p0");var gv=NativeExpeditionView.Build(run,"p1");
                for(double t=from;t<to;t+=hostStep){host.Observe(hv,t);foreach(var s in host.Items)if(hb.Fire(s,false))hFired.Add((s.Since,s.Tier));}
                for(double t=from+guestLag;t<to+guestLag;t+=guestStep){guest.Observe(gv,t);foreach(var s in guest.Items){if(gb.Fire(s,false))gFired.Add((s.Since,s.Tier));gb.Fire(s,false);}}
            }
            Frames(10,11,1/60.0,1/45.0,.08);
            Check(hFired.Count==0&&gFired.Count==0,"for sale: no burst");
            Check(host.Items.Single().State==NativeCacheState.ForSale&&host.Items.Single().Locker&&host.Items.Single().Tier==NativeCacheTier.Legendary,"the host sees a LEGENDARY steel locker for sale");
            Check(run.Purchase("p0","g1",100)&&run.RestockChest("c1","g2",100,run.Snapshot().elapsed+42,"rare"),"p0 buys; the cache restocks with a cooldown");
            Frames(11,14,1/60.0,1/45.0,.08);
            Check(hFired.Count==1&&gFired.Count==1,"the host and the guest each play one burst ("+hFired.Count+", "+gFired.Count+")");
            Check(hFired[0].tier==NativeCacheTier.Legendary&&gFired[0].tier==NativeCacheTier.Legendary,"both of the tier the offer awarded (LEGENDARY), not the restock's next tier");
            Check(Math.Abs(hFired[0].since-NativeCachePackFx.BurstAt)<1/30f&&Math.Abs(gFired[0].since-NativeCachePackFx.BurstAt)<1/30f,"both as the lid lifts on their own clock");
            var h=host.Items.Single();var g=guest.Items.Single();Check(h.Buyer&&!g.Buyer&&h.Seed==g.Seed,"the buyer and a watcher see the same opening (one seed)");
            var late=new NativeCacheClock{Solid=a=>true};var lb=new NativeCacheBursts();int lateFired=0;
            for(double t=12;t<14;t+=1/60.0){late.Observe(NativeExpeditionView.Build(run,"p1"),t);foreach(var s in late.Items)if(lb.Fire(s,false))lateFired++;}
            Check(lateFired==0,"a late joiner plays none");
            // the cooldown passes; the guest buys the RARE stock: one more burst each, RARE
            for(int i=0;i<170;i++)run.Tick(.25);Check(run.Credit("p1","cash",500),"credit p1");Frames(60,61,1/60.0,1/45.0,.08);
            Check(host.Items.Single().State==NativeCacheState.ForSale&&host.Items.Single().Tier==NativeCacheTier.Rare,"restocked RARE");
            Check(run.Purchase("p1","g2",100)&&run.RestockChest("c1","g3",100,run.Snapshot().elapsed+42),"p1 buys the RARE stock");
            Frames(61,64,1/60.0,1/45.0,.08);
            Check(hFired.Count==2&&gFired.Count==2&&hFired[1].tier==NativeCacheTier.Rare&&gFired[1].tier==NativeCacheTier.Rare,"one more burst each, RARE");
        }
        // The DEV preview's row: each of the four lockers bursts once a cycle (the hologram never), each of its tier that cycle.
        private static void Preview()
        {
            NativeCachePreview.Place(0,0,0,180,0);var b=new NativeCacheBursts();var shows=new List<NativeCacheShow>();var clicks=new List<NativeCacheShow>();
            var tiers=new List<(int cycle,string id,NativeCacheTier tier)>();double prev=0;
            for(double t=0;t<2*NativeCachePreview.Period;t+=1/30.0)
            {
                shows.Clear();clicks.Clear();NativeCachePreview.Fill(shows,clicks,t,prev);prev=t;int c=NativeCachePreview.Cycle(t,out _);
                foreach(var s in shows)if(b.Fire(s,false))tiers.Add((c,s.Id,s.Tier));
            }
            Check(tiers.Count==8&&tiers.All(x=>x.id!="dev-cache-hologram"),"four bursts a cycle, none on the hologram ("+tiers.Count+")");
            Check(tiers.All(x=>x.tier==(NativeCacheTier)((int.Parse(x.id.Substring(10))+x.cycle)%4)),"each locker bursts in its tier that cycle");
            NativeCachePreview.Start=double.NaN;
        }
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string core=R("Core/NativeCachePackFx.cs"),run=R("Runtime/NativeCachePackPresentation.cs");
            foreach(var (name,src) in new[]{("Core/NativeCachePackFx.cs",core),("Runtime/NativeCachePackPresentation.cs",run)})
            {
                Check(!Regex.IsMatch(src,@"\b(run|Run|session|Session|authority|arena)\s*\.\s*(Purchase|RestockChest|SetWorld|Credit|Claim|Select|Spend|Damage|Send)\("),name+" calls no authority mutator or sender");
                Check(!src.Contains("System.Random")&&!src.Contains("UnityEngine.Random")&&!src.Contains("NativePveRandom"),name+" draws no randomness of its own");
                Check(!Regex.IsMatch(src,@"0x[0-9a-fA-F]{6}"),name+" restates no colour (the tiers' colours are Core's)");
            }
            Check(R("Runtime/NativeCachePresentation.cs").Contains("CacheBuild(arena);CachePackFx(camera);if(vfx==null||cacheFrameCount==0)return;"),"the layer runs from the caches' effects pass, every frame, with or without caches");
            Check(run.Contains("Resources.Load<GameObject>(NativeCachePackFx.Resource);if(prefab==null)return false;"),"without the effects prefab nothing extra draws");
            Check(run.Contains("if(LowGraphics||CacheReduced||!NativeCachePackFx.On||!CachePackBank())return;"),"off on the low graphics tier and with Reduced effects");
            Check(run.Contains("cacheBursts.Fire(s,CacheReduced)")&&run.Contains("NativeCachePackFx.Glowing(s,CacheReduced)"),"the runtime asks Core's gates");
            Check(run.Contains("private void LateUpdate(){if(Off!=null&&Drawn<Time.frameCount-1)Off();}"),"put away when the caches stop drawing");
        }
        // The Glaive's throw, ricochet and catch ask the bank for their own recordings first (topdog.ring-*: contract keys,
        // positional, long enough for the clips), then today's recorded stand-ins, then the synthesized cue; the record's throw,
        // its throw and ricochet hits and its catch play them.
        private static void GlaiveCues(string root)
        {
            foreach(var (id,cap) in new[]{("ring-throw",.6f),("ring-ping",.5f),("ring-catch",.4f)})
            {
                string key=NativeToonSynth.BankKey(id);Check(key=="topdog."+id,id+"'s bank key");
                Check(NativeSoundKeys.Find(key,out var def)&&def.Space==NativeSoundSpace.Positional&&def.Cap>=cap,key+" is a positional contract key long enough for its clips");
                Check(NativeRecordedCues.Weapon(id).Key!=null,id+" keeps today's recorded stand-in as its fallback");
                Check(NativeToonSynth.Samples(id).Length>0,id+" keeps its synthesized cue as the last fallback");
            }
            Check(NativeRecordedCues.Weapon("ring-ping").Key!=NativeRecordedCues.Weapon("ring-catch").Key||NativeRecordedCues.Weapon("ring-ping").Take!=NativeRecordedCues.Weapon("ring-catch").Take,"the stand-ins differ");
            string toon=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeTopDogScreen.cs"));
            int bankAt=toon.IndexOf("if(BankPlay(key,position,gain,0,null,mine,rateScale:rate))return;",StringComparison.Ordinal),standIn=toon.IndexOf("if(PlayRecording(id,NativeRecordedCues.Weapon(id),position,gain,mine,rate))return;",StringComparison.Ordinal);
            Check(bankAt>0&&standIn>bankAt,"Toon asks the bank's own key before the stand-in, then synthesizes");
            string glaive=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeGlaivePresentation.cs"));
            Check(glaive.Contains("string id=k.CastKind==NativeGlaive.Throw?\"ring-throw\""),"the throw's release plays ring-throw");
            Check(glaive.Contains("case NativeGlaive.HitThrow:case NativeGlaive.HitRicochet:Toon(\"ring-ping\",at,.85f,mine,1+.06f*k.Bounces);"),"each throw hit and ricochet plays ring-ping, higher each bounce");
            Check(glaive.Contains("case NativeGlaive.HitCatch:Toon(\"ring-catch\",at,.7f,mine);"),"the catch plays ring-catch");
        }
    }
}
#endif
