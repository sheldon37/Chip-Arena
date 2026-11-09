// The parry's hexagon barrier (owner 2026-11-09: "pressing F; Lets add more of an effect rather that just turning character a color. Make it
// high quality, animated, and visible. Maybe like hexagon barriers?"): the dome of hex cells built in Core/NativeParryBarrier.cs (uv: x 0 at a
// cell's middle to 1 at its rim, y the cell's seed; uv2 the cell's middle on the unit sphere), drawn round the pilot while the parry's window is
// open (Runtime NativeParryBarrierPresentation.cs). Every cell animates on its own: it snaps in with an overshoot, the cells facing the pilot's
// front first and round to the back (_Facing); it breathes and flickers; a band of light climbs the dome; a parried blow (_Hit: its way from the
// middle, w its age) lights the cells round it white and sends a ring of pushed-out, brightened cells across the dome; when the window shuts
// (_Closed) the cells burst outward, shrinking and fading, each a moment apart. Bright rims and a faint fill, brighter at the silhouette; the
// inside of the dome drawn fainter. Additive, no depth write, both faces, scene fog toward black. Colour the pilot's accent (linear, HDR).
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
            float4 _BarrierColor,_Facing,_Hit;float _Age,_Closed,_Strength;
            struct appdata{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float3 middle:TEXCOORD1;};
            struct v2f{float4 pos:SV_POSITION;float3 wpos:TEXCOORD0;float3 wn:TEXCOORD1;float4 cell:TEXCOORD2;float2 lift:TEXCOORD3;UNITY_FOG_COORDS(4)};
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
                o.cell=float4(v.uv.x,seed,saturate(s)*(1-gone),c.y);o.lift=float2(ring,flash);UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i,float face:VFACE):SV_Target
            {
                float edge=i.cell.x,seed=i.cell.y,shown=i.cell.z;if(shown<=.001)discard;
                float t=_Time.y;float pulse=.55+.45*sin(t*5+seed*40),flicker=.85+.15*sin(t*37+seed*91)*sin(t*23+seed*13);
                float border=smoothstep(.7,.97,edge),core=smoothstep(.92,1,edge);
                float3 n=normalize(i.wn),view=normalize(_WorldSpaceCameraPos-i.wpos);float rim=pow(1-abs(dot(n,view)),2);
                // A band of light climbing the dome, and a flare over the whole as it snaps up.
                float band=exp(-abs(frac(t*.9)*2.6-.3-(i.cell.w*.5+.5)*2)*5)*.3,start=exp(-_Age*12)*.6;
                float ring=i.lift.x,flash=i.lift.y;
                float glow=(.05+.05*pulse)+border*(.7+.45*pulse)+rim*.45+band*(.4+border)+start+ring*(1.2+border*1.5)+flash*(1.6+border);
                float3 c=_BarrierColor.rgb*glow*flicker;c=lerp(c,float3(1,1,1)*glow,saturate(core*.35+flash*.6+ring*.3));
                c*=shown*_Strength*(face>0?1:.45);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));
                return float4(c,0);
            }
            ENDCG
        }
    }
}
