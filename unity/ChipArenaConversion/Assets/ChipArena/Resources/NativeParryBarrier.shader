// The parry's hexagon barrier (owner 2026-11-09: "pressing F; Lets add more of an effect rather that just turning character a color. Make it
// high quality, animated, and visible. Maybe like hexagon barriers?"): the dome of hex cells built in Core/NativeParryBarrier.cs (uv: x 0 at a
// cell's middle to 1 at its rim, y the cell's seed; uv2 the cell's middle on the unit sphere), drawn round the pilot while the parry's window is
// open (Runtime NativeParryBarrierPresentation.cs). Every cell animates on its own: it snaps in with an overshoot, the cells facing the pilot's
// front first and round to the back (_Facing); a parried blow (_Hit: its way from the middle, w its age) lights the cells round it and sends a
// ring of pushed-out, brightened cells across the dome; when the window shuts (_Closed) the cells burst outward, shrinking and fading, each a
// moment apart.
// A hologram (owner 2026-11-09: "It's currently too much glow and we can't actually see the player in there or the shape of hexagons ... We
// want a more holographic design"): each cell is a crisp, thin outline with a fainter inner line, over a face that is almost clear, so the
// pilot shows through it and every hexagon reads on its own; the dome brightens only toward its silhouette (a fresnel), where the light grazes
// it. Fine scanlines drift up it, a slow interference band climbs it, its tint shimmers a little with the view angle and the line edges split
// into colour, cells flicker and now and then glitch or drop out, and it glows faintly where it cuts into the floor or anything else (the
// camera's depth, _Soft 1; none on the low graphics tier). It stays under the bloom's threshold except at its silhouette, a blow and its
// snap-in. Additive, no depth write, both faces (the inside fainter, lines only), scene fog toward black. Colour the pilot's accent (linear).
Shader "ChipArena/NativeParryBarrier"
{
    Properties
    {
        _BarrierColor("Colour (linear, HDR)",Color)=(.3,.9,1,1)
        _Age("Seconds since the window opened",Float)=1
        _Closed("Seconds since it shut (negative: open)",Float)=-1
        _Facing("The pilot's front (unit)",Vector)=(0,0,1,0)
        _Hit("A parried blow's way from the middle (unit), w its age (negative: none)",Vector)=(0,0,1,-1)
        _Strength("Strength",Float)=1
        _Soft("Glow where it meets the scene (1: the camera has a depth texture)",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+3" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.5
            #include "UnityCG.cginc"
            float4 _BarrierColor,_Facing,_Hit;float _Age,_Closed,_Strength,_Soft;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            struct appdata{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float3 middle:TEXCOORD1;};
            struct v2f{float4 pos:SV_POSITION;float3 wpos:TEXCOORD0;float3 wn:TEXCOORD1;float4 cell:TEXCOORD2;float2 lift:TEXCOORD3;float4 screen:TEXCOORD4;UNITY_FOG_COORDS(5)};
            // The cells' moments (Core NativeParryBarrier: Sweep .13, Scatter .035, Snap .1, Burst .2, BurstScatter .06).
            float snapIn(float x){if(x<=0)return 0;if(x>=1)return 1;float y=x-1;return 1+2.70158*y*y*y+1.70158*y*y;}
            v2f vert(appdata v)
            {
                v2f o;float3 c=normalize(v.middle);float seed=v.uv.y;
                float delay=(1-dot(c,normalize(_Facing.xyz)))*.5*.13+seed*.035;float s=snapIn((_Age-delay)/.1);
                float gone=_Closed<0?0:saturate((_Closed-seed*.06)/.2);
                // A parried blow: the cells round it lit, a ring of them running out across the dome.
                float ring=0,flash=0;
                if(_Hit.w>=0){float th=acos(clamp(dot(c,normalize(_Hit.xyz)),-1,1));ring=exp(-abs(th-_Hit.w*7)*5)*exp(-_Hit.w*2.5);flash=exp(-th*4)*exp(-_Hit.w*7);}
                float grow=s*(1-gone),push=1+.3*(1-saturate((_Age-delay)/.1))+.55*gone+.1*ring+.06*flash;
                float3 p=(c+(v.vertex.xyz-c)*max(grow,0))*push;
                o.wpos=mul(unity_ObjectToWorld,float4(p,1)).xyz;o.wn=UnityObjectToWorldNormal(v.normal);o.pos=UnityWorldToClipPos(o.wpos);
                o.cell=float4(v.uv.x,seed,saturate(s)*(1-gone),c.y);o.lift=float2(ring,flash);
                // Where it is on screen and how far from the eye, for its contact glow against the scene's depth.
                o.screen=ComputeScreenPos(o.pos);o.screen.z=-mul(UNITY_MATRIX_V,float4(o.wpos,1)).z;
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float hash1(float x){return frac(sin(x*12.9898)*43758.5453);}
            float4 frag(v2f i,float face:VFACE):SV_Target
            {
                float edge=i.cell.x,seed=i.cell.y,shown=i.cell.z;if(shown<=.001)discard;
                float t=_Time.y;bool front=face>0;
                // The cell's outline: a crisp band along its rim, at least about a pixel and a third wide, its edge split into colour
                // (red a hair outward, blue a hair inward); and a fainter second line inside it.
                float aa=max(fwidth(edge),1e-4),width=max(.05,aa*1.35);
                float3 outline=saturate((edge+float3(aa*.8,0,-aa*.8)-(1-width))/aa);
                float inner=saturate(1-abs(edge-.8)/max(aa*1.1,.01));
                // The fresnel: clear face-on, brighter as the light grazes it toward the silhouette.
                float3 n=normalize(i.wn),view=normalize(_WorldSpaceCameraPos-i.wpos);float rim=pow(1-abs(dot(n,view)),3);
                // Fine scanlines drifting up, a slow interference band climbing the dome, the snap-in's flare.
                float scan=pow(.5+.5*sin(i.wpos.y*150-t*8),10);
                float band=exp(-abs(frac(t*.9)*2.6-.3-(i.cell.w*.5+.5)*2)*5);
                float start=exp(-max(_Age,0)*9);
                // Each cell flickers; now and then one glitches bright or drops out for a moment.
                float flicker=.86+.14*sin(t*31+seed*91)*sin(t*17+seed*13);float beat=floor(t*14);
                float glitch=step(.982,hash1(beat+seed*311.7)),dropout=step(.99,hash1(beat*1.31+seed*57.3+7.1));
                float ring=i.lift.x,flash=i.lift.y;
                // Where the dome cuts into the floor, a wall or the pilot: a soft line of light (sampled outside any branch).
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float touch=1-saturate((scene-i.screen.z)/.07);touch*=touch*step(.5,_Soft);
                float lines=.14+.42*rim+.4*start+.85*ring+1.1*flash+.55*glitch;
                float fill=.004+.045*rim+.02*scan*(.25+rim)+.035*band*(.3+rim)+.16*ring+.28*flash+.06*start;
                if(!front){lines*=.3;fill*=.12;inner*=.5;}
                // The tint shimmers about the pilot's colour with the view angle, the height and the cell (a thin film's play).
                float3 film=cos(6.2832*(float3(0,.33,.67)+rim*.55+i.cell.w*.18+seed*.25+t*.07));
                float3 tint=max(_BarrierColor.rgb*(1+.32*film),0);
                float3 c=tint*(outline*lines+inner*(.05+.16*rim)*(1+ring+flash)+fill)*flicker*(1-.85*dropout)+tint*touch*.45;
                // Hot spots run to white: a blow's flash and ring on the lines, a glitch.
                c=lerp(c,float3(1,1,1)*dot(c,float3(.3,.5,.2))*1.2,saturate(flash*.6+ring*.25+glitch*.3)*outline.g);
                c*=shown*_Strength;
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));
                return float4(c,0);
            }
            ENDCG
        }
    }
}
