#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateWardTraversal()
        {
            if(Map.look!="quarantine")throw new InvalidOperationException("Ward traversal requires the imported map");
            var scene=root.scene;var hold=Match.HoldResolution;var failures=new List<string>();
            try
            {
                ReplicaScene=scene;Reset();Match.HoldResolution=()=>true;var physics=scene.GetPhysicsScene();var p=Local;
                foreach(var other in Pilots.Where(x=>x!=p)){other.Body.isKinematic=true;other.Collider.enabled=false;}
                foreach(var r in NativeMapRamps.Flights(Map))
                foreach(bool down in new[]{false,true})
                foreach(float fraction in new[]{-.1f,0f,.5f})
                foreach(int side in new[]{-1,0,1})
                foreach(string mode in new[]{"host walk","host sprint","guest walk","guest sprint","bot"})
                {
                    string label=r.Id+" "+(down?"down":"up")+" start="+fraction+" side="+side+" "+mode;
                    try
                    {
                        var direction=Vec(r.Direction)*(down?-1:1);var low=Vec(down?r.High:r.Low);var high=Vec(down?r.Low:r.High);
                        var lateral=Vector3.Cross(Vec(r.Direction),Vector3.up)*(side*(r.Width/2-NativeLocomotionRules.Radius-.02f));
                        var foot=fraction<0?low-direction:Vector3.Lerp(low,high,fraction);foot+=lateral;
                        if(!physics.Raycast(foot+Vector3.up*2,Vector3.down,out var support,3,WorldMask,QueryTriggerInteraction.Ignore))throw new Exception("start has no support");
                        var start=support.point+Vector3.up*(NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Radius+NativeLocomotionRules.Radius/support.normal.y+.025f);
                        p.Body.position=start;p.Body.linearVelocity=p.MoveVelocity=p.Knockback=Vector3.zero;p.Hp=(float)p.Stats["hp"];p.Stamina=1000;
                        p.Collider.enabled=true;p.Body.isKinematic=false;p.Body.useGravity=true;p.Grounded=p.GroundAttached=false;p.Input=default;
                        Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10});navRoutes.Clear();Physics.SyncTransforms();
                        bool guest=mode.StartsWith("guest",StringComparison.Ordinal),sprint=mode.EndsWith("sprint",StringComparison.Ordinal);
                        var state=new NativePredictionState{Position=Vec(start),Grounded=true,CanMove=true,CanSprint=true,Speed=6.8f,Gravity=23,JumpSpeed=8.5f};
                        foreach(var point in new[]{high+lateral,high+lateral+direction*2})
                        {
                            var target=point+Vector3.up*.9f;int ticks=0;float tolerance=mode=="bot"?.8f:.18f;
                            Vector3 Position()=>guest?Vec(state.Position):p.Body.position;
                            float Distance(){var d=target-Position();d.y=0;return d.magnitude;}
                            while(Distance()>tolerance&&ticks++<900)
                            {
                                var move=target-Position();move.y=0;move.Normalize();if(mode=="bot")move=StructureNavMove(p,target,move);
                                var input=new InputFrame{X=move.x,Z=move.z,Yaw=Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg,Sprint=sprint};
                                if(guest)state=NativePrediction.Step(state,input,(a,b)=>Vec(NativeGroundMotor.Constrain(physics,Vec(a),Vec(b))),at=>NativeGroundMotor.Probe(physics,Vec(at)).Found);
                                else{p.Input=input;ProbePilotGround(p);StepPilot(p);BeforePhysics();physics.Simulate(1f/60);AfterPhysics();}
                                double next=Match.Time+1d/60;Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=next,fightAt=next});
                            }
                            if(Distance()>tolerance||Math.Abs(Position().y-target.y)>.45f)throw new Exception("target "+target.ToString("F2")+" position "+Position().ToString("F2"));
                        }
                    }
                    catch(Exception e){failures.Add(label+": "+e.Message);}
                }
            }
            finally{Match.HoldResolution=hold;Reset();}
            if(failures.Count>0)throw new Exception("Ward traversal failed:\n"+string.Join("\n",failures));
            Debug.Log("PASS Ward traversal: eight routes, both directions, floor/bottom/mid-flight, centre/left/right, host/guest walk/sprint and bot");
        }
    }
}
#endif
