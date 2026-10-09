// Resolves a rendered portrait (multisampled, over a transparent clear, so its colour is premultiplied by coverage) into
// straight alpha, the form IMGUI's texture blend (SrcAlpha, OneMinusSrcAlpha) composites correctly over the card, as the
// source's premultiplied WebGL canvas does over its pilot card.
Shader "Hidden/ChipArena/PortraitResolve" {
 Properties { _MainTex("Texture",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float4 frag(v2f_img i):SV_Target
   {
    float4 c=tex2D(_MainTex,i.uv);
    return c.a>.0001?float4(saturate(c.rgb/c.a),c.a):float4(0,0,0,0);
   }
   ENDCG
  }
 }
 Fallback Off
}
