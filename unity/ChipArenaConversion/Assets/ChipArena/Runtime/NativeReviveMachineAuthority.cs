using System;
using System.Linq;
using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private void BindReviveMachineRules(ExpeditionView view)
        {
            ReviveMachineRequest=target=>
            {
                var commands=ExpeditionCommands;if(commands==null)return "No Expedition connection.";
                var result=commands.Send(new ExpeditionCommand(ExpeditionIntent.Reprint,target));
                return result.state==ExpeditionCommandState.Pending?null:result.reason??"Revive refused.";
            };
            NativeReviveMachine.LiveSpits=view?.machineRevive?.spit==null?Array.Empty<NativeReviveSpit>():new[]{view.machineRevive.spit};
            NativeReviveMachine.Fill=(board,v)=>
            {
                board.Cost=v.reviveFree?0:NativeReviveMachine.Price;
                if(v.revivePermadeath)board.Blocked="PERMADEATH: REPRINTS DISABLED";
                board.Spits=v.machineRevive?.spit==null?Array.Empty<NativeReviveSpit>():new[]{v.machineRevive.spit};
                foreach(var row in board.Dead)
                {
                    var death=v.Member(row.Account)?.death;if(death==null)continue;
                    row.Cause=death.Cause;row.Killer=death.Killer;row.Body=death.Body;row.Located=death.Located;
                    row.Place=death.Place;row.DiedAt=death.DiedAt;row.Area=death.Area;row.AreaName=death.AreaName;
                }
                if(board.Blocked==null)board.Blocked=v.machineRevive!=null?"MACHINE BUSY":v.mine?.alive!=true?"A LIVING PILOT MUST PAY":v.mine.safe!=true?"RETURN TO THE SANCTUARY":board.Wallet<board.Cost?"NOT ENOUGH MONEY":null;
            };
        }
    }
    public sealed partial class NativePilot { public string MachineDeathCause,MachineKiller; }
    public sealed partial class NativeArena
    {
        private int reviveMachineSerial;
        private bool MachineHeld(NativePilot p)
        {
            return Expedition?.Session.Run.MachineHeld(p.Seat.id,Time)==true;
        }
        private ExpeditionCommandResult RequestMachineRevive(string payer,string target)
        {
            ExpeditionCommandResult Refuse(string reason)=>ExpeditionCommandResult.Of(ExpeditionCommandState.Refused,reason);
            var run=Expedition.Session.Run;var p=ExpeditionPilotFor(payer);var state=run.PartySnapshot();
            if(p==null||p.Hp<=0||!p.Seat.connected||!Expedition.Sanctuary(payer)||!NativeReviveMachine.Spot(MapId,out var at,out float yaw))return Refuse("Use the machine in the sanctuary while alive.");
            var frame=NativeReviveMachine.Frame(at,yaw);var use=NVector.Transform(NativeReviveMachine.UsePoint,frame);
            var feet=p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight;
            if(new Vector2(feet.x-use.X,feet.z-use.Z).sqrMagnitude>NativeReviveMachine.UseRange*NativeReviveMachine.UseRange||Mathf.Abs(feet.y-use.Y)>2)return Refuse("Move closer to the machine.");
            if(run.HasMutator("permadeath"))return Refuse("PERMADEATH: reprints disabled.");
            if(state.machineRevive!=null)return Refuse("The machine is busy.");
            var dead=state.members.FirstOrDefault(m=>m.account==target);
            if(dead==null||dead.alive||!dead.connected||dead.extracted||dead.transferring)return Refuse("That pilot cannot be revived.");
            int cost=run.HasMutator("second-wind")?0:NativeReviveMachine.Price;
            if(state.members.First(m=>m.account==payer).wallet<cost)return Refuse("Not enough money: revive costs $"+cost+".");
            // Search only the mat; no fallback to a remote spawn slot. Keep one pilot width clear.
            NVector? land=null;float step=2*NativeLocomotionRules.Radius+.15f;
            foreach(var offset in new[]{NVector.Zero,new NVector(-step,0,0),new NVector(step,0,0),new NVector(0,0,-step),new NVector(0,0,step)})
            {
                var candidate=NVector.Transform(NativeReviveMachine.DefaultLand+offset,frame);var point=new V3(candidate.X,candidate.Y,candidate.Z);
                if(ExpeditionSupported(point,NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight)&&Pilots.All(other=>other.Hp<=0||Vector3.Distance(other.Body.position,new Vector3(point.x,point.y+NativeLocomotionRules.HalfHeight,point.z))>=step)){land=candidate;break;}
            }
            if(land==null)return Refuse("Clear the landing mat first.");
            var spot=land.Value;var spit=new NativeReviveSpit{Id=++reviveMachineSerial,Account=target,Seat=Expedition.Session.Sources.Seat?.Invoke(target)??0,At=Time,Land=new V3(spot.X,spot.Y,spot.Z)};
            run.SetSafe(payer,true);
            return run.BeginMachineRevive(payer,spit)?ExpeditionCommandResult.Sent:Refuse("Revive refused: pilot or machine state changed.");
        }
        internal void StepReviveMachine()
        {
            var run=Expedition.Session.Run;var job=run.MachineRevive;
            if(job?.spit==null||expeditionArea.id!=run.Snapshot().world)return;
            if(run.StepMachineRevive(Time))
            {
                var p=ExpeditionPilotFor(job.spit.Account);
                if(p!=null){RevivePve(p,Vec(job.spit.Land)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f),true);expeditionDown.Remove(job.spit.Account);p.Death=null;}
            }
            var held=ExpeditionPilotFor(job.spit.Account);
            if(held!=null&&held.Hp>0&&Time<job.spit.At+NativeReviveMachine.HoldUntil)
            {held.Input=new InputFrame{Yaw=held.Yaw,Pitch=held.Pitch};held.Body.position=Vec(job.spit.Land)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);held.Knockback=held.MoveVelocity=held.Body.linearVelocity=Vector3.zero;}
        }
        private void RecordMachineDeath(NativePilot p,string account)
        {
            var d=p.Death;var at=d?.position??p.Body.position;
            Expedition.Session.Run.RecordMachineDeath(account,new NativeReviveDead{Account=account,Seat=p.Seat.id,Name=p.Seat.name,Weapon=p.Seat.spec,
                Area=expeditionArea.id,AreaName=Content.Map(MapId).name,Place=Content.Map(MapId).name,Body=new V3(at.x,at.y-NativeLocomotionRules.HalfHeight,at.z),Located=true,
                DiedAt=(float)Expedition.Session.Run.Elapsed,Killer=p.MachineKiller,Cause=p.MachineDeathCause??NativeReviveMachine.CauseLine(d?.damageKind,null,d?.hazardId)});
        }
    }
}
