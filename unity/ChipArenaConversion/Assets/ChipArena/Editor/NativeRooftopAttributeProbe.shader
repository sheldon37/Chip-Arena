// Editor-only probe: read back the production mesh's complete custom float4 attribute.
Shader "Hidden/ChipArena/RooftopAttributeProbe"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 attr:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float4 attr:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.attr=v.attr;return o;}
            float4 frag(v2f i):SV_Target{return i.attr;}
            ENDCG
        }
    }
    Fallback Off
}
