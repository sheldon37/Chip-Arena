#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The five M1 stages' looks (docs/arenas/NEW-STAGES.md "Stage looks (presentation)"; Core NativeNewStage*, Runtime
    // NativeNewStagePresentation, MapLooks.json and PackDressing.json). Against the real maps and the committed data: every
    // stage's look resolves and is styled; its block and kit looks name real blocks and known kinds; the kit's attached parts stay
    // inside their blocks (the collider rule) and its overhead structure clears every standable top under it; the pack rules
    // dress real blocks inside their colliders (a synthetic bank of the props' real bounds) and the yards and the backdrop stay
    // out of the play space; the rigs are the replicated hazard state's (the simulation's own hooks and loads, parked before
    // the arm delay, the same on host, guest and killcam); telegraphs are never reduced; everything is deterministic and
    // allocation free per frame; the runtime is wired; every other map is untouched.
    internal static class NewStageLookChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("New stage looks: "+what);count++;}
        private struct Box3 {public double X0,X1,Y0,Y1,Z0,Z1;}
        private static Box3 Of(ContentBlock b)=>new Box3{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2};
        private static Box3 Of(in FactoryPart p)
        {
            var q=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var b=new Box3{X0=double.MaxValue,X1=double.MinValue,Y0=double.MaxValue,Y1=double.MinValue,Z0=double.MaxValue,Z1=double.MinValue};
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?-p.Hx:p.Hx,(c&2)==0?-p.Hy:p.Hy,(c&4)==0?-p.Hz:p.Hz),q);b.X0=Math.Min(b.X0,p.X+v.X);b.X1=Math.Max(b.X1,p.X+v.X);b.Y0=Math.Min(b.Y0,p.Y+v.Y);b.Y1=Math.Max(b.Y1,p.Y+v.Y);b.Z0=Math.Min(b.Z0,p.Z+v.Z);b.Z1=Math.Max(b.Z1,p.Z+v.Z);}
            return b;
        }
        private static bool Within(Box3 a,Box3 b,double g)=>a.X0>=b.X0-g&&a.X1<=b.X1+g&&a.Y0>=b.Y0-g&&a.Y1<=b.Y1+g&&a.Z0>=b.Z0-g&&a.Z1<=b.Z1+g;
        private static bool OverlapXZ(Box3 a,Box3 b)=>a.X1>b.X0+1e-4&&a.X0<b.X1-1e-4&&a.Z1>b.Z0+1e-4&&a.Z0<b.Z1-1e-4;
        // The tops a pilot can stand on: block tops no pilot column covers (columns block from their bottom upward).
        private static List<(Box3 box,double top)> Standable(ContentMap map)
        {
            var o=new List<(Box3,double)>();
            foreach(var b in map.blocks)
            {
                var x=Of(b);bool covered=false;
                foreach(var p in map.pilotBlocks??Array.Empty<MapPilotBlock>()){if(p==null)continue;double hx=p.size.x/2,hz=p.size.z/2;if(p.center.x-hx<=x.X0+.05&&p.center.x+hx>=x.X1-.05&&p.center.z-hz<=x.Z0+.05&&p.center.z+hz>=x.Z1-.05&&p.bottom<=x.Y1+.3){covered=true;break;}}
                // a top another block rests on (a runway on its column, a roof on its wall) has no room to stand
                if(!covered)foreach(var c in map.blocks){if(ReferenceEquals(c,b))continue;var y=Of(c);if(y.Y0>=x.Y1-.01&&y.Y0<=x.Y1+1.0&&y.X0<=x.X0+.05&&y.X1>=x.X1-.05&&y.Z0<=x.Z0+.05&&y.Z1>=x.Z1-.05){covered=true;break;}}
                if(!covered)o.Add((x,x.Y1));
            }
            return o;
        }
        private static bool InBounds(ContentMap map,Box3 a,double margin)=>a.X1>map.bounds.minX-margin&&a.X0<map.bounds.maxX+margin&&a.Z1>map.bounds.minZ-margin&&a.Z0<map.bounds.maxZ+margin;
        // An overhead part: outside the play space, or at least the clearance above every standable top under it.
        private static bool Clear(ContentMap map,List<(Box3 box,double top)> stand,Box3 a,double clearance)
        {
            if(!InBounds(map,a,NativeNewStageScenery.Margin))return true;
            foreach(var (box,top) in stand)if(OverlapXZ(a,box)&&top<=a.Y0+.01&&a.Y0<top+clearance)return false;
            return a.Y0>=clearance||a.Y1<=-.5;
        }
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));string Rt(string f)=>R("Assets/ChipArena/Runtime/"+f);
            var content=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o);var gc=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);
            var mats=gc.materials.Concat(content.materials??new ContentMaterial[0]).GroupBy(m=>m.id).Select(g=>g.First()).ToArray();
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);
            Check(NativeNewStageLooks.Kinds.Length<=NativeFactoryKit.Kinds.Length&&NativeNewStageLooks.Defaults.Length==NativeNewStageLooks.Kinds.Length&&NativeNewStageLooks.Kinds.Distinct().Count()==NativeNewStageLooks.Kinds.Length,"the kit kinds fit NativeFactoryKit.Looks, each with a default");
            foreach(var (look,_) in NativeNewStageLooks.Defaults)Check(NativeBlockLooks.Id(look)>=0,"a default kit look is a known key: "+look);

            // 1. Every look resolves, styled, with the advanced lighting; block and kit looks name real blocks and known kinds.
            for(int i=0;i<NativeNewStageLooks.Maps.Length;i++)
            {
                string id=NativeNewStageLooks.Maps[i];var map=content.maps.Single(m=>m.id==id);var look=NativeMapLooks.ByLook(set,map.look);
                Check(look!=null&&look.id==id&&map.look==NativeNewStageLooks.Looks[i]&&ReferenceEquals(NativeMapLooks.Find(set,id),look),id+": its look "+map.look+" is its own MapLooks entry");
                Check(NativeBlockLooks.Dressed(look,false)&&NativeMapStyle.On(look,false)&&NativeAdvancedLooks.BeamsOn(look)&&NativeAdvancedLooks.ScatterOn(look)&&NativeAdvancedLooks.GiOn(look),id+": dressed blocks, the anime style and the advanced lighting");
                var styled=NativeMapStyle.Styled(look,false);var st=new MapLookState();NativeMapLooks.Applied(styled,map.sky,false,st);Check(st.Skybox&&st.Fog.On&&st.FarClip>=st.Fog.End,id+": a sky, fog and a far plane past the fog");
                Check((look.lights?.Length??0)>0&&look.lights.Length<=NativeMapLooks.MaxLights,id+": practical lights");
                var t=look.toon;NativeMapStyle.Validate(id,t);
                Check(t.line>0&&t.lineFadeEnd<=look.fog.end&&t.skyToon==1&&t.saturation>=1&&t.saturation<=1.2f&&t.detail<1&&t.shade.z>t.shade.x&&t.copings==1&&t.posts==1&&t.specular>0&&t.occlusion>0,id+": the style as the stage 2 maps take it (outline faded within the fog, the anime sky, a light grade, cool shade, copings and posts)");
                Check(t.key.bearing==look.key.bearing&&t.key.elevation==look.key.elevation&&t.sky.variant==look.sky.variant&&t.sky.bearing==look.sky.bearing,id+": the style keeps the stage's own key direction and time of day");
                foreach(var e in look.blockLooks)
                {
                    if(e.block.StartsWith("material:",StringComparison.Ordinal))continue;
                    Check(map.blocks.Any(b=>NativeBlockLooks.Score(e.block,b.id,b.material)>=1&&ReferenceEquals(Best(look.blockLooks,b),e)),id+": the block look "+e.block+" decides some block");
                }
                foreach(var e in look.kitLooks??Array.Empty<MapLookBlock>())Check(NativeNewStageLooks.Kinds.Contains(e.block),id+": the kit look "+e.block+" is a kit kind");
                foreach(var b in map.blocks)Check(NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0).Listed,id+": "+b.id+" has a look of its own (listed or by its material)");
            }
            string weatherOf(string id)=>NativeAdvancedLooks.WeatherOf(NativeMapLooks.Find(set,id));
            Check(weatherOf("rig")=="rain"&&NativeMapLooks.Find(set,"rig").advanced.weather.lightning>0&&weatherOf("polar")=="snow"&&weatherOf("skyframe")==""&&weatherOf("harbor")=="drizzle"&&weatherOf("breakwater")=="drizzle","the weather: Deepwell's storm, Polaris's snow, a clear noon, Pier 9's and Breakwater's wet air");
            Check(NativeMapLooks.Find(set,"rig").lights.Any(l=>l.id=="flare"&&l.flicker=="fire"&&l.range>=60),"Deepwell's flare lights the north faces and flickers");

            // 2. The kit: attached parts inside their blocks (2.1 rule 1, the stage rule's 0.25 m), overhead parts clear of the play.
            int attached=0,overhead=0;
            foreach(var id in NativeNewStageLooks.Maps)
            {
                var map=content.maps.Single(m=>m.id==id);var look=NativeMapStyle.Styled(NativeMapLooks.Find(set,id),false);var kit=NativeNewStageKit.Build(map,look,mats);var again=NativeNewStageKit.Build(map,look,mats);var stand=Standable(map);
                Check(kit.Count>100&&kit.Parent.Length==kit.Count&&kit.Overhead.Length==kit.Count,id+": "+kit.Count+" kit parts");
                Check(again.Count==kit.Count&&Enumerable.Range(0,kit.Count).All(i=>kit.Parts.Items[i].X==again.Parts.Items[i].X&&kit.Parts.Items[i].Y==again.Parts.Items[i].Y&&kit.Parts.Items[i].Color==again.Parts.Items[i].Color&&kit.Parts.Items[i].Look==again.Parts.Items[i].Look),id+": the kit is the same every time");
                for(int i=0;i<kit.Count;i++)
                {
                    var p=kit.Parts.Items[i];var a=Of(p);
                    if(kit.Overhead[i]){overhead++;Check(kit.Parent[i]<0&&Clear(map,stand,a,NativeNewStageKit.Clearance),id+": an overhead part at "+p.X.ToString("0.#")+", "+p.Y.ToString("0.#")+", "+p.Z.ToString("0.#")+" stands "+NativeNewStageKit.Clearance+" m over every top under it");continue;}
                    attached++;Check(kit.Parent[i]>=0&&Within(a,Of(map.blocks[kit.Parent[i]]),NativeNewStageKit.Proud+1e-3),id+": a kit part of "+(kit.Parent[i]>=0?map.blocks[kit.Parent[i]].id:"?")+" stays inside it (grown "+NativeNewStageKit.Proud+" m)");
                }
                Check(NativeNewStageKit.Build(new ContentMap{id="factory",blocks=map.blocks},look,mats)==null,"another map gets no kit");
            }
            Check(attached>3000&&overhead>150,"attached and overhead parts on the five ("+attached+", "+overhead+")");

            // 3. The pack rules: real blocks, solid props, inside their colliders, the same every time (a synthetic bank of real bounds).
            var table=JsonSerializer.Deserialize<PackDressingSet>(R("Assets/Conversion/Resources/PackDressing.json"),o);NativePackDressing.Validate(table);
            using var sel=JsonDocument.Parse(R("tools/pack_bank/selections/kit-harbor.json"));var groups=new Dictionary<string,string>();
            foreach(var p in sel.RootElement.GetProperty("props").EnumerateArray())groups[p.GetProperty("id").GetString()]=p.GetProperty("group").GetString();
            var bank=StageKitChecks.Synthetic(groups);
            var dressed=new Dictionary<string,int>{["breakwater"]=56,["harbor"]=35,["skyframe"]=34,["rig"]=8,["polar"]=24};
            foreach(var id in NativeNewStageLooks.Maps)
            {
                var entry=NativePackDressing.Entry(table,id);var map=content.maps.Single(m=>m.id==id);Check(entry!=null,id+": a pack entry");
                foreach(var r in entry.rules)Check(map.blocks.Any(b=>NativePackDressing.Rule(entry,b.id)==r),id+": the rule "+r.block+" dresses blocks");
                foreach(var r in entry.rules)foreach(var n in NativePackDressing.Kit(table,r.kit).props)Check(groups.TryGetValue(n,out var g)&&NativePackDressing.Solid.Contains(g)&&bank.PropIndex.ContainsKey(n),id+": "+n+" is a selected solid prop with known bounds");
                var plan=NativePackDressing.Plan(table,map,bank);Check(plan.Dressed==dressed[id]&&plan.Kept.Count==0,id+": "+plan.Dressed+" blocks as props (expected "+dressed[id]+"), kept: "+string.Join("; ",plan.Kept));
                StageKitChecks.Fits(plan,map,bank,id);
                foreach(var d in plan.ById.Values){var b=map.blocks[d.Block];Check(b.role=="static"||b.role=="cover",id+" "+b.id+": a static or cover block");}
                // 4. The yards: outside the blocks' keep-out and the bounds (the play space), the same every time.
                var scene=NativePackScenery.Build(entry,map,bank);var twice=NativePackScenery.Build(entry,map,bank);
                Check(scene.Props.Length==twice.Props.Length&&scene.Props.Select((p,i)=>p.Prop==twice.Props[i].Prop&&p.World==twice.Props[i].World).All(x=>x),id+": the yards are the same every time");
                Check((entry.yards.Length==0)==(id=="skyframe"||id=="rig")&&(entry.yards.Length==0||scene.Props.Length>20),id+": yards where the ground is at y 0 ("+scene.Props.Length+" props)");
                foreach(var p in scene.Props){NativePackDressing.Bounds(bank.Props[p.Prop],p.World,out var lo,out var hi);var a=new Box3{X0=lo.X,X1=hi.X,Y0=lo.Y,Y1=hi.Y,Z0=lo.Z,Z1=hi.Z};Check(!NativePackScenery.Enters(scene,lo.X,hi.X,lo.Z,hi.Z)&&!InBounds(map,a,NativeNewStageScenery.Margin),id+": the yard's "+bank.Props[p.Prop].Name+" stays outside the play space");}
            }

            // 5. The backdrop: built with and without the bank's yards (Pier 9's box cranes and stacks stand down), out of the play space.
            foreach(var id in NativeNewStageLooks.Maps)
            {
                var map=content.maps.Single(m=>m.id==id);var a=NativeNewStageScenery.Build(map);var b=NativeNewStageScenery.Build(map);var y=NativeNewStageScenery.Build(map,true);
                Check(a.Length==NativeNewStageScenery.Sectors&&a.All(m=>m.Indices.All(i=>i>=0&&i<m.Vertices)&&m.Indices.Length%3==0),id+": "+a.Sum(m=>m.Indices.Length/3)+" backdrop triangles in "+a.Length+" sectors");
                Check(a.Select((m,i)=>m.Positions.SequenceEqual(b[i].Positions)&&m.Colors.SequenceEqual(b[i].Colors)&&m.Emission.SequenceEqual(b[i].Emission)).All(x=>x),id+": the backdrop is the same every time");
                Check(id=="harbor"?y.Sum(m=>m.Boxes)<a.Sum(m=>m.Boxes):y.Sum(m=>m.Boxes)==a.Sum(m=>m.Boxes),id+": with the bank's yards only Pier 9's box cranes and stacks stand down");
                NativeNewStageScenery.PlaySpace(map,out double x0,out double x1,out double z0,out double z1,out double under);
                foreach(var m in a)for(int i=0;i+2<m.Positions.Length;i+=3){double px=m.Positions[i],py=m.Positions[i+1],pz=m.Positions[i+2];Check(py<=under+1e-3||px<=x0+1e-3||px>=x1-1e-3||pz<=z0+1e-3||pz>=z1-1e-3,id+": a backdrop vertex at "+px.ToString("0.#")+", "+py.ToString("0.#")+", "+pz.ToString("0.#")+" stays out of the play space");}
            }
            {
                var bw=content.maps.Single(m=>m.id=="breakwater");var s=new NativeNewStageScenery.Sink{Map="probe"};NativeNewStageScenery.PlaySpace(bw,out s.MinX,out s.MaxX,out s.MinZ,out s.MaxZ,out s.Under);bool threw=false;
                try{s.Box(0,1,0,2,2,2,0xffffff);}catch(InvalidOperationException){threw=true;}Check(threw,"a backdrop box in the play space is refused");s.Box(0,-3,0,10,1,10,0xffffff);s.Box(40,4,0,2,8,2,0xffffff);Check(s.Boxes==2,"under the floor and outside the bounds are allowed");
            }

            // 6. The rigs: the replicated hazard state's, parked before the arm delay, the same whatever FightAt and on every client.
            foreach(var id in new[]{"harbor","skyframe","rig"})
            {
                var map=content.maps.Single(m=>m.id==id);var look=NativeMapLooks.Find(set,id);var rig=new NativeNewStageRig(NativeNewStageLooks.Resolve(look.kitLooks));var other=new NativeNewStageRig(NativeNewStageLooks.Resolve(look.kitLooks));var stand=Standable(map);
                for(int round=0;round<2;round++)for(double t=0;t<70;t+=.25)
                {
                    rig.Build(id,map.hazards,map.blocks,round,NativePhase.Fight,0,t,t);other.Build(id,map.hazards,map.blocks,round,NativePhase.Fight,123.5,123.5+t,t);
                    Check(rig.Parts.Count==other.Parts.Count&&Enumerable.Range(0,rig.Parts.Count).All(i=>rig.Parts.Items[i].X==other.Parts.Items[i].X&&rig.Parts.Items[i].Y==other.Parts.Items[i].Y&&rig.Parts.Items[i].Color==other.Parts.Items[i].Color),id+": the rig depends on the time since FightAt only (host, guest, killcam) at "+t);
                    double clock=NativeFactoryHazards.Clock(NativePhase.Fight,0,t);
                    for(int k=0;k<map.hazards.Length;k++)
                    {
                        var h=map.hazards[k];if(h.kind!="crane")continue;var s=NativeFactoryHazards.State(h,round,clock);
                        if(clock<h.start+h.offset)Check(s.Phase==NativeFactoryHazards.Parked&&Math.Abs(rig.LoadY[k]-h.carry)<1e-6,id+": the crane is parked and its load hoisted before its first lift");
                        bool pinned=s.Phase==NativeFactoryHazards.Warning||s.Phase==NativeFactoryHazards.Falling||s.Phase==NativeFactoryHazards.Landed||s.Phase==NativeFactoryHazards.Regrip||s.Phase==NativeFactoryHazards.Hoisting;
                        if(pinned)Check(rig.Exact[k]&&Math.Abs(rig.HookX[k]-s.X)<1e-6&&Math.Abs(rig.HookZ[k]-s.Z)<1e-6&&rig.LoadY[k]==s.LoadY,id+": from the warning to the hoist the drawn hook and load are the simulation's ("+s.Phase+" at "+t+")");
                        if(id=="harbor")Check(rig.Exact[k]&&Math.Abs(rig.HookX[k]-s.X)<1e-6,"Pier 9's gantry hook is always the simulation's");
                    }
                    if(round==0&&((int)(t*4))%8==0)for(int i=0;i<rig.Parts.Count;i++)
                    {
                        var a=Of(rig.Parts.Items[i]);byte role=rig.Role[i];
                        if(role==NativeNewStageRig.Overhead)Check(Clear(map,stand,a,NativeNewStageKit.Clearance),id+": a rig's overhead part at "+a.X0.ToString("0.#")+".."+a.X1.ToString("0.#")+", "+a.Y0.ToString("0.#")+", "+a.Z0.ToString("0.#")+" clears the tops under it ("+t+")");
                        else if(role==NativeNewStageRig.Floor)Check(a.Y1-a.Y0<.1&&map.blocks.Any(b=>{var x=Of(b);return Math.Abs(x.Y1-a.Y0)<.05&&a.X0>=x.X0-.6&&a.X1<=x.X1+.6&&a.Z0>=x.Z0-.6&&a.Z1<=x.Z1+.6;}),id+": a rig's floor lamp or grate lies flat on a floor");
                        else if(role==NativeNewStageRig.Attached)Check(map.blocks.Any(b=>Within(a,Of(b),NativeNewStageKit.Proud)),id+": a rig's beacon stays on its block");
                    }
                }
                // the tower crane travels by slewing: its drawn ends are the simulation's zones
                if(id=="skyframe")
                {
                    var h=map.hazards.Single();for(int n=1;n<5;n++){double c0=h.start+h.offset+n*h.period;var s0=NativeFactoryHazards.State(h,0,c0+1e-6);var s1=NativeFactoryHazards.State(h,0,c0+h.travel-1e-6);
                        NativeNewStageRig.Reach(h,s0,0,c0+1e-6,out double a0,out double r0);NativeNewStageRig.Reach(h,s1,0,c0+h.travel-1e-6,out double a1,out double r1);var f=h.zones[s0.From].center;var z=h.zones[s0.Zone].center;
                        Check(Math.Abs(Math.Sin(a0)*r0-f.x)<.02&&Math.Abs(Math.Cos(a0)*r0-f.z)<.02&&Math.Abs(Math.Sin(a1)*r1-z.x)<.02&&Math.Abs(Math.Cos(a1)*r1-z.z)<.02,"the tower crane slews from the zone it left to the one it serves (cycle "+n+")");
                        for(double u=0;u<h.travel;u+=.1){var s=NativeFactoryHazards.State(h,0,c0+u);NativeNewStageRig.Reach(h,s,0,c0+u,out _,out double r);Check(r>=NativeNewStageRig.MinReach-1e-9,"the hook never passes through the mast");}}
                }
                if(id=="rig")
                {
                    var burn=map.hazards.First(h=>h.kind=="burner");var idle=NativeWardZeroHazards.At(burn,NativePhase.Fight,0,0,1);var warn=NativeWardZeroHazards.At(burn,NativePhase.Fight,0,0,burn.first+burn.offset+.5);var live=NativeWardZeroHazards.At(burn,NativePhase.Fight,0,0,burn.first+burn.offset+burn.warn+.5);
                    Check(idle.Phase==NativeHazardPhase.Idle&&warn.Phase==NativeHazardPhase.Warn&&live.Phase==NativeHazardPhase.Active,"Deepwell's burner phases sampled");
                    Check(NativeNewStageRig.BeaconColor(idle,1)==NativeFactoryRig.LampSafe&&NativeNewStageRig.BeaconColor(live,1)==NativeFactoryRig.LampRed&&NativeNewStageRig.NozzleColor(idle,1,0)!=NativeNewStageRig.NozzleColor(live,1,0),"the burner beacon and nozzles follow the burst");
                }
            }

            // 7. The effects: telegraphs never reduced; deterministic; nothing for another map.
            foreach(var id in NativeNewStageLooks.Maps)
            {
                var map=content.maps.Single(m=>m.id==id);var a=new NativeNewStageFx();var b=new NativeNewStageFx();a.Prepare(map);b.Prepare(map);
                for(double t=4;t<60;t+=1.7)
                {
                    a.Build(map.hazards,0,NativePhase.Fight,0,t,t,-10,6,-12,1,0,0,0,1,0,1,false);int tel=a.Telegraph,full=a.Quads.Count;float[] corners=a.Quads.Corners.Take(full*12).ToArray();
                    b.Build(map.hazards,0,NativePhase.Fight,0,t,t,-10,6,-12,1,0,0,0,1,0,0,true);Check(b.Telegraph==tel,id+": telegraphs are never reduced by the quality or the tier ("+t+")");Check(b.Atmosphere<=a.Atmosphere,id+": the atmosphere is");
                    b.Build(map.hazards,0,NativePhase.Fight,0,t,t,-10,6,-12,1,0,0,0,1,0,1,false);Check(b.Quads.Count==full&&b.Quads.Corners.Take(full*12).SequenceEqual(corners),id+": the effects are a pure function of the clocks and camera");
                }
                Check(a.Quads.Count<=NativeNewStageFx.Capacity,id+": within capacity");
            }
            {
                var rigMap=content.maps.Single(m=>m.id=="rig");var fx=new NativeNewStageFx();fx.Prepare(rigMap);var burn=rigMap.hazards.First(h=>h.kind=="burner");
                double active=burn.first+burn.offset+burn.warn+.5;fx.Build(rigMap.hazards,0,NativePhase.Fight,0,active,active,0,6,15,1,0,0,0,1,0,0,true);int on=fx.Telegraph;fx.Build(rigMap.hazards,0,NativePhase.Fight,0,1,1,0,6,15,1,0,0,0,1,0,0,true);
                Check(on>fx.Telegraph,"a burning lane draws its fire columns ("+on+" telegraph quads)");
                var harbor=content.maps.Single(m=>m.id=="harbor");var gh=harbor.hazards.Single();var hfx=new NativeNewStageFx();hfx.Prepare(harbor);double warn=NativeFactoryHazards.ArmDelay+gh.start+gh.offset+gh.travel+1;
                hfx.Build(harbor.hazards,0,NativePhase.Fight,0,warn,warn,0,6,0,1,0,0,0,1,0,0,true);Check(hfx.Telegraph>=3,"Pier 9's gantry telegraphs its slot through the warning");
                var beams=new NativeBeamList();hfx.Beams=beams;beams.Clear();hfx.Build(harbor.hazards,0,NativePhase.Fight,0,warn,warn,0,6,0,1,0,0,0,1,0,1,false);Check(beams.Count>0,"with the volumetric beams on, the cones go to the beam list");
                var other=new NativeNewStageFx();other.Prepare(content.maps.Single(m=>m.id=="factory"));other.Build(harbor.hazards,0,NativePhase.Fight,0,warn,warn,0,6,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no effects");
                var none=new NativeNewStageRig(NativeNewStageLooks.Resolve(null));none.Build("factory",harbor.hazards,harbor.blocks,0,NativePhase.Fight,0,warn,warn);Check(none.Parts.Count==0,"another map draws no rig");
            }

            // 8. No allocation per frame (rigs and effects, warmed).
            foreach(var id in NativeNewStageLooks.Maps)
            {
                var map=content.maps.Single(m=>m.id==id);var look=NativeMapLooks.Find(set,id);var rig=new NativeNewStageRig(NativeNewStageLooks.Resolve(look.kitLooks));var fx=new NativeNewStageFx();fx.Prepare(map);fx.Beams=new NativeBeamList();
                for(int i=0;i<3;i++){rig.Build(id,map.hazards,map.blocks,0,NativePhase.Fight,0,i,i);fx.Beams.Clear();fx.Build(map.hazards,0,NativePhase.Fight,0,i,i,-10,6,-12,1,0,0,0,1,0,1,false);}
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<240;i++){double t=i*.25;rig.Build(id,map.hazards,map.blocks,i&1,NativePhase.Fight,0,t,t);fx.Beams.Clear();fx.Build(map.hazards,i&1,NativePhase.Fight,0,t,t,-10+i*.1,6,-12,1,0,0,0,1,0,i%3==0?.3:1,i%5==0);}
                long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,id+": the rigs and effects allocate nothing per frame ("+(after-before)+" bytes)");
            }

            // 9. Every other map is untouched; the runtime's wiring (source scans) and the new files' metas.
            foreach(var m in content.maps)Check(NativeNewStageLooks.Active(m.id)==NativeNewStageLooks.Maps.Contains(m.id)&&(NativeNewStageScenery.Build(m)==null)!=NativeNewStageLooks.Maps.Contains(m.id),m.id+": the new stages' presentation is only theirs");
            foreach(var m in gc.maps)Check(!NativeNewStageLooks.Active(m.id),m.id+": an old map is not one of the five");
            string src=Rt("NativeSourcePresentation.cs"),warm=Rt("NativeStagePrewarm.cs"),draw=Rt("NativeNewStagePresentation.cs");
            Check(src.Contains("            NewStageScenery(arena);\n")&&src.IndexOf("NewStageScenery(arena);",StringComparison.Ordinal)>src.IndexOf("AdvancedLightingBegin(arena);",StringComparison.Ordinal)&&src.IndexOf("NewStageScenery(arena);",StringComparison.Ordinal)<src.IndexOf("AdvancedLightingWorld(arena);",StringComparison.Ordinal),"SourceWorld draws the new stages between the advanced lighting's begin and world (the beam list is the frame's)");
            Check(warm.Contains("if(PrewarmNewStage(arena))built++;"),"the loading screen builds them");
            Check(draw.Contains("if(!NativeNewStageLooks.Active(arena.MapId))return;")&&draw.Contains("rigBuilder.Build(map.id,map.hazards,map.blocks,round,match.Phase,match.FightAt,now,now);")&&draw.Contains("newStage.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);"),"the rigs pose from the replicated phase and FightAt on the field clock, only on the five");
            Check(draw.Contains("NativePackStage.Strip(map,k.Parts);")&&draw.Contains("NativeNewStageScenery.Build(map,NativePackStage.Entry(map.id)?.yards?.Length>0)")&&draw.Contains("const int layer=NativeSceneryBuilder.BackdropLayer;")&&draw.Contains("ShadowCastingMode.Off,true);"),"the kit drops a dressed block's parts, the backdrop knows the yards and draws on the backdrop layer without shadows");
            Check(draw.Contains("newStage.Beams=AdvancedBeams;")&&draw.Contains("fx.Beams=Beams;"),"the effects take the frame's beam list");
            var guids=new HashSet<string>();foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories)){var g=File.ReadAllLines(f).FirstOrDefault(l=>l.StartsWith("guid: ",StringComparison.Ordinal));if(g!=null)Check(guids.Add(g),"unique meta guid: "+f);}
            foreach(var f in new[]{"Core/NativeNewStageLooks.cs","Core/NativeNewStageScenery.cs","Core/NativeNewStageKit.cs","Core/NativeNewStageRig.cs","Core/NativeNewStageFx.cs","Runtime/NativeNewStagePresentation.cs"})Check(File.Exists(Path.Combine(root,"Assets/ChipArena",f+".meta")),f+" has a meta");
            Console.WriteLine("PASS new stage looks: "+count+" checks");
        }
        private static MapLookBlock Best(MapLookBlock[] entries,ContentBlock b){MapLookBlock best=null;int score=-1;foreach(var e in entries){int s=NativeBlockLooks.Score(e.block,b.id??"",b.material??"");if(s>score){score=s;best=e;}}return best;}
    }
}
#endif
