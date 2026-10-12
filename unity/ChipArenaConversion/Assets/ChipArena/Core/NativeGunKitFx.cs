using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The kit sink plus the weapons drawer's soft wisp (third person on the world camera, first person on the view-model
    /// camera): colour premultiplied by its coverage.</summary>
    public interface IGunKitSink:IKitFxSink
    {
        void Wisp(bool view,Vector3 at,Vector3 color,float coverage,float size,float seed,float roll);
    }
    /// <summary>A posed gun's frame in world metres (Unity): the hand origin, the hand matrix's unit axes (X the shooter's right,
    /// Y up, Z along the barrel) and its scale (1 in third person, the view model's size in first person).</summary>
    public struct GunFrame
    {
        public Vector3 O,X,Y,Z;public float Scale;
        /// <summary>A point given in the gun's source frame (+x the gun's left, as the weapon models and RangedPose).</summary>
        public Vector3 At(float x,float y,float z)=>O+(X*-x+Y*y+Z*z)*Scale;
        public Vector3 Dir(float x,float y,float z)=>X*-x+Y*y+Z*z;
    }
    /// <summary>What the kit effects read of the presented frame: the replicated actors, the local first-person pilot, each
    /// gun's posed frame (third-person rig, or with view the first-person view weapon) and the first-person eye.</summary>
    public interface IGunKitScene
    {
        IReadOnlyList<Actor> Actors{get;}
        int Viewer{get;}
        bool Gun(Actor a,bool view,out GunFrame frame);
        /// <summary>The first-person eye: position, unit forward/up/right and the tangent of half the vertical FOV, the aspect.</summary>
        bool Eye(out Vector3 at,out Vector3 forward,out Vector3 up,out Vector3 right,out float tanHalf,out float aspect);
    }

    /// <summary>
    /// The pistol and machine-gun Q kit's effects (Claude's presentation of the rules proposed to Astra, docs/RANGED-ABILITIES.md),
    /// each a pure function of the replicated kit (Actor.FanAt/FanShots, OverdriveAt/OverdriveUntil/VentUntil), the actor's
    /// LastShot and the posed gun, seeded by cast and round times, aged on the presentation clock. Fan the Hammer: a glint on
    /// the hammer at each slap, brass flicked out of the port per round, the smoking barrel after the volley (the muzzle stars,
    /// the shot cues and the tracers fanning out with the spread come from the rounds themselves; NativeTracerLook warms
    /// them). Overdrive: the barrel cluster spinning up, the barrel glow ramping orange to white-hot over the active time with
    /// heat shimmer and steam off the shroud, then the overheat vent: steam bursts from the side vents while the glow turns red
    /// and cools; first person adds a heat haze along the screen's bottom edge. Effects quality (Reduced, .3) thins them; the low
    /// graphics tier halves the particles. Nothing here reaches the authority, the network frame, hit timing or damage.
    /// </summary>
    public static class NativeGunKitFx
    {
        public const int Brass=0xd8a34a,BrassGlint=0xfff1c4,HammerGlint=0xfff6e0,Smoke=0x8a8f99,Steam=0xdfe6ee,Haze=0xb9a48e;
        public const int HeatDull=0xff3a0a,HeatOrange=0xff7a1c,HeatYellow=0xffc95a,HeatWhite=0xfff2dc,VentRed=0xff2412;
        public const float BrassLife=.5f,SmokeLife=1.1f,SteamLife=.7f,VentLife=.85f,HazeLife=.38f;
        public static Vector3 Lin(int hex,float gain=1)=>NativeMeleeKitFx.Lin(hex,gain);
        private static float H(long seed,long k)=>(float)NativeWeaponFx.Hash(seed,k);
        private static int Count(int full,float q,bool low){float n=full*Math.Max(.3f,Math.Min(1,q));if(low)n*=.5f;return Math.Max(1,(int)Math.Round(n));}

        /// <summary>The glow colour of the barrels at heat h (0..1: dull red-orange, orange, yellow, white-hot) shifted toward the
        /// vent red by r (0..1), HDR: brighter as it heats.</summary>
        public static Vector3 HeatColor(float h,float r)
        {
            h=NativeGunKit.Clamp01(h);Vector3 c=h<.45f?Vector3.Lerp(Lin(HeatDull),Lin(HeatOrange),h/.45f):h<.8f?Vector3.Lerp(Lin(HeatOrange),Lin(HeatYellow),(h-.45f)/.35f):Vector3.Lerp(Lin(HeatYellow),Lin(HeatWhite),(h-.8f)/.2f);
            c=Vector3.Lerp(c,Lin(VentRed),NativeGunKit.Clamp01(r));return c*(.35f+3.1f*h*h);
        }

        /// <summary>Draws every live kit effect of the frame at `time`.</summary>
        public static void Draw(IGunKitSink sink,IGunKitScene scene,float time,float quality,bool low)
        {
            if(sink==null||scene?.Actors==null)return;var actors=scene.Actors;int viewer=scene.Viewer;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null||!a.Alive)continue;var s=NativeGunKit.Of(a,time);bool view=viewer!=0&&a.Id==viewer;
                if(a.Weapon==WeaponId.Pistol&&s.HasFan&&NativeGunKit.FanAt(s,time,out _,out _)!=NativeGunKit.FanPhase.None&&scene.Gun(a,view,out var f))Fan(sink,a,s,f,view,time,quality,low);
                else if(a.Weapon==WeaponId.MachineGun&&s.HasOverdrive&&NativeGunKit.DriveAt(s,time)!=NativeGunKit.DrivePhase.None)
                {
                    if(scene.Gun(a,view,out var g))Drive(sink,a,s,g,view,time,quality,low);
                    if(view&&scene.Eye(out var eye,out var fwd,out var up,out var right,out float tan,out float aspect))ScreenHaze(sink,s,eye,fwd,up,right,tan,aspect,time,quality,low);
                }
            }
        }

        // ---- Fan the Hammer ----------------------------------------------------------------------------------------------
        /// <summary>
        /// Round k's brass at `age`: barrel-frame offset from the ejection port (flicked right, up and a little back at 2.2-3 m/s,
        /// damped), its world drop, the casing's tumbling axis (barrel frame) and opacity. Seeded by the cast and the round.
        /// </summary>
        public static bool Casing(long seed,int k,float age,out Vector3 local,out float drop,out Vector3 axis,out float alpha)
        {
            local=axis=default;drop=alpha=0;if(!(age>=0&&age<BrassLife))return false;
            float speed=2.2f+.8f*H(seed,10+k),spin=22+14*H(seed,20+k),u=age/BrassLife;
            var dir=Vector3.Normalize(new Vector3(-.85f-.25f*H(seed,30+k),.55f+.3f*H(seed,40+k),-.3f+.25f*H(seed,50+k)));
            float travel=speed*(1-(float)Math.Exp(-3*age))/3;local=new Vector3(-.035f,.075f,.12f)+dir*travel;drop=.5f*9.8f*age*age;
            float phi=spin*age+6.28f*H(seed,60+k);axis=new Vector3((float)Math.Cos(phi)*.2f,(float)Math.Sin(phi),(float)Math.Cos(phi));axis=Vector3.Normalize(axis);
            alpha=u<.85f?1:(1-u)/.15f;return true;
        }
        /// <summary>The smoking barrel's wisp k (born every .055 s from the last round): muzzle offset (barrel frame), world rise,
        /// size and coverage. The barrel smokes for FanSmoke after the volley.</summary>
        public static bool BarrelSmoke(in NativeGunKit.State s,long seed,int k,float time,out Vector3 local,out float rise,out float size,out float cover)
        {
            local=default;rise=size=cover=0;float born=s.FanLast+.04f+k*.055f,age=time-born;if(born>s.FanLast+NativeGunKit.FanSmoke-SmokeLife||!(age>=0&&age<SmokeLife))return false;
            float u=age/SmokeLife,h=H(seed,100+k),fade=1-NativeGunKit.Clamp01((born-s.FanLast)/(NativeGunKit.FanSmoke-SmokeLife));
            local=new Vector3((h-.5f)*.05f*u,.0f,.52f+.04f*u);rise=.08f+.32f*age+.05f*(float)Math.Sin(age*7+h*6);
            size=.025f+.22f*(float)Math.Sqrt(u);cover=.2f*NativeWeaponFx.Smooth(u/.15f)*(1-u)*(1-u)*(.35f+.65f*fade);return true;
        }
        private static void Fan(IGunKitSink sink,Actor a,in NativeGunKit.State s,in GunFrame f,bool view,float time,float quality,bool low)
        {
            long seed=NativeWeaponFx.Seed(s.FanAt,a.Id);float sc=f.Scale;
            for(int k=0;k<s.FanShots;k++)
            {
                float at=s.FanShot(k),age=time-at;if(age<0)break;
                // The slap's glint on the hammer spur (40 ms), a little brighter in first person.
                if(age<.04f){float g=1-age/.04f;sink.Sprite(view,f.At(0,.1f,-.1f),Lin(HammerGlint,(view?1.6f:2.4f)*g),.07f*sc,NativeMeleeKitFx.Sparkle);}
                if(Casing(seed,k,age,out var local,out float drop,out var axis,out float alpha))
                {
                    var c=f.At(local.X,local.Y,local.Z)-Vector3.UnitY*(drop*sc);var half=f.Dir(axis.X,axis.Y,axis.Z)*(.018f*sc);
                    sink.Line(view,c-half,c+half,Lin(Brass,1.6f*alpha),.017f*sc,false);
                    if(H(seed,70+k+(long)(age*30))>.55f)sink.Sprite(view,c,Lin(BrassGlint,1.2f*alpha),.05f*sc,NativeMeleeKitFx.Sparkle);
                }
            }
            int wisps=Count(30,quality,low);
            for(int k=0;k<wisps;k++)
            {
                if(!BarrelSmoke(s,seed,k*(30/wisps),time,out var local,out float rise,out float size,out float cover))continue;
                sink.Wisp(view,f.At(local.X,local.Y,local.Z)+Vector3.UnitY*(rise*sc),Lin(Smoke)*cover,cover*(view?.6f:1),size*sc,H(seed,200+k)*10,H(seed,300+k)*6.28f);
            }
        }

        // ---- Overdrive ----------------------------------------------------------------------------------------------------
        /// <summary>The vent burst's puff k (from the vents on both sides: k even the gun's right, odd its left; two vents along the
        /// shroud): barrel-frame offset, world rise, size and coverage at `time`. 32 puffs over the vent's first .66 s.</summary>
        public static bool VentPuff(in NativeGunKit.State s,long seed,int k,float time,out Vector3 local,out float rise,out float size,out float cover)
        {
            local=default;rise=size=cover=0;float born=s.OverdriveUntil+k*.021f,age=time-born;if(!(age>=0&&age<VentLife)||born>s.VentUntil)return false;
            float u=age/VentLife,h=H(seed,400+k),side=(k&1)==0?-1:1,z=(k&2)==0?.4f:.52f,speed=2.1f+.9f*h;
            float travel=speed*(1-(float)Math.Exp(-4*age))/4;
            local=new Vector3(side*(.075f+travel),.02f+.15f*travel*(H(seed,500+k)-.3f),z+(H(seed,600+k)-.5f)*.08f+.2f*travel*(H(seed,700+k)-.5f));
            rise=.35f*age+.1f*u;size=.05f+.38f*(float)Math.Sqrt(u);cover=.62f*NativeWeaponFx.Smooth(u/.06f)*(1-u)*(1-u);return true;
        }
        private static void Drive(IGunKitSink sink,Actor a,in NativeGunKit.State s,in GunFrame f,bool view,float time,float quality,bool low)
        {
            long seed=NativeWeaponFx.Seed(s.OverdriveAt,a.Id);float sc=f.Scale,heat=NativeGunKit.Heat(s,time),red=NativeGunKit.Redden(s,time),spin=NativeGunKit.Spin(s,time);
            var phase=NativeGunKit.DriveAt(s,time);bool firing=phase==NativeGunKit.DrivePhase.SpinUp||phase==NativeGunKit.DrivePhase.Sustain;
            // The barrel glow: a camera-facing halo card along the barrel (its rim shows round the barrel's silhouette), soft halos
            // along it to the muzzle, a hot core at the muzzle and the shroud warming late.
            var hot=HeatColor(heat,red);float halo=.6f+.6f*heat;
            sink.Line(view,f.At(0,0,.46f),f.At(0,0,.9f),hot*.32f,.11f*halo*sc,false);
            for(int k=0;k<5;k++){float z=.5f+k*.095f,flick=.92f+.08f*(float)Math.Sin(time*31+k*1.7f);sink.Sprite(view,f.At(0,0,z),hot*(.42f*flick),(.15f+.02f*k)*halo*sc,NativeMeleeKitFx.Soft);}
            if(heat>.3f)sink.Sprite(view,f.At(0,0,.88f),Vector3.Lerp(hot,Lin(HeatWhite,3),.5f*heat)*NativeGunKit.Clamp01((heat-.3f)/.5f),.13f*sc,NativeMeleeKitFx.Hot);
            if(heat>.55f){float w=(heat-.55f)/.45f;for(int k=0;k<3;k++)sink.Sprite(view,f.At(0,.01f,.36f+k*.13f),HeatColor(heat*.7f,red)*(.3f*w),.2f*sc,NativeMeleeKitFx.Soft);}
            // The spinning barrel cluster: six barrels round the axis in the heat's colour, each with two motion-blur echoes.
            if(spin>.02f)
            {
                float angle=NativeGunKit.SpinAngle(s,time),blur=Math.Min(.55f,spin*NativeGunKit.SpinRate*6.283f/60);var metal=Vector3.Lerp(Lin(0xc8d2e0,.55f),hot*1.1f,NativeGunKit.Clamp01(heat*1.4f));
                for(int k=0;k<6;k++)for(int e=0;e<3;e++)
                {
                    float phi=angle+k*1.0472f-e*blur*.5f,x=.034f*(float)Math.Cos(phi),y=.034f*(float)Math.Sin(phi),fade=(e==0?1:e==1?.5f:.25f)*(.35f+.65f*spin);
                    sink.Line(view,f.At(x,y,.5f),f.At(x,y,.88f),metal*(.6f*fade),.011f*sc,false);
                }
                sink.Sprite(view,f.At(0,0,.9f),hot*(.25f*spin)+Lin(0xd8e4f2,.12f*spin),.1f*sc,NativeMeleeKitFx.RingSprite);
            }
            // Heat shimmer above the barrel and thin steam off the shroud while it fires.
            if(firing&&heat>.15f)
            {
                int haze=Count(6,quality,low);float bucket=.07f;long now=(long)Math.Floor(time/bucket);
                for(long b=now-5;b<=now;b++)for(int j=0;j<haze;j++)
                {
                    float born=b*bucket+H(seed,b*16+j)*bucket,age=time-born;if(!(age>=0&&age<HazeLife))continue;float u=age/HazeLife,hj=H(seed,b*16+j+8);
                    var at=f.At((hj-.5f)*.08f+.03f*(float)Math.Sin(age*24+hj*9),.05f,.5f+.4f*H(seed,b*16+j+4))+Vector3.UnitY*((.08f+.9f*age)*sc);
                    // Heat shimmer: faint additive warm wisps (no coverage), wavering as they rise.
                    float glow=.05f*heat*(float)Math.Sin(Math.PI*u);sink.Wisp(view,at,HeatColor(heat*.8f,0)*glow,0,(.05f+.08f*u)*sc,hj*10,hj*6.28f);
                }
                int steam=Count(3,quality,low);float every=.3f-.18f*heat;long s0=(long)Math.Floor((time-s.OverdriveAt)/every);
                for(long b=Math.Max(0,s0-3);b<=s0;b++)for(int j=0;j<steam;j++)
                {
                    float born=s.OverdriveAt+b*every+H(seed,900+b*4+j)*every,age=time-born;if(!(age>=0&&age<SteamLife))continue;float u=age/SteamLife,hj=H(seed,950+b*4+j);
                    var at=f.At((hj-.5f)*.1f,.07f,(j&1)==0?.4f:.52f)+Vector3.UnitY*((.06f+.45f*age)*sc);float cover=.16f*heat*NativeWeaponFx.Smooth(u/.1f)*(1-u);
                    sink.Wisp(view,at,Lin(Steam)*cover,cover,(.04f+.14f*u)*sc,hj*10,hj*6.28f);
                }
            }
            // The overheat vent: the first jets, then the steam bursts from both sides of the shroud (soft wisps; smaller in first
            // person, where they blow past the view weapon).
            if(time>=s.OverdriveUntil&&time<s.VentUntil+VentLife)
            {
                float jet=time-s.OverdriveUntil,k1=view?.6f:1;
                if(jet<.18f){float g=1-jet/.18f;for(int k=0;k<4;k++){float side=(k&1)==0?-1:1,z=(k&2)==0?.4f:.52f;for(int j=0;j<3;j++){float d=.09f+(.06f+.1f*j)*(.5f+jet*5);sink.Wisp(view,f.At(side*d,.02f+.01f*j,z),Lin(Steam)*(.3f*g),.3f*g*k1,(.05f+.03f*j)*sc*k1,H(seed,1200+k*4+j)*10,H(seed,1300+k*4+j)*6.28f);}}}
                int puffs=Count(32,quality,low);
                for(int k=0;k<puffs;k++)
                {
                    int idx=k*(32/puffs);if(!VentPuff(s,seed,idx,time,out var local,out float rise,out float size,out float cover))continue;
                    var at=f.At(local.X,local.Y,local.Z)+Vector3.UnitY*(rise*sc);
                    sink.Wisp(view,at,Lin(Steam)*cover,cover*(view?.6f:.85f),size*sc*k1,H(seed,1000+idx)*10,H(seed,1100+idx)*6.28f);
                }
            }
        }
        /// <summary>
        /// The first-person heat haze along the bottom of the screen: warm wisps rising off the lower edge and two faint hot corners
        /// at the heat of the barrels (while firing and venting), on the view layer just in front of the eye.
        /// </summary>
        private static void ScreenHaze(IGunKitSink sink,in NativeGunKit.State s,Vector3 eye,Vector3 fwd,Vector3 up,Vector3 right,float tan,float aspect,float time,float quality,bool low)
        {
            float heat=NativeGunKit.Heat(s,time);if(heat<.12f||time>=s.VentUntil)return;float d=.3f,hh=d*tan,hw=hh*aspect;long seed=NativeWeaponFx.Seed(s.OverdriveAt,7);
            Vector3 P(float x,float y)=>eye+fwd*d+right*(x*hw)+up*(y*hh);
            int n=Count(14,quality,low);float every=.11f;long now=(long)Math.Floor(time/every);
            for(long b=now-6;b<=now;b++)for(int j=0;j<n;j++)
            {
                float born=b*every+H(seed,b*32+j)*every,age=time-born,life=.6f;if(!(age>=0&&age<life))continue;float u=age/life,hx=H(seed,b*32+j+16)*2-1;
                // Additive and faint (no coverage): a warm wavering shimmer off the lower edge, not smoke.
                float glow=.06f*heat*(float)Math.Sin(Math.PI*u);var at=P(hx*.95f+.04f*(float)Math.Sin(age*9+hx*5),-1.04f+.2f*u);
                sink.Wisp(true,at,HeatColor(heat*.75f,NativeGunKit.Redden(s,time))*glow,0,hh*(.14f+.12f*u),hx*10,hx*6.28f);
            }
            var warm=HeatColor(heat*.8f,NativeGunKit.Redden(s,time))*(.05f*heat);
            sink.Sprite(true,P(-.75f,-1.05f),warm,hh*.9f,NativeMeleeKitFx.Soft);sink.Sprite(true,P(.75f,-1.05f),warm,hh*.9f,NativeMeleeKitFx.Soft);
        }
    }

    /// <summary>
    /// The tracer of a gun round fired during the kit (WeaponFx's ShotFx, by the round's owner and birth time): Overdrive's rounds
    /// burn hotter (an orange-white edge), brighter, wider and longer, so the stream reads denser; fanned rounds a little warmer
    /// and wider. Other rounds keep the weapons milestone tracer.
    /// </summary>
    public struct NativeTracerLook
    {
        public float Width,Length,Gain,Sheath;public int Edge;public bool Kit;
        public static NativeTracerLook Of(Actor owner,float born,string spec,float time)
        {
            var l=new NativeTracerLook{Width=NativeTracerFx.Width(spec),Length=1,Gain=NativeTracerFx.CoreGain,Sheath=.5f,Edge=NativeTracerFx.EdgeColor};if(owner==null)return l;
            var s=NativeGunKit.Of(owner,time);
            if(spec=="machinegun"&&NativeGunKit.DriveRound(s,born)){l.Width*=1.35f;l.Length=1.3f;l.Gain*=1.5f;l.Sheath=.7f;l.Edge=0xffa64a;l.Kit=true;}
            else if(spec=="pistol"&&NativeGunKit.FanRound(s,born)){l.Width*=1.2f;l.Length=1.25f;l.Gain*=1.25f;l.Sheath=.65f;l.Edge=0xffd38a;l.Kit=true;}
            return l;
        }
    }
}
