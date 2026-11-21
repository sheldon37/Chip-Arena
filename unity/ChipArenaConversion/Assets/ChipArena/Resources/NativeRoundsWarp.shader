Shader "ChipArena/NativeRoundsWarp"
{
    // The ROUNDS packs' space distortion (Claude, 2026-10-19; the slow-time fields should "visibly distort"): Core
    // NativeRoundsFields builds the authority sphere (clipped at the floor) of every slow-time, gravity and repulsion field into one
    // dynamic mesh a frame; Runtime NativeRoundsFxPresentation draws it after the opaque world with this material. One named
    // GrabPass copies the screen once a frame and the sphere samples it bent: a lens bulge strongest toward its silhouette, slow
    // ripples running through it at the slowed pace (slow time), a swirl drawn in to its centre (gravity) or rings pushed out
    // (repulsion), a cool desaturated tint inside slow time; fading in at the birth and out after the expiry. Depth-tested, no
    // depth write, no blending, both faces. The low graphics tier draws none (no grab at all). Per vertex as NativeRoundsField
    // (Core RoundsFieldVertex): A the offset from the centre (m) and seed, B style, variant, age, life, C layer, the floor's offset
    // from the centre, the radius, strength.
    Properties {}
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_RoundsWarpGrab" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _RoundsWarpGrab;
            // SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
            // The screen offset (uv units) at a point of the sphere: n its outward normal, v the view vector (both in view space,
            // x right and y up), L its offset from the centre (m), style, age, life, radius R, strength.
            float2 RwOffset(float3 n,float3 v,float3 L,float style,float age,float life,float R,float st)
            {
                float post=age-life;float on=smoothstep(0.0,0.2,age)*(post>0.0?1.0-smoothstep(0.0,0.45,post):1.0)*st;
                float facing=abs(dot(n,v));float lens=(1.0-facing)*(1.0-facing);float2 o=-n.xy*lens*0.018;
                float slow=abs(style-3.0)<0.5?1.0:0.0;float pull=abs(style-5.0)<0.5?1.0:0.0;float push=abs(style-4.0)<0.5?1.0:0.0;
                float rr=length(L)/max(R,0.01);
                o+=float2(sin(L.y*3.1+age*0.9),cos(L.x*2.7-age*0.7))*0.006*slow;
                o+=float2(-n.y,n.x)*0.02*pull*(1.0-rr)+n.xy*0.012*pull;
                o+=n.xy*0.01*push*sin((rr*4.0-age*2.2)*6.2831853);
                return o*on;
            }
            float3 RwTint(float3 c,float style,float age,float life,float st)
            {
                float post=age-life;float on=smoothstep(0.0,0.2,age)*(post>0.0?1.0-smoothstep(0.0,0.45,post):1.0)*st;
                float l=dot(c,float3(0.2126,0.7152,0.0722));float slow=abs(style-3.0)<0.5?1.0:0.0;
                return lerp(c,lerp(float3(l,l,l),c,0.75)*float3(0.94,0.98,1.06),slow*on);
            }
            // SHARED END
            struct appdata_rw {float4 vertex:POSITION;float3 normal:NORMAL;float4 a:TEXCOORD0;float4 b:TEXCOORD1;float4 c:TEXCOORD2;};
            struct v2f {float4 pos:SV_POSITION;float4 grab:TEXCOORD0;float3 nv:TEXCOORD1;float3 vv:TEXCOORD2;float4 a:TEXCOORD3;float4 b:TEXCOORD4;float4 c:TEXCOORD5;};
            v2f vert(appdata_rw v)
            {
                v2f o;float4 w=mul(unity_ObjectToWorld,v.vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.grab=ComputeGrabScreenPos(o.pos);
                float3 n=normalize(v.a.xyz);o.nv=mul((float3x3)UNITY_MATRIX_V,n);o.vv=mul((float3x3)UNITY_MATRIX_V,normalize(_WorldSpaceCameraPos-w.xyz));o.a=v.a;o.b=v.b;o.c=v.c;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                if(i.a.y<i.c.y)discard;
                float2 uv=i.grab.xy/i.grab.w+RwOffset(normalize(i.nv),normalize(i.vv),i.a.xyz,i.b.x,i.b.z,i.b.w,i.c.z,i.c.w);
                float3 c=tex2D(_RoundsWarpGrab,uv).rgb;return fixed4(RwTint(c,i.b.x,i.b.z,i.b.w,i.c.w),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
