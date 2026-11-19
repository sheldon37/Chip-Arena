Shader "ChipArena/Thunderheart Enamel"
{
    Properties { _Music("Score", Vector)=(0,0,0,0) _Signal("Mechanical signal", Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        struct Input { float4 tint; float3 worldPos; float facing : VFACE; };
        float4 _Music;
        float _Signal;
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.tint=v.color;
        }
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            half3 color=i.tint.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            float3 p=i.worldPos;
            half grain=sin(p.y*11.5+sin(p.x*3.8+p.z*2.1))*sin(p.z*3.7-p.x*2.4);
            half metal=step(.32,i.tint.a)*.65;
            half luminous=step(.71,i.tint.a);
            half ripple=.5+.5*sin(length(p.xz)*1.65-_Music.w);
            o.Albedo=color*(.94+.06*grain);
            o.Metallic=metal;
            o.Smoothness=lerp(.36,.76,metal)+.045*grain;
            o.Emission=color*luminous*(.23+_Music.x*.55+_Music.y*.42*ripple+_Music.z*.24+_Signal);
            o.Normal=float3(0,0,i.facing>=0?1:-1);
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
