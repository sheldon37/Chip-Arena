using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>A pilot's boss relic state (docs/BOSS-CHIPS.md): fresh each round; Lux Cecidit's light comes back on a death too.</summary>
    [Serializable] public sealed class NativeRelicState
    {
        public int Round=int.MinValue;public float SprintFor,Heat,SlagBonus,WarmAt=-100,FallTop;public bool Full,Spent,Throw,Warming,LuxUsed,Airborne,NoclipHit;
        public NativeRelicState Copy()=>(NativeRelicState)MemberwiseClone();
    }
    public sealed partial class NativePilot {public NativeRelicState Relic=new NativeRelicState();}
    /// <summary>
    /// The twelve boss relics' rules (the owner, 2026-11-07: each boss's own chips, only from its reward and rarely). The numbers are docs/BOSS-CHIPS.md's.
    /// Hooks, beside the wild chips': outgoing damage (<see cref="RelicDamage"/>), a landed hit (<see cref="RelicHit"/>), a kill (<see cref="RelicKill"/>), damage taken
    /// (<see cref="RelicTaken"/>), a lethal blow (<see cref="RelicSave"/>), a roll (<see cref="RelicRoll"/>), each pilot's tick (<see cref="StepRelicPilot"/>) and the world's (<see cref="StepRelicWorld"/>).
    /// Every proc emits a ChipProc with the relic's id (the presentation draws it). Zone bosses can't be moved or stunned by them (MobilityPush already refuses).
    /// The relic counters and rails are the authority's own (not in the handoff frame, as the wild chips' timers aren't).
    /// </summary>
    public sealed partial class NativeArena
    {
        private sealed class RelicRail {public NativePilot Owner;public Vector3 From,To;public float Until;public readonly HashSet<int> Hit=new HashSet<int>();}
        private struct RelicTally {public int Count;public float At;}
        private readonly List<RelicRail> relicRails=new List<RelicRail>();
        private readonly Dictionary<(int owner,int target,int kind),RelicTally> relicTallies=new Dictionary<(int,int,int),RelicTally>();
        private readonly List<(NativePilot pilot,Collider other,float until)> relicNoclip=new List<(NativePilot,Collider,float)>();
        private const int TallyLullaby=1,TallyHum=2,TallyUndertow=3,TallyUndertowPull=4;

        private int RelicCount(NativePilot p,int target,int kind,float window)
        {
            var key=(p.Seat.id,target,kind);relicTallies.TryGetValue(key,out var c);if(Time-c.At>window)c.Count=0;c.Count++;c.At=Time;relicTallies[key]=c;
            if(relicTallies.Count>2048)foreach(var k in relicTallies.Where(x=>Time-x.Value.At>10).Select(x=>x.Key).ToArray())relicTallies.Remove(k);
            return c.Count;
        }
        private float RelicSince(NativePilot p,int target,int kind)=>relicTallies.TryGetValue((p.Seat.id,target,kind),out var c)?Time-c.At:float.MaxValue;
        private IEnumerable<(int id,NativePilot pilot,NativeMinion minion,Vector3 at,Collider collider)> RelicNear(NativePilot p,Vector3 at,float radius)
            =>MeleeTargets(p).Where(t=>Vector3.Distance(t.at,at)<=radius&&MeleeVisible(at+Vector3.up*.4f,t.at,p,t.collider)).ToArray();
        private void RelicBurst(NativePilot p,string relic,Vector3 at,float radius,float damage,int exceptId=0)
        {foreach(var t in RelicNear(p,at,radius))if(t.id!=exceptId)WildNoProcHit(p,t.id,damage,t.at);ChipProc(p,relic,at,0,radius);}
        private void RelicStun(NativePilot p,int id,float seconds)
        {var t=MeleeTarget(p,id);if(t.pilot!=null&&!Invulnerable(t.pilot))t.pilot.StunEnd=Math.Max(t.pilot.StunEnd,Time+seconds);else if(t.minion?.Pve!=null&&!IsZoneBoss(t.minion))t.minion.Pve.StaggerAt=Math.Max(t.minion.Pve.StaggerAt,Time+seconds);}
        private void RelicSlow(NativePilot p,int id,float factor,float seconds)
        {var t=MeleeTarget(p,id);if(t.pilot!=null)Slow(t.pilot,factor,seconds);else if(t.minion!=null&&!IsZoneBoss(t.minion))SlowMinion(t.minion,factor,seconds);}
        /// <summary>Moves a target toward the pilot by up to `distance`, never closer than 1.4 m.</summary>
        private void RelicDrag(NativePilot p,int id,Vector3 at,float distance)
        {var d=p.Body.position-at;d.y=0;float room=d.magnitude-1.4f;if(room<=.05f)return;MobilityPush(p,id,d.normalized,Math.Min(distance,room));}

        /// <summary>Outgoing damage (ExpansionDamage, before its multipliers): Express Service's first hit at full speed (+60%), Slag Heart's stored heat (+1 per 2, at most +40).</summary>
        private float RelicDamage(NativePilot p,float damage,bool hit)
        {
            if(!hit||p==null)return damage;var r=p.Relic;
            if(r.Full&&!r.Spent&&Has(p,NativeBossChips.Express)){r.Spent=true;r.Throw=true;damage*=1.6f;}
            if(r.Heat>=2&&Has(p,NativeBossChips.SlagHeart)){r.SlagBonus=Math.Min(40,(float)Math.Floor(r.Heat/2));r.Heat=0;damage+=r.SlagBonus;}
            return damage;
        }
        /// <summary>A hit that landed (ExpansionHit): Express Service's throw, Slag Heart's release, Lullaby, Max Hum-Buzz, Undertow.</summary>
        private void RelicHit(NativePilot p,int id,Vector3 at,float actual)
        {
            if(p==null||actual<=0)return;var r=p.Relic;var away=at-p.Body.position;away.y=0;away=away.sqrMagnitude>.001f?away.normalized:Quaternion.Euler(0,p.Input.Yaw,0)*Vector3.forward;
            if(r.Throw){r.Throw=false;TechniqueShove(p,id,(away+Vector3.up*.45f).normalized,11);ChipProc(p,NativeBossChips.Express,at,id,actual,from:p.Body.position,direction:away);}
            if(r.SlagBonus>0){ChipProc(p,NativeBossChips.SlagHeart,at,id,r.SlagBonus,from:p.Body.position);r.SlagBonus=0;}
            if(Has(p,NativeBossChips.Lullaby)&&RelicCount(p,id,TallyLullaby,8)%6==0){RelicDrag(p,id,at,2.5f);ChipProc(p,NativeBossChips.Lullaby,at,id,2.5f,from:p.Body.position);}
            if(Has(p,NativeBossChips.MaxHum))
            {int n=RelicCount(p,id,TallyHum,6);if(n>=5){relicTallies.Remove((p.Seat.id,id,TallyHum));RelicBurst(p,NativeBossChips.MaxHum,at,3,30);}else ChipProc(p,NativeBossChips.MaxHum,at,id,n);}
            if(Has(p,NativeBossChips.Undertow))
            {
                // A fast gun lands many hits a second: the 1 m pull is at most every .2 s on one target, the 3 m drag every 4th hit.
                if(RelicCount(p,id,TallyUndertow,8)%4==0){RelicDrag(p,id,at,3);ChipProc(p,NativeBossChips.Undertow,at,id,3,from:p.Body.position);}
                else if(RelicSince(p,id,TallyUndertowPull)>=.2f){RelicCount(p,id,TallyUndertowPull,1);RelicDrag(p,id,at,1);ChipProc(p,NativeBossChips.Undertow,at,id,1,from:p.Body.position);}
            }
        }
        /// <summary>A kill by the pilot's own hit (where Overkill reads it): Last Verse's song, The Pour's slag.</summary>
        private void RelicKill(NativePilot p,int id,Vector3 at)
        {
            if(p==null)return;
            if(Has(p,NativeBossChips.LastVerse)){foreach(var t in RelicNear(p,at,6))if(t.id!=id)RelicSlow(p,t.id,.6f,2);ChipProc(p,NativeBossChips.LastVerse,at,id,6);}
            if(Has(p,NativeBossChips.Pour)){var floor=at;floor.y=Math.Max(0,at.y-.8f);ExpansionAreaAdd(p,"slag-pour",floor,0,3,2.5f,1.25f);ChipProc(p,NativeBossChips.Pour,floor,id,1.25f);}
        }
        /// <summary>Damage the pilot took (after it landed): Slag Heart stores it as heat (80 heat is the +40 cap).</summary>
        private void RelicTaken(NativePilot p,float actual)
        {if(actual>0&&Has(p,NativeBossChips.SlagHeart))p.Relic.Heat=Math.Min(80,p.Relic.Heat+actual);}
        /// <summary>A blow that would kill: Lux Cecidit, once a round (and once a life), leaves 1 health and bursts with light (40 within 5 m), with a .6 s grace.</summary>
        private bool RelicSave(NativePilot p,float amount)
        {
            if(amount<p.Hp||!Has(p,NativeBossChips.Lux)||p.Relic.LuxUsed)return false;
            p.Relic.LuxUsed=true;p.Hp=1;p.Wild.ImmuneUntil=Math.Max(p.Wild.ImmuneUntil,Time+.6f);RelicBurst(p,NativeBossChips.Lux,p.Body.position,5,40);return true;
        }
        /// <summary>A roll starts (ChipRoll): Mind the Gap lays a live rail along it; Noclip lets it pass through enemies.</summary>
        private void RelicRoll(NativePilot p)
        {
            var dir=p.RollDirection;dir.y=0;if(dir.sqrMagnitude<.01f)return;dir.Normalize();float length=Mathf.Clamp((float)(p.Roll?.distance??4),1,9),until=Math.Max(p.RollEnd,Time+.2f);
            if(Has(p,NativeBossChips.MindGap))
            {
                var from=p.Body.position;from.y-=p.Collider.height/2;float reach=ForwardAbilityStep(p.Body.position,dir,length,out _);
                if(relicRails.Count>=32)relicRails.RemoveAt(0);relicRails.Add(new RelicRail{Owner=p,From=from,To=from+dir*reach,Until=until+1.5f});ChipProc(p,NativeBossChips.MindGap,from,0,reach,from:from,direction:dir);
            }
            if(Has(p,NativeBossChips.Noclip))
            {
                p.Relic.NoclipHit=false;var at=p.Body.position;
                foreach(var t in MeleeTargets(p))if(t.collider!=null&&Vector3.Distance(t.at,at)<=length+3&&!(t.minion!=null&&IsZoneBoss(t.minion))&&!Physics.GetIgnoreCollision(p.Collider,t.collider))
                {Physics.IgnoreCollision(p.Collider,t.collider,true);relicNoclip.Add((p,t.collider,until+.1f));}
                ChipProc(p,NativeBossChips.Noclip,at,0,length,from:at,direction:dir);
            }
        }
        /// <summary>Each pilot's tick (with the wild chips' own): the round's reset, Express Service, The Water Is Warm, Feather Fall, Noclip's pass, Lux Cecidit back after a death.</summary>
        private void StepRelicPilot(NativePilot p,ref float speed)
        {
            int round=Match.Set*100+Match.Round;if(p.Relic.Round!=round){p.Relic=new NativeRelicState{Round=round,FallTop=p.Body.position.y};foreach(var k in relicTallies.Keys.Where(k=>k.owner==p.Seat.id||k.target==p.Seat.id).ToArray())relicTallies.Remove(k);}
            var r=p.Relic;if(p.Hp<=0){r.LuxUsed=false;r.Full=r.Spent=r.Throw=r.Warming=false;r.SprintFor=0;return;}
            if(Has(p,NativeBossChips.Express))
            {
                var flat=p.MoveVelocity;flat.y=0;float run=6.8f*(float)p.Stats["speed"];bool sprinting=p.Input.Sprint&&flat.magnitude>=run*1.1f;
                if(sprinting){r.SprintFor+=1f/60;if(r.SprintFor>=2&&!r.Full){r.Full=true;r.Spent=false;ChipProc(p,NativeBossChips.Express,p.Body.position,p.Seat.id,0,direction:flat.normalized);}}
                else if(flat.magnitude<run*.9f){r.SprintFor=0;r.Full=r.Spent=r.Throw=false;}
                if(r.Full)speed*=1.25f;
            }
            if(Has(p,NativeBossChips.Warm))
            {
                bool still=p.ExpansionStill>=0&&Time-Math.Max(p.ExpansionStill,p.LastDamage)>=1;
                if(still&&!r.Warming){r.Warming=true;r.WarmAt=Time;ChipProc(p,NativeBossChips.Warm,p.Body.position,p.Seat.id,0);}
                if(!still)r.Warming=false;
                if(r.Warming&&Time>=r.WarmAt){r.WarmAt=Time+.25f;Heal(p,(float)p.Stats["hp"]*.01f,NativeBossChips.Warm);}
            }
            if(Has(p,NativeBossChips.Feather))
            {
                float y=p.Body.position.y;
                if(!p.Grounded)
                {
                    if(!r.Airborne){r.Airborne=true;r.FallTop=y;}r.FallTop=Math.Max(r.FallTop,y);
                    var v=p.Body.linearVelocity;if(v.y<-3.2f&&!NativeAirAttackSimulation.Falling(p.Swing)&&Time>=p.LaunchUntil){v.y=-3.2f;p.Body.linearVelocity=v;}
                }
                else if(r.Airborne){r.Airborne=false;var feet=p.Body.position;feet.y-=p.Collider.height/2;if(r.FallTop-y>=3)RelicBurst(p,NativeBossChips.Feather,feet,4,25);r.FallTop=y;}
            }
            if(!r.NoclipHit&&Time<p.RollEnd&&Has(p,NativeBossChips.Noclip))
            {
                var t=MeleeTargets(p).Where(t=>Vector3.Distance(t.at,p.Body.position)<=1.2f).OrderBy(t=>(t.at-p.Body.position).sqrMagnitude).FirstOrDefault();
                if(t.collider!=null){r.NoclipHit=true;WildNoProcHit(p,t.id,20,t.at);PriorityMark(p,t.id,NativeBossChips.Noclip,3);ChipProc(p,NativeBossChips.Noclip,t.at,t.id,20,from:p.Body.position);}
            }
        }
        /// <summary>The world's tick (with the wild chips'): Mind the Gap's rails, Noclip's passes ending.</summary>
        private void StepRelicWorld()
        {
            for(int i=relicRails.Count-1;i>=0;i--)
            {
                var rail=relicRails[i];if(Time>=rail.Until||rail.Owner==null||!Pilots.Contains(rail.Owner)){relicRails.RemoveAt(i);continue;}
                var d=rail.To-rail.From;d.y=0;float len2=d.sqrMagnitude;
                foreach(var t in MeleeTargets(rail.Owner).ToArray())
                {
                    if(rail.Hit.Contains(t.id))continue;var q=t.at-rail.From;q.y=0;float u=len2<.01f?0:Mathf.Clamp01(Vector3.Dot(q,d)/len2);var off=q-d*u;
                    if(off.magnitude>.7f||Math.Abs(t.at.y-rail.From.y)>2.2f)continue;rail.Hit.Add(t.id);WildNoProcHit(rail.Owner,t.id,30,t.at);RelicStun(rail.Owner,t.id,.4f);ChipProc(rail.Owner,NativeBossChips.MindGap,t.at,t.id,30,from:rail.From+d*u);
                }
            }
            for(int i=relicNoclip.Count-1;i>=0;i--)
            {var n=relicNoclip[i];if(Time<n.until&&n.pilot!=null&&n.other!=null&&Pilots.Contains(n.pilot))continue;if(n.pilot?.Collider!=null&&n.other!=null)Physics.IgnoreCollision(n.pilot.Collider,n.other,false);relicNoclip.RemoveAt(i);}
        }
    }
}
