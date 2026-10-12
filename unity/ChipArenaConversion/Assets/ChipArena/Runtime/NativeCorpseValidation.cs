#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    internal sealed partial class NativeCorpse
    {
        internal void CheckCorpseSupport(NativePropBody support)
        {
            if(!obstacles.TryGetValue(support,out var copy)||!copy.enabled||copy.gameObject.scene!=scene)
                throw new Exception("Corpse omitted isolated support: "+support.name);
            var source=support.GetComponent<BoxCollider>();
            if(copy.center!=source.center||copy.size!=source.size||copy.transform.position!=source.transform.position||copy.transform.localScale!=source.transform.lossyScale)
                throw new Exception("Corpse support shape differs: "+support.name);
        }
        internal bool SettledForCheck=>frozen&&Parts.Values.All(b=>b.isKinematic)&&(weapon==null||weapon.isKinematic);
    }
    public sealed partial class NativeArena
    {
        // This is a real Unity/PhysX build gate, not exercised by the managed rule tests or API compilation.
        public void ValidateCorpsePhysics()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Corpse physics: "+why);}
            var pilot=Local;var saved=pilot.Death;NativePropBody lower=null,upper=null,rubble=null;
            using(var clips=new CapturedAnimation())using(var rig=clips.Create(Root.transform,pilot.Seat.id))
            try
            {
                var slab=StaticBlocks.First(p=>p.Definition.id=="frontage-floor");
                // Clear southeast lane; the origin is inside the hotel stairwell.
                var position=new Vector3(25,3,20);var actor=pilot.Pose.ViewCopy();actor.SourceSpec="pistol";
                NativeDeath Death(Vector3 at,int seed)=>new NativeDeath{id=pilot.Seat.id,seed=seed,position=at,style="impact-ragdoll",model="pilot",actor=actor,at=Time};
                rig.ActorPose(actor,position-Vector3.up*.865f,Time);
                using(var body=new NativeCorpse(Death(position,813),rig,this))
                {
                    body.CheckCorpseSupport(slab);Check(body.RetainUntilRoundEnd,"PvP pilot lifetime");
                    body.Step(60);Check(body.SettledForCheck,"late join catches up and freezes body and dropped weapon");
                    Check(body.Center.y>-.1f&&body.Center.y<2,"Brickfall slab catches the ragdoll");
                    Check(body.Parts.Values.All(b=>MathUtil.Finite(b.position.y)&&b.position.y>-.5f),"no part falls through Brickfall");
                }
                pilot.Death=Death(pilot.Body.position,815);var frame=NativeFrameCodec.Clone(NetworkFrame());frame.time+=60;
                Check(frame.Valid(Content)&&frame.pilots.Any(p=>p.death?.seed==815&&frame.time-p.death.at>5),"old pilot death survives late-join codec");
                // A broad slab whose centre is 30 m away, appended after all 507 map blocks. Then an upper floor is
                // removed under the settled body and replaced by rubble. This catches centre-distance and Take(48) bugs.
                lower=AddProp(new ContentBlock{id="corpse-check-slab",material="foundation",role="static",pos=new V3(1000,-.5f,1000),size=new V3(80,1,80),rotation=new ContentRotation()});
                upper=AddProp(new ContentBlock{id="corpse-check-floor",material="foundation",role="carried",pos=new V3(1030,5.5f,1000),size=new V3(8,1,8),rotation=new ContentRotation()});
                position=new Vector3(1030,8,1000);rig.ActorPose(actor,position-Vector3.up*.865f,Time);Physics.SyncTransforms();
                using(var body=new NativeCorpse(Death(position,814),rig,this))
                {
                    body.CheckCorpseSupport(lower);body.CheckCorpseSupport(upper);
                    for(int i=0;i<=660;i++)body.Step(i/60f);
                    Check(body.SettledForCheck&&body.Center.y>5.5f&&body.Center.y<8,"settled on upper floor beyond five seconds");
                    upper.GetComponent<Collider>().enabled=false;
                    rubble=AddProp(new ContentBlock{id="corpse-check-rubble",material="foundation",role="rubble",pos=new V3(1030,.5f,1000),size=new V3(8,1,8),rotation=new ContentRotation()});Physics.SyncTransforms();
                    body.Step(11.2f);Check(!body.SettledForCheck,"support change wakes retained body");body.CheckCorpseSupport(rubble);
                    for(int i=673;i<=960;i++)body.Step(i/60f);
                    Check(body.Center.y>.7f&&body.Center.y<3,"body falls onto replacement rubble");
                    Check(pilot.Body.gameObject.scene!=body.Parts[1].gameObject.scene,"corpse remains separate from authority");
                }
                Debug.Log("PASS Brickfall corpses: static slab, late join, long-lived body/weapon, distant support, collapse wake/rubble, isolated authority and death snapshot");
            }
            finally
            {
                pilot.Death=saved;
                foreach(var prop in new[]{lower,upper,rubble})if(prop!=null){Props.Remove(prop);StaticBlocks.Remove(prop);CapturedAnimation.Release(prop.gameObject);}
                Physics.SyncTransforms();
            }
        }
    }
}
#endif
