using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        // One pilot's carved wounds (Core NativeWoundAnchors: the exported part boxes carved by NativeWoundCarve, each carve's
        // cells recorded) and the wounded models composed from them, one per body it is drawn on:
        //  - the Prototype "pilot": its parts with each wounded part replaced by name by its carved boxes (unchanged; built on
        //    the first Prototype draw);
        //  - a sculpted body skin tier (Core NativeBodyModels): the body's own parts with the same carve cut into them
        //    (Core NativeWoundSkin.Cut), never a Prototype part. Those cuts are kept per joint (SkinCut, keyed by the wounds
        //    on that joint), so a new wound recuts only the joints it reaches and a tier change cuts that tier once.
        // A pilot keeps two carves (two slots, keyed id and ~id): its live body's and the body it left (a corpse, a death scene,
        // a revive drawing the death's wounds), so the two never recut each other every frame while both are drawn (a revive
        // overlaps its corpse for up to 1.1 s). A third set replaces the one drawn longer ago; a carve no draw has asked for in
        // KeepCarve seconds is released by the pilot's next draw. The same open wounds as a kept carve are found without
        // allocating (the first eight valid ones compared in order).
        private sealed class Carved
        {
            public string Signature,Prefix,BodyHex;public bool Low;public int Key;public float Used;public WoundCarveGroup[] Groups;public HashSet<string> Replaced;public Part[] Owned;public Dictionary<uint,string> Keys;
            public readonly Dictionary<string,string> Models=new Dictionary<string,string>();public readonly Dictionary<string,List<WoundHole>> Holes=new Dictionary<string,List<WoundHole>>();
            public readonly int[] RawIds=new int[8];public readonly float[] RawAts=new float[8];public int RawCount;
        }
        private sealed class SkinCut {public string Signature;public readonly Dictionary<Part,Part> Replaced=new Dictionary<Part,Part>();public readonly List<Part> Owned=new List<Part>(),Extra=new List<Part>();public readonly List<WoundHole> Holes=new List<WoundHole>();}
        private readonly Dictionary<int,Carved> wounds=new Dictionary<int,Carved>(),woundsB=new Dictionary<int,Carved>();private static NativeWoundPart[] woundParts;
        /// <summary>Seconds a pilot's second carve stays kept after its last draw.</summary>
        public const float KeepCarve=2.5f;
        private readonly Dictionary<(int id,string basis,int bone),SkinCut> skinCuts=new Dictionary<(int,string,int),SkinCut>();
        /// <summary>Each sculpted body tier's Core mesh by bank id (NativeBodySkinModels keeps the build from load); its parts
        /// match the bank's by name and joint.</summary>
        private readonly Dictionary<string,PilotBodyMesh> pilotBodies=new Dictionary<string,PilotBodyMesh>();
        public static NativeWoundPart[] WoundParts {get{if(woundParts==null){var data=JsonUtility.FromJson<NativeWoundParts>(Resources.Load<TextAsset>("WoundParts").text);if(data.commit!="05950eaef80ff2483a9c44d898cad98be1a76bbc")throw new InvalidOperationException("Unverified wound geometry");woundParts=data.parts;}return woundParts;}}
        /// <summary>
        /// The model a pilot draws with its open wounds: `basis` (the body it wears: the Prototype "pilot" or a body skin's
        /// tier, NativeBodySkins) wounded, or `basis` itself while unwounded. The carve depends on the wounds only; the
        /// Prototype shows it as carved boxes, a body skin cut into its own surface (NativeWoundSkin).
        /// </summary>
        public double DiagnosticWoundMs {get;private set;}
        public string WoundedPilot(int id,NativeWound[] impacts,string bodyHex,bool low=false,string basis=NativeBodySkins.PrototypeModel)
        {
            long at=System.Diagnostics.Stopwatch.GetTimestamp();
            try{return WoundedPilotCore(id,impacts,bodyHex,low,basis);}
            finally{DiagnosticWoundMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-at)*1000d/System.Diagnostics.Stopwatch.Frequency;}
        }
        private string WoundedPilotCore(int id,NativeWound[] impacts,string bodyHex,bool low,string basis)
        {
            if(basis==null||!models.ContainsKey(basis))basis=NativeBodySkins.PrototypeModel;
            float now=Time.unscaledTime;wounds.TryGetValue(id,out var a);woundsB.TryGetValue(id,out var b);
            if(Same(a,impacts,bodyHex,low)){a.Used=now;Stale(b,now);return Wounded(a,basis);}
            if(Same(b,impacts,bodyHex,low)){b.Used=now;Stale(a,now);return Wounded(b,basis);}
            var valid=(impacts??Array.Empty<NativeWound>()).Where(w=>NativeBodyDamage.Valid(w)&&!w.destroyed).Take(8).OrderBy(w=>w.at).ThenBy(w=>w.id).ToArray();if(valid.Length==0){Stale(a,now);Stale(b,now);return basis;}
            string prefix=bodyHex+":"+low+":",signature=prefix+string.Join(",",valid.Select(w=>w.id+"@"+w.at.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            if(a!=null&&a.Signature==signature){Raw(a,impacts);a.Used=now;return Wounded(a,basis);}
            if(b!=null&&b.Signature==signature){Raw(b,impacts);b.Used=now;return Wounded(b,basis);}
            // A new set: a free slot, else the slot drawn longer ago.
            bool second=a!=null&&(b==null||b.Used<a.Used);if(second){if(b!=null)ReleaseCarved(b);}else if(a!=null)ReleaseCarved(a);
            var allParts=WoundParts;
            if(!ColorUtility.TryParseHtmlString(bodyHex??"#e5e7eb",out var body))body=Color.white;body=body.linear;var color=new WoundVector(body.r,body.g,body.b);
            var groups=NativeWoundAnchors.Carve(valid.SelectMany(w=>NativeWoundAnchors.Of(allParts,w,low,color)));var keys=new Dictionary<uint,string>();
            foreach(var w in valid)keys[NativeWoundCarve.Seed(w.id.ToString(System.Globalization.CultureInfo.InvariantCulture))]=w.id+"@"+w.at.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            var carved=new Carved{Signature=signature,Prefix=prefix,BodyHex=bodyHex,Low=low,Key=second?~id:id,Used=now,Groups=groups,Keys=keys,Replaced=new HashSet<string>(groups.Select(g=>g.Part.name))};Raw(carved,impacts);
            if(second)woundsB[id]=carved;else wounds[id]=carved;return Wounded(carved,basis);
        }
        /// <summary>Whether `impacts` opens the same wounds as carve `c` (its first eight valid open ones, in order), without allocating.</summary>
        private static bool Same(Carved c,NativeWound[] impacts,string bodyHex,bool low)
        {
            if(c==null||c.Low!=low||!string.Equals(c.BodyHex,bodyHex,StringComparison.Ordinal))return false;int n=0;
            if(impacts!=null)for(int i=0;i<impacts.Length;i++){var w=impacts[i];if(!NativeBodyDamage.Valid(w)||w.destroyed)continue;if(n==8)break;if(n>=c.RawCount||c.RawIds[n]!=w.id||c.RawAts[n]!=w.at)return false;n++;}
            return n>0&&n==c.RawCount;
        }
        private static void Raw(Carved c,NativeWound[] impacts)
        {
            int n=0;if(impacts!=null)for(int i=0;i<impacts.Length&&n<8;i++){var w=impacts[i];if(!NativeBodyDamage.Valid(w)||w.destroyed)continue;c.RawIds[n]=w.id;c.RawAts[n]=w.at;n++;}c.RawCount=n;
        }
        /// <summary>Releases carve `c` when no draw has asked for it in <see cref="KeepCarve"/> s.</summary>
        private void Stale(Carved c,float now){if(c!=null&&now-c.Used>KeepCarve)ReleaseCarved(c);}
        /// <summary>The cells the wounds of `model` (a <see cref="WoundedPilot"/> result) take out, in their joints' bank frames
        /// (NativeReviveHolo refills them); none for an unwounded body.</summary>
        public void WoundHoles(string model,List<WoundHole> into)
        {
            into.Clear();if(model==null)return;foreach(var c in wounds.Values)if(Holes(c,model,into))return;foreach(var c in woundsB.Values)if(Holes(c,model,into))return;
        }
        private static bool Holes(Carved c,string model,List<WoundHole> into)
        {
            foreach(var pair in c.Models)if(pair.Value==model)
            {
                if(!c.Holes.TryGetValue(pair.Key,out var list)){list=new List<WoundHole>();if(pair.Key==NativeBodySkins.PrototypeModel)NativeWoundSkin.PrototypeHoles(c.Groups,list);c.Holes[pair.Key]=list;}
                into.AddRange(list);return true;
            }
            return false;
        }
        private string Wounded(Carved carved,string basis)
        {
            if(carved.Models.TryGetValue(basis,out var model))return model;int id=carved.Key;
            model=basis==NativeBodySkins.PrototypeModel?"wounded-"+id:"wounded-"+id+"/"+basis;
            Part[] parts;
            if(basis==NativeBodySkins.PrototypeModel)
            {
                if(carved.Owned==null)
                {
                    var owned=new List<Part>();
                    foreach(var group in carved.Groups){var d=group.Part;var template=models[NativeBodySkins.PrototypeModel].FirstOrDefault(p=>p.Name==d.name);if(template==null)throw new InvalidOperationException("Wound part absent from source mesh: "+d.name);owned.Add(CarveMesh(d,group.Solids,template.Material));}
                    carved.Owned=owned.ToArray();
                    // Their Core arrays for a bisect (NativeBisectModels.CarveCore: the same boxes in Core).
                    for(int i=0;i<carved.Owned.Length;i++)carved.Owned[i].Core=CarveCore(carved.Groups[i].Part,carved.Groups[i].Solids);
                }
                var list=models[basis].Where(p=>!carved.Replaced.Contains(p.Name)).ToList();list.AddRange(carved.Owned);parts=list.ToArray();
            }
            // A sculpted body without its Core mesh (never the case once the bank has loaded) draws unwounded, never with
            // Prototype parts.
            else if(!pilotBodies.TryGetValue(basis,out var body))return basis;
            else parts=SkinWounds(id,carved,basis,body);
            if(basis!=NativeBodySkins.PrototypeModel)
            {var holes=new List<WoundHole>();foreach(var cut in skinCuts)if(cut.Key.id==id&&cut.Key.basis==basis)holes.AddRange(cut.Value.Holes);carved.Holes[basis]=holes;}
            models[model]=parts;carved.Models[basis]=model;return model;
        }
        /// <summary>A body skin's tier with the wounds cut in: each wounded joint's cut (kept while its wounds stay), the
        /// body's other parts as they are.</summary>
        private Part[] SkinWounds(int id,Carved carved,string basis,PilotBodyMesh body)
        {
            var baseParts=models[basis];var look=NativeBodyModels.WoundLook(NativeBodySkins.SkinOf(basis));var live=new HashSet<(int,string,int)>();
            var replaced=new Dictionary<Part,Part>();var extra=new List<Part>();
            foreach(var joint in carved.Groups.GroupBy(g=>g.Part.bone))
            {
                var groups=joint.ToArray();var key=(id,basis,joint.Key);live.Add(key);
                string signature=carved.Prefix+string.Join(";",groups.Select(g=>g.Part.name+"="+string.Join(",",g.Steps.Select((s,i)=>(carved.Keys.TryGetValue(s.Seed,out var k)?k:s.Seed.ToString())+(g.First[i]?"":"b")))));
                if(!skinCuts.TryGetValue(key,out var cut)||cut.Signature!=signature){if(cut!=null)ReleaseSkinCut(cut);cut=SkinCutOf(body,baseParts,groups,look,signature,basis);skinCuts[key]=cut;}
                foreach(var pair in cut.Replaced)replaced[pair.Key]=pair.Value;extra.AddRange(cut.Extra);
            }
            foreach(var stale in skinCuts.Keys.Where(k=>k.id==id&&k.basis==basis&&!live.Contains(k)).ToArray()){ReleaseSkinCut(skinCuts[stale]);skinCuts.Remove(stale);}
            var parts=new List<Part>(baseParts.Length+extra.Count);
            foreach(var p in baseParts){if(!replaced.TryGetValue(p,out var r))parts.Add(p);else if(r!=null)parts.Add(r);}
            parts.AddRange(extra);return parts.ToArray();
        }
        private SkinCut SkinCutOf(PilotBodyMesh body,Part[] baseParts,WoundCarveGroup[] groups,in WoundSkinLook look,string signature,string basis)
        {
            Part Base(int index){var b=body.Parts[index];foreach(var p in baseParts)if(p.Name==b.Name&&p.Bone==b.Bone)return p;return null;}
            var cut=new SkinCut{Signature=signature};
            foreach(var m in NativeWoundSkin.Cut(body,groups,look,cut.Holes))
            {
                var source=Base(m.Part>=0?m.Part:m.Material);if(source==null)continue;var mesh=BodyMesh(m.Mesh,basis+" / wounded "+m.Name+" / joint "+m.Bone);
                var part=mesh==null?null:new Part{Name=m.Part>=0?source.Name:m.Name,Bone=m.Bone,Mesh=mesh,Material=source.Material,NoShadow=source.NoShadow,Core=m.Mesh,Fill=m.Part>=0?body.Parts[m.Part].Mesh:null};if(part!=null)cut.Owned.Add(part);
                if(m.Part>=0)cut.Replaced[source]=part;else if(part!=null)cut.Extra.Add(part);
            }
            return cut;
        }
        private void ReleaseSkinCut(SkinCut cut){foreach(var part in cut.Owned){foreach(var key in draws.Keys.Where(k=>k.Item1==part).ToArray())draws.Remove(key);CapturedAnimation.Release(part.Mesh);}}
        /// <summary>Drops pilot `id`'s kept body-skin cuts (all bodies, or one `basis`).</summary>
        private void RemoveSkinCuts(int id,string basis){foreach(var key in skinCuts.Keys.Where(k=>k.id==id&&(basis==null||k.basis==basis)).ToArray()){ReleaseSkinCut(skinCuts[key]);skinCuts.Remove(key);}}
        private Part CarveMesh(NativeWoundPart d,List<WoundSolid> solids,Material material)
        {
            var matrix=new Matrix4x4();for(int i=0;i<16;i++)matrix[i%4,i/4]=d.matrix[i];matrix=matrix*Matrix4x4.Scale(new Vector3((float)(1/d.size.x),(float)(1/d.size.y),(float)(1/d.size.z)));var normalMatrix=matrix.inverse.transpose;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var emissions=new List<Vector4>();var indices=new List<int>();
            foreach(var box in solids)
            {var mid=(box.min+box.max)*.5;var extent=(box.max-box.min)*.5;Color tint=new Color((float)box.color.x,(float)box.color.y,(float)box.color.z);Color paint=box.flesh?tint:new Color(d.color[0]*tint.r,d.color[1]*tint.g,d.color[2]*tint.b);var emission=box.flesh?new Vector4(tint.r*.3f,tint.g*.3f,tint.b*.3f,0):new Vector4(d.emission[0]*tint.r,d.emission[1]*tint.g,d.emission[2]*tint.b,d.tag);
             for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1}){var n=Vector3.zero;n[axis]=sign;var u=Vector3.zero;u[(axis+1)%3]=1;var v=Vector3.Cross(n,u);int start=vertices.Count;foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){var q=n+u*corner.x+v*corner.y;var p=matrix.MultiplyPoint3x4(new Vector3((float)(mid.x+q.x*extent.x),(float)(mid.y+q.y*extent.y),(float)(mid.z+q.z*extent.z)));p.x=-p.x;var normal=normalMatrix.MultiplyVector(n).normalized;normal.x=-normal.x;vertices.Add(p);normals.Add(normal);colors.Add(paint);emissions.Add(emission);}indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});}}
            var mesh=new Mesh{name="Carved "+d.name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(1,emissions);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return new Part{Name=d.name,Bone=d.bone,Mesh=mesh,Material=material};
        }
        public (int bone,Vector3 point,Vector3 normal,Vector3 direction)? WoundMark(NativeWound wound)
        {
            if(woundParts==null)return null;var a=NativeWoundAnchors.Of(woundParts,wound,false,new WoundVector(1,1,1)).FirstOrDefault();if(a==null)return null;var matrix=new Matrix4x4();for(int i=0;i<16;i++)matrix[i%4,i/4]=a.Part.matrix[i];matrix*=Matrix4x4.Scale(new Vector3((float)(1/a.Part.size.x),(float)(1/a.Part.size.y),(float)(1/a.Part.size.z)));var hit=a.Spec.hit;var p=matrix.MultiplyPoint3x4(new Vector3((float)hit.x,(float)hit.y,(float)hit.z));var n=Vector3.zero;n[a.Spec.axis]=a.Spec.sign;n=matrix.inverse.transpose.MultiplyVector(n).normalized;var dir=matrix.MultiplyVector(new Vector3((float)a.Spec.dir.x,(float)a.Spec.dir.y,(float)a.Spec.dir.z)).normalized;p.x=-p.x;n.x=-n.x;dir.x=-dir.x;return(a.Part.bone,p,n,dir);
        }
        public void WoundCube(Matrix4x4 matrix,Color color,float opacity=1)
        {
            if(!models.TryGetValue("wound-cube",out var parts)){var mat=new Material(Resources.Load<Shader>("NativeSourceLit")){enableInstancing=true};materials.Add(mat);var d=new NativeWoundPart{name="wound-cube",bone=-1,size=new WoundVector(1,1,1),matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1},color=new float[]{1,1,1},emission=new float[]{.3f,.3f,.3f}};parts=new[]{CarveMesh(d,new List<WoundSolid>{new WoundSolid{min=new WoundVector(-.5,-.5,-.5),max=new WoundVector(.5,.5,.5),color=new WoundVector(1,1,1)}},mat)};models["wound-cube"]=parts;}
            var key=(parts[0],0,(Camera)null);if(!draws.TryGetValue(key,out var draw)){draw=new Draws{Part=parts[0]};draws[key]=draw;}draw.Matrices.Add(matrix);draw.Bodies.Add(new Vector4(1,1,1,opacity));draw.Accents.Add(Vector4.one);draw.Tints.Add(color);draw.Glows.Add(Vector4.one);
        }
        /// <summary>Releases one carve: its Prototype boxes, its models and its body-skin cuts.</summary>
        private void ReleaseCarved(Carved old)
        {
            if(old.Owned!=null)foreach(var part in old.Owned){foreach(var key in draws.Keys.Where(k=>k.Item1==part).ToArray())draws.Remove(key);CapturedAnimation.Release(part.Mesh);}foreach(var model in old.Models.Values)models.Remove(model);
            RemoveSkinCuts(old.Key,null);int id=old.Key<0?~old.Key:old.Key;var slot=old.Key<0?woundsB:wounds;if(slot.TryGetValue(id,out var held)&&held==old)slot.Remove(id);
        }
        public void ClearWounds(){foreach(var c in wounds.Values.Concat(woundsB.Values).ToArray())ReleaseCarved(c);foreach(var cut in skinCuts.Values)ReleaseSkinCut(cut);skinCuts.Clear();}
    }
}
