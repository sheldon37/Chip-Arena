using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Cloudbreak", the new default intro (docs/INTROS-GENERIC.md "Cloudbreak"). 7.6 s, the name at 5.7 s. Dawn above a sea of cloud:
    /// a summit relay station, its mast blinking red. A boom, and a white-hot point falls out of the sky on a contrail: the pilot in a
    /// head-first dive, glowing from the re-entry. It flares, tucks and flips feet first, its boots' thrusters fire a braking burn, and
    /// it settles onto the summit's pad in slow motion; the wash blows grit and snow across the pad and snaps the windsocks out. It
    /// rises as the sun breaks the cloud line and floods the pad with gold. Camera: vertical (a look up past the mast, a long-lens
    /// fall-follow, a fall alongside, a slow-motion touchdown, a low orbit, a hero frame with the sun behind the shoulder).
    /// </summary>
    public sealed class NativeIntroCloudbreak:NativeIntroGenericScript
    {
        public const float Total=7.6f,CardAt=5.7f;
        public const float BoomAt=.35f,StreakAt=.45f,DiveAt=1.3f,FlipAt=2.45f,BurnAt=2.5f,TouchAt=3.12f,RiseAt=4.4f,SunAt=5.05f,PuffAt=6.45f;
        public override string Title=>"Cloudbreak";
        private static readonly Vector3 Hot=NativeEntranceFx.Hex(0xfff2dc),Ember=NativeEntranceFx.Hex(0xff8a3c),Thrust=NativeEntranceFx.Hex(0x9fe0ff),Gold=NativeEntranceFx.Hex(0xffc27a),
            Snow=new Vector3(.9f,.91f,.94f),Grit=new Vector3(.46f,.43f,.41f),Red=NativeEntranceFx.Hex(0xff3524),PadBlue=NativeEntranceFx.Hex(0xbfe8ff);
        // The set (Unity space, generic_cloudbreak.py): the mast's beacon, the windsocks' pivots, the sun's azimuth.
        private static readonly Vector3 Beacon=U(3.9f,15.72f,-4.9f);
        private static readonly Vector3[] SockAt={new Vector3(3.95f,3.28f,1.52f),new Vector3(-3.95f,3.28f,1.52f)};
        private static readonly Vector2 SunDir=new Vector2(0,-1);
        // The fall (source frame): the streak in from far up, the dive, the flare, the braking burn onto the pad. Hermite keys.
        private static readonly float[] FallT={StreakAt,DiveAt,FlipAt,TouchAt};
        private static readonly Vector3[] FallP={new Vector3(30,125,-105),new Vector3(6,38,-22),new Vector3(0,7.5f,.15f),new Vector3(0,.88f,0)};
        private static readonly Vector3[] FallV={new Vector3(-62,-190,183)*.82f,new Vector3(-5.2f,-27,19.5f),new Vector3(0,-21,1.2f),Vector3.Zero};
        /// <summary>Where the root is on its fall at `t` (StreakAt..TouchAt; held at the ends).</summary>
        public static Vector3 Fall(float t)
        {
            if(t<=FallT[0])return FallP[0];if(t>=FallT[FallT.Length-1])return FallP[FallP.Length-1];
            int i=0;while(i<FallT.Length-2&&t>FallT[i+1])i++;float d=FallT[i+1]-FallT[i],u=(t-FallT[i])/d,u2=u*u,u3=u2*u;
            return (2*u3-3*u2+1)*FallP[i]+(u3-2*u2+u)*d*FallV[i]+(-2*u3+3*u2)*FallP[i+1]+(u3-u2)*d*FallV[i+1];
        }
        public NativeIntroCloudbreak():base(NativeIntroCatalog.DefaultId,Total,CardAt,TouchAt,0x7fd4ff)
        {
            Start=Stance(.2f,.08f,.15f,-.2f,-.06f,-.4f);
            Steps=new[]{Step(false,RiseAt+.25f,RiseAt+.55f,-.17f,-.06f,-.38f,.06f),Step(true,RiseAt+.45f,RiseAt+.75f,.17f,.07f,.12f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // The dive: head first, body pitched over, arms swept back along the sides, legs together, looking down the drop.
            k.Key(0,stand).Root(0,.88f,0,Facing).Spin(2.3f,0).Planted(0).J(Hd,-.55f).J(C,-.12f).J(LA,.55f,0,.3f).J(LE,-.08f).J(RA,.55f,0,-.3f).J(RE,-.08f).J(LL,.05f,0,.04f).J(LK,.08f).J(RL,.05f,0,-.04f).J(RK,.08f);
            k.Key(DiveAt,stand).Root(0,.88f,0,Facing).Spin(2.35f,.08f).Planted(0).J(Hd,-.6f).J(C,-.14f).J(LA,.6f,0,.28f).J(LE,-.06f).J(RA,.6f,0,-.28f).J(RE,-.06f).J(LL,.06f,0,.04f).J(LK,.06f).J(RL,.06f,0,-.04f).J(RK,.06f);
            k.Key(FlipAt-.12f,stand).Root(0,.88f,0,Facing).Spin(2.28f,-.06f).Planted(0).J(Hd,-.62f).J(C,-.15f).J(LA,.5f,0,.4f).J(LE,-.1f).J(RA,.5f,0,-.4f).J(RE,-.1f).J(LL,.02f,0,.06f).J(LK,.12f).J(RL,.02f,0,-.06f).J(RK,.12f).PassKey();
            // The flare: belly to the pad, arms thrown wide, knees bending.
            k.Key(FlipAt+.06f,stand).Root(0,.88f,0,Facing).Spin(1.45f,0).Planted(0).J(Hd,-.35f).J(C,-.1f).J(LA,-.35f,0,1.35f).J(LE,-.2f).J(RA,-.35f,0,-1.35f).J(RE,-.2f).J(LL,-.4f,0,.12f).J(LK,.9f).J(RL,-.4f,0,-.12f).J(RK,.9f).PassKey();
            // The tuck, swinging the feet under.
            k.Key(FlipAt+.24f,stand).Root(0,.88f,0,Facing).Spin(.62f,0).Planted(0).J(B,.15f).J(Hd,.25f).J(LA,-1.1f,0,.55f).J(LE,-.7f).J(RA,-1.1f,0,-.55f).J(RE,-.7f).J(LL,-1.35f,0,.12f).J(LK,2.0f).J(RL,-1.3f,0,-.12f).J(RK,1.95f).PassKey();
            // Feet first under the burn: legs reaching down, arms out for balance, head down on the pad.
            k.Key(FlipAt+.42f,stand).Root(0,.88f,0,Facing).Spin(.08f,0).Planted(0).J(Hd,.38f).J(LA,-.45f,0,1.05f).J(LE,-.25f).J(RA,-.45f,0,-1.05f).J(RE,-.25f).J(LL,-.3f,0,.1f).J(LK,.45f).J(RL,-.18f,0,-.1f).J(RK,.3f);
            k.Key(TouchAt-.1f,stand).Root(0,.88f,0,Facing).Spin(0,0).Planted(.4f).Drop(-.04f).J(Hd,.32f).J(LA,-.5f,0,1.15f).J(LE,-.25f).J(RA,-.5f,0,-1.15f).J(RE,-.25f).J(LL,-.42f,0,.12f).J(LK,.7f).J(RL,-.38f,0,-.12f).J(RK,.65f);
            // Touchdown: a deep, balanced crouch, arms spread out low, palms down; the head comes up from the pad.
            k.Key(TouchAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.44f).J(B,.32f).J(Sp,.14f).J(Hd,.3f).J(LA,-.55f,0,1.25f).J(LE,-.3f).J(RA,-.55f,0,-1.25f).J(RE,-.3f).ImpactKey();
            k.Key(TouchAt+.3f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.47f).J(B,.36f).J(Sp,.16f).J(Hd,.22f).J(LA,-.35f,0,.95f).J(LE,-.35f).J(RA,-.35f,0,-.95f).J(RE,-.35f);
            k.Key(RiseAt-.25f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.45f).J(B,.3f).J(Sp,.12f).J(Hd,-.12f).J(LA,-.25f,0,.75f).J(LE,-.4f).J(RA,-.25f,0,-.75f).J(RE,-.4f);
            // The rise: up through the knees, chest open, chin up; it looks to the sun as it breaks.
            k.Key(RiseAt+.55f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.02f).J(B,.02f).J(C,-.06f).J(Hd,-.12f,.18f,0).J(LA,-.12f,0,.22f).J(LE,-.3f).J(RA,-.12f,0,-.22f).J(RE,-.3f).PassKey();
            k.Key(SunAt+.15f,stand).Root(0,.88f,0,Facing+.08f).Planted(1).Drop(-.02f).J(C,-.1f,.1f,0).J(Hd,-.2f,.42f,0).J(LA,-.1f,0,.2f).J(LE,-.32f).J(RA,-.1f,0,-.2f).J(RE,-.32f);
            k.Key(CardAt-.2f,NativeEntranceKeys.Hero()).Planted(1).J(Hd,-.08f,.1f,0);
            k.Key(Total,NativeEntranceKeys.Hero()).Planted(1).J(C,-.04f,-.16f,0).J(Hd,-.1f,.14f,0);Keys=k;
            RootPath=(t,r)=>t<TouchAt?Fall(t):r;
            // The camera, vertical: up past the mast at the dawn sky, the long-lens fall-follow, falling alongside, the touchdown low
            // and slowed, a low orbit through the rise, the hero frame.
            var e0=new Vector3(-.6f,.45f,3.6f);
            Rig.Crane(0,e0,new Vector3(-.25f,.5f,3.1f),.15f,e0+Dir(.24f,-.97f,.44f)*30,new Vector3(-.25f,.5f,3.1f)+Dir(.18f,-.98f,.76f)*30,58,54).Ease(NativeIntroEase.InOut)
               .Dolly(DiveAt,new Vector3(5.4f,1.0f,6.2f),new Vector3(5.1f,.95f,5.7f),Vector3.Zero,Vector3.Zero,14,21).Follow(NativeEntranceAnchor.Chest,.05f).Ease(NativeIntroEase.Linear)
               .Spline(2.2f,Alongside(2.2f,2.95f),Vector3.Zero,new Vector3(0,-.1f,0),44,50).Follow(NativeEntranceAnchor.Chest).Ease(NativeIntroEase.Linear)
               .Dolly(2.95f,new Vector3(-3.7f,.42f,4.5f),new Vector3(-3.35f,.5f,4.05f),new Vector3(0,1.7f,0),new Vector3(0,.75f,0),46,42).Ease(NativeIntroEase.Out)
               .Orbit(RiseAt-.05f,Vector3.Zero,-52,8,3.7f,3.2f,.42f,.8f,new Vector3(0,-.1f,0),new Vector3(0,.12f,0),40,36).Follow(NativeEntranceAnchor.Chest,.05f).In(NativeIntroCut.Blend,.4f).Handheld(.4f)
               .Hero(CardAt-.25f,1,1.12f,3.2f,31).In(NativeIntroCut.Blend,.55f)
               .Bars(0,.85f).Bars(CardAt-.6f,.4f).Black(0,1).Black(.06f,0);
            Ramps.Ramp(2.95f,.35f,.6f,.8f);
        }
        private static Vector3 Dir(float x,float z,float up){var h=Vector2.Normalize(new Vector2(x,z));float c=(float)Math.Cos(up),s=(float)Math.Sin(up);return new Vector3(h.X*c,s,h.Y*c);}
        /// <summary>The eye falling alongside the pilot from `from` to `to`: its fall sampled at even times, set off to its left front.</summary>
        private static Vector3[] Alongside(float from,float to)
        {
            var p=new Vector3[6];for(int i=0;i<p.Length;i++){float t=from+(to-from)*i/(p.Length-1f);var r=Fall(Math.Min(t,TouchAt));p[i]=r+new Vector3(2.0f+.3f*i/5f,.75f-.2f*i/5f,2.3f+.25f*i/5f);if(p[i].Y<.35f)p[i].Y=.35f;}
            return p;
        }
        /// <summary>Unseen until the streak appears.</summary>
        public override float Solid(float t,int bone)=>t<StreakAt-.03f?0:1;
        protected override Vector3 Focus(float t,Vector3 chest)=>chest;
        public override string AccentAt(float t)=>t<DiveAt?"#ffd8a8":t<FlipAt?"#ffb070":"#9fe0ff";
        /// <summary>The thrusters' burn 0..1: on at the flip, full through the brake, falling away after the touchdown.</summary>
        public static float Burn(float t)=>NativeEntranceMath.Ramp(t,BurnAt,BurnAt+.06f)*(1-NativeEntranceMath.Ramp(t,TouchAt+.08f,TouchAt+.4f));
        /// <summary>The re-entry glow 0..1 (the streak, cooling through the dive).</summary>
        public static float Heat(float t)=>NativeEntranceMath.Ramp(t,StreakAt-.03f,StreakAt+.1f)*(1-NativeEntranceMath.Ramp(t,DiveAt-.1f,FlipAt-.2f));
        /// <summary>The sunrise 0..1.</summary>
        public static float Sunrise(float t)=>NativeEntranceMath.Ramp(t,SunAt-.25f,SunAt+.6f);
        private static float SunElevation(float t)=>(-9.5f+12.5f*NativeEntranceMath.Smooth((t-4.2f)/2.0f))*(float)(Math.PI/180);
        private Vector3 SunAt3(float t,float distance=160)
        {
            float e=SunElevation(t),c=(float)Math.Cos(e);return U(SunDir.X*c*distance,1+(float)Math.Sin(e)*distance,SunDir.Y*c*distance);
        }

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("cloudbreak/summit"))return false;
            float sun=Sunrise(t);
            set.Piece("cloudbreak/sky",Vector3.Zero,Still,1,White,White*(.78f+.42f*sun));
            set.Piece("cloudbreak/clouds",Vector3.Zero,Still,1,White*(.92f+.12f*sun),Vector3.Zero);
            set.Piece("cloudbreak/peaks",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("cloudbreak/summit",Vector3.Zero,Still,1,White,Vector3.Zero);
            float pad=.55f+.12f*(float)Math.Sin(t*2.1f)+2.2f*Flash(t,TouchAt,.03f,.5f);
            set.Piece("cloudbreak/padlights",Vector3.Zero,Still,1,White,PadBlue*pad);
            set.Piece("cloudbreak/beacon",Vector3.Zero,Still,1,White,Red*(.25f+5.5f*Blink(t,1.6f,.35f)));
            // The sun: a disc facing the summit, rising out of the clouds.
            {
                var at=SunAt3(t);float e=SunElevation(t),yaw=(float)Math.Atan2(-SunDir.X,-SunDir.Y);
                var turn=UTurn(Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,e));
                set.Piece("cloudbreak/sun",at,turn,1,White,Gold*(2.2f+2.6f*sun));
            }
            // The windsocks: hanging slack on a light breeze, snapped straight out by the burn's wash, relaxing after.
            float gust=NativeEntranceMath.Ramp(t,TouchAt-.15f,TouchAt+.05f)*(1-.6f*NativeEntranceMath.Ramp(t,TouchAt+.6f,TouchAt+2.2f));
            for(int i=0;i<SockAt.Length;i++)
            {
                var p=SockAt[i];float breeze=(float)Math.Atan2(.35f,.94f)+.12f*(float)Math.Sin(t*.9f+i*2.1f),outward=(float)Math.Atan2(p.X,p.Z);
                float yaw=breeze+Wrap(outward-breeze)*gust,flutter=(Context.Reduced?.4f:1)*(.05f+.07f*gust)*(float)Math.Sin(t*(7+3*gust)+i*1.7f);
                float droop=.95f-.88f*gust+flutter;
                var turn=UTurn(Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,droop));
                set.Piece("cloudbreak/sock",U(p),turn,1,White,Vector3.Zero);
            }
            return true;
        }
        private static float Wrap(float a){while(a>Math.PI)a-=(float)(2*Math.PI);while(a<-Math.PI)a+=(float)(2*Math.PI);return a;}
        /// <summary>Without the bank: a pale dawn floor lost in the fog, a pool under the pilot that flares at the touchdown.</summary>
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,t,NativeEntranceFx.Hex(0xb9aeb4),Vector3.Lerp(PadBlue,Gold,Sunrise(t)),.6f+1.4f*Flash(t,TouchAt,.03f,.5f)+.4f*Sunrise(t),Gold);

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            float sun=Sunrise(t);
            l.Clear();l.DriveProbe=true;
            l.Background=NativeEntranceFx.Hex(0xe9b896,.9f);l.Fog=NativeEntranceFx.Hex(0xe6c3bd,.85f+.15f*sun);l.FogNear=70;l.FogFar=520;
            l.Sky=NativeEntranceFx.Hex(0x9fb8e6,.5f+.22f*sun);l.Ground=NativeEntranceFx.Hex(0xd8b4ac,.32f+.12f*sun);
            float e=SunElevation(t);var toSun=U(SunDir.X,Math.Max(.1f,(float)Math.Sin(e)+.16f),SunDir.Y);
            l.Directional(toSun,NativeEntranceFx.Hex(0xffc48a,.9f+1.25f*sun));
            l.Directional(new Vector3(-.55f,.75f,.7f),NativeEntranceFx.Hex(0x8ea8ff,.3f));
            var chest=Anchor(NativeEntranceAnchor.Chest,t);
            float heat=Heat(t);if(heat>.01f)l.Add(chest,Ember*(9*heat),9);
            float burn=Burn(t);
            if(burn>.01f){var feet=(Anchor(NativeEntranceAnchor.LeftFoot,t)+Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;l.Add(feet-new Vector3(0,.35f,0),Thrust*(7*burn),6);}
            float touch=Flash(t,TouchAt,.03f,.45f);if(touch>.01f)l.Add(new Vector3(0,.35f,.2f),PadBlue*(6*touch),6);
            float b=Blink(t,1.6f,.35f);if(b>.01f)l.Add(Beacon+new Vector3(0,.2f,0),Red*(3*b),6);
            if(t>=CardAt-.3f){float c=NativeEntranceMath.Ramp(t,CardAt-.3f,CardAt+.2f);l.Add(chest+new Vector3(-1.6f,.9f,1.8f),Gold*(1.6f*c),6);}
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);float floor=NativeEntranceStage.FloorY;bool low=q<.5f||Context.Low;
            // The streak: a white-hot head, a long contrail laid on the camera's plane, smoke hanging along it.
            float heat=Heat(t);
            if(t>=StreakAt-.03f&&t<FlipAt)
            {
                var head=Fall(t);float lit=NativeEntranceMath.Ramp(t,StreakAt-.03f,StreakAt+.12f);
                fx.Glow(head,Hot*(5*lit*heat+.6f),0,1.1f+.9f*heat,1,0,21000);fx.Glow(head,Ember*(2.2f*lit*heat),0,3.2f*heat+.4f,1,0,21001);
                int n=low?7:16;
                for(int k=0;k<n;k++)
                {
                    float t0=t-k*.05f,t1=t-(k+1)*.05f;if(t1<StreakAt)break;float age=k*.05f,fade=1-age/(n*.05f+.01f);
                    var p0=Fall(t0);var p1=Fall(t1);var col=Vector3.Lerp(Hot,new Vector3(.9f,.86f,.84f),NativeEntranceMath.Clamp01(age*2.5f))*((1.6f*heat+.25f)*fade*lit);
                    Line(fx,p0,p0-p1,col,.18f+.7f*age,.25f,age,21010+k);
                }
            }
            if(t>=StreakAt)
            {
                int n=low?6:14;
                for(int k=0;k<n;k++)
                {
                    float born=StreakAt+.07f*k,a=t-born;if(a<0||a>2.6f||born>DiveAt+.3f)continue;
                    var p=Fall(born)+new Vector3(NativeEntranceMath.Signed(21100,k)*.8f,a*.4f,NativeEntranceMath.Signed(21101,k)*.8f);
                    fx.Smoke(p,new Vector3(.95f,.92f,.92f),.5f*(1-a/2.6f)*NativeEntranceMath.Ramp(a,0,.15f),1.6f+3.2f*a,a/2.6f,21100+k,-40);
                }
            }
            // The dive: wind streaming past, mist peeling off the shoulders, the glow cooling.
            if(t>=DiveAt-.1f&&t<TouchAt)
            {
                var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);var v=Velocity(NativeEntranceAnchor.Pelvis,t);float speed=v.Length();
                if(speed>2)
                {
                    var dir=v/speed;int n=low?6:14;
                    for(int i=0;i<n;i++)
                    {
                        float phase=(t*3.1f+NativeEntranceMath.Rand(21200,i))%1f,a=i*2.4f;var side=Vector3.Normalize(Vector3.Cross(dir,new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a))+new Vector3(0,.01f,0)));
                        var at=pelvis+side*(.45f+.25f*NativeEntranceMath.Rand(21201,i))+dir*((phase-.5f)*1.8f);
                        fx.Streak(at,-dir*Math.Min(speed,34),White*(.55f*(float)Math.Sin(Math.PI*phase)),.01f,.15f,.025f,.9f,phase,21200+i);
                    }
                    int m=t<FlipAt?(low?3:7):0;
                    for(int i=0;i<m;i++)
                    {
                        float born=t-((t*6+i/(float)m)%1f)*.5f,a=t-born;var p=Fall(Math.Max(StreakAt,born))+new Vector3(NativeEntranceMath.Signed(21300,i)*.35f,.5f+.3f*a,NativeEntranceMath.Signed(21301,i)*.25f);
                        fx.Smoke(p,new Vector3(.93f,.93f,.95f),.22f*(1-a/.5f),.3f+1.2f*a,a/.5f,21300+i,-40);
                    }
                }
                if(heat>.02f){var c=Anchor(NativeEntranceAnchor.Chest,t);fx.Glow(c,Ember*(1.6f*heat),0,.8f,1,0,21350);}
            }
            // The thrusters: a jet from each boot, hot white at the nozzle, blue down the plume; the wash on the pad as it nears.
            float burn=Burn(t);
            if(burn>.01f)
            {
                float flick=Context.Reduced?1:.85f+.15f*(float)Math.Sin(t*53);
                for(int s=0;s<2;s++)
                {
                    var foot=Anchor(s==0?NativeEntranceAnchor.LeftFoot:NativeEntranceAnchor.RightFoot,t);var knee=Anchor(s==0?NativeEntranceAnchor.LeftKnee:NativeEntranceAnchor.RightKnee,t);
                    var d=foot-knee;d=d.LengthSquared()>1e-6f?Vector3.Normalize(d):-Vector3.UnitY;var nozzle=foot+d*.06f;
                    fx.Glow(nozzle,Hot*(3.2f*burn*flick),0,.18f,1,0,21400+s);fx.Glow(nozzle+d*.25f,Thrust*(2.2f*burn),0,.5f,1,0,21402+s);
                    int n=low?3:7;
                    for(int i=0;i<n;i++)
                    {
                        float u=((t*9+i/(float)n+s*.37f)%1f);var at=nozzle+d*(.1f+.75f*u)+new Vector3(NativeEntranceMath.Signed(21410+s,i)*.03f,0,NativeEntranceMath.Signed(21420+s,i)*.03f);
                        fx.Streak(at,d*7,Vector3.Lerp(Hot,Thrust,u)*(2.2f*burn*(1-u)),.06f*(1-u*.6f),.15f,.035f,.5f,u,21410+s*10+i);
                    }
                }
                float near=NativeEntranceMath.Ramp(t,TouchAt-.45f,TouchAt-.1f)*burn;
                if(near>.01f)for(int i=0;i<(low?6:14);i++){float a=i*.449f+t*.6f,r=.5f+((t*2.4f+i*.31f)%1f)*2.6f;var p=new Vector3((float)Math.Cos(a)*r,floor+.08f,(float)Math.Sin(a)*r);fx.Smoke(p,Snow,.18f*near,.35f+.15f*r,(r-.5f)/2.6f,21450+i,floor);}
            }
            // The touchdown: grit and snow blown out over the pad, powder off its edges, a soft blue pop.
            NativeEntranceFx.GroundDust(fx,t,TouchAt,new Vector3(0,floor+.04f,0),low?10:24,.5f,4.6f,1.6f,Grit,.5f,.45f,21500,q,floor);
            {
                float a=t-TouchAt;
                if(a>=0&&a<2.4f)
                {
                    int n=low?8:20;
                    for(int i=0;i<n;i++)
                    {
                        float ang=i*6.2831853f/n+NativeEntranceMath.Signed(21600,i)*.12f,life=1.6f+.8f*NativeEntranceMath.Rand(21601,i);if(a>life)continue;float u=a/life;
                        var outw=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var p=outw*(3.6f+3.2f*(1-(1-u)*(1-u)))+new Vector3(0,floor+.15f+.9f*u,0);
                        fx.Smoke(p,Snow,.42f*(1-u)*NativeEntranceMath.Ramp(a,0,.12f),.5f+1.6f*u,u,21600+i,-30);
                    }
                    if(a<.5f){int m=low?8:18;for(int i=0;i<m;i++){float ang=i*6.2831853f/m+.2f,u=a/.5f;var outw=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));fx.Streak(outw*(.4f+3.4f*u)+new Vector3(0,floor+.03f,0),outw*5,Snow*(1.2f*(1-u)),.012f,.18f,.04f,.8f,u,21650+i);}}
                    float pop=Flash(t,TouchAt,.02f,.18f);if(pop>.01f)fx.Glow(new Vector3(0,floor+.3f,0),PadBlue*(2.2f*pop),0,1.4f,1,0,21690);
                }
            }
            // The thrusters winding down: three blue sputters and the boots' heat; one last puff after the name.
            for(int i=0;i<4;i++)
            {
                float at=i<3?RiseAt+.1f+.27f*i:PuffAt,p=Flash(t,at,.02f,.12f);if(p<.01f)continue;
                for(int s=0;s<2;s++){var foot=Anchor(s==0?NativeEntranceAnchor.LeftFoot:NativeEntranceAnchor.RightFoot,t);fx.Glow(foot-new Vector3(0,.03f,0),Thrust*(1.4f*p),0,.22f,1,0,21700+i*2+s);fx.Smoke(foot,new Vector3(.8f,.85f,.9f),.2f*p,.25f,1-p,21720+i*2+s,floor);}
            }
            {
                float h=Swell(t,TouchAt,.05f,.6f,1.6f);if(h>.01f)for(int s=0;s<2;s++){var foot=Anchor(s==0?NativeEntranceAnchor.LeftFoot:NativeEntranceAnchor.RightFoot,t);fx.Glow(foot,Ember*(.5f*h),0,.16f,0,0,21740+s);}
            }
            // The sun: its halo breaking over the cloud line, then a slow glint.
            {
                float sun=Sunrise(t),rise=NativeEntranceMath.Ramp(t,4.4f,SunAt);var at=SunAt3(t,170);
                if(rise>.01f){fx.Glow(at,Gold*(1.2f*rise+1.8f*sun),0,26+14*sun,1,0,21800);fx.Glow(at,Hot*(1.6f*sun),0,11,1,0,21801);}
            }
            // Ice crystals glittering in the air over the pad once the sun is up.
            {
                float sun=Sunrise(t);int n=low?8:22;
                for(int i=0;i<n;i++)
                {
                    var p=new Vector3(NativeEntranceMath.Signed(21900,i)*4.5f,.2f+2.6f*NativeEntranceMath.Rand(21901,i),NativeEntranceMath.Signed(21902,i)*4.5f)+new Vector3((float)Math.Sin(t*.4f+i)*.3f,(float)Math.Sin(t*.3f+i*1.3f)*.15f,t*.12f);
                    float tw=Context.Reduced?.7f:.5f+.5f*(float)Math.Abs(Math.Sin(t*1.7f+i*2.3f));
                    fx.Glow(p,Vector3.Lerp(PadBlue,Gold,sun)*(.12f+.3f*sun*tw),0,.03f,3,0,21900+i);
                }
            }
        }
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            // Afterimages through the flip, cyan, thinning as it brakes.
            float w=Swell(t,FlipAt-.05f,.08f,.3f,.3f);if(w<.02f||into.Length<3)return 0;
            for(int i=0;i<3;i++)into[i]=new NativeEntranceGhost{Lag=.035f*(i+1),Opacity=.28f*w*(1-i*.28f),Color=Thrust};
            return 3;
        }

        // ---- the name card and the sound ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Style=NativeIntroCardStyle.Wipe;card.Glow=Gold*1.4f;card.Ink=new Vector3(1,.97f,.92f);
        }
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.cloudbreak.wind","structure.whoosh","dash",.3f,.8f),
            new NativeIntroCue(BoomAt,"intro2.cloudbreak.boom","structure.boom","explosion",.4f,.7f),
            new NativeIntroCue(StreakAt,"intro2.cloudbreak.fall","magic.meteor.incoming","dash",.75f),
            new NativeIntroCue(FlipAt,"intro2.cloudbreak.flip","melee.miss","dash",.5f),
            new NativeIntroCue(BurnAt,"intro2.cloudbreak.ignite","gun.rocket.launch","fire",.85f),
            new NativeIntroCue(BurnAt+.08f,"intro2.cloudbreak.thrust","magic.flame-lance","fire",.6f,.9f),
            new NativeIntroCue(TouchAt,"intro2.cloudbreak.touch","blast.quake","stone",.85f),
            new NativeIntroCue(TouchAt+.03f,"intro2.cloudbreak.gust","structure.whoosh","dash",.65f),
            new NativeIntroCue(RiseAt+.1f,"intro2.cloudbreak.cool","structure.creak","metal",.35f),
            new NativeIntroCue(SunAt,"intro2.cloudbreak.sun","chip.heal","install",.5f),
            new NativeIntroCue(CardAt,"intro2.cloudbreak.name","chip.critical","install",.75f),
            new NativeIntroCue(PuffAt,"intro2.cloudbreak.puff","chip.teleport","dash",.25f),
        };
        public override NativeIntroCue[] Cues=>cues;
    }
}
