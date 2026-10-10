#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Placeholder batch 5b: the killed summon's jitter and voxel break-up and the minionHit puff (Core/NativeSummonBreak.cs),
    // the ceiling-field hit holograms (Core/NativeCeilingField.cs, Resources/NativeCeilingField.shader) and the PvE chip
    // pickup glow, YOURS label and light beam (Core/NativePickupFx.cs, Resources/NativePickupGlow.shader) against what the
    // pinned client/chaos-visuals.ts, summon-visuals.ts, ceiling-field.ts and pve/pickups.ts draw
    // (Tests/summon-ceiling-pickup-parity.json, exported by tools/export_summon_ceiling_pickup.mjs), plus scans of the
    // Runtime hooks and shaders so nothing falls back to the first port's palette cube cloud or bar beam.
    internal static class SummonCeilingPickupChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Summon/ceiling/pickup parity: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-5){Check(Math.Abs(a-b)<=tolerance+2e-6*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static bool B(JsonElement e,string name)=>e.GetProperty(name).GetBoolean();
        private static int I(JsonElement e,string name)=>e.GetProperty(name).GetInt32();
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        public static void Run(string root,ConversionContent data)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/summon-ceiling-pickup-parity.json")));var json=document.RootElement;
            Summon(json.GetProperty("summon"));Breakups();
            Ceiling(json.GetProperty("ceiling"));
            Pickup(json.GetProperty("pickup"),data);Canvas();
            Hooks(root,json.GetProperty("ceiling").GetProperty("glsl").GetString());
            Console.WriteLine("PASS summon, ceiling and pickup fx ("+count+" checks: summon hit puff, corpse jitter, break-up time, gib bursts and hide; ceiling hologram timeline, cap, colours, plane mapping and shader maths; pickup glow canvas, YOURS label, opacity, tier colours and beam; runtime hooks and shaders)");
        }

        // ---- summons ---------------------------------------------------------------------------------------------------
        private static void Summon(JsonElement s)
        {
            var look=s.GetProperty("look");Near(NativeSummonBreak.BreakAt,D(look,"breakAt"),"SUMMON_LOOK.death.breakAt");Near(NativeSummonBreak.Gone,D(look,"gone"),"SUMMON_LOOK.death.gone");
            foreach(bool toy in new[]{false,true})
            {
                var k=look.GetProperty(toy?"toy":"zombie");Near(toy?NativeSummonBreak.ToyScale:NativeSummonBreak.ZombieScale,D(k,"scale"),"summon scale");
                int i=0;foreach(var g in k.GetProperty("gib").EnumerateArray())Check(NativeSummonBreak.Gib(toy,i++)==g.GetInt32(),"gib colour "+i+(toy?" toy":" zombie"));Check(i==NativeSummonBreak.Bursts,"three gib colours");
            }
            // minionHit: a puff on each kind, nothing for a stale event or a target that is not a summon.
            foreach(var h in s.GetProperty("hits").EnumerateArray())
            {
                string kind=h.GetProperty("kind").GetString();float age=(float)D(h,"age");var burst=h.GetProperty("burst");bool fresh=NativeSummonBreak.Fresh(10.1f,10.1f-age);
                bool drawn=fresh&&NativeSummonBreak.Summon(kind);Check(drawn==(burst.ValueKind!=JsonValueKind.Null),"minionHit puff drawn: "+kind+" age "+age);
                if(!drawn)continue;double[] at=A(h.GetProperty("pos")),bp=A(burst.GetProperty("pos"));for(int j=0;j<3;j++)Near(at[j],bp[j],"puff at the hit");
                Check(NativeSummonBreak.HitColor(kind=="toy")==I(burst,"color")&&NativeSummonBreak.HitCount==I(burst,"count"),"puff colour and count "+kind);Near(NativeSummonBreak.HitPower,D(burst,"power"),"puff power");
            }
            Check(!NativeSummonBreak.Summon("siren-add")&&!NativeSummonBreak.Summon("slender")&&!NativeSummonBreak.Summon(null),"only zombies and toy soldiers are summons");
            Check(NativeSummonBreak.SummonModel("summon-toy",out bool t1)&&t1&&NativeSummonBreak.SummonModel("summon-zombie",out bool t2)&&!t2&&!NativeSummonBreak.SummonModel("horde-3",out _)&&!NativeSummonBreak.SummonModel(null,out _),"summon death models");
            var kill=s.GetProperty("killHit");Check(kill.ValueKind!=JsonValueKind.Null&&I(kill,"color")==NativeSummonBreak.ZombieHitColor,"the killing hit still puffs (the rig exists for its event)");
            // The corpse: jitter from breakAt - .2, hidden and three bursts at breakAt, gone at .9.
            var dead=s.GetProperty("dead");var memory=new NativeSummonBreakups();int fires=0;
            foreach(var f in s.GetProperty("frames").EnumerateArray())
            {
                double sourceAge=D(f,"age");var bursts=new List<JsonElement>();foreach(var b in f.GetProperty("bursts").EnumerateArray())bursts.Add(b);int used=0;
                foreach(var (id,seed,toy) in new[]{("z1",1,false),("t1",2,true)})
                {
                    var d=dead.GetProperty(id);double born=D(d,"at");float age=(float)((born+sourceAge)-born);var start=A(d.GetProperty("pos"));var r=f.GetProperty("rigs").GetProperty(id);string at=" "+id+" @"+sourceAge;
                    bool due=memory.Due(seed,seed,age);
                    if(!B(r,"present")){Check(!due&&age>=NativeSummonBreak.Gone,"rig gone at SUMMON_LOOK.death.gone"+at);continue;}
                    Check(NativeSummonBreak.RigShown(age)==B(r,"visible"),"rig shown until breakAt"+at);
                    var pos=A(r.GetProperty("pos"));Near(pos[0]-start[0],NativeSummonBreak.Shake(age),"corpse jitter"+at);Near(pos[1],start[1],"no vertical jitter");Near(pos[2],start[2],"no depth jitter");
                    if(!due)continue;fires++;
                    for(int i=0;i<NativeSummonBreak.Bursts;i++)
                    {
                        Check(used<bursts.Count,"a break-up burst for each gib colour"+at);var b=bursts[used++];NativeSummonBreak.Burst(toy,i,out float lift,out int color,out int n);
                        var bp=A(b.GetProperty("pos"));Near(bp[0],start[0],"burst x"+at);Near(bp[1],start[1]+lift,"burst height "+i+at);Near(bp[2],start[2],"burst z"+at);
                        Check(color==I(b,"color")&&n==I(b,"count"),"burst colour and count "+i+at);Near(NativeSummonBreak.BurstPower,D(b,"power"),"burst power");
                    }
                }
                Check(used==bursts.Count,"no other bursts @"+sourceAge);
            }
            Check(fires==2,"each corpse breaks up exactly once");
        }
        private static void Breakups()
        {
            var m=new NativeSummonBreakups();
            Check(!m.Due(5,9,.2f)&&m.Due(5,9,.76f)&&!m.Due(5,9,.8f),"a death bursts once");
            Check(!m.Due(5,9,.1f)&&m.Due(5,9,.75f),"replaying it from before breakAt (the killcam) bursts again");
            Check(!m.Due(6,1,.95f)&&!m.Due(6,1,1.4f),"a corpse first seen after it is gone never bursts");
            Check(m.Due(7,1,.8f)&&!m.Due(7,1,.85f),"first seen during the break-up window it bursts");
            for(int i=0;i<NativeSummonBreakups.Capacity*2;i++)Check(m.Due(100+i,i,.77f),"ring keeps working past its capacity "+i);
            Check(!m.Due(100+NativeSummonBreakups.Capacity*2-1,NativeSummonBreakups.Capacity*2-1,.8f),"the newest deaths are remembered");
        }

        // ---- ceiling field ---------------------------------------------------------------------------------------------
        private static void Ceiling(JsonElement c)
        {
            var plane=c.GetProperty("plane");Near(NativeCeilingField.Size,D(plane,"width"),"plane width");Near(NativeCeilingField.Size,D(plane,"height"),"plane height");
            var rot=A(plane.GetProperty("rotation"));Near(rot[0],-Math.PI/2,"plane faces down and up (rotation.x = -PI/2)");Near(rot[1],0,"plane yaw");
            var pos=A(plane.GetProperty("pos"));var ev=A(plane.GetProperty("event"));Near(pos[1],ev[1]-NativeCeilingField.Drop,".025 m under the hit");Near(pos[0],ev[0],"plane x");Near(pos[2],ev[2],"plane z");
            Check(B(plane,"transparent")&&!B(plane,"depthWrite")&&B(plane,"doubleSide")&&B(plane,"additive")&&!B(plane,"fog"),"additive, no depth write, both faces, no fog");
            var field=c.GetProperty("field");Near(NativeCeilingField.FieldHeight,D(field,"height"),"CEILING_FIELD.height");Near(A(field.GetProperty("point"))[1],NativeCeilingField.FieldHeight,"ceiling-hit events sit on the field plane");
            // The Unity quad: corner p = (uv - .5) * 7 at Unity (X - px, height - .025, Z - py) is the source world corner mirrored.
            float hx=-(float)ev[0],hz=(float)ev[2];int matched=0;
            foreach(var corner in plane.GetProperty("corners").EnumerateArray())
            {
                var v=A(corner);float px=(float)((v[0]-.5)*7),py=(float)((v[1]-.5)*7);
                for(int k=0;k<4;k++){NativeCeilingField.Corner(k,out float cx,out float cy);if(Math.Abs(cx-px)>1e-4||Math.Abs(cy-py)>1e-4)continue;matched++;
                    Near(hx-cx,-v[2],"corner x (mirrored)");Near(NativeCeilingField.FieldHeight-NativeCeilingField.Drop,v[3],"corner height");Near(hz-cy,v[4],"corner z");}
            }
            Check(matched==4,"the four plane corners");
            // The timeline: update(time), then this frame's ceiling-hit events (born now); at most 24, oldest dropped.
            var f=new NativeCeilingField();
            foreach(var frame in c.GetProperty("frames").EnumerateArray())
            {
                float time=(float)D(frame,"time");f.Update(time);
                foreach(var e in frame.GetProperty("events").EnumerateArray())
                {
                    if(e.GetProperty("type").GetString()!="ceiling-hit")continue;var p=A(e.GetProperty("pos"));var color=e.GetProperty("color");
                    if(NativeCeilingField.FreshEvent(time,time))f.Add((float)p[0],(float)p[2],time,NativeCeilingField.ColorOf(color.ValueKind==JsonValueKind.Null?0:color.GetInt32()));
                }
                var meshes=frame.GetProperty("meshes");Check(f.Count==meshes.GetArrayLength(),"hologram count @"+time+": "+f.Count+" != "+meshes.GetArrayLength());int i=0;
                foreach(var m in meshes.EnumerateArray())
                {
                    var h=f[i++];var mp=A(m.GetProperty("pos"));Near(h.X,mp[0],"hologram x @"+time);Near(h.Z,mp[2],"hologram z @"+time);Near(NativeCeilingField.FieldHeight-NativeCeilingField.Drop,mp[1],"hologram height");
                    Near(time-h.Born,D(m,"age"),"hologram age @"+time);NativeCeilingField.Display(h.Color,1,out double r,out double g,out double b);var tint=A(m.GetProperty("tint"));Near(r,tint[0],"tint r");Near(g,tint[1],"tint g");Near(b,tint[2],"tint b");
                }
            }
            Check(NativeCeilingField.ColorOf(0)==0x75ddff&&NativeCeilingField.ColorOf(0xb5a3ff)==0xb5a3ff,"event colour, else 0x75ddff");
            Check(NativeCeilingField.FreshEvent(1,.6f)&&!NativeCeilingField.FreshEvent(1,.5f),"events older than .4 s draw nothing");
            NativeCeilingField.Display(0x75ddff,1.7,out double dr,out _,out _);NativeCeilingField.Display(0x75ddff,1,out double fr,out _,out _);Near(dr,fr,"alpha clamped to 1");
            // The shader maths against the asserted source GLSL.
            foreach(var sample in c.GetProperty("alpha").EnumerateArray()){var v=A(sample);Near(NativeCeilingField.Alpha(v[0],v[1],v[2]),v[3],"shader alpha at p ("+v[0]+", "+v[1]+") age "+v[2],1e-9);}
        }

        // ---- pickups ---------------------------------------------------------------------------------------------------
        private static double Channel(int hex,int shift)=>((hex>>shift)&255)/255.0;
        private static void TierLinear(ConversionContent data,string tier,float t,out double r,out double g,out double b)
        {
            if(tier=="rainbow"){NativePickupFx.Rainbow(t,out float fr,out float fg,out float fb);r=fr;g=fg;b=fb;return;}
            int hex=-1;foreach(var rarity in data.rarities)if(rarity.id==tier)hex=Convert.ToInt32(rarity.color.TrimStart('#'),16);
            Check(hex>=0,"rarity colour for "+tier);NativeBlastSim.Linear(hex,out r,out g,out b);
        }
        private static void Pickup(JsonElement p,ConversionContent data)
        {
            int label=-1,plain=-1;
            foreach(var canvas in p.GetProperty("canvases").EnumerateArray())
            {
                Check(I(canvas,"width")==NativePickupFx.Canvas&&I(canvas,"height")==NativePickupFx.Canvas,"256 px glow canvas");
                var calls=new List<JsonElement>();foreach(var call in canvas.GetProperty("calls").EnumerateArray())calls.Add(call);
                string Name(int i)=>calls[i][0].GetString();double Arg(int i,int k)=>calls[i][k].GetDouble();
                Check(Name(0)=="createRadialGradient"&&Arg(0,1)==NativePickupFx.GradientX&&Arg(0,2)==NativePickupFx.GradientY&&Arg(0,3)==0&&Arg(0,4)==NativePickupFx.GradientX&&Arg(0,5)==NativePickupFx.GradientY&&Arg(0,6)==NativePickupFx.GradientRadius,"radial gradient centre and radius");
                double[] at=new double[3],alpha=new double[3];for(int i=0;i<3;i++){Check(Name(1+i)=="addColorStop","three colour stops");at[i]=Arg(1+i,1);var css=calls[1+i][2].GetString();Check(css.StartsWith("rgba(255,255,255,",StringComparison.Ordinal),"white stops");alpha[i]=double.Parse(css.Substring(17).TrimEnd(')'),System.Globalization.CultureInfo.InvariantCulture);}
                // The Core gradient against the stops at radii through every segment.
                foreach(double t in new[]{0,.1,.25,.4,.7,.99,1.2})
                {
                    double expected=t>=1?alpha[2]:t<=at[1]?alpha[0]+(alpha[1]-alpha[0])*(t-at[0])/(at[1]-at[0]):alpha[1]+(alpha[2]-alpha[1])*(t-at[1])/(at[2]-at[1]);
                    Near(NativePickupFx.Gradient(NativePickupFx.GradientX+(float)(t*NativePickupFx.GradientRadius),NativePickupFx.GradientY),expected,"gradient alpha at "+t,1e-5);
                }
                Check(Name(5)=="fillRect"&&Arg(5,1)==0&&Arg(5,2)==0&&Arg(5,3)==NativePickupFx.Canvas&&Arg(5,4)==NativePickupFx.GradientRows,"gradient fills the top 192 rows");
                Check(NativePickupFx.Gradient(128.5f,191.5f)>=0&&NativePickupFx.Gradient(128.5f,192.5f)==0,"nothing filled below row 192");
                bool hasText=calls.Count>6;if(!hasText){plain=I(canvas,"index");continue;}label=I(canvas,"index");
                var props=new Dictionary<string,JsonElement>();JsonElement text=default;foreach(var call in calls){if(call[0].GetString()=="set")props[call[1].GetString()]=call[2];if(call[0].GetString()=="fillText")text=call;}
                Check(props["font"].GetString().StartsWith("700 "+NativePickupFx.LabelPx+"px \"Rajdhani\",\"Bahnschrift\",\"Arial Narrow\"",StringComparison.Ordinal),"700 34 px HOLO_FONT");
                Check(props["textAlign"].GetString()=="center"&&props["textBaseline"].GetString()=="middle","centred, middle baseline");
                Check(props["shadowColor"].GetString()=="rgba(255,255,255,.9)"&&Math.Abs(NativePickupFx.LabelShadowAlpha-.9)<1e-6,"white .9 shadow");Near(NativePickupFx.LabelShadowBlur,props["shadowBlur"].GetDouble(),"shadow blur");
                Check(props["fillStyle"].GetString()=="#ffffff","white label");
                Check(text[1].GetString()==NativePickupFx.Label&&text[2].GetDouble()==NativePickupFx.LabelX&&text[3].GetDouble()==NativePickupFx.LabelY,"YOURS at (128, 226)");
            }
            Check(label>=0&&plain>=0&&label!=plain,"a plain and a labelled glow texture");
            var gm=p.GetProperty("glowMaterial");Check(B(gm,"additive")&&!B(gm,"depthWrite")&&!B(gm,"toneMapped")&&B(gm,"fog")&&D(gm,"rotation")==0,"glow sprite: additive, no depth write, not tone mapped, fogged");
            var centre=A(gm.GetProperty("center"));Check(centre[0]==.5&&centre[1]==.5,"glow sprite centred");
            foreach(var s in p.GetProperty("samples").EnumerateArray())
            {
                float t=(float)D(s,"t");string tier=s.GetProperty("tier").GetString();bool mine=B(s,"mine");string at=" "+tier+(mine?" mine":" other")+" @"+t;
                var glow=s.GetProperty("glow");Near(NativePickupFx.GlowSize,A(glow.GetProperty("scale"))[0],"glow size");Near(NativePickupFx.GlowSize,A(glow.GetProperty("scale"))[1],"glow size");
                Near(NativePickupFx.GlowLift,A(glow.GetProperty("pos"))[1],"glow under the chip");Near(NativePickupFx.GlowOpacity(mine,t),D(glow,"opacity"),"glow opacity"+at);
                Check(I(glow,"canvas")==(mine?label:plain),"YOURS only on the owner's pickup"+at);
                TierLinear(data,tier,t,out double r,out double g,out double b);var gc=A(glow.GetProperty("color"));Near(r,gc[0],"glow colour r"+at,1e-4);Near(g,gc[1],"glow colour g"+at,1e-4);Near(b,gc[2],"glow colour b"+at,1e-4);
                var beam=s.GetProperty("beam");float progress=NativeMachineRules.Pickup(default,default,(float)D(s,"born"),t).progress;
                Check(NativePickupFx.BeamShown(tier,progress)==B(beam,"visible"),"beam for epic and above once landed"+at);Near(NativePickupFx.BeamOpacity(mine,t),D(beam,"opacity"),"beam opacity"+at);
                var bc=A(beam.GetProperty("color"));Near(r,bc[0],"beam colour"+at,1e-4);Near(g,bc[1],"beam colour"+at,1e-4);Near(b,bc[2],"beam colour"+at,1e-4);
                double[] world=A(beam.GetProperty("world")),group=A(s.GetProperty("group"));Near(world[1],0,"beam stands on the floor"+at);Near(world[0],group[0],"beam under the chip");Near(world[2],group[2],"beam under the chip");
            }
            var bm=p.GetProperty("beam");var par=bm.GetProperty("parameters");
            Near(NativePickupFx.BeamTop,D(par,"radiusTop"),"beam top radius");Near(NativePickupFx.BeamBottom,D(par,"radiusBottom"),"beam floor radius");Near(NativePickupFx.BeamHeight,D(par,"height"),"beam height");
            Check(I(par,"radialSegments")==NativePickupFx.BeamSides&&I(par,"heightSegments")==1&&B(par,"openEnded"),"12-sided open cylinder");
            int v=0;foreach(var vertex in bm.GetProperty("positions").EnumerateArray()){var q=A(vertex);NativePickupFx.BeamVertex(v%(NativePickupFx.BeamSides+1),v/(NativePickupFx.BeamSides+1),out float x,out float y,out float z);Near(x,q[0],"beam vertex x "+v);Near(y,q[1],"beam vertex y "+v);Near(z,q[2],"beam vertex z "+v);v++;}
            Check(v==2*(NativePickupFx.BeamSides+1),"beam vertex count");
            var index=new List<int>();foreach(var i in bm.GetProperty("index").EnumerateArray())index.Add(i.GetInt32());int n=NativePickupFx.BeamSides+1,w=0;
            for(int column=0;column<NativePickupFx.BeamSides;column++){int a=column,bb=n+column,cc=n+column+1,d=column+1;foreach(int expected in new[]{a,bb,d,bb,cc,d})Check(index[w++]==expected,"beam faces "+column);}
            Check(B(bm,"side")&&B(bm,"additive")&&!B(bm,"depthWrite")&&B(bm,"transparent")&&!B(bm,"toneMapped")&&B(bm,"fog"),"beam: additive, translucent, both faces, fogged");
            foreach(var tier in p.GetProperty("tiers").EnumerateArray())
            {
                string name=tier.GetProperty("tier").GetString();float t=(float)D(tier,"t");TierLinear(data,name,t,out double r,out double g,out double b);var c=A(tier.GetProperty("linear"));
                Near(r,c[0],"tierColor r "+name+" @"+t,1e-4);Near(g,c[1],"tierColor g "+name+" @"+t,1e-4);Near(b,c[2],"tierColor b "+name+" @"+t,1e-4);
            }
            var beamTiers=new HashSet<string>();foreach(var x in p.GetProperty("beamTiers").EnumerateArray())beamTiers.Add(x.GetString());
            foreach(var tier in new[]{"common","uncommon","rare","epic","legendary","mythic","rainbow"})Check(NativePickupFx.BeamTier(tier)==beamTiers.Contains(tier),"BEAM_TIERS "+tier);
        }
        // The canvas composition itself: the shadow blur, source-over, quantisation, row order and the glyph raster.
        private static void Canvas()
        {
            const int n=NativePickupFx.Canvas;var plain=new float[n*n];NativePickupFx.Glow(plain,null);
            Near(plain[96*n+128],NativePickupFx.Gradient(128.5f,96.5f),"gradient at the centre pixel");Near(plain[96*n+128],.95,"centre alpha",.02);Check(plain[200*n+128]==0&&plain[0]==0,"no glow outside the circle");
            var block=new float[n*n];for(int y=216;y<236;y++)for(int x=100;x<156;x++)block[y*n+x]=1;
            var blurred=new float[n*n];NativePickupFx.Blur(block,blurred,n,n,4);double sum=0,sumBlur=0;for(int i=0;i<n*n;i++){sum+=block[i];sumBlur+=blurred[i];}Near(sumBlur,sum,"the blur keeps the coverage",1e-3*sum);
            Check(blurred[226*n+96]>.05&&blurred[226*n+96]<.5&&blurred[226*n+86]<.01,"sigma 4 shadow fades out ~3 sigma past the text");
            var mine=new float[n*n];NativePickupFx.Glow(mine,block);
            Near(mine[226*n+128],1,"text over its shadow");Near(mine[226*n+96],blurred[226*n+96]*NativePickupFx.LabelShadowAlpha,"the shadow alone, .9 white",1e-5);Near(mine[96*n+128],plain[96*n+128],"the label leaves the glow");
            var rgba=new byte[n*2*n*4];NativePickupFx.Write(plain,rgba,n*2,0);NativePickupFx.Write(mine,rgba,n*2,n);
            int top=((n-1-96)*n*2+128)*4;Check(rgba[top+3]==(byte)Math.Round(plain[96*n+128]*255)&&rgba[top]==255,"canvas row 96 is texture row 159, 8-bit alpha, white");
            int label=((n-1-226)*n*2+n+128)*4;Check(rgba[label+3]==255&&rgba[((n-1-226)*n*2+128)*4+3]==0,"YOURS only in the right half");
            int clear=((n-1-250)*n*2+5)*4;Check(rgba[clear]==0&&rgba[clear+3]==0,"transparent texels are black, as the un-premultiplied canvas");
            // A fully opaque 4 x 4 atlas glyph covers exactly its quad.
            var atlas=new byte[16];for(int i=0;i<16;i++)atlas[i]=255;var cover=new float[n*n];
            NativePickupFx.Stamp(cover,atlas,4,4,new NativePickupFx.Glyph{X0=10,X1=20,Top=30.5f,Bottom=40,TopLeftU=.125f,TopLeftV=.875f,TopRightU=.875f,TopRightV=.875f,BottomLeftU=.125f,BottomLeftV=.125f,BottomRightU=.875f,BottomRightV=.125f});
            Near(cover[35*n+15],1,"glyph interior");Check(cover[35*n+25]==0&&cover[45*n+15]==0,"nothing outside the glyph");Near(cover[30*n+15],.5,"half-covered edge row",1e-6);
        }

        // ---- runtime hooks and shaders -----------------------------------------------------------------------------------
        private static void Hooks(string root,string glsl)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string summon=R("NativeSummonPresentation.cs");
            Check(summon.Contains("summonBreakups.Due(death.id,death.seed,age)")&&summon.Contains("NativeSummonBreak.Burst(toy,i,out float lift,out int color,out int count);SourceBurst(death.position+Vector3.up*lift,color,count,NativeSummonBreak.BurstPower);"),"the break-up fires the three source bursts once");
            Check(summon.Contains("if(summon&&age>=NativeSummonBreak.BreakAt)return true;")&&summon.Contains("death.position-Vector3.right*NativeSummonBreak.Shake(age)"),"the rig hides at breakAt and jitters before it (mirrored x)");
            Check(!summon.Contains("VoxelCloud(")&&!summon.Contains("age>=.9f"),"no palette cube cloud from .9 s");
            Check(summon.Contains("SourceBurst(Vec(e.Position),NativeSummonBreak.HitColor(toy),NativeSummonBreak.HitCount,NativeSummonBreak.HitPower);")&&summon.Contains("m.Pve==null&&NativeSummonBreak.Summon(m.Kind)"),"minionHit puffs on summons only");
            string ceiling=R("NativeCeilingFieldPresentation.cs");
            Check(ceiling.Contains("Resources.Load<Shader>(\"NativeCeilingField\")")&&ceiling.Contains("e.Kind==EventKind.CeilingHit&&NativeCeilingField.FreshEvent(now,e.Time))field.Add(")&&ceiling.Contains("field.Update(now);"),"ceiling holograms from CeilingHit events on the presentation clock");
            Check(ceiling.Contains("ceilingField?.Render(arena,camera,now);SummonHitPuffs(arena,now);"),"the event world fx run both effects");
            string arena=R("ArenaPresentation.cs");
            Check(arena.Contains("blasts?.Render(arena,camera,now,BlastQuality,paused,fieldTelegraphs);EventWorldFx(arena,camera,fields);")&&arena.Contains("DisposeDevEffects();DisposeEventWorldFx();"),"hooked into RenderGlow (host, guests, killcam) and disposed");
            string machine=R("NativeMachinePresentation.cs");
            Check(machine.Contains("glow.Glow(")&&machine.Contains("glow.Beam(")&&machine.Contains("glow?.Draw(DeathFx()?.PopFont);"),"pickup glow and beam drawn");
            Check(machine.Contains("bool mine=k.Owner!=null&&k.Owner.Seat.id==arena.LocalId;")&&machine.Contains("NativePickupFx.GlowOpacity(mine,t)")&&machine.Contains("NativePickupFx.BeamOpacity(mine,t)")&&machine.Contains("NativePickupFx.BeamShown(k.Tier,pose.progress)"),"YOURS and the opacities follow the local seat");
            Check(!machine.Contains("Beam(new Vector3(k.Position.x")&&!machine.Contains("HSVToRGB")&&machine.Contains("NativePickupFx.Rainbow(t,"),"no amber bar beam; the rainbow is setHSL's");
            string glow=R("NativePickupGlowPresentation.cs");
            Check(glow.Contains("Resources.Load<Shader>(\"NativePickupGlow\")")&&glow.Contains("NativePickupFx.Glow(mine,label)")&&glow.Contains("NativePickupFx.Stamp(coverage,alpha,atlas.width,atlas.height,g)")&&glow.Contains("NativePickupFx.BeamVertex(column,row,"),"the glow texture and beam from Core");
            // The native match camera is untagged (Camera.main is null in a match and the killcam): the glow faces the drawing
            // camera from the view matrix, and the CPU-built SPLAT! pop uses RenderGlow's camera.
            Check(!machine.Contains("Camera.main")&&glow.Contains("mesh.SetUVs(1,corners);")&&glow.Contains("Vertex(centre,color,new Vector4(u0,0,1,0),new Vector2(-h,-h));"),"glow quads carry corner offsets, no Camera.main");
            Check(R("NativeVentDeathPresentation.cs").Contains("var fx=DeathFx();var eye=WorldEye;")&&ceiling.Contains("if(camera!=null)worldEye=camera;"),"the SPLAT! pop faces RenderGlow's camera");
            Check(R("NativeCombatHud.cs").Contains("if(presentation?.PickupLabelDrawn!=true)foreach(var k in arena.Pve.Pickups)"),"the flat HUD YOURS only stands in without the textured label");
            foreach(var file in new[]{"Runtime/NativeCeilingFieldPresentation.cs","Runtime/NativePickupGlowPresentation.cs","Core/NativeSummonBreak.cs","Core/NativeCeilingField.cs","Core/NativePickupFx.cs","Resources/NativeCeilingField.shader","Resources/NativePickupGlow.shader"})
            {string path=Path.Combine(root,"Assets/ChipArena",file);Check(File.Exists(path)&&File.Exists(path+".meta"),file+" and its meta");}
            string ceilingShader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeCeilingField.shader"));
            // Every statement of the source GLSL appears in the CG pass (vec2 -> float2, fract -> frac), except the documented rewrites.
            var rewrites=new Dictionary<string,string>{{"vec2 p=(uvField-.5)*7.","float2 p=i.field.xy;"},{"float ring=exp(-pow((d-radius)/.09,2.))","float k=(d-radius)/.09;float ring=exp(-k*k);"},
                {"float fade=pow(max(0.,1.-age/1.05),1.5)","float left=max(0.,1.-age/1.05);float fade=left*sqrt(left);"},{"gl_FragColor=vec4(tint,(grid*reveal*.36*scan+ring*.85+flash)*fade)","float alpha=saturate((grid*reveal*.36*scan+ring*.85+flash)*fade);"}};
            string body=glsl.Substring(glsl.IndexOf('{')+1).TrimEnd('}');int statements=0;
            foreach(var statement in body.Split(';',StringSplitOptions.RemoveEmptyEntries))
            {
                string cg=statement.Replace("vec2","float2").Replace("fract(","frac(");statements++;
                Check(ceilingShader.Contains(cg+";")||rewrites.TryGetValue(statement,out string replaced)&&ceilingShader.Contains(replaced),"ceiling shader keeps the source statement: "+statement);
            }
            Check(statements==10,"ten source shader statements");
            Check(ceilingShader.Contains("Blend One One")&&ceilingShader.Contains("ZWrite Off")&&ceilingShader.Contains("Cull Off")&&!ceilingShader.Contains("multi_compile_fog")&&ceilingShader.Contains("#pragma target 3.0"),"ceiling shader: additive, both faces, no fog, target 3.0");
            string glowShader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativePickupGlow.shader"));
            Check(glowShader.Contains("float3 world=mul(unity_ObjectToWorld,v.vertex).xyz+UNITY_MATRIX_V[0].xyz*v.corner.x+UNITY_MATRIX_V[1].xyz*v.corner.y;")&&glowShader.Contains("float2 corner:TEXCOORD1;"),"glow shader billboards along the view matrix");
            Check(glowShader.Contains("Blend One One")&&glowShader.Contains("Cull Off")&&glowShader.Contains("#pragma multi_compile_fog")&&glowShader.Contains("_MainTex")&&glowShader.Contains("#pragma target 3.0"),"glow shader: additive, both faces, fogged, textured, target 3.0");
            Check(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("new[]{\"NativeCeilingField\",\"NativePickupGlow\"}"),"both shaders build-checked");
        }
    }
}
#endif
