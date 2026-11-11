Shader "ChipArena/NativeRooftopScreen"
{
    // A holo screen of the home screen's rooftop city: its picture (rasterised by NativeIconRaster from Core NativeRooftopAds /
    // NativeRooftopArt markup, or the game's logo) in HDR, with scanlines, a slow roll, a rare glitch, soft edges and the city
    // fog. _UvRect picks the picture's sub-rectangle. The anime style (_ArenaToon.x on, NativeLighting.Rooftop): flatter
    // scanlines and a crisp edge, so the ad reads as clean flat colour fields; the glitch and the roll are kept.
    // Animated (owner 2026-10-29: "the screens better designed on buildings + animated"; 2026-11-11: "Changing animations
    // happening on the TV screens as well"). Core NativeRooftopCity.ScreenAnim writes each frame:
    //  _Anim   x the brightness, y the glitch of a cut (0..1), z the holo sweep's height, w the vertical signs' chase
    //  _Slide  x the slide's age (s), y its length, z 1 on the logo's slide, w the ticker's scroll
    //  _Bands  x the picture's foot, y the title band's foot, z the ticker's top (down the panel, 0 top .. 1 bottom)
    //  _Accent the slide's accent
    //  _Egg    the old TV set: xy the Ricochet card's corner, z how far it is on, w its bounce colour (0..5); _Egg2.x the
    //          seconds since its last bounce
    // _Kind: 0 classes, 1 brand, 2 stages (the rotating slides), 3 banner, 4 sign (the vertical signs), 5 retro (the TV set).
    // A slide plays in: the picture wipes on behind a lit edge and eases back from a push-in, the mask (the texture's alpha:
    // a pilot's silhouette and its class icon, a stage's emblem) strikes in from the right in the accent with speed lines, a
    // flash and a punch, the title slides in along its band, the ticker scrolls; the logo takes a glint across it and a slow
    // glow. A slide changes through a glitch cut. The vertical signs light letter by letter. The TV set shows static in fat
    // scanlines; now and then the old RICOCHET card bounces round it, tinted anew at every edge. Branch free but for the style.
    Properties { _MainTex("Ad",2D)="black"{} _Gain("Gain",Float)=1.5 _UvRect("Uv rect",Vector)=(0,0,1,1) _Calm("Reduced effects",Float)=0 _Kind("Kind",Float)=0 _Anim("Animation",Vector)=(1,0,-1,0)
        _Slide("Slide",Vector)=(9,10,0,0) _Bands("Bands",Vector)=(1,1,1,0) _Accent("Accent",Vector)=(.37,.96,1,0) _Egg("Egg",Vector)=(0,0,0,0) _Egg2("Egg 2",Vector)=(9,0,0,0) }
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
            sampler2D _MainTex;float _Gain,_Calm,_Kind;float4 _UvRect,_Anim,_Slide,_Bands,_Accent,_Egg,_Egg2;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            float4 Ad(float2 u,float2 dx,float2 dy){return tex2Dgrad(_MainTex,_UvRect.xy+saturate(u)*_UvRect.zw,dx,dy);}
            float3 Tone(float h){return saturate(abs(frac(h/6.0+float3(0.0,.333,.667))*6.0-3.0)-1.0);}
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;float k=_Kind;float t=_RoofTime;float pulse=_Anim.x,cut=_Anim.y,sweep=_Anim.z,chase=_Anim.w;
                float age=_Slide.x,len=max(_Slide.y,.1),logo=_Slide.z;
                float aspect=_UvRect.z/max(_UvRect.w,1e-3);float py=max(fwidth(uv.y),1e-4);
                float2 dx=ddx(uv)*_UvRect.zw,dy=ddy(uv)*_UvRect.zw;
                float slides=1.0-step(2.5,k),s3=step(2.5,k)-step(3.5,k),s4=step(3.5,k)-step(4.5,k),retro=step(4.5,k);
                // the glitch: the rare one, and through each cut (rows torn sideways, red split off)
                float glitch=max(step(.985,RoofHash2(float2(floor(t*8.0),1.0))),cut)*(1.0-_Calm)*(1.0-retro);
                float shift=(RoofHash2(float2(floor(uv.y*24.0),floor(t*20.0)))-.5)*.10*glitch+glitch*.01*sin(uv.y*80.0);
                float2 su=float2(uv.x+shift,uv.y);float vt=1.0-su.y;
                // a slide's bands: the picture (pushed in, easing back), the title (sliding in), the ticker (scrolling)
                float inPic=step(vt,_Bands.x)*slides,inTitle=step(_Bands.x,vt)*step(vt,_Bands.y)*slides,inTick=step(_Bands.z,vt)*slides;
                float zoom=1.0+.07*exp(-age*1.4)*(1.0-_Calm);float2 pc=float2(.5,1.0-_Bands.x*.5);
                float te=1.0-pow(1.0-saturate((age-.25)/.5),3.0);
                float2 fu=lerp(su,(su-pc)/zoom+pc,inPic);fu=lerp(fu,float2(su.x+(1.0-te)*.7,su.y),inTitle);fu=lerp(fu,float2(frac(su.x+_Slide.w),su.y),inTick);
                float4 tx=Ad(fu,dx,dy);float3 ad=tx.rgb; // an sRGB texture: sampled linear
                float red=Ad(fu+float2(.008,0.0),dx,dy).r;ad.r=lerp(ad.r,red,step(.01,glitch));
                float3 accent=lerp(float3(1.0,.30,.85)*(s3+s4)+float3(.35,.95,1.0)*retro,_Accent.rgb,slides);
                // the picture wipes on behind a lit edge
                float front=age/.5*1.3-(su.x*.8+vt*.2);float wipe=smoothstep(0.0,.05,front);
                ad*=lerp(1.0,wipe,inPic);ad+=accent*inPic*smoothstep(.0,.03,front)*smoothstep(.09,.03,front)*1.5;
                // the mask strikes in from the right: speed lines, a shadow, the accent with a white core, a flash and a punch
                float se=1.0-pow(1.0-saturate((age-.4)/.4),3.0);float hit=max(age-.8,0.0);
                float punch=1.0+.2*exp(-hit*9.0)*step(.8,age);float2 mc=float2(.5,1.0-_Bands.x*.53);
                float2 mu=(su-mc)/punch+mc+float2((1.0-se)*.75,0.0);float my=step(1.0-mu.y,_Bands.x)*step(.4,age)*(1.0-logo)*slides;
                float m=Ad(mu,dx,dy).a*my,shade=Ad(mu+float2(-.014,.014),dx,dy).a*my;
                float lines=step(.82,RoofHash2(float2(floor(su.y*46.0),floor(age*30.0))))*se*(1.0-se)*4.0*inPic*step(.4,age);
                ad=lerp(ad,ad*.2,shade*.65);ad=lerp(ad,lerp(accent,float3(1.0,1.0,1.0),smoothstep(.6,.92,m))*1.2,smoothstep(.06,.5,m));
                ad+=accent*(lines*.5+m*exp(-abs(age-.82)*13.0)*2.2+smoothstep(.02,.3,m)*.25*exp(-abs(frac(t*.5)-su.y)*14.0));
                // the logo's glint and slow glow
                float lum=dot(ad,float3(.3,.59,.11));float gl=frac((uv.x+uv.y*.4)*.5-t*.16);
                ad+=logo*slides*ad*(smoothstep(0.0,.04,gl)*smoothstep(.12,.04,gl)*2.2*smoothstep(.05,.3,lum)+.12*sin(t*1.9));
                // the vertical signs: a chase down the letters, then all lit; the corner sign's E stutters now and then
                float letters=lerp(4.0,5.0,s4),top=lerp(16.0,12.5,s4),step1=lerp(23.0,19.0,s4);
                float idx=clamp(floor(((1.0-uv.y)*100.0-top)/step1+.5),0.0,letters-1.0);
                float lead=step(idx,chase)+step(letters,chase);float litLetter=lerp(.26,1.0,saturate(lead));
                float burst=step(frac(t/7.0),.09)*s4*step(abs(idx-2.0),.5);float stutter=lerp(1.0,.15+.85*step(.5,RoofHash2(float2(floor(t*25.0),3.0))),burst);
                ad*=lerp(1.0,litLetter*stutter,s3+s4);
                // the old TV set: coarse pixels, static, a rolling bar; the Ricochet card bouncing, tinted at each edge
                float2 q=(floor(uv*float2(128.0,96.0))+.5)/float2(128.0,96.0);float card=_Egg.z;
                float snow=RoofHash2(q*float2(91.7,53.3)+floor(t*24.0)*float2(7.1,3.3));
                float bar=smoothstep(.12,0.0,abs(frac(q.y+t*.11)-.5))*.5;
                float2 cu=(q-_Egg.xy*(1.0-.42))/.42;float inside=step(0.0,cu.x)*step(cu.x,1.0)*step(0.0,cu.y)*step(cu.y,1.0)*card;
                float3 cardRgb=tex2Dlod(_MainTex,float4(_UvRect.xy+saturate(cu)*_UvRect.zw,0.0,1.0)).rgb;float cl=dot(cardRgb,float3(.3,.59,.11));
                float3 tint=lerp(float3(1.0,1.0,1.0),Tone(_Egg.w+.5),.7);cardRgb=lerp(cardRgb,cl*tint*2.2,.75)+tint*exp(-_Egg2.x*7.0)*.35;
                float rim=inside*(1.0-step(.03,min(min(cu.x,1.0-cu.x),min(cu.y,1.0-cu.y))));
                float3 tv=float3(.05,.07,.10)*(.35+snow*(1.3-card*.9))+float3(.10,.13,.17)*bar*(1.0-card*.6);
                tv=lerp(tv,cardRgb+tint*rim*.8,inside)+float3(.5,.55,.6)*snow*cut;
                tv*=(.72+.28*step(.5,frac(uv.y*96.0)))*smoothstep(.75,.3,length(uv-.5));
                float3 content=lerp(ad,tv,retro);
                // the border: an inset line and the corner brackets, the holo sweep with its fading trail
                float cx=min(uv.x,1.0-uv.x)*aspect,cy=min(uv.y,1.0-uv.y);float dm=min(cx,cy);
                float inset=1.0-smoothstep(py*1.2,py*1.2+.003,abs(dm-.016));
                float bracket=step(min(cx,cy),.011)*step(max(cx,cy),.10);
                float sv=1.0-sweep;float on=step(0.0,sweep);
                float swept=exp(-pow((uv.y-sv)/.012,2.0))*1.2+exp(-max(uv.y-sv,0.0)/.12)*step(sv,uv.y)*.15;
                float scan=.82+.18*sin(i.uv.y*900.0);float roll=.9+.1*smoothstep(0.0,.08,abs(frac(i.uv.y-_RoofTime*.07)-.5));
                float edge=smoothstep(0.0,.02,i.uv.x)*smoothstep(1.0,.98,i.uv.x)*smoothstep(0.0,.02,i.uv.y)*smoothstep(1.0,.98,i.uv.y);
                if(_ArenaToon.x>0.5){scan=lerp(scan,1.0,.65);edge=smoothstep(0.0,.006,i.uv.x)*smoothstep(1.0,.994,i.uv.x)*smoothstep(0.0,.006,i.uv.y)*smoothstep(1.0,.994,i.uv.y);}
                scan=lerp(scan,1.0,retro);roll=lerp(roll,1.0,retro);
                float3 c=content*_Gain*pulse*scan*roll*edge+accent*(inset*.5+bracket*1.3+swept*on*(1.0-_Calm*.6))*pulse*(1.0-retro)+float3(.01,.02,.04);
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
