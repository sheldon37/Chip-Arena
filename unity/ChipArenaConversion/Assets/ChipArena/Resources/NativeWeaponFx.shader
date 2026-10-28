// Effects overhaul, weapons milestone (owner 2026-10-11): gun muzzle stars, bullet tracers, hard-surface impact shards and
// sparks, energy projectile trails, melee slash crescents and the spell charge orb, all procedural (no texture), drawn by
// Runtime/NativeWeaponFxPresentation.cs as one dynamic mesh per camera: the match camera (layer 0) and, in first person,
// the view-model camera (layer 10), so a first-person muzzle star sits on the view weapon. Premultiplied (Blend One
// OneMinusSrcAlpha): the glows are additive (alpha 0), the slash underlays and smoke wisps cover (alpha > 0); the drawer
// queues the covering quads first. Queue Transparent+6: after the shared effects billboards (Transparent) and the shock
// shells (+4), before the combat glow halos (+10). Built-in forward base, SM3.5, Linear colour, scene fog (premultiplied:
// additive light fogs toward black). Colours arrive linear and may exceed 1 for the match camera's bloom. Soft particles:
// on the match camera, where the shared effects renderer has asked for its depth texture (normal tiers; _WeaponFxSoft per
// draw), every quad fades where it meets the scene (a third of a sprite's size, 5 cm for cards and crescents), so an
// impact glow on a wall or a crescent dipping into the floor shows no hard cut; the view-model camera and the low tier
// draw without it.
//   POSITION   oriented modes: the corner (world); billboards and axis cards: the centre (an axis card: its segment midpoint)
//   COLOR      linear rgb (premultiplied, HDR), a: coverage (0: additive)
//   TEXCOORD0  xy: the shape coordinates (oriented modes) or the corner (-1/1) of a camera-facing quad; z: mode; w: per mode
//              (crescent: 1 for its underlay; glow: profile; streak: taper; flame and wisp: seed)
//   TEXCOORD1  billboards: corner x, corner y, size (m), roll; axis cards: the world segment tail to head (xyz) and width (w);
//              oriented modes: x the crescent's edge gain
// Modes: 0 petal (a muzzle star's flame petal, x pivot to tip, y across), 1 crescent (x tail to head along the arc, y inner
// to outer), 2 ribbon (x tail to head, y across), 3 glow sprite (profiles 0 soft, 1 hot core, 2 ring, 3 sparkle), 4 smoke
// wisp, 5 streak (a capsule with a hot core), 6 flame card (the muzzle's side flame along the barrel), 7 shard (a curved
// triangle pointing at its head); and, from a gun family's own material only (NativeWeaponFxDraw.SheetFlame / SheetBurst, the
// owner's muzzle-flash pack through the pack bank), 8 the flipbook flame card along the barrel (w: the frame, negative
// mirrored) and 9 the burst sprite (a billboard).
Shader "ChipArena/NativeWeaponFx"
{
    Properties { _WeaponFxSoft("Soft particles (depth texture)",Float)=0 _SlashBrush("Slash brush (alpha: its streaks)",2D)="white"{} _SlashBrushRect("Slash brush: its streaks' rectangle (uv min, size)",Vector)=(0,0,1,1) _SlashBrushOn("Slash brush on",Float)=0 _FlashSheet("Pack flash: the flame flipbook",2D)="black"{} _FlashGrid("Pack flash: columns, rows",Vector)=(1,1,0,0) _FlashBurst("Pack flash: the burst",2D)="black"{} }
    SubShader
    {
        Tags { "Queue"="Transparent+6" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
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
            float _WeaponFxSoft;
            // The slash crescents' brush (NativeWeaponFxDraw.SlashBrush; the owner's effect pack through the local bake): an ink
            // stroke's streaks in the alpha of _SlashBrushRect. Off, the crescents are the procedural shape alone.
            sampler2D _SlashBrush;float4 _SlashBrushRect;float _SlashBrushOn;
            // A gun family's pack flash (NativeWeaponFxDraw.FlashSheets): the flame flipbook (_FlashGrid columns and rows, frame 0
            // at the top left) and the burst sprite. Only that family's material sets them and only its quads use modes 8 and 9.
            sampler2D _FlashSheet;float4 _FlashGrid;sampler2D _FlashBurst;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            // ---- WEAPON FX SHARED BEGIN (HLSL and GLSL: float literals only, no matrices, no texture reads) ----
            // Each shape returns its intensity (x) and its white-hot share (y).
            // A flame petal: x 0 at the muzzle to 1 at its tip, y -1..1 across. Widest near 40 %, a hot root.
            float2 FxPetal(float2 uv)
            {
                float x = saturate(uv.x);
                float hw = 1.6 * sqrt(x) * pow(1.0 - x, 0.8) + 0.04;
                float d = abs(uv.y) / hw;
                float body = smoothstep(1.0, 0.55, d) * saturate(1.0 - x * 0.25);
                float heat = exp(-x * 3.0) * exp(-uv.y * uv.y * 6.0);
                return float2(body * (0.75 + 0.5 * heat), heat);
            }
            // The side flame along the barrel: t 0 at the muzzle to 1 at its tip, s -1..1 across; a flicker by seed.
            float2 FxFlame(float t, float s, float seed)
            {
                float x = saturate(t);
                float hw = sin(3.14159 * pow(x, 0.55)) * (1.0 - 0.35 * x) * (1.0 + 0.14 * sin(x * 17.0 + seed * 6.283)) + 0.04;
                float body = smoothstep(1.0, 0.5, abs(s) / hw) * saturate(1.15 - x);
                float heat = exp(-x * 3.6) * exp(-s * s * 4.0);
                return float2(body * (0.7 + 0.6 * heat), heat);
            }
            // A slash crescent: x 0 at its tail to 1 at its head along the arc, y 0 inner to 1 outer. Thickest just behind its pointed head, a bright
            // outer edge (the blade's path), a soft inner edge; under: the dark underlay's coverage (no edge line).
            float2 FxCrescent(float2 uv, float under, float edge)
            {
                float x = saturate(uv.x);
                float tipU = saturate((x - 0.8) / 0.2);
                float th = (0.12 + 0.88 * pow(x, 0.7)) * (1.0 - tipU * tipU) + 0.02;
                float inner = 1.0 - th;
                float body = smoothstep(inner - 0.03, inner + 0.22 * th, uv.y) * smoothstep(1.0, 0.93, uv.y);
                float ends = smoothstep(0.0, 0.28, x) * smoothstep(1.0, 0.985, x);
                float depth = saturate((uv.y - inner) / max(th, 0.001));
                float rim = exp(-(1.0 - uv.y) * (1.0 - uv.y) * 160.0) * smoothstep(0.02, 0.4, x);
                float glow = body * (0.22 + 0.78 * depth * depth) * ends;
                float lit = glow + rim * edge * ends;
                return under > 0.5 ? float2(body * ends, 0.0) : float2(lit, saturate(rim * 1.2 + depth * depth * 0.25));
            }
            // A tapered ribbon: x 0 at its tail to 1 at its head, y -1..1 across.
            float2 FxRibbon(float2 uv)
            {
                float x = saturate(uv.x);
                float y2 = uv.y * uv.y;
                float body = exp(-y2 * 3.0) * saturate(1.0 - y2) * pow(x, 1.3);
                float heat = exp(-y2 * 16.0) * x * x;
                return float2(body * (0.7 + 0.6 * heat), heat);
            }
            // A glow sprite at corner c (unit disc). profile: 0 soft, 1 hot core (tight centre, wide halo), 2 ring, 3 sparkle.
            float2 FxGlow(float2 c, float profile)
            {
                float r2 = dot(c, c);
                float edge = saturate(1.0 - r2);
                float tight = exp(-r2 * 9.0);
                float wide = exp(-r2 * 2.8);
                float r = sqrt(r2);
                float soft = (tight * 0.6 + wide * 0.4) * edge;
                float core = (tight * 0.75 + wide * 0.35) * edge;
                float ring = exp(-(r - 0.62) * (r - 0.62) * 70.0) * edge;
                float star = (exp(-abs(c.x) * 14.0 - abs(c.y) * 2.4) + exp(-abs(c.y) * 14.0 - abs(c.x) * 2.4)) * 0.7 * edge + tight * 0.8;
                float a = profile < 0.5 ? soft : (profile < 1.5 ? core : (profile < 2.5 ? ring : star));
                float heat = profile < 0.5 ? tight * 0.5 : (profile < 1.5 ? tight : (profile < 2.5 ? 0.0 : tight));
                return float2(a, heat);
            }
            // A soft smoke wisp (premultiplied coverage), lumpy by seed.
            float FxWisp(float2 c, float seed)
            {
                float r2 = dot(c, c);
                float lump = 0.72 + 0.28 * sin(c.x * 4.7 + seed * 7.1) * sin(c.y * 3.9 - seed * 4.3);
                return pow(saturate(1.0 - r2), 1.8) * lump;
            }
            // A streak: coords x along and y across in half widths, z the segment's half length in half widths. A capsule with a hot
            // centre line, the tail (-x) dimmer by taper.
            float2 FxStreak(float3 coords, float taper)
            {
                float dx = max(abs(coords.x) - coords.z, 0.0);
                float r2 = dx * dx + coords.y * coords.y;
                float body = exp(-r2 * 2.8) * saturate(1.0 - r2);
                float along = saturate((coords.x + coords.z + 1.0) / (2.0 * coords.z + 2.0));
                float tail = 1.0 - taper * (1.0 - along);
                float centre = exp(-coords.y * coords.y * 10.0) * saturate(1.0 - dx);
                return float2(body * tail * (1.0 + centre), centre * tail);
            }
            // A curved shard: t 0 at its back to 1 at its point, s -1..1 across; concave sides and a concave back.
            float2 FxShard(float t, float s)
            {
                float hw = pow(saturate(1.0 - t), 1.7);
                float side = saturate((hw - abs(s)) * 6.0);
                float back = saturate((t - 0.35 * s * s) * 8.0);
                float a = side * back;
                return float2(a, a * exp(-s * s * 6.0) * 0.6);
            }
            // The camera-plane offset of an axis card's corner: axis the segment (tail to head) projected on the camera plane,
            // corner the quad corner (-1/1: x along, y across), hw the half width, cap whether it reaches a half width past each
            // end (a streak's round ends). coords: x along and y across in half widths, z the segment's half length.
            float2 FxAxisOffset(float2 axis, float2 corner, float hw, float cap, out float3 coords)
            {
                float len = length(axis);
                float2 dir = len > 0.00001 ? axis / len : float2(1.0, 0.0);
                float2 side = float2(-dir.y, dir.x);
                float hl = len * 0.5;
                float reach = hl + hw * cap;
                coords = float3(corner.x * reach / hw, corner.y, hl / hw);
                return dir * (corner.x * reach) + side * (corner.y * hw);
            }
            // Soft particles: 0 where the quad touches the scene's depth, 1 a fade length in front of it (eye depths in m).
            float FxSoftFade(float sceneDepth, float depth, float fadeLength)
            {
                return saturate((sceneDepth - depth) / max(fadeLength, 0.01));
            }
            // Premultiplied colour of a shape: whitened toward its brightest channel by the hot share, scaled by the intensity.
            float4 FxShade(float4 color, float2 shape)
            {
                float m = max(color.r, max(color.g, color.b));
                float3 rgb = color.rgb + (float3(m, m, m) - color.rgb) * saturate(shape.y);
                return float4(rgb * shape.x, color.a * shape.x);
            }
            // ---- WEAPON FX SHARED END ----
            // A crescent through the brush: its streaks run along the arc (the rectangle's width along x, tail to head; its height
            // across, inner to outer), their ragged ends at the tail and the head. The colour band takes the streaks and keeps a
            // faint body between them; the dark underlay reads them mirrored across, so ink and colour never line up stroke for
            // stroke.
            float2 SlashBrushed(float2 shape,float2 uv,float under)
            {
                float2 s=saturate(uv);if(under>0.5)s.y=1.0-s.y;
                float m=tex2D(_SlashBrush,_SlashBrushRect.xy+s*_SlashBrushRect.zw).a;
                return under>0.5?float2(shape.x*lerp(0.1,2.6,m),shape.y):float2(shape.x*lerp(0.25,1.45,m),shape.y*lerp(0.55,1.25,m));
            }
            // A photographic flash texel as the shapes' intensity (x: its brightness through its coverage) and white-hot share (y:
            // only its brightest, whitest parts), so FxShade keeps the flash colour (the kit's heat, the gain for bloom) on the
            // body and edges and whitens the core, and the texture gives the form.
            float2 FlashTexel(float4 t)
            {
                float m=max(t.r,max(t.g,t.b)),n=min(t.r,min(t.g,t.b)),l=dot(t.rgb,float3(0.3,0.55,0.15));
                return float2(t.a*m*(0.55+0.45*l),saturate((l-0.5)*2.4)*saturate((n/max(m,0.001)-0.35)*2.0));
            }
            // The flame card: t 0 at the crown to 1 at its tip (the cell's u), s -1..1 across (its v); frame's whole part the
            // frame, its fraction the blend toward the next (the last frame blends to nothing), its sign the mirror.
            float2 FlashFlame(float t,float s,float frame)
            {
                float cols=max(_FlashGrid.x,1.0),rows=max(_FlashGrid.y,1.0),f=abs(frame),f0=floor(f),k=f-f0;
                float2 cell=float2(clamp(t,0.004,0.996),clamp((frame<0.0?-s:s)*0.5+0.5,0.004,0.996));
                float2 a=float2((fmod(f0,cols)+cell.x)/cols,1.0-(floor(f0/cols)+1.0-cell.y)/rows);
                float f1=f0+1.0;float2 b=float2((fmod(f1,cols)+cell.x)/cols,1.0-(floor(f1/cols)+1.0-cell.y)/rows);
                float4 ta=tex2D(_FlashSheet,a),tb=f1<cols*rows?tex2D(_FlashSheet,b):float4(0,0,0,0);
                return FlashTexel(lerp(ta,tb,k));
            }
            // The burst sprite at corner c (-1..1).
            float2 FlashBurst(float2 c)
            {
                return FlashTexel(tex2D(_FlashBurst,saturate(c*0.5+0.5)));
            }
            struct appdata
            {
                float3 vertex:POSITION;
                float4 color:COLOR;
                float4 uv:TEXCOORD0;
                float4 extra:TEXCOORD1;
            };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float4 color:COLOR;
                float4 uv:TEXCOORD0;   // xy the shape coordinates, z mode, w the mode's parameter
                float4 data:TEXCOORD1; // x an axis card's half length in half widths, y the crescent edge gain, z the soft fade length
                UNITY_FOG_COORDS(2)
                float4 screen:TEXCOORD3; // screen position (xy, w) for the depth texture, z the fragment's eye depth
            };
            v2f vert(appdata v)
            {
                v2f o;
                float mode=v.uv.z;float3 world=v.vertex;float4 data=float4(0,0,0,0);float2 shapeUv=v.uv.xy;
                float3 right=UNITY_MATRIX_V[0].xyz,up=UNITY_MATRIX_V[1].xyz,back=UNITY_MATRIX_V[2].xyz;data.z=0.05;
                if(mode>2.5&&mode<4.5||mode>8.5)
                {
                    // Billboards: corner (extra.xy) times half the size, rolled.
                    float hw=max(v.extra.z,0.0001)*0.5;float s=sin(v.extra.w),c=cos(v.extra.w);float2 local=v.extra.xy*hw;
                    float2 offset=float2(local.x*c-local.y*s,local.x*s+local.y*c);
                    world+=right*offset.x+up*offset.y;shapeUv=v.extra.xy;data.z=clamp(v.extra.z*0.3,0.05,0.5);
                }
                else if(mode>4.5)
                {
                    // Axis cards: along the segment in the camera plane; a streak caps both ends.
                    float hw=max(v.extra.w,0.0001)*0.5;float3 coords;
                    float2 offset=FxAxisOffset(float2(dot(v.extra.xyz,right),dot(v.extra.xyz,up)),v.uv.xy,hw,mode<5.5?1.0:0.0,coords);
                    world+=right*offset.x+up*offset.y;shapeUv=coords.xy;data.x=coords.z;
                }
                else data.y=v.extra.x;
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1.0));
                o.color=v.color;o.uv=float4(shapeUv,mode,v.uv.w);o.data=data;
                o.screen=ComputeScreenPos(o.pos);o.screen.z=dot(world-_WorldSpaceCameraPos,-back);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float mode=i.uv.z,w=i.uv.w;float2 uv=i.uv.xy;float2 shape;
                if(mode<0.5)shape=FxPetal(uv);
                else if(mode<1.5)shape=FxCrescent(uv,w,i.data.y);
                else if(mode<2.5)shape=FxRibbon(uv);
                else if(mode<3.5)shape=FxGlow(uv,w);
                else if(mode<4.5)shape=float2(FxWisp(uv,w),0.0);
                else if(mode<5.5)shape=FxStreak(float3(uv,i.data.x),w);
                else
                {
                    // Flame and shard cards span their segment exactly: along 0..1 from the tail.
                    float t=(uv.x/max(i.data.x,0.0001))*0.5+0.5;
                    shape=mode<6.5?FxFlame(t,uv.y,w):FxShard(t,uv.y);
                }
                if(mode>7.5)shape=mode<8.5?FlashFlame((uv.x/max(i.data.x,0.0001))*0.5+0.5,uv.y,w):FlashBurst(uv);
                if(mode>0.5&&mode<1.5&&_SlashBrushOn>0.5)shape=SlashBrushed(shape,uv,w);
                float4 c=FxShade(i.color,shape);
                // A select, not a blend: whatever an unrendered depth texture holds never reaches the colour.
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                c*=_WeaponFxSoft>0.5?FxSoftFade(scene,i.screen.z,i.data.z):1.0;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(unity_FogColor.rgb*c.a,0));
                #if defined(UNITY_COLORSPACE_GAMMA)
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
