using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One frame of NOCTIEL's presentation: what the body, the camera, the water and the screen do. Stage coordinates (the disc's centre at the origin, the water at y 0).</summary>
    public sealed class FallenBossFrame
    {
        public NativeFallenRecord Live;public NativeFallenBossPreview.Act[] Actions;public FallenFxProp[] LiveAdds;public NativeFallenAdds.Seen[] Adds;public Vector3 Target;
        public float P;public int Section,Phase=1;public byte Kind;
        public float SongClip;public int SongEpoch;
        public Vector3 Boss;public float Yaw;public bool BossShown=true;
        /// <summary>The pose: ClipFrom at TimeFrom crossfading to ClipTo at TimeTo by Mix.</summary>
        public string ClipFrom,ClipTo;public float TimeFrom,TimeTo,Mix;
        /// <summary>Above the water by the move itself (a leap); feet on the water at 0.</summary>
        public float Air;
        /// <summary>Down on one knee (0..1), and the sword carried at its side, its point on the water, as it stands or walks (0..1).</summary>
        public float Kneel,Carry;
        /// <summary>At rest (0..1): Runtime squares the held stride into the owner's reference stance (feet planted apart, the left a little ahead, knees nearly straight, hips and chest square).</summary>
        public float Stand;
        /// <summary>Its breath (-1 out .. 1 in; NativeFallenLiveMotion.BreathAt) and how much of its life shows (0..1: all of it at rest, some under a walk or
        /// a move, none on a knee); Runtime lays them through the chest, shoulders, head and wings, with its weight and head drifting.</summary>
        public float Breath,Life;
        /// <summary>The head follows this pilot while it stands (0..1 how hard); gliding: it runs on nothing, its feet off the water.</summary>
        public float Track;public int TrackPilot;public bool Gliding;
        /// <summary>The sword draws its trail (the strike itself, not every move of the arm).</summary>
        public float Trail;
        /// <summary>The wings: each pair's opening (0 folded close, 1 spread), and how hard they're thrown out by a swing.</summary>
        public readonly float[] WingOpen=new float[3];public float Flare;
        /// <summary>The head turned to the camera (0..1).</summary>
        public float LookAtEye;
        /// <summary>The fist on the planted sword: 0 the clip's hand, 1 the sword stands in the water at SwordRoot and the fist closes on it (SwordLift raises it).</summary>
        public float Plant,SwordLift;
        /// <summary>The white light inside it, the key light on its plate, the water's dim, the floor's strike.</summary>
        public float Inner=1,Key=1,Reveal=1;
        /// <summary>The shot: a stage eye and look, or on a joint (Core: 0 none; else FallenBossFrame.Mark*), plus an offset.</summary>
        public Vector3 Eye,Look;public int EyeMark,LookMark;public float Fov=45;
        public float SlashAge=-1,SlashAngle;public byte SlashKind;
        public float ChipAge=-1;public int ChipPhase;
        public float CardAge=-1;
        /// <summary>The cutscene's one feather (Hidden: below the water).</summary>
        public Vector3 Feather;public float FeatherTurn,FeatherPitch,FeatherRoll;public bool FeatherShown;
        /// <summary>The intro's key poses on top of the clip (Core NativeFallenIntroMotion; off outside the cutscene).</summary>
        public FallenIntroPose Intro;
        public float Black;
        public const int MarkNone=0,MarkHead=1,MarkHand=2,MarkChest=3,MarkTip=4,MarkFeather=5;
    }
    /// <summary>Stand-in pilots on the water, and where they are (presentation only: for scale, ripples and a look target).</summary>
    public static class NativeFallenPilots
    {
        public const int Count=3;public const float StepEvery=.32f,WalkStep=.54f;
        public const float CutsceneEnd=50.81f;
        private static readonly float[] R={21,27,16},W={.33f,-.26f,.42f},Ph={.2f,2.4f,4.3f};
        public static Vector3 At(int i,float p)
        {
            if(p<CutsceneEnd){float x=i==0?-3.2f:i==1?6.4f:-7.8f,start=i==0?13.5f:i==1?15:16.6f;float z=-49+Math.Max(0,p-start)*1.5f;return new Vector3(x,0,z);}
            float a=W[i]*p+Ph[i],r=R[i]+3.5f*(float)Math.Sin(.31f*p+i*1.7f);return new Vector3(r*(float)Math.Cos(a),0,r*(float)Math.Sin(a));
        }
        public static float Yaw(int i,float p)
        {
            if(p<CutsceneEnd)return 0;var d=At(i,p+.05f)-At(i,p-.05f);return (float)(Math.Atan2(d.X,d.Z)*180/Math.PI);
        }
        public static bool Walking(float p)=>p<CutsceneEnd;
        public static string Clip(float p)=>Walking(p)?"Armed-Strafe-Forward":"Armed-Run-Forward";
        public static bool Shown(int i,float p)=>p>=CutsceneEnd||(p>=(i==0?13.5f:i==1?15:16.6f)&&(p<NativeFallenBossPreview.GauntletAt||p>=NativeFallenBossPreview.RiseAt));
    }
    /// <summary>
    /// NOCTIEL's DEV preview (DEV panel · Camera · "Final boss preview"), a scripted fight on the presentation clock, nothing spawned: the cutscene with the song from 0:04, each phase with its moves on the song's beats, the Slash and
    /// the chip screen between them, and the death. A pure function of the clock p. The capture tool reads <see cref="On"/>, <see cref="Start"/> and <see cref="ShownClock"/> by name. Astra's rules (docs/EXPEDITION-BOSS-FINAL.md section 13) replace
    /// the script with the record when they exist.
    /// </summary>
    public static class NativeFallenBossPreview
    {
        public static bool On;public static double Start=double.NaN,ShownClock;public static float ShownP;
        /// <summary>DEV: how fast the script runs on the clock (1 as it plays; slower to study a move frame by frame).</summary>
        public static double Rate=1;
        public static readonly FallenBossFrame Shown=new FallenBossFrame();
        public const byte KindCutscene=0,KindFight=1,KindChips=2,KindDeath=3;
        public struct Section{public float From,To,Clip;public int Phase;public byte Kind;}
        public static readonly Section[] Sections;public static readonly float Loop;
        public const float ChipSeconds=10.81f,ChipClip=135f;
        private const byte Hold=0,Looped=1,Strike=2;
        public struct Seg
        {
            public float T0,T1,Fade,Wind,Hit,ClipFrom,Rate;public string Clip;public byte Mode;
            public Vector3 A,B;public float YawA,YawB;public byte Path;public float Height;public float Flare;public float Kneel;
        }
        /// <summary>A move on the script: what the effects read (Core NativeFallenBossFx).</summary>
        public struct Act{public string Id;public float At,Hit,End;public int Phase,Seed,Target;public Vector3 From,To;public float Aim;}
        public static readonly Seg[] Segs;public static readonly Act[] Acts;
        // Paths: 0 eased; 1 late and hard (a lunge); 2 a leap of Height; 3 still.
        private const byte PathEase=0,PathLunge=1,PathLeap=2,PathStill=3;
        private const string Stance="Unarmed-Idle",Dash="Armed-Dash-Forward",Run="Armed-Run-Forward";
        public static float Bars(float bars)=>bars*NativeFallenSong.Bar;
        static NativeFallenBossPreview()
        {
            var s=new List<Section>();float t=0;
            void Add(float length,float clip,int phase,byte kind){s.Add(new Section{From=t,To=t+length,Clip=clip,Phase=phase,Kind=kind});t+=length;}
            Add(NativeFallenPilots.CutsceneEnd,NativeFallenSong.IntroFrom,1,KindCutscene);
            Add(NativeFallenSong.P2-NativeFallenSong.Drop,NativeFallenSong.Drop,1,KindFight);
            Add(ChipSeconds,ChipClip,2,KindChips);
            Add(NativeFallenSong.Break-NativeFallenSong.P2,NativeFallenSong.P2,2,KindFight);
            Add(ChipSeconds,ChipClip,3,KindChips);
            Add(NativeFallenSong.P4-NativeFallenSong.P3,NativeFallenSong.P3,3,KindFight);
            Add(ChipSeconds,ChipClip,4,KindChips);
            Add(NativeFallenSong.DeathAt-NativeFallenSong.P4,NativeFallenSong.P4,4,KindFight);
            Add(NativeFallenSong.End-NativeFallenSong.DeathAt,NativeFallenSong.DeathAt,4,KindDeath);
            Sections=s.ToArray();Loop=t+2;
            var b=new Script(Sections);b.Build();Segs=b.Segs.ToArray();Acts=b.Acts.ToArray();
        }
        public static int SectionAt(float p){for(int i=0;i<Sections.Length;i++)if(p<Sections[i].To)return i;return Sections.Length-1;}
        private static int SegAt(float p){int lo=0,hi=Segs.Length-1;while(lo<hi){int m=(lo+hi+1)/2;if(Segs[m].T0<=p)lo=m;else hi=m-1;}return lo;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float EaseOut(float x){x=Clamp01(x);return 1-(1-x)*(1-x)*(1-x);}
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float LerpAngle(float a,float b,float t){float d=(b-a)%360;if(d>180)d-=360;if(d<-180)d+=360;return a+d*t;}
        /// <summary>A strike's retiming: a long, slow windup to just before the clip's hit, the hit itself almost at once, then a slow settle: it holds still, then moves all at once.</summary>
        public static float ClipTime(in Seg s,float p,NativeVisualMotion.IClips clips)
        {
            if(s.Mode==Hold)return s.ClipFrom;
            if(s.Mode==Looped)return s.ClipFrom+(p-s.T0)*s.Rate;
            float len=clips!=null&&clips.Has(s.Clip)?clips.Length(s.Clip):1.2f,hit=clips!=null&&clips.Has(s.Clip)?clips.Hit(s.Clip):len*.45f;
            if(!(hit>.12f*len))hit=.12f*len;if(hit>.85f*len)hit=.85f*len;
            float a=.62f*hit,b=Math.Min(len,hit+.16f*len),w=s.T0+s.Wind,k=w+s.Hit;
            if(p<w){float u=Clamp01((p-s.T0)/Math.Max(.01f,s.Wind));return a*(float)Math.Pow(u,1.35f);}
            if(p<k)return a+(b-a)*EaseOut((p-w)/Math.Max(.01f,s.Hit));
            return b+(len-b)*EaseOut((p-k)/Math.Max(.01f,s.T1-k));
        }
        /// <summary>Where the boss is, off any script: for the camera's look and the moves' reach.</summary>
        public static void BossAt(float p,out Vector3 at,out float air)
        {
            ref readonly var s=ref Segs[SegAt(p)];float u=Clamp01((p-s.T0)/Math.Max(.01f,s.T1-s.T0));air=0;
            switch(s.Path)
            {
                case PathStill:at=s.A;return;
                case PathLunge:{float w=s.T0+s.Wind;float v=p<w?0:EaseOut((p-w)/Math.Max(.01f,s.Hit));at=Vector3.Lerp(s.A,s.B,v);return;}
                case PathLeap:{float w=s.T0+s.Wind;float v=p<w?0:Clamp01((p-w)/Math.Max(.01f,s.Hit));at=Vector3.Lerp(s.A,s.B,Smooth(v));air=4*s.Height*v*(1-v);if(p>=w+s.Hit)air=0;return;}
                default:at=Vector3.Lerp(s.A,s.B,Smooth(u));return;
            }
        }
        public static void Eval(float p,NativeVisualMotion.IClips clips,FallenBossFrame f)
        {
            f.P=p;int si=SectionAt(p);var sec=Sections[si];f.Section=si;f.Phase=sec.Phase;f.Kind=sec.Kind;f.SongClip=sec.Clip+(p-sec.From);f.SongEpoch=si;
            // A Slash happens in the section before the phase's number changes: the new design is drawn only under the black.
            int seg=SegAt(p);ref readonly var s=ref Segs[seg];
            BossAt(p,out f.Boss,out f.Air);
            float fade=s.Fade<=0?.14f:s.Fade;f.ClipTo=s.Clip;f.TimeTo=ClipTime(s,p,clips);f.Mix=Smooth((p-s.T0)/fade);
            f.Kneel=s.Kneel;f.Carry=0;bool still=s.Clip==Stance&&s.Kneel==0;if(still)f.Carry=Smooth((p-s.T0)/.32f);
            if(seg>0){ref readonly var q=ref Segs[seg-1];float m2=Smooth((p-s.T0)/fade);f.Kneel=Lerp(q.Kneel,s.Kneel,m2);bool was=q.Clip==Stance&&q.Kneel==0;if(was&&!still)f.Carry=1-Smooth((p-s.T0)/.13f);else if(was&&still)f.Carry=1;}
            if(seg>0&&f.Mix<1){ref readonly var q=ref Segs[seg-1];f.ClipFrom=q.Clip;f.TimeFrom=ClipTime(q,p,clips);}else{f.ClipFrom=s.Clip;f.TimeFrom=f.TimeTo;f.Mix=1;}
            f.Yaw=LerpAngle(s.YawA,s.YawB,EaseOut((p-s.T0)/Math.Max(.05f,Math.Min(.2f,s.T1-s.T0))));
            f.Trail=0;if(s.Mode==Strike){float tw=s.T0+s.Wind;f.Trail=p<tw-.05f?0:1-Smooth((p-tw-s.Hit-.14f)/.2f);}
            f.Flare=0;if(s.Mode==Strike){float w=s.T0+s.Wind;f.Flare=s.Flare*(p<w?.25f*Smooth((p-s.T0)/Math.Max(.01f,s.Wind)):1-Clamp01((p-w-s.Hit)/.5f));}
            f.Gliding=s.Clip==Dash;if(f.Gliding){float gu=Clamp01((p-s.T0)/Math.Max(.01f,s.T1-s.T0));f.Air=Math.Max(f.Air,.5f*(float)Math.Sin(gu*Math.PI));}
            f.Track=sec.Kind==KindFight?(s.Clip==Stance&&s.Kneel==0?Smooth((p-s.T0)/.4f):0):0;f.TrackPilot=si%NativeFallenPilots.Count;
            f.BossShown=true;f.Plant=0;f.SwordLift=0;f.LookAtEye=0;f.Intro=default;f.FeatherPitch=f.FeatherRoll=0;f.Inner=1;f.Key=1;f.Reveal=1;f.SlashAge=-1;f.ChipAge=-1;f.CardAge=-1;f.FeatherShown=false;f.Black=0;f.EyeMark=f.LookMark=0;
            for(int w=0;w<3;w++)f.WingOpen[w]=sec.Phase==1?.1f:sec.Phase==2?.3f:sec.Phase==3?.75f:1;
            if(sec.Kind==KindCutscene)Cutscene(p,f);
            else if(sec.Kind==KindChips){f.ChipAge=p-sec.From;f.ChipPhase=sec.Phase;f.Black=1;}
            else if(sec.Kind==KindDeath)Death(p,sec,f);
            else Fight(p,sec,si,f);
            // The Slash across a section's end (a cut) and the next's start (the reveal).
            SlashOf(p,si,sec,f);
        }
        private const float SlashCut=.5f,SlashCutLength=1.7f,SlashRevealLead=.32f,SlashRevealLength=1.5f;
        private static void SlashOf(float p,int si,in Section sec,FallenBossFrame f)
        {
            if(si+1<Sections.Length&&sec.Kind==KindFight&&Sections[si+1].Kind==KindChips&&p>=sec.To-SlashCutLength){f.SlashKind=NativeFallenSlash.Cut;f.SlashAge=p-(sec.To-SlashCutLength);f.SlashAngle=-32+si*19;}
            if(sec.Kind==KindCutscene&&p>=sec.To-SlashCut){f.SlashKind=NativeFallenSlash.Opening;f.SlashAge=p-(sec.To-SlashCut);f.SlashAngle=-58;}
            if(sec.Kind==KindFight&&si==1&&p<sec.From+NativeFallenSlash.Length(NativeFallenSlash.Opening)-SlashCut){f.SlashKind=NativeFallenSlash.Opening;f.SlashAge=p-sec.From+SlashCut;f.SlashAngle=-58;}
            // Reveal: from the black of the chip screen, the phase's drop opens it.
            if(sec.Kind==KindChips&&p>=sec.To-SlashRevealLead){f.SlashKind=NativeFallenSlash.Reveal;f.SlashAge=p-(sec.To-SlashRevealLead);f.SlashAngle=-24+si*31;}
            if(sec.Kind!=KindChips&&si>0&&Sections[si-1].Kind==KindChips&&p<sec.From+SlashRevealLength-SlashRevealLead){f.SlashKind=NativeFallenSlash.Reveal;f.SlashAge=p-sec.From+SlashRevealLead;f.SlashAngle=-24+(si-1)*31;}
        }
        // ---------------- the cutscene "The light fell" (docs/EXPEDITION-BOSS-FINAL.md section 7), p = song - 4 ----------------
        public const float FeatherLand=10.2f,GauntletAt=24.5f,BladeAt=28f,HaloAt=31.5f,RiseAt=35.2f,MaskAt=44.46f,PullAt=47f,CutAt=50.1f;
        public static readonly float[] WingAt={36.1f,38.3f,40.6f};
        private static void Shot(FallenBossFrame f,Vector3 eyeA,Vector3 eyeB,Vector3 lookA,Vector3 lookB,float fovA,float fovB,float u){f.Eye=Vector3.Lerp(eyeA,eyeB,u);f.Look=Vector3.Lerp(lookA,lookB,u);f.Fov=Lerp(fovA,fovB,u);}
        private static void Cutscene(float p,FallenBossFrame f)
        {
            // It kneels with the sword in the water, wings close; it rises, draws, and cuts at the lens on the drop. The body's key poses
            // are NativeFallenIntroMotion's (the owner's four reference clips): the fist stays on the planted sword until the keyed
            // path takes the blade out of the water, and holds it from there to the cut.
            var ip=NativeFallenIntroMotion.At(p);f.Intro=ip;
            f.BossShown=p>=11;f.Plant=ip.Mocap?ip.Plant:1-ip.Sword;f.Carry=0;f.SwordLift=ip.SwordLift;f.Key=p<RiseAt?.75f:1;f.Kneel=Math.Max(ip.KneelLegs,ip.KneelUpper);f.Trail=ip.Trail;f.Flare=ip.Flare;
            for(int w=0;w<3;w++)f.WingOpen[w]=Smooth((p-WingAt[w])/2.1f);
            if(p>MaskAt+.2f)for(int w=0;w<3;w++)f.WingOpen[w]=Lerp(f.WingOpen[w],.72f,Smooth((p-PullAt)/2));
            f.WingOpen[0]=Math.Min(1.12f,f.WingOpen[0]+ip.WingTop);
            f.LookAtEye=p<MaskAt?0:Smooth((p-MaskAt)/1.1f)*(ip.Mocap?1-Smooth((p-PullAt)/.6f):1);// the extracted motion's own head after the mask's close-up
            f.Reveal=p<GauntletAt?.04f:Smooth((p-GauntletAt-.5f)/8f)*.12f+.04f;
            if(p<11)
            {
                // The feather falls like a leaf (NativeFallenIntroMotion.Feather); the shot follows its old line down, as before.
                float u=Clamp01((p-1.4f)/(FeatherLand-1.4f));float y=3.7f*(1-u);
                NativeFallenIntroMotion.Feather(p,out f.Feather,out f.FeatherTurn,out f.FeatherPitch,out f.FeatherRoll,out f.FeatherShown);
                Shot(f,new Vector3(0,.95f,-55.5f),new Vector3(0,.72f,-55.2f),new Vector3(.2f,y*.92f+.1f,-50),new Vector3(.2f,y*.92f+.1f,-50),30,27,Clamp01(p/11));f.Black=1-Smooth((p-.6f)/1.2f);
            }
            else if(p<GauntletAt)
                Shot(f,new Vector3(3.4f,.42f,-46),new Vector3(5.2f,.36f,-40),new Vector3(0,1.5f,0),new Vector3(0,1.6f,0),15,12.5f,Clamp01((p-11)/(GauntletAt-11)));
            else if(p<BladeAt){Shot(f,new Vector3(.95f,-.25f,-1.9f),new Vector3(.75f,-.1f,-1.65f),Vector3.Zero,Vector3.Zero,27,24,Clamp01((p-GauntletAt)/3.5f));f.EyeMark=f.LookMark=FallenBossFrame.MarkHand;}
            else if(p<HaloAt)
            {
                float u=Clamp01((p-BladeAt)/(HaloAt-BladeAt));u=u*u*(3-2*u);
                {var at=NativeFallenSongPlant.At(Vector3.Zero,180);var m=NativeFallenIntroMocap.Active;if(m!=null&&ip.Mocap){var g=m.PlantGrip*NativeFallenBoss.Scale;at=new Vector3(-g.X,0,-g.Z);}Shot(f,at+new Vector3(-1.5f,.3f,-3.9f),at+new Vector3(-1.2f,1.5f,-3.7f),at+new Vector3(0,.3f,0),at+new Vector3(0,1.75f,0),15,17,u);}
            }
            else if(p<RiseAt){Shot(f,new Vector3(-.95f,.32f,-1.55f),new Vector3(.25f,.38f,-1.85f),new Vector3(0,.28f,-.1f),new Vector3(0,.3f,-.1f),30,27,Clamp01((p-HaloAt)/3.7f));f.EyeMark=f.LookMark=FallenBossFrame.MarkHead;}
            else if(p<MaskAt)Shot(f,new Vector3(0,.82f,-26),new Vector3(0,.7f,-22),new Vector3(0,2.5f,0),new Vector3(0,2.75f,0),20,19,Clamp01((p-RiseAt)/9.3f));
            else if(p<PullAt){Shot(f,new Vector3(.32f,-.42f,-2.65f),new Vector3(.16f,-.62f,-2.15f),Vector3.Zero,Vector3.Zero,29,26,Smooth((p-MaskAt)/2.5f));f.EyeMark=f.LookMark=FallenBossFrame.MarkHead;}
            else if(p<CutAt)Shot(f,new Vector3(-4.6f,.55f,-8.2f),new Vector3(-2.6f,.42f,-6.6f),new Vector3(-.3f,2.2f,0),new Vector3(-.25f,3.4f,0),36,40,Smooth((p-PullAt)/3.1f));
            else Shot(f,new Vector3(0,1.6f,-6.4f),new Vector3(0,1.15f,-5.4f),new Vector3(0,3.7f,0),new Vector3(0,3.2f,0),48,58,EaseOut((p-CutAt)/.7f));
        }
        // ---------------- the fight's camera: over the first pilot's shoulder, facing it; wide for what crosses the map; low for the phase's reveal and its last swing ----------------
        private static Vector3 SmoothFront(float p)
        {
            var sum=Vector3.Zero;for(int k=0;k<9;k++){ref readonly var s=ref Segs[SegAt(p-k*.12f)];double y=s.YawB*Math.PI/180;sum+=new Vector3((float)Math.Sin(y),0,(float)Math.Cos(y));}
            return sum.LengthSquared()<1e-4f?new Vector3(0,0,1):Vector3.Normalize(sum);
        }
        private static Vector3 SmoothBoss(float p){var sum=Vector3.Zero;for(int k=0;k<7;k++){BossAt(p-k*.09f,out var at,out float air);at.Y=air*.5f;sum+=at;}return sum/7;}
        private static void Fight(float p,in Section sec,int si,FallenBossFrame f)
        {
            float age=p-sec.From,left=sec.To-p;var boss=SmoothBoss(p);var pilot=NativeFallenPilots.At(0,p);
            var away=pilot-boss;away.Y=0;float gap=away.Length();away=gap>.01f?away/gap:new Vector3(0,0,-1);
            var front=SmoothFront(p);var flank=new Vector3(front.Z,0,-front.X);var way=Vector3.Normalize(front+flank*(.85f*(float)Math.Sin(si*1.3f+age*.21f)));
            var eye=boss+way*10.5f+new Vector3(0,2.1f,0);var look=boss+new Vector3(0,2.45f,0);float fov=42;
            // Wide for the moves that cross the map.
            float wide=0;foreach(var a in Acts){if(p<a.At-1.2f||p>a.End+1.2f)continue;if(a.Id=="verdict"||a.Id=="last"||a.Id=="eclipse"||a.Id=="fall"||a.Id=="rain"||a.Id=="severance"||a.Id=="sweep"){float k=Smooth((p-a.At+1.2f)/1.2f)*(1-Smooth((p-a.End)/1.2f));if(a.Id=="severance")k*=.4f;wide=Math.Max(wide,k);}}
            if(wide>0)
            {
                eye=Vector3.Lerp(eye,boss+way*17+new Vector3(0,6.2f,0),wide);look=Vector3.Lerp(look,boss+new Vector3(0,2.2f,0)-way*3,wide);fov=Lerp(fov,48,wide);
            }
            foreach(var a in Acts)
            {
                if(a.Id=="vow"&&p>a.At&&p<a.Hit+1.4f)
                {
                    // Side-on, the whole lane in frame.
                    float k=Smooth((p-a.At)/.35f)*(1-Smooth((p-a.Hit-.5f)/.8f));var mid=(a.From+a.To)*.5f;var lane=a.To-a.From;lane.Y=0;var perp=Vector3.Normalize(new Vector3(lane.Z,0,-lane.X)+new Vector3(.001f,0,0));if(Vector3.Dot(eye-mid,perp)<0)perp=-perp;
                    eye=Vector3.Lerp(eye,mid+perp*(lane.Length()*.5f+7)+new Vector3(0,3.6f,0),k);look=Vector3.Lerp(look,mid+new Vector3(0,2,0),k);fov=Lerp(fov,46,k);
                }
                else if(a.Id=="descent"&&p>a.At-.2f&&p<a.Hit+1.8f)
                {
                    // From across the place it's going to land, looking up at the fall.
                    float k=Smooth((p-a.At+.2f)/.45f)*(1-Smooth((p-a.Hit-.9f)/.8f));var v=a.From-a.To;v.Y=0;v=Vector3.Normalize(v+new Vector3(.01f,0,0));var side2=new Vector3(v.Z,0,-v.X);
                    eye=Vector3.Lerp(eye,a.To+Vector3.Normalize(v+side2*.8f)*15+new Vector3(0,2.6f,0),k);look=Vector3.Lerp(look,a.To+new Vector3(0,3.8f,0),k);fov=Lerp(fov,54,k);
                }
            }
            // Low at the opening of a phase: the new design, then back over the shoulder.
            float hero=age<3.4f&&si>1?1-Smooth((age-1.8f)/1.6f):0;
            if(hero>0)
            {
                var fw=new Vector3((float)Math.Sin(f.Yaw*Math.PI/180),0,(float)Math.Cos(f.Yaw*Math.PI/180));var e=f.Boss+fw*(7.5f-age*.6f)+new Vector3(1.4f,.75f,0);var l=f.Boss+new Vector3(0,2.45f,0);
                eye=Vector3.Lerp(eye,e,hero);look=Vector3.Lerp(look,l,hero);fov=Lerp(fov,36,hero);
            }
            // Low in front of it for its last swing at the lens.
            float last=left<2.6f&&si+1<Sections.Length&&Sections[si+1].Kind==KindChips?Smooth((2.6f-left)/.9f):0;
            if(last>0)
            {
                var fw=new Vector3((float)Math.Sin(f.Yaw*Math.PI/180),0,(float)Math.Cos(f.Yaw*Math.PI/180));float up=Smooth((1.5f-left)/.6f);var e=f.Boss+fw*(6.6f-up*1.2f)+new Vector3(0,Lerp(.9f,1.2f,up),0);var l=f.Boss+new Vector3(0,Lerp(1.65f,3.1f,up),0);
                eye=Vector3.Lerp(eye,e,last);look=Vector3.Lerp(look,l,last);fov=Lerp(fov,Lerp(40,54,up),last);f.LookAtEye=last;
            }
            f.Eye=eye;f.Look=look;f.Fov=fov;
            if(si==1&&age<3.2f){f.CardAge=age;}
            f.Inner=1;
        }
        // ---------------- the death: it staggers, kneels, plants the sword; the light leaks; at the song's hit (4:08) it goes, and the sword stands alone ----------------
        public const float DeathHit=248.89f-NativeFallenSong.DeathAt;
        private static void Death(float p,in Section sec,FallenBossFrame f)
        {
            float a=p-sec.From;float orbit=.09f*a+1.4f;var r=new Vector3((float)Math.Sin(orbit),0,(float)Math.Cos(orbit));
            f.Plant=Smooth((a-4.2f)/1.4f);f.SwordLift=0;for(int w=0;w<3;w++)f.WingOpen[w]=Lerp(1,0,Smooth((a-1)/5));f.LookAtEye=Smooth((a-6)/4);f.Inner=.85f+Smooth((a-6)/(DeathHit-6))*1.1f;
            float d=Lerp(13,7.2f,Smooth(a/DeathHit));f.Eye=f.Boss+r*d+new Vector3(0,1.3f,0);f.Look=f.Boss+new Vector3(0,1.8f,0);f.Fov=34;
            if(a>=DeathHit)
            {
                f.BossShown=false;float after=a-DeathHit;float o2=1.4f+.09f*DeathHit;var r2=new Vector3((float)Math.Sin(o2),0,(float)Math.Cos(o2));
                f.Eye=f.Boss+r2*(9+after*.12f)+new Vector3(0,1.1f,0);f.Look=f.Boss+new Vector3(.9f,1.5f,0);f.Fov=30;
                f.Black=Smooth((after-10)/3.2f);
            }
        }
        // ---------------- the script ----------------
        private sealed class Script
        {
            public readonly List<Seg> Segs=new List<Seg>();public readonly List<Act> Acts=new List<Act>();private readonly Section[] secs;
            private float now;private Vector3 pos;private float yaw;private int seed=1;
            public Script(Section[] s){secs=s;}
            private static float YawTo(Vector3 from,Vector3 to){var d=to-from;return d.LengthSquared()<1e-4f?0:(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);}
            private void Push(float until,string clip,byte mode,float rate,float clipFrom,Vector3 to,float yawTo,byte path=PathEase,float wind=0,float hit=0,float height=0,float fade=.14f,float flare=0,float kneel=0)
            {
                if(until<=now+.001f)return;
                Segs.Add(new Seg{T0=now,T1=until,Fade=fade,Wind=wind,Hit=hit,ClipFrom=clipFrom,Rate=rate,Clip=clip,Mode=mode,A=pos,B=to,YawA=yaw,YawB=yawTo,Path=path,Height=height,Flare=flare,Kneel=kneel});
                now=until;pos=to;yaw=yawTo;
            }
            private void Still(float until){var look=Target(0,until);Push(until,Stance,Looped,1,0,pos,YawTo(pos,look),PathStill,fade:.26f);}
            private Vector3 Target(int i,float at)=>NativeFallenPilots.At(i,at);
            private float OnBeat(float hit,float lead){while(hit-lead<now)hit+=NativeFallenSong.Beat;return hit;}
            // All at once: it stands still until just before the move, then it's there.
            private void Close(float hit,float wind,Vector3 stand){float length=Vector3.Distance(pos,stand);if(length<1)return;float d=Math.Min(.55f,.2f+length/34);Still(hit-wind-d);Push(hit-wind,Dash,Looped,1.6f,0,stand,YawTo(pos,stand),PathEase,fade:.08f);}
            private Act Mark(string id,float hit,float wind,float life,int phase,int target,Vector3 to,float aim=0)
            {
                var a=new Act{Id=id,At=hit-wind,Hit=hit,End=hit+life,Phase=phase,Seed=seed++,Target=target,From=pos,To=to,Aim=aim};Acts.Add(a);return a;
            }
            // One cut of the claymore, landing on `hit`.
            private void Cut(string id,string clip,float hit,float wind,float strike,float settle,int phase,int target,float reach,byte path=PathLunge,float flare=.6f)
            {
                var goal=Target(target,hit);var dir=goal-pos;dir.Y=0;float g=dir.Length();dir=g>.01f?dir/g:new Vector3(0,0,1);var to=pos+dir*Math.Max(0,Math.Min(reach,g-3));
                Mark(id,hit,wind,settle+.6f,phase,target,goal,YawTo(pos,goal));
                Push(hit+strike+settle,clip,Strike,1,0,to,YawTo(pos,goal),path,wind,strike,0,.12f,flare);
            }
            private void Cleave(float hit,int phase,int target)
            {
                hit=OnBeat(hit,.34f+.55f);
                var goal=Target(target,hit);var dir=goal-pos;dir.Y=0;float g=dir.Length();dir=g>.01f?dir/g:new Vector3(0,0,1);
                Close(hit,.34f,goal-dir*4.6f);
                Cut("cleave","Sword-Attack-R1",hit,.34f,.12f,.2f,phase,target,3.6f);float h=hit+2*NativeFallenSong.Beat;
                Cut("cleave","Sword-Attack-R2",h,Math.Max(.1f,h-now-.14f),.12f,.18f,phase,target,3.6f);h+=2*NativeFallenSong.Beat;
                Cut("cleave","Sword-Attack-R3",h,Math.Max(.1f,h-now-.14f),.14f,.5f,phase,target,4,flare:.9f);
            }
            private void Vow(float hit,int phase,int target)
            {
                const float wind=.8f;hit=OnBeat(hit,wind);Still(hit-wind);var goal=Target(target,hit);var dir=Vector3.Normalize(goal-pos+new Vector3(.001f,0,0));dir.Y=0;var to=pos+dir*Math.Min(35,Vector3.Distance(goal,pos)+7);
                Mark("vow",hit,wind,1.1f,phase,target,to,YawTo(pos,goal));Push(hit+.3f+.6f,"Armed-Run-Attack-R1",Strike,1,0,to,YawTo(pos,goal),PathLunge,wind,.3f,0,.14f,.8f);
            }
            private void Severance(float hit,int phase,int target,int waves=1)
            {
                const float wind=.9f;hit=OnBeat(hit,wind);Still(hit-wind);var goal=Target(target,hit);Mark("severance",hit,wind,5f,phase,target,goal,YawTo(pos,goal));
                Push(hit+.2f+.7f,"Sword-Attack-R4",Strike,1,0,pos,YawTo(pos,goal),PathStill,wind,.2f,0,.16f,1);
                if(waves>1){Mark("severance",hit+1.1f,0,5f,phase,target,goal,YawTo(pos,goal)+36);Push(hit+2.2f,"Sword-Attack-R6",Strike,1,0,pos,yaw,PathStill,.2f,.2f,0,.1f,1);}
            }
            private void Descent(float hit,int phase,int target)
            {
                const float rise=.28f,air=1.25f;hit=OnBeat(hit,air+rise);Still(hit-air-rise);var goal=Target(target,hit);Mark("descent",hit,air+rise,2.2f,phase,target,goal,YawTo(pos,goal));
                Push(hit-air,"2Hand-Sword-Jump",Strike,1,0,pos,YawTo(pos,goal),PathStill,rise*.7f,rise*.3f,0,.1f,.5f);
                // A leap, its landing on the hit.
                Push(hit,"2Hand-Sword-Air-Attack1",Strike,1,0,goal,YawTo(pos,goal),PathLeap,0,air,11,.1f,.7f);
                Push(hit+.75f,"2Hand-Sword-Land",Strike,1,0,goal,yaw,PathStill,0,.02f,0,.04f,.8f);
            }
            private void Rain(float hit,int phase)
            {
                const float wind=1.2f;hit=OnBeat(hit,wind);Still(hit-wind);Mark("rain",hit,wind,3.2f,phase,0,pos);Push(hit+.8f,"Armed-Activate-Left",Strike,1,0,pos,yaw,PathStill,wind,.3f,0,.2f,1);
            }
            private void Verdict(string id,float hit,float wind,int phase,int target)
            {
                hit=OnBeat(hit,wind);Still(hit-wind);var goal=Target(target,hit-wind);Mark(id,hit,wind,1.8f,phase,target,goal,YawTo(pos,goal));
                Push(hit+1.1f,"2Hand-Sword-Attack11",Strike,1,0,pos,YawTo(pos,goal),PathStill,wind,.18f,0,.22f,.7f);
            }
            private void Summon(string id,float hit,float life,int phase)
            {
                const float wind=1.1f;hit=OnBeat(hit,wind);Still(hit-wind);Mark(id,hit,wind,life,phase,0,pos);Push(hit+.7f,"Armed-Activate-Left",Strike,1,0,pos,yaw,PathStill,wind,.24f,0,.2f,.6f);
            }
            private void Sweep(float hit,int phase)
            {
                const float wind=1f;hit=OnBeat(hit,wind);Still(hit-wind);Mark("sweep",hit,wind,1.6f,phase,0,pos,yaw);Push(hit+1.05f,"2Hand-Sword-Attack3",Strike,1,0,pos,yaw,PathStill,wind,.3f,0,.2f,1.1f);
            }
            private void Eclipse(float hit,int phase)
            {
                const float wind=3.2f;hit=OnBeat(hit,wind);Still(hit-wind);Mark("eclipse",hit,wind,1.8f,phase,0,pos);Push(hit+1.1f,"2Hand-Sword-Activate",Strike,1,0,pos,yaw,PathStill,wind,.3f,0,.3f,.5f);
            }
            private void FallOfLight(float from,int bars,int phase)
            {
                float wind=NativeFallenSong.Bar;from=OnBeat(from,wind);Still(from-wind);Mark("fall",from,wind,bars*NativeFallenSong.Bar,phase,0,pos);Push(from+bars*NativeFallenSong.Bar,"2Hand-Sword-Activate",Strike,1,0,pos,yaw,PathStill,wind,.3f,0,.3f,.3f);
            }
            // The threshold: a knee, then it rises and swings at the lens as the Slash cuts.
            private void Threshold(float end,string clip="Sword-Attack-R6")
            {
                float knee=end-2.75f,swing=end-1.25f;Still(knee);Push(swing,Stance,Looped,.5f,0,pos,yaw,PathStill,fade:.22f,kneel:1);
                Push(end,clip,Strike,1,0,pos,yaw,PathStill,.55f,.14f,0,.16f,1);
            }
            private float Beat(int section,float bars,float beats=0)=>secs[section].From+(bars*4+beats)*NativeFallenSong.Beat;
            public void Build()
            {
                // The kneel, the rise, the sword drawn; the opening cut at the lens on the drop.
                pos=Vector3.Zero;yaw=180;now=0;
                float yawFront=180;
                Push(RiseAt,Stance,Looped,.4f,0,pos,yawFront,PathStill,fade:.01f,kneel:1);
                Push(PullAt,Stance,Looped,.4f,0,pos,yawFront,PathStill,fade:3.1f);
                Push(CutAt,Stance,Looped,.5f,0,pos,yawFront,PathStill,fade:.5f);
                // The cut itself is keyed (NativeFallenIntroMotion: load, a 0.20 s downswing, the lunge on the drop) over the stance.
                Push(secs[0].To+.45f,Stance,Looped,.5f,0,pos,yawFront,PathStill,fade:.2f);
                // ---- GRACE ----
                pos=new Vector3(0,0,0);Severance(Beat(1,2),1,0);Cleave(Beat(1,5),1,0);Vow(Beat(1,8),1,1);Descent(Beat(1,10),1,1);Cleave(Beat(1,13),1,2);
                Threshold(secs[1].To);
                // ---- WRATH ---- (the song's 1:22; it stands where it fell and comes on)
                now=secs[3].From;Segs.Add(new Seg{T0=secs[2].From,T1=now,Clip=Stance,Mode=Looped,Rate=1,A=pos,B=pos,YawA=yaw,YawB=yaw,Path=PathStill,Fade=.1f});
                Still(Beat(3,1));
                Rain(Beat(3,2),2);Severance(Beat(3,4.5f),2,1,2);Verdict("verdict",Beat(3,7),2.4f,2,0);Summon("choir",Beat(3,9),11,2);Cleave(Beat(3,11.5f),2,0);Vow(Beat(3,14),2,2);Descent(Beat(3,16),2,0);Verdict("verdict",Beat(3,18.5f),2.4f,2,1);Severance(Beat(3,21),2,2);
                Threshold(secs[3].To);
                // ---- RUIN ----
                now=secs[5].From;Segs.Add(new Seg{T0=secs[4].From,T1=now,Clip=Stance,Mode=Looped,Rate=1,A=pos,B=pos,YawA=yaw,YawB=yaw,Path=PathStill,Fade=.1f});
                Still(Beat(5,1));
                Sweep(Beat(5,2),3);Eclipse(Beat(5,5),3);Summon("penance",Beat(5,7),8,3);Cleave(Beat(5,9),3,1);
                Threshold(secs[5].To);
                // ---- LUX CECIDIT ----
                now=secs[7].From;Segs.Add(new Seg{T0=secs[6].From,T1=now,Clip=Stance,Mode=Looped,Rate=1,A=pos,B=pos,YawA=yaw,YawB=yaw,Path=PathStill,Fade=.1f});
                Still(Beat(7,1));
                FallOfLight(Beat(7,3),4,4);Cleave(Beat(7,8),4,0);Summon("thrones",Beat(7,9),16,4);Verdict("last",Beat(7,11),2.8f,4,0);Descent(Beat(7,13),4,2);Sweep(Beat(7,15),4);Severance(Beat(7,17),4,1,2);FallOfLight(Beat(7,22),4,4);Cleave(Beat(7,27),4,1);Vow(Beat(7,29),4,0);Verdict("last",Beat(7,31),2.8f,4,1);Descent(Beat(7,33),4,0);Sweep(Beat(7,35),4);
                Still(secs[7].To);
                // ---- the death ----
                Push(secs[8].From+3.2f,"2Hand-Sword-Stunned",Looped,.7f,0,pos,yaw,PathStill,fade:.3f);
                Push(secs[8].To+2,Stance,Looped,.3f,0,pos,yaw,PathStill,fade:1.6f,kneel:1);
                Segs.Add(new Seg{T0=now,T1=now+10,Clip=Stance,Mode=Looped,Rate=1,A=pos,B=pos,YawA=yaw,YawB=yaw,Path=PathStill,Fade=.1f});
            }
        }
    }
    /// <summary>The Slash (docs/EXPEDITION-BOSS-FINAL.md section 10) as numbers on its own clock: pure, drawn by Runtime NativeFallenSlashImage through Resources/NativeFallenSlash.shader. The line draws, the frame freezes, the halves part.</summary>
    public static class NativeFallenSlash
    {
        /// <summary>Cut: a phase ends, the halves part over black. Opening: the cutscene's halves part over the fight. Reveal: black's halves part over the next phase.</summary>
        public const byte Cut=0,Opening=1,Reveal=2;
        public static float Length(byte kind)=>kind==Cut?1.7f:kind==Opening?1.75f:1.5f;
        public static float FreezeAt(byte kind)=>kind==Reveal?.32f:.5f;
        public struct State{public float Draw,Open,Gap,Width,Flash,Black,Mono,Ink;public bool Freeze;}
        private static float C(float x)=>x<0?0:x>1?1:x;
        private static float EaseOut(float x){x=C(x);return 1-(1-x)*(1-x)*(1-x);}
        private static float EaseIn(float x){x=C(x);return x*x*x;}
        public static State Eval(byte kind,float age)
        {
            float cut=FreezeAt(kind),after=age-cut,span=Length(kind)-cut;var s=new State();
            s.Draw=EaseIn(age/cut*.98f+.02f);s.Freeze=age>=cut;
            // The line: thin as it's drawn, opening at the cut with its core a white hot wedge, thinning away.
            s.Width=age<cut?.0012f+.0035f*C(age/cut):.003f+.055f*(float)Math.Exp(-after*9)+.006f*(1-C(after/span));
            s.Mono=kind==Reveal?0:C(age/cut)*.92f;
            s.Flash=age<cut?0:(float)Math.Exp(-after*(kind==Reveal?9:13))*(kind==Reveal?.55f:.7f);
            s.Ink=age<cut?.15f*C(age/cut):1-C((after-span*.55f)/(span*.45f));
            float open=EaseOut(after/(span*(kind==Cut?1:.8f)));
            if(kind==Cut){s.Open=.34f*open;s.Gap=.018f*open;s.Black=C((after-span*.62f)/(span*.38f));s.Flash+=.7f*(float)Math.Exp(-Math.Abs(after-span*.62f)*26);}
            else{s.Open=1.5f*open*open;s.Gap=.05f*open;}
            return s;
        }
    }
}
