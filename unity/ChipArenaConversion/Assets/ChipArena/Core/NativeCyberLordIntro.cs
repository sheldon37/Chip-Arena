using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "DEV · Hardlight Boot", the Cyber Lord's DEV entrance (style dev-cyberlord, the class's own signature: a Cyber Lord pilot
    /// plays it, as L33T H4x plays dev-matrix; the Locker lists it only with DEV access). Directed (NativeEntranceDirector) on the
    /// standard 4.8 s with the name card at 3.2 s, so match timing is unchanged; the sting is the shipped pulse-wave bed (150
    /// bpm, a .4 s beat grid with its accent at 3.2 s). dev-* styles get no music key by contract (NativeSoundBank: no
    /// intro.dev- key). Beats: a scan of light builds the pilot from the shins up (0-.6 s), the hardlight gauntlets form edge
    /// by edge from the knuckles (FormAt), a jab and a cross that flare the fists on the beat (1.2, 1.6 s), the Pulse Palm
    /// thrust at the camera with its hex cone (2.0 s, slowed, caught up before the card), then the bladed stance settling
    /// under the name card on the accent.
    /// The upper body is the pack's unarmed takes (CyberLordMotion.Upper on a scripted view), the legs and root keyed and
    /// planted. Reduced effects keep every beat with fewer particles. Presentation only; pure functions of the clock.
    /// </summary>
    public sealed class NativeCyberLordIntroScript:NativeProceduralEntrance
    {
        public const float ScanTo=.6f,FormAt=.55f,JabHit=1.2f,CrossHit=1.6f,PalmHit=2.0f;
        public static readonly float JabAt=JabHit-CyberLordSpec.ComboDurations[0]*CyberLordSpec.ComboHitShare[0],CrossAt=CrossHit-CyberLordSpec.ComboDurations[1]*CyberLordSpec.ComboHitShare[1];
        public static readonly float PalmAt=PalmHit-CyberLordSpec.ComboDurations[2]*CyberLordSpec.ComboHitShare[2];
        private readonly CyberLordMotion motion=new CyberLordMotion();private readonly CyberLordView view=new CyberLordView();private readonly MeleeRig upper=new MeleeRig();
        private static readonly int[] UpperBones={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHand,CapturedRig.RHand};
        public NativeCyberLordIntroScript():base(CyberLordSpec.Entrance,PalmHit,NativeCyberLordLook.Cyan)
        {
            SlowRate=.3f;SlowHold=.12f;SlowRecover=.28f;SlowCatchUp=.9f;
            Start=Stance(.17f,.12f,-.18f,-.15f,-.18f,-.55f);
            Steps=new[]{Step(true,1.3f,1.42f,.2f,.17f,-.18f,.05f),Step(true,1.72f,1.9f,.21f,.22f,-.15f,.06f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Drop(-.04f);
            k.Key(.9f,stand).Root(0,.88f,0,Facing).Drop(-.07f);
            k.Key(1.75f,stand).Root(0,.88f,0,Facing).Drop(-.08f);
            k.Key(PalmHit,stand).Root(0,.88f,.06f,Facing).Drop(-.12f).ImpactKey();
            k.Key(NameAt+.4f,stand).Root(0,.88f,.04f,Facing).Drop(-.07f);
            k.Key(Duration,stand).Root(0,.88f,.04f,Facing).Drop(-.06f);Keys=k;
            Shots=new[]
            {
                // The build: low and wide as the scan climbs the body.
                Shot(0,NativeEntranceCut.Cut,40,36,V(1.6f,.35f,3.6f),V(1.2f,.7f,3.0f),V(0,.6f,0),V(0,1.0f,0),1,0),
                // The gauntlets forming: close on the lead forearm.
                Follow(Shot(FormAt-.1f,NativeEntranceCut.Cut,40,34,V(1.55f,.35f,1.8f),V(1.3f,.36f,1.5f),V(0,.05f,0),V(0,.06f,0),1,0),NativeEntranceAnchor.LeftHand),
                // Shadowboxing: a medium front three-quarter, the fists flaring toward the lens.
                Orbit(JabAt-.15f,NativeEntranceCut.Cut,42,40,28,14,2.9f,2.6f,1.2f,1.25f,V(0,1.15f,0),V(0,1.15f,0),1,0),
                // The Pulse Palm: straight down its line, pushing in to the accent.
                Shot(PalmAt-.25f,NativeEntranceCut.Cut,46,38,V(.15f,1.25f,2.9f),V(.1f,1.22f,2.2f),V(0,1.15f,0),V(0,1.18f,0),1,0),
                // The settle: a slow orbit round to the hero as the stance comes back up.
                Orbit(PalmHit+.55f,NativeEntranceCut.Cut,40,37,-24,-8,3.2f,3.4f,1.1f,.8f,V(0,1.1f,0),V(0,1.02f,0),1,0),
                HeroShot(),
            };
        }
        // The scripted view: the combo swings at their beats (the arena's own windows, so the poses are the gameplay's).
        private void Script(float t)
        {
            view.Clear();view.Id=-1;view.Alive=true;
            if(t>=PalmAt){view.ComboStep=2;view.ComboAt=PalmAt;view.ComboUntil=PalmAt+CyberLordSpec.ComboDurations[2];}
            else if(t>=CrossAt){view.ComboStep=1;view.ComboAt=CrossAt;view.ComboUntil=CrossAt+CyberLordSpec.ComboDurations[1];}
            else if(t>=JabAt){view.ComboStep=0;view.ComboAt=JabAt;view.ComboUntil=JabAt+CyberLordSpec.ComboDurations[0];}
            if(view.ComboAt>-90)view.ComboHit=view.ComboAt+(view.ComboUntil-view.ComboAt)*CyberLordSpec.ComboHitShare[view.ComboStep];
        }
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            base.Pose(t,pose,out root,out rotation);Script(t);motion.Upper(view,t,upper);
            for(int i=0;i<UpperBones.Length;i++)pose.Rotation[UpperBones[i]]=upper.Q[UpperBones[i]];
            // The capture's hips height eases the keyed body only a little (the legs stay keyed and planted).
            pose.Position[CapturedRig.Body].Y+=(upper.P[CapturedRig.Body].Y-CapturedRig.Bones[CapturedRig.Body].Position.Y)*.25f;
        }
        /// <summary>The fists' flare at each punch's hit (NativeCyberLordLook.Pieces on the entrance clock).</summary>
        public NativeCyberLordLook.Flare Flare(float t)
        {
            const float jab=JabHit,cross=CrossHit;
            if(t>=PalmHit)return NativeCyberLordLook.FlareAt(t,PalmHit,2,PalmHit+.2f);if(t>=cross)return NativeCyberLordLook.FlareAt(t,cross,1,cross+.3f);return NativeCyberLordLook.FlareAt(t,jab,0,jab+.3f);
        }
        /// <summary>The scan of light: each bone appears as the scan line passes its height (from the feet up, ScanTo s).</summary>
        public override float Solid(float t,int bone)
        {
            if(t>=ScanTo)return 1;float y=bone>=CapturedRig.LHip?(bone%4==2||bone%4==1?.1f:.45f):bone==CapturedRig.Head?1.7f:bone==CapturedRig.Neck?1.55f:bone>=CapturedRig.LClav?1.3f:bone==CapturedRig.Chest?1.25f:bone==CapturedRig.Spine?1.0f:.85f;
            return NativeEntranceMath.Ramp(.3f+1.6f*t/ScanTo,y-.1f,y+.05f);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            NativeEntranceStage.Rig(l,1,Accent,1,1);var chest=Anchor(NativeEntranceAnchor.Chest,t);
            float flash=NativeEntranceMath.Pulse(t,PalmHit,.01f,.3f)+.35f*NativeEntranceMath.Pulse(t,NameAt,.02f,.4f);l.Add(chest+new Vector3(0,0,.4f),Accent*(1.1f+3*flash),3.2f);
            l.Add(Anchor(NativeEntranceAnchor.LeftHand,t),Accent*.7f,1.6f);l.Add(Anchor(NativeEntranceAnchor.RightHand,t),NativeEntranceFx.Hex(NativeCyberLordLook.Magenta,.6f),1.4f);
        }
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,.45f+.55f*NativeEntranceMath.Ramp(t,0,ScanTo),PalmHit,NativeEntranceFx.Hex(NativeCyberLordLook.Cyan));
            // The scan line: a ring of light climbing the body from the shins as it is built (never on the floor).
            if(t<ScanTo+.1f){float y=NativeEntranceStage.FloorY+.3f+1.6f*Math.Min(1,t/ScanTo);int n=q<.5f?12:24;
                for(int i=0;i<n;i++){float a0=i*MeleeMath.TAU/n;var p=new Vector3((float)Math.Cos(a0)*.55f,y,(float)Math.Sin(a0)*.55f);props.Piece(WeaponShape.Cube,true,p,Quaternion.CreateFromAxisAngle(Vector3.UnitY,-a0),new Vector3(.02f,.012f,.16f),NativeEntranceFx.Hex(NativeCyberLordLook.Cyan,2.4f));}}
            // The Pulse Palm's hex rings driving out of the palm at the lens.
            float pa=t-PalmHit;if(pa>=0&&pa<.45f)
            {
                var palm=(Anchor(NativeEntranceAnchor.LeftHand,t)+Anchor(NativeEntranceAnchor.RightHand,t))*.5f;var f=ForwardOf(t);var r=RightOf(t);
                for(int k=0;k<3;k++){float d=1.6f*Math.Max(0,Math.Min(1,pa/.3f-k*.18f));if(d<=0)continue;float rad=.18f+d*.55f,g=(1-pa/.45f)*(1-k*.25f);var c=palm+f*d;
                    for(int i=0;i<6;i++){float a0=i*(float)Math.PI/3,a1=a0+(float)Math.PI/3;var p0=c+(r*(float)Math.Cos(a0)+Vector3.UnitY*(float)Math.Sin(a0))*rad;var p1=c+(r*(float)Math.Cos(a1)+Vector3.UnitY*(float)Math.Sin(a1))*rad;
                        var along=p1-p0;props.Piece(WeaponShape.Cube,true,(p0+p1)*.5f,Quaternion.CreateFromAxisAngle(f,(float)Math.Atan2(along.Y,Vector3.Dot(along,r)))*NativeEntranceMath.Yaw(Heading(t)),new Vector3(along.Length(),.02f,.02f),NativeEntranceFx.Hex(k==0?NativeCyberLordLook.Core:NativeCyberLordLook.Cyan,2.6f*g));}}
            }
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);
            // The fists' hits: a white flash and sparks at each punch's reach.
            const float jab=JabHit,cross=CrossHit;
            Spark(fx,t,jab,NativeEntranceAnchor.LeftHand,7100,q);Spark(fx,t,cross,NativeEntranceAnchor.RightHand,7200,q);
            float pa=t-PalmHit;if(pa>=0&&pa<.5f){var palm=(Anchor(NativeEntranceAnchor.LeftHand,t)+Anchor(NativeEntranceAnchor.RightHand,t))*.5f;float k=1-pa/.5f;fx.Glow(palm,NativeEntranceFx.Hex(NativeCyberLordLook.Core,3*k*k),0,.6f,1,pa,7300);fx.Glow(palm+ForwardOf(t)*.6f,NativeEntranceFx.Hex(NativeCyberLordLook.Cyan,1.2f*k),0,1.4f,2,pa,7301);}
            // The gauntlets forming: motes pulled into the forearms.
            float form=t-FormAt;if(form>=0&&form<.6f)for(int s=0;s<2;s++){var h=Anchor(s==0?NativeEntranceAnchor.LeftHand:NativeEntranceAnchor.RightHand,t);var e=Anchor(s==0?NativeEntranceAnchor.LeftElbow:NativeEntranceAnchor.RightElbow,t);int n=q<.5f?4:9;
                for(int i=0;i<n;i++){float u=(form*2.4f+i/(float)n)%1f;var p=Vector3.Lerp(h,e,(i%3)/2f)+new Vector3((float)Math.Sin(i*2.1f),(float)Math.Cos(i*1.7f),(float)Math.Sin(i*3.3f))*(.35f*(1-u));fx.Glow(p,NativeEntranceFx.Hex(NativeCyberLordLook.Cyan,1.6f*u),0,.05f,0,u,7400+i+s*20);}}
        }
        private void Spark(INativeEntranceFx fx,float t,float at,NativeEntranceAnchor hand,int seed,float q)
        {
            float a=t-at;if(a<0||a>.3f)return;var h=Anchor(hand,t)+ForwardOf(t)*.12f;float k=1-a/.3f;fx.Glow(h,NativeEntranceFx.Hex(NativeCyberLordLook.Core,3.2f*k*k),0,.32f,1,a,seed);
            int n=q<.5f?4:9;for(int i=0;i<n;i++){var d=Vector3.Normalize(new Vector3((float)Math.Sin(i*2.3f+seed),(float)Math.Cos(i*1.3f)*.6f,(float)Math.Sin(i*1.7f)));fx.Streak(h+d*(a*5),d*7,NativeEntranceFx.Hex(i%3==0?NativeCyberLordLook.Magenta:NativeCyberLordLook.Cyan,2.4f*k),.02f,.1f,.03f,.9f,a/.3f,seed+i);}
        }
    }
}
