// One separable Gaussian pass for the kit's shared backdrop blur (NativeUiBlur.cs, CSS backdrop-filter: blur): 13 taps
// along _NativeUiBlur.xy (a unit axis, in texels of the source) with standard deviation _NativeUiBlur.z texels. The
// result is opaque: the camera target's alpha is not coverage, and the kit draws the copy with the panel's opacity.
Shader "Hidden/ChipArena/NativeUiBlur" {
 Properties { _MainTex("Texture",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize;float4 _NativeUiBlur;
   float4 frag(v2f_img i):SV_Target
   {
    float2 axis=_NativeUiBlur.xy*_MainTex_TexelSize.xy;float sigma=max(_NativeUiBlur.z,.5),k=-.5/(sigma*sigma);
    float3 sum=0;float total=0;
    [unroll] for(int n=-6;n<=6;n++){float w=exp(n*n*k);sum+=tex2D(_MainTex,i.uv+axis*n).rgb*w;total+=w;}
    return float4(sum/total,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
