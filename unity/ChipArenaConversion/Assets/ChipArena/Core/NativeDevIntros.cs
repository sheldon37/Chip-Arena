using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV animation classes' own entrances (owner 2026-11-12, the DEV animation picks: "entrance → Intro"): the pack's Intro clip on the
    /// model, in place of the base class's entrance (the Colossus Oath, Crimson Moon). The clip plays once at its own pace from StartAt (its
    /// first frame held until then), then the class's stance under the hero shot and the name card; a keyed stance under it for the Classic
    /// body, a camera, the stage's light and a little dust where the swing lands. The runtime lays the clip on the intro body through the
    /// arena's motion (Core NativeVisualMotion.Choose, whatever the set), the weapon held as the match holds it (NativeDevMotions.Grip).
    /// Presentation only: functions of the entrance clock.
    /// </summary>
    public abstract class NativeDevPackIntro:NativeBladeIntro
    {
        private readonly string spec,clip,idle;private readonly float clipLength,startAt,total,cardAt,end;
        protected NativeDevPackIntro(string style,string spec,string clip,float clipLength,string idle,float startAt,float hit,float total,float cardAt,float end,int accent):base(style,hit,accent)
        {
            this.spec=spec;this.clip=clip;this.clipLength=clipLength;this.idle=idle;this.startAt=startAt;this.total=total;this.cardAt=cardAt;this.end=end;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.25f,0,-.2f);
            k.Key(hit,stand).Root(0,.84f,.05f,Facing).Planted(1).Drop(-.06f).J(B,.1f).J(RA,-1.2f,0,-.5f).J(LA,-.4f,0,.4f).ImpactKey();
            k.Key(total,stand).Root(0,.88f,.05f,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.3f,0,-.2f);Keys=k;
        }
        public override string Spec=>spec;
        public override float Length=>total;public override float SettleAt=>end;protected override float HeroAt=>cardAt;
        /// <summary>When the Intro clip starts and ends (entrance s).</summary>
        public float StartAt=>startAt;public float ClipEnd=>startAt+clipLength;
        public override bool Clip(float t,out string clip,out float time)
        {
            if(t<ClipEnd){clip=this.clip;time=Math.Max(0,t-startAt);return true;}
            clip=idle;time=t-ClipEnd;return true;
        }
        public override float Fade(float t)=>t>=ClipEnd-.02f&&t<ClipEnd+.4f?.3f:.12f;
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,Hit,.01f,.35f)*.025f;
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float up=NativeEntranceMath.Ramp(t,ClipEnd-.3f,cardAt);NativeEntranceStage.Rig(l,.35f+.55f*up,Accent,.5f+.5f*up,.2f+.5f*up,.35f+.65f*up);
            float flash=NativeEntranceMath.Pulse(t,Hit,.01f,.45f);if(flash>.01f)l.Add(new Vector3(0,1.3f,0)+Local(0,0,.6f),(Accent*1.5f+Vector3.One*.5f)*(5*flash),6);
        }
        protected override float Pylon(int i,float t)=>t<Hit?.15f:PowerOn(i,t-Hit+.05f,.1f);
        public override void Props(float t,INativeEntranceProps props,float q)=>Stage(props,t,.15f+.4f*NativeEntranceMath.Ramp(t,Hit,cardAt),Hit);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            // Dust kicked up round the feet as the swing lands, settling; a glint where the blade comes through.
            float s=t-Hit;if(s<0||s>1.6f)return;float floor=NativeEntranceStage.FloorY;int n=q<.5f?6:14;
            for(int i=0;i<n;i++)
            {
                float a=i*(float)(Math.PI*2)/n+NativeEntranceMath.Signed(23100,i)*.3f,u=s/1.6f,r=.35f+1.1f*(1-(1-u)*(1-u))*(.7f+.3f*NativeEntranceMath.Rand(23110,i));
                var at=new Vector3((float)Math.Cos(a)*r,floor+.08f+.25f*u*NativeEntranceMath.Rand(23120,i),(float)Math.Sin(a)*r);
                fx.Smoke(at,Dust,.45f*(1-u),.22f+.5f*u,u,23130+i,floor);
            }
            if(s<.3f){float u=s/.3f;fx.Glow(new Vector3(0,1.25f,0)+Local(0,0,.7f),(Vector3.One*2+Accent)*(1.6f*(1-u)),0,.45f+.6f*u,1,u,23140);}
        }
        private static readonly Vector3 Dust=NativeEntranceFx.Hex(0x8f8a80);
    }

    /// <summary>The DEV Greatsword's entrance: its back turned, it comes round with the great sword in two great swings and sets it on its
    /// shoulder in the stance (the great sword set's `Intro`, 2.0 s: the turn, a swing at 0.63 s and the second, the beat, at 1.27 s).</summary>
    public sealed class NativeDevGreatswordIntro:NativeDevPackIntro
    {
        public const string Name="Heavy Turn",Music="audio/intros/neon-drop",Blurb="Back turned, then round with the great sword: two great swings and the blade on the shoulder.";
        public const float Total=4.8f,CardAt=3.2f,End=4.45f,ClipAt=.4f,ClipLength=2f,Beat=ClipAt+1.27f;
        public NativeDevGreatswordIntro():base(NativeDevMotions.GreatswordEntrance,NativeDevMotions.Greatsword,NativeDevGreatswordMotion.Intro,ClipLength,NativeDevGreatswordMotion.Idle,ClipAt,Beat,Total,CardAt,End,0xffcb87)
        {
            Shots=new[]
            {
                // Wide from ahead: the pilot's back to us, turning round.
                Shot(0,NativeEntranceCut.Cut,44,40,V(.8f,1.45f,5.4f),V(.6f,1.35f,4.7f),V(0,1.1f,0),V(0,1.1f,0),1,0),
                // Low on the left for the first swing.
                Shot(ClipAt+.5f,NativeEntranceCut.Cut,40,36,V(-2.5f,.85f,2.7f),V(-2.25f,.9f,2.4f),V(0,1.0f,0),V(0,1.05f,0),1,0),
                // Close from the right through the second swing (the beat).
                Shot(Beat-.25f,NativeEntranceCut.Whip,38,34,V(2.3f,1.25f,2.5f),V(1.9f,1.2f,2.2f),V(0,1.1f,0),V(0,1.05f,0),1,0,.1f),
                // The hero, the blade on the shoulder.
                Shot(ClipAt+ClipLength+.05f,NativeEntranceCut.Blend,38,34,V(-1.2f,1.1f,3.7f),V(-.9f,1.12f,3.3f),V(0,1.2f,0),V(0,1.22f,0),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.04f,3.5f),V(-.66f,1.06f,3.25f),V(-.08f,1.24f,0),V(-.1f,1.26f,0),1,0),
            };
        }
    }

    /// <summary>The DEV Katana's entrance: the katana raised, swept round and spun in a flourish, then the stance (the katana set's `Intro`,
    /// 2.83 s; its last great sweep, the beat, at 2.53 s).</summary>
    public sealed class NativeDevKatanaIntro:NativeDevPackIntro
    {
        public const string Name="Edge Flourish",Music="audio/intros/arc-salute",Blurb="The katana raised, swept round and spun in a flourish, then the stance.";
        public const float Total=5.3f,CardAt=3.7f,End=4.95f,ClipAt=.3f,ClipLength=2.83f,Beat=ClipAt+2.53f;
        public NativeDevKatanaIntro():base(NativeDevMotions.KatanaEntrance,NativeDevMotions.Katana,NativeDevKatanaMotion.Intro,ClipLength,NativeDevKatanaMotion.Idle,ClipAt,Beat,Total,CardAt,End,0xff3d6e)
        {
            Shots=new[]
            {
                // Wide from ahead and a little low: the blade raised.
                Shot(0,NativeEntranceCut.Cut,44,40,V(-.6f,1.1f,5.2f),V(-.5f,1.15f,4.6f),V(0,1.15f,0),V(0,1.2f,0),1,0),
                // Close on the hands as the blade goes up.
                Shot(ClipAt+.9f,NativeEntranceCut.Cut,32,30,V(1.2f,1.5f,1.8f),V(1.1f,1.5f,1.6f),V(-.05f,1.45f,0),V(-.05f,1.5f,0),1,0),
                // Wide on the right through the sweeps.
                Shot(ClipAt+1.4f,NativeEntranceCut.Cut,46,42,V(2.8f,1.2f,2.6f),V(2.5f,1.2f,2.3f),V(0,1.05f,0),V(0,1.05f,0),1,0),
                // Close and low on the left for the last sweep (the beat).
                Shot(Beat-.3f,NativeEntranceCut.Whip,38,34,V(-2.1f,.9f,2.2f),V(-1.8f,.95f,1.95f),V(0,1.05f,0),V(0,1.1f,0),1,0,.1f),
                // The hero.
                Shot(ClipAt+ClipLength+.05f,NativeEntranceCut.Blend,38,34,V(-1.2f,1.1f,3.7f),V(-.9f,1.12f,3.3f),V(0,1.2f,0),V(0,1.22f,0),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.04f,3.5f),V(-.66f,1.06f,3.25f),V(-.08f,1.24f,0),V(-.1f,1.26f,0),1,0),
            };
        }
    }
}
