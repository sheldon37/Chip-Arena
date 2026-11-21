Shader "ChipArena/NativeMolten"
{
    // Molten metal for the City Ironworks (Claude, 2026-10-16; docs/EXPEDITION-CITY-IRONWORKS.md section 4, gap 1): flowing,
    // crusting iron in the runners, the ladle's stream, molds and pools, fire in the furnace mouths, hot slabs and billets,
    // glassy slag with glowing cracks and the coke in the open oven. Core NativeIronworksKit lays the pieces (NativeMoltenPiece)
    // and drives them each frame from the hazard views (NativeIronworksKit.MoltenState); Runtime NativeIronworksPresentation
    // draws them instanced on a unit cube (positions +-1, face normals) on layer 0, no shadows cast, only while map
    // `expedition-foundry` is loaded.
    // Per instance: _MoltenSize (half extents), _MoltenFlow (flow speed m/s, seed), _MoltenHeat (heat where the flow starts and
    // where it ends, 0 crust .. 1 white heat; crust share; front: how far along the flow the hot metal reaches, 0..1),
    // _MoltenStyle (style: 0 flow, 1 stream, 2 fire, 3 slab, 4 slag, 5 coke; flow axis 0 x, 1 y, 2 z, negative when the flow runs
    // toward -axis; the flow's speed scale; emission gain).
    // Global _MoltenClock: the presentation clock (s, wrapped by the runtime), so the killcam and every client flow alike.
    // Readability (docs section 3.2 rule 4): the hottest metal sits at HDR 1.4 to 4 (MoltenRamp tops out at 3.2 times the gain,
    // the gain at most 1.25), below a telegraph's full-white decal; heat is local (the emission never lights anything itself:
    // the kit's lights and the light field do). Lighting of the crust: the key and SH ambient as Unity's Lambert applies them,
    // the rim, MapLooks' 8 practical lights and the Ward light field (NativeWardLight.cginc: the Ironworks bakes its molten glows
    // into it), then the ground haze and fog, as NativeBlockLit does. No textures, SM 3.5.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGINCLUDE
        #include "UnityCG.cginc"
        UNITY_INSTANCING_BUFFER_START(MoltenProps)
        UNITY_DEFINE_INSTANCED_PROP(float4,_MoltenSize)
        UNITY_DEFINE_INSTANCED_PROP(float4,_MoltenFlow)
        UNITY_DEFINE_INSTANCED_PROP(float4,_MoltenHeat)
        UNITY_DEFINE_INSTANCED_PROP(float4,_MoltenStyle)
        UNITY_INSTANCING_BUFFER_END(MoltenProps)
        struct appdata_molten {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
        // SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
        float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaRimDir;float4 _ArenaRimColor;float4 _ArenaHaze;float _ArenaHazeTop;
        float _MoltenClock;
        #include "NativeWardLight.cginc"
        #define MO_FLOW 0.0
        #define MO_STREAM 1.0
        #define MO_FIRE 2.0
        #define MO_SLAB 3.0
        #define MO_SLAG 4.0
        #define MO_COKE 5.0
        // Crust: black iron oxide, a little grey where it has cooled longest.
        #define MO_CRUST float3(0.030,0.027,0.025)
        #define MO_SCALE float3(0.045,0.046,0.050)
        bool MoIs(float k,float id){return abs(k-id)<0.5;}
        float MoHash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
        float MoNoise(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);
            return lerp(lerp(MoHash(i),MoHash(i+float2(1.0,0.0)),u.x),lerp(MoHash(i+float2(0.0,1.0)),MoHash(i+float2(1.0,1.0)),u.x),u.y);
        }
        float MoFbm(float2 p){return MoNoise(p)*0.55+MoNoise(p*2.03+float2(11.7,3.1))*0.3+MoNoise(p*4.11+float2(2.3,17.9))*0.15;}
        // Cellular noise: x the distance to the nearest feature point, y to the second, z the nearest cell's hash (crust plates).
        float3 MoCells(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float d1=8.0;float d2=8.0;float id=0.0;
            for(int y=-1;y<=1;y++)
            {
                for(int x=-1;x<=1;x++)
                {
                    float2 g=float2(float(x),float(y));float h=MoHash(i+g);float2 o=float2(h,MoHash(i+g+float2(19.1,7.3)));
                    float2 r=g+o*0.85+0.075-f;float d=dot(r,r);
                    if(d<d1){d2=d1;d1=d;id=h;}else if(d<d2){d2=d;}
                }
            }
            return float3(sqrt(d1),sqrt(d2),id);
        }
        // Heat to light (linear HDR): black, a dull red glow, red, orange, yellow, a pale yellow-white at full heat. The ramp's
        // top (3.2 in red) keeps molten metal orange-white through the bloom's white blend instead of blowing out.
        float3 MoltenRamp(float t)
        {
            t=saturate(t);
            float3 c=lerp(float3(0.0,0.0,0.0),float3(0.30,0.018,0.002),smoothstep(0.08,0.26,t));
            c=lerp(c,float3(0.95,0.09,0.008),smoothstep(0.24,0.44,t));
            c=lerp(c,float3(1.75,0.50,0.055),smoothstep(0.42,0.64,t));
            c=lerp(c,float3(2.45,1.20,0.26),smoothstep(0.62,0.84,t));
            c=lerp(c,float3(3.20,2.25,1.05),smoothstep(0.82,1.0,t));
            return c;
        }
        float MoAxis(float3 v,float a){return a<0.5?v.x:(a<1.5?v.y:v.z);}
        struct MoltenSurf {float3 albedo;float3 emit;};
        // The surface at `obj` (the piece's own frame, m, from its centre), its face normal nO, half extents hs; style (kind,
        // signed axis + 1, speed scale, gain); heat (start, end, crust, front); speed m/s; seed; the clock.
        MoltenSurf MoltenSurface(float3 obj,float3 nO,float3 hs,float4 style,float4 heat,float speed,float seed,float time)
        {
            MoltenSurf s;float kind=style.x;float axis=abs(style.y)-1.0;float dir=style.y<0.0?-1.0:1.0;float live=style.z;float gain=style.w;
            float3 an=abs(nO);bool top=an.y>=max(an.x,an.z);
            // Along the flow (m, increasing downstream) and its share 0..1 from where it starts; across: the face's coordinate
            // square to the flow (the third axis, or the next one on a face looking along the flow).
            float along=MoAxis(obj,axis)*dir;float hlen=max(MoAxis(hs,axis),0.001);float u=saturate((along+hlen)/(2.0*hlen));
            float nAxis=an.x>=max(an.y,an.z)?0.0:(an.y>=an.z?1.0:2.0);float cAxis=abs(nAxis-axis)<0.5?(axis<1.5?axis+1.0:0.0):3.0-nAxis-axis;
            float across=MoAxis(obj,cAxis);float acrossHalf=max(MoAxis(hs,cAxis),0.001);
            float2 sd=float2(seed*37.0,seed*11.0);float t=time;
            float h=lerp(heat.x,heat.y,u);float crust=heat.z;float front=heat.w;
            float3 alb=MO_CRUST;float T=0.0;
            if(MoIs(kind,MO_STREAM))
            {
                // A falling stream: long streaks racing down it, its edges a little cooler; no crust.
                float fall=along-t*speed*live;float edge=1.0-pow(saturate(abs(across)/acrossHalf),3.0);
                float streak=MoFbm(float2(across*9.0+sd.x,fall*0.55));float ripple=MoNoise(float2(across*3.0,fall*2.4+sd.y));
                T=h*(0.86+0.1*streak+0.06*ripple)*(0.82+0.18*edge);alb=float3(0.05,0.04,0.03);
            }
            else if(MoIs(kind,MO_FIRE))
            {
                // Fire in a mouth: tongues rising over the glowing bed, dark soot between them, sparks of white near the bed.
                float hy=saturate((obj.y+hs.y)/(2.0*max(hs.y,0.001)));float rise=obj.y-t*speed*live*1.6;
                float tongue=MoFbm(float2(across*1.7+sd.x,rise*1.1));float lick=MoFbm(float2(across*3.3+sd.y,rise*2.3+7.0));
                float flame=smoothstep(hy*0.95-0.12,hy*0.95+0.22,tongue*0.75+lick*0.45);float bed=1.0-smoothstep(0.0,0.32,hy);
                T=lerp(heat.y,heat.x,bed)*(0.45+0.65*flame)+0.12*bed*MoNoise(float2(across*6.0,t*3.0+sd.x));
                T*=0.92+0.08*sin(t*7.3+seed*40.0);alb=float3(0.02,0.015,0.012);
            }
            else if(MoIs(kind,MO_SLAB))
            {
                // A hot slab or billet: even heat under flakes of scale, its edges a shade cooler.
                float2 p=top?obj.xz:float2(across,obj.y);float flakes=smoothstep(0.58,0.74,MoFbm(p*3.1+sd));float pits=smoothstep(0.7,0.85,MoNoise(p*11.0+sd.yx));
                float3 e3=hs-abs(obj);float edge=min(min(e3.x,e3.y),e3.z);float rim=smoothstep(0.0,0.07,edge+0.02*an.y);
                T=h*(0.86+0.14*rim)*(1.0-0.38*flakes)-0.08*pits;alb=lerp(MO_CRUST,MO_SCALE,flakes);
            }
            else if(MoIs(kind,MO_COKE))
            {
                // Glowing coke: one incandescent mass split by dark fissures between the lumps, hotter and cooler patches drifting
                // slowly over it, each lump breathing a little on its own.
                float2 q=float2(across,obj.y);float2 warp=float2(MoFbm(q*2.1+sd),MoFbm(q*2.1+sd+float2(3.7,1.9)))-0.5;
                float3 c=MoCells(q*7.5+warp*1.6+sd);float seam=smoothstep(0.0,0.1,c.y-c.x);
                float mottle=MoFbm(q*0.9+sd*0.3+float2(0.0,t*0.05));float breathe=0.9+0.1*sin(t*1.3+c.z*30.0);
                T=h*(0.45+0.55*mottle)*(0.62+0.38*seam)*breathe*(0.8+0.2*c.z);alb=lerp(float3(0.02,0.02,0.02),float3(0.05,0.045,0.04),c.z);
            }
            else
            {
                // Flow and slag: the moving metal (scrolling, warped) with crust plates drifting on it slower than the flow;
                // the plates' cracks keep the heat; beyond the front the trough has not filled (cold crust).
                float slag=MoIs(kind,MO_SLAG)?1.0:0.0;float fs=along-t*speed*live;float cs=along-t*speed*live*0.55;
                float2 p=float2(fs*0.9,across*1.6)+sd;float2 w=float2(MoFbm(p*0.8),MoFbm(p*0.8+float2(5.2,1.3)))-0.5;
                float body=MoFbm(p+w*1.6);float streak=MoNoise(float2(fs*0.35,across*5.5)+sd);
                float3 c=MoCells(float2(cs*1.5,across*2.2)*(1.0+0.6*slag)+sd.yx);float gap=c.y-c.x;
                float cover=saturate(crust*(1.2-0.55*h)+0.3*slag);float plate=step(c.z,cover)*smoothstep(0.03,0.12,gap);
                float filled=smoothstep(front+0.03,front-0.03,u);float lead=smoothstep(0.09,0.0,abs(u-front))*step(front,0.97);
                T=h*(0.78+0.3*body+0.12*streak)+0.25*lead*h;
                float crack=(1.0-smoothstep(0.0,0.05+0.03*slag,gap))*(0.55+0.45*body);
                T=lerp(T,T*0.62*crack,plate);
                T=lerp(h*0.18*(1.0-smoothstep(0.0,0.04,gap))+0.02,T,filled);
                alb=lerp(float3(0.045,0.04,0.035),lerp(MO_CRUST,float3(0.012,0.012,0.014),slag),max(plate,1.0-filled));
                if(slag>0.5)alb+=float3(0.008,0.009,0.012)*smoothstep(0.6,0.8,MoNoise(p*2.0));
            }
            s.albedo=alb;s.emit=MoltenRamp(T)*gain;return s;
        }
        float3 MoltenLights(float3 world,float3 n)
        {
            float3 sum=_ArenaRimColor.rgb*saturate(dot(n,_ArenaRimDir.xyz))+WardField(world,n);
            for(int k=0;k<8;k++)
            {
                float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);
                sum+=_ArenaLightColor[k].rgb*(a*a*saturate(dot(n,d/len)))*WardBeam(d,_ArenaLightColor[k].w);
            }
            return sum;
        }
        float3 MoltenHaze(float3 col,float3 world,float dist)
        {
            float t=saturate(world.y/max(_ArenaHazeTop,0.01));
            return lerp(col,_ArenaHaze.rgb,_ArenaHaze.w*(1.0-t)*(1.0-t)*smoothstep(4.0,40.0,dist));
        }
        // SHARED END
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #include "UnityLightingCommon.cginc"
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 obj:TEXCOORD1;float4 size:TEXCOORD2;float4 heat:TEXCOORD3;float4 style:TEXCOORD4;float3 nO:TEXCOORD5;float3 nW:TEXCOORD6;UNITY_FOG_COORDS(7)};
            v2f vert(appdata_molten v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);
                float4 size=UNITY_ACCESS_INSTANCED_PROP(MoltenProps,_MoltenSize);float4 flow=UNITY_ACCESS_INSTANCED_PROP(MoltenProps,_MoltenFlow);
                float3 p=v.vertex.xyz*size.xyz;float4 w=mul(unity_ObjectToWorld,float4(p,1.0));o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;
                o.obj=float4(p,flow.x);o.size=float4(size.xyz,flow.y);o.heat=UNITY_ACCESS_INSTANCED_PROP(MoltenProps,_MoltenHeat);o.style=UNITY_ACCESS_INSTANCED_PROP(MoltenProps,_MoltenStyle);
                o.nO=v.normal;o.nW=UnityObjectToWorldNormal(v.normal);UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 nO=normalize(i.nO);float3 nW=normalize(i.nW);
                MoltenSurf s=MoltenSurface(i.obj.xyz,nO,i.size.xyz,i.style,i.heat,i.obj.w,i.size.w,_MoltenClock);
                float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*saturate(dot(nW,_WorldSpaceLightPos0.xyz))+MoltenLights(i.world,nW);
                fixed4 c=fixed4(MoltenHaze(s.albedo*light+s.emit,i.world,distance(i.world,_WorldSpaceCameraPos)),1.0);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
