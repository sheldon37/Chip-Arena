Shader "ChipArena/SourceLit" {
 Properties { _SceneTint("Scene tint",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _BodyTint("Body tint",Color)=(1,1,1,1) _AccentTint("Accent tint",Color)=(1,1,1,1) _Ink("Siren ink",Float)=0 _Cull("Cull",Float)=2 }
 SubShader { Tags { "RenderType"="Opaque" } Cull [_Cull]
 CGPROGRAM
 #pragma surface surf Lambert fullforwardshadows addshadow vertex:vert
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;float _Ink;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;float3 worldPos;float3 viewDir;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o){float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);if(shell.a<.999)clip(shell.a-frac(sin(dot(floor(i.worldPos*120),float3(12.9898,78.233,37.719)))*43758.5453));float4 accent=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);float3 tint=i.emission.w>1.5?accent.rgb:i.emission.w>.5?shell.rgb:1;tint*=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb;float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=i.color.rgb*tint*tex.rgb;o.Emission=i.emission.rgb*tint;float rim=pow(1-saturate(abs(dot(normalize(i.viewDir),float3(0,0,1)))),3);float rise=sin(i.worldPos.y*23-_Time.y*6.5+sin(i.worldPos.x*13+_Time.y*1.7)*2.2+i.worldPos.z*9);o.Emission+=_Ink*float3(.62,.56,.82)*(rim*.3+smoothstep(.7,1,rise)*.025);o.Alpha=i.color.a*tex.a*shell.a;}
 ENDCG
 } Fallback "Diffuse"
}
