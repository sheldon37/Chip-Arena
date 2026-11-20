using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Threshold" (docs/INTROS-GENERIC.md "Threshold"). 8.2 s, the name at 6.4 s. Dusk in a canyon: at the end of a ruined plaza two
    /// leaning standing stones meet in a cleft. A crack of light runs up the seam between them in three jolts, dust shaken from it; the
    /// seam brightens, its runes waking; the stones grind apart and light floods out of the widening slit in a sheet, dust pouring
    /// along the ground; the pilot steps out through the slit and walks down the plaza to the mark as the stones grind shut behind it,
    /// the seam a fading line. Camera: formal (a centred push-in on the cleft, an arc in front as it steps out, low and centred with
    /// the stones towering behind). No ring, no lamps round a circle, no vortex (the integrator's review, 2026-11-20).
    /// </summary>
    public sealed class NativeIntroThreshold:NativeIntroGenericScript
    {
        public const string Id=NativeIntroCatalog.Prefix+"threshold";
        public const float Total=8.2f,CardAt=6.4f;
        public static readonly float[] Cracks={.6f,1.2f,1.75f};
        public const float ChargeAt=2.4f,OpenAt=3.0f,ThroughAt=3.4f,CloseAt=4.9f;
        public override string Title=>"Threshold";
        // The set (Unity space, generic_threshold.py): the seam's plane, the stones' gap at their feet at rest, the seam's height, how
        // far each stone slides out when they part, and where the walk out begins (in the slit).
        public const float SlitZ=-3.2f,Gap=.25f,SeamH=6.3f,PartBy=.45f,From=-3.7f;
        // The walk out (the model's feet, the world's frame, straight down +z): from the slit as the stones part, measured strides of
        // about .7 m every .49 s, the hips easing up to speed and slowing onto the mark; it steps into sight (ThroughAt) on its first
        // footfall, and closes onto the hero stance (the left foot, then the right) under the name.
        public const float WalkFrom=3.0f,Cadence=.49f,Swing=.4f,ArriveAt=6.2f;
        public const int Paces=5;
        private static readonly Vector3 Light=NativeEntranceFx.Hex(0xfff1d0),Gold=NativeEntranceFx.Hex(0xffc878),Amber=NativeEntranceFx.Hex(0xff9a4a),
            Dusk=NativeEntranceFx.Hex(0xff9a5a),Sand=NativeEntranceFx.Hex(0xc9a074),Rune=NativeEntranceFx.Hex(0xffd79a);
        private static readonly Track Path=new Track(WalkFrom,ArriveAt,From,0,.45f,.8f);
        /// <summary>When step `i` lands (0: the right foot's first, out of the slit; Paces: the left closing onto the mark).</summary>
        public static float Footfall(int i)=>WalkFrom+i*Cadence+Swing;
        private readonly Vector3[] prints=new Vector3[Paces+2];
        public NativeIntroThreshold():base(Id,Total,CardAt,ThroughAt,0xffd79a)
        {
            // In the slit, the feet together; then the walk, and the hero stance on the mark (the left foot forward, turned to the lens).
            Start=Stance(.1f,From+.03f,.08f,-.1f,From-.03f,-.08f,0);
            var steps=new System.Collections.Generic.List<NativeEntrancePose.Step>();
            Walk(steps,Path,false,Paces,WalkFrom,Cadence,Swing,.09f,.08f,.09f);
            var hero=Stance(.15f,.07f,.14f,-.14f,-.09f,-.36f);
            steps.Add(new NativeEntrancePose.Step(true,WalkFrom+Paces*Cadence,Footfall(Paces),hero[0],hero[1],hero[2],.07f));
            steps.Add(new NativeEntrancePose.Step(false,Footfall(Paces)+.06f,Footfall(Paces)+.36f,hero[3],hero[4],hero[5],.04f));
            Steps=steps.ToArray();for(int i=0;i<prints.Length;i++)prints[i]=new Vector3(Steps[i].X,NativeEntranceStage.FloorY+.02f,Steps[i].Z);
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // In the cleft (unseen until it steps out): head bowed, arms hanging; the head lifts as the light swells.
            k.Key(0,stand).Root(0,.88f,0,0).Planted(1).J(C,.08f).J(Hd,.3f).J(LA,-.04f,0,.08f).J(LE,-.25f).J(RA,-.04f,0,-.08f).J(RE,-.25f);
            k.Key(ChargeAt,stand).Root(0,.88f,0,0).Planted(1).J(C,.04f).J(Hd,.1f).J(LA,-.04f,0,.08f).J(LE,-.28f).J(RA,-.04f,0,-.08f).J(RE,-.28f);
            // The first step out, leaning into it.
            k.Key(WalkFrom+.2f,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(-.03f)).J(B,.07f).J(C,-.02f).J(Hd,-.02f).J(LA,-.3f,0,.06f).J(LE,-.5f).J(RA,.22f,0,-.06f).J(RE,-.25f).PassKey();
            // The walk: measured and upright, chest up, two keys a step. At each footfall the hips are lowest (the model's legs need a
            // little over 8 cm to reach both prints), turned with the leg landing in front, the shoulders countering them, the arm
            // opposite that leg forward with its elbow bent and the other back and longer; at the passing the hips ride highest and
            // dip on the swinging side, the arms crossing. The chin rises step by step; it glances back over its shoulder as the
            // stones grind shut behind it (Glance), then ahead. (s: +1 when the left foot lands.)
            for(int i=0;i<Paces+1;i++)
            {
                float land=Footfall(i),pass=land+Cadence*.5f;float s=i%2==1?1:-1,chin=-.015f*Math.Min(i,4),easy=i<Paces?1:.5f;
                k.Key(land,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(-.095f)).J(B,.035f)
                    .J(P,0,-s*.12f*easy,0).J(Sp,0,s*.04f*easy,0).J(C,-.06f,s*.1f*easy+Glance(land)*.3f,0).J(Hd,chin,-s*.05f*easy+Glance(land)*.95f,0)
                    .J(LA,s>0?.28f*easy:-.36f*easy,0,.06f).J(LE,s>0?-.22f:-.58f).J(RA,s>0?-.36f*easy:.28f*easy,0,-.06f).J(RE,s>0?-.58f:-.22f).PassKey();
                if(i>=Paces)break;
                k.Key(pass,stand).Root(0,.88f,0,0).Planted(1).Drop(Sink(-.025f)).J(B,.03f)
                    .J(P,0,0,s*.05f).J(Sp,0,0,-s*.02f).J(C,-.06f,Glance(pass)*.3f,0).J(Hd,chin-.01f,Glance(pass)*.95f,s*.015f)
                    .J(LA,-s*.04f,0,.07f).J(LE,-.36f).J(RA,s*.04f,0,-.07f).J(RE,-.36f).PassKey();
            }
            // Onto the mark: the left foot takes the weight (a settle that rings out) as it turns to the lens, the right brought up.
            k.Key(Footfall(Paces)+.16f,stand).Root(0,.88f,0,Facing+.1f).Planted(1).Drop(Sink(-.07f)).J(B,.02f).J(C,-.07f,.05f,0).J(Hd,-.08f,-.02f,0)
                .J(LA,-.08f,0,.1f).J(LE,-.32f).J(RA,.04f,0,-.1f).J(RE,-.3f).ImpactKey();
            // The hero under the card: tall, chest open, chin up, the hands loose and a little away; a last slow lift of the head and a
            // roll of the shoulders back.
            // The hero: the right arm sweeps up across and out aside, the hand open, presenting the gate it came through; it settles a
            // little lower and holds, the chest up, chin up, the head turned to the lens.
            k.Key(CardAt-.2f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.05f)).J(C,-.08f,.06f,0).J(Hd,-.1f,.06f,0).J(RA,-.75f,0,-.25f).J(RE,-.7f).J(LA,-.1f,0,.06f).J(LE,-.34f).PassKey();
            k.Key(CardAt+.15f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.02f)).J(C,-.12f,-.08f,0).J(Hd,-.16f,.08f,0).J(RA,-.15f,0,-1.05f).J(RE,-.12f).J(LA,-.1f,0,.06f).J(LE,-.34f).ImpactKey();
            k.Key(CardAt+.8f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.02f)).J(C,-.12f,-.1f,0).J(Hd,-.17f,.1f,0).J(RA,-.08f,0,-.82f).J(RE,-.16f).J(LA,-.1f,0,.07f).J(LE,-.33f);
            k.Key(Total,stand).Root(0,.88f,0,Facing).Planted(1).Drop(Sink(-.02f)).J(C,-.11f,-.1f,0).J(Hd,-.16f,.12f,0).J(RA,-.06f,0,-.78f).J(RE,-.18f).J(LA,-.1f,0,.07f).J(LE,-.33f);Keys=k;
            RootPath=(t,r)=>new Vector3(0,r.Y,Path.At(t));
            // The camera: centred and formal, pushing in on the cleft; an arc in front as it steps out; low and centred as it comes
            // down the plaza with the stones grinding shut behind it; the closing, wide and centred under the stones.
            Rig.Dolly(0,new Vector3(0,1.55f,12.5f),new Vector3(0,1.95f,7.6f),new Vector3(0,3.2f,SlitZ),new Vector3(0,2.8f,SlitZ),40,36).Ease(NativeIntroEase.InOut)
               .Spline(ThroughAt-.05f,Arc(),new Vector3(0,.1f,0),new Vector3(0,.15f,0),38,34).Follow(NativeEntranceAnchor.Chest,.08f).Ease(NativeIntroEase.InOut)
               .Dolly(CloseAt-.1f,new Vector3(0,.95f,4.6f),new Vector3(0,.9f,4.25f),new Vector3(0,2.3f,SlitZ),new Vector3(0,2.2f,SlitZ+.5f),46,44).In(NativeIntroCut.Blend,.4f)
               .Dolly(CardAt-.55f,new Vector3(0,.62f,6.65f),new Vector3(0,.7f,6.2f),new Vector3(0,1.26f,0),new Vector3(0,1.3f,0),54,52).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,.55f)
               .Bars(0,.9f).Bars(CardAt-.5f,.5f).Black(0,1).Black(.08f,0);
        }
        /// <summary>The arc's eye as it steps out: about a fixed point in front of the stones, from its left side round to its front,
        /// lowering (the look follows the pilot; the eye never takes the body's own sway).</summary>
        private static Vector3[] Arc()
        {
            var p=new Vector3[6];var pivot=new Vector3(0,0,-1.2f);
            for(int i=0;i<p.Length;i++){float u=i/(p.Length-1f),deg=78-66*u,r=5.6f-1.0f*u,h=2.4f-.9f*u;double a=deg*Math.PI/180+Facing;p[i]=pivot+new Vector3((float)Math.Sin(a)*r,h,(float)Math.Cos(a)*r);}
            return p;
        }
        /// <summary>The glance back over its shoulder as the stones grind shut behind it (0..1).</summary>
        private static float Glance(float t)=>NativeEntranceMath.Smooth((t-(CloseAt-.05f))/.35f)*(1-NativeEntranceMath.Smooth((t-(CloseAt+.6f))/.35f));
        /// <summary>The walk out of the slit to the mark (the hips' z, source frame).</summary>
        public static float Walk(float t)=>Path.At(t);
        /// <summary>How far up the seam the crack of light has run (0..1): three jolts, each running up a third.</summary>
        public static float Reach(float t)
        {
            float r=0;for(int i=0;i<Cracks.Length;i++){float a=t-Cracks[i];if(a<=0)break;r=(i+NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01(a/.16f)))/Cracks.Length;}
            return NativeEntranceMath.Clamp01(r);
        }
        /// <summary>How far each stone has slid out from the seam (m): grinding apart on the opening with a judder, shut after.</summary>
        public static float Part(float t)
        {
            if(t<OpenAt)return 0;float a=t-OpenAt;
            float open=PartBy*NativeEntranceMath.Smooth(a/.55f)+.012f*(float)Math.Sin(a*47)*(1-NativeEntranceMath.Clamp01(a/.55f));
            return t<CloseAt?open:PartBy*(1-NativeEntranceMath.Smooth((t-CloseAt)/.6f));
        }
        /// <summary>The seam's light (0..1+): the crack's flicker, the charge's swell, full while parted, a fading line after.</summary>
        public float Seam(float t)
        {
            float crack=0;foreach(var c in Cracks)crack=Math.Max(crack,Flash(t,c,.02f,.35f));
            float charge=NativeEntranceMath.Ramp(t,ChargeAt,OpenAt),open=Part(t)/PartBy;
            float after=t<CloseAt?1:1-.75f*NativeEntranceMath.Ramp(t,CloseAt,CloseAt+1.6f);
            return Math.Max(.35f*Reach(t)+.8f*crack,(.4f+.6f*charge+.8f*open)*after*(t>=ChargeAt?1:0));
        }
        public override float Solid(float t,int bone)=>t<ThroughAt?0:1;
        public override string AccentAt(float t)=>t<ThroughAt?"#000000":"#ffd79a";
        protected override Vector3 Focus(float t,Vector3 chest)=>t<ThroughAt?new Vector3(0,3.0f,SlitZ):chest;
        // The walk is the keyed rig's own (planned steps): the motion bank's Armed-Walk is the sword's guarded combat walk, its legs
        // side-on to the chest (Flashbulb's legs crossed sideways walking at the lens with it).

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("threshold/plaza"))return false;
            float part=Part(t),seam=Seam(t),runes=NativeEntranceMath.Ramp(t,Cracks[0],ChargeAt)*.6f+.9f*NativeEntranceMath.Ramp(t,ChargeAt,OpenAt);
            runes*=1-.6f*NativeEntranceMath.Ramp(t,CloseAt,CardAt);
            set.Piece("threshold/sky",Vector3.Zero,Still,1,White,White*(1-.25f*NativeEntranceMath.Ramp(t,0,Total)));
            set.Piece("threshold/canyon",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("threshold/plaza",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("threshold/stoneL",U(-Gap/2-part,0,SlitZ),Still,1,White,Rune*(.1f+2.2f*runes+.8f*seam));
            set.Piece("threshold/stoneR",U(Gap/2+part,0,SlitZ),Still,1,White,Rune*(.1f+2.2f*runes+.8f*seam));
            set.Piece("threshold/obglow",Vector3.Zero,Still,1,White,Gold*(.1f+1.6f*NativeEntranceMath.Ramp(t,ChargeAt,OpenAt)*(1-.5f*NativeEntranceMath.Ramp(t,CloseAt,CardAt))));
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)
        {
            Plain(set,t,NativeEntranceFx.Hex(0x8a6a58),Gold,.4f+.8f*Seam(t),Dusk);
            // Two dark slabs where the stones would stand, parting on the opening.
            float part=Part(t);var shade=new Vector3(.16f,.12f,.1f);
            for(int s=-1;s<=1;s+=2)set.Primitive(WeaponShape.Sphere,true,U(s*(Gap/2+part+1.0f),3.1f,SlitZ),Quaternion.Identity,new Vector3(2.0f,6.2f,1.4f),shade);
        }

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.DriveProbe=true;
            // A warm dusty haze down the canyon (the sandstone read magenta under the old mauve fog and the arena's own light).
            var fog=NativeEntranceFx.Hex(0x8f6a5e);l.Background=fog;l.Fog=fog;l.FogNear=32;l.FogFar=280;
            l.Sky=NativeEntranceFx.Hex(0x8a80b8,.42f);l.Ground=NativeEntranceFx.Hex(0xc08a60,.32f);
            l.Directional(U(-.86f,.22f,-.5f),Dusk*(.95f-.25f*NativeEntranceMath.Ramp(t,0,Total)));
            l.Directional(new Vector3(.4f,.7f,.6f),NativeEntranceFx.Hex(0x7a8cff,.25f));
            float seam=Seam(t),open=Part(t)/PartBy;
            // The cleft's light on the set: the intro owns its light, and the stage material takes only directionals and the ambient
            // (docs/INTROS-V2.md 3a), so the light pouring out of the slit is a warm directional from it, low over the plaza.
            if(open+.3f*seam>.01f)l.Directional(new Vector3(0,.3f,-1),Gold*(.25f*seam+1.1f*open+.8f*Flash(t,OpenAt,.03f,.5f)));
            if(seam>.01f)l.Add(new Vector3(0,1.8f,SlitZ+.9f),Gold*(1.6f*seam+5*open+5*Flash(t,OpenAt,.03f,.5f)),6+8*open);
            if(t>=ThroughAt-.05f&&t<CloseAt+.5f){var chest=Anchor(NativeEntranceAnchor.Chest,t);l.Add(chest+new Vector3(0,.4f,-.6f),Light*(2.4f*Swell(t,ThroughAt-.05f,.2f,.9f,.8f)),3.5f);}
            if(t>=CardAt-.3f){float c=NativeEntranceMath.Ramp(t,CardAt-.3f,CardAt+.3f);var chest=Anchor(NativeEntranceAnchor.Chest,t);l.Add(chest+new Vector3(1.4f,.7f,1.8f),Dusk*(1.5f*c),6);}
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);float floor=NativeEntranceStage.FloorY;bool low=q<.5f||Context.Low;
            float part=Part(t),open=part/PartBy,seam=Seam(t);
            var foot=new Vector3(0,floor,SlitZ+.06f);
            // Sand blowing across the plaza in low ribbons; blown outward from the slit while it stands open.
            {
                float blow=NativeEntranceMath.Ramp(t,OpenAt,OpenAt+.3f)*(1-NativeEntranceMath.Ramp(t,CloseAt,CloseAt+.3f));int n=low?14:34;
                for(int i=0;i<n;i++)
                {
                    float u=((t*.32f+NativeEntranceMath.Rand(24000,i))%1f);var start=new Vector3(9-18*NativeEntranceMath.Rand(24001,i),floor+.03f+.25f*NativeEntranceMath.Rand(24002,i),-9+15*NativeEntranceMath.Rand(24003,i));
                    var drift=new Vector3(-1.8f,0,.3f);var p=start+drift*(u*4);var v=drift*1.4f;
                    if(blow>.01f){var away=p-foot;away.Y=0;float d=away.Length();if(d>.01f){away/=d;v=Vector3.Lerp(v,away*5,blow*Math.Max(0,1-d/12));}}
                    fx.Streak(p,v,Sand*(.45f*(float)Math.Sin(Math.PI*u)),.02f,.08f,.06f,.6f,u,24000+i);
                }
            }
            // The seam's light: a white core, a gold sheet and a soft amber haze up the cleft, as wide as the slit, as high as the crack.
            float reach=t<ChargeAt?Reach(t):1;
            if(seam>.01f&&reach>.01f)
            {
                float h=SeamH*reach,w=Gap+2*part;var top=new Vector3(0,floor+h,SlitZ+.08f);var axis=new Vector3(0,h,0);
                Line(fx,top,axis,Light*(1.6f*seam),.025f+.55f*w*open,.3f,0,24100);
                Line(fx,top,axis,Gold*(.9f*seam),.08f+.9f*w*open,.4f,0,24101);
                if(!low)Line(fx,top+new Vector3(0,0,.1f),axis,Amber*(.35f*seam),.3f+1.3f*w*open,.5f,0,24102);
                // the crack's tip, flaring as it runs
                float tip=0;foreach(var c in Cracks)tip=Math.Max(tip,Flash(t,c,.02f,.3f));
                if(tip>.01f)fx.Glow(top,Light*(1.5f*tip),0,.22f,1,0,24103);
            }
            // Dust shaken from the stones by each crack, trickling down the seam's faces.
            for(int i=0;i<Cracks.Length;i++)
            {
                float a=t-Cracks[i];if(a<0||a>1.4f)continue;float hgt=SeamH*(i+1)/Cracks.Length;int n=low?3:7;
                for(int j=0;j<n;j++){float u=a/1.4f,side=NativeEntranceMath.Signed(24200+i,j);var p=new Vector3(side*(.15f+.25f*NativeEntranceMath.Rand(24210+i,j)),floor+hgt*(1-.15f*NativeEntranceMath.Rand(24220+i,j))-2.2f*u*u,SlitZ+.75f);fx.Smoke(p,Sand,.28f*(1-u),.18f+.35f*u,u,24230+i*10+j,floor);}
            }
            // The opening: a gust of dust out along the ground from the slit's foot, dust pouring out in the light, rays of light fanning
            // out of the slit.
            NativeEntranceFx.GroundDust(fx,t,OpenAt+.05f,foot+new Vector3(0,.03f,.6f),low?8:20,.6f,3.8f,1.4f,Sand,.5f,.6f,24300,q,floor);
            if(open>.05f)
            {
                int n=low?8:20;
                for(int i=0;i<n;i++)
                {
                    float u=((t*.7f+i/(float)n)%1f);float side=NativeEntranceMath.Signed(24320,i)*.5f*(Gap+2*part);
                    var p=new Vector3(side,floor+.2f+4.6f*NativeEntranceMath.Rand(24321,i)*(1-.4f*u),SlitZ+.3f+3.2f*u);
                    fx.Smoke(p,Vector3.Lerp(Gold,Sand,u),.22f*open*(float)Math.Sin(Math.PI*u),.25f+.6f*u,u,24330+i,floor);
                }
                int rays=low?4:9;
                for(int i=0;i<rays;i++)
                {
                    float ang=-1.2f+2.4f*i/(rays-1f),len=3+2.5f*NativeEntranceMath.Rand(24360,i);var dir=Vector3.Normalize(new Vector3((float)Math.Sin(ang),(float)Math.Cos(ang)*.6f+.15f,.15f));
                    var root=new Vector3(0,floor+1.2f+.9f*(float)Math.Cos(ang),SlitZ+.2f);
                    Line(fx,root+dir*len,dir*len,Gold*(.18f*open*(.7f+.3f*(float)Math.Sin(t*3+i))),.06f+.12f*open,.8f,0,24370+i);
                }
            }
            // Stepping out: light shed off the body as it leaves the slit.
            if(t>=ThroughAt-.05f&&t<ThroughAt+1.3f)
            {
                var chest=Anchor(NativeEntranceAnchor.Chest,t);float lv=Swell(t,ThroughAt-.05f,.2f,.6f,.6f);int n=low?6:14;
                for(int i=0;i<n;i++){float u=((t*1.6f+i/(float)n)%1f);var p=chest+new Vector3(NativeEntranceMath.Signed(24400,i)*.45f,NativeEntranceMath.Signed(24401,i)*.7f+.3f*u,-.3f-1.0f*u);fx.Glow(p,Gold*(.8f*lv*(1-u)),0,.05f,0,u,24400+i);}
            }
            // Footfalls: a little sand off each step on the plaza.
            for(int i=0;i<prints.Length;i++){float at=Steps[i].Land;if(at<ThroughAt)continue;NativeEntranceFx.GroundDust(fx,t,at,prints[i],low?2:4,.08f,.5f,.6f,Sand,.25f,.25f,24450+i*8,q,floor);}
            // The close: dust squeezed out of the seam as the stones grind shut, a last flare up the line.
            NativeEntranceFx.GroundDust(fx,t,CloseAt+.55f,foot+new Vector3(0,.03f,.5f),low?8:18,.4f,2.6f,1.2f,Sand,.45f,.5f,24500,q,floor);
            float seal=Flash(t,CloseAt+.58f,.02f,.3f);if(seal>.01f)Line(fx,new Vector3(0,floor+SeamH,SlitZ+.1f),new Vector3(0,SeamH,0),Light*(1.4f*seal),.05f,.3f,0,24550);
            // The afterglow: motes rising off the runes and the seam.
            if(t>=CloseAt)
            {
                float lv=NativeEntranceMath.Ramp(t,CloseAt,CloseAt+.5f);int n=low?6:16;
                for(int i=0;i<n;i++){float u=((t*.35f+i/(float)n)%1f);float side=(i%2==0?-1:1)*(.35f+.15f*NativeEntranceMath.Rand(24600,i));var p=new Vector3(side,floor+.6f+5.2f*NativeEntranceMath.Rand(24601,i)+1.2f*u,SlitZ+.85f);fx.Glow(p,Rune*(.6f*lv*(float)Math.Sin(Math.PI*u)),0,.035f,0,u,24610+i);}
            }
        }
        /// <summary>Warm afterimages as it steps out of the light.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float w=Swell(t,ThroughAt,.1f,.4f,.4f);if(w<.02f||into.Length<2)return 0;
            for(int i=0;i<2;i++)into[i]=new NativeEntranceGhost{Lag=.07f*(i+1),Opacity=.28f*w*(1-i*.35f),Color=Gold};
            return 2;
        }

        // ---- the name card and the sound ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Y=CardY;card.Style=NativeIntroCardStyle.Rise;card.Ink=new Vector3(1,.96f,.9f);card.Glow=Gold*1.4f;
        }
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.threshold.rumble","structure.rumble","darkness",.3f,.8f),
            new NativeIntroCue(.6f,"intro2.threshold.crack","blast.debris","stone",.7f),
            new NativeIntroCue(1.2f,"intro2.threshold.crack","blast.debris","stone",.75f,.94f),
            new NativeIntroCue(1.75f,"intro2.threshold.crack","blast.debris","stone",.8f,1.05f),
            new NativeIntroCue(ChargeAt,"intro2.threshold.charge","chip.delayed.charge","install",.55f),
            new NativeIntroCue(OpenAt,"intro2.threshold.split","structure.boom","stone",.85f),
            new NativeIntroCue(ThroughAt,"intro2.threshold.through","magic.blink-strike","dash",.55f),
            new NativeIntroCue(Footfall(0),"intro2.threshold.step","move.step.stone","stone",.4f),
            new NativeIntroCue(Footfall(2),"intro2.threshold.step","move.step.stone","stone",.42f,.96f),
            new NativeIntroCue(CloseAt,"intro2.threshold.seal","blast.implosion","darkness",.7f),
            new NativeIntroCue(Footfall(4),"intro2.threshold.step","move.step.stone","stone",.44f,1.04f),
            new NativeIntroCue(CardAt,"intro2.threshold.name","chip.critical","install",.75f),
        };
        public override NativeIntroCue[] Cues=>cues;
    }
}
