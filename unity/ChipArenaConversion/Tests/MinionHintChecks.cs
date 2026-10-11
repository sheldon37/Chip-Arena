#if ARENA_TESTS
using System;
using System.IO;
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
    // standing or below level 2), the grin mesh (canvas mapping, pieces, glows, fold-free strokes, distant widening) and the
    // runtime hooks.
    internal static class MinionHintChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Minion hint: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-6){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/minion-hint-parity.json")));var json=document.RootElement;
            Levels(json);Frames(json.GetProperty("frames"));Trail(json.GetProperty("trail"));Slots();Glyph();Hooks(root);
            Console.WriteLine("PASS minion hint ("+count+" checks: hintLevel by age; seam, halo and level 3 boost; grin 25 m gate, opacity and lift; trail gate, flame and cadence at 30/60/144 Hz and reduced; replicated start; frame-rate and client independent trail slots; grin mesh pieces, glows and fold-free strokes; runtime hooks)");
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
        private static void Glyph()
        {
            var pieces=NativeMinionHint.Glyph.Pieces;float[] x=NativeMinionHint.Glyph.X,y=NativeMinionHint.Glyph.Y,ox=NativeMinionHint.Glyph.OffsetX,oy=NativeMinionHint.Glyph.OffsetY,a=NativeMinionHint.Glyph.A;var index=NativeMinionHint.Glyph.Index;
            Check(pieces.Length==6&&pieces[0].Glow&&!pieces[1].Glow&&pieces[2].Glow&&!pieces[3].Glow&&pieces[4].Glow&&!pieces[5].Glow&&pieces[3].Stroke>0&&pieces[5].Stroke>0&&pieces[1].Stroke==0&&pieces[0].Stroke==0,"pieces in the canvas's order: mouth glow, mouth, lip glow, lips, teeth glow, teeth");
            int end=0;foreach(var piece in pieces){Check(piece.Start==end&&piece.Count>0&&piece.Indices>0&&piece.Indices%3==0,"pieces tile the vertices");end+=piece.Count;for(int i=piece.First;i<piece.First+piece.Indices;i++)Check(index[i]>=piece.Start&&index[i]<piece.Start+piece.Count,"a piece indexes its own vertices");}
            Check(end==x.Length&&x.Length<4000,"a few thousand vertices at most ("+x.Length+")");
            for(int v=0;v<x.Length;v++)Check(a[v]>=0&&a[v]<=1&&NativeMinionHint.Glyph.R[v]>=0&&NativeMinionHint.Glyph.R[v]<=1&&NativeMinionHint.Glyph.G[v]>=0&&NativeMinionHint.Glyph.B[v]<=1,"colours and alpha in range");
            // The canvas mapping: path (x + 28, y - 48) on 256 x 96 shown .78 x .29 m about its centre; the grin is symmetric.
            Near(NativeMinionHint.Glyph.MetresX(100),0,"the grin's axis is the sprite's centre",1e-12);Near(NativeMinionHint.Glyph.MetresY(96),0,"canvas row 48 is the sprite's centre",1e-12);
            var mouth=pieces[1];double mx0=1,mx1=-1,my0=1,my1=-1;for(int v=mouth.Start;v<mouth.Start+mouth.Count;v++){mx0=Math.Min(mx0,x[v]);mx1=Math.Max(mx1,x[v]);my0=Math.Min(my0,y[v]);my1=Math.Max(my1,y[v]);Check(a[v]>.98,"the mouth filled twice at .9");}
            Near(mx0,NativeMinionHint.Glyph.MetresX(22),"mouth left corner",1e-6);Near(mx1,NativeMinionHint.Glyph.MetresX(178),"mouth right corner",1e-6);Near(my1,NativeMinionHint.Glyph.MetresY(70),"mouth corners' height",1e-6);Near(my0,NativeMinionHint.Glyph.MetresY(104.5),"mouth's lowest point",2e-4);
            double area=0;for(int i=mouth.First;i<mouth.First+mouth.Indices;i+=3)area+=Math.Abs(Cross(index[i],index[i+1],index[i+2],x,y,ox,oy,0))/2;
            Near(area,NativeSirenSmile.Area(NativeSirenSmile.Grin)*NativeMinionHint.Glyph.Width/256*NativeMinionHint.Glyph.Height/96,"the mouth covers the grin path",area*.002);
            // The violet glows: peaked near the mouth and the strokes, fading to nothing at their grids' edges; symmetric.
            var peaks=new double[3];for(int g=0;g<3;g++){var piece=pieces[g*2];double x0=1,x1=-1,y0=1,y1=-1;for(int v=piece.Start;v<piece.Start+piece.Count;v++){peaks[g]=Math.Max(peaks[g],a[v]);x0=Math.Min(x0,x[v]);x1=Math.Max(x1,x[v]);y0=Math.Min(y0,y[v]);y1=Math.Max(y1,y[v]);}
                for(int v=piece.Start;v<piece.Start+piece.Count;v++)if(x[v]<=x0+1e-6||x[v]>=x1-1e-6||y[v]<=y0+1e-6||y[v]>=y1-1e-6)Check(a[v]<.03,"glow "+g+" fades out at its grid's edge ("+a[v]+")");}
            Check(peaks[0]>.6&&peaks[0]<.95&&peaks[1]>.4&&peaks[1]<.9&&peaks[2]>.3&&peaks[2]<.8,"glow peaks (mouth "+peaks[0]+", lips "+peaks[1]+", teeth "+peaks[2]+")");
            double left=0,right=0;for(int v=pieces[0].Start;v<pieces[0].Start+pieces[0].Count;v++){if(x[v]<-.05)left+=a[v];if(x[v]>.05)right+=a[v];}Near(left,right,"the mouth glow is symmetric",left*.02);
            // Strokes: one winding throughout at their own width and widened (no fold, so a translucent grin has no seams); the
            // lips keep the canvas's miter spike at the corners.
            foreach(int p in new[]{3,5})foreach(double k in new[]{1,NativeMinionHint.Glyph.MaxWiden})
            {
                int pos=0,neg=0;for(int i=pieces[p].First;i<pieces[p].First+pieces[p].Indices;i+=3){double c=Cross(index[i],index[i+1],index[i+2],x,y,ox,oy,k);if(c>1e-12)pos++;else if(c<-1e-12)neg++;}
                Check(pos==0||neg==0,(p==3?"lips":"teeth")+" stroke folds at scale "+k+" ("+pos+" / "+neg+")");
            }
            double lipX=0;for(int v=pieces[3].Start;v<pieces[3].Start+pieces[3].Count;v++)lipX=Math.Max(lipX,x[v]+ox[v]);
            Check(lipX>NativeMinionHint.Glyph.MetresX(178+6)&&lipX<NativeMinionHint.Glyph.MetresX(178+9),"the lips' miter spike at the corner ("+lipX+")");
            double lipW=NativeMinionHint.Glyph.LipWidth*NativeMinionHint.Glyph.Width/256;Near(pieces[3].Stroke,lipW,"lip stroke width",1e-9);
            // Distant strokes widen to a pixel (at most twice), fading as they widen.
            Check(NativeMinionHint.Glyph.StrokeScale(lipW,.005)==1&&Math.Abs(NativeMinionHint.Glyph.StrokeScale(lipW,lipW*1.5)-1.5)<1e-12&&NativeMinionHint.Glyph.StrokeScale(lipW,1)==2&&NativeMinionHint.Glyph.StrokeScale(0,1)==1,"stroke widening");
        }
        private static double Cross(int a,int b,int c,float[] x,float[] y,float[] ox,float[] oy,double k)
        {
            double ax=x[a]+ox[a]*k,ay=y[a]+oy[a]*k,bx=x[b]+ox[b]*k,by=y[b]+oy[b]*k,cx=x[c]+ox[c]*k,cy=y[c]+oy[c]*k;return (bx-ax)*(cy-ay)-(cx-ax)*(by-ay);
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var source=R("Assets/ChipArena/Runtime/NativeSourcePresentation.cs");Check(source.Contains("add.Possessed?add.PossessedAt:float.NaN")&&source.Contains("pos.y-(add.Crawl?.44f:.85f)"),"SourceWorld hands the replicated possession start and the floor to the hint");
            var fx=R("Assets/ChipArena/Runtime/NativeMinionFxPresentation.cs");
            Check(fx.Contains("NativeMinionHint.Level(true,job.PossessedAt,t)")&&fx.Contains("NativeMinionHint.GlyphShown(level,Vector3.Distance(feet,eye))")&&fx.Contains("feet.y=job.Ground+1")&&fx.Contains("NativeMinionHint.GlyphOpacity(level,t)")&&fx.Contains("NativeMinionHint.GlyphLift(t)"),"the grin by level, 25 m from the floor + 1 m, its opacity and lift");
            Check(fx.Contains("NativeMinionHint.Brightness(level)")&&fx.Contains("if(level>0)"),"seam and halo at every level, brighter at 3");
            Check(fx.Contains("NativeMinionHint.Trail(WorldFlames(),hordeTrailRandom,id,possessedAt,hordeTrailClock,hordeClock,")&&fx.Contains("Mathf.Clamp01(EffectsQuality)")&&fx.Contains("if(possessed&&body&&float.IsNaN(death))HordeTrail("),"the trail drips into the world flame pool on the presentation clock, at the effects quality, while the body is drawn");
            Check(fx.Contains("HordeGrin(draw.Effects,")&&fx.Contains("quality>=.5f")&&fx.Contains("NativeMinionHint.Glyph.StrokeScale(piece.Stroke,pixel)"),"the grin in the effect layer, glows off with reduced effects, strokes widened far away");
            Check(fx.Split("new NativeMinionHint.SlotRandom()").Length==2,"one trail stream, made once");
            Check(fx.Contains("if(level>0&&!hordeGrinQueued)")&&fx.Contains("ThreadPool.QueueUserWorkItem(_=>NativeMinionHint.Glyph.Warm())"),"the grin mesh builds off the main thread when a possession first shows");
        }
    }
}
#endif
