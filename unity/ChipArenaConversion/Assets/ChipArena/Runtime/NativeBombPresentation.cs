using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Bombadier in the arena (docs/BOMBADIER.md). The effects are Core/NativeBombFx, drawn from the kit every peer holds (NativeBombs.Source: the Dev Sandbox's preview until the rules
    // replicate it). The solid things are models: the TNT in the fists and what is thrown, the mines, the sticks driven in, the detonator with its plunger, and the belt the pilot wears. Presentation only: nothing here reaches
    // the authority, the network frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Dev Sandbox's Preview Bombadier: the whole kit on a loop. Off everywhere else.</summary>
        public bool BombPreviewOn;
        private readonly NativeBombPreview bombPreview=new NativeBombPreview();
        private void BombBegin(Snapshot s,float time)
        {
            Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
            if(!BombPreviewOn||me==null||!me.Alive){if(bombPreview.Any)bombPreview.Clear();if(ReferenceEquals(NativeBombs.Source,bombPreview))NativeBombs.Source=null;return;}
            NativeBombs.Source=bombPreview;var feet=visualPositions.TryGetValue(me.Id,out var v)?v:Vec(me.Position);bombPreview.Step(me,new NVector(feet.x,feet.y,feet.z),me.Yaw*Mathf.Deg2Rad,time);
        }
        private void ClearBomb(){bombPreview.Clear();if(ReferenceEquals(NativeBombs.Source,bombPreview))NativeBombs.Source=null;}
        private static readonly Color BombBlack=new Color(.03f,.03f,.035f),BombBrass=new Color(.55f,.38f,.14f),BombSteel=new Color(.16f,.16f,.18f),BombHazard=new Color(.8f,.6f,.05f),BombLeather=new Color(.3f,.18f,.08f),BombOlive=new Color(.085f,.09f,.065f),BombLed=new Color(5,.3f,.2f);
        // Where the next piece goes: the world, or (for what a fist holds this moment) the frame of that fist, on the first-person layer when it is the view rig's.
        private Matrix4x4 bombFrame=Matrix4x4.identity;private int bombLayer;private Camera bombCamera;
        private void BombPiece(WeaponShape shape,Vector3 at,Quaternion q,Vector3 scale,Color c,bool glow=false)=>sourceModels.QueuePiece(shape,glow,bombFrame*Matrix4x4.TRS(at,q,scale),c,bombLayer,bombCamera);
        /// <summary>End of Render: the kit's models and the pilot's gear.</summary>
        private void RenderBomb(Snapshot s,float time)
        {
            if(s?.Actors==null||sourceModels==null)return;
            foreach(var a in s.Actors)
            {
                if(a==null)continue;if(a.SourceSpec==NativeBombs.Spec&&a.Alive&&!(FirstPerson&&a.Id==LocalId))BombGear(a);
                var k=NativeBombs.Of(a);if(k==null)continue;var rig=FirstPerson&&a.Id==LocalId?viewRig:rigs.TryGetValue(a.Id,out var own)?own:null;
                if(k.Bombs!=null)foreach(var b in k.Bombs)if(b!=null&&time>=b.At)BombModel(b,time,time-b.At<BombLeaves?BombFrom(rig,k,b):null);
                if(k.Finale!=null&&time>=k.Finale.At&&time<k.Finale.Until+.3f)Detonator(k.Finale,time,rigs.TryGetValue(a.Id,out var body)?body:null);
            }
        }
        // ---- the TNT ----
        // One stick from NativeTnt's pieces, its axis q's y.
        private void BombTnt(NativeTntPiece[] pieces,Vector3 at,Quaternion q,float length)
        {
            foreach(var p in pieces)BombPiece(p.Shape,at+q*new Vector3(p.At.X*length,p.At.Y*length,p.At.Z*length),q,new Vector3(p.Size.X*length,p.Size.Y*length,p.Size.Z*length),new Color(p.Tint.X,p.Tint.Y,p.Tint.Z),p.Glow);
        }
        // The big one, a Cluster Lob: six sticks lashed round a seventh with hazard tape, its fuse out of the middle.
        private void BombBundle(Vector3 at,Quaternion q,float length)
        {
            float r=NativeTnt.Radius*length,ring=r*2.02f;BombTnt(NativeTnt.Stick,at,q,length);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;BombTnt(NativeTnt.Small,at+q*new Vector3(Mathf.Cos(a)*ring,0,Mathf.Sin(a)*ring),q,length*.92f);}
            float wrap=ring+r*1.1f;BombPiece(WeaponShape.Cylinder,at+q*new Vector3(0,.17f*length,0),q,new Vector3(wrap,.1f*length,wrap),BombHazard);BombPiece(WeaponShape.Cylinder,at-q*new Vector3(0,.2f*length,0),q,new Vector3(wrap,.07f*length,wrap),BombBlack);
        }
        // A mine: a steel plate with two sticks strapped across it, the stud round it standing up as it arms. Its light is an effect (NativeTechniqueFx.Charge).
        private void BombMine(Vector3 at,Vector3 n,float arm)
        {
            var q=Quaternion.LookRotation(Mathf.Abs(n.y)>.9f?Vector3.forward:Vector3.up,n);var across=q*Quaternion.Euler(0,0,90);
            BombPiece(WeaponShape.Cylinder,at+n*.015f,q,new Vector3(.19f,.03f,.19f),BombSteel);BombPiece(WeaponShape.Cylinder,at+n*.034f,q,new Vector3(.16f,.012f,.16f),BombBlack);
            for(int s=-1;s<=1;s+=2)BombTnt(NativeTnt.Small,at+n*.066f+q*new Vector3(0,0,s*.036f),across,.27f);
            BombPiece(WeaponShape.Cube,at+n*.068f,q,new Vector3(.045f,.072f,.14f),BombHazard);
            for(int i=0;i<4;i++){float y=i*90*Mathf.Deg2Rad;BombPiece(WeaponShape.Cube,at+q*new Vector3(Mathf.Cos(y)*.165f,.03f+.012f*arm,Mathf.Sin(y)*.165f),q*Quaternion.Euler(0,-i*90,0),new Vector3(.05f,.03f+.03f*arm,.03f),BombBrass);}
        }
        // How long what is thrown takes to leave the real fist for its place in the record (the record does not know where the hands are).
        private const float BombLeaves=.1f;
        private static Vector3? BombFrom(CapturedAnimation.Player rig,NativeBombKit k,NativeBomb b)
        {
            if(rig?.Bones==null)return null;Vector3 l=rig.Bones[CapturedRig.LHand].position,r=rig.Bones[CapturedRig.RHand].position;
            switch(b.Kind){case NativeBombs.Pop:return k.CastKind==NativeBombs.PopBomb&&NativeBombs.ThrowsLeft(k.CastAt)?l:r;case NativeBombs.Sticky:return r;case NativeBombs.Mine:return l;case NativeBombs.Lob:return (l+r)*.5f;default:return null;}
        }
        // What is out: a stick tumbling end over end, or lying where it stopped; the big bundle; a mine; a short stick driven into whatever it hit.
        private void BombModel(NativeBomb b,float time,Vector3? from)
        {
            var at=new Vector3(b.Pos.x,b.Pos.y,b.Pos.z);
            if(b.Stuck>0&&kitScene!=null&&s_BombStuck(b,out var stuck))at=stuck;var n=new Vector3(b.Normal.x,b.Normal.y,b.Normal.z);if(n.sqrMagnitude<.5f)n=Vector3.up;n.Normalize();
            if(from.HasValue){float u=Mathf.Clamp01((time-b.At)/BombLeaves);at=Vector3.Lerp(from.Value,at,u*u*(3-2*u));}
            float length=NativeTnt.Length(b.Kind);
            switch(b.Kind)
            {
                case NativeBombs.Mine:BombMine(at,n,Mathf.Clamp01((time-b.ArmAt)/.1f));break;
                case NativeBombs.Sticky:
                {
                    var q=Quaternion.FromToRotation(Vector3.up,n);BombTnt(NativeTnt.Stick,at+n*(length*.42f),q,length);
                    for(int i=0;i<3;i++)BombPiece(WeaponShape.Cube,at+n*.02f,q*Quaternion.Euler(0,i*60,0),new Vector3(.15f,.012f,.012f),BombSteel);
                    break;
                }
                default:
                {
                    var t=NativeBombs.ChargeTurn(b,time);var q=new Quaternion(t.X,t.Y,t.Z,t.W);if(b.Vel.Length<.5f&&b.Kind!=NativeBombs.Lob)at+=Vector3.up*(NativeTnt.Radius*length);
                    if(b.Kind==NativeBombs.Lob)BombBundle(at,q,length);else BombTnt(b.Kind==NativeBombs.Bomblet?NativeTnt.Small:NativeTnt.Stick,at,q,length);
                    break;
                }
            }
        }
        private bool s_BombStuck(NativeBomb b,out Vector3 at){at=default;var a=NativeTechniqueFx.ChargeAt(kitScene,b);at=new Vector3(a.X,a.Y,a.Z);return true;}
        // The fists (SourceCharacter): the bundle in each, by how much of it shows this moment (NativeBombs.HeldShare); what takes its place for a cast: a mine, the switch, or the big bundle held between both.
        private void BombHeld(CapturedAnimation.Player rig,Actor a,float time,string model,System.Func<int,Matrix4x4> bone,int layer,Camera camera,System.Func<int,Color> tint,float flash)
        {
            var k=NativeBombs.Of(a);float right=NativeBombs.HeldShare(k,time,a.ReloadAt,a.ReloadUntil,false),left=NativeBombs.HeldShare(k,time,a.ReloadAt,a.ReloadUntil,true);
            Matrix4x4 Shown(int b){var m=bone(b);float s=b==CapturedRig.RHand?right:b==CapturedRig.LHand?left:1;return s<1?m*Matrix4x4.Scale(Vector3.one*Mathf.Max(.001f,s)):m;}
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,Shown,b=>b==CapturedRig.RHand?right>.01f:b!=CapturedRig.LHand||left>.01f,layer,camera,accent:AccentTint(a.AccentColor),hitFlash:flash,partTint:tint,glow:HeldGlow);
            if(k?.CastKind==null||time<k.CastAt||time>=k.CastUntil)return;
            bombLayer=layer;bombCamera=camera;
            switch(k.CastKind)
            {
                case NativeBombs.BounceMine:if(time<k.CastRelease){bombFrame=bone(CapturedRig.LHand);BombMine(new Vector3(0,0,.03f),Vector3.forward,0);}break;
                case NativeBombs.DeadMansSwitch:bombFrame=bone(CapturedRig.LHand);BombSwitch(Mathf.Clamp01((time-k.CastRelease)/.05f));break;
                case NativeBombs.ClusterLob:
                    if(time<k.CastRelease)
                    {
                        Vector3 l=rig.Bones[CapturedRig.LHand].position,r=rig.Bones[CapturedRig.RHand].position,span=r-l;
                        bombFrame=Matrix4x4.TRS((l+r)*.5f,span.sqrMagnitude>1e-4f?Quaternion.FromToRotation(Vector3.up,span.normalized):Quaternion.identity,Vector3.one*rig.Root.transform.lossyScale.x);BombBundle(Vector3.zero,Quaternion.identity,NativeTnt.Length(NativeBombs.Lob));
                    }
                    break;
            }
            bombFrame=Matrix4x4.identity;bombLayer=0;bombCamera=null;
        }
        // The hand switch for Dead Man's Switch, in the fist's frame (its +z the thumb's end): the same one that sets off the Last Warning.
        private void BombSwitch(float press)
        {
            var i=Quaternion.identity;var up=Quaternion.Euler(90,0,0);
            BombPiece(WeaponShape.Cube,new Vector3(0,0,.035f),i,new Vector3(.058f,.036f,.125f),BombOlive);BombPiece(WeaponShape.Cube,new Vector3(0,.0185f,.035f),i,new Vector3(.05f,.002f,.042f),BombHazard);
            BombPiece(WeaponShape.Cylinder,new Vector3(0,0,.1f),up,new Vector3(.018f,.008f,.018f),BombBrass);BombPiece(WeaponShape.Cylinder,new Vector3(0,0,.108f-.006f*press),up,new Vector3(.012f,.01f,.012f),BombLed,true);
            BombPiece(WeaponShape.Cube,new Vector3(.02f,0,.18f),i,new Vector3(.004f,.004f,.16f),BombSteel);BombPiece(WeaponShape.Sphere,new Vector3(.02f,0,.26f),i,Vector3.one*.006f,BombLed,true);
        }
        // The crate and its T-handle: dropped in on the cut-in, the handle up, then home at the Lock. Both hands drive it: while the pilot stoops over the crate the handle rides in them, and stays down from the Lock.
        private const float CrateHigh=.46f,CrateHome=.3f;
        private void Detonator(NativeBombFinale f,float time,CapturedAnimation.Player rig)
        {
            var box=new Vector3(f.Box.x,f.Box.y,f.Box.z);var d=new Vector3(f.Dir.x,0,f.Dir.z);d=d.sqrMagnitude>1e-5f?d.normalized:Vector3.forward;var q=Quaternion.LookRotation(d,Vector3.up);float slam=Mathf.Clamp01((time-f.At)/.12f),down=Mathf.Clamp01((time-f.Lock)/.05f),stroke=Mathf.Lerp(.5f,CrateHome,down);box+=Vector3.up*((1-slam)*.5f);
            if(rig?.Bones!=null&&time>=f.At+NativeBombs.PlungeAt&&time<f.Lock){var hands=(rig.Bones[CapturedRig.LHand].position+rig.Bones[CapturedRig.RHand].position)*.5f;stroke=Mathf.Clamp(hands.y-box.y-CrateHigh-.02f,CrateHome,.62f);}
            Vector3 Up(float y)=>box+Vector3.up*y;float h=CrateHigh;
            // The crate: wood-red with steel corners, a stencilled hazard panel, a row of sticks strapped to its front, and two cables running off across the floor.
            BombPiece(WeaponShape.Cube,Up(h*.5f),q,new Vector3(.46f,h,.36f),new Color(.32f,.13f,.09f));BombPiece(WeaponShape.Cube,Up(h+.01f),q,new Vector3(.48f,.02f,.38f),BombBlack);BombPiece(WeaponShape.Cube,Up(h*.72f)+d*.183f,q,new Vector3(.38f,.1f,.006f),BombHazard);
            for(int i=-2;i<=2;i++)BombPiece(WeaponShape.Cube,Up(h*.72f)+d*.187f+q*new Vector3(i*.076f,0,0),q*Quaternion.Euler(0,0,35),new Vector3(.022f,.13f,.002f),BombBlack);
            for(int i=-1;i<=1;i++)BombTnt(NativeTnt.Small,Up(h*.3f)+d*.205f+q*new Vector3(i*.07f,0,0),Quaternion.Euler(0,0,90)*q,.26f);BombPiece(WeaponShape.Cube,Up(h*.3f)+d*.2f,q,new Vector3(.3f,.03f,.05f),BombBlack);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){BombPiece(WeaponShape.Cube,Up(h*.5f)+q*new Vector3(sx*.228f,0,sz*.178f),q,new Vector3(.03f,h+.01f,.03f),BombSteel);}
            for(int sx=-1;sx<=1;sx+=2){var side=q*Vector3.right*sx;var cable=Up(.03f)+side*.24f;var run=(side*.9f-d*.5f);BombPiece(WeaponShape.Cube,cable+run*.5f,Quaternion.LookRotation(run.normalized,Vector3.up),new Vector3(.018f,.018f,run.magnitude),BombBlack);}
            // The plunger: a steel shaft with its guide collar, the grips rubber-ended on a brass bar.
            BombPiece(WeaponShape.Cube,Up(h+.02f+stroke*.5f),q,new Vector3(.03f,stroke,.03f),BombSteel);BombPiece(WeaponShape.Cube,Up(h+.045f),q,new Vector3(.07f,.03f,.07f),BombBrass);BombPiece(WeaponShape.Cube,Up(h+.02f+stroke),q,new Vector3(.34f,.035f,.04f),BombBrass);
            for(int sx=-1;sx<=1;sx+=2)BombPiece(WeaponShape.Cube,Up(h+.02f+stroke)+q*new Vector3(sx*.15f,0,0),q,new Vector3(.08f,.05f,.05f),BombBlack);
            for(int s=-1;s<=1;s+=2){BombPiece(WeaponShape.Cube,Up(h+.03f)+q*new Vector3(s*.14f,0,-.1f),q,new Vector3(.03f,.03f,.03f),BombBrass);BombPiece(WeaponShape.Sphere,Up(h-.03f)+q*new Vector3(s*.14f,.02f,.17f),q,Vector3.one*.016f,(time<f.Strike&&Mathf.Sin(time*26+s)>0)?BombLed:new Color(.1f,.01f,.01f),true);}
        }
        // The belt: a strap across the chest with a row of sticks in its loops, a satchel on the hip with more showing out of the top. On the rig's chest and pelvis, so it follows every pose.
        private void BombGear(Actor a){if(rigs.TryGetValue(a.Id,out var rig))BombGear(rig);}
        private void BombGear(CapturedAnimation.Player rig)
        {
            if(rig?.Bones==null||!visualBodies.TryGetValue(rig,out var body))return;
            var core=VisualCoreFrame(body,rig);var hips=VisualLimbFrame(body,rig,CapturedRig.Pelvis);
            var cq=Quaternion.LookRotation(core.GetColumn(2),core.GetColumn(1));Vector3 chest=core.GetColumn(3);Vector3 C(float x,float y,float z)=>chest+cq*new Vector3(x,y,z);var strapQ=cq*Quaternion.Euler(0,0,-38);
            BombPiece(WeaponShape.Cube,C(0,-.01f,.02f),strapQ,new Vector3(.05f,.46f,.014f),BombLeather);
            for(int i=0;i<5;i++){float w=(i-2)*.082f;var at=C(-w*.62f,-.01f+w*.8f,.05f);BombTnt(NativeTnt.Small,at,cq,.13f);BombPiece(WeaponShape.Cube,at-cq*new Vector3(0,.018f,.012f),strapQ,new Vector3(.062f,.012f,.01f),BombBlack);}
            var pq=Quaternion.LookRotation(hips.GetColumn(2),hips.GetColumn(1));Vector3 pelvis=hips.GetColumn(3);Vector3 P(float x,float y,float z)=>pelvis+pq*new Vector3(x,y,z);BombPiece(WeaponShape.Cube,P(.25f,-.1f,.03f),pq,new Vector3(.1f,.17f,.17f),BombLeather);BombPiece(WeaponShape.Cube,P(.25f,-.01f,.03f),pq,new Vector3(.11f,.03f,.18f),BombBlack);BombPiece(WeaponShape.Cube,P(.302f,-.1f,.03f),pq,new Vector3(.004f,.05f,.1f),BombHazard);
            for(int i=-1;i<=1;i++)BombTnt(NativeTnt.Small,P(.25f+.012f*i,.05f,.03f+.046f*i),pq,.15f);
        }
        // First person: the arms are the kit's own (NativeBombView), not the game rig's gun hold.
        private Actor bombViewActor;private float bombViewTime;private System.Func<CapturedRig.Pose,bool> bombViewLayer;
        private void BombViewArms(CapturedAnimation.Player view,Actor a,float time)
        {
            bombViewActor=a;bombViewTime=time;bombViewLayer??=p=>{NativeBombView.Apply(p,NativeBombs.Of(bombViewActor),bombViewTime,bombViewActor.ReloadAt,bombViewActor.ReloadUntil,bombViewActor.BlockUntil);return true;};view.Overlay(bombViewLayer);
        }
        // The lit fuse on what each fist holds.
        private void BombHeldFuses(float q)
        {
            var actors=kitScene.Actors;if(actors==null)return;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null||a.SourceSpec!=NativeBombs.Spec||!a.Alive||a.WeaponAway)continue;bool view=FirstPerson&&a.Id==LocalId;if(view&&ScopeActive)continue;var rig=view?viewRig:rigs.TryGetValue(a.Id,out var own)?own:null;if(rig?.Bones==null)continue;var k=NativeBombs.Of(a);
                for(int hand=0;hand<2;hand++)
                {
                    bool left=hand==1;if(NativeBombs.HeldShare(k,kitClock,a.ReloadAt,a.ReloadUntil,left)<.6f)continue;var tip=NativeWeaponModels.TntFuseTip;if(left)tip.X=-tip.X;
                    var at=rig.Bones[left?CapturedRig.LHand:CapturedRig.RHand].localToWorldMatrix.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(tip)));techniqueFx.HeldFuse(techSink,view,new NVector(at.x,at.y,at.z),kitClock,a.Id*97L+hand*31,q,LowGraphics);
                }
            }
        }
        /// <summary>RenderGlow, after the techniques': the kit's particles.</summary>
        private void BombFx(float now)
        {
            if(kitScene==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            float q=Mathf.Clamp01(WeaponEffectsQuality);BombHeldFuses(q);if(NativeBombs.Source!=null)techniqueFx.DrawBombs(techSink,kitScene,kitClock,q,LowGraphics);
        }
        /// <summary>The Grand Finale on the local pilot's own camera (NativeCinematicCamera.Direct). False when it is not this pilot's cutscene.</summary>
        internal bool BombFinaleDirect(NativeArena arena,Camera view,float time,bool shake,bool reduced)
        {
            if(view==null||arena?.Local==null||NativeBombs.Source==null)return false;var k=NativeBombs.Source.Of(arena.LocalId);var f=k?.Finale;if(!NativeBombCamera.Active(f,time))return false;
            var feet=visualPositions.TryGetValue(arena.LocalId,out var v)?v:view.transform.position;var shot=NativeBombCamera.At(f,new NVector(feet.x,feet.y,feet.z),time,reduced,shake);if(shot.Blend<=0)return false;
            Vector3 position=new Vector3(shot.Position.X,shot.Position.Y,shot.Position.Z),look=new Vector3(shot.Look.X,shot.Look.Y,shot.Look.Z),ray=position-look;float distance=ray.magnitude;
            if(distance>.05f&&arena.Root!=null){ray/=distance;if(arena.Root.scene.GetPhysicsScene().Raycast(look+ray*.12f,ray,out var hit,Mathf.Max(0,distance-.12f),NativeArena.WorldMask,QueryTriggerInteraction.Ignore))position=look+ray*Mathf.Max(.3f,hit.distance);}
            NativeSirenCutscene.Shake(time,shot.Shake,out float dx,out float dy,out float roll);var forward=look-position;if(forward.sqrMagnitude<1e-8f)forward=view.transform.forward;var rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(-roll*Mathf.Rad2Deg,Vector3.forward);
            float b=shot.Blend;view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position,position+new Vector3(-dx,dy,0),b),Quaternion.Slerp(view.transform.rotation,rotation,b));view.fieldOfView=Mathf.Lerp(view.fieldOfView,shot.Fov,b);FirstPerson=false;AimHeld=false;return true;
        }
    }
}
