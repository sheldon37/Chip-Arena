// The anime map style's outline (docs/MAP-STYLE-ANIME.md "Outlines"; Runtime/NativeToonOutline.cs draws it, Core
// NativeMapStyle.Edge is its CPU copy). One full-screen pass from a command buffer on the match camera at
// CameraEvent.BeforeForwardAlpha: after the opaque scene, before the transparent effects and before the bloom and grade, so
// effects and the view model are never outlined. Built-in forward, SM 3.5, Linear.
//  - Input: the camera's depth texture (_CameraDepthTexture), which the normal tiers already render (the shadowed key light
//    and the soft particles ask for it; NativeToonOutline asks too), so the pass adds no geometry pass. The low tier has no
//    depth texture and no pass.
//  - Lines: a silhouette where a neighbour (one line width away, left, right, down, up) lies farther than the other side's
//    slope carries on by the gap (the second difference of depth across the pixel: a plane, even seen grazing, carries on
//    and is never lined), drawn on the nearer pixel only, so a line belongs to the occluding surface; a crease where 1/depth
//    bends (it is affine across any
//    plane, so a flat wall or floor never lines itself), relative to the pixel's own 1/depth, and only where no neighbour
//    is nearer by the gap (the far side of a silhouette is never lined, by either rule). Faded out with distance
//    (_ArenaEdge.y to .z m), times the strength _ArenaEdge.x.
//  - Only the environment: the stencil test passes where bit 16 is set, which NativeBlockLit, NativeLit and the scenery's
//    SourceLit materials write and the pilots', monsters' and zombies' shaders clear. With the line on the nearer pixel,
//    a character's silhouette is never lined by this pass (the characters bring their own outline), while a wall in front
//    of one still lines its own edge.
//  - Colour: the scene multiplied toward _ArenaEdgeColor (a dark tint, so a line reads as a darker version of what it
//    crosses, like a toon character's outline), one pass, no texture other than the depth.
// _ArenaEdge: x strength (0 off), y fade start (m), z fade end (m), w crease threshold. _ArenaEdgeColor: rgb the tint (linear).
Shader "Hidden/ChipArena/NativeToonEdge"
{
    Properties { _EdgeWidth("Line width (px)",Float)=1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend DstColor Zero
            Stencil { Ref 16 ReadMask 16 Comp Equal }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _EdgeWidth;
            float EdgeDepth(float2 uv){return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture,float4(uv,0.0,0.0)));}
            // EDGE SHARED BEGIN (HLSL and GLSL: float literals only; the host supplies EdgeDepth; Core NativeMapStyle.Edge mirrors it)
            float4 _ArenaEdge;float4 _ArenaEdgeColor;
            #define EDGE_GAP_MIN 0.25
            #define EDGE_GAP_SHARE 0.04
            #define EDGE_CREASE 0.85
            float EdgeLine(float2 uv,float2 px)
            {
                float c=EdgeDepth(uv);
                float l=EdgeDepth(uv-float2(px.x,0.0));float r=EdgeDepth(uv+float2(px.x,0.0));float d=EdgeDepth(uv-float2(0.0,px.y));float u=EdgeDepth(uv+float2(0.0,px.y));
                float gap=EDGE_GAP_MIN+EDGE_GAP_SHARE*c;float sil=step(gap,max(l+r,d+u)-2.0*c);
                float ic=1.0/c;float bend=max(abs(1.0/l+1.0/r-2.0*ic),abs(1.0/d+1.0/u-2.0*ic))/ic;
                float crease=smoothstep(_ArenaEdge.w,_ArenaEdge.w*2.0,bend)*step(c-min(min(l,r),min(d,u)),gap);
                return saturate(max(sil,crease*EDGE_CREASE))*(1.0-smoothstep(_ArenaEdge.y,_ArenaEdge.z,c))*_ArenaEdge.x;
            }
            float3 EdgeTint(float a){return lerp(float3(1.0,1.0,1.0),_ArenaEdgeColor.rgb,a);}
            // EDGE SHARED END
            struct v2f{float4 pos:SV_POSITION;};
            // A full-screen triangle in clip space (Runtime NativeToonOutline's mesh: (-1,-1), (3,-1), (-1,3)).
            v2f vert(float4 v:POSITION){v2f o;o.pos=float4(v.xy,0.5,1.0);return o;}
            float4 frag(v2f i):SV_Target
            {
                float2 texel=_ScreenParams.zw-1.0;
                return float4(EdgeTint(EdgeLine(i.pos.xy*texel,texel*_EdgeWidth)),1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
