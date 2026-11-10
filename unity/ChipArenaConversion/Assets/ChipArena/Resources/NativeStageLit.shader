Shader "ChipArena/StageLit"
{
    // The stage art (docs/arenas/STAGE-ART.md; Runtime NativeStageArtDraw, Core NativeStageArt): a stage bank's modelled meshes
    // (tools/stages/stagekit.py) lit by the engine. Built-in forward base pass and a shadow caster (the moon's cascades), like
    // ChipArena/SourceLit's Standard lighting but per pixel and with the arena's own light block, so the art sits in the same light
    // as the blocks, the kits and the pilots:
    //  - the vertex: colour.rgb the albedo (sRGB, its dirt, moss and soot baked), colour.a the baked ambient occlusion, uv0 the
    //    smoothness and metallic, uv1 the fine surface (DETAIL_*) and its parameter (the wood's grain axis; on the glow sub-mesh
    //    the emission gain, HDR above 1 for the bloom);
    //  - the fine surface, procedural in world space (no textures): ashlar courses with their mortar and pillowed stones, worn
    //    flagstones with polish and cracks, carved grain, marble veins, wood grain, hammered iron, brushed brass with a patina in
    //    its hollows, a cloth weave with its sheen, wax and plaster: a micro-normal and a tint, so close surfaces read as stone,
    //    wood, metal and velvet;
    //  - Unity's physically based BRDF (UnityPBSLighting) for the key (its shadow map) and the ambient (the SH, the light volume)
    //    with the reflection probe's cubemap (Runtime renders one per stage) for the metals and the polish;
    //  - the arena's light block exactly as NativeBlockLit's (BlockLights: the rim, the eight practical slots, the blast flashes,
    //    the Ward field, the muzzle lights), each with a GGX-like highlight on the smooth finishes;
    //  - the anime style (NativeToon.cginc) when the look turns it on: the key through the ramp, the flattened ambient, the points
    //    in stairs, a crisp highlight band; the occlusion band from the baked occlusion;
    //  - the ground haze, the advanced lighting's scattering, then Unity's fog.
    // _Glow (material): the glow sub-mesh, unlit, albedo x gain x _StageGlow.rgb where a draw sets it (w 1: a lantern's colour,
    // the coals' heat; unset, as baked). _StageTint (per draw): rgb a tint (unset 0: none), w the damage (a struck pew's soot).
    Properties { _Glow("Glow sub-mesh",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGINCLUDE
        #include "UnityCG.cginc"
        UNITY_INSTANCING_BUFFER_START(StageProps)
        UNITY_DEFINE_INSTANCED_PROP(float4,_StageGlow)
        UNITY_DEFINE_INSTANCED_PROP(float4,_StageTint)
        UNITY_INSTANCING_BUFFER_END(StageProps)
        struct appdata_stage {float4 vertex:POSITION;float3 normal:NORMAL;float2 texcoord:TEXCOORD0;float2 texcoord1:TEXCOORD1;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
        float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaFlashPos[3];float4 _ArenaFlashColor[3];float4 _ArenaRimDir;float4 _ArenaRimColor;float4 _ArenaHaze;float _ArenaHazeTop;
        #include "NativeWardLight.cginc"
        #include "NativeMuzzleLight.cginc"
        #include "NativeAdvancedLight.cginc"
        #include "NativeToon.cginc"
        float _Glow;
        #define DETAIL_NONE 0
        #define DETAIL_ASHLAR 1
        #define DETAIL_FLAG 2
        #define DETAIL_CARVED 3
        #define DETAIL_MARBLE 4
        #define DETAIL_WOOD 5
        #define DETAIL_IRON 6
        #define DETAIL_BRASS 7
        #define DETAIL_CLOTH 8
        #define DETAIL_WAX 9
        #define DETAIL_PLASTER 10
        #define DETAIL_GLOW 11
        #define DETAIL_TILE 16
        // ---- noise (cheap: one hash per lattice corner, the fine fields two octaves) ----
        float SHash(float3 p){p=frac(p*0.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
        float SHash2(float2 p){float3 q=frac(float3(p.x,p.y,p.x)*0.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
        float SNoise(float3 x)
        {
            float3 i=floor(x);float3 f=frac(x);f=f*f*(3.0-2.0*f);
            return lerp(lerp(lerp(SHash(i),SHash(i+float3(1,0,0)),f.x),lerp(SHash(i+float3(0,1,0)),SHash(i+float3(1,1,0)),f.x),f.y),
                        lerp(lerp(SHash(i+float3(0,0,1)),SHash(i+float3(1,0,1)),f.x),lerp(SHash(i+float3(0,1,1)),SHash(i+float3(1,1,1)),f.x),f.y),f.z);
        }
        float SFbm(float3 x){return 0.65*SNoise(x)+0.35*SNoise(x*2.07+7.1);}
        // A micro-normal from a scalar field's gradient (finite differences in world space), `k` its strength: one octave.
        float3 SBump(float3 n,float3 p,float freq,float k)
        {
            float3 q=p*freq;float e=0.4;float h0=SNoise(q);
            float3 g=float3(SNoise(q+float3(e,0,0))-h0,SNoise(q+float3(0,e,0))-h0,SNoise(q+float3(0,0,e))-h0)/e;
            g-=n*dot(g,n);return normalize(n-g*k);
        }
        // The surface's tangents: u along the face (horizontal where it can be), v up the face.
        void STangents(float3 n,out float3 tu,out float3 tv){float3 up=abs(n.y)<0.95?float3(0,1,0):float3(1,0,0);tu=normalize(cross(up,n));tv=cross(n,tu);}
        // ---- the fine surfaces ----
        struct StageSurf {float3 albedo;float3 n;float smooth;float metal;float occ;float sheen;float wrap;};
        // Ashlar: courses `ch` tall of stones about `bl` long, each course offset, the joints sunk (mortar), each stone pillowed and
        // tinted on its own. h along the face, v up it (for a floor, two plan axes).
        void SAshlar(inout StageSurf s,float2 hv,float bl,float ch,float joint,float3 tu,float3 tv,float pillow,float vary)
        {
            float row=floor(hv.y/ch);float off=frac(row*0.618)*bl;float len=bl*(0.8+0.45*SHash2(float2(row,7.0)));
            float u=(hv.x+off)/len;float cell=floor(u);float fu=frac(u),fv=frac(hv.y/ch);
            float du=min(fu,1.0-fu)*len,dv=min(fv,1.0-fv)*ch;float d=min(du,dv);
            float mortar=1.0-smoothstep(joint*0.5,joint,d);
            float hsh=SHash2(float2(cell,row));
            s.albedo*=lerp(1.0,0.87+0.26*hsh,vary)*(1.0-0.06*SHash2(float2(cell*3.1,row+1.7)));
            s.albedo=lerp(s.albedo,s.albedo*float3(0.62,0.59,0.56),mortar);
            s.occ*=lerp(1.0,0.55,mortar);s.smooth*=lerp(1.0,0.4,mortar);
            // the pillow: each stone's face rounds down into its joints
            float eu=smoothstep(joint*2.5,joint*0.6,du)*(fu<0.5?-1.0:1.0),ev=smoothstep(joint*2.5,joint*0.6,dv)*(fv<0.5?-1.0:1.0);
            s.n=normalize(s.n+(tu*eu+tv*ev)*pillow);
        }
        void SFine(inout StageSurf s,int detail,float param,float3 world,float toon)
        {
            float3 n=s.n;float3 tu,tv;STangents(n,tu,tv);float k=lerp(1.0,0.45,toon);
            if(detail==DETAIL_ASHLAR)
            {
                // the parameter scales the courses (0: as drawn): the crypt's rougher, smaller stones, the piers' shorter ones
                float sc=param>0.01?param:1.0;bool top=abs(n.y)>0.7;float2 hv=top?world.xz:float2(abs(n.x)>abs(n.z)?world.z:world.x,world.y);
                SAshlar(s,hv,(top?0.9:0.95)*sc,(top?0.6:0.42)*sc,0.022*lerp(1.0,sc,0.5),tu,tv,0.55*k,1.0);
                s.n=SBump(s.n,world,9.0,0.18*k);s.albedo*=0.92+0.16*SNoise(world*3.3);
            }
            else if(detail==DETAIL_FLAG)
            {
                float sc=param>0.01?param:1.0;bool top=n.y>0.7;float2 hv=top?world.xz:float2(abs(n.x)>abs(n.z)?world.z:world.x,world.y);
                SAshlar(s,hv,(top?1.25:0.95)*sc,(top?0.82:0.42)*sc,0.016,tu,tv,0.3*k,1.0);
                // polish where feet wear it, cracks here and there
                float wear=smoothstep(0.45,0.75,SFbm(world*0.45));s.smooth=lerp(s.smooth,min(1.0,s.smooth+0.32),wear);
                float c=abs(SNoise(world*1.7+3.0)-0.5);s.albedo*=lerp(1.0,0.55,(1.0-smoothstep(0.0,0.012,c))*step(0.6,SNoise(world*0.6)));
                s.n=SBump(s.n,world,7.0,0.12*k);
            }
            else if(detail==DETAIL_CARVED)
            {
                s.n=SBump(s.n,world,11.0,0.15*k);s.albedo*=0.9+0.2*SFbm(world*2.2);
                s.albedo*=1.0-0.18*smoothstep(0.72,0.85,SNoise(world*6.0));
            }
            else if(detail==DETAIL_MARBLE)
            {
                float v=sin(world.x*1.4+world.y*0.8+world.z*1.1+SFbm(world*1.6)*7.0);float vein=1.0-smoothstep(0.0,0.09,abs(v));
                float v2=sin(world.z*2.3-world.y*1.7+SFbm(world*3.1+2.0)*5.0);vein=max(vein,0.5*(1.0-smoothstep(0.0,0.05,abs(v2))));
                s.albedo=lerp(s.albedo,s.albedo*float3(0.55,0.53,0.58),vein);s.n=SBump(s.n,world,6.0,0.03);
            }
            else if(detail==DETAIL_WOOD)
            {
                // the grain runs along the parameter's axis: streaks across the other two
                float3 a=param<0.5?float3(1,0,0):param<1.5?float3(0,1,0):float3(0,0,1);float along=dot(world,a);float3 across=world-a*along;
                float g=SFbm(across*float3(9.0,9.0,9.0)+along*0.35);float streak=sin(dot(across,float3(31.0,37.0,29.0))+g*9.0);
                s.albedo*=0.8+0.25*g+0.12*streak;float knot=smoothstep(0.82,0.9,SNoise(world*1.3));s.albedo*=1.0-0.35*knot;
                float3 gp=across*14.0+along*0.6;s.n=SBump(s.n,gp,1.0,0.12*k);s.smooth*=0.85+0.3*g;
            }
            else if(detail==DETAIL_IRON)
            {
                s.n=SBump(s.n,world,13.0,0.25*k);float rust=smoothstep(0.62,0.82,SFbm(world*2.6));s.albedo=lerp(s.albedo,float3(0.12,0.05,0.025),rust*0.6);
                s.metal*=1.0-0.7*rust;s.smooth*=1.0-0.5*rust;
            }
            else if(detail==DETAIL_BRASS)
            {
                s.n=SBump(s.n,world,22.0,0.03);float pat=(1.0-s.occ)*smoothstep(0.35,0.7,SFbm(world*3.0));
                s.albedo=lerp(s.albedo,float3(0.05,0.08,0.06),pat*0.7);s.smooth*=1.0-0.45*pat;
            }
            else if(detail==DETAIL_CLOTH)
            {
                float w1=sin(dot(world,tu)*420.0),w2=sin(dot(world,tv)*420.0);s.n=normalize(s.n+(tu*w1+tv*w2)*0.05*k);
                s.albedo*=0.92+0.1*SFbm(world*5.0);s.sheen=0.45;
            }
            else if(detail==DETAIL_WAX){s.wrap=0.45;s.albedo*=0.95+0.08*SNoise(world*14.0);}
            else if(detail==DETAIL_PLASTER){s.albedo*=0.88+0.2*SFbm(world*0.8);s.n=SBump(s.n,world,4.0,0.08*k);}
            else if(detail==DETAIL_TILE)
            {
                // glazed tiles `param` m square (0: 15 cm) on the world's grid: sunk grout, each tile's glaze pillowed and a touch
                // its own shade
                float sz=param>0.01?param:0.15;bool top=abs(n.y)>0.7;float3 au=top?float3(1,0,0):(abs(n.x)>abs(n.z)?float3(0,0,1):float3(1,0,0));float3 av=top?float3(0,0,1):float3(0,1,0);
                float2 hv=float2(dot(world,au),dot(world,av))/sz;float2 cell=floor(hv);float2 f=frac(hv);float2 d2=min(f,1.0-f)*sz;float d=min(d2.x,d2.y);float joint=min(0.004+sz*0.02,0.012);
                float grout=1.0-smoothstep(joint*0.5,joint,d);
                s.albedo*=0.95+0.1*SHash2(cell);s.albedo=lerp(s.albedo,s.albedo*0.35+float3(0.2,0.19,0.18),grout);
                s.smooth*=lerp(1.0,0.3,grout);s.occ*=lerp(1.0,0.7,grout);
                float eu=smoothstep(joint*3.0,joint*0.8,d2.x)*(f.x<0.5?-1.0:1.0),ev=smoothstep(joint*3.0,joint*0.8,d2.y)*(f.y<0.5?-1.0:1.0);
                s.n=normalize(s.n+(au*eu+av*ev)*0.4*k);
            }
        }
        // ---- light ----
        // The rim, the practical slots ((1 - (d/r)^2)^2), the flashes, the Ward field and the muzzle lights: their diffuse (N.L,
        // wrapped by `wrap`) and, for the slots, a highlight of the given sharpness (spec: the specular colour times the light).
        float3 StageLights(float3 world,float3 n,float3 v,float wrap,float sharp,inout float3 spec)
        {
            float3 sum=_ArenaRimColor.rgb*saturate((dot(n,_ArenaRimDir.xyz)+wrap)/(1.0+wrap))+WardField(world,n);
            for(int k=0;k<8;k++)
            {
                float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);
                float3 l=d/len;float3 c=_ArenaLightColor[k].rgb*(a*a)*WardBeam(d,_ArenaLightColor[k].w);float ndl=saturate((dot(n,l)+wrap)/(1.0+wrap));
                sum+=c*ndl;float3 h=normalize(l+v);spec+=c*(pow(saturate(dot(n,h)),sharp)*(sharp+8.0)*0.0398*saturate(dot(n,l)));
            }
            for(int m=0;m<3;m++)
            {
                float3 d=_ArenaFlashPos[m].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaFlashPos[m].w,0.0001);q*=q;
                sum+=_ArenaFlashColor[m].rgb*(saturate(5.0*(1.0-q))/(1.0+25.0*q)*saturate(dot(n,d/len)));
            }
            sum+=MuzzleLights(world,n,wrap);
            return sum;
        }
        float3 StageHaze(float3 col,float3 world,float dist)
        {
            float t=saturate(world.y/max(_ArenaHazeTop,0.01));
            return lerp(col,_ArenaHaze.rgb,_ArenaHaze.w*(1.0-t)*(1.0-t)*smoothstep(4.0,40.0,dist));
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            // The environment's mark for the anime style's outline pass (Resources/NativeToonEdge.shader tests bit 16).
            Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "AutoLight.cginc"
            #include "UnityPBSLighting.cginc"
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 nW:TEXCOORD1;float4 col:TEXCOORD2;float4 mat:TEXCOORD3;UNITY_SHADOW_COORDS(4) UNITY_FOG_COORDS(5) UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata_stage v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.nW=UnityObjectToWorldNormal(v.normal);
                float3 c=v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=GammaToLinearSpace(c);
                #endif
                o.col=float4(c,v.color.a);o.mat=float4(v.texcoord.xy,v.texcoord1.xy);
                UNITY_TRANSFER_SHADOW(o,float2(0.0,0.0));UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 world=i.world;float3 v=normalize(_WorldSpaceCameraPos-world);float dist=distance(world,_WorldSpaceCameraPos);
                float4 tint=UNITY_ACCESS_INSTANCED_PROP(StageProps,_StageTint);
                if(_Glow>0.5)
                {
                    // a draw that sets its glow writes w 1 (a lantern's colour, the coals' heat); unset, the glow is as baked
                    float4 glow=UNITY_ACCESS_INSTANCED_PROP(StageProps,_StageGlow);
                    float3 e=i.col.rgb*max(i.mat.w,0.0)*(glow.w>0.5?glow.rgb:float3(1.0,1.0,1.0));
                    fixed4 g=fixed4(StageHaze(e,world,dist),1.0);g.rgb=ArenaScatter(g.rgb,world,_WorldSpaceCameraPos);UNITY_APPLY_FOG(i.fogCoord,g);return g;
                }
                bool toon=_ArenaToon.x>0.5;
                StageSurf s;s.albedo=i.col.rgb*(dot(tint.rgb,float3(1.0,1.0,1.0))>0.0?tint.rgb:float3(1.0,1.0,1.0));s.n=normalize(i.nW);s.smooth=saturate(i.mat.x);s.metal=saturate(i.mat.y);s.occ=i.col.a;s.sheen=0.0;s.wrap=0.0;
                // a face seen from behind (a thin web, a cloth) turns to the eye
                if(dot(s.n,v)<0.0)s.n=-s.n;
                // the fade to a plain surface far off keeps the fine detail from shimmering
                float fineFade=1.0-smoothstep(18.0,42.0,dist);
                if(fineFade>0.0){StageSurf f=s;SFine(f,(int)(i.mat.z+0.5),i.mat.w,world,toon?1.0:0.0);s.albedo=lerp(s.albedo,f.albedo,fineFade);s.n=normalize(lerp(s.n,f.n,fineFade));s.smooth=lerp(s.smooth,f.smooth,fineFade);s.metal=f.metal;s.occ=lerp(s.occ,f.occ,fineFade);s.sheen=f.sheen;s.wrap=f.wrap;}
                // a struck block's damage: soot and scorch
                float dmg=saturate(tint.w);if(dmg>0.0){float soot=smoothstep(0.3,0.7,SFbm(world*2.5))*dmg;s.albedo=lerp(s.albedo,float3(0.03,0.025,0.02),0.7*soot)*(1.0-0.2*dmg);s.smooth*=1.0-0.6*dmg;}
                float3 n=s.n;
                UNITY_LIGHT_ATTENUATION(atten,i,world);
                float3 L=_WorldSpaceLightPos0.xyz,lc=_LightColor0.rgb;
                half oneMinus;half3 specColor;half3 diffColor=DiffuseAndSpecularFromMetallic(s.albedo,s.metal,specColor,oneMinus);
                // the indirect: the SH (the look's ambient) through the light volume, the probe's reflection, both occluded
                float3 sh=ShadeSH9(float4(n,1.0));
                UnityGIInput d;UNITY_INITIALIZE_OUTPUT(UnityGIInput,d);d.worldPos=world;d.worldViewDir=v;d.atten=atten;
                d.probeHDR[0]=unity_SpecCube0_HDR;d.probeHDR[1]=unity_SpecCube1_HDR;
                #if defined(UNITY_SPECCUBE_BLENDING)||defined(UNITY_SPECCUBE_BOX_PROJECTION)
                d.boxMin[0]=unity_SpecCube0_BoxMin;
                #endif
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                d.boxMax[0]=unity_SpecCube0_BoxMax;d.probePosition[0]=unity_SpecCube0_ProbePosition;d.boxMax[1]=unity_SpecCube1_BoxMax;d.boxMin[1]=unity_SpecCube1_BoxMin;d.probePosition[1]=unity_SpecCube1_ProbePosition;
                #endif
                Unity_GlossyEnvironmentData gloss=UnityGlossyEnvironmentSetup(s.smooth,v,n,specColor);
                float3 env=UnityGI_IndirectSpecular(d,s.occ,gloss);
                float sharp=exp2(10.0*s.smooth+1.0);float3 pspec=float3(0.0,0.0,0.0);
                float3 points=StageLights(world,n,v,s.wrap,sharp,pspec);
                float3 col;
                if(!toon)
                {
                    UnityLight light;light.color=lc*atten;light.dir=L;
                    UnityIndirect ind;ind.diffuse=(sh+ArenaGI(sh,world,n))*s.occ;ind.specular=env;
                    col=BRDF1_Unity_PBS(diffColor,specColor,oneMinus,s.smooth,n,v,light,ind).rgb;
                    col+=diffColor*points*lerp(1.0,s.occ,0.5)+specColor*pspec*s.smooth;
                }
                else
                {
                    float3 amb=ToonAmbient(sh,ShadeSH9(float4(0.0,1.0,0.0,1.0)),ShadeSH9(float4(0.0,-1.0,0.0,1.0)),n.y);
                    float ndl=(dot(n,L)+s.wrap)/(1.0+s.wrap);
                    float3 lit=ToonDiffuse(amb,lc,ndl,atten);lit+=ArenaGI(ToonDiffuse(amb,float3(0.0,0.0,0.0),ndl,atten),world,n);
                    lit=ToonOcclude(lit,saturate((1.0-s.occ)*1.6-0.15));
                    float3 hk=normalize(L+v);float band=smoothstep(0.86,0.9,dot(n,hk))*ToonShadow(atten);
                    float fres=pow(1.0-saturate(dot(n,v)),5.0);float3 refl=env*lerp(specColor,float3(1.0,1.0,1.0),fres*s.smooth);
                    col=diffColor*(lit+ToonPoints(points))+specColor*(lc*band*s.smooth*1.4+ToonPoints(pspec)*s.smooth)+refl*lerp(0.35,1.0,s.metal);
                }
                // velvet and cloth: a soft sheen at grazing angles in the light about them
                col+=s.sheen*pow(1.0-saturate(dot(n,v)),3.0)*s.albedo*(sh+points*0.5+lc*atten*0.3);
                fixed4 c=fixed4(StageHaze(col,world,dist),1.0);
                c.rgb=ArenaScatter(c.rgb,world,_WorldSpaceCameraPos);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            struct v2fs {V2F_SHADOW_CASTER;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2fs vertShadow(appdata_stage v){v2fs o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2fs,o);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)return o;}
            float4 fragShadow(v2fs i):SV_Target{SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
