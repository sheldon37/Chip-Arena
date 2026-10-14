using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private sealed class PveAcidState
        {public NativeAcidRecord Record;public bool Landed;public double PoisonAt,CorrodeAt;}
        private readonly List<PveAcidState> pveAcid=new List<PveAcidState>(16);private int nextAcid;
        public int PveAcidCount=>pveAcid.Count;
        public NativeAcidRecord PveAcidAt(int index)=>pveAcid[index].Record;
        private NativeAcidRecord[] AcidFrame()=>pveAcid.Select(a=>a.Record.Copy()).ToArray();
        private void ApplyAcid(NativeAcidRecord[] records)
        {pveAcid.Clear();if(records!=null)foreach(var r in records)pveAcid.Add(new PveAcidState{Record=r.Copy()});}
        private void LaunchPveSpit(NativeMinion m)
        {
            var a=m.Pve;var target=a.Target;if(target==null||target.Hp<=0||!target.Seat.connected||pveAcid.Count>=16)return;
            var floor=m.Body.position-Vector3.up*(m.Collider.height/2);var from=floor+Vector3.up*(a.Crawl?.45f:1.2f)+Vec(V3.Forward(m.Yaw))*.3f;
            var aim=Vec(NativeSpitterRules.Lead(Vec(floor),Vec(target.Body.position-Vector3.up*(target.Collider.height/2)),Vec(target.Body.linearVelocity)));
            var to=GroundPoint(aim)+Vector3.up*.025f;var flat=to-from;flat.y=0;
            if(flat.magnitude>NativeSpitterRules.Range+.01f)return;
            float flight=NativeHordeFx.Flight(flat.magnitude);
            // Validate the authored arc against world collision before committing. This prevents a lob
            // crossing a floor/ceiling or wall; no pilot/prop is damaged before its landing clock.
            var previous=from;var nf=new System.Numerics.Vector3(from.x,from.y,from.z);var nt=new System.Numerics.Vector3(to.x,to.y,to.z);
            for(int i=1;i<=24;i++)
            {NativeHordeFx.Glob(nf,nt,flight,flight*i/24,out var v);var current=new Vector3(v.X,v.Y,v.Z);if(SceneLinecast(previous,current,WorldMask))return;previous=current;}
            var r=new NativeAcidRecord{devMonster=m.DevMonster,devTarget=m.DevMonster&&m.DevBehaviour!="frenzy"?target.Seat.id:0,id=++nextAcid,actor=m.Id,from=Vec(from),to=Vec(to),at=Time,flight=flight,damageScale=a.DamageScale};
            pveAcid.Add(new PveAcidState{Record=r,PoisonAt=Time+flight,CorrodeAt=Time+flight+NativeSpitterRules.CorrosionInterval});
            Emit(EventKind.Impact,m.Owner,m.Id,to,flight,"minion-spit",radius:NativeSpitterRules.Radius,from:from);
        }
        private bool AcidTouches(NativePilot p,NativeAcidRecord r,float radius)
        {return (!r.devMonster||r.devTarget==0||r.devTarget==p.Seat.id)&&p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss&&NativeSpitterRules.Touch(r.to,Vec(p.Body.position),p.Collider.height/2,p.Collider.radius,radius)&&!SceneLinecast(Vec(r.to)+Vector3.up*.1f,p.Body.position,WorldMask);}
        private void StepPveAcid()
        {
            for(int i=pveAcid.Count-1;i>=0;i--)
            {
                var a=pveAcid[i];var r=a.Record;double land=(double)r.at+r.flight,end=land+NativeSpitterRules.Duration;
                if(Time<land)continue;
                if(!a.Landed)
                {
                    a.Landed=true;
                    foreach(var p in Pilots)if(AcidTouches(p,r,NativeSpitterRules.DirectRadius))Damage(p,null,NativeSpitterRules.Damage*r.damageScale,Vector3.zero,0,false,primary:false,damageKind:r.devMonster?"dev-monster":"acid");
                    if(!r.devMonster)CorrodePve(r,NativeSpitterRules.DirectRadius,NativeSpitterRules.DirectCorrosion);
                }
                while(a.PoisonAt<=Time+1e-6&&a.PoisonAt<end)
                {foreach(var p in Pilots)if(AcidTouches(p,r,NativeSpitterRules.Radius))RefreshPvePoison(p,NativeSpitterRules.Dps*r.damageScale,NativeSpitterRules.DotDuration,r.devMonster);a.PoisonAt+=NativeSpitterRules.DotInterval;}
                while(a.CorrodeAt<=Time+1e-6&&a.CorrodeAt<=end)
                {if(!r.devMonster)CorrodePve(r,NativeSpitterRules.Radius,NativeSpitterRules.Corrosion);a.CorrodeAt+=NativeSpitterRules.CorrosionInterval;}
                if(Time>end)pveAcid.RemoveAt(i);
            }
        }
        private void CorrodePve(NativeAcidRecord acid,float radius,float damage)
        {
            // Fracture can mutate Props. Iterate a bounded reused candidate snapshot before dealing damage.
            acidProps.Clear();foreach(var p in Props)if(p!=null&&!p.Indestructible&&!p.Fragment&&p.isActiveAndEnabled)acidProps.Add(p);
            foreach(var p in acidProps)
            {if(p==null||p.Fragment||!p.isActiveAndEnabled)continue;var col=p.GetComponent<Collider>();if(col==null||!col.enabled)continue;var at=Vec(acid.to)+Vector3.up*.05f;var point=col.ClosestPoint(at);if((point-at).sqrMagnitude>radius*radius||SceneLinecast(at,point,out var hit,WorldMask)&&hit.collider!=col)continue;DamageProp(p,damage,Vector3.zero,point,null,"acid");}
        }
        private readonly List<NativePropBody> acidProps=new List<NativePropBody>();
        private void RefreshPvePoison(NativePilot target,double dps,double duration,bool dev=false)
        {
            string key=dev?"dev-monster:poison":"pve:poison";target.Dots.TryGetValue(key,out var old);bool live=old!=null&&old.Until>=Match.Time;
            if(!live)target.Dots[key]=new NativeDot{Source=0,Dps=dps,Next=Match.Time+.25,Until=Match.Time+duration};
            else{old.Dps=Math.Max(old.Dps,dps);old.Until=Math.Max(old.Until,Match.Time+duration);}
        }
    }
}
