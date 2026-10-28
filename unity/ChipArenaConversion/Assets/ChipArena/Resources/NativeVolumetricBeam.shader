Shader "ChipArena/NativeVolumetricBeam"
{
    // Volumetric beams (docs/LIGHTING-ADVANCED.md "Volumetric beams"; Core NativeBeamList, Runtime NativeVolumetricBeams):
    // one instanced draw of a unit open cone side (Core NativeBeamList.UnitMesh), each instance's matrix placing it from the
    // light's end (its origin) along its axis (+y, the beam's length) with its cross radii (x, z), additive and HDR so the bloom
    // takes the bright cores. Per pixel: the air the view ray crosses inside the beam (the chord through its local ellipse,
    // from where the ray meets the side to where it leaves it, or the beam's end when looking down it) over the beam's
    // width, so the beam is soft at its silhouette and bright seen end on; the fall-off from the source to the end; the
    // scene's depth where the depth texture is drawn (_BeamSoft 1), so a beam fades into the floor and walls it meets
    // instead of cutting them; a fade near the camera and, per beam, as the camera nears or enters it (Core
    // NativeBeamList.Outside); a two-octave scrolling dust (one octave on the medium tier); Unity's fog fading it to nothing.
    Properties {}
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One ZWrite Off ZTest LEqual Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            UNITY_INSTANCING_BUFFER_START(BeamProps)
            UNITY_DEFINE_INSTANCED_PROP(float4,_BeamColor)
            UNITY_DEFINE_INSTANCED_PROP(float4,_BeamShape)
            UNITY_INSTANCING_BUFFER_END(BeamProps)
            // x soft-edge power, y dust octaves (1 or 2), z the dust's clock (s), w the depth fade on (1)
            float4 _BeamParams;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;float4 color:TEXCOORD2;float4 shape:TEXCOORD3;float3 a:TEXCOORD4;float3 axis:TEXCOORD5;float3 wide:TEXCOORD6;float3 other:TEXCOORD7;UNITY_FOG_COORDS(8)};
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);
                float4 shape=UNITY_ACCESS_INSTANCED_PROP(BeamProps,_BeamShape);
                // The end's radius is shape.x times the source's.
                v.vertex.xz*=lerp(1.0,shape.x,v.vertex.y);
                o.world=mul(unity_ObjectToWorld,float4(v.vertex.xyz,1.0)).xyz;o.pos=UnityWorldToClipPos(o.world);
                o.screen=ComputeScreenPos(o.pos);COMPUTE_EYEDEPTH(o.screen.z);
                o.color=UNITY_ACCESS_INSTANCED_PROP(BeamProps,_BeamColor);o.shape=shape;
                o.a=unity_ObjectToWorld._m03_m13_m23;o.axis=unity_ObjectToWorld._m01_m11_m21;o.wide=unity_ObjectToWorld._m00_m10_m20;o.other=unity_ObjectToWorld._m02_m12_m22;
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float BeamHash(float3 p){p=frac(p*0.3183099+float3(0.71,0.113,0.419));p*=17.0;return frac(p.x*p.y*p.z*(p.x+p.y+p.z));}
            float BeamNoise(float3 p)
            {
                float3 i=floor(p);float3 f=frac(p);f=f*f*(3.0-2.0*f);
                float a=lerp(lerp(BeamHash(i),BeamHash(i+float3(1,0,0)),f.x),lerp(BeamHash(i+float3(0,1,0)),BeamHash(i+float3(1,1,0)),f.x),f.y);
                float b=lerp(lerp(BeamHash(i+float3(0,0,1)),BeamHash(i+float3(1,0,1)),f.x),lerp(BeamHash(i+float3(0,1,1)),BeamHash(i+float3(1,1,1)),f.x),f.y);
                return lerp(a,b,f.z);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float len=length(i.axis);float3 u=i.axis/max(len,0.0001);float r0=length(i.wide);float3 w=i.wide/max(r0,0.0001);float r0b=length(i.other);float3 wo=i.other/max(r0b,0.0001);
                float3 rel=i.world-i.a;float s=saturate(dot(rel,u)/max(len,0.0001));float k=lerp(1.0,i.shape.x,s);float ra=max(r0*k,0.01),rb=max(r0b*k,0.01);
                // The view ray from this point into the beam, in the ellipse's unit frame: where it leaves the side, or the end.
                float3 d=normalize(i.world-_WorldSpaceCameraPos);float2 P=float2(dot(rel,w)/ra,dot(rel,wo)/rb);float2 D2=float2(dot(d,w)/ra,dot(d,wo)/rb);
                float dd=max(dot(D2,D2),0.000001);float chord=max(-2.0*dot(P,D2)/dd,0.0);
                float du=dot(d,u);float left=du>0.0001?(len-dot(rel,u))/du:du<-0.0001?-dot(rel,u)/du:1e4;chord=min(chord,max(left,0.0));
                float thick=min(chord/(2.0*min(ra,rb)),3.0);float edge=pow(saturate(thick),_BeamParams.x)*(1.0+0.35*saturate(thick-1.0));
                // The source's soft start and the fall-off to the end.
                float along=pow(1.0-s,1.3)*smoothstep(0.0,0.06,s);
                // Near the camera, and the scene's depth (where the depth texture is drawn).
                float eye=i.screen.z;float fade=saturate((distance(i.world,_WorldSpaceCameraPos)-0.3)/1.6)*i.shape.y;
                if(_BeamParams.w>0.5){float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));fade*=saturate((scene-eye)/(0.5+0.6*min(ra,rb)));}
                // The dust, scrolling slowly down and along the wind.
                float t=_BeamParams.z;float3 q=i.world*0.9+float3(i.shape.z*13.1,t*0.35,t*0.12);
                float dust=BeamNoise(q);if(_BeamParams.y>1.5)dust=dust*0.65+BeamNoise(i.world*2.7-float3(t*0.2,t*0.6,i.shape.z*7.0))*0.35;
                float grain=lerp(1.0,0.35+1.3*dust,i.color.w);
                fixed4 c=fixed4(i.color.rgb*(edge*along*fade*grain),1.0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
