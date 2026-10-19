Shader "ChipArena/NativeMoltenFlow"
{
    // THE CONVERTER's molten metal as smooth geometry (Claude, 2026-10-19; owner: "When Ironworks pours it looks like minecraft
    // lava right now. I want it to flow realistically."): Core NativeMoltenFlow builds one dynamic mesh a frame (the pour's
    // tube on its ballistic arc, the pools with their rounded meniscus edge, the splash crown, droplets and slag lumps, the
    // strips running down the nose and through the pit's cracks); the Converter's runtime presenter draws it with this
    // material on layer 0, no shadows, in the opaque queue. Per vertex (TEXCOORD0..2, Core MoltenVertex):
    //  A: flow coordinate (stream: the flight time from the lip, s; pool: local x, m; strip: distance along, m; droplet: its
    //     own x), across (stream, crown: the angle / 2 pi; pool: local z; strip: -1..1; droplet: its own y), share (stream,
    //     strip: along 0..1; pool: r / R; droplet: its own z), seed;
    //  B: heat (1 white-hot .. 0 cold), crust (0 liquid .. 1 crusted over), kind (0 stream, 1 pool, 2 crown, 3 droplet or lump,
    //     4 strip), emission gain;
    //  C: speed (m/s; a pool's spread share), age (a pool's s since it landed), the distance to the drawn edge (m), size (m).
    // The look: flow noise advected at the metal's own speed (a stream's phase is the time its metal left the lip, so every
    // ripple travels down the arc at the true local speed), a white-yellow core, orange, red at the cooler skin and the
    // silhouette, dark oxide streaks; pools crust from their edge inward into dark plates split by glowing fissures, bubble
    // and pop while liquid, and keep a hot rolling lip on their boundary while they are hot (the visible hot area is the
    // damaging area). Heat to light is NativeMolten.shader's ramp (the Ironworks' palette; readability rule 4: the hottest
    // metal stays below a telegraph's full-white decal). Lit like NativeMolten (the key and SH ambient, the rim, MapLooks' 8
    // practical lights and the Ward light field, ground haze and fog). Global _MoltenClock: the presentation clock, so the
    // killcam and every client flow alike. No textures, SM 3.5.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata_flow {float4 vertex:POSITION;float3 normal:NORMAL;float4 a:TEXCOORD0;float4 b:TEXCOORD1;float4 c:TEXCOORD2;};
        // SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
        float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaRimDir;float4 _ArenaRimColor;float4 _ArenaHaze;float _ArenaHazeTop;
        float _MoltenClock;
        #include "NativeWardLight.cginc"
        #define MF_STREAM 0.0
        #define MF_POOL 1.0
        #define MF_CROWN 2.0
        #define MF_BLOB 3.0
        #define MF_STRIP 4.0
        #define MF_LIQUID float3(0.045,0.040,0.036)
        #define MF_CRUST float3(0.032,0.029,0.027)
        #define MF_SCALE float3(0.070,0.068,0.070)
        bool MfIs(float k,float id){return abs(k-id)<0.5;}
        float MfHash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
        float MfHash3(float3 p){p=frac(p*0.1031);p+=dot(p,p.zyx+31.32);return frac((p.x+p.y)*p.z);}
        float MfNoise(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);
            return lerp(lerp(MfHash(i),MfHash(i+float2(1.0,0.0)),u.x),lerp(MfHash(i+float2(0.0,1.0)),MfHash(i+float2(1.0,1.0)),u.x),u.y);
        }
        float MfNoise3(float3 p)
        {
            float3 i=floor(p);float3 f=frac(p);float3 u=f*f*(3.0-2.0*f);
            float a=lerp(lerp(MfHash3(i),MfHash3(i+float3(1.0,0.0,0.0)),u.x),lerp(MfHash3(i+float3(0.0,1.0,0.0)),MfHash3(i+float3(1.0,1.0,0.0)),u.x),u.y);
            float b=lerp(lerp(MfHash3(i+float3(0.0,0.0,1.0)),MfHash3(i+float3(1.0,0.0,1.0)),u.x),lerp(MfHash3(i+float3(0.0,1.0,1.0)),MfHash3(i+float3(1.0,1.0,1.0)),u.x),u.y);
            return lerp(a,b,u.z);
        }
        float MfFbm(float2 p){return MfNoise(p)*0.55+MfNoise(p*2.03+float2(11.7,3.1))*0.3+MfNoise(p*4.11+float2(2.3,17.9))*0.15;}
        float MfFbm3(float3 p){return MfNoise3(p)*0.58+MfNoise3(p*2.07+float3(11.7,3.1,5.3))*0.29+MfNoise3(p*4.13+float3(2.3,17.9,8.1))*0.13;}
        // Cellular noise: x the distance to the nearest feature point, y to the second, z the nearest cell's hash (crust plates).
        float3 MfCells(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float d1=8.0;float d2=8.0;float id=0.0;
            for(int y=-1;y<=1;y++)
            {
                for(int x=-1;x<=1;x++)
                {
                    float2 g=float2(float(x),float(y));float h=MfHash(i+g);float2 o=float2(h,MfHash(i+g+float2(19.1,7.3)));
                    float2 r=g+o*0.85+0.075-f;float d=dot(r,r);
                    if(d<d1){d2=d1;d1=d;id=h;}else if(d<d2){d2=d;}
                }
            }
            return float3(sqrt(d1),sqrt(d2),id);
        }
        // Heat to light (linear HDR): NativeMolten.shader's MoltenRamp (black, a dull red, red, orange, yellow, pale yellow-white).
        float3 MfRamp(float t)
        {
            t=saturate(t);
            float3 c=lerp(float3(0.0,0.0,0.0),float3(0.30,0.018,0.002),smoothstep(0.08,0.26,t));
            c=lerp(c,float3(0.95,0.09,0.008),smoothstep(0.24,0.44,t));
            c=lerp(c,float3(1.75,0.50,0.055),smoothstep(0.42,0.64,t));
            c=lerp(c,float3(2.45,1.20,0.26),smoothstep(0.62,0.84,t));
            c=lerp(c,float3(3.20,2.25,1.05),smoothstep(0.82,1.0,t));
            return c;
        }
        struct MfSurf {float3 albedo;float3 emit;};
        // Crust plates on a flowing surface at p (m): cover 0..1 the plates' share, crack the fissures' width; returns the plate
        // body (1 inside a crusted plate, 0 in the liquid or a fissure) and, through crackOut, how near a fissure p is.
        float MfPlates(float2 p,float cover,float crack,out float crackOut)
        {
            float3 c=MfCells(p);float gap=c.y-c.x;float cell=step(c.z,cover);
            float near=1.0-smoothstep(crack*0.45,crack,gap);crackOut=near*cell;
            return cell*(1.0-near);
        }
        MfSurf MfSurface(float3 world,float3 n,float3 view,float4 a,float4 b,float4 c,float time)
        {
            MfSurf s;float heat=b.x;float crust=b.y;float kind=b.z;float gain=b.w;float seed=a.w;
            float facing=abs(dot(n,view));float T=heat;float3 alb=MF_LIQUID;
            if(MfIs(kind,MF_STREAM))
            {
                // The phase is when this metal left the lip: features ride down at the local speed and stretch as it speeds up.
                float ang=a.y*6.2831853;float phase=time-a.x;float3 q=float3(cos(ang),sin(ang),0.0);
                float streak=MfFbm3(float3(q.x*1.7,q.y*1.7,phase*15.0+seed*3.0));
                float skin=smoothstep(0.60,0.74,MfFbm3(float3(q.x*2.6+seed,q.y*2.6,phase*9.0)));
                float boil=MfNoise3(float3(q.x*0.9,q.y*0.9,phase*4.0+seed*5.0));
                T=heat*(0.60+0.30*facing)*(0.86+0.18*streak)*(1.0-0.34*skin*(1.0-0.5*facing))+0.06*boil*facing*heat;
                alb=lerp(MF_LIQUID,MF_CRUST,skin);
            }
            else if(MfIs(kind,MF_POOL))
            {
                // Local metres; a slow swirl; crust plates from the edge inward as it cools; bubbles in the liquid; a hot lip on
                // the boundary while it is hot.
                float2 xz=a.xy;float rho=a.z;float age=c.y;float edge=c.z;float2 sd=float2(seed*37.0,seed*11.0);
                float2 w=float2(MfFbm(xz*0.8+sd+float2(time*0.06,0.0)),MfFbm(xz*0.8+sd.yx+float2(0.0,-time*0.05)))-0.5;
                float2 p=xz*1.3+w*1.4;
                float body=MfFbm(p*1.2+float2(time*0.09,-time*0.07));float streak=MfNoise(p*3.1+float2(-time*0.2,time*0.15));
                float liquid=heat*(0.50+0.26*body+0.10*streak);
                float cover=saturate(crust*1.25+(rho-0.55)*0.85*crust);
                float2 pw=p*1.15+sd.yx+(float2(MfNoise(p*3.7+sd),MfNoise(p*3.7+sd.yx+float2(4.1,2.7)))-0.5)*0.45;
                float crackOut;float plate=MfPlates(pw,cover,0.05+0.07*(1.0-crust),crackOut);
                // Bubbles: a cell's bubble swells and pops (a bright ring), only in the liquid and fewer as it cools.
                float3 bc=MfCells(xz*2.6+sd*0.7);float cyc=frac(time*0.45+bc.z*7.13);float br=0.05+0.24*cyc;
                float rim=1.0-smoothstep(0.02,0.06,abs(bc.x-br));float bub=(1.0-smoothstep(br-0.04,br,bc.x))*step(0.6,bc.z)*(1.0-crust);
                liquid+=heat*(0.10*bub-0.12*bub*smoothstep(0.82,0.98,cyc)+0.16*rim*smoothstep(0.85,1.0,cyc)*step(0.6,bc.z)*(1.0-crust));
                float crusted=heat*(0.07+0.16*(1.0-crust))*(0.6+0.5*MfNoise(pw*4.0+sd));float fissure=heat*(0.70+0.18*body);
                T=lerp(liquid,crusted,plate);T=lerp(T,max(T,fissure),crackOut);
                float lip=1.0-smoothstep(0.015,0.09,edge);T=lerp(T,max(T,heat*0.9),lip*smoothstep(0.08,0.3,heat));
                alb=lerp(MF_LIQUID,lerp(MF_CRUST,MF_SCALE,smoothstep(0.5,1.0,crust)*MfNoise(p*5.0)),plate);
            }
            else if(MfIs(kind,MF_CROWN))
            {
                float ang=a.y*6.2831853;float sheet=MfNoise(float2(ang*2.2+seed,a.x*3.0-time*4.0));
                T=heat*(0.80+0.16*sheet)*(1.0-0.2*a.x)*(0.85+0.15*facing);
            }
            else if(MfIs(kind,MF_BLOB))
            {
                float3 o=a.xyz;float grain=MfFbm3(o*2.4+seed*13.0+float3(0.0,time*0.7,0.0));
                T=heat*(0.74+0.28*facing)*(0.86+0.22*grain);
                if(crust>0.01)
                {
                    float crackOut;float plate=MfPlates(float2(o.x+o.z*0.7,o.y)*2.6+seed*5.0,saturate(crust*1.2),0.09,crackOut);
                    T=lerp(T,heat*0.10,plate);T=lerp(T,max(T,heat*0.85),crackOut);alb=lerp(MF_LIQUID,MF_CRUST*0.7,plate);
                }
            }
            else
            {
                // A strip: the metal running along it, cooler at its edges, crust plates drifting slower than the flow.
                float along=a.x;float across=a.y;float speed=c.x;float fs=along-time*speed;float cs=along-time*speed*0.5;
                float2 p=float2(fs*1.1,across*0.9*max(c.w,0.05)*4.0)+float2(seed*7.0,seed*3.0);
                float body=MfFbm(p*1.4);float edgeCool=1.0-0.35*across*across;
                T=heat*(0.78+0.3*body)*edgeCool;
                float crackOut;float plate=MfPlates(float2(cs*1.6,across*1.4)+float2(seed*5.0,seed*9.0),saturate(crust*1.3),0.07,crackOut);
                T=lerp(T,heat*0.12,plate);T=lerp(T,max(T,heat*0.82),crackOut);alb=lerp(MF_LIQUID,MF_CRUST,plate);
            }
            s.albedo=alb;s.emit=MfRamp(T)*gain;return s;
        }
        float3 MfLights(float3 world,float3 n)
        {
            float3 sum=_ArenaRimColor.rgb*saturate(dot(n,_ArenaRimDir.xyz))+WardField(world,n);
            for(int k=0;k<8;k++)
            {
                float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float at=saturate(1.0-q*q);
                sum+=_ArenaLightColor[k].rgb*(at*at*saturate(dot(n,d/len)))*WardBeam(d,_ArenaLightColor[k].w);
            }
            return sum;
        }
        float3 MfHaze(float3 col,float3 world,float dist)
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
            #include "UnityLightingCommon.cginc"
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 nW:TEXCOORD1;float4 a:TEXCOORD2;float4 b:TEXCOORD3;float4 c:TEXCOORD4;UNITY_FOG_COORDS(5)};
            v2f vert(appdata_flow v)
            {
                v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);float4 w=mul(unity_ObjectToWorld,v.vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;
                o.nW=UnityObjectToWorldNormal(v.normal);o.a=v.a;o.b=v.b;o.c=v.c;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 nW=normalize(i.nW);float3 view=normalize(_WorldSpaceCameraPos-i.world);
                // Two-sided (Cull Off): a back face lights as its front.
                if(dot(nW,view)<0.0)nW=-nW;
                MfSurf s=MfSurface(i.world,nW,view,i.a,i.b,i.c,_MoltenClock);
                float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*saturate(dot(nW,_WorldSpaceLightPos0.xyz))+MfLights(i.world,nW);
                fixed4 c=fixed4(MfHaze(s.albedo*light+s.emit,i.world,distance(i.world,_WorldSpaceCameraPos)),1.0);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
