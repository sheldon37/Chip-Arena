using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV animation classes' own entrances (owner 2026-11-12, the DEV animation picks: "entrance → Intro"): the pack's Intro clip on the
    /// model, in place of the base class's entrance (the Colossus Oath, Crimson Moon). The clip plays once at its own pace from StartAt (its
    /// first frame held until then), then the class's stance under the hero shot and the name card; a keyed stance under it for the Classic
    /// body. The runtime lays the clip on the intro body through the arena's motion (Core NativeVisualMotion.Choose, whatever the set), the
    /// weapon held as the match holds it (NativeDevMotions.Grip).
    /// Each is a cinematic of its own (owner 2026-11-13: "Lets also add more camera work and effects to DEV Katana and DEV Greatsword ... I
    /// mean for there intros"): a camera cut to the clip's beats, the scene slowed through its great cut, and effects on those beats that
    /// follow the blade as the model really holds it (Blade: the runtime hands it in each frame, Line its edge in the weapon's frame): its
    /// trail, the sparks and the score where it meets the floor, the light it catches. A checker that never hands the blade in sees the
    /// entrance without those. Presentation only: functions of the entrance clock and of the blade handed in; the entrance's length, its
    /// name card and the clip's pace are what they were.
    /// </summary>
    public abstract class NativeDevPackIntro:NativeBladeIntro
    {
        private readonly string spec,clip,idle;private readonly float clipLength,startAt,total,cardAt,end;
        /// <summary>`slow`: when the scene slows (the script's Hit: just before its great cut is fastest).</summary>
        protected NativeDevPackIntro(string style,string spec,string clip,float clipLength,string idle,float startAt,float slow,float total,float cardAt,float end,int accent):base(style,slow,accent)
        {
            this.spec=spec;this.clip=clip;this.clipLength=clipLength;this.idle=idle;this.startAt=startAt;this.total=total;this.cardAt=cardAt;this.end=end;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.25f,0,-.2f);
            k.Key(slow,stand).Root(0,.84f,.05f,Facing).Planted(1).Drop(-.06f).J(B,.1f).J(RA,-1.2f,0,-.5f).J(LA,-.4f,0,.4f).ImpactKey();
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
        /// <summary>
        /// How far the clip carries the weapon off the hand at entrance time `t`: 1 wholly on the body (its frame in the chest bone's,
        /// CarryFrame), 0 in the hand as the match holds it. A pack Intro may open with the weapon elsewhere than the right fist (the
        /// katana's across the back, the hand reaching over the shoulder for its hilt); the hold alone hung it from the open hand (owner
        /// 2026-11-12: "On dev katana intro, the katana isn't held correctly~ the hilt is not in hand").
        /// </summary>
        public virtual float Carried(float t)=>0;
        /// <summary>The carried weapon's frame in the model's chest bone frame (Unity axes; false: the clip never carries it).</summary>
        public virtual bool CarryFrame(out Vector3 at,out Quaternion turn){at=Vector3.Zero;turn=Quaternion.Identity;return false;}

        // ---- the blade ----
        /// <summary>The class's blade in its weapon's frame (where it leaves the hilt, and its tip), for the runtime to hand in where the
        /// model holds it: the base class's own (the Katana's sockets, the Greatsword's design: NativeWeaponModels' BladeBase and BladeTip).</summary>
        public bool Line(bool left,out Vector3 root,out Vector3 tip)
        {
            if(spec==NativeDevMotions.Greatsword&&!left){root=new Vector3(0,0,.262f);tip=new Vector3(0,0,1.685f);return true;}
            return NativeBladeView.Line(NativeDevMotions.Base(spec)??spec,left,out root,out tip);
        }
        /// <summary>A blade entrance's blade line: a DEV animation class's by its base class, every other by its own spec.</summary>
        public static bool BladeLine(NativeBladeIntro intro,bool left,out Vector3 root,out Vector3 tip)
        {
            if(intro is NativeDevPackIntro dev)return dev.Line(left,out root,out tip);
            return NativeBladeView.Line(intro.Spec,left,out root,out tip);
        }

        // ---- the camera's shakes ----
        /// <summary>The player's Camera shake setting (Runtime DirectedIntro sets it each frame): off, the entrance's camera never shakes.</summary>
        public bool Shake=true;
        /// <summary>A small, short shake from `at`: `size` m at its start, dying over `decay` s and wholly still four of those on; none
        /// with the setting off.</summary>
        protected Vector3 Jolt(float t,float at,float size,float decay)
        {
            if(!Shake||t<=at||t>=at+decay*4)return Vector3.Zero;float s=NativeEntranceMath.Pulse(t,at,.01f,decay)*size*(1-NativeEntranceMath.Ramp(t,at+decay*2.5f,at+decay*4));
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }

        // ---- where the blade meets the floor (taken from the blade handed in; a clock that runs back starts over) ----
        protected const int MarkMax=24;protected static float Floor=>NativeEntranceStage.FloorY;
        private readonly Vector3[] markAt=new Vector3[MarkMax],markDir=new Vector3[MarkMax];private readonly float[] markTime=new float[MarkMax];private int markCount;private float followClock=-1;
        protected int MarkCount=>markCount;
        protected void Mark(int i,out Vector3 at,out Vector3 dir,out float time){at=markAt[i];dir=markDir[i];time=markTime[i];}
        /// <summary>Whether mark `i` carries on from the one before it (one stroke along the floor).</summary>
        protected bool Joined(int i)=>i>0&&markTime[i]-markTime[i-1]<.1f&&Vector3.DistanceSquared(markAt[i],markAt[i-1])<1.6f*1.6f;
        /// <summary>The blade's tip velocity this frame (false: not handed in, or its first frame).</summary>
        protected bool TipVelocity(float t,out Vector3 tip,out Vector3 velocity)
        {
            velocity=Vector3.Zero;if(!Held(t,0,out _,out tip)||HeldCount(0)<2)return false;
            HeldSample(0,1,out _,out var before,out float at);float dt=t-at;if(dt<1e-4f||dt>.12f)return false;velocity=(tip-before)/dt;return true;
        }
        /// <summary>The script's own memory of the blade, cleared when the clock runs back.</summary>
        protected virtual void Rewind(){}
        /// <summary>What the script takes from the blade this frame (after the marks).</summary>
        protected virtual void Watch(float t){}
        /// <summary>Reads the blade handed in for `t` (once a frame, by Props and Effects alike): a mark where its tip passes within `reach`
        /// of the floor on the move (through the floor: where the blade crosses it).</summary>
        protected void Take(float t,float reach)
        {
            if(t<followClock-1e-4f){markCount=0;Rewind();}
            if(Math.Abs(t-followClock)<1e-6f)return;followClock=t;
            if(t>=BladeFrom&&Held(t,0,out var root,out var tip)&&TipVelocity(t,out _,out var v)&&tip.Y<=Floor+reach&&markCount<MarkMax)
            {
                var p=tip;if(tip.Y<Floor&&root.Y>Floor)p=Vector3.Lerp(root,tip,(root.Y-Floor)/(root.Y-tip.Y));p.Y=Floor;
                v.Y=0;float speed=v.Length();
                if(speed>1.5f&&(markCount==0||Vector3.DistanceSquared(markAt[markCount-1],p)>.05f*.05f)){markAt[markCount]=p;markDir[markCount]=v/speed;markTime[markCount]=t;markCount++;}
            }
            Watch(t);
        }
        /// <summary>Sparks thrown from the marks along the blade's way (`each` a mark, born along its stroke): hot to cool, falling, one bounce.</summary>
        protected void FloorSparks(INativeEntranceFx fx,float t,float q,int each,float speed,float life,Vector3 hot,Vector3 cool,float width,int seed)
        {
            for(int k=0;k<markCount;k++)
            {
                if(t-markTime[k]<0||t-markTime[k]>life+.1f)continue;bool joined=Joined(k);var d=markDir[k];var side=new Vector3(-d.Z,0,d.X);
                for(int i=0;i<each;i++)
                {
                    int id=k*32+i;if(NativeEntranceMath.Rand(seed^0x51f1,id)>Math.Max(.3f,q))continue;
                    float w=joined?NativeEntranceMath.Rand(seed,id*5):0;var from=Vector3.Lerp(markAt[k],joined?markAt[k-1]:markAt[k],w);float born=NativeEntranceMath.Lerp(markTime[k],joined?markTime[k-1]:markTime[k],w);
                    float a=t-born,l=life*(.45f+.55f*NativeEntranceMath.Rand(seed,id*5+1));if(a<0||a>l)continue;
                    var v=d*(speed*(.2f+.7f*NativeEntranceMath.Rand(seed,id*5+2)))+side*(speed*.4f*NativeEntranceMath.Signed(seed,id*5+3))+new Vector3(0,speed*(.3f+.7f*NativeEntranceMath.Rand(seed,id*5+4)),0);
                    var p=from+v*a+new Vector3(0,-4.9f*a*a,0);var vel=v+new Vector3(0,-9.8f*a,0);if(p.Y<Floor){p.Y=Floor+(Floor-p.Y)*.3f;vel.Y=-vel.Y*.3f;}
                    float u=a/l;fx.Streak(p,vel,Vector3.Lerp(hot,cool,NativeEntranceMath.Smooth(u*1.4f))*((1-u)*(1-u*.5f)),width,width*1.6f,.035f,.85f,u,seed+id);
                }
            }
        }
        /// <summary>The score the blade leaves along its marks: a line on the floor, hot when cut and cooling over `life` (`glow`: its light on the floor round it).</summary>
        protected void Score(INativeEntranceFx fx,float t,float life,Vector3 hot,Vector3 cool,float width,float glow,int seed)
        {
            for(int k=1;k<markCount;k++)
            {
                if(!Joined(k))continue;float age=t-markTime[k];if(age<0||age>life)continue;float u=age/life,heat=(1-u)*(1-u);
                var a=markAt[k-1]+new Vector3(0,.012f,0);var b=markAt[k]+new Vector3(0,.012f,0);var c=Vector3.Lerp(hot,cool,NativeEntranceMath.Smooth(u*4))*heat;
                // Past both ends a little, so the strokes join.
                var along=b-a;fx.Streak(b+along*.08f,along*1.16f,c,width*(1-.4f*u),0,1,0,u,seed+k);
                if(glow>0&&k%2==1)fx.Glow((a+b)*.5f+new Vector3(0,.04f,0),cool*(glow*heat),0,.34f,0,u,seed+64+k);
            }
        }

        // ---- the blade's trail ----
        private static Vector3 Spline(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float f){float f2=f*f,f3=f2*f;return .5f*(2*p1+(p2-p0)*f+(2*p0-5*p1+4*p2-p3)*f2+(3*p1-p0-3*p2+p3)*f3);}
        /// <summary>When the blade handed in is the blade the model swings (entrance s): from the clip's start; later for a clip that
        /// opens with the weapon carried off the hand.</summary>
        protected virtual float BladeFrom=>startAt;
        /// <summary>How fast the entrance clock runs at `t` beside the age's (1, but slowed through the great cut): what lasts a time on
        /// screen is measured on the clock by it.</summary>
        protected float Pace(float t)
        {
            if(t<=Hit||SlowRate>=1)return 1;float held=Hit+SlowRate*SlowHold;
            return SlowRate+(1-SlowRate)*NativeEntranceMath.Ramp(t,held,held+SlowRecover*.6f);
        }
        /// <summary>
        /// The blade's sweep: the edge's line along the tip's path and under it one broad, soft ribbon along the path of a point
        /// `ribbonAt` of the way up the blade (`ribbon` m wide, `sheet` its level; none at 0), over the last `length` m the tip came and
        /// the last `life` s on screen, and only where the tip moved at `from` m/s or more (full at twice that): a crescent behind a
        /// cut, nothing on a blade at rest. Drawn on a curve through the frames the blade was handed in at (a great sword's swing
        /// crosses the stage in a tenth of a second: straight between its frames it was a polygon). The edge's line in short pieces
        /// (7 cm: longer ones stood off a tight curve as a comb of tangents) that overlap, so it has no joints (a streak fades to
        /// its ends); the ribbon, soft and broad, in pieces three times as long.
        /// </summary>
        protected void Sweep(INativeEntranceFx fx,float t,float length,float life,float from,Vector3 tint,Vector3 hot,float width,float ribbon,float ribbonAt,float sheet,int seed,bool low)
        {
            int c=HeldCount(0);if(c<2)return;int most=low?6:16;float run=0;life*=Pace(t);
            for(int j=0;j<c-1&&run<length;j++)
            {
                HeldSample(0,j,out var r1,out var p1,out float t1);HeldSample(0,j+1,out var r2,out var p2,out float t2);if(t-t1>=life||t2<BladeFrom)break;float dt=t1-t2;if(dt>.12f||dt<1e-5f)continue;
                float far=Vector3.Distance(p1,p2);if(far<1e-4f)continue;float fast=NativeEntranceMath.Smooth((far/dt-from)/from);if(fast<=0){run+=far;continue;}
                HeldSample(0,Math.Max(0,j-1),out var r0,out var p0,out _);HeldSample(0,Math.Min(c-1,j+2),out var r3,out var p3,out _);
                int n=Math.Max(1,Math.Min(most,(int)(far/.07f)+1));var lastTip=p1;var ribbonFrom=Vector3.Lerp(r1,p1,ribbonAt);
                for(int i=1;i<=n;i++)
                {
                    float w=i/(float)n;var p=Spline(p0,p1,p2,p3,w);var r=Spline(r0,r1,r2,r3,w);
                    float f=Math.Min(1-(t-NativeEntranceMath.Lerp(t1,t2,w))/life,1-(run+far*w)/length);if(f<=0)break;float k=f*f*fast;
                    var d=lastTip-p;if(d.LengthSquared()>1e-8f)fx.Streak(lastTip+d*.3f,d*1.6f,Vector3.Lerp(tint,hot,f)*(1.5f*k),width*(.45f+.55f*f),0,1,0,1-f,seed+j*16+i);
                    if(ribbon>0&&(i%3==0||i==n)){var to=Vector3.Lerp(r,p,ribbonAt);var bd=ribbonFrom-to;if(bd.LengthSquared()>1e-8f)fx.Streak(ribbonFrom+bd*.6f,bd*2.2f,tint*(sheet*k),ribbon*(.6f+.4f*f),0,1,0,1-f,seed+2048+j*16+i);ribbonFrom=to;}
                    lastTip=p;
                }
                run+=far;
            }
        }
        /// <summary>Motes shed by the blade where it was fastest (`from` m/s on): flung on along its way, falling, gone in `life` s.</summary>
        protected void Shed(INativeEntranceFx fx,float t,float from,float life,float carry,float gravity,Vector3 color,float size,int most,int seed)
        {
            int c=HeldCount(0),shed=0;
            for(int j=0;j+1<c&&shed<most;j++)
            {
                HeldSample(0,j,out _,out var p0,out float t0);HeldSample(0,j+1,out _,out var p1,out float t1);float age=t-t0;if(age>life)break;float dt=t0-t1;if(dt<1e-4f||dt>.12f)continue;
                if(t1<BladeFrom)break;var v=(p0-p1)/dt;if(v.LengthSquared()<from*from)continue;shed++;
                // Where it left the blade: somewhere along that frame's stroke (a fixed place for the frame), so they do not line up on the frames.
                float w=NativeEntranceMath.Rand(seed,(int)(t0*977)&0xffff);var p=Vector3.Lerp(p0,p1,w)+v*(carry*age)+new Vector3(0,-.5f*gravity*age*age,0);if(p.Y<Floor)continue;
                fx.Glow(p,color*(1-age/life),0,size,0,age/life,seed+j);
            }
        }
        /// <summary>A glint running up the blade from its hilt to its tip between `from` and `to` (the light it catches as it turns).</summary>
        protected void EdgeGlint(INativeEntranceFx fx,float t,float from,float to,Vector3 color,float size,int seed)
        {
            if(t<from||t>to||!Held(t,0,out var root,out var tip))return;float u=(t-from)/(to-from),g=(float)Math.Sin(Math.PI*u);var at=Vector3.Lerp(root,tip,NativeEntranceMath.Smooth(u));
            var along=tip-root;if(along.LengthSquared()<1e-6f)return;along=Vector3.Normalize(along);var across=Vector3.Cross(along,Vector3.UnitZ);across=across.LengthSquared()<1e-4f?Vector3.UnitX:Vector3.Normalize(across);
            NativeEntranceFx.Glint(fx,at,color*g,size*(.5f+.5f*g),along,across,seed);
        }
        /// <summary>A four-point star on the blade's tip, swelling and gone over `from`..`to`.</summary>
        protected void TipStar(INativeEntranceFx fx,float t,float from,float to,Vector3 color,float size,int seed)
        {
            if(t<from||t>to||!Held(t,0,out _,out var tip))return;float g=NativeEntranceMath.Bump(t,from,to);
            NativeEntranceFx.Glint(fx,tip,color*g,size*(.4f+.6f*g),Vector3.UnitX,Vector3.UnitY,seed);
        }
    }

    /// <summary>
    /// "Heavy Turn", the DEV Greatsword's entrance (the great sword set's `Intro`, 2.0 s; 4.8 s in all, the name at 3.2 s). Dark, an ember
    /// light from above on its turned back; embers rising. It heaves the great sword overhead (0.6 s), comes round with it and the blade
    /// bites the floor (1.1 s: sparks at the lens, a score left glowing in the floor, grit thrown), winds up (the embers drawn in), and
    /// the second swing, the beat (1.67 s), goes through slowed: a ribbon of ember light, a wave of light over the floor, the stage waking.
    /// Then the blade laid on the shoulder as the camera comes round to the front.
    /// </summary>
    public sealed class NativeDevGreatswordIntro:NativeDevPackIntro
    {
        public const string Name="Heavy Turn",Music="audio/intros/neon-drop",Blurb="Back turned, then round with the great sword: two great swings and the blade on the shoulder.";
        public const float Total=4.8f,CardAt=3.2f,End=4.45f,ClipAt=.4f,ClipLength=2f,Beat=ClipAt+1.27f;
        /// <summary>The clip's other beats (entrance s): the blade at the top of the heave, biting the floor in the first swing, the
        /// wind-up, the scene slowing into the second swing, the blade coming to rest on the shoulder.</summary>
        public const float HeaveAt=ClipAt+.2f,BiteAt=ClipAt+.68f,WindAt=ClipAt+1.07f,SlowAt=ClipAt+1.215f,RestAt=ClipAt+1.8f;
        private static readonly Vector3 White=Vector3.One,Gold=NativeEntranceFx.Hex(0xffcb87),Ember=NativeEntranceFx.Hex(0xff8a3c),Deep=NativeEntranceFx.Hex(0xd2401a),Dust=NativeEntranceFx.Hex(0x8f8a80),Stone=NativeEntranceFx.Hex(0x2c3140);
        public NativeDevGreatswordIntro():base(NativeDevMotions.GreatswordEntrance,NativeDevMotions.Greatsword,NativeDevGreatswordMotion.Intro,ClipLength,NativeDevGreatswordMotion.Idle,ClipAt,SlowAt,Total,CardAt,End,0xffcb87)
        {
            SlowRate=.2f;SlowHold=.2f;SlowRecover=.3f;SlowCatchUp=1.05f;
            Shots=new[]
            {
                // Low in front, looking up its turned back as the great sword is heaved overhead: a slow push in.
                Shot(0,NativeEntranceCut.Cut,42,37,V(1.0f,.42f,4.5f),V(.7f,.5f,3.7f),V(0,1.25f,0),V(0,1.5f,0),0,0),
                // On the floor at its right as it comes round: the blade bites the floor a stride from the lens (the tip meets it 1.7 m
                // out on that side and drags away behind); drifting back from it.
                Shot(ClipAt+.52f,NativeEntranceCut.Cut,47,44,V(-2.55f,.28f,1.95f),V(-2.95f,.34f,2.3f),V(-.3f,.8f,-.2f),V(-.2f,.9f,-.2f),1,0),
                // Whipped round to its left, low and close, pushing in through the wind-up: the second swing starts over this lens.
                Shot(WindAt-.07f,NativeEntranceCut.Whip,40,35,V(2.65f,.7f,2.95f),V(2.2f,.75f,2.5f),V(0,1.15f,0),V(0,1.15f,0),2,0,.12f),
                // The second swing, slowed, sweeping across the front and away: the same eye thrown back and wide by it.
                Shot(SlowAt,NativeEntranceCut.Cut,35,47,V(2.2f,.75f,2.5f),V(2.75f,.9f,3.25f),V(0,1.15f,0),V(0,1.05f,0),1,0),
                // Rising round to the front as the blade is brought to rest, into the hero frame.
                Orbit(RestAt-.2f,NativeEntranceCut.Blend,45,36,40,-13.65f,4.2f,3.602f,.85f,1.04f,V(0,1.1f,0),V(-.08f,1.24f,0),0,0,.35f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.04f,3.5f),V(-.66f,1.06f,3.25f),V(-.08f,1.24f,0),V(-.1f,1.26f,0),1,0),
            };
        }
        public override Vector3 CameraJolt(float t)=>Jolt(t,BiteAt,.013f,.22f)+Jolt(t,Beat,.02f,.4f)+Jolt(t,RestAt,.005f,.18f);
        public override void Lights(float t,NativeEntranceLights l)
        {
            // Dark, the ember light from above and behind on its back; the stage comes up after the beat.
            float up=NativeEntranceMath.Ramp(t,Beat+.15f,CardAt);l.DriveProbe=true;NativeEntranceStage.Rig(l,.12f+.78f*up,Accent,.9f,.3f*up,.2f+.8f*up);
            l.Add(new Vector3(-.2f,4.2f,-.5f),(Gold+Ember*.6f)*(8*(1-.6f*up)),9);l.Add(new Vector3(2.8f,1.5f,2.4f),Ember*(2.2f*up),8);
            // The bite: the sparks' light on the floor, and the score's as it cools.
            if(MarkCount>0)
            {
                Mark(0,out var first,out _,out float at);Mark(MarkCount-1,out var last,out _,out _);var mid=(first+last)*.5f+new Vector3(0,.25f,0);
                float flash=NativeEntranceMath.Pulse(t,at,.01f,.3f),cool=1-NativeEntranceMath.Ramp(t,at,at+3f);l.Add(mid,(White+Ember*2)*(6*flash)+Deep*(1.3f*cool),5);
            }
            float beat=NativeEntranceMath.Pulse(t,Beat,.012f,.45f);if(beat>.01f)l.Add(new Vector3(0,1.25f,0)+Local(0,0,.8f),(Gold*1.6f+White)*(7*beat),8);
        }
        protected override float Pylon(int i,float t)=>t<Beat?.1f:PowerOn(i,t-Beat+.05f,.1f);
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Take(t,.1f);Stage(props,t,.1f+.45f*NativeEntranceMath.Ramp(t,Beat,CardAt),Beat);
            // Grit the bite throws out of the floor: chips tumbling along the blade's way, one bounce, gone.
            int each=q<.5f?1:3;
            for(int k=0;k<MarkCount;k++)
            {
                Mark(k,out var at,out var dir,out float born);float a=t-born;if(a<0||a>1.3f)continue;var side=new Vector3(-dir.Z,0,dir.X);
                for(int i=0;i<each;i++)
                {
                    int id=k*8+i;var v=dir*(1.2f+2.2f*NativeEntranceMath.Rand(23600,id))+side*(1.3f*NativeEntranceMath.Signed(23610,id))+new Vector3(0,1.6f+2.2f*NativeEntranceMath.Rand(23620,id),0);
                    // Up and over, one low bounce where it lands, then still.
                    float land=v.Y/4.9f,b=a-land;var p=at+v*Math.Min(a,land)+new Vector3(0,.02f+(a<land?v.Y*a-4.9f*a*a:0),0);
                    if(b>0){float hop=Math.Min(b,.12f*v.Y/4.9f*2);p+=new Vector3(v.X,0,v.Z)*(.4f*hop);p.Y+=Math.Max(0,.12f*v.Y*b-4.9f*b*b);}
                    float size=(.025f+.035f*NativeEntranceMath.Rand(23630,id))*(1-NativeEntranceMath.Ramp(a,.9f,1.3f));
                    props.Piece(WeaponShape.Cube,false,p,Quaternion.CreateFromAxisAngle(NativeEntranceMath.Direction(23640,id),a*(6+8*NativeEntranceMath.Rand(23650,id))),new Vector3(size,size*.6f,size*1.3f),Stone);
                }
            }
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Take(t,.1f);bool low=q<.5f;
            // The air: embers rising through the ember light.
            float up=NativeEntranceMath.Ramp(t,Beat+.15f,CardAt);
            NativeEntranceFx.Embers(fx,t,-3,Total,new Vector3(0,Floor+.04f,0),3.4f,low?5:13,.5f,2.8f,Ember*1.7f,.034f,23320,q,.3f);
            Beams(fx,t,q,Accent,up);
            // The great sword's sweep: a broad ribbon of ember light behind the blade, its edge gold-white; embers shed off it at speed.
            Sweep(fx,t,2.8f,.2f,6,Vector3.Lerp(Ember,Gold,.45f),White*.8f+Gold,.03f,.7f,.6f,.2f,23400,low);
            Shed(fx,t,22,.55f,.06f,5,Ember*2.2f,.03f,low?5:14,23460);
            // The heave: the light catching the blade's tip at the top.
            TipStar(fx,t,HeaveAt-.1f,HeaveAt+.24f,(White+Gold)*1.5f,.2f,23480);
            // The bite: sparks along the blade's way, the score left white-hot and cooling to an ember line, dust off it.
            FloorSparks(fx,t,q,low?5:10,6f,.75f,White*3.2f,Ember*2.2f,.018f,23500);
            Score(fx,t,3.4f,White*1.1f+Gold*1.6f,Deep*1.5f,.04f,.5f,23560);
            for(int k=0;k<MarkCount;k+=low?2:1)
            {
                Mark(k,out var at,out var dir,out float born);float a=t-born;if(a<0||a>1.2f)continue;float u=a/1.2f;
                fx.Smoke(at+dir*(.5f*NativeEntranceMath.EaseOut(u))+new Vector3(0,.12f+.55f*u,0),Dust,.42f*(1-u),.24f+.55f*u,u,23600+k,Floor);
            }
            // The wind-up: embers drawn in to the blade.
            {
                float s=t-WindAt,span=SlowAt-WindAt+.03f;
                if(s>=0&&s<span+.1f)
                {
                    var to=Held(t,0,out var root,out var tip)?Vector3.Lerp(root,tip,.6f):new Vector3(0,1.7f,0);int n=low?7:16;
                    for(int i=0;i<n;i++)
                    {
                        float u=NativeEntranceMath.Clamp01((s-.06f*NativeEntranceMath.Rand(23700,i))/span);if(u<=0||u>=1)continue;var d=NativeEntranceMath.Direction(23710,i);d.Y=Math.Abs(d.Y)*.6f-.1f;d=Vector3.Normalize(d);
                        float r=.2f+1.7f*(1-u)*(1-u);fx.Streak(to+d*r,-d*7,Vector3.Lerp(Ember,White+Gold,u)*(2.6f*(float)Math.Sin(Math.PI*u)),.018f,.1f,.03f,.6f,u,23720+i);
                    }
                }
            }
            // The beat: a flash on the blade, the swing's wave running out over the floor in streaks of light and a wall of dust, sparks thrown wide.
            {
                float s=t-Beat;
                if(s>=0&&s<1.7f)
                {
                    var centre=new Vector3(0,Floor,0);
                    if(s<.2f){var at=Held(t,0,out var root,out var tip)?Vector3.Lerp(root,tip,.7f):new Vector3(0,1.1f,0)+Local(0,0,.9f);float u=s/.2f;fx.Glow(at,(White*2+Gold)*(2.8f*(1-u)),0,.3f+.5f*u,1,u,23800);}
                    if(s<1f)
                    {
                        float u=s/1f,r=.5f+4.8f*(1-(1-u)*(1-u));int ring=low?16:36;
                        for(int i=0;i<ring;i++){float a=i*6.2831853f/ring+NativeEntranceMath.Signed(23810,i)*.05f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(centre+new Vector3(0,.03f,0)+d*(r*(.92f+.16f*NativeEntranceMath.Rand(23820,i))),d*6,Vector3.Lerp(White+Gold,Ember,u)*(2.1f*(1-u)),.03f,.22f,.035f,.7f,u,23830+i);}
                    }
                    NativeEntranceFx.GroundDust(fx,t,Beat,centre,low?4:8,.7f,5f,.9f,Dust,.2f,.4f,23900,q,Floor);
                    NativeEntranceFx.Sparks(fx,t,Beat,new Vector3(0,.9f,0)+Local(0,0,.7f),Vector3.UnitY,1.5f,low?12:30,7.5f,.9f,7,1.1f,(White+Gold)*1.6f,Ember*2,.016f,23940,q,Floor);
                }
            }
            // The blade laid on the shoulder: its weight in the floor (a low puff round the feet), the light running up its edge.
            NativeEntranceFx.GroundDust(fx,t,RestAt,new Vector3(0,Floor,0),low?3:5,.3f,1.4f,.6f,Dust,.16f,.26f,23980,q,Floor);
            EdgeGlint(fx,t,RestAt+.15f,RestAt+.7f,(White+Gold)*1.7f,.15f,23990);
            // The name card: one last star on the tip.
            TipStar(fx,t,CardAt-.05f,CardAt+.45f,(White+Gold)*1.3f,.16f,23995);
        }
    }

    /// <summary>
    /// "Edge Flourish", the DEV Katana's entrance (the katana set's `Intro`, 2.83 s; 5.3 s in all, the name at 3.7 s). Cold and clean: a
    /// steel-blue light from behind, still air. The hand goes over the shoulder for the hilt and the blade is out and over in one line
    /// (0.45 s, a glint on the hilt); it is raised, the light running up its edge (1.0 to 1.5 s); a level sweep seen from above (1.75 s);
    /// the flourish's wheel from the floor in front of it, the tip flicking sparks off it (2.1 to 2.5 s); and the last cut, the beat
    /// (2.83 s), slowed: its line carries on across the stage as a hairline of light, held a moment and gone. Every pass
    /// leaves a razor's line; no smoke, no weight: the great sword's opposite.
    /// </summary>
    public sealed class NativeDevKatanaIntro:NativeDevPackIntro
    {
        public const string Name="Edge Flourish",Music="audio/intros/arc-salute",Blurb="The katana raised, swept round and spun in a flourish, then the stance.";
        public const float Total=5.3f,CardAt=3.7f,End=4.95f,ClipAt=.3f,ClipLength=2.83f,Beat=ClipAt+2.53f;
        /// <summary>The clip's other beats (entrance s): the hand closing on the hilt, the blade raised, the level sweep, the flourish's
        /// wheels, the scene slowing into the last cut.</summary>
        public const float DrawAt=ClipAt+.15f,RaiseAt=ClipAt+.7f,SweepAt=ClipAt+1.45f,WheelAt=ClipAt+1.78f,SlowAt=ClipAt+2.475f;
        /// <summary>
        /// The pack's Intro opens with the katana across the back, its hilt over the right shoulder, until the right hand closes on it
        /// (clip s): wholly on the back to CarryUntil, in the hand from TakenAt (measured on the bake's body at 100 a second: the pack's
        /// sword rigid to the chest to 0.11 s, rigid to the hand from 0.15 s). Its first frame, held from the entrance's start, has it
        /// on the back.
        /// </summary>
        public const float CarryUntil=.11f,TakenAt=.15f;
        /// <summary>The carry, in the chest bone's frame: the hilt where the right palm closes on it at TakenAt, the blade along the pack's
        /// (behind the left shoulder toward the left hip), its edge turned as the match turns it in the hand.</summary>
        public static readonly Vector3 CarryAt=new Vector3(-.5128f,.3403f,.1467f);
        public static readonly Quaternion CarryTurn=new Quaternion(-.30222f,.8703f,-.18511f,.34203f);
        public override float Carried(float t)=>1-NativeEntranceMath.Ramp(t-ClipAt,CarryUntil,TakenAt);
        public override bool CarryFrame(out Vector3 at,out Quaternion turn){at=CarryAt;turn=CarryTurn;return true;}
        // Across the back until the hand has it: no edge to follow before then.
        protected override float BladeFrom=>ClipAt+TakenAt+.03f;
        private static readonly Vector3 White=Vector3.One,Steel=NativeEntranceFx.Hex(0xbfdcff),Crimson=NativeEntranceFx.Hex(0xff3d6e);
        public NativeDevKatanaIntro():base(NativeDevMotions.KatanaEntrance,NativeDevMotions.Katana,NativeDevKatanaMotion.Intro,ClipLength,NativeDevKatanaMotion.Idle,ClipAt,SlowAt,Total,CardAt,End,0xff3d6e)
        {
            SlowRate=.2f;SlowHold=.2f;SlowRecover=.28f;SlowCatchUp=.6f;
            Shots=new[]
            {
                // In front and low: the hand goes over the shoulder for the hilt, and the blade is out and over.
                Shot(0,NativeEntranceCut.Cut,40,37,V(-1.1f,.8f,3.5f),V(-.85f,.88f,3.0f),V(0,1.3f,0),V(0,1.3f,0),0,0),
                // Close at its right shoulder: the hand on the hilt, then the blade drawn up and out and turned in the light.
                Shot(ClipAt+.22f,NativeEntranceCut.Cut,37,33,V(1.55f,1.3f,2.05f),V(1.15f,1.55f,1.75f),V(-.05f,1.5f,0),V(-.05f,1.68f,0),0,0),
                // High over its right shoulder: the cut down in front and the level sweep round that side, drawn under the lens.
                Shot(SweepAt-.2f,NativeEntranceCut.Cut,46,42,V(-1.3f,2.15f,2.45f),V(-1.05f,2.0f,2.2f),V(0,.9f,.1f),V(0,.9f,.1f),1,0),
                // Whipped down in front of it, low, for the flourish (its wheel stands in the plane that faces this lens, the tip flicking
                // the floor) and pushed in on through the last cut, slowed, the frame canting with it.
                Shot(WheelAt-.02f,NativeEntranceCut.Whip,45,35,V(.75f,.42f,3.35f),V(-.42f,.74f,2.7f),V(-.1f,1.2f,0),V(-.15f,1.15f,0),2,0,.12f,0,.07f),
                // Out and up to the hero frame as it settles into the stance.
                Shot(ClipAt+ClipLength+.05f,NativeEntranceCut.Blend,40,36,V(-1.3f,.8f,3.95f),V(-.85f,1.04f,3.5f),V(0,1.15f,0),V(-.08f,1.24f,0),0,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.04f,3.5f),V(-.66f,1.06f,3.25f),V(-.08f,1.24f,0),V(-.1f,1.26f,0),1,0),
            };
        }
        // ---- the last cut's line: where the blade's tip was and where it was going as the cut went through (from the blade handed in) ----
        private bool cut,cutBegun;private Vector3 cutAt,cutDir,cutFrom;private float cutTime;
        protected override void Rewind(){cut=cutBegun=false;}
        protected override void Watch(float t)
        {
            if(cut||t<SlowAt-.03f||t>Beat+.12f||!TipVelocity(t,out var tip,out var v)||v.LengthSquared()<64)return;
            // Where the stroke began (the tip at speed, coming over the top), then, on the beat, where it is: the line runs along the two.
            if(!cutBegun){cutBegun=true;cutFrom=tip;}
            if(t<Beat-.012f)return;var along=tip-cutFrom;if(along.LengthSquared()<.09f)along=v;
            cut=true;cutAt=tip;cutDir=Vector3.Normalize(along);cutTime=t;
        }
        public override Vector3 CameraJolt(float t)=>Jolt(t,Beat,.009f,.16f);
        public override void Lights(float t,NativeEntranceLights l)
        {
            // Cold: a steel-blue light from behind and above, a thin crimson rim; the stage comes up as it settles.
            float up=NativeEntranceMath.Ramp(t,ClipEnd-.25f,CardAt);l.DriveProbe=true;NativeEntranceStage.Rig(l,.1f+.8f*up,Accent,.6f,.05f+.5f*up,.2f+.8f*up);
            l.Add(new Vector3(1.4f,3.6f,-2.2f),Steel*(6.5f*(1-.5f*up)),10);l.Add(new Vector3(-2.6f,1.2f,2.2f),Steel*(1.2f*(1-up)),7);
            float flash=NativeEntranceMath.Pulse(t,Beat,.008f,.16f);if(flash>.01f)l.Add(cut?cutAt:new Vector3(0,1.2f,0)+Local(0,0,.9f),(White*2+Steel)*(.8f*flash),4.5f);
            if(t>=CardAt-.2f){float k=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(-2.2f,2.4f,2),Crimson*(1.1f*k),8);}
        }
        protected override float Pylon(int i,float t)=>t<Beat?.08f:PowerOn(i,t-Beat+.05f,.1f);
        public override void Props(float t,INativeEntranceProps props,float q){Take(t,.07f);Stage(props,t,.08f+.4f*NativeEntranceMath.Ramp(t,Beat,CardAt),Beat);}
        // A hairline of light through `at` along `dir`, `half` m each way, tapered to its ends: a white core in a thin crimson fringe.
        private static void Hairline(INativeEntranceFx fx,Vector3 at,Vector3 dir,float half,float level,float width,int seed)
        {
            if(level<=.004f||half<=.01f)return;
            for(int side=-1;side<=1;side+=2){var d=dir*side;fx.Streak(at+d*half,d*half,White*(3*level),width,0,1,.9f,0,seed+side+1);fx.Streak(at+d*(half*.92f),d*(half*.92f),Crimson*(1.1f*level),width*3.5f,0,1,.9f,0,seed+side+4);}
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Take(t,.07f);bool low=q<.5f;float up=NativeEntranceMath.Ramp(t,ClipEnd-.25f,CardAt);
            // The air: still, a few cold motes in the back light.
            Motes(fx,t,q,Steel*.9f,24300);
            Beams(fx,t,q,Accent,up);
            // The edge: a razor's line behind the tip (white, in a breath of crimson), no broad sheet; steel dust off it at speed.
            Sweep(fx,t,3.4f,.24f,7,Crimson,White*.8f+Steel*.3f,.007f,.16f,.82f,.1f,24400,low);
            Shed(fx,t,15,.4f,.03f,3,(White+Steel)*.9f,.016f,low?4:10,24560);
            // The draw: a glint on the hilt as the hand closes on it.
            if(t>=DrawAt-.02f&&t<DrawAt+.22f&&Held(t,0,out var hilt,out _)){float g=NativeEntranceMath.Bump(t,DrawAt-.02f,DrawAt+.22f);NativeEntranceFx.Glint(fx,hilt,(White+Steel)*(1.8f*g),.16f*g,Vector3.UnitX,Vector3.UnitY,24580);}
            // The blade raised and turned in the light: the light running up its edge, a star on its tip.
            EdgeGlint(fx,t,RaiseAt-.05f,RaiseAt+.4f,(White+Steel)*1.8f,.13f,24590);TipStar(fx,t,RaiseAt+.3f,RaiseAt+.75f,(White+Steel)*1.5f,.17f,24595);
            // The flourish: where the tip flicks the floor, a pinch of sparks and a hairline scratch that is gone at once.
            FloorSparks(fx,t,q,low?3:7,3.4f,.4f,White*3,Crimson*2,.009f,24600);Score(fx,t,.7f,White*2.4f,Crimson*1.2f,.011f,0,24660);
            // The last cut: its line carried on across the stage as it goes through, held a moment and gone.
            if(cut)
            {
                float s=t-cutTime;
                if(s>=0&&s<.8f)
                {
                    float grow=NativeEntranceMath.EaseOut(s/.06f),level=s<.14f?1:(1-NativeEntranceMath.Ramp(s,.14f,.8f));level*=level;
                    Hairline(fx,cutAt,cutDir,4.2f*grow,level,.007f+.005f*(1-NativeEntranceMath.Clamp01(s/.14f)),24700);
                    if(s<.1f){float u=s/.1f;fx.Glow(cutAt,(White*2+Steel)*(1.2f*(1-u)),0,.1f+.12f*u,1,u,24710);}
                    NativeEntranceFx.Sparks(fx,t,cutTime,cutAt,cutDir,.5f,low?6:14,6,.4f,5,2,White*3,Crimson*2,.008f,24720,q,Floor);
                }
            }
            // The stance: a last star on the tip, and one more under the name.
            TipStar(fx,t,ClipEnd-.12f,ClipEnd+.3f,(White+Steel)*1.5f,.15f,24790);TipStar(fx,t,CardAt-.05f,CardAt+.4f,(White+Crimson)*1.2f,.13f,24795);
        }
    }
}
