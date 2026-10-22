Shader "ChipArena/VisualBodyToon" {
 Properties {
  _Texture2D("Colour weights (rgb), cut-out (a)",2D)="white"{}
  _UseCustomTexture("Nine colours from the weights (0: the texture as drawn)",Float)=1
  _Color_1("Layer 1 colour 1",Color)=(1,1,1,1) _Color_2("Layer 1 colour 2",Color)=(1,1,1,1) _Color_3("Layer 1 colour 3 (accent)",Color)=(1,1,1,1)
  _Color_4("Layer 2 colour 1",Color)=(1,1,1,1) _Color_5("Layer 2 colour 2",Color)=(1,1,1,1) _Color_6("Layer 2 colour 3 (accent)",Color)=(1,1,1,1)
  _Color_7("Layer 3 colour 1",Color)=(1,1,1,1) _Color_8("Layer 3 colour 2",Color)=(1,1,1,1) _Color_9("Layer 3 colour 3 (accent)",Color)=(1,1,1,1)
  _Cutoff("Cut-out",Range(0,1))=.5
  _UseOutline("Outline",Float)=1 _OutlineColor("Outline colour",Color)=(0,0,0,1) _OutlineDistance("Outline width (mm, times the vertex alpha)",Float)=2
  _Cull("Cull",Float)=2
 }
 // The visual body's toon (docs/BOZO-INTEGRATION.md step 4; Runtime/NativeVisualBodyPresentation.cs): a modular character's
 // inputs on the arena's own lighting, so a skinned pilot takes the light the voxel pilots take (ChipArena/SourceLit):
 //  - colour: a part's texture weights three colours in its rgb and the vertex colour's rgb picks among three such layers
 //    (nine colours: the outfit's palette; the third of each layer is the accent the pilot's colour fills), alpha cuts out
 //    lashes and hair tips; with _UseCustomTexture 0 the texture is drawn as it is;
 //  - light: the anime map style's model (NativeToonSurface.cginc ArenaToon: Lambert while the style is off) for the key and
 //    every pixel light, Ward Zero's actor lights while _ArenaActorLit is on (the practical slots and the ceiling field) and
 //    the guns' muzzle lights on every map, both wrapped and stepped as SourceLit adds them;
 //  - the outline: an inverted hull pushed out along the normal by _OutlineDistance mm times the vertex alpha (where the
 //    alpha is under .5 there is none), in the fog;
 //  - the anime style's outline mark: every pass writes 0 to stencil bit 16, as characters do, so the map's lines
 //    (NativeToonEdge) never cross a pilot.
 SubShader { Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" } Cull [_Cull]
 Stencil { Ref 0 WriteMask 16 Comp Always Pass Replace }
 CGPROGRAM
 #pragma surface surf ArenaToon fullforwardshadows addshadow alphatest:_Cutoff
 #pragma target 3.5
 sampler2D _Texture2D;float _UseCustomTexture;
 float4 _Color_1,_Color_2,_Color_3,_Color_4,_Color_5,_Color_6,_Color_7,_Color_8,_Color_9;
 struct Input {float2 uv_Texture2D;float4 color:COLOR;float3 worldPos;float3 worldNormal;};
 float _ArenaActorLit;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];
 #include "NativeWardLight.cginc"
 #include "NativeMuzzleLight.cginc"
 #include "NativeToonSurface.cginc"
 // Nine colours: each layer is its three colours weighted by the texture's rgb; layer 1 by the vertex red, blended to layer 2
 // by its green and to layer 3 by its blue.
 float3 VisualColours(float3 w,float3 layer)
 {
  float3 one=_Color_1.rgb*w.x+_Color_2.rgb*w.y+_Color_3.rgb*w.z,two=_Color_4.rgb*w.x+_Color_5.rgb*w.y+_Color_6.rgb*w.z,three=_Color_7.rgb*w.x+_Color_8.rgb*w.y+_Color_9.rgb*w.z;
  return lerp(lerp(one*layer.x,two,layer.y),three,layer.z);
 }
 void surf(Input i,inout SurfaceOutput o)
 {
  float4 map=tex2D(_Texture2D,i.uv_Texture2D);
  o.Albedo=_UseCustomTexture>.5?VisualColours(map.rgb,i.color.rgb):map.rgb;o.Alpha=map.a;
  float3 n=normalize(i.worldNormal);
  if(_ArenaActorLit>.5)o.Emission+=o.Albedo*ToonPoints(ActorLights(i.worldPos,n));
  o.Emission+=o.Albedo*ToonPoints(MuzzleLights(i.worldPos,n,.35));
 }
 ENDCG
 Pass {
  Name "OUTLINE" Cull Front ZWrite On
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma multi_compile_fog
  #include "UnityCG.cginc"
  sampler2D _Texture2D;float4 _Texture2D_ST;float _UseOutline,_OutlineDistance,_Cutoff;float4 _OutlineColor;
  struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float keep:TEXCOORD1;UNITY_FOG_COORDS(2)};
  v2f vert(appdata_full v)
  {
   v2f o;float push=_UseOutline>.5?_OutlineDistance*.001*v.color.a:0;
   o.pos=UnityObjectToClipPos(float4(v.vertex.xyz+normalize(v.normal)*push,1));o.uv=TRANSFORM_TEX(v.texcoord.xy,_Texture2D);
   o.keep=_UseOutline>.5?v.color.a:0;UNITY_TRANSFER_FOG(o,o.pos);return o;
  }
  fixed4 frag(v2f i):SV_Target
  {
   clip(i.keep-.5);clip(tex2D(_Texture2D,i.uv).a-_Cutoff);
   fixed4 c=_OutlineColor;UNITY_APPLY_FOG(i.fogCoord,c);return c;
  }
  ENDCG
 }
 } Fallback "Legacy Shaders/Transparent/Cutout/Diffuse"
}
