using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Lightfall", the bow's v2 intro (I4, owner 2026-11-20: replaces Arrowfall; docs/INTROS-RANGED.md). 8.0 s, the name at 6.2 s.
    /// An old shrine in a forest clearing under a full moon: the camera sinks through the canopy with the falling leaves onto the
    /// pilot on a mossy stone terrace among six dark stone lanterns, fireflies drifting. The pilot draws an arrow from the quiver and
    /// nocks it, raises the bow to the sky and draws; the fireflies spiral in and gather on the arrowhead, the glyphs on the stone gate
    /// wake, the leaves stop in the air. The release, slowed: a bolt of light climbs into the night, blooms and splits, and a volley of
    /// light arrows rains down round the shrine, each landing in a burst of petals of light, lighting the lanterns one by one. The bow
    /// comes down; the moon behind, the arrows standing in the moss, the name.
    /// The bow's string, arrow and quiver are the match's (INativeIntroBow: Runtime draws them from the view given here); the arms are
    /// the Bow's own (NativeBowPose), as the old Arrowfall's were. Presentation only: pure functions of the presentation clock.
    /// </summary>
    public sealed class NativeIntroLightfall:NativeRangedIntro,INativeIntroBow
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"bow";
        public const float Total=8.0f,CardAt=6.2f;
        public const float NockFrom=1.15f,NockTo=1.66f,RaiseFrom=1.4f,RaiseTo=1.9f,DrawFrom=1.95f,Full=2.75f,Release=3.25f,Bloom=3.72f,RainFrom=4.12f,LowerFrom=4.9f,LowerTo=5.45f;
        public const float SkyPitch=50,SkySpeed=58;public const int Volley=24;
        private static readonly Vector3 Gold=NativeEntranceFx.Hex(0xffd88a),White=new Vector3(1,1,1),Moonlight=NativeEntranceFx.Hex(0x9ab8ff),Firefly=NativeEntranceFx.Hex(0xc8ff8a),Mist=NativeEntranceFx.Hex(0x8a9ab8);
        /// <summary>The moon (ranged_lightfall.py MOON: behind the pilot, up to the left), the six lanterns' fire boxes.</summary>
        private static readonly Vector3 Moon=Vector3.Normalize(SDir(.40f,.39f,-.83f));
        private static readonly Vector3[] Fires=BuildFires();
        private static Vector3[] BuildFires(){float[] deg={35,90,145,215,270,325};var f=new Vector3[6];for(int i=0;i<6;i++){float a=deg[i]*(float)Math.PI/180;f[i]=S((float)Math.Sin(a)*4.5f,-.45f+1.42f,(float)Math.Cos(a)*4.5f);}return f;}
        private static NativeBow.Read MakeRead(){var r=NativeBow.Read.None;r.Bow=true;r.Seed=11;r.ReleaseDraw=1;r.DrawAt=DrawFrom;r.DrawTime=Full-DrawFrom;return r;}
        private static readonly NativeBow.Read Read=MakeRead();
        // the volley: where each arrow lands (source frame), its fall direction and when (presentation)
        private readonly Vector3[] spot=new Vector3[Volley],fall=new Vector3[Volley];private readonly float[] land=new float[Volley];private readonly int[] lights=new int[6];
        private readonly Vector3[] at=new Vector3[40];private readonly Quaternion[] turn=new Quaternion[40];
        private Vector3 releaseNock,releaseDir;private bool planned;

        public NativeIntroLightfall():base(Id,Total,CardAt,Release,0xffd88a)
        {
            float drawYaw=Facing+1.05f;
            Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f);
            Steps=new[]{Step(true,1.75f,1.98f,.2f,.15f,.3f,.05f,drawYaw),Step(false,1.82f,2.06f,-.17f,-.13f,-.55f,.05f,drawYaw),Step(true,LowerFrom+.2f,LowerFrom+.5f,.16f,.07f,.1f,.05f),Step(false,LowerFrom+.32f,LowerFrom+.62f,-.18f,-.1f,-.42f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).J(Hd,.1f).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            k.Key(1.0f,stand).Root(0,.88f,0,Facing).J(Hd,-.06f,.05f,0).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            k.Key(NockFrom+.25f,stand).Root(0,.88f,0,Facing).J(Hd,-.05f,-.4f,0).J(C,0,-.1f,0).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            k.Key(NockTo,stand).Root(0,.88f,0,Facing+.15f).J(Hd,.0f,-.1f,0).J(RA,-.12f,0,-.16f).J(RE,-.3f);
            k.Key(RaiseTo+.1f,stand).Root(0,.88f,0,drawYaw).Drop(-.05f).PassKey();
            k.Key(Full,stand).Root(0,.88f,0,drawYaw).Drop(-.09f);
            k.Key(Release,stand).Root(0,.88f,0,drawYaw).Drop(-.1f).ImpactKey();
            k.Key(Release+.35f,stand).Root(0,.88f,0,drawYaw).Drop(-.07f).J(B,-.04f);
            k.Key(LowerFrom,stand).Root(0,.88f,0,drawYaw).Drop(-.05f).J(RA,-.2f,0,-.25f).J(RE,-.6f);
            k.Key(LowerTo,stand).Root(0,.88f,0,Facing).Drop(-.02f).J(RA,-.16f,0,-.22f).J(RE,-.45f);
            k.Key(Total,NativeEntranceKeys.Hero());Keys=k;
            // the volley: sixteen arrows in a ring on the terrace, eight out by the lanterns (one beside each lantern it lights)
            for(int i=0;i<Volley;i++)
            {
                float a,r;if(i<16){a=(i+.5f)/16*6.2831853f+NativeEntranceMath.Signed(4800,i)*.15f;r=2.25f+.7f*NativeEntranceMath.Rand(4801,i);}
                else{int l=(i-16)%6;float[] deg={35,90,145,215,270,325};a=deg[l]*(float)Math.PI/180+(i>=22?.35f:-.2f);r=3.9f+.4f*NativeEntranceMath.Rand(4802,i);}
                var outward=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));float floor=i<16?Floor:-.45f;
                spot[i]=S(outward.X*r,floor,outward.Z*r);var o=SDir(outward.X,0,outward.Z);
                fall[i]=Vector3.Normalize(o*(.25f+.25f*NativeEntranceMath.Rand(4803,i))-Vector3.UnitY);
                land[i]=RainFrom+.04f*i+.12f*NativeEntranceMath.Rand(4804,i);
            }
            for(int l=0;l<6;l++){lights[l]=16+l;}
            // ---- the camera ----
            var sky=L(0,24,20);
            Rig.Spline(0,new[]{L(2.6f,11.5f,-6.8f),L(1.9f,6.8f,-4.8f),L(1.25f,2.5f,-3.45f)},L(0,1.0f,2),L(0,1.25f,0),55,42).Ease(NativeIntroEase.InOut).Focus(0,0,.3f)
               .Dolly(NockFrom-.03f,L(-.78f,1.76f,-.92f),L(-.66f,1.71f,-.78f),L(-.1f,1.42f,-.1f),L(.02f,1.38f,.18f),34,30).Focus(0,0,.6f)
               .Orbit(DrawFrom-.1f,Vector3.Zero,-72,-22,2.65f,2.4f,.45f,.62f,new Vector3(0,1.4f,0),new Vector3(0,1.9f,.3f),40,38).Focus(0,0,.35f)
               .Hold(Full-.08f,L(-.62f,1.66f,.62f),L(-.05f,1.62f,.12f),26).Focus(0,0,.75f)
               .Dolly(Release-.03f,L(.62f,.42f,-.82f),L(.52f,.46f,-.7f),L(0,6,4),L(0,14,10),52,50).Focus(0,0,.1f)
               .Dolly(3.6f,L(3.3f,1.0f,-3.1f),L(3.1f,1.1f,-2.9f),sky,sky+new Vector3(0,-2,0),60,58).Focus(0,0,.05f)
               .Spline(RainFrom-.06f,new[]{L(5.6f,11.2f,-7.2f),L(5.0f,10.2f,-6.4f),L(4.5f,9.4f,-5.7f)},L(0,0,1.0f),L(0,.2f,.6f),56,54).Focus(0,0,.1f)
               .Dolly(LowerFrom,L(-2.25f,1.2f,1.2f),L(-2.05f,1.26f,1.42f),L(0,1.2f,.2f),L(0,1.22f,.2f),34,33).Focus(0,0,.45f)
               .Dolly(LowerTo+.2f,L(1.62f,.56f,3.45f),L(1.25f,.62f,2.95f),L(0,1.18f,0),L(0,1.22f,0),34,30).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,.55f).Focus(0,0,.45f)
               .Bars(0,1).Bars(CardAt-.35f,.55f).Black(0,1).Black(.1f,0);
            Ramps.Ramp(3.12f,.3f,1.0f,1.25f);
        }
        // ---- the bow (INativeIntroBow: the runtime draws it) and the arms on it ----
        /// <summary>The bow's frame at `t`: carried, the nock from the quiver, the raise, the draw to full, the hold, the release's
        /// follow-through, lowered to the carry.</summary>
        public static NativeBow.View ViewAt(float t)
        {
            var v=new NativeBow.View{Phase=NativeBow.Phase.Carry,Arrows=1};
            if(t<Release)
            {
                float nock=NativeEntranceMath.Clamp01((t-NockFrom)/(NockTo-NockFrom)),a0=NativeBow.Follow/NativeBow.Nock,u=a0+(1-a0)*nock;
                if(t>=NockFrom&&nock<1){v.Phase=NativeBow.Phase.Nock;v.Nocking=u;}v.Nocked=t>=NockFrom&&u>=.86f;v.Arrows=v.Nocked?1:0;
                v.Raised=NativeEntranceMath.Ramp(t,RaiseFrom,RaiseTo);
                if(t>=DrawFrom){float f=NativeEntranceMath.Clamp01((t-DrawFrom)/(Full-DrawFrom));v.Phase=f>=1?NativeBow.Phase.Hold:NativeBow.Phase.Draw;v.Age=t-DrawFrom;v.Meter=f;v.Pull=NativeBow.PullOf(f);v.Full=f>=1;v.Extend=v.Pull;}
                return v;
            }
            float age=t-Release,down=NativeEntranceMath.Ramp(t,LowerFrom,LowerTo);
            v.Phase=age<NativeBow.Follow?NativeBow.Phase.Release:NativeBow.Phase.Carry;v.Age=age;v.Nocked=false;v.Arrows=0;
            v.Follow=NativeEntranceMath.Smooth(age/(.4f*NativeBow.Follow))*(1-down);v.Buzz=age<NativeBow.Buzz?1-age/NativeBow.Buzz:0;
            v.Extend=1-NativeEntranceMath.Ramp(t,LowerFrom-.1f,LowerTo);v.Raised=1-down;return v;
        }
        public bool Bow(float t,out NativeBow.View view){view=ViewAt(t);return true;}
        public static Vector3 AimAt(float t)=>NativeBowPose.AimOf(0,SkyPitch*NativeEntranceMath.Ramp(t,RaiseFrom,Full));
        private static readonly int[] RightArm={RA,RE,RH};private readonly Quaternion[] keyed=new Quaternion[3];
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            var v=ViewAt(t);pose.World();for(int i=0;i<3;i++)keyed[i]=pose.Rotation[RightArm[i]];
            var aim=AimAt(t);NativeBowPose.Torso(pose,aim,v.Raised,0,0);
            NativeBowPose.Arms(pose,Read,v,t,aim,NativeBowPose.ChestAnchor(pose),NativeBowPose.Start,v.Raised,NativeBowPose.CarryWrist,0,false);
            float reach=NativeEntranceMath.Ramp(t,NockFrom-.02f,NockFrom+.14f);
            if(t<RaiseFrom&&reach<1){for(int i=0;i<3;i++)pose.Rotation[RightArm[i]]=Quaternion.Slerp(keyed[i],pose.Rotation[RightArm[i]],reach);pose.World();}
        }
        private void Planned()
        {
            // the arrow's line at the release, from the anchors (the keyed rig's draw hand to its bow hand)
            if(planned)return;releaseNock=Anchor(NativeEntranceAnchor.RightHand,Release-.01f);releaseDir=Vector3.Normalize(Anchor(NativeEntranceAnchor.LeftHand,Release-.01f)-releaseNock);planned=true;
        }
        /// <summary>The nocked arrowhead (world) at `t` while drawn.</summary>
        private Vector3 Head(float t,out Vector3 dir){var nock=Anchor(NativeEntranceAnchor.RightHand,t);var bow=Anchor(NativeEntranceAnchor.LeftHand,t);var d=bow-nock;dir=d.LengthSquared()>1e-6f?Vector3.Normalize(d):Vector3.UnitY;return nock+dir*NativeBow.ArrowLength;}
        private Vector3 SkyArrow(float t){Planned();return releaseNock+releaseDir*(SkySpeed*Math.Max(0,Math.Min(t,Bloom)-Release));}
        private Vector3 BloomAt{get{Planned();return releaseNock+releaseDir*(SkySpeed*(Bloom-Release));}}
        /// <summary>Volley arrow `i`'s head at `t` while it flies (false before the bloom or once it has landed).</summary>
        private bool Flying(int i,float t,out Vector3 head,out Vector3 dir)
        {
            head=dir=default;float from=Bloom+.02f,to=land[i];if(t<from||t>=to)return false;float u=(t-from)/(to-from);
            var b=BloomAt;var outward=new Vector3(spot[i].X,0,spot[i].Z);if(outward.LengthSquared()<1e-6f)outward=Vector3.UnitZ;outward=Vector3.Normalize(outward);
            var c=b+outward*9+new Vector3(0,5,0);var e=spot[i]-fall[i]*.35f;
            var p0=Vector3.Lerp(b,c,u);var p1=Vector3.Lerp(c,e,u);head=Vector3.Lerp(p0,p1,u*u);var d=Vector3.Lerp(c-b,e-c,u*u);dir=d.LengthSquared()>1e-6f?Vector3.Normalize(d):-Vector3.UnitY;return true;
        }
        /// <summary>Lantern `l`'s fire 0..1: lit as the arrow beside it lands, then a slow flame's flicker (well under 3 Hz).</summary>
        private float Fire(int l,float t){float at2=land[lights[l]];if(t<at2)return 0;return NativeEntranceMath.Ramp(t,at2,at2+.35f)*(.85f+.15f*(float)Math.Sin(t*2.3f+l*1.7f));}

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("lightfall/grove"))return false;View(t,out var eye,out _);
            float bloom=Flash(t,Bloom,.03f,.5f);
            set.Piece("lightfall/sky",new Vector3(eye.X,0,eye.Z),Turn,1,Vector3.One,Vector3.One*(1+.5f*bloom));
            set.Piece("lightfall/grove",Vector3.Zero,Turn,1,Vector3.One,Vector3.One*(.6f+.9f*Glyphs(t)));
            for(int l=0;l<6;l++){float f=Fire(l,t);set.Piece("lightfall/fire",Fires[l],Turn,1,Vector3.One,Vector3.One*(.02f+f));}
            // the planted arrows of light (instanced), the petals of their landing, the leaves falling (stopped in the air through the hold)
            int n=0;
            for(int i=0;i<Volley&&n<at.Length;i++){if(t<land[i])continue;at[n]=spot[i]+fall[i]*.32f-fall[i]*NativeBow.ArrowLength;turn[n]=Quaternion.Normalize(CapturedRig.FromTo(-Vector3.UnitY,fall[i]));n++;}
            if(n>0)set.Batch("lightfall/arrow",new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),1,Vector3.One);
            n=0;float hold=LeafClock(t);
            for(int i=0;i<18&&n<at.Length;i++)
            {
                float life=5.5f,phase=(NativeEntranceMath.Rand(4900,i)*life+hold)%life;var top=L(NativeEntranceMath.Signed(4901,i)*5,8.5f,NativeEntranceMath.Signed(4902,i)*5+1);
                var p=top+new Vector3((float)Math.Sin(phase*1.7f+i)*.6f,-phase*1.6f,(float)Math.Cos(phase*1.3f+i)*.6f);if(p.Y<-.4f)continue;
                at[n]=p;turn[n]=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.3f,1,.2f)),phase*2.2f+i)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,.6f*(float)Math.Sin(phase*3+i));n++;
            }
            if(n>0)set.Batch("lightfall/leaf",new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),1.4f,Vector3.One);
            return true;
        }
        /// <summary>The leaves' own clock: drifting, nearly stopped through the full draw and the release's hang, then on again.</summary>
        private static float LeafClock(float t)
        {
            float a=Math.Min(t,Full),b=Math.Max(0,Math.Min(t,Release+.4f)-Full),c=Math.Max(0,t-(Release+.4f));return a+b*.08f+c;
        }
        /// <summary>The gate's glyphs waking with the draw, flaring at the release.</summary>
        private float Glyphs(float t)=>NativeEntranceMath.Ramp(t,DrawFrom,Full)*(1-.6f*NativeEntranceMath.Ramp(t,Release+.6f,LowerTo))+.6f*Flash(t,Release,.03f,.5f);
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Gold);v.Far=NativeEntranceFx.Hex(0x060a16);v.Floor=NativeEntranceFx.Hex(0x0e141c);v.Pool=Moonlight;v.Rim=Gold;return v;}

        // ---- the light: the moon behind, the clearing's blue dark, the gathering arrowhead, the bloom, the lanterns ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();var far=NativeEntranceFx.Hex(0x0c1424);l.Background=far;l.Fog=far;l.FogNear=10;l.FogFar=420;l.DriveProbe=true;
            float bloom=Flash(t,Bloom,.03f,.6f);
            l.Sky=NativeEntranceFx.Hex(0x3a4a7a,.32f+.6f*bloom);l.Ground=NativeEntranceFx.Hex(0x1a2418,.22f);
            l.Directional(Moon,Moonlight*(.85f));l.Directional(SDir(.3f,.6f,1),NativeEntranceFx.Hex(0x4a6aa0,.14f));
            var chest=Anchor(NativeEntranceAnchor.Chest,t);
            if(t>DrawFrom&&t<Release){float pull=NativeBow.PullOf(NativeEntranceMath.Clamp01((t-DrawFrom)/(Full-DrawFrom)));l.Add(Head(t,out _),Gold*(3.5f*pull*pull),3);}
            float rel=Flash(t,Release,.01f,.2f);if(rel>.01f){Planned();l.Add(releaseNock+new Vector3(0,.3f,0),NativeEntranceFx.Hex(0xfff0c8,16*rel),6);}
            if(bloom>.01f)l.Add(BloomAt,Gold*(60*bloom),45);
            float hit=0;for(int i=0;i<Volley;i++)hit+=Flash(t,land[i],.01f,.25f)*.25f;if(hit>.01f)l.Add(L(0,1.2f,0),Gold*(4*Math.Min(1.5f,hit)),9);
            for(int k=0;k<6;k++){float f=Fire(k,t);if(f>.01f)l.Add(Fires[k],NativeEntranceFx.Hex(0xffb070,1.5f*f),4.5f);}
            float card=NativeEntranceMath.Ramp(t,CardAt-.4f,CardAt+.4f);if(card>.01f)l.Add(chest+SDir(-1.4f,.5f,1.6f),Gold*(1.4f*card),6);
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            View(t,out var eye,out var fwd);float floor=Floor;
            // the moon's halo (in the camera's plane), mist hanging low over the moss, fireflies
            var moon=eye+Moon*160;fx.Glow(moon,NativeEntranceFx.Hex(0xdfe8ff,1.2f),0,9,1,0,4000);fx.Glow(moon,Moonlight*.35f,0,26,0,0,4001);
            int mist=q<.5f?6:14;for(int i=0;i<mist;i++){float a=i*.45f+t*.04f;var p=L((float)Math.Sin(a*2.1f+i)*7,-.2f+.25f*NativeEntranceMath.Rand(4010,i),(float)Math.Cos(a*1.7f+i)*7);fx.Smoke(p,Mist,.16f,2.6f+1.4f*NativeEntranceMath.Rand(4011,i),.5f,4010+i,-.5f);}
            float pull=t>DrawFrom&&t<Release?NativeBow.PullOf(NativeEntranceMath.Clamp01((t-DrawFrom)/(Full-DrawFrom))):0;
            Vector3 head=default,adir=default;if(t>DrawFrom-.4f&&t<Release)head=Head(t,out adir);
            int flies=q<.5f?14:30;
            for(int i=0;i<flies;i++)
            {
                var home=L(NativeEntranceMath.Signed(4100,i)*6,.4f+2.2f*NativeEntranceMath.Rand(4101,i),NativeEntranceMath.Signed(4102,i)*6)+new Vector3((float)Math.Sin(t*.7f+i)*.5f,(float)Math.Sin(t*.5f+i*2)*.3f,(float)Math.Cos(t*.6f+i)*.5f);
                // drawn into the arrowhead through the draw (each in its turn), gone into it by full draw
                float pullIn=t<Release&&t>DrawFrom-.4f?NativeEntranceMath.Ramp(t,DrawFrom-.3f+.025f*i,DrawFrom+.35f+.025f*i):0;
                if(t>=Release)continue;var p=pullIn>0?Vector3.Lerp(home,head,pullIn*pullIn):home;
                float blink=.5f+.5f*(float)Math.Sin(t*1.3f+i*2.1f);fx.Glow(p,Firefly*((.35f+.5f*blink)*(1-pullIn*.4f)),0,.03f,0,0,4100+i);
            }
            // the arrowhead gathering light, the shaft lit along its length; the full draw's star
            if(pull>0)
            {
                fx.Glow(head,Gold*(.6f+3.2f*pull*pull),0,.06f+.16f*pull,1,0,4200);fx.Glow(head,White*(1.6f*pull*pull),0,.03f+.04f*pull,1,0,4201);
                fx.Streak(head,adir*(NativeBow.ArrowLength*.85f*pull),Vector3.Lerp(Gold,White,.3f)*(1.3f*pull),.01f,0,1,.5f,0,4202);
                for(int i=0;i<(q<.5f?6:12);i++){float u=((t*1.4f+i/12f)%1f);var p=head+NativeEntranceMath.Direction(4210,i)*(.6f*(1-u));fx.Glow(p,Gold*(1.2f*u*pull),0,.02f,0,u,4210+i);}
                float star=Flash(t,Full,.04f,.3f);if(star>.02f)Star(fx,eye,fwd,head,NativeEntranceFx.Hex(0xfff2c0,3*star),.5f*star+.06f,.2f,4230);
            }
            // the release and the bolt of light climbing (laid on the camera plane: it is long), its trail
            float snap=Flash(t,Release,.01f,.12f);if(snap>.02f){Planned();fx.Glow(releaseNock,White*(3*snap),0,.25f,1,0,4300);}
            if(t>=Release&&t<Bloom+.05f)
            {
                var tip=SkyArrow(t)+releaseDir*NativeBow.ArrowLength;float trail=Math.Min(SkySpeed*(t-Release),9);
                fx.Glow(tip,White*2.4f,0,.16f,1,0,4310);fx.Glow(tip,Gold*1.6f,0,.6f,0,0,4311);
                Line(fx,eye,fwd,tip,tip-releaseDir*trail,Gold*1.8f,.035f,.9f,0,4312);
                for(int i=0;i<(q<.5f?5:10);i++){float v=i/10f*trail;fx.Glow(tip-releaseDir*v,Gold*(.4f*(1-i/10f)),0,.08f+.04f*i,0,0,4320+i);}
            }
            // the bloom: a flower of light opening high above, petals of light drifting from it
            float bl=Flash(t,Bloom,.03f,.55f);var b=BloomAt;
            if(bl>.02f){fx.Glow(b,White*(4*bl),0,1.6f,1,0,4400);fx.Glow(b,Gold*(2.2f*bl),0,6,0,0,4401);Star(fx,eye,fwd,b,NativeEntranceFx.Hex(0xfff0c0,3*bl),5*bl+.5f,.5f,4402);}
            if(t>=Bloom&&t<Bloom+2.6f){float a=t-Bloom;int n=q<.5f?12:28;for(int i=0;i<n;i++){var d=NativeEntranceMath.Direction(4410,i);var p=b+d*(6*(1-(float)Math.Exp(-a*1.8f)))+new Vector3((float)Math.Sin(a*2+i)*.4f,-1.2f*a,0);float u=a/2.6f;fx.Glow(p,Gold*(1.4f*(1-u)),0,.08f,0,u,4410+i);}}
            // the volley in flight: each a streak of light on its arc (laid on the camera plane)
            for(int i=0;i<Volley;i++)
            {
                if(!Flying(i,t,out var h,out var d))continue;Line(fx,eye,fwd,h,h-d*2.4f,Vector3.Lerp(Gold,White,.3f)*2.2f,.03f,.9f,0,4500+i);fx.Glow(h,Gold*1.8f,0,.18f,0,0,4530+i);
            }
            // landing: a burst of petals of light and a puff of glow; then the standing arrow's soft light
            for(int i=0;i<Volley;i++)
            {
                float l=land[i];int s=4600+i*10;
                NativeEntranceFx.Sparks(fx,t,l,spot[i]+new Vector3(0,.04f,0),Vector3.UnitY,.9f,q<.5f?4:8,2.4f,.6f,3,1.6f,White*2.4f,Gold*2,.012f,s,q,spot[i].Y);
                float pop=Flash(t,l,.01f,.15f);if(pop>.02f)fx.Glow(spot[i]+new Vector3(0,.12f,0),Gold*(2.4f*pop),0,.6f*pop+.1f,1,0,s+8);
                if(t>l)fx.Glow(spot[i]-fall[i]*.55f,Gold*(.35f+.25f*(float)Math.Sin(t*1.6f+i)),0,.12f,0,0,s+9);
            }
            // the lanterns' flames
            for(int k=0;k<6;k++){float f=Fire(k,t);if(f>.01f){fx.Glow(Fires[k],NativeEntranceFx.Hex(0xffc27a,1.6f*f),0,.22f,1,0,4900+k);fx.Glow(Fires[k],NativeEntranceFx.Hex(0xff8a40,.5f*f),0,.7f,0,0,4910+k);}}
            // under the name: motes of light lifting off the arrows
            if(t>CardAt-.6f)NativeEntranceFx.Embers(fx,t,CardAt-.6f,Total,L(0,0,0),3.2f,q<.5f?10:24,.4f,2.6f,Gold*.7f,.03f,4950,q,.3f);
        }

        // ---- sound: its own cues (no bed) ----
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.02f,"intro2.lightfall.grove",null,null,.35f),
            new NativeIntroCue(NockFrom+.05f,"intro2.lightfall.nock","melee.daggers.swing","daggers",.35f,1.2f),
            new NativeIntroCue(NockTo-.06f,"intro2.lightfall.seat","ui.click","ui",.3f,1.3f),
            new NativeIntroCue(DrawFrom,"intro2.lightfall.draw","structure.creak","wood",.5f,1.3f),
            new NativeIntroCue(DrawFrom+.2f,"intro2.lightfall.gather","chip.delayed.charge","install",.55f),
            new NativeIntroCue(Full,"intro2.lightfall.full","ui.install.catch","win",.4f,1.2f),
            new NativeIntroCue(Release,"intro2.lightfall.release","gun.flyby","dash",.9f,.8f),
            new NativeIntroCue(Bloom,"intro2.lightfall.bloom","chip.split","win",.85f),
            new NativeIntroCue(RainFrom+.05f,"intro2.lightfall.rain","impact.energy","lightning",.5f,1.2f),
            new NativeIntroCue(RainFrom+.3f,"intro2.lightfall.land","impact.energy","lightning",.45f,1.1f),
            new NativeIntroCue(RainFrom+.55f,"intro2.lightfall.land","impact.energy","lightning",.45f,1.0f),
            new NativeIntroCue(RainFrom+.8f,"intro2.lightfall.lantern","magic.fire.shot","fire",.35f,1.2f),
            new NativeIntroCue(RainFrom+1.05f,"intro2.lightfall.lantern","magic.fire.shot","fire",.3f,1.1f),
            new NativeIntroCue(CardAt,"intro2.lightfall.name","ui.install.surge","install",.5f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Style=NativeIntroCardStyle.Assemble;card.Ink=NativeEntranceFx.Hex(0xfff6e0);card.Glow=NativeEntranceFx.Hex(0xffd070,1.2f);
        }
    }
}
