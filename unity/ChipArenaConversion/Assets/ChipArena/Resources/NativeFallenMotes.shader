Shader "ChipArena/NativeFallenMotes"
{
    // The feathers drifting through the dark (docs/EXPEDITION-BOSS-FINAL.md section 5): one mesh of quads placed here from the clock alone, in a box wrapped round the eye, each turning as it falls, each catching the light with the song's level.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float _FbClock,_FbMoteCount,_FbMoteLight;float4 _FbStage;
            struct a2v{float4 vertex:POSITION;float4 uv:TEXCOORD0;};
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float k:TEXCOORD1;};
            v2f vert(a2v v)
            {
                v2f o;float seed=v.uv.z;float index=v.uv.w;float2 corner=v.uv.xy;const float box=130.,high=44.;
                if(index>_FbMoteCount){o.pos=float4(0,0,-2,1);o.uv=0;o.k=0;return o;}
                float s1=frac(seed*12.9898),s2=frac(seed*78.233),s3=frac(seed*37.719);float fall=.55+.75*s1;float t=_FbClock;
                float y=fmod(s2*high*7.-t*fall,high);if(y<0)y+=high;
                float3 eye=_WorldSpaceCameraPos;float2 base=float2(s3,frac(seed*93.989))*box;float2 sway=float2(sin(t*.7*fall+seed*40.),cos(t*.53*fall+seed*17.))*1.3;
                float2 at=fmod(base+sway-eye.xz+box*16.,box)-box*.5+eye.xz;float3 world=float3(at.x,_FbStage.y+y,at.y);
                float turn=t*(1.2+s1*1.6)+seed*90.;float3 toEye=normalize(eye-world);float3 r=normalize(cross(float3(0,1,0),toEye)),u=normalize(cross(toEye,r));
                float ct=cos(turn),st=sin(turn);float3 ax=r*ct+u*st,ay=-r*st+u*ct;float size=.05+.09*s1;
                world+=ax*(corner.x*size)+ay*(corner.y*size*2.4);
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1));o.uv=corner;float d=distance(world,eye);
                o.k=(.35+.65*abs(sin(turn*.5)))*_FbMoteLight*saturate(d/1.5)*exp(-d*.024)*smoothstep(0.,.5,y);return o;
            }
            float4 frag(v2f i):SV_Target{float prof=1-i.uv.y*i.uv.y;float k=smoothstep(prof*.38,prof*.1,abs(i.uv.x));return float4(k.xxx*i.k,0);}
            ENDCG
        }
    }
    Fallback Off
}
