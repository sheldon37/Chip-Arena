using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// The stage art (docs/arenas/STAGE-ART.md): the bank's plan, the Sanctum's art poses against its box rig, the layout export, the
// runtime's wiring and its bank-absent fallback, the stage shader; and, where the bank is on this machine (it is git-ignored),
// the bank itself against the map. `dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-art` (also in the full run).
internal static class StageArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Stage art: "+why);}
        // ---- the registry and the plan ----
        Check(NativeStageArt.BankOf("sanctum")=="ChipArenaStageSanctum"&&NativeStageArt.BankOf("training")==null&&NativeStageArt.BankOf(null)==null,"the Sanctum's bank is named, a map without art has none");
        Check(NativeStageArt.TierAt(3,false)==0&&NativeStageArt.TierAt(20,false)==1&&NativeStageArt.TierAt(60,false)==2&&NativeStageArt.TierAt(3,true)==2,"the tiers by distance, and the Low tier on the low graphics tier");
        Check(NativeStageArt.Split("static/30002/1",out var k1,out var i1,out var t1)&&k1=="static"&&i1=="30002"&&t1==1,"a static chunk's name");
        Check(NativeStageArt.Split("block/sanctum-pew-n-w-0/2",out var k2,out var i2,out var t2)&&k2=="block"&&i2=="sanctum-pew-n-w-0"&&t2==2,"a block model's name");
        Check(NativeStageArt.Split("rig/a/b/0",out _,out var i3,out _)&&i3=="a/b"&&!NativeStageArt.Split("rig/x/3",out _,out _,out _)&&!NativeStageArt.Split("rig//0",out _,out _,out _)&&!NativeStageArt.Split("cover/x",out _,out _,out _),"names: ids may hold slashes, tiers 0..2 only");
        var synthetic=Synthetic();var plan=NativeStageArt.Read(NativePackBank.Read(NativePackBank.Write(synthetic)));
        Check(plan.Static.Count==2&&plan.Static.All(c=>c.Props.All(p=>p>=0)),"two chunks, every tier filled (a missing tier takes the nearest)");
        var c0=plan.Static.Single(c=>c.Id=="1");Check(c0.Props[0]==0&&c0.Props[1]==0&&c0.Props[2]==1,"a missing Mid takes the Fine");
        Check(plan.Blocks.ContainsKey("b-1")&&plan.Blocks["b-1"].All(p=>p==3)&&plan.Rig.ContainsKey("bell")&&plan.Rig["bell"].SequenceEqual(new[]{4,5,5}),"block models and rig parts by name");
        Check(plan.Covers.SetEquals(new[]{"wall-1","b-1"})&&plan.RigProps("none")==null&&NativeStageArt.Read(null)==null,"the covers; nothing for an unknown part or no bank");
        Check(NativeStageArt.Stage(60,60,false,.66f,.33f)==0&&NativeStageArt.Stage(30,60,false,.66f,.33f)==1&&NativeStageArt.Stage(10,60,false,.66f,.33f)==2&&NativeStageArt.Stage(0,60,false,.66f,.33f)==3&&NativeStageArt.Keep==2,"a block's model stands to stage 2; the stub is the dressing's");

        // ---- the Sanctum: its art poses agree with the box rig at every moment ----
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("sanctum");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var looks=NativeSanctumLooks.Resolve(NativeMapLooks.Find(set,"sanctum").kitLooks);
        var rig=new NativeSanctumRig(looks);var pose=new NativeStageArtPose();
        Check(NativeSanctumArt.Slots.Length==35&&NativeSanctumArt.Slots.Distinct().Count()==35&&NativeSanctumArt.Slots[NativeSanctumArt.FirstPolishSlot]=="censer-body"&&NativeSanctumArt.Slots[NativeSanctumArt.DebrisPlaster]=="debris-plaster"&&NativeSanctumArt.Slots[NativeSanctumArt.BannerBlue]=="banner-blue"&&NativeSanctumArt.Slots[NativeSanctumArt.Fire+3]=="fire-sanctum-fire-s-1","the rig slots");
        Check(map.hazards.Count(h=>h.kind=="shutter")==2&&map.hazards.Count(h=>h.kind=="burner")==2&&map.hazards.Where(h=>h.kind=="shutter").Select(h=>"gate-"+h.id).SequenceEqual(NativeSanctumArt.Slots.Skip(NativeSanctumArt.Gate).Take(2)),"a gate slot per shutter, in the hazards' order");
        double worst=0;int seen=0,down=0,falling=0,held=0,swung=0;var phases=new HashSet<int>();
        for(int round=0;round<2;round++)for(double t=0;t<70;t+=.05)
        {
            rig.Build(map.hazards,round,NativePhase.Fight,0,t,t);NativeSanctumArt.Pose(rig,map.hazards,t,pose);seen++;
            for(int i=0;i<pose.Count;i++)
            {
                int slot=pose.Slot[i];
                if(slot==NativeSanctumArt.Chandelier)
                {
                    // the hub: the box rig's iron knop (.18 x .22 x .18) at the frame's (0, crown, 0)
                    At(pose,i,0,NativeSanctumLooks.CrownHeight,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.18f,.22f,.18f));
                }
                else if(slot==NativeSanctumArt.Wreck){down++;At(pose,i,0,.6,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.18f,.22f,.18f));}
                else if(slot==NativeSanctumArt.CenserBody)
                {
                    // the coals' disc (.5 x .06 x .5) at the frame's (0, .02, 0)
                    At(pose,i,0,.02,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.5f,.06f,.5f));if(Math.Abs(pose.Place[i*12+6])>1e-3)swung++;
                }
                else if(slot==NativeSanctumArt.Bell)
                {
                    // the lip (mouth + .05) at the frame's (0, -height - .1, 0)
                    At(pose,i,0,-NativeSanctumLooks.BellHeight-.1,0,out double x,out double y,out double z);float r=(float)NativeSanctumLooks.BellMouth+.05f;worst=Math.Max(worst,Nearest(rig,x,y,z,r,.05f,r));
                }
                else if(slot==NativeSanctumArt.PullRope)held++;
                else if(slot==NativeSanctumArt.BannerRed||slot==NativeSanctumArt.BannerBlue)
                {
                    // the cloth's middle (.012 x 2.8 x .7) at the frame's (0, -2.8, 0)
                    At(pose,i,0,-2.8,0,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.012f,2.8f,.7f));
                }
                else if(slot==NativeSanctumArt.Gate||slot==NativeSanctumArt.Gate+1)
                {
                    // a grille's first bar's foot: the box rig's bar (.04 x tall/2 x .035) centred tall/2 up
                    var h=map.hazards.Where(q=>q.kind=="shutter").ElementAt(slot-NativeSanctumArt.Gate);double tall=h.size.y;
                    At(pose,i,0,tall/2,-h.size.z/2+.1,out double x,out double y,out double z);worst=Math.Max(worst,Nearest(rig,x,y,z,.04f,(float)tall/2,.035f));
                }
            }
            for(int k=0;k<4;k++)phases.Add((int)rig.Ring[k*5+4]);if(rig.Ring.Where((v,j)=>j%5==4).Any(v=>v==2))falling++;
        }
        Check(worst<2e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.0000")+" m)");
        Check(phases.SetEquals(new[]{0,1,2,3,4})&&down>0&&falling>0&&held>0&&swung>0,"the poses cover every chandelier phase, the wreck, the censer held and swinging");
        // the count of draws stays inside the fixed buffer; deterministic; allocation free
        rig.Build(map.hazards,0,NativePhase.Fight,0,30,30);NativeSanctumArt.Pose(rig,map.hazards,30,pose);int n=pose.Count;var copy=pose.Place.Take(n*12).ToArray();
        Check(n>30&&n<NativeStageArtPose.Capacity,"a frame's rig draws ("+n+")");
        rig.Build(map.hazards,0,NativePhase.Fight,0,30,30);NativeSanctumArt.Pose(rig,map.hazards,30,pose);Check(pose.Count==n&&pose.Place.Take(n*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/60.0;rig.Build(map.hazards,1,NativePhase.Fight,0,t,t);NativeSanctumArt.Pose(rig,map.hazards,t,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");
        var span=new NativeStageArtPose();span.Span(0,1,2,3,1,5,3);At(span,0,0,1,0,out double sx,out double sy,out double sz);Check(Math.Abs(sx-1)<1e-5&&Math.Abs(sy-5)<1e-5&&Math.Abs(sz-3)<1e-5,"a span lays a unit prop end to end");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"sanctum",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;var kit=NativeSanctumKit.Build(map,looks);
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("parts").GetArrayLength()==kit.Parts.Count&&j.GetProperty("candles").GetArrayLength()*5==kit.Candles.Count,"the layout carries every block, kit part and candle");
            Check(kit.Family.Count==kit.Parts.Count&&kit.Owner.Count==kit.Parts.Count&&kit.Kind.Count==kit.Parts.Count&&kit.Family.All(f=>!string.IsNullOrEmpty(f)),"every kit part has its family, owner and kind");
            Check(j.GetProperty("parts").EnumerateArray().Any(p=>p.GetProperty("f").GetString()=="lancet")&&j.GetProperty("rig").GetProperty("chandeliers").GetArrayLength()==4,"the families and the chandeliers' candles");
            Check(j.GetProperty("windows").GetArrayLength()==NativeSanctumLooks.Windows.Length,"the windows");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the runtime's wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var draw=R("Assets/ChipArena/Runtime/NativeStageArt.cs");var sanctum=R("Assets/ChipArena/Runtime/NativeSanctumPresentation.cs");var dressing=R("Assets/ChipArena/Runtime/NativeArenaDressing.cs");
        Check(draw.Contains("var bank=NativePackBanks.Bank(name);if(bank==null)return false;")&&draw.Contains("public bool Ready=>plan!=null&&root!=null;"),"without the bank the art never readies");
        Check(sanctum.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);")&&sanctum.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the box kit and rig draw whenever the art does not");
        Check(dressing.Contains("int stage=NativeBlockLooks.Stage(f,e.S1,e.S2);if(stageArt&&NativeStageArtDraw.Hides(arena.MapId,p.Definition.id,stage))continue;"),"the dressing leaves out only the boxes the art drew this frame");
        Check(draw.Contains("Time.frameCount-activeFrame>1")&&draw.Contains("stage<=NativeStageArt.Keep"),"a stale or another map's art hides nothing");
        Check(draw.Contains("probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting")&&draw.Contains("probe.RenderProbe()"),"the reflection probe renders once, by script");
        var shader=R("Assets/ChipArena/Resources/NativeStageLit.shader");
        Check(shader.Contains("Shader \"ChipArena/StageLit\"")&&shader.Contains("\"LightMode\"=\"ShadowCaster\"")&&shader.Contains("Stencil { Ref 16 WriteMask 16")&&shader.Contains("BRDF1_Unity_PBS")&&shader.Contains("UnityGI_IndirectSpecular")&&shader.Contains("ToonDiffuse(")&&shader.Contains("ArenaScatter(")&&shader.Contains("UNITY_APPLY_FOG"),"the stage shader: shadows, the outline mark, the PBR key and probe, the toon, the scattering and fog");
        Check(StagePreloadPlan.Instanced("ChipArena/StageLit"),"the warm-up knows the stage shader is instanced");

        // ---- layers: stacked faces part by layer, not by millimetres (owner 2026-11-11: "Z-Fighting ... Floors mainly") ----
        // across a floor from a pilot's eye height (1.7 m) the depth between two faces `gap` apart is gap x 1.7 / distance; the
        // depth buffer's step there is distance^2 x 1e-6 m (24 bits, the .06 m near plane): a 20 mm lift is lost past 32 m
        Check(.02*1.7/40<40*40*1e-6&&.006*1.7/25<25*25*1e-6,"a lift of millimetres does not part a floor's layers across the arena (the reason for layers)");
        for(float d=1;d<=200;d*=1.5f)for(int l=1;l<=NativeStageArt.MostLayers;l++)
        {
            float step=NativeStageArt.Nearer(d,l)*d-NativeStageArt.Nearer(d,l-1)*d;
            Check(step>=3*d*d*1e-6f||NativeStageArt.Nearer(d,l)>=.033f-1e-6f,"a layer comes three depth steps nearer than the one under it at "+d+" m (layer "+l+")");
        }
        Check(NativeStageArt.Nearer(30,0)==0&&NativeStageArt.Nearer(30,-1)==0&&NativeStageArt.Nearer(1e6f,4)<=.033f&&NativeStageArt.Nearer(10,40)==NativeStageArt.Nearer(10,NativeStageArt.MostLayers),"no layer, no move; never more than a thirtieth nor sixteen layers");
        Check(NativeStageArt.Nearer(40,NativeStageArt.KitLayers)*40<.1f&&NativeStageArt.Nearer(10,NativeStageArt.ModelLayers)*10<.002f,"a kept kit box 40 m off comes under 10 cm nearer, a model 10 m off under 2 mm");
        Check(NativeStageArt.ModelLayers==2&&NativeStageArt.KitLayers==8&&NativeStageArt.KitLayers>NativeStageArt.ModelLayers+4&&NativeStageArt.MostLayers==16,"static layers, then the models', then the kept kit's");
        Check(shader.Contains("float f=min(min(layer,16.0)*(4e-6*clip.w+5e-5),0.033);")&&shader.Contains("clip.xyw*=1.0-f;clip.z=(clip.z-b)*(1.0-f)+b;")&&shader.Contains("if(layer<0.5||unity_OrthoParams.w>0.5)return clip;")
            &&shader.Contains("o.pos=StageLayer(o.pos,layer+_StageLift);")&&shader.Contains("float StageKindOf(float2 uv1){return floor(uv1.x+0.015625);}")&&shader.Contains("o.mat=float4(v.texcoord.xy,StageKindOf(v.texcoord1),v.texcoord1.y);"),
            "the shader draws a layered face nearer the eye along its ray (the same pixel), by Nearer's own numbers, and reads the kind without the layer");
        Check(draw.Contains("solidUp.SetFloat(LiftId,NativeStageArt.ModelLayers);")&&draw.Contains("glowUp.SetFloat(LiftId,NativeStageArt.ModelLayers);")&&draw.Contains("Graphics.DrawMesh(piece.Mesh,m,s==0?solidUp:glowUp,0,null,s,block,ShadowCastingMode.TwoSided,true)")
            &&draw.Contains("Graphics.DrawMeshInstanced(b.Piece.Mesh,s,s==0?solidUp:glowUp,b.M,b.Count,block")&&draw.Contains("r.sharedMaterials=piece.Subs>1?new[]{solid,glow}:new[]{solid};"),"the blocks' and the rig's models draw their layers up; the chunks do not");
        var kitPy=R("tools/stages/stagekit.py");
        Check(kitPy.Contains("def layer_polys(P64, S, reach=LAYER_REACH, least=2e-4, share=.02):")&&kitPy.Contains("LAYER_REACH = .03")&&kitPy.Contains("om.uv1.append([u[0] + L / 32.0, u[1]])")&&kitPy.Contains("        self.layer()")&&kitPy.Contains("def layer(self):"),
            "every bank is layered as it is written");
        // cells: no floor reaches out of its chunk's cell (a floor is never drawn twice where neighbours draw at different tiers)
        Check(kitPy.Contains("def split(self, g, reach=24, least=.08, upright=2.0):")&&kitPy.Contains("def at(self, block_id, pos, quat=(0.0, 0.0, 0.0, 1.0)):"),"the chunks cut their faces along their cells; a stage may place its block models");
        foreach(var script in new[]{"sanctum","club","emporium","causeway","breakwater","harbor","rig","polar","skyframe","brickfall","factory","cybercity"})
            Check(R("tools/stages/"+script+"_art.py").Contains("% chunks.split(g))"),script+": the static scene is cut along the chunks' cells before its bake");
        Check(R("tools/stages/wardzero_art.py").Contains("% chunks.split(S.static))"),"Ward Zero: the static scene is cut along the chunks' cells before its bake");
        // the kept kit boxes' own stacking
        {
            // a slab; a strip on it (6 mm proud); a mark on the strip (in the strip's plane: the smaller lies on the larger); a box apart; a turned box
            float[] bx={0,0,0, 2,.1f,2,   0,.006f,0, 1,.1f,.2f,   0,.006f,0, .2f,.1f,.1f,   9,0,0, 1,.1f,1,   0,.01f,0, .5f,.1f,.5f};var lay=new int[5];
            NativeStageArt.BoxLayers(bx,5,new[]{false,false,false,false,true},lay);
            Check(lay[0]==0&&lay[1]==1&&lay[2]==2&&lay[3]==0&&lay[4]==0,"a strip on a slab is one up, a mark on the strip two; a box apart and a turned box none ("+string.Join(",",lay)+")");
            // two bars crossed in one plane, each proud of the other at its ends: one order, the smaller on top
            float[] cross={0,0,0, 1,.05f,.1f,   0,0,0, .1f,.05f,.9f};var cl=new int[2];NativeStageArt.BoxLayers(cross,2,null,cl);
            Check(cl[0]+cl[1]==1&&cl[1]==1,"crossed bars take one order ("+cl[0]+","+cl[1]+")");
            // a ring of overlapping boxes never runs past the kit's share of the layers
            var ring=new float[12*6];for(int i=0;i<12;i++){double a=i*Math.PI/6;ring[i*6]=(float)Math.Cos(a)*2;ring[i*6+1]=0;ring[i*6+2]=(float)Math.Sin(a)*2;ring[i*6+3]=.8f;ring[i*6+4]=.01f;ring[i*6+5]=.8f;}
            var rl=new int[12];NativeStageArt.BoxLayers(ring,12,null,rl);Check(rl.All(v=>v>=0&&v<=NativeStageArt.MostLayers-NativeStageArt.KitLayers)&&rl.Max()>0,"a ring of boxes in one plane stays within the layers");
        }
        foreach(var stage in new[]{"Factory","Emporium","Club"})
        {
            var pres=R("Assets/ChipArena/Runtime/Native"+stage+"Presentation.cs");
            Check(pres.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);}nearEye=ArtDrawn?camera:null;")&&pres.Contains("var M=b.M;if(nearEye!=null){NativeStageArtDraw.Nearer(b.M,nearM,b.Count,nearEye.transform.position,NativeStageArt.KitLayers);M=nearM;}")
                &&pres.Contains("Graphics.DrawMeshInstanced(Block,0,lit,M,b.Count,props,shadow,true,0,null);return;")&&pres.Contains("            nearEye=null;"),
                stage+": the boxes kept over the art draw the kit's layers nearer the eye, only while the art draws");
        }
        Check(draw.Contains("public static void Nearer(Matrix4x4[] from,Matrix4x4[] to,int count,Vector3 eye,float layers)")&&draw.Contains("m.m03=eye.x+(m.m03-eye.x)*s;m.m13=eye.y+(m.m13-eye.y)*s;m.m23=eye.z+(m.m23-eye.z)*s;to[i]=m;"),"kept boxes are scaled about the eye (the same pixels, a nearer depth)");
        var wardDraw=R("Assets/ChipArena/Runtime/NativeWardZeroPresentation.cs");var metroDraw=R("Assets/ChipArena/Runtime/NativeCyberCityPresentation.cs");
        Check(wardDraw.Contains("if(art&&eye!=null){float near=1-NativeStageArt.Nearer(Vector3.Distance(from,at),NativeStageArt.KitLayers+(i<wzKitLayer.Length?wzKitLayer[i]:0));place=Matrix4x4.TRS(from+(at-from)*near,q,new Vector3(near,near,near));}")
            &&wardDraw.Contains("wzKitLayer=NativeWardZeroArt.KeptLayers(wzKit,blocks);"),"Ward Zero's kept boxes draw nearer the eye over the art, scaled about the eye; without the art as before");
        Check(metroDraw.Contains("if(metroArtDrawn&&eye!=null){float near=1-NativeStageArt.Nearer(Vector3.Distance(from,at),NativeStageArt.KitLayers+(i<metroKitLayer.Length?metroKitLayer[i]:0));place=Matrix4x4.TRS(from+(at-from)*near,q,new Vector3(near,near,near));}")
            &&metroDraw.Contains("metroKitLayer=NativeCyberCityArt.KeptLayers(metroKit,map.blocks);"),"Cyber City's kept boxes draw nearer the eye over the art, scaled about the eye; without the art as before");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Stage art: the Sanctum bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var pews=map.blocks.Where(b=>b.role!="static").ToArray();
            Check(pews.Length==14&&pews.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)),"every breakable pew has its model in every tier");
            // (a bank from before the hazards' polish lacks the polish slots, all of them: the runtime then poses the older models)
            var first=NativeSanctumArt.Slots.Take(NativeSanctumArt.FirstPolishSlot).ToArray();var polish=NativeSanctumArt.Slots.Skip(NativeSanctumArt.FirstPolishSlot).ToArray();
            Check(first.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)),"every rig slot has its model in every tier ("+string.Join(",",first.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            bool polished=polish.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0));Check(polished||polish.All(s=>!sp.Rig.ContainsKey(s)),"the hazards' polish props are all in the bank, or none of them ("+string.Join(",",polish.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            Console.WriteLine("Stage art: the Sanctum bank "+(polished?"has the hazards' polish props":"is from before the hazards' polish (the older censer and strewn candles draw)"));
            Check(new FileInfo(bankPath).Length<=21*1048576,"the bank keeps to its budget, about 20 MB ("+(new FileInfo(bankPath).Length/1048576.0).ToString("0.00")+" MB)");
            Check(sp.Static.Count>=20&&sp.Static.All(c=>c.Props.Distinct().Count()==3),"the static chunks, each in three tiers");
            // the play space: below 2 m over its floor the Fine art keeps to the blocks (within .25 m, the stage rule; a thin prop
            // such as the altars' candlesticks within .9 m); higher is overhead (the crypt's vault springs 2.2 m up); nothing
            // stands in a spawn's capsule
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            foreach(var c in sp.Static)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    double floor=v.Y<-.3?-3.6:v.Y<3.95?0:4;if(v.Y-floor>2||v.Y<floor-.05)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25){proud++;var key=((int)Math.Floor(v.X))+","+((int)Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Stage art: bank "+new FileInfo(bankPath).Length/1048576.0+" MB, "+bank.Props.Count+" props; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>total/100)Console.WriteLine("Stage art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS stage art ("+count+" checks)");
    }
    // The point (lx, ly, lz) of draw i's frame in the world.
    private static void At(NativeStageArtPose p,int i,double lx,double ly,double lz,out double x,out double y,out double z)
    {int o=i*12;var m=p.Place;x=m[o]*lx+m[o+1]*ly+m[o+2]*lz+m[o+3];y=m[o+4]*lx+m[o+5]*ly+m[o+6]*lz+m[o+7];z=m[o+8]*lx+m[o+9]*ly+m[o+10]*lz+m[o+11];}
    // The distance from (x, y, z) to the nearest box rig part of these half extents (or infinity).
    private static double Nearest(NativeSanctumRig rig,double x,double y,double z,float hx,float hy,float hz)
    {
        double best=double.MaxValue;for(int i=0;i<rig.Parts.Count;i++){ref var p=ref rig.Parts.Items[i];if(Math.Abs(p.Hx-hx)>1e-3||Math.Abs(p.Hy-hy)>1e-3||Math.Abs(p.Hz-hz)>1e-3)continue;double d=Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y)+(p.Z-z)*(p.Z-z));if(d<best)best=d;}
        return best;
    }
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageSanctum.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageSanctum.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
    // A small bank: two chunks (one without its Mid), a block model, a rig part with a missing Low, two covers.
    private static NativePackBank.Bank Synthetic()
    {
        var b=new NativePackBank.Bank();b.Materials.Add(new NativePackBank.Material{Name="stage",Albedo=-1,Emission=-1,Color=System.Numerics.Vector4.One});
        var mesh=new NativePackBank.Mesh{Name="m",Positions=new[]{new System.Numerics.Vector3(0,0,0),new System.Numerics.Vector3(1,0,0),new System.Numerics.Vector3(0,1,0)},Normals=new System.Numerics.Vector3[3],Uv0=new System.Numerics.Vector2[3],Submeshes=new[]{new NativePackBank.Submesh{Material=0,Indices=new[]{0,1,2}}}};
        b.Meshes.Add(mesh);var part=new[]{new NativePackBank.Part{Mesh=0,Place=System.Numerics.Matrix4x4.Identity}};
        void P(string n,string g,NativePackBank.Part[] parts){b.Props.Add(new NativePackBank.Prop{Name=n,Group=g,Min=System.Numerics.Vector3.Zero,Max=System.Numerics.Vector3.One,Parts=parts});}
        P("static/1/0","static",part);P("static/1/2","static",part);P("static/2/0","static",part);P("block/b-1/0","block",part);P("rig/bell/0","rig",part);P("rig/bell/1","rig",part);
        P("cover/wall-1","cover",Array.Empty<NativePackBank.Part>());P("cover/b-1","cover",Array.Empty<NativePackBank.Part>());P("odd/name","static",part);
        return b;
    }
}
