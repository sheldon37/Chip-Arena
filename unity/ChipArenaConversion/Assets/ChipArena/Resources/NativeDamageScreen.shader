// The local pilot's damage screen (rules: Core/NativeDamageScreen.cs; drawn by Runtime/NativeDamageScreenPresentation.cs
// through Runtime/NativeDamageScreenImage.cs): one full-screen pass over the finished 3D frame as the last screen camera's
// image effect (OnRenderImage -> Graphics.Blit), before the IMGUI HUD. All maths in linear light (Linear colour space: the
// source is an sRGB or HDR float target, which reads linear either way, and the destination encodes the writes). SceneTap
// clamps the frame to 0..1, as the old 8-bit copy did: an HDR source holds additive glow above 1, and the blood maths scale
// with luma, so unclamped glow would show through a bloom or the low-HP edge as pale pink instead of deep red glass.
//  * Up to four wound blooms: smoky crimson that drains the frame to red glass from the edge facing the attacker (lobe
//    angles in the screen square so every aspect reads alike), ragged by noise and dissolving back into the frame; a
//    headshot's bloom is radially striated, with a pale flash confined to the bloom.
//  * Impact chromatic split (radial, zero at the centre) and exposure dip; colour drain from trauma and low HP.
//  * Low HP: a churning crimson edge pooled in the corners that the heartbeat gently swells (reach, density and a warm rim
//    each rise by a fraction, never a blink). Burn: soot and flame tongues with rising embers from the bottom and lower
//    sides, heat shimmer. Poison: a sickly film and slow drips from the top and upper sides, a sway. Heal: a cool lift of
//    the edge band (a multiply, so black stays black) and, for a real heal, one thin soft wavefront of light out past
//    the corners.
//  * Nothing above reaches the crosshair: it is multiplied by a centre guard (lower and taller in third person).
//  * Then the threat ring round the crosshair, drawn over the guard but never inside R - 6 u (u = H / 1080 px): six
//    per-attacker arcs (three segments, dark keyline, outward chevron, white core; twin rails and a hollow diamond for a
//    headshot; elevation carets), a dashed ring for hits with no bearing, the melee shock arc, the headshot reticle mark
//    and a faint instrument track while any arc is up.
// The block between BEGIN SHARED and END SHARED is the whole effect. The offline render harness compiles exactly that block
// as GLSL ES 3.00 (float2 -> vec2, saturate, lerp, frac, atan2 and the BRANCH / UNROLL attributes are macros there), so
// keep it to the common subset: float literals, no scalar swizzles, no one-argument vector constructors, smoothstep only
// through ss(). D3D11 / SM 3.5: no derivatives (every texture read is tex2Dlod), no signed pow, fixed-length loops.
Shader "Hidden/ChipArena/NativeDamageScreen" {
 Properties { _MainTex("Texture",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #pragma target 3.5
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize;
   sampler2D _DmgNoise;  // NativeDamageScreen.NoiseTexels: R fbm/8, G fbm/16, B value/32, A value/64 (linear, repeat, bilinear)
   float4 _DmgA[4];      // per bloom: dir.x, dir.y, dir weight, amount
   float4 _DmgB[4];      // per bloom: reach (screen heights), cos half-width, dissolve, headshot
   float4 _DmgG;         // flash, chroma, desaturation, exposure dip
   float4 _DmgL;         // low HP, near death, heartbeat pulse, clock (s)
   float4 _DmgE;         // fire, poison, heal, heal wave (0..1, 0 = none)
   float4 _DmgK;         // fire tick, poison tick, headshot flash, reduced effects (0/1)
   float4 _DmgView;      // aspect, safe-centre y (screen heights, + up), safe scale, intensity (Subtle .6)
   float4 _DmgArcA[6];   // per arc: bearing (rad, 0 up, + clockwise), strength, half width (rad), core (u)
   float4 _DmgArcB[6];   // per arc: snap offset (u), white core 0..1, kind (0 hit, 1 headshot, 4 omni; +8 melee), carets -2..2
   float4 _DmgArcC[6];   // per arc: glow (u), severity, unused, unused
   float4 _DmgRing;      // ring radius (screen heights), screen height (px), track 0..1, third person (0/1)
   float4 _DmgFx;        // melee shock age (s, < 0 none), shock bearing (rad), headshot mark age (s, < 0 none), ring fade
   #define BRANCH [branch]
   #define UNROLL [unroll]
   float3 SceneTap(float2 uv){return saturate(tex2Dlod(_MainTex,float4(uv,0.0,0.0)).rgb);}
   float4 NoiseTap(float2 uv){return tex2Dlod(_DmgNoise,float4(uv,0.0,0.0));}
// BEGIN SHARED
static const float PI=3.14159265;
static const float3 HitCore=float3(1.0,0.0685,0.102);      // #ff4a5a
static const float3 HitGlow=float3(1.0,0.013,0.045);       // #ff1e3c
static const float3 HeadRail=float3(1.0,0.745,0.776);      // #ffe0e4
static const float3 OmniCore=float3(1.0,0.195,0.235);      // #ff7a85
static const float3 Ink=float3(0.003,0.005,0.008);         // #0a0f16 keyline
static const float3 TrackInk=float3(0.807,0.851,0.896);    // #e8edf2 ring track
static const float3 White=float3(1.0,1.0,1.0);

float hash12(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
// The baked noise, sampled at a lattice frequency: R and G hold 8 and 16 cells per texture, B 32, A 64.
float4 nz(float2 cells,float period){return NoiseTap(cells/period);}
float sdRoundBox(float2 p,float2 b,float r){float2 q=abs(p)-b+r;return length(max(q,0.0))+min(max(q.x,q.y),0.0)-r;}
float luma(float3 c){return dot(c,float3(0.2126,0.7152,0.0722));}
float3 Grey(float3 c){float l=luma(c);return float3(l,l,l);}
float sq(float x){return x*x;}   // never pow(x,2.0) on a signed base: D3D pow is NaN below zero
// smoothstep for either edge order (e0 > e1 falls as x rises); GLSL leaves smoothstep undefined for e0 >= e1.
float ss(float e0,float e1,float x){float k=saturate((x-e0)/(e1-e0));return k*k*(3.0-2.0*k);}
float WrapPi(float a){return a-2.0*PI*floor((a+PI)/(2.0*PI));}
float Seg(float2 p,float2 a,float2 b){float2 pa=p-a;float2 ba=b-a;float h=saturate(dot(pa,ba)/max(dot(ba,ba),1e-5));return length(pa-ba*h);}
// Coverage of a stroke `w` px wide at distance `d` px from its centre line (1 px anti-aliasing, no derivatives).
float Stroke(float d,float w){return saturate(w*0.5+0.5-d);}
float EaseOut(float x){x=saturate(x);return 1.0-(1.0-x)*(1.0-x)*(1.0-x);}
float3 ToGamma(float3 c){c=max(c,0.0);return max(1.055*pow(c,float3(0.4166667,0.4166667,0.4166667))-0.055,0.0);}
float3 ToLinear(float3 c){c=max(c,0.0);return pow((c+0.055)/1.055,float3(2.4,2.4,2.4));}
// The frame at a screen uv (y up); `flip` < 0 when the source is stored top-down (either way the tap lands on the blit's uv).
float3 Scene(float2 uv,float flip){return SceneTap(float2(uv.x,flip>0.0?uv.y:1.0-uv.y));}

// One threat arc at pixel offset d from the crosshair (y up), radius r, angle ang (0 up, + clockwise); premultiplied light
// and cover. Three segments (a centre plate and two tapering wings with 3 u gaps), a 3.5 u dark keyline, an outward glow,
// an outward chevron (a hollow diamond and twin rails for a headshot) and solid carets beyond it for an attacker above or
// below. No bearing: a 24-dash ring instead, so it never reads as a direction.
float4 Arc(float2 d,float r,float ang,int k,float u,float R)
{
 float4 A=_DmgArcA[k];float4 B=_DmgArcB[k];float4 C=_DmgArcC[k];
 float4 o=float4(0.0,0.0,0.0,0.0);
 BRANCH if(A.y<=0.001)return o;
 float kind=B.z;if(kind>=7.5)kind-=8.0;
 bool head=kind>0.5&&kind<1.5;bool omni=kind>3.5;
 BRANCH if(omni)
 {
  float dro=r-(R+B.x*u);float g=frac(ang*12.0/PI+0.25);float gd=g>=0.5?min(g-0.5,1.0-g):-min(0.5-g,g);float dash=saturate(gd*(PI/12.0)*r+0.5);
  float cv=Stroke(abs(dro),2.2*u)*dash;
  o.rgb=lerp(OmniCore,White,B.y)*cv*A.y;o.a=saturate((cv*0.9+Stroke(abs(dro),5.0*u)*dash*(1.0-cv)*0.4)*A.y);return o;
 }
 float da=abs(WrapPi(ang-A.x));float along=da/max(A.z,1e-3);
 float rr=R+B.x*u;float dr=r-rr;
 BRANCH if(along>1.6||dr<-26.0*u||dr>(C.x+48.0)*u)return o;
 float taper=1.0-ss(0.45,1.0,along);float th=A.w*u*(0.3+0.7*taper);
 float tips=1.0-ss(0.93,1.03,along);
 // Segment gaps at 38% of the half width, 3 u of arc each.
 float sArc=da*rr;float s1=0.38*A.z*rr;float gap=saturate(abs(sArc-s1)-1.5*u);
 float core;
 if(head){float off=th*0.5+1.6*u;core=max(Stroke(abs(dr-off),1.8*u),Stroke(abs(dr+off),1.8*u));core=max(core,0.3*Stroke(abs(dr),th));}
 else core=Stroke(abs(dr),th);
 core*=tips*gap;
 float key=Stroke(abs(dr),th+(head?7.0:3.5)*u)*tips*(1.0-core);
 float glowW=C.x*u;float glow=(dr>0.0?exp(-dr/glowW):exp(dr/(2.5*u)))*(1.0-ss(0.6,1.25,along));
 float2 dir=float2(sin(A.x),cos(A.x));float2 tg=float2(dir.y,-dir.x);
 float2 q=float2(dot(d,tg),dot(d,dir));
 float n0=rr+th*0.5+5.0*u;float h=(9.0+6.0*C.y)*u;float w=(6.5+4.0*C.y)*u;
 float pd;
 if(head)pd=min(min(Seg(q,float2(0.0,n0),float2(w,n0+h*0.5)),Seg(q,float2(w,n0+h*0.5),float2(0.0,n0+h))),min(Seg(q,float2(0.0,n0+h),float2(-w,n0+h*0.5)),Seg(q,float2(-w,n0+h*0.5),float2(0.0,n0))));
 else pd=min(Seg(q,float2(-w,n0),float2(0.0,n0+h)),Seg(q,float2(w,n0),float2(0.0,n0+h)));
 float pointer=Stroke(pd,2.4*u);float pkey=Stroke(pd,5.0*u)*(1.0-pointer);
 float carets=0.0;float ckey=0.0;float ce=B.w;
 if(abs(ce)>0.5)
 {
  // Solid screen-oriented triangles (unlike the hollow chevron): up for an attacker above, down for one below.
  float2 apex=dir*(n0+h+12.0*u);float sg=ce>0.0?1.0:-1.0;float tw=5.5*u;float tH=6.5*u;float nrm=sqrt(tH*tH+tw*tw);
  UNROLL for(int j=0;j<2;j++)
  {
   if(float(j)<abs(ce)-0.5){float2 cp=d-(apex+float2(0.0,sg*float(j)*8.0*u));cp.y*=sg;
    float sd=max((tH*abs(cp.x)+tw*cp.y-tw*tH*0.5)/nrm,-cp.y-tH*0.5);carets=max(carets,saturate(0.5-sd));ckey=max(ckey,saturate(0.5-(sd-2.0*u)));}
  }
 }
 float3 coreCol=lerp(head?HeadRail:HitCore,White,B.y);
 float shape=max(core,pointer);
 o.rgb=(coreCol*shape+HeadRail*carets*(1.0-shape)+HitGlow*glow*0.8*(0.55+0.45*C.y)*(1.0+B.y))*A.y;
 shape=max(shape,carets);
 o.a=saturate((shape*0.9+(key+pkey*0.8+ckey*(1.0-carets)*0.8)*0.45)*A.y);
 return o;
}

// The threat ring layer at p (screen heights from the centre, y up): arcs (max-combined), the third-person bottom sector at
// .7, the instrument track, the melee shock arc and the headshot reticle mark. Premultiplied light and cover.
float4 Ring(float2 p)
{
 float H=_DmgRing.y;float u=H/1080.0;float R=_DmgRing.x*H;
 float2 d=p*H;float r=length(d);
 float4 acc=float4(0.0,0.0,0.0,0.0);
 BRANCH if(r<R-7.0*u||r>R+90.0*u)return acc;
 float ang=atan2(d.x,d.y);
 UNROLL for(int k=0;k<6;k++){float4 a=Arc(d,r,ang,k,u,R);acc.rgb=max(acc.rgb,a.rgb);acc.a=max(acc.a,a.a);}
 // Third person: the bottom 90 degrees of the ring sit over the pilot's head, so they draw at .7.
 acc*=lerp(1.0,0.7,_DmgRing.w*ss(2.27,2.44,abs(ang)));
 float track=_DmgRing.z;
 if(track>0.001)
 {
  float a12=abs(WrapPi(ang*12.0))/12.0*R;
  float tick=Stroke(a12,1.6*u)*(1.0-ss(4.0*u,5.0*u,abs(r-R-6.0*u)));
  float trk=Stroke(abs(r-R),1.0*u)*0.10+tick*0.22;
  acc.rgb+=TrackInk*trk*track*(1.0-acc.a);
 }
 bool still=_DmgK.w>0.5;
 float shockAge=_DmgFx.x;
 if(shockAge>=0.0&&shockAge<0.22)
 {
  // Melee: a +-70 degree arc toward the attacker expanding from R + 10 u to R + 56 u, white fading to red (still: R + 30 u).
  float sk0=shockAge/0.22;float rr=still?R+30.0*u:R+10.0*u+46.0*u*EaseOut(sk0);
  float wa=1.0-ss(0.35,1.25,abs(WrapPi(ang-_DmgFx.y)));
  float sk=Stroke(abs(r-rr),(3.0-1.6*sk0)*u)*wa*(1.0-sk0);
  acc.rgb+=lerp(White,HitCore,saturate(sk0*2.5))*sk*0.85;acc.a=max(acc.a,sk*0.3);
 }
 float markAge=_DmgFx.z;
 if(markAge>=0.0&&markAge<0.26)
 {
  // Headshot: a white ring at R and four diagonal ticks sliding out 10 u (still: half way).
  float m=1.0-markAge/0.26;float e=still?0.5:EaseOut(markAge/0.26);
  float ringL=Stroke(abs(r-R),1.4*u)*0.55*m;
  float ticks=0.0;
  UNROLL for(int j=0;j<4;j++){float aa=PI*0.25+PI*0.5*float(j);float2 dv=float2(sin(aa),cos(aa));ticks=max(ticks,Stroke(Seg(d,dv*(R+6.0*u+10.0*u*e),dv*(R+15.0*u+10.0*u*e)),2.0*u));}
  acc.rgb+=White*max(ringL,ticks*m);acc.a=max(acc.a,ticks*m*0.6);
 }
 // Never inside R - 6 u.
 acc*=saturate(r-(R-6.0*u)+0.5)*_DmgFx.w;
 return acc;
}

// The whole effect at screen uv (y up) and pixel fragPx; returns linear colour.
float3 DamageScreen(float2 uv0,float flip,float2 fragPx)
{
 float aspect=_DmgView.x;float I=_DmgView.w;float t=_DmgL.w;
 float2 hb=float2(0.5*aspect,0.5);
 float2 p=(uv0-0.5)*float2(aspect,1.0);         // screen-height units, y up, centre 0
 float2 q=p/hb;                                 // the screen as a [-1,1] square (lobe angles)
 float edge=max(0.0,-sdRoundBox(p,hb,0.2));     // distance in from the rounded frame edge
 // Centre guard: nothing reaches the crosshair; the guard ellipse sits lower and taller in third person.
 float2 sc=(p-float2(0.0,_DmgView.y))/float2(1.0,_DmgView.z);
 float safe=ss(0.15,0.40,length(sc));
 float n1=nz(p*3.4+float2(0.0,t*0.07),8.0).r;            // broad ragged shape
 float n2=nz(p*11.0+float2(t*0.21,-t*0.13),32.0).b;      // fine grain / wisps
 float n3=nz(p*7.0+float2(-t*0.11,t*0.16),16.0).g;       // smoke inside a bloom

 // ---- UV distortion: heat shimmer at the bottom (fire), a slow sway at the top (poison) -----------------------------
 float2 uv=uv0;
 float db=p.y+0.5;float dt=0.5-p.y;
 if(_DmgE.x>0.001){uv.x+=sin(p.y*58.0+t*9.0)*0.0011*_DmgE.x*ss(0.32,0.0,db)*safe;}
 if(_DmgE.y>0.001){uv.y+=sin(p.x*6.0+t*2.1)*0.0022*_DmgE.y*ss(0.30,0.0,dt);}

 // ---- Scene, with the impact chromatic split (radial, zero at the centre) -----------------------------------------
 float3 col;
 float ch=_DmgG.y*I;
 BRANCH if(ch>0.001){float2 o=normalize(p+1e-5)*ch*0.011*dot(q,q)*0.5/float2(aspect,1.0);
  col=float3(Scene(uv+o,flip).r,Scene(uv,flip).g,Scene(uv-o,flip).b);}
 else col=Scene(uv,flip);

 // ---- Colour drain: trauma, low HP, the death beat; the middle keeps most of its colour ---------------------------
 float drain=_DmgG.z*lerp(0.35,1.0,ss(0.12,0.62,length(sc)))*I;
 col=lerp(col,Grey(col),drain);

 // ---- Wound blooms + the low-HP edge: one union of opacity, one rim --------------------------------------------
 float2 qd=q/max(length(q),1e-4);
 float A=0.0;float rim=0.0;float rimLow=0.0;float headM=0.0;
 UNROLL for(int k=0;k<4;k++)
 {
  float4 a=_DmgA[k];float4 b=_DmgB[k];if(a.w<=0.001)continue;
  float c=dot(qd,a.xy);
  float lobe=lerp(1.0,ss(b.y,lerp(b.y,1.0,0.62),c),a.z);
  float depth=b.x*pow(saturate(lobe),0.8)*(0.75+0.5*n1);                // reach, ragged by the broad noise
  float fuzz=0.22;float streak=1.0;bool head=b.w>0.5;
  if(head){                                                   // headshot: radial striations (an impact burst), not smoke;
   float s1=nz(qd*28.0+float(k)*3.7,32.0).b;float s2=nz(qd*63.0+float(k)*5.1,64.0).a;   // half the design study's streak gain
   streak=0.45+0.55*(pow(saturate(s1),2.5)*0.7+pow(saturate(s2),3.0)*0.5);depth*=0.8+0.35*streak;fuzz=0.06;}
  float v=edge/max(depth,1e-4)+(n2-0.5)*fuzz;                 // 0 at the frame, 1 at the reach; fuzzy front
  float dd=sq(saturate(1.0-v))*(head?streak:0.55+0.9*n3);     // soft density: smoky inside, striated for a headshot
  float th=b.z;dd=saturate((dd-th*0.8)/(1.0-th*0.6))*(1.0-0.3*th); // dissolve: retreats into the frame in wisps
  float m=min(1.0,dd)*lobe;
  A=1.0-(1.0-A)*(1.0-m*a.w);
  rim+=a.w*m*(1.0-m)*4.0*(1.0-th);                            // a warm inner glow where the bloom thins
  if(head)headM=max(headM,m*a.w);
 }
 // Low HP: a slow churning omni edge pooled in the corners (never an even frame); near death it reaches further, to about
 // a fifth of the height at the corners. The heartbeat is a swell, not a flash: +10% reach, +12% density and a warm rim
 // from .16 to .24 (the bloom rim's impact boost never applies here). corner: 1 toward the screen diagonals.
 float corner=saturate(abs(qd.x*qd.y)*2.0);
 if(_DmgL.x>0.001)
 {
  float reach=(0.05+0.06*_DmgL.x+0.03*_DmgL.y)*(1.0+0.10*_DmgL.z)*(0.6+0.6*corner);
  float nn=nz(p*2.2+float2(t*0.05,t*0.035)+float2(2.96,4.88),8.0).r;
  float v=edge/(reach*(0.6+0.7*nn))+(n2-0.5)*0.22;
  float m=pow(saturate(1.0-v),1.6)*(0.55+0.9*n3);
  A=1.0-(1.0-A)*(1.0-min(1.0,m)*ss(0.0,0.18,_DmgL.x)*(0.45+0.3*_DmgL.x)*(0.90+0.12*_DmgL.z));
  rimLow=(0.16+0.08*_DmgL.z)*ss(0.0,0.18,_DmgL.x)*m*(1.0-min(1.0,m))*4.0;
 }
 A*=safe*I;rim=(rim*(0.35+0.65*min(1.0,_DmgG.x))+rimLow)*safe*I;
 // Blood: the scene seen through deep red glass, darker at the very edge, a faint ember glow so black still reads red.
 float core=exp(-edge/0.06);
 float3 blood=luma(col)*float3(0.62,0.06,0.07)+float3(0.065,0.003,0.005)*(0.45+1.1*n3);
 col=lerp(col,Grey(col),min(1.0,A*1.4));          // drained first, so a thin bloom reads grey-red, never mauve
 col=lerp(col,blood,A);
 col*=1.0-0.5*A*core;
 col+=float3(0.75,0.05,0.06)*rim*0.30;
 // Headshot: a pale flash inside the bloom only, hugging the struck edge, never over the frame interior (it is linear
 // light: even a tenth of white reads as a pale veil over a dark floor).
 col=lerp(col,float3(1.0,0.82,0.80),min(0.45,_DmgK.z*headM*core*safe*I));

 // ---- Burn: flame tongues licking up from the bottom edge and the lower sides, pulsing on each tick ----------------
 float side=0.5*aspect-abs(p.x);
 if(_DmgE.x>0.001)
 {
  float fl=nz(float2(p.x*6.5,p.y*2.2-t*1.8)+float2(1.04,5.68),8.0).r*0.75+nz(float2(p.x*15.0,p.y*4.0-t*3.1)+float2(16.6,9.3),32.0).b*0.25;
  float h=(0.04+0.07*_DmgE.x+0.05*_DmgK.x)*(0.25+1.5*fl*fl);
  float dist=min(db,side+db*0.9);
  float body=ss(h,h*0.35,dist);
  float tip=exp(-sq((dist-h*0.7)/(h*0.28+0.004)))*ss(0.15,0.6,fl);
  // Embers: sparse sparks rising out of the flames.
  float2 ec=float2(p.x*26.0,p.y*26.0-t*4.2);float2 ei=floor(ec);float2 ef=frac(ec)-0.5;float eh=hash12(ei);
  float ember=step(0.93,eh)*ss(0.16,0.04,length(ef+float2(sin(t*3.0+eh*40.0)*0.2,0.0)))*ss(h*2.6,h*0.5,dist);
  float kf=_DmgE.x*safe*I;
  col=lerp(col,col*float3(0.75,0.38,0.20),body*kf*0.45);                 // scorched base
  col+=(float3(1.0,0.22,0.03)*body*0.10+float3(1.0,0.55,0.12)*tip*0.28)*kf*(1.0+1.2*_DmgK.x)+float3(1.0,0.70,0.30)*ember*kf*0.7;
 }
 // ---- Poison: a sickly film and slow drips hanging from the top edge and the upper sides, a throb on each tick ----
 if(_DmgE.y>0.001)
 {
  float reach=(0.04+0.06*_DmgE.y+0.03*_DmgK.y)*(1.0+0.08*sin(t*1.6));
  float dist=min(dt,side+dt*0.9);
  float band=pow(saturate(1.0-dist/(reach*(0.7+0.6*n1))),1.3);
  // Drips: one per narrow column (about half the columns), a tapering stem and a bead that slowly slides down.
  float cw=1.0/14.0;float cx=p.x/cw;float ci=floor(cx);float lx=(frac(cx)-0.5)*cw;
  float h1=hash12(float2(ci,4.1));float h2=hash12(float2(ci,9.3));
  float len=(0.025+0.11*h1*h1)*(0.5+0.5*_DmgE.y+0.25*_DmgK.y)*(0.75+0.25*frac(t*0.12+h2));
  float rd=(0.0045+0.005*h2)*(0.6+0.4*_DmgE.y);
  float ly=dist;
  float stem=length(float2(lx,ly-clamp(ly,0.0,len)))-rd*(1.0-0.45*saturate(ly/max(len,1e-3)));
  float bead=length(float2(lx,(ly-len)*0.85))-rd*1.55;
  float drip=ss(0.0016,-0.0016,min(stem,bead))*step(0.45,h2+0.25*h1);
  float shine=ss(0.0012,-0.0012,length(float2(lx+rd*0.45,ly-len+rd*0.5))-rd*0.45)*drip;
  float kp=_DmgE.y*safe*I;
  // The film is a multiply, so it all but vanishes on a dark floor: dark pixels take it 1.3x stronger.
  float dark=lerp(1.3,1.0,ss(0.04,0.08,luma(col)));
  col=lerp(col,col*float3(0.45,0.72,0.18)+float3(0.006,0.022,0.0)*dark,saturate(band*kp*0.85*dark));
  col=lerp(col,col*float3(0.18,0.32,0.04)+float3(0.035,0.085,0.006),drip*kp*0.85);
  col+=float3(0.55,0.95,0.25)*(shine*0.35+band*0.03*_DmgK.y)*kp;
 }
 // ---- Heal: a cool, clean lift of the edge band; a real heal sends one thin wavefront of light out past the corners ---
 if(_DmgE.z>0.001||_DmgE.w>0.0)
 {
  float3 mint=float3(0.55,1.0,0.90);
  float reach=(0.06+0.08*_DmgE.z)*(0.6+0.7*corner);
  float m=pow(saturate(1.0-edge/(reach*(0.8+0.4*n1))),1.6)*_DmgE.z;
  float wave=0.0;
  if(_DmgE.w>0.0)
  {
   // An ellipse in the screen square (1 at the edge midpoints, 1.41 at the corners) easing out from .4 to 1.55: a thin
   // front, crisp ahead and trailing softly behind, barely wavy, faded in away from the guard and out at the frame.
   float k=1.0-sq(1.0-_DmgE.w);float rq=length(q);
   float wd=rq-lerp(0.40,1.55,k)+(n1-0.5)*0.05;float ww=0.022+0.035*k;wd/=wd>0.0?ww*0.7:ww*1.6;
   wave=exp(-wd*wd)*pow(saturate(1.0-_DmgE.w),1.2)*ss(0.30,0.70,rq);
  }
  float lift=m*0.45*safe*I;float wl=wave*safe*I;
  // A multiply (black stays black); only the thin front adds a trace of light, mostly in proportion to the scene.
  col*=1.0+mint*(lift+wl*1.4);
  col+=mint*wl*(0.008+0.06*min(luma(col),0.5));
 }
 col*=1.0-_DmgG.w*I;

 // ---- The threat ring: over the guard, never inside R - 6 u; full strength in Subtle (it carries information) -------
 BRANCH if(_DmgRing.z>0.001||_DmgFx.x>=0.0||_DmgFx.z>=0.0)
 {
  float4 ring=Ring(p);
  col=ring.rgb+col*(1.0-ring.a);
 }
 // Dither (+-.5 of an 8-bit step in display space) so the dark red gradients do not band.
 float3 g=ToGamma(col)+(hash12(fragPx)-0.5)/255.0;
 return ToLinear(g);
}
// END SHARED
   float4 frag(v2f_img i):SV_Target
   {
    // Graphics.Blit samples _MainTex the right way up at i.uv on every API. When its source is stored top-down (D3D: an
    // anti-aliased or back-buffer resolve) it reports a negative texel height and i.uv runs top-down on screen; the overlay
    // is laid out in screen uv with y up (GL convention, like a depth texture), so it flips then. Scene() undoes the flip:
    // the frame itself is always read at i.uv.
    float flip=1.0;
    #if UNITY_UV_STARTS_AT_TOP
    if(_MainTex_TexelSize.y<0.0)flip=-1.0;
    #endif
    float2 uv=float2(i.uv.x,flip>0.0?i.uv.y:1.0-i.uv.y);
    return float4(DamageScreen(uv,flip,i.pos.xy),1.0);
   }
   ENDCG
  }
 }
 Fallback Off
}
