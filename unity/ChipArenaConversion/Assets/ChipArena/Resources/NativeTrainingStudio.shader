Shader "ChipArena/NativeTrainingStudio"
{
    // The Training Room's studio draws (Runtime/NativeTrainingPresentation.cs; Core NativeTrainingLook and NativeTrainingReadouts),
    // one shader in three modes chosen per material (_Mode, with the blend, depth write and queue set by the runtime):
    //  0 the floor grid: a quad just over the floor, multiplied over it (Blend DstColor Zero, no depth write, after the opaque
    //    queue), so the lines take the floor's own light. Lines every metre, bolder every 5 m and 10 m, measured from
    //    _GridOrigin.xy; _GridOrigin.z / .w switch the lines of constant x / z; nothing outside _GridRect or inside _GridCut.
    //    Each family is kept at least a pixel wide (and dimmed as much) and fades where its lines crowd under four pixels.
    //  1 a readout screen on a board: the cell's RGBA readout (sRGB) as light, times _Gain (bloom catches the lettering),
    //    opaque and fogged; texel edges stay crisp when magnified and the mip chain filters them when minified.
    //  2 a floating tag over a target: the same cell premultiplied by its alpha (the translucent plate) and _Fade.
    // _Flash (0..1) rims a screen or tag in cyan for a new hit. SM 3.5, built-in forward, fog per Unity's fog keywords.
    Properties
    {
        _MainTex ("Readout", 2D) = "black" {}
        _Mode ("Mode (0 grid, 1 screen, 2 tag)", Float) = 0
        _Gain ("Screen gain", Float) = 1.6
        _Fade ("Tag fade", Float) = 1
        _Flash ("Hit flash", Float) = 0
        _GridRect ("Grid rect (x0, z0, x1, z1)", Vector) = (-48,-28,48,28)
        _GridOrigin ("Grid origin (x, z) and families (x lines, z lines)", Vector) = (0,0,1,1)
        _GridCut ("Grid cut (x0, z0, x1, z1)", Vector) = (0,0,0,0)
        _GridInk ("Grid ink", Color) = (0.34,0.4,0.48,1)
        _GridWeights ("Grid weights (1 m, 5 m, 10 m)", Vector) = (0.2,0.36,0.52,0)
        _SrcBlend ("Source blend", Float) = 1
        _DstBlend ("Destination blend", Float) = 0
        _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_TexelSize;
            float _Mode,_Gain,_Fade,_Flash;
            float4 _GridRect,_GridOrigin,_GridCut,_GridInk,_GridWeights;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;UNITY_FOG_COORDS(2)};
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            // one family of lines `width` m wide every `spacing` m along coordinate c
            float GridFamily(float c,float spacing,float width)
            {
                float fw=max(fwidth(c),1e-5);
                float d=abs(frac(c/spacing+.5)-.5)*spacing;
                float w=max(width,fw*1.5);
                float cover=saturate((w*.5-d)/fw+.5);
                float crowd=saturate(spacing/(fw*4.0)-1.0);
                return cover*saturate(width/w)*crowd;
            }
            float GridLevels(float c)
            {
                return max(GridFamily(c,1.0,.02)*_GridWeights.x,max(GridFamily(c,5.0,.045)*_GridWeights.y,GridFamily(c,10.0,.075)*_GridWeights.z));
            }
            float GridAmount(float3 world)
            {
                float2 c=world.xz-_GridOrigin.xy;
                float2 inside=step(_GridRect.xy,world.xz)*step(world.xz,_GridRect.zw);
                float cut=step(_GridCut.x,world.x)*step(world.x,_GridCut.z)*step(_GridCut.y,world.z)*step(world.z,_GridCut.w);
                float ink=max(_GridOrigin.z*GridLevels(c.x),_GridOrigin.w*GridLevels(c.y));
                return ink*inside.x*inside.y*(1.0-cut);
            }
            // sample position for crisp texel edges when magnified (a one-pixel blend at each texel seam), plain when minified
            float2 CrispUv(float2 uv)
            {
                float2 texel=uv*_MainTex_TexelSize.zw;float2 seam=floor(texel+.5);float2 dudv=max(fwidth(texel),1e-4);
                float2 snapped=seam+clamp((texel-seam)/dudv,-.5,.5);
                float mixAmount=saturate(max(dudv.x,dudv.y)-1.0);
                return lerp(snapped,texel,mixAmount)*_MainTex_TexelSize.xy;
            }
            fixed4 frag(v2f i):SV_Target
            {
                if(_Mode<.5)
                {
                    float ink=GridAmount(i.world);
                    fixed4 shade=fixed4(lerp(float3(1,1,1),_GridInk.rgb,ink),1);
                    UNITY_APPLY_FOG_COLOR(i.fogCoord,shade,fixed4(1,1,1,1));
                    return shade;
                }
                float4 tex=tex2Dgrad(_MainTex,CrispUv(i.uv),ddx(i.uv),ddy(i.uv));
                float2 edge=min(i.uv,1.0-i.uv)*_MainTex_TexelSize.zw;
                float rim=saturate(1.0-min(edge.x,edge.y)/7.0)*_Flash;
                float3 glow=tex.rgb*_Gain+float3(.35,1.3,1.6)*rim;
                if(_Mode<1.5)
                {
                    fixed4 screen=fixed4(glow,1);
                    UNITY_APPLY_FOG(i.fogCoord,screen);
                    return screen;
                }
                float a=saturate(max(tex.a,rim))*_Fade;
                fixed4 tag=fixed4(glow*a,a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,tag,fixed4(unity_FogColor.rgb*a,a));
                return tag;
            }
            ENDCG
        }
    }
    Fallback Off
}
