using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Flashbulb" (docs/INTROS-GENERIC.md "Flashbulb"). 7.8 s, the name at 6 s. A premiere at night: a red carpet runs from a theatre's
    /// doors between velvet ropes, its marquee's bulbs chasing round it, searchlights sweeping the sky. The doors swing open and light
    /// spills down the carpet; the pilot strides out and down it as the photographers' flashes pop from both sides. On the mark it gives
    /// three poses, each held for its photograph (a look back over the shoulder, a hand on the hip, a two-finger salute off the brow);
    /// a storm of flashes and confetti; the cover shot under the name. Camera: a low dolly back ahead of the walk, three photographs
    /// from three angles (each a held frame lit by its flash), the cover.
    /// </summary>
    public sealed class NativeIntroFlashbulb:NativeIntroGenericScript
    {
        public const string Id=NativeIntroCatalog.Prefix+"flashbulb";
        public const float Total=7.8f,CardAt=6.0f;
        public const float BulbsAt=.05f,DoorsAt=.5f,CutAt=.8f,WalkAt=.82f,MarkAt=3.15f,StormAt=5.25f,CoverAt=5.65f;
        // The strut (the model's feet, the world's frame, straight down +z): from the doorway shot the edit cuts (CutAt) to the pilot
        // half way down the carpet already striding (OnCarpet), a catwalk strut of about .68 m steps every .433 s, the feet crossing
        // onto one line under swinging hips, slowing onto the mark.
        public const float OnCarpet=-3.3f,Cadence=.433f,Swing=.35f;
        public const int Paces=5;
        private static readonly Track Path=new Track(CutAt-.45f,MarkAt+.1f,OnCarpet-.5f*1.571f*.45f,0,.45f,.7f);
        /// <summary>When strut step `i` lands (0: the right foot's first after the cut).</summary>
        public static float Footfall(int i)=>WalkAt+i*Cadence+Swing;
        public static readonly float[] Photos={3.32f,3.95f,4.58f};
        public const float Hold=.38f;
        public override string Title=>"Flashbulb";
        // The set (Unity space, generic_flashbulb.py): the theatre's front, the swinging doors' hinges, the marquee.
        public const float FrontZ=-6.6f;
        private static readonly Vector3 Marquee=U(0,3.3f,-5.2f),Foyer=U(0,1.5f,-7.0f);
        private static readonly Vector3 Warm=NativeEntranceFx.Hex(0xffd59a),Bulb=NativeEntranceFx.Hex(0xfff0c8),Flashc=NativeEntranceFx.Hex(0xf4f8ff),Pink=NativeEntranceFx.Hex(0xff5aa8),
            Cyan=NativeEntranceFx.Hex(0x6fe0ff),Carpet=NativeEntranceFx.Hex(0xb3122b),Gold=NativeEntranceFx.Hex(0xffc85a);
        private static readonly Vector3[] Confetti={NativeEntranceFx.Hex(0xffd23a),NativeEntranceFx.Hex(0xff4f8a),NativeEntranceFx.Hex(0x5ad0ff),NativeEntranceFx.Hex(0xffffff),NativeEntranceFx.Hex(0xb06aff)};
        // The photographers' flashes along both barriers through the walk and the storm (times, places): fixed, so every peer agrees.
        private static readonly float[] FlashT;private static readonly Vector3[] FlashP;
        static NativeIntroFlashbulb()
        {
            var t=new System.Collections.Generic.List<float>();var p=new System.Collections.Generic.List<Vector3>();
            float at=1.0f;int i=0;
            while(at<StormAt+.75f)
            {
                bool storm=at>=StormAt;float gap=storm?.07f+.06f*NativeEntranceMath.Rand(25000,i):.22f+.2f*NativeEntranceMath.Rand(25000,i);
                bool inPhoto=false;foreach(var ph in Photos)if(at>ph-.1f&&at<ph+Hold+.05f)inPhoto=true;
                if(!inPhoto&&!(at>MarkAt-.05f&&at<Photos[0]-.1f))
                {
                    float side=NativeEntranceMath.Rand(25001,i)<.5f?-1:1;t.Add(at);
                    p.Add(new Vector3(side*(3.75f+.5f*NativeEntranceMath.Rand(25002,i)),1.55f+.7f*NativeEntranceMath.Rand(25003,i),-4.2f+7.8f*NativeEntranceMath.Rand(25004,i)));
                }
                at+=gap;i++;
            }
            FlashT=t.ToArray();FlashP=p.ToArray();cues=BuildCues();
        }
        public NativeIntroFlashbulb():base(Id,Total,CardAt,Photos[0],0xff5aa8)
        {
            // In the doorway (the model's feet, the world's frame); on the cut both feet put down mid-stride half way down the carpet, the
            // strut, then onto the mark and through the three poses, the feet stepping and pivoting with the body each time (never
            // both off the floor), and the cover's wide stance.
            Start=Stance(.1f,-6.33f,.12f,-.11f,-6.5f,-.14f,0);
            var steps=new System.Collections.Generic.List<NativeEntrancePose.Step>();
            steps.Add(new NativeEntrancePose.Step(true,CutAt-.0005f,CutAt,.025f,Path.At(WalkAt+Swing*.5f),.14f,0));
            steps.Add(new NativeEntrancePose.Step(false,CutAt-.0005f,CutAt,-.025f,Path.At(WalkAt-Cadence*.62f),-.14f,0));
            Walk(steps,Path,false,Paces,WalkAt,Cadence,Swing,.025f,.14f,.1f);
            // (pose one turned away to its right, the lens at its left: weight on the right foot, the left crossed in front; pose two
            // square to the lens on its right; pose three square to the low lens; the cover square to the closing camera, wide)
            float y1=-.8f,y2=-.45f,y3=-.05f;var one=Stance(.04f,.15f,.3f,-.09f,-.05f,-.25f,y1);var two=Stance(.07f,.11f,.35f,-.13f,-.03f,-.2f,y2);
            var three=Stance(.16f,.04f,.16f,-.16f,-.05f,-.3f,y3);var cover=Stance(.19f,.03f,.18f,-.19f,-.04f,-.34f,Facing);
            steps.Add(new NativeEntrancePose.Step(false,Footfall(Paces-1)+.06f,Photos[0]-.06f,one[3],one[4],one[5],.06f));
            steps.Add(new NativeEntrancePose.Step(true,Photos[0]-.1f,Photos[0]+.02f,one[0],one[1],one[2],.04f));
            steps.Add(new NativeEntrancePose.Step(true,Photos[1]-.32f,Photos[1]-.17f,two[0],two[1],two[2],.04f));
            steps.Add(new NativeEntrancePose.Step(false,Photos[1]-.2f,Photos[1]-.04f,two[3],two[4],two[5],.04f));
            steps.Add(new NativeEntrancePose.Step(false,Photos[2]-.3f,Photos[2]-.16f,three[3],three[4],three[5],.04f));
            steps.Add(new NativeEntrancePose.Step(true,Photos[2]-.18f,Photos[2]-.04f,three[0],three[1],three[2],.04f));
            steps.Add(new NativeEntrancePose.Step(true,CoverAt+.02f,CoverAt+.2f,cover[0],cover[1],cover[2],.05f));
            steps.Add(new NativeEntrancePose.Step(false,CoverAt+.12f,CoverAt+.3f,cover[3],cover[4],cover[5],.05f));
            Steps=steps.ToArray();
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // The silhouette in the doorway: weight back on one hip; as the doors swing open it rolls its shoulders back, lifts its chin
            // and leans into its first step.
            k.Key(0,stand).Root(0,.88f,0,0).Planted(1).J(P,0,.06f,-.04f).J(C,0,-.08f,.03f).J(Hd,-.04f,.05f,0).J(LA,-.04f,0,.08f).J(LE,-.3f).J(RA,-.06f,0,-.08f).J(RE,-.28f);
            k.Key(DoorsAt,stand).Root(0,.88f,0,0).Planted(1).J(P,0,.06f,-.04f).J(C,.04f,-.08f,.03f).J(Hd,-.02f,.05f,0).J(LA,-.04f,0,.08f).J(LE,-.32f).J(RA,-.06f,0,-.08f).J(RE,-.3f);
            k.Key(DoorsAt+.18f,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(.01f)).J(C,-.1f).J(Hd,-.16f).J(LA,.06f,0,.1f).J(LE,-.26f).J(RA,.06f,0,-.1f).J(RE,-.26f);
            k.Key(CutAt-.01f,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(-.03f)).J(B,.06f).J(C,-.08f).J(Hd,-.12f).J(LA,-.2f,0,.1f).J(LE,-.4f).J(RA,.18f,0,-.1f).J(RE,-.25f);
            // The strut, two keys a step: at each footfall the hips are lowest, turned well round with the leg landing in front (the
            // swagger), the shoulders back and countering, chin up, the arm opposite that leg swinging forward with a loose elbow, the
            // other back; at the passing the hips ride highest and drop hard on the swinging side over the stance leg (and swing out
            // over it: RootPath), the arms crossing loosely. (s: +1 when the left foot lands; the cut lands mid-stride on the left.)
            for(int i=-1;i<Paces;i++)
            {
                float land=i<0?CutAt:Footfall(i),pass=(i<0?Footfall(0)-Swing:land)+Cadence*.5f;float s=i<0||i%2==1?1:-1;
                if(i>=0)k.Key(land,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(-.085f)).J(B,.015f)
                    .J(P,0,-s*.17f,-s*.02f).J(Sp,0,s*.06f,0).J(C,-.09f,s*.12f,s*.03f).J(Hd,-.13f,-s*.06f,-s*.02f)
                    .J(LA,s>0?.3f:-.32f,0,s>0?.1f:.04f).J(LE,s>0?-.18f:-.5f).J(RA,s>0?-.32f:.3f,0,s>0?-.04f:-.1f).J(RE,s>0?-.5f:-.18f).PassKey();
                k.Key(pass,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(-.012f)).J(B,.01f)
                    .J(P,0,0,s*.1f).J(Sp,0,0,-s*.03f).J(C,-.09f,0,-s*.03f).J(Hd,-.14f,0,s*.03f)
                    .J(LA,-s*.03f,0,.09f).J(LE,-.34f).J(RA,s*.03f,0,-.09f).J(RE,-.34f).PassKey();
            }
            // Each pose is hit on its flash (an impact key: it snaps in and rings out) out of a little sink and unwind.
            // Pose one: turned away to its right, weight on the right leg, the left knee in; the shoulders turned back toward the lens and
            // the head over the left shoulder to it, chin down a touch; the right hand on the hip behind, the left loose.
            k.Key(Photos[0]-.14f,stand).Root(0,.88f,0,y1+.15f).Planted(1).Drop(Sink(-.06f)).J(P,0,0,-.04f).J(C,-.04f,.15f,0).J(Hd,-.04f,.35f,0).J(LA,-.15f,0,.1f).J(LE,-.4f).J(RA,.15f,0,-.3f).J(RE,-.9f).PassKey();
            k.Key(Photos[0],stand).Root(0,.88f,0,y1).Planted(1).Drop(Sink(-.035f)).J(P,0,0,-.07f).J(Sp,0,.08f,0).J(C,-.05f,.28f,.04f).J(Hd,.06f,.82f,.1f)
                .J(LA,-.06f,0,.06f).J(LE,-.32f).J(RA,.94f,.72f,-.42f).J(RE,-1.56f).ImpactKey();
            k.Key(Photos[0]+Hold,stand).Root(0,.88f,0,y1).Planted(1).Drop(Sink(-.04f)).J(P,0,0,-.07f).J(Sp,0,.08f,0).J(C,-.05f,.3f,.04f).J(Hd,.07f,.85f,.1f)
                .J(LA,-.06f,0,.06f).J(LE,-.32f).J(RA,.94f,.72f,-.42f).J(RE,-1.56f);
            k.Key((Photos[0]+Hold+Photos[1]-.12f)/2,stand).Root(0,.88f,0,(y1+y2)/2).Planted(1).Drop(Sink(-.07f)).J(C,-.03f,.1f,0).J(Hd,-.04f,.2f,0).J(LA,-.1f,0,.12f).J(LE,-.45f).J(RA,0,0,-.2f).J(RE,-.6f).PassKey();
            // Pose two: square to the lens, a hand on the hip, the hip popped over the straight right leg (the shoulders tilting back
            // the other way), chin up, the head tilted.
            k.Key(Photos[1],stand).Root(0,.88f,0,y2).Planted(1).Drop(Sink(-.03f)).J(P,0,.05f,.12f).J(Sp,0,0,-.05f).J(C,-.06f,-.05f,-.08f).J(Hd,-.16f,.05f,.14f)
                .J(RA,1.32f,1.29f,-1.3f).J(RE,-1.49f).J(LA,-.08f,0,.08f).J(LE,-.3f).ImpactKey();
            k.Key(Photos[1]+Hold,stand).Root(0,.88f,0,y2).Planted(1).Drop(Sink(-.035f)).J(P,0,.05f,.13f).J(Sp,0,0,-.05f).J(C,-.06f,-.05f,-.09f).J(Hd,-.17f,.06f,.15f)
                .J(RA,1.32f,1.29f,-1.3f).J(RE,-1.49f).J(LA,-.08f,0,.08f).J(LE,-.3f);
            k.Key((Photos[1]+Hold+Photos[2]-.12f)/2,stand).Root(0,.88f,0,(y2+y3)/2).Planted(1).Drop(Sink(-.07f)).J(C,-.03f).J(Hd,-.06f,.04f,0).J(RA,-.6f,0,-.3f).J(RE,-1.0f).J(LA,-.1f,0,.12f).J(LE,-.35f).PassKey();
            // Pose three: square to the low lens, two fingers to the brow, then flicked out wide.
            k.Key(Photos[2],stand).Root(0,.88f,0,y3).Planted(1).Drop(Sink(-.03f)).J(C,-.06f,0,.02f).J(Hd,-.08f,.04f,-.06f).J(RA,-1.34f,.68f,-1.1f).J(RE,-2.05f).J(LA,-.08f,0,.08f).J(LE,-.3f).ImpactKey();
            k.Key(Photos[2]+Hold-.06f,stand).Root(0,.88f,0,y3).Planted(1).Drop(Sink(-.03f)).J(C,-.06f,0,.02f).J(Hd,-.08f,.04f,-.06f).J(RA,-1.36f,.68f,-1.1f).J(RE,-2.07f).J(LA,-.08f,0,.08f).J(LE,-.3f);
            k.Key(Photos[2]+Hold+.12f,stand).Root(0,.88f,0,y3).Planted(1).Drop(Sink(-.05f)).J(C,-.08f,-.05f,0).J(Hd,-.12f,-.05f,0).J(RA,-2.26f,-.3f,-.94f).J(RE,-.19f).J(LA,-.08f,0,.1f).J(LE,-.3f).ImpactKey();
            // The storm: a dip, then both arms thrown up into a wide V, chest up, chin up, basking in it; then down to the hips for the
            // cover (hands on the hips, elbows out, chin up, the feet wide).
            k.Key(StormAt-.08f,stand).Root(0,.88f,0,y3).Planted(1).Drop(Sink(-.08f)).J(C,.03f).J(Hd,.02f).J(RA,-.4f,0,-.6f).J(RE,-.9f).J(LA,-.4f,0,.6f).J(LE,-.9f);
            k.Key(StormAt+.1f,stand).Root(0,.88f,0,y3).Planted(1).Drop(Sink(.01f)).J(C,-.16f).J(Hd,-.3f).J(RA,-.25f,0,-2.3f).J(RE,-.15f).J(LA,-.25f,0,2.3f).J(LE,-.15f).ImpactKey();
            k.Key(StormAt+.36f,stand).Root(0,.88f,0,y3).Planted(1).Drop(Sink(-.005f)).J(C,-.15f).J(Hd,-.26f).J(RA,-.2f,0,-2.15f).J(RE,-.25f).J(LA,-.2f,0,2.15f).J(LE,-.25f).PassKey();
            k.Key(CoverAt+.28f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.04f)).J(C,-.1f,-.04f,0).J(Hd,-.16f,.06f,0)
                .J(RA,1.45f,1.02f,-1.41f).J(RE,-1.59f).J(LA,1.41f,-1.03f,1.31f).J(LE,-1.61f).ImpactKey();
            k.Key(CardAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.035f)).J(C,-.11f,-.06f,0).J(Hd,-.17f,.08f,.04f)
                .J(RA,1.45f,1.02f,-1.41f).J(RE,-1.59f).J(LA,1.41f,-1.03f,1.31f).J(LE,-1.61f);
            k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.035f)).J(C,-.12f,-.1f,0).J(Hd,-.18f,.12f,.05f)
                .J(RA,1.45f,1.02f,-1.41f).J(RE,-1.59f).J(LA,1.41f,-1.03f,1.31f).J(LE,-1.61f);Keys=k;
            RootPath=(t,r)=>new Vector3(t<CutAt?0:Sway(t),r.Y,t<CutAt?-6.4f:Path.At(t));
            // The camera: the street, wide; dollying back low ahead of the stride; three photographs; the storm; the cover.
            Rig.Dolly(0,new Vector3(0,1.5f,9.2f),new Vector3(0,1.7f,8.1f),new Vector3(0,3.2f,-6),new Vector3(0,2.6f,-6),50,46)
               .Dolly(CutAt,new Vector3(.42f,.62f,.05f),new Vector3(.32f,.7f,3.25f),new Vector3(0,.15f,.3f),new Vector3(0,.12f,.2f),40,40).Follow(NativeEntranceAnchor.Chest).Ease(NativeIntroEase.Linear).Handheld(.4f)
               .Dolly(Photos[0]-.04f,new Vector3(2.5f,1.05f,2.5f),new Vector3(2.38f,1.04f,2.36f),new Vector3(0,1.2f,0),new Vector3(0,1.22f,0),30,28)
               .Dolly(Photos[1]-.04f,new Vector3(-2.35f,1.3f,2.75f),new Vector3(-2.22f,1.28f,2.6f),new Vector3(0,1.15f,0),new Vector3(0,1.17f,0),30,28)
               .Dolly(Photos[2]-.04f,new Vector3(0,.82f,3.0f),new Vector3(0,.84f,2.82f),new Vector3(0,1.32f,0),new Vector3(0,1.34f,0),32,30)
               .Crane(StormAt-.05f,new Vector3(0,.95f,3.4f),new Vector3(0,1.25f,5.9f),.5f,new Vector3(0,1.6f,-1),new Vector3(0,2.6f,-4.5f),42,46).Ease(NativeIntroEase.InOut)
               .Dolly(CardAt-.35f,new Vector3(0,.49f,5.9f),new Vector3(0,.55f,5.45f),new Vector3(0,.76f,0),new Vector3(0,.8f,0),38,36).Ease(NativeIntroEase.Out)
               .Bars(0,.8f).Bars(CardAt-.5f,.35f).Black(0,1).Black(.05f,0);
        }
        /// <summary>The strut down the carpet (the hips' z): in the doorway, then from the cut to the mark.</summary>
        public static float Walk(float t)=>t<CutAt?-6.4f:Path.At(t);
        public override bool Sliding(float t)=>Math.Abs(t-CutAt)<.06f;
        /// <summary>The strut's hips swinging out over the stance foot (m, its left +x): 4 cm a side, easing in at the cut and out onto the mark.</summary>
        private static float Sway(float t)=>-.04f*(float)Math.Sin(Math.PI*(t-Footfall(0))/Cadence)*NativeEntranceMath.Smooth((t-CutAt)/.2f)*(1-NativeEntranceMath.Smooth((t-(MarkAt-.25f))/.3f));
        /// <summary>The doors' swing (radians open).</summary>
        public static float Doors(float t)=>1.35f*NativeEntranceMath.EaseOut((t-DoorsAt)/.5f)*(t>DoorsAt?1:0)-.08f*(float)(Math.Exp(-6*Math.Max(0,t-DoorsAt-.5f))*Math.Sin(Math.Max(0,t-DoorsAt-.5f)*14))*(t>DoorsAt+.5f?1:0);
        // The stride is the keyed rig's own (planned steps on the carpet): the motion bank's Armed-Walk is the sword's guarded combat
        // walk, hips side-on to the chest, and walking at the lens its legs read as kicking and crossing sideways (the owner's
        // report, 2026-11-20); no clip plays.
        public override float Solid(float t,int bone)=>t<DoorsAt+.05f?0:1;
        public override string AccentAt(float t)=>t<DoorsAt?"#1a0a14":"#ff5aa8";
        protected override Vector3 Focus(float t,Vector3 chest)=>chest;
        /// <summary>The marquee's run `g` (0-3) lighting: the chase round the canopy (each bulb under 3 Hz; slower and gentler in Reduced).</summary>
        private float Run(int g,float t)
        {
            float on=NativeEntranceMath.Ramp(t,BulbsAt+g*.1f,BulbsAt+g*.1f+.08f);if(on<=0)return 0;
            float f=Context.Reduced?.7f:1.6f,amp=Context.Reduced?.25f:.7f;
            return on*(1-amp+amp*(.5f+.5f*(float)Math.Cos(2*Math.PI*(f*t-g/4f))));
        }

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("flashbulb/street"))return false;
            set.Piece("flashbulb/sky",Vector3.Zero,Still,1,White,White);
            set.Piece("flashbulb/street",Vector3.Zero,Still,1,White,Vector3.Zero);
            for(int g=0;g<4;g++)set.Piece(g==0?"flashbulb/bulbs0":g==1?"flashbulb/bulbs1":g==2?"flashbulb/bulbs2":"flashbulb/bulbs3",Vector3.Zero,Still,1,White,Bulb*(.2f+3.2f*Run(g,t)));
            float neon=NativeEntranceMath.Ramp(t,.2f,.5f)*(Context.Reduced?1:.85f+.15f*(float)Math.Sin(t*4.7f));
            set.Piece("flashbulb/neon",Vector3.Zero,Still,1,White,White*(.15f+2.4f*neon));
            set.Piece("flashbulb/posters",Vector3.Zero,Still,1,White,White*(.3f+1.3f*NativeEntranceMath.Ramp(t,.1f,.4f)));
            float open=NativeEntranceMath.Ramp(t,DoorsAt,DoorsAt+.3f);
            // The lit glass: its own warm colour times a warm glow, which High's bloom and tone map wash to a warm white; Low draws without
            // them, where the same product clips to flat yellow (the first Low captures), so there the glass's own colour carries it.
            var lit=Context.Low?White*.45f:Warm;
            set.Piece("flashbulb/glass",Vector3.Zero,Still,1,White,lit*(.5f+1.9f*open));
            float d=Doors(t);
            set.Piece("flashbulb/leaf",U(-1.0f,0,FrontZ+.08f),UYaw(-d),1,White,lit*(.5f+1.2f*open));
            set.Piece("flashbulb/leaf",U(1.0f,0,FrontZ+.08f),UYaw((float)Math.PI+d),1,White,lit*(.5f+1.2f*open));
            set.Piece("flashbulb/globes",Vector3.Zero,Still,1,White,Warm*2.2f);
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)
        {
            Plain(set,t,NativeEntranceFx.Hex(0x2a2430),Warm,.5f+.5f*NativeEntranceMath.Ramp(t,DoorsAt,DoorsAt+.4f),Pink);
            // The carpet stays: a long red runner of soft pools to the mark.
            for(int i=0;i<5;i++)set.Primitive(WeaponShape.Sphere,false,new Vector3(0,NativeEntranceStage.FloorY+.004f+.001f*i,-6.0f+2.6f*i),Quaternion.Identity,new Vector3(2.2f,.006f,3.4f),Carpet*.85f);
        }

        // ---- the light ----
        private float FlashLevel(int i,float t)=>Context.Reduced&&!Kept(i)?0:Flash(t,FlashT[i],.015f,.09f,Context.Reduced?.6f:1);
        /// <summary>In Reduced effects only the flashes at least 0.4 s after the last one kept are kept (under 3 Hz).</summary>
        private static bool Kept(int i){float last=-10;for(int k=0;k<=i;k++){if(FlashT[k]-last>=.4f){if(k==i)return true;last=FlashT[k];}}return false;}
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.DriveProbe=true;
            var fog=NativeEntranceFx.Hex(0x0d0b1a);l.Background=fog;l.Fog=fog;l.FogNear=22;l.FogFar=150;
            l.Sky=NativeEntranceFx.Hex(0x6c6aa8,.28f);l.Ground=NativeEntranceFx.Hex(0x8a3a30,.22f);
            l.Directional(new Vector3(.3f,.8f,.6f),NativeEntranceFx.Hex(0x9aa8ff,.16f));
            float bulbs=0;for(int g=0;g<4;g++)bulbs+=Run(g,t)*.25f;
            l.Add(Marquee,Warm*(3.6f*bulbs),10);
            float open=NativeEntranceMath.Ramp(t,DoorsAt,DoorsAt+.35f);
            // The marquee's and the foyer's warm light down the carpet: the stage material takes only directionals and the ambient
            // (docs/INTROS-V2.md 3a), so it is a directional from over the doors.
            l.Directional(new Vector3(0,.55f,-1),Warm*(.18f+.3f*bulbs+.28f*open));if(open>.01f)l.Add(Foyer+new Vector3(0,0,1.4f),Warm*(4.2f*open),8);
            // The photographs' own flashes, from the lens; the photographers' along the barriers (the brightest two this frame).
            Look(t);for(int i=0;i<Photos.Length;i++){float f=Flash(t,Photos[i],.015f,.16f);if(f>.01f)l.Add(Eye+new Vector3(0,.3f,0),Flashc*(26*f),9);}
            int a=-1,b=-1;float la=0,lb=0;
            for(int i=0;i<FlashT.Length;i++){if(FlashT[i]>t+.02f)break;if(t-FlashT[i]>.5f)continue;float lv=FlashLevel(i,t);if(lv>la){b=a;lb=la;a=i;la=lv;}else if(lv>lb){b=i;lb=lv;}}
            if(a>=0&&la>.01f)l.Add(FlashP[a],Flashc*(14*la),7);if(b>=0&&lb>.01f)l.Add(FlashP[b],Flashc*(14*lb),7);
            l.Add(U(6.2f,4.1f,5.1f),Warm*1.4f,6);l.Add(U(-6.2f,4.1f,5.1f),Warm*1.4f,6);
            if(t>=CardAt-.3f){float c=NativeEntranceMath.Ramp(t,CardAt-.3f,CardAt+.2f);var chest=Anchor(NativeEntranceAnchor.Chest,t);l.Add(chest+new Vector3(-1.5f,.8f,1.6f),Pink*(1.6f*c),6);l.Add(chest+new Vector3(1.6f,.9f,1.5f),Cyan*(1.4f*c),6);}
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);float floor=NativeEntranceStage.FloorY;bool low=q<.5f||Context.Low;
            // Searchlights sweeping the sky from the roof's corners: long soft beams on the camera's plane.
            {
                float on=NativeEntranceMath.Ramp(t,.3f,.8f);
                if(on>.01f)for(int s=0;s<2;s++)
                {
                    float sx=s==0?-1:1,ang=(float)Math.Sin(t*.55f+s*2.1f)*.45f+sx*.25f;var foot=U(sx*11.5f,12.8f,FrontZ-1.0f);
                    var dir=Vector3.Normalize(new Vector3((float)Math.Sin(ang),.95f,-.25f+.1f*(float)Math.Cos(t*.4f+s)));
                    Line(fx,foot+dir*42,dir*40,NativeEntranceFx.Hex(0xc8d4ff)*(.22f*on),1.6f,.7f,0,25100+s);
                    fx.Glow(foot,Warm*(.9f*on),0,.6f,1,0,25110+s);
                }
            }
            // The marquee's glow under the canopy and its bulbs' haloes.
            {
                float bulbs=0;for(int g=0;g<4;g++)bulbs+=Run(g,t)*.25f;
                for(int i=0;i<(low?3:7);i++){float x=-5+10*i/(low?2f:6f);fx.Glow(Marquee+new Vector3(x,.75f,1.3f),Warm*(.35f*bulbs),0,1.1f,1,0,25200+i);}
            }
            // The light spilling down the carpet from the open doors.
            {
                float open=NativeEntranceMath.Ramp(t,DoorsAt,DoorsAt+.4f);if(open>.01f){fx.Glow(Foyer+new Vector3(0,.3f,-.4f),Warm*(.9f*open),0,2.2f,1,0,25300);fx.Glow(new Vector3(0,floor+.05f,FrontZ+1.4f),Warm*(.5f*open),0,1.8f,1,0,25301);}
            }
            // The photographers' flashes: a white burst at each, a sparkle of glints.
            for(int i=0;i<FlashT.Length;i++)
            {
                if(FlashT[i]>t)break;if(t-FlashT[i]>.35f)continue;float lv=FlashLevel(i,t);if(lv<.01f)continue;
                fx.Glow(FlashP[i],Flashc*(3.4f*lv),0,.35f,1,0,25400+i%60);fx.Glow(FlashP[i],Flashc*(.9f*lv),0,1.6f,1,0,25460+i%60);
                if(!low){var fw=Vector3.Normalize(LookAt-Eye);var right=Vector3.Normalize(Vector3.Cross(fw,Vector3.UnitY));var up=Vector3.Cross(right,fw);NativeEntranceFx.Glint(fx,FlashP[i],Flashc*(1.5f*lv),.32f*lv+.08f,right,up,25520+i%60);}
            }
            // Each photograph's flash: the lens itself floods white for an instant (softened in Reduced effects).
            for(int i=0;i<Photos.Length;i++){float f=Flash(t,Photos[i],.015f,.12f,Context.Reduced?.5f:1);if(f>.01f){var f3=Vector3.Normalize(LookAt-Eye);fx.Glow(Eye+f3*.6f,Flashc*(1.6f*f),0,1.6f,1,0,25600+i);}}
            // The storm: confetti blown out over the carpet from both ends of the front, fluttering down.
            {
                float a=t-StormAt;
                if(a>=0)
                {
                    int n=low?24:64;
                    for(int i=0;i<n;i++)
                    {
                        float born=.25f*NativeEntranceMath.Rand(25700,i),s=a-born;if(s<0)continue;float life=2.8f;if(s>life)continue;
                        float side=i%2==0?-1:1;var from=U(side*4.8f,4.2f,FrontZ+.6f);var v=new Vector3(-side*(1.6f+1.4f*NativeEntranceMath.Rand(25701,i)),2.6f+1.8f*NativeEntranceMath.Rand(25702,i),2.0f+2.4f*NativeEntranceMath.Rand(25703,i));
                        float drag=(float)(1-Math.Exp(-1.8*s))/1.8f;var p=from+v*drag+new Vector3((float)Math.Sin(s*6+i)*.25f,-1.1f*s*s*.5f,(float)Math.Cos(s*5+i)*.2f);if(p.Y<floor+.02f)p.Y=floor+.02f;
                        float flick=Context.Reduced?.8f:.55f+.45f*(float)Math.Abs(Math.Sin(s*9+i));fx.Glow(p,Confetti[i%Confetti.Length]*(1.1f*flick*(1-NativeEntranceMath.Ramp(s,life-.6f,life))),.6f,.05f,3,s/life,25700+i);
                    }
                }
            }
            // Dust in the marquee's light.
            {int n=low?6:14;for(int i=0;i<n;i++){var p=Marquee+new Vector3(NativeEntranceMath.Signed(25900,i)*4.5f,-1.4f-1.6f*NativeEntranceMath.Rand(25901,i),1.2f+2.2f*NativeEntranceMath.Rand(25902,i))+new Vector3((float)Math.Sin(t*.4f+i)*.2f,(float)Math.Sin(t*.3f+i)*.15f,0);fx.Glow(p,Warm*.1f,0,.025f,0,0,25900+i);}}
        }

        // ---- the name card and the sound ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Y=CardY;card.Style=NativeIntroCardStyle.Slam;card.Ink=new Vector3(1,.97f,.95f);card.Glow=Pink*1.5f;
        }
        private static readonly NativeIntroCue[] cues;
        private static NativeIntroCue[] BuildCues()
        {
            var l=new System.Collections.Generic.List<NativeIntroCue>
            {
                new NativeIntroCue(DoorsAt+.05f,"intro2.flashbulb.crowd","ui.round-won","win",.35f),
                new NativeIntroCue(BulbsAt,"intro2.flashbulb.bulbs","ui.toggle","ui",.5f),
                new NativeIntroCue(DoorsAt,"intro2.flashbulb.doors","structure.creak","wood",.55f),
                new NativeIntroCue(WalkAt+.23f,"intro2.flashbulb.cheer",null,null,.45f),
                new NativeIntroCue(Photos[0],"intro2.flashbulb.photo","gun.dryfire","reload-end",.85f),
                new NativeIntroCue(Photos[1],"intro2.flashbulb.photo","gun.dryfire","reload-end",.85f,1.04f),
                new NativeIntroCue(Photos[2],"intro2.flashbulb.photo","gun.dryfire","reload-end",.85f,.96f),
                new NativeIntroCue(StormAt,"intro2.flashbulb.burst","gun.dryfire","reload-end",.7f),
                new NativeIntroCue(StormAt+.05f,"intro2.flashbulb.confetti","death.style.confetti","win",.5f),
                new NativeIntroCue(CardAt,"intro2.flashbulb.name","chip.critical","install",.75f),
            };
            // A shutter for every third photographer's flash through the walk (the rest are the crowd's bed).
            for(int i=0;i<FlashT.Length;i+=FlashT[i]<StormAt?3:2)l.Add(new NativeIntroCue(FlashT[i],"intro2.flashbulb.shutter","gun.dryfire","reload-end",.35f,.9f+.2f*NativeEntranceMath.Rand(25950,i)));
            l.Sort((x,y)=>x.At.CompareTo(y.At));return l.ToArray();
        }
        public override NativeIntroCue[] Cues=>cues;
    }
}
