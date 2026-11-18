using System;

namespace ChipArena.UnityConversion
{
    // The Blind Pick weapon flourishes' plan (owner 2026-11-18: "Give each weapon choice a selection animation for the player ...
    // they should look very nice ... like if they select pistol ... a pistol will eject from the icon and shoot around etc.
    // Be creative!"). Engine-free, so the managed checks hold it: which move each weapon plays, how long its selection and
    // lock-in flourishes run, the short quiet sounds they cue (an effects bank key, today's fallback clip, or a Top Dog toon
    // sound) and the clock that cues each sound once. Runtime/NativeWeaponFlourish*.cs draws the moves. Presentation only:
    // nothing here reads or writes rules, timers or the network, and only the local pilot's own screen plays them.
    public static class NativeWeaponFlourishPlan
    {
        public enum Move
        {
            Generic,Pistol,Rifle,MachineGun,Fire,Water,Lightning,Darkness,Sword,Daggers,Shield,Greatsword,Bow,Katana,Glaive,Fangs,
            DevGreatsword,DevKatana,Bombadier,TopDog,Siren,Emperor,Ansatsuken,Leet,CyberLord
        }
        /// <summary>A sound at `At` seconds into a flourish: the effects bank's `Key` (null: none) with today's `Clip` from
        /// Resources/Audio/audio as its fallback (null: silent without the bank), or the Top Dog's toon sound `Toon`; `Gain` is
        /// kept low (a pick screen, not a fight).</summary>
        public struct Cue{public float At,Gain;public string Key,Clip,Toon;}
        /// <summary>The lock-in finisher's length (s); Reduced effects' gentle selection.</summary>
        public const float Finish=.95f,Gentle=.9f;

        private static readonly string[] ids={"pistol","rifle","machinegun","fire","water","lightning","darkness","sword","daggers","shield","greatsword","bow","katana","glaive","fangs","dev-greatsword","dev-katana","bombadier","topdog","siren","emperor","ansatsuken","leet","cyberlord"};
        private static readonly Move[] moves={Move.Pistol,Move.Rifle,Move.MachineGun,Move.Fire,Move.Water,Move.Lightning,Move.Darkness,Move.Sword,Move.Daggers,Move.Shield,Move.Greatsword,Move.Bow,Move.Katana,Move.Glaive,Move.Fangs,Move.DevGreatsword,Move.DevKatana,Move.Bombadier,Move.TopDog,Move.Siren,Move.Emperor,Move.Ansatsuken,Move.Leet,Move.CyberLord};
        /// <summary>The weapon ids that have their own move (every roster row; an unknown id plays Generic).</summary>
        public static int Count=>ids.Length;
        public static string Id(int i)=>ids[i];
        public static Move Of(string id){if(id!=null)for(int i=0;i<ids.Length;i++)if(ids[i]==id)return moves[i];return Move.Generic;}

        /// <summary>The selection flourish's length (s): 1-2 s each, the gentle one under Reduced effects.</summary>
        public static float Length(Move m,bool calm=false)
        {
            if(calm)return Gentle;
            switch(m)
            {
                case Move.Pistol:return 1.75f;case Move.Rifle:return 1.9f;case Move.MachineGun:return 1.85f;case Move.Fire:return 1.7f;
                case Move.Water:return 1.7f;case Move.Lightning:return 1.6f;case Move.Darkness:return 1.8f;case Move.Sword:return 1.75f;
                case Move.Daggers:return 1.7f;case Move.Shield:return 1.6f;case Move.Greatsword:return 1.9f;case Move.Bow:return 1.75f;
                case Move.Katana:return 1.85f;case Move.Glaive:return 1.7f;case Move.Fangs:return 1.7f;case Move.DevGreatsword:return 1.95f;
                case Move.DevKatana:return 1.9f;case Move.Bombadier:return 1.9f;case Move.TopDog:return 1.95f;case Move.Siren:return 1.8f;
                case Move.Emperor:return 1.75f;case Move.Ansatsuken:return 1.8f;case Move.Leet:return 1.8f;case Move.CyberLord:return 1.8f;
                default:return 1.2f;
            }
        }

        private static Cue C(float at,string key,string clip,float gain)=>new Cue{At=at,Key=key,Clip=clip,Gain=gain};
        private static Cue T(float at,string toon,float gain)=>new Cue{At=at,Toon=toon,Gain=gain};
        private static readonly Cue[] none=new Cue[0];
        private static readonly Cue[][] select=new Cue[Enum.GetValues(typeof(Move)).Length][],finish=new Cue[select.Length][];
        static NativeWeaponFlourishPlan()
        {
            void S(Move m,params Cue[] c)=>select[(int)m]=c;void F(Move m,params Cue[] c)=>finish[(int)m]=c;
            S(Move.Pistol,C(.40f,"gun.pistol.shot","pistol",.15f),C(.70f,"gun.pistol.shot","pistol",.14f),C(1f,"gun.pistol.shot","pistol",.16f),C(1.62f,"gun.reload.end","reload-end",.2f));
            S(Move.Rifle,C(.3f,"gun.ads",null,.18f),C(.92f,"gun.rifle.shot","rifle",.19f),C(1.3f,"gun.reload.end","reload-end",.18f));
            S(Move.MachineGun,C(.3f,"gun.machinegun.shot","machinegun",.1f),C(.44f,"gun.machinegun.shot","machinegun",.1f),C(.58f,"gun.machinegun.shot","machinegun",.1f),
                C(.72f,"gun.machinegun.shot","machinegun",.1f),C(.86f,"gun.machinegun.shot","machinegun",.1f),C(1f,"gun.machinegun.shot","machinegun",.1f),C(1.14f,"gun.machinegun.shot","machinegun",.1f),C(1.7f,"gun.reload.end","reload-end",.16f));
            S(Move.Fire,C(.06f,"magic.fire.shot","fire",.1f),C(.72f,"magic.fire.shot","fire",.15f),C(1.02f,"magic.fire.hit","fire",.16f));
            S(Move.Water,C(.06f,"magic.water.shot","water",.13f),C(.5f,"magic.water.hit","water",.16f));
            S(Move.Lightning,C(.25f,"magic.lightning.shot","lightning",.15f),C(.45f,"magic.lightning.hit",null,.1f),C(.65f,"magic.lightning.hit",null,.1f));
            S(Move.Darkness,C(.18f,"magic.darkness.shot","darkness",.15f),C(1.12f,"magic.darkness.hit","darkness",.17f));
            S(Move.Sword,C(.38f,"melee.sword.swing","sword",.13f),C(.62f,"melee.sword.swing","sword",.13f),C(.88f,"melee.sword.swing","sword",.14f),C(1.62f,"melee.block","parry",.12f));
            S(Move.Daggers,C(.22f,"melee.daggers.swing","daggers",.11f),C(.5f,"melee.daggers.swing","daggers",.11f),C(.78f,"melee.daggers.swing","daggers",.11f),
                C(1.02f,"melee.daggers.throw","daggers",.13f),C(1.14f,"melee.daggers.hit","wood",.14f),C(1.46f,"melee.daggers.swing",null,.09f));
            S(Move.Shield,C(.45f,"melee.shield.bash","shield",.2f),C(.95f,"melee.shield.block","parry",.13f));
            S(Move.Greatsword,C(.52f,"melee.greatsword.swing","greatsword",.16f),C(.66f,"melee.greatsword.hit","stone",.21f),C(1.4f,"melee.greatsword.swing",null,.1f));
            S(Move.Bow,C(.75f,"gun.flyby","dash",.13f),C(.97f,"impact.wood","wood",.18f));
            S(Move.Katana,C(.56f,"melee.sword.swing","sword",.15f),C(.6f,"topdog.blade-slice","saw-cut",.11f),C(1.32f,"topdog.blade-sheathe","reload-end",.18f));
            S(Move.Glaive,C(.2f,"topdog.ring-throw","dash",.14f),C(1.24f,"topdog.ring-catch","parry",.15f));
            S(Move.Fangs,C(.28f,"topdog.fang-cut","daggers",.13f),C(.38f,"topdog.fang-cut","daggers",.13f),C(.55f,"topdog.fang-hiss","poison-tick",.1f));
            S(Move.DevGreatsword,C(.36f,"melee.greatsword.swing","greatsword",.15f),C(.5f,"melee.greatsword.hit","stone",.21f),C(.52f,"blast.quake",null,.09f),C(1.5f,"dev.leet.glitch",null,.11f));
            S(Move.DevKatana,C(.32f,"melee.sword.swing","sword",.1f),C(.46f,"melee.sword.swing","sword",.1f),C(.6f,"melee.sword.swing","sword",.1f),C(.74f,"melee.sword.swing","sword",.1f),
                C(.9f,"topdog.blade-cut","saw-cut",.14f),C(1.55f,"topdog.blade-sheathe","reload-end",.18f));
            S(Move.Bombadier,C(.5f,"chip.bomb.fuse-beep",null,.1f),C(.7f,"chip.bomb.fuse-beep",null,.1f),C(.84f,"chip.bomb.fuse-beep",null,.11f),C(.94f,"chip.bomb.fuse-beep",null,.12f),C(1f,"blast.fireworks","explosion",.19f));
            S(Move.TopDog,T(.02f,"boing",.17f),T(.56f,"boing",.13f),T(.84f,"boing",.13f),T(1.35f,"bonk",.2f),T(1.52f,"tweet",.12f));
            S(Move.Siren,C(.34f,"siren.claw","daggers",.15f),C(.62f,"siren.hellfire","fire",.14f));
            S(Move.Emperor,C(.25f,"magic.lightning.shot","lightning",.13f),C(.55f,"dev.fist.hit","arcade-punch",.15f),C(.75f,"dev.fist.hit","arcade-punch",.15f),C(.95f,"dev.fist.swing","electric-hook",.14f));
            S(Move.Ansatsuken,C(.5f,"dev.fist.swing","arcade-punch",.14f),C(.85f,"blast.kinetic","arcade-heavy",.14f),C(.92f,"dev.fist.swing","arcade-kick",.12f));
            S(Move.Leet,C(.02f,"dev.leet.glitch",null,.13f),C(.98f,"dev.leet.glitch",null,.1f));
            S(Move.CyberLord,C(.2f,"chip.teleport","dash",.12f),C(.45f,"chip.teleport","dash",.1f),C(.72f,"melee.shield.block","shield",.1f),C(1.05f,"blast.shock","electric-hook-hit",.12f));
            S(Move.Generic);
            F(Move.Pistol,C(.02f,"gun.reload.end","reload-end",.18f));F(Move.Rifle,C(.02f,"gun.reload.end","reload-end",.18f));F(Move.MachineGun,C(.02f,"gun.reload.end","reload-end",.18f));
            F(Move.Fire,C(.02f,"magic.fire.hit","fire",.14f));F(Move.Water,C(.02f,"magic.water.hit","water",.14f));F(Move.Lightning,C(.02f,"magic.lightning.hit","lightning",.13f));
            F(Move.Darkness,C(.02f,"magic.darkness.hit","darkness",.14f));F(Move.Sword,C(.02f,"melee.sword.hit","sword",.14f));F(Move.Daggers,C(.02f,"melee.daggers.hit","daggers",.14f));
            F(Move.Shield,C(.02f,"melee.shield.block","shield",.14f));F(Move.Greatsword,C(.02f,"melee.greatsword.hit","greatsword",.16f));F(Move.Bow,C(.02f,"impact.wood","wood",.14f));
            F(Move.Katana,C(.02f,"topdog.blade-sheathe","reload-end",.16f));F(Move.Glaive,C(.02f,"topdog.ring-catch","parry",.14f));F(Move.Fangs,C(.02f,"topdog.fang-stab","daggers",.14f));
            F(Move.DevGreatsword,C(.02f,"melee.greatsword.hit","greatsword",.16f));F(Move.DevKatana,C(.02f,"topdog.blade-sheathe","reload-end",.16f));F(Move.Bombadier,C(.02f,"blast.fireworks","explosion",.14f));
            F(Move.TopDog,T(.02f,"bonk",.16f));F(Move.Siren,C(.02f,"siren.claw","daggers",.14f));F(Move.Emperor,C(.02f,"dev.fist.hit","arcade-punch",.14f));
            F(Move.Ansatsuken,C(.02f,"dev.fist.hit","arcade-heavy",.14f));F(Move.Leet,C(.02f,"dev.leet.glitch",null,.12f));F(Move.CyberLord,C(.02f,"blast.shock","electric-hook-hit",.12f));
            F(Move.Generic);
        }
        /// <summary>The selection flourish's sounds, in time order (shared arrays: read only).</summary>
        public static Cue[] Cues(Move m)=>select[(int)m]??none;
        /// <summary>The lock-in finisher's sounds.</summary>
        public static Cue[] FinishCues(Move m)=>finish[(int)m]??none;

        /// <summary>
        /// Cues each sound of one flourish once. A flourish is its move and its start (the screen clock less its age); a new one
        /// opens its window one frame back (so a cue at 0 plays on its first frame) and never further, so a flourish first seen
        /// late does not fire its earlier sounds at once. The window of a frame is (From, To]: cues with From &lt; At &lt;= To are due.
        /// </summary>
        public sealed class CueClock
        {
            private Move move;private float start=float.NaN,last;
            public float From{get;private set;}
            public float To{get;private set;}
            public void Reset(){start=float.NaN;}
            public void Step(Move m,float startTime,float age,float frame)
            {
                if(float.IsNaN(start)||m!=move||Math.Abs(startTime-start)>.05f){move=m;start=startTime;last=age-Math.Max(0,Math.Min(frame,.1f))-1e-4f;}
                From=last;To=age;if(age>last)last=age;
            }
            public bool Due(in Cue c)=>c.At>From&&c.At<=To;
        }
    }
}
