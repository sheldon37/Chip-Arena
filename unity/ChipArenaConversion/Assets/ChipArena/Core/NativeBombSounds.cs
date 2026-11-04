using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>The Bombadier's sound (docs/BOMBADIER.md), on the bank's existing cues: each blast by kind, each cast at its release, and the Last Warning's timeline, which is its only sound: no music, so the opening is silence but for the fuse.</summary>
    public static class NativeBombSounds
    {
        private static int S(NativeKitSound[] o,int n,string key,string clip,float gain){if(n<o.Length)o[n]=new NativeKitSound{Key=key,Clip=clip,Gain=gain};return Math.Min(o.Length,n+1);}
        public static int Blast(NativeBlast b,NativeKitSound[] o)
        {
            int n=0;
            switch(b.Kind)
            {
                case NativeBombs.Spark:n=S(o,n,"gun.pistol.shot","pistol",.32f);break;
                case NativeBombs.Column:n=S(o,n,"blast.fire","explosion",1);n=S(o,n,"blast.quake","stone",.9f);break;
                case NativeBombs.Mine:n=S(o,n,"blast.kinetic","dash",.9f);n=S(o,n,"blast.fire","explosion",.5f);break;
                case NativeBombs.Bomblet:n=S(o,n,"blast.fire","explosion",.4f);break;
                default:n=S(o,n,"blast.fire","explosion",.75f);break;
            }
            if(b.Link>0)n=S(o,n,"gun.ricochet","metal",.3f+.12f*Math.Min(4,b.Link));
            return n;
        }
        public static int Cast(string kind,NativeKitSound[] o)
        {
            int n=0;
            switch(kind)
            {
                case NativeBombs.PopBomb:n=S(o,n,"gun.frag.throw","dash",.7f);break;
                case NativeBombs.BlastJump:n=S(o,n,"move.jump","jump",.9f);n=S(o,n,"blast.kinetic","dash",.6f);break;
                case NativeBombs.DeadMansSwitch:n=S(o,n,"gun.dryfire","metal",.9f);break;
                case NativeBombs.BounceMine:n=S(o,n,"melee.daggers.throw","daggers",.6f);break;
                case NativeBombs.StickyFuse:n=S(o,n,"melee.daggers.throw","daggers",.7f);break;
                case NativeBombs.ClusterLob:n=S(o,n,"melee.greatsword.swing","greatsword",.6f);break;
                case NativeBombs.FirecrackerLine:n=S(o,n,"blast.static","lightning",.4f);break;
                case NativeBombs.GrandFinale:n=S(o,n,"impact.metal","metal",.9f);break;
            }
            return n;
        }
        public struct IntroCue{public float At,Gain;public string Key,Clip;public IntroCue(float at,string key,string clip,float gain){At=at;Key=key;Clip=clip;Gain=gain;}}
        /// <summary>The Last Warning, in time order: the fuse, the ticks speeding up, the stamp, the thumb, the silence, and then all of it.</summary>
        public static readonly IntroCue[] Intro=BuildIntro();
        private static IntroCue[] BuildIntro()
        {
            var l=new List<IntroCue>{new IntroCue(.18f,"blast.static","lightning",.28f),new IntroCue(.95f,"blast.static","lightning",.2f),new IntroCue(1.7f,"blast.static","lightning",.2f)};
            // The ticks: every beat of NativeBombadierIntro.Tick.
            float prev=-1;for(float t=1.05f;t<NativeBombadierIntro.Click;t+=.004f){float u=(t-1.05f)/(NativeBombadierIntro.Click-1.05f),phase=3*u+9*u*u;float beat=(float)Math.Floor(phase);if(beat!=prev){prev=beat;l.Add(new IntroCue(t,"ui.countdown","ui",.4f+.5f*u));}}
            l.Add(new IntroCue(NativeBombadierIntro.Stomp,"move.land","explosion",.8f));
            l.Add(new IntroCue(NativeBombadierIntro.Click,"gun.dryfire","metal",1));
            for(int i=0;i<12;i++)l.Add(new IntroCue(NativeBombadierIntro.FirstBlast+i*NativeBombadierIntro.BlastStep,"blast.fire","explosion",.6f+.4f*i/11f));
            l.Add(new IntroCue(NativeBombadierIntro.FirstBlast,"blast.quake","stone",.8f));l.Add(new IntroCue(NativeBombadierIntro.ShockAt,"blast.quake","stone",1));l.Add(new IntroCue(NativeBombadierIntro.ShockAt,"blast.kinetic","dash",1));
            l.Add(new IntroCue(NativeBombadierIntro.CardAt,"melee.daggers.throw","daggers",.7f));l.Add(new IntroCue(NativeBombadierIntro.CardAt+.12f,"impact.metal","metal",.8f));l.Add(new IntroCue(NativeBombadierIntro.CatchAt,"melee.shield.block","parry",.25f));
            l.Sort((a,b)=>a.At.CompareTo(b.At));return l.ToArray();
        }
    }
}
