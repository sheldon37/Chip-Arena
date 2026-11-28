Shader "ChipArena/NativeRoundsField"
{
    // The ROUNDS adaptation packs' field surfaces (Claude, 2026-10-19; owner: "Some effects may have no visuals of their own yet,
    // such as ... the fire, frost and slow-time fields"): Core NativeRoundsFields builds one dynamic mesh a frame of ground discs
    // (rings out to exactly the drawn hazard radius, their outermost ring the boundary) and, for the slow-time and gravity fields
    // and any field hanging high off the floor, the authority sphere clipped at the floor; the proc pulses add their rings. Runtime
    // NativeRoundsFxPresentation draws it with this material, premultiplied (One OneMinusSrcAlpha), no depth write, depth-tested,
    // in the transparent queue just before the shared effects renderer's sprites (so flames, mist and motes lie over it). Per vertex
    // (Core RoundsFieldVertex): the look's hot, cool and dark colours as sRGB hex in the normal slot, then
    //  A: the point's offset from the field's centre (m), seed;  B: style, variant, age (s), life (s);
    //  C: layer (0 ground, 1 sphere), the ground radius (m; on the sphere the floor's offset from the centre), the authority radius
    //     (m), strength 0..1.
    // Every look is a pure function of these (age is the field's own clock), so host, guests, the killcam and the DEV preview draw
    // the same: fire burns as a charred ember bed whose leading edge is a flame front on the boundary; frost forms out from the
    // centre, ferns and cracks as it ages and shatters at its expiry; slow time is a clock ring and contracting ripples with a
    // lensing shell; repulsion rolls pressure rings out to its edge; gravity winds spiral arms in; the storm crawls with arcs.
    // The boundary line is drawn only while the field lives (age <= life). Lit by the key light and the SH ambient; linear fog.
    // No textures, SM 3.0.
    Properties {}
    SubShader
    {
        Tags { "Queue"="Transparent-2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata_rf {float4 vertex:POSITION;float3 normal:NORMAL;float4 a:TEXCOORD0;float4 b:TEXCOORD1;float4 c:TEXCOORD2;};
        // SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
        #define RF_FIRE 1.0
        #define RF_FROST 2.0
        #define RF_TIME 3.0
        #define RF_PUSH 4.0
        #define RF_PULL 5.0
        #define RF_STORM 6.0
        #define RF_PULSE 7.0
        bool RfIs(float k,float id){return abs(k-id)<0.5;}
        float RfHash(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
        float RfNoise(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);
            return lerp(lerp(RfHash(i),RfHash(i+float2(1.0,0.0)),u.x),lerp(RfHash(i+float2(0.0,1.0)),RfHash(i+float2(1.0,1.0)),u.x),u.y);
        }
        float RfFbm(float2 p){return RfNoise(p)*0.5+RfNoise(p*2.03+float2(11.7,3.1))*0.3+RfNoise(p*4.11+float2(2.3,17.9))*0.2;}
        // Cellular noise: x the nearest feature distance, y the second, z the nearest cell's hash.
        float3 RfCells(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float d1=8.0;float d2=8.0;float id=0.0;
            for(int y=-1;y<=1;y++)
            {
                for(int x=-1;x<=1;x++)
                {
                    float2 g=float2(float(x),float(y));float h=RfHash(i+g);float2 o=float2(h,RfHash(i+g+float2(19.1,7.3)));
                    float2 r=g+o*0.85+0.075-f;float d=dot(r,r);
                    if(d<d1){d2=d1;d1=d;id=h;}else if(d<d2){d2=d;}
                }
            }
            return float3(sqrt(d1),sqrt(d2),id);
        }
        // The distance to the nearest edge of a hex grid of cell size s at p (0 on a seam).
        float RfHex(float2 p,float s)
        {
            float2 q=p/s;float2 r=float2(1.0,1.7320508);float2 h=r*0.5;
            float2 a=q-r*floor(q/r+0.5);float2 b=q-h-r*floor((q-h)/r+0.5);float2 g=dot(a,a)<dot(b,b)?a:b;
            g=abs(g);return (0.5-max(dot(g,float2(0.5,0.8660254)),g.x))*s;
        }
        // sRGB hex (exact in a float) to linear.
        float3 RfHexColor(float h)
        {
            float r=floor(h/65536.0);float g=floor((h-r*65536.0)/256.0);float b=h-r*65536.0-g*256.0;
            float3 c=float3(r,g,b)/255.0;return pow(max(c,float3(0.0,0.0,0.0)),float3(2.2,2.2,2.2));
        }
        // Heat to light (linear HDR) through a dull red, red, orange and yellow to white: the embers' and the fire front's.
        float3 RfHeat(float t,float3 hot)
        {
            t=saturate(t);
            float3 c=lerp(float3(0.0,0.0,0.0),float3(0.32,0.03,0.004),smoothstep(0.05,0.25,t));
            c=lerp(c,float3(1.05,0.16,0.02),smoothstep(0.22,0.45,t));
            c=lerp(c,hot*1.7,smoothstep(0.42,0.7,t));
            c=lerp(c,float3(3.1,2.4,1.2),smoothstep(0.75,1.0,t));
            return c;
        }
        struct RfOut {float3 albedo;float alpha;float3 emit;};
        RfOut RfZero(){RfOut o;o.albedo=float3(0.0,0.0,0.0);o.alpha=0.0;o.emit=float3(0.0,0.0,0.0);return o;}
        // The boundary line: a crisp bright band on the ground radius, drawn only while the field lives.
        float RfRim(float d,float width,float alive){return alive*(1.0-smoothstep(0.0,width,d));}
        RfOut RfGround(float3 L,float3 view,float4 a,float4 b,float4 c,float3 hot,float3 cool,float3 dark)
        {
            RfOut o=RfZero();float seed=a.w;float style=b.x;float variant=b.y;float age=b.z;float life=b.w;float g=max(c.y,0.01);float st=c.w;
            float r=length(L.xz);float rho=r/g;float d=g-r;float post=age-life;float alive=step(age,life)*step(0.0,age);
            float ang=atan2(L.z,L.x);float2 sd=float2(seed*1.37,seed*0.71);
            if(RfIs(style,RF_FIRE))
            {
                // Burnt ground: soot-black char with a charcoal grain, glowing seams between its plates and a few hot coals, a burnt
                // hex trace (the cyber deck), and the burning front on the boundary licking inward, all flickering on the field's own
                // clock; it all cools to dull red then black after the expiry. Thornwake's burning vines through the bed.
                float cool0=post>0.0?saturate(1.0-post/1.2):1.0;float ign=smoothstep(0.0,0.1,age);
                float2 p=L.xz*2.3+sd;float2 w=float2(RfFbm(p*0.55+float2(age*0.12,0.0)),RfFbm(p*0.55+float2(3.7,-age*0.1)))-0.5;
                float3 cl=RfCells(p+w*1.3);float seam=1.0-smoothstep(0.015,0.07,cl.y-cl.x);
                float flick=0.5+0.5*RfNoise(L.xz*1.9+float2(age*3.3,-age*2.6)+sd);float slow=0.6+0.4*RfNoise(L.xz*0.7-float2(age*0.7,age*0.4)+sd.yx);
                float coal=smoothstep(0.8,0.97,cl.z)*(1.0-smoothstep(0.0,0.4,cl.x));
                float hexs=1.0-smoothstep(0.0,0.022,RfHex(L.xz+sd,0.46));float grain=RfFbm(L.xz*7.0+sd);
                float T=0.04+seam*0.42*flick*slow+coal*0.38*slow+hexs*0.16*flick*(0.5+0.5*rho);
                float tongues=RfNoise(float2(ang*g*2.6+seed,age*4.0))*0.55+RfNoise(float2(ang*g*6.3-seed,age*8.0))*0.45;
                float front=exp(-d/(0.16+0.22*tongues))*(0.62+0.45*tongues);
                T=max(T,front*0.92*alive+front*0.35*(1.0-alive)*cool0);T*=ign*(post>0.0?cool0*cool0:1.0);
                if(RfIs(variant,1.0)){float vine=1.0-smoothstep(0.0,0.04,abs(RfFbm(float2(ang*3.0+seed,r*1.3))-0.5)*1.5);T=max(T,vine*0.5*flick*ign*cool0);}
                o.emit=RfHeat(T,hot)*st;
                float fade=post>0.0?1.0-smoothstep(0.6,1.4,post):1.0;
                o.albedo=dark*(0.35+0.55*grain)+float3(0.004,0.0035,0.003);o.alpha=(0.9-0.2*grain)*fade*ign*(0.6+0.4*smoothstep(0.0,0.12,d));
                o.emit+=hot*(1.6*RfRim(d,0.04,alive));
            }
            else if(RfIs(style,RF_FROST))
            {
                // Frost forms out to the boundary in .18 s: a thin glassy film that shows the floor through it, feathered white frost
                // growing in fern-like streaks from the centre and banked thick at the boundary, crystal plates catching the light,
                // glints; cracks spread as it ages and run through it all at the expiry as it shatters away.
                float front=g*smoothstep(0.0,0.18,age);if(r>front+0.02)return o;
                float2 p=L.xz*1.4+sd;float3 pl=RfCells(p);
                float fern=RfFbm(float2(ang*9.0+seed,r*3.2))*0.55+RfFbm(float2(ang*21.0-seed,r*8.0))*0.45;fern=smoothstep(0.5,0.82,fern);
                float rime=RfFbm(L.xz*3.4+sd*1.7);float bank=smoothstep(0.55,0.0,d)*(0.6+0.4*rime);
                float density=saturate(0.1+0.55*fern+0.75*bank+0.14*(rime-0.5)+0.1*smoothstep(0.6,0.95,pl.z));
                float crackAmt=post>0.0?1.0:smoothstep(0.25,life,age)*0.7;float3 ck=RfCells(L.xz*1.1+sd.yx);
                float crack=(1.0-smoothstep(0.0,0.028,ck.y-ck.x))*step(ck.z,crackAmt);
                float lattice=RfIs(variant,1.0)?1.0-smoothstep(0.0,0.025,RfHex(L.xz+sd,0.62)):0.0;
                float3 frostLight=lerp(hot,float3(1.0,1.0,1.0),0.55);float3 ice=lerp(float3(0.16,0.22,0.3),float3(0.84,0.91,0.98),density);ice*=0.92+0.12*pl.z;
                float glint=step(0.988,RfHash(floor(L.xz*26.0)+sd))*(0.5+0.5*sin(age*7.0+RfHash(floor(L.xz*26.0))*30.0));
                float sheen=pow(saturate(1.0-view.y),3.0);
                float formed=1.0-smoothstep(front-0.3,front,r)*(1.0-step(0.18,age));
                float melt=post>0.0?1.0-smoothstep(0.0,0.5,post):1.0;
                o.albedo=ice;o.alpha=(0.22+0.66*density)*melt*formed*(1.0-0.3*crack);
                o.emit=frostLight*(0.06+0.2*density)+frostLight*(0.35*sheen*(1.0-density))+cool*(1.1*crack*(1.0-0.5*step(0.0,post)))+float3(1.6,1.8,2.0)*(2.2*glint*density)+cool*(0.5*lattice)+hot*(1.1*lattice);
                o.emit+=float3(1.3,1.5,1.7)*(2.4*RfRim(abs(r-front),0.05,1.0-step(0.18,age)));
                o.emit+=hot*(1.3*RfRim(d,0.04,alive));o.emit*=st*formed*melt;
            }
            else if(RfIs(style,RF_TIME))
            {
                // A clock ring just inside the boundary (sixty ticks, every fifth long) turning at a fifth of a normal pace, ripples
                // contracting slowly to the centre, a faint lens film; the interdicts' freeze shock runs out to the edge at their
                // birth; Temporal Sacrifice's hourglass and Lunar Interdict's crescent at the centre; Shadow Interdict pools dark.
                float slow=0.2;float th=(ang/6.2831853+0.5)+age*0.02*slow*5.0;float tk=frac(th*60.0);float major=step(frac(th*12.0),0.08);
                float band=step(g-0.42,r)*step(r,g-0.14);float tick=band*(1.0-smoothstep(0.08,0.16,abs(tk-0.5)*2.0-0.75))*(0.45+0.55*major)*step(g-0.42+0.12*(1.0-major),r);
                float ripple=pow(abs(sin((rho*3.5+age*0.18)*3.1415927)),40.0)*(0.15+0.85*rho)*0.6;
                float birth=RfRim(abs(r-g*smoothstep(0.0,0.25,age)),0.12,1.0-step(0.32,age));
                float film=RfIs(variant,4.0)?0.42:0.12;
                o.albedo=dark;o.alpha=film*st*(post>0.0?1.0-smoothstep(0.0,0.5,post):1.0);
                o.emit=hot*(1.1*tick+0.35*ripple+3.0*birth)+cool*0.06;
                float sig=0.0;
                if(RfIs(variant,1.0)){float2 q=L.xz/0.9;float tri=max(abs(q.x)*1.4-abs(q.y),abs(q.y)-1.0);sig=1.0-smoothstep(0.0,0.06,abs(tri));}
                if(RfIs(variant,3.0)){float c1=length(L.xz-float2(0.0,0.0))-0.8;float c2=length(L.xz-float2(0.35,0.12))-0.7;sig=(1.0-smoothstep(0.0,0.05,abs(max(c1,-c2))));}
                if(RfIs(variant,2.0)){float scan=step(0.7,RfHash(float2(floor(ang*14.0),floor(age*18.0))));o.emit+=cool*(1.2*scan*band);}
                o.emit+=cool*(1.6*sig)*step(0.0,age);o.emit+=hot*(2.4*RfRim(d,0.05,alive));o.emit*=st;
            }
            else if(RfIs(style,RF_PUSH))
            {
                // Pressure rings rolling out from the centre to the boundary, strongest near the middle; the Emergency Vent's
                // hazard chevrons round its rim.
                float wave=frac(rho*1.6-age*2.2);float ring=pow(1.0-abs(wave-0.5)*2.0,18.0)*(1.0-0.5*rho);
                float dust=RfFbm(L.xz*2.5+sd+float2(0.0,-age*0.8))*0.3;float chev=0.0;
                if(RfIs(variant,1.0)){float cv=frac(ang/6.2831853*g*2.0+r*0.6-age*0.6);chev=step(g-0.35,r)*step(cv,0.5);}
                o.albedo=dark*(1.0+dust);o.alpha=(0.16+0.1*dust+0.5*chev)*st*(post>0.0?1.0-smoothstep(0.0,0.4,post):1.0);
                o.emit=hot*(1.5*ring*step(0.0,-post+0.0001)+0.9*chev)+hot*(2.4*RfRim(d,0.06,alive));o.emit*=st;
            }
            else if(RfIs(style,RF_PULL))
            {
                // Spiral arms winding in to the core, darker toward it; the event-horizon line on the boundary.
                float arms=3.0;float s=frac((ang/6.2831853)*arms+log(max(rho,0.02))*0.9+age*0.45);float arm=pow(1.0-abs(s-0.5)*2.0,6.0);
                float glow=arm*(0.25+0.75*smoothstep(0.05,0.9,rho))*(0.7+0.3*RfNoise(L.xz*3.0+float2(age,0.0)));
                o.albedo=dark;o.alpha=(0.35*(1.0-rho*0.6))*st*(post>0.0?1.0-smoothstep(0.0,0.5,post):1.0);
                o.emit=hot*(1.2*glow)+cool*(0.5*(1.0-rho))+hot*(2.6*RfRim(d,0.05,alive));o.emit*=st;
            }
            else if(RfIs(style,RF_STORM))
            {
                // A charged sheet with arcs crawling over it, re-struck twelve times a second (violet and white).
                float tick=floor(age*12.0);float n1=RfFbm(L.xz*1.3+sd+float2(tick*0.37,tick*0.71));float arc=1.0-smoothstep(0.0,0.03,abs(n1-0.5));
                float n2=RfFbm(L.xz*2.9-sd+float2(tick*0.53,0.0));float arc2=1.0-smoothstep(0.0,0.02,abs(n2-0.5));
                o.albedo=dark;o.alpha=0.22*st*(post>0.0?1.0-smoothstep(0.0,0.4,post):1.0);
                o.emit=float3(1.4,1.2,1.6)*(1.4*arc)+hot*(1.0*arc2)+cool*0.08+hot*(2.4*RfRim(d,0.05,alive));o.emit*=st*(0.8+0.4*RfHash(float2(tick,seed)));
            }
            else
            {
                // A pulse: a band running out to the radius in .16 s (the boundary once there), a flash filling it, arcs or scorched
                // fragments by variant.
                float front=g*smoothstep(0.0,0.16,age);float fade=1.0-smoothstep(0.15,0.55,age);
                float bandv=RfRim(abs(r-front),0.16,1.0)*fade;float fill=(1.0-smoothstep(0.0,0.14,age))*(1.0-rho*0.5);
                float extra=0.0;if(RfIs(variant,1.0)){float n=RfFbm(L.xz*2.0+sd+float2(floor(age*20.0),0.0));extra=(1.0-smoothstep(0.0,0.03,abs(n-0.5)))*fade*step(r,front);}
                if(RfIs(variant,2.0)){extra=step(0.8,RfHash(floor(L.xz*9.0)+sd))*step(r,front)*0.6*(1.0-smoothstep(0.2,0.55,age));}
                o.albedo=dark;o.alpha=0.1*fade*step(r,front+0.05);
                o.emit=hot*(2.8*bandv+0.55*fill+1.6*extra)+cool*(0.15*fill);
            }
            return o;
        }
        // The sphere (the authority's volume, clipped at the floor): a fresnel rim, faint latitude and longitude lines turning at
        // the slowed pace, ripples running down it, a flash at the birth; the gravity well's darker lens.
        RfOut RfSphere(float3 L,float3 n,float3 view,float4 a,float4 b,float4 c,float3 hot,float3 cool,float3 dark)
        {
            RfOut o=RfZero();float style=b.x;float age=b.z;float life=b.w;float R=max(c.z,0.01);float st=c.w;float post=age-life;
            float f=1.0-abs(dot(n,view));float fr=f*f*f;float lat=asin(clamp(n.y,-1.0,1.0));float lon=atan2(n.z,n.x)+age*0.05;
            float grid=(1.0-smoothstep(0.0,0.025,abs(frac(lat*3.8197)-0.5)*2.0-0.95))+(1.0-smoothstep(0.0,0.025,abs(frac(lon*1.9099)-0.5)*2.0-0.95));
            float ripple=pow(abs(sin((n.y*3.0+age*0.3)*3.1415927)),30.0);float birth=exp(-age*7.0)*step(0.0,age);
            float end=post>0.0?1.0-smoothstep(0.0,0.45,post):1.0;float pull=RfIs(style,RF_PULL)?1.0:0.0;
            float k=RfIs(style,RF_TIME)||pull>0.5?1.0:0.45;
            o.albedo=dark;o.alpha=(0.05+0.12*pull)*fr*st*end;
            o.emit=(hot*(0.9*fr+0.14*grid*fr+0.35*ripple*fr)+cool*(0.25*birth)+hot*(2.0*birth*fr))*st*end*k;
            return o;
        }
        // SHARED END
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off ZTest LEqual Cull Off Blend One OneMinusSrcAlpha
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityLightingCommon.cginc"
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 hexes:TEXCOORD1;float4 a:TEXCOORD2;float4 b:TEXCOORD3;float4 c:TEXCOORD4;UNITY_FOG_COORDS(5)};
            v2f vert(appdata_rf v)
            {
                v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);float4 w=mul(unity_ObjectToWorld,v.vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;
                o.hexes=v.normal;o.a=v.a;o.b=v.b;o.c=v.c;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 view=normalize(_WorldSpaceCameraPos-i.world);float3 hot=RfHexColor(i.hexes.x);float3 cool=RfHexColor(i.hexes.y);float3 dark=RfHexColor(i.hexes.z);
                RfOut s;float3 n=float3(0.0,1.0,0.0);
                if(i.c.x>0.5)
                {
                    // The sphere: clipped at the floor (C.y is the floor's offset from the centre).
                    if(i.a.y<i.c.y)discard;n=normalize(i.a.xyz);s=RfSphere(i.a.xyz,n,view,i.a,i.b,i.c,hot,cool,dark);
                }
                else s=RfGround(i.a.xyz,view,i.a,i.b,i.c,hot,cool,dark);
                float3 light=ShadeSH9(float4(n,1.0))+_LightColor0.rgb*saturate(dot(n,_WorldSpaceLightPos0.xyz));
                float4 col=float4(s.albedo*light*s.alpha+s.emit,s.alpha);
                // Premultiplied: the fog takes the covered share to the fog colour and fades the light that adds.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                UNITY_CALC_FOG_FACTOR_RAW(length(i.world-_WorldSpaceCameraPos));col.rgb=lerp(unity_FogColor.rgb*col.a,col.rgb,saturate(unityFogFactor));
                #endif
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
