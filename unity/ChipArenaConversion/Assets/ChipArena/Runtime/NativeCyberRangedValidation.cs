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
            void Cast(byte slot){a.Input=new InputFrame{SirenAbility=slot,Shoot=slot==0,Yaw=0};StepCyberInput(a);Check(a.Cyber.Kind==CyberRangedSpec.Ids[slot],"cast "+slot);}
            void Release(){Clock(a.Cyber.Release+.0001f);StepCyberWorld();Physics.SyncTransforms();}
            try
            {
                Setup(false);a.Seat.access=NativeAccess.Guest;a.Input.SirenAbility=5;StepCyberInput(a);Check(!a.Cyber.Ranged.Ranged,"DEV gate");a.Seat.access=grant;
                foreach(string refusal in new[]{"stun","roll","juggle","dead","cast","swing","dash","link","root","kernel"})
                {
                    Setup(false);a.Input.SirenAbility=5;
                    if(refusal=="stun")a.StunEnd=11;else if(refusal=="roll")a.RollEnd=11;else if(refusal=="juggle")a.Juggle=new NativeJuggle{Until=11};else if(refusal=="dead")a.Hp=0;
                    else if(refusal=="cast")a.CastAt=11;else if(refusal=="swing")a.AttackEnd=11;else if(refusal=="dash")a.Cyber.DashUntil=11;else if(refusal=="link")a.Cyber.LinkUntil=11;
                    else if(refusal=="root")a.Cyber.Until=11;else a.Kernel=new NativeKernelPanic{Id=1,At=10,Until=11};
                    StepCyberInput(a);Check(!a.Cyber.Ranged.Ranged,"switch refusal "+refusal);a.Input=a.Input.HeldOnly();Clock(12);StepCyberInput(a);Check(!a.Cyber.Ranged.Ranged,"refused switch not queued "+refusal);
                }
                Setup(false);a.Input=new InputFrame{SirenAbility=5,Z=1};StepCyberInput(a);Check(a.Cyber.Ranged.Ranged,"switch accepts");Check(!TryBlock(a)&&!TryRoll(a),"switch refuses block/roll");a.Input=new InputFrame{Z=1};StepPilot(a);Check(a.MoveVelocity.z>0,"switch allows movement");Clock(10.5f);a.Input.SirenAbility=5;StepCyberInput(a);Check(a.Cyber.Ranged.Ranged,"switch cooldown");Clock(11);StepCyberInput(a);Check(!a.Cyber.Ranged.Ranged,"switch back");
                Setup();a.Mana=40;Cast(0);Release();var bolt=Bullets.Single();Near(bolt.Damage,9,"bolt damage");Near(bolt.Radius,.12f,"bolt radius");Near(bolt.Velocity.magnitude,64,"bolt speed");Near(bolt.Travel,40,"bolt range");Check(bolt.Spec=="cyberlord"&&bolt.Detail=="bit-bolt","bolt wire tag");
                BulletImpact(bolt,a,new NativeContact{Collider=b.Collider,Point=b.Body.position,Contact=b.Body.position,Normal=Vector3.back});Near(b.Hp,91,"bolt no head multiplier");Near(a.Mana,43,"bolt mana hit");Check(Events.Any(e=>e.Kind==EventKind.Hit&&e.Detail=="bit-bolt"),"bolt hit detail");
                BulletImpact(bolt,a,new NativeContact{Collider=floor.GetComponent<Collider>(),Point=floor.transform.position,Contact=floor.transform.position,Normal=Vector3.up});Check(Events.Any(e=>e.Kind==EventKind.Impact&&e.Detail=="bit-bolt"),"world impact detail");
                Setup();Cast(1);Near(a.Mana,85,"lance mana");Release();Near(b.Hp,78,"lance first");Near(c.Hp,78,"lance second");Check(Events.Count(e=>e.Kind==EventKind.Hit&&e.Detail=="ping-lance")==2,"lance pierce details");
                Setup();cover.SetActive(true);Physics.SyncTransforms();Cast(1);Release();Near(b.Hp,100,"lance stopped by world");Check(a.Cyber.Ranged.LanceTo.z<4,"lance endpoint at world");
                Setup();b.Seat.spec="cyberlord";b.Cyber.WallAt=9;b.Cyber.WallUntil=20;b.Cyber.WallPoint=new V3(0,99.135f,4);Cast(1);Release();Near(b.Hp,100,"lance firewall");Check(Events.Any(e=>e.Kind==EventKind.Absorb),"firewall absorb event");
                Setup();b.Hp=50;Cast(4);Near(a.Mana,40,"root mana");Near(a.KernelReady,30,"shared ultimate clock");Release();Near(b.Hp,0,"root missing-health lethal");Near(c.Hp,100,"root first target only");
                Setup();b.Hp=50;b.BlockEnd=20;Cast(4);Release();Near(b.Hp,20,"root block halves 60");
                Setup();Cast(4);Clock(10.5f);a.StunEnd=12;StepCyberWorld();Near(a.Cyber.Ranged.Release[4],10.5f,"root cut release");Near(a.Cyber.Ranged.Until[4],10.5f,"root cut until");Clock(12);StepCyberWorld();Near(b.Hp,100,"interrupted root never fires");
                Setup();Cast(4);Clock(11.4f);a.Input.SirenAbility=5;StepCyberInput(a);Clock(12);Check(!StartKernelPanic(a),"Root blocks Kernel shared clock");
                Setup(false);a.Input.SirenAbility=4;Check(StartKernelPanic(a),"Kernel starts in melee");Near(a.Cyber.Ranged.Ready[4],a.KernelReady,"Kernel seeds Root clock");
                Setup();a.Input=new InputFrame{SirenAbility=2,AbilityTarget=new V3(0,99.125f,8)};StepCyberInput(a);Release();var ranged=a.Cyber.Ranged;Check(ranged.FieldLand>Time&&ranged.FieldUntil>ranged.FieldLand,"field flight/lifetime");float land=ranged.FieldLand+.35f;for(int tick=0;tick<8;tick++){Clock(land+tick*.5f+.0001f);StepCyberWorld();}Near(b.Hp,44,"field eight ticks");Near(b.SlowFactor,.65f,"field slow");
                Setup();b.Body.position=new Vector3(0,100,2);Physics.SyncTransforms();Cast(3);Release();Check(a.Body.position.z<-.1f&&a.Body.position.z>=-9.001f,"swap backward constrained");Clock(a.Cyber.Ranged.MineAt+.0001f);StepCyberWorld();Near(b.Hp,82,"mine damage");Near(b.Knockback.magnitude,3,"mine shove");StepCyberWorld();Near(b.Hp,82,"mine only once");
                var frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.Valid(Content),"real frame validity");var record=frame.snapshot.Actors.First(x=>x.Id==a.Seat.id).Cyber.Ranged;Near(record.SwapFrom.z,0,"JSON swap origin");Near(record.MineAt,a.Cyber.Ranged.MineAt,"JSON mine clock");guest.ApplyFrame(frame,b.Seat.id);var guestRecord=guest.Snapshot().Actors.First(x=>x.Id==a.Seat.id).Cyber.Ranged;Near(guestRecord.SwapTo.z,record.SwapTo.z,"guest record application");record.Ready[0]=float.NaN;Check(!frame.Valid(Content),"malformed ranged frame rejected");
                Setup(false);var input=new InputFrame();BotCyber(a,ref input,b.Body.position,12,true);Check(input.SirenAbility==5,"bot switches at range");Setup();input=new InputFrame();BotCyber(a,ref input,b.Body.position,15,true);Check(input.SirenAbility==2,"bot field");b.Hp=50;input=new InputFrame();BotCyber(a,ref input,b.Body.position,15,true);Check(input.SirenAbility==4,"bot root");
                a.Hp=0;StepCyberWorld();Check(a.Cyber.Ranged==null,"death resets to melee");
                Debug.Log("PASS Cyber ranged authority: switch refusals/no queue, movement, shots, pierce/world/Firewall, shared ultimate, interrupt, field/mine, real JSON/guest bounds and bots");
            }
            finally{UnityEngine.Object.DestroyImmediate(cover);UnityEngine.Object.DestroyImmediate(floor);}
        }
    }
}
#endif
