#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Runs under the existing real-Unity gate. No production token bypass exists outside this fixture.
        public static void ValidateDevAbuseAuthority(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("DEV Abuse native: "+why);}
            foreach(bool online in new[]{false,true})
            {
                var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null,guest=null;
                try
                {
                    var match=new NativeMatch(content,11);var victim=new NativeSeat{id=1,name="Victim"};var dev=new NativeSeat{id=2,name="Dev"};match.Seats.Add(victim);match.Seats.Add(dev);
                    var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});dev.access=grant;
                    arena=new NativeArena(content,match,"foundry"){ReplicaScene=scene,LocalId=online?1:2};match.Start();match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});
                    int n=0;double clock=0;
                    NativeDevReply Send(string action,int target=1,params string[] args)
                    {
                        var message=new NativeNetMessage{type="dev-command",epoch=7,generation=arena.Generation,devCommand=new NativeDevCommand{transaction="native-"+(++n),action=action,target=target,args=args}};
                        if(online)message=NativeNetCodec.Decode(NativeNetCodec.Encode(message));
                        var reply=arena.DevCommand(dev,message.devCommand,message.epoch,message.generation,7,++clock);
                        if(online)reply=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="dev-reply",epoch=7,devReply=reply})).devReply;
                        Check(reply.state=="done",action+": "+reply.reason);return reply;
                    }
                    NativePilot Target()=>arena.Pilots.Find(p=>p.Seat.id==1);
                    Check(!dev.devRevealed&&!victim.devRevealed,"hidden defaults");
                    var denied=arena.DevCommand(victim,new NativeDevCommand{transaction="denied",action="restore"},7,arena.Generation,7,0);Check(denied.state=="refused"&&!string.IsNullOrEmpty(denied.reason),"non-dev refusal reason");
                    var savedAccess=victim.access;var savedProfile=new NativeProfile();string profile=JsonUtility.ToJson(savedProfile);
                    Send("god",1,"on");float hp=Target().Hp;arena.Damage(Target(),null,10000,Vector3.zero,0,false,forced:true);Check(Target().Hp==hp,"god prevents forced damage");Send("god",1,"off");
                    Send("infinite-ammo",1,"on");Target().Ammo=0;arena.DevStep();Check(Target().Ammo==arena.Magazine(Target()),"infinite ammo");Send("infinite-ammo",1,"off");
                    Send("no-cooldowns",1,"on");Target().QReady=1000;arena.DevStep();Check(Target().QReady==arena.Time,"cooldown bypass");Send("no-cooldowns",1,"off");
                    var chip=NativeContentRules.Eligible(content,"pistol",victim.chips).First(c=>c.max>0);Send("chip-add",1,chip.id,"1");Check(victim.chips[chip.id]==1,"chip added");Send("chip-remove",1,chip.id,"1");Check(!victim.chips.ContainsKey(chip.id),"chip removed");
                    Send("weapon",1,"rifle");Check(victim.spec=="rifle","weapon set");Send("swap-weapon",1,"pistol");Check(victim.access==savedAccess,"weapon never changes identity grant");
                    Send("freeze",1,"2");Check(Target().FrozenUntil==arena.Time+2,"freeze deadline");Send("launch",1,"20");Check(Target().Knockback.y==20&&Target().FrozenUntil==0,"launch clears freeze");
                    var point=Target().Body.position;string[] xyz={point.x.ToString(System.Globalization.CultureInfo.InvariantCulture),point.y.ToString(System.Globalization.CultureInfo.InvariantCulture),point.z.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                    Send("send",1,xyz);Send("teleport",1,xyz);Send("bring");
                    float radius=Target().Collider.radius;Send("shrink");Check(victim.devScale==.5f&&Target().Collider.radius==radius,"visual shrink, stable hit volume");Send("grow");Check(victim.devScale==2&&Target().Collider.radius==radius,"visual grow");
                    Send("spawn-dummy",1,"pistol","primary","3");Check(arena.Pilots.Count(p=>p.Seat.devDummy&&p.Seat.connected)==1,"dummy spawn");Send("spawn-dummies-around",1,"2");Check(arena.Pilots.Count(p=>p.Seat.devDummy&&p.Seat.connected)==3,"around spawn");Send("clear-dummies");Check(!arena.Pilots.Any(p=>p.Seat.devDummy&&p.Seat.connected),"clear only created dummies");
                    Send("spawn-dummy",1,"pistol","idle","2");Check(arena.Pilots.Count==5,"cleared slot reused");Send("clear-dummies");
                    string material=content.materials.First(m=>!m.indestructible).id;int props=arena.Props.Count;Send("debris",1,"8","15",material,"edge");Send("debris-rain",1,material);Check(arena.Props.Count==props+7,"debris emitted");
                    Send("time-scale",1,"0.5");Check(arena.DevTimeScale==.5f,"authoritative clock");
                    Send("down");Check(Target().Hp==0,"down");Send("restore");Check(Target().Hp>0&&Target().Seat.alive,"restore");
                    Send("reveal");Check(dev.devRevealed&&!victim.devRevealed,"reveal only sender");
                    Check(dev.devCommandsRan&&!victim.devCommandsRan&&!match.CanEarnProgression(2)&&match.CanEarnProgression(1),"progression eligibility");Check(JsonUtility.ToJson(savedProfile)==profile&&victim.access==savedAccess,"account profile and grant unchanged");
                    var frame=NativeFrameCodec.Clone(arena.NetworkFrame());Check(frame.Valid(content),"DEV frame valid");var legacy=NativeFrameCodec.Clone(frame);legacy.devTimeScale=0;foreach(var s in legacy.seats)s.devScale=0;legacy.RestorePresence();Check(legacy.devTimeScale==1&&legacy.seats.All(s=>s.devScale==1),"missing legacy scales normalize");
                    guest=new NativeArena(content,new NativeMatch(content),arena.MapId){ReplicaScene=scene};guest.ApplyFrame(frame,1);Check(guest.Match.Seat(2).devRevealed&&guest.Match.Seat(1).devScale==2&&guest.DevTimeScale==.5f,"guest reveal, scale and clock");
                    var deniedReplica=guest.DevCommand(dev,new NativeDevCommand{transaction="replica",action="restore"},7,guest.Generation,7,50);Check(deniedReplica.state=="refused","replica cannot execute");
                    Send("arena-reset");Check(dev.devCommandsRan&&dev.devRevealed,"reset does not restore receipt eligibility or hide reveal");
                    // Real authored hazard geometry, independently of ordinary scheduled hazard clocks.
                    arena.MapId=content.maps.First(m=>m.hazards?.Length>0).id;arena.Reset();
                    string hazard=arena.Map.hazards[0].id;Send("god",1,"on");Send("hazard-at");Send("hazard-trigger",1,hazard);Check(arena.DevHazardPulses.Count==2,"hazard pulses");Check(NativeFrameCodec.Clone(arena.NetworkFrame()).Valid(content),"authored-map DEV frame");
                    Send("round-end");Check(match.Phase==NativePhase.Result,"round ended");
                    arena.Dispose();arena=null;Check(victim.spec=="pistol"&&victim.devScale==1&&!dev.devRevealed,"match cleanup");
                }
                finally{guest?.Dispose();arena?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
            }
            Debug.Log("PASS DEV Abuse native authority: local and encoded guest commands, every action, replicas and account isolation");
        }
    }
}
#endif
