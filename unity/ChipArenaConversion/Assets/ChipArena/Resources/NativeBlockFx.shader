// The block effect (owner request 2026-10-11 "add a new block effect"; docs/MELEE-ABILITIES.md "The new block effect"): the
// energy guard in the weapon's colour while a block window is open, the clash (guard flash and cracks, spark spray, star and
// needles, upright shock ring) and the deflect streak and ping. One dynamic mesh per camera, built on the CPU each frame by
// Core/NativeBlockFx.cs (NativeBlockMesh) and drawn by Runtime/NativeBlockFxPresentation.cs: the match camera on layer 0, the
// first-person view camera on layer 10. Premultiplied (Blend One OneMinusSrcAlpha): light is additive (alpha 0) except a
// faint coverage behind the guard's fill, so the ward reads over bright scenery. Colours arrive linear with the brightest
// channel at 1; rims, sparks and flashes exceed 1 for the match camera's bloom (Runtime/NativeBloom.cs). Built-in forward,
// SM3.5, Linear colour, scene fog toward black (premultiplied light). Queue Transparent+6: after the effects billboards and
// shock shells, before the combat glow halos.
//   POSITION   world position (quads are built camera-facing on the CPU where they face the camera)
//   COLOR      linear colour (brightest channel 1), a: strength (first-person subtlety, Reduced effects)
//   TEXCOORD0  u, v, mode, seed
//   TEXCOORD1  alpha, flare, crack, time   (per mode: see NativeBlockVertex)
//   TEXCOORD2  contact u, contact v, metric u, metric v (m)
// Modes: 0 arc guard (scanlines), 1 hex guard (cells), 2 shock ring, 3 streak, 4 glow / star.
// _BlockFxLow 1 (the low graphics tier) drops the guard's sparkle noise and cell flicker.
Shader "ChipArena/NativeBlockFx"
{
    Properties
    {
        _BlockFxLow("Low graphics tier",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+6" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
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
            float _BlockFxLow;
            // ---- BLOCK FX SHARED BEGIN (HLSL and GLSL: float literals only, no ints, bools, loops, matrices or derivatives) ----
            float BlockFxHash(float2 p)
            {
                return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);
            }
            // Hex tiling of p (unit cells): x the distance to the cell's edge (0 on it, .5 at its centre), y a per-cell random.
            float2 BlockFxHex(float2 p)
            {
                float2 r=float2(1.0,1.7320508);
                float2 h=r*0.5;
                float2 a=p-r*floor(p/r)-h;
                float2 b=(p-h)-r*floor((p-h)/r)-h;
                float2 g=dot(a,a)<dot(b,b)?a:b;
                float2 q=abs(g);
                float d=max(dot(q,float2(0.5,0.8660254)),q.x);
                return float2(0.5-d,BlockFxHash(p-g));
            }
            // One jagged crack ray from the contact (d: offset from it in guard uv), at angle th, running out to rr.
            float BlockFxCrackRay(float2 d,float th,float rr,float seed,float aa)
            {
                float2 dir=float2(cos(th),sin(th));
                float along=dot(d,dir);
                float off=d.x*dir.y-d.y*dir.x;
                float jag=(0.05*sin(along*29.0+seed*7.0)+0.025*sin(along*71.0+seed*3.0))*along;
                float w=0.012+aa*1.5;
                float ray=1.0-smoothstep(w*0.5,w,abs(off-jag));
                return ray*step(0.0,along)*(1.0-smoothstep(rr*0.65,rr,along));
            }
            // The clash cracks: five rays from the contact, reaching .25 at once and their full length after 50 ms (crack: the
            // fade, 1 fresh -> 0; NativeBlockFx.CrackReach), fading with it.
            float BlockFxCracks(float2 uv,float2 contact,float crack,float seed,float aa)
            {
                float2 d=(uv-contact)*float2(1.0,0.75);
                float rr=(0.25+0.75*saturate((1.0-crack)*3.6))*0.9;
                float s=frac(seed*0.61803)*6.2831853;
                float c=BlockFxCrackRay(d,s,rr,seed,aa);
                c=max(c,BlockFxCrackRay(d,s+1.37,rr*0.8,seed+1.0,aa));
                c=max(c,BlockFxCrackRay(d,s+2.61,rr*0.95,seed+2.0,aa));
                c=max(c,BlockFxCrackRay(d,s+3.9,rr*0.7,seed+3.0,aa));
                c=max(c,BlockFxCrackRay(d,s+5.05,rr*0.85,seed+4.0,aa));
                return c*crack;
            }
            // The guard. uv: along/across (-1..1); metric (m) drives the scanlines (arc) or the hex cells (hex). aa.x: the uv's
            // screen-space width, aa.y: the metric's. Returns premultiplied light, alpha a faint coverage behind the fill.
            float4 BlockFxGuard(float2 uv,float3 color,float strength,float alpha,float flare,float crack,float clock,float2 contact,float2 metric,float seed,float hex,float low,float2 aa)
            {
                float e=hex>0.5?length(uv):abs(uv.y);
                float inside=1.0-smoothstep(1.0-aa.x*1.5,1.0,e);
                float rimW=0.12+aa.x*2.0;
                float rim=saturate(1.0-(1.0-e)/rimW);
                rim=rim*rim;
                float core=saturate(1.0-(1.0-e)/(rimW*0.35));
                // A bright band sweeping along the guard, and a soft inner glow toward the rim.
                float sweep=frac(metric.x*0.8-clock*1.5);
                sweep=exp(-(sweep-0.5)*(sweep-0.5)*60.0);
                float glow=0.10+0.22*e*e;
                float pattern;
                if(hex>0.5)
                {
                    float2 cell=BlockFxHex(metric*7.5);
                    float cellW=0.035+aa.y*7.5*1.5;
                    float edge=1.0-smoothstep(cellW*0.5,cellW,cell.x);
                    float pulse=0.35+0.65*step(0.6,frac(cell.y*13.0+clock*0.9))*(1.0-low);
                    pattern=edge*(0.55+0.25*pulse)+0.12*pulse*(1.0-edge);
                }
                else
                {
                    float lines=abs(frac(abs(metric.y)*20.0-clock*1.8)-0.5)*2.0;
                    float lineW=0.22+aa.y*20.0*2.0;
                    pattern=(1.0-smoothstep(lineW*0.5,lineW,lines))*0.3*(1.0-0.5*e);
                    pattern+=smoothstep(0.82,1.0,abs(uv.x))*0.9;
                }
                float sparkle=step(0.993,BlockFxHash(floor(metric*30.0)+floor(clock*20.0)))*(1.0-low)*0.8;
                float shimmer=0.88+0.12*sin(clock*29.0+metric.x*6.0);
                float3 c=color*((glow+pattern+0.3*sweep+sparkle)*shimmer+rim*2.6+core*1.6);
                // The clash: a white flash, hottest at the rim and round the contact (a wide ward never blooms into one white
                // blob), and white-hot cracks running out from the contact.
                float2 dc=(uv-contact)*float2(1.0,0.75);
                float hit=exp(-dot(dc,dc)*8.0);
                float fl=flare*(0.2+0.6*max(rim,hit));
                float k=BlockFxCracks(uv,contact,crack,seed,aa.x);
                c=lerp(c,float3(1.0,1.0,1.0)*(1.2+1.8*rim+1.8*hit),fl)+color*fl;
                c+=float3(3.5,3.5,3.5)*k+color*(k*1.5);
                float a=alpha*inside*strength;
                return float4(c*a,a*0.14*(0.4+glow));
            }
            // The upright shock ring: radius and width in quad halves, u its normalized age; white-hot young, then its colour.
            float4 BlockFxRing(float2 uv,float3 color,float strength,float alpha,float radius,float width,float u,float aa)
            {
                float d=length(uv);
                float x=(d-radius)/max(width,0.0001);
                float band=exp(-x*x*2.0)*(x<0.0?1.0:exp(-x*x*1.5));
                float edge=exp(-x*x*12.0);
                float fill=(1.0-smoothstep(0.0,radius,d))*0.12*(1.0-u);
                float3 hot=lerp(color,float3(1.0,1.0,1.0),0.65*(1.0-u));
                float3 c=hot*(band*2.0+edge*2.4)+color*fill;
                float a=alpha*strength*(1.0-smoothstep(1.0-aa*2.0,1.0,d));
                return float4(c*a,0.0);
            }
            // A streak: uv.x tail (0) to head (1), uv.y across; round ends half a width long (aspect: length over width).
            float4 BlockFxStreak(float2 uv,float3 color,float strength,float alpha,float heat,float taper,float aspect)
            {
                float across=abs(uv.y);
                float body=exp(-across*across*3.5);
                float core=exp(-across*across*16.0);
                float cap=0.5/max(aspect,1.0);
                float ends=smoothstep(0.0,cap,uv.x)*smoothstep(0.0,cap,1.0-uv.x);
                float bright=lerp(1.0-taper,1.0,saturate(uv.x));
                float3 hot=lerp(color,float3(1.0,1.0,1.0),heat*0.85);
                float3 c=hot*(body*1.4+core*2.8)*bright*ends;
                return float4(c*(alpha*strength),0.0);
            }
            // A glow: a soft disc, plus an eight-point star (star 0..1) turned by phase; white-hot with heat.
            float4 BlockFxGlow(float2 uv,float3 color,float strength,float alpha,float heat,float star,float phase)
            {
                float r2=dot(uv,uv);
                float disc=exp(-r2*6.0)*(1.0-smoothstep(0.6,1.0,r2));
                float hot=exp(-r2*40.0);
                float cs=cos(phase);
                float sn=sin(phase);
                float2 p=float2(uv.x*cs-uv.y*sn,uv.x*sn+uv.y*cs);
                float2 q=float2(p.x-p.y,p.x+p.y)*0.70710678;
                float4 m=abs(float4(p,q));
                float s4=exp(-m.y*30.0)*saturate(1.0-m.x)+exp(-m.x*30.0)*saturate(1.0-m.y);
                float s8=exp(-m.w*36.0)*saturate(1.0-m.z*1.5)+exp(-m.z*36.0)*saturate(1.0-m.w*1.5);
                float3 white=lerp(color,float3(1.0,1.0,1.0),heat*0.8);
                float3 c=white*(disc*1.1+hot*3.0+(s4*2.6+s8*1.3)*star);
                return float4(c*(alpha*strength),0.0);
            }
            // Per-fragment entry: the mode in uv.z picks the shape (NativeBlockFx.Mode*).
            float4 BlockFxShade(float4 color,float4 uv,float4 data,float4 extra,float2 aa,float low)
            {
                float mode=uv.z;
                if(mode<1.5)return BlockFxGuard(uv.xy,color.rgb,color.a,data.x,data.y,data.z,data.w,extra.xy,extra.zw,uv.w,mode>0.5?1.0:0.0,low,aa);
                if(mode<2.5)return BlockFxRing(uv.xy,color.rgb,color.a,data.x,data.y,data.z,data.w,aa.x);
                if(mode<3.5)return BlockFxStreak(uv.xy,color.rgb,color.a,data.x,data.y,data.z,extra.w);
                return BlockFxGlow(uv.xy,color.rgb,color.a,data.x,data.y,data.z,data.w);
            }
            // ---- BLOCK FX SHARED END ----
            struct appdata
            {
                float3 vertex:POSITION;
                float4 color:COLOR;
                float4 uv:TEXCOORD0;
                float4 data:TEXCOORD1;
                float4 extra:TEXCOORD2;
            };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float4 color:COLOR;
                float4 uv:TEXCOORD0;
                float4 data:TEXCOORD1;
                float4 extra:TEXCOORD2;
                float near:TEXCOORD3;   // fades a quad that comes within a few centimetres of the camera
                UNITY_FOG_COORDS(4)
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=mul(UNITY_MATRIX_VP,float4(v.vertex,1.0));
                o.color=v.color;o.uv=v.uv;o.data=v.data;o.extra=v.extra;
                float depth=-mul(UNITY_MATRIX_V,float4(v.vertex,1.0)).z;
                o.near=saturate((depth-0.03)/0.12);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                // Screen-space widths taken before any branch: the guard's edge coordinate and its metric (scanlines, cells).
                float2 aa=float2(max(fwidth(i.uv.x),fwidth(i.uv.y)),fwidth(i.extra.w));
                float4 c=BlockFxShade(i.color,i.uv,i.data,i.extra,aa,_BlockFxLow)*i.near;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
