using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

/// <summary>
/// The Training Room's presentation (docs/arenas/NEW-STAGES.md "Training Room presentation"; Core NativeTrainingLook,
/// NativeTrainingReadouts, NativeTrainingBay; Runtime/NativeTrainingPresentation.cs; Resources/NativeTrainingStudio.shader;
/// MapLooks.json "training-studio"): the marks and boards are deterministic and placed from the map's authored pads; nothing
/// stands in play space beyond the floor, the walls, block faces and the roof band; the readouts format, quantise and paint
/// the same from a host's rows and a guest's replicated copy; the per-frame paths allocate nothing; the runtime wiring is
/// the training map's alone. `--training-look`, and in the full suite.
/// </summary>
internal static class TrainingLookChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Training look: "+what);count++;}
    public static void Run(string root)
    {
        count=0;int last=0;int Since(){int n=count-last;last=count;return n;}var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var maps=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o);var map=maps.maps.Single(m=>m.id=="training");
        var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);NativeMapLooks.Validate(set);
        Layout(map);Console.WriteLine("PASS training look: the firing line, lanes, distances, ring and every pad's labels come from the authored pads; one readout cell per pad on its distance's board over its lane, the ring's board west to east ("+Since()+" checks)");
        Placement(map);Console.WriteLine("PASS training look: every mark lies on the floor (at most "+NativeTrainingLook.FloorPaintTop+" m), on a hall wall's or a block's face (at most "+NativeTrainingLook.FacePaint+" m proud) or in the roof band (from "+NativeTrainingLook.RoofBand+" m, under the roof, clear of every block and out of reach); the bay's fittings stay on the floor ("+Since()+" checks)");
        Determinism(map);Console.WriteLine("PASS training look: the marks, grids and cells build identically twice; the strafe rail runs from rail.a to rail.b ("+Since()+" checks)");
        Readouts(map);Console.WriteLine("PASS training look: readout numbers, source labels and distance labels format as drawn; the key changes only when a drawn value does; a guest's replicated rows paint the host's pixels ("+Since()+" checks)");
        Bay(map);Console.WriteLine("PASS training look: the bay draws every authored sample kind from the simulation's clocks, within its capacities through whole cycles, under the roof ("+Since()+" checks)");
        Allocation(map);Console.WriteLine("PASS training look: the bay's rigs and effects, the readout key, the cell painter and the tag rules allocate nothing per frame ("+Since()+" checks)");
        Look(map,set);Console.WriteLine("PASS training look: the training-studio look (sealed studio, no shadows, dressed, the anime style, every kit kind the bay draws listed, every block pattern matching) and no other map's look draws it ("+Since()+" checks)");
        Wiring(root,maps);Console.WriteLine("PASS training look: the runtime draws only the training map from SourceWorld and the prewarm, polls and repaints by change, allocates nothing per frame, the studio shader avoids reserved names, metas are fresh ("+Since()+" checks)");
    }

    // ---- placement from the pads ----
    private static void Layout(ContentMap map)
    {
        var plan=NativeTrainingPlan.Of(map);var range=map.trainingPads.Where(p=>p.kind!="melee").ToArray();var melee=map.trainingPads.Where(p=>p.kind=="melee").ToArray();
        foreach(var p in range)Check(Math.Abs(p.pos.x-p.distance-plan.FiringX)<.01f,"firing line from "+p.id);
        Check(plan.LaneZ.Length==4&&plan.LaneZ.SequenceEqual(new[]{13.75f,17.55f,21.35f,25.15f})&&Math.Abs(plan.LaneHalf-1.75f)<1e-3f,"four lanes at the pads' common z, 3.5 m wide");
        Check(plan.Distances.SequenceEqual(range.Select(p=>p.distance).Distinct().OrderBy(d=>d)),"one distance per authored distance");
        Check(Math.Abs(plan.RingX-melee.Average(p=>p.pos.x))<1e-4&&Math.Abs(plan.RingZ-melee.Average(p=>p.pos.z))<1e-4,"the ring centre is the melee pads' mean");
        foreach(var info in plan.Pads)
        {
            Check(info.DistanceLabel==NativeTrainingPlan.Metres(info.Pad.distance)&&(info.Melee?info.Header.StartsWith("MELEE "):info.Header=="L"+(info.Lane+1)+" · "+info.DistanceLabel),"labels of "+info.Id);
            if(!info.Melee)Check(Math.Abs(plan.LaneZ[info.Lane]-info.Pad.pos.z)<.5f&&info.Short=="L"+(info.Lane+1)+"-"+NativeTrainingPlan.Metres(info.Pad.distance,false),"lane of "+info.Id);
        }
        Check(NativeTrainingPlan.Metres(10)=="10 M"&&NativeTrainingPlan.Metres(3.54f)=="3.5 M"&&NativeTrainingPlan.Metres(80,false)=="80","distance labels");
        var marks=NativeTrainingMarks.Build(map);
        Check(marks.Cells.Length==map.trainingPads.Length&&marks.Cells.Select(c=>c.Pad).Distinct().Count()==map.trainingPads.Length,"one readout cell per pad");
        foreach(var c in marks.Cells)
        {
            var info=plan.Pads[c.Pad];
            Check(Math.Abs(c.Width/c.Height-NativeTrainingLook.CellAspect)<.05f,"cell aspect "+info.Id);
            Check(c.Center.y-c.Height/2>=NativeTrainingLook.RoofBand,"cell above the roof band "+info.Id);
            if(info.Melee)Check(c.Normal.z==-1&&c.Right.x==1&&Math.Abs(c.Center.z-(plan.StripZ0-.5f-NativeTrainingLook.BoardDepth/2-.016f))<.01f,"ring board faces the ring "+info.Id);
            else Check(c.Normal.x==-1&&c.Right.z==-1&&Math.Abs(c.Center.z-plan.LaneZ[info.Lane])<1e-4&&Math.Abs(c.Center.x-(plan.FiringX+info.Pad.distance))<.2f,"range cell over its lane at its distance "+info.Id);
        }
        var ring=marks.Cells.Where(c=>plan.Pads[c.Pad].Melee).ToArray();Check(ring.Length==4,"four ring cells");
        for(int i=1;i<ring.Length;i++){var a=plan.Pads[ring[i-1].Pad].Pad.pos;var b=plan.Pads[ring[i].Pad].Pad.pos;Check(ring[i].Center.x>ring[i-1].Center.x&&(a.x<b.x||a.x==b.x&&a.z>b.z),"ring cells west to east, north first");}
        // the boards: one per authored distance, each over its distance line; its header names it
        foreach(float d in plan.Distances)Check(marks.Parts.Any(p=>p.Tag=="board "+NativeTrainingPlan.Metres(d)&&p.At==NativeTrainingMesh.Place.Roof&&p.X0<=plan.FiringX+d&&p.X1>=plan.FiringX+d),"distance board at "+d);
        // every pad's puck and plate are where the pad is
        foreach(var info in plan.Pads)
        {
            var parts=marks.Parts.Where(p=>p.Tag=="pad "+info.Id).ToArray();Check(parts.Length>20,"pad marks "+info.Id);
            Check(parts.Any(p=>p.X0<=info.Pad.pos.x&&p.X1>=info.Pad.pos.x&&p.Z0<=info.Pad.pos.z&&p.Z1>=info.Pad.pos.z),"the puck under "+info.Id);
            Check(parts.All(p=>Math.Abs((p.X0+p.X1)/2-info.Pad.pos.x)<3.2f&&Math.Abs((p.Z0+p.Z1)/2-info.Pad.pos.z)<3.2f),"a pad's marks stay by it "+info.Id);
        }
        // the grids: the hall's on whole metres, the range's from the firing line over the lanes
        Check(marks.Grids.Length==2&&marks.Grids[0].X0==plan.MinX&&marks.Grids[0].X1==plan.MaxX&&marks.Grids[0].Z0==plan.MinZ&&marks.Grids[0].Z1==plan.MaxZ&&marks.Grids[0].OriginX==0,"hall grid");
        Check(marks.Grids[1].OriginX==plan.FiringX&&marks.Grids[1].LinesX==1&&marks.Grids[1].LinesZ==0&&marks.Grids[1].X0==plan.FiringX&&marks.Grids[0].CutX0==plan.FiringX&&marks.Grids[0].CutZ1==marks.Grids[1].Z1,"range grid measured from the firing line, cut from the hall's");
    }

    // ---- nothing in play space ----
    private static void Placement(ContentMap map)
    {
        var marks=NativeTrainingMarks.Build(map);var p=marks.Plan;float eps=1e-3f;
        var blocks=map.blocks.Select(b=>(id:b.id,x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
        int floor=0,wall=0,face=0,roof=0;
        foreach(var q in marks.Parts)
        {
            Check(q.X0>=p.MinX-eps&&q.X1<=p.MaxX+eps&&q.Z0>=p.MinZ-eps&&q.Z1<=p.MaxZ+eps&&q.Y0>=-eps&&q.Y1<=p.Roof+eps,"inside the hall: "+q.Tag);
            switch(q.At)
            {
                case NativeTrainingMesh.Place.Floor:floor++;Check(q.Y1<=NativeTrainingLook.FloorPaintTop&&q.Y0>=0,"floor paint stays on the floor: "+q.Tag);break;
                case NativeTrainingMesh.Place.Wall:wall++;
                {
                    float f=NativeTrainingLook.FacePaint;bool on=q.Z0>=p.MaxZ-f-eps&&q.Z1<=p.MaxZ+eps||q.Z0>=p.MinZ-eps&&q.Z1<=p.MinZ+f+eps||q.X0>=p.MinX-eps&&q.X1<=p.MinX+f+eps||q.X0>=p.MaxX-f-eps&&q.X1<=p.MaxX+eps;
                    Check(on,"wall paint on a hall wall's face: "+q.Tag);break;
                }
                case NativeTrainingMesh.Place.Face:face++;
                {
                    float f=NativeTrainingLook.FacePaint;
                    bool on=blocks.Any(b=>q.X0>=b.x0-f-eps&&q.X1<=b.x1+f+eps&&q.Y0>=b.y0-f-eps&&q.Y1<=b.y1+f+eps&&q.Z0>=b.z0-f-eps&&q.Z1<=b.z1+f+eps&&(q.X1<=b.x0+eps||q.X0>=b.x1-eps||q.Y1<=b.y0+eps||q.Y0>=b.y1-eps||q.Z1<=b.z0+eps||q.Z0>=b.z1-eps));
                    Check(on,"face paint on a block's face: "+q.Tag);break;
                }
                case NativeTrainingMesh.Place.Roof:roof++;
                {
                    Check(q.Y0>=NativeTrainingLook.RoofBand-eps&&q.Y1<=p.Roof+eps,"roof band: "+q.Tag);
                    foreach(var b in blocks)
                    {
                        bool overlap=q.X0<b.x1-eps&&q.X1>b.x0+eps&&q.Z0<b.z1-eps&&q.Z1>b.z0+eps;if(!overlap)continue;
                        Check(!(q.Y0<b.y1-eps&&q.Y1>b.y0+eps),"a fixture clear of "+b.id+": "+q.Tag);
                        // out of reach: nothing a pilot can stand on (tops up to the tower rails, 8.2 m) within 3.5 m below a fixture over it
                        if(b.y1<=8.5f&&b.y1<q.Y0)Check(q.Y0-b.y1>=3.5f,"out of reach over "+b.id+": "+q.Tag);
                    }
                    break;
                }
            }
        }
        Check(floor>1000&&wall>100&&face>50&&roof>500,"every place is used ("+floor+" floor, "+wall+" wall, "+face+" face, "+roof+" roof)");
        // the bay's static fittings are floor paint too
        var looks=NativeFactoryKit.Resolve(Array.Empty<MapLookBlock>());var fit=NativeTrainingBay.Fittings(map.hazards,looks);Check(fit.Count>20,"fittings");
        for(int i=0;i<fit.Count;i++){var f=fit.Items[i];Check(f.Y-f.Hy>=-1e-4&&f.Y+f.Hy<=NativeTrainingLook.FloorPaintTop,"a bay fitting stays on the floor");}
    }

    private static void Determinism(ContentMap map)
    {
        var a=NativeTrainingMarks.Build(map);var b=NativeTrainingMarks.Build(map);
        foreach(var (x,y,name) in new[]{(a.Paint,b.Paint,"paint"),(a.Fixtures,b.Fixtures,"fixtures")})
            Check(x.Positions.SequenceEqual(y.Positions)&&x.Normals.SequenceEqual(y.Normals)&&x.Colors.SequenceEqual(y.Colors)&&x.Emission.SequenceEqual(y.Emission)&&x.Indices.SequenceEqual(y.Indices)&&x.Indices.Length>0,name+" mesh deterministic");
        Check(a.Cells.SequenceEqual(b.Cells)&&a.Grids.SequenceEqual(b.Grids)&&a.Parts.Count==b.Parts.Count,"cells, grids and parts deterministic");
        Check(a.Paint.Indices.All(i=>i>=0&&i<a.Paint.Vertices)&&a.Fixtures.Indices.All(i=>i>=0&&i<a.Fixtures.Vertices)&&a.Paint.Indices.Length%3==0,"indices in range");
        Check(a.Paint.Positions.All(float.IsFinite)&&a.Fixtures.Positions.All(float.IsFinite),"finite vertices");
        // the strafe rail is drawn from rail.a to rail.b (with its stops just beyond)
        var strafe=map.trainingPads.Single(p=>p.kind=="strafe");var rail=a.Parts.Where(p=>p.Tag=="rail "+strafe.id).ToArray();
        float z0=Math.Min(strafe.rail.a.z,strafe.rail.b.z),z1=Math.Max(strafe.rail.a.z,strafe.rail.b.z);
        Check(rail.Length>10&&rail.Min(p=>p.Z0)<=z0-.3f&&rail.Max(p=>p.Z1)>=z1+.3f&&rail.Min(p=>p.Z0)>=z0-.8f&&rail.Max(p=>p.Z1)<=z1+.8f&&rail.All(p=>Math.Abs((p.X0+p.X1)/2-strafe.rail.a.x)<1.3f),"the rail spans rail.a to rail.b");
        // a moved pad moves its marks and its cell (placement follows the data)
        var moved=new ContentMap{id=map.id,look=map.look,bounds=map.bounds,blocks=map.blocks,hazards=map.hazards,spawns=map.spawns,
            trainingPads=map.trainingPads.Select(p=>new TrainingPad{id=p.id,kind=p.kind,pos=p.pos,yaw=p.yaw,distance=p.distance,rail=p.rail}).ToArray()};
        moved.trainingPads[0].pos=new V3(moved.trainingPads[0].pos.x+1,moved.trainingPads[0].pos.y,moved.trainingPads[0].pos.z);moved.trainingPads[0].distance+=1;
        var m=NativeTrainingMarks.Build(moved);Check(m.Parts.Where(p=>p.Tag=="pad "+map.trainingPads[0].id).Min(p=>p.X0)>a.Parts.Where(p=>p.Tag=="pad "+map.trainingPads[0].id).Min(p=>p.X0)+.9f,"marks follow the pad");
        Check(m.Plan.Distances.Contains(map.trainingPads[0].distance+1),"a new authored distance gets its board");
    }

    // ---- readouts ----
    private static string S(Func<char[],int> write){var b=new char[40];int n=write(b);return new string(b,0,n);}
    private static void Readouts(ContentMap map)
    {
        Check(S(b=>NativeTrainingReadoutText.Number(b,0,2125,b.Length))=="212.5"&&S(b=>NativeTrainingReadoutText.Number(b,0,0,b.Length))=="0.0"&&S(b=>NativeTrainingReadoutText.Number(b,0,99999,b.Length))=="9999.9","numbers to one decimal below 10,000");
        Check(S(b=>NativeTrainingReadoutText.Number(b,0,100000,b.Length))=="10,000"&&S(b=>NativeTrainingReadoutText.Number(b,0,123456789,b.Length))=="12,345,679"&&S(b=>NativeTrainingReadoutText.Number(b,0,1234565,b.Length))=="123,457","grouped whole numbers above");
        Check(S(b=>NativeTrainingReadoutText.Number(b,0,123456789,4))=="12,3".Substring(0,4)&&S(b=>NativeTrainingReadoutText.Number(b,3,55,4)).Length==4,"never past the cap");
        Check(S(b=>NativeTrainingReadoutText.Source(b,0,"rifle:Hit",b.Length))=="RIFLE"&&S(b=>NativeTrainingReadoutText.Source(b,0,"fire:BurnTick",b.Length))=="FIRE · BURN"&&S(b=>NativeTrainingReadoutText.Source(b,0,"greatsword:AbilityHit",b.Length))=="GREATSWORD · ABILITY","source labels");
        Check(S(b=>NativeTrainingReadoutText.Source(b,0,"environment:grid",b.Length))=="HAZARD · GRID"&&S(b=>NativeTrainingReadoutText.Source(b,0,"environment:hazard",b.Length))=="HAZARD"&&S(b=>NativeTrainingReadoutText.Source(b,0,null,b.Length))=="-"&&S(b=>NativeTrainingReadoutText.Source(b,0,"machinegun:Hit",6))=="MACHIN","hazard and empty sources, cut at the cap");
        // the key: tenths, only drawn values
        var r=new TrainingReadout{pad="p",actor=1000,total=34.04f,last=34.04f,dps=6.8f,distance=10.31f,source="rifle:Hit"};var k=NativeTrainingReadoutKey.Of(r);
        Check(k.Any&&k.Total==340&&k.Distance==103&&k.Same(NativeTrainingReadoutKey.Of(r)),"key tenths");
        r.total=34.06f;Check(!k.Same(NativeTrainingReadoutKey.Of(r)),"a drawn change repaints");r.total=34.04f;r.at=99;Check(k.Same(NativeTrainingReadoutKey.Of(r)),"the hit time alone does not repaint");
        r.source="pistol:Hit";Check(!k.Same(NativeTrainingReadoutKey.Of(r)),"a new source repaints");
        Check(!NativeTrainingReadoutKey.Of(null).Any&&!NativeTrainingReadoutKey.Of(new TrainingReadout{total=float.NaN}).Any,"no row or a bad value reads as no hits");
        // a meter's rows: each hit and the DPS decay change the key; the same moment does not
        var plan=NativeTrainingPlan.Of(map);var meter=new TrainingDamageMeter(map.trainingPads[0],NativeTrainingRules.FirstDummy);var last=NativeTrainingReadoutKey.Of(meter.Read(0));Check(!last.Any,"a fresh target reads no hits");
        meter.Hit(1,30,true,1,"rifle:Hit",10.2f);var k1=NativeTrainingReadoutKey.Of(meter.Read(1));Check(!k1.Same(last)&&k1.Head&&k1.Dps==60,"a hit repaints");
        Check(k1.Same(NativeTrainingReadoutKey.Of(meter.Read(1))),"the same moment does not");Check(!k1.Same(NativeTrainingReadoutKey.Of(meter.Read(6.5f))),"the window's decay repaints");
        // host and guest: the replicated copy (JSON round trip, as the frame codec carries it) paints the same pixels
        var host=new NativeTrainingReadoutRaster();var guest=new NativeTrainingReadoutRaster();var o=new JsonSerializerOptions{IncludeFields=true};
        meter.Hit(2,12.5f,false,1,"machinegun:Hit",10.4f);var row=meter.Read(2.5f);var copy=JsonSerializer.Deserialize<TrainingReadout>(JsonSerializer.Serialize(row,o),o);
        var info=plan.Pads[0];host.Paint(info.Header,info.Tag,NativeTrainingReadoutKey.Of(row),info.Accent);guest.Paint(info.Header,info.Tag,NativeTrainingReadoutKey.Of(copy),info.Accent);
        Check(host.Pixels.SequenceEqual(guest.Pixels),"guest pixels match the host's");
        // what the cell shows: the header band, the headshot badge only on a headshot, the plate's alpha
        int Px(NativeTrainingReadoutRaster x,int px,int py,int ch)=>x.Pixels[((NativeTrainingReadoutRaster.Height-1-py)*NativeTrainingReadoutRaster.Width+px)*4+ch];
        var a=new NativeTrainingReadoutRaster();a.Paint(info.Header,info.Tag,new NativeTrainingReadoutKey{Any=true,Total=2125,Last=340,Dps=425,Distance=103,Head=true,Source="rifle:Hit"},info.Accent);
        var b2=new NativeTrainingReadoutRaster();b2.Paint(info.Header,info.Tag,new NativeTrainingReadoutKey{Any=true,Total=2125,Last=340,Dps=425,Distance=103,Head=false,Source="rifle:Hit"},info.Accent);
        int bx=NativeTrainingReadoutRaster.Width-80,by=100;Check(Px(a,bx,by,0)==(NativeTrainingLook.Danger>>16&255)&&Px(b2,bx,by,0)!=(NativeTrainingLook.Danger>>16&255),"the HEAD badge only on a headshot");
        Check(Px(a,1,1,3)==255&&Px(a,200,40,3)==232&&Px(a,150,13,2)==(NativeTrainingLook.Indigo&255),"frame, translucent plate, header band");
        var none=new NativeTrainingReadoutRaster();none.Paint(info.Header,info.Tag,default,info.Accent);Check(!none.Pixels.SequenceEqual(a.Pixels),"no hits reads differently");
        // tags
        Check(NativeTrainingTags.Alpha(true,0)==1&&NativeTrainingTags.Alpha(true,NativeTrainingTags.TagHold+NativeTrainingTags.TagFade/2)==.5f&&NativeTrainingTags.Alpha(true,NativeTrainingTags.TagHold+NativeTrainingTags.TagFade)==0&&NativeTrainingTags.Alpha(false,0)==0&&NativeTrainingTags.Alpha(true,float.NaN)==0,"tag hold and fade");
        Check(NativeTrainingTags.Height(1,65)==NativeTrainingTags.TagMin&&NativeTrainingTags.Height(500,65)==NativeTrainingTags.TagMax&&NativeTrainingTags.Height(20,30)<NativeTrainingTags.Height(20,65),"tag size: a share of the view, clamped");
        Check(NativeTrainingTags.Scale(0)>1&&NativeTrainingTags.Scale(1)==1&&NativeTrainingTags.Flash(0)==1&&NativeTrainingTags.Flash(1)==0,"pop and flash");
        var slots=new int[NativeTrainingTags.MaxTags];var times=new float[slots.Length];for(int i=0;i<slots.Length;i++)slots[i]=-1;
        for(int i=0;i<10;i++)NativeTrainingTags.Keep(slots,times,i,(i*7)%10);Check(slots.OrderBy(x=>x).SequenceEqual(new[]{1,4,7})||slots.Select(s=>(s*7)%10).OrderBy(x=>x).SequenceEqual(new[]{7,8,9}),"the newest hits keep the tags");
    }

    // ---- the bay ----
    private static void Bay(ContentMap map)
    {
        var looks=NativeFactoryKit.Resolve(new[]{new MapLookBlock{block="factory-kit-blade",look="plain",color=0xC9CED2,note=""},new MapLookBlock{block="factory-kit-hub",look="plain",color=0xC9CED2,note=""}});var bay=new NativeTrainingBay(looks);int blade0=looks.Look[NativeFactoryKit.Blade],blade1=looks.Color[NativeFactoryKit.Blade];
        foreach(var h in map.hazards)Check(NativeTrainingBay.Drawn(h.kind),"the bay draws "+h.kind);
        int maxParts=0,maxQuads=0;bool blade=false,fell=false,live=false;
        for(double t=0;t<60;t+=.05)
        {
            bay.Build(map.hazards,0,t,NativePhase.Fight,0,t+NativeFactoryHazards.ArmDelay,-22,14);bay.Effects(map.hazards,0,t,NativePhase.Fight,0,t+NativeFactoryHazards.ArmDelay,-30,2,10,1,0,0,0,1,0,1);
            maxParts=Math.Max(maxParts,bay.Parts.Count);maxQuads=Math.Max(maxQuads,bay.Quads.Count);
            for(int i=0;i<bay.Parts.Count;i++){var p=bay.Parts.Items[i];bool slot=p.Look==blade0&&p.Color==blade1;float vy=Math.Abs(2*(p.Qx*p.Qy+p.Qw*p.Qz))*p.Hx+Math.Abs(1-2*(p.Qx*p.Qx+p.Qz*p.Qz))*p.Hy+Math.Abs(2*(p.Qy*p.Qz-p.Qw*p.Qx))*p.Hz;Check(p.Y+vy<=map.bounds.ceiling+.01f&&(slot||p.Y-vy>=-.03f)&&float.IsFinite(p.X),"rig parts between the floor and the roof (a blade rises from under its slot; a sling wraps a landed load 2 cm under it) at "+t.ToString("0.00"));if(slot&&p.Y>-.4f)blade=true;}
            foreach(var h in map.hazards)
            {
                if(h.kind=="crane"){var s=NativeFactoryHazards.State(h,0,t);if(s.Phase==NativeFactoryHazards.Falling)fell=true;}
                if(h.kind=="grid"&&NativeCyberCityHazards.At(h,NativePhase.Fight,0,0,t+NativeFactoryHazards.ArmDelay).Phase==NativeHazardPhase.Active)live=true;
            }
        }
        Check(maxParts<NativeTrainingBay.Capacity&&maxQuads<NativeTrainingBay.FxCapacity,"capacities hold ("+maxParts+" parts, "+maxQuads+" quads at most)");
        Check(blade&&fell&&live,"the saw rises, the crane drops and the grid goes live within a minute");
        // the strafing carriage sits where it is told, flat on the floor
        bay.Build(Array.Empty<MapHazard>(),0,0,NativePhase.Fight,0,0,-22,15.5);Check(bay.Parts.Count>0&&Math.Abs(bay.Parts.Items[0].X+22)<1e-4&&Math.Abs(bay.Parts.Items[0].Z-15.5f)<1e-4&&bay.Parts.Items[0].Y+bay.Parts.Items[0].Hy<=.031f,"the carriage under the strafe target");
        bay.Build(map.hazards,0,double.NegativeInfinity,NativePhase.Countdown,0,0);Check(bay.Parts.Count>0,"parked samples still draw before the fight");
    }

    private static void Allocation(ContentMap map)
    {
        var looks=NativeFactoryKit.Resolve(Array.Empty<MapLookBlock>());var bay=new NativeTrainingBay(looks);var raster=new NativeTrainingReadoutRaster();var plan=NativeTrainingPlan.Of(map);
        var row=new TrainingReadout{pad=plan.Pads[0].Id,actor=1000,total=212.5f,last=34,headshot=true,dps=42.5f,distance=10.3f,source="rifle:Hit",at=1};var slots=new int[NativeTrainingTags.MaxTags];var times=new float[slots.Length];
        for(int i=0;i<3;i++){bay.Build(map.hazards,0,i,NativePhase.Fight,0,i,-22,14);bay.Effects(map.hazards,0,i,NativePhase.Fight,0,i,-30,2,10,1,0,0,0,1,0,1);raster.Paint(plan.Pads[0].Header,plan.Pads[0].Tag,NativeTrainingReadoutKey.Of(row),plan.Pads[0].Accent);}
        long before=GC.GetAllocatedBytesForCurrentThread();float sink=0;
        for(int i=0;i<240;i++)
        {
            double t=i*.25;bay.Build(map.hazards,0,t,NativePhase.Fight,0,t,-22,13+(i%7));bay.Effects(map.hazards,0,t,NativePhase.Fight,0,t,-30,2,10,1,0,0,0,1,0,1);
            row.total=i;var k=NativeTrainingReadoutKey.Of(row);if(!k.Same(NativeTrainingReadoutKey.Of(row)))sink++;if(i%8==0)raster.Paint(plan.Pads[i%plan.Pads.Length].Header,plan.Pads[i%plan.Pads.Length].Tag,k,plan.Pads[0].Accent);
            for(int s=0;s<slots.Length;s++)slots[s]=-1;for(int p=0;p<28;p++)NativeTrainingTags.Keep(slots,times,p,(p*13+i)%17);
            sink+=NativeTrainingTags.Alpha(true,(float)t)+NativeTrainingTags.Height(20+i,65)+NativeTrainingTags.Scale(.1f)+NativeTrainingTags.Flash(.2f);
        }
        long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0&&sink>0,"bay, key, painter and tags allocate nothing per frame ("+(after-before)+" bytes)");
    }

    // ---- the look ----
    private static void Look(ContentMap map,MapLookSet set)
    {
        var look=NativeMapLooks.Find(set,"training");Check(look!=null&&look.look==NativeTrainingLook.Look&&map.look==NativeTrainingLook.Look&&ReferenceEquals(NativeMapLooks.ByLook(set,map.look),look),"the map's look is training-studio");
        Check(look.sky.variant=="none"&&look.key.shadows=="none"&&look.rim.shadows=="none"&&look.blocks.dressed&&look.toon!=null&&look.toon.on&&look.toon.skyToon==0&&look.advanced==null,"a sealed studio: no sky, no shadows (the roof would shade it all), dressed, the anime style");
        Check(look.key.intensity>=.8f&&look.ambient.skyScale>=.5f&&look.fog.start>=60&&look.lights.Length<=NativeMapLooks.MaxLights,"bright, even and clear");
        int Grey(int c)=>Math.Max(Math.Abs((c>>16&255)-(c>>8&255)),Math.Abs((c>>8&255)-(c&255)));Check(Grey(look.key.color)<=12&&Grey(look.ambient.sky)<=12&&Grey(look.blockLooks.Single(b=>b.block=="training-floor").color)<=20,"neutral light and floor");
        foreach(var b in look.blockLooks)Check(b.block.StartsWith("material:",StringComparison.Ordinal)?map.blocks.Any(x=>"material:"+x.material==b.block):map.blocks.Any(x=>Regex.IsMatch(x.id,"^"+Regex.Escape(b.block).Replace("\\*",".*")+"$")),"block look "+b.block+" matches a training block");
        foreach(int kind in new[]{NativeFactoryKit.Blade,NativeFactoryKit.Hub,NativeFactoryKit.Lamp,NativeFactoryKit.Void,NativeFactoryKit.Plate,NativeFactoryKit.Stripe,NativeFactoryKit.Danger,NativeFactoryKit.Grate,NativeFactoryKit.Ram,NativeFactoryKit.Chrome,NativeFactoryKit.CraneBody,NativeFactoryKit.CraneDark,NativeFactoryKit.Cable,NativeFactoryKit.Hook,NativeFactoryKit.Load,NativeFactoryKit.Strap,NativeFactoryKit.PaintZone,NativeFactoryKit.Motor,NativeFactoryKit.Wheel})
            Check(look.kitLooks.Any(k=>k.block==NativeFactoryKit.Kinds[kind]),"kit look "+NativeFactoryKit.Kinds[kind]);
        // every bay part kind the rigs and fittings emit is listed (none falls back to plain white)
        var looks=NativeFactoryKit.Resolve(look.kitLooks);var bay=new NativeTrainingBay(looks);var used=new HashSet<int>();var fit=NativeTrainingBay.Fittings(map.hazards,looks);for(int i=0;i<fit.Count;i++)used.Add(fit.Items[i].Look*1000+fit.Items[i].Flags);
        for(double t=0;t<40;t+=.5){bay.Build(map.hazards,0,t,NativePhase.Fight,0,t,-22,14);for(int i=0;i<bay.Parts.Count;i++)Check(bay.Parts.Items[i].Color!=0xffffff||bay.Parts.Items[i].Look!=0,"no unlisted kit kind");}
        Check(set.maps.Count(m=>m.look==NativeTrainingLook.Look)==1,"one training-studio look");
    }

    // ---- the runtime wiring (text) ----
    private static void Wiring(string root,UnityMapContent maps)
    {
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));string rt="Assets/ChipArena/Runtime/";
        foreach(var m in maps.maps)Check(NativeTrainingLook.Active(m.id)==(m.id=="training"),"active on the training map only: "+m.id);
        var source=R(rt+"NativeSourcePresentation.cs");int sw=source.IndexOf("private void SourceWorld(",StringComparison.Ordinal),end=source.IndexOf("HazardKitWorld(arena);",sw,StringComparison.Ordinal);
        Check(sw>0&&end>sw&&source.Substring(sw,end-sw).Contains("TrainingScenery(arena);"),"SourceWorld draws the room (the arena and the killcam replay)");
        Check(R(rt+"NativeStagePrewarm.cs").Contains("if(TrainingPrepare(arena))built++;"),"the prewarm builds it behind the loading screen");
        var pres=R(rt+"NativeTrainingPresentation.cs");
        Check(pres.Contains("if(!NativeTrainingLook.Active(arena.MapId))return;if(trainingStage==null)trainingStage=gameObject.AddComponent<NativeTrainingStage>();")&&pres.Contains("if(!NativeTrainingLook.Active(arena.MapId))return false;"),"nothing is created or drawn off the training map");
        Check(pres.Contains("arena.Training?arena.TrainingReadouts():")&&pres.Contains("Time.unscaledTime>=nextPoll")&&pres.Contains("nextPoll=Time.unscaledTime+1/PollHz")&&pres.Contains("if(!painted[i]||!key.Same(keys[i]))Repaint(i,key);"),"polled at PollHz, repainted only on a drawn change");
        Check(pres.Contains("r.pad==marks.Plan.Pads[pad].Id")&&R(rt+"NativeTrainingRoom.cs").Contains("Replica?TrainingRemote:"),"rows matched by actor and pad id; guests read the replicated rows");
        string Body(string from,string to){int i=pres.IndexOf(from,StringComparison.Ordinal),j=pres.IndexOf(to,i+1,StringComparison.Ordinal);Check(i>0&&j>i,"has "+from);return pres.Substring(i,j-i);}
        foreach(var body in new[]{Body("public void Render(","private Vector3 Anchor("),Body("private void Readouts(","private void Repaint("),Body("private void Fill(","private static void Put("),Body("private void Draw(Batch","private static Mesh KitMesh(")})
        {
            foreach(Match m in Regex.Matches(body,@"new\s+([A-Za-z_][A-Za-z0-9_.]*)"))Check(new[]{"Vector3","Vector4","Color","Rect"}.Contains(m.Groups[1].Value),"per-frame path: no reference allocation ("+m.Value+")");
            Check(!Regex.IsMatch(body,@"\.(Select|Where|ToArray|ToList|OrderBy|Any|First)\(")&&!Regex.IsMatch(body,"\"\\s*\\+|\\+\\s*\"")&&!body.Contains("$\""),"per-frame path: no LINQ and no string building");
        }
        var shader=R("Assets/ChipArena/Resources/NativeTrainingStudio.shader");
        Check(shader.Contains("Shader \"ChipArena/NativeTrainingStudio\"")&&shader.Contains("_Mode<.5")&&shader.Contains("_Mode<1.5")&&shader.Contains("tex2Dgrad")&&shader.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,shade,fixed4(1,1,1,1))"),"studio shader: grid, screen and tag modes, mip-correct sampling, the grid fogs to no change");
        var reserved=new[]{"line","point","triangle","sample","linear","texture","vector","matrix","pass","compile","string","precise","centroid","shared","sampler","register","lineadj","triangleadj","nointerpolation","noperspective","snorm","unorm","uniform","in","out","inout","static","const"};
        foreach(Match m in Regex.Matches(shader,@"\b(?:float[234]?|half[234]?|fixed[234]?|int[234]?|bool|sampler2D)\s+([A-Za-z_][A-Za-z0-9_]*)"))Check(!reserved.Contains(m.Groups[1].Value),"shader name not reserved: "+m.Groups[1].Value);
        var guids=new Dictionary<string,string>();
        foreach(var f in Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories)){var g=Regex.Match(File.ReadAllText(f),@"guid:\s*([0-9a-f]{32})");if(g.Success){Check(!guids.ContainsKey(g.Groups[1].Value)||!(f.Contains("NativeTraining")||guids[g.Groups[1].Value].Contains("NativeTraining")),"fresh guid "+Path.GetFileName(f));guids[g.Groups[1].Value]=f;}}
        foreach(var f in new[]{"Core/NativeTrainingLook.cs","Core/NativeTrainingReadouts.cs","Core/NativeTrainingBay.cs","Runtime/NativeTrainingPresentation.cs","Resources/NativeTrainingStudio.shader"})Check(File.Exists(Path.Combine(root,"Assets/ChipArena",f+".meta")),"meta for "+f);
    }
}
