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
        private NativeNavSearch navSearch;private Func<MapNavLink,bool> navActive;private Func<MapNavNode,bool> navUsable;private Vector3 navOrigin;
        public double NavigationMs {get;private set;}public int NavigationSearches {get;private set;}
        private Vector3 TimedStructureNavMove(int actorId,Vector3 pos,Vector3 target,Vector3 fallback)
        {
            long at=System.Diagnostics.Stopwatch.GetTimestamp();
            try{return StructureNavMoveCore(actorId,pos,target,fallback);}
            finally{NavigationMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-at)*1000d/System.Diagnostics.Stopwatch.Frequency;}
        }
        private Vector3 StructureNavMove(NativePilot pilot,Vector3 target,Vector3 fallback)
        =>StructureNavMove(pilot.Seat.id,pilot.Body.position,target,fallback);
        private Vector3 StructureNavMove(int actorId,Vector3 pos,Vector3 target,Vector3 fallback)=>TimedStructureNavMove(actorId,pos,target,fallback);
        private Vector3 StructureNavMoveCore(int actorId,Vector3 pos,Vector3 target,Vector3 fallback)
        {
            if(Map.nav==null)return fallback;
            if(navMap!=MapId){navMap=MapId;navSearch=new NativeNavSearch(Map.nav);navActive=NavLinkActive;navUsable=NavNodeUsable;navNodes=Map.nav.nodes.ToDictionary(n=>n.id);navEdges=Map.nav.nodes.ToDictionary(n=>n.id,n=>new List<MapNavLink>());foreach(var l in Map.nav.links){navEdges[l.a].Add(l);if(l.kind!="drop")navEdges[l.b].Add(l);}}
            if(Map.hazards!=null&&UnsafeStagePath(pos,pos))
            {var pocket=Map.nav.nodes.Where(n=>n.safe&&Math.Abs(n.pos.y-pos.y)<1.2&&!SceneLinecast(pos,Vec(n.pos),WorldMask)).OrderBy(n=>(Vec(n.pos)-pos).sqrMagnitude).FirstOrDefault();if(pocket!=null)return (Vec(pocket.pos)-pos).magnitude<.25f?Vector3.zero:new Vector3(pocket.pos.x-pos.x,0,pocket.pos.z-pos.z).normalized;}
            if(UnsafeStructure(pos)||UnsafeStagePath(pos,pos))
            {var safe=Map.nav.nodes.Where(n=>Math.Abs(n.pos.y-pos.y)<1.2&&!UnsafeStructure(Vec(n.pos))&&!UnsafeStagePath(Vec(n.pos),Vec(n.pos))&&!SceneLinecast(pos,Vec(n.pos),WorldMask)).OrderBy(n=>(Vec(n.pos)-pos).sqrMagnitude).FirstOrDefault();if(safe!=null)return new Vector3(safe.pos.x-pos.x,0,safe.pos.z-pos.z).normalized;}
            if(Map.hazards==null&&Math.Abs(target.y-pos.y)<1.1&&!SceneLinecast(pos,target,WorldMask)&&!UnsafeStructurePath(pos,target))return fallback;
            if(!navRoutes.TryGetValue(actorId,out var route)||Time-route.at>.7f||route.revision!=NavigationRevision||(route.target-target).sqrMagnitude>9)
            {
                var start=NearestNavNode(pos);var end=NearestNavNode(target);var path=route.path??new List<string>();
                path.Clear();NavigationSearches++;
                if(start!=null&&end!=null)navSearch.Find(start.id,end.id,navActive,path);
                route=(Time,NavigationRevision,target,path,0);navRoutes[actorId]=route;
            }
            if(route.path.Count==0)return Map.hazards!=null||UnsafeStructurePath(pos,pos+fallback*2)?Vector3.zero:fallback;
            while(route.step<route.path.Count-1&&(Vec(navNodes[route.path[route.step]].pos)-pos).magnitude<.65f)route.step++;
            if(route.step>0&&!navEdges[route.path[route.step-1]].Any(l=>(l.a==route.path[route.step]||l.b==route.path[route.step])&&NavLinkActive(l))){navRoutes.Remove(actorId);return Vector3.zero;}
            navRoutes[actorId]=route;var waypoint=Vec(navNodes[route.path[route.step]].pos);var delta=waypoint-pos;return new Vector3(delta.x,0,delta.z).normalized;
        }
        private MapNavNode NearestNavNode(Vector3 point)
        {navOrigin=point;return navSearch.Nearest(Vec(point),navUsable);}
        private bool NavNodeUsable(MapNavNode node)
        {
            bool enabled=false;foreach(var edge in navEdges[node.id])if(NavLinkEnabled(edge)){enabled=true;break;}
            var at=Vec(node.pos);return enabled&&!UnsafeStructure(at)&&!UnsafeStagePath(at,at)&&!SceneLinecast(navOrigin,at,WorldMask);
        }
        private bool UnsafeStructurePath(Vector3 a,Vector3 b)
        {
            if(UnsafeStagePath(a,b))return true;
            foreach(var segment in Map.segments)
            {int state=Structures.Get(segment.id).state;if(state!=1&&state!=2)continue;foreach(var volume in segment.sweeps)if(NativeStructureRules.Crosses(Vec(a),Vec(b),volume,.5f))return true;}return false;
        }
        private bool NavLinkEnabled(MapNavLink l)
        {
            if(!StageLinkOpen(l))return false;
            foreach(var id in l.requires??Array.Empty<string>())if(Structures.Get(id).state!=0)return false;
            foreach(var id in l.after??Array.Empty<string>())if(Structures.Get(id).state!=3)return false;
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
