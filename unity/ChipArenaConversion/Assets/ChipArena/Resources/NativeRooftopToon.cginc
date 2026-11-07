#ifndef NATIVE_ROOFTOP_TOON_INCLUDED
#define NATIVE_ROOFTOP_TOON_INCLUDED
// The home screen's rooftop in the anime style (docs/MAP-STYLE-ANIME.md section 6; Core NativeMenuStyle holds the CPU copy of
// every constant and formula here). Drawn only while _ArenaToon.x is on: NativeLighting.Rooftop writes the rooftop's own toon
// values (NativeMenuStyle.Rooftop) unless the `--ca-map-style legacy` command line turns the style off; then every rooftop
// shader draws its legacy look exactly. A dark blue anime city at night: a painted gradient sky with a city-glow band and
// cumulus lit from below by the city, flat two-tone towers in the cool shade palette (around #2A3460), crisp windows, cel
// bands on the roof's props, the neon as saturated accents, and an aerial haze in a few soft depth planes.
// Globals: _RoofToonFog (x the haze's depth planes 0 smooth .. 1 stepped, y the aerial density per metre, z the street
// glow's height (m), w its strength), set with the rest of the rooftop's values by NativeRooftopScene.
// ---- ROOFTOP TOON SHARED BEGIN (HLSL and GLSL: float literals only) ----
float4 _RoofToonFog;
// The sky (linear): zenith, upper sky, the horizon's band and the street haze below it; the city glow swings between the
// magenta and the cyan side round the horizon.
#define ROOF_TOON_ZENITH float3(0.0021,0.0037,0.0180)
#define ROOF_TOON_UPPER float3(0.0060,0.0110,0.0560)
#define ROOF_TOON_HORIZON float3(0.0300,0.0360,0.1550)
#define ROOF_TOON_GLOW_MAGENTA float3(0.1250,0.0350,0.2100)
#define ROOF_TOON_GLOW_CYAN float3(0.0220,0.0950,0.2400)
#define ROOF_TOON_STREET float3(0.0420,0.0240,0.1300)
#define ROOF_TOON_HAZE_GLOW 0.3
// The towers' two tones and roofs (the cool shade palette), the window and band gains.
#define ROOF_TOON_FACADE_SHADE float3(0.0090,0.0140,0.0600)
#define ROOF_TOON_FACADE_LIT float3(0.0200,0.0300,0.1250)
#define ROOF_TOON_ROOF float3(0.0250,0.0360,0.1500)
#define ROOF_TOON_KEY float3(0.3700,0.5500,0.7500)
#define ROOF_TOON_WINDOW 0.46
#define ROOF_TOON_RUN 6.0
// The props: their albedo lifted out of near-black toward the cool shade, the ambient's two bands (sky-facing, the rest).
#define ROOF_TOON_LIFT 2.6
#define ROOF_TOON_FLATTEN 4.0
#define ROOF_TOON_AMB_UP float3(0.44,0.54,1.15)
#define ROOF_TOON_AMB_SIDE float3(0.28,0.34,0.76)
// The anime night sky over the city (NativeRooftopSky): the gradient, a crisp band of city glow at the horizon.
float3 RoofToonSkyBase(float3 d)
{
    float h=d.y;float az=atan2(-d.x,-d.z);
    float3 glow=lerp(ROOF_TOON_GLOW_MAGENTA,ROOF_TOON_GLOW_CYAN,0.5+0.5*sin(az*1.3+0.8));
    float3 c=lerp(ROOF_TOON_HORIZON,ROOF_TOON_UPPER,smoothstep(0.02,0.22,h));c=lerp(c,ROOF_TOON_ZENITH,smoothstep(0.2,0.75,h));
    c=lerp(c,glow,(1.0-smoothstep(0.0,0.07,h))*step(-0.02,h));
    if(h<0.0)c=lerp(glow,ROOF_TOON_STREET,smoothstep(0.0,0.12,-h));
    return c;
}
// The haze toward a direction at height y (the street's glow low down, the horizon's colour above it).
float3 RoofToonHaze(float3 d,float y)
{
    float az=atan2(-d.x,-d.z);float3 glow=lerp(ROOF_TOON_GLOW_MAGENTA,ROOF_TOON_GLOW_CYAN,0.5+0.5*sin(az*1.3+0.8));
    float3 c=lerp(ROOF_TOON_HORIZON,glow,ROOF_TOON_HAZE_GLOW);float low=exp(-max(0.0,y-(ROOF_STREET+20.0))/55.0);
    return lerp(c*0.85,ROOF_TOON_STREET,low*0.6);
}
// Soft stairs (NativeToon.cginc's ToonStairs with a wider riser) for the haze's depth planes.
float RoofToonPlanes(float x){float k=x*4.0;float f=floor(k);return (f+smoothstep(0.3,0.7,k-f))/4.0;}
// Distance haze in a few soft depth planes, plus the glowing street fog.
float3 RoofToonFog(float3 c,float3 w)
{
    float3 v=w-_WorldSpaceCameraPos;float z=length(v);float3 d=v/max(z,0.0001);float aerial=1.0-exp(-z*_RoofToonFog.y);
    aerial=lerp(aerial,RoofToonPlanes(aerial),_RoofToonFog.x);
    float low=exp(-max(0.0,w.y-ROOF_STREET)/_RoofToonFog.z)*(1.0-exp(-z/90.0));
    return lerp(c,RoofToonHaze(d,w.y),saturate(min(0.95,aerial+low*_RoofToonFog.w)));
}
// One layer of cumulus round the horizon (NativeNightSky's SkyCumulus shape: cells, four puffs cut flat at the base, a
// crisp silhouette), lit from below by the city: the puff's round normal turned down takes `lit`, the rest `shade`, darker
// toward the top, with a bright underside rim. Returns premultiplied colour and coverage.
void RoofToonPuff(float2 p,float2 c,float r,inout float sd,inout float3 n)
{
    float2 q=(p-c)/r;float d=length(p-c)-r;if(d<sd){sd=d;float l=min(length(q),1.0);n=float3(q*min(1.0,1.0/max(length(q),0.0001))*l,sqrt(max(0.0,1.0-l*l)));}
}
float4 RoofToonCumulus(float az,float el,float px,float cells,float base,float size,float seed,float cover,float3 lit,float3 shade)
{
    float4 acc=float4(0.0,0.0,0.0,0.0);float cell=6.2831853/cells;float c0=floor(az/cell);
    for(int k=-1;k<=1;k++)
    {
        float ci=c0+float(k);float cw=ci-cells*floor(ci/cells);
        float h1=RoofHash1(float3(cw,seed,1.0)),h2=RoofHash1(float3(cw,seed,2.0)),h3=RoofHash1(float3(cw,seed,3.0)),h4=RoofHash1(float3(cw,seed,4.0)),h5=RoofHash1(float3(cw,seed,5.0));
        if(h1<=cover)
        {
            float cx=(ci+0.5+(h2-0.5)*0.5)*cell;float wd=cell*(0.3+0.2*h3)*size;float b=base*size+(h4-0.5)*0.3*wd;
            float sd=1000.0;float3 n=float3(0.0,0.0,1.0);float2 p=float2(az,el);
            float r0=wd*(0.3+0.08*h5),r1=wd*(0.5+0.1*h4),r2=wd*(0.44+0.1*h2),r3=wd*(0.3+0.1*h3);
            RoofToonPuff(p,float2(cx-0.62*wd,b+0.45*r0),r0,sd,n);RoofToonPuff(p,float2(cx-0.2*wd,b+0.55*r1),r1,sd,n);
            RoofToonPuff(p,float2(cx+0.24*wd,b+0.5*r2),r2,sd,n);RoofToonPuff(p,float2(cx+0.64*wd,b+0.4*r3),r3,sd,n);
            float cut=max(sd,b-el);float a=1.0-smoothstep(-px,px,cut);
            if(a>0.0)
            {
                float t=smoothstep(0.02,0.16,dot(n,normalize(float3(0.0,-0.8,0.6))));float3 tone=lerp(shade,lit,t);
                tone*=lerp(1.0,0.72,smoothstep(0.0,0.75*wd,el-b));
                float rim=(1.0-smoothstep(0.0,2.5*px+0.03*wd,el-b))*(1.0-smoothstep(0.0,2.5*px,-cut));tone=lerp(tone,lit*1.35,rim*0.8);
                acc.rgb+=(1.0-acc.a)*tone*a;acc.a+=(1.0-acc.a)*a;
            }
        }
    }
    return acc;
}
// ---- ROOFTOP TOON SHARED END ----
#endif
