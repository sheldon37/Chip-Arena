using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ChipArena.UnityConversion
{
    // Central Station's visuals (docs/EXPEDITION-CENTRAL-STATION-VISUALS.md; Core NativeExpeditionStation{,Kit,Lights,Fx},
    // Runtime NativeExpeditionStationPresentation, NativeExpeditionStationAudio). LIVE since the activation (PR #255): the map
    // (Resources/UnityMaps.json), its area exp-transit (Resources/ExpeditionLayout.json) and its look (MapLooks.json). Checks:
    //  - other maps untouched: the gate is the station's map id alone; the kit builds nothing for any other map; the runtime,
    //    audio and DEV hooks are gated on the station (so every other map draws and sounds as before);
    //  - normal play shows no hazard until the authority's station fields exist: the calm view draws no express, live rail,
    //    flood, cabinet, steam burst or glass (no telegraph quad, no beacon or express light, no cue); only the DEV preview does;
    //  - the live look validates, gives every station block a listed look and names the station's kit;
    //  - the kit from the live map and area (its records and machines): deterministic and
    //    independent of the blocks' order; every part on a station block with a size, a unit turn and a look the shader draws;
    //    the set pieces are there (the vault, the board with every live variant on both faces, the ticker's pages, CENTRAL,
    //    the signals and beacons, the live rail per section, the levers in both poses, the cabinets, the flood, the train's
    //    doors, the express);
    //  - placement, recomputed from the records: below 3.1 m over each level nothing stands out more than 0.25 m from a block
    //    but flat paint; free-standing parts on a walkable top below 2.2 m are at most 0.65 m tall unless against a wall or
    //    furniture; nothing meets a spawn's exit or path (track parts, the flood's surface and flat paint excepted; a spawn's
    //    own dressing excepted), a vent, a drop column, an entry, an interaction or boss point, the exit door's volumes, a
    //    machine or the spots its pilot stands on and its drop lands;
    //  - budgets: parts per room, the low tier, casters, instanced draws, light candidates and slots at cameras in every
    //    room (8, 4 on the low tier), effect quads with every hazard on;
    //  - the state: the express's warning, pass and wake; the signals, the danger lamps; the levers cutting the rails and the
    //    flood and halting the express; the board's rows (APPROACHING, SUSPENDED, LAST CALL, ALL SERVICES CANCELLED) and their
    //    blink, one variant lit per row and face; the ticker one page at a time; the express drawn only in the station; the
    //    cue timeline; the DEV preview's cycle;
    //  - no allocation per frame: the state, the drive over every part, the light picker, the atmosphere and the cues.
    // Static data and pure-code checks only: no Unity scene, GPU, PhysX, director or network claim.
    internal static class ExpeditionStationVisualChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition station visuals: "+what);count++;}
        private struct World{public double X0,X1,Y0,Y1,Z0,Z1;public double[] Cx,Cz;}
        private static World Pose(ContentBlock b,in NativeStationPart p)
        {
            NativeExpeditionStationKit.World(b,p,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw);
            var w=new World{Cx=new double[8],Cz=new double[8]};NativeExpeditionStationKit.Extent(x,y,z,p.Hx,p.Hy,p.Hz,qx,qy,qz,qw,out w.X0,out w.X1,out w.Y0,out w.Y1,out w.Z0,out w.Z1);
            for(int k=0;k<8;k++){NativeExpeditionStationKit.Rotate(qx,qy,qz,qw,(k&1)==0?-p.Hx:p.Hx,(k&2)==0?-p.Hy:p.Hy,(k&4)==0?-p.Hz:p.Hz,out double cx,out _,out double cz);w.Cx[k]=x+cx;w.Cz[k]=z+cz;}
            return w;
        }
        private static double Outside(ContentBlock b,double x,double z)
        {
            double yaw=2*Math.Atan2(b.rotation?.y??0,b.rotation?.w??1),c=Math.Cos(yaw),s=Math.Sin(yaw),dx=x-b.pos.x,dz=z-b.pos.z,lx=dx*c-dz*s,lz=dx*s+dz*c;
            double ox=Math.Max(0,Math.Abs(lx)-b.size.x/2),oz=Math.Max(0,Math.Abs(lz)-b.size.z/2);return Math.Sqrt(ox*ox+oz*oz);
        }
        private static bool BoxHit(World w,double x0,double x1,double y0,double y1,double z0,double z1)=>!(w.X1<=x0||w.X0>=x1||w.Y1<=y0||w.Y0>=y1||w.Z1<=z0||w.Z0>=z1);
        private static bool Disc(World w,double x,double z,double r,double y0,double y1)=>BoxHit(w,x-r,x+r,y0,y1,z-r,z+r);
        // The station's walkable tops (floors, platforms, the bed, decks, landings) as the placement rules read them.
        private static bool Walkable(string stem)=>stem.StartsWith("floor-",StringComparison.Ordinal)||stem.StartsWith("plat-",StringComparison.Ordinal)||stem=="track-bed"||stem.StartsWith("deck-",StringComparison.Ordinal)||
            stem.StartsWith("landing-",StringComparison.Ordinal)||stem=="bridge-w"||stem=="bridge-e"||stem=="signal-floor"||stem=="signal-landing"||stem=="hall-ceiling"||stem.StartsWith("train-car2-floor",StringComparison.Ordinal);

        internal static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            string unityText=R("Assets/Conversion/Resources/UnityMaps.json"),looksText=R("Assets/Conversion/Resources/MapLooks.json"),layoutText=R("Assets/Conversion/Resources/ExpeditionLayout.json");
            var unity=JsonSerializer.Deserialize<UnityMapContent>(unityText,o);var set=JsonSerializer.Deserialize<MapLookSet>(looksText,o);var live=NativeExpeditionLayout.Parse(layoutText);
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);
            var station=unity.maps.SingleOrDefault(m=>m.id==NativeExpeditionStation.MapId);Check(station!=null&&live.instances.Contains("exp-transit"),"the station is live: its map and its area in the live manifest");var blocks=station.blocks;

            // ---------------- other maps untouched ----------------
            var ids=unity.maps.Select(m=>m.id).Concat(content.maps.Select(m=>m.id)).Where(id=>id!=NativeExpeditionStation.MapId).Distinct().ToArray();
            Check(NativeExpeditionStation.Active(NativeExpeditionStation.MapId)&&ids.All(id=>!NativeExpeditionStation.Active(id)),"the gate is the station's map id alone ("+ids.Length+" other maps are not it)");
            foreach(var m in unity.maps.Where(m=>m.id!=NativeExpeditionStation.MapId)){var k=NativeExpeditionStationKit.Build(m.blocks,null);Check(k.Parts.Length==0&&k.Express.Length==0&&k.Glows.Length==0&&k.Shafts.Length==0&&k.Waters.Length==0,m.id+": the station kit builds nothing for it");}
            foreach(var m in content.maps)Check(NativeExpeditionStationKit.Build(m.blocks,null).Parts.Length==0,m.id+" (GameContent): the station kit builds nothing for it");
            string Src(string f)=>R("Assets/ChipArena/Runtime/"+f);
            var pres=Src("NativeExpeditionStationPresentation.cs");
            Check(Src("NativeSourcePresentation.cs").Contains("            StationScenery(arena);\n")&&pres.Contains("if(!NativeExpeditionStation.Active(arena.MapId)||map==null||!EnsureStation())return;"),"the runtime is hooked into SourceWorld and gated on the station's map id");
            Check(Src("NativeAudio.cs").Contains("            StationCues(arena,menu);\n")&&Src("NativeExpeditionStationAudio.cs").Contains("!NativeExpeditionStation.Active(arena.MapId)"),"the sounds are hooked and gated the same way");
            Check(Src("NativeDevPanel.cs").Contains("case \"preview-station\":NativeStationPreview.On=")&&NativeDevActions.Find("preview-station")?.Kind==NativeDevKind.Presentation,"the DEV preview is a presentation-only view tool");
            Check(!pres.Contains("NativeStationLive.View.")&&!pres.Contains("NativeStationLive.View=")&&!pres.Contains("NativeStationLive.Authority=")&&!Src("NativeExpeditionStationAudio.cs").Contains("NativeStationLive.View"),"the presentation never writes the live view (the authority fills it)");
            Check(pres.Contains("var view=NativeStationLive.Current;if(NativeStationPreview.On)")&&!NativeStationLive.Authority&&ReferenceEquals(NativeStationLive.Current,NativeStationLive.Calm),"normal play draws the calm station until the authority's station fields exist; the DEV preview overrides");

            // ---------------- the live look ----------------
            var look=set.maps.SingleOrDefault(m=>m.id==NativeExpeditionStation.MapId);
            Check(look!=null&&look.look==NativeExpeditionStation.Look&&station.look==look.look&&NativeBlockLooks.Dressed(look,false),"the station's live look, dressed");
            NativeMapLooks.Validate(set);count++;
            Check(look.dressing==NativeExpeditionStation.Look&&look.atmosphere==NativeExpeditionStation.Look,"the look names the station's dressing and atmosphere");
            foreach(var b in blocks){var c=NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0);Check(c.Listed,b.id+" has a listed look");}
            Check(!look.blockLooks.Any(b=>b.block.EndsWith("-screen",StringComparison.Ordinal)&&b.look=="cyber-glass"),"the screen is dressed glazing, not the city's curtain wall");

            // ---------------- the live area ----------------
            var area=live.Area("exp-transit");Check(area!=null&&area.map==NativeExpeditionStation.MapId&&area.spawns.Length>0&&area.machines.Length>0,"the station's live area: "+area?.spawns.Length+" spawns, "+area?.machines.Length+" machines");

            // ---------------- the kit: determinism, sanity, set pieces ----------------
            var sw=System.Diagnostics.Stopwatch.StartNew();var kit=NativeExpeditionStationKit.Build(blocks,area);long ms=sw.ElapsedMilliseconds;var again=NativeExpeditionStationKit.Build(blocks,area);
            Check(kit.Parts.Length>0&&kit.Parts.SequenceEqual(again.Parts)&&kit.Express.SequenceEqual(again.Express)&&kit.Glows.SequenceEqual(again.Glows)&&kit.Shafts.SequenceEqual(again.Shafts),"two builds give the same parts and lights ("+kit.Parts.Length+" parts, "+kit.Glows.Length+" lights, built in "+ms+" ms)");
            string Key(in NativeStationPart p,ContentBlock[] bs)=>bs[p.Parent].id+"|"+string.Join(",",new double[]{p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,p.Qx,p.Qy,p.Qz,p.Qw,p.Gain,p.Level,p.Param}.Select(v=>Math.Round(v,4)))+"|"+p.Look+"|"+p.Flags+"|"+p.Color+"|"+p.Drive+"|"+p.Role+"|"+p.Tag+"|"+p.Tier+p.Shadow+p.Fade;
            var reversed=blocks.Reverse().ToArray();var rev=NativeExpeditionStationKit.Build(reversed,area);
            Check(kit.Parts.Select(p=>Key(p,blocks)).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(rev.Parts.Select(p=>Key(p,reversed)).OrderBy(x=>x,StringComparer.Ordinal)),"the parts depend on the blocks' ids and places, not their order");
            Check(kit.Parts.All(p=>p.Parent>=0&&p.Parent<blocks.Length&&NativeExpeditionStationKit.Stem(blocks[p.Parent].id)!=null&&p.Hx>0&&p.Hy>0&&p.Hz>0&&Math.Abs(p.Qx*p.Qx+p.Qy*p.Qy+p.Qz*p.Qz+p.Qw*p.Qw-1)<1e-3)&&kit.Express.All(p=>p.Parent==-1&&p.Hx>0&&p.Hy>0&&p.Hz>0),"every part hangs on a station block (the express on its own frame), has a size and a unit turn");
            Check(kit.Parts.Concat(kit.Express).All(p=>(p.Flags&NativeCyberCityLooks.Bank)!=0?NativeCyberCityLooks.Keys[p.Look]!=null||NativeWardZeroLooks.Keys[p.Look]!=null:p.Look>=0&&p.Look<NativeBlockLooks.Keys.Length),"every part's look is one the shader draws");
            int Count(Func<NativeStationPart,bool> f)=>kit.Parts.Count(f);
            Check(Count(p=>p.Role==NativeExpeditionStationKit.RoleHigh&&blocks[p.Parent].id.Contains("roof-beam")&&p.Y+blocks[p.Parent].pos.y>18.5f)>300,"the vault: ribs, purlins and glazing over the concourse");
            Check(kit.Shafts.Count(s=>s.Kind==NativeExpeditionStationKit.ShaftMoon&&s.Room==NativeExpeditionStationKit.RoomConcourse)>=4&&kit.Shafts.Any(s=>s.Kind==NativeExpeditionStationKit.ShaftMoon&&s.Room==NativeExpeditionStationKit.RoomShed),"the moon falls through the vault's gaps and the shed's skylights");
            foreach(int tag in new[]{0,1,2,4*16,4*16+1,4*16+2,4*16+3,5*16,5*16+1})Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveBoard&&p.Tag==tag)>=4,"the board carries variant "+tag/16+"/"+tag%16+" on its faces");
            for(int pg=0;pg<NativeExpeditionStation.TickerPages.Length;pg++)Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveTicker&&p.Tag==pg)>=10,"the ticker's page "+pg);
            Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveSignal&&p.Tag==0)==6&&Count(p=>p.Drive==NativeExpeditionStationKit.DriveSignal&&p.Tag==1)==6,"a signal at every tunnel mouth, red over green");
            Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveBeacon)==4&&kit.Glows.Count(g=>g.Kind==NativeExpeditionStationKit.GlowBeacon)==4,"four beacons at the express line's mouths");
            for(int s=0;s<NativeExpeditionStation.RailSections;s++)Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveRail&&p.Tag==s)>=4&&!float.IsNaN(kit.RailX0[s]),"the live rail's section "+NativeExpeditionStation.RailIds[s]+" glints");
            Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveLever&&p.Tag==0)==32&&Count(p=>p.Drive==NativeExpeditionStationKit.DriveLever&&p.Tag==1)==32,"sixteen levers, home and thrown");
            Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveCabinet)==4&&Count(p=>p.Drive==NativeExpeditionStationKit.DriveFlood)==2&&kit.Waters.Count(w=>w.Kind==NativeExpeditionStationKit.WaterFlood)==2,"the cabinets' door gaps and the flood's two pools");
            Check(Count(p=>p.Role==NativeExpeditionStationKit.RoleSpawn&&blocks[p.Parent].id.Contains("train-car"))>=8*2,"the train's eight spawn doors");
            Check(kit.Express.Length>=150&&kit.Express.Count(p=>p.Tag==1)==6&&kit.Express.All(p=>p.Drive==NativeExpeditionStationKit.DriveExpress),"the express: three cars, their window bands");
            Check(Count(p=>p.Drive==NativeExpeditionStationKit.DriveMouth)==2,"the headlight's glow in the west mouth");

            // ---------------- placement ----------------
            var spawns=area.spawns.Select(x=>x.entry).ToArray();int low=0,flat=0,clutter=0;
            for(int i=0;i<kit.Parts.Length;i++)
            {
                var p=kit.Parts[i];var b=blocks[p.Parent];var w=Pose(b,p);string at=b.id+"#"+i+" (role "+p.Role+") at "+((w.X0+w.X1)/2).ToString("0.0")+", "+((w.Y0+w.Y1)/2).ToString("0.00")+", "+((w.Z0+w.Z1)/2).ToString("0.0");
                // The walkable top under the part (its highest below the part's base).
                double floorY=double.NegativeInfinity;foreach(var c in blocks){var s=NativeExpeditionStationKit.Stem(c.id);if(!Walkable(s))continue;double top=c.pos.y+c.size.y/2;if(top>w.Y0+.02||top<floorY)continue;if(Outside(c,(w.X0+w.X1)/2,(w.Z0+w.Z1)/2)>0)continue;floorY=top;}
                // Flat: paint on its floor, or anything floating on the flood's surface.
                bool isFlat=!double.IsNegativeInfinity(floorY)&&w.Y1<=floorY+NativeExpeditionStationKit.Flat+1e-4||p.Role==NativeExpeditionStationKit.RoleWater&&w.Y1<=NativeExpeditionStation.WaterY+.05;if(isFlat)flat++;
                if(!double.IsNegativeInfinity(floorY)&&w.Y0<floorY+NativeExpeditionStationKit.Low&&!isFlat)
                {
                    // Hugging: within 0.25 m of a block beside it, or standing over a solid (not walkable) block within 2.5 m (a lamp on a railing).
                    low++;bool hugs=true;for(int k=0;k<8&&hugs;k++){double best=double.MaxValue;foreach(var c in blocks){double ct=c.pos.y+c.size.y/2;bool beside=ct>=w.Y0-.3&&c.pos.y-c.size.y/2<=w.Y1+.05,under=!Walkable(NativeExpeditionStationKit.Stem(c.id))&&ct<=w.Y0+.01&&ct>=w.Y0-2.5;if(!beside&&!under)continue;best=Math.Min(best,Outside(c,w.Cx[k],w.Cz[k]));}if(best>NativeExpeditionStationKit.WallBand+.012)hugs=false;}
                    Check(hugs,"below 3.1 m a part stands within 0.25 m of a block (or on one): "+at);
                    // Free-standing on a walkable top under 2.2 m: low clutter only, unless it stands against a wall or furniture.
                    if(w.Y0<floorY+NativeExpeditionStationKit.Clutter&&w.Y1>floorY+NativeExpeditionStationKit.ClutterMax+1e-3)
                    {
                        // Against: within 0.25 m of a block that rises beside it (a wall, a platform's side over the pit, furniture) or that it
                        // stands on (a railing, a bench), not the floor under it.
                        bool against=true;for(int k=0;k<8&&against;k++){double best=double.MaxValue;foreach(var c in blocks){double ct=c.pos.y+c.size.y/2;bool beside=ct>=Math.Min(w.Y0+.1,w.Y1)-.01&&c.pos.y-c.size.y/2<=w.Y1+.05,stand=!Walkable(NativeExpeditionStationKit.Stem(c.id))&&ct<=w.Y0+.01&&ct>=w.Y0-2.5;if(!beside&&!stand)continue;best=Math.Min(best,Outside(c,w.Cx[k],w.Cz[k]));}if(best>NativeExpeditionStationKit.WallBand+.012)against=false;}
                        Check(against,"a free-standing part on a walkable top is at most 0.65 m tall: "+at+" ("+(w.Y1-floorY).ToString("0.00")+" m)");clutter++;
                    }
                }
                bool passable=p.Role==NativeExpeditionStationKit.RoleTrack&&w.Y1<NativeExpeditionStation.TrackBed+.36||p.Role==NativeExpeditionStationKit.RoleWater&&w.Y1<NativeExpeditionStation.WaterY+.05||p.Role==NativeExpeditionStationKit.RoleFlat&&w.Y1-w.Y0<=NativeExpeditionStationKit.Flat+1e-3;
                for(int s=0;s<spawns.Length;s++)
                {
                    var e=spawns[s];float clear=Math.Max(1.2f,e.clear),fy=e.exit.y;bool own=p.Role==NativeExpeditionStationKit.RoleSpawn&&Math.Sqrt((e.from.x-(w.X0+w.X1)/2)*(e.from.x-(w.X0+w.X1)/2)+(e.from.z-(w.Z0+w.Z1)/2)*(e.from.z-(w.Z0+w.Z1)/2))<5.2;
                    if(e.kind=="drop"){Check(!Disc(w,e.exit.x,e.exit.z,clear,fy-.05,fy+e.height+.5),"nothing in a drop column ("+e.id+"): "+at);continue;}
                    if(own||passable)continue;
                    Check(!Disc(w,e.exit.x,e.exit.z,clear,fy-.05,fy+(e.kind=="breach"?4.4:3.2)),"nothing on a spawn's exit ("+e.id+"): "+at);
                    if(e.kind=="vent")Check(!Disc(w,e.from.x,e.from.z,.9,fy-.05,fy+3.2),"nothing on a vent but its grate ("+e.id+"): "+at);
                    double x0=Math.Min(e.from.x,e.exit.x),x1=Math.Max(e.from.x,e.exit.x),z0=Math.Min(e.from.z,e.exit.z),z1=Math.Max(e.from.z,e.exit.z);bool alongX=x1-x0>z1-z0;double pw=e.kind=="breach"?2.0:1.1;
                    Check(!BoxHit(w,alongX?x0:x0-pw,alongX?x1:x1+pw,fy-.05,fy+(e.kind=="breach"?4.4:3.0),alongX?z0-pw:z0,alongX?z1+pw:z1),"nothing on a spawn's path ("+e.id+"): "+at);
                }
                void Point(ExpeditionPoint q,double r,double h,string what){if(q!=null)Check(!Disc(w,q.x,q.z,r,q.y-.05,q.y+h),"nothing on "+what+": "+at);}
                foreach(var q in area.bossPoints??Array.Empty<ExpeditionPoint>())Point(q,1.2,3.4,"a boss point");
                foreach(var m in area.machines)
                {
                    // Floor paint may run under a machine and its spots (as under a spawn's path); nothing else.
                    if(passable)continue;double bx=m.x-Math.Sin(m.yaw)*.25,bz=m.z-Math.Cos(m.yaw)*.25;Check(!Disc(w,bx,bz,.9,m.y-.05,m.y+2.4),"nothing on a machine ("+m.id+"): "+at);
                    foreach(var q in new[]{m.front,m.dispense,m.chipDrop})if(q!=null)Check(!Disc(w,q.x,q.z,.6,q.y-.05,q.y+2.2),"nothing where a machine's pilot stands or its drop lands ("+m.id+"): "+at);
                }
                foreach(var it in area.interactions)Check(!Disc(w,it.x,it.z,.9,it.y-.05,it.y+2.2),"nothing on an interaction point: "+at);
                foreach(var d in area.doors)
                {
                    Point(d.approach,.6,2.2,"the door's approach");foreach(var q in d.entries)Point(q,.6,2.2,"an entry slot");
                    foreach(var vol in new[]{d.interact,d.closure}){var x=NativeExpeditionCityKit.Aabb(vol,0);Check(!BoxHit(w,x[0],x[1],x[2],x[3],x[4],x[5]),"nothing in the exit door's volumes: "+at);}
                }
            }
            Check(low>0&&flat>0,"the rules saw both standing dressing and flat paint ("+low+" low parts, "+flat+" flat, "+clutter+" tall ones against walls or furniture)");
            foreach(var e in spawns.Where(e=>e.kind=="vent"&&e.from.y>-.5))Check(kit.Parts.Any(p=>{if(p.Role!=NativeExpeditionStationKit.RoleSpawn)return false;var w=Pose(blocks[p.Parent],p);double cx=(w.X0+w.X1)/2,cz=(w.Z0+w.Z1)/2;return Math.Sqrt((cx-e.from.x)*(cx-e.from.x)+(cz-e.from.z)*(cz-e.from.z))<.2;}),e.id+": its grate is dressed");

            // ---------------- budgets ----------------
            int total=kit.Parts.Count(p=>p.Tier!=NativeExpeditionStationKit.TierLow);
            for(int r=0;r<NativeExpeditionStationKit.Rooms;r++)Check(kit.RoomParts[r]<=NativeExpeditionStationKit.RoomBudget[r],NativeExpeditionStationKit.RoomNames[r]+": "+kit.RoomParts[r]+" parts within "+NativeExpeditionStationKit.RoomBudget[r]);
            int casters=kit.Casters,trims=total-casters,draws=(casters+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk+(trims+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk+(kit.Express.Length+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk+2;
            Check(draws<=NativeExpeditionStationKit.DrawBudget&&casters<=900&&kit.LowParts<total,"draws "+draws+" (kit "+total+" parts: "+casters+" casting; the express "+kit.Express.Length+"; the atmosphere 2) within "+NativeExpeditionStationKit.DrawBudget+"; the low tier draws "+kit.LowParts);
            var lights=NativeExpeditionStationLights.Build(kit,look);Check(lights.Candidates.Length>=40&&lights.Candidates.Length<=90,"light candidates: "+lights.Candidates.Length);
            var cams=new[]{(-1.2,1.7,31.6,"hall"),(-13.5,7.75,19.6,"concourse"),(17.5,1.7,-25.4,"P2"),(-25.5,1.55,-9.0,"P1"),(-40.6,1.6,-48.3,"flood"),(-28.0,1.7,-57.0,"corridor"),(34.0,7.75,-36.0,"footbridge"),(36,7.5,-53,"signal box"),(0,7.7,22,"gallery")};
            var view=new NativeStationView();var state=new NativeStationState();var scene=NativeStationFxScene.Build(kit,look);var fx=new NativeExpeditionStationFx{GlassView=view};int maxQuads=0;
            // Every hazard on at once (the worst frame): the express passing, the flood live, the cabinets arcing, the steam scalding, glass falling.
            view.TrainAt=40-.9;view.FloodLive=true;view.Cabinets[0]=view.Cabinets[1]=new NativeHazardState{Phase=NativeHazardPhase.Active,Level=1};view.Steam=new NativeHazardState{Phase=NativeHazardPhase.Active,Level=1};
            for(int g=0;g<4;g++)view.Glass[view.GlassCount++]=new NativeStationGlass{X=-6+g*4,Z=4+g*2,At=40+(g-2)*.4};state.Evaluate(view,40);
            foreach(var (x,y,z,name) in cams)
            {
                lights.Settle(x,y,z,state,40);int lit=lights.Lit();Check(lit>=3&&lit<=NativeExpeditionStationKit.Slots,name+": "+lit+" practical lights light the view");
                lights.Settle(x,y,z,state,40,NativeExpeditionStationKit.LowSlots);Check(lights.Lit()<=NativeExpeditionStationKit.LowSlots,name+": the low tier keeps 4 slots");
                fx.Build(scene,state,40,x,y,z,1,0,0,0,1,0,1,false);maxQuads=Math.Max(maxQuads,fx.Quads.Count);Check(fx.Quads.Count<fx.Quads.Capacity,name+": "+fx.Quads.Count+" effect quads within "+fx.Quads.Capacity);
                fx.Build(scene,state,40,x,y,z,1,0,0,0,1,0,1,true);
            }
            fx.Build(scene,state,40,-25.5,1.55,-9.0,0,0,-1,0,1,0,1,false);int tele=fx.Telegraph,full=fx.Quads.Count;fx.Build(scene,state,40,-25.5,1.55,-9.0,0,0,-1,0,1,0,1,true);
            Check(fx.Telegraph==tele&&fx.Quads.Count<full,"the low tier thins the atmosphere ("+full+" → "+fx.Quads.Count+" quads on P1) but never the telegraphs ("+tele+")");
            // Normal play (the calm view): no hazard drawn, lit or heard anywhere, over a whole minute.
            {
                var calm=new NativeStationState();var calmKinds=new byte[24];var calmTimes=new double[24];var calmPlaces=new float[72];int cues=0;
                for(double t=0;t<60;t+=.5)
                {
                    calm.Evaluate(NativeStationLive.Calm,t);cues+=NativeExpeditionStation.Cues(NativeStationLive.Calm,t,t+.5,calmKinds,calmTimes,calmPlaces);
                    Check(calm.Express==NativeStationState.ExpressIdle&&calm.Rails==0&&!calm.Flood&&calm.Danger==0&&calm.Glass==0&&calm.CabinetPhase.All(ph=>ph==NativeHazardPhase.Idle)&&calm.SteamPhase==NativeHazardPhase.Idle
                        &&calm.Row4==NativeStationState.Row4NoStop&&calm.Title==NativeStationState.TitleDepartures,"the calm station: no express, the third rail dark, the flood still, the cabinets and steam idle, the board's idle rows");
                }
                Check(cues==0,"the calm station makes no hazard sound");
                Check(kit.Parts.Where(p=>p.Drive==NativeExpeditionStationKit.DriveRail).All(p=>{NativeExpeditionStationKit.Drive(p,calm,30,out float r,out float g,out float b,out _);return r+g+b<1e-3;})&&kit.Express.All(p=>!NativeExpeditionStationKit.Drive(p,calm,30,out _,out _,out _,out _))
                    &&kit.Parts.Where(p=>p.Drive==NativeExpeditionStationKit.DriveMouth).All(p=>!NativeExpeditionStationKit.Drive(p,calm,30,out _,out _,out _,out _)),"the calm station: the rail's glints dark, no express, no headlight");
                var calmFx=new NativeExpeditionStationFx{GlassView=NativeStationLive.Calm};
                foreach(var (x,y,z,name) in cams)
                {
                    calmFx.Build(scene,calm,30,x,y,z,1,0,0,0,1,0,1,false);Check(calmFx.Telegraph==0&&calmFx.Sparks==0&&calmFx.Arcs==0&&calmFx.Glass==0&&calmFx.Dust==0,name+": the calm station draws no hazard effect");
                    lights.Settle(x,y,z,calm,30);for(int sl=0;sl<NativeExpeditionStationKit.Slots;sl++){int c=lights.Slot[sl];if(c<0)continue;byte kd=lights.Candidates[c].Kind;
                        Check(kd!=NativeExpeditionStationLights.KindHeadlight&&kd!=NativeExpeditionStationLights.KindBand&&kd!=NativeExpeditionStationKit.GlowBeacon,name+": no express or beacon light in the calm station");}
                }
            }

            // ---------------- the state ----------------
            var v=new NativeStationView();var s0=new NativeStationState();s0.Evaluate(v,100);
            Check(s0.Express==NativeStationState.ExpressIdle&&s0.Signal==NativeStationState.SignalRed&&s0.Rails==NativeExpeditionStation.AllRails&&!s0.Flood&&s0.Row0==NativeStationState.Row0Delayed&&s0.Row4==NativeStationState.Row4NoStop&&s0.Title==NativeStationState.TitleDepartures&&s0.Danger==0,
                "an untouched view is an idle station: signals red, every rail live, the board's standing rows");
            v.TrainAt=104;s0.Evaluate(v,101);float h1=s0.Headlight;s0.Evaluate(v,103.5);
            Check(s0.Express==NativeStationState.ExpressWarn&&s0.Headlight>h1&&s0.Signal==NativeStationState.SignalGreen&&s0.Danger>0&&s0.Row4==NativeStationState.Row4Approaching,"the warning: the headlight grows, the signals clear, the danger lamps flash, the board reads APPROACHING");
            s0.Evaluate(v,105);Check(s0.Express==NativeStationState.ExpressPass&&s0.Nose>-48&&s0.Nose<48+NativeExpeditionStation.TrainLength&&s0.Passing==1,"the pass: the nose in the station");
            s0.Evaluate(v,104+NativeExpeditionStation.PassSeconds+.5);Check(s0.Express==NativeStationState.ExpressAfter&&s0.Wake>0&&s0.Danger==0,"the wake: the dust settles, the lamps stop");
            s0.Evaluate(v,104+NativeExpeditionStation.PassSeconds+NativeExpeditionStation.WakeSeconds+.1);Check(s0.Express==NativeStationState.ExpressIdle&&s0.Row4==NativeStationState.Row4NoStop,"after it: idle again");
            v.Levers=true;v.LeversAt=103;v.LeversUntil=163;v.FloodLive=true;s0.Evaluate(v,103.5);
            Check(s0.Express==NativeStationState.ExpressHalted&&s0.Signal==NativeStationState.SignalDark&&s0.Rails==0&&!s0.Flood&&s0.Row4==NativeStationState.Row4Suspended&&s0.LeverPose>0&&s0.LeverPose<1,"the levers: the express halted, the signals dark, every rail and the flood dead, SUSPENDED, the levers moving");
            s0.Evaluate(v,170);Check(!s0.Levers&&s0.Rails==NativeExpeditionStation.AllRails&&s0.Flood,"power back after the levers' 60 s");
            v.Levers=false;v.LastCallAt=200;bool on=false,off=false;for(double t=200;t<205;t+=.05){s0.Evaluate(v,t);if(s0.Row0!=NativeStationState.Row0LastCall)continue;if(s0.BoardRowLit(0))on=true;else off=true;}
            Check(on&&off,"a last call blinks LAST CALL on row 0");s0.Evaluate(v,207);Check(s0.Row0==NativeStationState.Row0Delayed,"the last call clears");
            v.Boss=true;s0.Evaluate(v,210);Check(s0.Title==NativeStationState.TitleCancelled&&s0.Row0==NativeStationState.Row0Cancelled&&s0.Row4==NativeStationState.Row4Cancelled,"the boss: ALL SERVICES CANCELLED");
            // One variant lit per row and face; the ticker one page at a time; the express drawn only in the station.
            var shows=new Dictionary<int,int>();
            foreach(var (vv,t) in new[]{(new NativeStationView(),50.0),(new NativeStationView{TrainAt=52},50.0),(new NativeStationView{Levers=true,LeversAt=40,LeversUntil=100},50.0),(new NativeStationView{Boss=true},50.0),(new NativeStationView{LastCallAt=49.9},49.95)})
            {
                var st=new NativeStationState();st.Evaluate(vv,t);shows.Clear();
                foreach(var p in kit.Parts){if(p.Drive!=NativeExpeditionStationKit.DriveBoard||p.Tag<0)continue;if(!NativeExpeditionStationKit.Drive(p,st,t,out _,out _,out _,out _))continue;int row=p.Tag/16;shows[row*100+p.Tag%16]=1;}
                Check(shows.Keys.Count(k=>k/100==0)==1&&shows.Keys.Count(k=>k/100==4)==1&&shows.Keys.Count(k=>k/100==5)==1,"one variant lit per row (state "+st.Express+"/"+st.Row0+"/"+st.Row4+"/"+st.Title+")");
                int pages=kit.Parts.Where(p=>p.Drive==NativeExpeditionStationKit.DriveTicker).Where(p=>NativeExpeditionStationKit.Drive(p,st,t,out _,out _,out _,out _)).Select(p=>p.Tag).Distinct().Count();Check(pages==1,"the ticker shows one page");
                int ex=kit.Express.Count(p=>NativeExpeditionStationKit.Drive(p,st,t,out _,out _,out _,out _));Check(st.Express==NativeStationState.ExpressPass?ex==kit.Express.Length:ex==0,"the express is drawn only while it passes ("+st.Express+": "+ex+")");
            }
            // The signals' lamps and the rail's glints follow the state.
            {
                var st=new NativeStationState();st.Evaluate(new NativeStationView{TrainAt=52},50);float red=0,green=0;
                foreach(var p in kit.Parts.Where(p=>p.Drive==NativeExpeditionStationKit.DriveSignal)){NativeExpeditionStationKit.Drive(p,st,50,out float r,out float g,out _,out _);if(p.Tag==0)red=Math.Max(red,r);else green=Math.Max(green,g);}
                Check(green>1&&red<.2,"the warning lights the green aspects and darkens the red");
                st.Evaluate(new NativeStationView{RailLive=NativeExpeditionStation.AllRails&~(1<<NativeExpeditionStation.RailT2),RailChangedAt=40},50);
                Check(kit.Parts.Where(p=>p.Drive==NativeExpeditionStationKit.DriveRail&&p.Tag==NativeExpeditionStation.RailT2).All(p=>{NativeExpeditionStationKit.Drive(p,st,50,out float r,out float g,out float b,out _);return r+g+b<1e-3;})
                    &&kit.Parts.Where(p=>p.Drive==NativeExpeditionStationKit.DriveRail&&p.Tag==NativeExpeditionStation.RailT1W).All(p=>{NativeExpeditionStationKit.Drive(p,st,50,out float r,out float g,out float b,out _);return b>.1f;}),"a cut section's insulators go dark, the live ones glint");
            }
            // The cue timeline.
            var kinds=new byte[24];var times=new double[24];var places=new float[72];
            var cv=new NativeStationView{TrainAt=10};int n=NativeExpeditionStation.Cues(cv,5.9,6.1,kinds,times,places);
            Check(n==3&&kinds.Take(n).SequenceEqual(new[]{NativeExpeditionStation.CueWarn,NativeExpeditionStation.CueChime,NativeExpeditionStation.CueApproach}),"the warning's cues: the bell, the chime, the rails");
            Check(NativeExpeditionStation.Cues(cv,9.9,10.1,kinds,times,places)==1&&kinds[0]==NativeExpeditionStation.CuePass&&NativeExpeditionStation.Cues(cv,6.9,7.2,kinds,times,places)==1&&kinds[0]==NativeExpeditionStation.CueStandBack,"the pass and the PA's stand-back line");
            cv=new NativeStationView{Levers=true,LeversAt=20,LeversUntil=80,TrainAt=22};n=NativeExpeditionStation.Cues(cv,19.9,20.1,kinds,times,places);
            Check(n==4&&kinds[0]==NativeExpeditionStation.CueLever&&kinds[3]==NativeExpeditionStation.CuePower&&NativeExpeditionStation.Cues(cv,21,22.5,kinds,times,places)==0,"the levers' cues, and the halted express makes none");
            Check(NativeExpeditionStation.Cues(cv,79.9,80.1,kinds,times,places)==2&&kinds[1]==NativeExpeditionStation.CueRestored&&NativeExpeditionStation.Cues(cv,10,12,kinds,times,places)==0,"power restored; a long step plays nothing");
            Check(NativeExpeditionStation.CueKeys.Length==19&&NativeExpeditionStation.CueKeys.All(k=>k.StartsWith("station.",StringComparison.Ordinal)),"the cues' bank keys are the doc's station keys");
            // The DEV preview's cycle shows every state.
            bool warn=false,pass=false,lev=false,flood=false,boss=false,last=false,glass=false;var pv=new NativeStationView();var ps=new NativeStationState();
            for(double t=0;t<NativeStationPreview.Period;t+=.1){NativeStationPreview.Fill(pv,t);ps.Evaluate(pv,t);warn|=ps.Express==NativeStationState.ExpressWarn;pass|=ps.Express==NativeStationState.ExpressPass;lev|=ps.Levers;flood|=ps.Flood;boss|=ps.Title==NativeStationState.TitleCancelled;last|=ps.Row0==NativeStationState.Row0LastCall;glass|=ps.Glass>0;}
            Check(warn&&pass&&lev&&flood&&boss&&last&&glass&&pv.Preview,"the DEV preview cycles the express, the levers, the flood, the last call, the boss and the glass");

            // ---------------- no allocation per frame ----------------
            {
                var av=new NativeStationView();var ast=new NativeStationState();var al=NativeExpeditionStationLights.Build(kit,look);var afx=new NativeExpeditionStationFx{GlassView=av};float sink=0;
                void Frame(double t){NativeStationPreview.Fill(av,t);ast.Evaluate(av,t);foreach(var p in kit.Parts){NativeExpeditionStationKit.Drive(p,ast,t,out float r,out _,out _,out _);sink+=r;}foreach(var p in kit.Express)NativeExpeditionStationKit.Drive(p,ast,t,out _,out _,out _,out _);
                    al.Update(-13.5,7.75,19.6,ast,t,.016f);afx.Build(scene,ast,t,-13.5,7.75,19.6,1,0,0,0,1,0,1,false);NativeExpeditionStation.Cues(av,t-.016,t,kinds,times,places);}
                for(int f=0;f<30;f++)Frame(f*.4);
                long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<240;f++)Frame(30+f*.4);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(bytes==0,"the state, the drive, the lights, the atmosphere and the cues allocate nothing per frame ("+bytes+" bytes over 240 frames; "+sink.ToString("0")+")");
            }
            Console.WriteLine("PASS expedition station visuals ("+count+" checks; "+total+" kit parts: "+string.Join(", ",Enumerable.Range(0,NativeExpeditionStationKit.Rooms).Select(r=>NativeExpeditionStationKit.RoomNames[r]+" "+kit.RoomParts[r]))+"; low "+kit.LowParts+"; express "+kit.Express.Length+"; "+draws+" draws; "+lights.Candidates.Length+" light candidates; up to "+maxQuads+" quads)");
        }
    }
}
