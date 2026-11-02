#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The robot suit (Core/NativeRobotSuit; owner 2026-10-30: "design clothing that resembles our previous player model (Robotic
    // style) and reference the previous face"; four clothing pieces since owner 2026-11-01: "Can we make the armor portion into
    // clothing? Like separate them like we have other clothes currently."): its plates built on their bones, faces turned out,
    // each side the other's mirror, the plates over the torso, the gloves and the boots clear of their measured outlines,
    // painted in the body colour with the accent's light and dark metal, within its triangle budget; the four pieces each its
    // slot's default, each worn over its garment with the pilot's other pieces, hair and skin kept, each slot's plates shown
    // with its piece (the ear modules not under the helmet, the vambraces not under arm bands); and the runtime's wiring (the
    // live body, the draft's seat and a corpse; the Locker's OUTFIT rows). The fit to the pack's body was checked in Unity and
    // over its dumped rest pose on the owner's PC (DEV_LOG).
    internal static class RobotSuitChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Robot suit: "+what);count++;}
        public static void Run(string root)
        {
            var parts=NativeRobotSuit.Build();int bones=NativeRobotSuit.Bones.Length;
            Check(bones==16&&NativeRobotSuit.Bones.Distinct().Count()==16&&parts.All(p=>p.Bone>=0&&p.Bone<16)&&NativeRobotSuit.BoneSlot.Length==16,"every piece on one of the suit's sixteen bones, each bone a slot's");
            int faces=0,wrong=0;bool finite=true;
            foreach(var s in parts)
            {
                finite&=s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z));
                for(int i=0;i<s.Indices.Count;i+=3){int a=s.Indices[i],b=s.Indices[i+1],c=s.Indices[i+2];var n=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]);faces++;if(Vector3.Dot(n,s.Normals[a]+s.Normals[b]+s.Normals[c])<0)wrong++;}
            }
            Check(finite&&wrong==0&&faces>4000&&faces<20000,"built ("+faces+" triangles, "+wrong+" turned in), within the budget");
            string[] pieces={"chest","back","belt","collar","ears","pauldron L","pauldron R","vambrace L","vambrace R","thigh L","thigh R","knee L","knee R","shin L","shin R",
                "gauntlet L","gauntlet R","toe cap L","toe cap R","heel guard L","heel guard R"};
            Check(pieces.All(n=>parts.Count(p=>p.Part==n)==1)&&parts.Length==pieces.Length,"its pieces: chest and back plates, belt, collar, ear modules, pauldrons, vambraces, thigh plates, knee caps, shin guards, gauntlets, toe caps, heel guards");
            Check(parts.Single(p=>p.Part=="back").Bone==NativeRobotSuit.Chest&&parts.Single(p=>p.Part=="belt").Bone==NativeRobotSuit.Hips&&parts.Single(p=>p.Part=="ears").Bone==NativeRobotSuit.Head
                &&parts.Single(p=>p.Part=="pauldron L").Bone==NativeRobotSuit.ArmL&&parts.Single(p=>p.Part=="pauldron R").Bone==NativeRobotSuit.ArmR&&parts.Single(p=>p.Part=="knee L").Bone==NativeRobotSuit.ShinL
                &&parts.Single(p=>p.Part=="gauntlet R").Bone==NativeRobotSuit.HandR&&parts.Single(p=>p.Part=="toe cap L").Bone==NativeRobotSuit.FootL&&parts.Single(p=>p.Part=="heel guard R").Bone==NativeRobotSuit.FootR
                &&NativeRobotSuit.Bones[NativeRobotSuit.ArmL]=="upperarm_l"&&NativeRobotSuit.Bones[NativeRobotSuit.ShinR]=="calf_r"&&NativeRobotSuit.Bones[NativeRobotSuit.HandL]=="hand_l"
                &&NativeRobotSuit.Bones[NativeRobotSuit.FootR]=="foot_r","each piece on its bone, the left on the left (the toe cap on the foot's, which the stiff boot's toe box follows)");
            // Mirrored sides (the right vambrace stops short of the wrist device, so only its plates' bearings are compared).
            foreach(var n in new[]{"pauldron","thigh","knee","shin","gauntlet","toe cap","heel guard"})
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
            // The gauntlets, toe caps and heel guards clear the gloves' backs, the toes and the heels as measured (2 mm and more;
            // the toe cap 4 % and more out of the ellipsoid that holds the toe; the heel guard's sides a little nearer, where the
            // heel narrows).
            float glove=parts.Single(p=>p.Part=="gauntlet L").Positions.Min(NativeRobotSuit.OverGlove),toe=parts.Single(p=>p.Part=="toe cap L").Positions.Min(NativeRobotSuit.ToeReach),
                heel=parts.Single(p=>p.Part=="heel guard L").Positions.Min(NativeRobotSuit.OffHeel);
            Check(glove>.002f&&glove<.006f&&toe>1.04f&&toe<1.15f&&heel>.0003f&&heel<.004f,"the hands' and feet's plates clear the gloves and boots (glove "+glove+", toe "+toe+", heel "+heel+")");
            Check(parts.Single(p=>p.Part=="heel guard L").Positions.Max(p=>p.Y)<.075f,"the heel guards under the khakis' hem");
            // Paints: the shell in the body colour, the light in the accent, dark metal untinted.
            var tags=parts.SelectMany(p=>p.Emission).Select(e=>(int)MathF.Round(e.W)).Distinct().ToList();
            Check(tags.Contains(0)&&tags.Contains(1)&&tags.Contains(2)&&!tags.Contains(3),"its shell tinted by the body colour, its light by the accent, its metal plain");
            Check(parts.Single(p=>p.Part=="back").Emission.Count(e=>e.W==2)>20&&parts.Single(p=>p.Part=="belt").Emission.Count(e=>e.W==2)>50
                &&new[]{"gauntlet L","toe cap L","heel guard L"}.All(n=>parts.Single(p=>p.Part==n).Emission.Count(e=>e.W==2)>8),"the reactor's cross, the waist band and the hands' and feet's strips alight");
            // The four pieces: each slot's default, worn over its garment, the pilot's other pieces, hair and skin kept.
            for(int s=1;s<NativeLook.Slots.Length;s++)
                Check(NativeLook.Ids[s][0].StartsWith("robot-",StringComparison.Ordinal)&&NativeLook.Labels[s][0].StartsWith("Robot ",StringComparison.Ordinal)&&NativeLook.Ids[s].Count(id=>id.StartsWith("robot-",StringComparison.Ordinal))==1
                    &&Array.IndexOf(NativeLook.Ids[s],NativeRobotSuit.Under[s])>0&&!NativeLook.DevOnly(NativeRobotSuit.Under[s])&&NativeRobotSuit.Worn("",s)&&NativeRobotSuit.BoneSlot.Contains(s),
                    NativeLook.Slots[s]+": the robot's piece first (the default), over a garment of its own slot");
            Check(!NativeRobotSuit.Worn("",0)&&NativeLook.Ids[0].All(id=>!id.StartsWith("robot",StringComparison.Ordinal))&&NativeLook.Normalize("upper:robot-armour,feet:robot-boots")==""
                &&NativeLook.With("upper:tee",NativeLook.Upper,"robot-armour")==""&&NativeLook.Labels[1][0]=="Robot armour"&&NativeLook.Labels[2][0]=="Robot legs"&&NativeLook.Labels[3][0]=="Robot gauntlets"&&NativeLook.Labels[4][0]=="Robot boots",
                "the robot armour, legs, gauntlets and boots, never written (the hair has none)");
            Check(NativeRobotSuit.Dress("")=="upper:sweater,lower:skinny-jeans,hands:gloves,feet:work-boots"&&NativeRobotSuit.Dress(null)==NativeRobotSuit.Dress(""),"the default pilot dressed in every garment under its pieces ("+NativeRobotSuit.Dress("")+")");
            string worn=NativeRobotSuit.Dress("hair:wild,upper:hoodie,feet:sneakers,skin:aa8866");
            Check(worn=="hair:wild,upper:hoodie,lower:skinny-jeans,hands:gloves,feet:sneakers,skin:aa8866","the pieces mixed with the pilot's own, its hair and skin kept ("+worn+")");
            Check(NativeRobotSuit.Dress(NativeLook.Concept)==NativeLook.Concept&&NativeLook.Normalize(NativeLook.Concept)==NativeLook.Concept&&NativeRobotSuit.Shown(NativeLook.Concept,false)==0,"the concept's outfit wears no robot piece");
            int all=(1<<bones)-1,head=1<<NativeRobotSuit.Head,fore=1<<NativeRobotSuit.ForearmL|1<<NativeRobotSuit.ForearmR,hands=1<<NativeRobotSuit.HandL|1<<NativeRobotSuit.HandR;
            int Of(int slot){int m=0;for(int b=0;b<bones;b++)if(NativeRobotSuit.BoneSlot[b]==slot)m|=1<<b;return m;}
            Check(NativeRobotSuit.Shown("",false)==all&&NativeRobotSuit.Shown("",true)==(all&~head)&&NativeRobotSuit.Shown("upper:tee",false)==(all&~Of(1))&&NativeRobotSuit.Shown("lower:khakis,feet:sneakers",false)==(Of(1)|Of(3))
                &&NativeRobotSuit.Shown("hands:arm-bands",false)==(all&~fore&~hands)&&NativeRobotSuit.Shown("hands:sweat-bands",true)==(all&~head&~hands)&&NativeRobotSuit.Shown("upper:tee,lower:khakis,hands:bare,feet:mid-tops",false)==0
                &&(Of(1)>>NativeRobotSuit.Chest&1)==1&&(Of(4)&(1<<NativeRobotSuit.FootR))!=0&&(Of(4)&(1<<NativeRobotSuit.FootL))!=0,"each piece's plates shown with it, the ear modules under the helmet and the vambraces under arm bands left off");
            // The runtime.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime",f)).Replace("\r\n","\n");
            string suit=R("NativeRobotSuitPresentation.cs"),body=R("NativeVisualBodyPresentation.cs"),bank=R("NativeSourceModels.cs"),skins=R("NativeWeaponSkinPresentation.cs"),outfit=R("NativeLockerOutfitUI.cs");
            Check(bank.Contains("MachineSkins();ModelHats();SuitModels();")&&suit.Contains("models[Suit]=parts;"),"the suit in the bank");
            Check(suit.Contains("private int SuitShown(int id)=>NativeRobotSuit.Shown(VisualOutfitOf(id),HelmEars(id));")&&body.Contains("robotDressed[look]=dressed=NativeRobotSuit.Dress(look);")
                &&body.Contains("if(id<RooftopIdTop&&id>RooftopIdTop-8)return NativeLook.Concept;")&&skins.Contains("private const string RooftopCrowdSkins=\"face:none\";"),
                "the outfit's pieces worn (the first-person arms the local pilot's), each over its garment, the rooftop crowd in the concept's clothes");
            // The skin map's "suit" entry is gone (a saved one dropped); the Locker's OUTFIT rows are the slots' alone.
            Check(!NativeWeaponSkins.Entries.Contains("suit")&&NativeWeaponSkins.Normalize("suit:none,face:mask")=="face:mask"&&NativeWeaponSkins.Normalize("suit:robot")=="",
                "the old suit entry dropped from the skin map");
            Check(outfit.Contains("private float LkOutfitH()=>NativeOutfitPicker.Height(lockerW,NativeLook.Slots.Length);")&&!outfit.Contains("SuitEntry")&&!outfit.Contains("\"outfit-suit\""),"the OUTFIT rows the slots'");
            Check(suit.Contains("suitMatrices[i]=rig.Bones[i]!=null?rig.Bones[i].localToWorldMatrix*rig.Bind[i]:Matrix4x4.identity;")&&suit.Contains("if(rig==null||(shown&=rig.Found)==0)return;")
                &&suit.Contains("CapturedRig.Chest,CapturedRig.Pelvis,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee,")
                &&suit.Contains("CapturedRig.LHand,CapturedRig.RHand,CapturedRig.LFoot,CapturedRig.RFoot};"),
                "each piece on its bone through the bind pose (one the body lacks left off), a pilot's reveal and tint by the joint it answers to");
            Check(body.Contains("VisualSuit(body.Root,seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0);")&&body.Contains("if(!first)VisualSuit(body.Root,a.Id,a.BodyColor,a.AccentColor,layer,camera,reveal,tint,flash);")
                &&body.Contains("VisualSuit(c.Root,corpse.Death.id,body,accent,0,null,alpha<1?corpseFadeOf??=_=>corpseFade:null,null,0,VisualCorpseSuit(corpse));")&&body.Contains("if(c.Look!=1&&corpse.Death!=null)")&&body.Contains("if(!first&&!WearsArmour(a.Id))"),"worn by the live body, the draft's seat and a corpse (split, crushed and frozen ones too; not the ghost look), the back emblem under the armour's back plate");
            Console.WriteLine("PASS robot suit ("+count+" checks)");
        }
    }
}
#endif
