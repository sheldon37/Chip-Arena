Shader "ChipArena/VisualBodyToon" {
 Properties {
  _Texture2D("Colour weights (rgb), cut-out (a)",2D)="white"{}
  _UseCustomTexture("Nine colours from the weights (0: the texture as drawn)",Float)=1
  _Color_1("Layer 1 colour 1",Color)=(1,1,1,1) _Color_2("Layer 1 colour 2",Color)=(1,1,1,1) _Color_3("Layer 1 colour 3 (accent)",Color)=(1,1,1,1)
  _Color_4("Layer 2 colour 1",Color)=(1,1,1,1) _Color_5("Layer 2 colour 2",Color)=(1,1,1,1) _Color_6("Layer 2 colour 3 (accent)",Color)=(1,1,1,1)
  _Color_7("Layer 3 colour 1",Color)=(1,1,1,1) _Color_8("Layer 3 colour 2",Color)=(1,1,1,1) _Color_9("Layer 3 colour 3 (accent)",Color)=(1,1,1,1)
  _Cutoff("Cut-out",Range(0,1))=.5
  _UseOutline("Outline (where the vertex alpha is .5 or more)",Float)=1
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
 //  - the outline, matched to the anime map style's lines (NativeToonEdge): an inverted hull as wide on screen as the map's
 //    silhouettes (two lines of one pixel per 1080 screen lines, at every distance), times the vertex alpha (none where it
 //    is under .5); coloured as the map colours a line, the surface's own lit colour multiplied toward the map's tint
 //    (_ArenaEdgeColor) by its strength (_ArenaEdge.x) and faded out over its distances (_ArenaEdge.y to .z m), so the line
 //    reads as a darker version of the pilot as a map line does of a wall. A map without the style writes no line: the
 //    pilot keeps the style's own (#2A1E40 at full strength, faded out from 40 to 110 m), as on the low tier, where the
 //    map's pass is off but its tint and distances are written;
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
 // The outline, in the base pass after the body (the body's front faces hide the hull's inside): the key, the ambient, the
 // actor and the muzzle lights as the body takes them, so a line in shade is as much darker as the shade it borders.
 Pass {
  Name "OUTLINE" Tags { "LightMode"="ForwardBase" } Cull Front ZWrite On
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma target 3.5
  #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
  #pragma multi_compile_fog
  #include "UnityCG.cginc"
  #include "Lighting.cginc"
  #include "AutoLight.cginc"
  sampler2D _Texture2D;float4 _Texture2D_ST;float _UseCustomTexture,_UseOutline,_Cutoff;
  float4 _Color_1,_Color_2,_Color_3,_Color_4,_Color_5,_Color_6,_Color_7,_Color_8,_Color_9;
  float _ArenaActorLit;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];
  #include "NativeWardLight.cginc"
  #include "NativeMuzzleLight.cginc"
  #include "NativeToon.cginc"
  // The map's line (NativeToonEdge.shader's globals, NativeLighting writes them): x strength, y and z its fade (m); rgb tint.
  float4 _ArenaEdge;float4 _ArenaEdgeColor;
  float3 VisualColours(float3 w,float3 layer)
  {
   float3 one=_Color_1.rgb*w.x+_Color_2.rgb*w.y+_Color_3.rgb*w.z,two=_Color_4.rgb*w.x+_Color_5.rgb*w.y+_Color_6.rgb*w.z,three=_Color_7.rgb*w.x+_Color_8.rgb*w.y+_Color_9.rgb*w.z;
   return lerp(lerp(one*layer.x,two,layer.y),three,layer.z);
  }
  // The style's own line where a map writes none: its tint (#2A1E40, as the menus' and most maps') and distances.
  float3 EdgeTint()
  {
   if(_ArenaEdge.z>0)return _ArenaEdgeColor.rgb;
   #ifdef UNITY_COLORSPACE_GAMMA
   return float3(.1647,.1176,.2510);
   #else
   return float3(.0232,.0130,.0513);
   #endif
  }
  struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 texcoord:TEXCOORD0;float4 color:COLOR;};
  struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:TEXCOORD1;float3 world:TEXCOORD2;float3 normal:TEXCOORD3;float ink:TEXCOORD4;UNITY_SHADOW_COORDS(5) UNITY_FOG_COORDS(6)};
  v2f vert(appdata v)
  {
   v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;float3 n=normalize(UnityObjectToWorldNormal(v.normal));
   float depth=-mul(UNITY_MATRIX_V,float4(world,1)).z;
   float2 fade=_ArenaEdge.z>0?_ArenaEdge.yz:float2(40,110);float ink=(_ArenaEdge.x>0?_ArenaEdge.x:1)*(1-smoothstep(fade.x,fade.y,depth));
   // A screen pixel's size at this depth (m), and the map's silhouette width in pixels: a normal across the view (the
   // silhouette) moves the hull out by exactly that many on screen.
   float pixel=unity_OrthoParams.w>.5?2*unity_OrthoParams.y/_ScreenParams.y:2*depth/(unity_CameraProjection._m11*_ScreenParams.y);
   float keep=_UseOutline>.5&&v.color.a>=.5&&ink>0?1:0;float wide=keep*2*max(1,round(_ScreenParams.y/1080))*v.color.a;
   // Then back along the view ray (the same pixel on screen) by 2 cm and four times that push: a layer's hull never
   // specks through the layer it covers (a shoe's opening on the sock), and small steps (a chin over the neck, hair
   // strands) stop being lined as the pixels grow, as the map's creases fade out with their footprint, while a limb in
   // front of the body still lines its edge.
   float3 ray=unity_OrthoParams.w>.5?-UNITY_MATRIX_V[2].xyz:normalize(world-_WorldSpaceCameraPos);
   v.vertex=mul(unity_WorldToObject,float4(world+n*(wide*pixel)+ray*(4*wide*pixel+.02),1));
   // Lit with the normal turned across the view (the silhouette's): a line takes the light of the edge it borders, as a
   // map line takes its nearer surface's, not the far side's the hull's back faces show.
   float3 across=n-ray*dot(n,ray);float lean=length(across);across=lean>.001?across/lean:n;
   o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord.xy,_Texture2D);o.color=v.color;o.color.a=keep;
   o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=across;o.ink=ink;
   UNITY_TRANSFER_SHADOW(o,v.texcoord.xy);UNITY_TRANSFER_FOG(o,o.pos);return o;
  }
  fixed4 frag(v2f i):SV_Target
  {
   clip(i.color.a-.5);float4 map=tex2D(_Texture2D,i.uv);clip(map.a-_Cutoff);
   float3 albedo=_UseCustomTexture>.5?VisualColours(map.rgb,i.color.rgb):map.rgb;float3 n=normalize(i.normal);
   UNITY_LIGHT_ATTENUATION(atten,i,i.world);float ndl=dot(n,_WorldSpaceLightPos0.xyz);float3 sh=ShadeSH9(float4(n,1));
   // As NativeToonSurface's ArenaToon lights the body: Lambert while the style is off, the ramp over the flattened SH on.
   float3 light=_ArenaToon.x<.5?sh+_LightColor0.rgb*(saturate(ndl)*atten):ToonDiffuse(ToonAmbient(sh,ShadeSH9(float4(0,1,0,1)),ShadeSH9(float4(0,-1,0,1)),n.y),_LightColor0.rgb,ndl,atten);
   light+=ToonPoints(MuzzleLights(i.world,n,.35));if(_ArenaActorLit>.5)light+=ToonPoints(ActorLights(i.world,n));
   fixed4 c=fixed4(albedo*light*lerp(float3(1,1,1),EdgeTint(),saturate(i.ink)),1);UNITY_APPLY_FOG(i.fogCoord,c);return c;
  }
  ENDCG
 }
 } Fallback "Legacy Shaders/Transparent/Cutout/Diffuse"
}
