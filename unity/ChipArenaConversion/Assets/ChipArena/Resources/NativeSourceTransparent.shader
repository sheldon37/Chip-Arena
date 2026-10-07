Shader "ChipArena/SourceTransparent" {
 Properties { _MainTex("Texture",2D)="white"{} _BodyTint("Body tint",Color)=(1,1,1,1) _AccentTint("Accent tint",Color)=(1,1,1,1) _Ink("Siren ink",Float)=0 _Cull("Cull",Float)=0 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull [_Cull] ZWrite Off
 CGPROGRAM
 #pragma surface surf Lambert alpha:fade vertex:vert
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o){float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=i.color.rgb*tex.rgb;o.Emission=i.emission.rgb;o.Alpha=i.color.a*tex.a*shell.a;}
 ENDCG
 } Fallback "Transparent/Diffuse"
}
