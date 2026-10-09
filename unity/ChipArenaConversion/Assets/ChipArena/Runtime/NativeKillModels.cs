using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // C18 kill-effect pieces drawn through the shared source-model batches (one instanced draw per piece type):
    //  - KillCube: a lit cube with no emission (voxel-burst chunks: MeshLambertMaterial({color})).
    //  - KillGhost / QueueGhost: a translucent plain box or a model's meshes, flat albedo (Soul Drift: voxel-kit.ts draws
    //    see-through parts as their plain box), through Resources/NativeKillGhost.shader.
    //  - KillDrop: a blood drop, gibber-vfx.ts SphereGeometry(1,5,3), with the wound glow (WoundMaterial .3) when asked.
    //  - KillPool: a splat disk, CircleGeometry(1,12), translucent.
    //  - QueueRecolor: a model's own meshes with their albedo replaced (Frostbreak: material.color.set, emissive.set), through
    //    Resources/NativeKillBody.shader, which keeps each vertex's voxel shade.
    //  - QueueShadowless / KillCap: a model's meshes, or a wound-cube cap cell, casting no shadow (ragdoll.ts copies the rig
    //    with copyVoxelPart and woundCap turns castShadow off; only CrushDebris fragments cast one).
    // All geometry is built in source (three.js) axes and mirrored on x with the winding reversed, like tools/export_models.mjs.
    // Tints are linear colours. Meshes and materials are owned here and released by Dispose with the other source models.
    public sealed partial class NativeSourceModels
    {
        private Part killCube,killGhost,killDrop,killPool,killCap;private Material killBody,killGhostMaterial;private bool killBodyFailed,killGhostFailed;
        private readonly Dictionary<Material,Material> recolorMaterials=new Dictionary<Material,Material>(),ghostMaterials=new Dictionary<Material,Material>();
        private readonly Dictionary<Part,Part> recolorParts=new Dictionary<Part,Part>(),ghostParts=new Dictionary<Part,Part>(),shadowlessParts=new Dictionary<Part,Part>();private readonly List<Part> recolorStale=new List<Part>();
        private static readonly Vector4 NoGlow=Vector4.zero;

        // ---- geometry (source axes, CCW) mirrored into Unity -------------------------------------------------------------
        private sealed class Builder
        {
            public readonly List<Vector3> P=new List<Vector3>(),N=new List<Vector3>();public readonly List<int> I=new List<int>();
            public void Vertex(float x,float y,float z,float nx,float ny,float nz){P.Add(new Vector3(-x,y,z));N.Add(new Vector3(-nx,ny,nz));}
            public void Triangle(int a,int b,int c){I.Add(a);I.Add(c);I.Add(b);}
            public Mesh Mesh(string name,Color color,Vector4 emission)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(P);mesh.SetNormals(N);var colors=new Color[P.Count];var glow=new List<Vector4>(P.Count);var uv=new Vector2[P.Count];
                for(int i=0;i<P.Count;i++){colors[i]=color;glow.Add(emission);}mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,glow);mesh.SetTriangles(I,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
            }
        }
        /// <summary>A unit cube (BoxGeometry(1,1,1)).</summary>
        private static Builder Cube()
        {
            var b=new Builder();
            for(int axis=0;axis<3;axis++)foreach(int sign in new[]{1,-1})
            {
                var n=Vector3.zero;n[axis]=sign;var u=Vector3.zero;u[(axis+1)%3]=1;var v=Vector3.Cross(n,u);int start=b.P.Count;
                foreach(var c in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){var p=(n+u*c.x+v*c.y)*.5f;b.Vertex(p.x,p.y,p.z,n.x,n.y,n.z);}
                b.Triangle(start,start+1,start+2);b.Triangle(start,start+2,start+3);
            }
            return b;
        }
        /// <summary>three.js SphereGeometry(radius, widthSegments, heightSegments).</summary>
        public static void Sphere(int width,int height,List<Vector3> positions,List<Vector3> normals,List<int> indices)
        {
            var grid=new int[height+1,width+1];int index=0;
            for(int iy=0;iy<=height;iy++){float v=iy/(float)height;for(int ix=0;ix<=width;ix++){float u=ix/(float)width;float x=-Mathf.Cos(u*Mathf.PI*2)*Mathf.Sin(v*Mathf.PI),y=Mathf.Cos(v*Mathf.PI),z=Mathf.Sin(u*Mathf.PI*2)*Mathf.Sin(v*Mathf.PI);var n=new Vector3(x,y,z).normalized;positions.Add(new Vector3(-x,y,z));normals.Add(new Vector3(-n.x,n.y,n.z));grid[iy,ix]=index++;}}
            for(int iy=0;iy<height;iy++)for(int ix=0;ix<width;ix++)
            {
                int a=grid[iy,ix+1],b=grid[iy,ix],c=grid[iy+1,ix],d=grid[iy+1,ix+1];
                if(iy!=0){indices.Add(a);indices.Add(d);indices.Add(b);}if(iy!=height-1){indices.Add(b);indices.Add(d);indices.Add(c);}
            }
        }
        private Part KillPart(string id,Mesh mesh,bool transparent)
        {
            var shader=Resources.Load<Shader>(transparent?"NativeSourceTransparent":"NativeSourceLit");if(shader==null){CapturedAnimation.Release(mesh);return null;}
            var material=new Material(shader){enableInstancing=true,name="Kill effect "+id};material.SetInt("_Cull",transparent?0:2);materials.Add(material);
            var part=new Part{Name=id,Bone=-1,Mesh=mesh,Material=material,NoShadow=true};models[id]=new[]{part};return part;
        }
        private void Draw(Part part,Matrix4x4 matrix,Vector4 body,Color tint,Vector4 glow)=>Draw(part,matrix,body,tint,glow,Vector4.one);
        private void Draw(Part part,Matrix4x4 matrix,Vector4 body,Color tint,Vector4 glow,Vector4 accent)
        {
            var key=(part,0);if(!draws.TryGetValue(key,out var draw)){draw=new Draws{Part=part};draws[key]=draw;}
            draw.Matrices.Add(matrix);draw.Bodies.Add(body);draw.Accents.Add(accent);draw.Tints.Add(tint);draw.Glows.Add(glow);
        }
        /// <summary>A model's meshes as <see cref="Queue"/> draws them, but casting no shadow (a ragdoll's copied parts).</summary>
        public void QueueShadowless(string id,Matrix4x4 root,Func<int,Matrix4x4> bone,Func<int,bool> visible,Color? body,Color? accent,float opacity)
        {
            if(id==null||!models.TryGetValue(id,out var model))return;Color bc=body??Color.white,ac=accent??Color.white;
            if(shadowlessParts.Count>512)PruneRecolor();
            foreach(var part in model)
            {
                if(visible!=null&&!visible(part.Bone))continue;
                Draw(Shadowless(part),part.Bone>=0&&bone!=null?bone(part.Bone):root,new Vector4(bc.r,bc.g,bc.b,opacity),Color.white,Vector4.one,new Vector4(ac.r,ac.g,ac.b,1));
            }
        }
        private Part Shadowless(Part part)
        {
            if(part.NoShadow)return part;if(!shadowlessParts.TryGetValue(part,out var twin)){twin=new Part{Name=part.Name,Bone=part.Bone,Mesh=part.Mesh,Material=part.Material,NoShadow=true};shadowlessParts[part]=twin;}
            return twin;
        }
        /// <summary>A stump cap cell: the wound cube (lit, wound glow .3) without a shadow.</summary>
        public void KillCap(Matrix4x4 matrix,Color color,float opacity)
        {
            if(killCap==null)killCap=KillPart("kill-cap",Cube().Mesh("Kill cap",Color.white,new Vector4(.3f,.3f,.3f,0)),false);
            if(killCap!=null)Draw(killCap,matrix,new Vector4(1,1,1,opacity),color,Vector4.one);
        }
        /// <summary>A lit cube without emission (a voxel-burst chunk).</summary>
        public void KillCube(Matrix4x4 matrix,Color albedo)
        {
            if(killCube==null)killCube=KillPart("kill-cube",Cube().Mesh("Kill cube",Color.white,new Vector4(1,1,1,0)),false);
            if(killCube!=null)Draw(killCube,matrix,Vector4.one,albedo,NoGlow);
        }
        /// <summary>A translucent plain box with its own emissive (a Soul Drift part). False when the ghost shader is missing.</summary>
        public bool KillGhost(Matrix4x4 matrix,Color albedo,Color emission,float opacity)
        {
            if(killGhost==null)
            {
                if(!EnsureKillGhost())return false;var mesh=Cube().Mesh("Kill ghost box",Color.white,new Vector4(1,1,1,0));
                killGhost=new Part{Name="kill-ghost",Bone=-1,Mesh=mesh,Material=killGhostMaterial,NoShadow=true};models["kill-ghost"]=new[]{killGhost};
            }
            // KillGhost.shader: emission = vertex emission (1) * glow tint.
            Draw(killGhost,matrix,new Vector4(1,1,1,opacity),albedo,new Vector4(emission.r,emission.g,emission.b,1));return true;
        }
        /// <summary>A model's meshes translucent with a flat albedo, each keeping its own emission (a Soul Drift hat or weapon
        /// with round meshes). False only when the ghost shader is unavailable (a missing model draws nothing).</summary>
        public bool QueueGhost(string id,Matrix4x4 root,Func<int,Matrix4x4> bone,Color albedo,float opacity)
        {
            if(!EnsureKillGhost())return false;if(id==null||!models.TryGetValue(id,out var model))return true;
            if(ghostParts.Count>512)PruneRecolor();
            foreach(var part in model)
            {
                var twin=Ghost(part);if(twin==null)continue;var matrix=part.Bone>=0&&bone!=null?bone(part.Bone):root;
                Draw(twin,matrix,new Vector4(1,1,1,opacity),albedo,Vector4.one);
            }
            return true;
        }
        private bool EnsureKillGhost()
        {
            if(killGhostMaterial!=null)return true;if(killGhostFailed)return false;var shader=Resources.Load<Shader>("NativeKillGhost");if(shader==null||!shader.isSupported){killGhostFailed=true;return false;}
            killGhostMaterial=new Material(shader){enableInstancing=true,name="Kill ghost"};killGhostMaterial.SetInt("_Cull",2);materials.Add(killGhostMaterial);return true;
        }
        private Part Ghost(Part part)
        {
            if(ghostParts.TryGetValue(part,out var twin))return twin;if(part.Material==null||part.Mesh==null){ghostParts[part]=null;return null;}
            if(!ghostMaterials.TryGetValue(part.Material,out var material))
            {
                material=new Material(killGhostMaterial){name="Kill ghost "+part.Material.name};material.mainTexture=part.Material.mainTexture;material.mainTextureScale=part.Material.mainTextureScale;
                if(part.Material.HasProperty("_Cull"))material.SetInt("_Cull",part.Material.GetInt("_Cull"));materials.Add(material);ghostMaterials[part.Material]=material;
            }
            twin=new Part{Name=part.Name,Bone=part.Bone,Mesh=part.Mesh,Material=material,NoShadow=true};ghostParts[part]=twin;return twin;
        }
        /// <summary>A blood drop (unit sphere, 5 x 3 segments); glow .3 for the wound material, 0 for the Gibber's plain red.</summary>
        public void KillDrop(Matrix4x4 matrix,Color albedo,float glow)
        {
            if(killDrop==null){var b=new Builder();Sphere(5,3,b.P,b.N,b.I);killDrop=KillPart("kill-drop",b.Mesh("Kill drop",Color.white,new Vector4(1,1,1,0)),false);}
            if(killDrop!=null)Draw(killDrop,matrix,Vector4.one,albedo,new Vector4(glow,glow,glow,1));
        }
        /// <summary>A splat disk (CircleGeometry(1,12) in its XY plane), translucent.</summary>
        public void KillPool(Matrix4x4 matrix,Color albedo,float opacity)
        {
            if(killPool==null)
            {
                var b=new Builder();b.Vertex(0,0,0,0,0,1);for(int s=0;s<=12;s++){float a=s/12f*Mathf.PI*2;b.Vertex(Mathf.Cos(a),Mathf.Sin(a),0,0,0,1);}for(int i=1;i<=12;i++)b.Triangle(i,i+1,0);
                killPool=KillPart("kill-pool",b.Mesh("Kill splat",Color.white,Vector4.zero),true);
            }
            if(killPool!=null)Draw(killPool,matrix,new Vector4(1,1,1,opacity),albedo,NoGlow);
        }
        /// <summary>The model's opaque meshes with their albedo replaced and their emission set (Frostbreak); see-through
        /// sections keep their own colour. False when the recolour shader is unavailable (the caller draws the model as is).</summary>
        public bool QueueRecolor(string id,Matrix4x4 root,Func<int,Matrix4x4> bone,Func<int,bool> visible,Color albedo,Color emission,float opacity=1)
        {
            if(id==null||!models.TryGetValue(id,out var model)||!EnsureKillBody())return false;
            if(recolorParts.Count>512||shadowlessParts.Count>512)PruneRecolor();
            foreach(var part in model)
            {
                if(visible!=null&&!visible(part.Bone))continue;var matrix=part.Bone>=0&&bone!=null?bone(part.Bone):root;
                var twin=Recolor(part);if(twin==null){Draw(Shadowless(part),matrix,new Vector4(1,1,1,opacity),Color.white,Vector4.one);continue;}
                Draw(twin,matrix,new Vector4(1,1,1,opacity),albedo,new Vector4(emission.r,emission.g,emission.b,1));
            }
            return true;
        }
        private bool EnsureKillBody()
        {
            if(killBody!=null)return true;if(killBodyFailed)return false;var shader=Resources.Load<Shader>("NativeKillBody");if(shader==null||!shader.isSupported){killBodyFailed=true;return false;}
            killBody=new Material(shader){enableInstancing=true,name="Kill recolour"};materials.Add(killBody);
            // The export's shell and accent colours (createCharacterRig('#70e1d0','#e5e7eb')): a shell vertex's colour divided by
            // them is its voxel shade.
            NativeKillEffectRules.Linear(0xe5e7eb,out double sr,out double sg,out double sb);NativeKillEffectRules.Linear(0x70e1d0,out double ar,out double ag,out double ab);
            killBody.SetVector("_ShellSource",new Vector4((float)sr,(float)sg,(float)sb,1));killBody.SetVector("_AccentSource",new Vector4((float)ar,(float)ag,(float)ab,1));return true;
        }
        private Part Recolor(Part part)
        {
            if(recolorParts.TryGetValue(part,out var twin))return twin;
            if(part.Material==null||part.Material.renderQueue>=(int)RenderQueue.Transparent||part.Material.shader!=null&&part.Material.shader.name=="ChipArena/SourceTransparent"){recolorParts[part]=null;return null;}
            if(!recolorMaterials.TryGetValue(part.Material,out var material))
            {
                material=new Material(killBody){name="Kill recolour "+part.Material.name};material.mainTexture=part.Material.mainTexture;material.mainTextureScale=part.Material.mainTextureScale;
                if(part.Material.HasProperty("_Cull"))material.SetInt("_Cull",part.Material.GetInt("_Cull"));materials.Add(material);recolorMaterials[part.Material]=material;
            }
            twin=new Part{Name=part.Name,Bone=part.Bone,Mesh=part.Mesh,Material=material,NoShadow=true};recolorParts[part]=twin;return twin;
        }
        // Wound-carved pilots are rebuilt and released (NativeWoundGeometry); forget twins of released meshes.
        private void PruneRecolor(){Prune(recolorParts);Prune(ghostParts);Prune(shadowlessParts);}
        private void Prune(Dictionary<Part,Part> twins)
        {
            recolorStale.Clear();foreach(var pair in twins)if(pair.Key.Mesh==null)recolorStale.Add(pair.Key);
            foreach(var part in recolorStale){var twin=twins[part];twins.Remove(part);if(twin!=null)draws.Remove((twin,0));}
        }
    }
}
