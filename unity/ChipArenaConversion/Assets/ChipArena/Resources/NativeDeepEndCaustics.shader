Shader "ChipArena/NativeDeepEndCaustics"
{
    // Moving light on the room's surfaces while THE DEEP END's stage is on (docs/EXPEDITION-BOSS-BACKROOMS.md section 7: "half of what makes the level beautiful"): the level's own meshes drawn once more, added on top. Under the water, the ceiling's lights
    // focused on the floor and the walls; over it, the light of the lamps under the water thrown up on the walls and ceiling, falling off with height. The web is pushed around by the real waves: the field's curvature above a point focuses it, so a wave's ring
    // sweeps across the ceiling. Nothing here draws in the live level with no boss.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+40" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Blend One One Cull Back Offset -1,-1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "NativeDeepEndRoom.cginc"
            float _DeepCaustics;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_FOG_COORDS(2)};
            v2f vert(float4 vertex:POSITION,float3 normal:NORMAL){v2f o;float4 w=mul(unity_ObjectToWorld,vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;o.normal=UnityObjectToWorldNormal(normal);UNITY_TRANSFER_FOG(o,o.pos);return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float3 p=i.world;float3 n=normalize(i.normal);float t=_DeepClock;
                // A point's plane coordinates: floors and ceilings by x and z, walls along them and up.
                float2 q=abs(n.y)>0.6?p.xz:(abs(n.x)>0.5?float2(p.z,p.y*1.4):float2(p.x,p.y*1.4));
                // The waves over it: the curvature focuses the web.
                float2 fuv=(clamp(p.xz,_DeepRoom.xy-_DeepRoom.zz+0.3,_DeepRoom.xy+_DeepRoom.zz-0.3)-_DeepRect.xy)/max(_DeepRect.z,0.01);float tx=_DeepRect.w*2.0/max(_DeepRect.z,0.01);
                float hc=tex2D(_DeepField,fuv).r;float lap=tex2D(_DeepField,fuv+float2(tx,0.0)).r+tex2D(_DeepField,fuv-float2(tx,0.0)).r+tex2D(_DeepField,fuv+float2(0.0,tx)).r+tex2D(_DeepField,fuv-float2(0.0,tx)).r-4.0*hc;
                float2 warp=float2(tex2D(_DeepField,fuv+float2(tx,0.0)).r-tex2D(_DeepField,fuv-float2(tx,0.0)).r,tex2D(_DeepField,fuv+float2(0.0,tx)).r-tex2D(_DeepField,fuv-float2(0.0,tx)).r);
                float web=DeepCaustic(q*0.62+warp*2.6,t*0.42)+0.55*DeepCaustic(q*1.21+float2(3.1,1.7)-warp*1.4,t*0.58);
                float focus=1.0+clamp(-lap*7.0,-0.75,3.0);
                float3 glow=tex2D(_DeepGlowTex,fuv).rgb;float soft=_DeepLight.y;
                float above=p.y-_DeepTide;float3 c;
                if(above<0.0)
                {
                    // Under the water: the ceiling's light focused (dim by the depth); the lamps' own light moving too.
                    float deep=saturate(exp(above*0.42));float3 light=float3(0.55,0.72,0.70)*(_DeepLight.x*0.62+soft*0.25)+glow*0.9;c=light*web*focus*deep*1.1;
                }
                else
                {
                    // Over it: thrown up from the water, strongest near it and on the ceiling above the pool's lamps.
                    float fall=exp(-above*0.18);if(n.y<-0.6)fall=0.72;float3 light=glow*1.25+float3(0.20,0.36,0.36)*(_DeepLight.x*0.55+soft*0.2);c=light*web*focus*fall*0.72;
                }
                c*=_DeepCaustics;
                fixed4 o=fixed4(c,0.0);UNITY_APPLY_FOG_COLOR(i.fogCoord,o,fixed4(0,0,0,0));return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
