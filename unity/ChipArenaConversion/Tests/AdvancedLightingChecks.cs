#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The advanced lighting (docs/LIGHTING-ADVANCED.md): MapLooks' `advanced` block and its loader rules, the graphics tiers, the
    // light volume's bake (Core NativeLightVolume: occlusion, sky, one coloured bounce, determinism, the budget, the half
    // floats), the volumetric beams (Core NativeBeamList and the FX builders' hand-over), the weather (Core NativeWeatherFx,
    // NativeWeatherCover, the lightning), the scattering's closed form against a numeric integral, and source pins on the shaders
    // and the runtime: every part off (exactly today's look) on the low tier, without a look's `advanced`, and while a stage owns
    // the screen.
    internal static class AdvancedLightingChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Advanced lighting: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static string Body(string code,string head)
        {
            int at=code.IndexOf(head,StringComparison.Ordinal);Check(at>=0,"has "+head);int open=code.IndexOf('{',at),depth=0;
            for(int i=open;i<code.Length;i++){if(code[i]=='{')depth++;else if(code[i]=='}'&&--depth==0)return code.Substring(at,i-at+1);}
            throw new Exception("unclosed "+head);
        }
        private static string Between(string text,string a,string b){int i=text.IndexOf(a,StringComparison.Ordinal),j=i<0?-1:text.IndexOf(b,i,StringComparison.Ordinal);Check(i>=0&&j>i,"between "+a+" and "+b);return text.Substring(i,j-i);}
        private static ContentBlock Box(string id,float x,float y,float z,float sx,float sy,float sz,string material="stone")=>new ContentBlock{id=id,material=material,role="static",pos=new V3(x,y,z),size=new V3(sx,sy,sz)};
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            Looks(root,o);Tiers();Volume(root,o);Beams(root,o);Weather();Scatter();Pins(R);
            Console.WriteLine("PASS Advanced lighting ("+count+" checks: the looks' advanced block and tiers, the light volume's occlusion, sky and coloured bounce, the beams, the weather and lightning, the scattering's closed form, the shader and runtime hooks)");
        }

        // ---- MapLooks' advanced block ----
        private static void Looks(string root,JsonSerializerOptions o)
        {
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);
            string[] advanced={"factory-first-shift","golden-demolition","neon-rain","quarantine","expedition-city","central-station","ironworks"};
            foreach(var m in set.maps)
            {
                bool want=advanced.Contains(m.look);Check((m.advanced!=null)==want,m.look+(want?" has":" has no")+" an advanced block");
                if(m.look.StartsWith("night-",StringComparison.Ordinal))Check(m.advanced==null,m.look+": the old maps keep today's look");
                // The anime style's copy keeps the block (Apply reads the styled look).
                Check(ReferenceEquals(NativeMapStyle.Styled(m,false).advanced,m.advanced),m.look+": the styled look keeps its advanced block");
            }
            var cyber=NativeMapLooks.ByLook(set,"neon-rain").advanced;Check(cyber.weather.kind=="rain"&&cyber.weather.lightning>0&&cyber.weather.wet>0&&cyber.weather.puddles>0,"Cyber City: rain, wet streets, puddles and lightning");
            Check(NativeMapLooks.ByLook(set,"central-station").advanced.weather.kind=="drizzle"&&NativeMapLooks.ByLook(set,"central-station").advanced.weather.lightning==0,"Central Station: drizzle, no lightning");
            var town=NativeMapLooks.ByLook(set,"expedition-city").advanced.weather;Check(town.kind=="rain"&&town.lightning>0&&town.amount<1,"the town: a light storm");
            Check(!NativeAdvancedLooks.GiOn(NativeMapLooks.ByLook(set,"quarantine")),"Ward Zero keeps its own ceiling light field (no volume)");
            foreach(var look in advanced)Check(NativeAdvancedLooks.BeamsOn(NativeMapLooks.ByLook(set,look))&&NativeAdvancedLooks.ScatterOn(NativeMapLooks.ByLook(set,look)),look+": beams and scattering");
            // JsonUtility writes every field; the file does too.
            var raw=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json"))).RootElement;
            foreach(var m in raw.GetProperty("maps").EnumerateArray())
            {
                if(!m.TryGetProperty("advanced",out var a))continue;string id=m.GetProperty("look").GetString();
                foreach(var (part,fields) in new[]{("gi",new[]{"on","occlusion","bounce","floor","cell","actors"}),("scatter",new[]{"on","color","density","height","falloff","max","key","phase","lights"}),("beams",new[]{"on","gain","dust","soft"}),("weather",new[]{"kind","amount","wind","wet","puddles","lightning","interval","lightningColor"})})
                    foreach(var f in fields)Check(a.GetProperty(part).TryGetProperty(f,out _),id+": advanced."+part+"."+f+" written");
            }
            // Validation refuses what the runtime cannot apply.
            void Bad(Action<MapLookAdvanced> change,string what)
            {
                var a=new MapLookAdvanced{gi=new MapLookGi{on=true,occlusion=.5f,bounce=1,floor=.4f,cell=1,actors=.8f},scatter=new MapLookScatter{on=true,density=.01f,max=.3f},beams=new MapLookBeams{on=true,gain=1},weather=new MapLookWeather{kind="rain",amount=1}};
                NativeAdvancedLooks.Validate("ok",a);change(a);bool threw=false;try{NativeAdvancedLooks.Validate("bad",a);}catch(InvalidOperationException){threw=true;}Check(threw,"refuses "+what);
            }
            Bad(a=>a.gi.occlusion=1.5f,"an occlusion above 1");Bad(a=>a.gi.bounce=float.NaN,"a NaN bounce");Bad(a=>a.scatter.density=0,"scattering on with no density");Bad(a=>a.scatter.phase=.95f,"a phase past .9");
            Bad(a=>a.weather.kind="hail","an unknown weather");Bad(a=>{a.weather.kind="";a.weather.amount=.5f;},"a fall without a kind");Bad(a=>a.beams.dust=2,"dust above 1");Bad(a=>a.weather.wind=40,"a gale");
            NativeAdvancedLooks.Validate("none",null);
        }

        // ---- tiers ----
        private static void Tiers()
        {
            Check(NativeGraphicsTiers.Pick("performance",8192,50)==NativeGraphicsTier.Low&&NativeGraphicsTiers.Pick("balanced",8192,50)==NativeGraphicsTier.Medium&&NativeGraphicsTiers.Pick("high",1024,30)==NativeGraphicsTier.High,"the named tiers");
            Check(NativeGraphicsTiers.Pick("auto",1536,50)==NativeGraphicsTier.Low&&NativeGraphicsTiers.Pick("auto",4096,40)==NativeGraphicsTier.Low,"auto keeps today's low rule (under 2 GB or below shader model 4.5)");
            Check(NativeGraphicsTiers.Pick("auto",4096,50)==NativeGraphicsTier.Medium&&NativeGraphicsTiers.Pick("auto",8192,50)==NativeGraphicsTier.High&&NativeGraphicsTiers.Pick("mystery",8192,50)==NativeGraphicsTier.High,"auto picks Medium under 6 GB, High from it (an unknown choice reads as auto)");
            Check(NativeGraphicsTiers.Known("balanced")=="balanced"&&NativeGraphicsTiers.Known("medium")=="auto"&&NativeGraphicsTiers.Choices.SequenceEqual(new[]{"auto","high","balanced","performance"})&&NativeGraphicsTiers.Labels.Length==4,"the four choices, saved values kept");
            var look=new MapLook{look="t",advanced=new MapLookAdvanced{gi=new MapLookGi{on=true,occlusion=.6f,bounce=1,floor=.4f,cell=1,actors=.8f},scatter=new MapLookScatter{on=true,density=.02f,max=.3f,falloff=0,phase=.5f},beams=new MapLookBeams{on=true,gain=1.2f},weather=new MapLookWeather{kind="rain",amount=1,lightning=1,interval=0,wet=.8f}}};
            var s=new AdvancedLightState();
            NativeAdvancedLooks.Resolve(look,NativeGraphicsTier.Low,false,s);Check(!s.Any&&!s.Gi&&!s.Scatter&&!s.Beams&&s.Weather==""&&s.GiOcclusion==0&&s.Density==0&&s.Wet==0,"the low tier is today's look: everything off");
            NativeAdvancedLooks.Resolve(new MapLook{look="none"},NativeGraphicsTier.High,false,s);Check(!s.Any,"a look without the block: everything off");
            NativeAdvancedLooks.Resolve(look,NativeGraphicsTier.High,false,s);
            Check(s.Gi&&s.GiCell==1&&s.GiRays==NativeAdvancedLooks.RaysHigh&&s.Scatter&&s.Falloff==NativeAdvancedLooks.DefaultFalloff&&s.Beams&&s.BeamSamples==2&&s.BeamSoft==NativeAdvancedLooks.DefaultSoft&&s.Weather=="rain"&&s.Amount==1&&s.Interval==NativeAdvancedLooks.DefaultInterval,"High: everything, the defaults for unwritten values");
            NativeAdvancedLooks.Resolve(look,NativeGraphicsTier.Medium,false,s);
            Check(s.Gi&&Math.Abs(s.GiCell-NativeAdvancedLooks.MediumCell)<1e-6f&&s.GiRays==NativeAdvancedLooks.RaysMedium&&s.BeamSamples==1&&Math.Abs(s.Amount-NativeAdvancedLooks.MediumWeather)<1e-6f,"Medium: a coarser volume, fewer rays, one dust octave, less fall");
            NativeAdvancedLooks.Resolve(look,NativeGraphicsTier.High,true,s);Check(Math.Abs(s.Amount-NativeAdvancedLooks.ReducedWeather)<1e-6f&&Math.Abs(s.Lightning-NativeAdvancedLooks.ReducedLightning)<1e-6f,"Reduced effects halves the fall and the lightning");
            // Resolve allocates nothing.
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)NativeAdvancedLooks.Resolve(look,(NativeGraphicsTier)(i%3),i%2==0,s);Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"Resolve allocates nothing");
        }

        // ---- the light volume ----
        private static void Volume(string root,JsonSerializerOptions o)
        {
            // A street with a roofed room (one door) and, out in the open, a red wall lit by a warm lamp.
            var blocks=new[]{Box("ground",0,-.5f,0,60,1,60,"foundation"),Box("room-w",-14,2,0,.3f,4,8),Box("room-e",-6,2,0,.3f,4,8),Box("room-n",-10,2,4,8.3f,4,.3f),Box("room-s-a",-12.5f,2,-4,3.3f,4,.3f),Box("room-s-b",-7.5f,2,-4,3.3f,4,.3f),Box("room-roof",-10,4.15f,0,8.6f,.3f,8.6f),Box("red-wall",10,2,6,8,4,.4f,"brick")};
            var alb=new float[blocks.Length*3];for(int i=0;i<blocks.Length;i++){alb[i*3]=.4f;alb[i*3+1]=.4f;alb[i*3+2]=.4f;}alb[7*3]=.8f;alb[7*3+1]=.05f;alb[7*3+2]=.04f;
            var input=new NativeLightVolumeInput{Blocks=blocks,Albedo=alb,MinX=-20,MaxX=20,MinZ=-12,MaxZ=12,Ceiling=12,Cell=1,Rays=40,KeyX=-.3f,KeyY=.8f,KeyZ=-.5f,KeyR=.4f,KeyG=.38f,KeyB=.34f,Lights=new[]{new NativeVolumeLight(10,2.5f,4,9,2.2f,1.6f,1.0f)}};
            for(int k=0;k<3;k++){input.Ambient[k]=.08f;input.Ambient[3+k]=.05f;input.Ambient[6+k]=.02f;}
            var v=NativeLightVolume.Build(input);var again=NativeLightVolume.Build(input);
            Check(v.Data.Length==v.Nx*v.Ny*v.Nz*4&&v.Data.SequenceEqual(again.Data)&&v.Cell==1&&v.Lights==1&&v.Open>0&&v.Voxels>0,"the bake: one texel per cell, the same bytes twice (worker threads never change it)");
            Check(v.Data.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&x>=0),"every texel finite and non-negative");
            v.Sample(16,1,-8,0,1,0,out _,out _,out _,out float open);v.Sample(-10,1,0,0,1,0,out _,out _,out _,out float inside);v.Sample(-10,1,-3.2f,0,1,0,out _,out _,out _,out float door);
            Check(open>.85f&&inside<.62f&&inside<open-.25f,"open street "+open.ToString("0.00")+", the roofed room "+inside.ToString("0.00")+": the room is occluded");
            Check(door>inside,"by the door the room is more open ("+door.ToString("0.00")+")");
            v.Sample(10,1.2f,4.6f,0,0,1,out float wr,out float wg,out float wb,out _);v.Sample(10,1.2f,-6,0,0,-1,out float fr,out float fg,out float fb,out _);
            Check(wr>wb*2&&wr>fr*2,"the lamp's light bounces red off the red wall ("+wr.ToString("0.000")+" red, "+wb.ToString("0.000")+" blue; "+fr.ToString("0.000")+" away from it)");
            v.Sample(-6.1f,2,0,1,0,0,out _,out _,out _,out float wallOut);v.Sample(-5.6f,2,0,1,0,0,out _,out _,out _,out float airOut);Check(Math.Abs(wallOut-airOut)<.2f,"a wall's outer face reads the air in front of it, not the room behind ("+wallOut.ToString("0.00")+" vs "+airOut.ToString("0.00")+")");
            v.Sample(500,0,500,0,1,0,out float xr,out _,out _,out float xo);Check(xr==0&&xo==1,"outside the box: the open value");
            // The key's bounce: a lit floor throws light up a wall; the same scene with no key throws none (no lamp near).
            var dark=new NativeLightVolumeInput{Blocks=blocks,Albedo=alb,MinX=-20,MaxX=20,MinZ=-12,MaxZ=12,Ceiling=12,Cell=1,Rays=24};var vd=NativeLightVolume.Build(dark);
            vd.Sample(0,1,-8,0,1,0,out float dr,out _,out _,out _);var lit=NativeLightVolume.Build(new NativeLightVolumeInput{Blocks=blocks,Albedo=alb,MinX=-20,MaxX=20,MinZ=-12,MaxZ=12,Ceiling=12,Cell=1,Rays=24,KeyY=1,KeyR=1,KeyG=1,KeyB=1});
            lit.Sample(0,1,-8,0,1,0,out float lr,out _,out _,out _);Check(dr==0&&lr>.05f,"the key's bounce off the lit street ("+lr.ToString("0.000")+"), none without light");
            // The half floats match .NET's.
            var probe=new[]{0f,1f,-1f,.5f,65504f,70000f,1e-5f,6e-8f,-2.5e-6f,.333333f,123.456f,float.PositiveInfinity,float.NaN,1e-9f};var half=NativeLightVolume.Half(probe);
            for(int i=0;i<probe.Length;i++){ushort want=BitConverter.HalfToUInt16Bits((Half)probe[i]);Check(float.IsNaN(probe[i])?(half[i]&0x7c00)==0x7c00&&(half[i]&0x3ff)!=0:half[i]==want,"half of "+probe[i]+": "+half[i].ToString("x4")+" (want "+want.ToString("x4")+")");}
            var rnd=new Random(7);var many=new float[4000];for(int i=0;i<many.Length;i++)many[i]=(float)(Math.Pow(10,rnd.NextDouble()*10-6)*(rnd.Next(2)*2-1));var hm=NativeLightVolume.Half(many);
            Check(many.Select((x,i)=>hm[i]==BitConverter.HalfToUInt16Bits((Half)x)).All(b=>b),"4000 random magnitudes round as .NET's Half");
            // The real maps: in budget, and their interiors occluded.
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o);var set=MapLookChecks.Load(root);
            foreach(var id in new[]{"factory","cybercity","expedition-transit","expedition-city"})
            {
                var map=maps.maps.First(m=>m.id==id);var look=NativeMapStyle.Styled(NativeMapLooks.ByLook(set,map.look),false);
                var inp=NativeLightVolume.From(map,look,_=>0x8A9496,null,1,12);var w=System.Diagnostics.Stopwatch.StartNew();var vol=NativeLightVolume.Build(inp);
                Check((long)vol.Nx*vol.Ny*vol.Nz<=NativeLightVolume.MaxCells&&vol.Ny<=NativeLightVolume.MaxY&&vol.Cell>=NativeLightVolume.MinCell&&vol.Lights==look.lights.Length,id+": in budget ("+vol.Nx+"x"+vol.Ny+"x"+vol.Nz+" of "+vol.Cell.ToString("0.00")+" m, "+w.ElapsedMilliseconds+" ms at 12 rays)");
                if(id=="factory")
                {
                    vol.Sample(0,1.5f,20,0,1,0,out _,out _,out _,out float hall);vol.Sample(0,1.5f,0,0,1,0,out _,out _,out _,out float bay);
                    Check(hall<.8f,"the Factory's roofed hall is occluded ("+hall.ToString("0.00")+"; the open bay "+bay.ToString("0.00")+")");
                }
            }
        }

        // ---- the beams ----
        private static void Beams(string root,JsonSerializerOptions o)
        {
            var b=new NativeBeamList();Check(b.Add(0,10,0,0,0,0,.5,2,1,.8,.6,.5,3)&&b.Count==1&&Math.Abs(b.Wy[0])<1e-6&&Math.Abs(b.Wx[0]*b.Wx[0]+b.Wz[0]*b.Wz[0]-1)<1e-5,"a vertical beam: its cross axis level and unit");
            Check(b.Outside(0,0,5,0)==0&&b.Outside(0,0,5,30)==1&&b.Outside(0,0,20,0)==1&&b.Outside(0,0,-3,0)==1,"the camera inside the beam sees none of it; far from it or past its ends, all");
            float edge=b.Outside(0,1.4f,5,0);Check(edge>0&&edge<1,"near its side the beam fades ("+edge+")");
            Check(!b.Add(0,0,0,0,0,.01,1,1,1,1,1)&&!b.Add(0,0,0,0,5,0,1,1,0,0,0),"no beam without length or light");
            b.Clear();b.Gain=2;b.Shaft(0,8,0,0,0,0,2,3,.5,.5,.5,.1);Check(Math.Abs(b.R0[0]-1)<1e-6&&Math.Abs(b.R1[0]-1.5)<1e-6&&Math.Abs(b.R[0]-.5*.1*NativeBeamList.FromBillboard*2)<1e-6,"a billboard shaft's widths are its diameters; its light the colour times opacity times the share, times the look's gain");
            b.Clear();b.Gain=1;for(int i=0;i<NativeBeamList.Capacity+5;i++)b.Add(i,5,0,i,0,0,1,1,1,1,1);Check(b.Count==NativeBeamList.Capacity,"the list stops at its capacity");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<20;k++){b.Clear();for(int i=0;i<60;i++){b.Add(i,5,0,i+1,0,1,1,2,1,1,1);b.Outside(i,0,2,0);}}Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"adding and testing beams allocates nothing");
            NativeBeamList.UnitMesh(24,10,out var p,out var n,out var t);
            bool outward=true;for(int i=0;i<t.Length;i+=3){int a=t[i]*3,c=t[i+1]*3,d=t[i+2]*3;float ux=p[c]-p[a],uy=p[c+1]-p[a+1],uz=p[c+2]-p[a+2],vx=p[d]-p[a],vy=p[d+1]-p[a+1],vz=p[d+2]-p[a+2];float nx=uy*vz-uz*vy,nz=ux*vy-uy*vx;float mx=(p[a]+p[c]+p[d])/3,mz=(p[a+2]+p[c+2]+p[d+2])/3;if(nx*mx+nz*mz<=0)outward=false;}
            Check(t.Length==24*10*6&&p.Length==25*11*3&&outward&&p.Where((x,i)=>i%3==1).Min()==0&&p.Where((x,i)=>i%3==1).Max()==1,"the unit beam: an open cone side from y 0 to 1, wound outward (Unity's front faces)");
            // The Factory hands its skylight sheets over; without a list it draws its billboards exactly as before.
            string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));var hz=JsonSerializer.Deserialize<FactoryHazardContent>(text,o).maps.First(m=>m.id=="factory").hazards;
            var light=new NativeFxLight{Lx=-.47,Ly=.59,Lz=-.66,Sr=1.1,Sg=.9,Sb=.6};var fx=new NativeFactoryFx();
            fx.Build(hz,65,10,40,-10,1.8,-12,1,0,0,0,1,0,light,1,false,true);int quads=fx.Quads.Count,shafts=fx.Shafts;
            var list=new NativeBeamList();fx.Beams=list;fx.Build(hz,65,10,40,-10,1.8,-12,1,0,0,0,1,0,light,1,false,true);
            Check(shafts>0&&list.Count==shafts&&fx.Quads.Count==quads-shafts,"the Factory: "+shafts+" skylight sheets become beams, the rest of its quads unchanged");
            bool inside=true;for(int i=0;i<list.Count;i++)if(Math.Abs(list.Ax[i])>NativeFactoryFx.ShellX+.1||Math.Abs(list.Az[i])>NativeFactoryFx.ShellZ+.1||list.By[i]<-.1||list.Ay[i]<NativeFactoryFx.RoofY-.01)inside=false;Check(inside,"every beam starts at a skylight and ends in the hall");
            list.Clear();fx.Build(hz,65,10,40,-10,1.8,-12,1,0,0,0,1,0,light,1,true,true);Check(list.Count==0,"the low tier: no shafts, no beams");
            fx.Beams=null;fx.Build(hz,65,10,40,-10,1.8,-12,1,0,0,0,1,0,light,1,false,true);Check(fx.Quads.Count==quads&&fx.Shafts==shafts,"without a list: the billboards");
            // Brickfall's sun shafts and Cyber City's searchlights.
            var bf=new NativeBrickfallFx();var sun=new NativeFxLight{Lx=.5,Ly=.4,Lz=.77,Sr=1.2,Sg=.9,Sb=.6};bf.Build(22,0,1.8,-10,1,0,0,0,1,0,sun,1,false,true,null,0);int bq=bf.Quads.Count,bs=bf.Shafts;
            var bl=new NativeBeamList();bf.Beams=bl;bf.Build(22,0,1.8,-10,1,0,0,0,1,0,sun,1,false,true,null,0);Check(bl.Count>=bs&&bl.Count==NativeBrickfallAtmosphere.Count(NativeBrickfallAtmosphere.Shafts,1,false,true)&&bf.Quads.Count==bq-bs,"Brickfall: every sun shaft a beam (none hidden by the billboards' view fade)");
            Check(Enumerable.Range(0,bl.Count).All(i=>bl.Ay[i]>bl.By[i]),"the sun shafts slant down from the sun's side");
        }

        // ---- the weather ----
        private static void Weather()
        {
            var blocks=new[]{Box("ground",0,-.5f,0,80,1,80,"foundation"),Box("roof",0,6,0,20,.4f,20)};var cover=NativeWeatherCover.Build(blocks,new MapBounds{minX=-40,maxX=40,minZ=-40,maxZ=40});
            Check(cover.Top(0,0)==6.2f&&cover.Top(30,30)==0&&cover.Top(500,0)==0,"the cover: the roof's top over it, the ground beside it and outside");
            var fx=new NativeWeatherFx();var light=new NativeFxLight{Lx=0,Ly=1,Sr=.2,Sg=.2,Sb=.3};
            fx.Build("rain",1,1.6,10,25,1.7,25,1,0,0,0,1,0,light,cover);int open=fx.Fall;fx.Build("rain",1,1.6,10,0,1.7,0,1,0,0,0,1,0,light,cover);int under=fx.Fall;
            Check(open>200&&under<open*.75&&fx.Splashes>0,"rain round the eye ("+open+" streaks), less under the roof ("+under+"), splashes on the tops");
            fx.Build("rain",1,1.6,10,25,1.7,25,1,0,0,0,1,0,light,cover);var a=fx.Quads.Corners.Take(fx.Quads.Count*12).ToArray();fx.Build("rain",1,1.6,10,25,1.7,25,1,0,0,0,1,0,light,cover);Check(a.SequenceEqual(fx.Quads.Corners.Take(fx.Quads.Count*12)),"the same clock and camera give the same rain");
            fx.Build("rain",.5,1.6,10,25,1.7,25,1,0,0,0,1,0,light,cover);Check(fx.Fall<open*.65&&fx.Fall>open*.35,"half the amount, about half the rain");
            fx.Build("snow",1,.5,10,25,1.7,25,1,0,0,0,1,0,light,cover);bool flakes=fx.Fall>200&&fx.Splashes==0;for(int i=0;i<fx.Quads.Count;i++)if(fx.Quads.Additive[i]!=0||fx.Quads.Modes[i]!=1)flakes=false;Check(flakes,"snow: soft premultiplied flakes, no splashes");
            fx.Build("drizzle",1,.6,10,25,1.7,25,1,0,0,0,1,0,light,cover);Check(fx.Fall>300,"drizzle: fine streaks");
            fx.Build("",1,0,10,25,1.7,25,1,0,0,0,1,0,light,cover);Check(fx.Quads.Count==0,"no kind: nothing");
            fx.Build("rain",1,1.6,10,25,1.7,25,1,0,0,0,1,0,light,null);Check(fx.Fall>0,"no cover: the fall stops at the ground only");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<30;k++)fx.Build(k%3==0?"rain":k%3==1?"snow":"drizzle",1,1,k*.1,25,1.7,25,1,0,0,0,1,0,light,cover);Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the fall allocates nothing per frame");
            // The lightning: rare strikes of a few pulses; Reduced effects one soft pulse.
            int strikes=0;double peak=0,reducedPeak=0;bool rising=false;int reducedRises=0;double last=0;
            for(double t=0;t<1600;t+=.01){double l=NativeWeatherFx.Lightning(t,16,77,false);if(l>.5&&!rising){strikes++;rising=true;}if(l<.005)rising=false;peak=Math.Max(peak,l);double r=NativeWeatherFx.Lightning(t,16,77,true);reducedPeak=Math.Max(reducedPeak,r);if(r>last+1e-9&&last<1e-9)reducedRises++;last=r;}
            Check(peak<=1&&peak>.95&&strikes>=60&&strikes<=100,"about three strikes in four 16 s slots ("+strikes+" in 1600 s)");
            Check(reducedPeak<=.6+1e-9&&reducedRises<=strikes+1,"Reduced effects: one soft pulse each, at most .6");
            Check(NativeWeatherFx.Lightning(5,0,1,false)==0,"no interval, no lightning");
        }

        // ---- the scattering's closed form ----
        // A practical light's fall-off (1 - r^2/R^2)^2 along a ray, integrated in closed form (NativeAdvancedLight.cginc
        // ScatterChord), against a numeric integral.
        private static float Chord(float u,float c,float r2){float u2=u*u;return u*(c*c-2f*c*u2/(3f*r2)+u2*u2/(5f*r2*r2));}
        private static void Scatter()
        {
            var rnd=new Random(3);
            for(int k=0;k<40;k++)
            {
                float R=2+(float)rnd.NextDouble()*14,h=(float)rnd.NextDouble()*R*.95f,t0=(float)(rnd.NextDouble()*30-5),D=(float)(rnd.NextDouble()*40);float r2=R*R,c=1-h*h/r2,hw=MathF.Sqrt(r2-h*h);
                float a=Math.Clamp(t0-hw,0,D)-t0,b=Math.Clamp(t0+hw,0,D)-t0;float closed=b>a?(Chord(b,c,r2)-Chord(a,c,r2))/R:0;
                double sum=0;int n=4000;for(int i=0;i<n;i++){double t=(i+.5)/n*D,u=t-t0,rr=(h*h+u*u)/r2;if(rr<1)sum+=(1-rr)*(1-rr)*D/n;}
                Check(Math.Abs(closed-sum/R)<2e-3*(1+sum/R),"the lights' glow integral (R "+R.ToString("0.0")+", h "+h.ToString("0.0")+"): "+closed+" vs "+(sum/R));
            }
        }

        // ---- source pins ----
        private static void Pins(Func<string,string> R)
        {
            string res="Assets/ChipArena/Resources/",rt="Assets/ChipArena/Runtime/";
            string inc=R(res+"NativeAdvancedLight.cginc"),block=R(res+"NativeBlockLit.shader"),src=R(res+"NativeSourceLit.shader"),zombie=R(res+"NativeZombieLit.shader"),body=R(res+"NativeVisualBodyToon.shader"),beam=R(res+"NativeVolumetricBeam.shader"),sky=R(res+"NativeNightSky.shader");
            string shared=Between(inc,"// SHARED BEGIN","// SHARED END");
            Check(!Regex.IsMatch(Code(shared),@"\btex(2D|3D)|\bsampler|\bhalf\b|\bmul\(|\bstatic\b|ShadeSH9|_WorldSpace"),"the shared section stays GLSL-compatible (the volume's tap and the SH outside it)");
            Check(Regex.Matches(Code(inc),@"\btex3Dlod\b").Count==1&&inc.Contains("sampler3D _ArenaGIVolume;")&&!Regex.IsMatch(Code(inc),@"\btex2D"),"one volume read, without derivatives");
            foreach(var f in new[]{"if(_ArenaGI.w<0.5)return float4(0.0,0.0,0.0,1.0);","if(_ArenaGI.w<0.5&&_ArenaLightning.w<0.5)return float3(0.0,0.0,0.0);","if(_ArenaScatter.w<=0.0)return col;","if(_ArenaWet.x<=0.0||n.y<0.7)return 0.0;","if(_ArenaWet.x<=0.0)return float3(0.0,0.0,0.0);","float occ=lerp(1.0,max(g.a,_ArenaGI.z),_ArenaGI.x*share);"})
                Check(inc.Contains(f),"each part off while its switch is 0: "+f);
            Check(inc.Contains("#define GI_OFFSET 0.5")&&Math.Abs(new NativeLightVolume{Cell=2}.Offset-2*.5f)<1e-6f,"a face reads the volume half a cell out (the shader's GI_OFFSET is the Core's Offset)");
            Check(inc.Contains("float ScatterChord(float u,float c,float r2){float u2=u*u;return u*(c*c-2.0*c*u2/(3.0*r2)+u2*u2/(5.0*r2*r2));}"),"the glow integral is the one checked above");
            string bshared=Between(block,"// SHARED BEGIN","// SHARED END");
            Check(bshared.Contains("#include \"NativeAdvancedLight.cginc\"")&&bshared.IndexOf("#include \"NativeAdvancedLight.cginc\"",StringComparison.Ordinal)>bshared.IndexOf("float4 _ArenaLightPos[8];",StringComparison.Ordinal),"NativeBlockLit includes it in its shared section, after its light arrays");
            string frag=Body(block,"fixed4 frag(");
            Check(frag.Contains("light+=ArenaGI(ShadeSH9(float4(nW,1.0)),i.world.xyz,nW);")&&frag.Contains("float puddle=ArenaPuddle(i.world.xyz,nW);s.albedo=ArenaWetAlbedo(s.albedo,puddle,nW);")&&frag.Contains("col+=ArenaWetSheen(")&&frag.IndexOf("c.rgb=ArenaScatter(c.rgb,i.world.xyz,_WorldSpaceCameraPos);",StringComparison.Ordinal)<frag.IndexOf("UNITY_APPLY_FOG(i.fogCoord,c);",StringComparison.Ordinal)&&frag.Contains("c.rgb=ArenaScatter("),"NativeBlockLit: the volume on its ambient, wet faces, the scattering before Unity's fog");
            Check(Body(block,"float3 ToonBlockLit(").Contains("light+=ToonOcclude(ArenaGI(ToonDiffuse(ToonAmbient(shN,shUp,shDown,nT.y)*bounce,float3(0.0,0.0,0.0),dot(nT,keyDir),atten),world,nT),occ);"),"the anime style takes the volume through its ramp and its occlusion band");
            Check(src.Contains("#include \"NativeAdvancedLight.cginc\"")&&src.Contains("o.Emission+=o.Albedo*ArenaGIBody(i.worldPos,normalize(i.worldNormal));")&&Body(src,"void StageTone(").Contains("if(_StageLit<.5)ArenaScatterFinal(i.worldPos,color);")&&Regex.Matches(src,"#pragma").Count==3,"SourceLit: the volume on its lit parts, the scattering, never on a stage");
            Check(zombie.Contains("#include \"NativeAdvancedLight.cginc\"")&&zombie.Contains("o.Emission+=o.Albedo*ArenaGIBody(i.worldPos,normalize(i.worldNormal));")&&Body(zombie,"void ZombieTone(").Contains("if(_StageLit<.5)ArenaScatterFinal(i.worldPos,color);"),"ZombieLit the same");
            Check(Regex.Matches(body,"#include \"NativeAdvancedLight.cginc\"").Count==2&&body.Contains("o.Emission+=o.Albedo*ArenaGIBody(i.worldPos,n);")&&body.Contains("light+=ArenaGIBody(i.world,n);")&&body.Contains("ArenaScatterFinal(i.worldPos,color);")&&body.Contains("c.rgb=ArenaScatter(c.rgb,i.world,_WorldSpaceCameraPos);"),"the visual body and its outline the same");
            Check(beam.Contains("Blend One One ZWrite Off ZTest LEqual Cull Back")&&beam.Contains("#pragma multi_compile_fog")&&beam.Contains("#pragma multi_compile_instancing")&&beam.Contains("UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);")&&beam.Contains("if(_BeamParams.w>0.5)")&&beam.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));"),"the beam shader: additive, instanced, fogged out, the depth fade only where the depth texture is drawn");
            Check(sky.Contains("#pragma fragment fragFlash")&&sky.Contains("float4 fragFlash(v2f i):SV_Target {float4 c=frag(i);if(_SkyFlash>0)")&&sky.Contains("float4 frag(v2f i):SV_Target {float3 d=normalize(i.dir);if(_SkyToon.x>0.5)return float4(AnimeSky(d),1);"),"the sky's lightning wraps today's sky (0: unchanged)");
            // The runtime.
            string lighting=Code(R(rt+"NativeLighting.cs")),adv=Code(R(rt+"NativeAdvancedLighting.cs")),pres=Code(R(rt+"NativeAdvancedLightingPresentation.cs")),world=R(rt+"NativeSourcePresentation.cs"),settings=Code(R(rt+"NativeSettings.cs")),beams=Code(R(rt+"NativeVolumetricBeams.cs"));
            string apply=Body(lighting,"public static void Apply(ContentMap map,Camera camera)");Check(apply.TrimEnd().EndsWith("NativeAdvancedLighting.Apply(map,look);\n        }")||apply.Contains("NativeAdvancedLighting.Apply(map,look);"),"Apply hands the styled look to the advanced lighting");
            Check(Body(lighting,"public static void StyleOff()").Contains("NativeAdvancedLighting.Off();")&&Body(lighting,"public static void Rooftop(Camera camera,Material skybox)").Contains("NativeAdvancedLighting.Off();"),"every stage, the draft and the rooftop turn it off");
            Check(Body(adv,"public static void Apply(ContentMap m,MapLook l)").Contains("if(!on){Off();return;}")&&Body(adv,"public static void Off()").Contains("Shader.SetGlobalVector(GiId,Vector4.zero);")&&Body(adv,"public static void Off()").Contains("Shader.SetGlobalVector(ScatterId,Vector4.zero);")&&Body(adv,"public static void Off()").Contains("Shader.SetGlobalVector(WetId,Vector4.zero);")&&Body(adv,"public static void Off()").Contains("Shader.SetGlobalVector(LightningId,Vector4.zero);"),"off writes every switch to 0");
            Check(Body(adv,"private static void Bake()").Contains("System.Threading.Tasks.Task.Run(")&&Body(adv,"private static void Bake()").Contains("Time.frameCount<appliedFrame+2")&&Body(adv,"private static void Upload(").Contains("tex.SetPixelData(half,0);tex.Apply(false,true);"),"the bake runs on a worker thread a frame after the Apply; the main thread only uploads its half floats");
            Check(!Regex.IsMatch(Body(adv,"public static void Frame(float time)"),@"\bnew (?!Vector4\()")&&!Regex.IsMatch(Body(pres,"private void AdvancedLightingWorld(NativeArena arena)"),@"\bnew\b")&&!Regex.IsMatch(Body(beams,"public void Draw("),@"\bnew (?!Vector3\(|Vector4\(|Matrix4x4\(|Color\()"),"the frame allocates nothing");
            Check(settings.Contains("var tier=low?NativeGraphicsTier.Low:NativeGraphicsTiers.Pick(prefs.graphics,SystemInfo.graphicsMemorySize,SystemInfo.graphicsShaderLevel);NativeAdvancedLighting.Graphics(tier,prefs.reducedEffects);")&&settings.Contains("ChoiceGrid(NativeGraphicsTiers.Choices,NativeGraphicsTiers.Known(prefs.graphics),NativeGraphicsTiers.Labels)"),"Settings: the balanced tier, auto by the GPU, the tier handed over");
            int begin=world.IndexOf("            AdvancedLightingBegin(arena);\n",StringComparison.Ordinal),factory=world.IndexOf("            FactoryScenery(arena);\n",StringComparison.Ordinal),ward=world.IndexOf("            if(revamp)WardZeroScenery(arena);\n",StringComparison.Ordinal),end=world.IndexOf("            AdvancedLightingWorld(arena);\n",StringComparison.Ordinal),kit=world.IndexOf("HazardKitWorld(arena);",StringComparison.Ordinal);
            Check(begin>0&&begin<factory&&end>ward&&end<kit,"SourceWorld: the beam list before the maps' sceneries, the draw after them (the arena and the killcam only)");
            Check(Body(pres,"private void AdvancedLightingBegin(NativeArena arena)").Contains("if(!s.Any||LowGraphics)return;"),"nothing on the low tier");
            foreach(var (file,hook) in new[]{("NativeFactoryPresentation.cs","fx.Beams=Beams;fx.Build("),("NativeFactoryPresentation.cs","factoryStage.Beams=AdvancedBeams;"),("NativeFactoryPresentation.cs","factoryStage.Beams=null;factoryStage.Backdrop("),("NativeBrickfallPresentation.cs","brickfallFx.Beams=AdvancedBeams;brickfallFx.Build("),("NativeCyberCityPresentation.cs","metroFx.BeamList=AdvancedBeams;metroFx.ExternalRain=AdvancedRain&&atmosphere;metroFx.Build("),
                ("NativeExpeditionCityPresentation.cs","townFx.Beams=AdvancedBeams;townFx.Build("),("NativeExpeditionStationPresentation.cs","stationFx.Beams=AdvancedBeams;stationFx.Build("),("NativeIronworksPresentation.cs","iwFx.Beams=AdvancedBeams;iwFx.Music=iwMusic;iwFx.Build("),("NativeWardZeroPresentation.cs","wzFx.Beams=AdvancedBeams;wzFx.Build(")})
                Check(R(rt+file).Contains(hook),file+": "+hook);
            Check(Code(R("Assets/ChipArena/Core/NativeCyberCityFx.cs")).Contains("if(!ExternalRain){RainFx(scene,states,time,quality,low);SplashFx(scene,time,quality,low);}"),"Cyber City's rain stands down only for the advanced weather");
            Check(Code(R("Assets/ChipArena/Runtime/ArenaPresentation.cs")).Contains("DisposeAdvancedLighting();"),"its pieces are released with the presentation");
            foreach(var f in new[]{"Core/NativeAdvancedLighting.cs","Core/NativeLightVolume.cs","Core/NativeLightBeams.cs","Core/NativeWeather.cs","Runtime/NativeAdvancedLighting.cs","Runtime/NativeAdvancedLightingPresentation.cs","Runtime/NativeVolumetricBeams.cs","Resources/NativeAdvancedLight.cginc","Resources/NativeVolumetricBeam.shader"})
                Check(R("Assets/ChipArena/"+f+".meta").Contains("guid: "),f+" has its meta");
        }
    }
}
#endif
