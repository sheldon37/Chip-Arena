Shader "ChipArena/NativeGlow"
{
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
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
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float3 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float3 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            float4 frag(v2f i):SV_Target
            {
                float r2=dot(i.uv.xy,i.uv.xy);
                // uv.z: 0 gaussian halo (projectile-glow.ts texture), 1 ribbon with a (1-|u|)^2 profile, 2 flat ribbon (projectile-trails.ts).
                float alpha=i.uv.z>1.5?1:i.uv.z>.5?pow(saturate(1-abs(i.uv.x)),2):exp(-r2*4.5)*saturate(1-r2);
                return float4(i.color.rgb,i.color.a*alpha);
            }
            ENDCG
        }
    }
}
