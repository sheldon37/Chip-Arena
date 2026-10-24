Shader "ChipArena/NativeRooftopSky"
{
    // The home screen's night over the cyber city (the camera's skybox while NativeLighting.Rooftop holds the look): navy above
    // a light-polluted magenta/cyan horizon, a low cloud deck lit from below by the city, and two searchlights sweeping.
    // The anime style (_ArenaToon.x on, NativeLighting.Rooftop; NativeRooftopToon.cginc): the painted gradient with its band of
    // city glow, crisp stars, and three layers of cumulus lit from below by the city in place of the cloud deck (material
    // values from NativeMenuStyle, set by NativeRooftopScene: _RoofSkyToon y cover, z size, w the layers' base elevation;
    // _RoofSkyLit the clouds' lit underside, _RoofSkyShade their tops, linear). The searchlights sweep in both looks.
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
            float4 _RoofSkyToon,_RoofSkyLit,_RoofSkyShade;
            // The anime sky over the gradient: crisp stars high up, then three cumulus layers (far, low and hazy to near, high
            // and large) round the horizon, drifting slowly; the far layers take the horizon's glow.
            float3 RoofToonSky(float3 d,float3 c)
            {
                float3 st=d*230.0,cell=floor(st);c+=float3(.55,.65,.95)*step(.9975,RoofHash1(cell))*pow(max(0.0,1.0-length(frac(st)-.5)*2.0),6.0)*smoothstep(.1,.3,d.y);
                float az=atan2(-d.x,-d.z)+_RoofTime*.0003;float el=asin(clamp(d.y,-1.0,1.0));float px=max(length(fwidth(d)),.0004);
                float cover=_RoofSkyToon.y,size=_RoofSkyToon.z,base=_RoofSkyToon.w;
                float4 k=RoofToonCumulus(az,el,px,9.0,base,size,1.0,cover*1.1,_RoofSkyLit.rgb,_RoofSkyShade.rgb);
                float4 m=RoofToonCumulus(az+.9,el,px,15.0,base*.7,size*.62,2.0,cover,_RoofSkyLit.rgb,_RoofSkyShade.rgb);m.rgb=lerp(m.rgb,RoofToonSkyBase(float3(d.x,.02,d.z))*m.a,.25);k.rgb+=(1.0-k.a)*m.rgb;k.a+=(1.0-k.a)*m.a;
                float4 f=RoofToonCumulus(az+2.1,el,px,26.0,base*.45,size*.36,3.0,cover*.9,_RoofSkyLit.rgb,_RoofSkyShade.rgb);f.rgb=lerp(f.rgb,RoofToonSkyBase(float3(d.x,.02,d.z))*f.a,.45);k.rgb+=(1.0-k.a)*f.rgb;k.a+=(1.0-k.a)*f.a;
                return c*(1.0-k.a)+k.rgb;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir);float3 c=RoofSkyBase(d);
                if(_ArenaToon.x>0.5)c=RoofToonSky(d,c);
                else
                {
                float denom=d.y+.25;float cl=RoofFbm(float3(d.x/denom*1.5+_RoofTime*.004,d.z/denom*1.5,2.0));
                float dens=smoothstep(.45,.75,cl)*smoothstep(0.0,.08,d.y)*smoothstep(.5,.15,d.y);c=lerp(c,float3(.10,.03,.10)+float3(.02,.06,.08)*cl,dens*.7);
                }
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
