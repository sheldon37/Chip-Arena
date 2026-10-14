using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Expedition town's dressing (docs/EXPEDITION-LAYOUT.md "Town dressing"; Core NativeExpeditionCity{Kit,Scenery,Fx,Lights},
    // Runtime NativeExpeditionCityPresentation). Checks, from the imported map (Resources/UnityMaps.json), the town's records
    // (Resources/ExpeditionLayout.json) and the look (Resources/MapLooks.json):
    //  - determinism: two builds give the same parts; each block's parts depend only on its id, the geometry and the records
    //    (a build over the blocks in reverse order gives every block the same parts);
    //  - placement, recomputed here from the records (not from the kit's own keep-out list): below 3.1 m nothing stands out more
    //    than 0.25 m from a block except flat paint (at most 6 cm); nothing meets a spawn's exit or its path, a door spawn's
    //    doorway (other than its own frame), a vent (other than its grate), a drop column, a slot, a return, the approach, an
    //    interaction or boss point, the instance door's or the final gate's volumes; the sanctuary's floor holds only flat paint
    //    below 2.6 m away from its barricades and brazier; every door spawn has its doorway and every vent its grate;
    //  - other maps: the kit builds nothing for any other map; the skyline stands beyond the town;
    //  - budgets: parts, low-tier parts, casters, skyline boxes, fires, effect quads, light candidates;
    //  - no allocation per frame: the drive, the atmosphere and the light picker over many frames allocate nothing;
    //  - the runtime is hooked into SourceWorld and gated on the town's map id.
    // Static data and pure-code checks only: no Unity scene, GPU, PhysX or network claim.
    internal static class ExpeditionDressingChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition dressing: "+what);count++;}
        /// <summary>The budgets (instances per draw are NativeBlockLooks.Chunk = 500).</summary>
        internal const int MaxParts=8000,MaxLowParts=5000,MaxCasters=1000,MaxBackdrop=3000,MaxQuads=NativeExpeditionCityFx.Capacity;
        private struct World{public double X0,X1,Y0,Y1,Z0,Z1;public double[] Cx,Cz;}
        private static World Pose(ContentBlock b,in NativeCityPart p)
        {
            var r=b.rotation;double qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r?.w??1;NativeExpeditionCityKit.Rotate(qx,qy,qz,qw,p.X,p.Y,p.Z,out double ox,out double oy,out double oz);
            double wx=qw*p.Qx+qx*p.Qw+qy*p.Qz-qz*p.Qy,wy=qw*p.Qy-qx*p.Qz+qy*p.Qw+qz*p.Qx,wz=qw*p.Qz+qx*p.Qy-qy*p.Qx+qz*p.Qw,ww=qw*p.Qw-qx*p.Qx-qy*p.Qy-qz*p.Qz;
            var w=new World{Cx=new double[8],Cz=new double[8]};NativeExpeditionCityKit.Extent(b.pos.x+ox,b.pos.y+oy,b.pos.z+oz,p.Hx,p.Hy,p.Hz,wx,wy,wz,ww,out w.X0,out w.X1,out w.Y0,out w.Y1,out w.Z0,out w.Z1);
            for(int k=0;k<8;k++){NativeExpeditionCityKit.Rotate(wx,wy,wz,ww,(k&1)==0?-p.Hx:p.Hx,(k&2)==0?-p.Hy:p.Hy,(k&4)==0?-p.Hz:p.Hz,out double cx,out double cy,out double cz);w.Cx[k]=b.pos.x+ox+cx;w.Cz[k]=b.pos.z+oz+cz;}
            return w;
        }
        // Horizontal distance from (x, z) to a block's turned footprint (0 inside).
        private static double Outside(ContentBlock b,double x,double z)
        {
            var r=b.rotation;double qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r?.w??1;NativeExpeditionCityKit.Rotate(-qx,-qy,-qz,qw,x-b.pos.x,0,z-b.pos.z,out double lx,out _,out double lz);
            double dx=Math.Max(0,Math.Abs(lx)-b.size.x/2),dz=Math.Max(0,Math.Abs(lz)-b.size.z/2);return Math.Sqrt(dx*dx+dz*dz);
        }
        private static bool Disc(in World w,double x,double z,double r,double y0,double y1)
        {
            if(w.Y1<=y0||w.Y0>=y1)return false;double cx=Math.Max(w.X0,Math.Min(x,w.X1)),cz=Math.Max(w.Z0,Math.Min(z,w.Z1));return (cx-x)*(cx-x)+(cz-z)*(cz-z)<r*r-1e-6;
        }
        private static bool BoxHit(in World w,double x0,double x1,double y0,double y1,double z0,double z1)=>w.X1>x0+1e-4&&w.X0<x1-1e-4&&w.Y1>y0+1e-4&&w.Y0<y1-1e-4&&w.Z1>z0+1e-4&&w.Z0<z1-1e-4;
        private static double[] Aabb(ExpeditionVolume v)
        {
            double a=v.yaw*Math.PI/180,c=Math.Abs(Math.Cos(a)),s=Math.Abs(Math.Sin(a)),hx=c*v.sx/2+s*v.sz/2,hz=s*v.sx/2+c*v.sz/2;return new[]{v.x-hx,v.x+hx,v.y-v.sy/2,v.y+v.sy/2,v.z-hz,v.z+hz};
        }

        internal static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var maps=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o).maps;var town=maps.Single(m=>m.id==NativeExpeditionCityKit.MapId);var blocks=town.blocks;
            var layout=NativeExpeditionLayout.Parse(R("Assets/Conversion/Resources/ExpeditionLayout.json"));var area=layout.areas.Single(a=>a.map==town.id);
            var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,town.look);
            Check(look!=null&&look.id==town.id&&NativeBlockLooks.Dressed(look,false),"the town's look is dressed");
            Check(look.dressing==NativeExpeditionCityKit.MapId&&look.backdrop==NativeExpeditionCityKit.MapId&&look.atmosphere==NativeExpeditionCityKit.MapId&&look.ambience==NativeExpeditionCityAmbience.Id,"the look names the town's dressing, backdrop, atmosphere and ambience");

            // ---------------- determinism ----------------
            var sw=System.Diagnostics.Stopwatch.StartNew();var kit=NativeExpeditionCityKit.Build(blocks,area);long ms=sw.ElapsedMilliseconds;var again=NativeExpeditionCityKit.Build(blocks,area);
            Check(kit.Parts.Length>0&&kit.Parts.SequenceEqual(again.Parts)&&kit.Glows.SequenceEqual(again.Glows),"two builds give the same parts and lights ("+kit.Parts.Length+" parts, "+kit.Glows.Length+" lights, built in "+ms+" ms)");
            string Key(in NativeCityPart p,ContentBlock[] bs)=>bs[p.Parent].id+"|"+string.Join(",",new double[]{p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,p.Qx,p.Qy,p.Qz,p.Qw,p.Gain,p.Level,p.Param}.Select(v=>Math.Round(v,4)))+"|"+p.Look+"|"+p.Flags+"|"+p.Color+"|"+p.Drive+"|"+p.Role+"|"+p.Shadow+p.Fade+p.Detail;
            var reversed=blocks.Reverse().ToArray();var flat=NativeExpeditionCityKit.Build(blocks,area,false);var flatRev=NativeExpeditionCityKit.Build(reversed,area,false);
            var mine=flat.Parts.Select(p=>Key(p,blocks)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();var theirs=flatRev.Parts.Select(p=>Key(p,reversed)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            Check(mine.SequenceEqual(theirs),"each block's dressing depends on its id, not on the blocks' order ("+mine.Length+" parts compared before the layers)");
            Check(kit.Parts.All(p=>p.Parent>=0&&p.Parent<blocks.Length&&NativeExpeditionCityKit.Stem(blocks[p.Parent].id)!=null&&p.Hx>0&&p.Hy>0&&p.Hz>0&&Math.Abs(p.Qx*p.Qx+p.Qy*p.Qy+p.Qz*p.Qz+p.Qw*p.Qw-1)<1e-3),"every part hangs on a town block, has a size and a unit turn");
            Check(kit.Parts.All(p=>(p.Flags&NativeCyberCityLooks.Bank)!=0?NativeCyberCityLooks.Keys[p.Look]!=null||NativeWardZeroLooks.Keys[p.Look]!=null:p.Look>=0&&p.Look<NativeBlockLooks.Keys.Length),"every part's look is one the shader draws");

            // ---------------- placement ----------------
            var solid=blocks.Where(b=>{var s=NativeExpeditionCityKit.Stem(b.id);return !(s.StartsWith("slab-",StringComparison.Ordinal)||s.EndsWith("-paving",StringComparison.Ordinal));}).ToArray();
            int low=0,flatParts=0;var spawns=area.spawns.Select(s=>s.entry).ToArray();
            for(int i=0;i<kit.Parts.Length;i++)
            {
                var p=kit.Parts[i];var b=blocks[p.Parent];var w=Pose(b,p);string at=b.id+"#"+i+" (role "+p.Role+") at "+((w.X0+w.X1)/2).ToString("0.0")+", "+((w.Y0+w.Y1)/2).ToString("0.00")+", "+((w.Z0+w.Z1)/2).ToString("0.0");
                bool isFlat=w.Y1<=NativeExpeditionCityKit.Flat+1e-4;if(isFlat)flatParts++;
                if(w.Y0<NativeExpeditionCityKit.Low&&!isFlat)
                {
                    low++;bool hugs=true;for(int k=0;k<8&&hugs;k++){double best=double.MaxValue;foreach(var c in solid){if(c.pos.y+c.size.y/2<w.Y0-.3||c.pos.y-c.size.y/2>w.Y1)continue;best=Math.Min(best,Outside(c,w.Cx[k],w.Cz[k]));}if(best>NativeExpeditionCityKit.WallBand+.012)hugs=false;}
                    Check(hugs,"below 3.1 m a part stands within 0.25 m of a block (or on one): "+at);
                }
                for(int s=0;s<spawns.Length;s++)
                {
                    var e=spawns[s];float clear=Math.Max(1.2f,e.clear);bool own=p.Role==NativeExpeditionCityKit.RoleSpawnDoor&&e.kind=="door"||p.Role==NativeExpeditionCityKit.RoleVent&&e.kind=="vent";
                    if(e.kind=="drop"){Check(!Disc(w,e.exit.x,e.exit.z,clear,-1,e.height+2),"nothing in a drop column ("+e.id+"): "+at);continue;}
                    if(own&&Math.Sqrt((e.from.x-(w.X0+w.X1)/2)*(e.from.x-(w.X0+w.X1)/2)+(e.from.z-(w.Z0+w.Z1)/2)*(e.from.z-(w.Z0+w.Z1)/2))<3.6)continue;
                    Check(!Disc(w,e.exit.x,e.exit.z,clear,-1,e.kind=="breach"?4.4:3.2),"nothing on a spawn's exit ("+e.id+"): "+at);
                    if(e.kind=="vent")Check(!Disc(w,e.from.x,e.from.z,.75,-1,3),"nothing on a vent but its grate ("+e.id+"): "+at);
                }
                void Point(ExpeditionPoint q,double r,double h,string what){if(q!=null)Check(!Disc(w,q.x,q.z,r,q.y-1,q.y+h),"nothing on "+what+": "+at);}
                foreach(var q in area.slots)Point(q,.6,2.2,"a sanctuary slot");foreach(var q in area.bossPoints)Point(q,1.2,3.4,"a boss point");
                foreach(var it in area.interactions)Check(!Disc(w,it.x,it.z,.9,it.y-1,it.y+2.2),"nothing on an interaction point: "+at);
                foreach(var a in area.anchors)Check(!Disc(w,a.x,a.z,.9,a.y-1,a.y+2.2),"nothing on an anchor: "+at);
                foreach(var d in area.doors)
                {
                    Point(d.approach,.6,2.2,"the door's approach");foreach(var q in d.returns??Array.Empty<ExpeditionPoint>())Point(q,.6,2.2,"a door return");
                    foreach(var v in new[]{d.interact,d.closure}){var x=Aabb(v);Check(!BoxHit(w,x[0],x[1],x[2],x[3],x[4],x[5]),"nothing in the instance door's volumes: "+at);}
                }
                foreach(var g in area.gates){var x=Aabb(g.closure);Check(!BoxHit(w,x[0]-.5,x[1]+.5,x[2],x[3]+.5,x[4]-.5,x[5]+.5),"nothing in the final gate's volume: "+at);}
                // The sanctuary's floor: flat paint only below 2.6 m, except on and against the barricades and the brazier.
                foreach(var v in area.sanctuary)
                {
                    var x=Aabb(v);if(isFlat||w.Y0>=2.6||!BoxHit(w,x[0],x[1],x[2],x[3],x[4],x[5]))continue;var stem=NativeExpeditionCityKit.Stem(b.id);
                    bool onWall=stem.StartsWith("bldg-",StringComparison.Ordinal)&&Enumerable.Range(0,8).All(k=>Outside(b,w.Cx[k],w.Cz[k])<=NativeExpeditionCityKit.WallBand+.012);
                    Check(stem.StartsWith("sanctuary-barricade-",StringComparison.Ordinal)||stem=="sanctuary-brazier"||onWall,"the sanctuary floor holds only flat paint, the barricades' and brazier's dressing and its walls' own: "+at);
                }
            }
            // Every door spawn has its doorway and every vent its grate; nothing else covers a doorway.
            foreach(var e in spawns.Where(e=>e.kind=="door"||e.kind=="vent"))
            {
                byte role=e.kind=="door"?NativeExpeditionCityKit.RoleSpawnDoor:NativeExpeditionCityKit.RoleVent;
                Check(kit.Parts.Any(p=>{if(p.Role!=role)return false;var w=Pose(blocks[p.Parent],p);double cx=(w.X0+w.X1)/2,cz=(w.Z0+w.Z1)/2;return Math.Sqrt((cx-e.from.x)*(cx-e.from.x)+(cz-e.from.z)*(cz-e.from.z))<1.0;}),e.id+": its "+(e.kind=="door"?"doorway":"grate")+" is dressed");
                if(e.kind!="door")continue;double dx=e.exit.x-e.from.x,dz=e.exit.z-e.from.z,l=Math.Sqrt(dx*dx+dz*dz);dx/=l;dz/=l;double px=e.from.x+dx*.6,pz=e.from.z+dz*.6;
                foreach(var p in kit.Parts){if(p.Role==NativeExpeditionCityKit.RoleSpawnDoor)continue;var w=Pose(blocks[p.Parent],p);
                    Check(!BoxHit(w,Math.Min(px,px+dx*.3)-Math.Abs(dz)*.65-1e-3,Math.Max(px,px+dx*.3)+Math.Abs(dz)*.65+1e-3,0,2.45,Math.Min(pz,pz+dz*.3)-Math.Abs(dx)*.65-1e-3,Math.Max(pz,pz+dz*.3)+Math.Abs(dx)*.65+1e-3),e.id+": nothing but its frame covers the doorway");}
            }
            Check(low>0&&flatParts>0,"the rules saw both wall dressing and flat paint ("+low+" low parts, "+flatParts+" flat)");

            // ---------------- other maps ----------------
            foreach(var m in maps.Where(m=>m.id!=town.id))Check(NativeExpeditionCityKit.Build(m.blocks,null).Parts.Length==0,m.id+": the town kit builds nothing for it");
            Check(!NativeExpeditionCityKit.Active("brickfall")&&!NativeExpeditionCityKit.Active("cybercity")&&!NativeExpeditionCityKit.Active("siren")&&!NativeExpeditionCityKit.Active("expedition-ward-zero")&&NativeExpeditionCityKit.Active(town.id),"the dressing is gated on the town's map id");
            var back=NativeExpeditionCityScenery.Build(out var fires);var back2=NativeExpeditionCityScenery.Build(out var fires2);
            Check(back.SequenceEqual(back2)&&fires.SequenceEqual(fires2),"the skyline is the same every build");
            Check(back.All(b=>b.Y+b.Hy<=.001f&&!NativeExpeditionCityScenery.Inside(b,NativeExpeditionCityScenery.Edge-.001)||!NativeExpeditionCityScenery.Inside(b)),"the skyline stands beyond ±"+NativeExpeditionCityScenery.Inner+" m; its ground plates meet the town's edge");

            // ---------------- budgets ----------------
            int lowTier=kit.Parts.Count(p=>!p.Detail),casters=kit.Parts.Count(p=>p.Shadow);int Draws(int n)=>(n+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;
            Check(kit.Parts.Length<=MaxParts&&lowTier<=MaxLowParts&&casters<=MaxCasters,"kit budget: "+kit.Parts.Length+" parts (≤"+MaxParts+"), "+lowTier+" on the low tier (≤"+MaxLowParts+"), "+casters+" casting (≤"+MaxCasters+")");
            Check(back.Length<=MaxBackdrop&&fires.Length>=3&&fires.Length<=NativeExpeditionCityScenery.MaxFires,"skyline budget: "+back.Length+" boxes (≤"+MaxBackdrop+"), "+fires.Length+" burning towers");
            Console.WriteLine("   town dressing: kit "+kit.Parts.Length+" parts in "+(Draws(kit.Parts.Length-casters)+Draws(casters))+" draws ("+casters+" casting), low tier "+lowTier+" in "+(Draws(lowTier)+1)+"; skyline "+back.Length+" boxes in "+Draws(back.Length)+" draws; "+kit.Glows.Length+" light sources; "+kit.Rejected+" placements refused by the records");
            var scene=NativeCityFxScene.Build(blocks,kit,fires,look);var lights=NativeExpeditionCityLights.Build(kit,look);var fx=new NativeExpeditionCityFx();var sun=new NativeFxLight();for(int k=0;k<9;k++)sun.Ambient[k]=.05;
            Check(lights.Candidates.Length>=30&&lights.Candidates.Count(c=>c.Kind==255)==look.lights.Length,"the picker pools the look's "+look.lights.Length+" lights and the kit's ("+lights.Candidates.Length+" candidates)");
            int most=0;double[][] eyes={new[]{0,2.3,6.0},new[]{0,2.3,20.0},new[]{30,2.3,0.0},new[]{0,2.2,45.0},new[]{-20,2.3,-20.0},new[]{0,60,-80.0}};
            foreach(var q in new[]{1.0,.3})foreach(bool lo in new[]{false,true})foreach(var e in eyes){fx.Build(scene,31.3,e[0],e[1],e[2],1,0,0,0,1,0,sun,q,lo);most=Math.Max(most,fx.Quads.Count);Check(fx.Quads.Count<MaxQuads,"the atmosphere fits its buffer");}
            fx.Build(scene,31.3,0,2.3,6,1,0,0,0,1,0,sun,1,false);int full=fx.Quads.Count,flames=fx.Flames;fx.Build(scene,31.3,0,2.3,6,1,0,0,0,1,0,sun,.3,true);
            Check(fx.Quads.Count<full&&fx.Flames==flames,"the low tier thins the atmosphere ("+full+" → "+fx.Quads.Count+" quads at the sanctuary) but keeps the flames");
            Console.WriteLine("   town atmosphere: at most "+most+" quads (buffer "+MaxQuads+"), two draws");

            // ---------------- no allocation per frame ----------------
            void Frame(int f){double t=20+f*.016;fx.Build(scene,t,Math.Sin(f*.01)*30,2.3,Math.Cos(f*.01)*30,1,0,0,0,1,0,sun,1,false);lights.Update(Math.Sin(f*.01)*30,2.3,Math.Cos(f*.01)*30,t,.016f);
                for(int i=0;i<kit.Parts.Length;i++)NativeExpeditionCityKit.Drive(kit.Parts[i],t,out _,out _,out _,out _);}
            for(int f=0;f<5;f++)Frame(f);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<120;f++)Frame(f);long spent=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(spent==0,"the drive, the atmosphere and the light picker allocate nothing per frame ("+spent+" bytes over 120 frames)");

            // ---------------- ambience ----------------
            var cues=new NativeCityCue[64];var half1=new NativeCityCue[64];var half2=new NativeCityCue[64];int whole=0,split=0;
            for(double t=10;t<190;t+=4){int n=NativeExpeditionCityAmbience.Between(t,t+4,cues);whole+=n;split+=NativeExpeditionCityAmbience.Between(t,t+1.5,half1)+NativeExpeditionCityAmbience.Between(t+1.5,t+4,half2);
                for(int i=0;i<n;i++){var c=cues[i];double r=Math.Sqrt(c.At.x*c.At.x+c.At.z*c.At.z);Check(c.Time>t&&c.Time<=t+4&&(c.Kind==NativeExpeditionCityAmbience.Crackle?r<25:r>=140),"an ambience cue lies in its span, distant ones beyond the town");}}
            Check(whole>40&&whole==split,"the ambience schedule depends on the clock alone ("+whole+" cues in 180 s, the same however the span is cut)");
            long before2=GC.GetAllocatedBytesForCurrentThread();for(double t=10;t<70;t+=.016)NativeExpeditionCityAmbience.Between(t,t+.016,cues);
            Check(GC.GetAllocatedBytesForCurrentThread()==before2,"the ambience schedule allocates nothing per frame");

            // ---------------- runtime hooks ----------------
            string Src(string f)=>R("Assets/ChipArena/Runtime/"+f);var pres=Src("NativeExpeditionCityPresentation.cs");
            Check(Src("NativeAudio.cs").Contains("ExpeditionCityCues(arena,menu);")&&Src("NativeExpeditionCityAudio.cs").Contains("if(menu||arena==null||!NativeExpeditionCityKit.Active(arena.MapId))"),"the town's cues play from NativeAudio, only on the town");
            Check(Src("NativeSourcePresentation.cs").Contains("ExpeditionCityScenery(arena);")&&pres.Contains("if(!NativeExpeditionCityKit.Active(arena.MapId)")&&Src("ArenaPresentation.cs").Contains("DisposeExpeditionCity();"),"the runtime draws the dressing from SourceWorld, only on the town, and disposes it");
            Check(pres.Contains("bool lean=LowGraphics||!UsesInstancing")&&pres.Contains("if(!UsesInstancing)return;const int layer=NativeSceneryBuilder.BackdropLayer;")&&pres.Contains("NativeLighting.PickedLights(townLights.Pos,townLights.Rgb)"),"the low tier and compatibility rendering drop the detail parts and the skyline; the lights go through the picker");
            Console.WriteLine("PASS expedition dressing: "+count+" assertions (determinism, placement against the town's records, other maps, budgets, no per-frame allocation, hooks)");
        }
    }
}
