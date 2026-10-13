using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One way out of Cyber City's maglev cut ("the Cut"), found in the map's data by <see cref="NativeCyberCityExits.Build"/>.
    /// The door is where the route leaves the Cut's floor, on its wall's inner face; the top is where it reaches street level.
    /// Presentation only: nothing here collides, moves a pilot or enters the network frame.
    /// </summary>
    public struct NativeCutExit
    {
        /// <summary>+1: the wall on the +z side of the track (north); -1: the -z side.</summary>
        public int Side;
        /// <summary>The door on the wall's inner face (world x, z; its floor is the Cut's floor) and the opening along the wall:
        /// x from OpenX0 to OpenX1, up to OpenTop (the block over it, or the street when it is open to the sky).</summary>
        public float DoorX,DoorZ,OpenX0,OpenX1,OpenTop;
        /// <summary>Where the climb reaches street level (floor height), the direction it arrives in (unit xz), and the climb's
        /// overall run from the door to the top (unit xz: the way the stairs rise).</summary>
        public float TopX,TopY,TopZ,UpX,UpZ,RunX,RunZ;
        /// <summary>The block over the door (the lintel the EXIT sign hangs on) and the walls either side of the opening (Lo: its
        /// +x face is the opening's x0 edge; Hi: its -x face the x1 edge); -1 when there is none.</summary>
        public int Lintel,JambLo,JambHi;
        /// <summary>The route's walking length from the door to the top (m) and its rise.</summary>
        public float Length,Rise;
        /// <summary>The Cut floor component (connected nav floor) the door opens from.</summary>
        public int Component;
        /// <summary>The route's points (door, nav nodes up the climb, the street node) in NativeCyberCityExits.PathX/Y/Z.</summary>
        public int PathStart,PathCount;
    }

    /// <summary>The local pilot's exit hint this frame: shown only in the Cut; the nearest reachable exit, its door's horizontal
    /// distance (m) and its bearing in degrees clockwise from the camera's forward (0 ahead, 90 to the right).</summary>
    public struct NativeCutHint {public bool Shown;public int Exit;public float Distance,Bearing;}

    /// <summary>
    /// Cyber City's Cut and its exits, derived from map data only (docs/arenas/CYBERCITY-SIMULATION-CONTRACT.md §11), so the
    /// presentation follows whatever exits the simulation's geometry and navigation provide:
    /// <list type="bullet">
    /// <item>The Cut is the first `maglev` hazard's envelope (its track along x; the floor is the envelope's bottom), widened to
    /// the walls: axis-aligned blocks standing on that floor along either side (inner face within 1 m of the envelope, 1.7 m
    /// tall or more). Their inner faces give `Inner`, their outer faces `Outer` (alcoves and door recesses), their tops the
    /// street level.</item>
    /// <item>Exits are nav routes (MapNav nodes and links; a node stands <see cref="NavLift"/> over its floor, as
    /// tools/generate_stage_hazards.py writes them): each connected group of below-street nodes off the Cut's floor that links
    /// both to a Cut floor node and to a street-level node is a way out. Its door is where it joins the floor (split per 3 m
    /// cluster along a wall); its route is the shortest nav path from the door up to the street. Dead-end pockets (alcoves)
    /// never reach the street, so they are not exits. Hazard tags on links are ignored: a link gated for bots is still a way
    /// out for a pilot.</item>
    /// <item>The opening and the sign: the lowest block over the door at least 1.7 m above the floor is the lintel; the walls
    /// whose ends meet the opening are its jambs. The blocks under a route (its steps, ramp pieces, landing) carry the climb
    /// lights, each with its height up the climb (0 at the floor, 1 at the street).</item>
    /// </list>
    /// Built once per map (allocates then); the queries (<see cref="InCut"/>, <see cref="Nearest"/>, <see cref="NearestOnSide"/>,
    /// <see cref="HintFor"/>, <see cref="Tone"/>) are allocation free and pure.
    /// </summary>
    public sealed class NativeCyberCityExits
    {
        /// <summary>A nav node's height over its floor (tools/generate_stage_hazards.py: pos.y = floor + .9).</summary>
        public const float NavLift=.9f;
        /// <summary>Exit doors further apart than this along one wall are separate exits even when their climbs join.</summary>
        public const float DoorCluster=3;
        /// <summary>How far below the street a pilot's feet must be to count as in the Cut (the bridges and decks over it are at
        /// street level).</summary>
        public const float BelowStreet=.5f;

        public bool Found;public int Maglev=-1;
        /// <summary>The Cut: its floor's top, the street level, its x extent, the track's z, the walls' inner and outer face
        /// distances from it.</summary>
        public float FloorY,StreetY,X0,X1,Axis,Inner,Outer;
        public NativeCutExit[] Exits=Array.Empty<NativeCutExit>();
        /// <summary>The routes' points (floor heights), each exit's PathStart..PathStart+PathCount-1, door first.</summary>
        public float[] PathX=Array.Empty<float>(),PathY=Array.Empty<float>(),PathZ=Array.Empty<float>();
        /// <summary>Per map block: the exit whose climb it carries (-1 for none) and its height up that climb (0..1).</summary>
        public int[] BlockExit=Array.Empty<int>();public float[] BlockClimb=Array.Empty<float>();
        /// <summary>Per map block: the exit whose lintel (1), low jamb (2) or high jamb (3) it is, as exit * 4 + role; -1 for none.</summary>
        public int[] BlockOpening=Array.Empty<int>();
        // The Cut floor's nav components on a 1 m grid over the Cut (nearest node per cell), for the reachable-exit query.
        private int gx0,gz0,gw,gh;private short[] grid=Array.Empty<short>();

        public static NativeCyberCityExits Build(ContentMap map)=>Build(map?.blocks,map?.nav,map?.hazards);
        public static NativeCyberCityExits Build(ContentBlock[] blocks,MapNav nav,MapHazard[] hazards)
        {
            var c=new NativeCyberCityExits();blocks??=Array.Empty<ContentBlock>();int nb=blocks.Length;
            c.BlockExit=new int[nb];c.BlockClimb=new float[nb];c.BlockOpening=new int[nb];for(int i=0;i<nb;i++){c.BlockExit[i]=-1;c.BlockOpening[i]=-1;}
            if(hazards!=null)for(int i=0;i<hazards.Length;i++)if(NativeCyberCityHazards.IsMaglev(hazards[i])){c.Maglev=i;break;}
            if(c.Maglev<0)return c;var h=hazards[c.Maglev];if(!(h.size.x>0&&h.size.y>0&&h.size.z>0))return c;
            float half=h.size.z/2;c.Axis=h.center.z;c.X0=h.center.x-h.size.x/2;c.X1=h.center.x+h.size.x/2;c.FloorY=h.center.y-h.size.y/2;
            // Axis-aligned, static block boxes; the walls along the Cut.
            var lo=new V3[nb];var hi=new V3[nb];var usable=new bool[nb];var wallSide=new int[nb];
            float inner=float.MaxValue,outer=0,street=float.MinValue;
            for(int i=0;i<nb;i++)
            {
                var b=blocks[i];if(b?.id==null||b.dynamic||b.role=="rubble"||!Aligned(b.rotation)||!(b.size.x>0&&b.size.y>0&&b.size.z>0))continue;
                usable[i]=true;lo[i]=new V3(b.pos.x-b.size.x/2,b.pos.y-b.size.y/2,b.pos.z-b.size.z/2);hi[i]=new V3(b.pos.x+b.size.x/2,b.pos.y+b.size.y/2,b.pos.z+b.size.z/2);
                if(hi[i].x<=c.X0||lo[i].x>=c.X1)continue;
                int side=lo[i].z>=c.Axis+half-.01f?1:hi[i].z<=c.Axis-half+.01f?-1:0;if(side==0)continue;
                float near=side>0?lo[i].z-c.Axis:c.Axis-hi[i].z,far=side>0?hi[i].z-c.Axis:c.Axis-lo[i].z;
                if(near>half+1f||lo[i].y>c.FloorY+.1f||hi[i].y-lo[i].y<1.7f)continue;
                wallSide[i]=side;inner=Math.Min(inner,near);outer=Math.Max(outer,far);street=Math.Max(street,hi[i].y);
            }
            if(inner==float.MaxValue)return c;
            c.Inner=inner;c.Outer=outer;c.StreetY=street;c.Found=true;
            if(nav?.nodes==null||nav.links==null)return c;
            // Nav nodes: 1 the Cut's floor, 2 below the street off the floor (a climb, or a dead-end pocket), 3 street level.
            var nodes=nav.nodes;int n=nodes.Length;var index=new Dictionary<string,int>(n,StringComparer.Ordinal);var kind=new byte[n];var fy=new float[n];
            for(int i=0;i<n;i++)
            {
                var nd=nodes[i];if(nd?.id==null||index.ContainsKey(nd.id))continue;index[nd.id]=i;float f=nd.pos.y-NavLift;fy[i]=f;
                bool inside=nd.pos.x>=c.X0&&nd.pos.x<=c.X1&&Math.Abs(nd.pos.z-c.Axis)<=c.Inner+.05f; // a node on the wall's face line stands in an opening's mouth
                kind[i]=inside&&f>=c.FloorY-.5f&&f<=c.FloorY+.5f?(byte)1:f>=c.StreetY-.05f?(byte)3:f>=c.FloorY-.5f?(byte)2:(byte)0;
            }
            var adj=new List<int>[n];for(int i=0;i<n;i++)adj[i]=new List<int>(4);
            foreach(var l in nav.links){if(l?.a==null||l.b==null||!index.TryGetValue(l.a,out int a)||!index.TryGetValue(l.b,out int b)||a==b)continue;adj[a].Add(b);adj[b].Add(a);}
            // The floor's connected components.
            var comp=new int[n];for(int i=0;i<n;i++)comp[i]=-1;int comps=0;var queue=new Queue<int>();
            for(int i=0;i<n;i++)if(kind[i]==1&&comp[i]<0)
            {
                comp[i]=comps;queue.Enqueue(i);
                while(queue.Count>0){int at=queue.Dequeue();foreach(int k in adj[at])if(kind[k]==1&&comp[k]<0){comp[k]=comps;queue.Enqueue(k);}}
                comps++;
            }
            c.BuildGrid(nodes,kind,comp);
            // Each group of climb nodes reached from the floor: an exit when it also reaches the street.
            var group=new int[n];for(int i=0;i<n;i++)group[i]=-1;int groups=0;var members=new List<int>();var exits=new List<NativeCutExit>();var px=new List<float>();var py=new List<float>();var pz=new List<float>();
            var doors=new List<(int floor,int climb)>();var tops=new List<(int climb,int street)>();
            for(int i=0;i<n;i++)if(kind[i]==1)foreach(int s in adj[i])
            {
                if(kind[s]!=2||group[s]>=0)continue;
                members.Clear();doors.Clear();tops.Clear();group[s]=groups;queue.Enqueue(s);
                while(queue.Count>0)
                {
                    int at=queue.Dequeue();members.Add(at);
                    foreach(int k in adj[at]){if(kind[k]==2&&group[k]<0){group[k]=groups;queue.Enqueue(k);}else if(kind[k]==1)doors.Add((k,at));else if(kind[k]==3)tops.Add((at,k));}
                }
                groups++;if(doors.Count==0||tops.Count==0)continue;
                // Doors along each wall, clustered by x.
                doors.Sort((p,q)=>{int sp=SideOf(c,nodes[p.climb].pos.z),sq=SideOf(c,nodes[q.climb].pos.z);if(sp!=sq)return sq.CompareTo(sp);int d=nodes[p.climb].pos.x.CompareTo(nodes[q.climb].pos.x);return d!=0?d:p.climb.CompareTo(q.climb);});
                for(int d0=0;d0<doors.Count;)
                {
                    int side=SideOf(c,nodes[doors[d0].climb].pos.z),d1=d0+1;
                    while(d1<doors.Count&&SideOf(c,nodes[doors[d1].climb].pos.z)==side&&nodes[doors[d1].climb].pos.x-nodes[doors[d1-1].climb].pos.x<=DoorCluster)d1++;
                    if(side==0)side=SideOf(c,nodes[tops[0].street].pos.z);
                    float sx=0;for(int k=d0;k<d1;k++)sx+=nodes[doors[k].climb].pos.x;float doorX=sx/(d1-d0);
                    int entry=doors[d0].climb;float best=float.MaxValue;for(int k=d0;k<d1;k++){float dd=Math.Abs(nodes[doors[k].climb].pos.x-doorX);if(dd<best){best=dd;entry=doors[k].climb;}}
                    if(side!=0)exits.Add(c.Route(nodes,fy,members,adj,kind,tops,entry,doorX,side,comp[doors[d0].floor],px,py,pz));
                    d0=d1;
                }
            }
            exits.Sort((p,q)=>p.Side!=q.Side?q.Side.CompareTo(p.Side):p.DoorX.CompareTo(q.DoorX));
            // Re-pack the route points in the sorted order.
            var ox=new List<float>();var oy=new List<float>();var oz=new List<float>();var list=exits.ToArray();
            for(int e=0;e<list.Length;e++){int s0=list[e].PathStart;list[e].PathStart=ox.Count;for(int k=0;k<list[e].PathCount;k++){ox.Add(px[s0+k]);oy.Add(py[s0+k]);oz.Add(pz[s0+k]);}}
            c.Exits=list;c.PathX=ox.ToArray();c.PathY=oy.ToArray();c.PathZ=oz.ToArray();
            for(int e=0;e<list.Length;e++)c.Openings(blocks,lo,hi,usable,wallSide,e);
            for(int e=0;e<list.Length;e++)c.Supports(lo,hi,usable,wallSide,e);
            return c;
        }
        private static bool Aligned(ContentRotation r)=>r==null||Math.Abs(r.x)<1e-4f&&Math.Abs(r.y)<1e-4f&&Math.Abs(r.z)<1e-4f;
        private static int SideOf(NativeCyberCityExits c,float z)=>z>c.Axis+.01f?1:z<c.Axis-.01f?-1:0;

        // The shortest nav route from the door's entry node up to a street node (Dijkstra over the group, Euclidean lengths).
        private NativeCutExit Route(MapNavNode[] nodes,float[] fy,List<int> members,List<int>[] adj,byte[] kind,List<(int climb,int street)> tops,int entry,float doorX,int side,int component,List<float> px,List<float> py,List<float> pz)
        {
            int m=members.Count;var dist=new float[m];var prev=new int[m];var done=new bool[m];var slot=new Dictionary<int,int>(m);
            for(int i=0;i<m;i++){slot[members[i]]=i;dist[i]=float.MaxValue;prev[i]=-1;}
            dist[slot[entry]]=0;
            for(int it=0;it<m;it++)
            {
                int u=-1;for(int i=0;i<m;i++)if(!done[i]&&dist[i]<float.MaxValue&&(u<0||dist[i]<dist[u]))u=i;if(u<0)break;done[u]=true;
                foreach(int k in adj[members[u]])
                {
                    if(kind[k]!=2||!slot.TryGetValue(k,out int v)||done[v])continue;float w=Span(nodes[members[u]].pos,nodes[k].pos);
                    if(dist[u]+w<dist[v]){dist[v]=dist[u]+w;prev[v]=u;}
                }
            }
            int topClimb=-1,topStreet=-1;float total=float.MaxValue;
            foreach(var (cl,st) in tops){if(!slot.TryGetValue(cl,out int v)||dist[v]==float.MaxValue)continue;float t=dist[v]+Span(nodes[cl].pos,nodes[st].pos);if(t<total-1e-5f||Math.Abs(t-total)<=1e-5f&&string.CompareOrdinal(nodes[st].id,topStreet>=0?nodes[topStreet].id:null)<0){total=t;topClimb=v;topStreet=st;}}
            var e=new NativeCutExit{Side=side,DoorX=doorX,DoorZ=Axis+side*Inner,Lintel=-1,JambLo=-1,JambHi=-1,Component=component,PathStart=px.Count};
            if(topClimb<0){e.TopX=doorX;e.TopY=StreetY;e.TopZ=e.DoorZ;e.UpX=0;e.UpZ=side;e.RunX=0;e.RunZ=side;e.PathCount=0;return e;}
            var chain=new List<int>();for(int v=topClimb;v>=0;v=prev[v])chain.Add(members[v]);chain.Reverse();
            px.Add(doorX);py.Add(FloorY);pz.Add(e.DoorZ);foreach(int k in chain){px.Add(nodes[k].pos.x);py.Add(fy[k]);pz.Add(nodes[k].pos.z);}
            px.Add(nodes[topStreet].pos.x);py.Add(fy[topStreet]);pz.Add(nodes[topStreet].pos.z);e.PathCount=px.Count-e.PathStart;
            float len=0;for(int k=e.PathStart+1;k<px.Count;k++){float dx=px[k]-px[k-1],dy=py[k]-py[k-1],dz=pz[k]-pz[k-1];len+=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);}
            e.Length=len;e.TopX=nodes[topStreet].pos.x;e.TopY=fy[topStreet];e.TopZ=nodes[topStreet].pos.z;e.Rise=e.TopY-FloorY;
            var last=nodes[members[topClimb]].pos;Unit(e.TopX-last.x,e.TopZ-last.z,0,side,out e.UpX,out e.UpZ);
            var first=nodes[entry].pos;Unit(e.TopX-first.x,e.TopZ-first.z,e.UpX,e.UpZ,out e.RunX,out e.RunZ);
            return e;
        }
        private static float Span(V3 a,V3 b){float dx=a.x-b.x,dy=a.y-b.y,dz=a.z-b.z;return (float)Math.Sqrt(dx*dx+dy*dy+dz*dz);}
        private static void Unit(float x,float z,float fx,float fz,out float ux,out float uz){float l=(float)Math.Sqrt(x*x+z*z);if(l<1e-4f){ux=fx;uz=fz;return;}ux=x/l;uz=z/l;}

        // The opening: the lowest block over the door (1.7 m clear or more) is the lintel; its x span (else the gap between the
        // walls) is the opening; the walls whose ends meet it are the jambs.
        private void Openings(ContentBlock[] blocks,V3[] lo,V3[] hi,bool[] usable,int[] wallSide,int e)
        {
            var x=Exits[e];int side=x.Side;float face=Axis+side*Inner,best=float.MaxValue;
            for(int i=0;i<lo.Length;i++)
            {
                if(!usable[i]||lo[i].x>x.DoorX+.01f||hi[i].x<x.DoorX-.01f)continue;
                bool behind=side>0?lo[i].z<=face+.3f&&hi[i].z>=face+.05f:hi[i].z>=face-.3f&&lo[i].z<=face-.05f;
                if(!behind||lo[i].y<FloorY+1.7f||lo[i].y>StreetY+.05f)continue;
                if(lo[i].y<best){best=lo[i].y;x.Lintel=i;}
            }
            if(x.Lintel>=0){x.OpenX0=lo[x.Lintel].x;x.OpenX1=hi[x.Lintel].x;x.OpenTop=lo[x.Lintel].y;}
            else
            {
                x.OpenX0=x.DoorX-1;x.OpenX1=x.DoorX+1;x.OpenTop=StreetY;float a=float.MinValue,b=float.MaxValue;
                for(int i=0;i<lo.Length;i++){if(wallSide[i]!=side)continue;if(hi[i].x<=x.DoorX+.01f&&hi[i].x>a)a=hi[i].x;if(lo[i].x>=x.DoorX-.01f&&lo[i].x<b)b=lo[i].x;}
                if(a>float.MinValue&&x.DoorX-a<4)x.OpenX0=a;if(b<float.MaxValue&&b-x.DoorX<4)x.OpenX1=b;
            }
            for(int i=0;i<lo.Length;i++)
            {
                if(wallSide[i]!=side||hi[i].y<x.OpenTop-.05f)continue;
                if(x.JambLo<0&&Math.Abs(hi[i].x-x.OpenX0)<.05f)x.JambLo=i;else if(x.JambHi<0&&Math.Abs(lo[i].x-x.OpenX1)<.05f)x.JambHi=i;
            }
            if(x.Lintel>=0&&BlockOpening[x.Lintel]<0)BlockOpening[x.Lintel]=e*4+1;if(x.JambLo>=0&&BlockOpening[x.JambLo]<0)BlockOpening[x.JambLo]=e*4+2;if(x.JambHi>=0&&BlockOpening[x.JambHi]<0)BlockOpening[x.JambHi]=e*4+3;
            Exits[e]=x;
        }
        // The blocks under a route beyond the Cut's floor (steps, ramp pieces, the landing): their top within .3 m of the route's
        // floor where it passes over them (sampled every .25 m, footprints grown by .3 m). Slabs spanning the Cut's width (its
        // floor) and the walls are left out.
        private void Supports(V3[] lo,V3[] hi,bool[] usable,int[] wallSide,int e)
        {
            var x=Exits[e];if(x.PathCount<2)return;float rise=Math.Max(.01f,StreetY-FloorY);
            for(int k=x.PathStart+1;k<x.PathStart+x.PathCount;k++)
            {
                float ax=PathX[k-1],ay=PathY[k-1],az=PathZ[k-1],bx=PathX[k],by=PathY[k],bz=PathZ[k];int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az))/.25));
                for(int s=0;s<=steps;s++)
                {
                    float t=(float)s/steps,sx=ax+(bx-ax)*t,sy=ay+(by-ay)*t,sz=az+(bz-az)*t;if(Math.Abs(sz-Axis)<Inner-.01f)continue;
                    for(int i=0;i<lo.Length;i++)
                    {
                        if(!usable[i]||wallSide[i]!=0||BlockExit[i]>=0)continue;float top=hi[i].y;if(top<FloorY-.01f||top>StreetY-.02f||Math.Abs(top-sy)>.3f)continue;
                        if(lo[i].z<Axis-Inner+.1f&&hi[i].z>Axis+Inner-.1f)continue;
                        if(sx<lo[i].x-.3f||sx>hi[i].x+.3f||sz<lo[i].z-.3f||sz>hi[i].z+.3f)continue;
                        BlockExit[i]=e;BlockClimb[i]=Math.Max(0,Math.Min(1,(top-FloorY)/rise));
                    }
                }
            }
        }
        private void BuildGrid(MapNavNode[] nodes,byte[] kind,int[] comp)
        {
            gx0=(int)Math.Floor(X0)-1;gz0=(int)Math.Floor(Axis-Inner)-1;gw=(int)Math.Ceiling(X1)+2-gx0;gh=(int)Math.Ceiling(Axis+Inner)+2-gz0;
            if(gw<=0||gh<=0||(long)gw*gh>200000){gw=gh=0;return;}
            grid=new short[gw*gh];var near=new float[gw*gh];for(int i=0;i<grid.Length;i++){grid[i]=-1;near[i]=float.MaxValue;}
            for(int i=0;i<nodes.Length;i++)
            {
                if(kind[i]!=1||comp[i]<0||comp[i]>short.MaxValue)continue;var p=nodes[i].pos;int cx=(int)Math.Round(p.x)-gx0,cz=(int)Math.Round(p.z)-gz0;if(cx<0||cz<0||cx>=gw||cz>=gh)continue;
                float d=Math.Abs(p.x-(cx+gx0))+Math.Abs(p.z-(cz+gz0));int g=cz*gw+cx;if(d<near[g]){near[g]=d;grid[g]=(short)comp[i];}
            }
        }

        // ---------------- queries (pure, allocation free) ----------------
        /// <summary>Whether a pilot with feet at (x, feetY, z) is in the Cut: within its x extent and its walls' outer faces (the
        /// alcoves and door recesses count; the landings and stairs beyond do not) and at least <see cref="BelowStreet"/> under
        /// the street (the bridges over it do not count).</summary>
        public bool InCut(float x,float feetY,float z)=>Found&&x>=X0&&x<=X1&&Math.Abs(z-Axis)<Outer&&feetY<StreetY-BelowStreet&&feetY>FloorY-1.5f;
        /// <summary>The Cut floor component under (x, z): the nearest floor node's within 1.5 m, else -1 (unknown).</summary>
        public int ComponentAt(float x,float z)
        {
            if(gw==0)return -1;int cx=(int)Math.Round(x)-gx0,cz=(int)Math.Round(z)-gz0;int best=-1;float bd=float.MaxValue;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int ix=cx+dx,iz=cz+dz;if(ix<0||iz<0||ix>=gw||iz>=gh)continue;int g=grid[iz*gw+ix];if(g<0)continue;
                float ddx=ix+gx0-x,ddz=iz+gz0-z,d=ddx*ddx+ddz*ddz;if(d<bd){bd=d;best=g;}
            }
            return best;
        }
        /// <summary>The nearest exit reachable from (x, z) (one whose door opens on the same Cut floor component; any when that
        /// is unknown) by horizontal distance to its door, or -1.</summary>
        public int Nearest(float x,float z,out float distance)
        {
            distance=float.MaxValue;int best=-1,c=ComponentAt(x,z);
            for(int i=0;i<Exits.Length;i++)
            {
                if(c>=0&&Exits[i].Component!=c)continue;float dx=Exits[i].DoorX-x,dz=Exits[i].DoorZ-z,d=(float)Math.Sqrt(dx*dx+dz*dz);
                if(d<distance-1e-5f){distance=d;best=i;}
            }
            if(best<0)distance=0;return best;
        }
        /// <summary>The nearest exit on one wall (side +1 / -1) along the Cut from x (for the floor chevrons), its door's x minus x
        /// in `dx`; -1 when that wall has none.</summary>
        public int NearestOnSide(float x,int side,out float dx)
        {
            dx=0;int best=-1;float bd=float.MaxValue;
            for(int i=0;i<Exits.Length;i++){if(Exits[i].Side!=side)continue;float d=Math.Abs(Exits[i].DoorX-x);if(d<bd-1e-5f){bd=d;best=i;dx=Exits[i].DoorX-x;}}
            return best;
        }
        /// <summary>The local pilot's hint (feet at (x, feetY, z); the camera's forward in xz): hidden outside the Cut and when no
        /// exit is reachable.</summary>
        public NativeCutHint HintFor(float x,float feetY,float z,float forwardX,float forwardZ)
        {
            var h=new NativeCutHint{Exit=-1};if(!InCut(x,feetY,z))return h;int e=Nearest(x,z,out float d);if(e<0)return h;
            float fl=(float)Math.Sqrt(forwardX*forwardX+forwardZ*forwardZ);if(fl<1e-4f){forwardX=0;forwardZ=1;}else{forwardX/=fl;forwardZ/=fl;}
            float dx=Exits[e].DoorX-x,dz=Exits[e].DoorZ-z,rx=forwardZ,rz=-forwardX;
            h.Shown=true;h.Exit=e;h.Distance=d;h.Bearing=d<1e-4f?0:(float)(Math.Atan2(dx*rx+dz*rz,dx*forwardX+dz*forwardZ)*180/Math.PI);return h;
        }
        /// <summary>The way-out guidance's tone for the maglev's state (the floor chevrons and the HUD hint share it): 0 calm
        /// (exit white), 1 the warning (amber), 2 the danger and the pass (red); the clear is calm again, as the edge strips.</summary>
        public static int Tone(in NativeHazardState s)=>s.Phase==NativeHazardPhase.Warn?1:s.Phase==NativeHazardPhase.Danger||s.Phase==NativeHazardPhase.Active?2:0;
    }
}
