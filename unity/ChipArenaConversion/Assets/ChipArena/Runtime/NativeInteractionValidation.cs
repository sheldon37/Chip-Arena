#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Invoked by the cloud builder against real JsonUtility, rig transforms, imported banks and isolated PhysX bodies.
        public void ValidateInteractionFeedback(NativeArena guest,ArenaPresentation hostView,ArenaPresentation guestView)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Interaction feedback: "+message);}
            // AuthorityBoss is intentionally internal to the runtime assembly. Set it here,
            // in the editor-only fixture, without exposing authority mutation to Editor/client APIs.
            Pilots.Single(p=>p.Seat.id==8).Seat.AuthorityBoss=true;
            ConfigurePve(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text),false);
            StartPve();SetBossHidden(false);var victim=Members().First();var owner=Pve.Boss;
            victim.Body.position=new Vector3(0,1,0);owner.Body.position=new Vector3(0,1,1);owner.Yaw=180;
            owner.Siren=new NativeSirenState{Move=new NativeSirenMove{Rule=NativeSirenRules.Rule("laugh"),Caught=victim,CaughtAt=Time-.3f,CrushAt=Time+2.2f,Until=Time+2.8f}};victim.HeldBy=owner;
            var held=NativeFrameCodec.Clone(NetworkFrame());Check(held.Valid(Content),"held frame invalid");guest.ApplyFrame(held,victim.Seat.id);
            var a=held.snapshot.Actors.Single(p=>p.Id==victim.Seat.id);Check(a.GrabOwner==owner.Seat.id&&a.GrabCrushAt==owner.Siren.Move.CrushAt,"catch/crush timeline lost");
            var guestPosition=guest.Local.Body.position;guest.PredictReplica(new InputFrame{Sequence=1,X=1,Jump=true});Check(guest.Local.Body.position==guestPosition,"guest predicted movement while held");victim.Input=new InputFrame{Sequence=1};
            var position=victim.Body.position;float hp=victim.Hp;
            hostView.ValidateInteractionFrame(this,Time,victim.Seat.id,true,false,false,false);
            guestView.ValidateInteractionFrame(guest,Time,victim.Seat.id,true,false,false,false);
            Check(victim.Body.position==position&&victim.Hp==hp,"visible grab moved gameplay body / health");
            var bad=NativeFrameCodec.Clone(held);bad.snapshot.Actors.Single(p=>p.Id==victim.Seat.id).GrabOwner=victim.Seat.id;Check(!bad.Valid(Content),"self-grab accepted");
            bad=NativeFrameCodec.Clone(held);bad.snapshot.Actors.Single(p=>p.Id==victim.Seat.id).GrabCrushAt=float.NaN;Check(!bad.Valid(Content),"nonfinite grab accepted");
            bad=NativeFrameCodec.Clone(held);bad.snapshot.Actors.Single(p=>p.Id==victim.Seat.id).GrabCrushAt=Time-2;Check(!bad.Valid(Content),"reversed grab accepted");
            CancelSiren(owner);var released=NativeFrameCodec.Clone(NetworkFrame());guest.ApplyFrame(released,victim.Seat.id);
            Check(released.snapshot.Actors.Single(p=>p.Id==victim.Seat.id).GrabOwner==0&&guest.Local.Pose.GrabOwner==0,"released victim remained held");
            hostView.ValidateInteractionFrame(this,Time+.02f,victim.Seat.id,false,false,false,false);guestView.ValidateInteractionFrame(guest,Time+.02f,victim.Seat.id,false,false,false,false);
            Debug.Log("PASS Siren grab feedback: exact victim/times through real codec and guest application, current-frame claw anchor and animated limbs, cancellation, malformed state rejection, authority untouched");
            // A healed, damage-resistant party member still dies at the completed catch, once.
            victim.Hp=10;Heal(victim,(float)victim.Stats["hp"]);victim.ShieldUntil=Time+10;
            double oldTaken=victim.Stats["damageTaken"];victim.Stats["damageTaken"]=.1;
            owner.Siren=new NativeSirenState{Move=new NativeSirenMove{Rule=NativeSirenRules.Rule("laugh"),Caught=victim,CaughtAt=Time-3.2f,CrushAt=Time,Until=Time+.6f}};
            victim.HeldBy=owner;StepSirenWorld();
            Check(victim.Hp==0&&victim.Death?.style=="siren-crush"&&owner.Siren.Move.Crushed,"healed PvE victim survived completed crush");
            var crushDeath=victim.Death;StepSirenWorld();Check(ReferenceEquals(crushDeath,victim.Death),"crush repeated death");
            CancelSiren(owner);victim.ShieldUntil=0;victim.Stats["damageTaken"]=oldTaken;
            Physics.SyncTransforms();var deathOrigin=victim.Death.position;
            Check(SetReviveAnchor(victim,new Vector3(0,.4f,0)),"ragdoll floor anchor refused");
            var reviveAt=victim.Body.position;Check(victim.Death.position==deathOrigin,"revive anchor rewrote immutable death pose");
            Check(!SetReviveAnchor(victim,new Vector3(float.NaN,0,0))&&victim.Body.position==reviveAt,"invalid corpse anchor moved body");
            var anchored=NativeFrameCodec.Clone(NetworkFrame());guest.ApplyFrame(anchored,victim.Seat.id);
            Check((guest.Local.Body.position-reviveAt).sqrMagnitude<.001f,"local corpse anchor retained prediction offset");
            Check(!guest.SetReviveAnchor(guest.Local,Vector3.zero),"guest accepted authority-only corpse anchor");
            // A second, nearby anchor is below both teleport thresholds. Dead input and rendering
            // must not pull the guest back to its prior position between authority frames.
            Check(SetReviveAnchor(victim,new Vector3(.5f,.4f,0)),"nearby ragdoll floor anchor refused");
            reviveAt=victim.Body.position;anchored=NativeFrameCodec.Clone(NetworkFrame());
            Check(anchored.Valid(Content),"updated corpse anchor frame invalid");guest.ApplyFrame(anchored,victim.Seat.id);
            guest.PredictReplica(new InputFrame{Sequence=2,X=1,Jump=true});guest.InterpolateReplica(.016f);
            Check((guest.Local.Body.position-reviveAt).sqrMagnitude<.001f,"dead input/interpolation displaced updated anchor");
            RevivePve(victim);Check(victim.Body.position==reviveAt,"revive returned to original kill location");
            var anchorRevived=NativeFrameCodec.Clone(NetworkFrame());guest.ApplyFrame(anchorRevived,victim.Seat.id);
            Check((guest.Local.Body.position-reviveAt).sqrMagnitude<.001f&&guest.predictor.Pending==0,"revival retained corpse offset or dead input");
            victim.Hp=0;guest.ApplyFrame(NativeFrameCodec.Clone(NetworkFrame()),victim.Seat.id);
            var nearbyPad=reviveAt+Vector3.right*.5f;RevivePve(victim,nearbyPad);
            guest.ApplyFrame(NativeFrameCodec.Clone(NetworkFrame()),victim.Seat.id);
            Check((guest.Local.Body.position-nearbyPad).sqrMagnitude<.001f,"nearby pad revival retained corpse smoothing");
            int seed=300;
            foreach(string style in new[]{"impact-ragdoll","dev-gibber","siren-crush","pad"})
            {
                hostView.ResetPresentation();guestView.ResetPresentation();for(int tick=0;tick<8;tick++)Match.Tick(.25);
                var wound=style=="dev-gibber"?new NativeWound{id=1,part=CapturedRig.Head,at=Time-2,radius=.18f,severity=1,kind="sever",destroyed=true}:null;
                var actor=victim.Pose.ViewCopy();actor.Wounds=wound==null?Array.Empty<NativeWound>():new[]{wound};
                victim.Death=new NativeDeath{id=victim.Seat.id,seed=seed++,kept=true,model="pilot",at=Time-5,position=new Vector3(0,1,0),style=style=="pad"?"impact-ragdoll":style,actor=actor,wound=wound};
                victim.Hp=0;victim.Body.position=victim.Death.position;victim.Seat.alive=false;RevivePve(victim,style=="pad"?(Vector3?)new Vector3(5,1,0):null);
                var revived=NativeFrameCodec.Clone(NetworkFrame());Check(revived.Valid(Content),"revive frame invalid: "+style);guest.ApplyFrame(revived,victim.Seat.id);
                Check((guest.Local.Body.position-victim.Body.position).sqrMagnitude<.001f,"revive placement retained local smoothing: "+style);guest.InterpolateReplica(1);
                Check(guest.Local.RevivedAt==victim.RevivedAt&&guest.Local.Death?.seed==victim.Death.seed,"revive identity lost: "+style);
                position=victim.Body.position;hp=victim.Hp;bool place=style!="pad",missing=style=="dev-gibber"||style=="siren-crush";
                foreach(float age in new[]{-.15f,.02f,.2f,.65f,.81f,1.1f,1.41f})
                {
                    hostView.ValidateInteractionFrame(this,Time+age,victim.Seat.id,false,age<1.4f,place,missing);
                    guestView.ValidateInteractionFrame(guest,Time+age,victim.Seat.id,false,age<1.4f,place,missing);
                }
                Check(victim.Body.position==position&&victim.Hp==hp&&victim.Collider.enabled&&victim.Wounds.Count==0,"repair changed revival rules: "+style);
                // A second death stops the animation, including its glow; no stale body survives a reset.
                victim.Hp=0;hostView.ValidateInteractionFrame(this,Time+.3f,victim.Seat.id,false,false,false,false);victim.Hp=hp;
            }
            Debug.Log("PASS PvE repair feedback: intact/gibbed/Siren-crushed bodies and distant pads, host + guest scan/voxels/stand-up, interpolation lead, expiry/redeath/reset, real rig matrices finite, health/colliders unchanged (draws prepared; no visual acceptance claimed)");
        }
    }
    public sealed partial class ArenaPresentation
    {
        public void ValidateInteractionFrame(NativeArena arena,float time,int id,bool held,bool reviving,bool place,bool missing)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Interaction presentation: "+message);}
            WeaponLoadout=arena;sourceModels.Begin();var snapshot=arena.Snapshot();
            foreach(var a in snapshot.Actors)if(a.Alive)PrepareCapturedPose(a,Vec(a.Position),time);
            var target=snapshot.Actors.First(p=>p.Id==id);var rig=rigs[id];var neutral=rig.Bones[CapturedRig.LArm].localRotation;
            PrepareInteractions(snapshot,time);RenderRevives(arena);
            if(held)
            {
                var owner=rigs[target.GrabOwner];var expected=owner.Bones[CapturedRig.RHand].TransformPoint(new Vector3(0,-.36f,.16f))-Vector3.up*(.44f+.05f*(1-NativeSirenVictimPose.Lift(time-target.GrabAt)));
                Check(Vector3.Distance(rig.Root.transform.position,expected)<.0001f,"victim left the current claw");Check(Quaternion.Angle(rig.Bones[CapturedRig.LArm].localRotation,neutral)>10,"victim kept idle arms");
            }
            var pilot=PilotOf(arena,id);float age=time-pilot.RevivedAt;
            if(reviving)
            {
                Check(ReviveVisible(id,time)==(age>=NativeReviveRules.Scan),"live body visibility / duplicate corpse");
                if(age>=NativeReviveRules.Settle)
                {
                    Check(revivals.TryGetValue(id,out var r)&&r.Place==place,"body / pad selection");
                    Check((!place||missing)?r.Cells.Count>0:r.Cells.Count==0,"repair voxel selection");Check(r.Cells.Count<=360,"repair budget");
                    if(place&&age>=NativeReviveRules.Scan)Check(r.StandPosition!=null,"standing pose was not inherited");
                }
            }
            else {Check(!revivals.ContainsKey(id)&&ReviveVisible(id,time),"stale regeneration");if(!held&&target.Alive)Check(Vector3.Distance(rig.Root.transform.position,Vec(target.Position)+Vector3.up*.865f)<.001f,"released root remained on the claw");}
            foreach(var bone in rig.Bones){var m=bone.localToWorldMatrix;for(int n=0;n<16;n++)Check(MathUtil.Finite(m[n]),"nonfinite interaction bone");}
        }
    }
}
#endif
