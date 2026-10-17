// Unlit normal-blend world meshes for the field telegraphs (source chaos-visuals.ts glow disc and dome, siren/fx.ts
// scorch decal and warning ring, ability-preview.ts motes; all MeshBasic/glow materials, not tone mapped). Colour is
// _Color times the vertex colour; _Radial > 0 multiplies alpha by (1 - r)^_Radial with r = |uv| (the source's radial
// decal texture). Blend SrcAlpha OneMinusSrcAlpha, ZWrite Off, ZTest LEqual. Queue, culling and depth offset per material.
Shader "ChipArena/NativeFieldGlow"
{
    Properties
    {
        _Color("Color",Color)=(1,1,1,1)
        _Radial("Radial falloff exponent (0 off)",Float)=0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
        _Offset("Depth offset",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull [_Cull]
        Offset [_Offset],[_Offset]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _Color;float _Radial;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.position);return o;}
            float4 frag(v2f i):SV_Target
            {
                float4 c=i.color;
                if(_Radial>0)c.a*=pow(saturate(1-length(i.uv)),_Radial);
                UNITY_APPLY_FOG(i.fogCoord,c);
                return c;
            }
            ENDCG
        }
    }
}
