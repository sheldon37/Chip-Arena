#if ARENA_TESTS
using System;
using System.Linq;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Ward Zero's floor vents (Core/NativeStageVentFx.cs) and the Marked seeker's point light (Core/NativeSeekerLight.cs) against
    // what the pinned client/siren/stage.ts WardZeroVisuals and client/siren/fx.ts draw (Tests/stage-vent-parity.json, exported by
    // tools/export_stage_vent_fixtures.mjs): the rim geometry and material, frame-by-frame rim colour and opacity, lid angles
    // and pits, the black flame pool after the song's warnings and eruptions, a Ward Zero opening and PvE entries (replayed on the
    // source's random stream), the boss song schedule and entry openings the runtime feeds them (NativeBossTrack.Vent,
    // NativeVentRules), plus scans of the Runtime hooks so none of them falls back to the first port's cube stand-ins.
    internal static class StageVentChecks
    {
        private sealed class Mulberry
        {
            private uint a;public Mulberry(uint seed){a=seed;}
            public double Next(){unchecked{a+=0x6D2B79F5u;uint t=(a^(a>>15))*(1u|a);t=(t+(t^(t>>7))*(61u|t))^t;return (t^(t>>14))/4294967296.0;}}
        }
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Stage vent parity: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-6){Check(Math.Abs(a-b)<=tolerance+2e-6*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static bool B(JsonElement e,string name)=>e.GetProperty(name).GetBoolean();
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/stage-vent-parity.json")));var json=document.RootElement;
            var rim=json.GetProperty("rim");Rim(rim,root);
            using var pveDocument=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")));
            var track=JsonSerializer.Deserialize<NativeBossTrack>(pveDocument.RootElement.GetProperty("bossMusic").GetRawText(),new JsonSerializerOptions{IncludeFields=true});
            var vents=rim.GetProperty("layout");var x=new double[NativeStageVentFx.Count];var z=new double[NativeStageVentFx.Count];int n=0;
            foreach(var v in vents.EnumerateArray()){x[n]=D(v,"x");z[n]=D(v,"z");Check(v.GetProperty("group").GetInt32()==n%2,"vent "+n+" fires with group n%2 (NativeBossTrack.Vent(song, i%2))");n++;}
            Check(n==NativeStageVentFx.Count,"eight vents");
            foreach(var run in json.GetProperty("runs").EnumerateObject())Replay(run.Name,run.Value,track,x,z,rim.GetProperty("capacity").GetInt32());
            Seeker(json.GetProperty("seeker"));
            Hooks(root);
            Console.WriteLine("PASS stage vents ("+count+" checks: rim square frame and glow material, rim warn red to white, open flicker, lid swing and rattle, pits, warning wisps, eruption burst and jets, entry smoke and reduced counts replayed on the source random stream; boss song schedule and entry openings; seeker point light; runtime hooks)");
        }
        private static void Rim(JsonElement r,string root)
        {
            var p=r.GetProperty("parameters");Near(NativeStageVentFx.RimInner,D(p,"innerRadius"),"rim inner");Near(NativeStageVentFx.RimOuter,D(p,"outerRadius"),"rim outer");
            Check(NativeStageVentFx.RimSegments==p.GetProperty("thetaSegments").GetInt32()&&p.GetProperty("phiSegments").GetInt32()==1,"rim segments");
            int i=0;foreach(var v in r.GetProperty("vertices").EnumerateArray()){var e=A(v);NativeStageVentFx.RimVertex(i,out double x,out double y,out double z);Near(x,e[0],"rim vertex "+i+" x");Near(y,e[1],"rim vertex "+i+" y");Near(z,e[2],"rim vertex "+i+" z");i++;}
            Check(i==NativeStageVentFx.RimVertices,"rim vertex count");
            var tris=NativeStageVentFx.RimTriangles;i=0;foreach(var t in r.GetProperty("indices").EnumerateArray())Check(tris[i++]==t.GetInt32(),"rim index "+(i-1));Check(i==tris.Length,"rim index count");
            Check(r.GetProperty("color").GetInt32()==NativeStageVentFx.RimWarn,"rim starts PALETTE.warn");Near(D(r,"intensity"),1,"rim intensity");Near(D(r,"flicker"),0,"rim flicker");Near(D(r,"opacity"),0,"rim hidden at rest");
            Check(B(r,"transparent")&&!B(r,"depthWrite")&&B(r,"doubleSide")&&B(r,"normalBlending")&&B(r,"fog")&&!B(r,"toneMapped"),"rim glowMaterial: transparent, no depth write, double sided, normal blend, fogged, raw");
            Check(r.GetProperty("renderOrder").GetInt32()<r.GetProperty("pitRenderOrder").GetInt32(),"the open pit draws over the rim");
            Near(D(r,"swing"),NativeStageVentFx.Swing,"VENT_SWING");Near(D(r,"entryClose"),NativeStageVentFx.EntryClose,"ENTRY_CLOSE.vent");Near(D(r,"warn"),1,"VENT_PIT.warn (NativeVentRules.Warning)");Near(D(r,"lead"),.5,"VENT_PIT.lead (NativeVentRules.Open drawn)");
            // The grates stand where the runtime draws the vents (NativeArena.StageVents, Unity axes = the map export's axes).
            var stage=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSirenStage.cs"));
            Check(stage.Contains("StageContent.entries.Where(e=>e.song>=0)")&&stage.Contains("Vec(e.from.Position())"),"song vent origins come from map entry data");
            using var content=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")));
            var vents=content.RootElement.GetProperty("entries").EnumerateArray().Where(e=>e.GetProperty("song").GetInt32()>=0).ToArray();
            Check(vents.Length==NativeStageVentFx.Count,"eight data-owned song vents");
            for(int ventIndex=0;ventIndex<vents.Length;ventIndex++)Check(vents[ventIndex].GetProperty("e").GetInt32()==ventIndex&&vents[ventIndex].GetProperty("song").GetInt32()>=0&&vents[ventIndex].GetProperty("song").GetInt32()<=1,"song side/index valid");

        }
        private static void Replay(string name,JsonElement run,NativeBossTrack track,double[] vx,double[] vz,int capacity)
        {
            var rng=new Mulberry((uint)run.GetProperty("seed").GetInt32());var fire=new NativeFlameField(capacity,rng.Next);var states=new NativeStageVentState[NativeStageVentFx.Count];for(int i=0;i<states.Length;i++)states[i]=new NativeStageVentState();
            double q=NativeStageVentFx.Quality(D(run,"quality")),time=D(run,"start"),last=0;bool ward=B(run,"ward"),song=B(run,"song");var dts=A(run.GetProperty("dts"));
            var entries=new NativeEntryView[NativeStageVentFx.Count];var openAt=new double[NativeStageVentFx.Count];var closeAt=new double[NativeStageVentFx.Count];
            foreach(var e in run.GetProperty("entries").EnumerateArray()){int k=e.GetProperty("e").GetInt32();openAt[k]=D(e,"openAt");closeAt[k]=D(e,"closeAt");entries[k]=new NativeEntryView{entry=k,kind="vent",mode=e.GetProperty("mode").GetString(),openAt=(float)openAt[k],closeAt=(float)closeAt[k]};}
            var checkpoints=run.GetProperty("checkpoints");int mark=0;int frame=0;
            double[] haz=new double[8],open=new double[8],warn=new double[8],glow=new double[8],rim=new double[8],lid=new double[8];
            foreach(var row in run.GetProperty("frames").EnumerateArray())
            {
                var r=A(row);time+=dts[frame];string at=" ["+name+" frame "+frame+" t="+time.ToString("0.000")+"]";Near(time,r[0],"time"+at,1e-6);
                Array.Copy(r,3,haz,0,8);Array.Copy(r,11,open,0,8);Array.Copy(r,19,warn,0,8);Array.Copy(r,27,glow,0,8);Array.Copy(r,35,rim,0,8);Array.Copy(r,43,lid,0,8);
                int active=(int)r[51],sent=(int)r[52],white=(int)r[53],pit=(int)r[54];
                double dt=NativeStageVentFx.Step(last,time);last=time;
                for(int i=0;i<8;i++)
                {
                    bool on=(active>>i&1)==1;var entry=(sent>>i&1)==1?entries[i]:null;string v=" vent "+i+at;
                    // What the runtime feeds the Core: the boss song schedule and the entry openings, on float clocks.
                    if(song){var h=track.Vent(time,i%2);Check(h.active==on,"song eruption"+v);Near(h.warning,haz[i],"song warning"+v,1e-5);}else Check(!on&&haz[i]==0,"no song, no eruption"+v);
                    Near(NativeVentRules.Open(entry,(float)time,ward),open[i],"drawn opening"+v,2e-4);
                    Near(ward&&entry!=null?NativeVentRules.Warning(entry,(float)time):0,warn[i],"Ward Zero warning"+v,2e-4);
                    // The rim, the open flicker, the lid and the pit from the source's own inputs.
                    bool lit=ward?NativeStageVentFx.PitActive(open[i]):entry!=null&&NativeStageVentFx.EntryActive(openAt[i],closeAt[i],time);
                    Near(lit?NativeStageVentFx.OpenGlow(open[i],time):0,glow[i],"open vent flicker"+v);
                    NativeStageVentFx.Rim(on,Math.Max(haz[i],warn[i]),glow[i],out int hex,out double opacity);Near(opacity,rim[i],"rim opacity"+v);Check((hex==NativeStageVentFx.RimOn)==((white>>i&1)==1),"rim colour"+v);
                    Near(NativeStageVentFx.LidAngle(open[i],warn[i],time),lid[i],"lid angle"+v);Check((open[i]>1e-3)==((pit>>i&1)==1),"pit shown while open"+v);
                }
                // The flames, in the source's order: each vent entry's smoke, then each vent's wisps, burst and jet, then the update.
                for(int i=0;i<8;i++)states[i].Smoke=NativeStageVentFx.EntrySmoke(fire,states[i].Smoke,vx[i],vz[i],open[i],dt,q);
                for(int i=0;i<8;i++)NativeStageVentFx.Vent(fire,states[i],vx[i],vz[i],Math.Max(haz[i],warn[i]),(active>>i&1)==1,dt,q);
                fire.Update(dt);
                Check(fire.Cursor==(int)r[1],"flame cursor "+fire.Cursor+" != "+r[1]+at);Check(fire.Live==(int)r[2],"live flames "+fire.Live+" != "+r[2]+at);
                if(mark<checkpoints.GetArrayLength()&&checkpoints[mark].GetProperty("frame").GetInt32()==frame){Pool(fire,checkpoints[mark].GetProperty("state"),name+" frame "+frame);mark++;}
                frame++;
            }
            Check(frame==dts.Length&&mark==checkpoints.GetArrayLength(),name+" frames and checkpoints replayed");
        }
        private static void Pool(NativeFlameField f,JsonElement s,string what)
        {
            Check(f.Cursor==s.GetProperty("cursor").GetInt32()&&f.Live==s.GetProperty("live").GetInt32(),what+" pool cursor/live");int burning=0;
            foreach(var slot in s.GetProperty("slots").EnumerateArray())
            {
                var e=A(slot);int i=(int)e[0],k=i*3;string at=what+" slot "+i;burning++;
                for(int c=0;c<3;c++){Near(f.Pos[k+c],e[1+c],at+" pos",2e-5);Near(f.Vel[k+c],e[4+c],at+" vel",2e-5);Near(f.Rot[k+c],e[7+c],at+" rot",2e-5);Near(f.Spin[k+c],e[10+c],at+" spin",2e-5);Near(f.C0[k+c],e[18+c],at+" c0",2e-6);Near(f.C1[k+c],e[21+c],at+" c1",2e-6);}
                Near(f.Age[i],e[13],at+" age",2e-6);Near(f.Life[i],e[14],at+" life",2e-6);Near(f.Size[i],e[15],at+" size",2e-6);Near(f.Buoy[i],e[16],at+" buoyancy",2e-6);Near(f.Drag[i],e[17],at+" drag",2e-6);
            }
            int alive=0;for(int i=0;i<f.Capacity;i++)if(f.Age[i]<f.Life[i])alive++;Check(alive==burning,what+" burning slots "+alive+" != "+burning);
        }
        private static void Seeker(JsonElement s)
        {
            Near(D(s,"created"),NativeSeekerLight.Created,"seeker light created at 4");Near(D(s,"distance"),NativeSeekerLight.Distance,"seeker light distance");Near(D(s,"decay"),NativeSeekerLight.Decay,"seeker light decay");
            Check(s.GetProperty("color").GetInt32()==NativeSeekerLight.Color,"seeker light colour");
            Check(s.GetProperty("halo").GetInt32()==0xffffff&&Math.Abs(D(s,"haloOpacity")-.95)<1e-9,"white halo .95 (drawn in NativeDevEffects)");
            var red=s.GetProperty("red");Check(red.GetArrayLength()==1&&red[0].GetProperty("color").GetInt32()==NativeSirenFx.Warn&&Math.Abs(D(red[0],"opacity")-.55)<1e-9&&Math.Abs(D(red[0],"scale")-1.3)<1e-9,"one red halo, warn .55 at 1.3");
            foreach(var f in s.GetProperty("frames").EnumerateArray())
            {
                double t=D(f,"time");var lights=f.GetProperty("lights");var shots=f.GetProperty("shots");Check(lights.GetArrayLength()==shots.GetArrayLength(),"one light per live seeker @"+t);
                Check(lights.GetArrayLength()<=NativeSeekerLight.Cap,"within the pool");
                for(int i=0;i<lights.GetArrayLength();i++)
                {
                    var l=lights[i];Near(NativeSeekerLight.Intensity(t),D(l,"intensity"),"seeker light intensity @"+t,1e-6);Near(D(l,"distance"),NativeSeekerLight.Distance,"distance");Check(l.GetProperty("color").GetInt32()==NativeSeekerLight.Color,"colour");
                    var p=A(l.GetProperty("pos"));bool on=false;foreach(var shot in shots.EnumerateArray()){var q=A(shot);if(Math.Abs(q[0]-p[0])<1e-6&&Math.Abs(q[1]-p[1])<1e-6&&Math.Abs(q[2]-p[2])<1e-6)on=true;}Check(on,"the light rides its seeker @"+t);
                }
            }
            Near(NativeBlastSim.UnityLightIntensity(NativeSeekerLight.Created),NativeSeekerLight.Created*NativeBlastSim.UnityLightGain,"converted like the other pooled source lights",1e-6);
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string world=R("NativeWorldEffects.cs"),wardFile=R("NativeWardPresentation.cs"),vents=R("NativeStageVentPresentation.cs"),dev=R("NativeDevEffects.cs");
            foreach(var stand in new[]{"FrozenUntil","Controlling","ShieldUntil","Ring(","Beam(","Add(","BossTrack.Vent"})Check(!world.Contains(stand),"no first-port stand-in in WorldEffects: "+stand);
            Check(!wardFile.Contains("Ring(")&&wardFile.Contains("StageVentEffects(arena,wardEntries)")&&wardFile.Contains("NativeStageVentFx.LidAngle(opening,"),"Ward Zero warning is the rim and wisps with the lid rattle, not a cube ring");
            foreach(var hook in new[]{"NativeStageVentFx.EntrySmoke(fire,","NativeStageVentFx.Vent(fire,","NativeStageVentFx.Rim(","WorldFlames()","FieldClock(arena)","arena.BossTrack.Vent(song,NativeArena.StageContent.entries[i].song)","song+=clock-arena.Time","NativeStageVentFx.Quality(EffectsQuality)",
                "-vents[i].x","-at.x","Resources.Load<Shader>(\"NativeFieldGlow\")","CullMode.Off","RenderQueue.Transparent","NativeFieldRules.SirenDisplay(hex,1,","NativeStageVentFx.RimVertex(","NativeStageVentFx.OpenGlow(","NativeStageVentFx.PitActive(","NativeStageVentFx.EntryActive("})
                Check(vents.Contains(hook),"vent presentation hook: "+hook);
            Check(!vents.Contains("Ring(")&&!vents.Contains("Add(7")&&!vents.Contains("cube"),"no cube rings or rising boxes");
            foreach(var hook in new[]{"SeekerLight(seekers++,at,now)","SeekerLightsOff(seekers)","SeekerLightsOff(0)","NativeSeekerLight.Intensity(now)","NativeBlastSim.UnityLightIntensity(","NativeSeekerLight.Distance","NativeSeekerLight.Cap","l.cullingMask=~(1<<10|1<<NativeSceneryBuilder.BackdropLayer)"})
                Check(dev.Contains(hook),"seeker light hook: "+hook);
            Check(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("\"NativeFieldGlow\""),"the rim's shader is build-checked");
        }
    }
}
#endif
