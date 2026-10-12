Shader "ChipArena/NativeRooftop"
{
    // The home screen's rooftop city (NativeRooftopPresentation, Core NativeRooftopCity): one opaque pass for every static
    // surface, the vertex mode (uv0.z) choosing the look. Built-in forward, no textures, no Unity lights (the scene is lit by
    // its own night ambient, neon spill and fog), HDR out for NativeBloom.
    //  0 facade  a procedural window grid (1.5 x 2.4 m bays) in metric face coordinates (uv0.xy); a hash-lit share per tower
    //            (uv1: seed, tier, hue, roof flag), fading to the bay average where a bay is a few pixels or less so it never
    //            shimmers; roofs take a faint noise.
    //  1 lit     dark wet concrete and props: albedo in uv1.rgb, the night ambient, the sign's magenta and the cyan spill,
    //            and a wet sky reflection on upward faces.
    //  2 neon    an emissive bar or the street glow: HDR colour in uv1.rgb.
    //  3 car     a flying car's body with its cyan underglow and side strip (uv0.xy the body's local height and side).
    //  4 puddle  a dark mirror of the sky glow with rain rings, its shape cut from the quad (uv0.xy 0..1).
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 world:TEXCOORD2;float3 normal:TEXCOORD3;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv0=v.uv0;o.uv1=v.uv1;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            float3 Pal(float h){return h<.45?float3(.30,1.9,2.4):h<.7?float3(2.2,.35,1.9):h<.88?float3(1.5,1.3,1.1):float3(.55,.75,2.2);}
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);float mode=i.uv0.z;float3 c;
                if(mode<.5)
                {
                    float seed=i.uv1.x,tier=i.uv1.y,hue=i.uv1.z;bool roof=i.uv1.w>.5;
                    c=float3(.006,.008,.020)+float3(.004,.003,.008)*RoofHash1(float3(seed,seed,seed));
                    c+=(float3(.03,.025,.07)*max(0.0,n.y)+float3(.05,.012,.045)*max(0.0,-n.y))*.6;
                    float2 g=i.uv0.xy/float2(1.5,2.4);float2 fw=fwidth(g);
                    if(!roof&&tier<8.5)
                    {
                        float2 id=floor(g),f=frac(g);float lit=step(.58+.22*RoofHash1(float3(seed,1.0,1.0)),RoofHash2(id+seed*.37));
                        float win=step(.16,f.x)*step(f.x,.84)*step(.28,f.y)*step(f.y,.80);float far=smoothstep(.12,.34,max(fw.x,fw.y));
                        float hh=RoofHash2(id*1.7+seed);float3 wc=Pal(frac(hue+hh*.35))*(.35+.65*RoofHash2(id+3.1));
                        float flick=1.0-.5*step(.995,RoofHash2(id+floor(_RoofTime*3.0)*.13));
                        float3 wcol=wc*lit*flick*lerp(win,.42,far)*lerp(.55,1.0,step(.85,RoofHash2(id+9.7)));
                        float band=step(.96,frac(i.uv0.y/18.0+seed))*.6;
                        c+=wcol*.42+Pal(hue)*band*.10;
                    }
                    if(roof)c+=float3(.02,.03,.05)*(.5+.5*RoofNoise(i.world*.4));
                }
                else if(mode<1.5)
                {
                    float3 v=normalize(_WorldSpaceCameraPos-i.world);float3 alb=i.uv1.rgb;
                    float3 amb=float3(.06,.07,.16)*max(0.0,n.y)+float3(.10,.03,.10)*max(0.0,n.z*.5+.2)+float3(.04,.12,.16)*max(0.0,-n.x);
                    float3 ls=_RoofSignPos.xyz-i.world;float ds=length(ls);float3 sign=_RoofSignColor.rgb*max(0.0,dot(n,ls/ds))*4.0/(ds*ds+4.0);
                    float3 lc=_RoofCyanPos.xyz-i.world;float dc=length(lc);float3 cy=float3(.2,1.2,1.5)*max(0.0,dot(n,lc/dc))*1.2/(dc*dc+3.0);
                    float wet=n.y>.5?1.0:0.0;float3 r=reflect(-v,n);float3 refl=RoofSkyBase(normalize(float3(r.x,abs(r.y),r.z)))*1.2;float fres=pow(1.0-max(0.0,dot(n,v)),4.0);
                    c=alb*(1.0+amb*2.0)+alb*(sign+cy)*3.0+refl*fres*wet*.16;
                }
                else if(mode<2.5)c=i.uv1.rgb;
                else if(mode<3.5)
                {
                    c=float3(.02,.025,.04)*(1.0+3.0*max(0.0,n.y))+float3(.05,.01,.05)*max(0.0,-n.y);
                    c+=float3(.2,1.8,2.4)*step(abs(i.uv0.y+.05),.06)*step(.5,abs(n.x))+float3(.1,.9,1.2)*step(.5,-n.y)*.6+float3(.25,.35,.45)*step(.25,i.uv0.y)*step(.5,abs(n.x))*.3;
                }
                else
                {
                    float2 q=i.uv0.xy*2.0-1.0;float shape=smoothstep(1.0,.75,length(q)+.25*(RoofNoise(float3(i.uv0.xy*4.0,1.0))-.5));clip(shape-.5);
                    float3 v=normalize(_WorldSpaceCameraPos-i.world);float3 r=reflect(-v,float3(0,1,0));float fres=.25+.75*pow(1.0-max(0.0,v.y),3.0);
                    float ring=0.0;
                    [unroll] for(int k=0;k<4;k++)
                    {
                        float fk=k;float cyc=_RoofTime*1.3+fk*.37;float2 cc=float2(RoofHash2(float2(fk,floor(cyc))),RoofHash2(float2(fk+7.0,floor(cyc))))*1.6-.8;
                        float age=frac(cyc);float d=length(q-cc);ring+=smoothstep(.025,0.0,abs(d-age*.32))*(1.0-age);
                    }
                    float3 refl=RoofSkyBase(normalize(float3(r.x,abs(r.y)+.02,r.z)))*1.6+_RoofSignColor.rgb*.05*smoothstep(.2,1.0,i.uv0.y)+float3(.2,.9,1.1)*.06;
                    c=refl*fres+float3(.4,.8,1.0)*ring*.25+float3(.012,.014,.02);
                }
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
