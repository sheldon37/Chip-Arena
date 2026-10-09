#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Projectile visuals (C10): NativeShotScene, NativeShotRules, NativeBurstPool and NativeTrace against the pinned source's
    // drawn frames (tools/export_shot_fixtures.mjs runs the renderer.ts shot loop and abilityShot verbatim with
    // chip-effects.ts, burst-pool.ts, event-vfx.ts, projectile-trails.ts and projectile-glow.ts), plus the three.js
    // geometries, the 60 Hz emitter step, pool growth, the trail colour lock, the frag blink and the fallbacks.
    internal static class ShotChecks
    {
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Projectile visual check failed: "+what);}
        private static void Near(double a,double b,double tolerance,string what){if(!(Math.Abs(a-b)<=tolerance))throw new Exception("Projectile visual parity: "+what+": "+a+" != "+b);}
        private sealed class Spec
        {
            public int Id;public NativeShotKind Kind;public string Spec_,Ability,Melee,Arcade;public double Radius,Gravity,Fuse=double.NaN,RestAt=double.NaN;public bool Siren;
            public Dictionary<string,int> Mods=new Dictionary<string,int>();public bool Fire,Poison;public double[] Pos,Vel;
        }
        private static double[] Doubles(JsonElement e){var o=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())o[i++]=v.GetDouble();return o;}
        private static NativeShotShape Shape(string name)=>name switch
        {
            "box"=>NativeShotShape.Box,"fire-cone"=>NativeShotShape.FireCone,"water-sphere"=>NativeShotShape.WaterSphere,"dark-ico"=>NativeShotShape.DarkIco,"magic-ico"=>NativeShotShape.MagicIco,
            "arcade-sphere"=>NativeShotShape.ArcadeSphere,"frag"=>NativeShotShape.Frag,"rocket"=>NativeShotShape.Rocket,"orbital-mark"=>NativeShotShape.OrbitalMark,"flame-lance"=>NativeShotShape.FlameLance,
            "tidal-wave"=>NativeShotShape.TidalWave,"ball-lightning"=>NativeShotShape.BallLightning,"shackles"=>NativeShotShape.Shackles,"fallback"=>NativeShotShape.Fallback,
            "ring"=>NativeShotShape.Ring,"module-cone"=>NativeShotShape.ModuleCone,"orb"=>NativeShotShape.Orb,_=>throw new Exception("Unknown fixture geometry "+name)
        };
        private static NativeShotShape Part(string name)=>name switch{"ring"=>NativeShotShape.Ring,"cone"=>NativeShotShape.ModuleCone,"orb"=>NativeShotShape.Orb,"cube"=>NativeShotShape.Box,_=>throw new Exception("Unknown part "+name)};
        public static void Run(string root,ConversionContent content)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/shot-parity.json")));var json=document.RootElement;
            int shapes=Geometries(json.GetProperty("geometries"));
            int cases=0,frames=0;foreach(var c in json.GetProperty("cases").EnumerateArray()){frames+=Replay(json,c,content);cases++;}
            Emitter();Pool();Trace();Blink();Fallbacks(content);Nearest();Modules(content);
            Console.WriteLine("PASS "+cases+" source projectile runs ("+frames+" captured frames: 3 guns, 5 magic schools, bowling/velocity/element chips, 2 hadoukens, sword beam, 7 ability shots and the fallback, every chip module, thrown dagger and shield, Siren exclusion, a volley, reduced and off effects) against renderer.ts/chip-effects.ts/burst-pool.ts/event-vfx.ts/projectile-trails.ts/projectile-glow.ts, "+shapes+" three.js geometries, and emitter-step, pool-growth, trail colour lock, frag blink, fallback, light and module checks");
        }
        // NativeShotGeometry against the three r180 geometries the source builds (positions as float32, index order).
        private static int Geometries(JsonElement table)
        {
            int n=0;
            foreach(var g in table.EnumerateObject())
            {
                var mesh=NativeShotShapes.Mesh(Shape(g.Name));var p=Doubles(g.Value.GetProperty("positions"));
                Check(mesh.Positions.Length==p.Length,g.Name+" vertex count "+mesh.Positions.Length/3+" vs "+p.Length/3);
                for(int i=0;i<p.Length;i++)Near(mesh.Positions[i],p[i],2e-7,g.Name+" position "+i);
                var idx=g.Value.GetProperty("indices");
                if(idx.ValueKind==JsonValueKind.Null){for(int i=0;i<mesh.Indices.Length;i++)Check(mesh.Indices[i]==i,g.Name+" non-indexed order");Check(mesh.Indices.Length==p.Length/3,g.Name+" non-indexed count");}
                else{int i=0;foreach(var v in idx.EnumerateArray()){Check(i<mesh.Indices.Length&&mesh.Indices[i]==v.GetInt32(),g.Name+" index "+i);i++;}Check(i==mesh.Indices.Length,g.Name+" index count");}
                n++;
            }
            return n;
        }
        private static List<Spec> Shots(JsonElement c,ConversionContent content)
        {
            var list=new List<Spec>();
            foreach(var s in c.GetProperty("shots").EnumerateArray())
            {
                var spec=new Spec{Id=s.GetProperty("id").GetInt32(),Spec_=s.GetProperty("spec").GetString(),Radius=s.GetProperty("radius").GetDouble(),Gravity=s.GetProperty("gravity").GetDouble(),Siren=s.GetProperty("siren").GetBoolean(),Pos=Doubles(s.GetProperty("pos")),Vel=Doubles(s.GetProperty("vel"))};
                string kind=s.GetProperty("kind").GetString();spec.Kind=kind=="ability"?NativeShotKind.Ability:kind=="melee"?NativeShotKind.Melee:NativeShotKind.Bullet;
                foreach(var m in s.GetProperty("mods").EnumerateObject())spec.Mods[m.Name]=m.Value.GetInt32();
                foreach(var e in s.GetProperty("elements").EnumerateArray()){if(e.GetString()=="fire")spec.Fire=true;else if(e.GetString()=="poison")spec.Poison=true;}
                var ability=s.GetProperty("ability");if(ability.ValueKind==JsonValueKind.Object){spec.Ability=ability.GetProperty("id").GetString();if(ability.TryGetProperty("fuse",out var fuse))spec.Fuse=fuse.GetDouble();}
                if(s.GetProperty("melee").ValueKind==JsonValueKind.String)spec.Melee=s.GetProperty("melee").GetString();
                if(s.GetProperty("arcade").ValueKind==JsonValueKind.String)spec.Arcade=s.GetProperty("arcade").GetString();
                if(s.GetProperty("restAt").ValueKind==JsonValueKind.Number)spec.RestAt=s.GetProperty("restAt").GetDouble();
                list.Add(spec);
            }
            return list;
        }
        // What the presentation would know about a scripted projectile (the runtime builds the same view from NativeBullet).
        private static NativeShotView View(Spec s,double[] p,double[] v,double simTime,ConversionContent content)
        {
            var w=Array.Find(content.weapons,x=>x.id==s.Spec_);
            var view=new NativeShotView{Kind=s.Kind,Id=s.Id,X=p[0],Y=p[1],Z=p[2],VX=v[0],VY=v[1],VZ=v[2],Radius=s.Radius,Spec=s.Spec_,Class=w?.@class,WeaponColor=w!=null?(int)w.color:NativeShotRules.White,
                Ability=s.Kind==NativeShotKind.Ability?s.Ability:null,FuseLeft=double.IsNaN(s.Fuse)?1e9:s.Fuse-simTime,Fire=s.Fire,Poison=s.Poison,Arcade=s.Arcade=="red"?1:s.Arcade=="blue"?2:0,
                SwordBeam=s.Mods.TryGetValue("sword-beam",out int beam)&&beam>0,Siren=s.Siren,Shield=s.Melee=="shield",VelocityChips=s.Mods.TryGetValue("velocity",out int velocity)?velocity:0};
            view.Bowling=view.Class=="ranged"&&s.Mods.TryGetValue("bowling-ball",out int bowling)&&bowling>0;
            view.Modules=NativeShotRules.ModulesOf(content,s.Mods);return view;
        }
        private static double[] Matrix(in NativeShotAffine m)=>new double[]{m.M00,m.M10,m.M20,0,m.M01,m.M11,m.M21,0,m.M02,m.M12,m.M22,0,m.M03,m.M13,m.M23,1};
        private static void SameMatrix(double[] port,JsonElement source,double scale,string what)
        {
            int i=0;foreach(var e in source.EnumerateArray()){double expected=e.GetDouble()*(i<12&&i%4!=3?scale:1);Near(port[i],expected,2e-5+Math.Abs(expected)*2e-6,what+" matrix "+i);i++;}
        }
        private static void SameColor(float r,float g,float b,JsonElement c,string what){var e=Doubles(c);Near(r,e[0],2e-6,what+" red");Near(g,e[1],2e-6,what+" green");Near(b,e[2],2e-6,what+" blue");}
        private static void SameHex(float r,float g,float b,int hex,string what){NativeShotRules.Linear(hex,false,out float er,out float eg,out float eb);Near(r,er,2e-6,what+" red");Near(g,eg,2e-6,what+" green");Near(b,eb,2e-6,what+" blue");}
        private static int Replay(JsonElement json,JsonElement c,ConversionContent content)
        {
            string name=c.GetProperty("name").GetString();double dt=json.GetProperty("dt").GetDouble(),simStart=json.GetProperty("simStart").GetDouble(),quality=c.GetProperty("quality").GetDouble();
            int count=json.GetProperty("frames").GetInt32();var cam=Doubles(json.GetProperty("camera").GetProperty("pos"));var target=Doubles(json.GetProperty("camera").GetProperty("target"));
            // three Matrix4.lookAt: right = normalize(up x (eye - target)), the trail fallback when a segment points at the camera.
            double zx=cam[0]-target[0],zy=cam[1]-target[1],zz=cam[2]-target[2],zl=Math.Sqrt(zx*zx+zy*zy+zz*zz);zx/=zl;zy/=zl;zz/=zl;double rx=zz,ry=0,rz=-zx,rl=Math.Sqrt(rx*rx+rz*rz);
            var eye=new V3((float)cam[0],(float)cam[1],(float)cam[2]);var fallback=new V3((float)(rx/rl),(float)(ry/rl),(float)(rz/rl));
            var shots=Shots(c,content);var rng=new NativeShotRandom((uint)json.GetProperty("seed").GetInt32());var scene=new NativeShotScene(rng.Next,rng.Next);
            var traces=new Dictionary<int,NativeTrace>();var pos=new List<double[]>();var vel=new List<double[]>();var gravity=new List<double>();
            foreach(var s in shots){pos.Add((double[])s.Pos.Clone());vel.Add((double[])s.Vel.Clone());gravity.Add(s.Gravity);}
            var captures=new Dictionary<int,JsonElement>();foreach(var f in c.GetProperty("captures").EnumerateArray())captures[f.GetProperty("frame").GetInt32()]=f;
            double vt=json.GetProperty("visualStart").GetDouble();var views=new NativeShotView[shots.Count];var first=new int[shots.Count];var last=new int[shots.Count];int checkedFrames=0;
            for(int k=1;k<=count;k++)
            {
                // The exporter's kinematics, in the same double operations.
                double t=k*dt,simTime=simStart+k*dt;vt+=dt;
                for(int i=0;i<shots.Count;i++)
                {
                    if(!double.IsNaN(shots[i].RestAt)&&t>shots[i].RestAt){vel[i]=new double[]{0,0,0};gravity[i]=0;}
                    var p=pos[i];var v=vel[i];pos[i]=new[]{p[0]+v[0]*dt,p[1]+v[1]*dt,p[2]+v[2]*dt};vel[i]=new[]{v[0],v[1]-gravity[i]*dt,v[2]};
                }
                scene.Begin(vt,simTime,dt,quality);
                for(int i=0;i<shots.Count;i++)
                {
                    views[i]=View(shots[i],pos[i],vel[i],simTime,content);
                    if(scene.Knows(views[i].Kind,views[i].Id))views[i].Modules=default;
                    first[i]=scene.Draws.Count;if(!views[i].Siren)scene.Shot(views[i]);last[i]=scene.Draws.Count;
                    if(NativeShotRules.HasTrail(views[i])&&(traces.ContainsKey(shots[i].Id)||traces.Count<NativeShotRules.TrailLimit))
                    {
                        if(!traces.TryGetValue(shots[i].Id,out var trace))traces[shots[i].Id]=trace=new NativeTrace();
                        trace.Sample(new V3((float)pos[i][0],(float)pos[i][1],(float)pos[i][2]),(float)vt,NativeShotRules.TrailColor(views[i],vt),(float)NativeShotRules.TrailWidth(views[i]));
                    }
                }
                scene.End(dt);
                if(!captures.TryGetValue(k,out var f))continue;
                Compare(name+" frame "+k,f,shots,views,first,last,scene,traces,(float)vt,vt,quality,eye,fallback);checkedFrames++;
            }
            var lastInputs=c.GetProperty("last");int n=0;foreach(var s in lastInputs.EnumerateArray()){var p=Doubles(s.GetProperty("pos"));for(int j=0;j<3;j++)Check(p[j]==pos[n][j],name+" kinematics");n++;}
            return checkedFrames;
        }
        private static void Compare(string at,JsonElement f,List<Spec> shots,NativeShotView[] views,int[] first,int[] last,NativeShotScene scene,Dictionary<int,NativeTrace> traces,float now,double clock,double quality,V3 eye,V3 fallback)
        {
            var index=new Dictionary<int,int>();for(int i=0;i<shots.Count;i++)index[shots[i].Id]=i;var draws=scene.Draws;
            // Models: the source mesh per projectile (bullets and ability shots; no thrown melee weapons, no Siren shots).
            int models=0;
            foreach(var m in f.GetProperty("models").EnumerateArray())
            {
                int i=index[m.GetProperty("id").GetInt32()];var v=views[i];string what=at+" model "+shots[i].Id;Check(last[i]>first[i],what+" drawn");var d=draws[first[i]];models++;
                if(v.Kind==NativeShotKind.Ability)Check(d.Shape==NativeShotRules.AbilityShape(v.Ability,out _),what+" ability shape");else Check(d.Shape==NativeShotRules.PrimaryShape(v),what+" primary shape");
                // Primary meshes bake the first radius into the source geometry; the port scales a unit-radius mesh instead.
                double scale=v.Kind==NativeShotKind.Bullet&&d.Shape!=NativeShotShape.Box?shots[i].Radius:1;
                SameMatrix(Matrix(d.M),m.GetProperty("m"),scale,what);SameColor(d.R,d.G,d.B,m.GetProperty("c"),what+" colour");
                Check(Math.Abs(d.A-m.GetProperty("a").GetDouble())<1e-6&&(d.A<.999f)==m.GetProperty("transparent").GetBoolean(),what+" opacity");
                if(m.TryGetProperty("world",out var world))
                {
                    var mesh=NativeShotShapes.Mesh(d.Shape);var w=Doubles(world);Check(w.Length==mesh.Positions.Length,what+" world vertex count");
                    for(int k=0;k<mesh.Positions.Length;k+=3){d.M.Apply(mesh.Positions[k],mesh.Positions[k+1],mesh.Positions[k+2],out float x,out float y,out float z);Near(x,w[k],1.5e-4,what+" world x "+k/3);Near(y,w[k+1],1.5e-4,what+" world y "+k/3);Near(z,w[k+2],1.5e-4,what+" world z "+k/3);}
                }
            }
            int expectedModels=0;for(int i=0;i<shots.Count;i++)if(!views[i].Siren&&views[i].Kind!=NativeShotKind.Melee)expectedModels++;Check(models==expectedModels,at+" model count "+models+" vs "+expectedModels);
            // Chip modules: the group's parts in build order, each in the group frame.
            var withParts=new HashSet<int>();
            foreach(var g in f.GetProperty("modules").EnumerateArray())
            {
                int i=index[g.GetProperty("id").GetInt32()];withParts.Add(i);int start=first[i]+(views[i].Kind==NativeShotKind.Melee?0:1),n=0;
                foreach(var part in g.GetProperty("parts").EnumerateArray())
                {
                    string what=at+" module "+shots[i].Id+" part "+n;Check(start+n<last[i],what+" drawn");var d=draws[start+n];
                    Check(d.Shape==Part(part.GetProperty("shape").GetString()),what+" shape");SameHex(d.R,d.G,d.B,part.GetProperty("hex").GetInt32(),what+" colour");SameMatrix(Matrix(d.M),part.GetProperty("m"),1,what);n++;
                }
                Check(start+n==last[i],at+" module part count "+shots[i].Id);
            }
            for(int i=0;i<shots.Count;i++)if(!withParts.Contains(i))Check(last[i]-first[i]==(views[i].Siren||views[i].Kind==NativeShotKind.Melee?0:1),at+" no parts for "+shots[i].Id);
            // Sparks (BurstPool) and transient beams (EventVfx.beam), in spawn order.
            var pool=scene.Bursts;var sparks=f.GetProperty("sparks");Check(pool.Count==sparks.GetArrayLength(),at+" spark count "+pool.Count+" vs "+sparks.GetArrayLength());
            int si=0;foreach(var s in sparks.EnumerateArray())
            {
                var e=Doubles(s);int k=si*NativeBurstPool.SparkStride;var o=pool.Sparks;
                for(int j=0;j<3;j++)Near(o[k+j],e[j],2e-6,at+" spark "+si+" position");Near(o[k+6],e[3],2e-6,at+" spark "+si+" edge");for(int j=0;j<3;j++)Near(o[k+8+j],e[4+j],2e-6,at+" spark "+si+" colour");si++;
            }
            var beams=f.GetProperty("beams");Check(pool.Beams==beams.GetArrayLength(),at+" beam count "+pool.Beams+" vs "+beams.GetArrayLength());
            int bi=0;foreach(var b in beams.EnumerateArray())
            {
                int k=bi*NativeBurstPool.BeamStride;var d=pool.BeamData;var m=NativeShotAffine.Compose(d[k],d[k+1],d[k+2],d[k+9],d[k+10],d[k+11],d[k+12],d[k+6],d[k+7],d[k+8]);
                SameMatrix(Matrix(m),b.GetProperty("m"),1,at+" beam "+bi);var c=Doubles(b.GetProperty("c"));var e=Doubles(b.GetProperty("e"));
                for(int j=0;j<3;j++){Near(d[k+14+j],c[j],2e-6,at+" beam colour");Near(d[k+14+j]*.7,e[j],2e-6,at+" beam emission");}Near(d[k+13],b.GetProperty("life").GetDouble(),2e-6,at+" beam life");bi++;
            }
            // Trails: colour (locked at the first sample), width, points, fade and the drawn ribbon for the source camera.
            var seen=new HashSet<int>();
            foreach(var t in f.GetProperty("trails").EnumerateArray())
            {
                int id=t.GetProperty("id").GetInt32();string what=at+" trail "+id;Check(traces.TryGetValue(id,out var trace),what+" exists");seen.Add(id);
                Check(trace.Color==t.GetProperty("hex").GetInt32(),what+" colour "+trace.Color.ToString("x6")+" vs "+t.GetProperty("hex").GetInt32().ToString("x6"));
                Near(trace.Width,t.GetProperty("width").GetDouble(),1e-6,what+" width");Near(trace.Seen,t.GetProperty("seen").GetDouble(),2e-5,what+" seen");
                var points=t.GetProperty("points");Check(trace.Points.Count==points.GetArrayLength(),what+" point count "+trace.Points.Count+" vs "+points.GetArrayLength());
                int pi=0;foreach(var p in points.EnumerateArray()){var e=Doubles(p);var q=trace.Points[pi];Near(q.Position.x,e[0],2e-5,what+" point x");Near(q.Position.y,e[1],2e-5,what+" point y");Near(q.Position.z,e[2],2e-5,what+" point z");Near(q.At,e[3],2e-5,what+" point time");pi++;}
                Near(.85f*NativeGlowRules.TrailFade(now-trace.Seen),t.GetProperty("opacity").GetDouble(),2e-5,what+" opacity");
                if(t.TryGetProperty("quads",out var quads))
                {
                    var qv=Doubles(quads);var fades=Doubles(t.GetProperty("fades"));Check(qv.Length==(trace.Points.Count-1)*12,what+" ribbon length");
                    for(int i=1;i<trace.Points.Count;i++)
                    {
                        trace.Segment(i,eye,fallback,now,out var a0,out var a1,out var b0,out var b1,out float alphaA,out float alphaB);int o=(i-1)*12;V3[] corners={a0,a1,b0,b1};
                        for(int j=0;j<4;j++){Near(corners[j].x,qv[o+j*3],1e-4,what+" ribbon x");Near(corners[j].y,qv[o+j*3+1],1e-4,what+" ribbon y");Near(corners[j].z,qv[o+j*3+2],1e-4,what+" ribbon z");}
                        float tail=.85f*NativeGlowRules.TrailFade(now-trace.Seen);
                        Near(alphaA,fades[(i-1)*4]*tail,3e-5,what+" fade a");Near(alphaB,fades[(i-1)*4+2]*tail,3e-5,what+" fade b");
                        Near(fades[(i-1)*4],fades[(i-1)*4+1],1e-9,what+" fade pair");
                    }
                }
            }
            foreach(var pair in traces)if(!pair.Value.Expired(now))Check(seen.Contains(pair.Key),at+" extra trail "+pair.Key);
            // Halos and spill lights (projectile-glow.ts): every projectile but the Siren's, nearest three (one below .5) lit.
            var halos=f.GetProperty("halos");var lit=new List<int>();var distances=new List<double>();
            for(int i=0;i<shots.Count;i++)if(!views[i].Siren&&quality>0){lit.Add(i);double dx=views[i].X-eye.x,dy=views[i].Y-eye.y,dz=views[i].Z-eye.z;distances.Add(dx*dx+dy*dy+dz*dz);}
            Check(halos.GetArrayLength()==lit.Count,at+" halo count");
            foreach(var h in halos.EnumerateArray())
            {
                int i=index[h.GetProperty("id").GetInt32()];string what=at+" halo "+shots[i].Id;Check(!views[i].Siren,what+" not Siren");var p=Doubles(h.GetProperty("p"));
                Near(views[i].X,p[0],2e-6,what+" x");Near(views[i].Y,p[1],2e-6,what+" y");Near(views[i].Z,p[2],2e-6,what+" z");
                Near(NativeGlowRules.HaloSize((float)views[i].Radius,(float)quality),h.GetProperty("size").GetDouble(),2e-6,what+" size");
                Check(NativeShotRules.HaloColor(views[i],clock)==h.GetProperty("hex").GetInt32(),what+" colour "+NativeShotRules.HaloColor(views[i],clock).ToString("x6")+" vs "+h.GetProperty("hex").GetInt32().ToString("x6"));
            }
            var order=new int[3];int lights=NativeShotRules.Nearest(distances,quality<.5?1:3,order);var expected=f.GetProperty("lights");Check(expected.GetArrayLength()==lights,at+" light count "+lights+" vs "+expected.GetArrayLength());
            int li=0;foreach(var l in expected.EnumerateArray())
            {
                var e=Doubles(l);var v=views[lit[order[li]]];string what=at+" light "+li;NativeShotRules.Linear(NativeShotRules.HaloColor(v,clock),false,out float r,out float g,out float b);
                Near(v.X,e[0],2e-6,what+" x");Near(v.Y,e[1],2e-6,what+" y");Near(v.Z,e[2],2e-6,what+" z");Near(r,e[3],2e-6,what+" red");Near(g,e[4],2e-6,what+" green");Near(b,e[5],2e-6,what+" blue");
                Near(NativeShotRules.LightIntensity(v),e[6],1e-9,what+" intensity");Near(NativeShotRules.LightRange(v),e[7],1e-9,what+" distance");li++;
            }
        }
        // The 60 Hz emitter step: every frame at 60 Hz and 30 Hz, about 60 a second at 144 Hz and 240 Hz.
        private static void Emitter()
        {
            foreach(var (hz,expected) in new[]{(30,30),(60,60),(144,60),(240,60)})
            {
                double clock=0;int emits=0;for(int i=0;i<hz;i++)if(NativeShotRules.Emit(ref clock,1.0/hz))emits++;
                Check(Math.Abs(emits-expected)<=1,"emitter at "+hz+" Hz: "+emits+" emits a second");
            }
            double held=0;Check(!NativeShotRules.Emit(ref held,0),"a paused frame does not emit");
        }
        // BurstPool: starts at 180 sparks, doubles when full, keeps spawn order, clears back to 180; quality scales the count.
        private static void Pool()
        {
            var pool=new NativeBurstPool(new NativeShotRandom(3).Next);pool.Spawn(0,1,0,0xffffff,400,1,1);Check(pool.Count==400&&pool.Capacity==720,"pool doubles from 180 to 720");
            pool.Clear();Check(pool.Count==0&&pool.Capacity==NativeBurstPool.InitialCapacity,"pool clear restores 180");
            pool.Spawn(0,1,0,0xffffff,3,1,.3);Check(pool.Count==1,"reduced effects: 3 sparks at .3 spawn one (i < count*quality)");pool.Spawn(0,1,0,0xffffff,8,1,0);Check(pool.Count==1,"effects off spawns nothing");
            pool.Beam(0,0,0,0,0,1,0xffffff,0);Check(pool.Beams==0,"no beams at quality 0");pool.Beam(0,0,0,0,0,1,0xffffff,.3);Check(pool.Beams==1,"beams at reduced quality");
            pool.Update(.1);pool.Update(.1);Check(pool.Count==1&&pool.Beams==0,"beams live .09 s");
            // Sparks rise or stay level at spawn (vertical speed rand*power) and shrink by max(.93, 1-1.2dt).
            var probe=new NativeBurstPool(new NativeShotRandom(9).Next);probe.Spawn(0,0,0,0xffffff,50,2,1);for(int i=0;i<50;i++)Check(probe.Sparks[i*NativeBurstPool.SparkStride+4]>=0,"spark vertical velocity is never negative");
            double edge=probe.Sparks[6];probe.Update(1.0/60);Near(probe.Sparks[6],edge*.98,1e-12,"spark shrink at 60 Hz");
        }
        // NativeTrace keeps the colour of its first sample (projectile-trails.ts sets the material colour once) and resets on rewind.
        private static void Trace()
        {
            var trace=new NativeTrace();trace.Sample(new V3(0,0,0),1,0xff822f,1.45f);trace.Sample(new V3(1,0,0),1.05f,0x9de34d,1.45f);Check(trace.Color==0xff822f&&trace.Points.Count==2,"trail colour locks at the first sample");
            trace.Sample(new V3(2,0,0),.5f,0x9de34d,1);Check(trace.Color==0x9de34d&&trace.Points.Count==1,"a rewound trail restarts with the new colour");
            var still=new NativeTrace();for(int i=0;i<20;i++)still.Sample(new V3(i*.2f,0,0),i*.01f,1);Check(still.Points.Count==12,"at most 12 points");
        }
        // Frag fuse blink: red for ~40.3% of each cycle, 10 + 30/(left+.25) rad/s, none at 3 s or more or with no fuse.
        private static void Blink()
        {
            int red=0,n=100000;double left=1;double w=10+30/(left+.25);for(int i=0;i<n;i++)if(NativeShotRules.Blinks(left,i*(2*Math.PI/w)/n))red++;
            Near(red/(double)n,(Math.PI-2*Math.Asin(.3))/(2*Math.PI),2e-4,"frag blink duty");
            Check(!NativeShotRules.Blinks(3,Math.PI/2/(10+30/3.25)),"no blink at 3 s");Check(!NativeShotRules.Blinks(1e9,0.1),"no blink without a fuse");
            Near(10+30/(Math.Max(0,-.5)+.25),130,1e-9,"fastest blink at the fuse");
        }
        private static void Fallbacks(ConversionContent content)
        {
            // An ability shot with a missing or unknown definition is the white icosahedron with a white trail of width 1.
            var none=new NativeShotView{Kind=NativeShotKind.Ability,Id=1,Ability=null,WeaponColor=0xffd88d,Class="ranged",FuseLeft=1e9};
            Check(NativeShotRules.AbilityShape(null,out int color)==NativeShotShape.Fallback&&color==NativeShotRules.White,"null ability shape");
            Check(NativeShotRules.HasTrail(none)&&NativeShotRules.TrailColor(none,0)==NativeShotRules.White&&NativeShotRules.TrailWidth(none)==1,"null ability trail");
            var scene=new NativeShotScene();scene.Begin(0,0,1.0/60,1);scene.Shot(none);Check(scene.Draws.Count==1&&scene.Draws[0].Shape==NativeShotShape.Fallback,"null ability draws the fallback");
            var siren=new NativeShotView{Kind=NativeShotKind.Bullet,Id=2,Spec="siren",Siren=true,FuseLeft=1e9};scene.Shot(siren);Check(scene.Draws.Count==1&&!NativeShotRules.HasTrail(siren),"Siren shots draw nothing here and have no trail");
            var wave=new NativeShotView{Kind=NativeShotKind.Ability,Id=3,Ability="tidal-wave",FuseLeft=1e9};Check(!NativeShotRules.HasTrail(wave),"tidal wave has no trail");
            var melee=new NativeShotView{Kind=NativeShotKind.Melee,Id=4,Shield=true,WeaponColor=0x8cbee6};Check(NativeShotRules.HasTrail(melee)&&NativeShotRules.TrailWidth(melee)==3&&NativeShotRules.TrailColor(melee,0)==0x8cbee6,"thrown shield trail");
            scene.End(1.0/60);Check(scene.Live==1,"only drawn shots keep state");scene.Begin(0,0,1.0/60,1);scene.End(1.0/60);Check(scene.Live==0,"gone shots are forgotten");
            // Halo colour priority: arcade, then the active element (20 Hz), then the weapon colour.
            var bolt=new NativeShotView{Kind=NativeShotKind.Bullet,WeaponColor=0xff7d3e,Class="magic",Fire=true,Poison=true};
            Check(NativeShotRules.HaloColor(bolt,.01)==NativeShotRules.Fire&&NativeShotRules.HaloColor(bolt,.06)==NativeShotRules.Poison,"element halo switches every 50 ms");
            bolt.Arcade=1;Check(NativeShotRules.HaloColor(bolt,.06)==NativeShotRules.HaloRed&&NativeShotRules.LightIntensity(bolt)==6,"arcade halo and light");
            bolt.Arcade=0;bolt.Fire=bolt.Poison=false;Check(NativeShotRules.HaloColor(bolt,0)==0xff7d3e&&NativeShotRules.LightIntensity(bolt)==5&&NativeShotRules.LightRange(bolt)==5,"weapon halo and magic light");
            var gun=new NativeShotView{Kind=NativeShotKind.Bullet,WeaponColor=0xffd88d,Class="ranged",VelocityChips=2};Check(NativeShotRules.MeshColor(gun,0)==NativeShotRules.Ranged&&Math.Abs(NativeShotRules.TrailWidth(gun)-1.56)<1e-12,"ranged colour and Velocity Coil width");
        }
        private static void Nearest()
        {
            var order=new int[3];int n=NativeShotRules.Nearest(new double[]{9,1,4,1,16},3,order);Check(n==3&&order[0]==1&&order[1]==3&&order[2]==2,"nearest lights keep the source's stable order");
            Check(NativeShotRules.Nearest(new double[]{5},3,order)==1&&order[0]==0,"fewer projectiles than lights");
        }
        private static void Modules(ConversionContent content)
        {
            var none=NativeShotRules.ModulesOf(content,new Dictionary<string,int>());var parts=new List<NativeShotRules.Part>();NativeShotRules.Modules(none,false,false,parts);Check(parts.Count==0,"no chips, no parts");
            NativeShotRules.Modules(NativeShotRules.ModulesOf(content,new Dictionary<string,int>{{"ricochet",3},{"pinball",1}}),false,false,parts);int rings=0,kinetic=0;foreach(var p in parts){if(p.Shape==NativeShotShape.Ring&&p.Color==0xffcc49)rings++;if(p.Color==0xffaa64)kinetic++;}Check(rings==3&&kinetic==1,"at most three ricochet rings; Pinball's impulse adds the kinetic ring");
            NativeShotRules.Modules(none,true,false,parts);Check(parts.Count==5,"a fire shot always has five flame cones");
        }
    }
}
#endif
