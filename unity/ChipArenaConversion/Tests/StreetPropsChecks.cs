using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

// Meridian's solid street props, the contract for Astra (Core NativeMeridianStreetProps; NativeMeridianStreetLife.ProposeProps;
// docs/expedition/MERIDIAN-STREETS.md part 2): the table, and every layout's candidates, all filled (the worst case: any run's
// subset keeps every rule), measured by this check's own geometry: each block stands on a street's floor and enters no block or
// other prop; it keeps out of the camp and its gates' approaches, the caches, the doors, the routes' clear lanes and junctions and
// the flights; it stands flush to a wall or a prop or a pilot's width from it (no slot to stick in); the horde's lattice built
// with every block keeps every node it reached, but those under the blocks; the live deals' encounter exits keep 3.5 m from every
// block (so they do not move). With the props' bank (CHIPARENA_STREETPROPS_BANK) every model fills its block within 0.25 m and
// stands no more than 0.25 m out of it below 3.1 m.
internal static class StreetPropsChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Street props: "+what);}

    public static void Run(string root)
    {
        count=0;var S=NativeMeridianStreetProps.Stems;
        // ---- the table ----
        Check(S.Select(s=>s.Key).Distinct().Count()==S.Length,"the stems are distinct");
        var models=S.SelectMany(s=>s.Models).ToList();Check(models.Distinct().Count()==models.Count,"each model dresses one stem");
        foreach(var s in S)
        {
            Check(s.Size.x>0&&s.Size.y>0&&s.Size.z>0&&s.Fill>0&&s.Fill<=1&&s.Models.Length>0,s.Key+": a block, a fill and a model");
            Check(s.Key=="lamp"||s.Size.y<=NativeMeridianStreetLife.Clear,s.Key+": under the clear height (only the lamp post rises over it)");
        }
        foreach(var key in new[]{"car","wreck","bus","bench","dumpster","barricade","barrier","police","newsstand","shelter","planter","bollard","hydrant","lamp"})Check(NativeMeridianStreetProps.StemOf(key)!=null,"the table has the "+key);
        var report=new List<string>();var exits=Exits(root);
        for(int li=0;li<NativeCityLayout.Plans.Count;li++)
        {
            var name=NativeCityLayout.Plans[li].Name;var (walk,input,_,_)=StreetLifeChecks.Walk(li,77);
            var props=NativeMeridianStreetLife.ProposeProps(input);
            // determinism: the same candidates for any run of the layout; the run fills its own share
            var again=NativeMeridianStreetLife.ProposeProps(StreetLifeChecks.Walk(li,77).input);
            Check(Same(props,again,true),name+": deterministic");
            var other=NativeMeridianStreetLife.ProposeProps(StreetLifeChecks.Walk(li,991,false).input);var bare=NativeMeridianStreetLife.ProposeProps(StreetLifeChecks.Walk(li,77,false).input);
            Check(Same(bare,other,false),name+": the candidates are the layout's (a run changes only which are filled)");
            Check(props.Count>=200&&props.Count<=1000,name+": "+props.Count+" candidates");
            Rules(name,walk.Map,input,props);
            Lattice(name,walk.Map,props);
            // the encounter exits: the station's picker runs on the map with the props, so an exit may shift a node; every one of a live
            // deal's exits keeps an open apron (3 m clear of every prop) within 8 m of where it stood, so no spawn is pushed away
            if(exits.TryGetValue(li,out var ex))
            {
                var nodes=NativeMeridianNavigation.Build(walk.Map).nodes.Select(n=>new V3(n.pos.x,n.pos.y-.9f,n.pos.z)).ToList();int moved=0;
                foreach(var e in ex)
                {
                    bool Open(V3 f)=>props.All(b=>Dist(b.Lo,b.Hi,f)>=3f||Math.Abs(b.Lo.y-f.y)>3f);
                    if(Open(e))continue;moved++;
                    Check(nodes.Any(f=>Math.Abs(f.y-e.y)<.6f&&(f.x-e.x)*(f.x-e.x)+(f.z-e.z)*(f.z-e.z)<64f&&Open(f)),name+FormattableString.Invariant($": the exit at ({e.x:F0},{e.y:F0},{e.z:F0}) keeps no open apron within 8 m"));
                }
                Console.WriteLine("  "+name+": "+ex.Count+" live exits, "+moved+" would shift a node or two");
            }
            int filled=props.Count(b=>b.Filled);
            report.Add(FormattableString.Invariant($"{name}: {props.Count} candidates, {filled} filled this run: ")+string.Join(", ",props.GroupBy(b=>b.Stem).OrderByDescending(g=>g.Count()).Select(g=>g.Key+" "+g.Count())));
        }
        var bankPath=Environment.GetEnvironmentVariable("CHIPARENA_STREETPROPS_BANK");
        if(!string.IsNullOrEmpty(bankPath)&&File.Exists(bankPath))Models(NativePackBank.Read(File.ReadAllBytes(bankPath)));
        foreach(var line in report)Console.WriteLine("  "+line);
        Console.WriteLine("PASS meridian street props (the contract's proposal): "+count+" checks over the five layouts"+(string.IsNullOrEmpty(bankPath)?" (no bank: the models' fit needs CHIPARENA_STREETPROPS_BANK)":" with the bank"));
    }
    private static bool Same(List<NativeMeridianStreetProps.Block> a,List<NativeMeridianStreetProps.Block> b,bool fill)
    {
        if(a.Count!=b.Count)return false;
        for(int i=0;i<a.Count;i++){var x=a[i];var y=b[i];if(x.Stem!=y.Stem||x.Model!=y.Model||(x.Pos-y.Pos).Length>1e-4f||(x.Size-y.Size).Length>1e-4f||fill&&x.Filled!=y.Filled)return false;}
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // The contract's rules on every candidate
    // ------------------------------------------------------------------------------------------------------------------
    private static void Rules(string name,ContentMap map,NativeMeridianStreetLife.Input input,List<NativeMeridianStreetProps.Block> props)
    {
        var S=new NativeMeridianDress.Solids(map.blocks);var B=S.B;var lots=NativeMeridianDress.Lots(input.Layout);
        foreach(var b in props)
        {
            var lo=b.Lo;var hi=b.Hi;string what=name+": "+b.Id+" ("+b.Model+")"+FormattableString.Invariant($" at ({b.Pos.x:F1},{lo.y:F1},{b.Pos.z:F1})");
            var st=NativeMeridianStreetProps.StemOf(b.Stem);var want=NativeMeridianStreetProps.WorldSize(st,b.Yaw);
            Check((want-b.Size).Length<1e-4f&&Math.Abs(Math.Sin(b.Yaw*2))<1e-4f,what+": its block is its stem's, turned in quarter turns");
            // its floor: the whole footprint on a terrace's top (a bridge's for the small ones), nothing standing there
            for(int i=0;i<=4;i++)for(int j=0;j<=4;j++)
            {
                float x=lo.x+(hi.x-lo.x)*(.02f+.96f*i/4),z=lo.z+(hi.z-lo.z)*(.02f+.96f*j/4);
                Check(Math.Abs(S.Floor(x,z,lo.y+.02f)-lo.y)<.012f,what+": not on a floor all over");
            }
            // nothing in its box, and no other prop
            Check(Clear(S,lo+new V3(.02f,.02f,.02f),hi-new V3(.02f,.02f,.02f)),what+": enters a block");
            foreach(var o in props){if(o.Id==b.Id)continue;Check(!(Math.Min(hi.x,o.Hi.x)-Math.Max(lo.x,o.Lo.x)>.01f&&Math.Min(hi.z,o.Hi.z)-Math.Max(lo.z,o.Lo.z)>.01f&&Math.Min(hi.y,o.Hi.y)-Math.Max(lo.y,o.Lo.y)>.01f),what+": overlaps "+o.Id);}
            // the camp and its gates' approaches, the caches (and a metre), the doors
            Check(!(lo.x<26f&&hi.x>-26f&&lo.z<26f&&hi.z>-26f),what+": in the camp");
            foreach(var g in new[]{new V3(0,0,25),new V3(25,0,0),new V3(0,0,-25),new V3(-25,0,0)})Check(Dist(lo,hi,g)>=NativeMeridianStreetProps.Gate||Math.Abs(lo.y)>=1f,what+": in a gate's approach");
            var m=new V3(NativeMeridianStreetProps.CacheMargin,0,NativeMeridianStreetProps.CacheMargin);
            foreach(var ko in input.KeepOut)Check(!ko.Hits(lo-m,hi+m),what+": by a cache");
            foreach(var r in input.Routes){var d=r.Points[r.Points.Length-1];Check(Dist(lo,hi,d)>=NativeMeridianStreetProps.Door||Math.Abs(lo.y-d.y)>=8f,what+": in a door's front");}
            // the routes' clear lanes and junctions
            foreach(var r in input.Routes)for(int i=0;i+1<r.Points.Length;i++)
            {
                var a=r.Points[i];var c=r.Points[i+1];var ab=new V3(c.x-a.x,0,c.z-a.z);float len=ab.Length;if(len<.01f)continue;var al=ab/len;var ac=new V3(al.z,0,-al.x);
                float w=r.Widths!=null&&i<r.Widths.Length?r.Widths[i]:10;float half=Math.Max(NativeMeridianStreetProps.LaneHalf,w*.5f-NativeMeridianStreetProps.KerbBand);
                // the lane: sample its rectangle every 0.5 m; no sample inside the block (at the leg's height)
                float y0=Math.Min(a.y,c.y)-2f,y1=Math.Max(a.y,c.y)+3.2f;
                if(hi.y>y0&&lo.y<y1)for(float s=-1f;s<=len+1f;s+=.5f)for(float u=-half;u<=half;u+=.5f)
                {var q=a+al*s+ac*u;Check(!(q.x>lo.x+.01f&&q.x<hi.x-.01f&&q.z>lo.z+.01f&&q.z<hi.z-.01f),what+": in "+r.Name+"'s clear lane");}
                if(b.Stem!="bollard"&&b.Stem!="hydrant")foreach(var q in new[]{a,c})Check(Dist(lo,hi,q)>=NativeMeridianStreetProps.Junction-.01f||Math.Abs(lo.y-q.y)>=3f,what+": at "+r.Name+"'s junction");
            }
            // the flights: 4 m from every ramp's footprint
            foreach(var r in map.ramps)
            {
                var l=r.low;var h=r.high;float wx=l.x==h.x?r.width*.5f:0,wz=l.z==h.z?r.width*.5f:0;
                float fx0=Math.Min(l.x,h.x)-wx-NativeMeridianStreetProps.Flight,fx1=Math.Max(l.x,h.x)+wx+NativeMeridianStreetProps.Flight,fz0=Math.Min(l.z,h.z)-wz-NativeMeridianStreetProps.Flight,fz1=Math.Max(l.z,h.z)+wz+NativeMeridianStreetProps.Flight;
                Check(!(hi.x>fx0&&lo.x<fx1&&hi.z>fz0&&lo.z<fz1&&hi.y>r.bottom&&lo.y<h.y+.3f),what+": by the flight "+r.id);
            }
            // the gap rule on each side: the nearest wall or prop flush (5 cm) or a pilot's width away (1 m)
            for(int side=0;side<4;side++)
            {
                float gap=Gap(S,props,b,side);
                Check(gap<=NativeMeridianStreetProps.Flush+1e-3f||gap>=NativeMeridianStreetProps.Pass-1e-3f,what+FormattableString.Invariant($": a {gap:F2} m slot beside it (side {side})"));
            }
        }
    }
    private static float Dist(V3 lo,V3 hi,V3 p){float dx=Math.Max(0,Math.Max(lo.x-p.x,p.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-p.z,p.z-hi.z));return (float)Math.Sqrt(dx*dx+dz*dz);}
    private static bool Clear(NativeMeridianDress.Solids S,V3 lo,V3 hi){var B=S.B;foreach(int i in S.Near(lo.x,hi.x,lo.z,hi.z)){int o=i*6;if(hi.x>B[o]&&lo.x<B[o+1]&&hi.y>B[o+2]&&lo.y<B[o+3]&&hi.z>B[o+4]&&lo.z<B[o+5])return false;}return true;}
    // The nearest wall or prop beside a side, within 1.2 m (and at the block's height: 0.1-1.8 m over its floor).
    private static float Gap(NativeMeridianDress.Solids S,List<NativeMeridianStreetProps.Block> props,NativeMeridianStreetProps.Block b,int side)
    {
        var lo=b.Lo;var hi=b.Hi;float y0=lo.y+.1f,y1=Math.Min(hi.y,lo.y+1.8f);float best=99;
        void Try(V3 a,V3 c)
        {
            if(c.y<=y0||a.y>=y1)return;
            float d=side==0?a.x-hi.x:side==1?lo.x-c.x:side==2?a.z-hi.z:lo.z-c.z;
            bool across=side<2?c.z>lo.z+.05f&&a.z<hi.z-.05f:c.x>lo.x+.05f&&a.x<hi.x-.05f;
            if(across&&d>-.005f&&d<best)best=Math.Max(0,d);
        }
        var B=S.B;foreach(int i in S.Near(lo.x-1.3f,hi.x+1.3f,lo.z-1.3f,hi.z+1.3f)){int o=i*6;Try(new V3(B[o],B[o+2],B[o+4]),new V3(B[o+1],B[o+3],B[o+5]));}
        foreach(var o in props)if(o.Id!=b.Id)Try(o.Lo,o.Hi);
        return best;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // The horde: the lattice (Core NativeMeridianNavigation, the Hulk's radius and height) built with every candidate keeps every
    // node it reached without them, but the nodes under them; a prop's top is never a street (as the fire escapes' and the camp's)
    // ------------------------------------------------------------------------------------------------------------------
    private static void Lattice(string name,ContentMap map,List<NativeMeridianStreetProps.Block> props)
    {
        var before=NativeMeridianNavigation.Build(map);
        var with=new ContentMap{id=map.id,bounds=map.bounds,ramps=map.ramps,blocks=map.blocks.Concat(props.Select(b=>{var c=NativeMeridianStreetProps.ToBlock(b);c.id="fire-escape-"+c.id;return c;})).ToArray()};
        var after=NativeMeridianNavigation.Build(with);
        var cut=NativeMeridianStreetLife.Cut(before,after,props);int lost=cut.Count,under=0;
        {
            var have=new HashSet<(int,int,int)>(after.nodes.Select(n=>((int)Math.Round(n.pos.x),(int)Math.Round(n.pos.y*4),(int)Math.Round(n.pos.z))));
            foreach(var n in before.nodes)if(!have.Contains(((int)Math.Round(n.pos.x),(int)Math.Round(n.pos.y*4),(int)Math.Round(n.pos.z))))under++;
            under-=lost;
            if(Environment.GetEnvironmentVariable("STREETPROPS_DEBUG")!=null)foreach(var f in cut){var nb=props.OrderBy(b=>Dist(b.Lo,b.Hi,f)).First();Console.WriteLine(FormattableString.Invariant($"    lost node ({f.x:F0},{f.y:F1},{f.z:F0}); nearest prop {nb.Id} at {Dist(nb.Lo,nb.Hi,f):F2} m"));}
        }
        Check(lost==0,name+": the horde's lattice loses "+lost+" nodes no prop stands over (a path cut)");
        Console.WriteLine(FormattableString.Invariant($"  {name}: the horde's lattice {before.nodes.Length} nodes, {after.nodes.Length} with every prop ({under} under them)"));
    }

    /// <summary>Each layout's live encounter exits (the station's own path on one deal per layout: its generated world's spawns).</summary>
    private static Dictionary<int,List<V3>> Exits(string root)
    {
        var o=new Dictionary<int,List<V3>>();
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var l=new List<V3>();foreach(var s in live.World.Area.spawns)if(s.entry!=null)l.Add(new V3(s.entry.@exit.x,s.entry.@exit.y,s.entry.@exit.z));o[live.Plan]=l;
        }
        return o;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // The models in their blocks (the props' bank)
    // ------------------------------------------------------------------------------------------------------------------
    private static void Models(NativePackBank.Bank bank)
    {
        foreach(var st in NativeMeridianStreetProps.Stems)foreach(var m in st.Models)foreach(int t in new[]{0,2})
        {
            var prop=bank.Find("rig/props/"+m+"/"+t);Check(prop!=null&&prop.Parts.Length>0,"the bank carries "+m+" at tier "+t);
            float hx=st.Size.x*.5f,hz=st.Size.z*.5f;float x0=1e9f,x1=-1e9f,z0=1e9f,z1=-1e9f,top=-1e9f;
            foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
            {
                x0=Math.Min(x0,v.X);x1=Math.Max(x1,v.X);z0=Math.Min(z0,v.Z);z1=Math.Max(z1,v.Z);top=Math.Max(top,v.Y);
                Check(v.Y>=-.011f,m+": under its floor");
                if(v.Y<NativeMeridianStreetLife.Clear)Check(Math.Abs(v.X)<=hx+.25f+1e-3f&&Math.Abs(v.Z)<=hz+.25f+1e-3f,m+" tier "+t+": more than 0.25 m out of its block below 3.1 m");
            }
            Check(x0<=-hx+.25f&&x1>=hx-.25f&&z0<=-hz+.25f&&z1>=hz-.25f&&top>=st.Size.y-.25f,m+" tier "+t+FormattableString.Invariant($": fills its block within 0.25 m (x {x0:F2}..{x1:F2}, z {z0:F2}..{z1:F2}, top {top:F2})"));
        }
    }
}
