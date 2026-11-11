// The blood pack's decals (owner 2026-11-10; Core NativeBloodDecalLook, Runtime NativeBloodPackPresentation, docs/BLOOD-PACK.md):
// one world-space quad per splat on its surface, a pack sprite sheet of blood spreading from where it landed (6 x 6 frames:
// grey shading, the shape in alpha) with its normal map. The splat spreads in on the GPU (the frame from its landing time
// and _BloodNow, blended into the next) and keeps the last frame. Colours are today's (NativeBloodSplat): the rim and the
// wet core, lit on the CPU for the flat surface; the normal map turns the key's share of that light and, while the blood is
// wet, takes the highlights (NativeBloodLight.cginc). Vertices carry everything:
//   POSITION   world
//   NORMAL     the surface's normal
//   TANGENT    xyz the quad's +u axis (the travel over the surface), w the side of its +v axis (-1 mirrored)
//   COLOR      the rim colour (sRGB, lit on the CPU) and the splat's opacity
//   TEXCOORD0  xy the cell coordinates (0..1), z how wet it still is (1 fresh, 0 dry), w the time it landed (s)
//   TEXCOORD1  rgb the wet core colour (sRGB, lit), w the seconds it takes to spread
// Premultiplied, no depth write, scene fog, queued where today's splats draw (Transparent-60). One material per sheet, from
// the local bake: without it the runtime keeps today's splats.
Shader "ChipArena/NativeBloodDecal"
{
    Properties
    {
        _MainTex("Sheet (grey shading, alpha shape)",2D)="black"{}
        _NormalMap("Sheet normals",2D)="bump"{}
        _Gloss("Wet highlight",Range(0,1))=0.6
        _BloodNow("The field clock (s)",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-60" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            sampler2D _MainTex,_NormalMap;float _Gloss,_BloodNow;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float4 color:COLOR;float4 uv:TEXCOORD0;float4 core:TEXCOORD1;};
            struct v2f
            {
                float4 pos:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 core:TEXCOORD1;float4 world:TEXCOORD2;float3 n:TEXCOORD3;float3 t:TEXCOORD4;float3 b:TEXCOORD5;
                float3 domDir:TEXCOORD6;float4 domColor:TEXCOORD7;UNITY_FOG_COORDS(8)
            };
            // ---- BLOOD DECAL SHARED BEGIN (HLSL and GLSL: float literals only, no texture reads) ----
            // The frame a splat shows (0..35, fractional): fast first, easing to rest (Core NativeBloodDecalLook.Frame).
            float DecalFrame(float age,float spread)
            {
                float u=saturate(age/max(spread,0.001));
                return (1.0-(1.0-u)*(1.0-u))*35.0;
            }
            // A cell coordinate on the 6 x 6 sheet for a frame (rows from the top).
            float2 DecalCell(float2 uv,float frame)
            {
                float row=floor(frame/6.0);
                float col=frame-row*6.0;
                return float2((col+uv.x)/6.0,1.0-(row+1.0-uv.y)/6.0);
            }
            // A splat texel's colour (linear): thin blood takes the rim colour, the thick middle the wet core; the pack's grey
            // shading models it, more while it is wet.
            float3 DecalColor(float shade,float3 rim,float3 core,float wet)
            {
                float inner=smoothstep(0.3,0.62,shade);
                float3 base=lerp(rim,core,inner*(0.55+0.45*wet));
                return base*lerp(1.0,0.45+1.5*shade,0.55+0.3*wet);
            }
            // ---- BLOOD DECAL SHARED END ----
            v2f vert(appdata v)
            {
                v2f o;o.pos=mul(UNITY_MATRIX_VP,float4(v.vertex.xyz,1.0));o.color=v.color;o.core=v.core;
                float f=DecalFrame(_BloodNow-v.uv.w,v.core.w);float fa=floor(f);float fb=min(fa+1.0,35.0);
                // A little in from the cell's edge, so a neighbouring frame never bleeds in.
                float2 cell=clamp(v.uv.xy,0.004,0.996);
                o.uv=float4(DecalCell(cell,fa),DecalCell(cell,fb));o.world=float4(v.vertex.xyz,f-fa);
                o.n=normalize(v.normal);o.t=normalize(v.tangent.xyz);o.b=cross(o.n,o.t)*v.tangent.w;
                float3 dc;BloodArenaLights(v.vertex.xyz,o.n,0.0,o.domDir,dc);o.domColor=float4(dc,v.uv.z);
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float blend=i.world.w;float wet=i.domColor.w;
                float4 c=lerp(tex2D(_MainTex,i.uv.xy),tex2D(_MainTex,i.uv.zw),blend);
                float3 nt=UnpackNormal(lerp(tex2D(_NormalMap,i.uv.xy),tex2D(_NormalMap,i.uv.zw),blend));
                float a=saturate(c.a*1.25)*i.color.a;
                float3 n=normalize(i.t*nt.x+i.b*nt.y+i.n*max(nt.z,0.2));
                float3 v=normalize(_WorldSpaceCameraPos-i.world.xyz);float3 l=_WorldSpaceLightPos0.xyz;
                float3 col=DecalColor(c.r,GammaToLinearSpace(i.color.rgb),GammaToLinearSpace(i.core.rgb),wet);
                // The CPU lit the flat surface: the normal map turns the key's share.
                col*=saturate(1.0+0.5*(dot(n,l)-dot(i.n,l)));
                float gloss=_Gloss*(0.25+0.75*wet);
                float3 shine=_LightColor0.rgb*BloodGlint(n,v,l,gloss,0.03)+i.domColor.rgb*(BloodGlint(n,v,i.domDir,gloss,0.0)*0.6);
                float fres=pow(1.0-saturate(dot(n,v)),5.0);
                col+=shine*gloss+max(ShadeSH9(float4(reflect(-v,n),1.0)),0.0)*(fres*0.07*gloss);
                #ifdef UNITY_COLORSPACE_GAMMA
                float3 fog=GammaToLinearSpace(unity_FogColor.rgb);
                #else
                float3 fog=unity_FogColor.rgb;
                #endif
                float4 shaded=float4(col,1.0);UNITY_APPLY_FOG_COLOR(i.fogCoord,shaded,float4(fog,1.0));col=shaded.rgb;
                #ifdef UNITY_COLORSPACE_GAMMA
                col=LinearToGammaSpace(max(col,0.0));
                #endif
                return float4(col*a,a);
            }
            ENDCG
        }
    }
    Fallback Off
}
