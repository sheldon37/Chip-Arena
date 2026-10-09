#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // C18: the ragdoll kill effects (Core/NativeKillEffectRules.cs) against values captured from the original
    // ragdoll-vfx.ts, gibber-vfx.ts, body-trauma.ts and death-effects.ts (Tests/kill-effect-parity.json, written by
    // tools/export_kill_effect_fixtures.mjs), and the generated rig box table against the source rig's own world matrices.
    internal static class KillEffectChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Kill effect parity: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static double[] Arr(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        // tools/export_kill_effect_fixtures.mjs digest(): count, index-weighted column sums, first and last rows. Point
        // attributes and instance matrices are Float32Arrays in three.js, so each value may differ by a float's rounding.
        private const double Float32=1.2e-7;
        private static void Digest(JsonElement expected,List<double[]> rows,string what,double tolerance=2e-6)
        {
            Check(expected.GetProperty("n").GetInt32()==rows.Count,what+" count "+rows.Count+" != "+expected.GetProperty("n").GetInt32());if(rows.Count==0)return;
            var sums=Arr(expected.GetProperty("sums"));
            for(int k=0;k<sums.Length;k++){double s=0,bound=0;for(int i=0;i<rows.Count;i++){s+=(i+1)*rows[i][k];bound+=(i+1)*Math.Abs(rows[i][k]);}Near(s,sums[k],tolerance*10+Float32*bound,what+" sum "+k);}
            var first=Arr(expected.GetProperty("first"));var last=Arr(expected.GetProperty("last"));
            for(int k=0;k<first.Length;k++){Near(rows[0][k],first[k],tolerance+Float32*Math.Abs(first[k]),what+" first "+k);Near(rows[rows.Count-1][k],last[k],tolerance+Float32*Math.Abs(last[k]),what+" last "+k);}
        }
        private static void Lin(int hex,out double r,out double g,out double b)=>NativeKillEffectRules.Linear(hex,out r,out g,out b);
        private static int ColorHex(string css)=>NativeKillEffectRules.ParseHex(css,-1);

        public static void Run(string root)
        {
            count=0;using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/kill-effect-parity.json")));var R=document.RootElement;
            var c=R.GetProperty("constants");
            Check(D(c,"deathSeconds")==NativeKillEffectRules.DeathSeconds&&c.GetProperty("deathVoxels").GetInt32()==NativeKillEffectRules.DeathVoxels&&c.GetProperty("reducedDeathVoxels").GetInt32()==NativeKillEffectRules.ReducedDeathVoxels&&c.GetProperty("maxDeathVoxels").GetInt32()==NativeKillEffectRules.MaxDeathVoxels,"ANIMATION death constants");
            Check(Math.Abs(D(c,"keepAge")-NativeKillEffectRules.KeepAge)<1e-12&&c.GetProperty("keptBudget").GetInt32()==NativeKillEffectRules.KeptBudget,"DOWNED keep age and budget");
            Check(D(c,"arenaWidth")/2==NativeKillEffectRules.ArenaHalfWidth&&D(c,"arenaDepth")/2==NativeKillEffectRules.ArenaHalfDepth,"ARENA floor bounds");
            int k0=0;foreach(var s in c.GetProperty("styles").EnumerateArray())Check(s.GetString()==NativeKillEffectRules.SwarmStyles[k0++],"RAGDOLL_STYLES order");Check(k0==9,"nine ragdoll styles");
            var markers=R.GetProperty("markers");int markBody=ColorHex(markers.GetProperty("body").GetString());
            Check(NativeKillEffectRules.EdgeOf(markBody)==markers.GetProperty("edge").GetInt32()&&NativeKillEffectRules.EdgeOf(markBody)==NativeKillBoxes.MarkerEdge,"edgeOf");
            Check(NativeKillEffectRules.FlexOf(markBody)==markers.GetProperty("flex").GetInt32()&&NativeKillEffectRules.FlexOf(markBody)==NativeKillBoxes.MarkerFlex,"flexOf");
            foreach(int body in new[]{0xe5e7eb,0x33aa77,0x26547c,0x101010,0xffffff})Check(NativeKillEffectRules.EdgeOf(body)>=0&&NativeKillEffectRules.FlexOf(body)>=0,"edge/flex range");

            // 1. RagdollVfx counts.
            foreach(var e in R.GetProperty("swarmCounts").EnumerateArray())
            {
                NativeKillEffectRules.SwarmCounts(e.GetProperty("style").GetString(),e.GetProperty("budget").GetInt32(),D(e,"quality"),out int total,out int n,out int rings,out bool core);
                Check(total==e.GetProperty("total").GetInt32()&&n==e.GetProperty("count").GetInt32()&&rings==e.GetProperty("rings").GetInt32()&&core==e.GetProperty("core").GetBoolean(),"swarm counts "+e);
            }
            // 2. RagdollVfx frames.
            var parts=new double[33];
            foreach(var run in R.GetProperty("swarm").EnumerateArray())
            {
                string style=run.GetProperty("style").GetString();var v=new NativeKillSwarm(style,run.GetProperty("seed").GetInt32(),run.GetProperty("budget").GetInt32(),D(run,"quality"));string tag=style+" q"+D(run,"quality");
                Near(v.Size,D(run,"size"),1e-12,tag+" point size");int i0=0;foreach(var col in run.GetProperty("colors").EnumerateArray()){var a=Arr(col);for(int k=0;k<3;k++)Near(v.Colors[i0*3+k],a[k],2e-6,tag+" point colour");i0++;}Check(i0==v.Total,tag+" points");
                i0=0;foreach(var col in run.GetProperty("ringColors").EnumerateArray())Check(v.RingColor[i0++]==col.GetInt32(),tag+" ring colour");Check(i0==v.Rings,tag+" rings");
                i0=0;foreach(var tube in run.GetProperty("ringTubes").EnumerateArray())Near(v.RingTube[i0++],tube.GetDouble(),1e-12,tag+" ring tube");
                var coreColor=run.GetProperty("coreColor");Check(v.HasCore==(coreColor.ValueKind!=JsonValueKind.Null)&&(!v.HasCore||coreColor.GetInt32()==NativeKillSwarm.CoreColor),tag+" core");
                foreach(var f in run.GetProperty("frames").EnumerateArray())
                {
                    double age=D(f,"age"),cx=.4*Math.Sin(age),cy=.35+.1*Math.Cos(age*2),cz=-.3+.2*age;string at=tag+" @"+age;
                    for(int k=0;k<11;k++){parts[k*3]=cx+Math.Cos(k*1.3+age)*.3;parts[k*3+1]=cy+((k%4)*.15-.1);parts[k*3+2]=cz+Math.Sin(k*1.7-age*.5)*.25;}
                    v.Update(age,cx,cy,cz,parts,11);var rows=new List<double[]>();for(int i=0;i<v.Total;i++)rows.Add(new[]{v.Points[i*3],v.Points[i*3+1],v.Points[i*3+2]});
                    Digest(f.GetProperty("points"),rows,at+" points");Near(v.Opacity,D(f,"opacity"),2e-6,at+" opacity");Near(v.Collapse,D(f,"collapse"),2e-6,at+" collapse");
                    var body=f.GetProperty("body");Check((v.BodyColor<0?0xe5e7eb:v.BodyColor)==body.GetProperty("color").GetInt32(),at+" body colour");Check((v.BodyEmissive<0?0:v.BodyEmissive)==body.GetProperty("emissive").GetInt32(),at+" body emissive");Near(v.BodyOpacity,D(body,"opacity"),2e-6,at+" body opacity");
                    var core=f.GetProperty("core");if(core.ValueKind!=JsonValueKind.Null){var p=Arr(core.GetProperty("p"));Near(cx,p[0],2e-6,at+" core x");Near(cy,p[1],2e-6,at+" core y");Near(cz,p[2],2e-6,at+" core z");Near(v.CoreScale,D(core,"s"),2e-6,at+" core scale");}
                    int r=0;foreach(var ring in f.GetProperty("rings").EnumerateArray())
                    {
                        var p=Arr(ring.GetProperty("p"));Near(cx,p[0],2e-6,at+" ring x");var e=Arr(ring.GetProperty("r"));
                        Near(v.RingScale[r],D(ring,"s"),2e-6,at+" ring scale "+r);Near(v.RingOpacity[r],D(ring,"opacity"),2e-6,at+" ring opacity "+r);Check(v.RingVisible[r]==ring.GetProperty("visible").GetBoolean(),at+" ring visible "+r);
                        for(int k=0;k<3;k++)Near(v.RingEuler[r*3+k],e[k],2e-6,at+" ring euler "+r);r++;
                    }
                }
            }
            // 3. GibberVfx.
            foreach(var run in R.GetProperty("spray").EnumerateArray())
            {
                string name=run.GetProperty("profile").GetString();var profile=name=="gibber"?KillSprayProfile.Gibber:name=="sever"?KillSprayProfile.Sever:KillSprayProfile.Crush;string tag=name+" q"+D(run,"quality")+" b"+run.GetProperty("budget").GetInt32();
                var g=new NativeKillSpray(run.GetProperty("seed").GetInt32(),run.GetProperty("budget").GetInt32(),D(run,"quality"),profile,D(run,"floor"));
                Check(g.Count==run.GetProperty("count").GetInt32()&&g.Drops==run.GetProperty("drops").GetInt32()&&g.Pools==run.GetProperty("pools").GetInt32(),tag+" counts "+g.Drops+"/"+g.Pools);
                Check(g.Glow==(run.GetProperty("glow").ValueKind!=JsonValueKind.Null),tag+" wound material");Check(profile==KillSprayProfile.Gibber?run.GetProperty("material").GetInt32()==NativeKillSpray.MaterialGibber:run.GetProperty("material").GetInt32()==0xffffff,tag+" material colour");
                int i0=0;foreach(var col in run.GetProperty("colors").EnumerateArray()){Check(col.GetInt32()==(i0%3!=0?0xc91428:0x750516),tag+" instance colour");i0++;}Check(i0==g.Drops,tag+" colours");
                double elapsed=0;
                foreach(var f in run.GetProperty("frames").EnumerateArray())
                {
                    double age=D(f,"age");string at=tag+" @"+age;
                    while(elapsed+1/60.0<=age+1e-6)
                    {
                        double t=elapsed,nx=Math.Sin(t),ny=1,nz=.4*Math.Cos(t),l=Math.Sqrt(nx*nx+ny*ny+nz*nz);
                        g.Step(elapsed,.3*Math.Sin(t*2.1),1.1-.6*Math.Min(1,t)+.05*Math.Sin(t*5),-.2+.15*t,nx/l,ny/l,nz/l);elapsed+=1/60.0;
                    }
                    g.Update(age);var rows=new List<double[]>();var shown=new List<double[]>();
                    for(int d=0;d<g.Drops;d++){rows.Add(new[]{g.Pos[d*3],g.Pos[d*3+1],g.Pos[d*3+2],g.Vel[d*3],g.Vel[d*3+1],g.Vel[d*3+2],g.Life[d],g.Sizes[d]});if(g.Life[d]>0)shown.Add(new[]{d,g.Pos[d*3],g.Pos[d*3+1],g.Pos[d*3+2],g.Q[d*4],g.Q[d*4+1],g.Q[d*4+2],g.Q[d*4+3],g.S[d*3],g.S[d*3+1],g.S[d*3+2]});}
                    Digest(f.GetProperty("drops"),rows,at+" drops",5e-6);Digest(f.GetProperty("shown"),shown,at+" drop matrices",5e-6);
                    int p=0;foreach(var pool in f.GetProperty("pools").EnumerateArray())
                    {
                        Check(g.PoolVisible[p]==pool.GetProperty("visible").GetBoolean(),at+" pool visible "+p);var pp=Arr(pool.GetProperty("p"));var ps=Arr(pool.GetProperty("s"));
                        if(g.PoolVisible[p]){for(int k=0;k<3;k++)Near(g.PoolPos[p*3+k],pp[k],5e-6,at+" pool position");Near(g.PoolScale[p*2],ps[0],2e-6,at+" pool sx");Near(g.PoolScale[p*2+1],ps[1],2e-6,at+" pool sy");Near(g.PoolRz[p],D(pool,"rz"),2e-6,at+" pool rz");}p++;
                    }
                    Check(p==g.Pools,at+" pools");if(g.Pools>0)Near(g.PoolOpacity,D(f,"poolOpacity"),2e-6,at+" pool opacity");
                }
            }
            // 4. woundCap cells.
            var cells=new KillCell[NativeKillCaps.MaxCells];
            foreach(var cap in R.GetProperty("caps").EnumerateArray())
            {
                Lin(cap.GetProperty("shell").GetInt32(),out double sr,out double sg,out double sb);string b=cap.GetProperty("bone").GetString();string tag="cap "+cap.GetProperty("kind").GetString()+" "+D(cap,"width");
                int n=NativeKillCaps.Cap(D(cap,"width"),D(cap,"depth"),cap.GetProperty("kind").GetString()=="ragged",(uint)D(cap,"seed"),sr,sg,sb,b=="back"?1:b=="none"?2:0,cap.GetProperty("low").GetBoolean(),cells);
                var expected=cap.GetProperty("cells");Check(n==expected.GetArrayLength(),tag+" cells "+n+" != "+expected.GetArrayLength());int i=0;
                foreach(var e in expected.EnumerateArray())
                {
                    var p=Arr(e.GetProperty("p"));var q=Arr(e.GetProperty("q"));var s=Arr(e.GetProperty("s"));var col=Arr(e.GetProperty("c"));var x=cells[i++];
                    Near(x.X,p[0],2e-6,tag+" x");Near(x.Y,p[1],2e-6,tag+" y");Near(x.Z,p[2],2e-6,tag+" z");Near(x.Qx,q[0],2e-6,tag+" qx");Near(x.Qy,q[1],2e-6,tag+" qy");Near(x.Qz,q[2],2e-6,tag+" qz");Near(x.Qw,q[3],2e-6,tag+" qw");
                    Near(x.Sx,s[0],2e-6,tag+" sx");Near(x.Sy,s[1],2e-6,tag+" sy");Near(x.Sz,s[2],2e-6,tag+" sz");Near(x.R,col[0],2e-6,tag+" r");Near(x.G,col[1],2e-6,tag+" g");Near(x.B,col[2],2e-6,tag+" b");
                }
            }
            // 5. The box table rebuilds the source rig's world matrices, colours and faceAlong.
            var rig=new KillBox[NativeKillBoxes.MaxRigBoxes];var scratch=new double[16];var world=new double[16];var inverse=new double[16];
            double[][] Bones(JsonElement list){var bones=new double[22][];int i=0;foreach(var m in list.EnumerateArray())bones[i++]=Arr(m);return bones;}
            var rigs=new List<JsonElement>();foreach(var r in R.GetProperty("rigs").EnumerateArray())rigs.Add(r);
            foreach(var r in rigs)
            {
                string spec=r.GetProperty("spec").GetString(),hat=r.GetProperty("hat").ValueKind==JsonValueKind.Null?"none":r.GetProperty("hat").GetString();int accent=ColorHex(r.GetProperty("accent").GetString()),body=ColorHex(r.GetProperty("body").GetString());
                var bones=Bones(r.GetProperty("bones"));int n=NativeKillBoxes.Rig(spec,hat,rig);var boxes=r.GetProperty("boxes");Check(n==boxes.GetArrayLength(),spec+" rig boxes "+n+" != "+boxes.GetArrayLength());int i=0;
                foreach(var e in boxes.EnumerateArray())
                {
                    var box=rig[i++];Check(box.Name==e.GetProperty("name").GetString(),spec+" box order "+box.Name);NativeKillEffectRules.BoxWorld(box,bones[box.Bone],scratch,world);var m=Arr(e.GetProperty("m"));
                    for(int k=0;k<16;k++)Near(world[k],m[k],5e-6,spec+" box world "+box.Name);Check(NativeKillEffectRules.BoxColor(box,body,accent)==e.GetProperty("c").GetInt32(),spec+" box colour "+box.Name);
                }
                foreach(var face in r.GetProperty("faces").EnumerateArray())
                {
                    int joint=face.GetProperty("joint").GetInt32(),owner=face.GetProperty("owner").GetInt32();bool parent=face.GetProperty("side").GetString()=="parent";var list=new List<double>();
                    for(int b=0;b<n;b++){var box=rig[b];if(box.Root!=owner||box.Zone!=0||box.Detail)continue;NativeKillEffectRules.BoxWorld(box,bones[box.Bone],scratch,world);NativeKillEffectRules.Invert(world,inverse);list.AddRange(inverse);}
                    Check(list.Count/16==face.GetProperty("count").GetInt32(),spec+" faceAlong meshes "+joint);var jm=bones[joint];double sign=joint==5||joint==2?1:-1;
                    double s=NativeKillCaps.FaceAlong(jm[12],jm[13],jm[14],jm[4]*sign,jm[5]*sign,jm[6]*sign,list.ToArray(),list.Count/16,parent);Near(s,D(face,"s"),1e-9,spec+" faceAlong "+joint+" "+(parent?"parent":"child"));
                }
            }
            // 6. voxel-burst.
            foreach(var run in R.GetProperty("burst").EnumerateArray())
            {
                var r=rigs[run.GetProperty("rig").GetInt32()];string spec=r.GetProperty("spec").GetString(),hat=r.GetProperty("hat").ValueKind==JsonValueKind.Null?"none":r.GetProperty("hat").GetString();int accent=ColorHex(r.GetProperty("accent").GetString()),body=ColorHex(r.GetProperty("body").GetString());
                var bones=Bones(run.GetProperty("bones"));int n=NativeKillBoxes.Rig(spec,hat,rig);var matrices=new double[n*16];var colors=new int[n];
                for(int b=0;b<n;b++){NativeKillEffectRules.BoxWorld(rig[b],bones[rig[b].Bone],scratch,world);Array.Copy(world,0,matrices,b*16,16);colors[b]=NativeKillEffectRules.BoxColor(rig[b],body,accent);}
                var d=run.GetProperty("death");var p=d.GetProperty("pos");var vel=d.GetProperty("velocity");var dir=d.GetProperty("direction");string tag="burst "+spec+" q"+D(run,"quality");
                var burst=new NativeKillBurst(matrices,colors,n,d.GetProperty("seed").GetInt32(),D(p,"x"),D(p,"y"),D(p,"z"),D(vel,"x"),D(vel,"y"),D(vel,"z"),D(dir,"x"),D(dir,"y"),D(dir,"z"),D(run,"quality"),run.GetProperty("budget").GetInt32());
                Check(burst.Count==run.GetProperty("count").GetInt32(),tag+" count "+burst.Count);int i=0;foreach(var col in run.GetProperty("colors").EnumerateArray())Check(burst.Colors[i++]==col.GetInt32(),tag+" chunk colour "+(i-1));
                foreach(var f in run.GetProperty("frames").EnumerateArray())
                {
                    double age=D(f,"age");var rows=new List<double[]>();for(int k=0;k<burst.Count;k++){burst.Pose(k,age,out double x,out double y,out double z,out double ex,out double ey,out double ez,out double s);rows.Add(new[]{x,y,z,ex,ey,ez,s});}
                    Digest(f.GetProperty("chunks"),rows,tag+" @"+age,5e-6);
                }
            }
            // 7. CrushDebris.
            foreach(var run in R.GetProperty("debris").EnumerateArray())
            {
                var sources=run.GetProperty("sources");int n=sources.GetArrayLength();var m=new double[n*16];var col=new double[n*3];int i=0;
                foreach(var s in sources.EnumerateArray()){Array.Copy(Arr(s.GetProperty("m")),0,m,i*16,16);Lin(s.GetProperty("c").GetInt32(),out col[i*3],out col[i*3+1],out col[i*3+2]);i++;}
                var lp=Arr(run.GetProperty("loose").GetProperty("p"));var lq=Arr(run.GetProperty("loose").GetProperty("q"));var o=Arr(run.GetProperty("origin"));string tag="debris "+run.GetProperty("limit").GetInt32();
                var debris=new NativeKillDebris(m,col,n,lp,1,o[0],o[1],o[2],.3,0,-1,1,.5,-2,2,D(run,"severity"),3141,run.GetProperty("limit").GetInt32(),.2);
                Check(debris.Count==run.GetProperty("count").GetInt32(),tag+" count "+debris.Count);
                var colorRows=new List<double[]>();for(int k=0;k<debris.Chunks;k++)colorRows.Add(new[]{debris.Colors[k*3],debris.Colors[k*3+1],debris.Colors[k*3+2]});Digest(run.GetProperty("colors"),colorRows,tag+" colours");
                foreach(var f in run.GetProperty("frames").EnumerateArray())
                {
                    double age=D(f,"age");var rows=new List<double[]>();
                    for(int k=0;k<debris.Chunks;k++){debris.Chunk(k,age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double s);if(qw<0){qx=-qx;qy=-qy;qz=-qz;qw=-qw;}rows.Add(new[]{x,y,z,qx,qy,qz,qw,s});}
                    Digest(f.GetProperty("chunks"),rows,tag+" @"+age,5e-6);
                    if(debris.Loose>0)
                    {
                        debris.LooseAt(0,age,out double x,out double y,out double z,out double sx,out double sy,out double sz,out double sw,out double s);var e=f.GetProperty("loose");var p=Arr(e.GetProperty("p"));var q=Arr(e.GetProperty("q"));
                        double qx=sw*lq[0]+sx*lq[3]+sy*lq[2]-sz*lq[1],qy=sw*lq[1]-sx*lq[2]+sy*lq[3]+sz*lq[0],qz=sw*lq[2]+sx*lq[1]-sy*lq[0]+sz*lq[3],qw=sw*lq[3]-sx*lq[0]-sy*lq[1]-sz*lq[2];
                        Near(x,p[0],5e-6,tag+" loose x");Near(y,p[1],5e-6,tag+" loose y");Near(z,p[2],5e-6,tag+" loose z");Near(qx,q[0],5e-6,tag+" loose qx");Near(qy,q[1],5e-6,tag+" loose qy");Near(qz,q[2],5e-6,tag+" loose qz");Near(qw,q[3],5e-6,tag+" loose qw");Near(s,D(e,"s"),2e-6,tag+" loose scale");
                    }
                }
            }
            // 8. bodyPartVelocity for a crushed part.
            var ragdoll=R.GetProperty("ragdoll");Check(D(ragdoll,"maxSpeed")==NativeKillEffectRules.MaxBodySpeed&&D(ragdoll,"maxDetachedKick")==NativeKillEffectRules.MaxDetachedKick,"BODY_RAGDOLL constants");
            foreach(var e in ragdoll.GetProperty("detached").EnumerateArray())
            {
                var v=Arr(e.GetProperty("velocity"));var d=Arr(e.GetProperty("direction"));var o=Arr(e.GetProperty("out"));int part=e.GetProperty("part").GetInt32();
                NativeKillEffectRules.DetachedVelocity(v[0],v[1],v[2],part,D(e,"momentum"),d[0],d[1],d[2],out double x,out double y,out double z);
                Near(x,o[0],1e-9,"detached velocity x "+part);Near(y,o[1],1e-9,"detached velocity y "+part);Near(z,o[2],1e-9,"detached velocity z "+part);
            }
            // The ragdoll skips invisible meshes; only held weapons carry hidden boxes, so caps and fragments never need them.
            foreach(var b in NativeKillBoxes.Body)Check(!b.Hidden,"visible body box "+b.Name);foreach(var list in NativeKillBoxes.HatBoxes)foreach(var b in list)Check(!b.Hidden,"visible hat box "+b.Name);
            // Rules the fixtures do not reach: the stock ending, quality mapping and budget arithmetic.
            Near(NativeKillEffectRules.Fade(4.35),1,1e-12,"fade holds until 4.35 s");Near(NativeKillEffectRules.Fade(5),0,1e-12,"fade ends at 5 s");
            Near(NativeKillEffectRules.Sink(5),.35,1e-12,"sinks .35 m");Near(NativeKillEffectRules.PartScale(5),.85,1e-12,"shrinks to .85");Near(NativeKillEffectRules.Sink(2),0,1e-12,"no sink before the fade");
            Check(new NativeKillSpray(1,280,1,KillSprayProfile.Gibber,0).Drops==150&&new NativeKillSpray(1,280,.3,KillSprayProfile.Gibber,0).Drops==55,"gibber fountain 150 / 55");
            Check(new NativeKillSpray(1,280,1,KillSprayProfile.Sever,0).Drops==70&&new NativeKillSpray(1,280,.3,KillSprayProfile.Crush,0).Drops==26,"sever 70, crush 26 reduced");
            Check(new NativeKillSpray(1,0,1,KillSprayProfile.Gibber,0).Count==0,"no budget, no spray");
            var empty=new NativeKillSwarm("ragdoll-static",3,269,1);empty.Update(1,0,0,0,parts,0);Check(empty.Points[0]==0&&empty.Points[1]==0,"static points without parts stay on the centre");
            Console.WriteLine("PASS kill effects: "+count+" checks against ragdoll-vfx.ts, gibber-vfx.ts, body-trauma.ts and voxelBurst (C18)");
        }
    }
}
#endif
