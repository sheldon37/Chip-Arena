// Death presentation primitives (Runtime/NativeDeathFxDraw.cs): unlit vertex colour (alpha = opacity), no fog, no tone
// mapping, no depth write, both faces, blend and depth test per material.
//  - death-announcements.ts: one-pixel lines (MeshTopology.Lines), the rocket sphere, the nameplate glyphs (uv.z = 1 samples
//    the font atlas alpha) and the point-sprite stars: four vertices at the point, expanded in clip space by TEXCOORD1 =
//    corner * size / 2 scaled by (height / width, 1), i.e. size * (height / 2) / depth pixels, three.js's gl_PointSize.
//  - vent-fall.ts SPLAT!: the same glyphs with the depth test off.
//  - _Scan / _HoloTime: optional world-space scanlines drifting up (rgb *= .5 + .8 * step(.5, fract(y * 24 - t * 1.7))),
//    off on every current material.
Shader "ChipArena/NativeDeathFx"
{
    Properties
    {
        _MainTex("Glyph atlas",2D)="white"{}
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Source blend",Float)=5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Destination blend",Float)=10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test",Float)=4
        _Scan("Scanlines",Float)=0
        _HoloTime("Scanline time",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_Src] [_Dst]
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Scan,_HoloTime;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float2 corner:TEXCOORD1;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float3 uv:TEXCOORD0;float worldY:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;float4 clip=UnityObjectToClipPos(v.vertex);
                clip.xy+=v.corner*float2(_ScreenParams.y/_ScreenParams.x,1);
                o.position=clip;o.color=v.color;o.uv=v.uv.xyz;o.worldY=mul(unity_ObjectToWorld,v.vertex).y;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 c=i.color;
                float glyph=tex2D(_MainTex,i.uv.xy).a;
                c.a*=lerp(1,glyph,saturate(i.uv.z));
                c.rgb*=lerp(1,.5+.8*step(.5,frac(i.worldY*24-_HoloTime*1.7)),saturate(_Scan));
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
