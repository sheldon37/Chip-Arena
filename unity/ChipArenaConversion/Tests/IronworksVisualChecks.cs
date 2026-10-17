using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ChipArena.UnityConversion
{
    // The City Ironworks' in-game visuals (Claude, 2026-10-16; docs/EXPEDITION-CITY-IRONWORKS.md sections 3, 4, 5 and 9; Core
    // NativeIronworks{Kit,Hazards,Lights,Field,Fx,Shutter}, Runtime NativeIronworksPresentation, Resources NativeMolten and
    // NativeHeatHaze). LIVE on map `expedition-foundry` (Astra's activation, PR #255: the design blockout under the live prefix,
    //  the exp-foundry area of Resources/ExpeditionLayout.json, the Ironworks look in MapLooks.json). Checks:
    //  - only the foundry: the kit builds nothing for any other live map; the runtime and every hook are gated on the map id or
    //    the converter gate's id; the live manifest has no converter gate, so the shutter draws nothing live yet;
    //  - hazards off in normal play: with no authority bound and the DEV preview off, every hazard view is idle and off (runners
    //    crusted, no pour, no slab, no telegraphs, the frames at rest); only the DEV panel's preview runs them;
    //  - the look: the live Ironworks entry carries the design's block looks under the live prefix, and validates with the rest;
    //  - the kit builds from the live map and its records: every room dressed, deterministic, the molten pieces, the light
    //    sources, the Rigger's eight hang points as hoists on their girders (dressing until the Rigger exists), the doors on every
    //    door spawn and grates on every floor vent;
    //  - placement, recomputed here from the records: within 3.1 m over the ground or the ring nothing stands out more than
    //    0.25 m from a block except flat paint, thin chains and a hazard's own machinery inside its volume; nothing meets a spawn's
    //    exit, path or drop column (other than its own dressing), a slot, a cache, a boss point, a machine spot, the doors' or the
    //    shutter's volumes;
    //  - budgets (section 9.1): parts per room, kit and molten draws, the low tier's share, effect quads in view, the eight
    //    practical lights with two always cool, warm molten pools within 13 m;
    //  - hazard views (section 5): the cycles, Wave 1's light set, the waves' joins, the boss encounter's parked ladle, the
    //    converter stub; the definitions equal the live record's; the frames (ladle, ram, slab, shear, pusher, pot) and the molten
    //    states follow the views;
    //  - the converter shutter follows the closure states (sealed, opening, open, sealing) inside the state scene (on the design
    //    manifest, which has the gate);
    //  - readability: telegraphs cream, beacons the hazard kit's amber, the molten ramp's top within HDR 4;
    //  - no allocation per frame in the hazard views, frames, drives, molten states, picker, effects or the shutter.
    // Static data and pure-code checks only: no Unity scene, GPU, PhysX or network claim.
    internal static class IronworksVisualChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Ironworks visuals: "+what);count++;}
        /// <summary>The doc's per-room part budgets (section 9.1), the whole map's, and the effect quads in view.</summary>
        internal static readonly int[] RoomBudget={300,300,300,1800,1400,1300,900,700,600};
        internal const int EastWing=900,MaxParts=7600,MaxDraws=16,MaxQuadsInView=1100;
        private struct Box{public double X0,X1,Y0,Y1,Z0,Z1;}
        private static Box World(in NativeIronPart p)
        {
            double ox=NativeIronworksKit.FrameOrigin[p.Frame*3],oy=NativeIronworksKit.FrameOrigin[p.Frame*3+1],oz=NativeIronworksKit.FrameOrigin[p.Frame*3+2];var b=new Box();
            NativeIronworksKit.Extent(p.X+ox,p.Y+oy,p.Z+oz,p.Hx,p.Hy,p.Hz,p.Qx,p.Qy,p.Qz,p.Qw,out b.X0,out b.X1,out b.Y0,out b.Y1,out b.Z0,out b.Z1);return b;
        }
        private static Box World(in NativeMoltenPiece m)
        {
            double ox=NativeIronworksKit.FrameOrigin[m.Frame*3],oy=NativeIronworksKit.FrameOrigin[m.Frame*3+1],oz=NativeIronworksKit.FrameOrigin[m.Frame*3+2];var b=new Box();
            NativeIronworksKit.Extent(m.X+ox,m.Y+oy,m.Z+oz,m.Hx,m.Hy,m.Hz,m.Qx,m.Qy,m.Qz,m.Qw,out b.X0,out b.X1,out b.Y0,out b.Y1,out b.Z0,out b.Z1);return b;
        }
        private static bool Hit(in Box a,double x0,double x1,double y0,double y1,double z0,double z1)=>a.X1>x0+1e-4&&a.X0<x1-1e-4&&a.Y1>y0+1e-4&&a.Y0<y1-1e-4&&a.Z1>z0+1e-4&&a.Z0<z1-1e-4;
        private static double[] Aabb(ExpeditionVolume v)
        {
            double a=v.yaw*Math.PI/180,c=Math.Abs(Math.Cos(a)),s=Math.Abs(Math.Sin(a)),hx=c*v.sx/2+s*v.sz/2,hz=s*v.sx/2+c*v.sz/2;return new[]{v.x-hx,v.x+hx,v.y-v.sy/2,v.y+v.sy/2,v.z-hz,v.z+hz};
        }

        internal static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            // ---------------- only the foundry ----------------
            var live=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o).maps;var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);
            Check(live.Count(m=>NativeIronworksKit.Active(m.id))==1&&content.maps.All(m=>!NativeIronworksKit.Active(m.id)||m.id==NativeIronworksKit.MapId),"exactly one live map is the Ironworks ("+NativeIronworksKit.MapId+")");
            foreach(var m in live)if(!NativeIronworksKit.Active(m.id)){var k=NativeIronworksKit.Build(m.blocks,null);Check(k.Parts.Length==0&&k.Molten.Length==0&&k.Glows.Length==0,"the kit builds nothing for "+m.id);}
            string liveLayoutText=R("Assets/Conversion/Resources/ExpeditionLayout.json");var liveLayout=NativeExpeditionLayout.Parse(liveLayoutText);
            Check(liveLayout.areas.Count(a=>a.map==NativeIronworksKit.MapId)==1&&liveLayout.Area(NativeIronworksKit.AreaId)?.map==NativeIronworksKit.MapId,"the live manifest's "+NativeIronworksKit.AreaId+" area is the foundry");
            Check(liveLayout.areas.All(a=>(a.gates??Array.Empty<ExpeditionGateLayout>()).All(g=>!NativeIronworksShutter.Owns(g.id))),"the live manifest has no converter gate: every live gate keeps the state scene's gate and the shutter draws nothing live yet");
            string Src(string f)=>R("Assets/ChipArena/"+f);
            var pres=Src("Runtime/NativeIronworksPresentation.cs");
            Check(pres.Contains("var map=arena.Map;if(map==null||!NativeIronworksKit.Active(map.id)||!EnsureIronworks())return;",StringComparison.Ordinal),"the runtime draws only on the Ironworks map");
            Check(Src("Runtime/NativeSourcePresentation.cs").Contains("            IronworksScenery(arena);\n",StringComparison.Ordinal)&&Src("Runtime/ArenaPresentation.cs").Contains("            DisposeIronworks();\n",StringComparison.Ordinal),"hooked into SourceWorld and the presentation's teardown");
            Check(Src("Core/NativeExpeditionStateScene.cs").Contains("if(NativeIronworksShutter.Owns(c.id))NativeIronworksShutter.Build(Parts,PartCapacity,Quads,f,c,t,eye,reduced,low);else Gate(f,g,c,t,reduced,low);",StringComparison.Ordinal),"the state scene hands only the converter gate to the shutter");
            Check(NativeDevActions.Find("preview-ironworks")?.Kind==NativeDevKind.Presentation&&Src("Runtime/NativeDevPanel.cs").Contains("case \"preview-ironworks\":IronworksPreviewStep();break;",StringComparison.Ordinal),"the DEV preview is a presentation-only view tool");
            Check(Src("Editor/NativeBuildChecks.cs").Contains("\"NativeMolten\",\"NativeHeatHaze\"",StringComparison.Ordinal),"the editor build check compiles the two shaders");
            // ---------------- hazards off in normal play ----------------
            // The runtime's input: the DEV preview's, else the bound authority's, else nothing; nothing in the project binds it yet, and
            // only the DEV panel's step sets the preview.
            Check(pres.Contains("public static IronworksHazardInput Input()=>Preview?NativeIronworksHazards.Preview(PreviewStage,Time.unscaledTimeAsDouble-PreviewStart):Bound?Authority:default;",StringComparison.Ordinal)
                &&pres.Contains("NativeIronworksHazards.Evaluate(NativeIronworksLive.Input(),iwViews);",StringComparison.Ordinal),"the runtime's hazard input is the DEV preview's, else the bound authority's, else none");
            var sources=Directory.GetFiles(Path.Combine(root,"Assets"),"*.cs",SearchOption.AllDirectories).Select(File.ReadAllText).ToArray();
            Check(sources.All(t=>!t.Contains("NativeIronworksLive.Bound=",StringComparison.Ordinal)&&!t.Contains("NativeIronworksLive.Authority=",StringComparison.Ordinal))&&sources.Count(t=>t.Contains("NativeIronworksLive.PreviewStage=",StringComparison.Ordinal))==1,"no hazard authority is bound yet, and only the DEV preview step starts the preview");
            {
                var off=new IronworksHazardView[NativeIronworksHazards.Count];NativeIronworksHazards.Evaluate(default,off);
                Check(off.All(v=>v.Phase==NativeHazardPhase.Idle&&v.Rate==NativeIronworksHazards.RateOff&&!v.Parked),"with no input every hazard is idle and off");
                var offFx=new NativeIronworksFx();var offScene=NativeIronFxScene.Build(NativeIronworksKit.Build(live.Single(m=>NativeIronworksKit.Active(m.id)).blocks,liveLayout.Area(NativeIronworksKit.AreaId)));int telegraphs=0;
                for(double t=0;t<120;t+=1.7){offFx.Build(offScene,off,t,19,9.4,-13.4,0,0,-1,0,1,0,1,false);telegraphs+=offFx.Telegraphs;}
                Check(telegraphs==0,"hazards off: no telegraph decal ever shows in normal play");
                Check(!NativeIronworksKit.SlabShown(off[NativeIronworksHazards.Slab])&&NativeIronworksKit.RamLiftOf(off[NativeIronworksHazards.Hammer])>=0&&NativeIronworksKit.SlabRunOf(off[NativeIronworksHazards.Slab])<0,"hazards off: the hot slab stays in the furnace and the hammer rests");
            }
            // ---------------- the live look ----------------
            var looks=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(looks);
            var ironLook=looks.maps.Single(l=>l.id==NativeIronworksKit.MapId);var designLook=JsonSerializer.Deserialize<MapLookSet>(R("Tests/expedition-ironworks-look-draft.json"),o).maps.Single();
            Check(ironLook.look=="ironworks"&&ironLook.blockLooks.Length==designLook.blockLooks.Length&&ironLook.blockLooks.Zip(designLook.blockLooks,(a,b)=>a.block==b.block.Replace("expedition-ironworks-",NativeIronworksKit.Prefix)&&a.look==b.look&&a.color==b.color).All(x=>x),
                "the live Ironworks look carries the design's block looks under the live prefix (clean plate for the cranes, cars, converters and pusher)");
            // ---------------- the live map and its records ----------------
            var map=live.Single(m=>NativeIronworksKit.Active(m.id));var blocks=map.blocks;
            string layoutText=liveLayoutText;var area=liveLayout.Area(NativeIronworksKit.AreaId);Check(area!=null&&area.map==map.id&&blocks.All(b=>NativeIronworksKit.Stem(b.id)!=null),"the live foundry area and map (every block under the live prefix)");
            var rec=JsonNode.Parse(layoutText)["areas"].AsArray().Single(a=>(string)a["id"]==NativeIronworksKit.AreaId)["ironworks"];
            var sw=System.Diagnostics.Stopwatch.StartNew();var kit=NativeIronworksKit.Build(blocks,area);double buildMs=sw.Elapsed.TotalMilliseconds;var again=NativeIronworksKit.Build(blocks,area);
            Check(NativeIronworksKit.Active(map.id)&&kit.Parts.Length>4000&&kit.Molten.Length>60&&kit.Glows.Length>50,"the kit builds from the live map ("+kit.Parts.Length+" parts, "+kit.Molten.Length+" molten pieces, "+kit.Glows.Length+" light sources)");
            Check(kit.Parts.SequenceEqual(again.Parts)&&kit.Molten.SequenceEqual(again.Molten)&&kit.Glows.SequenceEqual(again.Glows),"deterministic: two builds give the same parts, molten pieces and lights");
            var parts=new int[NativeIronworksKit.Rooms];var molten=new int[NativeIronworksKit.Rooms];var lights=new int[NativeIronworksKit.Rooms];kit.Counts(parts,molten,lights);var lowParts=new int[NativeIronworksKit.Rooms];kit.Counts(lowParts,new int[NativeIronworksKit.Rooms],new int[NativeIronworksKit.Rooms],true);
            for(int r=0;r<NativeIronworksKit.Rooms;r++)Check(parts[r]>=120,"every room is dressed: "+NativeIronworksKit.RoomNames[r]+" has "+parts[r]+" parts");
            Check(molten[NativeIronworksKit.RoomCasting]>=20&&molten[NativeIronworksKit.RoomFurnace]>=6&&molten[NativeIronworksKit.RoomMill]>=6&&molten[NativeIronworksKit.RoomCoke]>=1&&molten[NativeIronworksKit.RoomConverter]>=3&&molten[NativeIronworksKit.RoomSlag]>=5,"molten metal in every hot room");
            Check(kit.Rejected<=40,"the kit fits its keep-outs (only "+kit.Rejected+" parts or groups rejected: "+string.Join("; ",kit.RejectedAt.Take(80).Select(r=>"("+((r.X0+r.X1)/2).ToString("F1")+","+r.Y0.ToString("F1")+".."+r.Y1.ToString("F1")+","+((r.Z0+r.Z1)/2).ToString("F1")+") k"+r.Kind+" s"+r.Spawn))+")");
            // ---------------- placement ----------------
            Check(kit.Unbacked<=kit.Parts.Length/40,"few parts left out by the placement rule ("+kit.Unbacked+": "+string.Join("; ",kit.UnbackedAt.Take(60).Select(r=>"("+((r.X0+r.X1)/2).ToString("F1")+","+r.Y0.ToString("F1")+".."+r.Y1.ToString("F1")+","+((r.Z0+r.Z1)/2).ToString("F1")+")"))+")");
            var violations=new List<string>();var floors=blocks.Where(b=>b.id.Contains("-deck-")&&!b.id.Contains("-rail-")&&!b.id.Contains("-col-")).ToArray();
            bool OverRing(Box w)=>floors.Any(d=>w.X1>d.pos.x-d.size.x/2&&w.X0<d.pos.x+d.size.x/2&&w.Z1>d.pos.z-d.size.z/2&&w.Z0<d.pos.z+d.size.z/2);
            bool InBand(Box w)=>blocks.Any(b=>{double bx0=b.pos.x-b.size.x/2-NativeIronworksKit.WallBand,bx1=b.pos.x+b.size.x/2+NativeIronworksKit.WallBand,by0=b.pos.y-b.size.y/2-NativeIronworksKit.WallBand,by1=b.pos.y+b.size.y/2+NativeIronworksKit.WallBand,bz0=b.pos.z-b.size.z/2-NativeIronworksKit.WallBand,bz1=b.pos.z+b.size.z/2+NativeIronworksKit.WallBand;
                return w.X0>=bx0-1e-4&&w.X1<=bx1+1e-4&&w.Y0>=by0-1e-4&&w.Y1<=by1+1e-4&&w.Z0>=bz0-1e-4&&w.Z1<=bz1+1e-4;});
            bool InVolume(Box w,int h){var d=NativeIronworksHazards.Defs[h];const double pad=.6;return w.X0>=d.X-d.Sx/2-pad&&w.X1<=d.X+d.Sx/2+pad&&w.Z0>=d.Z-d.Sz/2-pad&&w.Z1<=d.Z+d.Sz/2+pad&&w.Y0>=d.Y-d.Sy/2-pad;}
            // A hazard's machinery stands inside its hazard's volume (or, without an index, inside some hazard's volume).
            bool InHazard(Box w,short h)=>h>=0&&h<NativeIronworksHazards.Count?InVolume(w,h):Enumerable.Range(0,NativeIronworksHazards.Count).Any(k=>InVolume(w,k));
            bool Low(Box w)=>w.Y0<NativeIronworksKit.Low-.001&&!NativeIronworksKit.OnTop(w.X0,w.X1,w.Y0,w.Z0,w.Z1,blocks)||w.Y0>=NativeIronworksKit.Low-.001&&w.Y0<NativeIronworksKit.RingFloor+NativeIronworksKit.Low-.001&&w.Y1>NativeIronworksKit.RingFloor+.001&&OverRing(w);
            void Placement(string what,Box w,byte role,short hazard,bool flat,bool thin)
            {
                if(!Low(w)||flat||thin||role==NativeIronworksKit.RoleHazard&&InHazard(w,hazard)||InBand(w))return;
                violations.Add(what+" at ("+((w.X0+w.X1)/2).ToString("F2")+", "+w.Y0.ToString("F2")+".."+w.Y1.ToString("F2")+", "+((w.Z0+w.Z1)/2).ToString("F2")+") stands out from every block");
            }
            for(int i=0;i<kit.Parts.Length;i++){var p=kit.Parts[i];var w=World(p);Placement("part "+i+" (role "+p.Role+")",w,p.Role,p.Hazard,NativeIronworksKit.IsFlat(w.Y0,w.Y1),p.Role==NativeIronworksKit.RoleChain&&Math.Min(p.Hx,p.Hz)<=NativeIronworksKit.ChainHalf);}
            for(int i=0;i<kit.Molten.Length;i++){var m=kit.Molten[i];var w=World(m);Placement("molten "+i,w,m.Hazard>=0?NativeIronworksKit.RoleHazard:NativeIronworksKit.RoleMolten,m.Hazard,NativeIronworksKit.IsFlat(w.Y0,w.Y1),false);}
            Check(violations.Count==0,"within 3.1 m of the ground or the ring nothing stands out more than 0.25 m from a block except flat paint, thin chains and a hazard's own machinery ("+violations.Count+": "+string.Join("; ",violations.Take(6))+")");
            // The records, recomputed here: spawn exits, paths and drop columns, slots, caches, boss points, machine spots, doors, gates.
            var keep=new List<(double[] b,short spawn,string what)>();var spawns=area.spawns;
            for(short s=0;s<spawns.Length;s++)
            {
                // From the spawn's floor up: what lies under a deck's floor (its joists) blocks nothing.
                var e=spawns[s].entry;double clear=Math.Max(1.0,e.clear),fy=e.exit.y,fl=fy-.05;
                if(e.kind=="drop"||e.kind=="vent"&&e.surface=="ceiling"){keep.Add((new[]{e.exit.x-clear,e.exit.x+clear,fl,fy+Math.Max(3.2,e.height+.2),e.exit.z-clear,e.exit.z+clear},s,"drop column "+s));continue;}
                keep.Add((new[]{e.exit.x-clear,e.exit.x+clear,fl,fy+(e.kind=="breach"?4.4:3.2),e.exit.z-clear,e.exit.z+clear},s,"exit "+s));
                double x0=Math.Min(e.from.x,e.exit.x),x1=Math.Max(e.from.x,e.exit.x),z0=Math.Min(e.from.z,e.exit.z),z1=Math.Max(e.from.z,e.exit.z);bool alongX=x1-x0>z1-z0;double w2=e.kind=="breach"?2.0:1.1;
                keep.Add((new[]{alongX?x0:x0-w2,alongX?x1:x1+w2,fl,fy+(e.kind=="breach"?4.4:3.0),alongX?z0-w2:z0,alongX?z1+w2:z1},s,"path "+s));
            }
            foreach(var p in area.bossPoints)keep.Add((new double[]{p.x-1.6,p.x+1.6,p.y-.05,p.y+4,p.z-1.6,p.z+1.6},-1,"boss point"));
            foreach(var i in area.interactions)keep.Add((new double[]{i.x-1.1,i.x+1.1,i.y-.05,i.y+2.4,i.z-1.1,i.z+1.1},-1,"cache "+i.anchor));
            foreach(var d in area.doors){var a=Aabb(d.closure);keep.Add((new[]{a[0]-.5,a[1]+.5,a[2]-.2,a[3]+.5,a[4]-.5,a[5]+.5},-1,"exit door"));foreach(var p in d.entries)keep.Add((new double[]{p.x-.9,p.x+.9,p.y-.05,p.y+2.4,p.z-.9,p.z+.9},-1,"entry slot"));}
            foreach(var g in area.gates){var a=Aabb(g.closure);keep.Add((new[]{a[0]-.05,a[1]+.05,a[2]-.2,a[3]+.05,a[4]-.05,a[5]+.05},-1,"shutter"));}
            foreach(var mc in area.machines){double x0=Math.Min(mc.x,Math.Min(mc.front.x,mc.chipDrop.x)),x1=Math.Max(mc.x,Math.Max(mc.front.x,mc.chipDrop.x)),z0=Math.Min(mc.z,Math.Min(mc.front.z,mc.chipDrop.z)),z1=Math.Max(mc.z,Math.Max(mc.front.z,mc.chipDrop.z));keep.Add((new[]{x0-1.1,x1+1.1,mc.y-.05,mc.y+2.6,z0-1.1,z1+1.1},-1,"machine "+mc.kind));}
            Check(area.machines.Length==NativeIronworksKit.MachineSpots.Length/3&&area.machines.All(mc=>Enumerable.Range(0,area.machines.Length).Any(j=>Math.Abs(NativeIronworksKit.MachineSpots[j*3]-mc.x)<.01&&Math.Abs(NativeIronworksKit.MachineSpots[j*3+2]-mc.z)<.01)),"the live machines stand on the kit's machine spots");
            var hits=new List<string>();
            void Keepout(string what,Box w,short owner)
            {
                if(NativeIronworksKit.IsFlat(w.Y0,w.Y1))return;
                foreach(var k in keep){if(k.spawn>=0&&k.spawn==owner)continue;if(Hit(w,k.b[0],k.b[1],k.b[2],k.b[3],k.b[4],k.b[5])){hits.Add(what+" meets "+k.what);break;}}
            }
            for(int i=0;i<kit.Parts.Length;i++)Keepout("part "+i,World(kit.Parts[i]),kit.Parts[i].Owner);for(int i=0;i<kit.Molten.Length;i++)Keepout("molten "+i,World(kit.Molten[i]),kit.Molten[i].Owner);
            Check(hits.Count==0,"nothing meets a spawn's exit, path or drop column (other than its own dressing), a slot, a cache, a boss point, a machine spot, the doors or the shutter ("+hits.Count+": "+string.Join("; ",hits.Take(6))+")");
            // Every door spawn has its doorway and every floor vent its grate (the spawn's own dressing).
            for(short s=0;s<spawns.Length;s++){var e=spawns[s].entry;if(e.kind!="door"&&!(e.kind=="vent"&&e.surface!="ceiling"))continue;Check(kit.Parts.Any(p=>p.Owner==s),"spawn "+s+" ("+e.kind+") has its own dressing");}
            // The Rigger's eight hang points (the design record's; the live manifest has no Rigger yet, and three of the points are live
            // ceiling vents): each a hoist on its girder with a chain, as dressing.
            var designRec=JsonNode.Parse(R("Tests/expedition-ironworks-layout-draft.json"))["areas"].AsArray().Single(a=>(string)a["id"]==NativeIronworksKit.AreaId);
            var hangs=designRec["ironworks"]["spawns"].AsArray().Where(x=>x["hang"]!=null).ToArray();Check(hangs.Length==NativeIronworksKit.HangPoints.Length/4,"the kit's hang points are the design record's eight");
            foreach(var h in hangs){var e=designRec["spawns"].AsArray()[(int)h["spawn"]]["exit"];double ex=(double)e["x"],ez=(double)e["z"];int k=Enumerable.Range(0,hangs.Length).First(j=>Math.Abs(NativeIronworksKit.HangPoints[j*4]-ex)<.01&&Math.Abs(NativeIronworksKit.HangPoints[j*4+2]-ez)<.01);
                Check(Math.Abs(NativeIronworksKit.HangPoints[k*4+1]-(float)h["hang"]["hook"])<1e-3&&Math.Abs(NativeIronworksKit.HangPoints[k*4+3]-(float)h["hang"]["feet"])<1e-3,"hang point "+(int)h["spawn"]+" matches the design record's hook and drop heights");
                double hx=NativeIronworksKit.HangPoints[k*4],hz2=NativeIronworksKit.HangPoints[k*4+2];
                Check(kit.Parts.Count(p=>p.Role==NativeIronworksKit.RoleChain&&Math.Abs(p.X+NativeIronworksKit.FrameOrigin[p.Frame*3]-hx)<1.2&&Math.Abs(p.Z+NativeIronworksKit.FrameOrigin[p.Frame*3+2]-hz2)<1.2)>=8,"hang point at ("+hx+", "+hz2+") shows its hoist and chain");}
            Check(NativeIronworksKit.TrussX.Any(x=>Math.Abs(x+4)<.01)&&NativeIronworksKit.TrussX.Any(x=>Math.Abs(x-22)<.01)&&NativeIronworksKit.TrussX.Any(x=>Math.Abs(x-3.5)<1.5)&&NativeIronworksKit.TrussBottom==14.4f,"the roof trusses' bottom chords pass over the gantry hang points at 14.4 m (within 1.5 m)");
            // ---------------- hazards: definitions, cycles, schedule ----------------
            var defs=NativeIronworksHazards.Defs;var hz=rec["hazards"].AsArray();Check(defs.Length==hz.Count&&defs.Length==NativeIronworksHazards.Count,"the eleven hazards of section 5");
            for(int i=0;i<defs.Length;i++)
            {
                var h=hz[i];var v=h["volume"];var d=defs[i];
                Check(d.Id==(string)h["id"]&&Math.Abs(d.X-(float)v["x"])<1e-3&&Math.Abs(d.Y-(float)v["y"])<1e-3&&Math.Abs(d.Z-(float)v["z"])<1e-3&&Math.Abs(d.Sx-(float)v["sx"])<1e-3&&Math.Abs(d.Sy-(float)v["sy"])<1e-3&&Math.Abs(d.Sz-(float)v["sz"])<1e-3
                    &&Math.Abs(d.Warn-(float)h["warn"])<1e-3&&Math.Abs(d.Live-(float)h["live"])<1e-3&&Math.Abs(d.Period-(float)h["period"])<1e-3,d.Id+": volume and cycle equal the live record's");
                // The schedule is the design record's (the live activation snapshot carries the volumes and cycles only).
                var sched=h["schedule"]??designRec["ironworks"]["hazards"].AsArray()[i]["schedule"];string w1=(string)sched["wave1"],boss=(string)sched["boss"];
                Check((w1=="on")==(d.Wave1==NativeIronworksHazards.Wave1On)&&(w1=="half")==(d.Wave1==NativeIronworksHazards.Wave1Half)&&(boss=="off")==(d.Boss==NativeIronworksHazards.BossOff)&&(boss=="boss")==(d.Boss==NativeIronworksHazards.BossDriven),d.Id+": Wave 1 and boss schedule equal the design record's");
            }
            IronworksHazardInput In(int wave,bool boss,double clock)=>new IronworksHazardInput{Running=true,Wave=wave,Boss=boss,Clock=clock};
            byte Rate(int h,int wave,bool boss)=>NativeIronworksHazards.RateOf(h,In(wave,boss,0));
            Check(Rate(NativeIronworksHazards.R2,1,false)==NativeIronworksHazards.RateFull&&Rate(NativeIronworksHazards.R1,1,false)==0&&Rate(NativeIronworksHazards.R3,1,false)==0&&Rate(NativeIronworksHazards.Ladle,1,false)==NativeIronworksHazards.RateHalf
                &&Rate(NativeIronworksHazards.Steam,1,false)==NativeIronworksHazards.RateHalf&&Rate(NativeIronworksHazards.Hammer,1,false)==NativeIronworksHazards.RateFull&&Rate(NativeIronworksHazards.Slab,1,false)==0&&Rate(NativeIronworksHazards.Shear,1,false)==0
                &&Rate(NativeIronworksHazards.Slag,1,false)==0&&Rate(NativeIronworksHazards.Pusher,1,false)==0,"Wave 1 runs light: runner r2 only, the pour and the steam at half rate, the hammer, nothing else");
            Check(Rate(NativeIronworksHazards.R1,2,false)!=0&&Rate(NativeIronworksHazards.Slab,2,false)!=0&&Rate(NativeIronworksHazards.Shear,2,false)!=0&&Rate(NativeIronworksHazards.Slag,2,false)==0&&Rate(NativeIronworksHazards.Slag,3,false)!=0&&Rate(NativeIronworksHazards.Pusher,3,false)!=0&&Rate(NativeIronworksHazards.Ladle,4,false)==NativeIronworksHazards.RateFull,"the runners, slab and shear join in Wave 2, the slag spill and the pusher in Wave 3, the pour runs full from Wave 2");
            var bossLadle=NativeIronworksHazards.At(NativeIronworksHazards.Ladle,In(4,true,13));Check(bossLadle.Parked&&bossLadle.Phase==NativeHazardPhase.Idle&&Enumerable.Range(0,NativeIronworksHazards.Count).All(h=>h==NativeIronworksHazards.Ladle||h==NativeIronworksHazards.Converter||Rate(h,4,true)==NativeIronworksHazards.RateFull),"the boss encounter: the pour stops (the crane parks), everything else runs");
            Check(Enumerable.Range(0,60).All(t=>NativeIronworksHazards.At(NativeIronworksHazards.Converter,In(3,true,t)).Phase==NativeHazardPhase.Idle)&&Enumerable.Range(0,NativeIronworksHazards.Count).All(h=>NativeIronworksHazards.At(h,default).Phase==NativeHazardPhase.Idle),"the converter tilt is the boss's (a stub); nothing runs without an input");
            for(int h=0;h<NativeIronworksHazards.Count-1;h++)
            {
                var d=defs[h];int wave=Math.Max(2,(int)d.From);double off=d.Offset;
                var w=NativeIronworksHazards.At(h,In(wave,false,off+2*d.Period+d.Warn*.5));var a=NativeIronworksHazards.At(h,In(wave,false,off+2*d.Period+d.Warn+d.Live*.5));var c=NativeIronworksHazards.At(h,In(wave,false,off+2*d.Period+d.Warn+d.Live+Math.Max(.01,d.Cool*.5)));var i=NativeIronworksHazards.At(h,In(wave,false,off+3*d.Period-.05));
                Check(w.Phase==NativeHazardPhase.Warn&&w.Cycle==2&&Math.Abs(w.Level-.5f)<1e-3&&a.Phase==NativeHazardPhase.Active&&a.Level==1&&(d.Cool<=0||c.Phase==NativeHazardPhase.Clear)&&i.Phase==NativeHazardPhase.Idle&&Math.Abs(i.Until-.05)<1e-6,d.Id+": warn, live, cool-down and rest follow its cycle");
            }
            var half=NativeIronworksHazards.At(NativeIronworksHazards.Ladle,In(1,false,52+3.4));var half2=NativeIronworksHazards.At(NativeIronworksHazards.Ladle,In(1,false,26+1));
            Check(half.Phase==NativeHazardPhase.Warn&&half.Cycle==1&&half2.Phase==NativeHazardPhase.Idle&&NativeIronworksHazards.PeriodOf(NativeIronworksHazards.Ladle,NativeIronworksHazards.RateHalf)==52,"Wave 1's pour: one every 52 s with a 3.5 s warning");
            Check(NativeIronworksHazards.At(NativeIronworksHazards.R2,In(1,false,11+.5)).Phase==NativeHazardPhase.Warn&&NativeIronworksHazards.At(NativeIronworksHazards.R3,In(2,false,22+.5)).Phase==NativeHazardPhase.Warn&&NativeIronworksHazards.At(NativeIronworksHazards.R1,In(2,false,.5)).Phase==NativeHazardPhase.Warn,"the runners tap at offsets 0, 11 and 22 s");
            // The frames and the molten states follow the views.
            var views=new IronworksHazardView[NativeIronworksHazards.Count];
            IronworksHazardView F(int h,NativeHazardPhase ph,double since,double level=1)=>new IronworksHazardView{Phase=ph,Since=since,Level=(float)level,Rate=NativeIronworksHazards.RateFull,Cycle=1};
            NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,views,0,out _,out float ly0,out _,out _,out _,out float lqz0,out _);
            views[NativeIronworksHazards.Ladle]=F(NativeIronworksHazards.Ladle,NativeHazardPhase.Active,1);NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,views,0,out _,out _,out _,out _,out _,out float lqz1,out float lqw1);
            double tilt=2*Math.Asin(Math.Min(1,Math.Abs(lqz1)))*180/Math.PI;Check(Math.Abs(lqz0)<.02&&tilt>NativeIronworksKit.LadleTilt-3&&tilt<NativeIronworksKit.LadleTilt+3,"the ladle hangs upright at rest and tips to pour ("+tilt.ToString("F0")+" degrees)");
            views[NativeIronworksHazards.Ladle]=new IronworksHazardView{Parked=true};NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,views,0,out _,out float ly2,out _,out _,out _,out _,out _);NativeIronworksKit.FramePose(NativeIronworksKit.FrameHook,views,0,out _,out float hy2,out _,out _,out _,out _,out _);
            Check(Math.Abs(ly2-ly0-NativeIronworksKit.LadleHoist)<1e-3&&Math.Abs(hy2-NativeIronworksKit.FrameOrigin[NativeIronworksKit.FrameHook*3+1]-NativeIronworksKit.LadleHoist)<1e-3,"the boss encounter's parked crane: ladle and hook hoisted together");
            Check(NativeIronworksKit.RamLiftOf(F(0,NativeHazardPhase.Active,.2))==0&&NativeIronworksKit.RamLiftOf(F(0,NativeHazardPhase.Warn,1.2,1))>NativeIronworksKit.RamLift-.01&&NativeIronworksKit.RamLiftOf(F(0,NativeHazardPhase.Warn,0,0))<NativeIronworksKit.RamLift,"the drop hammer's ram lifts in the warning and strikes the billet in the live window");
            Check(!NativeIronworksKit.SlabShown(F(0,NativeHazardPhase.Idle,1))&&NativeIronworksKit.SlabShown(F(0,NativeHazardPhase.Warn,.5,.5))&&Math.Abs(NativeIronworksKit.SlabRunOf(F(0,NativeHazardPhase.Active,9))-NativeIronworksKit.SlabRun)<1e-3,"the hot slab shows in the reheat door in the warning and runs the table's length while live");
            var runner=kit.Molten.First(m=>m.Drive==NativeIronworksKit.MDriveRunner&&m.Hazard==NativeIronworksHazards.R2&&m.Hz>4);Array.Clear(views,0,views.Length);
            NativeIronworksKit.MoltenState(runner,views,0,out float ra,out _,out float rc,out float rf,out _,out _);views[NativeIronworksHazards.R2]=F(0,NativeHazardPhase.Active,3);NativeIronworksKit.MoltenState(runner,views,0,out float la,out _,out float lc,out float lf,out _,out _);
            Check(ra<.3f&&rc>.8f&&rf<.3f&&la>.9f&&lc<.4f&&lf>.99f,"a runner rests plugged and crusted and runs full and hot while live");
            var stream=kit.Molten.Where(m=>m.Drive==NativeIronworksKit.MDriveStream).ToArray();Check(stream.Length==NativeIronworksKit.StreamSegments,"the pour's stream");
            bool Shown(NativeMoltenPiece m){NativeIronworksKit.MoltenState(m,views,0,out _,out _,out _,out _,out _,out bool s);return s;}
            Array.Clear(views,0,views.Length);bool restOff=stream.All(m=>!Shown(m));views[NativeIronworksHazards.Ladle]=F(0,NativeHazardPhase.Active,1);bool pourOn=stream.All(m=>Shown(m));views[NativeIronworksHazards.Ladle]=new IronworksHazardView{Parked=true};bool parkedOff=stream.All(m=>!Shown(m));
            Check(restOff&&pourOn&&parkedOff,"the stream falls only while the ladle pours (never when the crane parks)");
            // ---------------- the converter shutter ----------------
            var c0=new ExpeditionClosureView{id=NativeIronworksShutter.GateId,area=NativeIronworksKit.AreaId,state="sealed",began=0};
            var pSealed=NativeIronworksShutter.PoseOf(c0,10);c0.state="opening";c0.began=10;var pHalf=NativeIronworksShutter.PoseOf(c0,10.5);var pDone=NativeIronworksShutter.PoseOf(c0,10.99);c0.state="open";var pOpen=NativeIronworksShutter.PoseOf(c0,20);c0.state="sealing";c0.began=20;var pSeal=NativeIronworksShutter.PoseOf(c0,20.5);
            Check(pSealed.Rise==0&&pSealed.Lock==1&&pSealed.Lamp==1&&pHalf.Rise>0&&pHalf.Rise<1&&pHalf.Lock<.01&&pDone.Rise>pHalf.Rise&&pOpen.Rise==1&&pOpen.Lamp==0&&pSeal.Rise<1&&pSeal.Rise>0,"the shutter: sealed down and bolted, opening rolls it up unbolted, open it is gone, sealing runs it down");
            // On the design manifest (the live one has no converter gate yet), the foundry area under the live map id.
            var draftLayout=NativeExpeditionLayout.Parse(R("Tests/expedition-ironworks-layout-draft.json"));var rig=ExpeditionStateRig.For(draftLayout,NativeIronworksKit.AreaId);var scene=new NativeExpeditionStateScene();var eye=new ExpeditionEye{X=-12,Y=2,Z=0,Rx=0,Ry=0,Rz=-1,Ux=0,Uy=1,Uz=0};
            int Built(string state,double prog){var c=new ExpeditionClosureView{id=NativeIronworksShutter.GateId,area=NativeIronworksKit.AreaId,state=state,began=100-prog};scene.Build(new ExpeditionView{area=NativeIronworksKit.AreaId,closures=new[]{c}},rig,null,100,eye,false,false);return scene.Parts.Count;}
            int sealedParts=Built("sealed",0);int sealedQuads=scene.Quads.Count;int openParts=Built("open",5);int openingParts=Built("opening",.5);int openingQuads=scene.Quads.Count;
            Check(scene.GatesDrawn==1&&sealedParts>30&&openParts<8&&openingParts>openParts&&openingParts<sealedParts&&sealedQuads==0&&openingQuads>0,"the state scene draws the converter shutter from the director's closure view ("+sealedParts+" parts sealed, "+openingParts+" opening, "+openParts+" open; dust and sparks only while it moves; no hardlight lattice)");
            // ---------------- lights, budgets, readability ----------------
            int total=kit.Parts.Length,lowTotal=lowParts.Sum();int draws=kit.Draws(false),meltDraws=kit.MoltenDraws(false);
            for(int r=0;r<NativeIronworksKit.Rooms;r++)if(r>=NativeIronworksKit.RoomCasting)Check(parts[r]<=RoomBudget[r],NativeIronworksKit.RoomNames[r]+": "+parts[r]+" parts within its budget of "+RoomBudget[r]);
            Check(parts[0]+parts[1]+parts[2]<=EastWing&&total<=MaxParts&&draws+meltDraws<=MaxDraws,"the east wing within "+EastWing+", the map within "+MaxParts+" parts ("+total+"), "+(draws+meltDraws)+" instanced draws for the kit and the molten metal");
            double share=1-lowTotal/(double)total;Check(share>.2&&share<.5,"the low tier leaves out the detail parts ("+(share*100).ToString("F0")+"%)");
            var picker=NativeIronworksLights.Build(kit);Check(picker.Candidates.Count(c=>c.Cool)>=8&&picker.Candidates.All(c=>c.Cool||c.Kind==NativeIronworksKit.GlowSodium||c.Kind==NativeIronworksKit.GlowOffice||c.Kind==NativeIronworksKit.GlowLamp||c.Kind==NativeIronworksKit.GlowBeacon||c.Kind==NativeIronworksKit.GlowWindow||c.Range<=13),"heat stays local: every molten and furnace light reaches 13 m at most");
            var fx=new NativeIronworksFx();var fxs=NativeIronFxScene.Build(kit);var input=In(3,false,0);var cams=new[]{new[]{19,9.4,-13.4},new[]{40.5,1.75,-.6},new[]{5.2,7.7,22.5},new[]{6,1.6,13.2},new[]{33.2,3.6,-28.4},new[]{-25.8,1.7,23.4},new[]{-25.2,1.7,-2.4},new[]{-25.6,3,-15.8},new[]{44,1.7,20},new[]{44,1.7,-22}};int maxQuads=0,minCool=8;
            for(double t=0;t<80;t+=.73)
            {
                input.Clock=t;NativeIronworksHazards.Evaluate(input,views);
                foreach(var c in cams){picker.Settle(c[0],c[1],c[2],views,t);minCool=Math.Min(minCool,picker.CoolSlots());fx.Build(fxs,views,t,c[0],c[1],c[2],0,0,-1,0,1,0,1,false);maxQuads=Math.Max(maxQuads,fx.Quads.Count);}
            }
            Check(minCool==NativeIronworksLights.Cool,"two of the eight practical lights are always cool lamps, at every camera through every hazard state");
            Check(maxQuads<=MaxQuadsInView&&maxQuads<NativeIronworksFx.Capacity,"effect quads in view within "+MaxQuadsInView+" (most "+maxQuads+")");
            var decal=(NativeIronworksFx.DecalG/NativeIronworksFx.DecalR);int amber=NativeIronworksKit.AmberHex;double amberGR=((amber>>8)&255)/(double)((amber>>16)&255);
            Check(decal>.8&&amberGR>.6&&amber==0xFFB000,"warnings are never plain orange: the telegraphs cream white, the beacons the hazard kit's amber");
            var shader=Src("Resources/NativeMolten.shader");var top=shader.Substring(shader.IndexOf("c=lerp(c,float3(3.20,2.25,1.05)",StringComparison.Ordinal),40);
            Check(shader.Contains("// SHARED BEGIN",StringComparison.Ordinal)&&shader.Contains("#include \"NativeWardLight.cginc\"",StringComparison.Ordinal)&&top.Contains("3.20",StringComparison.Ordinal)&&new[]{"_MoltenSize","_MoltenFlow","_MoltenHeat","_MoltenStyle","_MoltenClock"}.All(n=>shader.Contains(n,StringComparison.Ordinal)&&pres.Contains("\""+n+"\"",StringComparison.Ordinal)),"the molten shader: its per-instance properties are the runtime's, the hottest metal tops out at HDR 3.2 (within 4), the light field and the slots light its crust");
            var haze=Src("Resources/NativeHeatHaze.shader");Check(haze.Contains("GrabPass { \"_IronworksHeatGrab\" }",StringComparison.Ordinal)&&pres.Contains("if(camera==null||iwHaze==null||LowGraphics||!UsesInstancing)return;",StringComparison.Ordinal),"the heat haze: one named grab, none on the low tier");
            // ---------------- the light field ----------------
            var field=NativeIronworksField.Build(kit,blocks,map.bounds.minX,map.bounds.maxX,map.bounds.minZ,map.bounds.maxZ);var field2=NativeIronworksField.Build(kit,blocks,map.bounds.minX,map.bounds.maxX,map.bounds.minZ,map.bounds.maxZ);
            float At(float x,float z,int slice,int ch){int i=(int)((x-field.MinX)/NativeWardZeroField.Cell),k=(int)((z-field.MinZ)/NativeWardZeroField.Cell);return field.Data[((slice*field.Nz+k)*field.Nx+i)*NativeIronworksField.Channels+ch];}
            Check(field.Data.SequenceEqual(field2.Data)&&field.Data.All(x=>x>=0&&!float.IsNaN(x))&&field.Sources>=40,"the light field bakes the same every time ("+field.Sources+" glows, "+field.BakeMs.ToString("F0")+" ms)");
            Check(At(0,20,0,2)>.05f&&At(0,4,0,2)<At(0,20,0,2)*.25f&&At(0,-18,0,2)==0&&At(-42,26.6f,1,0)>.03f,"heat stays local in the field: a runner lights its trough, little of the floor 4 m away and nothing behind the mill wall; the coke doors light their lane ("+At(0,20,0,2).ToString("F3")+", "+At(0,4,0,2).ToString("F3")+", "+At(0,-18,0,2).ToString("F3")+", "+At(-42,26.6f,1,0).ToString("F3")+")");
            // ---------------- no allocation per frame ----------------
            var stateViews=new IronworksHazardView[NativeIronworksHazards.Count];var levels=new float[4];var closure=new ExpeditionClosureView{id=NativeIronworksShutter.GateId,area=NativeIronworksKit.AreaId,state="opening",began=99.5};var sview=new ExpeditionView{area=NativeIronworksKit.AreaId,closures=new[]{closure}};
            void Frame(double t)
            {
                input.Clock=t;NativeIronworksHazards.Evaluate(input,stateViews);for(int f=0;f<NativeIronworksKit.Frames;f++)NativeIronworksKit.FramePose(f,stateViews,t,out _,out _,out _,out _,out _,out _,out _);
                foreach(var p in kit.Parts)if(p.Drive!=NativeIronworksKit.DriveSteady)NativeIronworksKit.Drive(p,stateViews,t,out _,out _,out _,out _);
                foreach(var m in kit.Molten)NativeIronworksKit.MoltenState(m,stateViews,t,out _,out _,out _,out _,out _,out _);
                picker.Update(19,9.4,-13.4,stateViews,t,.016f);fx.Build(fxs,stateViews,t,19,9.4,-13.4,1,0,0,0,1,0,1,false);NativeIronworksField.Levels(stateViews,t,levels);scene.Build(sview,rig,null,100+(t%1),eye,false,false);
            }
            for(int f=0;f<5;f++)Frame(f*.016);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<240;f++)Frame(3+f*.11);long spent=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(spent==0,"the hazard views, frames, drives, molten states, picker, effects, field levels and the shutter allocate nothing per frame ("+spent+" bytes over 240 frames)");
            Console.WriteLine("PASS ironworks visuals ("+count+" checks: only on "+NativeIronworksKit.MapId+"; hazards off in normal play (DEV preview only); the live look; the kit from the live map: "+total+" parts ("+lowTotal+" on the low tier), "+kit.Molten.Length+" molten pieces, "+kit.Glows.Length+" light sources, "+(draws+meltDraws)+" instanced draws, built in "+buildMs.ToString("F0")+" ms; rooms "
                +string.Join(", ",Enumerable.Range(0,NativeIronworksKit.Rooms).Select(r=>NativeIronworksKit.RoomNames[r]+" "+parts[r]))+"; placement and keep-outs from the records; hazard cycles, Wave 1, the waves' joins and the boss's parked ladle; the shutter from the closure states; two cool lights always; "+maxQuads+" effect quads at most; the field in "+field.BakeMs.ToString("F0")+" ms; no allocation per frame)");
        }
    }
}
