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
            var preferences=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(new ConversionPreferences{map="random",lobbyPose="crossed-arms",fullscreen=true}));
            if(preferences.map!="random"||preferences.lobbyPose!="crossed-arms"||!preferences.fullscreen)throw new Exception("Saved pilot pose/map/fullscreen did not round trip");
            var profile=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="ready",profile=new NativeProfile{cosmetics=new NativeCosmetics{lobbyPose="crossed-arms"}}}));
            if(profile.profile.cosmetics.lobbyPose!="crossed-arms")throw new Exception("Ready/save DTO lost lobby pose");
            var original=authority.Local.Death;
            var scene=EditorSceneManager.NewPreviewScene();NativeArena replica=null;
            try
            {
                var frame=authority.NetworkFrame();var again=authority.NetworkFrame();float saved=frame.snapshot.Actors[0].Hp;authority.Local.Pose.Hp=0;
                if(frame.snapshot.Actors[0].Hp!=saved||!ReferenceEquals(frame.props[0],again.props[0]))throw new Exception("Replay capture mutated actor history or failed to share static props");authority.Local.Pose.Hp=saved;
                var message=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=frame}));
                var copy=message.frame;
                if(message.epoch!=7||!copy.Valid(authority.Content)||copy.pve!=null||copy.pilots.Any(p=>p.death!=null||p.down!=null)||copy.props.Any(p=>p.definition!=null))
                    throw new Exception("Real JsonUtility match-frame round trip lost optional field presence");
                replica=new NativeArena(authority.Content,new NativeMatch(authority.Content),authority.MapId){Replica=true,LocalId=authority.LocalId,ReplicaScene=scene};
                replica.ApplyFrame(copy,authority.LocalId);
                if(replica.Pve!=null||replica.Root.scene!=scene)throw new Exception("PvP replay/replica escaped its isolated scene or created PvE");
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
        private static void CheckCombatParity(ConversionContent content)
        {
            var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null;
            try
            {
                var match=new NativeMatch(content,7);match.Seats.Add(new NativeSeat{id=1,name="A",spec="pistol"});match.Seats.Add(new NativeSeat{id=2,name="B",spec="pistol"});
                arena=new NativeArena(content,match,"foundry"){ReplicaScene=scene};match.Start();match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});arena.ValidateParityCombat();arena.ValidateFollowupParity();
                Debug.Log("PASS native combat: no generic stun, ability block rules, splash/DoT roll rules, reload-independent abilities and authority wall-clamped aim");
            }
            finally{arena?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
