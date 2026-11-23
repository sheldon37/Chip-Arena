using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Physical hit reactions on the drawn models (owner 2026-11-21: "Utilize Ragdoll Asset for hit reactions"; Core
    // NativeHitPuppetRules, NativeHitPuppets.cs). A pilot (player or bot) or an add drawn on the model that takes a non-lethal hit
    // has its drawn pose pushed by an active ragdoll: the muscles near the hit unpin, a push lands at the hit point along the blow,
    // scaled to it, and the pose regains. Presentation only:
    //  - The hits are the replicated events every client already reads: a pilot's from the frame's events (ObserveHits, the same
    //    ones the felt-hit ledger reads), an add's from HordeHit; each lands on its body's own clock (the rig's render time, the
    //    horde's field clock), so it lines up with the existing flinch, stagger and flash. A killing hit pushes nothing (the
    //    death's own presentation takes the body).
    //  - Where and from where (owner 2026-11-23: "I want the animation to actually display hit direction (Like reactively)"): the
    //    push lands at the authority's hit point, measured from the body centre (a pilot's rig root: Actor.Position is its feet), along
    //    the shot's path; a blade's blow along its own swing, where its blade crosses (the swing the attacker played, as a kill's cut
    //    reads it: HitPuppetSwing); a headshot on the head's side the shot came from. How far it reaches (Core
    //    NativeHitPuppetRules.Spread: a pistol round's flick stays local, a greatsword's heavy blow bends the whole body, the hips as
    //    well) and an add's give (Loose: a shambler's the loosest) go to the bridge with it (NativeHitPuppets.Strike).
    //  - Each puppet steps right after its body is posed this frame (VisualBodyPose, ZombieModelBind) and before anything reads its
    //    bones, so hats, gear, the held weapon (the rig's hands follow the drawn hands: HitPuppetHands), effects and cloth follow
    //    the bent pose. The bridge bends the drawn bones' rotations, and the hips a little (a dip, a shift) with the feet kept where
    //    the game planted them and the bent body inside its hitbox (NativeHitPuppets.Bounds); the model's root stays where the game
    //    put it; nothing it does is read by a rule, a hit volume, the camera or the wire. The local pilot in first person draws no
    //    body (VisualBodyPose), so its view never moves with a reaction.
    //  - A pilot killed while its puppet bends it falls from that drawn pose (VisualRigFromDrawn copies the drawn bones), so the
    //    hand-over to the corpse does not snap; a dying add's puppet blends back while its fall plays.
    //  - The cost is capped (Core NativeHitPuppetRules): bodies reacting at once by tier, within its reach of the camera, none on
    //    the low tier (today's look), one puppet made a frame, sixteen kept made (a sleeping one costs nothing a frame); a puppet
    //    sleeps the frame its body is not drawn.
    // Without the local bridge every body draws as posed.
    public sealed partial class ArenaPresentation
    {
        private sealed class HitPuppet{public object Handle;public VisualBody Body;public long Owner=long.MinValue,Shaped=long.MinValue;public int PosedFrame=-10;public float HitAt;public Vector3 At;}
        // A headshot's point: the head's centre this far out along the neck, this far back toward the shot (m).
        private const float HitHeadCentre=.09f,HitHeadSide=.08f;
        private struct HitPuppetHit{public bool Zombie,Head;public int Id;public float At,Impulse,Unpin,Spread,Loose,Queued;public Vector3 Offset,Dir;}
        private readonly Dictionary<VisualBody,HitPuppet> hitPuppets=new Dictionary<VisualBody,HitPuppet>();
        private readonly List<VisualBody> hitPuppetsGone=new List<VisualBody>();
        private const int HitPuppetQueue=32;
        private readonly HitPuppetHit[] hitPuppetHits=new HitPuppetHit[HitPuppetQueue];private int hitPuppetHitCount,hitPuppetEvent,hitPuppetMakeFrame=-1,hitPuppetMakes;
        private bool hitPuppetsFailed;
        // The hands' drawn frames before a step (HitPuppetHands).
        private readonly Vector3[] hitPuppetHandAt=new Vector3[2];private readonly Quaternion[] hitPuppetHandTurn=new Quaternion[2];
        /// <summary>Diagnostics (the local tool's perf line): puppets made, and those reacting this frame.</summary>
        public int HitPuppetsMade=>hitPuppets.Count;
        public int HitPuppetsAwake{get{int n=0;foreach(var p in hitPuppets.Values)if(HitPuppetAwakeNow(p.Handle))n++;return n;}}
        private bool HitPuppetsOn=>!hitPuppetsFailed&&!Viewer&&NativeHitPuppets.Registered;
        private int HitPuppetCap=>LowGraphics?0:NativeHitPuppetRules.Cap(GraphicsTier,EffectsQuality<.5f);
        private bool HitPuppetEye(out Vector3 eye)
        {
            var camera=VisualEye!=null?VisualEye:WorldEye;eye=camera!=null?camera.transform.position:default;return camera!=null;
        }
        private bool HitPuppetNear(Vector3 at,float reach)=>!HitPuppetEye(out var eye)||(eye-at).sqrMagnitude<=reach*reach;
        // The bridge's calls; an exception turns the puppets off for the session (the bodies draw as posed).
        private void HitPuppetFailed(Exception e){hitPuppetsFailed=true;Debug.LogWarning("Hit puppets: the bridge failed and the reactions are off: "+e.Message);}
        private bool HitPuppetAwakeNow(object h){if(h==null||!HitPuppetsOn)return false;try{return NativeHitPuppets.Awake(h);}catch(Exception e){HitPuppetFailed(e);return false;}}
        private void HitPuppetSleep(object h){if(h==null||!HitPuppetsOn)return;try{NativeHitPuppets.Sleep(h);}catch(Exception e){HitPuppetFailed(e);}}
        private void HitPuppetFade(object h){if(h==null||!HitPuppetsOn)return;try{NativeHitPuppets.Fade(h);}catch(Exception e){HitPuppetFailed(e);}}
        private bool HitPuppetStep(object h,float dt){if(h==null||!HitPuppetsOn)return false;try{return NativeHitPuppets.Step(h,dt);}catch(Exception e){HitPuppetFailed(e);return false;}}
        // A bridge with Strike takes the blow's reach and the body's give; the first one (an older licensed-assets zip) Hit alone.
        private bool HitPuppetPush(object h,Vector3 point,Vector3 push,float unpin,float spread,float loose){if(h==null||!HitPuppetsOn)return false;try{return NativeHitPuppets.Strike!=null?NativeHitPuppets.Strike(h,point,push,unpin,spread,loose):NativeHitPuppets.Hit(h,point,push,unpin);}catch(Exception e){HitPuppetFailed(e);return false;}}
        private void HitPuppetBounds(object h,float radius,float height){if(h==null||!HitPuppetsOn||NativeHitPuppets.Bounds==null)return;try{NativeHitPuppets.Bounds(h,radius,height);}catch(Exception e){HitPuppetFailed(e);}}
        private object HitPuppetMake(Transform skeleton){if(skeleton==null||!HitPuppetsOn)return null;try{return NativeHitPuppets.Attach(skeleton);}catch(Exception e){HitPuppetFailed(e);return null;}}
        private void HitPuppetEnqueue(in HitPuppetHit h)
        {
            if(hitPuppetHitCount==HitPuppetQueue){Array.Copy(hitPuppetHits,1,hitPuppetHits,0,HitPuppetQueue-1);hitPuppetHitCount--;}
            hitPuppetHits[hitPuppetHitCount++]=h;
        }
        private bool HitPuppetMakeLeft=>hitPuppetMakeFrame!=Time.frameCount||hitPuppetMakes<NativeHitPuppetRules.MakesPerFrame;
        private void HitPuppetDequeue(int i){Array.Copy(hitPuppetHits,i+1,hitPuppetHits,i,hitPuppetHitCount-i-1);hitPuppetHitCount--;}
        private static Actor HitPuppetActor(Actor[] actors,int id){if(actors==null||id<=0)return null;foreach(var a in actors)if(a!=null&&a.Id==id)return a;return null;}

        // ObserveHits: this frame's new hits on living pilots (each once), queued for their bodies.
        private void HitPuppetsObserve(Snapshot s)
        {
            var events=s?.Events;if(events==null||!HitPuppetsOn)return;int newest=0;
            for(int i=0;i<events.Length;i++)if(events[i].Id>newest)newest=events[i].Id;
            // Event ids restart with a new arena (a new match, the killcam's replica): start over.
            if(newest>0&&newest<hitPuppetEvent){hitPuppetEvent=0;hitPuppetHitCount=0;}
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(e.Id<=hitPuppetEvent||e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot||e.Target<=0||e.Target>=10000||!(e.Amount>0))continue;
                var target=HitPuppetActor(s.Actors,e.Target);if(target==null||!target.Alive)continue;
                // A killing hit pushes nothing: its Death is in the same tick.
                bool lethal=false;for(int k=0;k<events.Length&&!lethal;k++)lethal=events[k].Kind==EventKind.Death&&events[k].Target==e.Target&&Math.Abs(events[k].Time-e.Time)<.004f;
                if(lethal)continue;
                var attacker=e.Source>0?HitPuppetActor(s.Actors,e.Source):null;string spec=e.Source==0?null:attacker?.SourceSpec;
                var w=NativeStaggerRules.Classify(spec,string.IsNullOrEmpty(e.DamageKind)?e.Detail??"direct":e.DamageKind,e.Amount);
                bool melee=NativeHitRules.Melee(e.Source,e.Detail,spec,false,Math.Abs(target.ReactionAt-e.Time)<.004f);
                // A blade class's cutting move (the Katana's, the Fangs', the Glaive's: Core NativeBladeCuts) is a blade's blow, its detail the move.
                melee|=NativeBladeCuts.Cuts(e,attacker);
                if(!NativeHitPuppetRules.Felt(e.Detail,melee))continue;
                bool head=e.Kind==EventKind.Headshot;float heft=NativeHitRules.Heft(spec,e.Amount);
                if(!NativeHitPuppetRules.Push(w,NativeStaggerRules.Scale(w,e.Amount),melee,heft,head,out float impulse,out float unpin))continue;
                // The hit point from the body centre, the rig root the pose hangs from (Actor.Position is the feet: measured from there,
                // every point rose .865 m and landed on the chest or the head), and the push along the shot's path or from the attacker.
                Vector3 centre=Vec(target.Position)+Vector3.up*NativeHitPuppetRules.CentreHeight,point=Vec(e.Position),dir=Vec(e.Normal);
                if(dir.sqrMagnitude<1e-6f&&attacker!=null)dir=centre-Vec(attacker.Position)-Vector3.up*NativeHitPuppetRules.CentreHeight;if(dir.sqrMagnitude<1e-6f)dir=centre-Vec(e.From);if(dir.sqrMagnitude<1e-6f)continue;
                // A blade's blow along its own swing (the authority's point is the hit volume's nearest the attacker's waist, its push
                // straight from the attacker, whichever way the blade travelled).
                bool swung=melee&&!head&&hitLedger.Blow(e.Target,e.Time,out var blow)&&HitPuppetSwing(blow,e.Id,centre,target.Yaw,ref dir,ref point);
                float spread=NativeHitPuppetRules.Spread(w,melee,heft);
                HitPuppetEnqueue(new HitPuppetHit{Zombie=false,Id=e.Target,At=e.Time,Impulse=impulse,Unpin=unpin,Spread=spread,Loose=0,Head=head,Offset=point-centre,Dir=dir.normalized,Queued=Time.unscaledTime});
                if(NativeHitPuppets.Trace!=null)HitPuppetTrace(false,e,spec,w,melee,swung,centre,target.Yaw,point,dir,impulse,unpin,spread,0);
            }
            if(newest>hitPuppetEvent)hitPuppetEvent=newest;
        }
        // HordeHit: a replicated hit on a living add, queued for its body (`floor`: the add's authority floor point; `push`: the
        // horde's push direction when the hit carries none; `kind`: its horde kind, how loose it is; `blow`: a blade's blow on it,
        // its swing, when `hasBlow`).
        private void HitPuppetAdd(NativeMinion m,in ArenaEvent e,StaggerWeight w,bool melee,string spec,Vector3 floor,Vector3 push,int kind,bool hasBlow,in MeleeBlow blow)
        {
            if(!HitPuppetsOn||m==null||!(m.Hp>0)||!NativeHitPuppetRules.Felt(e.Detail,melee))return;
            bool head=e.HasHordeHit?e.Head:e.Kind==EventKind.Headshot;float heft=NativeHitRules.Heft(spec,e.Amount);
            if(!NativeHitPuppetRules.Push(w,NativeStaggerRules.Scale(w,e.Amount),melee,heft,head,out float impulse,out float unpin))return;
            Vector3 dir=NativeArena.Vec(e.Normal),point=NativeArena.Vec(e.Position);if(dir.sqrMagnitude<1e-6f)dir=push;if(dir.sqrMagnitude<1e-6f)return;
            bool swung=melee&&!head&&hasBlow&&HitPuppetSwing(blow,e.Id,m.Body.position,m.Yaw,ref dir,ref point);
            float spread=NativeHitPuppetRules.Spread(w,melee,heft),loose=NativeHitPuppetRules.Loose(kind);
            HitPuppetEnqueue(new HitPuppetHit{Zombie=true,Id=m.Id,At=e.Time,Impulse=impulse,Unpin=unpin,Spread=spread,Loose=loose,Head=head,Offset=point-floor,Dir=dir.normalized,Queued=Time.unscaledTime});
            if(NativeHitPuppets.Trace!=null)HitPuppetTrace(true,e,spec,w,melee,swung,m.Body.position,m.Yaw,point,dir,impulse,unpin,spread,loose);
        }
        // An add's hitbox (its capsule, world size): false when its add is not known here (a pilot's capsule stands in).
        private bool HitPuppetAddShape(int id,out float radius,out float height)
        {
            radius=height=0;var m=hordeKindArena!=null?HordeMinion(hordeKindArena,id):null;var capsule=m?.Collider;if(capsule==null)return false;
            var size=capsule.transform.lossyScale;radius=capsule.radius*Mathf.Max(Mathf.Abs(size.x),Mathf.Abs(size.z));height=Mathf.Max(capsule.height*Mathf.Abs(size.y),2*radius);
            return radius>.05f;
        }
        // A blade's blow (on a pilot or an add, its body centre `centre` facing `yaw` degrees): its push (`push`, the authority's blow
        // direction in) turned along the blade's travel and its point where the blade crosses the body, from the swing the attacker
        // played (Core NativeMeleeSlice.Plan, as a kill's cut reads it: the attacker's cut for its attack turned by its committed yaw;
        // a blade class's own slash; NativeHitPuppetRules.Swing). False, both kept, without a cut to read (a hit no cut makes).
        private readonly SlicePlan hitPuppetPlan=new SlicePlan();
        private bool HitPuppetSwing(in MeleeBlow blow,int seed,Vector3 centre,float yaw,ref Vector3 push,ref Vector3 point)
        {
            if(!NativeMeleeSlice.Covers(blow.Spec))return false;
            NativeMeleeSlice.Plan(blow,true,blow.Spec,null,NativeArena.Vec(centre),yaw,seed,hitPuppetPlan);
            if(!NativeHitPuppetRules.Swing(new System.Numerics.Vector3(push.x,push.y,push.z),hitPuppetPlan.Travel,new System.Numerics.Vector3(centre.x,centre.y,centre.z),hitPuppetPlan.Height,NativeHitPuppetRules.CentreHeight,out var p,out var q))return false;
            push=new Vector3(p.X,p.Y,p.Z);point=new Vector3(q.X,q.Y,q.Z);return true;
        }
        // The Editor's diagnostics line for a hit (NativeHitPuppets.Trace, set only by the local bridge in the Editor): the point and
        // push in the struck body's frame (x its right, y up, z ahead, from its centre).
        private void HitPuppetTrace(bool zombie,in ArenaEvent e,string spec,StaggerWeight w,bool melee,bool swung,Vector3 centre,float yaw,Vector3 point,Vector3 dir,float impulse,float unpin,float spread,float loose)
        {
            var face=Quaternion.Inverse(Quaternion.Euler(0,yaw,0));Vector3 at=face*(point-centre),push=face*dir.normalized;
            NativeHitPuppets.Trace("HPQ "+(zombie?"add ":"pilot ")+e.Target+" t "+e.Time.ToString("0.000")+" "+e.Kind+" src "+e.Source+" "+(spec??"-")+" "+(e.Detail??"-")+" "+w+(melee?" melee":"")+(swung?" swung":"")+" yaw "+yaw.ToString("0")
                +" | event pos "+NativeArena.Vec(e.Position).ToString("F2")+" normal "+NativeArena.Vec(e.Normal).ToString("F2")+" | point "+at.ToString("F2")+" push "+push.ToString("F2")+" J "+impulse.ToString("0.0")+" unpin "+unpin.ToString("0.00")+" spread "+spread.ToString("0.00")+" loose "+loose.ToString("0.00"));
        }

        /// <summary>Whether `body`'s puppet is reacting (a zombie's is then posed every frame: ZombieModelBind's urgent).</summary>
        private bool HitPuppetAwake(VisualBody body)=>body!=null&&hitPuppets.Count>0&&hitPuppets.TryGetValue(body,out var p)&&HitPuppetAwakeNow(p.Handle);

        // Right after `body` is posed this frame (owner `owner`: a pilot's seat, an add's negated id; at `time` on its clock, its
        // hits measured from `anchor`): its due hits land, then its puppet steps and bends the drawn pose; true when it stepped. A
        // dying body's blends back.
        private bool HitPuppetPose(VisualBody body,long owner,bool zombie,int id,float time,Vector3 anchor,bool dying)
        {
            if(!HitPuppetsOn||body?.Root==null||body.Binding==null)return false;
            hitPuppets.TryGetValue(body,out var slot);int frame=Time.frameCount;
            // A gap in its frames, another owner (a spare zombie body), or a jump: back to the drawn pose first.
            if(slot!=null&&(slot.PosedFrame<frame-1||slot.Owner!=owner||(anchor-slot.At).sqrMagnitude>NativeHitPuppetRules.Jump*NativeHitPuppetRules.Jump))HitPuppetSleep(slot.Handle);
            for(int i=0;i<hitPuppetHitCount;)
            {
                var h=hitPuppetHits[i];
                if(h.Zombie!=zombie||h.Id!=id){i++;continue;}
                // It waits for its moment on the body's clock (a clock far behind it: it lands now).
                if(h.At>time+1f/60&&h.At<time+NativeHitPuppetRules.Ahead){i++;continue;}
                // A body that needs a puppet made waits for the next frame's making (a volley on a crowd lands body by body).
                if(slot?.Handle==null&&!HitPuppetMakeLeft&&!dying&&time-h.At<=NativeHitPuppetRules.Stale){i++;continue;}
                HitPuppetDequeue(i);
                if(dying||time-h.At>NativeHitPuppetRules.Stale)continue;
                HitPuppetLand(body,ref slot,owner,anchor,h);
                if(!HitPuppetsOn)return false;
            }
            if(slot?.Handle==null)return false;
            slot.Owner=owner;slot.PosedFrame=frame;slot.At=anchor;
            if(!HitPuppetAwakeNow(slot.Handle))return false;
            if(dying||HitPuppetCap==0||!HitPuppetNear(anchor,NativeHitPuppetRules.Reach(GraphicsTier)*1.2f))HitPuppetFade(slot.Handle);
            HitPuppetHandsBefore(body);
            HitPuppetStep(slot.Handle,Mathf.Min(Time.deltaTime,1f/30));
            return true;
        }
        // One hit on `body`: a puppet made for it if it has none (within the cap, the reach and the frame's making), then the push.
        private void HitPuppetLand(VisualBody body,ref HitPuppet slot,long owner,Vector3 anchor,in HitPuppetHit h)
        {
            int cap=HitPuppetCap;if(cap<=0||!HitPuppetNear(anchor,NativeHitPuppetRules.Reach(GraphicsTier)))return;
            if(!HitPuppetAwakeNow(slot?.Handle)){int awake=0;foreach(var p in hitPuppets.Values)if(HitPuppetAwakeNow(p.Handle))awake++;if(awake>=cap)return;}
            if(slot?.Handle==null)
            {
                if(hitPuppetMakeFrame!=Time.frameCount){hitPuppetMakeFrame=Time.frameCount;hitPuppetMakes=0;}
                if(hitPuppetMakes>=NativeHitPuppetRules.MakesPerFrame)return;
                // Past the puppets kept: the longest unhit sleeping one goes.
                if(hitPuppets.Count>=NativeHitPuppetRules.Kept)
                {
                    VisualBody oldest=null;float when=float.PositiveInfinity;
                    foreach(var pair in hitPuppets)if(pair.Value.HitAt<when&&!HitPuppetAwakeNow(pair.Value.Handle)){when=pair.Value.HitAt;oldest=pair.Key;}
                    if(oldest==null)return;HitPuppetRelease(oldest);
                }
                hitPuppetMakes++;var made=HitPuppetMake(body.Binding.Bones.Length>0?body.Binding.Bones[0]:null);if(made==null)return;
                slot=new HitPuppet{Handle=made,Body=body,Owner=owner,At=anchor,PosedFrame=Time.frameCount};hitPuppets[body]=slot;
            }
            // The hitbox the bent pose keeps to: a pilot's capsule (NativeLocomotionRules), an add's own (each owner anew: a spare
            // zombie body goes to another add).
            if(slot.Shaped!=owner)
            {
                slot.Shaped=owner;float scale=Mathf.Max(.2f,body.Root.transform.lossyScale.y);
                if(!h.Zombie||!HitPuppetAddShape(h.Id,out float radius,out float height)){radius=NativeLocomotionRules.Radius*scale;height=2*(NativeLocomotionRules.HalfHeight+NativeLocomotionRules.Skin)*scale;}
                HitPuppetBounds(slot.Handle,radius,height);
            }
            // The hit point on the drawn body: the authority's offset from the body's centre (a pilot's) or floor (an add's), drawn
            // in to the body; a headshot lands on the head.
            var of=body.Binding.Rig.Of;var bones=body.Binding.Bones;int pv=of[CapturedRig.Pelvis],hd=of[CapturedRig.Head];
            Vector3 point=anchor+h.Offset;
            if(pv>=0){var off=point-bones[pv].position;float r=NativeHitPuppetRules.PointReach;if(off.sqrMagnitude>r*r)point=bones[pv].position+off.normalized*r;}
            // A headshot on the head's side the shot came from (its centre, out along the neck, less the way the shot travels), so it
            // turns the head away from the shot; struck at the head bone (its pivot on the neck) it only shoved it.
            int nk=of[CapturedRig.Neck];
            if(h.Head&&hd>=0){Vector3 up=nk>=0?bones[hd].position-bones[nk].position:Vector3.up;up=up.sqrMagnitude>1e-6f?up.normalized:Vector3.up;point=bones[hd].position+up*HitHeadCentre-h.Dir*HitHeadSide;}
            if(HitPuppetPush(slot.Handle,point,h.Dir*h.Impulse,h.Unpin,h.Spread,h.Loose))slot.HitAt=Time.unscaledTime;
            else if(HitPuppetsOn)HitPuppetRelease(body);
        }
        // The drawn hands' frames before the step, and after it the game rig's hands carried with them, so what the rig's hands
        // carry (the held weapon's model, its muzzle, a blade's trail, a chip) stays in the bent hands (VisualBodyRestore puts the rig
        // hands back before the next pose).
        private void HitPuppetHandsBefore(VisualBody body)
        {
            var of=body.Binding.Rig.Of;var bones=body.Binding.Bones;
            for(int side=0;side<2;side++){int v=of[side==0?CapturedRig.LHand:CapturedRig.RHand];if(v<0||v>=bones.Length||bones[v]==null)continue;hitPuppetHandAt[side]=bones[v].position;hitPuppetHandTurn[side]=bones[v].rotation;}
        }
        private bool HitPuppetHandMoved(VisualBody body,int side,out Transform drawn,out Quaternion turn)
        {
            int v=body.Binding.Rig.Of[side==0?CapturedRig.LHand:CapturedRig.RHand];var bones=body.Binding.Bones;drawn=v>=0&&v<bones.Length?bones[v]:null;turn=Quaternion.identity;
            if(drawn==null)return false;turn=drawn.rotation*Quaternion.Inverse(hitPuppetHandTurn[side]);
            return (drawn.position-hitPuppetHandAt[side]).sqrMagnitude>1e-10f||Quaternion.Angle(turn,Quaternion.identity)>.01f;
        }
        // VisualBodyPose: a living pilot's body just posed.
        private void HitPuppetPilot(VisualBody body,CapturedAnimation.Player rig,Actor a,float time)
        {
            if(hitPuppets.Count==0&&hitPuppetHitCount==0||rig==null)return;
            if(!HitPuppetPose(body,a.Id,false,a.Id,time,rig.Root.transform.position,false))return;
            for(int side=0;side<2;side++)
            {
                if(!HitPuppetHandMoved(body,side,out var drawn,out var turn))continue;
                var hand=rig.Bones[side==0?CapturedRig.LHand:CapturedRig.RHand];hand.SetPositionAndRotation(drawn.position+turn*(hand.position-hitPuppetHandAt[side]),turn*hand.rotation);
            }
        }
        // ZombieModelBind: an add's body just posed (its carried staff follows the bent right hand).
        private void HitPuppetZombie(ZombieBody z)
        {
            if(hitPuppets.Count==0&&hitPuppetHitCount==0||z?.Body?.Root==null)return;
            var t=z.Body.Root.transform;
            if(!HitPuppetPose(z.Body,-(long)z.Id,true,z.Id,hordeNow,t.position,z.Dying)||!z.CarryKnown||!HitPuppetHandMoved(z.Body,1,out var drawn,out var turn))return;
            var carry=t.localToWorldMatrix*z.Carry;Vector3 at=carry.GetColumn(3);
            z.Carry=t.worldToLocalMatrix*Matrix4x4.TRS(drawn.position+turn*(at-hitPuppetHandAt[1]),turn*carry.rotation,Vector3.one);
        }
        // FinishSourceFrame: a body gone takes its puppet with it; one not drawn this frame sleeps; old queued hits go.
        private void HitPuppetsSweep()
        {
            if(hitPuppets.Count==0&&hitPuppetHitCount==0)return;
            int frame=Time.frameCount;float now=Time.unscaledTime;hitPuppetsGone.Clear();
            foreach(var pair in hitPuppets)
            {
                var p=pair.Value;if(p.Body.Root==null||p.Handle==null){hitPuppetsGone.Add(pair.Key);continue;}
                if(p.PosedFrame!=frame&&HitPuppetAwakeNow(p.Handle))HitPuppetSleep(p.Handle);
            }
            foreach(var body in hitPuppetsGone)HitPuppetRelease(body);
            for(int i=0;i<hitPuppetHitCount;)if(now-hitPuppetHits[i].Queued>1)HitPuppetDequeue(i);else i++;
        }
        private void HitPuppetRelease(VisualBody body)
        {
            if(!hitPuppets.TryGetValue(body,out var p))return;hitPuppets.Remove(body);
            if(p.Handle==null||NativeHitPuppets.Release==null)return;
            try{NativeHitPuppets.Release(p.Handle);}catch(Exception e){HitPuppetFailed(e);}
        }
        // ClearVisualBodies / ClearZombieModels: every puppet goes with the bodies.
        private void HitPuppetsClear()
        {
            hitPuppetsGone.Clear();hitPuppetsGone.AddRange(hitPuppets.Keys);foreach(var body in hitPuppetsGone)HitPuppetRelease(body);
            hitPuppetsGone.Clear();hitPuppetHitCount=0;
        }
    }
}
