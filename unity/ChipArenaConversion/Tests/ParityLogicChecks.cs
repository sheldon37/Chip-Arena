using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using ChipArena.UnityConversion;

internal static class ParityLogicChecks
{
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void NetworkLimits()
    {
        var events=new NativeEventBuffer();
        for(int tick=0;tick<60;tick++)for(int i=0;i<600;i++)events.Add(new ArenaEvent{Id=tick*600+i,Kind=EventKind.Hit,Time=tick/60f});
        events.Add(new ArenaEvent{Id=99999,Kind=EventKind.Death,Time=1});
        Check(events.Count<=512&&events.Any(e=>e.Kind==EventKind.Death),"36,000 horde hit notifications remain bounded without losing death");
        string invite=NativeLanDiscovery.Invite("192.168.1.4",30001,"ABCDEFGH");Check(NativeLanDiscovery.ParseInvite(invite,out var address,out int port,out var code)&&address=="192.168.1.4"&&port==30001&&code=="ABCDEFGH"&&!NativeLanDiscovery.ParseInvite("https://example.com",out _,out _,out _),"LAN invites round trip and reject unrelated URLs");
        var order=new NativeFrameOrder();Check(order.Control(4)&&!order.Frame(3,1,9),"lobby prevents stale match resurrection");
        Check(order.Frame(5,0,1)&&!order.Frame(5,0,1)&&!order.Frame(5,0,.5)&&order.Frame(5,1,0)&&!order.Frame(5,0,2),"epochs/generations/timestamps advance monotonically");
        var budget=new NativeMessageBudget();Check(Enumerable.Range(0,100).Count(i=>budget.Allow(1))==8&&budget.Allow(2),"one guest cannot amplify a control burst");
        Check(NativeRoomCodes.Alphabet.Distinct().Count()==32&&Enumerable.Range(0,100).All(_=>NativeRoomCodes.Create().All(c=>NativeRoomCodes.Alphabet.Contains(c))),"8 uniformly sampled base32 symbols preserve 40 bits of room entropy");
        Console.WriteLine("PASS presentation event budget, epoch/generation fencing, lobby rate budget and 40-bit room codes");
    }
    private static void Signatures()
    {
        // Public verification vector, RFC 6979 appendix A.2.5 (SHA-256, message sample).
        // https://www.rfc-editor.org/rfc/rfc6979.txt — no private signing material is stored.
        byte[] Hex(string s)=>Enumerable.Range(0,s.Length/2).Select(i=>Convert.ToByte(s.Substring(i*2,2),16)).ToArray();
        var x=Hex("60FED4BA255A9D31C961EB74C6356D68C049B8923B61FA6CE669622E60F29FB6");
        var y=Hex("7903FE1008B8BC99A41AE9E95628BC64F2F1B20C2D7E9F5177A3C294D4462299");
        var signature=Hex("EFD48B2AACB6A8FD1140DD9CD45E81D69D2C877B56AAF991C34D0EA84EAF3716F7CB1C942D657C41D436C7A1B6E29F65F3E900DBB9AFF4064DC4AB2F843ACDA8");
        Check(NativeP256.Verify(Encoding.ASCII.GetBytes("sample"),signature,x,y),"published P-256 signature verifies without a native ECDsa provider");
        Check(!NativeP256.Verify(Encoding.ASCII.GetBytes("tampered"),signature,x,y)&&!NativeP256.Verify(Array.Empty<byte>(),new byte[64],x,y),"tampering and zero scalars rejected");
        Check(!NativeP256.Verify(Array.Empty<byte>(),signature,new byte[32],new byte[32])&&!NativeP256.Verify(Array.Empty<byte>(),signature,Enumerable.Repeat((byte)255,32).ToArray(),y),"invalid/out-of-range curve points rejected");
        for(int i=0;i<24;i++)using(var key=ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            var data=Encoding.UTF8.GetBytes("independent provider "+i);var sig=key.SignData(data,HashAlgorithmName.SHA256);var pub=key.ExportParameters(false);
            Check(NativeP256.Verify(data,sig,pub.Q.X,pub.Q.Y),"independent .NET P1363 signature accepted");sig[i]^=1;Check(!NativeP256.Verify(data,sig,pub.Q.X,pub.Q.Y),"altered independent signature rejected");
        }
        string token=Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"))+"."+Convert.ToBase64String(new byte[64]);
        Check(ReferenceEquals(NativeAccess.Guest,NativeAccess.Verify(token,_=>throw new InvalidOperationException("decoder failure"),0)),"unexpected decoder failures fail closed");
        Console.WriteLine("PASS managed P-256: RFC 6979, 24 independent signatures, tampering, invalid points/scalars and catch-all token boundary");
    }
    private static void CombatAndLobby(ConversionContent content)
    {
        var brain=new NativeBotBrain();var first=brain.Observe(1,"easy",new V3(1,0,0),()=>.5);var delayed=brain.Observe(1.1,"easy",new V3(9,0,0),()=>.5);Check(first.x==delayed.x&&!brain.Reacted,"bot aim preserves reaction delay");brain.Observe(1.33,"easy",new V3(9,0,0),()=>.5);Check(brain.Aim.x==9&&brain.Reacted,"bot aim updates after think interval");
        Check(NativeBotRules.Reaction(2)<NativeBotRules.Reaction(0)&&NativeBotRules.Error(2)<NativeBotRules.Error(0)&&NativeBotRules.Desired(content.Weapon("rifle"))==32&&!NativeBotRules.Ads("pistol",12)&&NativeBotRules.Ads("rifle",12),"difficulty and weapon ranges match source AI");
        Check(NativeBotRules.BossDifficulty(1)=="normal"&&NativeBotRules.BossDifficulty(4)=="hard"&&NativeBotRules.Ability("meteor-strike").lead==.6f,"party boss difficulty and kit-specific lead");
        Check(NativeCombatRules.Headshot(new V3(),0,new V3(0,.64f,-3),new V3(0,0,1),0,false),"head trajectory receives bonus");
        foreach(var hit in new[]{new V3(.3f,.45f,-3),new V3(.35f,.5f,-3),new V3(0,.4f,-3)})Check(!NativeCombatRules.Headshot(new V3(),0,hit,new V3(0,0,1),.03f,false),"shoulder is not the head");
        Check(!NativeCombatRules.Headshot(new V3(),0,new V3(0,.64f,-3),new V3(0,0,1),0,true),"downed/juggled pilots do not receive headshots");
        Check(Math.Abs(NativeMeleeRules.HitStun(content.Weapon("daggers"),1)-.12f)<1e-5f
            &&Math.Abs(NativeMeleeRules.HitStun(content.Weapon("sword"),1)-.18f)<1e-5f
            &&Math.Abs(NativeMeleeRules.HitStun(content.Weapon("shield"),1)-.20f)<1e-5f
            &&Math.Abs(NativeMeleeRules.HitStun(content.Weapon("greatsword"),1)-.28f)<1e-5f,
            "normal melee hits have small weight-specific hit-stun windows");
        Check(Math.Abs(NativeMeleeRules.FighterHitStun(false,false,1)-.10f)<1e-5f
            &&Math.Abs(NativeMeleeRules.FighterHitStun(true,false,1)-.12f)<1e-5f
            &&Math.Abs(NativeMeleeRules.FighterHitStun(true,true,1)-.22f)<1e-5f,
            "light and heavy fighter strikes have separate stun windows");
        Check(!NativeMeleeRules.StaggerReady(1.25f,1.2f,1.7f)
            &&NativeMeleeRules.StaggerReady(1.7f,1.2f,1.7f)
            &&Math.Abs(NativeMeleeRules.StaggerImmuneThrough(1.2f)-1.7f)<1e-5f,
            "a successful stun protects against follow-up stuns until half a second after it ends");
        var a=new NativeSeat{id=1,team=0};var b=new NativeSeat{id=2,team=0};Check(!NativeCombatRules.Enemy(a,b,true)&&NativeCombatRules.CanHit(a,b,true,true)&&!NativeCombatRules.CanHit(a,b,true,false),"friendly fire changes contacts, never enemy targeting");
        Check(NativeCombatRules.Overtime(100,100,false,false)==0&&Math.Abs(NativeCombatRules.Overtime(101,100,false,false)*60-18)<.001&&NativeCombatRules.Overtime(101,100,true,false)==0&&NativeCombatRules.Overtime(101,100,false,true)==0,"overtime timing, damage and mode exclusions");
        Check(NativeLobbyRules.DefaultTeam(3,false)==0&&NativeLobbyRules.DefaultTeam(2,false)==1&&NativeLobbyRules.DefaultTeam(2,true)==0,"guest default alternates two teams, coop stays allied");
        var options=new NativeRoomOptions{rounds=11,bots=20,hostTeam=9,botOptions=new[]{new NativeBotOption{weapon="sword",team=7,difficulty="hard"}}};options.Normalize(content);
        var bot=NativeLobbyRules.Bot(content,options,0,"pistol",17);Check(options.rounds==10&&options.bots==7&&options.hostTeam==7&&bot.spec=="sword"&&bot.team==7&&bot.difficulty=="hard","room limits preserve per-bot kit/team/difficulty");
        var mixed=Enumerable.Range(0,80).Select(i=>NativeLobbyRules.Bot(content,new NativeRoomOptions(),0,"pistol",(uint)(i*7919+17))).ToArray();Check(mixed.Any(p=>content.Weapon(p.spec).@class=="melee")&&mixed.Any(p=>content.Weapon(p.spec).@class=="ranged"),"random bot pool includes melee and ranged kits");
        Check(Enumerable.Range(0,8).Select(_=>NativeMatch.NewSeed()).Distinct().Count()>1,"new match seeds vary");
        Console.WriteLine("PASS native head volume, friendly-fire targeting, overtime, lobby teams/options, mixed bots and fresh match seeds");
    }
    public static void Run(ConversionContent content)
    {
        NetworkLimits();Signatures();CombatAndLobby(content);
        var initial=new NativePredictionState{Position=new V3(0,1,0),Speed=6.8f,Gravity=23,JumpSpeed=8.5f,Grounded=true,CanMove=true,CanSprint=true};
        var prediction=new NativePrediction();prediction.Reset(initial);
        var authority=initial;
        for(int i=1;i<=12;i++)
        {
            var input=new InputFrame{Sequence=i,X=1};prediction.Push(input,null);
            if(i<=6)authority=NativePrediction.Step(authority,input,null);
        }
        var expected=prediction.State.Position;prediction.Reconcile(authority,6,null);
        Check(prediction.Pending==6&&(prediction.State.Position-expected).Length<.0001,"prediction replays only unacknowledged input");
        prediction.Reconcile(initial,2,null);Check((prediction.State.Position-expected).Length<.0001,"old acknowledgements cannot rewind prediction");
        prediction.Push(new InputFrame{Sequence=12,X=-1},null);Check(prediction.Pending==6,"duplicate inputs cannot move twice");
        for(int i=13;i<300;i++)prediction.Push(new InputFrame{Sequence=i,X=1},(from,to)=>new V3(Math.Min(to.x,1),to.y,to.z));
        Check(prediction.Pending==120&&prediction.State.Position.x<=1,"prediction history and collision-constrained movement stay bounded");
        prediction.Reset(initial);prediction.Push(new InputFrame{Sequence=1,Jump=true},null);Check(prediction.State.Position.y>1,"local jump responds before a host frame");
        prediction.Reset(initial);Check(prediction.Pending==0,"generation reset clears old inputs");
        foreach(int hz in new[]{30,60,144})
        {
            var clock=new NativeSnapshotClock();clock.Reset(10);double previous=clock.Time;
            for(int i=0;i<hz;i++){clock.Observe(10+(i/(hz/10))*0.1);double time=clock.Advance(1f/hz);Check(time>=previous&&time<=clock.Latest,"snapshot clock bounded and monotonic");previous=time;}
        }
        Console.WriteLine("PASS replica prediction: input acknowledgement/replay, duplicate/stale rejection, collision constraint, jump, bounded history and 30/60/144 Hz interpolation clocks");
    }
}
