using System;
using System.IO;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// Blind Pick All, Blind Pick with N choices and the Deathmatch's chip picks on the PvP draft's stage (owner 2026-11-18;
// presentation only). The N-choice rules are Astra's (PR #411, Core NativeOpeningDraft; DeathmatchChecks): here, that the screen
// reads exactly what those rules send.
internal static class BlindChoiceChecks
{
    public static void Run(string root)
    {
        int checks=0;
        void Check(bool passed,string message){if(!passed)throw new Exception("Blind choices: "+message);checks++;}
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");

        // ---- the screen's numbers against the rules' ----
        Check(NativeOpeningDraft.OfferCount(1,40)==NativeBlindChoices.Min&&NativeOpeningDraft.DefaultBlindChoices==3,"the fewest the rules deal is the fewest the screen draws; the owner's default of three");
        var content=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        Check(NativeOpeningDraft.OfferCount(99,content.weapons.Length)<=NativeBlindChoices.Max,"the screen lays out a whole pool ("+content.weapons.Length+" weapons)");
        Check(NativeBlindChoices.Name(false,3)==null&&NativeBlindChoices.Name(true,0)=="Blind Pick All"&&NativeBlindChoices.Name(true,3)=="Blind Pick · 3 choices","the modes' names");
        var labels=NativeBlindChoices.CountLabels(20);
        Check(labels.Length==20-NativeBlindChoices.Min+1&&labels[0]=="2 choices"&&labels[1]=="3 choices (default)","the count select: 2 up to the pool, the default marked");
        Check(NativeBlindChoices.CountLabels(4).Length==3,"the count select stops at the pool");

        foreach(var saved in new[]{null,"unknown","weapon","chair","sleep","crossed",NativeLobbyPoses.SheldonKey})
        {
            string pose=NativeLobbyPoses.UnarmedPortraitPose(saved);
            string expected=saved=="chair"||saved=="sleep"?saved:"crossed";
            NativeLobbyPoses.Sample(new MeleeRig(),"pistol",NativeLobbyPoses.Index(pose),0,out var held,out var offhand);
            Check(pose==expected&&!held.Visible&&!offhand.Visible,"blind room portrait hides both weapons for pose "+saved);
        }

        // ---- the hook ----
        var offers=new[]{"pistol","sword","fire"};
        var view=NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,offers,"sword",1,4,12);
        Check(view!=null&&view.Selected=="sword"&&view.Ready==1&&view.Total==4&&view.Remaining==12,"a Weapon view with offers is an N-choice pick");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,offers,"rifle",0,4,12).Selected==null,"a current weapon that was not dealt is no selection");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,Array.Empty<string>(),"rifle",0,4,12)==null,"Blind Pick All sends no offers");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,null,"rifle",0,4,12)==null,"no offers at all");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Chip,offers,"sword",0,4,12)==null,"the chip step's offers are chips");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,new[]{"pistol"},"pistol",0,4,12)==null,"one offer is no choice");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,new[]{"pistol","pistol"},"pistol",0,4,12)==null,"offers are distinct");
        Check(NativeBlindChoiceView.From("s",(int)OpeningStep.Weapon,new string[NativeBlindChoices.Max+1],"pistol",0,4,12)==null,"no more than a view carries");

        // ---- the hook reads the rules' private views ----
        var weapons=new[]{"pistol","rifle","sword","fire","bow","shield"};
        foreach(int choices in new[]{0,2,3,5})
        {
            var me=new NativeSeat{id=1,team=0,spec="pistol",name="A"};var other=new NativeSeat{id=2,team=1,spec="sword",name="B"};
            var deal=new NativeOpeningDraft(new[]{me,other},true,false,0,p=>Array.Empty<string>(),(s,id)=>Array.IndexOf(weapons,id)>=0,choices,choices==0?null:weapons,new SourceRandom(7).Next);
            var mine=deal.CardsFor(1,false,id=>id);var hook=NativeBlindChoiceView.From(deal.Session,(int)OpeningStep.Weapon,mine,deal.Pilots[1].Weapon,0,2,20);
            if(choices==0){Check(hook==null,"Blind Pick All draws the grid");continue;}
            Check(hook!=null&&hook.Offers.Length==choices&&hook.Selected==deal.Pilots[1].Weapon,"Blind Pick draws my "+choices+" dealt weapons");
            var theirs=deal.CardsFor(2,false,id=>id);Check(!ReferenceEquals(theirs,mine),"each seat's own offers");
            Check(Array.TrueForAll(hook.Offers,id=>deal.Submit(1,OpeningStep.Weapon,id,false,1)),"every card drawn is a pick the host takes");
        }

        // ---- the layout ----
        foreach(var area in new[]{(1544f,577f),(1144f,577f),(2044f,577f),(1544f,380f),(1144f,300f)})
            for(int n=2;n<=NativeBlindChoices.Max;n++)
            {
                var l=NativeBlindChoices.Fit(n,area.Item1,area.Item2);
                Check(l.Rows*l.Cols>=n&&l.Width>0&&l.Height>0,"every card has a place ("+n+" in "+area+")");
                Check(l.Cols*l.Width+(l.Cols-1)*NativeBlindChoices.Gap<=area.Item1+.5f&&l.Rows*l.Height+(l.Rows-1)*NativeBlindChoices.Gap<=area.Item2+.5f,"the cards fit the area ("+n+" in "+area+")");
                Check(!l.Hero||l.Width>=NativeBlindChoices.HeroMin,"hero cards only where they have room");
                float px=-1,py=-1;
                for(int i=0;i<n;i++)
                {
                    NativeBlindChoices.Place(l,i,n,28,150,area.Item1,area.Item2,out float x,out float y);
                    Check(x>=28-.5f&&x+l.Width<=28+area.Item1+.5f&&y>=150-.5f&&y+l.Height<=150+area.Item2+.5f,"a card stays inside the area");
                    if(i>0&&y==py)Check(x>=px+l.Width+NativeBlindChoices.Gap-.5f,"cards in a row never overlap");
                    px=x;py=y;
                }
            }
        var three=NativeBlindChoices.Fit(3,1544,577);
        Check(three.Hero&&three.Rows==1&&three.Width==NativeBlindChoices.HeroCap,"three choices at 16:9: one row of full hero cards");
        Check(NativeBlindChoices.Fit(5,1144,577).Hero&&NativeBlindChoices.Fit(5,1144,577).Rows==1,"five choices at 4:3: still one row of hero cards");

        // ---- the deal ----
        for(int i=0;i<NativeBlindChoices.Max;i++)
        {
            var before=NativeBlindChoices.At(NativeBlindChoices.DealStart+i*NativeBlindChoices.Stagger(NativeBlindChoices.Max)-.01f,i,NativeBlindChoices.Max,0,600,false);
            Check(before.Alpha==0&&!before.Face,"a card waits in the deck for its turn");
            var mid=NativeBlindChoices.At(NativeBlindChoices.DealStart+i*NativeBlindChoices.Stagger(NativeBlindChoices.Max)+NativeBlindChoices.DealFly*.5f,i,NativeBlindChoices.Max,0,600,false);
            Check(!mid.Face&&mid.Dy>0&&mid.Dy<600,"in flight it is face down between the deck and its place");
            var rest=NativeBlindChoices.At(NativeBlindChoices.DealEnd(NativeBlindChoices.Max)+1,i,NativeBlindChoices.Max,0,600,false);
            Check(rest.Face&&rest.Alpha==1&&rest.Dx==0&&rest.Dy==0&&rest.Scale==1&&rest.Degrees==0&&Math.Abs(rest.Flip-1)<1e-4f&&rest.Land==0,"every card rests face up in its place");
            var calm=NativeBlindChoices.At(0,i,NativeBlindChoices.Max,0,600,true);
            Check(calm.Face&&calm.Alpha==1&&calm.Dy==0&&calm.Flip==1,"reduced effects deal in place");
        }
        Check(NativeBlindChoices.DealEnd(NativeBlindChoices.Max)<2.4f&&NativeBlindChoices.DealEnd(3)<1.3f,"a deal of any size is done in under ~2 s");
        long bytes=GC.GetAllocatedBytesForCurrentThread();float sink=0;
        for(int f=0;f<400;f++){var l=NativeBlindChoices.Fit(2+f%11,1544,577);NativeBlindChoices.Place(l,f%3,3,28,150,1544,577,out float x,out float y);sink+=x+y+NativeBlindChoices.At(f*.01f,f%12,12,0,600,false).Dy;}
        Check(GC.GetAllocatedBytesForCurrentThread()-bytes==0&&sink!=0,"the layout and the deal allocate nothing per frame");

        // ---- the follow-through never holds a respawn ----
        Check(NativePickFollow.Plays(1,false,false,false,false),"the pick's follow-through plays");
        Check(NativePickFollow.Plays(.05f,true,false,false,false),"the pick's own press does not end it");
        Check(!NativePickFollow.Plays(.5f,true,false,false,false),"a key or click ends it");
        Check(!NativePickFollow.Plays(.5f,false,false,true,false),"the pilot's respawn ends it at once");
        Check(!NativePickFollow.Plays(.5f,false,true,false,false)&&!NativePickFollow.Plays(.5f,false,false,false,true),"an attack or the step's end ends it");
        Check(!NativePickFollow.Plays(NativeDraftPlant.ReturnEnd+.01f,false,false,false,false),"it ends with the draft's return");

        // ---- the screens' sources ----
        string stage=R("Runtime/NativeDeathmatchPickStage.cs"),choice=R("Runtime/NativeBlindChoiceUI.cs"),opening=R("Runtime/NativeOpeningDraftUI.cs"),dm=R("Runtime/NativeDeathmatchRuntime.cs"),options=R("Runtime/NativeRoomOptionsUI.cs"),draft=R("Runtime/NativeDraftPresentation.cs"),game=R("Runtime/ConversionGame.cs");
        Check(draft.Contains("if(!active&&!pve&&PickStageFrame())return true;")&&game.Contains("if(arena==null&&PickStageMenuFrame())return;"),"the chip picks draw the PvP draft's stage, in a match and in the room");
        // The comeback pick, mid-fight, resets nothing of the arena's presentation (its corpses, blood, wounds and gibs stay): the
        // stage restarts alone and the rest of the scene is hidden while it shows, put back as it was.
        string Body(string src,string head){int s=src.IndexOf(head,StringComparison.Ordinal),e=s<0?-1:src.IndexOf("\n        }",s,StringComparison.Ordinal);return s>=0&&e>s?src.Substring(s,e-s):"";}
        Check(stage.Contains("if(kind==PickStageKind.Comeback)presentation.RestartDraftStage();else presentation.ResetPresentation();")&&Regex.Matches(stage,@"ResetPresentation\(").Count==2,"the comeback stage's entry restarts the stage alone");
        Check(stage.Contains("private void PickStageLeave(){if(draftViewing&&draftViewSeat==ComebackStageView){draftViewing=false;RestoreLive();}PickStageUnhide();}")&&!Body(stage,"private void PickStageHide()").Contains("Reset")&&Body(stage,"private void PickStageHide()").Contains("r.enabled=false"),"its exit puts the hidden renderers back and resets nothing");
        Check(Body(stage,"private bool PickStageMenuFrame()").Contains("ResetPresentation")&&!Body(stage,"private bool PickStageFrame()").Contains("PickStageKind.Comeback)presentation.ResetPresentation"),"only the room's starting chip keeps the PvP draft's reset");
        Check(draft.Contains("public void RestartDraftStage(){draftRig?.Dispose();draftRig=null;}"),"RestartDraftStage drops only the stage's own rig and model");
        Check(dm.Contains("var r=new Rect(22-(1-enter)*30,H-24-124-12-44,w,44);"),"the comeback prompt sits above the vitals panel, not under it");
        Check(game.Contains("if(arena!=null&&!PickStageOverReplay()&&RenderReplay())return;")&&stage.Contains("if(ReplayActive)EndReplay();return true;"),"the comeback picker stands in front of that death's killcam");
        Check(stage.Contains("presentation.RenderDraft(seat,")&&stage.Contains("DraftShades(")&&stage.Contains("DraftHeader(")&&stage.Contains("LowerTray(")&&stage.Contains("PickedTray(")&&stage.Contains("Nameplate("),"the PvP draft's screen: its stage, shades, header, tray, pick and nameplate");
        Check(opening.Contains("if(step==OpeningStep.Chip){OpeningChipStage(v,w,h,now);return true;}")&&!opening.Contains("OpeningChipStep(")&&dm.Contains("if(picking)ComebackStage(seat,w,h,now);")&&!dm.Contains("ComebackPicker("),"both chip picks moved off the old tray");
        Check(Regex.Matches(stage,@"DrawTray\(").Count==2&&!stage.Contains("U.Button(")&&!stage.Contains("GUI.Button("),"the cards click through DrawTray's SilentButton");
        Check(!Regex.IsMatch(stage+choice,@"respawnAt\s*=(?!=)|TryDeathmatchRespawn|RecordDeathmatchDeath|TakeComebackChip|\.Tick\(|ComebackChips\s*=(?!=)|owedPicks\s*(=(?!=)|\+\+|--|\+=|-=)"),"no rule, timer or respawn is touched");
        Check(Regex.Matches(stage,@"new NativeNetMessage\{type=""").Count==1&&stage.Contains("type=\"dm-chip\",chip=id,pick=seat.pickTicket")&&stage.Contains("arena.InstallDeathmatchChip(seat.id,seat.pickTicket,id)"),"the comeback sends the same install as before");
        Check(stage.Contains("SendOpeningChoice(chips[pick].id,true)")&&!Regex.IsMatch(choice,@"new NativeNetMessage|SendServer\("),"the opening sends its own opening-choice messages only");
        Check(stage.Contains("PickStageOffers(\"opening/\"+v.pickSession,v.pickCards)")&&stage.Contains("seat.offers"),"each pilot's screen shows only their own offers");
        Check(choice.Contains("blindView=NativeBlindChoiceView.From(v.pickSession,v.pick,v.pickCards,v.chip,v.id,v.team,v.remainingSeconds);")&&!choice.Contains("RulesReady"),"the screen reads the host's view, ungated");
        Check(choice.Contains("string chosen=blindChoseIn==v.pickSession&&view.Offered(highlightedWeapon)?highlightedWeapon:null;")&&choice.Contains("\"RANDOM PICK AT 0\""),"nothing is marked until I choose; the timer says the rules pick at random");
        Check(options.Contains("RuleSelect(18740,RoomOpt.Blind,\"Weapon pick\",NativeBlindChoices.ModeLabels,mode);")&&options.Contains("RuleSelect(18756,RoomOpt.BlindChoices,\"Weapon choices\"")&&choice.Contains("o!=null&&o.blindPick?o.blindChoices:0"),"the room option sets the rules' blindPick and blindChoices");
        Check(!File.Exists(Path.Combine(root,"Assets/ChipArena/Runtime/NativeBlindChoicePreview.cs"))&&!Regex.IsMatch(stage+choice+opening,@"BlindPreview"),"no stand-in deal: the rules deal");
        int cards=choice.LastIndexOf("NativeWeaponCard.Draw(",StringComparison.Ordinal),flourish=choice.IndexOf("NativeWeaponCard.Flourishes();",StringComparison.Ordinal);
        Check(cards>0&&flourish>cards&&choice.Contains("SelectAge=sel?now-openingSelectAt:-10")&&choice.Contains("LockAge=sel&&locked?now-openingLockAt:-10")&&choice.Contains("if(chosen!=null)openingSelectAt=now"),"the dealt cards' choice and lock ages, then the flourishes");
        // The flourishes landed (claude/weapon-pick-flourish): the stand-in is gone and the real Flourishes() draws them.
        Check(R("Runtime/NativeWeaponCard.cs").Contains("public static partial class NativeWeaponCard")&&!File.Exists(Path.Combine(root,"Assets/ChipArena/Runtime/NativeWeaponCardFlourishStub.cs"))&&R("Runtime/NativeWeaponFlourish.cs").Contains("public static void Flourishes()"),"the flourishes, not a stand-in");

        // ---- Blind Pick All, as players read it ----
        foreach(var f in new[]{"Runtime/NativeOpeningDraftUI.cs","Runtime/NativeRoomOptionsUI.cs","Runtime/NativeLobbyUI.cs","Runtime/NativeLobbyRoomUI.cs","Runtime/NativeDeathmatchRuntime.cs","Runtime/ConversionGame.cs"})
        {var s=R(f);Check(!s.Contains("\"Blind weapon pick\"")&&!s.Contains("\"Blind pick")&&!s.Contains("\"BLIND PICK  ·  ")&&!s.Contains("and Blind Pick update"),f+": the old mode is Blind Pick All");}
        Check(opening.Contains("\"BLIND PICK ALL  ·  WEAPON\"")&&choice.Contains("\"BLIND PICK ALL\"")&&choice.Contains("Blind Pick All · Choose your weapon at match start")&&options.Contains("RuleSelect(18740,RoomOpt.Blind,\"Weapon pick\",NativeBlindChoices.ModeLabels,mode);")&&NativeBlindChoices.ModeLabels[1]=="Blind Pick All","the headers, the lobby line and the Weapon pick row say Blind Pick All");
        // (A co-op room's own Blind Pick All box is gone, 2026-11-19: it wrote the Play vs Bots pick; an expedition is always Blind
        // Pick with its own count and a boss battle keeps the loadouts. ExpeditionWeaponPickChecks holds that.)
        Check(R("Core/NativeRoomOptions.cs").Contains("public bool blindPick,deathmatch;"),"its id stays blindPick");

        Console.WriteLine("PASS blind pick all, blind choices and the deathmatch chip stage: "+checks+" checks");
    }
}
