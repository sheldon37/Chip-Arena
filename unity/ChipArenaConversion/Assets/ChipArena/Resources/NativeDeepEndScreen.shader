Shader "ChipArena/NativeDeepEndScreen"
{
    // THE DEEP END's two moments the whole screen takes part in (docs/EXPEDITION-BOSS-BACKROOMS.md section 7): the Lull (the view softens: colour blooms, the edges drift toward the water's colour, as the water soothes you, the warning before the hands) and the wave over the
    // camera at the arrival's end (a line of water sweeping up the frame, the picture bending and going teal under it, clearing as the fight begins). One triangle over the screen, after everything but the HUD; one named GrabPass. Not drawn at all when neither is on.
    Properties {}
    SubShader
    {
        Tags { "Queue"="Overlay+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_DeepScreenGrab" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _DeepScreenGrab;float4 _DeepScreenGrab_TexelSize;float _DeepSoftAmount,_DeepWashAmount,_DeepScreenClock;
            struct v2f {float4 pos:SV_POSITION;float4 grab:TEXCOORD0;float2 uv:TEXCOORD1;};
            v2f vert(float4 vertex:POSITION,float2 uv:TEXCOORD0){v2f o;float2 p=uv*2.0-1.0;o.pos=float4(p.x,p.y,UNITY_NEAR_CLIP_VALUE,1.0);o.grab=ComputeGrabScreenPos(o.pos);o.uv=uv;return o;}
            float Hash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
            float Noise(float2 p){float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);return lerp(lerp(Hash(i),Hash(i+float2(1.0,0.0)),u.x),lerp(Hash(i+float2(0.0,1.0)),Hash(i+float2(1.0,1.0)),u.x),u.y);}
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.grab.xy/i.grab.w;float t=_DeepScreenClock;float soft=_DeepSoftAmount,wash=_DeepWashAmount;
                // The wave's edge rises from the bottom of the frame, and clears down it.
                float line_=wash*1.6-0.3+0.05*sin(i.uv.x*11.0+t*7.0)+0.03*sin(i.uv.x*27.0-t*11.0);float under=smoothstep(line_+0.03,line_-0.03,i.uv.y);
                float2 wob=float2(Noise(i.uv*7.0+float2(0.0,t*2.2))-0.5,Noise(i.uv*7.0+float2(5.0,t*2.6))-0.5)*0.09*under*saturate(wash*2.0);
                float2 suv=uv+wob;
                // The soft blur: a ring of taps, wider toward the edges.
                float3 c=tex2D(_DeepScreenGrab,suv).rgb;float2 fromMid=i.uv-0.5;float vig=dot(fromMid,fromMid);
                float radius=soft*(0.0025+0.012*vig*4.0)*0.9+under*saturate(wash*2.0)*0.012;
                if(radius>0.00005)
                {
                    float3 sum=c;for(int k=0;k<8;k++){float a=6.2831853*(float(k)+0.5)/8.0;sum+=tex2D(_DeepScreenGrab,suv+float2(cos(a),sin(a))*radius*float2(_ScreenParams.y/_ScreenParams.x,1.0)).rgb;sum+=tex2D(_DeepScreenGrab,suv+float2(cos(a+0.39),sin(a+0.39))*radius*1.9*float2(_ScreenParams.y/_ScreenParams.x,1.0)).rgb;}c=sum/17.0;
                    // Colour blooms; the whole picture drifts toward the warm blue-green of the water.
                    float l=dot(c,float3(0.3,0.5,0.2));c=lerp(c,float3(l,l,l)*float3(0.84,1.08,1.08)+max(c-0.5,0.0)*0.8,soft*0.65);c=lerp(c,float3(0.48,0.78,0.78),soft*(0.08+vig*0.8));
                }
                if(wash>0.0)
                {
                    float3 tint=float3(0.06,0.32,0.36);float l=dot(c,float3(0.3,0.5,0.2));c=lerp(c,tint*(0.5+l*1.1),under*0.86);
                    // The white line of the water's edge, and bubbles under it.
                    float edge=exp(-abs(i.uv.y-line_)*130.0)*saturate(wash*3.0)*smoothstep(0.0,0.1,1.0-wash);c+=float3(0.7,0.9,0.92)*edge*0.3;
                    float bub=smoothstep(0.92,0.98,Noise(i.uv*float2(38.0,22.0)+float2(0.0,-t*4.0)))*under;c+=bub*float3(0.5,0.75,0.8)*0.5;
                }
                return fixed4(c,1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
