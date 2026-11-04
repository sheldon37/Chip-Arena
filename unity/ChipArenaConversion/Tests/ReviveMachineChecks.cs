using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The REVIVE-O-MATIC's presentation (docs/REVIVE-MACHINE.md): clean deterministic models inside their budgets, its show in the order it promises from any clock, a pilot who leaves by the mouth and ends exactly where the rules put them,
    /// sound that is original and in time, a screen that says what is true, a machine that stands clear of everything in the hubs, and nothing of it reaching money, the wire or who is alive.
    /// </summary>
    internal static class ReviveMachineChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Revive machine: "+what);count++;}
        private sealed class Recorder:ITechniqueFxSink
        {
            public int Draws;public float Hash;public bool Broken;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Hash+=v.X*.13f+v.Y*.17f+v.Z*.19f;Draws++;}
            private void F(float f){if(!MathUtil.Finite(f))Broken=true;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);F(size);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){V(head);V(velocity);F(width);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);F(opacity);F(size);}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){V(at);V(color);F(size);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){V(tail);V(head);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){V(a);V(b);V(c);V(d);}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){V(at);F(opacity);F(size);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){V(at);}
            public void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){V(head);}
            public void Tongue(bool view,Vector3 tail,Vector3 head,Vector3 color,float width){V(tail);V(head);}
        }
        public static void Run(string root)
        {
            count=0;string Code(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            Models();Show();Pilot();Sound();Board();Fx();Standing(root);Wiring(Code);
            Console.WriteLine("PASS Revive machine: "+count+" checks (clean models in budget, the show's order from any clock, the pilot from mouth to landing, original sound in time, an honest screen, clear of the hubs' records, nothing on the wire)");
        }
        private static void Models()
        {
            int total=0;
            foreach(var (id,build,budget) in NativeReviveMachineModels.All)
            {
                var m=build();total+=m.Triangles;Check(m.Triangles>0&&m.Triangles<=budget,id+" budget ("+m.Triangles+" of "+budget+")");var again=build();
                Check(m.Sections.Count==again.Sections.Count&&m.Sections.Zip(again.Sections,(a,b)=>a.Positions.SequenceEqual(b.Positions)&&a.Indices.SequenceEqual(b.Indices)).All(x=>x),id+" builds the same twice");
                foreach(var s in m.Sections)
                {
                    Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Emission.Count&&s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),id+" section arrays agree");
                    Check(s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),id+" finite positions and unit normals");
                    if(s.Bone==NativeReviveMachineModels.Glass)Check(s.Colors.All(c=>c.W<1),id+" glass is see-through");else Check(s.Colors.All(c=>c.W==1),id+" solid parts are opaque");
                    if(s.Bone>=NativeReviveMachineModels.Lights)for(int i=0;i<s.Colors.Count;i++){var c=s.Colors[i];var e=s.Emission[i];if(Math.Abs(c.X-e.X)+Math.Abs(c.Y-e.Y)+Math.Abs(c.Z-e.Z)>1e-4f||e.X+e.Y+e.Z<=0){Check(false,id+" light bone "+s.Bone+" must be unlit");break;}}
                }
            }
            Check(total<90000,"the whole machine's budget ("+total+")");
            var mat=NativeReviveMachineModels.All.First(a=>a.id==NativeReviveMachineModels.Mat).build();mat.Bounds(0,out var min,out var max);Check(max.Y<.1f,"the mat is flat: nothing to trip on ("+max.Y+")");
            var lane=NativeReviveMachineModels.All.First(a=>a.id==NativeReviveMachineModels.Lane).build();lane.Bounds(0,out min,out max);Check(max.Y<.02f,"the lane is paint");
            Check(max.Z<=NativeReviveMachineModels.MatAt.Z-NativeReviveMachineModels.MatSize/2+.01f,"the lane stops at the mat ("+max.Z+")");
            // Every shaking part in the shut pose stays inside the solid box, bar the lane and the mat on the floor and what stands over a pilot's head.
            var box=NativeReviveMachine.Solids[0];var parts=new List<(string id,Matrix4x4 m)>();NativeReviveMachine.PoseParts(-1,parts,0);
            foreach(var (id,m) in parts)
            {
                if(id==NativeReviveMachineModels.Lane||id==NativeReviveMachineModels.Mat||id==NativeReviveMachineModels.Console||id==NativeReviveMachineModels.Lever)continue;
                var built=NativeReviveMachineModels.All.First(a=>a.id==id).build();
                foreach(var s in built.Sections)if(s.Bone!=NativeReviveMachineModels.Glass)foreach(var p in s.Positions)
                {
                    var w=Vector3.Transform(p,m);if(w.Y>2.2f)continue;
                    if(Math.Abs(w.X-box.centre.X)>box.size.X/2+.02f||Math.Abs(w.Z-box.centre.Z)>box.size.Z/2+.02f){Check(false,id+" pokes out of the machine's collider at a pilot's height: "+w);break;}
                }
            }
        }
        private static void Show()
        {
            float clock=11;
            Check(NativeReviveMachine.CoinsEnd<NativeReviveMachine.Gulp&&NativeReviveMachine.SpinFrom<NativeReviveMachine.ShakeFrom&&NativeReviveMachine.ShakePeak<NativeReviveMachine.Spit&&NativeReviveMachine.Gulp<NativeReviveMachine.Spit&&NativeReviveMachine.Spit<NativeReviveMachine.Touch&&NativeReviveMachine.Touch<NativeReviveMachine.Touch2&&NativeReviveMachine.Touch2<NativeReviveMachine.SkidEnd&&NativeReviveMachine.SkidEnd<NativeReviveMachine.RollAt&&NativeReviveMachine.RollAt<NativeReviveMachine.RollEnd&&NativeReviveMachine.RollEnd<NativeReviveMachine.UpAt&&NativeReviveMachine.ShutFrom<NativeReviveMachine.Shut&&NativeReviveMachine.Shut<NativeReviveMachine.End,"the beats run in order");
            Check(NativeReviveMachine.Spit>=2.5f&&NativeReviveMachine.End<=8,"a wind-up long enough to enjoy and a show short enough to live with");
            // Shut, then open at the spit, then shut again by its end; the door never bursts before it should.
            for(float t=0;t<NativeReviveMachine.Spit;t+=.02f){Check(Math.Abs(NativeReviveMachine.JawAngle(t))<.05f&&Math.Abs(NativeReviveMachine.VisorAngle(t))<.05f,"the mouth stays shut before the spit ("+t+")");Check(NativeReviveMachine.TongueOut(t)==0,"the tongue waits");}
            Check(NativeReviveMachine.JawAngle(NativeReviveMachine.Spit+.08f)>1.3f&&NativeReviveMachine.VisorAngle(NativeReviveMachine.Spit+.09f)<-1.4f,"it bursts open at once");
            Check(NativeReviveMachine.TongueOut(NativeReviveMachine.Spit+.2f)>.95f,"the tongue is out by .2 s");
            Check(Math.Abs(NativeReviveMachine.JawAngle(NativeReviveMachine.End))<.04f&&Math.Abs(NativeReviveMachine.VisorAngle(NativeReviveMachine.End))<.04f&&NativeReviveMachine.TongueOut(NativeReviveMachine.End)==0,"shut again by the end");
            // Its angular speed never jumps: no pop in the wheel when the phases hand over.
            float last=NativeReviveMachine.WheelAngle(0,clock),lastSpeed=0;for(float t=.02f;t<NativeReviveMachine.End;t+=.02f){float a=NativeReviveMachine.WheelAngle(t,clock);float sp=(a-last)/.02f;Check(sp>=-.01f&&Math.Abs(sp-lastSpeed)<8,"the flywheel runs smoothly ("+t+": "+sp+")");last=a;lastSpeed=sp;}
            // The gulp is a real pause: no shake to speak of between the rear and the spit, the most just before it.
            Check(NativeReviveMachine.ShakeLevel(NativeReviveMachine.Gulp+.2f)<.2f&&NativeReviveMachine.ShakeLevel(NativeReviveMachine.ShakePeak-.02f)>.85f,"it shakes, then holds its breath");
            // The whole pose is finite at any clock, and the same from a late joiner's first frame as from a watcher's.
            var a1=new List<(string id,Matrix4x4 m)>();var a2=new List<(string id,Matrix4x4 m)>();
            foreach(float t in new[]{-1f,0,.5f,1.1f,2.41f,2.9f,3,3.01f,3.1f,3.9f,4.6f,5.5f,6,6.59f})
            {
                NativeReviveMachine.PoseParts(t,a1,clock);NativeReviveMachine.PoseParts(t,a2,clock);Check(a1.Count==a2.Count&&a1.Zip(a2,(x,y)=>x.id==y.id&&x.m==y.m).All(z=>z),"a moment draws the same twice ("+t+")");
                foreach(var (id,m) in a1)Check(new[]{m.M11,m.M22,m.M33,m.M41,m.M42,m.M43}.All(float.IsFinite)&&m.GetDeterminant()>.01f,"a sound matrix for "+id+" at "+t);
                Check(a1.All(x=>NativeReviveMachineModels.All.Any(y=>y.id==x.id)),"every part it asks for is in the bank");
            }
            // Nothing allocates per frame once the list is warm.
            var warm=new List<(string id,Matrix4x4 m)>(96);NativeReviveMachine.PoseParts(4,warm,clock);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)NativeReviveMachine.PoseParts(i*.03f,warm,clock);long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used<16000,"posing it allocates next to nothing ("+used+")");
            // Coins go up the tube and the lines of it meet the hopper.
            Check(NativeReviveMachine.CoinProgress(0,0)<0&&NativeReviveMachine.CoinProgress(NativeReviveMachine.Coins-1,NativeReviveMachine.CoinsEnd)>=1,"every coin is up the tube by the time the shake takes over");
            Check(Vector3.Distance(NativeReviveMachine.CoinAt(1),NativeReviveMachineModels.CoinPath[NativeReviveMachineModels.CoinPath.Length-1])<.001f,"the coin path ends at the tube's end");
        }
        private static void Pilot()
        {
            var mouth=new Vector3(0,1.36f,1.32f);var land=new Vector3(.4f,.1f,6.2f);const float lift=-.015f;
            Check(!NativeReviveMachine.Flying(NativeReviveMachine.Spit-.01f,mouth,land,lift,out _,out _,out _),"nothing to draw before the spit");
            Check(NativeReviveMachine.Flying(NativeReviveMachine.Spit,mouth,land,lift,out var at,out _,out _)&&Vector3.Distance(at,mouth-new Vector3(0,NativeReviveMachine.RootCentre,0))<.01f,"the pilot leaves from the mouth");
            Check(NativeReviveMachine.Flying(NativeReviveMachine.SkidEnd-.001f,mouth,land,lift,out at,out float tumble,out float roll)&&Vector3.Distance(at,land+new Vector3(0,lift,0))<.02f&&tumble==0&&roll==0,"they end exactly where the rules' capsule stands, upright");
            Check(!NativeReviveMachine.Flying(NativeReviveMachine.UpAt,mouth,land,lift,out _,out _,out _),"the pose is the rules' again once up");
            Vector3 prev=default;float prevTumble=0;bool have=false;float peak=0;
            for(float t=NativeReviveMachine.Spit;t<NativeReviveMachine.UpAt;t+=.01f)
            {
                if(!NativeReviveMachine.Flying(t,mouth,land,lift,out at,out tumble,out roll))continue;peak=Math.Max(peak,at.Y);
                if(have){float gap=Vector3.Distance(at,prev);Check(gap<.2f,"no jump in the flight ("+t+": "+gap+")");float turn=Math.Abs(tumble-prevTumble)%(float)(Math.PI*2);Check(Math.Min(turn,(float)(Math.PI*2)-turn)<.42f,"no jump in the tumble ("+t+")");}
                prev=at;prevTumble=tumble;have=true;Check(at.Y>=land.Y-.05f,"never through the floor ("+t+")");
            }
            Check(peak>mouth.Y-NativeReviveMachine.RootCentre+.8f,"a proper arc ("+peak+")");
            NativeReviveMachine.DownTimes(10,out float downAt,out float landed,out float pickup,out float rollAt,out float rollEnd,out float until);
            Check(downAt==10+NativeReviveMachine.Spit&&downAt<landed&&landed<=pickup&&pickup<rollAt&&rollAt<rollEnd&&rollEnd<until&&until==10+NativeReviveMachine.UpAt,"the knockdown's moments are in the order the pose reads them");
            Check(NativeSpecialRules.Phase(landed,pickup,rollAt,rollEnd,false,landed-.01f)=="air"&&NativeSpecialRules.Phase(landed,pickup,rollAt,rollEnd,false,rollAt+.01f)=="roll"&&NativeSpecialRules.Phase(landed,pickup,rollAt,rollEnd,false,rollEnd+.01f)=="rise","the game's knockdown reads them as flight, roll and rise");
            Check(NativeReviveMachine.HoldUntil==NativeReviveMachine.UpAt,"the rules hold the input until the get-up ends");
        }
        private static void Sound()
        {
            float at=-1;foreach(var c in NativeReviveMachineSounds.Timeline){Check(c.At>=at,"the cues are in time order");at=c.At;Check(c.Sound>=0&&c.Sound<NativeReviveMachineSounds.Count&&c.Gain>0&&c.Gain<=1,"a cue names a sound");}
            Check(NativeReviveMachineSounds.Timeline.Any(c=>c.Sound==NativeReviveMachineSounds.Ptooey&&c.At==NativeReviveMachine.Spit),"PTOOEY is on the spit");
            Check(NativeReviveMachineSounds.Timeline.Count(c=>c.Sound==NativeReviveMachineSounds.Splat&&(c.At==NativeReviveMachine.Touch||c.At==NativeReviveMachine.Touch2))==2,"a splat for each touch");
            // The gulp is silent for the machine's motor: the wind-up has stopped by then.
            var windup=NativeReviveMachineSounds.Timeline.First(c=>c.Sound==NativeReviveMachineSounds.Windup);Check(windup.At+NativeReviveMachineSounds.Duration(NativeReviveMachineSounds.Windup)<=NativeReviveMachine.Gulp+.01f,"the motor cuts out on the gulp");
            for(int i=0;i<NativeReviveMachineSounds.Count;i++)
            {
                var a=NativeReviveMachineSounds.Samples(i);var b=NativeReviveMachineSounds.Samples(i);Check(a.SequenceEqual(b),"sound "+i+" is the same twice");Check(a.All(v=>float.IsFinite(v)&&v>=-1&&v<=1),"sound "+i+" is in range");
                Check(a.Length==(int)Math.Ceiling(NativeReviveMachineSounds.Duration(i)*NativeReviveMachineSounds.SampleRate),"sound "+i+"'s length");float peak=a.Max(Math.Abs);Check(peak>.1f,"sound "+i+" isn't silent ("+peak+")");
            }
        }
        private static void Board()
        {
            Check(NativeReviveMachine.Price==250,"the owner's fixed price");
            var p=new NativeReviveMachinePreview();p.Reset();p.Place(new Vector3(0,0,0),0);
            Check(p.Board.Preview&&p.Board.Dead.Length==3&&p.Board.Dead.All(d=>!string.IsNullOrEmpty(d.Cause)&&!string.IsNullOrEmpty(d.AreaName)),"the preview's list reads as the real one will");
            string first=p.Board.Dead[0].Account;Check(p.Request("nobody",5)!=null,"it won't revive who isn't on the list");Check(p.Request(first,5)==null&&p.Board.Wallet==350&&p.Board.Dead.Length==2,"a revive charges the fixed price and takes them off the list");
            var spit=NativeReviveMachine.Current(p.Board,6);Check(spit!=null&&NativeReviveMachine.SpitOf(p.Board,NativeReviveMachinePreview.StandIn,6)==spit&&NativeReviveMachine.SpitOf(p.Board,1,6)==null,"the spit is findable by seat");Check(p.Request(p.Board.Dead[0].Account,6)!=null,"one at a time: it refuses while it's busy");
            Check(NativeReviveMachine.Current(p.Board,5.05f+NativeReviveMachine.End+.1f)==null,"the show ends");
            Check(p.Request(p.Board.Dead[0].Account,20)==null&&p.Board.Wallet==100,"a second one");Check(p.Request(p.Board.Dead[0].Account,40)!=null&&p.Board.Dead.Length==1,"too poor: refused, nothing charged");
            var land=new Vector3(spit.Land.x,spit.Land.y,spit.Land.z);Check(Vector3.Distance(land,Vector3.Transform(NativeReviveMachine.DefaultLand,NativeReviveMachine.Frame(p.At,p.Yaw)))<.01f,"it lands on the mat");
            // Auto runs every state of the screen and comes back round.
            p.Reset();bool sawOpen=false,sawPoor=false,sawEmpty=false,sawPress=false;int presses=0;
            for(float t=0;t<80;t+=.05f){p.Script(t);if(p.WantOpen)sawOpen=true;if(p.WantOpen&&p.Board.Dead.Length>0&&p.Board.Wallet<p.Board.Cost)sawPoor=true;if(p.WantOpen&&p.Board.Dead.Length==0)sawEmpty=true;if(p.WantPress>=0&&p.WantPress<p.Board.Dead.Length){Check(p.Request(p.Board.Dead[p.WantPress].Account,t)==null,"Auto only presses what will go");sawPress=true;presses++;}}
            Check(sawOpen&&sawPress&&sawPoor&&sawEmpty&&presses>=2,"Auto shows the list, a revive, too poor and nobody dead");
            // A row from the view says only what the view knows: the cause and the body's spot are the rules' to add.
            var row=NativeReviveMachine.FromMember(new ExpeditionViewMember{account="a",name="Ann",weapon="pistol",area="exp-ward-zero",seat=3,connected=false});
            Check(row.Name=="Ann"&&row.Seat==3&&!row.Connected&&!row.Located&&string.IsNullOrEmpty(row.Cause)&&row.AreaName==NativeReviveMachine.AreaName("exp-ward-zero")&&row.AreaName.Length>0,"a live row is what the view says, no invented cause");
            Check(NativeReviveMachine.FromMember(null)==null&&NativeReviveMachine.AreaName(null).Length>0,"an unknown area still reads");
        }
        private static void Fx()
        {
            var fx=new NativeTechniqueFx();var world=NativeReviveMachine.Frame(new Vector3(3,0,4),.7f);var mouth=Vector3.Transform(NativeReviveMachine.Mouth,world);var land=Vector3.Transform(NativeReviveMachine.DefaultLand,world);
            int idle=0,peak=0;
            for(float t=-1;t<NativeReviveMachine.End+.5f;t+=.11f)
            {
                var r=new Recorder();fx.DrawReviveMachine(r,world,NativeReviveMachine.HullPose(t,t+5),t,t+5,t>=0,mouth,land,-.015f,1,false);Check(!r.Broken,"finite effects at "+t);peak=Math.Max(peak,r.Draws);if(t<0)idle=Math.Max(idle,r.Draws);
                var r2=new Recorder();fx.DrawReviveMachine(r2,world,NativeReviveMachine.HullPose(t,t+5),t,t+5,t>=0,mouth,land,-.015f,1,false);Check(r.Hash==r2.Hash&&r.Draws==r2.Draws,"the same frame twice at "+t);
                var thin=new Recorder();fx.DrawReviveMachine(thin,world,NativeReviveMachine.HullPose(t,t+5),t,t+5,t>=0,mouth,land,-.015f,.3f,true);Check(thin.Draws<=r.Draws,"thinner on Reduced and the low tier at "+t);
            }
            Check(idle>0&&peak>idle*3&&peak<3000,"a quiet idle and a big moment, within reason ("+idle+", "+peak+")");
            // The held breath: its steam is cut.
            var a=new Recorder();fx.DrawReviveMachine(a,world,NativeReviveMachine.HullPose(2.2f,7),2.2f,7,true,mouth,land,-.015f,1,false);var b=new Recorder();fx.DrawReviveMachine(b,world,NativeReviveMachine.HullPose(2.8f,7.6f),2.8f,7.6f,true,mouth,land,-.015f,1,false);Check(b.Draws<a.Draws/2,"the gulp cuts the steam ("+a.Draws+" to "+b.Draws+")");
        }
        private static void Standing(string root)
        {
            // The generated hub: inside the sanctuary, its solid boxes clear of the party's slots, the gates and their thresholds; the mat wholly inside the fence, its landing clear of the slots.
            var content=ExpeditionDeploymentChecksContent(root,out string json,out var layout);var room=new ExpeditionDeploymentRoom("room","host",123,"black-siren",layout,content);Check(room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"a rolled world to stand in");
            var world=NativeExpeditionGeneratedWorld.Build(room);Hub(world.Map.id,world.Area,world.Hub,"generated hub");Check(NativeReviveMachine.Spot(world.Map.id,out _,out _),"it stands in the generated hub");
            var town=layout.Area(layout.world);Check(town.map=="expedition-city",town.map);var hub=ExpeditionHubLayout.Parse(json,town.id);Hub(town.map,town,hub,"authored town");
            Check(!NativeReviveMachine.Spot("expedition-ward-zero",out _,out _)&&!NativeReviveMachine.Spot("brickfall",out _,out _),"it stands only in the hubs");
        }
        private static ConversionContent ExpeditionDeploymentChecksContent(string root,out string json,out ExpeditionLayout layout)
        {
            string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
            string mapsJson=Read("UnityMaps.json");json=Read("ExpeditionLayout.json");var maps=System.Text.Json.JsonSerializer.Deserialize<UnityMapContent>(mapsJson,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
            var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};layout=NativeExpeditionLayout.Parse(json);layout.compatibility=NativeExpeditionLayout.Compatibility(json,content);return content;
        }
        private static void Hub(string map,ExpeditionAreaLayout area,ExpeditionHubLayout hub,string name)
        {
            Check(NativeReviveMachine.Spot(map,out var at,out float yaw),name+": a spot");var frame=NativeReviveMachine.Frame(at,yaw);
            bool InSolid(Vector3 w,float pad)
            {
                foreach(var (centre,size,turn) in NativeReviveMachine.Solids)
                {
                    var c=Vector3.Transform(centre,frame);float a=yaw+turn,cs=(float)Math.Cos(a),sn=(float)Math.Sin(a),dx=w.X-c.X,dz=w.Z-c.Z,lx=dx*cs-dz*sn,lz=dx*sn+dz*cs;
                    if(Math.Abs(lx)<=size.X/2+pad&&Math.Abs(lz)<=size.Z/2+pad)return true;
                }
                return false;
            }
            bool Inside(Vector3 w){foreach(var v in area.sanctuary)if(Math.Abs(w.X-v.x)<=v.sx/2&&Math.Abs(w.Z-v.z)<=v.sz/2)return true;return false;}
            foreach(var (centre,size,_) in NativeReviveMachine.Solids){for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){var w=Vector3.Transform(centre+new Vector3(sx*size.X/2,0,sz*size.Z/2),frame);Check(Inside(w),name+": the machine is inside the sanctuary "+w);}}
            float half=NativeReviveMachineModels.MatSize/2;var mat=NativeReviveMachineModels.MatAt;for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){var w=Vector3.Transform(mat+new Vector3(sx*half,0,sz*half),frame);Check(Inside(w),name+": the mat is inside the sanctuary "+w);}
            foreach(var s in area.slots)Check(!InSolid(new Vector3(s.x,0,s.z),.6f),name+": clear of a party slot "+s.x+","+s.z);
            foreach(var g in hub.Gates){Check(!InSolid(new Vector3(g.X,0,g.Z),3),name+": clear of a gate "+g.Id);Check(!InSolid(g.Inside.Position.ToVector(),1.2f),name+": clear of a gate's way in "+g.Id);}
            foreach(var i in area.interactions)if(i.anchor!=null&&!i.anchor.Contains(":machine:"))Check(!InSolid(new Vector3(i.x,0,i.z),.8f),name+": clear of "+i.anchor);
            foreach(var d in area.doors){Check(!InSolid(new Vector3(d.approach.x,0,d.approach.z),1),name+": clear of a door's approach");}
            var use=Vector3.Transform(NativeReviveMachine.UsePoint,frame);Check(!InSolid(use,.45f),name+": a pilot can stand at its use point");Check(Inside(use),name+": the use point is in the sanctuary, where the rules allow a remote recovery");
            var landing=Vector3.Transform(NativeReviveMachine.DefaultLand,frame);Check(!InSolid(landing,.6f)&&Inside(landing),name+": the landing is open floor in the sanctuary");
        }
        private static Vector3 ToVector(this V3 v)=>new Vector3(v.x,v.y,v.z);
        private static void Wiring(Func<string,string> R)
        {
            var arena=R("Runtime/ArenaPresentation.cs");Check(arena.Contains("ReviveMachineBegin(s,renderTime);")&&arena.Contains("p=ReviveMachinePlace(a,p,renderTime);")&&arena.Contains("ReviveMachineTumble(rig,a);"),"the render loop flies the spat pilot");
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("ReviveMachineWorld(arena);"),"it is drawn with the other machines");
            Check(R("Runtime/NativeMachineModels.cs").Contains("ReviveMachineSkins(Add);"),"its models are in the bank");
            Check(R("Runtime/NativeInteractionPresentation.cs").Contains("NativeReviveMachine.SpitOf(NativeReviveMachine.Board,p.Seat.id,time)==null"),"the holographic revive stands aside for a spit");
            Check(R("Runtime/NativeExpeditionLiveUI.cs").Contains("||ReviveMachineScreenOpen")&&R("Runtime/NativeExpeditionLiveUI.cs").Contains("if(ReviveMachineKeys())return true;"),"the screen holds play and takes the cursor and the controller");
            // Presentation only: its files never touch money, the wire or who's alive. The rules come through the one hook.
            foreach(var f in new[]{"Core/NativeReviveMachine.cs","Core/NativeReviveMachineFx.cs","Core/NativeReviveMachineModels.cs","Core/NativeReviveMachineSounds.cs","Runtime/NativeReviveMachinePresentation.cs","Runtime/NativeReviveMachineScreen.cs"})
            {
                var code=R(f);foreach(var forbidden in new[]{".Spend(",".Reprint(",".Revive(","RevivePve(","Execute(","wallet-=",".Hp=(float)","Send("})Check(!code.Contains(forbidden),f+" never does "+forbidden);
            }
            var screen=R("Runtime/NativeReviveMachineScreen.cs");Check(screen.Contains("ReviveMachineRequest!=null?ReviveMachineRequest(row.Account):NotWired"),"without the rules the screen says so instead of pretending");
        }
    }
}
