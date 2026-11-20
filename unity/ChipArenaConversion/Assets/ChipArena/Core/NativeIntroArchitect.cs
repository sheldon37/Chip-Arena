using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "DEV · Architect of Reality", the owner's dev intro (owner 2026-11-20: "a new dev intro for myself ... something 'bad-ass,
    /// godlike' really show the authority of the dev"; docs/INTRO-DEV-ARCHITECT.md). 15.7 s on the owner's song "Obey" (intro time =
    /// song time - 5.17 s), the name at the drop, 12.63 s. Every beat sits on a transient measured in the song:
    /// - 0.00 the strike: a column of light slams into the old arena (the entrance stage every intro used, modelled); its floor
    ///   cracks with light and the world glitches with the music's stutters;
    /// - 1.97 the break: the arena lets go; plates, dais and pylons lift, their stone falls in to wireframe, the wire breaks into
    ///   code glyphs and the glyphs into light; under it the void and its grid (2.97 a tear runs across the world);
    /// - 3.505 the sub returns: the presence, a point of light before any body, the old world's shards orbiting it, glyphs pouring in;
    /// - 5.035 the draft: the body traced in lines of light; 6.38-6.66 it condenses from the core out and the visor ignites;
    /// - 8.03 stillness: the architect floats, perfectly still, while pens of light draw the new world's plan in the void;
    /// - 10.5 the gathering: the arms rise and the new world flies in from the void as wireframes, a step nearer on each of the
    ///   riser's five stutters; 11.82 the inhale (all holds); 12.27/12.50 the hands close;
    /// - 12.62 THE DROP: everything snaps into place at once, solid; light floods the seams; the architect lands; the live name
    ///   is written into the sky in light, letter by letter, and settles; the crown turns; 15.3 fade with the song.
    /// Presentation only: pure functions of the presentation clock, allocation-free per frame (the name's strokes are laid out
    /// once per name), thinned by quality and Low; Reduced effects keeps every beat without shake or fast flashes.
    /// </summary>
    public sealed class NativeIntroArchitect:NativeIntroV2Script
    {
        // ---- length and the song's beats (intro s; measured from the owner's track, intro = song - 5.17 s) ----
        public const float Total=15.7f,CardAt=12.63f;
        public const float Strike=0f,Break=1.97f,Tear=2.97f,Presence=3.505f,PulseA=3.74f,PulseB=3.895f,Draft=5.035f,Condense=6.38f,Body=6.66f,
            Still=8.03f,Gather=10.5f,Inhale=11.82f,DipA=12.27f,DipB=12.5f,Drop=12.62f,FadeFrom=15.3f;
        /// <summary>The riser's five stutters: each pulls the new world a step nearer.</summary>
        public static readonly float[] Stutters={11.19f,11.31f,11.44f,11.565f,11.69f};
        /// <summary>The glitch stutters before the break (the song's high-band gates and onsets).</summary>
        public static readonly float[] Glitches={.06f,.3f,.58f,1.07f,1.43f,1.86f};
        public const string SongId="music/architect-obey",BankId="ChipArenaIntroDev",CardEffect="architect";
        /// <summary>The presence (and the hovering chest) height, and how high the architect floats until the drop.</summary>
        public const float PresenceY=1.5f,Hover=.45f;

        private static readonly Vector3 Cyan=NativeEntranceFx.Hex(0x8ff0ff),Ice=NativeEntranceFx.Hex(0xd8fbff),Gold=NativeEntranceFx.Hex(0xffd690),
            GlyphTint=NativeEntranceFx.Hex(0x5ff0ff),Rose=NativeEntranceFx.Hex(0xff4d7a),Blue=NativeEntranceFx.Hex(0x3f6dff),Neon=NativeEntranceFx.Hex(0xbfe8ff);
        private static readonly Vector3 Up=Vector3.UnitY;

        public NativeIntroArchitect():base(NativeIntroCatalog.ArchitectId,Total,CardAt,Drop,0x8ff0ff)
        {
            Start=Stance(.13f,.05f,.08f,-.14f,-.06f,-.36f);
            BuildKeys();BuildKit();BuildCamera();
        }
        /// <summary>The rig's camera, jolted by the strike and the drop (a short decaying shake; none in Reduced effects).</summary>
        public override void Camera(float age,out NativeEntranceView view)
        {
            base.Camera(age,out view);if(Context.Reduced)return;float t=Clock(Math.Min(age,SettleAt));
            float s=NativeEntranceMath.Pulse(t,Strike,.004f,.12f)*.035f+NativeEntranceMath.Pulse(t,Drop,.004f,.16f)*.05f+NativeEntranceMath.Pulse(t,Body,.004f,.1f)*.012f;if(s<1e-4f)return;
            var j=new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);view.Eye+=j;view.Look+=j*.6f;
        }
        public override string SetBank=>BankId;
        public override string Music=>SongId;
        public override NativeIntroCue[] Cues=>CueSheet;

        // =====================================================================================================================
        // The body: keyed on the rig (no pack clips), the drawn model follows it.
        // =====================================================================================================================
        private const float HoverY=.88f+Hover;
        private void BuildKeys()
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();float f=Facing;
            // Floating, head bowed, arms a little out, one knee drawn up, the feet pointed: the arrival.
            float[] form=NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.RootY]=HoverY;v[NativeEntranceKeys.Yaw]=f;v[NativeEntranceKeys.PlantL]=0;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,Hd,.48f,0,0);NativeEntranceKeys.Set(v,C,.1f,0,0);NativeEntranceKeys.Set(v,Sp,.04f,0,0);
                NativeEntranceKeys.Set(v,LA,-.06f,0,.3f);NativeEntranceKeys.Set(v,RA,-.06f,0,-.3f);NativeEntranceKeys.Set(v,LE,-.28f,0,0);NativeEntranceKeys.Set(v,RE,-.24f,0,0);
                NativeEntranceKeys.Set(v,LL,-.2f,0,.05f);NativeEntranceKeys.Set(v,LK,.42f,0,0);NativeEntranceKeys.Set(v,RL,.04f,0,-.05f);NativeEntranceKeys.Set(v,RK,.14f,0,0);
                NativeEntranceKeys.Set(v,CapturedRig.LFoot,.5f,0,0);NativeEntranceKeys.Set(v,CapturedRig.RFoot,.42f,0,0);});
            k.Key(0,form);k.Key(Draft,form);
            k.Key(Body-.06f,NativeEntranceKeys.With(form,v=>NativeEntranceKeys.Set(v,Hd,.52f,0,0)));
            // The body completes: the chest lifts, the arms are thrown a little out by its own pressure wave.
            k.Key(Body,form).J(Hd,.3f).J(C,-.12f).J(Sp,-.04f).J(LA,-.22f,0,.58f).J(RA,-.22f,0,-.58f).J(LE,-.12f).J(RE,-.1f).ImpactKey();
            k.Key(Body+.32f,form).J(Hd,.34f).J(C,-.04f).J(LA,-.1f,0,.4f).J(RA,-.1f,0,-.4f).J(LE,-.22f).J(RE,-.2f);
            // The head rises to look ahead (by the stillness).
            k.Key(Still-.15f,form).J(Hd,.0f).J(C,-.06f).J(LA,-.1f,0,.36f).J(RA,-.1f,0,-.36f).J(LE,-.22f).J(RE,-.2f);
            k.Key(Still+.5f,form).J(Hd,-.02f).J(C,-.07f).J(LA,-.1f,0,.36f).J(RA,-.1f,0,-.36f).J(LE,-.22f).J(RE,-.2f);
            // The stillness: the arms open out wide and low, slowly, the chest open.
            k.Key(Gather-.05f,form).J(Hd,-.06f).J(C,-.12f).J(LA,-.42f,-.25f,1.05f).J(RA,-.42f,.25f,-1.05f).J(LE,-.32f).J(RE,-.3f);
            // The gathering: the arms come forward and up, a step on each stutter, the head lifting with them.
            float[] lift={-.7f,-1.1f,-1.5f,-1.9f,-2.3f,-2.65f};float[] spread={1.0f,.9f,.8f,.7f,.62f,.55f};float[] head={-.1f,-.14f,-.18f,-.22f,-.26f,-.3f};
            k.Key(Stutters[0],form).J(Hd,head[0]).J(C,-.16f).J(LA,lift[0],0,spread[0]).J(RA,lift[0],0,-spread[0]).J(LE,-.35f).J(RE,-.35f);
            for(int s=0;s<Stutters.Length;s++)
            {
                float at=Stutters[s]+.065f;
                k.Key(at,form).J(Hd,head[s+1]).J(C,-.16f-.03f*s).J(LA,lift[s+1],0,spread[s+1]).J(RA,lift[s+1],0,-spread[s+1]).J(LE,-.3f+.02f*s).J(RE,-.3f+.02f*s);
            }
            // The inhale: held high, arched; then the hands close and draw in toward the head.
            k.Key(DipA,form).J(Hd,-.32f).J(C,-.3f).J(Sp,-.06f).J(LA,-2.75f,0,.5f).J(RA,-2.75f,0,-.5f).J(LE,-.18f).J(RE,-.18f);
            k.Key(DipB+.06f,form).J(Hd,-.24f).J(C,-.28f).J(Sp,-.06f).J(LA,-2.9f,0,.32f).J(RA,-2.9f,0,-.32f).J(LE,-1.15f).J(RE,-1.15f);
            // THE DROP: the arms slam down and out, the body lands on the dais, knees taking it.
            float[] land=NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.RootY]=.88f;v[NativeEntranceKeys.Yaw]=f;v[NativeEntranceKeys.BodyY]=-.17f;
                NativeEntranceKeys.Set(v,B,.16f,0,0);NativeEntranceKeys.Set(v,Hd,.22f,0,0);NativeEntranceKeys.Set(v,C,.06f,0,0);
                NativeEntranceKeys.Set(v,LA,.25f,0,.8f);NativeEntranceKeys.Set(v,RA,.25f,0,-.8f);NativeEntranceKeys.Set(v,LE,-.08f,0,0);NativeEntranceKeys.Set(v,RE,-.08f,0,0);});
            k.Key(Drop,land).ImpactKey();
            k.Key(Drop+.3f,land).Drop(-.09f).J(B,.08f).J(Hd,.12f).J(LA,.1f,0,.62f).J(RA,.1f,0,-.62f);
            // Authority: tall, chest open, arms held a little from the sides, the chin level, looking down the lens.
            float[] hero=NativeEntranceKeys.With(NativeEntranceKeys.Hero(),v=>{v[NativeEntranceKeys.Yaw]=f;NativeEntranceKeys.Set(v,C,-.1f,-.06f,0);NativeEntranceKeys.Set(v,Hd,.06f,.05f,0);
                NativeEntranceKeys.Set(v,LA,-.08f,0,.42f);NativeEntranceKeys.Set(v,RA,-.08f,0,-.42f);NativeEntranceKeys.Set(v,LE,-.18f,0,0);NativeEntranceKeys.Set(v,RE,-.18f,0,0);});
            k.Key(Drop+.85f,hero);k.Key(Total,hero);
            // Floating legs stay keyed until the landing plants them.
            Keys=k;
        }
        /// <summary>The body is built from the core out as it condenses (6.38-6.66): the chest first, the hands and feet last.</summary>
        public override float Solid(float t,int bone)
        {
            if(t>=Body+.02f)return 1;if(t<Condense)return 0;
            float order;switch(bone){case CapturedRig.Chest:case CapturedRig.Spine:order=0;break;case CapturedRig.Body:case CapturedRig.Pelvis:case CapturedRig.Neck:case CapturedRig.LClav:case CapturedRig.RClav:order=.25f;break;
                case CapturedRig.Head:case CapturedRig.LArm:case CapturedRig.RArm:case CapturedRig.LHip:case CapturedRig.RHip:order=.5f;break;case CapturedRig.LElbow:case CapturedRig.RElbow:case CapturedRig.LKnee:case CapturedRig.RKnee:order=.72f;break;default:order=.9f;break;}
            return NativeEntranceMath.Smooth((t-Condense-order*(Body-Condense-.04f))/.05f);
        }
        /// <summary>The body's outline in light before it condenses (the draft), a slow shimmer (never faster than 3 Hz).</summary>
        public override bool Holo(float t,out Vector3 color,out float opacity,out Vector3 offset)
        {
            color=Ice*1.3f;offset=Vector3.Zero;float a=NativeEntranceMath.Ramp(t,Draft,Draft+.9f)*(1-NativeEntranceMath.Ramp(t,Body-.05f,Body+.12f));
            opacity=a*(.16f+.1f*NativeEntranceMath.Ramp(t,Condense-.4f,Condense))*(.85f+.15f*(float)Math.Sin(t*2.3f*6.2831853f*.5f));return a>.004f;
        }
        /// <summary>Two afterimages of the fall onto the dais at the drop.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float a=t-Drop+.06f;if(a<0||a>.42f||into.Length<2)return 0;float o=(1-a/.42f);
            into[0]=new NativeEntranceGhost{Lag=.035f,Opacity=.42f*o,Color=Ice*1.4f,Offset=Vector3.Zero};into[1]=new NativeEntranceGhost{Lag=.075f,Opacity=.26f*o,Color=Cyan*1.2f,Offset=Vector3.Zero};return 2;
        }
        protected override Vector3 Focus(float t,Vector3 chest)=>t<Draft?new Vector3(0,PresenceY,0):chest;
        // The visor and trims: dark until the body ignites, ice-cyan while it floats, gold-white with the name.
        private const string AccentDark="#05080c",AccentIce="#bff6ff",AccentFlash="#ffffff",AccentGold="#ffe2a0";
        public override string AccentAt(float t)=>t<Body-.02f?AccentDark:t<Body+.12f?AccentFlash:t<Drop?AccentIce:t<Drop+.1f?AccentFlash:AccentGold;

        // =====================================================================================================================
        // The camera: cut on the song's transients.
        // =====================================================================================================================
        private void BuildCamera()
        {
            Rig.Dolly(Strike,V(3.4f,1.05f,6.5f),V(2.7f,.95f,5.5f),V(0,1.0f,0),V(0,1.12f,0),54,50).Ease(NativeIntroEase.Out).Handheld(.6f)
               .Dolly(1.07f,V(-2.5f,.3f,2.7f),V(-1.75f,.26f,1.75f),V(-4.6f,.38f,-2.6f),V(-4.6f,.5f,-3.1f),40,38).Handheld(.35f)
               .Crane(Break,V(4.6f,2.1f,8.8f),V(7.2f,3.6f,14.5f),3.2f,V(0,.6f,0),V(0,2.1f,0),50,56).Ease(NativeIntroEase.InOut)
               .Orbit(Presence,Vector3.Zero,40,-38,7.6f,6.2f,1.2f,1.95f,V(0,PresenceY,0),V(0,PresenceY,0),40,36).Ease(NativeIntroEase.InOut)
               .Dolly(Draft,V(1.0f,1.8f,3.5f),V(.72f,1.72f,2.55f),V(0,PresenceY-.12f,0),V(0,PresenceY-.16f,0),35,33).Focus(0,0,.35f)
               .Dolly(Body,V(.78f,.55f,2.35f),V(.64f,.6f,2.05f),V(0,PresenceY+.12f,0),V(0,PresenceY+.08f,0),44,41).Handheld(.25f)
               .Crane(Still,V(11.0f,4.2f,17.0f),V(9.6f,4.4f,14.6f),1.4f,V(0,2.3f,0),V(0,2.9f,0),42,38).Ease(NativeIntroEase.InOut)
               .Dolly(Gather,V(2.7f,.42f,5.1f),V(1.85f,.5f,3.45f),V(0,3.3f,-1.1f),V(0,2.8f,-.5f),52,44).Ease(NativeIntroEase.In)
               .Dolly(Inhale,V(.36f,1.52f,1.36f),V(.33f,1.54f,1.24f),V(0,2.1f,0),V(0,2.14f,0),39,36)
               .Dolly(Drop,V(2.6f,.75f,8.6f),V(.38f,.72f,4.3f),V(0,3.3f,-.6f),V(0,2.25f,-.6f),58,45).Ease(NativeIntroEase.Out)
               .Bars(0,.85f).Bars(Drop,.55f).Black(0,0).Black(FadeFrom+.12f,1);
        }

        // =====================================================================================================================
        // The kit: the old arena that comes apart and the architect's world that is gathered (tools/stages/intro_dev_architect.py
        // models every piece in its own frame; these are its numbers).
        // =====================================================================================================================
        private const byte OldPlate=0,OldWedge=1,OldPylon=2,OldSky=3,Dais=4,Tile=5,Plinth=6,Obelisk=7,Crown=8,Spire=9;
        private struct Piece
        {
            public string Prop,Wire;public byte Kind;public Vector3 Rest,Centroid,Outward;public float Yaw,Lift,Size,Delay;public int Seed;
            public Vector3 From,Hover,Swirl;public Quaternion Tumble,Residual;
        }
        private const float PlateSize=4f,Ground=NativeEntranceStage.FloorY-.132f,PylonRing=7.5f,MonoRing=9.3f,CrownR=11.5f,CrownY=13.2f,GridY=NativeEntranceStage.FloorY-.42f;
        private static readonly float[] RingR0={1.44f,3.24f,5.24f},RingR1={3.2f,5.2f,7.4f};private static readonly int[] RingCount={8,12,16};
        private Piece[] old,neo,spires;
        private static readonly string[] TileProps={"tile-1","tile-2","tile-3"},TileWires={"tile-1.wire","tile-2.wire","tile-3.wire"},SkyProps={"old-skyline-0","old-skyline-1","old-skyline-2","old-skyline-3"},
            SkyWires={"old-skyline-0.wire","old-skyline-1.wire","old-skyline-2.wire","old-skyline-3.wire"},SpireProps={"spire-a","spire-b","spire-c"},ShardProps={"shard-a","shard-b","shard-c","shard-d"};
        private static Vector3 Polar(float r,float a,float y)=>new Vector3(r*(float)Math.Sin(a),y,r*(float)Math.Cos(a));
        private static float R(int seed,int i)=>NativeEntranceMath.Rand(seed,i);
        private static float S(int seed,int i)=>NativeEntranceMath.Signed(seed,i);
        private void BuildKit()
        {
            var o=new List<Piece>();
            for(int i=-2;i<2;i++)for(int j=-2;j<2;j++){var at=new Vector3(i*PlateSize+PlateSize*.5f,Ground,j*PlateSize+PlateSize*.5f);o.Add(new Piece{Prop="old-plate",Wire="old-plate.wire",Kind=OldPlate,Rest=at,Centroid=new Vector3(0,-.06f,0),Size=2.8f});}
            for(int k=0;k<6;k++)o.Add(new Piece{Prop="old-dais-wedge",Wire="old-dais-wedge.wire",Kind=OldWedge,Rest=new Vector3(0,NativeEntranceStage.FloorY,0),Yaw=k*1.0471976f,Centroid=new Vector3(0,-.066f,1.5f),Size=1.6f});
            for(int i=0;i<12;i++){float a=(i+.5f)/12*6.2831853f;o.Add(new Piece{Prop="old-pylon",Wire="old-pylon.wire",Kind=OldPylon,Rest=Polar(PylonRing,a,Ground),Yaw=a,Centroid=new Vector3(0,1.8f,0),Size=1.9f});}
            for(int k=0;k<4;k++){float a=(k*6+3)/24f*6.2831853f;o.Add(new Piece{Prop=SkyProps[k],Wire=SkyWires[k],Kind=OldSky,Rest=new Vector3(0,Ground,0),Centroid=Polar(25,a,6),Size=9});}
            old=o.ToArray();
            for(int i=0;i<old.Length;i++)
            {
                ref var p=ref old[i];var c=World(p.Rest,p.Yaw,p.Centroid);float d=new Vector2(c.X,c.Z).Length();
                p.Seed=7100+i*13;p.Lift=Break+.03f+Math.Min(d,14)*.03f+.05f*R(p.Seed,1);p.Outward=d>.01f?Vector3.Normalize(new Vector3(c.X,0,c.Z)):Vector3.UnitZ;
                p.Swirl=Vector3.Normalize(new Vector3(S(p.Seed,2),.4f,S(p.Seed,3)));
            }
            var n=new List<Piece>();
            n.Add(new Piece{Prop="dais",Wire="dais.wire",Kind=Dais,Rest=new Vector3(0,NativeEntranceStage.FloorY,0),Centroid=new Vector3(0,-.5f,0),Size=1.4f});
            for(int r=0;r<3;r++)for(int i=0;i<RingCount[r];i++){float a=(i+(r==1?.5f:0))/RingCount[r]*6.2831853f,rm=(RingR0[r]+RingR1[r])*.5f;n.Add(new Piece{Prop=TileProps[r],Wire=TileWires[r],Kind=Tile,Rest=Polar(rm,a,NativeEntranceStage.FloorY),Yaw=a,Centroid=new Vector3(0,-.2f,0),Size=1+r*.3f});}
            for(int i=0;i<8;i++){float a=(i+.5f)/8*6.2831853f;var at=Polar(MonoRing,a,NativeEntranceStage.FloorY+.6f);n.Add(new Piece{Prop="mono-plinth",Wire="mono-plinth.wire",Kind=Plinth,Rest=at,Yaw=a,Centroid=new Vector3(0,-1.1f,0),Size=2});n.Add(new Piece{Prop="mono-obelisk",Wire="mono-obelisk.wire",Kind=Obelisk,Rest=at,Yaw=a,Centroid=new Vector3(0,5.4f,0),Size=6});}
            for(int i=0;i<8;i++){float a=(i+.5f)/8*6.2831853f;n.Add(new Piece{Prop="crown-arc",Wire="crown-arc.wire",Kind=Crown,Rest=Polar(CrownR,a,CrownY),Yaw=a,Centroid=Vector3.Zero,Size=4.5f});}
            neo=n.ToArray();
            for(int i=0;i<neo.Length;i++)
            {
                ref var p=ref neo[i];p.Seed=8300+i*17;var c=World(p.Rest,p.Yaw,p.Centroid);float d=new Vector2(c.X,c.Z).Length();p.Outward=d>.01f?Vector3.Normalize(new Vector3(c.X,0,c.Z)):Vector3.UnitZ;
                var side=new Vector3(-p.Outward.Z,0,p.Outward.X);float far=30+14*R(p.Seed,1);
                switch(p.Kind)
                {
                    // the floor rises out of the abyss under it; the monoliths fly in from the horizon; the crown falls from the sky
                    case Dais:case Tile:p.From=c+p.Outward*far*.55f+side*(S(p.Seed,2)*12)+new Vector3(0,-14-10*R(p.Seed,3),0);p.Hover=p.Rest+new Vector3(0,-.85f-.35f*R(p.Seed,4),0)+p.Outward*.35f;p.Delay=.05f+.25f*R(p.Seed,5)+d*.012f;break;
                    case Plinth:case Obelisk:p.From=c+p.Outward*far+side*(S(p.Seed,2)*10)+new Vector3(0,-4+12*R(p.Seed,3),0);p.Hover=p.Rest+p.Outward*1.7f+new Vector3(0,p.Kind==Plinth?-.9f:1.1f,0);p.Delay=(p.Kind==Plinth?.12f:0)+.05f*R(p.Seed,5);break;
                    default:p.From=c+p.Outward*far*.5f+new Vector3(0,26+10*R(p.Seed,3),0);p.Hover=p.Rest+new Vector3(0,2.4f,0)+p.Outward*1.4f;p.Delay=.18f+.08f*R(p.Seed,5);break;
                }
                p.Swirl=side*(S(p.Seed,6)>0?1:-1)*(6+6*R(p.Seed,7));
                p.Tumble=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(S(p.Seed,8),S(p.Seed,9)*.5f,S(p.Seed,10))),2.2f+2.4f*R(p.Seed,11));
                p.Residual=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(S(p.Seed,12),S(p.Seed,13),S(p.Seed,14))),.09f+.06f*R(p.Seed,15));
            }
            spires=new Piece[6];
            for(int i=0;i<6;i++){float a=(i+.3f*R(5,i))/6*6.2831853f+.4f,r=48+22*R(6,i);spires[i]=new Piece{Prop=SpireProps[i%3],Kind=Spire,Rest=Polar(r,a,-26+12*R(7,i)),Yaw=a,Seed=9100+i,Delay=.2f*R(8,i),Size=30};}
        }
        private static Vector3 World(Vector3 rest,float yaw,Vector3 local)=>rest+NativeEntranceMath.RotateY(local,yaw);
        private static Quaternion YawQ(float a)=>Quaternion.CreateFromAxisAngle(Up,a);

        // ---- the old arena's pieces ----
        /// <summary>Old piece `i` at `t`: its pose, its solid's scale (1 whole .. 0 gone, about its centroid) and its wire's level.</summary>
        private void OldPose(int i,float t,out Vector3 at,out Quaternion rot,out float solid,out float wire,out Vector3 centre)
        {
            ref var p=ref old[i];var q0=YawQ(p.Yaw);at=p.Rest;rot=q0;solid=1;wire=0;
            // The tremor before the break and the glitch tears (pieces thrown sideways in a band, for a few frames).
            if(t<p.Lift)
            {
                float shake=NativeEntranceMath.Ramp(t,.35f,Break)*.012f;if(!Context.Reduced&&shake>0)at+=new Vector3((float)Math.Sin(t*57+p.Seed)*shake,(float)Math.Abs(Math.Sin(t*43+p.Seed*.7f))*shake*.6f,(float)Math.Sin(t*61+p.Seed*1.3f)*shake);
                at+=TearShift(t,World(p.Rest,p.Yaw,p.Centroid).Y)*(p.Kind==OldSky?3f:1f);
                wire=.25f*TearLevel(t);
            }
            else
            {
                float tau=t-p.Lift,rise=NativeEntranceMath.EaseOut(tau/3.2f);float up=(p.Kind==OldSky?3.5f:.7f)+(p.Kind==OldSky?4:2.4f)*R(p.Seed,4);
                at+=new Vector3(0,up*rise,0)+p.Outward*((.25f+.5f*R(p.Seed,5))*tau)+new Vector3(0,.18f*tau*tau,0);
                float spin=(p.Kind==OldSky?.08f:.35f+.6f*R(p.Seed,6))*tau*(1+.35f*tau);var turn=Quaternion.CreateFromAxisAngle(p.Swirl,spin);
                // about its centroid: the turn moves the frame round the centre of the piece
                var c=NativeEntranceMath.RotateY(p.Centroid,p.Yaw);at+=c-Vector3.Transform(c,turn);rot=turn*q0;
                solid=1-NativeEntranceMath.Smooth((tau-.28f)/.7f);
                wire=(1-solid)*(1-NativeEntranceMath.Smooth((tau-.8f)/.5f))*1.6f+.35f*(1-NativeEntranceMath.Smooth(tau/.4f));
            }
            centre=at+Vector3.Transform(p.Centroid,rot);
        }
        // Tears: a band of the world (centred `TearY`) thrown sideways for a moment on each glitch and on the break's tear.
        private float TearLevel(float t)
        {
            float v=0;for(int i=0;i<Glitches.Length;i++)v=Math.Max(v,Flash(t,Glitches[i],.005f,.05f));v=Math.Max(v,Flash(t,Tear,.005f,.09f));return v;
        }
        private static float TearY(float t)=>t<Break?.4f+1.6f*NativeEntranceMath.Rand(731,(int)(t*9)):1.3f;
        private Vector3 TearShift(float t,float y)
        {
            float lvl=TearLevel(t);if(lvl<.01f)return Vector3.Zero;float band=Math.Max(0,1-Math.Abs(y-TearY(t))/.9f);if(band<=0)return Vector3.Zero;
            float dir=NativeEntranceMath.Rand(733,(int)(t*9))>.5f?1:-1;return new Vector3(dir*.22f*lvl*band*(Context.Reduced?.4f:1),0,0);
        }

        // ---- the architect's pieces ----
        private static readonly float[] Marks={.52f,.64f,.76f,.88f,1f};
        private static float Step(float t,float at,float span)=>NativeEntranceMath.EaseOut((t-at)/span);
        /// <summary>How far a new piece has come from the void to its hover point (0..1): a quickening approach, then a step on each stutter.</summary>
        private static float Approach(float t,float appear)
        {
            if(t<appear)return 0;float u=.38f*NativeEntranceMath.EaseIn(Math.Min(1,(t-appear)/Math.Max(.2f,Stutters[0]-appear)))*1;
            if(t<Stutters[0])return u;u=.38f;
            for(int s=0;s<Stutters.Length;s++){if(t<Stutters[s])break;float from=s==0?.38f:Marks[s-1];u=from+(Marks[s]-from)*Step(t,Stutters[s],.075f);}
            return u;
        }
        /// <summary>New piece `i` at `t`: shown (false before it appears), its pose, the solid shown (after the drop) and the wire's level.</summary>
        private bool NewPose(int i,float t,out Vector3 at,out Quaternion rot,out bool solid,out float wire,out float flash)
        {
            ref var p=ref neo[i];var rest=YawQ(p.Yaw);float appear=Gather+p.Delay;at=p.Rest;rot=rest;solid=false;wire=0;flash=0;
            if(t<appear)return false;
            if(t>=Drop)
            {
                // Locked: a hair of settle, the seams' flash travelling out with the wave of light.
                float s=t-Drop;at=p.Rest-new Vector3(0,.012f*(float)Math.Exp(-14*s)*(float)Math.Sin(s*30),0);solid=true;
                float reach=Math.Max(0,(new Vector2(p.Rest.X,p.Rest.Z).Length()-.5f))/22f;flash=Flash(t,Drop+reach,.01f,.3f)*(p.Kind==Crown?.6f:1);
                wire=1.8f*(1-NativeEntranceMath.Smooth(s/(p.Kind==Crown||p.Kind==Tile||p.Kind==Dais?.14f:.38f)));return true;
            }
            float snapFrom=Drop-.045f;
            if(t>=snapFrom)
            {
                // The snap: from the hover point into the slot, accelerating, arriving exactly on the drop.
                float u=NativeEntranceMath.EaseIn((t-snapFrom)/(Drop-snapFrom));at=Vector3.Lerp(p.Hover,p.Rest,u);rot=Quaternion.Slerp(p.Residual*rest,rest,u);wire=2.6f;return true;
            }
            float a=Approach(t,appear);
            // a curved flight: in along the swirl, nearing the hover point
            var mid=(p.From+p.Hover)*.5f+p.Swirl;float b=a;var pos=(1-b)*(1-b)*p.From+2*(1-b)*b*mid+b*b*p.Hover;
            // the hold: breathing; the dips: a tremble (small and slow in Reduced effects)
            if(t>=Stutters[4])
            {
                float tr=NativeEntranceMath.Bump(t,DipA-.05f,DipB+.08f);float amp=Context.Reduced?.004f:.016f,rate=Context.Reduced?2.4f:23;
                pos+=new Vector3(0,.01f*(float)Math.Sin(t*2.1f+p.Seed),0)+new Vector3((float)Math.Sin(t*rate+p.Seed),(float)Math.Sin(t*rate*1.13f+p.Seed*2),(float)Math.Sin(t*rate*.91f+p.Seed*3))*(amp*tr);
            }
            at=pos;rot=Quaternion.Slerp(p.Tumble*rest,p.Residual*rest,NativeEntranceMath.Smooth(a));
            float pulse=0;for(int s=0;s<Stutters.Length;s++)pulse=Math.Max(pulse,Flash(t,Stutters[s],.01f,.09f));
            float inhale=1-.55f*NativeEntranceMath.Bump(t,Inhale-.04f,DipA+.1f);
            wire=(.35f+1.0f*NativeEntranceMath.Ramp(t,appear,appear+.4f)+1.2f*pulse)*inhale*(.6f+.4f*a);return true;
        }
        private Vector3 SpireAt(int i,float t)
        {
            ref var p=ref spires[i];float u=NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-Gather-p.Delay)/(Drop-Gather-p.Delay+.4f)));return p.Rest-new Vector3(0,46*(1-u),0);
        }

        // ---- the set ----
        private const string GridProp="grid";
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("dais")||!set.Has("old-plate")||!set.Has("mono-obelisk"))return false;
            bool low=Context.Low;
            // The old arena until it is gone.
            if(t<Gather)
            {
                for(int i=0;i<old.Length;i++)
                {
                    OldPose(i,t,out var at,out var rot,out float solid,out float wire,out _);ref var p=ref old[i];
                    if(solid>.003f)
                    {
                        // the solid falls in on its own centroid, inside its wire
                        var c=Vector3.Transform(p.Centroid,rot);float neon=OldNeon(t,i);
                        set.Piece(p.Prop,at+c*(1-solid),rot,solid,Vector3.One,new Vector3(neon,neon,neon)*(1+1.5f*(1-solid)));
                    }
                    if(wire>.01f&&!(low&&p.Kind==OldPlate))set.Piece(p.Wire,at,rot,1,Vector3.One,Ice*wire);
                }
            }
            // The old world's shards orbit the presence, are thrown back by the body, wait, and are drawn in by the gathering.
            if(t>=Break+.8f&&t<Drop)for(int i=0;i<12;i++){if(Shard(i,t,out var at,out var rot,out float sc))set.Piece(ShardProps[i&3],at,rot,sc,new Vector3(.8f),Cyan*(.8f+1.4f*Flash(t,Body,.01f,.3f)));}
            // The void's grid: it shows as the arena lets go, breathes through the stillness and dims under the new floor.
            float grid=NativeEntranceMath.Ramp(t,Break,Break+1.1f)*(1-.55f*NativeEntranceMath.Ramp(t,Drop,Drop+.6f))*(.85f+.25f*NativeEntranceMath.Bump(t,Still,Gather))*(1+.8f*Flash(t,Presence,.01f,.4f));
            if(grid>.01f)set.Piece(GridProp,new Vector3(0,GridY,0),Quaternion.Identity,1,Vector3.One,new Vector3(grid,grid,grid));
            // The architect's world: wireframes in flight, solid from the drop.
            for(int i=0;i<neo.Length;i++)
            {
                if(!NewPose(i,t,out var at,out var rot,out bool solid,out float wire,out float flash))continue;ref var p=ref neo[i];
                if(p.Kind==Crown&&t>=Drop){var turn=YawQ(CrownTurn(t));at=Vector3.Transform(at,turn);rot=turn*rot;}
                // (Low keeps the wireframe flight for the obelisks only: the rest fly as dark stone with their seams ablaze)
                bool wired=!low||p.Kind==Obelisk;
                if(solid)set.Piece(p.Prop,at,rot,1,Vector3.One,Settled(t,p.Kind)+Ice*(5*flash));
                else if(!wired)set.Piece(p.Prop,at,rot,1,new Vector3(.45f),Ice*(1.6f*wire));
                if(wire>.01f&&wired)set.Piece(p.Wire,at,rot,1,Vector3.One,Ice*wire);
            }
            if(t>=Gather)for(int i=0;i<spires.Length;i++){ref var p=ref spires[i];float g=.4f+.9f*NativeEntranceMath.Ramp(t,Drop-.3f,Drop+.4f)+2*Flash(t,Drop+.25f,.02f,.5f);set.Piece(p.Prop,SpireAt(i,t),YawQ(p.Yaw),1,Vector3.One,new Vector3(g,g,g));}
            return true;
        }
        /// <summary>The old pylons' neon (and the stage's lit grid) through the strike: full, stuttering with the glitches, out at the break.</summary>
        private float OldNeon(float t,int i)
        {
            float on=.7f+.5f*Flash(t,Strike,.005f,.5f);float cut=0;for(int k=0;k<Glitches.Length;k++)cut=Math.Max(cut,Flash(t,Glitches[k],.005f,.06f));
            return on*(1-.75f*cut)*(1-NativeEntranceMath.Ramp(t,Break-.05f,Break+.25f))+.15f;
        }
        /// <summary>The new pieces' glow once locked: the seams steady, the crown and the capstones brighter.</summary>
        private static Vector3 Settled(float t,byte kind)
        {
            float b=kind==Crown?.55f:kind==Obelisk?1.1f:1;float sweep=.12f*NativeEntranceMath.Bump(t,13.8f,15.0f);return new Vector3(b+sweep,b+sweep,b+sweep);
        }
        private static float CrownTurn(float t)=>.11f*NativeEntranceMath.EaseOut((t-Drop)/2.5f)+.02f*Math.Max(0,t-Drop-2.5f);
        /// <summary>Shard `i`: born from the old world near the break, orbiting the presence, thrown back by the body at 6.66, hanging
        /// still through the stillness, drawn in to the new floor's rim by the gathering (gone at the drop).</summary>
        private bool Shard(int i,float t,out Vector3 at,out Quaternion rot,out float scale)
        {
            int seed=9500+i*7;float born=Break+.8f+.5f*R(seed,1);at=Vector3.Zero;rot=Quaternion.Identity;scale=0;if(t<born)return false;
            float r0=3.2f+3.4f*R(seed,2),h=.6f+3.6f*R(seed,3),a0=R(seed,4)*6.2831853f,w=(.22f+.2f*R(seed,5))*(i%2==0?1:-1);
            // time runs for them until the stillness, then barely
            float run=Math.Min(t,Still)-born+.04f*Math.Max(0,t-Still);
            float push=NativeEntranceMath.EaseOut((t-Body)/.9f)*(t>Body?1:0)*(4+3*R(seed,6));float r=r0+push;
            var pos=new Vector3((float)Math.Sin(a0+w*run)*r,h+.25f*(float)Math.Sin(run*.7f+i),(float)Math.Cos(a0+w*run)*r);
            float draw=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((t-Gather)/(Drop-.1f-Gather)));
            var rim=Polar(7.6f,a0+w*run,NativeEntranceStage.FloorY-.3f);pos=Vector3.Lerp(pos,rim,draw);
            at=pos;rot=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(S(seed,7),.6f,S(seed,8))),run*(.8f+R(seed,9)));
            scale=(.35f+.55f*R(seed,10))*NativeEntranceMath.Ramp(t,born,born+.5f)*(1-NativeEntranceMath.Smooth((draw-.8f)/.2f));return scale>.01f;
        }
        protected override NativeIntroVoidLook VoidLook(float t)
        {
            var look=NativeIntroVoidLook.Of(Cyan);look.Far=NativeEntranceFx.Hex(0x03050b);look.Floor=NativeEntranceFx.Hex(0x080b12);look.Pool=t<Drop?Cyan:Gold;look.Rim=Cyan;look.Key=t<Break?1:.7f;look.PoolLevel=t<Draft?.3f:1;return look;
        }

        // =====================================================================================================================
        // Light
        // =====================================================================================================================
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();var chest=Anchor(NativeEntranceAnchor.Chest,t);l.DriveProbe=true;
            float arena=1-NativeEntranceMath.Ramp(t,Break,Break+.9f),drop=NativeEntranceMath.Ramp(t,Drop-.02f,Drop+.15f);
            l.Background=Vector3.Lerp(NativeEntranceFx.Hex(0x02040a),NativeEntranceFx.Hex(0x05070f),arena)+NativeEntranceFx.Hex(0x0a1220)*drop;l.Fog=l.Background;l.FogNear=t<Break?10:18;l.FogFar=t<Break?40:150;
            l.Sky=NativeEntranceFx.Hex(0x6f8fbf,.18f+.2f*arena+.1f*drop);l.Ground=NativeEntranceFx.Hex(0x0f1a26,.25f+.05f*drop);
            float strike=Flash(t,Strike,.004f,.35f);
            l.Directional(new Vector3(2.5f,5,4),NativeEntranceFx.Hex(0xdfe8ff,.55f*arena+.2f*NativeEntranceMath.Ramp(t,Body,Body+.5f)*(1-drop)+.42f*drop));
            if(drop>0)l.Directional(new Vector3(-1,6,-2),Gold*(.3f*drop));
            // The strike: white at the centre, decaying to the thread of light that holds until the break.
            if(t<Break+.3f)l.Add(new Vector3(0,1.6f,0),Ice*(36*strike+2.5f*(1-NativeEntranceMath.Ramp(t,Break,Break+.3f))),14);
            // The old pylons' neon on the floor (two nearest the lens), out at the break.
            if(arena>.01f){float n=OldNeon(t,0)*arena;l.Add(new Vector3(3.6f,2,6.4f),Neon*(2.2f*n),7);l.Add(new Vector3(-6.4f,2,3.6f),Neon*(1.8f*n),7);}
            // The void's cold light from the grid below.
            float voidL=NativeEntranceMath.Ramp(t,Break,Break+1.2f)*(1-.5f*drop);if(voidL>.01f)l.Add(new Vector3(0,-1.2f,0),Cyan*(1.4f*voidL),12);
            // The presence: a point of light, pulsing on the sub's hits, becoming the chest's glow.
            if(t>=Presence-.05f&&t<Drop)
            {
                float pres=NativeEntranceMath.Ramp(t,Presence-.02f,Presence+.1f)*(1+1.2f*Flash(t,PulseA,.01f,.15f)+1.2f*Flash(t,PulseB,.01f,.15f)+2.5f*Flash(t,Body,.01f,.3f))*(1-.5f*NativeEntranceMath.Ramp(t,Body,Body+.6f));
                l.Add(t<Body?new Vector3(0,PresenceY,0):chest+new Vector3(0,0,.3f),Ice*(4.5f*pres),6);
            }
            // The body's rim, cyan, from behind.
            if(t>=Body-.1f)l.Add(chest+new Vector3(-.3f,.8f,-1.7f),Cyan*(5.5f*NativeEntranceMath.Ramp(t,Body-.1f,Body+.2f)*(1-.3f*drop)),7);
            // The gathering's wires lighting the void; the inhale dims everything.
            if(t>=Gather&&t<Drop+.1f){float g=NativeEntranceMath.Ramp(t,Gather,Inhale)*(1-.6f*NativeEntranceMath.Bump(t,Inhale-.05f,DipB+.1f));l.Add(new Vector3(0,3,0),Cyan*(3.2f*g),16);}
            // THE DROP: the flash, then the crown's gold from above, the seams' cyan from below, a white rim and the name's light.
            float hit=Flash(t,Drop,.004f,.32f);if(hit>.01f)l.Add(new Vector3(0,1.2f,.6f),Ice*(42*hit),18);
            if(drop>0)
            {
                l.Add(new Vector3(0,CrownY-1.5f,0),Gold*(3.2f*drop),24);l.Add(chest+new Vector3(.5f,.35f,1.25f),Ice*(1.5f*drop),3.2f);l.Add(new Vector3(.6f,2.6f,-2.3f),Ice*(4.2f*drop),7);
                float name=NameLevel(t);if(name>.01f)l.Add(NameCentre+new Vector3(0,0,.6f),Gold*(3.4f*name),9);
            }
        }

        // =====================================================================================================================
        // Effects
        // =====================================================================================================================
        private NativeEntranceView view;
        /// <summary>The camera this frame (the runtime's own: the rig at the presentation clock, held after SettleAt), for laying the
        /// long streaks onto their heads' camera planes.</summary>
        private void ViewAt(float t)=>Camera(t,out view);
        /// <summary>A line of light from `tail` to `head` (its head the bright end), laid onto the head's camera plane when over 1 m.</summary>
        private void Line(INativeEntranceFx fx,Vector3 head,Vector3 tail,Vector3 color,float width,float taper,int seed)
        {
            var axis=head-tail;float len=axis.Length();if(len<1e-4f)return;
            if(len>NativeCacheOpenFx.ExactStreak)
            {
                var fw=view.Look-view.Eye;if(fw.LengthSquared()>1e-8f)fw=Vector3.Normalize(fw);float ax=axis.X,ay=axis.Y,az=axis.Z;
                if(NativeCacheOpenFx.OntoHeadPlane(view.Eye.X,view.Eye.Y,view.Eye.Z,fw.X,fw.Y,fw.Z,.2f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref taper))axis=new Vector3(ax,ay,az);else return;
            }
            fx.Streak(head,axis,color,width,.001f,1,taper,0,seed);
        }
        private Vector3 CamRight{get{var f=view.Look-view.Eye;if(f.LengthSquared()<1e-8f)return Vector3.UnitX;var r=Vector3.Cross(Vector3.Normalize(f),Up);return r.LengthSquared()<1e-8f?Vector3.UnitX:Vector3.Normalize(r);}}
        private Vector3 CamUp(Vector3 right){var f=view.Look-view.Eye;if(f.LengthSquared()<1e-8f)return Up;return Vector3.Normalize(Vector3.Cross(right,Vector3.Normalize(f)));}

        // ---- code glyphs: tiny 3 x 5 characters drawn as two or three strokes, facing the camera ----
        // (an original set; points in a 2 x 4 cell, stroke pairs)
        private static readonly float[][] Glyph=
        {
            new[]{1f,0,1,4},new[]{0f,4,2,4,2,4,.6f,0},new[]{1f,1,1,3,0,2,2,2},new[]{0f,1.4f,2,1.4f,0,2.6f,2,2.6f},new[]{0f,4,0,0,0,0,2,0},new[]{0f,4,2,4,1,4,1,0},
            new[]{2f,4,0,2,0,2,2,0},new[]{0f,4,2,2,2,2,0,0},new[]{0f,0,2,4},new[]{0f,4,2,0},new[]{0f,0,0,4,0,4,2,4,2,4,2,0},new[]{0f,0,0,4,2,0,2,4,0,2,2,2},
            new[]{0f,0,0,4,0,4,2,0,2,0,2,4},new[]{0f,4,2,4,2,4,0,0,0,0,2,0},new[]{1.6f,0,1.6f,4,1.6f,4,0,1.4f,0,1.4f,2,1.4f},new[]{0f,4,2,4,.4f,2,1.6f,2,0,0,2,0},
        };
        private void DrawGlyph(INativeEntranceFx fx,int g,Vector3 at,float size,Vector3 color,Vector3 right,Vector3 up,int seed)
        {
            var s=Glyph[g&15];float k=size*.25f;var o=at-right*k-up*(k*2);float w=size*.11f;
            for(int i=0;i+3<s.Length;i+=4){var a=o+right*(s[i]*k)+up*(s[i+1]*k);var b=o+right*(s[i+2]*k)+up*(s[i+3]*k);fx.Streak(b,b-a,color,w,.001f,1,.15f,0,seed+i);}
        }

        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            ViewAt(t);float floor=NativeEntranceStage.FloorY;var right=CamRight;var up=CamUp(right);bool low=Context.Low;float thin=low?.42f:1;
            StrikeFx(fx,t,q,thin);
            if(t<Break+.5f)Cracks(fx,t,thin);
            TearFx(fx,t);
            if(t>=Break&&t<Gather+.5f)Dissolve(fx,t,thin,right,up);
            if(t>=Break+.4f&&t<Drop+.3f)Field(fx,t,thin,right,up);
            if(t>=Presence-.02f&&t<Body+.5f)PresenceFx(fx,t,thin,right,up);
            if(t>=Draft&&t<Body+.4f)DraftFx(fx,t,thin);
            if(t>=Still&&t<Drop+.15f)Blueprint(fx,t,thin);
            if(t>=Gather&&t<Drop+.1f)Trails(fx,t,thin);
            if(t>=Drop-.02f)DropFx(fx,t,thin,floor);
            if(t>=CardAt)NameFx(fx,t,thin,right,up);
            if(t>=Body-.05f)Aura(fx,t,thin,right,up);
            if(t>=Drop)NativeIntroVoid.Motes(fx,t,q,Gold*.8f,4300);
        }
        // The architect's own light: glyph-light circling the body while it floats (it alone moves through the stillness), and
        // after the drop a soft gold glow at the back of the head and a cool one at the feet.
        private void Aura(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            var chest=Anchor(NativeEntranceAnchor.Chest,t);float on=NativeEntranceMath.Ramp(t,Body,Body+.6f)*(1-NativeEntranceMath.Ramp(t,Drop-.05f,Drop+.25f));
            if(on>.01f)
            {
                int n=(int)(12*thin+.5f);
                for(int i=0;i<n;i++)
                {
                    int seed=11000+i*7;float a=R(seed,1)*6.2831853f+t*(.55f+.3f*R(seed,2))*(i%2==0?1:-1),r=.55f+.35f*R(seed,3),h=-.95f+1.6f*R(seed,4)+.08f*(float)Math.Sin(t*1.3f+i);
                    var p=new Vector3(chest.X+(float)Math.Sin(a)*r,chest.Y+h,chest.Z+(float)Math.Cos(a)*r);float lvl=on*(.5f+.5f*(float)Math.Sin(t*1.7f+i*2.1f));
                    if(i%3==0)fx.Glow(p,Ice*(1.2f*lvl),0,.04f,0,0,seed);else DrawGlyph(fx,(int)(R(seed,5)*16),p,.07f,GlyphTint*(1.3f*lvl),right,up,seed);
                }
            }
            float after=NativeEntranceMath.Ramp(t,Drop+.05f,Drop+.6f)*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total));
            if(after>.01f)
            {
                var head=Anchor(NativeEntranceAnchor.Head,t);var f=ForwardOf(t);fx.Glow(head-f*.32f+new Vector3(0,.12f,0),Gold*(.55f*after),0,1.1f,0,0,11200);
                var feet=(Anchor(NativeEntranceAnchor.LeftFoot,t)+Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;fx.Glow(new Vector3(feet.X,NativeEntranceStage.FloorY+.05f,feet.Z),Cyan*(.5f*after),0,1.4f,0,0,11201);
            }
            else if(t>=Still&&t<Drop)fx.Glow(new Vector3(0,GridY+.03f,0),Cyan*(.35f*NativeEntranceMath.Ramp(t,Still,Still+1)),0,2.4f,0,0,11202);
        }
        // The strike: a column of light out of the dark onto the dais; sparks thrown over the deck; a thread of light until the break.
        private void StrikeFx(INativeEntranceFx fx,float t,float q,float thin)
        {
            if(t>=Break+.35f)return;float hit=Flash(t,Strike,.004f,.22f),hold=(1-NativeEntranceMath.Ramp(t,Break-.1f,Break+.3f));
            float width=.06f+.55f*hit;var top=new Vector3(0,42,0);var foot=new Vector3(0,NativeEntranceStage.FloorY+.02f,0);
            Line(fx,foot,top,Ice*(2.2f+6*hit)*hold,width,.2f,6001);Line(fx,foot,top,Cyan*(1.1f+2*hit)*hold,width*3.2f,.5f,6002);
            fx.Glow(foot+new Vector3(0,.15f,0),Ice*(3+9*hit)*hold,0,.8f+2.4f*hit,1,0,6003);
            if(t<.9f)NativeEntranceFx.Sparks(fx,t,Strike,foot+new Vector3(0,.05f,0),new Vector3(0,.35f,0),1.2f,(int)(46*thin),7.5f,.75f,9.8f,.6f,Ice*3,Cyan*1.5f,.016f,6010,Math.Max(q,.6f),NativeEntranceStage.FloorY);
        }
        // Cracks of light racing out over the deck from the strike, cooling; they go with the plates.
        private void Cracks(INativeEntranceFx fx,float t,float thin)
        {
            int lines=(int)(11*thin+.5f);float fade=1-NativeEntranceMath.Ramp(t,Break,Break+.4f);if(fade<=0)return;
            for(int l=0;l<lines;l++)
            {
                float ang=l/(float)lines*6.2831853f+S(6100,l)*.22f;var prev=new Vector3(0,NativeEntranceStage.FloorY+.004f,0);float born=.02f*l;
                for(int k=1;k<=12;k++)
                {
                    float reachAt=born+k*.085f;if(t<reachAt)break;ang+=S(6101+l,k)*.38f;float step=.55f+.2f*R(6102+l,k);
                    var p=prev+new Vector3((float)Math.Sin(ang)*step,0,(float)Math.Cos(ang)*step);float rr=new Vector2(p.X,p.Z).Length();p.Y=(rr<2.2f?NativeEntranceStage.FloorY:Ground)+.004f;
                    float heat=(1-NativeEntranceMath.Smooth((t-reachAt)/1.6f)*.65f)*fade*(1+1.5f*TearLevel(t));
                    Line(fx,p,prev,Vector3.Lerp(Ice*2.6f,Cyan*1.4f,NativeEntranceMath.Clamp01((t-reachAt)*2))*heat,.022f-.001f*k,0,6200+l*16+k);prev=p;
                }
            }
        }
        // The tears: two thin lines across the band, split in colour, for the glitch's frames.
        private void TearFx(INativeEntranceFx fx,float t)
        {
            float lvl=TearLevel(t);if(lvl<.02f)return;float y=TearY(t);var right=CamRight;var centre=view.Look+(new Vector3(0,y,0)-new Vector3(0,view.Look.Y,0));
            float span=7;var a=centre-right*span;var b=centre+right*span;float off=Context.Reduced?.01f:.035f;
            Line(fx,b+new Vector3(0,.9f,0),a+new Vector3(0,.9f,0),Ice*(1.6f*lvl),.012f,0,6400);Line(fx,b-new Vector3(0,.9f,0),a-new Vector3(0,.9f,0),Ice*(1.2f*lvl),.01f,0,6401);
            Line(fx,b+new Vector3(0,.9f+off,0)+right*.05f,a+new Vector3(0,.9f+off,0)+right*.05f,Rose*(1.1f*lvl),.01f,0,6402);Line(fx,b-new Vector3(0,.9f+off,0)-right*.05f,a-new Vector3(0,.9f+off,0)-right*.05f,Blue*(1.1f*lvl),.01f,0,6403);
        }
        // The dissolve: glyphs boiling off each piece as it falls in to its wire, rising and fading into light.
        private void Dissolve(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            for(int i=0;i<old.Length;i++)
            {
                ref var p=ref old[i];float tau=t-p.Lift;if(tau<.3f||tau>2.6f)continue;int n=(int)((p.Kind==OldSky?7:p.Kind==OldPlate?4:3)*thin+.5f);
                for(int g=0;g<n;g++)
                {
                    int seed=p.Seed*31+g;float born=p.Lift+.35f+.85f*R(seed,1);float a=t-born;if(a<0||a>1.25f)continue;
                    OldPose(i,born,out _,out _,out _,out _,out var c);
                    var at=c+new Vector3(S(seed,2),S(seed,3)*.6f,S(seed,4))*(p.Size*.5f)+new Vector3(0,.55f*a+.25f*a*a,0);float u=a/1.25f;
                    float size=(p.Kind==OldSky?.5f:.11f)*(1+.4f*R(seed,5));var col=Vector3.Lerp(Ice*2.2f,GlyphTint*1.2f,u)*(1-u*u);
                    if(u<.7f)DrawGlyph(fx,(int)(R(seed,6)*16+t*3),at,size,col,right,up,seed);else fx.Glow(at,col*1.5f,0,size*.6f,0,u,seed);
                }
            }
        }
        // The field: glyphs adrift round the centre; frozen through the stillness; swept to the new floor by the gathering.
        private void Field(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            int n=(int)(46*thin);float frozen=Math.Min(t,Still)+.03f*Math.Max(0,t-Still);float show=NativeEntranceMath.Ramp(t,Break+.4f,Break+1.4f)*(1-NativeEntranceMath.Smooth((t-Drop+.25f)/.25f));
            float gather=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((t-Gather)/(Drop-Gather)));
            for(int i=0;i<n;i++)
            {
                int seed=6600+i*11;float r=2.2f+6.5f*R(seed,1),a0=R(seed,2)*6.2831853f,h=.2f+4.8f*R(seed,3),w=(.05f+.1f*R(seed,4))*(i%2==0?1:-1);
                float a=a0+w*frozen;var pos=new Vector3((float)Math.Sin(a)*r,h+.12f*(float)Math.Sin(frozen*.8f+i),(float)Math.Cos(a)*r);
                var dest=Polar(1.5f+5.5f*R(seed,5),a,NativeEntranceStage.FloorY+.05f);pos=Vector3.Lerp(pos,dest,gather);
                float lvl=show*(.55f+.45f*(float)Math.Sin(i*1.7f+frozen*1.3f))*(1+.8f*Flash(t,Presence,.01f,.4f));if(lvl<.03f)continue;
                DrawGlyph(fx,(int)(R(seed,6)*16+frozen*.7f),pos,.1f+.06f*R(seed,7),GlyphTint*(1.1f*lvl),right,up,seed);
            }
        }
        // The presence: a point of light, its halo and flare, glyphs spiralling into it, pulses on the sub's hits.
        private void PresenceFx(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            var c=new Vector3(0,PresenceY,0);float on=NativeEntranceMath.Ramp(t,Presence-.02f,Presence+.08f)*(1-NativeEntranceMath.Ramp(t,Body+.05f,Body+.45f));if(on<=0)return;
            float pulse=1+1.4f*Flash(t,PulseA,.01f,.14f)+1.4f*Flash(t,PulseB,.01f,.14f)+2.2f*Flash(t,Presence,.005f,.3f)+3*Flash(t,Body,.005f,.25f);float grow=1+.6f*NativeEntranceMath.Ramp(t,Draft,Body);
            fx.Glow(c,Vector3.One*(3.2f*on*pulse),0,.14f*grow,1,0,6800);fx.Glow(c,Ice*(1.4f*on*pulse),0,.55f*grow,1,0,6801);fx.Glow(c,Cyan*(.45f*on*pulse),0,1.8f*grow,0,0,6802);
            Line(fx,c+right*(.55f*grow*pulse),c-right*(.55f*grow*pulse),Ice*(2.2f*on),.012f,.9f,6803);Line(fx,c-right*(.55f*grow*pulse),c+right*(.55f*grow*pulse),Ice*(2.2f*on),.012f,.9f,6804);
            Line(fx,c+up*(.32f*grow*pulse),c-up*(.32f*grow*pulse),Ice*(1.5f*on),.01f,.9f,6805);
            // the streams: glyphs on spirals into the point
            int n=(int)(28*thin);
            for(int i=0;i<n;i++)
            {
                int seed=6900+i*13;float period=1.6f+.8f*R(seed,1);float u=((t-Presence)/period+R(seed,2))%1f;if(u<0)u+=1;float a=R(seed,3)*6.2831853f+u*5*(i%2==0?1:-1);
                float rr=(2.8f+1.6f*R(seed,4))*(1-u)*(1-u);var pos=c+new Vector3((float)Math.Sin(a)*rr,(S(seed,5)*1.6f)*(1-u),(float)Math.Cos(a)*rr);
                var col=Vector3.Lerp(GlyphTint,Ice*2,u)*(on*NativeEntranceMath.Smooth(u*4)*(1-u*u));
                if(u<.75f)DrawGlyph(fx,(int)(R(seed,6)*16),pos,.09f*(1-u*.5f),col,right,up,seed);else fx.Glow(pos,col*2,0,.05f,0,u,seed);
            }
            // the bursts on the hits
            if(t<PulseB+.8f){NativeEntranceFx.Sparks(fx,t,Presence,c,Vector3.Zero,1,(int)(30*thin),3.2f,.7f,0,1.6f,Ice*2.4f,GlyphTint,.012f,7000,1,-10);NativeEntranceFx.Sparks(fx,t,PulseA,c,Vector3.Zero,1,(int)(16*thin),2.2f,.5f,0,1.8f,Ice*2,GlyphTint,.01f,7040,1,-10);}
        }
        // The draft: the body traced in lines of light along its bones, pens running out from the chest; the condensing glyphs.
        private static readonly NativeEntranceAnchor[] BoneFrom={NativeEntranceAnchor.Chest,NativeEntranceAnchor.Chest,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.Chest,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.Chest,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.RightKnee};
        private static readonly NativeEntranceAnchor[] BoneTo={NativeEntranceAnchor.Head,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.LeftHand,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.RightHand,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.LeftFoot,NativeEntranceAnchor.RightKnee,NativeEntranceAnchor.RightFoot};
        private static readonly float[] BoneOrder={0,0,1,2,0,1,2,0,1,2,1,2};
        private void DraftFx(INativeEntranceFx fx,float t,float thin)
        {
            float fade=1-NativeEntranceMath.Ramp(t,Body,Body+.35f);
            for(int b=0;b<BoneFrom.Length;b++)
            {
                float start=Draft+.15f+BoneOrder[b]*.32f;float u=NativeEntranceMath.EaseOut((t-start)/.45f);if(u<=0)continue;
                var a=Anchor(BoneFrom[b],t);var e=Anchor(BoneTo[b],t);var head=Vector3.Lerp(a,e,u);float shimmer=.8f+.2f*(float)Math.Sin(t*5+b);
                Line(fx,head,a,Ice*(1.7f*shimmer*fade),.012f,.3f,7100+b);if(u<1)fx.Glow(head,Vector3.One*(2.5f*fade),0,.05f,1,0,7120+b);
            }
            // the condensing: glyph-light falling in to every part of the body from all round
            if(t>=Condense-.35f&&t<Body+.1f)
            {
                int n=(int)(40*thin);
                for(int i=0;i<n;i++)
                {
                    int seed=7200+i*5;float born=Condense-.35f+.55f*R(seed,1);float u=(t-born)/.32f;if(u<0||u>1)continue;var target=Anchor(BoneTo[i%BoneTo.Length],t);
                    var from=target+NativeEntranceMath.Direction(seed,2)*(1.4f+1.2f*R(seed,3));var p=Vector3.Lerp(from,target,NativeEntranceMath.EaseIn(u));
                    Line(fx,p,Vector3.Lerp(from,target,NativeEntranceMath.EaseIn(Math.Max(0,u-.15f))),Vector3.Lerp(GlyphTint,Ice*2.4f,u),.012f,.6f,seed);
                }
            }
            // the body's pressure wave: a shell of light thrown out from the chest
            NativeEntranceFx.Sparks(fx,t,Body,Anchor(NativeEntranceAnchor.Chest,Body),Vector3.Zero,1,(int)(44*thin),6.5f,.6f,0,1.4f,Ice*2.6f,Cyan*1.2f,.016f,7300,1,-10);
        }
        // The blueprint: pens of light draw the new world's plan in the void (the floor's seams out from the centre, the monoliths'
        // edges up out of the dark, their capstones), the lines holding, faint, until the pieces arrive into them.
        private void Blueprint(INativeEntranceFx fx,float t,float thin)
        {
            float fade=(1-NativeEntranceMath.Smooth((t-Drop+.05f)/.12f))*(1-.4f*NativeEntranceMath.Bump(t,Inhale-.04f,DipA+.1f));float y=NativeEntranceStage.FloorY+.01f;
            // the floor's seams: radial lines between the tiles, drawn out from the dais
            for(int r=0;r<3;r++)
            {
                int count=RingCount[r];int every=thin<1&&r>0?2:1;
                for(int i=0;i<count;i+=every)
                {
                    float a=(i+(r==1?.5f:0)+.5f)/count*6.2831853f;float start=Still+.1f+r*.28f+.15f*R(7400+r,i);float u=NativeEntranceMath.EaseOut((t-start)/.45f);if(u<=0)continue;
                    var a0=Polar(RingR0[r],a,y);var a1=Polar(RingR1[r],a,y);var head=Vector3.Lerp(a0,a1,u);float lvl=(1.4f+2.2f*(1-NativeEntranceMath.Smooth((t-start-.45f)/.6f)))*fade;
                    Line(fx,head,a0,Cyan*lvl,.014f,.2f,7500+r*40+i);if(u<1)fx.Glow(head,Ice*2.2f,0,.07f,1,0,7600+r*40+i);
                }
            }
            // the monoliths: four edges each, drawn up from the plinth to the capstone, then the capstone's four lines to its point
            for(int m=0;m<8;m++)
            {
                if(thin<1&&(m&1)==1)continue;
                float a=(m+.5f)/8*6.2831853f;var foot=Polar(MonoRing,a,NativeEntranceStage.FloorY+.6f);float start=Still+.9f+.11f*m;float u=NativeEntranceMath.EaseOut((t-start)/.7f);if(u<=0)continue;
                float lvl=(.8f+1.8f*(1-NativeEntranceMath.Smooth((t-start-.7f)/.7f)))*fade;var tip=foot+new Vector3(0,9.6f+1.6f,0);
                for(int c=0;c<4;c++)
                {
                    var off=NativeEntranceMath.RotateY(new Vector3(c<2?.55f:-.55f,0,c==0||c==3?.55f:-.55f),a);var off1=off*(.35f/.55f);
                    var b0=foot+off;var b1=foot+new Vector3(0,9.6f,0)+off1;var head=Vector3.Lerp(b0,b1,u);Line(fx,head,b0,Cyan*lvl,.016f,.15f,7700+m*8+c);
                    float v=NativeEntranceMath.EaseOut((t-start-.75f)/.35f);if(v>0)Line(fx,Vector3.Lerp(b1,tip,v),b1,Ice*(lvl*1.2f),.014f,.1f,7740+m*8+c);
                }
                if(u<1)fx.Glow(foot+new Vector3(0,9.6f*u,0),Ice*2.4f,0,.12f,1,0,7800+m);
            }
            // the axis: one line up from the dais to where the crown will hang
            float ax=NativeEntranceMath.EaseOut((t-(Gather-.55f))/.5f);if(ax>0)Line(fx,new Vector3(0,.2f+(CrownY-.2f)*ax,0),new Vector3(0,.2f,0),Ice*(1.4f*fade),.014f,.3f,7820);
        }
        // The gathering's trails: every flying piece draws a streak of light behind it (long on the stutters' steps).
        private void Trails(INativeEntranceFx fx,float t,float thin)
        {
            for(int i=0;i<neo.Length;i++)
            {
                if(thin<1&&neo[i].Kind==Tile&&(i&1)==1)continue;
                if(!NewPose(i,t,out var at,out _,out bool solid,out float wire,out _)||solid)continue;if(!NewPose(i,t-.07f,out var back,out _,out _,out _,out _))continue;
                ref var p=ref neo[i];var c=at+NativeEntranceMath.RotateY(p.Centroid,p.Yaw);var cb=back+NativeEntranceMath.RotateY(p.Centroid,p.Yaw);if((c-cb).LengthSquared()<.0004f)continue;
                Line(fx,c,cb,Cyan*(.9f*Math.Min(1.4f,wire)),.03f+.012f*p.Size,.7f,8000+i);
            }
            // the inhale: a breath of light drawn in to the architect's chest
            float inh=NativeEntranceMath.Bump(t,Inhale-.05f,DipA);if(inh>.01f){var chest=Anchor(NativeEntranceAnchor.Chest,t);fx.Glow(chest,Ice*(1.6f*inh),0,.25f,1,0,8090);}
        }
        // THE DROP: light floods out along every seam from the centre, climbs the monoliths, runs round the crown; a burst of light
        // dust off the floor; the shards and glyph-light gone into the world.
        private void DropFx(INativeEntranceFx fx,float t,float thin,float floor)
        {
            float s=t-Drop;if(s<-.02f)return;
            if(s<1.2f)
            {
                for(int r=0;r<3;r++)
                {
                    int count=RingCount[r];int every=thin<1&&r>0?2:1;
                    for(int i=0;i<count;i+=every)
                    {
                        float a=(i+(r==1?.5f:0)+.5f)/count*6.2831853f;float front=s*18-RingR0[r];if(front<0)continue;float len=RingR1[r]-RingR0[r];float u=Math.Min(1,front/len);
                        float lvl=(1-NativeEntranceMath.Smooth((s-(RingR0[r]+len)/18f)/.55f))*3.2f;if(lvl<.02f)continue;var a0=Polar(RingR0[r],a,floor+.012f);var head=Vector3.Lerp(a0,Polar(RingR1[r],a,floor+.012f),u);
                        Line(fx,head,a0,Ice*lvl,.02f,.3f,8100+r*40+i);
                    }
                }
                // up the monoliths: a pulse climbing each obelisk's face
                for(int m=0;m<8;m++)
                {
                    if(thin<1&&(m&1)==1)continue;float a=(m+.5f)/8*6.2831853f;float start=.5f;float u=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((s-start)/.45f));if(s<start||u>=1)continue;
                    var face=Polar(MonoRing-.56f,a,0);float y=NativeEntranceStage.FloorY+.6f+11.2f*u;Line(fx,new Vector3(face.X,y,face.Z),new Vector3(face.X,Math.Max(NativeEntranceStage.FloorY+.6f,y-1.6f),face.Z),Ice*2.6f,.05f,.4f,8200+m);
                }
            }
            NativeEntranceFx.Sparks(fx,t,Drop,new Vector3(0,floor+.05f,0),new Vector3(0,.25f,0),1.4f,(int)(52*thin),9,.9f,6,.9f,Ice*2.6f,Gold*1.4f,.018f,8300,1,floor);
            if(s>=0&&s<.4f)fx.Glow(new Vector3(0,1.2f,0),Ice*(5*(1-s/.4f)),0,2.5f*(1-s/.4f)+.4f,1,0,8390);
            // embers of gold rising from the seams for the rest of it
            NativeEntranceFx.Embers(fx,t,Drop+.2f,Total+.5f,new Vector3(0,floor+.05f,0),7.2f,thin<1?14:34,.7f,2.4f,Gold*1.1f,.03f,8400,1,.25f);
        }

        // =====================================================================================================================
        // The name, written into the world in light (NameFx) and on the card (the "architect" effect, Runtime NativeIntroArchitectCard)
        // =====================================================================================================================
        /// <summary>Where the name hangs: above and behind the architect, facing the hero camera; its letters' height.</summary>
        public static readonly Vector3 NameCentre=new Vector3(0,4.3f,-3.6f);
        public const float LetterHeight=.8f,NameMaxWidth=11f;
        /// <summary>When letter `i` of `n` starts and how long one takes to write.</summary>
        public const float LetterStep=.05f,LetterTime=.24f,NameStart=CardAt+.04f;
        public static float LetterAt(int i)=>NameStart+i*LetterStep;
        private float NameLevel(float t)=>NativeEntranceMath.Ramp(t,NameStart,NameStart+.3f)*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total));
        private string nameKey;private readonly List<float> nameSegs=new List<float>(512);private readonly List<int> nameLetter=new List<int>(64);private int nameLetters;private float nameScale;
        /// <summary>Lays the live name out once per name: each stroke segment (x0, y0, x1, y1 in letter units, its letter, its share
        /// of its letter's ink) in the name's plane. Names the font can't write mostly (an unknown script) are left to the card.</summary>
        private void LayName(string name)
        {
            if(ReferenceEquals(name,nameKey))return;nameKey=name;nameSegs.Clear();nameLetter.Clear();nameLetters=0;if(string.IsNullOrEmpty(name))return;
            int known=0,shown=0;foreach(char ch in name){if(ch==' ')continue;shown++;if(NativeArchitectFont.Has(ch))known++;}
            if(shown==0||known*3<shown*2)return;
            float x=0;int letter=0;
            foreach(char ch in name)
            {
                if(ch==' '){x+=NativeArchitectFont.Space;continue;}
                int first=nameSegs.Count;float ink=NativeArchitectFont.Strokes(ch,x,nameSegs);
                // each segment's share of the letter's ink (for the pen), cumulative
                float run=0;for(int s=first;s<nameSegs.Count;s+=6){float len=new Vector2(nameSegs[s+2]-nameSegs[s],nameSegs[s+3]-nameSegs[s+1]).Length();nameSegs[s+4]=letter;nameSegs[s+5]=ink>0?run/ink:0;run+=len;}
                nameLetter.Add(first);x+=NativeArchitectFont.Advance;letter++;
            }
            nameLetters=letter;float width=Math.Max(1,x-NativeArchitectFont.Advance+NativeArchitectFont.Width);
            nameScale=Math.Min(LetterHeight/NativeArchitectFont.Height,NameMaxWidth/width);nameWidth=width;
        }
        private float nameWidth;
        private void NameFx(INativeEntranceFx fx,float t,float thin,Vector3 camRight,Vector3 camUp)
        {
            LayName(Context.Name);if(nameLetters==0)return;float level=NameLevel(t);if(level<.01f)return;
            // the plane faces the final hero eye, so the name reads square in the hero frame
            Rig.Evaluate(this,SettleAt,Context.Reduced,out var hero);var f=NameCentre-hero.Eye;f=f.LengthSquared()>1e-6f?Vector3.Normalize(f):-Vector3.UnitZ;
            var right=Vector3.Normalize(Vector3.Cross(f,Up));var up=Vector3.Normalize(Vector3.Cross(right,f));float k=nameScale;
            var origin=NameCentre-right*(nameWidth*k*.5f)-up*(NativeArchitectFont.Height*k*.5f);
            float sweep=NativeEntranceMath.Clamp01((t-13.9f)/1.0f);float sweepX=-.15f+1.3f*sweep;
            for(int s=0;s<nameSegs.Count;s+=6)
            {
                int letter=(int)nameSegs[s+4];float share=nameSegs[s+5];float start=LetterAt(letter);float u=(t-start)/LetterTime;if(u<=share)continue;
                // the pen: this segment drawn up to where the pen has reached
                float segShare=s+6<nameSegs.Count&&(int)nameSegs[s+10]==letter?nameSegs[s+11]-share:1-share;float part=segShare<=1e-4f?1:NativeEntranceMath.Clamp01((u-share)/segShare);
                float age=t-start-LetterTime;float fresh=age<0?1:(float)Math.Exp(-age*5);
                // distortion settling: the letter slips sideways in a band and its colour splits, both dying away
                float slip=(Context.Reduced?.012f:.06f)*fresh*fresh*S(9700+letter,(int)(t*14));
                var a=origin+right*((nameSegs[s]*k)+slip)+up*(nameSegs[s+1]*k);var b0=origin+right*((nameSegs[s+2]*k)+slip)+up*(nameSegs[s+3]*k);var b=Vector3.Lerp(a,b0,part);
                float mx=(nameSegs[s]+nameSegs[s+2])*.5f/Math.Max(1,nameWidth);float shine=1+1.6f*Math.Max(0,1-Math.Abs(mx-sweepX)*6)*sweep*(1-sweep);
                var ink=Vector3.Lerp(Ice*3.2f,Gold*2.2f,NativeEntranceMath.Clamp01(age*2.5f))*(level*shine*(1+1.4f*fresh));
                fx.Streak(b,b-a,ink,.03f*Math.Max(.7f,k/.1f),.001f,1,.05f,0,9800+s);
                if(fresh>.08f&&thin>=1){var split=right*(.045f*fresh);fx.Streak(b+split,b-a,Rose*(1.1f*fresh*level),.022f,.001f,1,.05f,0,9900+s);fx.Streak(b-split,b-a,Blue*(1.3f*fresh*level),.022f,.001f,1,.05f,0,9950+s);}
                if(part<1)fx.Glow(b,Vector3.One*(3.2f*level),0,.09f,1,0,9990+s);
            }
            // the letters' strike: a spark burst as each closes
            for(int i=0;i<nameLetters;i++){float done=LetterAt(i)+LetterTime;if(t<done||t>done+.5f)continue;int first=nameLetter[i];var at=origin+right*((nameSegs[first]+1.8f)*k)+up*(3*k);NativeEntranceFx.Sparks(fx,t,done,at,Vector3.Zero,1,(int)(10*thin),1.8f,.4f,2,1.8f,Ice*2.2f,Gold,.01f,10100+i*13,1,-10);}
            // glyph-embers drifting up off the name once it is written
            int n=(int)(18*thin);for(int i=0;i<n;i++){int seed=10400+i*7;float born=NameStart+.6f+2.2f*R(seed,1);float a=t-born;if(a<0||a>1.4f)continue;var p=NameCentre+right*(S(seed,2)*nameWidth*k*.5f)+up*(S(seed,3)*.3f+.4f*a);float u=a/1.4f;DrawGlyph(fx,(int)(R(seed,4)*16),p,.07f,Gold*(1.4f*level*(1-u)),camRight,camUp,seed);}
        }
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-CardAt,new Vector3(1,.97f,.9f),Gold);card.Style=NativeIntroCardStyle.Assemble;card.Effect=CardEffect;card.Kicker="THE ARCHITECT";card.Size=.084f;card.Y=.885f;
            // EffectA: how far the build has run (s since the name beat); EffectB: the distortion left (1 at the beat .. 0 settled)
            card.EffectA=Math.Max(0,t-CardAt);card.EffectB=(float)Math.Exp(-Math.Max(0,t-CardAt-.3f)*2.2f);card.Exit=NativeEntranceMath.Ramp(t,FadeFrom,Total-.05f);
        }

        // =====================================================================================================================
        // Sound: the song carries the big hits; the cues fill the gaps and texture the beats (key, then an existing bank sound, then a clip).
        // =====================================================================================================================
        private const string Cue="intro2.architect.";
        public static readonly NativeIntroCue[] CueSheet=
        {
            new NativeIntroCue(Strike,Cue+"strike","magic.lightning.hit","lightning",.55f),
            new NativeIntroCue(.06f,Cue+"glitch","dev.leet.glitch","ui",.4f),
            new NativeIntroCue(.58f,Cue+"glitch","dev.leet.glitch","ui",.32f,1.12f),
            new NativeIntroCue(1.07f,Cue+"crack","structure.crack","stone",.5f),
            new NativeIntroCue(1.43f,Cue+"glitch","dev.leet.glitch","ui",.3f,.9f),
            new NativeIntroCue(1.86f,Cue+"glitch","dev.leet.glitch","ui",.36f,1.2f),
            new NativeIntroCue(Break,Cue+"dissolve","death.style.matrix","darkness",.75f),
            new NativeIntroCue(Tear,Cue+"tear","blast.static","electric-hook",.6f),
            new NativeIntroCue(Presence,Cue+"presence","blast.implosion","darkness",.45f),
            new NativeIntroCue(PulseA,Cue+"pulse","chip.teleport","install",.28f),
            new NativeIntroCue(Draft,Cue+"draft","turret.scan","electric-hook",.55f),
            new NativeIntroCue(Condense,Cue+"condense","magic.singularity","install",.45f),
            new NativeIntroCue(Body,Cue+"form","blast.nova","explosion",.42f),
            new NativeIntroCue(Still,Cue+"blueprint","field.time","ui",.5f),
            new NativeIntroCue(Gather,Cue+"gather","structure.whoosh","dash",.55f),
            new NativeIntroCue(11.19f,Cue+"snap","ui.install.lock","metal",.28f),
            new NativeIntroCue(11.31f,Cue+"snap","ui.install.lock","metal",.28f,1.06f),
            new NativeIntroCue(11.44f,Cue+"snap","ui.install.lock","metal",.3f,1.12f),
            new NativeIntroCue(11.565f,Cue+"snap","ui.install.lock","metal",.32f,1.18f),
            new NativeIntroCue(11.69f,Cue+"snap","ui.install.lock","metal",.34f,1.25f),
            new NativeIntroCue(Inhale,Cue+"inhale","blast.implosion","darkness",.42f,1.2f),
            new NativeIntroCue(Drop,Cue+"lock","blast.quake","explosion",.6f),
            new NativeIntroCue(CardAt+.05f,Cue+"forge","chip.critical","win",.5f),
            new NativeIntroCue(13.35f,Cue+"settle","field.heal","install",.32f),
        };
    }

    /// <summary>
    /// The stroke font the architect writes the live name in (an original design: angular, single strokes, the curves cut at 45
    /// degrees; a 4 x 6 cell). Letters (any case, the common accented Latin folded to their base), digits and common punctuation;
    /// anything else is left to the name card.
    /// </summary>
    public static class NativeArchitectFont
    {
        public const float Width=4,Height=6,Advance=5.6f,Space=3.2f;
        // per character: polylines "x,y x,y ...|x,y ..." in the 4 x 6 cell
        private static readonly string[] Spec=
        {
            "A:0,0 0,4 2,6 4,4 4,0|0,2.6 4,2.6","B:0,0 0,6 3,6 4,5 4,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0","C:4,6 1,6 0,5 0,1 1,0 4,0","D:0,0 0,6 3,6 4,5 4,1 3,0 0,0",
            "E:4,6 0,6 0,0 4,0|0,3 3,3","F:4,6 0,6 0,0|0,3 3,3","G:4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3","H:0,0 0,6|4,0 4,6|0,3 4,3","I:2,0 2,6|1,6 3,6|1,0 3,0",
            "J:4,6 4,1 3,0 1,0 0,1","K:0,0 0,6|4,6 0,2.4|1.3,3.3 4,0","L:0,6 0,0 4,0","M:0,0 0,6 2,3 4,6 4,0","N:0,0 0,6 4,0 4,6","O:1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            "P:0,0 0,6 3,6 4,5 4,4 3,3 0,3","Q:1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|2.5,1.5 4.3,-.3","R:0,0 0,6 3,6 4,5 4,4 3,3 0,3|2,3 4,0",
            "S:4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1","T:0,6 4,6|2,6 2,0","U:0,6 0,1 1,0 3,0 4,1 4,6","V:0,6 2,0 4,6","W:0,6 1,0 2,4 3,0 4,6",
            "X:0,6 4,0|4,6 0,0","Y:0,6 2,3 4,6|2,3 2,0","Z:0,6 4,6 0,0 4,0",
            "0:1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|1,1 3,5","1:1,5 2,6 2,0|1,0 3,0","2:0,5 1,6 3,6 4,5 4,4 0,0 4,0","3:0,6 4,6 2,3.5 3,3.5 4,2.5 4,1 3,0 1,0 0,1",
            "4:3,0 3,6 0,2 4,2","5:4,6 0,6 0,3.5 3,3.5 4,2.5 4,1 3,0 0,0","6:3.5,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2.5 3,3.5 0,3.5","7:0,6 4,6 1.5,0",
            "8:1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3","9:.5,0 3,0 4,1 4,5 3,6 1,6 0,5 0,3.5 1,2.5 4,2.5",
            "-:.5,3 3.5,3","_:0,0 4,0",".:1.7,0 2.3,0","!:2,6 2,1.6|2,.4 2,0","?:0,5 1,6 3,6 4,5 4,4 2,2.5 2,1.6|2,.4 2,0","#:1.3,0 1.3,6|2.7,0 2.7,6|0,2 4,2|0,4 4,4",
            "':2,6 2,4.6","+:2,1 2,5|0,3 4,3","*:2,1 2,5|.4,2 3.6,4|.4,4 3.6,2","/:0,0 4,6","\\:0,6 4,0","::2,4 2,4.4|2,1 2,1.4","(:3,6 2,5 2,1 3,0","):1,6 2,5 2,1 1,0",
            "[:3,6 1.5,6 1.5,0 3,0","]:1,6 2.5,6 2.5,0 1,0","=:0,2 4,2|0,4 4,4","~:0,3 1,4 3,2 4,3","|:2,0 2,6","^:1,4.5 2,6 3,4.5","<:4,6 0,3 4,0",">:0,6 4,3 0,0",
            "$:4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1|2,6.6 2,-.6","%:0,0 4,6|0,6 1,6 1,5 0,5 0,6|3,1 4,1 4,0 3,0 3,1",",:2,.3 1.6,-.6",";:2,4 2,4.4|2,.3 1.6,-.6",
            "&:4,0 1,4 1,5 2,6 3,5 3,4 0,1.5 0,.8 1,0 2.5,0 4,2","@:3,2 3,4 1,4 1,2 3,2 4,2 4,5 3,6 1,6 0,5 0,1 1,0 4,0",
        };
        private static readonly Dictionary<char,float[][]> font=Build();
        private static Dictionary<char,float[][]> Build()
        {
            var d=new Dictionary<char,float[][]>();var inv=System.Globalization.CultureInfo.InvariantCulture;
            foreach(var e in Spec)
            {
                char c=e[0];var lines=e.Substring(2).Split('|');var polys=new float[lines.Length][];
                for(int i=0;i<lines.Length;i++){var pts=lines[i].Split(' ');var p=new float[pts.Length*2];for(int j=0;j<pts.Length;j++){var xy=pts[j].Split(',');p[j*2]=float.Parse(xy[0],inv);p[j*2+1]=float.Parse(xy[1],inv);}polys[i]=p;}
                d[c]=polys;
            }
            return d;
        }
        /// <summary>The common accented Latin letters folded to their base letter; everything upper case.</summary>
        public static char Fold(char c)
        {
            if(c>='a'&&c<='z')return (char)(c-32);if(c<128)return c;
            const string from="ÀÁÂÃÄÅàáâãäåÇçÈÉÊËèéêëÌÍÎÏìíîïÑñÒÓÔÕÖØòóôõöøÙÚÛÜùúûüÝýÿ",to="AAAAAAAAAAAACCEEEEEEEEIIIIIIIINNOOOOOOOOOOOOUUUUUUUUYYY";
            int i=from.IndexOf(c);return i>=0?to[i]:c;
        }
        public static bool Has(char c)=>font.ContainsKey(Fold(c));
        /// <summary>Appends character `c`'s stroke segments at pen x (letter units) as (x0, y0, x1, y1, 0, 0) and returns its ink length;
        /// an unknown character draws a small open box.</summary>
        public static float Strokes(char c,float x,List<float> into)
        {
            float ink=0;
            if(!font.TryGetValue(Fold(c),out var polys)){float[] box={x+.6f,0,x+3.4f,0,x+3.4f,0,x+3.4f,4,x+3.4f,4,x+.6f,4,x+.6f,4,x+.6f,0};for(int i=0;i<box.Length;i+=4){into.Add(box[i]);into.Add(box[i+1]);into.Add(box[i+2]);into.Add(box[i+3]);into.Add(0);into.Add(0);ink+=Math.Abs(box[i+2]-box[i])+Math.Abs(box[i+3]-box[i+1]);}return ink;}
            foreach(var p in polys)for(int i=0;i+3<p.Length;i+=2){into.Add(x+p[i]);into.Add(p[i+1]);into.Add(x+p[i+2]);into.Add(p[i+3]);into.Add(0);into.Add(0);ink+=new Vector2(p[i+2]-p[i],p[i+3]-p[i+1]).Length();}
            return ink;
        }
        /// <summary>Every character the font writes.</summary>
        public static IEnumerable<char> Characters=>font.Keys;
    }
}
