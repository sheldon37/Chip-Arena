Shader "ChipArena/NativeLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emission ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        // The environment's mark for the anime style's outline pass (Resources/NativeToonEdge.shader tests bit 16).
        Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
        CGPROGRAM
        // ArenaToon (NativeToonSurface.cginc): Unity's Lambert exactly while the anime map style is off.
        #pragma surface surf ArenaToon fullforwardshadows addshadow
        #pragma target 3.5
        #pragma multi_compile_instancing
        // The guns' muzzle lights (NativeMuzzleLight.cginc) on the old maps' blocks, the trims and the gore: emission, unwrapped.
        #include "NativeMuzzleLight.cginc"
        #include "NativeToonSurface.cginc"
        struct Input { float3 worldPos; float3 worldNormal; };
        fixed4 _Color;
        fixed4 _Emission;
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Emission = _Emission.rgb + o.Albedo * ToonPoints(MuzzleLights(IN.worldPos, normalize(IN.worldNormal), 0.0));
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
