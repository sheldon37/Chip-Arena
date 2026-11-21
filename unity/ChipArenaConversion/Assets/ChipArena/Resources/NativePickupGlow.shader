// PvE chip pickup glow sprites and light beams (client/pve/pickups.ts at 05950ea; Core/NativePickupFx.cs,
// Runtime/NativePickupGlowPresentation.cs). One dynamic world-space mesh holds every glow quad and beam cylinder:
//   COLOR      rgb: the tier colour as the screen shows it (sRGB 0..1; the source materials encode their colour), a: opacity
//   TEXCOORD0  xy: glow texture uv, z: 1 samples the glow texture (the sprite), 0 is flat (the beam)
//   TEXCOORD1  xy: a sprite corner's offset in metres along the camera's right and up (the beam: zero). Every glow vertex
//              sits at the sprite centre and the vertex stage spreads it along rows 0 and 1 of the view matrix, as three.js
//              billboards a Sprite in view space, so every camera that draws it (the match view, the killcam) faces it.
// SpriteMaterial and MeshBasicMaterial with AdditiveBlending, toneMapped false, depthWrite false; the beam is DoubleSide.
// Both take the scene fog in the source (mixing the encoded colour toward the fog colour). The source adds (SrcAlpha, One)
// in its sRGB framebuffer: the colour is premultiplied by the alpha in screen space and then linearised, so a Linear
// project adds exactly the source's light over a dark background.
Shader "ChipArena/NativePickupGlow"
{
    Properties
    {
        _MainTex("Glow texture",2D)="white"{}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            // The exact sRGB decode (UnityCG's GammaToLinearSpace polynomial darkens the faint glow tail threefold near 0).
            float3 ToLinear(float3 c){c=max(c,0.);return lerp(c/12.92,pow((c+.055)/1.055,2.4),step(.04045,c));}
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float2 corner:TEXCOORD1;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float3 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata v)
            {
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz+UNITY_MATRIX_V[0].xyz*v.corner.x+UNITY_MATRIX_V[1].xyz*v.corner.y;
                v2f o;o.position=mul(UNITY_MATRIX_VP,float4(world,1));o.color=v.color;o.uv=v.uv.xyz;UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 map=tex2D(_MainTex,i.uv.xy);float textured=step(.5,i.uv.z);
                float4 col=float4(i.color.rgb*lerp(float3(1,1,1),map.rgb,textured),saturate(i.color.a*lerp(1,map.a,textured)));
                #ifdef UNITY_COLORSPACE_GAMMA
                float3 fog=unity_FogColor.rgb;
                #else
                float3 fog=LinearToGammaSpace(unity_FogColor.rgb);
                #endif
                UNITY_APPLY_FOG_COLOR(i.fogCoord,col,float4(fog,1));
                float3 c=col.rgb*col.a;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=ToLinear(c);
                #endif
                return float4(c,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
