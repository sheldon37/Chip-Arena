using System;
using System.IO;
using ChipArena.UnityConversion;
// The pilot's body (Core/NativeBodyKind; Astra's brief 2026-11-24 section 1): the look map's optional "body:female" key, omitted
// for today's pilot; the presentation keeps it at its own edges until NativeLook carries it (Astra's part); the female preset is
// the pack's Default_Girl; presentation only.
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
        // The female preset is the pack's Default_Girl (BoZo CustomCharacters/Anime/Default_Girl): its shapes, face, bone modifier.
        Check(NativeBodyKind.BodyShapes.Length==2&&NativeBodyKind.BodyShapes[0]=="Chest"&&NativeBodyKind.BodyWeights[0]==60&&NativeBodyKind.BodyShapes[1]=="Curvy"&&NativeBodyKind.BodyWeights[1]==100,"Default_Girl's body shapes");
        Check(NativeBodyKind.FaceShapes.Length==1&&NativeBodyKind.FaceShapes[0]=="EyeRoundness"&&NativeBodyKind.FaceWeights[0]==57&&NativeBodyKind.ChestBone=="spine_04"&&Math.Abs(NativeBodyKind.ChestScale-.97103566f)<1e-6,"Default_Girl's face and Chest Size");
        Check(NativeBodyKind.Ids[0]=="male"&&NativeBodyKind.Ids[1]=="female"&&NativeBodyKind.Labels[0]=="Male"&&NativeBodyKind.Labels[1]=="Female","the Locker's ids and labels");
        // Wiring: every body made dresses through VisualDress, which takes the female preset; the local pilot carries its pick; the
        // Locker's writers keep it; the suit and the core are fitted to it; it changes no rule.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string body=R("NativeVisualBodyPresentation.cs"),kind=R("NativeBodyKindPresentation.cs"),locker=R("NativeLockerBodyUI.cs"),ui=R("NativeLockerUI.cs"),outfit=R("NativeLockerOutfitUI.cs");
        Check(body.Contains("else if(NativeBodyKind.IsFemale(look)&&!NativeHotDog.Worn(look))VisualFemale(root);")&&body.Contains("if(NativeBodyKind.IsFemale(look))robotDressed[look]=dressed=NativeBodyKind.Keep(dressed,look);"),"bodies dress in the female preset through the robot dressing");
        Check(body.Contains("if(id==LocalId&&LocalBodyOwn(out var own))return own;")&&body.Contains("NativeBodyKind.Of(body.Look)!=NativeBodyKind.Of(look))return false;")&&body.Contains("VisualFemaleCore(body);"),"the local pilot's pick, a new body on a change, the core");
        Check(kind.Contains("chest.localScale*=NativeBodyKind.ChestScale;")&&kind.Contains("head.localScale/=NativeBodyKind.ChestScale;")&&R("NativeRobotSuitPresentation.cs").Contains("sourceModels.Queue(SuitModelOf(root,rig),"),"the Chest Size and the fitted plates");
        Check(locker.Contains("prefs.look=NativeBodyKind.With(prefs.look,k);lockerSpinAt=Time.unscaledTime;")&&ui.Contains("LkHeading(lockerSlots[k++],\"BODY\",")&&ui.Contains("return lockerYaw+LkSpin()+")&&outfit.Contains("lookShown=NativeBodyKind.Keep(lookShown,look);"),"the Locker's BODY row, its turntable and the shown look");
        Check(R("NativeColorWheelUI.cs").Contains("prefs.look=NativeBodyKind.Keep(prefs.look,was);")&&R("NativeLockerCostumeUI.cs").Contains("prefs.look=NativeBodyKind.Keep(NativeVoxelCostume.With(prefs.look,k==1),prefs.look);"),"the Locker's other writers keep the body");
        // Presentation only: nothing that simulates reads it.
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            string name=Path.GetFileName(f);
            if(name=="NativeArena.cs"||name.StartsWith("NativeCombat")||name.StartsWith("NativeDamage")||name.StartsWith("NativeMovement")||name.StartsWith("NativePrediction")||name=="NativeAbilities.cs"||name.StartsWith("NativeNetwork"))
                Check(!File.ReadAllText(f).Contains("NativeBodyKind"),"authority and the wire are body independent: "+name);
        }
        foreach(var file in new[]{"Core/ArenaWorld.cs","Core/NativeCombatRules.cs","Core/NativeMeleeRules.cs","Core/NativePrediction.cs","Core/NativeLook.cs"})
            Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena",file)).Contains("NativeBodyKind"),"authority and the look contract are untouched: "+file);
        Console.WriteLine("PASS body kind: the body:female key, its keeping, the Default_Girl preset and the Locker's BODY row");
    }
}
