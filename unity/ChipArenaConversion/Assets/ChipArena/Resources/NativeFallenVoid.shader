Shader "ChipArena/NativeFallenVoid"
{
    // NOCTIEL's void (docs/EXPEDITION-BOSS-FINAL.md; the owner, 2026-11-08: "Have the background (The void area) react with the music as well"). A sphere round the eye,
    // drawn first: black, but for the song (_FbSong, the file's own measure: r its level, g its bass). A low glow on the horizon breathes with the level; curtains of light
    // fall from high above, faint, flaring with the bass, and the bass climbs the sky as it plays (what's heard at a height is the bass of a moment ago); each slam
    // (_FbVoidSlam, seconds since it) flashes the horizon and sends a band of light up the sky. White light only. The Still Water mirrors all of it.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "IgnoreProjector"="True" "PreviewType"="Skybox" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _FbSong;float4 _FbSongAt;float _FbClock,_FbVoidSlam,_FbVoidDim;
            struct v2f{float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
            v2f vert(float4 v:POSITION){v2f o;o.pos=UnityObjectToClipPos(v);o.dir=v.xyz;return o;}
            float2 Song(float at){return _FbSongAt.z>0?tex2Dlod(_FbSong,float4(at*_FbSongAt.y,.5,0,0)).rg:float2(0,0);}
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir);float y=d.y;float a=atan2(d.z,d.x);float t=_FbClock;
                float2 now=Song(_FbSongAt.x);float level=now.x;
                // The horizon's glow, with the level.
                float horizon=exp(-abs(y)*13.)*(.012+.06*level*level);
                // Curtains of light falling from above: thin, drifting, brighter where the bass is (the bass at a height is the bass of a moment ago, so a beat climbs).
                float wander=sin(a*2.3+t*.041)*2.2+sin(a*5.1-t*.027)*.6;
                float sheet=pow(.5+.5*sin(a*11.+wander),16.)+.45*pow(.5+.5*sin(a*23.-t*.033+1.7),28.);
                float up=max(y,0);float heard=Song(_FbSongAt.x-up*1.6).y;heard*=heard;
                float rise=smoothstep(-.03,.08,y)*exp(-up*1.7);
                float curtains=sheet*rise*(.0025+.17*heard+.012*level);
                // A slam: the horizon flashes and a band of light runs up the sky.
                float s=_FbVoidSlam;float slam=0;
                if(s>=0&&s<2.4){slam=exp(-s*3.2)*.09*exp(-abs(y)*5.)+exp(-(up-s*.55)*(up-s*.55)*70.)*.07*exp(-s*1.3)*smoothstep(-.02,.05,y);}
                float c=(horizon+curtains+slam)*_FbVoidDim;
                return float4(c,c,c*1.03,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
