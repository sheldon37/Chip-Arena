#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Pier 9 rebuilt (docs/arenas/HARBOR.md; draft docs/arenas/scripts/harbor_m2.py; Core NativePier*, Runtime
    // NativePierPresentation, MapLooks.json `harbor-pier-lights`). Against the committed catalogue and looks: the record validates,
    // keeps its id and name, sits in the shipped catalogue and Random, every spawn is reachable on the nav graph and keeps 3 m from
    // every hazard on its floor; the hazards are the existing kinds (the coaster a maglev, the drop towers presses), idle for the
    // fight's first 4 s and telegraphed at least as long as today's; the drawn train is the simulation's in the pass and never in
    // the lane outside it; the look resolves, styled, with the advanced lighting; the backdrop stays out of the play space; the
    // kit's attached parts stay inside their blocks (the collider rule), its free parts hang overhead or stand where no floor is;
    // the set pieces turn outside or over the play space; the rig and effects follow the replicated state, telegraphs are never
    // reduced, everything is deterministic and allocation free per frame; each audio cue sounds once; the runtime is wired and the
    // map is off the five M1 stages' shared code.
    internal static class PierChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Pier 9: "+what);count++;}
        private struct Box3 {public double X0,X1,Y0,Y1,Z0,Z1;}
        private static Box3 Of(ContentBlock b)=>new Box3{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2};
        private static Box3 Of(in FactoryPart p)
        {
            var q=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var b=new Box3{X0=double.MaxValue,X1=double.MinValue,Y0=double.MaxValue,Y1=double.MinValue,Z0=double.MaxValue,Z1=double.MinValue};
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?-p.Hx:p.Hx,(c&2)==0?-p.Hy:p.Hy,(c&4)==0?-p.Hz:p.Hz),q);b.X0=Math.Min(b.X0,p.X+v.X);b.X1=Math.Max(b.X1,p.X+v.X);b.Y0=Math.Min(b.Y0,p.Y+v.Y);b.Y1=Math.Max(b.Y1,p.Y+v.Y);b.Z0=Math.Min(b.Z0,p.Z+v.Z);b.Z1=Math.Max(b.Z1,p.Z+v.Z);}
            return b;
        }
        private static bool Within(Box3 a,Box3 b,double g)=>a.X0>=b.X0-g&&a.X1<=b.X1+g&&a.Y0>=b.Y0-g&&a.Y1<=b.Y1+g&&a.Z0>=b.Z0-g&&a.Z1<=b.Z1+g;
        private static string Show(Box3 p)=>"["+p.X0.ToString("0.00")+".."+p.X1.ToString("0.00")+", "+p.Y0.ToString("0.00")+".."+p.Y1.ToString("0.00")+", "+p.Z0.ToString("0.00")+".."+p.Z1.ToString("0.00")+"]";
        /// <summary>The tops a pilot can stand on: every block at least .8 m square on plan that no pilot column closes to everyone.</summary>
        internal static List<(double x0,double x1,double z0,double z1,double top)> Floors(ContentMap map)
        =>map.blocks.Where(b=>b.size.x>=.8&&b.size.z>=.8&&!map.pilotBlocks.Any(p=>!p.bots&&p.id==b.id+"-top")).Select(b=>((double)(b.pos.x-b.size.x/2),(double)(b.pos.x+b.size.x/2),(double)(b.pos.z-b.size.z/2),(double)(b.pos.z+b.size.z/2),(double)(b.pos.y+b.size.y/2))).ToList();

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var text=R("Assets/Conversion/Resources/UnityMaps.json");var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);NativeMapRules.Merge(content,JsonSerializer.Deserialize<UnityMapContent>(text,o),text);
            NativeMapRules.ValidateShippedCatalog(content);
            var map=maps.maps.Single(m=>m.id==NativePierLooks.MapId);NativeMapRules.Validate(map);
            Check(map.id=="harbor"&&map.name=="Pier 9"&&map.look==NativePierLooks.Look,"the id, the name and the look");
            Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,8).Select(i=>new NativeSeat{id=i,team=i%2}),true),"selectable and in Random for a full room");
            Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
            Check(map.blocks.Length<=350&&map.blocks.All(b=>b.role=="static"),"the block budget, every block static ("+map.blocks.Length+")");
            Check(map.nav.nodes.Length<3500&&map.nav.links.Length<6500,"a lean nav graph ("+map.nav.nodes.Length+" nodes)");
            Check(map.bounds.killY<=-5&&map.bounds.killY>=-6&&map.blocks.All(b=>b.pos.x-b.size.x/2>=map.bounds.minX+4-1e-3&&b.pos.x+b.size.x/2<=map.bounds.maxX-4+1e-3&&b.pos.z-b.size.z/2>=map.bounds.minZ+4-1e-3&&b.pos.z+b.size.z/2<=map.bounds.maxZ-4+1e-3),
                "open edges: the sea runs 4 m past every side and a fall dies fast (killY "+map.bounds.killY+")");
            foreach(var b in map.blocks)Check(map.blocks.Any(c=>Math.Abs(c.pos.x+b.pos.x)<1e-3&&Math.Abs(c.pos.z+b.pos.z)<1e-3&&Math.Abs(c.pos.y-b.pos.y)<1e-3&&Math.Abs(c.size.x-b.size.x)<1e-3&&Math.Abs(c.size.y-b.size.y)<1e-3&&Math.Abs(c.size.z-b.size.z)<1e-3),"block "+b.id+" has its twin across the centre");
            Columns(map);Nav(map);Hazards(map,maps);
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,map.look);
            Check(look!=null&&look.id==map.id&&NativeMapStyle.On(look,false)&&look.advanced!=null&&look.blocks.dressed&&look.lights.Length==8&&look.farClip>=1200,"the look: styled, dressed, advanced, eight lights, a far plane past the shore");
            Check(look.blockLooks.All(b=>b.block.StartsWith("material:",StringComparison.Ordinal)||map.blocks.Any(k=>NativeBlockLooks.Glob(b.block,k.id))),"every block look names real blocks");
            Check(map.blocks.All(b=>look.blockLooks.Any(l=>!l.block.StartsWith("material:",StringComparison.Ordinal)&&NativeBlockLooks.Glob(l.block,b.id))),"every block has a look of its own");
            Presentation(map,look);
            Wiring(root,maps);
            Console.WriteLine("PASS Pier 9: "+count+" checks");
        }

        // No invisible walls: a column stays for everyone only under art that hangs over a top (the rides' upper decks, the turrets,
        // the masts); every other column stands at a lethal drop and stops bots only.
        private static void Columns(ContentMap map)
        {
            var all=map.pilotBlocks.Where(p=>!p.bots).ToList();
            Check(all.Count==12&&all.All(p=>p.id.StartsWith("harbor-ride-",StringComparison.Ordinal)&&p.id.Contains("-deck-")||p.id.StartsWith("harbor-turret-",StringComparison.Ordinal)||p.id.StartsWith("harbor-tower-",StringComparison.Ordinal)),
                "twelve columns for everyone: six ride decks, four turrets, two masts ("+all.Count+")");
            Check(map.pilotBlocks.Where(p=>p.bots).All(p=>p.id.StartsWith("harbor-rail-",StringComparison.Ordinal))&&map.pilotBlocks.Count(p=>p.bots)>=40,"every other column is a rail's at a lethal drop, for bots only");
        }

        private static void Nav(ContentMap map)
        {
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
            string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
            // every level of the map is on the graph: the catwalk under the boards, the boardwalk, the roof terraces
            foreach(var y in new[]{-2.6f,0f,4.2f})Check(seen.Any(n=>Math.Abs(nodes[n].pos.y-.9f-y)<.05f),"nav reaches level "+y);
            // both piers, joined: nodes on each side of the channel and on the bridges
            Check(seen.Any(n=>nodes[n].pos.z<-20)&&seen.Any(n=>nodes[n].pos.z>20)&&map.nav.links.Count(l=>l.hazards.Contains("harbor-coaster"))>=20,"the bridges cross the coaster's lane (gated links)");
        }

        private static void Hazards(ContentMap map,UnityMapContent maps)
        {
            var hz=map.hazards;Check(hz.Count(h=>h.kind=="press")==2&&hz.Count(h=>h.kind=="maglev")==1&&hz.Length==3,"the coaster and two drop towers");
            foreach(var h in hz)
            {
                for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)
                {
                    if(h.kind=="maglev")Check(NativeCyberCityHazards.At(h,NativePhase.Fight,round,0,t).Phase==NativeHazardPhase.Idle,"the coaster idles through the arm delay");
                    else Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
                }
                if(h.kind=="press")Check(h.warn>=3&&h.lethal&&Math.Abs(h.b.y-h.size.y/2)<.01f,"a drop's 3 s telegraph and its y-0 floor (the press kind's lethal band is absolute) "+h.id);
                if(h.kind=="maglev")Check(h.warn>=6&&h.danger>=3&&h.first-h.warn>=4&&h.lethal&&Math.Abs(h.axis.x-1)<1e-4,"the coaster's warning");
            }
            // a drop's lethal window: from the last of its fall through its hold and the first of its climb, never in its telegraph
            foreach(var h in hz.Where(h=>h.kind=="press"))
            {
                double open=h.period-h.warn-h.rise-h.dwell-h.travel,impact=h.start+h.offset+open+h.warn+h.rise;
                Check(!NativeFactoryHazards.State(h,0,impact-h.rise-.1).Lethal&&NativeFactoryHazards.State(h,0,impact+.05).Lethal&&!NativeFactoryHazards.State(h,0,impact+h.dwell+h.travel+.05).Lethal,"the drop's lethal window "+h.id);
                var parked=NativePierLooks.DropAt(h,0,impact-h.rise-h.warn-1);Check(Math.Abs(parked.Y-(h.a.y-h.size.y/2))<1e-4&&parked.Warn==0&&!parked.Live,"the gondola waits up its mast "+h.id);
                var held=NativePierLooks.DropAt(h,0,impact+.1);Check(Math.Abs(held.Y)<1e-4&&held.Live&&Math.Abs(held.Since-.1)<1e-6,"the gondola on the boards "+h.id);
                Check(NativePierLooks.DropAt(h,0,double.NegativeInfinity).Y>10,"outside the fight the gondola is up");
                // the mast stands inside the square, the square on a boardwalk plate
                Check(map.blocks.Any(b=>b.id.StartsWith("harbor-tower-",StringComparison.Ordinal)&&Math.Abs(b.pos.x-h.b.x)<.01&&Math.Abs(b.pos.z-h.b.z)<.01&&b.size.x<h.size.x-2),"the drop's mast "+h.id);
            }
            // the train: the simulation's in the pass, never in the lane outside it, never jumping
            var c=hz.Single(h=>h.kind=="maglev");double half=c.size.x/2;
            Check(Math.Abs(NativePierLooks.Cars*NativePierLooks.CarLength-c.length)<1e-4,"five cars are the train's length");
            for(int round=0;round<2;round++)
            {
                NativePierLooks.Train last=default;bool any=false;int passes=0;bool was=false;
                for(double t=0;t<120;t+=1/120.0)
                {
                    var st=NativeCyberCityHazards.At(c,NativePhase.Fight,round,0,t);var tr=NativePierLooks.TrainAt(c,NativePhase.Fight,round,0,t);bool active=st.Phase==NativeHazardPhase.Active;
                    bool inLane=tr.Min<half-1e-6&&tr.Min+c.length>-half+1e-6;
                    Check(inLane==active||Math.Abs(st.Since)<.02||Math.Abs(st.Until)<.02,"the train is in the lane exactly through the pass (t "+t.ToString("0.000")+")");
                    if(active){double lo=Math.Min(st.Head,st.Tail);Check(Math.Abs(tr.Min-lo)<1e-6&&tr.Side==0,"in the pass the drawn train is the simulation's");if(!was)passes++;}
                    if(any)Check(Math.Abs(tr.Min-last.Min)<=c.speed/120*1.02+1e-6,"the train never jumps (t "+t.ToString("0.000")+", "+last.Min.ToString("0.00")+" to "+tr.Min.ToString("0.00")+")");
                    Check(Math.Abs(tr.Min)<=half+NativePierLooks.TrackTop&&Math.Abs(tr.Min+c.length)<=half+NativePierLooks.TrackTop,"the train stays on its track");
                    last=tr;any=true;was=active;
                }
                Check(passes==3,"three passes in two minutes, round "+round);
            }
            var rest=NativePierLooks.TrainAt(c,NativePhase.Intro,0,0,50);Check(rest.Side==-1&&rest.Speed==0&&NativePierLooks.TrainAt(c,NativePhase.Intro,1,0,50).Side==1,"outside the fight the train stands on the spike its first pass comes from");
            double px=0,py=0;for(double s=-half-NativePierLooks.TrackTop;s<=half+NativePierLooks.TrackTop;s+=.25)
            {
                NativePierLooks.Track(s,half,out double x,out double y,out double pitch);if(s>-half-NativePierLooks.TrackTop)Check(Math.Abs(Math.Sqrt((x-px)*(x-px)+(y-py)*(y-py))-.25)<.01,"the track's coordinate is its length");
                Check(Math.Abs(s)<=half+NativePierLooks.TrackLevel?Math.Abs(y-NativePierLooks.RailY)<1e-9&&pitch==0:y>NativePierLooks.RailY&&Math.Abs(x)>half+NativePierLooks.TrackLevel,"level across the pier and under the wheel, then up the spikes");px=x;py=y;
            }
            Check(half+NativePierLooks.TrackLevel>-NativePierLooks.WheelX+3,"the level run passes under the Ferris wheel");
            // spawns: 3 m (plan) from every live volume spanning their body on their floor
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))
            {
                double fy=p.pos.y-1.1;
                foreach(var h in hz)
                {
                    double x0,x1,z0,z1,y0,y1;
                    if(h.kind=="press"){x0=h.b.x-h.size.x/2;x1=h.b.x+h.size.x/2;z0=h.b.z-h.size.z/2;z1=h.b.z+h.size.z/2;y0=h.b.y-h.size.y/2;y1=h.a.y+h.size.y/2;}
                    else{x0=h.center.x-h.size.x/2;x1=h.center.x+h.size.x/2;z0=h.center.z-h.size.z/2;z1=h.center.z+h.size.z/2;y0=h.center.y-h.size.y/2;y1=h.center.y+h.size.y/2;}
                    if(y0-.1>fy+1.8||y1<fy)continue;double dx=Math.Max(0,Math.Max(x0-p.pos.x,p.pos.x-x1)),dz=Math.Max(0,Math.Max(z0-p.pos.z,p.pos.z-z1));
                    Check(Math.Sqrt(dx*dx+dz*dz)>=3,"spawn "+p.pos.x+","+p.pos.z+" clear of "+h.id);
                }
            }
            // no rules change: no vent, no push, nothing but the two existing kinds
            Check(hz.All(h=>h.push==0&&string.IsNullOrEmpty(h.pushMode)),"no rules change");
            // the cues: each once however the frames cut the time
            var into=new FactoryCue[64];var scratch=new FactoryCue[128];var all=new List<(int,int,int)>();var whole=new List<(int,int,int)>();
            for(double t=0;t<90;t+=1/60.0){int n=NativePierLooks.Cues(hz,0,0,t,t+1/60.0,into,scratch);for(int i=0;i<n;i++)all.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            for(double t=0;t<90;t+=.5){int n=NativePierLooks.Cues(hz,0,0,t,t+.5,into,scratch);for(int i=0;i<n;i++)whole.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            Check(all.Count==all.Distinct().Count()&&all.OrderBy(x=>x).SequenceEqual(whole.OrderBy(x=>x)),"each cue sounds once ("+all.Count+")");
            Check(all.Count(x=>x.Item1==NativePierLooks.CueDropSlam)>=6&&all.Count(x=>x.Item1==NativePierLooks.CueDropWarn)>=6&&all.Count(x=>x.Item1==NativePierLooks.CueCoasterPass)>=2&&all.Count(x=>x.Item1==NativePierLooks.CueCoasterWarn)>=2,"every kind of cue sounds");
        }

        private static void Presentation(ContentMap map,MapLook look)
        {
            // the backdrop: builds (its sink throws on any box in the play space above the sea)
            var sectors=NativePierScenery.Build(map);Check(sectors!=null&&sectors.Length==NativePierScenery.Sectors&&sectors.All(s=>s.Vertices>0),"the backdrop builds");
            // the kit: attached parts inside their blocks grown by Proud; thin posts on their blocks; free parts overhead or where no floor is
            var kit=NativePierKit.Build(map,look);Check(kit!=null&&kit.Count>2500&&kit.Count<NativeFactoryKit.MaxParts&&kit.Count==kit.Parent.Length,"the kit builds ("+kit?.Count+" parts)");
            var floors=Floors(map);var outside=new List<string>();int attached=0,free=0,thin=0;
            for(int i=0;i<kit.Count;i++)
            {
                int parent=kit.Parent[i];var p=Of(kit.Parts.Items[i]);
                if(parent>=0){attached++;var b=map.blocks[parent];if(!Within(p,Of(b),NativePierKit.Proud+1e-3)&&outside.Count<12)outside.Add(b.id+" #"+i+" "+Show(p));continue;}
                if(parent==NativePierKit.Thin)
                {
                    thin++;bool slender=p.X1-p.X0<=.16+1e-6&&p.Z1-p.Z0<=.16+1e-6,stands=map.blocks.Any(b=>Math.Abs(b.pos.y+b.size.y/2-p.Y0)<.02&&p.X0>=b.pos.x-b.size.x/2&&p.X1<=b.pos.x+b.size.x/2&&p.Z0>=b.pos.z-b.size.z/2&&p.Z1<=b.pos.z+b.size.z/2);
                    if(!(slender&&stands)&&outside.Count<12)outside.Add("thin #"+i+" "+Show(p));continue;
                }
                free++;
                foreach(var f in floors)
                {
                    if(p.X1<=f.x0-.05||p.X0>=f.x1+.05||p.Z1<=f.z0-.05||p.Z0>=f.z1+.05)continue;
                    if(p.Y0>=f.top+NativePierKit.Clearance-1e-3||p.Y1<=f.top-.03)continue;
                    if(outside.Count<12)outside.Add("free #"+i+" "+Show(p)+" over the top at "+f.top.ToString("0.00"));break;
                }
            }
            Check(outside.Count==0,"attached parts stay within their blocks grown by "+NativePierKit.Proud+", thin posts stand on theirs, free parts hang "+NativePierKit.Clearance+" m over every standable top or under it: "+string.Join("; ",outside));
            Check(attached>1500&&free>400&&thin==10,"attached, free and thin parts ("+attached+", "+free+", "+thin+")");
            // the rig and the effects: the replicated state's, deterministic, telegraphs never reduced, allocation free per frame
            var looks=NativePierLooks.Resolve(look.kitLooks);var rig=new NativePierRig(looks,map);var fx=new NativePierFx();fx.Prepare(map);
            Check(rig.Signals==6,"the rig finds the crossing's signals");
            double fight=10;var drop=map.hazards.First(h=>h.kind=="press");var coaster=map.hazards.Single(h=>h.kind=="maglev");
            double impact=fight+NativeFactoryHazards.ArmDelay+drop.start+drop.offset+(drop.period-drop.warn-drop.rise-drop.dwell-drop.travel)+drop.warn+drop.rise,pass=fight+coaster.first;
            foreach(double t in new[]{fight+1,fight+12,pass-6,pass-2,pass+.4,pass+1.2,pass+2.5,pass+8,impact-1.5,impact-.2,impact+.1,impact+1,impact+3,fight+58})
            {
                FactoryPart[] Parts(){rig.Build(map.hazards,3,NativePhase.Fight,fight,t,t);return rig.Parts.Items.Take(rig.Parts.Count).ToArray();}
                Check(Parts().SequenceEqual(Parts()),"the rig is deterministic at "+t);
                int Tele(double q,bool low){fx.Build(map.hazards,3,NativePhase.Fight,fight,t,t,0,2,-6,1,0,0,0,1,0,q,low);return fx.Telegraph;}
                int full=Tele(1,false);Check(full==Tele(0,false)&&full==Tele(0,true),"telegraphs are never reduced at "+t);
                Check(fx.Quads.Count<NativePierFx.Capacity,"the effects fit their buffer at "+t);
            }
            fx.Build(map.hazards,3,NativePhase.Fight,fight,impact-1.5,impact-1.5,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>=18,"a drop's telegraph draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,pass-5,pass-5,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>=16,"the coaster's warning draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph==0&&fx.Fireworks>0&&fx.Lights>60&&fx.Atmosphere>20,"nothing telegraphs in the arm delay; the fireworks, the lights and the fog draw");
            var beams=new NativeBeamList();fx.Beams=beams;fx.Build(map.hazards,3,NativePhase.Fight,fight,pass+.5,pass+.5,0,2,-6,1,0,0,0,1,0,1,false);Check(beams.Count>=3,"the beams take the lighthouse's and the train's shafts when the look's beams are on");fx.Beams=null;
            // the set pieces: the wheel wholly off the west side; the mounts and dolls over their rides' upper decks; the boats outside
            var b0=map.bounds;
            for(double t=0;t<200;t+=2.3)
            {
                rig.Build(map.hazards,0,NativePhase.Fight,0,t,t);int wheel=0,mounts=0,dolls=0,boats=0,cabins=0;
                for(int i=0;i<rig.Parts.Count;i++)
                {
                    int slot=rig.Slot[i];var p=Of(rig.Parts.Items[i]);
                    if(slot==NativePierArt.Wheel){wheel++;Check(p.X1<b0.minX-1,"the wheel's hub off the west side");}
                    else if(slot>=NativePierArt.Cabin&&slot<NativePierArt.Cabin+4){cabins++;Check(p.X1<b0.minX-1&&p.Y0>2.4,"a cabin off the west side, clear over the track");}
                    else if(slot==NativePierArt.Mount||slot==NativePierArt.Mount+1){mounts++;Check(p.Y0>=NativePierLooks.RideDeck+.3&&Math.Abs((p.X0+p.X1)/2-NativePierLooks.CarouselX)<3.6&&Math.Abs((p.Z0+p.Z1)/2-NativePierLooks.CarouselZ)<3.6,"a mount rides over the carousel's upper deck");}
                    else if(slot==NativePierArt.Doll){dolls++;Check(p.Y0>=NativePierLooks.RideDeck&&Math.Abs((p.X0+p.X1)/2-NativePierLooks.BandX)<3.8&&Math.Abs((p.Z0+p.Z1)/2-NativePierLooks.BandZ)<3.8,"a doll stands on the band stand's cornice");}
                    else if(slot==NativePierArt.Boat){boats++;Check(p.X0>b0.maxX+1||p.X1<b0.minX-1,"a boat rides outside the bounds");}
                }
                Check(wheel==1&&cabins==NativePierLooks.Gondolas&&mounts==NativePierLooks.Mounts&&dolls==NativePierLooks.Dolls&&boats==4,"the wheel, its sixteen cabins, ten mounts, eight dolls and four boats every frame");
            }
            double lo=1e9,hi=-1e9;for(double t=0;t<20;t+=.05){NativePierLooks.Mount(0,t,out _,out double y,out _,out _,out _);lo=Math.Min(lo,y);hi=Math.Max(hi,y);}
            Check(hi-lo>.6,"the mounts rise and fall ("+(hi-lo).ToString("0.00")+" m)");
            Check(Math.Abs(NativePierLooks.WheelAngle(40)-NativePierLooks.WheelAngle(0))>1,"the wheel turns");
            // the hazards' machines: the train's cars inside the lane's box through the pass; the gondola inside its square
            rig.Build(map.hazards,0,NativePhase.Fight,0,coaster.first+.9,coaster.first+.9);
            for(int i=0;i<rig.Parts.Count;i++)if(rig.Slot[i]==NativePierArt.Car||rig.Slot[i]==NativePierArt.CarEnd){var p=Of(rig.Parts.Items[i]);Check(p.Z0>=coaster.center.z-coaster.size.z/2&&p.Z1<=coaster.center.z+coaster.size.z/2&&p.Y1<=coaster.center.y+coaster.size.y/2,"a car inside the lane's box");}
            Check(Enumerable.Range(0,rig.Parts.Count).Count(i=>rig.Slot[i]==NativePierArt.Car)==3&&Enumerable.Range(0,rig.Parts.Count).Count(i=>rig.Slot[i]==NativePierArt.CarEnd)==2,"three cars between two end cars");
            double held=NativeFactoryHazards.ArmDelay+drop.start+drop.offset+(drop.period-drop.warn-drop.rise-drop.dwell-drop.travel)+drop.warn+drop.rise+.1;rig.Build(map.hazards,0,NativePhase.Fight,0,held,held);
            int ring=Enumerable.Range(0,rig.Parts.Count).First(i=>rig.Slot[i]==NativePierArt.Ring);
            for(int i=ring;i<ring+17;i++){var p=Of(rig.Parts.Items[i]);Check(p.X0>=drop.b.x-drop.size.x/2-1e-3&&p.X1<=drop.b.x+drop.size.x/2+1e-3&&p.Z0>=drop.b.z-drop.size.z/2-1e-3&&p.Z1<=drop.b.z+drop.size.z/2+1e-3&&p.Y0>=-1e-3&&p.Y1<=drop.size.y+1e-3,"the gondola's part "+(i-ring)+" inside the drop's volume "+Show(p));}
            for(int warm=0;warm<3;warm++){rig.Build(map.hazards,3,NativePhase.Fight,fight,impact,impact);fx.Build(map.hazards,3,NativePhase.Fight,fight,impact,impact,0,2,-6,1,0,0,0,1,0,1,false);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++){double t=fight+f*.25;rig.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t);fx.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t,f*.1,2,-6,1,0,0,0,1,0,f%2,f%3==0);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"the rig and effects allocate nothing per frame ("+(after-before)+" bytes)");
            // the numbers (printed, not pinned)
            int rigMax=0,fxMax=0,fxLow=0;var sw=System.Diagnostics.Stopwatch.StartNew();int frames=0;
            for(double t=fight;t<fight+60;t+=.1,frames++){rig.Build(map.hazards,1,NativePhase.Fight,fight,t,t);rigMax=Math.Max(rigMax,rig.Parts.Count);fx.Build(map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,1,false);fxMax=Math.Max(fxMax,fx.Quads.Count);}
            double ms=sw.Elapsed.TotalMilliseconds/frames;
            for(double t=fight;t<fight+60;t+=.5){fx.Build(map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,0,true);fxLow=Math.Max(fxLow,fx.Quads.Count);}
            int verts=sectors.Sum(m=>m.Vertices),tris=sectors.Sum(m=>m.Indices.Length/3);
            Check(rigMax<NativePierRig.Capacity&&rigMax<2400&&fxMax<NativePierFx.Capacity&&fxLow<fxMax,"the frame's parts and quads fit and the low tier draws fewer");
            Console.WriteLine("   pier 9: kit "+kit.Count+" parts; rig up to "+rigMax+" parts a frame; effects up to "+fxMax+" quads (low tier "+fxLow+"); backdrop "+verts+" vertices, "+tris+" triangles in "+sectors.Length+" meshes; rig + effects "+ms.ToString("0.00")+" ms a frame (CoreTests, Release)");
            // another map draws nothing of this
            var other=new NativePierFx();other.Prepare(new ContentMap{id="polar"});other.Build(map.hazards,0,NativePhase.Fight,0,30,30,0,0,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no Pier 9 effects");
            Check(NativePierScenery.Build(new ContentMap{id="polar"})==null&&NativePierKit.Build(new ContentMap{id="polar"},look)==null,"another map builds no Pier 9 scenery or kit");
        }

        private static void Wiring(string root,UnityMapContent maps)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("PierScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmPier(arena))built++;"),"SourceWorld draws it and PrewarmStage builds it");
            Check(R("Runtime/NativeAudio.cs").Contains("PierCues(arena,menu);"),"the cues play");
            // off the five M1 stages' shared code: their presentation is inert here, and the other four keep their numbers
            var map=maps.maps.Single(m=>m.id=="harbor");
            Check(!NativeNewStageLooks.Active("harbor")&&NativeNewStageLooks.Index("harbor")<0&&NativeNewStageScenery.Build(map)==null&&NativeNewStageKit.Build(map,null,null)==null,"the M1 stages' presentation leaves Pier 9 alone");
            Check(NativeNewStageLooks.Maps.All(id=>NativeNewStageLooks.Index(id)==Array.IndexOf(new[]{"breakwater","harbor","skyframe","rig","polar"},id)),"the other M1 stages keep their numbers");
            Check(NativeStageMusic.Theme("harbor","x")=="harbor-theme","the owner's song for the pier plays (Last Night of the Season, 2026-11-11)");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativePierLooks.cs","Core/NativePierScenery.cs","Core/NativePierKit.cs","Core/NativePierFx.cs","Core/NativePierArt.cs","Runtime/NativePierPresentation.cs"})
            {
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");var g=System.Text.RegularExpressions.Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+": a guid of its own");
            }
            var all=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Where(f=>!f.Contains("NativePier")).Select(f=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f),"guid: ([0-9a-f]{32})").Groups[1].Value);
            Check(!all.Any(g=>guids.Contains(g)),"no other asset shares the new guids");
        }
    }
}
#endif
