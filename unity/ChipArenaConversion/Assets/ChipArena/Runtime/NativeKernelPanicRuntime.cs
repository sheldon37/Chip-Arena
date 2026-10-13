using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {
        public NativeKernelPanic Kernel;public float KernelReady;
        public float KernelTravelled;
        public bool KernelKinematic,KernelCollider,KernelPinned;
    }
    public sealed partial class NativeArena
    {
        private bool KernelBusy(NativePilot p)=>p?.Kernel?.Busy(Time)==true;
        private bool KernelLocked(NativePilot p)=>p?.Kernel?.Active==true;
        private bool StartKernelPanic(NativePilot p)
        {
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if(p.Seat.spec!="cyberlord"||!p.Seat.access.DeveloperWeapon("cyberlord",now)||p.Hp<=0||!p.Seat.connected||Incapacitated(p)||Time<p.Ready||Time<p.KernelReady||Time<p.AttackEnd||Time<p.RollEnd||Time<p.BlockEnd||p.AbilityCast!=null||p.Special!=null||p.Mana<NativeKernelPanicRules.Mana*(float)p.Stats["resourceCost"])return false;
            p.Mana-=NativeKernelPanicRules.Mana*(float)p.Stats["resourceCost"];
            NativeCyberRules.Interrupt(p.Cyber,Time);p.CyberLinkTick=0;
            p.KernelReady=Time+NativeKernelPanicRules.Cooldown;p.KernelTravelled=0;
            p.Kernel=new NativeKernelPanic{Id=nextAttack++,Caster=p.Seat.id,Seed=random.Next(),At=Time,Until=Time+NativeKernelPanicRules.Windup+NativeKernelPanicRules.Range/NativeKernelPanicRules.LungeSpeed,Ready=p.KernelReady,CasterStart=Vec(p.Body.position),Anchor=Vec(p.Body.position),Facing=V3.Forward(p.Input.Yaw)};
            p.Yaw=p.Input.Yaw;p.AttackAt=p.LastAttack=Time;p.AttackEnd=p.Ready=p.Kernel.Until;p.MoveVelocity=Vector3.zero;if(!p.Body.isKinematic){var v=p.Body.linearVelocity;p.Body.linearVelocity=new Vector3(0,v.y,0);}
            return true;
        }
        private RaycastHit[] kernelHits=new RaycastHit[16];
        private readonly Collider[] kernelOverlap=new Collider[1];
        private bool KernelClear(Vector3 at)
        {
            var clamp=ClampMap(at);if((clamp-at).sqrMagnitude>.0001f)return false;
            if(root.scene.GetPhysicsScene().OverlapCapsule(at+Vector3.up*.43f,at-Vector3.up*.43f,.41f,kernelOverlap,WorldMask,QueryTriggerInteraction.Ignore)!=0)return false;
            foreach(var h in NativeHazards.At(MapId,Time))
                if(Math.Abs(at.x-h.x)<Math.Max(h.sx*.5f,1.7f)+.6f&&Math.Abs(at.z-h.z)<Math.Max(h.sz*.5f,1.7f)+.6f)return false;
            return true;
        }
        private bool KernelGround(Vector3 desired,out Vector3 safe)
        {
            var scene=root.scene.GetPhysicsScene();desired=ClampMap(desired);
            // Nearest deterministic grid candidate, bounded to six metres; no random guest-side search.
            for(int ring=0;ring<=12;ring++)for(int x=-ring;x<=ring;x++)for(int z=-ring;z<=ring;z++)
            {
                if(Math.Max(Math.Abs(x),Math.Abs(z))!=ring)continue;
                var probe=ClampMap(desired+new Vector3(x*.5f,0,z*.5f));
                if(scene.Raycast(probe+Vector3.up*12,Vector3.down,out var floor,40,WorldMask,QueryTriggerInteraction.Ignore)&&floor.normal.y>=.65f)
                {var point=floor.point+Vector3.up*.875f;if(KernelClear(point)){safe=point;return true;}}
            }
            safe=default;return false;
        }
        private Vector3 KernelTravel(Vector3 from,Vector3 to)
        {
            to=ClampMap(to);var delta=to-from;float length=delta.magnitude;
            if(length>.001f&&root.scene.GetPhysicsScene().CapsuleCast(from+Vector3.up*.43f,from-Vector3.up*.43f,.42f,delta/length,out var wall,length,WorldMask,QueryTriggerInteraction.Ignore))to=from+delta/length*Math.Max(0,wall.distance-.04f);
            return KernelClear(to)?to:from;
        }
        private void KernelPin(NativePilot p)
        {
            p.KernelKinematic=p.Body.isKinematic;p.KernelCollider=p.Collider.enabled;p.KernelPinned=true;
            NativeCyberRules.Interrupt(p.Cyber,Time);p.CyberLinkTick=0;CancelSiren(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);CancelFighter(p);p.Down=null;p.Juggle=null;p.Roll=null;p.RollEnd=p.BlockEnd=p.StunEnd=Time;p.Knockback=p.MoveVelocity=Vector3.zero;
            if(!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;p.Body.isKinematic=true;p.Collider.enabled=false;
        }
        private void KernelRelease(NativePilot p)
        {
            if(!p.KernelPinned)return;p.KernelPinned=false;
            p.Body.isKinematic=Replica||p.KernelKinematic||p.Hp<=0||!p.Seat.connected;p.Collider.enabled=p.KernelCollider&&p.Hp>0&&p.Seat.connected;
            if(!p.Body.isKinematic){p.Body.linearVelocity=Vector3.zero;p.Body.useGravity=true;}
            p.Knockback=p.MoveVelocity=Vector3.zero;p.AttackEnd=p.Ready=Time;
        }
        private Vector3 KernelExit(Vector3 current,Vector3 fallback)
        {
            if(KernelGround(current,out var safe)||KernelGround(fallback,out safe))return safe;
            foreach(var spawn in Content.spawns)if(KernelGround(Vec(spawn),out safe))return safe;
            // Supported maps have a permanent floor; this fallback remains inside the prevalidated arena bounds.
            return ClampMap(fallback);
        }
        private void EndKernel(NativePilot caster,int outcome)
        {
            var s=caster.Kernel;if(s==null)return;var victim=Pilots.Find(p=>p.Seat.id==s.Victim);
            // Cancellation can occur in midair; settle survivors before restoring their colliders.
            if(s.Active)
            {
                if(caster.Hp>0)caster.Body.position=KernelExit(caster.Body.position,Vec(s.CasterExit));
                if(victim!=null&&victim.Hp>0)victim.Body.position=KernelExit(victim.Body.position,Vec(s.VictimExit));
            }
            s.Outcome=outcome;s.Until=Time;s.DamageDebt=0;KernelRelease(caster);if(victim!=null)KernelRelease(victim);
        }
        private void KernelWhiff(NativePilot p)
        {
            var s=p.Kernel;s.Outcome=2;s.Until=Time+NativeKernelPanicRules.WhiffRecovery;p.Ready=p.AttackEnd=s.Until;
            s.Ready=p.KernelReady=s.At+NativeKernelPanicRules.Cooldown*.5f;
        }
        private void ConnectKernel(NativePilot p)
        {
            var s=p.Kernel;var origin=p.Body.position;var facing=Vec(s.Facing);
            float requested=Math.Min(NativeKernelPanicRules.LungeSpeed/60f,NativeKernelPanicRules.Range-p.KernelTravelled);
            float range=ForwardAbilityStep(origin,facing,requested,out bool wall);
            var end=ClampMap(origin+facing*range);range=Math.Max(0,Vector3.Dot(end-origin,facing));
            wall|=range+1e-4f<requested;
            // Real capsule sweep: all colliders, nearest enemy contact, with a stable id tie-break.
            var scene=root.scene.GetPhysicsScene();int count;
            while((count=scene.CapsuleCast(origin+Vector3.up*.43f,origin-Vector3.up*.43f,NativeKernelPanicRules.Radius,facing,kernelHits,range,ShotMask,QueryTriggerInteraction.Ignore))==kernelHits.Length)
                Array.Resize(ref kernelHits,kernelHits.Length*2);
            NativePilot target=null;float first=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var q=kernelHits[i].collider.GetComponent<NativePilotBody>()?.Pilot;
                if(q==null||q==p||q.Hp<=0||!q.Seat.connected||!CanHit(p,q)||KernelBusy(q)||q.ScareBy!=null||q.HeldBy!=null||q.Seat.AuthorityBoss||!MeleeVisible(origin,q.Body.position,p,q.Collider))continue;
                float contact=kernelHits[i].distance;
                if(contact>first||contact==first&&target!=null&&q.Seat.id>target.Seat.id)continue;
                first=contact;target=q;
            }
            foreach(var q in Pilots)
            {
                if(q==p||q.Hp<=0||!q.Seat.connected||!CanHit(p,q)||KernelBusy(q)||q.ScareBy!=null||q.HeldBy!=null||q.Seat.AuthorityBoss||!q.Collider.enabled)continue;
                var nearest=q.Collider.ClosestPoint(origin);
                if(Vector3.Dot(q.Body.position-origin,facing)<0||(nearest-origin).sqrMagnitude>NativeKernelPanicRules.Radius*NativeKernelPanicRules.Radius||!MeleeVisible(origin,q.Body.position,p,q.Collider))continue;
                if(first>0||target==null||q.Seat.id<target.Seat.id){first=0;target=q;}
            }
            if(target==null)
            {
                p.Body.position=end;p.KernelTravelled+=range;
                if(wall||p.KernelTravelled>=NativeKernelPanicRules.Range-.001f)KernelWhiff(p);
                return;
            }
            p.Body.position=origin+facing*Math.Max(0,first-.08f);
            if(Time<target.BlockEnd||Invulnerable(target)||Time<target.ShieldUntil||NativeSpecialRules.Protected(target.Down,Time))
            {if(Time<target.BlockEnd)BlockProc(target,p,target.Body.position,"kernel-panic");KernelWhiff(p);return;}
            if(!KernelGround(target.Body.position,out var landing)||!KernelGround(p.Body.position,out var casterSafe)){KernelWhiff(p);return;}
            s.Anchor=s.VictimStart=Vec(target.Body.position);s.CasterStart=Vec(p.Body.position);s.Victim=target.Seat.id;s.ConnectedAt=Time;s.Until=Time+NativeKernelPanicRules.CutIn+NativeKernelPanicRules.Duration;
            var v0=target.Body.position;var v1=KernelTravel(v0,v0+facing*8+Vector3.up*1.5f);s.WallSplat=(v1-(v0+facing*8+Vector3.up*1.5f)).sqrMagnitude>.01f;var v2=KernelTravel(v1,v1+Vector3.up*5);
            if(!KernelGround(v1,out landing)){KernelWhiff(p);s.Victim=0;return;}
            // The downward leg is swept too; a roof becomes the safe landing floor, never a through-wall teleport.
            var v3=KernelTravel(v2,landing+Vector3.up*2);var v4=KernelTravel(v3,landing);
            if((v4-landing).sqrMagnitude>.04f){KernelWhiff(p);s.Victim=0;return;}
            var side=Vector3.Cross(Vector3.up,facing)*((s.Seed&1)==0?1.2f:-1.2f);
            Vector3 C(Vector3 at){if(KernelGround(at,out var floor))return floor;return casterSafe;}
            s.VictimPoints=new[]{Vec(v0),Vec(v1),Vec(v2),Vec(v3),Vec(v4),Vec(landing)};
            s.CasterPoints=new[]{s.CasterStart,Vec(KernelTravel(v1,v1-facing*1.1f)),Vec(KernelTravel(v2,v2+Vector3.up*1.8f)),Vec(KernelTravel(v3,v3+side)),Vec(C(landing-facing*1.2f)),Vec(C(landing-facing*1.2f))};
            s.CasterExit=Vec(casterSafe);s.VictimExit=Vec(landing);s.Outcome=1;target.Kernel=s;KernelPin(p);KernelPin(target);p.Ready=p.AttackEnd=target.Ready=target.AttackEnd=s.Until;
        }
        private void CancelInvalidKernel()
        {
            foreach(var p in Pilots)
            {
                var s=p.Kernel;if(s==null||s.Caster!=p.Seat.id||!s.Busy(Time))continue;
                var victim=Pilots.Find(q=>q.Seat.id==s.Victim);
                if(Match.Phase!=NativePhase.Fight||!p.Seat.connected||s.Active&&(victim==null||!victim.Seat.connected))EndKernel(p,3);
            }
        }
        private void StepKernelWorld()
        {
            foreach(var p in Pilots.ToArray())
            {
                var s=p.Kernel;if(s==null||s.Caster!=p.Seat.id)continue;
                if(!s.Busy(Time))continue;
                var victim=Pilots.Find(q=>q.Seat.id==s.Victim);
                if(Match.Phase!=NativePhase.Fight||!p.Seat.connected||p.Hp<=0||s.Active&&(victim==null||!victim.Seat.connected||victim.Hp<=0&&s.Next<5)){EndKernel(p,3);continue;}
                if(s.Outcome==0)
                {
                    if(Time<p.StunEnd||Downed(p)||p.Juggle!=null||Frozen(p)){EndKernel(p,3);continue;}
                    if(Time>=s.At+NativeKernelPanicRules.Windup)ConnectKernel(p);continue;
                }
                if(!s.Active)continue;
                NativeKernelPanicRules.Sample(s,Time,out var casterAt,out var victimAt);
                if(!KernelClear(Vec(casterAt))||!KernelClear(Vec(victimAt))){EndKernel(p,3);continue;}
                p.Body.position=Vec(casterAt);if(victim.Hp>0)victim.Body.position=Vec(victimAt);
                while(NativeKernelPanicRules.Due(s,Time))
                {
                    int step=++s.Next,repeats=step==4?3:1;
                    for(int hit=0;hit<repeats;hit++)
                    {
                        float actual=Damage(victim,p,NativeKernelPanicRules.Damage[step-1]*(float)p.Stats["damage"],Vec(s.Facing),0,false,rollAvoidable:false,weaponHit:true,damageKind:"kernel-panic-"+step,kernelStep:step);
                        if(actual>0)ImpactChips(p,victim,null,victim.Body.position,Vec(s.Facing),s.Id,s.Id*16+step*3+hit);
                        if(actual<=0)Emit(EventKind.Hit,p,victim.Seat.id,victim.Body.position,0,"kernel-panic-"+step);
                    }
                    if(step==5)foreach(var q in Pilots.ToArray())if(q!=victim&&q!=p&&q.Hp>0&&!KernelLocked(q)&&NativeCombatRules.CanHit(p.Seat,q.Seat,Match.Teams,FriendlyFire)&&(q.Body.position-victim.Body.position).magnitude<=5&&!SceneLinecast(victim.Body.position,q.Body.position,WorldMask))
                        Damage(q,p,20*(float)p.Stats["damage"],q.Body.position-victim.Body.position,260,false,direct:false,primary:false,damageKind:"kernel-panic-wave",kernelStep:5);
                }
                if(Time>=s.Until)EndKernel(p,4);
            }
        }
    }
}
