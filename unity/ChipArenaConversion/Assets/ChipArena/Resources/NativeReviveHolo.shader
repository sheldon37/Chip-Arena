// The holographic revive's hardlight (Core/NativeReviveHolo.cs; Runtime/NativeReviveModels.cs): a body part's hologram, its
// wireframe (a line mesh), the floor rings and the column. Additive and unlit, never writing depth, both sides. Per instance
// (the source-model batches' arrays): _BodyTint rgb the linear colour, a the opacity; _SceneTint xyz the sweep's unit normal
// (world), w its offset (the band's centre is where dot(world, n) = w); _GlowTint x the band's half width (m; 0 for none;
// negative: no band, the whole piece fades by that half width from the plane instead: the column's soft ends), y the fill outside
// the band, z the rim (fresnel) share, w the scanlines per metre (world y, drifting up on _ReviveTime; 0 for none).
Shader "ChipArena/NativeReviveHolo" {
 SubShader { Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" } Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 float _ReviveTime;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Output {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
 Output vert(Input v){Output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
 fixed4 frag(Output i):SV_Target {
  UNITY_SETUP_INSTANCE_ID(i);
  float4 c=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 sweep=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint);float4 shape=UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint);
  float d=dot(i.world,sweep.xyz)-sweep.w;float band=0,envelope=1;
  if(shape.x>.0001)band=exp(-d*d/(shape.x*shape.x));
  else if(shape.x<-.0001)envelope=exp(-d*d/(shape.x*shape.x));
  float3 n=i.normal;float nn=dot(n,n);float rim=0;
  if(nn>1e-8){float3 view=normalize(_WorldSpaceCameraPos-i.world);rim=1-saturate(abs(dot(view,n*rsqrt(nn))));rim*=rim;}
  float lines=1;
  if(shape.w>0)lines=.55+.45*step(.5,frac(i.world.y*shape.w-_ReviveTime*2.3));
  float a=c.a*saturate(shape.y+band*1.6+rim*shape.z)*lines*envelope;
  float3 rgb=c.rgb*(1+band*1.4);
  #ifdef UNITY_COLORSPACE_GAMMA
  rgb=LinearToGammaSpace(rgb);
  #endif
  return fixed4(rgb,a);
 }
 ENDCG }
 } Fallback Off
}
