#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Footstep surfaces (owner 2026-10-16: "walking continuously on one surface cycles through sounds that resemble different
    // terrains ... Verify with continuous walking and sprinting on one surface, then crossing an actual material boundary. Apply the
    // same check to player and zombie footsteps."; Core NativeFootSurface, NativeSurfaceMemory; Runtime NativeFootstepAudio,
    // NativeFootfallPresentation, NativeHulkChargeAudio; docs/ASSET-CREDITS.md, Footstep surfaces). On the shipped maps' colliders as
    // NativeArena builds them (a managed raycast: every block's box, the stair and ramp wedges with their treads' material, the
    // collisionOff blocks off), real gaits plant feet; every plant is probed at its planted foot, classified and remembered as the
    // runtime does, and the bank's own picker (NativeSoundBank.Next over the documented takes) chooses the take:
    //  - 60 s walking and sprinting on each surface, for every pilot gait (gun, magic, bow, the melee gait, no gait): one key, takes
    //    of its one series only, never the same take twice in a row;
    //  - crossing real boundaries (Ward Zero's walkway onto its metal deck, the factory floor onto a metal floor plate): the new key
    //    by the second plant on it and never back while on it;
    //  - every zombie kind walking and running the same: its own takes on the hard floors, the surface's on metal (a crawl drags);
    //  - the probe's geometry; the memory's rules; no allocation; the hooks.
    internal static class FootSurfaceChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Foot surface: "+what);count++;}
        private const float D2R=(float)(Math.PI/180);
        internal static Vector3 V(V3 v)=>new Vector3(v.x,v.y,v.z);

        public static void Run(string root)
        {
            count=0;var log=new List<string>();var maps=Maps(root);var takes=Takes(root,log);var bank=Bank(takes);
            Memory(log);Geometry(maps,log);
            Walks(maps,takes,bank,log);Crossings(maps,takes,bank,log);Zombies(maps,takes,bank,log);
            Allocation(log);Hooks(root,log);
            Console.WriteLine("PASS foot surfaces ("+count+" checks: "+string.Join("; ",log)+")");
        }

        // ---------- the maps and the documented takes ----------
        private sealed class Legacy{public ContentMap[] maps{get;set;}}
        internal static Dictionary<string,ContentMap> Maps(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};var all=new Dictionary<string,ContentMap>(StringComparer.Ordinal);
            foreach(var m in JsonSerializer.Deserialize<Legacy>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o).maps)all[m.id]=m;
            foreach(var m in JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o).maps)all[m.id]=m;
            return all;
        }
        /// <summary>Each footfall key's takes in clip order (docs/ASSET-CREDITS.md, Footstep surfaces): their recording series.</summary>
        internal static Dictionary<string,string[]> Takes(string root,List<string> log)
        {
            var keys=NativeSoundKeys.All.Select(k=>k.Key).Where(k=>k.StartsWith("move.step.",StringComparison.Ordinal)||k.StartsWith("pve.zombie.step.",StringComparison.Ordinal)).ToArray();
            var takes=new Dictionary<string,string[]>(StringComparer.Ordinal);string path=Path.Combine(root,"../../docs/ASSET-CREDITS.md");
            if(File.Exists(path))
                foreach(var line in File.ReadAllLines(path))
                {
                    var m=Regex.Match(line,@"^\| `((?:move\.step|pve\.zombie\.step)\.[a-z]+)` \|(.*)\|\s*$");if(!m.Success)continue;
                    var cells=m.Groups[2].Value.Split('|');Check(!takes.ContainsKey(m.Groups[1].Value),m.Groups[1].Value+" listed once");
                    takes[m.Groups[1].Value]=Regex.Matches(cells[cells.Length-1],@"`([^`]+\.wav)`").Select(x=>Series(x.Groups[1].Value)).ToArray();
                }
            if(takes.Count==0)
            {
                // Not in this tree: one series per key of the bank's counts, so the walks still check the key and the picker.
                foreach(var k in keys)takes[k]=Enumerable.Repeat(k+"-series",k=="move.step.concrete"?8:k=="pve.zombie.step.heavy"?5:k=="pve.zombie.step.crawl"?4:6).ToArray();
                log.Add("ASSET-CREDITS.md not in this tree: series checks use a one-series fixture");
            }
            foreach(var k in keys)Check(takes.ContainsKey(k)&&takes[k].Length>=4&&takes[k].Length<=8,k+" has 4-8 documented takes");
            return takes;
        }
        internal static string Series(string path)=>Regex.Replace(Regex.Replace(path.Substring(path.LastIndexOf('/')+1),@"(_mono|_stereo)?\.wav$",""),@"(_RR\d+|_[A-J]|_\d\d)$","");
        internal static NativeSoundBank Bank(Dictionary<string,string[]> takes)
        {
            var json=new StringBuilder("{\"schema\":1,\"events\":{");bool first=true;
            foreach(var p in takes){if(!first)json.Append(',');first=false;json.Append('"').Append(p.Key).Append("\":{\"clips\":[").Append(string.Join(",",Enumerable.Range(0,p.Value.Length).Select(i=>"\""+i+"\""))).Append("]}");}
            var bank=NativeSoundBank.Parse(json.Append("}}").ToString());Check(bank.Errors.Count==0&&bank.Count==takes.Count,"the takes' manifest parses ("+string.Join("; ",bank.Errors)+")");return bank;
        }

        // ---------- the colliders NativeArena builds, and Physics.Raycast down against them ----------
        internal sealed class World
        {
            private struct Box{public Vector3 C,H;public Quaternion Inv;public float R;public string M;}
            private struct Tri{public Vector3 A,B,C,N;public string M;}
            private readonly List<Box> boxes=new List<Box>();private readonly List<Tri> tris=new List<Tri>();
            // The boxes by 4 m cell of their footprint's bounding square (a query reads one cell).
            private const float Cell=4;private readonly Dictionary<long,List<int>> grid=new Dictionary<long,List<int>>();
            private static long CellOf(int x,int z)=>((long)x<<32)^(uint)z;
            private List<int> Near(float x,float z)=>grid.TryGetValue(CellOf((int)Math.Floor(x/Cell),(int)Math.Floor(z/Cell)),out var l)?l:none;private static readonly List<int> none=new List<int>();
            public readonly ContentMap Map;public readonly int Wedges;
            public World(ContentMap map)
            {
                Map=map;var off=new HashSet<string>(map.navHints?.collisionOff??Array.Empty<string>(),StringComparer.Ordinal);
                var flights=NativeCyberCityStairs.Flights(map).Concat(NativeMapRamps.Flights(map)).ToArray();
                foreach(var f in flights)foreach(var s in f.Steps)off.Add(s.id);
                foreach(var b in map.blocks)
                {
                    if(off.Contains(b.id))continue;var r=b.rotation;var q=r==null||r.x==0&&r.y==0&&r.z==0&&r.w==0?Quaternion.Identity:Quaternion.Normalize(new Quaternion(r.x,r.y,r.z,r.w));
                    var h=V(b.size)*.5f;var box=new Box{C=V(b.pos),H=h,Inv=Quaternion.Inverse(q),R=h.Length(),M=b.material};boxes.Add(box);
                    for(int cx=(int)Math.Floor((box.C.X-box.R)/Cell);cx<=(int)Math.Floor((box.C.X+box.R)/Cell);cx++)
                        for(int cz=(int)Math.Floor((box.C.Z-box.R)/Cell);cz<=(int)Math.Floor((box.C.Z+box.R)/Cell);cz++)
                        {if(!grid.TryGetValue(CellOf(cx,cz),out var l))grid[CellOf(cx,cz)]=l=new List<int>();l.Add(boxes.Count-1);}
                }
                foreach(var f in flights)
                {
                    // NativeArena.BuildCutExitCollision's closed wedge, its collider carrying the treads' physics material.
                    Wedges++;var low=V(f.Low);var high=V(f.High);var across=Vector3.Cross(V(f.Direction),Vector3.UnitY)*f.Width*.5f;
                    Vector3 Bottom(Vector3 p)=>new Vector3(p.X,f.Bottom,p.Z);var a=low-across;var b=high-across;var c=high+across;var d=low+across;
                    var v=new[]{a,b,c,d,Bottom(a),Bottom(b),Bottom(c),Bottom(d)};int[] t={0,3,2,0,2,1,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                    var mid=v.Aggregate(Vector3.Zero,(s,x)=>s+x)/8;
                    for(int i=0;i<t.Length;i+=3)
                    {
                        Vector3 A=v[t[i]],B=v[t[i+1]],C=v[t[i+2]],n=Vector3.Cross(B-A,C-A);if(n.LengthSquared()<1e-10f)continue;n=Vector3.Normalize(n);if(Vector3.Dot(n,(A+B+C)/3-mid)<0)n=-n;
                        tris.Add(new Tri{A=A,B=B,C=C,N=n,M=f.Steps[0].material});
                    }
                }
            }
            /// <summary>Physics.Raycast straight down from <paramref name="o"/>: the nearest box (never the one it starts inside) or
            /// wedge face (from outside: no back faces). <paramref name="wedgeNamed"/> false: a wedge names no material (the code
            /// before 2026-10-16 read only a NativePropBody's).</summary>
            public bool Down(Vector3 o,float length,out float distance,out string material,bool wedgeNamed=true)
            {
                distance=float.PositiveInfinity;material=null;
                foreach(int i in Near(o.X,o.Z))
                {
                    var b=boxes[i];
                    float dx=o.X-b.C.X,dz=o.Z-b.C.Z;if(dx*dx+dz*dz>b.R*b.R)continue;
                    var lo=Vector3.Transform(o-b.C,b.Inv);var ld=Vector3.Transform(-Vector3.UnitY,b.Inv);
                    if(Math.Abs(lo.X)<b.H.X&&Math.Abs(lo.Y)<b.H.Y&&Math.Abs(lo.Z)<b.H.Z)continue;
                    float t0=0,t1=length;bool hit=true;
                    for(int k=0;k<3&&hit;k++)
                    {
                        float p=k==0?lo.X:k==1?lo.Y:lo.Z,v=k==0?ld.X:k==1?ld.Y:ld.Z,h=k==0?b.H.X:k==1?b.H.Y:b.H.Z;
                        if(Math.Abs(v)<1e-7f){if(p<-h||p>h)hit=false;continue;}
                        float ta=(-h-p)/v,tb=(h-p)/v;if(ta>tb){float s=ta;ta=tb;tb=s;}t0=Math.Max(t0,ta);t1=Math.Min(t1,tb);if(t0>t1)hit=false;
                    }
                    if(hit&&t0<distance){distance=t0;material=b.M;}
                }
                var dir=-Vector3.UnitY;
                foreach(var t in tris)
                {
                    if(Vector3.Dot(t.N,dir)>=0)continue;
                    Vector3 e1=t.B-t.A,e2=t.C-t.A,p=Vector3.Cross(dir,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-9f)continue;float inv=1/det;
                    var s=o-t.A;float u=Vector3.Dot(s,p)*inv;if(u<0||u>1)continue;var q=Vector3.Cross(s,e1);float w=Vector3.Dot(dir,q)*inv;if(w<0||u+w>1)continue;
                    float at=Vector3.Dot(e2,q)*inv;if(at>=0&&at<=length&&at<distance){distance=at;material=wedgeNamed?t.M:null;}
                }
                return !float.IsPositiveInfinity(distance);
            }
            /// <summary>Whether a point is inside a block (a walker's body may not be).</summary>
            public bool Solid(Vector3 o)
            {
                foreach(int i in Near(o.X,o.Z)){var b=boxes[i];float dx=o.X-b.C.X,dz=o.Z-b.C.Z;if(dx*dx+dz*dz>b.R*b.R)continue;var lo=Vector3.Transform(o-b.C,b.Inv);if(Math.Abs(lo.X)<b.H.X&&Math.Abs(lo.Y)<b.H.Y&&Math.Abs(lo.Z)<b.H.Z)return true;}
                return false;
            }
            /// <summary>The runtime's classification of a foot at (x, z) whose ground point is <paramref name="groundY"/>.</summary>
            public string Key(float x,float groundY,float z,float scale=1)
            {
                bool hit=Down(new Vector3(x,NativeFootSurface.StartY(groundY,scale),z),NativeFootSurface.Reach(scale),out _,out string material);
                return NativeFootSurface.Key(Map.id,hit,material);
            }
            /// <summary>Before 2026-10-16: under the drawn position, .4 m up, 1.2 m down, a block's own material only.</summary>
            public string OldKey(float x,float groundY,float z)=>NativeFootsteps.Surface(Map.id,Down(new Vector3(x,groundY+.4f,z),1.2f,out _,out string m,false)?m:null);
            /// <summary>The floor a body at (x, z) stands on near <paramref name="y"/> (its replicated feet: the top plus .005).</summary>
            public float Ground(float x,float y,float z)=>Down(new Vector3(x,y+.5f,z),1.2f,out float d,out _)?y+.5f-d+.005f:y;
        }

        // ---------- routes: straight, clear, at one height ----------
        internal struct Route{public string Map,Name,From,To;public Vector3 A,B;public float Split;}
        private static bool Clear(World w,Vector3 p)
        {
            foreach(float y in new[]{.5f,1.1f,1.6f})foreach(var o in new[]{Vector3.Zero,new Vector3(.42f,0,0),new Vector3(-.42f,0,0),new Vector3(0,0,.42f),new Vector3(0,0,-.42f)})
                if(w.Solid(p+o+new Vector3(0,y,0)))return false;
            return true;
        }
        /// <summary>The longest clear run (along x or z) at height <paramref name="top"/> where every .2 m stands on <paramref name="key"/>.</summary>
        internal static Route Along(World w,string key,float top,string name,float min=8)
        {
            var best=default(Route);float bestLength=0;var b=w.Map.bounds;
            float x0=b!=null&&b.maxX>b.minX?b.minX:-32,x1=b!=null&&b.maxX>b.minX?b.maxX:32,z0=b!=null&&b.maxZ>b.minZ?b.minZ:-28,z1=b!=null&&b.maxZ>b.minZ?b.maxZ:28;
            for(int axis=0;axis<2;axis++)
                for(float lane=(axis==0?z0:x0)+1;lane<=(axis==0?z1:x1)-1;lane+=1.3f)
                {
                    float start=float.NaN;float lo=axis==0?x0:z0,hi=axis==0?x1:z1;
                    for(float s=lo+.5f;s<=hi-.5f+1e-3f;s+=.2f)
                    {
                        var p=axis==0?new Vector3(s,top,lane):new Vector3(lane,top,s);float g=w.Ground(p.X,top,p.Z);
                        bool ok=Math.Abs(g-top-.005f)<.13f&&w.Key(p.X,g,p.Z)==key&&Clear(w,new Vector3(p.X,g,p.Z));
                        if(ok&&float.IsNaN(start))start=s;
                        if((!ok||s+.2f>hi-.5f)&&!float.IsNaN(start))
                        {
                            float end=ok?s:s-.2f,length=end-start;
                            if(length>bestLength){bestLength=length;var a=axis==0?new Vector3(start,top,lane):new Vector3(lane,top,start);var e=axis==0?new Vector3(end,top,lane):new Vector3(lane,top,end);best=new Route{Map=w.Map.id,Name=name,From=key,To=key,A=a,B=e};}
                            start=float.NaN;
                        }
                    }
                }
            Check(bestLength>=min,name+": a clear "+min+" m run on "+key+" ("+bestLength.ToString("0.0")+" m)");
            return best;
        }
        /// <summary>A straight clear crossing from <paramref name="from"/> onto a block of <paramref name="material"/> whose top is
        /// within a step of <paramref name="top"/>: at least 3 m on each side.</summary>
        internal static bool Crossing(World w,string from,string material,float top,string name,out Route route)
        {
            route=default;
            string to=NativeFootsteps.Surface(w.Map.id,material);
            foreach(var b in w.Map.blocks.Where(b=>b.material==material&&Math.Abs(b.pos.y+b.size.y*.5f-top)<=.12f&&b.size.x*b.size.z>=2).OrderByDescending(b=>b.size.x*b.size.z))
            {
                var c=V(b.pos);float t=b.pos.y+b.size.y*.5f;
                foreach(var (dx,dz) in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                    foreach(float along in new[]{0f,-.3f,.3f})
                    {
                        var dir=new Vector3(dx,0,dz);var side=new Vector3(dz,0,dx);float half=dx!=0?b.size.x*.5f:b.size.z*.5f,width=dx!=0?b.size.z:b.size.x;
                        if(width<1.6f)continue;var edge=c+dir*half+side*(along*width);var A=edge+dir*4.5f;var B=edge-dir*Math.Min(4.5f,2*half-.6f);
                        if(Vector3.Distance(edge,B)<2.4f)continue;bool ok=true;string before=null;int n=0;
                        for(float s=0;s<=1.0001f&&ok;s+=.02f)
                        {
                            var p=Vector3.Lerp(A,B,s);float g=w.Ground(p.X,top,p.Z);ok=Math.Abs(g-top-.005f)<.36f&&Clear(w,new Vector3(p.X,g,p.Z));
                            string k=w.Key(p.X,g,p.Z);float into=Vector3.Dot(p-edge,-dir);
                            if(into<-.6f)ok&=k==from;else if(into>.6f)ok&=k==to;n++;before=k;
                        }
                        if(ok){route=new Route{Map=w.Map.id,Name=name,From=from,To=to,A=new Vector3(A.X,top,A.Z),B=new Vector3(B.X,top,B.Z),Split=Vector3.Distance(A,edge)};return true;}
                    }
            }
            return false;
        }

        // ---------- walkers: real gaits ----------
        internal sealed class Pilot
        {
            public readonly string Route;public readonly Actor A;private readonly GunMotion gun;private readonly MagicMotion magic;private readonly NativeBowMotion bow;private readonly MeleeMotion melee;private readonly MeleeView view;
            private readonly CapturedRig.Pose pose=new CapturedRig.Pose();private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();private readonly float[] channels=new float[13];
            public Pilot(string route,int id=7)
            {
                Route=route;string spec=route=="gun"?"pistol":route=="magic"?"fire":route=="bow"?NativeBowWire.Spec:route=="melee"?"emperor":"siren";
                A=new Actor{Id=id,SourceSpec=spec,Weapon=route=="gun"?WeaponId.Pistol:WeaponId.Sword,MagicVisual=route=="magic"?1:0,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),MeleeKit=new NativeMeleeKit(),LastShot=-100,Ammo=route=="gun"?GunRules.Get(WeaponId.Pistol).Magazine:0};
                if(route=="gun")gun=new GunMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};
                if(route=="magic")magic=new MagicMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};
                if(route=="bow")bow=new NativeBowMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};
                if(route=="melee"){melee=new MeleeMotion{JumpLayer=new NativeJumpMotion()};view=new MeleeView{Id=id.ToString(),Spec=spec};}
            }
            /// <summary>Poses the rig with its feet at <paramref name="feet"/> (the replicated position) facing <paramref name="yaw"/>
            /// degrees: the gait's planted feet (-1: no gait), and both foot bones in Unity world.</summary>
            public int Pose(Vector3 feet,float yaw,float t,float dt,out Vector3 left,out Vector3 right)
            {
                A.Position=new V3(feet.X,feet.Y,feet.Z);A.Yaw=yaw;A.AimYaw=yaw;left=right=feet;
                MeleeRig rig=gun!=null?gun.Rig:magic!=null?magic.Rig:bow!=null?bow.Rig:melee?.Rig;if(rig==null)return -1;
                rig.GroupPosition=new Vector3(-feet.X,feet.Y+.865f,feet.Z);rig.GroupYaw=-yaw*D2R;int c;
                if(gun!=null){gun.Pose(A,t,pose,false,juggle,out _);gun.Finish(pose,t);c=(gun.Planted(true)?1:0)|(gun.Planted(false)?2:0);}
                else if(magic!=null){magic.Pose(A,t,pose,channels,0,false,juggle,out _);magic.Finish(pose,t);c=(magic.Planted(true)?1:0)|(magic.Planted(false)?2:0);}
                else if(bow!=null){bow.Pose(A,t,pose,false,juggle,out _);bow.Finish(pose,t);c=(bow.Planted(true)?1:0)|(bow.Planted(false)?2:0);}
                else
                {
                    view.Vel=new Vector3(-A.Velocity.x,A.Velocity.y,A.Velocity.z);view.Grounded=true;view.Hp=100;view.Sprinting=A.Sprinting;view.Yaw=-yaw*D2R;
                    melee.FighterBase(view,t,dt);melee.FighterFinish(view,t,dt);melee.Rig.WritePose(pose);c=(melee.Gait.Planted(true)?1:0)|(melee.Gait.Planted(false)?2:0);
                }
                pose.World();var group=MeleeMath.AxisAngle(MeleeMath.Y,rig.GroupYaw);
                Vector3 W(int bone){var g=rig.GroupPosition+Vector3.Transform(pose.WorldPosition[bone],group);return new Vector3(-g.X,g.Y,g.Z);}
                left=W(CapturedRig.LFoot);right=W(CapturedRig.RFoot);return c;
            }
        }
        /// <summary>One plant as heard: where it was probed, what it saw, the key it played and the take the bank chose.</summary>
        internal struct Plant{public float T;public Vector3 Body,Foot;public string Seen,Key,Old,Series;public int Clip;}

        /// <summary>A pilot on <paramref name="r"/> at <paramref name="speed"/> m/s for <paramref name="seconds"/> (back and forth
        /// along it when <paramref name="pingPong"/>, else once from A to B): its plants through the tracker, probed at the
        /// planted foot (Leading, the runtime's pick) and remembered.</summary>
        internal static List<Plant> Walk(World w,Route r,Pilot p,float speed,bool sprint,float seconds,bool pingPong,NativeSoundBank bank,Dictionary<string,string[]> takes,int hz=60)
        {
            var plants=new List<Plant>();var tracker=new NativeStepTracker();var memory=new NativeSurfaceMemory();float dt=1f/hz;
            var dir=Vector3.Normalize(r.B-r.A);float length=Vector3.Distance(r.A,r.B),s=0,sign=1,top=r.A.Y;
            for(int f=0;f<seconds*hz;f++)
            {
                float t=2+f*dt;s+=sign*speed*dt;if(s>length){s=2*length-s;sign=pingPong?-1:0;}if(s<0){s=-s;sign=1;}
                if(!pingPong&&sign==0)break;
                var d=dir*sign;var at=r.A+dir*s;float g=w.Ground(at.X,top,at.Z);var feet=new Vector3(at.X,g,at.Z);
                p.A.Velocity=new V3(d.X*speed,0,d.Z*speed);p.A.Sprinting=sprint;float yaw=(float)(Math.Atan2(d.X,d.Z)/D2R);
                int c=p.Pose(feet,yaw,t,dt,out var lf,out var rf);
                int planted=tracker.Step(c,true,false,feet.X,feet.Z,speed,sprint,t,out bool land,out _);
                if(planted==0)continue;
                int foot=(planted&1)!=0?0:1;var at2=c<0?feet:NativeFootSurface.Leading(lf.X,lf.Z,rf.X,rf.Z,p.A.Velocity.x,p.A.Velocity.z,foot)==0?lf:rf;
                if(new Vector2(at2.X-feet.X,at2.Z-feet.Z).Length()>NativeFootSurface.FootReach)at2=feet;
                if(c>=0)Check(Vector3.Dot(at2-feet,d)>-.25f&&Math.Abs(at2.Y-feet.Y)<.3f,p.Route+": the probed foot is the leading, planted one ("+Vector3.Dot(at2-feet,d).ToString("0.00")+" m along, "+(at2.Y-feet.Y).ToString("0.00")+" m up)");
                string seen=w.Key(at2.X,g,at2.Z,p.A.RigScale),key=memory.Plant(seen,t);int clip=bank.Next(key);
                plants.Add(new Plant{T=t,Body=feet,Foot=at2,Seen=seen,Key=key,Old=w.OldKey(feet.X,g,feet.Z),Clip=clip,Series=takes[key][clip]});
            }
            return plants;
        }
        internal static readonly string[] PilotRoutes={"gun","magic","bow","melee","none"};

        // ---------- (i) 60 s on one surface ----------
        private static List<Route> Surfaces(Dictionary<string,ContentMap> maps)
        {
            var routes=new List<Route>();World W(string id)=>new World(maps[id]);
            routes.Add(Along(W("expedition-ward-zero"),NativeFootsteps.Concrete,0,"Ward Zero hall floor (tile slab)"));
            routes.Add(Along(W("expedition-city"),NativeFootsteps.Concrete,0,"the town's asphalt"));
            routes.Add(Along(W("factory"),NativeFootsteps.Metal,11.8f,"the factory's metal catwalk"));
            routes.Add(Along(W("expedition-ward-zero"),NativeFootsteps.Metal,6,"Ward Zero's metal deck",6));
            routes.Add(Along(W("sawmill"),NativeFootsteps.Wood,0,"the sawmill's boards"));
            routes.Add(Along(W("moonlit"),NativeFootsteps.Snow,0,"Moonlit's snow"));
            routes.Add(Along(W("outpost"),NativeFootsteps.Gravel,0,"the outpost's gravel"));
            routes.Add(Along(W("citadel"),NativeFootsteps.Stone,0,"the citadel's stone"));
            return routes;
        }
        private static void Walks(Dictionary<string,ContentMap> maps,Dictionary<string,string[]> takes,NativeSoundBank bank,List<string> log)
        {
            var worlds=maps.Values.ToDictionary(m=>m.id,m=>new World(m));int plants=0;var seen=new List<string>();
            foreach(var r in Surfaces(maps))
            {
                var w=worlds[r.Map];var line=new List<string>();
                foreach(var route in PilotRoutes)foreach(var (gait,speed,sprint) in new[]{("walk",3f,false),("sprint",9.18f,true)})
                {
                    var walk=Walk(w,r,new Pilot(route),speed,sprint,60,true,bank,takes);plants+=walk.Count;
                    Check(walk.Count>=60,r.Name+" "+route+" "+gait+": plants for 60 s ("+walk.Count+")");
                    var keys=walk.Select(x=>x.Key).Distinct().ToArray();Check(keys.Length==1&&keys[0]==r.From,r.Name+" "+route+" "+gait+": one key, "+r.From+" ("+string.Join(",",keys)+")");
                    var series=walk.Select(x=>x.Series).Distinct().ToArray();Check(series.Length==1,r.Name+" "+route+" "+gait+": takes of one series ("+string.Join(",",series)+")");
                    for(int i=1;i<walk.Count;i++)Check(walk[i].Clip!=walk[i-1].Clip,r.Name+" "+route+" "+gait+": never the same take twice in a row (plant "+i+")");
                    Check(walk.Select(x=>x.Clip).Distinct().Count()==takes[r.From].Length,r.Name+" "+route+" "+gait+": every take of the series plays");
                    if(route=="gun")line.Add(gait+" "+walk.Count+" plants");
                }
                seen.Add(r.Name+" "+r.From.Substring(10)+" ("+string.Join(", ",line)+")");
            }
            log.Add("60 s walking and sprinting, 5 pilot gaits, "+plants+" plants: one key and one series each, no take twice in a row: "+string.Join("; ",seen));
        }

        // ---------- (ii) crossing a real boundary ----------
        /// <summary>The maps' real material boundaries a pilot can walk straight across (the first two must exist: Ward Zero's
        /// tiled walkway and its metal deck, both ways).</summary>
        internal static List<Route> Boundaries(Dictionary<string,ContentMap> maps)
        {
            var list=new List<Route>();
            foreach(var (map,from,material,top,name) in new[]{("expedition-ward-zero",NativeFootsteps.Concrete,"metal",6f,"Ward Zero walkway onto its metal deck"),("expedition-ward-zero",NativeFootsteps.Metal,"stone",6f,"Ward Zero's metal deck onto its walkway"),
                ("brickfall",NativeFootsteps.Concrete,"metal",7.2f,"Brickfall's upper floor onto a steel plate"),("factory",NativeFootsteps.Concrete,"metal",0f,"the factory floor onto a press bolster"),
                ("factory",NativeFootsteps.Metal,"stone",3.4f,"a factory catwalk onto a stone ledge"),("cybercity",NativeFootsteps.Concrete,"metal",0f,"Cyber City's street onto a metal grate"),("cybercity",NativeFootsteps.Concrete,"wood",0f,"Cyber City's street onto boards")})
            {
                bool found=Crossing(new World(maps[map]),from,material,top,name,out var r);if(found)list.Add(r);
                if(list.Count<2)Check(found,name+": a clear straight crossing from "+from+" onto "+material+" at "+top);
            }
            return list;
        }
        private static void Crossings(Dictionary<string,ContentMap> maps,Dictionary<string,string[]> takes,NativeSoundBank bank,List<string> log)
        {
            var worlds=maps.Values.ToDictionary(m=>m.id,m=>new World(m));var info=new List<string>();
            foreach(var r in Boundaries(maps))
            {
                var w=worlds[r.Map];int worst=0,narrow=0;
                foreach(var route in PilotRoutes)foreach(var (gait,speed,sprint) in new[]{("walk",3f,false),("run",6.8f,false),("sprint",9.18f,true)})
                {
                    var walk=Walk(w,r,new Pilot(route),speed,sprint,30,false,bank,takes);string what=r.Name+" "+route+" "+gait;
                    // The first plant from which every probe is on the new surface: the key is the new one by the next plant at the latest.
                    int on=walk.Count;while(on>0&&walk[on-1].Seen==r.To)on--;
                    Check(on>=1&&walk.Take(on).All(x=>x.Key==r.From||x.Seen==r.To),what+": the old surface until the boundary ("+on+" plants)");
                    if(walk.Count-on<2){Check(walk.All(x=>x.Key==r.From),what+": one plant on a strip keeps the old key");narrow++;continue;}
                    int switched=walk.FindIndex(on,x=>x.Key==r.To);Check(switched>=0&&switched-on<=1,what+": the new key by its second plant ("+(switched-on+1)+")");
                    Check(walk.Skip(switched).All(x=>x.Key==r.To),what+": never back while on it");
                    Check(walk.Skip(switched).Select(x=>x.Series).Distinct().Count()==1&&walk.Take(Math.Max(0,on-1)).Select(x=>x.Series).Distinct().Count()==1,what+": one series each side");
                    worst=Math.Max(worst,switched-on+1);
                }
                info.Add(r.Name+" ("+r.From.Substring(10)+" to "+r.To.Substring(10)+", by plant "+worst+(narrow>0?"; "+narrow+" of 15 walks land one plant on it, which stays "+r.From.Substring(10):"")+")");
            }
            log.Add("boundaries, 5 gaits at a walk, run and sprint: "+string.Join("; ",info));
        }

        // ---------- (iii) zombies ----------
        /// <summary>One add of <paramref name="kind"/> on <paramref name="r"/> at <paramref name="speed"/> m/s: its gait's landed limbs
        /// (the ones that sound), probed at the limb and remembered, keyed as the runtime keys them.</summary>
        internal static List<Plant> Shamble(World w,Route r,int kind,float speed,float seconds,bool pingPong,NativeSoundBank bank,Dictionary<string,string[]> takes,int id)
        {
            var plants=new List<Plant>();var g=new NativeHordeGait();var pose=new CapturedRig.Pose();var memory=new NativeSurfaceMemory();float sc=NativeHordeKinds.Scale[kind],dt=1/60f;
            var dir=Vector3.Normalize(r.B-r.A);float length=Vector3.Distance(r.A,r.B),s=0,sign=1,top=r.A.Y;
            for(int f=0;f<seconds*60;f++)
            {
                float t=2+f*dt;s+=sign*speed*dt;if(s>length){s=2*length-s;sign=pingPong?-1:0;}if(s<0){s=-s;sign=1;}
                if(!pingPong&&sign==0)break;
                var d=dir*sign;var at=r.A+dir*s;float ground=w.Ground(at.X,top,at.Z)-.005f;
                g.Pose(new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Yaw=(float)(Math.Atan2(d.X,d.Z)/D2R),Ground=ground/sc,Root=new Vector3(at.X,ground+NativeHordeKinds.DrawRoot(kind,false),at.Z)/sc,Velocity=d*speed/sc,Health=1,AttackAge=float.NaN},pose);
                if(f<30||g.Landed==0)continue;float run=NativeHordeGait.RunBlend(kind,g.Speed);
                for(int i=0;i<4;i++)
                {
                    if((g.Landed&(1<<i))==0||!NativeZombieSteps.Sounds(g.Quad,i))continue;var foot=g.FootWorld[i]*sc;string gaitKey=NativeZombieSteps.Key(kind,g.Quad,run);
                    string seen=gaitKey==NativeZombieSteps.Crawling?null:w.Key(foot.X,ground,foot.Z,sc),step=seen==null?null:memory.Plant(seen,t),key=NativeFootSurface.Zombie(gaitKey,step);int clip=bank.Next(key);
                    plants.Add(new Plant{T=t,Body=at,Foot=foot,Seen=seen??gaitKey,Key=key,Old=gaitKey,Clip=clip,Series=takes[key][clip]});
                }
            }
            return plants;
        }
        private static void Zombies(Dictionary<string,ContentMap> maps,Dictionary<string,string[]> takes,NativeSoundBank bank,List<string> log)
        {
            var worlds=maps.Values.ToDictionary(m=>m.id,m=>new World(m));var surfaces=Surfaces(maps);var hard=surfaces[0];var metal=surfaces[2];var info=new List<string>();int falls=0;
            for(int kind=0;kind<NativeHordeKinds.Count;kind++)
            {
                var row=NativeHordeKinds.ProposedRow(kind);string name=NativeHordeKinds.Names[kind];var keysHard=new SortedSet<string>();var keysMetal=new SortedSet<string>();
                foreach(var (gait,speed) in new[]{("walk",row.walk),("run",row.run)})
                {
                    int id=12000+kind*10+(gait=="run"?1:0);
                    var a=Shamble(worlds[hard.Map],hard,kind,speed,60,true,bank,takes,id);var b=Shamble(worlds[metal.Map],metal,kind,speed,60,true,bank,takes,id+5);falls+=a.Count+b.Count;
                    Check(a.Count>=20&&b.Count>=20,name+" "+gait+": footfalls on both surfaces ("+a.Count+", "+b.Count+")");
                    foreach(var (walk,where,surface) in new[]{(a,"hard floor",hard.From),(b,"metal",metal.From)})
                    {
                        foreach(var x in walk)Check(x.Key==NativeFootSurface.Zombie(x.Old,x.Old==NativeZombieSteps.Crawling?null:surface),name+" "+gait+" on "+where+": its own takes on a hard floor, the surface's elsewhere, a crawl drags ("+x.Key+")");
                        foreach(var k in walk.Select(x=>x.Key).Distinct())Check(walk.Where(x=>x.Key==k).Select(x=>x.Series).Distinct().Count()==1,name+" "+gait+" on "+where+": "+k+" plays one series");
                        var byKey=walk.GroupBy(x=>x.Key);foreach(var grp in byKey){var list=grp.ToList();for(int i=1;i<list.Count;i++)Check(list[i].Clip!=list[i-1].Clip||takes[grp.Key].Length<2,name+" "+gait+": no take twice in a row");}
                    }
                    foreach(var x in a)keysHard.Add(x.Key.Substring(x.Key.LastIndexOf('.')+1));foreach(var x in b)keysMetal.Add(x.Key.Substring(x.Key.LastIndexOf('.')+1));
                    Check(b.All(x=>x.Key==(x.Old==NativeZombieSteps.Crawling?x.Old:NativeFootsteps.Metal)),name+" "+gait+": on metal every footfall is the metal pool (a crawl drags)");
                    Check(a.All(x=>x.Key==x.Old),name+" "+gait+": on the hard floor its gait's own takes");
                }
                info.Add(name+" "+string.Join("/",keysHard)+" | "+string.Join("/",keysMetal));
            }
            // A boundary: a shambler and a brute walk the Ward Zero walkway onto its metal deck.
            var r=Boundaries(maps)[0];
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Brute,NativeHordeKinds.Runner,NativeHordeKinds.Hulk})
            {
                var row=NativeHordeKinds.ProposedRow(kind);var walk=Shamble(worlds[r.Map],r,kind,kind==NativeHordeKinds.Runner?row.run:row.walk,40,false,bank,takes,13000+kind);
                int on=walk.Count;while(on>0&&walk[on-1].Seen==r.To)on--;int switched=walk.FindIndex(on,x=>x.Key==NativeFootsteps.Metal);
                Check(on<walk.Count-2&&switched>=0&&switched-on<=1&&walk.Skip(switched).All(x=>x.Key==NativeFootsteps.Metal)&&walk.Take(on).All(x=>x.Key!=NativeFootsteps.Metal||x.Seen==r.To),NativeHordeKinds.Names[kind]+" crosses onto the metal deck by its second footfall there and stays");
            }
            log.Add("zombies, every kind walking and running 60 s ("+falls+" footfalls), hard floor | metal: "+string.Join(", ",info)+"; a shambler, a brute, a runner and the Hulk cross onto the metal deck by their second footfall");
        }

        // ---------- (iv) the probe's geometry ----------
        private static void Geometry(Dictionary<string,ContentMap> maps,List<string> log)
        {
            Check(NativeFootSurface.StartY(0,1)>NativeLocomotionRules.StepHeight&&NativeFootSurface.StartY(0,1)-NativeFootSurface.Reach(1)<=-.5f,"the probe starts over a step's rise and reaches half a metre under the feet");
            Check(NativeFootSurface.StartY(0,.5f)==NativeFootSurface.StartY(0,1)&&NativeFootSurface.Reach(2)==2*NativeFootSurface.Reach(1),"a smaller walker probes as a pilot, a larger one further");
            // A block whose top is at the feet: hit, its material; none: the map's floor.
            var test=new ContentMap{id="test-map",blocks=new[]{new ContentBlock{id="deck",material="metal",pos=new V3(0,.85f,0),size=new V3(4,.3f,4)},new ContentBlock{id="floor",material="foundation",pos=new V3(0,-.25f,0),size=new V3(40,.5f,40)}}};
            var t=new World(test);
            Check(t.Key(0,1.005f,0)==NativeFootsteps.Metal&&t.Key(0,1f,0)==NativeFootsteps.Metal,"a block whose top is at the feet is hit");
            Check(t.Key(0,1f-NativeLocomotionRules.StepHeight,0)==NativeFootsteps.Metal,"a drawn position a step's rise under the tread still hits it");
            Check(t.Key(5,.005f,0)==NativeFootsteps.Concrete&&t.Key(5,.005f,0,1.5f)==NativeFootsteps.Concrete&&t.Key(30,.005f,0)==NativeFootsteps.Concrete,"the floor, else the map's floor");
            Check(t.OldKey(0,1.005f,0)==NativeFootsteps.Metal,"the old probe hit it too (its start was the replicated feet, not the capsule's centre)");
            // A Siren-sized pilot (capsule 2.55 m): its replicated position .41 m over its feet still reaches the floor at scale 1.5.
            Check(t.Key(5,.005f+(2.55f*.5f-.865f),0,1.5f)==NativeFootsteps.Concrete&&NativeFootSurface.StartY(.41f,1.5f)-NativeFootSurface.Reach(1.5f)<0,"a scaled pilot's probe reaches its floor");
            // Every walkable top of every map: a pilot standing on it (the replicated feet at the top plus .005) hits it or a block
            // level with it, from above it; and so does one drawn a step's rise low.
            int tops=0,starts=0;
            foreach(var m in maps.Values)
            {
                var w=new World(m);var off=new HashSet<string>(m.navHints?.collisionOff??Array.Empty<string>());foreach(var f in NativeCyberCityStairs.Flights(m).Concat(NativeMapRamps.Flights(m)))foreach(var s in f.Steps)off.Add(s.id);
                foreach(var b in m.blocks)
                {
                    var r=b.rotation;bool level=r==null||Math.Abs(r.x)<1e-3f&&Math.Abs(r.z)<1e-3f;if(!level||off.Contains(b.id)||b.size.x<.9f||b.size.z<.9f)continue;
                    float top=b.pos.y+b.size.y*.5f;var o=V(b.pos);
                    if(w.Solid(new Vector3(o.X,top+.05f,o.Z)))continue; // covered: nobody stands there
                    tops++;float feet=top+.005f;Check(NativeFootSurface.StartY(feet,1)>top,m.id+" "+b.id+": the probe starts over the floor");starts++;
                    Check(w.Down(new Vector3(o.X,NativeFootSurface.StartY(feet,1),o.Z),NativeFootSurface.Reach(1),out float d,out _)&&NativeFootSurface.StartY(feet,1)-d>=top-.02f,m.id+" "+b.id+": a standing pilot's probe hits its floor");
                    Check(w.Down(new Vector3(o.X,NativeFootSurface.StartY(feet-NativeLocomotionRules.StepHeight,1),o.Z),NativeFootSurface.Reach(1),out d,out _)&&NativeFootSurface.StartY(feet-NativeLocomotionRules.StepHeight,1)-d>=top-.02f,m.id+" "+b.id+": a step's rise of lag still hits it");
                }
            }
            log.Add("probe: "+NativeFootSurface.Above+" m over the feet to "+NativeFootSurface.Below+" m under (x scale over 1); hits "+tops+" walkable tops on "+maps.Count+" maps from above, also with a step's rise of drawn lag; a block top at the feet, the map's floor on a miss");
        }

        // ---------- the memory ----------
        private static void Memory(List<string> log)
        {
            string Run(string seq,float gap=.17f,int freshAt=-1)
            {
                var m=new NativeSurfaceMemory();var o=new StringBuilder();float t=1;
                for(int i=0;i<seq.Length;i++){if(seq[i]=='_'){t+=NativeSurfaceMemory.Forget+.5f;continue;}t+=gap;o.Append(m.Plant(seq[i].ToString(),t,i==freshAt));}
                return o.ToString();
            }
            Check(Run("BBBB")=="BBBB","the first plant takes its surface at once");
            Check(Run("AAABAAAA")=="AAAAAAAA","one stray probe never flips a step");
            Check(Run("AAABABABAB")=="AAAAAAAAAA","a seam or a rail under alternate feet never flips");
            Check(Run("AAABBBBB")=="AAAABBBB","a boundary switches on the second plant past it and stays");
            Check(Run("AAABBABBB")=="AAAABBBBB","once switched, one stray old probe does not flip back");
            Check(Run("AAA_B")=="AAAB","after a pause the new surface at once");
            Check(Run("ABABABAB",1.1f)=="AAAAAAAA","a Hulk's slow footfalls (over a second apart) along a seam never flip either");
            Check(Run("AAAB",.17f,3)=="AAAB","after a landing (fresh) the new surface at once");
            var r=new NativeSurfaceMemory();r.Plant("A",5);Check(r.Plant("B",3)=="B","a rewound clock takes the new surface at once");
            Check(NativeFootSurface.Zombie(NativeZombieSteps.Shuffle,NativeFootsteps.Concrete)==NativeZombieSteps.Shuffle&&NativeFootSurface.Zombie(NativeZombieSteps.Heavy,NativeFootsteps.Stone)==NativeZombieSteps.Heavy&&NativeFootSurface.Zombie(NativeZombieSteps.Running,null)==NativeZombieSteps.Running,"zombies keep their own takes on the hard floors");
            foreach(var s in new[]{NativeFootsteps.Metal,NativeFootsteps.Wood,NativeFootsteps.Gravel,NativeFootsteps.Snow})
                Check(NativeFootSurface.Zombie(NativeZombieSteps.Shuffle,s)==s&&NativeFootSurface.Zombie(NativeZombieSteps.Heavy,s)==s&&NativeFootSurface.Zombie(NativeZombieSteps.Crawling,s)==NativeZombieSteps.Crawling,"zombies step in "+s+"'s pool; a crawl drags");
            Check(NativeFootSurface.Leading(1,0,-1,0,2,0,1)==0&&NativeFootSurface.Leading(1,0,-1,0,-2,0,0)==1&&NativeFootSurface.Leading(1,0,-1,0,.1f,0,1)==1,"the planted foot leads the travel; standing, the gait's");
            log.Add("memory: the first plant at once, a new surface on its second plant in a row, strays and alternating seams never flip, a pause, a landing or a rewind take it at once");
        }

        // ---------- no allocation ----------
        private static void Allocation(List<string> log)
        {
            var memories=new NativeSurfaceMemory[64];string[] seen={NativeFootsteps.Concrete,NativeFootsteps.Metal,NativeFootsteps.Concrete,NativeFootsteps.Wood};
            void Frame(int f){float t=1+f/60f;for(int i=0;i<memories.Length;i++){string k=NativeFootSurface.Key("expedition-ward-zero",(f+i)%7!=0,(f+i)%5==0?"metal":"stone");string s=memories[i].Plant(k,t);NativeFootSurface.Zombie(NativeZombieSteps.Shuffle,s);NativeFootSurface.Leading(f,i,i,f,1,1,0);s=memories[i].Plant(seen[(f/9+i)%4],t);}}
            for(int f=0;f<300;f++)Frame(f);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=300;f<2700;f++)Frame(f);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 2400 frames of 64 walkers' plants ("+used+" bytes)");
            log.Add("no allocation (2400 frames, 64 walkers)");
        }

        // ---------- the hooks ----------
        private static void Hooks(string root,List<string> log)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var audio=R("Runtime/NativeFootstepAudio.cs");
            Check(audio.Contains("arena.PhysicsWorld.Raycast(new Vector3(x,NativeFootSurface.StartY(groundY,scale),z),Vector3.down,out var hit,NativeFootSurface.Reach(scale),NativeArena.WorldMask,QueryTriggerInteraction.Ignore)")&&!audio.Contains("at+Vector3.up*.4f")&&!audio.Contains(" Physics.Raycast("),"the runtime probe is the Core probe, at the planted foot, in the arena's own physics world");
            Check(audio.Contains("hit.collider.sharedMaterial")&&audio.Contains("prop.Definition.material"),"a hit names its block's material, else its collider's physics material (the stair and ramp wedges)");
            Check(audio.Contains("memory.Plant(NativeFootSurface.Key(arena.MapId,SurfaceUnder(arena,x,groundY,z,scale,out string material),material),Time.unscaledTime)")&&audio.Contains("StepSurface(arena,pilotSurfaces,f.Id,f.FootX,f.Y,f.FootZ,f.Scale)")&&audio.Contains("pilotSurfaces.Remove(f.Id)"),"pilots step through their surface memory; a landing starts it afresh");
            Check(audio.Contains("ZombieStep(arena,f.Id,zk,f.Kind,f.FootX,f.Y,f.FootZ,f.Scale,")&&audio.Contains("key=NativeFootSurface.Zombie(gait,step)"),"zombie footfalls are keyed by their surface");
            Check(audio.Contains("if(bank.Has(key)){BankPlay(key,")&&audio.Contains("if(standIn!=null&&bank.Has(standIn)){BankPlay(standIn,"),"a key with clips plays only its own takes (no stand-in or built-in take mixes in)");
            var fp=R("Runtime/NativeFootfallPresentation.cs");
            Check(fp.Contains("FootX=at.x,FootZ=at.y,Scale=a.RigScale")&&fp.Contains("NativeFootSurface.Leading(l.x,l.z,r.x,r.z,a.Velocity.x,a.Velocity.z,foot)")&&fp.Contains("rig.Bones[CapturedRig.LFoot].position"),"a pilot's footfall carries its planted foot bone");
            Check(fp.Contains("FootX=w.X,FootZ=w.Z,Scale=s.Scale"),"a zombie's footfall carries its landed limb");
            Check(R("Runtime/NativeHulkChargeAudio.cs").Contains("ZombieStep(arena,w.Id,step,NativeHordeKinds.Hulk,hulk.x,hulk.y,hulk.z,sc,near,"),"the Hulk's charge stomps step on its surface too");
            // The footfall's Y is the replicated feet (the capsule's bottom), which the probe's start and reach are measured from.
            var arena=R("Runtime/NativeArena.cs");Check(arena.Contains("a.Position=Vec(p.Body.position-Vector3.up*.865f)")&&R("Runtime/ArenaPresentation.cs").Contains("PilotFootfall(a,p,renderTime,posed);"),"a pilot's footfall height is its replicated feet");
            Check(R("Runtime/NativeCyberCityExitCollision.cs").Contains("collider.sharedMaterial=surfaces[flight.Steps[0].material]"),"the stair and ramp wedges carry their treads' physics material");
            var core=R("Core/NativeFootfalls.cs");Check(!core.Contains("UnityEngine")&&core.Contains("public struct NativeSurfaceMemory"),"the classification and the memory are Core");
            log.Add("hooks: the Core probe at the planted foot, block or wedge material, the memory per walker, zombies by surface, a key's own takes only");
        }
    }
}
#endif
