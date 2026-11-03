// The Smiler as an apparition (owner 2026-11-03: "The smiler is usually an black ghostly apparition like this", a billowing
// cloud of black smoke with two glowing white eyes and a wide grin of yellowed teeth glowing red at the gums; Core
// NativeSmilerLook, Runtime NativeSmilerPresentation). One draw: a box round the cloud, raymarched. The cloud is a signed
// field in its own space (metres, x right, y up, z ahead, the core at the origin): a core and six lobes turning slowly about
// it, a tail tapering down toward the floor (thinning to a wisp) and trailing its drift, the lunge's lobe thrown ahead on a neck of smoke; the
// field is pushed out and in by a tileable 3D noise (big billows, fine curls) boiling through it, so its edge is billowy,
// soft over a few centimetres, with wisps outside it. The smoke is near black (a little scene light and the face's own glow:
// red round the grin, white by the eyes). The face is drawn where the ray crosses a plane facing the camera on the cloud's
// near side (it turns to whoever looks at it); the smoke in front of it is thinned to a few wisps that drift across it.
// Premultiplied (Blend One OneMinusSrcAlpha), queue Transparent, no depth write; the march stops at the scene's depth
// (_CameraDepthTexture, _SmilerSoft 1; on the low graphics tier, 0, it runs to the box's far side). Two materials: from
// outside the box, its front faces depth tested; from inside it, its back faces always. Scene fog at the core's distance.
// Colours linear; the eyes and teeth pass 1 for the bloom.
Shader "ChipArena/NativeSmiler"
{
    Properties
    {
        _SmilerNoise("Billows (3D, RGBA linear: R billows, G curls, B value)",3D)="gray"{}
        _SmilerNoiseReady("Noise uploaded",Float)=0
        _SmilerSoft("Scene depth (depth texture)",Float)=0
        _Cull("Cull",Float)=2
        _ZTest("ZTest",Float)=4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            sampler3D _SmilerNoise;float _SmilerNoiseReady,_SmilerSoft;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            // World to cloud space (rotation and translation), cloud to world; the box (cloud space).
            float4x4 _SmilerToCloud,_SmilerToWorld;float4 _SmilerBoxMin,_SmilerBoxMax;
            // A: time (s), seed, presence, spread. B: gather, surge, rise (m), churn. C: drift (cloud space, m/s), bob (m).
            // D: eyes, squint, grin, open. E: face, glow, reach (m), 0.
            float4 _SmilerA,_SmilerB,_SmilerC,_SmilerD,_SmilerE;
            struct v2f{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;float3 amb:TEXCOORD2;UNITY_FOG_COORDS(3)};
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;o.pos=UnityObjectToClipPos(vertex);o.world=mul(unity_ObjectToWorld,vertex).xyz;o.screen=ComputeScreenPos(o.pos);
                o.amb=max(0.0,ShadeSH9(float4(0.0,1.0,0.0,1.0)));
                // The fog at the core's distance (the whole cloud fogs as one).
                float4 core=UnityWorldToClipPos(mul(_SmilerToWorld,float4(0.0,0.0,0.0,1.0)).xyz);UNITY_TRANSFER_FOG(o,core);
                return o;
            }
            float SmilerHash(float a,float b){return frac(sin(a*12.9898+b*78.233)*43758.5453);}
            float SmilerMin(float a,float b,float k){float h=saturate(0.5+0.5*(b-a)/k);return lerp(b,a,h)-k*h*(1.0-h);}
            // The cloud's field at p (negative inside, about metres) and its fine curls (0..1).
            float SmilerField(float3 p,out float curls)
            {
                float time=_SmilerA.x,seed=_SmilerA.y,spread=_SmilerA.w,gather=_SmilerB.x,surge=_SmilerB.y,churn=_SmilerB.w;float3 drift=_SmilerC.xyz;
                // Its own frame: bobbing, risen as it dies, drawn back through the lunge's wind-up.
                float3 q=p-float3(0.0,_SmilerC.w+_SmilerB.z,-0.18*gather);
                float grow=1.0+0.12*gather+0.9*spread;
                float d=length(q*float3(1.0,0.9,1.0))-0.5*grow;
                // Six lobes turning about the core, rising and sinking, trailing behind its drift.
                [unroll] for(int k=0;k<6;k++)
                {
                    float a=k*1.0472+seed+time*(0.17+0.05*k)*((k&1)==0?1.0:-1.0);
                    float3 c=float3(cos(a)*0.37,sin(time*0.41+k*1.9+seed)*0.2+0.04,sin(a)*0.37)*grow-drift*(0.05+0.03*k);
                    float r=(0.31+0.06*sin(time*0.57+k*2.3))*(1.0+0.35*spread);
                    d=SmilerMin(d,length(q-c)-r,0.2);
                }
                // The tail: tapering down toward the floor, swaying, trailing its drift.
                float3 a0=float3(0.0,-0.3,0.0),b0=float3(sin(time*0.6+seed)*0.13-drift.x*0.3,-1.25+0.35*spread,-0.12-drift.z*0.3+cos(time*0.47+seed)*0.08);
                float3 ba=b0-a0;float h=saturate(dot(q-a0,ba)/dot(ba,ba));
                d=SmilerMin(d,length(q-a0-ba*h)-lerp(0.33,0.05,h)*(1.0+0.3*spread),0.25);
                // The lunge's lobe, thrown ahead on its neck of smoke.
                float L=surge*_SmilerE.z;
                if(L>0.01)
                {
                    float3 sa=float3(0.0,0.0,0.15),sb=float3(0.0,0.0,L);float3 sd=sb-sa;float hh=saturate(dot(q-sa,sd)/max(1e-4,dot(sd,sd)));
                    d=SmilerMin(d,SmilerMin(length(q-sa-sd*hh)-lerp(0.42,0.3,hh),length(q-sb)-0.46,0.15),0.2);
                }
                // The billows: the big ones boiling slowly through it, the fine curls faster (its churn speeds both).
                float t=time*churn;
                float4 big=tex3Dlod(_SmilerNoise,float4(p*0.42+float3(seed*0.37,-t*0.065,seed*0.21),0.0));
                float4 fine=tex3Dlod(_SmilerNoise,float4(p*1.55+float3(t*0.05,t*0.09,-t*0.04),0.0));
                float billows=lerp(0.5,big.r,_SmilerNoiseReady);curls=lerp(0.5,fine.g,_SmilerNoiseReady);
                return d-(billows-0.5)*0.55-(curls-0.5)*0.2;
            }
            // The face at f (m on its plane: x right, y up; the mouth's corners at y 0) seen at px (m a pixel): premultiplied
            // light and coverage.
            float4 SmilerFace(float2 f,float px,float cell)
            {
                float eyes=_SmilerD.x,squint=_SmilerD.y,grin=_SmilerD.z,open=_SmilerD.w,face=_SmilerE.x,glow=_SmilerE.y,seed=_SmilerA.y;
                float3 rgb=0.0;float a=0.0;px=max(px,0.0012);
                // The eyes: white discs with a halo, squashed shut, cut by lids slanting toward the nose as they narrow.
                const float er=0.034;float lift=max(0.06,eyes),on=saturate(eyes*1.4-0.05);
                [unroll] for(int s=0;s<2;s++)
                {
                    float side=s==0?-1.0:1.0;float2 e=f-float2(side*0.105,0.08);
                    float r=length(float2(e.x,e.y/lift));float lid=e.y-er*lift*(1.0-0.9*squint)-side*e.x*0.55*squint;
                    float disc=saturate((er-r)/px+0.5)*saturate(-lid/px+0.5);
                    float halo=exp(-r*r/(er*er*2.0))*0.5+exp(-r/0.075)*0.12;
                    rgb+=float3(0.92,0.96,1.0)*(disc*7.0+halo*1.3*(1.0-disc))*on*glow;a=max(a,disc*on);
                }
                // The grin: a crescent pinched at its corners (the upper lip dips a little to the middle, the lower lip deep),
                // dropped as it parts.
                float hw=0.24*grin,t=f.x/hw,c=saturate(1.0-t*t);
                float yU=-0.075*pow(c,0.9),yL=-0.19*pow(c,0.68)-open*0.075*pow(c,0.55);
                float outside=max(max(f.y-yU,yL-f.y),(abs(t)-1.0)*hw);float inside=saturate(-outside/px+0.5);
                float H=max(yU-yL,1e-4),gum=0.012;
                // Two rows of teeth (a tooth a cell, narrower toward the corners as the grin wraps round): uneven, some pointed,
                // a few missing; the lower row offset half a tooth.
                float th=asin(clamp(t,-1.0,1.0)*0.985)/0.155;
                float cu=floor(th+0.5),lu=th-cu,cl=floor(th+1.0),ll=th+0.5-cl;
                float u1=SmilerHash(cu,seed+1.0),u2=SmilerHash(cu,seed+2.0),u3=SmilerHash(cu,seed+3.0),u4=SmilerHash(cu,seed+4.0);
                float l1=SmilerHash(cl,seed+11.0),l2=SmilerHash(cl,seed+12.0),l3=SmilerHash(cl,seed+13.0),l4=SmilerHash(cl,seed+14.0);
                float du=yU-gum-f.y,dl=f.y-yL-gum;
                float tipU=H*(0.5+0.12*(u2-0.5))*(1.0-0.3*open)-H*0.35*u4*pow(abs(lu)*2.0,1.5);
                float tipL=H*(0.44+0.12*(l2-0.5))*(1.0-0.3*open)-H*0.3*l4*pow(abs(ll)*2.0,1.5);
                float toothU=saturate(((0.5-0.06-0.06*u1)-abs(lu))/cell+0.5)*saturate(du/px+0.5)*saturate((tipU-du)/px+0.5)*step(0.07,u3);
                float toothL=saturate(((0.5-0.06-0.06*l1)-abs(ll))/cell+0.5)*saturate(dl/px+0.5)*saturate((tipL-dl)/px+0.5)*step(0.07,l3);
                float tooth=max(toothU,toothL),gums=saturate(1.0-tooth)*max(saturate((-du)/px+0.5),saturate((-dl)/px+0.5));
                // Enamel: yellowed, pale at the tip, browning and reddened toward the gum, streaked; deep red toward the corners.
                float tipward=toothU>toothL?saturate(du/max(tipU,1e-3)):saturate(dl/max(tipL,1e-3)),tone=toothU>toothL?u1:l1;
                float3 enamel=lerp(float3(0.6,0.24,0.07),float3(1.0,0.87,0.64),saturate(tipward*1.3))*(0.78+0.3*tone)*(0.82+0.18*SmilerHash(toothU>toothL?cu:cl,floor(f.y*95.0)));
                enamel=lerp(enamel,float3(1.0,0.2,0.05),smoothstep(0.55,1.0,abs(t)));
                float3 gumLight=float3(2.6,0.36,0.07),mouth=float3(0.32,0.025,0.008)*(0.6+1.6*open)*(0.6+0.4*c);
                float3 grinLight=lerp(lerp(mouth,gumLight,gums),enamel*1.7,tooth)*inside;
                // The lips' glow outside the mouth.
                float o=max(outside,0.0);grinLight+=float3(1.0,0.16,0.04)*(exp(-o/0.016)*0.9+exp(-o/0.06)*0.1)*(1.0-inside);
                rgb+=grinLight*glow*face;a=max(a,inside*face);
                return float4(rgb,a);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 camW=_WorldSpaceCameraPos,rdW=normalize(i.world-camW);
                float3 ro=mul(_SmilerToCloud,float4(camW,1.0)).xyz,rd=mul((float3x3)_SmilerToCloud,rdW);
                // The box along the ray, cut at the scene in front (a select: an unrendered depth texture never reaches it).
                float3 safe=float3(abs(rd.x)>1e-5?rd.x:1e-5,abs(rd.y)>1e-5?rd.y:1e-5,abs(rd.z)>1e-5?rd.z:1e-5);
                float3 ta=(_SmilerBoxMin.xyz-ro)/safe,tb=(_SmilerBoxMax.xyz-ro)/safe,lo=min(ta,tb),hi=max(ta,tb);
                float tn=max(max(lo.x,lo.y),max(lo.z,0.0)),tf=min(min(hi.x,hi.y),hi.z);
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.screen.xy/i.screen.w))/max(1e-3,dot(rdW,-UNITY_MATRIX_V[2].xyz));
                tf=_SmilerSoft>0.5?min(tf,scene):tf;
                // The face's plane: on the cloud's side toward the camera (its height kept near the core's), facing the camera.
                float3 anchor=float3(0.0,_SmilerC.w+_SmilerB.z,-0.18*_SmilerB.x+_SmilerB.y*_SmilerE.z);
                float3 toward=ro-anchor;toward.y*=0.35;toward=normalize(toward+float3(0.0,0.0,1e-4));
                float3 faceC=anchor+toward*0.44+float3(0.0,0.03,0.0),n=normalize(ro-faceC+float3(0.0,0.0,1e-4));
                float3 right=normalize(cross(float3(0.0,1.0,0.0),-n)+float3(1e-4,0.0,0.0)),up=cross(-n,right);
                float facing=dot(rd,n),tFace=facing<-1e-3?dot(faceC-ro,n)/facing:-1.0;
                float3 hit=ro+rd*tFace-faceC;float2 f=float2(dot(hit,right),dot(hit,up));
                float th=asin(clamp(f.x/(0.24*_SmilerD.z),-1.0,1.0)*0.985)/0.155;
                float4 faceLight=SmilerFace(f,max(fwidth(f.x),fwidth(f.y)),max(fwidth(th),1e-4));
                bool faceDue=tFace>tn&&tFace<tf;
                if(tf<=tn)discard;
                // The march: jittered per pixel, skipping the empty space by the field, a fixed step inside.
                float jitter=frac(52.9829189*frac(dot(i.pos.xy,float2(0.06711056,0.00583715))));
                float presence=_SmilerA.z,glow=_SmilerE.y,face=_SmilerE.x,eyes=_SmilerD.x;
                float3 mouthAt=faceC-up*0.11,eyeL=faceC+up*0.08-right*0.105,eyeR=faceC+up*0.08+right*0.105;
                float3 lightBase=float3(0.011,0.0095,0.012)*(i.amb+_LightColor0.rgb*0.35);
                float t=tn+jitter*0.03,T=1.0;float3 col=0.0;
                [loop] for(int k=0;k<80;k++)
                {
                    if(t>=tf||T<0.01)break;
                    float3 p=ro+rd*t;float curls;float d=SmilerField(p,curls);
                    // Dissolving (a blink, the death): eaten from outside in, unevenly.
                    float dd=d+(1.0-presence)*0.55*(0.4+curls);
                    float ds=0.035;
                    if(dd>0.32)ds=max(0.05,dd*0.6);
                    else
                    {
                        float dens=(saturate(-dd*8.0)*34.0+saturate(1.0-dd/0.22)*smoothstep(0.5,0.85,curls)*(dd>0.0?3.0:0.0))*presence;
                        // The tail thins to a ghostly wisp toward the floor.
                        dens*=lerp(0.18,1.0,smoothstep(-1.3,-0.45,p.y-_SmilerC.w-_SmilerB.z));
                        // In front of the face only wisps drift across it.
                        float along=dot(p-faceC,n),across=length(p-faceC-n*along);
                        dens*=lerp(1.0,0.04+0.9*smoothstep(0.7,0.92,curls),smoothstep(-0.03,0.02,along)*smoothstep(0.5,0.34,across));
                        float3 m=p-mouthAt,el=p-eyeL,er=p-eyeR;
                        float3 light=lightBase+(float3(1.0,0.14,0.03)*0.42*exp(-dot(m,m)/0.035)*face+float3(0.9,0.95,1.0)*0.25*(exp(-dot(el,el)/0.006)+exp(-dot(er,er)/0.006))*eyes)*glow;
                        float a=1.0-exp(-dens*ds);col+=T*a*light;T*=1.0-a;
                    }
                    if(faceDue&&t+ds>=tFace){faceDue=false;col+=T*faceLight.rgb;T*=1.0-faceLight.a;}
                    t+=ds;
                }
                if(faceDue&&T>=0.01){col+=T*faceLight.rgb;T*=1.0-faceLight.a;}
                float4 c=float4(col,1.0-T);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(unity_FogColor.rgb*c.a,0));
                #if defined(UNITY_COLORSPACE_GAMMA)
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
