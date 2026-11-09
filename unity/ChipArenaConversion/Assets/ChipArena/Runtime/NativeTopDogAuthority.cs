using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Top Dog's rules (owner 2026-11-08: a DEV class for the Hot Dog, "toon power", abilities 1-5 and an ultimate that traps a pilot, feeds
    // them wiggling hot dogs, lets them go swollen and they burst; the owner chose that Claude builds the rules too; docs/TOP-DOG.md). The host
    // steps it at 60 Hz and writes NativeTopDogKit, the only thing guests, late joiners and the killcam see. The mallet is its own left click (the
    // shared melee rules are the four standard blades'); Q, E and 1 to 5 are cooldown slots; 6 spends the meter on All You Can Eat.
    internal sealed class DogFlight
    {public NativeDogProp Record;public bool Landed,Done;public float Tick;public int Target;public bool TargetMinion;}
    public sealed partial class NativePilot
    {
        public NativeTopDogKit Dog=new NativeTopDogKit{Peels=NativeTopDog.PeelCharges,Ready=new float[NativeTopDog.Slots]};
        internal readonly List<DogFlight> DogFlights=new List<DogFlight>();
        internal readonly List<NativeDogMark> DogMarks=new List<NativeDogMark>();
        internal readonly List<NativeDogHit> DogHits=new List<NativeDogHit>();
        internal readonly HashSet<int> DogZipHit=new HashSet<int>();
        internal bool DogReleased,DogAirborne,DogPopped,DogFed;internal float DogAirAt,DogPeelReady,DogTick,DogPuddleTick,DogTaken;
        internal Vector3 DogAim,DogPoint,DogFacing,DogZipFrom,DogZipTo;
        /// <summary>The Top Dog feeding on this pilot (null: none).</summary>
        internal NativePilot DogHolder;
        internal NativeMinion DogMinion;
    }
    public sealed partial class NativeArena
    {
        private static readonly string[] DogKinds=NativeTopDog.Slot;
        private bool DogAllowed(NativePilot p)=>p.Seat.spec==NativeTopDog.Spec&&(p.Seat.access.DeveloperWeapon(NativeTopDog.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())||p.Seat.devWeaponAccess?.DeveloperWeapon(NativeTopDog.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())==true);
        /// <summary>Rooted: the feast's twirl, throw and feeding (to the let-go, or the lasso's miss), and the dash's wind-up.</summary>
        private bool DogRooted(NativePilot p)
        {
            if(p.Seat.spec!=NativeTopDog.Spec)return false;var f=p.Dog.Feast;var k=p.Dog;
            if(f!=null&&Time<(f.Catch>-50?(f.Snap>-50?f.Snap:f.Release):f.Until))return true;
            return k.CastKind==NativeTopDog.Zip&&Time>=k.CastAt&&Time<k.CastRelease;
        }
        private bool DogBusy(NativePilot p)=>p.Seat.spec==NativeTopDog.Spec&&(Time<p.Dog.CastUntil||DogRooted(p));
        /// <summary>A pop still to come (the round waits for it, as for the Bombadier's strike).</summary>
        private bool DogPending()=>Pilots.Any(p=>p.Dog.Feast!=null&&p.Dog.Feast.Catch>-50&&p.Dog.Feast.Snap<-50&&!p.DogPopped);
        /// <summary>Whether pilot `id` is caught in someone's feast.</summary>
        private bool DogHeld(NativePilot q)=>q.DogHolder!=null;
        private float DogScale(NativePilot p)=>NativeAbilityRules.Scale(p.Stats,false);
        private void DogMotion(NativePilot p,ref Vector3 v)
        {
            if(p.Seat.spec!=NativeTopDog.Spec)return;var k=p.Dog;
            if(DogRooted(p)){v.x=v.z=0;p.Knockback=p.MoveVelocity=Vector3.zero;return;}
            if(k.CastKind==NativeTopDog.Zip&&Time>=k.CastRelease&&Time<k.CastRelease+NativeTopDog.ZipTime+Rules.Dt){v.x=v.z=0;p.MoveVelocity=Vector3.zero;return;}
            if(Time>=k.CastUntil||k.CastKind==null)return;
            if(k.CastKind==NativeTopDog.Mustard){v.x*=.5f;v.z*=.5f;}
            else if(k.CastKind==NativeTopDog.Mallet){float s=k.CastStep==2&&Time<k.CastRelease+.15f?0:.55f;v.x*=s;v.z*=s;}
            else if(k.CastKind!=NativeTopDog.Boing){v.x*=.7f;v.z*=.7f;}
        }
        private void BotDog(NativePilot p,ref InputFrame i,Vector3 target,float distance,bool visible)
        {
            if(!DogAllowed(p))return;i.Shoot=i.Attack=visible&&distance<3.2f;if(DogBusy(p))return;var k=p.Dog;
            bool Ready(int s)=>Time>=k.Ready[s]&&(s!=3||k.Peels>0);
            if(k.Meter>=1&&visible&&distance<14)i.SirenAbility=6;
            else if(visible&&distance<3&&Ready(4))i.SirenAbility=3;
            else if(visible&&distance>5&&distance<12&&Ready(5))i.SirenAbility=4;
            else if(visible&&distance>4&&distance<20&&Ready(2))i.SirenAbility=1;
            else if(visible&&distance>6&&distance<24&&Ready(6))i.SirenAbility=5;
            else if(distance>6&&distance<12&&Ready(1))i.AbilityE=true;
            else if(visible&&distance<8&&Ready(3))i.SirenAbility=2;
            else if(distance>14&&Ready(0))i.AbilityQ=true;
            if(i.SirenAbility>0||i.AbilityQ||i.AbilityE)i.Shoot=i.Attack=i.Roll=false;
        }
        private void StepDogInput(NativePilot p)
        {
            if(!DogAllowed(p)||p.Hp<=0||DogBusy(p)||Incapacitated(p)||Time<p.RollEnd||Time<p.BlockEnd||Time<p.Ready||HubInside(p.Body.position))return;
            var k=p.Dog;if(k.Ready==null||k.Ready.Length!=NativeTopDog.Slots)k.Ready=new float[NativeTopDog.Slots];string kind=null;int slot=-1;
            if(p.Input.SirenAbility==6&&k.Meter>=1&&k.Feast==null)kind=NativeTopDog.Feast;
            else
            {
                slot=p.Input.AbilityQ?0:p.Input.AbilityE?1:p.Input.SirenAbility>=1&&p.Input.SirenAbility<=5?p.Input.SirenAbility+1:-1;
                if(slot>=0&&Time>=k.Ready[slot]&&(slot!=3||k.Peels>0))kind=DogKinds[slot];else slot=-1;
            }
            if(kind==null&&(p.Input.Shoot||p.Input.Attack))kind=NativeTopDog.Mallet;
            if(kind==null)return;
            float rate=Math.Max(.5f,(float)p.Stats["attackRate"]);p.DogReleased=false;k.CastKind=kind;k.CastAt=Time;k.CastStep=0;
            p.DogAim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)).normalized;p.DogFacing=Vec(V3.Forward(p.Input.Yaw));p.Yaw=p.Input.Yaw;p.AttackAt=p.LastAttack=Time;
            switch(kind)
            {
                case NativeTopDog.Mallet:
                {
                    int step=Time<=k.ComboUntil?(k.Combo+1)%3:0;k.Combo=k.CastStep=step;
                    k.CastRelease=Time+NativeTopDog.SwingHit[step]/rate;k.CastUntil=Time+NativeTopDog.SwingLength[step]/rate;k.ComboUntil=k.CastUntil+NativeTopDog.ComboWindow;break;
                }
                case NativeTopDog.Boing:k.CastRelease=Time+NativeTopDog.BoingRelease;k.CastUntil=k.CastRelease+.18f;break;
                case NativeTopDog.Zip:k.CastRelease=Time+NativeTopDog.ZipWind;k.CastUntil=k.CastRelease+NativeTopDog.ZipTime+.12f;break;
                case NativeTopDog.Anvil:k.CastRelease=Time+NativeTopDog.AnvilRelease;k.CastUntil=k.CastRelease+NativeTopDog.CastRecover;p.DogPoint=GroundPoint(AimedAbilityPoint(p,NativeTopDog.AnvilReach));break;
                case NativeTopDog.Peel:k.CastRelease=Time+NativeTopDog.PeelRelease;k.CastUntil=k.CastRelease+NativeTopDog.CastRecover;p.DogPoint=GroundPoint(AimedAbilityPoint(p,NativeTopDog.PeelReach));break;
                case NativeTopDog.Mustard:k.CastRelease=Time+NativeTopDog.MustardTick;k.CastUntil=k.CastRelease+NativeTopDog.MustardTime;break;
                case NativeTopDog.Glove:k.CastRelease=Time+NativeTopDog.GloveRelease;k.CastUntil=k.CastRelease+NativeTopDog.GloveOut+NativeTopDog.GloveBack+.1f;break;
                case NativeTopDog.Pie:k.CastRelease=Time+NativeTopDog.PieRelease;k.CastUntil=k.CastRelease+NativeTopDog.CastRecover;break;
                case NativeTopDog.Feast:
                {
                    k.Meter=0;p.DogPopped=false;p.DogFed=false;p.DogTaken=0;var hand=p.Body.position+Vector3.up*.7f;
                    k.Feast=new NativeDogFeast{At=Time,Throw=Time+NativeTopDog.Twirl,Until=Time+NativeTopDog.Twirl+NativeTopDog.LassoReach/NativeTopDog.LassoSpeed+NativeTopDog.After,Dir=Vec(p.DogFacing),From=Vec(hand),Lasso=Vec(hand),LassoAt=Time};
                    k.CastRelease=k.Feast.Throw;k.CastUntil=k.Feast.Until;break;
                }
            }
            if(slot>=0)
            {
                if(slot==3){k.Peels--;if(p.DogPeelReady<=0)p.DogPeelReady=Time+NativeTopDog.Cooldown[3]*(float)p.Stats["abilityCooldown"];k.Ready[3]=k.Peels>0?0:p.DogPeelReady;}
                else k.Ready[slot]=Time+NativeTopDog.Cooldown[slot]*(float)p.Stats["abilityCooldown"];
            }
            p.Ready=kind==NativeTopDog.Mallet?k.CastUntil-.04f:k.CastUntil;
            Emit(EventKind.AbilityCast,p,0,p.Body.position,0,kind);
        }

        // ---- who can be hit, and hitting them ----
        private IEnumerable<NativePilot> DogPilots(NativePilot p){foreach(var q in Pilots.ToArray())if(q!=p&&q.Hp>0&&q.Seat.connected&&CanHit(p,q)&&!HubInside(q.Body.position))yield return q;}
        private IEnumerable<NativeMinion> DogMinions(NativePilot p){foreach(var m in Minions.ToArray())if(m.Hp>0&&CanHit(p,m)&&!HubInside(m.Body.position))yield return m;}
        private void DogMeter(NativePilot p,float share){if(p.Hp>0&&DogAllowed(p)&&p.Dog.Feast==null)p.Dog.Meter=Math.Min(1,p.Dog.Meter+share);}
        private float DogHurt(NativePilot p,NativePilot q,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
        {
            float actual=Damage(q,p,amount*(melee?(float)p.Stats["damage"]:DogScale(p)),dir,impulse,false,primary:melee,melee:melee,weaponHit:melee,damageKind:"topdog-"+kind,hitPoint:q.Body.position+Vector3.up*.3f);
            if(actual>0)DogMeter(p,melee?NativeTopDog.MeterMallet:NativeTopDog.MeterAbility);return actual;
        }
        private float DogHurt(NativePilot p,NativeMinion m,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
        {
            float actual=HitMinion(m,p,amount*(melee?(float)p.Stats["damage"]:DogScale(p)),dir,impulse,m.Body.position,"topdog-"+kind);
            if(actual>0)DogMeter(p,melee?NativeTopDog.MeterMallet:NativeTopDog.MeterAbility);return actual;
        }
        private void DogPush(NativeMinion m,Vector3 horizontal,float lift)
        {
            if(m.Hp<=0||m.Body.isKinematic||IsZoneBoss(m))return;var v=m.Body.linearVelocity;v.y=Math.Max(v.y,lift);m.Body.linearVelocity=v;m.BombPush=new Vector3(horizontal.x,0,horizontal.z);m.BombPushUntil=Time+.5f;
        }
        private void DogMark(NativePilot p,int id,string kind,float seconds)
        {
            var m=p.DogMarks.Find(x=>x.Id==id&&x.Kind==kind);if(m==null){if(p.DogMarks.Count>=NativeTopDog.MaxMarks)p.DogMarks.RemoveAt(0);p.DogMarks.Add(m=new NativeDogMark{Id=id,Kind=kind,At=Time});}
            m.Until=Math.Max(m.Until,Time+seconds);
        }
        private void DogHit(NativePilot p,string kind,Vector3 at,Vector3 dir,int victim=0)
        {
            if(p.DogHits.Count>=NativeTopDog.MaxHits)p.DogHits.RemoveAt(0);
            p.DogHits.Add(new NativeDogHit{Id=nextEvent++,Kind=kind,At=Time,Pos=Vec(at),Dir=Vec(dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.up),Victim=victim});
        }
        private NativeDogProp DogProp(NativePilot p,string kind,Vector3 pos,float landAt,float until,out DogFlight flight)
        {
            flight=null;if(p.DogFlights.Count>=NativeTopDog.MaxProps){var old=p.DogFlights.FirstOrDefault(f=>f.Record.Kind==NativeTopDog.PropPuddle)??p.DogFlights[0];p.DogFlights.Remove(old);}
            var r=new NativeDogProp{Id=nextBullet++,Kind=kind,Pos=Vec(pos),At=Time,SampleAt=Time,LandAt=landAt,Until=until,Dir=Vec(p.DogFacing)};flight=new DogFlight{Record=r};p.DogFlights.Add(flight);return r;
        }
        /// <summary>Flatten (the slam, the anvil): stunned where they stand for `seconds`, their mark for the squash.</summary>
        private void DogPancake(NativePilot p,NativePilot q,float seconds)
        {if(q.Hp<=0||SirenArmor(q)||Pve?.Boss==q)return;StunMelee(q,seconds);q.Knockback=Vector3.zero;DogMark(p,q.Seat.id,NativeTopDog.Pancake,seconds);}
        private void DogDaze(NativePilot p,NativePilot q,float seconds)
        {if(q.Hp<=0||SirenArmor(q)||Pve?.Boss==q)return;q.StunEnd=Math.Max(q.StunEnd,Time+seconds);DogMark(p,q.Seat.id,NativeTopDog.Dazed,seconds+.6f);}
        private static bool DogCone(Vector3 from,Vector3 facing,Vector3 to,float reach,float halfArc,float extra=.45f)
        {
            var d=to-from;if(Math.Abs(d.y)>1.6f+extra)return false;var flat=new Vector3(d.x,0,d.z);float l=flat.magnitude;if(l>reach+extra)return false;if(l<.6f)return true;
            return Vector3.Angle(new Vector3(facing.x,0,facing.z),flat)*Mathf.Deg2Rad<=halfArc+Mathf.Atan2(extra,l);
        }

        // ---- the release of each cast ----
        private void DogRelease(NativePilot p)
        {
            var k=p.Dog;var feet=p.Body.position-Vector3.up*.865f;var hand=p.Body.position+Vector3.up*.45f+p.DogFacing*.35f;
            switch(k.CastKind)
            {
                case NativeTopDog.Mallet:DogSwing(p);break;
                case NativeTopDog.Boing:
                {
                    DogProp(p,NativeTopDog.PropSpring,GroundPoint(feet),Time,Time+.9f,out _);DogHit(p,NativeTopDog.HitBoing,feet,Vector3.up);
                    BombLaunch(p,p,p.DogFacing*NativeTopDog.BoingAlong,Mathf.Sqrt(2*23*NativeTopDog.BoingHeight),0);p.DogAirborne=true;p.DogAirAt=Time;break;
                }
                case NativeTopDog.Zip:
                {
                    var from=p.Body.position;var dir=p.DogFacing;float reach=NativeTopDog.ZipReach;
                    var hit=SweepNative(from+Vector3.up*.2f,dir*reach,.35f,c=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger);
                    if(hit.HasValue)reach=Math.Max(0,hit.Value.Distance-.45f);
                    p.DogZipFrom=from;p.DogZipTo=from+dir*reach;p.DogZipHit.Clear();
                    var r=DogProp(p,NativeTopDog.PropZip,GroundPoint(from),Time,Time+NativeTopDog.ZipTime+.9f,out _);r.To=Vec(GroundPoint(p.DogZipTo));DogHit(p,NativeTopDog.Zip,from,dir);break;
                }
                case NativeTopDog.Anvil:
                {
                    float land=Time+NativeTopDog.AnvilFall;DogProp(p,NativeTopDog.PropAnvil,p.DogPoint,land,land+NativeTopDog.AnvilStays,out var f);f.Tick=land;break;
                }
                case NativeTopDog.Peel:
                {
                    float t=NativeTopDog.PeelFlight;var r=DogProp(p,NativeTopDog.PropPeel,hand,Time+t,Time+t+NativeTopDog.PeelLife,out var f);
                    r.Vel=Vec((p.DogPoint-hand)/t+Vector3.up*(23*t/2));r.To=Vec(p.DogPoint);
                    var peels=p.DogFlights.Where(x=>x.Record.Kind==NativeTopDog.PropPeel).ToList();while(peels.Count>NativeTopDog.MaxPeels){p.DogFlights.Remove(peels[0]);peels.RemoveAt(0);}
                    break;
                }
                case NativeTopDog.Mustard:
                {
                    var r=DogProp(p,NativeTopDog.PropSquirt,hand,Time,k.CastUntil,out _);r.Dir=Vec(p.DogAim);r.Size=NativeTopDog.MustardReach;p.DogTick=Time;p.DogPuddleTick=Time+.15f;break;
                }
                case NativeTopDog.Glove:
                {
                    var aim=p.DogAim;float reach=NativeTopDog.GloveReach;int victim=0;bool minion=false;
                    var hit=SweepNative(hand,aim*reach,NativeTopDog.GloveRadius,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):!c.isTrigger));
                    if(hit.HasValue){reach=Math.Max(.3f,hit.Value.Distance);var q=hit.Value.Collider.GetComponent<NativePilotBody>()?.Pilot;var m=hit.Value.Collider.GetComponent<NativeMinionBody>()?.Minion;if(q!=null)victim=q.Seat.id;else if(m!=null){victim=m.Id;minion=true;}}
                    float land=Time+NativeTopDog.GloveOut*Math.Max(.25f,reach/NativeTopDog.GloveReach);
                    var r=DogProp(p,NativeTopDog.PropGlove,hand,land,land+NativeTopDog.GloveBack,out var f);r.Dir=Vec(aim);r.Size=reach;r.Hit=victim;f.Target=victim;f.TargetMinion=minion;f.Tick=land;
                    if(!hit.HasValue||victim==0)f.Target=-1;break;
                }
                case NativeTopDog.Pie:
                {
                    var r=DogProp(p,NativeTopDog.PropPie,hand,-100,Time+NativeTopDog.PieMaxFlight,out var f);r.Vel=Vec(p.DogAim*NativeTopDog.PieSpeed+Vector3.up*1.5f);break;
                }
            }
        }
        private void DogSwing(NativePilot p)
        {
            var k=p.Dog;int step=Math.Max(0,Math.Min(2,k.CastStep));var at=p.Body.position;var f=p.DogFacing;
            var slam=GroundPoint(at+f*NativeTopDog.SlamAhead);float damage=NativeTopDog.SwingDamage[step];int first=0;
            bool Inside(Vector3 q)
            {
                if(step==2){var s=q-slam;if(new Vector2(s.x,s.z).magnitude<=NativeTopDog.SlamRadius+.35f&&s.y>-.6f&&s.y<2.4f)return true;}
                return DogCone(at,f,q,NativeTopDog.SwingReach[step],NativeTopDog.SwingArc[step]);
            }
            foreach(var q in DogPilots(p))
            {
                if(!Inside(q.Body.position)||!MeleeVisible(at+Vector3.up*.3f,q.Body.position,p,q.Collider))continue;
                float actual=DogHurt(p,q,damage,q.Body.position-at,NativeTopDog.Mallet,step==2?0:(float)p.Weapon.impulse,true);if(actual<=0)continue;if(first==0)first=q.Seat.id;
                if(step==2)DogPancake(p,q,NativeTopDog.PancakeSeconds);else StaggerMelee(q,NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"]));
                DogHit(p,step==2?NativeTopDog.HitSlam:NativeTopDog.HitBonk,q.Body.position+Vector3.up*.55f,q.Body.position-at,q.Seat.id);
            }
            foreach(var m in DogMinions(p))
            {
                if(!Inside(m.Body.position)||!MeleeVisible(at+Vector3.up*.3f,m.Body.position,p,m.Collider))continue;
                if(DogHurt(p,m,damage,m.Body.position-at,NativeTopDog.Mallet,0,true)<=0)continue;if(first==0)first=m.Id;
                if(step!=2)DogPush(m,(m.Body.position-at).normalized*NativeTopDog.SwingPush,1.5f);else SlowMinion(m,.4f,NativeTopDog.PancakeSeconds);
                DogHit(p,step==2?NativeTopDog.HitSlam:NativeTopDog.HitBonk,m.Body.position+Vector3.up*.55f,m.Body.position-at,m.Id);
            }
            // The slam always lands: its ring and the floor's crack, hit or miss.
            if(step==2)DogHit(p,NativeTopDog.HitClang,slam,Vector3.up,first);
        }

        // ---- the world: flights, the channel, the dash, the feast ----
        private void StepDogWorld()
        {
            foreach(var p in Pilots)
            {
                if(p.Seat.spec!=NativeTopDog.Spec)continue;var k=p.Dog;if(k.Ready==null||k.Ready.Length!=NativeTopDog.Slots)k.Ready=new float[NativeTopDog.Slots];
                while(p.DogPeelReady>0&&Time>=p.DogPeelReady){k.Peels=Math.Min(NativeTopDog.PeelCharges,k.Peels+1);p.DogPeelReady=k.Peels<NativeTopDog.PeelCharges?p.DogPeelReady+NativeTopDog.Cooldown[3]*(float)p.Stats["abilityCooldown"]:0;k.Ready[3]=0;}
                if(p.Hp>0&&p.Seat.connected&&DogAllowed(p)&&k.Feast==null)k.Meter=Math.Min(1,k.Meter+NativeTopDog.MeterPerSecond/60);
                bool able=p.Hp>0&&p.Seat.connected&&!Incapacitated(p);
                // A cast cut short (stunned, knocked down, killed) ends; a feast keeps its own clock.
                if(k.CastKind!=null&&k.CastKind!=NativeTopDog.Feast&&Time<k.CastUntil&&!able){k.CastUntil=Time;if(k.CastKind==NativeTopDog.Mustard)foreach(var f in p.DogFlights)if(f.Record.Kind==NativeTopDog.PropSquirt)f.Record.Until=Time;}
                if(!p.DogReleased&&k.CastKind!=null&&k.CastKind!=NativeTopDog.Feast&&Time>=k.CastRelease){p.DogReleased=true;if(able&&Time<=k.CastUntil+.001f)DogRelease(p);}
                if(able)DogChannel(p);
                // The boing's landing.
                if(p.DogAirborne&&Time-p.DogAirAt>.3f&&(p.Grounded||Time-p.DogAirAt>4)){p.DogAirborne=false;if(p.Hp>0&&p.Grounded)DogThud(p);}
                DogFlightsStep(p);
                if(k.Feast!=null)DogFeastStep(p);
                k.Props=p.DogFlights.Select(f=>f.Record).ToArray();
                p.DogHits.RemoveAll(h=>Time-h.At>1.5f);k.Hits=p.DogHits.ToArray();
                p.DogMarks.RemoveAll(m=>Time>=m.Until);k.Marks=p.DogMarks.ToArray();
            }
        }
        private void DogChannel(NativePilot p)
        {
            var k=p.Dog;
            if(k.CastKind==NativeTopDog.Mustard&&Time>=k.CastRelease&&Time<k.CastUntil)
            {
                var nozzle=p.Body.position+Vector3.up*.45f+p.DogFacing*.35f;var aim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)).normalized;p.DogAim=aim;p.DogFacing=Vec(V3.Forward(p.Input.Yaw));p.Yaw=p.Input.Yaw;
                foreach(var f in p.DogFlights)if(f.Record.Kind==NativeTopDog.PropSquirt){f.Record.Pos=Vec(nozzle);f.Record.Dir=Vec(aim);f.Record.SampleAt=Time;}
                if(Time>=p.DogTick)
                {
                    p.DogTick+=NativeTopDog.MustardTick;
                    foreach(var q in DogPilots(p))
                    {
                        var d=q.Body.position-nozzle;if(d.magnitude>NativeTopDog.MustardReach+.4f||Vector3.Angle(aim,d)*Mathf.Deg2Rad>NativeTopDog.MustardArc+Mathf.Atan2(.45f,Math.Max(.5f,d.magnitude))||!MeleeVisible(nozzle,q.Body.position,p,q.Collider))continue;
                        if(DogHurt(p,q,NativeTopDog.MustardDamage,d,NativeTopDog.Mustard)>0){Slow(q,NativeTopDog.MustardSlow,NativeTopDog.MustardSlowFor);DogMark(p,q.Seat.id,NativeTopDog.Mustarded,NativeTopDog.MustardSlowFor);}
                    }
                    foreach(var m in DogMinions(p))
                    {
                        var d=m.Body.position-nozzle;if(d.magnitude>NativeTopDog.MustardReach+.4f||Vector3.Angle(aim,d)*Mathf.Deg2Rad>NativeTopDog.MustardArc+.1f||!MeleeVisible(nozzle,m.Body.position,p,m.Collider))continue;
                        if(DogHurt(p,m,NativeTopDog.MustardDamage,d,NativeTopDog.Mustard)>0)SlowMinion(m,NativeTopDog.MustardSlow,NativeTopDog.MustardSlowFor);
                    }
                }
                if(Time>=p.DogPuddleTick)
                {
                    p.DogPuddleTick+=NativeTopDog.PuddleEvery;
                    var end=root.scene.GetPhysicsScene().Raycast(nozzle,aim,out var hit,NativeTopDog.MustardReach,WorldMask)?hit.point:GroundPoint(nozzle+aim*NativeTopDog.MustardReach);
                    var spot=GroundPoint(end+Vector3.up*.3f);if(!HubInside(spot)){var r=DogProp(p,NativeTopDog.PropPuddle,spot,Time,Time+NativeTopDog.PuddleLife,out _);r.Size=NativeTopDog.PuddleRadius;}
                }
            }
            // The dash: carried along its path, swatting what it passes.
            if(k.CastKind==NativeTopDog.Zip&&Time>=k.CastRelease&&Time<=k.CastRelease+NativeTopDog.ZipTime+Rules.Dt)
            {
                float u=Mathf.Clamp01((Time-k.CastRelease)/NativeTopDog.ZipTime);var to=Vector3.Lerp(p.DogZipFrom,p.DogZipTo,1-(1-u)*(1-u));p.Body.position=KernelTravel(p.Body.position,to);p.MoveVelocity=Vector3.zero;
                foreach(var q in DogPilots(p))
                {
                    if(p.DogZipHit.Contains(q.Seat.id))continue;var d=q.Body.position-p.Body.position;if(new Vector2(d.x,d.z).magnitude>NativeTopDog.ZipWidth||Math.Abs(d.y)>1.8f)continue;
                    p.DogZipHit.Add(q.Seat.id);var side=Vector3.Cross(Vector3.up,p.DogFacing);if(Vector3.Dot(side,d)<0)side=-side;
                    if(DogHurt(p,q,NativeTopDog.ZipDamage,d,NativeTopDog.Zip)>0){BombLaunch(q,p,side*NativeTopDog.ZipPush+p.DogFacing*3,4,0);DogHit(p,NativeTopDog.HitSwat,q.Body.position+Vector3.up*.5f,side,q.Seat.id);}
                }
                foreach(var m in DogMinions(p))
                {
                    if(p.DogZipHit.Contains(m.Id))continue;var d=m.Body.position-p.Body.position;if(new Vector2(d.x,d.z).magnitude>NativeTopDog.ZipWidth||Math.Abs(d.y)>1.8f)continue;
                    p.DogZipHit.Add(m.Id);var side=Vector3.Cross(Vector3.up,p.DogFacing);if(Vector3.Dot(side,d)<0)side=-side;
                    if(DogHurt(p,m,NativeTopDog.ZipDamage,d,NativeTopDog.Zip)>0){DogPush(m,side*NativeTopDog.ZipPush,4);DogHit(p,NativeTopDog.HitSwat,m.Body.position+Vector3.up*.5f,side,m.Id);}
                }
            }
        }
        private void DogThud(NativePilot p)
        {
            var feet=p.Body.position-Vector3.up*.865f;DogHit(p,NativeTopDog.HitThud,feet,Vector3.up);
            foreach(var q in DogPilots(p)){var d=q.Body.position-p.Body.position;if(d.magnitude>NativeTopDog.ThudRadius||!MeleeVisible(feet+Vector3.up*.2f,q.Body.position,p,q.Collider))continue;if(DogHurt(p,q,NativeTopDog.ThudDamage,d,NativeTopDog.Boing)>0)BombLaunch(q,p,new Vector3(d.x,0,d.z).normalized*3,NativeTopDog.ThudLift,0);}
            foreach(var m in DogMinions(p)){var d=m.Body.position-p.Body.position;if(d.magnitude>NativeTopDog.ThudRadius)continue;if(DogHurt(p,m,NativeTopDog.ThudDamage,d,NativeTopDog.Boing)>0)DogPush(m,new Vector3(d.x,0,d.z).normalized*3,NativeTopDog.ThudLift);}
        }
        private void DogFlightsStep(NativePilot p)
        {
            foreach(var f in p.DogFlights.ToArray())
            {
                var r=f.Record;if(Time>=r.Until){p.DogFlights.Remove(f);continue;}
                switch(r.Kind)
                {
                    case NativeTopDog.PropAnvil:
                        if(!f.Done&&Time>=r.LandAt)
                        {
                            f.Done=true;var at=Vec(r.Pos);DogHit(p,NativeTopDog.HitAnvil,at,Vector3.up);
                            foreach(var q in DogPilots(p)){var d=q.Body.position-at;if(new Vector2(d.x,d.z).magnitude>NativeTopDog.AnvilRadius+.35f||d.y<-.5f||d.y>2.6f)continue;if(DogHurt(p,q,NativeTopDog.AnvilDamage,Vector3.down,NativeTopDog.Anvil)>0){DogPancake(p,q,NativeTopDog.AnvilPancake);r.Hit=q.Seat.id;}}
                            foreach(var m in DogMinions(p)){var d=m.Body.position-at;if(new Vector2(d.x,d.z).magnitude>NativeTopDog.AnvilRadius+.3f||d.y<-.5f||d.y>2.6f)continue;if(DogHurt(p,m,NativeTopDog.AnvilDamage,Vector3.down,NativeTopDog.Anvil)>0)SlowMinion(m,.4f,NativeTopDog.AnvilPancake);}
                        }
                        break;
                    case NativeTopDog.PropPeel:
                    {
                        if(!f.Landed&&Time>=r.LandAt){f.Landed=true;r.Pos=r.To;r.Vel=default;r.SampleAt=Time;}
                        if(!f.Landed)break;var at=Vec(r.Pos);
                        NativePilot slipped=DogPilots(p).FirstOrDefault(q=>q.Grounded&&Vector3.Distance(q.Body.position-Vector3.up*.865f,at)<=NativeTopDog.PeelTouch&&!Invulnerable(q));
                        if(slipped!=null)
                        {
                            var along=slipped.Body.linearVelocity;along.y=0;if(along.sqrMagnitude<.5f)along=Vec(V3.Forward(slipped.Yaw))*2;
                            DogHurt(p,slipped,NativeTopDog.PeelDamage,along,NativeTopDog.Peel);
                            if(slipped.Hp>0&&!SirenArmor(slipped)&&Pve?.Boss!=slipped){StunMelee(slipped,.24f);slipped.Down=new NativeRecovery{At=Time,Until=Time+5,PickupUsed=true,Source=p.Seat.id};BombLaunch(slipped,p,along.normalized*6,5,0);DogMark(p,slipped.Seat.id,NativeTopDog.Slipped,NativeTopDog.PeelDown+.6f);}
                            DogHit(p,NativeTopDog.HitSlip,at,along,slipped.Seat.id);p.DogFlights.Remove(f);break;
                        }
                        var minion=DogMinions(p).FirstOrDefault(m=>Vector3.Distance(m.Body.position-Vector3.up*m.Collider.height*.5f,at)<=NativeTopDog.PeelTouch+.2f);
                        if(minion!=null){DogHurt(p,minion,NativeTopDog.PeelDamage,Vector3.up,NativeTopDog.Peel);DogPush(minion,Vector3.zero,5);SlowMinion(minion,.4f,NativeTopDog.PeelDown);DogHit(p,NativeTopDog.HitSlip,at,Vector3.up,minion.Id);p.DogFlights.Remove(f);}
                        break;
                    }
                    case NativeTopDog.PropGlove:
                        if(!f.Done&&Time>=f.Tick)
                        {
                            f.Done=true;var tip=Vec(r.Pos)+Vec(r.Dir)*r.Size;
                            if(f.Target>0&&!f.TargetMinion){var q=Pilots.Find(x=>x.Seat.id==f.Target);if(q!=null&&q.Hp>0&&CanHit(p,q)&&DogHurt(p,q,NativeTopDog.GloveDamage,Vec(r.Dir),NativeTopDog.Glove,0,true)>0){BombLaunch(q,p,Vec(r.Dir)*NativeTopDog.GlovePush,NativeTopDog.GloveLift,0);DogDaze(p,q,NativeTopDog.GloveDaze);DogHit(p,NativeTopDog.HitPow,q.Body.position+Vector3.up*.4f,Vec(r.Dir),q.Seat.id);}else DogHit(p,NativeTopDog.HitBoing,tip,-Vec(r.Dir));}
                            else if(f.Target>0){var m=Minions.Find(x=>x.Id==f.Target);if(m!=null&&m.Hp>0&&DogHurt(p,m,NativeTopDog.GloveDamage,Vec(r.Dir),NativeTopDog.Glove,0,true)>0){DogPush(m,Vec(r.Dir)*NativeTopDog.GlovePush,NativeTopDog.GloveLift);DogHit(p,NativeTopDog.HitPow,m.Body.position+Vector3.up*.4f,Vec(r.Dir),m.Id);}}
                            else if(f.Target<0&&r.Size<NativeTopDog.GloveReach-.05f)DogHit(p,NativeTopDog.HitBoing,tip,-Vec(r.Dir));
                        }
                        break;
                    case NativeTopDog.PropPie:
                    {
                        if(f.Done)break;var from=Vec(r.Pos);var vel=Vec(r.Vel);vel.y-=NativeTopDog.PieGravity/60f;var delta=vel/60;
                        var hit=SweepNative(from,delta,NativeTopDog.PieRadius,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q):c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):!c.isTrigger));
                        if(hit.HasValue)
                        {
                            var h=hit.Value;var at=from+delta.normalized*Math.Max(0,h.Distance-.02f);f.Done=true;r.Pos=Vec(at);r.Vel=default;r.LandAt=Time;r.Until=Time+3;r.Dir=Vec(h.Normal);
                            var q=h.Collider.GetComponent<NativePilotBody>()?.Pilot;var m=h.Collider.GetComponent<NativeMinionBody>()?.Minion;
                            if(q!=null){r.Hit=q.Seat.id;if(DogHurt(p,q,NativeTopDog.PieDamage,delta,NativeTopDog.Pie)>0)DogMark(p,q.Seat.id,NativeTopDog.Pied,NativeTopDog.PieFor);}
                            else if(m!=null){r.Hit=m.Id;DogHurt(p,m,NativeTopDog.PieDamage,delta,NativeTopDog.Pie);}
                            foreach(var o in DogPilots(p))if(o!=q&&Vector3.Distance(o.Body.position,at)<=NativeTopDog.PieSplash){if(DogHurt(p,o,NativeTopDog.PieSplashDamage,o.Body.position-at,NativeTopDog.Pie)>0)DogMark(p,o.Seat.id,NativeTopDog.Pied,NativeTopDog.PieFor*.45f);}
                            DogHit(p,NativeTopDog.HitPie,at,-delta,r.Hit);
                        }
                        else {r.Pos=Vec(from+delta);r.Vel=Vec(vel);}
                        r.SampleAt=Time;break;
                    }
                    case NativeTopDog.PropPuddle:
                    {
                        var at=Vec(r.Pos);foreach(var q in DogPilots(p))if(q.Grounded&&new Vector2(q.Body.position.x-at.x,q.Body.position.z-at.z).magnitude<=r.Size&&Math.Abs(q.Body.position.y-.865f-at.y)<.6f)Slow(q,NativeTopDog.PuddleSlow,.25f);
                        break;
                    }
                }
            }
        }

        // ---- All You Can Eat ----
        private void DogFeastStep(NativePilot p)
        {
            var k=p.Dog;var f=k.Feast;
            // The lasso's flight.
            if(f.Catch<-50&&!f.Miss&&Time>=f.Throw)
            {
                if(f.LassoAt<f.Throw){p.DogAim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)).normalized;p.DogFacing=Vec(V3.Forward(p.Input.Yaw));p.Yaw=p.Input.Yaw;f.Lasso=Vec(p.Body.position+Vector3.up*.75f+p.DogFacing*.4f);f.LassoVel=Vec(p.DogAim*NativeTopDog.LassoSpeed);f.LassoAt=Time;f.From=f.Lasso;DogHit(p,NativeTopDog.Feast,Vec(f.Lasso),p.DogAim);}
                var from=Vec(f.Lasso);var delta=Vec(f.LassoVel)/60;
                var hit=SweepNative(from,delta,NativeTopDog.LassoRadius,c=>c!=p.Collider&&(c.GetComponent<NativePilotBody>()?.Pilot is NativePilot q?q.Hp>0&&CanHit(p,q)&&!Invulnerable(q)&&q.DogHolder==null:c.GetComponent<NativeMinionBody>()?.Minion is NativeMinion m?m.Hp>0&&CanHit(p,m):!c.isTrigger&&c.gameObject.layer!=ActorLayer));
                if(hit.HasValue)
                {
                    var h=hit.Value;f.Lasso=Vec(from+delta.normalized*Math.Max(0,h.Distance));var q=h.Collider.GetComponent<NativePilotBody>()?.Pilot;var m=h.Collider.GetComponent<NativeMinionBody>()?.Minion;
                    if(q!=null&&Time<q.BlockEnd){BlockProc(q,p,q.Body.position,"clash");DogFeastMiss(p,true);}
                    else if(q!=null&&(q.Seat.AuthorityBoss||q.Seat.spec=="siren"||Pve?.Boss==q||SirenArmor(q))){DogHurt(p,q,NativeTopDog.BossDamage,Vec(f.LassoVel),NativeTopDog.Feast);DogFeastMiss(p,true);}
                    else if(q!=null)DogCatch(p,q,null);
                    else if(m!=null&&(IsZoneBoss(m)||m.Kind!=null&&m.Kind.StartsWith("fallen-",StringComparison.Ordinal))){DogHurt(p,m,NativeTopDog.BossDamage,Vec(f.LassoVel),NativeTopDog.Feast);DogFeastMiss(p,true);}
                    else if(m!=null)DogCatch(p,null,m);
                    else DogFeastMiss(p,false);
                }
                else
                {
                    f.Lasso=Vec(from+delta);
                    if(Vector3.Distance(Vec(f.Lasso),Vec(f.From))>=NativeTopDog.LassoReach||HubInside(Vec(f.Lasso)))DogFeastMiss(p,false);
                }
                f.LassoAt=Time;
            }
            if(f.Catch>-50&&f.Snap<-50&&!p.DogPopped)DogFeed(p);
            if(Time>=f.Until){k.Feast=null;DogFree(p);}
        }
        private void DogFeastMiss(NativePilot p,bool snapped)
        {
            var f=p.Dog.Feast;f.Miss=true;f.Until=Time+NativeTopDog.After;p.Dog.CastUntil=f.Until;f.LassoVel=default;
            p.Dog.Meter=Math.Min(1,p.Dog.Meter+NativeTopDog.MissRefund);DogHit(p,snapped?NativeTopDog.HitSnap:NativeTopDog.Feast,Vec(f.Lasso),Vector3.down);
        }
        private void DogCatch(NativePilot p,NativePilot q,NativeMinion m)
        {
            var f=p.Dog.Feast;f.Catch=Time;f.FeedAt=NativeTopDog.FeedAt(Time);f.Release=NativeTopDog.ReleaseAt(f.FeedAt);f.Pop=NativeTopDog.PopAt(f.Release);f.Until=f.Pop+NativeTopDog.After;f.Fed=0;f.LassoVel=default;
            var facing=p.DogFacing;f.Dir=Vec(facing);f.Hold=Vec(GroundPoint(p.Body.position+facing*NativeTopDog.HoldAhead+Vector3.up*.5f)+Vector3.up*.865f);p.Dog.CastUntil=f.Release+.6f;p.DogTaken=0;
            if(q!=null)
            {
                f.Victim=q.Seat.id;f.From=Vec(q.Body.position);q.DogHolder=p;
                CancelSiren(q);CancelFighter(q);CancelMelee(q);CancelSpecial(q);CancelAbilities(q);q.Roll=null;q.RollEnd=Time;q.BlockEnd=0;q.Down=null;q.Juggle=null;q.Aiming=false;
            }
            else {f.Victim=m.Id;f.From=Vec(m.Body.position);p.DogMinion=m;}
            DogHit(p,NativeTopDog.HitGulp,Vec(f.Lasso),Vector3.up,f.Victim);
        }
        /// <summary>Where the victim is at `time`: reeled in along a hop, held, then (let go) staggering back on a little hop.</summary>
        private Vector3 DogVictimAt(NativeDogFeast f,float time)
        {
            var hold=Vec(f.Hold);var dir=Vec(f.Dir);
            if(time<f.Catch+NativeTopDog.Reel){float u=Mathf.Clamp01((time-f.Catch)/NativeTopDog.Reel);float e=1-(1-u)*(1-u);return Vector3.Lerp(Vec(f.From),hold,e)+Vector3.up*(1.2f*4*e*(1-e));}
            if(time<f.Release)return hold;
            float s=Mathf.Clamp01((time-f.Release)/NativeTopDog.Stagger);float g=1-(1-s)*(1-s);return hold+dir*(NativeTopDog.PushBack*g)+Vector3.up*(.35f*4*s*(1-s));
        }
        /// <summary>From StepPilot: a caught pilot is held where the feast says, and does nothing else.</summary>
        private bool PinDogVictim(NativePilot q)
        {
            var p=q.DogHolder;var f=p?.Dog.Feast;if(p==null||f==null||f.Victim!=q.Seat.id||f.Catch<-50||f.Snap>-50||p.DogPopped){if(p!=null)q.DogHolder=null;q.Body.useGravity=true;return false;}
            var at=DogVictimAt(f,Time);q.Body.position=KernelTravel(q.Body.position,at);q.Body.linearVelocity=q.Knockback=q.MoveVelocity=Vector3.zero;q.Body.useGravity=false;
            var look=p.Body.position-q.Body.position;look.y=0;if(look.sqrMagnitude>1e-4f)q.Yaw=Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg;q.StunEnd=Math.Max(q.StunEnd,Time+.05f);
            return true;
        }
        private void DogFeed(NativePilot p)
        {
            var f=p.Dog.Feast;var q=p.DogMinion==null?Pilots.Find(x=>x.Seat.id==f.Victim):null;var m=p.DogMinion;
            if(q==null&&m==null||q!=null&&(q.Hp<=0||!q.Seat.connected)||m!=null&&m.Hp<=0){DogEndFeast(p);return;}
            if(q!=null)PinDogVictim(q);
            else {var at=DogVictimAt(f,Time)-Vector3.up*(.865f-m.Collider.height*.5f);m.Body.position=at;m.Body.linearVelocity=Vector3.zero;m.NextAttack=Math.Max(m.NextAttack,Time+.5f);m.StrikeAt=-1;}
            // The links snap: the Top Dog stopped (killed, stunned, knocked down) or hurt enough while feeding.
            if(Time<f.Release&&(p.Hp<=0||!p.Seat.connected||Incapacitated(p)||p.DogTaken>=NativeTopDog.SnapDamage))
            {
                f.Snap=Time;f.Until=Time+.8f;p.Dog.CastUntil=f.Until;var victimAt=q?.Body.position??m.Body.position;DogHit(p,NativeTopDog.HitSnap,victimAt+Vector3.up*.6f,Vec(f.Dir),f.Victim);
                if(q!=null){q.DogHolder=null;q.Body.useGravity=true;q.StunEnd=Math.Max(q.StunEnd,Time+.4f);if(f.Fed>0)Damage(q,p.Hp>0?p:null,NativeTopDog.SnapPerDog*f.Fed,Vector3.zero,0,false,forced:true,direct:false,primary:false,damageKind:"topdog-feast");}
                else if(f.Fed>0)HitMinion(m,p,NativeTopDog.SnapPerDog*f.Fed,Vector3.zero,0,m.Body.position,"topdog-feast");
                p.DogMinion=null;return;
            }
            // Each hot dog going down.
            while(f.Fed<NativeTopDog.Feeds&&Time>=NativeTopDog.DownAt(f.FeedAt,f.Fed))
            {
                f.Fed++;var mouth=(q?.Body.position??m.Body.position)+Vector3.up*.75f;DogHit(p,NativeTopDog.HitGulp,mouth,Vec(f.Dir),f.Victim);
                if(q!=null)Damage(q,p,NativeTopDog.FeedDamage,Vector3.zero,0,false,forced:true,direct:false,primary:false,damageKind:"topdog-feast",finalCap:Math.Max(0,q.Hp-1));
                else HitMinion(m,p,Math.Min(NativeTopDog.FeedDamage,Math.Max(0,m.Hp-1)),Vector3.zero,0,m.Body.position,"topdog-feast");
            }
            // Let go: a kick to send them reeling, stuffed; then the pop.
            if(!p.DogFed&&Time>=f.Release){p.DogFed=true;DogHit(p,NativeTopDog.HitPow,DogVictimAt(f,Time)+Vector3.up*.2f,Vec(f.Dir),f.Victim);if(q!=null)DogMark(p,q.Seat.id,NativeTopDog.Stuffed,f.Pop-Time);}
            if(Time>=f.Pop)DogPop(p,q,m);
        }
        private void DogPop(NativePilot p,NativePilot q,NativeMinion m)
        {
            var f=p.Dog.Feast;p.DogPopped=true;var at=q?.Body.position??m.Body.position;DogHit(p,NativeTopDog.HitPop,at+Vector3.up*.2f,Vector3.up,f.Victim);
            if(q!=null)
            {
                q.DogHolder=null;q.Body.useGravity=true;q.DogPopped=true;
                // An execution, as the Siren's crush: the victim's current health, so healing in the meantime cannot save them.
                Damage(q,p,q.Hp+1,Vector3.up,0,false,forced:true,direct:false,primary:false,damageKind:"topdog-pop",causeKind:NativeTopDog.Spec);
                q.DogPopped=false;
            }
            else if(m!=null)HitMinion(m,p,m.Hp+1000,Vector3.up,0,m.Body.position,"topdog-pop");
            p.DogMinion=null;
            // The burst: hot dog shrapnel for the Top Dog's other enemies near it.
            foreach(var o in DogPilots(p)){if(o==q)continue;var d=o.Body.position-at;if(d.magnitude>NativeTopDog.PopRadius)continue;if(DogHurt(p,o,NativeTopDog.PopDamage,d,NativeTopDog.Feast)>0)BombLaunch(o,p,new Vector3(d.x,0,d.z).normalized*NativeTopDog.PopPush,NativeTopDog.PopLift,0);}
            foreach(var o in DogMinions(p)){if(o==m)continue;var d=o.Body.position-at;if(d.magnitude>NativeTopDog.PopRadius)continue;if(DogHurt(p,o,NativeTopDog.PopDamage,d,NativeTopDog.Feast)>0)DogPush(o,new Vector3(d.x,0,d.z).normalized*NativeTopDog.PopPush,NativeTopDog.PopLift);}
        }
        private void DogEndFeast(NativePilot p){var f=p.Dog.Feast;if(f==null)return;f.Until=Math.Min(f.Until,Time+.5f);p.Dog.CastUntil=Math.Min(p.Dog.CastUntil,f.Until);DogFree(p);if(f.Catch>-50&&f.Snap<-50&&!p.DogPopped)f.Snap=Time;}
        private void DogFree(NativePilot p)
        {
            foreach(var q in Pilots)if(q.DogHolder==p){q.DogHolder=null;q.Body.useGravity=true;}
            p.DogMinion=null;
        }
        /// <summary>Damage the Top Dog took (Damage's tail): it counts toward snapping the links while it feeds.</summary>
        private void DogDamaged(NativePilot target,float actual)
        {var f=target.Dog?.Feast;if(target.Seat.spec==NativeTopDog.Spec&&f!=null&&f.Catch>-50&&f.Snap<-50&&Time<f.Release)target.DogTaken+=actual;}
        /// <summary>Clears a pilot's Top Dog state when it stops being one (a new round makes a new pilot).</summary>
        private void DogClear(NativePilot p){p.Dog=new NativeTopDogKit{Peels=NativeTopDog.PeelCharges,Ready=new float[NativeTopDog.Slots]};p.DogFlights.Clear();p.DogMarks.Clear();p.DogHits.Clear();DogFree(p);}
    }
}
