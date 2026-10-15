using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>An optional ICyberFxSink extension: a flame card along tail to head (the weapons drawer's FlameMode: hot at the
    /// tail, tapering and flickering by seed toward the head). Sinks without it draw a streak line instead.</summary>
    public interface ICyberFxFlames{void Flame(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,float seed);}

    /// <summary>
    /// The cyber mage's basic attack look (owner 2026-10-15: "Ranged is also intended to be like a cyber mage- throwing like
    /// "Cyber-themed fire balls" for their basic attack"), part of the Cyber Lord's world effects and keyed the same way: the
    /// replicated shots of Spec cyberlord (today's Bit Bolts, Detail bit-bolt; Kit v2's Plasma Byte and Overflow), the throws'
    /// windows on the view and the hits and impacts their events carry.
    /// - The Plasma Byte fireball: a hot white core inside a magenta and cyan plasma flame (flame tongues streaming back off it,
    ///   a soft halo), a tail of pixel embers drifting where it passed, and a slight RGB glitch (offset magenta and cyan copies on
    ///   24 Hz steps). It leaves the throwing hand: for its first moments it eases from the hand onto the replicated path, so the
    ///   ball the hand throws is the one that flies. At range it keeps a minimum on-screen size, so it reads at 40 m.
    /// - The Overflow: bigger, with code bytes orbiting it.
    /// - The hand: a fireball held over each palm in ranged mode (the loaded hand's full, the forward hand's smaller), spent on
    ///   the throw and re-forming; a bright cast flash with a fan of flame where it leaves the hand.
    /// - The splash: a plasma burst (a white flash, flame petals blown out round the surface normal, a hex ring), hex shards with
    ///   gravity and a scorch flicker (upright flame licks and a wisp of smoke); Kit v2's splash radius as a fading shell.
    /// Reduced effects: no glitch, flicker or strobe, fewer embers and shards. The low tier: fewer tongues and embers, no smoke.
    /// Nothing lies flat on the floor. Presentation only.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        /// <summary>The fireball's timing (s): the ease from the hand onto the replicated path, an ember's life and the spacing of
        /// their emission, the cast flash, a splash's life (world, pilot).</summary>
        public const float Converge=.12f,EmberLife=.24f,EmberStep=.018f,CastFlash=.13f,SplashLife=.55f;
        /// <summary>The drawn sizes by kind (a v1 Bit Bolt, a Plasma Byte, an Overflow), and the smallest angular size (rad) a
        /// fireball's halo and body keep at range.</summary>
        public static readonly float[] KindScale={.85f,1,1.6f};
        public const float FarHalo=.03f,FarBody=.014f,FarCore=.006f;
        public const int HotWhite=0xffffff,Plasma=0xff2bd6,Violet=0x9b4dff;
        private static readonly Vector3 Up2=Vector3.UnitY;
        private static void Flame(ICyberFxSink s,bool view,Vector3 tail,Vector3 head,Vector3 color,float width,float seed)
        {
            if(s is ICyberFxFlames f)f.Flame(view,tail,head,color,width,seed);else s.Line(view,tail,head,color,width*.5f,false);
        }
        private static Vector3 Across(Vector3 dir){var r=Vector3.Cross(Up2,dir);if(r.LengthSquared()<1e-6f)r=Vector3.UnitX;return Vector3.Normalize(r);}

        /// <summary>
        /// One fireball at `at` flying along `dir` (unit) at `speed`, `age` s after it left the hand, of kind `kind` (0 a Bit Bolt,
        /// 1 a Plasma Byte, 2 an Overflow). `spawn` is where it left (the drawn path's start, after the hand's ease) and `hand` the
        /// offset from the replicated start to the hand (eased out over Converge).
        /// </summary>
        private void Fireball(ICyberFxSink s,Vector3 at,Vector3 spawn,Vector3 hand,Vector3 dir,float speed,float age,int kind,float time,long seed,float q,bool low)
        {
            bool reduced=q<.5f;float sc=KindScale[Math.Max(0,Math.Min(2,kind))],dist=Vector3.Distance(at,eye);int step=(int)(time*24);
            float fl=reduced?1:.86f+.28f*H(seed+step,3),birth=Smooth(age/.05f);var side=Across(dir);var up=Vector3.Normalize(Vector3.Cross(dir,side));
            float halo=Math.Max(.62f*sc,dist*FarHalo),body=Math.Max(.34f*sc,dist*FarBody),core=Math.Max(.14f*sc,dist*FarCore),far=1+Math.Min(1.2f,dist/30);
            // The halo (soft, sorted), the plasma body (magenta round a cyan heart) and the white-hot core.
            s.Glow(at,Lin(Plasma,.55f*fl*birth*far),0,halo,Core,MeleeMath.Frac(age*2.3f),seed);
            s.Sprite(false,at,Lin(Magenta,2.1f*fl*far),body,Soft);s.Sprite(false,at,Lin(Cyan,2.8f),body*.68f,Hot);s.Sprite(false,at,Lin(HotWhite,4.4f),core,Hot);
            // The flame tongues streaming back (shortened so a ball flying away from the eye never reaches behind it).
            float room=Vector3.Dot(at-eye,dir)<0?99:Math.Max(0,Vector3.Dot(at-eye,dir)-.35f),stretch=Math.Min(1.35f,.75f+speed/120f);
            int tongues=low?2:3;
            for(int i=0;i<tongues;i++)
            {
                float len=(i==0?.78f:i==1?.52f:.3f)*sc*stretch*(reduced?1:.9f+.2f*H(seed+step*3+i,5)),wd=(i==0?.36f:i==1?.22f:.09f)*sc;len=Math.Min(len,room);if(len<.05f)continue;
                var j=reduced?Vector3.Zero:side*((H(seed+step*7+i,7)-.5f)*.09f*sc)+up*((H(seed+step*5+i,9)-.5f)*.07f*sc);
                Flame(s,false,at+dir*(.05f*sc),at-dir*len+j,i==0?Lin(Magenta,1.9f*fl):i==1?Lin(Cyan,2.1f):Lin(HotWhite,2.6f),wd,reduced?.3f+i*.2f:H(seed+step+i,11));
            }
            s.Streak(at,dir*speed,Lin(Cyan,1.1f*birth),.07f*sc,.3f*sc,.008f,.9f,Clamp01(age/.6f),seed);
            // The RGB glitch: offset magenta and cyan copies of the core on 24 Hz steps, now and then, with a scanline tear.
            if(!reduced&&H(seed+step,13)>.52f)
            {
                float off=(.035f+.04f*H(seed+step,17))*sc;s.Sprite(false,at+side*off,Lin(Magenta,2.4f),core*1.7f,Hot);s.Sprite(false,at-side*off,Lin(Cyan,2.4f),core*1.7f,Hot);
                if(!low&&H(seed+step,19)>.5f){var y=up*((H(seed+step,23)-.5f)*.12f*sc);s.Line(false,at+y-side*(.2f*sc),at+y+side*(.2f*sc),Lin(HotWhite,1.6f),.012f*sc,false);}
            }
            // The ember tail: pixel cubes left where the ball passed, drifting up and out and shrinking.
            int n=reduced?5:low?4:12;int last=(int)Math.Floor(age/EmberStep);
            for(int k=0;k<n;k++)
            {
                int e=last-k;if(e<0)break;float te=e*EmberStep,ea=age-te;if(ea>EmberLife)break;float u=ea/EmberLife;long es=seed*31+e;
                var p=spawn+dir*(speed*te)+hand*(1-Smooth(te/Converge));
                var drift=Vector3.Normalize(new Vector3(H(es,3)-.5f,H(es,5)*.6f+.1f,H(es,7)-.5f))*(.7f*ea)+Up2*(.35f*ea);
                float z=(.05f+.03f*H(es,11))*sc*(1-u);var c=e%3==0?HotWhite:e%3==1?Magenta:Cyan;
                s.Voxel(false,WeaponShape.Cube,p+drift+side*((H(es,13)-.5f)*.12f*sc),Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(es,17),1,H(es,19))),ea*9+e),new Vector3(z,z,z),Lin(c,2.6f*(1-u*.6f)));
            }
            // The Overflow: code bytes orbiting it.
            if(kind==2)
            {
                int bytes=reduced?2:4;
                for(int i=0;i<bytes;i++){float a=i*MeleeMath.TAU/bytes+age*(reduced?6:14);var o=(side*(float)Math.Cos(a)+up*(float)Math.Sin(a))*(.3f*sc*.65f);float z=.05f*sc;s.Voxel(false,WeaponShape.Cube,at+o,Quaternion.CreateFromAxisAngle(dir,a),new Vector3(z,z*.6f,z*.25f),Lin(i%2==0?HotWhite:Cyan,2.8f));}
                s.Sprite(false,at,Lin(Violet,1.4f*fl),body*1.35f,RingSprite);
            }
        }

        /// <summary>The scene's fireballs: each replicated (or previewed) shot of Spec cyberlord drawn as a Plasma Byte, eased off
        /// the throwing hand over its first moments.</summary>
        private void PlasmaShots(ICyberFxSink s,ICyberFxScene scene,ICyberShots shots,float time,float q,bool low)
        {
            for(int i=0;i<shots.CyberShots;i++)
            {
                var b=shots.CyberShotAt(i);if(b.Slot!=CyberRangedSpec.Bolt)continue;float speed=b.Velocity.Length();if(speed<1e-3f)continue;var dir=b.Velocity/speed;float age=Math.Max(0,time-b.Born);
                var start=b.Position-b.Velocity*age;var hand=age<Converge?HandOffset(scene,b.Owner,b.Born,start,dir,time):Vector3.Zero;
                var at=b.Position+hand*(1-Smooth(age/Converge));
                Fireball(s,at,start,hand,dir,speed,age,b.Kind,time,b.Id*131L,q,low);
            }
        }
        // The throwing hand's emitter at the release minus the replicated start: the owner's throw on its view (the newest one,
        // released with this shot), its hand (both for an Overflow: their middle).
        private Vector3 HandOffset(ICyberFxScene scene,int owner,float born,Vector3 start,Vector3 dir,float time)
        {
            var actors=scene.Actors;Actor a=null;for(int i=0;i<actors.Count;i++)if(actors[i]!=null&&actors[i].Id==owner){a=actors[i];break;}
            if(a==null||!CyberLordFeed.Read(a,other))return Vector3.Zero;int hand;float release;
            if(other.Kit2R.Live){release=other.Kit2R.Release[CyberKit2Ranged.Plasma];hand=other.Kit2R.Hand;}else{release=other.RRelease[CyberRangedSpec.Bolt];hand=other.BoltHand;}
            if(!(Math.Abs(born-release)<.05f))return Vector3.Zero;
            bool me=scene.Viewer==owner;Vector3 l,r;bool hl=me?scene.ViewFist(true,out l):scene.Fist(a,true,out l),hr=me?scene.ViewFist(false,out r):scene.Fist(a,false,out r);
            Vector3 at;if(hand==2){if(!hl||!hr)return Vector3.Zero;at=(l+r)*.5f;}else if(hand==1){if(!hl)return Vector3.Zero;at=l;}else{if(!hr)return Vector3.Zero;at=r;}
            at+=dir*(me?.05f:.1f);var off=at-start;return off.LengthSquared()<3*3?off:Vector3.Zero;
        }

        /// <summary>The cast flash where a throw leaves the hand(s), and the fireballs held over the palms in ranged mode.</summary>
        private void PlasmaHands(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me,Vector3 aim)
        {
            bool reduced=q<.5f;float mode=v.ModeWeight(time);if(mode<=1e-3f||!a.Alive)return;
            Vector3 lf,rf;bool hasL=me?scene.ViewFist(true,out lf):scene.Fist(a,true,out lf),hasR=me?scene.ViewFist(false,out rf):scene.Fist(a,false,out rf);if(!hasL||!hasR)return;
            CyberRangedPose.Hands(v,time,out float loadR,out float loadL,out float formR,out float formL);float view=me?.8f:1;int step=(int)(time*24);
            // The cast flash (the newest throw's release).
            bool k2=v.Kit2R.Live;float rel=k2?v.Kit2R.Release[CyberKit2Ranged.Plasma]:v.RRelease[CyberRangedSpec.Bolt];bool fired=k2?v.Kit2R.Released(CyberKit2Ranged.Plasma):v.RReleased(CyberRangedSpec.Bolt);
            int hand=k2?v.Kit2R.Hand:v.BoltHand;float age=time-rel;
            if(fired&&age>=0&&age<CastFlash)
            {
                float k=1-age/CastFlash,big=hand==2?1.5f:1;long seed=a.Id*7919L+(long)Math.Round(rel*240);
                var at=(hand==2?(lf+rf)*.5f:hand==1?lf:rf)+aim*(me?.05f:.12f);
                s.Sprite(me,at,Lin(HotWhite,(reduced?1.8f:2.8f)*k),(.28f*big)*view*(.6f+.4f*k),Hot);s.Sprite(me,at,Lin(Magenta,2*k),(.12f+.42f*Smooth(1-k))*big*view,RingSprite);
                if(!me)s.Glow(at,Lin(Plasma,.6f*k),0,.55f*big,Core,1-k,seed);
                var side=Across(aim);var up=Vector3.Normalize(Vector3.Cross(aim,side));int fan=low?3:5;
                for(int i=0;i<fan;i++)
                {
                    float t=fan==1?0:i/(fan-1f)-.5f,len=(.42f+.1f*H(seed+i,3))*big*view*Smooth(Math.Min(1,age/.04f+.3f))*(.4f+.6f*k);
                    var dir=Vector3.Normalize(aim+side*(t*.8f)+up*(.15f*H(seed+i,5)-.05f));Flame(s,me,at,at+dir*len,i%2==0?Lin(Magenta,2*k):Lin(Cyan,2.2f*k),.12f*big*view*k,H(seed+i+step,7));
                }
                if(!me&&!low)for(int i=0;i<(reduced?3:7);i++){var dir=Vector3.Normalize(aim*1.2f+side*(H(seed+i,11)-.5f)*1.4f+up*(H(seed+i,13)-.3f));float sp=4+5*H(seed+i,17);s.Streak(at+dir*(sp*age),dir*sp,Lin(i%2==0?HotWhite:Magenta,2.2f*k),.02f,.1f,.02f,.9f,1-k,seed+i);}
            }
            // The held fireballs over the palms (the loaded hand's full, the forward hand's a smaller flame).
            for(int h=0;h<2;h++)
            {
                bool left=h==1;float load=left?loadL:loadR,form=left?formL:formR,size=(.5f+.5f*load)*form*mode;if(size<=1e-3f)continue;long seed=a.Id*977L+h*31;
                var fist=left?lf:rf;var at=fist+Up2*((me?.06f:.075f)+(me?.03f:.04f)*load)+aim*(me?.03f:.06f);float fl=reduced?1:.85f+.3f*H(seed+step,3),g=view*size;
                s.Sprite(me,at,Lin(Magenta,1.9f*fl),.22f*g,Soft);s.Sprite(me,at,Lin(Cyan,2.2f),.12f*g,Hot);s.Sprite(me,at,Lin(HotWhite,3.2f),.055f*g,Hot);
                var sway=reduced?Vector3.Zero:new Vector3(H(seed+step,5)-.5f,0,H(seed+step,7)-.5f)*(.05f*g);
                Flame(s,me,at-Up2*(.02f*g),at+Up2*(.2f*g)+sway,Lin(Magenta,1.8f*fl),.11f*g,reduced?.4f:H(seed+step,9));
                if(!low)Flame(s,me,at,at+Up2*(.12f*g)-sway*.6f,Lin(Cyan,2f),.06f*g,reduced?.6f:H(seed+step,11));
                if(!me&&!low&&!reduced)for(int i=0;i<2;i++){float u=MeleeMath.Frac(time*1.6f+i*.5f+H(seed,13));var p=at+Up2*(.05f+.3f*u)*size+new Vector3(H(seed+i+(int)(time*1.6f+i*.5f+H(seed,13)),15)-.5f,0,H(seed+i,17)-.5f)*(.08f*size);float z=.025f*(1-u)*size;s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Up2,u*6+i),new Vector3(z,z,z),Lin(i==0?Magenta:HotWhite,2.4f*(1-u)));}
                if(!me&&!low)s.Glow(at,Lin(Plasma,.32f*size*fl),0,.32f*size,Mote,MeleeMath.Frac(time*.7f+h*.5f),seed);
            }
        }

        /// <summary>A fireball's splash at `at` (normal `normal` toward where it came from), `age` s after it landed: kind as
        /// Fireball's; `pilot` when it struck a pilot (no scorch on a body); `splash` the splash radius drawn as a fading shell
        /// (0: none, today's Bit Bolt has no splash).</summary>
        private void PlasmaSplash(ICyberFxSink s,Vector3 at,Vector3 normal,float age,int kind,bool pilot,float splash,long seed,bool reduced,bool low)
        {
            float sc=KindScale[Math.Max(0,Math.Min(2,kind))],life=SplashLife*(kind==2?1.25f:1),scorch=pilot?0:.8f;if(age<0||age>=Math.Max(life,scorch))return;
            float u=age/life,k=1-Smooth(u),dist=Vector3.Distance(at,eye),boost=Math.Max(1,dist*.012f/sc);int step=(int)(age*24);
            if(normal.LengthSquared()<1e-6f)normal=Up2;normal=Vector3.Normalize(normal);var side=Across(normal);var up=Vector3.Normalize(Vector3.Cross(normal,side));if(Math.Abs(normal.Y)>.9f){side=Vector3.UnitX;up=Vector3.UnitZ;}
            if(u<1)
            {
                // The burst: a white flash, a plasma glow swelling, a hex ring snapping out.
                float flash=reduced?.7f*(1-Smooth(u/.4f)):1-Smooth(u/.22f);
                s.Sprite(false,at+normal*.05f,Lin(HotWhite,4.2f*flash),.95f*sc*boost,Hot);s.Glow(at+normal*.1f,Lin(Plasma,1.4f*k),0,(1f+1.7f*Smooth(u*3))*sc*boost,Core,u,seed);
                s.Sprite(false,at+normal*.04f,Lin(Cyan,2.4f*k),(.3f+1.4f*Smooth(u*4))*sc*boost,RingSprite);s.Sprite(false,at+normal*.06f,Lin(Magenta,2.2f*k),(.5f+.9f*Smooth(u*2.5f))*sc*boost,Soft);
                // Plasma petals blown out round the normal.
                int petals=reduced?5:low?5:9;
                for(int i=0;i<petals;i++)
                {
                    float a=i*MeleeMath.TAU/petals+H(seed+i,3)*.6f,lift=.35f+.5f*H(seed+i,5);var dir=Vector3.Normalize(normal*lift+(side*(float)Math.Cos(a)+up*(float)Math.Sin(a))*(1-lift*.4f));
                    float len=(.25f+.75f*Smooth(Math.Min(1,u*7)))*sc*(1-.35f*u)*(.8f+.4f*H(seed+i,7));Flame(s,false,at+normal*.03f,at+normal*.03f+dir*len,i%2==0?Lin(Magenta,2.4f*k):Lin(Cyan,2.4f*k),.22f*sc*(1-u*.7f),reduced?.5f:H(seed+i+step,9));
                }
                // Hex shards: flat pixel plates flung out and falling.
                int shards=reduced?4:low?4:9;
                for(int i=0;i<shards;i++)
                {
                    float a=i*2.39996f+H(seed+i,11);var dir=Vector3.Normalize(normal*(.6f+.6f*H(seed+i,13))+(side*(float)Math.Cos(a)+up*(float)Math.Sin(a)));float sp=(2.2f+2.6f*H(seed+i,17))*sc;
                    var p=at+dir*(sp*age)-Up2*(4.5f*age*age);if(p.Y<at.Y-.4f)continue;float z=(.07f+.03f*H(seed+i,19))*sc*(1-u*.8f);
                    s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(seed+i,23),1,H(seed+i,29))),age*14+i),new Vector3(z,z,z*.22f),Lin(i%3==0?HotWhite:i%3==1?Magenta:Cyan,2.6f*k));
                }
                // Kit v2's splash: a fading shell out to its radius.
                if(splash>0)s.Sprite(false,at+normal*.1f,Lin(Plasma,1.3f*(1-u)*(1-u)),2*splash*Smooth(Math.Min(1,u*2.2f)),RingSprite);
            }
            // The scorch flicker: upright flame licks and a wisp of smoke where it hit the world.
            if(scorch>0&&age<scorch)
            {
                float v=age/scorch,f=1-v;bool on=reduced||H(seed+step,31)>.25f;
                if(on)for(int i=0;i<(low?1:3);i++){var b=at+normal*.03f+side*((H(seed+i,37)-.5f)*.3f*sc)+up*((H(seed+i,41)-.5f)*.2f*sc);float h=(.12f+.22f*H(seed+i+step,43))*sc*f;Flame(s,false,b,b+Up2*h,Lin(i==0?Magenta:Plasma,1.7f*f),.08f*sc*f,H(seed+i+step,47));}
                if(!low&&v<.7f)s.Smoke(at+normal*.1f+Up2*(.35f*v),Lin(0x2a2338),.32f*(1-v/.7f),(.35f+.4f*v)*sc,v,seed+3,at.Y-.2f);
            }
        }
    }
}
