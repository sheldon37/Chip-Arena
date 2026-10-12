Shader "ChipArena/NativeRooftopSprite"
{
    // The rooftop scene's sprinkle of small effects (NativeRooftopPresentation): camera-facing quads expanded here from their
    // centre. uv0: corner xy, size, kind; uv1: drift velocity xyz and loop period (0: still); uv2: rgb (HDR) and alpha; uv3.x seed.
    // Kinds: 0 glow, 1 flat mist, 2 square mote (twinkles), 3 steam puff, 4 rain streak, 5 falling spark. Looping sprites rise
    // (or fall) along their drift over each period and fade in and out, so the sprinkle needs no CPU work once built.
    // _SrcBlend/_DstBlend: additive (One One) for light, alpha (SrcAlpha OneMinusSrcAlpha) for mist and steam.
    Properties { [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src",Float)=1 [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst",Float)=1 _Additive("Additive",Float)=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            float _Additive;
            struct appdata {float4 vertex:POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float4 uv2:TEXCOORD2;float4 uv3:TEXCOORD3;};
            struct v2f {float4 pos:SV_POSITION;float2 q:TEXCOORD0;float4 col:TEXCOORD1;float4 prm:TEXCOORD2;float3 world:TEXCOORD3;};
            v2f vert(appdata v)
            {
                v2f o;float3 p=mul(unity_ObjectToWorld,float4(v.vertex.xyz,1)).xyz;float kind=v.uv0.w,seed=v.uv3.x,fade=1.0;
                if(v.uv1.w>0.0)
                {
                    float ph=frac(seed*7.31+_RoofTime/v.uv1.w);p+=v.uv1.xyz*ph*v.uv1.w;fade=smoothstep(0.0,.12,ph)*smoothstep(1.0,.75,ph);
                    if(kind>4.5){p.y-=4.9*(ph*v.uv1.w)*(ph*v.uv1.w);fade*=1.0-ph;}
                }
                if(kind>1.5&&kind<2.5)fade*=.55+.45*sin(_RoofTime*2.3+seed*40.0);
                float3 right=UNITY_MATRIX_V[0].xyz,up=UNITY_MATRIX_V[1].xyz;float2 c=v.uv0.xy;float s=v.uv0.z;
                if(kind>3.5&&kind<4.5){up=normalize(float3(-.12,-1.0,0.0));right=normalize(cross(up,normalize(_WorldSpaceCameraPos-p)));c=float2(c.x*.04,c.y);}
                else if(kind>.5&&kind<1.5)c.y*=.28;
                float3 w=p+(right*c.x+up*c.y)*s;o.world=w;o.pos=mul(UNITY_MATRIX_VP,float4(w,1.0));
                o.q=v.uv0.xy*2.0;o.col=float4(v.uv2.rgb,v.uv2.a*fade);o.prm=float4(kind,seed,0,0);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 q=i.q;float r=length(q);float kind=i.prm.x,a;
                if(kind<.5||kind>4.5)a=exp(-r*r*5.0);
                else if(kind<1.5||(kind>2.5&&kind<3.5)){float n=RoofFbm(float3(q*1.4+i.prm.y*13.0,_RoofTime*.06+i.prm.y));a=smoothstep(1.0,.2,r+.5*(n-.5))*smoothstep(.15,.55,n+.25*(1.0-r));}
                else if(kind<2.5)a=step(max(abs(q.x),abs(q.y)),.7);
                else a=smoothstep(1.0,0.0,abs(q.x))*smoothstep(1.0,.3,abs(q.y));
                float z=length(i.world-_WorldSpaceCameraPos);float fog=exp(-z*.0012);
                if(_Additive>.5)return float4(i.col.rgb*a*i.col.a*fog,1.0);
                return float4(i.col.rgb,a*i.col.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
