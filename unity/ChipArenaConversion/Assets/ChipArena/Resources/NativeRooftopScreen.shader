Shader "ChipArena/NativeRooftopScreen"
{
    // A holo screen of the home screen's rooftop city: its ad (Core NativeRooftopArt, rasterised once by NativeIconRaster) in
    // HDR, with scanlines, a slow roll, a rare glitch, soft edges and the city fog. _UvRect picks the ad's sub-rectangle.
    // The anime style (_ArenaToon.x on, NativeLighting.Rooftop): flatter scanlines and a crisp edge, so the ad reads as clean
    // flat colour fields; the glitch and the roll are kept.
    // Animated (owner 2026-10-29: "Can make the screens better designed on buildings + animated"; Core
    // NativeRooftopCity.ScreenState writes _Anim each frame: x the brightness, y the cut to the screen's own loop, z the holo
    // sweep's height, w the vertical signs' chase): a lit border with corner brackets in the screen's colour, a sweep line
    // running down the panel, the Siren cutting through a glitch to a heartbeat monitor and Chip Arena to an equalizer, a
    // foil sheen over the chip card, the CHIP and ARENA signs lighting letter by letter (the sign's E stutters now and then).
    // _Kind: 0 siren, 1 arena, 2 chip, 3 banner, 4 sign. Branch free.
    Properties { _MainTex("Ad",2D)="black"{} _Gain("Gain",Float)=1.5 _UvRect("Uv rect",Vector)=(0,0,1,1) _Calm("Reduced effects",Float)=0 _Kind("Kind",Float)=0 _Anim("Animation",Vector)=(1,0,-1,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            sampler2D _MainTex;float _Gain,_Calm,_Kind;float4 _UvRect,_Anim;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            float Bump(float x,float m,float s){float d=(x-m)/s;return exp(-d*d);}
            // one heartbeat (P wave, QRS, T wave) over x 0..1
            float Ecg(float x){return .12*Bump(x,.15,.03)-.18*Bump(x,.37,.008)+1.0*Bump(x,.40,.012)-.32*Bump(x,.43,.01)+.25*Bump(x,.66,.05);}
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;float k=_Kind;float t=_RoofTime;float pulse=_Anim.x,cut=_Anim.y,sweep=_Anim.z,chase=_Anim.w;
                float aspect=_UvRect.z/max(_UvRect.w,1e-3);float py=max(fwidth(uv.y),1e-4);
                // the glitch: the rare one, and through each cut to the loop (rows torn sideways, red split off)
                float tear=4.0*cut*(1.0-cut);
                float glitch=max(step(.985,RoofHash2(float2(floor(t*8.0),1.0))),step(.05,tear)*tear)*(1.0-_Calm);
                float shift=(RoofHash2(float2(floor(uv.y*24.0),floor(t*20.0)))-.5)*.10*glitch+glitch*.01*sin(uv.y*80.0);
                float2 su=float2(uv.x+shift,uv.y);
                float3 ad=tex2D(_MainTex,_UvRect.xy+saturate(su)*_UvRect.zw).rgb; // an sRGB texture: sampled linear
                float red=tex2D(_MainTex,_UvRect.xy+saturate(su+float2(.008,0))*_UvRect.zw).r;ad.r=lerp(ad.r,red,step(.01,glitch));
                float s0=1.0-step(.5,k),s1=step(.5,k)-step(1.5,k),s2=step(1.5,k)-step(2.5,k),s3=step(2.5,k)-step(3.5,k),s4=step(3.5,k);
                float3 accent=float3(1.0,.30,.85)*(s0+s3+s4)+float3(.35,.95,1.0)*(s1+s2);
                // the chip card's foil: a rainbow sheen sweeping across its bright parts
                float lum=dot(ad,float3(.3,.59,.11));float sh=frac((uv.x*aspect+uv.y)*.35-t*.12);
                float band=smoothstep(0.0,.06,sh)*smoothstep(.16,.06,sh);float hue=frac(uv.x*.7+uv.y*.4+t*.05);
                float3 rainbow=saturate(abs(frac(hue+float3(0.0,.333,.667))*6.0-3.0)-1.0);
                ad+=s2*band*rainbow*(.12+lum)*.7;
                // the vertical signs: a chase down the letters, then all lit; the corner sign's E stutters now and then
                float letters=lerp(4.0,5.0,s4),top=lerp(16.0,12.5,s4),step1=lerp(23.0,19.0,s4);
                float idx=clamp(floor(((1.0-uv.y)*100.0-top)/step1+.5),0.0,letters-1.0);
                float lead=step(idx,chase)+step(letters,chase);float litLetter=lerp(.26,1.0,saturate(lead));
                float burst=step(frac(t/7.0),.09)*s4*step(abs(idx-2.0),.5);float stutter=lerp(1.0,.15+.85*step(.5,RoofHash2(float2(floor(t*25.0),3.0))),burst);
                ad*=lerp(1.0,litLetter*stutter,s3+s4);
                // the Siren's own loop: a heartbeat drawn by a wiping cursor over a faint grid, the skull dim behind
                float head=frac(t*.45);float age=frac(head-uv.x);float fade=saturate(1.0-age/.85);
                float trace=.47+.30*Ecg(frac(uv.x*2.0));float d=abs(uv.y-trace);
                float core=1.0-smoothstep(py*1.5,py*1.5+.008,d);float halo=exp(-d*30.0)*.35;
                float grid=max(1.0-smoothstep(0.0,py*1.5,abs(frac(uv.y*8.0+.5)-.5)/8.0),1.0-smoothstep(0.0,py*1.5,abs(frac(uv.x*aspect*8.0+.5)-.5)/8.0));
                float3 heart=float3(.03,.008,.05)+accent*grid*.05+accent*(core*2.2+halo*1.3)*fade+float3(1.0,.9,1.0)*core*step(age,.02)+ad*.22;
                // Chip Arena's own loop: an equalizer, eighteen segmented bars cyan to magenta
                float bx=uv.x*18.0;float bid=floor(bx);float hb=RoofHash2(float2(bid,7.0));
                float level=.12+.72*abs(sin(t*(2.0+3.0*hb)+bid*1.3))*(.55+.45*RoofHash2(float2(bid,3.0)));
                float bar=step(abs(frac(bx)-.5),.34)*step(.08,uv.y)*step(uv.y,.08+level*.84)*step(.22,frac(uv.y*16.0));
                float3 eq=float3(.004,.03,.04)+lerp(float3(.35,.95,1.0),float3(1.0,.30,.85),uv.y)*bar*.75+ad*.18;
                float3 content=lerp(ad,heart*s0+eq*s1+ad*(1.0-s0-s1),cut);
                // the border: an inset line and the corner brackets, the holo sweep with its fading trail
                float cx=min(uv.x,1.0-uv.x)*aspect,cy=min(uv.y,1.0-uv.y);float dm=min(cx,cy);
                float inset=1.0-smoothstep(py*1.2,py*1.2+.003,abs(dm-.016));
                float bracket=step(min(cx,cy),.011)*step(max(cx,cy),.10);
                float sv=1.0-sweep;float on=step(0.0,sweep);
                float swept=exp(-pow((uv.y-sv)/.012,2.0))*1.2+exp(-max(uv.y-sv,0.0)/.12)*step(sv,uv.y)*.15;
                float scan=.82+.18*sin(i.uv.y*900.0);float roll=.9+.1*smoothstep(0.0,.08,abs(frac(i.uv.y-_RoofTime*.07)-.5));
                float edge=smoothstep(0.0,.02,i.uv.x)*smoothstep(1.0,.98,i.uv.x)*smoothstep(0.0,.02,i.uv.y)*smoothstep(1.0,.98,i.uv.y);
                if(_ArenaToon.x>0.5){scan=lerp(scan,1.0,.65);edge=smoothstep(0.0,.006,i.uv.x)*smoothstep(1.0,.994,i.uv.x)*smoothstep(0.0,.006,i.uv.y)*smoothstep(1.0,.994,i.uv.y);}
                float3 c=content*_Gain*pulse*scan*roll*edge+accent*(inset*.5+bracket*1.3+swept*on*(1.0-_Calm*.6))*pulse+float3(.01,.02,.04);
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
