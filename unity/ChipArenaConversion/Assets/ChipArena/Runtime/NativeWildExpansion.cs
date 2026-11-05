using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeWildState
    {
        public NativeWildState Copy()=>(NativeWildState)MemberwiseClone();public int Round;public bool SpareUsed,BatterHit,Shrunk;public float SpinAt=-100,Spin=0,ReplyUntil=-100,HayAt,DownUntil=-100,ImmuneUntil=-100,AuraAt,ReloadAt,StompAt,TwinAt,BatterAt=-100,PeakY,PreviousY,LastYaw,YawRate;
        public Vector3 BatterFrom,BatterDir;
    }
    [Serializable] public sealed class NativeWildAffliction
    {public NativeWildAffliction Copy()=>(NativeWildAffliction)MemberwiseClone();public string Kind;public int By,Target,Count,Stacks;public float Until,Next,Damage;public Vector3 Point,Direction;}
    public sealed partial class NativePilot {public NativeWildState Wild=new NativeWildState();}
    public sealed partial class NativeArena
    {
        private readonly List<NativeWildAffliction> wildAfflictions=new List<NativeWildAffliction>();
        private NativeWildAffliction WildAfflict(NativePilot p,int id,string kind,float duration)
        {
            var a=wildAfflictions.FirstOrDefault(x=>x.By==p.Seat.id&&x.Target==id&&x.Kind==kind);
            if(a==null){if(wildAfflictions.Count>=256){var expendable=wildAfflictions.FirstOrDefault(x=>x.Kind!="wild-slow-bleed");if(expendable==null)return new NativeWildAffliction{By=p.Seat.id,Target=id,Kind=kind,Until=Time+duration};wildAfflictions.Remove(expendable);}a=new NativeWildAffliction{By=p.Seat.id,Target=id,Kind=kind};wildAfflictions.Add(a);}
            if(Time>=a.Until&&kind!="wild-anti-gravity")a.Count=0;a.Until=Time+duration;return a;
        }
        private float WildOutgoing(int id)
        {
            float worm=1;bool shrink=false;foreach(var a in wildAfflictions)if(a.Target==id&&a.Until>Time)
            {if(a.Kind=="wild-tapeworm")worm=Math.Min(worm,Math.Max(.1f,1-.04f*a.Count*a.Stacks));if(a.Kind=="wild-shrink-ray")shrink=true;}return worm*(shrink?.8f:1);
        }
        private void WildHit(NativePilot p,int id,Vector3 at,float actual,Vector3 direction,bool projectile)
        {
            if(p==null||actual<=0)return;var target=MeleeTarget(p,id);bool boss=target.pilot?.Seat.AuthorityBoss==true||target.minion!=null&&IsZoneBoss(target.minion);
            if(Has(p,"wild-haymaker")&&Time>=p.Wild.HayAt){p.Wild.HayAt=Time+12;ChipProc(p,"wild-haymaker",at,id,actual);}
            if(Has(p,"wild-tapeworm")){var a=WildAfflict(p,id,"wild-tapeworm",3);a.Count=Math.Min(5,a.Count+1);a.Stacks=ExpansionStacks(p,"wild-tapeworm");PriorityMark(p,id,a.Kind,3);}
            if(!boss&&Has(p,"wild-shrink-ray")){WildAfflict(p,id,"wild-shrink-ray",3);PriorityMark(p,id,"wild-shrink-ray",3);}
            if(!boss&&Has(p,"wild-anti-gravity")){var a=WildAfflict(p,id,"wild-anti-gravity",0);a.Count++;if(a.Count%4==0){a.Until=Time+.8f;a.Point=at;PriorityMark(p,id,a.Kind,.8f);}}
            if(!boss&&Has(p,"wild-jet-rounds"))
            {var a=wildAfflictions.FirstOrDefault(x=>x.By==p.Seat.id&&x.Target==id&&x.Kind=="wild-jet-rounds");if(a==null||Time>=a.Next){a=WildAfflict(p,id,"wild-jet-rounds",1);a.Next=Time+2;a.Direction=direction.sqrMagnitude>.001f?direction.normalized:(at-p.Body.position).normalized;PriorityMark(p,id,a.Kind,1);}}
            if(Has(p,"wild-domino"))foreach(var t in MeleeTargets(p).Where(t=>t.id!=id&&Vector3.Distance(t.at,at)<=6&&MeleeVisible(at,t.at,p,t.collider)).ToArray())
            {WildNoProcHit(p,t.id,actual*.3f,t.at);ChipProc(p,"wild-domino",t.at,t.id,actual*.3f,from:at);}
            if(projectile&&Has(p,"wild-flanker")&&target.collider!=null)
            {Vector3 spawn=at-Vec(V3.Forward(target.pilot?.Yaw??target.minion.Yaw))*3;if(!SceneLinecast(at,spawn,WorldMask)&&!HubCross(at,spawn)){ExpansionEchoShot(p,spawn,(at-spawn).normalized,actual*.35f,.3f,"wild-flanker",id);}}
        }
        private void WildNoProcHit(NativePilot p,int id,float amount,Vector3 at)
        {var t=MeleeTarget(p,id);if(t.pilot!=null)Damage(t.pilot,p,amount,Vector3.zero,0,false,direct:false,primary:false,damageKind:"expansion-copy");else if(t.minion!=null)HitMinion(t.minion,p,amount,Vector3.zero,0,at,"expansion-copy");}
        private void WildOverkill(NativePilot p,int id,Vector3 at,float extra)
        {
            if(p==null||extra<=0||!Has(p,"wild-overkill"))return;var t=MeleeTargets(p).Where(t=>t.id!=id&&Vector3.Distance(t.at,at)<=12&&MeleeVisible(at,t.at,p,t.collider)).OrderBy(t=>(t.at-at).sqrMagnitude).FirstOrDefault();
            if(t.collider!=null){WildNoProcHit(p,t.id,extra,t.at);ChipProc(p,"wild-overkill",t.at,t.id,extra,from:at);}
        }
        private bool WildSave(NativePilot p,float amount)
        {
            if(p.Wild.DownUntil>Time)return true;
            if(amount<p.Hp||!Has(p,"wild-spare-body")||p.Wild.SpareUsed)return false;
            p.Wild.SpareUsed=true;p.Wild.DownUntil=Time+2;p.Hp=.01f;CancelAbilities(p);CancelSpecial(p);CancelMelee(p);EndTechnique(p);p.MoveVelocity=p.Knockback=Vector3.zero;ChipProc(p,"wild-spare-body",p.Body.position,p.Seat.id,2);return true;
        }
        private void WildDefer(NativePilot target,NativePilot source,float amount)
        {
            if(wildAfflictions.Count>=256){Damage(target,source,amount,Vector3.zero,0,false,true,direct:false,primary:false,damageKind:"slow-bleed");return;}
            wildAfflictions.Add(new NativeWildAffliction{By=source?.Seat.id??0,Target=target.Seat.id,Kind="wild-slow-bleed",Until=Time+4,Next=Time+.25f,Count=16,Damage=amount/16});MarkTechnique(target,"wild-slow-bleed",source??target,4);
        }
        private void WildSuccessfulBlock(NativePilot p)
        {
            if(Has(p,"wild-charged-reply"))p.Wild.ReplyUntil=Time+5;
            if(Has(p,"wild-radar-block")){foreach(var t in MeleeTargets(p))PriorityMark(p,t.id,"wild-radar-block",4);ChipProc(p,"wild-radar-block",p.Body.position,0,4);}
        }
        private float WildAttackMultiplier(NativePilot p)
        {if(p.Wild.ReplyUntil<=Time)return 1;p.Wild.ReplyUntil=-100;return 1+.8f*ExpansionStacks(p,"wild-charged-reply");}
        private void WildStartBlock(NativePilot p)
        {
            var dir=Vec(V3.Forward(p.Input.Yaw));
            if(Has(p,"wild-blink-block")){float d=ForwardAbilityStep(p.Body.position,dir,6,out _);var from=p.Body.position;if(MeleeTeleport(p,from+dir*d,"wild-blink-block"))ChipProc(p,"wild-blink-block",p.Body.position,from:from);}
            if(Has(p,"wild-battering-block")){p.Wild.BatterAt=Time;p.Wild.BatterFrom=p.Body.position;p.Wild.BatterDir=dir;p.Wild.BatterHit=false;ChipProc(p,"wild-battering-block",p.Body.position,direction:dir);}
        }
        private void WildReload(NativePilot p)
        {if(Time<p.Wild.ReloadAt||!Has(p,"wild-hot-swap"))return;p.Wild.ReloadAt=Time+2;ChipProc(p,"wild-hot-swap",p.Body.position);ExpansionAreaAdd(p,"hot-swap",p.Body.position,0,0,15*ExpansionStacks(p,"wild-hot-swap"),5);foreach(var t in MeleeTargets(p).ToArray())if(Vector3.Distance(t.at,p.Body.position)<=5&&MeleeVisible(p.Body.position,t.at,p,t.collider))TechniqueShove(p,t.id,(t.at-p.Body.position).normalized,4);}
        private void WildDeath(NativePilot p)
        {if(Has(p,"wild-spite")){ChipProc(p,"wild-spite",p.Body.position,p.Seat.id,1);ExpansionAreaAdd(p,"spite",p.Body.position,1,0,60,5);}}
        private void WildRoll(NativePilot p)
        {if(Has(p,"wild-afterimage"))ExpansionDecoy(p,"wild-afterimage",1,2,p.Body.position);}
        private void StepWildPilot(NativePilot p,ref float rate,ref float speed)
        {
            int round=Match.Set*100+Match.Round;if(p.Wild.Round!=round){if(p.Wild.Shrunk){p.Collider.height*=1.25f;p.Collider.radius*=1.25f;}p.Wild=new NativeWildState{Round=round,PeakY=p.Body.position.y,PreviousY=p.Body.position.y,LastYaw=p.Yaw};wildAfflictions.RemoveAll(x=>x.Target==p.Seat.id||x.By==p.Seat.id);}
            var w=p.Wild;w.YawRate=Mathf.Clamp(Mathf.DeltaAngle(w.LastYaw,p.Input.Yaw)*60,-120,120);w.LastYaw=p.Input.Yaw;
            if(Has(p,"wild-spin-up")){w.Spin=Time-p.LastShot>.5f?Math.Max(0,w.Spin-1f/30):p.Input.Shoot?Math.Min(1,w.Spin+1f/120):Math.Max(0,w.Spin-1f/30);rate*=1+.5f*ExpansionStacks(p,"wild-spin-up")*w.Spin;}
            if(Has(p,"wild-bloodhound")){var t=MeleeTargets(p).Where(t=>MeleeVisible(p.Body.position,t.at,p,t.collider)).OrderBy(t=>(t.at-p.Body.position).sqrMagnitude).FirstOrDefault();var move=new Vector3(p.Input.X,0,p.Input.Z);if(t.collider!=null&&Vector3.Dot(move.normalized,(t.at-p.Body.position).normalized)>=.7071f)speed*=1+.25f*ExpansionStacks(p,"wild-bloodhound");}
            int cold=Pilots.Where(q=>q.Hp>0&&q!=p&&CanHit(q,p)&&Vector3.Distance(q.Body.position,p.Body.position)<=5).Select(q=>ExpansionStacks(q,"wild-cold-shoulder")).DefaultIfEmpty(0).Max();speed*=(float)Math.Pow(.85,cold);
            if(w.DownUntil>0&&Time>=w.DownUntil){w.DownUntil=-100;w.ImmuneUntil=Time+1;p.Hp=(float)p.Stats["hp"]*.5f;ChipProc(p,"wild-spare-body",p.Body.position,p.Seat.id,p.Hp);ExpansionAreaAdd(p,"spare-body",p.Body.position,0,0,25,4);}
            if(Time>=w.AuraAt){w.AuraAt=Time+.2f;foreach(var t in MeleeTargets(p).ToArray()){float distance=Vector3.Distance(t.at,p.Body.position);if(distance<=2.5f&&Has(p,"wild-buzz-saws")&&MeleeVisible(p.Body.position,t.at,p,t.collider))WildNoProcHit(p,t.id,2*ExpansionStacks(p,"wild-buzz-saws"),t.at);if(t.minion!=null&&distance<=5&&Has(p,"wild-cold-shoulder"))SlowMinion(t.minion,(float)Math.Pow(.85,ExpansionStacks(p,"wild-cold-shoulder")),.25f);}}
            w.PeakY=Math.Max(w.PeakY,p.Body.position.y);
            if(Has(p,"wild-head-stomp")&&Time>=w.StompAt&&p.Body.linearVelocity.y<0&&w.PeakY-p.Body.position.y>1)
            {var t=MeleeTargets(p).FirstOrDefault(t=>Math.Abs(t.at.x-p.Body.position.x)<.6f&&Math.Abs(t.at.z-p.Body.position.z)<.6f&&w.PreviousY-.865f>=t.collider.bounds.max.y&&p.Body.position.y-.865f<=t.collider.bounds.max.y+.1f);if(t.collider!=null){w.StompAt=Time+1;WildNoProcHit(p,t.id,30,t.at);var v=p.Body.linearVelocity;v.y=(float)Math.Sqrt(2*23*6);p.Body.linearVelocity=v;ChipProc(p,"wild-head-stomp",t.at,t.id,30);}}if(p.Grounded)w.PeakY=p.Body.position.y;w.PreviousY=p.Body.position.y;
            if(Has(p,"wild-evil-twin")&&Time>=w.TwinAt&&!Minions.Any(m=>m.Owner==p&&m.Kind=="wild-evil-twin"&&m.Hp>0)){ExpansionDecoy(p,"wild-evil-twin",60,3600,p.Body.position+Vector3.right*3);w.TwinAt=Time+8;}
        }
        private void StepWildConstraints()
        {
            foreach(var a in wildAfflictions.ToArray())
            {
                var owner=Pilots.Find(p=>p.Seat.id==a.By);var p=Pilots.Find(p=>p.Seat.id==a.Target);var m=Minions.Find(m=>m.Id==a.Target);
                if(a.Kind=="wild-slow-bleed")
                {while(p?.Hp>0&&a.Count>0&&Time>=a.Next){a.Next+=.25f;a.Count--;Damage(p,owner,a.Damage,Vector3.zero,0,false,true,direct:false,primary:false,damageKind:"slow-bleed");}if(a.Count<=0||p==null||p.Hp<=0)wildAfflictions.Remove(a);continue;}
                if(Time>=a.Until){if(a.Kind!="wild-anti-gravity"&&(a.Kind!="wild-jet-rounds"||Time>=a.Next))wildAfflictions.Remove(a);continue;}var body=p?.Body??m?.Body;if(body==null)continue;
                if(a.Kind=="wild-jet-rounds"&&owner!=null)MobilityPush(owner,a.Target,a.Direction,8f/60);
                if(a.Kind=="wild-anti-gravity"&&!body.isKinematic){var v=body.linearVelocity;v.y=body.position.y<a.Point.y+3?Math.Min(6,v.y+46f/60):Math.Min(0,v.y);body.linearVelocity=v;}
                if(a.Kind=="wild-splat"&&owner!=null){var v=p!=null?p.Knockback:body.linearVelocity;float step=ForwardAbilityStep(body.position,v.normalized,v.magnitude/60,out bool wall);if(wall){float damage=Math.Min(30,2*v.magnitude*a.Stacks);WildNoProcHit(owner,a.Target,damage,body.position);ChipProc(owner,"wild-splat",body.position,a.Target,damage);wildAfflictions.Remove(a);}}
            }
            foreach(var m in Minions.Where(m=>m.Hp>0)){bool shrink=wildAfflictions.Any(a=>a.Kind=="wild-shrink-ray"&&a.Target==m.Id&&a.Until>Time);if(shrink!=m.ExpansionShrunk){float scale=shrink?.8f:1.25f;m.Collider.height*=scale;m.Collider.radius*=scale;m.ExpansionShrunk=shrink;}}
            foreach(var p in Pilots.Where(p=>p.Hp>0))
            {
                var w=p.Wild;if(Time>=w.BatterAt&&Time<w.BatterAt+.18f){var desired=w.BatterFrom+w.BatterDir*(4*Mathf.Clamp01((Time+1f/60-w.BatterAt)/.18f))-p.Body.position;float length=ForwardAbilityStep(p.Body.position,desired.normalized,desired.magnitude,out bool wall);if(TechniqueDestination(p,p.Body.position+desired.normalized*length))p.Body.position+=desired.normalized*length;if(!w.BatterHit){var t=MeleeTargets(p).FirstOrDefault(t=>Vector3.Distance(t.at,p.Body.position)<=1.2f&&MeleeVisible(p.Body.position,t.at,p,t.collider));if(t.collider!=null){w.BatterHit=true;WildNoProcHit(p,t.id,15,t.at);TechniqueShove(p,t.id,w.BatterDir,8);}}if(wall)w.BatterAt=-100;}
                bool shrink=wildAfflictions.Any(a=>a.Kind=="wild-shrink-ray"&&a.Target==p.Seat.id&&a.Until>Time);if(shrink!=w.Shrunk){float scale=shrink?.8f:1.25f;p.Collider.height*=scale;p.Collider.radius*=scale;w.Shrunk=shrink;}
            }
        }
        private void UpdateChipGauges(NativePilot p)
        {
            bool planted=Has(p,"wild-planted"),opening=Has(p,"wild-opening-move"),doom=Has(p,"wild-doomsday-clock"),spin=Has(p,"wild-spin-up"),hay=Has(p,"wild-haymaker"),reply=Has(p,"wild-charged-reply"),spare=Has(p,"wild-spare-body");
            int count=(planted?1:0)+(opening?1:0)+(doom?1:0)+(spin?1:0)+(hay?1:0)+(reply?1:0)+(spare?1:0),i=0;
            // This is the live pilot buffer. Snapshot() already clones it for rendering/network/replay ownership.
            if(p.ChipGauges.Length!=count)p.ChipGauges=count==0?Array.Empty<NativeChipGauge>():new NativeChipGauge[count];
            if(planted)p.ChipGauges[i++]=new NativeChipGauge{Kind="wild-planted",Value=p.ExpansionStill<0?0:Mathf.Clamp01(Time-p.ExpansionStill)};
            if(opening)p.ChipGauges[i++]=new NativeChipGauge{Kind="wild-opening-move",Value=Time-p.ExpansionLastHit>=4?1:0};
            if(doom)p.ChipGauges[i++]=new NativeChipGauge{Kind="wild-doomsday-clock",Value=p.ExpansionDoomAt<0?0:Mathf.Clamp01((Time-p.ExpansionDoomAt)/3)};
            if(spin)p.ChipGauges[i++]=new NativeChipGauge{Kind="wild-spin-up",Value=p.Wild.Spin};
            if(hay)p.ChipGauges[i++]=new NativeChipGauge{Kind="wild-haymaker",Value=Time>=p.Wild.HayAt?1:0,Until=p.Wild.HayAt};
            if(reply)p.ChipGauges[i++]=new NativeChipGauge{Kind="wild-charged-reply",Value=p.Wild.ReplyUntil>Time?1:0,Until=p.Wild.ReplyUntil};
            if(spare)p.ChipGauges[i]=new NativeChipGauge{Kind="wild-spare-body",Value=p.Wild.DownUntil>Time?0:1,Until=p.Wild.DownUntil};
        }
    }
}
