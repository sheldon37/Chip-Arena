using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Headwear colours and the Sheldon Head in the shared model bank (Core/NativeHatLooks.cs, Core/NativeSheldonHead.cs).
    //  - Each recolourable hat gets one tintable twin, "hat-<id>+tint", built once from the same exported arrays: its main
    //    vertices carry the body tag (emission.w 1) with their brightness relative to the main colour, everything else is
    //    copied as is. Queued with the picked colour as the body tint it shows that colour with its details kept. Auto never
    //    touches it: the authored "hat-<id>" is queued exactly as before.
    //  - The Sheldon Head is built once from NativeSheldonHead.Build (x mirrored and the winding turned over, as the export
    //    converts the source models): "hat-sheldon-head" has every part at rest (every place a hat is drawn: corpses, death
    //    scenes, ghosts, holograms, the chip draft, revives), "hat-sheldon-head+live" only its shell, for the live arena rig and
    //    the portraits, which draw the face and the ring as animated pieces (QueueSheldonPieces, SheldonPiece).
    public sealed partial class NativeSourceModels
    {
        private Part[] sheldonParts;
        // The tintable twin of a recolourable hat, from the export's arrays (the bank's meshes are uploaded non-readable).
        private void HatTwin(SourceModel model,List<Part> parts)
        {
            if(model.id==null||!model.id.StartsWith("hat-",StringComparison.Ordinal))return;string hat=model.id.Substring(4);if(NativeHatLooks.Mains(hat).Length==0)return;
            var twins=new List<Part>();int k=0;
            foreach(var s in model.sections)
            {
                int count=s.positions.Length/3;if(count==0)continue;var source=parts[k++];
                var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var emission=new List<Vector4>(count);var uv=new Vector2[count];
                for(int i=0;i<count;i++)
                {
                    vertices[i]=new Vector3(s.positions[i*3],s.positions[i*3+1],s.positions[i*3+2]);normals[i]=new Vector3(s.normals[i*3],s.normals[i*3+1],s.normals[i*3+2]);uv[i]=new Vector2(s.uv[i*2],s.uv[i*2+1]);
                    float r=s.colors[i*4],g=s.colors[i*4+1],b=s.colors[i*4+2],a=s.colors[i*4+3];var e=new Vector4(s.emission[i*4],s.emission[i*4+1],s.emission[i*4+2],s.emission[i*4+3]);
                    if(NativeHatLooks.IsMain(hat,r,g,b,out float level)){float el=NativeHatLooks.EmissionLevel(hat,e.x,e.y,e.z);colors[i]=new Color(level,level,level,a);emission.Add(new Vector4(el,el,el,1));}
                    else{colors[i]=new Color(r,g,b,a);emission.Add(e);}
                }
                var mesh=new Mesh{name=model.id+NativeHatLooks.TintSuffix+" / joint "+s.bone,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.triangles=s.indices;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                twins.Add(new Part{Name=source.Name,Bone=source.Bone,Mesh=mesh,Material=source.Material,Core=CoreOf(vertices,normals,colors,emission,s.indices),Open=source.Open});
            }
            models[model.id+NativeHatLooks.TintSuffix]=twins.ToArray();
        }
        // The Sheldon Head's parts: the bank's lit shader for the opaque parts, its transparent one (unlit) for the ring's glow.
        private void SheldonModels()
        {
            var sections=NativeSheldonHead.Build();sheldonParts=new Part[sections.Length];
            var lit=Resources.Load<Shader>("NativeSourceLit");var clear=Resources.Load<Shader>("NativeSourceTransparent");if(lit==null||clear==null)throw new InvalidOperationException("Source material shader is missing");
            var all=new List<Part>();var shell=new List<Part>();
            for(int p=0;p<sections.Length;p++)
            {
                var part=SheldonPart(sections[p],lit,clear,NativeSheldonHead.Animated(p)&&p!=NativeSheldonHead.Ring);
                sheldonParts[p]=part;all.Add(part);if(!NativeSheldonHead.Animated(p))shell.Add(part);
            }
            models["hat-"+NativeSheldonHead.Hat]=all.ToArray();models[SheldonLive]=shell.ToArray();
            // On the model the hood is cloth (Core/NativeSheldonHood.cs, Runtime/NativeSheldonHoodPresentation.cs): its face's void
            // is a plate shaped to that hood's opening.
            models[SheldonHoodVoid]=new[]{SheldonPart(NativeSheldonHood.Void(),lit,clear,false)};
        }
        // One Sheldon section as a bank part (x mirrored and the winding turned over, as the export converts the source models).
        private Part SheldonPart(SheldonSection s,Shader lit,Shader clear,bool noShadow)
        {
            int n=s.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);
            for(int i=0;i<n;i++){var v=s.Positions[i];var nn=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(-v.X,v.Y,v.Z);normals[i]=new Vector3(-nn.X,nn.Y,nn.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
            var t=new int[s.Indices.Count];for(int i=0;i+2<t.Length;i+=3){t[i]=s.Indices[i];t[i+1]=s.Indices[i+2];t[i+2]=s.Indices[i+1];}
            var mesh=new Mesh{name="hat-sheldon-head / "+s.Part};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=new Vector2[n];mesh.SetUVs(1,emission);mesh.triangles=t;mesh.RecalculateBounds();mesh.UploadMeshData(true);
            var material=new Material(s.Transparent?clear:lit){enableInstancing=true,name="Sheldon Head "+s.Part};material.SetInt("_Cull",s.DoubleSide?0:2);material.SetFloat("_Ink",0);if(s.Transparent)material.SetFloat("_Unlit",1);materials.Add(material);
            return new Part{Name=s.Part,Bone=s.Bone,Mesh=mesh,Material=material,NoShadow=s.Transparent||noShadow,Core=CoreOf(vertices,normals,colors,emission,t),Open=s.Transparent||s.DoubleSide};
        }
        public const string SheldonLive="hat-"+NativeSheldonHead.Hat+NativeHatLooks.LiveSuffix,SheldonHoodVoid="hat-"+NativeSheldonHead.Hat+"+hood";
        // The hats made for the model's head (Core/NativeModelHatMeshes), each with its tintable twin: "hat-<id>+model" and
        // "hat-<id>+model+tint", authored in the model head bone's own frame (no mirror: their faces already face out).
        public const string ModelSuffix="+model";
        private void ModelHats()
        {
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)return;
            foreach(var hat in NativeModelHatMeshes.Hats)
                foreach(bool tinted in new[]{false,true})
                {
                    var sections=NativeModelHatMeshes.Build(hat,tinted);var parts=new Part[sections.Length];
                    for(int p=0;p<sections.Length;p++)parts[p]=ModelHatPart(sections[p],lit,hat);
                    models["hat-"+hat+ModelSuffix+(tinted?NativeHatLooks.TintSuffix:"")]=parts;
                }
        }
        private Part ModelHatPart(SheldonSection s,Shader lit,string hat)
        {
            int n=s.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);
            for(int i=0;i<n;i++){var v=s.Positions[i];var nn=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(v.X,v.Y,v.Z);normals[i]=new Vector3(nn.X,nn.Y,nn.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
            var t=s.Indices.ToArray();
            var mesh=new Mesh{name="hat-"+hat+ModelSuffix+" / "+s.Part,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=new Vector2[n];mesh.SetUVs(1,emission);mesh.triangles=t;mesh.RecalculateBounds();mesh.UploadMeshData(true);
            var material=new Material(lit){enableInstancing=true,name="Model hat "+hat};material.SetInt("_Cull",2);materials.Add(material);
            return new Part{Name=s.Part,Bone=s.Bone,Mesh=mesh,Material=material,Core=CoreOf(vertices,normals,colors,emission,t)};
        }
        // A face or ring piece's matrix on the head joint (Unity frame: the source motion mirrored in x).
        private static Matrix4x4 PieceLocal(int part,in SheldonFace face)
        {
            var m=NativeSheldonHead.Piece(part,face);var pivot=new Vector3(-m.Pivot.X,m.Pivot.Y,m.Pivot.Z);var offset=new Vector3(-m.Offset.X,m.Offset.Y,m.Offset.Z);
            return Matrix4x4.Translate(pivot+offset)*Matrix4x4.Rotate(Quaternion.Euler(0,0,-m.Spin*Mathf.Rad2Deg))*Matrix4x4.Scale(new Vector3(m.Sx,m.Sy,1))*Matrix4x4.Translate(-pivot);
        }
        /// <summary>
        /// The Sheldon Head's animated face and ring on `head` (the head joint's world matrix) with `glow` as the body tint,
        /// faded by `opacity`, the face's brightness from `face.Glow`; `scene` is the rig's part tint on the head (Chips, block).
        /// </summary>
        public void QueueSheldonPieces(Matrix4x4 head,in SheldonFace face,Color glow,int layer,Camera camera,float opacity,float hitFlash,Color scene)
        {
            if(sheldonParts==null)return;
            for(int p=NativeSheldonHead.EyeLeft;p<=NativeSheldonHead.RingGlow;p++)
            {
                var part=sheldonParts[p];var d=DrawsFor(part,layer,camera);
                float k=p<NativeSheldonHead.Ring?face.Glow:1;
                d.Matrices.Add(head*PieceLocal(p,face));d.Bodies.Add(new Vector4(glow.r,glow.g,glow.b,opacity));d.Accents.Add(new Vector4(1,1,1,hitFlash));d.Tints.Add(scene);d.Glows.Add(new Vector4(k,k,k,1));
            }
        }
        /// <summary>For the portraits: an animated piece's mesh, its matrix on the head joint and its material.</summary>
        public bool SheldonPiece(int part,in SheldonFace face,out Mesh mesh,out Matrix4x4 local,out Material material)
        {
            mesh=null;material=null;local=Matrix4x4.identity;if(sheldonParts==null||!NativeSheldonHead.Animated(part))return false;
            var p=sheldonParts[part];mesh=p.Mesh;material=p.Material;local=PieceLocal(part,face);return true;
        }
    }

    public sealed partial class ArenaPresentation
    {
        // The bank model and body tint a pilot's hat is queued with (NativeHatLooks): on Auto the authored model and no tint,
        // so the call is exactly the one made before colours existed. Model ids are cached (no per-frame strings).
        private readonly Dictionary<(string,string),string> hatModels=new Dictionary<(string,string),string>();
        // On the model's head the hat made for it, where there is one (its own fit: VisualHatFit).
        private string VisualHatModel(string hat,string color)
        {
            string id=HatModel(hat,color);if(id==null||!NativeModelHatMeshes.Has(hat)||sourceModels==null)return id;var key=(hat,"model:"+(color??""));
            if(!hatModels.TryGetValue(key,out var model)){model="hat-"+hat+NativeSourceModels.ModelSuffix+(id.EndsWith(NativeHatLooks.TintSuffix,StringComparison.Ordinal)?NativeHatLooks.TintSuffix:"");if(!sourceModels.Has(model))model=id;hatModels[key]=model;}
            return model;
        }
        private string HatModel(string hat,string color)
        {
            if(string.IsNullOrEmpty(hat))return null;color=color??"";var key=(hat,color);
            if(!hatModels.TryGetValue(key,out var id)){if(hatModels.Count>256)hatModels.Clear();id=NativeHatLooks.Model(hat,color);hatModels[key]=id;}
            return id;
        }
        // The Sheldon Head's parts by joint: the hood (head) and everything else (the mantle and drapes on the chest and clavicles).
        // Every other hat is all on the head, so a hood-only filter draws it whole.
        private static readonly Func<int,bool> SheldonHood=b=>b==CapturedRig.Head,SheldonBody=b=>b!=CapturedRig.Head;
        private static Color? HatBody(string hat,string color){int t=NativeHatLooks.Tint(hat,color);if(t<0)return null;var c=NativePortraitRules.Linear(t);return new Color(c.X,c.Y,c.Z,1);}

        // The Sheldon Head's expression inputs: the last hit (the hit flash's record) and the last kill each pilot scored.
        private readonly Dictionary<int,float> hatKills=new Dictionary<int,float>();private int hatKillEvent;
        private void BeginHatFx(Snapshot s)
        {
            if(s?.Events==null)return;int newest=0;foreach(var e in s.Events)newest=Math.Max(newest,e.Id);if(newest<hatKillEvent){hatKillEvent=0;hatKills.Clear();}
            foreach(var e in s.Events)if(e.Id>hatKillEvent&&e.Kind==EventKind.Death&&e.Source>0&&e.Source!=e.Target){if(!hatKills.TryGetValue(e.Source,out var at)||e.Time>=at)hatKills[e.Source]=e.Time;}
            hatKillEvent=Math.Max(hatKillEvent,newest);
        }
        private float SheldonHitAge(int id,float time)=>hitFlashes.TryGetValue(id,out var h)?time-h.At:-1;
        private float SheldonKillAge(int id,float time)=>hatKills.TryGetValue(id,out var at)?time-at:-1;
        /// <summary>
        /// The live Sheldon Head on an arena rig (third person, the killcam replay, entrances): its shell on the rig's joints and
        /// the face and ring animated (blinks, a flicker on hits, a laugh after a kill). False for any other hat (the caller
        /// queues the bank model, the Sheldon Head's included where no live rig exists).
        /// </summary>
        private bool SheldonHead(Actor a,Matrix4x4 root,Func<int,Matrix4x4> bone,float time,int layer,Camera camera,Func<int,Color> partTint,float flash,Func<int,bool> visible=null)
        {
            if(a.Hat!=NativeSheldonHead.Hat||sourceModels==null||!sourceModels.Has(NativeSourceModels.SheldonLive))return false;
            var glow=HatBody(a.Hat,a.HatColor)??Color.white;
            sourceModels.Queue(NativeSourceModels.SheldonLive,root,bone,visible,layer,camera,body:glow,partTint:partTint,hitFlash:flash);
            var face=sheldonScripted?sheldonScriptFace:NativeSheldonHead.Evaluate(time,a.Id,SheldonHitAge(a.Id,time),SheldonKillAge(a.Id,time),HaloReduced);
            sourceModels.QueueSheldonPieces(bone(CapturedRig.Head),face,glow,layer,camera,1,flash,partTint?.Invoke(CapturedRig.Head)??Color.white);
            return true;
        }
        /// <summary>The Sheldon Head on a menu rig (the home screen's rooftop pilot): its shell and the idle face and ring on
        /// `time` (blinks and breathing; no hit or kill there). False for any other hat.</summary>
        private bool SheldonStill(string hat,string color,Matrix4x4 root,Func<int,Matrix4x4> bone,Matrix4x4 head,float time,int seed,bool reduced,int layer,Camera camera)
        {
            if(hat!=NativeSheldonHead.Hat||sourceModels==null||!sourceModels.Has(NativeSourceModels.SheldonLive))return false;
            var glow=HatBody(hat,color)??Color.white;sourceModels.Queue(NativeSourceModels.SheldonLive,root,bone,null,layer,camera,body:glow);
            sourceModels.QueueSheldonPieces(head,NativeSheldonHead.Evaluate(time,seed,-1,-1,reduced),glow,layer,camera,1,1,Color.white);return true;
        }
    }
}
