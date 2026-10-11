// Effects overhaul, base layer (owner 2026-10-11: "The effects seem a bit outdated and there still seems to be cube
// placeholders for certain impact effects"). Every pooled blast, impact, field and spark particle that is not a lit mesh
// is one camera-facing quad in a single dynamic mesh (Runtime/NativeVfxDraw.cs), expanded here from per-vertex data and
// drawn premultiplied (Blend One OneMinusSrcAlpha), so glows (alpha 0: purely additive) and smoke (alpha > 0) share one
// back-to-front sorted draw and a glow behind a smoke puff is covered by it. Queue Transparent (the material), sorted among
// the other transparent draws by the batch's bounds. Built-in forward base pass (main light and ambient SH), SM3.5, Linear
// colour, scene fog (premultiplied, so additive light fogs toward black). Colours arrive linear; glows may exceed 1 for the
// match camera's bloom (Runtime/NativeBloom.cs, branch claude/fx-bloom).
//   POSITION   particle centre (world; a streak's segment midpoint)
//   COLOR      linear rgb (HDR for glows), a: coverage (smoke opacity; 0 for an additive glow)
//   TEXCOORD0  corner x, corner y (each -1 or 1), full size (m; a streak's width), roll (rad)
//   TEXCOORD1  normalized age u (0..1), seed, mode, floor height (-1e9: none)
//   TEXCOORD2  streak: world segment, tail to head (xyz) and taper (w); billboards: x a smoke puff's own-colour glow,
//              w height/width
// Modes (NativeVfxDraw constants): 0 mote, 1 core, 2 ring (bubble), 3 snow (soft glow sprites); 4 streak (a capsule along
// the segment, oriented to the camera, a hot core, dimmer tail); 5 smoke (an atlas puff, lit by the ambient, the main light
// and the three blast flashes, eroding over u instead of shrinking; a saturated cloud adds a little glow in its colour);
// 6 fireball (an atlas puff glowing in the particle's own hue, densest and hottest young, handing off into soot; the
// white-hot centre is the Core glows', review 2026-10-11).
// Smoke and fireball puffs fade where they dip below their floor; every quad fades near the camera and, soft particles,
// where it meets the scene's depth (_CameraDepthTexture, _VfxSoft 1; 0 on the low graphics tier, which asks for no depth
// texture). The smoke atlas (Core/NativeVfxTextures.SmokeAtlas: R density, G baked light, B erosion threshold, A edge) is
// built off the main thread; until it is uploaded (_VfxAtlasReady 0) the puffs use a round stand-in with the same channels.
Shader "ChipArena/NativeVfx"
{
    Properties
    {
        _VfxAtlas("Smoke atlas (RGBA linear)",2D)="black"{}
        _VfxAtlasReady("Atlas uploaded",Float)=0
        _VfxFlashGain("Blast flash light on smoke",Float)=1
        _VfxSoft("Soft particles (depth texture)",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
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
            sampler2D _VfxAtlas;
            float _VfxAtlasReady,_VfxFlashGain,_VfxSoft;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            // The three blast flashes (NativeArenaDressing.Flashes): position and range, colour times intensity as Unity applies it.
            float4 _ArenaFlashPos[3];
            float4 _ArenaFlashColor[3];
            // ---- VFX SHARED BEGIN (HLSL and GLSL: float literals only, no matrices, no implicit conversions) ----
            // A soft glow sprite at corner c (unit disc). profile: 0 mote, 1 core (wider halo), 2 ring (bubble), 3 snow.
            float VfxGlow(float2 c, float profile)
            {
                float r2 = dot(c, c);
                float r = sqrt(r2);
                float edge = saturate(1.0 - r2);
                float tight = exp(-r2 * 7.0);
                float wide = exp(-r2 * 2.6);
                float mote = tight * 0.72 + wide * 0.28;
                float core = tight * 0.5 + wide * 0.5;
                float ring = exp(-(r - 0.6) * (r - 0.6) * 55.0) * 0.85 + tight * 0.15;
                float snow = exp(-r2 * 5.0);
                float a = profile < 0.5 ? mote : (profile < 1.5 ? core : (profile < 2.5 ? ring : snow));
                return a * edge;
            }
            // The camera-plane offset of a streak corner: axis is the segment (tail to head) in the camera plane, corner the quad
            // corner (-1/1, x along, y across), hw the half width. coords: x along and y across in half widths, z the segment's
            // half length in half widths.
            float2 VfxStreakOffset(float2 axis, float2 corner, float hw, out float3 coords)
            {
                float len = length(axis);
                float2 dir = len > 0.00001 ? axis / len : float2(1.0, 0.0);
                float2 side = float2(-dir.y, dir.x);
                float hl = len * 0.5;
                float reach = hl + hw;
                coords = float3(corner.x * reach / hw, corner.y, hl / hw);
                return dir * (corner.x * reach) + side * (corner.y * hw);
            }
            // A streak's brightness: a capsule around the segment, a hot centre line, the tail (-x) dimmer by taper.
            float VfxStreak(float3 coords, float taper)
            {
                float dx = max(abs(coords.x) - coords.z, 0.0);
                float r2 = dx * dx + coords.y * coords.y;
                float body = exp(-r2 * 3.2) * saturate(1.0 - r2);
                float along = saturate((coords.x + coords.z + 1.0) / (2.0 * coords.z + 2.0));
                float tail = 1.0 - taper * (1.0 - along) * 0.85;
                float centre = 1.0 + 1.1 * exp(-coords.y * coords.y * 14.0) * saturate(1.0 - dx);
                return body * tail * centre;
            }
            // Core/NativeVfxTextures.Dissolve: density times a soft step on the texel's erosion threshold, gone at u = 1.
            float VfxDissolve(float density, float threshold, float u)
            {
                float soft = 0.15;
                float e = saturate((threshold + soft - u * (1.0 + soft)) / soft);
                return u < 1.0 ? density * e * e * (3.0 - 2.0 * e) : 0.0;
            }
            // A puff fades in over the first few percent of its life rather than popping in.
            float VfxBirth(float u)
            {
                return saturate(u * 22.0 + 0.2);
            }
            // The atlas channels for a round stand-in puff (before the atlas is uploaded).
            float4 VfxPuffStandIn(float2 c)
            {
                float r = length(c);
                float d = saturate((0.92 - r) / 0.62);
                d = d * d * (3.0 - 2.0 * d);
                float bright = saturate(0.5 + 0.38 * c.y);
                return float4(d, d > 0.0 ? bright : 0.0, saturate(0.12 + 0.85 * d), 4.0 * d * (1.0 - d));
            }
            // Unity's point-light fall-off for a blast flash (NativeBlockLooks.FlashFalloff): 0 at its range.
            float VfxFlashFalloff(float3 world, float4 lightPos)
            {
                float3 d = lightPos.xyz - world;
                float q = length(d) / max(lightPos.w, 0.0001);
                q = q * q;
                return saturate(5.0 * (1.0 - q)) / (1.0 + 25.0 * q);
            }
            // Lit smoke, premultiplied. tex: atlas RGBA; ambTop/ambBottom: the ambient from above and below (the baked light
            // picks between them); sun: the main light, weighted by its height; scatter: forward scatter toward the light (rims
            // glow when the puff is backlit); flash: the blast flashes at the puff.
            float4 VfxSmoke(float4 tex, float u, float opacity, float3 albedo, float3 ambTop, float3 ambBottom, float3 sun, float scatter, float3 flash)
            {
                float a = VfxDissolve(tex.r, tex.b, u) * opacity * VfxBirth(u);
                float3 ambient = lerp(ambBottom, ambTop, tex.g);
                float3 direct = sun * (0.15 + 0.85 * tex.g);
                float3 rim = sun * (scatter * (0.35 + 1.65 * tex.a));
                float3 glow = flash * (0.6 + 0.4 * tex.g + 0.6 * tex.a);
                float3 col = albedo * (ambient + direct + glow + rim);
                return float4(col * a, a);
            }
            // A fireball puff: emissive in the particle's own hue (darker at its thin edges, brighter where it is dense and young,
            // cooling over its life), nearly opaque, so stacked puffs converge on about 1.3 x the hue rather than clipping to
            // white (review 2026-10-11; the Core glows drawn over them are the white-hot centre); from mid-life it hands off into
            // lit soot.
            float4 VfxFireball(float4 tex, float u, float opacity, float3 tint, float3 sootLight)
            {
                float cover = VfxDissolve(tex.r, tex.b, u * 0.9) * opacity * VfxBirth(u);
                float heat = saturate(tex.r * 1.2 - u * 1.1 + 0.05);
                float3 fire = lerp(tint * 0.55, tint, smoothstep(0.0, 0.5, heat));
                float glow = (0.6 + 0.5 * heat) * (0.65 + 0.35 * tex.g) * saturate(1.3 - u * 1.25);
                float soot = saturate((u - 0.3) * 1.8);
                float alpha = cover * (0.85 + 0.1 * soot);
                float3 sootColor = lerp(float3(0.05, 0.045, 0.04), tint * 0.16, 0.3);
                float3 col = fire * (glow * cover * (1.0 - soot * 0.85)) + sootColor * sootLight * (cover * soot);
                return float4(col, alpha);
            }
            // Soft particles: 0 where the quad touches the scene's depth, 1 a fade length in front of it (eye depths in m).
            float VfxSoftFade(float sceneDepth, float depth, float fadeLength)
            {
                return saturate((sceneDepth - depth) / max(fadeLength, 0.01));
            }
            // ---- VFX SHARED END ----
            struct appdata
            {
                float3 vertex:POSITION;
                float4 color:COLOR;
                float4 shape:TEXCOORD0;
                float4 life:TEXCOORD1;
                float4 axis:TEXCOORD2;
            };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float4 color:COLOR;
                float4 uv:TEXCOORD0;    // corner (streak: along, across) and the atlas uv
                float4 data:TEXCOORD1;  // u, mode, streak half length (half widths; other modes: the soft fade length, m), height above the floor (in fade lengths)
                float4 amb0:TEXCOORD2;  // ambient from above, forward scatter
                float4 amb1:TEXCOORD3;  // ambient from below, near-camera fade
                float4 sun:TEXCOORD4;   // main light (height weighted), streak taper
                float4 flash:TEXCOORD5; // blast flashes at the puff, w: a smoke puff's own-colour glow
                UNITY_FOG_COORDS(6)
                float4 screen:TEXCOORD7;// screen position (xy, w) for the depth texture, z: the fragment's eye depth
            };
            v2f vert(appdata v)
            {
                v2f o;
                float mode=v.life.z,size=max(v.shape.z,0.0001),hw=size*0.5;
                // World-space camera axes: the rows of the view matrix.
                float3 right=UNITY_MATRIX_V[0].xyz,up=UNITY_MATRIX_V[1].xyz,back=UNITY_MATRIX_V[2].xyz;
                float2 offset;float3 coords=float3(v.shape.xy,0.0);
                if(mode>3.5&&mode<4.5)
                {
                    offset=VfxStreakOffset(float2(dot(v.axis.xyz,right),dot(v.axis.xyz,up)),v.shape.xy,hw,coords);
                }
                else
                {
                    float2 local=float2(v.shape.x*hw,v.shape.y*hw*max(v.axis.w,0.05));
                    float s=sin(v.shape.w),c=cos(v.shape.w);
                    offset=float2(local.x*c-local.y*s,local.x*s+local.y*c);
                }
                float3 world=v.vertex+right*offset.x+up*offset.y;
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1.0));
                o.color=v.color;
                // Atlas tile by seed (16 puffs, 4 x 4), mirrored by the seed's parity so the baked light stays on top.
                float tile=v.life.y-16.0*floor(v.life.y/16.0),col=tile-4.0*floor(tile/4.0),row=floor(tile/4.0);
                float mirror=frac(v.life.y*0.5)>0.25?-1.0:1.0;
                o.uv=float4(coords.xy,(float2(col,row)+float2(v.shape.x*mirror,v.shape.y)*0.46+0.5)*0.25);
                float depth=dot(v.vertex-_WorldSpaceCameraPos,-back);
                float nearFade=saturate((depth-0.2)/max(0.35,size*0.6));
                bool puff=mode>4.5,streak=mode>3.5&&mode<4.5;
                // Soft fade length: a third of the quad (at least 8 cm, at most .8 m); a streak's thin body uses 8 cm.
                o.data=float4(v.life.x,mode,streak?coords.z:clamp(size*0.3,0.08,0.8),puff?(world.y-v.life.w)/max(0.05,size*0.3):1.0);
                o.screen=ComputeScreenPos(o.pos);o.screen.z=dot(world-_WorldSpaceCameraPos,-back);
                o.amb0=0;o.amb1=float4(0,0,0,nearFade);o.sun=float4(0,0,0,v.axis.w);o.flash=0;
                if(puff)
                {
                    float3 toLight=_WorldSpaceLightPos0.xyz;
                    o.amb0=float4(ShadeSH9(float4(0,1,0,1)),pow(saturate(dot(normalize(v.vertex-_WorldSpaceCameraPos),toLight)),6.0));
                    o.amb1.rgb=ShadeSH9(float4(0,-1,0,1));
                    o.sun.rgb=_LightColor0.rgb*saturate(0.25+0.75*toLight.y);
                    float3 flash=0;
                    for(int k=0;k<3;k++)flash+=_ArenaFlashColor[k].rgb*VfxFlashFalloff(v.vertex,_ArenaFlashPos[k]);
                    o.flash=float4(flash*_VfxFlashGain,v.axis.x);
                }
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float mode=i.data.y,u=i.data.x;
                // Sampled outside the branches (no gradients in divergent flow); the stand-in until the atlas is uploaded.
                float4 tex=lerp(VfxPuffStandIn(i.uv.xy),tex2D(_VfxAtlas,i.uv.zw),_VfxAtlasReady);
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float4 c;
                if(mode<3.5)c=i.color*VfxGlow(i.uv.xy,mode);
                else if(mode<4.5)c=i.color*VfxStreak(float3(i.uv.xy,i.data.z),i.sun.w);
                else
                {
                    float3 ambient=lerp(i.amb1.rgb,i.amb0.rgb,tex.g);
                    if(mode<5.5)
                    {
                        c=VfxSmoke(tex,u,i.color.a,i.color.rgb,i.amb0.rgb,i.amb1.rgb,i.sun.rgb,i.amb0.a,i.flash.rgb);
                        // A saturated cloud (toxic gas, acid fumes) glows a little in its own colour, fading with its life.
                        c.rgb+=i.color.rgb*(i.flash.w*c.a*(1.0-u));
                    }
                    else c=VfxFireball(tex,u,i.color.a,i.color.rgb,ambient+i.sun.rgb*(0.2+0.8*tex.g)+i.flash.rgb);
                    c*=saturate(i.data.w);
                }
                // A select, not a blend: whatever an unrendered depth texture holds never reaches the colour.
                c*=i.amb1.a*(_VfxSoft>0.5?VfxSoftFade(scene,i.screen.z,mode>3.5&&mode<4.5?0.08:i.data.z):1.0);
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
