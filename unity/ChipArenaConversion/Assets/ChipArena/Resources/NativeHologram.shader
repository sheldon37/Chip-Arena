Shader "ChipArena/NativeHologram" {
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct Output {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 Output vert(Input v){Output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(Output i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);float alpha=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint).w;float stripe=.7+.3*step(.14,frac(i.world.y*.8));return fixed4(.32,.8,1,alpha*stripe);}
 ENDCG }
 } Fallback Off
}
