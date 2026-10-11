using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Four walkable layers per one-metre column, rebuilt in bounded batches when collision changes.
    // Edges allow the baseline 1.5m jump and at most a 3m drop; a route never crosses missing floor.
    public sealed class NativeArenaNavigation
    {
        private struct Box {public V3 p,s;}
        private sealed class Node {public V3 p;public int cell;public readonly List<int> incoming=new List<int>();public readonly List<int> outgoing=new List<int>();}
        private readonly ContentMap map;private readonly int width,depth;private readonly float minX,minZ;
        private List<Node> nodes=new List<Node>(),building;private List<int>[] cells,buildingCells;
        private Box[] boxes,danger;private int revision=-1,buildingRevision=-1,column;
        private readonly Dictionary<int,int[]> flows=new Dictionary<int,int[]>();
        public int Revision=>revision;public bool Ready=>revision>=0;
        public NativeArenaNavigation(ContentMap map)
        {this.map=map;var b=map.gameplay.bounds;minX=(float)Math.Ceiling(b.minX+1);minZ=(float)Math.Ceiling(b.minZ+1);width=(int)Math.Floor(b.maxX-1-minX)+1;depth=(int)Math.Floor(b.maxZ-1-minZ)+1;}
        public void Update(NativeStructure structure,int budget=256)
        {
            if(building==null)
            {
                if(revision==structure.Revision)return;
                buildingRevision=structure.Revision;column=0;building=new List<Node>();buildingCells=new List<int>[width*depth];
                var solid=new List<Box>();var unsafeBoxes=new List<Box>();
                foreach(var b in map.blocks.Where(b=>string.IsNullOrEmpty(b.group)))solid.Add(new Box{p=b.pos,s=b.size});
                foreach(var b in structure.Blocks)
                {
                    if(b.state==StructureState.Intact||b.state==StructureState.Telegraph||b.state==StructureState.Settled)solid.Add(new Box{p=b.position,s=b.size});
                    if(b.state==StructureState.Telegraph||b.state==StructureState.Falling)unsafeBoxes.Add(new Box{p=b.position,s=b.size});
                }
                // Always route around authored hazards rather than gambling on arrival-time phases.
                foreach(var h in map.gameplay.hazards??Array.Empty<MapHazard>())unsafeBoxes.Add(new Box{p=h.position,s=h.size});
                boxes=solid.ToArray();danger=unsafeBoxes.ToArray();
            }
            int end=Math.Min(width*depth,column+Math.Max(1,budget));
            for(;column<end;column++)
            {
                float x=minX+column%width,z=minZ+column/width;var cell=new List<int>();buildingCells[column]=cell;
                if(danger.Any(b=>Math.Abs(x-b.p.x)<b.s.x/2+.6f&&Math.Abs(z-b.p.z)<b.s.z/2+.6f))continue;
                var levels=boxes.Where(b=>Math.Abs(x-b.p.x)<=b.s.x/2-.42f&&Math.Abs(z-b.p.z)<=b.s.z/2-.42f).Select(b=>b.p.y+b.s.y/2+.9f).Distinct().OrderBy(y=>y);
                foreach(float y in levels)
                {var p=new V3(x,y,z);if(y<=map.gameplay.bounds.killY+1||y>=map.gameplay.bounds.ceiling-1||!Clear(p))continue;cell.Add(building.Count);building.Add(new Node{p=p,cell=column});if(cell.Count==4)break;}
            }
            if(column<width*depth)return;
            // At most four layers and four neighbours; bounded by the validated 128m map size.
            for(int i=0;i<building.Count;i++)
            {
                var a=building[i];int x=a.cell%width,z=a.cell/width;
                foreach(int c in new[]{x>0?a.cell-1:-1,x<width-1?a.cell+1:-1,z>0?a.cell-width:-1,z<depth-1?a.cell+width:-1})
                {
                    if(c<0)continue;foreach(int j in buildingCells[c])
                    {var b=building[j];float dy=b.p.y-a.p.y;if(dy>1.5f||dy< -3)continue;var mid=(a.p+b.p)*.5f;mid.y=Math.Max(a.p.y,b.p.y)+.05f;if(!Clear(mid))continue;a.outgoing.Add(j);b.incoming.Add(i);}
                }
            }
            nodes=building;cells=buildingCells;revision=buildingRevision;building=null;flows.Clear();
        }
        private bool Clear(V3 p)=>!boxes.Any(b=>Math.Abs(p.x-b.p.x)<b.s.x/2+.42f&&Math.Abs(p.z-b.p.z)<b.s.z/2+.42f&&p.y+.85f>b.p.y-b.s.y/2+.01f&&p.y-.85f<b.p.y+b.s.y/2-.01f);
        private int Nearest(V3 p)
        {
            if(nodes.Count==0)return -1;int cx=Math.Max(0,Math.Min(width-1,(int)Math.Round(p.x-minX))),cz=Math.Max(0,Math.Min(depth-1,(int)Math.Round(p.z-minZ))),best=-1;float distance=float.MaxValue;
            for(int z=Math.Max(0,cz-2);z<=Math.Min(depth-1,cz+2);z++)for(int x=Math.Max(0,cx-2);x<=Math.Min(width-1,cx+2);x++)foreach(int i in cells[x+z*width])
            {var delta=nodes[i].p-p;if(Math.Abs(delta.y)>1.8f)continue;float d=delta.x*delta.x+delta.z*delta.z+delta.y*delta.y*4;if(d<distance){distance=d;best=i;}}
            return best;
        }
        public V3 Direction(V3 from,V3 target,out bool jump)
        {
            jump=false;int start=Nearest(from),goal=Nearest(target);if(start<0||goal<0)return new V3();
            if(!flows.TryGetValue(goal,out var steps))
            {
                if(flows.Count>=8)flows.Clear();steps=Enumerable.Repeat(int.MaxValue,nodes.Count).ToArray();var queue=new Queue<int>();queue.Enqueue(goal);steps[goal]=0;
                while(queue.Count>0){int n=queue.Dequeue();foreach(int p in nodes[n].incoming)if(steps[p]==int.MaxValue){steps[p]=steps[n]+1;queue.Enqueue(p);}}flows[goal]=steps;
            }
            if(steps[start]==int.MaxValue)return new V3();int next=start;foreach(int n in nodes[start].outgoing)if(steps[n]<steps[next])next=n;
            var point=next==start?nodes[start].p:nodes[next].p;jump=point.y>from.y+.25f;var delta=point-from;delta.y=0;return delta.Length>.12f?delta.Normalized:new V3();
        }
    }
}
