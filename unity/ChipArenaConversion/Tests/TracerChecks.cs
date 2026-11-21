#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The gun rounds' tracers and landings (owner 2026-11-04; Core/NativeTracers.cs, Runtime/NativeTracerPresentation.cs): the path a tail lies
    // along (straight, ballistic, a corner), each gun's look and what chips and sides may change of it, the landings, the tiers, and
    // that the presentation draws what the frame holds and writes nothing. How it looks is reviewed in Unity captures, not here.
    internal static class TracerChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Tracers: "+what);count++;}
        private static bool Near(double a,double b,double e=1e-3)=>Math.Abs(a-b)<=e;
        private static NativeTracerStyle Gun(string spec,int round=3,float speed=60,float baseSpeed=60,NativeShotModules chips=default,bool fire=false,bool poison=false,NativeTracerSide side=NativeTracerSide.Own,float q=1,bool low=false,NativeTracerLook kit=default)
        {if(!kit.Kit)kit=NativeTracerLook.Of(null,0,spec,0);return NativeTracers.Of(spec,kit,round,speed,baseSpeed,chips,fire,poison,side,q,low);}
        public static void Run(string root)
        {
            count=0;Paths();Looks();Sides();Fast();Corners();Landings();Arrows();Ends();Text(root);
            Console.WriteLine("PASS tracers ("+count+" checks: the path a tail lies along, each gun's look, chips only in the sheath, friends and foes, the tiers, corners, the landings along the rebound, what the presentation reads)");
        }
        private static void Paths()
        {
            var o=new Vector3[NativeTracerPath.Capacity+1];var a=new float[o.Length];var p=new NativeTracerPath();
            Check(p.Walk(0,5,o,a)==0,"an empty path has nothing to draw");p.Push(Vector3.Zero);Check(p.Walk(0,5,o,a)==1,"a single sample has no tail");
            // Straight: one segment, whatever the samples, its length the tail's.
            p.Clear();for(int i=0;i<10;i++)p.Push(new Vector3(0,0,i));int n=p.Walk(0,4,o,a);
            Check(n==2&&Near(o[0].Z,9)&&Near(o[1].Z,5)&&Near(a[1],4),"a straight round is one segment, from its head back the tail's length");
            n=p.Walk(0,50,o,a);Check(n==2&&Near(o[1].Z,0)&&Near(a[1],9),"a tail is never longer than the path flown");
            n=p.Walk(1.5f,2,o,a);Check(n==2&&Near(o[0].Z,7.5)&&Near(o[1].Z,5.5)&&Near(a[1],2),"Skip starts the tail behind the head (an arrow's shaft)");
            // A ballistic arc: the tail bends with the real path and stays on it.
            p.Clear();for(int i=0;i<12;i++){float t=i*(1/60f)*3;p.Push(new Vector3(0,3+8*t-.5f*30*t*t,20*t));}n=p.Walk(0,6,o,a);
            Check(n>=3,"an arc keeps its bends ("+n+")");
            for(int i=0;i<n;i++){float t=o[i].Z/20;Check(Near(o[i].Y,3+8*t-.5f*30*t*t,.08),"the tail lies on the arc");}
            // A corner stays a corner, the tail wrapping round it.
            p.Clear();p.Push(new Vector3(0,0,0));p.Push(new Vector3(0,0,5));p.Push(new Vector3(5,0,5));n=p.Walk(0,7,o,a);
            Check(n==3&&o[1]==new Vector3(0,0,5)&&Near(o[2].Z,3)&&Near(a[2],7),"the tail wraps a ricochet's corner");
            // The ring keeps the newest; moving the newest does not grow it; no allocation.
            p.Clear();for(int i=0;i<NativeTracerPath.Capacity+9;i++)p.Push(new Vector3(0,0,i));Check(p.Count==NativeTracerPath.Capacity&&Near(p.Newest.Z,NativeTracerPath.Capacity+8),"a full ring drops the oldest");
            p.MoveNewest(new Vector3(1,2,3));Check(p.Count==NativeTracerPath.Capacity&&p.Newest==new Vector3(1,2,3),"moving the newest");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++){p.Push(new Vector3(i,0,0));p.Walk(.1f,3,o,a);}Check(GC.GetAllocatedBytesForCurrentThread()==before,"the path allocates nothing");
        }
        private static void Looks()
        {
            var pistol=Gun("pistol");var rifle=Gun("rifle",speed:150,baseSpeed:150);var mg=Gun("machinegun",round:3,speed:50,baseSpeed:50);var thin=Gun("machinegun",round:4,speed:50,baseSpeed:50);
            Check(NativeTracers.Length(rifle,150)>NativeTracers.Length(pistol,52)&&rifle.LingerLife>0&&pistol.LingerLife==0,"the rifle's the long needle with a line behind it");
            Check(Near(rifle.CoreColor.B,rifle.CoreColor.R,.2)&&rifle.Edge.B>rifle.Edge.R&&pistol.Edge.R>pistol.Edge.B&&mg.Edge.R>mg.Edge.G*1.5f,"the rifle cold, the pistol gold, the machine gun amber");
            Check(mg.Full&&!thin.Full&&mg.Sheath>0&&thin.Sheath==0&&thin.HeadSize==0,"one round in three of a belt is a full tracer, the others the thin line");
            foreach(var s in new[]{pistol,rifle,mg})Check(s.Core<=.03f&&s.Sheath<=.09f&&s.HeadSize<=.15f,"thin: no orb");
            // The tail is seconds of flight within its clamps: a faster round is longer by itself.
            Check(NativeTracers.Length(pistol,100)>NativeTracers.Length(pistol,50)&&NativeTracers.Length(pistol,5000)==pistol.MaxLength&&NativeTracers.Length(pistol,1)==pistol.MinLength,"length follows speed within its clamps");
            // Never under a couple of pixels.
            Check(NativeTracers.Width(.02f,1.6f,1000,.001f)>1.5f&&NativeTracers.Width(.02f,1.6f,1,.001f)==.02f,"a far round keeps its pixels, a near one its width");
            // Kits.
            var fan=new NativeTracerLook{Width=NativeTracerFx.Width("pistol")*1.2f,Length=1.25f,Gain=NativeTracerFx.CoreGain*1.25f,Sheath=.65f,Edge=0xffd38a,Kit=true};var f=Gun("pistol",kit:fan);
            Check(f.Core>pistol.Core&&f.Tail>pistol.Tail&&f.CoreGain>pistol.CoreGain,"fanned rounds are wider, longer and brighter");
            var drive=new NativeTracerLook{Width=NativeTracerFx.Width("machinegun")*1.35f,Length=1.3f,Gain=NativeTracerFx.CoreGain*1.5f,Sheath=.7f,Edge=0xffa64a,Heat=.9f,Kit=true};var d=Gun("machinegun",round:4,kit:drive);
            Check(d.Full&&d.Sheath>mg.Sheath&&d.Tail>mg.Tail&&d.CoreGain>mg.CoreGain&&d.HeadSize>mg.HeadSize,"every Overdrive round is a full, hotter tracer");
            // Chips colour the sheath, never the core; one colour only.
            var homing=Gun("pistol",chips:new NativeShotModules{Homing=35});Check(homing.CoreColor.R==pistol.CoreColor.R&&homing.CoreColor.G==pistol.CoreColor.G&&homing.Edge.B>pistol.Edge.B&&homing.SheathGain>pistol.SheathGain,"a chip shows in the sheath, the gun stays in the core");
            Check(!NativeTracers.Tint(default,false,false,out _)&&NativeTracers.Tint(new NativeShotModules{Bounces=1},false,false,out var bounce)&&bounce.R>bounce.B&&NativeTracers.Tint(new NativeShotModules{Velocity=1},false,false,out var cyan)&&cyan.B>cyan.R,"the modules' own colours");
            Check(NativeTracers.Tint(new NativeShotModules{Homing=1},true,false,out var flame)&&flame.R>flame.B,"an element first");
            // A slower round is heavier, a faster one finer.
            Check(Gun("pistol",speed:30,baseSpeed:60).Core>pistol.Core&&Gun("pistol",speed:120,baseSpeed:60).Core<pistol.Core,"speed changes the weight");
            Check(NativeTracers.HeadNear(.4f)==0&&NativeTracers.HeadNear(20)==1&&NativeTracers.HeadNear(80)==0,"the head's glint: not in the face, not a row of far dots");
            Check(NativeTrailFx.SpacingAt(40)==1&&NativeTrailFx.SpacingAt(120)==2,"a fast bolt sheds its sprites further apart");
            var own=pistol;NativeTracers.Own(ref own);Check(own.MaxLength>pistol.MaxLength&&own.Fade==NativeTracers.OwnFade,"the first-person round's tail reaches back to the barrel");
            Check(NativeTracers.Modules("machinegun",3)&&!NativeTracers.Modules("machinegun",4)&&NativeTracers.Modules("pistol",4)&&NativeTracers.ModuleShare<.5f,"chip modules small, on a belt's full tracers only");
            Check(NativeTracers.OwnFade>.5f,"the first-person tail stays in the shader's tracer mode");
            // Tiers.
            var r=Gun("rifle",speed:150,baseSpeed:150,q:.3f);Check(r.Sheath==0&&r.LingerLife==0&&r.HeadSize==0&&r.Core>0,"Reduced effects: the line alone");
            var l=Gun("machinegun",round:4,low:true);Check(l.Sheath==0&&l.HeadSize==0,"the low tier: no extras on the thin rounds");
            // An arrow: its own trail, behind its shaft.
            var arrow=NativeTracers.Arrow(1,NativeBow.Arrow.Full,NativeTracerSide.Own,1,false);var quick=NativeTracers.Arrow(.2f,NativeBow.Arrow.Quick,NativeTracerSide.Own,1,false);
            Check(arrow.Skip>0&&arrow.Edge.G>arrow.Edge.R&&arrow.CoreGain>quick.CoreGain&&NativeTracers.Length(arrow,50)>NativeTracers.Length(quick,50),"an arrow's trail follows its draw, green, behind the shaft");
        }
        private static void Sides()
        {
            var a=new NativeSeat{id=1,team=0};var b=new NativeSeat{id=2,team=0};var c=new NativeSeat{id=3,team=1};
            Check(NativeTracers.Side(a,a,true,false)==NativeTracerSide.Own&&NativeTracers.Side(a,b,true,false)==NativeTracerSide.Ally&&NativeTracers.Side(a,c,true,false)==NativeTracerSide.Enemy,"teams tell friends from foes");
            Check(NativeTracers.Side(a,c,false,false)==NativeTracerSide.Other&&NativeTracers.Side(a,c,false,true)==NativeTracerSide.Ally&&NativeTracers.Side(null,c,true,false)==NativeTracerSide.Other,"a free-for-all has neither; co-op all friends");
            var own=Gun("pistol");var ally=Gun("pistol",side:NativeTracerSide.Ally);var foe=Gun("pistol",side:NativeTracerSide.Enemy);var other=Gun("pistol",side:NativeTracerSide.Other);
            Check(ally.CoreGain<own.CoreGain&&ally.SheathGain<own.SheathGain,"a friend's round is quiet");Check(foe.Edge.R/Math.Max(.001f,foe.Edge.G)>own.Edge.R/Math.Max(.001f,own.Edge.G)&&foe.SheathGain>own.SheathGain,"a foe's sheath runs red");
            Check(other.CoreGain==own.CoreGain&&other.Edge.G==own.Edge.G,"in a free-for-all a round is a round");
        }
        private static void Fast()
        {
            var slow=Gun("pistol",speed:60,baseSpeed:60);var quick=Gun("pistol",speed:400,baseSpeed:400);
            Check(slow.LingerLife==0&&quick.LingerLife>0&&quick.LingerLife<=NativeTracers.FastLine+1e-4f,"a very fast round leaves a short line by itself, whatever its gun");
            Check(NativeTracers.Reached(10,.04f,250)&&!NativeTracers.Reached(60,.04f,250)&&NativeTracers.Reached(3,0,250)&&!NativeTracers.Reached(10,.5f,250)&&!NativeTracers.Reached(1,-.5f,250),"a stopping point belongs to a shot only if the round can have reached it");
            Check(NativeTracers.Line(0,.3f)==1&&NativeTracers.Line(.31,.3f)==0&&NativeTracers.Line(.1,.3f)>NativeTracers.Line(.2,.3f)&&NativeTracers.Line(-.1,.3f)==0,"a line fades over its life");
        }
        private static void Corners()
        {
            // A round bouncing off a wall at z = 5: down its old line, up its new.
            var d0=Vector3.Normalize(new Vector3(1,0,1));var d1=Vector3.Normalize(new Vector3(1,0,-1));var corner=new Vector3(5,0,5);
            var c=NativeTracers.Corner(corner-d0*.6f,d0,corner+d1*.9f,d1);Check(Vector3.Distance(c,corner)<.01f,"the corner is where the old and new lines meet");
            Check(NativeTracers.Corner(Vector3.Zero,Vector3.UnitZ,new Vector3(0,0,3),Vector3.UnitZ)==Vector3.Zero,"parallel lines have no corner");
            var far=NativeTracers.Corner(Vector3.Zero,Vector3.UnitZ,new Vector3(1,0,1),Vector3.UnitX);Check(far.Length()<3,"a corner stays near the step");
        }
        private static void Landings()
        {
            Check(NativeRoundImpacts.Gun("pistol")==NativeRoundImpact.Pistol&&NativeRoundImpacts.Gun("rifle")==NativeRoundImpact.Rifle&&NativeRoundImpacts.Gun("machinegun")==NativeRoundImpact.MachineGun&&NativeRoundImpacts.Gun("fire")==NativeRoundImpact.None&&NativeRoundImpacts.Gun(null)==NativeRoundImpact.None,"each gun lands its own");
            var n=-Vector3.UnitZ;var flight=Vector3.Normalize(new Vector3(.6f,0,.8f));var r=NativeRoundImpacts.Rebound(flight,n);Check(Near(r.X,.6f)&&Near(r.Z,-.8f),"the rebound mirrors the flight");
            Check(NativeRoundImpacts.Rebound(Vector3.Zero,n)==n,"no flight: out along the normal");
            foreach(var kind in new[]{NativeRoundImpact.Pistol,NativeRoundImpact.MachineGun,NativeRoundImpact.Rifle,NativeRoundImpact.Ricochet,NativeRoundImpact.Fork,NativeRoundImpact.Body})
            {
                int s=NativeRoundImpacts.Sparks(kind,5,1,false);Check(s>0&&NativeRoundImpacts.Sparks(kind,5,.3f,false)<s||s<=2,kind+": Reduced effects thin the sparks");
                Check(NativeRoundImpacts.Flash(kind,0,out float size,out var glow)&&glow.Max>0&&size<=.5f&&!NativeRoundImpacts.Flash(kind,.2,out _,out _),kind+": a small quick glow");
                for(int k=0;k<s;k++)
                {
                    Check(NativeRoundImpacts.Spark(kind,77,k,.01,n,flight,out var o,out var v,out var c,out float w)&&Vector3.Dot(o,n)>-.01f&&c.Max>.5f&&w>0&&w<.02f,kind+": sparks start out of the surface, hot and thin");
                    Check(!NativeRoundImpacts.Spark(kind,77,k,NativeRoundImpacts.Life,n,flight,out _,out _,out _,out _),kind+": sparks live under the mark's life");
                }
            }
            // The rifle's spray is tight along the rebound; the machine gun's a few, wide.
            float rifle=0;int m=NativeRoundImpacts.Sparks(NativeRoundImpact.Rifle,9,1,false);for(int k=0;k<m;k++){NativeRoundImpacts.Spark(NativeRoundImpact.Rifle,9,k,.02,n,flight,out var o,out _,out _,out _);rifle+=Vector3.Dot(Vector3.Normalize(o),r);}
            Check(rifle/m>.7f,"the rifle's spray runs along the rebound");Check(NativeRoundImpacts.Sparks(NativeRoundImpact.MachineGun,5,1,false)<=5&&NativeRoundImpacts.Sparks(NativeRoundImpact.Rifle,5,1,false)>5,"the machine gun throws few");
            Check(NativeRoundImpacts.Star(NativeRoundImpact.Rifle,5,0,out float rs,out _,out _)&&NativeRoundImpacts.Star(NativeRoundImpact.Pistol,5,0,out float ps,out _,out _)&&rs>ps&&!NativeRoundImpacts.Star(NativeRoundImpact.MachineGun,5,0,out _,out _,out _),"the rifle's star is the sharp one, the machine gun has none");
            Check(NativeRoundImpacts.Spike(NativeRoundImpact.Rifle,.01,out float len,out _,out _)&&len>0&&NativeRoundImpacts.Spike(NativeRoundImpact.Ricochet,.01,out _,out _,out _)&&!NativeRoundImpacts.Spike(NativeRoundImpact.Pistol,.01,out _,out _,out _),"a spike on the rifle's and a ricochet's");
            Check(NativeRoundImpacts.Debris("rifle",1)&&NativeRoundImpacts.Debris("pistol",1)&&NativeRoundImpacts.Debris("machinegun",3)&&!NativeRoundImpacts.Debris("machinegun",4),"a machine gun's debris one hit in three");
            Check(NativeRoundImpacts.Puffs(NativeRoundImpact.Rifle,.3f,false)==0&&NativeRoundImpacts.Puffs(NativeRoundImpact.Rifle,1,true)==0&&NativeRoundImpacts.Puffs(NativeRoundImpact.Rifle,1,false)>0,"dust follows the tiers");
            for(int k=0;k<8;k++)for(double age=0;age<.3;age+=.04)if(NativeRoundImpacts.Spark(NativeRoundImpact.Pistol,91,k,age,Vector3.UnitY,-Vector3.UnitY,out var o,out _,out _,out _))Check(o.Y>=0,"floor sparks bounce, never sink");
        }
        // An arrow's model points along its velocity at every point of its arc, its head on the replicated place; its trail is the path's, not a straight line.
        private sealed class ArrowSink:IBowFxSink
        {
            public Vector3 Nock,Dir;public int Arrows,Lines;
            public void Arrow(Vector3 nock,Vector3 dir,float opacity,bool view,bool standard){Nock=nock;Dir=dir;Arrows++;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Lines++;}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){}
        }
        private static void Arrows()
        {
            var sink=new ArrowSink();float g=30,speed=45;
            for(float t=0;t<1;t+=.1f)
            {
                var v=new Vector3(0,12-g*t,speed);var at=new Vector3(0,2+12*t-.5f*g*t*t,speed*t);
                NativeBowFx.InFlight(sink,new NativeBowArrowView{Position=at,Velocity=v,Born=0,Draw=1,Kind=NativeBow.Arrow.Full,PathTrail=true},t,1,false);
                Check(Vector3.Dot(sink.Dir,Vector3.Normalize(v))>.9999f,"an arrow points along its velocity, nose down as it falls");
                Check(Vector3.Distance(sink.Nock+sink.Dir*NativeBow.ArrowLength,at)<.001f,"its head on the replicated place");
            }
            Check(sink.Arrows==10&&sink.Lines==0,"with the path trail on, the old straight line is not drawn");
        }
        private static void Ends()
        {
            var ends=new NativeShotEnds();ends.Record(5,1,new V3(1,2,3));ends.Record(6,1.1f,new V3(4,5,6));
            Check(ends.Find(6,out var e)&&e.Position.x==4&&e.At==1.1f&&!ends.Find(7,out _),"the presentation reads a round's stopping point");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)ends.Find(5,out _);Check(GC.GetAllocatedBytesForCurrentThread()==before,"without allocating");
        }
        private static void Text(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            string run=R("Runtime/NativeTracerPresentation.cs"),wfx=R("Runtime/NativeWeaponFxPresentation.cs"),shots=R("Runtime/NativeShotPresentation.cs"),glow=R("Runtime/NativeCombatGlow.cs"),core=R("Core/NativeTracers.cs"),fx=R("Resources/NativeWeaponFx.shader");
            Check(wfx.Contains("HitscanFx(arena,now,q,low,trails);")&&wfx.Contains("RoundsUnseen(arena,now,q,low,trails);")&&run.Contains("e.Id<=hitscanEvent")&&run.Contains("now-e.Time>NativeTracers.LineStale"),"instant segments by their event id, stale ones skipped; rounds that came and went between frames from their stopping points");
            Check(wfx.Contains("RoundFx(draw,arena,b,v,now,q,low,trails,eye);")&&wfx.Contains("!RoundSpent(draw,arena,pair.Value,eye,now,q,low,trails)")&&wfx.Contains("RoundMarksDraw(draw,now,q,low);"),"WeaponFx draws the rounds, their ends and their landings");
            Check(shots.Contains("else if(TracerRounds&&NativeTracerFx.Traced(v)&&scene.Draws.Count>first){")&&shots.Contains("Relook(first,NativeShotLook.Slug);else HidePackHead(first);"),"a gun round's box is skipped (a heavy round's a smooth slug); the scene and its chip modules stay");
            // The launch's trace back allows the step a round takes in the tick it is born: rounds of any speed leave the drawn muzzle.
            Check(shots.Contains("a1=a+NativeShotLaunch.Tick")&&Near(NativeShotLaunch.Tick,1/60.0,1e-9),"a round's first step does not cost it its muzzle");
            Check(glow.Contains("NoHalo=round||MagicLooks&&NativeMagicFx.Owns(v)")&&glow.Contains("Traced=!round&&NativeShotRules.HasTrail(v)"),"no halo or ribbon on a gun round or an arrow");
            Check(fx.Contains("shape=w>0.5?FxTracer(uv,w):FxRibbon(uv);")&&fx.IndexOf("float2 FxTracer(",StringComparison.Ordinal)>fx.IndexOf("WEAPON FX SHARED END",StringComparison.Ordinal),"the needle in the ribbon's mode, outside the shared block");
            foreach(string banned in new[]{".Hp=","Damage(","Events.Add(",".Position=",".Velocity=","Bullets.Remove","Bullets.Add"})Check(!run.Contains(banned),"the presentation writes nothing of the match ("+banned+")");
            foreach(string banned in new[]{"UnityEngine","System.Random","DateTime","NativeArena","NativePilot"})Check(!core.Contains(banned),"Core rules read only their inputs ("+banned+")");
            foreach(string file in new[]{"Core/NativeTracers.cs","Runtime/NativeTracerPresentation.cs"})Check(File.Exists(Path.Combine(root,"Assets/ChipArena",file+".meta")),"meta: "+file);
        }
    }
}
#endif
