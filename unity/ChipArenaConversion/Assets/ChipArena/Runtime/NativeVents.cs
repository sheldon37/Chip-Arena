using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public readonly List<NativeEntryView> Entries=new List<NativeEntryView>();
        public NativeEntryView Entry(int index)
        {if(Pve==null){var forced=Entries.LastOrDefault(e=>e.entry==index&&Time>=e.openAt-1.5f&&Time<=e.closeAt+1);return forced??(MapId=="siren"&&index<8?NativeVentRules.Ward(index,Time):null);}return Entries.Where(e=>e.entry==index).OrderByDescending(e=>NativeVentRules.Open(e,Time)).FirstOrDefault();}
        private int EntryQueue(int entry)=>Minions.Count(m=>m.Pve!=null&&!m.Pve.Travelling&&m.Pve.Entry.e==entry&&Time<m.Pve.ActiveAt);
        private bool VentHazard(int entry,float span=1.95f)
        {if(BossSongTime<0||entry<0||entry>=8)return false;for(float t=0;t<=span+.0001f;t+=.25f){var h=BossTrack.Vent(BossSongTime+t,SongVent(entry).song);if(h.active||h.warning>0)return true;}return false;}
        private (float spawn,float walk,float active,bool first) PlanEntry(PveEntry entry,string kind="shambler")
        {
            var view=Entries.Where(e=>e.entry==entry.e).OrderByDescending(e=>e.closeAt).FirstOrDefault();bool first=view==null||Time>=view.closeAt;float start=Time;
            if(first){view=new NativeEntryView{entry=entry.e,kind=entry.kind,mode="spawn",openAt=Time,closeAt=Time};Entries.Add(view);}
            else if(pveEntries.TryGetValue(entry.e,out float last))start=Math.Max(Time,last+(entry.kind=="vent"?.8f:.7f));
            var timeline=NativeWardEntries.Timeline(entry,first,kind);pveEntries[entry.e]=start;view.closeAt=Math.Max(view.closeAt,start+timeline.duration+1.2f);return(start,start+timeline.walk,start+timeline.duration,first);
        }
        private void DiveEntry(PveEntry entry)
        {var view=Entries.Where(e=>e.entry==entry.e).OrderByDescending(e=>e.closeAt).FirstOrDefault();if(view==null||Time>=view.closeAt){view=new NativeEntryView{entry=entry.e,kind="vent",mode="dive",openAt=Time,closeAt=Time};Entries.Add(view);}view.closeAt=Math.Max(view.closeAt,Time+2.1f);}
        public bool InShaft(NativePilot p)=>p.Death!=null&&(p.Death.style=="vent-fall"||p.Death.style=="vent-splat"&&Time<p.Death.landAt);
        private void StepVents()
        {
            if(!NativeMapRules.WardInstance(MapId)||Match.Phase!=NativePhase.Fight||Pve?.Holding==true)return;Entries.RemoveAll(e=>Time>e.closeAt+1);
            foreach(var p in Pilots)
            {
                if(p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss)
                {for(int e=0;e<8;e++)if(SongVent(e).pit&&NativeVentRules.Falls(Vec(p.Body.position),Vec(StageVents[e]),NativeVentRules.Open(Entry(e),Time)))
                 {Damage(p,null,100000,Vector3.down,0,false,true);if(p.Death==null)break;p.Death.style="vent-fall";p.Death.velocity=Vector3.down;p.Death.wound=null;p.Death.landAt=Time+(SandboxAllowed?Mathf.Clamp(SandboxSplatDelay,1,30):10);p.Death.kept=Pve?.Rows.ContainsKey(p.Seat.id)==true||SandboxAllowed;p.Wounds.Clear();p.Knockback=p.MoveVelocity=Vector3.zero;break;}}
                var d=p.Death;if(d==null||p.Hp>0||d.style!="vent-fall"||!d.kept||Time<d.landAt-.6f)continue;
                Vector3 spot=SplatSpot(p);d.seed=nextEvent++;d.style="vent-splat";d.at=d.landAt-.6f;d.position=spot;d.velocity=Vector3.down*22;p.Body.position=spot;
            }
        }
        private Vector3 SplatSpot(NativePilot target)
        {
            bool Clear(Vector3 spot)
            {if(StageVents.Any(v=>Math.Abs(v.x-spot.x)<1.9f&&Math.Abs(v.z-spot.z)<1.9f))return false;if(Pve?.Hidden==false&&Vector3.Distance(Pve.Boss.Body.position,spot)<3.9f)return false;if(Pilots.Any(p=>p!=target&&p.Hp<=0&&!InShaft(p)&&Vector3.Distance(p.Body.position,spot)<1.1f))return false;return RoomFor(target,spot);}
            var center=BossRescue+Vector3.up*.9f;if(Clear(center))return center;for(int ring=1;ring<=8;ring++)for(int i=0;i<8*ring;i++){float a=i/(8f*ring)*Mathf.PI*2;var spot=center+new Vector3(Mathf.Round(Mathf.Sin(a)*ring*1.25f*100)/100,0,Mathf.Round(Mathf.Cos(a)*ring*1.25f*100)/100);if(Clear(spot))return spot;}return center;
        }
        private InputFrame AvoidVents(NativePilot p,InputFrame input)
        {
            if(!NativeMapRules.WardInstance(MapId)||!p.Seat.bot||p.Seat.AuthorityBoss)return input;Vector3 move=new Vector3(input.X,0,input.Z);float length=move.magnitude;if(length<.01f)return input;Vector3 dir=move/length,pos=p.Body.position,look=pos+dir*1.3f;
            for(int e=0;e<8;e++)
            {var entry=Entry(e);if(entry==null||Time<entry.openAt-1.5f||Time>entry.closeAt+.4f)continue;Vector3 v=StageVents[e];bool Near(Vector3 q)=>Math.Abs(q.y-v.y)<2.5f&&Math.Abs(q.x-v.x)<1.9f&&Math.Abs(q.z-v.z)<1.9f;if(!Near(pos)&&!Near(look))continue;Vector3 outwards=new Vector3(pos.x-v.x,0,pos.z-v.z).normalized;if(outwards.sqrMagnitude<.01f)outwards=-dir;float side=Vector3.Dot(dir,new Vector3(-outwards.z,0,outwards.x))>=0?1:-1;var steered=Vector3.ClampMagnitude(new Vector3(-outwards.z,0,outwards.x)*side*length*.9f+outwards*length*.45f,1);input.X=steered.x;input.Z=steered.z;break;}return input;
        }
    }
}
