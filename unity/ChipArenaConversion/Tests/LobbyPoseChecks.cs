#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // C14: the lobby idle poses and the menu portrait scene against values captured from the original lobby-poses.ts,
    // menu-portraits.ts, auditor-halo.ts and character-rig.ts (Tests/lobby-pose-parity.json, tools/export_lobby_poses.mjs):
    // every joint of every pose for every weapon, the held weapons, the joint covers, the framing, the hat tops (with the
    // exported hat models), the mannequin colours, the chair, the halo and the primitives.
    internal static class LobbyPoseChecks
    {
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/lobby-pose-parity.json")));var R=document.RootElement;int count=0;
            void Check(bool ok,string what){if(!ok)throw new Exception("Lobby pose parity: "+what);count++;}
            void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
            Vector3 V3(JsonElement e,int at=0)=>new Vector3((float)e[at].GetDouble(),(float)e[at+1].GetDouble(),(float)e[at+2].GetDouble());
            Quaternion Q4(JsonElement e,int at=0)=>new Quaternion((float)e[at].GetDouble(),(float)e[at+1].GetDouble(),(float)e[at+2].GetDouble(),(float)e[at+3].GetDouble());

            // shared/lobby-presentation.ts LOBBY_POSES and lobbyPose()'s fallback.
            var keys=R.GetProperty("poses");var labels=R.GetProperty("labels");Check(keys.GetArrayLength()==NativeLobbyPoses.Keys.Length,"pose count");
            for(int i=0;i<NativeLobbyPoses.Keys.Length;i++){Check(keys[i].GetString()==NativeLobbyPoses.Keys[i],"pose key "+i);Check(labels[i].GetString()==NativeLobbyPoses.Labels[i],"pose label "+i);}
            Check(NativeLobbyPoses.Normalize("crossed-arms")==R.GetProperty("fallback").GetString()&&NativeLobbyPoses.Normalize(null)=="weapon"&&NativeLobbyPoses.Normalize("sleep")=="sleep","lobbyPose fallback");
            Check(NativeLobbyPoses.Effective("siren","sleep")==NativeLobbyPoses.Weapon&&NativeLobbyPoses.Effective("pistol","chair")==NativeLobbyPoses.Chair,"the Siren stands");

            // Poses: local joints, world joints with a probe .2 m along each wrist, Euler state, held weapons, joint covers.
            var rig=new MeleeRig();var pose=new CapturedRig.Pose();var expected=new CapturedRig.Pose();int poses=0,idle=0;double worstP=0,worstQ=0;
            foreach(var c in R.GetProperty("cases").EnumerateArray())
            {
                string spec=c.GetProperty("spec").GetString(),key=c.GetProperty("pose").GetString();float t=(float)c.GetProperty("t").GetDouble();int p=NativeLobbyPoses.Index(key);
                rig.Reset();NativeLobbyPoses.Sample(rig,spec,p,t,out var weapon,out var off);string what=spec+"/"+key+"@"+t;
                foreach(var (h,e) in new[]{(weapon,c.GetProperty("weapon")),(off,c.GetProperty("off"))})
                {
                    Check(h.Visible==(e.GetProperty("v").GetInt32()==1),what+" held visibility");if(!h.Visible)continue;
                    Check(h.Bone==e.GetProperty("b").GetInt32(),what+" held bone");Near(Vector3.Distance(h.Position,V3(e.GetProperty("p"))),0,1e-6,what+" held position");Near(Angle(h.Rotation,Q4(e.GetProperty("q"))),0,1e-5,what+" held rotation");
                }
                if(NativeLobbyPoses.UsesIdle(spec,p)){idle++;continue;}
                var bones=c.GetProperty("bones");var euler=c.GetProperty("euler");
                for(int b=0;b<22;b++)
                {
                    var row=bones[b];expected.Position[b]=V3(row);expected.Rotation[b]=Q4(row,3);
                    double dp=Vector3.Distance(rig.P[b],expected.Position[b]),dq=Angle(rig.Q[b],expected.Rotation[b]);worstP=Math.Max(worstP,dp);worstQ=Math.Max(worstQ,dq);
                    Near(dp,0,2e-5,what+" "+CapturedRig.Bones[b].Name+" position");Near(dq,0,2e-4,what+" "+CapturedRig.Bones[b].Name+" rotation");
                    Near(Vector3.Distance(rig.E[b],V3(euler[b])),0,5e-4,what+" "+CapturedRig.Bones[b].Name+" euler");
                }
                rig.WritePose(pose);pose.World();expected.World();
                for(int b=0;b<22;b++)Near(Vector3.Distance(pose.WorldPosition[b],expected.WorldPosition[b]),0,1e-4,what+" world "+CapturedRig.Bones[b].Name);
                foreach(int w in new[]{CapturedRig.LHand,CapturedRig.RHand})
                {Vector3 probe=pose.WorldPosition[w]+Vector3.Transform(new Vector3(0,0,.2f),pose.WorldRotation[w]),want=expected.WorldPosition[w]+Vector3.Transform(new Vector3(0,0,.2f),expected.WorldRotation[w]);Near(Vector3.Distance(probe,want),0,1e-4,what+" wrist probe");}
                string[] covers={"left-elbow-joint","right-elbow-joint","left-knee-joint","right-knee-joint","left-kneecap","right-kneecap"};var cv=c.GetProperty("covers");
                for(int i=0;i<covers.Length;i++){Check(NativeLobbyPoses.Cover(covers[i],out int joint),"cover "+covers[i]);Near(NativeLobbyPoses.CoverAngle(rig,joint),cv[i][0].GetDouble(),5e-4,what+" cover "+covers[i]);}
                poses++;
            }
            Check(poses>=200&&idle>=10,"pose coverage");Check(!NativeLobbyPoses.Cover("left-forearm",out _),"forearm is no cover");

            // framePortrait over poses, card aspects and hat tops.
            int frames=0;
            foreach(var f in R.GetProperty("frames").EnumerateArray())
            {
                int p=NativeLobbyPoses.Index(f.GetProperty("pose").GetString());float aspect=(float)f.GetProperty("aspect").GetDouble(),top=(float)f.GetProperty("top").GetDouble();var fr=NativePortraitRules.Frame(p,aspect,top);var lrtb=f.GetProperty("lrtb");string what="frame "+p+" "+aspect+" "+top;
                Near(-fr.Half*fr.Aspect,lrtb[0].GetDouble(),1e-5,what+" left");Near(fr.Half*fr.Aspect,lrtb[1].GetDouble(),1e-5,what+" right");Near(fr.Half,lrtb[2].GetDouble(),1e-5,what+" top");Near(-fr.Half,lrtb[3].GetDouble(),1e-5,what+" bottom");
                Near(NativePortraitRules.Near,f.GetProperty("near").GetDouble(),1e-6,"near");Near(NativePortraitRules.Far,f.GetProperty("far").GetDouble(),1e-6,"far");
                Near(Vector3.Distance(fr.Position,V3(f.GetProperty("position"))),0,1e-5,what+" position");Near(Vector3.Distance(Vector3.Normalize(fr.Target-fr.Position),V3(f.GetProperty("forward"))),0,1e-5,what+" forward");frames++;
            }

            // hatTop(): the exported hat models' head-space bounds (Assets/Conversion/Resources/SourceModels.json, the meshes the
            // renderer draws) through the posed head; the halo from its own geometry at the portrait time. The source boxes each
            // mesh, the port the merged model, so a steeply rolled head (sleeping) reads higher; the sleeping framing ignores
            // the top, which is checked instead.
            var hats=HatBounds(root);int tops=0;double worstTop=0;
            foreach(var h in R.GetProperty("hatTops").EnumerateArray())
            {
                string hat=h.GetProperty("hat").GetString(),key=h.GetProperty("pose").GetString();float t=(float)h.GetProperty("t").GetDouble();double want=h.GetProperty("top").GetDouble();int p=NativeLobbyPoses.Index(key);
                rig.Reset();NativeLobbyPoses.Sample(rig,"pistol",p,t,out _,out _);rig.WritePose(pose);pose.World();
                double got=hat=="none"?0:hat=="auditor-halo"?NativePortraitRules.HaloTop(pose,t):NativePortraitRules.HatTop(pose,hats[hat].min,hats[hat].max);
                if(p==NativeLobbyPoses.Sleep){var a=NativePortraitRules.Frame(p,.8f,(float)got);var b=NativePortraitRules.Frame(p,.8f,(float)want);Check(a.Half==b.Half&&a.Position==b.Position&&a.Target==b.Target,"sleeping framing ignores the hat");continue;}
                worstTop=Math.Max(worstTop,Math.Abs(got-want));Near(got,want,.012,"hat top "+hat+" "+key+" @"+t);tops++;
            }

            // The portrait mannequin's colours: the exported glow rig's diffuse times the class tint is the portrait rig's.
            var colours=R.GetProperty("colours");var exported=colours.GetProperty("exported");
            Near(Vector3.Distance(V3(exported.GetProperty("body")),NativePortraitRules.Linear(NativePortraitRules.ExportBody)),0,2e-6,"exported body");
            Near(Vector3.Distance(V3(exported.GetProperty("accent")),NativePortraitRules.Ember(NativePortraitRules.ExportAccent)),0,2e-6,"exported accent (ember)");
            Near(Vector3.Distance(V3(exported.GetProperty("edge")),NativePortraitRules.Linear(NativePortraitRules.EdgeOf(NativePortraitRules.ExportBody))),0,2e-6,"exported edge");
            Near(Vector3.Distance(V3(exported.GetProperty("flex")),NativePortraitRules.Linear(NativePortraitRules.FlexOf(NativePortraitRules.ExportBody))),0,2e-6,"exported flex");
            foreach(var s in colours.GetProperty("portrait").EnumerateArray())
            {
                int body=NativePortraitRules.Parse(s.GetProperty("bodyHex").GetString(),-1),accent=NativePortraitRules.Parse(s.GetProperty("accentHex").GetString(),-1);Check(body>=0&&accent>=0,"colour parse");
                Near(Vector3.Distance(V3(exported.GetProperty("body"))*NativePortraitRules.BodyTint(body),V3(s.GetProperty("body"))),0,2e-5,"body "+body.ToString("x6"));
                Near(Vector3.Distance(V3(exported.GetProperty("accent"))*NativePortraitRules.AccentTint(accent),V3(s.GetProperty("accent"))),0,2e-5,"accent "+accent.ToString("x6"));
                Near(Vector3.Distance(V3(exported.GetProperty("edge"))*NativePortraitRules.EdgeTint(body),V3(s.GetProperty("edge"))),0,2e-5,"edge "+body.ToString("x6"));
                Near(Vector3.Distance(V3(exported.GetProperty("flex"))*NativePortraitRules.FlexTint(body),V3(s.GetProperty("flex"))),0,2e-5,"flex "+body.ToString("x6"));
            }
            foreach(var e in colours.GetProperty("emissive").EnumerateObject())Check(V3(e.Value)==Vector3.Zero,"portrait mannequin has no armour glow ("+e.Name+")");
            var classOf=new Dictionary<string,int>{{"body",NativePortraitRules.PartBody},{"accent",NativePortraitRules.PartAccent},{"edge",NativePortraitRules.PartEdge},{"flex",NativePortraitRules.PartFlex},{"hidden",NativePortraitRules.PartHidden},{"other",NativePortraitRules.PartOther}};
            int parts=0;foreach(var part in colours.GetProperty("parts").EnumerateObject()){Check(NativePortraitRules.PartClass(part.Name)==classOf[part.Value.GetString()],"part class "+part.Name);parts++;}
            foreach(string name in PilotParts(root))Check(colours.GetProperty("parts").TryGetProperty(name,out _),"exported pilot part known: "+name);

            // lobbyChair(), the podium and the box.
            var chair=R.GetProperty("chair");Check(chair.GetArrayLength()==NativePortraitRules.Chair.Length,"chair parts");
            for(int i=0;i<NativePortraitRules.Chair.Length;i++){var e=chair[i];var piece=NativePortraitRules.Chair[i];Check(piece.Name==e.GetProperty("name").GetString()&&piece.Color==e.GetProperty("c").GetInt32(),"chair part "+i);Near(Vector3.Distance(piece.Position,V3(e.GetProperty("p"))),0,1e-6,"chair position");Near(Vector3.Distance(piece.Size,V3(e.GetProperty("s"))),0,1e-6,"chair size");}
            var prims=R.GetProperty("primitives");var mesh=new PortraitMesh();
            NativePortraitRules.Cylinder(mesh,NativePortraitRules.PodiumTop,NativePortraitRules.PodiumBottom,NativePortraitRules.PodiumHeight,NativePortraitRules.PodiumSegments);int vertices=SameMesh(mesh,prims.GetProperty("podium"),"podium",Check,Near);
            mesh.Clear();NativePortraitRules.Box(mesh);vertices+=SameTriangles(mesh,prims.GetProperty("box"),"box",Check);

            // The Auditor halo: materials, geometry, the rest build and animateCosmetics at each time.
            var halo=R.GetProperty("halo");
            foreach(var (name,tube,color) in new[]{("arc",NativePortraitRules.HaloArcTube,NativePortraitRules.HaloRed),("aura",NativePortraitRules.HaloAuraTube,NativePortraitRules.HaloGlow)})
            {mesh.Clear();NativePortraitRules.Torus(mesh,NativePortraitRules.HaloArcRadius,tube,4,22,NativePortraitRules.HaloArc);vertices+=SameMesh(mesh,halo.GetProperty(name),"halo "+name,Check,Near);Check(halo.GetProperty(name).GetProperty("color").GetInt32()==color&&!halo.GetProperty(name).GetProperty("toneMapped").GetBoolean(),"halo "+name+" material");}
            Check(halo.GetProperty("aura").GetProperty("additive").GetBoolean()&&!halo.GetProperty("aura").GetProperty("depthWrite").GetBoolean(),"aura blending");Near(halo.GetProperty("aura").GetProperty("opacity").GetDouble(),NativePortraitRules.HaloAuraOpacity,1e-6,"aura opacity");
            var flame=halo.GetProperty("flame");Check(flame.GetProperty("color").GetInt32()==NativePortraitRules.HaloBlack&&flame.GetProperty("doubleSide").GetBoolean(),"flame material");
            var outline=NativePortraitRules.FlameOutline();var fp=flame.GetProperty("positions");Check(outline.Count*3==fp.GetArrayLength(),"flame outline points "+outline.Count);
            for(int i=0;i<outline.Count;i++){Near(outline[i].X,fp[i*3].GetDouble(),1e-6,"flame x "+i);Near(outline[i].Y,fp[i*3+1].GetDouble(),1e-6,"flame y "+i);}
            double Area(IList<int> idx,Func<int,Vector2> at,bool signed){double a=0;for(int i=0;i+2<idx.Count;i+=3){var p0=at(idx[i]);var p1=at(idx[i+1]);var p2=at(idx[i+2]);double s=((p1.X-p0.X)*(p2.Y-p0.Y)-(p1.Y-p0.Y)*(p2.X-p0.X))/2;a+=signed?s:Math.Abs(s);}return a;}
            var fi=new List<int>();foreach(var x in flame.GetProperty("indices").EnumerateArray())fi.Add(x.GetInt32());mesh.Clear();NativePortraitRules.Flame(mesh);
            double mine=Area(mesh.Indices,i=>new Vector2(mesh.Positions[i].X,mesh.Positions[i].Y),true),theirs=Area(fi,i=>outline[i],false);
            Near(mine,theirs,1e-7,"flame triangulation fills the source's area (counter-clockwise)");Check(mesh.Indices.Count==fi.Count,"flame triangle count");
            foreach(var fr in halo.GetProperty("frames").EnumerateArray())
            {
                float t=(float)fr.GetProperty("t").GetDouble();NativePortraitRules.HaloGroup(t,out var gp,out var gq,out float gs);var g=fr.GetProperty("group");
                Near(Vector3.Distance(gp,V3(g.GetProperty("p"))),0,5e-6,"halo group position @"+t);Near(Angle(gq,Q4(g.GetProperty("q"))),0,1e-5,"halo group rotation @"+t);Near(gs,g.GetProperty("s")[0].GetDouble(),1e-6,"halo scale");
                var children=fr.GetProperty("children");Check(children.GetArrayLength()==6+NativePortraitRules.HaloFlames,"halo children");
                for(int i=0;i<6;i++){NativePortraitRules.HaloArcLocal(i/2,out var ap,out var aq);var e=children[i];Check(e.GetProperty("name").GetString()==(i%2==0?"arc":"aura"),"halo order");Near(Vector3.Distance(ap,V3(e.GetProperty("p"))),0,1e-6,"arc position");Near(Angle(aq,Q4(e.GetProperty("q"))),0,1e-5,"arc rotation");}
                for(int n=0;n<NativePortraitRules.HaloFlames;n++){NativePortraitRules.HaloFlame(n,t,out var p,out var q,out var s);var e=children[6+n];Near(Vector3.Distance(p,V3(e.GetProperty("p"))),0,5e-6,"flame position");Near(Angle(q,Q4(e.GetProperty("q"))),0,1e-5,"flame rotation");Near(Vector3.Distance(s,V3(e.GetProperty("s"))),0,5e-6,"flame scale @"+t);}
            }

            // Texture size and the sleep marks (lobby.css; no script to capture: the CSS values themselves).
            Check(NativePortraitRules.TextureSize(390,520,1.2f,1280,out int tw,out int th)&&tw==468&&th==624,"texture size at 1.2");
            Check(NativePortraitRules.TextureSize(390,700,1.6f,1024,out tw,out th)&&th==1024&&tw==Math.Round(390*1.5*1024/1050.0),"texture capped");
            Check(!NativePortraitRules.TextureSize(200,30,1,1280,out _,out _),"too small for a portrait");
            NativePortraitRules.SleepMark(0,0,false,out var off0,out float sc0,out float op0);Check(off0==new Vector2(0,8)&&Math.Abs(sc0-.7f)<1e-6&&op0==0,"sleep mark start");
            NativePortraitRules.SleepMark(0,3.3f*.2f,false,out _,out _,out float op1);Near(op1,.8,1e-5,"sleep mark peak");NativePortraitRules.SleepMark(2,0,true,out var off2,out _,out float op2);Check(off2==new Vector2(27,-27)&&op2==.6f,"reduced sleep marks");
            Console.WriteLine("PASS lobby poses and portrait scene vs the original (lobby-poses.ts, menu-portraits.ts, auditor-halo.ts, character-rig.ts): "+poses+" poses ("+idle+" idle-held), worst joint "+(worstP*1000).ToString("0.0000")+" mm / "+worstQ.ToString("0.000000")+" rad, "+frames+" framings, "+tops+" hat tops (worst "+(worstTop*1000).ToString("0.0")+" mm), "+parts+" part classes, "+vertices+" primitive vertices; "+count+" assertions");
        }
        // The angle between two rotations, precise near zero (acos of a float dot is not: 1e-3 rad at dot 1-1.2e-7).
        private static double Angle(Quaternion a,Quaternion b)
        {var d=Quaternion.Normalize(a)*Quaternion.Conjugate(Quaternion.Normalize(b));double v=Math.Sqrt((double)d.X*d.X+(double)d.Y*d.Y+(double)d.Z*d.Z);return 2*Math.Atan2(v,Math.Abs(d.W));}
        private static int SameMesh(PortraitMesh m,JsonElement e,string what,Action<bool,string> check,Action<double,double,double,string> near)
        {
            var p=e.GetProperty("positions");var n=e.GetProperty("normals");var idx=e.GetProperty("indices");check(m.Positions.Count*3==p.GetArrayLength()&&m.Indices.Count==idx.GetArrayLength(),what+" sizes");
            for(int i=0;i<m.Positions.Count;i++){near(Vector3.Distance(m.Positions[i],new Vector3((float)p[i*3].GetDouble(),(float)p[i*3+1].GetDouble(),(float)p[i*3+2].GetDouble())),0,2e-6,what+" position "+i);near(Vector3.Distance(m.Normals[i],new Vector3((float)n[i*3].GetDouble(),(float)n[i*3+1].GetDouble(),(float)n[i*3+2].GetDouble())),0,2e-6,what+" normal "+i);}
            for(int i=0;i<m.Indices.Count;i++)check(m.Indices[i]==idx[i].GetInt32(),what+" index "+i);
            return m.Positions.Count;
        }
        // The same triangles (positions in winding order, each with its face normal), in any order.
        private static int SameTriangles(PortraitMesh m,JsonElement e,string what,Action<bool,string> check)
        {
            var p=e.GetProperty("positions");var n=e.GetProperty("normals");var idx=e.GetProperty("indices");
            // A triangle's key: its corners from the smallest rotation (winding kept) and its face normal.
            string Key(Vector3 a,Vector3 b,Vector3 c,Vector3 normal){string S(Vector3 v)=>Math.Round(v.X,4)+","+Math.Round(v.Y,4)+","+Math.Round(v.Z,4);string[] rot={S(a)+"|"+S(b)+"|"+S(c),S(b)+"|"+S(c)+"|"+S(a),S(c)+"|"+S(a)+"|"+S(b)};Array.Sort(rot,StringComparer.Ordinal);return rot[0]+"#"+S(normal);}
            Vector3 P(int i)=>new Vector3((float)p[i*3].GetDouble(),(float)p[i*3+1].GetDouble(),(float)p[i*3+2].GetDouble());Vector3 N(int i)=>new Vector3((float)n[i*3].GetDouble(),(float)n[i*3+1].GetDouble(),(float)n[i*3+2].GetDouble());
            var theirs=new List<string>();for(int i=0;i+2<idx.GetArrayLength();i+=3)theirs.Add(Key(P(idx[i].GetInt32()),P(idx[i+1].GetInt32()),P(idx[i+2].GetInt32()),N(idx[i].GetInt32())));
            var mine=new List<string>();for(int i=0;i+2<m.Indices.Count;i+=3)mine.Add(Key(m.Positions[m.Indices[i]],m.Positions[m.Indices[i+1]],m.Positions[m.Indices[i+2]],m.Normals[m.Indices[i]]));
            theirs.Sort(StringComparer.Ordinal);mine.Sort(StringComparer.Ordinal);check(theirs.Count==mine.Count,what+" triangle count");for(int i=0;i<mine.Count;i++)check(mine[i]==theirs[i],what+" triangle "+mine[i]);
            return m.Positions.Count;
        }
        private static JsonDocument models;
        private static JsonElement Models(string root)
        {
            if(models==null)
            {
                using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json")));
                using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
                models=JsonDocument.Parse(reader.ReadToEnd());
            }
            return models.RootElement.GetProperty("models");
        }
        // Head-space bounds of each exported hat model, back in the source frame (the export mirrors x for rig models).
        private static Dictionary<string,(Vector3 min,Vector3 max)> HatBounds(string root)
        {
            var result=new Dictionary<string,(Vector3,Vector3)>();
            foreach(var m in Models(root).EnumerateArray())
            {
                string id=m.GetProperty("id").GetString();if(!id.StartsWith("hat-",StringComparison.Ordinal))continue;Vector3 min=new Vector3(float.MaxValue),max=new Vector3(float.MinValue);
                foreach(var s in m.GetProperty("sections").EnumerateArray()){if(s.GetProperty("bone").GetInt32()!=CapturedRig.Head)throw new Exception("hat section off the head: "+id);var p=s.GetProperty("positions");for(int i=0;i+2<p.GetArrayLength();i+=3){var v=new Vector3(-(float)p[i].GetDouble(),(float)p[i+1].GetDouble(),(float)p[i+2].GetDouble());min=Vector3.Min(min,v);max=Vector3.Max(max,v);}}
                result[id.Substring(4)]=(min,max);
            }
            return result;
        }
        private static IEnumerable<string> PilotParts(string root)
        {
            foreach(var m in Models(root).EnumerateArray())if(m.GetProperty("id").GetString()=="pilot")foreach(var s in m.GetProperty("sections").EnumerateArray())yield return s.GetProperty("part").GetString();
        }
    }
}
#endif
