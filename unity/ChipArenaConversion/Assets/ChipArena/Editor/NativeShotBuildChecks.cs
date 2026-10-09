using System;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        // Projectile visuals (C10): construction, one host frame with every kind of projectile (including an ability shot
        // with no definition and a Siren shot, which is skipped), pruning and teardown. No frame is rendered.
        private static void CheckShots(NativeArena arena)
        {
            var host=new GameObject("Projectile visual check");host.transform.SetParent(arena.Root.transform);var camera=host.AddComponent<Camera>();camera.enabled=false;
            int bullets=arena.Bullets.Count,abilities=arena.AbilityShots.Count,melee=arena.MeleeShots.Count;
            try
            {
                using(var shots=new NativeShotPresentation())
                {
                    arena.Bullets.Add(new NativeBullet{Id=600,Spec="pistol",Position=Vector3.up,Velocity=Vector3.forward*40,Radius=.045f});
                    arena.Bullets.Add(new NativeBullet{Id=601,Spec="fire",Position=Vector3.up*1.5f,Velocity=Vector3.forward*34,Radius=.14f,Burn=4});
                    arena.Bullets.Add(new NativeBullet{Id=602,Spec="siren",SirenKind="seeker",Position=Vector3.up*2,Radius=.3f});
                    arena.AbilityShots.Add(new NativeAbilityShot{Id=603,Owner=arena.Local,Position=Vector3.up*2.5f,Radius=.2f});
                    arena.AbilityShots.Add(new NativeAbilityShot{Id=604,Owner=arena.Local,Definition=Array.Find(arena.Content.abilities,a=>a.id=="frag-grenade"),Position=Vector3.up*3,Radius=.14f,Fuse=arena.Time+.5f,Resting=true});
                    arena.MeleeShots.Add(new NativeMeleeShot{Id=605,Original=arena.Local,Owner=arena.Local,Spec="daggers",Position=Vector3.up*3.5f,Velocity=Vector3.forward*30,Radius=.095f,Burn=3});
                    shots.Render(arena,camera,arena.Time,1,false);
                    // Pistol box, fire cone + 5 flame cones, the white fallback, the frag, the thrown dagger's 5 flame cones.
                    if(shots.Live!=5||shots.Parts<14||shots.Sparks<3)throw new Exception("Projectile visuals missed a projectile, a module or the fire puffs ("+shots.Live+" live, "+shots.Parts+" parts, "+shots.Sparks+" sparks)");
                    arena.Bullets.RemoveRange(bullets,arena.Bullets.Count-bullets);arena.AbilityShots.RemoveRange(abilities,arena.AbilityShots.Count-abilities);arena.MeleeShots.RemoveRange(melee,arena.MeleeShots.Count-melee);
                    shots.Render(arena,camera,arena.Time+.02f,1,false);if(shots.Live!=0||shots.Parts!=0)throw new Exception("Projectile visuals kept a removed projectile");
                    shots.Clear();if(shots.Sparks!=0||shots.Beams!=0)throw new Exception("Projectile visual reset leaked sparks or beams");
                }
            }
            finally
            {
                if(arena.Bullets.Count>bullets)arena.Bullets.RemoveRange(bullets,arena.Bullets.Count-bullets);
                if(arena.AbilityShots.Count>abilities)arena.AbilityShots.RemoveRange(abilities,arena.AbilityShots.Count-abilities);
                if(arena.MeleeShots.Count>melee)arena.MeleeShots.RemoveRange(melee,arena.MeleeShots.Count-melee);
                CapturedAnimation.Release(host);
            }
            Debug.Log("PASS native projectile visuals: bullets, fire bolt with puffs, null-definition and frag ability shots, thrown dagger modules, Siren skipped, pruning and reset (draw submission only)");
            CheckShotIdentity(arena);
        }
        // The runtime mapping (NativeShotViews with a NativeShotTracker) on real projectile objects: a reflect changes only
        // the owner in the source (reflectProjectile), so a reflected hadouken, rocket or replica bullet keeps its first look.
        private static void CheckShotIdentity(NativeArena arena)
        {
            var a=arena.Pilots.Find(p=>p.Seat.id==1);var b=arena.Pilots.Find(p=>p.Seat.id==2);if(a==null||b==null)throw new Exception("Projectile identity check needs seats 1 and 2");
            var chips=new System.Collections.Generic.Dictionary<string,int>(a.Seat.chips);bool replica=arena.Replica;var rocket=Array.Find(arena.Content.abilities,d=>d.id=="rocket");
            try
            {
                a.Seat.chips.Clear();a.Seat.chips["arcade-red"]=1;
                // Host hadouken thrown by seat 1 (Arcade Red) and blocked by seat 2: Owner is now 2, Original stays 1.
                var tracker=new NativeShotTracker();tracker.Begin(arena,arena.Generation,false,arena.Time);
                var hadouken=new NativeBullet{Id=610,Spec="ansatsuken",Hadoken=true,Owner=2,Original=1,Position=Vector3.up,Velocity=Vector3.forward*23,Radius=.24f};
                NativeShotViews.Bullet(arena,hadouken,tracker,arena.Time,out var v);
                if(v.Arcade!=1||NativeShotRules.HaloColor(v,0)!=NativeShotRules.HaloRed)throw new Exception("A reflected hadouken took the reflector's colour (Arcade "+v.Arcade+")");
                // An ability shot keeps its caster's look after the owner changes, and a hadouken after its firer's chips change.
                var shot=new NativeAbilityShot{Id=611,Owner=a,Definition=rocket,Position=Vector3.up*2,Velocity=Vector3.forward*30,Radius=.2f};
                NativeShotViews.Ability(arena,shot,tracker,arena.Time,out var before);shot.Owner=b;a.Seat.chips.Clear();
                tracker.Begin(arena,arena.Generation,false,arena.Time);NativeShotViews.Ability(arena,shot,tracker,arena.Time,out var after);NativeShotViews.Bullet(arena,hadouken,tracker,arena.Time,out v);
                if(after.Class!=before.Class||after.WeaponColor!=before.WeaponColor||NativeShotRules.LightIntensity(after)!=NativeShotRules.LightIntensity(before)||v.Arcade!=1)
                    throw new Exception("A reflected ability shot or hadouken changed its look ("+before.Class+" -> "+after.Class+")");
                // A replica bullet whose rebuilt spec follows the new owner keeps the first spec and so its model.
                arena.Replica=true;var guest=new NativeShotTracker();guest.Begin(arena,arena.Generation,true,arena.Time);
                var bullet=new NativeBullet{Id=612,Owner=1,Spec="pistol",Position=Vector3.up,Velocity=Vector3.forward*40,Radius=.045f};
                NativeShotViews.Bullet(arena,bullet,guest,arena.Time,out var first);bullet.Owner=2;bullet.Spec="fire";
                guest.Begin(arena,arena.Generation,true,arena.Time+.05f);NativeShotViews.Bullet(arena,bullet,guest,arena.Time,out var later);
                if(later.Spec!="pistol"||NativeShotRules.PrimaryShape(later)!=NativeShotRules.PrimaryShape(first)||later.Fire)throw new Exception("A reflected replica bullet changed its model ("+later.Spec+")");
            }
            finally{a.Seat.chips.Clear();foreach(var pair in chips)a.Seat.chips[pair.Key]=pair.Value;arena.Replica=replica;}
            Debug.Log("PASS native projectile identity: a reflected hadouken, rocket and replica bullet keep the look they were fired with");
        }
    }
}
