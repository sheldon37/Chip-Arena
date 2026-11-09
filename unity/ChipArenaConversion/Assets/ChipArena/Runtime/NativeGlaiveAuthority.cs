using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Glaive's rules (owner 2026-11-09: "Begin creating a new melee class; Glaive ... It's throwable, slices like a sword, etc."; Claude
    // builds the rules as for the Top Dog, logged for Astra; docs/GLAIVE.md). The host steps it at 60 Hz and writes NativeGlaiveKit, the only
    // thing guests, late joiners and the killcam see. Its left click is its own (the shared melee rules are the four standard blades'): four
    // cuts that chain, a skim on a sprint; Q throws the ring (it ricochets, comes back, bursts on Q or a click while out); E sets it circling.
    // The held guard, the parry and the roll are any melee weapon's.
    public sealed partial class NativePilot
    {
        public NativeGlaiveKit Glaive=new NativeGlaiveKit{Ready=new float[NativeGlaive.Slots]};
        internal readonly List<NativeGlaiveHit> GlaiveHits=new List<NativeGlaiveHit>();
        // Who this flight, flick, skim or return has cut (once each), and when the halo may cut each again.
        internal readonly HashSet<int> GlaiveStruck=new HashSet<int>();internal readonly Dictionary<int,float> GlaiveHaloNext=new Dictionary<int,float>();
        internal bool GlaiveReleased,GlaiveAttackHeld,GlaiveFromThrow;internal Vector3 GlaiveFacing,GlaiveAim,GlaivePos,GlaiveVel,GlaiveFrom,GlaiveTo;internal float GlaiveTravel;
    }
    public sealed partial class NativeArena
    {
        private bool GlaiveAllowed(NativePilot p)=>p.Seat.spec==NativeGlaive.Spec&&(p.Seat.access.DeveloperWeapon(NativeGlaive.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())||p.Seat.devWeaponAccess?.DeveloperWeapon(NativeGlaive.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())==true);
        private bool GlaiveBusy(NativePilot p)=>p.Seat.spec==NativeGlaive.Spec&&Time<p.Glaive.CastUntil;
        /// <summary>Where the ring is held and caught (the body's middle is its centre, .865 m over the feet).</summary>
        private static Vector3 GlaiveHand(NativePilot p)=>p.Body.position+Vector3.up*NativeGlaive.HandHeight;
        private float GlaiveScale(NativePilot p)=>NativeAbilityRules.Scale(p.Stats,false);
        private IEnumerable<NativePilot> GlaivePilots(NativePilot p){foreach(var q in Pilots.ToArray())if(q!=p&&q.Hp>0&&q.Seat.connected&&CanHit(p,q)&&!HubInside(q.Body.position))yield return q;}
        private IEnumerable<NativeMinion> GlaiveMinions(NativePilot p){foreach(var m in Minions.ToArray())if(m.Hp>0&&CanHit(p,m)&&!HubInside(m.Body.position))yield return m;}
        private float GlaiveHurt(NativePilot p,NativePilot q,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
            =>Damage(q,p,amount*(melee?(float)p.Stats["damage"]:GlaiveScale(p)),dir,impulse,false,primary:melee,melee:melee,weaponHit:melee,damageKind:"glaive-"+kind,hitPoint:q.Body.position+Vector3.up*.3f);
        private float GlaiveHurt(NativePilot p,NativeMinion m,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
            =>HitMinion(m,p,amount*(melee?(float)p.Stats["damage"]:GlaiveScale(p)),dir,impulse,m.Body.position,"glaive-"+kind);
        private void GlaivePush(NativeMinion m,Vector3 horizontal,float lift)
        {
            if(m.Hp<=0||m.Body.isKinematic||IsZoneBoss(m))return;var v=m.Body.linearVelocity;v.y=Math.Max(v.y,lift);m.Body.linearVelocity=v;m.BombPush=new Vector3(horizontal.x,0,horizontal.z);m.BombPushUntil=Time+.4f;
        }
        private void GlaiveHit(NativePilot p,string kind,Vector3 at,Vector3 dir,int victim=0)
        {
            if(p.GlaiveHits.Count>=NativeGlaive.MaxHits)p.GlaiveHits.RemoveAt(0);
            p.GlaiveHits.Add(new NativeGlaiveHit{Id=nextEvent++,Kind=kind,At=Time,Pos=Vec(at),Dir=Vec(dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.up),Victim=victim});
        }
        private void GlaiveMotion(NativePilot p,ref Vector3 v)
        {
            if(p.Seat.spec!=NativeGlaive.Spec)return;var k=p.Glaive;
            if(k.CastKind==NativeGlaive.Skim&&Time>=k.CastAt&&Time<k.CastRelease+NativeGlaive.SkimTime+Rules.Dt){v.x=v.z=0;p.MoveVelocity=Vector3.zero;return;}
            if(Time>=k.CastUntil||k.CastKind==null)return;
            float s=k.CastKind==NativeGlaive.Swing?(k.CastStep==2?.4f:.65f):.75f;v.x*=s;v.z*=s;
        }
        private void BotGlaive(NativePilot p,ref InputFrame i,Vector3 target,float distance,bool visible)
        {
            if(!GlaiveAllowed(p))return;var k=p.Glaive;if(k.Ready==null||k.Ready.Length!=NativeGlaive.Slots)k.Ready=new float[NativeGlaive.Slots];
            i.Shoot=i.Attack=visible&&distance<2.8f&&k.Disc==NativeGlaive.Hand;
            // Out: burst it when it is round the target.
            if(k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back){i.AbilityQ=Vector3.Distance(p.GlaivePos,target)<NativeGlaive.BurstRadius*.6f&&Time-k.DiscAt>=NativeGlaive.BurstAfterThrow;return;}
            if(GlaiveBusy(p)||k.Disc!=NativeGlaive.Hand)return;
            if(visible&&distance>4&&distance<20&&Time>=k.Ready[0])i.AbilityQ=true;
            else if(visible&&distance<3.2f&&Time>=k.Ready[1])i.AbilityE=true;
            if(i.AbilityQ||i.AbilityE)i.Shoot=i.Attack=false;
        }
        private void StepGlaiveInput(NativePilot p)
        {
            if(!GlaiveAllowed(p))return;var k=p.Glaive;if(k.Ready==null||k.Ready.Length!=NativeGlaive.Slots)k.Ready=new float[NativeGlaive.Slots];
            bool attack=p.Input.Shoot||p.Input.Attack,press=attack&&!p.GlaiveAttackHeld;p.GlaiveAttackHeld=attack;
            if(p.Hp<=0||Incapacitated(p)||HubInside(p.Body.position))return;
            // The burst: Q, or a press of the left click, while the ring is out (not straight after it leaves the hand).
            if((k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back||k.Disc==NativeGlaive.Orbit)&&(p.Input.AbilityQ||press)&&Time-k.DiscAt>=NativeGlaive.BurstAfterThrow){GlaiveBurst(p);return;}
            if(GlaiveBusy(p)||Time<p.RollEnd||Time<p.BlockEnd||Time<p.Ready||k.Disc!=NativeGlaive.Hand)return;
            string kind=null;
            if(p.Input.AbilityQ&&Time>=k.Ready[0])kind=NativeGlaive.Throw;
            else if(p.Input.AbilityE&&Time>=k.Ready[1])kind=NativeGlaive.Halo;
            else if(attack)kind=p.Grounded&&Time>k.ComboUntil&&NativeMeleeFeel.SprintAttack(p.Input,p.Pose.Sprinting)?NativeGlaive.Skim:NativeGlaive.Swing;
            if(kind==null)return;
            float rate=Math.Max(.5f,(float)p.Stats["attackRate"]),cd=(float)p.Stats["abilityCooldown"];p.GlaiveReleased=false;k.CastKind=kind;k.CastAt=Time;k.CastStep=0;
            p.GlaiveAim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)).normalized;p.GlaiveFacing=Vec(V3.Forward(p.Input.Yaw));p.Yaw=p.Input.Yaw;p.AttackAt=p.LastAttack=Time;
            switch(kind)
            {
                case NativeGlaive.Swing:
                {
                    int step=Time<=k.ComboUntil?(k.Combo+1)%NativeGlaive.Steps:0;k.Combo=k.CastStep=step;
                    k.CastRelease=Time+NativeGlaive.SwingHit[step]/rate;k.CastUntil=Time+NativeGlaive.SwingLength[step]/rate;k.ComboUntil=k.CastUntil+NativeGlaive.ComboWindow;break;
                }
                case NativeGlaive.Skim:k.CastRelease=Time+NativeGlaive.SkimWind;k.CastUntil=k.CastRelease+NativeGlaive.SkimTime+NativeGlaive.SkimRecover;k.ComboUntil=0;break;
                // Q is held until the ring is back (its cooldown runs from the catch or the re-forming).
                case NativeGlaive.Throw:k.CastRelease=Time+NativeGlaive.ThrowRelease;k.CastUntil=k.CastRelease+NativeGlaive.ThrowRecover;k.ComboUntil=0;k.Ready[0]=Time+60;break;
                case NativeGlaive.Halo:k.CastRelease=Time+NativeGlaive.HaloRelease;k.CastUntil=k.CastRelease+.24f;k.ComboUntil=0;k.Ready[1]=k.CastRelease+NativeGlaive.HaloTime+NativeGlaive.Cooldown[1]*cd;break;
            }
            p.Ready=kind==NativeGlaive.Swing?k.CastUntil-.04f:k.CastUntil;
            Emit(EventKind.AbilityCast,p,0,p.Body.position,0,kind);
        }
        private void StepGlaiveWorld()
        {
            foreach(var p in Pilots)
            {
                if(p.Seat.spec!=NativeGlaive.Spec)continue;var k=p.Glaive;if(k.Ready==null||k.Ready.Length!=NativeGlaive.Slots)k.Ready=new float[NativeGlaive.Slots];
                bool able=p.Hp>0&&p.Seat.connected&&!Incapacitated(p);
                // A move cut short (stunned, knocked down, killed) ends; a dead wielder's ring is back in its hand.
                if(k.CastKind!=null&&Time<k.CastUntil&&!able){k.CastUntil=Time;if(k.CastKind==NativeGlaive.Throw&&!p.GlaiveReleased)k.Ready[0]=Time;}
                if(p.Hp<=0&&k.Disc!=NativeGlaive.Hand){k.Disc=NativeGlaive.Hand;k.DiscAt=Time;k.Reformed=false;k.Ready[0]=Math.Min(k.Ready[0],Time+NativeGlaive.Cooldown[0]);}
                if(!p.GlaiveReleased&&k.CastKind!=null&&Time>=k.CastRelease){p.GlaiveReleased=true;if(able&&Time<=k.CastUntil+.001f)GlaiveRelease(p);}
                if(able)GlaiveSkimStep(p);
                GlaiveDiscStep(p);
                p.GlaiveHits.RemoveAll(h=>Time-h.At>1.5f);k.Hits=p.GlaiveHits.ToArray();
            }
        }
        private void GlaiveRelease(NativePilot p)
        {
            var k=p.Glaive;
            switch(k.CastKind)
            {
                case NativeGlaive.Swing:if(k.CastStep==3)GlaiveFlick(p);else GlaiveCut(p,k.CastStep);break;
                case NativeGlaive.Skim:
                {
                    var from=p.Body.position;var dir=p.GlaiveFacing;float reach=NativeGlaive.SkimReach;
                    var hit=SweepNative(from+Vector3.up*.2f,dir*reach,.35f,c=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger);
                    if(hit.HasValue)reach=Math.Max(0,hit.Value.Distance-.45f);
                    p.GlaiveFrom=from;p.GlaiveTo=from+dir*reach;p.GlaiveStruck.Clear();GlaiveHit(p,NativeGlaive.HitSkim,GroundPoint(from),dir);break;
                }
                case NativeGlaive.Throw:
                {
                    p.GlaivePos=GlaiveHand(p)+p.GlaiveAim*.4f;p.GlaiveVel=p.GlaiveAim*NativeGlaive.ThrowSpeed;p.GlaiveTravel=0;p.GlaiveStruck.Clear();p.GlaiveFromThrow=true;
                    k.Disc=NativeGlaive.Out;k.DiscAt=Time;k.Bounces=0;k.Reformed=false;GlaiveSample(p);break;
                }
                case NativeGlaive.Halo:
                {
                    k.Disc=NativeGlaive.Orbit;k.DiscAt=Time;k.DiscDir=Vec(p.GlaiveFacing);k.Reformed=false;p.GlaiveHaloNext.Clear();p.GlaiveFromThrow=false;p.GlaivePos=GlaiveHand(p);break;
                }
            }
        }
        /// <summary>A cut (the forehand, the backhand: a cone ahead; the whirl: all round): the shared melee's damage, a stagger, a little push.</summary>
        private void GlaiveCut(NativePilot p,int step)
        {
            var at=p.Body.position;var f=p.GlaiveFacing;float reach=NativeGlaive.SwingReach[step],arc=NativeGlaive.SwingArc[step];float damage=NativeGlaive.SwingDamage[step];string kind=step==2?NativeGlaive.HitWhirl:NativeGlaive.HitCut;
            bool Inside(Vector3 q){var d=q-at;if(Math.Abs(d.y)>1.9f)return false;var flat=new Vector3(d.x,0,d.z);float l=flat.magnitude;if(l>reach+.45f)return false;if(l<.6f||arc>=3.1f)return true;return Vector3.Angle(f,flat)*Mathf.Deg2Rad<=arc;}
            foreach(var q in GlaivePilots(p))
            {
                if(!Inside(q.Body.position)||!MeleeVisible(at+Vector3.up*.3f,q.Body.position,p,q.Collider))continue;
                var d=q.Body.position-at;if(GlaiveHurt(p,q,damage,d,NativeGlaive.Swing,(float)p.Weapon.impulse,true)<=0)continue;
                StaggerMelee(q,NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"]));if(step==2)q.Knockback+=new Vector3(d.x,0,d.z).normalized*NativeGlaive.SwingPush;
                GlaiveHit(p,kind,q.Body.position+Vector3.up*.45f,d,q.Seat.id);
            }
            foreach(var m in GlaiveMinions(p))
            {
                if(!Inside(m.Body.position)||!MeleeVisible(at+Vector3.up*.3f,m.Body.position,p,m.Collider))continue;
                var d=m.Body.position-at;if(GlaiveHurt(p,m,damage,d,NativeGlaive.Swing,0,true)<=0)continue;GlaivePush(m,new Vector3(d.x,0,d.z).normalized*NativeGlaive.SwingPush,1);
                GlaiveHit(p,kind,m.Body.position+Vector3.up*.45f,d,m.Id);
            }
        }
        /// <summary>The fourth cut: the ring flicked out on its boomerang loop and caught again (Core NativeGlaive.FlickAt).</summary>
        private void GlaiveFlick(NativePilot p)
        {
            var k=p.Glaive;k.Disc=NativeGlaive.Flick;k.DiscAt=Time;k.DiscFrom=Vec(GlaiveHand(p));k.DiscDir=Vec(p.GlaiveFacing);k.Reformed=false;p.GlaiveStruck.Clear();p.GlaiveFromThrow=false;
        }
        private void GlaiveSkimStep(NativePilot p)
        {
            var k=p.Glaive;if(k.CastKind!=NativeGlaive.Skim||Time<k.CastRelease||Time>k.CastRelease+NativeGlaive.SkimTime+Rules.Dt)return;
            float u=Mathf.Clamp01((Time-k.CastRelease)/NativeGlaive.SkimTime);var to=Vector3.Lerp(p.GlaiveFrom,p.GlaiveTo,1-(1-u)*(1-u));p.Body.position=KernelTravel(p.Body.position,to);p.MoveVelocity=Vector3.zero;
            bool Beside(Vector3 d)=>new Vector2(d.x,d.z).magnitude<=NativeGlaive.SkimWidth&&Math.Abs(d.y)<=1.8f;
            foreach(var q in GlaivePilots(p))
            {
                if(p.GlaiveStruck.Contains(q.Seat.id))continue;var d=q.Body.position-p.Body.position;if(!Beside(d))continue;p.GlaiveStruck.Add(q.Seat.id);
                if(GlaiveHurt(p,q,NativeGlaive.SkimDamage,d,NativeGlaive.Skim,0,true)>0){var side=Vector3.Cross(Vector3.up,p.GlaiveFacing);if(Vector3.Dot(side,d)<0)side=-side;q.Knockback+=(side*.6f+p.GlaiveFacing*.4f).normalized*NativeGlaive.SkimPush;StaggerMelee(q,.3f);GlaiveHit(p,NativeGlaive.HitCut,q.Body.position+Vector3.up*.45f,side,q.Seat.id);}
            }
            foreach(var m in GlaiveMinions(p))
            {
                if(p.GlaiveStruck.Contains(m.Id))continue;var d=m.Body.position-p.Body.position;if(!Beside(d))continue;p.GlaiveStruck.Add(m.Id);
                if(GlaiveHurt(p,m,NativeGlaive.SkimDamage,d,NativeGlaive.Skim,0,true)>0){GlaivePush(m,p.GlaiveFacing*NativeGlaive.SkimPush,1);GlaiveHit(p,NativeGlaive.HitCut,m.Body.position+Vector3.up*.45f,p.GlaiveFacing,m.Id);}
            }
        }
        private void GlaiveSample(NativePilot p){var k=p.Glaive;k.DiscPos=Vec(p.GlaivePos);k.DiscVel=Vec(p.GlaiveVel);k.DiscSampleAt=Time;}
        private static float SegmentDistance(Vector3 a,Vector3 b,Vector3 q){var ab=b-a;float l=ab.sqrMagnitude;float t=l>1e-8f?Mathf.Clamp01(Vector3.Dot(q-a,ab)/l):0;return Vector3.Distance(a+ab*t,q);}
        /// <summary>The ring out of the hand: the throw's flight and ricochets, its return, the flick's loop, the halo, the re-forming.</summary>
        private void GlaiveDiscStep(NativePilot p)
        {
            var k=p.Glaive;float cd=(float)p.Stats["abilityCooldown"];
            void Caught(){k.Disc=NativeGlaive.Hand;k.DiscAt=Time;k.Reformed=false;if(p.GlaiveFromThrow)k.Ready[0]=Time+NativeGlaive.Cooldown[0]*cd;GlaiveHit(p,NativeGlaive.HitCatch,GlaiveHand(p),p.GlaiveFacing);}
            void Return(){k.Disc=NativeGlaive.Back;p.GlaiveStruck.Clear();GlaiveSample(p);}
            switch(k.Disc)
            {
                case NativeGlaive.Flick:
                {
                    float u=(Time-k.DiscAt)/NativeGlaive.FlickTime;var at=NativeGlaive.FlickAt(NativeGlaive.N(k.DiscFrom),NativeGlaive.N(k.DiscDir),u);var pos=new Vector3(at.X,at.Y,at.Z);p.GlaivePos=pos;
                    int half=u<.5f?0:1;
                    foreach(var q in GlaivePilots(p)){int key=q.Seat.id*2+half;if(p.GlaiveStruck.Contains(key)||Vector3.Distance(q.Body.position+Vector3.up*.2f,pos)>NativeGlaive.FlickRadius+.35f)continue;p.GlaiveStruck.Add(key);var d=q.Body.position-p.Body.position;if(GlaiveHurt(p,q,NativeGlaive.SwingDamage[3],d,NativeGlaive.Swing,0,true)>0){StaggerMelee(q,.25f);GlaiveHit(p,NativeGlaive.HitFlick,pos,d,q.Seat.id);}}
                    foreach(var m in GlaiveMinions(p)){int key=m.Id*2+half;if(p.GlaiveStruck.Contains(key)||Vector3.Distance(m.Body.position,pos)>NativeGlaive.FlickRadius+.35f)continue;p.GlaiveStruck.Add(key);if(GlaiveHurt(p,m,NativeGlaive.SwingDamage[3],m.Body.position-p.Body.position,NativeGlaive.Swing,0,true)>0)GlaiveHit(p,NativeGlaive.HitFlick,pos,m.Body.position-pos,m.Id);}
                    if(u>=1)Caught();break;
                }
                case NativeGlaive.Out:
                {
                    var from=p.GlaivePos;var to=from+p.GlaiveVel*Rules.Dt;float step=(to-from).magnitude;
                    // A wall turns it back with a spark.
                    var wall=SweepNative(from,to-from,.12f,c=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger);
                    if(wall.HasValue){p.GlaivePos=from+(to-from).normalized*Math.Max(0,wall.Value.Distance-.1f);GlaiveHit(p,NativeGlaive.HitRicochet,p.GlaivePos,-p.GlaiveVel);Return();break;}
                    p.GlaivePos=to;p.GlaiveTravel+=step;
                    // The first enemy it reaches: cut, then on to the nearest other in sight, or back.
                    NativePilot hitPilot=null;NativeMinion hitMinion=null;
                    foreach(var q in GlaivePilots(p)){if(p.GlaiveStruck.Contains(q.Seat.id))continue;if(SegmentDistance(from,to,q.Body.position+Vector3.up*.2f)<=NativeGlaive.ThrowRadius+.35f){hitPilot=q;break;}}
                    if(hitPilot==null)foreach(var m in GlaiveMinions(p)){if(p.GlaiveStruck.Contains(m.Id))continue;if(SegmentDistance(from,to,m.Body.position)<=NativeGlaive.ThrowRadius+.4f){hitMinion=m;break;}}
                    if(hitPilot!=null||hitMinion!=null)
                    {
                        float damage=NativeGlaive.ThrowDamage*Mathf.Pow(NativeGlaive.RicochetScale,k.Bounces);Vector3 where;
                        if(hitPilot!=null){p.GlaiveStruck.Add(hitPilot.Seat.id);where=hitPilot.Body.position+Vector3.up*.3f;if(GlaiveHurt(p,hitPilot,damage,p.GlaiveVel,NativeGlaive.Throw)>0)StaggerMelee(hitPilot,.3f);GlaiveHit(p,k.Bounces==0?NativeGlaive.HitThrow:NativeGlaive.HitRicochet,where,p.GlaiveVel,hitPilot.Seat.id);}
                        else{p.GlaiveStruck.Add(hitMinion.Id);where=hitMinion.Body.position;GlaiveHurt(p,hitMinion,damage,p.GlaiveVel,NativeGlaive.Throw);GlaiveHit(p,k.Bounces==0?NativeGlaive.HitThrow:NativeGlaive.HitRicochet,where,p.GlaiveVel,hitMinion.Id);}
                        p.GlaivePos=where;Vector3? next=null;float best=NativeGlaive.RicochetRange;
                        if(k.Bounces<NativeGlaive.Ricochets)
                        {
                            foreach(var q in GlaivePilots(p)){if(p.GlaiveStruck.Contains(q.Seat.id))continue;var c=q.Body.position+Vector3.up*.2f;float d=Vector3.Distance(c,where);if(d<best&&MeleeVisible(where,c,p,q.Collider)){best=d;next=c;}}
                            foreach(var m in GlaiveMinions(p)){if(p.GlaiveStruck.Contains(m.Id))continue;var c=m.Body.position;float d=Vector3.Distance(c,where);if(d<best&&MeleeVisible(where,c,p,m.Collider)){best=d;next=c;}}
                        }
                        if(next.HasValue){k.Bounces++;p.GlaiveVel=(next.Value-where).normalized*NativeGlaive.ThrowSpeed;p.GlaiveTravel=0;GlaiveSample(p);}else Return();
                        break;
                    }
                    if(p.GlaiveTravel>=NativeGlaive.ThrowReach||Time-k.DiscAt>=NativeGlaive.MostOut)Return();else GlaiveSample(p);
                    break;
                }
                case NativeGlaive.Back:
                {
                    // Home on the hand through anything in the way, cutting whoever it passes once.
                    var hand=GlaiveHand(p);var d=hand-p.GlaivePos;float l=d.magnitude,step=NativeGlaive.ReturnSpeed*Rules.Dt;var from=p.GlaivePos;
                    if(l<=NativeGlaive.CatchRadius+step||p.Hp<=0){Caught();break;}
                    p.GlaiveVel=d/l*NativeGlaive.ReturnSpeed;p.GlaivePos+=p.GlaiveVel*Rules.Dt;
                    foreach(var q in GlaivePilots(p)){if(p.GlaiveStruck.Contains(q.Seat.id)||SegmentDistance(from,p.GlaivePos,q.Body.position+Vector3.up*.2f)>NativeGlaive.ReturnRadius+.35f)continue;p.GlaiveStruck.Add(q.Seat.id);if(GlaiveHurt(p,q,NativeGlaive.ReturnDamage,p.GlaiveVel,NativeGlaive.Throw)>0)GlaiveHit(p,NativeGlaive.HitReturn,q.Body.position+Vector3.up*.3f,p.GlaiveVel,q.Seat.id);}
                    foreach(var m in GlaiveMinions(p)){if(p.GlaiveStruck.Contains(m.Id)||SegmentDistance(from,p.GlaivePos,m.Body.position)>NativeGlaive.ReturnRadius+.4f)continue;p.GlaiveStruck.Add(m.Id);if(GlaiveHurt(p,m,NativeGlaive.ReturnDamage,p.GlaiveVel,NativeGlaive.Throw)>0)GlaiveHit(p,NativeGlaive.HitReturn,m.Body.position,p.GlaiveVel,m.Id);}
                    if(Time-k.DiscAt>NativeGlaive.MostOut+3)Caught();else GlaiveSample(p);
                    break;
                }
                case NativeGlaive.Orbit:
                {
                    var at=NativeGlaive.OrbitAt(new System.Numerics.Vector3(p.Body.position.x,p.Body.position.y,p.Body.position.z)+System.Numerics.Vector3.UnitY*.1f,NativeGlaive.N(k.DiscDir),k.DiscAt,Time);var pos=new Vector3(at.X,at.Y,at.Z);p.GlaivePos=pos;
                    foreach(var q in GlaivePilots(p)){if(p.GlaiveHaloNext.TryGetValue(q.Seat.id,out float next)&&Time<next)continue;if(Vector3.Distance(q.Body.position+Vector3.up*.1f,pos)>NativeGlaive.HaloHitRadius+.35f)continue;p.GlaiveHaloNext[q.Seat.id]=Time+NativeGlaive.HaloEvery;var d=q.Body.position-p.Body.position;if(GlaiveHurt(p,q,NativeGlaive.HaloDamage,d,NativeGlaive.Halo)>0){StaggerMelee(q,.15f);GlaiveHit(p,NativeGlaive.HitHalo,pos,d,q.Seat.id);}}
                    foreach(var m in GlaiveMinions(p)){if(p.GlaiveHaloNext.TryGetValue(m.Id,out float next)&&Time<next)continue;if(Vector3.Distance(m.Body.position,pos)>NativeGlaive.HaloHitRadius+.4f)continue;p.GlaiveHaloNext[m.Id]=Time+NativeGlaive.HaloEvery;if(GlaiveHurt(p,m,NativeGlaive.HaloDamage,m.Body.position-p.Body.position,NativeGlaive.Halo)>0)GlaiveHit(p,NativeGlaive.HitHalo,pos,m.Body.position-pos,m.Id);}
                    if(Time>=k.DiscAt+NativeGlaive.HaloTime){p.GlaiveVel=Vector3.zero;Return();}
                    break;
                }
                case NativeGlaive.Gone:
                    if(Time>=k.DiscAt+NativeGlaive.Reform){k.Disc=NativeGlaive.Hand;k.DiscAt=Time;k.Reformed=true;if(p.GlaiveFromThrow)k.Ready[0]=Time+NativeGlaive.Cooldown[0]*cd;}
                    break;
            }
        }
        /// <summary>The ring bursts where it is: everyone round it hurt and thrown out; it re-forms in the hand.</summary>
        private void GlaiveBurst(NativePilot p)
        {
            var k=p.Glaive;var at=p.GlaivePos;int first=0;
            foreach(var q in GlaivePilots(p))
            {
                var d=q.Body.position-at;if(d.magnitude>NativeGlaive.BurstRadius+.3f||!MeleeVisible(at,q.Body.position,p,q.Collider))continue;
                if(GlaiveHurt(p,q,NativeGlaive.BurstDamage,d,NativeGlaive.Burst)<=0)continue;if(first==0)first=q.Seat.id;
                BombLaunch(q,p,new Vector3(d.x,0,d.z).normalized*NativeGlaive.BurstPush,NativeGlaive.BurstLift,0);
            }
            foreach(var m in GlaiveMinions(p))
            {
                var d=m.Body.position-at;if(d.magnitude>NativeGlaive.BurstRadius+.4f)continue;if(GlaiveHurt(p,m,NativeGlaive.BurstDamage,d,NativeGlaive.Burst)<=0)continue;if(first==0)first=m.Id;
                GlaivePush(m,new Vector3(d.x,0,d.z).normalized*NativeGlaive.BurstPush,NativeGlaive.BurstLift);
            }
            GlaiveHit(p,NativeGlaive.HitBurst,at,Vector3.up,first);
            k.Disc=NativeGlaive.Gone;k.DiscAt=Time;k.DiscPos=Vec(at);k.DiscVel=default;k.DiscSampleAt=Time;
            // A pose for it: the free hand snapped shut.
            if(!GlaiveBusy(p)){k.CastKind=NativeGlaive.Burst;k.CastAt=Time;k.CastRelease=Time+.05f;k.CastUntil=Time+.3f;k.CastStep=0;p.GlaiveReleased=true;}
            Emit(EventKind.AbilityCast,p,0,at,0,NativeGlaive.Burst);
        }
    }
}
