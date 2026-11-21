// A directed entrance's see-through pilot (Core/NativeEntranceDirector: Twin Phantom's afterimages, the Grid Rez
// silhouette, the Holo Projection hologram). One layer: a depth-only pass first, then the colour pass on the nearest
// surface only, so the voxel rig's overlapping boxes do not stack into a solid glow as a plain alpha blend does.
// _BodyTint.rgb is the linear colour and .a the opacity; a fresnel rim brightens the silhouette; _AccentTint.x adds
// hologram scanlines (world height) scrolling with _AccentTint.y (the entrance clock). Drawn through NativeSourceModels.Queue
// (instanced or one by one), unlit, no shadows, scene fog.
Shader "ChipArena/NativeEntranceGhost"
{
    Properties { _BodyTint("Colour (linear) and opacity",Color)=(1,1,1,1) _AccentTint("Scanlines, clock",Vector)=(0,0,0,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;};
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);o.pos=UnityObjectToClipPos(v.vertex);return o;}
            float4 frag(v2f i):SV_Target{return 0;}
            ENDCG
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
            UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);float4 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 scan=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);
                float3 view=normalize(_WorldSpaceCameraPos-i.world);float rim=1.0-saturate(abs(dot(normalize(i.normal),view)));rim=rim*rim;
                float lines=0.5+0.5*sin(i.world.y*120.0-scan.y*9.0);lines=lerp(1.0,0.55+0.75*lines*lines,saturate(scan.x));
                float4 c=float4(tint.rgb*(0.75+1.6*rim)*lines,saturate(tint.a*(0.65+0.9*rim)*lines));
                #ifdef UNITY_COLORSPACE_GAMMA
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
