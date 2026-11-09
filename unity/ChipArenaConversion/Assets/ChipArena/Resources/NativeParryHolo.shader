// The parry's holographic barrier (owner 2026-11-09: "Lets cover the users player model in a holographic barrier effect when the parry
// is active"): a copy of each of the body's skinned pieces drawn over it while the parry's window is open (Runtime
// NativeParryHoloPresentation.cs), pushed a little out along its normals, additive: a faint fill, a bright fresnel rim, fine scanlines
// rising up the body, a hexagonal lattice that shimmers, a sweep of light up from the feet as it comes up, a ripple out from where a
// parried blow lands and a flash with it, and a flicker; fading in and out with the window. Colour the pilot's accent (linear, HDR).
// Scene fog toward black, depth tested against the body itself (no depth write), Transparent+2.
Shader "ChipArena/NativeParryHolo"
{
    Properties
    {
        _HoloColor("Colour (linear, HDR)",Color)=(.3,.9,1,1)
        _HoloFade("Fade",Float)=1
        _HoloAge("Seconds since it went up",Float)=0
        _HoloFlash("Seconds since a parried blow (negative: none)",Float)=-1
        _HoloHit("Where that blow landed (world)",Vector)=(0,0,0,0)
        _HoloSpan("The body's feet and top (world y)",Vector)=(0,1.9,0,0)
        _HoloPush("Push out along the normals (m)",Float)=.014
    }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            float4 _HoloColor,_HoloHit,_HoloSpan;float _HoloFade,_HoloAge,_HoloFlash,_HoloPush;
            struct v2f{float4 pos:SV_POSITION;float3 wpos:TEXCOORD0;float3 wn:TEXCOORD1;UNITY_FOG_COORDS(2)};
            v2f vert(appdata_base v)
            {
                v2f o;float3 wn=normalize(UnityObjectToWorldNormal(v.normal));float3 wp=mul(unity_ObjectToWorld,v.vertex).xyz;
                // A little fuller as it comes up, settling to its push.
                float swell=1+.8*saturate(1-_HoloAge/.14);wp+=wn*_HoloPush*swell;
                o.wpos=wp;o.wn=wn;o.pos=UnityWorldToClipPos(wp);UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            // The distance to the nearest edge of a hexagonal lattice over (u, v).
            float hexEdge(float2 p)
            {
                const float2 s=float2(1,1.7320508);float2 a=fmod(abs(p),s)-s*.5,b=fmod(abs(p-s*.5),s)-s*.5;float2 g=dot(a,a)<dot(b,b)?a:b;
                g=abs(g);return .5-max(dot(g,normalize(s)),g.x);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.wn),view=normalize(_WorldSpaceCameraPos-i.wpos);float facing=saturate(dot(n,view));
                float rim=pow(1-facing,2.4);
                float t=_Time.y;float span=max(.2,_HoloSpan.y-_HoloSpan.x);float h=saturate((i.wpos.y-_HoloSpan.x)/span);
                // Scanlines climbing the body.
                float s=frac(i.wpos.y*42-t*1.6);float lines=pow(1-abs(s*2-1),14);
                // The lattice, on the side the body faces most (a cheap triplanar pick), its cells breathing.
                float3 an=abs(n);float2 uv=an.y>max(an.x,an.z)?i.wpos.xz:an.x>an.z?i.wpos.zy:i.wpos.xy;uv*=14;
                float edge=hexEdge(uv);float cell=smoothstep(.06,.0,edge);
                float2 id=floor(uv*.5);float breathe=.55+.45*sin(t*6+hash(id)*6.283);
                // The sweep up from the feet as it comes up.
                float sweepAt=saturate(_HoloAge/.22);float sweep=exp(-abs(h-sweepAt)*span*9)*(1-sweepAt*.4)*step(_HoloAge,.3);
                // A parried blow: a ring running out from where it landed, and a flash.
                float flash=0,ring=0;if(_HoloFlash>=0){float d=distance(i.wpos,_HoloHit.xyz),r=_HoloFlash*3.2;ring=exp(-abs(d-r)*22)*exp(-_HoloFlash*4);flash=exp(-_HoloFlash*9);}
                float flicker=.9+.1*sin(t*53+h*7)*sin(t*31);
                float glow=.05+rim*1.35+lines*.28*(.4+rim)+cell*.42*breathe*(.35+rim)+sweep*2.2+ring*2.6+flash*(.6+rim*1.4);
                float3 c=_HoloColor.rgb*glow*flicker*_HoloFade;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));
                return float4(c,0);
            }
            ENDCG
        }
    }
}
