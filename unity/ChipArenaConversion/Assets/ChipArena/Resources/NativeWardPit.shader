Shader "ChipArena/WardPit" {
 Properties{_Open("Opening",Range(0,1))=1}
 SubShader{Tags{"Queue"="Transparent+1" "RenderType"="Transparent"} ZWrite Off Cull Back Blend SrcAlpha OneMinusSrcAlpha Offset -3,-6
 Pass{
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Open;
 struct V{float4 vertex:POSITION;};struct F{float4 position:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;};
 F vert(V v){F o;o.position=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz*2.1;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(F i):SV_Target{float3 d=normalize(i.world-_WorldSpaceCameraPos),p=float3(i.local.x,0,i.local.z);float tx=abs(d.x)>1e-5?(sign(d.x)*1.05-p.x)/d.x:1e9,tz=abs(d.z)>1e-5?(sign(d.z)*1.05-p.z)/d.z:1e9,ty=d.y<-1e-5?-7/d.y:1e9;float t=min(min(tx,tz),ty);float3 h=p+d*max(t,0);float down=-h.y;float3 color=0;
 if(t<ty){float side=tx<tz?0:1,u=tx<tz?h.z:h.x;float3 base=lerp(float3(.13,.115,.16),float3(.085,.075,.11),side);float seam=1-smoothstep(0,.03,abs(frac(down*1.6)-.5)-.44),panel=1-smoothstep(0,.025,abs(frac(u*1.15+.5)-.5)-.46);float rung=(1-smoothstep(0,.02,abs(u)-.32))*(1-smoothstep(0,.025,abs(frac(down*2.4)-.5)-.45))*step(.15,down)*side;color=(base*(1-.45*max(seam,panel))+float3(.07,.065,.075)*rung)*exp(-down*.95)+float3(.32,.05,.08)*exp(-down*7)*.35;}
 return fixed4(color,smoothstep(0,.5,_Open));}
 ENDCG
 }} }
