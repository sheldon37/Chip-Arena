// C12 Siren black-flame effects (client/siren/flames.ts, fx.ts, first-person.ts and their materials.ts shaders).
// Vertices are in world space and carry everything per vertex, so one dynamic mesh holds a frame's flames or effects:
//   COLOR      colour and opacity, as the source material holds it (see flags)
//   TEXCOORD0  uv.xy, mode (z), flags (w: +1 additive, +2 authored sRGB colour)
//   TEXCOORD1  mode parameters: slash (sweep, fade), shell (fade, speed), orb/ink (local position xyz, rim strength)
//   TEXCOORD2  second colour: slash/shell edge, orb/ink rim (w: 1 orb churn with power 2.2, 0 ink rim power 3)
// Modes: 0 flat, 1 soft radial sprite (alpha (1-r)^1.6), 2 claw slash, 3 flame shell, 4 orb / ink rim.
// "Raw" colours come from three.js ShaderMaterials, which write their linear colour values to the screen unencoded
// (particleMaterial, glowMaterial, orbMaterial, flameShellMaterial, the slash); authored colours are MeshBasic, Lambert
// or Sprite colours, which three encodes to sRGB. Both reach the screen as the source shows them in a Linear or Gamma
// project. Output is premultiplied (Blend One OneMinusSrcAlpha): additive pieces write alpha 0. No tone mapping.
Shader "ChipArena/NativeSirenFx"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _Src("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst("Destination blend",Float)=10
        _ZWrite("Depth write",Float)=0
        // LEqual (4) for every layer but the overlay (Always, 8): the Marked crosshair draws over everything (depthTest false).
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test",Float)=4
        _FxTime("Effect clock",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_Src] [_Dst]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _FxTime;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;float4 uv:TEXCOORD0;float4 p:TEXCOORD1;float4 q:TEXCOORD2;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 p:TEXCOORD1;float4 q:TEXCOORD2;float3 normal:TEXCOORD3;float3 world:TEXCOORD4;UNITY_FOG_COORDS(5)};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.p=v.p;o.q=v.q;
                o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float mode=i.uv.z,flags=i.uv.w;bool additive=fmod(flags,2)>.5,authored=flags>1.5;
                float3 c=i.color.rgb,e=i.q.rgb;float a=i.color.a;
                // Authored colours work in linear light (three's built-in materials); raw ones in display values.
                if(authored){c=GammaToLinearSpace(c);e=GammaToLinearSpace(e);}
                if(mode>3.5)
                {
                    // orbMaterial: core + rim·(f·strength + churn·f·.6), f = (1-|N·V|)^2.2; InkMaterial rim: f = (1-|N·V|)^3.
                    float3 V=normalize(_WorldSpaceCameraPos-i.world);float facing=saturate(abs(dot(normalize(i.normal),V)));
                    float orb=i.q.w;float f=pow(1-facing,lerp(3.0,2.2,orb));
                    float churn=.5+.5*sin(i.p.y*14-_FxTime*9+sin(i.p.x*11+_FxTime*4)*2);
                    c=c+e*(f*i.p.w+orb*churn*f*.6);
                }
                else if(mode>2.5)
                {
                    // flameShellMaterial: dark licking tongues with pale edges and holes scrolling upward.
                    float ang=i.uv.x*6.2831853,y=i.uv.y,t=_FxTime*i.p.y;
                    float licks=.5+.5*sin(ang*7+sin(y*4-t*6)*1.4+t*1.7);
                    float tip=.62+.3*sin(ang*5+t*7)*sin(ang*3-t*3.3)+.08*sin(ang*11-t*9);
                    float body=licks*.55+.45-smoothstep(tip-.35,tip,y)*1.15;a=smoothstep(.32,.52,body)*i.p.x;clip(a-.04);
                    float edge=1-smoothstep(0,.12,abs(body-.42));float3 V=normalize(_WorldSpaceCameraPos-i.world);float fres=pow(1-abs(dot(normalize(i.normal),V)),2);
                    c=lerp(c,e,saturate(edge*.8+fres*.3));
                }
                else if(mode>1.5)
                {
                    // Claw slash: three black gouges with ragged burning pale edges, revealed by the sweep.
                    float lead=i.p.x-i.uv.x;clip(lead);float tail=smoothstep(0,.06,lead)*(1-smoothstep(.3,.85,lead));
                    float k=frac(i.uv.y*3),taper=sin(3.14159*saturate(i.uv.x));float gouge=smoothstep(.12,.3,k)*(1-smoothstep(.62,.8,k));
                    float rag=.5+.5*sin(i.uv.x*70+_FxTime*24);float edge=smoothstep(.5,.62,k)*(1-smoothstep(.62,.8,k))*(.7+.3*rag);
                    a=max(gouge*.9,edge)*tail*i.p.y*smoothstep(0,.25,taper*(.6+.4*k));clip(a-.02);c=lerp(c,e,edge);
                }
                else if(mode>.5)a*=pow(saturate(1-length(i.uv.xy)),1.6);
                // Scene fog (the slash material has none); additive pieces fade toward black as the blast glow does.
                if(mode<1.5||mode>2.5)
                {
                    #ifdef UNITY_COLORSPACE_GAMMA
                    float3 fog=GammaToLinearSpace(unity_FogColor.rgb);
                    #else
                    float3 fog=unity_FogColor.rgb;
                    #endif
                    float4 shaded=float4(c,1);UNITY_APPLY_FOG_COLOR(i.fogCoord,shaded,float4(additive?float3(0,0,0):fog,1));c=shaded.rgb;
                }
                #ifdef UNITY_COLORSPACE_GAMMA
                if(authored)c=LinearToGammaSpace(max(c,0));
                #else
                if(!authored)c=GammaToLinearSpace(max(c,0));
                #endif
                a=saturate(a);return float4(c*a,additive?0:a);
            }
            ENDCG
        }
    }
}
