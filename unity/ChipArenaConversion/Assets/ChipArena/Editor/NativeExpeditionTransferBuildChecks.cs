using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckExpeditionCohortPlacement(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Cohort placement: "+why);}
            using(var registry=new ExpeditionAreaRegistry<NativeExpeditionAreaRuntime>("exp-city","exp-ward-zero",area=>
                new NativeExpeditionAreaRuntime(area,content,area=="exp-city"?"expedition-city":"expedition-ward-zero",match=>
                {
                    match.Cooperative=true;match.ShowIntros=false;
                    foreach(int id in area=="exp-city"?new[]{1,2}:new[]{3,4})match.Seats.Add(new NativeSeat{id=id,name="Transfer test "+id,spec="pistol"});
                },_=>EditorSceneManager.NewPreviewScene(),scene=>EditorSceneManager.ClosePreviewScene(scene))))
            {
                var hub=registry.Acquire("exp-city");var ward=registry.Acquire("exp-ward-zero");
                foreach(var runtime in new[]{hub,ward}){runtime.Ready(a=>{a.Match.HoldResolution=()=>true;a.Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});});runtime.SetOccupants(2);}
                var layout=NativeExpeditionLayout.Parse(Resources.Load<TextAsset>("ExpeditionLayout").text);
                var pilot=hub.Arena.Pilots.Single(p=>p.Seat.id==1);var stay=hub.Arena.Pilots.Single(p=>p.Seat.id==2);
                pilot.QReady=31;pilot.EReady=37;pilot.Dots["900:fire"]=new NativeDot{Source=900,Dps=4,Next=11,Until=18};pilot.TransferCooldowns["1:fixture"]=22;pilot.TransferProcs.Add("1:panic");
                var hp=pilot.Hp;var origin=pilot.Body.position;Physics.SyncTransforms();
                var slot=layout.Area("exp-ward-zero").doors.Single().entries.FirstOrDefault(p=>ward.Arena.TransferSlotClear(p));
                Check(slot!=null,"a destination slot is available");int released=0;
                var placement=new NativeExpeditionCohortPlacement(hub,ward,new Dictionary<int,ExpeditionPoint>{{1,slot}},()=>released++);
                Check(pilot.ExpeditionTransferHeld&&!stay.ExpeditionTransferHeld,"only cohort is held");
                Check(placement.Validate()&&placement.BlockedReason()==null,"placement validated before commit");placement.Apply();
                Check(ReferenceEquals(ward.Arena.Pilots.Single(p=>p.Seat.id==1),pilot)&&!hub.Arena.Pilots.Contains(pilot),"same pilot moves exactly once");
                Check(pilot.Body.gameObject.scene==ward.Scene&&stay.Body.gameObject.scene==hub.Scene,"subset scene ownership");
                Check(pilot.QReady==31&&pilot.EReady==37&&pilot.Dots["900:fire"].Until==18&&pilot.Dots["900:fire"].Source==0&&pilot.TransferCooldowns["1:fixture"]==22&&pilot.TransferProcs.Contains("1:panic"),"ability deadlines, DOT damage and chip procs survive without foreign ownership");
                Check(pilot.Hp==hp&&!pilot.ExpeditionTransferHeld&&hub.Occupants==1&&ward.Occupants==3,"vitals and occupancy preserved");
                Check(hub.Arena.Match.Seat(1)==null&&ward.Arena.Match.Seat(1)==pilot.Seat&&released==1,"seat and reservation committed");
                placement.Rollback();placement.Rollback();
                Check(pilot.Body.gameObject.scene==hub.Scene&&pilot.Body.position==origin&&hub.Occupants==2&&ward.Occupants==2&&released==1,"rollback restores source once");
                Check(!placement.Validate()&&placement.BlockedReason()!=null,"completed transfer reports a refusal reason");
                // A revived pilot keeps the historical death record used by revival presentation.
                var slots=layout.Area("exp-ward-zero").doors.Single().entries.Where(p=>ward.Arena.TransferSlotClear(p)).Take(2).ToArray();
                Check(slots.Length==2,"two destination slots are available");
                for(int cycle=0;cycle<3;cycle++)
                {
                    foreach(var member in new[]{pilot,stay})
                    {
                        member.Death=new NativeDeath{id=member.Seat.id,seed=700+cycle*2+member.Seat.id,at=hub.Arena.Time-1};
                        member.RevivedAt=hub.Arena.Time;member.Seat.alive=true;
                    }
                    var party=new NativeExpeditionCohortPlacement(hub,ward,new Dictionary<int,ExpeditionPoint>{{1,slots[0]},{2,slots[1]}},()=>{});
                    Check(party.Validate(),"revived party validates, cycle "+cycle);party.Apply();
                    Check(ward.Arena.Pilots.Contains(pilot)&&ward.Arena.Pilots.Contains(stay)&&hub.Occupants==0&&ward.Occupants==4,"entire revived party transfers");
                    Check(pilot.Death!=null&&stay.Death!=null,"transfer retains revival history");party.Rollback();
                }
                foreach(bool deadSeat in new[]{false,true})
                {
                    pilot.Seat.alive=!deadSeat;pilot.Hp=deadSeat?hp:0;
                    bool deadRefused=false;try{new NativeExpeditionCohortPlacement(hub,ward,new Dictionary<int,ExpeditionPoint>{{1,slot}},()=>{});}catch(InvalidOperationException){deadRefused=true;}
                    Check(deadRefused&&!pilot.ExpeditionTransferHeld,"current death state still prevents transfer");
                }
                pilot.Seat.alive=true;pilot.Hp=hp;
                ward.Arena.Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=11,fightAt=0});
                bool refused=false;try{new NativeExpeditionCohortPlacement(hub,ward,new Dictionary<int,ExpeditionPoint>{{1,slot}},()=>{});}catch(InvalidOperationException){refused=true;}
                Check(refused&&!pilot.ExpeditionTransferHeld&&pilot.Body.gameObject.scene==hub.Scene,"unequal clocks refused before mutation");
                Debug.Log("PASS native cohort placement fixture: subset scene/seat movement and rollback. Not live director, transport, ability migration or Windows acceptance.");
            }
        }
    }
}
