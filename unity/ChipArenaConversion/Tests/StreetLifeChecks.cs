using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

// Meridian's street life (Core NativeMeridianStreetLife; docs/expedition/MERIDIAN-STREETS.md): the honesty rule on every piece of
// every layout's plan, measured by this check's own geometry (no invisible walls and nothing solid-looking a pilot can walk
// through: flat paint lies on a floor within 6 cm; wall art lies on a collider face within 10 cm; low clutter stands against a
// wall within 0.25 m; everything else hangs 3.1 m or more over the floor under it; nothing of it enters a block), the keep-outs
// (the camp, the caches, the door fronts, the bleed's warp, the dress's own paint), paint never on paint, determinism (the
// layout's fittings fixed, the run's story moving), the light's flash rate with reduced effects, the budgets with the bank
// (CHIPARENA_STREETS_BANK, a path to ChipArenaCityStreets.bytes), and the runtime's hooks.
internal static class StreetLifeChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Street life: "+what);}
    private static readonly string[] Deal={"Ward Zero","Central Station","City Ironworks"};

    /// <summary>A walk's plan as the walk plans it: the dress (every module, as with the bank), the bleed and the landmarks.</summary>
    internal static (NativeLayeredCityWalk walk,NativeMeridianStreetLife.Input input,NativeMeridianDress.Plan dress,NativeCityBleedDress.Plan bleedPlan) Walk(int layout,uint run,bool others=true)
    {
        var info=NativeCityLayout.Plans[layout];var walk=new NativeLayeredCityWalk(info);var slots=NativeCityLayout.DealHalls(run,Deal);
        NativeMeridianDress.Plan dress=null;NativeCityBleed bleed=null;NativeCityBleedDress.Plan bleedPlan=null;
        if(others)
        {
            bleed=NativeCityBleed.ForWalk(walk,slots);bleedPlan=NativeCityBleedDress.ForWalk(walk,bleed,run);
            try
            {
                NativeMeridianDress.ThemeAt=bleed.ThemeAt;var bp=bleedPlan;var bl=bleed;
                NativeMeridianDress.LampsReplaced=p=>bl.ReplacesLamps(p)&&NativeCityBleedDress.NearLamp(bp,p,14);
                NativeMeridianDress.FacadeCovered=(p,n)=>NativeCityBleedDress.Covers(bp,p,n);
                dress=NativeMeridianDress.Build(MeridianDressChecks.InputOf(walk,info.Seed,run));
            }
            finally{NativeMeridianDress.ThemeAt=null;NativeMeridianDress.LampsReplaced=null;NativeMeridianDress.FacadeCovered=null;}
        }
        var input=NativeMeridianStreetLife.InputOf(walk,info.Seed,run,slots,dress,bleed,bleedPlan);
        return (walk,input,dress,bleedPlan);
    }

    public static void Run(string root)
    {
        count=0;var K=NativeMeridianStreetLife.Kinds;
        // ---- the table: every mount's envelope inside its rule ----
        foreach(var k in K)
        {
            switch(k.Mount)
            {
                case NativeMeridianStreetLife.Mount.Floor:Check(k.Min.y>=0&&k.Max.y<=NativeMeridianStreetLife.Flat+1e-5f,k.Key+": flat paint, 6 cm or less");break;
                case NativeMeridianStreetLife.Mount.Wall:Check(k.Min.z>NativeMeridianStreetLife.HugOut&&k.Max.z<=NativeMeridianStreetLife.WallOut+1e-5f,k.Key+": wall art lies past the facades' 6 cm band and within 10 cm");break;
                case NativeMeridianStreetLife.Mount.Low:Check(k.Min.z>NativeMeridianStreetLife.HugOut&&k.Max.z<=NativeMeridianStreetLife.Reach+1e-5f&&(k.Stretch||k.Max.y<=NativeMeridianStreetLife.LowTop+1e-5f)&&k.Min.y>=0,k.Key+": low clutter within 0.25 m of its wall and under 1.6 m");break;
            }
            Check(!k.Lit||k.Light!=0&&k.Pool>0,k.Key+": a lit kind has its light and pool");
        }
        Check(K.Select(k=>k.Key).Distinct().Count()==K.Length&&K.Length>=70,"the kinds are distinct ("+K.Length+")");
        // ---- the light: never faster than 3 Hz with reduced effects (sampled at 60 Hz over 20 s), and the full flicker under 3 ----
        foreach(NativeMeridianStreetLife.Life life in Enum.GetValues(typeof(NativeMeridianStreetLife.Life)))
            foreach(bool reduced in new[]{false,true})
            {
                Check(NativeMeridianStreetLife.Rate(life,reduced)<3f,life+(reduced?" (reduced)":"")+": its rate is under 3 Hz");
                foreach(uint seed in new uint[]{7,912,40211})
                {
                    int most=Flashes(life,seed,reduced);
                    Check(most<=3,life+(reduced?" (reduced)":"")+" seed "+seed+": "+most+" flashes in its worst second (3 at most)");
                    if(reduced)Check(most<=1,life+" (reduced) seed "+seed+": "+most+" flashes in its worst second (reduced effects breathe, they do not flash)");
                }
            }
        // ---- every layout: the walk (two runs, with and without the other drawers) and its live deal ----
        var bankPath=Environment.GetEnvironmentVariable("CHIPARENA_STREETS_BANK");NativePackBank.Bank bank=null;
        if(!string.IsNullOrEmpty(bankPath)&&File.Exists(bankPath)){bank=NativePackBank.Read(File.ReadAllBytes(bankPath));BankChecks(bank);}
        var report=new List<string>();var sw=new Stopwatch();
        for(int li=0;li<NativeCityLayout.Plans.Count;li++)
        {
            var name=NativeCityLayout.Plans[li].Name;
            var (walk,input,dress,bleedPlan)=Walk(li,77);sw.Restart();var plan=NativeMeridianStreetLife.Build(input);sw.Stop();
            Honest(name,walk.Map,input,plan,dress);
            // determinism: the same records, the same plan; another run moves the story but not the city's fittings
            Check(NativeMeridianStreetLife.Build(Walk(li,77).input).Digest()==plan.Digest(),name+": deterministic");
            var other=NativeMeridianStreetLife.Build(Walk(li,991).input);Check(other.Digest()!=plan.Digest(),name+": another run deals another story");
            string Fixed(NativeMeridianStreetLife.Plan p)=>string.Join("|",p.Pieces.Where(q=>Architecture(K[q.Kind].Key)).Select(q=>K[q.Kind].Key+FormattableString.Invariant($"@{q.At.x:F2},{q.At.y:F2},{q.At.z:F2}/{q.Yaw:F3}")).OrderBy(s=>s,StringComparer.Ordinal));
            // (two runs that deal the same halls: the door fronts the fittings keep off are the deal's)
            uint same=78;var dealt=NativeCityLayout.DealHalls(77,Deal);while(!NativeCityLayout.DealHalls(same,Deal).SequenceEqual(dealt))same++;
            var bare=Walk(li,77,false);var fixedThis=NativeMeridianStreetLife.Build(bare.input);var fixedOther=NativeMeridianStreetLife.Build(Walk(li,same,false).input);
            Check(Fixed(fixedThis)==Fixed(fixedOther)&&fixedThis.Digest()!=fixedOther.Digest(),name+": the layout's fittings stand where they stood (markings, drains, signs, awnings, wires); the story moves");
            // the plan without the other drawers (a build without their banks) keeps every rule too
            Honest(name+" (alone)",bare.walk.Map,bare.input,fixedThis,null);
            // budgets with the bank: the six standard eyes (the camp's pilot, each route 18 m short of its door, the overview)
            string budget=bank!=null?Budgets(name,walk,plan,bank):"";
            report.Add(FormattableString.Invariant($"{name}: {plan.Pieces.Count} pieces ({string.Join(", ",Enum.GetValues(typeof(NativeMeridianStreetLife.Mount)).Cast<NativeMeridianStreetLife.Mount>().Select(m=>m+" "+plan.Count(m)))}) in {sw.ElapsedMilliseconds} ms{budget}"));
        }
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var world=live.World;var info=NativeCityLayout.Plans[live.Plan];uint seed=world.Blueprint.worldSeed;
            var bleedPlan=NativeCityBleedDress.ForWalk(world.City,world.Bleed,seed);var dress=HubZFightChecks.Dress(live,null,true);
            var input=NativeMeridianStreetLife.InputOf(world.City,info.Seed,seed,world.HallSlots,dress,world.Bleed,bleedPlan);
            var plan=NativeMeridianStreetLife.Build(input);Honest(info.Name+" live "+live.Seed,world.City.Map,input,plan,dress);
            report.Add(FormattableString.Invariant($"{info.Name} live deal {live.Seed}: {plan.Pieces.Count} pieces"));
        }
        Hooks(root);
        foreach(var line in report)Console.WriteLine("  "+line);
        Console.WriteLine("PASS meridian street life: "+count+" checks over the five layouts and their live deals"+(bank==null?" (no bank: the budgets need CHIPARENA_STREETS_BANK)":" with the bank"));
    }
    /// <summary>The layout's fittings (where they stand is the layout's; only their states are the run's).</summary>
    private static bool Architecture(string key)=>key.StartsWith("road.",StringComparison.Ordinal)||key=="ground.manhole"||key=="ground.drain"||key=="ground.grate"||key=="low.pipe"||
        key.StartsWith("upper.awning",StringComparison.Ordinal)||key.StartsWith("upper.sign",StringComparison.Ordinal)||key=="upper.ac"||key=="upper.box"||key=="upper.flag"||key=="upper.flood"||key=="upper.signal"||
        key.StartsWith("span.",StringComparison.Ordinal)||key=="hung.signal"||key.StartsWith("mark.cd",StringComparison.Ordinal);
    /// <summary>The most flashes (rises of 0.1 or more to a peak) a state's light makes in any second of 20 s at 60 Hz.</summary>
    private static int Flashes(NativeMeridianStreetLife.Life life,uint seed,bool reduced)
    {
        const int hz=60,n=hz*20;var v=new float[n];for(int i=0;i<n;i++)v[i]=NativeMeridianStreetLife.Level(life,seed,i/(float)hz+3.7f,reduced);
        var peaks=new List<int>();float low=v[0];
        for(int i=1;i+1<n;i++){low=Math.Min(low,v[i]);if(v[i]>=v[i-1]&&v[i]>v[i+1]&&v[i]-low>=.1f){peaks.Add(i);low=v[i];}}
        int most=0;for(int a=0;a<peaks.Count;a++){int b=a;while(b<peaks.Count&&peaks[b]-peaks[a]<hz)b++;most=Math.Max(most,b-a);}
        return most;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // The honesty rule and the keep-outs, piece by piece, by this check's own measure
    // ------------------------------------------------------------------------------------------------------------------
    private static void Honest(string name,ContentMap map,NativeMeridianStreetLife.Input input,NativeMeridianStreetLife.Plan plan,NativeMeridianDress.Plan dress)
    {
        var K=NativeMeridianStreetLife.Kinds;var S=new NativeMeridianDress.Solids(map.blocks);var B=S.B;
        var bleed=input.Bleed;var floorBoxes=new Dictionary<long,List<(V3 lo,V3 hi)>>();int floors=0;
        var dressPaint=new List<V3>();if(dress!=null)foreach(var p in dress.Placements)if(p.Paint&&p.Block<0)dressPaint.Add(new V3(p.M[3],p.M[7],p.M[11]));
        foreach(var p in plan.Pieces)
        {
            var k=K[p.Kind];NativeMeridianStreetLife.Bounds(p,out var lo,out var hi);string what=name+": "+k.Key+FormattableString.Invariant($" at ({p.At.x:F2},{p.At.y:F2},{p.At.z:F2})");
            // the camp, the caches, the doors, the bleed's warp and threshold
            Check(!(lo.x<25.6f&&hi.x>-25.6f&&lo.z<25.6f&&hi.z>-25.6f&&lo.y<9f),what+" stands in the camp");
            if(input.KeepOut!=null)foreach(var ko in input.KeepOut)if(k.Mount!=NativeMeridianStreetLife.Mount.Wall||ko.Core)Check(!ko.Hits(lo,hi),what+" enters a cache's keep-out");
            foreach(var r in input.Routes){var d=r.Points[r.Points.Length-1];float dx=Math.Max(0,Math.Max(lo.x-d.x,d.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-d.z,d.z-hi.z));Check(dx*dx+dz*dz>=NativeMeridianLandmarks.ApronDepth*NativeMeridianLandmarks.ApronDepth||Math.Abs(p.At.y-d.y)>14,what+" stands in a door front");}
            if(bleed!=null){var s=bleed.At(new V3((lo.x+hi.x)*.5f,lo.y,(lo.z+hi.z)*.5f));Check(s.Door<0||s.Band<NativeCityBleed.Bands.Warp||NativeCityBleed.Theme(bleed.Doors[s.Door].Instance)=="final",what+" stands in the bleed's "+s.Band);}
            switch(k.Mount)
            {
                case NativeMeridianStreetLife.Mount.Floor:
                {
                    floors++;
                    Check(hi.y<=p.At.y+NativeMeridianStreetLife.Flat+1e-4f&&lo.y>=p.At.y-1e-4f,what+": paint 6 cm or less");
                    // its footprint on a floor's top at its height, nothing standing on it
                    foreach(float u in new[]{.03f,.5f,.97f})foreach(float v in new[]{.03f,.5f,.97f})
                    {
                        float x=lo.x+(hi.x-lo.x)*u,z=lo.z+(hi.z-lo.z)*v;
                        Check(Math.Abs(S.Floor(x,z,p.At.y+.02f)-p.At.y)<.012f,what+FormattableString.Invariant($": the floor under ({x:F2},{z:F2}) is not its top"));
                        Check(!S.Inside(x,p.At.y+.1f,z,-1,.005f),what+": something stands on its paint");
                    }
                    // paint never on paint (its own), and never on the dress's road paint and puddles
                    for(int cz=(int)Math.Floor(lo.z/4);cz<=(int)Math.Floor(hi.z/4);cz++)for(int cx=(int)Math.Floor(lo.x/4);cx<=(int)Math.Floor(hi.x/4);cx++)
                    {
                        if(!floorBoxes.TryGetValue(((long)cx<<32)^(uint)cz,out var l))floorBoxes[((long)cx<<32)^(uint)cz]=l=new List<(V3,V3)>();
                        foreach(var (a,b) in l)Check(!(Math.Min(hi.x,b.x)-Math.Max(lo.x,a.x)>.01f&&Math.Min(hi.z,b.z)-Math.Max(lo.z,a.z)>.01f&&Math.Abs(a.y-lo.y)<.05f),what+": paint on paint");
                    }
                    for(int cz=(int)Math.Floor(lo.z/4);cz<=(int)Math.Floor(hi.z/4);cz++)for(int cx=(int)Math.Floor(lo.x/4);cx<=(int)Math.Floor(hi.x/4);cx++)floorBoxes[((long)cx<<32)^(uint)cz].Add((lo,hi));
                    foreach(var d in dressPaint)if(Math.Abs(d.y-p.At.y)<.2f)Check(!(d.x>lo.x-.6f&&d.x<hi.x+.6f&&d.z>lo.z-.6f&&d.z<hi.z+.6f),what+": on the dress's road paint");
                    break;
                }
                case NativeMeridianStreetLife.Mount.Wall:
                case NativeMeridianStreetLife.Mount.Low:
                {
                    Check(p.Block>=0,what+": on no wall");int o=p.Block*6;var n=new V3((float)Math.Sin(p.Yaw),0,(float)Math.Cos(p.Yaw));
                    float plane=n.x>.5f?B[o+1]:n.x<-.5f?B[o]:n.z>.5f?B[o+5]:B[o+4];float at=Math.Abs(n.x)>.5f?p.At.x:p.At.z;
                    Check(Math.Abs(at-plane)<.002f,what+": not on its wall's face");
                    // its depth off the face: wall art within 10 cm, low clutter within 0.25 m
                    float depth=Math.Abs(n.x)>.5f?Math.Max(Math.Abs(hi.x-plane),Math.Abs(lo.x-plane)):Math.Max(Math.Abs(hi.z-plane),Math.Abs(lo.z-plane));
                    Check(depth<=(k.Mount==NativeMeridianStreetLife.Mount.Wall?NativeMeridianStreetLife.WallOut:NativeMeridianStreetLife.Reach)+1e-3f,what+FormattableString.Invariant($": {depth:F3} m off its wall"));
                    // the face continuous behind it: 5 cm in is its block, 5 cm out is free, at its corners and middle
                    foreach(float u in new[]{.04f,.5f,.96f})foreach(float v in new[]{.04f,.5f,.96f})
                    {
                        float y=lo.y+(hi.y-lo.y)*v;
                        var f=Math.Abs(n.x)>.5f?new V3(plane,y,lo.z+(hi.z-lo.z)*u):new V3(lo.x+(hi.x-lo.x)*u,y,plane);
                        var inside=f-n*.05f;Check(inside.x>B[o]&&inside.x<B[o+1]&&inside.y>B[o+2]&&inside.y<B[o+3]&&inside.z>B[o+4]&&inside.z<B[o+5],what+": hangs off its wall's face");
                        var outside=f+n*.05f;Check(!S.Inside(outside.x,outside.y,outside.z,-1,.005f),what+": its wall is hidden there");
                    }
                    if(k.Mount==NativeMeridianStreetLife.Mount.Low&&!k.Stretch)
                    {
                        var foot=NativeMeridianStreetLife.Local(p,new V3(0,0,(k.Min.z+k.Max.z)*.5f));Check(Math.Abs(S.Floor(foot.x,foot.z,p.At.y+.02f)-p.At.y)<.012f,what+": not standing on the floor in front of its wall");
                        Check(hi.y-p.At.y<=NativeMeridianStreetLife.LowTop+1e-3f,what+": taller than 1.6 m");
                    }
                    Check(BoxClear(S,lo+new V3(.012f,.012f,.012f),hi-new V3(.012f,.012f,.012f),p.Block),what+": enters a block");
                    break;
                }
                default:
                {
                    Check(BoxClear(S,lo+new V3(.06f,.015f,.06f),hi-new V3(.06f,.015f,.06f),p.Block),what+": enters a block");
                    // every part 3.1 m or more over the floor under it
                    for(int i=0;i<=4;i++)for(int j=0;j<=4;j++)
                    {
                        float x=lo.x+(hi.x-lo.x)*i/4,z=lo.z+(hi.z-lo.z)*j/4;float f=S.Floor(x,z,lo.y-.01f);
                        if(f>-1e5f)Check(lo.y-f>=NativeMeridianStreetLife.Clear-.02f,what+FormattableString.Invariant($": hangs {lo.y-f:F2} m over the floor under it (3.1 m at least)"));
                    }
                    break;
                }
            }
        }
        Check(floors>500&&plan.Count(NativeMeridianStreetLife.Mount.Wall)>200&&plan.Count(NativeMeridianStreetLife.Mount.Span)>40,name+": the streets are dressed ("+plan.Pieces.Count+" pieces)");
    }
    private static bool BoxClear(NativeMeridianDress.Solids S,V3 lo,V3 hi,int skip)
    {
        var B=S.B;foreach(int i in S.Near(lo.x,hi.x,lo.z,hi.z)){if(i==skip)continue;int o=i*6;if(hi.x>B[o]&&lo.x<B[o+1]&&hi.y>B[o+2]&&lo.y<B[o+3]&&hi.z>B[o+4]&&lo.z<B[o+5])return false;}
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // The bank and the budgets
    // ------------------------------------------------------------------------------------------------------------------
    /// <summary>The views' caps: High's complete view (every piece in its reach, both sides of the eye, as GeneratedFrameChecks
    /// counts a view) and Low's, and the draws.</summary>
    public const int HighTris=150000,LowTris=45000,HighDraws=170,LowDraws=110;
    private static void BankChecks(NativePackBank.Bank bank)
    {
        foreach(var k in NativeMeridianStreetLife.Kinds)foreach(int t in new[]{0,1,2})
        {
            var prop=bank.Find("rig/streets/"+k.Key+"/"+t);Check(prop!=null&&prop.Parts.Length>0,"the bank carries "+k.Key+" at tier "+t);
            var m=bank.Meshes[prop.Parts[0].Mesh];foreach(var v in m.Positions)Check(v.X>=k.Min.x-.003f&&v.X<=k.Max.x+.003f&&v.Y>=k.Min.y-.003f&&v.Y<=k.Max.y+.003f&&v.Z>=k.Min.z-.003f&&v.Z<=k.Max.z+.003f,k.Key+" tier "+t+": a vertex outside its envelope");
            int tris=Tris(bank,prop);Check(tris<=(t==2?400:2000),k.Key+" tier "+t+": "+tris+" triangles");
        }
    }
    private static int Tris(NativePackBank.Bank bank,NativePackBank.Prop prop){int n=0;foreach(var part in prop.Parts){var m=bank.Meshes[part.Mesh];foreach(var s in m.Submeshes)n+=s.Indices.Length/3;}return n;}
    private static string Budgets(string name,NativeLayeredCityWalk walk,NativeMeridianStreetLife.Plan plan,NativePackBank.Bank bank)
    {
        var K=NativeMeridianStreetLife.Kinds;var near=new int[K.Length];var far=new int[K.Length];var subs=new int[K.Length];
        for(int k=0;k<K.Length;k++){var a=bank.Find("rig/streets/"+K[k].Key+"/0");var b=bank.Find("rig/streets/"+K[k].Key+"/2");near[k]=Tris(bank,a);far[k]=Tris(bank,b);subs[k]=bank.Meshes[a.Parts[0].Mesh].Submeshes.Length;}
        var eyes=new List<V3>{new V3(1,6,-10)};foreach(var r in walk.Routes){r.Sample(r.Length-18,out var foot,out _,out _);eyes.Add(foot+new V3(0,2,0));}eyes.Add(new V3(0,42,-18));
        string text="";
        foreach(bool low in new[]{false,true})
        {
            int maxT=0,maxD=0;float scale=low?.7f:1f;
            foreach(var e in eyes)
            {
                int t=0;var keys=new Dictionary<int,int>();
                foreach(var p in plan.Pieces)
                {
                    var k=K[p.Kind];NativeMeridianStreetLife.Bounds(p,out var lo,out var hi);
                    float dx=Math.Max(0,Math.Max(lo.x-e.x,e.x-hi.x)),dy=Math.Max(0,Math.Max(lo.y-e.y,e.y-hi.y)),dz=Math.Max(0,Math.Max(lo.z-e.z,e.z-hi.z));float d=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
                    float reach=NativeMeridianStreetLifeReach(k)*(low?(k.Lit||k.Mount==NativeMeridianStreetLife.Mount.Floor||k.Mount==NativeMeridianStreetLife.Mount.Wall||k.Mount==NativeMeridianStreetLife.Mount.Low?.7f:.55f):1f);
                    if(d>reach)continue;bool lod0=!low&&d<NativeStageArt.MidReach;t+=lod0?near[p.Kind]:far[p.Kind];int key=p.Kind*2+(lod0?0:1);keys.TryGetValue(key,out int c);keys[key]=c+1;
                }
                int draws=0;foreach(var kv in keys)draws+=subs[kv.Key>>1]*((kv.Value+499)/500);
                maxT=Math.Max(maxT,t);maxD=Math.Max(maxD,draws);
            }
            if(Environment.GetEnvironmentVariable("STREETS_BUDGET_PRINT")==null)Check(maxT<=(low?LowTris:HighTris)&&maxD<=(low?LowDraws:HighDraws),name+(low?" Low":" High")+": the street life's worst view "+maxT+" triangles, "+maxD+" draws");
            text+=FormattableString.Invariant($"; {(low?"Low":"High")} worst view {maxT:N0} triangles, {maxD} draws");
        }
        return text;
    }
    /// <summary>The drawer's reach for a kind (Runtime NativeMeridianStreetLifeDraw.ReachOf, which this pins by its source).</summary>
    private static float NativeMeridianStreetLifeReach(NativeMeridianStreetLife.Kind k)=>k.Lit?175f:k.Mount==NativeMeridianStreetLife.Mount.Floor?85f:k.Mount==NativeMeridianStreetLife.Mount.Wall||k.Mount==NativeMeridianStreetLife.Mount.Low?110f:150f;

    // ------------------------------------------------------------------------------------------------------------------
    // The runtime's hooks (source): one line each in the live ShowMeridian and the walk's open and close, the drawer's bank
    // guard and reach, the DEV preview compiled only into the Editor and development builds
    // ------------------------------------------------------------------------------------------------------------------
    private static void Hooks(string root)
    {
        string Src(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        var live=Src("NativeExpeditionWorldPresentation.cs");
        Check(live.Contains("            StreetLifeClose();\n")&&live.Contains("StreetLifeOpen(world,plan.Seed,liveBleed!=null?bleed:null,liveBleed!=null?dress:null);"),"the live run: one line in ShowMeridian's teardown and one after the landmarks");
        var walk=Src("NativeLayeredCityWalkPreview.cs");
        Check(walk.Contains("StreetLifeWalkOpen(cityWalk, plan.Seed, seed);")&&walk.Contains("StreetLifeWalkClose();"),"the walk: one line each in OpenCityWalk and CloseCityWalk");
        var draw=Src("NativeMeridianStreetLifeDraw.cs");
        Check(draw.Contains("var bank=NativePackBanks.Bank(NativeMeridianStreetLife.Bank);if(bank==null)return null;")&&draw.Contains("NativeAdvancedLighting.Reduced"),"the drawer: nothing without the bank; reduced effects read where the light is drawn");
        Check(draw.Contains("lit?175f:m==NativeMeridianStreetLife.Mount.Floor?85f:m==NativeMeridianStreetLife.Mount.Wall||m==NativeMeridianStreetLife.Mount.Low?110f:150f")&&draw.Contains("lit||m==NativeMeridianStreetLife.Mount.Floor||m==NativeMeridianStreetLife.Mount.Wall||m==NativeMeridianStreetLife.Mount.Low?.7f:.55f"),"the drawer's reach per mount and tier is the budgets' (this check counts views by it)");
        Check(!draw.Contains("Collider")&&!draw.Contains("Physics"),"the drawer makes no collider");
        var dev=Src("NativeMeridianStreetLifeWalk.cs");
        Check(dev.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD",StringComparison.Ordinal)&&dev.TrimEnd().EndsWith("#endif",StringComparison.Ordinal),"the props' DEV preview is compiled only into the Editor and development builds");
        Check(!live.Contains("StreetPropsPreview")&&!Src("NativeMeridianStreetLifeLive.cs").Contains("StreetPropsPreview"),"the live run never draws the solid props (their blocks are Astra's, not built yet)");
    }

    public static void Probe(string root)
    {
        if(Environment.GetEnvironmentVariable("STREETS_DUMP")!=null)
        {
            // each live deal's blocks, routes and pieces as CSV (a debug map: the capture paths are posed on it)
            var dir=Environment.GetEnvironmentVariable("STREETS_DUMP");Directory.CreateDirectory(dir);
            foreach(var live in HubZFightChecks.Worlds(root))
            {
                var world=live.World;var info=NativeCityLayout.Plans[live.Plan];uint seed=world.Blueprint.worldSeed;
                var bleedPlan=NativeCityBleedDress.ForWalk(world.City,world.Bleed,seed);var dress=HubZFightChecks.Dress(live,null,true);
                var input=NativeMeridianStreetLife.InputOf(world.City,info.Seed,seed,world.HallSlots,dress,world.Bleed,bleedPlan);var plan=NativeMeridianStreetLife.Build(input);
                var props=NativeMeridianStreetLife.ProposeProps(input);
                using var w=new StreamWriter(Path.Combine(dir,info.Seed+".csv"));var ci=System.Globalization.CultureInfo.InvariantCulture;
                foreach(var b in world.City.Map.blocks)w.WriteLine(string.Format(ci,"B,{0},{1},{2},{3},{4},{5},{6}",b.id,b.pos.x-b.size.x/2,b.pos.x+b.size.x/2,b.pos.y-b.size.y/2,b.pos.y+b.size.y/2,b.pos.z-b.size.z/2,b.pos.z+b.size.z/2));
                foreach(var r in world.City.Routes)w.WriteLine("R,"+r.Name.Replace(',',' ')+","+string.Join(";",r.Points.Select(q=>string.Format(ci,"{0:F1} {1:F1} {2:F1}",q.x,q.y,q.z))));
                foreach(var p in plan.Pieces){NativeMeridianStreetLife.Bounds(p,out var lo,out var hi);w.WriteLine(string.Format(ci,"P,{0},{1},{2},{3},{4},{5},{6},{7}",NativeMeridianStreetLife.Kinds[p.Kind].Key,lo.x,hi.x,lo.y,hi.y,lo.z,hi.z,p.District));}
                foreach(var b in props)w.WriteLine(string.Format(ci,"Q,{0},{1},{2},{3},{4},{5},{6},{7}",b.Stem,b.Lo.x,b.Hi.x,b.Lo.y,b.Hi.y,b.Lo.z,b.Hi.z,b.Filled?1:0));
                Console.WriteLine("dumped "+info.Name+" seed "+live.Seed);
            }
            return;
        }
        if(Environment.GetEnvironmentVariable("STREETS_SPOTS")!=null)
        {
            foreach(var live in HubZFightChecks.Worlds(root))
            {
                var world=live.World;var info=NativeCityLayout.Plans[live.Plan];uint seed=world.Blueprint.worldSeed;
                var bleedPlan=NativeCityBleedDress.ForWalk(world.City,world.Bleed,seed);var dress=HubZFightChecks.Dress(live,null,true);
                var plan=NativeMeridianStreetLife.Build(NativeMeridianStreetLife.InputOf(world.City,info.Seed,seed,world.HallSlots,dress,world.Bleed,bleedPlan));
                // the 16 m cells within 150 m of the camp with the most non-floor pieces, by district
                foreach(char d in "tsmrwc")
                {
                    var cells=plan.Pieces.Where(p=>p.District==d&&NativeMeridianStreetLife.Kinds[p.Kind].Mount!=NativeMeridianStreetLife.Mount.Floor&&p.At.x*p.At.x+p.At.z*p.At.z<150*150)
                        .GroupBy(p=>((int)Math.Floor(p.At.x/16),(int)Math.Floor(p.At.z/16),(int)Math.Round(p.At.y))).OrderByDescending(g=>g.Count()).Take(2);
                    foreach(var g in cells)Console.WriteLine(FormattableString.Invariant($"{info.Name} seed {live.Seed} district {d}: cell ({g.Key.Item1*16+8},{g.Key.Item3},{g.Key.Item2*16+8}) {g.Count()} pieces: ")+string.Join(", ",g.GroupBy(p=>NativeMeridianStreetLife.Kinds[p.Kind].Key).Select(k=>k.Key+" "+k.Count())));
                }
            }
            return;
        }
        for(int li=0;li<NativeCityLayout.Plans.Count;li++)
        {
            var (walk,input,dress,_)=Walk(li,77);var sw=Stopwatch.StartNew();var plan=NativeMeridianStreetLife.Build(input);sw.Stop();
            var byKind=plan.Pieces.GroupBy(p=>NativeMeridianStreetLife.Kinds[p.Kind].Key).OrderByDescending(g=>g.Count()).Select(g=>g.Key+" "+g.Count());
            Console.WriteLine(FormattableString.Invariant($"{NativeCityLayout.Plans[li].Name}: {plan.Pieces.Count} pieces in {sw.ElapsedMilliseconds} ms; bays {plan.Bays}, floor cells {plan.FloorCells}, spans {plan.Spans}, kept out {plan.KeptOut}"));
            foreach(NativeMeridianStreetLife.Mount m in Enum.GetValues(typeof(NativeMeridianStreetLife.Mount)))Console.Write("  "+m+" "+plan.Count(m));
            Console.WriteLine();Console.WriteLine("  "+string.Join(", ",byKind));
            Console.WriteLine("  refused: "+string.Join(", ",NativeMeridianStreetLife.Refusals.Select((r,i)=>r+" "+plan.Refused[i])));
            sw.Restart();var props=NativeMeridianStreetLife.ProposeProps(input);sw.Stop();
            Console.WriteLine(FormattableString.Invariant($"  props: {props.Count} candidates ({props.Count(b=>b.Filled)} filled) in {sw.ElapsedMilliseconds} ms: ")+string.Join(", ",props.GroupBy(b=>b.Stem).OrderByDescending(g=>g.Count()).Select(g=>g.Key+" "+g.Count())));
        }
    }
}
