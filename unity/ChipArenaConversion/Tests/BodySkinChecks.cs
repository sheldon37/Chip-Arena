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
    // Pilot body skins (owner 2026-10-12: "Sharper armored voxel" Standard body, the current body kept as "Prototype"; the DEV
    // Sheldon Clothes). Checks the Core bodies (NativeBodyModels) against the exported pilot (SourceModels.json "pilot", the
    // Prototype, byte-identical): the same parts on the same joints, inside the old volumes plus a little relief, the hood's and
    // hats' clearances, budgets, tiers and determinism; the skin map's body entry and its DEV gate; that hit geometry and the
    // rig never read a body (source scans and identical results); the Sheldon Clothes' cloth clipping over animation; the
    // armour-light tables and gibs; and the Runtime wiring of every system that draws a pilot (source text: Unity cannot run here).
    internal static class BodySkinChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Body skins: "+what);count++;}
        // The source bake's hash (as WeaponSkinChecks pins it): the Prototype body must stay byte-identical.
        private const string SourceModelsSha256="b256a94fb2c97c7542c68613b676e513433f099816be2cbf58a80c1d254b759c";
        private sealed class Proto{public string Part;public int Bone;public Vector3 Min,Max;public bool Emitter;public readonly List<(Vector3 min,Vector3 max)> Boxes=new List<(Vector3,Vector3)>();}
        public static void Run(string root,ConversionContent data)
        {
            count=0;var report=new List<string>();
            // ---- 1. The Prototype: the exported pilot, untouched, and the part table every skin builds ----
            var file=Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json");
            using(var sha=SHA256.Create())Check(string.Concat(sha.ComputeHash(File.ReadAllBytes(file)).Select(b=>b.ToString("x2")))==SourceModelsSha256,"SourceModels.json (the Prototype body) is byte-identical to the base");
            var proto=new List<Proto>();
            using(var envelope=JsonDocument.Parse(File.ReadAllText(file)))
            using(var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())))using(var zip=new GZipStream(bytes,CompressionMode.Decompress))using(var reader=new StreamReader(zip))
            using(var bank=JsonDocument.Parse(reader.ReadToEnd()))
            {
                var pilot=bank.RootElement.GetProperty("models").EnumerateArray().First(m=>m.GetProperty("id").GetString()=="pilot");
                foreach(var s in pilot.GetProperty("sections").EnumerateArray())
                {
                    var pos=s.GetProperty("positions").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();if(pos.Length==0)continue;var em=s.GetProperty("emission").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();
                    var p=new Proto{Part=s.GetProperty("part").GetString(),Bone=s.GetProperty("bone").GetInt32(),Min=new Vector3(float.MaxValue),Max=new Vector3(float.MinValue),Emitter=em[3]>1.5f&&em[0]+em[1]+em[2]>0};
                    for(int i=0;i<pos.Length;i+=3){var v=new Vector3(pos[i],pos[i+1],pos[i+2]);p.Min=Vector3.Min(p.Min,v);p.Max=Vector3.Max(p.Max,v);}
                    for(int b=0;b+72<=pos.Length;b+=72){var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);for(int i=b;i<b+72;i+=3){var v=new Vector3(pos[i],pos[i+1],pos[i+2]);lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);}p.Boxes.Add((lo,hi));}
                    proto.Add(p);
                }
            }
            Check(proto.Count==NativeBodySkins.Parts.Length,"the part table has every exported part ("+proto.Count+")");
            for(int i=0;i<proto.Count;i++)Check(proto[i].Part==NativeBodySkins.Parts[i].Name&&proto[i].Bone==NativeBodySkins.Parts[i].Bone&&proto[i].Emitter==NativeBodySkins.Parts[i].Emitter,"part "+i+" "+proto[i].Part+" on joint "+proto[i].Bone+" (emitter "+proto[i].Emitter+")");
            Check(NativeBodySkins.Model(NativeBodySkins.PrototypeIndex,0)=="pilot"&&NativeBodySkins.Model(NativeBodySkins.PrototypeIndex,2)=="pilot","the Prototype body is the bank's own \"pilot\" at every tier");

            // ---- 2. The skin map's body entry (Core/NativeWeaponSkins.cs) ----
            Check(NativeWeaponSkins.Entries.Length==NativeWeaponSkins.Weapons.Length+2&&NativeWeaponSkins.Entries[NativeWeaponSkins.Weapons.Length]==NativeBodySkins.Entry&&NativeWeaponSkins.Entries[^1]==NativeWeaponSkins.FaceEntry,"the map carries the body after the weapons, then the model's face gear");
            // The face gear (owner 2026-10-26): both by default (never written), the visor or the mask; anything else dropped.
            Check(NativeWeaponSkins.FaceKeys.SequenceEqual(new[]{"mask-visor","visor","mask"})&&NativeWeaponSkins.Normalize("face:visor")=="face:visor"&&NativeWeaponSkins.Normalize("face:mask")=="face:mask"&&NativeWeaponSkins.Normalize("face:mask-visor")==""&&NativeWeaponSkins.Normalize("face:prototype")==""
                &&NativeWeaponSkins.Normalize("face:mask,body:prototype")=="body:prototype,face:mask"&&NativeWeaponSkins.Index("body:prototype",NativeWeaponSkins.FaceEntry)==NativeWeaponSkins.FaceBoth&&NativeWeaponSkins.Index("face:visor",NativeWeaponSkins.FaceEntry)==NativeWeaponSkins.FaceVisor
                &&NativeWeaponSkins.With("body:prototype",NativeWeaponSkins.FaceEntry,"mask")=="body:prototype,face:mask"&&NativeBodySkins.Gate("body:sheldon,face:visor",false)=="face:visor","face gear picks normalize, default to both and survive the body's DEV gate");
            Check(NativeBodySkins.Keys.SequenceEqual(new[]{"standard","prototype","sheldon"})&&NativeBodySkins.Labels.SequenceEqual(new[]{"Standard","Prototype","Sheldon Clothes"}),"body skin keys and labels");
            Check(NativeWeaponSkins.Normalize("body:prototype")=="body:prototype"&&NativeWeaponSkins.Normalize("body:sheldon")=="body:sheldon"&&NativeWeaponSkins.Normalize("body:standard")==""&&NativeWeaponSkins.Normalize("body:gold")=="","body picks normalize (Standard is the default)");
            Check(NativeWeaponSkins.Normalize(" body : prototype ,sword:prototype,pistol:sheldon,body:sheldon")=="sword:prototype,body:sheldon","weapons first, the last body pick wins, a weapon cannot take the body's skin");
            Check(NativeBodySkins.Of("")==NativeBodySkins.StandardIndex&&NativeBodySkins.Of(null)==NativeBodySkins.StandardIndex&&NativeBodySkins.Of("bodyx:prototype,xbody:sheldon")==NativeBodySkins.StandardIndex&&NativeBodySkins.Of("body:prototypes")==NativeBodySkins.StandardIndex,"missing, malformed and lookalike entries read as Standard");
            Check(NativeBodySkins.Of("sword:prototype,body:prototype")==NativeBodySkins.PrototypeIndex&&NativeBodySkins.Of("body:sheldon")==NativeBodySkins.SheldonIndex,"the body pick reads back");
            Check(NativeWeaponSkins.With("sword:prototype","body","sheldon")=="sword:prototype,body:sheldon"&&NativeWeaponSkins.With("sword:prototype,body:sheldon","body","standard")=="sword:prototype"&&NativeWeaponSkins.Index("body:prototype","sword")==NativeWeaponSkins.StandardIndex,"setting the body keeps the weapon picks");
            var longest=string.Join(",",NativeWeaponSkins.Weapons.Select(s=>s+":prototype"))+",body:prototype";
            Check(NativeWeaponSkins.Normalize(longest)==longest&&longest.Length==298&&longest.Length<=NativeWeaponSkins.MaxLength,"the longest map with the body fits the frame limit ("+longest.Length+" / "+NativeWeaponSkins.MaxLength+")");
            // An older client parses only the weapons: the body entry is an unknown spec it drops, so it keeps its own body.
            Check(NativeWeaponSkins.Weapons.All(w=>NativeWeaponSkins.Index("body:prototype",w)==NativeWeaponSkins.StandardIndex)&&!NativeWeaponSkins.Skinned("body"),"the body entry never reads as a weapon pick");
            Check(NativeBodySkins.Index(null)==0&&NativeBodySkins.Index("")==0&&NativeBodySkins.Index("prototype")==1&&NativeBodySkins.Index("sheldon")==2&&NativeBodySkins.Index("nope")==0,"skin keys to indices");

            // ---- 3. The DEV gate (NativeCosmeticGate: live DEV access, no grant) ----
            double now=1e12;var dev=Access(true,now+60000);var lapsed=Access(true,now-1);var granted=Access(false,now+60000,"body:"+NativeBodySkins.SheldonClothes);var guest=NativeAccess.Guest;
            Check(NativeCosmeticGate.DevOnly(NativeBodySkins.SheldonClothes)&&NativeCosmeticGate.Restricted(NativeBodySkins.SheldonClothes),"the Sheldon Clothes are DEV only and restricted");
            Check(!NativeBodySkins.Allowed(guest,now)&&!NativeBodySkins.Allowed(null,now)&&!NativeBodySkins.LiveDeveloper(guest,now)&&!NativeBodySkins.LiveDeveloper(null,now),"no DEV access, no Sheldon Clothes");
            Check(NativeBodySkins.Gate("sword:prototype,body:sheldon",false)=="sword:prototype"&&NativeBodySkins.Gate("body:sheldon",true)=="body:sheldon"&&NativeBodySkins.Gate("body:prototype",false)=="body:prototype"&&NativeBodySkins.Gate(" body:sheldon ,x",false)=="","the gate drops the Sheldon Clothes to Standard for non-DEV accounts and normalizes");
            Check(NativeBodySkins.Allowed(dev,now)&&NativeBodySkins.LiveDeveloper(dev,now),"live DEV access wears the Sheldon Clothes");
            Check(!NativeBodySkins.Allowed(lapsed,now)&&!NativeBodySkins.LiveDeveloper(lapsed,now)&&!NativeBodySkins.Allowed(granted,now)&&!NativeBodySkins.LiveDeveloper(granted,now),"lapsed DEV access and a grant cannot (the Sheldon Head's gate)");
            foreach(var a in new[]{dev,lapsed,granted,guest,null})Check(NativeBodySkins.Allowed(a,now)==NativeBodySkins.LiveDeveloper(a,now),"the per-frame check agrees with the gate");

            // ---- 4. Meshes: every part, valid, deterministic, within budget, thinning by tier ----
            var built=new Dictionary<(int,int),PilotBodyMesh>();var tris=new Dictionary<(int,int),int>();
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})
            {
                for(int t=0;t<NativeBodySkins.Tiers;t++)
                {
                    var a=NativeBodyModels.Build(skin,t);var b=NativeBodyModels.Build(skin,t);built[(skin,t)]=a;tris[(skin,t)]=a.Triangles;string tag=NativeBodySkins.Labels[skin]+" tier "+t;
                    Check(a.Parts.Length==NativeBodySkins.Parts.Length,tag+" has every part");
                    Check(a.Triangles>0&&a.Triangles<=NativeBodySkins.Budget[t],tag+" within budget ("+a.Triangles+" / "+NativeBodySkins.Budget[t]+")");
                    Check(Same(a,b),tag+" builds deterministically");
                    for(int i=0;i<a.Parts.Length;i++)
                    {
                        var p=a.Parts[i];var s=p.Mesh;Check(p.Name==NativeBodySkins.Parts[i].Name&&p.Bone==NativeBodySkins.Parts[i].Bone&&p.Emitter==NativeBodySkins.Parts[i].Emitter,tag+" part "+p.Name+" keeps its name and joint");
                        Check(s.Vertices<65535&&s.Indices.All(k=>k>=0&&k<s.Vertices)&&s.Colors.Count==s.Vertices&&s.Emission.Count==s.Vertices&&s.Normals.Count==s.Vertices&&s.Indices.Count%3==0,tag+" "+p.Name+" arrays");
                        Check(s.Positions.All(v=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),tag+" "+p.Name+" finite positions, unit normals");
                        for(int k=0;k+2<s.Indices.Count;k+=3)
                        {
                            var p0=s.Positions[s.Indices[k]];var n=Vector3.Cross(s.Positions[s.Indices[k+1]]-p0,s.Positions[s.Indices[k+2]]-p0);
                            if(!(n.Length()>1e-12f))Check(false,tag+" "+p.Name+" degenerate triangle "+k/3);
                            // Faces point along their stored normal (the bank's winding: no mirror, no turn).
                            if(Vector3.Dot(n,s.Normals[s.Indices[k]])<0&&Vector3.Dot(n,s.Normals[s.Indices[k+1]])<0&&Vector3.Dot(n,s.Normals[s.Indices[k+2]])<0)Check(false,tag+" "+p.Name+" triangle "+k/3+" faces inward");
                        }
                        Check(s.Emission.All(e=>e.W==0||e.W==1||e.W==2),tag+" "+p.Name+" emission tags are untinted, body or accent");
                        Check(!p.Emitter||t>0||p.Lights.Count>0&&p.Lights.Count<=NativeArmorLight.MaxBoxes&&p.Level>1,tag+" "+p.Name+" emitter has 1-8 glowing voxels");
                    }
                    Check(a.Parts.Count(p=>p.Mesh.Vertices>0)>=40,tag+" draws nearly every part ("+a.Parts.Count(p=>p.Mesh.Vertices>0)+")");
                }
                string lab=NativeBodySkins.Labels[skin];int full=tris[(skin,0)],red=tris[(skin,1)],low=tris[(skin,2)];
                Check(red<=full*.75f&&low<=full*.45f&&low<red,lab+" tiers thin out ("+full+"/"+red+"/"+low+")");
                Check(8*full<=8*NativeBodySkins.Budget[0],lab+": eight pilots at Full detail draw "+8*full+" triangles");
                report.Add(lab+" "+full+" / "+red+" / "+low+" triangles (Full / Reduced / Low; 8 pilots at Full "+8*full+")");
            }
            Check(tris[(0,0)]>=2.5f*2616,"the Standard body is far more detailed than the Prototype ("+tris[(0,0)]+" vs 2616)");
            Check(NativeBodySkins.Model(0,0)=="pilot-std"&&NativeBodySkins.Model(0,1)=="pilot-std-reduced"&&NativeBodySkins.Model(0,2)=="pilot-std-low"&&NativeBodySkins.Model(2,0)=="pilot-sheldon"&&NativeBodySkins.Model(2,2)=="pilot-sheldon-low","model ids");
            Check(NativeBodySkins.Models().Count()==7&&NativeBodySkins.Models().All(NativeBodySkins.IsBody)&&!NativeBodySkins.IsBody("pilot-x")&&!NativeBodySkins.IsBody("siren")&&NativeBodySkins.SkinOf("pilot-sheldon-reduced")==2,"every body id is a pilot body to the per-part rules");
            Check(NativeBodySkins.Tier(false,1,5,false)==0&&NativeBodySkins.Tier(false,.3f,5,false)==1&&NativeBodySkins.Tier(true,1,5,false)==2&&NativeBodySkins.Tier(false,1,20,false)==1&&NativeBodySkins.Tier(false,1,40,false)==2&&NativeBodySkins.Tier(false,1,40,true)==0,"tiers by graphics, Reduced effects and distance; first person Full");

            // ---- 5. Volumes: inside the old parts plus relief; the head, hats and the Sheldon Head's cloth ----
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})
            {
                float relief=skin==0?NativeBodySkins.StandardRelief:NativeBodySkins.SheldonRelief;string lab=NativeBodySkins.Labels[skin];float worst=0;string worstPart="";
                var full=built[(skin,0)];
                for(int bone=0;bone<22;bone++)
                {
                    var pb=proto.Where(p=>p.Bone==bone).ToArray();if(pb.Length==0)continue;var pmin=pb.Select(p=>p.Min).Aggregate(Vector3.Min);var pmax=pb.Select(p=>p.Max).Aggregate(Vector3.Max);
                    foreach(var part in full.Parts.Where(p=>p.Bone==bone))foreach(var v in part.Mesh.Positions)
                    {
                        float o=Math.Max(Math.Max(Math.Max(pmin.X-v.X,v.X-pmax.X),Math.Max(pmin.Y-v.Y,v.Y-pmax.Y)),Math.Max(pmin.Z-v.Z,v.Z-pmax.Z));if(o>worst){worst=o;worstPart=part.Name;}
                    }
                }
                Check(worst<=relief+1e-4f,lab+" stays inside the exported joint volumes plus "+relief*100+" cm (most "+(worst*100).ToString("0.0")+" cm, "+worstPart+")");
                report.Add(lab+": most relief past the exported joint volumes "+(worst*100).ToString("0.00")+" cm ("+worstPart+"; limit "+relief*100+" cm)");
                // The head keeps its box (hats and the hood sit where they did): within the exported head, crown top at .36.
                var head=full.Parts.Where(p=>p.Bone==CapturedRig.Head).SelectMany(p=>p.Mesh.Positions).ToArray();var hmin=head.Aggregate(Vector3.Min);var hmax=head.Aggregate(Vector3.Max);
                var phead=proto.Where(p=>p.Bone==CapturedRig.Head).ToArray();var phmin=phead.Select(p=>p.Min).Aggregate(Vector3.Min);var phmax=phead.Select(p=>p.Max).Aggregate(Vector3.Max);
                Check(hmax.Y<=phmax.Y+1e-4f&&hmin.Y>=phmin.Y-1e-4f&&hmax.Z<=phmax.Z+.0015f&&hmin.Z>=phmin.Z-.0015f&&Math.Max(-hmin.X,hmax.X)<=.172f,lab+" head inside the exported head box (hat anchor and head size unchanged): "+hmin+" .. "+hmax);
                Check(Math.Max(-hmin.X,hmax.X)<NativeSheldonHead.ClearX&&hmax.Z<NativeSheldonHead.PlateZ&&hmin.Z>NativeSheldonHead.ClearBack&&hmax.Y<NativeSheldonHead.ClearTop&&hmin.Y>NativeSheldonHead.ClearBottom,lab+" head clear of the Sheldon Head's hood and face plate");
                // Nothing of the body enters the hood's own cloth (the hood, the mantle on the chest, the drapes on the clavicles: the
                // voxels NativeSheldonHead tests) where the exported body did not already reach: a vertex in a cloth voxel must lie in
                // the Prototype's boxes on its joint (the hood's lowest voxels already wrap the old cap tops and back panel).
                int inside=0,shared=0;
                foreach(var part in full.Parts)foreach(var v in part.Mesh.Positions)
                {
                    var src=new Vector3(-v.X,v.Y,v.Z);
                    bool cloth=part.Bone==CapturedRig.Chest&&NativeSheldonHead.MantleSolid(src)||part.Bone==CapturedRig.LClav&&NativeSheldonHead.DrapeSolid(src,1)||part.Bone==CapturedRig.RClav&&NativeSheldonHead.DrapeSolid(src,-1)||part.Bone==CapturedRig.Head&&NativeSheldonHead.HoodSolid(src);
                    if(!cloth)continue;if(proto.Any(p=>p.Bone==part.Bone&&p.Boxes.Any(b=>v.X>=b.min.X-.001f&&v.X<=b.max.X+.001f&&v.Y>=b.min.Y-.001f&&v.Y<=b.max.Y+.001f&&v.Z>=b.min.Z-.001f&&v.Z<=b.max.Z+.001f)))shared++;else inside++;
                }
                Check(inside==0,lab+" adds no vertex inside the Sheldon Head's hood, mantle or drapes beyond the exported body ("+inside+" new, "+shared+" where the old body already was)");
                // The Sheldon Head's chevron (chest space, source x .12 = bank -.12) sits in front of the body.
                float chevronBack=full.Parts.Where(p=>p.Bone==CapturedRig.Chest).SelectMany(p=>p.Mesh.Positions).Where(v=>Math.Abs(v.X+NativeSheldonHead.EmblemAt.X)<NativeSheldonHead.EmblemSize*.5f&&Math.Abs(v.Y-NativeSheldonHead.EmblemAt.Y)<NativeSheldonHead.EmblemSize*.55f).Select(v=>v.Z).DefaultIfEmpty(0).Max();
                Check(chevronBack<NativeSheldonHead.ChestFront+.0025f-.001f,lab+" leaves the Sheldon Head's chevron in front of the chest (body front there "+chevronBack.ToString("0.000")+")");
                // Team bands (NativeStatusVisuals) stay in front of the chest and back armour as on the Prototype.
                float chestFront=full.Parts.Where(p=>p.Bone==CapturedRig.Chest).SelectMany(p=>p.Mesh.Positions).Where(v=>Math.Abs(v.X)<.2f&&v.Y>.175f&&v.Y<.265f).Select(v=>v.Z).Max();
                Check(chestFront<NativeStatusVisuals.BandPositions[0].Z+NativeStatusVisuals.BandSizes[0].Z*.5f,lab+" chest under the team band");
            }

            // ---- 6. Armour lights (NativeArmorLight.Build over the body's own trims) ----
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})
            {
                var body=built[(skin,0)];var sections=NativeBodyModels.LightSections(body);var plans=NativeArmorLight.Build(sections);string lab=NativeBodySkins.Labels[skin];
                Check(sections.Count>=body.Parts.Length&&plans.Length==sections.Count,lab+" light sections: the parts then the strips");
                for(int i=0;i<body.Parts.Length;i++)
                {
                    var p=body.Parts[i];var plan=plans[i];
                    if(p.Emitter)Check(plan.Role==NativeArmorLight.Emitter&&plan.Boxes.Length==p.Lights.Count&&plan.Level>1,lab+" "+p.Name+" is an emitter of its own voxels");
                    else Check(plan.Role==NativeArmorLight.Receiver&&plan.Boxes.Length<=NativeArmorLight.MaxBoxes,lab+" "+p.Name+" is a receiver");
                }
                Check(plans.Take(body.Parts.Length).Count(p=>p.Role==NativeArmorLight.Receiver&&p.Boxes.Length>0)>=25,lab+" trims light the plates round them");
                Check(plans.Take(body.Parts.Length).Any(p=>p.SeverJoints!=0),lab+" a severed limb drops the light of a trim it left (sever masks)");
            }
            // The Standard cuffs are the exported cuff boxes, so the held weapons' cuff spill (built from the Prototype) stays true.
            foreach(var side in new[]{"left-cuff","right-cuff"})
            {
                var std=built[(0,0)].Parts.First(p=>p.Name==side);var pc=proto.First(p=>p.Part==side);
                Check(std.Lights.Count==pc.Boxes.Count&&std.Lights.All(l=>pc.Boxes.Any(b=>Vector3.Distance(b.min,l.min)<1e-4f&&Vector3.Distance(b.max,l.max)<1e-4f)),"Standard "+side+" lights the held weapon as the Prototype's");
            }

            // ---- 7. Hit geometry never reads a body: source scans and identical results for every skin ----
            string[] simulation={"Core/NativeBodyDamage.cs","Core/NativeCombatRules.cs","Core/NativeMeleeRules.cs","Core/NativeMeleeFeel.cs","Core/NativeMeleeSlice.cs","Core/NativeCorpseRules.cs","Core/NativeWoundCarve.cs","Core/NativeLocomotionRules.cs","Core/NativeKillBoxes.Generated.cs","Core/NativeKillEffectRules.cs","Core/MeleeClearance.cs","Core/CapturedRig.cs","Core/ArenaWorld.cs","Core/NativeHitReact.cs","Core/NativeStagger.cs","Core/NativeWhirlwindRules.cs","Core/NativeSprintAttack.cs","Core/NativeAirAttackSimulation.cs",
                "Runtime/NativeArena.cs","Runtime/NativeProjectiles.cs","Runtime/NativeMelee.cs","Runtime/NativeMeleeSpecials.cs","Runtime/NativeMeleeAbilities.cs","Runtime/NativeAbilities.cs","Runtime/NativeRangedAbilities.cs","Runtime/NativeDeaths.cs","Runtime/NativeCorpseWorld.cs","Runtime/NativeSummons.cs","Runtime/NativeNetworkFrame.cs"};
            string[] visual={"NativeBodySkins","NativeBodyModels","pilot-std","pilot-sheldon","WoundedPilot","sourceModels","SourceModels","BodySkin"};
            foreach(var f in simulation){var text=File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));foreach(var token in visual)Check(!text.Contains(token),f+" (hit volumes, damage, melee contact, wounds, corpses) never reads the visual body ("+token+")");}
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            Check(R("NativeArena.cs").Contains("capsule.radius=NativeLocomotionRules.Radius;capsule.height=2*NativeLocomotionRules.HalfHeight;")&&R("NativeDeathPresentation.cs").Contains("foreach(var shape in CapturedRig.Boxes)"),"the host capsule and the ragdoll colliders are sized from the rules and the rig boxes");
            // The hit regions, headshots and wounds over a grid, evaluated once per skin a seat could hold: identical results.
            var digests=new List<string>();
            foreach(var skinMap in new[]{"","body:prototype","body:sheldon"})
            {
                var seat=new NativeSeat{id=7,weaponSkins=skinMap};var sb=new StringBuilder();
                for(float y=-.9f;y<=.9f;y+=.09f)for(float x=-.4f;x<=.4f;x+=.08f)for(int yaw=0;yaw<360;yaw+=45)
                {
                    int part=NativeBodyDamage.Part(new V3(x,y,.1f),yaw);bool head=NativeCombatRules.Headshot(new V3(0,0,0),yaw,new V3(x*3,y+.64f,-3),new V3(-x,.0f,3),.05f,false);
                    var wound=NativeBodyDamage.Resolve(NativeBodyDamage.Melee("greatsword",1),part,40,y>.5f,head);
                    sb.Append(part).Append(head?'h':'b').Append(wound?.kind).Append(wound?.radius.ToString("R")).Append(';');
                }
                sb.Append(NativeLocomotionRules.Radius).Append(NativeLocomotionRules.HalfHeight).Append(seat.weaponSkins.Length>=0?"":"x");
                digests.Add(Hash(sb.ToString()));
            }
            Check(digests.Distinct().Count()==1,"hit regions, headshots and wounds are identical for the Standard, Prototype and Sheldon Clothes seats ("+digests[0]+")");
            report.Add("hit geometry: no simulation file references a body ("+simulation.Length+" files scanned); hit parts, headshots and wounds over "+(21*11*8)+" probes identical for all three skins (digest "+digests[0]+")");

            // ---- 8. The rig: the same joints for every body over animation; each body's joint-local extent past the Prototype ----
            string[] motion=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"*.cs").Where(f=>{var n=Path.GetFileName(f);return n!="NativeBodySkins.cs"&&n!="NativeBodyModels.cs"&&n!="NativeWeaponSkins.cs"&&n!="NativeHatLooks.cs";}).ToArray();
            foreach(var f in motion){var text=File.ReadAllText(f);Check(!text.Contains("NativeBodySkins")&&!text.Contains("NativeBodyModels"),Path.GetFileName(f)+": no pose, clip or rule reads the body skin");}
            var scenes=Scenes();float jointError=0;int frames=0;
            var clip=new Clip(built[(2,0)],proto);
            foreach(var sc in scenes)
            {
                // One rig per skin from the same replicated actor stream.
                var rigs=new[]{new Poser(),new Poser(),new Poser()};var actors=new[]{sc.Make(),sc.Make(),sc.Make()};
                for(float t=.6f;t<=sc.End+1e-4f;t+=1/60f)
                {
                    for(int k=0;k<3;k++){sc.Step(actors[k],t);rigs[k].Pose(actors[k],t);}
                    if(t<sc.From)continue;frames++;
                    for(int b=0;b<22;b++)for(int k=1;k<3;k++){jointError=Math.Max(jointError,Vector3.Distance(rigs[0].Position[b],rigs[k].Position[b]));jointError=Math.Max(jointError,Math.Abs(1-Math.Abs(Quaternion.Dot(rigs[0].Rotation[b],rigs[k].Rotation[b]))));}
                    clip.Frame(sc.Name,rigs[2]);
                }
            }
            Check(jointError<=1e-4f&&frames>500,"every joint of every body is where today's body has it over "+frames+" animation frames, within float error (most "+jointError+")");
            report.Add("rig: "+frames+" frames over "+scenes.Count+" animations (idle, run, sprint, roll, jump, air attack, sword chain, sprint attack, greatsword Whirlwind, stagger), joint positions and rotations equal for all three bodies within float error (most "+jointError.ToString("0.0e0")+" m / quaternion dot)");
            // ---- 9. The Sheldon Clothes' cloth over the same frames ----
            foreach(var line in clip.Report())report.Add(line);
            Check(clip.WorstOther<=.035f,"no animation pushes the other leg more than 3.5 cm into a coat panel (most "+(clip.WorstOther*100).ToString("0.0")+" cm)");
            Check(clip.FramesOther<=.25f,"the other leg touches a coat panel (>1 cm) in at most a quarter of the frames ("+(clip.FramesOther*100).ToString("0")+"%)");
            Check(clip.OwnShare<=.35f,"a leg's own shin or thigh sinks past its cloth (>1 cm) in at most a third of the frames ("+(clip.OwnShare*100).ToString("0")+"%)");

            // ---- 10. Gibs: the coat panels join a Sheldon Clothes corpse's kill boxes on their joints ----
            var boxes=new KillBox[NativeKillBoxes.MaxRigBoxes];int n0=NativeKillBoxes.Rig("sword","cap",boxes);
            Check(NativeBodyModels.Gibs(NativeBodySkins.StandardIndex,boxes,n0)==n0&&NativeBodyModels.Gibs(NativeBodySkins.PrototypeIndex,boxes,n0)==n0,"the Standard and Prototype corpses keep the exported kill boxes");
            int n1=NativeBodyModels.Gibs(NativeBodySkins.SheldonIndex,boxes,n0);
            Check(n1>n0&&n1-n0==NativeBodyModels.SheldonGibs.Length&&NativeBodyModels.SheldonGibs.Length==12,"the Sheldon Clothes' twelve coat panels are thrown with the body ("+(n1-n0)+")");
            Check(NativeBodyModels.SheldonGibs.All(b=>b.Zone==0&&b.Detail&&!b.Hidden&&b.Tag==0&&Array.IndexOf(NativeKillBoxes.PartRoots,b.Root)>=0&&b.Bone==b.Root),"each panel rides its leg joint's ragdoll part (a severed leg carries its cloth)");
            Check(NativeBodyModels.SheldonGibs.All(b=>b.Bone==CapturedRig.LHip||b.Bone==CapturedRig.RHip||b.Bone==CapturedRig.LKnee||b.Bone==CapturedRig.RKnee),"panels on the thighs and shins");
            int full96=NativeKillBoxes.Rig("leet","auditor-halo",new KillBox[NativeKillBoxes.MaxRigBoxes]);
            Check(full96+NativeBodyModels.SheldonGibs.Length+4<=NativeKillBoxes.MaxRigBoxes,"the largest rig with the panels and team bands fits the kill-box budget ("+(full96+12+4)+" / "+NativeKillBoxes.MaxRigBoxes+")");

            // ---- 11. Built once, no allocation per draw ----
            string sk=null;int sum=0;
            NativeBodySkins.Model("body:sheldon",1);NativeBodySkins.SheldonGlow("sheldon-head","","#70e1d0");
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20000;i++)
            {
                sk=NativeBodySkins.Model(NativeBodySkins.Of(i%3==0?"sword:prototype,body:sheldon":i%3==1?"body:prototype":""),NativeBodySkins.Tier(i%7==0,1,i%40,false));
                sum+=NativeBodySkins.SkinOf(sk)+(NativeBodySkins.IsBody(sk)?1:0)+NativeBodySkins.SheldonGlow(i%2==0?"sheldon-head":"cap",i%3==0?"#ff3dcf":"","#70e1d0").Length+NativeBodySkins.SheldonGlowRgb("sheldon-head","",0x70e1d0)+(NativeBodySkins.LiveDeveloper(dev,now)?1:0)+NativeWeaponSkins.Of("body:sheldon",NativeBodySkins.Entry).Length;
            }
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated==0&&sum!=0,"choosing a body, its tier, its id and its glow allocates nothing ("+allocated+" bytes over 20000 draws)");
            Check(ReferenceEquals(NativeBodySkins.Model(0,1),NativeBodySkins.Model("",1))&&ReferenceEquals(NativeBodySkins.SheldonGlow("sheldon-head","","#70e1d0"),NativeBodySkins.SheldonCyan),"ids and colours are cached strings");
            Check(NativeBodySkins.SheldonGlow("sheldon-head","","#70e1d0")=="#22e6ff"&&NativeBodySkins.SheldonGlow("sheldon-head","#ff3dcf","#70e1d0")=="#ff3dcf"&&NativeBodySkins.SheldonGlow("cap","","#70e1d0")=="#70e1d0"&&NativeBodySkins.SheldonGlow("cap","#4dffa6","#70e1d0")=="#4dffa6","the Sheldon Clothes glow in the hood's colour, a picked hat colour, else the accent");
            Check(NativeBodySkins.CyberCyan==0x22e6ff&&NativeBodySkins.CyberMagenta==0xff3dcf&&R("NativeCyberTheme.cs").Contains("CyanHex=0x22e6ff")&&R("NativeCyberTheme.cs").Contains("MagentaHex=0xff3dcf"),"the status lights use the cyber palette (NativeCyberTheme)");

            // ---- 12. Wiring (source text; Unity cannot run here) ----
            var bankText=R("NativeSourceModels.cs");var models=R("NativeBodySkinModels.cs");var pres=R("NativeBodySkinPresentation.cs");
            Check(bankText.Contains("HordeMaterials();WeaponSkins();SheldonModels();PilotBodies();")&&Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Count(f=>File.ReadAllText(f).Contains("NativeBodyModels.Build("))==1,"the bank builds the bodies once, at load, in one place");
            Check(models.Contains("mesh.vertices=vertices;mesh.normals=normals;")&&models.Contains("vertices[i]=new Vector3(p.X,p.Y,p.Z);")&&models.Contains("mesh.UploadMeshData(true);"),"body meshes upload in the bank frame (no mirror), non-readable");
            Check(models.Contains("NativeArmorLight.Build(NativeBodyModels.LightSections(full))")&&models.Contains("ArmorEmitters=e;ArmorReceivers=r;ArmorHeld=h;"),"armour tables per body part; the Prototype's counters (the Editor build check's eleven emitters) untouched");
            Check(pres.Contains("NativeBodySkins.Of(SeatWeaponSkins(id))")&&pres.Contains("NativeBodySkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first)")&&pres.Contains("sheldonScripted?NativeBodySkins.SheldonIndex"),"the body comes from the seat (live, guest, killcam), the tier from the settings and distance, the Sheldon Intro dresses its pilot");
            Check(!System.Text.RegularExpressions.Regex.IsMatch(pres.Substring(pres.IndexOf("private string RigBody(",StringComparison.Ordinal)),@"""[a-z\-]+""\s*\+"),"choosing a body builds no strings");
            var wiring=new (string file,string text,string what)[]
            {
                ("NativeSourcePresentation.cs","sourceModels.WoundedPilot(a.Id,a.Wounds,a.BodyColor,ForceCompatibilityRendering,RigBody(rig,a,first,out bodySkin))","arena, first-person arms, entrance, viewer and killcam rigs"),
                ("NativeSourcePresentation.cs","BodyAccent(bodySkin,a.AccentColor,a.Hat,a.HatColor),partOpacity:reveal,partTint:tint,look:look,hitFlash:flash","hit flash, status and Chip tints, stealth and reveal fades on the body"),
                ("NativeDeathPresentation.cs","BodyAt(death.id,center,out bodySkin)","corpses, ragdolls and dismember parts"),
                ("NativeDeathPresentation.cs","BodyAccent(bodySkin,death.actor?.AccentColor,death.actor?.Hat,death.actor?.HatColor),alpha);","corpse accent"),
                ("NativeSceneMotion.cs","string victimBody=BodyAt(death.id,death.position,out int bodySkin);","death scenes"),
                ("NativeVentDeathPresentation.cs","string pilot=BodyAt(death.id,death.position,out int bodySkin);","vent falls"),
                ("NativeSirenDropPresentation.cs","WoundedPilot(death.id,death.actor?.Wounds,death.actor?.BodyColor,ForceCompatibilityRendering,victimBody)","the Siren's crush drop"),
                ("NativeInteractionPresentation.cs","RepairBoxes(p.Seat.spec==\"siren\"?\"siren\":\"pilot\",r.Boxes)","PvE revive scan boxes (the exported part bounds, which every body sits within)"),
                ("NativeReviveHoloPresentation.cs","sourceModels.QueueShadowless(h.Healed,","the healed body (the holographic revive draws it over the wounded one)"),
                ("NativeDevFighterEffects.cs","demonBodies[slot]=BodyGhost(BodySkin(p.Seat.id),","Demon Step ghosts"),
                ("NativeBodyChipPresentation.cs","string shell=BodyGhost(","the parry flash shell"),
                ("NativeMeleeKitPresentation.cs","g.Body=Ghost(BodyModel(BodySkin(a.Id),BodyTier(root,false)));","dash ghosts"),
                ("NativeMeleeKitPresentation.cs","string pilot=Ghost(BodyModel(BodySkin(a.Id),","the Shadow Step decoy"),
                ("NativeWeaponFxPresentation.cs","afterimageBody[slot]=BodyAfterimage(BodySkin(a.Id),BodyTier(root,false))??pilot;","roll afterimages"),
                ("NativeDeathBroadcastPresentation.cs","stage.Pilot=sourceModels.Hologram(BodyModel(BodySkin(item.Victim),BodyCloseTier));","the death broadcast hologram"),
                ("NativeIntros.cs","sourceModels.EntranceGhost(BodyModel(script is NativeSheldonIntroScript?NativeBodySkins.SheldonIndex:BodySkin(introActor),BodyCloseTier))","entrance afterimages and holograms"),
                ("NativeDraftPresentation.cs","int bodySkin=NativeBodySkins.Of(seat.weaponSkins);if(!asModel){sourceModels.Queue(siren?\"siren\":BodyModel(bodySkin,BodyCloseTier)","the chip draft seat"),
                ("NativeRooftopPresentation.cs","BodyModel(i==0?pilot.BodySkin:NativeBodySkins.StandardIndex,","the rooftop home pilot and the crowd"),
                ("NativePortraits.cs","bool pilot=NativeBodySkins.IsBody(id);int cls=pilot?NativePortraitRules.PartClass(name)","portrait part classes and lobby-pose covers for every body"),
                ("NativePortraits.cs","var bodyParts=siren?Parts(\"siren\"):Parts(s.BodyModel);","portraits, lobby poses and the Locker stage"),
                ("NativePortraitUI.cs","look.BodySkin=NativeWeaponSkins.Of(seat.weaponSkins,NativeBodySkins.Entry);","result cards"),
                ("NativeLockerUI.cs","BodySkin=BodySkinShown()","the Locker preview"),
                ("NativeKillEffectPresentation.cs","int bodySkin=NativeBodySkins.Of(arena?.Match?.Seat(Death.actor?.Id??Death.id)?.weaponSkins);","gibs and crush fragments"),
                ("NativeWoundGeometry.cs","public string WoundedPilot(int id,NativeWound[] impacts,string bodyHex,bool low=false,string basis=NativeBodySkins.PrototypeModel)","wounds carve onto any body"),
                ("NativeSheldonIntroPresentation.cs","look.BodyColor=NativeSheldonIntro.Suit;","the Sheldon Intro's suit"),
                ("NativeNetwork.cs","weaponSkins=GatedSkins(),","the profile sends the gated map"),
                ("NativeNetwork.cs","weaponSkins=NativeBodySkins.Gate(c.weaponSkins,NativeCosmeticRules.Allowed(access,\"body\",NativeBodySkins.SheldonClothes)),","the host gates every seat"),
                ("NativeProfileUI.cs","prefs.weaponSkins=GatedSkins();","saving gates the map"),
                ("NativeLockerSkinsUI.cs","lockerBodyTry=keys[k]","the Locker's body card tries a body on"),
                ("NativeLockerUI.cs","lockerBodySheldon=NativeCosmeticRules.Allowed(access,\"body\",NativeBodySkins.SheldonClothes);lockerSkins.Clear();lockerSkins.Add(NativeBodySkins.Entry);","the body card leads the skins; the Sheldon Clothes listed only with DEV access"),
            };
            foreach(var w in wiring)Check(R(w.file).Contains(w.text),w.what+" ("+w.file+")");
            Check(NativeSheldonIntro.Suit=="#e5e7eb","the Sheldon Intro shows the Sheldon Clothes in the hood's own fabric (the export's body colour)");
            // The DEV fighters share the base body; the Siren keeps its own model, its first-person ink arms the Prototype's.
            var source=R("NativeSourcePresentation.cs");
            Check(source.Contains("a.SourceSpec==\"siren\"?first?sirenInk??(sirenInk=sourceModels.Ink(\"pilot\")):\"siren\":"),"the Siren keeps its own body and its first-person ink arms");
            var editor=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs"));
            Check(editor.Contains("models.PilotBodyModels!=6"),"the Editor build check counts the body models");
            foreach(var line in report)Console.WriteLine("PASS body skins: "+line);
            Console.WriteLine("PASS body skins: "+count+" checks (the Prototype bake, part table, skin map and DEV gate, "+tris.Count+" body tiers, volumes and hood clearances, armour lights, hit geometry, rig, cloth clipping, gibs, allocation, "+wiring.Length+" wiring sites)");
        }

        // ---- the animation set (the Core melee route the Runtime uses: MeleeMotion, the kit overlay, rolls, reactions) ----
        private sealed class Scene{public string Name;public float From=1,End=2.2f;public Func<Actor> Make;public Action<Actor,float> Step;}
        private static List<Scene> Scenes()
        {
            Actor Pilot(string spec)=>new Actor{Id=1,SourceSpec=spec,Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0,MeleeKit=new NativeMeleeKit(),AccentColor="#70e1d0"};
            var list=new List<Scene>
            {
                new Scene{Name="idle",Make=()=>Pilot("sword"),Step=(a,t)=>{}},
                new Scene{Name="run",Make=()=>Pilot("sword"),Step=(a,t)=>{a.Velocity=new V3(0,0,6.8f);}},
                new Scene{Name="sprint",Make=()=>Pilot("greatsword"),Step=(a,t)=>{a.Velocity=new V3(0,0,9.5f);a.Sprinting=true;}},
                new Scene{Name="roll",Make=()=>Pilot("daggers"),End=1.7f,Step=(a,t)=>{if(t>=1&&a.RollAt<0){a.RollAt=1;a.RollDirection=new V3(0,0,1);a.VisualRollDuration=.55f;}a.Velocity=t>=1&&t<1.55f?new V3(0,0,8):new V3();}},
                new Scene{Name="jump",Make=()=>Pilot("sword"),End=2,Step=(a,t)=>{float tau=t-1;bool air=tau>=0&&tau<.9f;a.Grounded=!air;a.Velocity=air?new V3(0,7-15.5f*tau,3):new V3();}},
                new Scene{Name="air attack",Make=()=>Pilot("greatsword"),End=2,Step=(a,t)=>{float tau=t-1;bool air=tau>=0&&tau<.9f;a.Grounded=!air;a.Velocity=air?new V3(0,7-15.5f*tau,2):new V3();if(t>=1.25f&&a.AttackAt<1){a.AttackAt=1.25f;a.AttackUntil=1.9f;a.VisualWindup=.2f;a.VisualActive=.16f;a.AirAttack=true;}}},
                new Scene{Name="sword chain",Make=()=>Pilot("sword"),End=2.8f,Step=(a,t)=>{int k=t<1.55f?0:t<2.1f?1:2;a.AttackAt=1+k*.55f;a.AttackUntil=a.AttackAt+.55f;a.VisualWindup=.24f;a.VisualActive=.16f;a.Combo=k;}},
                new Scene{Name="sprint attack",Make=()=>Pilot("sword"),End=2,Step=(a,t)=>{a.Sprinting=t<1.2f;a.Velocity=t<1.5f?new V3(0,0,9):new V3();if(t>=1.2f&&a.AttackAt<1){a.AttackAt=1.2f;a.AttackUntil=1.8f;a.VisualWindup=.2f;a.VisualActive=.16f;a.SprintAttack=true;}}},
                new Scene{Name="stagger",Make=()=>Pilot("sword"),End=1.8f,Step=(a,t)=>{if(t<1){a.ReactionAt=-100;return;}var r=MeleeReaction.Hit(a.Position,a.Yaw,new V3(0,0,1),new V3(0,1.2f,0),1,30,false);a.ReactionAt=r.At;a.ReactionUntil=Math.Max(r.Until,1.5f);a.ReactionYaw=r.Yaw;a.ReactionTurn=r.Turn;a.ReactionStrength=r.Strength;a.ReactionDir=new V3(r.Direction.X,r.Direction.Y,r.Direction.Z);a.ReactionPoint=new V3(r.Point.X,r.Point.Y,r.Point.Z);a.ReactionZone=r.Zone;a.StunUntil=1.5f;}},
            };
            // The greatsword Whirlwind on Astra's rule (Begin, Advance, NextTick), the kit as NativeMeleeAbilities replicates it.
            NativeMeleeAction act=null;
            list.Add(new Scene{Name="Whirlwind",End=3.6f,Make=()=>{act=NativeWhirlwindRules.Begin(1,0);return Pilot("greatsword");},Step=(a,t)=>
            {
                NativeWhirlwindRules.Advance(act,t);while(NativeWhirlwindRules.NextTick(act,t)){}
                var k=a.MeleeKit;bool live=t>=act.At&&t<NativeMeleeKitRules.End(act);a.Velocity=t>act.Release?new V3(-3,0,3):new V3();
                if(!live){k.Kind=k.Phase=k.RecastKind=null;k.Tick=0;a.SpecialKind=null;a.SpecialAt=-100;a.SpecialUntil=0;a.SpecialReleased=false;return;}
                k.Kind=act.Fx;k.Phase=act.Phase;k.At=act.At;k.Release=act.Release;k.Until=act.Until;k.RecoverUntil=NativeMeleeKitRules.End(act);k.Tick=act.Tick;k.Yaw=act.Yaw;k.RecastKind=null;k.Target=0;
                a.SpecialKind=act.Kind;a.SpecialAt=act.At;a.SpecialRelease=act.Release;a.SpecialUntil=NativeMeleeKitRules.End(act);a.SpecialReleased=act.Released;a.SpecialLanded=-1;a.Yaw=0;
            }});
            return list;
        }
        // CapturedAnimation.MeleePose on Core: MeleeMotion with the roll, special, recovery and juggle overlays; joints in the
        // Unity frame relative to the root (the source rig mirrored in x).
        private sealed class Poser
        {
            private readonly MeleeMotion melee=new MeleeMotion{Locomotion=new NativeLocomotionCarry()};private readonly MeleeView v=new MeleeView();private readonly CapturedRig.Pose pose=new CapturedRig.Pose();private float last=-100;
            private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();
            public readonly Vector3[] Position=new Vector3[22];public readonly Quaternion[] Rotation=new Quaternion[22];
            public void Pose(Actor a,float time)
            {
                float dt=last<-90||time<last||time-last>.35f?1/60f:Math.Min(.1f,time-last);last=time;
                v.Id="1";v.Spec=a.SourceSpec;v.Yaw=-a.Yaw*(float)Math.PI/180;v.Vel=new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);v.Grounded=a.Grounded;v.Hp=a.Hp;v.Sprinting=a.Sprinting;
                v.LastAttack=a.AttackAt;v.Windup=a.VisualWindup;v.AttackRate=1;v.AttackIndex=a.Combo+1;v.SwingAt=float.NaN;v.SwingUntil=float.NaN;v.AirAttack=a.AirAttack?1:0;v.SprintAttack=a.SprintAttack;
                v.RollAt=a.RollAt;v.RollUntil=a.RollAt>-90?a.RollAt+a.VisualRollDuration:-100;v.ActionKind=a.SpecialKind;v.ActionAt=a.SpecialAt;v.ActionUntil=a.SpecialUntil;
                v.Reaction=a.ReactionAt>-90;v.ReactionAt=a.ReactionAt;v.ReactionUntil=a.ReactionUntil;v.ReactionYaw=a.ReactionYaw;v.ReactionTurn=a.ReactionTurn;v.ReactionStrength=a.ReactionStrength;
                v.ReactionDir=new Vector3(a.ReactionDir.x,a.ReactionDir.y,a.ReactionDir.z);v.ReactionPoint=new Vector3(a.ReactionPoint.x,a.ReactionPoint.y,a.ReactionPoint.z);v.ReactionZone=a.ReactionZone;v.HitStunUntil=a.StunUntil;
                v.Down=NativeSpecialPose.Down(a,time);v.DownPhase=v.Down?NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time):null;
                bool roll=a.Rolling(time),special=NativeSpecialPose.Active(a,time),down=NativeSpecialPose.Down(a,time);
                melee.Rig.GroupPosition=new Vector3(0,.865f,0);melee.Rig.GroupYaw=0;MeleeKitMotion.Stance(a,time,melee.Gait);
                melee.Sample(v,time,dt,r=>{r.WritePose(pose);if(roll)CapturedRig.Roll(pose,(time-a.RollAt)/a.VisualRollDuration);if(special)NativeSpecialPose.Special(pose,a,time,false);if(down)NativeSpecialPose.Recover(pose,a,time);r.ReadPose(pose);if(special&&MeleeKitMotion.RidesGait(a,time))r.LegsLayered=true;});
                melee.Rig.WritePose(pose);pose.World();
                for(int b=0;b<22;b++){var p=pose.WorldPosition[b];var q=pose.WorldRotation[b];Position[b]=new Vector3(-p.X,p.Y,p.Z);Rotation[b]=new Quaternion(q.X,-q.Y,-q.Z,q.W);}
            }
        }
        // The Sheldon Clothes' coat panels against the legs, over the animation frames: the body's own leg geometry (the trousers,
        // leggings, knee wraps and boots: every leg vertex not on a coat panel) tested against the other leg's panels, and across
        // the knee against its own leg's other panels (the shin in the thigh's panels, the thigh in the shin's); depth = how far
        // a vertex sits inside a panel (to its nearest face). Panel against panel (cloth meeting cloth) is reported apart.
        private sealed class Clip
        {
            private readonly List<(int bone,Vector3 min,Vector3 max)> cloth=new List<(int,Vector3,Vector3)>();private readonly List<(int bone,Vector3 p,bool cloth)> samples=new List<(int,Vector3,bool)>();
            private int frames,otherFrames,ownFrames,clothFrames;private float worstOther,worstOwn,worstCloth;private readonly Dictionary<string,(int frames,int other,int own,float worst,float own2)> byScene=new Dictionary<string,(int,int,int,float,float)>();
            public float WorstOther=>worstOther;public float FramesOther=>frames>0?(float)otherFrames/frames:0;public float OwnShare=>frames>0?(float)ownFrames/frames:0;
            private static readonly int[] Left={CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.LToe},Right={CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot,CapturedRig.RToe};
            public Clip(PilotBodyMesh sheldon,List<Proto> proto)
            {
                foreach(var p in sheldon.Parts)foreach(var c in p.Cloth)cloth.Add((p.Bone,c.min,c.max));
                var seen=new HashSet<(int,Vector3)>();
                foreach(var p in sheldon.Parts)
                {
                    if(Array.IndexOf(Left,p.Bone)<0&&Array.IndexOf(Right,p.Bone)<0)continue;
                    foreach(var v in p.Mesh.Positions)
                    {
                        if(!seen.Add((p.Bone,v)))continue;
                        // On a panel of its own joint (within 4 mm: the panel, its chamfers and the trims laid on it) it is cloth.
                        bool onCloth=cloth.Any(c=>c.bone==p.Bone&&v.X>=c.min.X-.004f&&v.X<=c.max.X+.004f&&v.Y>=c.min.Y-.004f&&v.Y<=c.max.Y+.004f&&v.Z>=c.min.Z-.004f&&v.Z<=c.max.Z+.004f);
                        samples.Add((p.Bone,v,onCloth));
                    }
                }
            }
            private static bool Side(int bone,int[] side)=>Array.IndexOf(side,bone)>=0;
            private static bool Across(int a,int b)=>a==CapturedRig.LHip&&b==CapturedRig.LKnee||a==CapturedRig.LKnee&&b==CapturedRig.LHip||a==CapturedRig.RHip&&b==CapturedRig.RKnee||a==CapturedRig.RKnee&&b==CapturedRig.RHip;
            public void Frame(string scene,Poser r)
            {
                float other=0,own=0,cl=0;
                foreach(var (cb,min,max) in cloth)
                {
                    var inv=Quaternion.Inverse(r.Rotation[cb]);
                    foreach(var (sb,p,onCloth) in samples)
                    {
                        if(sb==cb)continue;bool otherLeg=Side(cb,Left)!=Side(sb,Left);if(!otherLeg&&!Across(cb,sb))continue;
                        var world=Vector3.Transform(p,r.Rotation[sb])+r.Position[sb];var local=Vector3.Transform(world-r.Position[cb],inv);
                        float d=Math.Min(Math.Min(Math.Min(local.X-min.X,max.X-local.X),Math.Min(local.Y-min.Y,max.Y-local.Y)),Math.Min(local.Z-min.Z,max.Z-local.Z));
                        if(d<=0)continue;if(onCloth)cl=Math.Max(cl,d);else if(otherLeg)other=Math.Max(other,d);else own=Math.Max(own,d);
                    }
                }
                frames++;if(other>.01f)otherFrames++;if(own>.01f)ownFrames++;if(cl>.01f)clothFrames++;worstOther=Math.Max(worstOther,other);worstOwn=Math.Max(worstOwn,own);worstCloth=Math.Max(worstCloth,cl);
                byScene.TryGetValue(scene,out var s);byScene[scene]=(s.frames+1,s.other+(other>.01f?1:0),s.own+(own>.01f?1:0),Math.Max(s.worst,other),Math.Max(s.own2,own));
            }
            public IEnumerable<string> Report()
            {
                yield return "Sheldon Clothes cloth clipping over "+frames+" frames ("+samples.Count(x=>!x.cloth)+" leg vertices, "+cloth.Count+" panels): the other leg >1 cm inside a coat panel in "+(FramesOther*100).ToString("0.0")+"% (most "+(worstOther*100).ToString("0.0")+" cm); a leg's own shin or thigh >1 cm into its panels across the knee in "+(OwnShare*100).ToString("0.0")+"% (most "+(worstOwn*100).ToString("0.0")+" cm); panel into panel >1 cm in "+(100f*clothFrames/Math.Max(1,frames)).ToString("0.0")+"% (most "+(worstCloth*100).ToString("0.0")+" cm)";
                foreach(var pair in byScene)yield return "  "+pair.Key+": "+pair.Value.frames+" frames, other leg "+(100f*pair.Value.other/pair.Value.frames).ToString("0")+"% (max "+(pair.Value.worst*100).ToString("0.0")+" cm), own knee "+(100f*pair.Value.own/pair.Value.frames).ToString("0")+"% (max "+(pair.Value.own2*100).ToString("0.0")+" cm)";
            }
        }
        // A NativeAccess as Verify would make it (reflection: its setters are private), as HatLookChecks builds one.
        private static NativeAccess Access(bool developer,double expires,params string[] grants)
        {
            var a=(NativeAccess)Activator.CreateInstance(typeof(NativeAccess),true);var t=typeof(NativeAccess);
            t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
            var set=(HashSet<string>)t.GetField("grants",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(a);foreach(var g in grants)set.Add(g);return a;
        }
        private static bool Same(PilotBodyMesh a,PilotBodyMesh b)
        {
            if(a.Parts.Length!=b.Parts.Length)return false;
            for(int i=0;i<a.Parts.Length;i++){var x=a.Parts[i].Mesh;var y=b.Parts[i].Mesh;if(!x.Positions.SequenceEqual(y.Positions)||!x.Normals.SequenceEqual(y.Normals)||!x.Colors.SequenceEqual(y.Colors)||!x.Emission.SequenceEqual(y.Emission)||!x.Indices.SequenceEqual(y.Indices))return false;}
            return true;
        }
        private static string Hash(string s){using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Take(6).Select(b=>b.ToString("x2")));}
    }
}
#endif
