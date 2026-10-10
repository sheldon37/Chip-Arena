// The Auditor halo (Core/NativeAuditorHalo.cs, drawn by Runtime/NativeAuditorHaloPresentation.cs). Built-in pipeline,
// unlit, no tone mapping; colours arrive linear in a Linear project (the material constants are converted by the Runtime).
// Vertices sit in the halo group's space (x mirrored into Unity's frame) and say what they are:
//   TEXCOORD0  kind (x: 0 arc, 1 glow shell, 2 crackle, 3 ember, 4 haze, 5 flame, 6 flat flame), along/corner x (y),
//              index (z: arc, bolt, ember, flame slot), shell layer / ribbon side / corner y (w)
//   TEXCOORD1  crackle plane and segment, sprite size
// The per-draw state comes from Core (NativeAuditorHalo.Pack): _HaloA (wrapped clock, opacity, haze, light), the arcs' turn
// and pulse, the crackle ends, the flames in far-to-near order, the flat flames' flicker and the embers. Materials set the
// blend: opaque (arcs and flat flames; a fading halo dithers them out like the body), premultiplied (flames) or additive
// (shells, crackle, embers, haze; coverage = brightest channel, as the portraits' straight-alpha resolve needs).
Shader "ChipArena/NativeAuditorHalo"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
        _HaloFog("Scene fog",Float)=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend [_Src] [_Dst]
        ZWrite [_ZWrite]
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            float4 _HaloA,_HaloSpin,_HaloPulse;
            float4 _HaloBolts[6];
            float4 _HaloFlames[24];
            float4 _HaloFins[12];
            float4 _HaloEmbers[8];
            float4 _HaloDeep,_HaloRed,_HaloHot,_HaloWhite,_HaloGlow,_HaloBolt,_HaloEmber,_HaloSoot,_HaloInner,_HaloRim;
            float _HaloFog;
            // ---- HALO SHARED BEGIN (HLSL and GLSL: float literals only, no matrices, no implicit conversions) ----
            float HaloHash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }
            float HaloNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = HaloHash(i);
                float b = HaloHash(i + float2(1.0, 0.0));
                float c = HaloHash(i + float2(0.0, 1.0));
                float d = HaloHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float HaloFbm(float2 p)
            {
                float v = 0.5 * HaloNoise(p);
                p = p * 2.03 + float2(1.7, 9.2);
                v += 0.25 * HaloNoise(p);
                p = p * 2.01 + float2(8.3, 2.8);
                v += 0.125 * HaloNoise(p);
                return v / 0.875;
            }
            // An energy pulse travelling along each arc.
            float HaloTravel(float s, float arc, float clock)
            {
                float d = frac(s * 0.85 - clock * 0.42 + arc * 0.37) - 0.5;
                return exp(-d * d * 36.0);
            }
            // The core tube: deep crimson at the silhouette, red, a hot centre and a white-hot line facing the eye.
            float3 HaloArcColor(float ndv, float pulse, float travel, float3 deep, float3 red, float3 hot, float3 white)
            {
                float3 c = lerp(deep, red, smoothstep(0.0, 0.55, ndv));
                c = lerp(c, hot, pow(ndv, 3.0));
                c += white * pow(ndv, 10.0) * (0.55 + 0.45 * travel);
                return c * pulse * (1.0 + 0.5 * travel);
            }
            // Soft glow shells: bright toward the tube's middle, gone at their silhouette.
            float HaloShell(float ndv, float layer)
            {
                return layer < 0.5 ? pow(ndv, 2.2) * 0.5 : pow(ndv, 1.5) * 0.10;
            }
            // A crackle ribbon across its width (side -1..1): a thin white-pink core in a red glow, thinning at the ends.
            float3 HaloBoltColor(float side, float u, float3 bolt, float3 glow)
            {
                float a = side * side;
                float ends = sqrt(saturate(sin(u * 3.14159)));
                return (bolt * exp(-a * 16.0) + glow * exp(-a * 2.5) * 0.55) * ends;
            }
            float2 HaloBoltJitter(float k, float seed)
            {
                return float2(HaloHash(float2(k, seed)), HaloHash(float2(seed + 3.7, k + 7.0))) - 0.5;
            }
            float HaloSoft(float2 c, float k)
            {
                float r = dot(c, c);
                return exp(-r * k) * saturate(1.0 - r);
            }
            // A black flame in its quad: p.x across (-1..1 of the half width), p.y up (0 the ring .. 1 the flame's height).
            // A swaying teardrop whose outline upward-scrolling noise eats into tongues, a soot core with a deep red heart
            // low down and a thin ember rim that is hottest near the ring. `detail` (0..1, from the flame's size on screen)
            // fades the tongues out on a distant flame, which keeps a clean silhouette instead of noise speckle.
            // Returns premultiplied colour and coverage.
            float4 HaloFlameColor(float2 p, float seed, float phase, float rimStrength, float clock, float detail, float3 soot, float3 inner, float3 rim)
            {
                float y = saturate(p.y);
                float sway = sin(y * 3.4 - clock * 4.6 + phase) * 0.20 * y * y + sin(y * 7.3 - clock * 8.1 + phase * 1.7) * 0.06 * y;
                float x = p.x - sway;
                float width = (0.95 - 0.10 * detail) * sqrt(saturate(y * 3.0 + 0.08)) * pow(saturate(1.0 - y), 0.85);
                float n = HaloFbm(float2(x * 2.8 + seed * 17.0, y * 1.5 - clock * 2.6 + seed * 5.0));
                float lick = HaloNoise(float2(x * 6.0 + seed * 31.0, y * 4.0 - clock * 4.2));
                float edge = abs(x) / max(width, 0.001) + ((n - 0.5) * 2.8 * smoothstep(0.08, 0.95, y) + (lick - 0.5) * 0.6 * y) * detail;
                float base = smoothstep(-0.1, 0.05, p.y);
                float cover = (1.0 - smoothstep(0.8, 1.0, edge)) * base;
                float band = smoothstep(0.70, 0.96, edge);
                float heat = 1.0 - y;
                float3 body = lerp(inner, soot, smoothstep(0.0, 0.5, y));
                float3 glow = rim * band * (0.12 + 0.88 * heat * heat * heat) * rimStrength;
                return float4((body + glow) * cover, cover);
            }
            // ---- HALO SHARED END ----
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 data:TEXCOORD0;float4 extra:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float4 data:TEXCOORD0;float4 info:TEXCOORD1;float3 normal:TEXCOORD2;float3 world:TEXCOORD3;UNITY_FOG_COORDS(4)};
            float Pick(float4 v,int i){return i==0?v.x:i==1?v.y:v.z;}
            // A turn about the group's up axis (Core NativeAuditorHalo.ShaderTurn).
            float3 Turn(float3 p,float a){float c=cos(a),s=sin(a);return float3(p.x*c+p.z*s,p.y,-p.x*s+p.z*c);}
            float3 ToEye(float3 world){return unity_OrthoParams.w>.5?UNITY_MATRIX_V[2].xyz:_WorldSpaceCameraPos-world;}
            v2f vert(appdata v)
            {
                v2f o;float kind=v.data.x;int index=(int)(v.data.z+.5);float3 local=v.vertex.xyz,n=v.normal,world=float3(0,0,0);o.info=float4(0,0,0,0);
                float scale=length(unity_ObjectToWorld._m00_m10_m20);float3 up=normalize(mul((float3x3)unity_ObjectToWorld,float3(0,1,0)));
                if(kind<1.5)
                {
                    float a=Pick(_HaloSpin,index);local=Turn(local,a);n=Turn(n,a);world=mul(unity_ObjectToWorld,float4(local,1)).xyz;o.info.x=Pick(_HaloPulse,index);
                }
                else if(kind<2.5)
                {
                    // The crackle: a zigzag between the arc ends (re-formed with the seed), two crossed ribbons so it reads from any side.
                    float4 from=_HaloBolts[index],to=_HaloBolts[index+3];float u=v.data.y;float3 axis=to.xyz-from.xyz;float len=max(length(axis),1e-4);axis/=len;
                    float3 across=cross(axis,float3(0,1,0));across=dot(across,across)>1e-8?normalize(across):float3(1,0,0);float3 lift=cross(across,axis);
                    float2 j=HaloBoltJitter(v.extra.y,to.w)*sin(u*3.14159)*max(len,.05)*.9;
                    local=lerp(from.xyz,to.xyz,u)+across*j.x+lift*j.y+(v.extra.x<.5?lift:across)*v.data.w*.024;
                    world=mul(unity_ObjectToWorld,float4(local,1)).xyz;o.info.x=from.w;
                }
                else if(kind<4.5)
                {
                    // Embers and the haze: sprites turned to the camera.
                    float4 e=kind<3.5?_HaloEmbers[index]:float4(0,-.03,0,1);float size=v.extra.x*(kind<3.5?.55+.45*e.w:1);
                    float3 centre=mul(unity_ObjectToWorld,float4(e.xyz,1)).xyz;world=centre+(UNITY_MATRIX_V[0].xyz*v.data.y+UNITY_MATRIX_V[1].xyz*v.data.w)*size*scale;o.info.x=e.w;
                }
                else if(kind<5.5)
                {
                    // A flame slot: stands on its ring point and turns about the group's up axis to face the eye.
                    float4 f=_HaloFlames[index],g=_HaloFlames[index+12];float3 centre=mul(unity_ObjectToWorld,float4(f.xyz,1)).xyz;
                    float3 r=cross(up,ToEye(centre));float3 right=dot(r,r)>1e-8?normalize(r):UNITY_MATRIX_V[0].xyz;
                    world=centre+right*v.data.y*g.x*scale+up*v.data.w*f.w*scale;o.info=float4(g.z,g.y,g.w,0);
                }
                else
                {
                    // A flat source flame on its ring point (Core NativeAuditorHalo.FinOffset).
                    float4 fin=_HaloFins[index];float sx=v.data.y*fin.x,sy=v.data.w*fin.y,c=cos(fin.z),s=sin(fin.z);float3 outward=normalize(float3(local.x,0,local.z)+float3(1e-6,0,0));
                    local+=outward*(sx*c-sy*s)+float3(0,1,0)*(sx*s+sy*c);world=mul(unity_ObjectToWorld,float4(local,1)).xyz;
                }
                o.world=world;o.normal=UnityObjectToWorldNormal(n);o.pos=mul(UNITY_MATRIX_VP,float4(world,1));o.data=v.data;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float kind=i.data.x,clock=_HaloA.x,opacity=_HaloA.y;float4 col=float4(0,0,0,0);int blend=0;
                float3 eye=normalize(ToEye(i.world));float ndv=saturate(dot(normalize(i.normal),eye));
                if(kind<.5||kind>5.5)
                {
                    // Opaque: the arcs, or a flat flame (soot). A fading halo dithers out on the screen grid.
                    if(opacity<.999)clip(opacity-HaloHash(floor(i.pos.xy)));
                    col=kind<.5?float4(HaloArcColor(ndv,i.info.x,HaloTravel(i.data.y,i.data.z,clock),_HaloDeep.rgb,_HaloRed.rgb,_HaloHot.rgb,_HaloWhite.rgb),1):float4(_HaloSoot.rgb,1);blend=0;
                }
                else if(kind<5.5&&kind>4.5){col=HaloFlameColor(i.data.yw,i.info.x,i.info.y,i.info.z,clock,saturate(1.6-fwidth(i.data.w)*18),_HaloSoot.rgb,_HaloInner.rgb,_HaloRim.rgb)*opacity;blend=2;}
                else
                {
                    float3 c;
                    if(kind<1.5)c=_HaloGlow.rgb*HaloShell(ndv,i.data.w)*i.info.x*(1+.6*HaloTravel(i.data.y,i.data.z,clock));
                    else if(kind<2.5)c=HaloBoltColor(i.data.w,i.data.y,_HaloBolt.rgb,_HaloGlow.rgb)*i.info.x;
                    else if(kind<3.5){float a=HaloSoft(i.data.yw,4.0)*i.info.x;c=_HaloEmber.rgb*a+_HaloWhite.rgb*a*a*a*.35;}
                    else c=_HaloGlow.rgb*HaloSoft(i.data.yw,3.0)*.03*_HaloA.z;
                    c*=opacity;col=float4(c,max(c.r,max(c.g,c.b)));blend=1;
                }
                float4 fogged=col;
                if(blend==0){UNITY_APPLY_FOG(i.fogCoord,fogged);}
                else if(blend==1){UNITY_APPLY_FOG_COLOR(i.fogCoord,fogged,float4(0,0,0,0));}
                else{UNITY_APPLY_FOG_COLOR(i.fogCoord,fogged,float4(unity_FogColor.rgb*col.a,col.a));}
                return float4(lerp(col.rgb,fogged.rgb,_HaloFog),col.a);
            }
            ENDCG
        }
    }
}
