Shader "ChipArena/NativeDeepEndFlow"
{
    // THE DEEP END's water geometry as Core NativeDeepEndBossFx builds it each frame (DeepEndFlow.cs vertices: position, normal, A (u, v, kind, seed), B (colour, share)): falling sheets streaked and breaking up as they fall, the jet's solid beam, the hands of water,
    // foam collars, the room's round lights and their halos, shadows on the water, drifting tile. Premultiplied: a kind writes its own alpha, the additive ones write none. Two materials use it: one under the water sheet (so the sheet refracts and absorbs it), one over.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "NativeDeepEndRoom.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 a:TEXCOORD0;float4 b:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 a:TEXCOORD2;float4 b:TEXCOORD3;UNITY_FOG_COORDS(4)};
            v2f vert(appdata v){v2f o;float4 w=float4(v.vertex.xyz,1.0);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;o.normal=v.normal;o.a=v.a;o.b=v.b;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float Hash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
            float Noise(float2 p){float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);return lerp(lerp(Hash(i),Hash(i+float2(1.0,0.0)),u.x),lerp(Hash(i+float2(0.0,1.0)),Hash(i+float2(1.0,1.0)),u.x),u.y);}
            float3 Lit(){return float3(0.82,0.88,0.88)*(_DeepLight.y*0.82+0.05)+float3(0.14,0.13,0.12)*_DeepLight.x;}
            fixed4 frag(v2f i):SV_Target
            {
                float kind=i.a.z,seed=i.a.w;float3 rgb=i.b.rgb;float share=i.b.a;float t=_DeepClock;float3 n=normalize(i.normal);float3 v=normalize(_WorldSpaceCameraPos-i.world);fixed4 o;
                if(kind<0.5)
                {
                    // A falling sheet: u -1..1 across, v its fall time. Long streaks running down, thin at the top, tearing into strands as it falls.
                    float across=i.a.x,fall=i.a.y;float edge=smoothstep(1.0,0.5,abs(across));float s1=Noise(float2(across*3.6+seed,fall*0.8-t*2.2));float s2=Noise(float2(across*11.0+seed*2.1,fall*1.8-t*3.6));float tear=smoothstep(0.12+fall*0.34,0.62+fall*0.26,s1*0.62+s2*0.38);
                    float a=saturate(share*edge*(0.1+0.68*tear)*(0.7+0.3*s2));float nv=abs(dot(n,v));float fres=0.05+0.95*pow(1.0-nv,3.0);float3 rv=reflect(-v,n);float3 c=rgb*Lit()*(0.5+0.5*s2)+DeepRoomColour(i.world,rv)*fres*0.9;
                    o=fixed4(c*a,a*0.84);
                }
                else if(kind<1.5)
                {
                    // The jet's solid beam: flow noise racing along it, white where it's rough, its rim bright; the core is the same in a brighter colour.
                    float u=i.a.x;float s=Noise(float2(u*3.0-t*9.0+seed*3.0,i.a.y*11.0))*0.6+Noise(float2(u*9.0-t*16.0,i.a.y*23.0))*0.4;float nv=abs(dot(n,v));float rim=pow(1.0-nv,2.2);float a=saturate(share*(0.72+0.3*rim+0.25*s));
                    float3 c=rgb*(0.55+0.5*s)*(Lit()+0.32)+float3(0.5,0.62,0.66)*rim*(_DeepLight.y+0.25);o=fixed4(c*a,a);
                }
                else if(kind<2.5)
                {
                    // A hand of water: clear in the middle, bright at its edges, moving under its skin.
                    float nv=abs(dot(n,v));float rim=pow(1.0-nv,2.6);float s=Noise(float2(i.a.x*5.0+seed,i.a.y*9.0-t*1.4));float a=saturate(share*(0.34+0.62*rim+0.12*s));float3 rv=reflect(-v,n);
                    float3 c=rgb*Lit()*(0.5+0.4*s)+DeepRoomColour(i.world,rv)*(0.08+0.92*pow(1.0-nv,4.0));o=fixed4(c*a,a);
                }
                else if(kind<3.5)
                {
                    // A foam collar: lace, thick at the body, breaking up outward.
                    float out_=i.a.y;float s=Noise(float2(i.a.x*48.0+seed,out_*3.0-t*0.5))*0.6+Noise(float2(i.a.x*120.0,out_*7.0+t*0.8))*0.4;float a=saturate(smoothstep(1.0-out_*0.55,0.95-out_*0.55-0.25,1.0-s)*(1.0-out_)*1.4);a=saturate(a+(1.0-smoothstep(0.0,0.3,out_))*0.55);
                    float3 c=rgb*Lit();o=fixed4(c*a,a);
                }
                else if(kind<4.5)
                {
                    // A halo: soft and added, a little flicker from the water over it.
                    float r=length(i.a.xy);float g=saturate(1.0-r);g=g*g*g*(1.0+1.6*saturate(1.0-r*2.6));o=fixed4(rgb*g,0.0);
                }
                else if(kind<5.5)
                {
                    float r=length(i.a.xy);float m;float3 c=rgb;
                    if(seed<0.5){float2 e=float2(max(0.0,abs(i.a.x)-0.86)/0.14,i.a.y);m=saturate(1.0-length(e));m=m*m*(3.0-2.0*m);}       // A long shadow: its ends rounded.
                    else if(seed<1.5){m=smoothstep(1.0,0.45,r);}                                                                                 // A dark disc.
                    else if(seed<2.5){float rr=frac(r*2.3-t*0.42);m=smoothstep(1.0,0.75,r)*(0.25+0.75*pow(rr,6.0));}                             // Rings moving in.
                    else if(seed<3.5){float s=Noise(i.a.xy*5.0+t*0.4);m=smoothstep(0.82,0.38,r)*smoothstep(0.34,0.7,s+0.3*(1.0-r));c=rgb*Lit();}// The foam at a hand's root.
                    else{float s=Noise(i.a.xy*2.2+0.3)*0.5+Noise(i.a.xy*5.1)*0.5;m=smoothstep(0.95,0.8,r+s*0.32);c=rgb*(Lit()*0.8);}         // A puddle.
                    float a=saturate(m*share);o=fixed4(c*a,a);
                }
                else if(kind<6.5)
                {
                    // A drifting tile: flat, lit from above, a dark edge for its grout.
                    float e=max(abs(i.a.x),abs(i.a.y));float grout=smoothstep(1.0,0.86,e);float light=0.55+0.45*saturate(n.y);o=fixed4(rgb*(Lit()+0.05)*light*(0.6+0.4*grout),1.0);
                }
                else if(kind<7.5)
                {
                    // A thin line on the water: mist tracking the sweep's start.
                    float s=Noise(float2(i.a.x*7.0-t*2.0,seed));float g=saturate(1.0-abs(i.a.y));g=g*g*(0.5+0.5*s);o=fixed4(rgb*g,0.0);
                }
                else if(kind>8.5)
                {
                    // A curtain of water across an arch: u across, v height. Streaks running down fast, deep teal behind them, bright where they catch the room.
                    float across=i.a.x,up=i.a.y;float s1=Noise(float2(across*7.0,up*0.8+t*3.1));float s2=Noise(float2(across*19.0+3.0,up*1.5+t*4.6));float streak=s1*0.6+s2*0.4;float nv=abs(dot(n,v));
                    float a=saturate(share*(0.82+0.2*streak));float3 c=float3(0.05,0.28,0.31)*(_DeepLight.y*0.9+0.1)+rgb*Lit()*pow(streak,2.4)*1.3+tex2D(_DeepGlowTex,float2(0.5,0.5)).rgb*0.08;o=fixed4(c*a,a);
                }
                else
                {
                    // A round light set in a surface: a lens (seed 1: with prism rings), a chrome ring round it, nothing past that.
                    float r=length(i.a.xy);if(r>1.0)discard;float lens=smoothstep(0.86,0.8,r);float ring=1.0-lens;float rings=seed>0.5?0.82+0.18*smoothstep(-0.2,0.6,sin(r*26.0)):1.0;float3 c=rgb*lens*rings+float3(0.46,0.5,0.52)*ring*(_DeepLight.y*0.6+0.1);o=fixed4(c,1.0);
                }
                UNITY_APPLY_FOG_COLOR(i.fogCoord,o,unity_FogColor*o.a);return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
