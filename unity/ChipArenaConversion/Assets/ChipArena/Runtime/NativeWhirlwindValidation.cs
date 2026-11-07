#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private void ValidateWhirlwind(Action<string> setup,Action<float> advance,Action<NativePilot,float> position,NativePilot a,NativePilot b,GameObject wall)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native Whirlwind: "+why);}
            void At(float time){if(time>Time)advance(time-Time);StepSpecial(a);}
            NativeMeleeAction Start(float target=1002)
            {setup("greatsword");position(b,target);Check(TryMeleeAbility(a,"e"),"cast accepted");return a.Special;}
            var spin=Start();float ready=a.EReady;
            NearNative(a.Stamina,55,"Whirlwind cost");NearNative(ready-Time,11,"Whirlwind cooldown");
            At(spin.Release-.001f);Check(b.Hp==100&&spin.Tick==0,"no windup hit");At(spin.Release+.001f);
            NearNative(b.Hp,91.5f,"first pulse");Check(b.StunEnd>Time&&spin.Phase=="active"&&!a.MeleeKit.SuperArmour,"only ordinary light stagger, no legacy armour");
            StepSpecial(a);NearNative(b.Hp,91.5f,"no repeated first pulse");
            Check(!TryRoll(a)&&!TryBlock(a)&&!TryMelee(a),"active input locks");
            for(int tick=1;tick<7;tick++)
            {
                b.StunEnd=b.StaggerImmune=0;At(spin.Release+tick*NativeWhirlwindRules.Interval+.001f);
                Check(b.StunEnd==0&&spin.Tick==tick+1,"later pulses do not stagger");
            }
            NearNative(b.Hp,40.5f,"seven pulse damage budget");
            Check(Events.Count(e=>e.Kind==EventKind.AbilityHit&&e.Detail=="whirlwind"&&e.Target==b.Seat.id)==7,"one AbilityHit per pulse/target");
            At(spin.Until+.001f);Check(a.Special==spin&&spin.Phase=="recovery"&&spin.Tick==7,"natural end retains recovery");
            Check(!TryRoll(a)&&!TryBlock(a)&&!TryMelee(a),"recovery cannot be cancelled");NearNative(a.MeleeKit.RecoverUntil,spin.Until+.5f,"replicated recovery end");
            At(spin.RecoverUntil+.001f);Check(a.Special==null&&a.EReady==ready,"full cleanup preserves cooldown");

            spin=Start(1020);At(spin.Release+.49f);Check(!TryMeleeRecast(a)&&!MeleeRecastOpen(a),"minimum spin enforced");
            At(spin.Release+.501f);ready=a.EReady;float stamina=a.Stamina;
            Check(MeleeRecastOpen(a)&&TryMeleeRecast(a)&&spin.Phase=="recovery","targetless recast accepted");
            NearNative(spin.Until,Time,"early spin end");NearNative(spin.RecoverUntil,Time+.5f,"full early recovery");
            Check(a.Stamina==stamina&&a.EReady==ready&&!TryMeleeRecast(a),"recast neither charges nor refunds nor repeats");
            var copy=JsonUtility.FromJson<NativeMeleeKit>(JsonUtility.ToJson(a.MeleeKit));
            Check(copy.Valid()&&copy.Kind=="whirlwind"&&copy.Phase=="recovery"&&copy.Tick==2&&copy.Until==Time&&copy.RecoverUntil==spin.RecoverUntil,"actual JSON retains early end and pulse count");
            At(spin.RecoverUntil+.001f);Check(a.Special==null,"early cleanup");

            spin=Start();ready=a.EReady;Check(TryRoll(a)&&a.Special==null&&a.EReady==ready,"only windup roll cancels; cooldown retained");
            spin=Start();b.BlockEnd=spin.Release+.1f;At(spin.Release+.001f);
            Check(b.Hp==100&&a.Special==null&&Events.Any(e=>e.Kind==EventKind.Block&&e.Detail=="clash"&&e.Target==a.Seat.id),"normal timed block punishes and cancels caster");
            spin=Start();b.Roll=new RollValues{iframeStart=0,iframeSeconds=.3};b.RollAt=spin.Release;b.RollEnd=spin.Release+.3f;At(spin.Release+.001f);
            Check(b.Hp==100&&spin.Tick==1&&a.Special==spin,"roll invulnerability avoids a pulse");b.Roll=null;b.RollEnd=0;
            At(spin.Release+.451f);NearNative(b.Hp,91.5f,"next pulse rechecks roll state");Check(b.StunEnd==0,"dodged first pulse does not move stagger to a later tick");
            spin=Start();wall.SetActive(true);Physics.SyncTransforms();At(spin.Release+.001f);Check(b.Hp==100&&spin.Tick==1,"cover blocks a pulse");wall.SetActive(false);Physics.SyncTransforms();At(spin.Release+.451f);NearNative(b.Hp,91.5f,"next pulse rechecks visibility");
            spin=Start(998);At(spin.Release+.001f);NearNative(b.Hp,91.5f,"rear target inside radial sweep");
            spin=Start(1003);At(spin.Release+.001f);Check(b.Hp==100,"target beyond radius");
            spin=Start(1020);At(spin.Release+.001f);Damage(a,b,10,Vector3.back,0,false,melee:true,weaponHit:true);Check(StaggerMelee(a,.12f)&&a.Special==null,"normal incoming stagger interrupts spin");

            spin=Start(1020);At(spin.Release+.001f);a.Input=new InputFrame{X=1,Yaw=90,Sprint=true,Jump=true};
            for(int i=0;i<12;i++)StepPilot(a);
            NearNative(a.MoveVelocity.x,5.44f,"steered spin speed");NearNative(a.Yaw,90,"steered spin yaw");
            Check(!a.Pose.Sprinting&&a.Body.linearVelocity.y<=0&&a.Swing==null,"no sprint/jump/primary during spin");
            At(spin.Until+.001f);StepPilot(a);Check(a.MoveVelocity==Vector3.zero,"recovery stops movement");

            setup("greatsword");var botInput=new InputFrame();for(int i=0;i<100&&!botInput.AbilityE;i++)BotMeleeAbilities(a,ref botInput,b.Body.position,3,true,2);
            Check(botInput.AbilityE&&!botInput.AbilityQ,"bot prefers Whirlwind at 3 m");
            a.QReady=Time+100;botInput=new InputFrame();for(int i=0;i<100;i++)BotMeleeAbilities(a,ref botInput,b.Body.position,3.01f,true,2);
            Check(!botInput.AbilityE,"bot respects 3 m cast range");
            spin=Start();At(spin.Release+.6f);botInput=new InputFrame();BotMeleeAbilities(a,ref botInput,b.Body.position,2,true,2);Check(!botInput.AbilityE,"bot keeps spinning with nearby enemy");
            BotMeleeAbilities(a,ref botInput,b.Body.position,5,true,2);Check(botInput.AbilityE,"bot can end an empty spin");
            Debug.Log("PASS native Whirlwind: costs, seven ticks/59.5 budget, contact/cover/roll/block/stagger, input locks, steering, early/natural recovery, JSON and bots");
        }
    }
}
#endif
