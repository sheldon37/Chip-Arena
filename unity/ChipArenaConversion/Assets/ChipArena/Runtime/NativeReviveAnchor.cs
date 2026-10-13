using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly Collider[] reviveObstacles=new Collider[8];
        // The authority's rendered corpse selects the location; guests never move gameplay bodies.
        // The existing dead pilot position replicates it to interaction prompts, beacon and eventual respawn.
        public bool SetReviveAnchor(NativePilot p,Vector3 corpseCenter)
        {
            if(Replica||Pve==null||p.Hp>0||p.Death==null||!Pve.Rows.ContainsKey(p.Seat.id)||InShaft(p))return false;
            if(float.IsNaN(corpseCenter.x)||float.IsNaN(corpseCenter.y)||float.IsNaN(corpseCenter.z)||float.IsInfinity(corpseCenter.sqrMagnitude))return false;
            var physics=root.scene.GetPhysicsScene();
            if(!physics.Raycast(corpseCenter+Vector3.up*.5f,Vector3.down,out var floor,6,WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<NativeLocomotionRules.MinGroundY)return false;
            var at=floor.point+Vector3.up*(NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Radius+NativeLocomotionRules.Radius/floor.normal.y+.025f);
            if((ClampMap(at)-at).sqrMagnitude>.001f)return false;
            float pole=NativeLocomotionRules.HalfHeight-NativeLocomotionRules.Radius;
            if(physics.OverlapCapsule(at+Vector3.up*pole,at-Vector3.up*pole,NativeLocomotionRules.Radius-NativeLocomotionRules.Skin,reviveObstacles,WorldMask,QueryTriggerInteraction.Ignore)>0)return false;
            p.Body.position=at;return true;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private NativeArena anchorArena;private float anchorAt;
        private void SyncReviveAnchors(NativeArena arena)
        {
            if(arena.Replica||arena.Pve==null)return;
            if(ReferenceEquals(anchorArena,arena)&&arena.Time>=anchorAt&&arena.Time<anchorAt+.1f)return;
            anchorArena=arena;anchorAt=arena.Time;
            foreach(var p in arena.Pilots)
                if(p.Hp<=0&&p.Death!=null&&corpses.TryGetValue(p.Seat.id,out var body)&&body.Death.seed==p.Death.seed)
                    arena.SetReviveAnchor(p,body.Center);
        }
    }
}
