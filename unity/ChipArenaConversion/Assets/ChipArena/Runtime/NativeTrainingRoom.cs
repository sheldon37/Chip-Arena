using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public bool Training=>MapId=="training";
        private readonly Dictionary<int,TrainingDamageMeter> trainingMeters=new Dictionary<int,TrainingDamageMeter>();
        public TrainingReadout[] TrainingRemote=Array.Empty<TrainingReadout>();
        public bool TrainingDummy(NativePilot p)=>Training&&NativeTrainingRules.IsPad(p.Seat.id);
        public bool DevRosterPilot(NativePilot p)=>p.Seat.connected&&!p.Seat.AuthorityBoss&&!TrainingDummy(p);
        private TrainingPad TrainingPadOf(NativePilot p)=>TrainingDummy(p)?Map.trainingPads[p.Seat.id-NativeTrainingRules.FirstDummy]:null;
        private void TrainingInitialize()
        {trainingMeters.Clear();if(!Training)return;foreach(var p in Pilots){if(!Replica)p.Seat.alive=p.Hp>0&&p.Seat.connected;var pad=TrainingPadOf(p);if(pad!=null){p.Body.position=Vec(pad.pos);p.Yaw=pad.yaw;trainingMeters.Add(p.Seat.id,new TrainingDamageMeter(pad,p.Seat.id));}}}
        private void TrainingStep()
        {
            if(!Training||Replica)return;
            foreach(var p in Pilots)
            {
                if(p.Hp<=0){if(p.Seat.connected&&(p.Death==null||Time-p.Death.at>=5))SandboxRestore(p);continue;}
                var pad=TrainingPadOf(p);if(pad==null)continue;
                var anchor=Vec(NativeTrainingRules.Position(pad,Math.Max(0,Time-Match.FightAt)));
                if(!p.Body.position.Equals(anchor))p.Body.position=anchor;
                if(!p.Body.linearVelocity.Equals(Vector3.zero))p.Body.linearVelocity=Vector3.zero;
                p.Knockback=p.MoveVelocity=Vector3.zero;p.Yaw=pad.yaw;
            }
        }
        private void TrainingHit(NativePilot target,NativePilot source,float actual,bool head,string sourceKind)
        {if(LabSample!=null&&source==Local&&TrainingDummy(target))LabSample.Hit(Time,actual);if(Training&&trainingMeters.TryGetValue(target.Seat.id,out var meter))meter.Hit(Time,actual,head,source?.Seat.id??0,(source?.Seat.spec??"environment")+":"+sourceKind,source==null?0:Vector3.Distance(source.Body.position,target.Body.position));}
        public TrainingReadout[] TrainingReadouts()=>!Training?Array.Empty<TrainingReadout>():Replica?TrainingRemote:trainingMeters.Values.Select(m=>m.Read(Time)).ToArray();
        // Host-owned reset restores the original authored props, dummies and human spawn pad together.
        public bool ResetTraining(){if(!Training||Replica)return false;Reset();return true;}
    }
    public sealed partial class ConversionGame
    {
        private bool trainingWanted,trainingRoom;
        private void StartTraining()
        {
            if(server!=null)return;
            labPanel=false;
            int bots=prefs.bots;trainingWanted=true;
            try{StartMatch();}finally{trainingWanted=false;prefs.bots=bots;}
        }
        private void TrainingControls()
        {
            if(arena?.Training!=true||!menu)return;
            GUILayout.BeginArea(new Rect(20,Screen.height/GUI.matrix.m11-100,260,80));
            if(!Online&&GUILayout.Button("Chip Lab")){labBuild=SelectedLabBuild();if(arena.LabSample==null)arena.ResetLab(labBuild);labPanel=true;}
            else if(Online)GUILayout.Label("Training Room · targets respawn in 5 s");
            if(server==null&&GUILayout.Button("Reset targets, readouts and samples")){if(arena.LabSample!=null)arena.ResetLab(labBuild);else arena.ResetTraining();}
            GUILayout.EndArea();
        }
    }
}
