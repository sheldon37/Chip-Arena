using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The pistol and machine-gun Q kit's presentation in the arena (Claude; the rules proposed to Astra in
    // docs/RANGED-ABILITIES.md). The pilot poses are Core/NativeGunKitMotion (reached through RangedAbilityPose on both gun rig
    // routes); the effects are Core/NativeGunKitFx, drawn here from what every peer holds: the snapshot's actors (their
    // replicated FanAt/FanShots, OverdriveAt/OverdriveUntil/VentUntil and LastShot), the posed guns (the third-person rigs, the
    // first-person view weapon) and the rounds in flight (their owner and birth time warm the tracers). Host, guests,
    // spectators and the killcam replay draw the same frame. Hooks: GunKitFx and the tracer look in WeaponFx (before the weapons
    // drawer is submitted) and GunKitKick (ConversionGame, first person, camera shake setting). Presentation only: nothing here
    // reaches the authority, the network frame, hit timing, hitboxes, damage, cooldowns or movement.
    public sealed partial class ArenaPresentation
    {
        private GunKitScene gunKitScene;private GunKitSink gunKitSink;
        /// <summary>WeaponFx, before the impacts and the submit: every kit effect of the frame on the pose clock.</summary>
        private void GunKitFx()
        {
            var s=weaponSnapshot;if(s?.Actors==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;
            gunKitScene??=new GunKitScene(this);gunKitSink??=new GunKitSink();gunKitScene.Bind(s,kitClock);gunKitSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            NativeGunKitFx.Draw(gunKitSink,gunKitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
        /// <summary>The tracer look of a round (its owner's kit at the round's birth); Kit false keeps the milestone tracer.</summary>
        private NativeTracerLook GunKitTracer(NativeBullet b,in NativeShotView v)
        {
            var actors=weaponSnapshot?.Actors;Actor owner=null;
            if(actors!=null)for(int i=0;i<actors.Length;i++)if(actors[i]!=null&&actors[i].Id==b.Original){owner=actors[i];break;}
            return NativeTracerLook.Of(owner,b.Born,v.Spec,kitClock);
        }
        /// <summary>A kit round's tracer: the milestone streak and sheath at the look's width, length, gain and edge colour.</summary>
        private static void KitTracer(NativeWeaponFxDraw draw,Vector3 at,Vector3 velocity,float travelled,in NativeTracerLook look)
        {
            float speed=velocity.magnitude;if(!(speed>.01f))return;float length=Mathf.Max(.08f,Mathf.Min(NativeTracerFx.Length(speed,travelled)*look.Length,travelled));Vector3 dir=velocity/speed;
            draw.Axis(false,at-dir*length,at,NativeWeaponFxShapes.Lin(look.Edge,look.Gain),look.Width,NativeWeaponFxDraw.StreakMode,.85f);
            draw.Axis(false,at-dir*(length*.6f),at,NativeWeaponFxShapes.Lin(look.Edge,look.Sheath),look.Width*4,NativeWeaponFxDraw.StreakMode,.95f);
        }
        /// <summary>
        /// The first-person camera kick of the local pilot's kit (Core/NativeGunKitMotion.CameraKick), after the aim point: the
        /// view is rebuilt from the look angles next frame. ConversionGame calls it only with the camera shake setting on.
        /// </summary>
        public void GunKitKick(Transform view,Snapshot s,int localId,float time,float scale)
        {
            if(!FirstPerson||view==null||s?.Actors==null)return;Actor a=null;foreach(var x in s.Actors)if(x!=null&&x.Id==localId){a=x;break;}
            NativeGunKitMotion.CameraKick(a,time,out float pitch,out float yaw);if(pitch==0&&yaw==0)return;
            view.rotation=view.rotation*Quaternion.Euler(-pitch*scale,yaw*scale,0);
        }

        // What the Core effects read of this presentation (allocation-free once bound).
        private sealed class GunKitScene:IGunKitScene
        {
            private readonly ArenaPresentation p;private readonly List<Actor> actors=new List<Actor>();
            public GunKitScene(ArenaPresentation owner){p=owner;}
            public void Bind(Snapshot s,float time){actors.Clear();if(s.Actors!=null)actors.AddRange(s.Actors);}
            public IReadOnlyList<Actor> Actors=>actors;
            public int Viewer=>p.FirstPerson&&!p.ScopeActive&&p.ViewCamera!=null&&p.viewRig!=null?p.LocalId:0;
            public bool Gun(Actor a,bool view,out GunFrame frame)
            {
                frame=default;CapturedAnimation.Player rig;
                if(view){if(Viewer==0||a.Id!=p.LocalId)return false;rig=p.viewRig;}
                else{if(p.FirstPerson&&a.Id==p.LocalId||!p.rigs.TryGetValue(a.Id,out rig))return false;}
                if(rig==null||rig.Bones==null||rig.Bones.Length<=CapturedRig.RHand)return false;
                var m=rig.Bones[CapturedRig.RHand].localToWorldMatrix;Vector3 z=m.MultiplyVector(Vector3.forward);float scale=z.magnitude;if(!(scale>1e-4f))return false;
                z/=scale;Vector3 x=m.MultiplyVector(Vector3.right).normalized,y=Vector3.Cross(z,x).normalized;Vector3 o=m.MultiplyPoint3x4(Vector3.zero);
                frame=new GunFrame{O=N(o),X=N(x),Y=N(y),Z=N(z),Scale=scale};return true;
            }
            public bool Eye(out NVector at,out NVector forward,out NVector up,out NVector right,out float tanHalf,out float aspect)
            {
                at=forward=up=right=default;tanHalf=aspect=0;var c=p.ViewCamera;if(c==null||Viewer==0)return false;var t=c.transform;
                at=N(t.position);forward=N(t.forward);up=N(t.up);right=N(t.right);tanHalf=Mathf.Tan(c.fieldOfView*.5f*Mathf.Deg2Rad);aspect=c.aspect>0?c.aspect:16/9f;return true;
            }
            private static NVector N(Vector3 v)=>new NVector(v.x,v.y,v.z);
        }
        // The effects' primitives onto the shared effects renderer and the weapons drawer (view: the first-person layer).
        private sealed class GunKitSink:IGunKitSink
        {
            private NativeVfxDraw vfx;private NativeWeaponFxDraw draw;private bool viewOk;
            public void Bind(NativeVfxDraw v,NativeWeaponFxDraw d,bool view){vfx=v;draw=d;viewOk=view;}
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static Color C(NVector c,float a=0)=>new Color(c.X,c.Y,c.Z,a);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),C(color,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),C(albedo),opacity,size,u,seed,floor);
            public void Sprite(bool view,NVector at,NVector color,float size,int profile){if(view&&!viewOk)return;draw.Glow(view,U(at),C(color),size,profile);}
            public void Line(bool view,NVector tail,NVector head,NVector color,float width,bool shard){if(view&&!viewOk)return;draw.Axis(view,U(tail),U(head),C(color),width,shard?NativeWeaponFxDraw.ShardMode:NativeWeaponFxDraw.StreakMode,shard?0:.85f);}
            public void Band(bool view,NVector a,NVector b,NVector c,NVector d,float ua,float ub,NVector color)
            {if(view&&!viewOk)return;draw.Quad(view,U(a),U(b),U(c),U(d),new Vector2(ua,-1),new Vector2(ub,-1),new Vector2(ub,1),new Vector2(ua,1),C(color),NativeWeaponFxDraw.Ribbon,0);}
            public void Wisp(bool view,NVector at,NVector color,float coverage,float size,float seed,float roll){if(view&&!viewOk)return;draw.Wisp(view,U(at),C(color,coverage),size,seed,roll);}
        }
    }
}
