Shader "ChipArena/NativePoolWater"
{
    // The Poolrooms' still water (Claude, 2026-11-02; docs/BACKROOMS.md "The Poolrooms"): a transparent sheet over every pool
    // cell (Core PoolroomsMesh's Water surface, drawn by Runtime NativePoolroomsPresentation). Ripples from the owner's Backrooms
    // add-on's water normal map (_PoolNormal, linear, tangent space with the sheet's x and z as its axes) sampled twice, drifting
    // slowly two ways over _PoolRepeat metres. The body is _PoolTint, seen through at _PoolAlpha (the tiles below show); toward
    // grazing angles a Fresnel reflection of the room (the SH ambient times _PoolSky) takes over and the sheet turns opaque. Each of
    // MapLooks' 8 practical lights (the pool's ceiling lights) leaves a sharp highlight on the ripples. Then the ground haze and
    // fog, as NativeBlockLit does. No shadows; drawn after the opaque geometry.
    Properties
    {
        _PoolNormal ("Ripples (normal map)", 2D) = "bump" {}
        _PoolTint ("Water", Color) = (0.20,0.52,0.52,1)
        _PoolSky ("Reflection", Color) = (0.95,0.96,0.95,1)
        _PoolAlpha ("Clarity (alpha at normal view)", Range(0,1)) = 0.55
        _PoolRepeat ("Ripple repeat (m)", Float) = 2.0
        _PoolStrength ("Ripple strength", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            sampler2D _PoolNormal;float4 _PoolTint;float4 _PoolSky;float _PoolAlpha;float _PoolRepeat;float _PoolStrength;
            float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaHaze;float _ArenaHazeTop;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;float4 w=mul(unity_ObjectToWorld,vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float3 Ripple(float2 uv)
            {
                float3 n=UnpackNormal(tex2D(_PoolNormal,uv));return float3(n.x,n.z,n.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float t=_Time.y;float2 uv=i.world.xz/max(_PoolRepeat,0.01);
                float3 r=Ripple(uv+float2(0.011,0.007)*t)+Ripple(uv*1.37+float2(-0.006,0.013)*t+0.31);
                float3 n=normalize(float3(r.x*_PoolStrength,1.0,r.z*_PoolStrength));
                float3 v=normalize(_WorldSpaceCameraPos-i.world);float nv=saturate(dot(n,v));
                float fres=0.02+0.98*pow(1.0-nv,5.0);
                float3 amb=ShadeSH9(float4(0.0,1.0,0.0,1.0));
                float3 body=_PoolTint.rgb*(amb+_LightColor0.rgb*saturate(_WorldSpaceLightPos0.y));
                float3 refl=_PoolSky.rgb*amb;
                float3 spec=float3(0.0,0.0,0.0);
                for(int k=0;k<8;k++)
                {
                    float3 d=_ArenaLightPos[k].xyz-i.world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);
                    float fall=saturate(1.0-q*q);float3 l=d/len;float3 h=normalize(l+v);
                    spec+=_ArenaLightColor[k].rgb*fall*fall*(pow(saturate(dot(n,h)),180.0)*6.0+pow(saturate(dot(n,h)),24.0)*0.15);
                }
                float3 col=lerp(body,refl,fres)+spec;
                float a=saturate(lerp(_PoolAlpha,1.0,fres)+dot(spec,float3(0.3,0.4,0.3)));
                float dist=distance(i.world,_WorldSpaceCameraPos);float hz=saturate(i.world.y/max(_ArenaHazeTop,0.01));
                col=lerp(col,_ArenaHaze.rgb,_ArenaHaze.w*(1.0-hz)*(1.0-hz)*smoothstep(4.0,40.0,dist));
                fixed4 c=fixed4(col,a);UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
