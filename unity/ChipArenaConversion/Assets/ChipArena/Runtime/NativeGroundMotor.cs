using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Queries only: the host retains its dynamic capsule, gravity in flight, and physical debris contacts.
    // The same step clearance and surface rules constrain the guest's kinematic prediction.
    public static class NativeGroundMotor
    {
        private const float Radius=NativeLocomotionRules.Radius,Half=NativeLocomotionRules.HalfHeight;
        private const float Skin=NativeLocomotionRules.Skin,Step=NativeLocomotionRules.StepHeight,Reach=NativeLocomotionRules.GroundReach;
        private const float Pole=Half-Radius,Lift=.5f;
        private static readonly Collider[] overlaps=new Collider[16];
        public struct Ground {public bool Found;public float Gap;public Vector3 Normal,Point;}
        private static bool Walkable(Vector3 n)=>NativeLocomotionRules.Walkable(NativeArena.Vec(n));
        private static Vector3 SurfaceNormal(RaycastHit hit)
        {
            // Sphere/capsule edge normals may point from the contact toward the sphere centre. Refine against
            // that collider's actual face before deciding whether it is a walkable floor.
            return hit.collider.Raycast(new Ray(hit.point+Vector3.up*.06f,Vector3.down),out var face,.12f)?face.normal:hit.normal;
        }
        public static Ground Probe(PhysicsScene physics,Vector3 position)
        {
            if(physics.SphereCast(position+Vector3.up*(Lift-Pole),Radius-Skin,Vector3.down,out var hit,Lift+Reach+Skin*2,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))
            {
                var normal=SurfaceNormal(hit);float gap=hit.distance-Lift-Skin/Math.Max(NativeLocomotionRules.MinGroundY,normal.y);
                if(Walkable(normal)&&gap>=-.06f&&gap<=Reach)return new Ground{Found=true,Gap=gap,Normal=normal,Point=hit.point};
            }
            // A nearby riser can intercept the broad probe before the floor directly under the feet.
            if(physics.Raycast(position,Vector3.down,out hit,Half+Reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)&&Walkable(hit.normal))
            {
                float gap=hit.distance-Pole-Radius/hit.normal.y;
                if(gap>=-.06f&&gap<=Reach)return new Ground{Found=true,Gap=gap,Normal=hit.normal,Point=hit.point};
            }
            return default;
        }
        private static bool Cast(PhysicsScene physics,Vector3 from,Vector3 delta,out RaycastHit hit)
        {
            hit=default;float distance=delta.magnitude;if(distance<.00001f)return false;
            return physics.CapsuleCast(from+Vector3.up*Pole,from-Vector3.up*Pole,Radius-Skin,delta/distance,out hit,distance+Skin,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
        }
        private static bool Clear(PhysicsScene physics,Vector3 at)=>physics.OverlapCapsule(at+Vector3.up*Pole,at-Vector3.up*Pole,Radius-Skin,overlaps,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)==0;
        // A committed step never slides sideways and never crosses unsupported floor.
        public static float ForwardStepDistance(PhysicsScene physics,Vector3 from,Vector3 facing,float distance)
        {
            if(distance<=0)return 0;
            if(Cast(physics,from,facing*distance,out var wall))distance=Math.Max(0,wall.distance-Skin);
            float safe=0;int samples=Math.Max(1,(int)Math.Ceiling(distance/.025f));
            for(int i=1;i<=samples;i++)
            {
                float d=distance*i/samples;var at=from+facing*d;
                if(!physics.Raycast(at,Vector3.down,out var floor,Half+Step+.001f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)||!Walkable(floor.normal))break;
                if(from.y-Half-floor.point.y>Step+.001f)break;
                safe=d;
            }
            return safe;
        }
        public static bool StepUp(PhysicsScene physics,Vector3 from,Vector3 horizontal,out float rise,out Vector3 allowed)
        {
            rise=0;horizontal.y=0;allowed=horizontal;if(horizontal.sqrMagnitude<.000001f||!Cast(physics,from,horizontal,out var obstruction))return false;
            if(NativeLocomotionRules.RampContact(NativeArena.Vec(obstruction.normal),NativeArena.Vec(SurfaceNormal(obstruction))))return false;
            // A refused step must also constrain horizontal input. Otherwise the frictionless dynamic capsule
            // keeps pressing into the rounded riser and the solver can squeeze it under an impassable ceiling.
            // Leave moving props to the solver so pushing objects and debris impulses retain their behavior.
            if(obstruction.rigidbody==null||obstruction.rigidbody.isKinematic)
                allowed=NativeArena.Vec(NativeLocomotionRules.BlockedStep(NativeArena.Vec(horizontal),NativeArena.Vec(obstruction.normal),obstruction.distance));
            // Up, forward, down. Never teleport through a ceiling, wall, or unsupported gap.
            float lift=Step;if(Cast(physics,from,Vector3.up*lift,out var ceiling))lift=Math.Max(0,ceiling.distance-Skin);
            if(lift<Skin*2)return false;var raised=from+Vector3.up*lift;
            if(!Clear(physics,raised)||Cast(physics,raised,horizontal,out _))return false;
            var ahead=raised+horizontal;
            if(!physics.CapsuleCast(ahead+Vector3.up*Pole,ahead-Vector3.up*Pole,Radius-Skin,Vector3.down,out var floor,lift+Reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)||!Walkable(SurfaceNormal(floor)))return false;
            rise=lift-floor.distance+Skin;
            if(rise<=Skin||rise>Step+.001f||!Clear(physics,from+Vector3.up*rise)||!Clear(physics,from+horizontal+Vector3.up*rise)){rise=0;return false;}
            allowed=horizontal;return true;
        }
        public static Vector3 Slide(PhysicsScene physics,Vector3 from,Vector3 delta)
        {
            if(!Cast(physics,from,delta,out var hit))return from+delta;
            var travel=delta.normalized*Math.Max(0,hit.distance-Skin);from+=travel;
            var slide=Vector3.ProjectOnPlane(delta-travel,hit.normal);
            if(Cast(physics,from,slide,out var second))slide=slide.normalized*Math.Max(0,second.distance-Skin);
            return from+slide;
        }
        public static Vector3 Constrain(PhysicsScene physics,Vector3 from,Vector3 desired)
        {
            var delta=desired-from;var ground=Probe(physics,from);bool supported=ground.Found&&delta.y<=.00001f;
            if(supported)
            {
                if(StepUp(physics,from,delta,out float rise,out var allowed)){from.y+=rise;delta=allowed;}
                else delta=NativeArena.Vec(NativeLocomotionRules.AlongGround(NativeArena.Vec(allowed),NativeArena.Vec(ground.Normal)));
            }
            var result=Slide(physics,from,delta);
            if(supported){var landing=Probe(physics,result);if(landing.Found&&landing.Gap>Skin)result.y-=landing.Gap-Skin;}
            return result;
        }
    }
    public sealed partial class NativePilot
    {public NativeGroundMotor.Ground Ground;public bool GroundAttached;public Vector3 GroundPosition;}
    public sealed partial class NativeArena
    {
        private void ProbePilotGround(NativePilot p)
        {
            p.GroundPosition=p.Body.position;p.Ground=NativeGroundMotor.Probe(root.scene.GetPhysicsScene(),p.GroundPosition);
            p.Grounded=p.Ground.Found&&(p.GroundAttached||Vector3.Dot(p.Body.linearVelocity,p.Ground.Normal)<=1);
            if(p.Grounded){p.AirJumps=0;p.AirborneAt=-1;p.AirAttackUsed=false;}else if(p.AirborneAt<0)p.AirborneAt=Time;
        }
        private bool GroundMotion(NativePilot p,ref Vector3 velocity,bool jump,bool swimming,float impulseY)
        {
            if((p.Body.position-p.GroundPosition).sqrMagnitude>1e-8f)ProbePilotGround(p); // abilities may teleport after the tick's first probe
            bool attach=NativeLocomotionRules.Attach(p.Grounded,p.GroundAttached,Vector3.Dot(velocity,p.Ground.Normal),jump,swimming,Time<p.LaunchUntil,impulseY);
            p.GroundAttached=attach;if(!attach)return false;
            var physics=root.scene.GetPhysicsScene();var position=p.Body.position;var horizontal=new Vector3(velocity.x,0,velocity.z);
            if(NativeGroundMotor.StepUp(physics,position,horizontal/60,out float rise,out var allowed)){position.y+=rise;p.Body.position=position;p.Ground=NativeGroundMotor.Probe(physics,position);}
            else if(p.Ground.Gap>NativeLocomotionRules.Skin){position.y-=p.Ground.Gap-NativeLocomotionRules.Skin;p.Body.position=position;}
            horizontal=allowed*60;
            velocity=Vec(NativeLocomotionRules.AlongGround(Vec(horizontal),Vec(p.Ground.Found?p.Ground.Normal:Vector3.up)));
            return true;
        }
    }
}
