Shader "ChipArena/NativeBlockLit"
{
    // Arena blocks for NativeArenaDressing (Core/NativeBlockLooks.cs; ARENA_PROPOSAL.md 2.2 "Block geometry" and "Surfaces",
    // 2.3, 2.4). Built-in forward base pass and a shadow caster; Linear, no tone mapping, no textures, SM 3.5 (no derivatives
    // in a vertex stage). One unit mesh per damage stage is instanced for every block size and look: the vertex stage expands
    // its lattice to the instance's half extents with a fixed chamfer, the fragment stage draws the look in rest space
    // (object position plus the block's rest centre, so courses line up across blocks and stay glued to a falling piece).
    // Per instance: _BlockSize (half extents, chamfer), _BlockRest (rest centre, the block's local dust coat), _BlockColor (base
    // colour, linear; window light: the 3.9 telegraph's 8 Hz strobe then black, 1 elsewhere), _BlockLook (look id, health share
    // as NativeBlockLooks.Normalised gives it, seed, flags: band, tag, fragment, the band lamp lit amber or red, mesh turns).
    // The round's map-wide dust level is the _ArenaDustCoat global (Core NativeDustCoat, with the local coat). Lighting: the key light with
    // its shadow map and the SH ambient as Unity's Lambert applies them, plus, in place of an add pass (so no per-pixel
    // Unity light other than the key reaches these blocks; NativeLighting keeps the rim per-pixel on dressed maps, so it
    // never lands in the SH either), the rim light (_ArenaRim*, NativeLighting.ArenaRim), MapLooks' 8 practical lights
    // (_ArenaLight*, its own arrays, never the entrance stage's _StageLight*) and the 3 blast flashes (_ArenaFlash*,
    // NativeArenaDressing.Flashes); then the ground haze and fog.
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGINCLUDE
        #include "UnityCG.cginc"
        UNITY_INSTANCING_BUFFER_START(BlockProps)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BlockSize)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BlockRest)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BlockColor)
        UNITY_DEFINE_INSTANCED_PROP(float4,_BlockLook)
        UNITY_INSTANCING_BUFFER_END(BlockProps)
        struct appdata_block {float4 vertex:POSITION;float3 normal:NORMAL;float4 texcoord:TEXCOORD0;float4 texcoord1:TEXCOORD1;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
        // SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
        // Look ids: NativeBlockLooks.Keys order.
        #define LOOK_PLAIN 0.0
        #define LOOK_BRICK_RED 1.0
        #define LOOK_BRICK_BUFF 2.0
        #define LOOK_TERRACOTTA 3.0
        #define LOOK_CONCRETE 4.0
        #define LOOK_STEEL 5.0
        #define LOOK_TIMBER 6.0
        #define LOOK_TAR 7.0
        #define LOOK_ASPHALT 8.0
        #define LOOK_COBBLE 9.0
        #define LOOK_WINDOW 10.0
        #define LOOK_NEON 11.0
        #define LOOK_CORRUGATED 12.0
        #define LOOK_DEMOLITION 13.0
        #define LOOK_FACTORY_GRATING 14.0
        // Flags: NativeBlockLooks.Band, Tag, Fragment, LampAmber, LampRed, SpinY, SpinZ.
        #define FLAG_BAND 1u
        #define FLAG_TAG 2u
        #define FLAG_FRAGMENT 4u
        #define FLAG_LAMP_AMBER 8u
        #define FLAG_LAMP_RED 16u
        #define FLAG_SPIN_Y 32u
        #define FLAG_SPIN_Z 64u
        // Cyber City's bank (Core NativeCyberCityLooks.Bank): with this flag the look id names a slot of BkCyber's bank.
        #define FLAG_CYBER 128u
        #define CY_TILE 0.0
        #define CY_PANEL 4.0
        #define CY_METAL 5.0
        #define CY_GRATE 7.0
        #define CY_ROAD 8.0
        #define CY_GLASS 10.0
        #define CY_HOLO 11.0
        #define CY_TUBE 12.0
        // Ward Zero's slots of the same bank (Core NativeWardZeroLooks; docs/arenas/WARDZERO_PROPOSAL.md section 10).
        #define WZ_TILE 1.0
        #define WZ_BURN 3.0
        #define WZ_PANEL 2.0
        #define WZ_GLASS 13.0
        #define WZ_LIGHT 14.0
        #define WZ_INK 15.0
        // The wet sheen of puddles (a faint mirror of the violet city sky) and the sky a pane reflects, linear.
        #define CY_SHEEN float3(0.010,0.009,0.030)
        #define CY_SKYREF float3(0.012,0.010,0.034)
        // ARENA_PROPOSAL.md 3.10 colours in linear light: mortar #CDBB9E, window frame #1F2A30, lit window #FFB45E, rust
        // #7A3E22, demolition yellow #F4B321, black #1C1A18, danger red #D6332A, tag lettering #F2EDE4, dust #B8A68F, soot
        // #4A403A, the dust coat #B3A48E, fresh concrete #C9C2B4, fresh brick #D2BFA3 and the band lamp's amber #FFB000 (its red
        // is the danger red). The dark course #7E3A26 is the brick red #9C4A30 times .63 in linear light.
        #define BK_MORTAR float3(0.6105,0.4969,0.3419)
        #define BK_FRAME float3(0.0137,0.0232,0.0296)
        #define BK_LIT float3(1.0000,0.4564,0.1119)
        #define BK_RUST float3(0.1946,0.0482,0.0160)
        #define BK_YELLOW float3(0.9047,0.4508,0.0152)
        #define BK_BLACK float3(0.0116,0.0103,0.0091)
        #define BK_DANGER float3(0.6724,0.0331,0.0232)
        #define BK_INK float3(0.8879,0.8469,0.7758)
        #define BK_DUST float3(0.4793,0.3813,0.2747)
        #define BK_SOOT float3(0.0685,0.0513,0.0423)
        #define BK_COAT float3(0.4508,0.3712,0.2705)
        #define BK_FRESH_CONCRETE float3(0.5841,0.5395,0.4564)
        #define BK_FRESH_BRICK float3(0.6445,0.5210,0.3663)
        #define BK_LAMP_AMBER float3(1.0000,0.4342,0.0000)
        #define BK_DARK_COURSE 0.63
        // The grime ramp: side faces 22% darker up to 1.2 m above the block's base, fading out by 1.8 m (3.10 readability).
        #define BK_GRIME 0.22
        float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];float4 _ArenaFlashPos[3];float4 _ArenaFlashColor[3];float4 _ArenaRimDir;float4 _ArenaRimColor;float4 _ArenaHaze;float _ArenaHazeTop;float _ArenaDust;float _ArenaDustCoat;
        // The vertex lattice (NativeBlockLooks.Corner) and the instance's 180-degree mesh turns, which only pick other chipped corners.
        float3 BlockLattice(float3 a,float3 b,float3 h,float c,float flags,inout float3 n)
        {
            float3 p=a*h+sign(b)*min(abs(b)*c,0.42*abs(a)*h);
            uint fl=uint(flags+0.5);
            if((fl&FLAG_SPIN_Y)!=0u){p.xz=-p.xz;n.xz=-n.xz;}
            if((fl&FLAG_SPIN_Z)!=0u){p.xy=-p.xy;n.xy=-n.xy;}
            return p;
        }
        // A flat normal from screen derivatives, turned to face along the interpolated preview normal (the derivatives' sign
        // depends on the API and render target); the hint stands in for a sub-pixel facet.
        float3 BlockOrient(float3 c,float3 hint)
        {
            float l=length(c);float3 n=l>1e-12?c/l:normalize(hint);
            return dot(n,hint)<0.0?-n:n;
        }
        bool BkIs(float look,float id){return abs(look-id)<0.5;}
        float BkHash2(float2 p){float3 p3=frac(float3(p.x,p.y,p.x)*0.1031);p3+=dot(p3,p3.yzx+33.33);return frac((p3.x+p3.y)*p3.z);}
        float BkNoise(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);
            float a=BkHash2(i),b=BkHash2(i+float2(1.0,0.0)),c=BkHash2(i+float2(0.0,1.0)),d=BkHash2(i+float2(1.0,1.0));
            return lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y);
        }
        float BkFbm(float2 p){return BkNoise(p)*0.57+BkNoise(p*2.07+float2(13.1,7.7))*0.29+BkNoise(p*4.13+float2(3.3,19.9))*0.14;}
        // Coverage of joints `w` wide every `period` along x over a pixel `f` wide; the average coverage once the joint is sub-pixel.
        float BkJoint(float x,float period,float w,float f)
        {
            float d=abs(frac(x/period+0.5)-0.5)*period;float j=1.0-smoothstep(w*0.5-f,w*0.5+f,d);
            return lerp(j,w/period,saturate(f/w-0.5));
        }
        // A filtered square wave: 1 for the second half of each cycle (x in cycles, f the pixel in cycles); .5 once it aliases.
        float BkStripe(float x,float f){return saturate(0.5+(abs(frac(x)-0.5)-0.25)/max(f*2.0,0.0001));}
        // A running bond: courses `ch` high, units `bl` long, every other course offset by half; returns joint coverage and the unit id.
        float BkBond(float2 uv,float ch,float bl,float mortar,float2 f,out float2 id)
        {
            float row=floor(uv.y/ch);float x=uv.x+frac(row*0.5)*bl;id=float2(floor(x/bl),row);
            return max(BkJoint(x,bl,mortar,f.x),BkJoint(uv.y,ch,mortar,f.y));
        }
        // Cellular edge distance (F2 - F1, in cells): small along the cracks between cells.
        float BkCrack(float2 p)
        {
            float2 i=floor(p);float2 f=frac(p);float d1=8.0;float d2=8.0;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {
                float2 g=float2(float(x),float(y));float2 o=float2(BkHash2(i+g),BkHash2(i+g+float2(17.0,31.0)));float2 r=g+o-f;float d=dot(r,r);
                if(d<d1){d2=d1;d1=d;}else if(d<d2){d2=d;}
            }
            return sqrt(d2)-sqrt(d1);
        }
        // "DANGER" on the 2.3 tag: 3x5 glyphs of 2 cm cells, 8.5 cm apart, centred on the plate (tp in metres from its centre).
        float BkGlyphs(float2 tp,float f)
        {
            float x=tp.x+0.2425;float L=floor(x/0.085);float lx=x-L*0.085;if(L<0.0||L>5.0||lx>=0.06)return 0.0;
            float col=floor(lx/0.02);float row=floor((0.05-tp.y)/0.02);if(row<0.0||row>4.0)return 0.0;
            float g=L<0.5?27502.0:L<1.5?11245.0:L<2.5?27501.0:L<3.5?14699.0:L<4.5?31143.0:27565.0;
            uint bit=uint(14.0-(row*3.0+col)+0.5);
            return float((uint(g+0.5)>>bit)&1u)*saturate(1.0-f/0.01);
        }
        // The 2.3 band fitted to the block's height (NativeBlockLooks.BandFit, m above its base): x, y the band, z the tag
        // plate's centre (-1: no room). 1.0-1.6 m on blocks 1.9 m tall or more, else up to 0.6 m centred; the tag 8 cm above the
        // band, else below it, only where it clears a bevel by 2 cm.
        float3 BkBand(float height)
        {
            float c=min(0.05,0.21*height);
            float2 band=height>=1.9?float2(1.0,1.6):height*0.5+float2(-0.5,0.5)*min(0.6,0.8*(height-2.0*c));
            float above=band.y+0.2;float below=band.x-0.2;
            return float3(band,above+0.12<=height-c-0.02?above:(below-0.12>=c+0.02?below:-1.0));
        }
        // Cyber City's surfaces (FLAG_CYBER; docs/arenas/CYBERCITY_PROPOSAL.md §8): paving, precast panels, anodised metal,
        // floor grating, wet asphalt, curtain-wall glass, LED/holo screens and neon tubes, in rest space like the base looks.
        // `win` is the instance's light level: 1 lit, the 3.9 telegraph's 8 Hz strobe and black on sim blocks (glass panes and
        // screens die with their segment), driven by the kit and the hazard rigs elsewhere; above 1 it glows a grate's void.
        void BkCyber(float look,float2 uv,float2 fw,float fwm,bool top,float3 base,float seed,float edge,float detail,float win,inout float3 alb,inout float3 emit,inout float j)
        {
            if(BkIs(look,CY_TILE))
            {
                // 60 cm pavers in 6 mm joints, each its own shade; wet patches on top faces darken and keep the sheen.
                float2 tid=floor(uv/0.6);j=max(BkJoint(uv.x,0.6,0.006,fw.x),BkJoint(uv.y,0.6,0.006,fw.y));
                float3 t=base*lerp(1.0,lerp(0.86,1.10,BkHash2(tid+seed*11.0)),detail);
                float wet=top?smoothstep(0.55,0.75,BkFbm(uv*0.35+seed*4.0)):0.0;
                alb=lerp(t*(1.0-0.45*wet),base*0.42,j);emit=CY_SHEEN*wet*0.6;
            }
            else if(BkIs(look,CY_PANEL))
            {
                // Precast panels 1.2 x 1.8 m in 1.5 cm recessed seams, each its own shade, rain streaks under each panel head;
                // roofs and tops take a gritty membrane.
                if(top){alb=base*(0.9+0.12*BkNoise(uv*0.9+seed))*(1.0+0.10*(BkHash2(floor(uv*55.0))-0.5)*detail);}
                else
                {
                    float2 pid=floor(uv/float2(1.2,1.8));float pf=frac(uv.y/1.8);
                    j=max(BkJoint(uv.x,1.2,0.015,fw.x),BkJoint(uv.y,1.8,0.015,fw.y));
                    float tone=lerp(1.0,lerp(0.86,1.08,BkHash2(pid+seed*23.0)),detail);
                    float streak=smoothstep(0.45,0.85,BkNoise(float2(uv.x*5.0,uv.y*0.35+seed*3.0)))*pf*0.28*detail;
                    alb=base*tone*(1.0-streak)*(1.0-0.62*j);
                }
            }
            else if(BkIs(look,CY_METAL))
            {
                // Anodised panels 0.6 x 1.2 m: fine brushed grain, 8 mm seams, a bolt head near each corner, no rust.
                float seam=max(BkJoint(uv.x,0.6,0.008,fw.x),BkJoint(uv.y,1.2,0.008,fw.y));
                float tone=lerp(1.0,lerp(0.9,1.1,BkHash2(floor(uv/float2(0.6,1.2))+seed*5.0)),detail);
                float brush=1.0+0.07*(BkNoise(float2(uv.x*0.8,uv.y*90.0))-0.5)*detail;
                float2 bp=abs(frac(uv/float2(0.6,1.2))-0.5)*float2(0.6,1.2)-float2(0.26,0.56);
                float bolt=(1.0-smoothstep(0.008-fwm,0.008+fwm,length(bp)))*saturate(1.0-fwm/0.01);
                alb=base*tone*brush*(1.0-0.5*seam)*(1.0+0.7*bolt);j=seam;
            }
            else if(BkIs(look,CY_GRATE))
            {
                // Floor grating: 1.2 cm bearing bars every 4 cm (along the face's x), 8 mm rods every 10 cm, a solid 7 cm
                // frame, over a dark void; sides are plain plate. A light level above 1 lights the void (charge).
                float bar=BkJoint(uv.x,0.04,0.012,fw.x);float rod=BkJoint(uv.y,0.10,0.008,fw.y);
                float m=top?max(max(bar,rod),1.0-smoothstep(0.06,0.08,edge)):1.0;
                alb=lerp(base*0.07,base*(0.92+0.2*(BkHash2(floor(uv*25.0)+seed)-0.5)*detail),m);
                emit=float3(0.30,0.80,1.00)*max(0.0,win-1.0)*(1.0-m)*2.2;j=0.0;
            }
            else if(BkIs(look,CY_ROAD))
            {
                // Wet asphalt: aggregate speckle, patching, puddles where the surface dips (darker, with the sheen); the cut
                // faces of the slab read as rough concrete.
                float spk=(BkHash2(floor(uv*60.0)+seed*3.0)-0.5)*0.22*detail*saturate(1.0-fwm/0.016);float blot=(BkFbm(uv*0.25+seed*2.0)-0.5)*0.14;
                float wet=top?smoothstep(0.52,0.70,BkFbm(uv*0.18+seed*1.3)):0.0;
                alb=top?base*(1.0+spk+blot)*(1.0-0.55*wet):base*1.35*(1.0+blot+spk*0.5);emit=CY_SHEEN*wet;
            }
            else if(BkIs(look,CY_GLASS))
            {
                // Curtain wall: 1.5 m bays in 3.6 m storeys (a .8 m spandrel at each floor line, a transom 1.1 m up), dark panes
                // mirroring the violet sky (lighter toward each pane's top), a third lit in cool white, cyan, magenta or warm, some
                // behind blinds. Far away a bay fades to its window's average coverage so towers glow and never shimmer.
                if(top){alb=base*0.6;emit=float3(0.0,0.0,0.0);}
                else
                {
                    float2 bay=floor(uv/float2(1.5,3.6));float2 bf=frac(uv/float2(1.5,3.6));
                    float fade=smoothstep(0.12,0.34,max(fw.x/1.5,fw.y/3.6));
                    float mull=lerp(max(max(BkJoint(uv.x,1.5,0.07,fw.x),BkJoint(uv.y,3.6,0.06,fw.y)),BkJoint(uv.y-1.1,3.6,0.04,fw.y)),0.12,fade);
                    float spand=lerp(step(bf.y,0.22),0.22,fade);
                    float h=BkHash2(bay+seed*41.0);float floorLit=step(0.3,BkHash2(float2(bay.y,seed*7.0)));float lit=step(h,0.30)*floorLit*saturate(win);
                    float3 lc=(h<0.13?float3(0.75,0.85,1.00):(h<0.21?float3(1.00,0.72,0.45):(h<0.25?float3(0.10,0.80,1.00):(h<0.28?float3(1.00,0.18,0.70):float3(0.55,0.62,0.85)))))*0.62;
                    float blinds=step(0.55,BkHash2(bay*1.7+float2(3.0,3.0)))*lerp(BkStripe(uv.y/0.09,fw.y/0.09),0.5,fade);
                    float3 pane=base*(0.45+0.55*bf.y);
                    alb=lerp(pane,base*1.8,max(mull,spand));
                    emit=(lc*lit*(0.7+0.5*BkHash2(bay*3.1))*(1.0-0.55*blinds)+CY_SKYREF*(0.4+0.8*bf.y)*(1.0-lit))*(1.0-max(mull,spand));
                }
            }
            else if(BkIs(look,CY_HOLO))
            {
                // An LED/holo screen: emissive in the block's colour with a secondary hue band, 1.5 cm scanlines and a 3 cm
                // pixel grid (fading to their average with distance) inside a dark 6 cm bezel.
                float bezel=1.0-smoothstep(0.05,0.07,edge);
                float scan=lerp(lerp(0.70,1.0,BkStripe(uv.y/0.015,fw.y/0.015)),0.85,saturate(fw.y/0.015));
                float pix=lerp(1.0-0.35*max(BkJoint(uv.x,0.03,0.006,fw.x),BkJoint(uv.y,0.03,0.006,fw.y)),0.9,saturate(fwm/0.03));
                float band=smoothstep(0.35,0.75,BkNoise(float2(uv.x*0.22+seed*9.0,uv.y*0.55)));
                float3 c=lerp(base,base.gbr,band*0.65);
                alb=lerp(base*0.06,BK_FRAME,bezel);emit=top?float3(0.0,0.0,0.0):c*1.5*scan*pix*saturate(win)*(1.0-bezel);
            }
            else if(BkIs(look,CY_TUBE))
            {
                // A neon tube or LED strip: emissive in the block's colour, a little brighter along the core of each face.
                alb=base*0.15;emit=base*saturate(win)*(0.85+0.3*smoothstep(0.0,0.03,edge));
            }
            else if(BkIs(look,WZ_BURN))
            {
                // Burner grate: the floor grating's bars and rods over the burner trough; a light level above 1 heats it
                // (the trough glows orange, the bars go dull red, then yellow-white in the burn).
                float bar=BkJoint(uv.x,0.04,0.012,fw.x);float rod=BkJoint(uv.y,0.10,0.008,fw.y);
                float m=top?max(max(bar,rod),1.0-smoothstep(0.06,0.08,edge)):1.0;float heat=max(0.0,win-1.0);
                alb=lerp(base*0.06,base*(0.92+0.2*(BkHash2(floor(uv*25.0)+seed)-0.5)*detail),m);
                emit=lerp(float3(1.0,0.32,0.06),float3(1.0,0.78,0.42),saturate(heat-0.6))*heat*((1.0-m)*2.4+m*0.35*saturate(heat-0.3));j=0.0;
            }
            else if(BkIs(look,WZ_TILE))
            {
                // Ward Zero clinical tile: 30 cm tiles in 4 mm joints, each its own pale shade; scuffs on floors, broad stains
                // (old disinfectant, worse in the corners), the joints darkened with grime.
                float2 tid=floor(uv/0.3);j=max(BkJoint(uv.x,0.3,0.004,fw.x),BkJoint(uv.y,0.3,0.004,fw.y));
                float tone=lerp(1.0,lerp(0.9,1.06,BkHash2(tid+seed*13.0)),detail);
                float scuff=top?smoothstep(0.62,0.80,BkFbm(uv*0.6+seed*3.0))*0.16*detail:0.0;
                float stain=smoothstep(0.55,0.85,BkFbm(uv*0.12+seed*5.0))*0.24;
                alb=lerp(base*tone*(1.0-scuff-stain),base*0.42,j);
            }
            else if(BkIs(look,WZ_PANEL))
            {
                // Ward Zero wall panels 1.2 x 2.4 m in 1 cm seams, each its own shade, a rivet near each corner, faint vertical
                // streaks; tops are a dusty plate.
                if(top){alb=base*(0.85+0.1*BkNoise(uv*0.8+seed));}
                else
                {
                    float2 pid=floor(uv/float2(1.2,2.4));j=max(BkJoint(uv.x,1.2,0.01,fw.x),BkJoint(uv.y,2.4,0.01,fw.y));
                    float tone=lerp(1.0,lerp(0.9,1.05,BkHash2(pid+seed*19.0)),detail);
                    float streak=smoothstep(0.5,0.9,BkNoise(float2(uv.x*7.0,uv.y*0.3+seed*2.0)))*0.12*detail;
                    float2 rp=abs(frac(uv/float2(1.2,2.4))-0.5)*float2(1.2,2.4)-float2(0.55,1.15);
                    float rivet=(1.0-smoothstep(0.007-fwm,0.007+fwm,length(rp)))*saturate(1.0-fwm/0.01);
                    alb=base*tone*(1.0-streak)*(1.0-0.55*j)*(1.0-0.35*rivet);
                }
            }
            else if(BkIs(look,WZ_GLASS))
            {
                // A specimen tank: lit fluid behind glass (the block's colour), darker toward the bottom, a bright meniscus 12%
                // under the top edge, bubble trails, a cool sheen up one side; the light level lights the fluid, 0 drains it.
                float lit=saturate(win);
                if(top){alb=base*0.25;emit=base*lit*0.35;}
                else
                {
                    float f=frac(uv.y/3.0);float men=smoothstep(0.84,0.86,f)*(1.0-smoothstep(0.88,0.9,f));
                    float trail=step(0.86,BkHash2(float2(floor(uv.x/0.07),seed*9.0)))*step(0.55,BkNoise(float2(uv.x*14.0,uv.y*6.0+seed*4.0)))*detail;
                    float sheen=smoothstep(0.75,0.95,frac(uv.x/1.6+seed));
                    alb=base*0.12+float3(0.02,0.03,0.035)*sheen;
                    emit=base*lit*(0.18+0.42*f*step(f,0.86)+1.6*men+0.55*trail)+float3(0.03,0.05,0.06)*sheen;
                }
            }
            else if(BkIs(look,WZ_LIGHT))
            {
                // A fluorescent troffer: an opal diffuser over two tubes along the face's u, a 15 cm louvre grid, emissive by
                // the light level (the kit's flicker and the ward's mood drive it); dark housing at the rim.
                float rim=1.0-smoothstep(0.03,0.05,edge);
                float tube=max(1.0-smoothstep(0.0,0.06,abs(frac(uv.y/0.3)-0.25)*0.3),1.0-smoothstep(0.0,0.06,abs(frac(uv.y/0.3)-0.75)*0.3));
                float louvre=lerp(max(BkJoint(uv.x,0.15,0.01,fw.x),BkJoint(uv.y,0.15,0.01,fw.y)),0.12,saturate(fwm/0.05));
                alb=lerp(base*0.6,BK_FRAME,rim);emit=base*saturate(win)*(0.55+0.45*tube)*(1.0-0.45*louvre)*(1.0-rim);
            }
            else if(BkIs(look,WZ_INK))
            {
                // The Siren's ink: glossy black-violet with a creeping vein web; the veins glow by the light level.
                float web=1.0-abs(BkFbm(uv*0.9+seed*7.0)*2.0-1.0);float vein=smoothstep(0.90,0.985,web)*detail;
                float pool=smoothstep(0.4,0.8,BkFbm(uv*0.3+seed*3.0));
                alb=base*(0.5+0.5*pool);emit=float3(0.55,0.16,1.0)*vein*saturate(win)*1.4;j=0.0;
            }
            else
            {
                alb=base*(1.0+0.06*(BkNoise(uv*3.0+seed*5.0)-0.5)*detail);
            }
        }
        struct BlockSurf {float3 albedo;float3 emit;};
        // The look in rest space (pat), then the shared layers: band, tag and lamp paint, vertex shade, fresh breaks, rebar,
        // cracks, edge wear, the grime ramp, top-face dust and soot, the dust coat (light on the band, tag and lamp). fwP:
        // fwidth(pat), taken before any branch. health: NativeBlockLooks.Normalised (the block's thresholds at 66/33%). dust: the
        // block's local coat. win: its window light (NativeStructureFx.WindowStrobe through NativeArenaDressing; 1 elsewhere).
        BlockSurf BlockSurface(float3 obj,float3 pat,float3 fwP,float3 nO,float3 hs,float code,float health,float3 base,float seed,float4 shade,float dust,float win)
        {
            float flv=floor(code/16.0+0.001);float look=code-16.0*flv;uint fl=uint(flv+0.5);
            float3 ax=abs(nO);bool top=ax.y>=max(ax.x,ax.z);bool sideX=!top&&ax.x>=ax.z;
            float2 uv=top?pat.xz:(sideX?pat.zy:pat.xy);float2 fw=top?fwP.xz:(sideX?fwP.zy:fwP.xy);float fwm=max(fw.x,fw.y);
            // across: the side face's horizontal coordinate, increasing to the viewer's right (Unity is left-handed), so the tag reads.
            float across=sideX?obj.z*sign(nO.x):-obj.x*sign(nO.z);float halfAcross=sideX?hs.z:hs.x;float yb=obj.y+hs.y;
            float edge=top?min(hs.x-abs(obj.x),hs.z-abs(obj.z)):(sideX?min(hs.y-abs(obj.y),hs.z-abs(obj.z)):min(hs.x-abs(obj.x),hs.y-abs(obj.y)));
            float detail=saturate(1.0-fwm/0.06);
            float3 alb=base;float3 emit=float3(0.0,0.0,0.0);float2 id=float2(0.0,0.0);float j=0.0;
            bool cyber=(fl&FLAG_CYBER)!=0u;
            if(cyber)BkCyber(look,uv,fw,fwm,top,base,seed,edge,detail,win,alb,emit,j);
            else
            if(BkIs(look,LOOK_BRICK_RED)||BkIs(look,LOOK_BRICK_BUFF))
            {
                // Running bond, 12 cm courses of 36 cm bricks in 1.6 cm mortar; some bricks from the dark course.
                j=BkBond(uv,0.12,0.36,0.016,fw,id);float thr=BkIs(look,LOOK_BRICK_BUFF)?0.95:0.88;
                float dark=step(thr,BkHash2(id+float2(7.3,1.1)));float mean=0.98*(1.0-(1.0-BK_DARK_COURSE)*(1.0-thr));
                float3 brick=base*lerp(mean,lerp(0.84,1.12,BkHash2(id+seed*61.0))*lerp(1.0,BK_DARK_COURSE,dark),detail);
                brick*=1.0+0.05*(BkNoise(uv*float2(9.0,22.0))-0.5)*detail;
                alb=lerp(brick,BK_MORTAR*0.9,j);
            }
            else if(BkIs(look,LOOK_TERRACOTTA))
            {
                // Ashlar blocks 60 x 30 cm in thin joints, each a little lighter at its top (glaze).
                j=BkBond(uv,0.30,0.60,0.008,fw,id);float3 blk=base*lerp(1.0,lerp(0.94,1.05,BkHash2(id+seed*13.0)),detail);
                blk*=1.0+0.05*(frac(uv.y/0.30)-0.5)*detail;alb=lerp(blk,base*0.78,j);
            }
            else if(BkIs(look,LOOK_CONCRETE))
            {
                // Board-formed: 15 cm boards with seams and grain, tie holes on a 60 x 45 cm grid, rain stains.
                float side=top?0.0:1.0;float board=floor(uv.y/0.15);float jb=BkJoint(uv.y,0.15,0.006,fw.y)*side;
                float v=lerp(1.0,lerp(0.92,1.06,BkHash2(float2(board,seed*29.0))),detail*side);
                float grain=(BkNoise(float2(uv.x*1.3,uv.y*28.0))-0.5)*0.08*detail*side;
                float2 tie=(frac(uv/float2(0.6,0.45)+0.5)-0.5)*float2(0.6,0.45);float th=(1.0-smoothstep(0.012-fwm,0.012+fwm,length(tie)))*detail*side;
                float stain=smoothstep(0.55,0.8,BkNoise(float2(uv.x*3.0,uv.y*0.35+seed*7.0)))*0.12;
                alb=base*(v+grain-stain)*(1.0-0.3*jb)*(1.0-0.55*th)*(1.0+0.08*(BkHash2(floor(uv*70.0))-0.5)*detail*(1.0-side));
            }
            else if(BkIs(look,LOOK_STEEL))
            {
                // Painted plates 1.2 x 2.4 m, rivets 4.5 cm either side of each seam every 10 cm, rust at edges and low down.
                float seam=max(BkJoint(uv.x,1.2,0.012,fw.x),BkJoint(uv.y,2.4,0.012,fw.y));
                float du=abs(frac(uv.x/1.2+0.5)-0.5)*1.2;float dv=abs(frac(uv.y/2.4+0.5)-0.5)*2.4;
                float ry=(frac(uv.y/0.1+0.5)-0.5)*0.1;float rx=(frac(uv.x/0.1+0.5)-0.5)*0.1;
                float rd=min(length(float2(du-0.045,ry)),length(float2(rx,dv-0.045)));
                float riv=(1.0-smoothstep(0.011-fwm,0.011+fwm,rd))*saturate(1.0-fwm/0.012);float dome=lerp(1.25,0.65,smoothstep(0.004,0.011,rd));
                float rust=smoothstep(0.62,0.8,BkFbm(uv*1.7+seed*9.0)+0.18*(1.0-saturate(yb/1.2))+0.15*(1.0-saturate(edge/0.12)));
                rust=max(rust,0.5*smoothstep(0.7,0.9,BkNoise(float2(uv.x*7.0,uv.y*0.6))));
                alb=lerp(base,BK_RUST,rust)*(1.0-0.45*seam)*lerp(1.0,dome,riv);
            }
            else if(BkIs(look,LOOK_TIMBER))
            {
                // 15 cm planks along the face (along the longer axis on top), grain, nails every 90 cm.
                bool turn=top&&hs.z>hs.x;float2 tv=turn?uv.yx:uv;float2 tf=turn?fw.yx:fw;
                float plank=floor(tv.y/0.15);float jt=BkJoint(tv.y,0.15,0.008,tf.y);
                float3 wood=base*lerp(1.0,lerp(0.82,1.12,BkHash2(float2(plank,seed*17.0))),detail);
                wood*=1.0+0.12*(BkNoise(float2(tv.x*2.2+plank*7.1,tv.y*55.0))-0.5)*detail;
                float2 nail=float2((frac(tv.x/0.9+0.5)-0.5)*0.9,(frac(tv.y/0.15)-0.5)*0.15);
                float nl=(1.0-smoothstep(0.006-fwm,0.006+fwm,length(nail)))*saturate(1.0-fwm/0.008);
                alb=wood*(1.0-0.55*jt)*(1.0-0.6*nl);
            }
            else if(BkIs(look,LOOK_TAR))
            {
                // Roll seams every 90 cm, patches, gravel grit.
                float seam=BkJoint(uv.y,0.9,0.03,fw.y);float blot=(BkFbm(uv*0.6+seed*5.0)-0.5)*0.2;
                float grit=lerp(0.07,step(0.93,BkHash2(floor(uv*80.0))),detail*saturate(1.0-fwm/0.0125));
                alb=base*(1.0+blot)*(1.0+0.25*seam)*(1.0+0.6*grit);
            }
            else if(BkIs(look,LOOK_ASPHALT))
            {
                // Aggregate speckle, patching and a few fine cracks.
                float spk=(BkHash2(floor(uv*60.0)+seed*3.0)-0.5)*0.28*detail*saturate(1.0-fwm/0.016);float blot=(BkFbm(uv*0.25+seed*2.0)-0.5)*0.16;
                float crack=(1.0-smoothstep(0.02,0.045,BkCrack(uv*0.7)))*smoothstep(0.55,0.7,BkNoise(uv*0.3+float2(11.0,11.0)))*detail;
                alb=base*(1.0+spk+blot)*(1.0-0.25*crack);
            }
            else if(BkIs(look,LOOK_COBBLE))
            {
                // Setts 22 x 13 cm in dark joints, each its own shade, rounded toward the joints.
                j=BkBond(uv,0.13,0.22,0.014,fw,id);float3 stone=base*lerp(1.0,lerp(0.82,1.16,BkHash2(id+seed*7.0)),detail);
                stone*=lerp(float3(1.0,1.0,1.0),float3(1.04,1.0,0.95),BkHash2(id*3.7)*detail);
                float row=floor(uv.y/0.13);float x=uv.x+frac(row*0.5)*0.22;float ex=min(frac(x/0.22),1.0-frac(x/0.22))*0.22;float ey=min(frac(uv.y/0.13),1.0-frac(uv.y/0.13))*0.13;
                stone*=lerp(1.0,0.8+0.2*smoothstep(0.0,0.035,min(ex,ey)),detail);alb=lerp(stone,base*0.45,j);
            }
            else if(BkIs(look,LOOK_WINDOW))
            {
                // Panes 50 x 70 cm in dark frames; about 35% lit, some behind a curtain; the telegraph's window light strobes them
                // (1 or .12 at 8 Hz) and then puts them out (0) until the segment lands.
                float2 pane=floor(uv/float2(0.5,0.7));float2 pv=frac(uv/float2(0.5,0.7));
                float fr=max(BkJoint(uv.x,0.5,0.045,fw.x),BkJoint(uv.y,0.7,0.045,fw.y));
                float lit=step(BkHash2(pane+seed*37.0),0.35)*win;
                float curtain=step(0.5,BkHash2(pane*1.9+float2(3.0,3.0)))*step(0.62,pv.y);
                alb=lerp(lerp(base*(0.55+0.45*pv.y),BK_LIT*0.2,lit),BK_FRAME,fr);emit=BK_LIT*(0.7+0.3*BkHash2(pane*3.1))*(1.0-0.55*curtain)*lit*(1.0-fr);
                if(top){alb=BK_FRAME;emit=float3(0.0,0.0,0.0);}
            }
            else if(BkIs(look,LOOK_NEON))
            {
                // A dark housing with a softly lit panel and a bright tube outlined 9 cm inside its edges.
                float housing=1.0-smoothstep(0.02,0.05,edge);float tube=1.0-smoothstep(0.012,0.03+fwm,abs(edge-0.09));
                alb=lerp(base*0.12,BK_FRAME,housing);emit=base*(0.28*(1.0-housing)+1.3*tube);
            }
            else if(BkIs(look,LOOK_CORRUGATED))
            {
                // 76 mm corrugations (vertical on the sides, along the longer axis on top) and rust streaks.
                bool turn=top&&hs.x>hs.z;float2 cv=turn?uv.yx:uv;float cf=turn?fw.y:fw.x;
                float rust=smoothstep(0.62,0.85,BkNoise(float2(cv.x*6.0,cv.y*0.5+seed*4.0)));
                alb=lerp(base,BK_RUST,0.6*rust)*(1.0+0.16*cos(cv.x*6.2831853/0.076)*saturate(1.0-cf/0.03));
            }
            else if(BkIs(look,LOOK_DEMOLITION))
            {
                // Yellow and black stripes over the whole block (the 2.3 colours; the DANGER tag only with FLAG_TAG).
                alb=lerp(BK_YELLOW,BK_BLACK,BkStripe((uv.x+uv.y)/0.35,fwm/0.35));
            }
            else if(BkIs(look,LOOK_FACTORY_GRATING))
            {
                // Pressed steel grating (the Factory's decks, treads and cages; FACTORY_PROPOSAL.md 3.10): on top faces 3.4 cm
                // bearing bars along the longer axis and cross rods every 10 cm over a dark void (filtered to their mean far
                // away), a brighter worn line down the middle; on the sides of thin blocks the flat-bar edge, on taller ones a
                // 5 cm welded mesh in a 4 cm frame; galvanised mottling throughout.
                float3 voidc=base*0.2;
                if(top)
                {
                    bool turn=hs.z>hs.x;float2 gv=turn?uv.yx:uv;float2 gf=turn?fw.yx:fw;
                    float bar=BkJoint(gv.y,0.034,0.016,gf.y);float rod=BkJoint(gv.x,0.10,0.007,gf.x);float m=max(bar,rod);
                    float wear=1.0-smoothstep(0.2,0.65,abs(turn?obj.x:obj.z)/max(turn?hs.x:hs.z,0.01));
                    alb=lerp(voidc,base*(0.88+0.24*BkNoise(gv*float2(0.6,3.0)+seed*7.0))*(1.0+0.14*wear*detail),m);
                }
                else if(hs.y>0.3)
                {
                    float wire=max(BkJoint(uv.x,0.05,0.005,fw.x),BkJoint(uv.y,0.05,0.005,fw.y));
                    alb=lerp(voidc*0.8,base*(0.9+0.15*BkNoise(uv*2.0+seed)),wire);alb=lerp(alb,base,1.0-smoothstep(0.03,0.05+fwm,edge));
                }
                else alb=base*(0.95+0.1*BkNoise(uv*float2(4.0,1.0)+seed*3.0));
            }
            else
            {
                alb=base*(1.0+0.06*(BkNoise(uv*3.0+seed*5.0)-0.5)*detail);
            }
            // The 2.3 load-bearing band: chevrons between black rules where BkBand fits them, the DANGER tag and the band lamp,
            // each only with its own flag (never on a stub, whose member is gone).
            float inBand=0.0;float plate=0.0;float lamp=0.0;
            if((fl&(FLAG_BAND|FLAG_TAG))!=0u&&!top&&health>0.0)
            {
                float3 bt=BkBand(2.0*hs.y);
                if((fl&FLAG_BAND)!=0u)
                {
                    inBand=smoothstep(bt.x-fw.y,bt.x+fw.y,yb)*(1.0-smoothstep(bt.y-fw.y,bt.y+fw.y,yb));
                    float rule=max(1.0-smoothstep(0.0,0.015+fw.y,abs(yb-bt.x)),1.0-smoothstep(0.0,0.015+fw.y,abs(yb-bt.y)));
                    alb=lerp(alb,lerp(BK_YELLOW,BK_BLACK,max(BkStripe((across+abs(yb-0.5*(bt.x+bt.y)))/0.3,fwm/0.3),rule)),inBand);emit*=1.0-inBand;
                }
                if((fl&FLAG_TAG)!=0u&&bt.z>0.0&&halfAcross>0.3)
                {
                    float2 tp=float2(across,yb-bt.z);
                    plate=(1.0-smoothstep(0.28-fwm,0.28+fwm,abs(tp.x)))*(1.0-smoothstep(0.12-fwm,0.12+fwm,abs(tp.y)));
                    alb=lerp(alb,lerp(BK_DANGER,BK_INK,BkGlyphs(tp,fwm)),plate);emit*=1.0-plate;
                }
                // The lamp at the band's middle (NativeBlockLooks.Lamp: amber while locked, red on the 4 Hz strobe at S2 or under a
                // stress telegraph, else dark glass): a dark housing ring round a lens that glows when lit.
                if((fl&FLAG_BAND)!=0u)
                {
                    float ld=length(float2(across,yb-0.5*(bt.x+bt.y)));
                    lamp=1.0-smoothstep(0.075-fwm,0.075+fwm,ld);float lens=1.0-smoothstep(0.055-fwm,0.055+fwm,ld);
                    float3 lc=(fl&FLAG_LAMP_RED)!=0u?BK_DANGER:((fl&FLAG_LAMP_AMBER)!=0u?BK_LAMP_AMBER:float3(0.0,0.0,0.0));
                    alb=lerp(lerp(alb,BK_FRAME,lamp),lerp(float3(0.03,0.03,0.035),lc*0.35,step(0.0001,lc.r)),lens);emit=lerp(emit*(1.0-lamp),lc*1.5,lens);
                }
            }
            // Vertex shade (NativeBlockLooks: bevels +18%, corners -22%, stored x .8), then fresh breaks on chips, stubs and fragments.
            alb*=shade.rgb*1.25;
            float masonry=look<4.5?1.0:0.0;
            float3 fresh=masonry>0.5?(look>0.5&&look<3.5?BK_FRESH_BRICK:BK_FRESH_CONCRETE):(BkIs(look,LOOK_TIMBER)?base*1.5:float3(0.42,0.42,0.43));
            float fm=smoothstep(0.35,0.75,shade.a+0.3*(BkNoise(uv*7.0)-0.5));if((fl&FLAG_FRAGMENT)!=0u)fm=max(fm,0.25);
            alb=lerp(alb,fresh*(0.85+0.3*BkHash2(floor(uv*40.0))),fm);
            // S2 and below: rust-coloured rebar, 30 cm apart, in the deep fresh breaks of masonry (chips and stub tops).
            if(health<=0.33&&masonry>0.5)
            {
                float rod=1.0-smoothstep(0.006,0.012+fwm,abs(frac(across/0.3+seed)-0.5)*0.3);
                alb=lerp(alb,BK_RUST,rod*fm*step(0.7,shade.a));
            }
            // Cracks from S1 (health 66%), spreading and widening to 0, in masonry (warped cell edges about 60 cm across) and
            // timber (splits along the grain); S2 adds spalled patches.
            if(health<=0.66&&(masonry>0.5||BkIs(look,LOOK_TIMBER)))
            {
                float dmg=saturate((0.66-health)/0.66);float width=0.012+0.02*dmg;
                float2 cp=(BkIs(look,LOOK_TIMBER)?uv*float2(0.5,5.0):uv*1.6)+0.35*float2(BkNoise(uv*3.1),BkNoise(uv*3.1+float2(7.0,7.0)));
                float crack=(1.0-smoothstep(width,width+0.02+fwm*1.6,BkCrack(cp+seed*13.0)))*step(BkNoise(uv*0.7+seed*3.0),0.12+0.75*dmg);
                alb*=1.0-0.5*crack;
                if(health<=0.33){float sp=smoothstep(0.66,0.8,BkFbm(uv*3.0+seed*5.0));alb=lerp(alb,fresh*0.8,sp*0.5)*(1.0-0.15*sp);}
            }
            // Edge wear: chipped paint and arrises lighter within 7 cm of a face's edge.
            float wear=(1.0-smoothstep(0.0,0.07,edge))*step(0.5,BkNoise(uv*9.0+seed*2.0))*detail;
            alb=lerp(alb,alb*1.22+float3(0.012,0.012,0.012),wear*0.6);
            if(!top)
            {
                float g=1.0-smoothstep(1.2,1.8,yb);float splash=1.0-smoothstep(0.0,0.3,yb);
                alb*=1.0-(BK_GRIME+0.04*(BkNoise(uv*float2(2.0,6.0))-0.5))*g-0.06*splash;
            }
            if(nO.y>0.7&&!cyber)
            {
                alb=lerp(alb,BK_DUST,0.08*smoothstep(0.35,0.75,BkFbm(uv*0.35+seed*3.0)));
                alb=lerp(alb,BK_SOOT,0.06*smoothstep(0.6,0.8,BkNoise(uv*0.25+float2(19.0,19.0))));
            }
            // The round's one dust coat (Core NativeDustCoat): the map's base (MapLooks blocks.dust), .6 x the map-wide level of
            // this round's landings (_ArenaDustCoat) and this block's own (breaks and nearby landings), heavier on top faces; the
            // band, the tag and the lamp keep a tenth of it (NativeBlockLooks.BandCoatKeep), so a pier's telegraph stays clean.
            float coat=saturate(saturate(_ArenaDust+0.6*_ArenaDustCoat+dust)*(nO.y>0.7?1.0:0.55)*(0.75+0.5*BkNoise(uv*1.3)));
            coat*=1.0-0.9*max(max(inBand,plate),lamp);
            BlockSurf s;s.albedo=lerp(alb,BK_COAT,coat);s.emit=emit*(1.0-0.5*coat);return s;
        }
        // The rim light, MapLooks' practical lights ((1 - (d/r)^2)^2, NativeMapLooks.Attenuation) and the blast flashes
        // (min(1, 5(1 - q^2)) / (1 + 25 q^2), NativeBlockLooks.FlashFalloff), each times max(0, N.L).
        float3 BlockLights(float3 world,float3 n)
        {
            float3 sum=_ArenaRimColor.rgb*saturate(dot(n,_ArenaRimDir.xyz));
            for(int k=0;k<8;k++)
            {
                float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);
                sum+=_ArenaLightColor[k].rgb*(a*a*saturate(dot(n,d/len)));
            }
            for(int m=0;m<3;m++)
            {
                float3 d=_ArenaFlashPos[m].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaFlashPos[m].w,0.0001);q*=q;
                sum+=_ArenaFlashColor[m].rgb*(saturate(5.0*(1.0-q))/(1.0+25.0*q)*saturate(dot(n,d/len)));
            }
            return sum;
        }
        // Ground haze (3.10: 15% from 0 to 2.5 m): _ArenaHaze.w at the street, gone at _ArenaHazeTop, faded in from 4 to 40 m away.
        float3 BlockHaze(float3 col,float3 world,float dist)
        {
            float t=saturate(world.y/max(_ArenaHazeTop,0.01));
            return lerp(col,_ArenaHaze.rgb,_ArenaHaze.w*(1.0-t)*(1.0-t)*smoothstep(4.0,40.0,dist));
        }
        // SHARED END
        void BlockVertex(inout appdata_block v)
        {
            float4 size=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockSize);float flags=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockLook).w;
            float3 n=v.normal;v.vertex=float4(BlockLattice(v.texcoord.xyz,v.texcoord1.xyz,size.xyz,size.w,flags,n),1.0);v.normal=n;
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #include "AutoLight.cginc"
            #include "UnityLightingCommon.cginc"
            struct v2f {float4 pos:SV_POSITION;float4 world:TEXCOORD0;float4 obj:TEXCOORD1;float4 pat:TEXCOORD2;float4 size:TEXCOORD3;float4 col:TEXCOORD4;float4 shade:TEXCOORD5;float3 nO:TEXCOORD6;float3 nW:TEXCOORD7;UNITY_SHADOW_COORDS(8) UNITY_FOG_COORDS(9)};
            v2f vert(appdata_block v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);
                float4 size=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockSize);float4 rest=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockRest);float4 col=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockColor);float4 look=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockLook);
                BlockVertex(v);o.pos=UnityObjectToClipPos(v.vertex);o.world=float4(mul(unity_ObjectToWorld,v.vertex).xyz,col.w);
                o.obj=float4(v.vertex.xyz,rest.w);o.pat=float4(v.vertex.xyz+rest.xyz,look.x+16.0*look.w);o.size=float4(size.xyz,look.y);o.col=float4(col.rgb,look.z);o.shade=v.color;
                o.nO=v.normal;o.nW=UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_SHADOW(o,float2(0.0,0.0));UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 fwP=fwidth(i.pat.xyz);
                float3 nO=BlockOrient(cross(ddx(i.obj.xyz),ddy(i.obj.xyz)),i.nO);float3 nW=BlockOrient(cross(ddx(i.world.xyz),ddy(i.world.xyz)),i.nW);
                BlockSurf s=BlockSurface(i.obj.xyz,i.pat.xyz,fwP,nO,i.size.xyz,i.pat.w,i.size.w,i.col.rgb,i.col.w,i.shade,i.obj.w,i.world.w);
                UNITY_LIGHT_ATTENUATION(atten,i,i.world.xyz);
                float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*(saturate(dot(nW,_WorldSpaceLightPos0.xyz))*atten)+BlockLights(i.world.xyz,nW);
                fixed4 c=fixed4(BlockHaze(s.albedo*light+s.emit,i.world.xyz,distance(i.world.xyz,_WorldSpaceCameraPos)),1.0);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            struct v2fs {V2F_SHADOW_CASTER;};
            v2fs vertShadow(appdata_block v){v2fs o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2fs,o);BlockVertex(v);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)return o;}
            float4 fragShadow(v2fs i):SV_Target{SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    Fallback Off
}
