#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
namespace ChipArena.UnityConversion
{
    // Chip modules on the weapon skins (owner 2026-10-12: "improve chip upgrade displays on newer weapons (Change based on
    // skin). The upgrades look out of place on the modern weapons"). The Prototype Series keeps the source's pieces byte for
    // byte: the files that build and queue them are pinned to the base, and so is a digest of their output. The Standard
    // modules (Core/NativeChipModules*.cs) show exactly the kinds the source shows; every part sits on its Standard model (inside
    // its bounds plus a small standoff, touching its surface, or close to it for the hovering hardlight parts); every weapon with a
    // model has sockets and they lie on the model; nothing crosses the first-person sight line; the three tiers thin out; the
    // per-frame route allocates nothing; and the Runtime routes each skin (source text: Unity cannot run here).
    internal static class ChipModuleChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Chip modules: "+what);count++;}
        // The base (f575670) files the Prototype pieces come from, byte-identical, and their output over every weapon's loadouts.
        private const string DressingSha="38c5212e42a9baf45dc14382f32a7c2655bbcb82c4c942d07a95ef2588060a6c",PrimitivesSha="cc7fa54dd5dc6aeec35110a11cb2a5be142883decc5dc65cc82d061d45fd6b61";
        private const string PrototypeDigest="c7d7877b6c6f734f";
        public const float Standoff=.05f,Contact=.012f,Hover=.06f,SightCone=6f;
        private sealed class Loadout{public string Name;public Dictionary<string,int> Chips;public ChipStats Stats;public bool Fire,Poison;public int Ember,Motes;}
        private static readonly Dictionary<string,Loadout[]> allocLoads=new Dictionary<string,Loadout[]>();
        private static Loadout Make(ConversionContent data,string spec,string name,Dictionary<string,int> chips,Action<ChipStats> tweak=null)
        {
            var st=NativeContentRules.Stats(data,spec,chips);tweak?.Invoke(st);var l=new Loadout{Name=name,Chips=chips,Stats=st};
            if(NativeWeaponDressing.Melee(spec)){l.Fire=st["burn"]>0;l.Poison=st["poison"]>0;chips.TryGetValue("ember",out l.Ember);chips.TryGetValue("poison",out l.Motes);}return l;
        }
        // One loadout per kind alone, then mixed: three kinds, and the full stack of every kind with both enchants.
        private static List<Loadout> Loadouts(ConversionContent data,string spec)
        {
            var list=new List<Loadout>();var f=NativeChipModules.Family(spec);string rate=f==ChipFamily.Gun?"fire-rate":f==ChipFamily.Staff?"cast-rate":"swing-rate";
            Dictionary<string,int> C(params (string,int)[] v)=>v.ToDictionary(x=>x.Item1,x=>x.Item2);
            list.Add(Make(data,spec,"multishot",C(),s=>s["extraProjectiles"]=6));list.Add(Make(data,spec,"ricochet",C(("ricochet",3))));list.Add(Make(data,spec,"velocity",C(("velocity",4))));
            list.Add(Make(data,spec,"homing",C(),s=>s["homing"]=35));list.Add(Make(data,spec,"split",C(),s=>s["split"]=1));list.Add(Make(data,spec,"pierce",C(),s=>s["pierces"]=1));
            list.Add(Make(data,spec,"magazine",C(("magazine",8))));list.Add(Make(data,spec,"pong",C(),s=>s["pong"]=1));list.Add(Make(data,spec,"boomerang",C(),s=>s["boomerang"]=1));
            list.Add(Make(data,spec,"vacuum",C(),s=>s["vacuum"]=1400));list.Add(Make(data,spec,"rate",C((rate,3),("overclock",2))));list.Add(Make(data,spec,"kinetic",C(),s=>s["impulse"]=2.4));
            list.Add(Make(data,spec,"demolition",C(("demolition",2))));list.Add(Make(data,spec,"shrapnel",C(),s=>s["shrapnel"]=6));list.Add(Make(data,spec,"static",C(),s=>s["staticEvery"]=4));
            list.Add(Make(data,spec,"reach",C(),s=>s["reach"]=1.4));list.Add(Make(data,spec,"echo",C(),s=>s["extraStrikes"]=1));
            list.Add(Make(data,spec,"three",f==ChipFamily.Gun?C(("velocity",2),("fire-rate",1),("magazine",3)):f==ChipFamily.Staff?C(("multishot",2),("ricochet",1),("static",1)):C(("reach",2),("ember",3),("kinetic",1))));
            var full=C(("ricochet",3),("velocity",4),("magazine",8),(rate,3),("overclock",2),("demolition",1),("ember",4),("poison",3));
            list.Add(Make(data,spec,"full",full,s=>{s["extraProjectiles"]=6;s["homing"]=35;s["split"]=1;s["pierces"]=1;s["pong"]=1;s["boomerang"]=1;s["vacuum"]=1400;s["impulse"]=2.4;s["shrapnel"]=6;s["staticEvery"]=3;s["reach"]=1.4;s["extraStrikes"]=1;}));
            return list;
        }
        // The modules then the enchant, as the Runtime draws them (two passes of at most MaxModules each), into one list `o`.
        private static readonly ChipModule[] pass=new ChipModule[NativeChipModules.MaxModules];
        private static int Standard(string spec,ConversionContent data,Loadout l,ChipModule[] o)
        {
            var w=data.weapons.First(x=>x.id==spec);int n=NativeChipModules.Modules(spec,w.@class=="ranged",(int)w.color,l.Chips,l.Stats,pass);Array.Copy(pass,o,n);
            if(NativeWeaponDressing.Melee(spec)&&(l.Fire||l.Poison)){int f=NativeChipModules.Enchant(spec,l.Fire,l.Poison,l.Ember,l.Motes,pass,0);Array.Copy(pass,0,o,n,f);n+=f;}return n;
        }
        private static Matrix4x4 Local(in ChipModule p)=>Matrix4x4.CreateScale(p.Scale)*Matrix4x4.CreateFromQuaternion(p.Rotation)*Matrix4x4.CreateTranslation(p.Position);
        private static readonly Dictionary<(string,ChipPart,int,int,bool),WeaponMesh> parts=new Dictionary<(string,ChipPart,int,int,bool),WeaponMesh>();
        private static WeaponMesh Part(string spec,ChipPart p,int v,int tier=0,bool left=false){var key=(NativeChipModules.Shared(p)?null:spec,p,v,tier,left);if(!parts.TryGetValue(key,out var m))parts[key]=m=NativeChipModules.Build(spec,p,v,tier,left);return m;}

        public static void Run(string root,ConversionContent data)
        {
            count=0;string Code(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));string Sha(string f){using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root,"Assets/ChipArena",f))).Select(b=>b.ToString("x2")));}
            var designed=NativeWeaponModels.Specs.ToArray();var cm=new ChipModule[NativeChipModules.MaxModules*2];var cm64=new ChipModule[NativeChipModules.MaxModules];var pieces=new WeaponPiece[NativeWeaponDressing.MaxPieces];

            // 1. The Prototype Series is unchanged: its piece source and primitives are the base files, its output digest is pinned,
            // and the Runtime routes a Prototype pick (and every weapon without a Standard design) through the source pieces.
            Check(Sha("Core/NativeWeaponDressing.cs")==DressingSha,"NativeWeaponDressing.cs (the Prototype pieces) is byte-identical to the base");
            Check(Sha("Runtime/NativeSourcePrimitives.cs")==PrimitivesSha,"NativeSourcePrimitives.cs (the Prototype primitives) is byte-identical to the base");
            var digest=new StringBuilder();
            foreach(var w in data.weapons)foreach(var l in Loadouts(data,w.id))
            {
                int n=NativeWeaponDressing.Modules(w.id,w.@class=="ranged",(int)w.color,l.Chips,l.Stats,pieces);digest.Append(w.id).Append(l.Name).Append(n);
                for(int i=0;i<n;i++)foreach(float t in new[]{0f,1.37f}){var p=pieces[i];NativeWeaponDressing.Animate(ref p,t);Piece(digest,p);}
                int f=NativeWeaponDressing.Enchant(w.id,l.Fire,l.Poison,l.Ember,l.Motes,pieces,0);digest.Append('e').Append(f);for(int i=0;i<f;i++){var p=pieces[i];NativeWeaponDressing.Animate(ref p,.61f);Piece(digest,p);}
            }
            string proto=Hash(digest.ToString());Console.WriteLine("  prototype piece digest "+proto);Check(proto==PrototypeDigest,"the Prototype pieces' output matches the base digest ("+proto+")");
            string dressing=Code("Runtime/NativeWeaponPresentation.cs"),route=Code("Runtime/NativeChipModulePresentation.cs");
            Check(dressing.Contains("if(StandardChips(a)){StandardDressing(rig,a,time,first,layer,camera,reach,pilot);return;}")&&dressing.Contains("int count=NativeWeaponDressing.Modules(spec,pilot.Weapon.@class==\"ranged\",(int)pilot.Weapon.color,chips,stats,weaponPieces);")
                &&dressing.Contains("for(int i=0;i<count;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,first?parent:SkinPiece(a,p,parent),layer,camera,rigFactor);}")
                &&dressing.Contains("for(int i=0;i<flames;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,SkinPiece(a,p,parent),layer,camera);}"),"WeaponDressing keeps the source piece route after the Standard branch");
            Check(route.Contains("private bool StandardChips(Actor a)=>a!=null&&sourceModels!=null&&NativeChipModules.Designed(a.SourceSpec)&&WeaponSkin(a)==NativeWeaponSkins.StandardIndex;"),"only a Standard pick of a designed weapon leaves the source route");
            Check(dressing.Contains("if(ThrownStandardEnchant(shot,matrix,time,fire,poison,ember,motes))return;\n            int count=NativeWeaponDressing.Enchant(shot.Shield?\"shield\":\"daggers\",fire,poison,ember,motes,weaponPieces,0);")
                &&route.Contains("NativeWeaponSkins.Index(shot.Original.Seat.weaponSkins,spec)!=NativeWeaponSkins.StandardIndex"),"a thrown Prototype dagger or shield keeps the source enchant");
            Check(!NativeChipModules.Designed("emperor")&&!NativeChipModules.Designed("ansatsuken")&&!NativeChipModules.Designed("siren")&&NativeChipModules.Sockets("emperor")==null,"fists and the Siren stay bare");
            foreach(var spec in new[]{"emperor","ansatsuken","siren"}){var l=Loadouts(data,spec).Last();Check(NativeChipModules.Modules(spec,false,0,l.Chips,l.Stats,cm64)==0&&NativeChipModules.Enchant(spec,true,true,6,6,cm64,0)==0,spec+" carries no Standard module or enchant");}

            // 2. Kind parity: every loadout shows the same kinds on both skins (the kinds' colours are the source's), and the
            // Standard enchant has the source's flame and mote counts plus its edge strip.
            foreach(var spec in designed.Where(NativeChipModules.Designed))
            {
                var w=data.weapons.First(x=>x.id==spec);
                foreach(var l in Loadouts(data,spec))
                {
                    int pn=NativeWeaponDressing.Modules(spec,w.@class=="ranged",(int)w.color,l.Chips,l.Stats,pieces);int sn=NativeChipModules.Modules(spec,w.@class=="ranged",(int)w.color,l.Chips,l.Stats,cm64);
                    var pc=new HashSet<int>();for(int i=0;i<pn;i++)if(pieces[i].Color!=0x647e89)pc.Add(pieces[i].Color);var sc=new HashSet<int>();for(int i=0;i<sn;i++)sc.Add(cm64[i].Color);
                    Check(pc.SetEquals(sc),spec+" "+l.Name+": the Standard modules show the source's kinds ("+string.Join(",",pc.Select(c=>c.ToString("x6")))+" / "+string.Join(",",sc.Select(c=>c.ToString("x6")))+")");
                    Check(sn<=NativeChipModules.MaxModules,spec+" "+l.Name+" within MaxPieces");
                    if(!NativeWeaponDressing.Melee(spec)||!(l.Fire||l.Poison))continue;
                    int pf=NativeWeaponDressing.Enchant(spec,l.Fire,l.Poison,l.Ember,l.Motes,pieces,0),sf=NativeChipModules.Enchant(spec,l.Fire,l.Poison,l.Ember,l.Motes,cm64,0);
                    Check(sf==pf+(l.Fire?1:0)+(l.Poison?1:0)&&cm64.Take(sf).Count(p=>p.Part==ChipPart.Flame)==pieces.Take(pf).Count(p=>p.Enchant==NativeWeaponDressing.FireEnchant)&&cm64.Take(sf).Count(p=>p.Part==ChipPart.Mote)==pieces.Take(pf).Count(p=>p.Enchant==NativeWeaponDressing.PoisonEnchant),spec+" "+l.Name+" enchant counts");
                }
            }

            // 3. Sockets for every weapon with a model, lying on that model.
            float worstSocket=0;
            foreach(var spec in designed)
            {
                if(NativeWeaponDressing.Fist(spec)){Check(NativeChipModules.Sockets(spec)==null,spec+" (fists) has no sockets");continue;}
                var s=NativeChipModules.Sockets(spec);Check(s!=null&&s.Family==NativeChipModules.Family(spec)&&s.Spec==spec,spec+" has its socket table");
                var d=NativeWeaponModels.Get(spec);var model=NativeWeaponModels.Build(spec,0);var tris=Triangles(model,d.Bone);
                float On(Vector3 at)=>Distance(tris,at);void OnModel(Vector3 at,string what,float tol=.015f){float e=On(at);worstSocket=Math.Max(worstSocket,e);Check(e<=tol,spec+" socket "+what+" lies on the model ("+e.ToString("0.0000")+" m at "+at+")");}
                if(s.Family==ChipFamily.Gun||s.Family==ChipFamily.Staff)
                {
                    Check(s.Sections.Length>0&&s.RicochetZ.Length==3&&s.VelocityZ.Length==4&&s.RicochetS.Length==3&&s.VelocityS.Length==4,spec+" ring stations");
                    // Each station's section clears and hugs the model's own slice at that z.
                    for(int i=0;i<7;i++)
                    {
                        float z=i<3?s.RicochetZ[i]:s.VelocityZ[i-3];var sec=s.Sections[i<3?s.RicochetS[i]:s.VelocityS[i-3]];Check(NativeChipModules.Slice(model,d.Bone,null,z,out float lo,out float hi,out float hy),spec+" station "+z+" crosses the model");
                        Check(Math.Max(-lo,hi)<=sec.HalfX+.0025f&&Math.Max(-lo,hi)>=sec.HalfX-.008f,spec+" station "+z+" wraps the model's width ("+lo+".."+hi+" / "+sec.HalfX+")");
                    }
                    if(s.HelixLength>0)foreach(float dz in new[]{-.5f,0,.5f}){float z=s.HelixZ+dz*s.HelixLength;NativeChipModules.Slice(model,d.Bone,null,z,out float lo,out float hi,out _);Check(Math.Max(-lo,hi)<s.HelixR-.003f,spec+" rate coil clears the model at "+z);}
                }
                if(s.Family==ChipFamily.Gun)
                {
                    foreach(var run in new[]{("top",s.Top),("side",s.Side),("under",s.Under)})if(run.Item2.Open)foreach(float u in new[]{0f,.5f,1f})OnModel(new Vector3(run.Item2.X,run.Item2.Y,run.Item2.Z0+(run.Item2.Z1-run.Item2.Z0)*u),run.Item1+" run");
                    foreach(var slot in s.Pods)OnModel(new Vector3(slot.X,slot.Y,s.PodZ),"pod slot");OnModel(s.MagAt,"magazine",.02f);Check(s.Device.HalfX>0&&s.DeviceR>s.Device.Reach&&s.IntakeR>s.Device.Reach,spec+" muzzle device rings clear the device");
                    if(s.Flywheel.Set)OnModel(s.Flywheel.At,"flywheel");OnModel(s.VaneMount.At,"vane mount");
                }
                else if(s.Family==ChipFamily.Staff)
                {
                    OnModel(new Vector3(s.ShaftR,0,(s.LowZ0+s.LowZ1)/2),"lower shaft");OnModel(new Vector3(0,s.ShaftR,s.ChargeZ),"charge band");OnModel(s.SeekerAt,"seeker");
                    foreach(float a in new[]{0f,(float)Math.PI/3,-(float)Math.PI/3})OnModel(new Vector3(s.CollarR*(float)Math.Cos(a),s.CollarR*(float)Math.Sin(a),s.CollarZ),"collar ring",.02f);
                    Check(s.HeadR>0&&s.DroneR>s.HeadR*.55f&&s.IntakeR>s.HeadR*.6f&&s.GimbalR>s.HeadR*.6f&&Math.Abs(s.GimbalR-s.IntakeR)>=.008f,spec+" head rings (the vacuum ring and the gimbal apart)");
                }
                else if(s.Family==ChipFamily.Blade)
                {
                    Check(s.ProfileZ.Length>=10,spec+" blade profile");
                    for(int i=0;i<s.ProfileZ.Length;i++){NativeChipModules.Slice(model,d.Bone,WeaponStretch.Blade,s.ProfileZ[i],out float lo,out float hi,out float hy);Check(Math.Abs(lo-s.ProfileMinX[i])<1e-4f&&Math.Abs(hi-s.ProfileMaxX[i])<1e-4f&&Math.Abs(hy-s.ProfileHalfY[i])<1e-4f,spec+" profile matches the blade at "+s.ProfileZ[i]);}
                    Check(s.StripZ0>=d.StretchBase&&s.StripZ1<=d.StretchTip&&s.TipZ>s.StripZ0&&s.TipZ<d.StretchTip,spec+" strips run on the blade");
                    foreach(var m in new[]{("kinetic",s.Kinetic),("static",s.Static),("split",s.Split),("homing",s.Homing),("vane",s.Vane)})OnModel(m.Item2.At,m.Item1);
                    OnModel(new Vector3(0,s.Charges.Size,s.Charges.At.Z),"charge band",.012f);OnModel(s.Cells.At,"cells");
                }
                else
                {
                    foreach(var m in s.Mounts.Concat(new[]{s.Static,s.Split,s.Kinetic,s.Vane}))OnModel(m.At,"rim mount");OnModel(s.Homing.At,"face seeker");OnModel(s.Charges.At,"top edge");OnModel(s.Cells.At,"back");
                }
            }

            // 4. Every Standard piece (each kind alone, mixed, the full stack; four moments of the animation) inside its model's
            // bounds plus the standoff, and touching it (mounted parts) or close to it (hovering hardlight parts).
            float worstOut=0,worstContact=0,worstHover=0;int placed=0;
            foreach(var spec in designed.Where(NativeChipModules.Designed))
            {
                var d=NativeWeaponModels.Get(spec);var model=NativeWeaponModels.Build(spec,0);var tris=Triangles(model,d.Bone);model.Bounds(d.Bone,out var mn,out var mx);mn-=new Vector3(Standoff);mx+=new Vector3(Standoff);
                foreach(var l in Loadouts(data,spec))
                {
                    int n=Standard(spec,data,l,cm);var stack=new List<Vector3>();
                    for(int i=0;i<n;i++)foreach(float t in new[]{0f,.37f,1.1f,2.9f})
                    {
                        var p=cm[i];NativeChipModules.Animate(ref p,t);var mesh=Part(spec,p.Part,p.Variant);Check(mesh!=null,spec+" "+l.Name+" "+p.Part+" has a mesh");var M=Local(p);
                        bool hover=p.Part==ChipPart.Drone||p.Part==ChipPart.Gimbal||p.Part==ChipPart.Spinner||p.Part==ChipPart.Mote||p.Part==ChipPart.Intake&&NativeChipModules.Family(spec)!=ChipFamily.Gun;float allow=hover?Hover:Contact;
                        // Magazine cells stack: each one touches the model or the cell before it.
                        bool cell=p.Part==ChipPart.Cell||p.Part==ChipPart.Cap;var w=new List<Vector3>();var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
                        foreach(var sec in mesh.Sections)foreach(var v in sec.Positions){var q=Vector3.Transform(v,M);w.Add(q);lo=Vector3.Min(lo,q);hi=Vector3.Max(hi,q);}
                        float out_=Math.Max(0,Math.Max(Math.Max(mn.X-lo.X,hi.X-mx.X),Math.Max(Math.Max(mn.Y-lo.Y,hi.Y-mx.Y),Math.Max(mn.Z-lo.Z,hi.Z-mx.Z))));worstOut=Math.Max(worstOut,out_);
                        Check(out_<=0,spec+" "+l.Name+" "+p.Part+" within the model's bounds plus "+Standoff+" m (by "+out_+")");
                        var near_=tris.Where(x=>x.Max.X>=lo.X-allow&&x.Min.X<=hi.X+allow&&x.Max.Y>=lo.Y-allow&&x.Min.Y<=hi.Y+allow&&x.Max.Z>=lo.Z-allow&&x.Min.Z<=hi.Z+allow).ToList();float near=allow*4;
                        foreach(var q in w){if(near<1e-4f)break;near=Math.Min(near,Distance(near_,q,near));if(cell)foreach(var c in stack)near=Math.Min(near,Vector3.Distance(c,q));}
                        if(hover)worstHover=Math.Max(worstHover,near);else worstContact=Math.Max(worstContact,near);
                        Check(near<=allow,spec+" "+l.Name+" "+p.Part+(hover?" hovers close to":" touches")+" the model ("+near.ToString("0.0000")+" m)");placed++;
                        if(cell&&t==0)stack.AddRange(w);
                    }
                }
            }

            // 5. First person: on the view weapon (the harness approximation of the Unity view rig: guns and staffs re-root their
            // route rig on the camera, melee holds the captured idle; the staff turned and sized as StaffMatrix does), no module
            // vertex comes within SightCone degrees of the crosshair.
            float worstSight=180,weaponSight=180;string worstAt="";
            foreach(var spec in designed.Where(NativeChipModules.Designed))
            {
                var a=Pilot(spec);var eye=new Vector3(0,.865f+NativeViewRules.EyeHeight,0);var fwd=Vector3.Normalize(new Vector3(0,-.08f,1));var camQ=LookQ(fwd);var bones=ViewBones(spec,a,1.48f,eye,camQ);
                Func<int,Matrix4x4> frame=hand=>FirstFrame(spec,a,bones,hand,camQ);float Angle(Vector3 w)=>(float)(Math.Acos(MathUtil.Clamp(Vector3.Dot(Vector3.Normalize(w-eye),fwd),-1,1))*180/Math.PI);
                float own=180;foreach(var sec in NativeWeaponModels.Build(spec,0).Sections)foreach(var v in sec.Positions)own=Math.Min(own,Angle(Vector3.Transform(Mx(v),frame(sec.Bone==NativeWeaponModels.LeftHand&&(NativeWeaponModels.Get(spec)?.Dual??false)?1:0))));
                weaponSight=Math.Min(weaponSight,own);if(Environment.GetEnvironmentVariable("CHIP_SIGHT")=="1")Console.WriteLine("    weapon "+spec+" nearest "+own.ToString("0.0")+" deg");
                foreach(var l in Loadouts(data,spec))
                {
                    int n=Standard(spec,data,l,cm);
                    for(int hand=0;hand<(spec=="daggers"?2:1);hand++)for(int i=0;i<n;i++)foreach(float t in new[]{0f,1.1f})
                    {
                        var p=cm[i];NativeChipModules.Animate(ref p,t);bool left=hand==1&&NativeWeaponModels.Get(spec).MirrorLeft;if(left){p.Position.X=-p.Position.X;p.Rotation=NativeChipModules.Mirrored(p.Rotation);}
                        var mesh=Part(spec,p.Part,p.Variant,0,left&&NativeChipModules.HasLeft(spec,p.Part));var M=Matrix4x4.CreateScale(p.Scale)*Matrix4x4.CreateFromQuaternion(Mx(p.Rotation))*Matrix4x4.CreateTranslation(Mx(p.Position))*frame(hand);float worst=180;
                        foreach(var sec in mesh.Sections)foreach(var v in sec.Positions)worst=Math.Min(worst,Angle(Vector3.Transform(Mx(v),M)));
                        if(worst<worstSight){worstSight=worst;worstAt=spec+" "+l.Name+" "+p.Part;}
                        if(Environment.GetEnvironmentVariable("CHIP_SIGHT")=="1"){if(worst<12)Console.WriteLine("    sight "+spec+" "+l.Name+" "+p.Part+" "+worst.ToString("0.0"));continue;}
                        Check(worst>=SightCone,spec+" "+l.Name+" "+p.Part+" stays out of the first-person sight line ("+worst.ToString("0.0")+" deg)");
                    }
                }
            }

            // 6. Part meshes: every one the bank builds exists, is valid (finite, unit normals, no degenerate triangle, tags 0 or 1),
            // deterministic, has a unique cached id, thins out tier by tier; the left dagger's copies mirror the right. The full
            // stack of every kind at once (more than any real loadout: most kinds are tag-exclusive) stays within a budget per blade.
            var seen=new HashSet<string>();int meshes=0;var tierTris=new Dictionary<(string,ChipPart,int),int[]>();
            foreach(var m in NativeChipModules.Meshes())
            {
                string what=(m.spec??"shared")+" "+m.part+" v"+m.variant+" t"+m.tier+(m.left?" left":"");var a=NativeChipModules.Build(m.spec,m.part,m.variant,m.tier,m.left);var b=NativeChipModules.Build(m.spec,m.part,m.variant,m.tier,m.left);
                Check(a!=null&&a.Triangles>0,what+" builds");var id=NativeChipModules.ModelId(m.spec,m.part,m.variant,m.tier,m.left);Check(id!=null&&seen.Add(id),what+" has a unique id ("+id+")");
                Check(a.Sections.Count==b.Sections.Count&&a.Sections.Zip(b.Sections,(x,y)=>x.Positions.SequenceEqual(y.Positions)&&x.Indices.SequenceEqual(y.Indices)&&x.Emission.SequenceEqual(y.Emission)).All(x=>x),what+" builds deterministically");
                foreach(var sec in a.Sections)
                {
                    Check(sec.Bone==-1&&sec.Stretch==WeaponStretch.None&&sec.Vertices<65535&&sec.Indices.All(i=>i>=0&&i<sec.Vertices)&&sec.Normals.Count==sec.Vertices&&sec.Colors.Count==sec.Vertices&&sec.Emission.Count==sec.Vertices,what+" section arrays");
                    Check(sec.Positions.All(q=>MathUtil.Finite(q.X)&&MathUtil.Finite(q.Y)&&MathUtil.Finite(q.Z))&&sec.Normals.All(q=>Math.Abs(q.Length()-1)<1e-3f),what+" finite positions, unit normals");
                    Check(sec.Emission.All(e=>e.W==0||e.W==1),what+" emission tags untinted (0) or Chip-coloured (1)");bool degenerate=false;
                    for(int i=0;i+2<sec.Indices.Count;i+=3){var p0=sec.Positions[sec.Indices[i]];if(!(Vector3.Cross(sec.Positions[sec.Indices[i+1]]-p0,sec.Positions[sec.Indices[i+2]]-p0).Length()>1e-12f))degenerate=true;}Check(!degenerate,what+" has no degenerate triangle");
                }
                if(m.left){var r=NativeChipModules.Build(m.spec,m.part,m.variant,m.tier,false);Check(r.Sections.Zip(a.Sections,(x,y)=>x.Positions.Zip(y.Positions,(u,v)=>u.X==-v.X&&u.Y==v.Y&&u.Z==v.Z).All(o=>o)).All(o=>o),what+" mirrors the right dagger's");}
                else{var key=(m.spec,m.part,m.variant);if(!tierTris.TryGetValue(key,out var tt))tierTris[key]=tt=new int[3];tt[m.tier]=a.Triangles;}
                meshes++;
            }
            foreach(var kv in tierTris)Check(kv.Value[1]<=kv.Value[0]&&kv.Value[2]<=kv.Value[1],(kv.Key.Item1??"shared")+" "+kv.Key.Item2+" thins out by tier ("+string.Join("/",kv.Value)+")");
            int worstFull=0;string budget="";
            foreach(var spec in designed.Where(NativeChipModules.Designed))
            {
                var full=Loadouts(data,spec).Last();int n=Standard(spec,data,full,cm);var tri=new int[3];
                for(int t=0;t<3;t++)for(int i=0;i<n;i++)tri[t]+=Part(spec,cm[i].Part,cm[i].Variant,t).Triangles;
                Check(tri[1]<=tri[0]*.9f&&tri[2]<=tri[0]*.6f,spec+" full stack thins out ("+string.Join("/",tri)+")");Check(tri[0]<=9500&&tri[1]<=7000&&tri[2]<=4500,spec+" full stack within 9500 / 7000 / 4500 triangles a weapon (a dagger each hand) ("+string.Join("/",tri)+")");
                worstFull=Math.Max(worstFull,tri[0]);budget+=spec+" "+string.Join("/",tri)+"; ";
            }

            // 7. The reach stretch: a deforming blade strip follows the blade (or hilt) exactly; any other piece keeps its size
            // and rides to its stretched place; nothing changes at reach 1 or on a gun, staff or the shield.
            foreach(var spec in new[]{"sword","daggers","greatsword"})
            {
                var d=NativeWeaponModels.Get(spec);
                foreach(float reach in new[]{1.2f,1.6f,2.4f})foreach(float z in new[]{-.15f,.1f,.4f,.9f})foreach(bool deform in new[]{false,true})
                {
                    var p=new ChipModule{Position=new Vector3(0,0,z),Deform=deform};Check(NativeChipModules.Frame(spec,p,reach,out float sc,out float off),spec+" frame under reach");var kind=z>=d.StretchBase?WeaponStretch.Blade:WeaponStretch.Rigid;
                    if(deform){NativeWeaponModels.Stretch(spec,kind,reach,out float bs,out float bo);Check(Math.Abs(sc-bs)<1e-6f&&Math.Abs(off-bo)<1e-6f,spec+" a strip stretches with the blade");}
                    else Check(Math.Abs(sc*reach-1)<1e-5f&&Math.Abs((z*sc+off)*reach-NativeWeaponModels.Stretched(spec,kind,reach,z))<1e-5f,spec+" a part keeps its size and rides to its stretched place");
                }
                Check(!NativeChipModules.Frame(spec,new ChipModule{Position=new Vector3(0,0,.5f)},1,out _,out _),spec+" no correction at reach 1");
            }
            foreach(var spec in new[]{"pistol","rifle","machinegun","fire","leet","shield"})Check(!NativeChipModules.Frame(spec,new ChipModule{Position=new Vector3(0,0,.5f)},1.6f,out _,out _),spec+" never stretches its modules");

            // 8. No allocation per frame: modules, enchants, the animation, the frame and the cached ids, after one warm-up pass.
            var allocSpecs=designed.Where(NativeChipModules.Designed).ToArray();var allocWeapons=allocSpecs.Select(x=>data.weapons.First(w=>w.id==x)).ToArray();
            void Frame_()
            {
                for(int si=0;si<allocSpecs.Length;si++)
                {
                    string spec=allocSpecs[si];var w=allocWeapons[si];var loads=allocLoads[spec];
                    for(int li=0;li<loads.Length;li++)
                    {
                        var l=loads[li];int n=NativeChipModules.Modules(spec,w.@class=="ranged",(int)w.color,l.Chips,l.Stats,cm64);
                        for(int i=0;i<n;i++){var p=cm64[i];NativeChipModules.Animate(ref p,1.3f);NativeChipModules.Frame(spec,p,1.4f,out _,out _);NativeChipModules.ModelId(spec,p.Part,p.Variant,1,false);}
                        int f=NativeChipModules.Enchant(spec,true,true,l.Ember,l.Motes,cm64,0);for(int i=0;i<f;i++){var p=cm64[i];NativeChipModules.Animate(ref p,.7f);NativeChipModules.Frame(spec,p,1.4f,out _,out _);NativeChipModules.ModelId(spec,p.Part,p.Variant,2,true);}
                    }
                }
            }
            foreach(var spec in allocSpecs)allocLoads[spec]=Loadouts(data,spec).ToArray();
            Frame_();long before=GC.GetAllocatedBytesForCurrentThread();for(int r=0;r<50;r++)Frame_();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated==0,"the per-frame module route allocates nothing ("+allocated+" bytes over 50 frames)");

            // 9. Presentation only: no simulation, network or rules file reads the modules; the Runtime routes each skin.
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"*.cs"))if(!Path.GetFileName(f).StartsWith("NativeChipModules",StringComparison.Ordinal))Check(!File.ReadAllText(f).Contains("NativeChipModules"),Path.GetFileName(f)+" (Core) never reads the chip modules");
            var users=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Where(f=>File.ReadAllText(f).Contains("NativeChipModules")).Select(Path.GetFileName).OrderBy(x=>x).ToArray();
            Check(users.SequenceEqual(new[]{"NativeChipModuleModels.cs","NativeChipModulePresentation.cs"}),"only the module bank and presentation read the modules ("+string.Join(",",users)+")");
            string bank=Code("Runtime/NativeChipModuleModels.cs"),skins=Code("Runtime/NativeWeaponSkinModels.cs");
            Check(skins.Contains("ChipModules();")&&bank.Contains("foreach(var m in NativeChipModules.Meshes())")&&bank.Contains("var mesh=WeaponMeshOf(s,id);")&&bank.Contains("NoShadow=NativeChipModules.Light(m.part)"),"the bank builds every part mesh once, with the Standard weapons, converted like them");
            Check(route.Contains("int tier=ChipTier(rig,first);")&&route.Contains("return NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first);"),"modules take the held weapon's detail tier");
            Check(route.Contains("if(!first||aimMotion.Weight<=.5f)")&&!route.Contains("FirstPersonModuleScale"),"first person keeps the ADS hide and the modules' own size and place");
            Check(route.Contains("if(left){p.Position.X=-p.Position.X;p.Rotation=NativeChipModules.Mirrored(p.Rotation);}")&&route.Contains("left&&NativeChipModules.HasLeft(spec,p.Part)"),"the left dagger's modules mirror with its blade");
            Check(route.Contains("NativeChipModules.Frame(spec,p,z/x,out float scale,out float offset)")&&route.Contains("sourceModels.Queue(id,matrix,null,null,layer,camera,body:color);"),"modules ride the reach stretch and queue by cached id with their Chip colour (no closure)");
            Check(route.Contains("QueueChip(spec,chipModules[i],parent,hand==1,tier,time,first,layer,camera,rigFactor);")&&route.Contains("QueueChip(spec,chipModules[i],parent,hand==1,tier,time,first,layer,camera,1);"),"modules dim with the block cooldown, the enchant does not");
            Check(Code("Runtime/NativeReplay.cs").Contains("presentation.WeaponLoadout=replayArena;"),"the killcam replay draws its pilots' modules through the same route");

            Console.WriteLine($"  sockets worst {worstSocket:0.0000} m; pieces outside bounds {worstOut:0.0000} m, mounted gap {worstContact:0.0000} m, hover gap {worstHover:0.0000} m; first person nearest {worstSight:0.0} deg ({worstAt}), weapons themselves {weaponSight:0.0} deg");
            Console.WriteLine("  "+meshes+" part meshes; full-stack triangles Full/Reduced/Low: "+budget);
            Console.WriteLine("PASS chip modules: "+count+" checks ("+placed+" placed pieces)");
        }
        // ---- the first-person view rig (the render harness's approximation of the Unity view clips, in the Unity frame) ----
        private const float DT=1/60f;
        private static Vector3 Mx(Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);
        private static Quaternion Mx(Quaternion q)=>new Quaternion(q.X,-q.Y,-q.Z,q.W);
        private static Matrix4x4 TR(Vector3 p,Quaternion q)=>Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(p);
        private static Quaternion LookQ(Vector3 fwd)
        {
            var z=Vector3.Normalize(fwd);var x=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,z));var y=Vector3.Cross(z,x);
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1)));
        }
        private static Actor Pilot(string spec)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            return new Actor{Id=1,SourceSpec=spec,Weapon=w,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,Yaw=0,AccentColor="#70e1d0",VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine};
        }
        private static Matrix4x4[] ViewBones(string spec,Actor a,float t,Vector3 eye,Quaternion camQ)
        {
            bool gun=NativeWeaponDressing.Gun(spec),magic=NativeWeaponDressing.Staff(spec)||spec=="leet";var off=gun?NativeViewBase.Gun:magic?NativeFirstPersonPose.MagicViewOffset(0):NativeViewBase.Melee;
            var root=Matrix4x4.CreateScale(NativeFirstPersonPose.Scale)*Matrix4x4.CreateFromQuaternion(camQ)*Matrix4x4.CreateTranslation(eye+Vector3.Transform(off,camQ));var pose=new CapturedRig.Pose();var bones=new Matrix4x4[22];
            if(!gun&&!magic)
            {
                int set=Math.Clamp(a.VisualSet,0,3);var idle=CapturedClips.Sets[set].First(c=>c.StateName=="idle");var row=new float[CapturedClip.Channels];idle.Sample(CapturedClip.Wrap(t,idle.Duration),row);CapturedRig.Sample(row,set,pose);
                NativeFirstPersonPose.SettleTorso(pose);for(int b=0;b<22;b++)bones[b]=TR(Mx(pose.WorldPosition[b]),Mx(pose.WorldRotation[b]))*root;return bones;
            }
            var group=new Vector3(0,.865f,0);var jt=new NativeJuggleTrack();
            if(gun){var g=new GunMotion();g.Rig.GroupPosition=group;g.Rig.GroupYaw=0;for(int f=0;f<90;f++){g.Pose(a,f*DT,pose,false,jt,out _);g.Finish(pose,f*DT);}}
            else{var m=new MagicMotion();var ch=new float[13];m.Rig.GroupPosition=group;m.Rig.GroupYaw=0;for(int f=0;f<90;f++){m.Pose(a,f*DT,pose,ch,0,false,jt,out _);m.Finish(pose,f*DT);}}
            pose.World();for(int b=0;b<22;b++)bones[b]=TR(Mx(pose.WorldPosition[b]),Mx(pose.WorldRotation[b]))*root;return bones;
        }
        // The view weapon's frame (Runtime SourceWeaponBone in first person): the staff matrix for magic, the held melee hand, the gun hand.
        private static Matrix4x4 FirstFrame(string spec,Actor a,Matrix4x4[] bones,int hand,Quaternion camQ)
        {
            if(NativeWeaponDressing.Staff(spec)||spec=="leet")
            {
                Matrix4x4.Decompose(bones[CapturedRig.LHand],out var sc,out var rot,out var pos);var r=NativeFirstPersonPose.StaffRotation(Mx(rot),Mx(Vector3.Transform(Vector3.UnitZ,camQ)),0);
                return Matrix4x4.CreateScale(sc*NativeFirstPersonPose.StaffSize(0))*Matrix4x4.CreateFromQuaternion(Mx(r))*Matrix4x4.CreateTranslation(pos);
            }
            int bone=hand==1?CapturedRig.LHand:CapturedRig.RHand;
            if(NativeWeaponDressing.Gun(spec))return bones[bone];
            return Matrix4x4.CreateFromQuaternion(Mx(NativeSpecialPose.HeldRotation(a,1.48f,hand==1)))*bones[bone];
        }
        private static void Piece(StringBuilder sb,in WeaponPiece p)
        {
            static string F(float v)=>Math.Round(v*1e5).ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.Append((int)p.Shape).Append(p.Glow?'g':'l').Append(p.Color).Append(':').Append(F(p.Position.X)).Append(',').Append(F(p.Position.Y)).Append(',').Append(F(p.Position.Z)).Append('/').Append(F(p.Scale.X)).Append(',').Append(F(p.Scale.Y)).Append(',').Append(F(p.Scale.Z))
                .Append('/').Append(F(p.Rotation.X)).Append(',').Append(F(p.Rotation.Y)).Append(',').Append(F(p.Rotation.Z)).Append(',').Append(F(p.Rotation.W)).Append(';');
        }
        private static string Hash(string s){using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Take(8).Select(b=>b.ToString("x2")));}
        // The model's triangles on its joint, with their bounds for a quick reject.
        private sealed class Tri{public Vector3 A,B,C,Min,Max;}
        private static List<Tri> Triangles(WeaponMesh m,int bone)
        {
            var list=new List<Tri>();foreach(var s in m.Sections){if(s.Bone!=bone)continue;for(int i=0;i+2<s.Indices.Count;i+=3){var a=s.Positions[s.Indices[i]];var b=s.Positions[s.Indices[i+1]];var c=s.Positions[s.Indices[i+2]];list.Add(new Tri{A=a,B=b,C=c,Min=Vector3.Min(a,Vector3.Min(b,c)),Max=Vector3.Max(a,Vector3.Max(b,c))});}}return list;
        }
        private static float Distance(List<Tri> tris,Vector3 p,float best=float.MaxValue)
        {
            foreach(var t in tris)
            {
                if(p.X<t.Min.X-best||p.X>t.Max.X+best||p.Y<t.Min.Y-best||p.Y>t.Max.Y+best||p.Z<t.Min.Z-best||p.Z>t.Max.Z+best)continue;
                float d=Vector3.Distance(p,Closest(p,t.A,t.B,t.C));if(d<best)best=d;
            }
            return best;
        }
        // Ericson's closest point on a triangle.
        private static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
        }
    }
}
#endif
