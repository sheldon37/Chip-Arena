// Water and darkness magic presentation (owner 2026-10-12: "Water still uses a basic model for Q : Tidal Wave. Also darkness
// basic attack could use some revamping"): the Tidal Wave's body and the floor and surface marks, CPU-built in world space by
// Core/NativeTidalWaveFx.cs (Core/NativeMagicFx.cs NativeMagicVertex) and drawn by Runtime/NativeMagicFxPresentation.cs.
// Premultiplied (Blend One OneMinusSrcAlpha), no depth write, both faces. Built-in forward base pass (ambient SH, main light,
// the three blast flashes), SM3.5, Linear colour, scene fog (premultiplied). Colours arrive linear.
//   POSITION, NORMAL  world (the normal's side does not matter: the shader faces it to the camera)
//   COLOR      the wave's tint rgb (linear), a: opacity
//   TEXCOORD0  data by mode (below)
//   TEXCOORD1  x: flow phase (s; marks: seed), y: mode, z: near-camera fade start (m), w: its length (m)
// Mode 0, the wave body (the wave queue, Transparent-1, one draw per wave with its own bounds): translucent water with a darker
// core where it is thick and seen face on, lighter thin edges and a fresnel rim, flow streaks rushing up the face (the noise
// tile scrolled by the flow phase), a sun glint, crisp white foam where the foam weight (crest, lip, toes, ends) beats the
// noise, a lighter, clearer tint where the background sits close behind it (the camera depth texture, soft particles; not on
// the low graphics tier) and a fade near the camera (the caster's own first-person wave fades much further out).
//   TEXCOORD0  across (m), along the profile (m), foam weight, thickness
// Marks (the mark queue, Transparent-50: before every transparent draw, so the effects over them sort normally):
//   1 wet streak: TEXCOORD0 across (-1..1), along (m), wetness: the floor darkened and a little glossy, its edges broken
//   2 puddle: TEXCOORD0 disc coordinates, wetness, age (s): a darker wet disc and a ripple ring opening at its birth
//   3 void scorch: disc coordinates, intensity, age: a dark burn with a faint violet heart
//   4 void crack: across (-1..1), along (0..1), intensity: a glowing violet crack line with a dark edge, tapering to its tip
Shader "ChipArena/NativeMagicFx"
{
    Properties
    {
        _MagicNoise("Noise tile (RGBA linear)",2D)="gray"{}
        _MagicSoft("Soft particles (depth texture)",Float)=0
        _MagicLow("Low graphics tier",Float)=0
        _VfxFlashGain("Blast flash light",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            sampler2D _MagicNoise;
            float _MagicSoft,_MagicLow,_VfxFlashGain;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _ArenaFlashPos[3];
            float4 _ArenaFlashColor[3];
            // ---- MAGIC FX SHARED BEGIN (HLSL and GLSL: float literals only, no matrices, no implicit conversions) ----
            // One blast flash at a point: Unity's point-light fall-off (NativeBlockLooks.FlashFalloff), 0 at its range.
            float MagicFlashFalloff(float3 world, float4 lightPos)
            {
                float3 d = lightPos.xyz - world;
                float q = length(d) / max(lightPos.w, 0.0001);
                q = q * q;
                return saturate(5.0 * (1.0 - q)) / (1.0 + 25.0 * q);
            }
            // The water body, premultiplied. n faces the camera, v the direction to it; tint and opacity from the vertex; foamW and
            // thick the wave's foam weight and thickness; nA, nB two scrolling noise taps; light the toon-ish light on the water
            // (mostly its own colour), sun and toLight the main light; behind 0 where the scene sits right behind the surface, 1
            // a metre or more behind (soft particles; 1 without them).
            float4 MagicWater(float3 n, float3 v, float4 tint, float foamW, float thick, float4 nA, float4 nB, float3 light, float3 sun, float3 toLight, float behind)
            {
                float ndv = saturate(abs(dot(n, v)));
                float rim = 1.0 - ndv;
                float fres = rim * rim * rim;
                float3 shallow = tint.rgb;
                float3 deep = tint.rgb * float3(0.03, 0.12, 0.26);
                // A darker core where the water is thick and seen face on, lighter where it thins (lip, toes, ends).
                float core = saturate(thick * (0.55 + 0.6 * ndv));
                float3 body = lerp(shallow * 1.05, deep, core);
                // Flow streaks: lighter bands rushing up the face, broken by the finer tap; a soft light band under the crest.
                float streak = smoothstep(0.55, 0.78, nA.r * 0.62 + nB.g * 0.38);
                body = body * (0.78 + 0.5 * streak);
                float3 h = normalize(toLight + v);
                float glint = pow(saturate(dot(n, h)), 64.0);
                float3 col = body * light + shallow * (fres * 1.1) + float3(0.8, 0.95, 1.0) * (fres * 0.22) + sun * (glint * 1.2);
                // Foam: the weight broken by the noise into crisp white patches with a soft blue edge, shaded a little.
                float broken = foamW + (nB.r - 0.5) * 0.55 + (nA.b - 0.5) * 0.22;
                float foam = smoothstep(0.55, 0.6, broken);
                float foamEdge = smoothstep(0.42, 0.55, broken) * (1.0 - foam);
                float3 foamColor = float3(0.9, 0.97, 1.02) * (0.55 + 0.45 * light) * (0.86 + 0.14 * ndv);
                col = lerp(col, lerp(col, shallow * 1.3 * light, 0.6), foamEdge);
                col = lerp(col, foamColor, foam);
                float alpha = tint.a * lerp(0.34 + 0.46 * core + 0.3 * fres, 1.0, foam);
                // Shallow water over a near background: clearer, a little lighter.
                float clear = (1.0 - saturate(behind)) * (1.0 - foam);
                col = lerp(col, shallow * light, clear * 0.45);
                alpha = alpha * (1.0 - 0.4 * clear);
                return float4(col * alpha, alpha);
            }
            // The floor and surface marks, premultiplied (modes 1-4 above); nz a noise tap at the mark, ns one stretched along a streak.
            float4 MagicMark(float mode, float4 data, float4 nz, float4 ns, float3 light)
            {
                if (mode < 1.5)
                {
                    float edge = 1.0 - smoothstep(0.5, 1.0, abs(data.x) + (nz.r - 0.5) * 0.5);
                    float w = data.z * edge;
                    float3 sheen = float3(0.012, 0.03, 0.045) * light + float3(0.2, 0.42, 0.6) * (pow(nz.g, 7.0) * 0.5);
                    // The foam lace the wave leaves: white cell borders, thinning as data.w fades.
                    float lace = smoothstep(0.6, 0.68, ns.g * 0.65 + ns.b * 0.35 + (data.w - 1.0) * 0.45) * edge * saturate(data.w * 3.0);
                    float3 col = sheen * w + float3(0.78, 0.9, 0.96) * light * (lace * 0.55);
                    return float4(col, saturate(w * 0.46 + lace * 0.45));
                }
                if (mode < 2.5)
                {
                    float r = length(data.xy) + (nz.r - 0.5) * 0.3;
                    float disc = 1.0 - smoothstep(0.62, 0.95, r);
                    float open = 0.85 * (1.0 - exp(-data.w * 3.5));
                    float ring = exp(-(r - open) * (r - open) * 60.0) * saturate(1.0 - data.w * 1.1) * 0.45;
                    float w = data.z;
                    float3 col = float3(0.016, 0.04, 0.06) * light * disc + float3(0.5, 0.82, 1.0) * (ring * 0.7);
                    return float4(col * w, disc * w * 0.5);
                }
                if (mode < 3.5)
                {
                    float r = length(data.xy) + (nz.r - 0.5) * 0.35;
                    float disc = 1.0 - smoothstep(0.32, 1.0, r);
                    float heart = 1.0 - smoothstep(0.0, 0.55, r);
                    float a = disc * data.z * 0.82;
                    float3 col = float3(0.2, 0.025, 0.32) * (heart * heart * data.z);
                    return float4(col * a, a);
                }
                float x = data.x + (nz.g - 0.5) * 0.35;
                float taper = saturate((1.0 - data.y) * 1.6) * saturate(data.y * 8.0 + 0.4);
                float core = exp(-x * x * 9.0);
                float hot = exp(-x * x * 42.0);
                float i = data.z * taper;
                float3 col = (float3(0.5, 0.1, 1.05) * core + float3(1.1, 0.6, 1.35) * hot) * (i * 1.8);
                float a = (1.0 - smoothstep(0.55, 1.0, abs(x))) * 0.7 * data.z * taper;
                return float4(col, a);
            }
            // ---- MAGIC FX SHARED END ----
            struct appdata
            {
                float3 vertex:POSITION;
                float3 normal:NORMAL;
                float4 color:COLOR;
                float4 data:TEXCOORD0;
                float4 extra:TEXCOORD1;
            };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float4 color:COLOR;
                float4 data:TEXCOORD0;
                float4 extra:TEXCOORD1;
                float3 normal:TEXCOORD2;
                float3 world:TEXCOORD3;
                float3 light:TEXCOORD4;
                UNITY_FOG_COORDS(5)
                float4 screen:TEXCOORD6;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=mul(UNITY_MATRIX_VP,float4(v.vertex,1.0));
                o.color=v.color;o.data=v.data;o.extra=v.extra;o.normal=v.normal;o.world=v.vertex;
                // Mostly the water's own colour (the toon puffs' rule), some ambient and moon, the blast flashes.
                float3 flash=0;
                for(int k=0;k<3;k++)flash+=_ArenaFlashColor[k].rgb*MagicFlashFalloff(v.vertex,_ArenaFlashPos[k]);
                o.light=0.62+0.38*(ShadeSH9(float4(0,1,0,1))+_LightColor0.rgb*saturate(0.3+0.7*_WorldSpaceLightPos0.y))+0.6*flash*_VfxFlashGain;
                o.screen=ComputeScreenPos(o.pos);o.screen.z=-mul(UNITY_MATRIX_V,float4(v.vertex,1.0)).z;
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float mode=i.extra.y;
                float3 toCamera=_WorldSpaceCameraPos-i.world;float distance=length(toCamera);float3 view=toCamera/max(distance,0.0001);
                // Sampled outside the branches (no gradients in divergent flow): two scrolling taps for the water, one for a mark.
                float4 nA=tex2D(_MagicNoise,float2(i.data.x*0.43,i.data.y*0.11-i.extra.x*0.62));
                float4 nB=tex2D(_MagicNoise,float2(i.data.x*0.47+0.31,i.data.y*0.41-i.extra.x*1.9));
                float4 nz=tex2D(_MagicNoise,i.data.xy*0.31+i.extra.xx*0.137);
                float4 ns=tex2D(_MagicNoise,float2(i.data.x*0.55,i.data.y*0.09)+i.extra.xx*0.071);
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                bool soft=_MagicSoft>0.5&&_MagicLow<0.5;
                float4 c;
                if(mode<0.5)
                {
                    float3 n=normalize(i.normal);if(dot(n,view)<0.0)n=-n;
                    // The low tier keeps one tap.
                    if(_MagicLow>0.5)nB=nA.gbra;
                    float behind=soft?saturate((scene-i.screen.z)/1.2):1.0;
                    c=MagicWater(n,view,i.color,i.data.z,i.data.w,nA,nB,i.light,_LightColor0.rgb,_WorldSpaceLightPos0.xyz,behind);
                    // Meets the floor and walls softly (no hard cut line), and fades near the camera.
                    c*=soft?saturate((scene-i.screen.z)/0.06):1.0;
                    c*=saturate((distance-i.extra.z)/max(i.extra.w,0.01));
                }
                else c=MagicMark(mode,i.data,nz,ns,i.light);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(unity_FogColor.rgb*c.a,0));
                #if defined(UNITY_COLORSPACE_GAMMA)
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
