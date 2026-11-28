Shader "ChipArena/NativeFallenFx"
{
    // NOCTIEL's effects (Core NativeFallenBossFx): quads with a mode in their uv, black and white only. Premultiplied, so a quad can add white light (alpha 0) or stand as black (alpha 1). Drawn twice: over the water, and mirrored under it first so everything reflects.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float _FbClock;
            struct a2v{float4 vertex:POSITION;float4 uv:TEXCOORD0;float2 tint:TEXCOORD1;};
            struct v2f{float4 pos:SV_POSITION;float4 uv:TEXCOORD0;float2 tint:TEXCOORD1;float3 at:TEXCOORD2;float3 eye:TEXCOORD3;};
            v2f vert(a2v v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.tint=v.tint;o.at=v.vertex.xyz;o.eye=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;return o;}
            float4 frag(v2f i):SV_Target
            {
                float u=i.uv.x,w=i.uv.y;int mode=(int)(i.uv.z+.5);float s=i.uv.w,a=i.tint.y,g=i.tint.x;float x=u*2-1;
                if(mode==1){float k=exp(-w*w*7.)*.45+exp(-w*w*70.);return float4(k.xxx*s*a,0);}
                if(mode==2){float d=x*x+w*w;float k=exp(-d*7.);return float4(k.xxx*s*a,0);}
                if(mode==3){float side=1-x*x*x*x;float up=saturate(w*.5+.5);float k=side*(.28+.72*(1-up)*(1-up));k*=.8+.2*sin(up*40.-_FbClock*22.+x*5.);float near=saturate(distance(i.at,i.eye)/30.);k*=near*near*.9+.1;return float4(k.xxx*s*a,0);}
                if(mode==4)
                {
                    // Ink: a black body, its edges lit and torn. w -1 foot, 1 crest.
                    float along=i.at.x*1.3+i.at.z*1.1;float tear=.09*sin(along*1.3+_FbClock*5.)+.05*sin(along*3.1-_FbClock*9.)+.02*sin(along*7.3);float e=smoothstep(.74+tear,.92+tear,abs(w));float crest=smoothstep(.2,.9,w);
                    float body=smoothstep(1.02,.92,abs(w));float white=e*(.4+.9*crest);return float4(white.xxx*s*a*body,a*body*(1-e*.4));
                }
                if(mode==5){float along=i.at.x*1.3+i.at.z*1.1;float edge=.5+.32*sin(along*4.+3.1*sin(along*9.))+.16*sin(along*21.);float k=smoothstep(edge,edge*.35,abs(w));return float4(k.xxx*s*a*1.3,0);}
                if(mode==6){float prof=pow(max(0.,sin(u*3.14159)),.7);float k=smoothstep(prof,prof*.35,abs(w));return float4(k.xxx*s*a,0);}
                if(mode==7)
                {
                    // A sword's trail, across it: u along, w -1 hilt, 1 point. A bright thin edge at the point, thinning off toward the hilt; black under it for the black blade.
                    float tip=saturate(w*.5+.5);float k=pow(tip,9.)*1.5+.07*tip*tip;
                    return float4(g*k.xxx*s*a,a*(1-g)*tip*tip*.85);
                }
                if(mode==8){float k=(1-x*x)*(1-w*w);return float4(k.xxx*s*a,0);}
                if(mode==9){float up=saturate(w*.5+.5);float k=up*up*up;return float4(k.xxx*s*a,0);}
                return float4(g*s*a,g*s*a,g*s*a,a*(1-g));
            }
            ENDCG
        }
    }
    Fallback Off
}
