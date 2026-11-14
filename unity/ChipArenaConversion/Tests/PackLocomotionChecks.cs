using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The melee travel carry from the owner's pack (owner 2026-10-12: "the arms kind of wiggle around when running. Make it look
/// more natural when moving~ Refer to the movement in our animation pack"; Core/NativeLocomotionCarry,
/// NativePackLocomotion.Generated): the exported loops match tools/pack-locomotion-provenance.json; the eight-way direction
/// bracket; for every melee weapon at a walk, run, strafe both ways, backpedal (walking and running) and a diagonal the hands
/// hold a steady carry (their per-stride travel and acceleration far under the captured walk and run's) while the steady
/// torso stays within MeleeFeelChecks' bounds and at most a degree and a half over today's values in any one component; the
/// legs are exactly the gait's (no skating added); no pop through a full turn of the travel direction or a speed ramp; no
/// allocation per frame; the hooks. Presentation only.
/// </summary>
internal static class PackLocomotionChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI;
    private static readonly string[] Specs={"sword","daggers","shield","greatsword"};
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Pack locomotion: "+what);}
    public static void Run(string root)
    {
        var log=new List<string>();Clips(root,log);Bracket(log);Travel(log);Continuity(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS pack locomotion: "+line);
    }
    private static void Clips(string root,List<string> log)
    {
        var names=NativePackLocomotion.Names;var slots=NativePackLocomotion.Slots;
        Check(string.Join(",",names)=="sword,daggers,greatsword,shield"&&NativeLocoRules.SetFor("sword")==0&&NativeLocoRules.SetFor("daggers")==1&&NativeLocoRules.SetFor("greatsword")==2&&NativeLocoRules.SetFor("shield")==3&&NativeLocoRules.SetFor("pistol")==NativeLocoRules.None,"four melee sets in NativeLocoRules order");
        Check(slots.Length==17&&slots[NativeLocoRules.Slow]=="slow"&&slots[NativeLocoRules.Walk]=="walk-forward"&&slots[NativeLocoRules.Walk+2]=="walk-left"&&slots[NativeLocoRules.Run+4]=="run-backward"&&slots[NativeLocoRules.Run+6]=="run-right","slots: slow, then walk and run in eight directions counter-clockwise from ahead");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-locomotion-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())prov[c.GetProperty("clip").GetString()]=c;
        Check(prov.Count==names.Length*slots.Length,"the provenance lists every loop");int frames=0;
        for(int s=0;s<names.Length;s++)for(int k=0;k<slots.Length;k++)
        {
            var c=NativePackLocomotion.Sets[s][k];string key=names[s]+"/"+slots[k];Check(c!=null&&c.Name==key&&c.Loop&&NativeLocoRules.Clip(s,k)==c,key+" in place, looping");
            var p=prov[key];Check(p.GetProperty("frames").GetInt32()==c.Frames&&p.GetProperty("fps").GetInt32()==c.Fps&&p.GetProperty("source").GetString()==c.Source,key+": size and source match the provenance");
            Check(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant()==p.GetProperty("sha256").GetString(),key+": track is the exporter's output");
            Check(c.Marks.Length==1&&c.Marks[0]>=0&&c.Marks[0]<=c.Duration+1e-4f,key+": its left-foot strike lies in the cycle");
            foreach(var v in c.Decode())Check(MathUtil.Finite(v)&&Math.Abs(v)<4,key+": finite, bounded channels");
            frames+=c.Frames;
        }
        Check(NativePackLocomotion.Sets[3][NativeLocoRules.Run+2].Source=="Armed-Shield-Run-Right (mirrored)"&&NativePackLocomotion.Sets[0][NativeLocoRules.Run+2].Source=="Armed-Run-Left"&&NativePackLocomotion.Sets[0][NativeLocoRules.Walk+4].Source=="Armed-Strafe-Backward"&&NativePackLocomotion.Sets[2][NativeLocoRules.Slow].Source=="2Hand-Sword-Walk-Slow","the mirrored shield swaps its left and right clips; the walk family is Walk and the strafes");
        log.Add(names.Length*slots.Length+" loops ("+frames+" frames: Walk-Slow, Walk and seven strafes, the run in eight directions, per melee weapon) match the exporter's provenance");
    }
    private static void Bracket(List<string> log)
    {
        void Is(float angle,int a,int b,float t){NativeLocoRules.Bracket(angle,out int x,out int y,out float u);Check(x==a&&y==b&&Math.Abs(u-t)<1e-4f,"bracket at "+angle+": "+x+","+y+","+u);}
        float q=(float)Math.PI/4;Is(0,0,1,0);Is(q*.5f,0,1,.5f);Is(2*q,2,3,0);Is((float)Math.PI,4,5,0);Is(-q,7,0,0);Is(-2*q,6,7,0);Is(-q*.25f,7,0,.75f);
        log.Add("the travel direction brackets between the two nearest of eight pack directions (ahead 0, the rig's left +)");
    }
    private sealed class Pilot
    {
        public readonly MeleeMotion Motion=new MeleeMotion{Impacts=new MeleeImpacts()};public readonly MeleeView View;public Vector3 Position;private float last=-100;
        public Pilot(string spec,bool pack){View=new MeleeView{Id="loco-"+spec,Spec=spec};if(pack)Motion.Locomotion=new NativeLocomotionCarry();}
        public void Pose(float t){float dt=last<-90?Dt:t-last;last=t;var r=Motion.Rig;r.GroupPosition=Position;r.GroupYaw=Motion.PresentedYaw(View,t,View.Yaw);Motion.Sample(View,t,dt);}
        public Quaternion Local(int bone)=>Motion.Rig.ChainQuat(bone,-1);public Vector3 Point(int bone)=>Motion.Rig.ChainPoint(bone,-1);
        public Vector3 InChest(int bone)=>Vector3.Transform(Point(bone)-Point(CapturedRig.Chest),Quaternion.Inverse(Local(CapturedRig.Chest)));
    }
    private sealed class Range{public float Min=float.MaxValue,Max=float.MinValue;public void Add(float v){Min=Math.Min(Min,v);Max=Math.Max(Max,v);}public float PP=>Max-Min;}
    private struct Gait{public float Yaw,Roll,Nod,Sway,Hand,Accel;public float LegError;}
    private static Vector3 YawPitchRoll(Quaternion q)
    {Vector3 f=Vector3.Transform(Vector3.UnitZ,q),r=Vector3.Transform(Vector3.UnitX,q);return new Vector3((float)Math.Atan2(f.X,f.Z)*Deg,(float)Math.Asin(Math.Clamp(-f.Y,-1,1))*Deg,(float)Math.Asin(Math.Clamp(r.Y,-1,1))*Deg);}
    private static readonly int[] Lower={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.LToe,CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot,CapturedRig.RToe};
    // MeleeFeelChecks' steady travel (same frames, same measures), plus the hands' travel in chest space and their acceleration.
    private static Gait Measure(string spec,float speed,float dir,bool pack,Pilot twin=null)
    {
        var p=new Pilot(spec,pack);var yaw=new Range();var roll=new Range();var nod=new Range();var sway=new Range();var hx=new Range[6];for(int i=0;i<6;i++)hx[i]=new Range();
        Vector3? r1=null,r2=null,l1=null,l2=null;double accel=0;int n=0;float leg=0;
        for(int f=0;f<270;f++)
        {
            float t=1+f*Dt;var vel=new Vector3((float)Math.Sin(dir),0,(float)Math.Cos(dir))*speed;p.Position+=vel*Dt;p.View.Vel=vel;p.Pose(t);
            if(twin!=null){twin.Position=p.Position;twin.View.Vel=vel;twin.Pose(t);foreach(int b in Lower)leg=Math.Max(leg,Vector3.Distance(p.Point(b),twin.Point(b)));}
            if(f<90)continue;
            var c=YawPitchRoll(p.Local(CapturedRig.Chest));yaw.Add(c.X);nod.Add(c.Y);roll.Add(c.Z);sway.Add(p.Point(CapturedRig.Head).X*100);
            Vector3 rh=p.InChest(CapturedRig.RHand),lh=p.InChest(CapturedRig.LHand);hx[0].Add(rh.X);hx[1].Add(rh.Y);hx[2].Add(rh.Z);hx[3].Add(lh.X);hx[4].Add(lh.Y);hx[5].Add(lh.Z);
            if(r2.HasValue){accel+=((rh-2*r1.Value+r2.Value)/(Dt*Dt)).Length()+((lh-2*l1.Value+l2.Value)/(Dt*Dt)).Length();n++;}r2=r1;r1=rh;l2=l1;l1=lh;
        }
        float hand=0;foreach(var h in hx)hand=Math.Max(hand,h.PP*100);
        return new Gait{Yaw=yaw.PP,Roll=roll.PP,Nod=nod.PP,Sway=sway.PP,Hand=hand,Accel=(float)(accel/Math.Max(1,n)),LegError=leg};
    }
    private static void Travel(List<string> log)
    {
        var cases=new (string Name,float Speed,float Dir)[]{("walk",3,0),("run",6.8f,0),("strafe left",6.8f,(float)Math.PI/2),("strafe right",6.8f,-(float)Math.PI/2),("backpedal",6.8f,(float)Math.PI),("backpedal walk",2.5f,(float)Math.PI),("walk strafe",3,(float)Math.PI/2),("diagonal",6.8f,(float)Math.PI/4)};
        float worstHand=0,worstAccel=0,worstUp=0,worstLeg=0,sumHand=0,sumAccel=0;string upAt="";int n=0;
        foreach(var spec in Specs)foreach(var c in cases)
        {
            var old=Measure(spec,c.Speed,c.Dir,false);var now=Measure(spec,c.Speed,c.Dir,true,new Pilot(spec,false));string at=spec+" "+c.Name;
            bool walk=c.Speed<4,owner=(spec=="sword"||spec=="daggers")&&c.Name=="run";
            string g=": chest twist "+now.Yaw.ToString("0.0")+", roll "+now.Roll.ToString("0.0")+", nod "+now.Nod.ToString("0.0")+" deg, head sway "+now.Sway.ToString("0.0")+" cm";
            if(owner)Check(now.Yaw<=15&&now.Roll<=6&&now.Nod<=6&&now.Sway<=3,at+g+" (the owner's targets)");
            else if(walk)Check(now.Yaw<=18&&now.Roll<=5&&now.Nod<=4&&now.Sway<=6.5f,at+g+" (walk bounds)");
            else Check(now.Yaw<=15&&now.Roll<=10&&now.Nod<=7.5f&&now.Sway<=6,at+g+" (run bounds)");
            float up=Math.Max(Math.Max(now.Yaw-old.Yaw,now.Roll-old.Roll),Math.Max(now.Nod-old.Nod,(now.Sway-old.Sway)*5));if(up>worstUp){worstUp=up;upAt=at;}
            Check(up<=1.6f,at+": the steady torso stays within 1.6 deg (.3 cm) of today's in every component ("+up.ToString("0.0")+")");
            // The arms hold a steady carry: per-stride hand travel and acceleration far under the captured walk and run's.
            Check(now.Hand<=Math.Max(6,old.Hand*.55f)&&now.Accel<=Math.Max(60,old.Accel*.6f),at+": the hands hold a steady carry ("+now.Hand.ToString("0.0")+" cm, "+now.Accel.ToString("0")+" m/s2 vs "+old.Hand.ToString("0.0")+" cm, "+old.Accel.ToString("0")+" m/s2)");
            Check(now.LegError<1e-5f,at+": the legs are exactly the gait's ("+now.LegError+")");
            worstHand=Math.Max(worstHand,now.Hand/Math.Max(1e-3f,old.Hand));sumHand+=now.Hand/Math.Max(1e-3f,old.Hand);sumAccel+=now.Accel/Math.Max(1e-3f,old.Accel);worstAccel=Math.Max(worstAccel,now.Accel/Math.Max(1e-3f,old.Accel));worstLeg=Math.Max(worstLeg,now.LegError);n++;
        }
        log.Add(n+" travel cases (4 weapons x walk, run, strafe both ways, backpedal and backpedal walk, walk strafe, diagonal): the steady torso within MeleeFeelChecks' bounds, at most "+worstUp.ToString("0.0")+" deg over today's in any component ("+upAt+"); the hands' per-stride travel "+(sumHand/n*100).ToString("0")+"% (at most "+(worstHand*100).ToString("0")+"%) and their acceleration "+(sumAccel/n*100).ToString("0")+"% (at most "+(worstAccel*100).ToString("0")+"%, an already two-handed greatsword walk) of the captured carry's on average; the legs exactly the gait's");
    }
    private static void Continuity(List<string> log)
    {
        float worst=0,worstOld=0;
        foreach(var spec in Specs)foreach(bool pack in new[]{false,true})
        {
            // A full turn of the travel direction at a run, then a ramp down to a stop and back up.
            var p=new Pilot(spec,pack);Vector3? prev=null;float step=0;
            for(int f=0;f<420;f++)
            {
                float t=1+f*Dt,u=f/240f,dir=(float)Math.PI*2*Math.Min(1,u),speed=f<240?6.8f:6.8f*Math.Abs((float)Math.Cos((f-240)/180f*Math.PI));
                var vel=new Vector3((float)Math.Sin(dir),0,(float)Math.Cos(dir))*speed;p.Position+=vel*Dt;p.View.Vel=vel;p.Pose(t);
                Vector3 h=p.InChest(CapturedRig.RHand)+p.InChest(CapturedRig.LHand);if(prev.HasValue&&f>30)step=Math.Max(step,(h-prev.Value).Length()/Dt);prev=h;
            }
            if(pack)worst=Math.Max(worst,step);else worstOld=Math.Max(worstOld,step);
        }
        Check(worst<=worstOld*.7f,"no pop through a full turn of the travel and a speed ramp (hand speed "+worst.ToString("0.0")+" m/s at most, captured "+worstOld.ToString("0.0")+")");
        log.Add("a full turn of the travel direction and a speed ramp: the hands move at most "+worst.ToString("0.0")+" m/s in chest space (captured carry "+worstOld.ToString("0.0")+"): no pop");
    }
    private static void Allocation(List<string> log)
    {
        var p=new Pilot("shield",true);for(int f=0;f<240;f++){float d=f/60f;p.View.Vel=new Vector3((float)Math.Sin(d),0,(float)Math.Cos(d))*6.8f;p.Pose(1+f*Dt);}
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=240;f<600;f++){float d=f/60f;p.View.Vel=new Vector3((float)Math.Sin(d),0,(float)Math.Cos(d))*(f%120<60?6.8f:3);p.Pose(1+f*Dt);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes)");
        var c=new NativeLocomotionCarry();Check(!c.Carry(new MeleeRig(),MeleeSet.For("sword"),new MeleeView{Spec="pistol"},1,1,0,1),"a weapon without pack locomotion keeps the captured walk and run");
        log.Add("no allocation per frame while turning and changing pace; other weapons keep the captured pair");
    }
    private static void Hooks(string root,List<string> log)
    {
        string mm=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/MeleeMotion.cs")),ca=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/CapturedAnimation.cs"));
        Check(mm.Contains("if(Locomotion!=null&&Locomotion.Carry(Rig,set,p,carry,k,phase,time)){}")&&mm.Contains("SteadyTorso(carry,carry*k);"),"MeleeMotion.Ready lays the pack carry in the captured pair's place, the steady torso after it");
        Check(ca.Contains("melee.Locomotion=Locomotion;")&&ca.Contains("Locomotion=>Hits!=null?locomotion??=new NativeLocomotionCarry():null;"),"the arena rigs (host, guests, killcam, spectators) take the pack carry; source parity and other rigs keep the captured pair");
        Check(ca.Contains("float keep=firstPerson&&MeleeMotion.Handles(a.SourceSpec)?NativeLocoRules.FirstPersonCarry:1;"),"the first-person melee view keeps only part of the captured pump");
        log.Add("hooks: MeleeMotion.Ready (pack carry, then the steady torso), the arena rigs, the first-person view's damped pump");
    }
}
