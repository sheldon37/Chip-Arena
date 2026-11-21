// Unlit vertex-colour voxels for the chip blasts (source chip-blasts.ts uses MeshBasicMaterial, toneMapped:false).
// Opaque pool: Blend One Zero, ZWrite On. Glow pool: Blend SrcAlpha One (additive), ZWrite Off, fog toward black.
Shader "ChipArena/NativeBlast"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
        _Additive("Additive fog",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        Blend [_Src] [_Dst]
        ZWrite [_ZWrite]
        ZTest LEqual
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Additive;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;UNITY_FOG_COORDS(0)};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;UNITY_TRANSFER_FOG(o,o.position);return o;}
            float4 frag(v2f i):SV_Target
            {
                float4 c=i.color;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,lerp(unity_FogColor,float4(0,0,0,0),_Additive));
                return c;
            }
            ENDCG
        }
    }
}
