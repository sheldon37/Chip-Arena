Shader "ChipArena/NativeSmoke"
{
 Properties { _Color("Smoke",Color)=(.1,.12,.18,1) }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 struct Input { float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct Output { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
 float4 _Color;
 Output vert(Input v){UNITY_SETUP_INSTANCE_ID(v);Output o;float4 view=mul(UNITY_MATRIX_MV,float4(0,0,0,1));o.pos=mul(UNITY_MATRIX_P,view);float size=min(70,800/max(1,-view.z));o.pos.xy+=v.vertex.xy*size*2/_ScreenParams.xy*o.pos.w;o.uv=v.uv;return o;}
 fixed4 frag(Output i):SV_Target {float r=length(i.uv-.5)*2;return fixed4(_Color.rgb,pow(max(0,1-r),2)*.3);}
 ENDCG }
 }
}
