#if ARENA_TESTS
using System;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The sky death broadcast (Core/NativeBroadcastFx.cs, NativeDeathBroadcast.cs, NativeBroadcastEdges.Generated.cs), the Ward
    // Zero vent-fall and vent-splat deaths (Core/NativeVentDeath.cs) against what the pinned
    // client/death-announcements.ts and client/pve/vent-fall.ts draw (the PvE revive is Astra's NativeInteractionPresentation)
    // (Tests/death-fx-parity.json, exported by tools/export_death_fx_fixtures.mjs), plus scans of the Runtime hooks so none of
    // them can silently fall back to the first port's cube stand-ins.
    internal static class DeathFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Death fx parity: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-5){Check(Math.Abs(a-b)<=tolerance+2e-6*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static bool B(JsonElement e,string name)=>e.GetProperty(name).GetBoolean();
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/death-fx-parity.json")));var json=document.RootElement;
            Broadcast(json.GetProperty("broadcast"));
            Vent(json.GetProperty("vent"));
            Hooks(root);
            Console.WriteLine("PASS death fx ("+count+" checks: sky broadcast rocket, trail, stars, hologram reveal, edges, scanlines, frame, nameplate and slots; vent-fall carry, flail, drop and spin; vent-splat fall, squash, wobble, pose, dust and SPLAT!; runtime hooks)");
        }
        private static System.Numerics.Quaternion Q(JsonElement e){var v=A(e);return new System.Numerics.Quaternion((float)v[0],(float)v[1],(float)v[2],(float)v[3]);}
        private static void SameRotation(System.Numerics.Quaternion a,System.Numerics.Quaternion b,string what){Near(Math.Abs(System.Numerics.Quaternion.Dot(System.Numerics.Quaternion.Normalize(a),System.Numerics.Quaternion.Normalize(b))),1,what,2e-5);}
        private static void Vent(JsonElement v)
        {
            var fx=v.GetProperty("fx");Near(NativeVentDeath.Stumble,D(fx,"stumble"),"stumble");Near(NativeVentDeath.Gravity,D(fx,"gravity"),"shaft gravity");Near(NativeVentDeath.HideBelow,D(fx,"hideBelow"),"hide depth");
            Near(NativeVentDeath.Height,D(fx,"height"),"splat height");Near(NativeVentDeath.Lie,D(fx,"lie"),"lying centre");Near(NativeVentDeath.Squash,D(fx,"squash"),"squash");Near(NativeVentDeath.Rest,D(fx,"rest"),"rest scale");Near(NativeVentDeath.KeepAge,D(fx,"keepAge"),"keep age");
            var dust=fx.GetProperty("dust");Check(NativeVentDeath.DustHigh==dust.GetProperty("high").GetInt32()&&NativeVentDeath.DustLow==dust.GetProperty("low").GetInt32(),"dust counts");Near(NativeVentDeath.DustSpeed,D(dust,"speed"),"dust speed");Near(NativeVentDeath.DustLife,D(dust,"life"),"dust life");
            var pop=fx.GetProperty("pop");Near(NativeVentDeath.PopLife,D(pop,"life"),"pop life");Near(NativeVentDeath.PopSize,D(pop,"size"),"pop size");Near(NativeVentDeath.Drop,D(v.GetProperty("pit"),"drop"),"VENT_PIT.drop");
            // vent-fall: the carried death pose, then the scripted flail, drop and spin.
            var carried=v.GetProperty("carried");var from=new System.Numerics.Quaternion[22];int j=0;foreach(var q in carried.GetProperty("joints").EnumerateArray())from[j++]=Q(q);var fb=A(carried.GetProperty("body"));var fromBody=new System.Numerics.Vector3((float)fb[0],(float)fb[1],(float)fb[2]);
            var pose=new CapturedRig.Pose();
            foreach(var f in v.GetProperty("fall").EnumerateArray())
            {
                float age=(float)D(f,"age");string at=" @"+age;NativeVentDeath.Fall(age,pose,out float drop,out bool visible,out float spin);NativeVentDeath.Carry(pose,from,fromBody,age);
                var p=A(f.GetProperty("pos"));Near(.95-drop,p[1],"fall height"+at);Near(1.5,p[0],"fall x");Check(visible==B(f,"visible"),"shaft hide"+at);Near(.6,D(f,"yaw"),"death yaw");
                var g=A(f.GetProperty("group"));Near(0,g[0],"group x");Near(spin,g[1],"spin"+at);Near(0,g[2],"group z");
                j=0;foreach(var q in f.GetProperty("joints").EnumerateArray()){SameRotation(pose.Rotation[j],Q(q),"fall joint "+j+at);j++;}
                var body=A(f.GetProperty("body"));for(int k=0;k<3;k++)Near(pose.Position[CapturedRig.Body].Y*(k==1?1:0)+pose.Position[CapturedRig.Body].X*(k==0?1:0)+pose.Position[CapturedRig.Body].Z*(k==2?1:0),body[k],"carried body"+at);
            }
            // vent-splat: fall, impact, wobble, the held pose, dust and the pop.
            foreach(var run in v.GetProperty("splat").EnumerateArray())
            {
                int seed=run.GetProperty("seed").GetInt32();float quality=(float)D(run,"quality");int n=NativeVentDeath.DustCount(quality);Check(n==run.GetProperty("count").GetInt32(),"dust count q"+quality);Near(NativeVentDeath.KeepAge,D(run,"keepAge"),"kept body hold");
                var dirs=new NativeVentDeath.Dust[n];for(int i=0;i<n;i++)dirs[i]=NativeVentDeath.DustDirection(seed,i);float floor=.95f-NativeVentDeath.FloorOffset,ground=floor+NativeVentDeath.Lie;
                foreach(var f in run.GetProperty("frames").EnumerateArray())
                {
                    float age=(float)D(f,"age");bool keep=B(f,"keep");float shown=keep?Math.Min(age,NativeVentDeath.KeepAge):age;string at=" @"+age+(keep?" kept":"")+" seed "+seed;
                    NativeVentDeath.Splat(shown,pose,out float lift,out float sxz,out float sy,out bool landed);float since=shown-NativeVentDeath.Drop;
                    var p=A(f.GetProperty("pos"));Near(ground+lift,p[1],"splat height"+at);var s=A(f.GetProperty("scale"));Near(sxz,s[0],"squash xz"+at);Near(sy,s[1],"squash y"+at);Near(sxz,s[2],"squash z"+at);
                    var g=A(f.GetProperty("group"));Near(NativeVentDeath.SplatTilt,g[0],"face down");Near(0,g[1],"group y");Near(0,g[2],"group z");
                    j=0;foreach(var q in f.GetProperty("joints").EnumerateArray()){SameRotation(pose.Rotation[j],Q(q),"splat joint "+j+at);j++;}
                    int i=0;
                    foreach(var d in f.GetProperty("dust").EnumerateArray())
                    {
                        bool on=NativeVentDeath.DustPose(dirs[i],since,out float along,out float height,out float rx,out float ry,out float scale)&&landed;Check(on==B(d,"visible"),"dust "+i+" shown"+at);Check(B(d,"chip")==(i%5==0),"every fifth voxel in the player's colour");
                        Near(landed?NativeVentDeath.DustOpacity(since):1,D(d,"opacity"),"dust opacity"+at);
                        if(on){var dp=A(d.GetProperty("pos"));Near(1.5+dirs[i].X*along,dp[0],"dust x "+i+at);Near(floor+height,dp[1],"dust y "+i+at);Near(-2+dirs[i].Z*along,dp[2],"dust z "+i+at);Near(scale,D(d,"scale"),"dust size "+i+at);SameRotation(MeleeMath.Euler(rx,ry,0),Q(d.GetProperty("quat")),"dust spin "+i+at);}
                        i++;
                    }
                    var pp=f.GetProperty("pop");bool popped=NativeVentDeath.Pop(since,out float w,out float rise,out float opacity)&&landed;Check(popped==B(pp,"visible"),"SPLAT! shown"+at);Check(!B(pp,"depthTest")&&pp.GetProperty("renderOrder").GetInt32()==6,"SPLAT! over everything");
                    if(popped){var ps=A(pp.GetProperty("scale"));Near(w,ps[0],"SPLAT! width"+at);Near(w/2,ps[1],"SPLAT! height"+at);var pq=A(pp.GetProperty("pos"));Near(floor+rise,pq[1],"SPLAT! rise"+at);Near(1.5,pq[0],"SPLAT! x");Near(opacity,D(pp,"opacity"),"SPLAT! fade"+at);}
                }
            }
        }
        private static void Broadcast(JsonElement b)
        {
            // Layout: three fixed stages over the northern skyline, scale 5.5.
            foreach(var l in b.GetProperty("layout").EnumerateArray())
            {
                int slot=l.GetProperty("slot").GetInt32();var pos=A(l.GetProperty("pos"));NativeBroadcastFx.Frame(slot,out float x,out float y,out float z);
                Near(x,pos[0],"stage x slot "+slot);Near(y,pos[1],"stage y slot "+slot);Near(z,pos[2],"stage z slot "+slot);Near(NativeBroadcastFx.Scale,D(l,"scale"),"stage scale");
            }
            var rocket=b.GetProperty("rocket");Near(NativeBroadcastFx.RocketRadius,D(rocket,"radius"),"rocket radius");Check(NativeBroadcastFx.RocketWidth==rocket.GetProperty("width").GetInt32()&&NativeBroadcastFx.RocketHeight==rocket.GetProperty("height").GetInt32(),"rocket segments");
            Check(NativeBroadcastFx.RocketColor==rocket.GetProperty("color").GetInt32()&&NativeBroadcastFx.LineColor==rocket.GetProperty("line").GetInt32()&&NativeBroadcastFx.StarColor==rocket.GetProperty("star").GetInt32()&&NativeBroadcastFx.HoloColor==rocket.GetProperty("holo").GetInt32(),"broadcast colours");
            // The static line sets.
            var scan=b.GetProperty("scan");Check(scan.GetArrayLength()==NativeBroadcastFx.ScanLines.Length,"scanline count "+NativeBroadcastFx.ScanLines.Length);int n=0;
            foreach(var s in scan.EnumerateArray()){var v=A(s);Near(-NativeBroadcastFx.ScanHalfWidth,v[0],"scan x0");Near(NativeBroadcastFx.ScanLines[n],v[1],"scan y "+n);Near(NativeBroadcastFx.ScanZ,v[2],"scan z");Near(NativeBroadcastFx.ScanHalfWidth,v[3],"scan x1");Near(v[1],v[4],"scan level");n++;}
            var frame=b.GetProperty("frame");Check(frame.GetArrayLength()*6==NativeBroadcastFx.FrameSegments.Length,"frame segment count");n=0;
            foreach(var s in frame.EnumerateArray()){var v=A(s);for(int k=0;k<6;k++)Near(NativeBroadcastFx.FrameSegments[n*6+k],v[k],"frame segment "+n);n++;}
            // Frame by frame.
            foreach(var f in b.GetProperty("frames").EnumerateArray())
            {
                double age=D(f,"age");if(!B(f,"shown"))continue;var s=NativeBroadcastFx.Sample((float)age);string at=" @"+age;
                var r=f.GetProperty("rocket");Check(B(r,"visible")==s.Rocket,"rocket shown"+at);
                if(s.Rocket){var p=A(r.GetProperty("pos"));Near(s.RocketX,p[0],"rocket x"+at);Near(s.RocketY,p[1],"rocket y"+at);var tr=f.GetProperty("trail");var a=A(tr.GetProperty("a"));var e=A(tr.GetProperty("b"));
                    Check(B(tr,"visible"),"trail with the rocket"+at);Near(s.TrailX,a[0],"trail x"+at);Near(s.TrailY,a[1],"trail y"+at);Near(0,e[0],"trail end x");Near(s.TrailEndY,e[1],"trail end y"+at);Near(NativeBroadcastFx.TrailOpacity,D(tr,"opacity"),"trail opacity");}
                var st=f.GetProperty("stars");Check(B(st,"visible")==s.Stars,"stars shown"+at);Near(NativeBroadcastFx.StarSize*NativeBroadcastFx.Scale,D(st,"size"),"star size (x layout scale)");
                if(s.Stars){Near(s.StarOpacity,D(st,"opacity"),"star opacity"+at);int i=0;foreach(var q in st.GetProperty("points").EnumerateArray()){var v=A(q);NativeBroadcastFx.Star(i,s.Burst,out float x,out float y,out float z);Near(x,v[0],"star "+i+" x"+at);Near(y,v[1],"star "+i+" y"+at);Near(z,v[2],"star "+i+" z"+at);i++;}Check(i==NativeBroadcastFx.Stars,"72 stars");}
                var h=f.GetProperty("holo");Check(B(h,"visible")==s.Holo,"hologram shown"+at);
                if(s.Holo)
                {
                    var sc=A(h.GetProperty("scale"));Near(1,sc[0],"hologram x scale");Near(s.ScaleY,sc[1],"hologram reveal"+at);Near(s.HologramAlpha,D(h,"material"),"hologram opacity"+at,1e-6);
                    var head=A(h.GetProperty("head"));Near(s.HeadX,head[0],"head x"+at);Near(s.HeadY,head[1],"head y"+at);Near(s.ScanOffset,D(h,"linesY"),"scanline bob"+at);
                    Near(s.ScanAlpha,D(h,"lines"),"scanline opacity"+at,1e-6);Near(s.FrameAlpha,D(h,"frame"),"frame opacity"+at,1e-6);Near(s.EdgeAlpha,D(h,"edges"),"edge opacity"+at,1e-6);
                }
            }
            // Four deaths at once: the native track launches them 1.5 s apart in slots 0, 1, 2, 0 and shows the newest three.
            var track=new NativeDeathBroadcast();string[] names={"A","B","C","E"};string[] killers={null,"A","B","C"};
            foreach(var stage in b.GetProperty("stages").EnumerateArray())
            {
                float t=(float)D(stage,"time");track.Tick(t);for(int i=0;i<4;i++)if(!track.Seen(i+1))track.Add(i+1,i+1,0,20,t,names[i],killers[i]);
                var items=stage.GetProperty("items");Check(items.GetArrayLength()==track.Items.Count,"track items @"+t);int k=0;
                foreach(var item in items.EnumerateArray()){var it=track.Items[k++];Near(it.At,D(item,"at"),"launch @"+t);Check(it.Slot==item.GetProperty("slot").GetInt32()&&it.Text==item.GetProperty("text").GetString(),"slot and line @"+t+": "+it.Text);}
                var shown=stage.GetProperty("shown");int visible=0;for(int i=0;i<track.Items.Count;i++)if(track.Shown(i,t))visible++;Check(visible==shown.GetArrayLength(),"newest three shown @"+t);
                foreach(var sh in shown.EnumerateArray()){string id=sh.GetProperty("id").GetString();int idx=track.Items.FindIndex(x=>"d-"+names[x.Seed-1].ToLowerInvariant()==id);Check(idx>=0&&track.Shown(idx,t),"shown "+id+" @"+t);
                    var pos=A(sh.GetProperty("pos"));NativeBroadcastFx.Frame(track.Items[idx].Slot,out float x,out _,out _);Near(x,pos[0],"slot position "+id);}
            }
            Check(track.Items.Exists(x=>x.Upper=="E"&&x.Killer=="C"&&x.Name=="E"),"nameplate names kept per broadcast");
            // The portrait per hat: fit and edges (generated table in step with the source).
            foreach(var hat in b.GetProperty("hats").EnumerateArray())
            {
                string name=hat.GetProperty("hat").GetString();int row=NativeBroadcastEdges.HatIndex(name);Check(NativeBroadcastEdges.Hats[row]==name,"hat row "+name);
                NativeBroadcastFx.RigFit(name,out float fit,out float ox,out float oy,out float oz);var off=A(hat.GetProperty("offset"));Near(fit,D(hat,"fit"),"fit "+name,1e-5);Near(ox,off[0],"fit x "+name,1e-5);Near(oy,off[1],"fit y "+name,1e-5);Near(oz,off[2],"fit z "+name,1e-5);
                void Same(JsonElement list,float[] table,string what)
                {
                    Check(list.GetArrayLength()*7==table.Length,what+" edge count "+name);int i=0;
                    foreach(var s in list.EnumerateArray()){var v=A(s);Check((int)table[i*7]==(int)v[0]&&NativeBroadcastFx.PortraitBone((int)v[0]),what+" edge bone "+name);for(int k=1;k<7;k++)Near(table[i*7+k],v[k],what+" edge "+i+" "+name,2e-6);i++;}
                }
                Same(hat.GetProperty("body"),NativeBroadcastEdges.Body,"body");Same(hat.GetProperty("segments"),NativeBroadcastEdges.HatSegments[row],"hat");
            }
            Check(NativeBroadcastEdges.HatIndex(null)==0&&NativeBroadcastEdges.HatIndex("no-such-hat")==0,"an unknown hat draws no hat edges");
            // Text rules.
            Check(NativeBroadcastFx.Verb(true)=="was killed by"&&NativeBroadcastFx.Verb(false)=="made an oopsie.","nameplate verbs");
            Near(NativeBroadcastFx.Squeeze(1880),.5,"maxWidth squeeze");Near(NativeBroadcastFx.Squeeze(400),1,"no squeeze under 940 px");
            NativeBroadcastFx.NamePixel(0,0,out float nx,out float ny);NativeBroadcastFx.NamePixel(1024,224,out float fx,out float fy);Near(nx,-3.1,"nameplate left");Near(ny,-4.26+.678,"nameplate top");Near(fx,3.1,"nameplate right");Near(fy,-4.26-.678,"nameplate bottom");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string broadcast=R("NativeDeathBroadcastPresentation.cs"),draw=R("NativeDeathFxDraw.cs");
            Check(broadcast.Contains("NativeBroadcastFx.Sample(age)")&&broadcast.Contains("fx.Sphere(")&&broadcast.Contains("fx.Point(")&&broadcast.Contains("NativeBroadcastEdges.Body")&&broadcast.Contains("NativeBroadcastFx.ScanLines")&&broadcast.Contains("NativeBroadcastFx.FrameSegments")&&broadcast.Contains("fx.Text(fx.Quads"),"the broadcast draws the rocket, stars, edges, scanlines, frame and nameplate from Core");
            Check(!broadcast.Contains("1.485f")&&!broadcast.Contains("Add(8,")&&!broadcast.Contains("Beam("),"no cube rocket, cube stars or beam frame");
            Check(broadcast.Contains("broadcasts.Shown(index,t)"),"only the newest three broadcasts show");
            string death=R("NativeDeathPresentation.cs"),vent=R("NativeVentDeathPresentation.cs");
            Check(death.Contains("if(VentStyle(death.style)){PresentVent(")&&!death.Contains("style==\"vent-fall\"){overlay")&&!death.Contains("style==\"vent-splat\"){overlay"),"vent deaths take the scripted vent-fall.ts victim, not the sliding corpse");
            Check(vent.Contains("NativeVentDeath.Fall(")&&vent.Contains("NativeVentDeath.Splat(")&&vent.Contains("NativeVentDeath.DustPose(")&&vent.Contains("\"SPLAT!\""),"vent fall, splat, dust and SPLAT! drawn from Core");
            Check(R("NativeInteractionPresentation.cs").Contains("Vent(d.id) is VentBody vent"),"a kept vent-splat revives from the body as drawn");
            Check(draw.Contains("MeshTopology.Lines")&&draw.Contains("Resources.Load<Shader>(\"NativeDeathFx\")"),"one-pixel lines through the death fx shader");
            string shaderPath=Path.Combine(root,"Assets/ChipArena/Resources/NativeDeathFx.shader");Check(File.Exists(shaderPath)&&File.Exists(shaderPath+".meta"),"death fx shader and meta under Resources");
            string shader=File.ReadAllText(shaderPath);foreach(var name in new[]{"_MainTex","_Src","_Dst","_ZTest","_Scan","_HoloTime"})Check(shader.Contains(name),"death fx shader declares "+name);
            Check(!shader.Contains("multi_compile_fog")&&shader.Contains("_ScreenParams.y/_ScreenParams.x"),"no fog (fog:false), size-attenuated points");
            Check(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("Resources.Load<Shader>(\"NativeDeathFx\")"),"death fx shader build-checked");
            string holo=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeHologram.shader"));Check(holo.Contains("float3(.1221,.7157,1)")&&!holo.Contains("stripe"),"portrait hologram is the source's flat 0x62dcff");
        }
    }
}
#endif
