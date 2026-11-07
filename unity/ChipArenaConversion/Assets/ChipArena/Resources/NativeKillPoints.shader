// C18 point swarms (ragdoll-vfx.ts: T.Points with PointsMaterial size .055 / .10, sizeAttenuation, vertexColors,
// AdditiveBlending, depthWrite false, toneMapped false). Each point is a camera-facing square quad: four vertices at the
// point's world position, expanded in clip space by TEXCOORD0 = corner * size / 2, scaled by (height / width, 1) so that the
// square is size * (height / 2) / depth pixels tall, exactly three.js's gl_PointSize = size * scale / -mvPosition.z with
// scale = drawing-buffer height / 2. Vertex colour rgb is the point colour, alpha the material opacity. Fog fades to black
// like the additive blast pool (NativeBlast.shader).
Shader "ChipArena/NativeKillPoints"
{
    SubShader
    {
        Tags { "Queue"="Transparent+3" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 corner:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;UNITY_FOG_COORDS(0)};
            v2f vert(appdata v)
            {
                v2f o;float4 clip=UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,clip);
                clip.xy+=v.corner*float2(_ScreenParams.y/_ScreenParams.x,1);
                o.position=clip;o.color=v.color;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 c=i.color;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));
                return c;
            }
            ENDCG
        }
    }
}
