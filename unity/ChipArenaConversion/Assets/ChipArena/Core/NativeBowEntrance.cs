using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Arrowfall (weapon-bow, the Bow's signature entrance; presentation only, the match sees the style and its 4.8 s). The
    /// pilot reaches back to the quiver, nocks, turns side-on and draws toward the sky, holds the glinting full draw, looses
    /// the arrow straight up in slow motion; it splits overhead and the volley lands in a ring round the pilot, the arrows left
    /// standing in the floor under the name card. The arms are the Bow's own (NativeBowPose: the bladed torso, the push-pull
    /// draw on the arrow line, the nock path from the quiver, the follow-through) over keyed legs with planted feet.
    /// Beat marks (match age = song time, on the 0.4 s grid of the 150 bpm sting until the licensed intro.weapon-bow track
    /// arrives): .4 the hand at the quiver, .8 the arrow seats on the string, 1.0 the draw, 1.6 full draw (the glint), 2.0 the
    /// release (the hit; slow motion to 3.04), 2.4 the split overhead, 2.8 the volley lands (stragglers to 3.0), 3.2 the hero
    /// frame among the planted arrows, 4.8 the hand-off. The intro rig's bow (riser, limbs flexing, string, arrows, quiver) and
    /// the arrows in the air and the floor are drawn by the runtime from BowAt and Arrows.
    /// </summary>
    public sealed class NativeBowEntrance:NativeProceduralEntrance
    {
        public const string Id="weapon-bow";
        public const float NockFrom=.3f,NockTo=.82f,RaiseFrom=.5f,RaiseTo=.95f,DrawFrom=1.0f,Full=1.6f,Release=2.0f,SplitAge=2.4f,VolleyAge=2.8f,LowerFrom=2.6f,LowerTo=3.05f;
        /// <summary>The draw's pitch above the horizon (degrees) and the turn side-on from the hero facing (rad, to the pilot's left).</summary>
        public const float SkyPitch=42,Turn=1.05f;
        /// <summary>The sky arrow's speed (m/s, presentation clock), the volley's spawn height and fall time.</summary>
        public const float SkySpeed=60,Top=9.2f,FallSpan=.3f;
        public const int Volley=11,MaxArrows=Volley+1;
        public static readonly float DrawFacing=Facing+Turn;
        /// <summary>The volley's landing ages (match age; the cluster on the 2.8 beat, three stragglers to 3.0).</summary>
        public static readonly float[] VolleyAges={2.78f,2.8f,2.79f,2.81f,2.77f,2.8f,2.82f,2.79f,2.88f,2.94f,3.0f};
        public readonly float Split;private readonly float[] land=new float[Volley],speed=new float[Volley];private readonly Vector3[] spot=new Vector3[Volley],fall=new Vector3[Volley];
        private Vector3[] nocks,dirs;private Vector3 releaseNock,releaseDir;private bool planned;
        private readonly CapturedRig.Pose scratch=new CapturedRig.Pose();
        private NativeBowPose.Frame last;private NativeBow.View lastView;private float lastT=-100;
        private static readonly NativeBow.Read Read=MakeRead();
        private static NativeBow.Read MakeRead(){var r=NativeBow.Read.None;r.Bow=true;r.Seed=7;r.ReleaseDraw=1;r.DrawAt=DrawFrom;r.DrawTime=Full-DrawFrom;return r;}
        public NativeBowEntrance():base(Id,Release,0x8ff0a6)
        {
            SlowRate=.3f;SlowHold=.14f;SlowRecover=.3f;SlowCatchUp=.9f;
            Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f);
            // Side-on for the draw (the bow foot ahead along the aim), back to the hero stance after the volley.
            Steps=new[]{Step(true,.9f,1.12f,.2f,.15f,.3f,.05f,DrawFacing),Step(false,.96f,1.2f,-.17f,-.13f,-.55f,.05f,DrawFacing),Step(true,2.72f,3.0f,.16f,.07f,.1f,.05f),Step(false,2.84f,3.12f,-.18f,-.1f,-.42f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).J(Hd,.04f).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            // A glance back over the shoulder at the quiver as the hand goes up.
            k.Key(.45f,stand).Root(0,.88f,0,Facing).J(Hd,-.05f,-.4f,0).J(C,0,-.1f,0).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            k.Key(.84f,stand).Root(0,.88f,0,Facing+.12f).J(Hd,.02f,-.1f,0).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            k.Key(1.16f,stand).Root(0,.88f,0,DrawFacing).Drop(-.05f).J(RA,-.2f,0,-.2f).J(RE,-.5f).PassKey();
            k.Key(Full,stand).Root(0,.88f,0,DrawFacing).Drop(-.08f);
            k.Key(Release,stand).Root(0,.88f,0,DrawFacing).Drop(-.09f).ImpactKey();
            k.Key(2.25f,stand).Root(0,.88f,0,DrawFacing).Drop(-.06f).J(B,-.04f);
            k.Key(LowerFrom,stand).Root(0,.88f,0,DrawFacing).Drop(-.05f).J(RA,-.2f,0,-.25f).J(RE,-.6f);
            k.Key(LowerTo,stand).Root(0,.88f,0,Facing).Drop(-.02f).J(RA,-.25f,0,-.25f).J(RE,-1.0f);
            k.Key(NameAt,NativeEntranceKeys.Hero());Keys=k;
            // The volley: a ring round the pilot (1.2 .. 2.2 m), each arrow falling steeply outward from the split overhead.
            for(int i=0;i<Volley;i++)
            {
                float a=(i+.5f)/Volley*6.2831853f+NativeEntranceMath.Signed(4400,i)*.22f,r=1.2f+1.0f*NativeEntranceMath.Rand(4400,i+20),tilt=.2f+.22f*NativeEntranceMath.Rand(4400,i+40);
                var outward=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));spot[i]=new Vector3(outward.X*r,NativeEntranceStage.FloorY,outward.Z*r);
                fall[i]=Vector3.Normalize(outward*(float)Math.Tan(tilt)+new Vector3(NativeEntranceMath.Signed(4400,i+60)*.06f,-1,0));
                land[i]=Clock(VolleyAges[i]);speed[i]=(Top-NativeEntranceStage.FloorY)/(FallSpan*-fall[i].Y);
            }
            Split=Clock(SplitAge);
            Shots=new[]
            {
                // Establishing: behind the bow-side shoulder, the quiver in frame, craning down as the pylons wake.
                Orbit(0,NativeEntranceCut.Cut,40,36,-150,-122,3.4f,2.9f,1.55f,1.2f,V(0,1.15f,0),V(0,1.1f,0),0,1,face:.6f),
                // The nock: close over the string-hand shoulder, from the quiver's mouth down onto the string.
                Shot(.42f,NativeEntranceCut.Cut,38,34,V(-2.1f,1.7f,1.6f),V(-1.85f,1.6f,1.75f),V(-.12f,1.4f,-.08f),V(0,1.25f,.2f),1,1),
                // The draw: low on the side the chest turns to, the bow and the sky above it.
                Shot(DrawFrom,NativeEntranceCut.Cut,42,37,V(-2.3f,.55f,1.0f),V(-2.0f,.5f,1.3f),V(0,1.3f,.2f),V(0,1.5f,.35f),1,1),
                // Full draw: on the face at the anchor and the glinting arrowhead.
                Follow(Shot(Full,NativeEntranceCut.Cut,36,32,V(-1.9f,1.6f,2.3f),V(-1.7f,1.55f,2.1f),V(0,-.12f,.3f),V(0,-.1f,.35f),1,1),NativeEntranceAnchor.Head),
                // The release, slowed: low under the bow, looking up after the arrow.
                Orbit(Release,NativeEntranceCut.Cut,48,44,-82,-52,3.0f,2.8f,.45f,.6f,V(0,1.45f,.3f),V(0,1.8f,.5f),1,1),
                // The volley: high and wide, the ring of arrows falling round the pilot.
                Shot(Clock(2.55f),NativeEntranceCut.Cut,52,47,V(2.8f,3.6f,3.4f),V(2.4f,3.1f,3.0f),V(0,.3f,0),V(0,.5f,0),1,0),
                HeroShot(),
            };
        }
        /// <summary>The bow's frame at presentation time `t` (Core NativeBow.View): arriving empty, the nock from the quiver, the
        /// raise, the draw to full, the hold, the release's follow-through held through the watch, lowered to the carry.</summary>
        public static NativeBow.View ViewAt(float t)
        {
            var v=new NativeBow.View{Phase=NativeBow.Phase.Carry,Arrows=1};
            if(t<Release)
            {
                float nock=NativeEntranceMath.Clamp01((t-NockFrom)/(NockTo-NockFrom)),a0=NativeBow.Follow/NativeBow.Nock,u=a0+(1-a0)*nock;
                if(t>=NockFrom&&nock<1){v.Phase=NativeBow.Phase.Nock;v.Nocking=u;}v.Nocked=t>=NockFrom&&u>=.86f;
                v.Raised=NativeEntranceMath.Ramp(t,RaiseFrom,RaiseTo);
                if(t>=DrawFrom){float f=NativeEntranceMath.Clamp01((t-DrawFrom)/(Full-DrawFrom));v.Phase=f>=1?NativeBow.Phase.Hold:NativeBow.Phase.Draw;v.Age=t-DrawFrom;v.Meter=f;v.Pull=NativeBow.PullOf(f);v.Full=f>=1;v.Extend=v.Pull;}
                return v;
            }
            float age=t-Release,down=NativeEntranceMath.Ramp(t,LowerFrom,LowerTo);
            v.Phase=age<NativeBow.Follow?NativeBow.Phase.Release:NativeBow.Phase.Carry;v.Age=age;v.Nocked=false;v.Arrows=0;
            v.Follow=NativeEntranceMath.Smooth(age/(.4f*NativeBow.Follow))*(1-down);v.Buzz=age<NativeBow.Buzz?1-age/NativeBow.Buzz:0;
            v.Extend=1-NativeEntranceMath.Ramp(t,LowerFrom-.1f,LowerTo);v.Raised=1-down;
            return v;
        }
        /// <summary>The draw's aim (rig frame): ahead, pitched up to SkyPitch by full draw.</summary>
        public static Vector3 AimAt(float t)=>NativeBowPose.AimOf(0,SkyPitch*NativeEntranceMath.Ramp(t,RaiseFrom,Full));
        private static readonly int[] RightArm={RA,RE,RH};private readonly Quaternion[] keyed=new Quaternion[3];
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            var v=ViewAt(t);pose.World();for(int i=0;i<3;i++)keyed[i]=pose.Rotation[RightArm[i]];
            var aim=AimAt(t);NativeBowPose.Torso(pose,aim,v.Raised,0,0);
            var f=NativeBowPose.Arms(pose,Read,v,t,aim,NativeBowPose.ChestAnchor(pose),NativeBowPose.Start,v.Raised,NativeBowPose.CarryWrist,0,false);
            // The reach back to the quiver grows out of the keyed arm.
            float reach=NativeEntranceMath.Ramp(t,NockFrom-.02f,NockFrom+.14f);
            if(t<RaiseFrom&&reach<1){for(int i=0;i<3;i++)pose.Rotation[RightArm[i]]=Quaternion.Slerp(keyed[i],pose.Rotation[RightArm[i]],reach);pose.World();}
            last=f;lastView=v;lastT=t;
        }
        /// <summary>The intro rig's bow at presentation time `t` (what the runtime draws on the rig's hands; rig frame).</summary>
        public void BowAt(float t,out NativeBow.View view,out NativeBowPose.Frame frame)
        {
            if(lastT!=t)Pose(t,scratch,out _,out _);view=lastView;frame=last;
        }
        protected override void Plan()
        {
            // The arrow line in the world (120 Hz): its nock and direction, for the effects and the arrows in flight.
            int n=(int)(Duration*AnchorRate)+2;nocks=new Vector3[n];dirs=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)AnchorRate;Pose(t,scratch,out var root,out var rot);
                nocks[i]=root+Vector3.Transform(last.Nock,rot);dirs[i]=Vector3.Normalize(Vector3.Transform(Vector3.Transform(Vector3.UnitZ,last.BowRotation),rot));
            }
            Pose(Release-.001f,scratch,out var r0,out var q0);releaseNock=r0+Vector3.Transform(last.Nock,q0);releaseDir=Vector3.Normalize(Vector3.Transform(Vector3.Transform(Vector3.UnitZ,last.BowRotation),q0));
            planned=true;lastT=-100;
        }
        private void Planned(){if(!planned)Prepare();}
        private Vector3 Table(Vector3[] table,float t){float x=Math.Max(0,Math.Min(table.Length-1.001f,t*AnchorRate));int i=(int)x;return Vector3.Lerp(table[i],table[i+1],x-i);}
        /// <summary>The nocked arrow's nock and head (world) at `t`.</summary>
        public Vector3 Nock(float t){Planned();return Table(nocks,t);}
        public Vector3 Head(float t){Planned();return Table(nocks,t)+Vector3.Normalize(Table(dirs,t))*NativeBow.ArrowLength;}
        /// <summary>The sky arrow's nock (world) `t` after the release.</summary>
        public Vector3 SkyArrow(float t){Planned();return releaseNock+releaseDir*(SkySpeed*Math.Max(0,t-Release));}
        public Vector3 SkyDirection{get{Planned();return releaseDir;}}
        /// <summary>Volley arrow `i`'s landing point (world, on the floor), its fall direction and landing time (presentation).</summary>
        public Vector3 Spot(int i)=>spot[i];public Vector3 Fall(int i)=>fall[i];public float Land(int i)=>land[i];
        /// <summary>
        /// The loose arrows at `t` (world, source frame; each the arrow model's nock and rotation): the sky arrow until it splits,
        /// the volley falling and then standing in the floor (a short quiver as each lands) to the end. Returns the count.
        /// </summary>
        public int Arrows(float t,Span<NativeBowPiece> into)
        {
            Planned();int n=0;
            if(t>=Release&&t<Split&&n<into.Length)into[n++]=Piece(SkyArrow(t),releaseDir,0,0);
            for(int i=0;i<Volley&&n<into.Length;i++)
            {
                float s=t-(land[i]-FallSpan);if(s<0)continue;
                Vector3 tip=t<land[i]?spot[i]-fall[i]*(speed[i]*(land[i]-t)):spot[i]+fall[i]*NativeBowStuck.Bury;
                float after=t-land[i],wobble=after>0?.07f*(float)Math.Exp(-after*9)*(float)Math.Sin(after*48):0;
                into[n++]=Piece(tip-fall[i]*NativeBow.ArrowLength,fall[i],wobble,i);
            }
            return n;
        }
        private static NativeBowPiece Piece(Vector3 nock,Vector3 dir,float wobble,int seed)
        {
            var q=CapturedRig.FromTo(Vector3.UnitZ,dir)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,seed*1.3f);if(wobble!=0)q=q*Quaternion.CreateFromAxisAngle(Vector3.UnitX,wobble);
            return new NativeBowPiece{Position=nock,Rotation=q,Scale=Vector3.One};
        }
        public override void Props(float t,INativeEntranceProps props,float q)=>Stage(props,t,.55f+.45f*NativeEntranceMath.Ramp(t,.1f,.7f),Release);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Planned();Motes(fx,t,q,Accent);Beams(fx,t,q,Accent,.8f);var white=Vector3.One;var right=RightOf(t);
            // The arrow seats on the string: a spark on the nock.
            float seat=NativeEntranceMath.Pulse(t,NockTo-.05f,.02f,.15f);if(seat>.02f)NativeEntranceFx.Glint(fx,Nock(t),white*(2.2f*seat),.1f*seat+.03f,right,Vector3.UnitY,4500);
            if(t>DrawFrom&&t<Release)
            {
                // The draw: light gathering on the arrowhead and running along the shaft as the string comes back; motes drawn in.
                float pull=NativeBow.PullOf(NativeEntranceMath.Clamp01((t-DrawFrom)/(Full-DrawFrom)));var head=Head(t);var dir=Vector3.Normalize(head-Nock(t));
                float hold=t>Full?.85f+.15f*(float)Math.Sin((t-Full)*31):1;
                fx.Glow(head,Accent*((.5f+2.2f*pull*pull)*hold),0,.05f+.11f*pull,0,0,4510);fx.Glow(head,white*(1.4f*pull*pull),0,.03f+.03f*pull,1,0,4511);
                fx.Streak(head,dir*(NativeBow.ArrowLength*.8f*pull),Vector3.Lerp(Accent,white,.35f)*(1.1f*pull),.008f,0,1,.6f,0,4512);
                for(int i=0;i<14;i++)
                {
                    if(NativeEntranceMath.Rand(4520,i)>q)continue;float born=DrawFrom+.04f*i,a=t-born;if(a<0||a>.36f)continue;float u=a/.36f;
                    var from=head+NativeEntranceMath.Direction(4520,i)*.55f;fx.Glow(Vector3.Lerp(from,head,u*u),Accent*(.4f+1.2f*u),0,.03f,0,u,4520+i);
                }
            }
            // Full draw: the glint on the arrowhead.
            float glint=NativeEntranceMath.Pulse(t,Full,.02f,.24f);if(glint>.02f&&t<Release)NativeEntranceFx.Glint(fx,Head(t),white*(3.2f*glint),.45f*glint+.08f,right,Vector3.UnitY,4540);
            // The release: the string snaps, the arrow streaks into the sky on a fading trail.
            float snap=NativeEntranceMath.Pulse(t,Release,.01f,.12f);if(snap>.02f){fx.Glow(releaseNock,white*(3*snap),0,.22f*snap+.04f,1,0,4550);fx.Glow(releaseNock,Accent*(2*snap),0,.6f*snap,0,0,4551);}
            if(t>=Release&&t<Split+.06f)
            {
                var p=SkyArrow(Math.Min(t,Split));var head=p+releaseDir*NativeBow.ArrowLength;float fade=1-NativeEntranceMath.Ramp(t,Split,Split+.06f);
                fx.Streak(head,releaseDir*SkySpeed,(white*2.4f+Accent*1.4f)*fade,.03f,1.4f,.4f,.9f,0,4560);fx.Glow(head,Accent*(1.6f*fade),0,.22f,0,0,4561);
                float trail=Math.Min(SkySpeed*(t-Release),7);if(trail>.1f)fx.Streak(p,releaseDir*trail,Accent*(.7f*fade),.012f,0,1,.95f,0,4562);
            }
            // The split overhead: a star and the volley's streaks fanning out of it.
            var top=SkyArrow(Split)+releaseDir*NativeBow.ArrowLength;float burst=NativeEntranceMath.Pulse(t,Split,.01f,.3f);
            if(burst>.02f){NativeEntranceFx.Glint(fx,top,white*(4*burst),1.1f*burst+.2f,right,Vector3.UnitY,4570);fx.Glow(top,Accent*(3*burst),0,2.0f*burst,1,0,4571);}
            for(int i=0;i<Volley;i++)
            {
                float a=t-Split;if(a<0||a>.25f||NativeEntranceMath.Rand(4580,i)>Math.Max(.4f,q))continue;var d=Vector3.Normalize(spot[i]+new Vector3(0,.6f,0)-top);
                fx.Streak(top+d*(a*14),d*(4+a*10),Vector3.Lerp(white*2.5f,Accent*2,a*4)*(1-a/.25f),.02f,.5f,.3f,.9f,0,4580+i);
            }
            // The volley: each arrow's trail as it falls, the hit's dust, chips and a pop of light; then the fletchings glow,
            // pulsing out on the name card.
            for(int i=0;i<Volley;i++)
            {
                float l=land[i];int seed=4600+i*20;
                if(t>l-FallSpan&&t<l){var tip=spot[i]-fall[i]*(speed[i]*(l-t));fx.Streak(tip,fall[i]*speed[i],Vector3.Lerp(white*2.6f,Accent*2,.4f),.02f,.9f,.5f,.9f,0,seed);}
                float pop=NativeEntranceMath.Pulse(t,l,.01f,.1f);if(pop>.02f)fx.Glow(spot[i]+new Vector3(0,.05f,0),white*(2.2f*pop),0,.28f*pop+.05f,1,0,seed+1);
                NativeEntranceFx.GroundDust(fx,t,l,spot[i],5,.04f,1.0f,.7f,NativeEntranceFx.Hex(0x8790a2),.3f,.18f,seed+2,q,NativeEntranceStage.FloorY);
                NativeEntranceFx.Sparks(fx,t,l,spot[i]+new Vector3(0,.03f,0),Vector3.UnitY-fall[i]*.4f,.9f,5,3f,.35f,9.8f,1.4f,white*2.5f,Accent*2,.015f,seed+10,q,NativeEntranceStage.FloorY);
                if(t>l){var nock=spot[i]+fall[i]*(NativeBowStuck.Bury-NativeBow.ArrowLength);float g=.22f+.9f*NativeEntranceMath.Pulse(t,NameAt+.03f*i,.03f,.35f);fx.Glow(nock,Accent*g,0,.07f,0,0,seed+19);}
            }
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);
            if(t>DrawFrom&&t<Release){float pull=NativeBow.PullOf(NativeEntranceMath.Clamp01((t-DrawFrom)/(Full-DrawFrom)));l.Add(Head(t),Accent*(3.5f*pull*pull),2.5f);}
            float f=NativeEntranceMath.Pulse(t,Release,.01f,.22f);if(f>.01f)l.Add(releaseNock+new Vector3(0,.25f,.2f),NativeEntranceFx.Hex(0xfff0d0,30*f),6);
            float hit=0;for(int i=0;i<Volley;i++)hit+=NativeEntranceMath.Pulse(t,land[i],.01f,.18f);if(hit>.01f)l.Add(new Vector3(0,.7f,0),Accent*(3*hit),6);
        }
    }
}
