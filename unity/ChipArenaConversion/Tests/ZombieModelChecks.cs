using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// The zombies on the player's model (owner 2026-10-29: "Implement the attached zombie designs ... using the existing low-poly player
// model as the starting point"; docs/ZOMBIE-MODELS.md): the variant table (Core NativeZombieLooks), its pick, the shape names, the
// shader's zombie look and the runtime's wiring into the horde's draw, death, corpses and wounds. The model itself, its captures and
// the crowd's cost are the owner's PC's (Unity and the licensed bake); these hold the data and the code paths.
internal static class ZombieModelChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie models: "+what);count++;}
    private static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk};
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Picks(){int sink=0;for(int i=0;i<4000;i++){var v=NativeZombieLooks.Of(Kinds[i%Kinds.Length],10000+i);sink+=v.Female?1:0;}return sink;}
    public static void Run(string root)
    {
        Table();Pick();Shapes();Sources(root);
        Console.WriteLine("PASS zombie models: "+count+" checks (20 variants over five kinds, two male and two female each; wardrobe fallbacks; silhouettes by build; the pick by id; the shape names; the shader's zombie look; the horde's draw, death, corpses and wounds on the model; presentation only)");
    }

    // ---- the variant table ----
    private static void Table()
    {
        var all=NativeZombieLooks.All;
        Check(all.Length==Kinds.Length*NativeZombieLooks.Variants&&all.Select(v=>v.Id).Distinct().Count()==all.Length,"twenty variants, each its own id");
        foreach(int kind in Kinds)
        {
            var mine=all.Where(v=>v.Kind==kind).ToArray();string name=NativeHordeKinds.Ids[kind];
            Check(mine.Length==4&&!mine[0].Female&&!mine[1].Female&&mine[2].Female&&mine[3].Female,name+": male A, male B, female A, female B");
            Check(mine.All(v=>v.Id.StartsWith(name+"-",StringComparison.Ordinal)),name+": ids named by kind");
            Check(mine.All(v=>v.Sac==(kind==NativeHordeKinds.Spitter)),name+": the throat sac is the spitter's alone");
            Check(mine.Where(v=>v.Female).All(f=>mine.Where(v=>!v.Female).All(m=>f.Shapes[0]<m.Shapes[0])),name+": each female body type under each male's (the pack's male/female shape)");
        }
        foreach(var v in all)
        {
            Check(v.Pieces.Length==NativeLook.Slots.Length&&v.Colours.Length==NativeLook.Slots.Length,v.Id+": a piece and a colour per slot");
            for(int s=0;s<v.Pieces.Length;s++)
            {
                var c=NativeZombieLooks.Candidates(v.Pieces[s]);string last=c.Last();
                Check(c.Length>0,v.Id+": a candidate in slot "+NativeLook.Slots[s]);
                Check(last==NativeZombieLooks.None&&s==0||NativeLook.Ids[s].Contains(last)&&!NativeLook.DevOnly(last),v.Id+": slot "+NativeLook.Slots[s]+" falls back on one of today's pieces (every bake dresses it): "+last);
                foreach(var id in c.Take(c.Length-1))Check(id.StartsWith(NativeZombieLooks.Own,StringComparison.Ordinal)&&!NativeLook.Ids.Any(ids=>ids.Contains(id)),v.Id+": a preferred piece is the zombies' own, never a Locker id: "+id);
                Check(v.Colours[s]==-1||v.Colours[s]>=0&&v.Colours[s]<=0xffffff,v.Id+": colour in range");
            }
            Check(v.Skin>=0&&v.Skin<=0xffffff&&v.Eyes>=0&&v.Eyes<=0xffffff,v.Id+": skin and eyes in range");
            Check(v.Shapes.Length==NativeZombieLooks.ShapeNames.Length&&v.Shapes.Take(NativeZombieLooks.BodyShapes).All(w=>!float.IsNaN(w))&&v.Shapes.All(w=>float.IsNaN(w)||w>=-100&&w<=100),v.Id+": every body shape set, weights in -100..100");
            var b=v.Build;
            Check(new[]{b.Arm,b.Forearm,b.Thigh,b.Calf,b.Hand}.All(g=>g>=.8f&&g<=1.6f)&&b.Head>=.9f&&b.Head<=1.1f&&b.Shoulder>=.9f&&b.Shoulder<=1.2f,v.Id+": the build within the rig's limits");
            Check(v.Grime>=0&&v.Grime<=1&&v.Tears>=0&&v.Tears<=.5f&&v.Rot>=0&&v.Rot<=1,v.Id+": wear within 0..1 (tears at most half: a few patches, not shreds)");
            // Pale skin and blank eyes: every zombie's skin is greyed (low saturation), its eyes near white.
            float Sat(int rgb){float r=(rgb>>16&255)/255f,g=(rgb>>8&255)/255f,bl=(rgb&255)/255f,hi=Math.Max(r,Math.Max(g,bl)),lo=Math.Min(r,Math.Min(g,bl));return hi>0?(hi-lo)/hi:0;}
            Check(Sat(v.Skin)<.22f&&Sat(v.Eyes)<.1f&&(v.Eyes>>16&255)>200,v.Id+": greyed skin, blank eyes");
        }
        // Silhouettes by build, not colour: the kinds' means apart, heavy ones heavier, the spitter the thinnest.
        float[] Sig(int kind){var m=all.Where(v=>v.Kind==kind).ToArray();float Mean(Func<NativeZombieLooks.Variant,float> f)=>m.Average(f);
            return new[]{NativeHordeKinds.Scale[kind],Mean(v=>v.Build.Arm),Mean(v=>v.Build.Forearm),Mean(v=>v.Build.Hand),Mean(v=>v.Build.Thigh),Mean(v=>v.Shapes[2]/100),Mean(v=>v.Shapes[1]/100),m[0].Sac?1:0};}
        float Arms(int kind)=>all.Where(v=>v.Kind==kind).Average(v=>v.Build.Arm+v.Build.Forearm);
        Check(Arms(NativeHordeKinds.Hulk)>Arms(NativeHordeKinds.Brute)&&Arms(NativeHordeKinds.Brute)>Arms(NativeHordeKinds.Shambler)&&Arms(NativeHordeKinds.Shambler)>Arms(NativeHordeKinds.Runner)&&Arms(NativeHordeKinds.Runner)>Arms(NativeHordeKinds.Spitter),"arm bulk: hulk > brute > shambler > runner > spitter");
        Check(all.Where(v=>v.Kind==NativeHordeKinds.Hulk||v.Kind==NativeHordeKinds.Brute).All(v=>v.Shapes[2]>=50&&v.Shapes[2]+v.Shapes[1]>=120)&&all.Where(v=>v.Kind==NativeHordeKinds.Spitter).All(v=>v.Shapes[1]<=5&&v.Shapes[2]==0),"the heavy kinds muscled or heavy-set (the brute's big-bellied male B), the spitter gaunt");
        for(int i=0;i<Kinds.Length;i++)for(int j=i+1;j<Kinds.Length;j++)
        {var a=Sig(Kinds[i]);var c=Sig(Kinds[j]);float d=(float)Math.Sqrt(a.Zip(c,(x,y)=>(x-y)*(x-y)).Sum());Check(d>=.2f,"silhouettes apart: "+NativeHordeKinds.Ids[Kinds[i]]+" and "+NativeHordeKinds.Ids[Kinds[j]]+" "+d.ToString("0.00"));}
    }

    // ---- the pick: by id, stable, every variant and both sexes worn ----
    private static void Pick()
    {
        foreach(int kind in Kinds)
        {
            var seen=new int[4];int women=0;var mine=NativeZombieLooks.All.Where(v=>v.Kind==kind).ToList();
            for(int id=10000;id<14000;id++){var v=NativeZombieLooks.Of(kind,id);Check(v!=null&&v.Kind==kind&&ReferenceEquals(v,NativeZombieLooks.Of(kind,id)),"a variant of its own kind, the same each time");seen[mine.IndexOf(v)]++;if(v.Female)women++;}
            Check(seen.All(n=>n>=800&&n<=1200),NativeHordeKinds.Ids[kind]+": each variant worn by about a quarter ("+string.Join(",",seen)+")");
            Check(women>=1800&&women<=2200,NativeHordeKinds.Ids[kind]+": about half female");
        }
        Check(NativeZombieLooks.Of(NativeHordeKinds.Crawler,1)==null&&NativeZombieLooks.Of(NativeHordeKinds.Bloater,1)==null&&NativeZombieLooks.Of(NativeHordeKinds.Count,1)==null,"kinds without variants keep their voxel model");
        bool was=NativeZombieLooks.Drafts;
        NativeZombieLooks.Drafts=false;
        Check(NativeZombieLooks.Modelled(NativeHordeKinds.Shambler)&&Kinds.Skip(1).All(k=>!NativeZombieLooks.Modelled(k)),"shipped: the shambler on the model first (the owner's order), the rest voxel until reviewed");
        NativeZombieLooks.Drafts=true;
        Check(Kinds.All(NativeZombieLooks.Modelled)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Crawler)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Bloater)&&!NativeZombieLooks.Modelled(NativeHordeKinds.Count)&&!NativeZombieLooks.Modelled(-1),"drafts: all five kinds; never the crawler, bloater or a zone kind");
        NativeZombieLooks.Drafts=was;
        Picks();long before=GC.GetAllocatedBytesForCurrentThread();int sink=Picks();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(sink>0&&bytes==0,"picking a variant allocates nothing ("+bytes+" B over 4000)");
    }

    // ---- the shape names the variants set on the pack's meshes ----
    private static void Shapes()
    {
        Check(NativeZombieLooks.ShapeNames.Take(NativeZombieLooks.BodyShapes).SequenceEqual(new[]{"BodyType","Weight","Muscle","Belly","Chest","NeckThickness","Curvy","ButtSize","WaistSize"}),"the pack's nine body shapes first (docs/BOZO-INTEGRATION.md)");
        foreach(var (name,want,ok) in new[]{("Shape_Muscle","Muscle",true),("Body.Shape_BodyType","BodyType",true),("muscle","Muscle",true),("Muscle","Muscle",true),("BSMC Weight","Weight",true),
            ("Tag_Muscle","Muscle",false),("Expression_Weight","Weight",false),("ShapeMuscle","Muscle",false),("Shape_MuscleX","Muscle",false),("NeckThickness","Thickness",false),("","Muscle",false),(null,"Muscle",false)})
            Check(NativeZombieLooks.ShapeMatches(name,want)==ok,"shape name "+(name??"null")+" is "+want+": "+ok);
    }

    // ---- the shader and the runtime ----
    private static void Sources(string root)
    {
        string A(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string fx=A("Resources/NativeVisualBodyFx.cginc"),toon=A("Resources/NativeVisualBodyToon.shader"),zr=A("Runtime/NativeZombieModelPresentation.cs"),kp=A("Runtime/NativeHordeKindPresentation.cs"),
            gp=A("Runtime/NativeHordeGorePresentation.cs"),dp=A("Runtime/NativeDeathPresentation.cs"),core=A("Core/NativeZombieLooks.cs");
        string look=fx.Substring(fx.IndexOf("float3 ZombieLook(float3 c,float2 uv)",StringComparison.Ordinal));look=look.Substring(0,look.IndexOf("\n}\n",StringComparison.Ordinal));
        Check(look.Contains("if(_ZombieEye.w>0.5)return _ZombieEye.rgb;")&&look.Contains("if(_ZombieTint.w<=0.0&&_ZombieWear.x+_ZombieWear.y+_ZombieWear.z<=0.0)return c;"),"the zombie look: blank eyes, and nothing changed with it unset (every pilot)");
        Check(Regex.Matches(toon,@"_Zombie(Tint|Wear|Eye|Skin)\(""[^""]*"",Vector\)=\(0,0,0,0\)").Count==4,"its four properties off by default");
        Check(toon.Contains("if(_VertexAlbedo<=0){o.Albedo=ZombieLook(o.Albedo,i.uv_Texture2D);")&&toon.Contains("albedo=ZombieLook(albedo,i.uv);"),"the surface and its outline take the zombie look (never a cut's cap)");
        Check(look.Contains("edge=1.0-0.45*_ZombieWear.y")&&look.Contains("uv*5.3"),"torn patches on the part's own texture coordinates (they stay on the cloth), a few at the table's tears");
        // The horde: the model in place of the voxel model on the full tier; the voxel draw kept as the fallback.
        Check(kp.Contains("if(!ZombieModelPose(rig,m.Id,kind,s.Tier,planted,airborne,a.Lost,0,flash,glow.x)){HordeQueue(rig,NativeHordeModels.Id(kind,s.Look,s.Tier),m.Id,a.Lost,0,swell,glow,flash);HordeQueueJaw(rig,kind,s.Look,s.Tier,s.Jaw,glow,flash);}"),"a living add: its model, else its voxel model and jaw");
        Check(kp.Contains("bool planted=(mode==0||mode==4||mode==5)&&!a.Crawl,airborne=mode==1||mode==3;"),"feet planted on the gait, a door's wake and a landing; airborne in a dive and a jump-down's fall");
        Check(kp.Contains("if(burn||!ZombieModelDeath(rig,death.id,d.Kind,death.lost,d.Slice)){HordeDeathBody(rig,model,death.id,death.lost,fx,hordeQueueVisible,burn);"),"a death: its model falls, else the voxel body");
        Check(kp.Contains("ClearHordeGore();ClearZombieModels();"),"cleared with the horde");
        Check(gp.Contains("if(ZombieCorpseDrawn(r,sink,alpha))continue;")&&gp.Contains("ZombieCorpsesKept();")&&gp.Contains("if(ZombieModelDrawn(id))return;"),"corpses from the baked model, sinking with the voxel timeline; no voxel-fitted wounds on the model");
        Check(dp.Contains("ZombieModelsShown();")&&dp.IndexOf("ZombieModelsShown();",StringComparison.Ordinal)>dp.IndexOf("finally{DiagnosticDeathWoundMs",StringComparison.Ordinal),"bodies posed this frame show, the rest hide, after the deaths");
        Check(zr.Contains("tier==NativeHordeModels.Full&&NativeZombieLooks.Modelled(kind)&&ZombieModelsOn")&&zr.Contains("VoxelZombiesArgument=\"--ca-voxel-zombies\"")&&zr.Contains("!ClassicBodies&&VisualBodiesOn"),"the model only on the full tier, for modelled kinds, with the bake, not Classic, not --ca-voxel-zombies");
        int build=zr.IndexOf("ZombieBuild(skeleton,v.Build);",StringComparison.Ordinal),binding=zr.IndexOf("Binding=new NativeVisualRigBinding(root.transform,skeleton)",StringComparison.Ordinal);
        Check(build>0&&binding>build,"the build set before the binding reads the rest pose");
        Check(zr.Contains("if(t.childCount==0&&n.IndexOf(\"twist\",StringComparison.OrdinalIgnoreCase)>=0)"),"girth only on the twist leaves (no child moves)");
        Check(zr.Contains("z.Body.Binding.Apply(pose,default(NativeVisualRig.Grasp),planted,airborne);"),"posed from the gait's own pose, no weapon grasp");
        Check(zr.Contains("if(zombieMakes>=ZombieMakesPerFrame||zombiePool.Count==0)return null;")&&zr.Contains("ZombieMakesPerFrame=3,ZombieSparesPerVariant=6")&&zr.Contains("zombieFill=InstantiateAsync(visualBodyPrefab,ZombiePoolBatch,zombieHolder.transform);")&&!zr.Contains("VisualCorpseInstance()")&&!zr.Contains("Instantiate(visualBodyPrefab)"),"a few new bodies a frame from their own pool, made ahead off the main thread (never at once, never the pilots' corpse pool); spares kept per variant");
        Check(kp.Contains("if(!HordeKindsReady)return;ZombieModelsPrewarm(arena);"),"the pool made ahead while a horde is about");
        Check(zr.Contains("Graphics.DrawMesh(mesh,m,mats[sub],0,null,sub,c.Blocks[i],false,true)"),"a corpse drawn from its baked meshes, no shadow");
        foreach(var ban in new[]{".Hp=","StrikeAt","LastAttack=","Struck","Damage(","NativeNetwork","PveAddStats",".radius=",".height="})Check(!zr.Contains(ban)&&!core.Contains(ban),"presentation only: never "+ban);
        Check(!core.Contains("UnityEngine"),"the table is Core (no Unity types)");
    }
}
