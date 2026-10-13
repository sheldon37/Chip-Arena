namespace ChipArena.UnityConversion
{
    public enum NativeJumpKind { None, Ground, Air, Swim }
    /// <summary>Shared authority, lab and guest jump commitment. Jump is an input edge, never held input.</summary>
    public static class NativeJumpSimulation
    {
        public const float AirMultiplier=.9f;
        public static bool CanAirJump(string spec)=>spec=="daggers"||spec=="bow"||spec=="cyberlord";
        public static NativeJumpKind Try(string spec,bool pressed,bool grounded,bool swimming,bool locked,
            float verticalSpeed,float jumpSpeed,ref int airJumps,out float launch)
        {
            launch=verticalSpeed;if(grounded)airJumps=0;
            if(!pressed||locked)return NativeJumpKind.None;
            if(swimming)
            {
                if(!grounded&&verticalSpeed>=2)return NativeJumpKind.None;
                launch=jumpSpeed*(10f/Rules.JumpSpeed);return NativeJumpKind.Swim;
            }
            if(grounded){launch=jumpSpeed;return NativeJumpKind.Ground;}
            if(!CanAirJump(spec)||airJumps!=0)return NativeJumpKind.None;
            airJumps=1;launch=jumpSpeed*AirMultiplier;return NativeJumpKind.Air;
        }
    }
}
