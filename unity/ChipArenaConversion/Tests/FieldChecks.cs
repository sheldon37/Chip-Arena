#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Field telegraphs: NativeFieldRules against what the pinned source draws (tools/export_field_fixtures.mjs runs
    // client/chaos-visuals.ts, client/siren/fx.ts and client/ability-preview.ts), plus the owner's no-outline rule, the
    // ally/enemy truth table and the core formulas.
    internal static class FieldChecks
    {
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Field telegraph check failed: "+what);}
        private static void Near(double a,double b,string what,double tolerance=2e-4){if(!(Math.Abs(a-b)<=tolerance))throw new Exception("Field telegraph parity: "+what+": "+a+" != "+b);}
        private static double[] Numbers(JsonElement e){var list=new List<double>();foreach(var v in e.EnumerateArray())list.Add(v.GetDouble());return list.ToArray();}
        private static string Text(JsonElement e)=>e.ValueKind==JsonValueKind.Null?null:e.GetString();
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/field-parity.json")));var json=document.RootElement;var k=json.GetProperty("constants");
            Check(k.GetProperty("glowAlpha").GetDouble()==NativeFieldRules.GlowAlpha&&k.GetProperty("ally").GetInt32()==NativeFieldRules.Ally&&k.GetProperty("enemy").GetInt32()==NativeFieldRules.Enemy,"glow alpha and telegraph colours match the source");
            Geometry(json);int frames=Fields(json),rows=Tint(json),siren=Siren(json),motes=Preview(json);
            Rim();NoOutline();Truth();Formulas();
            Console.WriteLine("PASS "+json.GetProperty("fields").GetArrayLength()+" source field telegraph runs ("+frames+" frames: glow, cores, domes for every kind and fx), "+rows+" tint rows, "+siren+" Siren floor telegraph frames, "+motes+" aim preview motes, and rim alpha, no-outline, tint truth table and core formula checks");
        }
        private static void Mesh(JsonElement g,NativeFieldMesh mesh,double x,double y,double z,double scale,double alpha,string what)
        {
            var world=Numbers(g.GetProperty("world"));var a=Numbers(g.GetProperty("alpha"));var index=Numbers(g.GetProperty("index"));
            Check(a.Length==mesh.Count&&index.Length==mesh.Triangles.Length,what+" vertex and index counts "+mesh.Count+"/"+mesh.Triangles.Length+" vs "+a.Length+"/"+index.Length);
            for(int i=0;i<mesh.Count;i++)
            {
                Near(x+mesh.Positions[i*3]*scale,world[i*3],what+" vertex "+i+" x");Near(y+mesh.Positions[i*3+1]*scale,world[i*3+1],what+" vertex "+i+" y");Near(z+mesh.Positions[i*3+2]*scale,world[i*3+2],what+" vertex "+i+" z");
                Near(mesh.Alpha[i]*alpha,a[i],what+" vertex "+i+" alpha",1e-6);
            }
            for(int i=0;i<index.Length;i++)Check(mesh.Triangles[i]==(int)index[i],what+" triangle order at "+i);
            Check(!g.GetProperty("depthWrite").GetBoolean()&&g.GetProperty("transparent").GetBoolean()&&g.GetProperty("blending").GetInt32()==1&&g.GetProperty("side").GetInt32()==0,what+" is a front-faced normal-blend transparent without depth write");
        }
        private static void Geometry(JsonElement json)
        {
            // Both sample fields sit at (1.5, .95, -2) with R 3.
            var disc=json.GetProperty("glowGeometry").GetProperty("disc");Mesh(disc,NativeFieldRules.GlowDisc,1.5,NativeFieldRules.GlowY(.95),-2,3,NativeFieldRules.GlowAlpha,"glow disc");
            Check(disc.GetProperty("renderOrder").GetInt32()==-1&&!disc.GetProperty("toneMapped").GetBoolean()&&disc.GetProperty("vertexColors").GetBoolean(),"glow disc draws first among transparents, not tone mapped");
            var dome=json.GetProperty("domeGeometry").GetProperty("dome");Mesh(dome,NativeFieldRules.Dome,1.5,NativeFieldRules.Origin(.95),-2,3,NativeFieldRules.DomeAlpha,"dome");
            Check(dome.GetProperty("renderOrder").GetInt32()==0,"dome after the glow disc");
        }
        private static int Fields(JsonElement json)
        {
            int frames=0;var cubes=new NativeFieldCube[NativeFieldRules.MaxCubes];
            foreach(var c in json.GetProperty("fields").EnumerateArray())
            {
                string kind=c.GetProperty("kind").GetString(),fx=Text(c.GetProperty("fx"));var pos=Numbers(c.GetProperty("pos"));double R=c.GetProperty("radius").GetDouble();
                var f=new NativeBlastField{Id=1,Kind=kind,Fx=fx,X=pos[0],Y=pos[1],Z=pos[2],Radius=R,Born=c.GetProperty("born").GetDouble(),Until=c.GetProperty("until").GetDouble()};
                // The fixture's viewer is an enemy of the owner in free-for-all.
                int color=NativeFieldRules.Color(kind,fx,true,false);var look=NativeFieldRules.LookOf(kind,fx);string name=kind+":"+(fx??"-");
                foreach(var frame in c.GetProperty("frames").EnumerateArray())
                {
                    double t=frame.GetProperty("t").GetDouble();string at=name+" t="+t;int n=NativeFieldRules.Cubes(f,color,t,cubes),seen=0;bool glow=false,dome=false;
                    foreach(var child in frame.GetProperty("children").EnumerateArray())
                    {
                        var v=new List<JsonElement>();foreach(var e in child.EnumerateArray())v.Add(e);string role=v[0].GetString();
                        double X=v[1].GetDouble(),Y=v[2].GetDouble(),Z=v[3].GetDouble(),qx=v[4].GetDouble(),qy=v[5].GetDouble(),qz=v[6].GetDouble(),qw=v[7].GetDouble(),sx=v[8].GetDouble(),sy=v[9].GetDouble(),sz=v[10].GetDouble(),opacity=v[12].GetDouble();int hex=v[11].GetInt32();bool visible=v[13].GetInt32()==1;
                        if(role=="glow"){glow=true;Near(X,f.X,at+" glow x");Near(Y,NativeFieldRules.GlowY(f.Y),at+" glow y");Near(Z,f.Z,at+" glow z");Near(sx,R,at+" glow scale");Near(sz,R,at+" glow scale z");Check(hex==color&&Math.Abs(opacity-NativeFieldRules.GlowAlpha)<1e-9,at+" glow colour "+hex.ToString("x6")+" vs "+color.ToString("x6"));continue;}
                        if(role=="dome"){dome=true;Check(look==NativeFieldLook.Dome,at+" dome look");Near(X,f.X,at+" dome x");Near(Y,NativeFieldRules.Origin(f.Y),at+" dome y");Near(sx,R,at+" dome scale");Near(sy,R,at+" dome scale y");Check(hex==color&&Math.Abs(opacity-NativeFieldRules.DomeAlpha)<1e-9&&Math.Abs(qw-1)<1e-6,at+" dome colour and pose");continue;}
                        if(!visible)continue;
                        Check(seen<n,at+" source draws more cubes than the port ("+role+")");var cube=cubes[seen++];
                        Near(cube.X,X,at+" "+role+" x");Near(cube.Y,Y,at+" "+role+" y");Near(cube.Z,Z,at+" "+role+" z");Near(cube.Sx,sx,at+" "+role+" size x");Near(cube.Sy,sy,at+" "+role+" size y");Near(cube.Sz,sz,at+" "+role+" size z");
                        double dot=cube.Qx*qx+cube.Qy*qy+cube.Qz*qz+cube.Qw*qw;Check(Math.Abs(Math.Abs(dot)-1)<2e-4,at+" "+role+" rotation "+dot);Check(cube.Color==hex&&opacity==1,at+" "+role+" colour "+cube.Color.ToString("x6")+" vs "+hex.ToString("x6"));
                    }
                    Check(seen==n,at+" port draws "+n+" cubes, source "+seen);Check(glow,at+" every field has a glow disc");Check(dome==(look==NativeFieldLook.Dome),at+" dome only for time and repulsion");frames++;
                }
            }
            return frames;
        }
        private static int Tint(JsonElement json)
        {
            int rows=0;
            foreach(var r in json.GetProperty("tint").EnumerateArray())
            {
                string kind=r.GetProperty("kind").GetString(),fx=Text(r.GetProperty("fx"));bool ally=NativeFieldRules.IsAlly(r.GetProperty("self").GetBoolean(),r.GetProperty("pve").GetBoolean(),r.GetProperty("teams").GetBoolean(),r.GetProperty("sameTeam").GetBoolean());
                int color=NativeFieldRules.Color(kind,fx,r.GetProperty("viewer").GetBoolean(),ally);
                Check(color==r.GetProperty("color").GetInt32(),"tint "+kind+":"+(fx??"-")+" "+r.GetProperty("label").GetString()+" "+color.ToString("x6")+" vs "+r.GetProperty("color").GetInt32().ToString("x6"));rows++;
            }
            return rows;
        }
        private static void Pose(JsonElement o,double x,double y,double z,double scale,bool visible,string what)
        {
            var p=Numbers(o.GetProperty("pos"));var q=Numbers(o.GetProperty("rot"));Check((o.GetProperty("visible").GetInt32()==1)==visible,what+" visibility");
            Near(p[0],x,what+" x");Near(p[1],y,what+" y");Near(p[2],z,what+" z");Near(o.GetProperty("scale").GetDouble(),scale,what+" scale");
            // The source turns its XY geometry to face +Y (rotation.x = -PI/2); the port's templates are built facing +Y.
            Check(Math.Abs(q[0]+Math.Sqrt(.5))<1e-4&&Math.Abs(q[3]-Math.Sqrt(.5))<1e-4,what+" faces up");
        }
        private static int Siren(JsonElement json)
        {
            int frames=0;var siren=json.GetProperty("siren");
            Check(siren.GetProperty("flickerGlsl").GetString().Contains("uFlicker*(0.5+0.5*sin(uTime*37.0+sin(uTime*11.0)*3.0))"),"flicker formula");
            foreach(var patch in siren.GetProperty("patches").EnumerateArray())
            {
                Check(patch.GetProperty("color").GetInt32()==NativeFieldRules.Scorch&&patch.GetProperty("opacity").GetDouble()==NativeFieldRules.ScorchAlpha&&!patch.GetProperty("depthWrite").GetBoolean()&&patch.GetProperty("renderOrder").GetInt32()==-1,"scorch decal material");
                var offset=Numbers(patch.GetProperty("polygonOffset"));Check(offset[0]==-2&&offset[1]==-2,"scorch decal polygon offset");
                string kind=patch.GetProperty("kind").GetString();Check(NativeFieldRules.SirenPatch(kind),"patch kind "+kind);var pos=Numbers(patch.GetProperty("pos"));double at=patch.GetProperty("at").GetDouble(),until=patch.GetProperty("until").GetDouble(),R=patch.GetProperty("radius").GetDouble();
                foreach(var f in patch.GetProperty("frames").EnumerateArray()){double t=f.GetProperty("t").GetDouble();bool shown=NativeFieldRules.ScorchDecal(at,until,R,t,out double scale);Pose(f,pos[0],pos[1]+NativeFieldRules.ScorchLift,pos[2],scale,shown,"scorch "+kind+" t="+t);frames++;}
            }
            foreach(var e in siren.GetProperty("eruptions").EnumerateArray())
            {
                Check(e.GetProperty("ringColor").GetInt32()==NativeFieldRules.Warn&&Math.Abs(e.GetProperty("ringOpacity").GetDouble()-NativeFieldRules.WarnAlpha)<1e-6&&Math.Abs(e.GetProperty("ringFlicker").GetDouble()-NativeFieldRules.WarnFlicker)<1e-6,"warn ring material");
                Check(e.GetProperty("fillColor").GetInt32()==NativeFieldRules.WarnDeep&&Math.Abs(e.GetProperty("fillOpacity").GetDouble()-NativeFieldRules.FillAlpha)<1e-6&&e.GetProperty("side").GetInt32()==2,"warn fill material (both double-sided)");
                var ring=e.GetProperty("ringGeometry");var fill=e.GetProperty("fillGeometry");int segments=ring.GetProperty("thetaSegments").GetInt32(),fillSegments=fill.GetProperty("segments").GetInt32();
                Check(Math.Abs(ring.GetProperty("innerRadius").GetDouble()-.86)<1e-9&&ring.GetProperty("outerRadius").GetDouble()==1&&ring.GetProperty("phiSegments").GetInt32()==1&&NativeFieldRules.WarnRing.Count==(segments+1)*2&&NativeFieldRules.WarnRing.Triangles.Length==segments*6,"warn ring geometry");
                Check(NativeFieldRules.SirenDisc.Count==fillSegments+2&&NativeFieldRules.SirenDisc.Triangles.Length==fillSegments*3,"Siren disc geometry");
                for(int i=0;i<NativeFieldRules.WarnRing.Count;i++){double r=Math.Sqrt(NativeFieldRules.WarnRing.Positions[i*3]*NativeFieldRules.WarnRing.Positions[i*3]+NativeFieldRules.WarnRing.Positions[i*3+2]*NativeFieldRules.WarnRing.Positions[i*3+2]);Check(Math.Abs(r-.86)<1e-6||Math.Abs(r-1)<1e-6,"warn ring radii");}
                var pos=Numbers(e.GetProperty("pos"));double at=e.GetProperty("at").GetDouble(),burst=e.GetProperty("burstAt").GetDouble(),R=e.GetProperty("radius").GetDouble();Near(burst-at,NativeFieldRules.EruptionWarning,"eruption warning");
                foreach(var f in e.GetProperty("frames").EnumerateArray())
                {
                    double t=f.GetProperty("t").GetDouble();bool shown=NativeFieldRules.EruptionWarn(at,burst,R,t,out double ringScale,out double fillScale);
                    Pose(f.GetProperty("ring"),pos[0],pos[1]+NativeFieldRules.RingLift,pos[2],ringScale,shown,"warn ring t="+t);Pose(f.GetProperty("fill"),pos[0],pos[1]+NativeFieldRules.FillLift,pos[2],fillScale,shown,"warn fill t="+t);
                    Near(NativeFieldRules.Flicker(t),f.GetProperty("flicker").GetDouble(),"flicker t="+t);frames++;
                }
            }
            return frames;
        }
        private static int Preview(JsonElement json)
        {
            int total=0;var into=new float[NativeFieldRules.PreviewCap*4];
            foreach(var c in json.GetProperty("preview").EnumerateArray())
            {
                Check(c.GetProperty("color").GetInt32()==NativeFieldRules.PreviewColor&&Math.Abs(c.GetProperty("opacity").GetDouble()-NativeFieldRules.PreviewAlpha)<1e-6&&Math.Abs(c.GetProperty("size").GetDouble()-NativeFieldRules.PreviewSize)<1e-9&&c.GetProperty("renderOrder").GetInt32()==3,"preview mote material");
                double t=c.GetProperty("t").GetDouble();int used=0;
                foreach(var circle in c.GetProperty("circles").EnumerateArray()){var p=Numbers(circle.GetProperty("pos"));used=NativeFieldRules.PreviewMotes(p[0],p[1],p[2],circle.GetProperty("radius").GetDouble(),circle.GetProperty("clamped").GetBoolean(),t,into,used);}
                string label=c.GetProperty("label").GetString();var motes=c.GetProperty("motes");Check(used==motes.GetArrayLength(),"preview "+label+" mote count "+used+" vs "+motes.GetArrayLength());int i=0;
                foreach(var m in motes.EnumerateArray()){var v=Numbers(m);for(int j=0;j<4;j++)Near(into[i*4+j],v[j],"preview "+label+" mote "+i+" value "+j);i++;}
                total+=used;
            }
            return total;
        }
        // The glow disc has no edge: every triangle joins the centre (alpha 1) to two rim vertices at the radius (alpha 0), so the
        // fragment alpha falls linearly to exactly 0 on the gameplay radius.
        private static void Rim()
        {
            var d=NativeFieldRules.GlowDisc;Check(d.Count==NativeFieldRules.GlowSegments+2&&d.Alpha[0]==1&&d.Positions[0]==0&&d.Positions[2]==0,"glow disc centre");
            for(int i=1;i<d.Count;i++){double r=Math.Sqrt(d.Positions[i*3]*d.Positions[i*3]+d.Positions[i*3+2]*d.Positions[i*3+2]);Check(d.Alpha[i]==0&&Math.Abs(r-1)<1e-6&&d.Positions[i*3+1]==0,"glow disc rim vertex "+i+" alpha "+d.Alpha[i]);}
            for(int t=0;t<d.Triangles.Length;t+=3){int centre=0;for(int j=0;j<3;j++)if(d.Triangles[t+j]==0)centre++;Check(centre==1,"glow disc triangle "+t/3+" fans from the centre");}
            // Facing +Y with the source's counter-clockwise winding, so a back-face-culled Unity draw shows it from above.
            for(int t=0;t<d.Triangles.Length;t+=3){var a=d.Triangles[t];var b=d.Triangles[t+1];var c=d.Triangles[t+2];double ux=d.Positions[b*3]-d.Positions[a*3],uz=d.Positions[b*3+2]-d.Positions[a*3+2],vx=d.Positions[c*3]-d.Positions[a*3],vz=d.Positions[c*3+2]-d.Positions[a*3+2];Check(uz*vx-ux*vz>0,"glow disc triangle "+t/3+" faces up");}
            var dome=NativeFieldRules.Dome;foreach(var a in dome.Alpha)Check(a==1,"dome has uniform alpha (no brighter rim row)");
            for(int t=0;t<dome.Triangles.Length;t+=3)
            {
                int a=dome.Triangles[t],b=dome.Triangles[t+1],c=dome.Triangles[t+2];double[] P(int i)=>new double[]{dome.Positions[i*3],dome.Positions[i*3+1],dome.Positions[i*3+2]};var A=P(a);var B=P(b);var C=P(c);
                double[] u={B[0]-A[0],B[1]-A[1],B[2]-A[2]},v={C[0]-A[0],C[1]-A[1],C[2]-A[2]};double nx=u[1]*v[2]-u[2]*v[1],ny=u[2]*v[0]-u[0]*v[2],nz=u[0]*v[1]-u[1]*v[0];
                Check(nx*(A[0]+B[0]+C[0])+ny*(A[1]+B[1]+C[1])+nz*(A[2]+B[2]+C[2])>0,"dome triangle "+t/3+" faces outward");
            }
            Check(dome.Count==(NativeFieldRules.DomeWidth+1)*(NativeFieldRules.DomeHeight+1)&&dome.Triangles.Length==(NativeFieldRules.DomeWidth*NativeFieldRules.DomeHeight*2-NativeFieldRules.DomeWidth)*3,"dome is the 14x6 hemisphere without a base cap");
        }
        // No geometry traces the gameplay radius: the only things drawn are the glow disc (alpha 0 at the rim), the dome surface
        // and cores whose cubes stay within 1.4 m of the centre, far inside every field radius (2.3 m and up).
        private static void NoOutline()
        {
            var cubes=new NativeFieldCube[NativeFieldRules.MaxCubes];
            foreach(var (kind,fx,R) in new[]{("black-hole",(string)null,5.0),("black-hole","singularity",6.0),("bomb",null,3.0),("bomb","bubble-mine",3.0),("bomb","aftershock",3.5),("bomb","artillery",3.0),("bomb","thunderstep",2.5),("bomb","comet",3.5),("bomb","meteor-strike",4.5),("bomb","orbital-mark",3.0),("bomb","geyser-wall",3.0),("mine","mirror-mine",2.3),("mine","proximity-mail",2.5),("implosion","implosion-bomb",4.0),("time","time-warp",4.0),("repulsion","panic-button",6.0),("acid","acid-rain",3.0),("fire","meteor-strike",3.2),("frost","blizzard",3.5),("heal","med-garden",2.5),("storm","lightning-rod",4.0)})
            {
                var look=NativeFieldRules.LookOf(kind,fx);int most=0;
                for(double t=0;t<4;t+=.05)
                {
                    var f=new NativeBlastField{Kind=kind,Fx=fx,X=2,Y=.95,Z=-1,Radius=R,Born=0,Until=look==NativeFieldLook.Mine?6:1};int n=NativeFieldRules.Cubes(f,0x123456,t,cubes);most=Math.Max(most,n);
                    for(int i=0;i<n;i++){var c=cubes[i];double r=Math.Sqrt((c.X-2)*(c.X-2)+(c.Z+1)*(c.Z+1))+.5*Math.Sqrt(c.Sx*c.Sx+c.Sz*c.Sz);Check(r<1.4&&r<R-.8,kind+":"+fx+" cube "+i+" reaches "+r+" m of R "+R);}
                }
                int expected=look==NativeFieldLook.BlackHole?12:look==NativeFieldLook.Implosion?8:look==NativeFieldLook.Bomb||look==NativeFieldLook.Mine?2:0;
                Check(most==expected,kind+":"+fx+" draws "+most+" cubes, expected "+expected);
            }
            // Ability bombs and the fused chip bombs read through the glow and their strike cues only (no core).
            foreach(var fx in new[]{"artillery","thunderstep","comet","meteor-strike","orbital-mark","geyser-wall"})Check(NativeFieldRules.LookOf("bomb",fx)==NativeFieldLook.Glow,fx+" has no core");
        }
        private static void Truth()
        {
            // Chip fields always keep their colour; thunderstep and comet have their own.
            foreach(var (viewer,ally) in new[]{(false,false),(true,false),(true,true)})
            {
                Check(NativeFieldRules.Color("bomb","bubble-mine",viewer,ally)==0xffa141&&NativeFieldRules.Color("bomb","thunderstep",viewer,ally)==0x9fd2ff&&NativeFieldRules.Color("bomb","comet",viewer,ally)==0xffc04a,"chip bombs keep their colour");
                Check(NativeFieldRules.Color("black-hole",null,viewer,ally)==0xab67ff&&NativeFieldRules.Color("implosion","implosion-bomb",viewer,ally)==0xb068ff&&NativeFieldRules.Color("time","time-warp",viewer,ally)==0x83aeff,"chip fields keep their kind colour");
            }
            Check(NativeFieldRules.Color("black-hole","singularity",false,true)==0xab67ff&&NativeFieldRules.Color("bomb","orbital-mark",false,false)==0xffa141&&NativeFieldRules.Color("fire","meteor-strike",false,true)==0xff672d,"no viewer: ability fields keep the kind colour");
            Check(NativeFieldRules.IsAlly(true,false,false,false)&&NativeFieldRules.IsAlly(false,true,false,false)&&NativeFieldRules.IsAlly(false,false,true,true)&&!NativeFieldRules.IsAlly(false,false,false,true)&&!NativeFieldRules.IsAlly(false,false,true,false),"self, PvE and same-team (teams mode only) are allies");
            foreach(var (kind,fx) in new[]{("bomb","orbital-mark"),("bomb","meteor-strike"),("fire","meteor-strike"),("bomb","geyser-wall"),("black-hole","singularity")})
                Check(NativeFieldRules.Color(kind,fx,true,true)==NativeFieldRules.Ally&&NativeFieldRules.Color(kind,fx,true,false)==NativeFieldRules.Enemy,kind+":"+fx+" tints ally cyan / enemy red");
            Check(NativeFieldRules.TintAbilityFields,"the design tint is on (owner can switch it off in NativeFieldRules)");
        }
        private static void Formulas()
        {
            var cubes=new NativeFieldCube[NativeFieldRules.MaxCubes];
            double Orbit(double t){var f=new NativeBlastField{Kind="implosion",Fx="implosion-bomb",X=0,Y=.95,Z=0,Radius=4,Born=0,Until=.9};int n=NativeFieldRules.Cubes(f,0,t,cubes);double r=0;for(int i=n-6;i<n;i++)r+=Math.Sqrt(cubes[i].X*cubes[i].X+cubes[i].Z*cubes[i].Z)/6;return r;}
            Near(Orbit(.2),.75-.5*(.2/.9),"implosion orbit at age .2");Near(Orbit(4.5),.25,"implosion orbit at the end of the fuse");Check(Orbit(.2)>Orbit(4.5),"implosion orbit tightens");
            var hole=new NativeBlastField{Kind="black-hole",X=0,Y=.95,Z=0,Radius=5,Born=0,Until=3.5};
            foreach(double t in new[]{0,.7,2.9}){int n=NativeFieldRules.Cubes(hole,0,t,cubes);Check(n==12,"black hole cubes");for(int i=0;i<10;i++){var c=cubes[2+i];Near(Math.Sqrt(c.X*c.X+c.Z*c.Z),i%2==0?.95:1.25,"accretion radius "+i);Near(Math.Atan2(c.Z,c.X),Math.Atan2(Math.Sin(NativeFieldRules.DiscAngle(i,t)),Math.Cos(NativeFieldRules.DiscAngle(i,t))),"accretion angle "+i);}}
            // The hidden inner cube really is inside the core at every rotation: half-diagonal .26 < inscribed .31.
            Check(.15*Math.Sqrt(3)<.31,"black-hole inner cube stays hidden");
            Near(NativeFieldRules.BombEdge(0,0),.38,"bomb body edge");Near(NativeFieldRules.BombEdge(Math.PI/16,0),.38*1.12,"bomb pulse");
            Check(NativeFieldRules.FuseVisible(0,0,2)&&!NativeFieldRules.FuseVisible(Math.PI/4,0,2)&&NativeFieldRules.FuseVisible(Math.PI/4,.5,2),"fuse blink speeds up with the fuse");
            Check(!NativeFieldRules.FuseVisible(0,1,Math.PI/120),"fuse flickers off in the last half second");
            Near(NativeFieldRules.GlowY(0),.1,"glow on a floor point");Near(NativeFieldRules.GlowY(.85),.18,"glow under a body centre");
            Check(NativeFieldRules.ScorchDecal(0,10,1,5,out _)&&!NativeFieldRules.ScorchDecal(0,10,1,9.99,out _),"scorch decal hides in its last fade");
            // The Siren glow ShaderMaterial skips the sRGB encode (fxref-c9 render: ring pixel 207,10,19 = linear ff2f44 x flicker
            // x .9 over the floor), so the warn ring shows deep red, not the hex.
            NativeFieldRules.SirenDisplay(NativeFieldRules.Warn,1,out double wr,out double wg,out double wb);NativeBlastSim.Linear(NativeFieldRules.Warn,out double lr,out double lg,out double lb);
            Check(wr==lr&&wg==lg&&wb==lb&&wg<.03&&wb<.06,"Siren warn ring shows its linear colour");NativeFieldRules.SirenDisplay(NativeFieldRules.WarnDeep,.5,out wr,out _,out _);NativeBlastSim.Linear(NativeFieldRules.WarnDeep,out lr,out _,out _);Near(wr,lr*.5,"Siren glow intensity scales the shown colour");
            Check(NativeFieldRules.EruptionWarn(0,1,2,.99,out _,out double fill)&&Math.Abs(fill-1.98)<1e-9&&!NativeFieldRules.EruptionWarn(0,1,2,1,out _,out _),"eruption warning ends at the burst");
            var into=new float[NativeFieldRules.PreviewCap*4];int used=0;for(int i=0;i<5;i++)used=NativeFieldRules.PreviewMotes(i*10,0,0,7,false,1,into,used);Check(used==NativeFieldRules.PreviewCap,"preview motes are capped at 120");
            Check(NativeFieldRules.Previews("geyser-wall",out bool ground)&&ground&&NativeFieldRules.Previews("frag-grenade",out ground)&&!ground&&!NativeFieldRules.Previews("flame-lance",out _),"preview abilities");
        }
    }
}
#endif
