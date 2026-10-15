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
                double turn=20+NativeExpeditionHubGates.LatchLift+(g.Wood?.1:.3);mem.Pose(g,v,20.1,hub);float before=mem.Pose(g,v,turn,hub).Open;v.Gates[0]=new ExpeditionGateState{Id=g.Id,Open=false,ChangedAt=turn};float after=mem.Pose(g,v,turn+.001,hub).Open;
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
            var ward2=new List<float[]>();foreach(var b in town.blocks.Where(b=>NativeExpeditionCityKit.Stem(b.id).StartsWith("sanctuary-fence-",StringComparison.Ordinal)))ward2.Add(new[]{b.pos.x-b.size.x/2,b.pos.z,b.pos.x+b.size.x/2,b.pos.z,3.2f});
            rest.Build(hub,pv,ward2,3,eye,false);Check(rest.WardQuads>40&&rest.Parts.Count>0,"with the ward on, the fence line and every opening shimmer ("+rest.WardQuads+" quads)");
            // No allocation per frame.
            var hot=new NativeExpeditionHubGates();var hv=new ExpeditionHubView();for(int f=0;f<10;f++){NativeExpeditionHubPreview.Fill(hv,hub,f*.016);hot.Build(hub,hv,ward2,f*.016,eye,false);}
            long before2=GC.GetAllocatedBytesForCurrentThread();for(int f=10;f<130;f++){NativeExpeditionHubPreview.Fill(hv,hub,f*.05);hot.Build(hub,hv,ward2,f*.05,eye,false);}
            long spent=GC.GetAllocatedBytesForCurrentThread()-before2;Check(spent==0,"the gates, their dust and the ward allocate nothing per frame ("+spent+" bytes over 120 frames)");
            Check(hot.Parts.Count<=NativeExpeditionHubGates.PartCapacity&&hot.Quads.Count<NativeExpeditionHubGates.QuadCapacity,"the busiest frame fits its buffers ("+hot.Parts.Count+" parts, "+hot.Quads.Count+" quads)");
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
            Check(Src("NativeDevPanel.cs").Contains("case \"preview-hub-gates\":NativeExpeditionHubPreview.On=")&&NativeDevActions.Find("preview-hub-gates")?.Kind==NativeDevKind.Presentation,"the DEV preview is a view tool in the DEV panel");
            Console.WriteLine("PASS expedition hub ("+count+" checks: the additive hub record; fence closed but for its gates; each instance through its gate and "+RouteMin+" m or more away; zones outside the fence and clear of the gates; gate models inside their closures shut and clear through their travel; continuous motion; resting open without state; DEV preview; cues; dressing; no per-frame allocation; hooks)");
        }
    }
}
