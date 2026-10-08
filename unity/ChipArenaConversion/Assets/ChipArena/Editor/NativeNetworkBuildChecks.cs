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
                arena=new NativeArena(content,match,"foundry"){ReplicaScene=scene};match.Start();match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});arena.ValidateParityCombat();
                Debug.Log("PASS native combat: no generic stun, ability block rules, splash/DoT roll rules, reload-independent abilities and authority wall-clamped aim");
            }
            finally{arena?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
