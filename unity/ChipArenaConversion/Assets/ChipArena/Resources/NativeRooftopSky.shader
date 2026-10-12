Shader "ChipArena/NativeRooftopSky"
{
    // The home screen's night over the cyber city (the camera's skybox while NativeLighting.Rooftop holds the look): navy above
    // a light-polluted magenta/cyan horizon, a low cloud deck lit from below by the city, and two searchlights sweeping.
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir);float3 c=RoofSkyBase(d);
                float denom=d.y+.25;float cl=RoofFbm(float3(d.x/denom*1.5+_RoofTime*.004,d.z/denom*1.5,2.0));
                float dens=smoothstep(.45,.75,cl)*smoothstep(0.0,.08,d.y)*smoothstep(.5,.15,d.y);c=lerp(c,float3(.10,.03,.10)+float3(.02,.06,.08)*cl,dens*.7);
                [unroll] for(int k=0;k<2;k++)
                {
                    float fk=k;float a=sin(_RoofTime*(.11+fk*.04)+fk*2.0)*.55+fk*.9-.45;float3 ax=normalize(float3(-sin(a),.9,-cos(a)*.6-.4));
                    float bd=length(cross(d,ax));c+=float3(.25,.55,.7)*.10*exp(-bd*bd*900.0)*smoothstep(0.0,.2,d.y);
                }
                return float4(c,1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
