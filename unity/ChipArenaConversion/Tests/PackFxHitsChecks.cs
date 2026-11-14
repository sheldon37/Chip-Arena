#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The effect upgrades from the owner's packs (owner 2026-10-28; docs/PACK-EFFECTS.md): the guns' pack muzzle flashes, the
    // pack's blood splats, the surface impacts for every surface, the afterimages' trails look and motion triggers. Each
    // look is a pure function of the replicated shot and falls back to exactly today's when its textures are missing.
    internal static class PackFxHitsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Pack fx hits: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Replace("\r\n","\n").Split('\n').Where(l=>!l.TrimStart().StartsWith("//")));
        private static string Between(string s,string a,string b){int i=s.IndexOf(a,StringComparison.Ordinal);if(i<0)return "";int j=s.IndexOf(b,i+a.Length,StringComparison.Ordinal);return j<0?s.Substring(i):s.Substring(i,j-i);}
        public static void Run(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            Muzzle(root,R);
            Blood(root,R);
            Impacts(root,R);
            Trails(root,R);
            Console.WriteLine("PASS pack fx hits: "+count+" checks");
        }

        // A. Muzzle flashes: per gun family a flipbook flame and a burst through the pack bank.
        private static void Muzzle(string root,Func<string,string> R)
        {
            foreach(var (spec,family) in new[]{("pistol",NativeMuzzleFx.SheetPistol),("rifle",NativeMuzzleFx.SheetRifle),("machinegun",NativeMuzzleFx.SheetMachineGun)})
                for(int skin=0;skin<2;skin++)
                {
                    Check(NativeMuzzleFx.Look(spec,skin==0?NativeWeaponSkins.StandardIndex:NativeWeaponSkins.PrototypeIndex,default,null,MuzzleKit.None,0,0,out var look)&&look.Sheet==family,spec+" skin "+skin+" takes its family's sheet");
                    Check(NativeMuzzleFx.SheetOf(look.Sheet,out var s)&&s.Columns*s.Rows>=s.First+s.Frames&&s.Frames>=4&&s.Length>0&&s.Width>0&&s.Life>=1&&s.BurstSize>0,spec+" sheet in its grid");
                }
            Check(NativeMuzzleFx.Look("machinegun",out var turret)&&turret.Sheet==NativeMuzzleFx.SheetMachineGun,"the plain look is the machine gun's");
            NativeMuzzleFx.Turret(ref turret);Check(turret.Sheet==NativeMuzzleFx.SheetTurret,"the hazard kit's turrets take their own sheet");
            Check(!NativeMuzzleFx.Look("sword",out var none)&&none.Sheet==0&&!NativeMuzzleFx.SheetOf(0,out _)&&!NativeMuzzleFx.SheetOf(NativeMuzzleFx.Sheets+1,out _),"no flash, no sheet");
            Check(!NativeMuzzleFx.SheetFlame(default,1,0,out _,out _,out _,out _,out _)&&!NativeMuzzleFx.SheetBurst(default,1,0,out _,out _,out _),"a look without a family draws no sheet");
            // The flame plays its frames once, inside the family's range, fades only, and is gone by its life; the burst is HDR.
            for(int family=1;family<=NativeMuzzleFx.Sheets;family++)
            {
                MuzzleLook l;if(family==NativeMuzzleFx.SheetTurret){NativeMuzzleFx.Look("machinegun",out l);NativeMuzzleFx.Turret(ref l);}else NativeMuzzleFx.Look(family==1?"pistol":family==2?"rifle":"machinegun",NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.None,0,0,out l);
                NativeMuzzleFx.SheetOf(family,out var s);float lastFrame=-1,lastAlpha=2;int shown=0;double life=l.Life*s.Life;
                for(double age=0;age<life+.05;age+=.002)
                {
                    bool on=NativeMuzzleFx.SheetFlame(l,77,age,out float length,out float width,out float frame,out bool mirror,out float alpha);
                    if(age>=life){Check(!on,"family "+family+" flame gone by its life");continue;}
                    Check(on&&frame>=s.First&&frame<s.First+s.Frames&&frame>=lastFrame&&alpha<=lastAlpha+1e-6f&&alpha>=0&&length>0&&width>0&&width<length,"family "+family+" frame "+frame+" alpha "+alpha+" at "+age);
                    lastFrame=frame;lastAlpha=alpha;shown++;
                    Check(NativeMuzzleFx.SheetFlame(l,77,age,out float length2,out _,out float frame2,out bool mirror2,out _)&&length2==length&&frame2==frame&&mirror2==mirror,"family "+family+" deterministic");
                }
                Check(shown>10&&lastFrame>s.First+s.Frames-1.5f,"family "+family+" plays every frame");
                Check(NativeMuzzleFx.SheetFlameColor(l,0,1).Max>1&&NativeMuzzleFx.SheetBurst(l,77,0,out float bs,out _,out var bc)&&bc.Max>1&&bs>0,"family "+family+" HDR at the shot (bloom)");
                Check(!NativeMuzzleFx.SheetBurst(l,77,l.Life*s.BurstLife+1e-3,out _,out _,out _),"family "+family+" burst gone by its life");
                int mirrored=0;for(int k=0;k<64;k++){NativeMuzzleFx.SheetFlame(l,NativeWeaponFx.Seed(k*.1,3),0,out _,out _,out _,out bool m,out _);if(m)mirrored++;}Check(mirrored>16&&mirrored<48,"family "+family+" mirrored about half the shots");
            }
            Check(NativeMuzzleFx.ViewSize(10,1,.5f,NativeMuzzleFx.ViewBurstShare)>NativeMuzzleFx.ViewSize(10,1,.5f)&&NativeMuzzleFx.ViewSize(10,1,.5f)==NativeMuzzleFx.ViewSize(10,1,.5f,NativeMuzzleFx.ViewShare),"first person: the burst's own cap above the glow's");
            // The selection names exactly the sheets' textures, from the muzzle-flash pack.
            var sel=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack_bank/selections/fx-muzzle.json"))).RootElement;
            var ids=sel.GetProperty("textures").EnumerateArray().Select(t=>t.GetProperty("id").GetString()).OrderBy(x=>x).ToArray();
            Check(ids.SequenceEqual(NativeMuzzleFx.SheetIds.OrderBy(x=>x))&&NativeMuzzleFx.SheetLooks.All(s=>NativeMuzzleFx.SheetIds.Contains(s.Flame)&&NativeMuzzleFx.SheetIds.Contains(s.Burst)),"fx-muzzle.json lists exactly the sheets' ids");
            Check(!sel.TryGetProperty("bank",out _)&&sel.GetProperty("textures").EnumerateArray().All(t=>t.GetProperty("clamp").GetBoolean()&&t.GetProperty("alpha").GetBoolean()),"textures only, clamped, with alpha");
            // The drawer: a material per family only with both textures; the shapes draw the sheet only when the drawer has it,
            // the procedural plume otherwise; the shader's two modes read only the family material's textures.
            string draw=Code(R("Runtime/NativeWeaponFxDraw.cs")),fx=R("Resources/NativeWeaponFx.shader"),kit=Code(R("Runtime/NativeHazardKitPresentation.cs"));
            Check(draw.Contains("Texture flame=NativePackBanks.Texture(s.Flame),burst=NativePackBanks.Texture(s.Burst);if(flame==null||burst==null)continue;")&&draw.Contains("public bool HasSheet(int family)=>family>=1&&family<=NativeMuzzleFx.Sheets&&sheetMaterials[family-1]!=null;"),"a family without both textures has no material (today's flash)");
            var shapes=Between(draw,"public static void Muzzle(","public static void Wisps(");
            Check(shapes.Contains("bool sheet=draw.HasSheet(look.Sheet);")&&shapes.Contains("if(sheet)\n")&&shapes.Contains("else if(NativeMuzzleFx.Plume(look,seed,age,out float plume,out float plumeWidth,out float plumeAlpha))")&&shapes.Contains("(sheet?SheetFireball:1)"),"the sheet replaces the plume and softens the fireball only with the family's pack flash");
            Check(Regex.Matches(shapes,@"draw\.Sheet(Flame|Burst)\(first,look\.Sheet,").Count==2&&draw.Contains("if(!HasSheet(family)||"),"the sheet quads go to the family's layers only");
            Check(Between(draw,"public void Submit(","public void Dispose()").Contains("sheetWorld[f].Draw(sheetMaterials[f],0,camera,worldProperties)")&&Between(draw,"public void Submit(","public void Dispose()").Contains("sheetView[f].Draw(sheetMaterials[f],10,viewCamera,viewProperties)")&&Between(draw,"public void Dispose()","}\n    }").Contains("CapturedAnimation.Release(sheetMaterials[f])"),"the family layers draw on both cameras and are released");
            Check(fx.Contains("_FlashSheet(\"Pack flash: the flame flipbook\",2D)=\"black\"{}")&&fx.Contains("if(mode>7.5)shape=mode<8.5?FlashFlame(")&&fx.Contains("if(mode>2.5&&mode<4.5||mode>8.5)")&&fx.IndexOf("float2 FlashFlame(",StringComparison.Ordinal)>fx.IndexOf("WEAPON FX SHARED END",StringComparison.Ordinal),"the shader's sheet modes, read outside the shared block, black by default");
            Check(draw.Contains("public const float SheetFlameMode=8,SheetBurstMode=9;"),"the drawer's sheet modes match the shader");
            Check(kit.Contains("NativeMuzzleFx.Look(\"machinegun\",out var look);NativeMuzzleFx.Turret(ref look);"),"the turrets' flash takes the turret sheet");
            foreach(var f in new[]{"Runtime/NativeWeaponFxDraw.cs","Core/NativeMuzzleFlash.cs","Resources/NativeWeaponFx.shader"})foreach(string pack in new[]{"Kripto","kripto","MuzzleFlash1","Flame3"})Check(!R(f).Contains(pack),"no pack names in "+f+": "+pack);
        }

        // B. Blood: the pack's splat textures, one quad a splat, today's colours, drying and pool; fans without them and on the
        // low graphics tier.
        private static void Blood(string root,Func<string,string> R)
        {
            int[] kinds=new int[NativeBloodSplatLook.Kinds];int mirrored=0,n=0;
            for(uint seed=1;seed<400;seed++)
            {
                float w=.012f+.05f*NativeBloodRules.Hash(seed,0,5),g=1+1.2f*NativeBloodRules.Hash(seed,1,5),l=w*g;
                NativeBloodSplatLook.Pick(seed,l,w,out int kind,out float spin,out bool mirror,out float along,out float across,out float ahead);
                NativeBloodSplatLook.Pick(seed,l,w,out int kind2,out float spin2,out bool mirror2,out float along2,out _,out _);
                Check(kind==kind2&&spin==spin2&&mirror==mirror2&&along==along2,"a splat's look is a function of its seed");
                Check(kind>=0&&kind<NativeBloodSplatLook.Kinds&&along>0&&across>0&&ahead>=0&&ahead<along,"seed "+seed+": a quad on the splat");
                Check(g>NativeBloodSplatLook.StreakAbove?kind==NativeBloodSplatLook.Streak:g>NativeBloodSplatLook.SplashAbove?kind==NativeBloodSplatLook.Splash:kind!=NativeBloodSplatLook.Streak,"seed "+seed+": the kind follows how it grazed ("+g+")");
                if(kind==NativeBloodSplatLook.Round)Check(ahead==0&&spin>=0&&spin<MathF.PI*2+1e-4f&&along>=l*2.5f&&across>=w*2.5f,"a round splat centred, spun at random, about its blob's size and its spatter");
                else Check(MathF.Abs(spin)<.26f&&ahead>along*.3f&&along>=l*1.9f,"a splash or streak laid along its travel, its impact end on the splat");
                kinds[kind]++;if(mirror)mirrored++;n++;
            }
            Check(kinds.All(k=>k>10)&&mirrored>n/4&&mirrored<n*3/4,"every kind shows; about half mirrored ("+string.Join(",",kinds)+")");
            NativeBloodSplatLook.Pick(9,.06f,.03f,out _,out _,out _,out float a1,out float c1,out _);NativeBloodSplatLook.Pick(9,.12f,.06f,out _,out _,out _,out float a2,out float c2,out _);
            Check(a2>a1*1.9f&&c2>c1*1.9f,"the quad grows with the splat (stretch by travel kept)");
            Check(NativeBloodSplatLook.Wet(0)==1&&NativeBloodSplatLook.Wet(600)<.01f&&NativeBloodSplatLook.Wet(30)<NativeBloodSplatLook.Wet(5),"the sheen dries off with the colour");
            var sel=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack_bank/selections/fx-blood.json"))).RootElement;
            Check(sel.GetProperty("textures").EnumerateArray().Select(t=>t.GetProperty("id").GetString()).OrderBy(x=>x).SequenceEqual(NativeBloodSplatLook.Ids.OrderBy(x=>x))&&!sel.TryGetProperty("bank",out _),"fx-blood.json lists exactly the splat ids");
            string blood=Code(R("Runtime/NativeBloodPresentation.cs")),shader=R("Resources/NativeBloodSplat.shader");
            Check(blood.Contains("bool sheets=!low&&SplatSheets();")&&blood.Contains("if(sheets)SheetSplat(s,a,now);else Splat(s,a,now,low);"),"the pack splats off the low tier only (the blob alone there), today's fans without them");
            Check(blood.Contains("if(round==null||splash==null||streak==null)return false;")&&blood.Contains("NativePackBanks.Texture(NativeBloodSplatLook.Ids[NativeBloodSplatLook.Round])")&&blood.Contains("var shader=Resources.Load<Shader>(\"NativeBloodSplat\");if(shader==null||!shader.isSupported)return false;"),"all three textures and the shader, or today's fans");
            Check(blood.Contains("Fan(o,u,v,n,Vector2.zero,1,rim);")&&blood.Contains("if(!low)Fan(o+n*.0005f,u,v,n,new Vector2(L*.12f,0),.58f,core);"),"today's fans unchanged");
            Check(blood.Contains("renderQueue=(int)RenderQueue.Transparent-60};")&&shader.Contains("Blend One OneMinusSrcAlpha")&&shader.Contains("ZWrite Off")&&shader.Contains("UNITY_APPLY_FOG_COLOR(")&&shader.Contains("GammaToLinearSpace(i.color.rgb)"),"premultiplied, no depth write, fogged, where today's splats draw, today's colours");
            var block=Code(Between(shader,"BLOOD SPLAT SHARED BEGIN","BLOOD SPLAT SHARED END"));
            Check(block.Length>100&&!block.Contains("tex2D")&&!block.Contains("mul(")&&Regex.Matches(block,@"(?<![\w.])\d+(?![\w.])").Count==0,"the splat block: float literals only, no texture reads");
            Check(Regex.IsMatch(R("Resources/NativeBloodSplat.shader.meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),"the splat shader has its .meta");
            var sheet=Between(blood,"private void SheetSplat(","private void Fan(");Check(!Regex.IsMatch(sheet,@"new\s+(?!Vector4\b|Vector3\b|Color\b)\w+")&&!blood.Contains("Random("),"the pack splats allocate nothing per splat and use no clock-free randomness");
        }

        // C. Surface impacts for every surface: the kinds by material, block and map; a kind the bake lacks plays the stone entry
        // (its own hole from the bank where there is one); the feature needs only the stone entry.
        private static void Impacts(string root,Func<string,string> R)
        {
            int S(string m,string map="arena",string block="b-1",float up=0)=>NativeSurfaceImpacts.Surface(m,map,block,up);
            Check(NativeSurfaceImpacts.Names.Length==NativeSurfaceImpacts.Kinds&&NativeSurfaceImpacts.Names.Distinct().Count()==NativeSurfaceImpacts.Kinds&&NativeSurfaceImpacts.Names[NativeSurfaceImpacts.Stone]=="stone","a bake entry name per kind, stone first");
            Check(S("brick")==NativeSurfaceImpacts.Brick&&S("masonry")==NativeSurfaceImpacts.Brick&&S("terracotta")==NativeSurfaceImpacts.Brick&&S("red-brick-wall")==NativeSurfaceImpacts.Brick,"brick props take brick");
            Check(S("glass")==NativeSurfaceImpacts.Glass&&S("shop-window")==NativeSurfaceImpacts.Glass,"glass and windows take glass");
            Check(S("metal")==NativeSurfaceImpacts.Metal&&S("steel")==NativeSurfaceImpacts.Metal&&S("rebar")==NativeSurfaceImpacts.Metal&&S("wood")==NativeSurfaceImpacts.Wood&&S("timber")==NativeSurfaceImpacts.Wood,"metal and wood as before");
            Check(S("snow")==NativeSurfaceImpacts.Dirt&&S("gravel")==NativeSurfaceImpacts.Dirt&&S("dirt")==NativeSurfaceImpacts.Dirt&&S("rubble")==NativeSurfaceImpacts.Rock&&S("water")==NativeSurfaceImpacts.Water,"loose ground dirt, rubble rock, water water");
            Check(S("foundation","moonlit","floor",1)==NativeSurfaceImpacts.Dirt&&S("foundation","frozen","floor",1)==NativeSurfaceImpacts.Dirt&&S("foundation","outpost","floor",1)==NativeSurfaceImpacts.Dirt,"the snow and gravel maps' ground kicks up dirt");
            Check(S("foundation","citadel","floor",1)==NativeSurfaceImpacts.Rock&&S("foundation","canal","floor",1)==NativeSurfaceImpacts.Wood&&S("foundation","foundry","floor",1)==NativeSurfaceImpacts.Metal&&S("foundation","arena","floor",1)==NativeSurfaceImpacts.Stone,"the other maps' ground as their footsteps");
            Check(S("foundation","moonlit","ledge",0)==NativeSurfaceImpacts.Stone&&S("stone","moonlit")==NativeSurfaceImpacts.Stone&&S("stone","citadel")==NativeSurfaceImpacts.Rock&&S(null)==NativeSurfaceImpacts.Stone,"a built foundation's side and stone blocks stone (rock on the stone maps)");
            Check(S("stone","moonlit","interior-house-3-facade-7",0)==NativeSurfaceImpacts.Plaster&&S("foundation","moonlit","interior-house-3-floor-2",1)==NativeSurfaceImpacts.Stone&&S("wood","moonlit","interior-house-3-facade-7",0)==NativeSurfaceImpacts.Wood,"inside a building: plaster walls, stone floors, its own wood and metal");
            Check(NativeSurfaceImpacts.Kind("brick")==NativeSurfaceImpacts.Stone,"the impact sound's three kinds unchanged");
            var sel=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack_bank/selections/fx-impacts.json"))).RootElement;
            var holes=Enumerable.Range(0,NativeSurfaceImpacts.Kinds).Select(NativeSurfaceImpacts.HoleTexture).Where(x=>x!=null).OrderBy(x=>x).ToArray();
            Check(holes.Length==3&&sel.GetProperty("textures").EnumerateArray().Select(t=>t.GetProperty("id").GetString()).OrderBy(x=>x).SequenceEqual(holes),"fx-impacts.json lists exactly the holes the bank stands in for");
            Check(!NativeSurfaceImpacts.Holes(NativeSurfaceImpacts.Water)&&Enumerable.Range(0,NativeSurfaceImpacts.Kinds).Count(NativeSurfaceImpacts.Holes)==NativeSurfaceImpacts.Kinds-1,"water leaves no hole");
            string impacts=Code(R("Runtime/NativeSurfaceImpactPresentation.cs"));
            Check(impacts.Contains("var stone=holder.Find(NativeSurfaceImpacts.Names[NativeSurfaceImpacts.Stone]);if(stone==null)return false;")&&impacts.Contains("var entry=holder.Find(NativeSurfaceImpacts.Names[k]);bool own=entry!=null;if(!own)entry=stone;")&&!impacts.Contains("if(entry==null)return false;"),"only the stone entry is required; a missing kind plays stone");
            Check(impacts.Contains("var lists=new List<LiveImpact>[NativeSurfaceImpacts.Kinds];")&&impacts.Contains("int kind=NativeSurfaceImpacts.Surface(SurfaceMaterialOf(arena,e.SurfaceId),arena.MapId,e.SurfaceId,normal.y);"),"the pools by kind count; the kind by material, map, block and normal");
            Check(impacts.Contains("NativePackBanks.Texture(NativeSurfaceImpacts.HoleTexture(k))")&&impacts.Contains("var hole=!own&&decals[k]!=null?"),"the bank's hole only where the bake lacks the kind");
            var doc=File.ReadAllText(Path.Combine(root,"../../docs/PACK-EFFECTS.md"));var request=Between(doc,"Local session: add these kinds to the impacts bake","\n## ");
            foreach(var (kind,prefab) in new[]{("brick","BrickImpact"),("glass","GlassImpact"),("dirt","DirtImpact"),("rock","RockImpact"),("plaster","PlasterImpact"),("water","WaterImpact")})Check(request.Contains("VisualImpacts/"+kind)&&request.Contains(prefab+".prefab"),"the bake request names "+kind+" and "+prefab);
        }

        // D. Afterimages: the motion triggers (with the pack look only) and the trails look on the copies.
        private static void Trails(string root,Func<string,string> R)
        {
            Actor A(string spec="sword")=>new Actor{SourceSpec=spec,Hp=100};
            var roll=A();roll.RollAt=1;roll.VisualRollDuration=.3f;
            Check(NativeAfterimages.Trigger(roll,1.1f,-100,-100,-100,out double k0)==NativeAfterimages.Roll&&k0==1,"the roll is still a trigger, keyed by its start");
            var sa=A();sa.SprintAttack=true;sa.SwingAt=2;sa.SwingUntil=2.25f;
            Check(NativeAfterimages.Trigger(sa,2.1f,-100,-100,-100,out double k1)==NativeAfterimages.SprintAttack&&NativeAfterimages.Trigger(sa,2.3f,-100,-100,-100,out _)==NativeAfterimages.None&&NativeAfterimages.Trigger(sa,1.9f,-100,-100,-100,out _)==NativeAfterimages.None,"the sprint attack's swing window");
            sa.SprintAttack=false;Check(NativeAfterimages.Trigger(sa,2.1f,-100,-100,-100,out _)==NativeAfterimages.None,"a plain swing leaves none");
            var jump=A();jump.AirJumpAt=3;Check(NativeAfterimages.Trigger(jump,3.1f,-100,-100,-100,out double k2)==NativeAfterimages.AirJump&&NativeAfterimages.Trigger(jump,3f+(float)NativeAfterimages.AirJumpWindow+.01f,-100,-100,-100,out _)==NativeAfterimages.None,"an air jump's first moment");
            Check(NativeAfterimages.Trigger(A(CyberLordSpec.Id),5.1f,-100,5,5.3f,out double k3)==NativeAfterimages.Dash&&NativeAfterimages.Trigger(A(CyberLordSpec.Id),5.4f,-100,5,5.3f,out _)==NativeAfterimages.None,"the Cyber Lord's dash window");
            var run=A();run.Sprinting=true;Check(NativeAfterimages.Trigger(run,6.5f,6,-100,-100,out double k4)==NativeAfterimages.Sprint&&NativeAfterimages.Trigger(run,7.1f,6,-100,-100,out _)==NativeAfterimages.None,"a sprint's first second");
            run.Sprinting=false;Check(NativeAfterimages.Trigger(run,6.5f,6,-100,-100,out _)==NativeAfterimages.None,"not once the sprint ends");
            Check(new[]{k0,k1,k2,k3,k4}.Distinct().Count()==5,"each trigger keys its own copies");
            foreach(var spec in new[]{"ansatsuken","siren"}){var x=A(spec);x.RollAt=1;x.VisualRollDuration=.3f;x.SprintAttack=true;x.SwingAt=1;x.SwingUntil=1.3f;x.AirJumpAt=1;x.Sprinting=true;Check(NativeAfterimages.Trigger(x,1.1f,1,1,1.3f,out _)==NativeAfterimages.None,spec+" keeps its own (no copies)");}
            var dead=A();dead.Hp=0;dead.AirJumpAt=1;Check(NativeAfterimages.Trigger(dead,1.1f,-100,-100,-100,out _)==NativeAfterimages.None,"not the dead");
            Check(NativeAfterimages.SpacingOf(NativeAfterimages.Roll,1)==NativeAfterimages.Spacing&&NativeAfterimages.SpacingOf(NativeAfterimages.Roll,.3f)==NativeAfterimages.ReducedSpacing&&NativeAfterimages.SpacingOf(NativeAfterimages.Sprint,1)>NativeAfterimages.SpacingOf(NativeAfterimages.AirJump,1)&&NativeAfterimages.SpacingOf(NativeAfterimages.AirJump,.3f)>NativeAfterimages.SpacingOf(NativeAfterimages.AirJump,1),"the roll's spacing as before; the sprint's wider; sparser with Reduced effects");
            var ghosts=new NativeAfterimages();Check(!ghosts.Due(1,System.Numerics.Vector3.Zero,7,7,1,1.2)&&!ghosts.Due(1,new System.Numerics.Vector3(1.1f,0,0),7,7.1,1,1.2)&&ghosts.Due(1,new System.Numerics.Vector3(1.2f,0,0),7,7.1,1,1.2),"a trigger's own spacing");
            Check(NativeAfterimages.LifeShare(0)==0&&NativeAfterimages.LifeShare(NativeAfterimages.Life)==1&&NativeAfterimages.LifeShare(-1)==0&&Math.Abs(NativeAfterimages.LifeShare(NativeAfterimages.Life/2)-.5f)<1e-5f,"the copy's life share");
            var sel=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack_bank/selections/fx-trails.json"))).RootElement;
            Check(sel.GetProperty("textures").EnumerateArray().Select(t=>t.GetProperty("id").GetString()).SequenceEqual(new[]{NativeAfterimages.RampId})&&!sel.TryGetProperty("bank",out _),"fx-trails.json holds the ramp, texture only");
            string fx=Code(R("Runtime/NativeWeaponFxPresentation.cs")),body=Code(R("Runtime/NativeVisualBodyPresentation.cs")),shader=R("Resources/NativeAfterimage.shader");
            var pack=Between(fx,"private bool PackTrails","private string PackModel(");
            Check(pack.Contains("if(LowGraphics)return false;")&&pack.Contains("var ramp=NativePackBanks.Texture(NativeAfterimages.RampId);var shader=ramp!=null?Resources.Load<Shader>(\"NativeAfterimage\"):null;")&&pack.Contains("Shader.SetGlobalTexture(AfterimageRampId,ramp);packTrailsState=1;"),"the pack look only off the low tier, with the ramp and the shader");
            var loop=Between(fx,"private void Afterimages(Snapshot s,float time)","private bool PackAfterimage(");
            Check(loop.Contains("int motionLeft=pack?NativeAfterimages.MotionPerFrame:0;")&&loop.Contains("if(trigger==NativeAfterimages.None&&motionLeft>0)")&&loop.Contains("NativeAfterimages.MotionRange*NativeAfterimages.MotionRange")&&loop.Contains("else motionLeft--;"),"the motion triggers only with the pack look, a few a frame, near the camera");
            Check(loop.Contains("if(trigger==NativeAfterimages.Roll){if(!afterimages.Due(a.Id,at,a.RollAt,time,q))continue;}")&&loop.Contains("afterimagePack[slot]=pack;")&&loop.Contains("if(afterimagePack[k]&&PackAfterimage(k,pose,tint,opacity,time))continue;"),"the roll as before; a copy keeps the look it was taken with");
            Check(Between(fx,"private bool PackAfterimage(","/// <summary>The charge orb").Contains("VisualEchoDraw(afterimageEchoes[k],\"NativeAfterimage\",tint,opacity,")&&body.Contains("visualPackEchoBlock.SetVector(VisualGhostAccent,accent);"),"the model's echoes take the look with their life in the accent tint");
            Check(shader.Contains("Blend One One")&&shader.Contains("ZWrite Off")&&shader.Contains("Offset [_EchoOffset],[_EchoOffset]")&&shader.Contains("UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)")&&shader.Contains("#pragma multi_compile_instancing")&&shader.Contains("\"Queue\"=\"Transparent+1\""),"additive, instanced, offset like the ghosts");
            var block=Code(Between(shader,"AFTERIMAGE SHARED BEGIN","AFTERIMAGE SHARED END"));
            Check(block.Length>100&&!block.Contains("tex2D")&&!block.Contains("mul(")&&Regex.Matches(block,@"(?<![\w.])\d+(?![\w.])").Count==0,"the afterimage block: float literals only, no texture reads");
            Check(Regex.IsMatch(R("Resources/NativeAfterimage.shader.meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),"the afterimage shader has its .meta");
            foreach(var f in new[]{"Runtime/NativeWeaponFxPresentation.cs","Resources/NativeAfterimage.shader","Core/NativeWeaponFx.cs"})foreach(string name in new[]{"Kronnect","StylizedRamp","TrailsFX"})Check(!R(f).Contains(name),"no pack names in "+f+": "+name);
        }
    }
}
#endif
