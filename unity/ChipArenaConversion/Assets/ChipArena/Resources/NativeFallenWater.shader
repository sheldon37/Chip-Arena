Shader "ChipArena/NativeFallenWater"
{
    // The Still Water (docs/EXPEDITION-BOSS-FINAL.md section 5): a black mirror. It reflects what's mirrored below it (the frame so far, GrabPass, bent by the water: the void and its
    // music, Resources/NativeFallenVoid.shader). What touches it rings it (_FbRing): each a packet of ripples like a step in shallow water, a steep front with a train of smaller
    // ripples inside it, their wavelength growing as they spread and fade, bending the reflection and catching the light on their slopes (glints, not a line round the ring).
    // The waves of the moves (_FbWave) cross the disc. Eclipse's dim and pools, a strike's white. Past the edge, nothing.
    // Since 2026-11-08 the song's bass no longer rings the floor from the boss (the owner: "more water-like like were stepping in water, not just random ripples"): the void
    // carries the music. Since 2026-11-08 again (the owner: "The ripples are too excessive I think maybe lower the opacity but a LOT (You know, kind of
    // similar color but you can tell theirs small ripples.)") the rings are faint: a sixth of their light, a third of the glints they add, under half
    // their slope, so they read in the water's own dark colour, the reflection bending a little where they pass. The moves' waves are as they were.
    // Output is opaque: the disc is the only thing down here.
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
            // The rings' share: their light, their slope (the glints and the bent reflection), the glints their slope still makes.
            #define RING_LIGHT .16
            #define RING_SLOPE .42
            #define RING_GLINT .33
            float Glint(float2 grad,float3 hv){float3 nrm=normalize(float3(-grad.x*2.2,1,-grad.y*2.2));return max(0,pow(saturate(dot(nrm,hv)),90.)-pow(saturate(hv.y),90.));}
            float Web(float2 q,float t)
            {
                float2 a=q*.32+float2(sin(q.y*.21+t*.31),sin(q.x*.19-t*.27))*1.4;float2 b=q*.61+float2(sin(q.y*.43-t*.23),sin(q.x*.37+t*.19))*1.1;
                float s=abs(sin(a.x)+sin(a.y));float u=abs(sin(b.x+1.3)+sin(b.y+.7));return exp(-s*9.)+.5*exp(-u*12.);
            }
            float4 frag(v2f i):SV_Target
            {
                float2 q=i.world.xz-_FbStage.xz;float3 toEye=_WorldSpaceCameraPos-i.world;float dist=length(toEye);float3 v=toEye/dist;float t=_FbClock;
                float2 grad=0,rings=0;float lit=0,ringLit=0;
                for(int k=0;k<(int)_FbRingCount;k++)
                {
                    // A packet: steep ahead of its front, a long train of smaller ripples behind it; the wavelength grows as it spreads, the height falls.
                    float4 r=_FbRing[k];float2 d=q-r.xy;float len=length(d)+.001;float x=len-r.z;float spread=1.+r.z*.14;float kk=10./spread;
                    float env=(x>0?exp(-x*x*8.):exp(-x*x*.9/spread))*r.w*rsqrt(1.+r.z*.3);
                    float ph=x*kk;rings+=d/len*cos(ph)*env*kk*.06;ringLit+=env*.12*pow(saturate(sin(ph+1.2)),3.);
                }
                for(int k=0;k<(int)_FbWaveCount;k++)
                {
                    float4 r=_FbWave[k];float2 d=q-r.xy;float len=length(d)+.001;float x=len-r.z;float env=exp(-x*x*.22)*r.w;
                    float ph=x*3.4;grad+=d/len*cos(ph)*env*.42;float crest=saturate(sin(ph+1.57));crest*=crest;lit+=env*(crest*crest*.42+.4*exp(-x*x*14.));
                }
                // The ripples' slopes catch the light: a glint where a slope turns the light toward the eye (the flat water's own highlight taken off, so
                // only what moves shows), fading with distance.
                // The waves' glints as they were; what the rings add to them, a third of it; the rings' own light, a sixth.
                float3 lgt=normalize(float3(.35,.8,.45));float3 hv=normalize(lgt+v);float waves=Glint(grad,hv);float2 all=grad+rings*RING_SLOPE;
                float glint=waves+max(0,Glint(all,hv)-waves)*RING_GLINT;lit+=glint*1.6*exp(-dist*.012)+ringLit*RING_LIGHT;grad=all;
                // (The song's web of light left the floor with its bass, 2026-11-08: it read as random ripples. The void carries the music.)
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
