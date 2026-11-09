using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>The Top Dog's sound (docs/TOP-DOG.md "Sound"): which toon sound (Core NativeToonSynth, or the owner's clip under its key) each hit,
    /// cast and release plays, and the entrance's timeline. Pure.</summary>
    public static class NativeTopDogSounds
    {
        private static int S((string id,float gain)[] o,int n,string id,float gain){if(n<o.Length)o[n]=(id,gain);return Math.Min(o.Length,n+1);}
        public static int Hit(string kind,(string id,float gain)[] o)
        {
            int n=0;
            switch(kind)
            {
                case NativeTopDog.HitBonk:n=S(o,n,"bonk",1);break;
                case NativeTopDog.HitSlam:n=S(o,n,"bonk",1);n=S(o,n,"squeak",.6f);break;
                case NativeTopDog.HitClang:n=S(o,n,"slam",1);break;
                case NativeTopDog.HitThud:n=S(o,n,"slam",.65f);break;
                case NativeTopDog.HitSwat:n=S(o,n,"punch",.75f);n=S(o,n,"squeak",.5f);break;
                case NativeTopDog.HitAnvil:n=S(o,n,"clang",1);n=S(o,n,"slam",.55f);break;
                case NativeTopDog.HitSlip:n=S(o,n,"slip",1);n=S(o,n,"tweet",.45f);break;
                case NativeTopDog.HitPow:n=S(o,n,"punch",1);n=S(o,n,"tweet",.5f);break;
                case NativeTopDog.HitPie:n=S(o,n,"pie",1);break;
                case NativeTopDog.HitGulp:n=S(o,n,"gulp",.95f);n=S(o,n,"chomp",.5f);break;
                case NativeTopDog.HitPop:n=S(o,n,"pop",1);n=S(o,n,"splat",.8f);n=S(o,n,"crash",.45f);break;
                case NativeTopDog.HitSnap:n=S(o,n,"sproing",.7f);n=S(o,n,"burp",.6f);break;
                case NativeTopDog.HitBoing:n=S(o,n,"boing",1);break;
                case NativeTopDog.Zip:n=S(o,n,"zip",1);break;
                case NativeTopDog.Feast:n=S(o,n,"slide-up",.6f);break;
            }
            return n;
        }
        /// <summary>What a cast plays as it starts.</summary>
        public static int Cast(string kind,int step,(string id,float gain)[] o)
        {
            int n=0;
            switch(kind)
            {
                case NativeTopDog.Zip:n=S(o,n,"windup",.85f);break;
                case NativeTopDog.Anvil:n=S(o,n,"honk",.6f);break;
                case NativeTopDog.Mallet:if(step==2)n=S(o,n,"stretch",.4f);break;
            }
            return n;
        }
        /// <summary>What a cast plays as it leaves the hand or lands.</summary>
        public static int Release(string kind,int step,(string id,float gain)[] o)
        {
            int n=0;
            switch(kind)
            {
                case NativeTopDog.Mallet:n=S(o,n,"swing",step==2?1:.8f);break;
                case NativeTopDog.Peel:n=S(o,n,"slide-up",.5f);break;
                case NativeTopDog.Pie:n=S(o,n,"swing",.6f);break;
            }
            return n;
        }
        /// <summary>A small pitch variation per hit, so a run of the same one is not a machine gun.</summary>
        public static float Rate(int id,int i){uint h=unchecked((uint)id*2654435761u+(uint)i*40503u);h^=h>>15;return .94f+.12f*((h&1023)/1023f);}

        public struct IntroCue{public float At,Gain,Rate;public string Id;public IntroCue(float at,string id,float gain,float rate=1){At=at;Id=id;Gain=gain;Rate=rate;}}
        /// <summary>The entrance (NativeTopDogIntro), in time order.</summary>
        public static readonly IntroCue[] Intro=BuildIntro();
        private static IntroCue[] BuildIntro()
        {
            var l=new List<IntroCue>
            {
                new IntroCue(NativeTopDogIntro.IrisOpen,"slide-up",.7f),new IntroCue(.25f,"sizzle",.8f),new IntroCue(NativeTopDogIntro.BellAt,"bell",1),
                new IntroCue(NativeTopDogIntro.DrumAt,"drumroll",.8f),new IntroCue(NativeTopDogIntro.LidAt,"pop",.8f),new IntroCue(NativeTopDogIntro.LidAt+.02f,"poof",.7f),
                new IntroCue(NativeTopDogIntro.SpringAt,"boing",1),new IntroCue(NativeTopDogIntro.SpringAt+.1f,"slide-up",.8f,1.1f),new IntroCue(NativeTopDogIntro.LandAt,"slam",.8f),new IntroCue(NativeTopDogIntro.LandAt+.04f,"squeak",.6f),
                new IntroCue(NativeTopDogIntro.MalletAt,"stretch",.6f),new IntroCue(NativeTopDogIntro.MalletAt+.3f,"honk",.7f),new IntroCue(NativeTopDogIntro.SlamAt-.12f,"swing",.9f),new IntroCue(NativeTopDogIntro.SlamAt,"slam",1),
                new IntroCue(NativeTopDogIntro.SlamAt,"bonk",.8f,.8f),new IntroCue(NativeTopDogIntro.SlamAt+.15f,"tweet",.5f),new IntroCue(NativeTopDogIntro.CardAt,"fanfare",1),new IntroCue(NativeTopDogIntro.CardAt+.02f,"crash",.5f),
                new IntroCue(NativeTopDogIntro.WinkAt,"squeak",.7f,1.2f),new IntroCue(NativeTopDogIntro.IrisClose,"slide-down",.7f),
            };
            l.Sort((a,b)=>a.At.CompareTo(b.At));return l.ToArray();
        }
    }
}
