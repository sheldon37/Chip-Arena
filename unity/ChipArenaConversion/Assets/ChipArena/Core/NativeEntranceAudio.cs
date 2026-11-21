using Beat = ChipArena.UnityConversion.NativeBombSounds.IntroCue;

namespace ChipArena.UnityConversion
{
    public static class NativeEntranceAudio
    {
        private static readonly Beat[] Crescent = {
            new Beat(NativeGlaiveIntro.EnterAt, "melee.sword.swing", "sword", .45f),
            new Beat(NativeGlaiveIntro.Hit1, "melee.block", "metal", .52f),
            new Beat(NativeGlaiveIntro.Hit2, "melee.block", "metal", .64f),
            new Beat(NativeGlaiveIntro.CatchAt, "gun.rocket.dud", "metal", .48f),
            new Beat(NativeGlaiveIntro.LandAt, "move.land", "stone", .64f),
            new Beat(NativeGlaiveIntro.FlingAt, "melee.greatsword.spin", "dash", .53f),
            new Beat(NativeGlaiveIntro.ReturnAt, "gun.rocket.dud", "metal", .46f),
            new Beat(NativeGlaiveIntro.SlashAt, "melee.sword.swing", "sword", .86f)
        };
        private static readonly Beat[] Moon = {
            new Beat(NativeKatanaIntro.GrabAt, "chip.bomb.attach", "metal", .29f),
            new Beat(NativeKatanaIntro.WhipAt, "melee.miss", "dash", .49f),
            new Beat(NativeKatanaIntro.CutAt, "melee.sword.swing", "sword", .83f),
            new Beat(NativeKatanaIntro.ClickAt, "gun.rocket.dud", "metal", .59f)
        };
        private static readonly Beat[] Fangs = {
            new Beat(NativeFangsIntro.FormAt, "status.poison-tick", "water", .35f),
            new Beat(NativeFangsIntro.StepAt, "magic.blink-strike", "demon-step", .55f),
            new Beat(NativeFangsIntro.FlurryAt, "melee.daggers.swing", "daggers", .58f),
            new Beat(NativeFangsIntro.LeapAt, "melee.daggers.throw", "dash", .55f),
            new Beat(NativeFangsIntro.StrikeAt, "blast.toxic", "darkness", .66f)
        };
        private static readonly Beat[] HeavyTurn = {
            new Beat(NativeDevGreatswordIntro.HeaveAt, "melee.greatsword.swing", "greatsword", .52f),
            new Beat(NativeDevGreatswordIntro.BiteAt, "structure.thud", "stone", .78f),
            new Beat(NativeDevGreatswordIntro.Beat, "melee.greatsword.spin", "greatsword", .65f),
            new Beat(NativeDevGreatswordIntro.RestAt, "gun.rocket.dud", "metal", .3f)
        };
        private static readonly Beat[] Flourish = {
            new Beat(NativeDevKatanaIntro.DrawAt, "melee.miss", "dash", .36f),
            new Beat(NativeDevKatanaIntro.SweepAt, "melee.sword.swing", "sword", .57f),
            new Beat(NativeDevKatanaIntro.WheelAt, "melee.daggers.swing", "daggers", .45f),
            new Beat(NativeDevKatanaIntro.Beat, "melee.sword.swing", "sword", .85f)
        };
        private static readonly Beat[] Hardlight = {
            new Beat(NativeCyberLordIntroScript.FormAt, "status.shock-zap", "lightning", .52f),
            new Beat(NativeCyberLordIntroScript.JabHit, "dev.fist.swing", "arcade-punch", .42f),
            new Beat(NativeCyberLordIntroScript.CrossHit, "dev.fist.swing", "arcade-punch", .55f),
            new Beat(NativeCyberLordIntroScript.PalmHit, "impact.energy", "electric-hook-hit", .78f)
        };
        public static Beat[] Timeline(string style)
        {
            switch(style)
            {
                case NativeGlaiveIntro.Id: return Crescent;
                case NativeKatana.Entrance: return Moon;
                case NativeFangs.Entrance: return Fangs;
                case NativeDevMotions.GreatswordEntrance: return HeavyTurn;
                case NativeDevMotions.KatanaEntrance: return Flourish;
                case CyberLordSpec.Entrance: return Hardlight;
                case NativeBombs.Entrance: return NativeBombSounds.Intro;
                default: return System.Array.Empty<Beat>();
            }
        }
    }

    public sealed class NativeEntranceCueClock
    {
        private string instance,style;
        private float through,from;

        public bool Step(string nextInstance,string nextStyle,float age)
        {
            bool changed=instance!=nextInstance||style!=nextStyle;
            instance=nextInstance;
            style=nextStyle;
            from=changed?age-.035f:System.Math.Max(through,age-.12f);
            through=changed?age:System.Math.Max(through,age);
            return changed;
        }
        public bool Crossed(float at)=>instance!=null&&at>from&&at<=through;
    }
}
