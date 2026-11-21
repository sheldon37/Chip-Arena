#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Siren's Unity finish beyond the source (Core/NativeSirenDetail.cs, owner request 2026-10-10) against
    // tools/export_siren_detail.mjs's run of tools/siren_design.mjs on a dressed source rig (Tests/siren-detail-parity.json):
    // the deterministic piece list (ids, groups, joints, tones), the tone constants, the stratified flame, every piece's
    // transform for scripted look states (the source's 136 with the twist, then the 258 detail pieces); then its own rules:
    // finite matrices over a sweep, every piece on a drawn joint, bounds, the budget, allocation-free evaluation that never
    // rewrites a static piece, nothing over the eyes or the grin (also through the entrance smile's mouth range), the glow
    // line fade, the bank's couture parts and grin rows, and the dressing of the dev-siren entrance and the Siren's death bakes.
    internal static class SirenDetailChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren detail: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static JsonDocument Bank(string root,string name)
        {
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/"+name+".json")));
            Check(envelope.RootElement.GetProperty("schema").GetInt32()==1&&envelope.RootElement.GetProperty("commit").GetString()=="05950eaef80ff2483a9c44d898cad98be1a76bbc",name+" envelope");
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            return JsonDocument.Parse(reader.ReadToEnd());
        }
        private static SirenLookState State(JsonElement s)=>new SirenLookState{Ranged=D(s,"ranged"),Open=D(s,"open"),Squint=D(s,"squint"),Anger=D(s,"anger"),Glow=D(s,"glow"),Time=D(s,"time"),Lean=D(s,"lean"),Flare=D(s,"flare"),Clock=D(s,"clock")};
        // Point (x, y) through a piece matrix (head-local), and the 8 corners of a unit box.
        internal static void Apply(double[] m,int o,double x,double y,double z,out double px,out double py,out double pz)
        {px=m[o]*x+m[o+1]*y+m[o+2]*z+m[o+3];py=m[o+4]*x+m[o+5]*y+m[o+6]*z+m[o+7];pz=m[o+8]*x+m[o+9]*y+m[o+10]*z+m[o+11];}
        internal static double Cross(double ax,double ay,double bx,double by,double cx,double cy)=>(bx-ax)*(cy-ay)-(by-ay)*(cx-ax);
        internal static bool InTriangle(double x,double y,double ax,double ay,double bx,double by,double cx,double cy)
        {double d1=Cross(ax,ay,bx,by,x,y),d2=Cross(bx,by,cx,cy,x,y),d3=Cross(cx,cy,ax,ay,x,y);bool neg=d1<0||d2<0||d3<0,pos=d1>0||d2>0||d3>0;return !(neg&&pos);}
        // A box's head-local xy footprint (convex hull of its corners) and its front z.
        internal sealed class Footprint {public string Id;public double[] X,Y;public double Front,MinX,MaxX,MinY,MaxY;}
        internal static Footprint Hull(string id,double[] m,int o)
        {
            var pts=new List<(double x,double y)>();double front=double.MinValue;
            for(int c=0;c<8;c++){Apply(m,o,(c&1)!=0?.5:-.5,(c&2)!=0?.5:-.5,(c&4)!=0?.5:-.5,out double x,out double y,out double z);pts.Add((x,y));front=Math.Max(front,z);}
            pts.Sort((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y.CompareTo(b.y));var h=new List<(double x,double y)>();
            for(int pass=0;pass<2;pass++){int start=h.Count;foreach(var p in pass==0?pts:Reverse(pts)){while(h.Count>=start+2&&Cross(h[h.Count-2].x,h[h.Count-2].y,h[h.Count-1].x,h[h.Count-1].y,p.x,p.y)<=0)h.RemoveAt(h.Count-1);h.Add(p);}h.RemoveAt(h.Count-1);}
            var f=new Footprint{Id=id,X=new double[h.Count],Y=new double[h.Count],Front=front,MinX=double.MaxValue,MaxX=double.MinValue,MinY=double.MaxValue,MaxY=double.MinValue};
            for(int i=0;i<h.Count;i++){f.X[i]=h[i].x;f.Y[i]=h[i].y;f.MinX=Math.Min(f.MinX,h[i].x);f.MaxX=Math.Max(f.MaxX,h[i].x);f.MinY=Math.Min(f.MinY,h[i].y);f.MaxY=Math.Max(f.MaxY,h[i].y);}
            return f;
        }
        private static IEnumerable<(double x,double y)> Reverse(List<(double x,double y)> l){for(int i=l.Count-1;i>=0;i--)yield return l[i];}
        internal static bool Inside(Footprint f,double x,double y)
        {if(x<f.MinX||x>f.MaxX||y<f.MinY||y>f.MaxY)return false;for(int i=0,j=f.X.Length-1;i<f.X.Length;j=i++)if(Cross(f.X[j],f.Y[j],f.X[i],f.Y[i],x,y)<0)return false;return true;}
        // mm² of triangle (a, b, c) under any footprint in front of the glow plane, on a .4 mm grid (also Tests/SirenSmileFaceChecks.cs).
        internal static double Covered(double ax,double ay,double bx,double by,double cx,double cy,List<Footprint> boxes,Dictionary<string,double> worst,string tag)
        {
            const double step=.0004;double total=0;double x0=Math.Min(ax,Math.Min(bx,cx)),x1=Math.Max(ax,Math.Max(bx,cx)),y0=Math.Min(ay,Math.Min(by,cy)),y1=Math.Max(ay,Math.Max(by,cy));
            foreach(var f in boxes)
            {
                if(f.Front<=.1638||f.MaxX<x0||f.MinX>x1||f.MaxY<y0||f.MinY>y1)continue;int n=0;
                for(double x=Math.Floor(x0/step)*step;x<=x1;x+=step)for(double y=Math.Floor(y0/step)*step;y<=y1;y+=step)if(InTriangle(x,y,ax,ay,bx,by,cx,cy)&&Inside(f,x,y))n++;
                if(n==0)continue;double a=n*step*step*1e6;total+=a;string key=tag+" x "+f.Id;worst[key]=Math.Max(worst.TryGetValue(key,out var w)?w:0,a);
            }
            return total;
        }

        public static void Run(string root)
        {
            count=0;int first=NativeSirenLook.Detail;
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-detail-parity.json")));var f=doc.RootElement;
            // 1. The piece list: deterministic, the same ids, groups, joints and tones as the design table.
            Check(f.GetProperty("count").GetInt32()==NativeSirenLook.Count&&f.GetProperty("first").GetInt32()==NativeSirenLook.Source&&NativeSirenLook.Source==136,"piece counts");
            Check(f.GetProperty("boxes").GetInt32()==NativeSirenDetail.Boxes&&NativeSirenDetail.Count==NativeSirenDetail.Boxes,"box count: every piece a box");
            var drawn=new HashSet<int>{1,2,3,5,6,7,8,9,10,11,12,13,14,18};int index=0,boxes=0,moving=0;var groups=new Dictionary<string,int>();
            foreach(var p in f.GetProperty("pieces").EnumerateArray())
            {
                string what="piece "+index;Check(NativeSirenDetail.Id(index)==p.GetProperty("id").GetString(),what+" id "+NativeSirenDetail.Id(index));Check(NativeSirenDetail.Group(index)==p.GetProperty("group").GetString(),what+" group");
                Check(NativeSirenLook.Joint(first+index)==p.GetProperty("joint").GetInt32()&&NativeSirenDetail.Bone(index)==p.GetProperty("joint").GetInt32(),what+" joint");Check(NativeSirenDetail.Tone(index)==p.GetProperty("tone").GetInt32(),what+" tone");
                Check(drawn.Contains(NativeSirenDetail.Bone(index)),what+" rides a joint the Siren draws ("+NativeSirenDetail.Bone(index)+")");
                Check(NativeSirenDetail.Moves(index)==p.GetProperty("moves").GetBoolean(),what+" moves");if(NativeSirenDetail.Moves(index))moving++;
                Check((NativeSirenDetail.ClawGroup(index)>=0)==(NativeSirenDetail.Group(index)=="rings"),what+" claw group");
                Check(NativeSirenDetail.Tone(index)>=0&&NativeSirenDetail.Tone(index)<NativeSirenDetail.Tones,what+" tone range");boxes++;groups[NativeSirenDetail.Group(index)]=(groups.TryGetValue(NativeSirenDetail.Group(index),out int g)?g:0)+1;index++;
            }
            Check(index==NativeSirenDetail.Count&&boxes==NativeSirenDetail.Boxes&&moving==NativeSirenDetail.MovingCount,"pieces "+index+", boxes "+boxes+", moving "+moving);
            // The design's own groups and counts (collar trimmed, front panels, 27 hem ash steps, the nimbus all boxes: no soft glow
            // competes with the eyes on the head).
            foreach(var (name,n) in new[]{("collar",53),("pauldron",8),("bodice",9),("girdle",8),("gown",34),("front",8),("hem",27),("train",10),("gauntlet",16),("rings",6),("nimbus",51),("face",24),("cheek",4)})
                Check(groups.TryGetValue(name,out int got)&&got==n,"group "+name+": "+got);
            // 2. Constants: tones, glow colours (three's sRGB decode x intensity), seams, talons, nimbus, cheeks, twist.
            var tones=f.GetProperty("tones");int t=0;
            foreach(var ink in tones.GetProperty("ink").EnumerateArray())
            {
                Check(ink.GetProperty("hex").GetInt32()==NativeSirenDetail.InkHex[t]&&D(ink,"rim")==NativeSirenDetail.InkRim[t]&&D(ink,"flow")==NativeSirenDetail.InkFlow[t],"ink tone "+t);
                var e=ink.GetProperty("emissive");Check(t==NativeSirenDetail.Ash?e[0].GetDouble()==NativeSirenDetail.AshEmitR&&e[1].GetDouble()==NativeSirenDetail.AshEmitG&&e[2].GetDouble()==NativeSirenDetail.AshEmitB:e[0].GetDouble()==0&&e[2].GetDouble()==0,"ink emission "+t);t++;
            }
            Check(t==4,"ink tones");
            foreach(var glow in tones.GetProperty("glow").EnumerateArray())
            {
                int tone=glow.GetProperty("tone").GetString()=="seam"?NativeSirenDetail.Seam:NativeSirenDetail.Filament;NativeSirenDetail.GlowColor(tone,out double r,out double gg,out double b);var l=glow.GetProperty("linear");
                Near(r,l[0].GetDouble(),2e-6,"glow "+tone+" r");Near(gg,l[1].GetDouble(),2e-6,"glow "+tone+" g");Near(b,l[2].GetDouble(),2e-6,"glow "+tone+" b");
                Check(glow.GetProperty("hex").GetInt32()==(tone==NativeSirenDetail.Seam?NativeSirenDetail.SeamHex:NativeSirenDetail.FilamentHex),"glow hex "+tone);
            }
            Check(!tones.TryGetProperty("sprite",out _),"no nimbus glow sprites");
            Check(D(f,"seamWidth")==NativeSirenDetail.SeamWidth&&D(f.GetProperty("talons"),"claw")==NativeSirenDetail.Talon&&D(f.GetProperty("talons"),"thumb")==NativeSirenDetail.ThumbTalon,"seam width and talons");
            var nim=f.GetProperty("nimbus");Check(D(nim,"y")==NativeSirenDetail.HubY&&D(nim,"z")==NativeSirenDetail.HubZ&&D(nim,"tilt")==NativeSirenDetail.HubTilt&&D(nim,"r")==NativeSirenDetail.Radius&&D(nim,"spin")==NativeSirenDetail.Spin,"nimbus constants");
            var ch=f.GetProperty("cheek");Check(D(ch,"dx")==NativeSirenDetail.CheekX&&D(ch,"dy")==NativeSirenDetail.CheekY&&D(ch,"dz")==NativeSirenDetail.CheekZ&&D(ch,"turn")==NativeSirenDetail.CheekTurn,"cheek constants");
            int tw=0;foreach(var w in f.GetProperty("twist").EnumerateArray()){NativeSirenLook.Twist(tw/NativeSirenLook.TongueSteps,tw%NativeSirenLook.TongueSteps,out double yaw,out double k);Near(yaw,w[0].GetDouble(),1e-8,"twist yaw "+tw);Near(k,w[1].GetDouble(),1e-8,"twist girth "+tw);tw++;}
            Check(tw==NativeSirenLook.TongueCount*NativeSirenLook.TongueSteps,"twist steps");
            // 3. Every piece of every scripted state, from the dressed source rig's own scene graph.
            var m=new double[NativeSirenLook.Count*16];var shown=new bool[NativeSirenLook.Count];double worst=0;int states=0;
            foreach(var s in f.GetProperty("states").EnumerateArray())
            {
                Array.Clear(m,0,m.Length);NativeSirenLook.Rest(m,shown);NativeSirenLook.Evaluate(State(s.GetProperty("state")),m,shown);int p=0;
                foreach(var rows in s.GetProperty("pieces").EnumerateArray()){int i=0;foreach(var v in rows.EnumerateArray()){double d=Math.Abs(m[p*16+i/4*4+i%4]-v.GetDouble());worst=Math.Max(worst,d);Check(d<=1e-6,"state "+states+" piece "+p+" ["+i+"]: off by "+d);i++;}Check(i==12,"rows");p++;}
                Check(p==NativeSirenLook.Count,"state pieces");p=0;foreach(var v in s.GetProperty("shown").EnumerateArray()){Check(shown[p]==(v.GetInt32()==1),"state "+states+" shown "+p);p++;}states++;
            }
            Check(states>=10,"scripted states");
            // 4. A sweep of look states: finite, non-degenerate matrices, bounded around their joints; static pieces untouched.
            NativeSirenLook.Rest(m,shown);var still=(double[])m.Clone();int frames=0;double reach=0;string far="";
            for(int i=0;i<240;i++)
            {
                double time=i*.37,u=i/239.0;var st=new SirenLookState{Ranged=u,Open=(i%5)/4.0,Squint=(i%7)/5.0,Anger=(i%9)/8.0,Glow=1,Time=time,Lean=Math.Sin(i*.7),Flare=(i%3)/2.0,Clock=time*1.3};
                NativeSirenLook.Evaluate(st,m,shown);frames++;
                for(int k=0;k<NativeSirenDetail.Count;k++)
                {
                    int o=(first+k)*16;for(int q=0;q<16;q++)Check(!double.IsNaN(m[o+q])&&!double.IsInfinity(m[o+q]),"finite piece "+k);
                    if(!NativeSirenDetail.Moves(k))for(int q=0;q<16;q++)Check(m[o+q]==still[o+q],"static piece "+NativeSirenDetail.Id(k)+" rewritten");
                    double det=m[o]*(m[o+5]*m[o+10]-m[o+6]*m[o+9])-m[o+1]*(m[o+4]*m[o+10]-m[o+6]*m[o+8])+m[o+2]*(m[o+4]*m[o+9]-m[o+5]*m[o+8]);Check(det>1e-12,"piece "+NativeSirenDetail.Id(k)+" degenerate or mirrored ("+det+")");
                    for(int c=0;c<8;c++){Apply(m,o,(c&1)!=0?.5:-.5,(c&2)!=0?.5:-.5,(c&4)!=0?.5:-.5,out double x,out double y,out double z);double r=Math.Sqrt(x*x+y*y+z*z);if(r>reach){reach=r;far=NativeSirenDetail.Id(k);}}
                }
            }
            // The farthest corner from its joint: the train's last ash step, swung back by the lean (about 1.17 m from the pelvis).
            Check(reach<1.25,"a piece reaches "+reach+" m from its joint ("+far+")");
            // 5. Budget: boxes, triangles, shared parts, and no allocation per frame.
            Check(NativeSirenDetail.Boxes<=350&&NativeSirenDetail.Boxes*12<=4200,"budget: "+NativeSirenDetail.Boxes+" boxes");
            var warm=new SirenLookState{Glow=1,Time=3,Clock=3,Squint=.5,Lean=.4,Flare=.2,Ranged=.3};NativeSirenLook.Evaluate(warm,m,shown);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<500;i++){warm.Time+=.016;warm.Clock+=.016;NativeSirenLook.Evaluate(warm,m,shown);}long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated==0,"Evaluate allocates "+allocated+" bytes over 500 frames");
            // 6. Nothing over the eyes or the grin: head pieces in front of the glow plane against every eye outline (41 cuts x
            // squint x mouth), the grin rows (open 0..1) and the entrance smile's mouth (open up to .26, the hold width .153 of
            // the source's .146); the cheeks at every lift. The accepted graze: the lower-row corner tooth under a cheek at a
            // fully open jaw (slam and rend peaks only), as faithful-plus measured it (36 mm²).
            var worstCover=new Dictionary<string,double>();var xy=new double[NativeSirenLook.EyeSegments*6];var gm=new double[NativeSirenLook.Count*16];var gs=new bool[NativeSirenLook.Count];
            var upper=new List<double>();foreach(var v in f.GetProperty("grin").GetProperty("upper").EnumerateArray())upper.Add(v.GetDouble());var lower=new List<double>();foreach(var v in f.GetProperty("grin").GetProperty("lower").EnumerateArray())lower.Add(v.GetDouble());
            Check(upper.Count==13*15*3&&lower.Count==12*15*3,"grin rows: 13 upper and 12 lower fangs of 5 triangles");
            double eyesCovered=0,teethStatic=0,teethSmile=0,teethOpen=0;
            foreach(double c in new[]{0,.3,.6,1.0})foreach(double open in new[]{0,.06,.26,.5,1.0})foreach(double squint in new[]{0,.4,.6,1.0})
            {
                var st=new SirenLookState{Open=open,Squint=squint,Glow=1,Time=1,Clock=1};NativeSirenLook.Rest(gm,gs);NativeSirenLook.Evaluate(st,gm,gs);
                // The cheeks at lift c (Evaluate lifts them by the squint; here every lift the arena and the entrance can show).
                NativeSirenDetail.Evaluate(st,gm,gs,first,NativeSirenLook.Claw,c);
                var stat=new List<Footprint>();var cheeks=new List<Footprint>();
                for(int k=0;k<NativeSirenDetail.Count;k++){if(NativeSirenDetail.Bone(k)!=5||NativeSirenDetail.Group(k)=="nimbus")continue;var fp=Hull(NativeSirenDetail.Id(k),gm,(first+k)*16);(NativeSirenDetail.Group(k)=="cheek"?cheeks:stat).Add(fp);}
                var all=new List<Footprint>(stat);all.AddRange(cheeks);
                if(open==0||open==.26||open==1.0)for(int side=0;side<2;side++)for(int a=0;a<=40;a+=4)
                {
                    NativeSirenLook.EyeOutline(side==0?1:-1,a/40.0,xy);int o=(NativeSirenLook.Eye+side)*16;
                    for(int q=0;q<NativeSirenLook.EyeSegments;q++){Apply(gm,o,xy[q*6],xy[q*6+1],0,out double ax,out double ay,out _);Apply(gm,o,xy[q*6+2],xy[q*6+3],0,out double bx,out double by,out _);Apply(gm,o,xy[q*6+4],xy[q*6+5],0,out double cx,out double cy,out _);eyesCovered+=Covered(ax,ay,bx,by,cx,cy,all,worstCover,"eye");}
                }
                foreach(double width in new[]{1,.153/.146})for(int row=0;row<2;row++)
                {
                    var tri=row==0?upper:lower;int o=(row==0?NativeSirenLook.GrinUpper:NativeSirenLook.GrinLower)*16;
                    for(int q=0;q<tri.Count;q+=9)
                    {
                        Apply(gm,o,tri[q]*width,tri[q+1],0,out double ax,out double ay,out _);Apply(gm,o,tri[q+3]*width,tri[q+4],0,out double bx,out double by,out _);Apply(gm,o,tri[q+6]*width,tri[q+7],0,out double cx,out double cy,out _);
                        teethStatic+=Covered(ax,ay,bx,by,cx,cy,stat,worstCover,"teeth");double cover=Covered(ax,ay,bx,by,cx,cy,cheeks,worstCover,"teeth open "+open);
                        if(open<=.26)teethSmile+=cover;else teethOpen=Math.Max(teethOpen,cover);
                    }
                }
            }
            double graze=0;foreach(var kv in worstCover)if(kv.Key.StartsWith("teeth open"))graze=Math.Max(graze,kv.Value);
            Check(eyesCovered==0,"a face piece covers an eye: "+string.Join(", ",worstCover.Keys));
            Check(teethStatic==0&&teethSmile==0,"a face piece covers the grin (static "+teethStatic+" mm², through the smile's mouth "+teethSmile+" mm²): "+string.Join(", ",worstCover.Keys));
            Check(graze<=40,"cheek over the lower row at a fully open jaw: "+graze+" mm²");
            // 7. The glow lines: the breath and the distance fade; the cheek lift.
            Near(NativeSirenDetail.LineGlow(1,0),1,1e-12,"line glow at rest");Near(NativeSirenDetail.LineGlow(0,0),.9,1e-12,"line glow trough");Near(NativeSirenDetail.LineGlow(1,12),1,1e-12,"line glow at 12 m");
            Near(NativeSirenDetail.LineGlow(1,17),.775,1e-12,"line glow at 17 m");Near(NativeSirenDetail.LineGlow(1,22),.55,1e-12,"line glow at 22 m");Near(NativeSirenDetail.LineGlow(1,60),.55,1e-12,"line glow far");
            Near(NativeSirenDetail.CheekLift(.5),.3,1e-12,"cheek lift");Near(NativeSirenDetail.CheekLift(1.4),.6,1e-12,"cheek lift clamps");Near(NativeSirenDetail.CheekLift(-1),0,1e-12,"cheek lift floor");
            Check(NativeSirenDetail.Nimbus&&NativeSirenDetail.Gown,"kill switches on");
            // 8. The bank: one unit box per couture part, black-and-white glow box, the finer grin rows in the teeth parts, the
            // longer talons; the entrance bake's named couture batches.
            using(var bank=Bank(root,"SourceModels"))
            {
                JsonElement siren=default,stage=default,dissipate=default;foreach(var mm in bank.RootElement.GetProperty("models").EnumerateArray()){string id=mm.GetProperty("id").GetString();if(id=="siren")siren=mm;else if(id=="stage-dev-siren")stage=mm;else if(id=="death-siren-dissipate")dissipate=mm;}
                var named=new Dictionary<string,JsonElement>();foreach(var sec in siren.GetProperty("sections").EnumerateArray()){string part=sec.GetProperty("part").GetString();if(!string.IsNullOrEmpty(part))named[part]=sec;}
                foreach(var part in new[]{"siren-couture-plate","siren-couture-deep","siren-couture-sheen","siren-couture-ash","siren-couture-seam"})
                {
                    Check(named.TryGetValue(part,out var sec),"bank part "+part);var pos=sec.GetProperty("positions");Check(pos.GetArrayLength()==24*3&&sec.GetProperty("bone").GetInt32()==-1,part+" is a unit box in its own frame");
                    double big=0;foreach(var v in pos.EnumerateArray())big=Math.Max(big,Math.Abs(v.GetDouble()));Near(big,.5,1e-6,part+" extent");
                    var col=sec.GetProperty("colors");var em=sec.GetProperty("emission");bool ink=sec.GetProperty("ink").GetBoolean();
                    if(part=="siren-couture-seam")Check(!ink&&col[0].GetDouble()==0&&col[2].GetDouble()==0&&em[0].GetDouble()==1&&em[2].GetDouble()==1&&em[3].GetDouble()==0,"glow box black, emitting white, untinted");
                    else
                    {
                        int tone=Array.IndexOf(new[]{"siren-couture-plate","siren-couture-deep","siren-couture-sheen","siren-couture-ash"},part);NativeBlastSim.Linear(NativeSirenDetail.InkHex[tone],out double r,out _,out double b);
                        Check(ink&&Math.Abs(col[0].GetDouble()-r)<2e-6&&Math.Abs(col[2].GetDouble()-b)<2e-6&&em[3].GetDouble()==0,part+" ink colour, no body or accent tint");
                        Check(tone==NativeSirenDetail.Ash?Math.Abs(em[0].GetDouble()-NativeSirenDetail.AshEmitR)<1e-6:em[0].GetDouble()==0,part+" emission");
                    }
                }
                // The teeth parts hold the design's rows (the bank mirrors x and turns each triangle over).
                foreach(var (part,row) in new[]{("siren-teeth-upper",upper),("siren-teeth-lower",lower)})
                {
                    var sec=named[part];var pos=sec.GetProperty("positions");var idx=sec.GetProperty("indices");Check(idx.GetArrayLength()==row.Count/3,part+" triangles");
                    for(int q=0;q<idx.GetArrayLength();q+=3)for(int k=0;k<3;k++){int v=idx[q+(k==0?0:k==1?2:1)].GetInt32(),src=(q+k)*3;Near(-pos[v*3].GetDouble(),row[src],2e-6,part+" x");Near(pos[v*3+1].GetDouble(),row[src+1],2e-6,part+" y");}
                }
                // Talons: the claw parts reach 1.15 times as far below the claw group (the long claws' tips).
                foreach(var part in new[]{"siren-claws-l","siren-claws-r"}){double low=0;var pos=named[part].GetProperty("positions");for(int i=1;i<pos.GetArrayLength();i+=3)low=Math.Min(low,pos[i].GetDouble());Check(low<-.3*1.08*.95&&low>-.3*1.08*1.16,part+" long claws x1.15 ("+low+")");}
                var stageParts=new Dictionary<string,int>();var glowTracks=new HashSet<int>();
                foreach(var sec in stage.GetProperty("sections").EnumerateArray()){string part=sec.GetProperty("part").GetString();if(string.IsNullOrEmpty(part)||!part.StartsWith("siren-couture-"))continue;stageParts[part]=(stageParts.TryGetValue(part,out int n)?n:0)+1;if(part=="siren-couture-seam")glowTracks.Add(sec.GetProperty("bone").GetInt32());}
                foreach(var part in new[]{"siren-couture-plate","siren-couture-deep","siren-couture-sheen","siren-couture-ash","siren-couture-seam"})Check(stageParts.ContainsKey(part),"entrance bake dresses the Siren with "+part);
                // The entrance's glow lines keep their own tracks: full while the bulb shows her, dimmed through the blackout so
                // the eyes lead (seams to .08, the nimbus filament to .14), the filament kindling again at the smile.
                using var scenes=Bank(root,"SceneMotion");JsonElement clip=default;foreach(var c in scenes.RootElement.GetProperty("clips").EnumerateArray())if(c.GetProperty("key").GetString()=="stage-dev-siren")clip=c;
                int frames2=clip.GetProperty("frames").GetInt32();double duration=clip.GetProperty("duration").GetDouble();var vals=clip.GetProperty("values");
                double Bright(int track,double age){int fr=(int)Math.Round(age/duration*(frames2-1)),o=(track*frames2+fr)*20;return Math.Max(vals[o+17].GetDouble(),Math.Max(vals[o+18].GetDouble(),vals[o+19].GetDouble()));}
                Check(glowTracks.Count>=25,"entrance glow tracks: "+glowTracks.Count);
                foreach(int track in glowTracks){double lit=Bright(track,4.4),dark=Bright(track,5.8);Check(lit>.4&&dark<=lit*.15+1e-6,"entrance glow track "+track+": "+lit+" lit, "+dark+" in the blackout");}
                // The Siren's own death (death-siren-dissipate) wears the whole finish too: every piece a named section on its own
                // track, shown when she dies and burnt away with the body by the end; its grin rows are the bank's finer grin.
                JsonElement death=default;foreach(var c in scenes.RootElement.GetProperty("clips").EnumerateArray())if(c.GetProperty("key").GetString()=="death-siren-dissipate")death=c;
                int deathFrames=death.GetProperty("frames").GetInt32();var deathValues=death.GetProperty("values");int dressed=0,burnt=0,fangRows=0;var deathParts=new HashSet<string>();var deathTracks=new HashSet<int>();
                foreach(var sec in dissipate.GetProperty("sections").EnumerateArray())
                {
                    string part=sec.GetProperty("part").GetString();int verts=sec.GetProperty("positions").GetArrayLength()/3;if(verts==13*15||verts==12*15)fangRows++;
                    if(string.IsNullOrEmpty(part)||!(part.StartsWith("siren-couture-")||part=="siren-flame-box"))continue;dressed++;deathParts.Add(part);int bone=sec.GetProperty("bone").GetInt32();deathTracks.Add(bone);
                    double shownAt=deathValues[(bone*deathFrames)*20+16].GetDouble(),goneAt=deathValues[(bone*deathFrames+deathFrames-1)*20+16].GetDouble();Check(shownAt>0,"dissipate couture section on track "+bone+" shown when she dies");if(goneAt==0)burnt++;
                }
                Check(dressed==NativeSirenDetail.Boxes&&deathTracks.Count==dressed,"dissipate dressing: "+dressed+" sections on "+deathTracks.Count+" tracks");
                foreach(var part in new[]{"siren-couture-plate","siren-couture-deep","siren-couture-sheen","siren-couture-ash","siren-couture-seam","siren-flame-box"})Check(deathParts.Contains(part),"dissipate wears "+part);
                Check(burnt==dressed,"the dissipate burns every couture piece away ("+burnt+" of "+dressed+")");Check(fangRows==2,"the dissipate grin is the finer grin ("+fangRows+" fang rows)");
            }
            // 9. The draw paths take every piece (QueuePieces and the portraits), with tints, glow and frames as described.
            string look=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSirenLookPresentation.cs")),portraits=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativePortraits.cs")),cine=File.ReadAllText(Path.Combine(root,"tools/export_cinematics.mjs")),models=File.ReadAllText(Path.Combine(root,"tools/export_models.mjs"));
            Check(look.Contains("Named(\"siren-couture-plate\")")&&look.Contains("CoutureInk();")&&look.Contains("NativeSirenDetail.LineGlow(pose.Breath,pose.GlowDistance)")&&look.Contains("NativeSirenLook.Rest(m,Shown)"),"runtime parts, ink strengths, glow fade and static pieces");
            Check(portraits.Contains("out var glow")&&portraits.Contains("NativePortraitRules.SirenTop(s.Pose)"),"portraits draw the glow lines and frame the nimbus");
            Check(cine.Contains("SirenDesign.dress(T,stage.rig")&&cine.Contains("SirenDesign.dress(T,rig,parts,sirenMaterials)")&&cine.Contains("dressedDissipate();")&&cine.Contains("bakeStatic")&&cine.Contains("SirenDesign.entranceGlow(t,sirenCues)")&&models.Contains("SirenDesign.finish(T,parts,{grin:true,talons:true})"),"exporters dress the entrance and finish the bank");
            Console.WriteLine($"PASS Siren detail: {NativeSirenDetail.Count} pieces ({NativeSirenDetail.Boxes} boxes, {NativeSirenDetail.MovingCount} moving) match the design table and {states} dressed-rig states (largest gap {worst:0.0e+0}); {frames} sweep frames finite, within {reach:0.00} m of their joints, static pieces untouched, no allocation; eyes and grin clear (cheek graze at a fully open jaw {graze:0.0} mm²); bank parts, grin rows, talons, and the entrance and death dressing present; {count} checks");
        }
    }
}
#endif
