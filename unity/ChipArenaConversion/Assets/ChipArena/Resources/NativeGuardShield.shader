// The held guard's shield (owner 2026-11-09: "change the block effect"; right click "a held block for melee"): a curved panel of light
// in front of a guarding pilot (Runtime NativeGuardShieldPresentation.cs draws it on the pilot's facing), additive: a hexagonal lattice
// whose cells breathe, a fresnel sheen, a bright edge, grown out from its middle as it goes up; each blocked blow a ring running out from
// where it struck and a flash there (up to four at once, _Hits: u, v, age, strength); a broken guard cracks along its cells, flares and
// falls apart (_Break: its age, negative while whole). Colour the pilot's accent (linear, HDR); _Strength its share (the first-person
// pilot's own fainter). Scene fog toward black, no depth write, both faces.
Shader "ChipArena/NativeGuardShield"
{
    Properties
    {
        _ShieldColor("Colour (linear, HDR)",Color)=(.4,.85,1,1)
        _ShieldAge("Seconds since it went up",Float)=1
        _ShieldFade("Fade",Float)=1
        _Break("Seconds since it broke (negative: whole)",Float)=-1
        _Strength("Share",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            float4 _ShieldColor;float _ShieldAge,_ShieldFade,_Break,_Strength;float4 _Hits[4];
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 wpos:TEXCOORD1;float3 wn:TEXCOORD2;UNITY_FOG_COORDS(3)};
            v2f vert(appdata_base v)
            {
                v2f o;o.wpos=mul(unity_ObjectToWorld,v.vertex).xyz;o.wn=UnityObjectToWorldNormal(v.normal);o.uv=v.texcoord.xy;o.pos=UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            // The distance to the nearest edge of a hexagonal lattice over p, and its cell's id.
            float hexEdge(float2 p,out float2 id)
            {
                const float2 s=float2(1,1.7320508);float2 ca=floor(p/s),cb=floor((p-s*.5)/s);float2 a=p-(ca+.5)*s,b=p-(cb+1)*s;
                bool first=dot(a,a)<dot(b,b);float2 g=first?a:b;id=first?ca:cb+.5;g=abs(g);return .5-max(dot(g,normalize(s)),g.x);
            }
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;float t=_Time.y;
                // The panel's edge: bright along it, the panel itself fading out toward it.
                float border=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));float edgeGlow=exp(-border*38);float body=smoothstep(0,.06,border);
                // Grown out from the middle as it goes up.
                float2 mid=(uv-.5)*float2(1.25,1);float grow=saturate(_ShieldAge/.14);float reach=length(mid)/.72;float grown=smoothstep(grow+.02,grow-.08,reach);
                float front=exp(-abs(reach-grow)*30)*(1-grow*.6)*step(_ShieldAge,.25);
                float2 id;float edge=hexEdge(uv*float2(15,9.5),id);float cell=smoothstep(.07,0,edge);float breathe=.5+.5*sin(t*5+hash(id)*6.283);
                float3 n=normalize(i.wn),view=normalize(_WorldSpaceCameraPos-i.wpos);float sheen=pow(1-abs(dot(n,view)),2);
                // Blows: a ring out from each and a flash there.
                float rings=0,flashes=0;
                for(int k=0;k<4;k++){float4 h=_Hits[k];if(h.z<0||h.w<=0)continue;float d=distance(uv*float2(1.25,1),h.xy*float2(1.25,1)),r=h.z*1.6;rings+=exp(-abs(d-r)*40)*exp(-h.z*3.5)*h.w;flashes+=exp(-d*14)*exp(-h.z*8)*h.w*2;}
                // Broken: the cells' edges flare and the panel falls apart, cell by cell.
                float shatter=1,crack=0;if(_Break>=0){float p=saturate(_Break/.4);crack=smoothstep(.12,0,edge)*(1-p)*2.5;shatter=step(p,hash(id)*.9+.05)*(1-p*.5);}
                float glow=.035*body+cell*.22*breathe*body+sheen*.3*body+edgeGlow*.65+front*1.8+rings*2.2+flashes+crack;
                float3 c=_ShieldColor.rgb*glow*grown*shatter*_ShieldFade*_Strength;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));
                return float4(c,0);
            }
            ENDCG
        }
    }
}
