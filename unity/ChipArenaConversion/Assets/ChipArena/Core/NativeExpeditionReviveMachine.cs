using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ExpeditionMachineRevive
    {
        public NativeReviveSpit spit;public string payer;public int cost;public bool completed;
        public bool Valid()=>spit!=null&&spit.Id>0&&spit.Seat>0&&ExpeditionValidation.Id(spit.Account)&&ExpeditionValidation.Id(payer)&&(cost==0||cost==NativeReviveMachine.Price)&&spit.At>=0&&MathUtil.Finite(spit.At)&&MathUtil.Finite(spit.Land.x)&&MathUtil.Finite(spit.Land.y)&&MathUtil.Finite(spit.Land.z);
        public ExpeditionMachineRevive Copy()=>new ExpeditionMachineRevive{payer=payer,cost=cost,completed=completed,spit=spit==null?null:new NativeReviveSpit{Id=spit.Id,Seat=spit.Seat,Account=spit.Account,At=spit.At,Land=spit.Land}};
    }
    public sealed partial class NativeExpedition
    {
        public ExpeditionMachineRevive MachineRevive=>s.machineRevive?.Copy();
        public bool MachineHeld(int seat,float clock)=>s.machineRevive?.spit!=null&&s.machineRevive.spit.Seat==seat&&clock<s.machineRevive.spit.At+NativeReviveMachine.HoldUntil;
        public void RecordMachineDeath(string account,NativeReviveDead death)
        {var p=Member(account);if(p==null||p.alive||death==null)return;p.death=death.Copy();Changed();}
        // Proximity and landing clearance are measured by the arena before this atomic transaction.
        public bool BeginMachineRevive(string payer,NativeReviveSpit spit)
        {
            var buyer=Member(payer);var target=Member(spit?.Account);
            int cost=HasMutator("second-wind")?0:NativeReviveMachine.Price;
            if(!Mutable||Area!=s.world||s.machineRevive!=null||HasMutator("permadeath")||!Live(buyer)||!buyer.safe||
                target==null||target.alive||target.extracted||!target.connected||target.transferring||buyer.wallet<cost||spit.Id<1||spit.Seat<1||
                !ExpeditionValidation.Number(spit.At)||spit.At<0||!ExpeditionValidation.Number(spit.Land.x)||!ExpeditionValidation.Number(spit.Land.y)||!ExpeditionValidation.Number(spit.Land.z))return false;
            if(SplitAreas&&target.area!=buyer.area&&ReprintPlacement?.Invoke(payer,target.account)!=true)return false;
            buyer.wallet-=cost;target.carriedBy=null;
            s.machineRevive=new ExpeditionMachineRevive{payer=payer,cost=cost,spit=spit}.Copy();Changed();return true;
        }
        public bool StepMachineRevive(float clock)
        {
            var job=s.machineRevive;if(job?.spit==null||Area!=s.world)return false;
            var target=Member(job.spit.Account);
            if(!job.completed&&(target==null||!target.connected||target.extracted))
            {var buyer=Member(job.payer);if(buyer!=null)buyer.wallet+=job.cost;s.machineRevive=null;Changed();return false;}
            bool revived=false;
            if(!job.completed&&clock>=job.spit.At+NativeReviveMachine.Spit)
            {target.alive=true;target.safe=true;target.carriedBy=null;job.completed=true;revived=true;Changed();}
            if(clock>=job.spit.At+NativeReviveMachine.End){s.machineRevive=null;Changed();}
            return revived;
        }
    }
}
