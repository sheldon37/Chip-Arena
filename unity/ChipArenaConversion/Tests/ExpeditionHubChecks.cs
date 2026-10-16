using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Expedition hub (docs/EXPEDITION-HUB.md; exp-layout-3): the town's fenced sanctuary, its four gates, the wilds and the
    // hub record the manifest carries (Core NativeExpeditionHub, NativeExpeditionCityKit.Hub, Runtime NativeExpeditionHubPresentation).
    // Checks, on the imported map (Resources/UnityMaps.json) and manifest (Resources/ExpeditionLayout.json):
    //  - the hub record is additive: the loader's strict reader still parses the manifest and reads only the final gate; a
    //    manifest without the record parses to no hub;
    //  - the fence is closed except at its gates (a capsule flood over the map's blocks with every gate's closure solid stays in
    //    the sanctuary; open, every crossing of the sanctuary's edge lies in a gate's opening);
    //  - every instance door is reached from the sanctuary only through its route's gate, and is at least the target run away;
    //  - the spawn zones lie outside the fence and clear of the gates; every spawn but the final plaza's is in exactly one;
    //  - the gate models stay inside their closures when shut and never meet the map's blocks or the sanctuary's records
    //    through their whole travel; their motion ends where it should and a reversal mid-swing is continuous;
    //  - without a state the gates rest open (no collider in the openings yet); the DEV preview cycles every gate; the sound
    //    schedule plays each transition's cues once;
    //  - the dressing: every fence section dressed, eight gate lamps, a header per gate, the wilds' pieces dressed, no lit
    //    window or working lamp in the wilds, distance markers along the Ward Zero route;
    //  - no allocation per frame; the runtime hooks are in place and the holster tag waits for the authority's rule.
    // Static data and pure-code checks only: no Unity scene, PhysX, network or gameplay claim.
    internal static class ExpeditionHubChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition hub: "+what);count++;}
        public const double RouteMin=70;
        // A pilot-capsule grid over the map (0.25 m cells): a cell is free when no colliding block between the step and the
        // head (0.36..1.85 m) lies within the capsule's radius of its centre. Yaw-only blocks, as the generators model them.
        private sealed class Grid
        {
            public readonly double X0,Z0,Cell=.25;public readonly int Nx,Nz;public readonly bool[] Free;
            public Grid(ContentMap m,IEnumerable<ExpeditionVolume> extra,double radius=.45)
            {
                X0=m.bounds.minX;Z0=m.bounds.minZ;Nx=(int)Math.Round((m.bounds.maxX-X0)/Cell);Nz=(int)Math.Round((m.bounds.maxZ-Z0)/Cell);Free=new bool[Nx*Nz];for(int k=0;k<Free.Length;k++)Free[k]=true;
                var boxes=new List<(double x,double z,double hx,double hz,double c,double s)>();
                foreach(var b in m.blocks){double y0=b.pos.y-b.size.y/2,y1=b.pos.y+b.size.y/2;if(y1<=.36||y0>=1.85)continue;double yaw=2*Math.Atan2(b.rotation.y,b.rotation.w);boxes.Add((b.pos.x,b.pos.z,b.size.x/2,b.size.z/2,Math.Cos(yaw),Math.Sin(yaw)));}
                foreach(var v in extra??Array.Empty<ExpeditionVolume>()){double yaw=v.yaw*Math.PI/180;boxes.Add((v.x,v.z,v.sx/2,v.sz/2,Math.Cos(yaw),Math.Sin(yaw)));}
                foreach(var b in boxes)
                {
                    double ext=Math.Abs(b.c)*b.hx+Math.Abs(b.s)*b.hz+radius,extz=Math.Abs(b.s)*b.hx+Math.Abs(b.c)*b.hz+radius;
                    int i0=Math.Max(0,(int)((b.x-ext-X0)/Cell)),i1=Math.Min(Nx-1,(int)((b.x+ext-X0)/Cell)),j0=Math.Max(0,(int)((b.z-extz-Z0)/Cell)),j1=Math.Min(Nz-1,(int)((b.z+extz-Z0)/Cell));
                    for(int j=j0;j<=j1;j++)for(int i=i0;i<=i1;i++)
                    {
                        double dx=X0+(i+.5)*Cell-b.x,dz=Z0+(j+.5)*Cell-b.z,lx=dx*b.c-dz*b.s,lz=dx*b.s+dz*b.c,ex=Math.Max(0,Math.Abs(lx)-b.hx),ez=Math.Max(0,Math.Abs(lz)-b.hz);
                        if(ex*ex+ez*ez<radius*radius)Free[j*Nx+i]=false;
                    }
                }
            }
            public int At(double x,double z){int i=(int)Math.Floor((x-X0)/Cell),j=(int)Math.Floor((z-Z0)/Cell);return i<0||j<0||i>=Nx||j>=Nz?-1:j*Nx+i;}
            public void Centre(int k,out double x,out double z){x=X0+(k%Nx+.5)*Cell;z=Z0+(k/Nx+.5)*Cell;}
            public HashSet<int> Flood(double x,double z)
            {
                var seen=new HashSet<int>();int s=At(x,z);if(s<0||!Free[s])return seen;var st=new Stack<int>();st.Push(s);seen.Add(s);
                while(st.Count>0){int k=st.Pop();int i=k%Nx,j=k/Nx;foreach(var (di,dj) in new[]{(1,0),(-1,0),(0,1),(0,-1)}){int a=i+di,b=j+dj;if(a<0||b<0||a>=Nx||b>=Nz)continue;int n=b*Nx+a;if(Free[n]&&seen.Add(n))st.Push(n);}}
                return seen;
            }
            /// <summary>Walking distance (m, 8-connected, no corner cutting) from (x0, z0) to (x1, z1), or infinity.</summary>
            public double Path(double x0,double z0,double x1,double z1)
            {
                int s=At(x0,z0),t=At(x1,z1);if(s<0||t<0||!Free[s]||!Free[t])return double.PositiveInfinity;var dist=new Dictionary<int,double>{[s]=0};var heap=new SortedSet<(double,int)>{(0,s)};
                while(heap.Count>0)
                {
                    var (d,k)=heap.Min;heap.Remove(heap.Min);if(k==t)return d;if(d>dist[k])continue;int i=k%Nx,j=k/Nx;
                    for(int di=-1;di<=1;di++)for(int dj=-1;dj<=1;dj++)
                    {
                        if(di==0&&dj==0)continue;int a=i+di,b=j+dj;if(a<0||b<0||a>=Nx||b>=Nz)continue;int n=b*Nx+a;if(!Free[n])continue;if(di!=0&&dj!=0&&(!Free[j*Nx+a]||!Free[b*Nx+i]))continue;
                        double nd=d+Cell*(di!=0&&dj!=0?1.41421356:1);if(!dist.TryGetValue(n,out var old)||nd<old){if(dist.ContainsKey(n))heap.Remove((old,n));dist[n]=nd;heap.Add((nd,n));}
                    }
                }
                return double.PositiveInfinity;
            }
        }
        // A part's eight world corners.
        private static IEnumerable<(double x,double y,double z)> Corners(NativeHubPart p)
        {
            for(int k=0;k<8;k++){NativeExpeditionCityKit.Rotate(p.Qx,p.Qy,p.Qz,p.Qw,(k&1)==0?-p.Hx:p.Hx,(k&2)==0?-p.Hy:p.Hy,(k&4)==0?-p.Hz:p.Hz,out double ox,out double oy,out double oz);yield return (p.X+ox,p.Y+oy,p.Z+oz);}
        }
        private static bool In(ExpeditionVolume v,double x,double y,double z,double pad){double a=v.yaw*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),dx=x-v.x,dz=z-v.z;return Math.Abs(c*dx-s*dz)<=v.sx/2+pad&&Math.Abs(y-v.y)<=v.sy/2+pad&&Math.Abs(s*dx+c*dz)<=v.sz/2+pad;}
        // Whether a part meets a block: a point of the part (its corners and a 5 x 5 x 5 lattice) inside the block less 1 cm.
        private static bool Meets(NativeHubPart p,ContentBlock b)
        {
            double yaw=2*Math.Atan2(b.rotation.y,b.rotation.w),c=Math.Cos(yaw),s=Math.Sin(yaw);
            for(int a=0;a<5;a++)for(int bb=0;bb<5;bb++)for(int e=0;e<5;e++)
            {
                double lx=(a/2.0-1)*p.Hx,ly=(bb/2.0-1)*p.Hy,lz=(e/2.0-1)*p.Hz;NativeExpeditionCityKit.Rotate(p.Qx,p.Qy,p.Qz,p.Qw,lx,ly,lz,out double ox,out double oy,out double oz);
                double x=p.X+ox-b.pos.x,y=p.Y+oy-b.pos.y,z=p.Z+oz-b.pos.z,bx=x*c-z*s,bz=x*s+z*c;
                if(Math.Abs(bx)<b.size.x/2-.01&&Math.Abs(y)<b.size.y/2-.01&&Math.Abs(bz)<b.size.z/2-.01)return true;
            }
            return false;
        }

        // ---------------- Z-fighting: faces that share a plane (owner, 2026-10-15: "Z-fighting on the gates") ----------------
        private static void QMul(double ax,double ay,double az,double aw,double bx,double by,double bz,double bw,out double x,out double y,out double z,out double w)
        {x=aw*bx+ax*bw+ay*bz-az*by;y=aw*by-ax*bz+ay*bw+az*bx;z=aw*bz+ax*by-ay*bx+az*bw;w=aw*bw-ax*bx-ay*by-az*bz;}
        private static NativeSurfaceLayers.Box Box(NativeHubPart p)=>new NativeSurfaceLayers.Box{X=p.X,Y=p.Y,Z=p.Z,Hx=p.Hx,Hy=p.Hy,Hz=p.Hz,Qx=p.Qx,Qy=p.Qy,Qz=p.Qz,Qw=p.Qw,Material=-1};
        // A flat quad (an effect: the ward's shimmer, the sanctuary's curtain) as a box 0.2 mm thick in its plane, no bevel.
        private static NativeSurfaceLayers.Box Box(NativeFxQuads q,int i)
        {
            int o=i*12;double ax=q.Corners[o],ay=q.Corners[o+1],az=q.Corners[o+2],ux=q.Corners[o+3]-ax,uy=q.Corners[o+4]-ay,uz=q.Corners[o+5]-az,vx=q.Corners[o+9]-ax,vy=q.Corners[o+10]-ay,vz=q.Corners[o+11]-az;
            double lu=Math.Sqrt(ux*ux+uy*uy+uz*uz),lv=Math.Sqrt(vx*vx+vy*vy+vz*vz);if(lu<1e-6||lv<1e-6)return default;ux/=lu;uy/=lu;uz/=lu;vx/=lv;vy/=lv;vz/=lv;
            double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx,ln=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(ln<1e-6)return default;nx/=ln;ny/=ln;nz/=ln;vx=ny*uz-nz*uy;vy=nz*ux-nx*uz;vz=nx*uy-ny*ux;
            double tr=ux+vy+nz,qw,qx,qy,qz;
            if(tr>0){double k=Math.Sqrt(tr+1)*2;qw=.25*k;qx=(vz-ny)/k;qy=(nx-uz)/k;qz=(uy-vx)/k;}
            else if(ux>vy&&ux>nz){double k=Math.Sqrt(1+ux-vy-nz)*2;qw=(vz-ny)/k;qx=.25*k;qy=(vx+uy)/k;qz=(nx+uz)/k;}
            else if(vy>nz){double k=Math.Sqrt(1+vy-ux-nz)*2;qw=(nx-uz)/k;qx=(vx+uy)/k;qy=.25*k;qz=(ny+vz)/k;}
            else{double k=Math.Sqrt(1+nz-ux-vy)*2;qw=(uy-vx)/k;qx=(nx+uz)/k;qy=(ny+vz)/k;qz=.25*k;}
            return new NativeSurfaceLayers.Box{X=(ax+q.Corners[o+6])/2,Y=(ay+q.Corners[o+7])/2,Z=(az+q.Corners[o+8])/2,Hx=lu/2,Hy=lv/2,Hz=1e-4,Qx=qx,Qy=qy,Qz=qz,Qw=qw,Bevel=-1,Material=-1};
        }
        // Faces of `free` (settled in order, nothing may move) left within NativeSurfaceLayers.Step of a face they overlap, among
        // themselves and against `fixedBoxes`: a pair the depth test cannot tell apart.
        private static int Shared(NativeSurfaceLayers.Box[] fixedBoxes,IList<NativeSurfaceLayers.Box> free)
        {
            var all=new NativeSurfaceLayers.Box[fixedBoxes.Length+free.Count];Array.Copy(fixedBoxes,all,fixedBoxes.Length);for(int i=0;i<free.Count;i++)all[fixedBoxes.Length+i]=free[i];
            NativeSurfaceLayers.Solve(all,fixedBoxes.Length,new float[fixedBoxes.Length*6],false,out int unresolved,0);return unresolved;
        }
        // The map's blocks as the arena draws them (their own lifts) and the town kit's parts (already layered), round the
        // sanctuary; the gates' parts at a pose; the ward's and the sanctuary edge's quads.
        private static void Faces(ContentMap town,ExpeditionAreaLayout area,ExpeditionHubLayout hub,ExpeditionLayout layout)
        {
            var blocks=town.blocks;var lifts=NativeSurfaceLayers.Blocks(blocks);var near=new List<NativeSurfaceLayers.Box>();
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];if(Math.Abs(b.pos.x)>30||Math.Abs(b.pos.z)>30)continue;NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);
                var r=b.rotation;double qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r?.w??1;NativeExpeditionCityKit.Rotate(qx,qy,qz,qw,sx,sy,sz,out double ox,out double oy,out double oz);
                near.Add(new NativeSurfaceLayers.Box{X=b.pos.x+ox,Y=b.pos.y+oy,Z=b.pos.z+oz,Hx=b.size.x/2+gx,Hy=b.size.y/2+gy,Hz=b.size.z/2+gz,Qx=qx,Qy=qy,Qz=qz,Qw=qw,Material=-1});
            }
            var kit=NativeExpeditionCityKit.Build(blocks,area,hub,town.bounds);
            foreach(var p in kit.Parts)
            {
                var bx=NativeSurfaceLayers.Parented(blocks[p.Parent],p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,-1);if(Math.Abs(bx.X)>30||Math.Abs(bx.Z)>30)continue;
                QMul(bx.Qx,bx.Qy,bx.Qz,bx.Qw,p.Qx,p.Qy,p.Qz,p.Qw,out bx.Qx,out bx.Qy,out bx.Qz,out bx.Qw);near.Add(bx);
            }
            var statics=near.ToArray();var scene=new NativeExpeditionHubGates();int poses=0;
            // 0. Nothing collider-only is drawn at a gate: the closures (Astra's renderer-less gate, no-vault and horde-ward
            //    colliders) are not map blocks, no drawn block stands in a closure, and the fence's exclusion columns are
            //    pilotBlocks (position clamps), never blocks.
            foreach(var g in hub.Gates)
            {
                int inOpening=blocks.Count(b=>b.pos.y-b.size.y/2<g.Closure.y+g.Closure.sy/2-.01&&b.pos.y+b.size.y/2>.01&&ExpeditionHubRules.Intersects(g.Closure,new V3(b.pos.x,0,b.pos.z),new V3(b.pos.x,0,b.pos.z),Math.Min(b.size.x,b.size.z)/2-.01f));
                Check(inOpening==0&&!blocks.Any(b=>b.id.Contains("hub-gate")||b.id.Contains("closure")),g.Side+": no drawn map block in its closure, none is a gate or closure collider ("+inOpening+")");
            }
            Check(town.pilotBlocks.Where(x=>x.id.Contains("sanctuary-fence")).All(x=>!blocks.Any(b=>b.id==x.id)),"the fence's exclusion columns are never drawn blocks");
            // 1. The gates' own parts, against each other and the fence, posts, header and dressing, through their travel.
            foreach(var g in hub.Gates)foreach(var (open,latch) in new[]{(0f,0f),(0f,1f),(.3f,1f),(.6f,1f),(1f,1f),(1.05f,1f)})
            {
                scene.Parts.Clear();scene.Model(g,new ExpeditionGatePose{Open=open,Latch=latch});int shared=Shared(statics,scene.Parts.Select(Box).ToArray());poses++;
                Check(shared==0,g.Side+" ("+(g.Wood?"wood":"metal")+") at "+open.ToString("0.00")+" open: no gate face shares a plane with another gate part, the fence, its posts or the dressing ("+shared+")");
            }
            // 2. The effects drawn over them: the ward's shimmer (on the fence's outer face and across each opening) and the
            //    sanctuary's lit edge (its curtain stands just inside the plaza's walls, the fence and the wooden leaves), with the
            //    gates shut and open.
            var eye=new ExpeditionEye{X=0,Y=1.7f,Z=0,Rx=1,Uy=1};var ward=new List<float[]>();NativeExpeditionHubGates.WardSegments(blocks,ward);
            var rig=ExpeditionStateRig.For(layout,"exp-city");var edge=new NativeExpeditionStateScene();edge.Build(new ExpeditionView{area="exp-city",phase=ExpeditionPhase.Town},rig,null,20,eye,false,false);
            foreach(bool open in new[]{false,true})
            {
                var v=new ExpeditionHubView{Gates=hub.Gates.Select(x=>new ExpeditionGateState{Id=x.Id,Open=open,ChangedAt=10}).ToArray(),Count=hub.Gates.Length,Ward=true,Holster=true};
                scene.Forget();scene.Build(hub,v,ward,20,eye,false);var opaque=statics.Concat(scene.Parts.Select(Box)).ToArray();
                foreach(var (name,q) in new[]{("ward",scene.Quads),("sanctuary edge",edge.Quads)})
                {
                    int n=0,shared=0;for(int i=0;i<q.Count;i++){var b=Box(q,i);if(!(b.Hx>0))continue;n++;shared+=Shared(opaque,new[]{b});}
                    Check(n>40&&shared==0,"gates "+(open?"open":"shut")+": none of the "+n+" "+name+" quads lies in the plane of a face it overlaps ("+shared+")");
                }
            }
            Check(poses==24,"every gate checked at six poses");
            // 3. The hub keeps its safe zone unseen (owner, 2026-10-15): no edge, curtain, motes or ripple, while the crossing cues still
            //    record (the SAFE tag and chip read membership, not the drawing).
            var cues=new ExpeditionStateCues();var townView=new ExpeditionView{area="exp-city",phase=ExpeditionPhase.Town};cues.Observe(townView,20);
            var sv=rig.Sanctuary[0];ExpeditionStateRig.Along(sv,ExpeditionStateRig.Perimeter(sv)*.1f,out float ex,out float ez,out float enx,out float enz);float ey=sv.y-sv.sy/2+.05f;
            cues.Pilot(7,ex+enx*.3f,ey,ez+enz*.3f,rig,20);cues.Pilot(7,ex-enx*.3f,ey,ez-enz*.3f,rig,20.2);
            var hidden=new NativeExpeditionStateScene{HideSanctuaryEdge=true};hidden.Build(townView,rig,cues,20.3,eye,false,false);
            var shown=new NativeExpeditionStateScene();shown.Build(townView,rig,cues,20.3,eye,false,false);
            int recorded=0;foreach(var r in cues.Ripples)if(!double.IsNegativeInfinity(r.At))recorded++;
            Check(hidden.Quads.Count==0&&hidden.Parts.Count==0&&shown.Quads.Count>40&&recorded>=1,"the hub's safe zone draws no border (hidden "+hidden.Quads.Count+" quads, shown "+shown.Quads.Count+"); the crossing still records ("+recorded+")");
        }
        // ---------------- the leaves in step with the authority's collider ----------------
        // Whether the leaves at pose `p` leave the opening walkable: no leaf part below 2.2 m in the passage through the gate,
        // the closure the collider fills and the 2 m inside it where the wooden leaves swing (its width less 0.2 m at either
        // post, where the hinges and pockets are).
        private static bool Walkable(NativeExpeditionHubGates scene,ExpeditionHubGate g,in ExpeditionGatePose p)
        {
            scene.Parts.Clear();scene.Model(g,p);double a=g.Yaw*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),half=g.Width/2-.2;var v=g.Closure;double cd=(v.x-g.X)*s+(v.z-g.Z)*c;
            foreach(var q in scene.Parts)
            {
                if(q.Fixed)continue;
                foreach(var k in Corners(q)){double x=k.x-g.X,z=k.z-g.Z,u=x*c-z*s,d=x*s+z*c;if(k.y<2.2&&Math.Abs(u)<half&&d-cd<v.sz/2&&d-cd>-v.sz/2-2)return false;}
            }
            return true;
        }
        // The real authority (ExpeditionHubAuthority: interact, auto-close, Blocking) against the drawn leaves, every 1/240 s.
        private static void Timing(ExpeditionHubLayout hub)
        {
            var scene=new NativeExpeditionHubGates();var model=new NativeExpeditionHubGates();
            foreach(var g in hub.Gates)
            {
                int i=Array.IndexOf(hub.Gates,g);var a=new ExpeditionHubAuthority(hub,0);scene.Forget();double t0=10;
                Check(a.Interact(g.Inside.Position,t0,_=>false)&&a.View().Gates[i].Open,g.Side+": the authority opens");
                int blockedOpen=0,drawnShutPassable=0,openAtBlock=0,early=0;double t1=-1;
                for(double t=t0;t<t0+6;t+=1/240.0)
                {
                    a.Step(t,_=>false);var view=a.View();if(t1<0&&!view.Gates[i].Open)t1=t;bool solid=a.Blocking(i,t);var p=scene.Pose(g,view,t,hub);bool walk=Walkable(model,g,p);
                    bool drawnShut=p.Open<.01f&&!p.Moving;
                    if(view.Gates[i].Open&&!solid&&!walk)blockedOpen++;                       // released, a leaf still in the way
                    if(drawnShut&&!solid)drawnShutPassable++;                                    // drawn shut, walkable
                    if(!view.Gates[i].Open&&solid&&p.Open>.04f&&t-t1>=NativeExpeditionHubGates.MeetAt(hub))openAtBlock++;  // solid and met, still drawn open
                    if(view.Gates[i].Open&&t-t0<hub.Release-.25&&walk)early++;                   // drawn open long before it is
                }
                Check(t1>0&&Math.Abs(t1-(t0+hub.AutoClose))<.02,g.Side+": with nobody near it the authority closes it 2.5 s later ("+(t1-t0).ToString("0.000")+" s)");
                Check(blockedOpen==0,g.Side+": once the collider releases (0.5 s into the opening) the leaves are clear of the opening ("+blockedOpen+" samples with a leaf in the way)");
                Check(drawnShutPassable==0,g.Side+": never drawn shut while the opening can be walked through ("+drawnShutPassable+")");
                Check(openAtBlock==0,g.Side+": the leaves meet as the collider turns solid again (0.65 s into the closing), never left standing open across it ("+openAtBlock+")");
                Check(early==0,g.Side+": not drawn open while its latch lifts ("+early+")");
                var mid=scene.Pose(g,new ExpeditionHubView{Gates=new[]{new ExpeditionGateState{Id=g.Id,Open=false,ChangedAt=t1}},Count=1},t1+hub.Block-.15,hub);
                Check(mid.Moving&&mid.Open>.2f,g.Side+": visibly still closing 0.15 s before it blocks ("+mid.Open.ToString("0.00")+" open)");
                var clear=NativeExpeditionHubGates.PoseOf(g,true,0,0,NativeExpeditionHubGates.ClearAt(g,hub),hub);
                Check(Walkable(model,g,clear)&&NativeExpeditionHubGates.ClearAt(g,hub)<hub.Release,g.Side+": clear of the opening at "+NativeExpeditionHubGates.ClearAt(g,hub).ToString("0.00")+" s, before the release");
            }
        }
        // ---------------- the presentation clock ----------------
        private static void Clock()
        {
            var c=new ExpeditionHubClock();double prev=-1,maxLead=0;bool monotonic=true,onUpdate=true;
            for(int f=0;f<600;f++)
            {
                double now=f/60.0,auth=f/3*.05;double r=c.Read(auth,now);if(r<prev-1e-9)monotonic=false;prev=r;maxLead=Math.Max(maxLead,r-auth);
                if(f%3==0&&Math.Abs(r-auth)>1e-9)onUpdate=false;
            }
            Check(monotonic&&onUpdate&&maxLead<=ExpeditionHubClock.MaxLead,"the presentation clock moves every frame between 20 Hz updates, never backwards, and meets each update (lead "+maxLead.ToString("0.000")+" s)");
            var stall=new ExpeditionHubClock();stall.Read(5,0);Check(Math.Abs(stall.Read(5,3)-(5+ExpeditionHubClock.MaxLead))<1e-9,"a stalled authority clock is carried at most "+ExpeditionHubClock.MaxLead+" s ahead");
        }
        // Claude's run-through repro, promoted from a diagnostic to a hard regression on all four gates.
        private static void PendingAuthority(ExpeditionHubLayout hub)
        {
            foreach(var g in hub.Gates)
            {
                int i=Array.IndexOf(hub.Gates,g);var a=new ExpeditionHubAuthority(hub,0);double yaw=g.Yaw*Math.PI/180,ox=Math.Sin(yaw),oz=Math.Cos(yaw),r=NativeLocomotionRules.Radius;
                double face=(g.Closure.x-g.X)*ox+(g.Closure.z-g.Z)*oz+g.Closure.sz/2,d=face+3,touch=face+r+.01,t=10;
                for(int k=0;k<240;k++,t+=1/60.0)
                {
                    var center=new V3((float)(g.X+ox*d),.9f,(float)(g.Z+oz*d));
                    a.Step(t,v=>ExpeditionHubRules.CapsuleOverlaps(v,center,1.8f,(float)r));bool solid=a.Blocking(i,t);
                    Check(!a.View().Gates[i].Open&&solid,g.Side+": walking into shut gate cannot open or release it");
                    double next=d-6.8/60;if(solid&&next<touch)next=touch;d=next;
                }
                Check(d>=touch-1e-6,g.Side+": pilot remains outside solid closure");
            }
            Console.WriteLine("PASS (was PENDING) Expedition hub: all four shut gates stop a pilot who walks into them; no interact, no opening or collider release");
        }

        public static void Run(string root)
        {
            ExpeditionHubAuthorityChecks.Run(root);HubPerceptionChecks.Run(root);
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            string manifest=R("Assets/Conversion/Resources/ExpeditionLayout.json");var town=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o).maps.Single(m=>m.id=="expedition-city");
            // ---------------- the record ----------------
            var layout=NativeExpeditionLayout.Parse(manifest);var area=layout.Area("exp-city");var hub=ExpeditionHubLayout.Parse(manifest);
            Check(layout.revision=="exp-layout-3"&&hub!=null&&hub.Area=="exp-city","exp-layout-3 carries the town's hub record");
            Check(area.gates.Length==1&&area.gates[0].id=="exp-city:gate:final"&&area.spawns.Length>0&&area.slots.Length==8,"the loader's strict reader still reads the manifest: the final gate only, the spawns and slots");
            Check(ExpeditionHubLayout.Parse(R("Tests/expedition-layout-draft.json"))!=null&&ExpeditionHubLayout.Parse(manifest.Replace("\"hub\":","\"hubOld\":"))==null,"a manifest without the record has no hub (exp-layout-2 and earlier read as before)");
            Check(hub.Gates.Select(g=>g.Side).SequenceEqual(new[]{"north","east","south","west"})&&hub.Gates.Count(g=>g.Wood)==2&&hub.Gates.All(g=>g.Wood==(g.Motion=="swing"))&&hub.Gates.All(g=>Math.Abs(g.Width-4)<1e-4),"four 4 m gates: wooden swing (north, west), metal slide (east, south)");
            Check(hub.Fence.Length==8&&hub.Fence.All(f=>town.blocks.Any(b=>b.id==f.Block&&b.role=="static"))&&hub.Gates.All(g=>g.Fence.Length==2&&g.Fence.All(id=>hub.Fence.Any(f=>f.Block==id&&f.Gate==g.Id))),"eight fence sections, two flanking each gate, all static map blocks");
            Check(town.pilotBlocks.Count(p=>hub.Fence.Any(f=>p.id==f.Block+"-top"))==8,"every fence section under a pilot exclusion column");
            Check(hub.Open>0&&hub.Close>0&&hub.AutoClose>hub.Close&&hub.Release<hub.Open&&hub.Block<hub.Close,"gate timing: release inside the opening, blocking inside the closing, auto-close after it");
            var sanctuary=area.sanctuary[0];
            // ---------------- the fence ----------------
            var shut=new Grid(town,hub.Gates.Select(g=>g.Closure).Concat(area.gates.Select(g=>g.closure)));var slot=area.slots[0];var inside=shut.Flood(slot.x,slot.z);
            int outside=inside.Count(k=>{shut.Centre(k,out double x,out double z);return !In(sanctuary,x,sanctuary.y,z,.3);});
            Check(inside.Count>1000&&outside==0,"the fence is closed except at its gates: shut, the sanctuary's "+inside.Count+" free cells reach nothing outside it");
            var open=new Grid(town,area.gates.Select(g=>g.closure));var all=open.Flood(slot.x,slot.z);int crossings=0,stray=0;
            foreach(int k in all){int i=k%open.Nx;if(i+1>=open.Nx||!all.Contains(k+1))continue;open.Centre(k,out double x0,out double z0);open.Centre(k+1,out double x1,out double z1);if(In(sanctuary,x0,sanctuary.y,z0,0)==In(sanctuary,x1,sanctuary.y,z1,0))continue;crossings++;if(!hub.Gates.Any(g=>In(g.Closure,(x0+x1)/2,g.Closure.y,(z0+z1)/2,.4)))stray++;}
            foreach(int k in all){if(!all.Contains(k+open.Nx))continue;open.Centre(k,out double x0,out double z0);open.Centre(k+open.Nx,out double x1,out double z1);if(In(sanctuary,x0,sanctuary.y,z0,0)==In(sanctuary,x1,sanctuary.y,z1,0))continue;crossings++;if(!hub.Gates.Any(g=>In(g.Closure,(x0+x1)/2,g.Closure.y,(z0+z1)/2,.4)))stray++;}
            Check(crossings>0&&stray==0,"open, all "+crossings+" crossings of the sanctuary's edge lie in a gate's opening");
            // ---------------- routes ----------------
            var door=area.doors.Single();var ward=hub.Routes.Single(r=>!r.Reserved);
            Check(ward.To=="exp-ward-zero"&&Math.Abs(ward.Approach.x-door.approach.x)<1e-3&&Math.Abs(ward.Approach.z-door.approach.z)<1e-3,"the Ward Zero route ends at the door's approach");
            foreach(var r in hub.Routes)
            {
                var only=new Grid(town,hub.Gates.Where(g=>g.Id!=r.Gate).Select(g=>g.Closure).Concat(area.gates.Select(g=>g.closure)));var reach=only.Flood(slot.x,slot.z);
                Check(reach.Contains(only.At(r.Approach.x,r.Approach.z))&&!inside.Contains(shut.At(r.Approach.x,r.Approach.z)),r.To+" is reached through "+r.Gate+" alone (and not with every gate shut)");
                var g=hub.Gate(r.Gate);double walked=open.Path(g.X,g.Z,r.Approach.x,r.Approach.z);
                Check(walked>=RouteMin&&r.Path>=RouteMin&&Math.Abs(walked-r.Path)<3,r.To+": "+walked.ToString("0.0")+" m walked from the gate (record "+r.Path.ToString("0.0")+", target "+RouteMin+"): "+(walked/6.8).ToString("0.0")+" s running, "+(walked/(6.8*1.35)).ToString("0.0")+" s sprinting");
            }
            // ---------------- zones ----------------
            var spawns=area.spawns.Select(s=>s.entry).ToArray();var finals=spawns.Where(e=>area.bossTrigger.Contains(e.@exit.Position())).Select(e=>e.e).ToHashSet();
            foreach(var z in hub.Zones)
            {
                var v=z.Volume;double x0=v.x-v.sx/2,x1=v.x+v.sx/2,z0=v.z-v.sz/2,z1=v.z+v.sz/2;
                Check(!(x1>sanctuary.x-sanctuary.sx/2&&x0<sanctuary.x+sanctuary.sx/2&&z1>sanctuary.z-sanctuary.sz/2&&z0<sanctuary.z+sanctuary.sz/2),z.Id+" lies outside the fence");
                foreach(var g in hub.Gates){double d=Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(x0-g.X,g.X-x1)),2)+Math.Pow(Math.Max(0,Math.Max(z0-g.Z,g.Z-z1)),2));Check(d>=hub.ZoneApron,z.Id+" is "+hub.ZoneApron+" m or more from the "+g.Side+" gate");}
                Check(new[]{"near","mid","far"}.Contains(z.Ring)&&z.Spawns.Length>0&&z.Spawns.All(e=>v.Contains(new V3(spawns[e].@exit.x,v.y,spawns[e].@exit.z))),z.Id+": its spawns' exits lie in it");
            }
            var listed=hub.Zones.SelectMany(z=>z.Spawns).OrderBy(e=>e).ToArray();
            Check(listed.SequenceEqual(spawns.Select(e=>e.e).Where(e=>!finals.Contains(e)).OrderBy(e=>e)),"every spawn but the final plaza's "+finals.Count+" is in exactly one zone");
            double nearest=spawns.Min(e=>hub.Gates.Min(g=>Math.Sqrt(Math.Pow(e.@exit.x-g.X,2)+Math.Pow(e.@exit.z-g.Z,2))));Check(nearest>=hub.SpawnApron,"no spawn exit within "+hub.SpawnApron+" m of a gate (nearest "+nearest.ToString("0.0")+" m)");
            double Lo(string ring)=>hub.Zones.Where(z=>z.Ring==ring).Min(z=>z.Near);double Hi(string ring)=>hub.Zones.Where(z=>z.Ring==ring).Max(z=>z.Far);
            Check(Lo("near")<Lo("mid")&&Lo("mid")<Lo("far")&&Hi("mid")<Lo("far"),"rings by walking distance: near "+Lo("near")+"-"+Hi("near")+" m, mid "+Lo("mid")+"-"+Hi("mid")+", far "+Lo("far")+"-"+Hi("far"));
            Check(!manifest.Contains("\"density\"")&&!manifest.Contains("\"money\""),"no balance in the manifest (the rings' density and money are the doc's proposals)");
            // ---------------- gates: models and motion ----------------
            var scene=new NativeExpeditionHubGates();var statics=town.blocks.Where(b=>b.pos.y+b.size.y/2>.02).ToArray();
            var guard=area.slots.Select(p=>(p.x,p.z,.6)).Concat(area.interactions.Select(p=>(p.x,p.z,.6))).Concat(area.anchors.Select(p=>(p.x,p.z,.5))).ToArray();
            foreach(var g in hub.Gates)
            {
                scene.Parts.Clear();scene.Model(g,new ExpeditionGatePose{Open=0,Latch=0});var shutParts=scene.Parts.Where(p=>!p.Fixed).ToArray();
                Check(shutParts.Length>20&&shutParts.All(p=>Corners(p).All(c=>In(g.Closure,c.x,c.y,c.z,.011))),g.Side+": shut, its leaves and latch stay inside the closure the collider fills ("+shutParts.Length+" parts)");
                int met=0,onRecord=0,outward=0;
                for(int s=0;s<=24;s++)
                {
                    float f=s/24f*1.08f;scene.Parts.Clear();scene.Model(g,new ExpeditionGatePose{Open=f,Latch=1});
                    foreach(var p in scene.Parts)
                    {
                        if(p.Fixed)continue;foreach(var b in statics)if(Meets(p,b)){met++;break;}
                        foreach(var c in Corners(p)){NativeExpeditionHubGates.World(g,0,0,0,out double gx,out _,out double gz);double a=g.Yaw*Math.PI/180,d=(c.x-gx)*Math.Sin(a)+(c.z-gz)*Math.Cos(a);if(d>.23)outward++;if(c.y<2.3&&guard.Any(q=>(c.x-q.x)*(c.x-q.x)+(c.z-q.z)*(c.z-q.z)<q.Item3*q.Item3))onRecord++;}
                    }
                }
                Check(met==0&&onRecord==0&&outward==0,g.Side+": through its whole travel (to 108%, the overshoot) no leaf meets a block, a slot, an interaction or an anchor, or swings out of the sanctuary side ("+met+", "+onRecord+", "+outward+")");
                // The motion: an opening ends open, a closing shut; a reversal mid-swing starts where the leaves are.
                var op=NativeExpeditionHubGates.PoseOf(g,true,10,0,10+hub.Open+.6,hub);var cl=NativeExpeditionHubGates.PoseOf(g,false,10,1,10+hub.Close+.6,hub);
                Check(Math.Abs(op.Open-1)<.01&&op.Latch==1&&!op.Moving&&Math.Abs(cl.Open)<.01&&cl.Latch<.01&&!cl.Moving,g.Side+": an opening ends open and unlatched, a closing shut and latched");
                var mem=new NativeExpeditionHubGates();var v=new ExpeditionHubView{Gates=new[]{new ExpeditionGateState{Id=g.Id,Open=true,ChangedAt=20}},Count=1};
                double turn=20+NativeExpeditionHubGates.Unlatch(g)+(g.Wood?.1:.15);mem.Pose(g,v,20.1,hub);float before=mem.Pose(g,v,turn,hub).Open;v.Gates[0]=new ExpeditionGateState{Id=g.Id,Open=false,ChangedAt=turn};float after=mem.Pose(g,v,turn+.001,hub).Open;
                Check(before>.15&&before<.95&&Math.Abs(before-after)<.02&&mem.Pose(g,v,turn+hub.Close+.6,hub).Open<.01,g.Side+": shut mid-opening, the leaves turn back from where they were ("+before.ToString("0.00")+" → "+after.ToString("0.00")+")");
                float maxOpen=0;for(double t=0;t<1.5;t+=.01)maxOpen=Math.Max(maxOpen,NativeExpeditionHubGates.PoseOf(g,true,0,0,t,hub).Open);Check(maxOpen<=1.08f,g.Side+": the overshoot stays within the travel checked ("+maxOpen.ToString("0.000")+")");
                // Sound: each transition's three cues, once, however the clock is cut.
                var kinds=new int[8];var times=new double[8];int whole=NativeExpeditionHubGates.Cues(g,new ExpeditionGateState{Id=g.Id,Open=true,ChangedAt=5},4.9,7,hub,kinds,times);
                int cut=0;for(double t=4.9;t<7;t+=.016)cut+=NativeExpeditionHubGates.Cues(g,new ExpeditionGateState{Id=g.Id,Open=true,ChangedAt=5},t,t+.016,hub,kinds,times);
                int shutCues=NativeExpeditionHubGates.Cues(g,new ExpeditionGateState{Id=g.Id,Open=false,ChangedAt=5},4.9,7,hub,kinds,times);
                Check(whole==3&&cut==3&&shutCues==3&&kinds.Take(3).SequenceEqual(new[]{NativeExpeditionHubGates.CueCreak,NativeExpeditionHubGates.CueSlam,NativeExpeditionHubGates.CueBolt}),g.Side+": latch, creak and stop opening; creak, slam and latch closing; each once");
            }
            // Without a state every gate rests open (no collider in the opening until the director's gate authority exists).
            var rest=new NativeExpeditionHubGates();var eye=new ExpeditionEye{X=0,Y=2,Z=0,Rx=1,Uy=1};rest.Build(hub,null,new List<float[]>(),0,eye,false);
            Check(NativeExpeditionHubGates.RestOpen&&rest.GatesDrawn==4&&hub.Gates.All(g=>rest.Pose(g,null,0,hub).Open==1)&&rest.WardQuads==0,"no state: all four gates drawn resting open, no ward");
            // The DEV preview: every gate cycles open and shut on its own clock; the ward and holster read on; it says so.
            var pv=new ExpeditionHubView();int opens=0,shuts=0;for(double t=0;t<16;t+=.5){NativeExpeditionHubPreview.Fill(pv,hub,t);opens+=pv.Gates.Take(pv.Count).Count(s=>s.Open);shuts+=pv.Gates.Take(pv.Count).Count(s=>!s.Open);}
            Check(pv.Preview&&pv.Holster&&pv.Ward&&pv.Count==4&&opens>20&&shuts>20,"the DEV preview cycles every gate and turns the ward and holster on, marked as a preview");
            var ward2=new List<float[]>();NativeExpeditionHubGates.WardSegments(town.blocks,ward2);Check(ward2.Count==8,"a ward segment along every fence section");
            rest.Build(hub,pv,ward2,3,eye,false);Check(rest.WardQuads>40&&rest.Parts.Count>0,"with the ward on, the fence line and every opening shimmer ("+rest.WardQuads+" quads)");
            // Gameplay hides the ward (owner, 2026-10-16: "The hub still shows an orange barrier"): no shimmer, curtain, threshold
            // line or mote, while the gates' models, poses and dust are exactly those drawn with it.
            var live=new ExpeditionHubView{Gates=hub.Gates.Select(x=>new ExpeditionGateState{Id=x.Id,Open=false,ChangedAt=1}).ToArray(),Count=hub.Gates.Length,Ward=true,Holster=true};
            var shown=new NativeExpeditionHubGates();shown.Build(hub,live,ward2,3,eye,false);
            var hidden=new NativeExpeditionHubGates{HideWard=true};hidden.Build(hub,live,ward2,3,eye,false);
            var off=new NativeExpeditionHubGates();live.Ward=false;off.Build(hub,live,ward2,3,eye,false);live.Ward=true;
            bool sameGates=hidden.GatesDrawn==shown.GatesDrawn&&hidden.Parts.Count==shown.Parts.Count&&hidden.DustQuads==shown.DustQuads&&hidden.Quads.Count==off.Quads.Count;
            for(int i=0;sameGates&&i<hidden.Parts.Count;i++)sameGates=hidden.Parts[i].Equals(shown.Parts[i]);
            Check(shown.WardQuads>40&&hidden.WardQuads==0&&sameGates,"gameplay draws no ward (fence shimmer, gate curtains, threshold lines, motes) and the same gates, poses and dust ("+shown.WardQuads+" ward quads hidden)");
            // No allocation per frame.
            var hot=new NativeExpeditionHubGates();var hv=new ExpeditionHubView();var hclock=new ExpeditionHubClock();for(int f=0;f<10;f++){NativeExpeditionHubPreview.Fill(hv,hub,f*.016);hot.Build(hub,hv,ward2,hclock.Read(f*.05,f*.016),eye,false);}
            long before2=GC.GetAllocatedBytesForCurrentThread();for(int f=10;f<130;f++){NativeExpeditionHubPreview.Fill(hv,hub,f*.05);hot.Build(hub,hv,ward2,hclock.Read(Math.Floor(f/3.0)*.05,f*.0167),eye,false);}
            long spent=GC.GetAllocatedBytesForCurrentThread()-before2;Check(spent==0,"the gates, their dust and the ward allocate nothing per frame ("+spent+" bytes over 120 frames)");
            Check(hot.Parts.Count<=NativeExpeditionHubGates.PartCapacity&&hot.Quads.Count<NativeExpeditionHubGates.QuadCapacity,"the busiest frame fits its buffers ("+hot.Parts.Count+" parts, "+hot.Quads.Count+" quads)");
            Faces(town,area,hub,layout);Timing(hub);Clock();PendingAuthority(hub);
            // ---------------- the dressing ----------------
            var kit=NativeExpeditionCityKit.Build(town.blocks,area,hub,town.bounds);
            foreach(var f in hub.Fence){int i=Array.FindIndex(town.blocks,b=>b.id==f.Block);Check(kit.Parts.Count(p=>p.Parent==i)>=10,f.Block+" is dressed ("+(f.Kind=="wood"?"palisade logs":"scrap sheets")+")");}
            Check(kit.Glows.Count(g=>Math.Abs(g.X)<13&&Math.Abs(g.Z)<13&&g.Kind==NativeExpeditionCityKit.GlowLamp&&g.Y>4)>=6,"lamps on the gate posts light the sanctuary");
            Check(kit.Parts.Count(p=>p.Role==NativeExpeditionCityKit.RoleSign&&Math.Abs(town.blocks[p.Parent].pos.x)<13&&Math.Abs(town.blocks[p.Parent].pos.z)<13)>=40,"each gate's header carries SANCTUARY and its destination");
            foreach(var b in town.blocks.Where(b=>NativeExpeditionCityKit.Stem(b.id).StartsWith("wild-",StringComparison.Ordinal))){int i=Array.IndexOf(town.blocks,b);Check(kit.Parts.Any(p=>p.Parent==i),b.id+" is dressed");}
            int litWild=0;foreach(var p in kit.Parts){if(p.Role!=NativeExpeditionCityKit.RoleFacade||p.Drive==NativeExpeditionCityKit.DriveFire||(p.Flags&NativeCyberCityLooks.Bank)==0||p.Look!=NativeCyberCityLooks.Tube||p.Gain<.38f||p.Color==NativeExpeditionCityKit.GlassSkyHex||p.Color==NativeExpeditionCityKit.GlassDuskHex)continue;var b=town.blocks[p.Parent];if(hub.InWilds(b.pos.x+p.X,b.pos.z+p.Z))litWild++;}
            int lampsWild=kit.Glows.Count(g=>g.Kind==NativeExpeditionCityKit.GlowLamp&&hub.InWilds(g.X,g.Z));
            Check(litWild==0&&lampsWild==0,"the wilds are dark: no lit window (fires burn on) and no steady street lamp in them ("+litWild+" lit, "+lampsWild+" lamps"+string.Concat(kit.Glows.Where(g=>g.Kind==NativeExpeditionCityKit.GlowLamp&&hub.InWilds(g.X,g.Z)).Take(4).Select(g=>" at "+g.X.ToString("0.0")+","+g.Y.ToString("0.0")+","+g.Z.ToString("0.0")))+")");
            int markers=kit.Parts.Count(p=>p.Role==NativeExpeditionCityKit.RoleGround&&p.Color==0x7AD8E8);Check(markers>30,"distance markers painted along the Ward Zero route through the wilds ("+markers+" parts)");
            // ---------------- runtime hooks ----------------
            string Src(string f)=>R("Assets/ChipArena/Runtime/"+f);
            Check(Src("NativeExpeditionCityPresentation.cs").Contains("DrawTownFx(now,eye);ExpeditionHubFrame(eye,now);")&&Src("NativeExpeditionCityPresentation.cs").Contains("NativeExpeditionCityKit.Build(blocks,townArea,townHub,map.bounds)"),"the town presentation draws the hub and builds the kit with the hub record");
            Check(Src("NativeExpeditionCityAudio.cs").Contains("townTime=time;ExpeditionHubCues();")&&Src("NativeExpeditionHubPresentation.cs").Contains("NativeExpeditionHubLive.Authority?.Holster==true"),"the gates' cues play with the picture; the holster tag waits for the authority's rule");
            Check(Src("NativeExpeditionStatesPresentation.cs").Contains("var hub=NativeExpeditionHubLive.Layout;expeditionStates.HideSanctuaryEdge=hub!=null&&hub.Area==rig.Area;\n            expeditionStates.Build(v,rig,"),"the fenced hub hides its safe-zone border every frame before the scene is built; instances keep theirs");
            Check(Src("NativeExpeditionHubPresentation.cs").Contains("hubGates.HideWard=!NativeExpeditionHubPreview.On;\n            hubGates.Build(townHub,v,hubWard,t,view,LowGraphics);")
                &&Src("NativeExpeditionHubAuthority.cs").Contains("Box(\"Hub horde ward \",wardLayer,20,40);"),"the hub hides the ward every gameplay frame (the DEV preview still shows it); the ward's horde collider stays");
            Check(Src("NativeDevPanel.cs").Contains("case \"preview-hub-gates\":NativeExpeditionHubPreview.On=")&&NativeDevActions.Find("preview-hub-gates")?.Kind==NativeDevKind.Presentation,"the DEV preview is a view tool in the DEV panel");
            Console.WriteLine("PASS expedition hub ("+count+" checks: the additive hub record; fence closed but for its gates; each instance through its gate and "+RouteMin+" m or more away; zones outside the fence and clear of the gates; gate models inside their closures shut and clear through their travel; no shared planes on the gates, the ward or the sanctuary edge; leaves in step with the authority's collider; the presentation clock; continuous motion; resting open without state; DEV preview; cues; dressing; no per-frame allocation; hooks)");
        }
    }
}
