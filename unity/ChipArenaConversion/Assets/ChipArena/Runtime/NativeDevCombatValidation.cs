#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Actual authority methods and JsonUtility, run by NativeBuildChecks in an isolated Unity scene.
        public void ValidateDevCombatParity()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native DEV combat: "+why);}
            void Near(float a,float b,string why){Check(Math.Abs(a-b)<.001f,why+": "+a+" != "+b);}
            var a=Pilots[0];var b=Pilots[1];int local=LocalId;var saved=Match.Seats.Select(s=>(seat:s,spec:s.spec,access:s.access)).ToArray();
            // Editor-only fixture object. Never changes the shared Guest instance or the signed production verifier.
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            // The isolated combat positions are outside the authored map. Grounded abilities
            // must use real support, since StepPilot refreshes ground after a teleport.
            var floor=new GameObject("DEV combat validation floor");floor.layer=PropLayer;floor.transform.SetParent(root.transform);
            floor.transform.position=new Vector3(1000,10-NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Skin-.1f,1003);floor.AddComponent<BoxCollider>().size=new Vector3(12,.2f,16);
            void Setup(string spec="emperor")
            {
                Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});LocalId=local;remoteInputs.Clear();Bullets.Clear();Events.Clear();AbilityEffects.Clear();StormTrails.Clear();
                foreach(var p in new[]{a,b})
                {
                    CancelSiren(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);p.Fighter=null;p.Siren=null;p.Seat.chips.Clear();p.Seat.spec=p==a?spec:"pistol";p.Seat.access=grant;p.Seat.bot=false;p.Seat.alive=p.Seat.connected=true;
                    p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);p.Hp=p.Mana=p.Stamina=100;p.Grounded=p.GroundAttached=false;p.Ground=default;p.GroundPosition=default;p.Body.isKinematic=false;p.Collider.enabled=true;
                    p.Body.position=new Vector3(1000,10,p==a?1000:1006);p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Down=null;p.Juggle=null;p.Roll=null;p.Dots.Clear();p.Death=null;p.Wounds.Clear();
                    p.Ready=p.QReady=p.EReady=p.ReloadEnd=p.BlockEnd=p.RollEnd=p.StunEnd=p.StaggerImmune=p.FrozenUntil=p.ScareUntil=p.ShieldUntil=p.CastAt=p.AttackEnd=p.LaunchUntil=0;p.Ammo=Magazine(p);p.Input=new InputFrame();p.RegenAt=Time+20;
                }
                Physics.SyncTransforms();
                foreach(var p in new[]{a,b}){ProbePilotGround(p);Check(p.Grounded,"combat fixture floor, seat="+p.Seat.id);}
            }
            try
            {
                foreach(bool guest in new[]{false,true})for(int offset=0;offset<11;offset++)
                {
                    Setup();if(guest)LocalId=b.Seat.id;
                    for(int tick=30;tick<=30+offset;tick++)
                    {
                        var input=new InputFrame{Sequence=tick,AbilityEHeld=true,AbilityQ=tick==30+offset};
                        if(guest){AcceptInput(a.Seat.id,input);a.Input=RemoteInput(a.Seat.id);}else a.Input=input;
                        StepPilot(a);if(tick<30+offset)Match.Tick(1d/60);
                    }
                    Check(a.Fighter.Kind=="electric","one-tick Q lost, guest="+guest+", tick="+(30+offset));
                    if(offset>0){Near(a.Knockback.z,9,"cancel retains wave push");Near(a.Body.linearVelocity.z,15,"wave plus grounded hook step");}
                    int attack=a.Fighter.Id;float end=a.Fighter.Until;
                    a.Input=guest?RemoteInput(a.Seat.id):a.Input.HeldOnly();Check(!a.Input.AbilityQ&&a.Input.AbilityEHeld,"tap consumed, held E retained");
                    while(Time<end-.02f){Match.Tick(1d/60);StepFighter(a);Check(a.Fighter.Id==attack&&a.Fighter.Kind=="electric","E chain interrupts hook recovery");}
                    Match.Tick(.05);StepFighter(a);Check(a.Fighter.Kind=="wave","held E resumes after hook, grounded="+a.Grounded+", kind="+a.Fighter.Kind+", ready="+a.Ready+", time="+Time);
                }
                Setup();a.Input.AbilityEHeld=true;StepPilot(a);Match.Tick(1d/60);a.Input.Shoot=true;StepPilot(a);Check(a.Fighter.Kind=="jab","jab cannot cancel wave");Near(a.Body.linearVelocity.z,9,"jab retains push");
                Setup();a.Grounded=false;a.Input.AbilityEHeld=true;StepFighter(a);Check(a.Fighter.Kind==null&&a.Knockback==Vector3.zero,"airborne wave started");
                Setup();a.Input.AbilityEHeld=true;StepPilot(a);float waveEnd=a.Fighter.WaveUntil;Match.Tick(1d/60);a.Input.AbilityQ=true;StepPilot(a);a.Input=a.Input.HeldOnly();
                while(Time<waveEnd){Match.Tick(1d/60);StepPilot(a);}Near(a.Knockback.z,9*Mathf.Exp(-3.1f/60),"push drag starts at original wave deadline");
                a.Grounded=false;Near(FighterMotion(a).magnitude,0,"airborne hook adds ground step");
                Setup();a.Input.AbilityEHeld=true;StepFighter(a);a.Ready=Time+1;Check(!StartFighter(a,"electric"),"wave cancel bypassed cooldown");a.Ready=0;a.StunEnd=Time+1;Check(!StartFighter(a,"electric"),"stunned cancel");
                Setup();a.Input.AbilityEHeld=true;StepFighter(a);a.Seat.bot=true;var botInput=new InputFrame();
                for(int n=0;n<100&&!botInput.AbilityQ;n++)BotAbilities(a,ref botInput,b.Body.position,Vector3.zero,2.6f,true,2);
                Check(botInput.AbilityQ,"bot Q decision suppressed during wave");a.Input=botInput;StepFighter(a);Check(a.Fighter.Kind=="electric","bot Q did not cancel wave");
                Setup();b.Seat.spec="greatsword";b.Weapon=Content.Weapon("greatsword");b.Stats=NativeContentRules.Stats(Content,"greatsword",b.Seat.chips);b.Seat.bot=true;
                Check(TryMelee(b),"greatsword knockup fixture");b.StaggerImmune=Time+5;b.MeleeBufferedUntil=Time+1;
                ConfirmFighter(a,b,8,true,.3f);
                Check(b.Juggle!=null&&b.StunEnd==0&&b.Swing==null&&!b.HitPending&&b.MeleeBufferedUntil<0,"electric launch must interrupt even with stagger immunity");
                Setup("ansatsuken");Check(StartFighter(a,"combo"),"arcade fixture");Check(!StartFighter(a,"shoryuken"),"arcade free cancel");a.Seat.chips["arcade-cancel"]=1;a.Fighter.LastHit=Time;Check(StartFighter(a,"shoryuken"),"arcade confirmed cancel regressed");
                Setup("siren");var state=Siren(a);Check(StartSiren(a,"swipe")&&state.Move.Side==1&&state.Side==-1,"first swipe side");CancelSiren(a);a.Ready=0;
                Check(StartSiren(a,"slam")&&state.Move.Side==1&&state.Side==-1,"slam consumed alternation");CancelSiren(a);a.Ready=0;state.Ranged=true;
                Check(StartSiren(a,"hellfire")&&state.Move.Side==-1&&state.Side==1,"hellfire side after special");CancelSiren(a);a.Ready=0;
                a.Input.AbilityQ=true;StepSirenPilot(a);state.ModeAt=Time-.075f;SirenShot(a,"seeker",b);var frame=NativeFrameCodec.Clone(NetworkFrame());
                Near(frame.snapshot.Actors.First(p=>p.Id==a.Seat.id).SourceModeAt,state.ModeAt,"mode timestamp serialization");Check(frame.snapshot.Shots.Single().SirenTarget==b.Seat.id&&frame.Valid(Content),"seeker target serialization/validity");
                ApplyEffects(frame);Check(Bullets.Single().SirenTarget==b.Seat.id,"guest/replay seeker target restore");frame.snapshot.Actors[0].SourceModeAt=float.NaN;Check(!frame.Valid(Content),"NaN mode stamp accepted");
                Setup("leet");a.Seat.chips["l44t-time"]=1;a.Input.AbilityE=true;a.Ready=Time+.2f;float mana=a.Mana;StepLeetPilot(a);Check(a.Mana==mana&&a.EReady==0&&b.FrozenUntil==0,"Leet E bypasses cooldown");
                a.Ready=0;StepLeetPilot(a);Check(a.Mana==mana-30&&b.FrozenUntil==Time+4&&Events.Count(e=>e.Kind==EventKind.Leet&&e.Detail=="leet-freeze"&&e.Source==a.Seat.id&&e.Radius==28)==1,"Admin Pause event/effect");StepLeetPilot(a);Check(a.Mana==mana-30,"Admin Pause retrigger");
                Setup("pistol");Summon(b,1,"toy",b.Body.position,20);var toy=Minions.Last();Events.Clear();var point=toy.Body.position+Vector3.up*.2f;
                HitMinion(toy,a,1,Vector3.forward,0,point,"direct");Check(Events.Count(e=>e.Kind==EventKind.Impact&&e.Target==toy.Id&&e.Source==a.Seat.id&&(e.Position-Vec(point)).Length<.0001f)==1,"summon contact impact");Events.Clear();HitMinion(toy,a,1,Vector3.zero,0,kind:"dot");Check(!Events.Any(e=>e.Kind==EventKind.Impact),"DoT emitted contact impact");
                a.Stats["staticEvery"]=1;a.Stats["staticDamage"]=1;MinionWeaponHit(toy,a,1,0,0);Check(Events.Any(e=>e.Kind==EventKind.Explosion&&e.Detail=="static"),"summon static tag");RemoveMinion(toy,false);
                b.Seat.chips["corpse-party"]=1;Summon(b,1,"zombie",b.Body.position,20);Events.Clear();RemoveMinion(Minions.Last());Check(Events.Any(e=>e.Kind==EventKind.Explosion&&e.Detail=="corpse-party"),"Corpse Party tag");
                Setup("pistol");var ball=new NativeBullet{Spec="pistol",Bounces=3,FixedSpeed=30,RallyCap=48,RallyBonus=2,Damage=10,Radius=.1f,RallyMark=a.Body.position-Vector3.forward*7,Velocity=Vector3.forward*30};var bounce=a.Body.position;
                Check(!PongBounce(ball,a,bounce,Vector3.forward*30),"rally removed ball");var rally=Events.Last(e=>e.Kind==EventKind.Pong);Check(rally.Target==b.Seat.id&&rally.Detail=="rally"&&(rally.From-Vec(bounce)).Length<.0001f&&(rally.Position-Vec(ball.Position)).Length<.0001f,"Pong target/from payload");
                Events.Clear();ball.Velocity=Vector3.down;PongBounce(ball,a,bounce,Vector3.up);Check(Events.Single(e=>e.Kind==EventKind.Pong).Target==0,"floor bounce got rally target");
                Debug.Log("PASS native DEV combat: Emperor Q 11/11 local and 11/11 remote ticks, jab, held-E recovery, momentum/drag, grounded restriction, bot cancel, arcade guards; Siren mode/target JsonUtility and sides; Leet cooldown/freeze; summon impact/static/corpse tags; Pong target/from");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(floor);
                foreach(var x in saved){x.seat.spec=x.spec;x.seat.access=x.access;x.seat.chips.Clear();}LocalId=local;remoteInputs.Clear();Reset();Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});
            }
        }
    }
}
#endif
