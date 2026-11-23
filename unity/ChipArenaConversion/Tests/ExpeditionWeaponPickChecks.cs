using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
using Mode=ChipArena.UnityConversion.NativeRoomPick.Mode;

// An expedition's own weapon pick (owner 2026-11-19: "Its pulling chip selection logic from play vs bots (So if people select Blind
// pick all or anything else, its changing expedition pick logic.) I want Blind Pick choice with 5 options by default for
// expedition."). Core NativeRoomPick says which pick each mode's opening deals; the deal is the rules' (NativeOpeningDraft, Astra's).
// Here: the Play vs Bots Weapon pick never changes an expedition's (or a boss battle's) pick, an expedition's settings never change
// Play vs Bots, the default is five, the rules deal it, and the Expeditions lobby shows the pick instead of a loadout.
// (Not the expedition's chip pick: that is ExpeditionPickChecks.)
internal static class ExpeditionWeaponPickChecks
{
    public static void Run(string root)
    {
        int checks=0;
        void Check(bool passed,string message){if(!passed)throw new Exception("Expedition weapon pick: "+message);checks++;}
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
        string Body(string src,string head){int s=src.IndexOf(head,StringComparison.Ordinal),e=s<0?-1:src.IndexOf("\n        }",s,StringComparison.Ordinal);return s>=0&&e>s?src.Substring(s,e-s):"";}
        var content=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});

        Check(NativeTilesets.Selected(null).WeaponPool==WeaponPool.Ranged,"City uses the ranged weapon pool");
        foreach(WeaponPool pool in Enum.GetValues(typeof(WeaponPool)))
        {
            bool Expected(string family)=>pool==WeaponPool.All||pool==WeaponPool.RangedAndMagic&&(family=="ranged"||family=="magic")||
                string.Equals(pool.ToString(),family,StringComparison.OrdinalIgnoreCase);
            var eligible=content.weapons.Where(w=>NativeWeaponPools.Allows(pool,w)).Select(w=>w.id).ToArray();
            Check(eligible.SequenceEqual(content.weapons.Where(w=>Expected(w.@class)).Select(w=>w.id)),"catalog family filter "+pool);
            foreach(bool full in new[]{false,true})
            {
                var pilots=new[]{new NativeSeat{id=1,spec="sword"},new NativeSeat{id=2,spec="sword",bot=true}};
                bool Access(NativeSeat seat,string id)=>seat.id==1||!content.Weapon(id).devOnly;
                var deal=new NativeOpeningDraft(pilots,true,false,0,p=>Array.Empty<string>(),Access,
                    full?0:5,eligible,new SourceRandom(7).Next,full);
                Check(deal.Refusal==null,"pool opens "+pool);
                foreach(var pilot in deal.Pilots.Values)
                {
                    var permitted=eligible.Where(id=>Access(pilot.Seat,id)).ToArray();
                    Check(pilot.WeaponOffers.All(permitted.Contains)&&permitted.Contains(pilot.Weapon),"authorized offers and bot pick");
                    Check(pilot.WeaponOffers.Length==(full?permitted.Length:Math.Min(5,eligible.Count(id=>Access(pilots[1],id)))),"full and capped deal sizes");
                }
                foreach(var excluded in content.weapons.Where(w=>!eligible.Contains(w.id)))
                    Check(!deal.Submit(1,OpeningStep.Weapon,excluded.id,true,1),"reject excluded "+excluded.id);
                deal.Tick(21);
                Check(deal.Ready&&eligible.Contains(deal.Pilots[1].Weapon),"timeout stays in pool");
            }
        }

        // ---- the default: five ----
        Check(NativeRoomPick.ExpeditionDefault==5,"an expedition deals five choices by default");
        Check(R("Runtime/NativeExpeditionWeaponPick.cs").Contains("private int expeditionChoices=NativeRoomPick.ExpeditionDefault;"),"the host's count starts at the default");
        var fresh=NativeRoomPick.Apply(new NativeRoomOptions{cooperative=true,expedition=true},NativeRoomPick.ExpeditionDefault);fresh.Normalize(content);
        Check(fresh.blindPick&&fresh.blindChoices==5,"a fresh expedition room broadcasts Blind Pick with five choices");
        var labels=NativeBlindChoices.CountLabels(20,NativeRoomPick.ExpeditionDefault);
        Check(labels[5-NativeBlindChoices.Min]=="5 choices (default)"&&labels[3-NativeBlindChoices.Min]=="3 choices","the expedition's count select marks five as its default");
        Check(NativeBlindChoices.CountLabels(20)[3-NativeBlindChoices.Min]=="3 choices (default)","the versus count select keeps its default of three");

        // ---- the Play vs Bots Weapon pick never changes another mode's pick ----
        foreach(bool blind in new[]{false,true})
            foreach(int choices in new[]{0,2,3,7,24})
            {
                string pvb="(Play vs Bots blindPick "+blind+", blindChoices "+choices+")";
                var e=NativeRoomPick.Of(Mode.Expedition,blind,choices,NativeRoomPick.ExpeditionDefault);
                Check(e.Blind&&e.Dealt&&e.Choices==5,"an expedition always deals its own five "+pvb);
                var b=NativeRoomPick.Of(Mode.Boss,blind,choices,NativeRoomPick.ExpeditionDefault);
                Check(!b.Blind&&!b.Dealt,"a boss battle keeps the lobby loadouts "+pvb);
                var v=NativeRoomPick.Of(Mode.Versus,blind,choices,NativeRoomPick.ExpeditionDefault);
                Check(v.Blind==blind&&v.Dealt==(blind&&choices!=0)&&(!blind||v.Choices==choices),"a versus room reads Play vs Bots as before "+pvb);
                var eo=NativeRoomPick.Apply(new NativeRoomOptions{blindPick=blind,blindChoices=choices,cooperative=true,expedition=true},5);
                var bo=NativeRoomPick.Apply(new NativeRoomOptions{blindPick=blind,blindChoices=choices,cooperative=true},5);
                var vo=NativeRoomPick.Apply(new NativeRoomOptions{blindPick=blind,blindChoices=choices},5);
                Check(eo.blindPick&&eo.blindChoices==5&&!bo.blindPick&&bo.blindChoices==0&&vo.blindPick==blind&&vo.blindChoices==choices,"what each room broadcasts "+pvb);
            }
        foreach(int n in new[]{2,3,5,8,12,NativeBlindChoices.Max})Check(NativeRoomPick.Of(Mode.Expedition,true,3,n).Choices==n,"the host's count is dealt ("+n+")");
        Check(NativeRoomPick.Count(0)==NativeBlindChoices.Min&&NativeRoomPick.Count(1)==NativeBlindChoices.Min&&NativeRoomPick.Count(99)==NativeBlindChoices.Max,"the count stays a pick the screen draws");
        Check(NativeRoomPick.ModeOf(false,true)==Mode.Versus&&NativeRoomPick.ModeOf(true,false)==Mode.Boss&&NativeRoomPick.ModeOf(true,true)==Mode.Expedition,"the modes as CaptureRoomOptions reads them");

        // ---- the game's own path: Capture, a change, SetRoomOptions (the mode before, Normalize, Store, the mode after) ----
        {
            bool pve=false,run=false,bp=false;int bc=0,ex=NativeRoomPick.ExpeditionDefault;
            NativeRoomOptions Capture()=>NativeRoomPick.Apply(new NativeRoomOptions{blindPick=bp,blindChoices=bc,cooperative=pve,expedition=pve&&run},ex);
            void Set(NativeRoomOptions o){var was=NativeRoomPick.ModeOf(pve,run);o.Normalize(content);NativeRoomPick.Store(was,o,ref bp,ref bc,ref ex);pve=o.cooperative;run=o.expedition;}
            NativeRoomPick.Pick Now()=>NativeRoomPick.Of(NativeRoomPick.ModeOf(pve,run),bp,bc,ex);
            // Play vs Bots: Weapon pick Blind Pick All (ApplyRule Blind 1).
            {var o=Capture();o.blindPick=true;o.blindChoices=0;Set(o);}Check(Now().Mode==Mode.Versus&&Now().Blind&&!Now().Dealt,"Play vs Bots on Blind Pick All");
            // The Expeditions screen in its Expedition mode (ApplyRule Versus 2).
            {var o=Capture();o.cooperative=true;o.expedition=true;Set(o);}
            Check(Now().Mode==Mode.Expedition&&Now().Dealt&&Now().Choices==5,"Play vs Bots' Blind Pick All does not reach the expedition: Blind Pick, five choices");
            Check(bp&&bc==0,"switching to the expedition leaves Play vs Bots on Blind Pick All");
            // The host's count (ApplyRule BlindChoices), then another rule: the count is the expedition's alone.
            {var o=Capture();o.blindChoices=7;Set(o);}Check(Now().Choices==7&&bp&&bc==0,"an expedition's count changes only the expedition");
            {var o=Capture();o.skipPlayerIntros=true;Set(o);}Check(ex==7&&bp&&bc==0,"another expedition rule keeps both picks");
            {var w=Capture();w.Normalize(content);Check(w.blindPick&&w.blindChoices==7&&w.expedition,"guests receive the expedition's pick in the room options");
             bool gbp=false;int gbc=0,gex=NativeRoomPick.ExpeditionDefault;NativeRoomPick.Store(Mode.Expedition,w,ref gbp,ref gbc,ref gex);
             Check(gex==7&&!gbp&&gbc==0,"a guest promoted to host keeps the room's count and its own Play vs Bots pick");}
            // Boss Battle (ApplyRule Versus 1): the lobby loadouts; nothing stored either way.
            {var o=Capture();o.cooperative=true;o.expedition=false;Set(o);}Check(Now().Mode==Mode.Boss&&!Now().Blind&&bp&&bc==0&&ex==7,"Boss Battle keeps its loadouts and stores nothing");
            {var o=Capture();o.blindPick=true;o.blindChoices=9;Set(o);}Check(bp&&bc==0&&ex==7,"a boss battle's options write no pick");
            // Play vs Bots again (Go(Play) clears pve): still Blind Pick All; its own changes leave the expedition's seven.
            pve=false;Check(Now().Mode==Mode.Versus&&Now().Blind&&!Now().Dealt,"Play vs Bots is still on its own setting");
            {var o=Capture();o.blindPick=true;o.blindChoices=3;Set(o);}{var o=Capture();o.deathmatch=true;Set(o);}{var o=Capture();o.blindPick=false;o.blindChoices=0;Set(o);}
            Check(!bp&&bc==0&&ex==7,"Play vs Bots' changes never reach the expedition");
            pve=true;run=true;Check(Now().Dealt&&Now().Choices==7,"back on the Expeditions screen: still the expedition's own count");
            // Leaving the expedition through the room options (Versus 0) carries its Blind Pick into nothing.
            {var o=Capture();o.cooperative=false;o.expedition=false;Set(o);}Check(!bp&&bc==0&&Now().Mode==Mode.Versus&&!Now().Blind,"leaving the expedition does not switch Play vs Bots to Blind Pick");
        }

        // ---- the rules deal it, as PvP's Blind Pick (host-dealt, private, checked, a random offer at 0 s) ----
        {
            var ids=content.weapons.Select(w=>w.id).ToArray();var dev=new HashSet<string>(content.weapons.Where(w=>w.devOnly).Select(w=>w.id));
            Func<NativeSeat,string,bool> allowed=(s,id)=>id!="siren"&&!dev.Contains(id);
            var pick=NativeRoomPick.Of(Mode.Expedition,false,0,NativeRoomPick.ExpeditionDefault);bool differed=false;
            for(uint seed=1;seed<=40;seed++)
            {
                var host=new NativeSeat{id=1,team=0,spec="pistol",name="Host"};var guest=new NativeSeat{id=2,team=0,spec="sword",name="Guest"};
                var deal=new NativeOpeningDraft(new[]{host,guest},pick.Blind,false,0,p=>Array.Empty<string>(),allowed,pick.Dealt?pick.Choices:0,ids,new SourceRandom(seed).Next);
                Check(deal.Refusal==null,"the expedition's deal is accepted");
                foreach(var id in new[]{1,2})
                {
                    var p=deal.Pilots[id];var mine=deal.CardsFor(id,false,x=>x);
                    Check(p.Step==OpeningStep.Weapon&&mine.Length==5&&mine.Distinct().Count()==5&&mine.All(x=>allowed(p.Seat,x)),"each pilot is dealt five different allowed weapons (no Siren, no DEV without access)");
                    var view=NativeBlindChoiceView.From(deal.Session,(int)OpeningStep.Weapon,mine,p.Weapon,0,2,20);
                    Check(view!=null&&view.Offers.Length==5,"the N-choice screen draws the five");
                }
                differed|=!deal.Pilots[1].WeaponOffers.SequenceEqual(deal.Pilots[2].WeaponOffers);
                var notDealt=ids.First(x=>allowed(host,x)&&!deal.Pilots[1].WeaponOffers.Contains(x));
                Check(!deal.Submit(1,OpeningStep.Weapon,notDealt,true,1)&&!deal.Submit(1,OpeningStep.Weapon,"siren",true,1),"a weapon not dealt is refused");
                var choice=deal.Pilots[1].WeaponOffers[(int)(seed%5)];
                Check(deal.Submit(1,OpeningStep.Weapon,choice,true,1)&&deal.Pilots[1].Weapon==choice&&deal.Pilots[1].Step==OpeningStep.Ready,"a pick locks that weapon");
                deal.Tick(21);
                Check(deal.Ready&&deal.Pilots[2].WeaponOffers.Contains(deal.Pilots[2].Weapon),"at 0 s an undecided pilot gets one of their own offers");
            }
            Check(differed,"each pilot's deal is their own");
        }

        // ---- the sources: the opening, the station's Deploy, the room options and the lobby ----
        string pickUi=R("Runtime/NativeExpeditionWeaponPick.cs"),opening=R("Runtime/NativeOpeningDraftUI.cs"),live=R("Runtime/NativeDeploymentLive.cs"),lobby=R("Runtime/NativeLobbyLogic.cs"),
            net=R("Runtime/NativeNetwork.cs"),lobbyUi=R("Runtime/NativeLobbyUI.cs"),options=R("Runtime/NativeRoomOptionsUI.cs"),exp=R("Runtime/NativeExpeditionsMenu.cs"),stage=R("Runtime/NativeStageLoadingUI.cs");
        var begin=Body(opening,"private bool BeginOpening(");
        Check(begin.Contains("var pick=RoomPick;")&&begin.Contains("dealt?pick.Choices:0")&&begin.Contains("new NativeOpeningDraft(seats,pick.Blind,chips,")&&!Regex.IsMatch(begin,@"prefs\.blind(Pick|Choices)"),"the opening deals the room's own pick, never the Play vs Bots preference itself");
        Check(begin.Contains("if(pick.Mode==NativeRoomPick.Mode.Expedition&&deploymentRoom!=null&&!deploy)return false;"),"an expedition from the station picks at its Deploy, not again when its run loads (or restarts)");
        Check(stage.Contains("action==(Action)StartExpeditionM1)&&BeginOpening(()=>QueueStage(kind,()=>WithOpening(action))))return;"),"a run started without a station still picks as it loads");
        Check(pickUi.Contains("private NativeRoomPick.Pick RoomPick=>NativeRoomPick.Of(NativeRoomPick.ModeOf(prefs.pve,prefs.expedition),prefs.blindPick,prefs.blindChoices,expeditionChoices);"),"the room's pick from its mode");
        Check(live.Contains("else if(action==DeployIntent.Deploy)DeployWithPick(state.revision);")&&!Regex.IsMatch(live,@"DeployExpedition\("),"the station's Deploy picks first");
        Check(pickUi.Contains("if(BeginOpening(()=>WithOpening(()=>{KeepExpeditionPicks();DeployExpedition(revision);}),true))return;"),"then deploys with the picks, under the opening");
        var keep=Body(pickUi,"private void KeepExpeditionPicks()");
        Check(keep.Contains("keptPicks.Clear();keptPickRoom=deploymentRoom;foreach(var p in openingRun.Pilots.Values)keptPicks[p.Seat.id]=p.Weapon;")&&keep.Contains("foreach(var peer in peers)if(peer.Seat!=null)ApplyOpeningWeapon(peer.Seat);"),"every pilot's pick kept for this station, and each guest's on its seat");
        Check(Body(pickUi,"private bool KeptExpeditionPick(").Contains("deploymentRoom!=null&&ReferenceEquals(keptPickRoom,deploymentRoom)&&keptPicks.TryGetValue(seat.id,out id)&&OpeningWeaponAllowed(seat,id)"),"a kept pick belongs to its own station and is checked again");
        Check(Body(opening,"private void ApplyOpeningWeapon(").Contains("else if(!applyingOpening && KeptExpeditionPick(seat,out var kept))seat.spec=kept;")&&R("Runtime/NativeExpeditionM1.cs").Contains("foreach(var member in seats)ApplyOpeningWeapon(member);"),"the run's seats take the kept picks as it starts (a rebuilt seat or my preferences in between change nothing)");
        Check(!Regex.IsMatch(pickUi,@"new NativeNetMessage|SendServer\(|\.Send\(|Random|NewSeed"),"no new message and no deal of its own: the rules deal");
        Check(lobby.Contains("CaptureRoomOptions()=>NativeRoomPick.Apply(new NativeRoomOptions{blindPick=prefs.blindPick,")&&lobby.Contains("hostTeam=prefs.team},expeditionChoices);"),"the room options carry the room's own pick to guests");
        Check(lobby.Contains("var was=NativeRoomPick.ModeOf(prefs.pve,prefs.expedition);options.Normalize(content);")&&lobby.Contains("NativeRoomPick.Store(was,options,ref prefs.blindPick,ref prefs.blindChoices,ref expeditionChoices);")&&!Regex.IsMatch(lobby,@"prefs\.blindPick=options\.blindPick"),"a rules change is stored for its own mode only");
        Check(options.Contains("NativeRoomPick.Apply(m,expeditionChoices);return m;"),"the rows read the room's own pick");
        Check(options.Contains("if(rules.cooperative&&rules.expedition)ExpeditionPickRows(rules);")&&!Regex.IsMatch(options,@"RuleCheck\(18740,RoomOpt\.Blind,"),"a co-op room's rows set the expedition's count and no longer write the Play vs Bots pick");
        Check(Body(pickUi,"private void ExpeditionPickRows(").Contains("RuleSelect(18764,RoomOpt.BlindChoices,\"Weapon choices\",expCountLabels,"),"the count goes through the room options' own Weapon choices");
        Check(net.Contains("if(prefs.blindPick&&!prefs.pve&&!trainingRoom)foreach(var row in list){row.spec=\"pistol\";")&&lobbyUi.Contains("private bool RoomBlind=>!trainingRoom&&!prefs.pve&&(Hosting?prefs.blindPick:GuestRules?.blindPick==true);"),"a co-op room's pilots keep their looks in the lobby");

        // ---- the Expeditions lobby: the pick, no loadout weapon; the look stays ----
        Check(NativeRoomPick.RowLabel=="WEAPON PICK  ·  DEALT AT DEPLOY"&&NativeRoomPick.RowLine(5)=="BLIND PICK  ·  5 CHOICES"&&NativeRoomPick.CardLine(5)=="BLIND PICK · 5","the lobby's words: WEAPON PICK · BLIND PICK · 5 CHOICES");
        var side=Body(exp,"private void ExpSidePanel(");
        Check(side.Contains("ExpPickRow(ExpRect(m.Loadout),seat,a,now,ExpLive(a),L.Compact);")&&!side.Contains("ExpLoadoutRow("),"START EXPEDITION's row is the weapon pick, not the loadout");
        var row=Body(exp,"private void ExpPickRow(");
        Check(row.Contains("NativeRoomPick.RowLabel")&&row.Contains("NativeRoomPick.RowLine(ExpPickChoices)")&&row.Contains("ExpToggleDrawer();")&&!row.Contains("prefs.weapon"),"the row reads the pick and its count, not my weapon");
        Check(Body(exp,"private void ExpModePanel(").Contains("ExpLoadoutRow("),"Boss Battle keeps its loadout row");
        Check(Body(exp,"private void ExpDrawerBody(").Contains("if(expMode==X.Mode.Expedition){ExpPickDrawer(seat,o);return;}"),"the drawer of a run is the weapon pick sheet");
        var sheet=Body(exp,"private void ExpPickDrawer(");
        Check(sheet.Contains("ExpeditionPickRows(o)")&&!sheet.Contains("WeaponCards(")&&!sheet.Contains("ClassRow("),"a run's drawer offers no class or weapon");
        var card=Body(exp,"private void ExpSquadCard(");
        Check(card.Contains("NativeRoomPick.CardLine(ExpPickChoices)")&&!card.Contains("\"LOADOUT\"")&&card.Contains("Go(MenuScreen.Customize)")&&card.Contains("LobbyPortrait("),"a squad card names the pick, keeps the pilot's portrait and opens Customize");
        Check(exp.Contains("(\"palette\",\"CUSTOMIZE\",0)")&&exp.Contains("Go(MenuScreen.Customize)"),"Customize (look and outfit) stays in the header");
        Check(Body(R("Runtime/NativeBlindChoiceUI.cs"),"private void BlindChoiceStep(").Contains("DraftHeader((run?\"EXPEDITION  ·  \":\"\")+\"BLIND PICK  ·  \"+n+\" CHOICES\""),"the pick screen names the run");
        Check(NativeExpeditionCatalogue.Steps[0].Line.Contains("At Deploy"),"HOW A RUN GOES says where the pick is");

        Console.WriteLine("PASS expedition weapon pick: Play vs Bots never changes it, five by default, the rules deal it, the lobby shows it: "+checks+" checks");
    }
}
