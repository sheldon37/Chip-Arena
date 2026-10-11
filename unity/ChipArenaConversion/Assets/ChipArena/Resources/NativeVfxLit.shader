// Effects overhaul, base layer (owner 2026-10-11): the lit meshes of the effect particles and the shock fronts, CPU-built in
// world space into one dynamic mesh per material by Runtime/NativeVfxDraw.cs. Built-in forward base pass (main light and
// ambient SH), SM3.5, Linear colour, scene fog. The three blast flashes reach these meshes through the same globals the
// dressed blocks read (NativeArenaDressing.Flashes), since there is no add pass.
// Lit material (_Shock 0; opaque, depth write): rubble chunks and Quake slabs (flat-shaded, a faint rim), crystal shards
// (a strong fresnel edge in their tint, a specular glint, a little emission) and the chip bomb (dark metal casing with a
// specular highlight; its band and fuse tip glow with the fuse pulse).
//   POSITION, NORMAL  world
//   COLOR      albedo rgb, a: specular strength
//   TEXCOORD0  emissive rgb (linear, HDR), w: gloss (0..1)
//   TEXCOORD1  fresnel rim colour rgb (linear, HDR), w unused
// Shock material (_Shock 1; additive, no depth write, Cull Off): one expanding fresnel shell per blast, impact or Nova front
// (Core NativeBlastSim.Shocks): a thin bright rim with a clear centre (no veil), broken up a little by the noise tile (not
// on the low graphics tier, _VfxLow), bright at the arrival and quickly dimmer as it travels, fading out at the gameplay
// radius; a dome's base fades so it never draws a ring or a bowl on the floor. While the camera is outside the shell only
// its front faces draw (the far inner face would add a second arch and a tint over the whole interior: the glassy bubble of
// review 2026-10-11); from inside, the surrounding shell draws.
//   COLOR      hot colour rgb (linear, HDR, times the record's fade), a: front progress (0..1)
//   TEXCOORD0  cool colour rgb (the same scale), w: normalized age u
//   TEXCOORD1  seed, dome (0/1), height on the unit shell (normal y), w: the shell's radius (m)
Shader "ChipArena/NativeVfxLit"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
        _Shock("Shock shell",Float)=0
        _VfxNoise("Noise tile (RGBA linear)",2D)="gray"{}
        _VfxFlashGain("Blast flash light",Float)=1
        _VfxLow("Low graphics tier",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend [_Src] [_Dst]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            sampler2D _VfxNoise;
            float _Shock,_VfxFlashGain,_VfxLow;
            float4 _ArenaFlashPos[3];
            float4 _ArenaFlashColor[3];
            // ---- VFX LIT SHARED BEGIN (HLSL and GLSL: float literals only, no matrices, no implicit conversions) ----
            // One blast flash on a surface: Unity's point-light fall-off (NativeBlockLooks.FlashFalloff) times a wrapped N.L.
            float3 VfxLitFlash(float3 world, float3 n, float4 lightPos, float4 lightColor)
            {
                float3 d = lightPos.xyz - world;
                float len = max(length(d), 0.0001);
                float q = len / max(lightPos.w, 0.0001);
                q = q * q;
                float att = saturate(5.0 * (1.0 - q)) / (1.0 + 25.0 * q);
                float ndl = saturate((dot(n, d / len) + 0.25) / 1.25);
                return lightColor.rgb * (att * ndl);
            }
            // The lit material: diffuse (ambient, main light and flashes, per vertex) on the albedo, a Blinn-Phong glint from the
            // main light, a fresnel rim and the emission.
            float3 VfxLit(float3 albedo, float spec, float gloss, float3 emissive, float3 rim, float3 n, float3 v, float3 l, float3 sun, float3 diffuse)
            {
                float3 h = normalize(l + v);
                float glint = pow(saturate(dot(n, h)), 8.0 + 88.0 * gloss) * spec * (0.25 + gloss);
                float fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
                return albedo * diffuse + sun * glint + rim * fresnel + emissive;
            }
            // The shock shell: a thin rim by the view angle and a clear centre, the noise breakup, the dome's base fade, from the
            // hot to the cool colour as it travels; full at the arrival (progress 0), .46 a quarter of the way, .27 half way and
            // .17 at the gameplay radius.
            float3 VfxShock(float3 hot, float3 cool, float progress, float n_dot_v, float height, float dome, float breakup)
            {
                float facing = saturate(abs(n_dot_v));
                float body = pow(1.0 - facing, 4.0);
                float base = lerp(1.0, smoothstep(0.05, 0.5, height), dome);
                float travel = 0.15 + 0.85 * exp(-4.0 * progress);
                float3 tint = lerp(hot, cool, saturate(progress * 1.1));
                return tint * (body * base * travel * (0.45 + 1.1 * breakup));
            }
            // ---- VFX LIT SHARED END ----
            struct appdata
            {
                float3 vertex:POSITION;
                float3 normal:NORMAL;
                float4 color:COLOR;
                float4 a:TEXCOORD0;
                float4 b:TEXCOORD1;
            };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float4 color:COLOR;
                float4 a:TEXCOORD0;
                float4 b:TEXCOORD1;
                float3 normal:TEXCOORD2;
                float3 world:TEXCOORD3;
                float3 diffuse:TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=mul(UNITY_MATRIX_VP,float4(v.vertex,1.0));
                o.color=v.color;o.a=v.a;o.b=v.b;o.world=v.vertex;
                float3 n=normalize(v.normal);o.normal=n;
                o.diffuse=0;
                if(_Shock<0.5)
                {
                    float3 diffuse=ShadeSH9(float4(n,1.0))+_LightColor0.rgb*saturate(dot(n,_WorldSpaceLightPos0.xyz));
                    float3 flash=0;
                    for(int k=0;k<3;k++)flash+=VfxLitFlash(v.vertex,n,_ArenaFlashPos[k],_ArenaFlashColor[k]);
                    o.diffuse=diffuse+flash*_VfxFlashGain;
                }
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal),view=normalize(_WorldSpaceCameraPos-i.world);
                // The shell's breakup: the noise tile projected along y, drifting with its seed and age (no mips, so a level-0
                // read may sit in the branch); the low tier skips the read.
                float4 grain=float4(0.5,0.5,0.5,0.5);
                if(_Shock>0.5&&_VfxLow<0.5)grain=tex2Dlod(_VfxNoise,float4(n.xz*0.42+float2(i.b.x*0.137,i.b.x*0.291+i.a.w*0.35),0,0));
                float4 c;
                if(_Shock>0.5)
                {
                    // Fades where the shell passes the camera, so a front around the viewer does not fill the screen.
                    float nearFade=saturate((length(_WorldSpaceCameraPos-i.world)-0.4)/2.2);
                    // Front faces only while the camera is outside the shell (its centre: this point less the radius along n).
                    float front=dot(n,view)<0.0&&length(_WorldSpaceCameraPos-(i.world-n*i.b.w))>i.b.w?0.0:1.0;
                    c=float4(VfxShock(i.color.rgb,i.a.rgb,i.color.a,dot(n,view),i.b.z,i.b.y,grain.r*0.6+grain.g*0.4)*(nearFade*front),0);
                    UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));
                }
                else
                {
                    c=float4(VfxLit(i.color.rgb,i.color.a,i.a.w,i.a.rgb,i.b.rgb,n,view,_WorldSpaceLightPos0.xyz,_LightColor0.rgb,i.diffuse),1);
                    UNITY_APPLY_FOG(i.fogCoord,c);
                }
                #if defined(UNITY_COLORSPACE_GAMMA)
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
