#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Real map, real capsule and StepPilot/PhysX, not isolated replacement stair fixtures.
        public void ValidateCyberCityExits()
        {
            if(MapId!="cybercity")throw new InvalidOperationException("Cut exit gate requires Cyber City");
            var scene=root.scene;var hold=Match.HoldResolution;var failures=new List<string>();
            try
            {
                ReplicaScene=scene;Reset();Match.HoldResolution=()=>true;
                var physics=scene.GetPhysicsScene();var p=Local;
                foreach(var other in Pilots.Where(x=>x!=p)){other.Body.isKinematic=true;other.Collider.enabled=false;}
                void Check(bool ok,string why){if(!ok)throw new Exception(why);}
                Vector3 Start()
                {
                    // (-24,0) is on the .30m guideway. Resolve its surface rather than embed feet at y=-4.
                    Check(physics.Raycast(new Vector3(-24,-2,0),Vector3.down,out var hit,3,WorldMask,QueryTriggerInteraction.Ignore),"mid-cut floor missing");
                    return hit.point+Vector3.up*(NativeLocomotionRules.HalfHeight+.025f);
                }
                void Place(Vector3 start)
                {
                    p.Body.position=start;p.Body.linearVelocity=p.MoveVelocity=p.Knockback=Vector3.zero;
                    p.Hp=(float)p.Stats["hp"];p.Seat.alive=true;p.Body.isKinematic=false;p.Collider.enabled=true;p.Death=null;
                    p.Body.useGravity=true;p.Grounded=p.GroundAttached=false;p.Input=default;p.Stamina=1000;
                    Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10});
                    navRoutes.Clear();Physics.SyncTransforms();
                }
                void Tick(Vector3 direction,bool sprint)
                {
                    // Hold hazard clocks at the quiet phase. This gate checks escape geometry, not train timing.
                    p.Input=new InputFrame{X=direction.x,Z=direction.z,Yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,Sprint=sprint};
                    ProbePilotGround(p);StepPilot(p);BeforePhysics();physics.Simulate(1f/60);AfterPhysics();
                    Check(p.Body!=null&&p.Hp>0&&ReferenceEquals(Local,p),"pilot replaced or killed");
                    double next=Match.Time+1d/60;Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=next,fightAt=next});
                }
                foreach(var flight in NativeCyberCityStairs.Flights(Map))
                {
                    float sign=Math.Sign(flight.Landing.z);
                    var points=new[]{new V3(flight.Landing.x,-4,sign*1.8f),flight.Landing,
                        flight.Low,flight.High,flight.Street,new V3(flight.Street.x,0,sign*7)};
                    foreach(string approach in new[]{"mid-cut","landing centre","landing left","landing right","mid-stair centre","mid-stair left","mid-stair right"})
                    foreach(string mode in new[]{"host walk","host sprint","guest walk","guest sprint","bot"})
                    {
                        try
                        {
                            if(mode=="bot"&&approach!="mid-cut")continue;
                            float side=approach.EndsWith("left",StringComparison.Ordinal)?-1:approach.EndsWith("right",StringComparison.Ordinal)?1:0;
                            // Leave 2cm clearance at either side for the actual .42m capsule.
                            var lateral=Vector3.Cross(Vec(flight.Direction),Vector3.up)*(side*(flight.Width*.5f-NativeLocomotionRules.Radius-.02f));
                            var route=points;
                            var start=Start();
                            if(approach!="mid-cut")
                            {
                                bool middle=approach.StartsWith("mid-stair",StringComparison.Ordinal);
                                var foot=middle?Vector3.Lerp(Vec(flight.Low),Vec(flight.High),.5f):Vec(flight.Landing);
                                foot+=lateral;
                                Check(physics.Raycast(foot+Vector3.up*2,Vector3.down,out var support,3,WorldMask,QueryTriggerInteraction.Ignore),"start support missing");
                                // A capsule tangent to a slope needs R / normal.y, not R, above its pole.
                                start=support.point+Vector3.up*(NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Radius+NativeLocomotionRules.Radius/support.normal.y+.025f);
                                route=middle?new[]{Vec(Vec(flight.High)+lateral),Vec(Vec(flight.Street)+lateral),new V3(flight.Street.x,0,sign*7)}:
                                    new[]{Vec(Vec(flight.Low)+lateral),Vec(Vec(flight.High)+lateral),Vec(Vec(flight.Street)+lateral),new V3(flight.Street.x,0,sign*7)};
                            }
                            Place(start);bool guest=mode.StartsWith("guest",StringComparison.Ordinal),sprint=mode.EndsWith("sprint",StringComparison.Ordinal);
                            // Bots target graph nodes (1m grid), not the caller's exact world-space point.
                            float tolerance=mode=="bot"?.6f:.18f;
                            var state=new NativePredictionState{Position=Vec(p.Body.position),Grounded=true,CanMove=true,CanSprint=true,Speed=6.8f,Gravity=23,JumpSpeed=8.5f};
                            foreach(var point in route)
                            {
                                var target=Vec(point)+Vector3.up*.875f;int ticks=0;
                                Vector3 Position()=>guest?Vec(state.Position):p.Body.position;
                                float Distance(){var d=target-Position();d.y=0;return d.magnitude;}
                                while(Distance()>tolerance&&ticks++<900)
                                {
                                    var delta=target-Position();delta.y=0;var direction=delta.normalized;
                                    if(mode=="bot")direction=StructureNavMove(p,target,direction);
                                    if(guest)
                                    {
                                        var input=new InputFrame{X=direction.x,Z=direction.z,Yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,Sprint=sprint};
                                        state=NativePrediction.Step(state,input,(a,b)=>Vec(NativeGroundMotor.Constrain(physics,Vec(a),Vec(b))),at=>NativeGroundMotor.Probe(physics,Vec(at)).Found);
                                    }
                                    else Tick(direction,sprint);
                                    Check(Position().y>-4,"fell below cut floor");
                                }
                                Check(Distance()<=tolerance,"stalled before "+target.ToString("F2")+" at "+Position().ToString("F2"));
                                // A horizontal arrival alone must not pass while still beneath the street.
                                Check(Math.Abs(Position().y-target.y)<.4f,"wrong elevation at "+target.ToString("F2")+": "+Position().ToString("F2"));
                            }
                            Check((guest?state.Position.y:p.Body.position.y)>.7f,"did not reach promenade");
                            Debug.Log("PASS Cyber City cut exit "+flight.Id+" "+approach+" "+mode+": reached street and promenade");
                        }
                        catch(Exception e){failures.Add(flight.Id+" "+approach+" "+mode+": "+e.Message);}
                    }
                }
            }
            finally{Match.HoldResolution=hold;Reset();}
            if(failures.Count>0)throw new Exception("Cyber City cut traversal failed:\n"+string.Join("\n",failures));
        }
    }
}
#endif
