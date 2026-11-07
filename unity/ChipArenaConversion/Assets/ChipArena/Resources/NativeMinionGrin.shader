// The Siren possession grin (client/siren/minions/hints.ts glyph() at 05950ea; Core/NativeMinionHint.cs Glyph bakes its canvas,
// Runtime/NativeMinionFxPresentation.cs draws it): one camera-facing quad of the baked image at _Opacity, so the grin fades as
// one image, as the source's SpriteMaterial fades its canvas texture (the layers beneath the lips and teeth never show through).
//   _MainTex  rgb: the colour premultiplied in linear light, sRGB-encoded (an sRGB texture: the sampler decodes and filters it
//             premultiplied); a: alpha. Gamma projects decode it here.
// SpriteMaterial: normal blending, depthWrite false, toneMapped false, the scene fog (the colour mixed toward the fog colour,
// alpha kept). Output premultiplied (Blend One OneMinusSrcAlpha), as the Siren effect layer's.
Shader "ChipArena/NativeMinionGrin"
{
    Properties
    {
        _MainTex("Grin",2D)="black"{}
        _Opacity("Opacity",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Opacity;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.position);return o;}
            float4 frag(v2f i):SV_Target
            {
                float4 t=tex2D(_MainTex,i.uv);float k=saturate(_Opacity);
                #ifdef UNITY_COLORSPACE_GAMMA
                float3 p=GammaToLinearSpace(t.rgb),fog=GammaToLinearSpace(unity_FogColor.rgb);
                #else
                float3 p=t.rgb,fog=unity_FogColor.rgb;
                #endif
                // The straight colour p / a toward the fog, premultiplied again: p * f + fog * a * (1 - f).
                float4 c=float4(p,1);UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(fog*t.a,1));
                #ifdef UNITY_COLORSPACE_GAMMA
                c.rgb=LinearToGammaSpace(max(c.rgb/max(t.a,1e-4),0))*t.a;
                #endif
                return float4(c.rgb*k,t.a*k);
            }
            ENDCG
        }
    }
    Fallback Off
}
