using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The block effect (owner request 2026-10-11 "add a new block effect"; docs/MELEE-ABILITIES.md "The new block effect"),
    // drawn from what host, guests, spectators and the killcam replay all hold: each pilot's replicated block window (BlockUntil,
    // and its length NativeMeleeRules.BlockDuration from the replicated stats) and the authoritative Block events (Source the
    // blocker, Target the attacker, Position the contact). The rules, shapes and timing curves are Core/NativeBlockFx.cs; this
    // file only reads the posed rigs (the blade the renderer draws, SourceWeaponBone with the reach stretch), builds the mesh and
    // submits it (Resources/NativeBlockFx.shader):
    //  - Guard: while the window is open, every pilot's ward in the weapon's colour in front of the weapon (third person, on the
    //    match camera); the local first-person pilot's as a subtle arch over the view weapon on the view camera (layer 10).
    //  - Clash / deflect: NativeBlockFx.Classify per event (the Block Detail guard/clash/deflect overrides it once the simulation
    //    sends one); the clash flashes and cracks the guard, sprays sparks along the blade and rings toward the attacker, the
    //    deflect streaks along the reflected projectile's velocity (the nearest reflected shot of the blocker, else back toward
    //    the attacker) with a ping. The blocker's camera hitch is BlockHitch (ConversionGame, camera shake setting).
    // Reduced effects (WeaponEffectsQuality .3) thins the sprays and dims the hits; the low graphics tier (LowGraphics) halves the
    // guard's segments and sparks and drops its sparkle and cell flicker. Presentation only: nothing here reaches the authority,
    // the network frame, hit timing, hitboxes, damage, cooldowns, movement or Time.timeScale.
    public sealed partial class ArenaPresentation
    {
        private struct BlockRecord
        {
            public int Id,Blocker,Seed;public float At;public NativeBlockHit Hit;public Vector3 Point,Toward,Reflect;
            // The world spray frame, fixed when first drawn (a strafing blocker never drags its sparks along).
            public bool Framed;public NativeClashFrame Frame;
        }
        private readonly List<BlockRecord> blockRecords=new List<BlockRecord>();
        private readonly NativeBlockMesh blockWorld=new NativeBlockMesh(),blockView=new NativeBlockMesh();
        private NativeBlockFxDraw blockDraw;private bool blockDrawFailed;
        private int blockCursor;private float blockClock=float.NegativeInfinity;
        private static NVector N(Vector3 v)=>CapturedAnimation.Num(v);
        private static Vector3 U(NVector v)=>CapturedAnimation.Vec(v);
        private static Actor FindActor(Actor[] actors,int id){if(actors==null||id<=0)return null;for(int i=0;i<actors.Length;i++)if(actors[i]!=null&&actors[i].Id==id)return actors[i];return null;}
        private static Vector3 Flat(Vector3 v,Vector3 fallback){v.y=0;return v.sqrMagnitude>1e-6f?v.normalized:fallback;}
        private static Vector3 Facing(Actor a)=>Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;
        private static bool MeleeClass(Actor a)=>string.IsNullOrEmpty(a.SourceSpec)?a.Weapon==WeaponId.Sword&&a.MagicVisual==0:NativeWeaponDressing.Melee(a.SourceSpec);
        private static NativeGuardForm GuardForm(Actor a)=>NativeBlockFx.FormOf(a.SourceSpec,a.VisualSet,GunRules.Ranged(a.Weapon)||a.MagicVisual>0);
        private int GuardHex(Actor a){var w=LoadoutPilot(a.Id)?.Weapon;return w!=null?(int)w.color:NativeBlockFx.DefaultColor(a.SourceSpec,a.VisualSet);}
        // The window length the replicated stats give (the prototype lab's fixed one without a loadout).
        private float BlockWindow(Actor a){var stats=LoadoutPilot(a.Id)?.Stats;return stats!=null?NativeMeleeRules.BlockDuration(stats):Rules.BlockDuration;}
        private float AttackerReach(Actor a)
        {
            var p=LoadoutPilot(a.Id);if(p?.Weapon==null)return 2.5f;
            float reach=1;if(p.Stats!=null)reach=(float)p.Stats["reach"];return NativeBlockFx.Reach((float)p.Weapon.range,reach);
        }
        /// <summary>
        /// Reads the fresh Block events into clash and deflect records (idempotent per frame: BlockHitch and Render both call it).
        /// Event ids restart with a new arena or the killcam's replica, and its clock rewinds: both start over.
        /// </summary>
        private void ObserveBlocks(Snapshot s,float time)
        {
            var events=s?.Events;if(events==null)return;int newest=0;
            for(int i=0;i<events.Length;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<blockCursor||time<blockClock-.25f){blockRecords.Clear();blockCursor=0;}
            blockClock=time;
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(e.Id<=blockCursor||e.Kind!=EventKind.Block||e.Source<=0||NativeBlockFx.Notice(e.Detail)||time-e.Time>NativeBlockFx.EventWindow)continue;
                Actor blocker=FindActor(s.Actors,e.Source),attacker=FindActor(s.Actors,e.Target);Vector3 point=ArenaPresentation.Vec(e.Position);
                Vector3 facing=blocker!=null?Facing(blocker):Vector3.forward,toward=attacker!=null?Flat(ArenaPresentation.Vec(attacker.Position)-(blocker!=null?ArenaPresentation.Vec(blocker.Position):point),facing):facing;
                float distance=attacker!=null&&blocker!=null?(ArenaPresentation.Vec(attacker.Position)-ArenaPresentation.Vec(blocker.Position)).magnitude:0;
                var hit=NativeBlockFx.Classify(e.Detail,attacker!=null,attacker!=null&&MeleeClass(attacker),distance,attacker!=null?AttackerReach(attacker):0);
                var record=new BlockRecord{Id=e.Id,Blocker=e.Source,Seed=NativeBlockFx.Seed(e.Id),At=e.Time,Hit=hit,Point=point,Toward=toward,Reflect=toward};
                if(hit==NativeBlockHit.Deflect)record.Reflect=ReflectDirection(s,e.Source,point,attacker,toward);
                if(blockRecords.Count>=NativeBlockFx.MaxRecords)blockRecords.RemoveAt(0);blockRecords.Add(record);
            }
            if(newest>blockCursor)blockCursor=newest;
            for(int i=blockRecords.Count-1;i>=0;i--)if(time-blockRecords[i].At>NativeBlockFx.RecordSeconds)blockRecords.RemoveAt(i);
        }
        // The reflected projectile's new heading: the blocker's nearest reflected shot (host and guests both hold it), else back
        // toward the attacker's chest, else the blocker's way.
        private static Vector3 ReflectDirection(Snapshot s,int blocker,Vector3 point,Actor attacker,Vector3 toward)
        {
            float best=9;Vector3 dir=Vector3.zero;
            if(s.Shots!=null)foreach(var shot in s.Shots)
            {
                if(shot==null||shot.Owner!=blocker||shot.Reflections<=0)continue;Vector3 v=ArenaPresentation.Vec(shot.Velocity);if(v.sqrMagnitude<1e-4f)continue;
                float d=(ArenaPresentation.Vec(shot.Position)-point).sqrMagnitude;if(d<best){best=d;dir=v.normalized;}
            }
            if(dir!=Vector3.zero)return dir;
            if(attacker!=null){Vector3 to=ArenaPresentation.Vec(attacker.Position)+Vector3.up*1.3f-point;if(to.sqrMagnitude>1e-4f)return to.normalized;}
            return toward;
        }
        // One blade of a posed rig in world space (the frame the renderer queues the weapon model in): the guard to the
        // 'blade-tip' socket; for the shield its face up to its top edge. False for guns, staffs, fists and a weapon put away.
        private bool BladeLine(CapturedAnimation.Player rig,Actor a,float time,bool first,bool left,out Vector3 root,out Vector3 tip)
        {
            root=tip=default;if(rig==null||a.WeaponAway)return false;string spec=a.SourceSpec;
            if(sourceModels!=null&&!string.IsNullOrEmpty(spec))
            {
                if(NativeWeaponDressing.Fist(spec)||!NativeWeaponDressing.BladeTip(spec,out var socket))return false;
                var m=SourceWeaponBone(rig,a,time,first,left?CapturedRig.LHand:CapturedRig.RHand,WeaponReach(rig,a,time));
                root=m.MultiplyPoint3x4(U(CapturedRig.Mirror(spec=="shield"?new NVector(0,.05f,.25f):new NVector(0,0,.22f))));tip=m.MultiplyPoint3x4(U(CapturedRig.Mirror(socket)));return true;
            }
            if(a.Weapon!=WeaponId.Sword||a.MagicVisual>0)return false;
            var hand=MeleeHand(rig,a,time,left);int set=rig.Set;
            if(set==3){root=hand.MultiplyPoint3x4(new Vector3(0,-.05f,.2f));tip=hand.MultiplyPoint3x4(new Vector3(0,.42f,.2f));return true;}
            float length=set==1?.5f:set==2?1.35f:.94f;root=hand.MultiplyPoint3x4(new Vector3(0,0,.26f));tip=hand.MultiplyPoint3x4(new Vector3(0,0,.26f+length));return true;
        }
        /// <summary>The blocker as Core's guard sees it: third person its rig (chest bone, yaw, blades), first person the view
        /// camera and the view weapon. A pilot with no rig (the prototype's original melee) guards from its body.</summary>
        private NativeGuardPose BlockPose(Actor a,float time,bool view,NativeGuardForm form)
        {
            var pose=new NativeGuardPose();CapturedAnimation.Player rig;
            if(view)
            {
                var eye=ViewCamera.transform;rig=viewRig;
                pose.Chest=N(eye.TransformPoint(U(NativeBlockFx.ViewChest(form))));pose.Forward=N(eye.forward);pose.Up=N(eye.up);pose.View=true;pose.Scale=NativeBlockFx.ViewScale(form);
            }
            else
            {
                rigs.TryGetValue(a.Id,out rig);Vector3 facing=rig!=null?Flat(rig.Root.transform.forward,Facing(a)):Facing(a);
                pose.Chest=N(rig!=null?rig.Bones[CapturedRig.Chest].position:Position(a)+Vector3.up*1.15f);pose.Forward=N(facing);pose.Up=N(Vector3.up);pose.Scale=1;
            }
            if(form==NativeGuardForm.Ward||!BladeLine(rig,a,time,view,false,out var b0,out var t0))return pose;
            pose.HasBlade=true;pose.Base0=N(b0);pose.Tip0=N(t0);
            if(form==NativeGuardForm.Twin&&BladeLine(rig,a,time,view,true,out var b1,out var t1)){pose.Twin=true;pose.Base1=N(b1);pose.Tip1=N(t1);}
            return pose;
        }
        private static NativeBlockEye BlockEye(Camera camera)=>camera==null?default:new NativeBlockEye{Position=N(camera.transform.position),Up=N(camera.transform.up)};
        // The newest clash or deflect on this blocker that still flashes its guard.
        private bool GuardHit(int blocker,float time,out BlockRecord hit)
        {
            for(int i=blockRecords.Count-1;i>=0;i--){var r=blockRecords[i];float age=time-r.At;if(r.Blocker==blocker&&age>=0&&age<NativeBlockFx.HoldSeconds){hit=r;return true;}}
            hit=default;return false;
        }
        /// <summary>The block effect for this frame (Render, after the rigs and the view weapon are posed): guards, clashes and
        /// deflects into the match camera's mesh and the view camera's.</summary>
        private void RenderBlockFx(Snapshot s,float time)
        {
            if(s?.Actors==null)return;ObserveBlocks(s,time);
            bool any=blockRecords.Count>0;if(!any)foreach(var a in s.Actors)if(a!=null&&a.Alive&&time<a.BlockUntil){any=true;break;}
            if(!any)return;
            if(blockDraw==null){if(blockDrawFailed)return;blockDraw=NativeBlockFxDraw.Create(gameObject);blockDrawFailed=blockDraw==null;if(blockDraw==null)return;}
            blockWorld.Clear();blockView.Clear();
            var worldCamera=WorldEye;bool viewOk=FirstPerson&&ViewCamera!=null&&viewRig!=null&&!ScopeActive;
            NativeBlockEye worldEye=BlockEye(worldCamera),viewEye=viewOk?BlockEye(ViewCamera):default;
            float quality=Mathf.Clamp01(WeaponEffectsQuality),dim=Mathf.Lerp(.75f,1,quality);bool low=LowGraphics;
            foreach(var a in s.Actors)
            {
                if(a==null||!a.Alive)continue;bool view=FirstPerson&&a.Id==LocalId;if(view&&!viewOk)continue;
                float openAt=NativeBlockFx.OpenAt(a.BlockAt,a.BlockUntil,BlockWindow(a));bool open=time>=openAt&&time<a.BlockUntil,hasHit=GuardHit(a.Id,time,out var hit);
                if(!open&&!hasHit)continue;
                var form=GuardForm(a);var pose=BlockPose(a,time,view,form);
                (view?blockView:blockWorld).GuardFor(pose,form,NativeBlockFx.GuardColor(GuardHex(a)),time,openAt,a.BlockUntil,hasHit,hit.Hit,time-hit.At,N(hit.Toward),hit.Seed,view?NativeBlockFx.FirstPersonStrength:1,low);
            }
            for(int i=0;i<blockRecords.Count;i++)
            {
                var r=blockRecords[i];float age=time-r.At;if(age<0)continue;
                var a=FindActor(s.Actors,r.Blocker);bool view=FirstPerson&&r.Blocker==LocalId;if(view&&(!viewOk||a==null||!a.Alive))continue;
                var form=a!=null?GuardForm(a):NativeGuardForm.Ward;var shape=NativeBlockFx.Shape(form);var color=NativeBlockFx.GuardColor(a!=null?GuardHex(a):NativeBlockFx.DefaultColor(null,0));
                NativeGuardPose pose;
                if(a!=null&&a.Alive)pose=BlockPose(a,time,view,form);
                else pose=new NativeGuardPose{Chest=N(r.Point),Forward=N(r.Toward),Up=N(Vector3.up),Scale=1};
                var mesh=view?blockView:blockWorld;var eye=view?viewEye:worldEye;float strength=(view?NativeBlockFx.FirstPersonHitStrength:1)*dim;
                if(r.Hit==NativeBlockHit.Clash)
                {
                    NativeClashFrame frame;
                    if(view)frame=NativeBlockFx.ClashFrame(pose,shape,pose.Chest,N(r.Toward),N(Vector3.up));
                    else{if(!r.Framed){r.Frame=NativeBlockFx.ClashFrame(pose,shape,N(r.Point),N(r.Toward),N(Vector3.up));r.Framed=true;blockRecords[i]=r;}frame=r.Frame;}
                    mesh.Clash(frame,color,age,r.Seed,quality,low,true,eye,strength);
                }
                else
                {
                    // First person the deflect leaves the view guard (the contact is the hidden body, at the eye).
                    NVector at=N(r.Point);float scale=1;
                    if(view){scale=pose.Scale;at=NativeBlockFx.Frame(pose,shape,0,out var g)?g.Centre:pose.Chest;}
                    mesh.Deflect(at,N(r.Reflect),N(Vector3.up),color,age,r.Seed,quality,low,eye,strength,scale);
                }
            }
            blockDraw.Low=low;blockDraw.Submit(blockWorld,worldCamera,0,false);if(viewOk)blockDraw.Submit(blockView,ViewCamera,10,true);
        }
        /// <summary>
        /// The clash hitch for the blocker's own camera (Core NativeBlockFx.Hitch: a 50 ms kick up and back with a little roll,
        /// never a freeze and never Time.timeScale), applied after the aim point was taken; the view is rebuilt from the look
        /// angles every frame, so it never accumulates. scale: the settings scale (camera shake on; reduced with Reduced effects).
        /// </summary>
        public void BlockHitch(Transform view,Snapshot s,int localId,float time,float scale)
        {
            if(view==null||s==null||!(scale>0))return;ObserveBlocks(s,time);
            for(int i=blockRecords.Count-1;i>=0;i--)
            {
                var r=blockRecords[i];if(r.Blocker!=localId||r.Hit!=NativeBlockHit.Clash)continue;
                if(NativeBlockFx.Hitch(time-r.At,Vector3.Dot(r.Toward,view.right),scale,out var k)){view.rotation*=Quaternion.Euler(k.Pitch,0,k.Roll);view.position-=view.forward*k.Back;}
                return;
            }
        }
    }

    /// <summary>
    /// The block effect's GPU side: the NativeBlockFx material and two dynamic meshes (the match camera's and the view camera's,
    /// both drawn in the same frame), refilled from Core's arrays with the advanced Mesh API. A component on the presentation's
    /// object, so its meshes and material go with it.
    /// </summary>
    internal sealed class NativeBlockFxDraw : MonoBehaviour
    {
        private sealed class Slot{public Mesh Mesh;public int Vertices=-1,Indices=-1;}
        private static readonly VertexAttributeDescriptor[] Layout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4)
        };
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private static readonly int LowId=Shader.PropertyToID("_BlockFxLow");
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        private Material material;private Slot world,view;
        /// <summary>The low graphics tier (drops the guard's sparkle and cell flicker in the shader).</summary>
        public bool Low{set{if(material!=null)material.SetFloat(LowId,value?1:0);}}
        public static NativeBlockFxDraw Create(GameObject host)
        {
            var shader=Resources.Load<Shader>("NativeBlockFx");
            if(shader==null||!shader.isSupported){Debug.LogWarning("NativeBlockFx shader is missing or unsupported; the block effect is off.");return null;}
            var draw=host.AddComponent<NativeBlockFxDraw>();
            draw.material=new Material(shader){name="Native block fx",renderQueue=(int)RenderQueue.Transparent+6,hideFlags=HideFlags.HideAndDontSave};
            draw.world=new Slot{Mesh=NewMesh("Native block fx (match camera)")};draw.view=new Slot{Mesh=NewMesh("Native block fx (view camera)")};return draw;
        }
        private static Mesh NewMesh(string name){var m=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};m.MarkDynamic();return m;}
        /// <summary>Uploads and draws one camera's mesh on its layer (0: the match camera, 10: the view camera).</summary>
        public void Submit(NativeBlockMesh data,Camera camera,int layer,bool viewLayer)
        {
            if(data==null||camera==null||material==null||data.VertexCount==0||data.IndexCount==0)return;var slot=viewLayer?view:world;var mesh=slot.Mesh;
            if(slot.Vertices!=data.Vertices.Length){mesh.SetVertexBufferParams(data.Vertices.Length,Layout);slot.Vertices=data.Vertices.Length;}
            if(slot.Indices!=data.Indices.Length){mesh.SetIndexBufferParams(data.Indices.Length,IndexFormat.UInt32);slot.Indices=data.Indices.Length;mesh.subMeshCount=1;}
            mesh.SetVertexBufferData(data.Vertices,0,0,data.VertexCount,0,Quiet);mesh.SetIndexBufferData(data.Indices,0,0,data.IndexCount,Quiet);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,data.IndexCount){bounds=Everywhere,firstVertex=0,vertexCount=data.VertexCount},Quiet);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,layer,camera,0,null,ShadowCastingMode.Off,false);
        }
        private void OnDestroy()
        {
            if(world!=null)CapturedAnimation.Release(world.Mesh);if(view!=null)CapturedAnimation.Release(view.Mesh);if(material!=null)CapturedAnimation.Release(material);
            world=view=null;material=null;
        }
    }
}
