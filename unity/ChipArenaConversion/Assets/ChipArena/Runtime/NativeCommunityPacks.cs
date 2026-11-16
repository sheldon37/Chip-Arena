using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Each installed file is self-contained so the multiplayer revision covers every asset.
    [Serializable] internal sealed class CommunityPack
    {
        public int schema;
        public string id,version;
        public CommunityWeapon[] weapons;
        public CommunityArena[] maps;
        public ContentChip[] chips;
    }
    [Serializable] internal sealed class CommunityWeapon
    {
        public string id,name,category,baseWeapon,unique;
        public CommunityMotion[] animations;
    }
    [Serializable] internal sealed class CommunityMotion
    {
        public string action;
        public float seconds;
        public CommunityTrack[] tracks;
    }
    [Serializable] internal sealed class CommunityTrack
    {
        public string bone;
        public Quaternion[] rotations;
        [NonSerialized] public int index;
    }
    [Serializable] internal sealed class CommunityArena
    {
        public ContentMap map;
        public CommunityMesh[] meshes;
        public string prefab;
    }
    [Serializable] internal sealed class CommunityMesh
    {
        public string material;
        public Vector3[] vertices;
        public int[] triangles;
        public bool solid;
        [NonSerialized] public Mesh mesh;
    }

    public sealed class NativeCommunityPacks
    {
        private static NativeCommunityPacks active;
        private readonly Dictionary<string,CommunityMotion[]> motions=new Dictionary<string,CommunityMotion[]>();
        private readonly Dictionary<string,CommunityMesh[]> geometry=new Dictionary<string,CommunityMesh[]>();
        private readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        private readonly List<AssetBundle> bundles=new List<AssetBundle>();
        private readonly HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        private NativeCommunityPacks(){}
        private const int MaxBytes=16*1024*1024;
        private static readonly Regex Id=new Regex(@"\A[a-z][a-z0-9-]{0,19}\z");
        private static readonly string[] Actions={"idle","move","attack","reload","block","cast"};

        private static void Need(bool valid,string message)
        {
            if(!valid)throw new InvalidDataException(message);
        }
        private static bool Key(string value)=>value!=null&&Id.IsMatch(value);
        private static string Qualified(string pack,string id)=>"mod."+pack+"."+id;

        // A manager profile and a manual installation use exactly the same loader.
        public static ConversionContent Load(ConversionContent original,string root,out int count)
        {
            count=0;
            var next=new NativeCommunityPacks();
            var candidate=original.CopyCatalog();
            var receipts=new SortedDictionary<string,(CommunityPack pack,string dir,string digest)>(StringComparer.Ordinal);
            if(Directory.Exists(root))
            {
                Need((File.GetAttributes(root)&FileAttributes.ReparsePoint)==0,"Linked content root");
                var dirs=Directory.EnumerateDirectories(root).Take(513).ToList();
                Need(dirs.Count<=512,"Too many package directories");
                dirs.Insert(0,root);
                long bytes=0;
                foreach(string dir in dirs)
                {
                    if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;
                    string path=Path.Combine(dir,"chiparena.pack.json");
                    if(!File.Exists(path))continue;
                    Need((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Linked content file");
                    using(var stream=File.OpenRead(path))
                    {
                        long size=stream.Length;
                        Need(size>0&&size<=MaxBytes&&(bytes+=size)<=4L*MaxBytes&&receipts.Count<16,"Content pack size or count limit exceeded");
                        using(var reader=new StreamReader(stream))
                        {
                            string json=reader.ReadToEnd();
                            try
                            {
                                var pack=JsonUtility.FromJson<CommunityPack>(json);
                                next.ValidateHeader(pack);
                                receipts.Add(pack.id,(pack,dir,NativeMapRules.Revision(json)));
                            }
                            catch(Exception e){throw new InvalidDataException(Path.GetFileName(dir)+": "+e.Message,e);}
                        }
                    }
                }
            }
            // Catalog order controls seeded rolls as well as menus. Sort by manifest ID on every peer.
            var fingerprints=new List<string>();
            long bundleBytes=0;
            try
            {
                foreach(var pair in receipts)
                {
                    var entry=pair.Value;
                    try
                    {
                        AssetBundle bundle=null;
                        string bundleDigest="";
                        if(entry.pack.maps?.Any(m=>!string.IsNullOrEmpty(m?.prefab))==true)
                        {
                            string path=Path.Combine(entry.dir,"arena-assets.bundle");
                            Need(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Missing or linked arena-assets.bundle");
                            using(var stream=File.OpenRead(path))
                            {
                                long size=stream.Length;
                                Need(size>0&&size<=128L*1024*1024&&(bundleBytes+=size)<=512L*1024*1024,"AssetBundle budget exceeded");
                                using(var hash=SHA256.Create())bundleDigest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                                stream.Position=0;
                                // LoadFromMemory takes its own copy and leaves no open package file.
                                using(var reader=new BinaryReader(stream))bundle=AssetBundle.LoadFromMemory(reader.ReadBytes((int)size));
                            }
                            Need(bundle!=null,"Cannot load Windows AssetBundle");
                            next.bundles.Add(bundle);
                        }
                        next.Register(candidate,entry.pack,entry.digest,bundle);
                        fingerprints.Add(pair.Key+"="+entry.digest+"/"+bundleDigest);
                    }
                    catch(Exception e){throw new InvalidDataException(pair.Key+": "+e.Message,e);}
                }
                if(receipts.Count>0)
                {
                    candidate.Validate();
                    candidate.contentRevision=NativeMapRules.Revision(candidate.contentRevision+"|community-v1|"+string.Join("|",fingerprints));
                }
            }
            catch{next.Release();throw;}
            Clear();active=next;count=receipts.Count;
            return candidate;
        }
        private void ValidateHeader(CommunityPack p)
        {
            Need(p!=null&&p.schema==1&&Key(p.id)&&ids.Add(p.id),"Invalid schema or duplicate pack ID");
            Need(p.version!=null&&Regex.IsMatch(p.version,@"\A\d{1,4}\.\d{1,4}\.\d{1,4}\z"),"Expected major.minor.patch");
            Need((p.weapons?.Length??0)<=32&&(p.maps?.Length??0)<=16,"Too many maps or weapons");
            Need((p.weapons?.Length??0)+(p.maps?.Length??0)+(p.chips?.Length??0)>0,"Empty pack");
        }
        private void Register(ConversionContent data,CommunityPack p,string digest,AssetBundle bundle)
        {
            foreach(var w in p.weapons??Array.Empty<CommunityWeapon>())
            {
                Need(w!=null&&Key(w.id)&&!string.IsNullOrWhiteSpace(w.name)&&NativeMods.CreditValid(w.name),"Invalid weapon");
                var baseWeapon=data.Weapon(w.baseWeapon);
                Need(!baseWeapon.devOnly&&baseWeapon.combatBase==null&&baseWeapon.@class==w.category&&
                    (w.category=="melee"||w.category=="ranged"||w.category=="magic"),"Base weapon and category must match");
                string unique=null;
                if(!string.IsNullOrEmpty(w.unique))
                {
                    Need(Key(w.unique),"Invalid unique ID");
                    unique=w.unique==baseWeapon.id?w.unique:p.id+"."+w.unique;
                }
                string id=Qualified(p.id,w.id);
                Need(!data.weapons.Any(x=>x.id==id),"Duplicate weapon "+id);
                var clips=w.animations??Array.Empty<CommunityMotion>();
                Need(clips.Length<=Actions.Length&&clips.Select(x=>x.action).Distinct().Count()==clips.Length,"Duplicate animation action");
                foreach(var clip in clips)ValidateMotion(clip);
                motions.Add(id,clips);
                data.weapons=data.weapons.Concat(new[]{baseWeapon.Variant(id,w.name,unique)}).ToArray();
                data.weights=data.weights.Concat(new[]{new NumberValue{name=id,value=data.Weight(baseWeapon.id)}}).ToArray();
            }
            foreach(var arena in p.maps??Array.Empty<CommunityArena>())
            {
                var map=arena.map;
                Need(map!=null&&Key(map.id)&&!string.IsNullOrWhiteSpace(map.name)&&NativeMods.CreditValid(map.name),"Invalid arena");
                map.id=Qualified(p.id,map.id);map.unity=true;
                Need(!data.maps.Any(x=>x.id==map.id),"Duplicate arena "+map.id);
                Need(map.blocks!=null&&map.blocks.Length<=4096&&(map.segments?.Length??0)<=128&&(map.hazards?.Length??0)<=128&&
                    (map.nav?.nodes?.Length??0)<=4096&&(map.nav?.links?.Length??0)<=16384,"Map exceeds geometry or navigation budget");
                NativeMapRules.Validate(map);
                if(!string.IsNullOrEmpty(arena.prefab))
                {
                    var prefab=bundle.LoadAsset<GameObject>(arena.prefab);
                    Need(prefab!=null,"Unknown map prefab "+arena.prefab);
                    ValidateScenery(prefab);
                    prefabs.Add(map.id,prefab);
                }
                var meshes=arena.meshes??Array.Empty<CommunityMesh>();
                Need(meshes.Length<=128,"Too many meshes");
                foreach(var m in meshes)
                {
                    data.Material(m.material);
                    Need(m.vertices!=null&&m.vertices.Length>=3&&m.vertices.Length<=65535&&m.vertices.All(v=>NativeMapRules.Finite(NativeArena.Vec(v))),"Invalid mesh vertices");
                    Need(m.triangles!=null&&m.triangles.Length>=3&&m.triangles.Length%3==0&&m.triangles.Length<=393216&&m.triangles.All(i=>i>=0&&i<m.vertices.Length),"Invalid triangle indices");
                }
                geometry.Add(map.id,meshes);
                data.maps=data.maps.Concat(new[]{map}).ToArray();
            }
            if((p.chips?.Length??0)>0)
            {
                var chips=new NativeChipPack{schema=1,id=p.id,name=p.id,version=p.version,chips=p.chips};
                NativeMods.Apply(data,0,new[]{new NativeLoadedPack{Pack=chips,Digest=digest}});
            }
        }
        private static void ValidateScenery(GameObject prefab)
        {
            // Check inactive children too, before instantiating anything into the match scene.
            var parts=prefab.GetComponentsInChildren<Component>(true);
            Need(parts.Length<=20000,"Map prefab exceeds component budget");
            foreach(var part in parts)
            {
                Need(part is Transform||part is MeshFilter||part is MeshRenderer||part is LODGroup||part is Light||
                    part is BoxCollider||part is SphereCollider||part is CapsuleCollider||part is MeshCollider,
                    "Map prefab contains a missing or unsupported component");
                if(part is Collider collider)Need(!collider.isTrigger,"Use native map hazards for triggers");
            }
        }
        private static void ValidateMotion(CommunityMotion clip)
        {
            Need(Actions.Contains(clip.action)&&clip.seconds>0&&clip.seconds<=60,"Invalid animation action or duration");
            Need(clip.tracks!=null&&clip.tracks.Length>0&&clip.tracks.Length<=CapturedRig.Bones.Length&&clip.tracks.Select(t=>t.bone).Distinct().Count()==clip.tracks.Length,"Invalid bone tracks");
            foreach(var t in clip.tracks)
            {
                t.index=Array.FindIndex(CapturedRig.Bones,b=>b.Name==t.bone);
                Need(t.index>=0&&t.rotations!=null&&t.rotations.Length>0&&t.rotations.Length<=3600,"Unknown bone or invalid sample count");
                foreach(var q in t.rotations){float n=Quaternion.Dot(q,q);Need(n>.98f&&n<1.02f,"Expected normalized bone rotation");}
            }
        }
        public static void BuildGeometry(string id,Transform root,Dictionary<string,Material> materials)
        {
            if(active==null||!active.geometry.TryGetValue(id,out var rows))return;
            if(active.prefabs.TryGetValue(id,out var prefab))
            {
                var instance=UnityEngine.Object.Instantiate(prefab,root,false);
                foreach(var child in instance.GetComponentsInChildren<Transform>(true))child.gameObject.layer=NativeArena.PropLayer;
            }
            foreach(var row in rows)
            {
                if(row.mesh==null)
                {
                    row.mesh=new Mesh{name=id,vertices=row.vertices,triangles=row.triangles};
                    row.mesh.RecalculateNormals();row.mesh.RecalculateBounds();
                }
                var go=new GameObject("Map geometry"){layer=NativeArena.PropLayer};
                go.transform.SetParent(root,false);
                go.AddComponent<MeshFilter>().sharedMesh=row.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=materials[row.material];
                if(row.solid)go.AddComponent<MeshCollider>().sharedMesh=row.mesh;
            }
        }
        internal static bool Pose(Actor actor,float time,Transform[] bones)
        {
            if(active==null||string.IsNullOrEmpty(actor.Motion)||!active.motions.TryGetValue(actor.Motion,out var clips)||
                actor.Hp<=0||actor.StunUntil>time||actor.Rolling(time)||NativeSpecialPose.Down(actor,time))return false;
            CommunityMotion selected=null;float age=0;
            foreach(var clip in clips)
            {
                float start;
                switch(clip.action)
                {
                    case "attack":start=Mathf.Max(actor.LastShot,actor.AttackAt);break;
                    case "cast":start=actor.AbilityAt;break;
                    case "reload":if(time>=actor.ReloadUntil)continue;start=actor.ReloadAt;break;
                    case "block":if(time>=actor.BlockUntil)continue;start=actor.BlockAt;break;
                    default:continue;
                }
                float t=time-start;
                if(start>=0&&t>=0&&t<clip.seconds&&(selected==null||t<age)){selected=clip;age=t;}
            }
            if(selected==null)
            {
                string action=actor.Velocity.x*actor.Velocity.x+actor.Velocity.z*actor.Velocity.z>.01f?"move":"idle";
                foreach(var clip in clips)if(clip.action==action){selected=clip;age=Mathf.Repeat(time,clip.seconds);break;}
            }
            if(selected==null)return false;
            foreach(var track in selected.tracks)
            {
                var frames=track.rotations;float frame=Mathf.Clamp01(age/selected.seconds)*(frames.Length-1);
                int i=(int)frame;bones[track.index].localRotation=Quaternion.Slerp(frames[i],frames[Math.Min(i+1,frames.Length-1)],frame-i);
            }
            return true;
        }
        public static void Clear()
        {
            active?.Release();active=null;
        }
        private void Release()
        {
            foreach(var rows in geometry.Values)foreach(var row in rows)if(row.mesh!=null)CapturedAnimation.Release(row.mesh);
            foreach(var bundle in bundles)bundle.Unload(true);
        }
    }
}
