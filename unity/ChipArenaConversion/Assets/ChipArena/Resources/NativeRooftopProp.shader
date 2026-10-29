Shader "ChipArena/NativeRooftopProp"
{
    // The home screen's built detail (owner 2026-10-29: "Add exceptional detail to make a beautiful display"; Core
    // NativeRooftopCity.Prop quads, drawn by NativeRooftopPresentation): our roof and its machinery, the towers' parapets,
    // ledges, fins, crowns, rooftop plant, water tanks, masts, fire escapes, balconies and signs. One short opaque pass.
    //  uv0.xy  metric face coordinates (patterns: seams, slats, glyphs)
    //  uv1     rgb the linear albedo (HDR glow on signs and lamps), a the material:
    //          0 concrete   grain and a wet sheen on its tops
    //          1 metal      painted panels with their seams, a sky sheen
    //          2 grille     louvres in dark slots
    //          3 sign       a lit panel of stacked glyph strokes (each sign its own), a faint flicker
    //          4 membrane   the roof's wet membrane: gravel, laps between its strips, the sky in the wet
    //          5 glass      dark glazing mirroring the sky
    //          6 lamp       a soft emissive housing
    // Lit as the towers are (NativeRooftopLights.cginc: the street's glow, the screens' and signs' spill, the sky) with a
    // rim from the haze. HDR out for NativeBloom; the city fog last.
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            #include "NativeRooftopLights.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 world:TEXCOORD2;float3 normal:TEXCOORD3;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv0=v.uv0;o.uv1=v.uv1;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            // 1 on lines `width` wide every `period` (antialiased by the footprint `fw`).
            float Lines(float x,float period,float fw,float width){float d=abs(frac(x/period+.5)-.5)*period;return 1.0-smoothstep(width*.5,width*.5+fw*period*1.5,d);}
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);float3 alb=i.uv1.rgb;float mat=i.uv1.a;float2 uv=i.uv0.xy;
                // footprints before any flow control
                float2 fw=max(fwidth(uv),float2(1e-4,1e-4));float2 fg=max(fwidth(uv*float2(1.0,1.0)/float2(.55,.7)),float2(1e-4,1e-4));
                float3 v=normalize(_WorldSpaceCameraPos-i.world);float rim=pow(1.0-saturate(dot(n,v)),3.0);
                float3 light=RoofCityGlow(i.world,n)*1.6+RoofSpill(i.world,n);float up=saturate(n.y);
                float3 c;
                if(mat<.5)
                {
                    float grain=RoofNoise(i.world*3.1)*.25+RoofNoise(i.world*.6)*.3;
                    c=alb*(.75+grain)*light*2.4+RoofMirror(i.world,n,.03)*up*.55;
                }
                else if(mat<1.5)
                {
                    float seam=max(Lines(uv.x,1.25,fw.x,.03),Lines(uv.y,1.25,fw.y,.03));
                    c=alb*(1.0-.45*seam)*light*2.2+RoofMirror(i.world,n,.10)*.55+light*rim*.10;
                }
                else if(mat<2.5)
                {
                    float slot=Lines(uv.y,.12,fw.y,.06);
                    c=alb*(.35+.65*(1.0-slot))*light*2.0+RoofMirror(i.world,n,.06)*.25*(1.0-slot);
                }
                else if(mat<3.5)
                {
                    // glyph cells .55 x .7 m down the panel, two to four strokes each, gaps between them
                    float2 g=uv/float2(.55,.7);float2 id=floor(g),f=frac(g);float k=RoofHash2(id+floor(alb.r*7.0+alb.b*3.0));
                    float vbar=step(abs(f.x-.25-.5*RoofHash2(id+1.3)),.07)*step(.15,f.y)*step(f.y,.85);
                    float hbar=step(abs(f.y-.3-.4*RoofHash2(id+2.9)),.06)*step(.15,f.x)*step(f.x,.85);
                    float box=step(.6,k)*(step(abs(f.x-.5),.32)*step(abs(f.y-.5),.32)-step(abs(f.x-.5),.22)*step(abs(f.y-.5),.22));
                    float glyph=saturate(vbar+hbar*step(.3,k)+box);float far=smoothstep(.25,.6,max(fg.x,fg.y));
                    float flick=1.0-.7*step(.993,RoofHash2(float2(floor(_RoofTime*6.0),floor(alb.g*17.0+.5))));
                    c=alb*lerp(.10+.95*glyph,.42,far)*flick+alb*.04;
                }
                else if(mat<4.5)
                {
                    float gravel=RoofNoise(i.world*9.0)*.35+RoofNoise(i.world*1.3)*.25;float lap=Lines(i.world.x,1.0,fw.x,.02)*.35;
                    float wet=smoothstep(.45,.75,RoofNoise(float3(i.world.xz*.35,3.0)));
                    c=alb*(.7+gravel)*(1.0-lap)*light*2.2+RoofMirror(i.world,n,.04+.10*wet)*(.5+1.2*wet)*up;
                }
                else if(mat<5.5)c=alb*light+RoofMirror(i.world,n,.08)*1.4;
                else c=alb*(.85+.15*RoofNoise(i.world*2.0));
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
        // A depth-only shadow caster: the props in the camera's depth texture (the anime outline reads it); nothing casts.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
            struct v2f {V2F_SHADOW_CASTER;};
            v2f vert(appdata v){v2f o;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)return o;}
            float4 frag(v2f i):SV_Target{SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
