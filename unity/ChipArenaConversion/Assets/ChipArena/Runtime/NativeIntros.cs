using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool IntroActive=>match?.Phase==NativePhase.Intro||arena?.Pve?.Holding==true&&arena.Pve.Cinematic=="intro";
        private NativePilot IntroPilot=>match.Phase==NativePhase.Intro?arena.Pilots.Find(p=>p.Seat.id==match.IntroSeat):arena.Pve.Boss;
        // The staged entrance (NativeIntroRules.Entrance): the replicated style, or the source's fallback when it has no stage.
        private string IntroStyle=>NativeIntroRules.Entrance(match.Phase==NativePhase.Intro?match.IntroStyle:"dev-siren");
        private float IntroAge=>match.Phase==NativePhase.Intro?(float)(match.Time-match.IntroStarted):arena.Pve.HoldDuration-arena.Pve.HoldLeft;
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player introRig;private int introActor;private Func<int,Matrix4x4> introHand;
        public void RenderIntro(NativeArena arena,NativePilot pilot,string style,float age,Camera camera)
        {
            if(pilot==null)return;style=NativeIntroRules.Entrance(style);sirenLookCamera=camera;if(introRig==null||introActor!=pilot.Seat.id){introRig?.Dispose();introRig=captured.Create(transform,-100);introActor=pilot.Seat.id;}
            sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            var actor=arena.Snapshot().Actors.FirstOrDefault(a=>a.Id==pilot.Seat.id)??pilot.Pose;NativeSourceMotion.Intro(introRig,style,age,camera);
            if(style!="dev-siren"){SourceCharacter(introRig,actor,arena.Time,weapon:false);IntroWeapon(style);TeamBands(introRig,NativeStatusVisuals.PilotBand(arena.Match.Teams,arena.Match.Cooperative,pilot.Seat.AuthorityBoss,pilot.Seat.team,pilot.Seat.id==arena.LocalId));}if(!SourceStage(style,age,camera))throw new System.InvalidOperationException("Missing source entrance stage: "+style);
            DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);
        }
        // player-intros.ts: the bare intro rig holds only a weapon intro's own weapon (WEAPON_INTROS[style].spec) or the Emperor's
        // gauntlets, at its hand sockets with no chip dressing; every other entrance keeps its hands empty, so the L33T Throne's floating staff (a
        // stage prop, grabbed at 4 s) is the only staff. Staff models ride the left hand bone, where the arena's grip puts them.
        private void IntroWeapon(string style)
        {
            string model=NativeIntroRules.IntroWeapon(style);if(model==null)return;
            if(introHand==null)introHand=b=>introRig.Bones[b].localToWorldMatrix;
            sourceModels.Queue(model,introRig.Root.transform.localToWorldMatrix,introHand);
        }
    }
}
