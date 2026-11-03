using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Backrooms' entities in the horde's draw (docs/EXPEDITION-ENEMY-REWORK.md section 4; the authority: Core
    // NativeBackroomsKinds and the zone frame's attack name and times, section 6). Their looks are the player's model's
    // (Core NativeZombieLooks: the Faceling's blank face, the Smiler's void and grin, the Drowned's striped costume, the
    // Lifeguard's hook); here their moves:
    // - (a) BackroomsAttack: the authority's names onto the gait. The wake, howl, blink, whistle and hook are their own motion
    //   (b), never the claw; the pounce, the lunges and the pole's sweep play the gait's takes at the authority's timings (the
    //   Hound's .35 s crouch and .5 s leap to contact at .85 s, the Smiler's .6 s and the Drowned's .4 s lunges, the sweep's .5 s);
    // - (b) BackroomsPosed: Core NativeBackroomsMotion over the gait's pose (the Faceling dormant at its wall and its head snapping
    //   round on the wake, the Hound's howl, the Lifeguard's whistle and hook), the Drowned sunk under the water while submerged.
    //   The Lifeguard's staff carry on the model gives way to them (BackroomsHold).
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        private static string BackroomsId(int kind){var z=NativeHordeZones.Of(kind);return z!=null&&NativeBackroomsKinds.Own(z.Id)?z.Id:null;}

        // ---- (a) the authority's moves onto the gait ----
        private static void BackroomsAttack(NativeMinion m,int kind,float now,ref float age,ref NativeHordeKinds.Attack attack,ref float strike,ref float recover)
        {
            var id=BackroomsId(kind);if(id==null)return;
            switch(m.Pve?.Zone.attack)
            {
                case "wake":case "howl":case "blink":case "whistle":case "hook":age=float.NaN;return;
                case "pounce":attack=NativeHordeKinds.Attack.Pounce;strike=.85f;recover=1.65f;break;
                case "lunge":attack=NativeHordeKinds.Attack.Lunge;strike=id=="smiler"?.6f:.4f;recover=strike+.8f;break;
                case "sweep":attack=NativeHordeKinds.Attack.Swipe;strike=.5f;recover=1.1f;break;
                default:return;
            }
            float a=now-m.LastAttack;age=a>=0&&a<=recover+.3f?a:float.NaN;
        }

        /// <summary>A Backrooms entity's jaw over the gait's (the Hound's howl wide open).</summary>
        private static void BackroomsJaw(NativeMinion m,int kind,float now,ref float jaw)
        {if(BackroomsId(kind)=="hound"&&m.Pve?.Zone.attack=="howl")jaw=Mathf.Max(jaw,NativeBackroomsMotion.HowlJaw(now-m.LastAttack));}
        // ---- (b) their own motion over the gait's pose ----
        private readonly Dictionary<int,float> backroomsHold=new Dictionary<int,float>();
        /// <summary>How much of the Lifeguard's arms its own move holds now (its staff carry on the model eases off by it).</summary>
        private float BackroomsHold(int id)=>backroomsHold.TryGetValue(id,out var k)?k:0;
        private void BackroomsPosed(NativeArena arena,NativeMinion m,HordeKindState s,int kind,float now,in HordeMotionInput f,ref Vector3 pos)
        {
            var id=BackroomsId(kind);var z=m.Pve?.Zone;if(id==null||z==null)return;float age=now-m.LastAttack,hold=0;
            switch(id)
            {
                case "faceling":
                    if(z.dormant)NativeBackroomsMotion.Dormant(minionPose,NativeHordeKinds.Seed(m.Id),now);
                    else if(z.attack=="wake"&&f.HasTarget){var look=s.Gait.ToLocal(f.Target,f.Root);look.Y=0;NativeBackroomsMotion.Wake(minionPose,age,look);}
                    break;
                case "hound":
                    if(z.attack=="howl")NativeBackroomsMotion.HowlPose(minionPose,age);
                    break;
                case "lifeguard":
                    if(z.attack=="whistle")hold=NativeBackroomsMotion.WhistlePose(minionPose,age);
                    else if(z.attack=="hook")
                    {
                        float pull=NativeZoneCombat.View(z.pullAt);var pilot=arena!=null&&z.pullTarget>=0?HordePilot(arena,z.pullTarget):null;
                        var at=pilot!=null?s.Gait.ToLocal(ToN(pilot.Body.position)/s.Scale,f.Root):new NVector(0,1.2f,3);
                        hold=NativeBackroomsMotion.HookPose(minionPose,float.IsNaN(pull)?age:now-pull,at);
                    }
                    break;
                case "drowned":
                    pos.y-=NativeBackroomsMotion.Sink(z.submerged,now-NativeZoneCombat.View(z.surfaceAt))*s.Scale;
                    break;
            }
            if(id=="lifeguard")backroomsHold[m.Id]=hold;
        }
    }
}
