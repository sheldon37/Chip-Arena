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
    // the display stocks every rarity; the chip lights both faces in its tint; budgets hold; the draws use the sculpted ids
    // with the exported ones as the fallback, and the exported models stay in the bank.
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
            Check(pres.Contains("*.27f,1.02f,")&&NativeMachineModels.Slot==new Vector3(0,1.02f,.27f),"the chip leaves the slot the model draws");
            // The chip slot sits under the flap and in front of the display; the display stocks every rarity.
            var chip=NativeMachineModels.Build(NativeMachineModels.Chip);var lights=chip.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Emission).ToList();
            foreach(int hex in NativeMachineModels.RarityColors){var c=NativePortraitRules.Linear(hex);Check(lights.Any(v=>Vector3.Distance(Norm(new Vector3(v.X,v.Y,v.Z)),Norm(c))<.02f),"a rarity lamp or stock die in #"+hex.ToString("x6"));}
            // Deny reads red: every body keeps pale lights (the deny tint keeps red only).
            foreach(var kind in NativeMachineModels.Kinds){var b=NativeMachineModels.Build(kind);Check(b.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Emission).Any(v=>v.X>.5f),kind+" has a light with a red share for the deny flash");}
            // The dispensed chip: centred, thin, a die on both faces in the tint, gold leads.
            var pickup=NativeMachineModels.Pickup();Clean(pickup,"chip");Check(Same(pickup,NativeMachineModels.Pickup()),"chip builds the same twice");
            pickup.Bounds(-1,out var cmin,out var cmax);
            Check(Math.Abs(cmin.X+cmax.X)<.01f&&Math.Abs(cmin.Z+cmax.Z)<.01f&&Math.Abs(cmin.Y+cmax.Y)<.01f&&cmax.X<=.56f&&cmax.Y<=.1f,"the chip is centred and thin ("+cmin+" .. "+cmax+")");
            var die=pickup.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Positions).ToList();
            Check(die.Any(p=>p.Y>.07f)&&die.Any(p=>p.Y<-.07f),"the chip lights both faces");
            Check(pickup.Sections.Where(s=>s.Bone==NativeMachineModels.Lights).SelectMany(s=>s.Colors).All(c=>Math.Abs(c.X-c.Y)<1e-4f&&Math.Abs(c.Y-c.Z)<1e-4f),"the chip's lights are neutral (the tint is the rarity)");
            Check(pickup.Sections.Select(s=>s.Bone).Distinct().OrderBy(b=>b).SequenceEqual(new[]{0,1})&&pickup.Triangles<=3200,"the chip keeps bones 0 and 1 within budget ("+pickup.Triangles+")");
            // Runtime: the bank builds them, the draws prefer them, the exported models stay.
            var bank=R("Assets/ChipArena/Runtime/NativeSourceModels.cs");var reg=R("Assets/ChipArena/Runtime/NativeMachineModels.cs");var draft=R("Assets/ChipArena/Runtime/NativeDraftPresentation.cs");
            Check(bank.Contains("HordeKindModels();MachineSkins();")&&reg.Contains("NativeMachineModels.Build(kind)")&&reg.Contains("NativeMachineModels.Pickup()")&&reg.Contains("NativeSourceTransparent"),"the bank builds the sculpted models (glass see-through)");
            Check(pres.Contains("MachineModel(NativeMachineModels.BodyId(m.kind),\"machine-\"+m.kind)")&&pres.Contains("MachineModel(NativeMachineModels.MovingId(m.kind),\"machine-moving-\"+m.kind)")&&pres.Contains("MachineModel(NativeMachineModels.PickupId,\"pve-pickup\")"),"the machine draws prefer the sculpted models");
            Check(draft.Contains("MachineModel(NativeMachineModels.PickupId,\"pve-pickup\")"),"the draft tray's chip is the sculpted chip");
            Check(!reg.Contains("Update(")&&!reg.Contains("LateUpdate"),"built once at load, nothing per frame");
            Console.WriteLine("PASS machine models ("+count+" checks: chip dispenser, recruit pod and med station sculpted inside their colliders with the bank's tinted-light part, the flap, door and pulse about the presentation's pivots, see-through glass and pulse, every rarity stocked, red-capable lights for the deny flash, the dispensed chip lit on both faces in its tint, budgets, deterministic builds, and the draws preferring the sculpted models with the exported ones kept as the fallback)");
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
