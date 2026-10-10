using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// The native melee hit layer (Core/NativeHitReact): melee classification and weapon heft from events, the ledger, the
/// impact envelope and timing, reaction selection by direction, weight, headshot and block, the dazed stun pose only while
/// stunned, no layer on non-melee hits, planted feet and kept two-handed grips, finite and bounded blend weights and bone
/// offsets, the bounded first-person camera kick and view-model dip, no allocation per frame, and the runtime hooks.
/// Every layered rig is compared against the same rig without the layer. Presentation only.
/// </summary>
internal static class HitReactionChecks
{
    private const float D=(float)Math.PI/180;
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Hit reactions: "+what);}
    public static void Run(string root)
    {
        var log=new List<string>();
        Classification(log);Ledger(log);Envelope(log);Selection(log);Weight(log);Head(log);Block(log);NonMelee(log);Stun(log);Planted(log);Bounds(log);Kick(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS hit reactions: "+line);
    }
    // ---- a struck pilot on one of the four routes, with and without the layer, in lockstep ----
    private sealed class Rig
    {
        public readonly Actor A;public readonly HitVictim Kind;public readonly NativeHitLayer Layer=new NativeHitLayer();public readonly NativeHitLedger Ledger;
        readonly MeleeMotion melee=new MeleeMotion();readonly GunMotion gun=new GunMotion();readonly MagicMotion magic=new MagicMotion();readonly MeleeMotion scratch=new MeleeMotion();
        readonly MeleeView view=new MeleeView();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();readonly NativeJuggleTrack juggle=new NativeJuggleTrack();readonly float[] channels=new float[13];
        public Rig(string spec,bool layered,NativeHitLedger ledger)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
            A=new Actor{Id=1,SourceSpec=spec,Weapon=w,Grounded=true,Hp=100,CastRate=1,MagicVisual=spec=="fire"?1:0,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine};
            var route=NativePoseRoute.For(A,false);Kind=route==PoseRoute.Gun?HitVictim.Gun:route==PoseRoute.Magic?HitVictim.Magic:route==PoseRoute.Fighter?HitVictim.Fighter:HitVictim.Melee;Ledger=layered?ledger:null;
        }
        void View(float t)
        {
            var a=A;var v=view;v.Id="1";v.Spec=a.SourceSpec;v.Yaw=-a.Yaw*D;v.Vel=new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);v.Grounded=a.Grounded;v.Hp=a.Hp;
            v.LastAttack=-100;v.Windup=.14f;v.AttackRate=1;v.AttackIndex=1;v.SwingAt=v.SwingUntil=float.NaN;v.Commit=false;
            v.BlockTime=t<a.BlockUntil?a.BlockUntil-t:0;v.LastBlock=a.BlockAt;v.HitStunUntil=a.StunUntil;v.RollAt=v.RollUntil=-100;v.ActionKind=null;
            v.Reaction=a.ReactionAt>-90;v.Parry=a.ReactionParry;v.ReactionAt=a.ReactionAt;v.ReactionUntil=a.ReactionUntil;v.ReactionYaw=a.ReactionYaw;v.ReactionTurn=a.ReactionTurn;v.ReactionStrength=a.ReactionStrength;
            v.ReactionDir=new Vector3(a.ReactionDir.x,a.ReactionDir.y,a.ReactionDir.z);v.ReactionPoint=new Vector3(a.ReactionPoint.x,a.ReactionPoint.y,a.ReactionPoint.z);v.ReactionZone=a.ReactionZone;v.Down=false;v.Juggle=false;
        }
        public void Step(float t,float dt)
        {
            if(Kind==HitVictim.Melee)
            {
                View(t);melee.Rig.GroupPosition=new Vector3(0,.865f,0);melee.Rig.GroupYaw=melee.PresentedYaw(view,t,view.Yaw);
                Layer.Bind(melee,A,Ledger,HitVictim.Melee);melee.Sample(view,t,dt);melee.Rig.WritePose(Pose);Pose.World();return;
            }
            if(Kind==HitVictim.Gun)
            {
                gun.Rig.GroupPosition=new Vector3(0,.865f,0);gun.Rig.GroupYaw=-A.Yaw*D;gun.Pose(A,t,Pose,false,juggle,out bool react);
                if(react){View(t);scratch.ReactOver(Pose,view,t);}
                if(Ledger!=null)Layer.Over(scratch.Rig,Pose,A,Ledger,HitVictim.Gun,t,gun.Rig.GroupYaw);gun.Finish(Pose,t);return;
            }
            magic.Rig.GroupPosition=new Vector3(0,.865f,0);magic.Rig.GroupYaw=-A.Yaw*D;magic.Pose(A,t,Pose,channels,0,false,juggle,out bool r2);
            if(r2){View(t);scratch.ReactOver(Pose,view,t);}
            if(Ledger!=null)Layer.Over(scratch.Rig,Pose,A,Ledger,HitVictim.Magic,t,magic.Rig.GroupYaw);magic.Finish(Pose,t);
        }
    }
    private static int eventId;
    // A blow from `attacker` standing at `angle` degrees toward the victim's left (0 front, 180 behind), as NativeArena
    // records it (MeleeHitReaction, then the stagger) with its Hit event (and a fighter's strike event).
    private static void Strike(Rig[] rigs,NativeHitLedger ledger,string attacker,float angle,float at,float stun,float damage,bool head=false,string detail="direct",bool fighter=false)
    {
        float a=angle*D;Vector3 body=new Vector3(0,.865f,0),from=new Vector3(-(float)Math.Sin(a),0,(float)Math.Cos(a))*1.3f+body,dir=Vector3.Normalize(body-from);
        var hit=MeleeReaction.Hit(new V3(0,.865f,0),0,new V3(dir.X,dir.Y,dir.Z),new V3(0,.965f,0),at,damage,head);
        foreach(var r in rigs)
        {
            var v=r.A;hit.Until=Math.Max(hit.Until,v.StunUntil);v.ReactionAt=hit.At;v.ReactionUntil=hit.Until;v.ReactionYaw=hit.Yaw;v.ReactionTurn=hit.Turn;v.ReactionStrength=hit.Strength;
            v.ReactionDir=new V3(hit.Direction.X,hit.Direction.Y,hit.Direction.Z);v.ReactionPoint=new V3(hit.Point.X,hit.Point.Y,hit.Point.Z);v.ReactionZone=hit.Zone;v.ReactionParry=false;
            if(stun>0)v.StunUntil=Math.Max(v.StunUntil,at+stun);
        }
        var foe=new Actor{Id=2,SourceSpec=attacker,Position=new V3(from.X,0,from.Z),Hp=100};var events=new List<ArenaEvent>();
        if(fighter)events.Add(new ArenaEvent{Id=++eventId,Kind=attacker=="emperor"?EventKind.Emperor:EventKind.Arcade,Source=2,Target=1,Time=at,Amount=damage});
        events.Add(new ArenaEvent{Id=++eventId,Kind=head?EventKind.Headshot:EventKind.Hit,Source=attacker=="add"?0:2,Target=1,Time=at,Amount=damage,Detail=detail});
        ledger.Observe(new Snapshot{Actors=new[]{rigs[0].A,foe},Events=events.ToArray()});
    }
    private sealed class Run2
    {
        public readonly Rig On,Off;public readonly NativeHitLedger Ledger=new NativeHitLedger();
        public Run2(string spec){On=new Rig(spec,true,Ledger);Off=new Rig(spec,false,null);}
        public Rig[] Both=>new[]{On,Off};
        public void Step(float t,float dt){On.Step(t,dt);Off.Step(t,dt);}
        public Vector3 Delta(int bone)=>On.Pose.WorldPosition[bone]-Off.Pose.WorldPosition[bone];
        public float Angle(int bone)=>MeleeMath.AngleTo(On.Pose.WorldRotation[bone],Off.Pose.WorldRotation[bone]);
        public float MaxDelta(){float m=0;for(int b=0;b<CapturedRig.Bones.Length;b++)m=Math.Max(m,Delta(b).Length());return m;}
    }
    private const float Hit=2,Dt=1/60f;
    // Runs a victim from t=1 with one blow at t=2 and returns per-frame samples after the blow through `observe`.
    private static Run2 Scene(string victim,string attacker,float angle,float stun,float damage,Action<Run2,float> observe,float until=3.2f,bool head=false,string detail="direct",bool fighter=false,Action<Run2,float> before=null)
    {
        var s=new Run2(victim);bool struck=false;
        for(float t=1;t<until;t+=Dt)
        {
            before?.Invoke(s,t);
            if(!struck&&t>=Hit-1e-4f){struck=true;Strike(s.Both,s.Ledger,attacker,angle,t,stun,damage,head,detail,fighter);}
            s.Step(t,Dt);if(struck)observe?.Invoke(s,t);
        }
        return s;
    }
    private static void Classification(List<string> log)
    {
        Check(NativeHitRules.Melee(0,"direct",null,false)&&!NativeHitRules.Melee(0,"secondary",null,false),"a direct source-less hit is a PvE add's swing; a stage hit is not");
        foreach(var s in new[]{"sword","daggers","greatsword","shield"}){Check(NativeHitRules.Melee(2,"direct",s,false),s+" direct hits are melee");Check(!NativeHitRules.Melee(2,"thorns",s,false)&&!NativeHitRules.Melee(2,"secondary",s,false),s+" thorns and secondary damage are not melee");}
        foreach(var s in new[]{"pistol","rifle","machinegun","fire","water","lightning","darkness","leet"})Check(!NativeHitRules.Melee(2,"direct",s,false)&&!NativeHitRules.Melee(2,"direct",s,true),s+" never melees");
        foreach(var s in new[]{"emperor","ansatsuken"})Check(NativeHitRules.Melee(2,"direct",s,true)&&!NativeHitRules.Melee(2,"direct",s,false),s+": a strike is melee, a Hadoken is not");
        Check(NativeHitRules.Melee(2,"secondary","siren",false)&&!NativeHitRules.Melee(2,"direct","siren",false),"the Siren's claws (secondary) are melee, her shots are not");
        float dag=NativeHitRules.Heft("daggers",15),sw=NativeHitRules.Heft("sword",30),sh=NativeHitRules.Heft("shield",23),gs=NativeHitRules.Heft("greatsword",52);
        Check(dag<sw&&sw<sh&&sh<gs,"heft follows the content weights: daggers < sword < shield < greatsword");
        foreach(var s in new[]{"daggers","sword","shield","greatsword","siren","emperor","ansatsuken",null,"add","pistol","fire"})foreach(float amount in new[]{-5f,0,8,30,500,float.NaN,float.PositiveInfinity})
        {float h=NativeHitRules.Heft(s,amount);Check(h>=NativeHitRules.MinHeft-1e-6f&&h<=NativeHitRules.MaxHeft+1e-6f&&!float.IsNaN(h),"heft bounded for "+(s??"no spec")+" at "+amount);}
        Check(NativeHitRules.Heft("emperor",8)<NativeHitRules.Heft("emperor",18)&&NativeHitRules.Heft("ansatsuken",30)<=1.35f,"a fighter strike's heft grows with its damage");
        log.Add("melee classification (adds, standard melee, fighter strikes vs Hadoken, Siren claws vs shots, guns and magic never) and bounded weapon heft daggers "+dag+" < sword "+sw+" < shield "+sh+" < greatsword "+gs);
    }
    private static void Ledger(List<string> log)
    {
        var l=new NativeHitLedger();var victim=new Actor{Id=1,Hp=100};var sword=new Actor{Id=2,SourceSpec="sword",Position=new V3(0,0,1.3f)};var gun=new Actor{Id=3,SourceSpec="pistol"};var emp=new Actor{Id=4,SourceSpec="emperor"};
        Snapshot S(params ArenaEvent[] e)=>new Snapshot{Actors=new[]{victim,sword,gun,emp},Events=e};
        var e1=new ArenaEvent{Id=10,Kind=EventKind.Hit,Source=2,Target=1,Time=5,Amount=30,Detail="direct"};l.Observe(S(e1));
        Check(l.Hit(1,5,out var h)&&h.Melee&&Math.Abs(h.Heft-1)<1e-6f&&h.HasFrom&&h.From.z==1.3f,"a sword hit is recorded as melee with its heft and the attacker's position");
        Check(!l.Hit(1,5.1f,out _),"a reaction at another time does not match");
        l.Observe(S(e1,new ArenaEvent{Id=11,Kind=EventKind.Headshot,Source=3,Target=1,Time=6,Amount=30,Detail="direct"}));
        Check(l.Hit(1,6,out h)&&!h.Melee&&h.Head,"a pistol headshot is recorded as a non-melee headshot");
        l.Observe(S(new ArenaEvent{Id=12,Kind=EventKind.Emperor,Source=4,Target=1,Time=7,Amount=18,Detail="jab"},new ArenaEvent{Id=13,Kind=EventKind.Hit,Source=4,Target=1,Time=7,Amount=18,Detail="direct"}));
        Check(l.Hit(1,7,out h)&&h.Melee&&h.Heft>NativeHitRules.Heft("emperor",8),"an Emperor strike's hit is melee, heft by the strike's damage");
        l.Observe(S(new ArenaEvent{Id=14,Kind=EventKind.Hit,Source=4,Target=1,Time=8,Amount=23,Detail="direct"}));
        Check(l.Hit(1,8,out h)&&!h.Melee,"an Emperor Hadoken hit (no strike event) is not melee");
        l.Observe(S(new ArenaEvent{Id=15,Kind=EventKind.Block,Source=1,Target=2,Time=9}));
        Check(l.Block(1,9.2f,.5f,out var b)&&b.Melee&&b.HasFrom&&!l.Block(1,9.6f,.5f,out _)&&!l.Block(2,9.2f,.5f,out _),"a block is recorded for the blocker with the attacker's position, within its window");
        // Re-reading the same events changes nothing; ids restarting (a new arena or the killcam) start over.
        l.Observe(S(new ArenaEvent{Id=13,Kind=EventKind.Hit,Source=3,Target=1,Time=7,Detail="direct"},new ArenaEvent{Id=15,Kind=EventKind.Block,Source=1,Target=3,Time=9.1f}));Check(l.Hit(1,8,out _)&&l.Block(1,9.2f,.5f,out b)&&b.At==9,"events are read once per id");
        l.Observe(S(new ArenaEvent{Id=2,Kind=EventKind.Hit,Source=2,Target=1,Time=1,Detail="direct"}));Check(l.Hit(1,1,out h)&&h.Melee&&!l.Block(1,9.2f,.5f,out _),"restarted event ids start a fresh ledger");
        l.Observe(null);l.Observe(new Snapshot());Check(true,"null snapshots are ignored");
        log.Add("ledger: melee and heft by attacker, fighter strike vs Hadoken, headshot flag, blocks with the attacker side and window, events read once, restart on new ids");
    }
    private static void Envelope(List<string> log)
    {
        float lo=1,hi=0;
        foreach(float heft in new[]{.5f,.6f,1,1.4f,1.5f})foreach(float span in new[]{0f,.01f,.05f,.1f,.193f,.25f,.376f,.593f,1.4f,3})
        {
            NativeHitRules.Timing(heft,span,out float rise,out float hold,out float fall);
            Check(rise>=0&&hold>=0&&fall>=0&&rise+hold+fall<=Math.Max(span,0)+1e-5f,"timing fits the span "+span+" at heft "+heft);
            for(float age=-.1f;age<4;age+=.002f){float e=NativeHitRules.Envelope(age,rise,hold,fall);lo=Math.Min(lo,e);hi=Math.Max(hi,e);Check(!float.IsNaN(e)&&e>=-.02f&&e<=1,"envelope bounded");if(age>=rise+hold+fall||age<=0)Check(e==0,"envelope 0 outside the blow at "+age);}
        }
        NativeHitRules.Timing(1,.3f,out float r1,out float h1,out float f1);Check(NativeHitRules.Envelope(r1,r1,h1,f1)==1&&NativeHitRules.Envelope(r1+h1*.5f,r1,h1,f1)==1,"the impact frame holds the peak");
        NativeHitRules.Timing(.6f,1,out float rl,out float hl,out _);NativeHitRules.Timing(1.4f,1,out float rh,out float hh,out _);Check(rh>rl&&hh>hl,"heavier blows rise and hold longer");
        Check(NativeHitRules.Envelope(float.NaN,.03f,.03f,.2f)==0&&NativeHitRules.Envelope(.1f,.03f,.03f,float.NaN)==0,"non-finite inputs give 0");
        // The daze: 0 before the blow and from the stun end, near 1 mid stun, continuous.
        float prev=0,jump=0,peak=0;
        for(float t=1.9f;t<3.7f;t+=.001f){float w=NativeHitRules.Daze(t,2,3.4f);Check(w>=0&&w<=1,"daze bounded");if(t<2||t>=3.4f)Check(w==0,"daze only while stunned ("+t+")");jump=Math.Max(jump,Math.Abs(w-prev));prev=w;peak=Math.Max(peak,w);}
        Check(peak>.99f&&jump<.03f,"daze eases in and out smoothly ("+jump+")");
        Check(NativeHitRules.Daze(2.2499f,2,2.25f)<.01f,"a short stun's daze is gone by its end");
        Check(NativeHitRules.Variant(2)!=NativeHitRules.Variant(2+1/60f)&&NativeHitRules.Variant(float.NaN)==1,"successive ticks alternate the variant");
        log.Add("impact envelope in ["+lo.ToString("0.000")+", "+hi+"] with a held impact frame, timing inside every span, heavier blows slower, daze 0 outside the stun (max step "+jump.ToString("0.0000")+")");
    }
    private static void Selection(List<string> log)
    {
        // The victim's facing frame: +z forward, +x the rig's left (source frame).
        Vector3 Peak(string victim,float angle,int bone)
        {
            Vector3 best=Vector3.Zero;Scene(victim,"sword",angle,.25f,30,(s,t)=>{var d=s.Delta(bone);if(d.Length()>best.Length())best=d;},2.4f);return best;
        }
        foreach(var victim in new[]{"sword","rifle","fire","emperor"})
        {
            if(victim=="emperor")continue; // fighter style clips need the baked source motion (Runtime), checked offline
            var front=Peak(victim,0,CapturedRig.Head);Check(front.Z<-.02f,victim+": a blow from the front snaps the head back ("+front+")");
            var back=Peak(victim,180,CapturedRig.Chest);Check(back.Z>.01f,victim+": a blow from behind lurches the torso forward ("+back+")");
            var right=Peak(victim,-90,CapturedRig.Chest);Check(right.X>.01f,victim+": a blow from the right pushes the torso to the left ("+right+")");
            var left=Peak(victim,90,CapturedRig.Chest);Check(left.X<-.01f,victim+": a blow from the left pushes the torso to the right ("+left+")");
            log.Add(victim+" reacts away from the attacker: front head "+front.Z.ToString("0.000")+" m, back chest +"+back.Z.ToString("0.000")+" m, right/left chest "+right.X.ToString("0.000")+"/"+left.X.ToString("0.000")+" m");
        }
        // Chained blows read as separate hits: each new blow starts a new impact peak.
        var s2=new Run2("sword");int peaks=0;float last=0;bool rising=false;
        for(float t=1;t<3.2f;t+=Dt)
        {
            foreach(float h in new[]{2f,2.2f,2.4f})if(t>=h-1e-4f&&t<h+Dt-1e-4f)Strike(s2.Both,s2.Ledger,"emperor",0,t,.25f,8,fighter:true);
            s2.Step(t,Dt);float w=s2.On.Layer.ImpactWeight;if(w>last+1e-4f&&!rising&&w>.2f){rising=true;}if(rising&&w<last-1e-4f){peaks++;rising=false;}last=w;
        }
        Check(peaks>=3,"three chained blows give three impact peaks ("+peaks+")");
        log.Add("three chained blows give "+peaks+" separate impact peaks");
    }
    private static void Weight(List<string> log)
    {
        // Equal damage and stun: a light hit's whole layer, and a stunned hit's torso (the daze is the same for every weapon).
        float Max(string victim,string attacker,float stun,int bone){float m=0;Scene(victim,attacker,0,stun,30,(s,t)=>m=Math.Max(m,bone<0?s.MaxDelta():s.Delta(bone).Length()),2.8f);return m;}
        foreach(var victim in new[]{"sword","rifle","fire"})foreach(bool stunned in new[]{false,true})
        {
            float stun=stunned?.25f:0;int bone=stunned?CapturedRig.Chest:-1;
            float light=Max(victim,"daggers",stun,bone),mid=Max(victim,"sword",stun,bone),heavy=Max(victim,"greatsword",stun,bone);
            Check(light<mid&&mid<heavy&&heavy>light*1.6f,victim+(stunned?" stunned":" light")+": the layer scales with the attacker's weapon (daggers "+light+" < sword "+mid+" < greatsword "+heavy+")");
            log.Add(victim+(stunned?" stunned torso":" light hit")+" by weapon at equal damage: daggers "+light.ToString("0.000")+" m < sword "+mid.ToString("0.000")+" m < greatsword "+heavy.ToString("0.000")+" m");
        }
    }
    private static void Head(List<string> log)
    {
        float Snap(bool head){float m=0;Scene("sword","daggers",0,.196f,22,(s,t)=>m=Math.Max(m,s.Angle(CapturedRig.Head)-s.Angle(CapturedRig.Chest)),2.6f,head:head);return m;}
        float body=Snap(false),head=Snap(true);
        Check(head>body*1.4f&&head>.1f,"a headshot snaps the head ("+head+" rad over the chest vs "+body+")");
        log.Add("a thrown-dagger headshot snaps the head "+head.ToString("0.000")+" rad past the chest vs "+body.ToString("0.000")+" for a body hit");
    }
    private static void Block(List<string> log)
    {
        foreach(var victim in new[]{"sword","rifle"})
        {
            float shove=0,none=0;
            foreach(bool evented in new[]{true,false})
            {
                var s=new Run2(victim);bool sent=false;
                for(float t=1;t<3;t+=Dt)
                {
                    foreach(var r in s.Both){r.A.BlockAt=Hit-.12f;r.A.BlockUntil=Hit+.35f;}
                    if(evented&&!sent&&t>=Hit-1e-4f){sent=true;s.Ledger.Observe(new Snapshot{Actors=new[]{s.On.A,new Actor{Id=2,SourceSpec="greatsword",Position=new V3(0,0,1.3f)}},Events=new[]{new ArenaEvent{Id=++eventId,Kind=EventKind.Block,Source=1,Target=2,Time=t}}});}
                    s.Step(t,Dt);if(t>=Hit){float d=s.MaxDelta();if(evented)shove=Math.Max(shove,d);else none=Math.Max(none,d);}
                }
            }
            Check(shove>.01f&&none<1e-5f,victim+": a blocked greatsword blow shoves the guard ("+shove+"), a raised guard alone does nothing ("+none+")");
            log.Add(victim+" block hit shoves the guard "+shove.ToString("0.000")+" m (none without a blow)");
        }
    }
    private static void NonMelee(List<string> log)
    {
        foreach(var victim in new[]{"sword","rifle","fire"})
        {
            float m=0;Scene(victim,"pistol",0,0,23,(s,t)=>m=Math.Max(m,s.MaxDelta()),3);
            Check(m<1e-5f,victim+": a pistol hit keeps only the source's light reaction (layer "+m+")");
            float g=0;Scene(victim,"fire",0,0,24,(s,t)=>g=Math.Max(g,s.MaxDelta()),3);Check(g<1e-5f,victim+": a magic hit adds nothing ("+g+")");
        }
        log.Add("pistol and magic hits add no layer on sword, rifle and fire pilots (the source's light reaction only)");
    }
    private static void Stun(List<string> log)
    {
        foreach(var victim in new[]{"sword","rifle","fire"})
        {
            float peak=0,outside=0,after=0;
            Scene(victim,"daggers",0,.593f,15,(s,t)=>
            {
                float w=s.On.Layer.DazeWeight;if(t<Hit+.593f)peak=Math.Max(peak,w);else outside=Math.Max(outside,w);
                if(t>Hit+.593f+.8f)after=Math.Max(after,s.MaxDelta());
            },3.6f);
            Check(peak>.9f&&outside==0,victim+": the dazed stun pose holds while stunned ("+peak+") and is 0 after ("+outside+")");
            Check(after<.003f,victim+": after the stun the pose is the route's own again ("+after+" m)");
            log.Add(victim+" dazed stun pose: weight "+peak.ToString("0.00")+" during a .593 s dagger stun, 0 after, back to the route within "+(after*1000).ToString("0.00")+" mm");
        }
    }
    private static void Planted(List<string> log)
    {
        float feet=0,grip=0,gunGrip=0;
        Scene("sword","greatsword",0,.376f,52,(s,t)=>{if(t<Hit+.376f)foreach(int f in new[]{CapturedRig.LFoot,CapturedRig.RFoot})feet=Math.Max(feet,s.Delta(f).Length());},2.6f);
        Scene("greatsword","sword",0,.25f,30,(s,t)=>{float on=Vector3.Distance(s.On.Pose.WorldPosition[CapturedRig.LHand],s.On.Pose.WorldPosition[CapturedRig.RHand]),off=Vector3.Distance(s.Off.Pose.WorldPosition[CapturedRig.LHand],s.Off.Pose.WorldPosition[CapturedRig.RHand]);grip=Math.Max(grip,Math.Abs(on-off));},2.6f);
        Scene("rifle","greatsword",0,.376f,52,(s,t)=>{float on=Vector3.Distance(s.On.Pose.WorldPosition[CapturedRig.LHand],s.On.Pose.WorldPosition[CapturedRig.RHand]),off=Vector3.Distance(s.Off.Pose.WorldPosition[CapturedRig.LHand],s.Off.Pose.WorldPosition[CapturedRig.RHand]);gunGrip=Math.Max(gunGrip,Math.Abs(on-off));},2.6f);
        Check(feet<.01f,"a stunned melee pilot's feet stay planted under the layer ("+feet+" m)");
        Check(grip<.002f&&gunGrip<.002f,"two-handed grips keep their hands together (greatsword "+grip+", rifle "+gunGrip+")");
        log.Add("planted feet within "+(feet*1000).ToString("0.0")+" mm while stunned; greatsword and rifle grips within "+(Math.Max(grip,gunGrip)*1000).ToString("0.00")+" mm");
    }
    private static void Bounds(List<string> log)
    {
        var rnd=new Random(1177);string[] victims={"sword","daggers","greatsword","shield","rifle","pistol","machinegun","fire"},attackers={"sword","daggers","greatsword","shield","emperor","ansatsuken","add","siren","pistol"};
        float worstAngle=0,worstBody=0,worstImpact=0;int frames=0;
        for(int n=0;n<40;n++)
        {
            string victim=victims[rnd.Next(victims.Length)],attacker=attackers[rnd.Next(attackers.Length)];float angle=(float)rnd.NextDouble()*360-180,stun=(float)(rnd.NextDouble()*1.5),dmg=(float)(rnd.NextDouble()*80);
            bool head=rnd.Next(4)==0;bool fighter=NativeHitRules.Fighter(attacker);string detail=attacker=="siren"?"secondary":"direct";
            var s=Scene(victim,attacker,angle,stun,dmg,(r,t)=>
            {
                frames++;
                for(int b=0;b<CapturedRig.Bones.Length;b++){var p=r.On.Pose.WorldPosition[b];Check(!float.IsNaN(p.X+p.Y+p.Z)&&!float.IsInfinity(p.X+p.Y+p.Z),"finite bones ("+victim+" by "+attacker+")");worstAngle=Math.Max(worstAngle,r.Angle(b));}
                worstBody=Math.Max(worstBody,r.Delta(CapturedRig.Body).Length());
                var L=r.On.Layer;Check(L.ImpactWeight>=-.05f&&L.ImpactWeight<=1.0001f&&L.DazeWeight>=0&&L.DazeWeight<=1&&L.BlockWeight>=-.05f&&L.BlockWeight<=1.0001f,"blend weights bounded");worstImpact=Math.Max(worstImpact,L.ImpactWeight);
            },3.4f,head,detail,fighter);
        }
        Check(worstAngle<1.2f&&worstBody<.12f,"bounded bone offsets (max "+worstAngle+" rad, hips "+worstBody+" m)");
        log.Add(frames+" frames over 40 random blows (victims, weapons, sides, stuns, damage, headshots): finite, bone turn <= "+worstAngle.ToString("0.000")+" rad, hips <= "+(worstBody*100).ToString("0.0")+" cm, weights in range");
    }
    private static void Kick(List<string> log)
    {
        Actor Struck(float angle,float at,bool head=false)
        {
            float a=angle*D;Vector3 body=new Vector3(0,.865f,0),from=new Vector3(-(float)Math.Sin(a),0,(float)Math.Cos(a))*1.3f+body,dir=Vector3.Normalize(body-from);
            var h=MeleeReaction.Hit(new V3(0,.865f,0),0,new V3(dir.X,dir.Y,dir.Z),new V3(0,.965f,0),at,30,head);
            return new Actor{Id=1,Hp=100,ReactionAt=h.At,ReactionUntil=h.Until,ReactionYaw=h.Yaw,ReactionStrength=h.Strength,ReactionDir=new V3(h.Direction.X,h.Direction.Y,h.Direction.Z),ReactionZone=h.Zone,StunUntil=at+.25f};
        }
        HitSource Melee(float at,float heft=1)=>new HitSource{Known=true,Melee=true,At=at,Heft=heft};
        float Peak(Actor a,HitSource h,float scale,Func<NativeHitRules.Kick,float> f){float m=0;for(float t=1.9f;t<2.8f;t+=.004f){NativeHitRules.CameraKick(a,t,h,default,scale,out var k);float v=f(k);if(Math.Abs(v)>Math.Abs(m))m=v;}return m;}
        var front=Struck(0,2);float pitch=Peak(front,Melee(2),1,k=>k.Pitch);Check(pitch<-.5f,"a blow from the front pitches the view up ("+pitch+")");
        Check(Peak(Struck(180,2),Melee(2),1,k=>k.Pitch)>.5f,"a blow from behind pitches the view down");
        Check(Peak(Struck(-90,2),Melee(2),1,k=>k.Roll)>.5f&&Peak(Struck(90,2),Melee(2),1,k=>k.Roll)<-.5f,"a side blow rolls the view with the push");
        Check(Math.Abs(Peak(front,Melee(2,1.4f),1,k=>k.Pitch))>Math.Abs(Peak(front,Melee(2,.6f),1,k=>k.Pitch))*1.5f,"the kick scales with heft");
        Check(Peak(front,Melee(2),0,k=>k.Pitch)==0&&!NativeHitRules.CameraKick(front,2.05f,Melee(2),default,0,out _),"camera shake off: no kick");
        float reduced=Peak(front,Melee(2),.4f,k=>k.Pitch);Check(reduced<0&&Math.Abs(reduced)<=Math.Abs(pitch)*.5f,"reduced effects scale the kick down ("+reduced+" vs "+pitch+")");
        Check(Peak(front,new HitSource{Known=true,Melee=false,At=2,Heft=1},1,k=>k.Pitch)==0,"a non-melee hit does not kick");
        Check(Peak(front,Melee(2.5f),1,k=>k.Pitch)==0,"a hit that is not this reaction does not kick");
        Check(!NativeHitRules.CameraKick(front,2.6f,Melee(2),default,1,out _),"the kick is over within .5 s");
        var block=new HitSource{Known=true,Melee=true,At=2,Heft=1.4f,HasFrom=true,From=new V3(0,0,1.3f)};var still=new Actor{Id=1,Hp=100};float bp=0;
        for(float t=1.9f;t<2.8f;t+=.004f){NativeHitRules.CameraKick(still,t,default,block,1,out var k);bp=Math.Min(bp,k.Pitch);}
        Check(bp<-.1f&&Math.Abs(bp)<Math.Abs(Peak(front,Melee(2,1.4f),1,k=>k.Pitch))*.6f,"a blocked blow kicks lightly ("+bp+")");
        var rnd=new Random(77);float worst=0;
        for(int i=0;i<4000;i++)
        {
            var a=Struck((float)rnd.NextDouble()*360,2,rnd.Next(3)==0);if(rnd.Next(10)==0)a.ReactionStrength=float.NaN;if(rnd.Next(10)==0)a.ReactionDir=new V3(float.NaN,0,float.PositiveInfinity);
            NativeHitRules.CameraKick(a,1.9f+(float)rnd.NextDouble(),Melee(2,(float)rnd.NextDouble()*3),block,(float)rnd.NextDouble()*2,out var k);
            Check(Math.Abs(k.Pitch)<=NativeHitRules.KickPitch&&Math.Abs(k.Yaw)<=NativeHitRules.KickYaw&&Math.Abs(k.Roll)<=NativeHitRules.KickRoll&&Math.Abs(k.Offset.X)<=NativeHitRules.KickOffset&&Math.Abs(k.Offset.Y)<=NativeHitRules.KickOffset&&Math.Abs(k.Offset.Z)<=NativeHitRules.KickOffset&&!float.IsNaN(k.Pitch+k.Yaw+k.Roll+k.Offset.X+k.Offset.Y+k.Offset.Z)&&k.Weight>=0&&k.Weight<=1,"kick bounded");
            worst=Math.Max(worst,Math.Max(Math.Abs(k.Pitch),Math.Abs(k.Roll)));
        }
        // The first-person view model's guard drop: only while stunned, gone at the stun end.
        float dipMid=NativeHitRules.ViewDip(front,2.15f,default,out float dp).Y,dipEnd=NativeHitRules.ViewDip(front,2.25f,default,out float de).Y,dipNone=NativeHitRules.ViewDip(new Actor{Id=1,Hp=100},2.1f,default,out _).Y;
        Check(dipMid<-.02f&&dp>2&&dipEnd==0&&de==0&&dipNone==0,"the view model drops its guard only while stunned");
        log.Add("first-person kick: front up "+pitch.ToString("0.00")+" deg, back down, side roll with the push, heft-scaled, off with camera shake, reduced effects "+reduced.ToString("0.00")+" deg, none for non-melee, block light; bounded (max "+worst.ToString("0.00")+" deg over 4000 random inputs); view-model guard drop only while stunned");
    }
    private static void Allocation(List<string> log)
    {
        var ledger=new NativeHitLedger();var layer=new NativeHitLayer();var scratch=new MeleeRig();var pose=new CapturedRig.Pose();
        var a=new Actor{Id=1,SourceSpec="rifle",Weapon=WeaponId.Rifle,Hp=100,Grounded=true,ReactionAt=2,ReactionUntil=2.24f,ReactionStrength=.9f,ReactionDir=new V3(0,0,-1),StunUntil=2.4f,BlockAt=2.5f,BlockUntil=2.9f};
        var snap=new Snapshot{Actors=new[]{a,new Actor{Id=2,SourceSpec="sword"}},Events=new[]{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=2,Target=1,Time=2,Detail="direct"},new ArenaEvent{Id=2,Kind=EventKind.Block,Source=1,Target=2,Time=2.6f}}};
        ledger.Observe(snap);for(float t=1.9f;t<2.5f;t+=Dt){layer.Over(scratch,pose,a,ledger,HitVictim.Gun,t,0);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<600;i++){float t=1.95f+i*Dt*.1f;ledger.Observe(snap);layer.Over(scratch,pose,a,ledger,HitVictim.Gun,t,0);ledger.Hit(1,2,out var h);ledger.Block(1,t,.5f,out var b);NativeHitRules.CameraKick(a,t,h,b,1,out _);NativeHitRules.ViewDip(a,t,h,out _);}
        long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes over 600 frames)");
        log.Add("no allocation per frame (ledger, layer, kick and view dip over 600 frames)");
    }
    private static void Hooks(string root,List<string> log)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string ca=R("CapturedAnimation.cs"),ap=R("ArenaPresentation.cs"),cg=R("ConversionGame.cs"),na=R("NativeArena.cs"),vp=R("NativeViewPresentation.cs"),mm=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/MeleeMotion.cs"));
        Check(ca.Contains("hitLayer.Bind(melee,a,Hits,HitVictim.Melee);")&&ca.Contains("hitLayer.Bind(melee,a,Hits,HitVictim.Fighter);")&&ca.Contains("HitVictim.Gun,time,gunMotion.Rig.GroupYaw")&&ca.Contains("HitVictim.Magic,time,magicMotion.Rig.GroupYaw"),"all four third-person routes run the layer");
        Check(ca.IndexOf("hitLayer.Bind(melee,a,Hits,HitVictim.Melee);")<ca.IndexOf("melee.Sample(v,time,dt"),"the captured-melee route binds the layer before sampling");
        Check(mm.Contains("HitLayer?.Layer(r,time);\n            gait.GroundPass(r,")&&mm.Contains("HitLayer?.Layer(Rig,time);\n            gait.GroundPass(Rig,"),"MeleeMotion runs the layer right before its ground passes");
        Check(ap.Contains("ObserveHits(s);")&&ap.IndexOf("ObserveHits(s);")<ap.IndexOf("PrepareCapturedPose(a,p,renderTime)")&&ap.Contains("rig.Hits=hitLedger;")&&ap.Contains("hitLedger.Clear();"),"the presentation reads the events before posing, hands rigs the ledger and clears it on reset");
        int aim=cg.IndexOf("pending.AimTarget="),kick=cg.IndexOf("presentation.HitKick(");
        Check(aim>0&&kick>aim&&cg.Contains("presentation.FirstPerson&&prefs.cameraShake")&&cg.Contains("prefs.reducedEffects?.4f:1"),"the kick is first person only, after the aim point, gated by camera shake and scaled by reduced effects");
        Check(na.Contains("MeleeHitReaction(target,direction,hitPoint,actual,head,direct||melee);"),"melee blows dealt as non-direct damage (the Siren's claws) record a reaction");
        Check(vp.Contains("offset+=HitViewDip(source,time,out float dip);"),"the view model drops its guard while stunned");
        log.Add("runtime hooks: four routes, MeleeMotion before the ground pass, events before posing, kick after aim and gated by settings, Siren claw reactions, view-model guard drop");
    }
}
