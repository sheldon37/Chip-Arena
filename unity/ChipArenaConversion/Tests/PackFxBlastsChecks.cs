#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The pack pass for blasts, smoke, fire and the magic bolts (owner 2026-10-28, the licensed packs as in-place upgrades;
    // docs/PACK-EFFECTS.md). Core: the schools a bolt maps to, the heads, trail sprites and hit bursts (tiles inside the magic
    // sheet, sizes and colours finite and additive, deterministic, fewer on Reduced effects and the low tier, nothing past a
    // burst's life, no allocation once warm). The selections: the sheet ids the game loads and the tile order Core names.
    // Source scans pin the renderer's pack sprite mode and its fallbacks (no sheet: today's toon sheet, energy core, model and
    // soft sprites). The look itself is not proven here (offline WebGL renders only; no Unity, GPU or Windows run).
    internal static class PackFxBlastsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Pack fx blasts: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c>=0?l.Substring(0,c):l;}));
        private static readonly NativeMagicSchool[] Schools={NativeMagicSchool.Fire,NativeMagicSchool.Water,NativeMagicSchool.Lightning,NativeMagicSchool.Leet};
        private static NativeShotView Bolt(string spec)=>new NativeShotView{Kind=NativeShotKind.Bullet,Id=3,Spec=spec,Class="magic",Radius=.14,VX=30,WeaponColor=0xff7d3e};
        public static void Run(string root)
        {
            count=0;Schools_(root);Sprites();Selections(root);Sources(root);
            Console.WriteLine("PASS pack fx blasts: "+count+" checks (schools, heads, trails, hit bursts, selections, renderer pins; no Unity render)");
        }
        private static void Schools_(string root)
        {
            Check(NativeMagicPackFx.School("fire")==NativeMagicSchool.Fire&&NativeMagicPackFx.School("water")==NativeMagicSchool.Water&&NativeMagicPackFx.School("lightning")==NativeMagicSchool.Lightning&&NativeMagicPackFx.School("leet")==NativeMagicSchool.Leet,"the four schools");
            Check(NativeMagicPackFx.School("darkness")==NativeMagicSchool.None&&NativeMagicPackFx.School("rifle")==NativeMagicSchool.None&&NativeMagicPackFx.School(null)==NativeMagicSchool.None,"darkness (NativeMagicFx's void orb) and the rest have none");
            foreach(var spec in new[]{"fire","water","lightning","leet"})
            {
                var v=Bolt(spec);Check(NativeMagicPackFx.Bolt(v)==NativeMagicPackFx.School(spec)&&NativeTrailFx.Energy(v),spec+": a primary bolt with the energy trail takes the look");
                var beam=v;beam.SwordBeam=true;var arcade=v;arcade.Arcade=1;var siren=v;siren.Siren=true;var ability=v;ability.Kind=NativeShotKind.Ability;ability.Ability="ball-lightning";var ranged=v;ranged.Class="ranged";
                Check(NativeMagicPackFx.Bolt(beam)==NativeMagicSchool.None&&NativeMagicPackFx.Bolt(arcade)==NativeMagicSchool.None&&NativeMagicPackFx.Bolt(siren)==NativeMagicSchool.None&&NativeMagicPackFx.Bolt(ability)==NativeMagicSchool.None&&NativeMagicPackFx.Bolt(ranged)==NativeMagicSchool.None,spec+": not a sword beam, arcade orb, the Siren's, an ability shot or a gun round");
            }
            Check(NativeMagicPackFx.Bolt(Bolt("darkness"))==NativeMagicSchool.None&&NativeMagicFx.Owns(Bolt("darkness")),"the darkness orb stays NativeMagicFx's");
        }
        private static bool Good(in NativePackSprite s)=>s.Hot>=0&&s.Hot<=1&&s.Hot==NativeMagicPackFx.HotOf(s.Tile)&&s.Tile>=0&&s.Tile<NativeMagicPackFx.Tiles&&s.Size>0&&float.IsFinite(s.Size)&&float.IsFinite(s.Roll)&&float.IsFinite(s.At.X+s.At.Y+s.At.Z)&&s.Color.A==0&&s.Color.R>=0&&s.Color.G>=0&&s.Color.B>=0&&float.IsFinite(s.Color.R+s.Color.G+s.Color.B);
        private static void Sprites()
        {
            var a=new NativePackSprite[NativeMagicPackFx.ImpactMax];var b=new NativePackSprite[NativeMagicPackFx.ImpactMax];var halo=new FxColor(1,.3f,.08f);var at=new Vector3(1,2,3);
            foreach(var s in Schools)
            {
                // Heads: a few sprites round the bolt, scaling with its radius, the same for the same clock.
                for(double t=0;t<1;t+=.037)
                {
                    int n=NativeMagicPackFx.Head(s,at,.14f,halo,77,t,a);Check(n>=1&&n<=NativeMagicPackFx.HeadMax,s+" head count at "+t);
                    for(int i=0;i<n;i++)Check(Good(a[i])&&Vector3.Distance(a[i].At,at)<1e-4f&&a[i].Size<.14f*9,s+" head sprite "+i+" at the bolt, at most 9 radii");
                    int m=NativeMagicPackFx.Head(s,at,.14f,halo,77,t,b);Check(m==n&&Enumerable.Range(0,n).All(i=>a[i].Size==b[i].Size&&a[i].Tile==b[i].Tile&&a[i].Roll==b[i].Roll),s+" head deterministic");
                    int big=NativeMagicPackFx.Head(s,at,.28f,halo,77,t,b);Check(big==n&&Math.Abs(b[0].Size-2*a[0].Size)<1e-4f,s+" head scales with the radius");
                }
                // Trails: a tile of the school, a size factor, the colour white-hot at birth and gone at the end.
                for(long k=0;k<40;k++)
                {
                    NativeMagicPackFx.Trail(s,123,k,out int tile,out float scale,out float roll,out float spin);NativeMagicPackFx.Trail(s,123,k,out int tile2,out float scale2,out _,out _);
                    Check(tile>=0&&tile<NativeMagicPackFx.Tiles&&scale>.2f&&scale<2&&float.IsFinite(roll)&&Math.Abs(spin)<=4&&tile==tile2&&scale==scale2,s+" trail sprite "+k);
                    var c0=NativeMagicPackFx.TrailColor(s,halo,.03,tile);var c1=NativeMagicPackFx.TrailColor(s,halo,1,tile);
                    Check(c0.A==0&&c1.Max==0&&float.IsFinite(c0.Max),s+" trail colour: additive, gone at the end of its life");
                }
                // Hits: sprites while the burst lives, none outside it, fewer when reduced, deterministic.
                int full=0,reduced=0;
                for(double t=-.05;t<NativeMagicPackFx.ImpactLife+.1;t+=.01)
                {
                    int n=NativeMagicPackFx.Impact(s,at,Vector3.UnitY,4242,t,1,halo,1,false,a);
                    if(t<0||t>=NativeMagicPackFx.ImpactLife)Check(n==0,s+" no burst outside its life ("+t+")");
                    Check(n<=NativeMagicPackFx.ImpactMax,s+" burst within its cap");for(int i=0;i<n;i++)Check(Good(a[i])&&Vector3.Distance(a[i].At,at)<4,s+" burst sprite "+i+" near the hit");
                    int again=NativeMagicPackFx.Impact(s,at,Vector3.UnitY,4242,t,1,halo,1,false,b);Check(again==n&&Enumerable.Range(0,n).All(i=>a[i].At==b[i].At&&a[i].Tile==b[i].Tile),s+" burst deterministic");
                    full+=n;reduced+=NativeMagicPackFx.Impact(s,at,Vector3.UnitY,4242,t,1,halo,.3f,false,b);
                    int pilot=NativeMagicPackFx.Impact(s,at,Vector3.Zero,4242,t,.85f,halo,1,true,b);for(int i=0;i<pilot;i++)Check(Good(b[i]),s+" pilot burst sprite");
                }
                Check(full>0&&reduced<full,s+" burst: Reduced effects draws fewer pieces ("+reduced+" of "+full+")");
                Check(NativeMagicPackFx.Impact(s,at,Vector3.UnitY,4242,.1,1,halo,1,false,a)>0,s+" burst shows");
            }
            // The white masks (hexagon, data bits, the flare flipbook) take the shot's colour; the coloured art keeps its white core.
            Check(NativeMagicPackFx.HotOf(NativeMagicPackFx.LeetRing)<.5f&&NativeMagicPackFx.HotOf(NativeMagicPackFx.LeetBits)<.5f&&NativeMagicPackFx.HotOf(NativeMagicPackFx.Burst+NativeMagicPackFx.BurstFrames-1)<.5f&&NativeMagicPackFx.HotOf(NativeMagicPackFx.FireOrb)==1&&NativeMagicPackFx.HotOf(NativeMagicPackFx.Glint)==1,"white masks are tinted, coloured art keeps its white cores");
            Check(NativeMagicPackFx.Head(NativeMagicSchool.None,at,.14f,halo,1,0,a)==0&&NativeMagicPackFx.Impact(NativeMagicSchool.None,at,Vector3.UnitY,1,.1,1,halo,1,false,a)==0,"no school, no sprites");
            // No allocation once warm (the runtime calls these every frame).
            for(int w=0;w<3;w++)foreach(var s in Schools){NativeMagicPackFx.Head(s,at,.14f,halo,5,w*.1,a);NativeMagicPackFx.Impact(s,at,Vector3.UnitY,9,w*.1,1,halo,1,false,a);NativeMagicPackFx.Trail(s,5,w,out _,out _,out _,out _);NativeMagicPackFx.TrailColor(s,halo,.5,0);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int k=0;k<200;k++)foreach(var s in Schools){NativeMagicPackFx.Head(s,at,.14f,halo,5,k*.013,a);NativeMagicPackFx.Impact(s,at,Vector3.UnitY,9,k*.002,1,halo,1,false,a);NativeMagicPackFx.Trail(s,5,k,out _,out _,out _,out _);NativeMagicPackFx.TrailColor(s,halo,k/200.0,0);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"heads, trails and bursts allocate nothing per frame ("+used+" bytes)");
        }
        private static void Selections(string root)
        {
            string Sel(string f)=>File.ReadAllText(Path.Combine(root,"tools/pack_bank/selections",f));
            var id=new Regex("^[a-z0-9][a-z0-9-]{0,79}$");
            using(var blasts=JsonDocument.Parse(Sel("fx-blasts.json")))
            {
                var sel=blasts.RootElement;Check(!sel.TryGetProperty("bank",out _)&&!sel.TryGetProperty("props",out _),"fx-blasts: texture-only, no bank");
                var sheet=sel.GetProperty("sheets")[0];Check(sheet.GetProperty("id").GetString()==NativeVfxTextures.PackToonId&&id.IsMatch(NativeVfxTextures.PackToonId)&&sheet.GetProperty("kind").GetString()=="toon","fx-blasts: the toon sheet's id is the one the renderer loads");
                Check(sheet.GetProperty("puffs").GetArrayLength()==NativeVfxTextures.ToonPuffs&&sheet.GetProperty("shapes").GetArrayLength()==NativeVfxTextures.ToonColumns*NativeVfxTextures.ToonRows-NativeVfxTextures.ToonPuffs,"fx-blasts: 16 puffs, then the 16 flash, crescent and spark tiles");
                var shapes=sheet.GetProperty("shapes");Check(shapes[NativeVfxTextures.DropTile-NativeVfxTextures.ToonPuffs].TryGetProperty("procedural",out var p)&&p.GetString()=="drop","fx-blasts: the droplet tile is Core's own teardrop");
                Check(Enumerable.Range(0,NativeVfxTextures.FlashFrames).All(k=>shapes[NativeVfxTextures.FlashFirst-NativeVfxTextures.ToonPuffs+k].GetProperty("frame").GetInt32()==k),"fx-blasts: the flash flipbook's frames in order");
            }
            using(var magic=JsonDocument.Parse(Sel("fx-magic.json")))
            {
                var sel=magic.RootElement;Check(!sel.TryGetProperty("bank",out _),"fx-magic: texture-only, no bank");
                var sheet=sel.GetProperty("sheets")[0];var grid=sheet.GetProperty("grid");var tiles=sheet.GetProperty("tiles");
                Check(sheet.GetProperty("id").GetString()==NativeMagicPackFx.SheetId&&id.IsMatch(NativeMagicPackFx.SheetId)&&sheet.GetProperty("kind").GetString()=="atlas","fx-magic: the atlas id Core names");
                Check(grid[0].GetInt32()==NativeMagicPackFx.Columns&&grid[1].GetInt32()==NativeMagicPackFx.Rows&&tiles.GetArrayLength()==NativeMagicPackFx.Tiles&&NativeMagicPackFx.Tiles<=NativeMagicPackFx.Columns*NativeMagicPackFx.Rows,"fx-magic: the grid and tile count Core names");
                string Path_(int t)=>tiles[t].GetProperty("path").GetString();
                Check(Path_(NativeMagicPackFx.FireOrb).Contains("fire_orb")&&Path_(NativeMagicPackFx.FireRing).Contains("fire_ring")&&Path_(NativeMagicPackFx.WaterDrop).Contains("water_drop")&&Path_(NativeMagicPackFx.LightningBall).Contains("lightning_ball")&&Path_(NativeMagicPackFx.LightningArc).Contains("lightning_new_arc.png")&&Path_(NativeMagicPackFx.LeetRing).Contains("hexagon"),"fx-magic: the named tiles are where Core reads them");
                Check(Enumerable.Range(0,NativeMagicPackFx.ArcFrames).All(k=>tiles[NativeMagicPackFx.LightningArcs+k].GetProperty("frame").GetInt32()==k)&&Enumerable.Range(0,NativeMagicPackFx.BurstFrames).All(k=>tiles[NativeMagicPackFx.Burst+k].GetProperty("frame").GetInt32()==2*k),"fx-magic: the flipbooks' frames in order");
            }
        }
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string vfx=R("Resources/NativeVfx.shader"),draw=Code(R("Runtime/NativeVfxDraw.cs")),wfx=Code(R("Runtime/NativeWeaponFxPresentation.cs")),hits=Code(R("Runtime/NativeMagicPackPresentation.cs")),shots=Code(R("Runtime/NativeShotPresentation.cs"));
            // The shader: mode 12 faces the camera at its roll, reads its tile (outside the branches) and premultiplies.
            Check(draw.Contains("public const float PackMode=12;")&&vfx.Contains("if(mode>11.5){float2 local=v.shape.xy*hw;")&&vfx.Contains("o.toon.zw=(float2(pc,pr)+v.shape.xy*0.48+0.5)/_VfxPackGrid.xy;"),"pack sprites: mode 12, camera-facing at their roll, inset in their tile");
            Check(vfx.Contains("float4 pack=tex2D(_VfxPack,i.toon.zw);")&&vfx.Contains("else if(mode>11.5)c=VfxPackSprite(pack,i.color,i.sun.w);\n                else c=VfxToonSprite(toon,aa,i.color);"),"the magic sheet is read outside the branches; modes 10 and 11 keep the toon sprite");
            Check(vfx.Contains("float4 VfxPackSprite(float4 tex, float4 color, float hot)")&&vfx.IndexOf("float4 VfxPackSprite(",StringComparison.Ordinal)<vfx.IndexOf("---- VFX SHARED END",StringComparison.Ordinal),"the pack sprite's maths sit in the GLSL-shared block");
            // The renderer: the sheets by id from the pack banks, the procedural toon sheet first (the fallback), no release.
            Check(draw.Contains("packSheet=NativePackBanks.Texture(NativeMagicPackFx.SheetId);packToon=NativePackBanks.Texture(NativeVfxTextures.PackToonId);")&&draw.Contains("billboardMaterial.SetTexture(ToonId,toon);billboardMaterial.SetFloat(ReadyId,1);")&&draw.Contains("if(packToon!=null){packToon.wrapMode=TextureWrapMode.Clamp;packToon.filterMode=FilterMode.Trilinear;billboardMaterial.SetTexture(ToonId,packToon);}"),"the pack toon sheet replaces the procedural one only when it is there");
            Check(draw.Contains("{if(packSheet==null||!(size>1e-4f)||tile<0)return;Add(at,color,size,roll,0,0,PackMode,tile,new Vector4(0,0,0,hot));}")&&!draw.Contains("Release(packSheet)")&&!draw.Contains("Release(packToon)"),"Pack draws nothing without the sheet; the bank's textures are not released here");
            // The bolts: the pack head instead of the energy core, pack trail sprites, the model hidden, the hits.
            Check(wfx.Contains("var school=PackLooks?NativeMagicPackFx.Bolt(v):NativeMagicSchool.None;")&&wfx.Contains("if(school!=NativeMagicSchool.None)PackHead(school,at,e.Halo,e.Radius,e.Seed,now);\n            else NativeWeaponFxShapes.EnergyCore("),"a school bolt's head replaces its energy core; every other shot keeps it");
            Check(wfx.Contains("NativeTrailFx.Sprite(e.Seed,e.Emitted,e.Radius,out float life,out float size,out var offset,out var drift);")&&wfx.Contains("e.Emitted++;")&&wfx.Contains("if(p.Tile>=0&&PackLooks)PackTrailSprite(p,age);else NativeWeaponFxShapes.Sprite("),"trail sprites keep NativeTrailFx's spacing, life and drift; the soft sprite without the sheet");
            Check(wfx.Contains("MagicPackHits(arena,now,q,low);")&&wfx.Contains("if(shots!=null)shots.PackHeads=PackLooks;")&&wfx.Contains("ClearMagicPack();"),"the hit bursts run with the weapon effects; the model gives way only while the head draws");
            Check(shots.Contains("if(PackHeads&&NativeMagicPackFx.Bolt(v)!=NativeMagicSchool.None&&scene.Draws.Count>first)HidePackHead(first);")&&shots.Contains("if(i<packHiddenCount&&packHidden[i])continue;")&&shots.Contains("public bool PackHeads;"),"only a school bolt's own model is skipped (its chip parts stay)");
            Check(hits.Contains("private bool PackLooks=>vfx!=null&&vfx.PackSprites;")&&hits.Contains("new MagicHit[32]")&&hits.Contains("now-e.Time>=NativeMagicPackFx.ImpactLife")&&hits.Contains("(e.Detail==\"direct\"||e.Detail==\"reflected\")"),"hits: surface impacts and direct pilot hits, a ring of 32, for the burst's life");
            Check(!hits.Contains("new List<")&&!hits.Contains("ToArray(")&&!Regex.IsMatch(Code(R("Runtime/NativeMagicPackPresentation.cs")),@"new\s+NativePackSprite\[(?!NativeMagicPackFx\.ImpactMax\])"),"the hit and head drawing reuse their arrays");
        }
    }
}
#endif
