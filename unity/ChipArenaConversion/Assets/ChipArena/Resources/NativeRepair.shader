Shader "ChipArena/NativeRepair" {
 SubShader { Tags { "Queue"="Transparent+4" "RenderType"="Transparent" } Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 float _RepairTime;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Output {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 Output vert(Input v){Output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(Output i):SV_Target {
  UNITY_SETUP_INSTANCE_ID(i);
  float4 c=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);
  // client/pve/downed.ts regenMaterial: world-space scanlines drift upward, additive and unlit.
  c.rgb*=.5+.8*step(.5,frac(i.world.y*24.-_RepairTime*1.7));
  #ifdef UNITY_COLORSPACE_GAMMA
  c.rgb=LinearToGammaSpace(c.rgb);
  #endif
  return c;
 }
 ENDCG }
 } Fallback Off
}
