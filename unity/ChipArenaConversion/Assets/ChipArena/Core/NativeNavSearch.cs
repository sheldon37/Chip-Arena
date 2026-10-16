using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Reused by sequential authority searches. No per-expanded-node LINQ, collections or delegates.
    public sealed class NativeNavSearch
    {
        private readonly MapNavNode[] nodes;
        private readonly Dictionary<string,int> index=new Dictionary<string,int>();
        private readonly struct Edge
        {
            public readonly MapNavLink Link;public readonly int Next;public readonly float Cost;
            public Edge(MapNavLink link,int next,float cost){Link=link;Next=next;Cost=cost;}
        }
        private readonly List<Edge>[] edges,incoming;
        private readonly float[] distance,estimate;
        private readonly int[] previous,heap,position;
        private readonly bool[] closed;
        private int count;
        private int flowNode=-1,flowEdge;
        public bool FlowComplete=>count==0&&flowNode<0;
        private readonly int[] nearestOrder;private readonly float[] nearestDistance;private readonly IComparer<int> nearestComparer;
        private sealed class NearestComparer:IComparer<int>
        {
            private readonly float[] distances;public NearestComparer(float[] distances){this.distances=distances;}
            public int Compare(int a,int b){int order=distances[a].CompareTo(distances[b]);return order!=0?order:a.CompareTo(b);}
        }
        public MapNavNode Nearest(V3 point,Func<MapNavNode,bool> usable)
        {
            int closest=-1;float best=float.PositiveInfinity;
            for(int i=0;i<nodes.Length;i++){nearestOrder[i]=i;var d=nodes[i].pos-point;float distance=nearestDistance[i]=V3.Dot(d,d);if(distance<best){best=distance;closest=i;}}
            // Usually the closest floor node is usable. Avoid sorting the whole map
            // for every mob; retain the ordered fallback for obstructed candidates.
            if(closest>=0&&usable(nodes[closest]))return nodes[closest];
            Array.Sort(nearestOrder,nearestComparer);
            foreach(int i in nearestOrder)if(usable(nodes[i]))return nodes[i];return null;
        }
        public NativeNavSearch(MapNav graph)
        {
            nodes=graph.nodes;int n=nodes.Length;edges=new List<Edge>[n];incoming=new List<Edge>[n];
            nearestOrder=new int[n];nearestDistance=new float[n];nearestComparer=new NearestComparer(nearestDistance);
            distance=new float[n];estimate=new float[n];previous=new int[n];heap=new int[n];position=new int[n];closed=new bool[n];
            for(int i=0;i<n;i++){index.Add(nodes[i].id,i);edges[i]=new List<Edge>();incoming[i]=new List<Edge>();}
            foreach(var link in graph.links)
            {
                int a=index[link.a],b=index[link.b];float cost=(nodes[b].pos-nodes[a].pos).Length;
                edges[a].Add(new Edge(link,b,cost));incoming[b].Add(new Edge(link,a,cost));
                if(link.kind!="drop"){edges[b].Add(new Edge(link,a,cost));incoming[a].Add(new Edge(link,b,cost));}
            }
        }
        public void Find(string startId,string endId,Func<MapNavLink,bool> active,List<string> path)
        {
            path.Clear();int start=index[startId],end=index[endId];count=0;
            for(int i=0;i<nodes.Length;i++){distance[i]=float.PositiveInfinity;previous[i]=-1;position[i]=-1;closed[i]=false;estimate[i]=(nodes[i].pos-nodes[end].pos).Length;}
            distance[start]=0;PushOrDecrease(start);
            while(count>0)
            {
                int at=Pop();if(at==end){for(int p=end;p>=0;p=previous[p])path.Add(nodes[p].id);path.Reverse();return;}closed[at]=true;
                foreach(var edge in edges[at])
                {
                    int next=edge.Next;if(closed[next])continue;
                    float cost=distance[at]+edge.Cost;
                    // World/hazard queries are expensive. A non-improving edge cannot change the route.
                    if(cost>=distance[next]||!active(edge.Link))continue;distance[next]=cost;previous[next]=at;PushOrDecrease(next);
                }
            }
        }
        // Crowd routes share one reverse shortest-path tree per destination. Incoming
        // edges preserve one-way drops; previous[] points toward the destination.
        public void BuildFlow(string endId,Func<MapNavLink,bool> active)
        {
            BeginFlow(endId);while(!StepFlow(active,int.MaxValue)){}
        }
        public void BeginFlow(string endId)
        {
            int end=index[endId];count=0;flowNode=-1;flowEdge=0;
            for(int i=0;i<nodes.Length;i++){distance[i]=float.PositiveInfinity;estimate[i]=0;previous[i]=-1;position[i]=-1;closed[i]=false;}
            distance[end]=0;PushOrDecrease(end);
        }
        // Bound edge visits, including rejected edges. Call once per destination/tick,
        // not once per mob. Settled nodes can already provide shortest paths.
        public bool StepFlow(Func<MapNavLink,bool> active,int edgeBudget)
        {
            if(edgeBudget<1)throw new ArgumentOutOfRangeException(nameof(edgeBudget));
            while(flowNode>=0||count>0)
            {
                if(flowNode<0){flowNode=Pop();closed[flowNode]=true;flowEdge=0;}
                var links=incoming[flowNode];
                while(flowEdge<links.Count)
                {
                    if(edgeBudget==0)return false;edgeBudget--;
                    var edge=links[flowEdge++];int next=edge.Next;if(closed[next])continue;
                    float cost=distance[flowNode]+edge.Cost;
                    if(cost>=distance[next]||!active(edge.Link))continue;
                    distance[next]=cost;previous[next]=flowNode;PushOrDecrease(next);
                }
                flowNode=-1;
            }
            return true;
        }
        public bool TraceSettledFlow(string startId,List<string> path)
        {
            if(!closed[index[startId]]){path.Clear();return false;}
            TraceFlow(startId,path);return true;
        }
        public void TraceFlow(string startId,List<string> path)
        {
            path.Clear();int start=index[startId];if(float.IsPositiveInfinity(distance[start]))return;
            for(int at=start;at>=0;at=previous[at])path.Add(nodes[at].id);
        }
        // Navigation nodes and support rays are authored for the standard pilot height.
        // Keep the actual capsule unchanged; anchor these queries relative to its feet.
        public static V3 BodyAnchor(V3 center,float halfHeight)
            =>new V3(center.x,center.y-halfHeight+NativeLocomotionRules.HalfHeight,center.z);
        private bool Before(int a,int b)
        {float x=distance[a]+estimate[a],y=distance[b]+estimate[b];return x<y||x==y&&StringComparer.Ordinal.Compare(nodes[a].id,nodes[b].id)<0;}
        private void Put(int slot,int node){heap[slot]=node;position[node]=slot;}
        private void PushOrDecrease(int node)
        {
            int slot=position[node];if(slot<0)slot=count++;
            while(slot>0){int parent=(slot-1)/2;if(!Before(node,heap[parent]))break;Put(slot,heap[parent]);slot=parent;}Put(slot,node);
        }
        private int Pop()
        {
            int result=heap[0],last=heap[--count];position[result]=-1;
            if(count>0){int slot=0;while(slot*2+1<count){int child=slot*2+1;if(child+1<count&&Before(heap[child+1],heap[child]))child++;if(!Before(heap[child],last))break;Put(slot,heap[child]);slot=child;}Put(slot,last);}return result;
        }
    }
    // Per-map broad phase: authored endpoints and hazard envelopes never move.
    // Only candidate membership is cached; LinkOpen evaluates the current clock on every query.
    public sealed class NativeNavHazards
    {
        private readonly Dictionary<MapNavLink,MapHazard[]> links=new Dictionary<MapNavLink,MapHazard[]>();
        private readonly Dictionary<MapNavNode,MapHazard[]> nodes=new Dictionary<MapNavNode,MapHazard[]>();
        public NativeNavHazards(MapNav graph,MapHazard[] hazards)
        {
            var byId=new Dictionary<string,MapNavNode>();foreach(var node in graph.nodes)byId.Add(node.id,node);
            MapHazard[] Candidates(V3 a,V3 b)
            {
                var list=new List<MapHazard>();foreach(var h in hazards??Array.Empty<MapHazard>())
                    if(NativeStageHazards.CrossesEnvelope(h,a,b))list.Add(h);
                return list.Count==0?Array.Empty<MapHazard>():list.ToArray();
            }
            foreach(var node in graph.nodes)nodes.Add(node,Candidates(node.pos,node.pos));
            foreach(var link in graph.links)links.Add(link,Candidates(byId[link.a].pos,byId[link.b].pos));
        }
        public bool Unsafe(MapNavLink link,V3 a,V3 b,NativePhase phase,int round,double fightAt,double now)
            =>Unsafe(links[link],a,b,phase,round,fightAt,now);
        public bool Unsafe(MapNavNode node,NativePhase phase,int round,double fightAt,double now)
            =>Unsafe(nodes[node],node.pos,node.pos,phase,round,fightAt,now);
        private static bool Unsafe(MapHazard[] candidates,V3 a,V3 b,NativePhase phase,int round,double fightAt,double now)
        {
            foreach(var h in candidates)if(!NativeStageHazards.LinkOpen(h,phase,round,fightAt,now,a,b))return true;
            return false;
        }
    }

}
