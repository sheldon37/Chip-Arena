Shader "ChipArena/NativeFallenBody"
{
    // NOCTIEL's plate, robe, wings and sword (docs/EXPEDITION-BOSS-FINAL.md section 4): black, white and grey only, whatever map light is around, so its look can't drift from the design. One hard key (_FbKey, the shot's) in two toon bands with a porcelain
    // highlight, a rim, a faint bounce off the water; the white light inside it as its own glow (_FbGlow), bright enough to bloom. The reflection in the water is the same draw mirrored (_FbMirror: lit as the real one from the mirrored eye). The second pass is the
    // black line, where the bank's vertex alpha asks for one. _FbClipAxis cuts a piece off along an axis (the veins climbing the blade).
    Properties { _FbGlow("Glow",Float)=0 _FbFlash("Flash",Float)=0 _FbMirror("Mirror",Float)=0 _FbClipAxis("Cut",Vector)=(0,0,0,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float _FbGlow,_FbFlash,_FbMirror;float4 _FbClipAxis;float4 _FbKey;
            struct a2v{float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;};
            struct v2f{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 color:COLOR;float cut:TEXCOORD2;};
            v2f vert(a2v v){v2f o;float4 w=mul(unity_ObjectToWorld,v.vertex);o.pos=mul(UNITY_MATRIX_VP,w);o.world=w.xyz;o.normal=UnityObjectToWorldNormal(v.normal);o.color=v.color;o.cut=dot(v.vertex.xyz,_FbClipAxis.xyz);return o;}
            float4 frag(v2f i):SV_Target
            {
                clip(_FbClipAxis.w-i.cut);
                float3 c=i.color.rgb;
                if(_FbGlow>0)return float4(c*_FbGlow,1);
                float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos-i.world);if(_FbMirror>.5){n.y=-n.y;v.y=-v.y;}
                float ndl=dot(n,_FbKey.xyz);
                // Two hard bands, a soft porcelain sheen and a sharp highlight.
                float band=smoothstep(-.05,.02,ndl)*.5+smoothstep(.36,.42,ndl)*.5;
                float h=saturate(dot(n,normalize(_FbKey.xyz+v)));float spec=pow(h,110)*2.6+pow(h,18)*.16;
                float rim=pow(1-saturate(dot(n,v)),3.4);float bounce=saturate(.5-n.y*.5);bounce*=bounce;
                float3 o=c*(_FbKey.w*(band+spec)+.035+.11*bounce)+rim*(.34*_FbKey.w+.04)*c;
                // The cloth (the robe and the undersuit, tools/gear/fallen_boss.py 'cloth' 0x17171B): dark, but fabric, never a hole in
                // the black stage (owner 2026-11-06: the dark column beside the planted sword). A charcoal floor in the shade and a
                // velvet sheen toward its edges, grey only; the black of the eyes and the veins is darker and left as it was.
                float lum=dot(c,float3(.2126,.7152,.0722));float cloth=smoothstep(.04,.06,lum)*(1-smoothstep(.13,.17,lum));
                float fv=1-saturate(dot(n,v));o+=cloth*(c*(.28+.2*bounce)+float3(.78,.8,.86)*fv*fv*(.05+.08*_FbKey.w));
                o+=_FbFlash;
                return float4(o,1);
            }
            ENDCG
        }
        Pass
        {
            Cull Front ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            float _FbGlow;float4 _FbClipAxis;
            struct a2v{float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;};
            struct v2f{float4 pos:SV_POSITION;float cut:TEXCOORD0;};
            v2f vert(a2v v)
            {
                v2f o;float4 w=mul(unity_ObjectToWorld,v.vertex);float3 n=UnityObjectToWorldNormal(v.normal);float d=distance(w.xyz,_WorldSpaceCameraPos);float width=_FbGlow>0||v.color.a<.5?0:clamp(.0012*d,.006,.05);
                w.xyz+=n*width;o.pos=mul(UNITY_MATRIX_VP,w);if(width<=0)o.pos=float4(0,0,-2,1);o.cut=dot(v.vertex.xyz,_FbClipAxis.xyz);return o;
            }
            float4 frag(v2f i):SV_Target{clip(_FbClipAxis.w-i.cut);return float4(0,0,0,1);}
            ENDCG
        }
    }
    Fallback Off
}
