Shader "ChipArena/NativeDeepEndWater"
{
    // THE DEEP END's water (docs/EXPEDITION-BOSS-BACKROOMS.md section 7): one displaced grid over the whole room, the real tide, the whirlpool's funnel, the seiche's wave and the wave field all in its height (Core DeepEndWaves.Compose,
    // the _DeepField texture: height, foam). Seen through by a named GrabPass and the scene's depth: what is under it bends with the surface (the boss's lamps shimmer) and darkens with how much water it is behind, the abyss going black;
    // over that the room's reflection (Fresnel, the box of NativeDeepEndRoom.cginc: the ceiling's round lights in the water), foam where the field says and where the water meets anything, the lamps' light in the water from below.
    // The level's own NativePoolWater is untouched: this one draws only while the boss's stage is on.
    Properties
    {
        _DeepNormal ("Ripples (normal map)", 2D) = "bump" {}
        _DeepDepthOn ("The scene's depth is on", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_DeepGrab" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off Cull Off Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "NativeDeepEndRoom.cginc"
            sampler2D _DeepNormal;sampler2D _DeepGrab;sampler2D_float _CameraDepthTexture;float _DeepDepthOn;float _DeepRipples;float _DeepMurk;
            float4 _DeepWhirl; // the whirlpool's centre x, z, its strength, how far it has turned
            float4 _ArenaHaze;float _ArenaHazeTop;
            float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;float4 grab:TEXCOORD2;float height:TEXCOORD3;UNITY_FOG_COORDS(4)};
            float2 FieldUv(float2 xz){return (xz-_DeepRect.xy)/max(_DeepRect.z,0.01);}
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;float4 w=mul(unity_ObjectToWorld,vertex);float2 uv=FieldUv(w.xz);float h=tex2Dlod(_DeepField,float4(uv,0.0,0.0)).r;w.y=_DeepRoomY+h;
                o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;o.height=h;o.screen=ComputeScreenPos(o.pos);o.grab=ComputeGrabScreenPos(o.pos);o.screen.z=-mul(UNITY_MATRIX_V,w).z;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float3 Ripple(float2 uv){float3 n=UnpackNormal(tex2D(_DeepNormal,uv));return float3(n.x,n.z,n.y);}
            float Hash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
            float Noise(float2 p){float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);return lerp(lerp(Hash(i),Hash(i+float2(1.0,0.0)),u.x),lerp(Hash(i+float2(0.0,1.0)),Hash(i+float2(1.0,1.0)),u.x),u.y);}
            // The surface's turning: where the whirlpool has carried a point, slower far out (two phases blended, so it never winds up).
            float2 Swirl(float2 p,float phase,float turn)
            {
                float2 c=p-_DeepWhirl.xy;float r=length(c);float a=turn*_DeepWhirl.w*2.6/(0.8+r*0.45)*_DeepWhirl.z*phase;float s=sin(a);float k=cos(a);return _DeepWhirl.xy+float2(c.x*k-c.y*s,c.x*s+c.y*k);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float t=_DeepClock;float2 w=i.world.xz;float tx=_DeepRect.w/max(_DeepRect.z,0.01);float2 fuv=FieldUv(w);
                float hl=tex2D(_DeepField,fuv-float2(tx,0.0)).r,hr=tex2D(_DeepField,fuv+float2(tx,0.0)).r,hd=tex2D(_DeepField,fuv-float2(0.0,tx)).r,hu=tex2D(_DeepField,fuv+float2(0.0,tx)).r;float2 field=tex2D(_DeepField,fuv).rg;
                float2 slope=float2(hr-hl,hu-hd)/max(2.0*_DeepRect.w,0.001);
                // The ripples, carried by the whirlpool's turning.
                float cyc=frac(_DeepWhirl.w*0.35);float2 pa=Swirl(w,1.0,1.0-0.0)*0.5,pb=pa;float mixK=0.0;
                if(_DeepWhirl.z>0.01){pa=Swirl(w,cyc,1.0)*0.5;pb=Swirl(w,frac(cyc+0.5),1.0)*0.5;mixK=abs(cyc*2.0-1.0);}
                float3 r=Ripple(pa+float2(0.011,0.007)*t)+Ripple(pa*1.37+float2(-0.006,0.013)*t+0.31);
                if(_DeepWhirl.z>0.01){float3 r2=Ripple(pb+float2(0.011,0.007)*t)+Ripple(pb*1.37+float2(-0.006,0.013)*t+0.31);r=lerp(r2,r,mixK);}
                float rs=0.03+0.17*_DeepRipples;float3 n=normalize(float3(r.x*rs-slope.x*1.25,1.0,r.z*rs-slope.y*1.25));
                float3 v=normalize(_WorldSpaceCameraPos-i.world);float nv=abs(dot(n,v));float fres=0.022+0.978*pow(1.0-nv,5.0);
                // ---- through the water ----
                float2 suv=i.grab.xy/i.grab.w;float eye=i.screen.z;float bend=clamp(0.42/max(eye,1.5),0.0,0.05);float2 off=n.xz*bend;
                float sceneEye=_DeepDepthOn>0.5?LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(float4(i.screen.xy+off*i.screen.w,i.screen.w,i.screen.w)))):eye+1.4;
                float thick=sceneEye-eye;if(thick<0.0){off=float2(0.0,0.0);sceneEye=_DeepDepthOn>0.5?LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen))):eye+1.4;thick=sceneEye-eye;}
                thick=max(0.0,thick);float3 seen=tex2D(_DeepGrab,suv+off).rgb;
                // How much light is left after this much water, and what the water itself gives back.
                float3 trans=exp(-thick*float3(0.30,0.105,0.092)*_DeepMurk);
                float soft=_DeepLight.y;float3 glow=tex2D(_DeepGlowTex,fuv).rgb;
                float3 scatter=float3(0.075,0.34,0.36)*(soft*0.62+0.012)+glow*0.55;
                float3 body=seen*trans+scatter*(1.0-trans);
                // ---- on it ----
                float3 rv=reflect(-v,n);if(rv.y<0.01)rv.y=0.01;rv=normalize(rv);float3 refl=DeepRoomColour(i.world,rv);
                float3 col=lerp(body,refl,fres);
                // Glints of the practical lights on the ripples.
                float3 spec=float3(0.0,0.0,0.0);
                for(int k=0;k<8;k++)
                {
                    float3 d=_ArenaLightPos[k].xyz-i.world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float fall=saturate(1.0-q*q);if(fall<=0.0)continue;float3 l=d/len;float3 h=normalize(l+v);float ndh=saturate(dot(n,h));
                    spec+=_ArenaLightColor[k].rgb*fall*fall*(pow(ndh,220.0)*7.0);
                }
                col+=spec;
                // ---- foam: the field's, and a line where the water meets anything ----
                float fn=Noise(pa*2.2+float2(t*0.05,-t*0.03))*0.6+Noise(pa*6.1-float2(t*0.08,t*0.04))*0.4;
                float edge=_DeepDepthOn>0.5?saturate(1.0-thick/0.34):0.0;
                float laced=smoothstep(0.28,0.62,field.g*1.18-(1.0-fn)*0.72)+edge*smoothstep(0.42,0.7,fn+edge*0.3)*0.65;
                float crest=saturate((length(slope)-0.55)*1.3)*smoothstep(0.4,0.75,fn);
                float foam=saturate(laced+crest);
                float3 lit=float3(0.82,0.86,0.86)*(soft*0.95+0.05)+glow*0.7+_DeepLight.x*float3(0.12,0.11,0.1);
                col=lerp(col,lit,foam*0.92);
                float dist=distance(i.world,_WorldSpaceCameraPos);float haze=saturate((i.world.y-_DeepRoomY)/max(_ArenaHazeTop,0.01));
                col=lerp(col,_ArenaHaze.rgb,_ArenaHaze.w*(1.0-haze)*(1.0-haze)*smoothstep(4.0,40.0,dist));
                fixed4 c=fixed4(col,1.0);UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
