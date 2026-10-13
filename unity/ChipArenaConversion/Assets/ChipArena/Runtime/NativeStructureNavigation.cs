using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly Dictionary<int,(float at,int revision,Vector3 target,List<string> path,int step)> navRoutes=new Dictionary<int,(float,int,Vector3,List<string>,int)>();
        private Dictionary<string,MapNavNode> navNodes;
        private Dictionary<string,List<MapNavLink>> navEdges;
        private string navMap;
        private Vector3 StructureNavMove(NativePilot pilot,Vector3 target,Vector3 fallback)
        =>StructureNavMove(pilot.Seat.id,pilot.Body.position,target,fallback);
        private Vector3 StructureNavMove(int actorId,Vector3 pos,Vector3 target,Vector3 fallback)
        {
            if(Map.nav==null)return fallback;
            if(navMap!=MapId){navMap=MapId;navNodes=Map.nav.nodes.ToDictionary(n=>n.id);navEdges=Map.nav.nodes.ToDictionary(n=>n.id,n=>new List<MapNavLink>());foreach(var l in Map.nav.links){navEdges[l.a].Add(l);if(l.kind!="drop")navEdges[l.b].Add(l);}}
            if(Map.hazards!=null&&UnsafeStagePath(pos,pos))
            {var pocket=Map.nav.nodes.Where(n=>n.safe&&Math.Abs(n.pos.y-pos.y)<1.2&&!SceneLinecast(pos,Vec(n.pos),WorldMask)).OrderBy(n=>(Vec(n.pos)-pos).sqrMagnitude).FirstOrDefault();if(pocket!=null)return (Vec(pocket.pos)-pos).magnitude<.25f?Vector3.zero:new Vector3(pocket.pos.x-pos.x,0,pocket.pos.z-pos.z).normalized;}
            if(UnsafeStructure(pos)||UnsafeStagePath(pos,pos))
            {var safe=Map.nav.nodes.Where(n=>Math.Abs(n.pos.y-pos.y)<1.2&&!UnsafeStructure(Vec(n.pos))&&!UnsafeStagePath(Vec(n.pos),Vec(n.pos))&&!SceneLinecast(pos,Vec(n.pos),WorldMask)).OrderBy(n=>(Vec(n.pos)-pos).sqrMagnitude).FirstOrDefault();if(safe!=null)return new Vector3(safe.pos.x-pos.x,0,safe.pos.z-pos.z).normalized;}
            if(Map.hazards==null&&Math.Abs(target.y-pos.y)<1.1&&!SceneLinecast(pos,target,WorldMask)&&!UnsafeStructurePath(pos,target))return fallback;
            if(!navRoutes.TryGetValue(actorId,out var route)||Time-route.at>.7f||route.revision!=NavigationRevision||(route.target-target).sqrMagnitude>9)
            {
                MapNavNode Nearest(Vector3 p)=>Map.nav.nodes.OrderBy(n=>(Vec(n.pos)-p).sqrMagnitude).FirstOrDefault(n=>navEdges[n.id].Any(NavLinkEnabled)&&!UnsafeStructure(Vec(n.pos))&&!UnsafeStagePath(Vec(n.pos),Vec(n.pos))&&!SceneLinecast(p,Vec(n.pos),WorldMask));
                var start=Nearest(pos);var end=Nearest(target);var path=new List<string>();
                if(start!=null&&end!=null)
                {
                    var distance=new Dictionary<string,float>{{start.id,0}};var previous=new Dictionary<string,string>();var open=new List<string>{start.id};var closed=new HashSet<string>();
                    while(open.Count>0)
                    {
                        string at=open.OrderBy(id=>distance[id]+(Vec(navNodes[id].pos)-Vec(end.pos)).magnitude).ThenBy(id=>id,StringComparer.Ordinal).First();open.Remove(at);if(at==end.id)break;if(!closed.Add(at))continue;
                        foreach(var l in navEdges[at])
                        {
                            string next=l.a==at?l.b:l.a;if(closed.Contains(next)||!NavLinkActive(l))continue;float cost=distance[at]+(Vec(navNodes[next].pos)-Vec(navNodes[at].pos)).magnitude;
                            if(!distance.TryGetValue(next,out float old)||cost<old){distance[next]=cost;previous[next]=at;if(!open.Contains(next))open.Add(next);}
                        }
                    }
                    if(distance.ContainsKey(end.id)){string n=end.id;path.Add(n);while(n!=start.id){n=previous[n];path.Add(n);}path.Reverse();}
                }
                route=(Time,NavigationRevision,target,path,0);navRoutes[actorId]=route;
            }
            if(route.path.Count==0)return Map.hazards!=null||UnsafeStructurePath(pos,pos+fallback*2)?Vector3.zero:fallback;
            while(route.step<route.path.Count-1&&(Vec(navNodes[route.path[route.step]].pos)-pos).magnitude<.65f)route.step++;
            if(route.step>0&&!navEdges[route.path[route.step-1]].Any(l=>(l.a==route.path[route.step]||l.b==route.path[route.step])&&NavLinkActive(l))){navRoutes.Remove(actorId);return Vector3.zero;}
            navRoutes[actorId]=route;var waypoint=Vec(navNodes[route.path[route.step]].pos);var delta=waypoint-pos;return new Vector3(delta.x,0,delta.z).normalized;
        }
        private bool UnsafeStructurePath(Vector3 a,Vector3 b)=>UnsafeStagePath(a,b)||Map.segments.Any(s=>(Structures.Get(s.id).state==1||Structures.Get(s.id).state==2)&&s.sweeps.Any(v=>NativeStructureRules.Crosses(Vec(a),Vec(b),v,.5f)));
        private bool NavLinkEnabled(MapNavLink l)
        {
            if(!StageLinkOpen(l))return false;
            if((l.requires??Array.Empty<string>()).Any(s=>Structures.Get(s).state!=0)||(l.after??Array.Empty<string>()).Any(s=>Structures.Get(s).state!=3))return false;
            foreach(var d in Map.segments)
            {int state=Structures.Get(d.id).state;if(state!=0&&(d.nav?.remove??Array.Empty<string>()).Contains(l.id))return false;if((d.nav?.add??Array.Empty<string>()).Contains(l.id)&&state!=3)return false;}
            return true;
        }
        private bool NavLinkActive(MapNavLink l)
        {
            if(!NavLinkEnabled(l))return false;Vector3 a=Vec(navNodes[l.a].pos),b=Vec(navNodes[l.b].pos);if(UnsafeStructurePath(a,b))return false;
            if(l.kind=="stair"||l.kind=="ramp"||l.kind=="drop")return true;
            return !SceneLinecast(a,b,WorldMask);
        }
    }
}
