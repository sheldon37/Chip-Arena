using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's dispatch (docs/WEAPON-TECHNIQUES.md, "The third set"). Each family's effects, previews, sounds and poses are one file: NativeTechniqueThirdMelee, ...Ranged, ...Magic.
    // On the same terms as the first two sets: pure functions of the replicated record and the clock, thinned by Reduced and the low tier, reaching nothing but the drawer.
    public sealed partial class NativeTechniqueFx
    {
        /// <summary>Whether a third-set technique has its effect yet. The checks read this, so they cover each as it lands.</summary>
        public static bool ThirdBuilt(string id)=>Array.IndexOf(MeleeBuilt,id)>=0||Array.IndexOf(RangedBuilt,id)>=0||Array.IndexOf(MagicBuilt,id)>=0;
        /// <summary>When the wind-up ends, for a kind whose `Arm` is that time: the rules write `Arm` only at the release, so until then it reads as `Release`.</summary>
        public static float ArmAt(NativeTechnique k)=>k.Arm>k.At?k.Arm:k.Release;
        /// <summary>The height of a pilot's capsule centre over the feet: the rules' `Path`, Magnetize's `Point` and a double's `To` are at body height, not on the floor.</summary>
        public const float BodyCentre=.865f;
        /// <summary>Whether a pilot's body is out of sight for someone `distance` away: Mirror Double's second, Ghillie once settled and beyond its 6 m. The rules hide the name and the bar; this is the body.</summary>
        public static bool Unseen(NativeTechnique k,float time,float distance)=>k?.Kind==NativeTechniques.MirrorDouble&&time>=k.Release&&time<k.Arm||k?.Kind==NativeTechniques.Ghillie&&time>=ArmAt(k)+.25f&&time<k.Until&&distance>6;
        /// <summary>The whiteout at `time` for whoever carries Flash Round's mark: full for over half of it, then clearing.</summary>
        public static float Whiteout(NativeTechniqueMark[] marks,float time)
        {
            float a=0;if(marks==null)return 0;foreach(var m in marks){if(m.Kind!=NativeTechniques.FlashRound||time<m.At||time>=m.Until)continue;float u=(time-m.At)/Math.Max(.05f,m.Until-m.At);a=Math.Max(a,1-Smooth((u-.55f)/.45f));}return a;
        }
        // What the rules leave on someone else: each pilot's own marks, so the rest of the fight can see who has been cracked open, who is rallying against whom, and who is reeling. The rest of the marks are drawn by the caster's own effect.
        private static void ThirdMarks(ITechniqueFxSink s,IKitFxScene scene,Actor a,float t,bool low)
        {
            var marks=a.TechniqueMarks;if(marks==null||marks.Length==0||!a.Alive||scene.Viewer==a.Id)return;var feet=scene.Feet(a);var chest=scene.Chest(a,out var c0)?c0:feet+Up*1.25f;
            foreach(var m in marks)
            {
                if(t<m.At||t>=m.Until)continue;float env=Smooth((t-m.At)/.12f)*Smooth((m.Until-t)/.2f);long seed=a.Id*97L+(long)Math.Round(m.At*240);
                switch(m.Kind)
                {
                    // Cracks of light across the body.
                    case NativeTechniques.Sunder:
                    {
                        float flick=.8f+.2f*(float)Math.Sin(t*19+a.Id);int n=low?3:6;
                        for(int i=0;i<n;i++){var d=RandomDir(seed,i*5+1);d.Y*=1.3f;var p0=chest+d*.06f;var p1=p0+d*.2f+RandomDir(seed,i*5+2)*.08f;var p2=p1+d*.18f+RandomDir(seed,i*5+3)*.08f;var col=new Vector3(1,.7f,.4f)*(1.9f*env*flick);s.Line(false,p0,p1,col,.03f,true);s.Line(false,p1,p2,col,.02f,true);}
                        s.Sprite(false,chest,new Vector3(1,.46f,.16f)*(.7f*env*flick),.34f,Hot);break;
                    }
                    // A gold chevron over the head, pointing down: it is angry at whoever shouted.
                    case NativeTechniques.WarCry:
                    {
                        var top=feet+Up*(2.2f+.04f*(float)Math.Sin(t*5+a.Id));var side=RightOf(Flat(N(V3.Forward(a.Yaw)),Vector3.UnitZ))*.17f;var col=new Vector3(1,.74f,.26f)*(1.5f*env);s.Line(false,top+side+Up*.14f,top,col,.05f,true);s.Line(false,top-side+Up*.14f,top,col,.05f,true);break;
                    }
                    // Ink over the eyes, and it reels.
                    case NativeTechniques.Dread:
                    {
                        var head=feet+Up*1.72f;s.Glow(head,new Vector3(.03f,.01f,.06f),.8f*env,.46f,Core,0,seed);int n=low?2:4;for(int i=0;i<n;i++){float ang=t*7+i*6.2831853f/n;s.Sprite(false,head+new Vector3((float)Math.Cos(ang)*.3f,.18f,(float)Math.Sin(ang)*.3f),new Vector3(.6f,.3f,1)*(1.6f*env),.08f,Sparkle);}break;
                    }
                    // Dazzled, for anyone looking at them.
                    case NativeTechniques.FlashRound:s.Sprite(false,feet+Up*1.7f,Vector3.One*(1.8f*env),.34f,Sparkle);break;
                }
            }
        }
        private static void Third(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(!ThirdMelee(s,scene,a,k,t,seed,q,low)&&!ThirdRanged(s,scene,a,k,t,seed,q,low))ThirdMagic(s,scene,a,k,t,seed,q,low);
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static void ThirdWrite(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first)
        {
            if(!WriteMelee(k,id,feet,f,r,up,hand,t0,first)&&!WriteRanged(k,id,feet,f,r,up,hand,t0,first))WriteMagic(k,id,feet,f,r,up,hand,t0,first);
        }
        private static float ThirdPeriod(string id){float p=PeriodMelee(id);if(p>0)return p;p=PeriodRanged(id);if(p>0)return p;p=PeriodMagic(id);return p>0?p:3.4f;}
        // Whoever the preview has to aim at, for the moves that hunt someone.
        private void ThirdAim(NativeTechnique k,string id){if(TargetId<=0)return;if(!AimMelee(k,id,TargetId,TargetAt)&&!AimRanged(k,id,TargetId,TargetAt))AimMagic(k,id,TargetId,TargetAt);}
    }
    public static partial class NativeTechniqueSounds
    {
        // The rules' `Release` for these is the wind-up's end, when the move goes off. Their first cue belongs on the press, and the `Arm` cue on the going off.
        private static bool ThirdFromPress(string kind)=>kind==NativeTechniques.GunSpin||kind==NativeTechniques.Combust||kind==NativeTechniques.Thunderclap||kind==NativeTechniques.Overload||kind==NativeTechniques.Ghillie||kind==NativeTechniques.Dread;
        // The rules keep these records past the landing: the sound belongs on the boots coming down.
        private static float ThirdEnd(NativeTechnique k)=>k.Kind==NativeTechniques.VaultShot?k.RecoverUntil:k.Kind==NativeTechniques.SlideShot?k.Until-.2f:k.Until;
        private static int ThirdMoment(string kind,int m,NativeKitSound[] o,ref bool atPoint)
        {
            int n=MomentMelee(kind,m,o,ref atPoint);if(n==0)n=MomentRanged(kind,m,o,ref atPoint);if(n==0)n=MomentMagic(kind,m,o,ref atPoint);return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private static bool ThirdPose(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            // Most of these lock nothing after the release (the rules' RecoverUntil is the Release), so a pose ending on `back` alone would be gone in a blink.
            back=Math.Max(back,k.Release+.3f);
            return PoseMelee(set,k,time,back,clips,out pick)||PoseRanged(set,k,time,back,clips,out pick)||PoseMagic(set,k,time,back,clips,out pick);
        }
    }
}
