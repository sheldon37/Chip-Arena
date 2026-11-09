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
                case NativeTopDog.HitPiano:n=S(o,n,"piano",1);n=S(o,n,"crash",.5f);break;
                case NativeTopDog.HitWall:n=S(o,n,"splat",1);n=S(o,n,"slam",.6f);n=S(o,n,"tweet",.45f);break;
                case NativeTopDog.HitMunch:n=S(o,n,"chomp",1);n=S(o,n,"gulp",.6f);break;
                case NativeTopDog.HitToonFlat:n=S(o,n,"slam",1);n=S(o,n,"splat",.7f);break;
                case NativeTopDog.HitToonPop:n=S(o,n,"pop",.8f);n=S(o,n,"boing",1);n=S(o,n,"tweet",.5f);break;
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

        /// <summary>Each chip's pick (Runtime NativeDogChipArt.Reveal), on its gag's clock (s after it starts), in time order.</summary>
        public static readonly IntroCue[][] Picks=BuildPicks();
        private static IntroCue[][] BuildPicks()
        {
            IntroCue C(float at,string id,float gain,float rate=1)=>new IntroCue(at,id,gain,rate);
            var l=new[]{
                // Bigger Mallet: the swing, the BONK, the stars, the mallet growing as it lifts away.
                new[]{C(.1f,"swing",.9f,.8f),C(.42f,"bonk",1,.8f),C(.43f,"slam",.7f),C(.55f,"tweet",.6f),C(.95f,"stretch",.7f,.8f)},
                // Hammerspace: the hole, the hand rummaging, a honk out, the piano up and down, the chord crashing.
                new[]{C(0,"poof",.6f),C(.25f,"honk",.6f),C(.6f,"clang",.4f,1.3f),C(.95f,"slide-up",.7f),C(1.2f,"whistle-fall",.8f,1.4f),C(1.55f,"piano",1),C(1.56f,"crash",.6f)},
                // Anvil Shower: three falls, three clangs.
                new[]{C(0,"whistle-fall",.6f,1.6f),C(.45f,"clang",.9f),C(.75f,"clang",.8f,1.12f),C(1.05f,"clang",.9f,.9f),C(1.25f,"tweet",.5f)},
                // Banana Bonanza: peels landing, the slip, the slide whistle down, a squeak up.
                new[]{C(.05f,"splat",.35f,1.6f),C(.35f,"slip",1),C(.4f,"slide-down",.8f),C(.62f,"slam",.5f),C(1.1f,"slide-up",.6f,1.2f)},
                // Hot Sauce: the squirt, the sizzle, the steam whistle.
                new[]{C(.2f,"squirt",.8f),C(.3f,"sizzle",.9f),C(.9f,"honk",.5f,1.4f)},
                // Pie Volley: three splats, the squeegee.
                new[]{C(.3f,"pie",1),C(.52f,"pie",.9f,1.1f),C(.74f,"pie",1,.9f),C(1.5f,"squeak",.6f),C(1.7f,"squeak",.5f,1.2f)},
                // Knockout Glove: the sproing, the POW, the tweets.
                new[]{C(.05f,"sproing",.9f),C(.4f,"punch",1),C(.42f,"bonk",.6f,.7f),C(.6f,"tweet",.6f),C(1.2f,"wobble",.6f)},
                // Double Boing: the spring winds, BOING, the fall, boing, boing.
                new[]{C(0,"windup",.6f),C(.25f,"boing",1),C(.3f,"slide-up",.6f),C(.7f,"slide-down",.5f),C(.95f,"boing",.9f,1.1f),C(1.3f,"boing",.7f,1.25f),C(1.5f,"boing",.5f,1.4f)},
                // Second Helpings: the bell, the cloche off in a puff, the hot dogs raining, a fanfare.
                new[]{C(.35f,"bell",1),C(.95f,"poof",.7f),C(1.05f,"fanfare",.8f),C(1.3f,"chomp",.5f),C(1.6f,"chomp",.5f,1.2f)},
                // Toon Logic: the anvil, SPLUT, the pump three times, the pop, ta-da.
                new[]{C(0,"whistle-fall",.7f,1.5f),C(.42f,"slam",1),C(.43f,"splat",.8f),C(.95f,"inflate",.8f),C(1.85f,"pop",.9f),C(1.87f,"boing",.8f),C(1.95f,"fanfare",.8f)},
            };
            foreach(var a in l)Array.Sort(a,(x,y)=>x.At.CompareTo(y.At));return l;
        }
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
