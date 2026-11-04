using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Bombadier in the arena (docs/BOMBADIER.md). The effects are Core/NativeBombFx, drawn from the kit every peer holds (NativeBombs.Source: the Dev Sandbox's preview until the rules
    // replicate it). The solid things are models: the bombs, the mines, the fuses stuck on, the detonator with its plunger, and the bandolier the pilot wears. Presentation only: nothing here reaches
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
        private static readonly Color BombBlack=new Color(.03f,.03f,.035f),BombBrass=new Color(.55f,.38f,.14f),BombRope=new Color(.09f,.075f,.06f),BombSteel=new Color(.16f,.16f,.18f),BombStrap=new Color(.11f,.075f,.05f),BombHazard=new Color(.8f,.6f,.05f),BombCase=new Color(.45f,.13f,.09f),BombLeather=new Color(.3f,.18f,.08f),BombShell=new Color(.09f,.09f,.1f);
        private void BombPiece(WeaponShape shape,Vector3 at,Quaternion q,Vector3 scale,Color c,bool glow=false)=>sourceModels.QueuePiece(shape,glow,Matrix4x4.TRS(at,q,scale),c);
        /// <summary>End of Render: the kit's models and the pilot's gear.</summary>
        private void RenderBomb(Snapshot s,float time)
        {
            if(s?.Actors==null||sourceModels==null)return;
            foreach(var a in s.Actors)
            {
                if(a==null)continue;if(a.SourceSpec==NativeBombs.Spec&&a.Alive&&!(FirstPerson&&a.Id==LocalId))BombGear(a);
                var k=NativeBombs.Of(a);if(k==null)continue;
                if(k.Bombs!=null)foreach(var b in k.Bombs)if(b!=null&&time>=b.At)BombModel(b,time);
                if(k.Finale!=null&&time>=k.Finale.At&&time<k.Finale.Until+.3f)Detonator(k.Finale,time);
            }
        }
        // A round black bomb with a brass collar and a stub of fuse; a flat mine with its dome; a stuck fuse with its barbs.
        private void BombModel(NativeBomb b,float time)
        {
            var at=new Vector3(b.Pos.x,b.Pos.y,b.Pos.z);
            if(b.Stuck>0&&kitScene!=null&&s_BombStuck(b,out var stuck))at=stuck;var n=new Vector3(b.Normal.x,b.Normal.y,b.Normal.z);if(n.sqrMagnitude<.5f)n=Vector3.up;n.Normalize();
            switch(b.Kind)
            {
                case NativeBombs.Mine:
                {
                    float arm=Mathf.Clamp01((time-b.ArmAt)/.1f);var q=Quaternion.LookRotation(Mathf.Abs(n.y)>.9f?Vector3.forward:Vector3.up,n);BombPiece(WeaponShape.Cube,at+n*.02f,q,new Vector3(.3f,.045f,.3f),BombSteel);BombPiece(WeaponShape.Cube,at+n*.032f,q*Quaternion.Euler(0,45,0),new Vector3(.3f,.04f,.3f),BombBlack);BombPiece(WeaponShape.Cube,at+n*.055f,q,new Vector3(.2f,.02f,.04f),BombHazard);BombPiece(WeaponShape.Cube,at+n*.055f,q,new Vector3(.04f,.02f,.2f),BombHazard);
                    for(int i=0;i<4;i++){float y=i*90*Mathf.Deg2Rad;BombPiece(WeaponShape.Cube,at+q*new Vector3(Mathf.Cos(y)*.17f,.03f+.01f*arm,Mathf.Sin(y)*.17f),q*Quaternion.Euler(0,-i*90,0),new Vector3(.05f,.03f+.03f*arm,.03f),BombBrass);}
                    break;
                }
                case NativeBombs.Sticky:
                {
                    var q=Quaternion.LookRotation(n,Mathf.Abs(n.y)>.9f?Vector3.forward:Vector3.up);BombPiece(WeaponShape.Cube,at-n*.03f,q,new Vector3(.06f,.06f,.14f),BombCase);BombPiece(WeaponShape.Cube,at+n*.05f,q,new Vector3(.03f,.03f,.06f),BombBrass);for(int i=0;i<3;i++){BombPiece(WeaponShape.Cube,at-n*.07f,q*Quaternion.Euler(0,0,i*60),new Vector3(.13f,.012f,.012f),BombSteel);}
                    break;
                }
                default:
                {
                    float rad=b.Kind==NativeBombs.Lob?.17f:b.Kind==NativeBombs.Pop?.09f:.06f;float spin=(time-b.At)*9;var q=Quaternion.AngleAxis(spin*Mathf.Rad2Deg,new Vector3(.4f,.2f,.9f).normalized);var up=q*Vector3.up;
                    BombPiece(WeaponShape.Sphere,at,q,Vector3.one*rad,BombBlack);BombPiece(WeaponShape.Cube,at+up*(rad*.96f),q,new Vector3(rad*.62f,rad*.2f,rad*.62f),BombBrass);BombPiece(WeaponShape.Cube,at+up*(rad*1.3f),q,new Vector3(rad*.14f,rad*.5f,rad*.14f),BombRope);
                    if(b.Kind==NativeBombs.Lob)for(int i=0;i<3;i++){BombPiece(WeaponShape.Cube,at+q*new Vector3(0,(i-1)*rad*.5f,0),q,new Vector3(rad*2.05f,rad*.12f,rad*2.05f),i==1?BombHazard:BombBlack);}
                    break;
                }
            }
        }
        private bool s_BombStuck(NativeBomb b,out Vector3 at){at=default;var a=NativeTechniqueFx.ChargeAt(kitScene,b);at=new Vector3(a.X,a.Y,a.Z);return true;}
        // The box and its T-handle: dropped in on the cut-in, the handle up, then home at the Lock.
        private void Detonator(NativeBombFinale f,float time)
        {
            var box=new Vector3(f.Box.x,f.Box.y,f.Box.z);var d=new Vector3(f.Dir.x,0,f.Dir.z);d=d.sqrMagnitude>1e-5f?d.normalized:Vector3.forward;var q=Quaternion.LookRotation(d,Vector3.up);float slam=Mathf.Clamp01((time-f.At)/.12f),down=Mathf.Clamp01((time-f.Lock)/.05f),stroke=Mathf.Lerp(.42f,.16f,down);box+=Vector3.up*((1-slam)*.5f);
            BombPiece(WeaponShape.Cube,box+Vector3.up*.11f,q,new Vector3(.36f,.22f,.28f),new Color(.32f,.13f,.09f));BombPiece(WeaponShape.Cube,box+Vector3.up*.225f,q,new Vector3(.38f,.02f,.3f),BombBlack);BombPiece(WeaponShape.Cube,box+Vector3.up*.12f+d*.143f,q,new Vector3(.3f,.05f,.006f),BombHazard);
            BombPiece(WeaponShape.Cube,box+Vector3.up*(.225f+stroke*.5f),q,new Vector3(.03f,stroke,.03f),BombSteel);BombPiece(WeaponShape.Cube,box+Vector3.up*(.225f+stroke),q,new Vector3(.34f,.035f,.04f),BombBrass);
            for(int s=-1;s<=1;s+=2){BombPiece(WeaponShape.Cube,box+Vector3.up*.235f+q*new Vector3(s*.12f,0,-.08f),q,new Vector3(.03f,.03f,.03f),BombBrass);BombPiece(WeaponShape.Sphere,box+Vector3.up*.2f+q*new Vector3(s*.12f,.02f,.1f),q,Vector3.one*.016f,(time<f.Strike&&Mathf.Sin(time*26+s)>0)?new Color(5,.3f,.2f):new Color(.1f,.01f,.01f),true);}
        }
        // The bandolier: a strap across the chest with little bombs on it, a satchel on the hip. On the rig's chest and pelvis, so it follows every pose.
        private void BombGear(Actor a){if(rigs.TryGetValue(a.Id,out var rig))BombGear(rig);}
        private void BombGear(CapturedAnimation.Player rig)
        {
            if(rig?.Bones==null||!visualBodies.TryGetValue(rig,out var body))return;
            var core=VisualCoreFrame(body,rig);var hips=VisualLimbFrame(body,rig,CapturedRig.Pelvis);
            var cq=Quaternion.LookRotation(core.GetColumn(2),core.GetColumn(1));Vector3 chest=core.GetColumn(3);Vector3 C(float x,float y,float z)=>chest+cq*new Vector3(x,y,z);var strapQ=cq*Quaternion.Euler(0,0,-38);
            BombPiece(WeaponShape.Cube,C(0,-.01f,.02f),strapQ,new Vector3(.05f,.46f,.014f),BombLeather);
            for(int i=0;i<5;i++){float w=(i-2)*.082f;var at=C(-w*.62f,-.01f+w*.8f,.048f);BombPiece(WeaponShape.Sphere,at,cq,Vector3.one*.03f,BombShell);BombPiece(WeaponShape.Cube,at+cq*new Vector3(0,.03f,0),cq,new Vector3(.022f,.01f,.022f),BombBrass);BombPiece(WeaponShape.Cube,at+cq*new Vector3(0,.045f,0),cq,new Vector3(.005f,.02f,.005f),BombHazard);BombPiece(WeaponShape.Cube,at-cq*new Vector3(0,.0f,.03f),strapQ,new Vector3(.07f,.012f,.01f),BombBlack);}
            var pq=Quaternion.LookRotation(hips.GetColumn(2),hips.GetColumn(1));Vector3 pelvis=hips.GetColumn(3);Vector3 P(float x,float y,float z)=>pelvis+pq*new Vector3(x,y,z);BombPiece(WeaponShape.Cube,P(.25f,-.1f,.03f),pq,new Vector3(.1f,.17f,.17f),BombLeather);BombPiece(WeaponShape.Cube,P(.25f,-.01f,.03f),pq,new Vector3(.11f,.03f,.18f),BombBlack);BombPiece(WeaponShape.Cube,P(.302f,-.1f,.03f),pq,new Vector3(.004f,.05f,.1f),BombHazard);
            BombPiece(WeaponShape.Cube,P(-.25f,-.06f,.0f),pq,new Vector3(.05f,.2f,.05f),BombCase);BombPiece(WeaponShape.Cube,P(-.25f,-.06f,.0f),pq,new Vector3(.056f,.03f,.056f),BombBlack);BombPiece(WeaponShape.Cube,P(-.25f,.05f,.0f),pq,new Vector3(.03f,.03f,.03f),BombBrass);
        }
        /// <summary>RenderGlow, after the techniques': the kit's particles.</summary>
        private void BombFx(float now)
        {
            if(NativeBombs.Source==null||kitScene==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            techniqueFx.DrawBombs(techSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
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
