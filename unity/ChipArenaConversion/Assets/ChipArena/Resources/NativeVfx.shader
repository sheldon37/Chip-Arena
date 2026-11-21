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
//   TEXCOORD2  streak and sprite: world segment, tail to head (xyz) and taper (w); toon water: segment (xyz) and its shadow
//              colour as a packed sRGB hex (w); billboards: x a smoke puff's own-colour glow, y toon fire's ash colour as a
//              packed sRGB hex, w height/width
// Modes (NativeVfxDraw constants): 0 mote, 1 core, 2 ring (bubble), 3 snow (soft glow sprites); 4 streak (a capsule along
// the segment, oriented to the camera, a hot core, dimmer tail); 5 smoke (an atlas puff, lit by the ambient, the main light
// and the three blast flashes, eroding over u instead of shrinking; a saturated cloud adds a little glow in its colour);
// 6 fireball (an atlas puff glowing in the particle's own hue, densest and hottest young, handing off into soot; the
// white-hot centre is the Core glows', review 2026-10-11).
// Toon pass (owner 2026-10-11, after the Asset Store pack study; every shape is Core's procedural toon sheet,
// NativeVfxTextures.ToonSheet, signed distances drawn with a one-pixel edge): 7 toon smoke (hard lobes, a two-tone shadow and
// lobe outlines, mostly its own colour with a little scene light, eaten into blobs late in its life), 8 toon fire (white-hot,
// flame, orange, then ash), 9 toon water (pale lit tone, blue shadow tone, a white rim on its lit edge; oriented along its
// segment), 10 flash (a camera-facing toon sheet sprite; the round flash flipbook plays over its life) and 11 sprite (a toon
// sheet sprite along its segment: crescent licks, spikes, droplets). TEXCOORD1.w is a sprite's tile for 10 and 11.
// Smoke and fireball puffs fade where they dip below their floor; every quad fades near the camera and, soft particles,
// where it meets the scene's depth (_CameraDepthTexture, _VfxSoft 1; 0 on the low graphics tier, which asks for no depth
// texture). The smoke atlas (Core/NativeVfxTextures.SmokeAtlas: R density, G baked light, B erosion threshold, A edge) is
// built off the main thread; until it is uploaded (_VfxAtlasReady 0) the puffs use a round stand-in with the same channels.
// Pack pass (owner 2026-10-28, the licensed packs as in-place upgrades): _VfxToon may be the effect pack's cartoon masks in
// the same layout and channels (nothing here changes for it), and 12 is a pack sprite: a camera-facing tile of _VfxPack (the
// magic sheet, _VfxPackGrid columns x rows, row 0 at its top; TEXCOORD1.w the tile) at its roll, its shape the sheet's
// brightest channel and its white-hot share the sheet's own (times TEXCOORD2.w), tinted by its colour (VfxPackSprite).
// Without the sheet nothing draws in mode 12.
Shader "ChipArena/NativeVfx"
{
    Properties
    {
        _VfxAtlas("Smoke atlas (RGBA linear)",2D)="black"{}
        _VfxToon("Toon sheet (RGBA linear)",2D)="black"{}
        _VfxAtlasReady("Atlas uploaded",Float)=0
        _VfxFlashGain("Blast flash light on smoke",Float)=1
        _VfxSoft("Soft particles (depth texture)",Float)=0
        _VfxPack("Magic sheet (premultiplied colour, coverage)",2D)="black"{}
        _VfxPackGrid("Magic sheet grid (columns, rows)",Vector)=(8,4,0,0)
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
            sampler2D _VfxAtlas,_VfxToon,_VfxPack;float4 _VfxPackGrid;
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
                float2 dir = len > 0.00001 ? axis / len : float2(0.0, 1.0);
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
            // ---- toon pass ----
            // An sRGB hex packed in a float (exact below 2^24) to linear colour (Unity's GammaToLinearSpace fit).
            float3 VfxHexLinear(float hex)
            {
                float r = floor(hex / 65536.0);
                float g = floor((hex - r * 65536.0) / 256.0);
                float b = hex - r * 65536.0 - g * 256.0;
                float3 c = float3(r, g, b) / 255.0;
                return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
            }
            // Core/NativeVfxTextures.ToonCut: the erosion cut at life u (0 until ToonEatFrom, just past 1 at u = 1).
            float VfxToonCut(float u)
            {
                float x = saturate((u - 0.42) / 0.58);
                return x * x * (3.0 - 2.0 * x) * 1.02;
            }
            // The toon sheet channels of a round stand-in puff (before the sheet is uploaded): a disc lit from above.
            float4 VfxToonStandIn(float2 c)
            {
                float d = 0.7 - 0.92 * length(c);
                float lit = 0.62 - 0.92 * length(c + float2(-0.12, 0.2));
                return float4(0.5 + d * 2.0, 0.5 + lit * 2.0, 0.6, 0.5 + d * 2.0);
            }
            // A toon puff from its toon sheet texel tex (R silhouette, G lit, B erosion, A lobe edge; signed distances at .5) and
            // the texel's one-pixel widths aa (fwidth of tex): x its coverage (silhouette and erosion), y lit (1 lit, 0 in a shadow
            // crescent), z an outline band along the lobe edges and round the holes the erosion eats.
            float3 VfxToonMask(float4 tex, float4 aa, float cut)
            {
                float4 w = max(aa * 0.75, float4(0.004, 0.004, 0.004, 0.004));
                float inside = saturate((tex.r - 0.5) / w.r + 0.5);
                float eaten = saturate((tex.b - cut) / w.b + 0.5);
                float lit = saturate((tex.g - 0.5) / w.g + 0.5);
                float lobe = 1.0 - saturate((tex.a - 0.56) / w.a + 0.5);
                float hole = cut > 0.0001 ? 1.0 - saturate((tex.b - cut - 0.05) / w.b + 0.5) : 0.0;
                return float3(inside * eaten, lit, max(lobe, hole));
            }
            // The light on a toon puff: mostly its own colour (the toon references are unlit), some ambient and moon, the flashes.
            float3 VfxToonLight(float3 ambTop, float3 sun, float3 flash)
            {
                return 0.78 + 0.3 * (ambTop + sun) + 0.8 * flash;
            }
            // ---- the model's puffs (owner 2026-10-29: the blasts' smoke restyled for the model; the toon sheet keeps water and the
            // sprites) ----
            // A smoke or fire puff from the billowy atlas (R density, G baked light, B erosion threshold, A thin edge), as a mask:
            // x its coverage, thin and see-through at its billows' edges, dense in its core, eaten into wisps over its life; y its
            // light, a soft terminator between two tones; z its thin rims, which catch the light (no outline).
            float3 VfxSoftMask(float4 tex, float u)
            {
                float d = VfxDissolve(tex.r, tex.b, u);
                float cover = smoothstep(0.0, 0.8, d) * VfxBirth(u);
                float lit = smoothstep(0.32, 0.68, tex.g);
                return float3(cover, lit, tex.a * saturate(1.0 - d * 1.5));
            }
            // The model's smoke, premultiplied: the lit tone and a cooler shade across the soft terminator, its folds deepened by
            // the baked light, its thin rims lit by the moon and the flashes; a saturated cloud adds a little of its own glow.
            float4 VfxSoftSmoke(float3 mask, float4 tex, float opacity, float3 albedo, float3 ambTop, float3 ambBottom, float3 sun, float3 flash, float glow)
            {
                float3 shade = (0.42 + 0.3 * ambBottom + 0.35 * flash) * float3(0.84, 0.89, 1.05);
                float3 lit = 0.8 + 0.32 * (ambTop + sun) + 0.8 * flash;
                float3 col = albedo * lerp(shade, lit, mask.y) * (0.8 + 0.2 * tex.g);
                col += albedo * mask.z * (0.3 + 0.5 * dot(sun + flash, float3(0.333, 0.333, 0.333))) + albedo * glow;
                float a = mask.x * saturate(opacity);
                return float4(col * a, a);
            }
            // Toon fire, premultiplied: white-hot, its flame, orange, then its ash (lit like smoke); the shadow tone deeper and redder;
            // HDR while hot, for the bloom.
            float4 VfxToonFire(float3 mask, float u, float opacity, float3 flame, float3 ash, float3 light)
            {
                float3 orange = flame * float3(1.0, 0.42, 0.16);
                float3 hot = lerp(flame * 1.25 + float3(0.35, 0.3, 0.2), flame, saturate(u / 0.12));
                float3 fire = lerp(hot, orange, saturate((u - 0.12) / 0.26));
                float cool = saturate((u - 0.34) / 0.16);
                float emit = 1.0 + 1.4 * (1.0 - saturate(u / 0.3));
                float3 lit = lerp(fire * emit, ash * light, cool);
                float3 shadow = lerp(fire * float3(0.92, 0.5, 0.28) * emit, ash * light * float3(0.62, 0.66, 0.8), cool);
                float3 col = lerp(shadow, lit, mask.y) * (1.0 - 0.3 * mask.z);
                float a = mask.x * saturate(opacity);
                return float4(col * a, a);
            }
            // Toon water, premultiplied: its lit tone, its shadow tone in the crescents and outlines, a white rim on its lit edge.
            float4 VfxToonWater(float3 mask, float4 tex, float4 aa, float opacity, float3 lit, float3 shade, float3 light)
            {
                float w = max(aa.r * 0.75, 0.004);
                float rim = (1.0 - saturate((tex.r - 0.6) / w + 0.5)) * mask.y;
                float3 col = lerp(shade * (0.7 + 0.3 * light), lit * light, mask.y);
                col = lerp(col, shade * 0.55, mask.z * 0.6);
                col = lerp(col, float3(1.15, 1.2, 1.25), rim * 0.85);
                float a = mask.x * saturate(opacity);
                return float4(col * a, a);
            }
            // A toon sheet sprite (the flash flipbook, crescents, spikes, droplets), premultiplied: its colour with a hotter core;
            // color.a 0 adds it with a soft halo, above 0 blends it at that coverage.
            float4 VfxToonSprite(float4 tex, float4 aa, float4 color)
            {
                float4 w = max(aa * 0.75, float4(0.004, 0.004, 0.004, 0.004));
                float shape = saturate((tex.r - 0.5) / w.r + 0.5);
                float core = saturate((tex.g - 0.5) / w.g + 0.5);
                float peak = max(max(color.r, color.g), color.b);
                float3 col = lerp(color.rgb, color.rgb * 1.35 + float3(0.25, 0.25, 0.25) * peak, core);
                float halo = tex.b * (1.0 - saturate(color.a * 4.0));
                return float4(col * shape + color.rgb * (halo * 0.22), shape * color.a);
            }
            // A pack sprite (the magic sheet: premultiplied colour, coverage), premultiplied: its brightest channel is the shape, its
            // whiteness (least over brightest channel) where the pack's own art runs white-hot (times hot: 1 keeps the art's white
            // cores, 0 tints a white mask all through), the rest takes color's hue (HDR); color.a 0 adds it, above 0 blends it at
            // that coverage.
            float4 VfxPackSprite(float4 tex, float4 color, float hot)
            {
                float shape = max(max(tex.r, tex.g), tex.b);
                float white = shape > 0.0001 ? min(min(tex.r, tex.g), tex.b) / shape : 0.0;
                float peak = max(max(color.r, color.g), color.b);
                float3 col = lerp(color.rgb, float3(peak, peak, peak), white * white * hot);
                return float4(col * shape, tex.a * color.a);
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
                float4 toon:TEXCOORD8;  // the toon sheet uv (xy)
                float4 alt:TEXCOORD9;   // toon fire's ash, toon water's shadow colour (rgb, linear)
            };
            v2f vert(appdata v)
            {
                v2f o;
                float mode=v.life.z,size=max(v.shape.z,0.0001),hw=size*0.5;
                // World-space camera axes: the rows of the view matrix.
                float3 right=UNITY_MATRIX_V[0].xyz,up=UNITY_MATRIX_V[1].xyz,back=UNITY_MATRIX_V[2].xyz;
                float2 offset;float3 coords=float3(v.shape.xy,0.0);
                // Streaks, toon water and sprites lie along their segment; the rest face the camera at their roll.
                bool oriented=mode>3.5&&mode<4.5||mode>8.5&&mode<9.5||mode>10.5;
                float2 screenAxis=float2(dot(v.axis.xyz,right),dot(v.axis.xyz,up));
                if(oriented)
                {
                    offset=VfxStreakOffset(screenAxis,v.shape.xy,hw,coords);
                }
                else
                {
                    float2 local=float2(v.shape.x*hw,v.shape.y*hw*max(v.axis.w,0.05));
                    float s=sin(v.shape.w),c=cos(v.shape.w);
                    offset=float2(local.x*c-local.y*s,local.x*s+local.y*c);
                }
                // A pack sprite faces the camera at its roll, as the glows do (the oriented branch above takes every mode past 10.5).
                if(mode>11.5){float2 local=v.shape.xy*hw;float s=sin(v.shape.w),c=cos(v.shape.w);offset=float2(local.x*c-local.y*s,local.x*s+local.y*c);coords=float3(v.shape.xy,0.0);}
                float3 world=v.vertex+right*offset.x+up*offset.y;
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1.0));
                o.color=v.color;
                // Atlas tile by seed (16 puffs, 4 x 4), mirrored by the seed's parity so the baked light stays on top.
                float tile=v.life.y-16.0*floor(v.life.y/16.0),col=tile-4.0*floor(tile/4.0),row=floor(tile/4.0);
                float mirror=frac(v.life.y*0.5)>0.25?-1.0:1.0;
                o.uv=float4(coords.xy,(float2(col,row)+float2(v.shape.x*mirror,v.shape.y)*0.46+0.5)*0.25);
                // The toon sheet (8 x 4 tiles): a puff's tile by seed (16 puffs, mirrored by parity); a flash's from TEXCOORD1.w
                // (the round flipbook from tile 16 advances over its life, later tiles stay put); a sprite's from TEXCOORD1.w with
                // x along its segment; toon water's puff with its top along the segment (flipped so it never hangs upside down).
                float sheet=tile,along=coords.x/(coords.z+1.0);float2 tc=float2(v.shape.x*mirror,v.shape.y);
                if(mode>10.5){sheet=v.life.w;tc=float2(along,coords.y);}
                else if(mode>9.5){sheet=v.life.w<23.5?v.life.w+min(floor(v.life.x*8.0),7.0):v.life.w;tc=v.shape.xy;}
                else if(mode>8.5){float s=screenAxis.y<0.0?-1.0:1.0;tc=float2(-coords.y*s,along*s);}
                float sheetCol=sheet-8.0*floor(sheet/8.0),sheetRow=floor(sheet/8.0);
                o.toon=float4((sheetCol+tc.x*0.46+0.5)*0.125,(sheetRow+tc.y*0.46+0.5)*0.25,0,0);
                // The magic sheet's tile (zw): column t mod columns, rows from the top of the sheet (its image's order).
                if(mode>11.5){float pt=v.life.w,pc=pt-_VfxPackGrid.x*floor(pt/_VfxPackGrid.x),pr=_VfxPackGrid.y-1.0-floor(pt/_VfxPackGrid.x);o.toon.zw=(float2(pc,pr)+v.shape.xy*0.48+0.5)/_VfxPackGrid.xy;}
                o.alt=float4(VfxHexLinear(mode>8.5?v.axis.w:v.axis.y),0);
                float depth=dot(v.vertex-_WorldSpaceCameraPos,-back);
                float nearFade=saturate((depth-0.2)/max(0.35,size*0.6));
                bool puff=mode>4.5&&mode<9.5,streak=mode>3.5&&mode<4.5,toonPuff=mode>6.5&&mode<9.5;
                // Soft fade length: a third of the quad (at least 8 cm, at most .8 m); a streak's thin body uses 8 cm. A toon puff
                // meets the floor and the scene nearly crisply (an eighth of its size, 5-25 cm): a long fade at its foot reads as a
                // glowing ring on the floor.
                float fadeLength=toonPuff?clamp(size*0.12,0.05,0.25):clamp(size*0.3,0.08,0.8);
                o.data=float4(v.life.x,mode,streak?coords.z:fadeLength,puff?(world.y-v.life.w)/fadeLength:1.0);
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
                float4 toon=lerp(VfxToonStandIn(i.uv.xy),tex2D(_VfxToon,i.toon.xy),_VfxAtlasReady);float4 aa=fwidth(toon);
                float4 pack=tex2D(_VfxPack,i.toon.zw);
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float4 c;
                if(mode<3.5)c=i.color*VfxGlow(i.uv.xy,mode);
                else if(mode<4.5)c=i.color*VfxStreak(float3(i.uv.xy,i.data.z),i.sun.w);
                else if(mode<9.5)
                {
                    float3 ambient=lerp(i.amb1.rgb,i.amb0.rgb,tex.g);
                    if(mode<5.5)
                    {
                        c=VfxSmoke(tex,u,i.color.a,i.color.rgb,i.amb0.rgb,i.amb1.rgb,i.sun.rgb,i.amb0.a,i.flash.rgb);
                        // A saturated cloud (toxic gas, acid fumes) glows a little in its own colour, fading with its life.
                        c.rgb+=i.color.rgb*(i.flash.w*c.a*(1.0-u));
                    }
                    else if(mode<6.5)c=VfxFireball(tex,u,i.color.a,i.color.rgb,ambient+i.sun.rgb*(0.2+0.8*tex.g)+i.flash.rgb);
                    else
                    {
                        float3 mask=VfxToonMask(toon,aa,VfxToonCut(u)),light=VfxToonLight(i.amb0.rgb,i.sun.rgb,i.flash.rgb),soft=VfxSoftMask(tex,u);
                        if(mode<7.5)c=VfxSoftSmoke(soft,tex,i.color.a,i.color.rgb,i.amb0.rgb,i.amb1.rgb,i.sun.rgb,i.flash.rgb,i.flash.w*(1.0-u));
                        else if(mode<8.5)c=VfxToonFire(float3(soft.xy,0.0),u,i.color.a,i.color.rgb,i.alt.rgb,light);
                        else c=VfxToonWater(mask,toon,aa,i.color.a,i.color.rgb,i.alt.rgb,light);
                    }
                    c*=saturate(i.data.w);
                }
                else if(mode>11.5)c=VfxPackSprite(pack,i.color,i.sun.w);
                else c=VfxToonSprite(toon,aa,i.color);
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
