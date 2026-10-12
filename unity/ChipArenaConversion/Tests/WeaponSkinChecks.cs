#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Weapon skins (owner 2026-10-12: new higher quality weapon models; the current models become the Prototype Series).
    // Checks the Standard models (Core/NativeWeaponModels*.cs) against the sockets every consumer reads (NativeWeaponDressing,
    // MeleeCapture, MeleeRig, NativeLobbyPoses), the reach stretch, determinism and budgets, the skin map rules
    // (Core/NativeWeaponSkins.cs), that the Prototype Series is the untouched source bake, chip modules and enchants, and the
    // Runtime / Locker wiring (source text: Unity cannot run here).
    internal static class WeaponSkinChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Weapon skins: "+what);count++;}
        private static bool Nan(Vector3 v)=>float.IsNaN(v.X);
        // The source bake's hash at base f026b9e: the Prototype Series must stay byte-identical.
        private const string SourceModelsSha256="b256a94fb2c97c7542c68613b676e513433f099816be2cbf58a80c1d254b759c";
        public static void Run(string root,ConversionContent data)
        {
            count=0;
            // 1. Both skins for every weapon with a model: the Prototype bake ("weapon-"/"loose-" + spec, unchanged) and a Standard
            // design at three tiers plus its loose copy.
            var specs=data.weapons.Select(w=>w.id).Where(id=>id!="siren").ToArray();
            Check(specs.OrderBy(s=>s).SequenceEqual(NativeWeaponSkins.Weapons.OrderBy(s=>s)),"the skinned weapons are every weapon but the Siren");
            var designed=NativeWeaponSkins.Weapons.Where(NativeWeaponModels.Designed).ToArray();
            Check(NativeWeaponModels.Specs.All(NativeWeaponSkins.Skinned)&&NativeWeaponModels.Specs.Count()==designed.Length,"every Standard design is a skinned weapon");
            Check(!NativeWeaponSkins.Skinned("siren")&&!NativeWeaponModels.Designed("siren"),"the Siren has no weapon skin");
            var file=Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json");
            using(var sha=SHA256.Create())Check(string.Concat(sha.ComputeHash(File.ReadAllBytes(file)).Select(b=>b.ToString("x2")))==SourceModelsSha256,"SourceModels.json (the Prototype Series bake) is byte-identical to the base");
            var bank=new HashSet<string>();
            using(var envelope=JsonDocument.Parse(File.ReadAllText(file)))
            using(var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())))using(var zip=new GZipStream(bytes,CompressionMode.Decompress))using(var reader=new StreamReader(zip))
            using(var doc=JsonDocument.Parse(reader.ReadToEnd()))foreach(var m in doc.RootElement.GetProperty("models").EnumerateArray())bank.Add(m.GetProperty("id").GetString());
            foreach(var spec in NativeWeaponSkins.Weapons)
            {
                Check(bank.Contains("weapon-"+spec)&&bank.Contains("loose-"+spec),spec+" Prototype Series models in the bake");
                Check(NativeWeaponSkins.Model("weapon-"+spec,NativeWeaponSkins.PrototypeIndex)=="weapon-"+spec&&NativeWeaponSkins.Model("loose-"+spec,NativeWeaponSkins.PrototypeIndex)=="loose-"+spec,spec+" Prototype ids are the bake's own");
                if(!NativeWeaponModels.Designed(spec)){Check(NativeWeaponSkins.Model("weapon-"+spec,NativeWeaponSkins.StandardIndex)=="weapon-"+spec,spec+" without a design draws the Prototype");continue;}
                for(int t=0;t<NativeWeaponModels.Tiers;t++)Check(NativeWeaponSkins.Model("weapon-"+spec,NativeWeaponSkins.StandardIndex,t)==NativeWeaponSkins.StandardModel(spec,t)&&!bank.Contains(NativeWeaponSkins.StandardModel(spec,t)),spec+" Standard tier "+t+" id");
                Check(NativeWeaponSkins.Model("loose-"+spec,NativeWeaponSkins.StandardIndex)=="loose-std-"+spec,spec+" Standard loose id");
            }
            Check(NativeWeaponSkins.Model("weapon-siren",NativeWeaponSkins.StandardIndex)=="weapon-siren"&&NativeWeaponSkins.Model("hat-cap",NativeWeaponSkins.StandardIndex)=="hat-cap"&&NativeWeaponSkins.Model(null,0)==null,"ids without a design pass through");

            // 2. Meshes: valid, deterministic, within budget, thinning tier by tier.
            var digest=new StringBuilder();
            foreach(var spec in designed)
            {
                var d=NativeWeaponModels.Get(spec);int[] tris=new int[3];
                for(int t=0;t<3;t++)
                {
                    var a=NativeWeaponModels.Build(spec,t);var b=NativeWeaponModels.Build(spec,t);tris[t]=a.Triangles;
                    Check(a.Triangles>0&&a.Triangles<=NativeWeaponModels.Budget[t],spec+" tier "+t+" within budget ("+a.Triangles+" / "+NativeWeaponModels.Budget[t]+")");
                    Check(Same(a,b),spec+" tier "+t+" builds deterministically");
                    foreach(var s in a.Sections)
                    {
                        Check(s.Vertices<65535&&s.Indices.All(i=>i>=0&&i<s.Vertices)&&s.Colors.Count==s.Vertices&&s.Emission.Count==s.Vertices&&s.Normals.Count==s.Vertices,spec+" tier "+t+" section arrays");
                        Check(s.Positions.All(p=>MathUtil.Finite(p.X)&&MathUtil.Finite(p.Y)&&MathUtil.Finite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),spec+" tier "+t+" finite positions and unit normals");
                        for(int i=0;i+2<s.Indices.Count;i+=3){var p0=s.Positions[s.Indices[i]];var area=Vector3.Cross(s.Positions[s.Indices[i+1]]-p0,s.Positions[s.Indices[i+2]]-p0).Length();if(!(area>1e-12f))Check(false,spec+" tier "+t+" degenerate triangle "+i/3);}
                        Check(s.Emission.All(e=>e.W==0||e.W==2),spec+" tier "+t+" emission tags are untinted (0) or accent (2)");
                        Check(s.Bone==(d.Dual&&s.Bone==NativeWeaponModels.LeftHand?NativeWeaponModels.LeftHand:d.Bone),spec+" tier "+t+" section joint");
                    }
                    Check(t==0||a.Sections.Count>0,spec+" tier "+t+" sections");
                    if(d.Dual)Check(a.Sections.Count(s=>s.Bone==NativeWeaponModels.LeftHand)==a.Sections.Count(s=>s.Bone==NativeWeaponModels.RightHand),spec+" tier "+t+" both hands");
                    if(t==0)digest.Append(spec).Append(':').Append(Digest(a)).Append(';');
                }
                Check(tris[1]<=tris[0]*.85f&&tris[2]<=tris[0]*.45f&&tris[2]<tris[1],spec+" tiers thin out ("+string.Join("/",tris)+")");
                var loose=NativeWeaponModels.Loose(spec);Check(loose.Sections.Count==1&&loose.Sections[0].Bone==-1&&loose.Triangles==(d.Dual?tris[0]/2:tris[0]),spec+" loose copy is one weapon on the model root");
                Check(tris[0]>=(spec=="leet"?1.5f:3f)*ProtoTriangles(spec),spec+" Standard is far more detailed than the Prototype ("+tris[0]+" vs "+ProtoTriangles(spec)+")");
            }

            // 3. Sockets: every model is built around the shared sockets (source weapon frame) and has geometry at each one.
            foreach(var spec in designed)
            {
                var d=NativeWeaponModels.Get(spec);var m=NativeWeaponModels.Build(spec,0);var all=m.Sections.Where(s=>s.Bone==d.Bone).SelectMany(s=>s.Positions).ToArray();
                float Near(Vector3 at)=>all.Min(p=>Vector3.Distance(p,at));
                bool gun=NativeWeaponDressing.Gun(spec),staff=NativeWeaponDressing.Staff(spec)||spec=="leet",fist=NativeWeaponDressing.Fist(spec);
                if(gun)
                {
                    float socket=NativeWeaponDressing.MuzzleZ(spec);Check(!Nan(d.Muzzle)&&d.Muzzle.X==0&&d.Muzzle.Y==0,spec+" bore on y = 0");
                    Check(socket-d.Muzzle.Z>=0&&socket-d.Muzzle.Z<=.03f,spec+" muzzle face just behind the muzzle socket ("+d.Muzzle.Z+" / "+socket+")");
                    Check(Near(d.Muzzle)<.004f,spec+" geometry at the muzzle face");
                    Check(!all.Any(p=>p.Z>socket+.002f&&new Vector2(p.X,p.Y).Length()<.06f),spec+" nothing in front of the muzzle socket");
                    var support=spec=="pistol"?new Vector3(.06f,-.10f,-.01f):NativeLobbyPoses.GunSupportGrip;Check(d.Support==support&&Near(support)<.07f,spec+" support grip "+support+" by the model");
                    Check(all.Any(p=>p.Y<-.08f&&p.Y>-.26f&&Math.Abs(p.Z)<.09f)&&Near(new Vector3(0,-.12f,-.04f))<.075f,spec+" pistol grip under the hand");
                    Check(all.Min(p=>p.Z)>=-.32f,spec+" stock butt at z -.30");
                    Check(m.Sections.All(s=>s.Stretch==WeaponStretch.None),spec+" never stretches");
                }
                else if(staff)
                {
                    float socket=NativeWeaponDressing.MuzzleZ(spec);Check(d.Muzzle==new Vector3(0,0,socket),spec+" muzzle socket at "+socket);
                    Check(!Nan(d.StaffTip)&&Near(d.StaffTip)<.075f&&d.StaffTip.Z<=socket&&socket-d.StaffTip.Z<.2f,spec+" energy core under the staff tip");
                    Check(all.Max(p=>p.Z)<=socket+.06f&&all.Min(p=>p.Z)>=-.5f,spec+" head and heel within the Prototype's length");
                    Check(Near(Vector3.Zero)<.05f&&(spec=="leet"||Near(new Vector3(0,0,.40f))<.05f),spec+" main grip at the hand, the guard-grip at .40");
                    Check(d.Bone==NativeWeaponModels.LeftHand,spec+" rides the left hand");
                }
                else if(fist)
                {
                    Check(NativeWeaponDressing.BladeTip(spec,out var tip)&&Vector3.Distance(tip,d.BladeTip)<1e-5f,spec+" punch socket");
                    var mn=new Vector3(float.MaxValue);var mx=new Vector3(float.MinValue);foreach(var p in all){mn=Vector3.Min(mn,p);mx=Vector3.Max(mx,p);}
                    Check(mn.X<-.09f&&mx.X>.09f&&mn.Y<-.08f&&mx.Y>.06f&&mn.Z<-.09f&&mx.Z>.09f,spec+" gauntlet encloses the fist like the Prototype box");
                    Check(tip.Z<=mx.Z+.02f&&tip.Z>=mx.Z-.06f&&tip.Y>mn.Y&&tip.Y<mx.Y&&Math.Abs(tip.X)<mx.X,spec+" punching face at the socket");
                }
                else
                {
                    Check(NativeWeaponDressing.BladeTip(spec,out var tip)&&Vector3.Distance(tip,d.BladeTip)<.013f,spec+" blade tip by the socket ("+d.BladeTip+" / "+tip+")");
                    Check(Near(d.BladeTip)<.006f,spec+" geometry at the blade tip");
                    if(spec!="shield")
                    {
                        var set=MeleeSet.For(spec);var blade=m.Sections.Where(s=>s.Bone==d.Bone&&s.Stretch==WeaponStretch.Blade).SelectMany(s=>s.Positions).ToArray();var hilt=m.Sections.Where(s=>s.Bone==d.Bone&&s.Stretch==WeaponStretch.Rigid).SelectMany(s=>s.Positions).ToArray();
                        Check(blade.Length>0&&hilt.Length>0&&m.Sections.All(s=>s.Stretch!=WeaponStretch.None),spec+" hilt and blade sections");
                        Check(Math.Abs(blade.Max(p=>p.Z)-d.BladeTip.Z)<1e-4f&&blade.Min(p=>p.Z)>=d.StretchBase-.03f&&Math.Abs(d.StretchTip-tip.Z)<1e-5f,spec+" the blade runs from its base to the tip socket");
                        Check(hilt.Min(p=>p.Z)>=-set.Fit.Pommel-.012f,spec+" pommel within the clearance fit ("+hilt.Min(p=>p.Z)+" / "+(-set.Fit.Pommel)+")");
                        Check(blade.Where(p=>p.Z>d.StretchBase+.3f).All(p=>Math.Abs(p.X)<=set.Fit.HalfWidth+.012f),spec+" blade within the clearance half width");
                        Check(hilt.Any(p=>Math.Abs(p.Z)<.02f&&new Vector2(p.X,p.Y).Length()<.05f),spec+" grip round the hand");
                        if(spec=="greatsword")Check(d.Support==MeleeRig.SupportGrip&&hilt.Any(p=>Math.Abs(p.Z-MeleeRig.SupportGrip.Z)<.02f&&new Vector2(p.X,p.Y).Length()<.05f),spec+" lower grip at the support hand");
                        Check(BladeLengthCovered(blade,d,spec),spec+" blade covers the enchant track (BladeLength)");
                    }
                    else
                    {
                        Check(m.Sections.All(s=>s.Stretch==WeaponStretch.None),"shield keeps the Prototype's uniform reach stretch");
                        var mn=new Vector3(float.MaxValue);var mx=new Vector3(float.MinValue);foreach(var p in all){mn=Vector3.Min(mn,p);mx=Vector3.Max(mx,p);}
                        Check(mn.X<=-.29f&&mx.X>=.29f&&mn.Y<=-.33f&&mx.Y>=.41f&&mn.Z>=.12f&&mx.Z<=.34f,"shield face spans the Prototype's ("+mn+" / "+mx+")");
                    }
                }
            }

            // 4. The reach stretch: the hilt keeps its length, the blade's base stays at the guard, the tip lands on the stretched
            // socket; and the Runtime's matrix read-back (z / x scale) recovers the reach.
            foreach(var spec in new[]{"sword","daggers","greatsword"}.Where(NativeWeaponModels.Designed))
            {
                var d=NativeWeaponModels.Get(spec);var m=NativeWeaponModels.Build(spec,0);var hilt=m.Sections.Where(s=>s.Stretch==WeaponStretch.Rigid).SelectMany(s=>s.Positions).ToArray();
                float lo=hilt.Min(p=>p.Z),hi=hilt.Max(p=>p.Z);
                foreach(float reach in new[]{1f,1.2f,1.6f,2.4f})
                {
                    Check(Math.Abs(NativeWeaponModels.Stretched(spec,WeaponStretch.Rigid,reach,hi)-NativeWeaponModels.Stretched(spec,WeaponStretch.Rigid,reach,lo)-(hi-lo))<1e-4f,spec+" reach "+reach+" keeps the grip length");
                    Check(Math.Abs(NativeWeaponModels.Stretched(spec,WeaponStretch.Blade,reach,d.StretchTip)-d.StretchTip*reach)<1e-4f,spec+" reach "+reach+" tip on the stretched blade-tip socket");
                    Check(Math.Abs(NativeWeaponModels.Stretched(spec,WeaponStretch.Blade,reach,d.StretchBase)-d.StretchBase)<1e-4f,spec+" reach "+reach+" blade base stays at the guard");
                    Check(Math.Abs(NativeWeaponModels.Stretched(spec,WeaponStretch.None,reach,1)-reach)<1e-5f,spec+" reach "+reach+" unstretched parts follow the matrix");
                    // The Runtime: a rotated, uniformly scaled hand matrix with the Prototype's z scale; scale back from the ratio.
                    var hand=Matrix4x4.CreateScale(1,1,reach)*Matrix4x4.CreateScale(.78f)*Matrix4x4.CreateFromYawPitchRoll(.7f,-.4f,1.1f)*Matrix4x4.CreateTranslation(3,1,-2);
                    float ratio=new Vector3(hand.M31,hand.M32,hand.M33).Length()/new Vector3(hand.M11,hand.M12,hand.M13).Length();Check(Math.Abs(ratio-reach)<1e-4f,spec+" reach "+reach+" read back from the weapon matrix");
                    if(NativeWeaponModels.Stretch(spec,WeaponStretch.Blade,ratio,out float sc,out float off))
                    {
                        var tipWorld=Vector3.Transform(new Vector3(0,0,d.StretchTip*sc+off),hand);var socketWorld=Vector3.Transform(new Vector3(0,0,d.StretchTip),hand);
                        Check(Vector3.Distance(tipWorld,socketWorld)<1e-4f,spec+" reach "+reach+" drawn tip on the trails' blade-tip sample");
                    }
                }
                Check(!NativeWeaponModels.Stretch(spec,WeaponStretch.Blade,1,out _,out _)&&!NativeWeaponModels.Stretch(spec,WeaponStretch.Rigid,float.NaN,out _,out _),spec+" no correction at reach 1 or invalid reach");
            }

            // 5. Chip modules and blade enchants attach to the Standard models (they are placed from the shared sockets).
            var pieces=new WeaponPiece[NativeWeaponDressing.MaxPieces];
            var chips=new Dictionary<string,int>{{"ricochet",2},{"velocity",2},{"magazine",3},{"fire-rate",2},{"demolition",1},{"overclock",1}};
            foreach(var spec in designed)
            {
                var w=data.weapons.First(x=>x.id==spec);var stats=NativeContentRules.Stats(data,spec,chips);stats["extraProjectiles"]=4;stats["homing"]=1;stats["split"]=1;stats["pierces"]=1;stats["pong"]=1;stats["boomerang"]=1;stats["vacuum"]=1;stats["impulse"]=2;stats["shrapnel"]=1;stats["staticEvery"]=3;if(NativeWeaponDressing.Melee(spec)){stats["reach"]=1.4;stats["extraStrikes"]=1;}
                var m=NativeWeaponModels.Build(spec,0);m.Bounds(NativeWeaponModels.Get(spec).Bone,out var mn,out var mx);
                int n=NativeWeaponDressing.Modules(spec,w.@class=="ranged",(int)w.color,chips,stats,pieces);
                if(NativeWeaponDressing.Fist(spec)){Check(n==0,spec+" fists carry no modules");continue;}
                Check(n>0,spec+" modules built");
                for(int i=0;i<n;i++){var p=pieces[i].Position;Check(p.X>=mn.X-.45f&&p.X<=mx.X+.45f&&p.Y>=mn.Y-.35f&&p.Y<=mx.Y+.35f&&p.Z>=mn.Z-.1f&&p.Z<=Math.Max(mx.Z,NativeWeaponDressing.MuzzleZ(spec))+.15f,spec+" module "+i+" sits on the model or its muzzle socket ("+p+")");}
                if(!NativeWeaponDressing.Melee(spec))continue;
                int f=NativeWeaponDressing.Enchant(spec,true,true,3,2,pieces,0);Check(f>0,spec+" enchant built");
                for(int i=0;i<f;i++){var p=pieces[i].Position;Check(p.Z>=mn.Z&&p.Z<=mx.Z+.05f&&Math.Abs(p.X)<=Math.Max(Math.Abs(mn.X),mx.X)+.12f,spec+" enchant "+i+" along the blade ("+p+")");}
            }

            // 6. The skin map: normalized, compact, allocation-free reads, every malformed input reads as Standard.
            Check(NativeWeaponSkins.Normalize(null)==""&&NativeWeaponSkins.Normalize("")==""&&NativeWeaponSkins.Normalize(",,:,")=="","empty maps");
            Check(NativeWeaponSkins.Normalize("sword:prototype,pistol:prototype")=="pistol:prototype,sword:prototype","canonical weapon order");
            Check(NativeWeaponSkins.Normalize(" sword : prototype ,sword:standard,rifle:prototype,nope:prototype,shield:gold,daggers")=="rifle:prototype","trims, last pick wins, unknown weapons and skins drop");
            Check(NativeWeaponSkins.Normalize("siren:prototype")=="","the Siren carries no skin");
            var everything=string.Join(",",NativeWeaponSkins.Weapons.Select(s=>s+":prototype"));var full=NativeWeaponSkins.Normalize(everything);
            Check(full==everything&&full.Length<=NativeWeaponSkins.MaxLength,"the longest map fits the frame limit ("+full.Length+")");
            Check(NativeWeaponSkins.Normalize(new string(',',6000)+"sword:prototype")=="","an oversized input is cut, never parsed in full");
            foreach(var spec in NativeWeaponSkins.Weapons)
            {
                var one=NativeWeaponSkins.With("",spec,NativeWeaponSkins.Prototype);
                Check(one==spec+":prototype"&&NativeWeaponSkins.Index(one,spec)==NativeWeaponSkins.PrototypeIndex&&NativeWeaponSkins.Of(one,spec)==NativeWeaponSkins.Prototype,spec+" pick round-trips");
                Check(NativeWeaponSkins.Normalize(one)==one&&NativeWeaponSkins.With(one,spec,NativeWeaponSkins.Standard)=="",spec+" normalized and cleared");
                foreach(var other in NativeWeaponSkins.Weapons)if(other!=spec)Check(NativeWeaponSkins.Index(one,other)==NativeWeaponSkins.StandardIndex,spec+" pick leaves "+other+" Standard");
                if(!NativeWeaponModels.Designed(spec))continue;
                Check(NativeWeaponSkins.Held(spec,one,1)=="weapon-"+spec&&NativeWeaponSkins.Held(spec,"",1)==NativeWeaponSkins.StandardModel(spec,1),spec+" held model by map");
                Check(NativeWeaponSkins.ModelIn("weapon-"+spec,one)=="weapon-"+spec&&NativeWeaponSkins.ModelIn("loose-"+spec,"")=="loose-std-"+spec,spec+" model by the map's pick");
            }
            Check(NativeWeaponSkins.Index("swordx:prototype,sword:prototypes",  "sword")==NativeWeaponSkins.StandardIndex&&NativeWeaponSkins.Index("xsword:prototype","sword")==NativeWeaponSkins.StandardIndex,"prefix lookalikes read as Standard");
            Check(NativeWeaponSkins.With(full,"rifle",NativeWeaponSkins.Standard)==string.Join(",",NativeWeaponSkins.Weapons.Where(s=>s!="rifle").Select(s=>s+":prototype")),"clearing one pick keeps the rest");
            Check(NativeWeaponSkins.Labels.SequenceEqual(new[]{"Standard","Prototype Series"})&&NativeWeaponSkins.Keys[NativeWeaponSkins.StandardIndex]==NativeWeaponSkins.Standard,"skin labels");
            Check(NativeWeaponSkins.Tier(false,1,5,false)==0&&NativeWeaponSkins.Tier(false,.3f,5,false)==1&&NativeWeaponSkins.Tier(true,1,5,false)==2&&NativeWeaponSkins.Tier(false,1,20,false)==1&&NativeWeaponSkins.Tier(false,1,40,false)==2
                &&NativeWeaponSkins.Tier(true,.3f,40,true)==1&&NativeWeaponSkins.Tier(false,.3f,40,true)==0,"detail tiers: graphics, Reduced effects, distance, first person");

            // 7. Wiring (source text; Unity cannot run here).
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var models=R("NativeSourceModels.cs");var skins=R("NativeWeaponSkinModels.cs");var presentation=R("NativeSourcePresentation.cs");var frame=R("NativeNetworkFrame.cs");var network=R("NativeNetwork.cs");
            Check(models.Contains("HordeMaterials();WeaponSkins();")&&models.Contains("d.Matrices.Add(weaponStretch.Count>0?WeaponStretched(part.Mesh,matrix):matrix);"),"the bank builds the Standard models and stretches their sections per draw");
            Check(skins.Contains("vertices[i]=new Vector3(-p.X,p.Y,p.Z);normals[i]=new Vector3(-n.X,n.Y,n.Z);")&&skins.Contains("triangles[i]=s.Indices[i];triangles[i+1]=s.Indices[i+2];triangles[i+2]=s.Indices[i+1];"),"Standard meshes convert like the source export (x mirrored, winding turned)");
            Check(skins.Contains("NativeArmorLight.HeldPlan(armorPilot,CapturedRig.RHand)")&&skins.Contains("NativeArmorLight.HandHeld(NativeWeaponSkins.PrototypeHeld(spec))"),"hand-held Standard weapons receive the cuff spill");
            Check(presentation.Contains("string held=HeldWeaponModel(rig,a,first);sourceModels.Queue(held,")&&presentation.Contains("hitFlash:StandardModel(held)?flash:1,partTint:tint,glow:HeldGlow);"),"held weapons draw the pilot's skin and Standard ones take the hit flash");
            foreach(var f in new[]{"NativeSourcePresentation.cs","NativeMeleeKitPresentation.cs","NativeWeaponFxPresentation.cs","NativeSceneMotion.cs"})Check(!R(f).Contains("Queue(\"weapon-\"+")&&!R(f).Contains("Ghost(\"weapon-\"+a.SourceSpec)")&&!R(f).Contains("\"weapon-\"+a.SourceSpec;if(!afterimage"),f+" draws weapons through the skin");
            Check(R("NativeKillEffectPresentation.cs").Contains("NativeWeaponSkins.Model(\"loose-\"+Death.actor.SourceSpec,")&&R("NativeIntros.cs").Contains("IntroModel(NativeIntroRules.IntroWeapon(style))")&&R("NativeAbilityPresentation.cs").Contains("if(!ThrownWeapon(shot,matrix))"),"drops, intros and thrown weapons use the skin");
            Check(frame.Contains("lobbyPose,lobbyBack;public string weaponSkins;")&&frame.Contains("s.weaponSkins!=null&&s.weaponSkins.Length>NativeWeaponSkins.MaxLength")&&frame.Contains("weaponSkins=s.weaponSkins,")&&frame.Contains("seat.weaponSkins=NativeWeaponSkins.Normalize(sf.weaponSkins);"),"seat frames carry, bound and normalize the map");
            Check(network.Contains("weaponSkins=NativeWeaponSkins.Normalize(prefs.weaponSkins)")&&network.Contains("weaponSkins=NativeWeaponSkins.Normalize(c.weaponSkins)")&&R("NativeLobbyLogic.cs").Contains("weaponSkins=s.weaponSkins,"),"profile, seat and lobby frame carry the map");
            Check(R("NativeAccount.cs").Contains("{public string weaponSkins;public bool overrideWeaponIntro;")&&R("ConversionGame.cs").Contains("public string weaponSkins=\"\";")&&R("NativeProfileUI.cs").Contains("if(c.weaponSkins!=null)prefs.weaponSkins=NativeWeaponSkins.Normalize(c.weaponSkins);"),"prefs and account cosmetics hold the map; an account without the field keeps the local pick");
            var locker=R("NativeLockerSkinsUI.cs");var lockerUi=R("NativeLockerUI.cs");
            Check(locker.Contains("prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,w.id,NativeWeaponSkins.Keys[k]);")&&lockerUi.Contains("default:LkSkinsLayout();break;")&&lockerUi.Contains("Skin=LockerSkinShown(w.id)"),"the Locker's skin selector equips, and its stage tries on, a skin");
            Check(lockerUi.Contains("b.Skins!=prefs.weaponSkins")&&lockerUi.Contains("prefs.weaponSkins=b.Skins;"),"the Locker baseline covers the skins");
            Console.WriteLine("PASS weapon skins: "+count+" checks ("+designed.Length+" of "+NativeWeaponSkins.Weapons.Length+" weapons with both skins, sockets, reach stretch, tiers/budgets/determinism, modules/enchants, skin map, wiring) digest "+Hash(digest.ToString()));
        }
        private static int ProtoTriangles(string spec)=>spec=="pistol"?180:spec=="rifle"?264:spec=="machinegun"?228:spec=="sword"?264:spec=="daggers"?456:spec=="greatsword"?300:spec=="shield"?168:spec=="emperor"?144:spec=="ansatsuken"?72:spec=="leet"?1388:180;
        private static bool BladeLengthCovered(Vector3[] blade,NativeWeaponModels.Design d,string spec)
        {
            // The enchant track: .28 .. .28 + BladeLength (weapon-visuals.ts:126) inside the blade's own span.
            float lo=blade.Min(p=>p.Z),hi=blade.Max(p=>p.Z);return lo<=.29f&&hi>=.28f+NativeWeaponDressing.BladeLength(spec)-.01f;
        }
        private static bool Same(WeaponMesh a,WeaponMesh b)
        {
            if(a.Sections.Count!=b.Sections.Count)return false;
            for(int i=0;i<a.Sections.Count;i++){var x=a.Sections[i];var y=b.Sections[i];if(x.Bone!=y.Bone||x.Stretch!=y.Stretch||!x.Positions.SequenceEqual(y.Positions)||!x.Normals.SequenceEqual(y.Normals)||!x.Colors.SequenceEqual(y.Colors)||!x.Emission.SequenceEqual(y.Emission)||!x.Indices.SequenceEqual(y.Indices))return false;}
            return true;
        }
        private static string Digest(WeaponMesh m)
        {
            var sb=new StringBuilder();foreach(var s in m.Sections){sb.Append(s.Bone).Append(s.Stretch).Append(s.Vertices).Append('/').Append(s.Triangles);foreach(var p in s.Positions)sb.Append(Math.Round(p.X*1e4)).Append(Math.Round(p.Y*1e4)).Append(Math.Round(p.Z*1e4));}
            return Hash(sb.ToString());
        }
        private static string Hash(string s){using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Take(6).Select(b=>b.ToString("x2")));}
    }
}
#endif
