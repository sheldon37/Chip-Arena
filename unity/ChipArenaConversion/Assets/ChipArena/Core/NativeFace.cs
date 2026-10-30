using System;

namespace ChipArena.UnityConversion
{
    // Faces on the player's model (owner 2026-10-30: "I think we have facial expressions available? Can we begin utizling those
    // in the game as well? For zombies as well. Characters are currently emotionless"). The pack's head carries 43 Expression_*
    // blend shapes (the head, the lashes, the brows and the teeth each their share of them: brows raised, lowered, angry and
    // sad; eyes opened wide, narrowed and closed; the mouth smiling, sad, wide, grimacing and puckered; the jaw open and aside;
    // clenched teeth, a big smile, the tongue, the vowels), and its expression clips (Angry, Devious, Evil, Happy, Sad, Smile,
    // Surprised, Blink) set them; nothing in the game drove them. A face here is moods over those shapes (Shapes, the weights
    // 0..100 as the clips use them), layered from what a body is doing this frame, eased (quick to come, slower to go) and
    // blinking on its own clock:
    // - a pilot (Pilot): in a match a set, narrowed focus; a shout through a blow, a special or an ability; gritted teeth through
    //   a block or a roll; a squint down the sights; a wince when hit (by the hit's strength), strain at low health, a daze when
    //   stunned, a grin after a kill, a breath when sprinting tired; slack and closed-eyed once dead. In the menus a smile;
    // - a zombie (Zombie): its kind's resting face (the shambler's moan, the runner's bared teeth, the spitter's slack mouth, the
    //   brute's grimace, the Hulk's rage), its jaw where its gait opens it (the voxel jaw's own opening: moans, bites, roars), a
    //   roar through a blow, a wince when hit, the spitter's mouth gathering through a spit, slack once dying.
    // Presentation only: nothing here is read by a rule, a hit volume, a timing, the AI or the network.
    public static class NativeFace
    {
        /// <summary>The shapes a face drives (the pack's names after "Expression_"; on each face mesh as "BS_mesh.Expression_" + name).</summary>
        public static readonly string[] Shapes=
        {
            "Brows_Raised_L","Brows_Raised_R","Brows_Lowered_L","Brows_Lowered_R","Brows_Angry_L","Brows_Angry_R","Brows_Sad_L","Brows_Sad_R",
            "Eyes_UpperClosed_L","Eyes_UpperClosed_R","Eyes_LowerClosed_L","Eyes_LowerClosed_R","Eyes_UpperOpen_L","Eyes_UpperOpen_R","Eyes_LowerOpen_L","Eyes_LowerOpen_R",
            "Mouth_Happy_L","Mouth_Happy_R","Mouth_Sad_L","Mouth_Sad_R","Mouth_Wide_L","Mouth_Wide_R","Mouth_Grimace_L","Mouth_Grimace_R","Mouth_Pucker_L","Mouth_Pucker_R",
            "Jaw_Open","Jaw_Left","Jaw_Right","ClentchedTeeth","BigSmile","SurpriesedMouth","TongueExtend","LipsA","LipsO",
        };
        public const string Prefix="Expression_";
        public static readonly int Count=Shapes.Length;
        private static int S(string name){int i=Array.IndexOf(Shapes,name);if(i<0)throw new ArgumentException(name);return i;}
        private static readonly int UpperL=S("Eyes_UpperClosed_L"),UpperR=S("Eyes_UpperClosed_R"),Jaw=S("Jaw_Open"),Pucker=S("Mouth_Pucker_L");
        /// <summary>A mood: the shapes' weights (a name without _L or _R sets both sides).</summary>
        private static float[] M(params (string shape,float weight)[] set)
        {
            var w=new float[Count];
            foreach(var (shape,weight) in set){int i=Array.IndexOf(Shapes,shape);if(i>=0){w[i]=weight;continue;}w[S(shape+"_L")]=weight;w[S(shape+"_R")]=weight;}
            return w;
        }
        public enum Mood{Neutral,Focus,Shout,Grit,Aim,Pain,Strain,Dazed,Grin,Smile,Happy,Smirk,Sad,Surprised,Dead,Breath,Moan,Snarl,Bare,Slack,Grimace,Rage,Roar,Spit}
        /// <summary>Each mood's weights (the pack's clips where they fit: Happy, Sad, Surprised; Smirk its Devious; the rest composed).</summary>
        public static readonly float[][] Moods=
        {
            M(),
            M(("Brows_Lowered",40),("Brows_Angry",15),("Eyes_LowerClosed",18),("Mouth_Grimace",6)),
            M(("Brows_Angry",85),("Brows_Lowered",50),("Eyes_LowerClosed",25),("Jaw_Open",55),("Mouth_Wide",45),("LipsA",30),("Mouth_Grimace",25)),
            M(("Brows_Angry",70),("Brows_Lowered",30),("Eyes_LowerClosed",35),("ClentchedTeeth",75),("Mouth_Grimace",45),("Mouth_Wide",30)),
            M(("Brows_Lowered",55),("Brows_Angry",20),("Eyes_LowerClosed",35),("Eyes_UpperClosed_L",30),("Mouth_Grimace",10)),
            M(("Brows_Sad",75),("Brows_Lowered",35),("Eyes_UpperClosed",65),("Eyes_LowerClosed",40),("Mouth_Grimace",70),("ClentchedTeeth",55),("Jaw_Open",15)),
            M(("Brows_Sad",45),("Brows_Lowered",30),("Eyes_LowerClosed",20),("Mouth_Grimace",25),("Mouth_Sad",20)),
            M(("Brows_Raised",50),("Brows_Sad",30),("Eyes_UpperClosed",45),("Jaw_Open",35),("Mouth_Sad",20),("Jaw_Left",30)),
            M(("Mouth_Happy",85),("Mouth_Wide",60),("Brows_Angry",45),("Eyes_LowerClosed",30),("BigSmile",30)),
            M(("Mouth_Happy",55),("Mouth_Wide",35),("Brows_Raised",20),("Eyes_LowerClosed",12)),
            M(("Mouth_Happy",100),("Mouth_Wide",100),("Jaw_Open",34),("Eyes_UpperOpen",60),("Eyes_LowerOpen",16),("Eyes_LowerClosed",12),("Brows_Raised",66),("Mouth_Pucker",50),("Mouth_Grimace",34),("Brows_Sad",12)),
            M(("Brows_Angry",70),("Mouth_Happy_L",71),("Mouth_Happy_R",46),("Mouth_Wide_L",100),("Mouth_Wide_R",79),("Eyes_LowerClosed",19),("Brows_Sad_R",34)),
            M(("Brows_Sad",100),("Brows_Lowered",100),("Mouth_Sad",42),("Eyes_UpperClosed",13),("Eyes_LowerClosed",10)),
            M(("Brows_Raised",100),("Brows_Sad",34),("Eyes_UpperOpen",100),("Eyes_LowerOpen",97),("Jaw_Open",46),("SurpriesedMouth",50)),
            M(("Eyes_UpperClosed",80),("Eyes_LowerClosed",30),("Jaw_Open",30),("Brows_Sad",20),("Mouth_Sad",15)),
            M(("Jaw_Open",30),("LipsO",25),("Brows_Sad",20),("Eyes_LowerClosed",15)),
            // The zombies: the shambler's moan, the snarl, the runner's bared teeth, the spitter's slack mouth, the brute's grimace,
            // the Hulk's rage, a roar through a blow, a spit's gathering pucker.
            M(("Brows_Sad",55),("Brows_Raised",20),("LipsO",45),("Mouth_Sad",40),("Eyes_UpperClosed",25)),
            M(("Brows_Angry",100),("Brows_Lowered",70),("Mouth_Grimace",80),("ClentchedTeeth",60),("Mouth_Wide",40),("Eyes_UpperOpen",30)),
            M(("Brows_Angry",90),("Brows_Lowered",40),("Mouth_Grimace",100),("Mouth_Wide",80),("ClentchedTeeth",90),("Eyes_UpperOpen",55),("Eyes_LowerOpen",40)),
            M(("Brows_Sad",40),("Brows_Raised",35),("Mouth_Sad",30),("Mouth_Wide",30),("Eyes_UpperClosed",35),("Jaw_Left",25)),
            M(("Brows_Angry",80),("Brows_Lowered",90),("Mouth_Grimace",90),("Mouth_Sad",40),("ClentchedTeeth",50),("Eyes_LowerClosed",30)),
            M(("Brows_Angry",100),("Brows_Lowered",100),("Mouth_Grimace",85),("Mouth_Wide",70),("ClentchedTeeth",80),("Eyes_UpperOpen",40),("Eyes_LowerClosed",20)),
            M(("Brows_Angry",100),("Brows_Lowered",80),("Mouth_Wide",90),("Mouth_Grimace",60),("Jaw_Open",80),("LipsA",30),("Eyes_UpperOpen",60),("Eyes_LowerOpen",50)),
            M(("Mouth_Pucker",80),("LipsO",60),("Brows_Raised",30),("Eyes_UpperOpen",40),("Jaw_Open",15)),
        };
        /// <summary>How quickly a face eases toward its moods (seconds: coming, going; the lids at most Lids), and its blink (seconds: the
        /// gap's range, the blink).</summary>
        public const float Rise=.06f,Fall=.2f,Lids=.035f,BlinkMin=2.2f,BlinkMax=5.6f,BlinkFor=.14f;

        public sealed class Face
        {
            /// <summary>The weights shown (0..100), the moods' target this frame.</summary>
            public readonly float[] Weight=new float[Count],Target=new float[Count];
            public float Time=float.NaN,NextBlink,BlinkAt=float.NegativeInfinity,GrinUntil=float.NegativeInfinity;public int Kills=-1;
            private uint seed;
            public Face(int id){seed=(uint)id*2654435761u+12345u;NextBlink=BlinkMin+Random()*(BlinkMax-BlinkMin);}
            internal float Random(){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return (seed&0xffffff)/16777216f;}
            /// <summary>A fresh start (a new body, a respawn): the next frame sets the face at once.</summary>
            public void Forget(){Time=float.NaN;}
        }
        // The target: a base mood, then each layer lerped in by its share.
        private static void Base(Face f,Mood mood,float share=1){var m=Moods[(int)mood];for(int i=0;i<Count;i++)f.Target[i]=m[i]*share;}
        private static void Layer(Face f,Mood mood,float share)
        {
            if(share<=0)return;share=Math.Min(1,share);var m=Moods[(int)mood];
            for(int i=0;i<Count;i++)f.Target[i]+=(m[i]-f.Target[i])*share;
        }

        /// <summary>What a pilot is doing this frame (times in the actor's clock; Since* how long ago, large when never).</summary>
        public struct Pilot
        {
            public float Time,Health,Stamina,SinceHit,HitStrength,SinceAttack,SinceSpecial,SinceAbility,SinceShot,SinceBlock,SinceRoll;
            public bool Arena,Alive,Hurt,Aiming,Sprinting,Stunned,Attacking;public int Kills;
        }
        /// <summary>A pilot's face this frame (see the class's notes).</summary>
        public static void PilotFace(Face f,in Pilot p)
        {
            if(!p.Alive){Base(f,Mood.Dead);Step(f,p.Time,false);return;}
            Base(f,p.Arena?Mood.Focus:Mood.Smile);
            if(f.Kills>=0&&p.Kills>f.Kills)f.GrinUntil=p.Time+1.6f;f.Kills=p.Kills;
            if(p.Arena)Layer(f,Mood.Strain,Smooth((.4f-p.Health)/.3f)*.8f);
            if(p.Sprinting)Layer(f,Mood.Breath,Smooth((60-p.Stamina)/40)*.8f);
            Layer(f,Mood.Grin,Smooth((f.GrinUntil-p.Time)/.4f));
            Layer(f,Mood.Aim,(p.Aiming?.7f:0)+Fade(p.SinceShot,.35f)*.3f);
            Layer(f,Mood.Grit,Math.Max(Fade(p.SinceBlock,.5f),Fade(p.SinceRoll,.45f))*.85f);
            Layer(f,Mood.Shout,Math.Max(p.Attacking?.9f:Fade(p.SinceAttack,.45f),Math.Max(Fade(p.SinceSpecial,.8f),Fade(p.SinceAbility,.6f))));
            Layer(f,Mood.Dazed,p.Stunned?1:0);
            Layer(f,Mood.Pain,Math.Max(Fade(p.SinceHit,.6f)*Math.Max(.5f,Math.Min(1,p.HitStrength)),p.Hurt?.6f:0));
            Step(f,p.Time,true);
        }

        /// <summary>What a zombie is doing this frame: its kind, its jaw's opening (0..1, the voxel jaw's), how much of it a blow,
        /// a reel or a roar owns (0..1), its spit's swell, whether a hit flashes it, whether it is dying.</summary>
        public struct Zombie{public float Time,Jaw,Busy,Swell;public int Kind;public bool Hurt,Dying;}
        /// <summary>A zombie's face this frame (see the class's notes).</summary>
        public static void ZombieFace(Face f,in Zombie z)
        {
            if(z.Dying){Base(f,Mood.Slack);f.Target[Jaw]=Math.Max(f.Target[Jaw],45);Step(f,z.Time,false);return;}
            int k=z.Kind;Mood rest=k==NativeHordeKinds.Shambler?Mood.Moan:k==NativeHordeKinds.Runner?Mood.Bare:k==NativeHordeKinds.Spitter?Mood.Slack:k==NativeHordeKinds.Brute?Mood.Grimace:k==NativeHordeKinds.Hulk?Mood.Rage:Mood.Snarl;
            Base(f,rest);
            // (A jaw the gait opens snarls wide, not round: a bite, a lunge; the shambler's moans stay round.)
            Layer(f,Mood.Roar,Math.Max(z.Busy*.9f,k==NativeHordeKinds.Shambler?0:Math.Min(1,Math.Max(0,z.Jaw))*.75f));
            Layer(f,Mood.Spit,Math.Min(1,z.Swell)*.9f);
            Layer(f,Mood.Pain,z.Hurt?.7f:0);
            // The jaw where the gait opens it (moans, bites, a roar), at least the mood's; a spit's pucker closes it.
            f.Target[Jaw]=Math.Max(f.Target[Jaw],Math.Min(1,Math.Max(0,z.Jaw))*100*(1-.7f*Math.Min(1,f.Target[Pucker]/80)));
            Step(f,z.Time,k!=NativeHordeKinds.Spitter||z.Swell<.3f);
        }

        // Toward the target (quick to come, slower to go) by the time since the last frame, and the blink on its own clock (a
        // closed-eyed mood blinks no further).
        private static void Step(Face f,float time,bool blinks)
        {
            float dt=float.IsNaN(f.Time)?-1:Math.Max(0,Math.Min(.25f,time-f.Time));if(float.IsNaN(f.Time)||time<f.Time-1)f.NextBlink=time+BlinkMin+f.Random()*(BlinkMax-BlinkMin);f.Time=time;
            if(blinks&&time>=f.NextBlink){f.BlinkAt=time;f.NextBlink=time+BlinkMin+f.Random()*(BlinkMax-BlinkMin);if(f.Random()<.15f)f.NextBlink=time+BlinkFor*2.2f;}
            float blink=time-f.BlinkAt<BlinkFor?(float)Math.Sin(Math.PI*(time-f.BlinkAt)/BlinkFor)*100:0;
            for(int i=0;i<Count;i++)
            {
                float target=f.Target[i];if(blink>0&&(i==UpperL||i==UpperR))target=Math.Max(target,blink);
                if(dt<0){f.Weight[i]=target;continue;}
                // (The lids snap through a blink and open again as quickly.)
                float tau=target>f.Weight[i]?Rise:Fall;if(i==UpperL||i==UpperR)tau=blink>0?.012f:Math.Min(tau,Lids);
                f.Weight[i]+=(target-f.Weight[i])*(1-(float)Math.Exp(-dt/tau));
            }
        }
        private static float Fade(float since,float span)=>since<0||since>=span?0:1-since/span;
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
    }
}
