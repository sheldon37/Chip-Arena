using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2 (owner 2026-11-20; the contract is docs/INTROS-V2.md): what the runtime tells an intro each frame before it
    /// asks for anything. Set by Runtime DirectedIntro (and the Locker's preview) before Pose, Camera, Lights, Props, Set and
    /// Effects; the managed checks set it themselves.
    /// </summary>
    public struct NativeIntroContext
    {
        /// <summary>The player's Reduced effects setting: no camera shake, nothing flashing faster than 3 Hz.</summary>
        public bool Reduced;
        /// <summary>The Low graphics tier (fewer set pieces, thinner effects).</summary>
        public bool Low;
        /// <summary>The effects quality 0..1 (the same `quality` Props and Effects get).</summary>
        public float Quality;
        /// <summary>The pilot's live display name (the seat's name: the account profile's name when signed in), never null.</summary>
        public string Name;
    }

    /// <summary>One sound of an intro's cue timeline, on the intro's presentation clock (NativeEntranceScript.Clock).</summary>
    public struct NativeIntroCue
    {
        /// <summary>When it plays (presentation s).</summary>
        public float At;
        /// <summary>The effects-bank key requested for this cue ("intro2.&lt;intro&gt;.&lt;cue&gt;", generated through
        /// docs/audio/SOUND-REQUESTS.md), or null.</summary>
        public string Key;
        /// <summary>An existing effects-bank key played when <see cref="Key"/> has no clip (e.g. "melee.sword.swing"), or null.</summary>
        public string Alt;
        /// <summary>A built-in clip (Resources/Audio/audio/&lt;Clip&gt;, e.g. "sword") played when neither key has one, or null.</summary>
        public string Clip;
        public float Gain,Rate;
        public NativeIntroCue(float at,string key,string alt,string clip,float gain,float rate=1){At=at;Key=key;Alt=alt;Clip=clip;Gain=gain;Rate=rate;}
    }

    /// <summary>The ways a name card arrives (Runtime NativeIntroCardScreen draws each).</summary>
    public enum NativeIntroCardStyle:byte
    {
        /// <summary>Rises and sharpens out of a blur, the tracking closing (the classic card, refined).</summary>
        Rise,
        /// <summary>Slams in from large with a hard settle and a light sweep across the letters.</summary>
        Slam,
        /// <summary>Typed on letter by letter behind a caret, as code.</summary>
        Type,
        /// <summary>Letters assemble from scattered shards toward their places.</summary>
        Assemble,
        /// <summary>A bright line draws across, then the name is revealed from it (a wipe).</summary>
        Wipe,
    }

    /// <summary>
    /// An intro's name card this frame (INativeIntroV2.Card fills it; Runtime NativeIntroCardScreen draws it). Colours are
    /// linear rgb (0..1, glow may exceed 1). The name is the live display name (NativeIntroContext.Name); the card never
    /// shows the intro's own name.
    /// </summary>
    public struct NativeIntroCardState
    {
        /// <summary>Draw the card at all (false: the plain match intro card).</summary>
        public bool Shown;
        /// <summary>Seconds since the card began (its reveal is timed from 0).</summary>
        public float Age;
        public NativeIntroCardStyle Style;
        /// <summary>The name's fill and glow colours.</summary>
        public Vector3 Ink,Glow;
        /// <summary>The kicker line above (null: the weapon's name) and the line below (null: the pilot's intro text).</summary>
        public string Kicker,Line;
        /// <summary>The name's height as a share of the screen height (0: the default .075).</summary>
        public float Size;
        /// <summary>Where the name's centre sits, as screen shares (0,0: the default, centred low at .5,.82).</summary>
        public float X,Y;
        /// <summary>0..1: the card leaving (fades and drops).</summary>
        public float Exit;
        /// <summary>A bespoke effect drawn over the card (Runtime NativeIntroCardEffects: "architect" is the dev intro's), or null.</summary>
        public string Effect;
        /// <summary>Free parameters for the bespoke effect.</summary>
        public float EffectA,EffectB;
        public static NativeIntroCardState Default(float age,Vector3 ink,Vector3 glow)=>new NativeIntroCardState{Shown=age>=0,Age=Math.Max(0,age),Style=NativeIntroCardStyle.Rise,Ink=ink,Glow=glow};
    }

    /// <summary>The lens this frame: the cinema bars and the focus (NativeIntroCamera).</summary>
    public struct NativeIntroLens
    {
        /// <summary>0..1: how far the letterbox bars are in (1: 2.39:1 framing).</summary>
        public float Letterbox;
        /// <summary>The focus distance (m) and how shallow the focus is (0 deep .. 1 very shallow): Runtime approximates the feel
        /// (a vignette that tightens with the aperture); a real depth-of-field pass can read the same values later.</summary>
        public float Focus,Aperture;
        /// <summary>0..1: a fade to black (an intro's own cut to black or iris).</summary>
        public float Black;
    }

    /// <summary>
    /// Where an intro's set goes (Runtime NativeIntroSetDraw): modelled pieces from its set bank (Resources/ChipArenaIntro*.bytes,
    /// Core NativePackBank format, git-ignored) and the source-model primitives every build has. Source frame (+x the rig's left).
    /// </summary>
    public interface INativeIntroSet
    {
        /// <summary>The intro's bank is loaded (false in CI, a build without the bank, --ca-no-pack-banks): draw the fallback.</summary>
        bool Ready{get;}
        /// <summary>The bank has prop `prop` (its tiers: "&lt;prop&gt;" Fine, "&lt;prop&gt;/low" Low).</summary>
        bool Has(string prop);
        /// <summary>Bank prop `prop` at a pose and uniform scale; `tint` multiplies its colours, `glow` its glow submesh (linear,
        /// may exceed 1). The Low tier draws "&lt;prop&gt;/low" when the bank has it. Missing props draw nothing.</summary>
        void Piece(string prop,Vector3 at,Quaternion rotation,float scale,Vector3 tint,Vector3 glow);
        /// <summary>A source-model primitive (lit, or glow: unlit, its colour emitted, linear, may exceed 1).</summary>
        void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color);
        /// <summary>Many copies of bank prop `prop` in one instanced draw (casings, debris, shards): positions and turns, one scale
        /// and tint for all, no shadows. At most <see cref="NativeIntroSetLimits.Batch"/> a frame in all.</summary>
        void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> rotation,float scale,Vector3 tint);
    }
    /// <summary>The set's per-frame budgets (the managed checks hold every intro to them).</summary>
    public static class NativeIntroSetLimits
    {
        /// <summary>Instanced copies a frame (Batch), all props together.</summary>
        public const int Batch=400;
        /// <summary>Pieces a frame: a generic, default or weapon intro; the dev intro (High, Low).</summary>
        public const int Pieces=80,DevPieces=180,DevPiecesLow=90;
        /// <summary>Effect calls a frame: a generic, default or weapon intro (High and Low); the dev intro (High, Low).</summary>
        public const int Fx=600,DevFx=1000,DevFxLow=450;
        /// <summary>The longest intro (s): a generic, default or weapon intro; the dev intro (the coordinator: the owner's song).</summary>
        public const float Longest=9f,DevLongest=16f;
    }

    /// <summary>
    /// Optional for a v2 intro: the model body plays a motion-bank clip (docs/INTROS-V2.md "Motion"): the clip and its time at `t`
    /// (false: the model follows the keyed rig), and the fade into it. The keyed rig stays the anchors' source (camera, effects) and
    /// the Classic body's pose. A NativeBladeIntro keeps its own path (this is not read for one).
    /// </summary>
    public interface INativeIntroBody
    {
        bool Clip(float t,out string clip,out float time);
        float Fade(float t);
    }
    /// <summary>One hand as the runtime hands it in (source frame): the wrist's position and turn, from the model body when it is
    /// drawn (Model true), else the keyed rig's.</summary>
    public struct NativeIntroHand{public bool Model;public Vector3 At;public Quaternion Turn;
        /// <summary>A gun Weapon intro's right hand only: the drawn weapon's muzzle and its bore (unit), from the weapon's frame after the
        /// hand fit (NativeWeaponDressing.MuzzleZ along its +z); HasMuzzle false otherwise.</summary>
        public bool HasMuzzle;public Vector3 Muzzle,Bore;}
    /// <summary>
    /// Optional for a v2 intro: the hands as drawn this frame, handed in before Props, Set and Effects (docs/INTROS-V2.md "Hands"),
    /// so a muzzle flash, a cast or a trail leaves the drawn weapon. A checker that never hands them in still sees every effect.
    /// </summary>
    public interface INativeIntroHands{void Hands(float t,in NativeIntroHand right,in NativeIntroHand left);}
    /// <summary>Optional for a v2 intro: the head as drawn (the model's head joint when the model is drawn, else the keyed head anchor), handed
    /// in with the hands, before Props, Set and Effects (I5: a lit eye on the visor, a crown on the head while a clip plays).</summary>
    public interface INativeIntroHead{void Head(float t,in NativeIntroHand head);}
    /// <summary>Optional for a v2 Bow intro: the bow as the match draws it (on the model's left hand, its string and its nocked arrow)
    /// from the view at `t` (false: carried, one arrow). Without it the bow is the plain model in the hand.</summary>
    public interface INativeIntroBow{bool Bow(float t,out NativeBow.View view);}

    /// <summary>
    /// A held weapon's points in its weapon frame (source frame: +x the rig's left, +y up the sights, +z along the blade or barrel: the
    /// frame the weapon is drawn on), as the match places its own effects there: a blade's tip socket (NativeWeaponDressing.BladeTip; a
    /// blade class's NativeBladeView.Line), a gun's, a staff's and the bow's muzzle (NativeWeaponDressing.MuzzleZ along +z). The anchors'
    /// keyed fallback puts them on the keyed wrist (NativeEntranceScript.Anchor); the runtime on the drawn weapon (Runtime IntroV2Drawn,
    /// where a gun's muzzle is the drawn skin's crown, NativeMuzzleFx.Crown, as the match's flash leaves it).
    /// </summary>
    public static class NativeIntroWeapon
    {
        /// <summary>The daggers and the Fangs hold a blade in each hand.</summary>
        public static bool Twin(string spec)=>spec=="daggers"||spec==NativeFangs.Spec;
        /// <summary>Whether `spec`'s weapon rides hand `left` (a staff and the bow ride the left, as the match holds them; the twins both).</summary>
        public static bool Holds(string spec,bool left)=>spec!=null&&(Twin(spec)||(NativeLobbyPoses.HeldBone(spec)==CapturedRig.LHand)==left);
        /// <summary>The weapon's far end in hand `left`'s weapon frame: a blade's point, a gun's or a staff's muzzle; false when that hand holds none.</summary>
        public static bool Tip(string spec,bool left,out Vector3 tip)
        {
            tip=Vector3.Zero;if(!Holds(spec,left))return false;
            if(NativeBladeView.Line(spec,left,out _,out tip))return true;
            if(NativeWeaponDressing.BladeTip(spec,out tip)){if(left)tip.X=-tip.X;return true;}
            tip=new Vector3(0,0,NativeWeaponDressing.MuzzleZ(spec));return true;
        }
        /// <summary>Where a shot or a cast leaves the weapon in hand `left` (a gun's muzzle, a staff's head, the bow's arrow line ahead of the
        /// riser; a blade's tip); false when that hand holds none.</summary>
        public static bool Muzzle(string spec,bool left,out Vector3 muzzle)
        {
            muzzle=Vector3.Zero;if(!Holds(spec,left))return false;
            if(NativeWeaponDressing.Gun(spec)||NativeWeaponDressing.Staff(spec)||NativeBowWire.Is(spec)){muzzle=new Vector3(0,0,NativeWeaponDressing.MuzzleZ(spec));return true;}
            return Tip(spec,left,out muzzle);
        }
    }

    /// <summary>
    /// The v2 intro interface (docs/INTROS-V2.md "The per-frame interface"). An intro is a NativeEntranceScript (its pose, camera,
    /// lights, props, effects, ghosts and blasts, as every directed entrance) that also implements this: its set, its name card,
    /// its lens, its music and its sound cues. Usually through <see cref="NativeIntroV2Script"/>; a script that must derive
    /// from another base (NativeBladeIntro for pack-clip blades) implements this interface itself.
    /// </summary>
    public interface INativeIntroV2
    {
        /// <summary>Set by the runtime before every frame's calls.</summary>
        NativeIntroContext Context{get;set;}
        /// <summary>The set bank this intro reads ("ChipArenaIntro&lt;Name&gt;"), or null for primitives only.</summary>
        string SetBank{get;}
        /// <summary>The set at presentation time `t` (after Props, before Effects).</summary>
        void Set(float t,INativeIntroSet set);
        /// <summary>The name card at presentation time `t`.</summary>
        void Card(float t,ref NativeIntroCardState card);
        /// <summary>The lens at presentation time `t`.</summary>
        void Lens(float t,out NativeIntroLens lens);
        /// <summary>The music bed played from the intro's start (a Resources clip id such as "audio/intros/neon-drop"), or null.</summary>
        string Music{get;}
        /// <summary>The cue timeline, sorted by At (built once; never allocated per frame).</summary>
        NativeIntroCue[] Cues{get;}
    }

    /// <summary>
    /// A speed ramp table: the presentation clock as a function of the intro age, slowed through each ramp and caught up after it,
    /// so the total length (and so every beat after the last ramp's repayment, the name beat included) is unchanged. Built once
    /// (Ramp calls in the constructor), sampled without allocation.
    /// </summary>
    public sealed class NativeIntroTime
    {
        private const int Rate=240;
        private readonly System.Collections.Generic.List<(float at,float rate,float hold,float repay)> ramps=new System.Collections.Generic.List<(float,float,float,float)>();
        private float[] table;private float length;
        /// <summary>A ramp: from `at` the clock runs at `rate` (e.g. .25 for quarter speed) for `hold` s of age, easing in and out
        /// over .08 s, then runs fast over `repay` s of age until it has caught up.</summary>
        public NativeIntroTime Ramp(float at,float rate,float hold,float repay){ramps.Add((at,Math.Max(.05f,Math.Min(1,rate)),Math.Max(0,hold),Math.Max(.1f,repay)));table=null;return this;}
        public bool Any=>ramps.Count>0;
        private float Speed(float a)
        {
            float v=1;
            foreach(var r in ramps)
            {
                float edge=Math.Min(.08f,r.hold*.5f);float debt=(1-r.rate)*r.hold;
                // the slow window (smooth edges) and the repayment (a smooth bump whose area is the debt)
                float inSlow=edge>0?NativeEntranceMath.Ramp(a,r.at-edge,r.at+edge)*(1-NativeEntranceMath.Ramp(a,r.at+r.hold-edge,r.at+r.hold+edge)):(a>=r.at&&a<r.at+r.hold?1:0);
                v-=(1-r.rate)*inSlow;
                float u=(a-r.at-r.hold-edge)/r.repay;if(u>0&&u<1)v+=debt/r.repay*2*(float)Math.Pow(Math.Sin(Math.PI*u),2);
            }
            return Math.Max(.02f,v);
        }
        private void Build(float upTo)
        {
            length=Math.Max(upTo,1);int n=(int)(length*Rate)+2;table=new float[n];float sum=0;table[0]=0;
            for(int i=1;i<n;i++){float a=(i-.5f)/Rate;sum+=Speed(a)/Rate;table[i]=sum;}
        }
        /// <summary>The presentation time at age `age` (identity without ramps; ages past the table run on at speed 1).</summary>
        public float Map(float age,float length)
        {
            if(!(age>0))return 0;if(ramps.Count==0)return age;if(table==null||length>this.length)Build(length+1);
            float x=age*Rate;int i=(int)x;if(i>=table.Length-1)return table[table.Length-1]+(age-(table.Length-1)/(float)Rate);float f=x-i;return table[i]+(table[i+1]-table[i])*f;
        }
    }

    /// <summary>
    /// The base for a procedural v2 intro (docs/INTROS-V2.md): keyed pose (NativeProceduralEntrance), the cinematic camera rig
    /// (<see cref="Rig"/>), speed ramps (<see cref="Ramps"/>), its own set with the dark lit void as the fallback, its name card,
    /// its lens, its cues. No pylon arena, no warp at Hit (ramps instead), no camera jolt (the rig's handheld, off in Reduced).
    /// </summary>
    public abstract class NativeIntroV2Script:NativeProceduralEntrance,INativeIntroV2,INativeIntroBody
    {
        /// <summary>The model's motion-bank clip at `t` (false: it follows the keyed rig); override with your retimed clips.</summary>
        public virtual bool Clip(float t,out string clip,out float time){clip=null;time=0;return false;}
        /// <summary>The fade into the clip at `t` (s).</summary>
        public virtual float Fade(float t)=>.12f;
        /// <summary>How far the model's free arms are re-placed by its own body at `t` (Core NativeVisualRig Grasp.Relax, the match's idles'
        /// hang): 0, the default, draws your keyed arms as keyed (docs/INTROS-V2.md 2b); 1 relaxes a free arm hanging below the shoulder line.</summary>
        public virtual float ArmsRelax(float t)=>0;
        /// <summary>Planted legs bend as true hinges (NativeEntrancePose.HingedLeg), so the model's knees bend forward, never sideways.</summary>
        protected override bool HingedLegs=>true;
        /// <summary>The intro's length and its name beat (presentation s; the registry repeats them for the match).</summary>
        public readonly float IntroTotal,IntroNameAt;
        public NativeIntroContext Context{get;set;}
        /// <summary>The camera (built in the constructor with the rig's fluent calls).</summary>
        protected readonly NativeIntroCamera Rig=new NativeIntroCamera();
        /// <summary>The speed ramps (built in the constructor).</summary>
        protected readonly NativeIntroTime Ramps=new NativeIntroTime();
        protected NativeIntroV2Script(string id,float total,float cardAt,float hit,int accent):base(id,hit,accent)
        {
            IntroTotal=total;IntroNameAt=cardAt;SlowRate=1;SlowHold=0;SlowRecover=0;SlowCatchUp=0;
            Context=new NativeIntroContext{Quality=1,Name="Pilot"};
        }
        public override float Length=>IntroTotal;public override float SettleAt=>IntroTotal-.25f;protected override float HeroAt=>IntroNameAt;
        public override float Clock(float age){if(!(age>0))return 0;return Ramps.Any?Ramps.Map(Math.Min(age,IntroTotal+1),IntroTotal+1):Math.Min(age,IntroTotal+1);}
        public override void Camera(float age,out NativeEntranceView view)
        {
            float t=Clock(Math.Min(age,SettleAt));Rig.Evaluate(this,t,Context.Reduced,out view);
            if(view.Eye.Y<NativeEntranceStage.FloorY+.06f)view.Eye.Y=NativeEntranceStage.FloorY+.06f;
        }
        public override Vector3 CameraJolt(float t)=>Vector3.Zero;
        public override int Boxes(float t,Span<Vector4> into)=>0;
        /// <summary>The hologram (Holo) without the pilot's head gear (an intro that draws its own over it); its ghosts say so each (NativeEntranceGhost.Bare).</summary>
        public virtual bool HoloBare=>false;
        /// <summary>No pylon arena: the set comes through <see cref="Set"/>.</summary>
        public override void Props(float t,INativeEntranceProps props,float quality){}
        public virtual string SetBank=>null;
        public virtual string Music=>null;
        public virtual NativeIntroCue[] Cues=>Array.Empty<NativeIntroCue>();
        public void Set(float t,INativeIntroSet set){if(!(set.Ready&&Scene(t,set)))Fallback(t,set);}
        /// <summary>The modelled set from the bank; return false to draw the fallback instead (a piece missing).</summary>
        protected virtual bool Scene(float t,INativeIntroSet set)=>false;
        /// <summary>The look of the fallback void (override to tint it to the intro).</summary>
        protected virtual NativeIntroVoidLook VoidLook(float t)=>NativeIntroVoidLook.Of(Accent);
        /// <summary>The set without the bank: the dark lit void (never the old block arena).</summary>
        protected virtual void Fallback(float t,INativeIntroSet set)=>NativeIntroVoid.Draw(set,t,VoidLook(t));
        /// <summary>The void's light rig (fog, ambient, a key, a rim); an intro adds its own lights after calling this.</summary>
        public override void Lights(float t,NativeEntranceLights l){NativeIntroVoid.Light(l,t,VoidLook(t),Anchor(NativeEntranceAnchor.Chest,t));}
        public virtual void Card(float t,ref NativeIntroCardState card){card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.96f,.98f,1),Accent);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);}
        public virtual void Lens(float t,out NativeIntroLens lens){Rig.Lens(this,t,Context.Reduced,out lens);}
        /// <summary>A flash level that is safe in Reduced effects: `level` as authored, but never brighter than a 3 Hz-limited
        /// envelope when Reduced (a pulse narrower than 1/6 s is widened and dimmed).</summary>
        protected float Flash(float t,float at,float attack,float decay,float gain=1)=>Context.Reduced?NativeEntranceMath.Pulse(t,at,Math.Max(attack,.08f),Math.Max(decay,.18f))*gain*.6f:NativeEntranceMath.Pulse(t,at,attack,decay)*gain;
    }
}
