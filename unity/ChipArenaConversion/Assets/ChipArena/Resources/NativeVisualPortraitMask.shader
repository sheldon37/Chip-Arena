Shader "Hidden/ChipArena/VisualPortraitMask" {
 Properties { _Texture2D("Cut-out (a)",2D)="white"{} _Cutoff("Cut-out",Range(0,1))=.5 _Cull("Cull",Float)=2 }
 // The menu portraits on the model (Runtime/NativeVisualPortraitPresentation.cs): the body's lit passes keep the target's
 // alpha, so after the colour render this replacement (RenderWithShader by RenderType, the colour render's depth kept) writes
 // the coverage alone: 1 wherever a surface drew, with the visual body's cut-out (hair tips, lashes), so the portrait's
 // straight-alpha resolve keeps the body over the card's backdrop.
 SubShader { Tags { "RenderType"="TransparentCutout" }
  Pass {
   ZWrite Off ZTest LEqual ColorMask A Cull [_Cull]
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _Texture2D;float4 _Texture2D_ST;float _Cutoff;
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord.xy,_Texture2D);return o;}
   fixed4 frag(v2f i):SV_Target{clip(tex2D(_Texture2D,i.uv).a-_Cutoff);return fixed4(0,0,0,1);}
   ENDCG
  }
 }
 SubShader { Tags { "RenderType"="Opaque" }
  Pass {
   ZWrite Off ZTest LEqual ColorMask A Cull [_Cull]
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 vert(float4 v:POSITION):SV_POSITION{return UnityObjectToClipPos(v);}
   fixed4 frag():SV_Target{return fixed4(0,0,0,1);}
   ENDCG
  }
 }
 Fallback Off
}
