Shader "ChipArena/NativeFallenSlash"
{
    // The Slash (docs/EXPEDITION-BOSS-FINAL.md section 10): a white line cut across the finished frame, the frame frozen, its halves sliding apart along the cut over the live image (or over black), black and white ink thrown off the line in streaks and drops. Core NativeFallenSlash gives the numbers.
    Properties { _MainTex("Frame",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _MainTex,_FbFrozen;
            float4 _FbLine;  // direction x, y; aspect; the frozen image's in use
            float4 _FbCut;   // draw, open, gap, width
            float4 _FbCut2;  // flash, black, mono, ink
            float _FbSeed;
            float H(float n){return frac(sin(n*127.1)*43758.5453);}
            float4 frag(v2f_img i):SV_Target
            {
                float aspect=_FbLine.z;float2 d=_FbLine.xy;float2 n=float2(-d.y,d.x);float2 p=(i.uv-.5)*float2(aspect,1);
                float along=dot(p,d),s=dot(p,n);float side=s>=0?1:-1;float halfLen=length(float2(aspect,1))*.5+.05;
                float a01=along/halfLen*.5+.5;
                float3 c=tex2D(_MainTex,i.uv).rgb;
                // The frozen halves: each slides its own way along the cut, and off it by the gap.
                if(_FbLine.w>.5)
                {
                    float2 off=d*(_FbCut.y*side)+n*(_FbCut.z*side);float2 q=p-off;float qs=dot(q,n);
                    float2 fuv=q/float2(aspect,1)+.5;bool inside=qs*side>=0&&fuv.x>=0&&fuv.x<=1&&fuv.y>=0&&fuv.y<=1;
                    if(inside)c=tex2D(_FbFrozen,fuv).rgb;
                    if(inside&&_FbCut.y>.001)c+=float3(1,1,1)*exp(-abs(qs)/.0028)*1.6;
                }
                // Black and white, as it cuts.
                float l=dot(c,float3(.3,.59,.11));c=lerp(c,saturate(l*1.2-.03).xxx,_FbCut2.z);
                // The line: drawn from one end, tapered, a white hot core with a soft halo.
                float drawn=smoothstep(_FbCut.x,_FbCut.x-.03,a01);float taper=pow(saturate(1-abs(a01*2-1)),.5);float w=_FbCut.w*taper+.0004;
                float core=smoothstep(w,w*.4,abs(s))*drawn;float halo=exp(-abs(s)/(w*2.4+.0015))*drawn;
                // A few thin cuts beside it.
                float scratch=0;for(int k=0;k<3;k++){float off2=(k==0?-.11:k==1?.17:-.27);float from=H(k+3.)*.5,to=from+.2+.3*H(k+7.);float m=smoothstep(from,from+.03,a01)*smoothstep(to,to-.03,a01);scratch+=smoothstep(.0016,.0003,abs(s-off2-.01*sin(a01*9.+k)))*m*saturate((_FbCut2.w-.2)*2.);}
                // Ink: streaks thrown off the line, and drops flung out from it.
                float ink=0,white=0;
                for(int L=0;L<2;L++)
                {
                    float cells=L==0?31.:67.;float fa=a01+s*.32*(a01-.5);float cell=floor(fa*cells);float f0=frac(fa*cells)-.5;float r1=H(cell+L*37.+_FbSeed),r2=H(cell*1.7+L*11.+_FbSeed),r4=H(cell*2.3+L*5.+_FbSeed);
                    float way=r2>.5?1.:-1.;float reach=pow(r1,3.)*(.05+.62*_FbCut.y+.16*_FbCut2.w)*_FbCut2.w*taper;
                    if(s*way>0.&&r1>.3&&reach>.002)
                    {
                        float k=abs(s)/reach;float width=(.46-.2*L)*pow(saturate(1.-k),1.6)*(.45+.55*r4);float st=smoothstep(width,width*.55,abs(f0))*step(k,1.);
                        float end=length(float2(f0/cells*2.2,abs(s)-reach*1.05));float dot_=smoothstep(.0052*(.5+r4),.0026*(.5+r4),end);
                        if(r1>.68){white=max(white,st);white=max(white,dot_);}else{ink=max(ink,st);ink=max(ink,dot_);}
                    }
                }
                float2 g=float2(along,s)*30.;float2 id=floor(g);float2 f=frac(g)-.5;float r3=H(id.x*3.1+id.y*17.7+_FbSeed);float2 o=float2(H(r3*9.+1.),H(r3*13.+2.))-.5;float dr=(.05+.22*r3*r3*r3)*step(abs(s),.5*_FbCut2.w*(.1+r3*.9)+_FbCut.y*.5)*step(.93,r3);float drop=smoothstep(dr,dr*.72,length(f-o*.7))*_FbCut2.w;
                if(r3>.975)white=max(white,drop);else ink=max(ink,drop);
                c=lerp(c,float3(0,0,0),saturate(ink));c=lerp(c,float3(1.6,1.6,1.6),saturate(white));
                c+=float3(1,1,1)*(core*5.+halo*.7+scratch*2.);
                c=lerp(c,float3(0,0,0),_FbCut2.y);
                c+=float3(1,1,1)*_FbCut2.x*1.3;
                return float4(c,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
