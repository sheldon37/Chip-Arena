#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The sculpted PvE machines and dispensed chip (Core NativeMachineModels, Runtime NativeMachineModels): every model builds
    // deterministically with clean geometry; the bodies keep the bank's part convention (0 the body, 1 the tinted lights) and
    // stay inside the machine collider (PveContent machineBlocks) apart from the allowed front trim and crown; the moving parts
    // keep the source pivots the presentation turns them about; see-through sections carry partial alpha and nothing else does;
    // the display stocks every rarity; the chip (a cartridge per tier) lights both faces in its tint and wears its tier's
    // ornaments; budgets hold; the draws use the sculpted ids with the exported ones as the fallback, and the exported models
    // stay in the bank.
    internal static class MachineModelChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Machine models: "+what);count++;}
        public static void Run(string root)
        {
            count=0;var R=new Func<string,string>(f=>File.ReadAllText(Path.Combine(root,f)));
            foreach(var kind in NativeMachineModels.Kinds)
            {
                var body=NativeMachineModels.Build(kind);var moving=NativeMachineModels.Moving(kind);
                Check(body!=null&&moving!=null,kind+" builds");Clean(body,kind);Clean(moving,kind+" moving");
                Check(Same(body,NativeMachineModels.Build(kind))&&Same(moving,NativeMachineModels.Moving(kind)),kind+" builds the same twice");
                var bones=body.Sections.Select(s=>s.Bone).Distinct().OrderBy(b=>b).ToArray();
                Check(bones.Contains(NativeMachineModels.Body)&&bones.Contains(NativeMachineModels.Lights)&&bones.All(b=>b==NativeMachineModels.Body||b==NativeMachineModels.Lights||b==NativeMachineModels.Glass&&kind==NativeMachineModels.Chip),kind+" bones "+string.Join(",",bones));
                Check(moving.Sections.All(s=>s.Bone==-1||s.Bone==NativeMachineModels.Glass),kind+" moving parts ride the root");
                // Collider: the hidden machineBlocks box, with the allowed front trim and crown.
                body.Bounds(-1,out var min,out var max);var lo=NativeMachineModels.BlockMin;var hi=NativeMachineModels.BlockMax;const float e=.001f;
                Check(min.X>=lo.X-e&&max.X<=hi.X+e&&min.Z>=lo.Z-e&&min.Y>=-e&&max.Z<=hi.Z+NativeMachineModels.FrontAllowance&&max.Y<=hi.Y+NativeMachineModels.TopAllowance,kind+" inside its collider ("+min+" .. "+max+")");
                Check(max.Y>=hi.Y-.15f&&max.X>=hi.X-.05f&&min.X<=lo.X+.05f,kind+" fills its collider (no gap players would walk into)");
                Check(body.Triangles>=1500&&body.Triangles<=7000&&moving.Triangles<=800,kind+" budget ("+body.Triangles+", "+moving.Triangles+")");
                Alpha(body,kind);Alpha(moving,kind+" moving");
            }
            // The moving parts about the presentation's pivots (NativeMachinePresentation turns them there).
            NativeMachineModels.Moving(NativeMachineModels.Chip).Bounds(-1,out var fmin,out var fmax);
            Check(fmax.Y<=.001f&&fmin.Y>=-.18f&&Math.Abs(fmin.X+fmax.X)<.01f&&fmax.X<=.27f&&Math.Abs(fmax.Z)<.04f,"the flap hangs under its hinge ("+fmin+" .. "+fmax+")");
            NativeMachineModels.Moving(NativeMachineModels.Recruit).Bounds(-1,out var dmin,out var dmax);
            Check(dmax.X<=.001f&&dmin.X>=-.99f&&dmin.Y>=.2f&&dmax.Y<=2.1f&&Math.Abs(dmax.Z)<.06f,"the door spans its hinge to its leading edge ("+dmin+" .. "+dmax+")");
            NativeMachineModels.Moving(NativeMachineModels.Med).Bounds(-1,out var pmin,out var pmax);
            Check(Math.Abs(pmin.Z)<1e-4f&&Math.Abs(pmax.Z)<1e-4f&&pmax.X<=.45f&&Math.Abs(pmin.X+pmax.X)<.01f,"the pulse is a flat ring about its centre");
            Check(NativeMachineModels.Moving(NativeMachineModels.Med).Sections.All(s=>s.Bone==NativeMachineModels.Glass),"the pulse is all see-through (the draw fades it)");
            var pres=R("Assets/ChipArena/Runtime/NativeMachinePresentation.cs");
            Check(pres.Contains("new Vector3(0,1.12f,.27f)")&&pres.Contains("new Vector3(.49f,0,.22f)")&&pres.Contains("new Vector3(0,1.55f,.2f)")&&
                NativeMachineModels.FlapPivot==new Vector3(0,1.12f,.27f)&&NativeMachineModels.DoorPivot==new Vector3(.49f,0,.22f)&&NativeMachineModels.PulsePivot==new Vector3(0,1.55f,.2f),"pivots match the presentation");
            var slotAt=NativeMachineRules.Slot(new PveMachine{x=0,y=0,z=0,yaw=0});Check(pres.Contains("var from=NativeMachineRules.Slot(machine);")&&NativeMachineModels.Slot==new Vector3(0,1.02f,.27f)&&slotAt.x==0&&slotAt.y==1.02f&&slotAt.z==.27f,"the chip leaves the slot the model draws");
            // The chip slot sits under the flap and in front of the display; the display stocks every rarity.
            var chip=NativeMachineModels.Build(NativeMachineModels.Chip);var lights=chip.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Emission).ToList();
            foreach(int hex in NativeMachineModels.RarityColors){var c=NativePortraitRules.Linear(hex);Check(lights.Any(v=>Vector3.Distance(Norm(new Vector3(v.X,v.Y,v.Z)),Norm(c))<.02f),"a rarity lamp or stock die in #"+hex.ToString("x6"));}
            // Deny reads red: every body keeps pale lights (the deny tint keeps red only).
            foreach(var kind in NativeMachineModels.Kinds){var b=NativeMachineModels.Build(kind);Check(b.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Emission).Any(v=>v.X>.5f),kind+" has a light with a red share for the deny flash");}
            // The dispensed / held chip: the neural core cartridge, one model per tier (Core/NativeChipCartridge.cs). Each builds the
            // same twice with clean geometry; it is centred on its spin and stays a slim cartridge (its body .8 wide, the hand's
            // PickupWidth; fins inside the old leads' reach; under a quarter as thick as wide); the core's crystal, inlay, traces
            // and the edge band light both faces in neutral colours (the draw tints them with the rarity); the glass domes are
            // see-through; the ring (bone 2) belongs to Epic and up; budgets hold; the tiers differ by ornament (the crest rises
            // over the top edge, the fins widen it, the gold reads gold) as well as by tint.
            var common=NativeMachineModels.Pickup("common");common.Bounds(-1,out var c0,out var c1);float commonTall=c1.Z-c0.Z;
            Check(Same(NativeMachineModels.Pickup(),common)&&NativeMachineModels.PickupModel("common")==NativeMachineModels.PickupId&&NativeMachineModels.PickupModel("bogus")==NativeMachineModels.PickupId,"the common cartridge is the fallback model");
            foreach(var tier in NativeMachineModels.PickupTiers)
            {
                var look=NativeChipCartridge.Of(tier);var pickup=NativeMachineModels.Pickup(tier,out var core);string id=NativeMachineModels.PickupModel(tier);Clean(pickup,"chip "+tier);Alpha(pickup,"chip "+tier);
                Check(Same(pickup,NativeMachineModels.Pickup(tier)),tier+" chip builds the same twice");Check(tier=="common"||id==NativeMachineModels.PickupId+"-"+tier,tier+" chip has its own id");
                pickup.Bounds(-1,out var cmin,out var cmax);
                Check(Math.Abs(cmin.X+cmax.X)<.01f&&Math.Abs(cmin.Z+cmax.Z)<.01f&&Math.Abs(cmin.Y+cmax.Y)<.01f&&cmax.X<=.56f&&cmax.Y<=.115f,tier+" chip is centred and slim ("+cmin+" .. "+cmax+")");
                Check(cmax.Y<(cmax.X*2)*.25f,tier+" chip under a quarter as thick as wide");
                var lit=pickup.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Positions).ToList();
                Check(lit.Any(p=>p.Y>.07f)&&lit.Any(p=>p.Y<-.07f),tier+" chip lights both faces");
                Check(pickup.Sections.Where(s=>s.Bone==NativeMachineModels.Lights||s.Bone==NativeMachineModels.Orbit).SelectMany(s=>s.Colors).All(c=>Math.Abs(c.X-c.Y)<1e-4f&&Math.Abs(c.Y-c.Z)<1e-4f),tier+" chip's lights are neutral (the tint is the rarity)");
                var bones=pickup.Sections.Select(s=>s.Bone).Distinct().OrderBy(b=>b).ToArray();var want=look.Ring?new[]{NativeMachineModels.Glass,0,1,NativeMachineModels.Orbit}:new[]{NativeMachineModels.Glass,0,1};
                Check(bones.SequenceEqual(want),tier+" chip bones "+string.Join(",",bones));
                Check(pickup.Triangles<=(look.Ornaments>=4?3600:3200),tier+" chip budget ("+pickup.Triangles+")");
                // The ring's pivot is the core: inside the chip, above its middle (the core sits in the upper half), on the face plane.
                Check(Math.Abs(core.X)<1e-3f&&Math.Abs(core.Y)<1e-3f&&core.Z>.02f&&core.Z<cmax.Z,tier+" ring pivot at the core ("+core+")");
                if(look.Ring){pickup.Bounds(NativeMachineModels.Orbit,out var omin,out var omax);var mid=(omin+omax)*.5f;Check(Math.Abs(mid.X-core.X)<.01f&&Math.Abs(mid.Z-core.Z)<.01f,tier+" ring centred on its pivot");}
                // Ornament by shape: the crest rises over the common chip's top, the fins widen it.
                bool crest=look.Crest!=NativeChipCartridge.Crest.None;Check(!crest||cmax.Z-cmin.Z>commonTall+.04f,tier+" crest rises over the top edge");
                Check(look.Fins?cmax.X>.45f:cmax.X<=.43f,tier+" fins "+(look.Fins?"widen it":"absent")+" ("+cmax.X+")");
                if(look.Metal==NativeChipCartridge.Metal.Gold)Check(pickup.Sections.Where(s=>s.Bone==NativeMachineModels.Body).SelectMany(s=>s.Colors).Count(c=>c.X>c.Y*1.4f&&c.Y>c.Z*1.6f&&c.X>.4f)>200,"legendary chip is gold");
            }
            // Runtime: the bank builds them, the draws prefer them, the exported models stay.
            var bank=R("Assets/ChipArena/Runtime/NativeSourceModels.cs");var reg=R("Assets/ChipArena/Runtime/NativeMachineModels.cs");var draft=R("Assets/ChipArena/Runtime/NativeDraftPresentation.cs");
            Check(bank.Contains("HordeKindModels();MachineSkins();")&&reg.Contains("NativeMachineModels.Build(kind)")&&reg.Contains("NativeMachineModels.Pickup(tier,out var core)")&&reg.Contains("foreach(var tier in NativeMachineModels.PickupTiers)")&&reg.Contains("NativeSourceTransparent"),"the bank builds the sculpted models (glass see-through), a chip per tier");
            Check(pres.Contains("MachineModel(NativeMachineModels.BodyId(m.kind),\"machine-\"+m.kind)")&&pres.Contains("MachineModel(NativeMachineModels.MovingId(m.kind),\"machine-moving-\"+m.kind)")&&pres.Contains("MachineModel(NativeMachineModels.PickupModel(k.Tier),\"pve-pickup\")"),"the machine draws prefer the sculpted models, the dispensed chip its tier's");
            Check(pres.Contains("sourceModels.PickupOrbit(model,t)")&&pres.Contains("b==NativeMachineModels.Orbit?orbit:root")&&pres.Contains("NativeChipCartridge.Breath("),"the dispensed chip's ring orbits and its core breathes");
            Check(draft.Contains("MachineModel(NativeMachineModels.PickupModel(chipTier),\"pve-pickup\")")&&draft.Contains("chosenChip?.rarity")&&draft.Contains("b==NativeMachineModels.Orbit?draftChipOrbit:draftChip"),"the draft tray's chip is the chosen tier's cartridge, its ring orbiting");
            Check(!reg.Contains("Update(")&&!reg.Contains("LateUpdate"),"built once at load, nothing per frame");
            Console.WriteLine("PASS machine models ("+count+" checks: chip dispenser, recruit pod and med station sculpted inside their colliders with the bank's tinted-light part, the flap, door and pulse about the presentation's pivots, see-through glass and pulse, every rarity stocked, red-capable lights for the deny flash, the dispensed chip a cartridge per tier lit on both faces in its tint with its tier's ornaments and orbiting ring, budgets, deterministic builds, and the draws preferring the sculpted models with the exported ones kept as the fallback)");
        }
        private static Vector3 Norm(Vector3 v){float m=Math.Max(v.X,Math.Max(v.Y,v.Z));return m>1e-6f?v/m:v;}
        private static void Clean(WeaponMesh mesh,string what)
        {
            foreach(var s in mesh.Sections)
            {
                Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Emission.Count&&s.Indices.Count%3==0,what+" section arrays agree");
                Check(s.Indices.All(i=>i>=0&&i<s.Positions.Count),what+" indices in range");
                Check(s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),what+" finite positions and unit normals");
                Check(s.Colors.All(c=>c.X>=0&&c.Y>=0&&c.Z>=0&&c.X<=1.0001f&&c.Y<=1.0001f&&c.Z<=1.0001f)&&s.Emission.All(v=>v.X>=0&&v.Y>=0&&v.Z>=0&&v.W==0),what+" colours in range, no emission tags (the draw tints whole parts)");
            }
        }
        private static void Alpha(WeaponMesh mesh,string what)
        {
            foreach(var s in mesh.Sections)
            {
                if(s.Bone==NativeMachineModels.Glass)Check(s.Colors.All(c=>c.W>0&&c.W<1),what+" glass is see-through");
                else Check(s.Colors.All(c=>c.W==1),what+" solid parts are opaque");
            }
        }
        private static bool Same(WeaponMesh a,WeaponMesh b)
        {
            if(a.Sections.Count!=b.Sections.Count)return false;
            for(int i=0;i<a.Sections.Count;i++){var x=a.Sections[i];var y=b.Sections[i];if(x.Bone!=y.Bone||!x.Positions.SequenceEqual(y.Positions)||!x.Indices.SequenceEqual(y.Indices)||!x.Colors.SequenceEqual(y.Colors))return false;}
            return true;
        }
    }
}
#endif
