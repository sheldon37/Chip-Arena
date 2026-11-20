using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Waygate" (docs/INTROS-GENERIC.md "Waygate"). 8.2 s, the name at 6.4 s. Dusk in a canyon: an ancient ring gate of carved stone on a
    /// stepped dais at the end of a ruined plaza. Its inner ring turns and locks five times with heavy clunks, each lighting a lamp in
    /// the outer ring; light races round the channels; the gate opens on a turning disc of light that pulls the sand toward it. A figure
    /// takes shape in the light and floats out, arms spread, glides over the dais and settles on the plaza; the disc collapses to a point
    /// behind it and the gate goes dark; it steps forward under the ring's afterglow. Camera: formal (a centred push-in, a quarter orbit as
    /// it comes through, centred and low under the ring like a halo).
    /// </summary>
    public sealed class NativeIntroWaygate:NativeIntroGenericScript
    {
        public const string Id=NativeIntroCatalog.Prefix+"waygate";
        public const float Total=8.2f,CardAt=6.4f;
        public static readonly float[] Locks={.5f,1.1f,1.6f,2.0f,2.3f};
        public const float ChargeAt=2.4f,OpenAt=3.0f,ThroughAt=3.6f,LandAt=4.65f,CloseAt=5.2f,StepAt=5.55f;
        public override string Title=>"Waygate";
        // The set (Unity space, generic_waygate.py): the ring's centre and its lamps (degrees from Unity +x toward +y, seen from +z).
        private static readonly Vector3 Ring=U(0,4.05f,-7.0f);
        private const float KeyR=3.5f,FaceZ=.5f;
        private static readonly float[] KeyDeg={10,50,130,170,250,290};
        private static readonly Vector3 Glyph=NativeEntranceFx.Hex(0x7fe6ff),Core=NativeEntranceFx.Hex(0xe8fbff),Deep=NativeEntranceFx.Hex(0x5f7dff),Dusk=NativeEntranceFx.Hex(0xff9a5a),
            Sand=NativeEntranceFx.Hex(0xc9a074),Gold=NativeEntranceFx.Hex(0xffc878);
        public NativeIntroWaygate():base(Id,Total,CardAt,ThroughAt,0x7fe6ff)
        {
            Start=Stance(.17f,-.95f,.1f,-.17f,-1.08f,-.4f);
            Steps=new[]{Step(true,StepAt,StepAt+.4f,.16f,-.38f,.1f,.08f),Step(false,StepAt+.32f,StepAt+.72f,-.18f,-.12f,-.42f,.08f),Step(true,StepAt+.62f,StepAt+.95f,.17f,.06f,.12f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // Taking shape in the light: arms spread wide, palms out, legs together and soft, head bowed; lifting as it floats out.
            float[] float_(float head,float spread)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.PlantL]=0;v[NativeEntranceKeys.PlantR]=0;v[NativeEntranceKeys.Yaw]=Facing;
                NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,C,-.06f,0,0);
                NativeEntranceKeys.Set(v,LA,-.25f,0,spread);NativeEntranceKeys.Set(v,LE,-.22f,0,0);NativeEntranceKeys.Set(v,RA,-.25f,0,-spread);NativeEntranceKeys.Set(v,RE,-.22f,0,0);
                NativeEntranceKeys.Set(v,LL,-.08f,0,.03f);NativeEntranceKeys.Set(v,LK,.25f,0,0);NativeEntranceKeys.Set(v,RL,.02f,0,-.03f);NativeEntranceKeys.Set(v,RK,.32f,0,0);});
            k.Key(0,float_(.45f,.9f));
            k.Key(ThroughAt,float_(.42f,1.05f));
            k.Key(ThroughAt+.45f,float_(.05f,1.4f)).PassKey();
            k.Key(LandAt-.25f,float_(-.12f,1.3f));
            // Settling: the toes find the stone, a soft give in the knees, the arms lowering.
            k.Key(LandAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.1f).J(B,.06f).J(Hd,-.08f).J(LA,-.2f,0,.95f).J(LE,-.3f).J(RA,-.2f,0,-.95f).J(RE,-.3f).ImpactKey();
            k.Key(CloseAt,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.03f).J(Hd,-.04f).J(LA,-.15f,0,.5f).J(LE,-.3f).J(RA,-.15f,0,-.5f).J(RE,-.3f);
            k.Key(CloseAt+.12f,stand).Root(0,.88f,0,Facing).Planted(1).Drop(-.06f).J(C,-.04f).J(Hd,.04f).J(LA,-.12f,0,.42f).J(LE,-.32f).J(RA,-.12f,0,-.42f).J(RE,-.32f).ImpactKey();
            k.Key(CardAt-.3f,NativeEntranceKeys.Hero()).Planted(1).J(Hd,-.12f,0,0).J(C,-.06f,0,0);
            k.Key(Total,NativeEntranceKeys.Hero()).Planted(1).J(Hd,-.14f,.06f,0).J(C,-.06f,-.08f,0);Keys=k;
            RootPath=(t,r)=>t<LandAt?Floating(t):t<StepAt?new Vector3(0,.88f,-1.0f):new Vector3(0,.88f,-1.0f+1.0f*NativeEntranceMath.Smooth((t-StepAt)/.95f));
            // The camera: centred and formal, pushing in; a quarter orbit as it comes through; low and centred under the ring.
            Rig.Dolly(0,new Vector3(0,1.55f,13.5f),new Vector3(0,2.1f,8.2f),Ring+new Vector3(0,-.4f,0),Ring+new Vector3(0,-.2f,0),40,36).Ease(NativeIntroEase.InOut)
               .Spline(ThroughAt-.05f,Arc(),new Vector3(0,.1f,0),new Vector3(0,.15f,0),38,34).Follow(NativeEntranceAnchor.Chest,.08f).Ease(NativeIntroEase.InOut)
               .Dolly(LandAt+.05f,new Vector3(0,.95f,4.6f),new Vector3(0,.9f,4.25f),new Vector3(0,2.3f,-4.5f),new Vector3(0,2.2f,-4f),46,44).In(NativeIntroCut.Blend,.4f)
               .Dolly(CardAt-.35f,new Vector3(0,.78f,4.25f),new Vector3(0,.8f,3.75f),new Vector3(0,2.55f,-3),new Vector3(0,2.6f,-3),44,42).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,.6f)
               .Bars(0,.9f).Bars(CardAt-.5f,.5f).Black(0,1).Black(.08f,0);
        }
        /// <summary>The quarter orbit's eye as it comes through: an arc about a fixed point between the gate and the landing, from its
        /// left side round to its front, lowering (the look follows the pilot; the eye never takes the body's own sway).</summary>
        private static Vector3[] Arc()
        {
            var p=new Vector3[6];var pivot=new Vector3(0,0,-3.3f);
            for(int i=0;i<p.Length;i++){float u=i/(p.Length-1f),deg=78-66*u,r=5.6f-1.0f*u,h=2.4f-.9f*u;double a=deg*Math.PI/180+Facing;p[i]=pivot+new Vector3((float)Math.Sin(a)*r,h,(float)Math.Cos(a)*r);}
            return p;
        }
        /// <summary>The float out of the gate: from the light's centre, out over the dais, down onto the plaza in front of it.</summary>
        public static Vector3 Floating(float t)
        {
            float u=NativeEntranceMath.Clamp01((t-ThroughAt)/(LandAt-ThroughAt));
            float z=Ring.Z+.3f+(-1.0f-Ring.Z-.3f)*NativeEntranceMath.Smooth(u);
            float y=NativeEntranceMath.Lerp(3.25f,.88f,NativeEntranceMath.Smoother(u))+.25f*(float)Math.Sin(Math.PI*u)*(1-u);
            return new Vector3(0,y,z);
        }
        /// <summary>The inner ring's turn (radians): five steps of 40 degrees, each swinging in fast and settling on its lock.</summary>
        public static float InnerTurn(float t)
        {
            float a=0;
            foreach(var at in Locks){float s=t-(at-.28f);if(s<=0)continue;float u=NativeEntranceMath.Clamp01(s/.28f);float e=u<1?u*u*(3-2*u):1+.06f*(float)(Math.Exp(-12*(s-.28f))*Math.Sin((s-.28f)*30));a+=.698f*e;}
            return a-t*.04f*NativeEntranceMath.Ramp(t,OpenAt,CloseAt)*0;
        }
        /// <summary>Lamp `i`'s light (0..1+): lit on its lock (the sixth on the charge), steady while the gate is open, fading after.</summary>
        private float LampLevel(int i,float t)
        {
            float at=i<Locks.Length?Locks[i]:ChargeAt;if(t<at)return 0;
            return (1.1f+1.4f*Flash(t,at,.02f,.25f))*(1-.85f*NativeEntranceMath.Ramp(t,CloseAt,CloseAt+1.4f))+.15f;
        }
        /// <summary>The ring's channels: dark, racing alight on the charge, full while open, an afterglow after the close.</summary>
        public static float Channels(float t)=>NativeEntranceMath.Ramp(t,ChargeAt,OpenAt)*(1-.7f*NativeEntranceMath.Ramp(t,CloseAt,CloseAt+1.6f));
        /// <summary>The disc of light's radius (m).</summary>
        public static float Disc(float t)
        {
            if(t<OpenAt||t>=CloseAt+.28f)return 0;float a=t-OpenAt;float open=a<.3f?2.65f*NativeEntranceMath.EaseOut(a/.3f)*(1+.08f*(float)Math.Sin(a*20)):2.65f+.04f*(float)Math.Sin(t*3);
            return t<CloseAt?open:open*(1-NativeEntranceMath.EaseIn((t-CloseAt)/.28f));
        }
        public override float Solid(float t,int bone)=>t<ThroughAt-.05f?0:1;
        public override string AccentAt(float t)=>t<ThroughAt?"#000000":t<CloseAt?"#bff6ff":"#7fe6ff";
        protected override Vector3 Focus(float t,Vector3 chest)=>t<ThroughAt?Ring:chest;
        /// <summary>The source-frame point at angle `deg` (the set's degrees, seen from +z) and radius `r` on the ring's face.</summary>
        private static Vector3 OnRing(float deg,float r,float z=FaceZ){double a=deg*Math.PI/180;return Ring+new Vector3(-(float)Math.Cos(a)*r,(float)Math.Sin(a)*r,z);}

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("waygate/plaza"))return false;
            float ch=Channels(t);
            set.Piece("waygate/sky",Vector3.Zero,Still,1,White,White*(1-.25f*NativeEntranceMath.Ramp(t,0,Total)));
            set.Piece("waygate/canyon",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("waygate/plaza",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("waygate/inner",Ring,URoll(InnerTurn(t)),1,White,Glyph*(.15f+2.6f*ch+.6f*NativeEntranceMath.Ramp(t,Locks[0],ChargeAt)));
            set.Piece("waygate/channels",Vector3.Zero,Still,1,White,Glyph*(3.2f*ch+2.2f*Flash(t,OpenAt,.03f,.4f)));
            for(int i=0;i<KeyDeg.Length;i++){float lv=LampLevel(i,t);set.Piece("waygate/lens",OnRing(KeyDeg[i],KeyR,FaceZ+.165f),URoll((float)(KeyDeg[i]*Math.PI/180-Math.PI/2)),1,White,Glyph*(2.4f*lv));}
            set.Piece("waygate/obglow",Vector3.Zero,Still,1,White,Gold*(.1f+1.6f*NativeEntranceMath.Ramp(t,ChargeAt,OpenAt)*(1-.5f*NativeEntranceMath.Ramp(t,CloseAt,CardAt))));
            set.Piece("waygate/inlay",Vector3.Zero,Still,1,White,Glyph*(.05f+1.8f*Swell(t,CloseAt+.1f,.25f,.6f,1.2f)+.5f*NativeEntranceMath.Ramp(t,CloseAt,CardAt)));
            float r=Disc(t);if(r>.01f)set.Piece("waygate/membrane",Ring+new Vector3(0,0,.05f),URoll(t*1.3f),r,White,White*(2.2f+1.4f*Flash(t,OpenAt,.03f,.35f)));
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)
        {
            Plain(set,t,NativeEntranceFx.Hex(0x8a6a58),Glyph,.4f+1.6f*Flash(t,CloseAt,.03f,.6f)+.6f*Channels(t),Dusk);
            // The gate's light still opens behind the pilot without the bank: a soft disc of glow where the ring would stand.
            float r=Disc(t);if(r>.01f)set.Primitive(WeaponShape.Sphere,true,Ring,Quaternion.Identity,new Vector3(r*2,r*2,.05f),Core*.06f);
        }

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.DriveProbe=true;
            var fog=NativeEntranceFx.Hex(0x7a5a72);l.Background=fog;l.Fog=fog;l.FogNear=32;l.FogFar=280;
            l.Sky=NativeEntranceFx.Hex(0x8a80b8,.42f);l.Ground=NativeEntranceFx.Hex(0xc08a60,.32f);
            l.Directional(U(-.86f,.22f,-.5f),Dusk*(.95f-.25f*NativeEntranceMath.Ramp(t,0,Total)));
            l.Directional(new Vector3(.4f,.7f,.6f),NativeEntranceFx.Hex(0x7a8cff,.25f));
            float ch=Channels(t),r=Disc(t);
            if(ch+r>.01f)l.Add(Ring+new Vector3(0,0,1.2f),Glyph*(3*ch+3.5f*(r/2.65f)+6*Flash(t,OpenAt,.03f,.4f)),11);
            float lamps=0;for(int i=0;i<KeyDeg.Length;i++)lamps+=LampLevel(i,t);if(lamps>.01f)l.Add(Ring+new Vector3(0,3.4f,.8f),Glyph*(.35f*lamps),7);
            if(t>=ThroughAt-.05f&&t<CloseAt+.5f){var chest=Anchor(NativeEntranceAnchor.Chest,t);l.Add(chest+new Vector3(0,.2f,.4f),Core*(2.2f*Swell(t,ThroughAt-.05f,.2f,.8f,.6f)),3.5f);}
            float close=Flash(t,CloseAt,.03f,.45f);if(close>.01f)l.Add(Ring+new Vector3(0,-1,1.5f),Core*(10*close),12);
            if(t>=CardAt-.3f){float c=NativeEntranceMath.Ramp(t,CardAt-.3f,CardAt+.3f);var chest=Anchor(NativeEntranceAnchor.Chest,t);l.Add(chest+new Vector3(1.4f,.7f,1.8f),Dusk*(1.5f*c),6);}
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);float floor=NativeEntranceStage.FloorY;bool low=q<.5f||Context.Low;
            // Sand blowing across the plaza in low ribbons, pulled toward the gate while it is open.
            {
                float pull=NativeEntranceMath.Ramp(t,OpenAt,OpenAt+.4f)*(1-NativeEntranceMath.Ramp(t,CloseAt,CloseAt+.2f));int n=low?14:34;
                for(int i=0;i<n;i++)
                {
                    float u=((t*.32f+NativeEntranceMath.Rand(24000,i))%1f);var start=new Vector3(9-18*NativeEntranceMath.Rand(24001,i),floor+.03f+.25f*NativeEntranceMath.Rand(24002,i),-9+15*NativeEntranceMath.Rand(24003,i));
                    var drift=new Vector3(-1.8f,0,.3f);var p=start+drift*(u*4);var toGate=new Vector3(Ring.X,floor+.1f,Ring.Z+1.5f)-p;var v=drift*1.4f;
                    if(pull>.01f){p+=toGate*(.45f*pull*u);v=Vector3.Lerp(v,Vector3.Normalize(toGate)*4,pull);}
                    fx.Streak(p,v,Sand*(.45f*(float)Math.Sin(Math.PI*u)),.02f,.08f,.06f,.6f,u,24000+i);
                }
            }
            // Each lock: dust shaken off the ring, a spark at its lamp.
            for(int i=0;i<Locks.Length;i++)
            {
                float a=t-Locks[i];if(a<0||a>1.2f)continue;var lamp=OnRing(KeyDeg[i],KeyR,FaceZ+.2f);
                int n=low?3:7;for(int j=0;j<n;j++){float u=a/1.2f,ang=(KeyDeg[i]+NativeEntranceMath.Signed(24100+i,j)*25)*(float)(Math.PI/180);var p=Ring+new Vector3(-(float)Math.Cos(ang)*3.9f,(float)Math.Sin(ang)*3.9f-1.2f*u*u,.3f);fx.Smoke(p,Sand,.3f*(1-u),.2f+.6f*u,u,24100+i*10+j,floor);}
                NativeEntranceFx.Sparks(fx,t,Locks[i],lamp,new Vector3(0,.2f,1),.8f,low?3:7,2.4f,.35f,9.8f,.4f,Core*2,Glyph,.01f,24150+i*10,q,floor);
            }
            // The charge: motes of light spiralling into the ring.
            if(t>=ChargeAt-.1f&&t<OpenAt+.15f)
            {
                int n=low?8:20;float lv=NativeEntranceMath.Ramp(t,ChargeAt-.1f,ChargeAt+.2f);
                for(int i=0;i<n;i++){float u=((t-ChargeAt)*1.6f+i/(float)n)%1f;float ang=i*.9f+u*5,rr=4.6f*(1-u)+.2f;var p=Ring+new Vector3((float)Math.Cos(ang)*rr,(float)Math.Sin(ang)*rr,.6f+.4f*(1-u));fx.Glow(p,Glyph*(1.4f*lv*(.3f+.7f*u)),0,.06f,0,u,24200+i);}
            }
            // Open: the vortex's arms turning on the disc, a core of white, light spilling over the dais.
            float r=Disc(t);
            if(r>.05f)
            {
                var c=Ring+new Vector3(0,0,.12f);fx.Glow(c,Core*(1.6f),0,1.1f*r/2.65f+.2f,1,0,24300);fx.Glow(c,Glyph*(.8f),0,2.6f*r/2.65f,1,0,24301);
                int arms=3,per=low?6:12;
                for(int a=0;a<arms;a++)for(int j=0;j<per;j++)
                {
                    float f=j/(float)per,ang=a*2.094f+f*3.2f-t*2.2f,rr=r*(.15f+.85f*f);var p=c+new Vector3((float)Math.Cos(ang)*rr,(float)Math.Sin(ang)*rr,.02f);
                    var tan=new Vector3(-(float)Math.Sin(ang),(float)Math.Cos(ang),0);fx.Streak(p,tan*(-2.6f*rr),Vector3.Lerp(Core,Deep,f)*(1.3f*(1-f*.6f)),.05f*(1-f*.5f)+.02f,.08f,.12f,.6f,f,24310+a*20+j);
                }
            }
            // Coming through: light shed off the body as it floats out, the disc's surface rippling where it passed.
            if(t>=ThroughAt-.05f&&t<LandAt+.4f)
            {
                var chest=Anchor(NativeEntranceAnchor.Chest,t);float lv=Swell(t,ThroughAt-.05f,.2f,.7f,.5f);int n=low?6:16;
                for(int i=0;i<n;i++){float u=((t*1.8f+i/(float)n)%1f);var p=chest+new Vector3(NativeEntranceMath.Signed(24400,i)*.5f,NativeEntranceMath.Signed(24401,i)*.7f+.3f*u,-.4f-1.2f*u);fx.Glow(p,Glyph*(.9f*lv*(1-u)),0,.05f,0,u,24400+i);}
                float ripple=Flash(t,ThroughAt+.05f,.05f,.4f);if(ripple>.01f)fx.Glow(Ring+new Vector3(0,-.8f,.3f),Core*(1.6f*ripple),0,1.6f,1,0,24450);
            }
            // The landing: a soft puff of sand round the feet.
            NativeEntranceFx.GroundDust(fx,t,LandAt,new Vector3(0,floor+.03f,-1.0f),low?6:14,.25f,1.6f,1.2f,Sand,.4f,.35f,24500,q,floor);
            // The close: light rushing into the point, a white blink, a gust of sand off the dais.
            {
                float a=t-(CloseAt-.12f);
                if(a>=0&&a<.42f){int n=low?10:24;for(int i=0;i<n;i++){float u=a/.42f,ang=i*.2618f;var dir=new Vector3((float)Math.Cos(ang),(float)Math.Sin(ang),0);var p=Ring+dir*(3.0f*(1-u)+.1f)+new Vector3(0,0,.2f);fx.Streak(p,-dir*(7*(1-u)+1),Core*(1.5f*u),.03f,.1f,.06f,.5f,u,24600+i);}}
                float blink=Flash(t,CloseAt+.28f,.02f,.25f);if(blink>.01f)fx.Glow(Ring+new Vector3(0,0,.3f),Core*(3.2f*blink),0,1.8f,1,0,24650);
                NativeEntranceFx.GroundDust(fx,t,CloseAt+.3f,new Vector3(0,floor+.05f,-2.8f),low?8:20,1.2f,3.6f,1.5f,Sand,.45f,.55f,24660,q,floor);
            }
            // The afterglow: motes rising off the ring's channels and the plaza's inlay.
            if(t>=CloseAt)
            {
                float lv=NativeEntranceMath.Ramp(t,CloseAt,CloseAt+.4f);int n=low?8:20;
                for(int i=0;i<n;i++){float u=((t*.35f+i/(float)n)%1f);bool ring=i%2==0;float ang=i*1.37f;var p=ring?Ring+new Vector3((float)Math.Cos(ang)*3.4f,(float)Math.Sin(ang)*3.4f+1.4f*u,.6f):new Vector3((float)Math.Cos(ang)*2.0f,floor+.05f+1.6f*u,(float)Math.Sin(ang)*2.0f);fx.Glow(p,(ring?Glyph:Gold)*(.5f*lv*(float)Math.Sin(Math.PI*u)),0,.035f,0,u,24700+i);}
            }
        }
        /// <summary>Afterimages as it comes through the light.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float w=Swell(t,ThroughAt,.1f,.5f,.4f);if(w<.02f||into.Length<3)return 0;
            for(int i=0;i<3;i++)into[i]=new NativeEntranceGhost{Lag=.06f*(i+1),Opacity=.3f*w*(1-i*.25f),Color=Glyph};
            return 3;
        }

        // ---- the name card and the sound ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Style=NativeIntroCardStyle.Rise;card.Ink=new Vector3(1,.96f,.9f);card.Glow=Glyph*1.5f;
        }
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.waygate.drone","magic.singularity","darkness",.3f,.8f),
            new NativeIntroCue(.5f,"intro2.waygate.lock","structure.land.metal","stone",.75f),
            new NativeIntroCue(1.1f,"intro2.waygate.lock","structure.land.metal","stone",.75f,.96f),
            new NativeIntroCue(1.6f,"intro2.waygate.lock","structure.land.metal","stone",.78f,1.02f),
            new NativeIntroCue(2.0f,"intro2.waygate.lock","structure.land.metal","stone",.8f,.94f),
            new NativeIntroCue(2.3f,"intro2.waygate.lock","structure.land.metal","stone",.85f,1.06f),
            new NativeIntroCue(ChargeAt,"intro2.waygate.charge","chip.delayed.charge","install",.55f),
            new NativeIntroCue(OpenAt,"intro2.waygate.open","chip.teleport","water",.85f),
            new NativeIntroCue(ThroughAt,"intro2.waygate.through","magic.blink-strike","dash",.6f),
            new NativeIntroCue(LandAt,"intro2.waygate.touch","move.land","stone",.45f),
            new NativeIntroCue(CloseAt,"intro2.waygate.close","blast.implosion","darkness",.75f),
            new NativeIntroCue(StepAt+.4f,"intro2.waygate.step","move.step.stone","stone",.4f),
            new NativeIntroCue(CardAt,"intro2.waygate.name","chip.critical","install",.75f),
        };
        public override NativeIntroCue[] Cues=>cues;
    }
}
