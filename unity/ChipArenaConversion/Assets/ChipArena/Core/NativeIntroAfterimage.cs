using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Afterimage", the daggers' intro (intros v2; docs/INTROS-MELEE.md): faster than its own image. 7.2 s, the name at 5.8 s. A night
    /// market lane (Lantern Alley: timber shopfronts, wet cobbles, paper lanterns strung overhead, steam from the grates). The pilot walks up
    /// the lane and stops; two echoes of it peel off to either side, one teal and one magenta, each a beat behind the body. It cuts, and the
    /// echoes cut after it, each throwing its edge up through a lantern's cord: the lanterns drop and roll on the cobbles. A second exchange;
    /// then the echoes slide back into the body as it crosses its daggers, three images landing as one, and it stands under the name.
    /// The model plays the daggers' clips (NativeBladeIntro); the keyed rig plays the game's own captures of them (the holds are the match's),
    /// and the echoes are that rig's ghosts a moment behind, drawn as the model. Presentation only.
    /// </summary>
    public sealed class NativeIntroAfterimage:NativeIntroMeleeBlade
    {
        public const string Title="Afterimage";
        public const float Total=7.2f,CardAt=5.8f;
        /// <summary>The beats (presentation s): the stop, the split, the first cut, the second, the convergence, the stance.</summary>
        public const float StopAt=1.6f,SplitAt=2.15f,CutAAt=2.5f,CutCAt=3.65f,ConvergeAt=4.6f,StanceAt=5.15f;
        private const float CutLength=1.0f,HitA=.536f,HitC=.494f,LagL=.16f,LagR=.32f,Spread=1.25f;
        private static readonly Vector3 Teal=NativeEntranceFx.Hex(0x37ffd8),Magenta=NativeEntranceFx.Hex(0xff4fc8),Mint=NativeEntranceFx.Hex(0x87ffd5),Lamp=NativeEntranceFx.Hex(0xffb070),
            Night=NativeEntranceFx.Hex(0x06070c),Steam=new Vector3(.36f,.38f,.42f);
        public override string Spec=>"daggers";
        protected override float TipLength(bool left)=>.84f;
        private static readonly Vector3 WalkFrom=new Vector3(0,0,-2.4f);
        // The lanterns (the alley set's rigs: their cords' rings, Unity space) and which echo's cut drops which (-1: none).
        private static readonly Vector3[] Lanterns={new Vector3(.1f,3.35f,6.0f),new Vector3(-.35f,3.55f,3.4f),new Vector3(.25f,3.2f,.8f),new Vector3(-.15f,3.45f,-1.8f),new Vector3(.4f,3.6f,-4.4f),new Vector3(-.3f,3.3f,-7.0f),new Vector3(.15f,3.5f,-9.5f),new Vector3(-.4f,3.25f,-12.0f)};
        private static readonly string[] lanternProps={"alley/lantern-0","alley/lantern-1","alley/lantern-2","alley/lantern-3","alley/lantern-4","alley/lantern-5","alley/lantern-6","alley/lantern-7"};
        /// <summary>When lantern k's cord is cut (presentation s; never for those that stay up).</summary>
        private static float CutAt(int k)=>k==2?CutAAt+HitA+LagL:k==3?CutAAt+HitA+LagR:k==1?CutCAt+HitC+LagR:1e6f;

        public NativeIntroAfterimage():base(NativeIntroCatalog.WeaponPrefix+"daggers",Total,CardAt,CutAAt+HitA,0x87ffd5)
        {
            RootPath=(t,r)=>r+Way(t);
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1);k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1);Keys=k;
            // The camera: low in front as it walks up; close as the echoes peel off; down from a balcony on the three cutting and the
            // lanterns dropping; low at the side for the second exchange; in front as they converge; the hero.
            Rig.Dolly(0,V(-.6f,1.0f,4.6f),V(-.5f,1.05f,3.65f),V(0,1.3f,-1.8f),V(0,1.25f,-.3f),42,40)
               .Dolly(StopAt,V(-1.1f,1.0f,1.95f),V(-.9f,1.02f,1.62f),V(0,1.3f,0),V(0,1.28f,0),34,32)
               .Dolly(CutAAt-.1f,V(1.6f,3.6f,3.4f),V(1.3f,3.7f,3.0f),V(0,1.0f,-.4f),V(0,1.1f,-.6f),50,48)
               .Dolly(CutCAt-.05f,V(2.1f,.45f,.9f),V(2.0f,.5f,1.4f),V(0,1.0f,0),V(0,1.05f,0),46,46).Handheld(.5f)
               .Dolly(ConvergeAt-.05f,V(.3f,.8f,3.2f),V(.25f,.85f,2.7f),V(0,1.1f,0),V(0,1.12f,0),38,36);
            HeroFrame(StanceAt+.05f,1,1.15f,3.2f,32,.6f);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
        }
        public override string SetBank=>Bank;
        /// <summary>The walk up the lane (source frame, from the keyed root): from Start to the mark by the stop.</summary>
        public static Vector3 Way(float t)=>WalkFrom*(1-NativeEntranceMath.Clamp01(t/StopAt));

        // ---- the body: the daggers' clips on the model, the game's captures on the keyed rig ----
        private const int Idle=0,Walk=1,CutA=3,CutC=5,Block=7;
        private static void Moves(float t,out int capture,out string clip,out float time)
        {
            if(t<StopAt){capture=Walk;clip="Armed-Walk";time=t;return;}
            if(t<CutAAt){capture=Idle;clip="Armed-Idle";time=t-StopAt;return;}
            if(t<CutAAt+CutLength){capture=CutA;clip="Dagger-Attack-R1";time=t-CutAAt;return;}
            if(t<CutCAt){capture=Idle;clip="Armed-Idle";time=.5f+t-CutAAt-CutLength;return;}
            if(t<CutCAt+CutLength-.05f){capture=CutC;clip="Dagger-Attack-R2";time=t-CutCAt;return;}
            if(t<StanceAt){capture=Block;clip="Armed-Block-Dual";time=t-ConvergeAt+.3f;return;}
            capture=Idle;clip="Armed-Idle";time=1.0f+t-StanceAt;
        }
        public override bool Clip(float t,out string clip,out float time){Moves(t,out _,out clip,out time);return true;}
        public override float Fade(float t)=>t>=StopAt-.02f&&t<StopAt+.3f?.25f:t>=StanceAt-.02f&&t<StanceAt+.4f?.4f:.1f;
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            base.Pose(t,pose,out root,out rotation);Moves(t,out int capture,out _,out float time);Captured(1,capture,time,pose);
        }

        // ---- the echoes: the keyed rig's ghosts a moment behind, peeling out to either side and folding back in ----
        /// <summary>How far out the echoes stand (0..1) at `t`.</summary>
        private static float Out(float t)=>NativeEntranceMath.Smoother((t-SplitAt)/.3f)*(1-NativeEntranceMath.Smoother((t-ConvergeAt)/.45f));
        private static Vector3 Side=>Vector3.Transform(Vector3.UnitX,Quaternion.CreateFromAxisAngle(Vector3.UnitY,Facing));
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float o=Out(t);if(o<=.002f||into.Length<2)return 0;float lagOut=NativeEntranceMath.Smooth((t-SplitAt)/.4f);
            into[0]=new NativeEntranceGhost{Lag=LagL*lagOut,Opacity=.6f*o,Color=Teal,Offset=Side*(Spread*o)};
            into[1]=new NativeEntranceGhost{Lag=LagR*lagOut,Opacity=.6f*o,Color=Magenta,Offset=-Side*(Spread*o)};
            return 2;
        }

        // ---- the lanterns: swaying on their cords; the cut ones dropping, bouncing and rolling to a stop ----
        private static void Lantern(int k,float t,out Vector3 at,out Quaternion turn)
        {
            var r=Lanterns[k];at=U(r.X,r.Y,r.Z);float sway=.06f*(float)Math.Sin(t*1.3f+k*1.7f)+.03f*(float)Math.Sin(t*2.1f+k);
            turn=Quaternion.CreateFromAxisAngle(Vector3.UnitX,sway)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.5f*sway);
            float s=t-CutAt(k);if(s<=0)return;
            // Off its cord: falling, a tumble, one bounce on the cobbles, a short roll down the lane.
            float drop=r.Y-.35f,fall=(float)Math.Sqrt(2*drop/9.8f);
            if(s<fall){at.Y-=4.9f*s*s;turn=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,.2f,.4f)),s*5)*turn;return;}
            float b=s-fall,v=.35f*9.8f*fall;float hop=Math.Max(0,v*b-4.9f*b*b);at.Y-=drop-hop*.25f;float roll=.6f*(1-(float)Math.Exp(-2.5f*b));at.Z+=roll*(k%2==0?1:-1);at.X+=.15f*roll;
            turn=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,1.45f)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,roll*3);
        }
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("alley/static"))return false;
            set.Piece("alley/static",SetAt,Quaternion.Identity,1,White,White);
            for(int k=0;k<Lanterns.Length;k++){Lantern(k,t,out var at,out var turn);float lit=1-.7f*NativeEntranceMath.Ramp(t,CutAt(k)+.6f,CutAt(k)+1.4f);set.Piece(lanternProps[k],at,turn,1,White,White*lit);}
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x15161c),Mint,.8f,NativeEntranceFx.Hex(0x3a2440));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x0d0c14);l.FogNear=6;l.FogFar=30;l.DriveProbe=true;
            l.Sky=NativeEntranceFx.Hex(0x2a2440,.3f);l.Ground=NativeEntranceFx.Hex(0x120c10,.3f);
            l.Directional(new Vector3(.3f,1,.2f),NativeEntranceFx.Hex(0x8090c0,.18f));
            // The lanterns overhead (the near ones), the shop signs' neon either side, the echoes' own glow.
            for(int k=1;k<=3;k++){Lantern(k,t,out var at,out _);float lit=1-.7f*NativeEntranceMath.Ramp(t,CutAt(k)+.6f,CutAt(k)+1.4f);l.Add(at-new Vector3(0,.55f,0),Lamp*(2.2f*lit),5.5f);}
            l.Add(new Vector3(2.1f,2.4f,-1.5f),Magenta*1.6f,6);l.Add(new Vector3(-2.1f,2.4f,1.5f),Teal*1.4f,6);
            float o=Out(t);if(o>.01f){l.Add(Side*(Spread*o)+new Vector3(0,1.2f,0),Teal*(1.2f*o),3.5f);l.Add(-Side*(Spread*o)+new Vector3(0,1.2f,0),Magenta*(1.2f*o),3.5f);}
            float join=Flash(t,ConvergeAt+.35f,.02f,.3f);if(join>.01f)l.Add(new Vector3(0,1.2f,.4f),(White*2+Mint)*(4*join),6);
        }

        // ---- the effects ----
        private readonly Vector3[] tips=new Vector3[10];
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY;
            // Steam rising off the grates.
            for(int g=0;g<4;g++){float z=g==0?6.4f:g==1?1.2f:g==2?-4.6f:-9.4f;for(int i=0;i<(low?2:4);i++){float u=((t*.35f+i/4f+g*.21f)%1f);var p=U(.6f*(float)Math.Sin(i*2.1f+g),.1f+1.8f*u,z+.2f*(float)Math.Cos(i+t*.3f));fx.Smoke(p,Steam,.16f*(1-u),.3f+.7f*u,u,40500+g*8+i,floor);}}
            // The daggers' trails: the body's mint, each echo's in its own colour, a moment behind.
            float o=Out(t);
            bool cutting=t>=CutAAt+.25f&&t<CutAAt+CutLength||t>=CutCAt+.2f&&t<CutCAt+CutLength;
            if(cutting)
            {
                Echo(fx,t,0,0,Vector3.Zero,Mint*1.6f,White,.014f,40600,low);Echo(fx,t,0,1,Vector3.Zero,Mint*1.6f,White,.014f,40640,low);
                if(o>.01f){Echo(fx,t,LagL,0,Side*(Spread*o),Teal*1.4f,Teal,.012f,40700,low);Echo(fx,t,LagR,0,-Side*(Spread*o),Magenta*1.4f,Magenta,.012f,40760,low);}
            }
            else if(o>.01f&&t>=CutAAt+.25f&&t<CutCAt+CutLength+LagR+.2f)
            {
                // (the echoes still finishing their cuts after the body)
                Echo(fx,t,LagL,0,Side*(Spread*o),Teal*1.4f,Teal,.012f,40700,low);Echo(fx,t,LagR,0,-Side*(Spread*o),Magenta*1.4f,Magenta,.012f,40760,low);
            }
            // Each cut lantern: the echo's edge thrown up through its cord as a thin crescent of light, then sparks off the falling paper.
            for(int k=1;k<=3;k++)
            {
                float s=t-CutAt(k);if(s<-.05f||s>1.4f)continue;var r=Lanterns[k];var cord=U(r.X,r.Y-.05f,r.Z);
                bool left=k==2;var from=Anchor(NativeEntranceAnchor.RightTip,t-(left?LagL:LagR))+(left?Side:-Side)*(Spread*o);
                if(s>=-.05f&&s<.25f){float a=1-NativeEntranceMath.Clamp01(s/.25f);Line(fx,cord,cord-from,(left?Teal:Magenta)*(2*a),.02f,.5f,0,40800+k);}
                Lantern(k,t,out var at,out _);if(s>=0&&s<1.2f)NativeEntranceFx.Sparks(fx,t,CutAt(k),at-new Vector3(0,.4f,0),Vector3.UnitY,1,low?4:10,1.6f,.6f,6,.5f,Lamp*2,Lamp,.008f,40820+k*20,q,floor);
            }
            // The convergence: the echoes folding back into the body in a flash of both colours.
            float a2=t-ConvergeAt-.35f;
            if(a2>=0&&a2<.4f){float u=a2/.4f;fx.Glow(new Vector3(0,1.2f,.1f),(White+Mint)*(2*(1-u)),0,.4f+.8f*u,1,u,40900);for(int i=0;i<(low?8:20);i++){float ang=i*.314f;var d=new Vector3((float)Math.Cos(ang),.3f*(float)Math.Sin(ang*2),(float)Math.Sin(ang));fx.Streak(new Vector3(0,1.2f,.1f)+d*(.3f+1.2f*u),d*4,(i%2==0?Teal:Magenta)*(1.4f*(1-u)),.01f,.05f,.04f,.5f,u,40910+i);}}
        }
        /// <summary>A dagger's trail behind the body or an echo (`lag` s behind, displaced by `offset`): the captured tip's path over the
        /// last fifth of a second, its segments laid onto the camera's plane.</summary>
        private void Echo(INativeEntranceFx fx,float t,float lag,int hand,Vector3 offset,Vector3 head,Vector3 tail,float width,int seed,bool low)
        {
            int n=low?6:tips.Length,c=0;var which=hand==0?NativeEntranceAnchor.RightTip:NativeEntranceAnchor.LeftTip;
            for(int i=0;i<n;i++){float s=t-lag-.2f*i/(n-1);if(s<0)break;tips[c++]=Anchor(which,s)+offset;}
            for(int i=1;i<c;i++){float u=(float)i/c;var a=tips[i-1];var b=tips[i];if((a-b).LengthSquared()<1e-8f)continue;Line(fx,a,a-b,Vector3.Lerp(head,tail,u)*(1-u),width*(1-.6f*u),.2f,u,seed+i);}
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.afterimage.lane",null,"water",.25f,.6f),
            new NativeIntroCue(StopAt,"intro2.afterimage.stop","move.step.stone","wood",.4f),
            new NativeIntroCue(SplitAt,"intro2.afterimage.split","magic.blink-strike","dash",.7f),
            new NativeIntroCue(CutAAt+HitA-.08f,"intro2.afterimage.cut","melee.daggers.swing","daggers",.85f),
            new NativeIntroCue(CutAAt+HitA+LagL-.06f,"intro2.afterimage.echo","melee.daggers.swing","daggers",.6f,1.15f),
            new NativeIntroCue(CutAAt+HitA+LagR-.04f,"intro2.afterimage.echo2","melee.daggers.swing","daggers",.5f,1.3f),
            new NativeIntroCue(CutAAt+HitA+LagL+.75f,"intro2.afterimage.lanterns","break.wood","wood",.6f,1.2f),
            new NativeIntroCue(CutCAt+HitC-.08f,"intro2.afterimage.cut2","melee.daggers.swing","daggers",.85f,.9f),
            new NativeIntroCue(ConvergeAt+.35f,"intro2.afterimage.join","magic.blink-strike","dash",.8f,.8f),
            new NativeIntroCue(CardAt,"intro2.afterimage.name","topdog.fang-cut","daggers",.45f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.95f,1,.98f),Mint*1.2f);card.Style=NativeIntroCardStyle.Rise;NameBeside(ref card,true);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
