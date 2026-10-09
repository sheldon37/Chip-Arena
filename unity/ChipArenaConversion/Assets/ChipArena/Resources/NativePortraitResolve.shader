// Resolves a rendered portrait (multisampled, over a transparent clear, so its colour is premultiplied by coverage) into
// straight alpha, the form IMGUI's texture blend (SrcAlpha, OneMinusSrcAlpha) composites correctly over the card, as the
// source's premultiplied WebGL canvas does over its pilot card.
// An empty texel takes the coverage-weighted colour of the covered texels around it (alpha stays 0): the card samples the
// texture bilinearly, and a black empty texel next to the silhouette would darken its anti-aliased edge wherever the
// texture is not drawn 1:1 (card entrance scale, fractional offsets, the size cap).
Shader "Hidden/ChipArena/PortraitResolve" {
 Properties { _MainTex("Texture",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize;
   float4 frag(v2f_img i):SV_Target
   {
    float4 c=tex2D(_MainTex,i.uv),around=0;
    [unroll] for(int y=-1;y<=1;y++)
     [unroll] for(int x=-1;x<=1;x++)
      around+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy);
    if(c.a>.0001)return float4(saturate(c.rgb/c.a),c.a);
    return around.a>.0001?float4(saturate(around.rgb/around.a),0):float4(0,0,0,0);
   }
   ENDCG
  }
 }
 Fallback Off
}
