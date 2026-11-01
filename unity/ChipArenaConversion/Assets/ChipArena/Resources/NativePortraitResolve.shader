// Resolves a rendered portrait (multisampled, over a transparent clear, so its colour is premultiplied by coverage) into
// straight alpha, the form IMGUI's texture blend (SrcAlpha, OneMinusSrcAlpha) composites correctly over the card, as the
// source's premultiplied WebGL canvas does over its pilot card.
// An empty texel takes the coverage-weighted colour of the covered texels around it (alpha stays 0): the card samples the
// texture bilinearly, and a black empty texel next to the silhouette would darken its anti-aliased edge wherever the
// texture is not drawn 1:1 (card entrance scale, fractional offsets, the size cap).
// The anime style (_PortraitToon.x on: NativePortraits while it is on; Core NativeMenuStyle, docs/MAP-STYLE-ANIME.md section
// 6) adds, before the straight-alpha step: the outline the maps' pass draws, from the portrait's own coverage (a covered
// texel whose neighbour one or two line widths away is markedly less covered: the silhouette, on the pilot's and the
// podium's own side), its colour multiplied toward the maps' line colour; and a painted gradient under them, in soft
// bands from the podium's row up (the backdrop's tint over the cool shade, translucent, so the card's own backdrop and its
// colour still show through).
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
   // _PortraitToon: x on, y the line's width (texels), z the gradient's opacity at the podium, w the podium's row (uv.y).
   // _PortraitLine: rgb the line's tint (linear). _PortraitBack: rgb the backdrop's tint (linear), w the gradient's top row (uv.y).
   float4 _PortraitToon,_PortraitLine,_PortraitBack;
   // The gradient's cool shade (#2A3460, linear) and how much of the backdrop's tint it takes at its top and its foot.
   #define PORTRAIT_SHADE float3(0.023,0.034,0.117)
   #define PORTRAIT_TINT_LOW 0.12
   #define PORTRAIT_TINT_HIGH 0.3
   float PortraitBands(float x){float k=saturate(x)*3.0;float f=floor(k);return min(1.0,(f+smoothstep(0.35,0.65,k-f))/3.0);}
   float4 PortraitToon(float4 c,float2 uv)
   {
    float2 w=_MainTex_TexelSize.xy*_PortraitToon.y;
    float n1=min(min(tex2D(_MainTex,uv-float2(w.x,0.0)).a,tex2D(_MainTex,uv+float2(w.x,0.0)).a),min(tex2D(_MainTex,uv-float2(0.0,w.y)).a,tex2D(_MainTex,uv+float2(0.0,w.y)).a));
    float n2=min(min(tex2D(_MainTex,uv-float2(2.0*w.x,0.0)).a,tex2D(_MainTex,uv+float2(2.0*w.x,0.0)).a),min(tex2D(_MainTex,uv-float2(0.0,2.0*w.y)).a,tex2D(_MainTex,uv+float2(0.0,2.0*w.y)).a));
    float ink=max(smoothstep(0.2,0.45,c.a-n1),0.7*smoothstep(0.45,0.75,c.a-n2))*smoothstep(0.25,0.55,c.a);
    c.rgb*=lerp(float3(1.0,1.0,1.0),_PortraitLine.rgb,ink);
    float g=PortraitBands(1.0-(uv.y-_PortraitToon.w)/max(_PortraitBack.w-_PortraitToon.w,0.001))*_PortraitToon.z;
    float3 gc=lerp(PORTRAIT_SHADE,_PortraitBack.rgb,PORTRAIT_TINT_LOW+PORTRAIT_TINT_HIGH*g/max(_PortraitToon.z,0.001));
    return c+float4(gc*g,g)*(1.0-c.a);
   }
   float4 frag(v2f_img i):SV_Target
   {
    float4 c=tex2D(_MainTex,i.uv),around=0;
    if(_PortraitToon.x>0.5)c=PortraitToon(c,i.uv);
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
