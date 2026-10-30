#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The robot suit (Core/NativeRobotSuit; owner 2026-10-30: "design clothing that resembles our previous player model (Robotic
    // style) and reference the previous face"): its pieces built on their bones, faces turned out, each side the other's mirror,
    // the plates over the torso clear of the under-suit's measured outline, painted in the body colour with the accent's light
    // and dark metal, within its triangle budget; the under-suit put in place of a pilot's clothes, its hair and skin kept; its
    // pick in the skin map's "suit" entry and the Locker's OUTFIT section; and the runtime's wiring (the live body, the draft's
    // seat and a corpse). The fit to the pack's
    // body was checked in Unity and over its dumped rest pose on the owner's PC (DEV_LOG).
    internal static class RobotSuitChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Robot suit: "+what);count++;}
        public static void Run(string root)
        {
            var parts=NativeRobotSuit.Build();
            Check(NativeRobotSuit.Bones.Length==12&&NativeRobotSuit.Bones.Distinct().Count()==12&&parts.All(p=>p.Bone>=0&&p.Bone<12),"every piece on one of the suit's twelve bones");
            int faces=0,wrong=0;bool finite=true;
            foreach(var s in parts)
            {
                finite&=s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z));
                for(int i=0;i<s.Indices.Count;i+=3){int a=s.Indices[i],b=s.Indices[i+1],c=s.Indices[i+2];var n=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]);faces++;if(Vector3.Dot(n,s.Normals[a]+s.Normals[b]+s.Normals[c])<0)wrong++;}
            }
            Check(finite&&wrong==0&&faces>4000&&faces<14000,"built ("+faces+" triangles, "+wrong+" turned in), within the budget");
            string[] pieces={"chest","back","belt","collar","ears","pauldron L","pauldron R","vambrace L","vambrace R","thigh L","thigh R","knee L","knee R","shin L","shin R"};
            Check(pieces.All(n=>parts.Count(p=>p.Part==n)==1),"its pieces: chest and back plates, belt, collar, ear modules, pauldrons, vambraces, thigh plates, knee caps, shin guards");
            Check(parts.Single(p=>p.Part=="back").Bone==NativeRobotSuit.Chest&&parts.Single(p=>p.Part=="belt").Bone==NativeRobotSuit.Hips&&parts.Single(p=>p.Part=="ears").Bone==NativeRobotSuit.Head
                &&parts.Single(p=>p.Part=="pauldron L").Bone==NativeRobotSuit.ArmL&&parts.Single(p=>p.Part=="pauldron R").Bone==NativeRobotSuit.ArmR&&parts.Single(p=>p.Part=="knee L").Bone==NativeRobotSuit.ShinL
                &&NativeRobotSuit.Bones[NativeRobotSuit.ArmL]=="upperarm_l"&&NativeRobotSuit.Bones[NativeRobotSuit.ShinR]=="calf_r","each piece on its bone, the left on the left");
            // Mirrored sides (the right vambrace stops short of the wrist device, so only its plates' bearings are compared).
            foreach(var n in new[]{"pauldron","thigh","knee","shin"})
            {
                var l=parts.Single(p=>p.Part==n+" L");var r=parts.Single(p=>p.Part==n+" R");
                Check(l.Positions.Count==r.Positions.Count&&Enumerable.Range(0,l.Positions.Count).All(i=>Vector3.Distance(r.Positions[i],new Vector3(-l.Positions[i].X,l.Positions[i].Y,l.Positions[i].Z))<1e-5f)
                    &&l.Positions.Average(p=>p.X)<0,n+": the right the left's mirror, the left on the body's left (-x)");
            }
            var vl=parts.Single(p=>p.Part=="vambrace L");var vr=parts.Single(p=>p.Part=="vambrace R");
            Check(vl.Positions.Min(p=>p.Y)<vr.Positions.Min(p=>p.Y)-.02f,"the right vambrace stops short of the wrist device");
            // The torso's plates clear the under-suit's measured outline by 4 mm or more (their inner faces at the gap).
            foreach(var n in new[]{"chest","back","belt"})
            {
                var s=parts.Single(p=>p.Part==n);int close=0,total=0;
                foreach(var p in s.Positions)
                {
                    NativeRobotSuit.Torso(0,p.Y,out float z);float th=MathF.Atan2(p.X,p.Z-z),reach=NativeRobotSuit.Torso(th,p.Y,out _),r=MathF.Sqrt(p.X*p.X+(p.Z-z)*(p.Z-z));
                    if(p.Y<1f||p.Y>1.44f)continue;total++;if(r<reach+.004f)close++;
                }
                Check(total>50&&close==0,n+": clear of the under-suit ("+close+" of "+total+" points within 4 mm)");
            }
            Check(Math.Abs(NativeRobotSuit.Torso(.7f,1.3f,out _)-NativeRobotSuit.Torso(-.7f,1.3f,out _))<1e-6f&&NativeRobotSuit.Torso(0,1.3f,out float z0)<NativeRobotSuit.Torso(MathF.PI/2,1.3f,out _)&&z0>0,"the torso's outline even side to side, wider than deep");
            // Paints: the shell in the body colour, the light in the accent, dark metal untinted.
            var tags=parts.SelectMany(p=>p.Emission).Select(e=>(int)MathF.Round(e.W)).Distinct().ToList();
            Check(tags.Contains(0)&&tags.Contains(1)&&tags.Contains(2)&&!tags.Contains(3),"its shell tinted by the body colour, its light by the accent, its metal plain");
            Check(parts.Single(p=>p.Part=="back").Emission.Count(e=>e.W==2)>20&&parts.Single(p=>p.Part=="belt").Emission.Count(e=>e.W==2)>50,"the reactor's cross and the waist band alight");
            // The under-suit in place of the clothes, the pilot's hair and skin kept.
            string worn=NativeRobotSuit.Look("hair:wild,upper:"+NativeSheldonWear.Robe+",lower:baggy-pants,feet:sneakers,skin:aa8866");
            Check(NativeLook.Of(worn,NativeLook.Hair)=="wild"&&NativeLook.Of(worn,NativeLook.Upper)=="sweater"&&NativeLook.Of(worn,NativeLook.Lower)=="skinny-jeans"&&NativeLook.Of(worn,NativeLook.Hands)=="gloves"
                &&NativeLook.Of(worn,NativeLook.Feet)=="work-boots"&&NativeLook.SkinOf(worn)==0xaa8866,"the under-suit worn over the pilot's own hair and skin ("+worn+")");
            Check(NativeLook.Of(NativeRobotSuit.Look(""),NativeLook.Hair)=="spiky"&&NativeLook.SkinOf(NativeRobotSuit.Look(""))<0&&NativeRobotSuit.Look(null)==NativeRobotSuit.Look(""),"the default pilot's under-suit");
            // The runtime.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime",f)).Replace("\r\n","\n");
            string suit=R("NativeRobotSuitPresentation.cs"),body=R("NativeVisualBodyPresentation.cs"),bank=R("NativeSourceModels.cs");
            Check(bank.Contains("MachineSkins();ModelHats();SuitModels();")&&suit.Contains("models[Suit]=parts;"),"the suit in the bank");
            Check(suit.Contains("private bool WearsSuit(int id)=>NativeWeaponSkins.Index(SeatWeaponSkins(id==ViewBodyId?LocalId:id),NativeWeaponSkins.SuitEntry)==NativeWeaponSkins.SuitRobot;")
                &&body.Contains("private string VisualLookOf(int id){var look=VisualOutfitOf(id);return WearsSuit(id)?NativeRobotSuit.Look(look):look;}"),"the skin map's suit entry wears it (the first-person arms the local pilot's), its under-suit in place of the clothes");
            // Picked in the skin map's "suit" entry (none by default, never written), set from the Locker's OUTFIT section.
            Check(NativeWeaponSkins.SuitKeys.SequenceEqual(new[]{"none","robot"})&&NativeWeaponSkins.Normalize("suit:robot")=="suit:robot"&&NativeWeaponSkins.Normalize("suit:none")==""&&NativeWeaponSkins.Normalize("suit:prototype")==""
                &&NativeWeaponSkins.Normalize("suit:robot,face:mask,body:prototype")=="body:prototype,face:mask,suit:robot"&&NativeWeaponSkins.Index("body:prototype",NativeWeaponSkins.SuitEntry)==NativeWeaponSkins.SuitNone
                &&NativeWeaponSkins.With("sword:prototype",NativeWeaponSkins.SuitEntry,"robot")=="sword:prototype,suit:robot"&&NativeBodySkins.Of("suit:robot")==NativeBodySkins.StandardIndex,"the suit's pick normalizes, defaults to none and leaves the body and masks alone");
            string outfit=R("NativeLockerOutfitUI.cs");
            Check(outfit.Contains("int pick=NativeOutfitPicker.Row(NativeOutfitPicker.Cell(grid,lockerW,row),row,NativeOutfitPicker.SuitName,NativeWeaponSkins.SuitKeys,NativeWeaponSkins.SuitLabels,suit,")
                &&outfit.Contains("if(pick!=suit){prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,NativeWeaponSkins.SuitEntry,NativeWeaponSkins.SuitKeys[pick]);Click();}")&&outfit.Contains("\"outfit-suit\""),"a SUIT row under the outfit's slots picks it");
            Check(suit.Contains("suitMatrices[i]=rig.Bones[i].localToWorldMatrix*rig.Bind[i];")&&suit.Contains("CapturedRig.Chest,CapturedRig.Pelvis,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee"),
                "each piece on its bone through the bind pose, a pilot's reveal and tint by the joint it answers to");
            Check(body.Contains("VisualSuit(body.Root,seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0);")&&body.Contains("if(!first)VisualSuit(body.Root,a.Id,a.BodyColor,a.AccentColor,layer,camera,reveal,tint,flash);")
                &&body.Contains("VisualSuit(c.Root,corpse.Death.id,corpse.Death.actor?.BodyColor,")&&body.Contains("if(!first&&!WearsSuit(a.Id))"),"worn by the live body, the draft's seat and a corpse, the back emblem under the back plate");
            Console.WriteLine("PASS robot suit ("+count+" checks)");
        }
    }
}
#endif
