using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool IntroActive=>match?.Phase==NativePhase.Intro||arena?.Pve?.Holding==true&&arena.Pve.Cinematic=="intro";
        private NativePilot IntroPilot=>match.Phase==NativePhase.Intro?arena.Pilots.Find(p=>p.Seat.id==match.IntroSeat):arena.Pve.Boss;
        private string IntroStyle=>match.Phase==NativePhase.Intro?match.IntroStyle:"dev-siren";
        private float IntroAge=>match.Phase==NativePhase.Intro?(float)(match.Time-match.IntroStarted):arena.Pve.HoldDuration-arena.Pve.HoldLeft;
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player introRig;private int introActor;
        public void RenderIntro(NativeArena arena,NativePilot pilot,string style,float age,Camera camera)
        {
            if(pilot==null)return;if(introRig==null||introActor!=pilot.Seat.id){introRig?.Dispose();introRig=captured.Create(transform,-100);introActor=pilot.Seat.id;}
            sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            var actor=arena.Snapshot().Actors.FirstOrDefault(a=>a.Id==pilot.Seat.id)??pilot.Pose;NativeSourceMotion.Intro(introRig,style,age,camera);
            if(style!="dev-siren"){SourceCharacter(introRig,actor,arena.Time);TeamBands(introRig,NativeStatusVisuals.BandTeam(arena.Match.Teams,arena.Match.Cooperative,pilot.Seat.AuthorityBoss,pilot.Seat.team));}if(!SourceStage(style,age,camera))throw new System.InvalidOperationException("Missing source entrance stage: "+style);
            DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);
        }
    }
}
