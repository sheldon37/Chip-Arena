#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Real input/authority/PhysicsScene/JsonUtility gate, isolated by the Editor caller.
        public void ValidateCyberRanged(NativeArena guest)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Cyber ranged authority: "+why);}
            void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.002f,why+": "+a+" != "+b);
            var a=Pilots[0];var b=Pilots[1];var c=Pilots[2];
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            var floor=new GameObject("Cyber ranged test floor");floor.layer=PropLayer;floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,99.025f,0);floor.AddComponent<BoxCollider>().size=new Vector3(60,.2f,60);
            var cover=new GameObject("Cyber ranged test cover");cover.layer=PropLayer;cover.transform.SetParent(root.transform);cover.transform.position=new Vector3(0,100,4);cover.AddComponent<BoxCollider>().size=new Vector3(5,5,.3f);cover.SetActive(false);
            void Clock(float time){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=time});}
            void Setup(bool ranged=true)
            {
                Clock(10);Events.Clear();Bullets.Clear();cover.SetActive(false);
                int index=0;foreach(var p in Pilots)
                {
                    CancelSiren(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);CancelFighter(p);p.Kernel=null;p.KernelReady=0;p.KernelPinned=false;p.HeldBy=p.ScareBy=null;p.Down=null;p.Juggle=null;p.Roll=null;
                    p.Seat.spec=p==a?"cyberlord":"pistol";p.Seat.access=grant;p.Seat.bot=false;p.Seat.connected=p.Seat.alive=true;p.Seat.chips.Clear();p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);
                    p.Stats["damage"]=p.Stats["damageTaken"]=p.Stats["abilityPower"]=p.Stats["resourceCost"]=p.Stats["abilityCost"]=p.Stats["abilityCooldown"]=p.Stats["attackRate"]=1;p.Stats["hp"]=p.Stats["maxMana"]=100;p.Stats["manaSurge"]=p.Stats["arcaneSurge"]=p.Stats["critical"]=0;
                    p.Cyber=new NativeCyberKit{Ranged=new NativeCyberRangedState{Ranged=p==a&&ranged}};p.CyberMineDone=true;p.CyberFieldTick=0;p.CyberTrailTick=p.CyberWallTick=p.CyberLinkTick=0;
                    p.Hp=p.Mana=p.Stamina=100;p.Dots.Clear();p.Death=null;p.Wounds.Clear();p.Ready=p.AttackEnd=p.RollEnd=p.RollReady=p.BlockEnd=p.BlockReady=p.StunEnd=p.ShieldUntil=p.FrozenUntil=p.ScareUntil=p.CastAt=p.ReloadEnd=p.LaunchUntil=0;p.AbilityCast=null;p.Special=null;
                    p.Body.isKinematic=false;p.Collider.enabled=true;p.Body.position=new Vector3(0,100,index++*8);p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Input=new InputFrame();p.Grounded=true;
                }
                Physics.SyncTransforms();
            }
            void Cast(byte slot){a.Input=new InputFrame{SirenAbility=slot,Shoot=slot==0,Yaw=0,AbilityTarget=new V3(0,99.125f,8)};StepCyberInput(a);Check(a.Cyber.Version==2&&a.Cyber.Kind==CyberKit2Spec.Ids[7+slot],"v2 cast "+slot);Check(a.Cyber.Valid(),"record valid from cast start");}
            void Release(){Clock(a.Cyber.Release+.0001f);StepCyberWorld();Physics.SyncTransforms();}
            try
            {
                Setup();a.MoveVelocity=new Vector3(2,0,1);Cast(0);Check(a.MoveVelocity.x==2,"throw retains movement");Release();Check(Bullets.Count==1&&Bullets[0].Detail=="plasma-byte"&&Bullets[0].Draw==0,"first right-hand plasma");Near(Bullets[0].Damage,10,"plasma direct");Near(Bullets[0].Velocity.magnitude,38,"plasma speed");
                Clock(a.Cyber.Until+.001f);Cast(0);Release();Check(Bullets.Last().Draw==1,"second left hand");Clock(a.Cyber.Until+.001f);Cast(0);Release();Check(Bullets.Last().Detail=="plasma-overflow"&&Bullets.Last().Draw==2,"third both hands overflow");Near(Bullets.Last().Damage,18,"overflow damage");
                Setup();Cast(1);Release();Near(b.Hp,80,"chain first hit");Clock(a.Cyber.Release+CyberKit2Spec.HopDelay+.001f);StepCyberWorld();Near(c.Hp,83,"chain second hit falloff");Check(a.Cyber.Kit2R.ChainCount==2&&a.Cyber.Kit2R.ChainIds[0]==b.Seat.id,"chain record order");StepCyberWorld();Near(c.Hp,83,"chain hop exactly once");
                Setup();cover.SetActive(true);Physics.SyncTransforms();Cast(1);Release();Near(b.Hp,100,"chain cannot target through wall");
                Setup();Cast(4);Clock(10.3f);a.StunEnd=12;StepCyberWorld();Near(a.Cyber.Kit2R.CutAt[4],10.3f,"orbital cut timestamp");Clock(12);StepCyberWorld();Near(b.Hp,100,"cut orbital has no ticks");
                Setup();Cast(4);float ultimate=a.KernelReady;Clock(a.Cyber.Until+.01f);a.Input=new InputFrame{SirenAbility=5};StepCyberInput(a);Clock(a.Cyber.Until+.01f);Check(!StartKernelPanic(a)&&a.KernelReady==ultimate,"orbital shares Kernel clock");
                Setup(false);a.Input=new InputFrame{SirenAbility=4};Check(StartKernelPanic(a),"unchanged Kernel starts");Check(a.Cyber.Version==2&&a.Cyber.Kit2R.Ready[4]==a.KernelReady,"Kernel publishes v2 ultimate clock");
                Setup();a.Input=new InputFrame{AbilityE=true};StepCyberInput(a);Release();Check(NativeCyberKit2Rules.Overclock(a.Cyber,Time),"Overclock active on release");Clock(a.Cyber.Until+.001f);Cast(0);Release();Check(Bullets.Count==3,"Overclock splits plasma into three");Near(Bullets[1].Damage,4,"side projectile share");
                Setup(false);for(int n=0;n<5;n++){a.Input=new InputFrame{Shoot=true,Yaw=0};StepCyberInput(a);Check(a.Cyber.Combo==n&&a.Cyber.Kind==CyberKit2Spec.ComboKinds[n],"five-step combo "+n);Release();Clock(a.Cyber.Until+.001f);}
                Setup();Cast(2);Release();Check(a.Cyber.Kit2R.OrbLand>Time&&Math.Abs(a.Cyber.Kit2R.Implode-a.Cyber.Kit2R.OrbLand-2.5f)<.001f,"orb flight and lifetime");
                var frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.Valid(Content),"real v2 frame validity");var record=frame.snapshot.Actors.First(x=>x.Id==a.Seat.id).Cyber;Check(record.Version==2,"JSON version");Near(record.Kit2R.OrbLand,a.Cyber.Kit2R.OrbLand,"JSON orb clock");guest.ApplyFrame(frame,b.Seat.id);Check(guest.Snapshot().Actors.First(x=>x.Id==a.Seat.id).Cyber.Version==2,"guest v2 application");record.Kit2R.Ready[0]=float.NaN;Check(!frame.Valid(Content),"malformed v2 frame rejected");
                Setup(false);var input=new InputFrame();BotCyber(a,ref input,b.Body.position,12,true);Check(input.SirenAbility==5,"bot switches at range");Setup();input=new InputFrame();BotCyber(a,ref input,b.Body.position,15,true);Check(input.AbilityE,"bot Overclock");
                a.Hp=0;StepCyberWorld();Check(a.Cyber.Version==0&&a.Cyber.Ranged==null,"death clears v2 state");
                Debug.Log("PASS Cyber v2 authority: cast records, moving plasma, hands/Overflow, chain/LOS/falloff, interrupt, shared ultimate, Overclock, five-hit combo, orb, JSON/guest bounds, bots");
            }
            finally{UnityEngine.Object.DestroyImmediate(cover);UnityEngine.Object.DestroyImmediate(floor);}
        }
    }
}
#endif
