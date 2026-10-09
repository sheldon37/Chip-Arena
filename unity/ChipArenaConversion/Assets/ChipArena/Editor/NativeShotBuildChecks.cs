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
        }
    }
}
