#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Tests/status-parity.json is written by tools/export_status_fixtures.mjs from the pinned StatusRig and teamArmor.
    internal static class StatusVisualChecks
    {
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/status-parity.json")));var json=document.RootElement;int sample=0;
            void Check(bool ok,string name){if(!ok)throw new Exception("Status visuals "+sample+": "+name);}
            void Near(float a,double b,string name,float tolerance=.00003f){if(!MathUtil.Finite(a)||Math.Abs(a-b)>tolerance)throw new Exception("Status visuals "+sample+" "+name+": "+a+" != "+b);}
            Vector3 Vec(JsonElement e)=>new Vector3(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
            var vfx=json.GetProperty("vfx");
            Check(vfx.GetProperty("flames").GetInt32()==NativeStatusVisuals.Flames&&vfx.GetProperty("embers").GetInt32()==NativeStatusVisuals.Embers&&vfx.GetProperty("bubbles").GetInt32()==NativeStatusVisuals.Bubbles&&vfx.GetProperty("reducedFlames").GetInt32()==NativeStatusVisuals.ReducedFlames&&vfx.GetProperty("reducedEmbers").GetInt32()==NativeStatusVisuals.ReducedEmbers&&vfx.GetProperty("reducedBubbles").GetInt32()==NativeStatusVisuals.ReducedBubbles,"STATUS_VFX counts");
            Near(NativeStatusVisuals.FireRise,vfx.GetProperty("fireRise").GetDouble(),"fire rise");Near(NativeStatusVisuals.BubbleRise,vfx.GetProperty("bubbleRise").GetDouble(),"bubble rise");Near(NativeStatusVisuals.BubblePeriod,vfx.GetProperty("bubblePeriod").GetDouble(),"bubble period");
            // Materials: colour, opacity, sidedness; translucent surfaces never write depth, opaque ones do.
            string[] names={"flame","core","ember","bubble","rim","glint","slow","shackle","mark"};
            for(int i=0;i<names.Length;i++)
            {
                var m=json.GetProperty("materials").GetProperty(names[i]);var s=NativeStatusVisuals.Surfaces[i];
                Check(m.GetProperty("color").GetInt32()==s.Color&&m.GetProperty("doubleSide").GetBoolean()==s.DoubleSide&&m.GetProperty("transparent").GetBoolean()==s.Transparent&&m.GetProperty("depthWrite").GetBoolean()==!s.Transparent&&m.GetProperty("fog").GetBoolean(),"surface "+names[i]);
                Near(s.Opacity,m.GetProperty("opacity").GetDouble(),"opacity "+names[i]);
            }
            // Geometry: the flame constants and the generated sphere/ring/octahedron reproduce three r180 exactly.
            var geometry=json.GetProperty("geometry");
            void Mesh(string name,Vector3[] points,int[] triangles,bool expanded=false)
            {
                var g=geometry.GetProperty(name);var p=g.GetProperty("positions");var index=g.GetProperty("index");
                if(expanded){Check(index.ValueKind==JsonValueKind.Null&&p.GetArrayLength()==triangles.Length*3,name+" size");for(int i=0;i<triangles.Length;i++){var v=points[triangles[i]];Near(v.X,p[i*3].GetDouble(),name+" x");Near(v.Y,p[i*3+1].GetDouble(),name+" y");Near(v.Z,p[i*3+2].GetDouble(),name+" z");}return;}
                Check(p.GetArrayLength()==points.Length*3&&index.GetArrayLength()==triangles.Length,name+" size");
                for(int i=0;i<points.Length;i++){Near(points[i].X,p[i*3].GetDouble(),name+" x");Near(points[i].Y,p[i*3+1].GetDouble(),name+" y");Near(points[i].Z,p[i*3+2].GetDouble(),name+" z");}
                for(int i=0;i<triangles.Length;i++)Check(index[i].GetInt32()==triangles[i],name+" index "+i);
            }
            var flame=new Vector3[NativeStatusVisuals.FlameOutline.Length/2];for(int i=0;i<flame.Length;i++)flame[i]=new Vector3(NativeStatusVisuals.FlameOutline[i*2],NativeStatusVisuals.FlameOutline[i*2+1],0);
            Mesh("flame",flame,NativeStatusVisuals.FlameTriangles);
            NativeStatusVisuals.Sphere(8,6,out var bubble,out var bubbleTriangles);Mesh("bubble",bubble,bubbleTriangles);
            NativeStatusVisuals.Ring(.88f,1,16,out var ring,out var ringTriangles);Mesh("rim",ring,ringTriangles);
            NativeStatusVisuals.Sphere(5,3,out var glint,out var glintTriangles);Mesh("glint",glint,glintTriangles);
            Mesh("ember",NativeStatusVisuals.OctahedronPoints,NativeStatusVisuals.OctahedronTriangles,true);
            // Rig samples: every shown source mesh, in traversal order, against the composed native pieces.
            var camera=json.GetProperty("camera");var cameraPosition=Vec(camera.GetProperty("position"));var q=camera.GetProperty("quaternion");var cameraRotation=new Quaternion(q[0].GetSingle(),q[1].GetSingle(),q[2].GetSingle(),q[3].GetSingle());
            var pieces=new NativeStatusVisuals.Piece[NativeStatusVisuals.MaxPieces];var expected=new List<(string kind,int color,Matrix4x4 matrix)>();var actual=new List<(string kind,int color,Matrix4x4 matrix)>();
            Matrix4x4 Trs(Vector3 p,Quaternion r,Vector3 s)=>Matrix4x4.CreateScale(s)*Matrix4x4.CreateFromQuaternion(r)*Matrix4x4.CreateTranslation(p);
            int shown=0;bool sawShackle=false,sawLockedHidden=false,sawPop=false;
            foreach(var s in json.GetProperty("samples").EnumerateArray())
            {
                float time=s.GetProperty("time").GetSingle();var slowed=s.GetProperty("slowed");var mark=s.GetProperty("mark");var rootAt=Vec(s.GetProperty("root"));
                var rig=new NativeStatusVisuals.Rig{Active=s.GetProperty("active").GetBoolean(),FirstPerson=s.GetProperty("mode").GetString()=="first",Fire=s.GetProperty("fire").GetBoolean(),Poison=s.GetProperty("poison").GetBoolean(),Quality=s.GetProperty("quality").GetSingle(),Root=rootAt,Camera=cameraPosition,CameraRotation=cameraRotation};
                if(slowed.ValueKind!=JsonValueKind.Null){float factor=slowed.GetProperty("factor").GetSingle();rig.Slowed=true;rig.SlowUntil=slowed.GetProperty("until").GetSingle();rig.Shackled=NativeStatusVisuals.Shackled(factor);Check(rig.Shackled==(slowed.GetProperty("kind").GetString()=="shackles"),"shackle kind from factor");sawShackle|=rig.Shackled;}
                if(mark.ValueKind!=JsonValueKind.Null){rig.Marked=true;rig.MarkBorn=mark.GetProperty("born").GetSingle();rig.MarkUntil=mark.GetProperty("until").GetSingle();rig.MarkLock=mark.TryGetProperty("lockAt",out var lockAt)&&lockAt.ValueKind==JsonValueKind.Number?lockAt.GetSingle():rig.MarkUntil;}
                expected.Clear();actual.Clear();
                foreach(var m in s.GetProperty("meshes").EnumerateArray())
                {
                    if(!m.GetProperty("visible").GetBoolean())continue;var e=m.GetProperty("matrix");var x=new float[16];for(int i=0;i<16;i++)x[i]=e[i].GetSingle();
                    // three stores column-major elements; System.Numerics (row vectors) reads them in row order.
                    expected.Add((m.GetProperty("kind").GetString(),m.GetProperty("color").GetInt32(),new Matrix4x4(x[0],x[1],x[2],x[3],x[4],x[5],x[6],x[7],x[8],x[9],x[10],x[11],x[12],x[13],x[14],x[15])));
                }
                int count=NativeStatusVisuals.Build(rig,time,pieces);var origin=Matrix4x4.CreateTranslation(rootAt);
                for(int i=0;i<count;i++)
                {
                    var p=pieces[i];var local=Trs(p.Position,p.Rotation,p.Scale)*origin;int Color(NativeStatusVisuals.Surface surface)=>NativeStatusVisuals.Surfaces[(int)surface].Color;
                    switch(p.Kind)
                    {
                        case NativeStatusVisuals.Kind.Flame:actual.Add(("flame",Color(NativeStatusVisuals.Surface.Fire),local));actual.Add(("core",Color(NativeStatusVisuals.Surface.Core),Trs(NativeStatusVisuals.CorePosition,Quaternion.Identity,NativeStatusVisuals.CoreScale)*local));break;
                        case NativeStatusVisuals.Kind.Ember:actual.Add(("ember",Color(NativeStatusVisuals.Surface.Ember),local));break;
                        case NativeStatusVisuals.Kind.Bubble:
                            if(!p.Popping)actual.Add(("bubble",Color(NativeStatusVisuals.Surface.Bubble),local));else sawPop=true;
                            actual.Add(("rim",Color(NativeStatusVisuals.Surface.Rim),Matrix4x4.CreateTranslation(NativeStatusVisuals.RimPosition)*local));
                            if(!p.Popping)actual.Add(("glint",Color(NativeStatusVisuals.Surface.Glint),Trs(NativeStatusVisuals.GlintPosition,Quaternion.Identity,NativeStatusVisuals.GlintScale)*local));break;
                        case NativeStatusVisuals.Kind.Slow:actual.Add(("slow",Color(NativeStatusVisuals.Surface.Slow),local));break;
                        case NativeStatusVisuals.Kind.Shackle:actual.Add(("slow",Color(NativeStatusVisuals.Surface.Shackle),local));break;
                        default:actual.Add(("mark",Color(NativeStatusVisuals.Surface.Mark),local));break;
                    }
                }
                if(rig.Marked&&rig.Active&&!rig.FirstPerson&&time>=rig.MarkLock&&time<rig.MarkUntil&&Math.Sin(time*40.0)<=-.4)sawLockedHidden=true;
                Check(actual.Count==expected.Count,"shown mesh count "+actual.Count+" != "+expected.Count);
                for(int i=0;i<expected.Count;i++)
                {
                    Check(actual[i].kind==expected[i].kind&&actual[i].color==expected[i].color,"mesh "+i+" "+actual[i].kind+" != "+expected[i].kind);
                    var a=actual[i].matrix;var b=expected[i].matrix;
                    Near(a.M11,b.M11,"m11 "+i);Near(a.M12,b.M12,"m12 "+i);Near(a.M13,b.M13,"m13 "+i);Near(a.M21,b.M21,"m21 "+i);Near(a.M22,b.M22,"m22 "+i);Near(a.M23,b.M23,"m23 "+i);
                    Near(a.M31,b.M31,"m31 "+i);Near(a.M32,b.M32,"m32 "+i);Near(a.M33,b.M33,"m33 "+i);Near(a.M41,b.M41,"x "+expected[i].kind+" "+i);Near(a.M42,b.M42,"y "+expected[i].kind+" "+i);Near(a.M43,b.M43,"z "+expected[i].kind+" "+i);
                }
                shown+=expected.Count;sample++;
            }
            Check(sawShackle&&sawPop&&sawLockedHidden&&shown>1000,"fixture coverage");
            // Team bands: colours, the four parent joints and their offsets; no team hides them.
            var teams=json.GetProperty("teams");Check(teams.GetArrayLength()==NativeStatusVisuals.TeamColors.Length,"team count");for(int i=0;i<teams.GetArrayLength();i++)Check(teams[i].GetInt32()==NativeStatusVisuals.TeamColors[i],"team colour "+i);
            string[] parents={"chest","spine","leftArm","rightArm"};int[] bones={CapturedRig.Chest,CapturedRig.Spine,CapturedRig.LArm,CapturedRig.RArm};int bands=0;
            foreach(var b in json.GetProperty("bands").EnumerateArray())
            {
                int slot=Array.IndexOf(parents,b.GetProperty("parent").GetString());Check(slot>=0&&NativeStatusVisuals.BandBones[slot]==bones[slot],"band joint");
                bool team=b.GetProperty("team").ValueKind!=JsonValueKind.Null;Check(b.GetProperty("visible").GetBoolean()==team&&!b.GetProperty("toneMapped").GetBoolean(),"band visibility");
                Check(Vector3.Distance(Vec(b.GetProperty("position")),NativeStatusVisuals.BandPositions[slot])<1e-5f&&Vector3.Distance(Vec(b.GetProperty("scale")),NativeStatusVisuals.BandSizes[slot])<1e-5f,"band box "+parents[slot]);
                if(team)Check(b.GetProperty("color").GetInt32()==NativeStatusVisuals.TeamColors[NativeStatusVisuals.BandTeam(true,false,false,Array.IndexOf(new[]{"red","blue","green","yellow","purple","orange","cyan","pink"},b.GetProperty("team").GetString()))],"band colour");
                bands++;
            }
            Check(bands==36,"band samples");
            Check(NativeStatusVisuals.BandTeam(false,false,false,3)==-1&&NativeStatusVisuals.BandTeam(true,true,false,0)==1&&NativeStatusVisuals.BandTeam(true,true,true,1)==4&&NativeStatusVisuals.BandTeam(true,false,false,-1)==7,"band team rules");
            Near(NativeStatusVisuals.AddRootHeight(.85f,false),.85,"add root");Near(NativeStatusVisuals.AddRootHeight(.9f,true),.35,"crawl root");Near(NativeStatusVisuals.AddRootHeight(2.85f,false),2.85,"raised add root");
            Console.WriteLine("PASS Status rigs (burn, poison, slow, shackles, Orbital Mark; world and first person) and team bands match the source over "+sample+" samples");
        }
    }
}
#endif
