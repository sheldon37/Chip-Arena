// The chip draft's animated cyber backdrop (Core/NativeDraftBackdrop.cs, Runtime/NativeDraftBackdropPresentation.cs). One quad
// in front of the draft camera, drawn first (Background queue, no depth write), so the pilot, the held chip and the plant pulse
// draw over it. Each pixel casts its view ray into a data corridor running away behind the seated pilot (Unity world: the
// floor at BACK_FLOOR, the walls BACK_HALF either side of the pilot, rays reaching BACK_FAR), so every line of it converges
// behind the pilot's head, and shades whichever surface it meets first:
//   floor: a 1 m circuit grid with data lanes every 1.5 m and cross rows every 3 m (filtered: they fade instead of moireing as
//          they thin), packets running down the lanes, a lit trim along each wall's foot;
//   walls: server panels (PvP) or the ward's hexes (PvE / Siren pick) with soft seams, a few panels lighting and dimming, ribs
//          every 6 m, two light rails with packets, and (full tier) glyph streams falling down a few columns;
//   gates: two frames of light (the scan) running down the corridor every 7.5 s; the plant's ring; the power-on front;
//   haze:  the corridor fades into navy haze that brightens toward its far end, behind the pilot's head;
//   air:   (full tier) binary motes drifting up on two planes between the pilot and the far end, twinkling (never flashing);
//   glow:  a soft screen-space glow behind the pilot's chest and head, so the silhouette reads.
// The frame (_BackFrame: grid, seams, ribs, hexes) is cyber cyan; the light (_BackAccent: trims, rails, packets, gates, glow,
// far haze) the stage tint. Two clocks: the calm one (_BackCam.w: breathing, panels, drift) and the flow one (_BackClock.x:
// packets, streams, scan, twinkle), which the Core integrates, so the pick's inflow speeds it up and Reduced effects stops it
// without a jump. Under the heading and over the tray the detail is kept to _BackSafe.z / .w and the brightest channel capped
// (_BackCap), the formulas NativeDraftBackdrop.SafeAt / CapAt mirror; the column behind the head (_BackCap.w) keeps the glow.
// Colours are linear (gamma-space builds encode at the end). No fog, lighting or shadows: nothing here reads or changes the
// stage's lighting state. The block between the SHARED markers is plain enough to compile as GLSL too (the offline draft
// renders): float literals, no fmod / atan2 / mul / static constants, and derivatives outside any varying branch.
// The anime style (_BackToon.x on: NativeDraftBackdropPresentation sets it from Core NativeMenuStyle.DraftToon, off under the
// `--ca-map-style legacy` command line; docs/MAP-STYLE-ANIME.md section 6): the corridor closes on a painted gradient back
// wall BACK_TOON_WALL m behind the pilot, its server panels (the ward's hexes) take flat tones lit in cel bands by the far
// glow, the haze and both glows step into soft bands, and ink lines come from the corridor's own distance field (the
// floor-wall crease, the back wall's edge, the panel seams). Every neon element (trims, rails, packets, gates, streams,
// motes) keeps its light, and the text-safe bands and the cap apply as before. Off, every term is the legacy code exactly.
Shader "ChipArena/NativeDraftBackdrop"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" "PreviewType"="Plane" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_local __ DRAFT_BACKDROP_LOW
            #include "UnityCG.cginc"
            // [unroll] for the derivatives in the motes' loop (the offline GLSL build defines BACK_UNROLL empty).
            #define BACK_UNROLL [unroll]
            float4 _BackCam,_BackClock,_BackFwd,_BackUp,_BackRight,_BackAccent,_BackFrame,_BackCounter,_BackDeep,_BackVoid,_BackPulse,_BackStyle,_BackFx,_BackSafe,_BackCap,_BackGlow;
            // The anime style's switch (x 1 on, 0 the legacy look), set once with the material.
            float4 _BackToon;
            // ---- DRAFT BACKDROP SHARED BEGIN
            #define BACK_FLOOR 0.0
            #define BACK_HALF 4.6
            #define BACK_FAR 400.0
            float BackHash(float2 p){float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
            float BackSq(float x){return x*x;}
            // A filtered grid line along one coordinate (v in cells, w its screen footprint in cells): 1 on the line, px pixels
            // wide, fading out as the cells shrink under about three pixels (far surfaces keep only their haze: no moire).
            float BackLine(float v,float w,float px){return (1.0-min(abs(frac(v+.5)-.5)/(w*px),1.0))*saturate(1.4-w*3.0);}
            // A world-space strip of half width h (m) round x = 0, at least ~a pixel wide (w the footprint in m) and dimmed to keep
            // its energy as it thins, so a far trim neither sparkles nor vanishes.
            float BackStrip(float x,float h,float w){float k=max(h,w*.8);return exp(-BackSq(x/k))*min(1.0,h/k*1.6);}
            // The nearest cell of a hex lattice (p in cells): xy the offset from its centre, zw its id; the edge distance of an offset.
            float4 BackHex(float2 p){float2 s=float2(1.0,1.7320508);float4 c=floor(float4(p,p-float2(.5,1.0))/s.xyxy)+.5;float4 h=float4(p-c.xy*s,p-(c.zw+.5)*s);return dot(h.xy,h.xy)<dot(h.zw,h.zw)?float4(h.xy,c.xy):float4(h.zw,c.zw+.5);}
            float BackHexEdge(float2 q){q=abs(q);return .5-max(dot(q,float2(.5,.8660254)),q.x);}
            // A binary digit in a cell (q in -.5..5, y up): kind < .5 a one, else a zero; aa the filter width.
            float BackGlyph(float2 q,float kind,float aa)
            {
                float one=max(abs(q.x)-.075,abs(q.y)-.32),zero=abs(length(q*float2(1.6,1.0))-.26)-.07;
                return 1.0-smoothstep(-aa,aa,kind<.5?one:zero);
            }
            // A packet travelling along u (0..1 per lane period, the head at the top): a short bright head with a fading tail.
            float BackPacket(float u){return smoothstep(.80,.985,u)*(1.0-smoothstep(.985,1.0,u));}
            float3 BackHue(float h){return saturate(abs(frac(h+float3(0.0,.6666667,.3333333))*6.0-3.0)-1.0);}
            // The anime style's constants (Core NativeMenuStyle mirrors them): the back wall's distance (m) and the height its
            // painted gradient spans, the height the panels' tones step over, the ink's half width (pixels), the soft bands' count
            // and riser, the panels' two flat tones and the floor's (times the navy), how much the far glow's banded light lifts
            // them, the back wall's navy top (times the navy) and its tint glow, the ink's tint.
            #define BACK_TOON_WALL 52.0
            #define BACK_TOON_WALL_HIGH 9.0
            #define BACK_TOON_PANEL_HIGH 3.2
            #define BACK_TOON_LINE 1.0
            #define BACK_TOON_BANDS 4.0
            #define BACK_TOON_RISER 0.15
            #define BACK_TOON_PANEL_A 1.5
            #define BACK_TOON_PANEL_B 2.1
            #define BACK_TOON_FLOOR 1.3
            #define BACK_TOON_LIGHT 1.4
            #define BACK_TOON_SKY 0.9
            #define BACK_TOON_GLOW 0.24
            #define BACK_TOON_INK float3(0.18,0.2,0.42)
            // Soft stairs: x (0..1) in `n` flat bands with a riser BACK_TOON_RISER wide either side of each step.
            float BackBands(float x,float n){float k=saturate(x)*n;float f=floor(k);return min(1.0,(f+smoothstep(.5-BACK_TOON_RISER,.5+BACK_TOON_RISER,k-f))/n);}
            float3 BackShade(float3 ray)
            {
                float3 d=normalize(ray);float3 eye=_BackCam.xyz;float t=_BackCam.w,f=_BackClock.x;
                float fz=max(dot(d,_BackFwd.xyz),1e-4);float row=.5-.5*dot(d,_BackUp.xyz)/(fz*_BackFwd.w);float col=.5+.5*dot(d,_BackRight.xyz)/(fz*_BackFwd.w*_BackUp.w);
                float3 A=_BackAccent.rgb;float3 S=_BackFrame.rgb;float3 B=_BackCounter.rgb;float ink=_BackCounter.w;float ward=_BackStyle.x;
                // The corridor: the floor, and the side wall the ray turns toward, whichever it meets first.
                float sx=d.x>=0.0?1.0:-1.0;float tf=(BACK_FLOOR-eye.y)/min(d.y,-1e-4),ts=(sx*BACK_HALF-eye.x)/(sx*max(abs(d.x),1e-4));
                float onFloor=step(tf,ts);float th=min(min(tf,ts),BACK_FAR);float3 pf=eye+d*min(tf,BACK_FAR);float3 pw=eye+d*min(ts,BACK_FAR);
                float depth=-lerp(pw.z,pf.z,onFloor);
                // The anime style's back wall: met first beyond BACK_TOON_WALL m, it closes the corridor.
                float toon=_BackToon.x;float tw=(-BACK_TOON_WALL-eye.z)/min(d.z,-1e-4);float onBack=0.0;
                if(toon>0.5){onBack=step(tw,min(tf,ts));th=lerp(th,min(tw,BACK_FAR),onBack);depth=lerp(depth,BACK_TOON_WALL,onBack);}
                // The rainbow pick: the accent turns slowly through the hues down the corridor.
                float3 prism=lerp(BackHue(frac(depth*.03+t*.02)),float3(1.0,1.0,1.0),.2);A=lerp(A,prism,_BackStyle.y);S=lerp(S,prism,_BackStyle.y*.6);float3 hot=lerp(lerp(A,float3(1.0,1.0,1.0),.4),B,ink*.6);
                float pulseW=1.0-_BackDeep.w*(.5-.5*sin(t*.55-depth*.32));
                // ---- floor: a 1 m grid, data lanes every 1.5 m running away down the corridor and cross rows every 3 m.
                float2 g=float2(pf.x,-pf.z);float2 gw=fwidth(g);float fwm=max(gw.x,gw.y);
                float minor=max(BackLine(g.x,gw.x,1.0),BackLine(g.y,gw.y,1.0)),laneX=BackLine(g.x/1.5,gw.x/1.5,1.6),rowZ=BackLine(g.y/3.0,gw.y/3.0,1.5);
                float lane=floor(g.x/1.5+.5),hl=BackHash(float2(lane,3.1));float pz=BackPacket(frac((g.y-f*(2.2+1.8*hl))/11.0+hl))*laneX*step(hl,.7);
                float3 floorC=S*(minor*.010+max(laneX,rowZ)*.026)*pulseW+hot*pz*.34*_BackFx.x;
                floorC+=A*.09*BackStrip(BACK_HALF-abs(pf.x),.035,fwm);
                // ---- walls: server panels (2 x .8 m; the ward's hexes), two light rails with packets, a few panels lit, the
                // structural ribs every 6 m.
                float2 v=float2(-pw.z,pw.y);float2 vw=fwidth(v);float vwm=max(vw.x,vw.y);
                float2 pr=v/float2(2.0,.8);float rect=max(BackLine(pr.x,vw.x/2.0,1.0),BackLine(pr.y,vw.y/.8,1.0));
                float4 hx=BackHex(v/.6);float hexL=(1.0-smoothstep(0.0,vwm/.6*1.3,BackHexEdge(hx.xy)))*saturate(1.4-vwm/.6*3.0);
                float seam=lerp(rect,hexL,ward);float2 cell=lerp(floor(pr),hx.zw,ward)+float2(sx*17.0,0.0);float hp=BackHash(cell+11.0);
                float lit=step(.84,hp)*smoothstep(.5,1.0,.5+.5*sin(t*(.22+.35*hp)+hp*40.0));float rib=BackLine(v.x/6.0,vw.x/6.0,2.2);
                float rails=BackStrip(v.y-.32,.025,vwm)+BackStrip(v.y-2.75,.025,vwm);float hr=BackHash(float2(sx,floor(v.y)));
                float pr2=BackPacket(frac((v.x-f*(3.0+hr))/13.0+hr+sx*.37))*rails;
                float3 wallC=S*(seam*.022*pulseW+rib*.022)+A*(lit*.022+rails*.05)+hot*pr2*.30*_BackFx.x;
                wallC+=A*.09*BackStrip(pw.y-BACK_FLOOR,.035,vwm);
                #ifndef DRAFT_BACKDROP_LOW
                // Glyph streams falling down a few wall columns, the head of each brightest.
                float k=floor(v.x/.26),hk=BackHash(float2(k,5.7+sx));float2 sq=float2(frac(v.x/.26)-.5,0.0);float gy=(v.y+f*(.4+.5*hk))/.26;sq.y=frac(gy)-.5;
                float tail=frac((v.y+f*(.4+.5*hk))/(1.8+2.0*hk)+hk);float stream=BackGlyph(sq*1.3,BackHash(float2(k,floor(gy))),vw.x/.26*1.8)*exp(-tail*3.0)*step(hk,.14);
                wallC+=lerp(S,hot,.5)*stream*.07*_BackFx.z*saturate(1.6-vw.x/.26*2.2)*saturate(v.y*2.0);
                #endif
                float lines=lerp(max(seam,rails),max(laneX,rowZ),onFloor);float3 detail=lerp(wallC,floorC,onFloor);
                // ---- gates: two frames of light running away down the corridor (the scan), the plant's ring, the power-on front.
                float gwid=.12+th*.005;float gate=0.0;
                BACK_UNROLL for(int n=0;n<2;n++){float zg=frac(f/7.5+float(n)*.5)*46.0+1.0;gate+=exp(-BackSq((depth-zg)/gwid))*(1.0-zg/48.0);}
                float wave=exp(-BackSq((depth-_BackPulse.x)/(.5+th*.012)))*_BackPulse.y;
                float boot=saturate((_BackAccent.w-depth)/2.0),front=exp(-BackSq((depth-_BackAccent.w)/(.4+th*.006)))*(1.0-_BackRight.w);
                detail=detail*boot+A*(gate*_BackFx.y*(.03+.10*lines)+wave*(.10+.35*lines)+front*(.05+.15*lines));
                // ---- haze: the corridor fades into a navy haze that brightens toward its far end, behind the pilot's head.
                float fog=1.0-exp(-th*.032);float axis=exp(-(d.x*d.x+BackSq(d.y+.01))/.004);
                float3 haze=_BackDeep.rgb*(1.0+1.6*axis)+A*(.004+.03*axis)*pulseW;
                float3 nearC=lerp(_BackVoid.rgb,_BackDeep.rgb,.35);
                if(toon>0.5)
                {
                    // Flat-toned panels (two tones by panel, a third on the floor) lit by the far glow in cel bands down the corridor;
                    // the haze and the far glow in soft bands; the back wall a painted gradient (the stage tint's glow low, navy up
                    // high, in bands) with rings of light behind the pilot's head; ink where the corridor's distance field folds.
                    float lightB=BackBands(depth/BACK_TOON_WALL,BACK_TOON_BANDS);float panel=lerp(BACK_TOON_PANEL_A,BACK_TOON_PANEL_B,BackBands(v.y/BACK_TOON_PANEL_HIGH,3.0))*(.92+.16*step(.5,hp));
                    nearC=_BackDeep.rgb*lerp(panel,BACK_TOON_FLOOR,onFloor)*(1.0+BACK_TOON_LIGHT*lightB)+A*.004*lightB;
                    float axisB=BackBands(axis,3.0);fog=lerp(BackBands(fog,BACK_TOON_BANDS),fog,onBack)*(1.0-.55*onBack);
                    float3 pb=eye+d*tw;float rise=BackBands(1.0-saturate((pb.y-BACK_FLOOR)/BACK_TOON_WALL_HIGH),3.0);
                    float3 paint=lerp(_BackDeep.rgb*BACK_TOON_SKY,_BackDeep.rgb*BACK_TOON_PANEL_A+A*BACK_TOON_GLOW,rise)+A*BACK_TOON_GLOW*axisB*pulseW;
                    haze=lerp(_BackDeep.rgb*(1.0+1.6*axisB)+A*(.004+.03*axisB)*pulseW,paint,onBack);
                    float df=tf-ts,db=min(tf,ts)-tw;float crease=(1.0-smoothstep(BACK_TOON_LINE-.4,BACK_TOON_LINE+.4,abs(df)/max(fwidth(df),1e-5)))*(1.0-onBack);
                    float edge=1.0-smoothstep(BACK_TOON_LINE-.4,BACK_TOON_LINE+.4,abs(db)/max(fwidth(db),1e-5));
                    float ink=max(max(crease,edge),max(seam,rib)*.85*(1.0-onFloor)*(1.0-onBack))*(1.0-fog*.6);
                    nearC*=lerp(float3(1.0,1.0,1.0),BACK_TOON_INK,ink);haze*=lerp(float3(1.0,1.0,1.0),BACK_TOON_INK,edge*(1.0-onBack));
                    // the seams and ribs are ink now, not light; the back wall keeps only a trace of the corridor's detail
                    detail=max(detail-S*(seam*.022*pulseW+rib*.022)*(1.0-onFloor)*boot,0.0);detail*=1.0-onBack*.85;
                }
                float3 c=lerp(nearC,haze,fog);detail*=1.0-fog*.85;
                #ifndef DRAFT_BACKDROP_LOW
                // ---- the air: binary motes on two planes between the pilot and the far corridor, drifting up and twinkling.
                BACK_UNROLL for(int m=0;m<2;m++)
                {
                    float fm=float(m);float zm=-3.4-2.8*fm;float tm=(zm-eye.z)/min(d.z,-1e-4);float3 pm=eye+d*tm;float2 q0=pm.xy/(.36+.1*fm)+float2(fm*13.7,-t*(.08+.03*fm));
                    float2 id=floor(q0);float hm=BackHash(id+fm*7.3);float2 q=frac(q0)-.5-(float2(BackHash(id+1.7),BackHash(id+4.1))-.5)*.4;
                    float tw=smoothstep(.3,1.0,.5+.5*sin(f*(.45+.6*hm)+hm*30.0));float aa=fwidth(q0.x)*3.2;
                    detail+=lerp(S,hot,.3*fm)*BackGlyph(q*3.2,BackHash(id+3.3),aa)*step(.95,hm)*(.032-.008*fm)*lerp(.5,1.0,tw)*_BackFx.w*step(BACK_FLOOR+.2,pm.y)*step(abs(pm.x),BACK_HALF-.2)*saturate(1.6-aa);
                }
                #endif
                // ---- the glow behind the pilot's chest and head, so the silhouette reads.
                float2 gd=float2((col-_BackGlow.x)*_BackUp.w/_BackGlow.z,(row-_BackGlow.y)/_BackGlow.w);float halo=exp(-dot(gd,gd));if(toon>0.5)halo=BackBands(halo,3.0);detail+=A*_BackStyle.w*halo*(.85+.15*pulseW);
                // ---- presence, the plant's flash and the text-safe bands (NativeDraftBackdrop.SafeAt / CapAt)
                detail*=_BackStyle.z*_BackRight.w*(1.0+_BackPulse.z);
                float hb=BackSq((col-.5)*_BackUp.w/max(_BackCap.w,1e-3));float hs=max(smoothstep(_BackSafe.x-.01,_BackSafe.x+.07,row),exp(-hb*hb)),tb=1.0-smoothstep(_BackSafe.y-.06,_BackSafe.y,row);
                float safe=lerp(_BackSafe.z,1.0,hs)*lerp(_BackSafe.w,1.0,tb),cap=min(lerp(_BackCap.x,_BackCap.z,hs),lerp(_BackCap.y,_BackCap.z,tb));
                c=c*lerp(.35,1.0,_BackRight.w)+detail*safe;float peak=max(c.r,max(c.g,c.b));return c*min(1.0,cap/max(peak,1e-6));
            }
            // ---- DRAFT BACKDROP SHARED END
            // The exact sRGB encode for gamma-space builds (the backdrop is authored in linear light).
            float3 BackEncode(float3 c){c=max(c,0.0);return lerp(c*12.92,1.055*pow(c,1.0/2.4)-.055,step(.0031308,c));}
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 pos:SV_POSITION;float3 ray:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.ray=mul(unity_ObjectToWorld,v.vertex).xyz-_BackCam.xyz;return o;}
            float4 frag(v2f i):SV_Target
            {
                float3 c=BackShade(i.ray);
                #ifdef UNITY_COLORSPACE_GAMMA
                c=BackEncode(c);
                #endif
                return float4(c,1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
