using System;
using System.Collections.Generic;
using System.Reflection;
using ChipArena.UnityConversion;

// Claude's overnight follow-up to Astra's landing attacks and Q/E kit state (PR #136, docs/MELEE-SIMULATION-HANDOFF.md):
// the owner's .6 horizontal carry through the hang and plunge, only the daggers carrying their combo on after the impact,
// lava swimming keeping its ordinary swings, and the presentation reading the new replicated data (the Kunai recast's
// explicit recovery still drawing its arrival stab, the Bulwark wall-slam flag, the pilot Hit direction for the stagger).
internal static class JumpAttackFollowupChecks
{
    static void Check(bool ok,string why){if(!ok)throw new Exception("Jump attack follow-up: "+why);}
    static bool Near(float a,float b,float e=.0005f)=>Math.Abs(a-b)<=e;
    static V3 Floor(V3 from,V3 to)=>new V3(to.x,Math.Max(0,to.y),to.z);
    static bool Ground(V3 p)=>p.y<=0;
    public static void Run(ConversionContent data)
    {
        Carry(data);Combo(data);Swimming(data);KunaiRecast();HitDirection();
        Console.WriteLine("PASS jump attack follow-up: .6 horizontal carry through hang/plunge with guest replay, dagger-only combo after the impact, lava swims keep ordinary swings, Kunai recast recovery stab, hit-direction stagger with attacker fallback");
    }
    static NativePredictionState Air(ConversionContent data,string spec,V3 velocity,float height=3)
        =>new NativePredictionState{JumpSpec=spec,Time=1,AirborneAt=.8f,Position=new V3(0,height,0),Velocity=velocity,CanMove=true,MeleeWeapon=data.Weapon(spec),
            MeleeStats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>()),Gravity=23,Speed=6.8f,Stamina=100,Combo=-1,BufferUntil=-1};
    // The owner's "plunge at about -14 m/s with horizontal speed x.6": the committed horizontal velocity is carried at .6 through
    // the hang and plunge (input cannot steer it), host-seeded guests replay it exactly, and the landing stops it.
    static void Carry(ConversionContent data)
    {
        foreach(string spec in new[]{"daggers","sword","shield","greatsword"})
        {
            var start=Air(data,spec,new V3(5,2,-3));var predictor=new NativePrediction();predictor.Reset(start,0);
            predictor.Push(new InputFrame{Sequence=1,Attack=true,X=-1},Floor,Ground);var s=predictor.State.Swing;
            Check(s!=null&&s.Air&&Near(s.Carry.x,3)&&Near(s.Carry.z,-1.8f)&&s.Carry.y==0,"carry is .6 of the committed horizontal velocity "+spec);
            Check(Near(predictor.State.Velocity.x,3)&&Near(predictor.State.Velocity.z,-1.8f)&&predictor.State.Velocity.y==0,"hang holds height and carries "+spec);
            int n=1;float x0=predictor.State.Position.x;
            while(++n<200&&NativeAirAttackSimulation.Falling(predictor.State.Swing))
            {
                predictor.Push(new InputFrame{Sequence=n,X=n%2==0?-1:1,Z=1},Floor,Ground);
                if(NativeAirAttackSimulation.Falling(predictor.State.Swing))Check(Near(predictor.State.Velocity.x,3)&&Near(predictor.State.Velocity.z,-1.8f),"input cannot steer the plunge "+spec);
            }
            var landed=predictor.State;
            Check(landed.Grounded&&landed.Swing!=null&&landed.Swing.ImpactAt>0,"plunge lands "+spec);
            float flight=landed.Swing.ImpactAt-landed.Swing.At;
            Check(Near(landed.Position.x-x0,3*flight,.02f),"horizontal travel is carry x airtime "+spec+" "+(landed.Position.x-x0)+" vs "+3*flight);
            predictor.Push(new InputFrame{Sequence=++n,X=1},Floor,Ground);Check(predictor.State.Velocity.x==0&&predictor.State.Velocity.z==0,"the landing recovery roots "+spec);
            // Acknowledgement replay of the whole flight reproduces it without repaying stamina or relaunching.
            var before=predictor.State;predictor.Reconcile(start,0,Floor,Ground);
            Check((before.Position-predictor.State.Position).Length<.001f&&before.Stamina==predictor.State.Stamina,"replayed carry "+spec);
            // A guest rebuilt mid-plunge from the host's replicated move velocity (the carry) continues the same line.
            var mid=start;mid=NativePrediction.Step(mid,new InputFrame{Attack=true},Floor,Ground);for(int i=0;i<12;i++)mid=NativePrediction.Step(mid,new InputFrame(),Floor,Ground);
            var rebuilt=NativeMeleeFeel.Begin(mid.MeleeWeapon,mid.MeleeStats,mid.Swing.At,mid.Swing.Index,mid.Swing.Yaw);NativeAirAttackSimulation.Apply(rebuilt);rebuilt.Carry=new V3(mid.Velocity.x,0,mid.Velocity.z);
            var seeded=mid;seeded.Swing=rebuilt;var a=NativePrediction.Step(mid,new InputFrame{X=1},Floor,Ground);var b=NativePrediction.Step(seeded,new InputFrame{X=1},Floor,Ground);
            Check((a.Position-b.Position).Length<1e-5f,"rebuilt guest swing continues the host carry "+spec);
        }
        var still=Air(data,"sword",new V3(0,0,0));still=NativePrediction.Step(still,new InputFrame{Attack=true},Floor,Ground);
        Check(still.Swing.Air&&still.Swing.Carry.Length==0&&still.Velocity.x==0&&still.Velocity.z==0,"a standing jump still drops straight");
    }
    // "Only daggers may chain into a combo afterwards": after the impact the daggers' next swing continues their combo; every
    // other weapon's next swing is cut A again.
    static void Combo(ConversionContent data)
    {
        foreach(string spec in new[]{"daggers","sword","shield","greatsword"})
        {
            var p=Air(data,spec,new V3(0,0,0),.5f);p=NativePrediction.Step(p,new InputFrame{Attack=true},Floor,Ground);int air=p.Swing.Index;
            for(int i=0;i<200&&p.Swing!=null&&NativeAirAttackSimulation.Falling(p.Swing);i++)p=NativePrediction.Step(p,new InputFrame(),Floor,Ground);
            var s=p.Swing;Check(s!=null&&s.ImpactAt>0,"landed "+spec);
            Check(spec=="daggers"?Near(p.ComboUntil,s.Until+NativeMeleeFeel.ComboPause):Near(p.ComboUntil,s.ImpactAt),"combo window after the impact "+spec);
            for(int i=0;i<200&&p.Swing!=null;i++)p=NativePrediction.Step(p,new InputFrame(),Floor,Ground);
            p=NativePrediction.Step(p,new InputFrame{Attack=true},Floor,Ground);
            Check(p.Swing!=null&&!p.Swing.Air&&p.Swing.Index==(spec=="daggers"?air+1:1),"next swing "+spec+" index "+p.Swing?.Index);
        }
    }
    // Lava swimming is not airtime: a swimmer's press is an ordinary swing, never a plunge into the channel.
    static void Swimming(ConversionContent data)
    {
        var swim=Air(data,"sword",new V3(0,0,0),-.95f);swim.Swimming=true;swim=NativePrediction.Step(swim,new InputFrame{Attack=true},null);
        Check(swim.Swing!=null&&!swim.Swing.Air,"swimming swing stays ordinary");
        Check(NativeAirAttackSimulation.Airborne(false,false)&&!NativeAirAttackSimulation.Airborne(false,true)&&!NativeAirAttackSimulation.Airborne(true,false),"airborne excludes ground and lava");
    }
    // The Kunai recast enters "recovery" at its strike (PR #136); the layer still draws the arrival stab from the release and fades
    // it over the cast's last .25 s, exactly as when that span was its active phase.
    static void KunaiRecast()
    {
        Actor Cast(string phase,float recoverAt)=>new Actor{Id=1,SourceSpec="daggers",VisualSet=1,Hp=100,CastRate=1,SpecialKind="kunai-recast",SpecialAt=2,SpecialRelease=2.2f,SpecialUntil=2.65f,
            MeleeKit=new NativeMeleeKit{Kind="kunai-mark",Phase=phase,At=2,Release=2.2f,Until=2.65f,RecoverUntil=2.65f,RecoverAt=recoverAt}};
        var active=Cast("active",-1);var recovery=Cast("recovery",2.2f);var fields=typeof(KitPose).GetFields(BindingFlags.Public|BindingFlags.Instance);int compared=0;
        for(float t=2.2f;t<2.65f;t+=1/60f)
        {
            var a=new KitPose();var b=new KitPose();Check(MeleeKitMotion.Sample(active,t,a)&&MeleeKitMotion.Sample(recovery,t,b),"both phases drawn at "+t);
            foreach(var f in fields)
            {
                object x=f.GetValue(a),y=f.GetValue(b);
                if(x is float fx)Check(Near(fx,(float)y,1e-5f),"recast recovery "+f.Name+" at "+t);
                else if(x is System.Numerics.Vector3 vx)Check((vx-(System.Numerics.Vector3)y).Length()<1e-5f,"recast recovery "+f.Name+" at "+t);
            }
            compared++;
        }
        Check(compared>=26,"recast span sampled");
    }
    // Pilot Hit events carry the actual damage direction (ArenaEvent.Normal, PR #136): the stagger pushes along it instead of
    // guessing from the attacker; an undirected hit (a tick, a field) keeps the attacker guess.
    static void HitDirection()
    {
        var victim=new Actor{Id=1,SourceSpec="sword",Hp=80,Position=new V3(0,0,0)};var attacker=new Actor{Id=2,SourceSpec="sword",Position=new V3(0,0,-2)};
        var ledger=new NativeHitLedger();var hits=new StaggerHit[NativeHitLedger.StaggerSlots];
        ledger.Observe(new Snapshot{Actors=new[]{victim,attacker},Events=new[]{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=2,Target=1,Time=1,Amount=30,Detail="direct",Normal=new V3(.6f,.3f,.8f)}}});
        int n=ledger.Staggers(1,hits);Check(n==1&&hits[0].HasDir&&Near(hits[0].Dir.x,.6f)&&Near(hits[0].Dir.z,.8f)&&hits[0].Dir.y==0,"directed hit pushes along its replicated direction");
        ledger.Observe(new Snapshot{Actors=new[]{victim,attacker},Events=new[]{new ArenaEvent{Id=2,Kind=EventKind.Hit,Source=2,Target=1,Time=2,Amount=30,Detail="direct"}}});
        n=ledger.Staggers(1,hits);Check(n==2&&hits[1].HasDir&&Near(hits[1].Dir.z,1)&&Near(hits[1].Dir.x,0),"undirected hit falls back to the attacker position");
        Check(!NativeStaggerRules.Along(new V3(0,1,0),out _)&&!NativeStaggerRules.Along(new V3(float.NaN,0,1),out _),"vertical or invalid directions fall back");
    }
}
