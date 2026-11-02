Shader "ChipArena/NativeDeployScreen"
{
    // The Expedition Deployment Station's screens (Runtime/NativeDeployScene.cs draws Core NativeDeployStage's screen sections):
    //  - the reel and card strips: the strip texture sampled at uv.y x _Rep + _Scroll (wrapping, with the unwrapped gradient so
    //    the wrap never picks a blurry mip), smeared along v by _Blur while a reel spins fast;
    //  - the holo maps (_Holo): the blueprint picture's colours lifted by _Chroma, its fill dimmed, an edge glow from its own
    //    contrast (roads, the plaza ring, district borders), the blueprint's 25 x 19 cell grid, a frame with corner brackets
    //    and the scan sweep (_Sweep, -1 none); _Texel is one texel of the map;
    //  - the PENDING / ACTIVE stamps, the floor's light pools and the light columns (vertex alpha fades them).
    // HLSL reserves `line`, `point`, `triangle` and `sample`: keep them out of variable names.
    // Raw HDR (never graded: the bloom takes the bright parts), times the vertex colour, _Tint and _Gain; linear fog (to black
    // when additive). The material sets the blend: opaque strips, additive light (_Additive), premultiplied-alpha stamps.
    Properties { _MainTex("Texture",2D)="white"{} _Tint("Tint",Vector)=(1,1,1,1) _Rep("Rep",Float)=1 _Scroll("Scroll",Float)=0 _Blur("Blur",Float)=0 _Gain("Gain",Float)=1 _Sweep("Sweep",Float)=-1 _Chroma("Chroma",Float)=1 _Holo("Holo",Float)=0 _Texel("Map texel",Vector)=(.005,.0066,0,0) _Additive("Additive",Float)=0 _SrcBlend("Src blend",Float)=1 _DstBlend("Dst blend",Float)=0 _ZWrite("ZWrite",Float)=1 _Cull("Cull",Float)=2 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _Tint,_Texel;float _Rep,_Scroll,_Blur,_Gain,_Sweep,_Chroma,_Holo,_Additive;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_FOG_COORDS(1)};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float Lum(float3 c){return dot(c,float3(.3,.59,.11));}
            float4 frag(v2f i):SV_Target
            {
                float2 g=float2(i.uv.x,i.uv.y*_Rep+_Scroll);float2 dx=ddx(g),dy=ddy(g);float4 s=0;
                [unroll] for(int k=0;k<7;k++){float off=(k/6.0-.5)*_Blur;s+=tex2Dgrad(_MainTex,float2(g.x,frac(g.y+off)),dx,dy);}
                s/=7;float3 c=max(0,lerp(Lum(s.rgb),s.rgb,_Chroma));
                if(_Holo>.5)
                {
                    float l1=Lum(tex2D(_MainTex,i.uv+float2(_Texel.x,0)).rgb),l2=Lum(tex2D(_MainTex,i.uv-float2(_Texel.x,0)).rgb);
                    float l3=Lum(tex2D(_MainTex,i.uv+float2(0,_Texel.y)).rgb),l4=Lum(tex2D(_MainTex,i.uv-float2(0,_Texel.y)).rgb);
                    float edge=smoothstep(.015,.08,length(float2(l1-l2,l3-l4)));float2 cell=abs(frac(i.uv*float2(25,19))-.5);float grid=smoothstep(.45,.5,max(cell.x,cell.y));
                    c=c*.55+normalize(c+1e-3)*edge*1.1+float3(.25,.7,1)*(grid*.07+edge*.15);
                }
                c*=i.color.rgb*_Tint.rgb*_Gain;float a=s.a*i.color.a*_Tint.a;
                if(_Holo>.5)
                {
                    float2 e=min(i.uv,1-i.uv);float rule=1-smoothstep(0,.006,abs(min(e.x,e.y)-.01));float bracket=step(min(e.x,e.y),.007)*step(max(e.x,e.y),.08);
                    c+=float3(.35,.85,1)*(rule*.7+bracket*1.5);
                }
                if(_Sweep>=0)
                {
                    float d=i.uv.y-_Sweep;float sweep=exp(-d*d/(.006*.006))*1.2+exp(-max(-d,0)/.08)*step(d,0)*.25;
                    c+=_Tint.rgb*float3(.5,.9,1)*sweep*.8*step(.02,dot(s.rgb,float3(1,1,1)));c*=.9+.1*sin(i.uv.y*600);
                }
                float4 col;
                if(_Additive>.5){col=float4(c*a,0);UNITY_APPLY_FOG_COLOR(i.fogCoord,col,float4(0,0,0,0));}
                else if(_DstBlend>.5){col=float4(c,1);UNITY_APPLY_FOG(i.fogCoord,col);col=float4(col.rgb*a,a);}
                else{col=float4(c,1);UNITY_APPLY_FOG(i.fogCoord,col);}
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
