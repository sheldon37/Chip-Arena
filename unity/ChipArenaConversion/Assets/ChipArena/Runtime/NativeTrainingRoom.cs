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
        private bool TrainingDummy(NativePilot p)=>Training&&p.Seat.id>=NativeTrainingRules.FirstDummy&&p.Seat.id<NativeTrainingRules.FirstDummy+(Map.trainingPads?.Length??0);
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
                p.Body.position=Vec(NativeTrainingRules.Position(pad,Math.Max(0,Time-Match.FightAt)));p.Body.linearVelocity=Vector3.zero;p.Knockback=p.MoveVelocity=Vector3.zero;p.Yaw=pad.yaw;
            }
        }
        private void TrainingHit(NativePilot target,NativePilot source,float actual,bool head,string sourceKind)
        {if(Training&&trainingMeters.TryGetValue(target.Seat.id,out var meter))meter.Hit(Time,actual,head,source?.Seat.id??0,(source?.Seat.spec??"environment")+":"+sourceKind,source==null?0:Vector3.Distance(source.Body.position,target.Body.position));}
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
            int bots=prefs.bots;trainingWanted=true;
            try{StartMatch();}finally{trainingWanted=false;prefs.bots=bots;}
        }
        private void TrainingControls()
        {
            if(arena?.Training!=true||!menu)return;
            GUILayout.BeginArea(new Rect(20,Screen.height/GUI.matrix.m11-100,260,80));
            GUILayout.Label("Training Room · targets respawn in 5 s");
            if(server==null&&GUILayout.Button("Reset targets, readouts and samples"))arena.ResetTraining();
            GUILayout.EndArea();
        }
    }
}
