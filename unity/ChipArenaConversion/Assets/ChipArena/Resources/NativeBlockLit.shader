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
    // (_ArenaLight*, its own arrays, never the entrance stage's _StageLight*), the 3 blast flashes (_ArenaFlash*,
    // NativeArenaDressing.Flashes) and the guns' 4 muzzle lights (_MuzzleLight*, NativeMuzzleLight.cginc); then the ground
    // haze and fog. On Ward Zero only (NativeWardLight.cginc): the ceiling light
    // field (one 3D texture, the only texture read here) and the practical slots' beacon beams.
    // BLOCK_PACK (a local keyword; Runtime/NativePackStage's materials, docs/arenas/STAGE-KITS.md): a pack bank prop drawn in a
    // block's place or round the map. Its own mesh and place (no lattice), the pack's atlas (_PackTex, uv0) times _PackColor as
    // the albedo, the same texel times _PackEmit and the map's _ArenaPackGlow as emission (the lights atlas: windows and lamps),
    // a cutout below _PackCutoff (the logos), and every light, toon, haze, fog, muzzle, flash and dust term a block takes
    // (PackSurface). Per instance: _BlockRest y the prop's base height (its grime ramp), w its local dust coat; _BlockLook y the
    // block's health share, z its seed, w its flags; _BlockColor rgb a tint. Normals go through the instance's inverse transpose
    // (a fitted prop is scaled per axis).
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
        // The anime style's taper (Core NativeMapStyle.Taper): with the style on, the part narrows toward its top by TOON_TAPER.
        #define FLAG_TAPER 256u
        // The anime style's toothed ends (NativeMapStyle.ToothEnds: a big brick wall's free end along its longer axis, + and -).
        #define FLAG_TOOTH_POS 512u
        #define FLAG_TOOTH_NEG 1024u
        // The anime style's kit shapes (each an opt-in a kit part carries; Core NativeMapStyle.Frame and its neighbours): a
        // window pane painted inside a frame with depth, a cornice with a moulded underside, a pediment slab raked to the
        // pediment's slope, an awning strip arched like a canvas barrel.
        #define FLAG_FRAME 2048u
        #define FLAG_CORNICE 4096u
        #define FLAG_RAKE 8192u
        #define FLAG_AWNING 16384u
        #define TOON_RAKE_SLOPE 0.33
        #define TOON_AWNING_ARCH 0.12
        #define TOON_FRAME 0.07
        // The style's paint extras (Core MapStyleState.Floor; 0 none): x the albedo floor, tinted by the shade tint; y the share
        // of a brick face drawn in courses (the rest in stage 1's patches).
        float4 _ArenaToonFloor;
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
        // Ward Zero's ceiling light field and the slots' beacon beams (off on every other map: its globals are 0).
        #include "NativeWardLight.cginc"
        // The guns' muzzle lights (four slots of their own on every map; off while _MuzzleLightCount is 0).
        #include "NativeMuzzleLight.cginc"
        // The anime map style's switch, ramp and helpers (_ArenaToon*; 0 everywhere but a map whose look turns it on).
        #include "NativeToon.cginc"
        // The vertex lattice (NativeBlockLooks.Corner) and the instance's 180-degree mesh turns, which only pick other chipped corners.
        float3 BlockLattice(float3 a,float3 b,float3 h,float c,float flags,inout float3 n)
        {
            float3 p=a*h+sign(b)*min(abs(b)*c,0.42*abs(a)*h);
            uint fl=uint(flags+0.5);
            if((fl&FLAG_SPIN_Y)!=0u){p.xz=-p.xz;n.xz=-n.xz;}
            if((fl&FLAG_SPIN_Z)!=0u){p.xy=-p.xy;n.xy=-n.xy;}
            return p;
        }
        // The anime style's shapes (Core NativeMapStyle.Chamfer and Shape): a rod (its two thinner half extents both at most
        // TOON_ROD and within TOON_ROD_SQUARE of each other, its longest TOON_ROD_LONG times the thicker) rounds its section at
        // the lattice's clamp; any other block up to TOON_MID_HALF thick grows its chamfer with its thinnest half extent
        // (TOON_CHAMFER_GROW per metre, at most TOON_CHAMFER_MAX); bigger blocks keep theirs.
        #define TOON_ROD 0.15
        #define TOON_ROD_SQUARE 0.6
        #define TOON_ROD_LONG 3.0
        #define TOON_MID_HALF 1.0
        #define TOON_CHAMFER_GROW 0.16
        #define TOON_CHAMFER_MAX 0.08
        #define TOON_TAPER 0.3
        float ToonChamfer(float3 h,float c)
        {
            float lo=min(h.x,min(h.y,h.z));float hi=max(h.x,max(h.y,h.z));float mid=h.x+h.y+h.z-lo-hi;
            if(mid<=TOON_ROD&&lo>=TOON_ROD_SQUARE*mid&&hi>=TOON_ROD_LONG*mid)return 0.42*lo;
            if(lo<=TOON_MID_HALF)return clamp(TOON_CHAMFER_GROW*lo,c,TOON_CHAMFER_MAX);
            return c;
        }
        // The kit shapes on a placed point (the wrap and the style's lattice): a pediment slab's ends raked in toward its top
        // along its longer axis; an awning strip arched up toward its middle along its longer axis.
        float3 ToonKitShape(float3 p,float3 h,uint fl)
        {
            bool alongX=h.x>=h.z;float hl=alongX?h.x:h.z;float t=alongX?p.x:p.z;
            if((fl&FLAG_RAKE)!=0u){float r=saturate((p.y+h.y)/max(2.0*h.y,0.0001))*2.0*h.y/TOON_RAKE_SLOPE;t*=max(hl-r,0.0)/max(hl,0.0001);}
            if((fl&FLAG_AWNING)!=0u){float u=t/max(hl,0.0001);p.y+=min(TOON_AWNING_ARCH,0.25*hl)*(1.0-u*u);}
            if(alongX)p.x=t;else p.z=t;
            return p;
        }
        // The lattice with the style's chamfer and taper (the shader's vertex stage and the shadow caster); off: BlockLattice as is.
        float3 ToonLattice(float3 a,float3 b,float3 h,float c,float flags,inout float3 n)
        {
            if(_ArenaToon.x<0.5)return BlockLattice(a,b,h,c,flags,n);
            float3 p=BlockLattice(a,b,h,ToonChamfer(h,c),flags,n);
            if((uint(flags+0.5)&FLAG_TAPER)!=0u)p.xz*=1.0-TOON_TAPER*saturate(p.y/max(h.y,0.0001)*0.5+0.5);
            return ToonKitShape(p,h,uint(flags+0.5));
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
        // The anime style's wrap (Core NativeToonMesh.Wrap; NativeMapStyle.WrapClass, WrapShape and WrapPoint are its CPU copy):
        // a lathe of a rounded section (a quarter circle at each corner, a post line either side of it) through eight rows, which
        // this places for the block's class. A wall (masonry at least .6 m tall, 20 cm thick and 60 cm long) gets a plinth at its
        // foot under a sloped weathering, a coping band at its top over a sloped drip, a crisp bevel on every edge and, on some
        // blocks of old masonry, one broken top corner; a column (a masonry pier at least 1.2 m tall, no more than 1.6 m across
        // and roughly square) a base and a capital and a round section; a crate (timber, up to 2.4 m tall and 3 m long) corner
        // posts proud of its planks with a bottom rail and a top rim; a container (corrugated, at least .8 m tall and .6 m thick)
        // posts and rails. Every other block is a box rounded on all its edges. Never above or below the block; the bands and
        // posts stand at most WRAP_OUT m proud of its faces (presentation only: the collider is the block). _ArenaToonWrap turns
        // each feature on per map (x plinths, bases and big walls' pilasters, y copings and capitals, z broken corners, w crate
        // and container posts).
        // Vertex data: uv0 (A.x, row, A.z, 1), uv1 (B.x, zone, B.z, slide): zone 2 marks the corner points (posts, chips), the
        // slide moves a post line in from the corner (1 along x, 2 along z, its sign the direction).
        #define WRAP_NONE 0.0
        #define WRAP_WALL 1.0
        #define WRAP_COLUMN 2.0
        #define WRAP_CRATE 3.0
        #define WRAP_CONTAINER 4.0
        #define WRAP_OUT 0.11
        #define WRAP_BEVEL 0.07
        #define WRAP_CHIP 0.22
        #define WRAP_CHIP_SHARE 0.3
        #define WRAP_PILASTER 0.35
        #define WRAP_PILASTER_OUT 0.05
        // The damaged wraps (stages S1-S3, NativeToonMesh.WrapMesh): a break's largest drop and inset (m), the stub's ragged top,
        // the S2 waist; the toothed end's bands (count, course height cap, recess cap).
        #define WRAP_DMG_DROP 0.35
        #define WRAP_DMG_IN 0.1
        #define WRAP_STUB_RAG 0.12
        #define WRAP_WAIST 0.08
        #define WRAP_TEETH 12.0
        #define WRAP_TOOTH_COURSE 0.36
        #define WRAP_TOOTH_DEPTH 0.18
        #define WRAP_TOOTH_SEAM 0.003
        float ToonWrapClass(float3 h,float look,float flags)
        {
            if((uint(flags+0.5)&FLAG_CYBER)!=0u)return WRAP_NONE;
            float lo=min(h.x,h.z);float hi=max(h.x,h.z);bool masonry=look>0.5&&look<4.5||BkIs(look,LOOK_COBBLE);
            if(masonry&&h.y>=0.6&&lo>=0.08&&lo>=0.5*hi&&hi<=0.8&&h.y>=1.8*hi)return WRAP_COLUMN;
            if(masonry&&h.y>=0.3&&lo>=0.1&&hi>=0.3)return WRAP_WALL;
            if(BkIs(look,LOOK_TIMBER)&&h.y>=0.2&&h.y<=1.2&&lo>=0.2&&hi<=1.5)return WRAP_CRATE;
            if(BkIs(look,LOOK_CORRUGATED)&&h.y>=0.4&&lo>=0.3)return WRAP_CONTAINER;
            return WRAP_NONE;
        }
        // The class's shape (m): the section's corner radius rs, the plinth's height pb and outset op, the coping's height ct
        // and outset oc, the top's and the bottom's rounding rt and rb, the post line's slide d and the posts' outset po, and
        // the broken corner (0 none, else 1 + the corner: x+ 1, z+ 2).
        void ToonWrapShape(float cls,float3 h,float c,float seed,float flags,out float rs,out float pb,out float op,out float ct,out float oc,out float rt,out float rb,out float d,out float po,out float chip)
        {
            float H=2.0*h.y;float lo=min(h.x,h.z);float hi=max(h.x,h.z);float4 w=_ArenaToonWrap;
            rs=c;pb=0.0;op=0.0;ct=0.0;oc=0.0;d=0.0;po=0.0;chip=0.0;rt=min(c,0.42*min(lo,h.y));
            if(cls>0.5&&cls<1.5)
            {
                rs=max(c,min(WRAP_BEVEL,0.4*lo));rt=min(WRAP_BEVEL,0.42*min(lo,h.y));
                pb=min(0.32,0.09*H)*w.x;op=min(0.035,0.25*lo)*w.x;ct=min(0.26,0.07*H)*w.y;oc=min(0.05,0.3*lo)*w.y;
                chip=w.z*step(0.7,frac(seed*7.31))*(1.0+floor(frac(seed*13.7)*3.999));d=chip>0.5?WRAP_CHIP:0.0;
                // A big wall (at least 4 m long and 4 m tall) stands pilasters at its ends (with the plinths: the masonry's frame).
                if(hi>=2.0&&h.y>=2.0&&w.x>0.5){d=WRAP_PILASTER;po=WRAP_PILASTER_OUT;}
            }
            else if(cls>1.5&&cls<2.5)
            {
                rs=max(c,0.25*lo);rt=min(0.04,0.42*min(lo,h.y));
                pb=min(0.3,0.12*H)*w.x;op=min(0.07,0.25*lo)*w.x;ct=min(0.28,0.12*H)*w.y;oc=min(0.08,0.3*lo)*w.y;
            }
            else if(cls>2.5&&cls<3.5){pb=0.1*w.w;op=0.015*w.w;ct=0.1*w.w;oc=0.015*w.w;d=min(0.11,0.3*lo)*w.w;po=0.032*w.w;}
            else if(cls>3.5){pb=0.12*w.w;op=0.018*w.w;ct=0.12*w.w;oc=0.018*w.w;d=min(0.12,0.3*lo)*w.w;po=0.035*w.w;}
            rb=pb>0.0?0.0:rt;
            // A cornice: a deep round under it (a moulding), a crisp top. An awning strip: its post lines spread for the arch.
            uint kf=uint(flags+0.5);if((kf&FLAG_CORNICE)!=0u){rb=min(0.6*h.y,0.6*lo);rt=min(rt,0.02);}if((kf&FLAG_AWNING)!=0u)d=hi;
            pb=min(pb,0.25*H);ct=min(max(ct,rt+oc),0.3*H);
        }
        // uv0.w: 1 + the stage the mesh draws (1 intact, 2 S1, 3 S2, 4 the S3 stub). dmg: the damaged meshes' per-vertex break
        // (the vertex colour: r its drop, g its inset, a a fresh break), 0 on the intact wrap. Rows 8 and 9 are the S2 waist,
        // rows from 10 the toothed end's band edges (two rows each, below and above). n: the orientation hint, turned with the
        // instance's 180-degree spin (FLAG_SPIN_Y only: the wrap never turns over).
        float3 ToonWrap(float4 a,float4 b,float3 h,float c,float look,float flags,float seed,float4 dmg,inout float3 n)
        {
            float rs,pb,op,ct,oc,rt,rb,d,po,chip;ToonWrapShape(ToonWrapClass(h,look,flags),h,c,seed,flags,rs,pb,op,ct,oc,rt,rb,d,po,chip);
            float stage=a.w-1.0;float lo=min(h.x,h.z);uint fl=uint(flags+0.5);
            // A damaged wrap: the break's slide for every class and no seeded corner; the stub loses its coping.
            if(stage>0.5){d=max(d,min(WRAP_CHIP,WRAP_CHIP_SHARE*lo));chip=0.0;}
            if(stage>2.5){oc=0.0;ct=rt;}
            float row=a.y;float3 p;
            p.x=a.x*h.x+sign(b.x)*min(abs(b.x)*rs,0.42*abs(a.x)*h.x);p.z=a.z*h.z+sign(b.z)*min(abs(b.z)*rs,0.42*abs(a.z)*h.z);
            if(abs(b.w)>0.5){bool alongX=abs(b.w)<1.5;float sl=sign(b.w)*min(d,WRAP_CHIP_SHARE*(alongX?h.x:h.z));if(alongX)p.x+=sl;else p.z+=sl;}
            // The point's outward direction in the section (a face's normal, or the corner circle's).
            float2 sn=float2(sign(a.x)*(1.0-abs(b.x)),sign(a.z)*(1.0-abs(b.z)));
            float y;float o;
            if(row>9.5)
            {
                // A toothed end: band edges down from the body's top, odd bands recessed at a free end of the longer axis.
                float t=row-10.0;float j=floor(t*0.5)+1.0;float bh=min(WRAP_TOOTH_COURSE,(2.0*h.y-pb-ct)/(WRAP_TEETH+1.0));
                // The two rows of an edge sit WRAP_TOOTH_SEAM apart, so the faces between them never meet in a T (no pinholes).
                float side=t-2.0*(j-1.0);y=h.y-ct-j*bh+(side<0.5?-WRAP_TOOTH_SEAM:WRAP_TOOTH_SEAM);o=0.0;float band=side<0.5?j:j-1.0;
                bool alongX=h.x>=h.z;float end=alongX?sign(a.x):sign(a.z);
                if(band<WRAP_TEETH-0.5&&frac(band*0.5)>0.25&&(end>0.0&&(fl&FLAG_TOOTH_POS)!=0u||end<0.0&&(fl&FLAG_TOOTH_NEG)!=0u))
                {float td=min(WRAP_TOOTH_DEPTH,0.25*(alongX?h.x:h.z));if(alongX)p.x-=end*td;else p.z-=end*td;}
            }
            else if(row>8.5){y=h.y/3.0;o=-min(WRAP_WAIST,0.3*lo);}
            else if(row>7.5){y=-h.y/3.0;o=-min(WRAP_WAIST,0.3*lo);}
            else if(row<0.5){y=-h.y;o=pb>0.0?op:-rb;}
            else if(row<1.5){y=pb>0.0?-h.y+pb-min(op,0.5*pb):-h.y+0.2929*rb;o=pb>0.0?op:-0.2929*rb;}
            else if(row<2.5){y=-h.y+max(pb,rb);o=0.0;}
            else if(row<3.5){y=h.y-ct;o=0.0;}
            else if(row<4.5){y=h.y-ct+min(oc,0.5*(ct-rt));o=oc;}
            else if(row<5.5){y=h.y-rt;o=oc;}
            else if(row<6.5){y=h.y-0.2929*rt;o=oc-0.2929*rt;}
            else{y=h.y;o=oc-rt;}
            if(b.y>1.5)o+=po;
            // The broken corner: its corner points above the coping's foot drop and step in (the top fan slopes into it).
            if(chip>0.5&&row>3.5&&row<7.5&&b.y>1.5&&abs((a.x>0.0?1.0:0.0)+(a.z>0.0?2.0:0.0)-(chip-1.0))<0.5)
            {y=max(y-min(WRAP_CHIP,ct+0.1)*(row-3.0)*0.25,h.y-ct+0.002*(row-3.0));o=min(o,0.0)-0.04;}
            // A damaged mesh's break: its corners drop and step in (S1, S2), the stub's top is ragged (S3).
            if(stage>0.5){y-=dmg.r*(stage>2.5?min(WRAP_STUB_RAG,0.6*h.y):min(WRAP_DMG_DROP,0.6*h.y));o-=dmg.g*min(WRAP_DMG_IN,0.35*lo);}
            p.xz+=sn*o;p.y=y;
            p=ToonKitShape(p,h,fl);
            if((fl&FLAG_SPIN_Y)!=0u){p.xz=-p.xz;n.xz=-n.xz;}
            if((fl&FLAG_TAPER)!=0u)p.xz*=1.0-TOON_TAPER*saturate(p.y/max(h.y,0.0001)*0.5+0.5);
            return p;
        }

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
        // The anime style's extras (0 in the legacy look, which never reads them): occ its occlusion band (0..1), bump a turn of
        // the normal in the block's frame (corrugation ribs, plank edges).
        struct BlockSurf {float3 albedo;float3 emit;float occ;float3 bump;};
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
            // The anime style keeps only part of each look's fine variation (per-brick tones, grain, speckle, edge wear).
            bool toon=_ArenaToon.x>0.5;if(toon)detail*=_ArenaToonPaint.x;
            float3 alb=base;float3 emit=float3(0.0,0.0,0.0);float2 id=float2(0.0,0.0);float j=0.0;float occLook=0.0;float3 bump=float3(0.0,0.0,0.0);
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
                // The anime style suggests the brickwork the way a painted background does: bricks drawn in patches and along the
                // face's edges and foot, flat paint between; broad joints in a dark tone of the brick, a few bricks lit. A look's
                // brickCourses (_ArenaToonFloor.y, stage 2) draws the courses over that share of the whole face (the furnace walls).
                if(toon)
                {
                    float2 tid;float tj=BkBond(uv,0.12,0.36,0.024,fw,tid);
                    float show=max(max(smoothstep(0.4,0.48,BkFbm(uv*0.42+seed*9.0)),1.0-smoothstep(0.3,0.75,min(edge,yb))),_ArenaToonFloor.y)*(1.0-smoothstep(0.02,0.045,fwm));
                    float3 tone=base*mean;float3 face=tone*(1.0+0.16*step(0.82,BkHash2(tid+seed*13.0))*show);
                    alb=lerp(face,tone*0.5,tj*show);
                }
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
                // 15 cm planks along the face (along the longer axis on top), grain, nails every 90 cm (the anime style paints
                // fewer, broader boards: 22 cm).
                bool turn=top&&hs.z>hs.x;float2 tv=turn?uv.yx:uv;float2 tf=turn?fw.yx:fw;float pitch=toon?0.22:0.15;
                float plank=floor(tv.y/pitch);float jt=BkJoint(tv.y,pitch,toon?0.012:0.008,tf.y);
                float3 wood=base*lerp(1.0,lerp(0.82,1.12,BkHash2(float2(plank,seed*17.0))),detail);
                wood*=1.0+0.12*(BkNoise(float2(tv.x*2.2+plank*7.1,tv.y*55.0))-0.5)*detail;
                float2 nail=float2((frac(tv.x/0.9+0.5)-0.5)*0.9,(frac(tv.y/pitch)-0.5)*pitch);
                float nl=(1.0-smoothstep(0.006-fwm,0.006+fwm,length(nail)))*saturate(1.0-fwm/0.008);
                alb=wood*(1.0-0.55*jt)*(1.0-0.6*nl);
                // The anime style: each plank's edges turn over (a lit top edge and a dark under edge in the ramp).
                if(toon)
                {
                    float pj=frac(tv.y/pitch);float tilt=(smoothstep(0.78,1.0,pj)-(1.0-smoothstep(0.0,0.22,pj)))*0.65*saturate(1.0-tf.y/0.02);
                    bump=(top?(turn?float3(1.0,0.0,0.0):float3(0.0,0.0,1.0)):float3(0.0,1.0,0.0))*tilt;
                    // A crate's side: a diagonal brace board between its posts and rails, lighter, with a shade under it.
                    if(!top&&ToonWrapClass(hs,look,flv)>2.5&&ToonWrapClass(hs,look,flv)<3.5)
                    {
                        float ha=halfAcross-0.13;float hb=2.0*hs.y-0.26;float bx=across+ha;float by=yb-0.13;
                        float side=bx*hb-by*2.0*ha;float dd=abs(side)/sqrt(hb*hb+4.0*ha*ha+0.0001);
                        float inside=step(abs(across),ha)*step(0.0,by)*step(by,hb);
                        float brace=(1.0-smoothstep(0.065-fwm,0.065+fwm,dd))*inside*(1.0-smoothstep(0.02,0.05,fwm));
                        alb=lerp(alb,base*lerp(1.0,1.14,_ArenaToonWrap.w),brace*_ArenaToonWrap.w);
                        occLook=(1.0-smoothstep(0.085,0.1+fwm,dd))*(1.0-brace)*inside*step(0.0,side)*_ArenaToonWrap.w*(1.0-smoothstep(0.02,0.05,fwm));
                    }
                }
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
                // The anime style: the frame's depth, its head's shadow on the pane below it.
                else if(toon)occLook=smoothstep(0.84,0.88,pv.y)*(1.0-fr)*(1.0-smoothstep(0.015,0.04,fwm));
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
                // The anime style: the ribs turn the normal (sin, the slope of the cos profile), so the ramp draws them as bands.
                if(toon)bump=(top?(turn?float3(0.0,0.0,1.0):float3(1.0,0.0,0.0)):(sideX?float3(0.0,0.0,1.0):float3(1.0,0.0,0.0)))*(0.6*sin(cv.x*6.2831853/0.076)*(1.0-smoothstep(0.008,0.018,cf)));
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
            // The anime style's paint under the telegraphs: walls lighter toward the sky over the first 18 m (the rest frame's height,
            // so stacked blocks run on), and a broad soft wash in place of fine noise.
            if(toon)
            {
                // The material looks (terracotta to cobble, corrugated; brick paints its own) lean toward their flat base colour: the pattern stays legible
                // but reads as painted, not as noise (screens, glass, neon, the bank's looks and the hazard paint keep theirs).
                // Past a few centimetres a pixel every pattern resolves to that flat paint (no moire on courses, boards or ribs).
                // (Brick paints its own suggested courses above and keeps them.)
                if(!cyber&&look>2.5&&(look<9.5||BkIs(look,LOOK_CORRUGATED)))alb=lerp(base*0.85,alb,(0.4+0.6*_ArenaToonPaint.x)*(1.0-smoothstep(0.02,0.06,fwm)));
                // Cyber City's glass curtain walls, holo screens and tubes keep their own painting (they only lose fine noise).
                bool painted=cyber&&(BkIs(look,CY_GLASS)||BkIs(look,CY_HOLO)||BkIs(look,CY_TUBE));
                if(!top&&!painted)alb*=lerp(1.0-_ArenaToonPaint.y,1.0+0.5*_ArenaToonPaint.y,smoothstep(0.0,18.0,pat.y));
                if(!painted)alb*=1.0+_ArenaToonPaint.z*(BkFbm(uv*float2(0.22,0.6)+seed*3.0)-0.5);
                // A floor under near-black paint (a night map's dark vans and booths hold their form), cool like the shade.
                alb=max(alb,_ArenaToonFloor.x*_ArenaToonShade.rgb/max(max(_ArenaToonShade.r,_ArenaToonShade.g),max(_ArenaToonShade.b,0.0001)));
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
            // The anime style drops the baked bevel and corner shade (its rounded normal lights the edges instead).
            if(toon)shade.rgb=float3(0.8,0.8,0.8);
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
            if(!top&&toon)alb*=1.0-_ArenaToonPaint.w*(1.0-smoothstep(0.0,1.6,yb));
            else if(!top)
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
            BlockSurf s;s.albedo=lerp(alb,BK_COAT,coat);s.emit=emit*(1.0-0.5*coat);s.occ=0.0;
            // The anime style's occlusion band: a crisp band up a wall's foot (_ArenaToonFx.w m) and every downward face (the
            // underside of a coping, a ledge, a balcony).
            s.bump=bump;
            // The anime style: a kit window pane inside a frame with depth: a dark frame round the pane, its head's shade on the
            // pane below it (the pane reads set back); emission only inside the frame. Faded to the plain pane by footprint.
            if(toon&&(fl&FLAG_FRAME)!=0u&&!top)
            {
                float fk=1.0-smoothstep(0.02,0.05,fwm);float frm=(1.0-smoothstep(TOON_FRAME-fwm,TOON_FRAME+fwm,edge))*fk;
                s.albedo=lerp(s.albedo,base*0.18+float3(0.02,0.018,0.016),frm);s.emit*=1.0-frm;
                occLook=max(occLook,(1.0-smoothstep(0.0,0.1+fwm,hs.y-obj.y-TOON_FRAME))*(1.0-frm)*fk);
            }
            if(toon)s.occ=max(max(top?0.0:1.0-smoothstep(_ArenaToonFx.w-0.04,_ArenaToonFx.w+0.04,yb),step(0.5,-nO.y)),occLook);

            return s;
        }
        // A pack prop's surface (BLOCK_PACK; Core NativePackDressing): tex the atlas texel times the material colour (linear), glow
        // its emission; the layers a block takes that read on a painted prop: the anime style's albedo floor, soot from S1 (health
        // as NativeBlockLooks.Normalised gives it; past the rule's kept stage the block draws as its box again), the grime ramp up
        // from the prop's base (baseY), top-face dust, the round's dust coat and the style's occlusion band at the foot.
        float _ArenaPackGlow;
        BlockSurf PackSurface(float3 tex,float3 glow,float3 world,float3 n,float baseY,float health,float dust,float seed)
        {
            bool toon=_ArenaToon.x>0.5;float3 alb=tex;
            if(toon)alb=max(alb,_ArenaToonFloor.x*_ArenaToonShade.rgb/max(max(_ArenaToonShade.r,_ArenaToonShade.g),max(_ArenaToonShade.b,0.0001)));
            bool top=n.y>0.7;float2 uv=top?world.xz:(abs(n.x)>abs(n.z)?world.zy:world.xy);float yb=world.y-baseY;
            if(health<0.66)
            {
                float dmg=saturate((0.66-health)/0.33);float soot=smoothstep(0.35,0.75,BkFbm(uv*1.7+seed*7.0))*dmg;
                alb=lerp(alb,BK_SOOT,0.6*soot)*(1.0-0.15*dmg);
            }
            if(!top&&toon)alb*=1.0-_ArenaToonPaint.w*(1.0-smoothstep(0.0,1.6,yb));
            else if(!top)alb*=1.0-BK_GRIME*0.6*(1.0-smoothstep(1.2,1.8,yb));
            if(top)alb=lerp(alb,BK_DUST,0.08*smoothstep(0.35,0.75,BkFbm(uv*0.35+seed*3.0)));
            float coat=saturate(saturate(_ArenaDust+0.6*_ArenaDustCoat+dust)*(top?1.0:0.55)*(0.75+0.5*BkNoise(uv*1.3)));
            BlockSurf s;s.albedo=lerp(alb,BK_COAT,coat);s.emit=glow*(1.0-0.5*coat);s.bump=float3(0.0,0.0,0.0);s.occ=0.0;
            if(toon)s.occ=max(top?0.0:1.0-smoothstep(_ArenaToonFx.w-0.04,_ArenaToonFx.w+0.04,yb),step(0.5,-n.y));
            return s;
        }
        // The rim light, MapLooks' practical lights ((1 - (d/r)^2)^2, NativeMapLooks.Attenuation; a beacon slot's beam,
        // NativeWardLight.cginc WardBeam) and the blast flashes (min(1, 5(1 - q^2)) / (1 + 25 q^2), NativeBlockLooks.FlashFalloff),
        // each times max(0, N.L); then Ward Zero's ceiling light field (WardField; nothing elsewhere) and the guns' muzzle lights
        // (NativeMuzzleLight.cginc MuzzleLights, unwrapped).
        float3 BlockLights(float3 world,float3 n)
        {
            float3 sum=_ArenaRimColor.rgb*saturate(dot(n,_ArenaRimDir.xyz))+WardField(world,n);
            for(int k=0;k<8;k++)
            {
                float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);
                sum+=_ArenaLightColor[k].rgb*(a*a*saturate(dot(n,d/len)))*WardBeam(d,_ArenaLightColor[k].w);
            }
            for(int m=0;m<3;m++)
            {
                float3 d=_ArenaFlashPos[m].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaFlashPos[m].w,0.0001);q*=q;
                sum+=_ArenaFlashColor[m].rgb*(saturate(5.0*(1.0-q))/(1.0+25.0*q)*saturate(dot(n,d/len)));
            }
            sum+=MuzzleLights(world,n,0.0);
            return sum;
        }
        // The anime style's light for a block (NativeToon.cginc): the rounded normal nT takes the key through the ramp, the
        // flattened ambient and the point lights in stairs (the practical slots, flashes, muzzle lights and the ward field, all
        // of BlockLights); the facet nW says how far the edge has turned for the edge light. v: toward the eye.
        float3 ToonBlockLit(float3 albedo,float3 emit,float occ,float3 world,float3 nT,float3 nW,float3 v,float3 keyDir,float3 key,float atten,float3 shN,float3 shUp,float3 shDown)
        {
            float edge=saturate((1.0-dot(nT,nW))*TOON_EDGE_GAIN);
            // The shade side's sky bounce: the ambient a little lighter up a wall than at its foot (a painted gradient in shade).
            float bounce=1.0+_ArenaToonPaint.y*(saturate(world.y/12.0)-0.35)*1.5;
            float3 light=ToonOcclude(ToonDiffuse(ToonAmbient(shN,shUp,shDown,nT.y)*bounce,key,dot(nT,keyDir),atten),occ)+ToonPoints(BlockLights(world,nT));
            return albedo*(light+ToonEdge(nT,v,keyDir,key,atten,edge))+ToonSheen(nT,v,shUp)+emit;
        }
        // Ground haze (3.10: 15% from 0 to 2.5 m): _ArenaHaze.w at the street, gone at _ArenaHazeTop, faded in from 4 to 40 m away.
        float3 BlockHaze(float3 col,float3 world,float dist)
        {
            float t=saturate(world.y/max(_ArenaHazeTop,0.01));
            return lerp(col,_ArenaHaze.rgb,_ArenaHaze.w*(1.0-t)*(1.0-t)*smoothstep(4.0,40.0,dist));
        }
        // SHARED END
        sampler2D _PackTex;float4 _PackColor;float _PackEmit;float _PackCutoff;
        // A pack prop's normal in the world: the instance's inverse transpose (the cofactors of its 3x3, normalised).
        float3 PackNormal(float3 n)
        {
            float3 a=unity_ObjectToWorld[0].xyz,b=unity_ObjectToWorld[1].xyz,c=unity_ObjectToWorld[2].xyz;
            return normalize(float3(dot(cross(b,c),n),dot(cross(c,a),n),dot(cross(a,b),n)));
        }
        void BlockVertex(inout appdata_block v)
        {
            // A pack prop keeps its own vertices (BLOCK_PACK).
            #if !defined(BLOCK_PACK)
            float4 size=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockSize);float flags=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockLook).w;
            float3 n=v.normal;
            // The anime style's wrap mesh (uv0.w 1; only drawn while the style is on) or every other stage mesh.
            if(v.texcoord.w>0.5){float4 look=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockLook);v.vertex=float4(ToonWrap(v.texcoord,v.texcoord1,size.xyz,ToonChamfer(size.xyz,size.w),look.x,look.w,look.z,v.color,n),1.0);}
            else v.vertex=float4(ToonLattice(v.texcoord.xyz,v.texcoord1.xyz,size.xyz,size.w,flags,n),1.0);
            v.normal=n;
            #endif
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            // The environment's mark for the anime style's outline pass (Resources/NativeToonEdge.shader tests bit 16).
            Stencil { Ref 16 WriteMask 16 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_local __ BLOCK_PACK
            #pragma instancing_options assumeuniformscaling
            #include "AutoLight.cginc"
            #include "UnityLightingCommon.cginc"
            struct v2f {float4 pos:SV_POSITION;float4 world:TEXCOORD0;float4 obj:TEXCOORD1;float4 pat:TEXCOORD2;float4 size:TEXCOORD3;float4 col:TEXCOORD4;float4 shade:TEXCOORD5;float3 nO:TEXCOORD6;float3 nW:TEXCOORD7;UNITY_SHADOW_COORDS(8) UNITY_FOG_COORDS(9) float4 ax:TEXCOORD10;float4 ay:TEXCOORD11;};
            v2f vert(appdata_block v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);
                float4 size=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockSize);float4 rest=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockRest);float4 col=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockColor);float4 look=UNITY_ACCESS_INSTANCED_PROP(BlockProps,_BlockLook);
                #if defined(BLOCK_PACK)
                // A pack prop: uv0 in pat.xy, its base height in shade.x, the world normal through the inverse transpose.
                o.pos=UnityObjectToClipPos(v.vertex);o.world=float4(mul(unity_ObjectToWorld,v.vertex).xyz,col.w);o.obj=float4(v.vertex.xyz,rest.w);
                o.pat=float4(v.texcoord.xy,0.0,look.w);o.size=float4(0.0,0.0,0.0,look.y);o.col=float4(col.rgb,look.z);o.shade=float4(rest.y,0.0,0.0,0.0);
                o.nO=v.normal;o.nW=PackNormal(v.normal);
                #else
                BlockVertex(v);o.pos=UnityObjectToClipPos(v.vertex);o.world=float4(mul(unity_ObjectToWorld,v.vertex).xyz,col.w);
                o.obj=float4(v.vertex.xyz,rest.w);o.pat=float4(v.vertex.xyz+rest.xyz,look.x+16.0*look.w);o.size=float4(size.xyz,look.y);o.col=float4(col.rgb,look.z);o.shade=v.color;
                o.nO=v.normal;o.nW=UnityObjectToWorldNormal(v.normal);
                // The anime style: the instance's x and y axes in the world (its rounded normal's frame) and the chamfer drawn.
                o.ax=float4(UnityObjectToWorldDir(float3(1.0,0.0,0.0)),_ArenaToon.x>0.5?ToonChamfer(size.xyz,size.w):size.w);o.ay=float4(UnityObjectToWorldDir(float3(0.0,1.0,0.0)),0.0);
                #endif
                UNITY_TRANSFER_SHADOW(o,float2(0.0,0.0));UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                #if defined(BLOCK_PACK)
                {
                    float4 t=tex2D(_PackTex,i.pat.xy);clip(t.a-_PackCutoff);float3 alb=t.rgb*_PackColor.rgb*i.col.rgb;
                    float3 nT=normalize(i.nW);float3 nF=BlockOrient(cross(ddx(i.world.xyz),ddy(i.world.xyz)),nT);
                    BlockSurf ps=PackSurface(alb,t.rgb*(_PackEmit*_ArenaPackGlow),i.world.xyz,nT,i.shade.x,i.size.w,i.obj.w,i.col.w);
                    UNITY_LIGHT_ATTENUATION(pa,i,i.world.xyz);float3 pc;
                    if(_ArenaToon.x>0.5)pc=ToonBlockLit(ps.albedo,ps.emit,ps.occ,i.world.xyz,nT,nF,normalize(_WorldSpaceCameraPos-i.world.xyz),_WorldSpaceLightPos0.xyz,_LightColor0.rgb,pa,ShadeSH9(float4(nT,1.0)),ShadeSH9(float4(0.0,1.0,0.0,1.0)),ShadeSH9(float4(0.0,-1.0,0.0,1.0)));
                    else pc=ps.albedo*(ShadeSH9(float4(nT,1.0))+_LightColor0.rgb*(saturate(dot(nT,_WorldSpaceLightPos0.xyz))*pa)+BlockLights(i.world.xyz,nT))+ps.emit;
                    fixed4 pk=fixed4(BlockHaze(pc,i.world.xyz,distance(i.world.xyz,_WorldSpaceCameraPos)),1.0);
                    UNITY_APPLY_FOG(i.fogCoord,pk);return pk;
                }
                #else
                float3 fwP=fwidth(i.pat.xyz);float fpx=length(fwidth(i.world.xyz));
                float3 nO=BlockOrient(cross(ddx(i.obj.xyz),ddy(i.obj.xyz)),i.nO);float3 nW=BlockOrient(cross(ddx(i.world.xyz),ddy(i.world.xyz)),i.nW);
                BlockSurf s=BlockSurface(i.obj.xyz,i.pat.xyz,fwP,nO,i.size.xyz,i.pat.w,i.size.w,i.col.rgb,i.col.w,i.shade,i.obj.w,i.world.w);
                UNITY_LIGHT_ATTENUATION(atten,i,i.world.xyz);
                float3 col;
                // The anime style (NativeToon.cginc; Core NativeMapStyle): the rounded normal in the instance's frame, then the ramp.
                if(_ArenaToon.x>0.5)
                {
                    // The rounded normal only where its radius spans a few pixels (a sub-pixel edge would shimmer): the facet beyond.
                    float rad=ToonRadius(i.size.xyz,i.ax.w);float keep=smoothstep(1.0,3.0,rad/max(fpx,0.00001));
                    float3 nR=normalize(lerp(nO,ToonRoundNormal(i.obj.xyz,i.size.xyz,rad,nO),keep)+s.bump);
                    float3 nT=normalize(i.ax.xyz*nR.x+i.ay.xyz*nR.y+cross(i.ax.xyz,i.ay.xyz)*nR.z);
                    col=ToonBlockLit(s.albedo,s.emit,s.occ,i.world.xyz,nT,nW,normalize(_WorldSpaceCameraPos-i.world.xyz),_WorldSpaceLightPos0.xyz,_LightColor0.rgb,atten,ShadeSH9(float4(nT,1.0)),ShadeSH9(float4(0.0,1.0,0.0,1.0)),ShadeSH9(float4(0.0,-1.0,0.0,1.0)));
                }
                else
                {
                    float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*(saturate(dot(nW,_WorldSpaceLightPos0.xyz))*atten)+BlockLights(i.world.xyz,nW);
                    col=s.albedo*light+s.emit;
                }
                fixed4 c=fixed4(BlockHaze(col,i.world.xyz,distance(i.world.xyz,_WorldSpaceCameraPos)),1.0);
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
                #endif
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
            #pragma multi_compile_local __ BLOCK_PACK
            #pragma instancing_options assumeuniformscaling
            // A pack prop's cutout (the logos) holds in its shadow and depth too.
            struct v2fs {V2F_SHADOW_CASTER;float2 uv:TEXCOORD1;};
            v2fs vertShadow(appdata_block v){v2fs o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2fs,o);BlockVertex(v);o.uv=v.texcoord.xy;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)return o;}
            float4 fragShadow(v2fs i):SV_Target
            {
                #if defined(BLOCK_PACK)
                clip(tex2D(_PackTex,i.uv).a-_PackCutoff);
                #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback Off
}
