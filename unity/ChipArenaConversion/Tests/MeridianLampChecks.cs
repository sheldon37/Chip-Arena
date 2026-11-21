using System;
using System.Collections.Generic;
using System.Linq;
using ChipArena.UnityConversion;

// Meridian's street lamps on the surfaces round them (owner, 2026-11-20, Crown Hill at night: "the light doesn't seem to be on the
// material properly? (Like this seems to be flat, in the air light)"; Core NativeCityLamps, NativeMeridianDressLight; Runtime
// NativeCityLampLight; Resources/NativeCityLamps.cginc). Every street plan's live deal dressed as the live run dresses it, its lamps' light
// as the city's lamp grid holds it, and the formula ChipArena/StageLit runs on it:
//  - the grid lists every lamp in every cell its reach touches (none dropped, the cells' light the light of every lamp);
//  - every working swan-neck lights the wall under its bracket (1 m under it to at least WallNear, 2 m to at least WallFar, of linear
//    luminance before the surface's own colour), every flush lamp the wall round its lens (to FlushWall: shared's slot, at its lens,
//    gave its own wall 0.12), and every lamp the floor of its pool where its reach gets there (to Floor);
//  - shared's eight slots lit only the lamps nearest the eye (fewer round the camp); now every lamp near the owner's view lights;
//  - a flickering lamp changes at most three times a second, and with Reduced effects only breathes, never under 0.6;
//  - the shader, the drawer and the upload as pinned below (the slot marked so a surface never takes a lamp twice, the grid off on
//    every other map and while a stage or intro owns the light, the beams only with the depth texture, the glints only on level
//    floors, no flat pool), and the per-frame packing allocates nothing.
internal static class MeridianLampChecks
{
    private static readonly System.Globalization.CultureInfo Inv=System.Globalization.CultureInfo.InvariantCulture;
    private static string F(V3 v)=>string.Format(Inv,"({0:F2},{1:F2},{2:F2})",v.x,v.y,v.z);
    private static float Luma(V3 c)=>c.x*.2126f+c.y*.7152f+c.z*.0722f;
    /// <summary>The stated levels (linear luminance of the lamp's light on the surface, before its colour and the anime steps).</summary>
    public const float WallNear=.6f,WallFar=.35f,FlushWall=.2f,Floor=.25f;
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Meridian lamps: "+what);}

    public static void Run(string root)
    {
        count=0;var report=new List<string>();
        Check(NativeCityLamps.Falloff(0,10)==1&&NativeCityLamps.Falloff(10,10)==0&&Math.Abs(NativeCityLamps.Falloff(5,10)-.5625f)<1e-6f,"the fall-off is the slots' (1 - (d/r)^2)^2");
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var plan=HubZFightChecks.Dress(live,null,false);string name=NativeCityLayout.Plans[live.Plan].Name;
            var lights=NativeMeridianDress.LightsOf(plan);var table=NativeCityLamps.Build(lights);
            Check(table.Dropped==0&&table.Fullest<=NativeCityLamps.PerCell,name+": the grid lists every lamp in every cell its reach touches (fullest "+table.Fullest+", dropped "+table.Dropped+")");
            Check(HubZFightChecks.Dress(live,null,false).Digest()==plan.Digest()&&NativeCityLamps.Build(NativeMeridianDress.LightsOf(HubZFightChecks.Dress(live,null,false))).Cells.SequenceEqual(table.Cells),name+": the lamps and their grid are deterministic");
            int walls=0,flushes=0,floors=0,unreached=0;float minNear=9,minFar=9,minFlush=9,minFloor=9;
            for(int i=0;i<plan.Lamps.Count;i++)
            {
                var l=plan.Lamps[i];var g=lights[i];
                if(l.State==NativeMeridianDress.Dead){Check(g.Range==0,name+": dead lamp "+i+" lights nothing");continue;}
                Check(g.Range>0&&g.R>0&&Math.Abs(g.Range-l.Radius*NativeMeridianDress.LampReach)<1e-4f,name+": lamp "+i+" has its light");
                var n=WallNormal(l);
                if(l.Kind==NativeMeridianDress.Swan)
                {
                    var a=l.Mount-new V3(0,1,0)+n*.002f;var b=l.Mount-new V3(0,2,0)+n*.002f;float la=Luma(table.Light(a,n)),lb=Luma(table.Light(b,n));
                    Check(la>=WallNear&&lb>=WallFar,name+FormattableString.Invariant($": swan-neck {i} at {F(l.Mount)} lights the wall under its bracket ({la:F2} at 1 m, {lb:F2} at 2 m)"));
                    Check(Near(table.Light(a,n),table.LightAll(a,n))&&Near(table.Light(b,n),table.LightAll(b,n)),name+": the grid gives the wall under swan-neck "+i+" every lamp's light");
                    minNear=Math.Min(minNear,la);minFar=Math.Min(minFar,lb);walls++;
                }
                else if(l.Kind==NativeMeridianDress.Flush)
                {
                    var a=l.Mount-new V3(0,1,0)+n*.002f;float la=Luma(table.Light(a,n));
                    Check(la>=FlushWall&&Luma(OldSlot(l,a,n))<FlushWall,name+FormattableString.Invariant($": flush lamp {i} lights the wall under its lens ({la:F2}; shared's slot {Luma(OldSlot(l,a,n)):F2})"));
                    minFlush=Math.Min(minFlush,la);flushes++;
                }
                var p=l.Pool+new V3(0,.01f,0);
                if((g.At-p).Length>g.Range*.7f){unreached++;continue;}
                float lf=Luma(table.Light(p,new V3(0,1,0)));
                Check(lf>=Floor&&Near(table.Light(p,new V3(0,1,0)),table.LightAll(p,new V3(0,1,0))),name+FormattableString.Invariant($": lamp {i} lights its pool's floor ({lf:F2})"));
                minFloor=Math.Min(minFloor,lf);floors++;
            }
            report.Add(FormattableString.Invariant($"{name}: {plan.Lamps.Count} lamps; walls under {walls} swan-necks at least {minNear:F2} (1 m) and {minFar:F2} (2 m), round {flushes} flush lenses {minFlush:F2}, {floors} pools' floors {minFloor:F2} ({unreached} pools out of their lamp's reach); grid {table.Nx} x {table.Nz}, fullest cell {table.Fullest}"));
        }
        // the whole city's grid as the live run fills it (the bleed drawn): the dress's lamps, the street life's lit pieces, the bleed's
        // lamps and the camp's lights, as their drawers add them: no cell over its PerCell
        foreach(var live in HubZFightChecks.Worlds(root))
        {
            var lamps=CityTable(live,out int dress,out int street,out int bleed,out int camp);var table=NativeCityLamps.Build(lamps);string name=NativeCityLayout.Plans[live.Plan].Name;
            Check(table.Dropped==0&&lamps.Count<=NativeCityLamps.Capacity,name+": the whole city's grid lists every light in every cell it reaches (fullest "+table.Fullest+", dropped "+table.Dropped+")");
            report.Add(FormattableString.Invariant($"{name}, the whole city: {lamps.Count} lights ({dress} street lamps, {street} street life, {bleed} bleed, {camp} camp), the fullest cell {table.Fullest} of {NativeCityLamps.PerCell}"));
        }
        // the owner's view: Crown Hill's sunken west street from its foot (xseed 2027808452, the capture's), the eye 2.4 m over it looking up
        // it; shared lit only the eight lamps its slots held (five once the camp's three kit lights took the weakest), the grid lights all
        {
            var live=HubZFightChecks.Worlds(root).First(l=>l.Plan==0);var plan=HubZFightChecks.Dress(live,null,false);var lights=NativeMeridianDress.LightsOf(plan);var table=NativeCityLamps.Build(lights);
            var e=new V3(-92,-4.6f,.3f);var fwd=new V3(1,0,0);int inView=0,lit=0;
            for(int i=0;i<plan.Lamps.Count;i++)
            {
                var l=plan.Lamps[i];if(l.State==NativeMeridianDress.Dead)continue;var d=l.Bowl-e;if(d.Length>60||V3.Dot(d,fwd)<0)continue;inView++;
                var n=WallNormal(l);var a=(l.Kind==NativeMeridianDress.Pendant?l.Pool:l.Mount-new V3(0,1,0))+n*.002f;if(Luma(table.Light(a,n))>=FlushWall)lit++;
            }
            Check(lit==inView&&inView>8,"every lamp ahead in the owner's view lights its wall ("+lit+" of "+inView+"; shared's slots held 8, 5 beside the camp)");
            // the cause, at the owner's third-person view (the camera 4.5 m behind a pilot at the street's foot, looking up it): shared's
            // drawer gave its eight slots to the lamps nearest the camera in 3D (three of them up on the terraces, unseen from the sunken
            // street), then the camp's three kit lights (70 m off) took the slots NativeKitLightPool.Merge judged worth least at the eye:
            // the swan-neck on the left wall 22 m ahead among them. The drawer still drew that lamp's beam (from its own pick) and its pool,
            // so its cone hung in the air over a dark wall. Now its wall takes its light from the grid.
            var cam=new V3(-94.5f,-5.2f,0);var look=new V3(1,-.2f,0);look=look/look.Length;int swan=-1;
            for(int i=0;i<plan.Lamps.Count;i++){var l=plan.Lamps[i];if(l.Kind==NativeMeridianDress.Swan&&Math.Abs(l.Bowl.x+72.8f)<.1f&&l.Bowl.z>0)swan=i;}
            Check(swan>=0,"Crown Hill's live deal hangs a swan-neck on the west street's north wall at x -72.8");
            var pick=SharedPick(plan,cam,look,NativeMapLooks.MaxLights);var pos=new float[NativeMapLooks.MaxLights*4];var rgb=new float[NativeMapLooks.MaxLights*3];
            for(int s=0;s<pick.Length;s++){var l=plan.Lamps[pick[s]];var c=(l.Protected?NativeMeridianDress.RouteSodium:NativeMeridianDress.Sodium)*1.35f;pos[s*4]=l.Bowl.x;pos[s*4+1]=l.Bowl.y-.2f;pos[s*4+2]=l.Bowl.z;pos[s*4+3]=l.Radius*1.25f;rgb[s*3]=c.x;rgb[s*3+1]=c.y;rgb[s*3+2]=c.z;}
            var fx=new NativeCampFx(NativeCamp.Live(live.World.Blueprint.worldSeed));fx.Build(30,cam.x,cam.y,cam.z,0,0,-1,0,1,0,null,1,false,false,0);
            NativeKitLightPool.Merge(pos,rgb,NativeMapLooks.MaxLights,fx.LightPos,fx.LightRgb,fx.LightCount,cam.x,cam.y,cam.z);
            var swanAt=plan.Lamps[swan].Bowl-new V3(0,.2f,0);bool slotted=false;for(int s=0;s<NativeMapLooks.MaxLights;s++)if(Math.Abs(pos[s*4]-swanAt.x)<.01f&&Math.Abs(pos[s*4+2]-swanAt.z)<.01f)slotted=true;
            var sw=plan.Lamps[swan];var wn=WallNormal(sw);var wallPt=sw.Mount-new V3(0,1,0)+wn*.002f;
            Check(Array.IndexOf(pick,swan)>=0&&fx.LightCount==3&&!slotted,"shared's view: the swan-neck ahead was in the drawer's pick (its beam drew) but the camp's kit lights took its slot (its wall unlit)");
            Check(Luma(table.Light(wallPt,wn))>=WallNear,"now the grid lights that swan-neck's wall ("+Luma(table.Light(wallPt,wn)).ToString("F2",Inv)+")");
            int terraces=0;foreach(int i in pick)if(plan.Lamps[i].Pool.y>-1)terraces++;
            report.Add("Crown Hill's west street from its foot: "+lit+" of the "+inView+" lamps within 60 m ahead light their wall; shared's eight slots there: "+terraces+" lamps up on the terraces (unseen), then the camp's 3 kit lights took the weakest, the swan-neck 22 m ahead on the left wall among them (its beam drawn, its wall dark)");
        }
        // flicker: at most three changes a second; Reduced: a breath, never under ReducedFloor
        {
            var l=new NativeMeridianDress.Lamp{State=NativeMeridianDress.Flicker};int worst=0;float low=1;double slope=0;
            for(int s=0;s<64;s++)
            {
                l.Seed=s/64f;float prev=NativeMeridianDress.LampLevel(l,0,false);
                for(int sec=0;sec<30;sec++){int changes=0;for(int k=1;k<=240;k++){float t=sec+k/240f,v=NativeMeridianDress.LampLevel(l,t,false);if(v!=prev)changes++;prev=v;}worst=Math.Max(worst,changes);}
                float pr=NativeMeridianDress.LampLevel(l,0,true);for(int k=1;k<=240*30;k++){float v=NativeMeridianDress.LampLevel(l,k/240f,true);low=Math.Min(low,v);slope=Math.Max(slope,Math.Abs(v-pr)*240);pr=v;}
            }
            Check(worst<=3,"a flickering lamp changes at most three times a second ("+worst+")");
            Check(low>=NativeMeridianDress.ReducedFloor-1e-4f&&slope<.5,FormattableString.Invariant($"with Reduced effects a flickering lamp only breathes (never under {low:F2}, at most {slope:F2} a second)"));
            Check(NativeMeridianDress.LampLevel(new NativeMeridianDress.Lamp{State=NativeMeridianDress.Dead},1,false)==0&&NativeMeridianDress.LampLevel(new NativeMeridianDress.Lamp{State=NativeMeridianDress.Working},1,true)==1,"dead lamps are dark, working lamps steady");
        }
        // the per-frame packing allocates nothing
        {
            var live=HubZFightChecks.Worlds(root).First(l=>l.Plan==0);var table=NativeCityLamps.Build(NativeMeridianDress.LightsOf(HubZFightChecks.Dress(live,null,false)));
            var level=new float[table.Lamps.Length];var pix=new float[level.Length*4];table.PackLight(level,pix);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<200;f++){for(int i=0;i<level.Length;i++)level[i]=(f+i)%3*.5f;table.PackLight(level,pix);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the per-frame light packing allocates nothing");
        }
        Pins(root);
        foreach(var line in report)Console.WriteLine("  "+line);
        Console.WriteLine("PASS meridian lamps: "+count+" checks over the five plans' live deals: every lamp lights the wall under it and its pool's floor through the city's lamp grid, none dropped, flicker under 3 Hz");
    }
    private static bool Near(V3 a,V3 b)=>Math.Abs(a.x-b.x)<1e-4f&&Math.Abs(a.y-b.y)<1e-4f&&Math.Abs(a.z-b.z)<1e-4f;
    private static float Lin(uint hex,int shift){double v=((hex>>shift)&255)/255.0;return (float)(v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4));}
    /// <summary>A live deal's lights as its drawers add them to the grid (Runtime NativeMeridianDressDraw, NativeMeridianStreetLifeDraw,
    /// NativeCityBleedDraw, NativeCampDraw), the bleed drawn.</summary>
    public static List<NativeCityLamps.Lamp> CityTable(HubZFightChecks.Live live,out int dress,out int street,out int bleed,out int camp)
    {
        var world=live.World;var city=world.City;var map=city.Map;uint seed=world.Blueprint.worldSeed;
        var plan=HubZFightChecks.Dress(live,null,true);var lamps=new List<NativeCityLamps.Lamp>(NativeMeridianDress.LightsOf(plan));dress=lamps.Count;
        var all=Enumerable.Range(0,map.blocks.Length).ToList();
        var bleedPlan=NativeCityBleedDress.ForWalk(city,world.Bleed,seed);
        var life=NativeMeridianStreetLife.Build(NativeMeridianStreetLife.InputOf(city,NativeCityLayout.Plans[live.Plan].Seed,seed,world.HallSlots,plan,world.Bleed,bleedPlan));
        street=0;
        foreach(var p in life.Pieces)
        {
            var k=NativeMeridianStreetLife.Kinds[p.Kind];if(!k.Lit||p.Life==NativeMeridianStreetLife.Life.Dead||p.Life==NativeMeridianStreetLife.Life.Spent)continue;
            var h=NativeMeridianStreetLife.Local(p,k.Head);float floor=NativeCityBleedDress.FloorBelow(map.blocks,all,new V3(h.x,h.y-.02f,h.z));if(floor<-1e8f)floor=p.At.y;
            var pc=k.Glow==NativeMeridianStreetLife.Glow.Flood?NativeMeridianStreetLife.Local(p,new V3(0,0,k.Pool*.55f)):h;
            lamps.Add(NativeMeridianStreetLife.CityLight(k,h,new V3(pc.x,floor+.04f,pc.z),Lin(k.Light,16),Lin(k.Light,8),Lin(k.Light,0)));street++;
        }
        bleed=0;
        foreach(var p in bleedPlan.Pieces)
        {
            var K=NativeCityBleedDress.Kinds[p.Kind];if(!K.Lamp||p.Lamp==NativeCityBleedDress.LampState.Dead)continue;var head=NativeCityBleedDress.Local(p,K.Head);
            float floor=NativeCityBleedDress.FloorBelow(map.blocks,all,new V3(head.x,head.y-.05f,head.z));if(floor<-1e8f)floor=head.y-4.6f;bool ceiling=K.Key.StartsWith("level0.ceiling",StringComparison.Ordinal);
            float s=(ceiling?.8f:1.2f)/NativeCityLamps.DownFloor;uint c=p.Light!=0?p.Light:0xFFFFFF;lamps.Add(NativeCityLamps.Overhead(head,Math.Max(5f,Math.Min(10f,(head.y-floor)*1.8f)),Lin(c,16)*s,Lin(c,8)*s,Lin(c,0)*s));bleed++;
        }
        var campLights=new NativeCampFx(NativeCamp.Live(seed)).CityLamps();lamps.AddRange(campLights);camp=campLights.Length;
        return lamps;
    }
    private static void Pins(string root)
    {
        string R(string f)=>System.IO.File.ReadAllText(System.IO.Path.Combine(root,f));
        var inc=R("Assets/ChipArena/Resources/NativeCityLamps.cginc");var stage=R("Assets/ChipArena/Resources/NativeStageLit.shader");
        Check(Math.Abs(NativeCityLamps.SpotOf(1,2.5f,NativeCityLamps.SpotFloor)-1)<1e-6f&&Math.Abs(NativeCityLamps.SpotOf(0,2.5f,.45f)-.45f)<1e-6f&&NativeCityLamps.SpotOf(-1,0,.45f)==1,"a lobe keeps its floor off its aim, all of it on its aim; no lobe lights all round");
        Check(inc.Contains("float a=1.0-q*q;float3 l=d/len;float k=a*a;")&&inc.Contains("if(q>=1.0)continue;")&&inc.Contains("k*=aim.w+(1.0-aim.w)*pow(saturate(-dot(l,aim.xyz)),lc.w);")&&R("Assets/ChipArena/Core/NativeCityLamps.cs").Contains("into[o+7]=l.Floor;")
            &&inc.Contains("float ndl=saturate((dot(n,l)+wrap)/(1.0+wrap));"),"the shader's lamp is the Core's (the slots' fall-off, the flood's cone, N.L)");
        Check(inc.Contains("if(_CityLampGrid.w<0.5)return sum;")&&inc.Contains("bool CityLampSlot(float w){return _CityLampGrid.w>0.5&&w<-0.5;}")&&inc.Contains("float id=ids[j];if(id<0.0)return sum;"),"the grid is off unless turned on (every other map draws as before); a cell's list ends at its first empty");
        Check(Regex(stage,"#include \"NativeCityLamps.cginc\"")==1&&stage.Contains("if(CityLampSlot(_ArenaLightColor[k].w))continue;")&&stage.Contains("sum+=CityLamps(world,n,v,wrap,sharp,spec);"),"StageLit takes the city's lamps from the grid and leaves their slots to the pilots and zombies (never twice)");
        foreach(var s in new[]{"NativeBlockLit.shader","NativeMolten.shader","NativeMoltenFlow.shader","NativeWardLight.cginc"})Check(R("Assets/ChipArena/Resources/"+s).Contains("WardBeam(d,_ArenaLightColor[k].w)"),s+" reads a marked slot (w -1) as a point light (WardBeam's w < 0.5)");
        Check(R("Assets/ChipArena/Resources/NativeWardLight.cginc").Contains("if(w<0.5)return 1.0;"),"WardBeam: any w under 0.5 is a point light");
        var draw=R("Assets/ChipArena/Runtime/NativeMeridianDressDraw.cs");
        Check(draw.Contains("lampLight=NativeMeridianDress.LightsOf(plan);lampBlock=NativeCityLampLight.Add(mapId,lampLight);")&&draw.Contains("NativeCityLampLight.Remove(lampBlock);"),"the dress hands every lamp to the grid and takes them back as it closes");
        Check(draw.Contains("NativeLighting.PickedLights(slotPos,slotRgb,slotBeam);")&&draw.Contains("slotBeam[s]=CitySlot;")&&draw.Contains("int slots=tier==NativeGraphicsTier.Low?3:NativeMapLooks.MaxLights;"),"the slots (8, Low 3) carry the nearest lamps to the pilots and zombies, marked");
        Check(!draw.Contains("NativeFieldGlow")&&!draw.Contains("PoolAlpha"),"no flat pool disc laid over the floor");
        Check(draw.Contains("if((cam.depthTextureMode&DepthTextureMode.Depth)==0)return;")&&draw.Contains("if(l.Kind==NativeMeridianDress.Flush)continue;float k=")&&draw.Contains("BeamFar"),"beams only where the depth texture fades them into what they meet, none from a flush lamp, gone far off");
        Check(draw.Contains("if(Mathf.Abs(solids.Floor(end.x,end.z,p.y+.3f)-p.y)>.03f||Mathf.Abs(solids.Floor(half.x,half.z,p.y+.3f)-p.y)>.03f)continue;"),"a wet glint only on a level floor");
        Check(draw.Contains("dark=NativeLighting.MutatorDark")&&draw.Contains("float k=dark?0:NativeMeridianDress.LampLevel(lamps[i],time,reduced);lampBlock.Level[i]=k;"),"DARKNESS puts the street lamps out");
        Check(!draw.Contains("WardField(")&&!draw.Contains("NativeWardZeroField"),"the dress never touches Ward Zero's light field");
        var up=R("Assets/ChipArena/Runtime/NativeCityLampLight.cs");
        Check(up.Contains("bool on=table!=null&&light!=null&&map!=null&&map==tableMap&&!NativeLighting.IntroOwned&&!NativeLighting.StageOwned;")&&up.Contains("else Shader.SetGlobalVector(GridId,Vector4.zero);")&&up.Contains("[DefaultExecutionOrder(1100)]"),
            "the grid lights only the lamps' own map, never a stage or an intro, after every drawer wrote its levels");
        Check(!System.Text.RegularExpressions.Regex.IsMatch(Body(up,"private void LateUpdate()"),@"\bnew (?!Vector4\()"),"the upload allocates nothing per frame");
        // the street life, the bleed and the camp light the same way (their flat pools gone)
        var life=R("Assets/ChipArena/Runtime/NativeMeridianStreetLifeDraw.cs");
        Check(life.Contains("lampBlock=NativeCityLampLight.Add(map.id,lampLights);")&&life.Contains("NativeMeridianStreetLife.CityLight(")&&life.Contains("NativeCityLampLight.Remove(lampBlock);")&&!life.Contains("Meridian street pools")
            &&life.Contains("if(plan!=null&&Hidden&&lampBlock!=null)for(int j=0;j<lit.Length;j++)lampBlock.Level[j]=0;"),"the street life's lit pieces light through the grid (none while hidden), no pool discs");
        var bleed=R("Assets/ChipArena/Runtime/NativeCityBleedDraw.cs");
        Check(bleed.Contains("lampBlock=NativeCityLampLight.Add(map.id,lamps.ToArray());")&&bleed.Contains("lampBlock.Level[j]=Intensity(lampPieces[j],time);")&&!bleed.Contains("floor+.06f")&&bleed.Contains("NativeCityLampLight.Remove(lampBlock);"),"the bleed's lamps light through the grid at their flicker, no pool discs");
        var camp=R("Assets/ChipArena/Runtime/NativeCampDraw.cs");var campFx=R("Assets/ChipArena/Core/NativeCampFx.cs");
        Check(camp.Contains("cityLamps=lampMap!=null?NativeCityLampLight.Add(lampMap,fx.CityLamps()):null;")&&camp.Contains("NativeLighting.KitLights(fx.LightPos,fx.LightRgb,fx.LightCount,at,CampSlots);")&&camp.Contains("CampSlots={-1,-1,-1}")
            &&!campFx.Contains("Pools++"),"the camp's lights light through the grid, its slots marked for the pilots, no pool discs");
        foreach(var f in new[]{"Core/NativeCityLamps.cs","Core/NativeMeridianDressLight.cs","Runtime/NativeCityLampLight.cs","Resources/NativeCityLamps.cginc"})Check(System.IO.File.Exists(System.IO.Path.Combine(root,"Assets/ChipArena",f+".meta")),f+" has its meta");
    }
    private static int Regex(string s,string what){int n=0,i=0;while((i=s.IndexOf(what,i,StringComparison.Ordinal))>=0){n++;i+=what.Length;}return n;}
    private static string Body(string s,string head){int a=s.IndexOf(head,StringComparison.Ordinal);if(a<0)return "";int b=s.IndexOf('{',a),depth=0;for(int i=b;i<s.Length;i++){if(s[i]=='{')depth++;else if(s[i]=='}'&&--depth==0)return s.Substring(b,i-b+1);}return s.Substring(b);}

    // `--meridian-lamps-probe PLAN X Z R`: the live deal's lamps within R of (X, Z) (every street plan's live deal, as the live run
    // dresses it), the light they put on the wall under each bracket and the floor under each lamp, and the grid's fill.
    public static void Probe(string root,int planIndex,float x,float z,float r)
    {
        var lives=HubZFightChecks.Worlds(root);
        foreach(var lv in lives)
        {
            var pl=HubZFightChecks.Dress(lv,null,false);var tab=NativeCityLamps.Build(NativeMeridianDress.LightsOf(pl));
            int[] kinds=new int[3];foreach(var l in pl.Lamps)if(l.State!=NativeMeridianDress.Dead)kinds[l.Kind]++;
            Console.WriteLine(string.Format(Inv,"{0}: {1} lamps ({2} swan, {3} pendant, {4} flush lit), grid {5} x {6}, fullest cell {7}, dropped {8}",NativeCityLayout.Plans[lv.Plan].Name,pl.Lamps.Count,kinds[0],kinds[1],kinds[2],tab.Nx,tab.Nz,tab.Fullest,tab.Dropped));
        }
        var live=lives.First(l=>l.Plan==planIndex);
        var plan=HubZFightChecks.Dress(live,null,false);var lights=NativeMeridianDress.LightsOf(plan);var table=NativeCityLamps.Build(lights);
        Console.WriteLine("plan "+NativeCityLayout.Plans[planIndex].Name+" seed "+live.Seed+": "+plan.Lamps.Count+" lamps");
        for(int i=0;i<plan.Lamps.Count;i++)
        {
            var l=plan.Lamps[i];float dx=l.Bowl.x-x,dz=l.Bowl.z-z;if(dx*dx+dz*dz>r*r)continue;
            var n=WallNormal(l);var wall1=l.Mount-n*0f-new V3(0,1,0);var wall2=l.Mount-new V3(0,2,0);
            string lit=l.State==NativeMeridianDress.Dead?"":string.Format(Inv," wall-1m {0:F2} wall-2m {1:F2} floor {2:F2} (old slot: wall-1m {3:F2} wall-2m {4:F2} floor {5:F2})",
                Luma(table.Light(wall1,n)),Luma(table.Light(wall2,n)),Luma(table.Light(l.Pool+new V3(0,.01f,0),new V3(0,1,0))),Luma(OldSlot(l,wall1,n)),Luma(OldSlot(l,wall2,n)),Luma(OldSlot(l,l.Pool+new V3(0,.01f,0),new V3(0,1,0))));
            Console.WriteLine(string.Format(Inv,"lamp {0} kind {1} state {2} prot {3} mount {4} bowl {5} pool {6} radius {7}{8}",i,l.Kind,l.State,l.Protected,F(l.Mount),F(l.Bowl),F(l.Pool),l.Radius,lit));
        }
        var eyeSpec=Environment.GetEnvironmentVariable("MERIDIAN_EYE");if(string.IsNullOrEmpty(eyeSpec))return;
        var ev=eyeSpec.Split(',').Select(s=>float.Parse(s,Inv)).ToArray();var e=new V3(ev[0],ev[1],ev[2]);var fw=new V3(ev[3],ev[4],ev[5]);fw=fw/fw.Length;
        var order=Enumerable.Range(0,plan.Lamps.Count).Where(i=>plan.Lamps[i].State!=NativeMeridianDress.Dead).Select(i=>{var l=plan.Lamps[i];var d=l.Bowl-e;float s=V3.Dot(d,d)*(V3.Dot(d,fw)<-l.Radius?3f:1f);return (i,s);}).OrderBy(p=>p.s).Take(12).ToList();
        foreach(var (i,s) in order){var l=plan.Lamps[i];Console.WriteLine(string.Format(Inv,"pick lamp {0} kind {1} score {2:F0} dist {3:F1} bowl {4}",i,l.Kind,s,(l.Bowl-e).Length,F(l.Bowl)));}
    }
    /// <summary>Shared's drawer's pick (before 2026-11-21): the nearest lit lamps to the camera, one behind it (past its pool's radius)
    /// at three times its distance squared.</summary>
    private static int[] SharedPick(NativeMeridianDress.Plan plan,V3 e,V3 fwd,int slots)
    {
        return Enumerable.Range(0,plan.Lamps.Count).Where(i=>plan.Lamps[i].State!=NativeMeridianDress.Dead)
            .Select(i=>{var l=plan.Lamps[i];var d=l.Bowl-e;return (i,s:V3.Dot(d,d)*(V3.Dot(d,fwd)<-l.Radius?3f:1f));}).OrderBy(p=>p.s).ThenBy(p=>p.i).Take(slots).Select(p=>p.i).ToArray();
    }
    /// <summary>The face a lamp hangs on, outward (a pendant's: up).</summary>
    private static V3 WallNormal(NativeMeridianDress.Lamp l)
    {
        if(l.Kind==NativeMeridianDress.Pendant)return new V3(0,1,0);var n=l.Bowl-l.Mount;n=new V3(n.x,0,n.z);return n.Length>1e-4f?n/n.Length:new V3(0,1,0);
    }
    /// <summary>Shared's practical slot for a lamp (before 2026-11-21): at the bowl less 0.2 m, its reach the pool's radius x 1.25, x 1.35.</summary>
    private static V3 OldSlot(NativeMeridianDress.Lamp l,V3 p,V3 n)
    {
        var c=l.Protected?NativeMeridianDress.RouteSodium:NativeMeridianDress.Sodium;var at=l.Bowl-new V3(0,.2f,0);var d=at-p;float len=Math.Max(d.Length,.0001f);
        float f=NativeCityLamps.Falloff(len,l.Radius*1.25f)*Math.Max(0,V3.Dot(n,d/len))*1.35f;return c*f;
    }
}
internal static class MeridianLampStats
{
    public static void Run(string root)
    {
        float Luma(V3 c)=>c.x*.2126f+c.y*.7152f+c.z*.0722f;
        foreach(var lv in HubZFightChecks.Worlds(root))
        {
            var pl=HubZFightChecks.Dress(lv,null,false);var tab=NativeCityLamps.Build(NativeMeridianDress.LightsOf(pl));
            var w1=new List<float>[3];var w2=new List<float>[3];var fl=new List<float>[3];for(int k=0;k<3;k++){w1[k]=new List<float>();w2[k]=new List<float>();fl[k]=new List<float>();}
            foreach(var l in pl.Lamps)
            {
                if(l.State==NativeMeridianDress.Dead)continue;var n=l.Bowl-l.Mount;n=new V3(n.x,0,n.z);n=n.Length>1e-4f?n/n.Length:new V3(0,1,0);if(l.Kind==NativeMeridianDress.Pendant)n=new V3(0,1,0);
                if(l.Kind!=NativeMeridianDress.Pendant){w1[l.Kind].Add(Luma(tab.Light(l.Mount-new V3(0,1,0)+n*.001f,n)));w2[l.Kind].Add(Luma(tab.Light(l.Mount-new V3(0,2,0)+n*.001f,n)));}
                fl[l.Kind].Add(Luma(tab.Light(l.Pool+new V3(0,.01f,0),new V3(0,1,0))));
            }
            string S(List<float> a)=>a.Count==0?"-":string.Format(System.Globalization.CultureInfo.InvariantCulture,"min {0:F2} p5 {1:F2} med {2:F2}",a.Min(),a.OrderBy(v=>v).ElementAt(a.Count/20),a.OrderBy(v=>v).ElementAt(a.Count/2));
            Console.WriteLine(NativeCityLayout.Plans[lv.Plan].Name+": swan wall-1m "+S(w1[0])+" | wall-2m "+S(w2[0])+" | floor "+S(fl[0])+" || flush wall-1m "+S(w1[2])+" | floor "+S(fl[2])+" || pendant floor "+S(fl[1]));
        }
    }
}
