using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blade classes' entrances (owner 2026-11-10: "give them both a very awesome intro with effects"; docs/KATANA.md, docs/FANGS.md "The
    /// intro"). Each plays the Frank Slash Pack's own clips on the model (Clip: which clip and where in it at intro time `t`; the runtime lays
    /// it on the intro body through the arena's motion, Runtime NativeVisualMotionPresentation), with a keyed stance under it for the Classic
    /// body, its camera, lights and effects. Presentation only: functions of the entrance clock, thinned by `quality`.
    /// Their look (owner 2026-11-11: "Intro also needs to be fixed on both fangs and Katana. (Floating parts, needs much improved vfx)"): set
    /// pieces where a shape must read crisp (the Katana's moon, its petals, the line of light and its shards; the Fangs' venom drops), lit smoke
    /// where soft glow balls hung in the air before, no glow rings, and the blades' trails, drips and flung venom taken from the blades as the
    /// model really holds them (Blade: the runtime hands them in each frame; the keyed stance's hands are not where the clip puts them). A
    /// checker that never hands them in sees the effects without those.
    /// </summary>
    public abstract class NativeBladeIntro:NativeProceduralEntrance
    {
        protected NativeBladeIntro(string style,float hit,int accent):base(style,hit,accent){}
        public abstract string Spec{get;}
        /// <summary>The pack clip the model plays at `t` and its time (false: the keyed stance).</summary>
        public abstract bool Clip(float t,out string clip,out float time);
        /// <summary>Whether the blade is put away at `t` (the katana in its saya).</summary>
        public virtual bool Sheathed(float t)=>false;
        /// <summary>How long the model takes to go over into the clip it plays at `t` (s; NativeBladeMotion.IntroFade).</summary>
        public virtual float Fade(float t)=>.12f;
        /// <summary>A clip window `from`..`to` (clip s) laid across intro `t0`..`t1`, with `at` (clip s) landing on intro `hit`.</summary>
        protected static float Fit(float t,float t0,float hit,float t1,float from,float at,float to)
        {
            if(t<=hit)return from+(at-from)*NativeEntranceMath.Clamp01((t-t0)/Math.Max(1e-3f,hit-t0));
            return at+(to-at)*NativeEntranceMath.Clamp01((t-hit)/Math.Max(1e-3f,t1-hit));
        }

        // ---- the blades as the model holds them (Runtime NativeBladePresentation.BladeIntroHeld) ----
        private const int Samples=48;
        private readonly Vector3[] heldRoot=new Vector3[2*Samples],heldTip=new Vector3[2*Samples],lastRoot=new Vector3[2],lastTip=new Vector3[2];
        private readonly float[] heldAt=new float[2*Samples],lastAt={-100,-100};private readonly int[] heldCount=new int[2],heldHead=new int[2];
        private float heldClock=-1;
        /// <summary>The blade in `hand` (0 the right, 1 the left) as the model holds it at intro `t` (stage frame): its root where it leaves
        /// the hilt and its tip; `shown` false while it is put away or the pilot unseen. Called each frame before Props and Effects; a clock
        /// that runs back (a replay) starts the trails over.</summary>
        public void Blade(float t,int hand,Vector3 root,Vector3 tip,bool shown)
        {
            if(hand<0||hand>1)return;
            if(t<heldClock-1e-4f){heldCount[0]=heldCount[1]=0;lastAt[0]=lastAt[1]=-100;}
            heldClock=t;
            if(!shown||!Finite(root)||!Finite(tip)){heldCount[hand]=0;return;}
            lastRoot[hand]=root;lastTip[hand]=tip;lastAt[hand]=t;
            int c=heldCount[hand],h=heldHead[hand],o=hand*Samples;
            if(c>0&&t-heldAt[o+h]<1/240f){heldRoot[o+h]=root;heldTip[o+h]=tip;return;}
            h=(h+1)%Samples;heldRoot[o+h]=root;heldTip[o+h]=tip;heldAt[o+h]=t;heldHead[hand]=h;heldCount[hand]=Math.Min(Samples,c+1);
        }
        private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
        /// <summary>The blade in `hand` this frame (false: none handed in for `t`).</summary>
        protected bool Held(float t,int hand,out Vector3 root,out Vector3 tip){root=lastRoot[hand];tip=lastTip[hand];return Math.Abs(lastAt[hand]-t)<1e-4f;}
        /// <summary>When the blade in `hand` was last seen, and where.</summary>
        protected float LastSeen(int hand,out Vector3 root,out Vector3 tip){root=lastRoot[hand];tip=lastTip[hand];return lastAt[hand];}
        protected int HeldCount(int hand)=>heldCount[hand];
        /// <summary>The blade in `hand` `back` samples before the newest.</summary>
        protected void HeldSample(int hand,int back,out Vector3 root,out Vector3 tip,out float at){int i=hand*Samples+((heldHead[hand]-back)%Samples+Samples)%Samples;root=heldRoot[i];tip=heldTip[i];at=heldAt[i];}
        /// <summary>The blade's trail over the last `life` s: a chain of streaks along its tip's path, hot at the head (the edge), and the
        /// blade's afterimages (from a third of the way up to the tip) at each sample and between them, faint, soft and close together, so
        /// the sweep fills as a sheet of light (separate chains part way up read as stripes in the slowed cut).</summary>
        protected void Trail(INativeEntranceFx fx,float t,int hand,float life,Vector3 tint,Vector3 hot,float width,float gain,int seed,bool low)
        {
            int c=heldCount[hand];if(c<2)return;int fill=low?0:3;
            for(int j=0;j<c-1;j++)
            {
                HeldSample(hand,j,out var r0,out var p0,out float t0);HeldSample(hand,j+1,out var r1,out var p1,out float t1);
                float age=t-t0;if(age>=life)break;float dt=Math.Max(1e-4f,t0-t1),f=1-age/life,k=f*f*gain;
                var d=p0-p1;if(d.LengthSquared()>1e-8f)fx.Streak(p0,d/dt,Vector3.Lerp(tint,hot,f)*(2.4f*k),width*(.4f+.6f*f),0,dt,0,1-f,seed+j*4);
                for(int i=0;i<fill;i++)
                {
                    float u=i/(float)fill;var r=Vector3.Lerp(r0,r1,u);var p=Vector3.Lerp(p0,p1,u);var a=Vector3.Lerp(r,p,.33f);var ax=p-a;if(ax.LengthSquared()<1e-6f)continue;
                    fx.Streak(p,ax*10,tint*(.75f*k),width*2.4f,0,.1f,0,1-f,seed+j*4+1+i);
                }
            }
        }
        /// <summary>The turn that lays the stage's sphere (its poles on y) face-on to `d`.</summary>
        protected static Quaternion FaceOn(Vector3 d){var axis=new Vector3(d.Z,0,-d.X);float s=axis.Length();return s<1e-5f?(d.Y<0?Quaternion.CreateFromAxisAngle(Vector3.UnitX,(float)Math.PI):Quaternion.Identity):Quaternion.CreateFromAxisAngle(axis/s,(float)Math.Acos(Math.Clamp(d.Y,-1f,1f)));}
        /// <summary>The turn that lays a piece's z along `d`.</summary>
        protected static Quaternion Along(Vector3 d){var axis=new Vector3(-d.Y,d.X,0);float s=axis.Length();return s<1e-5f?(d.Z<0?Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.PI):Quaternion.Identity):Quaternion.CreateFromAxisAngle(axis/s,(float)Math.Acos(Math.Clamp(d.Z,-1f,1f)));}
    }

    /// <summary>
    /// "Crimson Moon", the Katana's entrance. 7.2 s, the name at 5.4 s. Night; a crimson moon low behind the pilot, petals drifting, a dark mist
    /// over the floor. The pilot stands with the blade sheathed at the hip, settles, and draws: one great iai cut (slowed at its height, its
    /// sweep a crimson trail) leaves a line of light hanging across the stage, the petals it crossed cut in two and still. The blade is slid
    /// home; at the click the line flares and shatters, the cut petals fall apart, the moon flares. The hero: standing calm, the hand on the hilt.
    /// </summary>
    public sealed class NativeKatanaIntro:NativeBladeIntro
    {
        public const string Name="Crimson Moon",Music="audio/intros/arc-salute",Blurb="Under a crimson moon: one draw, a line of light, and the click that splits it.";
        public const float Total=7.2f,CardAt=5.4f,End=6.9f;
        public const float Poise=.9f,DrawAt=1.25f,CutAt=2.55f,HoldTo=3.4f,SheatheAt=3.3f,ClickAt=4.35f,Hero=4.7f;
        /// <summary>When the iai's hand takes the blade out of the saya (the pack's draw, NativeKatanaSaya.IaiDraw, on the clip's own pace).</summary>
        /// <summary>The iai on the pack's clip (owner 2026-11-11: "They don't pull sword out until animation is done essentially"): its stance
        /// from DrawAt (the left hand pulling the saya forward), the blade taken out of the saya on GrabAt (the clip's 0.6 s) and eased out of it
        /// as the hand gathers, and its draw-out, the clip's last tenth of a second before the cut, played 3.5 times slower from WhipAt, so the
        /// blade is seen leaving the saya before it cuts on CutAt (it flashed out in 0.1 s at the clip's pace).</summary>
        public const float GrabAt=1.75f,WhipAt=CutAt-.35f;
        private const float IaiCut=1.3f,IaiWhip=1.2f,IaiEnd=2.05f,SheatheEnd=2.3f;
        public override string Spec=>NativeKatana.Spec;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        private static readonly Vector3 Moon=new Vector3(8f,4.8f,-10f),White=new Vector3(1,1,1),Crimson=NativeEntranceFx.Hex(0xff3d6e),Blossom=NativeEntranceFx.Hex(0xffb3c6),Night=NativeEntranceFx.Hex(0x14101e),Mist=NativeEntranceFx.Hex(0x5a4f66);
        /// <summary>The line the cut leaves, across the stage at its height (stage frame).</summary>
        public static readonly Vector3 LineFrom=Local(-2.6f,1.25f,.9f),LineTo=Local(2.8f,1.05f,.35f);
        /// <summary>The moon's disc (its radius, m) turned face-on to the stage; three turns of the sphere about its own axis keep its edge round.</summary>
        private const float MoonRadius=1.05f;private static readonly Quaternion MoonFace=FaceOn(Vector3.Normalize(new Vector3(0,1.5f,4)-Moon));
        private static readonly Quaternion[] MoonTurns={MoonFace,MoonFace*Quaternion.CreateFromAxisAngle(Vector3.UnitY,.2618f),MoonFace*Quaternion.CreateFromAxisAngle(Vector3.UnitY,.5236f)};
        private static readonly Vector3 LineDir=Vector3.Normalize(LineTo-LineFrom),CutNormal=Vector3.Normalize(Vector3.UnitY-LineDir*Vector3.Dot(Vector3.UnitY,LineDir));
        private static readonly Quaternion LineTurn=Along(LineDir);
        private const int Petals=40,PetalsLow=16,CutPetals=9;private const float PetalLife=5.5f;
        public NativeKatanaIntro():base(NativeKatana.Entrance,CutAt,0xff3d6e)
        {
            SlowRate=.25f;SlowHold=.18f;SlowRecover=.3f;SlowCatchUp=.5f;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.25f,0,-.2f);
            k.Key(CutAt,stand).Root(0,.84f,.1f,Facing).Planted(1).Drop(-.08f).J(B,.1f).J(RA,-1.4f,0,-.6f).J(LA,-.4f,0,.5f).ImpactKey();
            k.Key(Total,stand).Root(0,.88f,.1f,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.3f,0,-.2f);Keys=k;
            Shots=new[]
            {
                // The moon over the pilot's shoulder, slow push in.
                Shot(0,NativeEntranceCut.Cut,46,40,V(-1.4f,1.3f,4.8f),V(-1.1f,1.35f,4.2f),V(.6f,1.9f,-3f),V(.2f,1.6f,-1f),1,0),
                // Close on the hand at the hilt as it settles.
                Shot(Poise,NativeEntranceCut.Cut,30,28,V(1.1f,1.05f,1.5f),V(1.0f,1.05f,1.35f),V(-.1f,.95f,0),V(-.1f,.95f,0),1,0),
                // Low on the pilot's left, the saya's side, for the draw: the pack's iai stance winds the body away from the front, so from
                // ahead and to the left (and wide: the pilot small) the body hid the blade leaving the saya at the hip.
                Shot(DrawAt+.35f,NativeEntranceCut.Cut,40,36,V(-2.25f,.8f,.15f),V(-2f,.85f,.12f),V(.05f,.92f,.2f),V(.25f,.95f,.2f),1,0),
                // Behind the line as it hangs.
                Shot(CutAt+.35f,NativeEntranceCut.Whip,44,40,V(2.9f,1.6f,2.9f),V(2.6f,1.5f,2.6f),V(-.4f,1.15f,.2f),V(0,1.1f,0),1,0,.1f),
                // The click and the split, close and in front.
                Shot(SheatheAt,NativeEntranceCut.Cut,40,36,V(-.9f,1.05f,3.2f),V(-.75f,1.08f,2.9f),V(0,1.05f,0),V(0,1.1f,0),1,0),
                // The hero.
                Shot(Hero,NativeEntranceCut.Blend,38,34,V(-1.2f,1.1f,3.7f),V(-.9f,1.12f,3.3f),V(0,1.2f,0),V(0,1.22f,0),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.04f,3.5f),V(-.66f,1.06f,3.25f),V(-.08f,1.24f,0),V(-.1f,1.26f,0),1,0),
            };
        }
        public override bool Clip(float t,out string clip,out float time)
        {
            const string K="Frank_RPG_Katana_";
            // Sheathed and still (the left hand on the saya), then the iai: the pack's stance-three draw, the left hand pulling the saya
            // forward, the blade drawn out of it on GrabAt and its cut on CutAt; then the whole sheathing: the blade turned over in the hand,
            // the saya taken up by the left hand, the blade slid home on the click (ClickAt; Core NativeKatanaSaya), calm.
            if(t<DrawAt){clip=K+"Unequip_Idle";time=t+.5f;return true;}
            if(t<SheatheAt){clip=NativeBladeMotion.KatanaIai;time=t<GrabAt?Fit(t,DrawAt,GrabAt,WhipAt,0,NativeKatanaSaya.IaiDraw,IaiWhip):t<WhipAt?Fit(t,GrabAt,WhipAt,CutAt,NativeKatanaSaya.IaiDraw,IaiWhip,IaiCut):Fit(t,WhipAt,CutAt,SheatheAt,IaiWhip,IaiCut,IaiEnd);return true;}
            if(t<Hero){clip=NativeBladeMotion.KatanaSheathe;time=Fit(t,SheatheAt,ClickAt,Hero,0,NativeKatanaSaya.SheatheClick,SheatheEnd);return true;}
            clip=K+"Unequip_Idle";time=1.2f+(t-Hero);return true;
        }
        public override bool Sheathed(float t)=>t<GrabAt||t>=ClickAt-.02f;
        // Slower into the draw (the left hand takes the saya, the body sinks into the stance) and into the sheathing.
        public override float Fade(float t)=>t>=DrawAt-.02f&&t<DrawAt+.45f?.3f:t>=SheatheAt-.02f&&t<SheatheAt+.35f?.22f:.12f;
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,CutAt,.01f,.35f)*.03f+NativeEntranceMath.Pulse(t,ClickAt,.01f,.3f)*.02f;
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            // Moonlight: a crimson key from behind, a cold fill; the cut and the click flash white and crimson.
            float up=NativeEntranceMath.Ramp(t,Hero-.3f,CardAt);l.DriveProbe=true;NativeEntranceStage.Rig(l,.12f+.4f*up,Accent,.3f+.4f*up,.08f+.3f*up,.1f+.4f*up);
            l.Add(Moon*.5f+new Vector3(0,-.5f,0),Crimson*(2.4f+.8f*(float)Math.Sin(t*1.3f)),10);
            float cut=NativeEntranceMath.Pulse(t,CutAt,.01f,.5f),click=NativeEntranceMath.Pulse(t,ClickAt,.01f,.6f);
            if(cut>.01f)l.Add((LineFrom+LineTo)*.5f,(White*1.5f+Crimson)*(4*cut),6);if(click>.01f)l.Add(new Vector3(0,1.2f,0),(White+Crimson*2)*(4*click),7);
            if(t>=CardAt-.2f){float k=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(-2.2f,2.4f,2),Blossom*(1.6f*k),8);}
        }
        protected override float Pylon(int i,float t)=>t<ClickAt?.05f:PowerOn(i,t-ClickAt+.05f,.1f);
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,t<ClickAt?.03f:.1f+.25f*NativeEntranceMath.Ramp(t,ClickAt,CardAt),ClickAt,Accent);bool low=q<.5f;
            // The moon: a crisp crimson disc, flaring at the split (its halo is the effects').
            float flare=NativeEntranceMath.Pulse(t,ClickAt,.02f,.9f);var moon=(Crimson*1.9f+White*.6f)*(1.1f+1.3f*flare);
            foreach(var turn in MoonTurns)props.Piece(WeaponShape.Sphere,true,Moon,turn,new Vector3(MoonRadius,MoonRadius*.3f,MoonRadius),moon);
            // Petals drifting across the night, tumbling.
            int n=low?PetalsLow:Petals;for(int i=0;i<n;i++)
            {
                float born=NativeEntranceMath.Rand(21100,i)*PetalLife,a=((t-born)%PetalLife+PetalLife)%PetalLife/PetalLife;
                var p=new Vector3(-4.5f+9*a+NativeEntranceMath.Signed(21100,i+50)*.8f,3.2f-2.8f*a+.3f*(float)Math.Sin(t*2+i),-2.5f+5*NativeEntranceMath.Rand(21100,i+90));
                float fade=NativeEntranceMath.Clamp01(a*8)*NativeEntranceMath.Clamp01((1-a)*8);if(fade<=.01f)continue;
                props.Piece(WeaponShape.Sphere,true,p,Tumble(i,t),new Vector3(.034f,.008f,.025f)*fade,Blossom*1.15f);
            }
            // The petals the cut crosses: drifting in to the line, cut in two and held still on it, falling apart at the click.
            float click=t-ClickAt;
            if(click<=1.4f)for(int i=0;i<CutPetals;i++)
            {
                float u=(i+.5f)/CutPetals+NativeEntranceMath.Signed(21200,i)*.03f;var on=Vector3.Lerp(LineFrom,LineTo,u)+new Vector3(0,NativeEntranceMath.Signed(21210,i)*.04f,0);
                var drift=new Vector3(.5f+.2f*NativeEntranceMath.Rand(21220,i),-.3f-.15f*NativeEntranceMath.Rand(21230,i),NativeEntranceMath.Signed(21240,i)*.1f);var size=new Vector3(.034f,.008f,.025f);
                if(t<CutAt){float before=t-CutAt;if(before<-3)continue;props.Piece(WeaponShape.Sphere,true,on+drift*before+new Vector3(0,.04f*(float)Math.Sin(t*2+i),0),Tumble(i+50,t),size*NativeEntranceMath.Clamp01((before+3)*2),Blossom*1.2f);continue;}
                var turn=Tumble(i+50,CutAt);var half=size*new Vector3(1,1,.55f);
                if(click<0){float apart=.006f+.012f*NativeEntranceMath.Ramp(t,CutAt,ClickAt);for(int side=-1;side<=1;side+=2)props.Piece(WeaponShape.Sphere,true,on+CutNormal*(apart*side),turn,half,Blossom*1.3f);continue;}
                float g=1-click/1.4f;
                for(int side=-1;side<=1;side+=2)props.Piece(WeaponShape.Sphere,true,on+CutNormal*(side*(.02f+.5f*click))+drift*click+new Vector3(0,-1.6f*click*click,0),Quaternion.CreateFromAxisAngle(CutNormal,side*click*9)*turn,half*g,Blossom*1.3f);
            }
            // The line of light: drawn by the cut, held trembling and brightening toward the click, flaring and gone at the split; then its shards.
            float s=t-CutAt;
            if(s>=-.05f&&click<.1f)
            {
                float grow=NativeEntranceMath.Clamp01((s+.05f)/.12f),near=NativeEntranceMath.Ramp(t,SheatheAt,ClickAt),len=Vector3.Distance(LineFrom,LineTo)*grow,burst=click>=0?1-click/.1f:0;
                float w=.007f+.007f*near+.03f*burst;var at=LineFrom+LineDir*(len*.5f)+new Vector3(0,.004f*near*(float)Math.Sin(t*97),0);
                props.Piece(WeaponShape.Cube,true,at,LineTurn,new Vector3(w,w,len),(White*(2.2f+2.4f*near)+Crimson*(1.4f+1.4f*near))*(1+3*burst));
            }
            if(click>=0&&click<1.1f)
            {
                float u=click/1.1f;int m=low?12:28;
                for(int i=0;i<m;i++)
                {
                    var at=Vector3.Lerp(LineFrom,LineTo,(i+.5f)/m);var d=new Vector3(NativeEntranceMath.Signed(21450,i)*.8f,.4f+.8f*NativeEntranceMath.Rand(21460,i),NativeEntranceMath.Signed(21470,i)*.8f);
                    float size=(1-u)*(.05f+.04f*NativeEntranceMath.Rand(21480,i));var spin=Quaternion.CreateFromAxisAngle(NativeEntranceMath.Direction(21490,i),click*(7+6*NativeEntranceMath.Rand(21495,i)));
                    props.Piece(WeaponShape.Cube,true,at+d*(1.3f*click)+new Vector3(0,-2.2f*click*click,0),spin*LineTurn,new Vector3(size*.22f,size*.22f,size*2),(White*1.4f+Crimson*2.4f)*(1.5f*(1-u)));
                }
            }
        }
        // A petal's slow tumble.
        private static Quaternion Tumble(int i,float t)=>Quaternion.CreateFromAxisAngle(NativeEntranceMath.Direction(21300,i),t*(1.2f+1.6f*NativeEntranceMath.Rand(21310,i))+i);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;bool low=q<.5f;
            // The moon's halo and corona round its disc (Props), swelling at the split; dark cloud drifting across its face.
            float flare=NativeEntranceMath.Pulse(t,ClickAt,.02f,.9f);
            fx.Glow(Moon,Crimson*(.5f+1.1f*flare),0,6.5f+1.5f*flare,0,0,21000);fx.Glow(Moon,(Crimson*.8f+White*.2f)*(.9f+1.6f*flare),0,3.1f,1,0,21001);
            int clouds=low?1:3;for(int i=0;i<clouds;i++){float a=((t*.05f+NativeEntranceMath.Rand(21020,i))%1+1)%1;var p=Moon+new Vector3(-3.4f+6.8f*a,-.6f+.9f*NativeEntranceMath.Rand(21030,i),1.6f);fx.Smoke(p,Night*1.3f,.55f*(float)Math.Sin(Math.PI*a),1.9f+.6f*NativeEntranceMath.Rand(21040,i),a,21050+i,-100);}
            // The draw: the blade's crimson trail through the cut, a glint racing along its edge, a flash where it cuts.
            Trail(fx,t,0,.42f,Crimson,White,.03f,1,21700,low);
            if(Held(t,0,out _,out var tip))
            {
                float glint=NativeEntranceMath.Ramp(t,CutAt-.25f,CutAt-.05f)*(1-NativeEntranceMath.Ramp(t,CutAt+.3f,CutAt+.6f));if(glint>.01f)fx.Glow(tip,White*(2.6f*glint),0,.14f,1,0,21360);
                float s=t-CutAt;if(s>=0&&s<.25f){float u=s/.25f;fx.Glow(tip,(White*2+Crimson)*(3*(1-u)),0,.35f+.5f*u,1,u,21361);}
            }
            // The line's glow round its crisp core (Props): long overlapping streaks, so it reads as one line, not a dashed one.
            {
                float s=t-CutAt;
                if(s>=-.05f&&t<ClickAt)
                {
                    float grow=NativeEntranceMath.Clamp01((s+.05f)/.12f),near=NativeEntranceMath.Ramp(t,SheatheAt,ClickAt);int segs=low?3:6;
                    for(int i=0;i<segs;i++){float u0=i/(float)segs,u1=Math.Min(grow,(i+1.35f)/segs);if(u0>=grow)break;var a=Vector3.Lerp(LineFrom,LineTo,u0);var b=Vector3.Lerp(LineFrom,LineTo,u1);float edge=1-Math.Abs((u0+u1)*.5f-.5f)*.9f;fx.Streak(b,(b-a)*10,Crimson*((.7f+1.1f*near)*edge),.08f+.06f*near,0,.1f,0,0,21300+i);}
                }
                // The split: motes off the shattered line, rising and drifting.
                float c=t-ClickAt;
                if(c>=0&&c<1.5f)
                {
                    float u=c/1.5f;int m=low?16:40;
                    for(int i=0;i<m;i++){float w=NativeEntranceMath.Rand(21400,i);var p=Vector3.Lerp(LineFrom,LineTo,w);var d=new Vector3(NativeEntranceMath.Signed(21400,i+40),.4f+NativeEntranceMath.Rand(21400,i+80),NativeEntranceMath.Signed(21400,i+120));fx.Glow(p+d*(1.1f*c)+new Vector3(0,-.5f*c*c,0),(i%3==0?Blossom:Crimson)*(2*(1-u)),0,.035f,3,u,21410+i);}
                    // The click: a short hot flash at the saya's mouth (where the hilt went home) and a shockwave of light running out over the floor.
                    if(c<.5f)
                    {
                        float v=c/.5f;var hip=LastSeen(0,out var hilt,out _)>ClickAt-1.2f?hilt:Local(.15f,.95f,.05f);if(c<.25f)fx.Glow(hip,(White*2+Crimson)*(2.6f*(1-c/.25f)),0,.16f+.2f*c/.25f,1,c/.25f,21580);
                        int r=low?18:40;for(int i=0;i<r;i++){float a=i*6.2831853f/r;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(new Vector3(0,floor+.03f,0)+d*(.3f+3.4f*(1-(1-v)*(1-v))),d*5,Crimson*(2*(1-v)),.025f,.15f,.03f,.7f,v,21520+i);}
                    }
                }
            }
            // A dark mist low over the floor all through (lit smoke: the moon's crimson on it).
            int mist=low?5:12;for(int i=0;i<mist;i++){float a=((t*.06f+NativeEntranceMath.Rand(21600,i))%1+1)%1;var p=new Vector3(-4.5f+9*NativeEntranceMath.Rand(21600,i+20)+1.2f*(a-.5f),floor+.14f+.08f*a,-3+6*NativeEntranceMath.Rand(21600,i+40));fx.Smoke(p,Mist,.36f*(float)Math.Sin(Math.PI*a),1.5f+.7f*a,a,21610+i,floor);}
        }
    }

    /// <summary>
    /// "Silent Kill", the Fangs' entrance. 7 s, the name at 5.2 s. Darkness and a low dark mist; two points of venom-green light, then the
    /// pilot forms out of gathering smoke, crouched. A shadow step: gone in a burst of smoke, back a stride ahead. The twin daggers in a flurry
    /// (green trails, venom flung off them), a leap and a two-handed strike into the floor: the venom blooms, a shockwave of green, a splash
    /// and a billow of smoke. The hero: the daggers held low, venom dripping from both, smoke curling round the legs.
    /// </summary>
    public sealed class NativeFangsIntro:NativeBladeIntro
    {
        public const string Name="Silent Kill",Music="audio/intros/pulse-wave",Blurb="Out of the smoke: a shadow step, a flurry of fangs and a bloom of venom.";
        public const float Total=7f,CardAt=5.2f,End=6.7f;
        public const float FormAt=.55f,StepAt=1.6f,ArriveAt=1.85f,FlurryAt=1.95f,StrikeAt=4.25f,Hero=4.65f;
        public override string Spec=>NativeFangs.Spec;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        private static readonly Vector3 Venom=NativeEntranceFx.Hex(0x7dff3a),White=new Vector3(1,1,1),Smoke=NativeEntranceFx.Hex(0x5c6660),Haze=NativeEntranceFx.Hex(0x46524a);
        // The venom's hot edge (a pale yellow green, where plain white read as any blade's spark) and its fumes' body.
        private static readonly Vector3 Bile=NativeEntranceFx.Hex(0xd9ff8a),Fumes=NativeEntranceFx.Hex(0x3f9a30),Across=Local(1,0,0);
        // The flurry's and the strike's own travel (owner 2026-11-13: "This applies to the intro too (They flip in place with no movement)"):
        // the in-place takes leave their roots' way out, so the pilot shown carries it. The flurry (the assassin's fifth combo) leaps: its
        // lowest point (feet, shins, hips, chest, head, hands, as the model's contacts) up to 1.78 m off the floor through its somersaults,
        // and drifts 12 cm back; the strike (the two-handed Skill02) lunges 1.26 m on and steps back 0.53. Both read from the root-motion
        // takes in the FBX (the game body's 0.835 of the pack's skeleton), 30 a second from the clip times the script starts them at. The
        // way ends Travel m on from where the shadow step comes out of the smoke, so the step comes out that far short of the hero's spot
        // (Arrive): the pilot ends where play starts, with no snap and no slide.
        private static readonly float[] FlurryWay={0,-.033f,-.056f,-.059f,-.055f,-.049f,-.04f,-.03f,-.018f,-.006f,.005f,.016f,.025f,.033f,.037f,.038f,.039f,.04f,.041f,.041f,.041f,.041f,.041f,.04f,.039f,.038f,.035f,.032f,.027f,.021f,.015f,.007f,-.001f,-.01f,-.019f,-.028f,-.038f,-.047f,-.057f,-.066f,-.076f,-.085f,-.093f,-.101f,-.108f,-.114f,-.119f,-.122f,-.124f},
            FlurryLift={0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,.091f,.778f,1.09f,1.625f,1.779f,1.672f,1.568f,1.545f,1.575f,1.633f,1.648f,1.652f,1.669f,1.701f,1.611f,1.429f,1.118f,.73f,.242f,0,0,0,0};
        private const float FlurryFrom=.35f,FlurryHit=1.217f,FlurryTo=1.95f,StrikeFrom=NativeBladeMotion.BloomWayFrom,StrikeHit=.9f,StrikeTo=1.6f;
        /// <summary>How far the flurry and the strike carry the pilot on (m, along its facing).</summary>
        public static readonly float Travel=FlurryWay[FlurryWay.Length-1]+NativeBladeMotion.Sample(NativeBladeMotion.BloomWay,StrikeFrom,StrikeTo);
        public static readonly Vector3 Before=Local(0,0,-1.4f),After=Local(0,0,0),Arrive=Local(0,0,-Travel);
        /// <summary>The flurry's cuts (when venom flies off the blades, if the runtime has not handed them in).</summary>
        private static readonly float[] Cuts={FlurryAt+.45f,FlurryAt+.85f,FlurryAt+1.45f};
        /// <summary>Where the strike lands (the pilot's spot on the floor at its blow).</summary>
        public static readonly Vector3 StrikeSpot=new Vector3(Way(StrikeAt).X,0,Way(StrikeAt).Z);
        private static float FlurryClip(float t)=>Fit(t,FlurryAt,FlurryAt+.85f,StrikeAt-.45f,FlurryFrom,FlurryHit,FlurryTo);
        private static float StrikeClip(float t)=>Fit(t,StrikeAt-.45f,StrikeAt,Hero+.3f,StrikeFrom,StrikeHit,StrikeTo);
        /// <summary>The pilot's way at `t` from the keyed root (the stage's frame): through the shadow step back by Travel (unseen), then on
        /// along the flurry's and the strike's roots and up the flurry's leap, ending on the hero's spot.</summary>
        public static Vector3 Way(float t)
        {
            if(t<StepAt)return Vector3.Zero;
            if(t<FlurryAt)return Local(0,0,-Travel*NativeEntranceMath.Clamp01((t-StepAt)/(ArriveAt-StepAt)));
            float on,up=0;
            if(t<StrikeAt-.45f){float c=FlurryClip(t);on=NativeBladeMotion.Sample(FlurryWay,FlurryFrom,c);up=NativeBladeMotion.Sample(FlurryLift,FlurryFrom,c);}
            else on=FlurryWay[FlurryWay.Length-1]+NativeBladeMotion.Sample(NativeBladeMotion.BloomWay,StrikeFrom,t<Hero+.3f?StrikeClip(t):StrikeTo);
            return Local(0,up,on-Travel);
        }
        /// <summary>The pilot's spot on the floor at `t` (stage frame; After from the strike's step back on).</summary>
        private static Vector3 Feet(float t){var w=Way(t);return After+new Vector3(w.X,0,w.Z);}
        private static NativeEntranceShot Following(NativeEntranceShot s,NativeEntranceAnchor a){s.Follow=(int)a;return s;}
        public NativeFangsIntro():base(NativeFangs.Entrance,StrikeAt,0x7dff3a)
        {
            SlowRate=.3f;SlowHold=.14f;SlowRecover=.25f;SlowCatchUp=.6f;RootPath=(t,r)=>r+Way(t);
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(Before.X,.7f,Before.Z,Facing).Planted(1).Drop(-.18f).J(B,.25f).J(LA,-.5f,0,.4f).J(RA,-.5f,0,-.4f);
            k.Key(StepAt,stand).Root(Before.X,.7f,Before.Z,Facing).Planted(1).Drop(-.18f).J(B,.25f);
            k.Key(ArriveAt,stand).Root(After.X,.78f,After.Z,Facing).Planted(1).Drop(-.1f).J(B,.15f);
            k.Key(Total,stand).Root(After.X,.84f,After.Z,Facing).Planted(1).Drop(-.04f).J(B,.08f).J(LA,-.3f,0,.3f).J(RA,-.3f,0,-.3f);Keys=k;
            Shots=FangsShots();
        }
        /// <summary>Unseen until it forms out of the smoke; gone through the step.</summary>
        /// <summary>The camera (owner 2026-11-13: more camera work on the Fangs' intro), a shot to each beat, in the stage's own axes:
        /// creeping low toward the eyes with the frame tilted; climbing round the body as the smoke winds into it; from behind its
        /// shoulder for the step, so it is seen to go and to be there ahead; whipped round to its front as it lands and circling close
        /// through the flurry on the floor; cut low and wide as it leaps, the whole leap held in one still frame from the floor (the take
        /// launches and drops in a tenth of a second: a look that chased it would whip); in profile for the strike's lunge, which crosses
        /// the frame; straight down on the strike (the cut on the blow itself), lifting and turning over the bloom as it opens; then one
        /// move down through the fumes to the hero.</summary>
        private static NativeEntranceShot[] FangsShots()=>new[]
        {
            Shot(0,NativeEntranceCut.Cut,36,33,V(1.1f,.42f,4.6f),V(.7f,.5f,3.5f),V(0,1f,-1.4f),V(0,1.1f,-1.4f),2,0,roll0:.07f,roll1:.03f),
            Arc(FormAt,NativeEntranceCut.Cut,38,33,V(1.9f,.5f,.4f),V(1.6f,.75f,.9f),V(.9f,1f,1.2f),V(0,.8f,-1.4f),V(0,.95f,-1.4f),0,0),
            Shot(StepAt-.2f,NativeEntranceCut.Cut,46,42,V(-.9f,1.45f,-3.6f),V(-.7f,1.35f,-3.2f),V(0,.9f,-1f),V(Arrive.X,.9f,Arrive.Z),1,0),
            Shot(ArriveAt+.05f,NativeEntranceCut.Whip,40,36,V(1.9f,.9f,1.7f),V(.3f,1.1f,2.2f),V(Arrive.X,1f,Arrive.Z),V(Arrive.X,1f,Arrive.Z),1,0,.18f),
            Shot(LeapAt,NativeEntranceCut.Cut,52,50,V(-2.3f,.3f,1.9f),V(-2.05f,.32f,2.1f),V(Arrive.X,1.55f,Arrive.Z),V(Arrive.X,1.62f,Arrive.Z),2,0),
            Shot(StrikeAt-.5f,NativeEntranceCut.Cut,46,42,V(3.6f,.6f,-.1f),V(3.4f,.62f,.1f),V(0,.95f,-.55f),V(0,.9f,.15f),2,0),
            Shot(StrikeAt,NativeEntranceCut.Cut,46,56,StrikeSpot+V(.25f,2.7f,.9f),StrikeSpot+V(.35f,3f,1.1f),StrikeSpot+V(0,.2f,.3f),StrikeSpot+V(0,.2f,.3f),1,0,roll0:0,roll1:.3f),
            Shot(StrikeAt+.15f,NativeEntranceCut.Blend,40,36,V(-1.05f,.95f,3.4f),V(-.85f,1f,3.25f),V(0,1.05f,0),V(-.05f,1.08f,0),1,0,.8f),
            Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.8f,.98f,3.45f),V(-.64f,1.01f,3.2f),V(-.08f,1.12f,0),V(-.1f,1.14f,0),1,0),
        };
        /// <summary>When the flurry leaves the floor (its second cut; the leap is the clip's own, FlurryLift).</summary>
        public const float LeapAt=FlurryAt+.85f;
        /// It condenses out of the smoke (dithered in over FormFade, the whole body as one: the reveal's segments are the keyed stance's, not
        /// the clip's), thins away into the step and snaps back out of it.
        public override float Solid(float t,int bone)=>t<StepAt?NativeEntranceMath.Clamp01((t-FormAt)/FormFade):t<ArriveAt-.03f?1-NativeEntranceMath.Clamp01((t-StepAt)/.04f):NativeEntranceMath.Clamp01((t-(ArriveAt-.03f))/.05f);
        public const float FormFade=.3f;
        public override bool Clip(float t,out string clip,out float time)
        {
            const string A="Frank_RPG_Assassin_";
            if(t<StepAt){clip="Frank_Stealth_Kill_Sneak_Idle";time=t;return true;}
            if(t<FlurryAt){clip=A+"Evade_F_IP";time=Fit(t,StepAt,ArriveAt,FlurryAt,.05f,.35f,.5f);return true;}
            // The flurry (the pack's fifth combo, its three cuts), then the two-handed strike (its blow on StrikeAt), then the stance.
            if(t<StrikeAt-.45f){clip=A+"Combo05_All_IP";time=FlurryClip(t);return true;}
            if(t<Hero+.3f){clip=A+"Skill02_IP";time=StrikeClip(t);return true;}
            clip=A+"Equip_Idle";time=t-Hero;return true;
        }
        /// <summary>The player's Camera shake setting (Runtime DirectedIntro sets it each frame): off, the entrance's camera takes no jolt.</summary>
        public bool Shake=true;
        public override Vector3 CameraJolt(float t)
        {
            // The strike, the step's arrival, and a tick on each of the flurry's cuts; none with the Camera shake setting off.
            if(!Shake)return Vector3.Zero;
            float s=NativeEntranceMath.Pulse(t,StrikeAt,.01f,.4f)*.04f+NativeEntranceMath.Pulse(t,ArriveAt,.01f,.2f)*.012f;
            foreach(float cut in Cuts)s+=NativeEntranceMath.Pulse(t,cut,.01f,.12f)*.006f;
            return s<1e-4f?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float up=NativeEntranceMath.Ramp(t,StrikeAt,CardAt);l.DriveProbe=true;NativeEntranceStage.Rig(l,.05f+.45f*up,Accent,.25f+.5f*up,.06f+.35f*up,.08f+.45f*up);
            l.Add(new Vector3(0,.3f,0),Venom*(.8f+.4f*(float)Math.Sin(t*2.1f)),5);
            // A cold rim from behind once it has formed, so the silhouette reads against the dark; it flares on the step's two bursts and on
            // each cut of the flurry.
            {
                float rim=.5f*NativeEntranceMath.Ramp(t,FormAt,FormAt+.5f)*(1-.6f*up)+1.6f*NativeEntranceMath.Pulse(t,StepAt,.01f,.18f)+1.6f*NativeEntranceMath.Pulse(t,ArriveAt-.03f,.01f,.22f);
                foreach(float cut in Cuts)rim+=1.1f*NativeEntranceMath.Pulse(t,cut,.01f,.16f);
                var at=t<StepAt+.1f?Before:Feet(t);if(rim>.01f)l.Add(at+Local(-.9f,1.7f,-1.5f),(Venom*.7f+Bile*.3f)*rim,6);
            }
            float strike=NativeEntranceMath.Pulse(t,StrikeAt,.01f,.8f);if(strike>.01f)l.Add(StrikeSpot+new Vector3(0,.5f,.4f),(Venom*2+White)*(4*strike),8);
            // The bloom's own light: the fumes' green from under them, dying with them.
            {float s=t-StrikeAt;if(s>0&&s<BloomLife)l.Add(StrikeSpot+Local(0,.45f,.35f),Venom*(1.7f*NativeEntranceMath.Smooth(s*6)*(1-s/BloomLife)),6);}
            if(t>=CardAt-.2f){float k=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(2.2f,2.2f,1.8f),Venom*(1.8f*k),8);}
        }
        /// <summary>How long the strike's bloom lasts (s; its veins the longest), and its fumes: whole until BloomHang, thinned away by
        /// BloomGone, which is the name's moment (they open between the hero's camera and the pilot, and must not veil the name).</summary>
        public const float BloomLife=1.9f,BloomHang=.45f,BloomGone=CardAt-StrikeAt;
        protected override float Pylon(int i,float t)=>t<StrikeAt?.04f:PowerOn(i,t-StrikeAt+.05f,.12f);
        // The blade tip in `hand` now: the model's (handed in), else the keyed stance's hand.
        private Vector3 Tip(float t,int hand){if(Held(t,hand,out _,out var tip))return tip;return Anchor(hand==0?NativeEntranceAnchor.RightTip:NativeEntranceAnchor.LeftTip,t);}
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            Stage(props,t,t<StrikeAt?.02f:.08f+.3f*NativeEntranceMath.Ramp(t,StrikeAt,CardAt),StrikeAt,Accent);bool low=q<.5f;
            // Venom dripping from both blades from the strike on: beads that swell at the tip and fall.
            if(t<StrikeAt)return;int m=low?2:4;const float Period=.8f;
            for(int hand=0;hand<2;hand++)
            {
                var tip=Tip(t,hand);
                for(int i=0;i<m;i++)
                {
                    float born=StrikeAt+.1f+(i+hand*.5f)*Period/m,a=((t-born)%Period+Period)%Period/Period;if(t<born)continue;
                    float swell=NativeEntranceMath.Clamp01(a*3),fall=Math.Max(0,a-.33f)*Period,y=-.015f-4.9f*fall*fall;if(tip.Y+y<NativeEntranceStage.FloorY+.02f)continue;
                    props.Piece(WeaponShape.Sphere,true,tip+new Vector3(0,y,0),Quaternion.Identity,new Vector3(.009f,.012f+.008f*NativeEntranceMath.Clamp01(fall*8),.009f)*(.5f+.5f*swell),Venom*1.7f);
                }
            }
        }
        // Venom through the runtime's own looks when the sink has them (fumes that glow in their colour, teardrops); a plain sink draws
        // smoke and motes, one draw each either way.
        private static void Fume(INativeEntranceFx fx,Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor,float glow){if(fx is INativeEntranceVenom v)v.Fume(at,albedo,opacity,size,u,seed,floor,glow);else fx.Smoke(at,albedo,opacity,size,u,seed,floor);}
        private static void Bead(INativeEntranceFx fx,Vector3 at,Vector3 velocity,Vector3 color,float width,float u,float seed){if(fx is INativeEntranceVenom v)v.Bead(at,velocity*.03f,color,.92f,width,u,seed);else fx.Glow(at,color,0,width*1.3f,0,u,seed);}
        /// <summary>A glint running up the blade in `hand` from `at` for `life` s: a four-point star that swells and dies as it reaches the tip
        /// (nothing while the blade is not handed in).</summary>
        private void BladeGlint(INativeEntranceFx fx,float t,float at,float life,int hand,float size,int seed)
        {
            float s=t-at;if(s<0||s>=life||!Held(t,hand,out var root,out var tip))return;float u=s/life,k=(float)Math.Sin(Math.PI*u);
            NativeEntranceFx.Glint(fx,Vector3.Lerp(root,tip,.2f+.8f*NativeEntranceMath.Smooth(u)),(Venom+Bile)*(1.5f*k),size*(.35f+.65f*k),Vector3.UnitY,Across,seed);
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;bool low=q<.5f;
            // The low mist: dark smoke over the floor (the venom light under it), and a faint green breath at the floor.
            int mist=low?6:14;for(int i=0;i<mist;i++){float a=((t*.08f+NativeEntranceMath.Rand(22000,i))%1+1)%1;var p=new Vector3(-4+8*NativeEntranceMath.Rand(22000,i+20)+.8f*(a-.5f),floor+.12f+.12f*a,-3+6*NativeEntranceMath.Rand(22000,i+40));fx.Smoke(p,Haze,.4f*(float)Math.Sin(Math.PI*a),1.3f+.6f*a,a,22010+i,floor);}
            for(int i=0;i<(low?2:4);i++){var p=new Vector3(-2.4f+1.6f*i,floor+.05f,-1+NativeEntranceMath.Signed(22050,i));fx.Glow(p,Venom*(.05f+.02f*(float)Math.Sin(t*1.7f+i)),0,2.4f,0,0,22060+i);}
            // Venom motes adrift in the dark all through, rising slowly out of the mist.
            NativeEntranceFx.Embers(fx,t,-2.6f,Total,new Vector3(0,floor+.08f,-.5f),3.4f,low?2.5f:6,.2f,2.6f,Venom*.6f,.02f,22070,1,.25f);
            // Two eyes in the dark before it forms: slits of venom light that narrow once, as a blink, and go out as the body takes them.
            if(t<FormAt+FormFade)
            {
                float k=NativeEntranceMath.Ramp(t,.08f,.3f)*(1-NativeEntranceMath.Ramp(t,FormAt,FormAt+FormFade)),open=1-.9f*NativeEntranceMath.Bump(t,.34f,.46f);var head=Before+new Vector3(0,EyeHeight,0);
                for(int e=0;e<2;e++)
                {
                    var p=head+Local(e==0?-.05f:.05f,0,.12f);fx.Glow(p,Venom*(3*k*open),0,.05f,1,0,22100+e);
                    fx.Streak(p+Across*.075f,Across*.15f,(Venom+Bile*.5f)*(1.5f*k),.004f+.012f*open,0,1,0,0,22104+e);
                }
            }
            // It forms out of smoke: two coils winding in round it from the floor up, as a snake winds, green sparks drawn in with them; then
            // a glint up each fang as the light first finds them.
            {
                float s=t-(FormAt-.45f);
                if(s>=0&&s<1.25f)
                {
                    float k=NativeEntranceMath.EaseOut(s/.8f),fade=1-NativeEntranceMath.Ramp(s,.65f,1.25f),rise=NativeEntranceMath.Clamp01(.25f+s/.5f);int n=low?4:9;
                    for(int coil=0;coil<2;coil++)for(int i=0;i<n;i++)
                    {
                        float f=i/(float)(n-1),a=(coil==0?1:-1)*(f*5.4f+s*3.2f)+coil*3.1415927f,r=(1.3f*(1-k)+.17f)*(1-.3f*f);
                        fx.Smoke(Before+new Vector3((float)Math.Sin(a)*r,.12f+1.05f*f*rise,(float)Math.Cos(a)*r),Smoke,.62f*fade*(.6f+.4f*(1-f)),.32f+.22f*k,NativeEntranceMath.Clamp01(s/1.25f),22160+coil*16+i,floor);
                    }
                    if(s<.65f){int m=low?5:12;var c=Before+new Vector3(0,.55f,0);for(int i=0;i<m;i++){var d=NativeEntranceMath.Direction(22180,i);d.Y*=.5f;var p=c+d*(1.25f*(1-s/.65f));fx.Streak(p,-d*6,Venom*(2.2f*(1-s/.65f)),.02f,.12f,.03f,.6f,s/.65f,22190+i);}}
                }
            }
            BladeGlint(fx,t,FormAt+FormFade,.42f,0,.12f,22140);BladeGlint(fx,t,FormAt+FormFade+.12f,.42f,1,.1f,22144);
            // The step: a burst of smoke where it went, streaks of green along its way and a wake of smoke dragged low after it, the light
            // drawn in to where it comes out an instant before the burst there, and a glint off each fang as it lands.
            Burst(fx,t,StepAt,Before+new Vector3(0,.85f,0),floor,low,22200);Burst(fx,t,ArriveAt-.03f,Arrive+new Vector3(0,.85f,0),floor,low,22300);
            {
                float s=t-StepAt,span=ArriveAt-StepAt;
                if(s>=0&&s<span+.25f){float u=NativeEntranceMath.Clamp01(s/span),fade=1-NativeEntranceMath.Clamp01((s-span)/.25f);for(int i=0;i<3;i++){var from=Before+new Vector3(0,.45f+.4f*i,0);var to=Vector3.Lerp(from,Arrive+new Vector3(0,.45f+.4f*i,0),u);var d=to-from;if(d.LengthSquared()>1e-4f)fx.Streak(to,d*4,Venom*(1.6f*fade*(i==1?1:.6f)),.03f,0,.25f,.5f,1-fade,22250+i);}}
                if(s>=0&&s<StepWake)
                {
                    int n=low?3:7;
                    for(int i=0;i<n;i++)
                    {
                        float f=(i+.5f)/n,born=f*span,a=s-born;if(a<0)continue;float u=a/(StepWake-born);
                        var p=Vector3.Lerp(Before,Arrive,f)+new Vector3(.14f*NativeEntranceMath.Signed(22270,i),.2f+.4f*u,.1f*NativeEntranceMath.Signed(22274,i));fx.Smoke(p,Smoke,.5f*(1-u)*(1-u),.28f+.45f*u,u,22260+i,floor);
                    }
                }
                float g=t-(ArriveAt-.11f);
                if(g>=0&&g<.11f){float u=g/.11f;int m=low?5:10;var c=Arrive+new Vector3(0,.85f,0);for(int i=0;i<m;i++){var d=NativeEntranceMath.Direction(22280,i);d.Y*=.6f;fx.Streak(c+d*(1f*(1-u)+.12f),-d*9,Venom*(1.9f*u),.018f,.1f,.03f,.5f,u,22285+i);}}
            }
            BladeGlint(fx,t,ArriveAt+.02f,.3f,0,.14f,22292);BladeGlint(fx,t,ArriveAt+.09f,.3f,1,.12f,22296);
            // The flurry and the strike: both blades' green trails (their heads the venom's pale edge), a star riding the tip while a cut is
            // at its fastest, venom flung off the fastest of them as drops.
            Trail(fx,t,0,.32f,Venom,Bile,.022f,1,22320,low);Trail(fx,t,1,.32f,Venom,Bile,.022f,1,22520,low);
            if(HeldCount(0)>1||HeldCount(1)>1)
            {
                for(int hand=0;hand<2;hand++)
                {
                    int c=HeldCount(hand),flung=0,cap=low?5:12;
                    if(c>1&&t>=FlurryAt)
                    {
                        HeldSample(hand,0,out _,out var h0,out float a0);HeldSample(hand,1,out _,out var h1,out float a1);
                        if(t-a0<.05f){float speed=((h0-h1)/Math.Max(1e-4f,a0-a1)).Length(),k=NativeEntranceMath.Smooth((speed-CutSpeed)/CutSpeed);if(k>.01f)NativeEntranceFx.Glint(fx,h0,(Venom+Bile)*(1.3f*k),.05f+.07f*k,Vector3.UnitY,Across,22690+hand*4);}
                    }
                    for(int j=0;j+1<c&&flung<cap;j+=2)
                    {
                        HeldSample(hand,j,out _,out var p0,out float t0);HeldSample(hand,j+1,out _,out var p1,out float t1);float age=t-t0;if(age>.5f)break;
                        var v=(p0-p1)/Math.Max(1e-4f,t0-t1);if(v.LengthSquared()<36)continue;flung++;
                        var p=p0+v*(.22f*age)+new Vector3(0,.8f*age-4.9f*age*age,0);if(p.Y<floor)continue;Bead(fx,p,v*.22f+new Vector3(0,.8f-9.8f*age,0),Venom*(1.8f*(1-age/.5f)),.026f,age/.5f,22700+hand*64+j);
                    }
                }
            }
            else foreach(float cut in Cuts)
            {
                float s=t-cut;if(s<0||s>.7f)continue;int m=low?10:24;
                for(int i=0;i<m;i++){var d=new Vector3(NativeEntranceMath.Signed(22500+(int)(cut*100),i),.3f+.5f*NativeEntranceMath.Rand(22500,i),NativeEntranceMath.Signed(22540,i));var p=Feet(cut)+new Vector3(0,1.1f,0)+d*(2.2f*s)+new Vector3(0,-4.5f*s*s,0);Bead(fx,p,d*2.2f+new Vector3(0,-9*s,0),Venom*(2*(1-s/.7f)),.028f,s/.7f,22510+i+(int)(cut*1000)%400);}
            }
            // The strike: the venom blooms. A hot flash and a star at the blades; veins of venom light cracking out over the floor and
            // dying slowly; a thin wave of green racing out along it; drops splashing up; and the bloom itself, petals of glowing fumes
            // opening round the blow as a flower opens, then lifting and thinning away, dark smoke rolling out low under them.
            {
                float s=t-StrikeAt;if(s>=0&&s<BloomLife)
                {
                    float u=s/BloomLife;var at=StrikeSpot+Local(0,.25f,.35f);var on=new Vector3(at.X,floor,at.Z);
                    if(s<.22f)fx.Glow(at,(Venom*2+White)*(3.5f*(1-s/.22f)),0,.3f+.6f*s/.22f,1,s/.22f,22600);
                    if(s<.34f)NativeEntranceFx.Glint(fx,at,(Venom+Bile)*(2.6f*(1-s/.34f)),.3f+.5f*s/.34f,Vector3.UnitY,Across,22604);
                    // Veins: each a jagged run of segments growing out from the blow, its root the brightest.
                    int veins=low?5:9;float grow=NativeEntranceMath.EaseOut(s/.3f),ember=(1-u)*(1-u);
                    for(int i=0;i<veins;i++)
                    {
                        float a=i*6.2831853f/veins+.3f*NativeEntranceMath.Signed(22610,i),len=(1.4f+1.3f*NativeEntranceMath.Rand(22611,i))*grow;var from=on+new Vector3(0,.03f,0);
                        for(int j=0;j<VeinSegments;j++)
                        {
                            a+=.55f*NativeEntranceMath.Signed(22612+j,i);var to=from+new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a))*(len/VeinSegments);
                            fx.Streak(to,to-from,Vector3.Lerp(Bile,Venom,j/(float)VeinSegments)*(2.3f*ember*(1-.18f*j)),.04f*(1-.17f*j),0,1,.25f,u,22620+i*VeinSegments+j);from=to;
                        }
                    }
                    if(s<.7f){float w=s/.7f;int ring=low?14:32;for(int i=0;i<ring;i++){float a=i*6.2831853f/ring;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(on+new Vector3(0,.03f,0)+d*(.4f+4.4f*(1-(1-w)*(1-w))),d*6,Venom*(1.7f*(1-w)),.026f,.2f,.03f,.7f,w,22700+i);}}
                    if(s<.9f){int m=low?10:24;for(int i=0;i<m;i++){var d=NativeEntranceMath.Direction(22760,i,true);var v=new Vector3(d.X*2.6f,d.Y*3.4f,d.Z*2.6f);var p=at+v*s+new Vector3(0,-4.9f*s*s,0);if(p.Y<floor)continue;Bead(fx,p,v+new Vector3(0,-9.8f*s,0),Venom*(2*(1-s/.9f)),.034f,s/.9f,22800+i);}}
                    // The bloom: each petal a run of fumes from the blow outward and up, opening over half a second.
                    int petals=low?5:BloomPetals,per=low?3:5;float open=NativeEntranceMath.EaseOut(s/.55f),hang=1-NativeEntranceMath.Smooth((s-BloomHang)/(BloomGone-BloomHang));
                    if(hang<=0)petals=0;
                    for(int i=0;i<petals;i++)
                    {
                        float a=i*6.2831853f/petals+.45f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
                        for(int j=0;j<per;j++)
                        {
                            float f=(j+1f)/per,shown=NativeEntranceMath.Smooth(s*9-j*.7f);if(shown<=0)continue;
                            var p=on+d*(1.55f*(float)Math.Pow(f,1.5)*open+.25f*u*f)+new Vector3(0,.1f+1.2f*f*open+.55f*u*f,0);
                            Fume(fx,p,Fumes,.4f*shown*hang*(1-.3f*f),.34f+.5f*f*open+.5f*u,NativeEntranceMath.Clamp01(s/BloomGone),22840+i*8+j,floor,.35f);
                        }
                    }
                    NativeEntranceFx.GroundDust(fx,t,StrikeAt,on,low?6:13,.35f,3.1f,1.5f,Haze,.5f,.8f,22900,1,floor);
                }
            }
            // The hero: smoke curling round the legs, thin fumes lifting off both fangs, and a glint up each as the name lands.
            if(t>=Hero)
            {
                int sm=low?4:10;for(int i=0;i<sm;i++){float a=((t*.45f+NativeEntranceMath.Rand(22930,i))%1+1)%1,ang=i*.63f+t*.9f+a*2.2f,r=.32f+.12f*(float)Math.Sin(t+i);var p=Feet(t)+new Vector3((float)Math.Sin(ang)*r,.08f+1.1f*a,(float)Math.Cos(ang)*r);fx.Smoke(p,Smoke,.32f*(float)Math.Sin(Math.PI*a),.3f+.35f*a,a,22940+i,floor);}
                float up=NativeEntranceMath.Ramp(t,Hero,Hero+.5f);int wisps=low?2:4;const float Period=1.3f;
                for(int hand=0;hand<2;hand++)
                {
                    if(!Held(t,hand,out var root,out var tip))continue;
                    for(int i=0;i<wisps;i++)
                    {
                        float a=(((t-Hero)/Period+i/(float)wisps+hand*.37f)%1+1)%1;var p=Vector3.Lerp(root,tip,.3f+.65f*NativeEntranceMath.Rand(22960+hand,i))+new Vector3(.05f*(float)Math.Sin(a*5+i),.5f*a,.04f*(float)Math.Cos(a*4+i));
                        Fume(fx,p,Fumes,.3f*up*(float)Math.Sin(Math.PI*a),.08f+.2f*a,a,22970+hand*8+i,floor,.4f);
                    }
                }
            }
            BladeGlint(fx,t,CardAt+.04f,.5f,0,.17f,22990);BladeGlint(fx,t,CardAt+.3f,.45f,1,.13f,22994);
        }
        /// <summary>Where the eyes hang in the dark (m above the floor at Before): the crouched model's visor, measured in the Editor (the
        /// stealth idle crouches half a metre under the keyed stance's head).</summary>
        public const float EyeHeight=.76f;
        /// <summary>The step's wake of smoke (s), the blade speed a cut's star needs (m/s), and the strike's veins and petals.</summary>
        public const float StepWake=.9f,CutSpeed=7f;public const int VeinSegments=4,BloomPetals=7;
        // A burst of smoke (the step's): dark puffs thrown out and rising, green sparks out of it, a short green flash at its heart.
        private static void Burst(INativeEntranceFx fx,float t,float at,Vector3 c,float floor,bool low,int seed)
        {
            float s=t-at;if(s<0||s>.9f)return;float u=s/.9f;int n=low?7:16;
            for(int i=0;i<n;i++){var d=NativeEntranceMath.Direction(seed,i);d.Y*=.6f;var p=c+d*(.2f+.8f*NativeEntranceMath.EaseOut(u))+new Vector3(0,.4f*u,0);fx.Smoke(p,Smoke,.7f*(1-u)*(1-u),.35f+.5f*u,u,seed+i,floor);}
            if(s<.35f){int m=low?5:10;for(int i=0;i<m;i++){var d=NativeEntranceMath.Direction(seed+40,i);fx.Streak(c+d*(.15f+1.3f*s),d*7,Venom*(2*(1-s/.35f)),.02f,.1f,.03f,.6f,s/.35f,seed+50+i);}}
            if(s<.15f)fx.Glow(c,Venom*(2.6f*(1-s/.15f)),0,.3f+.3f*s/.15f,1,s/.15f,seed+90);
        }
    }
    /// <summary>Silent Kill's venom, for a sink that can draw it (the runtime's; a checker's need not): fumes, a soft vapour that glows in
    /// its own colour (`glow` 0 to about .5; the toon smoke's hard lobes read as green shrubs in the Editor), and teardrops (`segment` the drop's tail; `coverage` 0 adds it).</summary>
    public interface INativeEntranceVenom
    {
        void Fume(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor,float glow);
        void Bead(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed);
    }
}
