using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // NOCTIEL's claymore as Core NativeFallenSword builds it (the owner, 2026-11-08: "more stylized. Maybe slightly longer, thinner and overall a better
    // design with glow effects"), drawn on hand_r in place of the gear bank's sword pieces, with its light: the fuller's line, the edges of phase 3's
    // black blade, phase 4's blade of light, the guard's points and the pommel's eye, each glowing by phase and breathing a little; a soft glow along
    // the blade, a glint running up its edge now and then, and motes drifting off it (rising off phase 3's edges, phase 4's light).
    // Presentation only: the blade's length is drawn, never read by a rule.
    public sealed partial class ArenaPresentation
    {
        private readonly Mesh[,] fbSword=new Mesh[NativeFallenSword.Count,2];private readonly bool[,] fbSwordGlowOnly=new bool[NativeFallenSword.Count,2];private bool fbSwordBuilt;
        /// <summary>The bank's sword pieces stay hidden while the built claymore is there, its words with them (the owner, 2026-11-08: "remove the text
        /// from the sword of noctiel").</summary>
        private bool FallenSwordOn=>fbSwordBuilt;
        private static bool FbSwordPart(string name)=>name.StartsWith("sword",StringComparison.Ordinal);
        /// <summary>The sword's frame on hand_r (the bank's to_sword(): x across the edges, y through the flats, z up the blade, the fist at the origin).</summary>
        private static Matrix4x4 FbSwordFrame()
        {
            var b=FbBladeWay.normalized;var f=FbFlatWay-b*Vector3.Dot(FbFlatWay,b);f.Normalize();var a=Vector3.Cross(b,f).normalized;
            var m=Matrix4x4.identity;m.SetColumn(0,a);m.SetColumn(1,f);m.SetColumn(2,b);m.SetColumn(3,new Vector4(FbGrip.x,FbGrip.y,FbGrip.z,1));return m;
        }
        private void FallenSwordBuild()
        {
            FallenSwordDispose();var frame=FbSwordFrame();
            try
            {
                for(int p=0;p<NativeFallenSword.Count;p++)for(int t=0;t<2;t++){var kit=NativeFallenSword.Build(p,t);fbSword[p,t]=FbKitMesh(kit,frame,"NOCTIEL "+NativeFallenSword.Names[p],out bool glowOnly);fbSwordGlowOnly[p,t]=glowOnly;}
                fbSwordBuilt=true;
            }
            catch(Exception e){Debug.LogWarning("NOCTIEL's sword: "+e.Message);FallenSwordDispose();return;}
        }
        private void FallenSwordDispose()
        {
            for(int p=0;p<NativeFallenSword.Count;p++)for(int t=0;t<2;t++){if(fbSword[p,t]!=null)CapturedAnimation.Release(fbSword[p,t]);fbSword[p,t]=null;}
            fbSwordBuilt=false;
        }
        private static readonly List<Vector3> FbKitVs=new List<Vector3>(),FbKitNs=new List<Vector3>();private static readonly List<Color> FbKitCs=new List<Color>();private static readonly List<int> FbKitT0=new List<int>(),FbKitT1=new List<int>();
        /// <summary>A kit's mesh through `m` (its corners turned round if `m` mirrors), the bank's hex made linear as ZombieGearBuild makes the bank's.</summary>
        internal static Mesh FbKitMesh(NativeFallenKit k,in Matrix4x4 m,string name,out bool glowOnly,Mesh into=null)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;bool flip=m.determinant<0;var nm=m.inverse.transpose;
            var vs=FbKitVs;var ns=FbKitNs;var cs=FbKitCs;vs.Clear();ns.Clear();cs.Clear();
            for(int i=0;i<k.Count;i++)
            {
                var p=k.P[i];var n=k.N[i];vs.Add(m.MultiplyPoint3x4(new Vector3(p.X,p.Y,p.Z)));ns.Add(nm.MultiplyVector(new Vector3(n.X,n.Y,n.Z)).normalized);
                uint h=k.Color[i];var c=new Color(((h>>16)&255)/255f,((h>>8)&255)/255f,(h&255)/255f,k.Line[i]?1:0);cs.Add(linear?new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),c.a):c);
            }
            List<int> Tris(List<int> from,List<int> o){o.Clear();for(int t=0;t+2<from.Count;t+=3){o.Add(from[t]);if(flip){o.Add(from[t+2]);o.Add(from[t+1]);}else{o.Add(from[t+1]);o.Add(from[t+2]);}}return o;}
            var mesh=into??new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};if(into!=null)mesh.Clear();mesh.indexFormat=k.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vs);mesh.SetNormals(ns);mesh.SetColors(cs);glowOnly=k.Lit.Count==0&&k.Glow.Count>0;
            if(glowOnly){mesh.subMeshCount=1;mesh.SetTriangles(Tris(k.Glow,FbKitT0),0,false);}
            else{mesh.subMeshCount=k.Glow.Count>0?2:1;mesh.SetTriangles(Tris(k.Lit,FbKitT0),0,false);if(k.Glow.Count>0)mesh.SetTriangles(Tris(k.Glow,FbKitT1),1,false);}
            mesh.RecalculateBounds();return mesh;
        }
        /// <summary>One kit mesh in the body shader: its lit faces, then its light at `glow` (a mesh of light only draws that alone).</summary>
        private void FbDrawKit(Mesh mesh,bool glowOnly,in Matrix4x4 m,float glow,float flash,float mirror,Vector4? cut)
        {
            var block=fbBlock;block.Clear();block.SetFloat(FbFlashId,flash);block.SetFloat(FbMirrorId,mirror);block.SetVector(FbClipId,cut??FbNoCut);
            if(!glowOnly){block.SetFloat(FbGlowId,0);Graphics.DrawMesh(mesh,m,fbBody,0,null,0,block,ShadowCastingMode.Off,false);FallenBossDraws++;}
            if(glowOnly||mesh.subMeshCount>1){block.SetFloat(FbGlowId,Mathf.Max(.001f,glow));Graphics.DrawMesh(mesh,m,fbBody,0,null,glowOnly?0:1,block,ShadowCastingMode.Off,false);FallenBossDraws++;}
        }
        /// <summary>Each piece's light by phase (1 to 4), before the frame's inner light.</summary>
        private static float FbSwordGlow(int piece,int phase,float now)
        {
            float breathe=.88f+.12f*Mathf.Sin(now*1.6f);
            switch(piece)
            {
                case NativeFallenSword.Fuller:return (phase==1?.5f:phase==2?.7f*(.85f+.15f*Mathf.Sin(now*13.1f)*Mathf.Sin(now*4.3f)):1.05f)*breathe;
                case NativeFallenSword.BladeDark:return 1.1f*breathe;
                case NativeFallenSword.BladeLight:return .72f*breathe;
                case NativeFallenSword.EyeShut:return .55f*breathe;
                case NativeFallenSword.EyeOpen:return 1.3f*breathe;
                default:return .9f*breathe;
            }
        }
        /// <summary>The claymore on `world` (hand_r's matrix, or the planted sword's), its reflection under the water through `mirror`.</summary>
        private void FallenSwordDraw(int phase,in Matrix4x4 world,float inner,float flash,Matrix4x4 mirror,Vector4 cut,int tier,float now,bool flakes)
        {
            int bit=1<<(phase-1);
            for(int p=0;p<NativeFallenSword.Count;p++)
            {
                if((NativeFallenSword.Phases[p]&bit)==0||(!flakes&&p==NativeFallenSword.Flakes))continue;var mesh=fbSword[p,tier]??fbSword[p,0];if(mesh==null)continue;bool g=fbSwordGlowOnly[p,tier];
                Vector4? c=p==NativeFallenSword.Veins1?cut:(Vector4?)null;float glow=inner*FbSwordGlow(p,phase,now);
                FbDrawKit(mesh,g,world,glow,flash,0,c);FbDrawKit(mesh,g,mirror*world,glow,flash,1,c);
            }
        }

        // ---- the light round it ----
        private float fbSwordGlintAt=-10,fbSwordMoteAt;private readonly System.Random fbSwordRandom=new System.Random(77);
        private struct FbSwordMote{public Vector3 At,Way;public float Born,Life,Size;}
        private readonly List<FbSwordMote> fbSwordMotes=new List<FbSwordMote>();
        /// <summary>The blade's soft light, a glint running up an edge, the motes off it: on the stage's frame through the effects' quads.</summary>
        private void FallenSwordLight(FallenBossFrame f,float now,float dt,Vector3 origin,in Matrix4x4 hand,bool held)
        {
            if(!fbSwordBuilt){fbSwordMotes.Clear();return;}var frame=hand*FbSwordFrame();
            Vector3 At(float z,float x=0)=>frame.MultiplyPoint3x4(new Vector3(x,0,z))-origin;
            int phase=f.Phase;float inner=f.Inner;float k=(phase==1?.07f:phase==2?.1f:phase==3?.13f:.3f)*inner*(.85f+.15f*Mathf.Sin(now*1.6f));
            var hilt=At(NativeFallenSword.BladeFrom);var tip=At(NativeFallenSword.Point-.06f);
            // The soft light along it: a wide faint line, a narrow brighter one at the fuller.
            fbFx.SwordLight(ToNV(hilt),ToNV(tip),.2f*NativeFallenBoss.Scale*.5f,k,phase==4?.9f:.6f);fbFx.SwordLight(ToNV(hilt),ToNV(At(1.28f)),.05f,k*1.6f,1.2f);
            // The guard's points of light and the pommel's eye.
            fbFx.SwordSpot(ToNV(At(.114f)),.16f,(phase>=3?.5f:.32f)*inner,1.4f);fbFx.SwordSpot(ToNV(At(-.17f)),phase>=3?.22f:.1f,(phase>=3?.7f:.25f)*inner,1.6f);
            // A glint running up an edge now and then.
            if(now-fbSwordGlintAt>(phase>=3?2.2f:3.6f)||now<fbSwordGlintAt)fbSwordGlintAt=now;float g=(now-fbSwordGlintAt)/.55f;
            if(g>=0&&g<=1){float t=Mathf.SmoothStep(0,1,g);float side=((int)(fbSwordGlintAt*10)%2)==0?1:-1;var at=At(NativeFallenSword.Z(t*.96f),side*NativeFallenSword.Width(t*.96f)*.95f);fbFx.SwordSpot(ToNV(at),.12f+.1f*Mathf.Sin(g*Mathf.PI),Mathf.Sin(g*Mathf.PI)*(.55f+.25f*phase/4f)*inner,2.2f);}
            // Motes: a few always, more off phase 3's edges (they drip upward) and phase 4's light; they drift up and fade.
            float rate=phase==1?1.2f:phase==2?2.5f:phase==3?7:10;if(!held)rate*=.4f;fbSwordMoteAt+=dt*rate;
            while(fbSwordMoteAt>=1&&fbSwordMotes.Count<48)
            {
                fbSwordMoteAt-=1;float t=(float)fbSwordRandom.NextDouble()*.95f;float side=fbSwordRandom.NextDouble()<.5?1:-1;float x=phase==3?side*NativeFallenSword.Width(t):(float)(fbSwordRandom.NextDouble()*2-1)*NativeFallenSword.Width(t);
                var way=new Vector3((float)(fbSwordRandom.NextDouble()-.5)*.25f,.35f+(float)fbSwordRandom.NextDouble()*.45f,(float)(fbSwordRandom.NextDouble()-.5)*.25f);
                fbSwordMotes.Add(new FbSwordMote{At=At(NativeFallenSword.Z(t),x),Way=way,Born=now,Life=1.1f+(float)fbSwordRandom.NextDouble()*1.2f,Size=.035f+(float)fbSwordRandom.NextDouble()*.04f});
            }
            if(fbSwordMoteAt>4)fbSwordMoteAt=0;
            for(int i=fbSwordMotes.Count-1;i>=0;i--)
            {
                var m=fbSwordMotes[i];float age=now-m.Born;if(age<0||age>m.Life){fbSwordMotes.RemoveAt(i);continue;}float u=age/m.Life;
                fbFx.ClothMote(ToNV(m.At+m.Way*age+new Vector3(0,.12f*age*age,0)),m.Size*(1-u*.5f),Mathf.Sin(Mathf.Min(1,u*4)*Mathf.PI*.5f)*(1-u)*(phase>=3?.85f:.6f)*inner);
            }
        }
    }
}
