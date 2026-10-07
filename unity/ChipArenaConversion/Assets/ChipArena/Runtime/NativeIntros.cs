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
        private void IntroHud(float width,float height)
        {
            GUI.Box(new Rect(0,0,width,82),"");GUI.Box(new Rect(0,height-116,width,116),"");
            if(IntroAge>=NativeIntroRules.NameAt(IntroStyle)){GUI.Label(new Rect(45,height-102,width-90,45),IntroPilot.Seat.name,heading);GUI.Label(new Rect(45,height-58,width-90,40),IntroPilot.Seat.introText,label);}
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player introRig;private int introActor;
        public void RenderIntro(NativeArena arena,NativePilot pilot,string style,float age,Camera camera)
        {
            if(pilot==null)return;if(introRig==null||introActor!=pilot.Seat.id){introRig?.Dispose();introRig=captured.Create(transform,-100);introActor=pilot.Seat.id;}
            sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            var actor=arena.Snapshot().Actors.FirstOrDefault(a=>a.Id==pilot.Seat.id)??pilot.Pose;NativeSourceMotion.Intro(introRig,style,age,camera);
            SourceCharacter(introRig,actor,arena.Time);Add(3,Matrix4x4.TRS(new Vector3(0,-.08f,0),Quaternion.identity,new Vector3(40,.1f,40)));
            if(style=="dev-siren")
            {Add(5,Matrix4x4.TRS(new Vector3(0,1.6f,-2.2f),Quaternion.identity,new Vector3(7,3.4f,.2f)));Add(5,Matrix4x4.TRS(new Vector3(2.2f,1.6f,-1),Quaternion.identity,new Vector3(.2f,3.4f,7)));}
            else{Ring(new Vector3(0,.01f,0),2.3f,0,age*8);Ring(new Vector3(0,.02f,0),3.2f,1,-age*5);}
            DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);
        }
    }
}
