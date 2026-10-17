// C12 Demon Step afterimage (client/demon-afterimages.ts ghostMesh): a MeshBasicMaterial silhouette, flat colour, transparent,
// depthWrite false, no tone mapping, front faces, scene fog. Drawn through NativeSourceModels.Queue (instanced or one by
// one): _BodyTint.rgb is the linear colour and .a the opacity.
Shader "ChipArena/NativeDevGhost"
{
    Properties { _BodyTint("Colour (linear) and opacity",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;UNITY_FOG_COORDS(0) UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);float4 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 c=float4(tint.rgb,tint.a);
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
