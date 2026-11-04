using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "DEV · Last Warning", the Bombadier's entrance (owner 2026-11-04: "make this like 'Oh shit, they're using the bombadier'"; docs/BOMBADIER.md). The first of the longer, higher-detail
    /// intros: 7.6 s, the name at 6 s. The dread comes first: a fuse burning across a dark arena past charges taped to every pylon, their red lights coming on and ticking faster, until it
    /// runs to a pair of boots and is stamped out. A slow tilt up the pilot; a detonator held up, the head tilted, the thumb coming down. Every light goes out for one beat of black. Then
    /// the pylons blow one after another toward the camera, the pilot walks out of it without looking back, the shockwave passes in slow motion, and a bomb is tossed and caught under the name.
    /// Presentation only: pure functions of the entrance clock, thinned by `quality`, with every beat kept on Reduced.
    /// </summary>
    public sealed class NativeBombadierIntro:NativeProceduralEntrance
    {
        public const float Total=7.6f,CardAt=6,End=7.2f;
        public const float FuseFrom=.2f,Stomp=2.72f,TiltFrom=2.98f,RaiseFrom=3.6f,Click=4.08f,BlackAt=4.14f,FirstBlast=4.52f,BlastStep=.07f,ShockAt=5.3f,WalkFrom=4.52f,TossAt=6.42f,CatchAt=7.02f;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        private static readonly Vector3 FlameColor=NativeEntranceFx.Hex(0xff8a2c),Hot=NativeEntranceFx.Hex(0xffd27a),Led=NativeEntranceFx.Hex(0xff1c14),Smoke=new Vector3(.08f,.07f,.07f),Ember=NativeEntranceFx.Hex(0xff6a1c);
        // The fuse: a curve along the floor from the far pylon to the right boot, walked at even speed.
        private static readonly Vector3[] Way={new Vector3(6.6f,0,-1.9f),new Vector3(5.2f,0,1.3f),new Vector3(2.8f,0,2.9f),new Vector3(.5f,0,3.2f),new Vector3(-1.5f,0,2.1f),new Vector3(-1,0,.8f),new Vector3(-.16f,0,.1f)};
        private readonly Vector3[] fuse=new Vector3[96];private readonly float[] along=new float[96];private float fuseLength;
        private readonly int[] order=new int[NativeEntranceStage.Pylons];
        public NativeBombadierIntro():base(NativeBombs.Entrance,ShockAt,0xff5a2a)
        {
            SlowRate=.2f;SlowHold=.26f;SlowRecover=.2f;SlowCatchUp=.42f;
            BuildFuse();
            Start=Stance(.17f,.06f,-.2f,-.17f,-.1f,-.4f);
            Steps=new[]
            {
                // The stamp on the fuse's flame.
                Step(false,Stomp-.26f,Stomp,-.17f,.02f,-.32f,.2f),
                // The walk out, not hurrying.
                Step(true,WalkFrom+.02f,WalkFrom+.46f,.15f,.68f,-.08f,.1f),Step(false,WalkFrom+.46f,WalkFrom+.9f,-.16f,1.32f,-.28f,.1f),Step(true,WalkFrom+.9f,WalkFrom+1.36f,.15f,1.94f,-.1f,.1f),Step(false,WalkFrom+1.36f,WalkFrom+1.78f,-.16f,2.3f,-.32f,.08f),
            };
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();var r0=Local(0,.88f,0);
            // Waiting in the dark: head down, the launcher hanging from the right hand.
            k.Key(0,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.08f).J(Hd,.34f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-.12f,0,.2f).J(LE,-.32f);
            k.Key(Stomp-.3f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.08f).J(Hd,.36f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-.12f,0,.2f).J(LE,-.32f);
            k.Key(Stomp,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.07f).J(Sp,.14f).J(Hd,.42f).J(RA,-.46f,0,-.14f).J(RE,-.8f).J(LA,-.2f,0,.26f).J(LE,-.4f).ImpactKey();
            // The chin comes up as the camera arrives.
            k.Key(TiltFrom+.22f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.06f).J(Hd,.34f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-.12f,0,.2f).J(LE,-.32f);
            k.Key(RaiseFrom,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.02f).J(C,-.04f).J(Hd,-.04f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-.2f,0,.26f).J(LE,-.5f);
            // The detonator held up beside the head, which tilts to look at it, then at you.
            k.Key(RaiseFrom+.32f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.02f).J(C,-.08f,.12f,0).J(Hd,-.08f,.12f,.2f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-2.2f,.2f,.36f).J(LE,-1.35f);
            k.Key(Click-.03f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.02f).J(C,-.08f,.12f,0).J(Hd,-.06f,.02f,.26f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-2.24f,.2f,.36f).J(LE,-1.4f);
            k.Key(Click+.03f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(C,-.06f,.12f,0).J(Hd,-.04f,.02f,.26f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-2.14f,.2f,.36f).J(LE,-1.3f).ImpactKey();
            k.Key(FirstBlast-.04f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(C,-.05f,.1f,0).J(Hd,-.04f,.02f,.22f).J(RA,-.42f,0,-.12f).J(RE,-.75f).J(LA,-2.14f,.2f,.36f).J(LE,-1.3f);
            // The walk: the left arm falling to its side as the arena goes up behind.
            var w0=Local(0,.88f,.34f);var w1=Local(0,.88f,1.0f);var w2=Local(0,.88f,1.63f);var w3=Local(0,.88f,2.12f);
            k.Key(WalkFrom+.3f,stand).Root(w0.X,w0.Y,w0.Z,Facing).Drop(-.02f).J(C,-.02f,.02f,0).J(Hd,-.03f).J(RA,-.2f,0,-.1f).J(RE,-.55f).J(LA,-.5f,0,.2f).J(LE,-.7f).PassKey();
            k.Key(WalkFrom+.72f,stand).Root(w1.X,w1.Y,w1.Z,Facing).Drop(-.03f).J(C,-.02f,-.05f,0).J(Hd,-.03f).J(RA,-.3f,0,-.1f).J(RE,-.65f).J(LA,.18f,0,.16f).J(LE,-.3f).PassKey();
            k.Key(WalkFrom+1.18f,stand).Root(w2.X,w2.Y,w2.Z,Facing).Drop(-.02f).J(C,-.03f,.05f,0).J(Hd,-.04f).J(RA,-.2f,0,-.1f).J(RE,-.55f).J(LA,-.22f,0,.16f).J(LE,-.36f).PassKey();
            k.Key(CardAt-.12f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.02f).J(C,-.04f).J(Hd,-.05f).J(RA,-.3f,0,-.1f).J(RE,-.62f).J(LA,-.3f,0,.26f).J(LE,-.9f);
            // The toss: a flick of the left forearm, watched up and down; caught and spun.
            k.Key(TossAt-.1f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.02f).J(C,-.05f).J(Hd,-.04f).J(RA,-.3f,0,-.1f).J(RE,-.62f).J(LA,-.46f,0,.3f).J(LE,-1.3f);
            k.Key(TossAt+.04f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.03f).J(C,-.05f).J(Hd,-.16f).J(RA,-.3f,0,-.1f).J(RE,-.62f).J(LA,-.74f,0,.28f).J(LE,-1.0f);
            k.Key(CatchAt-.05f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.02f).J(C,-.05f).J(Hd,-.08f).J(RA,-.3f,0,-.1f).J(RE,-.62f).J(LA,-.46f,0,.3f).J(LE,-1.3f);
            k.Key(CatchAt+.06f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.05f).J(C,-.05f).J(Hd,.02f).J(RA,-.3f,0,-.1f).J(RE,-.62f).J(LA,-.38f,0,.3f).J(LE,-1.2f);
            k.Key(Total,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.03f).J(C,-.05f).J(Hd,-.02f).J(RA,-.3f,0,-.1f).J(RE,-.62f).J(LA,-.42f,0,.3f).J(LE,-1.26f);Keys=k;
            // The pylons blow farthest from the lens first.
            for(int i=0;i<order.Length;i++)order[i]=i;Array.Sort(order,(a,b)=>NativeEntranceStage.Pylon(a).Z.CompareTo(NativeEntranceStage.Pylon(b).Z));
            Shots=new[]
            {
                // Macro on the flame, racing ahead of it along the floor.
                Follow(Shot(0,NativeEntranceCut.Cut,30,36,V(6.1f,.14f,.3f),V(1.3f,.2f,3.3f),V(0,.03f,0),V(0,.04f,0),0,0),NativeEntranceAnchor.Focus),
                // Up and back: the dark arena, red lights spreading round it.
                Shot(1.42f,NativeEntranceCut.Whip,54,50,V(3.4f,1.1f,6.4f),V(2.2f,3.4f,7.2f),V(-.8f,.2f,1.4f),V(-.3f,.6f,0),1,0,.22f),
                // Down on the boots as the flame runs in.
                Follow(Shot(2.3f,NativeEntranceCut.Cut,32,30,V(.95f,.17f,1.7f),V(.6f,.18f,1.25f),V(0,.04f,0),V(0,.05f,0),1,0),NativeEntranceAnchor.Focus),
                // The slow tilt from the boots to the visor.
                Shot(TiltFrom,NativeEntranceCut.Cut,34,30,V(.42f,.2f,1.32f),V(.34f,1.02f,1.38f),V(0,.14f,0),V(0,1.52f,0),2,0),
                // Close on the thumb.
                Follow(Shot(RaiseFrom+.2f,NativeEntranceCut.Cut,30,25,V(.92f,1.22f,1.28f),V(.82f,1.26f,1.12f),V(-.05f,.03f,0),V(-.04f,.05f,0),1,0),NativeEntranceAnchor.LeftHand),
                // The visor, and the lights going out in it.
                Shot(Click+.02f,NativeEntranceCut.Cut,28,26,V(.14f,1.6f,.98f),V(.12f,1.6f,.94f),V(0,1.58f,0),V(0,1.58f,0),1,0),
                // The pilot coming at the lens, the arena going up behind.
                Shot(FirstBlast-.02f,NativeEntranceCut.Cut,46,40,V(-.5f,.62f,6.4f),V(-.72f,.56f,5.5f),V(.2f,1.1f,0),V(-.3f,1.16f,1.5f),1,0),
                // The shockwave, low and wide: it does not look back.
                Shot(ShockAt-.1f,NativeEntranceCut.Cut,58,52,V(1.7f,.3f,3.5f),V(1.45f,.32f,3.9f),V(-.4f,1.15f,1.1f),V(-.6f,1.22f,1.55f),1,0),
                // The name, and the toss.
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(.52f,.82f,4.82f),V(.3f,.92f,4.5f),V(-.44f,1.15f,2.1f),V(-.46f,1.2f,2.1f),1,0),
            };
        }
        private void BuildFuse()
        {
            int n=fuse.Length,seg=Way.Length-1;
            for(int i=0;i<n;i++)
            {
                float x=i/(n-1f)*seg;int s=Math.Min(seg-1,(int)x);float u=x-s;var p0=Way[Math.Max(0,s-1)];var p1=Way[s];var p2=Way[s+1];var p3=Way[Math.Min(seg,s+2)];float u2=u*u,u3=u2*u;
                fuse[i]=.5f*(2*p1+(p2-p0)*u+(2*p0-5*p1+4*p2-p3)*u2+(3*p1-p0-3*p2+p3)*u3);fuse[i].Y=NativeEntranceStage.FloorY+.012f;
                along[i]=i==0?0:along[i-1]+Vector3.Distance(fuse[i],fuse[i-1]);
            }
            fuseLength=along[n-1];
        }
        private Vector3 FuseAt(float metres){int n=fuse.Length;if(metres<=0)return fuse[0];if(metres>=fuseLength)return fuse[n-1];int lo=0,hi=n-1;while(hi-lo>1){int m=(lo+hi)/2;if(along[m]<metres)lo=m;else hi=m;}float u=(metres-along[lo])/Math.Max(1e-4f,along[hi]-along[lo]);return Vector3.Lerp(fuse[lo],fuse[hi],u);}
        /// <summary>How far the flame has burned at `t` (m); slightly accelerating.</summary>
        private float Burned(float t){float u=NativeEntranceMath.Clamp01((t-FuseFrom)/(Stomp-FuseFrom));return fuseLength*(u*.7f+.3f*u*u);}
        public Vector3 FlameAt(float t)=>FuseAt(Burned(t));
        /// <summary>The lit match, falling out of the dark onto the rope's end as the fuse catches.</summary>
        private Vector3 MatchAt(float t){float u=NativeEntranceMath.Clamp01(t/FuseFrom);return fuse[0]+new Vector3(.03f*(1-u),.62f*(1-u*u)+.012f,-.02f*(1-u));}
        protected override Vector3 Focus(float t,Vector3 chest)=>t<Stomp?FlameAt(t):chest;
        public float BlastAt(int pylon){for(int i=0;i<order.Length;i++)if(order[i]==pylon)return FirstBlast+i*BlastStep;return FirstBlast;}
        /// <summary>When pylon `i`'s charge's light comes on: the farther round the ring from the fuse's start, the later.</summary>
        private static float LedOn(int i){float a=(float)Math.Atan2(NativeEntranceStage.Pylon(i).X,NativeEntranceStage.Pylon(i).Z);return 1.05f+1.5f*(((a+3.1415927f)/6.2831853f+.72f)%1);}
        /// <summary>The charges' lights 0..1: on from LedOn, ticking faster toward the click, out at BlackAt. The tick is shared, so they pulse as one.</summary>
        private static float LedLevel(int i,float t)
        {
            if(t<LedOn(i)||t>=BlackAt)return 0;return .35f+.65f*Tick(t);
        }
        /// <summary>The beeping: each beat comes sooner than the last until the click. 1 on a beep, falling away.</summary>
        public static float Tick(float t)
        {
            float u=NativeEntranceMath.Clamp01((t-1.05f)/(Click-1.05f)),phase=(3*u+9*u*u);phase-=(float)Math.Floor(phase);return (float)Math.Exp(-phase*5);
        }
        protected override float Pylon(int i,float t)
        {
            float at=BlastAt(i);if(t>=at)return 0;if(t>=BlackAt)return 0;return .03f+.3f*LedLevel(i,t);
        }
        // The visor and trims: barely alive in the dark, beating red with the charges, out with them, and burning orange once it has all gone up.
        private static readonly string[] Reds={"#000000","#1c0403","#360705","#500a07","#6e0d09","#8e110c","#b0150f","#d41a12","#ff2015"};
        public override string AccentAt(float t)
        {
            if(t>=FirstBlast)return "#ff6a1c";if(t>=BlackAt)return Reds[0];
            float level=t<1.05f?.14f:t<Stomp?.2f+.5f*Tick(t):t<Click?.3f+.7f*Tick(t):1;return Reds[Math.Max(0,Math.Min(Reds.Length-1,(int)Math.Round(level*(Reds.Length-1))))];
        }
        /// <summary>A little shake once the blasts begin, and the jolt of the stamp; none under Reduced (the runtime scales it).</summary>
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,Stomp,.01f,.22f)*.012f;if(t>=FirstBlast&&t<CardAt){float g=NativeEntranceMath.Ramp(t,FirstBlast,ShockAt)*(1-NativeEntranceMath.Ramp(t,ShockAt+.3f,CardAt));s+=g*.035f;}
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float dark=t<FirstBlast?1:0,up=NativeEntranceMath.Ramp(t,FirstBlast,ShockAt),black=NativeEntranceMath.Ramp(t,BlackAt-.03f,BlackAt)*(1-NativeEntranceMath.Ramp(t,FirstBlast-.01f,FirstBlast));
            // Almost nothing before the blasts: what you see, the flame and the lights show you.
            NativeEntranceStage.Rig(l,dark>0?.012f*(1-black):.25f+.4f*up,FlameColor,dark>0?0:.6f+.8f*up,0,dark>0?.05f*(1-black):.22f+.25f*up);
            if(t<FuseFrom+.25f){float fl=.8f+.2f*(float)Math.Sin(t*61);l.Add(MatchAt(t)+new Vector3(0,.06f,0),Hot*(1.4f*fl),2.2f);}
            if(t<Stomp&&t>=FuseFrom-.05f){float fl=.85f+.15f*(float)Math.Sin(t*47)+.1f*(float)Math.Sin(t*31);l.Add(FlameAt(t)+new Vector3(0,.2f,0),FlameColor*(2.4f*fl),4.2f);}
            float redAll=0;for(int i=0;i<order.Length;i++)redAll+=LedLevel(i,t);redAll/=order.Length;
            if(redAll>.001f){l.Add(new Vector3(5,2.4f,-4.5f),Led*(9*redAll),15);l.Add(new Vector3(-5,2.4f,-4.5f),Led*(9*redAll),15);l.Add(new Vector3(0,3,5.5f),Led*(7*redAll),13);}
            // The pale fuse-glow on the pilot from below as it reaches the boots, and the click's single red spark.
            float boot=NativeEntranceMath.Pulse(t,Stomp,.02f,.3f);if(boot>.01f&&t<BlackAt)l.Add(new Vector3(0,.2f,.3f),Hot*(3*boot),3.2f);
            if(t>=RaiseFrom&&t<BlackAt){float c=.7f+1.2f*Tick(t);l.Add(Anchor(NativeEntranceAnchor.LeftHand,t)+new Vector3(0,.12f,0),Led*(.9f*c),2.4f);}
            // The wall of fire behind the walk.
            if(t>=FirstBlast){float g=up*(1-.35f*NativeEntranceMath.Ramp(t,CardAt,Total));var at=Anchor(NativeEntranceAnchor.Chest,t);l.Add(at+new Vector3(-.3f,.5f,-2.4f),FlameColor*(16*g),10);l.Add(new Vector3(-5,1.6f,-3),FlameColor*(10*g),14);l.Add(new Vector3(5,1.6f,-3),Ember*(10*g),14);l.Fog=NativeEntranceFx.Hex(0x2a0f07,.4f+.6f*g);l.Background=l.Fog;}
        }
        // ---- props: the fuse on the floor, the charges, the detonator, the bomb, the flying wreckage ----
        private static readonly Quaternion Flat=Quaternion.Identity;
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,t<FirstBlast?.02f:.08f);
            // The match: a sliver of pale wood with a black head, turning as it falls; it lies on the rope afterwards.
            if(t<Stomp)
            {
                float u=NativeEntranceMath.Clamp01(t/FuseFrom);var m=MatchAt(t);var turn=u<1?Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.4f,.2f,.9f)),.5f+6*u):Quaternion.CreateFromAxisAngle(Vector3.UnitZ,1.5f);var along=Vector3.Transform(Vector3.UnitY,turn);
                props.Piece(WeaponShape.Cube,false,m,turn,new Vector3(.006f,.07f,.006f),new Vector3(.62f,.5f,.3f));props.Piece(WeaponShape.Sphere,false,m+along*.036f,turn,new Vector3(.0075f,.011f,.0075f),new Vector3(.04f,.03f,.03f));
            }
            // The rope, charred behind the flame.
            float burned=Burned(t);int step=q<.5f?2:1;var rope=new Vector3(.09f,.075f,.06f);var ash=new Vector3(.015f,.015f,.015f);
            for(int i=0;i+step<fuse.Length;i+=step)
            {
                var a=fuse[i];var b=fuse[i+step];var mid=(a+b)*.5f;var d=b-a;float len=d.Length();if(len<1e-4f)continue;float mL=(along[i]+along[i+step])*.5f;bool gone=mL<burned;
                props.Piece(WeaponShape.Cube,false,mid,Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.Atan2(d.X,d.Z)),new Vector3(.024f,.018f,len*1.02f),gone?ash:rope);
                // What just burned stays hot for a moment.
                float since=(burned-mL)/2.2f;if(gone&&since<1&&t<Stomp+.3f)props.Piece(WeaponShape.Cube,true,mid+new Vector3(0,.012f,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.Atan2(d.X,d.Z)),new Vector3(.012f,.006f,len),FlameColor*(1.6f*(1-since)*(1-since)));
            }
            // A bundle of charges strapped to each pylon, facing in, until it goes.
            for(int i=0;i<order.Length;i++)
            {
                if(t>=BlastAt(i))continue;var p=NativeEntranceStage.Pylon(i);var inward=Vector3.Normalize(new Vector3(-p.X,0,-p.Z));var side=new Vector3(inward.Z,0,-inward.X);var yaw=Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.Atan2(inward.X,inward.Z));var c=p+inward*.42f+new Vector3(0,1.15f,0);
                for(int s=-1;s<=1;s++)props.Piece(WeaponShape.Cube,false,c+side*(s*.1f),yaw,new Vector3(.085f,.36f,.085f),new Vector3(.45f,.13f,.09f));
                props.Piece(WeaponShape.Cube,false,c+inward*.002f,yaw,new Vector3(.34f,.05f,.1f),new Vector3(.06f,.06f,.07f));props.Piece(WeaponShape.Cube,false,c-new Vector3(0,.12f,0),yaw,new Vector3(.34f,.04f,.1f),new Vector3(.06f,.06f,.07f));
                float led=LedLevel(i,t);props.Piece(WeaponShape.Cube,false,c+inward*.05f+new Vector3(0,.13f,0),yaw,new Vector3(.12f,.08f,.03f),new Vector3(.05f,.05f,.055f));
                props.Piece(WeaponShape.Sphere,true,c+inward*.068f+new Vector3(0,.13f,0),Flat,new Vector3(.026f),led>0?Led*(1+7*led):new Vector3(.03f,.005f,.005f));
            }
            // The detonator in the left hand.
            if(t>=RaiseFrom-.1f&&t<FirstBlast+.5f)
            {
                var h=Anchor(NativeEntranceAnchor.LeftHand,t);var f=ForwardOf(t);var side=new Vector3(f.Z,0,-f.X);var yaw=NativeEntranceMath.Yaw(Heading(t));float press=NativeEntranceMath.Ramp(t,Click-.03f,Click+.015f);bool dead=press>=1&&t>=BlackAt;
                Vector3 B(float x,float y,float z)=>h+side*x+Vector3.UnitY*y+f*z;var olive=new Vector3(.085f,.09f,.065f);var steel=new Vector3(.17f,.17f,.19f);var hazard=new Vector3(.8f,.6f,.05f);
                props.Piece(WeaponShape.Cube,false,B(0,.05f,0),yaw,new Vector3(.062f,.125f,.04f),olive);props.Piece(WeaponShape.Cube,false,B(0,.117f,0),yaw,new Vector3(.066f,.012f,.044f),steel);props.Piece(WeaponShape.Cube,false,B(0,-.015f,0),yaw,new Vector3(.066f,.01f,.044f),steel);
                // A band of hazard stripe round its middle, and a cell of screws.
                props.Piece(WeaponShape.Cube,false,B(0,.03f,.021f),yaw,new Vector3(.064f,.034f,.002f),hazard);for(int s=-1;s<=1;s++)props.Piece(WeaponShape.Cube,false,B(s*.02f,.03f,.0222f),yaw*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.6f),new Vector3(.007f,.04f,.0015f),new Vector3(.02f,.02f,.02f));
                for(int s=-1;s<=1;s+=2)props.Piece(WeaponShape.Cube,false,B(s*.024f,.098f,.021f),yaw,new Vector3(.006f,.006f,.002f),steel);
                // The button under the thumb in its collar, and the safety guard flipped back.
                props.Piece(WeaponShape.Cube,false,B(0,.125f,0),yaw,new Vector3(.036f,.006f,.036f),new Vector3(.55f,.38f,.14f));props.Piece(WeaponShape.Cube,true,B(0,.132f-.006f*press,0),yaw,new Vector3(.024f,.01f,.024f),dead?new Vector3(.08f,.01f,.01f):Led*(2.4f+2*Tick(t)));
                props.Piece(WeaponShape.Cube,false,B(0,.142f,-.026f),yaw*Quaternion.CreateFromAxisAngle(Vector3.UnitX,-1.1f),new Vector3(.04f,.042f,.004f),new Vector3(.5f,.06f,.04f));
                // The whip aerial, a telltale on its tip.
                props.Piece(WeaponShape.Cube,false,B(.022f,.205f,-.012f),yaw,new Vector3(.004f,.17f,.004f),steel);props.Piece(WeaponShape.Sphere,true,B(.022f,.29f,-.012f),Flat,new Vector3(.006f),dead?new Vector3(.03f,.005f,.005f):Led*(2+4*Tick(t)));
            }
            // The bomb under the name: tossed, spinning, and caught.
            if(t>=CardAt-.1f)
            {
                var h=Anchor(NativeEntranceAnchor.LeftHand,t)+new Vector3(0,.07f,0);var ball=h;float spin=(t-TossAt)*11;
                if(t>=TossAt&&t<CatchAt){float u=(t-TossAt)/(CatchAt-TossAt);var from=Anchor(NativeEntranceAnchor.LeftHand,TossAt)+new Vector3(0,.07f,0);var to=Anchor(NativeEntranceAnchor.LeftHand,CatchAt)+new Vector3(0,.07f,0);ball=Vector3.Lerp(from,to,u)+new Vector3(0,.62f*4*u*(1-u),0);}
                if(t>=CatchAt)spin=(CatchAt-TossAt)*11+(t-CatchAt)*4;var q0=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.3f,.8f,.5f)),spin);
                BombModel(props,ball,q0,.058f);
            }
            // The wreckage: closed-form chunks of each pylon, tumbling and falling; the near ones sail past the lens.
            int chunks=q<.5f?5:12;
            for(int i=0;i<order.Length;i++)
            {
                float at=BlastAt(i),a=t-at;if(a<0||a>2.6f)continue;var p=NativeEntranceStage.Pylon(i);var o=new Vector3(p.X,1.2f,p.Z);var outFrom=Vector3.Normalize(new Vector3(-p.X,0,-p.Z));
                for(int n=0;n<chunks;n++)
                {
                    int seed=i*37+n;var v=outFrom*(5+7*NativeEntranceMath.Rand(seed,1))+new Vector3(NativeEntranceMath.Signed(seed,2)*5,3.5f+6*NativeEntranceMath.Rand(seed,3),NativeEntranceMath.Signed(seed,4)*5);var c=o+v*a+new Vector3(0,-4.9f*a*a,0);
                    if(c.Y<NativeEntranceStage.FloorY+.08f){c.Y=NativeEntranceStage.FloorY+.08f;}
                    float size=.05f+.2f*NativeEntranceMath.Rand(seed,5);var rot=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(seed,6),.4f,NativeEntranceMath.Signed(seed,7))),a*(4+9*NativeEntranceMath.Rand(seed,8)));int kind=seed%3;
                    var shape=kind==0?new Vector3(size*1.5f,size*.1f,size*1.1f):kind==1?new Vector3(size*.14f,size*.14f,size*2.2f):new Vector3(size,size*.55f,size*.8f);var metal=new Vector3(.075f,.07f,.075f)*(.7f+.6f*NativeEntranceMath.Rand(seed,9));
                    props.Piece(WeaponShape.Cube,false,c,rot,shape,metal);
                    // Some edges still hot, cooling as they fly.
                    float cool=1-a/1.7f;if(cool>0&&seed%2==0)props.Piece(WeaponShape.Cube,true,c+Vector3.Transform(new Vector3(0,shape.Y*.5f,0),rot),rot,new Vector3(shape.X*.92f,.006f,shape.Z*.92f),Ember*(2.6f*cool*cool));
                }
            }
        }
        /// <summary>A round black bomb: the sphere, its brass collar and a stub of fuse.</summary>
        public static void BombModel(INativeEntranceProps props,Vector3 at,Quaternion q,float radius)
        {
            props.Piece(WeaponShape.Sphere,false,at,q,new Vector3(radius),new Vector3(.03f,.03f,.035f));var up=Vector3.Transform(Vector3.UnitY,q);
            props.Piece(WeaponShape.Cube,false,at+up*(radius*.96f),q,new Vector3(radius*.62f,radius*.2f,radius*.62f),new Vector3(.55f,.38f,.14f));props.Piece(WeaponShape.Cube,false,at+up*(radius*1.3f),q,new Vector3(radius*.14f,radius*.5f,radius*.14f),new Vector3(.09f,.075f,.06f));
        }
        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;
            // The match's own flame, and the rope catching from it.
            if(t<FuseFrom+.08f)
            {
                float u=NativeEntranceMath.Clamp01(t/FuseFrom);var turn=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.4f,.2f,.9f)),.5f+6*u);var head=MatchAt(t)+Vector3.Transform(new Vector3(0,.04f,0),turn);float fl=.82f+.18f*(float)Math.Sin(t*67),gone=1-NativeEntranceMath.Ramp(t,FuseFrom,FuseFrom+.08f);
                fx.Glow(head,Vector3.One*(2.2f*fl*gone),0,.03f,1,0,7990);fx.Glow(head+new Vector3(0,.012f,0),Hot*(2*fl*gone),0,.08f,1,0,7991);fx.Glow(head,FlameColor*(.8f*fl*gone),0,.3f,0,0,7992);
            }
            NativeEntranceFx.Sparks(fx,t,FuseFrom,fuse[0],new Vector3(0,.5f,0),1,q<.5f?8:20,2.2f,.45f,6,.6f,Hot*2.6f,FlameColor,.008f,7995,q,floor);
            // The flame on the fuse: spitting sparks, a thread of smoke, light on the floor.
            if(t>=FuseFrom-.08f&&t<Stomp)
            {
                var at=FlameAt(t);var back=at-FlameAt(t-.03f);var dir=back.LengthSquared()>1e-6f?Vector3.Normalize(back):Vector3.UnitZ;float fl=.85f+.15f*(float)Math.Sin(t*53);
                fx.Glow(at+new Vector3(0,.035f,0),Vector3.One*(3.4f*fl),0,.12f,1,0,8001);fx.Glow(at+new Vector3(0,.05f,0),Hot*(2.2f*fl),0,.3f,1,0,8002);fx.Glow(at+new Vector3(0,.04f,0),FlameColor*(1.1f*fl),0,.9f,0,0,8003);
                int n=q<.5f?8:18;
                for(int i=0;i<n;i++)
                {
                    float born=((t*44+i*.37f)%1)*.34f,a=born;int seed=(int)(t*44)+i*97;var v=new Vector3(NativeEntranceMath.Signed(seed,1)*1.6f,.8f+2.4f*NativeEntranceMath.Rand(seed,2),NativeEntranceMath.Signed(seed,3)*1.6f)-dir*(1.1f*NativeEntranceMath.Rand(seed,4));var p=at+v*a+new Vector3(0,-3.8f*a*a,0);if(p.Y<floor)p.Y=floor+.005f;
                    fx.Streak(p,v-new Vector3(0,7.6f*a,0),(Hot*2.6f)*(1-a/.34f),.008f,.012f,.02f,.6f,a/.34f,seed);
                }
                int m=q<.5f?3:6;for(int i=0;i<m;i++){float u=((t*2.1f+i/(float)m)%1);var p=FlameAt(t-u*.5f)+new Vector3((float)Math.Sin(t*5+i)*.05f*u,.05f+.6f*u,(float)Math.Cos(t*4+i)*.05f*u);fx.Smoke(p,new Vector3(.2f,.2f,.22f),.2f*(1-u),.1f+.3f*u,.2f+.6f*u,8100+i,floor);}
            }
            // The stamp: sparks squeezed out under the boot, and a thin curl of smoke after.
            NativeEntranceFx.Sparks(fx,t,Stomp,fuse[fuse.Length-1],new Vector3(0,.5f,0),1.1f,q<.5f?12:30,2.6f,.5f,6,.6f,Hot*2.6f,FlameColor,.01f,8200,q,floor);
            if(t>=Stomp&&t<Stomp+1.6f){int n=q<.5f?3:6;for(int i=0;i<n;i++){float u=((t-Stomp)*.8f+i/(float)n)%1;fx.Smoke(fuse[fuse.Length-1]+new Vector3((float)Math.Sin(i*2+t)*.04f,.08f+.7f*u,(float)Math.Cos(i*3+t)*.04f),new Vector3(.32f,.32f,.34f),.16f*(1-u)*(1-NativeEntranceMath.Ramp(t,Stomp+1,Stomp+1.6f)),.08f+.26f*u,.3f+.5f*u,8210+i,floor);}}
            // Haloes on the charges' lights, thin dust hanging red in the air.
            if(t<BlackAt){for(int i=0;i<order.Length;i++){float led=LedLevel(i,t);if(led<=0)continue;var p=NativeEntranceStage.Pylon(i);var inward=Vector3.Normalize(new Vector3(-p.X,0,-p.Z));fx.Glow(p+inward*.52f+new Vector3(0,1.28f,0),Led*(.9f*led),0,.5f,1,0,8300+i);}Motes(fx,t,q,Led*.5f,8400);}
            // The click: one spark off the thumb.
            NativeEntranceFx.Sparks(fx,t,Click,Anchor(NativeEntranceAnchor.LeftHand,Click)+new Vector3(0,.12f,0),new Vector3(0,.6f,0),1,q<.5f?3:7,1.2f,.2f,4,.5f,Hot*2,FlameColor,.006f,8500,q,floor);
            if(t<FirstBlast)return;
            // ---- it all goes ----
            var flames=fx as INativeEntranceFire;
            for(int i=0;i<order.Length;i++)
            {
                float at=BlastAt(i),a=t-at;if(a<-.01f)continue;int s=8600+i*40;var p=NativeEntranceStage.Pylon(i);var o=new Vector3(p.X,.1f,p.Z);float scale=1+.55f*Rank(i);
                // A column of fire climbing out of the pylon's foot, rolling into black smoke.
                int fire=q<.5f?12:30;
                for(int n=0;n<fire;n++)
                {
                    int k=s+n;float life=.9f+.9f*NativeEntranceMath.Rand(k,1),born=.28f*NativeEntranceMath.Rand(k,2),b=a-born;if(b<0||b>life)continue;float u=b/life,rise=(1-(1-u)*(1-u));
                    var c=o+new Vector3(NativeEntranceMath.Signed(k,3)*(.5f+1.7f*u),.2f+(2.4f+3.8f*NativeEntranceMath.Rand(k,4))*scale*rise,NativeEntranceMath.Signed(k,5)*(.5f+1.7f*u));float size=(.5f+1.3f*u)*scale;
                    if(flames!=null)flames.Fire(c,new Vector3(1,.42f,.07f),(1-u*u)*(.75f+.25f*NativeEntranceMath.Rand(k,6)),size*1.25f,.04f+.86f*u,k,floor);
                    else{var col=u<.25f?Vector3.Lerp(Vector3.One*2.6f,Hot*2.4f,u/.25f):u<.6f?Vector3.Lerp(Hot*2.4f,FlameColor*1.5f,(u-.25f)/.35f):FlameColor*(1.5f*(1-u)/.4f);fx.Glow(c,col,0,size,1,0,k);}
                }
                int smoke=q<.5f?6:16;
                for(int n=0;n<smoke;n++)
                {
                    int k=s+100+n;float life=2+1.8f*NativeEntranceMath.Rand(k,1),born=.2f+.5f*NativeEntranceMath.Rand(k,2),b=a-born;if(b<0||b>life)continue;float u=b/life,rise=(1-(1-u)*(1-u));
                    var c=o+new Vector3(NativeEntranceMath.Signed(k,3)*(.7f+2.4f*u),.5f+(4+4*NativeEntranceMath.Rand(k,4))*scale*rise,NativeEntranceMath.Signed(k,5)*(.7f+2.4f*u));
                    fx.Smoke(c,Smoke,.72f*(1-u*u)*NativeEntranceMath.Ramp(b,0,.2f),(.9f+2*u)*scale,.15f+.7f*u,k,floor);
                }
                // The white-hot instant it goes, lighting everything round it.
                if(a>=0&&a<.14f){float w=1-a/.14f;fx.Glow(o+new Vector3(0,1.2f,0),(Vector3.One*1.6f+Hot)*w,0,3.4f*scale,1,0,s+390);}
                NativeEntranceFx.Sparks(fx,t,at,o+new Vector3(0,1,0),new Vector3(0,.7f,0),1.2f,q<.5f?14:40,11,1.1f,8,.5f,Hot*3,Ember,.02f,s+200,q,floor);
                NativeEntranceFx.GroundDust(fx,t,at,o,q<.5f?5:14,.6f,5.5f,1.3f,new Vector3(.16f,.13f,.11f),.55f,.9f*scale,s+300,q,floor);
            }
            // Burning embers streaming past for the rest of it.
            NativeEntranceFx.Embers(fx,t,FirstBlast+.2f,Total+.5f,new Vector3(-.3f,1,1.4f),5.5f,q<.5f?26:80,1.6f,1.5f,Ember*2,.03f,8900,q,.6f);
            // The shockwave: a ring of dust and hard streaks running out over the lens, and a white blink as it passes.
            float sw=t-ShockAt;
            if(sw>=0&&sw<.7f)
            {
                float reach=2+17*sw;int n=q<.5f?24:64;for(int i=0;i<n;i++){float ang=i*6.2831853f/n+.2f*NativeEntranceMath.Rand(9000,i);var d=new Vector3((float)Math.Sin(ang),0,(float)Math.Cos(ang));float lag=.82f+.3f*NativeEntranceMath.Rand(9002,i);var at=new Vector3(0,.04f+.55f*NativeEntranceMath.Rand(9001,i)*NativeEntranceMath.Rand(9003,i),-2)+d*(reach*lag);fx.Streak(at,d*(10+8*NativeEntranceMath.Rand(9004,i)),Hot*(1.5f*(1-sw/.7f)),.012f+.012f*NativeEntranceMath.Rand(9005,i),.3f,.05f,.8f,sw/.7f,9100+i);}
                NativeEntranceFx.GroundDust(fx,t,ShockAt,new Vector3(0,.1f,-2),q<.5f?12:32,1.5f,14,.8f,new Vector3(.22f,.17f,.14f),.5f,1.1f,9200,q,floor);
            }
            // Heat on the toss: the bomb's fuse spitting under the name.
            if(t>=TossAt-.2f)
            {
                var h=Anchor(NativeEntranceAnchor.LeftHand,t)+new Vector3(0,.16f,0);float u=t>=TossAt&&t<CatchAt?(t-TossAt)/(CatchAt-TossAt):0;if(u>0){var from=Anchor(NativeEntranceAnchor.LeftHand,TossAt)+new Vector3(0,.16f,0);var to=Anchor(NativeEntranceAnchor.LeftHand,CatchAt)+new Vector3(0,.16f,0);h=Vector3.Lerp(from,to,u)+new Vector3(0,.62f*4*u*(1-u),0);}
                float fl=.8f+.2f*(float)Math.Sin(t*51);fx.Glow(h,Hot*(1.8f*fl),0,.08f,1,0,9400);int n=q<.5f?4:10;for(int i=0;i<n;i++){float a=((t*20+i*.41f)%1)*.3f;int k=(int)(t*20)+i*53;var v=new Vector3(NativeEntranceMath.Signed(k,1)*.8f,.7f+.8f*NativeEntranceMath.Rand(k,2),NativeEntranceMath.Signed(k,3)*.8f);fx.Streak(h+v*a,v,Hot*(2*(1-a/.3f)),.006f,.01f,.02f,.6f,a/.3f,k);}
            }
        }
        private float Rank(int pylon){for(int i=0;i<order.Length;i++)if(order[i]==pylon)return i/(order.Length-1f);return 0;}
        protected override void Plan()
        {
            // The sim's own shock and flash at each pylon, so the world lights and the sound's punch are the game's.
            var list=new System.Collections.Generic.List<NativeEntranceBlast>();
            for(int i=0;i<order.Length;i++){var p=NativeEntranceStage.Pylon(order[i]);if(p.Z>1.2f)continue;float r=i/(order.Length-1f);list.Add(new NativeEntranceBlast{At=FirstBlast+i*BlastStep,Radius=3+1.4f*r,Size=1.6f+1.2f*r,Style=NativeBlastStyle.Fire,Position=new Vector3(p.X,.9f,p.Z)});}
            Blasts=list.ToArray();
        }
    }
}
