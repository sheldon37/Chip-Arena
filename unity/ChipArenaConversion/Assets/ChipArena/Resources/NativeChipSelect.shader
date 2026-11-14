// The PvE chip selection's wrist hologram (Core/NativeChipSelect.cs NativeChipSelectHolo builds it; Runtime/
// NativeChipSelectPresentation.cs draws it): one dynamic world-space mesh for every selecting pilot (and one for the
// first-person view model). Additive hardlight, unlit, depth-tested, no depth write, both faces; the scene fog fades it.
//   COLOR      rgb: the part's colour as the screen shows it (sRGB 0..1: cyan, a rarity, danger red), a: its opacity
//   TEXCOORD0  xy: the quad's uv (0..1), z: the kind (0 panel, 1 card, 2 projection cone, 3 mote, 4 ring, 5 line), w: a
//              parameter (panel: the unfold; card: 1 lit; cone: 1 the faint back sheet; ring: its thickness; line: 1 across x)
//   TEXCOORD1  x: HDR gain (over 1 the bloom picks it up), y: a seed (the scan's phase per hologram)
// _ChipHolo: x the presentation clock, y the scanline travel (0 with Reduced effects), z unused, w 1 on the low graphics tier
// (no panel grid, no card pads). Every pattern is drawn here from the uv, so the mesh is plain quads.
Shader "ChipArena/NativeChipSelect"
{
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _ChipHolo;
            float3 ToLinear(float3 c){c=max(c,0.);return lerp(c/12.92,pow((c+.055)/1.055,2.4),step(.04045,c));}
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 extra:TEXCOORD1;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float4 extra:TEXCOORD1;UNITY_FOG_COORDS(2)};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.extra=v.extra;UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            // A chamfered box (half size b, corner cut c) and a rounded one (radius r): signed distances.
            float Chamfer(float2 p,float2 b,float c){float2 q=abs(p)-b;return max(max(q.x,q.y),(abs(p.x)+abs(p.y)-(b.x+b.y-c))*.7071);}
            float Rounded(float2 p,float2 b,float r){float2 q=abs(p)-b+r;return length(max(q,0.))+min(max(q.x,q.y),0.)-r;}
            float Line(float d,float w){return 1.-smoothstep(0.,w,abs(d));}
            float Box(float2 p,float2 c,float2 h){float2 q=abs(p-c)-h;return step(max(q.x,q.y),0.);}
            // The chip cartridge (Core/NativeChipCartridge.cs): a box with its shoulders cut deep and its lower corners shallow
            // (y up), and a pointy-top hexagon (apothem a).
            float Cartridge(float2 p){float2 q=float2(abs(p.x),p.y-.02);float box=max(q.x-.36,abs(q.y)-.44);return max(box,max((q.x+q.y-.68)*.7071,(q.x-q.y-.74)*.7071));}
            float Hexagon(float2 p,float a){p=abs(p);return max(p.x,p.x*.5+p.y*.866)-a;}
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv.xy;float kind=i.uv.z,param=i.uv.w,t=_ChipHolo.x,low=_ChipHolo.w;
                // Scanlines across every part, travelling down slowly (still with Reduced effects).
                float scan=.72+.28*step(.45,frac(uv.y*26.-t*_ChipHolo.y*1.6+i.extra.y*7.));
                float a=0;
                if(kind<.5)
                {
                    // The panel: a chamfered hardlight frame (aspect 1.6), corner brackets, a faint fill and a dot grid.
                    float2 p=(uv-.5)*float2(1.6,1.);float d=Chamfer(p,float2(.78,.48),.12);
                    float edge=Line(d+.018,.016),inside=step(d,0.);
                    float2 corner=abs(p)-float2(.66,.36);float brackets=step(0.,corner.x)*step(0.,corner.y)*Line(d+.045,.012);
                    float2 g=frac(p*float2(14.,14.))-.5;float grid=(1.-low)*inside*(1.-smoothstep(.05,.12,length(g)))*.35;
                    a=edge+brackets*.9+inside*(.14+grid*.5);a*=scan;
                }
                else if(kind<1.5)
                {
                    // A card: the chip cartridge (aspect .76) with its rim, the hex core on its top-left shoulder, the name and two text
                    // bars, and the edge connector's pads under it (a key gap a third along); brighter when lit.
                    float2 p=(uv-.5)*float2(.76,1.);float d=Cartridge(p);float inside=step(d,0.);
                    float rim=Line(d+.025,.022);
                    float hd=Hexagon(p-float2(-.21,.31),.1);float core=step(hd,-.03)*.75+Line(hd,.018)*.8;
                    float bars=Box(p,float2(.1,.31),float2(.14,.032))*.8+Box(p,float2(0,.0),float2(.27,.024))*.6+Box(p,float2(-.04,-.11),float2(.23,.02))*.5;
                    float pads=0;if(low<.5){for(int k=-3;k<=3;k++){if(k!=-1)pads+=Box(p,float2(k*.075,-.455),float2(.024,.03));}}
                    a=rim*(1.+param*.6)+inside*(.22+.16*param)+(core+bars)*inside*(.55+.45*param)+pads*.6*(.55+.45*param);a*=scan;
                }
                else if(kind<2.5)
                {
                    // The projection cone: brightest at the emitter, fading to the panel, soft at its sides, with light travelling up.
                    float side=sin(uv.x*3.14159);float along=pow(1.-uv.y,1.4);float bands=.8+.2*sin(uv.y*38.-t*7.);
                    a=along*(.35+.65*side)*bands*(param>.5?.6:1.);
                }
                else if(kind<3.5){float2 c=uv*2.-1.;a=exp(-dot(c,c)*5.);}
                else if(kind<4.5){float r=length(uv*2.-1.);a=Line(r-.78,param*.5)+(1.-smoothstep(0.,.3,r))*.35*step(.2,param);}
                else{float across=param>.5?uv.x:uv.y;float c=1.-abs(across*2.-1.);a=c*c*(.75+.25*sin((param>.5?uv.y:uv.x)*30.-t*5.));}
                float4 col=float4(i.color.rgb,saturate(i.color.a*a));
                // Added light fades out into the fog (toward nothing, as an additive part should).
                UNITY_APPLY_FOG_COLOR(i.fogCoord,col,float4(0,0,0,0));
                float3 c=col.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=ToLinear(c);
                #endif
                return float4(c*col.a*i.extra.x,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
