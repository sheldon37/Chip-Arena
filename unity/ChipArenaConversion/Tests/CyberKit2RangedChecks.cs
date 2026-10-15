using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The DEV Cyber Lord's Kit v2, ranged half, presentation (Claude; docs/CYBER-LORD.md "Kit v2", Core/CyberKit2Ranged.cs) and
// the cyber mage's ranged body and fireballs that already draw on today's v1 Bit Bolts. The v2 rules are Astra's and pending:
// these drive the proposed record (CyberKit2RangedPreview, written as the host would) through CyberLordFeed.
//  1. the contract: the record's mapping only through CyberLordFeed and only on Version 2 (v1 untouched otherwise), its bounds
//     and JSON round trip, the Details, the shots' kinds, the shared helpers (storm drops, the orbital chase, chain falloff);
//  2. the body: the stance, each throw releasing on the replicated release, the Overflow from both hands, every v2 cast's
//     state, finite and continuous on even, uneven and rewound clocks, the legs the locomotion's while casting on the move, the
//     glide sprint, a delayed guest copy posing the same frames, first person;
//  3. the effects: the fireball (hand to path, its kinds, readable at range), its splash, each v2 effect on its window and its
//     helpers (the chain's hops on their times, the storm's drops at their points, the column on the chase), nothing flat on
//     the floor, the tiers, determinism (a copied record and a second instance draw the same), the first-person pilot;
//  4. the HUD cards, the icons and the sounds;
//  5. no allocation per frame.
// Pixels are checked by the offline renders; nothing here runs Unity.
internal static class CyberKit2RangedChecks
{
    private static int count;
    private const float Dt=1/60f,D2R=(float)Math.PI/180;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord Kit v2 ranged: "+what);count++;}

    private sealed class Pilot
    {
        public readonly Actor A;public readonly MeleeMotion M=new MeleeMotion{JumpLayer=new NativeJumpMotion()};public readonly CyberLordMotion C=new CyberLordMotion();
        public readonly MeleeView V=new MeleeView();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public string State;
        public Pilot(int id=7){A=CyberLordFixture.Pilot(id,Vector3.Zero,0);V.Id=id.ToString();V.Spec=CyberLordSpec.Id;}
        public void Step(float t,float dt=Dt)
        {
            var root=new Vector3(A.Position.x,A.Position.y,A.Position.z);
            V.Vel=new Vector3(-A.Velocity.x,A.Velocity.y,A.Velocity.z);V.Grounded=A.Grounded;V.Hp=A.Hp;V.Sprinting=A.Sprinting;V.Yaw=-A.Yaw*D2R;V.AirJumpAt=-100;
            M.Rig.GroupPosition=new Vector3(-root.X,.865f+root.Y,root.Z);M.Rig.GroupYaw=-A.Yaw*D2R;
            M.FighterBase(V,t,dt);State=C.Style(M,A,t,A.StunUntil>t);M.FighterFinish(V,t,dt);M.Rig.WritePose(Pose);Pose.World();
        }
        public Quaternion Group=>MeleeMath.AxisAngle(MeleeMath.Y,M.Rig.GroupYaw);
        public Vector3 World(int bone)=>M.Rig.GroupPosition+Vector3.Transform(Pose.WorldPosition[bone],Group);
        public Vector3 InChest(int bone)=>Vector3.Transform(Pose.WorldPosition[bone]-Pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(Pose.WorldRotation[CapturedRig.Chest]));
    }
    private sealed class Sink:ICyberFxSink,ICyberFxFlames
    {
        public int Count,View,Flat,Flames,Lines,Voxels;public string FlatWhat;public double Hash;
        private void Floor(float bottom,float thick,float across,string what){if(bottom<.12f&&thick<.03f&&across>.1f){Flat++;FlatWhat??=what+" (top "+bottom.ToString("0.000")+")";}}
        private void H(Vector3 a,float k){Hash=Hash*1.000003+a.X*1.7+a.Y*3.1+a.Z*5.3+k*7.7;}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;H(at,size);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;H(head,width);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;H(at,size);}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Count++;if(view)View++;H(at,size);}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Count++;Lines++;if(view)View++;else Floor(Math.Max(tail.Y,head.Y),Math.Abs(tail.Y-head.Y)+width,new Vector2(head.X-tail.X,head.Z-tail.Z).Length(),"line");H(tail,width);H(head,0);}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Count++;if(view)View++;else Floor(Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y)),Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))-Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y)),Vector3.Distance(a,c),"band");H(a,0);H(c,0);}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Count++;Voxels++;if(view)View++;else Floor(at.Y+scale.Y*.5f,scale.Y,Math.Max(scale.X,scale.Z),"voxel");H(at,scale.X);}
        public void Flame(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,float seed){Count++;Flames++;if(view)View++;else Floor(Math.Max(tail.Y,head.Y),Math.Abs(tail.Y-head.Y)+width,new Vector2(head.X-tail.X,head.Z-tail.Z).Length(),"flame");H(tail,width);H(head,seed);}
        public void Reset(){Count=View=Flames=Lines=Voxels=0;Hash=0;}
    }
    private sealed class Scene:ICyberFxScene,ICyberShots
    {
        public readonly List<Actor> List=new List<Actor>();public int ViewerId,LocalId;public ICyberShots Shots;public Vector3 EyeAt=new Vector3(0,1.62f,-3);
        public IReadOnlyList<Actor> Actors=>List;
        public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+new Vector3(0,1.15f,0);return true;}
        public bool Fist(Actor a,bool left,out Vector3 fist){fist=Feet(a)+new Vector3(left?-.2f:.2f,1.3f,.4f);return true;}
        public int Viewer=>ViewerId;public int Local=>LocalId;
        public bool ViewFist(bool left,out Vector3 fist){fist=new Vector3(left?-.15f:.15f,1.4f,.35f);return ViewerId!=0;}
        public Vector3 Eye=>EyeAt;
        public float Floor(Vector3 at)=>0;
        public int CyberShots=>Shots?.CyberShots??0;public CyberShot CyberShotAt(int i)=>Shots.CyberShotAt(i);
        public int CyberImpacts=>Shots?.CyberImpacts??0;public CyberImpact CyberImpactAt(int i)=>Shots.CyberImpactAt(i);
    }
    private sealed class Copied:ICyberKit2RangedSource{public CyberKit2RangedRecord Record;public int Id;public CyberKit2RangedRecord Of(int actor)=>actor==Id?Record:null;}

    // The script (s): ranged mode at .3, six throws .3 s apart (right, left, Overflow, left, right, Overflow), each of 1-4.
    private static readonly Vector3 From=new Vector3(0,1.165f,.2f),Ahead=Vector3.UnitZ;
    private const float Throws=1,ChainAt=3.4f,SingAt=4.4f,StormAt=8,OrbitalAt=10.5f,End=15.5f;
    private static readonly float[] Beats={Throws,Throws+.3f,Throws+.6f,Throws+.9f,Throws+1.2f,Throws+1.5f,ChainAt,SingAt,StormAt,OrbitalAt};
    private static readonly int[] Ids={21,22,23,24,25,26};
    private static readonly Vector3[] Points={new Vector3(0,1.15f,8),new Vector3(3,1.15f,12),new Vector3(-1,1.15f,15),new Vector3(4,1.15f,18),new Vector3(-2,1.15f,21),new Vector3(1,1.15f,25)};
    private static Vector3 OrbitalAim(float t)=>new Vector3(4*(float)Math.Sin((t-OrbitalAt)*1.3f),0,12+3*(float)Math.Cos((t-OrbitalAt)*.9f));
    private static void Script(CyberKit2RangedPreview p,int id,float t,ref int beat,Vector3 feet=default)
    {
        // (The live host writes a Version 2 record for every Cyber Lord from the start.)
        p.For(id);
        while(beat<Beats.Length&&t>=Beats[beat])
        {
            float w=Beats[beat];
            switch(beat)
            {
                case 6:p.Chain(id,w,From+feet,Ids,Points,6);break;
                case 7:p.Singularity(id,w,From+feet,new Vector3(0,0,11));break;
                case 8:p.Storm(id,w,new Vector3(0,0,14),7);break;
                case 9:p.Orbital(id,w,new Vector3(0,0,12));break;
                default:p.Plasma(id,w,From+feet,Ahead,beat==2?14:CyberKit2Spec.PlasmaRange,beat==2);break;
            }
            beat++;
        }
        p.Steer(id,t,OrbitalAim(t),feet);p.Advance(t);
    }
    private static float MaxStep(Vector3[] a,Vector3[] b){float m=0;for(int i=0;i<a.Length;i++)m=Math.Max(m,Vector3.Distance(a[i],b[i]));return m;}

    public static IEnumerable<string> Run(string root)
    {
        var kept=CyberLordFeed.RangedSource;var kept2=CyberLordFeed.Kit2RangedSource;
        try{foreach(var r in Body(root))yield return r;}
        finally{CyberLordFeed.RangedSource=kept;CyberLordFeed.Kit2RangedSource=kept2;}
    }
    // A pilot in ranged mode (v1's mode fields, which Kit v2 keeps: key 5 unchanged).
    private static CyberRangedPreview Ranged(int id,float at=.3f){var m=new CyberRangedPreview();m.Switch(id,at);return m;}

    private static IEnumerable<string> Body(string root)
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The contract
        // ------------------------------------------------------------------------------------------------------------------
        int c0=count;
        {
            Check(CyberKit2Ranged.Slots==5&&CyberKit2Ranged.Spec(CyberKit2Ranged.Plasma)==CyberKit2Spec.RangedLmb&&CyberKit2Ranged.Spec(CyberKit2Ranged.Orbital)==CyberKit2Spec.Orbital,"the five ranged slots map onto CyberKit2Spec's");
            for(int r=0;r<CyberKit2Ranged.Slots;r++)Check(CyberKit2Spec.RangedSlot(CyberKit2Ranged.Spec(r)),CyberKit2Spec.Names[CyberKit2Ranged.Spec(r)]+" is a ranged slot");
            // Only CyberLordFeed maps the record; the view never names Astra's kit.
            var files=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories);
            var mappers=files.Where(f=>File.ReadAllText(f).Contains("CyberKit2RangedFeed.Map(")).Select(Path.GetFileName).ToArray();
            Check(mappers.Length==1&&mappers[0]=="NativeCyberLord.cs","only CyberLordFeed maps the v2 record ("+string.Join(",",mappers)+")");
            var readers=files.Where(f=>Regex.IsMatch(File.ReadAllText(f),@"Kit2RangedSource\?*\.Of\(")).Select(Path.GetFileName).ToArray();
            Check(readers.Length==1&&readers[0]=="NativeCyberLord.cs","only CyberLordFeed reads the v2 source ("+string.Join(",",readers)+")");
            foreach(var f in new[]{"CyberKit2Ranged.cs","CyberLordRangedMotion.cs","NativeCyberLordPlasmaFx.cs","NativeCyberLordKit2RangedFx.cs","CyberKit2RangedHud.cs"})
            {var src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f));foreach(var banned in new[]{".Cyber.",".Cyber;","a.Cyber","NativePilot","NativeArena","UnityEngine","System.Random","DateTime"})Check(!src.Contains(banned),f+" reads no authority state ("+banned+")");}
            var prev=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCyberLordKit2Preview.cs"));
            foreach(var banned in new[]{"arena.Step","NativeSimulation","Rules.","Bullets.Add","Emit(","Events.Add",".Hp=",".Position=",".Velocity=","Damage("})Check(!prev.Contains(banned),"the Dev Sandbox preview touches no rule, actor or event ("+banned+")");
            Check(prev.Contains("CyberLordFeed.Kit2RangedSource=cyberKit2")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSandbox.cs")).Contains("presentation.CyberKit2RangedPreviewOn=!presentation.CyberKit2RangedPreviewOn"),"the sandbox's gallery toggles the preview, which feeds the record through CyberLordFeed");

            // Version gate: no record, Version 0 or 1 leave v1 exactly as it was; Version 2 is live.
            var a=CyberLordFixture.Pilot(3,Vector3.Zero,0);var v=new CyberLordView();var src2=new Copied{Id=3};CyberLordFeed.Kit2RangedSource=src2;
            var rec=new CyberKit2RangedRecord{Version=CyberKit2Spec.Version};var p=new CyberKit2RangedPreview();p.Plasma(3,2,From,Ahead);rec=p.Of(3);
            foreach(int ver in new[]{0,1,3}){var x=rec.ViewCopy();x.Version=ver;src2.Record=x;CyberLordFeed.Read(a,v);Check(!v.Kit2R.Live&&v.Kit2R.At.All(z=>z==-100),"Version "+ver+" is ignored (v1 draws)");}
            src2.Record=rec;CyberLordFeed.Read(a,v);Check(v.Kit2R.Live&&Math.Abs(v.Kit2R.At[CyberKit2Ranged.Plasma]-2)<1e-5f&&Math.Abs(v.Kit2R.Release[CyberKit2Ranged.Plasma]-2-CyberKit2Spec.PlasmaWindup)<1e-5f,"Version 2 maps its windows");
            CyberLordFeed.Kit2RangedSource=null;CyberLordFeed.Read(a,v);Check(!v.Kit2R.Live,"no source: not live");
            // Bounds and the JSON round trip (a Unity-serializable record: fields, V3, fixed arrays).
            Check(rec.Valid(),"a written record is within bounds");var bad=rec.ViewCopy();bad.At[1]=float.NaN;Check(!bad.Valid(),"a NaN fails the bounds");bad=rec.ViewCopy();bad.ChainIds=new int[3];Check(!bad.Valid(),"a short chain array fails");
            var opt=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var json=JsonSerializer.Serialize(rec,opt);var back=JsonSerializer.Deserialize<CyberKit2RangedRecord>(json,opt);Check(JsonSerializer.Serialize(back,opt)==json&&back.Valid(),"the record round-trips through JSON");
            var copy=rec.ViewCopy();copy.At[0]=99;copy.ChainIds[0]=5;Check(rec.At[0]!=99&&rec.ChainIds[0]!=5,"ViewCopy deep-copies the arrays");
            // The Details and the shots.
            for(int i=0;i<CyberKit2Ranged.EventDetails.Length;i++)Check(CyberLordFeed.Hit(CyberKit2Ranged.EventDetails[i])==CyberKit2RangedFeed.PlasmaHit+i&&CyberKit2RangedFeed.Is(CyberLordFeed.Hit(CyberKit2Ranged.EventDetails[i])),"Detail "+CyberKit2Ranged.EventDetails[i]+" maps");
            Check(CyberLordFeed.Hit("bit-bolt")==CyberHit.Bolt&&CyberLordFeed.Hit("cyber-jab")==CyberHit.Jab&&!CyberKit2RangedFeed.Is(CyberHit.Root),"v1's Details map as before");
            Check(CyberLordFeed.ShotSlot("cyberlord","plasma-byte")==CyberRangedSpec.Bolt&&CyberLordFeed.ShotSlot("cyberlord","plasma-overflow")==CyberRangedSpec.Bolt&&CyberLordFeed.ShotSlot("cyberlord","bit-bolt")==CyberRangedSpec.Bolt&&CyberLordFeed.ShotSlot("cyberlord",null)<0&&CyberLordFeed.ShotSlot("pistol","plasma-byte")<0,
                "the fireballs (v1's bit-bolt, v2's plasma-byte and plasma-overflow) are the Cyber Lord's shots; the Data Packets stay the default drawer's");
            Check(CyberKit2RangedFeed.ShotKind("plasma-byte")==1&&CyberKit2RangedFeed.ShotKind("plasma-overflow")==2&&CyberKit2RangedFeed.ShotKind("bit-bolt")==0,"the shot kinds");
            // The chain: the throws' hands (right, left, both) and the reset after a pause.
            var q=new CyberKit2RangedPreview();var hands=new List<int>();float t=1;for(int i=0;i<6;i++){q.Plasma(9,t,From,Ahead);hands.Add(CyberKit2Spec.PlasmaHand(q.Of(9).PlasmaShot));t+=.3f;}
            Check(hands.SequenceEqual(new[]{0,1,2,1,0,2}),"the throws alternate with an Overflow from both every third ("+string.Join(",",hands)+")");
            q.Plasma(9,t+2,From,Ahead);Check(q.Of(9).PlasmaShot==0,"a pause over ComboReset restarts the chain on the right hand");
        }
        {
            // The shared helpers drive what is drawn: the storm's drops, the orbital chase, the chain's hop times and falloff.
            var p=new CyberKit2RangedPreview();p.Storm(5,2,new Vector3(3,0,14),11);var a=CyberLordFixture.Pilot(5,Vector3.Zero,0);var v=new CyberLordView();CyberLordFeed.Kit2RangedSource=p;CyberLordFeed.Read(a,v);var k=v.Kit2R;float last=-1;
            for(int i=0;i<CyberKit2Spec.Drops;i++)
            {
                k.Drop(i,out float at,out var pt);CyberKit2Spec.StormDrop(11,i,out float at2,out float x,out float z);
                Check(Math.Abs(at-(k.Release[CyberKit2Ranged.Storm]+at2))<1e-5f&&Math.Abs(pt.X-3-x)<1e-5f&&Math.Abs(pt.Z-14-z)<1e-5f,"drop "+i+" is StormDrop's");
                Check(new Vector2(x,z).Length()<=CyberKit2Spec.StormRadius+1e-3f&&at2<=CyberKit2Spec.StormSeconds+1e-4f,"drop "+i+" inside the ring and the storm");last=Math.Max(last,at);
            }
            Check(Math.Abs(k.StormEnd-last)<1e-5f,"the storm ends on its last drop");
            p.Orbital(5,4,new Vector3(0,0,10));p.Steer(5,4.6f,new Vector3(6,0,10),Vector3.Zero);p.Steer(5,4.85f,new Vector3(6,0,10),Vector3.Zero);CyberLordFeed.Read(a,v);k=v.Kit2R;
            float ox=0,oz=10;CyberKit2Spec.OrbitalStep(ref ox,ref oz,6,10,.25f);var at3=k.OrbitalAt(4.85f);Check(Math.Abs(at3.X-ox)<1e-4f&&Math.Abs(at3.Z-oz)<1e-4f,"the column's point is OrbitalStep's");
            var later=k.OrbitalAt(4.85f+.1f);Check(Math.Abs(later.X-Math.Min(6,ox+CyberKit2Spec.OrbitalChase*.1f))<1e-3f,"between snapshots it chases on at OrbitalChase");
            Check(Math.Abs(k.OrbitalAt(9).X-6)<1e-4f,"it never overshoots the aim");
            var far=new Vector3(80,0,10);p.Steer(5,5.1f,far,Vector3.Zero);CyberLordFeed.Read(a,v);Check(new Vector2(v.Kit2R.OrbitalAim.X,v.Kit2R.OrbitalAim.Z).Length()<=CyberKit2Spec.OrbitalRange+1e-3f,"the aim is kept within OrbitalRange of the caster");
            p.Chain(5,7,From,Ids,Points,6);CyberLordFeed.Read(a,v);k=v.Kit2R;
            for(int i=0;i<6;i++)Check(Math.Abs(k.HopAt(i)-(7+CyberKit2Spec.Windup[CyberKit2Spec.Chain]+i*CyberKit2Spec.HopDelay))<1e-5f,"hop "+i+" lands HopDelay after the last");
            for(int i=1;i<6;i++)Check(CyberKit2Spec.ChainHopDamage(i)<CyberKit2Spec.ChainHopDamage(i-1),"each hop is weaker");
        }
        yield return $"Cyber Lord Kit v2 ranged contract: {count-c0} checks (the record mapped only by CyberLordFeed and only on Version 2, v1 untouched otherwise; bounds, JSON, deep copy; the seven Details and the fireballs' kinds; the throws' hands and the chain's reset; the storm's drops, the orbital chase and the chain's hops from the shared helpers)";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. The body
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;var seen=new HashSet<string>();float handStep=0,spike=0,s1=-1,s2=-1,fpStep=0,worstGuest=0;string spikeState="";float spikeAt=0;
        {
            var host=new Pilot(31);var mode=Ranged(31);CyberLordFeed.RangedSource=mode;var p=new CyberKit2RangedPreview();int beat=0;
            var guest=new Pilot(31);var copied=new Copied{Id=31};var history=new Queue<(float t,CyberKit2RangedRecord r)>();var hostFrames=new Queue<Quaternion[]>();
            var last=new Vector3[2];bool had=false;string before=JsonSerializer.Serialize(host.A.ViewCopy());string lastState=null;
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(p,31,t,ref beat);CyberLordFeed.Kit2RangedSource=p;host.Step(t);seen.Add(host.State);
                foreach(var qq in host.M.Rig.Q)Check(float.IsFinite(qq.W)&&Math.Abs(qq.Length()-1)<1e-3f,"finite rotations at "+t.ToString("0.00"));
                var hands=new[]{host.InChest(CapturedRig.RHand),host.InChest(CapturedRig.LHand)};
                if(had){float hs=MaxStep(hands,last);handStep=Math.Max(handStep,hs);if(s1>=0&&s2>=0&&s1-Math.Max(s2,hs)>spike){spike=s1-Math.Max(s2,hs);spikeAt=t-Dt;spikeState=lastState;}lastState=host.State;s2=s1;s1=hs;}last=hands;had=true;
                hostFrames.Enqueue((Quaternion[])host.M.Rig.Q.Clone());history.Enqueue((t,p.Of(31)?.ViewCopy()));
                if(history.Count>6){var (gt,gr)=history.Dequeue();var want=hostFrames.Dequeue();copied.Record=gr;CyberLordFeed.Kit2RangedSource=copied;guest.Step(gt);for(int i=0;i<want.Length;i++)worstGuest=Math.Max(worstGuest,1-Math.Abs(Quaternion.Dot(want[i],guest.M.Rig.Q[i])));}
            }
            Check(JsonSerializer.Serialize(host.A.ViewCopy())==before,"the actor is never written");
            foreach(var want in new[]{"cyber-ranged-aim","cyber-plasma-byte","cyber-plasma-overflow","cyber-daisy-chain","cyber-singularity","cyber-packet-storm","cyber-orbital-call","cyber-orbital-steer"})Check(seen.Contains(want),"shows "+want+" ("+string.Join(", ",seen)+")");
            Check(handStep<.3f&&spike<.07f,"no frame jumps (the largest hand step "+(handStep*100).ToString("0.0")+" cm; the largest spike over its neighbours "+(spike*100).ToString("0.0")+" cm at "+spikeAt.ToString("0.000")+" "+spikeState+")");
            Check(worstGuest<1e-6f,"a guest on a 6-frame delayed copy of the record poses the host's frames exactly ("+worstGuest.ToString("0.0e0")+")");
        }
        {
            // Each throw passes its release mark on the replicated release: the throwing hand moves fastest round it, forward.
            foreach(int shot in new[]{0,1,2})
            {
                var h=new Pilot(32);CyberLordFeed.RangedSource=Ranged(32);var p=new CyberKit2RangedPreview();CyberLordFeed.Kit2RangedSource=p;float at=2;
                for(int i=0;i<shot;i++)p.Plasma(32,at-.3f*(shot-i),From,Ahead);
                float peak=0,peakAt=0;Vector3 prev=default;bool fired=false;
                for(float t=1;t<3;t+=Dt)
                {
                    if(!fired&&t>=at){p.Plasma(32,t,From,Ahead);at=t;fired=true;}h.Step(t);
                    var hand=shot==2?(h.World(CapturedRig.RHand)+h.World(CapturedRig.LHand))*.5f:h.World(shot==1?CapturedRig.LHand:CapturedRig.RHand);
                    if(t>at-.05f&&t<at+.25f){float fwd=(hand.Z-prev.Z)/Dt;if(fwd>peak){peak=fwd;peakAt=t;}}prev=hand;
                }
                float rel=at+CyberKit2Spec.PlasmaWindup;Check(Math.Abs(peakAt-rel)<=2.01f*Dt&&peak>2,"throw "+shot+" ("+(shot==2?"both":shot==1?"left":"right")+"): the hand drives forward fastest on the release ("+peakAt.ToString("0.000")+" vs "+rel.ToString("0.000")+", "+peak.ToString("0.0")+" m/s)");
            }
            // And today's v1 bolts the same (the throw set reads v1's window until the v2 record lands).
            var h1=new Pilot(33);var m1=Ranged(33);CyberLordFeed.RangedSource=m1;CyberLordFeed.Kit2RangedSource=null;float b=2,pk=0,pkAt=0;Vector3 pv=default;bool f1=false;
            for(float t=1;t<3;t+=Dt){if(!f1&&t>=b){m1.Bolt(33,t,From,Ahead);b=t;f1=true;}h1.Step(t);var hand=h1.World(m1.Of(33).BoltHand==1?CapturedRig.LHand:CapturedRig.RHand);if(t>b-.05f&&t<b+.2f){float fwd=(hand.Z-pv.Z)/Dt;if(fwd>pk){pk=fwd;pkAt=t;}}pv=hand;}
            Check(Math.Abs(pkAt-(b+CyberRangedSpec.Windup[CyberRangedSpec.Bolt]))<=2.01f*Dt&&h1.State!=null,"a v1 Bit Bolt throws on its release too ("+pkAt.ToString("0.000")+")");
        }
        {
            // Casting on the move: the legs stay the locomotion's (planted, no slip) while every throw and cast plays, and the
            // pose stays finite on uneven and rewound clocks.
            var run=new Pilot(34);CyberLordFeed.RangedSource=Ranged(34);var p=new CyberKit2RangedPreview();int beat=0;float slip=0;var prevF=new Vector3[2];var on=new bool[2];var rng=new Random(5);bool castRun=false;
            run.A.Velocity=new V3(-Rules.Speed*.7f,0,Rules.Speed*.7f);
            for(float t=.5f,dt=Dt;t<End;dt=(float)(1/240.0+rng.NextDouble()*(1/30.0-1/240.0)),t+=dt)
            {
                run.A.Position=new V3(-Rules.Speed*.7f*(t-.5f),0,Rules.Speed*.7f*(t-.5f));var feet=new Vector3(run.A.Position.x,run.A.Position.y,run.A.Position.z);
                Script(p,34,t,ref beat,feet);CyberLordFeed.Kit2RangedSource=p;run.Step(t,dt);if(t<1.5f)continue;castRun|=run.State!=null&&run.State.StartsWith("cyber-")&&run.State!="cyber-ranged-aim"&&run.State!="cyber-ranged-run";
                Check(!run.M.Rig.LegsAuthored,"the ranged layer never takes the legs ("+run.State+")");
                for(int i=0;i<2;i++){bool lf=i==0,planted=run.M.Gait.Planted(lf);var at=run.World(lf?CapturedRig.LFoot:CapturedRig.RFoot);if(planted&&on[i]){var d=at-prevF[i];d.Y=0;slip=Math.Max(slip,d.Length()/dt);}on[i]=planted;prevF[i]=at;}
            }
            Check(castRun&&slip<.05f,"throwing and casting on a diagonal run: the contact foot stays planted ("+(slip*100).ToString("0.00")+" cm/s)");
            // Rewound: posing an earlier moment after a later one gives the same frame as a fresh rig there.
            var a1=new Pilot(35);var a2=new Pilot(35);var m=Ranged(35);CyberLordFeed.RangedSource=m;var q=new CyberKit2RangedPreview();int bq=0;for(float t=.5f;t<12;t+=Dt)Script(q,35,t,ref bq);CyberLordFeed.Kit2RangedSource=q;
            for(float t=.5f;t<12;t+=Dt)a1.Step(t);for(float t=1.2f;t<1.9f;t+=Dt){a1.Step(t);foreach(var qq in a1.M.Rig.Q)Check(float.IsFinite(qq.W),"finite on a rewound clock");}
            for(float t=.5f;t<1.9f;t+=Dt)a2.Step(t);float diff=0;for(int i=0;i<CapturedRig.RHand+1;i++)diff=Math.Max(diff,1-Math.Abs(Quaternion.Dot(a1.M.Rig.Q[i],a2.M.Rig.Q[i])));
            Check(diff<.02f,"the arms on a rewound clock match a fresh rig's ("+diff.ToString("0.0000")+")");
        }
        {
            // The glide: sprinting in ranged mode sweeps both arms back and out (the hands behind the chest), the lean lifted.
            var g=new Pilot(36);CyberLordFeed.RangedSource=Ranged(36);CyberLordFeed.Kit2RangedSource=null;g.A.Sprinting=true;g.A.Velocity=new V3(0,0,Rules.Speed*Rules.SprintMultiplier);
            for(float t=.5f;t<2.5f;t+=Dt){g.A.Position=new V3(0,0,Rules.Speed*Rules.SprintMultiplier*(t-.5f));g.Step(t);}
            // (In the rig's frame, facing +z: the chest leans far forward, so its own axes would read a trailing hand as ahead.)
            var r=g.Pose.WorldPosition[CapturedRig.RHand]-g.Pose.WorldPosition[CapturedRig.Chest];var l=g.Pose.WorldPosition[CapturedRig.LHand]-g.Pose.WorldPosition[CapturedRig.Chest];
            Check(g.State=="cyber-ranged-sprint"&&r.Z<-.05f&&l.Z<-.05f&&r.Y<0&&l.Y<0&&Math.Abs(r.X)>.25f&&Math.Abs(l.X)>.25f,"the glide: both hands swept back and out ("+r.ToString("0.00")+", "+l.ToString("0.00")+", "+g.State+")");
        }
        {
            // First person: finite, in view, no jumps, every state.
            var p=new CyberKit2RangedPreview();var a=CyberLordFixture.Pilot(37,Vector3.Zero,0);var m=new CyberLordMotion();var pose=new CapturedRig.Pose();int beat=0;Vector3? last=null;var fpSeen=new HashSet<string>();CyberLordFeed.RangedSource=Ranged(37);
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(p,37,t,ref beat);CyberLordFeed.Kit2RangedSource=p;fpSeen.Add(m.FirstPerson(pose,a,t));pose.World();var hh=pose.WorldPosition[CapturedRig.RHand];
                Check(float.IsFinite(hh.X)&&float.IsFinite(hh.Y)&&float.IsFinite(hh.Z),"first person stays finite");if(last.HasValue)fpStep=Math.Max(fpStep,Vector3.Distance(hh,last.Value));last=hh;
            }
            Check(fpSeen.Contains("cyber-plasma-byte")&&fpSeen.Contains("cyber-plasma-overflow")&&fpSeen.Contains("cyber-orbital-steer"),"first person shows the throws and the casts ("+string.Join(", ",fpSeen)+")");
            Check(fpStep<.25f,"first person: no frame jumps ("+(fpStep*100).ToString("0.0")+" cm)");
        }
        yield return $"Cyber Lord Kit v2 ranged body: {count-c0} checks ({seen.Count(x=>x!=null&&x.StartsWith("cyber-"))} states; throws on their releases (right, left, both, and v1's bolt); hand steps at most {(handStep*100):0.0} cm with no spike over {(spike*100):0.0} cm; the legs the run's while casting; uneven and rewound clocks; the glide; a delayed guest exact; first person {(fpStep*100):0.0} cm)";

        // ------------------------------------------------------------------------------------------------------------------
        // 3. The effects
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;int full=0,reduced=0,low=0,peakShots=0,viewDraws=0,flames=0;
        {
            var p=new CyberKit2RangedPreview();var a=CyberLordFixture.Pilot(41,Vector3.Zero,0);int beat=0;CyberLordFeed.RangedSource=Ranged(41);
            var fx=new NativeCyberLordFx();var fx2=new NativeCyberLordFx();var fxR=new NativeCyberLordFx();var fxL=new NativeCyberLordFx();var fxMe=new NativeCyberLordFx();var fxG=new NativeCyberLordFx();
            var sc=new Scene{Shots=p};sc.List.Add(a);var me=new Scene{Shots=p,ViewerId=41,LocalId=41};me.List.Add(a);var guestSrc=new Copied{Id=41};
            for(int i=0;i<6;i++)sc.List.Add(new Actor{Id=Ids[i],SourceSpec="sword",Hp=100,Grounded=true,Position=new V3(Points[i].X,0,Points[i].Z)});
            var sink=new Sink();var sink2=new Sink();var sinkR=new Sink();var sinkL=new Sink();var sinkMe=new Sink();var sinkG=new Sink();var drawn=new int[CyberKit2Ranged.Slots];int baseline=0;
            var hopDraws=new float[6];
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(p,41,t,ref beat);CyberLordFeed.Kit2RangedSource=p;
                sink.Reset();fx.Draw(sink,sc,t,1,false);sink2.Reset();fx2.Draw(sink2,sc,t,1,false);sinkR.Reset();fxR.Draw(sinkR,sc,t,.3f,false);sinkL.Reset();fxL.Draw(sinkL,sc,t,1,true);sinkMe.Reset();fxMe.Draw(sinkMe,me,t,1,false);
                Check(sink.Count==sink2.Count&&Math.Abs(sink.Hash-sink2.Hash)<1e-6*Math.Max(1,Math.Abs(sink.Hash)),"a second instance draws the identical frame at "+t.ToString("0.00"));
                full+=sink.Count;reduced+=sinkR.Count;low+=sinkL.Count;viewDraws+=sinkMe.View;flames+=sink.Flames;peakShots=Math.Max(peakShots,p.CyberShots);
                if(t<Throws)baseline=Math.Max(baseline,sink.Count);
                var v=new CyberLordView();CyberLordFeed.Read(a,v);for(int s=0;s<CyberKit2Ranged.Slots;s++)if(v.Kit2R.Phase(s,t).Live&&sink.Count>baseline)drawn[s]++;
                guestSrc.Record=p.Of(41)?.ViewCopy();CyberLordFeed.Kit2RangedSource=guestSrc;sinkG.Reset();fxG.Draw(sinkG,sc,t,1,false);Check(sinkG.Count==sink.Count&&Math.Abs(sinkG.Hash-sink.Hash)<1e-6*Math.Max(1,Math.Abs(sink.Hash)),"a copied record draws the same effects at "+t.ToString("0.00"));
            }
            for(int s=0;s<CyberKit2Ranged.Slots;s++)Check(drawn[s]>0,CyberKit2Spec.Names[CyberKit2Ranged.Spec(s)]+" draws on its window");
            Check(sink.Flat+sinkR.Flat+sinkL.Flat+sinkMe.Flat==0,"nothing lies flat within 12 cm of the floor ("+(sink.FlatWhat??sinkR.FlatWhat??sinkL.FlatWhat??sinkMe.FlatWhat)+")");
            Check(reduced<full&&low<full,"the reduced ("+reduced+") and low ("+low+") tiers draw less than full ("+full+")");
            Check(peakShots>=2&&flames>0,"the fireballs fly as shots ("+peakShots+" at once) with flame tongues");
            Check(viewDraws>0,"first person draws its own hand effects on the view layer ("+viewDraws+")");
        }
        {
            // The chain draws hop by hop on its times; the storm's drops land at their points; a cut cast draws nothing past its cut.
            var p=new CyberKit2RangedPreview();var a=CyberLordFixture.Pilot(42,Vector3.Zero,0);CyberLordFeed.RangedSource=Ranged(42);CyberLordFeed.Kit2RangedSource=p;var sc=new Scene{Shots=p};sc.List.Add(a);var fx=new NativeCyberLordFx();var s=new Sink();
            p.Chain(42,2,From,Ids,Points,6);var v=new CyberLordView();CyberLordFeed.Read(a,v);int prevLines=-1;
            for(int i=0;i<6;i++){float h=v.Kit2R.HopAt(i);s.Reset();fx.Draw(s,sc,h+.005f,1,true);int lines=s.Lines;Check(lines>prevLines,"hop "+i+" adds its bolt at its time ("+lines+")");prevLines=lines;}
            var q=new CyberKit2RangedPreview();CyberLordFeed.Kit2RangedSource=q;var b=CyberLordFixture.Pilot(43,Vector3.Zero,0);var sb=new Scene();sb.List.Add(b);
            q.Singularity(43,2,From,new Vector3(0,0,9));q.Interrupt(43,2.1f);var fy=new NativeCyberLordFx();var sk=new Sink();fy.Draw(sk,sb,3,1,false);int cut=sk.Count;
            var q2=new CyberKit2RangedPreview();CyberLordFeed.Kit2RangedSource=q2;var fz=new NativeCyberLordFx();var s3=new Sink();fz.Draw(s3,sb,3,1,false);Check(cut==s3.Count,"a Singularity cut before its release throws no orb");
            CyberLordFeed.Kit2RangedSource=q;var cv=new CyberLordView();CyberLordFeed.Read(b,cv);Check(!cv.Kit2R.Released(CyberKit2Ranged.Singularity)&&cv.Kit2R.Recover[CyberKit2Ranged.Singularity]==cv.Kit2R.Until[CyberKit2Ranged.Singularity],"the cut cast: never released, no settle");
        }
        {
            // The fireball reads at range: its halo and body keep a minimum angular size.
            var p=new CyberKit2RangedPreview();var a=CyberLordFixture.Pilot(44,Vector3.Zero,0);CyberLordFeed.RangedSource=Ranged(44);CyberLordFeed.Kit2RangedSource=p;p.Plasma(44,1,From,Ahead);var fx=new NativeCyberLordFx();
            var near=new SizeSink();var far=new SizeSink();var sn=new Scene{Shots=p,EyeAt=new Vector3(3,1.6f,5)};sn.List.Add(a);var sf=new Scene{Shots=p,EyeAt=new Vector3(0,1.6f,45)};sf.List.Add(a);
            float t=1+CyberKit2Spec.PlasmaWindup+.12f;p.Advance(t);fx.Draw(near,sn,t,1,false);new NativeCyberLordFx().Draw(far,sf,t,1,false);
            Check(near.Largest>.3f&&far.Largest>far.Distance*.02f,"the fireball keeps its size up close ("+near.Largest.ToString("0.00")+" m) and at "+far.Distance.ToString("0")+" m ("+far.Largest.ToString("0.00")+" m)");
        }
        yield return $"Cyber Lord Kit v2 ranged effects: {count-c0} checks (every v2 effect and the fireballs on their windows; full {full}, reduced {reduced}, low {low} draws; never flat on the floor; determinism across instances and a copied record; the chain hop by hop; a cut orb draws nothing; the fireball readable at range)";

        // ------------------------------------------------------------------------------------------------------------------
        // 4. The HUD, the icons, the sounds
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;int cues=0;
        {
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json"))))
            {var art=new HashSet<string>(doc.RootElement.GetProperty("icons").EnumerateArray().Select(i=>i.GetProperty("id").GetString()));for(int r=0;r<CyberKit2Ranged.Slots;r++)Check(art.Contains(CyberKit2Spec.Icons[CyberKit2Ranged.Spec(r)]),"icon "+CyberKit2Spec.Icons[CyberKit2Ranged.Spec(r)]+" is in the class icon art");}
            var p=new CyberKit2RangedPreview();p.For(45);var a=CyberLordFixture.Pilot(45,Vector3.Zero,0);CyberLordFeed.RangedSource=Ranged(45);CyberLordFeed.Kit2RangedSource=p;var v=new CyberLordView();
            CyberLordFeed.Read(a,v);Check(CyberKit2RangedHud.Shows(v)&&CyberKit2RangedHud.Of(v,CyberKit2Ranged.Chain,2,100).State=="READY"&&CyberKit2RangedHud.Labels[CyberKit2Ranged.Chain]=="1 · DAISY CHAIN"&&CyberKit2RangedHud.Labels[CyberKit2Ranged.Orbital]=="4 · ORBITAL UPLINK","the v2 set's cards ready, labelled 1-4");
            p.Chain(45,3,From,Ids,Points,6);p.Singularity(45,4,From,new Vector3(0,0,9));p.Orbital(45,7,new Vector3(0,0,12));CyberLordFeed.Read(a,v);
            Check(CyberKit2RangedHud.Of(v,CyberKit2Ranged.Chain,3.05f,100).State=="CHARGE"&&CyberKit2RangedHud.Of(v,CyberKit2Ranged.Chain,3.2f,100).State=="ACTIVE"&&CyberKit2RangedHud.Of(v,CyberKit2Ranged.Chain,4,100).State.EndsWith("s"),"Daisy Chain: CHARGE, ACTIVE, its cooldown");
            Check(CyberKit2RangedHud.Of(v,CyberKit2Ranged.Singularity,6,100).State=="ACTIVE"&&CyberKit2RangedHud.Of(v,CyberKit2Ranged.Singularity,9,100).State.EndsWith("s"),"Singularity ACTIVE while the hole lives, then its cooldown");
            Check(CyberKit2RangedHud.Of(v,CyberKit2Ranged.Orbital,7.2f,100).State=="CALL"&&CyberKit2RangedHud.Of(v,CyberKit2Ranged.Orbital,9,100).State=="STEER","Orbital Uplink: CALL, then STEER");
            Check(CyberKit2RangedHud.Of(v,CyberKit2Ranged.Orbital,40,100,kernelReady:50).State!="READY"&&CyberKit2RangedHud.Of(v,CyberKit2Ranged.Storm,40,5).State=="LOW MANA","the shared ultimate clock, low mana");
            var screen=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCyberLordScreen.cs"));Check(screen.Contains("NativeCyberLordHud.Drawer(v,arena.Time,p.Mana,p.KernelReady,cyberCards,cyberLabels)")&&!screen.Contains("NativeCyberLordHud.RangedOf(")&&!screen.Contains("NativeCyberLordHud.RangedLabels"),"the drawer shows the v2 set (the shipped kit) with or without the record, never v1's");
            var o=new NativeKitSound[NativeCyberLordSounds.Max];var clips=new HashSet<string>(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),"*.wav").Select(Path.GetFileNameWithoutExtension));
            foreach(var key in CyberKit2RangedSounds.Keys)Check(NativeSoundKeys.Known(key),"bank key "+key+" exists");foreach(var c in CyberKit2RangedSounds.Clips)Check(clips.Contains(c),"fallback clip "+c+" ships");
            var q=new CyberKit2RangedPreview();CyberLordFeed.Kit2RangedSource=q;var heard=new HashSet<(int,float)>();int beat=0;var w=new CyberLordView();
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(q,45,t,ref beat);CyberLordFeed.Read(a,w);
                for(int slot=0;slot<CyberKit2RangedSounds.Slots;slot++){int n=CyberKit2RangedSounds.State(w,slot,o,out float at);if(n==0||!(t>=at&&t-at<.15f))continue;for(int i=0;i<n;i++)Check(CyberKit2RangedSounds.Keys.Contains(o[i].Key)&&CyberKit2RangedSounds.Clips.Contains(o[i].Clip),"cue "+slot+" plays a listed key and clip");heard.Add((slot,at));}
            }
            for(int slot=0;slot<CyberKit2RangedSounds.Slots;slot++)Check(heard.Any(h=>h.Item1==slot),"v2 cue "+slot+" sounds in the script");cues=heard.Count;
            Check(heard.Count(h=>h.Item1==CyberKit2RangedSounds.Throw)==6,"one cue per throw");
            foreach(var h in new[]{CyberKit2RangedFeed.PlasmaHit,CyberKit2RangedFeed.OverflowHit,CyberKit2RangedFeed.SingularityTick,CyberKit2RangedFeed.OrbitalTick})foreach(var kind in new[]{EventKind.Hit,EventKind.AbilityHit,EventKind.Absorb,EventKind.Impact})foreach(bool mine in new[]{false,true})
            {int n=NativeCyberLordSounds.Event(kind,h,mine,o);for(int i=0;i<n;i++)Check(CyberKit2RangedSounds.Keys.Contains(o[i].Key)&&CyberKit2RangedSounds.Clips.Contains(o[i].Clip),"event cue "+o[i].Key);}
            Check(NativeCyberLordSounds.Event(EventKind.Hit,CyberKit2RangedFeed.PlasmaHit,false,o)>0,"a fireball's hit sounds");
            // A cut cast is silent past its cut; v1's Bit Bolt sounds as a fireball.
            var cut=new CyberKit2RangedPreview();cut.Orbital(46,1,new Vector3(0,0,10));cut.Interrupt(46,1.3f);CyberLordFeed.Kit2RangedSource=cut;var cv=new CyberLordView();CyberLordFeed.Read(CyberLordFixture.Pilot(46,Vector3.Zero,0),cv);
            Check(CyberKit2RangedSounds.State(cv,CyberKit2RangedSounds.OrbitalBeam,o,out _)==0&&CyberKit2RangedSounds.State(cv,CyberKit2RangedSounds.OrbitalMark,o,out _)>0,"a cut call-down: its mark heard, never its column");
            var bolt=Ranged(47);bolt.Bolt(47,2,From,Ahead);CyberLordFeed.RangedSource=bolt;CyberLordFeed.Kit2RangedSource=null;var bv=new CyberLordView();CyberLordFeed.Read(CyberLordFixture.Pilot(47,Vector3.Zero,0),bv);
            Check(NativeCyberLordSounds.State(bv,NativeCyberLordSounds.BoltShot,o,out _)>0&&o[0].Key=="magic.fire.shot","today's Bit Bolt is thrown with a fire whoosh");
        }
        yield return $"Cyber Lord Kit v2 ranged HUD, icons and sounds: {count-c0} checks (five line icons; the 1-4 cards' CHARGE, ACTIVE, CALL, STEER, cooldowns, the shared ultimate clock and low mana; {CyberKit2RangedSounds.Slots} state cues on {CyberKit2RangedSounds.Keys.Length} existing keys, {cues} heard once each; a cut cast silent)";

        // ------------------------------------------------------------------------------------------------------------------
        // 5. No allocation per frame
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            var p=new CyberKit2RangedPreview();var pilot=new Pilot(50);var fx=new NativeCyberLordFx();var sc=new Scene{Shots=p};sc.List.Add(pilot.A);for(int i=0;i<6;i++)sc.List.Add(new Actor{Id=Ids[i],SourceSpec="sword",Hp=100,Grounded=true,Position=new V3(Points[i].X,0,Points[i].Z)});
            var nul=new Sink();var view=new CyberLordView();var o=new NativeKitSound[NativeCyberLordSounds.Max];var fpM=new CyberLordMotion();var fpPose=new CapturedRig.Pose();int beat=0;float loop=0;var mode=Ranged(50);
            void Frame(float t)
            {
                float lt=t-loop;if(lt>=End){loop=t;lt=0;beat=0;p.Clear();}
                CyberLordFeed.RangedSource=mode;Script(p,50,.5f+lt,ref beat);CyberLordFeed.Kit2RangedSource=p;float at=.5f+lt;pilot.Step(at);fpM.FirstPerson(fpPose,pilot.A,at);
                fx.Draw(nul,sc,at,1,false);fx.Draw(nul,sc,at,.3f,true);CyberLordFeed.Read(pilot.A,view);
                for(int s=0;s<CyberKit2Ranged.Slots;s++)CyberKit2RangedHud.Of(view,s,at,60);
                for(int s=0;s<CyberKit2RangedSounds.Slots;s++)CyberKit2RangedSounds.State(view,s,o,out _);
            }
            for(float t=0;t<End*1.1f;t+=Dt)Frame(t);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<400;f++)Frame(End*1.1f+f*Dt);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"400 frames of the v2 ranged set (pose, first person, effects, HUD, sounds) allocate "+bytes+" bytes");
        }
        yield return $"Cyber Lord Kit v2 ranged allocation: 0 bytes over 400 frames; {count} checks in all";
    }
    // Records the largest sprite or glow near the first shot's position and its distance from the eye.
    private sealed class SizeSink:ICyberFxSink
    {
        public float Largest,Distance;
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Largest=Math.Max(Largest,size);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){if(at.Z>2){Largest=Math.Max(Largest,size);Distance=Math.Max(Distance,Math.Abs(45-at.Z));}}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){}
    }
}
