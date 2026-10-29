Shader "ChipArena/NativeRooftopFacade"
{
    // The home screen's towers (owner 2026-10-29: "Improve building design (It still appears "voxels") and flat"; Core
    // NativeRooftopCity.Tower quads, drawn by NativeRooftopPresentation): one short opaque pass for every tower facade.
    //  uv0.xy  metric face coordinates (x along the face, y the world height); uv0.w the tower's roof height
    //  uv1     seed, style (0 glazed, 1 residential, 2 corporate, 3 masonry), hue, the share of its rooms lit
    // Each style has its own rhythm: a glazed curtain wall of narrow panes between thin mullions, punched residential windows
    // with their sills, the corporate tower's ribbon windows over dark spandrels and its slab lights, deep-set masonry windows
    // between pilasters. Behind every pane a room (interior mapping: the ray into a box the bay's width and the storey's
    // height, 4.5-6 m deep) with its lit ceiling strip, back wall, desk band and now and then someone at it; offices light in
    // runs along a storey, homes one by one; mostly warm lamps and cool office white, a few neon tints; blinds, a rare flicker.
    // The room's detail fades to a plain lit pane (brighter under its ceiling) once a bay is a dozen pixels, and to the
    // storey's lit band, then the tower's average, once it is a few, so nothing shimmers. The walls take the moon from one side
    // and the street's magenta glow from below (the anime style: the cool shade palette's two tones), the screens' and signs'
    // spill (NativeRooftopLights.cginc) and, on crowned towers, their roof uplights' wash. No textures; HDR out for
    // NativeBloom; the city fog last.
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            // The anime map style's environment mark (Resources/NativeToonEdge.shader), as the rooftop's other surfaces.
            Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "NativeRooftopCommon.cginc"
            #include "NativeRooftopLights.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;};
            struct v2f {float4 pos:SV_POSITION;float4 uv0:TEXCOORD0;float4 uv1:TEXCOORD1;float3 world:TEXCOORD2;float3 normal:TEXCOORD3;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv0=v.uv0;o.uv1=v.uv1;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            float3 Pal(float h){return h<.45?float3(.30,1.9,2.4):h<.7?float3(2.2,.35,1.9):h<.88?float3(1.5,1.3,1.1):float3(.55,.75,2.2);}
            // A room's light by its hash: warm lamps most, cool office white, a fluorescent green-white, now and then a neon tint.
            float3 RoomLight(float h){return h<.40?float3(1.0,.60,.30):h<.74?float3(.60,.74,1.0):h<.84?float3(.74,.95,.82):h<.92?float3(1.0,.34,.70):float3(.30,.86,1.0);}
            float4 frag(v2f i):SV_Target
            {
                // the seed is a whole number, rounded back from the interpolator: a hash of it off by an interpolation
                // ulp would differ pixel to pixel (a speckle over every facade)
                float3 n=normalize(i.normal);float seed=frac(floor(i.uv1.x+.5)*.6180339)*97.0,style=i.uv1.y,hue=i.uv1.z,share=i.uv1.w;
                float s1=step(.5,style)-step(1.5,style),s2=step(1.5,style)-step(2.5,style),s3=step(2.5,style),s0=1.0-s1-s2-s3;
                // the bay (width, storey) and its pane (side margin, sill, head as fractions of the bay)
                float2 bay=float2(1.5,3.6)*s0+float2(2.8,3.0)*s1+float2(3.2,4.0)*s2+float2(2.4,3.4)*s3;
                float3 pane=float3(.025,.07,.985)*s0+float3(.24,.30,.82)*s1+float3(.012,.34,.93)*s2+float3(.30,.20,.80)*s3;
                float2 g=float2(i.uv0.x,i.uv0.y-ROOF_STREET)/bay;
                // the bay's pixel footprint, before any flow control (derivatives there are undefined)
                float2 fw=max(fwidth(g),float2(1e-4,1e-4));float px=max(fw.x,fw.y);
                float2 id=floor(g),f=frac(g);float vertical=step(abs(n.y),.5);
                float2 aa=fw*1.25;
                float wx=smoothstep(pane.x-aa.x,pane.x+aa.x,f.x)*smoothstep(pane.x-aa.x,pane.x+aa.x,1.0-f.x);
                float wy=smoothstep(pane.y-aa.y,pane.y+aa.y,f.y)*(1.0-smoothstep(pane.z-aa.y,pane.z+aa.y,f.y));
                float mask=wx*wy*vertical;
                // which rooms are lit: whole storeys by the tower's share, broken by dark runs of offices (a few bays long, single
                // homes), a few strays on the dark floors; each run one light
                float run=floor(2.0+RoofHash1(float3(seed,2.0,7.0))*(7.0*s0+1.5*s1+6.0*s2+2.0*s3))-s1;
                float2 rid=float2(floor(id.x/run),id.y)+floor(seed*13.0);
                float storey=step(RoofHash2(float2(id.y*.731+seed,seed*1.7)),share);
                float isLit=storey*step(RoofHash2(rid*1.37+.5),.74)+(1.0-storey)*step(RoofHash2(rid+seed*3.1),share*.18);
                float rh=RoofHash2(rid*2.31+seed);float3 lc=RoomLight(frac(rh+hue*.13))*(.55+.5*RoofHash2(rid+7.7));
                float flick=1.0-.6*step(.997,RoofHash2(rid+floor(_RoofTime*4.0)*.31));
                // the room behind the pane: the view ray in the face's frame (x along it, y up, z into the tower)
                float3 t=normalize(cross(n,float3(0,1,0))+float3(1e-5,0,0));float3 vw=normalize(i.world-_WorldSpaceCameraPos);
                float3 vt=float3(dot(vw,t),vw.y,max(dot(vw,-n),.06));float depth=4.5+1.5*s1;
                float3 p0=float3(f.x*bay.x,f.y*bay.y,0.0);float3 sg=step(0.0,vt);float3 vs=vt+(sg*2.0-1.0)*1e-4;
                float sx=(sg.x*bay.x-p0.x)/vs.x,sy=(sg.y*bay.y-p0.y)/vs.y,sz=depth/vt.z;
                float s=min(min(sx,sy),sz);float3 h=p0+vt*s;
                float back=step(sz,min(sx,sy)),side=(1.0-back)*step(sx,sy),level=(1.0-back)*(1.0-side),over=level*sg.y,ground=level*(1.0-sg.y);
                float strip=smoothstep(.32,.06,abs(h.x/bay.x-.5))*over;float desk=step(.70,h.y)*step(h.y,.95);
                float2 at=float2((h.x/bay.x-.25-RoofHash2(rid+5.1)*.5)*bay.x,h.y-1.32);
                float person=back*step(.80,RoofHash2(rid+3.3))*(1.0-smoothstep(.16,.24,length(at*float2(1.7,.75))));
                float room=back*(.55+.25*h.y/bay.y)*(1.0-.45*desk)+side*.36+over*(.42+1.7*strip)+ground*.15;
                room*=(1.0-.4*saturate(h.z/depth))*(1.0-.85*person);
                // a plain lit pane where the room is a dozen pixels or less: brighter under its ceiling, as the room averages
                float rise=saturate((f.y-pane.y)/max(pane.z-pane.y,.01));float plain=.38+.34*rise;
                float detail=1.0-smoothstep(.045,.09,px);
                float3 lamp=lc*lerp(plain,room,detail);
                float blinds=step(.74,RoofHash2(rid*.91+1.9))*(1.0-s0*.6);float slat=lerp(.5,step(.45,frac(f.y*bay.y*5.0)),1.0-smoothstep(.012,.025,fw.y));
                lamp=lerp(lamp,lc*(.30+.24*slat),blinds*.8);
                float3 dark=float3(.006,.008,.016)*(1.0+back*detail);
                float3 inside=lamp*isLit*flick+dark*(1.0-isLit);
                // the glass: the night sky mirrored (strongest on the curtain wall), lit rooms seen through its tint
                float3 glass=RoofMirror(i.world,n,.04+.06*s0)*(.7+.9*s0);inside*=1.0-.25*s0;
                // the walls: each style's material, the moon from one side, the street's glow from below, the spill, the crown wash
                float3 albedo=float3(.022,.028,.058)*s0+float3(.046,.044,.058)*s1+float3(.020,.026,.052)*s2+float3(.056,.034,.036)*s3;
                albedo*=.8+.4*RoofHash1(float3(seed,1.3,2.1));
                float grime=RoofNoise(float3(i.uv0.x*.35,i.uv0.y*.045,seed*7.0))*.3;
                // mullions a touch lighter than the spandrels; masonry courses and pilasters; the residential sill band
                float mull=s0*(1.0-wx)*.6;float course=s3*.22*step(.9,frac(i.uv0.y*3.3));float pilaster=s3*.3*step(.8,frac(g.x/3.0));
                float sill=s1*.5*step(abs(f.y-pane.y+.03),.03)*step(.2,f.x)*step(f.x,.8);
                float3 moonDir=normalize(float3(-.72,.42,.18));float key=saturate(dot(n,moonDir));
                float band=lerp(key,smoothstep(.18,.30,key),step(.5,_ArenaToon.x));
                float crown=smoothstep(18.0,0.0,i.uv0.w-i.world.y)*step(.6,RoofHash1(float3(seed,4.0,4.0)));
                float3 light=RoofCityGlow(i.world,n)*1.4+float3(.10,.14,.30)*band+RoofSpill(i.world,n)+Pal(hue)*crown*.12;
                float3 wall=albedo*(1.0-grime)*(1.0-course)*(1.0+mull+pilaster+sill)*light*2.2;
                // the corporate grid's slab lights, a line of the tower's hue at every other storey's floor
                float slab=s2*vertical*(1.0-smoothstep(.02,.02+aa.y*2.0,f.y))*step(.45,RoofHash1(float3(seed,8.0,1.0)))*step(.5,frac(id.y*.5));
                wall+=Pal(hue)*slab*.30;
                // a few pixels a bay or less: each run of offices one lit or dark segment along its storey; a few pixels a run, the
                // storey's band in its own light; a storey under a pixel, the tower's average (never a shimmer of single panes)
                float3 mean=(RoomLight(.2)*.4+RoomLight(.6)*.34+RoomLight(.8)*.1+RoomLight(.9)*.08+RoomLight(.96)*.08)*.8;
                float3 tone=lerp(mean,RoomLight(RoofHash2(float2(id.y,seed*3.3)))*.8,.5);
                float3 segment=lc*isLit*flick*.55*(1.0-.25*s0);
                float3 storeyMean=tone*(storey*.74+(1.0-storey)*share*.18),towerMean=mean*(share*.74+(1.0-share)*share*.18);
                float far=smoothstep(.14,.36,px),farRun=smoothstep(.22,.55,fw.x/max(run,1.0)),farFloor=smoothstep(.35,.9,fw.y);
                float cover=lerp(wy*(1.0-2.0*pane.x),(1.0-2.0*pane.x)*(pane.z-pane.y),farFloor)*vertical;
                float3 near=lerp(wall,inside+glass,mask);
                float3 distant=lerp(wall,lerp(lerp(segment,storeyMean,farRun),towerMean,farFloor)+glass*.5,cover);
                float3 c=lerp(near,distant,far);
                return float4(RoofFog(c,i.world),1.0);
            }
            ENDCG
        }
        // A depth-only shadow caster: the towers in the camera's depth texture (the anime outline reads it); nothing casts.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
            struct v2f {V2F_SHADOW_CASTER;};
            v2f vert(appdata v){v2f o;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)return o;}
            float4 frag(v2f i):SV_Target{SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
