// Ceiling-field hit holograms (client/ceiling-field.ts CeilingFieldVisuals at 05950ea; Core/NativeCeilingField.cs,
// Runtime/NativeCeilingFieldPresentation.cs). One dynamic world-space mesh holds every live hologram, a 7 x 7 m quad each:
//   COLOR      the screen colour, sRGB 0..1: the tint uniform's linear value (the source ShaderMaterial writes it unencoded)
//   TEXCOORD0  xy: the source plane coordinates p = (uv - .5) * 7, z: the hologram's age in seconds
// The fragment maths is the source GLSL line for line (fract -> frac, vec -> float); the ring's pow((d - radius) / .09, 2.)
// is written as a square (pow of a negative base is undefined; the compilers fold it to x * x). The source blends additively
// (SrcAlpha, One) in its unencoded framebuffer, with the alpha clamped to [0, 1]: the colour is premultiplied by that alpha
// in screen space and then linearised, so a Linear project adds exactly the source's light over a dark background. No fog,
// no tone mapping, no depth write, both faces.
Shader "ChipArena/NativeCeilingField"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            // The exact sRGB decode (UnityCG's GammaToLinearSpace polynomial darkens the faint grid threefold near 0).
            float3 ToLinear(float3 c){c=max(c,0.);return lerp(c/12.92,pow((c+.055)/1.055,2.4),step(.04045,c));}
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float3 field:TEXCOORD0;};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.field=v.uv.xyz;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 p=i.field.xy;float age=i.field.z;
                float d=length(p),radius=.2+age*4.5;float k=(d-radius)/.09;float ring=exp(-k*k);
                float2 cell=abs(frac(p*2.)-.5);float grid=1.-smoothstep(.015,.06,min(cell.x,cell.y));
                float reveal=(1.-smoothstep(radius-.1,radius+.3,d))*exp(-d*.65);
                float left=max(0.,1.-age/1.05);float fade=left*sqrt(left);
                float flash=exp(-age*18.)*exp(-d*3.);
                float scan=.65+.35*sin(p.x*16.+age*19.);
                float alpha=saturate((grid*reveal*.36*scan+ring*.85+flash)*fade);
                float3 c=i.color.rgb*alpha;
                #ifndef UNITY_COLORSPACE_GAMMA
                c=ToLinear(c);
                #endif
                return float4(c,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
