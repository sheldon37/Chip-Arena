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
        /// <summary>A model's meshes as <see cref="Queue"/> draws them, but casting no shadow (a ragdoll's copied parts).
        /// `partOpacity`, when given, scales each joint's opacity (below 1 the body shader dithers it: a revive's skin solidifying).</summary>
        public void QueueShadowless(string id,Matrix4x4 root,Func<int,Matrix4x4> bone,Func<int,bool> visible,Color? body,Color? accent,float opacity,Func<int,float> partOpacity=null)
        {
            if(id==null||!models.TryGetValue(id,out var model))return;Color bc=body??Color.white,ac=accent??Color.white;
            if(shadowlessParts.Count>512)PruneRecolor();
            int torn=-1;
            foreach(var part in model)
            {
                if(visible!=null&&!visible(part.Bone))continue;
                // Only a part whose spill crosses a torn joint skips those boxes (NativeArmorLights.TornJoints).
                int severs=SeverJoints(part.Material);if(severs!=0&&torn<0)torn=bone!=null?TornJoints(bone,visible):0;
                float shown=partOpacity==null?opacity:opacity*partOpacity(part.Bone);if(partOpacity!=null&&shown<=.002f)continue;
                Draw(Shadowless(part),part.Bone>=0&&bone!=null?bone(part.Bone):root,new Vector4(bc.r,bc.g,bc.b,shown),Color.white,(severs&torn)!=0?CorpseGlow:Vector4.one,new Vector4(ac.r,ac.g,ac.b,1));
            }
        }
        private Part Shadowless(Part part)
        {
            if(part.NoShadow)return part;if(!shadowlessParts.TryGetValue(part,out var twin)){twin=new Part{Name=part.Name,Bone=part.Bone,Mesh=part.Mesh,Material=part.Material,NoShadow=true};shadowlessParts[part]=twin;}
            return twin;
        }
        /// <summary>A stump cap cell (lit, wound glow .3, no shadow): since 2026-11-09 (realistic blood) a soft wet lump a little wider
        /// than its cell, so the cells run together into a torn surface instead of a grid of cubes. With `flesh` (owner 2026-11-13:
        /// "match the quality with our blood") it is drawn as wet tissue (Resources/NativeFlesh.shader) in the cell's colour.</summary>
        public void KillCap(Matrix4x4 matrix,Color color,float opacity,bool flesh=false)
        {
            if(flesh&&FleshReady())
            {
                if(fleshCap==null){var b=new Builder();Sphere(10,7,b.P,b.N,b.I);for(int i=0;i<b.P.Count;i++)b.P[i]*=.62f;fleshCap=FleshPart("flesh-cap",b.Mesh("Flesh cap",Color.white,Vector4.zero),1,false,2);}
                if(fleshCap!=null){Draw(fleshCap,matrix,new Vector4(1,1,1,opacity),color,Vector4.one);return;}
            }
            if(killCap==null){var b=new Builder();Sphere(10,7,b.P,b.N,b.I);for(int i=0;i<b.P.Count;i++)b.P[i]*=.62f;killCap=KillPart("kill-cap",b.Mesh("Kill cap",Color.white,new Vector4(.3f,.3f,.3f,0)),false);}
            if(killCap!=null)Draw(killCap,matrix,new Vector4(1,1,1,opacity),color,Vector4.one);
        }
        private readonly Part[] killChunks=new Part[3];
        /// <summary>A crushed part's fragment (CrushDebris; it casts a shadow): one of three rounded, torn lumps (a sphere pushed in and out
        /// by its own noise), about the size of the cube it replaces. `variant` picks the lump. With `flesh` it is one of five
        /// torn lumps of wet tissue, or with `bone` one of three shards of bone (FleshLump, BoneShard).</summary>
        public void KillChunk(Matrix4x4 matrix,Color color,float opacity,int variant,bool flesh=false,bool bone=false)
        {
            if(flesh&&FleshReady())
            {
                var list=bone?boneShards:fleshLumps;int f=((variant%list.Length)+list.Length)%list.Length;
                if(list[f]==null)list[f]=FleshPart((bone?"bone-shard-":"flesh-lump-")+f,bone?BoneShard(f):FleshLump(f),bone?2:1,true,bone?0:2);
                if(list[f]!=null){Draw(list[f],matrix,new Vector4(1,1,1,opacity),color,Vector4.one);return;}
            }
            int v=((variant%3)+3)%3;
            if(killChunks[v]==null)
            {
                var b=new Builder();Sphere(9,6,b.P,b.N,b.I);uint seed=(uint)(v*7919+17);
                for(int i=0;i<b.P.Count;i++){var p=b.P[i];float n=Mathf.Sin(p.x*5.1f+v*1.7f)*Mathf.Sin(p.y*4.3f+v)*Mathf.Sin(p.z*4.7f+v*2.3f);b.P[i]=p*(.55f+.16f*n+.06f*Mathf.Sin(p.x*11+p.z*9+v));}
                // Its normals from its own faces (the mesh is uploaded unreadable), turned outward.
                var acc=new Vector3[b.P.Count];for(int t=0;t+2<b.I.Count;t+=3){int i0=b.I[t],i1=b.I[t+1],i2=b.I[t+2];var fn=Vector3.Cross(b.P[i1]-b.P[i0],b.P[i2]-b.P[i0]);acc[i0]+=fn;acc[i1]+=fn;acc[i2]+=fn;}
                for(int i=0;i<b.P.Count;i++){var n=acc[i].sqrMagnitude>1e-12f?acc[i].normalized:b.P[i].normalized;b.N[i]=Vector3.Dot(n,b.P[i])<0?-n:n;}
                var mesh=b.Mesh("Kill chunk "+v,Color.white,new Vector4(.15f,.15f,.15f,0));
                killChunks[v]=KillPart("kill-chunk-"+v,mesh,false);if(killChunks[v]!=null)killChunks[v].NoShadow=false;
            }
            if(killChunks[v]!=null)Draw(killChunks[v],matrix,new Vector4(1,1,1,opacity),color,Vector4.one);
        }
        // ---- flesh and bone (owner 2026-11-13: "Using higher quality bone structure and flesh"; Resources/NativeFlesh.shader) ---
        // The lumps and shards are made once each and drawn through the same instanced batches as the pieces above, so a gib
        // makes nothing per frame.
        private Part fleshCap;private readonly Part[] fleshLumps=new Part[5],boneShards=new Part[3];private Shader fleshShader;private bool fleshMissing;
        private bool FleshReady(){if(fleshShader==null&&!fleshMissing){fleshShader=Resources.Load<Shader>("NativeFlesh");if(fleshShader==null||!fleshShader.isSupported){fleshShader=null;fleshMissing=true;}}return fleshShader!=null;}
        // (A shard is drawn from both sides: its broken ends are open, and the shader lights whichever face shows.)
        private Part FleshPart(string id,Mesh mesh,int mode,bool shadow,int cull)
        {
            var material=new Material(fleshShader){enableInstancing=true,name="Gore "+id};material.SetInt("_Cull",cull);material.SetFloat("_Mode",mode);materials.Add(material);
            var part=new Part{Name=id,Bone=-1,Mesh=mesh,Material=material,NoShadow=!shadow};models[id]=new[]{part};return part;
        }
        private static float GoreHash(int x,int y,int z,int seed){uint h=(uint)(x*374761393+y*668265263+z*2147483647+seed*362437);h=(h^(h>>13))*1274126177u;h^=h>>16;return (h&0xffffff)/16777215f;}
        private static float GoreNoise(Vector3 p,int seed)
        {
            int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);float fx=p.x-x,fy=p.y-y,fz=p.z-z;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);fz=fz*fz*(3-2*fz);
            float a=Mathf.Lerp(Mathf.Lerp(GoreHash(x,y,z,seed),GoreHash(x+1,y,z,seed),fx),Mathf.Lerp(GoreHash(x,y+1,z,seed),GoreHash(x+1,y+1,z,seed),fx),fy);
            float b=Mathf.Lerp(Mathf.Lerp(GoreHash(x,y,z+1,seed),GoreHash(x+1,y,z+1,seed),fx),Mathf.Lerp(GoreHash(x,y+1,z+1,seed),GoreHash(x+1,y+1,z+1,seed),fx),fy);
            return Mathf.Lerp(a,b,fz);
        }
        // Smooth normals from the faces, shared by vertices at one place (a sphere's seam and poles), turned out from `inside`.
        private static void GoreNormals(Builder b,Func<Vector3,Vector3> inside)
        {
            var acc=new Dictionary<(int,int,int),Vector3>();(int,int,int) Key(Vector3 p)=>(Mathf.RoundToInt(p.x*4096),Mathf.RoundToInt(p.y*4096),Mathf.RoundToInt(p.z*4096));
            for(int t=0;t+2<b.I.Count;t+=3){Vector3 p0=b.P[b.I[t]],p1=b.P[b.I[t+1]],p2=b.P[b.I[t+2]];var fn=Vector3.Cross(p1-p0,p2-p0);if(fn.sqrMagnitude<1e-16f)continue;var mid=(p0+p1+p2)/3;if(Vector3.Dot(fn,mid-inside(mid))<0)fn=-fn;foreach(var p in new[]{p0,p1,p2}){acc.TryGetValue(Key(p),out var sum);acc[Key(p)]=sum+fn;}}
            for(int i=0;i<b.P.Count;i++)b.N[i]=acc.TryGetValue(Key(b.P[i]),out var n)&&n.sqrMagnitude>1e-16f?n.normalized:(b.P[i]-inside(b.P[i])).normalized;
        }
        // Each face wound so its front (Unity's clockwise) looks away from `inside`.
        private static void GoreWind(Builder b,Func<Vector3,Vector3> inside)
        {
            for(int t=0;t+2<b.I.Count;t+=3){Vector3 p0=b.P[b.I[t]],p1=b.P[b.I[t+1]],p2=b.P[b.I[t+2]];var mid=(p0+p1+p2)/3;if(Vector3.Dot(Vector3.Cross(p1-p0,p2-p0),mid-inside(mid))<0){int k=b.I[t+1];b.I[t+1]=b.I[t+2];b.I[t+2]=k;}}
        }
        /// <summary>A torn lump of flesh, about the size of the cube it replaces (a sphere pulled into its shape, then pushed in
        /// and out by two sizes of noise): 0 a clump, 1 a strip, 2 a flat slab, 3 two lobes, 4 a tapering shred.</summary>
        private static Mesh FleshLump(int v)
        {
            var b=new Builder();Sphere(14,9,b.P,b.N,b.I);
            var shape=v==0?new Vector3(.56f,.5f,.54f):v==1?new Vector3(.78f,.3f,.4f):v==2?new Vector3(.62f,.24f,.6f):v==3?new Vector3(.7f,.4f,.42f):new Vector3(.86f,.26f,.3f);
            for(int i=0;i<b.P.Count;i++)
            {
                var u=b.P[i];float big=GoreNoise(u*1.7f+Vector3.one*(v*3.1f),v)-.5f,small=GoreNoise(u*4.6f+Vector3.one*(v*5.3f),v+11)-.5f;
                float r=1+.5f*big+.2f*small;
                if(v==3)r*=.72f+.4f*Mathf.Abs(u.x);else if(v==4)r*=Mathf.Lerp(1.15f,.45f,(u.x+1)*.5f);else if(v==1)r*=1+.18f*Mathf.Sin(u.x*5);
                b.P[i]=Vector3.Scale(u,shape)*r;
            }
            GoreWind(b,_=>Vector3.zero);GoreNormals(b,_=>Vector3.zero);return b.Mesh("Flesh lump "+v,Color.white,Vector4.zero);
        }
        /// <summary>A shard of bone about the size of the cube it replaces, along x: 0 a shaft's end with its knuckle, broken off
        /// at the other end; 1 a long splinter; 2 a curved piece of rib. The broken ends are jagged and show the marrow (their
        /// vertex colour: the shader darkens the bone by it); a whole end is rounded.</summary>
        private static Mesh BoneShard(int v)
        {
            var b=new Builder();const int sides=9;int rings=v==2?9:7;var colors=new List<Color>();var marrow=new Color(.34f,.05f,.05f);
            Vector3 Axis(float t)=>v==2?new Vector3(Mathf.Sin((t-.5f)*1.5f)*.62f,(Mathf.Cos((t-.5f)*1.5f)-.86f)*.62f,0):new Vector3((t-.5f)*(v==1?1.5f:1.2f),0,0);
            float Radius(float t)=>v==0?Mathf.Lerp(.15f,.13f,t)+.2f*Mathf.Exp(-t*t*30):v==1?Mathf.Lerp(.02f,.13f,Mathf.Sin(t*Mathf.PI)):.085f+.02f*Mathf.Sin(t*9);
            for(int r=0;r<rings;r++)
            {
                float t=r/(float)(rings-1);var at=Axis(t);var along=(Axis(Mathf.Min(1,t+.01f))-Axis(Mathf.Max(0,t-.01f))).normalized;var side=Vector3.Cross(along,Vector3.forward).normalized;var up=Vector3.Cross(side,along);
                bool broken=r==rings-1||v==2&&r==0;
                for(int k=0;k<sides;k++)
                {
                    float a=k*2*Mathf.PI/sides,rad=Radius(t)*(v==2?1:1+.12f*(GoreHash(k,r,v,5)-.5f));float flat=v==2?.55f:v==1?.6f:1;
                    var p=at+(side*Mathf.Cos(a)+up*Mathf.Sin(a)*flat)*rad;if(broken)p+=along*((r==0?-1:1)*.16f*GoreHash(k,r,v,9));
                    b.P.Add(p);b.N.Add(Vector3.up);colors.Add(Color.white);
                }
            }
            for(int r=0;r<rings-1;r++)for(int k=0;k<sides;k++){int a=r*sides+k,c=r*sides+(k+1)%sides,d=a+sides,e=c+sides;b.I.AddRange(new[]{a,d,c,c,d,e});}
            // The ends: a rounded knuckle closes whole; a broken end closes on its marrow, sunk a little into the shaft.
            for(int end=0;end<2;end++)
            {
                int ring=end==0?0:(rings-1)*sides;bool broken=end==1||v==2;float t=end;var along=(Axis(Mathf.Min(1,t+.01f))-Axis(Mathf.Max(0,t-.01f))).normalized*(end==0?-1:1);
                var mid=Vector3.zero;for(int k=0;k<sides;k++)mid+=b.P[ring+k];mid/=sides;mid+=along*(broken?-.05f:Radius(t)*.6f);
                int first=b.P.Count;
                if(broken)for(int k=0;k<sides;k++){b.P.Add(Vector3.Lerp(b.P[ring+k],mid,.3f)-along*.03f);b.N.Add(along);colors.Add(marrow);}
                else for(int k=0;k<sides;k++){b.P.Add(Vector3.Lerp(b.P[ring+k],mid,.45f)+along*(Radius(t)*.12f));b.N.Add(along);colors.Add(Color.white);}
                int centre=b.P.Count;b.P.Add(mid);b.N.Add(along);colors.Add(broken?marrow:Color.white);
                for(int k=0;k<sides;k++)
                {
                    int a=ring+k,c=ring+(k+1)%sides;
                    int ia=first+k,ic=first+(k+1)%sides;b.I.AddRange(new[]{a,ia,c,c,ia,ic,ia,centre,ic});
                }
            }
            Vector3 Inside(Vector3 p){float best=float.MaxValue;var at=Vector3.zero;for(int k=2;k<=14;k++){var q=Axis(k/16f);float d=(q-p).sqrMagnitude;if(d<best){best=d;at=q;}}return at;}
            GoreWind(b,Inside);GoreNormals(b,Inside);
            var mesh=new Mesh{name="Bone shard "+v};mesh.SetVertices(b.P);mesh.SetNormals(b.N);mesh.SetColors(colors);mesh.uv=new Vector2[b.P.Count];mesh.SetTriangles(b.I,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
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
            // The export's shell and accent colours (createCharacterRig('#70e1d0','#e5e7eb',true): the glowing accent parts carry
            // ember('#70e1d0')): a shell vertex's colour divided by them is its voxel shade.
            NativeKillEffectRules.Linear(0xe5e7eb,out double sr,out double sg,out double sb);var accent=NativePortraitRules.ExportEmber;
            killBody.SetVector("_ShellSource",new Vector4((float)sr,(float)sg,(float)sb,1));killBody.SetVector("_AccentSource",new Vector4(accent.X,accent.Y,accent.Z,1));return true;
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
