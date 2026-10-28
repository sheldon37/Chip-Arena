#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The effect upgrades from the owner's packs (owner 2026-10-28; docs/PACK-EFFECTS.md): the guns' pack muzzle flashes. Each
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
    }
}
#endif
