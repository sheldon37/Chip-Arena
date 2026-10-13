using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The DEV Cyber Lord's presentation (Claude; docs/CYBER-LORD.md) against Astra's replicated data (her rules: Actor.Cyber,
// PR #142, and Actor.Kernel, PR #138; docs/NEW-KITS.md section 3). Driven by CyberLordFixture, which writes her fields the way
// her host does:
//  1. the contract: the numbers are the spec's and her rules', each record and interrupt maps, one field mapping;
//  2. pose timing: the pack takes are the exporter's, each take's impact lands on its replicated hit, the sequence's states;
//  3. replicated data only: a copied actor (and a kit round-tripped through JSON) draws the identical frame, in any order;
//  4. effect lifecycles: each effect starts and ends with its window, records expire, ghosts and the look's formation;
//  5. tiers: reduced effects and the low graphics tier thin everything; reduced never strobes, shakes or cuts hard;
//  6. the camera, the participants' clock and the cut-in;
//  7. the DEV gate on every UI entry, the HUD cards, the entrance, the audio keys;
//  8. no per-frame allocation;
//  9. the idle stance (the sword idle, twitching) and its glitch.
// Pixels are checked by the offline renders, not here; nothing here runs Unity.
internal static class CyberLordChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord: "+what);count++;}
    private static void Near(float a,float b,string what,float tol=1e-3f)=>Check(Math.Abs(a-b)<=tol,what+" ("+a+" vs "+b+")");
    private static bool NearV(V3 a,V3 b,float tol=1e-4f)=>(a-b).Length<=tol;

    // ---- a recording sink and a fixed scene ----
    private sealed class Sink:ICyberFxSink
    {
        public int Count,View,Voxels,Glows,Sprites,Bands,Flat;public string FlatWhat;public double Hash;public bool Hashing=true;
        // The owner's rule (no glowing disc, ring or tile lying on the floor; areas read through their volume): a piece whose
        // whole extent is within 12 cm of the floor, under 3 cm thick and over 10 cm across counts as flat.
        private void Floor(float bottom,float thick,float across,string what){if(bottom<.12f&&thick<.03f&&across>.1f){Flat++;FlatWhat??=what+" (top "+bottom.ToString("0.000")+" m, "+thick.ToString("0.000")+" thick, "+across.ToString("0.00")+" across)";}}
        private static Vector3 Abs(Vector3 v)=>new Vector3(Math.Abs(v.X),Math.Abs(v.Y),Math.Abs(v.Z));
        private void Mix(Vector3 a,float k){if(!Hashing)return;Hash=Hash*1.0000003+a.X*.37+a.Y*.61+a.Z*.89+k*1.3;if(!(float.IsFinite(a.X)&&float.IsFinite(a.Y)&&float.IsFinite(a.Z)&&float.IsFinite(k)))throw new Exception("Cyber Lord: a non-finite effect");}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;Glows++;Mix(at,size+color.X+u);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;Mix(head,width+velocity.Y+color.Y);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;Mix(at,opacity+size);}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Count++;Sprites++;if(view)View++;Mix(at,size+color.Z+profile);}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Count++;if(view)View++;Mix(tail+head*.5f,width+color.X);if(!view)Floor(Math.Max(tail.Y,head.Y),Math.Abs(tail.Y-head.Y)+width,new Vector2(head.X-tail.X,head.Z-tail.Z).Length(),"line");}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Count++;Bands++;if(view)View++;Mix(a+c*.5f,ua+ub+color.Y);if(!view){float top=Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y)),bot=Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y));Floor(top,top-bot,Math.Max(new Vector2(c.X-a.X,c.Z-a.Z).Length(),new Vector2(d.X-b.X,d.Z-b.Z).Length()),"band");}}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Count++;Voxels++;if(view)View++;Mix(at,scale.X+rotation.W+color.Z);if(!view){var h=Abs(Vector3.Transform(Vector3.UnitX,rotation))*scale.X*.5f+Abs(Vector3.Transform(Vector3.UnitY,rotation))*scale.Y*.5f+Abs(Vector3.Transform(Vector3.UnitZ,rotation))*scale.Z*.5f;Floor(at.Y+h.Y,2*h.Y,2*Math.Max(h.X,h.Z),"voxel");}}
        public int Particles=>Count-Voxels-Bands;
        public void Reset(){Count=View=Voxels=Glows=Sprites=Bands=0;Hash=0;}public int Flats;public void Tally(){Flats+=Flat;Flat=0;}
    }
    private sealed class Null:ICyberFxSink
    {
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){}
    }
    private sealed class Scene:ICyberFxScene
    {
        public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public int ViewerId,LocalId;public Vector3 EyeAt=new Vector3(0,1.6f,-6);
        private static Vector3 P(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public Vector3 Feet(Actor a)=>P(a);
        public bool Chest(Actor a,out Vector3 c){c=P(a)+new Vector3(0,1.25f,0);return true;}
        public bool Fist(Actor a,bool left,out Vector3 f){var fw=KernelPanicPath.Forward(a.Yaw);var r=new Vector3(fw.Z,0,-fw.X);f=P(a)+new Vector3(0,1.3f,0)+fw*.35f+r*(left?-.18f:.18f);return true;}
        public int Viewer=>ViewerId;public int Local=>LocalId;
        public bool ViewFist(bool left,out Vector3 f){f=EyeAt+new Vector3(left?-.15f:.15f,-.25f,.45f);return ViewerId!=0;}
        public Vector3 Eye=>EyeAt;public float Floor(Vector3 at)=>0;
    }
    private static Actor Sword(int id,Vector3 at,float yaw)=>new Actor{Id=id,SourceSpec="sword",Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,Yaw=yaw,Position=new V3(at.X,at.Y,at.Z),MeleeKit=new NativeMeleeKit()};
    private static ArenaEvent Ev(int id,EventKind kind,float time,int source,int target,Vector3 at,string detail,float amount=10)=>new ArenaEvent{Id=id,Kind=kind,Time=time,Source=source,Target=target,Position=new V3(at.X,at.Y,at.Z),From=new V3(at.X,at.Y,at.Z),Amount=amount,Detail=detail};

    // Everything at once (8 .. 13 s): a Kernel Panic on pilot 2, a wall, the dash trail, a link on 3, the crash field
    // glitching 3, packet shards, the combo's palm, and the events those make.
    private static (List<Actor> actors,List<ArenaEvent> events) Everything()
    {
        var c=CyberLordFixture.Pilot(1,Vector3.Zero,0);var v=Sword(2,new Vector3(0,0,1.4f),180);var o=Sword(3,new Vector3(2.5f,0,3),220);
        CyberLordFixture.KernelPanic(c,8.0f,2,new Vector3(0,0,1.4f));
        CyberLordFixture.Firewall(c,9.0f);CyberLordFixture.SystemCrash(c,9.3f);CyberLordFixture.DataDash(c,9.5f);CyberLordFixture.SiphonLink(c,3,9.6f);
        CyberLordFixture.PacketBurst(c,9.85f);CyberLordFixture.Combo(c,2,9.9f);
        var view=new CyberLordView();CyberLordFeed.Read(c,view);var ev=new List<ArenaEvent>();int id=1;
        for(int s=1;s<=5;s++)for(int k=0;k<(s==4?3:1);k++)ev.Add(Ev(id++,EventKind.Hit,CyberLordFixture.PanicHitTime(view,s),1,2,new Vector3(0,1.2f,2),CyberLordFeed.Details[7+s]));
        ev.Add(Ev(id++,EventKind.Hit,CyberLordFixture.PanicHitTime(view,5),1,3,new Vector3(2.5f,1,3),"kernel-panic-wave",20));
        ev.Add(Ev(id++,EventKind.Absorb,9.6f,4,1,new Vector3(0,1.2f,.8f),"firewall"));ev.Add(Ev(id++,EventKind.Hit,9.9f,1,3,new Vector3(.4f,1,.8f),"firewall",2.5f));
        ev.Add(Ev(id++,EventKind.AbilityHit,9.8f,1,3,new Vector3(2.5f,1,3),"system-crash",25));ev.Add(Ev(id++,EventKind.Hit,10.2f,1,3,new Vector3(2.5f,1.2f,3),"siphon-link",2));ev.Add(Ev(id++,EventKind.Heal,10.2f,1,1,Vector3.Zero,null,1));
        ev.Add(Ev(id++,EventKind.Hit,9.9f+CyberLordSpec.ComboWindups[2],1,3,new Vector3(.3f,1.2f,1.6f),"cyber-palm",22));ev.Add(Ev(id++,EventKind.AbilityHit,10.0f,1,3,new Vector3(2,1,3),"packet-burst",5));
        ev.Add(Ev(id++,EventKind.AbilityHit,9.62f,1,3,new Vector3(1,1,4),"data-dash",18));
        return (new List<Actor>{c,v,o},ev);
    }
    private static (int count,double hash,int ghosts,double ghostHash) Frame(NativeCyberLordFx fx,Scene scene,List<Actor> actors,List<ArenaEvent> events,float t,Sink sink,float q=1,bool low=false)
    {
        scene.List.Clear();scene.List.AddRange(actors);fx.Observe(events,t);sink.Reset();fx.Draw(sink,scene,t,q,low);sink.Tally();double g=0;
        for(int i=0;i<fx.Ghosts;i++){var h=fx.Ghost(i);g=g*1.0001+h.Kind+h.Actor*.3+h.Offset.X+h.Position.X*.7+h.Position.Z*.11+h.Yaw*.01+h.PoseAge+h.Opacity+h.Tint.X+h.Step;}
        return (sink.Count,sink.Hash,fx.Ghosts,g);
    }

    public static IEnumerable<string> Run(string root,ConversionContent data)
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The contract
        // ------------------------------------------------------------------------------------------------------------------
        // The spec lives outside the Unity project; CI checks out only unity/ChipArenaConversion, so its text checks run where it is present.
        string specPath=Path.Combine(root,"../../docs/NEW-KITS.md");bool hasSpec=File.Exists(specPath);if(!hasSpec)Console.WriteLine("SKIP Cyber Lord spec text: docs/NEW-KITS.md is not in this tree");
        string spec=hasSpec?File.ReadAllText(specPath):"";int at3=spec.IndexOf("## 3. Cyber Lord",StringComparison.Ordinal);if(hasSpec)Check(at3>0,"NEW-KITS section 3 is there");
        string s3=hasSpec?spec.Substring(at3,spec.IndexOf("## Owner-facing defaults",at3,StringComparison.Ordinal)-at3):"";
        foreach(var (text,ok) in new (string,bool)[]{
            ("Jab: 12 damage, .28 s",CyberLordSpec.ComboDurations[0]==.28f&&CyberLordSpec.ComboDamage[0]==12),("Cross: 14 damage, .30 s",CyberLordSpec.ComboDurations[1]==.30f),("Pulse Palm: 22 damage, .45 s",CyberLordSpec.ComboDurations[2]==.45f),
            ("range 2.2 m, arc 1.4",CyberLordSpec.ComboRange==2.2f&&CyberLordSpec.ComboArc==1.4f),("1.6 m forward cone",CyberLordSpec.PalmCone==1.6f),("after .6 s idle",CyberLordSpec.ComboReset==.6f),("restores 6",CyberLordSpec.ManaPerHit==6),("mana (100)",CyberLordSpec.Mana==100),
            ("7 m dash",CyberLordSpec.DashDistance==7),("1.2 s trail",CyberLordSpec.TrailSeconds==1.2f),("per .3 s. Cooldown 6 s, 20 mana",CyberLordSpec.TrailTick==.3f&&CyberLordSpec.Cooldowns[0]==6&&CyberLordSpec.Costs[0]==20),
            ("5 m wide and 2.5 m tall, placed .8 m in front, lasting 4 s",CyberLordSpec.WallWidth==5&&CyberLordSpec.WallHeight==2.5f&&CyberLordSpec.WallAhead==.8f&&CyberLordSpec.WallSeconds==4),("Cooldown 12 s, 35 mana",CyberLordSpec.Cooldowns[1]==12&&CyberLordSpec.Costs[1]==35),
            ("6 m, 50 degree cone of 8 glitch shards",CyberLordSpec.BurstRange==6&&CyberLordSpec.BurstAngle==50&&CyberLordSpec.BurstShards==8),("Cooldown 4 s,\n  15 mana",CyberLordSpec.Cooldowns[2]==4&&CyberLordSpec.Costs[2]==15),
            ("within 12 m for 2.5 s",CyberLordSpec.LinkRange==12&&CyberLordSpec.LinkSeconds==2.5f),("beyond 15 m. Cooldown 10 s, 25 mana",CyberLordSpec.LinkBreak==15&&CyberLordSpec.Cooldowns[3]==10&&CyberLordSpec.Costs[3]==25),
            ("a .5 s windup ground slam that makes a 4 m field",CyberLordSpec.CrashWindup==.5f&&CyberLordSpec.CrashRadius==4),("1.0 s \"glitched\"\n  stun. Cooldown 14 s, 40 mana",CyberLordSpec.CrashStun==1&&CyberLordSpec.Cooldowns[4]==14&&CyberLordSpec.Costs[4]==40),
            ("A .25 s windup",CyberLordSpec.PanicWindup==.25f),("6 m hardlight lunge-punch",CyberLordSpec.PanicLunge==6),("radius .6 m",CyberLordSpec.PanicRadius==.6f),("**Whiff:** .6 s recovery, and half the cooldown",CyberLordSpec.PanicWhiff==.6f),
            ("Cooldown 30 s, 60 mana",CyberLordSpec.Cooldowns[5]==30&&CyberLordSpec.Costs[5]==60),("a .6 s \"skill cut-in\"",CyberLordSpec.CutIn==.6f),
            ("launches the victim 8 m back along the facing, rising 1.5 m",KernelPanicPath.Launch==8&&KernelPanicPath.LaunchRise==1.5f),("| 2. Blink-behind | .45 s |",KernelPanicPath.Steps[1]==.45f),("kicks them straight up 5 m",KernelPanicPath.KickUp==5),
            ("| 3. Blink-above | .95 s |",KernelPanicPath.Steps[2]==.95f),("| 4. Fork bomb | 1.35 s |",KernelPanicPath.Steps[3]==1.35f),("| 5. Kernel Panic | 1.9 s |",KernelPanicPath.Steps[4]==1.9f),("| 1. Breaker | 0 s |",KernelPanicPath.Steps[0]==0),
            ("| Recovery | from 2.6 s | Caster recovers for .8 s",KernelPanicPath.Recovery==2.6f&&Math.Abs(KernelPanicPath.RecoverySeconds-.8f)<1e-5f),("a 5 m shockwave",KernelPanicPath.Shockwave==5),("about 3.4 s",Math.Abs(KernelPanicPath.End-3.4f)<1e-5f)})
            Check((!hasSpec||s3.Replace("\r","").Contains(text.Replace("\n  ","\n  ")))&&ok,"the presentation's number is the spec's: "+text.Replace("\n"," "));
        var details=Regex.Matches(s3,@"`(cyber-[a-z]+|data-dash|firewall|packet-burst|siphon-link|system-crash)`").Select(m=>m.Groups[1].Value).Distinct().ToArray();
        Check((!hasSpec||details.SequenceEqual(CyberLordFeed.Details.Take(8)))&&Enumerable.Range(1,5).All(i=>CyberLordFeed.Details[7+i]=="kernel-panic-"+i)&&(!hasSpec||s3.Contains("`kernel-panic-1` to `-5`")),"the event Details are the spec's ("+string.Join(",",details)+")");
        for(int i=0;i<CyberLordFeed.Details.Length;i++)Check(CyberLordFeed.Hit(CyberLordFeed.Details[i])==(CyberHit)(i+1),"Detail "+CyberLordFeed.Details[i]+" maps");
        Check(CyberLordFeed.Hit(null)==CyberHit.None&&CyberLordFeed.Hit("sword")==CyberHit.None&&CyberLordFeed.Hit("kernel-panic-6")==CyberHit.None,"unknown Details map to nothing");
        Check(Enumerable.Range(1,5).All(i=>CyberLordFeed.PanicStep(CyberHit.Panic1+i-1)==i)&&CyberLordFeed.PanicStep(CyberHit.Palm)==0,"Kernel Panic steps");
        Check(CyberLordSpec.Ids.Select(CyberLordSpec.Slot).SequenceEqual(Enumerable.Range(0,6))&&CyberLordSpec.Keys.SequenceEqual(new[]{"Q","E","1","2","3","4"})&&CyberLordSpec.Slot("nope")==-1,"slots Q, E, 1-4");
        // Astra's kit (PR #142) is the replicated state: Actor.Cyber, a [Serializable] NativeCyberKit the snapshot carries; the
        // proposed Actor.CyberKit slot is gone. The copy the presentation holds is deep.
        Check(typeof(Actor).GetField("CyberKit")==null&&typeof(Actor).GetField("Cyber")?.FieldType==typeof(NativeCyberKit)&&typeof(Actor).GetField("Cyber").GetCustomAttribute<NonSerializedAttribute>()==null,"Actor.Cyber is Astra's replicated NativeCyberKit (no proposed slot)");
        Check(typeof(NativeCyberKit).GetCustomAttribute<SerializableAttribute>()!=null&&typeof(NativeCyberKit).Assembly.GetType("ChipArena.UnityConversion.NativeCyberCast")==null,"her kit is serialisable; the proposed cast record is gone");
        if(hasSpec)Check(spec.Contains("## Cyber Lord implementation contract")&&spec.Contains("`Actor.Cyber` is a deep-copied `NativeCyberKit`"),"NEW-KITS records Astra's contract");
        var orig=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.DataDash(orig,1);CyberLordFixture.KernelPanic(orig,1,2,new Vector3(0,0,1.4f));var copy=orig.ViewCopy();
        Check(!ReferenceEquals(copy.Cyber,orig.Cyber)&&!ReferenceEquals(copy.Kernel,orig.Kernel)&&!ReferenceEquals(copy.Kernel.CasterPoints,orig.Kernel.CasterPoints),"Actor.ViewCopy deep-copies Astra's kit and Kernel");
        copy.Cyber.DashAt=99;copy.Kernel.CasterPoints[1]=new V3(9,9,9);Check(orig.Cyber.DashAt==1&&orig.Kernel.CasterPoints[1].x!=9,"a copy's edits leave the original alone");
        // The fixtures write the kit as her rules do (NativeCyberRules: the combo's numbers; StepCyberInput's windows).
        Check(ReferenceEquals(CyberLordSpec.ComboDurations,NativeCyberRules.ComboSeconds)&&ReferenceEquals(CyberLordSpec.ComboDamage,NativeCyberRules.ComboDamage)&&CyberLordSpec.Ids.Take(5).SequenceEqual(new[]{"data-dash","firewall","packet-burst","siphon-link","system-crash"})
            &&CyberLordSpec.ComboKinds.SequenceEqual(CyberLordFeed.Details.Take(3)),"the combo's numbers are NativeCyberRules', the kinds her Kind strings");
        {
            var k=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.DataDash(k,5);var dk=k.Cyber.ViewCopy();
            Check(NearV(NativeCyberRules.DashPosition(dk,5.2f),dk.DashTo)&&NearV(NativeCyberRules.DashPosition(dk,5.1f),(dk.DashFrom+dk.DashTo)*.5f)&&Math.Abs((dk.DashTo-dk.DashFrom).Length-7)<1e-4f&&Math.Abs(dk.DashFrom.y-CyberLordFeed.BodyCentre)<1e-5f,"the fixture's dash is her 7 m, .2 s DashPosition between body centres");
        }
        {
            // Her Predict (the guest's copy of StepCyberInput) begins each untargeted cast and combo step with the windows the
            // presentation and its fixtures key on, at attack rate 1 and 1.25 and cooldown scale 1.
            Func<V3,V3,V3> free=(p0,p1)=>p1;
            foreach(var (slot,input) in new[]{(CyberLordSpec.Q,new InputFrame{AbilityQ=true}),(CyberLordSpec.E,new InputFrame{AbilityE=true}),(CyberLordSpec.One,new InputFrame{SirenAbility=1}),(CyberLordSpec.Three,new InputFrame{SirenAbility=3})})
            {
                var st=new NativeCyberKit();float mana=100;NativeCyberRules.Predict(st,input,10,new V3(0,CyberLordFeed.BodyCentre,0),ref mana,1,1,1,free);
                var fa=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.Cast(fa,slot,10);var fk=fa.Cyber;
                Check(st.Kind==CyberLordSpec.Ids[slot]&&Math.Abs(st.Release-st.At-CyberLordSpec.CastWindup[slot])<1e-5f&&Math.Abs(st.Until-st.At-CyberLordSpec.CastSeconds[slot])<1e-5f&&Math.Abs(100-mana-CyberLordSpec.Costs[slot])<1e-4f,CyberLordSpec.Names[slot]+": her Predict's windup, length and cost are the presentation's");
                Check(fk.Kind==st.Kind&&fk.At==st.At&&fk.Release==st.Release&&fk.Until==st.Until&&fk.QReady==st.QReady&&fk.EReady==st.EReady&&fk.PacketReady==st.PacketReady&&fk.CrashReady==st.CrashReady&&fk.DashAt==st.DashAt&&fk.DashUntil==st.DashUntil&&fk.TrailUntil==st.TrailUntil
                    &&NearV(fk.DashFrom,st.DashFrom)&&NearV(fk.DashTo,st.DashTo)&&fk.WallAt==st.WallAt&&fk.WallUntil==st.WallUntil&&fk.WallYaw==st.WallYaw&&NearV(fk.WallPoint,st.WallPoint)&&fk.CrashAt==st.CrashAt&&fk.CrashUntil==st.CrashUntil&&NearV(fk.CrashPoint,st.CrashPoint),CyberLordSpec.Names[slot]+": the fixture writes the kit as her Predict does");
            }
            foreach(float rate in new[]{1f,1.25f})
            {
                var st=new NativeCyberKit();float mana=100;
                for(int step=0;step<3;step++)
                {
                    float t=10+step*.5f;NativeCyberRules.Predict(st,new InputFrame{Attack=true},t,new V3(0,CyberLordFeed.BodyCentre,0),ref mana,rate,1,1,free);
                    Check(st.Combo==step&&st.Kind==CyberLordSpec.ComboKinds[step]&&Math.Abs(st.Release-t-CyberLordSpec.ComboWindups[step]/rate)<1e-5f&&Math.Abs(st.Until-t-CyberLordSpec.ComboDurations[step]/rate)<1e-5f,"combo step "+step+" at rate "+rate+": her windup and length");
                    // The kit as the host leaves it after a block that began after the swing: the swing's own end still reads.
                    var x=CyberLordFixture.Pilot(1,Vector3.Zero,0);x.Cyber=st.ViewCopy();x.Cyber.Until=t+3;var xv=new CyberLordView();CyberLordFeed.Read(x,xv);Near(xv.ComboUntil,t+CyberLordSpec.ComboDurations[step]/rate,"step "+step+" at rate "+rate+": the swing's end without the actor's attack window",1e-4f);
                }
            }
        }
        {
            // Each record into the view (feet, windows, cooldowns), and Astra's interrupt (StepCyberWorld: Until moved to the host's
            // time each tick, the dash and the link zeroed) ending a window where it began.
            var a=CyberLordFixture.Pilot(1,new Vector3(1,0,2),90);var v=new CyberLordView();
            CyberLordFixture.Combo(a,2,10);CyberLordFeed.Read(a,v);Check(v.ComboStep==2&&v.ComboAt==10&&Math.Abs(v.ComboUntil-10.45f)<1e-4f&&Math.Abs(v.ComboHit-10.18f)<1e-4f,"the palm: its swing and its hit on Release");
            CyberLordFixture.Combo(a,0,10);CyberLordFixture.Block(a,10.1f,10.6f);CyberLordFeed.Read(a,v);Near(v.ComboUntil,10.1f,"a block cuts the swing where it began",1e-4f);
            Check(CyberLordMotion.Of(v,10.3f,true,10.1f).State=="cyber-guard","the guard shows through a cut swing");
            CyberLordFixture.Combo(a,0,10);CyberLordFixture.Block(a,11,11.5f);CyberLordFeed.Read(a,v);Near(v.ComboUntil,10.28f,"a later block leaves the finished swing alone",1e-4f);
            CyberLordFixture.DataDash(a,20);CyberLordFeed.Read(a,v);var f=KernelPanicPath.Forward(90);
            Check(v.At[0]==20&&v.Release[0]==20&&Math.Abs(v.Until[0]-20.2f)<1e-4f&&Math.Abs(v.RecoverUntil[0]-20.2f-CyberLordSpec.Settle[0])<1e-4f&&Math.Abs(v.TrailUntil-21.4f)<1e-4f&&v.Ready[0]==26
                &&NearV(v.DashFrom,new V3(1,0,2))&&NearV(v.DashTo,new V3(1+f.X*7,0,2+f.Z*7),1e-3f),"Data Dash: its window, settle, trail, cooldown and feet path");
            CyberLordFixture.Block(a,20.1f,20.4f);CyberLordFeed.Read(a,v);Check(Math.Abs(v.Until[0]-20.1f)<1e-4f&&v.RecoverUntil[0]==v.Until[0]&&Math.Abs(v.TrailUntil-21.4f)<1e-4f,"an interrupted dash ends there; its trail ticks on");
            CyberLordFixture.DataDash(a,20);CyberLordFixture.Block(a,25,25.3f);CyberLordFeed.Read(a,v);Near(v.Until[0],20.2f,"a later block leaves the finished dash alone",1e-4f);
            CyberLordFixture.Firewall(a,40);CyberLordFeed.Read(a,v);
            Check(v.WallBorn==40&&v.WallUntil==44&&Math.Abs(v.Until[1]-40.2f)<1e-4f&&Math.Abs(v.WallYaw-90)<1e-4f&&NearV(v.WallAt,new V3(1+f.X*.8f,0,2+f.Z*.8f),1e-3f)&&v.Ready[1]==52,"Firewall: the wall's bottom centre, yaw and life");
            CyberLordFixture.PacketBurst(a,50);CyberLordFeed.Read(a,v);Check(Math.Abs(v.Release[2]-50.08f)<1e-4f&&Math.Abs(v.Until[2]-50.25f)<1e-4f&&v.Ready[2]==54&&v.WallBorn==40,"Packet Burst: shards on Release; the wall's record stays");
            CyberLordFixture.DataDash(a,51);CyberLordFeed.Read(a,v);Check(v.At[2]==-100,"Packet Burst has no record once another cast replaces the kit's window");
            CyberLordFixture.SiphonLink(a,3,60);CyberLordFeed.Read(a,v);Check(v.LinkTarget==3&&v.LinkAt==60&&Math.Abs(v.LinkUntil-62.5f)<1e-4f&&Math.Abs(v.Release[3]-60.2f)<1e-4f&&Math.Abs(v.Until[3]-62.5f)<1e-4f&&v.Ready[3]==70,"Siphon Link: the reach over the cast, the tether 2.5 s");
            CyberLordFixture.SiphonLink(a,3,60,61);CyberLordFeed.Read(a,v);Near(v.LinkUntil,61,"a lost link ends at the break",1e-4f);
            CyberLordFixture.SiphonLink(a,3,60);CyberLordFixture.Block(a,61.2f,61.5f);CyberLordFeed.Read(a,v);Near(v.LinkUntil,61.2f,"an interrupt ends the link where it began",1e-4f);
            CyberLordFixture.SystemCrash(a,70);CyberLordFeed.Read(a,v);Check(Math.Abs(v.Release[4]-70.5f)<1e-4f&&Math.Abs(v.Until[4]-70.8f)<1e-4f&&Math.Abs(v.CrashBorn-70.5f)<1e-4f&&Math.Abs(v.CrashUntil-71.5f)<1e-4f&&NearV(v.CrashAt,new V3(1,0,2))&&v.Ready[4]==84,"System Crash: the slam at Release, the field for the stun, at the feet");
            CyberLordFixture.SystemCrash(a,70);CyberLordFixture.Block(a,70.2f,70.6f);CyberLordFeed.Read(a,v);Check(v.CrashBorn==-100&&Math.Abs(v.Until[4]-70.2f)<1e-4f&&v.Release[4]<=v.Until[4],"a crash interrupted in its windup makes no field");
            CyberLordFixture.SystemCrash(a,70);CyberLordFixture.Block(a,70.6f,71);CyberLordFeed.Read(a,v);Check(Math.Abs(v.CrashBorn-70.5f)<1e-4f&&Math.Abs(v.Until[4]-70.6f)<1e-4f,"a crash interrupted after the slam keeps its field");
            // Kernel Panic after a swing: its own AttackAt replaces the swing's, and the incapacity moves the kit's Until.
            CyberLordFixture.Combo(a,0,80);CyberLordFixture.KernelPanic(a,80.3f,0,Vector3.Zero,connect:false);a.Cyber.Until=81;CyberLordFeed.Read(a,v);Near(v.ComboUntil,80.28f,"a swing before Kernel Panic keeps its own end",1e-4f);
            var reset=CyberLordFixture.Pilot(1,Vector3.Zero,0);reset.Cyber=new NativeCyberKit();reset.AttackAt=5;reset.AttackUntil=5.3f;CyberLordFeed.Read(reset,v);Check(v.ComboAt==-100&&v.At.Take(5).All(x=>x==-100)&&v.Ready.Take(5).All(x=>x==0),"the host's reset kit: nothing drawn, every key ready");
            // The Siphon's heal: a plain Heal (no Detail) of the caster in the tick of its drain hit.
            Check(CyberLordFeed.SiphonHeal(EventKind.Heal,null,1,1,10,1,10)&&!CyberLordFeed.SiphonHeal(EventKind.Heal,null,1,1,10.25f,1,10)&&!CyberLordFeed.SiphonHeal(EventKind.Heal,null,2,2,10,1,10)&&!CyberLordFeed.SiphonHeal(EventKind.Heal,"chip",1,1,10,1,10)&&!CyberLordFeed.SiphonHeal(EventKind.Hit,null,1,1,10,1,10),"a heal is the Siphon's only in its drain's tick");
            Check(!CyberLordFeed.Kind(EventKind.AbilityCast)&&!CyberLordFeed.Kind(EventKind.Teleport)&&CyberLordFeed.Kind(EventKind.Absorb)&&CyberLordFeed.Strike(EventKind.Hit)&&CyberLordFeed.Strike(EventKind.AbilityHit)&&!CyberLordFeed.Strike(EventKind.Heal),"the casts draw from the kit, not their AbilityCast / Teleport events");
        }
        // Kernel Panic is Astra's implemented contract (PR #138): her numbers, names and outcome codes, read as they are.
        Check(CyberLordSpec.CutIn==NativeKernelPanicRules.CutIn&&CyberLordSpec.PanicWindup==NativeKernelPanicRules.Windup&&CyberLordSpec.PanicWhiff==NativeKernelPanicRules.WhiffRecovery&&CyberLordSpec.PanicLunge==NativeKernelPanicRules.Range&&CyberLordSpec.PanicRadius==NativeKernelPanicRules.Radius
            &&CyberLordSpec.Cooldowns[CyberLordSpec.Four]==NativeKernelPanicRules.Cooldown&&CyberLordSpec.Costs[CyberLordSpec.Four]==NativeKernelPanicRules.Mana&&ReferenceEquals(KernelPanicPath.Steps,NativeKernelPanicRules.Steps)&&KernelPanicPath.Recovery==NativeKernelPanicRules.RecoveryAt&&KernelPanicPath.End==NativeKernelPanicRules.Duration,"Kernel Panic's numbers are Astra's (NativeKernelPanicRules)");
        if(hasSpec)Check(s3.Contains("`Actor.Kernel`")&&s3.Contains("`kernel-panic-wave`")&&s3.Contains("Outcome: 0 windup, 1 connect"),"the contract recorded in NEW-KITS names Actor.Kernel, its outcomes and kernel-panic-wave");
        for(int i=1;i<=5;i++){var e=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(e,10,2,new Vector3(0,0,1.4f));var ev=new CyberLordView();CyberLordFeed.Read(e,ev);Near(CyberLordFixture.PanicHitTime(ev,i),NativeKernelPanicRules.HitAt(e.Kernel,i-1),"step "+i+" lands on NativeKernelPanicRules.HitAt");}
        {
            // The bodies the snapshot shows (NativeKernelPanicRules.Sample, the host's motion) are where KernelPanicPath puts them.
            var e=CyberLordFixture.Pilot(1,new Vector3(0,0,-1),0);CyberLordFixture.KernelPanic(e,10,2,new Vector3(0,0,1.4f),seed:8);var ev=new CyberLordView();CyberLordFeed.Read(e,ev);float worst=0;
            for(float t=ev.PanicConnect;t<ev.PanicStart+KernelPanicPath.End+.2f;t+=1/120f){NativeKernelPanicRules.Sample(e.Kernel,t,out var hc,out var hvic);var pc=KernelPanicPath.Caster(ev,t-ev.PanicStart,out _);var pv=KernelPanicPath.Victim(ev,t-ev.PanicStart);
                worst=Math.Max(worst,Math.Max((new Vector3(hc.x,hc.y-CyberLordFeed.BodyCentre,hc.z)-pc).Length(),(new Vector3(hvic.x,hvic.y-CyberLordFeed.BodyCentre,hvic.z)-pv).Length()));}
            Check(worst<1e-4f,"the presentation's path is the host's Sample (worst "+worst+" m)");
            Check(ev.PanicOutcome==CyberLordSpec.Connect&&ev.PanicVictim==2&&ev.PanicPointCount==6&&Math.Abs(ev.PanicYaw)<1e-3f&&ev.Ready[CyberLordSpec.Four]==e.Kernel.Ready,"connect, victim, points, facing and ready map");
            var victimCopy=Sword(2,new Vector3(0,0,1.4f),180);victimCopy.Kernel=e.Kernel.ViewCopy();var cyberVictim=CyberLordFixture.Pilot(2,new Vector3(0,0,1.4f),180);cyberVictim.Kernel=e.Kernel.ViewCopy();var cv=new CyberLordView();
            Check(CyberLordFeed.Read(cyberVictim,cv)&&cv.PanicOutcome==0&&cv.At[CyberLordSpec.Four]==-100,"a victim's copy of the caster's state is not read as its own cast");
            foreach(var (o,want) in new[]{(0,0),(2,CyberLordSpec.Whiff),(3,CyberLordSpec.EndedEarly),(4,CyberLordSpec.Connect)}){var k=e.Kernel.ViewCopy();k.Outcome=o;if(o==0){k.Victim=0;k.CasterPoints=k.VictimPoints=Array.Empty<V3>();}var x=CyberLordFixture.Pilot(1,Vector3.Zero,0);x.Kernel=k;var xv=new CyberLordView();CyberLordFeed.Read(x,xv);Check(xv.PanicOutcome==want,"outcome "+o+" maps to "+want);}
        }
        // The one field mapping: CyberLordFeed.Read names Astra's fields (a.Cyber, a.Kernel) once each; every other file that
        // draws from the view (the poses, effects, sounds, HUD, cameras, entrance and the runtime hosts) never names them. Her
        // simulation files (NativeCyberRules, Runtime/NativeCyberLord, NativePrediction, NativeNetworkFrame, NativeArena and the
        // rest) and the fixtures do, and are not presentation.
        var core=Path.Combine(root,"Assets/ChipArena/Core");var runtime=Path.Combine(root,"Assets/ChipArena/Runtime");
        string Rel(string f)=>Path.GetRelativePath(Path.Combine(root,"Assets/ChipArena"),f).Replace('\\','/');
        var sources=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories);
        var viewers=sources.Where(f=>{var t=File.ReadAllText(f);return t.Contains("CyberLordView")||t.Contains("ICyberFxSink");}).Select(Rel).Where(f=>f!="Core/NativeCyberLord.cs"&&f!="Core/CyberLordFixture.cs").OrderBy(f=>f).ToArray();
        foreach(var want in new[]{"Core/CyberLordMotion.cs","Core/NativeCyberLordFx.cs","Core/NativeCyberLordSounds.cs","Core/NativeCyberLordHud.cs","Core/NativeKernelPanicView.cs","Core/NativeCyberLordIntro.cs","Runtime/NativeCyberLordPresentation.cs","Runtime/NativeCyberLordScreen.cs"})Check(viewers.Contains(want),want+" draws from the view");
        foreach(var f in viewers){var src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));Check(!Regex.IsMatch(src,@"\.(Cyber|Kernel)\b")&&!src.Contains("NativeCyberKit")&&!src.Contains("CyberKit"),f+" reads Astra's state only through the view");}
        var naming=sources.Where(f=>Regex.IsMatch(File.ReadAllText(f),@"\.Cyber\b")).Select(Rel).ToArray();
        Check(naming.Contains("Core/NativeCyberLord.cs")&&naming.Contains("Runtime/NativeCyberLord.cs")&&naming.All(f=>!viewers.Contains(f)),"the files naming Actor.Cyber are the feed, the fixtures and her simulation ("+naming.Length+")");
        string feedSrc=File.ReadAllText(Path.Combine(core,"NativeCyberLord.cs"));Check(Regex.Matches(feedSrc,@"a\.Cyber\b").Count==1&&Regex.Matches(feedSrc,@"a\.Kernel\b").Count==1&&!Regex.IsMatch(feedSrc,@"\b(p|s|state)\.Cyber\b"),"CyberLordFeed.Read is the only reader of the kit and of Astra's Kernel");
        Check(!sources.Any(f=>Regex.IsMatch(File.ReadAllText(f),@"\bCyberKit\b")),"no file names the proposed Actor.CyberKit");
        foreach(var f in new[]{"NativeCyberLord.cs","CyberLordMotion.cs","NativeCyberLordLook.cs","NativeCyberLordFx.cs","NativeCyberLordSounds.cs","NativeKernelPanicView.cs","NativeCyberLordHud.cs","NativeCyberLordIntro.cs"})
        {
            string src=File.ReadAllText(Path.Combine(core,f));
            foreach(var banned in new[]{"NativePilot","NativeArena","UnityEngine","System.Random","DateTime","Rules.Step(","NativeSimulation",".Stamina","p.Mana","a.Mana","DamageDebt"})Check(!src.Contains(banned),f+" reads no authority-only state ("+banned+")");
        }
        foreach(var f in new[]{"NativeCyberLordPresentation.cs","NativeCyberLordScreen.cs"})
        {
            string src=File.ReadAllText(Path.Combine(runtime,f));foreach(var banned in new[]{"Time.timeScale","Time.time","arena.Step","NativeSimulation","Rules."})Check(!src.Contains(banned),f+" touches no clock or rule ("+banned+")");
        }
        var none=new CyberLordView();Check(!CyberLordFeed.Read(Sword(4,Vector3.Zero,0),none)&&none.Id==0&&!CyberLordFeed.Read(null,none),"other pilots map to nothing");
        // An empty kit (a new pilot, or the host's reset after death or a lost DEV identity: all zeros) draws nothing, and the
        // actor's attack window alone (Kernel Panic's own AttackAt) is never read as a swing.
        var bare=new Actor{Id=5,SourceSpec=CyberLordSpec.Id,Hp=100,Combo=1,AttackAt=3,AttackUntil=3.3f,SwingAt=3.12f};var bv=new CyberLordView();
        Check(CyberLordFeed.Read(bare,bv)&&bv.ComboAt==-100&&bv.At.All(x=>x==-100)&&bv.WallBorn==-100&&bv.LinkTarget==0&&bv.CrashBorn==-100&&bv.TrailUntil==-100,"an empty kit draws no swing or cast");
        bare.Cyber=null;Check(CyberLordFeed.Read(bare,bv)&&bv.ComboAt==-100,"a missing kit draws nothing");
        yield return $"Cyber Lord contract: {count} checks (the presentation's numbers are NEW-KITS section 3's and Astra's rules', the {CyberLordFeed.Details.Length} Details map, Actor.Cyber and Actor.Kernel read only in CyberLordFeed)";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. Pose timing
        // ------------------------------------------------------------------------------------------------------------------
        int c0=count;
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-cyberlord-provenance.json"))))
        {
            var prov=doc.RootElement.GetProperty("clips").EnumerateArray().ToArray();Check(prov.Length==NativePackCyberLord.Clips.Length&&NativePackCyberLord.Names.Length==prov.Length&&doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels,"the provenance lists every take");
            for(int i=0;i<prov.Length;i++)
            {
                var c=NativePackCyberLord.Clips[i];var e=prov[i];Check(c.Name==e.GetProperty("clip").GetString()&&c.Name=="cyberlord/"+NativePackCyberLord.Names[i],c.Name+" in order");
                string sha=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant();Check(sha==e.GetProperty("sha256").GetString(),c.Name+": the exporter's output ("+sha.Substring(0,12)+")");
                Check(c.Fps==e.GetProperty("fps").GetInt32()&&c.Frames==e.GetProperty("frames").GetInt32()&&c.Loop==e.GetProperty("loop").GetBoolean()&&c.Marks.SequenceEqual(e.GetProperty("marks").EnumerateArray().Select(x=>(float)x.GetDouble())),c.Name+": timing and marks");
                var rows=c.Decode();Check(rows.Length==c.Frames*CapturedClip.Channels&&rows.All(float.IsFinite),c.Name+": decodes finite");
            }
        }
        string[] names={"stance","guard","run","sprint","jab","cross","palm","dash","firewall","burst","siphon","crash","lunge","breaker","kick","hammer","slam","launched","knockdown","stunned","boost"};
        Check(NativePackCyberLord.Names.SequenceEqual(names)&&CyberLordMotion.Jab==4&&CyberLordMotion.Palm==6&&CyberLordMotion.Crash==11&&CyberLordMotion.Slam==16&&CyberLordMotion.Boost==20,"the motion's take indices are the pack's");
        foreach(int i in new[]{CyberLordMotion.Jab,CyberLordMotion.Cross,CyberLordMotion.Palm,CyberLordMotion.Lunge,CyberLordMotion.Breaker,CyberLordMotion.Kick,CyberLordMotion.Hammer,CyberLordMotion.Slam})
        {var c=CyberLordMotion.Clip(i);Check(c.Marks.Length==3&&c.Marks[1]>0&&c.Marks[1]<c.Duration,c.Name+" carries its impact mark");}
        // Each take's impact on its replicated hit: the combo's SwingAt (the rules' hit), each ability's release, the opener's
        // connect, and each Kernel Panic step's hit (KernelPanicPath.HitAge, which the hit-stops and fixture events share).
        var cl=CyberLordFixture.Pilot(1,Vector3.Zero,0);var view=new CyberLordView();
        for(int step=0;step<3;step++)
        {
            CyberLordFixture.Combo(cl,step,10);CyberLordFeed.Read(cl,view);var m=CyberLordMotion.Of(view,view.ComboHit,false,0);int clip=CyberLordMotion.ComboClip(step);
            Check(m.Clip==clip&&m.State==(step==0?"cyber-jab":step==1?"cyber-cross":"cyber-palm"),"combo step "+step+" plays its take");Near(m.Time,CyberLordMotion.Impact(clip),"combo step "+step+" impact on the replicated hit",2e-3f);
            Near(view.ComboHit,10+CyberLordSpec.ComboWindups[step],"step "+step+" hit on the kit's Release");
            float prev=-1;for(float t=10;t<view.ComboUntil;t+=1/240f){var mm=CyberLordMotion.Of(view,t,false,0);Check(mm.Time>=prev-1e-5f,"combo "+step+" take time runs forward");prev=mm.Time;}
            Check(CyberLordMotion.Of(view,view.ComboUntil+CyberLordMotion.BlendOut+.01f,false,0).Clip<0,"combo "+step+" ends with its window");
        }
        CyberLordFixture.Combo(cl,1,20);CyberLordFeed.Read(cl,view);var chain=CyberLordMotion.Of(view,20.01f,false,0);Check(chain.From==CyberLordMotion.Jab&&chain.FromWeight>0,"a chained cross starts from the jab's end");
        var abilityStates=new[]{"cyber-data-dash","cyber-firewall","cyber-packet-burst","cyber-siphon-link","cyber-system-crash"};
        for(int slot=0;slot<5;slot++)
        {
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);float t0=30;
            switch(slot){case 0:CyberLordFixture.DataDash(a,t0);break;case 1:CyberLordFixture.Firewall(a,t0);break;case 2:CyberLordFixture.PacketBurst(a,t0);break;case 3:CyberLordFixture.SiphonLink(a,2,t0);break;default:CyberLordFixture.SystemCrash(a,t0);break;}
            CyberLordFeed.Read(a,view);var at=CyberLordMotion.Of(view,view.Release[slot],false,0);int clip=CyberLordMotion.Dash+slot;
            Check(at.Clip==clip&&at.State==abilityStates[slot],CyberLordSpec.Names[slot]+" plays its take");
            if(slot!=3)Near(at.Time,CyberLordMotion.Impact(clip),CyberLordSpec.Names[slot]+": the take's impact on the replicated release",2e-3f);
            else Check(at.Time>CyberLordMotion.Clip(clip).Duration-.1f,"Siphon Link reaches by the release and holds");
            Check(CyberLordMotion.Of(view,t0-.01f,false,0).Clip<0&&CyberLordMotion.Of(view,view.RecoverUntil[slot]+CyberLordMotion.BlendOut+.01f,false,0).Clip<0,CyberLordSpec.Names[slot]+" starts and ends with its window");
        }
        Check(CyberLordSpec.CastWindup[CyberLordSpec.Three]==CyberLordSpec.CrashWindup&&CyberLordSpec.CastWindup[CyberLordSpec.Four]==CyberLordSpec.PanicWindup&&CyberLordSpec.CastSeconds[CyberLordSpec.Q]==CyberLordSpec.DashSeconds&&CyberLordSpec.Settle[CyberLordSpec.Four]==0,"the cast windows carry the rules' windups (Kernel Panic's from Actor.Kernel)");
        var caster=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(caster,40,2,new Vector3(0,0,1.4f));CyberLordFeed.Read(caster,view);
        Near(view.PanicConnect,40+CyberLordSpec.PanicWindup,"the connect at the lunge's end");Near(view.PanicStart,view.PanicConnect+CyberLordSpec.CutIn,"the sequence after the .6 s cut-in");
        var lunge=CyberLordMotion.Of(view,view.PanicConnect-1e-4f,false,0);Check(lunge.Clip==CyberLordMotion.Lunge,"the opener lunges");Near(lunge.Time,CyberLordMotion.Impact(CyberLordMotion.Lunge),"the lunge's punch lands on the connect",3e-3f);
        Check(CyberLordMotion.Of(view,view.PanicConnect+.3f,false,0).State=="cyber-kernel-cut-in","frozen on the punch through the cut-in");
        int[] stepClip={CyberLordMotion.Breaker,CyberLordMotion.Kick,CyberLordMotion.Hammer,-1,CyberLordMotion.Slam};
        for(int s=1;s<=5;s++)
        {
            if(stepClip[s-1]<0)continue;var m=CyberLordMotion.Of(view,view.PanicStart+KernelPanicPath.HitAge(s),false,0);Check(m.Clip==stepClip[s-1],"step "+s+" plays its take");
            Near(m.Time,CyberLordMotion.Impact(stepClip[s-1]),"step "+s+" impact on its hit",3e-3f);Near(CyberLordFixture.PanicHitTime(view,s),view.PanicStart+KernelPanicPath.HitAge(s),"step "+s+" fixture hit");
        }
        string[] seq={"cyber-kernel-breaker","cyber-kernel-kick","cyber-kernel-hammer","cyber-kernel-fork-bomb","cyber-kernel-slam","cyber-kernel-recovery"};float[] probe={.2f,.6f,1.1f,1.5f,2.0f,3.0f};
        for(int i=0;i<probe.Length;i++)Check(CyberLordMotion.Of(view,view.PanicStart+probe[i],false,0).State==seq[i],"the sequence at "+probe[i]+" s is "+seq[i]);
        Check(CyberLordMotion.Of(view,view.PanicStart+KernelPanicPath.End+CyberLordMotion.BlendOut+.01f,false,0).Clip<0,"back to the stance after the recovery");
        string[] vseq={"kernel-victim-held","kernel-victim-launched","kernel-victim-kicked","kernel-victim-hammered","kernel-victim-fork-bomb","kernel-victim-slammed"};float[] vprobe={-.3f,.2f,.6f,1.1f,1.5f,2.4f};
        for(int i=0;i<vprobe.Length;i++)Check(CyberLordMotion.Victim(view,view.PanicStart+vprobe[i]).State==vseq[i],"the victim at "+vprobe[i]+" s is "+vseq[i]);
        Check(CyberLordMotion.Victim(view,view.PanicStart+KernelPanicPath.End+.4f).Clip<0,"the victim's tumble ends with the sequence");
        var wall=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(wall,40,2,new Vector3(0,0,1.4f),wall:new Vector3(0,1.2f,4.4f));var wv=new CyberLordView();CyberLordFeed.Read(wall,wv);
        Check(wv.PanicWall&&CyberLordMotion.Victim(wv,wv.PanicStart+.4f).State=="kernel-victim-wall-splat"&&CyberLordMotion.Victim(view,view.PanicStart+.4f).State=="kernel-victim-launched","the wall-splat only on a wall");
        var whiff=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(whiff,40,0,Vector3.Zero,connect:false);var hv=new CyberLordView();CyberLordFeed.Read(whiff,hv);
        Check(CyberLordMotion.Of(hv,hv.Until[5]+.1f,false,0).State=="cyber-kernel-whiff"&&!hv.PanicLive(hv.Until[5]+.1f)&&!KernelPanicCamera.Active(hv,hv.Until[5]+.1f),"a whiff stumbles out with no sequence or camera");
        var early=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(early,40,2,new Vector3(0,0,1.4f),endedAt:41.5f);var ev2=new CyberLordView();CyberLordFeed.Read(early,ev2);
        Check(CyberLordMotion.Of(ev2,41.6f,false,0).State=="cyber-kernel-ended","an early end eases the caster out ("+CyberLordMotion.Of(ev2,41.6f,false,0).State+")");
        Check(CyberLordMotion.Of(ev2,41.5f+CyberLordMotion.BlendOut+.01f,false,0).Clip<0,"back to the stance after an early end ("+CyberLordMotion.Of(ev2,41.5f+CyberLordMotion.BlendOut+.01f,false,0).State+")");
        Check(CyberLordMotion.Victim(ev2,41.5f+.31f).Weight<=1e-4f,"the victim eases out of an early end");
        // The full rig on the fighter route (MeleeMotion.FighterBase, the style layer, FighterFinish) and the first-person view:
        // finite, normalised, and the presented states in order.
        {
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var mo=new MeleeMotion();var cyber=new CyberLordMotion();var mv=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true};var pose=new CapturedRig.Pose();
            CyberLordFixture.KernelPanic(a,4,2,new Vector3(0,0,1.4f));var seen=new List<string>();var fp=new List<string>();
            for(float t=0;t<9;t+=1/60f)
            {
                // The kit holds one window, so the timeline writes each cast when the host would: the jab, then the wall.
                if(t<2)CyberLordFixture.Combo(a,0,1);else if(a.Cyber.Kind!="firewall")CyberLordFixture.Firewall(a,2);
                mo.Rig.GroupPosition=new Vector3(0,.865f,0);mo.Rig.GroupYaw=0;mo.FighterBase(mv,t,1/60f);string st=cyber.Style(mo,a,t,false);mo.FighterFinish(mv,t,1/60f);if(seen.Count==0||seen[^1]!=st)seen.Add(st);
                foreach(var q in mo.Rig.Q)Check(float.IsFinite(q.W)&&Math.Abs(q.Length()-1)<1e-3f,"finite rotations at "+t);
                string f=cyber.FirstPerson(pose,a,t);if(fp.Count==0||fp[^1]!=f)fp.Add(f);foreach(var q in pose.Rotation)Check(float.IsFinite(q.W),"finite first-person rotations at "+t);
            }
            foreach(var st in new[]{"cyber-stance","cyber-jab","cyber-firewall","cyber-kernel-lunge","cyber-kernel-cut-in","cyber-kernel-breaker","cyber-kernel-kick","cyber-kernel-hammer","cyber-kernel-fork-bomb","cyber-kernel-slam","cyber-kernel-recovery"})
                Check(seen.Contains(st)&&fp.Contains(st),"the rig presents "+st+" (third and first person)");
            Check(seen.IndexOf("cyber-jab")<seen.IndexOf("cyber-firewall")&&seen.IndexOf("cyber-kernel-breaker")<seen.IndexOf("cyber-kernel-slam"),"in timeline order");
            var guard=CyberLordFixture.Pilot(1,Vector3.Zero,0);guard.BlockAt=1;guard.BlockUntil=3;var gm=new MeleeMotion();
            gm.Rig.GroupPosition=new Vector3(0,.865f,0);gm.FighterBase(mv,2,1/60f);Check(cyber.Style(gm,guard,2,false)=="cyber-guard","RMB holds the hardlight guard");
            Check(cyber.Style(gm,guard,2,true)=="hit reaction","a stun yields to the reaction");
        }
        // The victim's tumble layers over any route; the look's form and flare windows.
        {
            var pose=new CapturedRig.Pose();for(int b=0;b<MeleeRig.N;b++){pose.Rotation[b]=Quaternion.Identity;pose.Position[b]=CapturedRig.Bones[b].Position;}var vm=new CyberLordMotion();
            Check(vm.VictimPose(pose,view,2,view.PanicStart+.6f)&&vm.State=="kernel-victim-kicked"&&pose.Rotation.All(q=>float.IsFinite(q.W)),"the victim tumbles over its own pose");
            Check(!vm.VictimPose(pose,view,3,view.PanicStart+.6f)&&!vm.VictimPose(pose,view,0,view.PanicStart+.6f),"only the victim");
            var flare=NativeCyberLordLook.FlareAt(10.05f,10,0,10.38f);Check(flare.Left>.5f&&flare.Right<flare.Left,"the jab flares the lead fist");
            var cross=NativeCyberLordLook.FlareAt(10.05f,10,1,10.38f);Check(cross.Right>.5f&&cross.Left<cross.Right,"the cross flares the rear fist");
            var gone=NativeCyberLordLook.FlareAt(11,10,0,10.38f);Check(gone.Left<.05f&&gone.Right<.05f&&gone.Charge<.05f,"the flare and charge fade");
            Near(NativeCyberLordLook.BodySolid(view,view.PanicStart+1.6f),0,"the caster is glitched out through the fork bomb",.05f);Near(NativeCyberLordLook.BodySolid(view,view.PanicStart+2.2f),1,"and back for the slam");
            var dash=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.DataDash(dash,5);var dv=new CyberLordView();CyberLordFeed.Read(dash,dv);
            Check(NativeCyberLordLook.BodySolid(dv,dv.Release[0]+.1f)<.9f&&NativeCyberLordLook.BodySolid(dv,4.9f)>=.999f&&NativeCyberLordLook.BodySolid(dv,dv.RecoverUntil[0]+.5f)>=.999f,"the dash body goes translucent only for the dash");
        }
        yield return $"Cyber Lord poses: {count-c0} checks ({NativePackCyberLord.Clips.Length} pack takes are the exporter's; every take's impact lands on its replicated hit; the combo chains; the sequence, victim tumble, wall-splat, whiff and early end; the fighter rig and first person stay finite)";

        // ------------------------------------------------------------------------------------------------------------------
        // 3. Replicated data only: a copied actor draws the same frame, in any order
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;var (actors,events)=Everything();var sink=new Sink();var scene=new Scene{LocalId=0};
        var json=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
        List<Actor> Copies(){return actors.Select(a=>a.ViewCopy()).ToList();}
        List<Actor> Wire(){return actors.Select(a=>{var c=a.ViewCopy();if(c.Cyber!=null)c.Cyber=JsonSerializer.Deserialize<NativeCyberKit>(JsonSerializer.Serialize(a.Cyber,json),json);if(c.Kernel!=null)c.Kernel=JsonSerializer.Deserialize<NativeKernelPanic>(JsonSerializer.Serialize(a.Kernel,json),json);return c;}).ToList();}
        var times=Enumerable.Range(0,121).Select(i=>8f+i*.042f).ToArray();var first=new Dictionary<float,(int,double,int,double)>();var fxA=new NativeCyberLordFx();int drawn=0,ghosted=0;
        foreach(float t in times){var f=Frame(fxA,scene,actors,events,t,sink);first[t]=f;if(f.count>0)drawn++;if(f.ghosts>0)ghosted++;}
        Check(drawn==times.Length&&ghosted>10,"the busy timeline draws every frame ("+drawn+") with ghosts on "+ghosted);
        foreach(var (label,make) in new (string,Func<List<Actor>>)[]{("a copied actor",Copies),("the kit and Astra's Kernel through JSON",Wire)})
        {
            var fxB=new NativeCyberLordFx();var other=make();
            foreach(float t in times){var f=Frame(fxB,scene,other,events,t,sink);Check(f.Equals(first[t]),label+" draws the same frame at "+t.ToString("0.000"));}
        }
        {
            // Shuffled and repeated (a fresh effects object per pass sees the same events): the frame is a function of the data.
            var rng=new Random(2026);var order=times.OrderBy(_=>rng.Next()).ToArray();var fxC=new NativeCyberLordFx();fxC.Observe(events,times[0]);
            foreach(float t in order){scene.List.Clear();scene.List.AddRange(actors);sink.Reset();fxC.Draw(sink,scene,t,1,false);Check(sink.Count==first[t].Item1&&sink.Hash==first[t].Item2,"shuffled order draws the same frame at "+t.ToString("0.000"));}
            // The same for spectators and the killcam: a replay restarting the event ids (and the clock) starts over cleanly.
            var fxD=new NativeCyberLordFx();Frame(fxD,scene,actors,events,12.9f,sink);float rewind=times[33];var back=Frame(fxD,scene,actors,events,rewind,sink);
            Check(back.Equals(first[rewind]),"a rewound killcam replay draws the frame it drew live ("+back.count+" vs "+first[rewind].Item1+")");
        }
        {
            // The same pose for a copied actor: two rigs, the original and its copy, step identically.
            var a=actors[0];var b=a.ViewCopy();var ma=new MeleeMotion();var mb=new MeleeMotion();var ca=new CyberLordMotion();var cb=new CyberLordMotion();var mv=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true};
            for(float t=8;t<12.5f;t+=1/60f){foreach(var (m,c,act) in new[]{(ma,ca,a),(mb,cb,b)}){m.Rig.GroupPosition=new Vector3(0,.865f,0);m.FighterBase(mv,t,1/60f);c.Style(m,act,t,false);m.FighterFinish(mv,t,1/60f);}
                Check(ma.Rig.Q.SequenceEqual(mb.Rig.Q)&&ca.State==cb.State,"a copied actor poses the same at "+t.ToString("0.00"));}
            var pa=new CapturedRig.Pose();var pb=new CapturedRig.Pose();ca.FirstPerson(pa,a,10.3f);cb.FirstPerson(pb,b,10.3f);Check(pa.Rotation.SequenceEqual(pb.Rotation),"and the same first-person fists");
        }
        yield return $"Cyber Lord replication: {count-c0} checks ({times.Length} busy frames: a copied actor and the kit through JSON draw the identical effects and ghosts, shuffled order too; copied actors pose identically)";

        // ------------------------------------------------------------------------------------------------------------------
        // 4. Effect lifecycles
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;var fx=new NativeCyberLordFx();var empty=new List<ArenaEvent>();
        int Count(Actor a,float t,List<Actor> extra=null,List<ArenaEvent> evs=null,float q=1,bool low=false){var list=new List<Actor>{a};if(extra!=null)list.AddRange(extra);var f=new NativeCyberLordFx();return Frame(f,scene,list,evs??empty,t,sink,q,low).count;}
        var target=Sword(2,new Vector3(0,0,5),180);var targets=new List<Actor>{target};
        foreach(var (name,make,from,live,end) in new (string,Action<Actor>,float,float,Func<CyberLordView,float>)[]{
            ("Data Dash",a=>CyberLordFixture.DataDash(a,50),50,50.6f,v=>v.TrailUntil+.2f),
            ("Firewall",a=>CyberLordFixture.Firewall(a,50),50,52,v=>v.WallUntil+CyberLordSpec.WallCollapse),
            ("Packet Burst",a=>CyberLordFixture.PacketBurst(a,50),50,50.2f,v=>v.RecoverUntil[2]+.6f),
            ("Siphon Link",a=>CyberLordFixture.SiphonLink(a,2,50),50,51.5f,v=>Math.Max(v.LinkUntil+.3f,v.RecoverUntil[3])),
            ("System Crash",a=>CyberLordFixture.SystemCrash(a,50),50,50.9f,v=>Math.Max(v.CrashUntil+.3f,v.RecoverUntil[4])),
            ("Kernel Panic",a=>CyberLordFixture.KernelPanic(a,50,2,new Vector3(0,0,1.4f)),50,52,v=>v.PanicStart+KernelPanicPath.End+.6f),
            ("Kernel Panic whiff",a=>CyberLordFixture.KernelPanic(a,50,0,Vector3.Zero,connect:false),50,50.4f,v=>v.RecoverUntil[5]+.4f)})
        {
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);make(a);var v=new CyberLordView();CyberLordFeed.Read(a,v);float stop=end(v);
            Check(Count(a,from-.01f,targets)==0,name+": nothing before its cast");Check(Count(a,live,targets)>0,name+": drawn while live");Check(Count(a,stop+.01f,targets)==0,name+": gone after "+(stop-from).ToString("0.00")+" s");
            int peak=0;for(float t=from;t<stop;t+=1/30f)peak=Math.Max(peak,Count(a,t,targets));Check(peak<=900,name+": at most "+peak+" draws a frame");
        }
        // The combo's sparks, mana flow and the palm cone live with their event / hit; the firewall's ripple and burn flicker,
        // the siphon's heal glint, the crash's glitched stun (RGB-split ghosts) only for their events.
        {
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.Combo(a,2,60);var v=new CyberLordView();CyberLordFeed.Read(a,v);var three=Sword(3,new Vector3(0,0,1.5f),180);var both=new List<Actor>{three};
            var hit=new List<ArenaEvent>{Ev(1,EventKind.Hit,v.ComboHit,1,3,new Vector3(0,1.2f,1.2f),"cyber-palm",22)};
            Check(Count(a,v.ComboHit+.05f,both,hit)>Count(a,v.ComboHit+.05f,both),"the palm's hit adds sparks and the mana flow");
            Check(Count(a,v.ComboHit+NativeCyberLordFx.PalmSeconds+NativeCyberLordFx.ManaFlowSeconds+.2f,both,hit)==Count(a,v.ComboHit+NativeCyberLordFx.PalmSeconds+NativeCyberLordFx.ManaFlowSeconds+.2f,both),"and they fade");
            Check(Count(a,v.ComboHit+.05f,both)>Count(a,v.ComboHit-.01f,both),"the palm cone on the replicated hit");
            // (Astra's events: the wall's burn is a Hit "firewall"; the Siphon's heal a plain Heal of the caster in its drain hit's tick.)
            foreach(var (detail,kind,life) in new[]{("firewall",EventKind.Absorb,.6f),("firewall",EventKind.Hit,.35f),("siphon-link",EventKind.Heal,.55f),("data-dash",EventKind.AbilityHit,NativeCyberLordFx.SparkSeconds),("packet-burst",EventKind.AbilityHit,NativeCyberLordFx.SparkSeconds)})
            {
                var b=CyberLordFixture.Pilot(1,Vector3.Zero,0);var e=kind==EventKind.Heal?new List<ArenaEvent>{Ev(1,EventKind.Hit,70,1,3,new Vector3(0,1,1.5f),detail,2),Ev(2,EventKind.Heal,70,1,1,new Vector3(0,1,0),null,1)}:new List<ArenaEvent>{Ev(1,kind,70,1,3,new Vector3(0,1,1),detail)};
                Check(Count(b,70.05f,both,e)>0&&Count(b,70+life+.05f,both,e)==0&&Count(b,69.9f,both,e)==0,detail+" "+kind+" lives "+life+" s");
            }
            {
                var b=CyberLordFixture.Pilot(1,Vector3.Zero,0);var lone=new List<ArenaEvent>{Ev(1,EventKind.Heal,70,1,1,new Vector3(0,1,0),null,1)};var cast=new List<ArenaEvent>{Ev(1,EventKind.AbilityCast,70,1,0,new Vector3(0,1,0),"firewall",0)};
                Check(Count(b,70.05f,both,lone)==0&&Count(b,70.05f,both,cast)==0,"a heal without its drain hit, and a cast's AbilityCast, draw nothing");
            }
            var crash=new List<ArenaEvent>{Ev(1,EventKind.AbilityHit,80,1,3,new Vector3(0,1,1.5f),"system-crash",25)};var f=new NativeCyberLordFx();var lst=new List<Actor>{CyberLordFixture.Pilot(1,Vector3.Zero,0),three};
            var g0=Frame(f,scene,lst,crash,80.3f,sink);Check(g0.ghosts==2&&Enumerable.Range(0,f.Ghosts).All(i=>f.Ghost(i).Kind==CyberGhost.Split&&f.Ghost(i).Actor==3),"the glitched stun splits its victim RGB");
            Check(Frame(f,scene,lst,crash,80+CyberLordSpec.CrashStun+.05f,sink).ghosts==0,"for the stun's 1 s only");
        }
        {
            // Records: idempotent per frame, capped, expiring.
            var f=new NativeCyberLordFx();var many=Enumerable.Range(1,300).Select(i=>Ev(i,EventKind.Hit,90+i*.001f,1,3,Vector3.Zero,"cyber-jab")).ToList();
            f.Observe(many,90.4f);int n=f.Records.Count;f.Observe(many,90.4f);Check(n==NativeCyberLordFx.MaxRecords&&f.Records.Count==n,"records cap at "+NativeCyberLordFx.MaxRecords+" and a second read adds nothing");
            f.Observe(many,90.4f+NativeCyberLordFx.RecordSeconds+.4f);Check(f.Records.Count==0,"records expire after "+NativeCyberLordFx.RecordSeconds+" s");
            f.Observe(new List<ArenaEvent>{Ev(1,EventKind.Hit,10,1,3,Vector3.Zero,"sword"),Ev(2,EventKind.Block,10,1,3,Vector3.Zero,"cyber-jab")},10);Check(f.Records.Count==0,"other Details and kinds are ignored");
        }
        {
            // Kernel Panic: three clones strike (one at a time) through the fork bomb and only then; the code column falls at step 5.
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(a,100,2,new Vector3(0,0,1.4f));var v=new CyberLordView();CyberLordFeed.Read(a,v);var vic=Sword(2,new Vector3(0,0,1.4f),180);
            var f=new NativeCyberLordFx();var lst=new List<Actor>{a,vic};var seenClone=new HashSet<int>();float firstClone=-1,lastClone=-1;
            for(float age=-.6f;age<KernelPanicPath.End;age+=1/120f){Frame(f,scene,lst,empty,v.PanicStart+age,sink);for(int i=0;i<f.Ghosts;i++){var g=f.Ghost(i);if(g.Kind!=CyberGhost.Clone)continue;seenClone.Add(g.Index);if(firstClone<0)firstClone=age;lastClone=age;Check(g.Opacity>0&&g.Opacity<=1&&g.Step>=0&&g.Step<3,"clone ghost sane");}}
            Check(seenClone.SetEquals(new[]{0,1,2})&&firstClone>=KernelPanicPath.Clones[0]-.17f&&lastClone<=KernelPanicPath.Clones[2]+.25f,"three clones strike through the fork bomb only ("+firstClone.ToString("0.00")+" .. "+lastClone.ToString("0.00")+" s)");
            var clone=new Actor();var cg=new CyberGhost{Kind=CyberGhost.Clone,Index=1,Step=2,Position=new Vector3(1,0,1),Yaw=30,PoseAge=.16f};NativeCyberLordFx.CloneActor(clone,a,cg);var cv=new CyberLordView();
            Check(CyberLordFeed.Read(clone,cv)&&cv.ComboStep==2&&clone.Id<0&&!ReferenceEquals(clone.Cyber,a.Cyber),"a clone stand-in is its own Cyber Lord");Near(NativeCyberLordFx.CloneTime(cg),cv.ComboHit,"a clone's strike lands on its take's hit at .16 s",1e-3f);
        }
        {
            // The look forms from light: nothing at the start, every piece once formed, never past capacity.
            var pieces=new CyberPiece[NativeCyberLordLook.Capacity];var flare=new NativeCyberLordLook.Flare();
            int at0=NativeCyberLordLook.Pieces(10,10,flare,false,1,1,false,pieces),mid=NativeCyberLordLook.Pieces(10+NativeCyberLordLook.FormSeconds*.5f,10,flare,false,1,1,false,pieces),full=NativeCyberLordLook.Pieces(12,10,flare,false,1,1,false,pieces);
            Check(at0<mid&&mid<full&&full<=NativeCyberLordLook.Capacity&&full>=NativeCyberLordLook.Count*.8f,"the gauntlets form edge by edge ("+at0+" / "+mid+" / "+full+" pieces)");
            int fpn=NativeCyberLordLook.Pieces(12,10,flare,true,1,1,false,pieces);bool viewOnly=true;for(int i=0;i<fpn;i++)viewOnly&=NativeCyberLordLook.ViewBone(pieces[i].Bone);Check(fpn>0&&fpn<full&&viewOnly,"first person keeps the view-model bones' pieces ("+fpn+")");
            for(int i=0;i<full;i++)Check(float.IsFinite(pieces[i].Position.X)&&pieces[i].Scale.X>0&&Math.Abs(pieces[i].Rotation.Length()-1)<1e-3f,"piece "+i+" sane");
            int ghost=NativeCyberLordLook.Pieces(12,10,flare,false,.3f,1,false,pieces);Check(ghost<=full,"a dithered body shows no more pieces");
        }
        Check(sink.Flats==0,"no glowing piece lies flat on the floor (owner rule: no disc, ring or tile; "+sink.Flats+" found, first a "+sink.FlatWhat+")");
        yield return $"Cyber Lord effect lifecycles: {count-c0} checks (each ability's effects start with its cast and are gone with its window; sparks, ripples, burn flicker, heal glint and the glitched stun live with their events; records cap and expire; the fork-bomb clones; the look forms from light)";

        // ------------------------------------------------------------------------------------------------------------------
        // 5. Tiers
        // ------------------------------------------------------------------------------------------------------------------
        // Reduced effects thin the particles (glows, streaks, smoke, sprites, lines); the hardlight constructs (the wall's cells,
        // bands) keep their shape. The low graphics tier thins both.
        c0=count;long high=0,reduced=0,low=0,all=0,allLow=0;int voxHigh=0,voxLow=0;
        foreach(float t in times)
        {
            var f1=new NativeCyberLordFx();Frame(f1,scene,actors,events,t,sink,1,false);high+=sink.Particles;all+=sink.Count;voxHigh+=sink.Voxels;
            var f2=new NativeCyberLordFx();Frame(f2,scene,actors,events,t,sink,.3f,false);reduced+=sink.Particles;
            var f3=new NativeCyberLordFx();Frame(f3,scene,actors,events,t,sink,1,true);low+=sink.Particles;allLow+=sink.Count;voxLow+=sink.Voxels;
        }
        Check(reduced<high*.7&&low<high*.8&&allLow<all*.6&&voxLow<voxHigh*.6,"reduced effects ("+(100*reduced/high)+"% of the particles) and the low tier ("+(100*low/high)+"% of the particles, "+(100*allLow/all)+"% of all draws, voxels "+voxLow+"/"+voxHigh+") thin the effects");
        {
            // Reduced effects never strobe: the guard, the cut-in sparks and the splits hold steady frame to frame.
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);a.BlockAt=1;a.BlockUntil=3;var lst=new List<Actor>{a};double prev=double.NaN;int jumps=0;
            for(float t=1.5f;t<2.5f;t+=1/60f){var f=new NativeCyberLordFx();Frame(f,scene,lst,empty,t,sink,.3f,false);if(!double.IsNaN(prev)&&Math.Abs(sink.Hash-prev)>Math.Abs(prev)*.02+1)jumps++;prev=sink.Hash;}
            Check(jumps==0,"the reduced guard holds steady ("+jumps+" jumps)");
            var pieces=new CyberPiece[NativeCyberLordLook.Capacity];Check(NativeCyberLordLook.Pieces(12,10,default,false,1,1,true,pieces)<NativeCyberLordLook.Pieces(12,10,default,false,1,1,false,pieces),"the low tier drops the look's fine pieces");
        }
        var cut=KernelPanicCutIn.At(.02f,true);var cutFull=KernelPanicCutIn.At(.02f,false);Check(cut.Flash==0&&cut.Split==0&&cut.Tear==0&&cutFull.Flash>0&&cutFull.Split>0,"reduced cut-in: no flash, RGB split or tear");
        for(float t=0;t<.6f;t+=.01f){var s=KernelPanicCutIn.At(t,true);Check(s.Flash==0&&s.Split==0&&s.Tear==0,"reduced cut-in steady at "+t);}
        {
            var g=KernelPanicFeel.Screen(view,view.PanicStart+1.92f,true,false);var gf=KernelPanicFeel.Screen(view,view.PanicStart+1.92f,false,false);var gl=KernelPanicFeel.Screen(view,view.PanicStart+1.5f,false,true);
            Check(g.Tear==0&&g.Noise==0&&g.Blocks==0&&gf.Tear>0&&g.Split<gf.Split,"reduced effects: the screen shifts colour without tearing");Check(gl.Blocks==0&&gl.Passes==1,"the low tier: one pass, no blocks");
            var outside=KernelPanicFeel.Screen(view,view.PanicConnect-.1f,false,false);Check(outside.Split==0&&outside.Tear==0&&outside.Chroma==0,"no glitch before the connect");
        }
        yield return $"Cyber Lord tiers: {count-c0} checks (reduced effects draw {100*reduced/high}% and the low tier {100*low/high}% of the full particles, the low tier {100*allLow/all}% of all draws; reduced never strobes, flashes or tears)";

        // ------------------------------------------------------------------------------------------------------------------
        // 6. The camera, the participants' clock and the cut-in
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        Check(!KernelPanicCamera.Active(view,view.PanicConnect-.01f)&&KernelPanicCamera.Active(view,view.PanicConnect)&&KernelPanicCamera.Active(view,view.PanicStart+3)&&!KernelPanicCamera.Active(view,view.PanicStart+KernelPanicPath.End),"the camera directs from the connect to the end");
        foreach(bool victim in new[]{false,true})foreach(bool red in new[]{false,true})foreach(bool shake in new[]{false,true})
        {
            Vector3 prevPos=default;float maxJump=0,maxBlendStep=0,prevBlend=0,jumpAt=0;int cuts=0;bool firstShot=true;
            for(float t=view.PanicConnect;t<view.PanicStart+KernelPanicPath.End;t+=1/120f)
            {
                var s=KernelPanicCamera.At(view,KernelPanicCamera.Clock(view,t,red),victim,red,shake,16/9f);string w=(victim?"victim":"caster")+(red?" reduced":"")+(shake?" shake":"")+" at "+(t-view.PanicStart).ToString("0.00");
                Check(float.IsFinite(s.Position.X)&&float.IsFinite(s.Look.Y)&&s.Fov>20&&s.Fov<=75&&s.Blend>=0&&s.Blend<=1,w+": a sane shot");Check(s.Position.Y>.15f,w+": above the floor ("+s.Position.Y.ToString("0.00")+")");
                Check(Vector3.Distance(s.Position,s.Look)>1,w+": not inside the subject");
                if(!shake||red)Check(s.Shake==0,w+": no shake without the setting or with reduced effects");if(red)Check(s.Roll==0,w+": no roll with reduced effects");
                if(!firstShot){float jump=Vector3.Distance(prevPos,s.Position);if(jump>maxJump)jumpAt=t-view.PanicStart;maxJump=Math.Max(maxJump,jump);if(jump>.5f)cuts++;maxBlendStep=Math.Max(maxBlendStep,Math.Abs(s.Blend-prevBlend));}
                prevPos=s.Position;prevBlend=s.Blend;firstShot=false;
            }
            Check(maxBlendStep<.12f,"the camera blends in and out ("+maxBlendStep.ToString("0.000")+" a frame)");
            if(red)Check(cuts==0,(victim?"victim":"caster")+" reduced: no hard cuts (largest step "+maxJump.ToString("0.00")+" m at "+jumpAt.ToString("0.000")+" s)");else Check(cuts>=3,(victim?"victim":"caster")+": cuts between its shots ("+cuts+")");
        }
        {
            var c=KernelPanicCamera.At(view,view.PanicStart+1.1f,false,false,true,16/9f);var v2=KernelPanicCamera.At(view,view.PanicStart+1.1f,true,false,true,16/9f);Check(Vector3.Distance(c.Position,v2.Position)>1,"the victim gets its own angle on the hammer");
            var narrow=KernelPanicCamera.At(view,view.PanicStart+.2f,false,false,true,1f);var wide=KernelPanicCamera.At(view,view.PanicStart+.2f,false,false,true,16/9f);Check(Vector3.Distance(narrow.Position,narrow.Look)>Vector3.Distance(wide.Position,wide.Look),"a narrow screen pulls back");
        }
        foreach(bool red in new[]{false,true})
        {
            float prev=float.NegativeInfinity,worstLag=0;
            for(float t=view.PanicConnect-.5f;t<view.PanicStart+KernelPanicPath.End+.5f;t+=1/480f)
            {
                float c=KernelPanicFeel.Clock(view,t,red);Check(float.IsFinite(c)&&c>=prev-1e-5f,"the participants' clock never runs backwards at "+(t-view.PanicStart).ToString("0.000")+(red?" (reduced)":""));Check(c<=t+1e-5f,"the clock never runs ahead");
                worstLag=Math.Max(worstLag,t-c);prev=c;
                if(t<view.PanicStart-.02f||t>=view.PanicStart+KernelPanicPath.Recovery)Check(Math.Abs(c-t)<1e-4f,"the clock is the match's outside the beats at "+(t-view.PanicStart).ToString("0.000"));
            }
            Check(worstLag>.05f&&worstLag<.4f,"hit-stops and the slow beat lag at most "+worstLag.ToString("0.000")+" s"+(red?" (reduced)":""));
            for(int s=1;s<=5;s++){float h=view.PanicStart+KernelPanicPath.HitAge(s);Check(KernelPanicFeel.Clock(view,h+.02f,red)<h+.02f-1e-3f,"step "+s+" hit-stops"+(red?" (reduced)":""));}
        }
        Check(KernelPanicFeel.Participant(view,1)&&KernelPanicFeel.Participant(view,2)&&!KernelPanicFeel.Participant(view,3)&&!KernelPanicFeel.Participant(view,0),"only the caster and the victim are participants");
        Check(KernelPanicCutIn.Splashes(view,1)&&KernelPanicCutIn.Splashes(view,2)&&!KernelPanicCutIn.Splashes(view,3),"the splash for the caster and the victim, the banner for everyone else");
        Check(KernelPanicCutIn.Seconds==CyberLordSpec.CutIn&&KernelPanicCutIn.At(-.01f,false).Alpha==0&&KernelPanicCutIn.At(.3f,false).Alpha>.9f&&KernelPanicCutIn.At(CyberLordSpec.CutIn,false).Alpha==0,"the splash spans the .6 s cut-in");
        Check(KernelPanicCutIn.Banner(.5f)>.9f&&KernelPanicCutIn.Banner(KernelPanicCutIn.BannerSeconds)==0&&KernelPanicCutIn.Banner(-.1f)==0,"the banner shows 1.5 s");
        Check(KernelPanicCutIn.BannerTop>=46&&KernelPanicCutIn.BannerTop+KernelPanicCutIn.BannerHeight<900*.12f&&KernelPanicCutIn.BannerWidth<1600*.35f,"the banner stays at the top, clear of the crosshair");
        var chars=new char[KernelPanicCutIn.Title.Length];Check(new string(chars,0,KernelPanicCutIn.Typed(KernelPanicCutIn.Title.Length,3,chars))==KernelPanicCutIn.Title&&new string(chars,0,KernelPanicCutIn.Typed(4,3,chars)).StartsWith("KERN")&&KernelPanicCutIn.Title=="KERNEL PANIC","the title glitch-types to KERNEL PANIC");
        {
            // Third parties keep their own camera and clock: the runtime directs only for a participant, the pose clock warps only for one.
            string pres=File.ReadAllText(Path.Combine(runtime,"NativeCyberLordPresentation.cs"));
            Check(pres.Contains("KernelPanicFeel.Participant(v,LocalId)?KernelPanicFeel.Clock(")&&Regex.IsMatch(pres,@"KernelPanicMine\(")&&pres.Contains("internal bool KernelPanicDirect("),"the runtime warps and directs only for the participants");
            Check(File.ReadAllText(Path.Combine(runtime,"NativeCinematicCamera.cs")).Contains("presentation.KernelPanicDirect(arena,view,presentation.CinematicClock(arena),presentation.CameraShake,presentation.WeaponEffectsQuality<.5f)"),"the cinematic camera hands Kernel Panic the settings");
            string game=File.ReadAllText(Path.Combine(runtime,"ConversionGame.cs"));Check(game.Contains("KernelPanicSplash(width,height);")&&game.Contains("KernelPanicBanner();")&&game.Contains("CameraShake"),"the splash and banner hooks");
        }
        yield return $"Cyber Lord camera and clock: {count-c0} checks (the caster's and victim's cameras stay sane, above the floor, blend in and out, cut only without reduced effects, shake only with the setting; the participants' clock is monotone and back on time by the recovery; the cut-in spans .6 s, the banner stays clear of the crosshair)";

        // ------------------------------------------------------------------------------------------------------------------
        // 7. DEV gate, HUD, entrance, audio, icon
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        var proposed=new ContentWeapon{id=CyberLordSpec.Id,@class="melee",devOnly=true};Check(NativeLoadoutText.Role(proposed).StartsWith("DEV",StringComparison.Ordinal),"the loadout role reads DEV");Check(NativeLoadoutText.ClassOf(proposed,true)=="dev"&&NativeLoadoutText.InClass(proposed,"dev",true,true)&&!NativeLoadoutText.InClass(proposed,"dev",false,true),"as a devOnly weapon it sits on the DEV tile, for DEV access only");
        Check(NativeLocker.DevItem(CyberLordSpec.Entrance)&&NativeCosmeticGate.Restricted(CyberLordSpec.Entrance)&&!NativeCosmeticGate.Allowed(NativeAccess.Guest,"intro",CyberLordSpec.Entrance,0),"the entrance carries the DEV badge and is gated (a guest cannot pick it)");
        var scoped=NativeAccess.ScopedWeapon(NativeAccess.Guest,CyberLordSpec.Id,0);Check(!NativeCosmeticGate.Allowed(scoped,"intro",CyberLordSpec.Entrance,0),"no grant without DEV access unlocks it");
        Check(!string.IsNullOrEmpty(NativeLocker.IntroText(CyberLordSpec.Entrance))&&NativeLocker.IntroIcon(CyberLordSpec.Entrance)=="diamond"&&NativeIntroRules.StyleName(CyberLordSpec.Entrance)=="DEV · Hardlight Boot","the Locker entry: text, icon and DEV name");
        Check(NativeIntroRules.Style(new NativeSeat{spec=CyberLordSpec.Id})==CyberLordSpec.Entrance&&NativeIntroRules.Style(new NativeSeat{spec="sword",intro=CyberLordSpec.Entrance,overrideIntro=true})!=CyberLordSpec.Entrance,"a Cyber Lord plays its entrance; nobody without DEV access can pick it");
        Check(NativeSoundKeys.Intro(CyberLordSpec.Entrance)==null&&NativeIntroRules.IntroSting(CyberLordSpec.Entrance)=="audio/intros/pulse-wave"&&NativeEntranceDirector.Directed(CyberLordSpec.Entrance),"the entrance is directed on the shipped pulse-wave bed with no music key (dev- by contract)");
        {
            string fight=File.ReadAllText(Path.Combine(runtime,"NativeFightHud.cs")),combat=File.ReadAllText(Path.Combine(runtime,"NativeCombatHud.cs")),screen=File.ReadAllText(Path.Combine(runtime,"NativeCyberLordScreen.cs")),pres=File.ReadAllText(Path.Combine(runtime,"NativeCyberLordPresentation.cs"));
            Check(fight.Contains("if(CyberLordFeed.Handles(p.Seat.spec))CyberLordCards(p);")&&combat.Contains("CyberLordFeed.Handles(p.Seat.spec)"),"the HUD cards and mana rows only for a Cyber Lord pilot");
            Check(screen.Contains("var v=presentation?.CyberLordViewOf(p.Pose?.Id??0);if(v==null)return;")&&pres.Contains("if(!CyberLordFeed.Handles(a))continue;"),"the views (cards, cut-in, banner) exist only for Cyber Lord pilots");
            Check(Regex.Matches(screen,@"KernelPanicCutInView\(").Count>=2&&screen.Contains("if(v==null||KernelPanicCutIn.Splashes(v,arena.LocalId))return;"),"the cut-in and banner only for a live Kernel Panic");
            Check(File.ReadAllText(Path.Combine(runtime,"NativeProfileUI.cs")).Contains("{\"dev-cyberlord\",\"DEV · Hardlight Boot\"}"),"the Locker lists it with the DEV label (gated by NativeCosmeticGate)");
        }
        {
            var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var v=new CyberLordView();CyberLordFixture.DataDash(a,10);CyberLordFeed.Read(a,v);
            Check(NativeCyberLordHud.Of(v,0,10.1f,100).State=="ACTIVE"&&NativeCyberLordHud.Of(v,0,13,100).State=="3.0s"&&NativeCyberLordHud.Of(v,0,17,10).State=="LOW MANA"&&NativeCyberLordHud.Of(v,0,17,100).Ready&&NativeCyberLordHud.Of(v,1,17,100).State=="READY","a card: active, counting down, low mana, ready");
            Check(NativeCyberLordHud.Of(v,0,13,5).Low&&NativeCyberLordHud.Of(v,0,13,100).Dial>.4f&&NativeCyberLordHud.Of(v,0,13,100).Dial<.6f,"the dial and the low-mana tint");
            Check(NativeCyberLordHud.Order.SequenceEqual(new[]{0,1,2,3,4,5})&&NativeCyberLordHud.Labels[5]=="4 · KERNEL PANIC"&&NativeCyberLordHud.Labels[0]=="Q · DATA DASH","cards Q, E, 1-4 in drawer order");
            var lucide=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json"));foreach(var icon in NativeCyberLordHud.Icons.Append(NativeLocker.IntroIcon(CyberLordSpec.Entrance)))Check(lucide.Contains("\"id\":\""+icon+"\"")||lucide.Contains("\"id\": \""+icon+"\""),"the card icon "+icon+" ships");
            Check(NativeCyberLordHud.Of(view,5,view.PanicStart+1,100).State=="PANIC"&&NativeCyberLordHud.ReadyAt(hv,5)==hv.Ready[5]&&Math.Abs(hv.Ready[5]-(40+CyberLordSpec.Cooldowns[5]*.5f))<1e-4f,"Kernel Panic's card: PANIC through the sequence, a whiff refunding half");
            Check(NativeCyberLordHud.GainFlash(1.1f,1)>0&&NativeCyberLordHud.GainFlash(1.4f,1)==0&&NativeCyberLordHud.Time(2.95f)=="3.0s"&&NativeCyberLordHud.Time(0)=="0.0s","the mana gain flash and the countdown text");
        }
        {
            var o=new NativeKitSound[NativeCyberLordSounds.Max];var clips=new HashSet<string>(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),"*.wav").Select(Path.GetFileNameWithoutExtension));
            foreach(var k in NativeCyberLordSounds.Keys)Check(NativeSoundKeys.Known(k),"bank key "+k+" exists");foreach(var c in NativeCyberLordSounds.Clips)Check(clips.Contains(c),"fallback clip "+c+" ships");
            int cues=0;foreach(var src in new[]{view,hv,ev2})for(int slot=0;slot<NativeCyberLordSounds.Slots;slot++){int n=NativeCyberLordSounds.State(src,slot,o,out float at);if(n>0)Check(float.IsFinite(at),"cue "+slot+" has its moment");for(int i=0;i<n;i++){cues++;Check(NativeCyberLordSounds.Keys.Contains(o[i].Key)&&(o[i].Clip==null||NativeCyberLordSounds.Clips.Contains(o[i].Clip))&&o[i].Gain>0&&o[i].Gain<=1,"state cue "+o[i].Key);}}
            foreach(EventKind kind in new[]{EventKind.Hit,EventKind.AbilityHit,EventKind.Heal,EventKind.Absorb,EventKind.BurnTick})foreach(CyberHit h in Enum.GetValues(typeof(CyberHit)))foreach(bool me in new[]{false,true})
            {int n=NativeCyberLordSounds.Event(kind,h,me,o);for(int i=0;i<n;i++){cues++;Check(NativeCyberLordSounds.Keys.Contains(o[i].Key)&&(o[i].Clip==null||NativeCyberLordSounds.Clips.Contains(o[i].Clip)),"event cue "+o[i].Key);}}
            int ended=0;for(int slot=NativeCyberLordSounds.Blink2;slot<=NativeCyberLordSounds.Reappear;slot++)ended+=NativeCyberLordSounds.State(ev2,slot,o,out _);Check(ended<4*1+2,"an early end cuts the choreography's later cues");
            Check(NativeCyberLordSounds.State(hv,NativeCyberLordSounds.Connect,o,out _)==0&&NativeCyberLordSounds.State(hv,NativeCyberLordSounds.FourWhiff,o,out _)>0,"a whiff sounds a miss, not the connect");
            Check(cues>30,"the routing plays "+cues+" cues on existing keys");
        }
        yield return $"Cyber Lord DEV gate, HUD, entrance and audio: {count-c0} checks (DEV tile, badge and gate on the entrance; the cards, mana rows, cut-in and banner only for a Cyber Lord; the cards' states; {NativeCyberLordSounds.Keys.Length} existing bank keys and {NativeCyberLordSounds.Clips.Length} shipped fallback clips)";

        // ------------------------------------------------------------------------------------------------------------------
        // 9. The idle stance and its glitch (Core/CyberLordIdle.cs; owner: "a idle pose ... slight glitch effects", then "Change
        //    Cyberlord idle to be similar to the sword idle, but more "psycho" appearance, like they're twitching")
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            // A Cyber Lord on the fighter route (MeleeMotion.FighterBase, the style layer, FighterFinish), as the arena poses it.
            Actor pilot=null;var mo=new MeleeMotion();var cy=new CyberLordMotion();var mv=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true};
            void Fresh(string hat){pilot=CyberLordFixture.Pilot(1,Vector3.Zero,0);pilot.Hat=hat;mo=new MeleeMotion();cy=new CyberLordMotion();mv=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true};}
            string PoseAt(float t)
            {
                mv.Vel=new Vector3(-pilot.Velocity.x,pilot.Velocity.y,pilot.Velocity.z);mv.Grounded=pilot.Grounded;mv.BlockTime=t<pilot.BlockUntil?pilot.BlockUntil-t:0;mv.LastBlock=pilot.BlockAt;mv.HitStunUntil=pilot.StunUntil;
                mo.Rig.GroupPosition=new Vector3(0,.865f,0);mo.Rig.GroupYaw=0;mo.FighterBase(mv,t,1/60f);var st=cy.Style(mo,pilot,t,pilot.StunUntil>t);mo.FighterFinish(mv,t,1/60f);return st;
            }
            float Hold(float from,float to){for(float t=from;t<to-1e-4f;t+=1/60f)PoseAt(t);return cy.IdleWeight;}
            // A pilot already standing still (a late join, the killcam's fresh rig) shows the stance at once; its state is cyber-idle.
            Fresh(NativeSheldonHead.Hat);Check(PoseAt(.5f)=="cyber-idle"&&cy.IdleWeight>.999f,"a still pilot on a fresh rig stands in the idle at once");
            // Every action blends it out within BlendOut (plus a frame), and it waits the still time after the move has settled
            // and blended out (the presentation's own settle) before it returns.
            foreach(var (label,act,end) in new (string,Action<float>,float)[]{
                ("a step",t=>{pilot.Velocity=new V3(0,0,2.5f);},3.5f),("a jab",t=>CyberLordFixture.Combo(pilot,0,t),3+CyberLordSpec.ComboDurations[0]+CyberLordMotion.BlendOut),
                ("a cast",t=>CyberLordFixture.Firewall(pilot,t),3+CyberLordSpec.CastSeconds[CyberLordSpec.E]+CyberLordSpec.Settle[CyberLordSpec.E]+CyberLordMotion.BlendOut),("the guard",t=>{pilot.BlockAt=t;pilot.BlockUntil=t+.5f;},3.5f),
                ("a roll",t=>pilot.RollAt=t,3+pilot.VisualRollDuration),("a hit reaction",t=>{pilot.ReactionAt=t;pilot.ReactionUntil=t+.3f;},3.3f),("a stun",t=>pilot.StunUntil=t+.4f,3.4f),
                ("a jump",t=>pilot.Grounded=false,3.4f),("Kernel Panic",t=>CyberLordFixture.KernelPanic(pilot,t,0,Vector3.Zero,connect:false),3+NativeKernelPanicRules.Windup+NativeKernelPanicRules.WhiffRecovery+CyberLordMotion.BlendOut)})
            {
                Fresh(NativeSheldonHead.Hat);Hold(1,3);Check(cy.IdleWeight>.999f,label+": idle before it");act(3);
                float w=Hold(3,3+CyberLordIdle.BlendOut+1.5f/60);Check(w<=.02f,label+" blends the idle out at once ("+w+")");
                // (The step and the jump end at `end`; the still time counts from the last busy frame.)
                float stop=end;if(label=="a step"){Hold(3+CyberLordIdle.BlendOut+1.5f/60,stop);pilot.Velocity=default;}else if(label=="a jump"){Hold(3+CyberLordIdle.BlendOut+1.5f/60,stop);pilot.Grounded=true;}
                else Hold(3+CyberLordIdle.BlendOut+1.5f/60,stop);
                float settle=label=="a step"?.6f:.05f;
                Check(Hold(stop,stop+CyberLordIdle.Delay-.06f)<=.001f,label+": the stance waits the still time");
                Check(Hold(stop+CyberLordIdle.Delay-.06f,stop+CyberLordIdle.Delay+CyberLordIdle.BlendIn+settle+.1f)>.99f,label+": and blends back after it ("+cy.IdleWeight+")");
            }
            // The pose: the sword's standing idle (hunched, knees bent, the arms out wide) with the hands closed into fists, twitching
            // over the gait's planted legs; bounded joints; the actor is never written.
            {
                Fresh("none");string before=JsonSerializer.Serialize(pilot.ViewCopy(),json);Vector3 l0=default,r0=default;bool has=false;float drift=0,floorGap=0,worstHead=0,worstClav=0;int clenched=0;
                for(float t=1;t<31;t+=1/60f)
                {
                    PoseAt(t);var r=mo.Rig;var la=r.ChainPoint(CapturedRig.LFoot,-1);var ra=r.ChainPoint(CapturedRig.RFoot,-1);
                    if(t>2){if(!has){l0=la;r0=ra;has=true;}drift=Math.Max(drift,Math.Max((la-l0).Length(),(ra-r0).Length()));floorGap=Math.Max(floorGap,Math.Max(Math.Abs(la.Y-l0.Y),Math.Abs(ra.Y-r0.Y)));}
                    var head=r.ChainQuat(CapturedRig.Head,-1);var chest=r.ChainQuat(CapturedRig.Chest,-1);worstHead=Math.Max(worstHead,2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(chest,head)))));
                    foreach(int c in new[]{CapturedRig.LClav,CapturedRig.RClav})worstClav=Math.Max(worstClav,2*(float)Math.Acos(Math.Min(1,Math.Abs(r.Q[c].W))));
                    foreach(var q in r.Q)Check(float.IsFinite(q.W)&&Math.Abs(q.Length()-1)<1e-3f,"finite rotations in the idle");
                    if(cy.IdleGlitch.LClench>0||cy.IdleGlitch.RClench>0)clenched++;
                    if(t>2&&!cy.IdleGlitch.Visible)
                    {
                        // The sword idle's read: the fists below the chest and out wide of the hips.
                        var lf=r.ChainPoint(CapturedRig.LHand,-1);var rf=r.ChainPoint(CapturedRig.RHand,-1);float cY=r.ChainPoint(CapturedRig.Chest,-1).Y;
                        Check(lf.Y<cY&&rf.Y<cY&&lf.X>.22f&&rf.X<-.22f,"the fists hang out wide below the chest, as the sword idle's hands ("+lf+", "+rf+")");
                    }
                }
                Check(drift<.012f&&floorGap<.006f,"the feet stay planted through 30 s of twitching (drift "+(drift*1000).ToString("0.0")+" mm)");
                Check(worstHead<1.1f&&worstClav<.6f,"bounded joints: the head within "+worstHead.ToString("0.00")+" rad of the chest, the shoulders within "+worstClav.ToString("0.00")+" rad");
                Check(clenched>60,"the fists clench (their shells flare: "+clenched+" frames)");
                Check(CyberLordIdlePose.Clench(new CyberIdleGlitch{LClench=1},default).Left>.5f&&CyberLordIdlePose.Clench(default,default).Left==0,"the clench reaches the hardlight look's fist flare");
                Check(JsonSerializer.Serialize(pilot.ViewCopy(),json)==before,"the idle never writes the actor (the hitboxes are the capsule)");
                // The stance crouches over the gait's feet, lower than the fighter stance (the sword idle's bent knees).
                Fresh("none");PoseAt(3);var p0=mo.Rig.ChainPoint(CapturedRig.Pelvis,-1);var a0=mo.Rig.ChainPoint(CapturedRig.LFoot,-1);
                var ref1=CyberLordFixture.Pilot(1,Vector3.Zero,0);var mo2=new MeleeMotion();var cy2=new CyberLordMotion();var mv2=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true};
                ref1.AttackAt=2.95f;ref1.AttackUntil=3.5f;mo2.Rig.GroupPosition=new Vector3(0,.865f,0);mo2.FighterBase(mv2,3,1/60f);cy2.Style(mo2,ref1,3,false);mo2.FighterFinish(mv2,3,1/60f);
                Check(cy2.IdleWeight<.001f,"(the reference pilot is busy)");var p1=mo2.Rig.ChainPoint(CapturedRig.Pelvis,-1);var a1=mo2.Rig.ChainPoint(CapturedRig.LFoot,-1);
                Check((a0-a1).Length()<.01f&&p1.Y-p0.Y>.02f&&Math.Abs(p0.Z-p1.Z)<.06f,"the idle crouches over the gait's same feet ("+(p0-p1)+")");
            }
            // The twitches: a pure function of the seed and the time; irregular (no metronome), fast attacks with holds and eased
            // releases, no per-frame noise; every kind occurs; bounded; reduced effects fewer and smaller.
            {
                (int head,int spasm,int clench,int flinch,int shift,int gasp,float headMax,float shoulderMax,float flinchMax,float still,float cv,float attack,int synced) Survey(int seed,bool reduced)
                {
                    int head=0,spasm=0,clench=0,flinch=0,shift=0,gasp=0,stillN=0,n=0,synced=0;float hm=0,sm=0,fm=0,attack=0;var on=new bool[6];var onsets=new List<float>();CyberTwitch prev=default;
                    var flinches=new List<float>();
                    for(float t=0;t<120;t+=1/60f)
                    {
                        var w=CyberLordIdlePose.Twitch(seed,t,reduced);var again=CyberLordIdlePose.Twitch(seed,t,reduced);
                        Check(w.Head==again.Head&&w.Breath==again.Breath&&w.Shift==again.Shift&&w.LClench==again.LClench,"the twitches are a pure function of the seed and the time");
                        float hl=w.Head.Length();bool[] now={hl>.02f,Math.Max(w.LShoulder,w.RShoulder)>.02f,Math.Max(w.LClench,w.RClench)>.1f,w.Flinch>.05f,Math.Abs(w.Shift)>.004f,w.Breath>1.05f};
                        if(now[0]&&!on[0]){head++;onsets.Add(t);}attack=Math.Max(attack,hl-prev.Head.Length());
                        if(now[1]&&!on[1])spasm++;if(now[2]&&!on[2])clench++;if(now[3]&&!on[3]){flinch++;flinches.Add(t);}if(now[4]&&!on[4])shift++;if(now[5]&&!on[5])gasp++;
                        on=now;hm=Math.Max(hm,Math.Max(Math.Abs(w.Head.X),Math.Max(Math.Abs(w.Head.Y),Math.Abs(w.Head.Z))));sm=Math.Max(sm,Math.Max(w.LShoulder,w.RShoulder));fm=Math.Max(fm,w.Flinch);
                        if((w.Head-prev.Head).Length()<1e-5f&&Math.Abs(w.Shift-prev.Shift)<1e-6f)stillN++;n++;prev=w;
                        Check(float.IsFinite(hl)&&w.Breath>=-1e-4f&&w.Breath<1.2f+CyberLordIdlePose.Gasp&&Math.Abs(w.Shift)<=CyberLordIdlePose.ShiftMax*1.5f+1e-4f&&Math.Max(w.LClench,w.RClench)<=1+1e-4f,"bounded twitches");
                    }
                    foreach(float o in flinches){bool any=false;for(float u=o;u<o+.2f;u+=1/60f)any|=CyberLordIdlePose.Glitch(seed,u,1,reduced).Split>0;if(any)synced++;}
                    var gaps=new List<float>();for(int i=1;i<onsets.Count;i++)gaps.Add(onsets[i]-onsets[i-1]);float mean=gaps.Count>0?gaps.Average():0,sd=gaps.Count>1?(float)Math.Sqrt(gaps.Select(g=>(g-mean)*(g-mean)).Average()):0;
                    return (head,spasm,clench,flinch,shift,gasp,hm,sm,fm,(float)stillN/n,mean>0?sd/mean:0,attack,synced);
                }
                var full=Survey(1,false);var soft=Survey(1,true);var other=Survey(2,false);
                Check(full.head>=30&&full.spasm>=12&&full.clench>=20&&full.flinch>=5&&full.shift>=10&&full.gasp>=10,"every twitch occurs in 120 s (head "+full.head+", spasm "+full.spasm+", clench "+full.clench+", flinch "+full.flinch+", shift "+full.shift+", gasp "+full.gasp+")");
                Check(full.cv>.3f,"irregular: the head jerks' intervals vary (coefficient of variation "+full.cv.ToString("0.00")+")");
                Check(full.attack>.05f&&soft.attack<full.attack*.6f,"fast attacks: the head snaps up to "+full.attack.ToString("0.000")+" rad in a 60 Hz frame (reduced "+soft.attack.ToString("0.000")+")");
                Check(CyberLordIdlePose.Envelope(.04f,.04f,.2f,.3f)==1&&CyberLordIdlePose.Envelope(.02f,.04f,.2f,.3f)>.8f&&CyberLordIdlePose.Envelope(.2f,.04f,.2f,.3f)==1&&CyberLordIdlePose.Envelope(.4f,.04f,.2f,.3f)>0&&CyberLordIdlePose.Envelope(.6f,.04f,.2f,.3f)==0,"each twitch: a fast attack, a hold, an eased release");
                Check(full.still>.2f,"no per-frame noise: the head and hips hold dead still between snaps ("+(100*full.still).ToString("0")+"% of frames)");
                Check(full.headMax<=CyberLordIdlePose.HeadTilt*1.4f+.2f&&full.shoulderMax<=CyberLordIdlePose.SpasmShoulder*1.6f+.05f,"bounded: the head within "+full.headMax.ToString("0.00")+" rad, the shoulders "+full.shoulderMax.ToString("0.00"));
                Check(soft.head<full.head&&soft.spasm<full.spasm&&soft.flinch<=full.flinch&&soft.headMax<full.headMax*.6f&&soft.shoulderMax<full.shoulderMax*.6f&&soft.flinchMax<full.flinchMax*.6f,"reduced effects: fewer and smaller jerks (head "+soft.head+" vs "+full.head+", largest "+soft.headMax.ToString("0.00")+" vs "+full.headMax.ToString("0.00")+")");
                Check(full.synced>=Math.Max(1,full.flinch/2),"the glitch flickers with the flinches ("+full.synced+" of "+full.flinch+")");
                Check(other.head!=full.head||other.cv!=full.cv,"each pilot twitches on its own schedule");
                // A fresh rig of a pilot already standing still draws the identical pose (the twitches keep no memory).
                Fresh("none");for(float t=1;t<10;t+=1/60f)PoseAt(t);var qa=(Quaternion[])mo.Rig.Q.Clone();
                var keep=mo;Fresh("none");for(float t=9;t<10;t+=1/60f)PoseAt(t);float qd=0;int worstBone=-1;for(int b2=0;b2<qa.Length;b2++){float d=2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(qa[b2],mo.Rig.Q[b2]))));if(d>qd){qd=d;worstBone=b2;}}
                Check(qd<.06f,"deterministic: a fresh rig poses the same twitching idle (worst joint "+worstBone+" within "+qd.ToString("0.0000")+" rad: the route's own smoothing)");
            }
            // The glitch: seeded per pilot and deterministic, only on the settled stance, slight and occasional; reduced effects
            // keep the soft version (no slice, no twitch, no strobing).
            {
                int frames=0,shown=0,twitches=0;bool split=false,slice=false,scan=false,twitch=false,lastTwitch=false;float worstSlice=0;int differ=0;
                for(float t=0;t<180;t+=1/60f)
                {
                    var g=CyberLordIdlePose.Glitch(1,t,1,false);var again=CyberLordIdlePose.Glitch(1,t,1,false);frames++;
                    Check(g.Slice==again.Slice&&g.Split==again.Split&&g.Scan==again.Scan&&g.Twitch==again.Twitch&&g.ScanY==again.ScanY,"the glitch is a pure function of the seed and the time");
                    if(g.Visible)shown++;split|=g.Split>0&&g.Slice==0;slice|=g.Slice!=0;scan|=g.Scan>0;bool tw=g.Twitch!=0;if(tw&&!lastTwitch)twitches++;lastTwitch=tw;twitch|=tw;worstSlice=Math.Max(worstSlice,Math.Abs(g.Slice));
                    var other=CyberLordIdlePose.Glitch(2,t,1,false);if(other.Visible!=g.Visible)differ++;
                    Check(!CyberLordIdlePose.Glitch(1,t,CyberLordIdlePose.Settled-.01f,false).Visible,"no glitch while the stance settles");
                }
                float share=(float)shown/frames;
                Check(split&&slice&&scan&&twitch,"every glitch kind occurs (split "+split+", slice "+slice+", scan "+scan+", twitch "+twitch+")");
                Check(share>.02f&&share<.14f,"the glitch is occasional: "+(100*share).ToString("0.0")+"% of the idle's frames");
                Check(twitches>=3&&twitches<=30,"the twitch is rare ("+twitches+" in 180 s)");Check(worstSlice<=CyberLordIdlePose.SliceMax+1e-5f,"the slice stays slight");
                Check(differ>frames/50,"each pilot has its own schedule");
                float prev=0,worstStep=0;int reducedShown=0;
                for(float t=0;t<180;t+=1/60f)
                {
                    var g=CyberLordIdlePose.Glitch(1,t,1,true);if(g.Visible)reducedShown++;
                    Check(g.Slice==0&&g.HeadSlice==0&&g.Twitch==0&&g.Split<=CyberLordIdlePose.ReducedSplit+1e-5f,"reduced effects: no tear or twitch, a softer split");
                    worstStep=Math.Max(worstStep,Math.Abs(g.Split-prev));prev=g.Split;
                    var pc=new CyberPiece{Bone=CapturedRig.Chest,Position=new Vector3(0,.1f,.15f)};float gain=CyberLordIdlePose.ScanGain(g,pc,3,true);Check(gain>=1-1e-5f&&gain<=1.5f,"reduced effects: the band only brightens softly");
                }
                Check(worstStep<.06f,"reduced effects: the split eases in and out (largest 60 Hz step "+worstStep.ToString("0.000")+")");Check(reducedShown>0,"reduced effects keep a glitch");
                var full=new CyberIdleGlitch{Scan=1,ScanY=CapturedRig.Bones[CapturedRig.Chest].Position.Y-.135f+.1f,Step=4};float lo=9,hi=0;
                for(int i=0;i<12;i++){var pc=new CyberPiece{Bone=CapturedRig.Chest,Position=new Vector3(0,.1f,.15f)};full.Step=i;float k=CyberLordIdlePose.ScanGain(full,pc,i,false);lo=Math.Min(lo,k);hi=Math.Max(hi,k);}
                Check(lo<.5f&&hi>1.5f&&CyberLordIdlePose.ScanGain(full,new CyberPiece{Bone=CapturedRig.LFoot},0,false)==1,"the scanline band flickers the hardlight parts inside it only");
            }
            // In a match the glitch reaches the pose (the tear moves the upper body off the hips; the twitch turns the neck).
            {
                Fresh(NativeSheldonHead.Hat);float tear=0;
                for(float t=1;t<40;t+=1/60f){PoseAt(t);var g=cy.IdleGlitch;if(g.Slice!=0){var r=mo.Rig;tear=Math.Max(tear,Math.Abs(r.P[CapturedRig.Spine].X-CapturedRig.Bones[CapturedRig.Spine].Position.X));}}
                Check(tear>.01f&&tear<=CyberLordIdlePose.SliceMax+1e-4f,"the tear shifts the upper body ("+tear.ToString("0.000")+" m)");
                cy.Reduced=true;Fresh(NativeSheldonHead.Hat);cy.Reduced=true;for(float t=1;t<40;t+=1/60f){PoseAt(t);Check(cy.IdleGlitch.Slice==0&&cy.IdleGlitch.Twitch==0,"reduced effects never tear the pose");}
            }
            // The portraits (NativeLobbyPoses): the Cyber Lord stands in the same twitching sword idle, unarmed, on the match idle's
            // feet; every other pilot as before.
            {
                var r=new MeleeRig();var rb=new MeleeRig();
                NativeLobbyPoses.Sample(r,CyberLordSpec.Id,NativeLobbyPoses.Weapon,1.3f,NativeSheldonHead.Hat,false,7,out var w,out var off,out var g);var st=CyberLordStance.Stance;
                Check(!w.Visible&&!off.Visible,"the lobby stance is unarmed");
                Check((r.ChainPoint(CapturedRig.LFoot,-1)-new Vector3(st.LeadX,MeleeRig.FLOOR,st.LeadZ)).Length()<.02f&&(r.ChainPoint(CapturedRig.RFoot,-1)-new Vector3(st.RearX,MeleeRig.FLOOR,st.RearZ)).Length()<.02f,"the lobby stance stands on the match idle's feet");
                var lf=r.ChainPoint(CapturedRig.LHand,-1);var rf=r.ChainPoint(CapturedRig.RHand,-1);float cY=r.ChainPoint(CapturedRig.Chest,-1).Y;Check(lf.Y<cY&&rf.Y<cY,"the lobby stance: the sword idle's low fists");
                NativeLobbyPoses.Sample(r,CyberLordSpec.Id,NativeLobbyPoses.Weapon,0,"none",false,7,out _,out _,out var still);Check(!still.Visible&&still.LClench==0&&still.RClench==0,"reduced motion (portrait time 0): the stance holds still, no twitch or glitch");
                float lobbyHead=0;for(float t=.1f;t<30;t+=1/30f){NativeLobbyPoses.Sample(r,CyberLordSpec.Id,NativeLobbyPoses.Weapon,t,"none",false,7,out _,out _,out _);var h=r.ChainQuat(CapturedRig.Head,-1);var c=r.ChainQuat(CapturedRig.Chest,-1);lobbyHead=Math.Max(lobbyHead,2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(c,h)))));}
                Check(lobbyHead>.15f&&lobbyHead<1.25f,"the portrait twitches too (the head up to "+lobbyHead.ToString("0.00")+" rad off the chest)");
                foreach(var other in new[]{"sword","pistol","emperor","siren","fire"})foreach(int pose in new[]{NativeLobbyPoses.Weapon,NativeLobbyPoses.Chair,NativeLobbyPoses.Crossed})
                {
                    r.Reset();rb.Reset();NativeLobbyPoses.Sample(r,other,pose,1.1f,"hat-x",false,3,out var w1,out var o1,out var g1);NativeLobbyPoses.Sample(rb,other,pose,1.1f,out var w2,out var o2);
                    bool same=w1.Visible==w2.Visible&&o1.Visible==o2.Visible&&!g1.Visible;for(int b=0;b<MeleeRig.N;b++)same&=r.Q[b]==rb.Q[b]&&r.P[b]==rb.P[b];
                    Check(same,"every other pilot's lobby pose is unchanged ("+other+" "+pose+")");
                }
                int lobbyGlitch=0;for(float t=0;t<60;t+=1/30f){NativeLobbyPoses.Sample(r,CyberLordSpec.Id,NativeLobbyPoses.Weapon,t,NativeSheldonHead.Hat,true,7,out _,out _,out var gr);if(gr.Visible)lobbyGlitch++;Check(gr.Slice==0&&gr.Twitch==0,"reduced effects: the portrait never tears");}
                Check(lobbyGlitch>0,"the portrait glitches too");
            }
        }
        yield return $"Cyber Lord idle: {count-c0} checks (a still pilot settles after {CyberLordIdle.Delay} s over {CyberLordIdle.BlendIn} s and leaves it within {CyberLordIdle.BlendOut} s on a step, jab, cast, guard, roll, reaction, stun, jump or Kernel Panic; the sword idle with fists, twitching on seeded irregular schedules with fast attacks and holds, bounded, the feet planted, the actor untouched, fewer and smaller jerks when reduced; the glitch seeded, slight, occasional, synced to the flinches, soft when reduced; the lobby stance the same, every other pilot's unchanged)";

        // ------------------------------------------------------------------------------------------------------------------
        // 8. No per-frame allocation (after the first frame builds its tables)
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            var nul=new Null();var fxN=new NativeCyberLordFx();var sc=new Scene{LocalId=1,ViewerId=1};sc.List.AddRange(actors);var pieces=new CyberPiece[NativeCyberLordLook.Capacity];var o=new NativeKitSound[NativeCyberLordSounds.Max];
            var mo=new MeleeMotion();var cy=new CyberLordMotion();var mv=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true};var pose=new CapturedRig.Pose();var vpose=new CapturedRig.Pose();var feedView=new CyberLordView();var chars2=new char[KernelPanicCutIn.Title.Length];
            for(int b=0;b<MeleeRig.N;b++){vpose.Rotation[b]=Quaternion.Identity;vpose.Position[b]=CapturedRig.Bones[b].Position;}
            var idlePilot=CyberLordFixture.Pilot(1,Vector3.Zero,0);idlePilot.Hat=NativeSheldonHead.Hat;var idleRig=new MeleeMotion();var idleMotion=new CyberLordMotion();var lobbyRig=new MeleeRig();
            var parts=new (string name,Action<float> run)[]{
                ("effects",t=>{fxN.Observe(events,t);fxN.Draw(nul,sc,t,1,false);fxN.Draw(nul,sc,t,.3f,true);fxN.FlareOf(1,t);}),
                ("look",t=>{CyberLordFeed.Read(actors[0],feedView);NativeCyberLordLook.Pieces(t,8,fxN.FlareOf(1,t),false,NativeCyberLordLook.BodySolid(feedView,t),1,false,pieces);NativeCyberLordLook.Pieces(t,8,default,true,1,.3f,true,pieces);}),
                ("rig",t=>{mo.Rig.GroupPosition=new Vector3(0,.865f,0);mo.FighterBase(mv,t,1/60f);cy.Style(mo,actors[0],t,false);mo.FighterFinish(mv,t,1/60f);}),
                ("first person",t=>cy.FirstPerson(pose,actors[0],t)),("victim",t=>cy.VictimPose(vpose,feedView,2,t)),
                ("HUD",t=>{for(int s=0;s<6;s++)NativeCyberLordHud.Of(feedView,s,t,40);}),
                ("camera",t=>{KernelPanicCamera.At(feedView,KernelPanicCamera.Clock(feedView,t,false),false,false,true,16/9f);KernelPanicCamera.At(feedView,KernelPanicCamera.Clock(feedView,t,true),true,true,false,1.3f);KernelPanicFeel.Screen(feedView,t,false,false);}),
                ("cut-in",t=>{var cs=KernelPanicCutIn.At(t-feedView.PanicConnect,false);KernelPanicCutIn.Typed(cs.Typed,cs.Seed,chars2);KernelPanicCutIn.Banner(t-feedView.PanicConnect);}),
                ("sounds",t=>{for(int s=0;s<NativeCyberLordSounds.Slots;s++)NativeCyberLordSounds.State(feedView,s,o,out _);NativeCyberLordSounds.Event(EventKind.Hit,CyberHit.Palm,false,o);}),
                ("idle",t=>{idleRig.Rig.GroupPosition=new Vector3(0,.865f,0);idleRig.FighterBase(mv,t+20,1/60f);idleMotion.Style(idleRig,idlePilot,t+20,false);idleRig.FighterFinish(mv,t+20,1/60f);
                    var g=CyberLordIdlePose.Glitch(1,t+20,1,false);for(int i=0;i<4;i++)CyberLordIdlePose.ScanGain(g,pieces[i],i,false);CyberLordIdlePose.Twitch(1,t+20,false);CyberLordIdlePose.Clench(g,default);CyberLordIdlePose.Stand(lobbyRig,t,1,false);}),
            };
            CyberLordFeed.Read(actors[0],feedView);
            foreach(var (name,run) in parts){for(float t=8;t<13;t+=1/60f)run(t);}
            foreach(var (name,run) in parts)
            {
                long before=GC.GetAllocatedBytesForCurrentThread();for(float t=8;t<13;t+=1/60f)run(t);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(bytes==0,"a 60 Hz playthrough of the busy timeline allocates "+bytes+" bytes in the "+name);
            }
        }
        yield return $"Cyber Lord allocation: 0 bytes over 300 frames of the busy timeline (effects, look, rigs, HUD cards, camera, clock, cut-in, sounds, the idle stance and glitch); {count} checks in all";
    }
}
