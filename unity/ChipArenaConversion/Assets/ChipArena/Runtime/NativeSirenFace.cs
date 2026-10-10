using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The dev-siren entrance face for a frame (Core/NativeSirenSmileFace.cs): the 25 fangs, the eyes and eye halos and the
    /// cheeks, each piece's head-local transform mirrored into Unity (S·M·S, the frame the model bank is stored in).
    /// </summary>
    public sealed class NativeSirenFacePose
    {
        public readonly NativeSirenSmileFace Face=new NativeSirenSmileFace();
        public readonly Matrix4x4[] Tooth=new Matrix4x4[NativeSirenSmileFace.Teeth],Eye=new Matrix4x4[2],Halo=new Matrix4x4[2],Cheek=new Matrix4x4[NativeSirenSmileFace.CheekPieces];
        /// <summary>The face at intro `age` for the boss song's beat table (null: the intro's own).</summary>
        public NativeSirenFacePose Set(double age,double[] beats)
        {
            var f=Face.Evaluate(age,beats);
            for(int i=0;i<Tooth.Length;i++)Tooth[i]=Mirror(f.Tooth,i*16);
            for(int i=0;i<2;i++){Eye[i]=Mirror(f.Eye,i*16);Halo[i]=Mirror(f.Halo,i*16);}
            for(int i=0;i<Cheek.Length;i++)Cheek[i]=Mirror(f.Cheeks,i*16);
            return this;
        }
        private static Matrix4x4 Mirror(double[] m,int o)
        {var u=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)u[r,c]=(float)((r==0)^(c==0)?-m[o+r*4+c]:m[o+r*4+c]);return u;}
    }

    public sealed partial class NativeSourceModels
    {
        // The unit fang (NativeSirenSmileFace.FangOutline, 5 triangles) in the teeth's glow material, one mesh per row: the
        // lower row's holds -v (its tips point up), so no fang's transform is mirrored and one cull mode serves both rows.
        private readonly Part[] sirenFangs=new Part[2];
        /// <summary>True when both fang parts (upper and lower row) were built from the bank's teeth.</summary>
        public bool HasSirenFangs=>sirenFangs[0]!=null&&sirenFangs[1]!=null;
        private void SirenFangs(SourceModelSet data,Part teeth)
        {
            SourceSection section=null;foreach(var m in data.models)if(m.id=="siren")foreach(var s in m.sections)if(s.part=="siren-teeth-upper")section=s;
            var color=new Color(section.colors[0],section.colors[1],section.colors[2],section.colors[3]);var glow=new Vector4(section.emission[0],section.emission[1],section.emission[2],section.emission[3]);
            var uv=new double[NativeSirenSmileFace.Fang.Length];var fan=NativeSirenSmileFace.FangFan;
            for(int row=0;row<2;row++)
            {
                NativeSirenSmileFace.FangOutline(uv,row==0);
                // Source-frame triangles counter-clockwise in the row's own (u, v) (every fang's map keeps the turn), then the
                // bank's frame: x mirrored and each triangle turned over, as tools/export_models.mjs stores the grin rows.
                int n=fan.Length;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);var texture=new Vector2[n];var triangles=new int[n];
                for(int t=0;t<n;t+=3)
                {
                    int a=fan[t],b=fan[t+1],c=fan[t+2];double cross=(uv[b*2]-uv[a*2])*(uv[c*2+1]-uv[a*2+1])-(uv[c*2]-uv[a*2])*(uv[b*2+1]-uv[a*2+1]);
                    if(cross<0){int q=b;b=c;c=q;}
                    int[] k={a,b,c};for(int j=0;j<3;j++){vertices[t+j]=new Vector3(-(float)uv[k[j]*2],(float)uv[k[j]*2+1],0);normals[t+j]=Vector3.forward;colors[t+j]=color;emission.Add(glow);}
                    triangles[t]=t;triangles[t+1]=t+2;triangles[t+2]=t+1;
                }
                var mesh=new Mesh{name="siren / fang "+(row==0?"upper":"lower")};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=texture;mesh.SetUVs(1,emission);mesh.triangles=triangles;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                pieceMeshes.Add(mesh);sirenFangs[row]=new Part{Name="siren-fang",Bone=-1,Mesh=mesh,Material=teeth.Material,NoShadow=true};
            }
        }
        /// <summary>
        /// The dev-siren entrance face on the baked head track `head` (Unity frame): the eyes (one of the 41 brow cuts), the 25
        /// fangs at the smile's glow (from the cue), and the cheekbones with their ash trims, lit with the stage like the bake.
        /// </summary>
        public void QueueSirenFace(NativeSirenFacePose pose,Matrix4x4 head,int layer,Camera camera)
        {
            if(pose==null||!lookPieces.TryGetValue("siren",out var list))return;var f=pose.Face;
            for(int i=0;i<2;i++){var shapes=list[NativeSirenLook.Eye+i].Shapes;if(shapes!=null)Face(shapes[f.Shape],head*pose.Eye[i],layer,camera,Vector4.one);}
            if(f.Grin&&sirenFangs[0]!=null){float g=(float)f.Glow;var glow=new Vector4(g,g,g,1);for(int i=0;i<NativeSirenSmileFace.Teeth;i++)Face(sirenFangs[i<NativeSirenSmileFace.UpperTeeth?0:1],head*pose.Tooth[i],layer,camera,glow);}
            for(int k=0;k<NativeSirenSmileFace.CheekPieces;k++){var part=list[NativeSirenLook.Detail+NativeSirenDetail.CheekPiece(k)].Part;if(part!=null)Face(part,head*pose.Cheek[k],layer,camera,Vector4.one);}
        }
        private void Face(Part part,Matrix4x4 world,int layer,Camera camera,Vector4 glow)
        {
            var key=(part,layer);if(!draws.TryGetValue(key,out var d)){d=new Draws{Part=part,Layer=layer,Camera=camera};draws.Add(key,d);}
            d.Matrices.Add(world);d.Bodies.Add(Vector4.one);d.Accents.Add(Vector4.one);d.Tints.Add(Vector4.one);d.Glows.Add(glow);
        }
    }

    public sealed partial class NativeSirenLookDraw
    {
        // The entrance's eye halos: the eye-halo quad at the face's opacity, one baked quad per 1/40 (made on first use).
        private const int IntroHaloLevels=40;private Mesh[] introHalo;
        /// <summary>The two eye halos of the entrance face on the baked head track, facing `facing`, at the face's linear opacity (HaloAlpha).</summary>
        public void IntroHalos(NativeSirenFacePose pose,Matrix4x4 head,Camera facing,int layer,Camera camera)
        {
            if(pose==null||facing==null)return;int level=Mathf.Clamp(Mathf.RoundToInt((float)pose.Face.HaloAlpha*IntroHaloLevels),0,IntroHaloLevels);if(level==0)return;
            if(introHalo==null)introHalo=new Mesh[IntroHaloLevels+1];
            var quad=introHalo[level]??(introHalo[level]=Quad("Native Siren entrance eye halo "+level,NativeSirenLook.EyeHaloColor,(float)level/IntroHaloLevels));
            for(int i=0;i<2;i++)Sprite(quad,head*pose.Halo[i],facing.transform.rotation,layer,camera,(float)NativeSirenSmileFace.HaloSpread);
        }
        private void DisposeIntroHalos(){if(introHalo==null)return;foreach(var q in introHalo)if(q!=null)CapturedAnimation.Release(q);introHalo=null;}
    }

    public sealed partial class ArenaPresentation
    {
        private readonly NativeSirenFacePose sirenFace=new NativeSirenFacePose();
        /// <summary>
        /// The dev-siren entrance face (NativeSirenSmileFace) on the stage's baked head track at the age the entrance is drawn
        /// at (NativeIntroClock), shown while the bake shows the head: the stare, then the acted smile from the cue. The bake
        /// itself leaves the source's eyes and grin out (tools/export_cinematics.mjs) and carries the smile's body layer.
        /// </summary>
        private void SirenEntranceFace(NativeSceneClip clip,float age,Camera camera)
        {
            int head=clip.head;if(head<1||head>=clip.tracks||NativeSceneMotion.Opacity(clip,head,age)<=.001f)return;
            var at=NativeSceneMotion.Matrix(clip,head,age);sirenFace.Set(age,introBeats);sourceModels.QueueSirenFace(sirenFace,at,0,null);
            if(sirenLookFailed||camera==null)return;
            if(sirenLookDraw==null)try{sirenLookDraw=new NativeSirenLookDraw();}catch(System.InvalidOperationException e){sirenLookFailed=true;Debug.LogWarning("Siren orbs and halos disabled: "+e.Message);return;}
            sirenLookDraw.IntroHalos(sirenFace,at,camera,0,camera);
        }
    }
}
