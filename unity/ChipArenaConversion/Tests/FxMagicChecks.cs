#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Realistic magic (Claude MG1, 2026-11-25; effects plan phase 3, Core NativeMagicFxPack): the atlases' layout is the tool's and fits its
    // atlases; the heads, trails and hits are pure functions (finite, deterministic, inside their flipbooks, capped, fewer on Reduced
    // effects and the low tier, nothing outside their lives); a trail lies on the path the bolt flew and lives out after it; the school's
    // colour carries through; the darkness layers keep the void orb's place and clock; the loaders fall back to today's look. The look
    // itself is proven by the Editor sheets and clips (DEV_LOG), not here.
    internal static class FxMagicChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Fx magic: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c>=0?l.Substring(0,c):l;}));
        private static readonly NativeMagicSchool[] Schools={NativeMagicSchool.Fire,NativeMagicSchool.Water,NativeMagicSchool.Lightning,NativeMagicSchool.Leet};
        // The staffs' colours (GameContent weapons) as the shot's halo colour, linear.
        private static Vector3 Halo(NativeMagicSchool s)=>NativeMagicMath.Lin(s==NativeMagicSchool.Fire?0xff7d3e:s==NativeMagicSchool.Water?0x65d7ed:s==NativeMagicSchool.Lightning?0xc2a3ff:0x52ff83);
        private static float Speed(NativeMagicSchool s)=>s==NativeMagicSchool.Fire?100:s==NativeMagicSchool.Water?110:s==NativeMagicSchool.Lightning?120:19;
        public static void Run(string root)
        {
            count=0;Layout(root);Heads();Trails();Hits();Darkness();Sources(root);Counts();
            Console.WriteLine("PASS fx magic: "+count+" checks (atlas layout, heads, trails, hits, darkness, loaders; no Unity render)");
        }
        private static bool Good(in NativeMagicSprite s)
        {
            bool sheet=s.Look==NativeMagicLook.CodeGlow||s.Look==NativeMagicLook.CodeStreak||s.Look==NativeMagicLook.Drop?s.Sheet<0:s.Sheet>=0&&s.Sheet<NativeMagicFxPack.Subs&&s.Frame>=0&&s.Frame<NativeMagicFxPack.Frames(s.Sheet);
            return sheet&&s.Size>0&&float.IsFinite(s.Size)&&float.IsFinite(s.At.X+s.At.Y+s.At.Z)&&s.Opacity>0&&s.Opacity<=1.0001f&&float.IsFinite(s.Roll)&&s.Heat>=0&&float.IsFinite(s.Heat+s.Gain)
                &&s.Occlusion>=0&&s.Occlusion<=1&&s.Hot>=0&&s.Hot<=1&&float.IsFinite(s.Segment.X+s.Segment.Y+s.Segment.Z)&&float.IsFinite(s.Tint.X+s.Tint.Y+s.Tint.Z);
        }
        private static bool Same(NativeMagicList a,NativeMagicList b)=>a.Count==b.Count&&Enumerable.Range(0,a.Count).All(i=>a.Items[i].At==b.Items[i].At&&a.Items[i].Frame==b.Items[i].Frame&&a.Items[i].Size==b.Items[i].Size&&a.Items[i].Hex==b.Items[i].Hex);

        // ---- the atlases: Core's table is the tool's layout, each flipbook inside its atlas, none overlapping ----
        private static void Layout(string root)
        {
            Check(NativeMagicFxPack.Sheets.Length==NativeMagicFxPack.Subs&&NativeMagicFxPack.Subs==7&&NativeMagicFxPack.Subs<=8,"seven flipbooks, at most the shader's eight slots");
            for(int k=0;k<NativeMagicFxPack.Subs;k++)
            {
                var s=NativeMagicFxPack.Sheets[k];Check(s.Atlas>=0&&s.Atlas<NativeMagicFxPack.Atlases&&s.X>=0&&s.Y>=0&&s.X+s.W<=1.0001f&&s.Y+s.H<=1.0001f&&s.Columns>0&&s.Rows>0,"flipbook "+k+" inside its atlas");
                Check(NativeMagicFxPack.Frames(k)<=255,"flipbook "+k+" frames fit the shader's encoding (x 256)");
                for(int j=0;j<k;j++){var o=NativeMagicFxPack.Sheets[j];if(o.Atlas!=s.Atlas)continue;Check(s.X>=o.X+o.W-1e-4f||o.X>=s.X+s.W-1e-4f||s.Y>=o.Y+o.H-1e-4f||o.Y>=s.Y+s.H-1e-4f,"flipbooks "+j+" and "+k+" do not overlap");}
            }
            Check(Math.Abs(NativeMagicFxPack.Aspect(NativeMagicFxPack.Flame)-.5f)<1e-4f&&Math.Abs(NativeMagicFxPack.Aspect(NativeMagicFxPack.Plasma)-.5f)<1e-4f&&Math.Abs(NativeMagicFxPack.Aspect(NativeMagicFxPack.Arcs)-2)<1e-4f&&Math.Abs(NativeMagicFxPack.Aspect(NativeMagicFxPack.Waves)-2)<1e-4f&&Math.Abs(NativeMagicFxPack.Aspect(NativeMagicFxPack.Smoke)-1)<1e-4f&&Math.Abs(NativeMagicFxPack.Aspect(NativeMagicFxPack.Tongues)-.5f)<1e-4f,"frame aspects: flames, tongues and plasma tall, arcs and waves wide");
            // The tool writes the same layout: atlas, quadrant (1024 px) and source, in Core's flipbook order.
            var tool=File.ReadAllText(Path.Combine(root,"tools/fx_bank/magic.py"));var rows=Regex.Matches(tool,@"\((\d), (\d), (\d), '(\w+)\.png', '(\w+)'\),\s+# (\d) ");
            Check(rows.Count==NativeMagicFxPack.Subs,"the tool lays out "+rows.Count+" flipbooks");
            string[] sources={"Fire2","Fog5","PlasmaBall1","Lightning2","EnergyBall","Fog1","Fire4"};
            foreach(Match m in rows)
            {
                int k=int.Parse(m.Groups[6].Value),atlas=int.Parse(m.Groups[1].Value),qx=int.Parse(m.Groups[2].Value),qy=int.Parse(m.Groups[3].Value);var s=NativeMagicFxPack.Sheets[k];
                float w=NativeMagicFxPack.AtlasWidth[atlas],h=NativeMagicFxPack.AtlasHeight[atlas];
                Check(s.Atlas==atlas&&Math.Abs(s.X-qx*1024/w)<1e-4f&&Math.Abs(s.Y-qy*1024/h)<1e-4f&&Math.Abs(s.W-1024/w)<1e-4f&&Math.Abs(s.H-1024/h)<1e-4f&&m.Groups[4].Value==sources[k],"flipbook "+k+" where the tool writes it");
            }
            Check(tool.Contains("SIZES = {0: (2048, 2048), 1: (2048, 2048)}")&&NativeMagicFxPack.AtlasWidth.SequenceEqual(new[]{2048,2048})&&NativeMagicFxPack.AtlasHeight.SequenceEqual(new[]{2048,2048}),"the atlases' sizes");
            Check(tool.Contains("'ChipArenaFx', 'Resources', 'ChipArenaMagicFx'")&&NativeMagicFxPack.Folder=="ChipArenaMagicFx"&&NativeMagicFxPack.Path(0)=="ChipArenaMagicFx/magic0"&&NativeMagicFxPack.Path(1)=="ChipArenaMagicFx/magic1","the effects bank's folder and names");
            Check(NativeMagicFxPack.Hex(Vector3.One)==0xffffff&&NativeMagicFxPack.Hex(NativeMagicMath.Lin(0xff7d3e))==0xff7d3e&&NativeMagicFxPack.Hex(NativeMagicMath.Lin(0x52ff83))==0x52ff83&&NativeMagicFxPack.Hex(Vector3.Zero)==0,"the tint hex round-trips the school colours");
        }

        // ---- heads ----
        private static void Heads()
        {
            var a=new NativeMagicList();var b=new NativeMagicList();var at=new Vector3(1,1.4f,2);var dir=Vector3.UnitX;
            foreach(var s in Schools)
            {
                for(float t=0;t<2;t+=.031f)
                {
                    a.Clear();b.Clear();NativeMagicFxPack.Head(a,s,at,dir,Speed(s),.2f,Halo(s),77,t,1,false);NativeMagicFxPack.Head(b,s,at,dir,Speed(s),.2f,Halo(s),77,t,1,false);
                    Check(a.Count>=3&&a.Count<=10,s+" head: "+a.Count+" sprites");Check(Same(a,b),s+" head deterministic");
                    for(int i=0;i<a.Count;i++){Check(Good(a.Items[i]),s+" head sprite "+i+" at "+t);Check(Vector3.Distance(a.Items[i].At,at)<=.2f*6+1e-3f,s+" head rides the bolt (its tongues lick back up to six radii)");}
                }
                a.Clear();b.Clear();NativeMagicFxPack.Head(a,s,at,dir,Speed(s),.2f,Halo(s),77,.4f,1,false);NativeMagicFxPack.Head(b,s,at,dir,Speed(s),.4f,Halo(s),77,.4f,1,false);
                Check(a.Count==b.Count&&Enumerable.Range(0,a.Count).All(i=>Math.Abs(b.Items[i].Size-2*a.Items[i].Size)<1e-3f),s+" head scales with the radius");
                // Still (before its second frame): no direction, nothing stretched.
                a.Clear();NativeMagicFxPack.Head(a,s,at,Vector3.Zero,0,.2f,Halo(s),77,.4f,1,false);Check(a.Count>=3&&Enumerable.Range(0,a.Count).All(i=>a.Items[i].Segment==Vector3.Zero&&Good(a.Items[i])),s+" head before it has a direction");
                // The school's colour: the glowing flipbooks are tinted in its hue (fire's flames run through the fire ramp instead).
                a.Clear();NativeMagicFxPack.Head(a,s,at,dir,Speed(s),.2f,Halo(s),77,.4f,1,false);
                if(s==NativeMagicSchool.Fire)Check(Enumerable.Range(0,a.Count).Count(i=>a.Items[i].Look==NativeMagicLook.Fire&&a.Items[i].Heat>1)>=2,"fire: hot flames");
                else Check(Enumerable.Range(0,a.Count).Any(i=>a.Items[i].Look==NativeMagicLook.Glow&&a.Items[i].Hex==NativeMagicFxPack.Hex(Halo(s))),s+": a flipbook in the school's own colour");
            }
            a.Clear();NativeMagicFxPack.Head(a,NativeMagicSchool.None,at,dir,100,.2f,Vector3.One,1,0,1,false);Check(a.Count==0,"no school, no head");
        }

        // ---- trails: a bolt flown at its speed, recorded at 60 fps ----
        private static NativeMagicTrails Fly(NativeMagicSchool s,float until,float seconds,out NativeMagicTrails.Trail trail,float fps=60,long key=4242)
        {
            var trails=new NativeMagicTrails();trail=null;var start=new Vector3(-20,1.3f,0);
            for(int f=0;f<=Math.Round(Math.Min(until,seconds)*fps);f++){float t=f/fps;trail=trails.Seen(key,s,start+Vector3.UnitX*(Speed(s)*t),t,.2f,Halo(s));}
            return trails;
        }
        private static void Trails()
        {
            foreach(var s in Schools)
            {
                var trails=Fly(s,.5f,.5f,out var trail);var a=new NativeMagicList();var b=new NativeMagicList();
                Check(trail.Samples>=2&&Math.Abs(trail.Travel-Speed(s)*.5f)<.01f*Speed(s)&&Vector3.Distance(trail.Dir,Vector3.UnitX)<1e-3f&&Math.Abs(trail.Speed-Speed(s))<.5f,s+": the path, its direction and speed");
                a.Clear();trails.Draw(a,.5f,1,false);var again=Fly(s,.5f,.5f,out _);b.Clear();again.Draw(b,.5f,1,false);
                Check(a.Count>0&&Same(a,b),s+" trail deterministic ("+a.Count+" sprites)");Check(a.Count<=90,s+" trail capped: "+a.Count);
                float head=-20+Speed(s)*.5f;
                for(int i=0;i<a.Count;i++){Check(Good(a.Items[i]),s+" trail sprite "+i);Check(a.Items[i].At.X<=head+.3f,s+" trail sprites lie behind the head");}
                var low=new NativeMagicList();trails.Draw(low,.5f,1,true);var reduced=new NativeMagicList();trails.Draw(reduced,.5f,.3f,false);
                Check(low.Count<a.Count&&reduced.Count<a.Count,s+" fewer on the low tier ("+low.Count+") and Reduced effects ("+reduced.Count+")");
                // The bolt gone (not seen this frame): its trail lives out, then its slot frees.
                var gone=new NativeMagicList();trails.Draw(gone,.5f+NativeMagicFxPack.TrailLife(s)*.5f,1,false);Check(gone.Count>0&&!trail.Live,s+": a trail lives out after its bolt");
                gone.Clear();trails.Draw(gone,.5f+NativeMagicFxPack.TrailLife(s)+.1f,1,false);Check(gone.Count==0&&!trail.Used,s+": then nothing, its slot free");
                // A rewound clock (the killcam's replica, a new arena) starts over.
                var rw=Fly(s,.5f,.5f,out var t2);var r=new NativeMagicList();rw.Draw(r,.1f,1,false);Check(r.Count==0&&!t2.Used,s+": a rewind clears the trails");
            }
            // 60 bolts at once: the pool reuses its oldest (no allocation, no growth).
            var many=new NativeMagicTrails();for(int k=0;k<60;k++)many.Seen(k,NativeMagicSchool.Fire,new Vector3(k,1,0),0,.2f,Vector3.One);
            Check(many.Count==NativeMagicTrails.MaxTrails,"the trail pool keeps its size");
            // A slow L33t orb recorded at 30 fps still has a path and a trail.
            var slow=Fly(NativeMagicSchool.Leet,.8f,.8f,out var lt,30);var l=new NativeMagicList();slow.Draw(l,.8f,1,false);Check(lt.Samples>=2&&l.Count>0,"a slow bolt at 30 fps");
        }

        // ---- hits ----
        private static void Hits()
        {
            var a=new NativeMagicList();var b=new NativeMagicList();var at=new Vector3(3,1.2f,-1);
            foreach(var s in Schools)
            {
                int peak=0,full=0,reduced=0;
                for(float t=-.05f;t<NativeMagicFxPack.ImpactLife+.1f;t+=.02f)
                {
                    a.Clear();b.Clear();NativeMagicFxPack.Impact(a,s,at,-Vector3.UnitX,9001,t,1.3f,Halo(s),1,false);NativeMagicFxPack.Impact(b,s,at,-Vector3.UnitX,9001,t,1.3f,Halo(s),1,false);
                    if(t<0||t>=NativeMagicFxPack.ImpactLife)Check(a.Count==0,s+" no hit outside its life ("+t+")");
                    Check(Same(a,b),s+" hit deterministic");peak=Math.Max(peak,a.Count);full+=a.Count;
                    for(int i=0;i<a.Count;i++){Check(Good(a.Items[i]),s+" hit sprite "+i+" at "+t);Check(Vector3.Distance(a.Items[i].At,at)<4,s+" hit sprite near the point");}
                    var r=new NativeMagicList();NativeMagicFxPack.Impact(r,s,at,-Vector3.UnitX,9001,t,1.3f,Halo(s),.3f,false);reduced+=r.Count;
                    var p=new NativeMagicList();NativeMagicFxPack.Impact(p,s,at,Vector3.Zero,9001,t,1.1f,Halo(s),1,true);for(int i=0;i<p.Count;i++)Check(Good(p.Items[i]),s+" pilot hit sprite");
                }
                Check(peak>=4&&peak<=45,s+" hit peak "+peak+" sprites");Check(reduced<full,s+" fewer on Reduced effects");
                a.Clear();NativeMagicFxPack.Impact(a,s,at,-Vector3.UnitX,9001,.05f,1.3f,Halo(s),1,false);Check(a.Count>0,s+" the hit shows at once");
            }
            a.Clear();NativeMagicFxPack.Impact(a,NativeMagicSchool.None,at,Vector3.UnitY,1,.1f,1,Vector3.One,1,false);Check(a.Count==0,"no school, no hit");
        }

        // ---- darkness: the void orb's realistic smoke and black flames ----
        private static void Darkness()
        {
            var a=new NativeMagicList();var at=new Vector3(0,1,0);
            NativeMagicFxPack.DarkSmoke(a,at,NativeMagicMath.Lin(NativeVoidOrbFx.SmokeHex),.9f,.3f,.4f,123);
            Check(a.Count==1&&a.Items[0].Look==NativeMagicLook.Smoke&&a.Items[0].At==at&&Good(a.Items[0])&&a.Items[0].Hex==NativeMagicFxPack.Hex(NativeMagicMath.Lin(NativeVoidOrbFx.SmokeHex)),"a dark puff is one smoke wisp in the puff's own place and colour");
            Check(a.Items[0].Gain<.2f,"the dark smoke stays as dark as the toon puff's albedo");
            for(float t=0;t<2;t+=.05f){a.Clear();NativeMagicFxPack.DarkFlames(a,at,new Vector3(0,0,26),.2f,77,t,1,false);Check(a.Count>0&&a.Count<=6&&Enumerable.Range(0,a.Count).All(i=>Good(a.Items[i])&&Vector3.Distance(a.Items[i].At,at)<1.2f),"black flames ride the orb");}
            for(float t=-.1f;t<1.2f;t+=.03f)
            {
                a.Clear();NativeMagicFxPack.DarkBurst(a,at,Vector3.UnitY,true,55,t,1,1,false);
                if(t<NativeVoidOrbFx.ImplodeSeconds||t>=NativeVoidOrbFx.ImplodeSeconds+.8f)Check(a.Count==0,"no black flames during the implosion or after the burst ("+t+")");
                for(int i=0;i<a.Count;i++)Check(Good(a.Items[i])&&Vector3.Distance(a.Items[i].At,at)<3,"a burst flame near the impact");
            }
            a.Clear();NativeMagicFxPack.DarkBurst(a,at,Vector3.UnitY,true,55,.2f,1,1,false);var low=new NativeMagicList();NativeMagicFxPack.DarkBurst(low,at,Vector3.UnitY,true,55,.2f,1,1,true);Check(a.Count>0&&low.Count<a.Count,"the burst burns, fewer on the low tier");
        }

        // ---- the runtime: loads once, falls back, hooks where the magic sheet's look did ----
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            var draw=Code(R("Runtime/NativeVfxDraw.cs"));var shader=R("Resources/NativeVfx.shader");var pack=Code(R("Runtime/NativeMagicPackPresentation.cs"));var fx=Code(R("Runtime/NativeMagicFxPresentation.cs"));
            Check(draw.Contains("Resources.Load<Texture2D>(NativeMagicFxPack.Path(k))")&&draw.Contains("if(magicRead)return;magicRead=true;")&&draw.Contains("MagicSheets();if(magicLoaded)MagicBind(billboardMaterial);"),"the atlases load by name once a process; both or none");
            Check(draw.Contains("public bool PackMagic=>magicLoaded&&NativeMagicFxPack.On;")&&draw.Contains("if(list==null||!PackMagic)return;")&&draw.Contains("if(!PackMagic)return false;"),"without the atlases nothing is queued and the darkness keeps its toon puffs");
            Check(shader.Contains("_MagicSheet0(")&&shader.Contains("_MagicSheet1(")&&shader.Contains("tex2Dgrad(_MagicSheet0")&&shader.Contains("tex2Dgrad(_MagicSheet1")&&shader.Contains("float4 _MagicRect[8],_MagicGrid[8];"),"the shader reads both atlases and the flipbook table");
            Check(shader.Contains("else if(mode>15.5)")&&shader.Contains("c=mode<16.5?MagicGlow(t,i.color,i.alt.rgb):FxPack(mode-4.0,0.0,t,i.color,i.alt.rgb,light,i.flash.w);")&&shader.Contains("else if(mode>12.5)"),"modes 16-18 ahead of 13-15");
            Check(Regex.Matches(shader,@"\bsampler2D\b[^;]*;").Cast<Match>().Sum(m=>m.Value.Split(',').Length)+1<=16,"the shader stays within sixteen samplers (with the depth texture)");
            Check(pack.Contains("private bool PackLooks=>vfx!=null&&(vfx.PackSprites||vfx.PackMagic);")&&pack.Contains("private bool RealMagic=>vfx!=null&&vfx.PackMagic;"),"the realistic look rides the magic sheet's hooks (the model gives way, the generic shards too)");
            Check(pack.Contains("if(RealMagic||!(age>=0&&age<p.Life))return;")&&pack.Contains("magicList.Clear();magicTrails.Draw(magicList,now,q,low);"),"the trail is the recorded path's, not the pack trail sprites");
            var wfx=Code(R("Runtime/NativeWeaponFxPresentation.cs"));
            Check(wfx.Contains("float ribbon=school!=NativeMagicSchool.None&&RealMagic?NativeMagicFxPack.RibbonLife:(float)NativeTrailFx.RibbonLife;")&&NativeMagicFxPack.RibbonLife>0&&NativeMagicFxPack.RibbonLife<NativeTrailFx.RibbonLife,"a realistic bolt keeps a shorter ribbon; every other shot today's");
            Check(pack.Contains("magicTrails.Clear();}")&&pack.Contains("magicHits[i].Born=-100;magicTrails.Clear();}"),"a new arena or a reset clears the trails");
            Check(fx.Contains("if(!vfx.MagicSmoke(U(at),C(albedo),opacity,size,u,seed))vfx.Smoke(U(at),C(albedo),opacity,size,u,seed,floor);")&&fx.Contains("DarkFlames(now);")&&fx.Contains("if(vfx==null||!vfx.PackMagic||magicScene==null)return;"),"darkness: realistic smoke and black flames only with the atlases");
            // Nothing reaches a rule: the recipes read no arena, match, network or damage type.
            var core=Code(R("Core/NativeMagicFxPack.cs"));Check(!core.Contains("NativeArena")&&!core.Contains("NativeMatch")&&!core.Contains("Network")&&!core.Contains("Damage")&&!core.Contains("Random"),"the recipes read no rule and draw no random number");
            foreach(var f in new[]{"Core/NativeMagicFxPack.cs","Runtime/NativeVfxDraw.cs","Runtime/NativeMagicPackPresentation.cs","Runtime/NativeMagicFxPresentation.cs","Resources/NativeVfx.shader"})
                foreach(var name in new[]{"kripto","Kripto","KriptoFX","RFX4","Realistic Effects Pack"})Check(!R(f).Contains(name),"no pack publisher or product named in "+f);
        }

        // The counts against today's (informational; DEV_LOG): today's energy bolt draws its core (2 quads), glow and ribbon, and NativeTrailFx's
        // soft sprites (a sprite every .16 m x speed/60, living .08-.2 s); the realistic look draws these sprites per bolt and per hit.
        private static void Counts()
        {
            foreach(var s in Schools)
            {
                var trails=Fly(s,.5f,.5f,out var trail);var t=new NativeMagicList();trails.Draw(t,.5f,1,false);var h=new NativeMagicList();NativeMagicFxPack.Head(h,s,trail.Path[trail.Samples-1],trail.Dir,trail.Speed,.2f,Halo(s),4242,.5f,1,false);
                int peak=0;for(float a=0;a<NativeMagicFxPack.ImpactLife;a+=.02f){var l=new NativeMagicList();NativeMagicFxPack.Impact(l,s,Vector3.Zero,Vector3.UnitY,7,a,1.3f,Halo(s),1,false);peak=Math.Max(peak,l.Count);}
                float spacing=NativeTrailFx.Spacing(1,false)*NativeTrailFx.SpacingAt(Speed(s));int today=(int)Math.Round(.14f*Speed(s)/spacing);
                Console.WriteLine("  counts "+s+" at "+Speed(s)+" m/s: head "+h.Count+" sprites, trail "+t.Count+" (today about "+today+" soft sprites + core, glow, ribbon), hit peak "+peak);
            }
            var d=new NativeMagicList();NativeMagicFxPack.DarkFlames(d,Vector3.Zero,new Vector3(0,0,26),.2f,1,.3f,1,false);int burst=0;for(float a=0;a<1;a+=.02f){var l=new NativeMagicList();NativeMagicFxPack.DarkBurst(l,Vector3.Zero,Vector3.UnitY,true,5,a,1,1,false);burst=Math.Max(burst,l.Count);}
            Console.WriteLine("  counts darkness: +"+d.Count+" black-flame sprites per orb, +"+burst+" at a burst's peak; each toon puff becomes one smoke wisp (same count)");
        }
    }
}
#endif
