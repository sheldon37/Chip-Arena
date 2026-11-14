#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The Siren reveal's smile (owner playtest 2026-10-10: "looks like just a line"): Core/NativeSirenSmile.cs against what
    // the pinned client draws in Chromium (Tests/siren-smile-parity.json, tools/export_siren_smile_fixtures.mjs): the face's
    // path data, bounds, paint, grin origin and glow; the smile's timeline (stare flicker, the grin opening from its .04 slit,
    // the flame row's rise, the tears) sample by sample; every tongue's screen transform under its lick; the tray's spotlight,
    // booting chip and locked pulse. Then the rasterised face itself (two almond eyes and a tall toothed grin, never a bar),
    // the glow, the helpers, and scans of the runtime so the reveal cannot fall back to a stand-in.
    internal static class SirenSmileChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren smile parity: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a.ToString(CultureInfo.InvariantCulture)+" != "+b.ToString(CultureInfo.InvariantCulture));}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        private static int Rgb(string css){var m=Regex.Match(css,@"rgba?\((\d+),\s*(\d+),\s*(\d+)");return int.Parse(m.Groups[1].Value)<<16|int.Parse(m.Groups[2].Value)<<8|int.Parse(m.Groups[3].Value);}
        private static double Px(string v)=>double.Parse(v.Replace("px","").Trim(),CultureInfo.InvariantCulture);
        private static void Box(List<float[]> paths,double[] box,string what,double tolerance=.02)
        {
            NativeSirenSmile.Bounds(paths,out float x0,out float y0,out float x1,out float y1);
            Near(x0,box[0],what+" x",tolerance);Near(y0,box[1],what+" y",tolerance);Near(x1-x0,box[2],what+" width",tolerance);Near(y1-y0,box[3],what+" height",tolerance);
        }
        // Keyframe steps the comparison should not straddle (a sample within a hair of one may land on either side).
        private static bool NearStep(double k,params double[] steps){foreach(var s in steps)if(Math.Abs(k-s)<2e-3)return true;return false;}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-smile-parity.json")));var json=document.RootElement;
            Check(json.GetProperty("source").GetString()=="05950eaef80ff2483a9c44d898cad98be1a76bbc","pinned source commit");
            var vp=A(json.GetProperty("viewport"));float W=(float)vp[0],H=(float)vp[1];

            // ---- the face: siren-card.ts EYES and GRIN, its paint, filter and the grin's transform origin ----
            var face=json.GetProperty("face");var eyes=face.GetProperty("eyes");
            Check(eyes[0].GetString()==NativeSirenSmile.EyeLeftPath&&eyes[1].GetString()==NativeSirenSmile.EyeRightPath,"eye paths");
            Check(face.GetProperty("grin").GetString()==NativeSirenSmile.GrinPath,"grin path");Check(face.GetProperty("teeth").GetString()==NativeSirenSmile.TeethPath,"teeth path");
            Check(face.GetProperty("viewBox").GetString()=="0 0 200 120"&&NativeSirenSmile.ViewWidth==200&&NativeSirenSmile.ViewHeight==120,"200 x 120 view box");
            Check(NativeSirenSmile.Eyes.Count==2,"two eyes");
            for(int i=0;i<2;i++)Box(new List<float[]>{NativeSirenSmile.Eyes[i]},A(face.GetProperty("eyeBoxes")[i]),"eye "+i);
            Box(NativeSirenSmile.Grin,A(face.GetProperty("grinBox")),"grin");Box(NativeSirenSmile.TeethLine,A(face.GetProperty("teethBox")),"teeth");
            var mouthBox=A(face.GetProperty("mouthBox"));var origin=face.GetProperty("mouthOrigin").GetString().Split(' ');
            Near(NativeSirenSmile.MouthOriginX,mouthBox[0]+Px(origin[0]),"grin origin x (50% of its fill-box)",.02);Near(NativeSirenSmile.MouthOriginY,mouthBox[1]+Px(origin[1]),"grin origin y (40%)",.02);
            Near(NativeSirenSmile.MouthBoxX1-NativeSirenSmile.MouthBoxX0,mouthBox[2],"grin fill-box width",.02);Near(NativeSirenSmile.MouthBoxY1-NativeSirenSmile.MouthBoxY0,mouthBox[3],"grin fill-box height",.02);
            Check(Rgb(face.GetProperty("eyeFill").GetString())==NativeSirenSmile.FaceRgb&&Rgb(face.GetProperty("grinFill").GetString())==NativeSirenSmile.FaceRgb,"face fill #f6f3ff");
            Check(Rgb(face.GetProperty("teethStroke").GetString())==NativeSirenSmile.TeethRgb&&face.GetProperty("teethFill").GetString()=="none","teeth stroke #050409, no fill");
            Near(Px(face.GetProperty("teethWidth").GetString()),NativeSirenSmile.TeethWidth,"teeth stroke width",1e-6);
            Check(face.GetProperty("teethJoin").GetString()=="miter"&&face.GetProperty("teethCap").GetString()=="butt","teeth miter joins, butt caps");
            Near(double.Parse(face.GetProperty("teethMiter").GetString(),CultureInfo.InvariantCulture),NativeSirenSmile.MiterLimit,"miter limit",1e-6);
            var filter=Regex.Match(face.GetProperty("filter").GetString(),@"drop-shadow\(rgba\((\d+), (\d+), (\d+), ([\d.]+)\) 0px 0px ([\d.]+)px\)");Check(filter.Success,"stare drop-shadow parsed");
            Check((int.Parse(filter.Groups[1].Value)<<16|int.Parse(filter.Groups[2].Value)<<8|int.Parse(filter.Groups[3].Value))==NativeSirenSmile.GlowRgb,"glow colour #b7a8ff");
            Near(double.Parse(filter.Groups[4].Value,CultureInfo.InvariantCulture),NativeSirenSmile.GlowAlpha,"glow alpha",2e-3);Near(double.Parse(filter.Groups[5].Value,CultureInfo.InvariantCulture),NativeSirenSmile.GlowSigma,"glow blur",1e-6);
            var stare=A(face.GetProperty("stareRect"));NativeSirenSmile.Face(W,H,NativeSirenSmile.StareTop,out float fx,out float fy,out float unit);
            Near(fx,stare[0],"stare x",.01);Near(fy,stare[1],"stare y",.01);Near(unit*200,stare[2],"stare width min(96vw, 1200px)",.01);Near(unit*120,stare[3],"stare height",.01);
            Near(NativeSirenSmile.StareWidth(1067),1024.32,"stare width at a narrow canvas (96vw)",.01);

            // ---- the smile's timeline, sample by sample (1.4 s, written at its start) ----
            Smile(json,W,H);
            // ---- the flame row: every tongue's placement, paint, lick and screen transform ----
            Flames(json,W,H);
            // ---- the reveal tray: pve-spot-in, siren-boot, pve-locked ----
            foreach(var s in json.GetProperty("spotIn").EnumerateArray())
            {
                double t=D(s,"t");NativeSirenSmile.SpotIn((float)(t/.7),out float o,out float sx,out float sy,out float tx);
                Near(o,D(s,"opacity"),"spot-in opacity at "+t,2e-3);Near(sx,D(s,"sx"),"spot-in scaleX at "+t,2e-3);Near(sy,D(s,"sy"),"spot-in scaleY at "+t,2e-3);Near(tx,D(s,"tx"),"spot-in translateX at "+t,.02);
            }
            foreach(var s in json.GetProperty("sirenBoot").EnumerateArray())
            {
                double t=D(s,"t");NativeSirenSmile.SirenBoot((float)t,out float o,out float sx,out float sy,out float tx);
                Near(o,D(s,"opacity"),"siren-boot opacity at "+t,2e-3);Near(sx,D(s,"sx"),"siren-boot scaleX at "+t,2e-3);Near(sy,D(s,"sy"),"siren-boot scaleY at "+t,2e-3);Near(tx,D(s,"tx"),"siren-boot translateX at "+t,.02);
            }
            foreach(var s in json.GetProperty("pulse").EnumerateArray())
            {
                double t=D(s,"t");float o=NativeSirenSmile.LockedPulse((float)t,out float mix);Near(o,D(s,"opacity"),"locked pulse opacity at "+t,2e-3);
                int c=Rgb(s.GetProperty("color").GetString()),a=0x4e4470,b=0x6d5fb0;
                for(int sh=0;sh<=16;sh+=8){double want=((a>>sh)&255)+(((b>>sh)&255)-((a>>sh)&255))*mix;Near((c>>sh)&255,want,"locked pulse colour at "+t,1.01);}
            }

            // ---- the drawn face: two almond eyes and a tall toothed grin, never a bar ----
            Raster();
            // ---- helpers ----
            var rnd=new Random(7);
            for(int i=0;i<40;i++)
            {
                float a=(float)(rnd.NextDouble()*3+.1),b=(float)(rnd.NextDouble()*2-1),c=(float)(rnd.NextDouble()*2-1),d=(float)(rnd.NextDouble()*3+.1);if(a*d-b*c<=.05f)continue;
                NativeSirenSmile.Decompose(a,b,c,d,out float th,out float sx,out float sy,out float ph);double t1=th*Math.PI/180,t2=ph*Math.PI/180,c1=Math.Cos(t1),s1=Math.Sin(t1),c2=Math.Cos(t2),s2=Math.Sin(t2);
                // rotate(theta) . scale(sx, sy) . rotate(phi), Unity TRS rotation [[cos, -sin], [sin, cos]].
                double m00=c1*sx*c2-s1*sy*s2,m01=-c1*sx*s2-s1*sy*c2,m10=s1*sx*c2+c1*sy*s2,m11=-s1*sx*s2+c1*sy*c2;
                Near(m00,a,"decompose a",1e-4);Near(m01,b,"decompose b",1e-4);Near(m10,c,"decompose c",1e-4);Near(m11,d,"decompose d",1e-4);Check(sx>0&&sy>0,"decompose keeps positive scales");
            }
            {
                var src=new byte[5*2*4];for(int i=0;i<src.Length;i++)src[i]=(byte)rnd.Next(256);var half=NativeSirenSmile.HalveWidth(src,5,2,out int hw,0xffffff);Check(hw==3,"halved width rounds up");
                for(int y=0;y<2;y++){double sum=0,got=0;for(int x=0;x<5;x++)sum+=src[(y*5+x)*4+3];for(int x=0;x<3;x++)got+=half[(y*3+x)*4+3]*2;Near(got,sum,"halving keeps the row's alpha",6);}
            }
            var boxes=NativeSirenSmile.BoxSizes(NativeSirenSmile.GlowSigma/NativeSirenSmile.Glow.Texel,3);double variance=0;foreach(int b in boxes){Check(b%2==1,"odd box width");variance+=(b*b-1)/12.0;}
            Near(Math.Sqrt(variance)*NativeSirenSmile.Glow.Texel,NativeSirenSmile.GlowSigma,"three boxes make the 28 px Gaussian",1.5);

            Runtime(root);
            Console.WriteLine("PASS Siren smile: "+count+" checks against siren-card.ts / chip-cards.css / pve.css in Chromium, the drawn face and the reveal runtime");
        }

        private static void Smile(JsonElement json,float W,float H)
        {
            int samples=0;NativeSirenSmile.FlameBox(W,H,out float bx,out float by,out float bw,out float bh,out _,out _);
            foreach(var s in json.GetProperty("smile").EnumerateArray())
            {
                double t=D(s,"t"),k=t/1.4;samples++;
                Near(NativeSirenSmile.StareOpacity((float)k),D(s,"stare"),"stare opacity at "+t,2e-3);
                NativeSirenSmile.GrinOpen((float)k,out float mo,out float ms);Near(ms,D(s,"mouthScale"),"grin scaleX at "+t,2e-3);Near(mo,D(s,"mouthOpacity"),"grin opacity at "+t,2e-3);
                Near(NativeSirenSmile.Rise((float)k)*bh,D(s,"rise"),"flame rise at "+t,.05);
                var r=A(s.GetProperty("flameRect"));Near(bx,r[0],"flame box x",.01);Near(by+NativeSirenSmile.Rise((float)k)*bh,r[1],"flame box y at "+t,.05);Near(bw,r[2],"flame box width",.01);Near(bh,r[3],"flame box height",.01);
                var tears=s.GetProperty("tears");double[] lead={0,NativeSirenSmile.TearBLead};
                for(int i=0;i<2;i++)
                {
                    double tk=(t+lead[i])/1.4;if(NearStep(tk,.04,.05,.07,.09,.62,.63,.65))continue;float op=NativeSirenSmile.Tear((float)tk,out float dx);
                    Near(op,D(tears[i],"opacity"),"tear "+i+" opacity at "+t,1e-3);Near(dx,D(tears[i],"dx"),"tear "+i+" shift at "+t,1e-3);
                }
            }
            Check(samples>=141,"the whole 1.4 s sampled");
            var tear=json.GetProperty("tear");Near(Px(tear.GetProperty("height").GetString()),NativeSirenSmile.TearHeight*H,"tear height 9vh",.01);
            Near(Px(tear.GetProperty("tops")[0].GetString()),NativeSirenSmile.TearA*H,"tear a at 30%",.01);Near(Px(tear.GetProperty("tops")[1].GetString()),NativeSirenSmile.TearB*H,"tear b at 63%",.01);
            Check(json.GetProperty("scan").GetString().Contains("rgba(255, 255, 255, 0.03) 1px, rgba(0, 0, 0, 0) 1px, rgba(0, 0, 0, 0) 3px"),"scanlines: 1 px of #ffffff08 every 3 px");
            // The grin is a slit only for a moment: hidden until 15%, .04 wide at 16%, open by 38% and held.
            NativeSirenSmile.GrinOpen(.10f,out float o1,out _);NativeSirenSmile.GrinOpen(.16f,out float o2,out float s2);NativeSirenSmile.GrinOpen(.38f,out _,out float s3);NativeSirenSmile.GrinOpen(1,out _,out float s4);
            Check(o1==0&&Math.Abs(o2-1)<1e-4&&Math.Abs(s2-.04f)<1e-4&&Math.Abs(s3-1)<1e-4&&Math.Abs(s4-1)<1e-4,"grin: hidden, slit, open, held");
        }

        private static void Flames(JsonElement json,float W,float H)
        {
            var tongues=json.GetProperty("tongues");Check(tongues.GetArrayLength()==NativeSirenSmile.TongueCount&&NativeSirenSmile.TongueCount==27,"27 tongues (14 back, 13 front)");
            var info=json.GetProperty("flameInfo");Check(info.GetProperty("viewBox").GetString()=="0 0 400 160"&&info.GetProperty("aspect").GetString()=="xMidYMax slice"&&info.GetProperty("overflow").GetString()=="hidden","flame row: 400 x 160 sliced to its box and clipped by it");
            int nb=NativeSirenSmile.StageBack.Length/2;
            for(int i=0;i<NativeSirenSmile.TongueCount;i++)
            {
                var g=tongues[i];bool front=i>=nb;int n=front?i-nb:i;var row=front?NativeSirenSmile.StageFront:NativeSirenSmile.StageBack;
                var place=Regex.Match(g.GetProperty("place").GetString(),@"translate\(([-\d.]+) 160\) scale\(([\d.]+)\)");Check(place.Success,"tongue "+i+" placement parsed");
                Near(double.Parse(place.Groups[1].Value,CultureInfo.InvariantCulture),row[n*2],"tongue "+i+" x",1e-6);Near(double.Parse(place.Groups[2].Value,CultureInfo.InvariantCulture),row[n*2+1],"tongue "+i+" scale",1e-6);
                Check(g.GetProperty("cls").GetString()=="siren-tongue "+(front?"front":"back")+" t"+(n%7),"tongue "+i+" class");
                bool left=(n&1)==1;Check(g.GetProperty("d").GetString()==(left?NativeSirenSmile.TongueLeft:NativeSirenSmile.TongueRight),"tongue "+i+" path");
                Box(left?NativeSirenSmile.LeftTongue:NativeSirenSmile.RightTongue,A(g.GetProperty("box")),"tongue "+i+" bounds",.02);
                var box=A(g.GetProperty("box"));var o=g.GetProperty("origin").GetString().Split(' ');Near(box[0]+Px(o[0]),left?NativeSirenSmile.LeftOriginX:NativeSirenSmile.RightOriginX,"tongue "+i+" lick origin x",.02);Near(box[1]+Px(o[1]),0,"tongue "+i+" lick origin on its base",.02);
                Check(Rgb(g.GetProperty("rimFill").GetString())==(front?NativeSirenSmile.RimRgb:NativeSirenSmile.BackRimRgb),"tongue "+i+" rim fill");
                Check(Rgb(g.GetProperty("rimStroke").GetString())==(front?NativeSirenSmile.RimStrokeRgb:NativeSirenSmile.BackRimStrokeRgb),"tongue "+i+" rim stroke");
                Near(double.Parse(g.GetProperty("rimStrokeOpacity").GetString(),CultureInfo.InvariantCulture),NativeSirenSmile.RimStrokeOpacity,"tongue "+i+" rim stroke opacity",1e-6);
                Near(Px(g.GetProperty("rimStrokeWidth").GetString()),NativeSirenSmile.RimStrokeWidth,"tongue "+i+" rim line",1e-6);Check(g.GetProperty("vectorEffect").GetString()=="non-scaling-stroke","tongue "+i+" rim line does not scale");
                var core=g.GetProperty("core");
                if(front){Check(Rgb(core.GetProperty("fill").GetString())==NativeSirenSmile.CoreRgb&&core.GetProperty("transform").GetString()=="scale(.9 .92)"&&NativeSirenSmile.CoreScaleX==.9f&&NativeSirenSmile.CoreScaleY==.92f,"front tongue "+i+" black core at .9 x .92");}
                else Check(core.ValueKind==JsonValueKind.Null,"back tongue "+i+" has no core");
            }
            foreach(var sample in json.GetProperty("licks").EnumerateArray())
            {
                double t=D(sample,"t");float rise=NativeSirenSmile.Rise((float)(t/1.4));var lick=sample.GetProperty("lick");var ctm=sample.GetProperty("ctm");
                for(int i=0;i<NativeSirenSmile.TongueCount;i++)
                {
                    int n=i>=nb?i-nb:i;NativeSirenSmile.Lick((float)t,n,out float a,out float skew);var l=A(lick[i]);
                    Near(a,l[3],"lick scaleY tongue "+i+" at "+t,2e-3);Near(Math.Tan(skew*Math.PI/180),l[2],"lick skewX tongue "+i+" at "+t,2e-3);
                    NativeSirenSmile.Tongue(i,W,H,(float)t,rise,false,out _,out _,out _,out float xx,out float xy,out float yy,out float tx,out float ty,out float clipY);var c=A(ctm[i]);
                    Near(xx,c[0],"tongue "+i+" screen a at "+t,2e-3*Math.Abs(c[0])+1e-4);Near(0,c[1],"tongue "+i+" screen b",1e-4);Near(xy,c[2],"tongue "+i+" screen c at "+t,1e-2);
                    Near(yy,c[3],"tongue "+i+" screen d at "+t,2e-3*Math.Abs(c[3])+1e-3);Near(tx,c[4],"tongue "+i+" screen x at "+t,.05);Near(ty,c[5],"tongue "+i+" screen y at "+t,.1);
                    NativeSirenSmile.FlameBox(W,H,out _,out _,out _,out float fbh,out _,out _);Near(clipY,-fbh/yy,"tongue "+i+" cut at the flame box top",1e-3);
                }
                // still (reduced motion): no lick, the static placement.
                NativeSirenSmile.Tongue(nb+5,W,H,(float)t,0,true,out _,out _,out float sc,out float sxx,out float sxy,out float syy,out _,out _,out float stillClip);Check(stillClip>-60,"the tallest tongue is cut flat by the flame box");NativeSirenSmile.FlameBox(W,H,out _,out _,out _,out _,out float u,out _);
                Check(Math.Abs(sxy)<1e-6&&Math.Abs(sxx-u*sc)<1e-4&&Math.Abs(syy-u*sc)<1e-4,"reduced motion holds the tongues still");
            }
        }

        private static void Raster()
        {
            // The face at 4 px per view unit: rows each part covers and the teeth's share of the grin.
            const float d=4;float m=2/d;
            int Rows(List<float[]> paths,float x0,float y0,float x1,float y1,out double area,int rgb=NativeSirenSmile.FaceRgb,List<float[]> over=null)
            {
                int w=(int)Math.Ceiling((x1-x0+2*m)*d),h=(int)Math.Ceiling((y1-y0+2*m)*d);var rgba=new byte[w*h*4];var layers=new List<NativeSirenSmile.Layer>{NativeSirenSmile.Paint(paths,rgb,1,d,-(x0-m)*d,d,-(y0-m)*d)};
                if(over!=null)layers.Add(NativeSirenSmile.Paint(over,NativeSirenSmile.TeethRgb,1,d,-(x0-m)*d,d,-(y0-m)*d));NativeSirenSmile.Render(rgba,w,h,layers.ToArray(),8,rgb);
                int rows=0;area=0;for(int y=0;y<h;y++){bool lit=false;for(int x=0;x<w;x++){int i=(y*w+x)*4;if(rgba[i+3]>128&&rgba[i]>200){lit=true;area+=1;}}if(lit)rows++;}area/=d*d;return rows;
            }
            int eyeRows=Rows(NativeSirenSmile.Eyes,NativeSirenSmile.EyesX0,NativeSirenSmile.EyesY0,NativeSirenSmile.EyesX1,NativeSirenSmile.EyesY1,out double eyeArea);
            Check(eyeRows>=17*d,"the eyes are almonds 18 units tall, not slits ("+eyeRows/d+")");Near(eyeArea,NativeSirenSmile.Area(NativeSirenSmile.Eyes),"eyes' painted area",NativeSirenSmile.Area(NativeSirenSmile.Eyes)*.03);
            int grinRows=Rows(NativeSirenSmile.Grin,NativeSirenSmile.MouthX0,NativeSirenSmile.MouthY0,NativeSirenSmile.MouthX1,NativeSirenSmile.MouthY1,out double grinArea);
            Check(grinRows>=33*d,"the grin is 34.5 units tall, not a line ("+grinRows/d+")");Check(grinArea>2300&&grinArea<2700,"the grin's area ("+grinArea+")");
            Near(grinArea,NativeSirenSmile.Area(NativeSirenSmile.Grin),"grin's painted area",NativeSirenSmile.Area(NativeSirenSmile.Grin)*.03);
            Rows(NativeSirenSmile.Grin,NativeSirenSmile.MouthX0,NativeSirenSmile.MouthY0,NativeSirenSmile.MouthX1,NativeSirenSmile.MouthY1,out double whiteArea,NativeSirenSmile.FaceRgb,NativeSirenSmile.TeethStroke);
            double teethShare=1-whiteArea/grinArea;Check(teethShare>.3&&teethShare<.48,"the dark teeth cut "+teethShare.ToString("0.00")+" of the grin");
            // Sixteen zigzag strokes joined by fifteen mitred wedges (every join's miter is within the limit of 4).
            Check(NativeSirenSmile.TeethLine.Count==1&&NativeSirenSmile.TeethLine[0].Length==17*2,"seventeen teeth points");
            Check(NativeSirenSmile.TeethStroke.Count==16+15,"sixteen stroke quads and fifteen joins");foreach(var p in NativeSirenSmile.TeethStroke)Check(p.Length==8||p.Length==6,"quad or wedge");
            int mitred=0;foreach(var p in NativeSirenSmile.TeethStroke)if(p.Length==8)mitred++;Check(mitred==16+15,"every join mitred (a quad wedge)");
            NativeSirenSmile.Bounds(NativeSirenSmile.TeethStroke,out _,out _,out _,out float sy1);Check(sy1>103.8f+1.7f,"miter tips reach past the teeth's lowest points");
            // The glow: a soft halo around the face (the full 3-sigma reach), knocked out inside the shapes.
            var glow=new NativeSirenSmile.Glow();Check(glow.Build(6,1,1),"glow builds");Check(!glow.Build(6,1,1),"an unchanged glow is not rebuilt");Check(glow.Build(6,.5f,1),"the opening grin rebuilds the glow");glow.Build(6,1,1);
            float Alpha(float x,float y){int ix=(int)((x-glow.X0)/glow.UnitsPerTexel),iy=(int)((y-glow.Y0)/glow.UnitsPerTexel);return glow.Rgba[((glow.Height-1-iy)*glow.Width+ix)*4+3]/255f;}
            Check(Alpha(59,48)<.05f,"no glow under the eye's middle");Check(Alpha(100,90)<.05f,"no glow under the grin's middle");
            float edge=Alpha(100,104.5f+1),near=Alpha(100,104.5f+28/6f),far=Alpha(100,104.5f+84/6f);
            Check(edge>.25f&&edge<=NativeSirenSmile.GlowAlpha+.01f,"glow strong at the lip ("+edge+")");Check(near<edge&&near>.06f,"glow a sigma out ("+near+")");Check(far<near&&far<.06f,"glow fades by three sigma ("+far+")");
        }

        private static void Runtime(string root)
        {
            string reveal=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSirenReveal.cs"));
            Check(reveal.Contains("NativeSirenSmileArt.Scene(W,H,age,SirenSmile,still)")&&reveal.Contains("NativeSirenSmileArt.Backdrop(W,H,clock,fade,still)"),"the reveal draws the source stage in both windows");
            foreach(var call in new[]{"Smile.StareOpacity(k)","Smile.GrinOpen(k,out mo,out ms)","Smile.Rise(k)","Smile.Tongue(i,W,H,t,rise,still","Smile.Tear(k,out float dx)","glowBake.Build(","Smile.SpotIn(","Smile.SirenBoot(","Smile.LockedPulse(","Smile.Render(","Smile.HalveWidth("})
                Check(reveal.Contains(call),"runtime uses "+call);
            Check(!reveal.Contains("int teeth=17")&&!reveal.Contains("SirenGrin("),"the bar-and-ticks stand-in grin is gone");
            Check(reveal.Contains("prefs.reducedEffects"),"reduced effects respected");
            Check(reveal.Contains("System.Threading.Tasks.Task.Run(()=>BakeAll("),"textures bake off the main thread");
        }
    }
}
#endif
