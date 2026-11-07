using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Globalization;
namespace ChipArena.UnityConversion
{
    internal static class BackroomsGeneratorChecks
    {
        public static void Run(string root)
        {
            int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Backrooms rules: "+why);}
            foreach(bool expedition in new[]{false,true})foreach(int seed in new[]{0,1,-1,17,647031,int.MinValue,int.MaxValue})
            {
                var g=NativeBackroomsGenerator.Build(seed,expedition);var l=g.Layout;NativeMapRules.Validate(g.Map);
                Check(l.Width==(expedition?40:16)&&l.Depth==l.Width,"requested dimensions");
                Check(g.Identity==NativeBackroomsGenerator.Build(seed,expedition).Identity,"repeat identity");
                var d=NativeBackroomsGenerator.Distances(l,l.Index(l.EntryX,l.EntryZ));
                Check(Enumerable.Range(0,d.Length).All(c=>!NativeBackroomsGenerator.Walkable(l,c)||d[c]>=0),"all playable cells connected");
                Check(g.Players.Length==8&&g.Players.Distinct().Count()==8&&g.Enemies.Length>0,"party and enemy slots");
                Check(g.Players.All(c=>d[c]>=0)&&g.Enemies.All(c=>d[c]>=0&&g.Players.All(p=>(NativeBackroomsGenerator.Position(l,c)-NativeBackroomsGenerator.Position(l,p)).Length>=12)),"reachable separated spawn sets");
                Check(d[l.Index(l.ExitX,l.ExitZ)]>20,"exit requires a maze route");
                Check(g.Map.rotation==!expedition&&NativeMapRules.ArenaSelectable(g.Map)==!expedition,"mode selection");
                // Independent box/circle collision probes at every cell centre and every navigation link.
                var collision=g.Map.blocks.AsEnumerable();
                if(expedition){var door=NativeBackroomsExpedition.Area(g).doors[0].closure;collision=collision.Concat(new[]{new ContentBlock{id="closed-return-door",pos=new V3(door.x,door.y,door.z),size=new V3(door.sx,door.sy,door.sz)}});}
                var obstacles=collision.ToArray();
                bool Clear(V3 p,float radius)
                {
                    foreach(var b in obstacles)
                    {
                        if(b.pos.y+b.size.y/2<=.01f||b.pos.y-b.size.y/2>=1.8f)continue;
                        float dx=Math.Max(0,Math.Abs(p.x-b.pos.x)-b.size.x/2),dz=Math.Max(0,Math.Abs(p.z-b.pos.z)-b.size.z/2);
                        if(dx*dx+dz*dz<radius*radius)return false;
                    }
                    return g.Map.blocks.Any(b=>b.id.Contains(":floor-")&&Math.Abs(p.x-b.pos.x)<=BackroomsLayout.Cell/2+.001f&&Math.Abs(p.z-b.pos.z)<=BackroomsLayout.Cell/2+.001f);
                }
                Check(g.Map.nav.nodes.All(n=>Clear(n.pos,.45f)),"navigation nodes clear for largest allowed enemy");
                var nodes=g.Map.nav.nodes.ToDictionary(n=>n.id);
                Check(g.Map.nav.links.All(e=>Enumerable.Range(0,7).All(k=>Clear(nodes[e.a].pos+(nodes[e.b].pos-nodes[e.a].pos)*(k/6f),.45f))),"navigation edges have collision clearance and floor");
                Check(g.Map.spawns.ffa.All(s=>Clear(s.pos,.45f)),"spawn collision clearance");
                if(!expedition)Check(!l.Floor.Contains(BackroomsLayout.Pit),"PvP has no pits");
                else Check(g.Map.blocks.Count(b=>b.id.Contains(":pit-bottom-"))==l.Floor.Count(f=>f==BackroomsLayout.Pit),"pit shafts match layout");
            }
            var arena=NativeBackroomsGenerator.Build(NativeBackroomsGenerator.ArenaSeed,false);
            var before=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("ar-SA");Check(arena.Identity==NativeBackroomsGenerator.Build(NativeBackroomsGenerator.ArenaSeed,false).Identity,"culture independent");}finally{CultureInfo.CurrentCulture=before;}
            Check(arena.Identity!=NativeBackroomsGenerator.Build(NativeBackroomsGenerator.ArenaSeed+1,false).Identity,"different seed changes layout");
            var generated=NativeBackroomsGenerator.Build(17,true);var area=NativeBackroomsExpedition.Area(generated);
            var layout=new ExpeditionLayout{world="city",instances=new[]{area.id},areas=new[]{area}};
            var options=new JsonSerializerOptions{IncludeFields=true};var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
            var run=NativeExpedition.Create("backrooms-objective","city","host","pistol",17,new[]{new ExpeditionInstance{id=area.id,target=1}});
            var session=new ExpeditionSession(run,content,_=>{});V3? at=generated.Exit;bool visible=true;
            var director=new ExpeditionDirector(session,layout,_=>at,(_,__)=>visible,10,20,30);
            Check(run.Enter(area.id),"enter maze");
            Check(!director.TryBackroomsExit(),"quota required");run.Progress("enemy-1",1);
            visible=false;Check(!director.TryBackroomsExit(),"wall blocks exit evidence");visible=true;
            at=generated.Entry;Check(!director.TryBackroomsExit(),"distance required");at=null;Check(!director.TryBackroomsExit(),"absent pilot cannot finish");at=generated.Exit;
            run.SetLoading(true);Check(!director.TryBackroomsExit(),"loading blocks objective");run.SetLoading(false);
            Check(director.TryBackroomsExit()&&!run.Snapshot().instances.Single().cleared&&run.Snapshot().rewards.Length==0,"arrival cannot pay or clear the exit");
            Check(director.BossDefeated(NativeBackroomsExpedition.Objective,generated.Exit),"boss death clears exit objective");
            int rewards=run.Snapshot().rewards.Length;Check(rewards>0&&!director.TryBackroomsExit()&&run.Snapshot().rewards.Length==rewards,"completion ledger pays once");
            Console.WriteLine("PASS Backrooms rules: "+checks+" checks (14 seed/mode layouts, collision routes, spawns, identity, objective ledger)");
        }
    }
}
