Shader "ChipArena/NativeRooftopScreen"
{
    // A holo screen of the home screen's rooftop city: its ad (Core NativeRooftopArt, rasterised once by NativeIconRaster) in
    // HDR, with scanlines, a slow roll, a rare glitch, soft edges and the city fog. _UvRect picks the ad's sub-rectangle.
    // The anime style (_ArenaToon.x on, NativeLighting.Rooftop): flatter scanlines and a crisp edge, so the ad reads as clean
    // flat colour fields; the glitch and the roll are kept.
    Properties { _MainTex("Ad",2D)="black"{} _Gain("Gain",Float)=1.5 _UvRect("Uv rect",Vector)=(0,0,1,1) _Calm("Reduced effects",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            sampler2D _MainTex;float _Gain,_Calm;float4 _UvRect;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;float glitch=step(.985,RoofHash2(float2(floor(_RoofTime*8.0),1.0)))*(1.0-_Calm);uv.x+=glitch*.02*sin(uv.y*80.0);
                float3 t=tex2D(_MainTex,_UvRect.xy+saturate(uv)*_UvRect.zw).rgb; // an sRGB texture: sampled linear
                float scan=.82+.18*sin(i.uv.y*900.0);float roll=.9+.1*smoothstep(0.0,.08,abs(frac(i.uv.y-_RoofTime*.07)-.5));
                float edge=smoothstep(0.0,.02,i.uv.x)*smoothstep(1.0,.98,i.uv.x)*smoothstep(0.0,.02,i.uv.y)*smoothstep(1.0,.98,i.uv.y);
                if(_ArenaToon.x>0.5){scan=lerp(scan,1.0,.65);edge=smoothstep(0.0,.006,i.uv.x)*smoothstep(1.0,.994,i.uv.x)*smoothstep(0.0,.006,i.uv.y)*smoothstep(1.0,.994,i.uv.y);}
                float3 c=t*_Gain*scan*roll*edge+float3(.01,.02,.04);
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
