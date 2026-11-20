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
        public const float ChargeAt=2.4f,OpenAt=3.0f,ThroughAt=3.4f,WalkAt=3.5f,CloseAt=4.9f,ArriveAt=6.1f;
        public override string Title=>"Threshold";
        // The set (Unity space, generic_threshold.py): the seam's plane, the stones' gap at their feet at rest, the seam's height, how
        // far each stone slides out when they part, and where the walk out begins (in the slit).
        public const float SlitZ=-3.2f,Gap=.25f,SeamH=6.3f,PartBy=.45f,From=-3.7f,Stride=.53f,Cadence=.37f;
        public const int Paces=7;
        private static readonly Vector3 Light=NativeEntranceFx.Hex(0xfff1d0),Gold=NativeEntranceFx.Hex(0xffc878),Amber=NativeEntranceFx.Hex(0xff9a4a),
            Dusk=NativeEntranceFx.Hex(0xff9a5a),Sand=NativeEntranceFx.Hex(0xc9a074),Rune=NativeEntranceFx.Hex(0xffd79a);
        public NativeIntroThreshold():base(Id,Total,CardAt,ThroughAt,0xffd79a)
        {
            // The walk's stance and steps are in the world's frame (facing 0: the walk runs straight down +z; Stance and Step otherwise
            // turn their x and z by the hero facing about the mark, which 5 m back put the feet a metre to the side of the body).
            Start=Stance(.13f,From,.1f,-.13f,From-.2f,-.12f,0);
            var steps=new System.Collections.Generic.List<NativeEntrancePose.Step>();
            for(int i=0;i<Paces;i++){float lift=WalkAt+.02f+i*Cadence;bool l=i%2==1;steps.Add(Step(l,lift,lift+Cadence-.03f,l?.12f:-.12f,From+Stride*(i+1),l?.1f:-.1f,.1f,0));}
            // Onto the mark: the left foot, then the right brought up beside it (the hero stance).
            steps.Add(Step(true,ArriveAt+.02f,ArriveAt+.26f,.17f,.06f,.12f,.06f));
            steps.Add(Step(false,ArriveAt+.2f,ArriveAt+.44f,-.18f,-.1f,-.42f,.06f));
            Steps=steps.ToArray();
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // In the light: upright, arms loose, chin a little down; then the walk out, arms swinging, chin coming up.
            k.Key(0,stand).Root(0,.88f,From,Facing+.22f).Planted(1).J(Hd,.1f).J(LA,-.12f,0,.18f).J(LE,-.3f).J(RA,-.12f,0,-.18f).J(RE,-.3f);
            k.Key(WalkAt,stand).Root(0,.88f,From,Facing+.22f).Planted(1).Drop(-.02f).J(Hd,.06f).J(LA,-.12f,0,.2f).J(LE,-.3f).J(RA,-.12f,0,-.2f).J(RE,-.3f);
            // The walk out, measured and upright, two keys a step (the feet are the planned steps above): riding a little higher at the
            // swing's passing, the arms crossing; settling at the heel's contact, the hips turning gently with the leg in front, the
            // shoulders countering and the arm opposite that leg swinging forward. The chin rises step by step out of the light.
            // (s: +1 when the left foot swings; the source frame's left is +x.)
            for(int i=0;i<Paces;i++)
            {
                float lift=WalkAt+.02f+i*Cadence;float s=i%2==1?1:-1,chin=.04f-.025f*i;
                k.Key(lift+.18f,stand).Root(0,.88f,0,Facing+.22f).Planted(1).Drop(-.015f)
                    .J(P,0,0,-s*.03f).J(C,-.02f,0,0).J(Hd,chin,0,0).J(LA,s*.03f,0,.14f).J(LE,-.3f).J(RA,-s*.03f,0,-.14f).J(RE,-.3f).PassKey();
                k.Key(lift+Cadence-.03f,stand).Root(0,.88f,0,Facing+.22f).Planted(1).Drop(-.045f)
                    .J(P,0,-s*.08f,0).J(Sp,0,s*.04f,0).J(C,0,s*.08f,0).J(Hd,chin+.02f,-s*.03f,0)
                    .J(LA,s*.26f,0,.15f).J(LE,s>0?-.2f:-.45f).J(RA,-s*.28f,0,-.15f).J(RE,s>0?-.45f:-.2f).PassKey();
            }
            k.Key(ArriveAt+.12f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.05f).J(C,-.04f).J(Hd,-.08f).J(LA,-.12f,0,.3f).J(LE,-.32f).J(RA,-.12f,0,-.3f).J(RE,-.32f).ImpactKey();
            k.Key(CardAt+.05f,NativeEntranceKeys.Hero()).Planted(1).J(Hd,-.12f,0,0).J(C,-.06f,0,0);
            k.Key(Total,NativeEntranceKeys.Hero()).Planted(1).J(Hd,-.14f,.06f,0).J(C,-.06f,-.08f,0);Keys=k;
            RootPath=(t,r)=>t<ArriveAt+.1f?new Vector3(0,r.Y,Walk(t)):new Vector3(0,r.Y,0);
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
        /// <summary>The walk out of the slit to the mark (z, source frame).</summary>
        public static float Walk(float t)=>From-From*(NativeEntranceMath.Clamp01((t-WalkAt)/(ArriveAt-WalkAt))*.9f+.1f*NativeEntranceMath.Smooth((t-WalkAt)/(ArriveAt-WalkAt)));
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
            var fog=NativeEntranceFx.Hex(0x7a5a72);l.Background=fog;l.Fog=fog;l.FogNear=32;l.FogFar=280;
            l.Sky=NativeEntranceFx.Hex(0x8a80b8,.42f);l.Ground=NativeEntranceFx.Hex(0xc08a60,.32f);
            l.Directional(U(-.86f,.22f,-.5f),Dusk*(.95f-.25f*NativeEntranceMath.Ramp(t,0,Total)));
            l.Directional(new Vector3(.4f,.7f,.6f),NativeEntranceFx.Hex(0x7a8cff,.25f));
            float seam=Seam(t),open=Part(t)/PartBy;
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
            if(t>=ThroughAt-.05f&&t<WalkAt+1.2f)
            {
                var chest=Anchor(NativeEntranceAnchor.Chest,t);float lv=Swell(t,ThroughAt-.05f,.2f,.6f,.6f);int n=low?6:14;
                for(int i=0;i<n;i++){float u=((t*1.6f+i/(float)n)%1f);var p=chest+new Vector3(NativeEntranceMath.Signed(24400,i)*.45f,NativeEntranceMath.Signed(24401,i)*.7f+.3f*u,-.3f-1.0f*u);fx.Glow(p,Gold*(.8f*lv*(1-u)),0,.05f,0,u,24400+i);}
            }
            // Footfalls: a little sand off each step on the plaza.
            for(int i=0;i<Paces;i++){float at=WalkAt+.02f+i*Cadence+Cadence-.03f;NativeEntranceFx.GroundDust(fx,t,at,new Vector3(i%2==1?.12f:-.12f,floor+.02f,From+Stride*(i+1)),low?2:4,.08f,.5f,.6f,Sand,.25f,.25f,24450+i*8,q,floor);}
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
            new NativeIntroCue(WalkAt+.36f,"intro2.threshold.step","move.step.stone","stone",.4f),
            new NativeIntroCue(WalkAt+.36f+2*Cadence,"intro2.threshold.step","move.step.stone","stone",.42f,.96f),
            new NativeIntroCue(CloseAt,"intro2.threshold.seal","blast.implosion","darkness",.7f),
            new NativeIntroCue(WalkAt+.36f+4*Cadence,"intro2.threshold.step","move.step.stone","stone",.44f,1.04f),
            new NativeIntroCue(CardAt,"intro2.threshold.name","chip.critical","install",.75f),
        };
        public override NativeIntroCue[] Cues=>cues;
    }
}
