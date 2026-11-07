Shader "ChipArena/NativeBeacon"
{
    // client/pve/beacons.ts revive beacon: additive, double-sided, no depth write, not tone-mapped. Vertex colour carries
    // the beacon colour and opacity; uv.z = 1 draws flat (the floor ring), 0 samples the motes map scrolled by _Scroll.
    Properties
    {
        _MainTex ("Motes", 2D) = "white" {}
        _Scroll ("Scroll", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Scroll;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float3 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float3 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            float4 frag(v2f i):SV_Target
            {
                float4 motes=tex2D(_MainTex,float2(i.uv.x,i.uv.y+_Scroll));
                float4 t=i.uv.z>.5?float4(1,1,1,1):motes;
                return float4(i.color.rgb*t.rgb,i.color.a*t.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
