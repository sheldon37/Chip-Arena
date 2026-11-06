Shader "ChipArena/NativeTelegraph"
{
    // Attack warnings on the ground for every boss and enemy (Core NativeTelegraph; docs/TELEGRAPHS.md). One quad per warning, its
    // shape computed here: a dark film for contrast, the fill closing in on the hit with its bright front, the style's pattern, a
    // crisp edge never thinner than a pixel and a half, a glow that pulses faster as the hit nears, the lock-on strobe in the last
    // moments, the impact flash and its shock ring, and a live hazard's steady burn. Premultiplied: light adds, the film darkens.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Offset -1,-1 Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float _TgClock;
            struct a2v{float4 vertex:POSITION;float4 l:TEXCOORD0;float4 s:TEXCOORD1;float4 t:TEXCOORD2;float4 k:TEXCOORD3;};
            struct v2f{float4 pos:SV_POSITION;float4 l:TEXCOORD0;float4 s:TEXCOORD1;float4 t:TEXCOORD2;float4 k:TEXCOORD3;};
            v2f vert(a2v v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.l=v.l;o.s=v.s;o.t=v.t;o.k=v.k;return o;}

            float Box(float2 p,float2 h){float2 d=abs(p)-h;return length(max(d,0))+min(max(d.x,d.y),0);}
            // A wedge facing +y (Inigo Quilez's sdPie), its half angle in radians.
            float Pie(float2 p,float ha,float r){if(ha>=3.13)return length(p)-r;float2 c=float2(sin(ha),cos(ha));p.x=abs(p.x);float l=length(p)-r;float m=length(p-c*clamp(dot(p,c),0,r));return max(l,m*sign(c.y*p.x-c.x*p.y));}
            float Shape(int shape,float2 p,float A,float B,float C)
            {
                if(shape==0)return length(p)-A;
                if(shape==1)return abs(length(p)-(A+B)*.5)-(A-B)*.5;
                if(shape==2){float d=Pie(p,radians(B),A);return C>0?max(d,C-length(p)):d;}
                if(shape==3)return Box(p-float2(0,(A-C)*.5),float2(B,(A+C)*.5));
                if(shape==4)return Box(p,float2(A,A));
                return max(abs(length(p)-A)-B,Pie(p,radians(C),1e4));
            }
            // Where the fill starts (0) and where it ends (1): in from the edge, out from the centre, or along the attack.
            float Fill(int shape,int mode,float2 p,float A,float B,float C)
            {
                float r=length(p);
                if(shape==3)return mode==2?saturate((p.y+C)/max(A+C,.01)):saturate(1-abs(p.x)/max(B,.01));
                if(shape==4){float m=max(abs(p.x),abs(p.y))/max(A,.01);return mode==1?saturate(m):saturate(1-m);}
                if(shape==1)return saturate((A-r)/max(A-B,.01));
                if(mode==1||mode==2)return saturate(r/max(A,.01));
                return saturate(1-r/max(A,.01));
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            // The style's colour and its film's darkness (Core NativeTelegraph's numbers).
            float3 Tint(int style){
                if(style==1||style==2)return float3(1,1,1);if(style==3)return float3(.82,.94,1);if(style==4)return float3(1,.68,.12);if(style==5)return float3(1,.4,.07);
                if(style==6)return float3(.86,1,.5);if(style==7)return float3(.32,.88,1);if(style==8)return float3(1,.24,.74);return float3(1,.3,.1);}
            // The style's pattern inside the shape (0..1), moving with the fill toward the hit.
            float Pattern(int style,int shape,float2 p,float f,float t,float seed)
            {
                float r=length(p),ang=atan2(p.x,p.y);
                if(style==1||style==2)
                {
                    // Engraved rings drifting in, a dial of ticks; the kill style adds spokes and a darker heart.
                    float rings=smoothstep(.86,1,frac(r*1.1-t*.35))+smoothstep(.9,1,frac(f*6-t*.6))*.5;
                    float spokes=style==2?smoothstep(.93,1,frac(ang/6.2832*36+t*.05))*saturate(r*.2):0;
                    return saturate(rings*.6+spokes);
                }
                if(style==3){return .5+.5*sin(r*2.2+t*2.4);}
                if(style==4){float s=frac((p.x+p.y)*.42-t*.9);return step(.5,s);}
                if(style==5){float n=Noise(p*.9+float2(0,-t*1.6))*Noise(p*2.3-t*.7);return saturate(n*2.2);}
                if(style==6){float flick=step(.18,Hash(float2(floor(t*13),seed)));float grid=max(smoothstep(.93,1,frac(p.x*.5)),smoothstep(.93,1,frac(p.y*.5)));return flick*(.35+.65*grid);}
                if(style==7){float c=sin(p.x*1.3+sin(p.y*1.1+t*1.4)*1.6+t)+sin(p.y*1.5+sin(p.x*.9-t*1.2)*1.7-t*.8);return saturate(pow(abs(c)*.5,3)*3);}
                // Chevrons running toward the hit.
                float v=shape==3?p.y-abs(p.x)*.7:(shape==0||shape==4?-r:r);return smoothstep(.55,.62,frac(v*.45-t*1.3));
            }

            float4 frag(v2f i):SV_Target
            {
                float2 p=i.l.xy;int shape=(int)(i.l.z+.5),style=(int)(i.l.w+.5),mode=(int)(i.s.w+.5);float A=i.s.x,B=i.s.y,C=i.s.z;
                float fill=i.t.x,lockOn=i.t.y,after=i.t.z,live=i.t.w,strength=i.k.x,seed=i.k.y;float t=_TgClock;
                float d=Shape(shape,p,A,B,C);float px=max(fwidth(d),1e-4);
                float inside=saturate(.5-d/px);
                float f=Fill(shape,mode,p,A,B,C);
                bool wave=shape==5;if(wave){fill=1;live=max(live,1);}
                float filled=inside*smoothstep(fill+.012,fill-.012,f);
                float front=inside*exp(-abs(f-fill)*34)*step(fill,.995)*step(.01,fill);
                // The edge: crisp at any distance; a glow outside it and a softer one inside.
                float rimW=max(.1+.08*lockOn,px*1.6);float rim=saturate(1-abs(d)/rimW);
                float halo=d>0?exp(-d/.6):0;float innerGlow=inside*exp(d/1.3);
                // A heartbeat that quickens as the hit nears; a strobe in the lock-on; the impact's flash and shock ring.
                float rate=lerp(1.1,7.5,fill*fill);float pulse=.62+.38*sin(t*6.2832*rate+seed*3.1);
                float strobe=lockOn>0?.5+.5*sin(t*92):0;
                float post=after>=0?1:0;float flash=post*exp(-after*8.5);float shock=post*exp(-abs(d-after*7)/.3)*exp(-after*5.5);
                float pat=Pattern(style,shape,p,f,t,seed);
                float3 tint=Tint(style);float3 white=float3(1,1,1);
                bool fallen=style==1||style==2;float darkness=style==2?.62:fallen?.42:style==3?.08:.3;
                // The film darkens what is under the warning (a white warning reads on a bright floor), deeper as it fills.
                float alpha=inside*darkness*(.42+.58*filled)*(1-flash*.6);
                float3 light=tint*(filled*.26+front*1.35+pat*inside*(.16+.42*filled)+innerGlow*.22);
                light+=lerp(tint,white,.45)*(rim*(.85+.9*lockOn)*pulse+halo*.4*pulse);
                light+=tint*strobe*lockOn*inside*.55;
                light+=white*(flash*inside*1.1+shock*1.6);
                light+=tint*live*inside*(.3+.55*pat)+lerp(tint,white,.5)*live*rim*.5;
                if(style==3){alpha=inside*.05;light=tint*(innerGlow*.55+rim*pulse*1.1+halo*.5+pat*inside*.12*fill);}
                // Gone after its flash unless it is still live.
                float fade=post>0?saturate(max(live,1-(after-.22)/.13)):1;
                return float4(light*strength*fade,saturate(alpha*strength*fade));
            }
            ENDCG
        }
    }
}
