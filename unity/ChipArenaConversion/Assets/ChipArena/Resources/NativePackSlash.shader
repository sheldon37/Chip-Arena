// The slash rings, claw marks and line slashes from the owner's slash pack on the special moves and finishers
// (Runtime/NativePackSlashPresentation.cs; docs/PACK-EFFECTS.md). The pack's particle systems keep their meshes, curves and
// vertex streams; this draws them. A stroke: its texture scrolled by the particle's offset (TEXCOORD0.zw, a custom stream)
// and wobbled by a drifting shift texture (which also picks the part of an arc that shows), times twice the tint and the
// particle colour; with the dissolve on, cut away where the dissolve texture (scrolled the other way) rises past the
// particle's alpha, so a stroke burns out along its length as its alpha falls. A plain one (sparks, quads): the texture
// times the tint and the particle colour, its alpha shaped by a power and a gain. Alpha blended, both sides, no depth
// write, fogged.
Shader "ChipArena/PackSlash"
{
    Properties
    {
        _MainTex("Stroke (rgba)",2D)="white"{}
        [HDR]_TintColor("Tint",Color)=(1,1,1,1)
        _CutoutTex("Dissolve (r)",2D)="white"{}
        _UseCutout("Dissolve on",Float)=1
        _WobbleTex("Wobble (rg)",2D)="black"{}
        _Wobble("Wobble: shift (xy), drift (zw)",Vector)=(0,0,0,0)
        _Plain("Plain (no scroll, wobble or dissolve)",Float)=0
        _AlphaPow("Plain alpha power",Float)=1
        _AlphaMul("Plain alpha gain",Float)=1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex,_CutoutTex,_WobbleTex;float4 _MainTex_ST,_CutoutTex_ST,_WobbleTex_ST,_TintColor,_Wobble;float _UseCutout,_Plain,_AlphaPow,_AlphaMul;
            struct appdata { float4 vertex:POSITION; float4 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float4 stroke:TEXCOORD0; float2 burn:TEXCOORD1; float2 wobble:TEXCOORD2; float4 color:COLOR; UNITY_FOG_COORDS(3) };
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);
                o.stroke=float4(TRANSFORM_TEX(v.uv.xy,_MainTex),v.uv.zw);o.burn=TRANSFORM_TEX(v.uv.xy,_CutoutTex)-v.uv.zw;
                o.wobble=TRANSFORM_TEX(v.uv.xy,_WobbleTex);o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float4 c;
                if(_Plain>0.5)
                {
                    c=tex2D(_MainTex,i.stroke.xy)*_TintColor*i.color;
                    c.a=saturate(pow(c.a,_AlphaPow)*_AlphaMul);
                }
                else
                {
                    float2 shift=tex2D(_WobbleTex,i.wobble+_Time.x*_Wobble.zw).rg*_Wobble.xy;
                    c=tex2D(_MainTex,i.stroke.xy+i.stroke.zw+shift)*_TintColor*i.color*2.0;
                    if(_UseCutout>0.5)
                    {
                        // Kept while the burn texture stays under the particle's alpha plus the stroke's own.
                        float keep=step(tex2D(_CutoutTex,i.burn).r,i.color.a+c.a);
                        c.a=saturate(c.a*4.0)*keep;
                    }
                }
                UNITY_APPLY_FOG(i.fogCoord,c);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
