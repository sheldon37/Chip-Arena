using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // The Standard weapon skins in the source-model bank (Core/NativeWeaponModels*.cs, Core/NativeWeaponSkins.cs). Each
    // designed weapon becomes three held models ("weapon-std-<spec>", "-reduced", "-low": the detail tiers) and one loose model
    // ("loose-std-<spec>", the dropped and thrown copy), converted from the source weapon frame as tools/export_models.mjs
    // converts the Prototype bake (x mirrored, each triangle turned over), on joints 9/13 like the exported weapons. They are
    // ordinary bank models drawn by NativeSourceLit, so the hit flash, the accent tint (emission tag 2), the armour-light cuff
    // spill on the hand-held kinds (NativeArmorLight.HandHeld), the ghosts, afterimages and portraits all work on them.
    // The reach stretch: a Standard blade section scales from its base and its rigid hilt keeps its length; every draw already
    // carries the Prototype's z scale by reach in its weapon matrix, so Queue reads the reach back from that matrix's z / x
    // scale ratio (bone matrices are otherwise uniformly scaled) and applies NativeWeaponModels.Stretch per section mesh.
    public sealed partial class NativeSourceModels
    {
        private readonly Dictionary<Mesh,(string spec,WeaponStretch kind)> weaponStretch=new Dictionary<Mesh,(string,WeaponStretch)>();
        /// <summary>The Standard models built at load (held tiers plus loose copies), for the build checks.</summary>
        public int StandardWeaponModels{get;private set;}
        private void WeaponSkins()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            Material Make(string name){var m=new Material(shader){name=name,enableInstancing=true};m.SetInt("_Cull",2);m.SetFloat("_Ink",0);materials.Add(m);return m;}
            var plain=Make("Native Standard weapon");Material heldLeft=null,heldRight=null;
            foreach(var spec in NativeWeaponSkins.Weapons)
            {
                if(!NativeWeaponModels.Designed(spec))continue;bool handHeld=armorPilot!=null&&NativeArmorLight.HandHeld(NativeWeaponSkins.PrototypeHeld(spec));
                for(int tier=0;tier<NativeWeaponModels.Tiers;tier++)
                {
                    var built=NativeWeaponModels.Build(spec,tier);var parts=new List<Part>();
                    foreach(var s in built.Sections)
                    {
                        var material=plain;
                        if(handHeld&&(s.Bone==CapturedRig.LHand||s.Bone==CapturedRig.RHand))
                        {
                            // The cuffed hand's spill reaches the held weapon (as ArmorLights does for the Prototype ids).
                            if(s.Bone==CapturedRig.LHand){if(heldLeft==null){heldLeft=Make("Native Standard weapon (left hand)");ArmorMaterial(heldLeft,NativeArmorLight.HeldPlan(armorPilot,CapturedRig.LHand));}material=heldLeft;}
                            else{if(heldRight==null){heldRight=Make("Native Standard weapon (right hand)");ArmorMaterial(heldRight,NativeArmorLight.HeldPlan(armorPilot,CapturedRig.RHand));}material=heldRight;}
                        }
                        var mesh=WeaponMeshOf(s,spec+" / tier "+tier+" / joint "+s.Bone);if(mesh==null)continue;
                        if(s.Stretch!=WeaponStretch.None)weaponStretch[mesh]=(spec,s.Stretch);
                        parts.Add(new Part{Name="std-"+spec,Bone=s.Bone,Mesh=mesh,Material=material});
                    }
                    models[NativeWeaponSkins.StandardModel(spec,tier)]=parts.ToArray();StandardWeaponModels++;
                }
                var loose=NativeWeaponModels.Loose(spec);var looseParts=new List<Part>();
                foreach(var s in loose.Sections){var mesh=WeaponMeshOf(s,spec+" / loose");if(mesh!=null)looseParts.Add(new Part{Name="std-loose-"+spec,Bone=-1,Mesh=mesh,Material=plain});}
                models[NativeWeaponSkins.StandardLoose(spec)]=looseParts.ToArray();StandardWeaponModels++;
            }
        }
        private static Mesh WeaponMeshOf(WeaponSection s,string name)
        {
            int count=s.Positions.Count;if(count==0)return null;
            var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var uv=new Vector2[count];var emission=new List<Vector4>(count);
            for(int i=0;i<count;i++)
            {
                var p=s.Positions[i];var n=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];
                vertices[i]=new Vector3(-p.X,p.Y,p.Z);normals[i]=new Vector3(-n.X,n.Y,n.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));
            }
            var triangles=new int[s.Indices.Count];for(int i=0;i+2<triangles.Length;i+=3){triangles[i]=s.Indices[i];triangles[i+1]=s.Indices[i+2];triangles[i+2]=s.Indices[i+1];}
            var mesh=new Mesh{name="Standard weapon "+name,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.triangles=triangles;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        /// <summary>A Standard section's draw matrix after the reach stretch (unchanged for every other part).</summary>
        private Matrix4x4 WeaponStretched(Mesh mesh,Matrix4x4 m)
        {
            if(!weaponStretch.TryGetValue(mesh,out var s))return m;
            float x=((Vector3)m.GetColumn(0)).magnitude,z=((Vector3)m.GetColumn(2)).magnitude;if(!(x>1e-6f))return m;
            if(!NativeWeaponModels.Stretch(s.spec,s.kind,z/x,out float scale,out float offset))return m;
            return m*Matrix4x4.Translate(new Vector3(0,0,offset))*Matrix4x4.Scale(new Vector3(1,1,scale));
        }
    }
}
