using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeWoundPart {public string name,root;public int bone,tag;public WoundVector center,size;public float[] matrix,color,emission;public WoundSolid[] solids;}
    [Serializable] public sealed class NativeWoundParts {public string commit;public NativeWoundPart[] parts;}
    public sealed partial class NativeSourceModels
    {
        private sealed class Carved {public string Signature,Model;public Part[] Owned;}
        private readonly Dictionary<int,Carved> wounds=new Dictionary<int,Carved>();private static NativeWoundPart[] woundParts;
        public static NativeWoundPart[] WoundParts {get{if(woundParts==null){var data=JsonUtility.FromJson<NativeWoundParts>(Resources.Load<TextAsset>("WoundParts").text);if(data.commit!="05950eaef80ff2483a9c44d898cad98be1a76bbc")throw new InvalidOperationException("Unverified wound geometry");woundParts=data.parts;}return woundParts;}}
        private sealed class Anchor {public NativeWoundPart part;public WoundCarveSpec spec;}
        private static string WoundRoot(int b)=>b==5?"head":b==2?"spine":b==7?"left-shoulder":b==11?"right-shoulder":b==8?"left-elbow":b==12?"right-elbow":b==14?"left-hip":b==18?"right-hip":b==15?"left-knee":"right-knee";
        private static WoundVector ClampVector(WoundVector p,WoundVector min,WoundVector max)=>new WoundVector(Math.Max(min.x,Math.Min(max.x,p.x)),Math.Max(min.y,Math.Min(max.y,p.y)),Math.Max(min.z,Math.Min(max.z,p.z)));
        private static (double first,double last,int axis)? RayBox(WoundVector o,WoundVector d,WoundVector min,WoundVector max)
        {double first=double.NegativeInfinity,last=double.PositiveInfinity;int axis=0;for(int a=0;a<3;a++){if(Math.Abs(d[a])<1e-9){if(o[a]<min[a]||o[a]>max[a])return null;continue;}double n=(min[a]-o[a])/d[a],f=(max[a]-o[a])/d[a];if(n>f){double swap=n;n=f;f=swap;}if(n>first){first=n;axis=a;}last=Math.Min(last,f);if(first>last)return null;}return(first,last,axis);}
        private static List<Anchor> Anchors(NativeWound w,bool low,WoundVector body)
        {
            string root=WoundRoot(w.part);var parts=woundParts.Where(p=>p.root==root||root=="spine"&&p.root=="pelvis").ToArray();if(parts.Length==0)return new List<Anchor>();var point=new WoundVector(w.point.x,w.point.y,w.point.z);var travel=new WoundVector(w.direction.x,w.direction.y,w.direction.z);if(travel.Length<1e-9)travel=new WoundVector(0,0,-1);travel=travel.Normal;
            Anchor On(NativeWoundPart p,WoundVector hit,int axis,int sign,WoundVector direction){hit=hit-p.center;hit=ClampVector(hit,p.size*-.5,p.size*.5);hit[axis]=sign*p.size[axis]/2;var inward=new WoundVector();inward[axis]=-sign;direction=direction.Length>1e-9?direction.Normal:inward;double dot=direction[axis]*-sign;if(dot<.5)direction=(direction+inward*(.85-dot)).Normal;return new Anchor{part=p,spec=new WoundCarveSpec{hit=hit,axis=axis,sign=sign,dir=direction,size=p.size,kind=w.kind,trauma=w.trauma,radius=w.radius,severity=w.severity,seed=NativeWoundCarve.Seed(w.id.ToString(System.Globalization.CultureInfo.InvariantCulture)),low=low,body=body,vertical=w.direction.y}};}
            List<Anchor> Along(WoundVector origin,WoundVector direction,WoundVector keep)
            {var hits=parts.Select(p=>(p,ray:RayBox(origin,direction,p.center-p.size*.5,p.center+p.size*.5))).Where(v=>v.ray.HasValue&&v.ray.Value.last>=0).OrderBy(v=>v.ray.Value.first).ToArray();if(hits.Length==0)return null;var first=hits[0];int axis=first.ray.Value.axis;var result=new List<Anchor>{On(first.p,origin+direction*first.ray.Value.first,axis,direction[axis]>0?-1:1,keep)};if(first.p.size[axis]<.05){foreach(var next in hits.Skip(1))if(next.ray.Value.first>=first.ray.Value.last-.01&&next.ray.Value.first<=first.ray.Value.last+.08){int a=next.ray.Value.axis;result.Add(On(next.p,origin+direction*next.ray.Value.first,a,direction[a]>0?-1:1,keep));break;}}return result;}
            var shot=Along(point-travel*.6,travel,travel);if(shot!=null)return shot;var nearest=parts.OrderBy(p=>(ClampVector(point,p.center-p.size*.5,p.center+p.size*.5)-point).Length+(Math.Min(p.size.x,Math.Min(p.size.y,p.size.z))<.05?.05:0)).First();var local=point-nearest.center;int face=0;double score=double.NegativeInfinity;for(int a=0;a<3;a++){double n=Math.Abs(local[a])/Math.Max(1e-6,nearest.size[a]/2);if(n>score){score=n;face=a;}}int side=local[face]>=0?1:-1;var spot=ClampVector(point,nearest.center-nearest.size*.5,nearest.center+nearest.size*.5);var dir=new WoundVector();dir[face]=-side;return Along(spot-dir*.3,dir,dir)??new List<Anchor>{On(nearest,spot,face,side,dir)};
        }
        public string WoundedPilot(int id,NativeWound[] impacts,string bodyHex,bool low=false)
        {
            var valid=(impacts??Array.Empty<NativeWound>()).Where(w=>NativeBodyDamage.Valid(w)&&!w.destroyed).Take(8).OrderBy(w=>w.at).ThenBy(w=>w.id).ToArray();if(valid.Length==0){RemoveWounds(id);return "pilot";}
            string signature=bodyHex+":"+low+":"+string.Join(",",valid.Select(w=>w.id+"@"+w.at.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));if(wounds.TryGetValue(id,out var previous)&&previous.Signature==signature)return previous.Model;RemoveWounds(id);
            var allParts=WoundParts;
            if(!ColorUtility.TryParseHtmlString(bodyHex??"#e5e7eb",out var body))body=Color.white;body=body.linear;var color=new WoundVector(body.r,body.g,body.b);var groups=valid.SelectMany(w=>Anchors(w,low,color)).GroupBy(a=>a.part.name).ToArray();var replaced=new HashSet<string>(groups.Select(g=>g.Key));var parts=models["pilot"].Where(p=>!replaced.Contains(p.Name)).ToList();var owned=new List<Part>();
            foreach(var group in groups){var d=group.First().part;var solids=d.solids.ToList();foreach(var a in group)solids=NativeWoundCarve.Carve(solids,a.spec);var template=models["pilot"].FirstOrDefault(p=>p.Name==d.name);if(template==null)throw new InvalidOperationException("Wound part absent from source mesh: "+d.name);var part=CarveMesh(d,solids,template.Material);parts.Add(part);owned.Add(part);}
            string model="wounded-"+id;models[model]=parts.ToArray();wounds[id]=new Carved{Signature=signature,Model=model,Owned=owned.ToArray()};return model;
        }
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
            if(woundParts==null)return null;var a=Anchors(wound,false,new WoundVector(1,1,1)).FirstOrDefault();if(a==null)return null;var matrix=new Matrix4x4();for(int i=0;i<16;i++)matrix[i%4,i/4]=a.part.matrix[i];matrix*=Matrix4x4.Scale(new Vector3((float)(1/a.part.size.x),(float)(1/a.part.size.y),(float)(1/a.part.size.z)));var hit=a.spec.hit;var p=matrix.MultiplyPoint3x4(new Vector3((float)hit.x,(float)hit.y,(float)hit.z));var n=Vector3.zero;n[a.spec.axis]=a.spec.sign;n=matrix.inverse.transpose.MultiplyVector(n).normalized;var dir=matrix.MultiplyVector(new Vector3((float)a.spec.dir.x,(float)a.spec.dir.y,(float)a.spec.dir.z)).normalized;p.x=-p.x;n.x=-n.x;dir.x=-dir.x;return(a.part.bone,p,n,dir);
        }
        public void WoundCube(Matrix4x4 matrix,Color color,float opacity=1)
        {
            if(!models.TryGetValue("wound-cube",out var parts)){var mat=new Material(Resources.Load<Shader>("NativeSourceLit")){enableInstancing=true};materials.Add(mat);var d=new NativeWoundPart{name="wound-cube",bone=-1,size=new WoundVector(1,1,1),matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1},color=new float[]{1,1,1},emission=new float[]{.3f,.3f,.3f}};parts=new[]{CarveMesh(d,new List<WoundSolid>{new WoundSolid{min=new WoundVector(-.5,-.5,-.5),max=new WoundVector(.5,.5,.5),color=new WoundVector(1,1,1)}},mat)};models["wound-cube"]=parts;}
            var key=(parts[0],0);if(!draws.TryGetValue(key,out var draw)){draw=new Draws{Part=parts[0]};draws[key]=draw;}draw.Matrices.Add(matrix);draw.Bodies.Add(new Vector4(1,1,1,opacity));draw.Accents.Add(Vector4.one);draw.Tints.Add(color);
        }
        private void RemoveWounds(int id){if(!wounds.TryGetValue(id,out var old))return;foreach(var part in old.Owned){foreach(var key in draws.Keys.Where(k=>k.Item1==part).ToArray())draws.Remove(key);CapturedAnimation.Release(part.Mesh);}models.Remove(old.Model);wounds.Remove(id);}
        public void ClearWounds(){foreach(int id in wounds.Keys.ToArray())RemoveWounds(id);}
    }
}
