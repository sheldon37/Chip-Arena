using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// The DEV Cyber Lord's ranged mode presentation (Claude; docs/CYBER-LORD.md "Ranged mode", Core/CyberLordRanged.cs). The rules
// are Astra's and pending: these drive the proposed record through the preview source, as the Dev Sandbox and the renders do.
//  1. the proposed kit and the contract's mapping: the numbers, the icons, the ranged Details, the shot slot (the Data Packets
//     stay the default drawer's), the mode on the Siren's replicated fields when no record exists;
//  2. the switch rules as the preview host applies them (refused mid-cast and through its cooldown, never queued) and the
//     view's eased mode weight;
//  3. the pose: every ranged state shows on its window, the feet stay planted, the hold follows the aim, no frame jumps, the
//     first-person view; melee untouched (with no record the frames are bit-identical to no source at all); a guest on a
//     delayed copy of the record poses the host's frames exactly;
//  4. the effects: each cast draws on its window, nothing flat on the floor, the reduced and low tiers draw less, the
//     first-person pilot gets no ghost of itself, the bolts fly and land, a copied record draws the same; the sprint's
//     streaks ease in and out with the flag and never show in the pilot's own first person;
//  5. the look's emitters (melee mode unchanged piece for piece) and the HUD's two sets with the key-5 card;
//  6. the sounds on existing keys, each cue once, a cut cast silent;
//  7. no allocation per frame.
// Pixels are checked by the offline renders; nothing here runs Unity.
internal static class CyberLordRangedChecks
{
    private static int count;
    private const float Dt=1/60f,D2R=(float)Math.PI/180;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord ranged: "+what);count++;}

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
    private sealed class Sink:ICyberFxSink
    {
        public int Count,View,Streaks,Flat;
        private void Floor(float bottom,float thick,float across){if(bottom<.12f&&thick<.03f&&across>.1f)Flat++;}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;Streaks++;}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Count++;if(view)View++;}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Count++;if(view)View++;else Floor(Math.Max(tail.Y,head.Y),Math.Abs(tail.Y-head.Y)+width,new Vector2(head.X-tail.X,head.Z-tail.Z).Length());}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Count++;if(view)View++;else Floor(Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y)),Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))-Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y)),Vector3.Distance(a,c));}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Count++;if(view)View++;else Floor(at.Y+scale.Y*.5f,scale.Y,Math.Max(scale.X,scale.Z));}
        public void Reset(){Count=View=Streaks=0;}
    }
    private sealed class Scene:ICyberFxScene,ICyberShots
    {
        public readonly List<Actor> List=new List<Actor>();public int ViewerId,LocalId;public CyberRangedPreview Shots;
        public IReadOnlyList<Actor> Actors=>List;
        public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+new Vector3(0,1.3f,0);return true;}
        public bool Fist(Actor a,bool left,out Vector3 fist){fist=Feet(a)+new Vector3(left?-.2f:.2f,1.3f,.4f);return true;}
        public int Viewer=>ViewerId;public int Local=>LocalId;
        public bool ViewFist(bool left,out Vector3 fist){fist=new Vector3(left?-.15f:.15f,1.4f,.35f);return ViewerId!=0;}
        public Vector3 Eye=>new Vector3(0,1.62f,0);
        public float Floor(Vector3 at)=>0;
        public int CyberShots=>Shots?.CyberShots??0;public CyberShot CyberShotAt(int i)=>Shots.CyberShotAt(i);
        public int CyberImpacts=>Shots?.CyberImpacts??0;public CyberImpact CyberImpactAt(int i)=>Shots.CyberImpactAt(i);
    }
    // A guest's (or the killcam's) source: the record as a copy, set by the caller.
    private sealed class Copied:ICyberRangedSource{public CyberRangedRecord Record;public int Id;public CyberRangedRecord Of(int actor)=>actor==Id?Record:null;}

    // The script: the switch in, a burst of bolts, each of 1-4, the switch out (times in s; aimed down +z from the chest).
    private static readonly Vector3 From=new Vector3(.12f,1.3f,.45f),Ahead=Vector3.UnitZ;
    private const float SwitchIn=1,Bolts=1.6f,LanceAt=3.4f,FieldAt=4.4f,SwapAt=5.6f,RootAt=6.8f,SwitchOut=9,End=10.5f;
    private static readonly float[] Beats={SwitchIn,Bolts,Bolts+.26f,Bolts+.52f,Bolts+.78f,Bolts+1.04f,LanceAt,FieldAt,SwapAt,RootAt,SwitchOut};
    private static void Script(CyberRangedPreview p,int id,float t,ref int beat)
    {
        var at=Beats;
        while(beat<at.Length&&t>=at[beat])
        {
            float w=at[beat];
            switch(beat)
            {
                case 0:case 10:p.Switch(id,w);break;
                case 6:p.Lance(id,w,From,From+Ahead*30,From+Ahead*12,true);break;
                case 7:p.Field(id,w,From,new Vector3(0,0,12));break;
                case 8:p.Swap(id,w,Vector3.Zero,new Vector3(0,0,-9));break;
                case 9:p.Root(id,w,From,From+Ahead*45);break;
                default:p.Bolt(id,w,From,Ahead,beat==3?14:CyberRangedSpec.BoltRange,beat==3);break;
            }
            beat++;
        }
        p.Advance(t);
    }
    private static float MaxStep(Vector3[] a,Vector3[] b){float m=0;for(int i=0;i<a.Length;i++)m=Math.Max(m,Vector3.Distance(a[i],b[i]));return m;}
    /// <summary>The ranged stance's shape on a posed rig (empty when it holds): each hand below the chest, at least 5 cm behind
    /// its shoulder and out past it, its elbow at least 3 cm further back (the forearm curving forward to the hand), no hand
    /// within 45 cm of the head; the shoulders' line within .12 rad of square.</summary>
    internal static string Stance(CapturedRig.Pose pose)
    {
        var w=pose.WorldPosition;var head=w[CapturedRig.Head];var chest=w[CapturedRig.Chest];var why="";
        for(int i=0;i<2;i++)
        {
            bool l=i==1;string n=l?"left":"right";var sh=w[l?CapturedRig.LArm:CapturedRig.RArm];var el=w[l?CapturedRig.LElbow:CapturedRig.RElbow];var h=w[l?CapturedRig.LHand:CapturedRig.RHand];
            if(!(h.Y<chest.Y))why+=n+" hand above the chest; ";if(!(h.Z<sh.Z-.05f))why+=n+" hand not behind its shoulder ("+(h.Z-sh.Z).ToString("0.00")+"); ";
            if(!(Math.Abs(h.X)>Math.Abs(sh.X)))why+=n+" hand inside its shoulder; ";if(!(h.Z-el.Z>.03f))why+=n+" forearm not curving forward ("+(h.Z-el.Z).ToString("0.00")+"); ";
            if(!(Vector3.Distance(h,head)>.45f))why+=n+" hand near the head; ";
        }
        var line=w[CapturedRig.LArm]-w[CapturedRig.RArm];double twist=Math.Atan2(line.Z,line.X);if(Math.Abs(twist)>.12)why+="the chest bladed ("+twist.ToString("0.00")+")";
        return why;
    }

    public static IEnumerable<string> Run(string root)
    {
        var kept=CyberLordFeed.RangedSource;
        try{foreach(var r in Body(root))yield return r;}
        finally{CyberLordFeed.RangedSource=kept;}
    }
    private static IEnumerable<string> Body(string root)
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The proposed kit and the contract's mapping
        // ------------------------------------------------------------------------------------------------------------------
        int c0=count;const int S=CyberRangedSpec.Slots;
        foreach(var arr in new Array[]{CyberRangedSpec.Keys,CyberRangedSpec.Names,CyberRangedSpec.Ids,CyberRangedSpec.Roles,CyberRangedSpec.Cooldowns,CyberRangedSpec.Costs,CyberRangedSpec.Windup,CyberRangedSpec.Seconds,CyberRangedSpec.Settle,CyberRangedSpec.Icons})Check(arr.Length==S,"every per-slot table has the five slots");
        Check(CyberRangedSpec.Keys.SequenceEqual(new[]{"LMB","1","2","3","4"})&&CyberRangedSpec.ModeKey=="5","LMB and 1-4, switched on key 5");
        for(int s=0;s<S;s++)Check(CyberRangedSpec.Windup[s]<CyberRangedSpec.Seconds[s]&&CyberRangedSpec.Settle[s]>0&&CyberRangedSpec.Slot(CyberRangedSpec.Ids[s])==s,CyberRangedSpec.Names[s]+": its window and id");
        Check(CyberRangedSpec.Cooldowns[CyberRangedSpec.Root]==NativeKernelPanicRules.Cooldown&&CyberRangedSpec.Costs[CyberRangedSpec.Root]==NativeKernelPanicRules.Mana,"Root Access shares Kernel Panic's clock and cost");
        Check(CyberRangedSpec.SwitchSeconds>0&&CyberRangedSpec.SwitchSeconds<CyberRangedSpec.SwitchCooldown,"the switch is shorter than its cooldown");
        Check(CyberRangedSpec.FieldFlight(0)>=.35f&&CyberRangedSpec.FieldFlight(CyberRangedSpec.FieldRange)<=1.1f+1e-4f,"the cube's flight time stays in its band");
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json"))))
        {
            var art=new HashSet<string>(doc.RootElement.GetProperty("icons").EnumerateArray().Select(i=>i.GetProperty("id").GetString()));
            foreach(var id in CyberRangedSpec.Icons.Append(CyberRangedSpec.ModeIcon).Append(CyberRangedSpec.MeleeIcon))Check(art.Contains(id),"icon "+id+" is in the class icon art");
        }
        for(int s=0;s<S;s++)Check(CyberLordFeed.Hit(CyberRangedSpec.Ids[s])==CyberHit.Bolt+s,"Detail "+CyberRangedSpec.Ids[s]+" maps to its ranged hit");
        Check(CyberLordFeed.Kind(EventKind.Impact)&&CyberLordFeed.Hit(null)==CyberHit.None,"impacts with a ranged Detail are read; an undetailed impact is not");
        Check(CyberLordFeed.ShotSlot("cyberlord","bit-bolt")==CyberRangedSpec.Bolt&&CyberLordFeed.ShotSlot("cyberlord",null)<0&&CyberLordFeed.ShotSlot("cyberlord","packet-burst")<0&&CyberLordFeed.ShotSlot("pistol","bit-bolt")<0,
            "a shot is a ranged bolt only with Spec cyberlord and Detail bit-bolt (the Data Packets stay the default drawer's)");
        {
            // No record: the mode on the Siren's replicated fields (Actor.SourceRanged / SourceModeAt); none at all is melee.
            CyberLordFeed.RangedSource=null;var a=CyberLordFixture.Pilot(3,Vector3.Zero,0);var v=new CyberLordView();
            CyberLordFeed.Read(a,v);Check(!v.Ranged&&v.ModeWeight(5)==0&&v.SwitchU(5)<0&&v.RAt.All(x=>x==-100)&&!NativeCyberLordHud.ShowsRanged(v,5),"no mode fields: melee, no ranged window");
            a.SourceRanged=true;a.SourceModeAt=2;CyberLordFeed.Read(a,v);
            Check(v.Ranged&&v.ModeAt==2&&Math.Abs(v.ModeReady-3)<1e-5f&&Math.Abs(v.ModeWeight(2+CyberRangedSpec.SwitchSeconds*.5f)-.5f)<1e-4f&&v.ModeWeight(1.9f)==0&&v.ModeWeight(2.5f)==1,"the Siren's mode fields drive the switch (eased from SourceModeAt; before it, the mode it left)");
            var src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeShotPresentation.cs"));Check(src.Contains("CyberLordFeed.ShotSlot(b.Spec,b.Detail)<0"),"the default shot drawer skips the ranged bolts only");
            var prev=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCyberLordRangedPreview.cs"));
            foreach(var banned in new[]{"arena.Step","NativeSimulation","Rules.","Bullets.Add","Emit(","Events.Add",".Hp=",".Position=",".Velocity=","Damage("})Check(!prev.Contains(banned),"the Dev Sandbox preview touches no rule, actor or event ("+banned+")");
        }
        yield return $"Cyber Lord ranged contract: {count-c0} checks (LMB and 1-4 on key 5, five windows, Root Access on Kernel Panic's clock, seven art icons, the five ranged Details, bolts as Spec cyberlord / Detail bit-bolt, the mode on the Siren's fields)";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. The switch rules (the preview host)
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            var p=new CyberRangedPreview();const int id=4;
            Check(p.Switch(id,1)&&p.Of(id).Ranged&&p.Of(id).ModeReady==1+CyberRangedSpec.SwitchCooldown,"key 5 switches to ranged");
            Check(!p.Switch(id,1.1f)&&!p.Switch(id,1.9f)&&p.Of(id).Ranged,"refused through the switch and its cooldown (never queued)");
            Check(p.Switch(id,2.05f)&&!p.Of(id).Ranged&&p.Switch(id,3.1f)&&p.Of(id).Ranged,"and back, and in again");
            p.Lance(id,4.3f,From,From+Ahead*30);Check(!p.Switch(id,4.4f)&&p.Switch(id,4.3f+CyberRangedSpec.Seconds[CyberRangedSpec.Lance]+.01f),"refused mid-cast, allowed once the cast ends");
            Check(!p.Switch(id,9,busy:true),"refused while the kit says busy (a melee cast, a dash, a stun)");
            p.Root(id,10,From,From+Ahead*45);p.Interrupt(id,10.5f);var r=p.Of(id);
            Check(r.Until[CyberRangedSpec.Root]==10.5f&&r.Release[CyberRangedSpec.Root]==10.5f,"an interrupt mid-charge cuts the beam before it fires");
            p.Field(id,12,From,new Vector3(0,0,10));p.Interrupt(id,12.05f);Check(r.FieldLand==-100&&r.FieldUntil==-100,"a lob cut before its release throws no field");
            p.Swap(id,14,Vector3.Zero,new Vector3(0,0,-9));p.Interrupt(id,14.01f);Check(r.MineAt==-100,"a swap cut before its blink leaves no mine");
            CyberLordFeed.RangedSource=p;var a=CyberLordFixture.Pilot(id,Vector3.Zero,0);var v=new CyberLordView();CyberLordFeed.Read(a,v);
            Check(!v.RReleased(CyberRangedSpec.Root)&&v.RRecover[CyberRangedSpec.Root]==v.RUntil[CyberRangedSpec.Root],"the view: the cut beam never released, no settle");
            // The view's weight eases over the switch, no frame steps further than the smoothstep's slope.
            var q=new CyberRangedPreview();q.Switch(id,1);CyberLordFeed.RangedSource=q;float last=0,step=0;bool flipped=false;
            for(float t=.9f;t<1.6f;t+=Dt){CyberLordFeed.Read(a,v);float w=v.ModeWeight(t);step=Math.Max(step,Math.Abs(w-last));last=w;if(NativeCyberLordHud.ShowsRanged(v,t)&&!flipped){flipped=true;Check(Math.Abs(t-1-CyberRangedSpec.SwitchSeconds*.5f)<Dt*1.01f,"the bar swaps sets at the switch's middle");}}
            Check(last==1&&flipped&&step<=1.5f*Dt/CyberRangedSpec.SwitchSeconds+1e-4f,"the mode eases in over "+CyberRangedSpec.SwitchSeconds+" s (largest frame step "+step.ToString("0.000")+")");
        }
        yield return $"Cyber Lord ranged switch: {count-c0} checks (key 5 in and out, refused mid-switch, through its {CyberRangedSpec.SwitchCooldown} s cooldown, mid-cast and while busy, never queued; interrupts cut the beam, the lob and the blink before they leave the hands; the bar swaps at the switch's middle)";

        // ------------------------------------------------------------------------------------------------------------------
        // 3. The pose
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;var seen=new HashSet<string>();float footSlip=0,handStep=0,fpStep=0,stepAt=0;string stepState=null;float spike=0,s1=-1,s2=-1,spikeAt=0;string spikeState=null,lastState=null;
        {
            var p=new CyberRangedPreview();var host=new Pilot();int beat=0;
            // The guest: the same pilot on a copy of the record as it stood 6 frames earlier, posed 6 frames behind.
            var guest=new Pilot();var copied=new Copied{Id=host.A.Id};var history=new Queue<(float t,CyberRangedRecord r)>();var hostFrames=new Queue<Quaternion[]>();
            Vector3[] feet0=null;var lastHands=new Vector3[2];bool had=false;string before=JsonSerializer.Serialize(host.A.ViewCopy());int frame=0;float worstGuest=0;
            for(float t=.5f;t<End;t+=Dt,frame++)
            {
                Script(p,host.A.Id,t,ref beat);CyberLordFeed.RangedSource=p;host.Step(t);seen.Add(host.State);
                foreach(var q in host.M.Rig.Q)Check(float.IsFinite(q.W)&&Math.Abs(q.Length()-1)<1e-3f,"finite rotations at "+t.ToString("0.00"));
                var feet=new[]{host.World(CapturedRig.LFoot),host.World(CapturedRig.RFoot)};if(t>.8f){if(feet0==null)feet0=feet;else footSlip=Math.Max(footSlip,MaxStep(feet,feet0));}
                var hands=new[]{host.InChest(CapturedRig.RHand),host.InChest(CapturedRig.LHand)};if(had){float hs=MaxStep(hands,lastHands);if(hs>handStep){handStep=hs;stepAt=t;stepState=host.State;}if(s1>=0&&s2>=0&&s1-Math.Max(s2,hs)>spike){spike=s1-Math.Max(s2,hs);spikeAt=t-Dt;spikeState=lastState;}lastState=host.State;s2=s1;s1=hs;}lastHands=hands;had=true;
                hostFrames.Enqueue((Quaternion[])host.M.Rig.Q.Clone());history.Enqueue((t,p.Of(host.A.Id)?.ViewCopy()));
                if(history.Count>6)
                {
                    var (gt,gr)=history.Dequeue();var want=hostFrames.Dequeue();copied.Record=gr;CyberLordFeed.RangedSource=copied;guest.Step(gt);
                    for(int i=0;i<want.Length;i++)worstGuest=Math.Max(worstGuest,1-Math.Abs(Quaternion.Dot(want[i],guest.M.Rig.Q[i])));
                }
                // The stance (owner 2026-10-15: "arms back, curved forward a bit more like their holding energy in their hands";
                // before: a pitcher's set with a hand loaded beside the head): both hands low behind their shoulders, the elbows
                // behind the hands, no hand near the head, the chest squared to the aim.
                if(Math.Abs(t-3.2f)<Dt*.5f){var why=Stance(host.Pose);Check(host.State=="cyber-ranged-aim"&&why.Length==0,"the stance holds the energy back at the sides ("+why+")");}
            }
            Check(JsonSerializer.Serialize(host.A.ViewCopy())==before,"the actor is never written");
            foreach(var want in new[]{"cyber-reconfigure","cyber-ranged-aim","cyber-bit-bolt","cyber-ping-lance","cyber-lag-field","cyber-proxy-swap","cyber-root-charge","cyber-root-fire"})Check(seen.Contains(want),"shows "+want+" ("+string.Join(", ",seen)+")");
            Check(footSlip<.015f,"the feet stay planted through the switch and every cast ("+(footSlip*100).ToString("0.0")+" cm)");
            // Continuity: the casts swing the hands fast (the lob's throw), but no frame's step stands out over both its neighbours'.
            Check(handStep<.25f&&spike<.06f,"no frame jumps (the largest hand step "+(handStep*100).ToString("0.0")+" cm at "+stepAt.ToString("0.000")+" s, "+stepState+"; the largest spike over its neighbours "+(spike*100).ToString("0.0")+" cm at "+spikeAt.ToString("0.000")+" "+spikeState+")");
            Check(worstGuest<1e-6f,"a guest on a 6-frame delayed copy of the record poses the host's frames exactly ("+worstGuest.ToString("0.0e0")+")");
        }
        {
            // The aim: the held energy takes a share of the pitch (the arms stay swept back); a throw follows all of it.
            var up=new Pilot(11);var down=new Pilot(11);var p=new CyberRangedPreview();p.Switch(11,0);CyberLordFeed.RangedSource=p;up.A.Pitch=30;down.A.Pitch=-30;float dyHold=0,dyThrow=0;bool bolted=false;
            for(float t=.5f;t<2;t+=Dt)
            {
                if(!bolted&&t>=1.5f){p.Bolt(11,t,From,Ahead);bolted=true;}up.Step(t);down.Step(t);
                if(!bolted)dyHold=up.World(CapturedRig.RHand).Y-down.World(CapturedRig.RHand).Y;
                else{int hb=p.Of(11).BoltHand==1?CapturedRig.LHand:CapturedRig.RHand;dyThrow=Math.Max(dyThrow,up.World(hb).Y-down.World(hb).Y);}
            }
            Check(dyHold>.01f&&dyHold<.15f&&dyThrow>.25f,"the held energy takes a share of the pitch ("+(dyHold*100).ToString("0")+" cm higher at +30 than at -30), the throw all of it ("+(dyThrow*100).ToString("0")+" cm)");
            // Running in ranged mode: the legs keep the run's planted feet, the emitters stay up.
            var run=new Pilot(11);run.A.Velocity=new V3(0,0,Rules.Speed);CyberLordFeed.RangedSource=p;float slip=0;var prev=new Vector3[2];var on=new bool[2];bool ranState=false;float behind=0;int nb=0;
            for(float t=.5f;t<4;t+=Dt)
            {
                run.A.Position=new V3(0,0,Rules.Speed*(t-.5f));run.Step(t);if(t<1.5f)continue;ranState|=run.State=="cyber-ranged-run";
                var wp=run.Pose.WorldPosition;behind+=wp[CapturedRig.RHand].Z-wp[CapturedRig.RArm].Z+wp[CapturedRig.LHand].Z-wp[CapturedRig.LArm].Z;nb+=2;
                for(int i=0;i<2;i++){bool lf=i==0,planted=run.M.Gait.Planted(lf);var at=run.World(lf?CapturedRig.LFoot:CapturedRig.RFoot);if(planted&&on[i]){var d=at-prev[i];d.Y=0;slip=Math.Max(slip,d.Length()/Dt);}on[i]=planted;prev[i]=at;}
            }
            Check(ranState&&slip<.03f&&behind/nb<-.05f,"the ranged run: its own state, the feet planted ("+(slip*100).ToString("0.00")+" cm/s), the energy held back at the sides ("+(behind/nb*100).ToString("0")+" cm behind the shoulders on average)");
        }
        {
            // Melee untouched: with a source holding no record for the pilot, every frame is bit-identical to no source at all.
            var a=new Pilot(12);var b=new Pilot(12);var none=new CyberRangedPreview();none.Switch(99,0);int diff=0;
            for(float t=.5f;t<4;t+=Dt)
            {
                if(Math.Abs(t-1)<Dt*.5f){CyberLordFixture.Combo(a.A,1,t);CyberLordFixture.Combo(b.A,1,t);}if(Math.Abs(t-2.4f)<Dt*.5f){a.A.Velocity=b.A.Velocity=new V3(0,0,Rules.Speed);}
                a.A.Position=b.A.Position=new V3(0,0,a.A.Velocity.z*(t-.5f));
                CyberLordFeed.RangedSource=none;a.Step(t);CyberLordFeed.RangedSource=null;b.Step(t);
                for(int i=0;i<a.M.Rig.Q.Length;i++)if(a.M.Rig.Q[i]!=b.M.Rig.Q[i])diff++;
            }
            Check(diff==0,"melee mode is untouched: the frames equal those with no ranged source ("+diff+" joints differ)");
        }
        {
            // First person: the emitters in the lower view in both modes, the switch folding them in, no jumps.
            var p=new CyberRangedPreview();var a=CyberLordFixture.Pilot(13,Vector3.Zero,0);var m=new CyberLordMotion();var pose=new CapturedRig.Pose();int beat=0;Vector3? last=null;var fpSeen=new HashSet<string>();
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(p,13,t,ref beat);CyberLordFeed.RangedSource=p;fpSeen.Add(m.FirstPerson(pose,a,t));pose.World();var h=pose.WorldPosition[CapturedRig.RHand];
                Check(float.IsFinite(h.X)&&float.IsFinite(h.Y)&&float.IsFinite(h.Z),"first person stays finite");if(last.HasValue)fpStep=Math.Max(fpStep,Vector3.Distance(h,last.Value));last=h;
            }
            Check(fpSeen.Contains("cyber-reconfigure")&&fpSeen.Contains("cyber-ranged-aim")&&fpSeen.Contains("cyber-root-charge")&&fpSeen.Contains("cyber-stance"),"first person shows the switch, the hold, the casts and melee again ("+string.Join(", ",fpSeen)+")");
            Check(fpStep<.25f,"first person: no frame jumps ("+(fpStep*100).ToString("0.0")+" cm)");
        }
        yield return $"Cyber Lord ranged pose: {count-c0} checks (the energy-holding stance; all {seen.Count(s=>s!=null&&s.StartsWith("cyber-")&&(s.Contains("ranged")||s.Contains("reconfigure")||s.Contains("bolt")||s.Contains("lance")||s.Contains("field")||s.Contains("swap")||s.Contains("root")))} ranged states on their windows; the feet within {(footSlip*100):0.0} cm; hand steps at most {(handStep*100):0.0} cm with no spike over {(spike*100):0.0} cm (first person {(fpStep*100):0.0}); the held energy and the throws on the aim, the energy held back over the run; melee bit-identical; a delayed guest copy exact; the actor untouched)";

        // ------------------------------------------------------------------------------------------------------------------
        // 4. The effects
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;int full=0,reduced=0,low=0,peakShots=0,ghostsOther=0,ghostsSelf=0,viewDraws=0;
        {
            var p=new CyberRangedPreview();var a=CyberLordFixture.Pilot(21,Vector3.Zero,0);int beat=0;
            var fx=new NativeCyberLordFx();var fxR=new NativeCyberLordFx();var fxL=new NativeCyberLordFx();var fxMe=new NativeCyberLordFx();var fxG=new NativeCyberLordFx();
            var sc=new Scene{Shots=p};sc.List.Add(a);var me=new Scene{Shots=p,ViewerId=21,LocalId=21};me.List.Add(a);
            var guestSrc=new Copied{Id=21};var gsc=new Scene{Shots=p};gsc.List.Add(a);
            var sink=new Sink();var sinkR=new Sink();var sinkL=new Sink();var sinkMe=new Sink();var sinkG=new Sink();
            var drawn=new int[CyberRangedSpec.Slots];var baseline=0;
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(p,21,t,ref beat);CyberLordFeed.RangedSource=p;
                sink.Reset();fx.Draw(sink,sc,t,1,false);sinkR.Reset();fxR.Draw(sinkR,sc,t,.3f,false);sinkL.Reset();fxL.Draw(sinkL,sc,t,1,true);sinkMe.Reset();fxMe.Draw(sinkMe,me,t,1,false);
                full+=sink.Count;reduced+=sinkR.Count;low+=sinkL.Count;viewDraws+=sinkMe.View;peakShots=Math.Max(peakShots,p.CyberShots);
                for(int g=0;g<fx.Ghosts;g++)if(fx.Ghost(g).Actor==21)ghostsOther++;for(int g=0;g<fxMe.Ghosts;g++)if(fxMe.Ghost(g).Actor==21&&fxMe.Ghost(g).Kind==CyberGhost.Split)ghostsSelf++;
                if(t<SwitchIn)baseline=Math.Max(baseline,sink.Count);
                var v=new CyberLordView();CyberLordFeed.Read(a,v);for(int s=0;s<CyberRangedSpec.Slots;s++)if(v.RPhase(s,t).Live&&sink.Count>baseline)drawn[s]++;
                // A guest on a copy of the record draws the same.
                guestSrc.Record=p.Of(21)?.ViewCopy();CyberLordFeed.RangedSource=guestSrc;sinkG.Reset();fxG.Draw(sinkG,gsc,t,1,false);Check(sinkG.Count==sink.Count,"a copied record draws the same effects at "+t.ToString("0.00"));
            }
            for(int s=0;s<CyberRangedSpec.Slots;s++)Check(drawn[s]>0,CyberRangedSpec.Names[s]+" draws on its window");
            Check(sink.Flat+sinkR.Flat+sinkL.Flat+sinkMe.Flat==0,"nothing lies flat within 12 cm of the floor ("+(sink.Flat+sinkR.Flat+sinkL.Flat+sinkMe.Flat)+")");
            Check(reduced<full&&low<full,"the reduced ("+reduced+") and low ("+low+") tiers draw less than full ("+full+")");
            Check(peakShots>=2,"the bolts fly as shots ("+peakShots+" at once)");
            Check(ghostsOther>0&&ghostsSelf==0,"the switch's RGB split ghosts the pilot for others ("+ghostsOther+"), never on its own first-person view");
            Check(viewDraws>0,"first person draws its own emitter effects on the view layer ("+viewDraws+")");
        }
        {
            // Authority cuts end at Release == Until. They must not draw a phantom release at the cut.
            CyberLordFeed.RangedSource=null;
            foreach(int slot in new[]{CyberRangedSpec.Bolt,CyberRangedSpec.Lance,CyberRangedSpec.Swap,CyberRangedSpec.Root})
            {
                var a=CyberLordFixture.Pilot(23,Vector3.Zero,0);var sc=new Scene();sc.List.Add(a);
                a.Cyber=new NativeCyberKit{Ranged=new NativeCyberRangedState{Ranged=true}};float mana=100;
                NativeCyberRangedRules.Begin(a.Cyber,slot,10,ref mana,1,1,1);float cut=10+CyberRangedSpec.Windup[slot]*.5f;NativeCyberRules.Interrupt(a.Cyber,cut);
                var sink=new Sink();new NativeCyberLordFx().Draw(sink,sc,cut+.01f,1,false);int stopped=sink.Count;
                a.Cyber=new NativeCyberKit{Ranged=new NativeCyberRangedState{Ranged=true}};sink.Reset();new NativeCyberLordFx().Draw(sink,sc,cut+.01f,1,false);
                Check(stopped==sink.Count,"no phantom release effects after authority cut, slot "+slot);
            }
        }
        {
            // The sprint's streaks: in and out with the replicated flag's ease, none in the pilot's own first person.
            var a=CyberLordFixture.Pilot(22,Vector3.Zero,0);var fx=new NativeCyberLordFx();var fxMe=new NativeCyberLordFx();var sc=new Scene();sc.List.Add(a);var me=new Scene{ViewerId=22,LocalId=22};me.List.Add(a);
            var sink=new Sink();var sinkMe=new Sink();CyberLordFeed.RangedSource=null;int onFrames=0,fadeFrames=0,selfStreaks=0;
            for(float t=1;t<3;t+=Dt)
            {
                bool sprint=t>=1.5f&&t<2.2f;a.Sprinting=sprint;a.Velocity=new V3(0,0,sprint?Rules.Speed*Rules.SprintMultiplier:Rules.Speed);
                sink.Reset();fx.Draw(sink,sc,t,1,false);sinkMe.Reset();fxMe.Draw(sinkMe,me,t,1,false);selfStreaks+=sinkMe.Streaks;
                if(sprint&&t>1.7f&&sink.Streaks>=2)onFrames++;if(!sprint&&t>=2.2f&&t<2.2f+NativeSprintRules.Blend&&sink.Streaks>0)fadeFrames++;if(t>2.5f)Check(sink.Streaks==0,"the streaks are gone after the fade");
            }
            Check(onFrames>20&&fadeFrames>2,"the fists trail streaks through the sprint and fade out with the flag ("+onFrames+" frames on, "+fadeFrames+" fading)");
            Check(selfStreaks==0,"never in the pilot's own first person");
        }
        yield return $"Cyber Lord ranged effects: {count-c0} checks (each cast draws on its window; full {full}, reduced {reduced}, low {low} draws; never flat on the floor; up to {peakShots} bolts in flight; the switch's split for others only; a copied record draws the same; the sprint streaks ease with the flag)";

        // ------------------------------------------------------------------------------------------------------------------
        // 5. The look and the HUD
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;int melee=0,ranged=0;
        {
            var into=new CyberPiece[NativeCyberLordLook.Capacity];var into2=new CyberPiece[NativeCyberLordLook.Capacity];var flare=new NativeCyberLordLook.Flare();
            melee=NativeCyberLordLook.Pieces(5,-100,flare,false,1,1,false,into);int m2=NativeCyberLordLook.Pieces(5,-100,flare,false,1,1,false,into2,new NativeCyberLordLook.Mode());
            bool same=melee==m2;for(int i=0;i<melee&&same;i++)same=into[i].Bone==into2[i].Bone&&into[i].Shape==into2[i].Shape&&into[i].Position==into2[i].Position&&into[i].Scale==into2[i].Scale&&into[i].Rotation==into2[i].Rotation&&into[i].Color==into2[i].Color;
            Check(same,"melee mode builds the look piece for piece as before");
            ranged=NativeCyberLordLook.Pieces(5,-100,flare,false,1,1,false,into,new NativeCyberLordLook.Mode{Ranged=1});Check(ranged>melee&&ranged<=NativeCyberLordLook.Capacity,"ranged mode adds the emitters, the barrel and the coil ("+melee+" to "+ranged+" pieces)");
            int r2=NativeCyberLordLook.Pieces(5,-100,flare,false,1,.3f,false,into,new NativeCyberLordLook.Mode{Ranged=1}),l2=NativeCyberLordLook.Pieces(5,-100,flare,false,1,1,true,into,new NativeCyberLordLook.Mode{Ranged=1}),fp=NativeCyberLordLook.Pieces(5,-100,flare,true,1,1,false,into,new NativeCyberLordLook.Mode{Ranged=1});
            Check(r2<=ranged&&l2<=ranged&&fp>0,"the reduced ("+r2+"), low ("+l2+") and first-person ("+fp+") emitters");
            var p=new CyberRangedPreview();p.Switch(30,1);p.Root(30,3,From,From+Ahead*45);CyberLordFeed.RangedSource=p;var a=CyberLordFixture.Pilot(30,Vector3.Zero,0);var v=new CyberLordView();CyberLordFeed.Read(a,v);
            var mid=NativeCyberLordLook.ModeOf(v,1+CyberRangedSpec.SwitchSeconds*.5f);var charge=NativeCyberLordLook.ModeOf(v,3.8f);
            Check(mid.Glitch>.9f&&Math.Abs(mid.Ranged-.5f)<.01f&&charge.Charge>.7f&&charge.Ranged==1,"the switch glitches the pieces at its middle; the charge lights the emitters");
            // The HUD: Q and E stay; 1-4 swap to the ranged set; the key-5 card names the live set.
            float t=2.5f;var lance=NativeCyberLordHud.RangedOf(v,CyberRangedSpec.Lance,t,100);var mode=NativeCyberLordHud.ModeOf(v,t);
            Check(NativeCyberLordHud.ShowsRanged(v,t)&&lance.Ready&&lance.State=="READY"&&lance.Icon=="ability-ping-lance"&&mode.Key=="5"&&mode.Icon==CyberRangedSpec.ModeIcon&&mode.Ready,"ranged: the set ready, the 5 card on RANGED");
            Check(NativeCyberLordHud.RangedOf(v,CyberRangedSpec.Root,3.5f,100).State=="CHARGE"&&NativeCyberLordHud.RangedOf(v,CyberRangedSpec.Root,4.5f,100).State!="READY","Root Access: CHARGE, then its cooldown");
            Check(NativeCyberLordHud.RangedOf(v,CyberRangedSpec.Lance,t,5).State=="LOW MANA"&&NativeCyberLordHud.RangedLabels[CyberRangedSpec.Lance]=="1 · PING LANCE"&&NativeCyberLordHud.RangedLabels[CyberRangedSpec.Root]=="4 · ROOT ACCESS","low mana, and the labels 1-4");
            Check(NativeCyberLordHud.ModeOf(v,1.1f).State=="SWITCHING"&&NativeCyberLordHud.ModeOf(v,1.5f).State!="READY"&&NativeCyberLordHud.RangedMode.StartsWith("5")&&NativeCyberLordHud.MeleeMode.StartsWith("5"),"the 5 card: SWITCHING, its cooldown, the 5 hint in both modes");
            var mv=new CyberLordView();CyberLordFeed.RangedSource=null;CyberLordFeed.Read(CyberLordFixture.Pilot(31,Vector3.Zero,0),mv);Check(!NativeCyberLordHud.ShowsRanged(mv,t)&&NativeCyberLordHud.ModeOf(mv,t).Icon==CyberRangedSpec.MeleeIcon,"melee: the melee set, the 5 card on MELEE");
        }
        yield return $"Cyber Lord ranged look and HUD: {count-c0} checks (melee's look unchanged; {melee} to {ranged} pieces in ranged mode; the switch's glitch and the charge; the bar's two sets and the key-5 card)";

        // ------------------------------------------------------------------------------------------------------------------
        // 6. The sounds
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;int cues=0;
        {
            var o=new NativeKitSound[NativeCyberLordSounds.Max];var clips=new HashSet<string>(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),"*.wav").Select(Path.GetFileNameWithoutExtension));
            foreach(var k in NativeCyberLordSounds.Keys)Check(NativeSoundKeys.Known(k),"bank key "+k+" exists");foreach(var c in NativeCyberLordSounds.Clips)Check(clips.Contains(c),"fallback clip "+c+" ships");
            var p=new CyberRangedPreview();var a=CyberLordFixture.Pilot(40,Vector3.Zero,0);var v=new CyberLordView();int beat=0;var heard=new Dictionary<(int,float),int>();
            for(float t=.5f;t<End;t+=Dt)
            {
                Script(p,40,t,ref beat);CyberLordFeed.RangedSource=p;CyberLordFeed.Read(a,v);
                for(int slot=NativeCyberLordSounds.Switch;slot<NativeCyberLordSounds.Slots;slot++)
                {
                    int n=NativeCyberLordSounds.State(v,slot,o,out float at);if(n==0||!(t>=at&&t-at<.15f))continue;
                    for(int i=0;i<n;i++)Check(NativeCyberLordSounds.Keys.Contains(o[i].Key)&&NativeCyberLordSounds.Clips.Contains(o[i].Clip),"cue "+slot+" plays a listed key and clip");
                    heard[(slot,at)]=heard.TryGetValue((slot,at),out int c)?c+1:1;
                }
            }
            for(int slot=NativeCyberLordSounds.Switch;slot<NativeCyberLordSounds.Slots;slot++)Check(heard.Keys.Any(k=>k.Item1==slot),"ranged cue "+slot+" sounds in the script");
            cues=heard.Count;Check(heard.Keys.Count(k=>k.Item1==NativeCyberLordSounds.BoltShot)==5&&heard.Keys.Count(k=>k.Item1==NativeCyberLordSounds.Switch)==2,"one cue per bolt and per switch");
            Check(heard.Keys.Any(k=>k.Item1==NativeCyberLordSounds.RootTell&&Math.Abs(k.Item2-(RootAt+CyberRangedSpec.TellFrom))<1e-4f),"Root Access warns its target at the telegraph");
            // A cut cast is silent past its cut.
            var cut=new CyberRangedPreview();cut.Root(41,1,From,From+Ahead*45);cut.Interrupt(41,1.5f);CyberLordFeed.RangedSource=cut;var cv=new CyberLordView();CyberLordFeed.Read(CyberLordFixture.Pilot(41,Vector3.Zero,0),cv);
            Check(NativeCyberLordSounds.State(cv,NativeCyberLordSounds.RootFire,o,out _)==0&&NativeCyberLordSounds.State(cv,NativeCyberLordSounds.RootCharge,o,out _)>0,"a cut beam: its charge heard, never its shot");
            foreach(var h in new[]{CyberHit.Bolt,CyberHit.Lance,CyberHit.Field,CyberHit.Swap,CyberHit.Root})foreach(var kind in new[]{EventKind.Hit,EventKind.AbilityHit,EventKind.Absorb,EventKind.Impact})foreach(bool mine in new[]{false,true})
            {int n=NativeCyberLordSounds.Event(kind,h,mine,o);for(int i=0;i<n;i++)Check(NativeCyberLordSounds.Keys.Contains(o[i].Key)&&NativeCyberLordSounds.Clips.Contains(o[i].Clip),"event cue "+o[i].Key);}
            Check(NativeCyberLordSounds.Event(EventKind.AbilityHit,CyberHit.Root,false,o)>0&&NativeCyberLordSounds.Event(EventKind.Hit,CyberHit.Bolt,false,o)>0,"the beam's and a bolt's hits sound");
        }
        yield return $"Cyber Lord ranged sounds: {count-c0} checks ({NativeCyberLordSounds.Slots-NativeCyberLordSounds.Switch} state cues on existing keys and shipped fallbacks, {cues} heard once each in the script; a cut beam silent)";

        // ------------------------------------------------------------------------------------------------------------------
        // 7. No allocation per frame
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            var p=new CyberRangedPreview();var pilot=new Pilot(50);var fx=new NativeCyberLordFx();var sc=new Scene{Shots=p};sc.List.Add(pilot.A);var nul=new Sink();var view=new CyberLordView();
            var into=new CyberPiece[NativeCyberLordLook.Capacity];var o=new NativeKitSound[NativeCyberLordSounds.Max];var fpM=new CyberLordMotion();var fpPose=new CapturedRig.Pose();int beat=0;float loop=0;
            void Frame(float t)
            {
                float lt=t-loop;if(lt>=End){loop=t;lt=0;beat=0;p.Clear();}
                Script(p,50,.5f+lt,ref beat);CyberLordFeed.RangedSource=p;float at=.5f+lt;pilot.Step(at);fpM.FirstPerson(fpPose,pilot.A,at);
                fx.Draw(nul,sc,at,1,false);fx.Draw(nul,sc,at,.3f,true);CyberLordFeed.Read(pilot.A,view);
                NativeCyberLordLook.Pieces(at,-100,default,false,1,1,false,into,NativeCyberLordLook.ModeOf(view,at));
                for(int s=0;s<CyberRangedSpec.Slots;s++)NativeCyberLordHud.RangedOf(view,s,at,60);NativeCyberLordHud.ModeOf(view,at);
                for(int s=0;s<NativeCyberLordSounds.Slots;s++)NativeCyberLordSounds.State(view,s,o,out _);
            }
            for(float t=0;t<End*1.1f;t+=Dt)Frame(t);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++)Frame(End*1.1f+f*Dt);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"300 frames of the ranged set (pose, first person, effects, look, HUD, sounds) allocate "+bytes+" bytes");
        }
        yield return $"Cyber Lord ranged allocation: 0 bytes over 300 frames; {count} checks in all";
    }
}
