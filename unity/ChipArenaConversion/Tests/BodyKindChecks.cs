using System;
using System.IO;
using ChipArena.UnityConversion;
// The pilot's body (Core/NativeBodyKind; Astra's brief 2026-11-24 section 1): the look map's optional "body:female" key, omitted
// for today's pilot; the presentation keeps it at its own edges until NativeLook carries it (Astra's part); the female preset is
// the pack's female directions at three levels; presentation only.
internal static class BodyKindChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Body kind: "+why);}
        // The key: absent, unknown or malformed is today's pilot; the last valid entry wins; "male" is never written.
        Check(NativeBodyKind.Of("")==NativeBodyKind.Male&&NativeBodyKind.Of(null)==NativeBodyKind.Male&&NativeBodyKind.Of("body:")==NativeBodyKind.Male&&NativeBodyKind.Of("body:robot")==NativeBodyKind.Male,"absent or unknown is today's pilot");
        Check(NativeBodyKind.Of("upper:tee,body:female")==NativeBodyKind.Female&&NativeBodyKind.Of("body:female,body:male")==NativeBodyKind.Male&&NativeBodyKind.Of("body:male,body:female")==NativeBodyKind.Female,"the last valid entry wins");
        Check(NativeBodyKind.With("",NativeBodyKind.Female)=="body:female"&&NativeBodyKind.With("body:female",NativeBodyKind.Male)==""&&NativeBodyKind.With("upper:tee",NativeBodyKind.Male)=="upper:tee","with: written for the female body only");
        // Placed after the five slots, before the costume and the skin tone; every other entry kept in its order.
        Check(NativeBodyKind.With("upper:tee,costume:voxel,skin:c48a62",NativeBodyKind.Female)=="upper:tee,body:female,costume:voxel,skin:c48a62","before the costume");
        Check(NativeBodyKind.With("hair:wild,skin:c48a62",NativeBodyKind.Female)=="hair:wild,body:female,skin:c48a62"&&NativeBodyKind.With("hair:wild",NativeBodyKind.Female)=="hair:wild,body:female","before the skin tone, else last");
        Check(NativeBodyKind.With("body:female,upper:tee,body:female",NativeBodyKind.Female)=="upper:tee,body:female","duplicates folded into one");
        string female="upper:tee,body:female,skin:c48a62";Check(ReferenceEquals(NativeBodyKind.With(female,NativeBodyKind.Female),female),"the same string when nothing changes");
        foreach(string hair in new[]{"bob","high-ponytail"})
        {
            string look="hair:"+hair+",body:female,costume:voxel,skin:c48a62";
            Check(NativeLook.Normalize("skin:C48A62,body:male,hair:"+hair+",costume:voxel,body:female,body:invalid")==look,"canonical peer and account look");
            Check(NativeLook.Gate("upper:hot-dog,"+look,false)==look,"public body and hair survive the host gate");
            Check(NativeLook.SameOutfit(look,NativeLook.WithSkin(look,0))&&!NativeLook.SameOutfit(look,NativeBodyKind.With(look,NativeBodyKind.Male)),"redress on a body change");
            Check(NativeLook.KeepSkin("",look,3)=="hair:"+hair+",body:female,skin:c48a62","schema 3 keeps local body and hair but accepts remote costume reset");
            Check(NativeLook.KeepSkin("hair:wild",look,4)=="hair:wild,skin:c48a62","schema 4 accepts remote body and hair reset");
            Check(NativeLook.KeepSkin(look,"",4)==look,"new PC accepts saved look");
        }
        // Keep: a writer's result (NativeLook drops the key today) takes the body of what it was made from.
        foreach(var slot in NativeLook.Slots)foreach(var id in NativeLook.Ids[NativeLook.SlotIndex(slot)])
        {
            string before=NativeBodyKind.With(NativeLook.With("",slot,id),NativeBodyKind.Female);
            string kept=NativeBodyKind.Keep(NativeLook.Gate(NativeLook.With(before,NativeLook.Upper,"tee"),true),before);
            Check(NativeBodyKind.Of(kept)==NativeBodyKind.Female&&NativeLook.Of(kept,NativeLook.Upper)=="tee"&&NativeLook.Of(kept,slot)==(slot==NativeLook.Upper?"tee":id),"a slot change keeps the body: "+id);
            Check(NativeLook.SameOutfit(kept,NativeLook.Normalize(kept))&&NativeLook.Normalize(kept).Length<=NativeLook.MaxLength,"the outfit reads the same with the key");
        }
        Check(NativeBodyKind.Of(NativeBodyKind.Keep(NativeLook.WithSkin("body:female",0xc48a62),"body:female"))==NativeBodyKind.Female&&NativeBodyKind.Of(NativeBodyKind.Keep(NativeVoxelCostume.With("body:female",true),"body:female"))==NativeBodyKind.Female,"the tone and the costume keep the body");
        Check(NativeBodyKind.Keep("upper:tee","")=="upper:tee"&&NativeBodyKind.Keep("upper:tee,body:female","")=="upper:tee","Keep takes today's pilot back off");
        // The dressing (robot pieces under their garments) keeps the outfit's meaning; the body rides on top of it.
        Check(NativeBodyKind.Of(NativeBodyKind.Keep(NativeRobotSuit.Dress("body:female"),"body:female"))==NativeBodyKind.Female,"dressed robot pieces keep the body");
        // The female levels (owner 2026-11-25: "more feminine"): the pack's female directions past Default_Girl, every level further than
        // the last on each lever; the whole body never under .95; the shape names the pack's (matched by name on every worn mesh).
        var L=NativeBodyKind.Levels;Check(L.Length==3&&NativeBodyKind.DefaultLevel==NativeBodyKind.Strong&&NativeBodyKind.LevelOf(-1)==L[NativeBodyKind.DefaultLevel]&&NativeBodyKind.LevelOf(9)==L[NativeBodyKind.DefaultLevel],"three levels, Strong by default (the owner's pick)");
        string[] names={"BodyType","Weight","Chest","Curvy","WaistSize","ButtSize","NeckThickness","EyeRoundness","Roundness","Sharpness","Muscle"};
        foreach(var level in L)Check(level.Shapes.Length==names.Length&&level.Weights.Length==names.Length&&level.Root>=.95f&&level.Root<1&&level.Span<1&&level.Hand<1&&level.Chest<1&&level.ArmY<=1&&level.ArmZ<=1&&level.Chin<=1,"level "+level.Name+": every lever toward the pack's female");
        for(int k=0;k<names.Length;k++)Check(L[0].Shapes[k]==names[k],"the pack's shape names");
        for(int k=0;k<names.Length;k++){float a=L[0].Weights[k],b=L[1].Weights[k],c=L[2].Weights[k];bool down=k<2||k==6;Check(down?(a<=0&&b<=a&&c<=b):(a>=0&&b>=a&&c>=b),"each level further than the last: "+names[k]);}
        Check(L[0].Root>L[1].Root&&L[1].Root>L[2].Root&&L[0].Span>L[1].Span&&L[1].Span>L[2].Span&&NativeBodyKind.ChestBone=="spine_04","the bones, level by level");
        foreach(var level in L)Check(level.Weights[9]==0&&level.Weights[10]==0&&level.Lips==1,"level "+level.Name+": the male levers untouched, the lips as baked");
        // The male levels (owner 2026-11-27: "male pilot seems to look a bit feminine (Lips look kind of like lipstick?) And it looks
        // like they have "small" breasts"): the pack's male directions, every level further than the last; the chest flatter, the
        // lips toward the skin; the height and every hit volume as they are; on the same machinery (levels past MaleBase).
        var M=NativeBodyKind.MaleLevels;
        Check(M.Length==3&&NativeBodyKind.MaleDefault==NativeBodyKind.MaleBase+NativeBodyKind.Medium&&NativeBodyKind.MaleBase>=L.Length&&NativeBodyKind.IsMaleLevel(NativeBodyKind.MaleDefault)&&!NativeBodyKind.IsMaleLevel(NativeBodyKind.Strong),"three male levels past the female's, Medium by default");
        for(int i=0;i<M.Length;i++)Check(NativeBodyKind.LevelOf(NativeBodyKind.MaleBase+i)==M[i]&&M[i].Shapes.Length==names.Length&&M[i].Weights.Length==names.Length,"male level "+M[i].Name+" by its number");
        foreach(var level in M)Check(level.Root==1&&level.Span>1&&level.Hand>=1&&level.Chest>=1&&level.ArmY>=1&&level.ArmZ>=1&&level.Chin>=1&&level.Lips>0&&level.Lips<1,"male level "+level.Name+": every lever toward the pack's male, the height kept");
        foreach(int k in new[]{0,1,6,9,10}){float a=M[0].Weights[k],b=M[1].Weights[k],c=M[2].Weights[k];Check(a>0&&b>=a&&c>=b&&c<=100,"each male level further than the last: "+names[k]);}
        Check(M[0].Weights[2]<0&&M[1].Weights[2]<M[0].Weights[2]&&M[2].Weights[2]<M[1].Weights[2]&&M[2].Lips<M[0].Lips&&Array.TrueForAll(M,m=>m.Weights[3]==0&&m.Weights[4]==0&&m.Weights[5]==0&&m.Weights[7]==0),"the chest flatter and the lips quieter level by level; no female lever");
        Check(NativeBodyKind.StanceLegsIn>0&&NativeBodyKind.StanceKneesIn>0&&NativeBodyKind.StanceChestUp>0&&NativeBodyKind.MatchStanceLegsIn<NativeBodyKind.StanceLegsIn,"the lobby stance; the match option lighter");
        Check(NativeBodyKind.Ids[0]=="male"&&NativeBodyKind.Ids[1]=="female"&&NativeBodyKind.Labels[0]=="Male"&&NativeBodyKind.Labels[1]=="Female","the Locker's ids and labels");
        // Wiring: every body made dresses through VisualDress, which takes the female preset; the local pilot carries its pick; the
        // Locker's writers keep it; the suit and the core are fitted to it; it changes no rule.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string body=R("NativeVisualBodyPresentation.cs"),kind=R("NativeBodyKindPresentation.cs"),locker=R("NativeLockerBodyUI.cs"),ui=R("NativeLockerUI.cs"),outfit=R("NativeLockerOutfitUI.cs");
        Check(body.Contains("else if(NativeBodyKind.IsFemale(look)&&!NativeHotDog.Worn(look))VisualFemale(root,FemaleLevel);")&&body.Contains("if(body.FemaleMade&&body.FemaleLevel!=FemaleLevel)return false;")&&body.Contains("VisualFemaleMatchStance(body,a);\n            VisualHandFit(body,rig,held);")&&NativeBodyKind.IsFemale(NativeRobotSuit.Dress("body:female")),"bodies dress in the female preset through the robot dressing");
        Check(body.Contains("else if(!NativeHotDog.Worn(look)&&MaleLevel>=0)VisualFemale(root,MaleLevel);")&&body.Contains("body.FemaleMade=!NativeBodyKind.IsMaleLevel(femaleAt);")&&kind.Contains("var d=NativeBodyKind.IsMaleLevel(level)?null:VisualLookData();")&&kind.Contains("if(L.Lips<1)VisualLips(root,L.Lips);")&&kind.Contains("public static int MaleLevel=NativeBodyKind.MaleDefault;"),"male bodies at their level: no female face pieces or stance, the lips toned");
        Check(body.Contains("var seat=WeaponLoadout?.Match?.Seat(id);if(seat!=null)return seat.look")&&body.Contains("NativeBodyKind.Of(body.Look)!=NativeBodyKind.Of(look))return false;")&&body.Contains("VisualFemaleCore(body);"),"the local pilot's pick, a new body on a change, the core");
        Check(kind.Contains("chest.localScale*=L.Chest;")&&kind.Contains("head.localScale/=L.Chest;")&&kind.Contains("public static bool FemaleMatchStance;")&&kind.Contains("skin.SetBlendShapeWeight(index[k],Mathf.Clamp(L.Weights[k],0,100));")&&R("NativeVisualPortraitPresentation.cs").Contains("else if(pose!=NativeLobbyPoses.Chair)VisualFemaleLobbyStance(body);")&&locker.Contains("private const string LkFemaleHair=\"fringe-tail\";")&&R("NativeRobotSuitPresentation.cs").Contains("sourceModels.Queue(SuitModelOf(root,rig),"),"the Chest Size and the fitted plates");
        Check(locker.Contains("prefs.look=NativeBodyKind.With(prefs.look,k);lockerSpinAt=Time.unscaledTime;")&&ui.Contains("LkHeading(lockerSlots[k++],\"BODY\",")&&ui.Contains("return lockerYaw+LkSpin()+")&&outfit.Contains("lookShown=NativeBodyKind.Keep(lookShown,look);"),"the Locker's BODY row, its turntable and the shown look");
        Check(R("NativeColorWheelUI.cs").Contains("prefs.look=NativeBodyKind.Keep(prefs.look,was);")&&R("NativeLockerCostumeUI.cs").Contains("prefs.look=NativeBodyKind.Keep(NativeVoxelCostume.With(prefs.look,k==1),prefs.look);"),"the Locker's other writers keep the body");
        // Save Pilot keeps the pick in this PC's prefs; what leaves the PC (the profile, the seat) is GatedLook, normalised, including it.
        Check(R("NativeProfileUI.cs").Contains("prefs.look=GatedLook();")&&R("NativeNetwork.cs").Contains("look=GatedLook()")&&R("NativeNetwork.cs").Contains("private string GatedLook()=>NativeLook.Gate(prefs.look,"),"Save Pilot keeps the pick locally; the wire takes the normalised look");
        // Presentation only: nothing that simulates reads it.
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            string name=Path.GetFileName(f);
            if(name=="NativeArena.cs"||name.StartsWith("NativeCombat")||name.StartsWith("NativeDamage")||name.StartsWith("NativeMovement")||name.StartsWith("NativePrediction")||name=="NativeAbilities.cs"||name.StartsWith("NativeNetwork"))
                Check(!File.ReadAllText(f).Contains("NativeBodyKind"),"authority and the wire are body independent: "+name);
        }
        foreach(var file in new[]{"Core/ArenaWorld.cs","Core/NativeCombatRules.cs","Core/NativeMeleeRules.cs","Core/NativePrediction.cs"})
            Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena",file)).Contains("NativeBodyKind"),"authority and the look contract are untouched: "+file);
        Console.WriteLine("PASS body kind: the body:female key, its keeping, the female levels, stance and the Locker's BODY row");
    }
}
