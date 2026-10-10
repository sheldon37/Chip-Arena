Shader "ChipArena/SourceTransparent" {
 Properties { _SceneTint("Scene tint",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _BodyTint("Body tint",Color)=(1,1,1,1) _AccentTint("Accent tint",Color)=(1,1,1,1) _GlowTint("Emission tint (linear)",Vector)=(1,1,1,1) _Ink("Siren ink",Float)=0 _Unlit("Unlit (flat colour, no lighting)",Float)=0 _Cull("Cull",Float)=0 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull [_Cull] ZWrite Off
 CGPROGRAM
 // _Unlit (per material): like three.js MeshBasicMaterial, the colour alone with no diffuse, ambient or light term.
 #pragma surface surf Lambert alpha:fade vertex:vert
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;float _Unlit;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o){float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 accent=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);float3 tint=i.emission.w>1.5?accent.rgb:i.emission.w>.5?shell.rgb:1;tint*=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb;float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=i.color.rgb*tint*tex.rgb;o.Emission=i.emission.rgb*tint*UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint).rgb;o.Alpha=i.color.a*tex.a*shell.a;if(_Unlit>.5){o.Emission=o.Albedo;o.Albedo=0;}}
 ENDCG
 } Fallback "Transparent/Diffuse"
}
