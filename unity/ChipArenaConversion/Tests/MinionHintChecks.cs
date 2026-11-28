#if ARENA_TESTS
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Siren minion possession hints (Core/NativeMinionHint.cs, Runtime/NativeMinionFxPresentation.cs) against what the pinned
    // client/siren/minions/hints.ts and client/pve/horde-view.ts do (Tests/minion-hint-parity.json, exported by
    // tools/export_minion_hint_fixtures.mjs): hintLevel by possession age, the seam and halo at each level with level 3's boost,
    // the grin's 25 m gate, opacity and lift over the head, the trail's gate and flame and how many flames a walk drips at
    // several frame rates and qualities. Then the Unity rules: the replicated start (an older host's frame keeps level 1), the
    // trail's slots (frame-rate independent, the same flames on host, guest and killcam replay, nothing on a rewind, a jump,
    // standing or below level 2), the grin baked against Chromium's hints.ts canvas (the fixture's `grin`; level 1 of the mip
    // chain is the canvas's size), faded as one image over the hall, its mip chain and its background bake, and the runtime hooks.
    internal static class MinionHintChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Minion hint: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-6){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/minion-hint-parity.json")));var json=document.RootElement;
            Levels(json);Frames(json.GetProperty("frames"));Trail(json.GetProperty("trail"));Slots();Glyph(json.GetProperty("grin"));Hooks(root);
            Console.WriteLine("PASS minion hint ("+count+" checks: hintLevel by age; seam, halo and level 3 boost; grin 25 m gate, opacity and lift; trail gate, flame and cadence at 30/60/144 Hz and reduced; replicated start; frame-rate and client independent trail slots; grin baked from the source canvas, faded as one image, mips, background bake; runtime hooks)");
        }
        private static void Levels(JsonElement json)
        {
            Check(json.GetProperty("range").GetDouble()==NativeMinionHint.GlyphRange,"HINT_GLYPH_RANGE 25");var levels=A(json.GetProperty("levels"));
            Check(levels.Length==3&&levels[0]==0&&levels[1]==NativeMinionHint.Level2At&&levels[2]==NativeMinionHint.Level3At,"HINT_LEVELS 0, 15, 35");
            foreach(var a in json.GetProperty("ages").EnumerateArray()){double age=a.GetProperty("age").GetDouble();Check(NativeMinionHint.Level(age)==a.GetProperty("level").GetInt32(),"hintLevel("+age+")");}
            Check(NativeMinionHint.Level(double.NaN)==0,"no level without an age");
            // The replicated start: unpossessed 0; a start of 0 (an older host's frame, the field's default) keeps level 1.
            Check(NativeMinionHint.Level(false,100,140)==0&&NativeMinionHint.Level(true,0,500)==1&&NativeMinionHint.Level(true,100,99.9)==0&&NativeMinionHint.Level(true,100,100)==1&&NativeMinionHint.Level(true,100,115)==2&&NativeMinionHint.Level(true,100,135)==3,"levels from the replicated possession start");
            Check(NativeMinionHint.Brightness(1)==1&&NativeMinionHint.Brightness(2)==1&&NativeMinionHint.Brightness(3)==1.35,"level 3 is 1.35 brighter");
        }
        private static void Frames(JsonElement frames)
        {
            int shown=0,hidden=0,levels=0;
            foreach(var f in frames.EnumerateArray())
            {
                int level=f.GetProperty("level").GetInt32();double t=f.GetProperty("t").GetDouble(),dist=f.GetProperty("dist").GetDouble(),phase=f.GetProperty("phase").GetDouble();bool cracked=f.GetProperty("face").GetString()=="cracked";
                var seam=f.GetProperty("seam");var halo=f.GetProperty("halo");var glyph=f.GetProperty("glyph");string tag="level "+level+" at "+t+" s, "+dist+" m";
                Check(seam.GetProperty("visible").GetBoolean()==(level>0)&&halo.GetProperty("visible").GetBoolean()==(level>0),"seam and halo shown from level 1 ("+tag+")");
                Check(glyph.GetProperty("visible").GetBoolean()==NativeMinionHint.GlyphShown(level,dist),"grin gate ("+tag+")");
                if(level==0){levels|=1;continue;}levels|=1<<level;
                NativeMinionFx.Hint(cracked,NativeMinionFx.HintPulse(phase,t),out _,out _,out _,out _,out _,out _,out double so,out double hs,out double ho,NativeMinionHint.Brightness(level));
                Near(so,seam.GetProperty("opacity").GetDouble(),"seam opacity ("+tag+")",1e-6);Near(ho,halo.GetProperty("opacity").GetDouble(),"halo opacity ("+tag+")",1e-6);Near(hs,halo.GetProperty("scale").GetDouble(),"halo size ("+tag+")",1e-6);
                if(!NativeMinionHint.GlyphShown(level,dist)){hidden++;continue;}shown++;
                Near(NativeMinionHint.GlyphOpacity(level,t),glyph.GetProperty("opacity").GetDouble(),"grin opacity ("+tag+")",1e-6);
                var pos=A(glyph.GetProperty("pos"));var head=A(f.GetProperty("head"));var scale=A(glyph.GetProperty("scale"));
                Near(pos[0],head[0],"grin over the head x ("+tag+")",1e-6);Near(pos[2],head[2],"grin over the head z ("+tag+")",1e-6);Near(pos[1]-head[1],NativeMinionHint.GlyphLift(t),"grin lift ("+tag+")",2e-6);
                Near(scale[0],NativeMinionHint.Glyph.Width,"grin width",1e-9);Near(scale[1],NativeMinionHint.Glyph.Height,"grin height",1e-9);
            }
            Check(levels==15&&shown>=40&&hidden>=40,"fixtures cover every level and both sides of the 25 m gate ("+shown+" shown, "+hidden+" gated)");
        }
        private static void Trail(JsonElement trail)
        {
            Check(trail.GetProperty("level").GetInt32()==NativeMinionHint.TrailLevel&&trail.GetProperty("speed").GetDouble()==NativeMinionHint.TrailSpeed&&trail.GetProperty("height").GetDouble()==NativeMinionHint.TrailHeight,"horde-view.ts trail gate: level 2, .2 m/s, .25 m over the floor");
            Check(trail.GetProperty("factor").GetInt32()*trail.GetProperty("dripRate").GetDouble()==NativeMinionHint.TrailRate&&trail.GetProperty("dripRate").GetDouble()==NativeGoreFlames.DripRate,"the trail drips at twice gore.ts's stump rate");
            int runs=0;
            foreach(var r in trail.GetProperty("runs").EnumerateArray())
            {
                double q=r.GetProperty("quality").GetDouble(),fps=r.GetProperty("fps").GetDouble();int frames=r.GetProperty("frames").GetInt32(),want=r.GetProperty("flames").GetInt32();
                var f=new NativeFlameField(512,new NativeShotRandom(5).Next);var random=new NativeMinionHint.SlotRandom();int n=0;double start=20;
                for(int k=0;k<frames;k++){double a=start+k/fps,b=start+(k+1)/fps;n+=NativeMinionHint.Trail(f,random,7,1,a,b,1.5,-2,1.5,-2,.85,0,1.2,q);}
                Check(Math.Abs(n-want)<=1,"a "+frames+" frame walk at "+fps+" Hz, quality "+q+": "+n+" flames, the source "+want);
                var first=r.GetProperty("first");var pos=A(first.GetProperty("pos"));var vel=A(first.GetProperty("vel"));
                Near(f.Pos[1],pos[1],"trail flame height",1e-6);Near(f.Pos[0],pos[0],"trail flame x",1e-6);Near(f.Pos[2],pos[2],"trail flame z",1e-6);
                Near(f.Vel[0],vel[0],"trail flame drift",1e-9);Near(f.Vel[1],vel[1],"trail flame fall",1e-6);Near(f.Life[0],first.GetProperty("life").GetDouble(),"trail flame life",1e-6);Near(f.Size[0],first.GetProperty("size").GetDouble(),"trail flame size",1e-6);
                Near(f.Buoy[0],first.GetProperty("buoyancy").GetDouble(),"trail flame buoyancy",1e-6);Near(f.Drag[0],first.GetProperty("drag").GetDouble(),"trail flame drag",1e-6);
                Check(first.GetProperty("tone").GetString()=="black","the trail is black flame");NativeBlastSim.Linear(NativeFlameField.Fade(NativeFlameTone.Black),out double fr,out _,out _);Near(f.C1[0],fr,"black flame fades to violet smoke",1e-5);
                runs++;
            }
            Check(runs==12,"trail runs at three rates, two lengths and two qualities");
        }
        private static void Slots()
        {
            // The same walk at 24, 30, 50, 60 and 144 Hz drips the same flames: same slots, same jitter, same places (linear walk).
            double p=100,t0=121.3,t1=124.9,vx=.9,vz=-.6,x0=3,z0=-1;double X(double t)=>x0+vx*(t-t0);double Z(double t)=>z0+vz*(t-t0);
            NativeFlameField Walk(double fps,double from,double to,double q,int id=9)
            {
                var f=new NativeFlameField(256,()=>throw new Exception("the trail draws only from its slot stream"));var random=new NativeMinionHint.SlotRandom();
                int frames=(int)Math.Round((to-from)*fps);for(int k=0;k<frames;k++){double a=from+k/fps,b=from+(k+1)/fps;NativeMinionHint.Trail(f,random,id,p,a,b,X(a),Z(a),X(b),Z(b),0,vx,vz,q);}
                return f;
            }
            var reference=Walk(60,t0,t1,1);int slots=reference.Cursor;Check(slots==(int)(NativeMinionHint.TrailSlot(p,t1,1)-NativeMinionHint.TrailSlot(p,t0,1)),"one flame per slot ("+slots+")");
            Check(Math.Abs(slots-(t1-t0)*NativeMinionHint.TrailRate)<=1,"12 flames a second");
            foreach(double fps in new[]{24.0,30,50,144})
            {
                var other=Walk(fps,t0,t1,1);Check(other.Cursor==slots,"the same slots at "+fps+" Hz");
                for(int i=0;i<slots*3;i++){Near(other.Pos[i],reference.Pos[i],"slot place at "+fps+" Hz",2e-5);Check(other.Rot[i]==reference.Rot[i]&&other.Spin[i]==reference.Spin[i]&&other.C0[i]==reference.C0[i],"slot jitter at "+fps+" Hz");}
            }
            // A killcam replay of the last second (or a guest joining late) drips exactly the host's flames for those slots.
            var replay=Walk(60,t1-1.0,t1,1);long first=NativeMinionHint.TrailSlot(p,t1-1.0,1)-NativeMinionHint.TrailSlot(p,t0,1);
            for(int i=0;i<replay.Cursor;i++)for(int c=0;c<3;c++){int a=(int)(first+i)*3+c,b=i*3+c;Near(replay.Pos[b],reference.Pos[a],"replay slot place",2e-5);Check(replay.Rot[b]==reference.Rot[a]&&replay.C0[b]==reference.C0[a],"replay slot jitter");}
            Check(replay.Cursor>=11&&replay.Cursor<=12,"the replayed second's slots");
            // Each flame sits on the walk where the add was at its slot's time, .25 m over the floor.
            for(int i=0;i<slots;i++){double ts=NativeMinionHint.SlotTime(p,NativeMinionHint.TrailSlot(p,t0,1)+1+i,1);Near(reference.Pos[i*3],X(ts),"flame on the path x",2e-5);Near(reference.Pos[i*3+2],Z(ts),"flame on the path z",2e-5);Near(reference.Pos[i*3+1],NativeMinionHint.TrailHeight,"flame height",1e-6);}
            // Reduced effects: 3.6 a second; another add's slots look different.
            Near(NativeMinionHint.TrailPerSecond(.3),3.6,"reduced trail rate",1e-12);Near(NativeMinionHint.TrailPerSecond(0),3.6,"trail rate floor",1e-12);Check(NativeMinionHint.TrailPerSecond(1)==12,"full trail rate");
            Check(Math.Abs(Walk(60,t0,t1,.3).Cursor-(t1-t0)*3.6)<=1,"reduced effects drip 3.6 a second");
            var stranger=Walk(60,t0,t1,1,10);Check(stranger.Rot[1]!=reference.Rot[1]||stranger.Rot[4]!=reference.Rot[4],"each add its own jitter");
            // Nothing below level 2, without a replicated start, standing, over a jump or a rewind; the pool's own random comes back.
            var g=new NativeFlameField(64,new NativeShotRandom(3).Next);var keep=g.Random;var rs=new NativeMinionHint.SlotRandom();
            Check(NativeMinionHint.Trail(g,rs,9,p,p+14,p+14.9,0,0,1,0,0,1,0,1)==0,"no trail at level 1");
            Check(NativeMinionHint.Trail(g,rs,9,p,p+20,p+20.3,0,0,1,0,0,1,0,1)>0&&g.Random==keep,"a trail at level 2, the pool's random restored");
            Check(NativeMinionHint.Trail(g,rs,9,0,520,520.3,0,0,1,0,0,1,0,1)==0,"no trail from an older host's frame");
            Check(NativeMinionHint.Trail(g,rs,9,p,p+40,p+40.3,0,0,0,0,0,.15,.1,1)==0,"no trail standing (under .2 m/s)");
            Check(NativeMinionHint.Trail(g,rs,9,p,p+40,p+40.5,0,0,1,0,0,1,0,1)==0&&NativeMinionHint.Trail(g,rs,9,p,p+40.3,p+40,0,0,1,0,0,1,0,1)==0&&NativeMinionHint.Trail(g,rs,9,p,p+40,p+40,0,0,1,0,0,1,0,1)==0,"no trail over a jump, a rewind or a paused frame");
            Check(NativeMinionHint.Trail(g,rs,9,p,p+40,p+40.34,0,0,1,0,0,1,0,1)==4,"a long frame catches up its slots");
        }
        private static void Glyph(JsonElement grin)
        {
            // The bake: the 256 x 96 canvas at twice its size with Unity's whole mip chain, packed level 0 first, the same every time.
            var img=NativeMinionHint.Glyph.Bake(NativeMinionHint.Glyph.Scale);
            Check(img.Width==512&&img.Height==192&&img.Levels==10&&img.Widths[1]==256&&img.Heights[1]==96&&img.Widths[7]==4&&img.Heights[7]==1&&img.Widths[9]==1&&img.Heights[9]==1,"512 x 192 with ten mip levels");
            int total=0;for(int l=0;l<img.Levels;l++){Check(img.Offset[l]==total,"levels packed in order");total+=img.Widths[l]*img.Heights[l]*4;}Check(total==img.Rgba.Length,"the raw texture data's size ("+total+" bytes)");
            Check(NativeMinionHint.Glyph.Bake(NativeMinionHint.Glyph.Scale).Rgba.SequenceEqual(img.Rgba),"the bake is deterministic");
            // Source parity: level 1 (the canvas's own size) against Chromium's hints.ts canvas, compared premultiplied.
            Check(grin.GetProperty("w").GetInt32()==256&&grin.GetProperty("h").GetInt32()==96,"hints.ts's 256 x 96 canvas");
            byte[] src;using(var z=new DeflateStream(new MemoryStream(Convert.FromBase64String(grin.GetProperty("deflate").GetString())),CompressionMode.Decompress))using(var m=new MemoryStream()){z.CopyTo(m);src=m.ToArray();}
            Check(src.Length==256*96*4,"the canvas's texels");
            double Diff(int x,int y)
            {
                int i=(y*256+x)*4;double sa=src[i+3]/255.0;img.Texel(1,x,y,out double r,out double g,out double b,out double a);
                return Math.Max(Math.Abs(a-sa),Math.Max(Math.Abs(r*a-src[i]/255.0*sa),Math.Max(Math.Abs(g*a-src[i+1]/255.0*sa),Math.Abs(b*a-src[i+2]/255.0*sa))));
            }
            double sum=0,worst=0;int big=0,covered=0;for(int y=0;y<96;y++)for(int x=0;x<256;x++){double d=Diff(x,y);sum+=d;worst=Math.Max(worst,d);if(d>.1)big++;if(src[(y*256+x)*4+3]>0)covered++;}
            Check(sum/(256*96)<.01&&big<256*96*.005&&worst<.25&&covered>256*96/3,"the bake matches the source canvas (mean "+(sum/(256*96)).ToString("0.0000")+", "+big+" texels over .1, worst "+worst.ToString("0.000")+")");
            // Named texels (path units): the canvas's colours, as the sprite shows them over the dark hall at level 2's .68, the
            // whole image faded at once: white teeth stay white (no glow beneath them), the dark mouth stays the canvas's.
            foreach(var (name,px,py) in new[]{("tooth",91.0,100.0),("tooth",55.0,90.0),("lip",100.0,82.0),("lip",100.0,104.5),("mouth",100.0,96.0),("mouth glow",15.0,70.0),("glow below",100.0,120.0)})
            {
                int x=(int)Math.Floor(px+NativeMinionHint.Glyph.ShiftX),y=(int)Math.Floor(py+NativeMinionHint.Glyph.ShiftY),i=(y*256+x)*4;img.Texel(1,x,y,out double r,out double g,out double b,out double a);
                Check(Diff(x,y)<.03,name+" texel ("+px+", "+py+") as the canvas holds it");
                foreach(double o in new[]{.68,1.0}){double[] bg={26/255.0,22/255.0,32/255.0},mine={r,g,b},theirs={src[i]/255.0,src[i+1]/255.0,src[i+2]/255.0};double sa=src[i+3]/255.0;
                    for(int c=0;c<3;c++)Near(mine[c]*a*o+bg[c]*(1-a*o),theirs[c]*sa*o+bg[c]*(1-sa*o),name+" over the hall at opacity "+o,6/255.0);}
            }
            img.Texel(1,(int)(91+28),(int)(100-48),out double tr,out double tg,out double tb,out double ta);Check(ta>.99&&tr>.99&&tg>.99&&tb>.99,"a tooth is opaque white, so at .68 it shows the hall through it, never the glow");
            // The mip chain: each texel the premultiplied mean in linear light of the texels it covers above (to a byte).
            for(int l=1;l<img.Levels;l++)
            {
                int w=img.Widths[l],h=img.Heights[l],pw=img.Widths[l-1],ph=img.Heights[l-1];
                for(int y=0;y<h;y+=Math.Max(1,h/7))for(int x=0;x<w;x+=Math.Max(1,w/9))
                {
                    double[] mean=new double[4];int n=0;for(int yy=y*ph/h;yy<(y+1)*ph/h;yy++)for(int xx=x*pw/w;xx<(x+1)*pw/w;xx++){img.Texel(l-1,xx,yy,out double r,out double g,out double b,out double a);mean[0]+=NativeMinionHint.Glyph.Linear(r)*a;mean[1]+=NativeMinionHint.Glyph.Linear(g)*a;mean[2]+=NativeMinionHint.Glyph.Linear(b)*a;mean[3]+=a;n++;}
                    img.Texel(l,x,y,out double mr,out double mg,out double mb,out double ma);Near(ma,mean[3]/n,"mip "+l+" alpha",1.5/255);
                    if(ma>.2){Near(NativeMinionHint.Glyph.Linear(mr)*ma,mean[0]/n,"mip "+l+" red",.012);Near(NativeMinionHint.Glyph.Linear(mb)*ma,mean[2]/n,"mip "+l+" blue",.012);}
                }
            }
            // The grin is symmetric about the canvas's middle (path x 100), and its shadows end at the canvas's edge.
            double mirror=0;for(int y=0;y<192;y++)for(int x=0;x<256;x++){img.Texel(0,x,y,out _,out _,out _,out double a0);img.Texel(0,511-x,y,out _,out _,out _,out double a1);mirror=Math.Max(mirror,Math.Abs(a0-a1));}
            Check(mirror<.05,"the grin is symmetric ("+mirror.ToString("0.000")+")");
            img.Texel(0,256,0,out _,out _,out _,out double edge);img.Texel(0,256,60,out _,out _,out _,out double rim);Check(edge<rim&&edge<.2,"the mouth's glow fades toward the canvas's top edge");
            // Prepare bakes once on the thread pool; Ready hands over the same image and never waits for it.
            NativeMinionHint.Glyph.Prepare();NativeMinionHint.Glyph.Prepare();var clock=System.Diagnostics.Stopwatch.StartNew();
            while(NativeMinionHint.Glyph.Ready==null&&NativeMinionHint.Glyph.Failure==null&&clock.Elapsed.TotalSeconds<30)System.Threading.Thread.Sleep(5);
            Check(NativeMinionHint.Glyph.Failure==null&&NativeMinionHint.Glyph.Ready!=null&&NativeMinionHint.Glyph.Ready.Rgba.SequenceEqual(img.Rgba)&&NativeMinionHint.Glyph.Ready.Levels==img.Levels,"the background bake is the same image");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var source=R("Assets/ChipArena/Runtime/NativeSourcePresentation.cs");Check(source.Contains("add.Possessed?add.PossessedAt:float.NaN")&&source.Contains("pos.y-(add.Crawl?.44f:.85f)"),"SourceWorld hands the replicated possession start and the floor to the hint");
            var fx=R("Assets/ChipArena/Runtime/NativeMinionFxPresentation.cs");
            Check(fx.Contains("NativeMinionHint.Level(true,job.PossessedAt,t)")&&fx.Contains("NativeMinionHint.GlyphShown(level,Vector3.Distance(feet,eye))")&&fx.Contains("feet.y=job.Ground+1")&&fx.Contains("NativeMinionHint.GlyphLift(t)"),"the grin by level, 25 m from the floor + 1 m, its lift");
            Check(fx.Contains("NativeMinionHint.Brightness(level)")&&fx.Contains("if(level>0)"),"seam and halo at every level, brighter at 3");
            Check(fx.Contains("NativeMinionHint.Trail(WorldFlames(),hordeTrailRandom,id,possessedAt,hordeTrailClock,hordeClock,")&&fx.Contains("Mathf.Clamp01(EffectsQuality)")&&fx.Contains("if(possessed&&body&&float.IsNaN(death))HordeTrail("),"the trail drips into the world flame pool on the presentation clock, at the effects quality, while the body is drawn");
            Check(fx.Split("new NativeMinionHint.SlotRandom()").Length==2,"one trail stream, made once");
            // The grin fades as one image: one quad of the baked texture per grin, its opacity applied to the whole texel once.
            Check(fx.Contains("HordeGrin(camera,c,(float)NativeMinionHint.GlyphOpacity(level,t))")&&fx.Contains("hordeGrinBlock.SetFloat(GrinOpacityId,opacity)")&&fx.Contains("Graphics.DrawMesh(hordeGrinQuad,")&&!fx.Contains("HordeGrin(draw.")&&fx.Split("Graphics.DrawMesh(").Length==2,"the grin is one quad at the hint's opacity");
            Check(fx.Contains("new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,image.Levels,false)")&&fx.Contains("LoadRawTextureData(image.Rgba)")&&fx.Contains("Apply(false,true)")&&fx.Contains("Resources.Load<Shader>(\"NativeMinionGrin\")")&&fx.Contains("RenderQueue.Transparent+2"),"the baked sRGB texture with its own mips, drawn after the effect layer");
            var shader=R("Assets/ChipArena/Resources/NativeMinionGrin.shader");
            Check(shader.Contains("Blend One OneMinusSrcAlpha")&&shader.Contains("ZWrite Off")&&shader.Contains("return float4(c.rgb*k,t.a*k);")&&shader.Contains("float k=saturate(_Opacity);")&&shader.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(fog*t.a,1))"),"the shader fades the premultiplied texel as one, fogged as the sprite");
            Check(R("Assets/ChipArena/Editor/NativeBuildChecks.cs").Contains("Resources.Load<Shader>(\"NativeMinionGrin\")"),"the grin shader is build-checked");
            // The bake starts long before level 2 and never on the main thread's draw: the first PvE frame or horde add, and the
            // grin is left out until Ready has it.
            Check(fx.Contains("if(arena.Pve!=null)NativeMinionHint.Glyph.Prepare();")&&fx.Contains("if(rig==null||sourceModels==null)return;NativeMinionHint.Glyph.Prepare();"),"the bake starts with the PvE arena or the first add");
            Check(fx.Contains("var image=NativeMinionHint.Glyph.Ready;")&&fx.Contains("bool grin=hordeFxCount>0&&HordeGrinReady();")&&fx.Contains("if(level>=2&&grin)")&&!fx.Contains("QueueUserWorkItem")&&!fx.Contains("NativeMinionHint.Glyph.Bake("),"the grin waits for the bake instead of running it");
            Check(R("Assets/ChipArena/Runtime/NativeDevEffects.cs").Contains("DisposeHordeGrin();"),"the grin's texture, quad and material are released");
        }
    }
}
#endif
