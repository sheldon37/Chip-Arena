#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Runs only in the build gate's isolated scene; deliberately does not register an unfinished class.
        public void ValidateKernelPanic()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Kernel Panic authority: "+why);}
            var a=Pilots[0];var b=Pilots[1];var saved=Match.Seats.Select(s=>(seat:s,spec:s.spec,access:s.access)).ToArray();
            var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});
            var floor=new GameObject("Kernel Panic validation floor");floor.layer=PropLayer;floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,99.025f,0);floor.AddComponent<BoxCollider>().size=new Vector3(64,.2f,54);
            void Clock(float t){Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=t});StepKernelWorld();}
            void Setup()
            {
                foreach(var p in new[]{a,b}){if(p.Kernel?.Active==true)EndKernel(a,3);p.Kernel=null;p.KernelReady=0;p.KernelPinned=false;}
                Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});Events.Clear();
                foreach(var p in new[]{a,b})
                {
                    p.Seat.spec=p==a?"cyberlord":"pistol";p.Seat.access=grant;p.Seat.connected=p.Seat.alive=true;p.Seat.chips.Clear();p.Weapon=Content.Weapon(p==a?"emperor":"pistol");p.Stats=NativeContentRules.Stats(Content,p.Weapon.id,p.Seat.chips);p.Stats["damage"]=p.Stats["damageTaken"]=1;p.Stats["critical"]=0;
                    CancelSiren(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);CancelFighter(p);p.Down=null;p.Juggle=null;p.HeldBy=null;p.ScareBy=null;p.Hp=p.Mana=p.Stamina=100;p.Dots.Clear();p.Death=null;p.Wounds.Clear();p.Roll=null;p.Knockback=p.MoveVelocity=Vector3.zero;
                    p.Body.isKinematic=false;p.Collider.enabled=true;p.Body.position=new Vector3(0,100,p==a?0:3);p.Body.linearVelocity=Vector3.zero;p.Input=new InputFrame{Yaw=0};p.Ready=p.AttackEnd=p.RollEnd=p.BlockEnd=p.StunEnd=p.ShieldUntil=p.FrozenUntil=p.ScareUntil=0;
                }
                Physics.SyncTransforms();
            }
            try
            {
                Setup();a.Seat.access=NativeAccess.Guest;Check(!StartKernelPanic(a),"non-DEV rejected");a.Seat.access=grant;
                Check(StartKernelPanic(a)&&Math.Abs(a.Mana-40)<.001f&&a.KernelReady==40,"cast spends mana/cooldown");b.Hp=10;Clock(10.25f);
                Check(a.Kernel.Active&&ReferenceEquals(a.Kernel,b.Kernel)&&!a.Collider.enabled&&!b.Collider.enabled,"connect locks both and removes colliders");
                var copy=JsonUtility.FromJson<NativeKernelPanic>(JsonUtility.ToJson(a.Kernel));Check(copy.Valid()&&copy.Victim==b.Seat.id&&copy.VictimPoints.Length==6,"Unity JSON carries resolved path");
                Check(Damage(b,null,1000,Vector3.zero,0,false,forced:true)==0&&b.Hp==10,"even environment/forced outsiders cannot damage victim");
                Check(Damage(a,b,1000,Vector3.zero,0,false,forced:true)==0&&a.Hp==100,"caster immune");
                Clock(10.85f);Check(b.Hp==1&&b.Kernel.DamageDebt>0,"first lethal hit retained");
                Clock(12.3f);Check(b.Hp==1&&a.Kernel.Next==4,"catch-up hits cannot kill");
                Clock(12.75f);Check(b.Hp==0&&a.Kernel.Next==5,"finisher kills");
                for(int step=1;step<=5;step++)Check(Events.Any(e=>e.Detail=="kernel-panic-"+step),"per-step event "+step);
                Clock(14.25f);Check(a.Kernel.Outcome==4&&!a.Body.isKinematic&&a.Collider.enabled&&!b.Collider.enabled,"recovery restores only living bodies");
                Setup();b.Body.position=new Vector3(0,100,14);Physics.SyncTransforms();Check(StartKernelPanic(a),"whiff cast");Clock(10.25f);Check(a.Kernel.Outcome==2&&a.KernelReady==25&&Math.Abs(a.Ready-10.85f)<.001f,"whiff recovery and half refund");
                Setup();b.BlockEnd=20;Check(StartKernelPanic(a),"blocked cast");Clock(10.25f);Check(a.Kernel.Outcome==2&&b.Hp==100,"block prevents connect");
                Setup();Check(StartKernelPanic(a),"disconnect cast");Clock(10.25f);a.Seat.connected=false;Clock(10.3f);Check(a.Kernel.Outcome==3&&!b.Body.isKinematic&&b.Collider.enabled,"disconnect releases victim");
                Setup();Check(StartKernelPanic(a),"round-end cast");Clock(10.25f);Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Result,time=10.3f});StepKernelWorld();Check(a.Kernel.Outcome==3&&!a.Body.isKinematic&&!b.Body.isKinematic,"round end releases both");
                Debug.Log("PASS Kernel Panic authority: DEV gate, spend, capsule connect, JSON path, isolation, lethal hold, five events, whiff, block, disconnect and round cleanup");
            }
            finally{if(a.Kernel?.Active==true)EndKernel(a,3);foreach(var x in saved){x.seat.spec=x.spec;x.seat.access=x.access;}UnityEngine.Object.DestroyImmediate(floor);}
        }
    }
}
#endif
