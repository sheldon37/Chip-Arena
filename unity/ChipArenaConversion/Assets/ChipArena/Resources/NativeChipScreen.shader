// The Expedition wrist chip screen (Core/NativeChipScreen.cs builds it; Runtime/NativeChipScreenPresentation.cs draws it): one dynamic
// world mesh for every choosing pilot's screen. Owner 2026-10-15: "Holographic display kind of buggy, and display is odd. Can we make it
// like a better holographic display with no artifacting?" The screen is ONE quad drawn by this shader (glass, grid, scanlines, soft rim,
// fresnel sheen, thin frame line and brackets, the header and footer rules, and the card slots inside it), so nothing is stacked or
// coplanar on it; the rays, the emitter ring and the motes are drawn first, behind it, and a pick's flying card last, in front of it.
//  * Alpha-blended glass, premultiplied (Blend One OneMinusSrcAlpha), no depth write, depth-tested, both faces, one render queue:
//    drawn again it converges on its own colour instead of adding up. The scene fog fades it out (its light and its glass).
//  * Light is budgeted in linear HDR (Core NativeChipScreenLight; the CS_* numbers below are the same, read by Tests/ChipScreenChecks):
//    the body sums under NativeBloom's threshold of 1, and only the thin frame line, the brackets, the emitter ring and a lit card's
//    rim go over it, as a soft glow of a few pixels. Lines thinner than a pixel fade instead of breaking up, and the grid and the
//    scanlines fade out before they would alias, so nothing shimmers.
//  * A damaging hit (TEXCOORD1.z, the alert) turns the frame line and the rim red for a moment, never the body.
// The stream (NativeChipScreen.Build): TEXCOORD0 uv (0..1, y up), kind (0 screen, 1 ray, 2 ring, 3 mote, 4 flying card), opacity;
// TEXCOORD1 the screen's width and height (m) as drawn, the alert, the cards' scan (a ray: its width, m); TEXCOORD2 the screen: count +
// 4 x mode (0 blank backs, 1 mine under the HUD's faces, 2 mine without them), the card height as a share of the screen's, the lit
// card, the seed (the rest: x the tint as a packed sRGB hex); TEXCOORD3 each card's visibility; TEXCOORD4 the cards' tints and the tone.
// _ChipScreen: x the presentation clock, y 1 animated (0 with Reduced effects), z unused, w 1 on the low graphics tier (no grid).
// The block between SHARED BEGIN and SHARED END is the whole per-pixel look. An offline harness compiles exactly that block as GLSL
// ES 3.00 (float2/3/4 -> vec2/3/4; lerp, frac and saturate are macros there), so keep it to the common subset: float literals with a
// decimal point, no implicit int/float conversions, no texture reads, no arrays; derivatives come in as arguments (fw, ndv).
Shader "ChipArena/NativeChipScreen"
{
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
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
            float4 _ChipScreen;
// SHARED BEGIN
// The layout (Core NativeChipScreen: the open screen at rig scale 1; only their ratios are used).
#define CS_W 1.06
#define CS_H 0.52
#define CS_CARD_W 0.3
#define CS_CARD_GAP 0.026
#define CS_CARDS_Y (-0.012)
#define CS_DANGER_HEX 16731501.0
// The light budget (Core NativeChipScreenLight), as a share of the tint's brightest channel.
#define CS_BODY_A 0.58
#define CS_BODY 0.05
#define CS_GRID 0.025
#define CS_SCAN 0.02
#define CS_RIM 0.16
#define CS_RIM_W 0.022
#define CS_FRESNEL 0.06
#define CS_SLOT_A 0.35
#define CS_SLOT 0.02
#define CS_GLYPH 0.14
#define CS_FACED_GLOW 0.05
#define CS_RULE 0.35
#define CS_LINE 1.45
#define CS_LINE_W 0.0022
#define CS_BRACKET 1.2
#define CS_CARD_RIM 0.55
#define CS_CARD_LIT 1.05
#define CS_FACED_RIM 0.32
#define CS_FACED_LIT 0.6
#define CS_SCAN_IN 1.0
#define CS_RAY 0.28
#define CS_RAY_A 0.12
#define CS_RING 1.2
#define CS_MOTE 0.3
float CsMod(float x,float y){return x-y*floor(x/y);}
// A packed sRGB hex (NativeChipScreen.Pack) to linear.
float3 CsHex(float h){h=floor(h+0.5);float r=floor(h/65536.0);float g=floor((h-r*65536.0)/256.0);float b=h-r*65536.0-g*256.0;return float3(r,g,b)/255.0;}
float3 CsLinear(float3 c){return lerp(c/12.92,pow((c+0.055)/1.055,float3(2.4,2.4,2.4)),step(float3(0.04045,0.04045,0.04045),c));}
// Signed distances (m): a chamfered box (half size b, corner cut c), a rounded one (radius r), a flat-topped hexagon (apothem a).
float CsChamfer(float2 p,float2 b,float c){float2 q=abs(p)-b;return max(max(q.x,q.y),(abs(p.x)+abs(p.y)-(b.x+b.y-c))*0.7071);}
float CsRounded(float2 p,float2 b,float r){float2 q=abs(p)-b+float2(r,r);return length(max(q,float2(0.0,0.0)))+min(max(q.x,q.y),0.0)-r;}
float CsHexagon(float2 p,float a){p=abs(p);return max(p.x*0.866+p.y*0.5,p.y)-a;}
float CsBox(float2 p,float2 c,float2 h){float2 q=abs(p-c)-h;return max(q.x,q.y);}
// Antialiased at pixel size px (m): inside a distance field, and a band of width w round d = 0 (thinner than a pixel, it keeps its
// energy and fades rather than breaking up into crawling fragments).
float CsFill(float d,float px){return saturate(0.5-d/px);}
float CsLine(float d,float w,float px){float hw=max(w,px)*0.5;return saturate((hw-abs(d))/px+0.5)*min(1.0,w/px);}
// A blank back's emblem (a hexagon core and three bars), in metres from the card's centre (sc: the card's scale).
float CsGlyph(float2 q,float sc,float px)
{
    float hx=CsHexagon(q-float2(-0.095,0.028)*sc,0.022*sc);
    float bars=CsFill(CsBox(q,float2(0.035,0.04)*sc,float2(0.06,0.004)*sc),px)+0.7*CsFill(CsBox(q,float2(0.015,0.014)*sc,float2(0.08,0.003)*sc),px)+0.5*CsFill(CsBox(q,float2(-0.005,-0.012)*sc,float2(0.1,0.003)*sc),px);
    return saturate(CsLine(hx,0.0018*sc,px)+0.45*CsFill(hx+0.008*sc,px)+0.6*bars);
}
// Card k of n inside the screen (p: metres from the screen's centre as drawn, wh its size): premultiplied light and alpha.
float4 CsCard(float2 p,float2 wh,float px,float k,float n,float vis,float tintHex,float lit,float mode,float share)
{
    if(vis<=0.001)return float4(0.0,0.0,0.0,0.0);
    float span=n*CS_CARD_W+(n-1.0)*CS_CARD_GAP;float sc=wh.x/CS_W;
    float2 c=float2((-span*0.5+CS_CARD_W*0.5+k*(CS_CARD_W+CS_CARD_GAP))*sc,CS_CARDS_Y/CS_H*wh.y);
    float deal=0.86+0.14*vis;float2 hb=float2(CS_CARD_W*0.5*sc,share*wh.y*0.5)*deal;float2 q=p-c;
    float d=CsRounded(q,hb,min(0.012*sc,0.45*min(hb.x,hb.y)));
    float3 tint=CsLinear(CsHex(tintHex));float isLit=1.0-step(0.5,abs(lit-k));float inside=CsFill(d,px);
    float a=CS_SLOT_A*inside;float3 e=tint*(CS_SLOT*inside);
    if(mode<0.5)e+=tint*(CS_CARD_RIM*CsLine(d+0.004*sc,CS_LINE_W*sc,px)+CS_GLYPH*CsGlyph(q,sc,px)*inside);
    else if(mode<1.5)e+=tint*(lerp(CS_FACED_RIM,CS_FACED_LIT,isLit)*CsLine(d-0.003*sc,CS_LINE_W*sc,px)+CS_FACED_GLOW*exp(-max(d,0.0)/(0.012*sc))*(1.0-inside));
    else e+=tint*(lerp(CS_CARD_RIM,CS_CARD_LIT,isLit)*CsLine(d+0.004*sc,CS_LINE_W*sc,px)+CS_GLYPH*CsGlyph(q,sc,px)*inside);
    return float4(e,a)*vis;
}
// The screen (kind 0).
float4 CsScreen(float4 A,float4 B,float4 C,float4 D,float4 E,float4 holo,float ndv,float2 fw)
{
    float2 wh=max(B.xy,float2(0.0001,0.0001));float2 p=(A.xy-0.5)*wh;float px=max(max(fw.x*wh.x,fw.y*wh.y),0.00001);
    float2 hb=wh*0.5;float sc=wh.x/CS_W;float d=CsChamfer(p,hb,min(0.045*sc,0.4*min(hb.x,hb.y)));float inside=CsFill(d,px);
    float3 tone=CsLinear(CsHex(E.w));float3 edge=lerp(tone,CsLinear(CsHex(CS_DANGER_HEX)),saturate(B.z));
    // The glass, its grid (fading out before it would alias, and near the edge) and its scanlines (a soft wave drifting down).
    float a=CS_BODY_A*inside;float3 e=tone*(CS_BODY*inside);
    float2 g=abs(frac(A.xy*float2(32.0,16.0)+0.5)-0.5)/float2(32.0,16.0)*wh;
    float grid=max(CsLine(g.x,0.0008*sc,px),CsLine(g.y,0.0008*sc,px))*saturate((min(wh.x/32.0,wh.y/16.0)/px-6.0)/6.0)*saturate(-d/(0.03*sc))*(1.0-holo.w);
    float sp=wh.y/104.0;float scan=(0.5+0.5*cos(6.2832*(A.y*104.0+holo.x*0.35*holo.y)))*saturate((sp/px-3.0)/3.0);
    e+=tone*((CS_GRID*grid+CS_SCAN*scan)*inside);
    // The soft inner rim and the fresnel sheen at a glancing view.
    float rim=exp(min(d,0.0)/(CS_RIM_W*sc))*inside;float fres=pow(1.0-saturate(ndv),3.0)*inside;
    e+=edge*(CS_RIM*rim)+tone*(CS_FRESNEL*fres);
    // The frame line just inside the edge, the corner brackets inside it, the header and footer rules (the HUD's bands).
    float2 cq=abs(p)-(hb-float2(0.075,0.075)*sc);float corner=saturate(cq.x/px+0.5)*saturate(cq.y/px+0.5);
    e+=edge*(CS_LINE*CsLine(d+0.005*sc,CS_LINE_W*sc,px)+CS_BRACKET*CsLine(d+0.012*sc,CS_LINE_W*1.4*sc,px)*corner);
    float yh=(0.5-0.110/CS_H)*wh.y;float yf=(0.100/CS_H-0.5)*wh.y;float ends=saturate((hb.x-0.034*sc-abs(p.x))/(0.02*sc));
    e+=tone*(CS_RULE*(CsLine(p.y-yh,0.0012*sc,px)+0.6*CsLine(p.y-yf,0.0012*sc,px))*ends*inside);
    // The cards, revealed by a scan line sweeping down as the screen opens.
    float cm=floor(C.x+0.5);float mode=floor(cm/4.0);float n=cm-mode*4.0;float yScan=lerp(hb.y,-hb.y,saturate(B.w));float shown=saturate((p.y-yScan)/px+0.5);
    float4 cards=(CsCard(p,wh,px,0.0,n,D.x,E.x,C.z,mode,C.y)+CsCard(p,wh,px,1.0,n,D.y,E.y,C.z,mode,C.y)+CsCard(p,wh,px,2.0,n,D.z,E.z,C.z,mode,C.y))*(shown*inside);
    e+=cards.rgb+edge*(CS_SCAN_IN*CsLine(p.y-yScan,0.0016*sc,px)*(1.0-saturate(B.w))*inside);a=1.0-(1.0-a)*(1.0-cards.a);
    return float4(e,a)*A.w;
}
// Everything (premultiplied light and alpha): the screen, a ray, the emitter ring, a mote, the flying card.
float4 ScreenColour(float4 A,float4 B,float4 C,float4 D,float4 E,float4 holo,float ndv,float2 fw)
{
    float kind=A.z;
    if(kind<0.5)return CsScreen(A,B,C,D,E,holo,ndv,fw);
    float3 tint=CsLinear(CsHex(C.x));
    if(kind<1.5)
    {
        // A ray: brightest at the wrist, a soft profile across, its energy kept when it is thinner than two pixels.
        float across=1.0-abs(A.x*2.0-1.0);float prof=across*across*(1.0-0.82*A.y)*min(1.0,0.5/max(fw.x,0.00001));
        return float4(tint*(CS_RAY*prof),CS_RAY_A*prof)*A.w;
    }
    float2 c=A.xy*2.0-1.0;float r=length(c);float pu=max(max(fw.x,fw.y)*2.0,0.0001);
    if(kind<2.5)return float4(tint*(CS_RING*CsLine(r-0.78,0.12,pu)+0.3*CS_RING*(1.0-smoothstep(0.0,0.35,r))),0.0)*A.w;
    if(kind<3.5)return float4(tint*(CS_MOTE*exp(-dot(c,c)*6.0)),0.0)*A.w;
    // The flying card: a lit card's rim, a slot's glass and the emblem.
    float2 wh=max(B.xy,float2(0.0001,0.0001));float2 q=(A.xy-0.5)*wh;float px=max(max(fw.x*wh.x,fw.y*wh.y),0.00001);float sc=wh.x/CS_CARD_W;
    float d=CsRounded(q,wh*0.5-float2(0.004,0.004)*sc,0.012*sc);float inside=CsFill(d,px);
    return float4(tint*(CS_CARD_LIT*CsLine(d+0.003*sc,CS_LINE_W*sc,px)+CS_SLOT*inside+CS_GLYPH*CsGlyph(q,sc,px)*inside),CS_SLOT_A*inside)*A.w;
}
// SHARED END
            struct appdata {float4 vertex:POSITION;float4 a:TEXCOORD0;float4 b:TEXCOORD1;float4 c:TEXCOORD2;float4 d:TEXCOORD3;float4 e:TEXCOORD4;};
            struct v2f {float4 position:SV_POSITION;float4 a:TEXCOORD0;float4 b:TEXCOORD1;float4 c:TEXCOORD2;float4 d:TEXCOORD3;float4 e:TEXCOORD4;float3 world:TEXCOORD5;UNITY_FOG_COORDS(6)};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.a=v.a;o.b=v.b;o.c=v.c;o.d=v.d;o.e=v.e;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                // The screen's facing from the flat quad's own derivatives, against the view (the fresnel sheen).
                float3 cr=cross(ddx(i.world),ddy(i.world));float l=length(cr);float ndv=l>1e-12?abs(dot(cr/l,normalize(_WorldSpaceCameraPos-i.world))):1;
                float4 c=ScreenColour(i.a,i.b,i.c,i.d,i.e,_ChipScreen,ndv,fwidth(i.a.xy));
                // The fog fades it out (its light and its glass).
                float4 keep=float4(1,1,1,1);UNITY_APPLY_FOG_COLOR(i.fogCoord,keep,float4(0,0,0,0));
                return c*keep.r;
            }
            ENDCG
        }
    }
    Fallback Off
}
