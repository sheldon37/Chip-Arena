// The blood pack's splashes (owner 2026-11-10; Core NativeBloodSplash, Runtime NativeBloodPackPresentation, docs/BLOOD-PACK.md):
// one camera-facing quad per splash, a frame of the pack's simulated sheet blended into the next. The sheet is the pack's
// grey shading with the fluid's shape in alpha, tinted with the game's blood colour; its normal map lights it per pixel:
// the ambient, the key light half-wrapped (a thin sheet of fluid takes light from behind too), the arena's practical lights
// and a wet highlight from the key and the strongest practical (NativeBloodLight.cginc). Vertices carry everything:
//   POSITION   world
//   NORMAL     the quad's normal (toward the camera)
//   TANGENT    xyz the quad's +u axis (the spray), w the side of its +v axis (-1 mirrored)
//   COLOR      the tint (linear) and the splash's opacity
//   TEXCOORD0  xy the first frame's coordinates, zw the next frame's
//   TEXCOORD1  x the blend from the first to the next
// Premultiplied, no depth write, scene fog. The materials (one per sheet) come from the local bake: without it nothing draws.
Shader "ChipArena/NativeBloodSplash"
{
    Properties
    {
        _MainTex("Sheet (grey shading, alpha shape)",2D)="black"{}
        _NormalMap("Sheet normals",2D)="bump"{}
        _Gloss("Wet highlight",Range(0,1))=0.75
        _Shade("Shading gain",Float)=2.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "NativeBloodLight.cginc"
            sampler2D _MainTex,_NormalMap;float _Gloss,_Shade;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float4 color:COLOR;float4 uv:TEXCOORD0;float4 mix:TEXCOORD1;};
            struct v2f
            {
                float4 pos:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 world:TEXCOORD1;float3 n:TEXCOORD2;float3 t:TEXCOORD3;float3 b:TEXCOORD4;
                float3 soft:TEXCOORD5;float3 domDir:TEXCOORD6;float3 domColor:TEXCOORD7;UNITY_FOG_COORDS(8)
            };
            v2f vert(appdata v)
            {
                v2f o;o.pos=mul(UNITY_MATRIX_VP,float4(v.vertex.xyz,1.0));o.color=v.color;o.uv=v.uv;o.world=float4(v.vertex.xyz,v.mix.x);
                o.n=normalize(v.normal);o.t=normalize(v.tangent.xyz);o.b=cross(o.n,o.t)*v.tangent.w;
                // The ambient from above and from the camera's side alike, and the practical lights with no facing: a splash is
                // a thin, tumbling sheet of fluid.
                float3 amb=0.5*(ShadeSH9(float4(0.0,1.0,0.0,1.0))+ShadeSH9(float4(o.n,1.0)));
                o.soft=max(amb,0.0)+min(BloodArenaLights(v.vertex.xyz,o.n,1.0,o.domDir,o.domColor)*0.7,float3(1.2,1.2,1.2));
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float blend=i.world.w;
                float4 c=lerp(tex2D(_MainTex,i.uv.xy),tex2D(_MainTex,i.uv.zw),blend);
                float3 nt=UnpackNormal(lerp(tex2D(_NormalMap,i.uv.xy),tex2D(_NormalMap,i.uv.zw),blend));
                float a=saturate(c.a*1.5)*i.color.a;
                clip(a-0.004);
                float3 n=normalize(i.t*nt.x+i.b*nt.y+i.n*max(nt.z,0.2));
                float3 v=normalize(_WorldSpaceCameraPos-i.world.xyz);float3 l=_WorldSpaceLightPos0.xyz;
                float s=c.r*_Shade;float3 albedo=i.color.rgb*min(s*sqrt(s),1.7);
                float3 key=_LightColor0.rgb*(dot(n,l)*0.5+0.5);
                // Never darker than a dim room's light: blood reads red in the dark stages too.
                float3 lit=albedo*max(i.soft+key+i.domColor*(0.3*saturate(dot(n,i.domDir))),float3(0.3,0.3,0.3));
                float fres=pow(1.0-saturate(dot(n,v)),4.0);
                float3 shine=_LightColor0.rgb*BloodGlint(n,v,l,_Gloss,0.14)+i.domColor*(BloodGlint(n,v,i.domDir,_Gloss,0.05)*0.6)+i.soft*(fres*0.2);
                float3 col=lit+shine*_Gloss;
                #ifdef UNITY_COLORSPACE_GAMMA
                col=LinearToGammaSpace(max(col,0.0));
                #endif
                float4 shaded=float4(col,1.0);UNITY_APPLY_FOG(i.fogCoord,shaded);
                return float4(shaded.rgb*a,a);
            }
            ENDCG
        }
    }
    Fallback Off
}
