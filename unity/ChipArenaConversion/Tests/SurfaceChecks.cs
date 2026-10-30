using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    // Coincident surfaces on the Unity maps (the owner's surface artifacts on the new maps, 2026-10-13): every block and every
    // dressing-kit part each map draws (Factory, Cyber City and Ward Zero kits from their own builders, the blocks as the
    // dressing draws them), as boxes in the world. Two faces that lie in one plane, face the same way and overlap draw at the
    // same depth: whichever wins flickers with the view (z-fighting). A pair whose shared patch is covered by a third box is not
    // seen and is not counted.
    internal static class SurfaceChecks
    {
        // Closer than this counts as one plane: 90% of the step, as float32 coordinates hundreds of metres out round the 2 mm.
        private const float Near=NativeSurfaceLayers.Step*.9f;
        internal struct Box{public string Id;public int Look,Color,Skip;public bool Kit;public float Bevel;public Vector3 C,H;public Quaternion Q;public Vector3 Min,Max;}
        internal struct Clash{public int A,B,FaceA,FaceB;public float Gap,Area;public Vector3 At,Normal;}
        private static Vector3 V(V3 v)=>new Vector3(v.x,v.y,v.z);
        private static Quaternion Q(ContentRotation r)=>r==null?Quaternion.Identity:Quaternion.Normalize(new Quaternion(r.x,r.y,r.z,r.w));
        private static Box Make(string id,bool kit,Vector3 c,Vector3 h,Quaternion q,int look,int color,float bevel=0)
        {
            var b=new Box{Id=id,Kit=kit,C=c,H=h,Q=q,Look=look,Color=color,Bevel=bevel};var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
            for(int k=0;k<8;k++){var p=c+Vector3.Transform(new Vector3((k&1)==0?-h.X:h.X,(k&2)==0?-h.Y:h.Y,(k&4)==0?-h.Z:h.Z),q);lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}
            b.Min=lo;b.Max=hi;return b;
        }
        /// <summary>Every box map `map` draws in play: its blocks and its dressing kit's parts, as the dressing and the kits draw
        /// them (`layers`: with NativeSurfaceLayers' lifts, as today; off: as before them).</summary>
        internal static List<Box> Boxes(string root,ContentMap map,bool layers=true,bool hub=false)
        {
            var o=new List<Box>();var blocks=map.blocks??Array.Empty<ContentBlock>();var options=new JsonSerializerOptions{IncludeFields=true};
            // Blocks look as the dressing draws them (NativeArenaDressing: the map look's block looks over the material colour).
            var lookSet=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),options);
            var look=(string.IsNullOrEmpty(map.look)?null:NativeMapLooks.ByLook(lookSet,map.look))??NativeMapLooks.Resolve(lookSet,map.id,map.theme,map.sky,map.accent);
            var materials=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),options).materials??Array.Empty<ContentMaterial>();
            var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
            int Colour(ContentBlock b){if(b.material=="foundation")return map.floor;foreach(var m in content.materials??materials)if(m?.id==b.material)return m.color;return 0xffffff;}
            var lifts=layers?NativeSurfaceLayers.Blocks(blocks):null;
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];if(b?.id==null)continue;var c=NativeBlockLooks.Resolve(look?.blockLooks,null,b.id,b.material,Colour(b));
                NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);var q=Q(b.rotation);
                o.Add(Make(b.id,false,V(b.pos)+Vector3.Transform(new Vector3(sx,sy,sz),q),V(b.size)*.5f+new Vector3(gx,gy,gz),q,c.Look,c.Color));
            }
            if(map.id=="factory")
            {
                FactoryHazard[] hz=null;using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"))))
                    foreach(var m in doc.RootElement.GetProperty("maps").EnumerateArray())if(m.GetProperty("id").GetString()=="factory")hz=JsonSerializer.Deserialize<FactoryHazard[]>(m.GetProperty("hazards").GetRawText(),options);
                var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),options);
                var kit=NativeFactoryKit.Build(blocks,hz,NativeFactoryKit.Resolve(set.maps.First(l=>l.id=="factory").kitLooks),layers);
                for(int i=0;i<kit.Count;i++){var p=kit.Items[i];o.Add(Make("factory-kit#"+i,true,new Vector3(p.X,p.Y,p.Z),new Vector3(p.Hx,p.Hy,p.Hz),Quaternion.Normalize(new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw)),p.Look,p.Color,NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz)));}
            }
            if(map.id=="cybercity")
            {
                var kit=NativeCyberCityKit.Build(blocks,map.hazards??Array.Empty<MapHazard>(),NativeCyberCityExits.Build(blocks,map.nav,map.hazards??Array.Empty<MapHazard>()),layers);
                for(int i=0;i<kit.Parts.Length;i++){var p=kit.Parts[i];var b=blocks[p.Parent];var q=Q(b.rotation);o.Add(Make(b.id+"#kit"+i,true,V(b.pos)+Vector3.Transform(new Vector3(p.X,p.Y,p.Z),q),new Vector3(p.Hx,p.Hy,p.Hz),q,p.Look,p.Color));}
            }
            if(map.id==NativeExpeditionCityKit.MapId)
            {
                // The town's dressing kit with its records (Resources/ExpeditionLayout.json): each part turned with its block, then by its own turn.
                // `hub`: with the hub record and the map's bounds, as the runtime builds it (fence, headers, markers; ExpeditionCityChecks).
                string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json"));var layout=NativeExpeditionLayout.Parse(text);var townArea=layout.areas.First(a=>a.map==map.id);
                var kit=hub?NativeExpeditionCityKit.Build(blocks,townArea,ExpeditionHubLayout.Parse(text,townArea.id),map.bounds,layers):NativeExpeditionCityKit.Build(blocks,townArea,layers);
                for(int i=0;i<kit.Parts.Length;i++){var p=kit.Parts[i];var b=blocks[p.Parent];var q=Q(b.rotation);o.Add(Make(b.id+"#kit"+i,true,V(b.pos)+Vector3.Transform(new Vector3(p.X,p.Y,p.Z),q),new Vector3(p.Hx,p.Hy,p.Hz),Quaternion.Normalize(q*new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw)),p.Look,p.Color));}
            }
            if(map.id=="siren")
            {
                var entries=JsonSerializer.Deserialize<NativeWardEntrySet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),options)?.entries??Array.Empty<NativeWardEntry>();
                var kit=NativeWardZeroKit.Build(blocks,map.hazards??Array.Empty<MapHazard>(),entries,layers);
                // Moving parts (doors, leaves, slats) at rest, as the calm mood and a closed entry draw them.
                for(int i=0;i<kit.Parts.Length;i++){var p=kit.Parts[i];var b=blocks[p.Parent];var q=Q(b.rotation);o.Add(Make(b.id+"#kit"+i,true,V(b.pos)+Vector3.Transform(new Vector3(p.X,p.Y,p.Z),q),new Vector3(p.Hx,p.Hy,p.Hz),q,p.Look,p.Color));}
            }
            return o;
        }
        private static void Face(in Box b,int f,out Vector3 c,out Vector3 n,out Vector3 u,out Vector3 v,out float hu,out float hv)
        {
            int axis=f>>1;float s=(f&1)==0?-1:1;Vector3 e0=Vector3.Transform(Vector3.UnitX,b.Q),e1=Vector3.Transform(Vector3.UnitY,b.Q),e2=Vector3.Transform(Vector3.UnitZ,b.Q);
            var e=new[]{e0,e1,e2};var h=new[]{b.H.X,b.H.Y,b.H.Z};n=e[axis]*s;c=b.C+n*h[axis];u=e[(axis+1)%3];v=e[(axis+2)%3];hu=(float)NativeSurfaceLayers.Flat(h[(axis+1)%3],b.Bevel);hv=(float)NativeSurfaceLayers.Flat(h[(axis+2)%3],b.Bevel);
        }
        private static bool Inside(in Box b,Vector3 p,float pad=0)
        {
            var d=Vector3.Transform(p-b.C,Quaternion.Conjugate(b.Q));return Math.Abs(d.X)<b.H.X+pad&&Math.Abs(d.Y)<b.H.Y+pad&&Math.Abs(d.Z)<b.H.Z+pad;
        }
        // Convex polygon clip (Sutherland-Hodgman) of `poly` by the rectangle |x|<=hu, |y|<=hv.
        private static List<Vector2> Clip(List<Vector2> poly,float hu,float hv)
        {
            var edges=new[]{(new Vector2(1,0),hu),(new Vector2(-1,0),hu),(new Vector2(0,1),hv),(new Vector2(0,-1),hv)};
            foreach(var (n,d) in edges)
            {
                var o=new List<Vector2>();for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];float da=Vector2.Dot(a,n)-d,db=Vector2.Dot(b,n)-d;if(da<=0)o.Add(a);if(da<=0!=db<=0)o.Add(a+(b-a)*(da/(da-db)));}
                poly=o;if(poly.Count==0)break;
            }
            return poly;
        }
        private static float Area(List<Vector2> p){float s=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];s+=a.X*b.Y-a.Y*b.X;}return Math.Abs(s)*.5f;}
        /// <summary>The visible coincident faces among `boxes`: same plane within `gap` m, same facing, overlapping by more than
        /// `area` m², the shared patch not inside a third box.</summary>
        internal static List<Clash> Clashes(List<Box> boxes,MapBounds bounds=null,float gap=.002f,float area=1e-4f,float minWidth=0)
        {
            // A camera only looks from inside the play space (the map's bounds, up to its ceiling or 30 m).
            bool Reach(Vector3 w)=>bounds==null||w.X>bounds.minX&&w.X<bounds.maxX&&w.Z>bounds.minZ&&w.Z<bounds.maxZ&&w.Y>-1&&w.Y<(bounds.ceiling>0?bounds.ceiling+1:30);
            var o=new List<Clash>();const float Cell=2;var grid=new Dictionary<(int,int,int),List<int>>();
            for(int i=0;i<boxes.Count;i++)
            {
                var b=boxes[i];for(int x=(int)Math.Floor(b.Min.X/Cell);x<=(int)Math.Floor(b.Max.X/Cell);x++)for(int y=(int)Math.Floor(b.Min.Y/Cell);y<=(int)Math.Floor(b.Max.Y/Cell);y++)for(int z=(int)Math.Floor(b.Min.Z/Cell);z<=(int)Math.Floor(b.Max.Z/Cell);z++)
                {if(!grid.TryGetValue((x,y,z),out var l))grid[(x,y,z)]=l=new List<int>();l.Add(i);}
            }
            var seen=new HashSet<long>();
            foreach(var cell in grid.Values)for(int ia=0;ia<cell.Count;ia++)for(int ib=ia+1;ib<cell.Count;ib++)
            {
                int a=Math.Min(cell[ia],cell[ib]),b=Math.Max(cell[ia],cell[ib]);if(!seen.Add(((long)a<<32)|(uint)b))continue;var A=boxes[a];var B=boxes[b];
                if(A.Min.X>B.Max.X+gap||B.Min.X>A.Max.X+gap||A.Min.Y>B.Max.Y+gap||B.Min.Y>A.Max.Y+gap||A.Min.Z>B.Max.Z+gap||B.Min.Z>A.Max.Z+gap)continue;
                for(int fa=0;fa<6;fa++)for(int fb=0;fb<6;fb++)
                {
                    if((A.Skip>>fa&1)!=0||(B.Skip>>fb&1)!=0)continue;
                    Face(A,fa,out var ca,out var na,out var ua,out var va,out float hua,out float hva);Face(B,fb,out var cb,out var nb,out var ub,out var vb,out float hub,out float hvb);
                    if(Vector3.Dot(na,nb)<.9999f)continue;float g=Vector3.Dot(na,cb-ca);if(Math.Abs(g)>gap)continue;
                    var poly=new List<Vector2>();foreach(var (su,sv) in new[]{(-1,-1),(1,-1),(1,1),(-1,1)}){var p=cb+ub*(su*hub)+vb*(sv*hvb)-ca;poly.Add(new Vector2(Vector3.Dot(p,ua),Vector3.Dot(p,va)));}
                    var shared=Clip(poly,hua,hva);float s=shared.Count>2?Area(shared):0;if(s<=area)continue;
                    if(minWidth>0&&Math.Min(shared.Max(q=>q.X)-shared.Min(q=>q.X),shared.Max(q=>q.Y)-shared.Min(q=>q.Y))<minWidth)continue;
                    // Seen? Points of the shared patch a millimetre outside it, not inside a third box.
                    var mid=Vector2.Zero;foreach(var p in shared)mid+=p;mid/=shared.Count;bool visible=false;
                    foreach(var k in new[]{0f,.6f})foreach(var p in shared.Select(q=>mid+(q-mid)*k).Distinct())
                    {
                        var w=ca+ua*p.X+va*p.Y+na*.001f;if(!Reach(w))continue;bool covered=false;for(int t=0;t<boxes.Count&&!covered;t++)if(t!=a&&t!=b&&Inside(boxes[t],w,1e-5f))covered=true;if(!covered){visible=true;break;}
                    }
                    if(!visible)continue;o.Add(new Clash{A=a,B=b,FaceA=fa,FaceB=fb,Gap=g,Area=s,At=ca+ua*mid.X+va*mid.Y,Normal=na});
                }
            }
            return o;
        }
        /// <summary>
        /// The regression checks (2026-10-14): on every Unity map, as the dressing and the kits draw it, no two visible faces of
        /// one facing overlap closer than NativeSurfaceLayers.Step; nothing is drawn twice; the separation is render only (the
        /// blocks' data untouched, every kit part kept with its parent and look, moved and grown by millimetres at most) and
        /// deterministic; the Factory lathes' beds keep their machine-green tops; the Ward Zero lane dashes lie on their decks;
        /// and the runtime draws use the lifts.
        /// </summary>
        internal static void Run(string root)
        {
            int checks=0;void Check(bool ok,string what){if(!ok)throw new Exception("Surfaces: "+what);checks++;}
            var options=new JsonSerializerOptions{IncludeFields=true};string Read(string f)=>File.ReadAllText(Path.Combine(root,f));
            var maps=JsonSerializer.Deserialize<UnityMapContent>(Read("Assets/Conversion/Resources/UnityMaps.json"),options).maps;
            Check(maps.Select(m=>m.id).OrderBy(x=>x).SequenceEqual(new[]{"brickfall","cybercity","expedition-city","expedition-foundry","expedition-transit","expedition-ward-zero","factory","siren"}),"all eight Unity maps are audited");
            foreach(var map in maps)
            {
                string Snap()=>string.Join("|",map.blocks.Select(b=>b.id+":"+b.pos.x+","+b.pos.y+","+b.pos.z+":"+b.size.x+","+b.size.y+","+b.size.z+":"+(b.rotation==null?"-":b.rotation.x+","+b.rotation.y+","+b.rotation.z+","+b.rotation.w)+":"+b.material+":"+b.role+":"+b.hp+":"+b.parent+":"+b.segment));
                string snap=Snap();var boxes=Boxes(root,map,true);
                Check(Snap()==snap,map.id+": building the dressing and its lifts leaves the blocks (colliders, ids, nav, spawns) as they were");
                var clashes=Clashes(boxes,map.bounds,Near);
                Check(clashes.Count==0,map.id+": no two visible faces of one facing overlap closer than 2 mm"+(clashes.Count>0?" (first: "+boxes[clashes[0].A].Id+" / "+boxes[clashes[0].B].Id+" at "+clashes[0].At+")":""));
                string K(Box b)=>string.Join(",",new[]{b.C.X,b.C.Y,b.C.Z,b.H.X,b.H.Y,b.H.Z,b.Q.X,b.Q.Y,b.Q.Z,b.Q.W}.Select(v=>Math.Round(v*1e4).ToString()))+"/"+b.Look+"/"+b.Color;
                Check(boxes.Select(K).Distinct().Count()==boxes.Count,map.id+": no box is drawn twice in one place with one look");
                var lifts=NativeSurfaceLayers.Solve(NativeSurfaceLayers.BlockBoxes(map.blocks),0,null,true,out int unresolved);
                Check(unresolved==0&&lifts.All(l=>l>=0&&l<=NativeSurfaceLayers.MaxLift+1e-6f)&&lifts.SequenceEqual(NativeSurfaceLayers.Blocks(map.blocks)),map.id+": the blocks' lifts settle within 0-16 mm, the same every time");
            }
            var factory=maps.First(m=>m.id=="factory");FactoryHazard[] hz=null;
            using(var doc=JsonDocument.Parse(Read("Assets/Conversion/Resources/UnityMaps.json")))foreach(var m in doc.RootElement.GetProperty("maps").EnumerateArray())if(m.GetProperty("id").GetString()=="factory")hz=JsonSerializer.Deserialize<FactoryHazard[]>(m.GetProperty("hazards").GetRawText(),options);
            var looks=NativeFactoryKit.Resolve(JsonSerializer.Deserialize<MapLookSet>(Read("Assets/Conversion/Resources/MapLooks.json"),options).maps.First(l=>l.id=="factory").kitLooks);
            var fa=NativeFactoryKit.Build(factory.blocks,hz,looks,false);var fb=NativeFactoryKit.Build(factory.blocks,hz,looks);var fc=NativeFactoryKit.Build(factory.blocks,hz,looks);
            bool Within(float a,float b,float d)=>Math.Abs(a-b)<=d+1e-5f;float half=NativeSurfaceLayers.MaxLift/2,full=NativeSurfaceLayers.MaxLift;
            Check(fa.Count==fb.Count&&Enumerable.Range(0,fa.Count).All(i=>{var a=fa.Items[i];var b=fb.Items[i];return a.Look==b.Look&&a.Color==b.Color&&a.Flags==b.Flags&&a.Shadow==b.Shadow&&a.Qx==b.Qx&&a.Qy==b.Qy&&a.Qz==b.Qz&&a.Qw==b.Qw
                &&Within(a.X,b.X,half)&&Within(a.Y,b.Y,half)&&Within(a.Z,b.Z,half)&&b.Hx>=a.Hx&&b.Hy>=a.Hy&&b.Hz>=a.Hz&&Within(a.Hx,b.Hx,full)&&Within(a.Hy,b.Hy,full)&&Within(a.Hz,b.Hz,full);}),"factory: the layers keep every part, its look and turn, moving it at most 8 mm and growing it at most 16 mm");
            Check(fb.Count==fc.Count&&Enumerable.Range(0,fb.Count).All(i=>fb.Items[i].Equals(fc.Items[i])),"factory: the layered kit is the same every build");
            int lathes=0;
            foreach(var bed in factory.blocks.Where(b=>b.id.StartsWith("factory-lathe-",StringComparison.Ordinal)&&!b.id.EndsWith("-head",StringComparison.Ordinal)))
            {
                lathes++;float top=bed.pos.y+bed.size.y/2,x0=bed.pos.x-bed.size.x/2,x1=bed.pos.x+bed.size.x/2,z0=bed.pos.z-bed.size.z/2,z1=bed.pos.z+bed.size.z/2;
                bool covered=Enumerable.Range(0,fb.Count).Select(i=>fb.Items[i]).Any(p=>p.Look==NativeBlockLooks.Demolition&&Math.Abs(p.Y+p.Hy-top)<.01f&&p.X-p.Hx<x1-.05f&&p.X+p.Hx>x0+.05f&&p.Z-p.Hz<z1-.05f&&p.Z+p.Hz>z0+.05f);
                Check(!covered,bed.id+": the hazard collar runs along the bed's sides; its machine-green top is not laid over");
            }
            Check(lathes==6,"factory: the six lathe beds are checked");
            var entries=JsonSerializer.Deserialize<NativeWardEntrySet>(Read("Assets/Conversion/Resources/PveContent.json"),options)?.entries??Array.Empty<NativeWardEntry>();
            var siren=maps.First(m=>m.id=="siren");var wa=NativeWardZeroKit.Build(siren.blocks,siren.hazards,entries,false);var wb=NativeWardZeroKit.Build(siren.blocks,siren.hazards,entries);
            Check(wa.Parts.Length==wb.Parts.Length&&Enumerable.Range(0,wa.Parts.Length).All(i=>{var a=wa.Parts[i];var b=wb.Parts[i];return a.Parent==b.Parent&&a.Look==b.Look&&a.Color==b.Color&&a.Drive==b.Drive&&a.Hazard==b.Hazard&&a.Entry==b.Entry&&a.Mx==b.Mx&&a.My==b.My&&a.Mz==b.Mz
                &&Within(a.X,b.X,half)&&Within(a.Y,b.Y,half)&&Within(a.Z,b.Z,half)&&b.Hx>=a.Hx&&b.Hy>=a.Hy&&b.Hz>=a.Hz&&Within(a.Hx,b.Hx,full)&&Within(a.Hy,b.Hy,full)&&Within(a.Hz,b.Hz,full);}),"siren: the layers keep every part with its parent, look, drive and travel, moving it at most 8 mm");
            var dashes=wb.Parts.Where(p=>p.Color==0xC8CC9A&&Math.Abs(p.Hx-.6f)<.01f&&NativeWardZeroKit.Stem(siren.blocks[p.Parent].id).StartsWith("deck-",StringComparison.Ordinal)).ToList();
            Check(dashes.Count>0&&dashes.All(p=>{var d=siren.blocks[p.Parent];return Math.Abs(p.X)<=d.size.x/2+1e-3f;}),"siren: each concourse lane dash lies on the deck it belongs to ("+dashes.Count+")");
            var cyber=maps.First(m=>m.id=="cybercity");var exits=NativeCyberCityExits.Build(cyber.blocks,cyber.nav,cyber.hazards);
            var ca=NativeCyberCityKit.Build(cyber.blocks,cyber.hazards,exits,false);var cb=NativeCyberCityKit.Build(cyber.blocks,cyber.hazards,exits);
            Check(ca.Parts.Length==cb.Parts.Length&&ca.Glows.Length==cb.Glows.Length&&Enumerable.Range(0,ca.Parts.Length).All(i=>{var a=ca.Parts[i];var b=cb.Parts[i];return a.Parent==b.Parent&&a.Look==b.Look&&a.Color==b.Color&&a.Drive==b.Drive
                &&Within(a.X,b.X,half)&&Within(a.Y,b.Y,half)&&Within(a.Z,b.Z,half)&&b.Hx>=a.Hx&&b.Hy>=a.Hy&&b.Hz>=a.Hz&&Within(a.Hx,b.Hx,full)&&Within(a.Hy,b.Hy,full)&&Within(a.Hz,b.Hz,full);}),"cybercity: the layers keep every part (glows still name theirs), moving it at most 8 mm");
            Check(cyber.blocks.Count(b=>b.id.StartsWith("cybercity-pit-",StringComparison.Ordinal)&&b.id.Contains("-stair-"))>0&&exits.Exits.Length==4,"cybercity: the pit stair ids and the Cut's four exits stand as authored");
            // The backdrops (the skylines around the maps): the same, with their far settings.
            foreach(var which in new[]{"factory","brickfall","cybercity",NativeExpeditionCityKit.MapId})
            {
                var sw=System.Diagnostics.Stopwatch.StartNew();if(which=="factory")NativeFactoryScenery.Build();else if(which=="brickfall")NativeBrickfallScenery.Build();else if(which=="cybercity")NativeCyberCityScenery.Build();else NativeExpeditionCityScenery.Build();long ms=sw.ElapsedMilliseconds;
                var boxes=Backdrop(which,true,out int unresolved,out float most);var clashes=Clashes(boxes,null,Near,1e-4f,(float)NativeSurfaceLayers.Far.MinWidth);
                int toward=clashes.Count(c=>Vector3.Dot(c.Normal,new Vector3(0,10,0)-c.At)>0),differ=clashes.Count(c=>boxes[c.A].Color!=boxes[c.B].Color);
                Check(toward==0&&differ==0&&unresolved==0&&most<=NativeSurfaceLayers.BackdropLift+1e-6f,which+" backdrop: no coincident faces toward the arena or of two colours ("+clashes.Count+" same-colour pairs facing away), lifts within 64 mm");
                Console.WriteLine("   "+which+" backdrop built in "+ms+" ms, lifts at most "+(most*1000).ToString("0")+" mm");
            }
            string R(string f)=>Read("Assets/ChipArena/"+f);
            Check(R("Runtime/NativeArenaDressing.cs").Contains("lifts=NativeSurfaceLayers.Blocks(source)")&&R("Runtime/NativeArenaDressing.cs").Contains("Matrix4x4.TRS(pos+rot*e.Offset,rot,Vector3.one)"),"the dressing draws the blocks with their lifts");
            Check(R("Runtime/NativeWardZeroPresentation.cs").Contains("wzBlockLifts=NativeSurfaceLayers.Blocks(blocks)"),"the Ward Zero menu backdrop draws its blocks with the same lifts");
            Check(R("Runtime/NativeFactoryPresentation.cs").Contains("NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz)"),"the Factory draw and the layers agree on the parts' chamfer");
            Console.WriteLine("PASS surfaces: "+checks+" checks (coincident faces, duplicates, render-only lifts on six maps)");
        }
        /// <summary>The audit: every Unity map's coincident visible faces before and after the separation, the largest groups
        /// listed (the `--surface-audit` mode).</summary>
        internal static void Audit(string root)
        {
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true}).maps;
            foreach(var map in maps)foreach(bool layers in new[]{false,true})
            {
                if(layers)
                {
                    // The lifts as drawn: each face's move out, from the boxes with and without the layers (same order).
                    var plain=Boxes(root,map,false);var lifted=Boxes(root,map,true);var bins=new int[6];int moved=0;float most=0;
                    for(int i=0;i<plain.Count;i++)
                    {
                        var d=Vector3.Transform(lifted[i].C-plain[i].C,Quaternion.Conjugate(plain[i].Q));var g=lifted[i].H-plain[i].H;
                        foreach(float l in new[]{g.X-d.X,g.X+d.X,g.Y-d.Y,g.Y+d.Y,g.Z-d.Z,g.Z+d.Z})if(l>1e-4f){moved++;most=Math.Max(most,l);bins[Math.Min(5,(int)Math.Round(l*1000/2))]++;}
                    }
                    Console.WriteLine("   lifts: "+moved+" faces moved out (of "+plain.Count*6+"), at most "+(most*1000).ToString("0")+" mm; by 2/4/6/8 mm: "+string.Join("/",bins.Skip(1).Take(4)));
                }
                var sw=System.Diagnostics.Stopwatch.StartNew();var boxes=Boxes(root,map,layers);long built=sw.ElapsedMilliseconds;var clashes=Clashes(boxes,map.bounds,Near);int exact=clashes.Count(c=>Math.Abs(c.Gap)<1e-4f);
                int differ=clashes.Count(c=>boxes[c.A].Color!=boxes[c.B].Color||boxes[c.A].Look!=boxes[c.B].Look);int green=clashes.Count(c=>Green(boxes[c.A].Color)||Green(boxes[c.B].Color));
                int steel=clashes.Count(c=>boxes[c.A].Look==NativeBlockLooks.SteelGreen||boxes[c.B].Look==NativeBlockLooks.SteelGreen);
                Console.WriteLine("== "+map.id+(layers?" after":" before")+": "+boxes.Count+" boxes ("+boxes.Count(b=>b.Kit)+" kit, built in "+built+" ms), "+clashes.Count+" coincident visible face pairs ("+exact+" exact), "+clashes.Sum(c=>c.Area).ToString("0.00")+" m²; "+differ+" with different colour or look, "+green+" involving green, "+steel+" involving the steel-green panel look");
                foreach(var g in clashes.GroupBy(c=>Kind(boxes[c.A])+" | "+Kind(boxes[c.B])).OrderByDescending(g=>g.Sum(c=>c.Area)).Take(layers?40:12))
                {
                    var c=g.First();Console.WriteLine("   "+g.Count()+"x "+g.Sum(x=>x.Area).ToString("0.000")+" m²  "+g.Key+"  e.g. "+boxes[c.A].Id+" / "+boxes[c.B].Id+" gap "+(c.Gap*1000).ToString("0.00")+" mm at "+c.At.ToString("0.00")+" n "+c.Normal.ToString("0.00")+" look "+boxes[c.A].Look+"/"+boxes[c.B].Look+" colour "+boxes[c.A].Color.ToString("x6")+"/"+boxes[c.B].Color.ToString("x6"));
                }
            }
        }
        /// <summary>The differing pairs (colour or look), grouped and largest first (`--surface-audit differ`).</summary>
        internal static void Differ(string root)
        {
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true}).maps;
            foreach(var map in maps)
            {
                var boxes=Boxes(root,map);var clashes=Clashes(boxes,map.bounds).Where(c=>boxes[c.A].Color!=boxes[c.B].Color||boxes[c.A].Look!=boxes[c.B].Look).ToList();
                Console.WriteLine("== "+map.id+": "+clashes.Count+" differing pairs, "+clashes.Sum(c=>c.Area).ToString("0.00")+" m²");
                foreach(var g in clashes.GroupBy(c=>Kind(boxes[c.A])+" "+boxes[c.A].Color.ToString("x6")+"/"+boxes[c.A].Look+" | "+Kind(boxes[c.B])+" "+boxes[c.B].Color.ToString("x6")+"/"+boxes[c.B].Look).OrderByDescending(g=>g.Sum(c=>c.Area)))
                {
                    var c=g.First();var A=boxes[c.A];var B=boxes[c.B];
                    Console.WriteLine("   "+g.Count()+"x "+g.Sum(x=>x.Area).ToString("0.000")+" m²  "+g.Key+"  e.g. "+A.Id+" c"+A.C.ToString("0.000")+" h"+A.H.ToString("0.000")+" / "+B.Id+" c"+B.C.ToString("0.000")+" h"+B.H.ToString("0.000")+" n "+c.Normal.ToString("0"));
                }
            }
        }
        /// <summary>Boxes drawn twice: same place, size and turn (within 0.1 mm), listed per map (`--surface-audit dupes`).</summary>
        internal static void Dupes(string root)
        {
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true}).maps;
            foreach(var map in maps)
            {
                var boxes=Boxes(root,map,false);
                string K(Box b)=>string.Join(",",new[]{b.C.X,b.C.Y,b.C.Z,b.H.X,b.H.Y,b.H.Z,b.Q.X,b.Q.Y,b.Q.Z,b.Q.W}.Select(v=>Math.Round(v*1e4).ToString()));
                var groups=Enumerable.Range(0,boxes.Count).GroupBy(i=>K(boxes[i])).Where(g=>g.Count()>1).ToList();
                Console.WriteLine("== "+map.id+": "+groups.Count+" places drawn more than once ("+groups.Sum(g=>g.Count()-1)+" extra boxes; "+groups.Count(g=>g.Select(i=>boxes[i].Color+"/"+boxes[i].Look).Distinct().Count()>1)+" with differing looks)");
                foreach(var g in groups.GroupBy(g=>string.Join(" + ",g.Select(i=>Kind(boxes[i])+" "+boxes[i].Color.ToString("x6")+"/"+boxes[i].Look))).OrderByDescending(x=>x.Count()).Take(20))
                {var f=g.First().ToList();Console.WriteLine("   "+g.Count()+"x  "+g.Key+"  e.g. "+string.Join(" / ",f.Select(i=>boxes[i].Id))+" c"+boxes[f[0]].C.ToString("0.000")+" h"+boxes[f[0]].H.ToString("0.000"));}
            }
        }
        /// <summary>A map's backdrop as boxes (`which`: factory, brickfall, cybercity), as its builder draws them (`layers`: with
        /// the lifts; off: as before them), and the lifts' unresolved count. The baked backdrops come through the builder's audit
        /// hook; their boxes have no chamfer and drop the faces the builder skips.</summary>
        internal static List<Box> Backdrop(string which,bool layers,out int unresolved,out float most)
        {
            var o=new List<Box>();unresolved=0;most=0;
            if(which=="cybercity")
            {
                var back=NativeCyberCityScenery.Build(layers);var raw=NativeCyberCityScenery.Build(false);
                for(int i=0;i<back.Length;i++){var b=back[i];o.Add(Make("scenery#"+i+"/k"+b.Kind,true,new Vector3(b.X,b.Y,b.Z),new Vector3(b.Hx,b.Hy,b.Hz),Quaternion.CreateFromAxisAngle(Vector3.UnitY,b.Yaw*(float)Math.PI/180),b.Look,b.Color));
                    most=Math.Max(most,Math.Max(b.Hx-raw[i].Hx,Math.Max(b.Hy-raw[i].Hy,b.Hz-raw[i].Hz))*2);}
                return o;
            }
            if(which==NativeExpeditionCityKit.MapId)
            {
                var back=NativeExpeditionCityScenery.Build(layers);var raw=NativeExpeditionCityScenery.Build(false);
                for(int i=0;i<back.Length;i++){var b=back[i];o.Add(Make("town-scenery#"+i+"/k"+b.Kind,true,new Vector3(b.X,b.Y,b.Z),new Vector3(b.Hx,b.Hy,b.Hz),Quaternion.CreateFromAxisAngle(Vector3.UnitY,b.Yaw*(float)Math.PI/180),b.Look,b.Color));
                    most=Math.Max(most,Math.Max(b.Hx-raw[i].Hx,Math.Max(b.Hy-raw[i].Hy,b.Hz-raw[i].Hz))*2);}
                return o;
            }
            NativeSurfaceLayers.Box[] boxes=null;float[] lifts=null;NativeSceneryBuilder.Audit=(b,l)=>{if(boxes==null||b.Length>boxes.Length){boxes=b;lifts=l;}};
            try{if(which=="factory")NativeFactoryScenery.Build();else NativeBrickfallScenery.Build();}finally{NativeSceneryBuilder.Audit=null;}
            NativeSurfaceLayers.Solve(boxes,0,null,false,out unresolved,NativeSurfaceLayers.BackdropLift,NativeSurfaceLayers.Far);
            for(int i=0;i<boxes.Length;i++)
            {
                var b=boxes[i];var q=new Quaternion((float)b.Qx,(float)b.Qy,(float)b.Qz,(float)b.Qw);float sx=0,sy=0,sz=0,gx=0,gy=0,gz=0;
                if(layers)NativeSurfaceLayers.Apply(lifts,i,out sx,out sy,out sz,out gx,out gy,out gz);most=Math.Max(most,Enumerable.Range(0,6).Max(k=>lifts[i*6+k]));
                var x=Make(which+"-scenery#"+i,true,new Vector3((float)b.X,(float)b.Y,(float)b.Z)+Vector3.Transform(new Vector3(sx,sy,sz),q),new Vector3((float)b.Hx+gx,(float)b.Hy+gy,(float)b.Hz+gz),q,0,(int)(b.Material&0xFFFFFF),-1);
                x.Skip=b.Skip;o.Add(x);
            }
            return o;
        }
        /// <summary>The backdrops' coincident visible faces before and after the layers (`--scenery`); "toward" counts the pairs
        /// whose faces turn toward the arena (from 10 m up at its middle).</summary>
        internal static void Scenery()
        {
            foreach(var which in new[]{"factory","brickfall","cybercity"})foreach(bool layers in new[]{false,true})
            {
                var boxes=Backdrop(which,layers,out int unresolved,out float most);var clashes=Clashes(boxes,null,Near,1e-4f,(float)NativeSurfaceLayers.Far.MinWidth);
                int toward=clashes.Count(c=>Vector3.Dot(c.Normal,new Vector3(0,10,0)-c.At)>0);
                Console.WriteLine("== "+which+" backdrop "+(layers?"after":"before")+": "+boxes.Count+" boxes, "+clashes.Count+" coincident visible face pairs ("+toward+" toward the arena), "+clashes.Sum(c=>c.Area).ToString("0.0")+" m²; "+clashes.Count(c=>boxes[c.A].Color!=boxes[c.B].Color)+" of different colour"+(layers?"; lifts at most "+(most*1000).ToString("0")+" mm, "+unresolved+" unresolved":""));
                foreach(var c in clashes.Where(c=>Vector3.Dot(c.Normal,new Vector3(0,10,0)-c.At)>0).OrderByDescending(c=>c.Area).Take(6))
                    Console.WriteLine("   "+c.Area.ToString("0.000")+" m² gap "+(c.Gap*1000).ToString("0.0")+" mm at "+c.At.ToString("0.0")+" n "+c.Normal.ToString("0")+" "+boxes[c.A].Id+" ("+boxes[c.A].Color.ToString("x6")+", "+(boxes[c.A].H*2).ToString("0.0")+") / "+boxes[c.B].Id+" ("+boxes[c.B].Color.ToString("x6")+", "+(boxes[c.B].H*2).ToString("0.0")+")");
            }
        }
        internal static bool Green(int c)
        {
            if(c<0)return false;float r=(c>>16&255)/255f,g=(c>>8&255)/255f,b=(c&255)/255f;float mx=Math.Max(r,Math.Max(g,b)),mn=Math.Min(r,Math.Min(g,b));
            // Hue 80-170 degrees, saturated: the machine greens, the planters, the tank fluid; not the grey-teal steels.
            if(mx<.12f||(mx-mn)/mx<.3f)return false;float h=mx==r?60*((g-b)/(mx-mn)):mx==g?60*(2+(b-r)/(mx-mn)):60*(4+(r-g)/(mx-mn));if(h<0)h+=360;return h>=80&&h<=170;
        }
        private static string Kind(Box b)
        {
            string id=b.Id;int hash=id.IndexOf('#');string stem=hash>=0?id.Substring(0,hash):id;
            var parts=stem.Split('-');stem=string.Join("-",parts.Where(p=>!(p.Length>0&&(char.IsDigit(p[0])||p.Length<=2&&p!="nw"&&p!="ne"&&p!="sw"&&p!="se"))));
            return (b.Kit?"kit:":"")+stem;
        }
    }
}
