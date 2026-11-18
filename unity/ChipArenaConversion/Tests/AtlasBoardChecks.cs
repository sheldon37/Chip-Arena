#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using A=ChipArena.UnityConversion.NativeAtlasBoard;
using T=ChipArena.UnityConversion.NativeTilesets;
namespace ChipArena.UnityConversion
{
    // ATLAS, the Deployment Station's tileset departures board (docs/EXPEDITION-OVERHAUL.md R1 and W2; Core NativeTilesets,
    // NativeAtlasBoard, NativeAtlasModels, NativeAtlasArt, NativeAtlasSounds; Runtime NativeAtlasScene, NativeAtlasAudio,
    // NativeAtlasConsoleUI): the registry's City row and its honest empty lines; the board's instances equal the Instance
    // Machine's real pool; the layout; clean models inside their footprint and budgets; placed under the ceiling beam and seen
    // whole from the Overview with the DEPLOY sign clear; the arrival (blank, a clatter forward through the drum, landed by the
    // bell, nothing after); its shot only over the Overview; machine 2's sparks and Claude's sounds heard once; the glyph atlas;
    // no per-frame allocation; and the runtime wiring (a minimal hook in the station, nothing asks the authority).
    internal static class AtlasBoardChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("ATLAS board: "+what);count++;}
        public static void Run(string root)
        {
            count=0;Registry();Pool(root);Layout();Models();Placement();Flaps();Arrival();Framing();Sparks();Sounds();Art();Allocation();Wiring(root);
            Console.WriteLine("PASS ATLAS board: "+count+" checks (City's row and the empty lines from the registry, the Instance Machine's pool, the layout, clean models in "+
                "budget, placed under the beam and seen whole, the arrival landed by the bell, its shot over the Overview only, machine 2's sparks, sounds heard once, the "+
                "glyph atlas, no per-frame allocation, the runtime wiring)");
        }

        private static string Text(int[] targets,A.Layout L,int line,int column)
        {
            var s=new System.Text.StringBuilder();int start=L.LineStart[line];
            for(int i=0;i<targets.Length;i++){var c=L.Cells[start+i];if(c.Column==column)s.Append(targets[i]<A.Glyphs?A.Drum[targets[i]]:'#');}
            return s.ToString().TrimEnd();
        }
        private static void Registry()
        {
            var city=T.All[0];
            Check(city.Id==T.City&&city.State==T.State.Live&&ReferenceEquals(T.Of(null),city)&&ReferenceEquals(T.Of("nowhere"),city)&&ReferenceEquals(T.Of(T.City),city),"City is the registry's first, live row and the fallback for null or unknown ids");
            Check(T.Live==1&&T.All.Select(t=>t.Id).Distinct().Count()==T.All.Length,"City alone is live (Q3); ids are distinct");
            Check(city.Label=="CITY · MERIDIAN"&&T.MenuLine=="TILESET: CITY · MERIDIAN","the label and the Expeditions screen's line");
            var row=Enumerable.Range(0,4).Select(c=>T.Cell(city,c)).ToArray();
            Check(row.SequenceEqual(new[]{"CITY · MERIDIAN","THE INFECTED","5 INSTANCES","THE SIREN"}),"line 1 reads the plan's row ("+string.Join(" | ",row)+")");
            Check(T.Columns.SequenceEqual(new[]{"TILESET","ENEMIES","INSTANCES","FINAL"})&&T.Lines==4,"four columns and four lines");
            Check(T.Cell(null,0)==T.NotInService&&T.Cell(null,1)==""&&T.Cell(null,2)==""&&T.Cell(null,3)==""&&T.NotInService=="NOT IN SERVICE","an empty line: NOT IN SERVICE, no invented names or dates");
            foreach(var t in T.All)
            {
                foreach(var s in new[]{t.Name,t.Place,t.Enemies,t.Final,t.Label})Check(!string.IsNullOrEmpty(s)&&s==s.ToUpperInvariant()&&s.All(ch=>A.Drum.IndexOf(ch)>=0&&NativeDeployArt.Lettered(ch)),t.Id+": '"+s+"' is caps in the drum and the stroke font");
                Check(t.Accent>0&&t.Accent<=0xffffff&&t.Instances!=null&&t.Instances.Length>0,t.Id+": an accent and its instances");
            }
            foreach(var s in T.Columns.Concat(new[]{T.NotInService,"ATLAS","· DEPARTURES"}))Check(s.All(ch=>A.Drum.IndexOf(ch)>=0&&NativeDeployArt.Lettered(ch)),"'"+s+"' is lettered");
            // the final is the catalogue's run's final boss; the selection hook is City's for every state (A3 fills it)
            Check(NativeExpeditionCatalogue.Run.Areas.Single(a=>a.Boss).Name==city.Final,"FINAL is the run's final boss ("+city.Final+")");
            var auth=new NativeExpeditionDeployment("preview","host",31);var states=new List<ExpeditionDeploymentState>{null,auth.Snapshot()};
            var pool=city.Instances.Select(id=>new ExpeditionInstanceTheme{id=id,theme=id=="exp-foundry"?"industrial":id=="exp-transit"?"rail":"quarantine"}).ToArray();
            auth.RollInstances("host",pool);states.Add(auth.Snapshot());auth.RollMutators("host",NativeDeployPresentation.MutatorIds);states.Add(auth.Snapshot());auth.Deploy("host",true);states.Add(auth.Snapshot());
            foreach(var s in states)Check(ReferenceEquals(T.Selected(s),city),"the selection hook reads City for every state until A3");
            var lines=T.Board(city,new T.Tileset[T.Lines]);Check(ReferenceEquals(lines[0],city)&&lines.Skip(1).All(x=>x==null),"the board: City on line 1, three empty lines");
            var dev=new T.Tileset{Id="dev-proof",State=T.State.Dev,Name="PROOF",Place="DEV",Accent=0xffffff,Enemies="X",Final="Y",Instances=new[]{"exp-ward-zero"}};
            var withDev=T.Board(dev,null);Check(ReferenceEquals(withDev[0],dev)&&ReferenceEquals(withDev[1],city)&&withDev.Skip(2).All(x=>x==null),"a selected tileset shows on line 1 whatever its state (A3's DEV proof); the live ones follow");
            Check(ReferenceEquals(T.Board(null,null)[0],city),"no selection reads as City");
        }
        // The board's instance list equals the Instance Machine's pool: the station's real room (the shipped layout and maps)
        // rolled until every destination has come up, and the live station's reel list.
        private static void Pool(string root)
        {
            var city=T.All[0];var options=new JsonSerializerOptions{IncludeFields=true};
            string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
            string mapsJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,options);var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
            var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
            var room=new ExpeditionDeploymentRoom("atlas","host",4242,NativeExpeditionCatalogue.Run.Id,layout,content);var seen=new HashSet<string>();
            for(int roll=0;roll<240;roll++){Check(room.Execute("host",room.Snapshot().revision,ExpeditionDeploymentIntent.RollInstances),"roll "+roll);foreach(var d in room.Snapshot().districts)seen.Add(d.instance);}
            Check(seen.SetEquals(city.Instances),"ATLAS's instances are the Instance Machine's pool ("+string.Join(",",seen.OrderBy(x=>x))+")");
            Check(T.Cell(city,2)==seen.Count+" INSTANCES","the INSTANCES cell counts the pool");
            var live=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeploymentLive.cs"));var block=live.Substring(live.IndexOf("LiveDeployInstances={",StringComparison.Ordinal));block=block.Substring(0,block.IndexOf("};",StringComparison.Ordinal));
            var reels=Regex.Matches(block,@"id=(""[^""]+""|NativeBackroomsGenerator\.AreaId|NativePoolroomsGenerator\.AreaId)").Select(m=>m.Groups[1].Value=="NativeBackroomsGenerator.AreaId"?NativeBackroomsGenerator.AreaId:m.Groups[1].Value=="NativePoolroomsGenerator.AreaId"?NativePoolroomsGenerator.AreaId:m.Groups[1].Value.Trim('"')).ToArray();
            Check(reels.Length==city.Instances.Length&&new HashSet<string>(reels).SetEquals(city.Instances),"and the live station's reels ("+string.Join(",",reels)+")");
            var known=Enumerable.Range(0,NativeDeployPresentation.DestinationCount).Select(i=>NativeDeployPresentation.DestinationAt(i).Id).ToArray();
            foreach(var id in city.Instances)Check(known.Contains(id),id+" is a destination the reels draw");
        }
        private static void Layout()
        {
            var L=A.Board;Check(ReferenceEquals(L,A.Board),"the layout is built once");
            Check(L.LineStart.Length==T.Lines&&L.LineCount.Sum()==L.Cells.Length,"four lines cover every cell");
            for(int line=0;line<T.Lines;line++)
            {
                var first=L.Cells[L.LineStart[line]];Check(first.Kind==A.Kind.Emblem&&first.Large==(line==0),"line "+line+" starts with its emblem flap"+(line==0?", large":", small"));
                for(int i=0;i<L.LineCount[line];i++){var c=L.Cells[L.LineStart[line]+i];Check(c.Line==line&&c.Index==i&&c.Large==(line==0),"line "+line+" cell "+i+" bookkeeping");}
                for(int col=0;col<T.Columns.Length;col++)Check(A.Columns(L,line,col)>=(line==0?L.Count[col]:Math.Max(T.NotInService.Length,1)*(col==0?1:0)),"line "+line+" column "+col+" holds its text");
            }
            for(int col=0;col<T.Columns.Length;col++)
            {
                int longest=Math.Max(T.Cell(null,col).Length,T.All.Max(t=>T.Cell(t,col).Length));Check(L.Count[col]==Math.Max(1,longest),"column "+col+" is as wide as its longest text ("+L.Count[col]+")");
                Check(L.Right[col]>L.Left[col]&&(col==0||L.Left[col]>=L.Right[col-1]+A.ColumnGap-1e-4f),"column "+col+" in order with its gap");
            }
            float x0=-A.Width/2+A.Bezel,x1=A.Width/2-A.Bezel,y0=A.Bezel,y1=A.Height-A.Bezel;
            foreach(var c in L.Cells)
            {
                Check(c.X0>=x0&&c.X1<=x1&&c.Y0>=y0&&c.Y1<=y1&&c.X1>c.X0&&c.Y1>c.Y0,"cell "+c.Line+":"+c.Index+" on the face");
                if(c.Kind==A.Kind.Glyph){Check(c.X0>=L.Left[c.Column]-1e-4f&&c.X1<=L.Right[c.Column]+1e-4f,"cell "+c.Line+":"+c.Index+" in its column");Check(Math.Abs((c.X1-c.X0)/(c.Y1-c.Y0)-A.FlapAspect)<.03f,"a glyph flap's shape ("+((c.X1-c.X0)/(c.Y1-c.Y0)).ToString("0.000")+")");}
                else Check(Math.Abs(c.X1-c.X0-(c.Y1-c.Y0))<1e-4f&&c.X1<A.ColumnsX,"an emblem flap is square, left of the columns");
            }
            // no two flaps overlap (each line's cells left to right, the lines stacked with clear gaps under the header and labels)
            for(int i=0;i<L.Cells.Length;i++)for(int j=i+1;j<L.Cells.Length;j++){var a=L.Cells[i];var b=L.Cells[j];if(a.X0<b.X1-1e-5f&&b.X0<a.X1-1e-5f&&a.Y0<b.Y1-1e-5f&&b.Y0<a.Y1-1e-5f){Check(false,"flaps "+i+" and "+j+" overlap");}}
            Check(A.LargeTop<A.LabelBottom&&A.LabelBottom<A.HeaderBottom&&A.HeaderBottom<A.HeaderTop&&A.SmallTop<A.LargeTop-A.LargeFlap-.01f,"header, labels, the large line, then the small ones");
            // the targets spell each line, left-aligned in its columns, from the registry alone
            var city=T.All[0];int empty=T.All.Length;
            for(int col=0;col<4;col++){Check(Text(L.Large[0],L,0,col)==T.Cell(city,col),"line 1 column "+col+" spells '"+T.Cell(city,col)+"'");Check(Text(L.Small[empty],L,1,col)==T.Cell(null,col),"an empty line's column "+col+" reads '"+T.Cell(null,col)+"'");}
            Check(L.Large[0][0]==A.EmblemTile(0)&&L.Small[empty][0]==A.BlankEmblem&&L.Large[empty][0]==A.BlankEmblem,"City's emblem flap, a blank one on an empty line");
            Check(A.Tiles==A.Glyphs+1+T.All.Length&&A.Drum[0]==' '&&A.Drum.Distinct().Count()==A.Drum.Length&&A.GlyphOf('c')==A.GlyphOf('C')&&A.GlyphOf('~')==0,"the drum: blank first, every glyph once, unknowns blank");
        }
        private static void Clean(WeaponMesh mesh,string what)
        {
            foreach(var s in mesh.Sections)
            {
                Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Emission.Count&&s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),what+" section arrays agree");
                Check(s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),what+" finite positions and unit normals");
                Check(s.Colors.All(c=>c.X>=0&&c.Y>=0&&c.Z>=0&&c.X<=1.0001f&&c.Y<=1.0001f&&c.Z<=1.0001f&&c.W==1)&&s.Emission.All(v=>v.W==0),what+" colours in range, opaque, no emission tags");
                if(s.Bone>=NativeAtlasModels.Lights)for(int i=0;i<s.Colors.Count;i++){var c=s.Colors[i];var e=s.Emission[i];if(Math.Abs(c.X-e.X)+Math.Abs(c.Y-e.Y)+Math.Abs(c.Z-e.Z)>1e-4f||e.X+e.Y+e.Z<=0){Check(false,what+" light bone "+s.Bone+" is unlit");break;}}
                if(s.Bone==NativeAtlasModels.Screen)Check(s.Data.Count==s.Positions.Count&&s.Data.All(d=>d.X>=0&&d.X<1&&d.Y>=0&&d.Y<1),what+" screen faces carry their uv (inside the atlas, never on its wrap)");
            }
        }
        private static bool Same(WeaponMesh a,WeaponMesh b)
        {
            if(a.Sections.Count!=b.Sections.Count)return false;
            for(int i=0;i<a.Sections.Count;i++){var x=a.Sections[i];var y=b.Sections[i];if(x.Bone!=y.Bone||!x.Positions.SequenceEqual(y.Positions)||!x.Indices.SequenceEqual(y.Indices)||!x.Colors.SequenceEqual(y.Colors)||!x.Data.SequenceEqual(y.Data))return false;}
            return true;
        }
        private static void Models()
        {
            var cab=NativeAtlasModels.Cabinet();var flaps=NativeAtlasModels.Flaps();Clean(cab,"the cabinet");Clean(flaps,"the flaps");
            Check(Same(cab,NativeAtlasModels.Cabinet())&&Same(flaps,NativeAtlasModels.Flaps()),"the cabinet and the flaps build the same twice");
            Check(cab.Triangles>1000&&cab.Triangles<=NativeAtlasModels.CabinetBudget,"the cabinet's budget ("+cab.Triangles+")");
            int cells=A.Board.Cells.Length;Check(flaps.Triangles==cells*6&&flaps.Triangles<=NativeAtlasModels.FlapsBudget,"three quads per flap ("+flaps.Triangles+")");
            Check(new[]{NativeAtlasModels.Body,NativeAtlasModels.Lights,NativeAtlasModels.Accent,NativeAtlasModels.Dim}.All(b=>cab.Sections.Any(s=>s.Bone==b))&&flaps.Sections.All(s=>s.Bone==NativeAtlasModels.Screen),"the cabinet's body and lamp bones; the flaps are screen faces");
            A.Footprint(out var lo,out var hi);const float e=.002f;
            foreach(var (m,id) in new[]{(cab,"the cabinet"),(flaps,"the flaps")}){m.Bounds(-1,out var min,out var max);Check(min.X>=lo.X-e&&min.Y>=lo.Y-e&&min.Z>=lo.Z-e&&max.X<=hi.X+e&&max.Y<=hi.Y+e&&max.Z<=hi.Z+e,id+" inside the footprint ("+min+" .. "+max+")");}
            cab.Bounds(-1,out var cmin,out var cmax);Check(cmax.X-cmin.X>(hi.X-lo.X)*.95f&&cmax.Y-cmin.Y>(hi.Y-lo.Y)*.9f,"the cabinet fills its footprint");
            cab.Bounds(NativeAtlasModels.Accent,out var lamp0,out var lamp1);var lc=(lamp0+lamp1)/2;Check(Math.Abs(lc.X-A.LampX)<.01f&&Math.Abs(lc.Y-NativeAtlasModels.LineY(0))<.01f,"the in-service lamp at line 1's start");
            // the rods' clamps on the beam's bottom flange
            var tops=cab.Sections.SelectMany(s=>s.Positions).Where(p=>p.Y>A.Height+.2f).ToArray();
            Check(tops.Length>0&&tops.Max(p=>p.Y)<=A.BeamBottom-A.At.Y+1e-3f&&tops.Max(p=>p.Y)>=A.BeamBottom-A.At.Y-1e-3f,"the drop rods reach the beam's bottom flange");
            Check(tops.All(p=>Math.Abs(p.X)>A.RodX-.2f&&Math.Abs(p.X)<A.RodX+.2f&&p.Z+A.At.Z>A.BeamZ-.19f&&p.Z+A.At.Z<A.BeamZ+.19f),"and hang under its flange at x ±"+A.RodX+" ("+tops.Min(p=>p.Z+A.At.Z)+" .. "+tops.Max(p=>p.Z+A.At.Z)+")");
        }
        // Möller–Trumbore: the ray from o along d (unit) hits the triangle before `far`.
        private static bool Hit(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c,float far)
        {
            var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-9f)return false;float inv=1/det;var t0=o-a;
            float u=Vector3.Dot(t0,p)*inv;if(u<0||u>1)return false;var q=Vector3.Cross(t0,e1);float v=Vector3.Dot(d,q)*inv;if(v<0||u+v>1)return false;float t=Vector3.Dot(e2,q)*inv;return t>1e-4f&&t<far;
        }
        private static bool Blocked(Vector3 eye,Vector3 at,IEnumerable<(Vector3,Vector3,Vector3)> tris){var d=at-eye;float far=d.Length()-.02f;d/=d.Length();foreach(var (a,b,c) in tris)if(Hit(eye,d,a,b,c,far))return true;return false;}
        private static IEnumerable<(Vector3,Vector3,Vector3)> Tris(WeaponMesh m,Matrix4x4 world)
        {
            foreach(var s in m.Sections)for(int i=0;i+2<s.Indices.Count;i+=3)yield return (Vector3.Transform(s.Positions[s.Indices[i]],world),Vector3.Transform(s.Positions[s.Indices[i+1]],world),Vector3.Transform(s.Positions[s.Indices[i+2]],world));
        }
        // A point's place on a 16:9 screen (x 0..1 left to right, y 0..1 top to bottom) for a view, and whether it is in front.
        private static Vector2 Screen(NativeDeployPresentation.View v,Vector3 p,out bool front)
        {
            var f=Vector3.Normalize(v.Target-v.Eye);var r=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,f));var u=Vector3.Cross(f,r);var d=p-v.Eye;float z=Vector3.Dot(d,f);front=z>.1f;
            float t=(float)Math.Tan(v.Fov*Math.PI/360);return new Vector2((Vector3.Dot(d,r)/(z*t*16f/9f)+1)/2,(1-Vector3.Dot(d,u)/(z*t))/2);
        }
        // The console's TILESET card (Runtime NativeAtlasConsole.Top and Height, mirrored here: Runtime is not compiled in the checks).
        private const float NativeAtlasConsoleTop=18,NativeAtlasConsoleHeight=46;
        private static void Placement()
        {
            var root=A.Root;A.Footprint(out var lo,out var hi);var bmin=A.At+lo;var bmax=A.At+hi;
            NativeDeployModels.Footprint(NativeDeployModels.Room,out var r0,out var r1);
            Check(bmin.X>r0.X&&bmax.X<r1.X&&bmax.Y<=r1.Y&&bmax.Z<r1.Z,"inside the room");
            NativeDeployModels.Footprint(NativeDeployModels.Gate,out var g0,out var g1);var gate=NativeDeployPresentation.Gate;
            Check(bmax.Z<gate.Z+g0.Z,"in front of the DEPLOY gate's frame ("+bmax.Z+" < "+(gate.Z+g0.Z)+")");
            Check(A.At.Y>gate.Y+g1.Y-.3f&&A.At.Y+A.Height<A.BeamBottom-.1f,"over the gate's frame, under the ceiling beam");
            // seen whole from the Overview: no room, gate or machine triangle between the eye and any flap, the header or the lamps
            var view=NativeDeployPresentation.ShotOf(NativeDeployPresentation.Shot.Overview);
            var room=Tris(NativeDeployModels.StationRoom(),Matrix4x4.Identity).Concat(Tris(NativeDeployModels.GateFrame(),Matrix4x4.CreateTranslation(gate))).ToList();
            var points=A.Board.Cells.Where(c=>c.Line==0||c.Index%9==0).Select(c=>new Vector3((c.X0+c.X1)/2,(c.Y0+c.Y1)/2,NativeAtlasModels.FlapZ)).ToList();
            foreach(var x in new[]{-3.7f,0f,3.7f})foreach(var y in new[]{A.HeaderTop-.01f,A.HeaderBottom+.01f})points.Add(new Vector3(x,y,-.03f));
            foreach(var p in points){var w=Vector3.Transform(p,root);Check(!Blocked(view.Eye,w,room),"the Overview sees "+p);var s=Screen(view,w,out bool front);Check(front&&s.X>0&&s.X<1&&s.Y>0&&s.Y<1,"in the Overview's frame "+p);}
            // and the board hides none of the DEPLOY sign
            var cabinet=Tris(NativeAtlasModels.Cabinet(),root).ToList();
            foreach(var x in new[]{-.6f,0f,.6f})Check(!Blocked(view.Eye,gate+new Vector3(x,4.45f+.62f+.12f,-.73f),cabinet),"the DEPLOY sign stays clear of the board at x "+x);
            // its own shot sees it whole, the gate under it, clear of the console's side panels and its TILESET card (a 1600 x 900 canvas)
            var shot=A.Shot;float panelLeft=380f/1600,panelRight=(1600-380f)/1600,card=(NativeAtlasConsoleTop+NativeAtlasConsoleHeight)/900;
            foreach(var c in new[]{new Vector3(-A.Width/2,0,0),new Vector3(A.Width/2,0,0),new Vector3(-A.Width/2,A.Height,0),new Vector3(A.Width/2,A.Height,0)})
            {var s=Screen(shot,Vector3.Transform(c,root),out bool front);Check(front&&s.X>panelLeft&&s.X<panelRight&&s.Y>card&&s.Y<.7f,"the board shot frames the corner "+c+" between the panels ("+s+")");}
            {var s=Screen(shot,gate+new Vector3(0,5.07f,-.73f),out bool front);Check(front&&s.Y<.9f&&!Blocked(shot.Eye,gate+new Vector3(0,5.07f,-.73f),cabinet),"the board shot shows the DEPLOY sign under the board");}
            Check(!Blocked(shot.Eye,Vector3.Transform(new Vector3(0,(A.LargeTop+A.SmallTop)/2,NativeAtlasModels.FlapZ),root),room),"nothing in the room between the board shot and the board");
            Check(shot.Eye.Y>2.4f&&Vector3.Distance(new Vector3(shot.Eye.X,0,shot.Eye.Z),NativeDeployPresentation.Pad(0))<1.5f,"the board shot looks up from over the host's pad, above the party's heads");
        }
        private static void Flaps()
        {
            var L=A.Board;var c=L.Cells[L.LineStart[0]+3];var pos=new Vector3[NativeAtlasModels.CellVertices];var uv=new Vector2[NativeAtlasModels.CellVertices];var shade=new float[NativeAtlasModels.CellVertices];
            float mid=(c.Y0+c.Y1)/2,h=c.Y1-mid;
            NativeAtlasModels.Cell(c,new A.Pose{Prev=5,Next=5,Phase=-1},pos,uv,shade,0);
            var flapArea=Vector3.Cross(pos[9]-pos[8],pos[11]-pos[8]).Length();Check(flapArea<1e-7f,"at rest the flap folds flat on its hinge");
            NativeAtlasArt.Rect(5,false,out float u0,out float v0,out float u1,out float v1);float vm=(v0+v1)/2;
            Check(Math.Abs(uv[0].Y-vm)<1e-6f&&Math.Abs(uv[2].Y-v1)<1e-6f&&Math.Abs(uv[4].Y-v0)<1e-6f&&Math.Abs(uv[6].Y-vm)<1e-6f,"the halves show the glyph's top and bottom");
            float lastAngle=-1;
            for(float ph=0;ph<1;ph+=.05f)
            {
                NativeAtlasModels.Cell(c,new A.Pose{Prev=3,Next=4,Phase=ph},pos,uv,shade,0);float th=A.FlapAngle(ph);Check(th>=lastAngle&&th>=0&&th<=Math.PI+1e-5,"the flap falls one way");lastAngle=th;
                Check(Math.Abs(pos[8].Y-mid)<.004f&&Math.Abs(pos[9].Y-mid)<.004f,"the flap turns on the middle hinge");
                float len=Vector3.Distance((pos[8]+pos[9])/2,(pos[10]+pos[11])/2);Check(Math.Abs(len-h)<.004f,"the flap is half a cell long");
                Check(pos[10].Z<=NativeAtlasModels.FlapZ+1e-5f,"the flap falls toward the room");
                NativeAtlasArt.Rect(3,false,out _,out float pv0,out _,out float pv1);NativeAtlasArt.Rect(4,false,out _,out float nv0,out _,out float nv1);
                bool face=Math.Cos(th)>=0;Check(face?Math.Abs(uv[10].Y-pv1)<1e-6f:Math.Abs(uv[10].Y-nv0)<1e-6f,"the flap shows the old top, then the new bottom");
                Check(Math.Abs(uv[2].Y-nv1)<1e-6f&&Math.Abs(uv[4].Y-pv0)<1e-6f,"the new top behind it, the old bottom under it");
                Check(shade.All(k=>k>0&&k<=1),"the flap's shade");
            }
            Check(A.FlapAngle(0)==0&&Math.Abs(A.FlapAngle(1)-Math.PI)<1e-5,"the flap falls from up to down");
        }
        private static DeployInput Input(float t,ExpeditionDeploymentState s=null,bool reduced=false)=>new DeployInput{State=s,Time=t,SinceInstanceRoll=float.NaN,SinceMutatorRoll=float.NaN,SinceDeploy=float.NaN,Reduced=reduced,Seats=1,Ready=new[]{true,false,false,false,false,false,false,false}};
        private static void Arrival()
        {
            var L=A.Board;var st=new NativeAtlasStage();var targets=L.Large[0];int n0=L.LineCount[0];var history=new List<int>[n0];for(int i=0;i<n0;i++)history[i]=new List<int>();
            float lastMove=-1;int versions=0,lastVersion=int.MinValue;bool landedEarly=false;
            for(int f=0;f<=240;f++)
            {
                float t=f/120f;st.Frame(Input(1000+t),NativeDeployPresentation.Shot.Overview);
                if(f==0){Check(Math.Abs(st.Since)<1e-6f,"the station's first frame opens the board");for(int i=0;i<n0;i++){var c=L.Cells[i];Check(st.Poses[i].Next==(c.Kind==A.Kind.Emblem?A.BlankEmblem:0),"line 1 starts blank");}
                    for(int line=1;line<T.Lines;line++){int s=L.LineStart[line];for(int i=0;i<L.LineCount[line];i++)Check(st.Poses[s+i].Next==L.Small[T.All.Length][i]&&st.Poses[s+i].Phase<0,"line "+(line+1)+" already reads NOT IN SERVICE");}}
                for(int i=0;i<n0;i++){var p=st.Poses[i];if(history[i].Count==0||history[i][history[i].Count-1]!=p.Next)history[i].Add(p.Next);if(p.Phase>=0)lastMove=t;
                    if(t<A.EmblemAt-1e-4f)Check(p.Phase<0,"nothing moves before the emblem");if(L.Cells[i].Kind==A.Kind.Glyph&&t<A.ArriveAt-1e-4f)Check(p.Phase<0&&p.Next==0,"no letter moves before the line arrives");}
                if(st.Version!=lastVersion){versions++;lastVersion=st.Version;}
                if(t<A.LandAt-1e-4f&&st.Landed)landedEarly=true;
                if(t>=A.LandAt){Check(st.Landed,"landed at the bell ("+t+")");for(int i=0;i<n0;i++)Check(st.Poses[i].Phase<0&&st.Poses[i].Next==targets[i],"cell "+i+" rests on its letter by the bell");}
            }
            Check(!landedEarly,"not landed before the bell");
            Check(lastMove>A.ArriveAt&&lastMove<=A.ArriveAt+1.2f&&lastMove<A.LandAt,"the line clatters into place within 1.2 s, before the bell ("+lastMove+")");
            Check(versions>20,"the flaps' version changes while they fall ("+versions+")");
            int moving=0;
            for(int i=0;i<n0;i++)
            {
                var c=L.Cells[i];var h=history[i];
                if(targets[i]==0&&c.Kind==A.Kind.Glyph){Check(h.Count==1&&h[0]==0,"a space never flaps");continue;}
                Check(h[h.Count-1]==targets[i],"cell "+i+" ends on its target");moving++;
                if(c.Kind==A.Kind.Emblem){Check(h.Count==2,"the emblem falls once");continue;}
                Check(h.Count-1>=A.MinFlips-1&&h.Count-1<=A.MaxFlips,"cell "+i+" runs "+A.MinFlips+"-"+A.MaxFlips+" flaps ("+(h.Count-1)+"; a first flap onto blank shows no change)");
                for(int k=2;k<h.Count;k++)Check(h[k]==(h[k-1]+1)%A.Glyphs,"cell "+i+" runs forward through the drum");
            }
            Check(moving==targets.Count(x=>x!=0),"every letter of line 1 flaps in");
            // the same clock draws the same board; a clock that runs backwards opens it again; a jump lands it
            var a=new NativeAtlasStage();var b=new NativeAtlasStage();a.Frame(Input(0),NativeDeployPresentation.Shot.Overview);b.Frame(Input(0),NativeDeployPresentation.Shot.Overview);
            for(float t=0;t<2;t+=.037f){a.Frame(Input(t),NativeDeployPresentation.Shot.Overview);b.Frame(Input(t),NativeDeployPresentation.Shot.Overview);Check(a.Poses.SequenceEqual(b.Poses),"deterministic at "+t);}
            a.Frame(Input(.5f),NativeDeployPresentation.Shot.Overview);Check(Math.Abs(a.Since)<1e-6f&&a.Poses.Take(n0).All(p=>p.Phase<0),"a clock that runs backwards reopens the board");
            var j=new NativeAtlasStage();j.Frame(Input(5),NativeDeployPresentation.Shot.Overview);j.Frame(Input(25),NativeDeployPresentation.Shot.Overview);Check(j.Landed&&j.Poses.Take(n0).Select(p=>p.Next).SequenceEqual(targets),"a frame long after the opening shows it landed");
        }
        private static void Framing()
        {
            var st=new NativeAtlasStage();
            foreach(var (t,shot,want) in new[]{(0f,NativeDeployPresentation.Shot.Overview,true),(1.3f,NativeDeployPresentation.Shot.Overview,true),(A.HoldUntil-.01f,NativeDeployPresentation.Shot.Overview,true),(A.HoldUntil,NativeDeployPresentation.Shot.Overview,false),(9f,NativeDeployPresentation.Shot.Overview,false)})
            {st.Frame(Input(t),shot);Check(st.Framed==want,"framed at "+t+": "+want);}
            var s2=new NativeAtlasStage();foreach(var shot in new[]{NativeDeployPresentation.Shot.InstanceMachine,NativeDeployPresentation.Shot.MutatorMachine,NativeDeployPresentation.Shot.Deploy,NativeDeployPresentation.Shot.Map}){s2.Frame(Input(.5f),shot);Check(!s2.Framed,"never over the "+shot+" shot");}
            // through the station: it opens on the board (a cut), eases to the Overview after; a roll during the arrival frames the machine
            var auth=new NativeExpeditionDeployment("preview","host",44);var pool=new[]{new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"}};
            var stage=new NativeDeployStage();var input=Input(0,auth.Snapshot());input.Instances=pool;input.Mutators=NativeDeployPresentation.MutatorIds;input.Host=true;input.PartyReady=true;
            stage.Frame(input,-1);Check(stage.Shot==NativeDeployPresentation.Shot.Overview&&Vector3.Distance(stage.Camera.Eye,A.Shot.Eye)<1e-4f&&stage.Atlas.Framed,"the station opens on the board (the rules' shot is still the Overview)");
            for(float t=1/60f;t<A.HoldUntil+4;t+=1/60f){input.Time=t;stage.Frame(input,1/60f);}
            var over=NativeDeployPresentation.ShotOf(NativeDeployPresentation.Shot.Overview);Check(Vector3.Distance(stage.Camera.Eye,over.Eye)<.05f&&!stage.Atlas.Framed,"then eases to the Overview");
            Check(stage.Lights.Points==8&&stage.Lights.Directionals==1,"the station keeps its eight practicals: the flaps light nothing");
            var st2=new NativeDeployStage();var in2=input;in2.Time=0;st2.Frame(in2,-1);auth.RollInstances("host",pool);in2.State=auth.Snapshot();
            for(float t=.5f;t<1.2f;t+=1/60f){in2.Time=t;in2.SinceInstanceRoll=t-.5f;st2.Frame(in2,1/60f);}
            Check(st2.Shot==NativeDeployPresentation.Shot.InstanceMachine&&!st2.Atlas.Framed&&Vector3.Distance(st2.Camera.Eye,NativeDeployPresentation.ShotOf(NativeDeployPresentation.Shot.InstanceMachine).Eye)<Vector3.Distance(A.Shot.Eye,NativeDeployPresentation.ShotOf(NativeDeployPresentation.Shot.InstanceMachine).Eye),"a roll during the arrival frames the instance machine");
        }
        private static readonly NativeDeploySparks.Spark[] buf=new NativeDeploySparks.Spark[A.SparkLimit];
        private static NativeDeploySparks.Spark[] S(float t,bool reduced=false){int n=A.Sparks(buf,0,t,T.All[0].Accent,reduced);return buf.Take(n).ToArray();}
        private static void Sparks()
        {
            foreach(float t in new[]{float.NaN,-1f,0f,A.ArriveAt,A.LandAt-.01f,A.SparkEnd,A.SparkEnd+3})Check(S(t).Length==0,"no sparks at "+t);
            var burst=S(A.LandAt+.12f);Check(burst.Length>20&&burst.Length<=A.SparkLimit,"a burst as the bell rings ("+burst.Length+")");
            Check(burst.Count(s=>s.Kind==NativeDeploySparks.Kind.Burst&&s.At.X<-A.Width/2+.6f)>=8&&burst.Count(s=>s.Kind==NativeDeploySparks.Kind.Burst&&s.At.X>A.Width/2-.6f)>=8,"out of both cheeks");
            Check(burst.All(s=>s.At.Y<A.BeamBottom&&s.At.Y>A.At.Y-1.5f&&Math.Abs(s.At.Z-A.At.Z)<1.2f),"near the board, under the ceiling");
            var flash=burst.Where(s=>s.Kind==NativeDeploySparks.Kind.Flash).ToArray();var accent=WeaponPaint.Linear(T.All[0].Accent);
            Check(flash.Length==1&&Math.Abs(flash[0].At.X-(A.At.X+A.LampX))<.01f&&Math.Abs(flash[0].Color.Y/flash[0].Color.X-accent.Y/accent.X)<.02f,"a flash at the in-service lamp in the accent");
            int full=0,reduced=0;for(float t=A.LandAt;t<A.SparkEnd;t+=1/60f){full+=S(t).Length;reduced+=S(t,true).Length;}Check(reduced>0&&reduced<full*.45f,"reduced effects: about a third ("+reduced+" of "+full+")");
            var a=S(A.LandAt+.3f);var b=S(A.LandAt+.3f);Check(a.Length==b.Length&&a.Zip(b,(x,y)=>x.At==y.At&&x.Color==y.Color).All(x=>x),"the same clock draws the same sparks");
            var small=new NativeDeploySparks.Spark[5];Check(A.Sparks(small,0,A.LandAt+.1f,T.All[0].Accent,false)==5&&A.Sparks(buf,7,A.LandAt+.1f,T.All[0].Accent,false)>7,"never past the buffer, added after the count");
        }
        private static void Sounds()
        {
            Check(A.Schedule.Length==2&&A.Schedule[0].Kind==A.CueKind.Clatter&&A.Schedule[0].At==A.EmblemAt&&A.Schedule[1].Kind==A.CueKind.Bell&&A.Schedule[1].At==A.LandAt,"the clatter with the first flap, the bell as the line lands");
            var into=new A.Cue[4];var seen=new List<A.CueKind>();float before=float.NaN;
            for(int f=0;f<=240;f++){float now=f/60f;int n=A.Heard(before,now,into,0);before=now;for(int i=0;i<n;i++)seen.Add(into[i].Kind);}
            Check(seen.SequenceEqual(new[]{A.CueKind.Clatter,A.CueKind.Bell}),"each heard once, in order");
            Check(A.Heard(float.NaN,5,into,0)==0,"a late first frame hears nothing of an arrival long done");
            Check(A.Heard(3,0.1f,into,0)==1&&into[0].Kind==A.CueKind.Clatter,"a reopened board starts again");
            var st=new NativeAtlasStage();var kinds=new List<A.CueKind>();for(int f=0;f<=200;f++){st.Frame(Input(7+f/60f),NativeDeployPresentation.Shot.Overview);for(int i=0;i<st.CueCount;i++)kinds.Add(st.Cues[i].Kind);}
            Check(kinds.SequenceEqual(new[]{A.CueKind.Clatter,A.CueKind.Bell}),"the stage hears the arrival once");
            for(int i=0;i<NativeAtlasSounds.Count;i++)
            {
                var pcm=NativeAtlasSounds.Samples(i);Check(pcm.Length==(int)Math.Ceiling(NativeAtlasSounds.Duration(i)*NativeAtlasSounds.SampleRate),"clip "+i+" lasts its declared length");
                Check(pcm.All(x=>float.IsFinite(x)&&Math.Abs(x)<1)&&pcm[0]==0&&pcm[pcm.Length-1]==0,"clip "+i+": finite, unclipped, click-free ends");
                Check(pcm.Max(Math.Abs)>.1f&&pcm.Select(x=>(double)x*x).Average()>1e-4,"clip "+i+" is audible");Check(pcm.SequenceEqual(NativeAtlasSounds.Samples(i)),"clip "+i+" is deterministic");
            }
            Check(NativeAtlasSounds.Of(A.Schedule[0]).Index==NativeAtlasSounds.Clatter&&NativeAtlasSounds.Of(A.Schedule[1]).Index==NativeAtlasSounds.Bell&&NativeAtlasSounds.Keys.Distinct().Count()==NativeAtlasSounds.Count&&NativeAtlasSounds.Keys.All(k=>k.StartsWith("deploy.atlas.")),"each moment its own key");
            Check(NativeAtlasSounds.Of(A.Schedule[0]).Gain>0&&NativeAtlasSounds.Of(A.Schedule[1]).Gain<=1,"bounded gains");
            // the clatter lands with the picture: one click per line-1 flap, each at its flap's landing
            var L=A.Board;var clicks=A.Clicks(L,0);int flaps=0;for(int i=0;i<L.LineCount[0];i++){var c=L.Cells[i];if(L.Large[0][i]==0&&c.Kind==A.Kind.Glyph)continue;A.Plan(c,0,0,out _,out int k);flaps+=k;}
            Check(clicks.Count==flaps&&clicks.All(x=>x>=0)&&clicks.Zip(clicks.Skip(1),(x,y)=>y>=x).All(x=>x),"a click for every flap, in time ("+clicks.Count+")");
            Check(Math.Abs(clicks[clicks.Count-1]+A.EmblemAt-(A.LandAt-.25f))<.3f,"the last click near the line's landing");
            var clatter=NativeAtlasSounds.Samples(NativeAtlasSounds.Clatter);int last=(int)((clicks[clicks.Count-1]+.05f)*NativeAtlasSounds.SampleRate);double inside=0,after=0;for(int i=0;i<clatter.Length;i++){double e=clatter[i]*clatter[i];if(i<last)inside+=e;else after+=e;}
            Check(after<inside*.02,"the clatter's sound ends with its last flap");
            // machine 2 is never Astra's: NativeDeploySounds plays machines 0 and 1 only
            Check(NativeDeploySounds.Of(new NativeDeploySparks.Cue{Kind=NativeDeploySparks.CueKind.Stop,Machine=A.Machine,Reel=0}).Key==null,"Astra's station sounds ignore machine 2");
        }
        private static void Art()
        {
            var atlas=NativeAtlasArt.Atlas();Check(atlas.Length==NativeAtlasArt.Bytes&&atlas.All(float.IsFinite)&&atlas.All(v=>v>=0&&v<=1),"the atlas's size and range ("+NativeAtlasArt.Width+"x"+NativeAtlasArt.Height+")");
            Check(atlas.SequenceEqual(NativeAtlasArt.Atlas()),"the atlas is deterministic");Check(NativeAtlasArt.Rows*NativeAtlasArt.Across>=A.Tiles,"every tile fits");
            int W=NativeAtlasArt.Width,H=NativeAtlasArt.Height;
            float Ink(int tile,bool emblem){NativeAtlasArt.Rect(tile,emblem,out float u0,out float v0,out float u1,out float v1);int bright=0;for(int y=(int)(v0*H)+2;y<(int)(v1*H)-2;y++)for(int x=(int)(u0*W)+2;x<(int)(u1*W)-2;x++){int i=(y*W+x)*4;if(atlas[i]+atlas[i+1]+atlas[i+2]>2.4f)bright++;}return bright;}
            for(int g=0;g<A.Glyphs;g++){float ink=Ink(g,false);if(A.Drum[g]==' ')Check(ink==0,"the blank flap is blank");else Check(ink>12,"glyph '"+A.Drum[g]+"' has ink on its flap ("+ink+")");}
            Check(Ink(A.BlankEmblem,true)==0&&Ink(A.EmblemTile(0),true)>200,"the blank emblem flap and City's emblem");
            // the split runs dark across every flap's middle; outside a glyph flap the cabinet's black
            for(int g=0;g<A.Tiles;g++)
            {
                bool emblem=g>=A.Glyphs;NativeAtlasArt.Rect(g,emblem,out float u0,out float v0,out float u1,out float v1);int ym=(int)((v0+v1)/2*H);
                for(int x=(int)(u0*W)+4;x<(int)(u1*W)-4;x+=3){int i=(ym*W+x)*4;if(atlas[i]+atlas[i+1]+atlas[i+2]>.3f){Check(false,"tile "+g+"'s split is dark");break;}}
                Check(u0>=0&&u1<1&&v0>0&&v1<1&&u1>u0&&v1>v0,"tile "+g+"'s rect is inside the atlas, off its wrap");
                if(!emblem){int i=(((int)(v0*H)+10)*W+(int)(u0*W)-3)*4;Check(atlas[i]+atlas[i+1]+atlas[i+2]<.15f,"tile "+g+" is black beside its flap");Check(Math.Abs((u1-u0)*W/((v1-v0)*H)-A.FlapAspect)<.03f,"tile "+g+"'s flap has the cells' shape");}
            }
            var bytes=NativeAtlasArt.Pack(atlas);var back=NativeAtlasArt.Unpack(bytes);Check(bytes.Length==NativeAtlasArt.Bytes&&back.Zip(atlas,(x,y)=>Math.Abs(x-y)<=.5f/255+1e-6f).All(x=>x),"the bake round-trips within a byte");
            bool bad=false;try{NativeAtlasArt.Unpack(new byte[8]);}catch(FormatException){bad=true;}Check(bad,"a wrong-sized bake is refused");
            Check(Regex.IsMatch(NativeAtlasArt.Key,"^DeployCards/atlas-flaps-[0-9a-f]{8}$")&&NativeAtlasArt.Key==NativeAtlasArt.Key,"the bake's key is stable and names its inputs' hash");
            foreach(char ch in A.Drum)if(ch!=' ')Check(NativeAtlasArt.GlyphMarkup(ch).Contains("<line"),"glyph '"+ch+"' draws strokes");
        }
        private static void Allocation()
        {
            var st=new NativeAtlasStage();var ds=new NativeDeployStage();var auth=new NativeExpeditionDeployment("preview","host",45);var input=Input(0,auth.Snapshot());
            for(int warm=0;warm<2;warm++)
            {
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int f=0;f<240;f++){input.Time=f/60f;st.Frame(input,NativeDeployPresentation.Shot.Overview);ds.Frame(input,1/60f);}
                long used=GC.GetAllocatedBytesForCurrentThread()-before;if(warm==1)Check(used==0,"the board and the station with it allocate nothing per frame ("+used+" bytes)");
            }
        }
        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string scene=R("Runtime/NativeDeployScene.cs"),stage=R("Core/NativeDeployStage.cs"),atlas=R("Runtime/NativeAtlasScene.cs"),audio=R("Runtime/NativeAudio.cs"),play=R("Runtime/NativeAtlasAudio.cs"),console=R("Runtime/NativeDeployConsoleUI.cs"),card=R("Runtime/NativeAtlasConsoleUI.cs"),bake=R("Editor/NativeDeployCardBuild.cs");
            // the hooks: one line each in the station's scene, stage, console, audio and card bake
            Check(scene.Contains("atlas=NativeAtlasScene.Make();")&&scene.Contains("atlas?.Dispose();")&&Regex.Matches(scene,@"atlas\?\.Draw\(camera,Stage\);").Count==1,"the station's scene makes, draws and frees the board");
            int d0=scene.IndexOf("public void Draw(Camera camera",StringComparison.Ordinal),d1=scene.IndexOf("public void Dispose()",StringComparison.Ordinal);Check(scene.Substring(d0,d1-d0).Contains("atlas?.Draw(camera,Stage);"),"inside its draw");
            Check(stage.Contains("public readonly NativeAtlasStage Atlas=new NativeAtlasStage();")&&Regex.Matches(stage,@"Atlas\.Frame\(").Count==1&&stage.Contains("if(Atlas.Frame(f,Shot))want=NativeAtlasBoard.Shot;")&&!Regex.IsMatch(stage,@"Shot=NativeAtlasBoard"),"the stage poses the board once a frame and only eases its camera to it (its Shot stays the rules')");
            Check(console.Contains("Header(W,s,ui);NativeAtlasConsole.Card(stage,W,ui);Destinations(stage,s,ui);"),"the console's TILESET card comes first");
            Check(audio.Contains("NativeAtlasScene.Sound+=AtlasHeard;")&&audio.Contains("NativeAtlasScene.Sound-=AtlasHeard;ClearAtlasClips();"),"the audio hears the board");
            Check(play.Contains("limiter.Cue(sound.Key,Time.unscaledTime,sound.Gain)")&&play.Contains("Muted||Effects<=0")&&play.Contains("Voice()")&&play.Contains("gain*Effects")&&play.Contains("NativeAtlasSounds.Samples("),"its sounds on the shared voices, volume, mute and limiter");
            Check(bake.Contains("NativeAtlasArt.Key")&&bake.Contains("NativeAtlasArt.Pack(NativeAtlasArt.Atlas())")&&bake.Contains("asset.bytes.Length!=NativeAtlasArt.Bytes"),"the build bakes the glyph atlas once and checks it");
            Check(atlas.Contains("Resources.Load<TextAsset>(NativeAtlasArt.Key)")&&atlas.Contains("baked.bytes.Length==NativeAtlasArt.Bytes")&&atlas.Contains("Task.Run(()=>NativeAtlasArt.Atlas())"),"the scene loads the bake, else rasterises it on a worker");
            Check(atlas.Contains("Sound?.Invoke(a.Cues[i])")&&atlas.Contains("catch(Exception e){failed=true;Debug.LogException(e);}")&&atlas.Contains("public static NativeAtlasScene Make(){try{"),"its sounds go out; a failure turns the board off, never the station");
            Check(card.Contains("board.Since>=NativeAtlasBoard.LandAt")&&card.Contains("\"ARRIVING…\"")&&!card.Contains("DeployIntent"),"the card mirrors the board and raises no intent");
            Check(card.Contains("public const float Width=262,Height="+NativeAtlasConsoleHeight+",Top="+NativeAtlasConsoleTop+";"),"the card's place, as the board's shot is checked against it");
            // the draw allocates nothing: no reference allocation, lambda or LINQ in Frame or Rebuild
            foreach(var name in new[]{"private void Frame(Camera camera","private void Rebuild("})
            {
                int s=atlas.IndexOf(name,StringComparison.Ordinal);int e=atlas.IndexOf("\n        }",s,StringComparison.Ordinal);var body=atlas.Substring(s,e-s);
                Check(s>=0&&!Regex.IsMatch(body,@"new (?!Vector[234]\(|Matrix4x4\()[A-Z]")&&!body.Contains("=>")&&!Regex.IsMatch(body,@"\.(Select|Where|ToArray|ToList)\("),"the board's "+name.Split('(')[0].Split(' ').Last()+" allocates nothing");
            }
            // nothing of the board asks the authority, sends anything or touches the ticket
            var calls=new Regex(@"\.(RollInstances|RollMutators|SkipMutators|Deploy)\(|new NativeExpeditionDeployment\(|SendServer|NativeNetMessage|Ticket");
            foreach(var f in new[]{"Core/NativeTilesets.cs","Core/NativeAtlasBoard.cs","Core/NativeAtlasModels.cs","Core/NativeAtlasArt.cs","Core/NativeAtlasSounds.cs","Runtime/NativeAtlasScene.cs","Runtime/NativeAtlasAudio.cs","Runtime/NativeAtlasConsoleUI.cs"})
                Check(!calls.IsMatch(R(f)),f+" never asks the authority or the wire");
            Check(!R("Core/NativeDeploySounds.cs").Contains("Atlas")&&!R("Core/NativeDeployPresentation.cs").Contains("Atlas"),"Astra's station sounds and the eject's rules are untouched");
            // each new file is registered with Unity, with its own guid
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeTilesets.cs","Core/NativeAtlasBoard.cs","Core/NativeAtlasModels.cs","Core/NativeAtlasArt.cs","Core/NativeAtlasSounds.cs","Runtime/NativeAtlasScene.cs","Runtime/NativeAtlasAudio.cs","Runtime/NativeAtlasConsoleUI.cs"})
            {var meta=Path.Combine(root,"Assets/ChipArena",f+".meta");Check(File.Exists(meta),f+" has a meta");var text=File.ReadAllText(meta);var g=Regex.Match(text,@"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g)&&!text.Contains(" \n"),f+" has its own guid and a clean meta");}
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories))
            {
                if(Path.GetFileName(f).StartsWith("NativeAtlas",StringComparison.Ordinal)||Path.GetFileName(f)=="NativeTilesets.cs.meta")continue;
                var g=Regex.Match(File.ReadAllText(f),@"guid: ([0-9a-f]{32})").Groups[1].Value;if(guids.Contains(g)){Check(false,"no other asset shares a new guid ("+Path.GetFileName(f)+")");break;}
            }
        }
    }
}
#endif
