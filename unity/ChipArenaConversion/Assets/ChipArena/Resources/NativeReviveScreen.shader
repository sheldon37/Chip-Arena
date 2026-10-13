// The holographic revive's first-person vignette (Core/NativeReviveHolo.Vignette; Runtime/NativeReviveHoloPresentation.cs
// ReviveView): a quad just past the near plane filling the view, additive and drawn over everything in that camera: cyan
// scanlines (screen rows) and a soft glow at the edges, one faint sweep down the view, the centre clear. _Strength 0..1.
Shader "ChipArena/NativeReviveScreen" {
 Properties { _Strength("Strength",Float)=0 _ReviveTime("Clock",Float)=0 }
 SubShader { Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" } Blend SrcAlpha One ZWrite Off ZTest Always Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Strength,_ReviveTime;
 struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
 Output vert(Input v){Output o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.screen=ComputeScreenPos(o.pos);return o;}
 fixed4 frag(Output i):SV_Target {
  float2 q=i.uv*2-1;float r=length(q*float2(1,.8));
  float edge=smoothstep(.55,1.15,r);
  float row=i.screen.y/max(i.screen.w,1e-5)*_ScreenParams.y;
  float lines=.45+.55*step(.5,frac(row*.25+_ReviveTime*6));
  float s=(i.uv.y-(1-frac(_ReviveTime*.9)))*9;float sweep=exp(-s*s);
  float a=_Strength*saturate(edge*.55*lines+sweep*.12*(.3+edge));
  float3 rgb=float3(.1144,.807,1);
  #ifdef UNITY_COLORSPACE_GAMMA
  rgb=LinearToGammaSpace(rgb);
  #endif
  return fixed4(rgb,a);
 }
 ENDCG }
 } Fallback Off
}
