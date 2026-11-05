using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private PoolroomsLayout arenaPoolrooms;
        public PoolroomsLayout Poolrooms=>MapId==NativePoolroomsGenerator.ArenaId?(arenaPoolrooms??(arenaPoolrooms=NativePoolroomsGenerator.Build(NativePoolroomsGenerator.ArenaSeed,false).Layout)):MapId==NativePoolroomsGenerator.InstanceMap?GeneratedRun?.Poolrooms?.Layout:null;
        public float PoolMove(Vector3 centre,float halfHeight=.85f)=>NativePoolroomsWater.Speed(NativePoolroomsWater.Depth(Poolrooms,Vec(centre-Vector3.up*halfHeight),BossTide));
        private float PoolStep=>Poolrooms==null?NativeLocomotionRules.StepHeight:.42f;
    }
}
