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
    // The held guard, the parry and the roll are any melee weapon's. Its ten chips (Core NativeGlaiveChips; owner 2026-11-09: "create
    // uniquely designed chips to benefit the glaive") change these rules here, each only for a Glaive holding it.
    internal sealed class GlaiveShard{public Vector3 Pos,Dir;public float Travel;public readonly HashSet<int> Struck=new HashSet<int>();}
    public sealed partial class NativePilot
    {
        public NativeGlaiveKit Glaive=new NativeGlaiveKit{Ready=new float[NativeGlaive.Slots]};
        internal readonly List<NativeGlaiveHit> GlaiveHits=new List<NativeGlaiveHit>();
        // Who this flight, flick, skim or return has cut (once each), and when the halo may cut each again.
        internal readonly HashSet<int> GlaiveStruck=new HashSet<int>();internal readonly Dictionary<int,float> GlaiveHaloNext=new Dictionary<int,float>();
        internal bool GlaiveReleased,GlaiveAttackHeld,GlaiveFromThrow;internal Vector3 GlaiveFacing,GlaiveAim,GlaivePos,GlaiveVel,GlaiveFrom,GlaiveTo;internal float GlaiveTravel;
        // The chips' (Core NativeGlaiveChips): when Twin Orbit's ring may cut each again, Shatterpoint's shards in flight, who bleeds from
        // Keen Edge and until when (and the next drip the effects show), Quick Catch's burst of speed.
        internal readonly Dictionary<int,float> GlaiveTwinNext=new Dictionary<int,float>(),GlaiveBleedUntil=new Dictionary<int,float>(),GlaiveBleedShow=new Dictionary<int,float>();
        internal readonly List<GlaiveShard> GlaiveShards=new List<GlaiveShard>();internal float GlaiveQuickUntil=-100;
    }
    public sealed partial class NativeArena
    {
        private bool GlaiveAllowed(NativePilot p)=>p.Seat.spec==NativeGlaive.Spec;
        private bool GlaiveBusy(NativePilot p)=>p.Seat.spec==NativeGlaive.Spec&&Time<p.Glaive.CastUntil;
        /// <summary>Where the ring is held and caught (the body's middle is its centre, .865 m over the feet).</summary>
        private static Vector3 GlaiveHand(NativePilot p)=>p.Body.position+Vector3.up*NativeGlaive.HandHeight;
        private float GlaiveScale(NativePilot p)=>NativeAbilityRules.Scale(p.Stats,false);
        private IEnumerable<NativePilot> GlaivePilots(NativePilot p){foreach(var q in Pilots.ToArray())if(q!=p&&q.Hp>0&&q.Seat.connected&&CanHit(p,q)&&!HubInside(q.Body.position))yield return q;}
        private IEnumerable<NativeMinion> GlaiveMinions(NativePilot p){foreach(var m in Minions.ToArray())if(m.Hp>0&&CanHit(p,m)&&!HubInside(m.Body.position))yield return m;}
        private float GlaiveHurt(NativePilot p,NativePilot q,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
        {
            float actual=Damage(q,p,amount*GlaiveKeen(p)*(melee?(float)p.Stats["damage"]:GlaiveScale(p)),dir,impulse,false,primary:melee,melee:melee,weaponHit:melee,damageKind:"glaive-"+kind,hitPoint:q.Body.position+Vector3.up*.3f);
            if(actual>0&&kind!=NativeGlaive.Burst)GlaiveBleed(p,q);return actual;
        }
        private float GlaiveHurt(NativePilot p,NativeMinion m,float amount,Vector3 dir,string kind,float impulse=0,bool melee=false)
        {
            float actual=HitMinion(m,p,amount*GlaiveKeen(p)*(melee?(float)p.Stats["damage"]:GlaiveScale(p)),dir,impulse,m.Body.position,"glaive-"+kind);
            if(actual>0&&kind!=NativeGlaive.Burst)GlaiveBleed(p,m);return actual;
        }
        private void GlaivePush(NativeMinion m,Vector3 horizontal,float lift)
        {
            if(m.Hp<=0||m.Body.isKinematic||IsZoneBoss(m))return;var v=m.Body.linearVelocity;v.y=Math.Max(v.y,lift);m.Body.linearVelocity=v;m.BombPush=new Vector3(horizontal.x,0,horizontal.z);m.BombPushUntil=Time+.4f;
        }
        private NativeGlaiveHit GlaiveHit(NativePilot p,string kind,Vector3 at,Vector3 dir,int victim=0)
        {
            if(p.GlaiveHits.Count>=NativeGlaive.MaxHits)p.GlaiveHits.RemoveAt(0);
            var h=new NativeGlaiveHit{Id=nextEvent++,Kind=kind,At=Time,Pos=Vec(at),Dir=Vec(dir.sqrMagnitude>1e-6f?dir.normalized:Vector3.up),Victim=victim};p.GlaiveHits.Add(h);return h;
        }

        // ---- the chips (Core NativeGlaiveChips) ----
        private int GlaiveStacks(NativePilot p,string id,int cap=1)=>p.Seat.spec==NativeGlaive.Spec&&Has(p,id)&&p.Seat.chips.TryGetValue(id,out int n)?Math.Min(cap,Math.Max(0,n)):0;
        private bool GlaiveChip(NativePilot p,string id)=>GlaiveStacks(p,id)>0;
        /// <summary>Keen Edge: every Glaive hit's share.</summary>
        private float GlaiveKeen(NativePilot p)=>1+NativeGlaiveChips.KeenDamage*GlaiveStacks(p,NativeGlaiveChips.KeenEdge,NativeGlaiveChips.KeenMax);
        /// <summary>Fourth Bounce: the throw's ricochets, how far it looks for the next and how much of the hit each keeps.</summary>
        private int GlaiveRicochets(NativePilot p)=>NativeGlaive.Ricochets+(GlaiveChip(p,NativeGlaiveChips.FourthBounce)?NativeGlaiveChips.BounceMore:0);
        private float GlaiveRicochetRange(NativePilot p)=>NativeGlaive.RicochetRange+(GlaiveChip(p,NativeGlaiveChips.FourthBounce)?NativeGlaiveChips.BounceLook:0);
        private float GlaiveRicochetScale(NativePilot p)=>GlaiveChip(p,NativeGlaiveChips.FourthBounce)?NativeGlaiveChips.BounceKeep:NativeGlaive.RicochetScale;
        /// <summary>Shatterpoint: the burst's radius.</summary>
        private float GlaiveBurstRadius(NativePilot p)=>NativeGlaive.BurstRadius*(GlaiveChip(p,NativeGlaiveChips.Shatterpoint)?NativeGlaiveChips.ShatterWide:1);
        private static bool GlaiveOut(NativeGlaiveKit k)=>k!=null&&(k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back||k.Disc==NativeGlaive.Orbit||k.Disc==NativeGlaive.Flick);
        /// <summary>Crescent Guard: the share of a blow a Glaive takes while its ring is out of the hand (thrown, coming back, circling, flicked).</summary>
        private float GlaiveGuardTaken(NativePilot target)=>target!=null&&target.Seat.spec==NativeGlaive.Spec&&GlaiveOut(target.Glaive)&&GlaiveChip(target,NativeGlaiveChips.CrescentGuard)?NativeGlaiveChips.GuardTaken:1;
        /// <summary>Quick Catch: the move speed's share just after a catch.</summary>
        private float GlaiveSpeed(NativePilot p)=>p.Seat.spec==NativeGlaive.Spec&&Time<p.GlaiveQuickUntil&&GlaiveChip(p,NativeGlaiveChips.QuickCatch)?NativeGlaiveChips.QuickSpeed:1;
        /// <summary>Keen Edge's bleed on a cut: the arena's damage over time ("bleed", refreshed by the next cut), and the drips the effects show.</summary>
        private void GlaiveBleed(NativePilot p,NativePilot q)
        {
            if(GlaiveStacks(p,NativeGlaiveChips.KeenEdge,NativeGlaiveChips.KeenMax)<=0||q.Hp<=0)return;RefreshDot(q,p,"bleed",NativeGlaiveChips.BleedDps,NativeGlaiveChips.BleedFor);
            p.GlaiveBleedUntil[q.Seat.id]=Time+NativeGlaiveChips.BleedFor;if(!p.GlaiveBleedShow.ContainsKey(q.Seat.id))p.GlaiveBleedShow[q.Seat.id]=Time+NativeGlaiveChips.BleedShow;
        }
        private void GlaiveBleed(NativePilot p,NativeMinion m)
        {
            if(GlaiveStacks(p,NativeGlaiveChips.KeenEdge,NativeGlaiveChips.KeenMax)<=0||m.Hp<=0||HubDamageBlocked(p,m.Body.position))return;
            string key=p.Seat.id+":bleed";m.Dots.TryGetValue(key,out var old);bool live=old!=null&&old.Until>=Time;
            m.Dots[key]=new NativeDot{HubNoReward=HubInside(p.Body.position),Source=p.Seat.id,Dps=NativeGlaiveChips.BleedDps,Next=live?old.Next:Time+.25,Until=Time+NativeGlaiveChips.BleedFor};
            p.GlaiveBleedUntil[m.Id]=Time+NativeGlaiveChips.BleedFor;if(!p.GlaiveBleedShow.ContainsKey(m.Id))p.GlaiveBleedShow[m.Id]=Time+NativeGlaiveChips.BleedShow;
        }
        /// <summary>Drags `q` toward the wielder by `distance` (never closer than `close`): a pull on its knockback, as far as it carries.</summary>
        private void GlaiveDrag(NativePilot p,NativePilot q,float distance,float close)
        {
            if(q.Hp<=0||q.Body.isKinematic||SirenArmor(q)||Pve?.Boss==q)return;var d=p.Body.position-q.Body.position;d.y=0;float l=d.magnitude;float pull=Math.Min(distance,Math.Max(0,l-close));if(pull<.05f)return;
            // A knockback of v m/s carries v/3.1 m as it eases off (NativeMeleeRules.External).
            q.Knockback=d/l*(pull*3.1f);GlaiveHit(p,NativeGlaive.HitPull,q.Body.position+Vector3.up*.9f,d,q.Seat.id);
        }
        private void GlaiveDrag(NativePilot p,NativeMinion m,float distance,float close)
        {
            if(m.Hp<=0||m.Body.isKinematic||IsZoneBoss(m))return;var d=p.Body.position-m.Body.position;d.y=0;float l=d.magnitude;float pull=Math.Min(distance,Math.Max(0,l-close));if(pull<.05f)return;
            // A minion's push is held .4 s, easing off (NativeBombAuthority): it carries about .235 s of its speed.
            GlaivePush(m,d/l*(pull/.235f),.5f);GlaiveHit(p,NativeGlaive.HitPull,m.Body.position+Vector3.up*.6f,d,m.Id);
        }
        /// <summary>What the record carries of the chips (each tick).</summary>
        private void GlaiveChipRecord(NativePilot p)
        {
            var k=p.Glaive;k.Keen=GlaiveStacks(p,NativeGlaiveChips.KeenEdge,NativeGlaiveChips.KeenMax);k.Twin=GlaiveChip(p,NativeGlaiveChips.TwinOrbit);k.Shatter=GlaiveChip(p,NativeGlaiveChips.Shatterpoint);
            k.LongArc=GlaiveChip(p,NativeGlaiveChips.LongArc);k.Guard=GlaiveChip(p,NativeGlaiveChips.CrescentGuard);k.QuickUntil=GlaiveChip(p,NativeGlaiveChips.QuickCatch)?p.GlaiveQuickUntil:-100;
            if(!GlaiveChip(p,NativeGlaiveChips.SkimmingStone))k.SlickAt=-100;
        }
        /// <summary>Whirlwind: as the whirl winds up, everyone within its reach is dragged in.</summary>
        private void GlaiveWhirlDrag(NativePilot p)
        {
            var at=p.Body.position;bool Near(Vector3 q){var d=q-at;if(Math.Abs(d.y)>1.9f)return false;return new Vector2(d.x,d.z).magnitude<=NativeGlaiveChips.WhirlReach+.45f;}
            foreach(var q in GlaivePilots(p))if(Near(q.Body.position)&&MeleeVisible(at+Vector3.up*.3f,q.Body.position,p,q.Collider))GlaiveDrag(p,q,NativeGlaiveChips.WhirlDrag,NativeGlaiveChips.WhirlClose);
            foreach(var m in GlaiveMinions(p))if(Near(m.Body.position)&&MeleeVisible(at+Vector3.up*.3f,m.Body.position,p,m.Collider))GlaiveDrag(p,m,NativeGlaiveChips.WhirlDrag,NativeGlaiveChips.WhirlClose);
        }
        /// <summary>Skimming Stone's slick: enemies on it slowed while they stay.</summary>
        private void GlaiveSlickStep(NativePilot p)
        {
            var k=p.Glaive;if(!NativeGlaiveChips.SlickOn(k.SlickAt,Time))return;var a=Vec(k.SlickFrom);var b=Vec(k.SlickTo);
            // Laid as the skim goes: only the part behind it so far.
            float laid=Mathf.Clamp01((Time-k.SlickAt)/NativeGlaive.SkimTime);b=Vector3.Lerp(a,b,laid);
            bool On(Vector3 q){var f=new Vector3(q.x,0,q.z);float d=SegmentDistance(new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z),f);return d<=NativeGlaiveChips.SlickWidth&&q.y-Math.Min(a.y,b.y)<1.6f&&q.y-Math.Max(a.y,b.y)>-.8f;}
            foreach(var q in GlaivePilots(p))if(On(q.Body.position-Vector3.up*.865f))Slow(q,NativeGlaiveChips.SlickSlow,NativeGlaiveChips.SlickHold);
            foreach(var m in GlaiveMinions(p))if(On(m.Body.position-Vector3.up*.5f))SlowMinion(m,NativeGlaiveChips.SlickSlow,NativeGlaiveChips.SlickHold);
        }
        /// <summary>Keen Edge's drips for the effects: one now and then on each one still bleeding.</summary>
        private void GlaiveBleedStep(NativePilot p)
        {
            if(p.GlaiveBleedUntil.Count==0)return;
            foreach(var id in p.GlaiveBleedUntil.Keys.ToArray())
            {
                var q=Pilots.Find(x=>x.Seat.id==id);var m=q==null?Minions.Find(x=>x.Id==id):null;bool alive=q!=null?q.Hp>0:m!=null&&m.Hp>0;
                if(!alive||Time>=p.GlaiveBleedUntil[id]){p.GlaiveBleedUntil.Remove(id);p.GlaiveBleedShow.Remove(id);continue;}
                if(p.GlaiveBleedShow.TryGetValue(id,out float next)&&Time<next)continue;p.GlaiveBleedShow[id]=Time+NativeGlaiveChips.BleedShow;
                GlaiveHit(p,NativeGlaive.HitBleed,(q!=null?q.Body.position+Vector3.up*.35f:m.Body.position+Vector3.up*.2f),Vector3.down,id);
            }
        }
        /// <summary>Shatterpoint's shards: out from the burst, each cutting whoever it passes once, stopped by a wall.</summary>
        private void GlaiveShardStep(NativePilot p)
        {
            for(int i=p.GlaiveShards.Count-1;i>=0;i--)
            {
                var s=p.GlaiveShards[i];float step=Math.Min(NativeGlaiveChips.ShardSpeed*Rules.Dt,NativeGlaiveChips.ShardRange-s.Travel);var from=s.Pos;var to=from+s.Dir*step;bool done=false;
                var wall=SweepNative(from,to-from,.08f,c=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger);
                if(wall.HasValue){to=from+s.Dir*Math.Max(0,wall.Value.Distance-.05f);done=true;}
                foreach(var q in GlaivePilots(p)){if(s.Struck.Contains(q.Seat.id)||SegmentDistance(from,to,q.Body.position+Vector3.up*.2f)>NativeGlaiveChips.ShardRadius+.35f)continue;s.Struck.Add(q.Seat.id);if(GlaiveHurt(p,q,NativeGlaiveChips.ShardDamage,s.Dir,NativeGlaive.HitShard)>0)GlaiveHit(p,NativeGlaive.HitShard,q.Body.position+Vector3.up*.4f,s.Dir,q.Seat.id);}
                foreach(var m in GlaiveMinions(p)){if(s.Struck.Contains(m.Id)||SegmentDistance(from,to,m.Body.position)>NativeGlaiveChips.ShardRadius+.4f)continue;s.Struck.Add(m.Id);if(GlaiveHurt(p,m,NativeGlaiveChips.ShardDamage,s.Dir,NativeGlaive.HitShard)>0)GlaiveHit(p,NativeGlaive.HitShard,m.Body.position+Vector3.up*.3f,s.Dir,m.Id);}
                s.Pos=to;s.Travel+=step;if(done||s.Travel>=NativeGlaiveChips.ShardRange-1e-4f)p.GlaiveShards.RemoveAt(i);
            }
        }
        /// <summary>Crescent Guard: the halo's ring (or its twin) at `pos` cuts down the enemy shots it passes (bullets, ability shots, acid).</summary>
        private void GlaiveGuardShots(NativePilot p,Vector3 pos)
        {
            float reach=NativeGlaive.HaloHitRadius+NativeGlaiveChips.GuardReach;
            foreach(var b in Bullets.ToArray())
            {
                if(b.Life<=0)continue;var owner=ExpansionOwner(b.Owner);if(owner==null||owner==p||!Enemy(owner,p))continue;
                if(SegmentDistance(b.Position,b.Position+b.Velocity*Rules.Dt,pos)>reach+b.Radius)continue;GlaiveHit(p,NativeGlaive.HitGuard,b.Position,-b.Velocity);EndBullet(b);
            }
            foreach(var s in AbilityShots.ToArray())
            {
                if(s.Life<=0||s.Owner==null||s.Owner==p||!Enemy(s.Owner,p))continue;
                if(SegmentDistance(s.Position,s.Position+s.Velocity*Rules.Dt,pos)>reach+s.Radius)continue;GlaiveHit(p,NativeGlaive.HitGuard,s.Position,-s.Velocity);AbilityShots.Remove(s);
            }
            foreach(var spit in pveAcid.ToArray())
            {
                var r=spit.Record;if(spit.Landed||Time>=r.at+r.flight||r.devMonster&&r.devTarget!=0&&r.devTarget!=p.Seat.id)continue;
                NativeHordeFx.Glob(new System.Numerics.Vector3(r.from.x,r.from.y,r.from.z),new System.Numerics.Vector3(r.to.x,r.to.y,r.to.z),r.flight,Time-r.at,out var glob);var at=new Vector3(glob.X,glob.Y,glob.Z);
                if(Vector3.Distance(at,pos)>reach+.2f)continue;GlaiveHit(p,NativeGlaive.HitGuard,at,at-pos);pveAcid.Remove(spit);
            }
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
            // The plunging slam (the standard swing, KitSlam): none of the kit's moves while it lasts; its attack is the slam's.
            if(p.Swing!=null&&Time<p.AttackEnd)return;if(KitSlam(p)){attack=press=false;}
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
                    k.CastRelease=Time+NativeGlaive.SwingHit[step]/rate;k.CastUntil=Time+NativeGlaive.SwingLength[step]/rate;k.ComboUntil=k.CastUntil+NativeGlaive.ComboWindow;
                    // Whirlwind: the whirl drags them in as it winds up, before it cuts.
                    if(step==2&&GlaiveChip(p,NativeGlaiveChips.Whirlwind))GlaiveWhirlDrag(p);
                    break;
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
                GlaiveChipRecord(p);
                if(!p.GlaiveReleased&&k.CastKind!=null&&Time>=k.CastRelease){p.GlaiveReleased=true;if(able&&Time<=k.CastUntil+.001f)GlaiveRelease(p);}
                if(able)GlaiveSkimStep(p);
                GlaiveDiscStep(p);
                if(p.GlaiveShards.Count>0)GlaiveShardStep(p);
                GlaiveSlickStep(p);GlaiveBleedStep(p);
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
                    // Skimming Stone: farther, and a slick of light left along the way.
                    bool stone=GlaiveChip(p,NativeGlaiveChips.SkimmingStone);var from=p.Body.position;var dir=p.GlaiveFacing;float reach=NativeGlaive.SkimReach*(stone?NativeGlaiveChips.SkimFarther:1);
                    var hit=SweepNative(from+Vector3.up*.2f,dir*reach,.35f,c=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger);
                    if(hit.HasValue)reach=Math.Max(0,hit.Value.Distance-.45f);
                    p.GlaiveFrom=from;p.GlaiveTo=from+dir*reach;p.GlaiveStruck.Clear();GlaiveHit(p,NativeGlaive.HitSkim,GroundPoint(from),dir);
                    if(stone){k.SlickAt=Time;k.SlickFrom=Vec(GroundPoint(from));k.SlickTo=Vec(GroundPoint(p.GlaiveTo));}
                    break;
                }
                case NativeGlaive.Throw:
                {
                    // Thrown near level (the camera's own downward look would bury it in the floor a few metres out).
                    var aim=NativeGlaive.ThrowAim(p.GlaiveAim.x,p.GlaiveAim.y,p.GlaiveAim.z);var throwAim=new Vector3(aim.X,aim.Y,aim.Z);
                    p.GlaivePos=GlaiveHand(p)+throwAim*.4f;p.GlaiveVel=throwAim*NativeGlaive.ThrowSpeed;p.GlaiveTravel=0;p.GlaiveStruck.Clear();p.GlaiveFromThrow=true;
                    k.Disc=NativeGlaive.Out;k.DiscAt=Time;k.Bounces=0;k.Reformed=false;GlaiveSample(p);break;
                }
                case NativeGlaive.Halo:
                {
                    k.Disc=NativeGlaive.Orbit;k.DiscAt=Time;k.DiscDir=Vec(p.GlaiveFacing);k.Reformed=false;p.GlaiveHaloNext.Clear();p.GlaiveTwinNext.Clear();p.GlaiveFromThrow=false;p.GlaivePos=GlaiveHand(p);break;
                }
            }
        }
        /// <summary>A cut (the forehand, the backhand: a cone ahead; the whirl: all round): the shared melee's damage, a stagger, a little push.</summary>
        private void GlaiveCut(NativePilot p,int step)
        {
            var at=p.Body.position;var f=p.GlaiveFacing;float reach=NativeGlaive.SwingReach[step],arc=NativeGlaive.SwingArc[step];float damage=NativeGlaive.SwingDamage[step];string kind=step==2?NativeGlaive.HitWhirl:NativeGlaive.HitCut;
            // Whirlwind: the whirl reaches farther and harder, and holds them in (no push out).
            bool wind=step==2&&GlaiveChip(p,NativeGlaiveChips.Whirlwind);if(wind){reach=NativeGlaiveChips.WhirlReach;damage*=NativeGlaiveChips.WhirlDamage;}
            bool Inside(Vector3 q){var d=q-at;if(Math.Abs(d.y)>1.9f)return false;var flat=new Vector3(d.x,0,d.z);float l=flat.magnitude;if(l>reach+.45f)return false;if(l<.6f||arc>=3.1f)return true;return Vector3.Angle(f,flat)*Mathf.Deg2Rad<=arc;}
            foreach(var q in GlaivePilots(p))
            {
                if(!Inside(q.Body.position)||!MeleeVisible(at+Vector3.up*.3f,q.Body.position,p,q.Collider))continue;
                var d=q.Body.position-at;if(GlaiveHurt(p,q,damage,d,NativeGlaive.Swing,(float)p.Weapon.impulse,true)<=0)continue;
                StaggerMelee(q,NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"]));if(step==2&&!wind)q.Knockback+=new Vector3(d.x,0,d.z).normalized*NativeGlaive.SwingPush;
                GlaiveHit(p,kind,q.Body.position+Vector3.up*.45f,d,q.Seat.id);
            }
            foreach(var m in GlaiveMinions(p))
            {
                if(!Inside(m.Body.position)||!MeleeVisible(at+Vector3.up*.3f,m.Body.position,p,m.Collider))continue;
                var d=m.Body.position-at;if(GlaiveHurt(p,m,damage,d,NativeGlaive.Swing,0,true)<=0)continue;if(!wind)GlaivePush(m,new Vector3(d.x,0,d.z).normalized*NativeGlaive.SwingPush,1);
                GlaiveHit(p,kind,m.Body.position+Vector3.up*.45f,d,m.Id);
            }
        }
        /// <summary>The fourth cut: the ring flicked out on its boomerang loop and caught again (Core NativeGlaive.FlickAt).</summary>
        private void GlaiveFlick(NativePilot p)
        {
            var k=p.Glaive;k.Disc=NativeGlaive.Flick;k.DiscAt=Time;k.DiscFrom=Vec(GlaiveHand(p));k.DiscDir=Vec(p.GlaiveFacing);k.Reformed=false;p.GlaiveStruck.Clear();p.GlaiveFromThrow=false;k.LongArc=GlaiveChip(p,NativeGlaiveChips.LongArc);
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
        /// <summary>The halo's ring (or Twin Orbit's) at `pos`: it cuts each enemy it passes, each at most every HaloEvery s on its own clock
        /// (`next`); Crescent Guard's ring cuts down the shots it passes too.</summary>
        private void GlaiveHaloCuts(NativePilot p,Vector3 pos,Dictionary<int,float> next)
        {
            foreach(var q in GlaivePilots(p)){if(next.TryGetValue(q.Seat.id,out float at)&&Time<at)continue;if(Vector3.Distance(q.Body.position+Vector3.up*.1f,pos)>NativeGlaive.HaloHitRadius+.35f)continue;next[q.Seat.id]=Time+NativeGlaive.HaloEvery;var d=q.Body.position-p.Body.position;if(GlaiveHurt(p,q,NativeGlaive.HaloDamage,d,NativeGlaive.Halo)>0){StaggerMelee(q,.15f);GlaiveHit(p,NativeGlaive.HitHalo,pos,d,q.Seat.id);}}
            foreach(var m in GlaiveMinions(p)){if(next.TryGetValue(m.Id,out float at)&&Time<at)continue;if(Vector3.Distance(m.Body.position,pos)>NativeGlaive.HaloHitRadius+.4f)continue;next[m.Id]=Time+NativeGlaive.HaloEvery;if(GlaiveHurt(p,m,NativeGlaive.HaloDamage,m.Body.position-p.Body.position,NativeGlaive.Halo)>0)GlaiveHit(p,NativeGlaive.HitHalo,pos,m.Body.position-pos,m.Id);}
            if(GlaiveChip(p,NativeGlaiveChips.CrescentGuard))GlaiveGuardShots(p,pos);
        }
        /// <summary>The ring out of the hand: the throw's flight and ricochets, its return, the flick's loop, the halo, the re-forming.</summary>
        private void GlaiveDiscStep(NativePilot p)
        {
            var k=p.Glaive;float cd=(float)p.Stats["abilityCooldown"];
            void Caught(bool flick=false)
            {
                k.Disc=NativeGlaive.Hand;k.DiscAt=Time;k.Reformed=false;if(p.GlaiveFromThrow)k.Ready[0]=Time+NativeGlaive.Cooldown[0]*cd;GlaiveHit(p,NativeGlaive.HitCatch,GlaiveHand(p),p.GlaiveFacing);
                // Quick Catch: the thrown or circling ring caught takes time off Q and puts a spring in the step (not the combo's flick).
                if(!flick&&p.Hp>0&&GlaiveChip(p,NativeGlaiveChips.QuickCatch)){k.Ready[0]=Math.Max(Time,k.Ready[0]-NativeGlaiveChips.QuickCut);p.GlaiveQuickUntil=Time+NativeGlaiveChips.QuickFor;k.QuickUntil=p.GlaiveQuickUntil;}
            }
            void Return(){k.Disc=NativeGlaive.Back;p.GlaiveStruck.Clear();GlaiveSample(p);}
            switch(k.Disc)
            {
                case NativeGlaive.Flick:
                {
                    // Long Arc: the loop farther and wider, its cuts harder.
                    bool arc=k.LongArc;float u=(Time-k.DiscAt)/NativeGlaive.FlickTime;var at=arc?NativeGlaive.FlickAt(NativeGlaive.N(k.DiscFrom),NativeGlaive.N(k.DiscDir),u,NativeGlaive.FlickReach*NativeGlaiveChips.ArcReach,NativeGlaive.FlickWide*NativeGlaiveChips.ArcWide):NativeGlaive.FlickAt(NativeGlaive.N(k.DiscFrom),NativeGlaive.N(k.DiscDir),u);var pos=new Vector3(at.X,at.Y,at.Z);p.GlaivePos=pos;
                    int half=u<.5f?0:1;float damage=NativeGlaive.SwingDamage[3]*(arc?NativeGlaiveChips.ArcDamage:1);
                    foreach(var q in GlaivePilots(p)){int key=q.Seat.id*2+half;if(p.GlaiveStruck.Contains(key)||Vector3.Distance(q.Body.position+Vector3.up*.2f,pos)>NativeGlaive.FlickRadius+.35f)continue;p.GlaiveStruck.Add(key);var d=q.Body.position-p.Body.position;if(GlaiveHurt(p,q,damage,d,NativeGlaive.Swing,0,true)>0){StaggerMelee(q,.25f);GlaiveHit(p,NativeGlaive.HitFlick,pos,d,q.Seat.id);}}
                    foreach(var m in GlaiveMinions(p)){int key=m.Id*2+half;if(p.GlaiveStruck.Contains(key)||Vector3.Distance(m.Body.position,pos)>NativeGlaive.FlickRadius+.35f)continue;p.GlaiveStruck.Add(key);if(GlaiveHurt(p,m,damage,m.Body.position-p.Body.position,NativeGlaive.Swing,0,true)>0)GlaiveHit(p,NativeGlaive.HitFlick,pos,m.Body.position-pos,m.Id);}
                    if(u>=1)Caught(true);break;
                }
                case NativeGlaive.Out:
                {
                    var from=p.GlaivePos;var to=from+p.GlaiveVel*Rules.Dt;float step=(to-from).magnitude;
                    // A wall turns it back with a spark.
                    var wall=SweepNative(from,to-from,.12f,c=>c.gameObject.layer==PropLayer||c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null&&!c.isTrigger);
                    if(wall.HasValue)
                    {
                        p.GlaivePos=from+(to-from).normalized*Math.Max(0,wall.Value.Distance-.1f);
                        // A floor skips it on (it bounces off, a little slower, with a spark); a wall turns it back.
                        var n=wall.Value.Normal;if(n.y>.6f&&p.GlaiveVel.y<0){p.GlaiveVel=Vector3.Reflect(p.GlaiveVel,n)*.92f;p.GlaiveVel.y=Math.Max(p.GlaiveVel.y,1.5f);p.GlaivePos+=n*.05f;GlaiveHit(p,NativeGlaive.HitReturn,p.GlaivePos,n);GlaiveSample(p);break;}
                        GlaiveHit(p,NativeGlaive.HitRicochet,p.GlaivePos,-p.GlaiveVel);Return();break;
                    }
                    p.GlaivePos=to;p.GlaiveTravel+=step;
                    // The first enemy it reaches: cut, then on to the nearest other in sight, or back.
                    NativePilot hitPilot=null;NativeMinion hitMinion=null;
                    foreach(var q in GlaivePilots(p)){if(p.GlaiveStruck.Contains(q.Seat.id))continue;if(SegmentDistance(from,to,q.Body.position+Vector3.up*.2f)<=NativeGlaive.ThrowRadius+.35f){hitPilot=q;break;}}
                    if(hitPilot==null)foreach(var m in GlaiveMinions(p)){if(p.GlaiveStruck.Contains(m.Id))continue;if(SegmentDistance(from,to,m.Body.position)<=NativeGlaive.ThrowRadius+.4f){hitMinion=m;break;}}
                    if(hitPilot!=null||hitMinion!=null)
                    {
                        // Fourth Bounce: more ricochets, a longer look, more of the hit kept.
                        float damage=NativeGlaive.ThrowDamage*Mathf.Pow(GlaiveRicochetScale(p),k.Bounces);Vector3 where;
                        if(hitPilot!=null){p.GlaiveStruck.Add(hitPilot.Seat.id);where=hitPilot.Body.position+Vector3.up*.3f;if(GlaiveHurt(p,hitPilot,damage,p.GlaiveVel,NativeGlaive.Throw)>0)StaggerMelee(hitPilot,.3f);GlaiveHit(p,k.Bounces==0?NativeGlaive.HitThrow:NativeGlaive.HitRicochet,where,p.GlaiveVel,hitPilot.Seat.id);}
                        else{p.GlaiveStruck.Add(hitMinion.Id);where=hitMinion.Body.position;GlaiveHurt(p,hitMinion,damage,p.GlaiveVel,NativeGlaive.Throw);GlaiveHit(p,k.Bounces==0?NativeGlaive.HitThrow:NativeGlaive.HitRicochet,where,p.GlaiveVel,hitMinion.Id);}
                        p.GlaivePos=where;Vector3? next=null;float best=GlaiveRicochetRange(p);
                        if(k.Bounces<GlaiveRicochets(p))
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
                    // Home on the hand through anything in the way, cutting whoever it passes once (Magnet Return: and dragging them in).
                    var hand=GlaiveHand(p);var d=hand-p.GlaivePos;float l=d.magnitude,step=NativeGlaive.ReturnSpeed*Rules.Dt;var from=p.GlaivePos;
                    if(l<=NativeGlaive.CatchRadius+step||p.Hp<=0){Caught();break;}
                    p.GlaiveVel=d/l*NativeGlaive.ReturnSpeed;p.GlaivePos+=p.GlaiveVel*Rules.Dt;bool magnet=GlaiveChip(p,NativeGlaiveChips.MagnetReturn);
                    foreach(var q in GlaivePilots(p)){if(p.GlaiveStruck.Contains(q.Seat.id)||SegmentDistance(from,p.GlaivePos,q.Body.position+Vector3.up*.2f)>NativeGlaive.ReturnRadius+.35f)continue;p.GlaiveStruck.Add(q.Seat.id);if(GlaiveHurt(p,q,NativeGlaive.ReturnDamage,p.GlaiveVel,NativeGlaive.Throw)>0){GlaiveHit(p,NativeGlaive.HitReturn,q.Body.position+Vector3.up*.3f,p.GlaiveVel,q.Seat.id);if(magnet)GlaiveDrag(p,q,NativeGlaiveChips.MagnetDrag,NativeGlaiveChips.MagnetClose);}}
                    foreach(var m in GlaiveMinions(p)){if(p.GlaiveStruck.Contains(m.Id)||SegmentDistance(from,p.GlaivePos,m.Body.position)>NativeGlaive.ReturnRadius+.4f)continue;p.GlaiveStruck.Add(m.Id);if(GlaiveHurt(p,m,NativeGlaive.ReturnDamage,p.GlaiveVel,NativeGlaive.Throw)>0){GlaiveHit(p,NativeGlaive.HitReturn,m.Body.position,p.GlaiveVel,m.Id);if(magnet)GlaiveDrag(p,m,NativeGlaiveChips.MagnetDrag,NativeGlaiveChips.MagnetClose);}}
                    if(Time-k.DiscAt>NativeGlaive.MostOut+3)Caught();else GlaiveSample(p);
                    break;
                }
                case NativeGlaive.Orbit:
                {
                    var centre=new System.Numerics.Vector3(p.Body.position.x,p.Body.position.y,p.Body.position.z)+System.Numerics.Vector3.UnitY*.1f;
                    var at=NativeGlaive.OrbitAt(centre,NativeGlaive.N(k.DiscDir),k.DiscAt,Time);var pos=new Vector3(at.X,at.Y,at.Z);p.GlaivePos=pos;
                    GlaiveHaloCuts(p,pos,p.GlaiveHaloNext);
                    // Twin Orbit: the second ring, the other way round, farther out, its own cuts.
                    if(GlaiveChip(p,NativeGlaiveChips.TwinOrbit)){var tw=NativeGlaiveChips.TwinAt(centre,NativeGlaive.N(k.DiscDir),k.DiscAt,Time);GlaiveHaloCuts(p,new Vector3(tw.X,tw.Y,tw.Z),p.GlaiveTwinNext);}
                    if(Time>=k.DiscAt+NativeGlaive.HaloTime){p.GlaiveVel=Vector3.zero;Return();}
                    break;
                }
                case NativeGlaive.Gone:
                    if(Time>=k.DiscAt+NativeGlaive.Reform){k.Disc=NativeGlaive.Hand;k.DiscAt=Time;k.Reformed=true;if(p.GlaiveFromThrow)k.Ready[0]=Time+NativeGlaive.Cooldown[0]*cd;}
                    break;
            }
        }
        /// <summary>The ring bursts where it is: everyone round it hurt and thrown out; it re-forms in the hand (Shatterpoint: wider, and
        /// its shards flung out).</summary>
        private void GlaiveBurst(NativePilot p)
        {
            var k=p.Glaive;var at=p.GlaivePos;int first=0;float radius=GlaiveBurstRadius(p);
            foreach(var q in GlaivePilots(p))
            {
                var d=q.Body.position-at;if(d.magnitude>radius+.3f||!MeleeVisible(at,q.Body.position,p,q.Collider))continue;
                if(GlaiveHurt(p,q,NativeGlaive.BurstDamage,d,NativeGlaive.Burst)<=0)continue;if(first==0)first=q.Seat.id;
                BombLaunch(q,p,new Vector3(d.x,0,d.z).normalized*NativeGlaive.BurstPush,NativeGlaive.BurstLift,0);
            }
            foreach(var m in GlaiveMinions(p))
            {
                var d=m.Body.position-at;if(d.magnitude>radius+.4f)continue;if(GlaiveHurt(p,m,NativeGlaive.BurstDamage,d,NativeGlaive.Burst)<=0)continue;if(first==0)first=m.Id;
                GlaivePush(m,new Vector3(d.x,0,d.z).normalized*NativeGlaive.BurstPush,NativeGlaive.BurstLift);
            }
            var burst=GlaiveHit(p,NativeGlaive.HitBurst,at,Vector3.up,first);
            if(GlaiveChip(p,NativeGlaiveChips.Shatterpoint))for(int i=0;i<NativeGlaiveChips.Shards;i++){var dir=NativeGlaiveChips.ShardDir(burst.Id,i);p.GlaiveShards.Add(new GlaiveShard{Pos=at,Dir=new Vector3(dir.X,dir.Y,dir.Z)});}
            k.Disc=NativeGlaive.Gone;k.DiscAt=Time;k.DiscPos=Vec(at);k.DiscVel=default;k.DiscSampleAt=Time;
            // A pose for it: the free hand snapped shut.
            if(!GlaiveBusy(p)){k.CastKind=NativeGlaive.Burst;k.CastAt=Time;k.CastRelease=Time+.05f;k.CastUntil=Time+.3f;k.CastStep=0;p.GlaiveReleased=true;}
            Emit(EventKind.AbilityCast,p,0,at,0,NativeGlaive.Burst);
        }
    }
}
