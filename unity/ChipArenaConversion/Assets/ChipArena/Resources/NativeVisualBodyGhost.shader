// The visual body's spawn-in hologram (docs/BOZO-INTEGRATION.md step 4; Runtime/NativeVisualBodyPresentation.cs adds it on
// extra material slots of the body's renderers only while a spawn-in shows, so it draws the same skinned mesh; the values
// are the body's own per-renderer ones, NativeVisualBodyFx.cginc). The voxel pilot's hologram (NativeSpawnInPresentation.cs
// SpawnHologram on Resources/NativeEntranceGhost.shader) in one pass per slot, with its look: a fresnel rim and scanlines
// on world height scrolling with the clock, unlit, in the fog.
//  - _GhostSide 0: the slice leading the solid front (the hologram colour at the slice's opacity, scanlines) and the
//    completion's flash silhouette over the whole body (pale cyan at .55 of the flash, no scanlines).
//  - _GhostSide 1 or -1: one copy of the RGB split, moved by the split's offset to the camera's right or left, in
//    _GhostColor (magenta and cyan, set once by the arena) at the split's opacity.
// Alpha-blended without a depth write, behind the formed body's depth; the texture's alpha cuts out as the body's does.
Shader "ChipArena/VisualBodyGhost"
{
    Properties
    {
        _GhostColor("Split copy colour (linear)",Color)=(1,1,1,1)
        _GhostSide("0: the slice and flash; 1 or -1: a split copy to the camera's right or left",Float)=0
        _Texture2D("Cut-out (a)",2D)="white"{}
        _Cutoff("Cut-out",Range(0,1))=.5
        _SpawnFront("Spawn-in: solid front (world y), band, heat span, slice lead",Vector)=(100000,.24,.55,.38)
        _SpawnGlitch("Spawn-in: flash, split (m), split opacity, clock",Vector)=(0,0,0,0)
        _SpawnHolo("Spawn-in: hologram colour (linear) and slice opacity",Vector)=(1,1,1,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NativeVisualBodyFx.cginc"
            sampler2D _Texture2D;float4 _Texture2D_ST;float _Cutoff;float4 _GhostColor;float _GhostSide;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 texcoord:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_FOG_COORDS(3)};
            v2f vert(appdata v)
            {
                v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                // A split copy moves along the camera's right (the view matrix's first row).
                world+=UNITY_MATRIX_V[0].xyz*(_GhostSide*_SpawnGlitch.y);
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1.0));o.world=world;o.normal=UnityObjectToWorldNormal(v.normal);
                o.uv=TRANSFORM_TEX(v.texcoord.xy,_Texture2D);UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            // NativeEntranceGhost's look for one layer: colour and opacity brightened on the rim, scanlines when `scan` is 1.
            float4 Ghost(float3 rgb,float alpha,float scan,float3 world,float3 normal)
            {
                float3 view=normalize(_WorldSpaceCameraPos-world);float rim=1.0-saturate(abs(dot(normalize(normal),view)));rim=rim*rim;
                float lines=0.5+0.5*sin(world.y*120.0-_SpawnGlitch.w*9.0);lines=lerp(1.0,0.55+0.75*lines*lines,scan);
                return float4(rgb*(0.75+1.6*rim)*lines,saturate(alpha*(0.65+0.9*rim)*lines));
            }
            float4 frag(v2f i):SV_Target
            {
                clip(tex2D(_Texture2D,i.uv).a-_Cutoff);float4 c;
                if(abs(_GhostSide)<0.5)
                {
                    // The flash silhouette over the slice, as the voxel pilot draws them (the slice first).
                    float4 slice=Ghost(_SpawnHolo.rgb,SpawnSlice(i.world.y)*_SpawnHolo.w,1.0,i.world,i.normal);
                    float4 flash=Ghost(float3(0.8,1.0,1.1),_SpawnGlitch.x*0.55,0.0,i.world,i.normal);
                    c.a=flash.a+slice.a*(1.0-flash.a);c.rgb=(flash.rgb*flash.a+slice.rgb*slice.a*(1.0-flash.a))/max(c.a,0.0001);
                }
                else c=Ghost(_GhostColor.rgb,_SpawnGlitch.z,1.0,i.world,i.normal);
                clip(c.a-0.002);
                #ifdef UNITY_COLORSPACE_GAMMA
                c.rgb=LinearToGammaSpace(c.rgb);
                #endif
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
