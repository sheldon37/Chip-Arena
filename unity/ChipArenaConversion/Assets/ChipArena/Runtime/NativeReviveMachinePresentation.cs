using System;
using System.Collections.Generic;
using UnityEngine;
using NMatrix=System.Numerics.Matrix4x4;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The hub's REVIVE-O-MATIC in the arena (docs/REVIVE-MACHINE.md). The models and every moment of the show are Core's (NativeReviveMachineModels, NativeReviveMachine); this draws them, flies the spat pilot's drawn body, and
    // emits the effects. Presentation only: the only solid thing it adds is the machine's collider (NativeArena below). Nothing here reaches the authority, the network frame, money or who is alive.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Dev Sandbox's Preview Revive Machine, and whether it runs through its states by itself. Off everywhere else.</summary>
        public bool ReviveMachineGallery,ReviveMachineAuto;
        public readonly NativeReviveMachinePreview ReviveMachinePreview=new NativeReviveMachinePreview();
        /// <summary>The machine this frame: whether one stands here, where, and the seconds from the money (below 0: idling).</summary>
        public bool ReviveMachineStands{get;private set;}
        public Vector3 ReviveMachineAt{get;private set;}
        public float ReviveMachineYaw{get;private set;}
        public float ReviveMachineSeq{get;private set;}=-1;
        public float ReviveMachineClock{get;private set;}
        private readonly List<(string id,NMatrix m)> reviveMachineParts=new List<(string,NMatrix)>(96);
        private NativeReviveSpit reviveMachineSpit;private Func<int,Color> reviveMachineTint;private Color reviveLights,reviveBankA,reviveBankB,reviveHeat;private bool reviveWasGallery;
        private Actor reviveStandIn;
        private readonly Dictionary<int,(Vector3 pivot,Quaternion turn)> reviveTumbles=new Dictionary<int,(Vector3,Quaternion)>();
        /// <summary>Start of Render: this frame's machine and board.</summary>
        private void ReviveMachineBegin(Snapshot s,float time)
        {
            var arena=WeaponLoadout;reviveTumbles.Clear();ReviveMachineClock=time;ReviveMachineStands=false;ReviveMachineSeq=-1;reviveMachineSpit=null;if(arena==null)return;
            bool gallery=(ReviveMachineGallery||ReviveMachineAuto)&&arena.Sandbox;
            if(gallery)
            {
                Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
                if(!reviveWasGallery||!ReviveMachinePreview.Placed){var feet=me!=null?(visualPositions.TryGetValue(me.Id,out var v)?v:Vec(me.Position)):Vector3.zero;ReviveMachinePreview.Reset();ReviveMachinePreview.Place(new NVector(feet.x,feet.y,feet.z),me!=null?me.Yaw*Mathf.Deg2Rad:0);}
                NativeReviveMachine.Board=ReviveMachinePreview.Board;if(ReviveMachineAuto)ReviveMachinePreview.Script(time);else{ReviveMachinePreview.WantOpen=false;ReviveMachinePreview.WantPress=-1;}
                var at=ReviveMachinePreview.At;ReviveMachineAt=new Vector3(at.X,at.Y,at.Z);ReviveMachineYaw=ReviveMachinePreview.Yaw;ReviveMachineStands=true;
                // The stand-in is an actor of the frame while it flies and stands, so it takes the real pilot's whole path: the pose, the model's clip, the tumble.
                var spit=NativeReviveMachine.Current(NativeReviveMachine.Board,time)??reviveLastSpit;reviveLastSpit=spit;
                if(me!=null&&spit!=null&&time>=spit.At+NativeReviveMachine.Spit&&time<spit.At+NativeReviveMachine.End+2.5f)
                {
                    var a=reviveStandIn??(reviveStandIn=me.ViewCopy());a.Id=NativeReviveMachinePreview.StandIn;a.Bot=true;a.Hp=100;a.Yaw=(ReviveMachineYaw+Mathf.PI)*Mathf.Rad2Deg;a.AimYaw=a.Yaw;a.Pitch=0;
                    a.Position=new V3(spit.Land.x,spit.Land.y+.035f,spit.Land.z);a.Velocity=new V3();a.Grounded=true;a.FlashUntil=-100;a.DownAt=-100;a.DownUntil=0;a.DownLanded=-1;a.DownRoll=-1;a.DownRollEnd=-1;a.LastShot=-100;a.LastAttack=-100;
                    if(s?.Actors!=null&&Array.IndexOf(s.Actors,a)<0){var list=new Actor[s.Actors.Length+1];s.Actors.CopyTo(list,0);list[s.Actors.Length]=a;s.Actors=list;}
                }
            }
            else
            {
                if(reviveWasGallery){ReviveMachinePreview.Placed=false;reviveLastSpit=null;reviveStandIn=null;if(ReferenceEquals(NativeReviveMachine.Board,ReviveMachinePreview.Board))NativeReviveMachine.Board=null;}
                if(NativeReviveMachine.Spot(arena.MapId,out var at,out float yaw)&&arena.Expedition!=null){ReviveMachineAt=new Vector3(at.X,at.Y,at.Z);ReviveMachineYaw=yaw;ReviveMachineStands=true;}
            }
            reviveWasGallery=gallery;
            if(ReviveMachineStands){reviveMachineSpit=NativeReviveMachine.Current(NativeReviveMachine.Board,time);if(reviveMachineSpit!=null)ReviveMachineSeq=time-reviveMachineSpit.At;}
        }
        private NativeReviveSpit reviveLastSpit;
        // The drawn root of a pilot being spat out (the rig only: the authority's capsule stands on the landing), and the game's knockdown moments, so both bodies take its poses: flight, lying, roll and rise.
        private Vector3 ReviveMachinePlace(Actor a,Vector3 p,float time)
        {
            var spit=NativeReviveMachine.SpitOf(NativeReviveMachine.Board,a.Id,time);if(spit==null||!ReviveMachineStands)return p;
            float t=time-spit.At;
            if(t<NativeReviveMachine.Spit){a.Hp=0;return p;}
            if(t>=NativeReviveMachine.UpAt)return p;
            var mouth=NVector.Transform(NativeReviveMachine.Mouth,NativeReviveMachine.Frame(new NVector(ReviveMachineAt.x,ReviveMachineAt.y,ReviveMachineAt.z),ReviveMachineYaw));var land=new NVector(spit.Land.x,spit.Land.y,spit.Land.z);
            if(!NativeReviveMachine.Flying(t,mouth,land,ReviveRootLift,out var at,out float tumble,out float roll))return p;
            NativeReviveMachine.DownTimes(spit.At,out float downAt,out float landed,out float pickup,out float rollAt,out float rollEnd,out float until);
            a.DownAt=downAt;a.DownLanded=time>=landed?landed:-1;a.DownPickup=pickup;a.DownRoll=rollAt;a.DownRollEnd=rollEnd;a.DownUntil=until;a.DownUsed=false;a.Grounded=t>=NativeReviveMachine.Touch2;
            var q=Vec3(at);var along=Vec3(land-mouth);along.y=0;along=along.sqrMagnitude>.01f?along.normalized:Vector3.forward;var right=Vector3.Cross(Vector3.up,along);
            if(Mathf.Abs(tumble)>.001f||Mathf.Abs(roll)>.001f)reviveTumbles[a.Id]=(q+Vector3.up*NativeReviveMachine.RootCentre,Quaternion.AngleAxis(tumble*Mathf.Rad2Deg,right)*Quaternion.AngleAxis(roll*Mathf.Rad2Deg,along));
            return q;
        }
        private static Vector3 Vec3(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        /// <summary>A grounded pilot's rig root above its feet: the capsule's half height less the root's drop.</summary>
        private const float ReviveRootLift=NativeLocomotionRules.HalfHeight-NativeReviveMachine.RootCentre;
        // After the rig is posed, before the model binds to it: the tumble, about the body's centre.
        private void ReviveMachineTumble(CapturedAnimation.Player rig,Actor a)
        {
            if(reviveTumbles.Count==0||!reviveTumbles.TryGetValue(a.Id,out var t))return;var root=rig.Root.transform;root.SetPositionAndRotation(t.pivot+t.turn*(root.position-t.pivot),t.turn*root.rotation);
        }
        /// <summary>The source-model pass (beside the PvE machines): the machine, each part where the show has put it.</summary>
        private void ReviveMachineWorld(NativeArena arena)
        {
            if(!ReviveMachineStands||sourceModels==null)return;float t=ReviveMachineSeq,clock=ReviveMachineClock;
            NativeReviveMachine.PoseParts(t,reviveMachineParts,clock);
            var root=Matrix4x4.TRS(ReviveMachineAt,Quaternion.Euler(0,ReviveMachineYaw*Mathf.Rad2Deg,0),Vector3.one);
            float sign=NativeReviveMachine.Sign(t,clock);reviveLights=new Color(sign,sign,sign);float a=NativeReviveMachine.Bulbs(0,t,clock),b=NativeReviveMachine.Bulbs(1,t,clock),h=NativeReviveMachine.Heat(t,clock);reviveBankA=new Color(a,a,a);reviveBankB=new Color(b,b,b);reviveHeat=new Color(h,h,h);
            reviveMachineTint??=ReviveMachineTintOf;
            for(int i=0;i<reviveMachineParts.Count;i++){var (id,m)=reviveMachineParts[i];sourceModels.Queue(id,root*UnityM(m),partTint:reviveMachineTint);}
        }
        private Color ReviveMachineTintOf(int bone)=>bone==NativeReviveMachineModels.Lights?reviveLights:bone==NativeReviveMachineModels.BulbsA?reviveBankA:bone==NativeReviveMachineModels.BulbsB?reviveBankB:bone==NativeReviveMachineModels.Gullet?reviveHeat:Color.white;
        /// <summary>RenderGlow: its steam, sparks, confetti and dust.</summary>
        private void ReviveMachineFx()
        {
            if(!ReviveMachineStands||kitScene==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            float t=ReviveMachineSeq,clock=ReviveMachineClock,q=Mathf.Clamp01(WeaponEffectsQuality);var at=new NVector(ReviveMachineAt.x,ReviveMachineAt.y,ReviveMachineAt.z);var world=NativeReviveMachine.Frame(at,ReviveMachineYaw);
            var spit=reviveMachineSpit;bool has=spit!=null;var mouth=NVector.Transform(NativeReviveMachine.Mouth,world);var land=has?new NVector(spit.Land.x,spit.Land.y,spit.Land.z):NVector.Transform(NativeReviveMachine.DefaultLand,world);
            techniqueFx.DrawReviveMachine(techSink,world,NativeReviveMachine.HullPose(t,clock),t,clock,has,mouth,land,ReviveRootLift,q,LowGraphics);
        }
        /// <summary>The spat pilot's own camera: everyone else keeps theirs. It watches the machine from beside the mat through the wind-up, then follows the flight in. False when it isn't this viewer being spat.</summary>
        internal bool ReviveMachineDirect(NativeArena arena,Camera view,float time,bool shake)
        {
            if(view==null||arena==null||!ReviveMachineStands)return false;var spit=NativeReviveMachine.SpitOf(NativeReviveMachine.Board,arena.LocalId,time);if(spit==null)return false;float t=time-spit.At;if(t>=NativeReviveMachine.UpAt+.15f)return false;
            var world=NativeReviveMachine.Frame(new NVector(ReviveMachineAt.x,ReviveMachineAt.y,ReviveMachineAt.z),ReviveMachineYaw);var mouth=NVector.Transform(NativeReviveMachine.Mouth,world);var land=new NVector(spit.Land.x,spit.Land.y,spit.Land.z);
            var eye=Vec3(NVector.Transform(new NVector(3.6f,1.5f,7.6f),world));Vector3 look=Vec3(mouth);
            if(t>=NativeReviveMachine.Spit&&NativeReviveMachine.Flying(Mathf.Min(t,NativeReviveMachine.UpAt-.001f),mouth,land,ReviveRootLift,out var at,out _,out _)){var chest=Vec3(at)+Vector3.up*NativeReviveMachine.RootCentre;float k=Mathf.Clamp01((t-NativeReviveMachine.Spit)/.25f);look=Vector3.Lerp(look,chest,k);}
            float blend=Mathf.Clamp01(t/.3f)*Mathf.Clamp01((NativeReviveMachine.UpAt+.15f-t)/.3f);var rotation=Quaternion.LookRotation(look-eye,Vector3.up);
            if(shake){float sh=NativeReviveMachine.ShakeLevel(t);if(sh>.01f)rotation*=Quaternion.Euler(Mathf.Sin(t*53)*.5f*sh,Mathf.Sin(t*61)*.4f*sh,0);}
            view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position,eye,blend),Quaternion.Slerp(view.transform.rotation,rotation,blend));FirstPerson=false;AimHeld=false;return true;
        }
        /// <summary>For the screen and its prompt: the machine's use point in the world.</summary>
        public Vector3 ReviveMachineUsePoint(){var p=NVector.Transform(NativeReviveMachine.UsePoint,NativeReviveMachine.Frame(new NVector(ReviveMachineAt.x,ReviveMachineAt.y,ReviveMachineAt.z),ReviveMachineYaw));return Vec3(p);}
        /// <summary>The mouth in the world, for the comic word over it.</summary>
        public Vector3 ReviveMachineMouth(){var p=NVector.Transform(NativeReviveMachine.Mouth+new NVector(0,.5f,.2f),NativeReviveMachine.Frame(new NVector(ReviveMachineAt.x,ReviveMachineAt.y,ReviveMachineAt.z),ReviveMachineYaw));return Vec3(p);}
    }
    public sealed partial class NativeArena
    {
        private bool reviveMachineSolid;
        /// <summary>The machine's collider on this hub's map: plain boxes where it stands, on the host and every guest alike, so nobody walks through it (the mat and lane are flat paint). Nothing else about the map changes: the
        /// navigation stays the map's (no zombie comes in here: the sanctuary keeps them out).</summary>
        internal void ReviveMachineCollision()
        {
            if(reviveMachineSolid||!NativeReviveMachine.Spot(MapId,out var at,out float yaw))return;reviveMachineSolid=true;
            var frame=NativeReviveMachine.Frame(at,yaw);
            foreach(var (centre,size,turn) in NativeReviveMachine.Solids)
            {
                var c=NVector.Transform(centre,frame);var obj=new GameObject("Revive machine");obj.layer=PropLayer;obj.transform.SetParent(root.transform);obj.transform.position=new Vector3(c.X,c.Y,c.Z);obj.transform.rotation=Quaternion.Euler(0,(yaw+turn)*Mathf.Rad2Deg,0);
                var box=obj.AddComponent<BoxCollider>();box.size=new Vector3(size.X,size.Y,size.Z);
            }
            Physics.SyncTransforms();
        }
    }
    public sealed partial class NativeSourceModels
    {
        /// <summary>The REVIVE-O-MATIC's models, built at load with the other machines' (MachineSkins).</summary>
        private void ReviveMachineSkins(Action<string,WeaponMesh> add){foreach(var (id,build,_) in NativeReviveMachineModels.All)add(id,build());}
    }
}
