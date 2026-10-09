// The menu portrait material (client/menu-portraits.ts at 05950ea): three's Lambert under the portrait's own lights (a
// hemisphere light and two directional lights, physically based intensities, diffuse / PI), then ACES filmic tone mapping
// at exposure 1.1 and the output colour space, written over a transparent clear. It reads no Unity light, ambient, fog or
// shadow, so nothing in the arena lights a portrait and the portrait lights nothing else. Source-model vertices carry
// their tag (emission.w: 1 body, 2 accent) and emission like ChipArena/SourceLit; unlit MeshBasic parts (emission equal
// to the colour) show their colour. _Mode 1 is MeshBasic (tone mapped), 2 MeshBasic without tone mapping (the halo).
// _UseAlpha: 0 opaque (alpha 1), 1 the part's opacity, 2 additive glow (premultiplied, see frag).
Shader "Hidden/ChipArena/Portrait" {
 Properties {
  _MainTex("Texture",2D)="white"{} _Color("Colour (linear)",Vector)=(1,1,1,1)
  _BodyTint("Body tint",Vector)=(1,1,1,1) _AccentTint("Accent tint",Vector)=(1,1,1,1) _SceneTint("Part tint",Vector)=(1,1,1,1) _GlowTint("Emission tint",Vector)=(1,1,1,1)
  _Ink("Siren ink",Float)=0 _Mode("Mode",Float)=0 _UseAlpha("Write alpha",Float)=0 _DoubleSide("Double sided",Float)=0
  _HemiSky("Hemisphere sky",Vector)=(0,0,0,0) _HemiGround("Hemisphere ground",Vector)=(0,0,0,0) _KeyDir("Key direction",Vector)=(0,1,0,0) _KeyColor("Key",Vector)=(0,0,0,0) _RimDir("Rim direction",Vector)=(0,1,0,0) _RimColor("Rim",Vector)=(0,0,0,0) _Exposure("Exposure",Float)=1.1
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2 [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src",Float)=1 [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst",Float)=0
  [Enum(UnityEngine.Rendering.BlendMode)] _SrcAlpha("Src alpha",Float)=1 [Enum(UnityEngine.Rendering.BlendMode)] _DstAlpha("Dst alpha",Float)=0 _ZWrite("ZWrite",Float)=1
 }
 SubShader {
  Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
  Pass {
   Cull [_Cull] ZWrite [_ZWrite] ZTest LEqual Blend [_SrcBlend] [_DstBlend], [_SrcAlpha] [_DstAlpha]
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_ST;
   float4 _Color,_BodyTint,_AccentTint,_SceneTint,_GlowTint,_HemiSky,_HemiGround,_KeyDir,_KeyColor,_RimDir,_RimColor;
   float _Ink,_Mode,_UseAlpha,_DoubleSide,_Exposure;
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;float2 uv:TEXCOORD0;float4 emission:TEXCOORD1;};
   struct v2f {float4 pos:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 emission:TEXCOORD1;float3 normal:TEXCOORD2;float3 world:TEXCOORD3;};
   v2f vert(appdata v)
   {
    v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.emission=v.emission;
    o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;
   }
   // three r180 ACESFilmicToneMapping (exposure / 0.6, the RRT/ODT fit, clamp).
   float3 RRTAndODTFit(float3 v){float3 a=v*(v+0.0245786)-0.000090537;float3 b=v*(0.983729*v+0.4329510)+0.238081;return a/b;}
   float3 Aces(float3 c)
   {
    c*=_Exposure/0.6;
    c=float3(dot(c,float3(0.59719,0.35458,0.04823)),dot(c,float3(0.07600,0.90834,0.01566)),dot(c,float3(0.02840,0.13383,0.83777)));
    c=RRTAndODTFit(c);
    c=float3(dot(c,float3(1.60475,-0.53108,-0.07367)),dot(c,float3(-0.10208,1.10813,-0.00605)),dot(c,float3(-0.00327,-0.07276,1.07602)));
    return saturate(c);
   }
   float4 frag(v2f i):SV_Target
   {
    float4 tex=tex2D(_MainTex,i.uv);
    #ifdef UNITY_COLORSPACE_GAMMA
    tex.rgb=GammaToLinearSpace(tex.rgb);
    #endif
    float tag=i.emission.w;float3 tint=(tag>1.5?_AccentTint.rgb:tag>.5?_BodyTint.rgb:float3(1,1,1))*_SceneTint.rgb;
    float3 albedo=i.color.rgb*tint*tex.rgb,emission=i.emission.rgb*tint*_GlowTint.rgb;
    // An unlit source part (exported MeshBasic: emission equal to its colour) shows its colour; never on a part whose
    // emission is switched off (the portrait mannequin), which would otherwise turn black.
    bool basic=_Mode>.5||(dot(_GlowTint.rgb,float3(1,1,1))>0&&dot(i.emission.rgb,float3(1,1,1))>0&&all(abs(i.emission.rgb-i.color.rgb)<.002));
    float3 color;
    if(basic)color=_Mode>.5?albedo:emission;
    else
    {
     // Toward the camera: the view matrix's third row (orthographic: one direction for the whole portrait). Double-sided
     // parts light their back faces with the flipped normal, as three does.
     float3 n=normalize(i.normal),toCamera=normalize(UNITY_MATRIX_V[2].xyz);if(_DoubleSide>.5&&dot(n,toCamera)<0)n=-n;
     float3 irradiance=lerp(_HemiGround.rgb,_HemiSky.rgb,.5*n.y+.5)+saturate(dot(n,_KeyDir.xyz))*_KeyColor.rgb+saturate(dot(n,_RimDir.xyz))*_RimColor.rgb;
     color=albedo*irradiance*UNITY_INV_PI+emission;
     if(_Ink>.5)
     {
      float rim=pow(1-saturate(abs(dot(toCamera,float3(0,0,1)))),3);float rise=sin(i.world.y*23-_Time.y*6.5+sin(i.world.x*13+_Time.y*1.7)*2.2+i.world.z*9);
      color+=float3(.62,.56,.82)*(rim*.3+smoothstep(.7,1,rise)*.025);
     }
    }
    if(_Mode<1.5)color=Aces(color);else color=saturate(color);
    #ifdef UNITY_COLORSPACE_GAMMA
    color=LinearToGammaSpace(color);
    #endif
    float alpha=_UseAlpha>.5?i.color.a*tex.a*_BodyTint.a:1;
    // _UseAlpha 2, additive glow (blend One One): colour premultiplied by opacity, coverage its brightest channel.
    if(_UseAlpha>1.5)return float4(color*alpha,max(color.r,max(color.g,color.b))*alpha);
    return float4(color,alpha);
   }
   ENDCG
  }
 }
 Fallback Off
}
