#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Executed by the Windows build gate in an isolated arena, using the actual capsule, StepPilot and PhysX.
        public void ValidateLocomotion()
        {
            var physics=root.scene.GetPhysicsScene();var p=Local;var fixtures=new List<GameObject>();var holdResolution=Match.HoldResolution;
            var origin=new Vector3(1000,20,1000);int ramps=0,stairs=0;
            void Check(bool ok,string why){if(!ok)throw new Exception("Native locomotion: "+why);}
            GameObject Box(Vector3 at,Vector3 size,Quaternion rotation)
            {
                var box=new GameObject("Locomotion gate collision");box.layer=PropLayer;box.transform.SetParent(root.transform);box.transform.SetPositionAndRotation(at,rotation);box.AddComponent<BoxCollider>().size=size;fixtures.Add(box);return box;
            }
            void Clear(){foreach(var box in fixtures)CapturedAnimation.Release(box);fixtures.Clear();Physics.SyncTransforms();}
            void Place(Vector3 at)
            {
                p.Body.position=at;p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;p.Body.useGravity=true;p.GroundAttached=p.Grounded=false;
                p.Input=default;p.Stamina=1000;Physics.SyncTransforms();
            }
            void Tick(Vector3 direction,bool sprint=false,bool jump=false)
            {
                p.Input=new InputFrame{X=direction.x,Z=direction.z,Yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,Sprint=sprint,Jump=jump};
                ProbePilotGround(p);StepPilot(p);BeforePhysics();physics.Simulate(1f/60);AfterPhysics();Match.Tick(1d/60);
                Check(p.Body!=null&&ReferenceEquals(Local,p)&&Match.Phase==NativePhase.Fight,"fixture match changed phase or replaced its pilot");
            }
            void Predict(Vector3 start,Vector3 direction,float distance,bool sprint,string label)
            {
                var state=new NativePredictionState{Position=Vec(start),Grounded=true,CanMove=true,CanSprint=true,Speed=6.8f,Gravity=23,JumpSpeed=8.5f};
                var input=new InputFrame{X=direction.x,Z=direction.z,Yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,Sprint=sprint};
                for(int i=0;i<240&&Vector3.Dot(Vec(state.Position)-start,direction)<distance;i++)
                    state=NativePrediction.Step(state,input,(a,b)=>Vec(NativeGroundMotor.Constrain(physics,Vec(a),Vec(b))),at=>NativeGroundMotor.Probe(physics,Vec(at)).Found);
                Check(Vector3.Dot(Vec(state.Position)-start,direction)>=distance-.05f&&state.Position.y>start.y+.15f,label+" guest prediction cannot climb");
            }
            void Hold(string label)
            {
                for(int i=0;i<20;i++)Tick(Vector3.zero);var at=p.Body.position;
                for(int i=0;i<90;i++)Tick(Vector3.zero);
                Check(Vector3.Distance(at,p.Body.position)<.06f,label+" slides while idle: "+Vector3.Distance(at,p.Body.position));
                Check(p.Knockback.magnitude<.001f,label+" invented contact knockback");
            }
            try
            {
                // A one-pilot match otherwise wins immediately and ResetRound destroys this fixture after five seconds.
                // Keep the real clock/motor/physics running, but suspend round resolution for this isolated gate only.
                Match.HoldResolution=()=>true;
                Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});
                foreach(var map in Content.maps.Where(m=>!m.unity))
                {
                    // Every distinct authored pitch/width in each map, including Canal/Spikevault/Lavaworks >45°.
                    var examples=map.blocks.Where(b=>b.id.Contains("stair")&&b.rotation!=null&&Math.Abs(b.rotation.x)+Math.Abs(b.rotation.z)>.001f)
                        .GroupBy(b=>Math.Round(b.rotation.x*b.rotation.x+b.rotation.z*b.rotation.z,4)+":"+b.size.x).Select(g=>g.First());
                    foreach(var b in examples)
                    {
                        Clear();var rotation=new Quaternion(b.rotation.x,b.rotation.y,b.rotation.z,b.rotation.w);var normal=rotation*Vector3.up;
                        var a=origin+rotation*new Vector3(0,b.size.y*.5f,-b.size.z*.5f);var z=origin+rotation*new Vector3(0,b.size.y*.5f,b.size.z*.5f);
                        var low=a.y<z.y?a:z;var high=a.y<z.y?z:a;var direction=high-low;direction.y=0;float length=direction.magnitude;direction.Normalize();
                        Box(origin,Vec(b.size),rotation);var surface=low+direction*.55f;surface.y=low.y-Vector3.Dot(surface-low,normal)/normal.y;
                        var start=surface+Vector3.up*(NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Radius+NativeLocomotionRules.Radius/normal.y+.025f);
                        foreach(bool sprint in new[]{false,true})
                        {
                            Place(start);float distance=Math.Min(length-1.7f,length*.3f);
                            for(int i=0;i<180&&Vector3.Dot(p.Body.position-start,direction)<distance;i++)Tick(direction,sprint);
                            float advance=Vector3.Dot(p.Body.position-start,direction);Check(advance>distance*.75f,map.id+" "+b.id+" cannot ascend ("+advance+" m)");
                            Check(p.Body.position.y>start.y+distance*.25f,map.id+" did not gain stair height");
                            Hold(map.id+" "+b.id);var top=p.Body.position;
                            for(int i=0;i<90&&Vector3.Dot(top-p.Body.position,direction)<distance*.5f;i++)Tick(-direction);
                            Check(p.Body.position.y<top.y-.1f,map.id+" cannot descend");
                            Predict(start,direction,distance,sprint,map.id+" "+b.id);
                        }
                        ramps++;
                    }
                }
                var brickfall=Content.maps.First(m=>m.id=="brickfall");
                foreach(string prefix in new[]{"hotel-stair-0-","ashby-stair-0-","bray-stair-0-"})
                {
                    Clear();var flight=brickfall.blocks.Where(b=>b.id.StartsWith(prefix,StringComparison.Ordinal)).OrderBy(b=>b.pos.y+b.size.y*.5f).ToArray();Check(flight.Length>4,"missing authored stair flight "+prefix);
                    var first=flight[0];var last=flight[flight.Length-1];var direction=Vec(flight[1].pos-first.pos);direction.y=0;direction.Normalize();
                    var shift=origin-Vec(first.pos);shift.y=origin.y-(first.pos.y+first.size.y*.5f-.15f);
                    foreach(var b in flight)Box(Vec(b.pos)+shift,Vec(b.size),new Quaternion(b.rotation.x,b.rotation.y,b.rotation.z,b.rotation.w));
                    Box(origin-Vector3.up*.2f,new Vector3(12,.4f,12),Quaternion.identity);
                    var end=Vec(last.pos)+shift;float topY=end.y+last.size.y*.5f;
                    Box(new Vector3(end.x,topY-.1f,end.z)+direction*1.1f,new Vector3(2.1f,.2f,2.1f),Quaternion.identity);
                    var start=new Vector3(origin.x,origin.y+.875f,origin.z)-direction*1.2f;
                    foreach(bool sprint in new[]{false,true})
                    {
                        Place(start);float distance=Vector3.Dot(end-start,direction)+.25f;
                        for(int i=0;i<240&&Vector3.Dot(p.Body.position-start,direction)<distance;i++)Tick(direction,sprint);
                        Check(Vector3.Dot(p.Body.position-start,direction)>distance-.7f&&p.Body.position.y>topY+.7f,prefix+" actual treads cannot be climbed");
                        Predict(start,direction,distance,sprint,prefix);
                        Hold(prefix);var high=p.Body.position;Tick(Vector3.zero,jump:true);Check(p.Body.useGravity&&p.Body.linearVelocity.y>7,"jump cancelled on stairs");
                        for(int i=0;i<18;i++)Tick(Vector3.zero);Check(p.Body.position.y>high.y+.7f,"stair jump did not rise");
                    }
                    stairs++;
                }
                // Clearance: same capsule against a tall wall, and a short step with too little headroom.
                foreach(bool lowCeiling in new[]{false,true})
                {
                    Clear();Box(origin-Vector3.up*.2f,new Vector3(8,.4f,8),Quaternion.identity);
                    Box(origin+new Vector3(0,lowCeiling?.075f:1.5f,1),new Vector3(3,lowCeiling?.15f:3,1),Quaternion.identity);
                    if(lowCeiling)Box(origin+new Vector3(0,1.84f,.4f),new Vector3(4,.1f,4),Quaternion.identity);
                    Place(origin+Vector3.up*.875f);for(int i=0;i<90;i++)Tick(Vector3.forward);
                    Check(p.Body.position.z<origin.z+.5f&&p.Body.position.y<origin.y+1,lowCeiling?"stepped through ceiling":"climbed a wall");
                }
                Clear();Box(origin-Vector3.up*.2f,new Vector3(3,.4f,3),Quaternion.identity);Place(origin+Vector3.up*.875f);
                for(int i=0;i<55;i++)Tick(Vector3.forward);Check(!p.GroundAttached&&p.Body.useGravity&&p.Body.position.y<origin.y,"host hovered after leaving ledge");
                Debug.Log("PASS native locomotion: "+ramps+" authored ramp variants across old maps, "+stairs+" actual Brickfall flights, host/guest walking/sprinting/up/down/idle, jumps, walls, low ceilings and ledge gravity (real StepPilot/PhysX)");
            }
            finally{Match.HoldResolution=holdResolution;Clear();}
        }
    }
}
#endif
