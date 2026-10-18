using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckNetworkSerialization(NativeArena authority)
        {
            CheckAccountCompatibility();ConversionGame.ValidateLobbyPromotion(authority.Content);
            var expedition=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-public",expeditionPublic=new ExpeditionView
            {
                members=new[]{new ExpeditionViewMember{account="pilot",seat=7,safe=true}},
                areaBarrier=new ExpeditionAreaBarrierView{layout=new string('a',64)},
                bossArrivals=new[]{new ExpeditionBossArrivalView{id="boss",area="ward",archetype="test-hulk",spawned=2,x=3,y=4,z=5,hp=25,maxHp=120}}
            }})).expeditionPublic;
            if(expedition.members.Single().seat!=7||!expedition.members[0].safe||expedition.areaBarrier.layout!=new string('a',64)||expedition.areaBarrier.active||
                expedition.bossArrivals.Single().x!=3||expedition.bossArrivals[0].y!=4||expedition.bossArrivals[0].z!=5||expedition.bossArrivals[0].hp!=25||expedition.bossArrivals[0].maxHp!=120||expedition.bossArrivals[0].spawned!=2)
                throw new Exception("Expedition public JsonUtility envelope lost pilot, boss or startup layout state");
            var areaReady=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-area-ready",
                expeditionAreaReady=new ExpeditionAreaReadyRequest{run="run",epoch=3,target="ward",generation=7,layout=new string('b',64)}})).expeditionAreaReady;
            if(areaReady==null||areaReady.run!="run"||areaReady.epoch!=3||areaReady.target!="ward"||areaReady.generation!=7||areaReady.layout!=new string('b',64))
                throw new Exception("Expedition area-ready JsonUtility envelope lost load fences");
            var preferences=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(new ConversionPreferences{map="random",lobbyPose="crossed-arms",fullscreen=true}));
            if(preferences.map!="random"||preferences.lobbyPose!="crossed-arms"||!preferences.fullscreen)throw new Exception("Saved pilot pose/map/fullscreen did not round trip");
            var profile=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="ready",profile=new NativeProfile{cosmetics=new NativeCosmetics{lobbyPose="crossed-arms"}}}));
            if(profile.profile.cosmetics.lobbyPose!="crossed-arms")throw new Exception("Ready/save DTO lost lobby pose");
            // The portrait backdrop (prefs.lobbyBack) is saved with the pilot and sent in the ready/profile cosmetics like the
            // pose; a save from before backdrops loads as the tick ring.
            var backPrefs=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(new ConversionPreferences{lobbyBack="circuit"}));
            var backReady=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="ready",profile=new NativeProfile{cosmetics=new NativeCosmetics{lobbyBack="circuit"}}}));
            if(backPrefs.lobbyBack!="circuit"||backReady.profile.cosmetics.lobbyBack!="circuit"||NativePortraitBacks.Index(JsonUtility.FromJson<ConversionPreferences>("{\"name\":\"Old Pilot\"}").lobbyBack)!=NativePortraitBacks.TickRing)
                throw new Exception("Saved pilot/ready DTO lost the portrait backdrop, or an old save did not load as the tick ring");
            // Weapon skins (Core/NativeWeaponSkins.cs) are saved and sent the same way; an old save reads as every weapon Standard.
            var skinPrefs=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(new ConversionPreferences{weaponSkins="sword:prototype"}));
            var skinReady=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="ready",profile=new NativeProfile{cosmetics=new NativeCosmetics{weaponSkins="pistol:prototype,sword:prototype"}}}));
            if(skinPrefs.weaponSkins!="sword:prototype"||skinReady.profile.cosmetics.weaponSkins!="pistol:prototype,sword:prototype"||NativeWeaponSkins.Index(JsonUtility.FromJson<ConversionPreferences>("{\"name\":\"Old Pilot\"}").weaponSkins,"sword")!=NativeWeaponSkins.StandardIndex)
                throw new Exception("Saved pilot/ready DTO lost the weapon skins, or an old save did not load as Standard");
            if(NativeBodySkins.Of(JsonUtility.FromJson<ConversionPreferences>("{\"name\":\"Old Pilot\"}").weaponSkins)!=NativeBodySkins.StandardIndex||NativeBodySkins.Gate("body:sheldon",false)!="")throw new Exception("An old save did not load the Standard body, or the Sheldon Clothes passed the gate");
            // Headwear and backdrop colours (NativeHatLooks) travel the same way; an old save and an old profile are Auto.
            var colourPrefs=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(new ConversionPreferences{hatColor="#22e6ff",backColor="#ff3dcf"}));
            var colourReady=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="ready",profile=new NativeProfile{cosmetics=new NativeCosmetics{hatColor="#22e6ff",backColor="#ff3dcf",hat="sheldon-head"}}}));
            var oldPrefs=JsonUtility.FromJson<ConversionPreferences>("{\"name\":\"Old Pilot\"}");
            if(colourPrefs.hatColor!="#22e6ff"||colourPrefs.backColor!="#ff3dcf"||colourReady.profile.cosmetics.hatColor!="#22e6ff"||colourReady.profile.cosmetics.backColor!="#ff3dcf"||!NativeHatLooks.IsAuto(oldPrefs.hatColor)||!NativeHatLooks.IsAuto(oldPrefs.backColor))
                throw new Exception("Saved pilot/ready DTO lost the hat or backdrop colour, or an old save did not load as Auto");
            // The Sheldon Head is DEV-only: the host strips it from a guest or a grant, keeps it for DEV access.
            if(NativeCosmeticRules.Hat("sheldon-head",NativeAccess.Guest)!="none"||NativeCosmeticRules.Hat("sheldon-head",null)!="none"||NativeCosmeticRules.Hat("cap",NativeAccess.Guest)!="cap")
                throw new Exception("A non-DEV request kept the Sheldon Head");
            var original=authority.Local.Death;
            var scene=EditorSceneManager.NewPreviewScene();NativeArena replica=null;
            try
            {
                var frame=authority.NetworkFrame();var again=authority.NetworkFrame();float saved=frame.snapshot.Actors[0].Hp;authority.Local.Pose.Hp=0;
                if(frame.snapshot.Actors[0].Hp!=saved||!ReferenceEquals(frame.props[0],again.props[0]))throw new Exception("Replay capture mutated actor history or failed to share static props");authority.Local.Pose.Hp=saved;
                var message=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=frame}));
                var copy=message.frame;
                if(frame.sequence<=0||again.sequence<=frame.sequence||copy.sequence!=frame.sequence||again.time!=frame.time)
                    throw new Exception("Capture order must survive JsonUtility and advance independently of simulation time");
                if(message.epoch!=7||!copy.Valid(authority.Content)||copy.pve!=null||copy.pilots.Any(p=>p.death!=null||p.down!=null)||copy.props.Any(p=>p.definition!=null))
                    throw new Exception("Real JsonUtility match-frame round trip lost optional field presence");
                replica=new NativeArena(authority.Content,new NativeMatch(authority.Content),authority.MapId){Replica=true,LocalId=authority.LocalId,ReplicaScene=scene};
                replica.ApplyFrame(copy,authority.LocalId);
                if(replica.Pve!=null||replica.Root.scene!=scene)throw new Exception("PvP replay/replica escaped its isolated scene or created PvE");
                var final=NativeFrameCodec.Clone(copy);final.hasFinalKillcam=true;final.finalVictim=final.seats[1].id;final.finalSeed=42;final.resultAt=final.time;final.phaseEnd=final.time+8.5;final.phase=NativePhase.Result;
                final=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=final})).frame;
                if(!final.Valid(authority.Content))throw new Exception("Final kill marker failed real JsonUtility validation");
                replica.ApplyFrame(final,authority.LocalId);
                if(!replica.Match.HasFinalKillcam||replica.Match.FinalVictim!=final.finalVictim||replica.Match.FinalSeed!=42||replica.Match.ResultAt!=final.time||replica.Match.PhaseEnd!=final.phaseEnd)throw new Exception("Guest lost final kill marker");
                var legacy=JsonUtility.FromJson<NativeMatchFrame>("{}");
                if(legacy.hasFinalKillcam||legacy.finalVictim!=0||legacy.finalSeed!=0||legacy.resultAt!=0)throw new Exception("Old host final kill defaults");
                final.finalVictim=999;if(final.Valid(authority.Content))throw new Exception("Unknown final victim accepted");final.finalVictim=final.seats[1].id;
                final.finalSeed=-1;if(final.Valid(authority.Content))throw new Exception("Negative final seed accepted");final.finalSeed=42;
                final.resultAt=double.NaN;if(final.Valid(authority.Content))throw new Exception("Nonfinite result clock accepted");
                final.hasFinalKillcam=false;final.RestorePresence();replica.ApplyFrame(final,authority.LocalId);
                if(replica.Match.HasFinalKillcam||replica.Match.FinalVictim!=0||replica.Match.FinalSeed!=0||replica.Match.ResultAt!=0)throw new Exception("Absent final kill presence not cleared");
                replica.ApplyFrame(copy,authority.LocalId);
                Debug.Log("PASS final kill marker: real JsonUtility, guest ApplyFrame, old-host defaults and invalid marker rejection");
                var endpointFrame=NativeFrameCodec.Clone(copy);
                endpointFrame.projectileEnds=new[]{new NativeShotEnd{Id=912345,At=(float)endpointFrame.time,Position=new V3(1,2,3)}};
                endpointFrame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=endpointFrame})).frame;
                if(!endpointFrame.Valid(authority.Content))throw new Exception("Projectile endpoint JsonUtility frame invalid");
                replica.ApplyFrame(endpointFrame,authority.LocalId);var recaptured=replica.NetworkFrame().projectileEnds;
                if(recaptured.Length!=1||recaptured[0].Id!=912345||recaptured[0].Position.z!=3||recaptured[0].At!=(float)copy.time)throw new Exception("Guest replay lost projectile endpoint");
                endpointFrame.projectileEnds[0].At=float.NaN;if(endpointFrame.Valid(authority.Content))throw new Exception("NaN projectile endpoint accepted");
                endpointFrame.projectileEnds=null;replica.ApplyFrame(endpointFrame,authority.LocalId);
                if(replica.NetworkFrame().projectileEnds.Length!=0)throw new Exception("Old host or rewind retained stale projectile endpoint");
                replica.ApplyFrame(copy,authority.LocalId);
                Debug.Log("PASS projectile replay endpoints: real JsonUtility, validation, guest recapture and old-host clearing");
                var replayPilot=replica.Local;var beforeDeath=copy.pilots.First(p=>p.id==replayPilot.Seat.id);
                float deathAt=(float)copy.time+.025f;
                var afterDeath=new NativePilotFrame{hp=0,death=new NativeDeath{id=replayPilot.Seat.id,seed=987654,at=deathAt,position=beforeDeath.position+Vector3.right*.3f,yaw=beforeDeath.yaw,actor=replayPilot.Pose.ViewCopy()}};
                afterDeath.death.projectile=912345;
                if(JsonUtility.FromJson<NativeDeath>(JsonUtility.ToJson(afterDeath.death)).projectile!=912345||JsonUtility.FromJson<NativeDeath>("{}").projectile!=0)throw new Exception("Fatal projectile field round trip/default failed");
                replica.ReplaySampledShots=true;
                ConversionGame.ReplayPilotFrame(replayPilot,beforeDeath,afterDeath,copy.time,copy.time+.05,deathAt-.0125f);
                if(replayPilot.Hp<=0||Vector3.Distance(replayPilot.Body.position,beforeDeath.position+Vector3.right*.15f)>.001f)throw new Exception("Moving replay victim froze before contact");
                ConversionGame.ReplayPilotFrame(replayPilot,beforeDeath,afterDeath,copy.time,copy.time+.05,deathAt);
                if(replayPilot.Hp>0||replayPilot.Death!=afterDeath.death||replayPilot.Collider.enabled||replica.Snapshot().Actors.First(p=>p.Id==replayPilot.Seat.id).Hp>0)throw new Exception("Replay death did not switch at impact time");
                ConversionGame.ReplayPilotFrame(replayPilot,beforeDeath,afterDeath,copy.time,copy.time+.05,(float)copy.time);
                if(replayPilot.Hp<=0||!replayPilot.Collider.enabled||replayPilot.Death!=beforeDeath.death)throw new Exception("Replay death rewind did not restore live state");
                replica.ReplaySampledShots=false;replica.ApplyFrame(copy,authority.LocalId);
                Debug.Log("PASS replay death timing: moving victim reaches contact, dies at the recorded clock and rewinds cleanly");
                var devCosmetics=NativeFrameCodec.Clone(copy);devCosmetics.phase=NativePhase.Intro;devCosmetics.introStyle="sheldon-intro";devCosmetics.introSeat=devCosmetics.seats[0].id;
                devCosmetics.seats[0].hat="sheldon-head";devCosmetics.seats[0].intro="sheldon-intro";devCosmetics.seats[0].overrideIntro=true;devCosmetics.snapshot.Actors[0].Hat="sheldon-head";
                devCosmetics=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=devCosmetics})).frame;replica.ApplyFrame(devCosmetics,authority.LocalId);
                if(replica.Match.IntroStyle!="sheldon-intro"||replica.Match.Seat(devCosmetics.seats[0].id).hat!="sheldon-head"||replica.Pilots[0].Pose.Hat!="sheldon-head")throw new Exception("Host-approved DEV cosmetics did not reach a guest");
                if(NativeCosmeticRules.Hat("sheldon-head",NativeAccess.Guest)!="none"||NativeCosmeticRules.OverrideIntro("sheldon-intro",true,NativeAccess.Guest))throw new Exception("Guest could select DEV-only Sheldon cosmetics");
                replica.ApplyFrame(copy,authority.LocalId);
                Debug.Log("PASS DEV cosmetics: host-approved Sheldon hat/intro survive real frame codec and guest application; guest selection remains denied");
                var draft=NativeFrameCodec.Clone(copy);draft.phase=NativePhase.Draft;draft.draftSeat=draft.seats[1].id;draft.chosen=null;draft.readyAt=draft.time;
                draft.seats[1].offers=new[]{authority.Content.chips.First(c=>c.rarity=="common").id};
                draft=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=draft})).frame;
                replica.ApplyFrame(draft,draft.draftSeat);
                if(draft.chosen!=null||replica.Match.Chosen!=null||replica.Match.Phase!=NativePhase.Draft||replica.Local.Seat.offers.Count!=1||replica.Match.DraftSeat!=replica.LocalId)
                    throw new Exception("Guest draft was already chosen or lost its local offers after JsonUtility");
                draft.chosen=draft.seats[1].offers[0];replica.ApplyFrame(NativeFrameCodec.Clone(draft),draft.draftSeat);
                if(replica.Match.Chosen!=draft.chosen)throw new Exception("Guest lost the confirmed draft pick");
                draft.chosen="";replica.ApplyFrame(NativeFrameCodec.Clone(draft),draft.draftSeat);
                if(replica.Match.Chosen!=null)throw new Exception("Guest next draft kept the old chosen chip");
                replica.ApplyFrame(copy,authority.LocalId);
                var intro=NativeFrameCodec.Clone(copy);intro.phase=NativePhase.Intro;intro.introStyle="dev-siren";
                intro.introStarted=10.1234567;intro.time=intro.introStarted+2;intro.bossSongAt=(float)intro.introStarted;intro.bossSongLead=0;
                replica.ApplyFrame(NativeFrameCodec.Clone(intro),authority.LocalId);
                if(!replica.IntroStartsBossSong||Math.Abs(replica.BossSongTime-2)>.0001)throw new Exception("First PvP Siren lost its song origin on the guest");
                intro.introStarted+=NativeIntroRules.Duration("dev-siren");intro.time=intro.introStarted+.1;
                replica.ApplyFrame(NativeFrameCodec.Clone(intro),authority.LocalId);
                if(replica.IntroStartsBossSong||replica.BossSongTime<9)throw new Exception("Second PvP Siren restarted or acquired the first Siren's song");
                replica.ApplyFrame(copy,authority.LocalId);
                Debug.Log("PASS Siren PvP audio origins: real frame codec preserves first-intro ownership and a continuous song for later Sirens");
                // Late first sight after a reflect must use the projectile payload, not the new owner's loadout.
                var visual=NativeFrameCodec.Clone(frame);int source=visual.seats[0].id,target=visual.seats[1].id;
                visual.seats[0].lobbyPose="crossed";visual.pilots[0].dots=new[]{new NativeDotFrame{key=target+":fire",source=target,dps=4,next=1,until=4}};visual.pilots[0].slowEnd=4;visual.pilots[0].slowFactor=.5f;
                visual.snapshot.Actors[0].LastAttack=.4f;visual.snapshot.Actors[0].JuggleUntil=2;
                visual.snapshot.Shots=new[]{new Shot{Id=901,Owner=target,Original=source,Spec="ansatsuken",SirenKind="",LeetKind="",Hadoken=true,Burn=3,Poison=2,Born=.2f,Radius=.2f,Mods=new[]{new ChipCount{id="arcade-red",count=1}}}};
                visual.effects=new[]{
                    new NativeEffectFrame{type="ability",id=902,owner=source,ability="frag-grenade",radius=.14f,fuse=3},
                    new NativeEffectFrame{type="zone",id=903,owner=source,ability="orbital-mark",kind="bomb",radius=3,at=.2f,until=5,attached=target,lockAt=2},
                    new NativeEffectFrame{type="melee",id=904,owner=target,original=source,spec="daggers",radius=.1f,burn=4,poison=2},
                    new NativeEffectFrame{type="chip",id=905,owner=source,kind="fire",effect="hot-foot",radius=3,at=.3f,until=5}};
                visual=NativeFrameCodec.Clone(visual);if(!visual.Valid(authority.Content))throw new Exception("Visual/status frame failed validation");replica.ApplyFrame(visual,authority.LocalId);
                var guestPilot=replica.Pilots.First(p=>p.Seat.id==source);var bullet=replica.Bullets.Single();
                if(guestPilot.Seat.lobbyPose!="crossed"||guestPilot.Dots[target+":fire"].Dps!=4||guestPilot.SlowFactor!=.5f||guestPilot.SlowEnd!=4||guestPilot.Pose.LastAttack!=.4f||guestPilot.Pose.JuggleUntil!=2||!bullet.Hadoken||bullet.Original!=source||bullet.Mods["arcade-red"]!=1||bullet.Burn!=3||bullet.Poison!=2||replica.AbilityShots.Single().Fuse!=3||replica.AbilityZones.Single().Id!=903||replica.AbilityZones.Single().Born!=.2f||replica.AbilityZones.Single().Attached?.Seat.id!=target||replica.MeleeShots.Single().Burn!=4||replica.MeleeShots.Single().Poison!=2||replica.ChipFields.Single().Effect!="hot-foot")
                    throw new Exception("Guest/replay lost pose, status, zone identity, fuse or original projectile effects");
                if(bullet.SirenKind!=null||bullet.LeetKind!=null)throw new Exception("Empty optional shot tags were not restored to null after JsonUtility");
                var tracker=new NativeShotTracker();tracker.Begin(replica,replica.Generation,true,replica.Time);NativeShotViews.Bullet(replica,bullet,tracker,replica.Time,out var view);if(view.Siren||view.Arcade!=1||!view.Fire||!view.Poison)throw new Exception("Late reflected hadouken appearance: Siren="+view.Siren+", Arcade="+view.Arcade+", Fire="+view.Fire+", Poison="+view.Poison);
                // Nonempty special tags must survive the same boundary; normalization must not hide real Siren shots.
                var special=NativeFrameCodec.Clone(visual);special.snapshot.Shots[0].SirenKind="seeker";special.snapshot.Shots[0].LeetKind="lock";
                replica.ApplyFrame(NativeFrameCodec.Clone(special),authority.LocalId);var specialBullet=replica.Bullets.Single();tracker.Clear();tracker.Begin(replica,replica.Generation,true,replica.Time);NativeShotViews.Bullet(replica,specialBullet,tracker,replica.Time,out var specialView);
                if(specialBullet.SirenKind!="seeker"||specialBullet.LeetKind!="lock"||!specialView.Siren)throw new Exception("Nonempty special projectile tags lost their identity");
                var invalid=NativeFrameCodec.Clone(visual);invalid.snapshot.Shots[0].Mods=new[]{new ChipCount{id="x",count=1},new ChipCount{id="x",count=2}};if(invalid.Valid(authority.Content))throw new Exception("Duplicate projectile mods accepted");
                invalid=NativeFrameCodec.Clone(visual);invalid.pilots[0].dots[0].dps=double.NaN;if(invalid.Valid(authority.Content))throw new Exception("Nonfinite status accepted");
                replica.ApplyFrame(copy,authority.LocalId);if(guestPilot.Dots.Count!=0||replica.AbilityZones.Count!=0||replica.Bullets.Count!=0)throw new Exception("Expired replicated effects survived the next frame");
                Debug.Log("PASS guest/replay visual contracts: lobby pose, DoT/slow, fighter timestamps, reflected shot flags/mods, ability fuse, zone identity/attachment, melee effects and chip source; malformed payloads rejected");
                // Portrait backdrops ride the seat frame like the lobby pose: a known key arrives, an unknown one (another
                // client's newer style) is the tick ring, and an over-long key fails validation.
                var backs=NativeFrameCodec.Clone(copy);backs.seats[0].lobbyBack="warp";backs.seats[1].lobbyBack="not-a-backdrop";backs=NativeFrameCodec.Clone(backs);
                if(!backs.Valid(authority.Content))throw new Exception("Portrait backdrop frame failed validation");replica.ApplyFrame(backs,authority.LocalId);
                if(replica.Match.Seat(backs.seats[0].id)?.lobbyBack!="warp"||replica.Match.Seat(backs.seats[1].id)?.lobbyBack!=NativePortraitBacks.Default)throw new Exception("Guest/replay lost or did not normalise the portrait backdrop");
                var longBack=NativeFrameCodec.Clone(copy);longBack.seats[0].lobbyBack=new string('x',NativePortraitBacks.MaxLength+1);if(longBack.Valid(authority.Content))throw new Exception("Over-long portrait backdrop accepted");
                // Weapon skins ride the seat frame too: a picked skin arrives, junk normalises away (Standard), an over-long map fails.
                var skins=NativeFrameCodec.Clone(copy);skins.seats[0].weaponSkins="sword:prototype,rifle:prototype";skins.seats[1].weaponSkins="sword:gold,nope";skins=NativeFrameCodec.Clone(skins);
                if(!skins.Valid(authority.Content))throw new Exception("Weapon skin frame failed validation");replica.ApplyFrame(skins,authority.LocalId);
                if(replica.Match.Seat(skins.seats[0].id)?.weaponSkins!="rifle:prototype,sword:prototype"||replica.Match.Seat(skins.seats[1].id)?.weaponSkins!="")throw new Exception("Guest/replay lost or did not normalise the weapon skins");
                var longSkins=NativeFrameCodec.Clone(copy);longSkins.seats[0].weaponSkins=new string('x',NativeWeaponSkins.MaxLength+1);if(longSkins.Valid(authority.Content))throw new Exception("Over-long weapon skin map accepted");
                // The pilot's body skin is the map's "body" entry (Core/NativeBodySkins.cs): it arrives with the weapon picks (the
                // host gated the Sheldon Clothes when it built the seat), and a body pick no client knows reads as Standard.
                var bodies=NativeFrameCodec.Clone(copy);bodies.seats[0].weaponSkins="body:sheldon,sword:prototype";bodies.seats[1].weaponSkins="body:gold";bodies=NativeFrameCodec.Clone(bodies);
                if(!bodies.Valid(authority.Content))throw new Exception("Body skin frame failed validation");replica.ApplyFrame(bodies,authority.LocalId);
                if(replica.Match.Seat(bodies.seats[0].id)?.weaponSkins!="sword:prototype,body:sheldon"||NativeBodySkins.Of(replica.Match.Seat(bodies.seats[1].id)?.weaponSkins)!=NativeBodySkins.StandardIndex)throw new Exception("Guest/replay lost or did not normalise the body skin");
                // Hat and backdrop colours ride the seat frame too: a colour arrives (lower-cased), junk is Auto, an older
                // host's frame without them is Auto, an over-long one fails validation; the Sheldon Head id is shown as sent.
                var colours=NativeFrameCodec.Clone(copy);colours.seats[0].hatColor="#22E6FF";colours.seats[0].backColor="#ff3dcf";colours.seats[0].hat="sheldon-head";colours.seats[1].hatColor="red";colours.seats[1].backColor=null;colours=NativeFrameCodec.Clone(colours);
                if(!colours.Valid(authority.Content))throw new Exception("Colour frame failed validation");replica.ApplyFrame(colours,authority.LocalId);
                var cs0=replica.Match.Seat(colours.seats[0].id);var cs1=replica.Match.Seat(colours.seats[1].id);
                if(cs0?.hatColor!="#22e6ff"||cs0.backColor!="#ff3dcf"||cs0.hat!="sheldon-head"||cs1?.hatColor!=""||cs1.backColor!="")throw new Exception("Guest/replay lost or did not normalise the hat/backdrop colours or the DEV hat");
                var longColour=NativeFrameCodec.Clone(copy);longColour.seats[0].hatColor="#22e6ff00";if(longColour.Valid(authority.Content))throw new Exception("Over-long hat colour accepted");
                replica.ApplyFrame(copy,authority.LocalId);
                var melee=NativeFrameCodec.Clone(copy);var ma=melee.snapshot.Actors[0];
                ma.CommitAt=1;ma.CommitUntil=1.62f;ma.CommitYaw=24;ma.SwingAt=1.14f;ma.SwingUntil=1.30f;ma.StepAt=1.077f;ma.StepUntil=1.268f;ma.StepDistance=2.2f;ma.ChainAt=1.5f;ma.ComboUntil=2.02f;ma.MeleeBufferUntil=1.8f;ma.SprintAttack=ma.OpenerLunge=true;
                ma.MeleeKit=new NativeMeleeKit{Kind="aegis-ricochet",Phase="active",At=1,Release=1.3f,Until=4,Yaw=.7f,RecastKind="kunai-mark",Target=2,RecastUntil=5,MarkTarget=2,MarkUntil=5,ShieldOut=true,ShieldPosition=new V3(1,2,3),ShieldChain=new[]{2,3},Afterimage=new V3(3,2,1),AfterimageUntil=2,FissureStart=new V3(1,0,2),FissureDirection=new V3(0,0,1),FissureSpeed=25,FissureLength=10,FissureAt=1,FissureUntil=3,SuperArmour=true,Absorbed=3};
                melee.seats[0].intro="orbital-drop";melee.seats[0].overrideIntro=true;
                melee=NativeFrameCodec.Clone(melee);if(!melee.Valid(authority.Content))throw new Exception("Melee kit frame rejected");replica.ApplyFrame(melee,authority.LocalId);
                var mp=replica.Pilots.First(p=>p.Seat.id==ma.Id);var mk=mp.MeleeKit;
                if(mp.Pose.CommitYaw!=24||mp.Pose.StepDistance!=2.2f||mp.Pose.ChainAt!=1.5f||mp.Pose.ComboUntil!=2.02f||mp.Pose.MeleeBufferUntil!=1.8f||!mp.Pose.SprintAttack||!mp.Pose.OpenerLunge)throw new Exception("JsonUtility/guest lost melee feel fields");
                var edge=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="input",input=new InputFrame{Sequence=4,Attack=true,Shoot=true,LockTarget=2}})).input;
                if(!edge.Attack||!edge.Shoot||edge.LockTarget!=2||edge.HeldOnly().Attack)throw new Exception("Input edge/lock contract lost");
                var badFeel=NativeFrameCodec.Clone(melee);badFeel.snapshot.Actors[0].CommitYaw=float.NaN;if(badFeel.Valid(authority.Content))throw new Exception("Nonfinite melee commit yaw accepted");
                if(mk.Kind!="aegis-ricochet"||mk.Phase!="active"||mk.RecastUntil!=5||mk.MarkTarget!=2||mk.MarkUntil!=5||!mk.ShieldOut||mk.ShieldChain.Length!=2||mk.ShieldPosition.z!=3||mk.FissureSpeed!=25||mk.FissureLength!=10||mk.AfterimageUntil!=2||!mk.SuperArmour||mk.Absorbed!=3||!replica.Match.Seats[0].overrideIntro||replica.Match.Seats[0].intro!="orbital-drop")throw new Exception("JsonUtility/guest lost melee or entrance state");
                melee.snapshot.Actors[0].MeleeKit.ShieldChain[0]=99;if(mk.ShieldChain[0]!=2)throw new Exception("Replica aliases replay melee history");
                melee.seats[0].intro="pulse-wave";replica.ApplyFrame(NativeFrameCodec.Clone(melee),authority.LocalId);if(replica.Match.Seats[0].overrideIntro||replica.Match.Seats[0].intro!=NativeIntroRules.Default)throw new Exception("Retired seat entrance was not migrated");
                melee.snapshot.Actors[0].MeleeKit.Absorbed=4;if(melee.Valid(authority.Content))throw new Exception("Unbounded absorbed count accepted");
                replica.ApplyFrame(copy,authority.LocalId);
                Debug.Log("PASS melee snapshot contract: real JsonUtility phase/recast/mark/chain/fissure/afterimage/armour/absorb round trip, deep-copy isolation and old/new entrance seat normalization");
                var moved=NativeFrameCodec.Clone(frame);moved.time+=1;moved.pilots[1].position+=Vector3.right*3;
                replica.ApplyFrame(moved,authority.LocalId);replica.InterpolateReplica(.1f);
                var remote=replica.Pilots.First(p=>p.Seat.id==moved.pilots[1].id);
                if(Vector3.Distance(remote.Body.transform.position,moved.pilots[1].position)>.01f)
                    throw new Exception("Replica Transform did not move without a physics simulation step");
                authority.Local.Death=new NativeDeath{id=authority.LocalId,seed=17,actor=authority.Local.Pose.ViewCopy()};
                var death=NativeFrameCodec.Clone(authority.NetworkFrame());
                if(!death.Valid(authority.Content)||death.pilots[0].death==null||death.pilots[0].death.wound!=null||death.pilots[0].death.actor==null)
                    throw new Exception("A death without a wound must survive the real replay serialization path");
                replica.ApplyFrame(death,authority.LocalId);
                var pve=new NativeGameFrame{pve=new NativePveFrame{rows=new[]{new NativePveRowFrame{picks=new[]{new NativePvePickFrame{offers=null},new NativePvePickFrame{offers=new[]{"test"}}}}}}};
                pve=NativeFrameCodec.Clone(pve);
                if(pve.pve==null||pve.pve.rows[0].picks[0].offers!=null||pve.pve.rows[0].picks[1].offers.Length!=1)
                    throw new Exception("PvE and deferred choice presence lost in JsonUtility");
                string control=NativeNetCodec.Encode(new NativeNetMessage{type="input",input=new InputFrame{Sequence=1}});
                if(control.Contains("\"frame\""))throw new Exception("Control messages serialized an unused match frame");
                var big=NativeFrameCodec.Clone(frame);big.snapshot.Events=Enumerable.Range(0,20000).Select(i=>new ArenaEvent{Id=i,Kind=EventKind.Hit}).ToArray();
                var packet=NativeFrameBudget.Encode(big,7);
                if(packet==null||packet.Length>NativeFrameBudget.MaxPacketBytes||NativeNetCodec.Decode(NativeConnection.Decode(packet,NativeFrameBudget.MaxJsonBytes)).frame.snapshot.Events.Length>NativeEventBuffer.CapacityLimit)throw new Exception("Frame presentation budget did not constrain a horde burst");
                string ready=NativeNetCodec.Encode(new NativeNetMessage{type="ready",ready=true,profile=new NativeProfile{cosmetics=new NativeCosmetics{preferredSpec="rifle"}}});
                if(NativeNetCodec.Decode(ready).profile.cosmetics.preferredSpec!="rifle")throw new Exception("Ready lost selected loadout");
                Debug.Log("PASS real JsonUtility: PvP frames validate/apply, optional nested death/PvE/props preserved, replay stays in isolated physics, compact controls");
            }
            finally{authority.Local.Death=original;replica?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static void CheckAccountCompatibility()
        {
            var old=JsonUtility.FromJson<ConversionPreferences>("{\"name\":\"Saved Pilot\",\"weapon\":\"rifle\",\"lobbyPose\":\"crossed\",\"accountOrigin\":\"\",\"introText\":\"My old pilot\"}");
            if(!old.MigrateAccountService()||old.accountOrigin!=NativeAccount.DefaultOrigin||old.name!="Saved Pilot"||old.weapon!="rifle"||old.lobbyPose!="crossed"||old.introText!="My old pilot")throw new Exception("Account service migration changed the saved pilot");
            old.accountOrigin="";old=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(old));
            if(old.MigrateAccountService()||old.accountOrigin!="")throw new Exception("Explicit offline account choice was overwritten on reload");
            var custom=new ConversionPreferences{accountOrigin="https://accounts.example.test"};custom.MigrateAccountService();
            if(custom.accountOrigin!="https://accounts.example.test"||new ConversionPreferences().accountOrigin!=NativeAccount.DefaultOrigin)throw new Exception("Account service default/custom URL compatibility failed");
            var profile=new NativeProfile{name="Saved Pilot",@class="magic",color="#123456",cosmetics=new NativeCosmetics{lobbyPose="crossed",preferredSpec="lightning",intro="pulse-wave",introText="My saved entrance",bodyColor="#abcdef",hat="crown",killEffect="ragdoll-petals",overrideWeaponIntro=true}};
            string expected=JsonUtility.ToJson(profile);
            var saved=JsonUtility.FromJson<NativeProfileRequest>(JsonUtility.ToJson(new NativeProfileRequest{revision=17,profile=profile}));
            var loaded=JsonUtility.FromJson<NativeAccountResponse>(JsonUtility.ToJson(new NativeAccountResponse{account=new NativeAccountData{id="fixture-account",profile=profile,hasProfile=true,revision=17,profileSchema=2,grantVersion=4,developer=true,unlocks=new[]{"weapon:emperor","hat:auditor-halo"},flags=new[]{"playtester","vip"}}}));
            var ready=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="ready",profile=profile}));
            if(saved.revision!=17||JsonUtility.ToJson(saved.profile)!=expected||JsonUtility.ToJson(loaded.account.profile)!=expected||JsonUtility.ToJson(ready.profile)!=expected||!loaded.account.hasProfile||!loaded.account.developer||loaded.account.revision!=17||loaded.account.profileSchema!=2||loaded.account.grantVersion!=4||loaded.account.unlocks.Length!=2||loaded.account.flags.Length!=2)throw new Exception("Account save/reload or lobby payload dropped profile fields, grants or flags");
            var go=new GameObject("Account compatibility check");
            try
            {
                // A build check must never erase a developer's remembered sign-in.
                var account=go.AddComponent<NativeAccount>();account.Configure("");account.Login("login","fixture-user","",null,false);account.Save(profile);account.ReloadSavedProfile();account.Refresh();account.Clear(false);
                if(account.Status!="Signed out / offline"||account.Busy||account.Account!=null||account.Access.Developer||account.Token!=""||account.RecoveryCode!="")throw new Exception("Unconfigured accounts must stay signed out/offline without a request or grants");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            Debug.Log("PASS account compatibility: one-time service migration, explicit offline/custom URL, complete profile save/reload/lobby data, grants and flags");
        }
        private static void CheckCombatParity(ConversionContent content)
        {
            NativeArena.ValidateDevAbuseAuthority(content);
            var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null;
            try
            {
                var match=new NativeMatch(content,7);match.Seats.Add(new NativeSeat{id=1,name="A",spec="pistol"});match.Seats.Add(new NativeSeat{id=2,name="B",spec="pistol"});
                arena=new NativeArena(content,match,"foundry"){ReplicaScene=scene};match.Start();match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});arena.ValidateDevCombatParity();arena.ValidateParityCombat();arena.ValidateFollowupParity();
                Debug.Log("PASS native combat: no generic stun, ability block rules, splash/DoT roll rules, reload-independent abilities and authority wall-clamped aim");
            }
            finally{arena?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
            CheckKernelPanic(content);CheckCyberRanged(content);
        }
        private static void CheckKernelPanic(ConversionContent content)
        {
            // Follow-up parity leaves a Siren AuthorityBoss, PvE and sandbox state behind.
            // Kernel Panic must exercise ordinary PvP seats in its own match/physics scene.
            var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null;
            try
            {
                var match=new NativeMatch(content,7);match.Seats.Add(new NativeSeat{id=1,name="A",spec="pistol"});match.Seats.Add(new NativeSeat{id=2,name="B",spec="pistol"});
                arena=new NativeArena(content,match,"foundry"){ReplicaScene=scene};match.Start();
                arena.ValidateKernelPanic();
            }
            finally{arena?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
