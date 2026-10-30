// Effects overhaul, weapons milestone (owner 2026-10-11): the roll afterimages (Trails FX snapshot approach, rebuilt). A
// snapshot of a rolling pilot's posed voxel parts drawn as an additive fresnel ghost: a faint body and a bright rim in the
// pilot's accent colour, fading with the copy's age. Drawn through NativeSourceModels.Queue (instanced, or one by one):
// _BodyTint.rgb is the linear colour (HDR) and .a the copy's opacity. Additive, no depth write, depth tested against the
// opaque scene, front faces, scene fog toward black; Transparent+1 so it lands after the arena's alpha-blended parts.
// _EchoOffset: a depth offset toward the eye for the visual body's echoes (a baked snapshot over the GPU-skinned body would
// z-fight while they overlap); 0 for the voxel ghosts.
// The visual body's per-vertex cut (NativeVisualBodyFx.cginc CutKeep: the death broadcast's bust, a split corpse's halves
// under the soul look); unset on everything else, which it leaves whole.
Shader "ChipArena/NativeWeaponGhost"
{
    Properties { _BodyTint("Colour (linear) and opacity",Color)=(1,1,1,1) _EchoOffset("Depth offset",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Offset [_EchoOffset],[_EchoOffset]
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.5
            #define VISUAL_BODY_CUT
            #include "UnityCG.cginc"
            #include "NativeVisualBodyFx.cginc"
            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
            UNITY_INSTANCING_BUFFER_END(Props)
            // ---- GHOST SHARED BEGIN (HLSL and GLSL: float literals only) ----
            // facing: |N.V|. A faint body and a strong rim (Trails FX's outline rim, pow(1 - |N.V|, 2.4)).
            float GhostFresnel(float facing)
            {
                float rim = pow(saturate(1.0 - facing), 2.4);
                return 0.06 + 1.6 * rim;
            }
            // ---- GHOST SHARED END ----
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;uint vid:SV_VertexID;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;UNITY_FOG_COORDS(2) float cut:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;o.pos=UnityObjectToClipPos(v.vertex);
                o.normal=UnityObjectToWorldNormal(v.normal);o.view=_WorldSpaceCameraPos-world;o.cut=CutKeep(v.vid);
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                clip(i.cut);UNITY_SETUP_INSTANCE_ID(i);float4 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);
                float facing=abs(dot(normalize(i.normal),normalize(i.view)));
                float4 c=float4(tint.rgb*(GhostFresnel(facing)*tint.a),0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));
                #ifdef UNITY_COLORSPACE_GAMMA
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
