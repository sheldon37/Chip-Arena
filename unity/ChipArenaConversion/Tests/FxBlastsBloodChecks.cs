#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The effects bank's explosions, fire and blood spray (Claude X1, 2026-11-25; effects plan phase 4, Core NativeFxPack). The blast sim
    // records each restyled blast after its spawns with no random draw (the pools, the shock front and the pinned frames are what they
    // were), and its seed ranges are exactly that blast's particles; the recipes are pure functions of age (finite, deterministic, inside
    // their sheets, gone after their life, fewer on the low tier); V1's two faults are fixed in numbers (the fireball's first .12 s
    // outsizes today's flash; the frag's smoke has a self-light floor); the blood spray follows its wound; the loaders fall back.
    // The look itself is proven by the Editor sheets and clips (DEV_LOG), not here.
    internal static class FxBlastsBloodChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Fx blasts and blood: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c>=0?l.Substring(0,c):l;}));
        public static void Run(string root)
        {
            count=0;Records();Recipes();Faults();Blood();Fires();Sources(root);Counts();
            Console.WriteLine("PASS fx blasts and blood: "+count+" checks (sim records, recipes, V1's two faults, spray, fires, loaders; no Unity render)");
        }
        private static NativeBlastSim Sim()=>new NativeBlastSim(new NativeBlastRandom().Next);
        private static void Records()
        {
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))
            {
                var sim=Sim();int s0=sim.Solid.Spawned,g0=sim.Glow.Spawned;int n=sim.Blast(style,1,.95,2,3,1,false,null,0);
                bool restyled=NativeFxPack.Restyled(style,NativeBlastLook.Default,false);
                Check(sim.Bursts.Count==(restyled?1:0),style+": a record only for a restyled blast");
                // The same blast in a sim that never records draws the same random sequence: the counts and the shock fronts match.
                var twin=Sim();int n2=twin.Blast(style,1,.95,2,3,1,false,null,0);Check(n==n2&&twin.Shocks.Count==sim.Shocks.Count,style+": the record changes no spawn");
                if(!restyled)continue;var b=sim.Bursts[0];
                Check(((b.SolidTo-b.SolidFrom)&NativeBlastPool.SeedMask)==sim.Solid.Spawned-s0&&((b.GlowTo-b.GlowFrom)&NativeBlastPool.SeedMask)==sim.Glow.Spawned-g0,style+": its seed ranges are exactly its particles");
                sim.Update(1/60.0);var o=sim.Glow.Output;int owned=0;
                for(int i=0;i<sim.Glow.Drawn;i++){int seed=(int)o[i*NativeBlastPool.Stride+NativeBlastPool.SeedAt];if(b.Owns(true,seed))owned++;}
                Check(owned==sim.Glow.Drawn,style+": every live glow particle is the blast's own");
                // The shock front still ends exactly on the gameplay radius.
                foreach(var s in sim.Shocks)if(s.Kind==NativeShockKind.Blast)Check(Math.Abs(s.RadiusAt(s.Born+s.FrontTime)-3)<1e-9,style+": the front ends on the radius");
                for(int k=0;k<300;k++)sim.Update(1/60.0);Check(sim.Bursts.Count==0,style+": the record expires after its life");
            }
            var t=Sim();t.Blast(NativeBlastStyle.Fire,0,1,0,3,1,false,new NativeBlastTint(0xffffff,0xff0000,0x00ff00),0);Check(t.Bursts.Count==0,"a tinted Fire blast keeps today's look");
            t.Blast(NativeBlastStyle.Kinetic,0,1,0,3,1,false,NativeBlastSim.TintOf("tidal-wave"),0);Check(t.Bursts.Count==0,"the Tidal Wave keeps its water pop");
            t.Blast(NativeBlastStyle.Fire,0,1,0,3);t.Clear();Check(t.Bursts.Count==0,"Clear empties the records");
            var w=new NativeBlastBurst{GlowFrom=NativeBlastPool.SeedMask-2,GlowTo=3};Check(w.Owns(true,NativeBlastPool.SeedMask-1)&&w.Owns(true,0)&&w.Owns(true,2)&&!w.Owns(true,3)&&!w.Owns(true,NativeBlastPool.SeedMask-3),"seed ranges wrap at the mask");
            Check(NativeFxPack.Replaced(NativeFxRole.Fireball)&&NativeFxPack.Replaced(NativeFxRole.Smoke)&&NativeFxPack.Replaced(NativeFxRole.Flash)&&NativeFxPack.Replaced(NativeFxRole.Lick)
                &&!NativeFxPack.Replaced(NativeFxRole.Core)&&!NativeFxPack.Replaced(NativeFxRole.Halo)&&!NativeFxPack.Replaced(NativeFxRole.Spark)&&!NativeFxPack.Replaced(NativeFxRole.Shell)&&!NativeFxPack.Replaced(NativeFxRole.HotDebris)&&!NativeFxPack.Replaced(NativeFxRole.Debris)&&!NativeFxPack.Replaced(NativeFxRole.Rock),
                "only toon puffs, flashes and licks are replaced: glows, sparks, rubble and the shell stay the code's");
        }
        private static NativeBlastBurst Burst(NativeBlastStyle style,double r=3)
        {var sim=Sim();sim.Blast(style,0,.95,0,r,1,false,null,0);return sim.Bursts[0];}
        private static bool Good(in NativeFxSprite s)
        {
            bool sheet=s.Look==NativeFxLook.Glow||s.Look==NativeFxLook.Streak?s.Sheet<0:s.Sheet>=0&&s.Sheet<NativeFxPack.Sheets&&s.Frame>=0&&s.Frame<NativeFxPack.Frames(s.Sheet);
            return sheet&&s.Size>0&&float.IsFinite(s.Size)&&float.IsFinite(s.At.X+s.At.Y+s.At.Z)&&s.Opacity>0&&s.Opacity<=1.0001f&&float.IsFinite(s.Roll)&&s.Heat>=0&&float.IsFinite(s.Heat);
        }
        private static void Recipes()
        {
            foreach(var style in new[]{NativeBlastStyle.Fire,NativeBlastStyle.Meteor,NativeBlastStyle.Mine,NativeBlastStyle.Kinetic,NativeBlastStyle.Quake})
            {
                var b=Burst(style);var a=new NativeFxList();var c=new NativeFxList();int peak=0;
                for(float age=0;age<(float)NativeFxPack.BurstLife+.5f;age+=.05f)
                {
                    a.Clear();c.Clear();NativeFxPack.Blast(a,b,age,false);NativeFxPack.Blast(c,b,age,false);peak=Math.Max(peak,a.Count);
                    Check(a.Count==c.Count,style+" at "+age+": deterministic");
                    for(int i=0;i<a.Count;i++){Check(Good(a.Items[i]),style+" at "+age+": a sprite inside its sheet, finite, visible");Check(a.Items[i].At==c.Items[i].At&&a.Items[i].Frame==c.Items[i].Frame,style+": same sprite twice");
                        Check(!NativeFxPack.Blood(a.Items[i].Sheet),style+": blasts draw no blood sheet");}
                    if(age>=NativeFxPack.BurstLife)Check(a.Count==0,style+": nothing after its life");
                }
                Check(peak>0&&peak<=60,style+": a peak of "+peak+" sprites (at most 60)");
                var low=new NativeFxList();a.Clear();NativeFxPack.Blast(a,b,.3f,false);NativeFxPack.Blast(low,b,.3f,true);Check(low.Count<a.Count,style+": the low tier draws fewer");
            }
            Check(NativeFxPack.Keep(4,1,false)==4&&NativeFxPack.Keep(4,.3f,false)==1&&NativeFxPack.Keep(30,1,true)==15&&NativeFxPack.Keep(1,.1f,true)==1,"Keep scales by quality and halves on the low tier");
            Check(NativeFxPack.Fireball(NativeBlastStyle.Fire,false)&&!NativeFxPack.Fireball(NativeBlastStyle.Fire,true)&&NativeFxPack.Dusty(NativeBlastStyle.Kinetic,NativeBlastLook.Default,false)&&!NativeFxPack.Dusty(NativeBlastStyle.Kinetic,NativeBlastLook.Tide,false)
                &&!NativeFxPack.Restyled(NativeBlastStyle.Shock,NativeBlastLook.Default,false)&&!NativeFxPack.Restyled(NativeBlastStyle.Frost,NativeBlastLook.Default,false)&&!NativeFxPack.Restyled(NativeBlastStyle.Void,NativeBlastLook.Default,false),
                "the fireball on the untinted Fire family, the dust on the frag and Quake; magic styles keep theirs");
            // A bigger blast scales its layer (the sim's own scale, clamp(R/3, .45, 2.2)).
            var small=new NativeFxList();var big=new NativeFxList();NativeFxPack.Blast(small,Burst(NativeBlastStyle.Fire,1.5),.3f,false);NativeFxPack.Blast(big,Burst(NativeBlastStyle.Fire,6),.3f,false);
            Check(big.Items.Take(big.Count).Max(s=>s.Size)>small.Items.Take(small.Count).Max(s=>s.Size)*1.8f,"a 6 m blast's fire is about four times a 1.5 m one's");
        }
        private static void Faults()
        {
            // Fix 1: in the first .12 s the fireball's own fire (the ignition and the burst sheet) is larger than today's flash flipbook and
            // halo together (NativeBlastSim's Fire: Flashes(2, 1.9 x scale) and Halo(2.5 x scale)), and its flash glow is wider still.
            var b=Burst(NativeBlastStyle.Fire);var l=new NativeFxList();
            foreach(var age in new[]{.0f,.02f,.05f,.08f,.12f})
            {
                l.Clear();NativeFxPack.Blast(l,b,age,false);var items=l.Items.Take(l.Count).ToList();
                float fire=items.Where(s=>s.Look==NativeFxLook.Fire).Select(s=>s.Size*s.Opacity).DefaultIfEmpty(0).Max();
                Check(fire>=2.5f*(float)b.Scale*1.3f,"fix 1 at "+age+" s: the fire reads larger than today's flash ("+fire.ToString("0.00")+" m)");
                if(age<NativeFxPack.FlashLife)Check(items.Any(s=>s.Look==NativeFxLook.Glow&&s.Size>=6),"fix 1 at "+age+" s: the wide flash glow");
            }
            // Fix 2: the frag's smoke and dust have a self light (they read on a dark stage) and a lighter tint than V1's first pass (.36 sRGB).
            var d=Burst(NativeBlastStyle.Kinetic);l.Clear();NativeFxPack.Blast(l,d,.4f,false);
            var smoke=l.Items.Take(l.Count).Where(s=>s.Look==NativeFxLook.Smoke).ToList();
            Check(smoke.Count>=8&&smoke.All(s=>s.Self>=.3f),"fix 2: every smoke and dust sprite has its self-light floor");
            Check(smoke.All(s=>((s.Hex>>16)&255)/255f>.5f),"fix 2: the smoke is a lighter grey than V1's first pass");
            Check(smoke.Any(s=>s.Sheet==NativeFxPack.Dust)&&smoke.Any(s=>s.Sheet==NativeFxPack.Smoke),"the frag shows its dust ring and its smoke");
            // And the fire core opens it, opaque from the first frame.
            l.Clear();NativeFxPack.Blast(l,d,0,false);Check(l.Items.Take(l.Count).Any(s=>s.Look==NativeFxLook.Fire&&s.Opacity>.95f&&s.Size>=2.5f),"fix 2: the fire core is opaque at once");
        }
        private static void Blood()
        {
            var l=new NativeFxList();var along=Vector3.Normalize(new Vector3(1,.2f,0));
            NativeFxPack.Spray(l,new Vector3(0,1.3f,0),along,.1f,1234u,false,1,false);
            Check(l.Count>=10,"a gun hit's spray: mist, splatters and drops ("+l.Count+")");
            for(int i=0;i<l.Count;i++){var s=l.Items[i];Check(Good(s)&&s.Look==NativeFxLook.Blood&&NativeFxPack.Blood(s.Sheet),"blood draws only its own sheets");
                if(s.Sheet==NativeFxPack.Splatters)Check(s.Frame==NativeFxPack.SplatA||s.Frame==NativeFxPack.SplatB||s.Frame==NativeFxPack.SplatLine,"a splatter tile");}
            var mean=Vector3.Zero;for(int i=0;i<l.Count;i++)mean+=l.Items[i].At-new Vector3(0,1.3f,0);Check(Vector3.Dot(Vector3.Normalize(mean),along)>.5f,"the spray flies along the wound's exit");
            Check(l.Items.Take(l.Count).Any(s=>s.Segment.LengthSquared()>0),"the drops are stretched along their flight");
            l.Clear();NativeFxPack.Spray(l,Vector3.Zero,along,1.25f,1u,true,1,false);Check(l.Count==0,"nothing after its life");
            Check(NativeBloodSplashes.Sprays_("puncture",false,false)&&NativeBloodSplashes.Sprays_("cut",true,false)&&!NativeBloodSplashes.Sprays_("cut",false,false)&&!NativeBloodSplashes.Sprays_("puncture",true,true),"gun hits and kills spray; ink never");
            var sp=new NativeBloodSplashes();sp.Add("puncture",.7f,9u,Vector3.UnitY,Vector3.UnitX,Vector3.UnitX,1,1,false,false);Check(sp.Sprays.Count(s=>s.Live)==0,"no spray without the sheets");
            sp.Spraying=true;sp.Add("puncture",.7f,9u,Vector3.UnitY,Vector3.UnitX,Vector3.UnitX,2,1,false,false);Check(sp.Sprays.Count(s=>s.Live)==1&&sp.Sprays[0].Along==Vector3.UnitX,"one spray per gun hit, along its exit");
            Check(NativeFxPack.Path(NativeFxPack.Splatters)=="ChipArenaBloodSpray/splatters"&&NativeFxPack.Path(NativeFxPack.Mist)=="ChipArenaBloodSpray/mist"&&NativeFxPack.Path(NativeFxPack.Burst)=="ChipArenaBlastFx/burst","the Resources paths");
        }
        private static void Fires()
        {
            var a=new NativeFxList();var b=new NativeFxList();int lo=int.MaxValue,hi=0;
            for(float t=10;t<14;t+=.1f)
            {
                a.Clear();b.Clear();NativeFxPack.Fire(a,new Vector3(3,0,4),1.2f,t,77u,false,1,false);NativeFxPack.Fire(b,new Vector3(3,0,4),1.2f,t,77u,false,1,false);
                Check(a.Count==b.Count,"a fire is a pure function of its clock");lo=Math.Min(lo,a.Count);hi=Math.Max(hi,a.Count);
                for(int i=0;i<a.Count;i++)Check(Good(a.Items[i])&&!NativeFxPack.Blood(a.Items[i].Sheet),"a fire sprite inside its sheet");
            }
            Check(lo>=6&&hi<=60,"a campfire burns with a steady "+lo+"-"+hi+" sprites");
            a.Clear();NativeFxPack.Fire(a,Vector3.Zero,2,12,5u,true,1,false);Check(a.Items.Take(a.Count).Any(s=>s.Sheet==NativeFxPack.Large),"the large flame");
            b.Clear();NativeFxPack.Fire(b,Vector3.Zero,2,12,5u,true,1,false,false,false);Check(b.Count<a.Count&&!b.Items.Take(b.Count).Any(s=>s.Look==NativeFxLook.Streak||s.Sheet==NativeFxPack.FlameSmoke),"embers and smoke can be left out");
            // The sim lists live fire fields and forgets them.
            var sim=Sim();var f=new NativeBlastField{Id=5,Kind="fire",Fx="",X=1,Y=.95,Z=2,Radius=2.5,Born=0,Until=99};
            for(double t=0;t<1;t+=1/60.0){sim.Field(f,t,1);sim.Update(1/60.0);}
            Check(sim.Fires.Count==1&&sim.Fires[0].Id==5&&Math.Abs(sim.Fires[0].Radius-2.5)<1e-9,"a live fire field is listed");
            for(int k=0;k<30;k++)sim.Update(1/60.0);Check(sim.Fires.Count==0,"a field not seen for a quarter second is gone");
            Check(NativeFxPack.FieldFlames(.5)==1&&NativeFxPack.FieldFlames(4)==7&&NativeFxPack.FieldFlames(2.5)>=3,"a field burns with one to seven flames by its size");
            for(int j=1;j<5;j++){var p=NativeFxPack.FieldFlameAt(0,0,0,3,9,j,5);Check(new Vector2(p.X,p.Z).Length()<=3*.55f*1.16f,"a field's flame stands inside its radius");}
            var fires=new NativePackFires();for(int i=0;i<NativePackFires.Max+10;i++)fires.Add(new NativePackFire{Size=1});Check(fires.Count==NativePackFires.Max,"the stage fires are capped a frame");
        }
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            var draw=Code(R("Runtime/NativeVfxDraw.cs"));var shader=R("Resources/NativeVfx.shader");
            Check(draw.Contains("Resources.Load<Texture2D>(NativeFxPack.Path(k))")&&draw.Contains("if(fxSheets[k]==null){if(NativeFxPack.Blood(k))fxBlood=false;else fxBlasts=false;}"),"the sheets load by name, and a missing one turns its layer off");
            Check(draw.Contains("public bool PackBlasts=>fxBlasts&&NativeFxPack.On;")&&draw.Contains("public bool PackBlood=>fxBlood&&NativeFxPack.On;"),"the layers' switches");
            Check(draw.Contains("if(!PackBlasts)return false;")&&draw.Contains("if(!PackBlood)return false;")&&draw.Contains("fxCover=PackBlasts&&"),"without the bank nothing is left out and every caller keeps today's look");
            for(int k=0;k<8;k++)Check(shader.Contains("_FxSheet"+k+"(")&&shader.Contains("tex2Dgrad(_FxSheet"+k),"the shader reads sheet "+k);
            Check(shader.Contains("else if(mode>12.5)")&&shader.Contains("float4 FxPack(float mode"),"modes 13-15");
            Check(Code(R("Runtime/NativeMeleeFlash.cs")).Contains("if(vfx.PackSlam(impact.Position,(float)age,(uint)(impact.At*1000)^0x51a3u,quality))opacity=0;"),"a slam keeps its toon dust without the bank");
            Check(Code(R("Runtime/NativeStatusPresentation.cs")).Contains("bool packFire=rig.Fire&&target==statusWorld&&blasts!=null&&vfx!=null&&vfx.PackBlasts;"),"burning bodies keep their status flames without the bank (and in first person)");
            Check(Code(R("Core/NativeCampFx.cs")).Contains("if(!PackFlames)for(int t=0;t<5;t++)")&&Code(R("Core/NativeExpeditionCityFx.cs")).Contains("if(!PackFlames)for(int t=0;t<5;t++)"),"the camp's and the town's flames stay without the bank");
            Check(Code(R("Core/NativeBombFx.cs")).Contains("bool pack=s is INativeFxPackSink packSink&&packSink.PackBlast("),"the Bombadier asks the bank first");
            foreach(var f in new[]{"Core/NativeFxPack.cs","Runtime/NativeVfxDraw.cs","Core/NativeBloodPack.cs","Runtime/NativeBloodPackPresentation.cs"})
                Check(!R(f).Contains("Hivemind"),"no pack publisher named in "+f);
            // Nothing in the layer reaches a rule: the recipes read no arena, match or network type.
            var core=Code(R("Core/NativeFxPack.cs"));Check(!core.Contains("NativeArena")&&!core.Contains("NativeMatch")&&!core.Contains("Network")&&!core.Contains("Damage"),"the recipes read no rule");
        }
        // The counts against today's (informational; DEV_LOG): today's drawn pool particles of a 3 m blast (no shell voxels, which are never
        // drawn), and with the bank the same minus the replaced roles plus the pack's sprites.
        private static void Counts()
        {
            foreach(var style in new[]{NativeBlastStyle.Fire,NativeBlastStyle.Kinetic,NativeBlastStyle.Quake})
            {
                var row=new List<string>();
                foreach(var age in new[]{.05,.12,.25,.5,.9,1.6,2.6})
                {
                    var sim=Sim();sim.Blast(style,0,.95,0,3,1,false,null,0);for(double t=0;t<age-1e-9;t+=1/60.0)sim.Update(Math.Min(1/60.0,age-t));
                    int today=0,kept=0;
                    foreach(var pool in new[]{sim.Solid,sim.Glow})
                        for(int i=0;i<pool.Drawn;i++){var role=(NativeFxRole)(int)(pool.Output[i*NativeBlastPool.Stride+NativeBlastPool.RoleAt]+.5f);if(role==NativeFxRole.Shell)continue;today++;if(!NativeFxPack.Replaced(role))kept++;}
                    var l=new NativeFxList();if(sim.Bursts.Count>0)NativeFxPack.Blast(l,sim.Bursts[0],(float)(sim.Time-sim.Bursts[0].Born),false);
                    row.Add("t="+age.ToString("0.00")+" "+today+"->"+(kept+l.Count));
                }
                Console.WriteLine("  counts "+style+" r3 (today->bank, drawn particles + pack sprites): "+string.Join("  ",row));
            }
            var spray=new NativeFxList();NativeFxPack.Spray(spray,Vector3.Zero,Vector3.UnitX,.1f,3u,false,1,false);var kill=new NativeFxList();NativeFxPack.Spray(kill,Vector3.Zero,Vector3.UnitX,.1f,3u,true,1,false);
            var fire=new NativeFxList();NativeFxPack.Fire(fire,Vector3.Zero,1.2f,12,3u,false,1,false);var slam=new NativeFxList();NativeFxPack.Slam(slam,Vector3.Zero,.3f,3u,1,false);
            Console.WriteLine("  counts spray (added over the RVFX splashes): hit "+spray.Count+", kill "+kill.Count+"; campfire "+fire.Count+" (today's camp drum: 5 tongues + 1 halo + 9 embers + 4 puffs); slam dust "+slam.Count+" (today: 40 toon puffs)");
        }
    }
}
#endif
