Shader "ChipArena/NativeFallenWater"
{
    // The Still Water (docs/EXPEDITION-BOSS-FINAL.md section 5): a black mirror, the song on it. It reflects what's mirrored below it (the frame so far, GrabPass, bent by the water). Footsteps' rings (_FbRing) and the waves that cross the disc (_FbWave) catch a thin white
    // light on their crests and bend the reflection. The song runs out from the boss: the rings on the floor are the bass as it was r/34 s ago (_FbSong, the file's own measure), so the floor is the music's waveform moving outward, and goes dark when the bass stops.
    // A faint web of light moves with the song's level. Eclipse's dim and pools, a strike's white. Past the edge, nothing. Output is opaque: the disc is the only thing down here.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent-20" "IgnoreProjector"="True" }
        GrabPass { "_FbGrab" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #include "UnityCG.cginc"
            sampler2D _FbGrab,_FbSong;
            float4 _FbRing[48],_FbWave[10],_FbPool[6];float4 _FbBoss,_FbSongAt,_FbStage;
            float _FbRingCount,_FbWaveCount,_FbPoolCount,_FbDim,_FbStrike,_FbClock;
            struct v2f{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 grab:TEXCOORD1;};
            v2f vert(float4 v:POSITION){v2f o;float4 w=mul(unity_ObjectToWorld,v);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;o.grab=ComputeGrabScreenPos(o.pos);return o;}
            float Web(float2 q,float t)
            {
                float2 a=q*.32+float2(sin(q.y*.21+t*.31),sin(q.x*.19-t*.27))*1.4;float2 b=q*.61+float2(sin(q.y*.43-t*.23),sin(q.x*.37+t*.19))*1.1;
                float s=abs(sin(a.x)+sin(a.y));float u=abs(sin(b.x+1.3)+sin(b.y+.7));return exp(-s*9.)+.5*exp(-u*12.);
            }
            float4 frag(v2f i):SV_Target
            {
                float2 q=i.world.xz-_FbStage.xz;float3 toEye=_WorldSpaceCameraPos-i.world;float dist=length(toEye);float3 v=toEye/dist;float t=_FbClock;
                float2 grad=0;float lit=0;
                for(int k=0;k<(int)_FbRingCount;k++)
                {
                    float4 r=_FbRing[k];float2 d=q-r.xy;float len=length(d)+.001;float x=len-r.z;float env=exp(-x*x*(3.2-min(1.6,r.z*.18)))*r.w;
                    float ph=x*(11.-min(5.,r.z*.8));grad+=d/len*cos(ph)*env*.5;float crest=saturate(sin(ph+1.57));crest*=crest;lit+=env*crest*crest*.8;
                }
                for(int k=0;k<(int)_FbWaveCount;k++)
                {
                    float4 r=_FbWave[k];float2 d=q-r.xy;float len=length(d)+.001;float x=len-r.z;float env=exp(-x*x*.22)*r.w;
                    float ph=x*3.4;grad+=d/len*cos(ph)*env*.42;float crest=saturate(sin(ph+1.57));crest*=crest;lit+=env*(crest*crest*.42+.4*exp(-x*x*14.));
                }
                // The bass, moving out from the boss.
                float2 fromBoss=q-_FbBoss.xy;float br=length(fromBoss)+.001;float2 dir=fromBoss/br;float wob=sin(atan2(fromBoss.y,fromBoss.x)*3.+t*.31)*.26+sin(atan2(fromBoss.y,fromBoss.x)*7.-t*.23)*.1;
                float heard=_FbSongAt.x-br/34.;float bass=tex2Dlod(_FbSong,float4(heard*_FbSongAt.y,.5,0,0)).g;bass=bass*bass*bass;
                float ph2=(br+wob*min(1.,br*.08))*1.85-t*1.4;float lines=pow(.5+.5*sin(ph2),22.);float reach=exp(-br*.024);
                // Far off, where the lines are finer than a pixel, their mean; and thin at a grazing eye, where they'd stack into bars.
                float fine=saturate(fwidth(br*1.85)*.55);lines=lerp(lines,.09,fine);float graze=.3+.7*smoothstep(.02,.2,v.y);
                lit+=bass*lines*reach*.3*graze;grad+=dir*cos(ph2)*bass*reach*.075*(1-fine);
                // The song's level in a faint moving web, more of it the louder it is.
                float level=tex2Dlod(_FbSong,float4(_FbSongAt.x*_FbSongAt.y,.5,0,0)).r;float web=Web(q,t*.9)*smoothstep(.42,.95,level);
                lit+=web*.055*exp(-dist*.016);
                // The reflection.
                float2 uv=i.grab.xy/i.grab.w+grad*float2(.055,.11)*saturate(14./(dist+6.));
                float3 refl=tex2D(_FbGrab,uv).rgb;float fres=.3+.56*pow(1-saturate(v.y),2.4);
                float inPool=0;for(int k=0;k<(int)_FbPoolCount;k++){float4 p=_FbPool[k];float dd=length(q-p.xy);inPool=max(inPool,p.w*smoothstep(p.z,p.z-.5,dd));}
                float dim=lerp(_FbDim,1,saturate(inPool));
                float fade=exp(-dist*.0075);float edge=smoothstep(92.,76.,length(q));
                float3 c=refl*fres*dim*edge;
                float grazeAll=.42+.58*smoothstep(.015,.17,v.y);lit=(1-exp(-lit*grazeAll*1.25))*1.25;c+=float3(1,1,1)*lit*fade*edge*dim;
                for(int k=0;k<(int)_FbPoolCount;k++){float4 p=_FbPool[k];float dd=length(q-p.xy);c+=float3(1,1,1)*p.w*(smoothstep(p.z+.06,p.z-.06,dd)*.13+exp(-(dd-p.z)*(dd-p.z)*9.)*1.5);}
                if(_FbStrike>0)c+=float3(1,1,1)*_FbStrike*(1-saturate(inPool))*2.2*edge*fade;
                return float4(c,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
