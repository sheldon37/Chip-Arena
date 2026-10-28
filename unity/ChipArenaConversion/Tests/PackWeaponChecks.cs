#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The pack weapons (Core/NativePackWeapons.cs, Runtime/NativePackWeaponPresentation.cs, docs/BOZO-INTEGRATION.md "Pack
    // weapons"). Without licensed content: every fit lands its grip on the game handle's grip point (the fist the visual rig
    // closes there) and its measured tip on the game's sockets within a few centimetres (blade tips, the shield's edge, the
    // staffs' tip and muzzle, the bow string's anchors at every pull); the shaping keeps the largest piece, splits the marked
    // one, keeps winding and unit normals; the runtime is pinned (the bake first, arena bodies only, the Standard skin, the
    // off-hand frame, the thrown dagger, the bow's limbs, built once). With the weapons bank on this machine (CHIP_PACK_WEAPONS,
    // or the checkout's Assets/LocalAssets), the real meshes are measured the same way.
    internal static class PackWeaponChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Pack weapons: "+what);count++;}
        private static Vector3 M(Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);// source weapon frame -> Unity's
        public static void Run(string root)
        {
            count=0;
            Table(root);Shaping();Bow();Runtime(root);
            string real=Real(root);
            Console.WriteLine("PASS pack weapons: the daggers, shield, bow and five staffs fit the game's grips and sockets; shaping, the bow's limbs and the runtime pinned"+real+" ("+count+" checks)");
        }

        // ---- the table against the game's grips and sockets ----
        private static void Table(string root)
        {
            var specs=new HashSet<string>();
            foreach(var f in NativePackWeapons.Fits)
            {
                Check(specs.Add(f.Spec),"one fit per spec: "+f.Spec);
                Check(NativeWeaponModels.Get(f.Spec)!=null,"a designed game weapon: "+f.Spec);
                Check(Regex.IsMatch(f.Prop??"","^[a-z0-9][a-z0-9-]{0,79}$"),"a bank prop id: "+f.Prop);
                Check(Math.Abs(f.Along.Length()-1)<1e-5f&&Math.Abs(f.Up.Length()-1)<1e-5f&&Math.Abs(Vector3.Dot(f.Along,f.Up))<1e-5f,"square axes: "+f.Spec);
                Check(Math.Abs(Vector3.Dot(Vector3.Cross(f.Right,f.Up),f.Along)-1)<1e-5f,"a turn, not a mirror (the faces keep their winding): "+f.Spec);
                Check(f.Scale>.3f&&f.Scale<2&&f.From.Length==f.To.Length&&f.From.Length>=2,"a scale and an axis map: "+f.Spec);
                for(int i=1;i<f.From.Length;i++){float k=(f.To[i]-f.To[i-1])/(f.From[i]-f.From[i-1]);Check(f.From[i]>f.From[i-1]&&k>.3f&&k<2,"the axis map rises: "+f.Spec);}
                Check(Vector3.Distance(NativePackWeapons.Place(f,f.Grip),f.At)<1e-4f,"the grip lands on At: "+f.Spec);
                Check(f.Handle>.008f&&f.Handle<.036f,"a handle the fist closes round: "+f.Spec);
            }
            foreach(string s in new[]{"daggers","shield","bow","fire","water","lightning","darkness","leet"})Check(specs.Contains(s),"a pack model for "+s);
            foreach(string s in new[]{"pistol","rifle","machinegun","sword","greatsword"})Check(!specs.Contains(s),"the bake's own weapons stay the bake's: "+s);
            Check(NativePackWeapons.For("nothing")==null&&NativePackWeapons.For(null)==null,"no fit for others");

            // Daggers: the reverse grip's centre (both hands), the point on the game's.
            var d=NativePackWeapons.For("daggers");
            Check(d.Dual&&!d.Left&&!d.Seat,"the daggers: both hands, the melee set's own grips");
            Check(Vector3.Distance(d.At,M(NativeVisualRig.Blade(Quaternion.Identity,true).Center))<1e-4f,"the dagger's grip at the reverse grip's centre");
            Check(Vector3.Distance(NativePackWeapons.Place(d,d.Tip),M(NativeWeaponModels.Get("daggers").BladeTip))<.02f,"the dagger's point within 2 cm of the game's blade tip");
            Check(NativePackWeapons.Place(d,d.End).Z>-.21f,"the dagger's pommel within the melee clearance (.19 m behind the grip)");
            // Shield: the left loop at the grip bar the braced fist reaches, the top edge on the game's, its height the game's.
            var sh=NativePackWeapons.For("shield");var bar=NativeVisualRig.ShieldBar(Quaternion.Identity);
            Check(bar.Braced&&Vector3.Distance(sh.At,M(bar.Center))<1e-4f,"the shield's loop at the braced grip bar");
            var top=NativePackWeapons.Place(sh,sh.Tip);
            Check(Vector3.Distance(top,M(NativeWeaponModels.Get("shield").BladeTip))<.06f,"the shield's top edge within 6 cm of the game's edge socket (the pack's heater dips at its middle)");
            Check(top.Z>sh.At.Z+.05f&&Math.Abs(NativePackWeapons.Place(sh,sh.End).Y-(-.34f))<.02f,"the face ahead of the bar; the bottom at the game's");
            // The forearm's side: the game's right forearm leaves the hand toward the frame's +x in the shield set's clips (the
            // second loop goes there: CapturedClips set 3, Unity frame).
            var pose=new CapturedRig.Pose();var channels=new float[CapturedClip.Channels];Vector3 elbow=Vector3.Zero;
            foreach(var clip in CapturedClips.Sets[3])for(int k=0;k<4;k++){clip.Sample(clip.Duration*k/4,channels);CapturedRig.Sample(channels,3,pose);pose.World();elbow+=Vector3.Normalize(Vector3.Transform(pose.WorldPosition[CapturedRig.RElbow]-pose.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(pose.WorldRotation[CapturedRig.RHand])));}
            Check(M(elbow).X>0&&NativePackWeapons.Place(sh,sh.Grip+new Vector3(.154f,0,0)).X>.15f,"the shield's second loop on the forearm's side");
            // Bow: the string nocks on the game's anchors (the string's x offset aside), the game's grip on the riser.
            var b=NativePackWeapons.For("bow");
            Check(b.Left&&b.Limbs&&b.KeepLargest&&!b.Seat,"the bow: the bow hand, its limbs, its own string and arrow dropped");
            for(int t=0;t<2;t++)
            {
                var nock=NativePackWeapons.Place(b,t==0?b.Tip:b.End);var anchor=M(NativeBow.Anchor(t==0,0));
                Check(Math.Abs(nock.Y-anchor.Y)<.005f&&Math.Abs(nock.Z-anchor.Z)<.005f&&Math.Abs(nock.X-anchor.X)<.015f,"the bow's "+(t==0?"upper":"lower")+" nock on the string's anchor");
            }
            var riser=NativePackWeapons.Place(b,b.Grip+new Vector3(0,0,.123f));var grip=M(NativeBow.Grip);
            Check(Math.Abs(grip.Y)<NativeBow.PivotY&&Math.Abs(grip.Z-riser.Z)<b.Handle*b.Scale&&Math.Abs(grip.X)<b.Handle,"the game's bow grip on the riser");
            // Staffs: the left fist at the game's main grip, the crystal on the staff tip, the muzzle (the charge orb) in the head.
            foreach(string s in new[]{"fire","water","lightning","darkness","leet"})
            {
                var f=NativePackWeapons.For(s);var design=NativeWeaponModels.Get(s);
                Check(f.Left&&f.Seat&&!float.IsNaN(f.Mark.X)&&f.Glow>0&&f.Color!=0,s+": the left hand's, the fist closed on it, the crystal in the school's colour");
                Check(Vector3.Distance(NativePackWeapons.Place(f,f.Grip),M(NativeVisualRig.Blade(Quaternion.Identity).Center))<.012f,s+": the grip at the left fist's seat");
                var crystal=NativePackWeapons.Place(f,f.Tip);var head=NativePackWeapons.Place(f,f.End);float muzzle=NativeWeaponDressing.MuzzleZ(s);
                Check(Vector3.Distance(crystal,M(design.StaffTip))<.05f,s+": the crystal within 5 cm of the staff tip");
                Check(muzzle>crystal.Z&&muzzle<head.Z+.01f,s+": the muzzle (charge orb) inside the head");
            }
            Check(Math.Abs(NativePackWeapons.Place(NativePackWeapons.For("leet"),NativePackWeapons.For("leet").End).Z-NativeWeaponDressing.MuzzleZ("leet"))<.02f,"L33t H4x: the top at its muzzle");
            Check(NativePackWeapons.Place(NativePackWeapons.For("fire"),new Vector3(0,-.863f,-.032f)).Z>-.42f,"the staff's heel within the game's (-.41)");
        }

        // ---- shaping on a synthetic bank ----
        private static NativePackBank.Mesh Box(Vector3 c,Vector3 h)
        {
            // A box's six faces, clockwise front faces (Unity), split normals.
            var pos=new List<Vector3>();var nrm=new List<Vector3>();var idx=new List<int>();
            void Face(Vector3 n,Vector3 u,Vector3 v)
            {
                int s=pos.Count;var o=c+n*Vector3.Dot(h,Vector3.Abs(n));var du=u*Vector3.Dot(h,Vector3.Abs(u));var dv=v*Vector3.Dot(h,Vector3.Abs(v));
                pos.Add(o-du-dv);pos.Add(o-du+dv);pos.Add(o+du+dv);pos.Add(o+du-dv);for(int i=0;i<4;i++)nrm.Add(n);
                // Clockwise seen from outside (Unity's front faces): (b - a) x (c - a) along the outward normal.
                idx.AddRange(new[]{s,s+2,s+1,s,s+3,s+2});
            }
            Face(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ);Face(-Vector3.UnitX,Vector3.UnitZ,Vector3.UnitY);Face(Vector3.UnitY,Vector3.UnitZ,Vector3.UnitX);Face(-Vector3.UnitY,Vector3.UnitX,Vector3.UnitZ);Face(Vector3.UnitZ,Vector3.UnitX,Vector3.UnitY);Face(-Vector3.UnitZ,Vector3.UnitY,Vector3.UnitX);
            var m=new NativePackBank.Mesh{Name="box",Positions=pos.ToArray(),Normals=nrm.ToArray(),Uv0=new Vector2[pos.Count],Submeshes=new[]{new NativePackBank.Submesh{Material=0,Indices=idx.ToArray()}}};
            m.Min=c-h;m.Max=c+h;return m;
        }
        // Two boxes in one mesh (stacked: their touching corners weld into one piece).
        private static NativePackBank.Mesh Boxes(NativePackBank.Mesh a,NativePackBank.Mesh b)
        {
            var idx=new List<int>(a.Submeshes[0].Indices);foreach(int i in b.Submeshes[0].Indices)idx.Add(i+a.Positions.Length);
            var pos=new List<Vector3>(a.Positions);pos.AddRange(b.Positions);var nrm=new List<Vector3>(a.Normals);nrm.AddRange(b.Normals);
            return new NativePackBank.Mesh{Name="boxes",Positions=pos.ToArray(),Normals=nrm.ToArray(),Uv0=new Vector2[pos.Count],Submeshes=new[]{new NativePackBank.Submesh{Material=0,Indices=idx.ToArray()}},Min=Vector3.Min(a.Min,b.Min),Max=Vector3.Max(a.Max,b.Max)};
        }
        // The winding Unity draws as front (clockwise seen from the front, left-handed axes): (b - a) x (c - a), computed with
        // System.Numerics' cross product, points along the outward normal.
        private static float Winding(Vector3 a,Vector3 b,Vector3 c,Vector3 n)=>Vector3.Dot(Vector3.Cross(b-a,c-a),n);
        private static void Shaping()
        {
            var bank=new NativePackBank.Bank();bank.Textures.Add(new NativePackBank.Texture{Name="syn"});bank.Materials.Add(new NativePackBank.Material{Name="syn",Albedo=0,Emission=-1,Color=Vector4.One});
            // A long shaft (along prop +y), a small crystal box on top and a loose speck (the largest piece is the shaft).
            bank.Meshes.Add(Boxes(Box(new Vector3(0,.25f,0),new Vector3(.02f,.25f,.02f)),Box(new Vector3(0,.75f,0),new Vector3(.02f,.25f,.02f))));bank.Meshes.Add(Box(new Vector3(0,1.1f,0),new Vector3(.03f,.05f,.03f)));bank.Meshes.Add(Box(new Vector3(.3f,0,0),new Vector3(.01f,.01f,.01f)));
            var parts=new NativePackBank.Part[3];for(int i=0;i<3;i++)parts[i]=new NativePackBank.Part{Mesh=i,Place=Matrix4x4.Identity};
            // The crystal's part placed (turned and moved) to check the parts' places.
            parts[1].Place=Matrix4x4.CreateRotationY(MathF.PI/2);
            bank.Props.Add(new NativePackBank.Prop{Name="syn-staff",Group="staff",Parts=parts});bank.PropIndex["syn-staff"]=0;
            var f=new NativePackWeapons.Fit{Spec="syn",Prop="syn-staff",Grip=new Vector3(0,.2f,0),Along=Vector3.UnitY,Up=-Vector3.UnitZ,Scale=.5f,From=new[]{-.2f,.8f,.95f},To=new[]{-.08f,.32f,.47f},At=new Vector3(0,0,.01f),Mark=new Vector3(0,1.1f,0)};
            var s=NativePackWeapons.Shape(bank,f);
            Check(s!=null&&s.Subs.Count==2&&s.Subs[0].Material==0&&s.Subs[1].Material==-1,"the marked piece is its own (weapon colour) submesh, after the bank's");
            Check(s.Subs[1].Indices.Length==36&&s.Subs[0].Indices.Length==108,"the crystal's 12 triangles marked; the shaft's 24 and the speck's 12 kept");
            foreach(var n in s.Normals)Check(Math.Abs(n.Length()-1)<1e-4f,"unit normals");
            // The axis map: the grip on At, the shaft at .5 per metre, the head at 1 per metre.
            Check(Vector3.Distance(NativePackWeapons.Place(f,new Vector3(0,.2f,0)),f.At)<1e-5f,"the grip on At");
            Check(Math.Abs(NativePackWeapons.Place(f,new Vector3(0,1f,0)).Z-.33f)<1e-4f&&Math.Abs(NativePackWeapons.Place(f,new Vector3(0,1.1f,0)).Z-.43f)<1e-4f,"the axis map's two slopes");
            Check(Math.Abs(NativePackWeapons.Place(f,new Vector3(0,-.5f,0)).Z-(-.27f))<1e-4f,"the end slope carried on");
            Check(Math.Abs(NativePackWeapons.Place(f,new Vector3(.1f,.2f,0)).X-.05f)<1e-5f&&Math.Abs(NativePackWeapons.Place(f,new Vector3(0,.2f,-.1f)).Y-.05f)<1e-5f,"the cross-section at the scale");
            // Winding: every triangle still faces out from its box's centre the same way as before.
            float before=0,after=0;var src=bank.Meshes[0];for(int t=0;t<src.Submeshes[0].Indices.Length;t+=3){var ix=src.Submeshes[0].Indices;before+=Math.Sign(Winding(src.Positions[ix[t]],src.Positions[ix[t+1]],src.Positions[ix[t+2]],src.Normals[ix[t]]));}
            var main=s.Subs[0].Indices;for(int t=0;t<main.Length;t+=3)after+=Math.Sign(Winding(s.Positions[main[t]],s.Positions[main[t+1]],s.Positions[main[t+2]],s.Normals[main[t]]));
            Check(before==24&&after==36,"the faces keep their (Unity front) winding against their normals");
            // The largest piece only: the speck goes.
            f.KeepLargest=true;f.Mark=new Vector3(float.NaN);var k=NativePackWeapons.Shape(bank,f);
            Check(k.Subs.Count==1&&k.Subs[0].Indices.Length==72&&k.Positions.Length==48,"only the largest piece kept, its vertices compacted");
            foreach(var p in k.Positions)Check(Math.Abs(p.X)<.011f&&Math.Abs(p.Y)<.011f,"the kept piece is the shaft");
            Check(NativePackWeapons.Shape(bank,new NativePackWeapons.Fit{Prop="missing"})==null&&NativePackWeapons.Shape(null,f)==null,"no prop, no shape");
        }

        // ---- the bow's limbs ----
        private static void Bow()
        {
            float y0=NativeBow.PivotY,y1=y0+NativePackWeapons.LimbBlend,last=0;
            Check(NativePackWeapons.LimbWeight(0)==0&&NativePackWeapons.LimbWeight(y0)==0&&NativePackWeapons.LimbWeight(-y0)==0,"the riser does not bend");
            Check(NativePackWeapons.LimbWeight(y1)==1&&NativePackWeapons.LimbWeight(NativeBow.TipY)==1&&NativePackWeapons.LimbWeight(-NativeBow.TipY)==-1,"the tips bend as the game's limbs");
            for(float y=0;y<.8f;y+=.01f){float w=NativePackWeapons.LimbWeight(y);Check(w>=last-1e-6f&&Math.Abs(NativePackWeapons.LimbWeight(-y)+w)<1e-6f,"the limb weight rises and is signed by the limb");last=w;}
            var b=NativePackWeapons.For("bow");
            for(float pull=0;pull<=1.0001f;pull+=.125f)for(int t=0;t<2;t++)
            {
                var rest=NativePackWeapons.Place(b,t==0?b.Tip:b.End);var bent=NativePackWeapons.Bent(rest,pull);var anchor=M(NativeBow.Anchor(t==0,pull));
                Check(Math.Abs(bent.Y-anchor.Y)<.005f&&Math.Abs(bent.Z-anchor.Z)<.005f&&Math.Abs(bent.X-anchor.X)<.015f,"the string's anchor stays on the bent nock at pull "+pull);
                // The runtime bends the limb bone by the game's limb piece: the same point.
                var piece=NativeBowRig.Limb(t==0,pull);var src=M(rest);var bone=M(Vector3.Transform(src,piece.Rotation)+piece.Position);
                Check(Vector3.Distance(bone,bent)<1e-4f,"the limb bone's turn is the game's limb piece's");
            }
            Check(NativePackWeapons.Bent(new Vector3(0,.1f,.03f),1)==new Vector3(0,.1f,.03f),"the riser stays at full draw");
        }

        // ---- the runtime ----
        private static void Runtime(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string pack=R("Runtime/NativePackWeaponPresentation.cs"),weapons=R("Runtime/NativeVisualWeaponPresentation.cs"),body=R("Runtime/NativeVisualBodyPresentation.cs"),source=R("Runtime/NativeSourcePresentation.cs"),bow=R("Runtime/NativeBowPresentation.cs"),banks=R("Runtime/NativePackBanks.cs"),core=R("Core/NativePackWeapons.cs");
            Check(banks.Contains("Weapons=\"ChipArenaPackWeapons\"")&&NativePackWeapons.Bank=="ChipArenaPackWeapons","the runtime's weapons bank is the table's");
            Check(weapons.Contains("if(a.WeaponAway||string.IsNullOrEmpty(a.SourceSpec)||WeaponSkin(a)!=NativeWeaponSkins.StandardIndex)return null;\n            if(body.Weapons!=null&&body.Weapons.TryGetValue(a.SourceSpec,out var w))return w;\n            return body.Arena?PackWeaponOf(body,a.SourceSpec):null;"),"the bake's model first, then the pack's, only in hand on the Standard skin and only on an arena pilot's body");
            Check(body.Contains("body.Arena=rigs.TryGetValue(id,out var own)&&own==rig;visualBodies[rig]=body;"),"an arena body is one made for the rig the arena poses (not the entrances', menus', portraits' or first person's)");
            Check(weapons.Contains("body.MainAway=a.MainWeaponAway;")&&weapons.Contains("bool main=!(w.LeftMount!=null&&body.MainAway);"),"the thrown main dagger hides its copy, the off-hand's stays");
            Check(weapons.Contains("if(w.LeftMount!=null&&(!left.HasValue||!VisualWeaponMount(w.LeftMount,left.Value)))return false;"),"a weapon on both hands draws only with the off-hand's frame (else the game's pair draws)");
            Check(source.Contains("VisualWeaponDrawn(rig,WeaponBone(CapturedRig.RHand),a.SourceSpec==\"daggers\"?WeaponBone(CapturedRig.LHand):(Matrix4x4?)null)"),"SourceCharacter passes the off-hand blade's frame (its reach stretch included)");
            Check(weapons.Contains("if(w.Seat){g.LeftGrip=1;g.Left=NativeVisualRig.Blade(System.Numerics.Quaternion.Identity);}"),"a pack staff seats the left fist on its shaft");
            Check(weapons.Contains("public void Hide(){if(Model!=null)Model.SetActive(false);if(LeftModel!=null)LeftModel.SetActive(false);}")&&weapons.Contains("body.Shown.Hide();body.Shown=null;"),"both copies hide the frame they are not drawn");
            int pb=bow.IndexOf("if(PackBowDrawn(rig,bowHand,f.Pull))n+=3;",StringComparison.Ordinal),riser=bow.IndexOf("sourceModels.Queue(riser,root,bowHandBone,",StringComparison.Ordinal),str=bow.IndexOf("string str=std?NativeBowModels.StringId",StringComparison.Ordinal);
            Check(pb>0&&riser>pb&&str>riser,"the pack bow takes the riser's and limbs' place; the string, arrows and quiver draw as before");
            Check(pack.Contains("var piece=BowPiece(NativeBowRig.Limb(l==0,pull));")&&pack.Contains("weights[i]=new BoneWeight{boneIndex0=0,weight0=1-k,boneIndex1=w>0?1:w<0?2:0,weight1=k};"),"the limbs bend as the game's limb pieces, by the shaped weights");
            Check(pack.Contains("if(packWeaponAssets.TryGetValue(spec,out var asset))return asset;")&&pack.Contains("if(body.Packs.TryGetValue(spec,out var w))return w;")&&pack.Contains("mesh.UploadMeshData(true);"),"built once per spec, made once per body and spec");
            Check(pack.Contains("Resources.Load<Shader>(\"NativeVisualBodyToon\")")&&pack.Contains("m.SetFloat(UseCustomId,0);m.SetFloat(UseOutlineId,0);"),"the visual body's toon, the texture as drawn");
            Check(pack.Contains("var bank=fit!=null?NativePackBanks.Bank(NativePackWeapons.Bank):null;")&&pack.Contains("Debug.LogWarning(\"Pack weapon \"+spec+\": \"+e.Message+\"; the game's model draws.\");"),"no bank or a failure: the game's model");
            foreach(string name in new[]{"Toon Harbor","Synty","polyperfect","POLYGON","Polygon","RB_","SM_","Gladius","Knights","Fantasy","Low Poly","Kronnect","Archanor"})
            {Check(!pack.Contains(name)&&!core.Contains(name)&&!weapons.Contains(name),"no pack names in the pack weapons' code: "+name);}
            // The selection names each fit's prop, in the table's bank.
            string sel=File.ReadAllText(Path.Combine(root,"tools","pack_bank","selections","weapons.json"));
            Check(sel.Contains("\"bank\": \"ChipArenaPackWeapons\""),"the selection writes the weapons bank");
            foreach(var f in NativePackWeapons.Fits)Check(sel.Contains("\"id\": \""+f.Prop+"\""),"the selection carries "+f.Prop);
        }

        // ---- the real bank, where this machine has it ----
        private static string Real(string root)
        {
            string path=Environment.GetEnvironmentVariable("CHIP_PACK_WEAPONS");
            if(string.IsNullOrEmpty(path))path=Path.Combine(root,"Assets","LocalAssets","ChipArenaLocal","Resources",NativePackWeapons.Bank+".bytes");
            if(!File.Exists(path)){Console.WriteLine("INFO pack weapons: no weapons bank here (licensed; set CHIP_PACK_WEAPONS to measure the real meshes)");return "";}
            var bank=NativePackBank.Read(File.ReadAllBytes(path));
            foreach(var f in NativePackWeapons.Fits)
            {
                var s=NativePackWeapons.Shape(bank,f);Check(s!=null&&s.Positions.Length>0,"the bank carries "+f.Prop);
                Check(s.Triangles<=NativeWeaponModels.Budget[NativeWeaponModels.Full]&&s.Triangles>40,f.Spec+": within the full tier's triangle budget ("+s.Triangles+")");
                Vector3 lo=new Vector3(float.MaxValue),hi=new Vector3(float.MinValue);foreach(var p in s.Positions){lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}
                // The handle's section where the fist closes: where the triangles' edges cross the plane across the handle there,
                // their distance from its line (a blade's or shaft's grip along z at At; the shield's loop and the bow's riser
                // stand along y: the loop at its bar, the riser at the game's bow grip).
                bool upright=f.Spec=="shield"||f.Limbs;var centre=f.Limbs?new Vector3(0,M(NativeBow.Grip).Y,NativePackWeapons.Place(f,f.Grip+new Vector3(0,0,.123f)).Z):f.At;float reach=f.Spec=="shield"?.03f:.05f,r=0;int hits=0;
                foreach(var sub in s.Subs)for(int t=0;t+2<sub.Indices.Length;t+=3)for(int e=0;e<3;e++)
                {
                    var a=s.Positions[sub.Indices[t+e]]-centre;var c=s.Positions[sub.Indices[t+(e+1)%3]]-centre;float da=upright?a.Y:a.Z,dc=upright?c.Y:c.Z;
                    if(da*dc>0||Math.Abs(da-dc)<1e-7f)continue;var q=Vector3.Lerp(a,c,da/(da-dc));float dist=upright?new Vector2(q.X,q.Z).Length():new Vector2(q.X,q.Y).Length();
                    if(dist<reach){r=Math.Max(r,dist);hits++;}
                }
                Check(hits>=3&&r>.008f&&r<.045f,f.Spec+": a handle the fist closes round at the grip (radius "+r.ToString("0.000")+" m)");
                if(Environment.GetEnvironmentVariable("CHIP_PACK_WEAPONS_INFO")=="1")Console.WriteLine("INFO pack weapon "+f.Spec+": "+s.Triangles+" triangles, handle radius "+r.ToString("0.000")+" m, z "+lo.Z.ToString("0.000")+" to "+hi.Z.ToString("0.000"));
                if(f.Spec=="daggers")Check(Math.Abs(hi.Z-NativeWeaponModels.Get("daggers").BladeTip.Z)<.01f&&lo.Z>-.21f,"the gladius reaches the game's blade tip, its pommel inside the clearance");
                if(f.Spec=="shield")
                {
                    float edge=float.MinValue;Vector3 at=default;foreach(var p in s.Positions)if(Math.Abs(p.X)<.03f&&p.Y>edge){edge=p.Y;at=p;}
                    Check(Vector3.Distance(at,M(NativeWeaponModels.Get("shield").BladeTip))<.06f&&Math.Abs(lo.Y-(-.34f))<.02f&&Math.Abs(hi.Y-.425f)<.02f,"the shield's top edge on the game's edge socket, its height the game's");
                }
                if(f.Limbs)for(int t=0;t<2;t++)
                {
                    var anchor=M(NativeBow.Anchor(t==0,0));float near=float.MaxValue;foreach(var p in s.Positions)near=Math.Min(near,Vector3.Distance(p,anchor));
                    Check(near<.025f,"the bow's "+(t==0?"upper":"lower")+" tip at the string's anchor ("+near.ToString("0.000")+" m)");
                    Check(Math.Abs(t==0?hi.Y:-lo.Y)<.7f,"the bow no longer than the game's by more than its recurve");
                }
                if(!float.IsNaN(f.Mark.X))
                {
                    var tint=s.Subs.Find(x=>x.Material<0);Check(tint.Indices!=null&&tint.Indices.Length>=12,f.Spec+": the crystal is marked");
                    var c=Vector3.Zero;foreach(int i in tint.Indices)c+=s.Positions[i];c/=tint.Indices.Length;
                    Check(Vector3.Distance(c,M(NativeWeaponModels.Get(f.Spec).StaffTip))<.06f,f.Spec+": the crystal at the staff tip");
                    Check(hi.Z>NativeWeaponDressing.MuzzleZ(f.Spec)-.01f&&lo.Z>(f.Spec=="leet"?-.55f:-.42f),f.Spec+": the head reaches the muzzle, the heel within the game's");
                }
            }
            return "; the real meshes measured";
        }
    }
}
#endif
