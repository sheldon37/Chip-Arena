using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The machines in the generated instances (docs/EXPEDITION-INSTANCE-MACHINES.md): every seed gets all three kinds, each with its back to a real wall, its front and dispensers on floor a pilot can stand on, reachable by the level's own routes, the collider clear of the nav nodes a
    /// zombie walks to, the spawn anchors keeping away, and the same from the same seed.
    /// </summary>
    internal static class InstanceMachineChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Instance machines: "+what);count++;}
        public static void Run()
        {
            count=0;
            for(int seed=1;seed<=24;seed++){Backrooms(seed*7919);Poolrooms(seed*7919);}
            Check(NativeBackroomsGenerator.Build(5,false).Machines.Length==0&&NativePoolroomsGenerator.Build(5,false).Machines.Length==0,"the PvP arenas stay as they were");
            foreach(var (id,build,budget) in NativeInstanceMachineDress.All)
            {
                var m=build();Check(m.Triangles>0&&m.Triangles<=budget,id+" budget ("+m.Triangles+" of "+budget+")");var again=build();Check(m.Triangles==again.Triangles,id+" builds the same");
                foreach(var section in m.Sections)
                {
                    Check(section.Positions.All(q=>float.IsFinite(q.X)&&float.IsFinite(q.Y)&&float.IsFinite(q.Z))&&section.Indices.Count%3==0,id+" clean");
                    // Flat on the floor or up on the wall: nothing a pilot's capsule would pass through at a height it could be seen doing so, and nothing through the wall behind.
                    Check(section.Positions.All(q=>q.Z>=-.72f),id+" stays in front of its wall");
                    Check(section.Positions.All(q=>q.Y<3.05f),id+" under the lowest ceiling");
                }
            }
            Check(NativeInstanceMachineDress.For(NativeBackroomsGenerator.InstanceMap)==NativeInstanceMachineDress.BackroomsId&&NativeInstanceMachineDress.For(NativePoolroomsGenerator.InstanceMap)==NativeInstanceMachineDress.PoolroomsId&&NativeInstanceMachineDress.For("expedition-ward-zero")==null,"each level its own, the authored ones none");
            Console.WriteLine("PASS Instance machines: "+count+" checks (24 seeds of the Backrooms and the Poolrooms: all three kinds, backs to real walls, standable fronts, the routes and spawns clear, deterministic)");
        }
        private struct Box{public float MinX,MaxX,MinZ,MaxZ;public bool Hits(float x,float z,float pad)=>x>=MinX-pad&&x<=MaxX+pad&&z>=MinZ-pad&&z<=MaxZ+pad;}
        // The hidden collider the authority builds for a machine (NativeExpeditionLayout.BindContent).
        private static Box Solid(PveMachine m,ExpeditionAreaLayout area)
        {
            var data=new NativePveContent();NativeExpeditionLayout.BindContent(area,data);var b=data.machineBlocks[m.m];float yaw=m.yaw,cx=b.pos.x,cz=b.pos.z;
            bool square=Math.Abs(Math.Sin(yaw*2))<1e-3;Check(square,"a machine stands square to its walls");bool alongX=Math.Abs(Math.Cos(yaw))>.5f;float hx=(alongX?b.size.x:b.size.z)/2,hz=(alongX?b.size.z:b.size.x)/2;
            return new Box{MinX=cx-hx,MaxX=cx+hx,MinZ=cz-hz,MaxZ=cz+hz};
        }
        private static void Common(string name,int seed,ExpeditionAreaLayout area,ContentMap map,V3 door,V3 exit,Func<V3,bool> standable)
        {
            string t=name+" seed "+seed+": ";var machines=area.machines;
            Check(machines.Any(m=>m.kind=="chip")&&machines.Any(m=>m.kind=="recruit")&&machines.Any(m=>m.kind=="med"),t+"chip, recruit and mending machines ("+string.Join(",",machines.Select(m=>m.kind))+")");
            Check(machines.Length>=3&&machines.Length<=16,t+"a sane count");
            Check(machines.Select(m=>m.m).SequenceEqual(Enumerable.Range(0,machines.Length))&&machines.Select(m=>m.id).Distinct().Count()==machines.Length,t+"indexed as the layout parser requires");
            foreach(var a in machines)foreach(var b in machines)if(a!=b){float dx=a.x-b.x,dz=a.z-b.z;Check(dx*dx+dz*dz>=NativeInstanceMachines.Apart*NativeInstanceMachines.Apart*.8f,t+"spread along the level");}
            var nodes=map.nav.nodes;
            foreach(var m in machines)
            {
                var box=Solid(m,area);
                Check(standable(m.front.Position())&&standable(m.dispense.Position())&&standable(m.chipDrop.Position()),t+m.id+" front, dispenser and drop on open floor");
                float d=0;d=(float)Math.Sqrt((m.x-door.x)*(m.x-door.x)+(m.z-door.z)*(m.z-door.z));Check(d>=NativeInstanceMachines.FromDoor-1.5f,t+m.id+" not on top of the way in ("+d+")");
                Check(!box.Hits(door.x,door.z,.6f)&&!box.Hits(exit.x,exit.z,.6f),t+m.id+" clear of the door and the exit");
                Check(!box.Hits(m.front.x,m.front.z,.44f),t+m.id+" a pilot fits at its front");
                // No nav node for a zombie to walk into: every one is a capsule clear of the block.
                foreach(var n in nodes)Check(!box.Hits(n.pos.x,n.pos.z,.42f),t+m.id+" clear of nav node "+n.id);
                foreach(var s in area.spawns){var e=s.entry.@exit;Check(!box.Hits(e.x,e.z,.5f),t+m.id+" clear of spawn "+s.entry.id);}
                foreach(var s in area.doors.SelectMany(x=>x.entries))Check(!box.Hits(s.x,s.z,.6f),t+m.id+" clear of a party slot");
            }
        }
        private static void Backrooms(int seed)
        {
            var g=NativeBackroomsGenerator.Build(seed,true);var again=NativeBackroomsGenerator.Build(seed,true);var l=g.Layout;
            Check(g.Machines.Select(m=>m.Cell+":"+m.Dx+":"+m.Dz+":"+m.Kind).SequenceEqual(again.Machines.Select(m=>m.Cell+":"+m.Dx+":"+m.Dz+":"+m.Kind))&&g.Identity==again.Identity,"the same seed, the same machines");
            var area=NativeBackroomsExpedition.Area(g);
            foreach(var m in g.Machines){int x=m.Cell%l.Width,z=m.Cell/l.Width;Check(l.Between(x,z,m.Dx,m.Dz)==BackroomsLayout.Wall,"Backrooms: a whole wall behind it");Check(NativeBackroomsGenerator.Link(l,m.Cell,-m.Dx,-m.Dz),"Backrooms: open in front of it");}
            bool Standable(V3 p){int x=(int)Math.Floor(p.x/BackroomsLayout.Cell),z=(int)Math.Floor(p.z/BackroomsLayout.Cell);return l.Inside(x,z)&&NativeBackroomsGenerator.Walkable(l,l.Index(x,z));}
            Common("Backrooms",seed,area,g.Map,g.Entry,g.Exit,Standable);
        }
        private static void Poolrooms(int seed)
        {
            var g=NativePoolroomsGenerator.Build(seed,true);var again=NativePoolroomsGenerator.Build(seed,true);var l=g.Layout;
            Check(g.Machines.Select(m=>m.Cell+":"+m.Dx+":"+m.Dz+":"+m.Kind).SequenceEqual(again.Machines.Select(m=>m.Cell+":"+m.Dx+":"+m.Dz+":"+m.Kind))&&g.Identity==again.Identity,"the same seed, the same machines");
            var area=NativePoolroomsExpedition.Area(g);
            foreach(var m in g.Machines){int x=m.Cell%l.Width,z=m.Cell/l.Width;Check(l.Between(x,z,m.Dx,m.Dz)==PoolroomsLayout.Wall,"Poolrooms: a whole wall behind it");Check(l.Floor[m.Cell]==PoolroomsLayout.Deck&&l.Floor[l.Index(x-m.Dx,z-m.Dz)]==PoolroomsLayout.Deck,"Poolrooms: on the deck, deck in front");}
            // Taking the machine's cell off the navigation leaves every other node reached from the party's cell.
            var adj=new Dictionary<string,List<string>>();foreach(var n in g.Map.nav.nodes)adj[n.id]=new List<string>();foreach(var k in g.Map.nav.links){Check(adj.ContainsKey(k.a)&&adj.ContainsKey(k.b),"Poolrooms: every link has both its nodes");adj[k.a].Add(k.b);adj[k.b].Add(k.a);}
            var start="pool:"+g.Players[0]+":0";Check(adj.ContainsKey(start),"Poolrooms: the party's cell is on the navigation");var seen=new HashSet<string>{start};var q=new Queue<string>();q.Enqueue(start);while(q.Count>0)foreach(var o in adj[q.Dequeue()])if(seen.Add(o))q.Enqueue(o);
            foreach(int c in g.Enemies.Concat(new[]{g.ExitCell}))Check(seen.Contains("pool:"+c+":0"),"Poolrooms: enemies and the exit still reached");
            // And every spawn anchor the level has: the machines cost none of them their way to the party.
            foreach(var s in area.spawns){var e=s.entry.@exit;int cell=l.Index((int)Math.Floor(e.x/PoolroomsLayout.Cell),(int)Math.Floor(e.z/PoolroomsLayout.Cell));Check(!g.Machines.Any(m=>m.Cell==cell),"Poolrooms: no spawn anchor in a machine's cell");Check(seen.Contains("pool:"+cell+":0"),"Poolrooms: spawn "+s.entry.id+" still reaches the party");}
            bool Standable(V3 p){int x=(int)Math.Floor(p.x/PoolroomsLayout.Cell),z=(int)Math.Floor(p.z/PoolroomsLayout.Cell);return l.Inside(x,z)&&l.Floor[l.Index(x,z)]==PoolroomsLayout.Deck;}
            Common("Poolrooms",seed,area,g.Map,g.Entry,g.Exit,Standable);
        }
    }
}
