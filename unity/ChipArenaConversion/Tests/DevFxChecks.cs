#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // C12 part 2: the Emperor bolts, the Leet glyph pool, nuke blast, nuke field and tree, the hadouken dome and the Demon
    // Step afterimage pool, replayed through Core/NativeDevFx.cs on the scripted snapshots tools/export_dev_fx_fixtures.mjs
    // fed the pinned source (Tests/dev-fx-parity.json).
    internal static class DevFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Dev fx parity: "+what);count++;}
        private static void Near(double a,double b,string what){Check(Math.Abs(a-b)<=2e-5+2e-6*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static double[] V(JsonElement e,string name)=>A(e.GetProperty(name));
        /// <summary>three Matrix4.compose, column-major.</summary>
        private static void Compose(double[] m,int o,double px,double py,double pz,double x,double y,double z,double w,double sx,double sy,double sz)
        {
            double x2=x+x,y2=y+y,z2=z+z,xx=x*x2,xy=x*y2,xz=x*z2,yy=y*y2,yz=y*z2,zz=z*z2,wx=w*x2,wy=w*y2,wz=w*z2;
            m[o]=(1-(yy+zz))*sx;m[o+1]=(xy+wz)*sx;m[o+2]=(xz-wy)*sx;m[o+3]=0;m[o+4]=(xy-wz)*sy;m[o+5]=(1-(xx+zz))*sy;m[o+6]=(yz+wx)*sy;m[o+7]=0;
            m[o+8]=(xz+wy)*sz;m[o+9]=(yz-wx)*sz;m[o+10]=(1-(xx+yy))*sz;m[o+11]=0;m[o+12]=px;m[o+13]=py;m[o+14]=pz;m[o+15]=1;
        }
        private static void Same(double[] actual,double[] expected,string what){Check(actual.Length==expected.Length,what+" length "+actual.Length+" != "+expected.Length);for(int i=0;i<expected.Length;i++)Near(actual[i],expected[i],what+"["+i+"]");}
        public static void Run(string root)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/dev-fx-parity.json")));var runs=doc.RootElement.GetProperty("runs");
            Emperor(runs.GetProperty("emperor"));Leet(runs.GetProperty("leet"));Hadouken(runs.GetProperty("hadouken"));Demon(runs.GetProperty("demon"));
            Console.WriteLine("PASS C12 dev fighter fx parity ("+count+" checks: Emperor bolts and budget, Leet glyph trails/columns/cap, nuke blast/field, tree, hadouken dome and tongues, Demon Step ghosts)");
        }
        private static void Emperor(JsonElement run)
        {
            var bolts=new NativeEmperorBolts();var hand=V(run,"hand");var trails=run.GetProperty("trails");var events=run.GetProperty("events");int k=0;
            foreach(var f in run.GetProperty("frames").EnumerateArray())
            {
                double t=10+k/30.0,q=D(f,"q");Near(t,D(f,"t"),"emperor time");
                foreach(var e in events.EnumerateArray())
                {
                    if(e.GetProperty("at").GetInt32()!=k)continue;string type=e.GetProperty("type").GetString();if(type!="emperor-hit"&&type!="emperor-bound")continue;var p=e.GetProperty("pos");
                    bolts.Impact(D(p,"x"),D(p,"y"),D(p,"z"),t,e.GetProperty("id").GetInt32(),e.TryGetProperty("radius",out var r)?r.GetDouble():NativeEmperorBolts.DefaultRadius);
                }
                bolts.Begin();
                if(t-10.05>=0&&t-10.05<NativeEmperorBolts.FistSeconds)bolts.Bolt(hand[0],hand[1],hand[2],t,"emp-1".Length,NativeEmperorBolts.FistSize);
                if(t-10.2>=0&&t-10.2<NativeEmperorBolts.FistSeconds)bolts.Bolt(-3,.9+.4,2,t,"emp-22".Length,NativeEmperorBolts.FistSize);
                if(t-10.1<NativeEmperorBolts.JuggleSeconds)bolts.Bolt(2,1.5,0,t,3,NativeEmperorBolts.JuggleSize);
                bolts.DrawImpacts(t);
                if(q>=.5&&k<16){int n=k<8?3:24;int i=0;foreach(var tr in trails.EnumerateArray()){if(i++>=n)break;var p=tr.GetProperty("pos");bolts.Bolt(D(p,"x"),NativeEmperorBolts.TrailHeight,D(p,"z"),t,Math.Floor(D(tr,"at")*10),NativeEmperorBolts.TrailSize);}}
                Check(bolts.Count==f.GetProperty("count").GetInt32(),"emperor frame "+k+" count "+bolts.Count+" != "+f.GetProperty("count").GetInt32());
                string[] layers={"glow","dark","core"};
                for(int layer=0;layer<3;layer++)
                {
                    var expected=f.GetProperty(layers[layer]);if(expected.ValueKind==JsonValueKind.Null)continue;var m=new double[bolts.Count*16];
                    for(int i=0;i<bolts.Count;i++){bolts.Box(i,layer,out double cx,out double cy,out double cz,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz);Compose(m,i*16,cx,cy,cz,qx,qy,qz,qw,sx,sy,sz);}
                    Same(m,A(expected),"emperor frame "+k+" "+layers[layer]);
                }
                k++;
            }
            bolts.Clear();Check(bolts.Count==0&&bolts.Impacts==0,"emperor clear");
        }
        private static void Leet(JsonElement run)
        {
            var pool=new NativeGlyphPool();var cam=V(run,"camera");int lastEvent=0;double blastAt=double.NaN;double bx=0,by=0,bz=0;int k=0;var events=run.GetProperty("events");
            foreach(var f in run.GetProperty("frames").EnumerateArray())
            {
                double t=20+k/60.0,q=D(f,"q");
                foreach(var e in events.EnumerateArray())
                {
                    int id=e.GetProperty("id").GetInt32();if(id<=lastEvent||e.GetProperty("frame").GetInt32()>k)continue;lastEvent=id;string type=e.GetProperty("type").GetString();var p=e.GetProperty("pos");
                    bool column=type=="leet-slender-hop"||type=="minionDeath"&&e.GetProperty("message").GetString()=="slender";
                    if(column&&t-D(e,"time")<.5){if(type=="leet-slender-hop"&&e.TryGetProperty("from",out var from))pool.StaticColumn(D(from,"x"),D(from,"y"),D(from,"z"),t,q);pool.StaticColumn(D(p,"x"),D(p,"y"),D(p,"z"),t,q);}
                    else if(type=="leet-nuke"&&t-D(e,"time")<NativeLeetFx.BlastLife){blastAt=D(e,"time");bx=D(p,"x");by=D(p,"y");bz=D(p,"z");}
                }
                var blasts=f.GetProperty("blasts");
                if(!double.IsNaN(blastAt)&&t-blastAt>NativeLeetFx.BlastLife)blastAt=double.NaN;
                Check(blasts.GetArrayLength()==(double.IsNaN(blastAt)?0:1),"leet frame "+k+" blasts");
                if(!double.IsNaN(blastAt)){var b=blasts[0];NativeLeetFx.Blast(t-blastAt,out double s,out double ring,out double opacity);Same(new[]{bx,by,bz},V(b,"p"),"blast position");Near(s,D(b,"s"),"blast scale");Near(ring,D(b,"ring"),"blast ring");Near(opacity,D(b,"opacity"),"blast opacity");}
                pool.BeginTrails();
                pool.Trail(1,-2+k*.2,1.3,1-k*.05,t,q);if(k<30)pool.Trail(3,4,2-k*.02,-1,t,q);if(k>=40)pool.Trail(3,5,1,5,t,q);
                pool.EndTrails();pool.Expire(t);
                var n=f.GetProperty("n");int n0=0,n1=0;for(int i=0;i<pool.Count;i++)if(pool.Bit[i]==0)n0++;else n1++;
                Check(n0==n[0].GetInt32()&&n1==n[1].GetInt32(),"leet frame "+k+" counts "+n0+"/"+n1+" != "+n[0]+"/"+n[1]);
                if(f.GetProperty("m0").ValueKind!=JsonValueKind.Null)
                {
                    var m=new[]{new double[n0*16],new double[n1*16]};var c=new[]{new double[n0*3],new double[n1*3]};int[] at={0,0};
                    for(int i=0;i<pool.Count;i++)
                    {
                        pool.Pose(i,t,out double x,out double y,out double z,out double s,out double r,out double g,out double b2);int bit=pool.Bit[i],j=at[bit]++;
                        Compose(m[bit],j*16,x,y,z,cam[0],cam[1],cam[2],cam[3],s,s,s);c[bit][j*3]=r;c[bit][j*3+1]=g;c[bit][j*3+2]=b2;
                    }
                    Same(m[0],V(f,"m0"),"leet frame "+k+" zeros");Same(m[1],V(f,"m1"),"leet frame "+k+" ones");Same(c[0],V(f,"c0"),"leet frame "+k+" zero colours");Same(c[1],V(f,"c1"),"leet frame "+k+" one colours");
                }
                var nuke=f.GetProperty("nuke");
                if(nuke.ValueKind!=JsonValueKind.Null)
                {
                    Same(new[]{3,.03,-2},V(nuke,"p"),"nuke field position");int i=0;
                    foreach(var g in nuke.GetProperty("glyphs").EnumerateArray()){NativeLeetFx.FieldGlyph(i,t-19.5,t,out double x,out double y,out double z,out double turn);Same(new[]{x,y,z,turn},A(g),"nuke glyph "+i);i++;}
                    Check(i==NativeLeetFx.FieldGlyphs,"nuke glyph count");
                }
                Check(k<70==(nuke.ValueKind!=JsonValueKind.Null),"nuke field lifetime");
                Near(NativeLeetFx.TreeHeight(t-19.9),D(f,"tree"),"tree growth");
                k++;
            }
            int part=0;
            foreach(var p in run.GetProperty("tree").EnumerateArray())
            {
                NativeLeetFx.TreePart(part,out double px,out double py,out double pz,out double rx,out double ry,out double rz,out double sx,out double sy,out double sz);
                Same(new[]{px,py,pz},V(p,"p"),"tree part "+part+" position");Same(new[]{rx,ry,rz},V(p,"r"),"tree part "+part+" rotation");Same(new[]{sx,sy,sz},V(p,"s"),"tree part "+part+" scale");Same(new double[]{1,1,1},V(p,"size"),"tree part box");part++;
            }
            Check(part==1+NativeLeetFx.Branches,"tree parts");
            pool.Clear();Check(pool.Count==0,"glyph clear");
            // Geometry: the '0' is four merged 24-vertex boxes, the '1' one.
            Check(NativeLeetFx.Glyphs[0].VertexCount==96&&NativeLeetFx.Glyphs[1].VertexCount==24,"glyph meshes");
        }
        private static void Hadouken(JsonElement runs)
        {
            var tongue=new double[NativeHadoukenFx.Points*2*3];
            foreach(var run in runs.EnumerateArray())
            {
                bool red=run.GetProperty("red").GetBoolean();var mats=run.GetProperty("materials");
                int[] hex={NativeHadoukenFx.Hot(red),NativeHadoukenFx.Energy(red),NativeHadoukenFx.Haze(red)};double[] opacity={NativeHadoukenFx.HotOpacity,NativeHadoukenFx.EnergyOpacity,NativeHadoukenFx.HazeOpacity};
                for(int i=0;i<3;i++){Check(mats[i].GetProperty("color").GetInt32()==hex[i],"hadouken colour "+i);Near(opacity[i],D(mats[i],"opacity"),"hadouken opacity "+i);Check(mats[i].GetProperty("additive").GetBoolean(),"hadouken additive");}
                double qx=0,qy=0,qz=0,qw=1;
                foreach(var f in run.GetProperty("frames").EnumerateArray())
                {
                    double t=D(f,"t"),q=D(f,"q");var vel=V(f,"vel");NativeHadoukenFx.Frame(D(f,"radius"),t,out double r,out double pulse);
                    if(vel[0]*vel[0]+vel[1]*vel[1]+vel[2]*vel[2]>.0001){double x=vel[0],y=vel[1],z=vel[2];NativeDevFxMath.Normalize(ref x,ref y,ref z);NativeDevFxMath.UnitRotation(0,0,1,x,y,z,out qx,out qy,out qz,out qw);}
                    Same(new[]{qx,qy,qz,qw},V(f,"quat"),"hadouken orientation");
                    NativeHadoukenFx.Core(r,pulse,out double cz,out double csx,out double csy,out double csz);Same(new[]{cz,csx,csy,csz},V(f,"core"),"hadouken core");
                    NativeHadoukenFx.Shell(r,out double sz0,out double ssx,out double ssy,out double ssz);Same(new[]{sz0,ssx,ssy,ssz},V(f,"shell"),"hadouken shell");
                    NativeHadoukenFx.RimPart(r,pulse,t,out double rz,out double rs,out double roll);Same(new[]{rz,rs,roll},V(f,"rim"),"hadouken rim");
                    int i=0;var hot=f.GetProperty("hot");
                    foreach(var tg in f.GetProperty("tongues").EnumerateArray())
                    {
                        Check(NativeHadoukenFx.TongueVisible(i,q)==(tg.ValueKind!=JsonValueKind.Null),"tongue "+i+" visibility");Check(NativeHadoukenFx.TongueHot(i)==hot[i].GetBoolean(),"tongue "+i+" material");
                        if(tg.ValueKind!=JsonValueKind.Null){NativeHadoukenFx.Tongue(i,r,t,tongue);Same(tongue,A(tg),"tongue "+i);}
                        i++;
                    }
                    Check(i==NativeHadoukenFx.Tongues,"tongue count");
                }
            }
            Check(NativeHadoukenFx.TongueIndices.Length==72&&NativeHadoukenFx.TongueIndices[3]==1&&NativeHadoukenFx.TongueIndices[5]==2,"tongue indices");
        }
        private static void Demon(JsonElement run)
        {
            var ghosts=new NativeDemonGhosts();int epoch=-1,k=0;
            foreach(var f in run.GetProperty("frames").EnumerateArray())
            {
                double t=40+k/60.0,q=D(f,"q");int e=f.GetProperty("epoch").GetInt32();if(e!=epoch){ghosts.Clear();epoch=e;}
                bool a1Alive=!(k>=30&&k<40);double a2At=k<45?40.2:40.85;
                // Players in source order: a1, a2 (the pistol pilot never steps).
                for(int p=1;p<=2;p++)
                {
                    double at=p==1?40.05:a2At;bool alive=p==1?a1Alive:true,dashing=t>=at&&t-at<.3;
                    if(!alive||!dashing||!ghosts.Due(p,t,at,q))continue;ghosts.Take(p,t,q);
                }
                int s=0;
                foreach(var slot in f.GetProperty("slots").EnumerateArray())
                {
                    string owner=slot[1].GetString();int o=owner=="a1"?1:owner=="a2"?2:0;
                    Near(ghosts.At[s],slot[0].GetDouble(),"demon frame "+k+" slot "+s+" time");Check(ghosts.Owner[s]==o,"demon frame "+k+" slot "+s+" owner");
                    bool ownerAlive=o==2||o==1&&a1Alive;Check((ghosts.Live(s,t)&&ownerAlive)==(slot[2].GetInt32()==1),"demon frame "+k+" slot "+s+" visible");
                    Near(NativeDemonGhosts.Fade(t-ghosts.At[s]),slot[3].GetDouble(),"demon frame "+k+" slot "+s+" opacity");
                    Check(NativeDemonGhosts.Color(s)==(s%2==1?0x594de7:0x9c7cff),"demon slot colour");s++;
                }
                Check(s==NativeDemonGhosts.Slots,"demon slots");k++;
            }
        }
    }
}
#endif
