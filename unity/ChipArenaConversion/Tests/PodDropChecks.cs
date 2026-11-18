using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The Expedition drop pods crash in (Core NativePodDrop, NativePodModels, NativePodSounds; Runtime NativePodPresentation, the
// spawn-in's pod path, the directed drop camera, the Locker's Pod tab; docs/EXPEDITION-OVERHAUL.md R7 and W4). Re-pinned on
// purpose for a crash, not a landing (2026-11-18): the drop spans the opening the run holds (today's 3 s countdown, or Astra's
// 5 s PodOpening once A0 lands: both timelines checked), each pod comes in on its own line from behind its door, the retros of
// two pods in four fail, the impact comes at -2.8 + .25 n (-1.75 + .12 n on 3 s), the pod ploughs at most 1.5 m to rest on its
// centre sunk .2 m and leaning at most 4 degrees inside the collider envelope, the bolts blow the door and the pilot stands on
// the capsule by GO; what lasts (crater, furrow, cooling hull, smoke) is a pure function of the record and the clock; the
// director's shots, shake and still shot; the sounds; the look map; the LED name sign; the models; and the wiring, including
// that no presentation field initializer makes a Unity object (it broke the component).
internal static class PodDropChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Pod drop: "+what);count++;}
    private sealed class Recorder:INativeEntranceFx
    {
        public readonly List<string> Calls=new List<string>();
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>Calls.Add("g"+at+color+size);
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>Calls.Add("s"+head+velocity+color);
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>Calls.Add("m"+at+opacity+size);
    }
    private sealed class Sink:ISpawnInSink
    {
        public int Vertices,Triangles;public bool Finite=true;
        public int Vertex(Vector3 p,Vector3 uv,Vector4 c){if(!float.IsFinite(p.X+p.Y+p.Z+c.W))Finite=false;return Vertices++;}
        public void Triangle(int a,int b,int c)=>Triangles++;
    }
    public static void Run(string root)
    {
        count=0;Plans();Flights();Ground();Lasting();Effects();Shots();Launch();Sounds();Look();Sign();Models();Wiring(root);
        Console.WriteLine("PASS pod drop: "+count+" checks (a crash over the 3 s and 5 s openings: its own line from behind the door, the retros, two failing, the impact in its turn, the plough to its "+
            "centre, sunk and leaning inside the envelope, the bolts and the door, the pilot standing on the capsule by GO; the crater, furrow, cooling hull and smoke from the record and the clock; "+
            "the directed camera, its shake and the still shot; the sounds; parked, then leaving; the look map; the LED sign; the models; the wiring and no Unity object made in a field initializer)");
    }
    private static float Deg(float r)=>r*180/(float)Math.PI;
    private static PodPlan P(int seat,int order,float x,float z,double end,float yaw,float opening)=>NativePodDrop.Plan(seat,order,x,z,end,yaw,opening);
    private static float Angle(Quaternion q){q=Quaternion.Normalize(q);return 2*(float)Math.Acos(Math.Min(1,Math.Abs(q.W)));}
    private static void Plans()
    {
        // the opening: today's 3 s countdown by default, Astra's 5 s when a longer countdown is seen (or once A0 sets Opening)
        Check(NativePodDrop.Opening==NativeSpawnIn.Countdown&&NativePodDrop.ShortOpening==3&&NativePodDrop.LongOpening==5,"the openings: today's countdown and the 5 s PodOpening");
        Check(NativePodDrop.OpeningFor(float.NaN)==NativePodDrop.Opening&&NativePodDrop.OpeningFor(-3)==NativePodDrop.Opening&&NativePodDrop.OpeningFor(-4.9f)==5,"a countdown seen longer than 3 s is the 5 s opening");
        foreach(float opening in new[]{3f,5f})
        {
            bool five=opening==5;float first=five?-2.8f:-1.75f,stagger=five?.25f:.12f,door=five?.6f:.35f;
            for(int order=0;order<8;order++)
            {
                var p=P(3+order,order,order*2-3,-2,120.25,0,opening);
                Check(p.Appear==-opening&&p.Opening==opening,"the drop starts with the "+opening+" s opening");
                Check(Math.Abs(p.Impact-(first+stagger*order))<1e-5f,"pod "+order+" hits in its turn ("+p.Impact+" on "+opening+" s)");
                Check((order<4?Math.Abs(p.DoorAt-(p.Impact+door))<1e-5f:p.DoorAt<=p.Impact+door+1e-5f)&&p.DoorAt-NativePodDrop.BoltLead>p.Impact,"its bolts blow the door "+door+" s after the impact (sooner for a fifth pod on)");
                Check(p.Appear<p.Ride&&p.Ride<p.Punch&&p.Punch<p.RetroAt&&p.RetroAt<p.Impact&&p.Impact<p.DoorAt&&p.DoorAt<p.StandFrom&&p.StandFrom<p.StandEnd&&p.StandEnd<=-NativePodDrop.StandMargin+1e-6f&&p.End<=1e-5f,"pod "+order+"'s beats in order, standing by GO ("+opening+" s)");
                Check(p.Stop<p.Impact&&p.Impact<p.ArcFrom&&p.ArcFrom<p.ArcTo&&p.ArcTo<=0&&p.HandOff>=.5f,"the camera's beats in order (pod "+order+", "+opening+" s: "+p.Stop+" "+p.Impact+" "+p.ArcFrom+" "+p.ArcTo+")");
            }
            // the fourth pilot is out of the harness with time to spare
            var fourth=P(4,3,3,-2,120.25,0,opening);Check(fourth.StandEnd<=-NativePodDrop.StandMargin+1e-6f&&fourth.DoorAt<(five?-1.4f:-.9f),"the fourth pod's door blows well before GO");
            // the plan's timing tracks the 5 s one exactly
            if(five){var p0=P(3,0,-3,-2,120.25,0,5);Check(Math.Abs(p0.Ride+4.1f)<1e-5f&&Math.Abs(p0.RetroAt+3.2f)<1e-5f&&Math.Abs(p0.Punch+3.7f)<.45f,"the 5 s beats: the ride at -4.1, the retros at -3.2, the punch near -3.7 ("+p0.Punch+")");}
        }
        // two of the first four pods fail, every run; which two is the run's
        int[] fails=new int[4];int runs=0;
        for(int run=0;run<400;run++){double end=60+run*7.31;int n=0;for(int k=0;k<4;k++)if(NativePodDrop.Fails(end,k)){n++;fails[k]++;}Check(n==2,"exactly two of four fail (run "+run+")");runs++;}
        Check(fails.All(f=>f>runs/4&&f<runs*3/4),"every seat fails some runs and not others ("+string.Join(",",fails)+")");
        // the plan is the record's: the same record, the same crash; another seat, spot or start, another one
        var a=P(5,1,-1,-2,99.5,0,5);var b=P(5,1,-1,-2,99.5,0,5);Check(a.Seed==b.Seed&&a.Bearing==b.Bearing&&a.P0==b.P0&&a.Crater==b.Crater&&a.CraterTurn==b.CraterTurn&&a.Lean==b.Lean,"the same record plans the same crash");
        Check(P(6,1,-1,-2,99.5,0,5).Seed!=a.Seed&&P(5,1,-.5f,-2,99.5,0,5).Seed!=a.Seed&&P(5,1,-1,-2,99.6,0,5).Seed!=a.Seed,"another seat, spot or start makes another crash");
    }
    private static void Flights()
    {
        int seen=0;var rnd=new Random(7);
        foreach(float opening in new[]{3f,5f})for(int i=0;i<300;i++)
        {
            float yaw=i<3?new[]{0f,90f,225f}[i]:(float)(rnd.NextDouble()*360);var p=P(1+i%8,i%4,(float)(rnd.NextDouble()*30-15),(float)(rnd.NextDouble()*30-15),40+i*3.7,yaw,opening);
            // it comes in from behind its door, within 40 degrees, at 45-60 degrees
            Check(Vector3.Dot(p.Travel,p.Forward)>=(float)Math.Cos(NativePodDrop.MaxBearing)-1e-4f&&Math.Abs(p.Bearing)<=NativePodDrop.MaxBearing+1e-5f,"its line is within 40 degrees of straight behind its door");
            Check(Vector3.Dot(new Vector3(p.P0.X,0,p.P0.Z),p.Forward)<0,"it comes from behind its door");
            float dive=(float)Math.Atan2(-p.V1.Y,new Vector2(p.V1.X,p.V1.Z).Length());Check(Deg(dive)>=45-1e-3f&&Deg(dive)<=60+1e-3f,"it comes in at 45-60 degrees ("+Deg(dive)+")");
            Check(Math.Abs(Vector3.Distance(Vector3.Normalize(p.Forward),new Vector3((float)Math.Sin(yaw*Math.PI/180),0,(float)Math.Cos(yaw*Math.PI/180))))<1e-4f,"the door faces the record's yaw in degrees ("+yaw+")");
            // high and far: the long opening 200 m out and 140 m up, the short one nearer
            float reach=new Vector2(p.P0.X,p.P0.Z).Length(),height=p.P0.Y;
            Check(opening==5?reach>175&&reach<235&&height>125&&height<160:reach>105&&reach<142&&height>75&&height<100,"it appears high and far ("+reach+" m out, "+height+" m up)");
            // it only falls and closes in, braking at the end
            float lastY=float.MaxValue,lastReach=float.MaxValue,bad=float.NaN;var touch=p.P1;
            for(float t=p.Appear;t<p.Impact-1e-3f;t+=1/90f)
            {
                var at=NativePodDrop.FlightAt(p,t,out var v);float r=new Vector2(at.X-touch.X,at.Z-touch.Z).Length();
                if(!(float.IsFinite(at.X+at.Y+at.Z+v.X+v.Y+v.Z)&&at.Y<lastY+1e-4f&&r<=lastReach+1e-3f)&&float.IsNaN(bad))bad=t;lastY=at.Y;lastReach=r;
            }
            Check(float.IsNaN(bad),"pod "+i+" only falls and closes in ("+bad+")");
            var end=NativePodDrop.FlightAt(p,p.Impact,out var v1);Check(Vector3.Distance(end,p.P1)<1e-3f&&Math.Abs(v1.Length()-p.Speed)<.05f,"it touches down where and as fast as planned");
            NativePodDrop.FlightAt(p,p.Appear,out var v0);
            Check(p.Fails?p.Speed>=40:p.Speed<.4f*v0.Length()&&p.Speed>8,"the retros brake it hard (a working set) or it comes in hot (a failing one): "+v0.Length()+" to "+p.Speed+" m/s");
            Check(NativePodDrop.Retro(p,p.RetroAt-.05f)==0&&NativePodDrop.Retro(p,p.Impact-.05f)>0,"the retros burn only at the end");
            seen++;
        }
        // a run's four pods come in on four lines
        var run=Enumerable.Range(0,4).Select(k=>P(10+k,k,k*2-3,-2,77.7,0,5)).ToArray();
        for(int x=0;x<4;x++)for(int y=x+1;y<4;y++)Check(Math.Abs(run[x].Bearing-run[y].Bearing)>1e-3f,"each pod has its own line");
        // a working set holds its burn, a failing one sputters
        var working=Enumerable.Range(0,40).Select(k=>P(1,k%4,0,0,30+k,0,5)).First(q=>!q.Fails);var failing=Enumerable.Range(0,40).Select(k=>P(1,k%4,0,0,30+k,0,5)).First(q=>q.Fails);
        float Min(PodPlan q){float m=9;for(float t=q.RetroAt+.08f;t<q.Impact-.02f;t+=.005f)m=Math.Min(m,NativePodDrop.Retro(q,t));return m;}
        Check(Min(working)>.7f&&Min(failing)<.3f,"a working burn holds ("+Min(working)+"), a failing one sputters ("+Min(failing)+")");
        Check(seen==600,"600 lines checked");
    }
    private static void Ground()
    {
        var envelope=NativePodModels.Pod().Sections.SelectMany(s=>s.Positions).ToArray();var rnd=new Random(3);
        foreach(float opening in new[]{3f,5f})for(int i=0;i<200;i++)
        {
            float yaw=i<3?new[]{0f,90f,225f}[i]:(float)(rnd.NextDouble()*360);var p=P(2+i%7,i%4,(float)(rnd.NextDouble()*20-10),(float)(rnd.NextDouble()*20-10),55+i*2.9,yaw,opening);
            // it touches down at most 1.5 m short, behind its door within 40 degrees, and the furrow stays in its 2.5 m apron
            var back=-new Vector3(p.P1.X,0,p.P1.Z);Check(p.Skid<=NativePodDrop.MaxSkid+1e-5f&&p.Skid>.5f&&Math.Abs(p.P1.Y)<1e-5f,"it touches down at most 1.5 m short ("+p.Skid+")");
            Check(Vector3.Dot(Vector3.Normalize(back),p.Forward)>=(float)Math.Cos(NativePodDrop.MaxBearing)-1e-4f,"the touchdown is behind its door");
            Check(p.Furrow>=3&&p.Furrow<=NativePodModels.Radius+2.5f&&p.FurrowWidth<=2*NativePodModels.Radius,"the furrow is 3 m or more and stays inside the skid apron ("+p.Furrow+")");
            // the frames: the slam, the plough to the centre, then sunk and leaning at rest
            float lastDoor=0,worst=0;bool shown=false;string wrong=null;
            for(float t=p.Impact;t<.6f;t+=1/120f)
            {
                var f=NativePodDrop.Evaluate(t,p);worst=Math.Max(worst,Deg(Angle(f.Tilt)));
                if(!(float.IsFinite(f.Offset.X+f.Offset.Y+f.Offset.Z+f.Door+f.Pilot.X+f.Pilot.Y+f.Pilot.Z)&&!f.Gone))wrong??="finite frames";
                if(!(new Vector2(f.Offset.X,f.Offset.Z).Length()<=p.Skid+1e-4f&&f.Offset.Y<=.001f&&f.Offset.Y>=-p.Sink-.09f))wrong??="it ploughs in from the touchdown, never past its centre";
                if(t>=p.Impact+p.SkidSeconds&&!(new Vector2(f.Offset.X,f.Offset.Z).Length()<1e-4f))wrong??="it rests on its centre after the skid";
                if(!(f.Door>=lastDoor-.15f))wrong??="the door only opens (a small bounce at rest)";lastDoor=f.Door;
                if(t<p.DoorAt&&!(f.Door==0&&!f.Shown))wrong??="the door stays shut until the bolts blow it";
                if(f.Shown){shown=true;if(!(t>=p.DoorAt-1e-4f))wrong??="the pilot shows once the door is blown";}
                if(t<p.DoorAt-NativePodDrop.BoltLead-1e-4f&&f.Bolts!=0)wrong??="the bolts are dark until they fire";
                if(wrong!=null){wrong+=" ("+t+")";break;}
            }
            Check(wrong==null,wrong??"");Check(worst<(p.Fails?24:20),"the slam pitches it over, not down ("+worst+")");
            Check(shown,"its pilot shows");
            var rest=NativePodDrop.Evaluate(float.NaN,p);
            Check(!rest.Dropping&&rest.Offset==new Vector3(0,-NativePodDrop.Sink,0)&&Math.Abs(Angle(rest.Tilt)-p.Lean)<1e-3f&&p.Lean<=NativePodDrop.MaxLean+1e-5f&&rest.Door==p.RestDoor&&rest.Shown&&rest.Sign==1,"parked: sunk .2 m, leaning at most 4 degrees, door open, sign lit");
            var settled=NativePodDrop.Evaluate(p.End+.01f,p);Check(Math.Abs(Angle(settled.Tilt)-p.Lean)<1e-3f&&settled.Offset==rest.Offset,"after the drop it sits at rest");
            // at rest the hull is inside the collider envelope A1 will give it (two crossed 1.96 x 2.85 x 1.96 m boxes)
            var turn=Quaternion.Multiply(rest.Tilt,NativePodDrop.Facing(p));var inv=Quaternion.Inverse(NativePodDrop.Facing(p));float h=NativePodModels.EnvelopeHalf,c=(float)Math.Cos(Math.PI/4),sn=(float)Math.Sin(Math.PI/4);
            Vector3? outside=null;
            foreach(var v in envelope)
            {
                var w=Vector3.Transform(Vector3.Transform(v,turn)+rest.Offset,inv);float ax=Math.Abs(w.X),az=Math.Abs(w.Z),bx=Math.Abs(w.X*c-w.Z*sn),bz=Math.Abs(w.X*sn+w.Z*c);
                if(!(w.Y<=NativePodModels.EnvelopeHeight+1e-3f&&(ax<=h+.03f&&az<=h+.03f||bx<=h+.03f&&bz<=h+.03f))){outside=w;break;}
            }
            Check(outside==null,"the rested hull stays inside the collider envelope ("+outside+")");
            // the ramp rests its top on the crater floor
            var rot=Quaternion.Multiply(rest.Tilt,NativePodDrop.Facing(p));var tip=rest.Offset+Vector3.Transform(NativePodModels.Hinge+new Vector3(0,NativePodModels.DoorHeight*(float)Math.Cos(p.RestDoor),NativePodModels.DoorHeight*(float)Math.Sin(p.RestDoor)),rot);
            Check(Math.Abs(tip.Y-(NativePodModels.FlatLift+.01f))<.02f,"the ramp's top rests on the crater ("+tip.Y+")");
            // the pilot: strapped in on the pod's full offset and lean, then standing on the capsule by GO
            var ride=NativePodDrop.Evaluate((p.RetroAt+p.Impact)*.5f,p);Check(Vector3.Distance(ride.Pilot,NativePodDrop.PodPoint(ride,p,NativePodDrop.CabinFeet))<1e-4f&&ride.Harness==0,"strapped in, the pilot rides the pod's offset");
            var go=NativePodDrop.Evaluate(-NativePodDrop.StandMargin,p);Check(Vector3.Distance(go.Pilot,NativePodDrop.Capsule(p))<1e-3f&&Angle(go.PilotTilt)<1e-3f&&go.Harness>.99f,"by GO the pilot stands upright on the capsule, the harness up");
            var unbuckled=NativePodDrop.Evaluate(p.StandFrom-.01f,p);Check(unbuckled.Shown&&Angle(unbuckled.PilotTilt)>NativePodDrop.HunchAngle*.5f,"braced against the bar when the door blows");
        }
        // the capsule is the pod centre until A1 (ExitReach 0), and the record is the capsule
        Check(NativeExpeditionHubPlan.ExitReach==0&&NativePodDrop.Centre(3,0,-2,90)==new Vector3(3,0,-2),"the pod centre is the record (ExitReach 0)");
    }
    private static void Lasting()
    {
        var p=P(7,2,9,4,300.1,135,5);
        Check(NativePodDrop.Cool(0)>.8f&&NativePodDrop.Cool(5)>.3f&&NativePodDrop.Cool(NativePodDrop.CoolSeconds)==0&&NativePodDrop.Cool(30)==0,"the hull cools from hot to black over 20 s");
        Check(NativePodDrop.SmokeLevel(0)==1&&NativePodDrop.SmokeLevel(20)>.2f&&NativePodDrop.SmokeLevel(20)<.6f&&NativePodDrop.SmokeLevel(NativePodDrop.SmokeSeconds)==0,"the smoke thins away over 40 s");
        var hot=NativePodDrop.HullColor(1);var cold=NativePodDrop.HullColor(0);var cherry=NativePodDrop.HullColor(.5f);
        Check(hot.X>2&&hot.Y>1.5f&&cold.X<.1f&&cold.Y<.1f&&cherry.X>1&&cherry.Y<.3f,"white hot, cherry, black");
        // what lasts is the record's and the clock's: a late joiner's fresh plan draws the same crater and the same hull
        foreach(float since in new[]{20f,40f})
        {
            var a=NativePodDrop.Evaluate(p.Impact+since,P(7,2,9,4,300.1,135,5));var b=NativePodDrop.Evaluate(p.Impact+since,p);
            Check(a.Hull==b.Hull&&a.Smoke==b.Smoke&&a.Offset==b.Offset&&a.Tilt==b.Tilt&&a.Door==b.Door,"+"+since+" s: a late joiner sees the same hull, smoke and rest");
        }
        Check(NativePodDrop.Evaluate(p.Impact+20,p).Hull==0&&NativePodDrop.Evaluate(p.Impact+40,p).Smoke==0,"black by +20 s, the smoke gone by +40 s");
    }
    private static void Effects()
    {
        var p=P(3,0,-3,-2,120.25,0,5);var centre=new Vector3(-3,0,-2);
        var r1=new Recorder();var r2=new Recorder();
        foreach(float t in new[]{-4.6f,-3.9f,-3.4f,-3f,p.Impact+.05f,p.Impact+.3f,p.DoorAt,p.DoorAt+.2f,-.2f,5f,20f}){NativePodDrop.Effects(r1,NativePodDrop.Evaluate(t,p),p,centre,1,0);NativePodDrop.Effects(r2,NativePodDrop.Evaluate(t,p),p,centre,1,0);}
        Check(r1.Calls.Count>200&&r1.Calls.SequenceEqual(r2.Calls),"the effects are the same each time ("+r1.Calls.Count+" calls)");
        int Of(float t,char kind,float q=1){var r=new Recorder();NativePodDrop.Effects(r,NativePodDrop.Evaluate(t,p),p,centre,q,0);return r.Calls.Count(c=>c[0]==kind);}
        Check(Of(-4.2f,'s')>=6,"a burning trail on the way in");Check(Of(-4.0f,'m')>=10,"a contrail left in the sky");
        Check(Of(p.Punch+.3f,'m')>=10,"the cloud punch's vapour ring");
        Check(Of(p.RetroAt+.1f,'s')>=4,"the retros");
        Check(Of(p.Impact+.15f,'m')>=20&&Of(p.Impact+.15f,'s')>=20&&Of(p.Impact+.05f,'g')>=2,"the impact: dust, sparks and a flash");
        Check(Of(p.DoorAt-.02f,'g')>=4&&Of(p.DoorAt+.1f,'m')>=6,"the bolts flash and the door's blow vents");
        Check(Of(15,'m')>=3,"smoke off the hull long after");Check(Of(45,'m')==0&&Of(45,'g')==0&&Of(45,'s')==0,"quiet once cool and clear");
        Check(Of(p.Impact+.15f,'m',.3f)<Of(p.Impact+.15f,'m'),"Reduced effects thin the dust");
        var gone=new Recorder();NativePodDrop.Effects(gone,NativePodDrop.Evaluate(p.Appear-.1f,p),p,centre,1,0);Check(gone.Calls.Count==0,"nothing before the streak");
        // the meteor: an unfogged ribbon far off, none close to the eye, nothing after the touchdown
        var far=new Sink();NativePodDrop.Meteor(far,NativePodDrop.Evaluate(-4.5f,p),p,centre,new Vector3(0,2,-10),1);Check(far.Vertices>=30&&far.Triangles>=30&&far.Finite,"a meteor streak over the city");
        var close=new Sink();var cf=NativePodDrop.Evaluate(-4.5f,p);NativePodDrop.Meteor(close,cf,p,centre,centre+cf.Offset+new Vector3(0,3,-8),1);Check(close.Vertices==0,"no unfogged streak close to the eye");
        var down=new Sink();NativePodDrop.Meteor(down,NativePodDrop.Evaluate(p.Impact+.1f,p),p,centre,new Vector3(0,2,-30),1);Check(down.Vertices==0,"no streak once down");
    }
    private static void Shots()
    {
        var p=P(3,1,-1,-2,120.25,0,5);var centre=new Vector3(-1,0,-2);var gameEye=centre+new Vector3(0,1.62f,0);
        foreach(int mode in new[]{NativePodDrop.ShotChase,NativePodDrop.ShotFirst,NativePodDrop.ShotStill})
        {
            PodShot last=default;bool first=true;
            for(float t=p.Appear;t<=0;t+=1/60f)
            {
                var s=NativePodDrop.Shot(p,centre,t,mode,gameEye);Check(float.IsFinite(s.Eye.X+s.Eye.Y+s.Eye.Z+s.Look.X+s.Look.Y+s.Look.Z+s.Fov)&&Vector3.Distance(s.Eye,s.Look)>.5f&&s.Fov>40&&s.Fov<90,"a valid shot ("+mode+", "+t+")");
                Check(s.Directed>=0&&s.Directed<=1&&(t<-p.HandOff-1e-3f?s.Directed==1:true),"directed until the hand-off");
                if(mode!=NativePodDrop.ShotStill&&!s.Cabin)Check(s.Eye.Y>(t<p.ArcFrom?3.4f:1.2f),"the camera stays clear of the fence ("+mode+", "+t+", "+s.Eye.Y+")");
                if(mode==NativePodDrop.ShotStill)Check(Math.Abs(s.Eye.X)<=NativePodDrop.PlazaHalf+1e-3f&&Math.Abs(s.Eye.Z)<=NativePodDrop.PlazaHalf+1e-3f&&s.Eye.Y>1.5f,"Reduced effects: the still shot stands on the plaza, inside the fence");
                // the still shot holds still until it glides behind the pilot at the end
                if(mode==NativePodDrop.ShotStill&&!first&&t<p.ArcFrom)Check(Vector3.Distance(s.Eye,last.Eye)<1e-4f&&Vector3.Distance(s.Look,last.Look)<1e-4f,"Reduced effects: a still shot");
                // the chase never jumps except at its cuts
                if(mode==NativePodDrop.ShotChase&&!first&&t>p.Ride+.02f&&t<p.ArcTo)Check(Vector3.Distance(s.Eye,last.Eye)<5,"the chase moves smoothly ("+t+")");
                last=s;first=false;
            }
            Check(NativePodDrop.Shot(p,centre,0,mode,gameEye).Directed<1e-4f,"the game's camera has it back at GO");
        }
        Check(NativePodDrop.Shot(p,centre,p.Ride+.1f,NativePodDrop.ShotFirst,gameEye).Cabin&&!NativePodDrop.Shot(p,centre,p.Ride+.1f,NativePodDrop.ShotChase,gameEye).Cabin&&!NativePodDrop.Shot(p,centre,p.CabinEnd+.05f,NativePodDrop.ShotFirst,gameEye).Cabin,"first person: one braced second in the cabin, then the chase");
        var stop=NativePodDrop.Shot(p,centre,p.Impact,NativePodDrop.ShotChase,gameEye);Check(Math.Abs(stop.Eye.Y-10)<.05f,"the chase stops 10 m up as the pod punches in ("+stop.Eye.Y+")");
        Check(Vector3.Distance(NativePodDrop.Shot(p,centre,p.ArcTo+.01f,NativePodDrop.ShotFirst,gameEye).Eye,gameEye)<1e-3f,"first person: the arc ends in my pilot's eyes");
        // the shake: the crash's own by distance, mine harder; none far off
        Check(NativePodDrop.Shake(p,p.Impact+.05f,4,false)>0&&NativePodDrop.Shake(p,p.Impact+.05f,30,false)==0&&NativePodDrop.Shake(p,p.Impact+.05f,4,true)>NativePodDrop.Shake(p,p.Impact+.05f,4,false),"the slam shakes nearby eyes, mine hardest");
        Check(NativePodDrop.Shake(p,p.Impact+2,4,false)==0&&NativePodDrop.Shake(p,p.Punch+.05f,40,true)>0,"the cloud punch shakes my ride; the slam's shake is over in a second");
        Check(Math.Abs(NativePodDrop.ShakeNoise(5,1,3.3f))<=1&&NativePodDrop.ShakeNoise(5,1,3.3f)==NativePodDrop.ShakeNoise(5,1,3.3f),"the shake's noise is bounded and repeatable");
    }
    private static void Launch()
    {
        var p=P(3,0,-3,-2,120.25,0,5);
        var at0=NativePodDrop.Launch(0,p);Check(at0.Dropping&&Math.Abs(at0.Door-p.RestDoor)<1e-4f&&at0.Offset==NativePodDrop.RestOffset(p),"boarding: the door still open");
        var shut=NativePodDrop.Launch(NativePodDrop.LaunchClose+.01f,p);Check(shut.Door<1e-3f&&shut.Offset==NativePodDrop.RestOffset(p),"the door shuts before it lifts");
        float last=-1;for(float t=NativePodDrop.LaunchIgnite;t<NativePodDrop.LaunchGone;t+=.05f){var f=NativePodDrop.Launch(t,p);Check(f.Offset.Y>=last&&f.Retro>.99f,"it lifts away on its retros");last=f.Offset.Y;}
        Check(last>20&&NativePodDrop.Launch(NativePodDrop.LaunchGone,p).Gone,"and is gone");
        Check(!NativePodDrop.Launch(float.NaN,p).Dropping&&NativePodDrop.Launch(float.NaN,p).Door==p.RestDoor,"no launch: parked");
        Check(NativePodDrop.LaunchGone==3.4f,"LaunchGone stays Astra's 3.4 s");
    }
    private static void Sounds()
    {
        var p=P(3,2,1,-2,120.25,0,5);var cues=new NativePodSounds.Cue[NativePodSounds.MaxCues];int n=NativePodSounds.Plan(p,cues);
        Check(n>12&&n<=NativePodSounds.MaxCues,"a pod plans its sounds ("+n+")");
        for(int i=1;i<n;i++)Check(cues[i].At>=cues[i-1].At,"in time order");
        bool Has(int sound,float at)=>cues.Take(n).Any(c=>c.Sound==sound&&Math.Abs(c.At-at)<1e-4f);
        Check(Has(NativePodSounds.Roar,p.Appear+.05f)&&Has(NativePodSounds.Boom,p.Punch)&&Has(p.Fails?NativePodSounds.Sputter:NativePodSounds.Retro,p.RetroAt)&&Has(NativePodSounds.Impact,p.Impact)&&Has(NativePodSounds.Bolts,p.DoorAt-NativePodDrop.BoltLead)&&Has(NativePodSounds.Door,p.DoorAt+p.DoorSeconds),"the roar, the boom, the retros, the impact, the bolts, the door, each on its beat");
        Check(cues.Take(n).Count(c=>c.Sound==NativePodSounds.Tick)>=6&&cues.Take(n).Where(c=>c.Sound==NativePodSounds.Tick).All(c=>c.At>p.Impact&&c.At<p.Impact+NativePodDrop.CoolSeconds),"the hull ticks as it cools");
        Check(NativePodSounds.Keys.Length==NativePodSounds.Count&&NativePodSounds.Keys.Distinct().Count()==NativePodSounds.Count,"one key per sound");
        for(int i=0;i<NativePodSounds.Count;i++)
        {
            var a=NativePodSounds.Samples(i);var b=NativePodSounds.Samples(i);float peak=a.Max(x=>Math.Abs(x));
            Check(a.Length==(int)Math.Ceiling(NativePodSounds.Duration(i)*NativePodSounds.SampleRate)&&a.SequenceEqual(b)&&a.All(float.IsFinite)&&peak<=1&&peak>.05f,"sound "+NativePodSounds.Keys[i]+" is original, repeatable and audible ("+peak+")");
        }
    }
    private static void Look()
    {
        Check(NativePodDrop.Normalize("")==""&&NativePodDrop.Normalize(null)=="","no look: the default");
        Check(NativePodDrop.Normalize("light:00FF00, paint:ff0000,bogus:123456,trim:zzzzzz,paint:0000ff")=="paint:0000ff,light:00ff00","the look is canonical: known entries, valid hex, the last wins, in order");
        var l=NativePodDrop.With("","trim",0x112233);Check(l=="trim:112233"&&NativePodDrop.Of(l,"trim")==0x112233&&NativePodDrop.Of(l,"paint")==-1,"With sets one entry");
        Check(NativePodDrop.With(l,"trim",-1)==""&&NativePodDrop.With(l,"nope",5)==l,"With -1 goes back to Auto; an unknown key changes nothing");
        NativePodDrop.Colors("","#123456","#abcdef",out int p,out int t,out int g);Check(p==0x123456&&t==NativePodDrop.DefaultTrim&&g==0xabcdef,"Auto: the body colour, gunmetal, the accent");
        NativePodDrop.Colors("paint:ff0000,light:00ff00","#123456","junk",out p,out t,out g);Check(p==0xff0000&&g==0x00ff00&&t==NativePodDrop.DefaultTrim,"picked colours win");
        NativePodDrop.Colors("",null,null,out p,out t,out g);Check(p==0xe5e7eb&&g==0x70e1d0,"no seat colours: the defaults");
        Check(NativePodDrop.Paints.Length>=12&&NativePodDrop.Paints.Distinct().Count()==NativePodDrop.Paints.Length,"twelve distinct paints");
        Check(NativePodDrop.Keys.SequenceEqual(new[]{"paint","trim","light"}),"the look's save keys are unchanged");
    }
    private static void Sign()
    {
        Check(NativePodDrop.SignText("  sheldon ")=="SHELDON"&&NativePodDrop.SignText("")=="PILOT"&&NativePodDrop.SignText(null)=="PILOT","upper case, trimmed, PILOT when empty");
        Check(NativePodDrop.SignText("ñame~1")=="?AME?1"&&NativePodDrop.SignText(new string('a',40)).Length==NativePodDrop.MaxName,"letters the font lacks read '?'; at most twenty");
        foreach(char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.!?'#*+/: ")Check(NativePodDrop.SignText(c.ToString()).Length==1||c==' ',"the font has '"+c+"'");
        Check(NativePodDrop.Lit("A",2,0)&&!NativePodDrop.Lit("A",0,0)&&NativePodDrop.Lit("A",0,3)&&!NativePodDrop.Lit("A",5,3)&&!NativePodDrop.Lit("A",-1,3)&&!NativePodDrop.Lit("A",2,7),"A's dots, the gap column and the edges");
        Check(!NativePodDrop.Scrolls("SHELDON")&&NativePodDrop.Scrolls("ABCDEFGHIJK")&&NativePodDrop.TextColumns("AB")==11,"ten letters fit, eleven scroll");
        var px=NativePodDrop.SignPixels("HI",8,out int w,out int h);Check(w==NativePodDrop.SignRows*8&&h==NativePodDrop.SignColumns*8&&px.Length==w*h*4,"the sign texture: rows across, the sign's length along");
        var lp=NativePodDrop.SignPixels("ABCDEFGHIJKL",8,out _,out int lh);Check(lh==(NativePodDrop.TextColumns("ABCDEFGHIJKL")+12)*8,"a long name's texture is its text and a gap (it scrolls round)");
        var flat=NativePodDrop.SignPixelsFlat("HI",4,out int fw,out int fh);Check(fw==NativePodDrop.SignColumns*4&&fh==NativePodDrop.SignRows*4&&flat.Length==fw*fh*4,"the Locker card's sign lies along x");
        int Bright(byte[] b){int n=0;for(int i=3;i<b.Length;i+=4)if(b[i]>200)n++;return n;}
        Check(Bright(px)>0&&Bright(NativePodDrop.SignPixels("I",8,out _,out _))<Bright(NativePodDrop.SignPixels("M",8,out _,out _)),"lit dots follow the letters");
    }
    private static void Models()
    {
        var built=new List<(string,Func<WeaponMesh>)>{("pod",NativePodModels.Pod),("door",NativePodModels.Door),("furrow",NativePodModels.Furrow)};
        for(int k=0;k<NativePodModels.CraterShapes;k++){int shape=k;built.Add(("crater "+k,()=>NativePodModels.Crater(shape)));}
        foreach(var (name,build) in built)
        {
            var a=build();var b=build();int tris=a.Sections.Sum(s=>s.Indices.Count/3);
            Check(a.Sections.Count==b.Sections.Count&&a.Sections.Zip(b.Sections,(x,y)=>x.Positions.SequenceEqual(y.Positions)&&x.Indices.SequenceEqual(y.Indices)).All(x=>x),name+" builds the same each time");
            Check(tris>50&&tris<9000,name+" stays in budget ("+tris+" triangles)");
            foreach(var s in a.Sections)
            {
                Check(s.Bone>=0&&s.Bone<=NativePodModels.Harness||s.Bone==NativePodModels.Glass,name+" bone "+s.Bone+" is a pod part");
                Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),name+" section arrays agree");
                if(s.Bone==NativePodModels.Paint)Check(s.Colors.All(c=>c.X>.7f&&c.Y>.7f&&c.Z>.7f),name+"'s paint is near white (the draw tints it)");
                if(s.Bone==NativePodModels.Lights||s.Bone==NativePodModels.Cabin||s.Bone==NativePodModels.Retro||s.Bone==NativePodModels.Heat||s.Bone==NativePodModels.Bolts)Check(s.Emission.All(e=>e.X+e.Y+e.Z>0),name+"'s lit parts glow");
            }
        }
        var pod=NativePodModels.Pod();var all=pod.Sections.SelectMany(s=>s.Positions).ToArray();
        Check(NativePodModels.Radius<=1.17f&&NativePodModels.Height<=3&&all.All(p=>new Vector2(p.X,p.Z).Length()<NativePodModels.Radius+.05f&&p.Y>-.08f&&p.Y<NativePodModels.Height+.05f),"the pod stays inside its footprint (r 1.17 or less) and height (3.0 or less)");
        foreach(int bone in new[]{NativePodModels.Paint,NativePodModels.Trim,NativePodModels.Lights,NativePodModels.Cabin,NativePodModels.Retro,NativePodModels.Heat,NativePodModels.Bolts,NativePodModels.Harness})Check(pod.Sections.Any(s=>s.Bone==bone),"the pod has bone "+bone+" to tint or turn");
        // the doorway: nothing of the hull in the door's opening (x within the door, from the sill up, toward +z)
        Check(!pod.Sections.Where(s=>s.Bone==NativePodModels.Paint||s.Bone==NativePodModels.Heat).SelectMany(s=>s.Positions).Any(p=>Math.Abs(p.X)<NativePodModels.DoorWidth/2-.05f&&p.Y>NativePodModels.Hinge.Y+.1f&&p.Y<NativePodModels.Hinge.Y+NativePodModels.DoorHeight-.1f&&p.Z>.5f),"the hull leaves the doorway open");
        var doorMesh=NativePodModels.Door();var door=doorMesh.Sections.SelectMany(s=>s.Positions).ToArray();Check(door.All(p=>p.Y>=-.04f&&p.Y<=NativePodModels.DoorHeight+.01f&&Math.Abs(p.X)<=NativePodModels.DoorWidth/2+.01f),"the door stands up from its hinge, its width");
        Check(doorMesh.Sections.Any(s=>s.Bone==NativePodModels.Glass&&s.Positions.All(p=>Math.Abs(p.Y-NativePodModels.PortholeY)<=NativePodModels.PortholeHalf+.01f)),"a porthole of glass in the door");
        Check(!doorMesh.Sections.Where(s=>s.Bone==NativePodModels.Paint).SelectMany(s=>s.Indices.Select(i=>s.Positions[i])).Any(p=>Math.Abs(p.X)<NativePodModels.PortholeHalf-.01f&&Math.Abs(p.Y-NativePodModels.PortholeY)<NativePodModels.PortholeHalf-.01f&&Math.Abs(p.Z)<.03f),"the plate leaves the porthole open");
        // opened on a level floor, the door lies outward as a ramp, its top on the ground
        var top=Vector3.Transform(new Vector3(0,NativePodModels.DoorHeight,0),Matrix4x4.CreateRotationX(NativePodModels.OpenAngle))+NativePodModels.Hinge;Check(top.Z>NativePodModels.Hinge.Z+1.4f&&Math.Abs(top.Y)<.08f,"open, the door rests on the ground as a ramp ("+top.Y+")");
        Check(NativePodModels.SignCentre.Z>.84f&&NativePodModels.SignCentre.Y>NativePodModels.Hinge.Y+NativePodModels.DoorHeight,"the sign sits over the door, on the marquee's face");
        // the harness bar: over the chest when down, out of the doorway and inside the nose when up
        var bar=pod.Sections.Where(s=>s.Bone==NativePodModels.Harness).SelectMany(s=>s.Positions).ToArray();var hinge=NativePodModels.HarnessHinge;
        var upBar=bar.Select(p=>Vector3.Transform(p-hinge,Matrix4x4.CreateRotationX(NativePodModels.HarnessUp))+hinge).ToArray();
        Check(bar.Any(p=>p.Y<1.45f&&p.Z>.1f)&&upBar.All(p=>p.Y>NativePodModels.Hinge.Y+NativePodModels.DoorHeight-.05f||p.Z<.3f)&&upBar.All(p=>new Vector2(p.X,p.Z).Length()<.62f),"the harness bar crosses the chest, then lifts clear of the doorway inside the hull");
        // the marks: flat paint (at most 6 cm, off the floor), facing up, a crater about 5 m across and its furrow
        foreach(var mark in Enumerable.Range(0,NativePodModels.CraterShapes).Select(NativePodModels.Crater).Concat(new[]{NativePodModels.Furrow()}))
        {
            var pts=mark.Sections.SelectMany(s=>s.Positions).ToArray();
            Check(pts.All(p=>p.Y<=NativePodModels.FlatHeight+1e-4f&&p.Y>=NativePodModels.FlatLift-1e-3f),mark.Spec+" is flat paint off the floor (y "+pts.Min(p=>p.Y)+".."+pts.Max(p=>p.Y)+")");
            int tris=mark.Sections.Sum(s=>s.Indices.Count/3),down=mark.Sections.Sum(s=>Enumerable.Range(0,s.Indices.Count/3).Count(i=>s.Normals[s.Indices[i*3]].Y<-.2f));
            Check(down<=tris*.15f&&mark.Sections.Sum(s=>Enumerable.Range(0,s.Indices.Count/3).Count(i=>s.Normals[s.Indices[i*3]].Y>.2f))>=tris*.6f,mark.Spec+" faces up ("+down+" of "+tris+" down)");
            if(mark.Spec!=NativePodModels.FurrowId){float r=pts.Max(p=>new Vector2(p.X,p.Z).Length());Check(r>2.4f&&r<3.9f,mark.Spec+" is about 5 m across, scorch past it ("+r+")");}
            else Check(pts.All(p=>p.Z<=1e-3f&&p.Z>=-1.001f&&Math.Abs(p.X)<=.56f),"the furrow is a unit long behind its origin");
        }
        Check(NativePodModels.CraterIds.Length==NativePodModels.CraterShapes&&NativePodModels.CraterIds.Distinct().Count()==NativePodModels.CraterShapes,"one model id per crater shape");
    }
    private static void Wiring(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        var spawn=R("Runtime/NativeSpawnInPresentation.cs");var pods=R("Runtime/NativePodPresentation.cs");var cine=R("Runtime/NativeCinematicCamera.cs");var game=R("Runtime/ConversionGame.cs");
        Check(spawn.Contains("if(PodRun(arena))return PodSpawn(arena,pilot,feet,age,NativeSpawnIn.End(m.Phase,m.PhaseEnd,m.FightAt),out raise);"),"a pod run's pilots take the pod path");
        Check(spawn.Contains("||v.Pod)return;var f=v.Frame;bool slice")&&spawn.Contains("if(!v.Shown||v.Arena!=arena||v.Pod)continue;")&&spawn.Contains("||!v.Shown||v.Pod)return false;")&&spawn.Contains("look.Ghost=holo&&!v.Pod"),"no beam, hologram or veil for a pod");
        Check(spawn.Contains("if(v.Pod)return;"),"the pod's ride is the directed camera's, not the spawn-in's kick");
        Check(spawn.Contains("if(v.Pod){position=PodFeet(a,v,position);return position;}"),"the drawn pilot rides the pod's full offset (not only its height)");
        Check(pods.Contains("v.Frame=new SpawnInFrame{Active=true,Age=age,Built=f.Shown?1:0,Front=f.Shown?1e4f:-1e4f,Held=true,Land=-1};")&&spawn.Contains("return v.Frame.Held||!v.Frame.Falling&&replicated;"),"strapped in, the pilot is posed standing (no Fall loop), held through GO");
        Check(pods.Contains("if(f.Age>=PodSettle||a.Grounded&&Mathf.Abs(position.y-rest.y)<.015f){v.Shown=false;return position;}"),"after GO the drawn pilot waits on its capsule until it has settled (no snap, no Land take)");
        Check(R("Runtime/NativeFootfallPresentation.cs").Contains("if(PodHoldsSteps(a.Id)){stepTrackers.Remove(a.Id);return;}"),"no steps in the pod and no landing thud at GO on a pod run");
        Check(cine.Contains("if(presentation.PodDropDirect(arena,view,out var podHand)){Directing=PodDrop=true;siren.Stop();return true;}")&&cine.Contains("public void PodHandOff(Camera view)")&&pods.Contains("if(m.Phase!=NativePhase.Countdown"),"the directed drop is a Countdown branch of the cinematic camera, handing back at GO");
        Check(cine.IndexOf("presentation.PodDropDirect(",StringComparison.Ordinal)<cine.IndexOf("presentation.BackroomsBossPreviewShot(",StringComparison.Ordinal),"it directs before the fight-only shots (the countdown is not their phase)");
        Check(pods.Contains("ConversionGame.CaptureEye.HasValue)return false;"),"a capture's fixed eye wins over the directed drop");
        Check(game.Contains("cinematic.PodHandOff(cameraView);")&&game.Contains("if(cinematic.PodDrop){if(!menu)CountdownOverlay();}"),"the hand-off and the countdown over the directed drop");
        Check(pods.Contains("bool reduced=WeaponEffectsQuality<.5f,shake=CameraShake&&!reduced;")&&pods.Contains("int mode=reduced?NativePodDrop.ShotStill:FirstPersonPick?NativePodDrop.ShotFirst:NativePodDrop.ShotChase;")&&game.Contains("presentation.FirstPersonPick=prefs.firstPerson;"),"the directed drop honours Camera shake, Reduced effects and the player's camera");
        Check(pods.Contains("p.Facing=new Vector3(Mathf.Sin(record.yaw*Mathf.Deg2Rad),0,Mathf.Cos(record.yaw*Mathf.Deg2Rad));")&&pods.Contains("Quaternion.Euler(0,pilot.Yaw,0)*Vector3.forward")&&!pods.Contains("Mathf.Sin(record.yaw)")&&!pods.Contains("pilot.Yaw*Mathf.Rad2Deg"),"the pods face the record's yaw in degrees");
        Check(R("Runtime/ArenaPresentation.cs").Contains("ReviveMachineTumble(rig,a);PodRideTilt(rig,a);"),"the drawn pilot leans with the pod");
        Check(R("Runtime/NativeMachineModels.cs").Contains("Add(NativePodModels.PodId,NativePodModels.Pod());Add(NativePodModels.DoorId,NativePodModels.Door());")&&R("Runtime/NativeMachineModels.cs").Contains("Flat(NativePodModels.CraterIds[k],NativePodModels.Crater(k));")&&R("Runtime/NativeMachineModels.cs").Contains("Flat(NativePodModels.FurrowId,NativePodModels.Furrow());"),"the bank builds the pod, its door and the crash's marks");
        Check(R("Runtime/NativeSourcePresentation.cs").Contains("ExpeditionPods(arena);")&&R("Runtime/ArenaPresentation.cs").Contains("SpawnInFx(arena,camera);PodFx(arena,camera);"),"the pods and their effects are drawn");
        Check(R("Runtime/NativeBlastPresentation.cs").Contains("var lit=PodFlashes!=null?PodFlashes(sim.Flashes):sim.Flashes;"),"the crash's flash lights the city through the blast lights (they survive DARKNESS)");
        Check(game.Contains("nativeAudio?.PodFrame(presentation.PodCues,cameraView!=null?cameraView.transform:null);")&&R("Runtime/NativePodAudio.cs").Contains("NativePodSounds.Samples("),"the pods are heard");
        Check(game.Contains("presentation.LocalPodLook=prefs.pod;")&&game.Contains("public string pod=\"\";"),"my pod wears the Locker's look (prefs.pod)");
        Check(pods.Contains("podBlock??=new MaterialPropertyBlock();")&&pods.Contains("if(f.Sign<=.01f||pod.Sign==null||opacity<.99f||!EnsurePodSign())return;")&&pods.Contains("ghost?NativePodModels.GhostId:NativePodModels.PodId")&&R("Runtime/NativeMachineModels.cs").Contains("Ghost(NativePodModels.GhostId,NativePodModels.Pod());"),"the sign.s property block is made on first use; my faded pod is the glass copy and hides its sign");
        Check(R("Runtime/NativeLockerUI.cs").Contains("case NativeLocker.Pod:LkPodLayout();break;")&&R("Runtime/NativeLockerUI.cs").Contains("case NativeLocker.Pod:LkPod(k,e);break;"),"the Locker lays out and draws the Pod tab");
        // No field initializer of the presentation component (ArenaPresentation, a MonoBehaviour) makes a Unity object: Unity
        // refuses it in the constructor and leaves the component half built (a MaterialPropertyBlock did, 2026-11-02).
        var made=new Regex(@"^ {8}(private|public|internal|protected)[^;(=]*\b\w+\s*=\s*new (MaterialPropertyBlock|Material|Mesh|Texture2D|RenderTexture|GameObject)\(");
        foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            bool inside=false;foreach(var line in File.ReadLines(file))
            {
                if(Regex.IsMatch(line,@"^    \S.*\bclass\s+\w+"))inside=Regex.IsMatch(line,@"\bclass\s+ArenaPresentation\b");
                if(inside)Check(!made.IsMatch(line),Path.GetFileName(file)+": no Unity object made in an ArenaPresentation field initializer ("+line.Trim()+")");
            }
        }
        foreach(var f in new[]{"Core/NativePodDrop.cs.meta","Core/NativePodModels.cs.meta","Core/NativePodSounds.cs.meta","Core/NativeExpeditionHubPlan.cs.meta","Runtime/NativePodPresentation.cs.meta","Runtime/NativePodAudio.cs.meta","Runtime/NativeLockerPodUI.cs.meta"}){var t=R(f);Check(t.Contains("guid: ")&&!t.Contains(" \n"),"meta "+f);}
    }
}
