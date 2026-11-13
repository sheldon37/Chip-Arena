// The flesh and bone of a gib (owner 2026-11-13: "we do use new blood, but the gibbing seems to still use voxel flesh and wounds?
// Can we essentially match the quality with our blood? Using higher quality bone structure and flesh"; docs/GORE-FLESH.md):
// a cut's cross-section, a wound's pit, a torn lump and a bone's end, drawn per pixel from what the mesh carries, lit as the
// blood is (NativeBloodLight.cginc: the ambient, the key light with its shadow, the arena's light block, a wet highlight) and
// in the blood's own reds. Nothing is textured: the tissue is noise in the body's own space, so it stays put on its piece.
//  - _Mode 0, a cap (Runtime NativeVisualCutPresentation.cs, NativeVisualWoundPresentation.cs): texcoord1 x the distance in
//    from the section's edge (m) and y its kind (Core BisectLook: 0 the vertex colour as it is, 1 tissue: skin, a band of fat,
//    then muscle: a few large muscles in fine bundles, pale sheaths between; 2 a long bone: cortex, spongy bone, marrow; 3 spongy bone; 4 the skull round the brain; 5 a
//    sausage: casing, then meat; 6 a wound's pit: x is the depth down it); texcoord2 the shell's own paint at the edge
//    (in the project's colour space, as the vertex colour); texcoord3 the point in the death pose (m). The vertex colour's alpha is the share of its own light it keeps.
//  - _Mode 1, a torn lump, and 2, a bone shard (Runtime NativeKillModels.cs KillChunk, KillCap): the source-model batches'
//    instanced _SceneTint is its colour and _BodyTint's alpha its opacity (dithered below 1, as ChipArena/SourceLit).
// A corpse's dissolve (_Dissolve, NativeVisualBodyFx.cginc DissolveFront) takes the caps with the body.
Shader "ChipArena/NativeFlesh"
{
    Properties
    {
        _Gloss("Wet highlight",Range(0,1))=0.62
        _Glow("Its own light (share of its colour)",Range(0,1))=0.2
        _Mode("0 a cap, 1 a torn lump, 2 a bone shard",Float)=0
        _Cull("Cull",Float)=0
        _BodyTint("Opacity (alpha)",Color)=(1,1,1,1)
        _SceneTint("A lump's colour",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        CGINCLUDE
        #include "UnityCG.cginc"
        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
        UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
        UNITY_INSTANCING_BUFFER_END(Props)
        float FleshDither(float3 world){return frac(sin(dot(floor(world*120.0),float3(12.9898,78.233,37.719)))*43758.5453);}
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityLightingCommon.cginc"
            #include "AutoLight.cginc"
            #include "NativeBloodLight.cginc"
            float _Gloss,_Glow,_Mode;
            sampler2D _VisualDissolveNoise;float _Dissolve;float4 _DissolveEdge;
            struct appdata
            {
                float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;float2 texcoord:TEXCOORD0;float2 texcoord1:TEXCOORD1;
                float3 texcoord2:TEXCOORD2;float3 texcoord3:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 n:TEXCOORD2;float4 data:TEXCOORD3;
                float3 shell:TEXCOORD4;float3 rest:TEXCOORD5;float3 soft:TEXCOORD6;float3 domDir:TEXCOORD7;float3 domColor:TEXCOORD8;
                float4 color:COLOR;UNITY_SHADOW_COORDS(9) UNITY_FOG_COORDS(10) UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            v2f vert(appdata v)
            {
                v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.n=UnityObjectToWorldNormal(v.normal);
                o.color=v.color;o.shell=v.texcoord2;
                if(_Mode>0.5)
                {
                    // A lump: the tissue in its own space (a unit lump is about 9 cm of it), each one's own patch by its colour.
                    float4 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint);
                    o.rest=v.vertex.xyz*0.09+float3(tint.r*7.3,tint.g*11.9,tint.b*5.1)+unity_ObjectToWorld._m03_m13_m23*0.37;
                    // How far in from its skin a point of it reads: its hollows deeper, its bulges shallower.
                    o.data=float4(saturate(0.62-length(v.vertex.xyz))*0.05,_Mode>1.5?7.0:1.0,0.0,0.0);
                }
                else{o.rest=v.texcoord3;o.data=float4(v.texcoord1.x,v.texcoord1.y,0.0,0.0);}
                o.soft=max(ShadeSH9(float4(o.n,1.0)),0.0)+BloodArenaLights(o.world,o.n,0.45,o.domDir,o.domColor);
                UNITY_TRANSFER_SHADOW(o,float2(0.0,0.0));UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            // Noise in the body's own space: a value noise, and the nearest two cell centres' distances (bundles, lobules, pores).
            float FleshHash(float3 p){p=frac(p*0.3183099+float3(0.71,0.113,0.419));p*=17.0;return frac(p.x*p.y*p.z*(p.x+p.y+p.z));}
            float FleshNoise(float3 x)
            {
                float3 i=floor(x),f=frac(x);f=f*f*(3.0-2.0*f);
                return lerp(lerp(lerp(FleshHash(i),FleshHash(i+float3(1,0,0)),f.x),lerp(FleshHash(i+float3(0,1,0)),FleshHash(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(FleshHash(i+float3(0,0,1)),FleshHash(i+float3(1,0,1)),f.x),lerp(FleshHash(i+float3(0,1,1)),FleshHash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            float3 FleshHash3(float3 p){p=float3(dot(p,float3(127.1,311.7,74.7)),dot(p,float3(269.5,183.3,246.1)),dot(p,float3(113.5,271.9,124.6)));return frac(sin(p)*43758.5453);}
            float2 FleshCells(float3 x)
            {
                float3 i=floor(x),f=frac(x);float a=8.0,b=8.0;
                for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int k=-1;k<=1;k++)
                {
                    float3 g=float3(k,y,z);float3 r=g+FleshHash3(i+g)-f;float d=dot(r,r);
                    if(d<a){b=a;a=d;}else if(d<b)b=d;
                }
                return sqrt(float2(a,b));
            }
            // The muscle `d` m in from the fat: a few large muscles with pale sheaths between them (the cells of a noise warped so
            // none is regular), each of fine bundles, its own tone, some sheaths faint, darker toward the middle, blood lying in it. `h` its relief,
            // `wet` its share of the highlight.
            float3 FleshMuscle(float3 rest,float d,out float h,out float wet)
            {
                float3 q=rest+(float3(FleshNoise(rest*37.0+1.7),FleshNoise(rest*37.0+5.3),FleshNoise(rest*37.0+9.1))-0.5)*0.009;
                float2 g=FleshCells(q*27.0);float band=(1.0-smoothstep(0.02,0.10,g.y-g.x))*lerp(0.3,1.0,smoothstep(0.25,0.7,FleshNoise(q*14.0+2.3)));
                float2 w=FleshCells(q*105.0);float fine=1.0-smoothstep(0.04,0.2,w.y-w.x);
                float tone=FleshNoise(q*23.0+4.1),grain=FleshNoise(rest*430.0),deep=smoothstep(0.004,0.05,d);
                float3 c=lerp(float3(0.36,0.016,0.022),float3(0.13,0.004,0.008),deep)*((0.82+0.36*tone)*(1.0-0.16*fine)*(0.94+0.12*grain));
                c=lerp(c,float3(0.62,0.36,0.30),band*0.75*(1.0-deep*0.4));
                float pooled=smoothstep(0.5,0.72,FleshNoise(rest*31.0+7.3));c=lerp(c,float3(0.11,0.002,0.005),pooled*0.72);
                h=FleshNoise(q*55.0)*0.6+(1.0-w.x)*0.22-band*0.45+pooled*0.2;wet=0.7+0.3*pooled;return c;
            }
            float3 FleshFat(float3 rest,out float h,out float wet)
            {
                float2 w=FleshCells(rest*210.0);float edge=1.0-smoothstep(0.02,0.14,w.y-w.x);
                float3 c=lerp(float3(0.84,0.60,0.27),float3(0.74,0.36,0.21),FleshNoise(rest*70.0));c=lerp(c,float3(0.42,0.05,0.045),edge*0.55);
                h=saturate(1.0-w.x)*0.6;wet=0.55;return c;
            }
            float3 FleshTissue(float3 rest,float d,float3 shell,float skin,float fatFrom,out float h,out float wet)
            {
                float wob=(FleshNoise(rest*150.0)-0.5)*0.0024;float e=d+wob;
                float fat=fatFrom+0.0045*FleshNoise(rest*42.0+3.1);
                float hm,wm,hf,wf;float3 muscle=FleshMuscle(rest,max(e-skin-fat,0.0),hm,wm);float3 lard=FleshFat(rest,hf,wf);
                float inFat=smoothstep(skin-0.0004,skin+0.0004,e),inMuscle=smoothstep(skin+fat-0.0007,skin+fat+0.0007,e);
                // The skin's own paint, darker where it meets the flesh (a line of blood under it).
                float3 rim=shell*lerp(0.9,0.45,smoothstep(skin*0.45,skin,e));
                float3 c=lerp(rim,lard,inFat);c=lerp(c,muscle,inMuscle);
                h=lerp(lerp(0.35,hf,inFat),hm,inMuscle);wet=lerp(lerp(0.25,wf,inFat),wm,inMuscle);return c;
            }
            float3 FleshBone(float3 rest,float d,float kind,out float h,out float wet)
            {
                float3 ivory=float3(0.80,0.71,0.53)*(0.9+0.14*FleshNoise(rest*900.0));float3 c=ivory;h=0.3;wet=0.35;
                if(kind<2.5)
                {
                    // A long bone: dense cortex, a ring of spongy bone, then the marrow.
                    float2 w=FleshCells(rest*1500.0);float pore=smoothstep(0.38,0.2,w.x);
                    float3 spongy=lerp(float3(0.70,0.52,0.33),float3(0.30,0.03,0.03),pore);
                    float3 marrow=lerp(float3(0.20,0.008,0.012),float3(0.50,0.20,0.07),smoothstep(0.6,0.85,FleshNoise(rest*600.0)));
                    float a=smoothstep(0.0019,0.0025,d),b=smoothstep(0.0034,0.0042,d);
                    c=lerp(lerp(ivory,spongy,a),marrow,b);h=lerp(lerp(0.5,0.2+0.3*(1.0-pore),a),0.1,b);wet=lerp(0.3,0.9,b);
                }
                else if(kind<3.5)
                {
                    // Spongy bone: a thin shell round a honeycomb, marrow in its pores.
                    float2 w=FleshCells(rest*1100.0);float pore=smoothstep(0.42,0.22,w.x);float a=smoothstep(0.0011,0.0017,d);
                    c=lerp(ivory,lerp(float3(0.66,0.46,0.29),float3(0.26,0.02,0.025),pore),a);h=0.5-0.35*pore*a;wet=0.3+0.5*pore*a;
                }
                else
                {
                    // The skull: its two tables with the spongy layer between, round the brain.
                    float a=smoothstep(0.0030,0.0040,d);float fold=abs(FleshNoise(rest*95.0)-0.5)*2.0;
                    float3 brain=lerp(float3(0.28,0.10,0.10),float3(0.66,0.36,0.34),smoothstep(0.05,0.3,fold));
                    float diploe=smoothstep(0.0009,0.0013,d)*(1.0-smoothstep(0.0021,0.0026,d));
                    c=lerp(lerp(ivory,float3(0.58,0.34,0.24),diploe*0.7),brain,a);h=lerp(0.5,fold*0.8,a);wet=lerp(0.3,0.85,a);
                }
                return c;
            }
            // The Hot Dog (it is sausage through): its casing, a darker ring under it, then pale meat with flecks of fat and pepper.
            float3 FleshSausage(float3 rest,float d,float3 shell,out float h,out float wet)
            {
                float e=d+(FleshNoise(rest*150.0)-0.5)*0.0016;float2 w=FleshCells(rest*260.0);
                float3 meat=lerp(float3(0.74,0.27,0.19),float3(0.62,0.18,0.13),FleshNoise(rest*60.0));
                meat=lerp(meat,float3(0.86,0.72,0.62),smoothstep(0.26,0.14,w.x)*step(0.55,FleshHash(floor(rest*260.0))));
                meat=lerp(meat,float3(0.16,0.05,0.035),smoothstep(0.87,0.93,FleshNoise(rest*380.0)));
                meat*=lerp(0.72,1.0,smoothstep(0.0025,0.008,e));
                float3 c=lerp(shell*0.8,meat,smoothstep(0.0017,0.0025,e));h=0.4+0.3*FleshNoise(rest*300.0);wet=0.45;return c;
            }
            float4 frag(v2f i,float facing:VFACE):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 body=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);if(body.a<0.999)clip(body.a-FleshDither(i.world));
                float front=0.0;
                if(_Mode<0.5&&_Dissolve>0.0){float dn=tex2D(_VisualDissolveNoise,i.uv*3.0).r,cut=_Dissolve*1.08;clip(dn-cut);front=1.0-saturate((dn-cut)/0.07);}
                float kind=i.data.y,d=i.data.x;float3 rest=i.rest;float h=0.3,wet=0.5;float3 albedo;
                float3 paint=i.color.rgb,shell=i.shell;
                #ifdef UNITY_COLORSPACE_GAMMA
                paint=GammaToLinearSpace(paint);shell=GammaToLinearSpace(shell);
                #endif
                UNITY_BRANCH if(_Mode>0.5)
                {
                    float3 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb;
                    #ifdef UNITY_COLORSPACE_GAMMA
                    tint=GammaToLinearSpace(tint);
                    #endif
                    UNITY_BRANCH if(kind>6.5)
                    {
                        // A bone shard: ivory, stained, its broken ends showing the spongy bone.
                        float2 w=FleshCells(rest*900.0);float stain=smoothstep(0.45,0.75,FleshNoise(rest*55.0));
                        albedo=tint*paint*(0.86+0.2*FleshNoise(rest*700.0));albedo=lerp(albedo,float3(0.26,0.02,0.025),stain*0.6);
                        h=0.4+0.2*w.x;wet=0.3+0.5*stain;
                    }
                    else
                    {
                        // A torn lump: red meat takes the tissue's look; a scrap of the shell (cloth, plating) keeps its colour,
                        // wet with blood in its hollows.
                        float red=saturate((tint.r-max(tint.g,tint.b)*1.6)*6.0)*step(tint.g+tint.b,tint.r);
                        float hm,wm;float3 meat=FleshMuscle(rest,d,hm,wm)*lerp(0.6,1.15,saturate(tint.r/0.4));
                        float soak=smoothstep(0.4,0.7,FleshNoise(rest*48.0));float3 scrap=lerp(tint,float3(0.11,0.002,0.005),soak*0.7);
                        albedo=lerp(scrap,meat,red);h=lerp(0.3+0.3*soak,hm,red);wet=lerp(0.3+0.6*soak,wm,red);
                    }
                }
                else
                {
                    UNITY_BRANCH if(kind<0.5){albedo=paint;wet=0.6;h=FleshNoise(rest*300.0)*0.4;}
                    else
                    {
                        UNITY_BRANCH if(kind<1.5)albedo=FleshTissue(rest,d,shell,0.003,0.003,h,wet);
                        else
                        {
                            UNITY_BRANCH if(kind<4.5)albedo=FleshBone(rest,d,kind,h,wet);
                            else
                            {
                                UNITY_BRANCH if(kind<5.5)albedo=FleshSausage(rest,d,shell,h,wet);
                                // A wound's pit: the layers down its wall, darker the deeper it goes.
                                else albedo=FleshTissue(rest,d,shell,0.0016,0.002,h,wet)*lerp(1.0,0.42,saturate(d/0.07));
                            }
                        }
                    }
                }
                // The relief turns the normal (the height's slope across the pixel).
                float3 n=normalize(i.n)*(facing>0.0?1.0:-1.0);
                float3 dpx=ddx(i.world),dpy=ddy(i.world);float hx=ddx(h),hy=ddy(h);
                float3 r1=cross(dpy,n),r2=cross(n,dpx);float det=dot(dpx,r1);
                float3 grad=sign(det)*(hx*r1+hy*r2);float bump=0.0011;
                n=normalize(abs(det)*n-grad*bump);
                float3 v=normalize(_WorldSpaceCameraPos-i.world);float3 l=_WorldSpaceLightPos0.xyz;
                UNITY_LIGHT_ATTENUATION(atten,i,i.world);
                float3 key=_LightColor0.rgb*(saturate(dot(n,l)*0.7+0.3)*atten);
                float3 col=albedo*(i.soft+key)+albedo*_Glow*i.color.a+_DissolveEdge.rgb*front;
                float gloss=_Gloss*wet;
                col+=(_LightColor0.rgb*(BloodGlint(n,v,l,gloss,0.06)*atten)+i.domColor*BloodGlint(n,v,i.domDir,gloss,0.06))*gloss;
                // A wet face also catches the sky: a glint from overhead in the ambient's colour, and more of it at a glancing angle.
                col+=max(ShadeSH9(float4(0.0,1.0,0.0,1.0)),0.0)*(BloodGlint(n,v,normalize(float3(0.25,0.9,0.3)),gloss,0.0)*0.9*gloss);
                float rimLight=pow(1.0-saturate(dot(n,v)),3.0);col+=max(ShadeSH9(float4(reflect(-v,n),1.0)),0.0)*(rimLight*0.35*gloss);
                #ifdef UNITY_COLORSPACE_GAMMA
                col=LinearToGammaSpace(max(col,0.0));
                #endif
                float4 shaded=float4(col,1.0);UNITY_APPLY_FOG(i.fogCoord,shaded);return shaded;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            struct v2f {V2F_SHADOW_CASTER;float3 world:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            v2f vert(appdata_base v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o;}
            float4 frag(v2f i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);float4 body=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);if(body.a<0.999)clip(body.a-FleshDither(i.world));SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
