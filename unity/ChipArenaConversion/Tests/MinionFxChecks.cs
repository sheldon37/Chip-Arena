#if ARENA_TESTS
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Siren minion effects (Core/NativeMinionFx.cs, Runtime/NativeMinionFxPresentation.cs) against what the pinned
    // client/siren/minions draws (Tests/minion-fx-parity.json, exported by tools/export_minion_fx_fixtures.mjs): the six horde
    // variants' tongue anchors, faces and floors (NativeMinionFx.Generated.cs), each tongue's matrix over idle, vent-climb and
    // dying frames, fx.ts MinionFx cubes for the emergence and the burn-away at full and reduced quality, and the glow cells the
    // bank must carry as flickering emission codes (never as the flag in the albedo, the first port's blown-out cracks). Then
    // the Unity rules (flicker, the add-wide drop and its float-exact glow bits, the charred embers, phases, flare, char and
    // burn-away order, licks, hint pulse, haze, vent age, effect ranks, bands and reservations) and scans of the runtime,
    // shader and exporter hooks.
    internal static class MinionFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Minion fx: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-6){Check(Math.Abs(a-b)<=tolerance+2e-6*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        private static double N(JsonElement e,string name){var v=e.GetProperty(name);return v.ValueKind==JsonValueKind.Null?double.NaN:v.GetDouble();}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/minion-fx-parity.json")));var json=document.RootElement;
            Check(json.GetProperty("glowFlag").GetInt32()==16,"materials.ts GLOW_FLAG is 16");
            Variants(json.GetProperty("variants"));Tongues(json.GetProperty("tongueFrames"));Effects(json.GetProperty("fx"));Bank(root,json.GetProperty("variants"));Rules();Hooks(root);
            Console.WriteLine("PASS minion fx ("+count+" checks: horde tongue anchors, faces and floors; tongue sway, swell and flare over idle, vent and death frames; MinionFx emergence and burn-away cubes at full and reduced quality; bank glow codes and tongue pieces; flicker, drop bits, charred embers, phases, char and burn-away order, licks, hint, haze, effect ranks and bands; runtime, shader and exporter hooks)");
        }
        private static void Variants(JsonElement list)
        {
            Check(list.GetArrayLength()==NativeMinionFx.Variants,"six horde variants");int v=0;
            foreach(var x in list.EnumerateArray())
            {
                Check(NativeMinionFx.Cracked[v]==(x.GetProperty("face").GetString()=="cracked"),"variant "+v+" face");Near(NativeMinionFx.Floor[v],x.GetProperty("floor").GetDouble(),"variant "+v+" floor",1e-6);
                var tongues=x.GetProperty("tongues");Check(NativeMinionFx.TongueCount(v)==tongues.GetArrayLength(),"variant "+v+" tongue count");int k=0;
                foreach(var t in tongues.EnumerateArray())
                {
                    int i=NativeMinionFx.TongueStart[v]+k,o=i*9;Check(NativeMinionFx.TongueJoint[i]==t.GetProperty("joint").GetInt32()&&NativeMinionFx.TongueLayout[i]==t.GetProperty("layout").GetInt32(),"variant "+v+" tongue "+k+" joint and layout");
                    var p=A(t.GetProperty("pos"));var q=A(t.GetProperty("quat"));for(int j=0;j<3;j++)Near(NativeMinionFx.TongueFrame[o+j],p[j],"tongue anchor",1e-6);for(int j=0;j<4;j++)Near(NativeMinionFx.TongueFrame[o+3+j],q[j],"tongue base turn",1e-6);
                    Near(NativeMinionFx.TongueFrame[o+7],t.GetProperty("height").GetDouble(),"tongue height",1e-6);Near(NativeMinionFx.TongueFrame[o+8],t.GetProperty("phase").GetDouble(),"tongue phase",1e-6);k++;
                }
                v++;
            }
        }
        private static void Tongues(JsonElement frames)
        {
            var m=new double[16];int n=0;
            foreach(var f in frames.EnumerateArray())
            {
                int v=f.GetProperty("variant").GetInt32();double t=f.GetProperty("time").GetDouble(),e=N(f,"e"),d=N(f,"d"),flare=NativeMinionFx.Flare(double.IsNaN(e)?double.PositiveInfinity:e,double.IsNaN(d)?double.PositiveInfinity:d);int k=0;
                foreach(var g in f.GetProperty("tongues").EnumerateArray())
                {
                    Check(NativeMinionFx.TongueMatrix(v,k,t,flare,0,m,out _,out _),"tongue "+k+" of variant "+v);var p=A(g.GetProperty("pos"));var q=A(g.GetProperty("quat"));var s=A(g.GetProperty("scale"));var want=new double[16];
                    NativeMinionFx.Rotation(want,q[0],q[1],q[2],q[3],s[0],s[1],s[2],p[0],p[1],p[2]);for(int j=0;j<12;j++)Near(m[j],want[j],"variant "+v+" tongue "+k+" at "+t+" element "+j,1e-6);k++;n++;
                }
            }
            Check(n>=30,"tongue frames cover every variant's tongues");
        }
        private static void Effects(JsonElement runs)
        {
            var cubes=new NativeMinionFx.Cube[NativeMinionFx.EffectCubes];var m=new double[16];int emerge=0,death=0;
            foreach(var r in runs.EnumerateArray())
            {
                uint seed=(uint)r.GetProperty("seed").GetDouble();double q=r.GetProperty("quality").GetDouble(),e=N(r,"e"),d=N(r,"d"),floor=r.GetProperty("floor").GetDouble();var points=A(r.GetProperty("points"));var slots=r.GetProperty("slots");
                int n=NativeMinionFx.Effects(seed,e,d,floor,points,points.Length/3,q,cubes);string tag="seed "+seed+" q "+q+(double.IsNaN(e)?" d "+d:" e "+e);
                Check(n==slots.GetArrayLength(),"MinionFx shown cubes "+tag+": "+n+" != "+slots.GetArrayLength());Check(r.GetProperty("visible").GetBoolean()==(n>0),"MinionFx visibility "+tag);
                int i=0;foreach(var s in slots.EnumerateArray())
                {
                    var c=cubes[i++];NativeFxMath.Compose(m,c.X,c.Y,c.Z,c.Ex,c.Ey,c.Ez,c.Sx,c.Sy,c.Sz);var want=A(s.GetProperty("m"));var rgb=A(s.GetProperty("c"));
                    for(int row=0;row<3;row++)for(int col=0;col<4;col++)Near(m[row*4+col],want[col*4+row],"MinionFx slot "+s.GetProperty("i").GetInt32()+" "+tag,3e-6);
                    Near(c.R,rgb[0],"MinionFx colour "+tag,3e-6);Near(c.G,rgb[1],"MinionFx colour "+tag,3e-6);Near(c.B,rgb[2],"MinionFx colour "+tag,3e-6);
                }
                if(n>0){if(double.IsNaN(e))death++;else emerge++;}
            }
            Check(emerge>=8&&death>=10,"fixtures cover the emergence and the burn-away");
        }
        private static void Bank(string root,JsonElement variants)
        {
            var env=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json"))).RootElement;
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            using var bank=JsonDocument.Parse(reader.ReadToEnd());var models=bank.RootElement.GetProperty("models").EnumerateArray().ToArray();
            for(int v=0;v<NativeMinionFx.Variants;v++)
            {
                var model=models.First(x=>x.GetProperty("id").GetString()=="horde-"+v);var classes=new int[3];double brightest=0;
                foreach(var s in model.GetProperty("sections").EnumerateArray())
                {
                    Check(!s.GetProperty("ink").GetBoolean(),"horde-"+v+" bakes no tongue (they are horde-tongue pieces)");
                    var c=A(s.GetProperty("colors"));var e=A(s.GetProperty("emission"));for(int i=0;i<c.Length;i+=4)brightest=Math.Max(brightest,Math.Max(c[i],Math.Max(c[i+1],c[i+2])));
                    for(int i=0;i<e.Length;i+=4){if(e[i+3]<2.5)continue;NativeMinionFx.Decode(e[i+3],out int cls,out double cell);classes[cls]++;Check(cell>=0&&cell<=1&&e[i]+e[i+1]+e[i+2]>.05,"horde-"+v+" glow code and emission");}
                }
                Check(brightest<1.5,"horde-"+v+" keeps the glow flag out of its albedo (max "+brightest+")");
                var want=variants[v].GetProperty("glow").GetProperty("classes");for(int k=0;k<3;k++)Check(classes[k]%24==0&&classes[k]/24==want[k].GetInt32(),"horde-"+v+" glow cells of class "+k+": "+classes[k]/24+" != "+want[k].GetInt32());
            }
            var tongue=models.First(x=>x.GetProperty("id").GetString()=="horde-tongue").GetProperty("sections").EnumerateArray().ToArray();
            Check(tongue.Length==2&&tongue[0].GetProperty("part").GetString()=="horde-tongue-0"&&tongue[1].GetProperty("part").GetString()=="horde-tongue-1","two horde-tongue pieces");
            foreach(var s in tongue)Check(s.GetProperty("ink").GetBoolean()&&s.GetProperty("bone").GetInt32()==-1&&s.GetProperty("positions").GetArrayLength()==17*24*3,"a tongue piece is 17 ink boxes in its own frame");
        }
        private static void Rules()
        {
            // The flicker: bounded by its class, and the add-wide drop takes every cell of the class at once.
            for(int i=0;i<600;i++)
            {
                int cls=i%3;double cell=(i*.6180339)%1,phase=(i*.37)%6.47,t=i*.0731,f=NativeMinionFx.ClassFlicker[cls],code=NativeMinionFx.Code(cls,cell),v=NativeMinionFx.Flicker(code,phase,t);
                NativeMinionFx.Decode(code,out int back,out double c2);Check(back==cls&&Math.Abs(c2-cell)<1e-9,"glow code round trip");
                Check(v<=1+1e-9&&v>=(1-f)*(1-.8*f)-1e-9,"flicker bounds");
                if(NativeMinionFx.Drop(phase,t,cls)<1)for(int k=0;k<5;k++)Check(NativeMinionFx.Flicker(NativeMinionFx.Code(cls,k*.19),phase,t)<=1-.8*f+1e-9,"the drop dims every cell of the add");
            }
            int drops=0;for(int i=0;i<7000;i++)if(NativeMinionFx.Drop(1.3,i/7.0+.01)<1)drops++;Check(drops>600&&drops<1400,"about one slot in seven drops ("+drops+")");
            var phases=Enumerable.Range(0,64).Select(NativeMinionFx.Phase).ToArray();Check(phases.Distinct().Count()==64&&phases.All(p=>p>=0&&p<=4*1.618&&p*4096==Math.Round(p*4096)),"every add its own glow phase, on the 1/4096 grid");
            // The drop bits: decoded in float exactly as NativeSourceLit does, the phase comes back bit for bit and the bits are the
            // drop the haze follows, for every add over a long match.
            for(int i=0;i<4000;i++)
            {
                int id=i%97;double phase=NativeMinionFx.Phase(id),t=i*1.37+(i%7)*.031;float w=(float)NativeMinionFx.GlowW(phase,t),d12=w>=15.5f?1:0;w-=16*d12;float d0=w>=7.5f?1:0,back=w-8*d0;
                Check(back==(float)phase&&(d0>0)==NativeMinionFx.Dropped(phase,t,0)&&(d12>0)==NativeMinionFx.Dropped(phase,t,1)&&NativeMinionFx.Dropped(phase,t,1)==NativeMinionFx.Dropped(phase,t,2),"glow bits carry the phase and the drop ("+id+" at "+t+")");
                NativeMinionFx.SplitGlow(NativeMinionFx.GlowW(phase,t),out double p2,out bool s0,out bool s12);Check(Math.Abs(p2-phase)<1e-12&&s0==(d0>0)&&s12==(d12>0),"SplitGlow reads GlowW");
                Check((NativeMinionFx.Drop(phase,t)<1)==(d0>0),"the haze drops on the crack cells' bit");
            }
            // Charred embers: materials.ts charred glow 3.2 over the live glow and flicker .6; no change unburnt.
            Check(Math.Abs(NativeMinionFx.CharGlow[0]-1.882353)<1e-6&&Math.Abs(NativeMinionFx.CharGlow[1]-2.133333)<1e-6&&NativeMinionFx.CharGlow[2]==2&&NativeMinionFx.CharFlicker==.6,"charred glow and flicker");
            for(int i=0;i<600;i++)
            {
                int cls=i%3;double cell=(i*.381966)%1,phase=NativeMinionFx.Phase(i),t=i*.0613,code=NativeMinionFx.Code(cls,cell),live=NativeMinionFx.Flicker(code,phase,t),burnt=NativeMinionFx.Flicker(code,phase,t,1),g=NativeMinionFx.CharGlow[cls];
                Check(NativeMinionFx.Flicker(code,phase,t,0)==live&&burnt<=g+1e-9&&burnt>=g*(1-.6)*(1-.48)-1e-9,"charred flicker bounds");
                if(NativeMinionFx.Dropped(phase,t,cls))Check(burnt<=g*(1-.48)+1e-9,"a charred joint's embers drop with the add");
            }
            Check(NativeMinionFx.Phase(7)==NativeMinionFx.Phase(7)&&NativeMinionFx.Phase(3)!=NativeMinionFx.Phase(9),"adds of one variant flicker out of step");
            Check(NativeMinionFx.Variant(13)==1&&NativeMinionFx.Variant(0)==0,"variant id % 6");
            // Flare windows.
            Check(NativeMinionFx.Flare(double.PositiveInfinity,double.PositiveInfinity)==1&&NativeMinionFx.Win(double.NaN,0,1,2,3)==0,"no flare without a climb or a death");
            Near(NativeMinionFx.Flare(1.0,double.PositiveInfinity),1.5,"emergence flare peak");Near(NativeMinionFx.Flare(double.PositiveInfinity,1.2),1.7,"burn-away flare peak");
            // Char and burn-away: feet first, the head last, all gone by 2.35 s; the body hides at DEATH_SECONDS.
            uint seed=NativeMinionFx.Seed(42);
            for(int j=0;j<22;j++){double at=NativeMinionFx.CharAt(seed,j);Check(at>=.42&&at<=1.12&&NativeMinionFx.Charred(seed,j,at-.01)==0&&NativeMinionFx.Charred(seed,j,at+.11)==1,"joint "+j+" chars at its hashed time");}
            Check(NativeMinionFx.Charred(seed,5,double.NaN)==0&&NativeMinionFx.Vanish(5,double.NaN)==0,"alive: no char, no burn-away");
            double feet=0,head=0;for(double d=1.2;d<=2.4;d+=.01){double f=NativeMinionFx.Vanish(CapturedRig.LFoot,d),h=NativeMinionFx.Vanish(CapturedRig.Head,d);Check(f>=feet-1e-12&&h>=head-1e-12,"burn-away never comes back");Check(h<=f+1e-12,"the head burns after the feet");feet=f;head=h;}
            for(int j=1;j<22;j++)if(!double.IsNaN(NativeMinionFx.JointRest[j*4]))Check(NativeMinionFx.Vanish(j,2.36)>.999,"joint "+j+" gone by 2.35 s");
            Check(NativeMinionFx.Vanish(CapturedRig.Body,2.4)==0,"a joint without parts never shrinks");Check(!NativeMinionFx.Gone(2.29)&&NativeMinionFx.Gone(2.3),"the body hides at DEATH_SECONDS");
            // Licks: pure in time, small, near the tip, trailing the add's motion; halved with reduced effects.
            Check(NativeMinionFx.LickCount(1)==4&&NativeMinionFx.LickCount(.3)==2,"licks halve with reduced effects");int shown=0;
            for(int i=0;i<4000;i++)
            {
                int k=i%3,j=(i/3)%4;double t=i*.0137;
                if(!NativeMinionFx.Lick(seed,k,j,t,0,0,out double dx,out double dy,out double dz,out _,out _,out _,out double edge,out double r,out double g,out double b))continue;shown++;
                NativeMinionFx.Lick(seed,k,j,t,0,0,out double dx2,out double dy2,out double dz2,out _,out _,out _,out double edge2,out _,out _,out _);Check(dx==dx2&&dy==dy2&&dz==dz2&&edge==edge2,"a lick is pure in time");
                Check(edge>0&&edge<=.035&&dy>=0&&dy<=.141&&Math.Abs(dx)<=.04&&Math.Abs(dz)<=.04&&r>=0&&g>=0&&b>=0&&r<1.2&&g<1.2&&b<1.2,"a lick stays small and near its tip");
                NativeMinionFx.Lick(seed,k,j,t,2,0,out double mx,out _,out _,out _,out _,out _,out _,out _,out _,out _);Check(mx<dx+1e-12,"a lick trails a moving add");
            }
            Check(shown>1500,"licks keep a column over each tongue ("+shown+" of 4000)");
            // Effects counts and the reduced cap.
            var cubes=new NativeMinionFx.Cube[NativeMinionFx.EffectCubes];var pts=new double[]{0,1.6,0,0,1.2,0,0,.9,0,.1,.5,0,-.1,.5,0,.3,1.1,0};int most=0,mostLow=0;
            for(double d=0;d<3.2;d+=.05){most=Math.Max(most,NativeMinionFx.Effects(seed,double.NaN,d,-.86,pts,6,1,cubes));mostLow=Math.Max(mostLow,NativeMinionFx.Effects(seed,double.NaN,d,-.86,pts,6,.3,cubes));}
            for(double e=0;e<2.4;e+=.05){most=Math.Max(most,NativeMinionFx.Effects(seed,e,double.NaN,-.86,pts,0,1,cubes));mostLow=Math.Max(mostLow,NativeMinionFx.Effects(seed,e,double.NaN,-.86,pts,0,.3,cubes));}
            Check(most<=NativeMinionFx.EffectCubes&&mostLow<=NativeMinionFx.Pool+NativeMinionFx.Flames/2+NativeMinionFx.Sparks/2&&most>mostLow,"fx cubes capped and halved with reduced effects");
            Check(NativeMinionFx.Effects(seed,double.NaN,double.NaN,-.86,pts,6,1,cubes)==0&&NativeMinionFx.Effects(seed,2.36,double.NaN,-.86,pts,0,1,cubes)==0&&NativeMinionFx.Effects(seed,double.NaN,3.11,-.86,pts,6,1,cubes)==0,"no effects outside the climb and the death windows");
            // Reservations cover what an add shows; ranks and distance bands (horde-view.ts: 24 full rigs, 20 m + 4 m hysteresis).
            Check(NativeMinionFx.MostCubes(1)==NativeMinionFx.EffectCubes&&most<=NativeMinionFx.MostCubes(1)&&NativeMinionFx.MostCubes(.3)==NativeMinionFx.Pool+NativeMinionFx.Flames/2+NativeMinionFx.Sparks/2&&mostLow<=NativeMinionFx.MostCubes(.3)&&NativeMinionFx.MostCubes(-1)==0,"an add reserves its most cubes");
            for(double x=-.5;x<3.4;x+=.05){if(!NativeMinionFx.Showing(x,double.NaN))Check(NativeMinionFx.Effects(seed,x,double.NaN,-.86,pts,0,1,cubes)==0,"no emergence cubes outside Showing");if(!NativeMinionFx.Showing(double.NaN,x))Check(NativeMinionFx.Effects(seed,double.NaN,x,-.86,pts,6,1,cubes)==0,"no burn-away cubes outside Showing");}
            Check(NativeMinionFx.Showing(.1,double.NaN)&&NativeMinionFx.Showing(double.NaN,3.0)&&!NativeMinionFx.Showing(2.36,double.NaN)&&!NativeMinionFx.Showing(double.NaN,3.11)&&!NativeMinionFx.Showing(double.NaN,double.NaN),"Showing windows");
            Check(NativeMinionFx.Rank(true,double.NaN,double.NaN,60)<NativeMinionFx.Rank(false,.5,double.NaN,1)&&NativeMinionFx.Rank(false,double.NaN,1,60)<NativeMinionFx.Rank(false,.5,double.NaN,1)&&NativeMinionFx.Rank(false,.5,double.NaN,60)<NativeMinionFx.Rank(false,double.NaN,double.NaN,1)&&NativeMinionFx.Rank(false,9,double.NaN,2)<NativeMinionFx.Rank(false,double.NaN,double.NaN,3),"the possessed and dying adds first, then vent climbers, then the rest, nearest first");
            Check(NativeMinionFx.Rank(true,double.NaN,double.NaN,50)<NativeMinionFx.Rank(false,double.NaN,double.NaN,1)&&NativeMinionFx.Rank(false,double.NaN,double.NaN,4)<NativeMinionFx.Rank(false,double.NaN,double.NaN,5),"licks: the possessed add, then the nearest");
            Check(NativeMinionFx.Band(10,1)==1&&NativeMinionFx.Band(24,1)==1&&NativeMinionFx.Band(30,1)==.3&&NativeMinionFx.Band(30,.2)==.2&&NativeMinionFx.Band(40,1)==.3&&NativeMinionFx.Band(40.01,1)<0,"effect cubes full to 24 m, halved to 40 m, none beyond");
            Check(NativeMinionFx.MostTongues==3&&NativeMinionFx.FullRigs==24,"the budgets' units");
            // The vent age: the source puts ActiveAt at EMERGE_SECONDS for first and following adds.
            Check(double.IsNaN(NativeMinionFx.VentAge(false,10,9))&&double.IsNaN(NativeMinionFx.VentAge(true,10,7))&&Math.Abs(NativeMinionFx.VentAge(true,10,10)-NativeMinionFx.EmergeSeconds)<1e-12,"vent age");
            // Hint pulse and haze.
            for(int i=0;i<200;i++){double t=i*.093,p=NativeMinionFx.HintPulse(1.1,t);Check(p>=0&&p<=1&&Math.Abs(p-NativeMinionFx.HintPulse(1.1,t+4))<1e-9,"hint pulse every 4 s");NativeMinionFx.Haze(2.2,t,out double size,out double opacity);Check(size>=.28&&size<=.38&&opacity>=0&&opacity<=.26,"haze bounds");}
            NativeMinionFx.Hint(true,1,out _,out double hy,out _,out double sx,out _,out double sz,out double seam,out double halo,out double ho);Check(Math.Abs(hy-.365)<1e-12&&sx==.09&&sz==.22&&seam==1&&Math.Abs(halo-1.7)<1e-12&&ho==1,"hints.ts cracked seam and halo at full pulse");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var source=R("Assets/ChipArena/Runtime/NativeSourcePresentation.cs");Check(source.Contains("BeginHordeFx(arena);")&&source.Contains("HordeAdd(rig,m.Id,")&&source.Contains("glow:add!=null?HordeGlow(m.Id)"),"SourceWorld draws the minion effects with each add");
            Check(source.Contains("arena.Time-d.at<NativeMinionFx.DeathFxSeconds"),"a dead add's rig outlives its death effects");
            var summon=R("Assets/ChipArena/Runtime/NativeSummonPresentation.cs");Check(summon.Contains("HordeDeathBody(rig,death.model")&&!summon.Contains("Mathf.Clamp01((2.3f-age)/.3f)"),"the horde death burns away instead of the dithered fade");
            var dev=R("Assets/ChipArena/Runtime/NativeDevEffects.cs");int a=dev.IndexOf("DrawHordeFx(camera);",StringComparison.Ordinal),b=dev.IndexOf("fxDraw.Draw(camera,ViewCamera,now);",StringComparison.Ordinal);Check(a>0&&b>a,"the effect layers get the minion effects before they draw");
            Check(R("Assets/ChipArena/Runtime/NativeSourceModels.cs").Contains("SplitSirenPieces(data);HordeMaterials();"),"the bank flags the horde materials and lifts the tongue pieces");
            var fx=R("Assets/ChipArena/Runtime/NativeMinionFxPresentation.cs");Check(fx.Contains("Shader.SetGlobalFloat(MinionClockId,hordeClock)")&&fx.Contains("hordeClock=FieldClock(arena)")&&fx.Contains("NativeMinionFx.LickCount(quality)")&&fx.Contains("EffectsQuality"),"one presentation clock and the effects quality");
            Check(fx.Contains("NativeMinionFx.GlowW(NativeMinionFx.Phase(id),hordeClock)")&&fx.Contains("new Color(k,k,k,1-c)"),"the glow carries the frame's drop bits and the death tint the char");
            Check(fx.Contains("Array.Sort(hordeRank,hordeOrder,0,hordeFxCount);")&&fx.Contains("most>0&&most<=cubes&&NativeMinionFx.Showing(job.Emerge,job.Death)")&&fx.Contains("hordeDistance[n]<LickRange&&want>0&&want<=lickBudget")&&fx.Contains("NativeMinionFx.Rank(job.Possessed,double.NaN,double.NaN,hordeDistance[n])")&&fx.Contains("NativeMinionFx.Effects(job.Seed,job.Emerge,job.Death,floor,hordePoints,points,band,hordeCubes)")&&!fx.Contains("HordeFxMax"),"effects go out by rank and band, whole adds, with no job cap");
            var shader=R("Assets/ChipArena/Resources/NativeSourceLit.shader");
            foreach(var s in new[]{"float MinionFlicker(float code,float w,float charred)","float d12=step(15.5,w);w-=16*d12;float d0=step(7.5,w),phase=w-8*d0;","f=lerp(cls<.5?.35:cls<1.5?.85:.9,.6,charred),drop=cls>.5?d12:d0;phase+=cls>.5?.37:0;","frac(sin(cell*289.37+phase*7.13)*43758.5453)","sin(_MinionClock*(4+9*h)+h*40+phase*3)","lerp(1,wave,f)*(1-drop*f*.8)*lerp(1,cls<.5?1.882353:cls<1.5?2.133333:2,charred)","float4 sceneTint=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint);tint*=sceneTint.rgb;","if(_MinionGlow>.5&&i.emission.w>2.5)o.Emission*=MinionFlicker(i.emission.w,glowTint.w,1-sceneTint.a);","float3 tint=i.emission.w>2.5?1:"})
                Check(shader.Contains(s),"NativeSourceLit minion flicker: "+s);
            Check(!shader.Contains("floor(_MinionClock*7")&&!shader.Contains("slot*12.9898"),"NativeSourceLit takes the drop from the glow bits, never a float re-hash");
            Check(NativeMinionFx.ClassFlicker.SequenceEqual(new[]{.35,.85,.9})&&NativeMinionFx.ClassPhase.SequenceEqual(new[]{0,.37,.37}),"Core flicker classes match the shader");
            var export=R("tools/export_models.mjs");Check(export.Contains("o.name==='siren-minion-tongue'")&&export.Contains("lit=minionGlow(mat,q,instanceColor)")&&export.Contains("NativeMinionFx.Generated.cs")&&export.Contains("id:'horde-tongue'"),"the exporter decodes the glow flag and lifts the tongues");
        }
    }
}
#endif
