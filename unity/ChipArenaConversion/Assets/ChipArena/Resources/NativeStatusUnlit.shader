Shader "ChipArena/NativeStatusUnlit"
{
    // Unlit, fogged flat colour: the source's MeshBasicMaterial status parts (status-effects.ts) and team bands
    // (team-identity.ts). Blend, depth write and culling are per material (translucent parts never write depth).
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _SrcBlend ("Source blend", Float) = 1
        _DstBlend ("Destination blend", Float) = 0
        _ZWrite ("Depth write", Float) = 1
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 position:SV_POSITION; UNITY_FOG_COORDS(0) };
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); o.position=UnityObjectToClipPos(v.vertex); UNITY_TRANSFER_FOG(o,o.position); return o; }
            fixed4 frag(v2f i):SV_Target { fixed4 color=_Color; UNITY_APPLY_FOG(i.fogCoord,color); return color; }
            ENDCG
        }
    }
    Fallback Off
}
