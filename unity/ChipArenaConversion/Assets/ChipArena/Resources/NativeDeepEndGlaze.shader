Shader "ChipArena/NativeDeepEndGlaze"
{
    // THE DEEP END's wet glaze and chrome (docs/EXPEDITION-BOSS-BACKROOMS.md section 4: "the cleanest thing in the game"), added over the gear's toon (ZombieGearMaterials) on the same mesh: the room's reflection on the tile (its round lights sliding over the tiles as it moves, each
    // tile's own small tilt breaking them), sharp highlights from the practical lights, full mirror on chrome, and the caustic web on it while it is under or near the water. uv.y is the mesh's gloss (0 grout .. 1 tile .. 2 chrome), or _DeepGloss on the rigid parts.
    Properties {_DeepGloss ("Gloss (negative: the mesh's own)", Float) = -1}
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest+60" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Blend One One Cull Back Offset -1,-1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "NativeDeepEndRoom.cginc"
            float _DeepGloss;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float gloss:TEXCOORD2;UNITY_FOG_COORDS(3)};
            v2f vert(float4 vertex:POSITION,float3 normal:NORMAL,float2 uv:TEXCOORD0)
            {
                v2f o;float4 w=mul(unity_ObjectToWorld,vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;o.normal=UnityObjectToWorldNormal(normal);o.gloss=_DeepGloss>=0.0?_DeepGloss:uv.y;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);float3 v=normalize(_WorldSpaceCameraPos-i.world);float nv=saturate(dot(n,v));float g=i.gloss;
                float metal=saturate(g-1.0);float f0=lerp(0.05,0.72,metal);float fres=f0+(1.0-f0)*pow(1.0-nv,5.0);float glaze=saturate(g*1.15);
                float3 r=reflect(-v,n);float3 room=DeepRoomColour(i.world,r);
                float3 c=room*fres*glaze*(1.0+metal*0.5);
                for(int k=0;k<8;k++)
                {
                    float3 d=_ArenaLightPos[k].xyz-i.world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float fall=saturate(1.0-q*q);if(fall<=0.0)continue;float3 l=d/len;float3 h=normalize(l+v);float ndh=saturate(dot(n,h));
                    c+=_ArenaLightColor[k].rgb*fall*fall*(pow(ndh,420.0)*16.0+pow(ndh,48.0)*0.32)*glaze;
                }
                // The web on it, near and under the water.
                float2 q2=i.world.xz+float2(i.world.y*0.4,-i.world.y*0.6);float above=i.world.y-_DeepTide;float near=above<0.0?exp(above*0.35):exp(-above*0.5);float web=DeepCaustic(q2*0.7,_DeepClock*0.5);
                float2 fuv=(clamp(i.world.xz,_DeepRoom.xy-_DeepRoom.zz+0.3,_DeepRoom.xy+_DeepRoom.zz-0.3)-_DeepRect.xy)/max(_DeepRect.z,0.01);float3 glow=tex2D(_DeepGlowTex,fuv).rgb;
                c+=(glow*0.8+float3(0.3,0.42,0.42)*(_DeepLight.x*0.5+_DeepLight.y*0.25))*web*near*saturate(g)*0.42;
                fixed4 o=fixed4(c,0.0);UNITY_APPLY_FOG_COLOR(i.fogCoord,o,fixed4(0,0,0,0));return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
