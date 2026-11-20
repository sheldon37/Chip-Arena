using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The four magic weapons' v2 intros (owner 2026-11-20: "a much higher quality standard, great animations and effects,
    /// non-block based background and designs ... With great camera work as well!"; docs/INTROS-ELEMENTAL.md). Each summons its
    /// element on its own modelled set (the local bank ChipArenaIntroElemental, tools/intros/elemental_set.py; the framework's
    /// dark lit void without it): Forgeborn (fire), Still Tide (water), Strike Point (lightning), Black Sun (darkness). They
    /// replace Cinder Crown, Tidal Serpent, Thunderstep and Eventide Gate, which stay archived in NativeEntranceWeapons.
    /// The shared parts: the staff in the left hand placed by arm IK on a keyed grip track (the staff is where the effects
    /// start), long streaks laid on their head's camera plane, the set's pieces by name (no strings built per frame).
    /// Presentation only: pure functions of the intro's presentation clock, allocation-free per frame.
    /// </summary>
    public abstract class NativeIntroElementalScript:NativeIntroV2Script,INativeIntroHands
    {
        public const string Bank="ChipArenaIntroElemental";
        /// <summary>The staff: its grip at the wrist, its heel and its head along the hand's +z (Core NativeWeaponModels.Magic).</summary>
        public const float StaffHead=.88f,StaffHeel=-.41f,StaffSupport=.40f;
        protected NativeIntroElementalScript(string id,float total,float cardAt,float hit,int accent):base(id,total,cardAt,hit,accent){}
        public override string SetBank=>Bank;
        protected override float TipLength(bool left)=>left?StaffHead:.1f;
        /// <summary>The hero facing's forward and left (the pilot ends facing Facing; +x the rig's left).</summary>
        protected static readonly Vector3 Fwd=new Vector3((float)Math.Sin(Facing),0,(float)Math.Cos(Facing)),Left=new Vector3((float)Math.Cos(Facing),0,-(float)Math.Sin(Facing));
        protected static Vector3 L(float x,float y,float z)=>Local(x,y,z);
        protected static readonly Vector3 White=Vector3.One;
        protected static Vector3 Hex(int hex,float gain=1)=>NativeEntranceFx.Hex(hex,gain);

        // ---- the staff: a keyed grip track in the body's frame, the left arm reaching it ----
        /// <summary>Grip keys: time, then the grip (x left, y up from the root, z ahead; the body's frame) and the staff's axis
        /// (the same frame), seven floats a key. Set in the constructor.</summary>
        protected float[] StaffTimes=Array.Empty<float>(),StaffKeys=Array.Empty<float>();
        private const int StaffStride=6;
        protected void StaffKey(int i,float t,float gx,float gy,float gz,float ax,float ay,float az)
        {
            if(StaffTimes.Length<=i){Array.Resize(ref StaffTimes,i+1);Array.Resize(ref StaffKeys,(i+1)*StaffStride);}
            var v=Vector3.Normalize(new Vector3(ax,ay,az));StaffTimes[i]=t;int o=i*StaffStride;StaffKeys[o]=gx;StaffKeys[o+1]=gy;StaffKeys[o+2]=gz;StaffKeys[o+3]=v.X;StaffKeys[o+4]=v.Y;StaffKeys[o+5]=v.Z;
        }
        /// <summary>The keyed grip and axis at `t` (body frame).</summary>
        protected void StaffLocal(float t,out Vector3 grip,out Vector3 axis)
        {
            float C(int c)=>NativeEntranceMath.Curve(StaffTimes,StaffKeys,c,StaffStride,t);
            grip=new Vector3(C(0),C(1),C(2));axis=new Vector3(C(3),C(4),C(5));if(axis.LengthSquared()<1e-6f)axis=Vector3.UnitY;axis=Vector3.Normalize(axis);
        }
        /// <summary>How much of the right hand rides the staff's support grip at `t` (0: keyed).</summary>
        protected virtual float Support(float t)=>0;
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            if(StaffTimes.Length==0)return;StaffLocal(t,out var g,out var a);
            var grip=root+Vector3.Transform(g,rotation);var axis=Vector3.Transform(a,rotation);
            pose.World();var shoulder=root+Vector3.Transform(pose.WorldPosition[CapturedRig.LArm],rotation);
            // The hand's +y toward the shoulder (the fingers wrap the staff from its far side), +z along the staff.
            var wrist=CapturedRig.Look(axis,shoulder-grip);NativeEntrancePose.Reach(pose,root,rotation,true,grip,wrist,1);
            float s=Support(t);if(s<=.001f)return;
            var shoulderR=root+Vector3.Transform(pose.WorldPosition[CapturedRig.RArm],rotation);var hold=grip+axis*StaffSupport;
            NativeEntrancePose.Reach(pose,root,rotation,false,hold,CapturedRig.Look(axis,shoulderR-hold),s);
        }
        /// <summary>The staff's heel, grip and head at `t` (world, from the anchors: the staff as the rig holds it).</summary>
        protected void Staff(float t,out Vector3 heel,out Vector3 head)
        {
            head=Anchor(NativeEntranceAnchor.LeftTip,t);var hand=Anchor(NativeEntranceAnchor.LeftHand,t);var d=head-hand;float l=d.Length();
            var axis=l>1e-4f?d/l:Vector3.UnitY;heel=head-axis*(StaffHead-StaffHeel);
            // As drawn this frame: the rig's staff carried to where the model's wrist is (the model plays its own clips).
            if(drawnModel&&Math.Abs(drawnAt-t)<1e-4f){var shift=drawnLeft-hand;if(shift.LengthSquared()<1){head+=shift;heel+=shift;}}
        }

        // ---- the model's own motion: motion-bank clips over windows of the presentation clock (INativeIntroBody) ----
        /// <summary>One window: the model plays `Name` from clip time From to To across [Start, End) (a loop wraps).</summary>
        protected struct ClipWindow{public float Start,End,From,To,Fade;public string Name;}
        /// <summary>The windows in time order: before the first the model follows the keyed rig, from it to the end it plays clips (a
        /// clip is never left for the keys, which would pop).</summary>
        protected ClipWindow[] Clips=Array.Empty<ClipWindow>();
        protected static ClipWindow W(float start,float end,string name,float from,float to,float fade=.16f)=>new ClipWindow{Start=start,End=end,Name=name,From=from,To=to,Fade=fade};
        protected const string Idle="2Hand-Staff-Idle~mirror";
        public override bool Clip(float t,out string clip,out float time)
        {
            clip=null;time=0;var c=Clips;if(c.Length==0||t<c[0].Start)return false;
            int k=0;for(int i=1;i<c.Length;i++)if(c[i].Start<=t)k=i;var w=c[k];
            float u=NativeEntranceMath.Clamp01((t-w.Start)/Math.Max(1e-3f,w.End-w.Start));clip=w.Name;time=w.From+(w.To-w.From)*u;return true;
        }
        public override float Fade(float t){var c=Clips;if(c.Length==0)return .12f;int k=0;for(int i=1;i<c.Length;i++)if(c[i].Start<=t)k=i;return c[k].Fade;}
        // ---- the hands as drawn (INativeIntroHands): this frame's left wrist, for the staff's effects ----
        private Vector3 drawnLeft;private float drawnAt=-100;private bool drawnModel;
        public void Hands(float t,in NativeIntroHand right,in NativeIntroHand left){drawnLeft=left.At;drawnModel=left.Model;drawnAt=t;}

        // ---- effects helpers ----
        /// <summary>The camera at presentation time `t` (what the long streaks are laid against).</summary>
        protected void ViewAt(float t,out NativeEntranceView view){Rig.Evaluate(this,Math.Min(t,SettleAt),Context.Reduced,out view);}
        /// <summary>A streak from `tail` to `head`; one longer than NativeCacheOpenFx.ExactStreak lies on its head's camera plane.</summary>
        protected static void Beam(INativeEntranceFx fx,in NativeEntranceView v,Vector3 head,Vector3 tail,Vector3 color,float width,float taper,float u,float seed)
        {
            var axis=head-tail;float tp=taper;
            if(axis.LengthSquared()>NativeCacheOpenFx.ExactStreak*NativeCacheOpenFx.ExactStreak)
            {
                var f=v.Look-v.Eye;float l=f.Length();
                // (a long one whose head is behind the lens is not drawn: it could not be laid on its plane, and it is not seen)
                if(l>1e-5f){f/=l;float ax=axis.X,ay=axis.Y,az=axis.Z;if(!NativeCacheOpenFx.OntoHeadPlane(v.Eye.X,v.Eye.Y,v.Eye.Z,f.X,f.Y,f.Z,.2f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref tp))return;axis=new Vector3(ax,ay,az);}
            }
            if(axis.LengthSquared()<1e-10f)return;fx.Streak(head,axis,color,width,.01f,1,tp,u,seed);
        }
        /// <summary>A jagged bolt from `a` to `b` (its segments short; each laid on its head's plane), re-shaped every `flicker` s, forks
        /// off every third joint, hot glows at some joints. Returns the FX calls made.</summary>
        protected int Bolt(INativeEntranceFx fx,in NativeEntranceView v,float t,Vector3 a,Vector3 b,float jag,float flicker,Vector3 core,Vector3 halo,float width,int seed,int forks,float quality)
        {
            var axis=b-a;float len=axis.Length();if(len<1e-3f)return 0;var dir=axis/len;int segments=Math.Max(4,Math.Min(Context.Low?8:14,(int)(len/(Context.Low?1.2f:.8f))+2));if(Context.Low)forks=0;
            int phase=(int)Math.Floor(t/Math.Max(.02f,flicker));int s=seed*31+phase*977;
            var side=Vector3.Normalize(Vector3.Cross(dir,Math.Abs(dir.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var side2=Vector3.Cross(dir,side);
            var prev=a;int calls=0;
            for(int i=1;i<=segments;i++)
            {
                float u=(float)i/segments,env=(float)Math.Sin(Math.PI*u);
                var p=i==segments?b:a+axis*u+(side*NativeEntranceMath.Signed(s,i*2)+side2*NativeEntranceMath.Signed(s,i*2+1))*(jag*Math.Min(len,6)*env*.12f);
                Beam(fx,v,p,prev,core,width*(1-.35f*u),.15f,0,s+i);calls++;
                if(i%3==0&&NativeEntranceMath.Rand(s,i+40)<quality){fx.Glow(p,halo,0,width*10,1,0,s+i);calls++;}
                if(forks>0&&i%3==1&&i<segments-1&&NativeEntranceMath.Rand(s,i+50)<Math.Max(.35f,quality))
                {
                    var f0=p;var fd=Vector3.Normalize(dir*.55f+side*NativeEntranceMath.Signed(s,i+70)+side2*NativeEntranceMath.Signed(s,i+90)*.6f);float fl=len*(.08f+.1f*NativeEntranceMath.Rand(s,i+110));
                    var f1=f0+fd*fl*.5f+side2*(NativeEntranceMath.Signed(s,i+130)*fl*.15f);var f2=f0+fd*fl;
                    Beam(fx,v,f1,f0,core*.65f,width*.55f,.3f,0,s+i+200);Beam(fx,v,f2,f1,core*.45f,width*.4f,.6f,0,s+i+300);calls+=2;
                }
                prev=p;
            }
            return calls;
        }
        /// <summary>A four-point star (NativeEntranceFx.Glint's look) whose long rays are laid on their head's camera plane.</summary>
        protected static void Star(INativeEntranceFx fx,in NativeEntranceView v,Vector3 at,Vector3 color,float size,Vector3 axisA,Vector3 axisB,int seed)
        {
            if(size<=1e-3f)return;fx.Glow(at,color,0,size,1,0,seed);
            Beam(fx,v,at+axisA*size*1.1f,at-axisA*size*1.1f,color*1.4f,size*.08f,0,0,seed+1);Beam(fx,v,at+axisB*size*.8f,at-axisB*size*.8f,color*1.2f,size*.07f,0,0,seed+2);
        }
        /// <summary>The weapons' flame puff where the sink draws it, else a hot glow.</summary>
        protected static void Flame(INativeEntranceFx fx,Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor)
        {
            if(fx is INativeEntranceFire fire)fire.Fire(at,tint,opacity,size,u,seed,floor);
            else fx.Glow(at,tint*(2.2f*opacity),0,size*.8f,1,u,seed);
        }
        /// <summary>A count thinned by the effects quality and the Low tier (at least one).</summary>
        protected int N(int full,float quality){float n=full*Math.Max(.3f,Math.Min(1,quality));if(Context.Low)n*=.5f;return Math.Max(1,(int)n);}
        /// <summary>A slow, Reduced-safe flicker in 0..1 (no component faster than about 2.6 Hz).</summary>
        protected static float Flicker(float t,float seed)=>.5f+.25f*(float)Math.Sin(t*7.1f+seed)+.25f*(float)Math.Sin(t*11.3f+seed*1.7f);
        /// <summary>A world point at height `y` on the forward line `z` m ahead of the hero mark (negative: behind it).</summary>
        protected static Vector3 OnLine(float z,float y=0)=>Fwd*z+new Vector3(0,y,0);
        protected static Vector3 Flat(Vector3 v){v.Y=0;return v;}
    }

    // =====================================================================================================================
    // FORGEBORN (fire)
    // =====================================================================================================================
    /// <summary>
    /// Forgeborn, the fire staff's intro (7.8 s, the name at 6.0 s; docs/INTROS-ELEMENTAL.md). In a dead volcano's forge the
    /// cracks in the caldera floor race in from the rim, a column of fire erupts from the dais's vent, the pilot walks out of it
    /// with the flame peeling off its shoulders, the column's fire spirals up into a crown of flame over its head, and the staff
    /// comes down on the dais: fire runs out along every seam. The hero frame holds under the burning crown.
    /// </summary>
    public sealed class NativeIntroForgeborn:NativeIntroElementalScript
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"fire";public const string Name="Forgeborn";
        public const float Total=7.8f,CardAt=6.0f;
        public const float CrackFrom=1.0f,EruptAt=2.15f,RevealFrom=2.5f,WalkFrom=3.3f,CrownAt=4.75f,CrownSet=5.25f,SlamAt=5.45f;
        /// <summary>The vent (the column's foot): on the forward line, 1.3 m behind the hero mark (tools/intros/elemental_set.py FIRE_VENT).</summary>
        public static readonly Vector3 Vent=OnLine(-1.3f,NativeEntranceStage.FloorY);
        /// <summary>The lava rings' outer radii about the hero mark (rig fire/lava/1..5; fire/lava/0 under the dais), the rivers' bearings.</summary>
        public static readonly float[] Rings={6.5f,9.5f,13f,17.5f,25f};
        public static readonly float[] Rivers={-2.45f,-1.0f,.55f,2.2f};
        public const float ForgeZ=-11.5f;
        private static readonly Vector3 Flame0=Hex(0xffd27a),Orange=Hex(0xff6c32),Deep=Hex(0xd83410),Ember=Hex(0xff7a2a),Ash=new Vector3(.05f,.04f,.035f),Cool=Hex(0x4a90b8);
        private static readonly string[] LavaProps={"fire/lava/0","fire/lava/1","fire/lava/2","fire/lava/3","fire/lava/4","fire/lava/5"},RiverProps={"fire/river/0","fire/river/1","fire/river/2","fire/river/3"};
        public NativeIntroForgeborn():base(Id,Total,CardAt,EruptAt,0xff6c32)
        {
            var v=Vent;var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f,Facing,v.X,v.Z);
            // Out of the fire: three steps along the line to the mark.
            Steps=new[]{Step(true,WalkFrom+.05f,WalkFrom+.5f,.16f,-.62f,.1f,.09f),Step(false,WalkFrom+.5f,WalkFrom+.95f,-.18f,-.12f,-.42f,.09f),Step(true,WalkFrom+.95f,WalkFrom+1.3f,.16f,.07f,.1f,.06f)};
            Vector3 R(float z)=>OnLine(z,.88f);var r0=R(-1.3f);
            // In the fire: head bowed, the free arm loose.
            k.Key(0,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.06f).J(C,.08f).J(Hd,.38f).J(RA,-.08f,0,-.14f).J(RE,-.3f);
            k.Key(WalkFrom-.35f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.06f).J(C,.08f).J(Hd,.36f).J(RA,-.08f,0,-.14f).J(RE,-.3f);
            // The head comes up as it starts to walk.
            k.Key(WalkFrom,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.02f).J(C,.02f).J(Hd,.05f).J(RA,-.1f,0,-.15f).J(RE,-.35f);
            var w1=R(-.95f);var w2=R(-.45f);var w3=R(-.08f);var w4=R(0);
            k.Key(WalkFrom+.3f,stand).Root(w1.X,w1.Y,w1.Z,Facing).Drop(-.035f).J(C,0,.05f,0).J(Hd,-.02f).J(RA,.22f,0,-.14f).J(RE,-.4f).PassKey();
            k.Key(WalkFrom+.72f,stand).Root(w2.X,w2.Y,w2.Z,Facing).Drop(-.02f).J(C,0,-.05f,0).J(Hd,-.04f).J(RA,-.3f,0,-.14f).J(RE,-.45f).PassKey();
            k.Key(WalkFrom+1.12f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.035f).J(C,0,.03f,0).J(Hd,-.05f).J(RA,.12f,0,-.14f).J(RE,-.35f).PassKey();
            k.Key(CrownAt-.12f,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(-.02f).J(Hd,-.06f).J(RA,-.1f,0,-.18f).J(RE,-.4f);
            // The crown: the staff raised, the free hand open under the gathering fire, chin up.
            k.Key(CrownAt+.25f,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(.01f).J(B,-.04f).J(C,-.06f).J(Hd,-.38f).J(RA,-2.15f,0,-.5f).J(RE,-.55f).J(RH,0,0,.3f);
            k.Key(CrownSet,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(.02f).J(B,-.05f).J(C,-.08f).J(Hd,-.42f).J(RA,-2.3f,0,-.45f).J(RE,-.45f).J(RH,0,0,.3f).PassKey();
            // The slam: both hands drive the staff down, the knees giving.
            k.Key(SlamAt,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(-.32f).J(B,.18f).J(Sp,.12f).J(C,.06f).J(Hd,.05f).J(RA,-1.0f,0,.3f).J(RE,-.6f).ImpactKey();
            k.Key(SlamAt+.3f,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(-.27f).J(B,.14f).J(Sp,.1f).J(C,.04f).J(Hd,-.02f).J(RA,-1.0f,0,.3f).J(RE,-.6f);
            // Up, the staff at its side, the chest turned to the lens: the hero hold.
            var hero=NativeEntranceKeys.With(NativeEntranceKeys.Hero(),q=>{NativeEntranceKeys.Set(q,CapturedRig.Head,-.08f,.1f,0);});
            k.Key(CardAt-.25f,hero).Root(w4.X,w4.Y,w4.Z,Facing);
            k.Key(Total,hero).Root(w4.X,w4.Y,w4.Z,Facing);Keys=k;
            // The staff: upright at its side, carried out of the fire, raised high for the crown, driven down, back at its side.
            int s=0;
            StaffKey(s++,0,.27f,.1f,.16f,.06f,1,.1f);StaffKey(s++,WalkFrom,.27f,.1f,.17f,.06f,1,.12f);StaffKey(s++,WalkFrom+.72f,.27f,.12f,.2f,.06f,1,.06f);
            StaffKey(s++,CrownAt-.12f,.27f,.1f,.18f,.05f,1,.1f);StaffKey(s++,CrownAt+.25f,.22f,.62f,.24f,.05f,1,.02f);StaffKey(s++,CrownSet,.2f,.68f,.26f,.04f,1,0);
            StaffKey(s++,SlamAt-.06f,.14f,.32f,.42f,.02f,1,.05f);StaffKey(s++,SlamAt,.12f,-.4f,.44f,.02f,1,.05f);StaffKey(s++,SlamAt+.3f,.13f,-.34f,.43f,.03f,1,.05f);
            StaffKey(s++,CardAt-.25f,.27f,.1f,.17f,.06f,1,.1f);StaffKey(s++,Total,.27f,.1f,.17f,.06f,1,.1f);
            // The camera: macro along a seam; a crane up and back to the caldera; low under the eruption; up the column; the walk
            // out at the lens; round under the crown; wide and low for the slam; the hero frame.
            Rig.Dolly(0,L(1.6f,.26f,8.6f),L(.9f,.3f,4.4f),L(1.1f,.04f,4.0f),L(.1f,.3f,-.8f),32,30).Ease(NativeIntroEase.InOut).Focus(0,0,.55f)
               .Crane(CrackFrom+.05f,L(2.4f,.85f,5.6f),L(4.2f,.85f,9.6f),3.1f,L(0,.4f,-1.3f),L(0,1.9f,-3.6f),44,50).In(NativeIntroCut.Blend,.45f)
               .Dolly(EruptAt-.05f,L(3.0f,.3f,4.3f),L(2.7f,.32f,3.9f),OnLine(-1.3f,2.4f),OnLine(-1.3f,4.6f),52,48)
               .Crane(EruptAt+.6f,L(1.8f,.5f,2.3f),L(1.5f,.5f,1.8f),6.2f,OnLine(-1.3f,1.0f),OnLine(-1.3f,7.6f),42,46).Ease(NativeIntroEase.InOut)
               .Dolly(WalkFrom,L(.15f,1.25f,4.6f),L(.1f,1.3f,3.25f),new Vector3(0,.08f,0),new Vector3(0,.12f,0),34,31).Follow(NativeEntranceAnchor.Chest,.05f).Handheld(.4f)
               .Orbit(CrownAt-.15f,Vector3.Zero,30,72,3.0f,2.6f,.45f,.62f,new Vector3(0,1.95f,0),new Vector3(0,2.15f,0),40,42)
               .Dolly(SlamAt-.08f,L(-3.6f,.45f,5.0f),L(-3.3f,.5f,4.5f),L(0,.7f,-.3f),L(0,.75f,-.2f),48,46)
               .Hero(CardAt-.2f,1,1.2f,3.3f,31).In(NativeIntroCut.Blend,.45f)
               .Bars(0,1).Bars(CardAt-.3f,.6f).Black(0,1).Black(.08f,0);
            Ramps.Ramp(EruptAt,.3f,.32f,.9f);
            // The model's own casts once it is out of the fire: the staff raised for the crown, the cast down, the staff idle.
            Clips=new[]{W(CrownAt-.13f,CrownSet-.03f,"2Hand-Staff-Cast-L-Buff1_start~mirror",0,.67f,.25f),W(CrownSet-.03f,SlamAt,"2Hand-Staff-Cast-L-AOE1_start~mirror",0,.47f),W(SlamAt,SlamAt+.35f,"2Hand-Staff-Cast-L-AOE1_end~mirror",0,.27f),W(SlamAt+.35f,Total,Idle,0,Total-SlamAt-.35f,.25f)};
        }
        /// <summary>Hidden in the fire until the column takes it, then built in from the feet up as it forms.</summary>
        public override float Solid(float t,int bone)
        {
            if(t<RevealFrom)return 0;float h=bone==CapturedRig.LFoot||bone==CapturedRig.RFoot||bone==CapturedRig.LToe||bone==CapturedRig.RToe?0:bone==CapturedRig.LKnee||bone==CapturedRig.RKnee?.2f:bone==CapturedRig.Head||bone==CapturedRig.Neck?1:bone>=CapturedRig.LClav&&bone<=CapturedRig.RHand?.75f:.5f;
            return NativeEntranceMath.Smooth((t-RevealFrom-h*.35f)/.3f);
        }
        protected override Vector3 Focus(float t,Vector3 chest)=>t<WalkFrom?Vent+new Vector3(0,1.4f,0):chest;
        public override string AccentAt(float t)=>t<RevealFrom?"#3a1406":t<CrownAt?"#ff6c32":"#ffb040";
        /// <summary>How far the cracks have lit inward (0 none .. 1 to the dais) at `t`, and the slam's wave out.</summary>
        public static float Ring(int k,float t)
        {
            // k: 0 the dais, 1..5 outward. The wave in: the rim first (k 5) at CrackFrom, the dais last just before the eruption.
            float on=CrackFrom+(5-k)*.2f;float level=.18f+.62f*NativeEntranceMath.Ramp(t,on,on+.25f);
            level+=.6f*NativeEntranceMath.Pulse(t,EruptAt+(k*.05f),.05f,.5f);
            level+=.9f*NativeEntranceMath.Pulse(t,SlamAt+k*.07f,.04f,.45f);
            return level;
        }
        /// <summary>The column's strength (0..1): erupting, roaring, drawn up into the crown.</summary>
        public static float Column(float t)=>NativeEntranceMath.Ramp(t,EruptAt-.04f,EruptAt+.12f)*(1-NativeEntranceMath.Ramp(t,CrownAt-.1f,CrownSet));
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);l.FogNear=12;l.FogFar=62;var smoke=Vector3.Lerp(Hex(0x0b0504),Hex(0x2a0f07),.4f*Column(t)+.25f*NativeEntranceMath.Ramp(t,CrackFrom,EruptAt));l.Fog=smoke;l.Background=smoke;
            l.Sky=Hex(0x5a2a1a,.18f+.25f*Column(t));l.Ground=Hex(0x401408,.35f+.4f*NativeEntranceMath.Ramp(t,CrackFrom,EruptAt));
            float col=Column(t),fl=Flicker(t,1.3f);
            // the lava's glow from below: the rivers, the vent, the hearth
            l.Add(new Vector3(-6,.4f,-4),Ember*(1.4f+1.2f*NativeEntranceMath.Ramp(t,CrackFrom,EruptAt)),9);l.Add(new Vector3(6.5f,.4f,-2.5f),Ember*(1.2f+1.2f*NativeEntranceMath.Ramp(t,CrackFrom,EruptAt)),9);
            l.Add(new Vector3(0,1.4f,ForgeZ-.4f),Orange*(3.2f*(.85f+.15f*fl)),7);
            l.Add(Vent+new Vector3(0,.35f,0),Flame0*(.6f+2.4f*NativeEntranceMath.Ramp(t,CrackFrom+1,EruptAt)+4*Flash(t,EruptAt,.03f,.4f)),4.5f);
            // the column
            if(col>.01f){l.Add(Vent+new Vector3(0,2.0f,.4f),Flame0*(9*col*(.85f+.15f*fl)),11);l.Add(Vent+new Vector3(0,5.5f,0),Orange*(7*col),13);}
            // the crown
            float crown=NativeEntranceMath.Ramp(t,CrownAt,CrownSet);if(crown>.01f)l.Add(Anchor(NativeEntranceAnchor.Head,t)+new Vector3(0,.5f,.15f),Flame0*(3.4f*crown*(.9f+.1f*fl)),4.5f);
            // the slam's flash at the heel, and the hero's warm key with a cool fill for the hold
            float slam=Flash(t,SlamAt,.02f,.35f);if(slam>.01f){Staff(SlamAt,out var heel,out _);l.Add(heel+new Vector3(0,.3f,0),Flame0*(7*slam),6);}
            float hero=NativeEntranceMath.Ramp(t,CardAt-.6f,CardAt);if(hero>.01f){var c=Anchor(NativeEntranceAnchor.Chest,t);l.Add(c+L(1.4f,.6f,1.8f),Flame0*(2.4f*hero),6);l.Add(c+L(-2.2f,.4f,1.2f),Cool*(1.1f*hero),6);}
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Orange);v.Far=Hex(0x0d0504);v.Floor=Hex(0x120a08);v.Pool=Ember;v.Key=.25f+.6f*NativeEntranceMath.Ramp(t,EruptAt,EruptAt+.5f);v.PoolLevel=.3f+1.5f*NativeEntranceMath.Ramp(t,CrackFrom,EruptAt);return v;}
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("fire/near"))return false;var one=Vector3.One;var q=Quaternion.Identity;
            set.Piece("fire/near",Vector3.Zero,q,1,one,one);set.Piece("fire/mid",Vector3.Zero,q,1,one,one);set.Piece("fire/far",Vector3.Zero,q,1,one,one);
            for(int k=0;k<LavaProps.Length;k++){float g=Ring(k,t);set.Piece(LavaProps[k],Vector3.Zero,q,1,one,one*g);}
            float rivers=.75f+.25f*NativeEntranceMath.Ramp(t,CrackFrom,EruptAt);for(int k=0;k<RiverProps.Length;k++)set.Piece(RiverProps[k],Vector3.Zero,q,1,one,one*(rivers*(.92f+.08f*Flicker(t,k))));
            set.Piece("fire/falls",Vector3.Zero,q,1,one,one*(.9f+.1f*Flicker(t,7)));
            float seams=.25f+.75f*NativeEntranceMath.Ramp(t,CrackFrom+.4f,EruptAt)+.8f*Flash(t,EruptAt,.05f,.6f)+1.2f*Flash(t,SlamAt+.2f,.05f,.6f);set.Piece("fire/forge-seams",Vector3.Zero,q,1,one,one*seams);
            set.Piece("fire/hearth",Vector3.Zero,q,1,one,one*(.85f+.15f*Flicker(t,3)));
            return true;
        }
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Style=NativeIntroCardStyle.Rise;card.Ink=new Vector3(1,.93f,.82f);card.Glow=Hex(0xff7a2a,1.6f);
        }
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.05f,"intro2.fire.rumble","boss.ironworks.arrive.rumble","stone",.5f),
            new NativeIntroCue(CrackFrom,"intro2.fire.crack","boss.ironworks.crust.crack","stone",.6f),
            new NativeIntroCue(EruptAt,"intro2.fire.erupt","blast.fire","explosion",.9f),
            new NativeIntroCue(EruptAt+.2f,"intro2.fire.column","field.fire","fire",.55f),
            new NativeIntroCue(WalkFrom+.5f,"intro2.fire.step","impact.stone","stone",.32f),
            new NativeIntroCue(WalkFrom+.95f,"intro2.fire.step2","impact.stone","stone",.3f),
            new NativeIntroCue(CrownAt,"intro2.fire.crown","magic.flame-lance","fire",.6f),
            new NativeIntroCue(SlamAt,"intro2.fire.slam","magic.fire.hit","explosion",.8f),
            new NativeIntroCue(CardAt,"intro2.fire.name","melee.greatsword.hit","metal",.45f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            ViewAt(t,out var view);float floor=NativeEntranceStage.FloorY,col=Column(t);
            // Embers rising over the whole caldera, thicker once it erupts.
            NativeEntranceFx.Embers(fx,t,0,Total+.5f,new Vector3(0,.1f,-2),11,Context.Low?10:20,1.3f,2.8f,Ember*1.6f,.035f,7100,q,.5f);
            // High smoke drifting under the caldera's lid.
            for(int i=0,n=N(10,q);i<n;i++){float u=(t*.05f+i/(float)n)%1;var p=new Vector3(NativeEntranceMath.Signed(7150,i)*14,7.5f+3*NativeEntranceMath.Rand(7150,i+20),-6+NativeEntranceMath.Signed(7150,i+40)*8)+new Vector3(u*3,0,0);fx.Smoke(p,new Vector3(.06f,.035f,.03f),.35f*(float)Math.Sin(Math.PI*u),3.5f+2*u,u,7150+i,-10);}
            // The cracks racing in: sparks running down the radial seams at the wave's front.
            if(t>=CrackFrom&&t<EruptAt+.2f)
            {
                float u=NativeEntranceMath.Clamp01((t-CrackFrom)/(EruptAt-CrackFrom));float front=24*(1-u)+2.8f*u;
                for(int i=0,n=N(28,q);i<n;i++){float a=i*6.2831853f/n+NativeEntranceMath.Signed(7200,i)*.1f,r=front+NativeEntranceMath.Signed(7210,i)*.6f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));var p=d*r+new Vector3(0,floor+.02f,0);Beam(fx,view,p,p+d*.7f,Flame0*(2.4f*(1-u*.3f)),.025f,.6f,0,7200+i);}
            }
            // The rivers: bright glints riding the lava toward the dais, smoke off the falls.
            for(int k=0;k<4;k++)for(int i=0,n=N(8,q);i<n;i++)
            {
                float u=1-((t*.06f+i/(float)n+k*.17f)%1),r=6+19*u;var p=River(k,r)+new Vector3(0,-.08f,0);var nx=River(k,r-.5f)+new Vector3(0,-.08f,0);Beam(fx,view,p,p+(p-nx)*.6f,Flame0*1.8f,.05f,.8f,0,7300+k*20+i);
            }
            for(int k=0;k<2;k++){float a=Rivers[k]+(k==0?.05f:.03f);var foot=new Vector3((float)Math.Cos(a)*24.2f,floor+.3f,(float)Math.Sin(a)*24.2f);NativeEntranceFx.Jet(fx,t,0,Total+1,foot,new Vector3(0,1,0),3,.6f,2.5f,2.4f,new Vector3(.08f,.05f,.04f),.45f,7400+k*30,q*.6f,floor,.8f,.4f);}
            // The vent before: a shimmer of flame licking up, a thread of smoke.
            if(t<EruptAt+.1f)
            {
                float pre=NativeEntranceMath.Ramp(t,CrackFrom+.6f,EruptAt);
                for(int i=0,n=N(8,q);i<n;i++){float u=((t*1.6f+i/(float)n)%1);var p=Vent+new Vector3(NativeEntranceMath.Signed(7500,i)*.3f*(1-u),.1f+(.3f+1.1f*pre)*u,NativeEntranceMath.Signed(7501,i)*.3f*(1-u));Flame(fx,p,Vector3.Lerp(Flame0,Deep,u),(.35f+.6f*pre)*(1-u),.25f+.25f*pre,u,7500+i,floor);}
                NativeEntranceFx.Jet(fx,t,0,EruptAt,Vent+new Vector3(0,.2f,0),new Vector3(0,1,0),3,.5f,2.2f,.9f,new Vector3(.12f,.09f,.08f),.35f,7520,q*.6f,floor,.6f,.3f);
            }
            // The column: a roaring pillar of fire puffs, its core white-hot, its skin red, smoke rolling off its head, tongues
            // climbing it; a burst of sparks as it erupts.
            if(col>.01f)
            {
                int n=N(96,q);float top=9.5f*NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-EruptAt)/.45f));
                for(int i=0;i<n;i++)
                {
                    float life=.6f+.35f*NativeEntranceMath.Rand(7600,i),u=((t-EruptAt)/life+NativeEntranceMath.Rand(7601,i))%1;if(t<EruptAt)u=0;
                    float y=top*(float)Math.Pow(u,.8f),r=(.55f+.25f*(float)Math.Sin(y*1.3f+t*4))*(1-.35f*u)*(.7f+.3f*col);float a=NativeEntranceMath.Rand(7602,i)*6.283f+t*(1.2f+.6f*NativeEntranceMath.Rand(7603,i));
                    var p=Vent+new Vector3((float)Math.Cos(a)*r*NativeEntranceMath.Rand(7604,i),.15f+y,(float)Math.Sin(a)*r*NativeEntranceMath.Rand(7604,i));
                    var tint=u<.3f?Vector3.Lerp(Flame0*1.4f,Flame0,u/.3f):Vector3.Lerp(Orange,Deep,(u-.3f)/.7f);Flame(fx,p,tint,col*(1-u*u)*(.8f+.2f*NativeEntranceMath.Rand(7605,i)),(.55f+.7f*u)*(.8f+.2f*col),u,7600+i,floor);
                }
                fx.Glow(Vent+new Vector3(0,1.4f,0),Flame0*(1.6f*col),0,1.6f,1,0,7690);fx.Glow(Vent+new Vector3(0,4.5f,0),Orange*(.9f*col),0,3.2f,0,0,7691);
                for(int i=0,m=N(18,q);i<m;i++){float u=((t-EruptAt)*.55f+i/(float)m)%1;var p=Vent+new Vector3(NativeEntranceMath.Signed(7700,i)*1.4f*u,top+.4f+2.5f*u,NativeEntranceMath.Signed(7701,i)*1.4f*u);fx.Smoke(p,new Vector3(.09f,.06f,.05f),.55f*col*(1-u),1.2f+2.6f*u,u,7700+i,floor);}
                for(int i=0,m=N(12,q);i<m;i++){float u=((t*.9f+i/(float)m)%1),a=i*2.4f+u*5;var head=Vent+new Vector3((float)Math.Cos(a)*.6f,.4f+top*u,(float)Math.Sin(a)*.6f);var tail=Vent+new Vector3((float)Math.Cos(a-.6f)*.65f,.4f+top*Math.Max(0,u-.14f),(float)Math.Sin(a-.6f)*.65f);Beam(fx,view,head,tail,Flame0*(1.6f*col*(1-u)),.12f,.7f,u,7720+i);}
            }
            NativeEntranceFx.Sparks(fx,t,EruptAt,Vent+new Vector3(0,.4f,0),new Vector3(0,1,0),.8f,Context.Low?24:56,13,1.3f,9,.4f,Flame0*3,Ember,.02f,7750,q,floor);
            NativeEntranceFx.GroundDust(fx,t,EruptAt,Vent,N(18,q),.8f,5,1.4f,new Vector3(.09f,.06f,.05f),.5f,.9f,7820,q,floor);
            // Walking out: flame peeling off its shoulders, arms and back, embers in its steps.
            if(t>=WalkFrom-.2f&&t<CrownAt+.3f)
            {
                float peel=1-NativeEntranceMath.Ramp(t,WalkFrom+.6f,CrownAt+.2f);var back=-Fwd;
                for(int i=0,n=N(24,q);i<n;i++)
                {
                    var src=(NativeEntranceAnchor)(i%6==0?(int)NativeEntranceAnchor.LeftShoulder:i%6==1?(int)NativeEntranceAnchor.RightShoulder:i%6==2?(int)NativeEntranceAnchor.LeftElbow:i%6==3?(int)NativeEntranceAnchor.RightElbow:i%6==4?(int)NativeEntranceAnchor.Chest:(int)NativeEntranceAnchor.Head);
                    float u=((t*2.2f+i/(float)n)%1);float born=t-u*.45f;var p=Anchor(src,born)+back*(.5f*u)+new Vector3(NativeEntranceMath.Signed(7900,i)*.08f,.55f*u,NativeEntranceMath.Signed(7901,i)*.08f);
                    Flame(fx,p,Vector3.Lerp(Flame0,Deep,u),peel*(1-u)*.85f,.14f+.22f*u,u,7900+i,floor);
                }
                NativeEntranceFx.Embers(fx,t,WalkFrom,CrownAt,Anchor(NativeEntranceAnchor.Pelvis,t),.4f,Context.Low?8:16,1.1f,1.0f,Ember*1.8f,.03f,7950,q,1.5f);
            }
            // The crown: the column's fire spirals up round the pilot and gathers into nine flame tongues over its head.
            float gather=NativeEntranceMath.Ramp(t,CrownAt-.2f,CrownSet),crown=NativeEntranceMath.Ramp(t,CrownAt+.15f,CrownSet+.1f);
            var crownAt=Anchor(NativeEntranceAnchor.Head,t)+new Vector3(0,.32f,0);
            if(t>CrownAt-.2f&&t<CrownSet+.3f)
            {
                for(int i=0,n=N(30,q);i<n;i++){float u=((t*1.4f+i/(float)n)%1)*(1-crown*.6f),a=i*.21f+t*5+u*7;float r=2.2f*(1-u)+.3f*u;var p=crownAt+new Vector3((float)Math.Cos(a)*r,-2.2f*(1-u)*(1-gather*.5f),(float)Math.Sin(a)*r);Flame(fx,p,Vector3.Lerp(Orange,Flame0,u),gather*(1-crown*.7f)*.8f,.25f*(1-.5f*u),u,8000+i,floor);}
            }
            if(crown>.01f)
            {
                for(int i=0;i<9;i++)
                {
                    float a=i*.6981f+t*.9f,fl=.75f+.25f*Flicker(t,i);var b=crownAt+new Vector3((float)Math.Cos(a)*.27f,0,(float)Math.Sin(a)*.27f);
                    for(int j=0,m=Context.Low?2:4;j<m;j++){float u=((t*2.6f+j/(float)m+i*.13f)%1);Flame(fx,b+new Vector3(0,.04f+.24f*u*fl,0),Vector3.Lerp(Flame0*1.3f,Orange,u),crown*(1-u)*.9f,(.11f-.05f*u)*(.8f+.4f*fl),u,8100+i*8+j,floor);}
                }
                fx.Glow(crownAt+new Vector3(0,.1f,0),Flame0*(.7f*crown),0,.6f,1,0,8190);
                NativeEntranceFx.Embers(fx,t,CrownAt+.2f,Total+.5f,crownAt,.25f,Context.Low?6:12,.9f,1.4f,Ember*1.8f,.025f,8200,q,1.2f);
            }
            // The slam: fire racing out along the floor from the heel, sparks, a ring of ash puffs hugging the floor.
            float sw=t-SlamAt;
            if(sw>=0&&sw<.9f)
            {
                Staff(SlamAt,out var heel,out _);var hit=new Vector3(heel.X,floor+.03f,heel.Z);
                for(int i=0,n=N(32,q);i<n;i++){float a=i*6.2831853f/n+NativeEntranceMath.Signed(8300,i)*.12f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));float reach=.3f+7.5f*NativeEntranceMath.EaseOut(sw/.7f),u=sw/.9f;var p=hit+d*reach;Beam(fx,view,p,p-d*(.9f+.8f*(1-u)),Flame0*(2.6f*(1-u)),.06f,.5f,u,8300+i);}
                for(int i=0,n=N(14,q);i<n;i++){float a=i*6.2831853f/n;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));float u=NativeEntranceMath.Clamp01(sw/.6f);Flame(fx,hit+d*(.3f+1.6f*u)+new Vector3(0,.15f+.3f*u,0),Vector3.Lerp(Flame0,Deep,u),(1-u)*.9f,.35f+.5f*u,u,8350+i,floor);}
            }
            NativeEntranceFx.Sparks(fx,t,SlamAt,Vent,new Vector3(0,1,0),1.1f,Context.Low?18:40,7,.9f,9,.5f,Flame0*3,Ember,.016f,8400,q,floor);
            {Staff(SlamAt,out var heel,out _);NativeEntranceFx.GroundDust(fx,t,SlamAt,new Vector3(heel.X,floor,heel.Z),N(16,q),.3f,4,1.2f,Ash*3,.55f,.6f,8450,q,floor);}
        }
        /// <summary>River `k`'s centre at radius `r` (the same path the set script lays: tools/intros/elemental_set.py fire_river).</summary>
        public static Vector3 River(int k,float r){float a=Rivers[k]+.22f*(float)Math.Sin(r*.31f+k*1.7f)+.08f*(float)Math.Sin(r*.83f+k);return new Vector3((float)Math.Cos(a)*r,NativeEntranceStage.FloorY-.1f,(float)Math.Sin(a)*r);}
    }

    // =====================================================================================================================
    // STILL TIDE (water)
    // =====================================================================================================================
    /// <summary>
    /// Still Tide, the water staff's intro (8.2 s, the name at 6.4 s; docs/INTROS-ELEMENTAL.md). A drowned colonnade under the moon:
    /// the pilot stands ankle-deep, head bowed; the tide comes in and a wave rears up behind it, its lip curling; at a sweep of the
    /// staff the wave parts down its middle and its halves surge past on either side; the staff taps the water and frost races up
    /// both walls, freezing them mid-curl, the spray hanging in the air as glints (the camera orbits through the stopped moment); the
    /// pilot walks out between the frozen waves. The two halves are the bank's own pieces (liquid, then frozen), posed by Half.
    /// </summary>
    public sealed class NativeIntroStillTide:NativeIntroElementalScript
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"water";public const string Name="Still Tide";
        public const float Total=8.2f,CardAt=6.4f;
        public const float RiseFrom=1.3f,RearFrom=1.75f,RearAt=2.4f,SweepAt=2.95f,Parted=3.55f,FreezeAt=3.75f,HoldFrom=4.85f;
        /// <summary>Where the pilot stands throughout: the hero mark (the walls freeze on either side of it).</summary>
        public static readonly Vector3 Mark=OnLine(0);
        /// <summary>The water over the floor (m) before and after the tide (tools/intros/elemental_set.py WATER_*).</summary>
        public const float Shallow=.14f,Tide=.22f;
        /// <summary>The halves: their length, how far out to each side they end, how far behind the mark they rear and end.</summary>
        public const float HalfLength=5f,Apart=1.9f,BehindRear=3.4f,BehindFinal=2.6f;
        /// <summary>The frost's rings round the mark (rig water/ice/k: from the previous radius to this one).</summary>
        public static readonly float[] IceRings={1.6f,3.2f,5.5f,8.5f,14f};
        /// <summary>The hall's skylights (hall frame: x across, z along; tools/intros/elemental_set.py WATER_SKYLIGHTS) and its height.</summary>
        public static readonly float[] Skylights={-14f,-6f,2f};public const float VaultTop=14.2f,RoseZ=-23.6f,RoseY=10.8f;
        private static readonly Vector3 Cyan=Hex(0x63e2ff),Foam=Hex(0xeaffff),Deep=Hex(0x2b8bd6),Moon=Hex(0xa9c8ff),Ice=Hex(0xcff6ff),Teal=Hex(0x0e3a4a);
        private static readonly string[] IceProps={"water/ice/0","water/ice/1","water/ice/2","water/ice/3","water/ice/4"};
        /// <summary>A hall-frame point in the world (the hall runs along the hero facing).</summary>
        public static Vector3 Hall(float x,float y,float z)=>Left*x+Fwd*z+new Vector3(0,y,0);
        public NativeIntroStillTide():base(Id,Total,CardAt,SweepAt,0x63e2ff)
        {
            var m=Mark;var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f,Facing,m.X,m.Z);
            Vector3 R(float z)=>OnLine(z,.88f);var r0=R(0);
            k.Key(0,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.05f).J(C,.06f).J(Hd,.4f).J(RA,-.08f,0,-.14f).J(RE,-.3f);
            k.Key(RiseFrom-.1f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.05f).J(C,.06f).J(Hd,.36f).J(RA,-.08f,0,-.14f).J(RE,-.3f);
            // It hears the sea come in: chin up, a look up and back over its shoulder at the wave.
            k.Key(RearFrom,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.02f).J(C,-.04f,.15f,0).J(Hd,-.3f,.45f,0).J(RA,-.15f,0,-.25f).J(RE,-.4f);
            // The staff raised high against it, the free hand thrown out.
            k.Key(RearAt+.15f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.06f).J(B,-.06f).J(C,-.1f,.05f,0).J(Hd,-.45f,.1f,0).J(RA,-1.4f,0,-1.0f).J(RE,-.3f);
            k.Key(SweepAt-.08f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.08f).J(B,-.04f).J(C,-.06f,.35f,0).J(Hd,-.2f,.2f,0).J(RA,-1.2f,0,-1.2f).J(RE,-.3f).PassKey();
            // The sweep: the chest turns through, the staff carried across in front.
            k.Key(SweepAt+.3f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.14f).J(B,.08f).J(C,.02f,-.4f,0).J(Hd,-.05f,-.25f,0).J(RA,-.6f,0,-.9f).J(RE,-.5f).ImpactKey();
            // The tap: knees giving a little, the head down to the water.
            k.Key(FreezeAt-.12f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.06f).J(C,0,-.1f,0).J(Hd,.1f).J(RA,-.3f,0,-.4f).J(RE,-.6f);
            k.Key(FreezeAt,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.12f).J(B,.06f).J(C,.04f).J(Hd,.22f).J(RA,-.25f,0,-.35f).J(RE,-.6f).ImpactKey();
            k.Key(HoldFrom-.2f,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.04f).J(Hd,-.05f).J(RA,-.1f,0,-.18f).J(RE,-.35f);
            var hero=NativeEntranceKeys.With(NativeEntranceKeys.Hero(),q=>{NativeEntranceKeys.Set(q,CapturedRig.Chest,0,-.06f,0);NativeEntranceKeys.Set(q,CapturedRig.Head,-.06f,.05f,0);});
            k.Key(CardAt-.3f,hero).Root(r0.X,r0.Y,r0.Z,Facing);
            k.Key(Total,hero).Root(r0.X,r0.Y,r0.Z,Facing);Keys=k;
            int s=0;
            StaffKey(s++,0,.27f,.1f,.16f,.06f,1,.1f);StaffKey(s++,RearFrom,.27f,.12f,.18f,.06f,1,.1f);
            StaffKey(s++,RearAt+.15f,.3f,.62f,.12f,.25f,1,-.15f);StaffKey(s++,SweepAt-.08f,.38f,.48f,.1f,.85f,.45f,-.25f);
            StaffKey(s++,SweepAt+.3f,-.12f,.36f,.42f,-.85f,.3f,.35f);StaffKey(s++,FreezeAt-.12f,.16f,.12f,.4f,.05f,1,.05f);
            StaffKey(s++,FreezeAt,.16f,-.12f,.4f,.04f,1,.05f);StaffKey(s++,HoldFrom-.2f,.27f,.1f,.18f,.06f,1,.1f);StaffKey(s++,Total,.27f,.1f,.17f,.06f,1,.1f);
            // The camera: a glide over the water to the figure; up as the tide comes; low under the rearing wave; front and low as
            // it parts; an orbit through the frozen moment; ahead of it walking out; the centred hero frame between the walls.
            Rig.Dolly(0,L(.5f,.36f,9.6f),L(.3f,.4f,4.8f),m+new Vector3(0,1.0f,0),m+new Vector3(0,1.15f,0),38,34).Ease(NativeIntroEase.Out).Focus(0,0,.4f)
               .Dolly(RiseFrom,L(2.6f,1.7f,7.0f),L(2.2f,1.9f,6.2f),m+L(0,1.6f,-1.5f),m+L(0,2.6f,-2.8f),44,47).In(NativeIntroCut.Blend,.5f)
               .Dolly(RearAt-.3f,L(.65f,.32f,1.9f),L(.5f,.3f,1.6f),m+L(0,2.4f,-2.4f),m+L(0,4.0f,-3.2f),54,58)
               .Dolly(SweepAt-.05f,L(0,.55f,3.6f),L(0,.6f,3.2f),m+new Vector3(0,1.5f,0),m+new Vector3(0,1.45f,0),58,54)
               .Orbit(FreezeAt-.07f,m,-38,38,3.4f,3.2f,.6f,1.4f,new Vector3(0,1.3f,0),new Vector3(0,1.1f,0),44,40).Ease(NativeIntroEase.Linear)
               .Dolly(HoldFrom-.05f,L(.12f,1.3f,1.6f),L(.15f,1.25f,3.7f),L(0,1.4f,0),L(0,1.22f,0),40,35)
               .Dolly(CardAt-.25f,L(0,.95f,4.9f),L(0,1.0f,4.4f),L(0,1.25f,0),L(0,1.28f,0),32,30).In(NativeIntroCut.Blend,.5f).Ease(NativeIntroEase.Out)
               .Bars(0,1).Bars(CardAt-.3f,.6f).Black(0,1).Black(.1f,0);
            Ramps.Ramp(SweepAt,.5f,.16f,.32f).Ramp(FreezeAt,.22f,.5f,.95f);
            // The model's own casts: the staff idle, raised against the wave, swept, a tap, the idle again.
            Clips=new[]{W(0,RearAt-.2f,Idle,0,RearAt-.2f),W(RearAt-.2f,RearAt+.35f,"2Hand-Staff-Cast-L-Buff2_start~mirror",0,.47f),W(RearAt+.35f,SweepAt,"2Hand-Staff-Cast-L-Buff2_end~mirror",0,.2f),
                W(SweepAt,SweepAt+.35f,"2Hand-Staff-Cast-L-AOE2_start~mirror",0,.53f),W(SweepAt+.35f,Parted,"2Hand-Staff-Cast-L-AOE2_end~mirror",0,.27f),
                W(Parted,FreezeAt,"2Hand-Staff-Cast-Attack1_start~mirror",0,.27f),W(FreezeAt,FreezeAt+.4f,"2Hand-Staff-Cast-Attack1_end~mirror",0,.23f),W(FreezeAt+.4f,Total,Idle,0,Total-FreezeAt-.4f,.3f)};
        }
        /// <summary>The water's top at `t` (world y).</summary>
        public static float Level(float t)=>NativeEntranceStage.FloorY+Shallow+(Tide-Shallow)*NativeEntranceMath.Ramp(t,RiseFrom,RearAt);
        /// <summary>How far the wave has reared (0 under the water .. 1 full height).</summary>
        public static float Reared(float t)=>NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-RearFrom)/(RearAt-RearFrom)));
        /// <summary>How far the halves have swept round to the sides (0 one wall behind .. 1 flanking), held from the freeze.</summary>
        public static float Swept(float t)=>NativeEntranceMath.Smoother((Math.Min(t,FreezeAt)-SweepAt)/(Parted-SweepAt));
        /// <summary>Half `left`'s place and turn at `t` (its mesh: the wall along its +z from its origin, the curl toward its +x for
        /// the right half and -x for the left; tools/intros/elemental_set.py water_half).</summary>
        public static void Half(bool left,float t,out Vector3 at,out Quaternion turn)
        {
            float rise=Reared(t),sweep=Swept(t);var rear=Mark-Fwd*BehindRear;var fin=Mark+(left?Left:-Left)*Apart-Fwd*BehindFinal;
            at=Vector3.Lerp(rear,fin,sweep);at.Y=NativeEntranceStage.FloorY-5.4f*(1-rise)*(1-rise);
            float yaw=Facing+(left?1:-1)*(float)Math.PI/2*(1-sweep);float lean=(left?-1:1)*(.22f*(1-rise)+.06f*(float)Math.Sin(Math.PI*sweep));
            turn=Quaternion.Normalize(NativeEntranceMath.Yaw(yaw)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-lean));
        }
        /// <summary>A point on half `left`'s crest (u 0..1 along it) at `t`.</summary>
        private static Vector3 Crest(bool left,float t,float u,float height=4.55f,float reach=1.05f){Half(left,t,out var at,out var q);return at+Vector3.Transform(new Vector3((left?-1:1)*reach,height,HalfLength*u),q);}
        /// <summary>How far the frost has spread from the tap (m).</summary>
        public static float Frost(float t)=>t<FreezeAt?0:18*NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-FreezeAt)/.7f));
        protected override Vector3 Focus(float t,Vector3 chest)=>t<SweepAt?Mark+new Vector3(0,2.2f*Reared(t)+.8f,-1.4f*Reared(t)):chest;
        public override string AccentAt(float t)=>t<FreezeAt?"#63e2ff":"#cff6ff";
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);l.FogNear=10;l.FogFar=48;var far=Hex(0x02080c);l.Fog=far;l.Background=far;l.Sky=Hex(0x4a6f8f,.3f);l.Ground=Hex(0x0e2a33,.35f);
            l.Add(Hall(0,RoseY,RoseZ+1.5f),Moon*3.2f,18);
            for(int i=0;i<Skylights.Length;i++){var p=Hall(.6f,.3f,Skylights[i]-1.2f);l.Add(p+new Vector3(0,.4f,0),Moon*1.8f,6);}
            float rear=Reared(t)*(1-NativeEntranceMath.Ramp(t,FreezeAt,FreezeAt+.4f));if(rear>.01f)l.Add(Mark-Fwd*2.6f+new Vector3(0,3.2f,0),Cyan*(3.5f*rear),8);
            float flash=Flash(t,FreezeAt,.03f,.5f);if(flash>.01f)l.Add(Mark+new Vector3(0,1.2f,.4f),Ice*(9*flash),7);
            float ice=NativeEntranceMath.Ramp(t,FreezeAt,FreezeAt+.5f);if(ice>.01f){l.Add(Mark+Left*2.2f+new Vector3(0,2.4f,0)-Fwd*.6f,Cyan*(2.6f*ice),6);l.Add(Mark-Left*2.2f+new Vector3(0,2.4f,0)-Fwd*.6f,Cyan*(2.6f*ice),6);}
            float hero=NativeEntranceMath.Ramp(t,CardAt-.6f,CardAt);if(hero>.01f){var c=Anchor(NativeEntranceAnchor.Chest,t);l.Add(c+L(.6f,.8f,2.2f),Moon*(2.2f*hero),6);}
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Cyan);v.Far=Hex(0x02080c);v.Floor=Hex(0x061418);v.Pool=Moon;v.Key=.8f;v.PoolLevel=.6f;return v;}
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("water/hall"))return false;var one=Vector3.One;var q=Quaternion.Identity;
            set.Piece("water/hall",Vector3.Zero,q,1,one,one*.9f);set.Piece("water/vault",Vector3.Zero,q,1,one,one);set.Piece("water/far",Vector3.Zero,q,1,one,one);
            set.Piece("water/surface",new Vector3(0,Level(t),0),q,1,one,one);
            if(t<FreezeAt){Half(true,t,out var a,out var qa);set.Piece("water/wave/L",a,qa,1,one,one);Half(false,t,out var b,out var qb);set.Piece("water/wave/R",b,qb,1,one,one);}
            else
            {
                float frost=1+1.6f*Flash(t,FreezeAt+.15f,.06f,.7f);
                Half(true,t,out var a,out var qa);set.Piece("water/frozen/L",a,qa,1,one,one*frost);Half(false,t,out var b,out var qb);set.Piece("water/frozen/R",b,qb,1,one,one*frost);
                float front=Frost(t),r0=0;var lvl=new Vector3(Mark.X,Level(FreezeAt)+.006f,Mark.Z);
                for(int k=0;k<IceProps.Length;k++){if(front>r0)set.Piece(IceProps[k],lvl,NativeEntranceMath.Yaw(Facing),1,one,one*(1+.8f*Flash(t,FreezeAt+r0/18*.7f,.05f,.5f)));r0=IceRings[k];}
            }
            return true;
        }
        public override void Card(float t,ref NativeIntroCardState card){base.Card(t,ref card);card.Style=NativeIntroCardStyle.Wipe;card.Ink=new Vector3(.9f,.97f,1);card.Glow=Hex(0x63e2ff,1.5f);}
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.05f,"intro2.water.drips","boss.deepend.drip","water",.45f),
            new NativeIntroCue(RiseFrom,"intro2.water.rise","boss.deepend.tide","water",.6f),
            new NativeIntroCue(RearFrom,"intro2.water.rear","magic.tidal-wave","water",.75f),
            new NativeIntroCue(SweepAt,"intro2.water.part","boss.deepend.wave","water",.85f),
            new NativeIntroCue(FreezeAt,"intro2.water.freeze","blast.frost","shield",.85f),
            new NativeIntroCue(HoldFrom+.1f,"intro2.water.settle","impact.glass","shield",.3f),
            new NativeIntroCue(CardAt,"intro2.water.name","boss.noctiel.ripple","install",.5f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            ViewAt(t,out var view);float floor=NativeEntranceStage.FloorY,level=Level(t);
            // Moon shafts through the broken vault, motes turning in them.
            if(!Context.Low||q>.5f)for(int i=0;i<Skylights.Length;i++){var top=Hall(-.8f,VaultTop,Skylights[i]+.6f);var bottom=Hall(.6f,level,Skylights[i]-1.2f);NativeEntranceFx.Shaft(fx,t,top,bottom,.9f,1.5f,Moon*.06f,1,Context.Low?5:8,Context.Low?4:8,9100+i*20,q);}
            // Drips from the vault, each a falling bead and a little splash.
            for(int i=0,n=N(10,q);i<n;i++){float period=1.3f+.7f*NativeEntranceMath.Rand(9150,i),u=((t+NativeEntranceMath.Rand(9151,i)*period)%period)/period;var x=NativeEntranceMath.Signed(9152,i)*4;var z=-12+NativeEntranceMath.Rand(9153,i)*18;var top=Hall(x,VaultTop-1,z);var at=Vector3.Lerp(top,Hall(x,level,z),u*u);if(u<.97f)fx.Streak(at,new Vector3(0,-1,0)*(.3f+.6f*u),Foam*.6f,.008f,.05f,1,.5f,u,9150+i);else fx.Smoke(Hall(x,level+.04f,z),Foam*.6f,.35f,.12f,(u-.97f)/.03f,9160+i,floor);}
            // The water's face: moonlight glints on it, slow.
            for(int i=0,n=N(22,q);i<n;i++){var p=Mark+new Vector3(NativeEntranceMath.Signed(9200,i)*5,0,NativeEntranceMath.Signed(9201,i)*5);p.Y=level+.004f;float tw=.5f+.5f*(float)Math.Sin(t*1.7f+i*2.1f);fx.Glow(p,Moon*(.12f*tw),0,.05f,3,0,9200+i);}
            // The tide coming in: foam rolling along the colonnade's feet.
            float tide=NativeEntranceMath.Ramp(t,RiseFrom,RearAt)*(1-NativeEntranceMath.Ramp(t,FreezeAt,FreezeAt+.3f));
            if(tide>.01f)for(int i=0,n=N(16,q);i<n;i++){float side=i%2==0?1:-1,z=-16+((t*2.4f+i*1.7f)%26);var p=Hall(side*4.7f,level+.06f,z);fx.Smoke(p,Foam,.45f*tide,.5f+.3f*NativeEntranceMath.Rand(9250,i),((t*.8f+i*.13f)%1),9250+i,floor);}
            // The wave: spray tearing off its crest and lip, foam on its face, sheets of water running off it.
            float liquid=t<FreezeAt?Reared(t):0;
            if(liquid>.05f)for(int h=0;h<2;h++)
            {
                bool left=h==0;
                for(int i=0,n=N(12,q);i<n;i++){float u=(i+.5f)/n;var c=Crest(left,t,u);fx.Smoke(c+new Vector3(0,.1f,0),Foam,.75f*liquid,.55f+.3f*NativeEntranceMath.Rand(9300+h,i),((t*1.5f+i*.17f)%1),9300+h*40+i,floor);}
                for(int i=0,n=N(14,q);i<n;i++){float u=NativeEntranceMath.Rand(9350+h,i),age=((t*1.8f+i*.11f)%1)*.55f;var c=Crest(left,t-age,u,4.3f,1.4f);var v=(Crest(left,t,u,4.3f,1.4f)-Crest(left,t-.05f,u,4.3f,1.4f))*20+new Vector3(0,1.4f,0)+Fwd*1.6f;var p=c+v*age+new Vector3(0,-4.9f*age*age,0);if(p.Y<level)continue;var vel=v+new Vector3(0,-9.8f*age,0);Beam(fx,view,p,p-vel*.035f,Foam*(.9f*liquid*(1-age/.55f)),.018f,.6f,age,9350+h*40+i);}
                for(int i=0,n=N(8,q);i<n;i++){float u=(i+.5f)/n,a=((t*.9f+i*.21f)%1);var c=Crest(left,t,u,4.0f-3.6f*a,1.25f-.6f*a);fx.Smoke(c,Hex(0x9fe6ff),.4f*liquid*(1-a),.4f+.4f*a,a,9420+h*20+i,floor);}
            }
            // The parting: a crash of spray from the middle, its droplets stopping in the air at the freeze and hanging there.
            if(t>=SweepAt-.05f)
            {
                for(int i=0,n=N(40,q);i<n;i++)
                {
                    float born=SweepAt+.25f*NativeEntranceMath.Rand(9500,i),life=1.1f+.5f*NativeEntranceMath.Rand(9501,i);float age=Math.Min(t,FreezeAt)-born;if(age<0)continue;
                    var d=NativeEntranceMath.Direction(9502,i,true);var v=new Vector3(d.X*3.2f,1.8f+3.4f*NativeEntranceMath.Rand(9503,i),d.Z*2.2f)+Fwd*1.2f;var o=Mark-Fwd*(2.4f+NativeEntranceMath.Rand(9504,i))+new Vector3(NativeEntranceMath.Signed(9505,i)*.6f,1.2f+2.2f*NativeEntranceMath.Rand(9506,i),0);
                    var p=o+v*age+new Vector3(0,-4.9f*age*age,0);if(p.Y<level)continue;
                    if(t<FreezeAt){if(age<life)fx.Streak(p,v+new Vector3(0,-9.8f*age,0),Foam*(1-age/life),.02f,.05f,.03f,.6f,age/life,9500+i);}
                    else{float hang=t-FreezeAt;p+=new Vector3((float)Math.Sin(i+hang*.4f),(float)Math.Sin(i*1.7f+hang*.3f),(float)Math.Cos(i+hang*.35f))*(.02f*hang);float tw=.55f+.45f*(float)Math.Sin(hang*2.2f+i*1.9f);fx.Glow(p,Ice*(.45f*tw),0,.035f,3,0,9500+i);}
                }
                if(t<FreezeAt)for(int i=0,n=N(14,q);i<n;i++){float u=NativeEntranceMath.Clamp01((t-SweepAt)/.8f);var p=Mark-Fwd*(2.6f-1.8f*u)+new Vector3(NativeEntranceMath.Signed(9560,i)*(.6f+1.6f*u),.2f+1.4f*u*NativeEntranceMath.Rand(9561,i),0);fx.Smoke(p,Foam,.7f*(1-u),.8f+1.4f*u,u,9560+i,floor);}
            }
            // The freeze: frost racing up both walls from their feet, a white bloom, sparkle, cold mist rolling low.
            if(t>=FreezeAt)
            {
                float f=NativeEntranceMath.Clamp01((t-FreezeAt)/.45f);
                if(f<1)for(int h=0;h<2;h++)for(int i=0,n=N(12,q);i<n;i++){float u=(i+.5f)/n;var foot=Crest(h==0,t,u,.2f,.1f);var top=Crest(h==0,t,u,.2f+4.3f*f,.1f+.9f*f*f);Beam(fx,view,top,Vector3.Lerp(foot,top,.55f),Ice*(2.2f*(1-f*.5f)),.03f,.6f,f,9600+h*20+i);}
                for(int h=0;h<2;h++)for(int i=0,n=N(10,q);i<n;i++){var p=Crest(h==0,t,NativeEntranceMath.Rand(9650+h,i),.4f+4f*NativeEntranceMath.Rand(9652+h,i),.3f+.8f*NativeEntranceMath.Rand(9654+h,i));float tw=.5f+.5f*(float)Math.Sin(t*2.4f+i*2.7f+h);fx.Glow(p,Ice*(.5f*tw*NativeEntranceMath.Ramp(t,FreezeAt,FreezeAt+.4f)),0,.05f,3,0,9650+h*20+i);}
                for(int i=0,n=N(12,q);i<n;i++){float u=((t-FreezeAt)*.18f+i/(float)n)%1;var p=Mark+new Vector3(NativeEntranceMath.Signed(9700,i)*3.5f,level+.15f+.2f*u,NativeEntranceMath.Signed(9701,i)*3.5f);fx.Smoke(p,Ice*.8f,.22f*(float)Math.Sin(Math.PI*u)*NativeEntranceMath.Ramp(t,FreezeAt,FreezeAt+.5f),1.1f+.8f*u,u,9700+i,floor);}
                Staff(FreezeAt,out var heel,out _);NativeEntranceFx.Sparks(fx,t,FreezeAt,new Vector3(heel.X,level+.02f,heel.Z),new Vector3(0,1,0),1.2f,Context.Low?14:30,3.5f,.6f,6,.8f,Ice*2.4f,Cyan,.012f,9750,q,floor);
            }
            // The ice settling: a shiver of frost off each wall's crest; the breath frosting at the hero hold.
            for(int h=0;h<2;h++)NativeEntranceFx.Sparks(fx,t,HoldFrom+.1f+.25f*h,Crest(h==0,t,.5f,4.4f,1.0f),new Vector3(0,-1,0),1.2f,Context.Low?6:12,1.2f,.9f,5,.8f,Ice*1.4f,Cyan,.008f,9800+h*20,q,floor);
            if(t>=CardAt-.4f){var head=Anchor(NativeEntranceAnchor.Head,t);for(int i=0;i<3;i++){float u=((t-CardAt)*.42f+i/3f)%1;if(u<0)u+=1;fx.Smoke(head+Fwd*(.15f+.35f*u)+new Vector3(0,-.04f+.08f*u,0),Ice,.18f*(1-u),.08f+.2f*u,u,9850+i,floor);}}
        }
    }

    // =====================================================================================================================
    // STRIKE POINT (lightning)
    // =====================================================================================================================
    /// <summary>
    /// Strike Point, the lightning staff's intro (7.2 s, the name at 5.5 s; docs/INTROS-ELEMENTAL.md). A summit under a storm: rain
    /// on the slate, the clouds lit from inside, six conductor spires humming; arcs leap spire to spire and crawl in, the cloud over the
    /// summit turns into a vortex and a point of light gathers in its eye; the bolt comes down with branching forks and is the pilot,
    /// landing white-hot in a crouch (a lag-0 afterimage over the body, cooling); the branches die in the air, arcs run out over the
    /// stone; it rises with arcs crawling on its limbs, calls a second bolt into the staff and sends it to every spire, which fire back
    /// into the sky. In Reduced effects the flickers are held to one slow swell.
    /// </summary>
    public sealed class NativeIntroStrikePoint:NativeIntroElementalScript
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"lightning";public const string Name="Strike Point";
        public const float Total=7.2f,CardAt=5.5f;
        public const float ChargeFrom=1.1f,GatherAt=2.0f,StrikeAt=2.45f,RiseFrom=3.7f,CallAt=4.3f,CallStrike=4.55f,SpireFire=4.78f;
        /// <summary>The six spires: their bearings from the hero facing (degrees, + toward the pilot's left), their ring and their tips'
        /// height (tools/intros/elemental_set.py SPIRE_*).</summary>
        public static readonly float[] SpireBearings={30,90,150,-30,-90,-150};public const float SpireRing=7.6f,SpireTip=6.2f;
        private static readonly Vector3 Violet=Hex(0xc8aaff,2.4f),Core=Hex(0xf3ecff,4.2f),Storm=Hex(0x8fa4c8),Rain=Hex(0x9fb0cc);
        private static readonly string[] SeamProps={"lightning/seams/0","lightning/seams/1","lightning/seams/2","lightning/seams/3","lightning/seams/4","lightning/seams/5"};
        /// <summary>The cloud's own flashes (presentation s); in Reduced only those at least .45 s apart, each widened.</summary>
        private static readonly float[] CloudFlashes={.35f,.82f,1.24f,1.55f,1.9f,2.18f,3.25f,3.9f,5.05f,5.75f,6.5f};
        public static Vector3 Spire(int i,float y=0){float a=Facing+SpireBearings[i]*(float)(Math.PI/180);return new Vector3((float)Math.Sin(a)*SpireRing,y,(float)Math.Cos(a)*SpireRing);}
        public NativeIntroStrikePoint():base(Id,Total,CardAt,StrikeAt,0xc8aaff)
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            Start=Stance(.24f,.12f,.25f,-.22f,-.2f,-.5f);
            // The landing: a deep crouch over the planted feet, the free hand on the stone, head down.
            float[] crouch(float drop,float lean,float head)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=Facing;v[NativeEntranceKeys.BodyY]=drop;NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Sp,.15f,0,0);NativeEntranceKeys.Set(v,C,.1f,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,RA,-.75f,0,-.25f);NativeEntranceKeys.Set(v,RE,-.15f,0,0);});
            k.Key(0,crouch(-.44f,.55f,.5f)).Root(0,.88f,0,Facing);
            k.Key(StrikeAt,crouch(-.46f,.58f,.5f)).Root(0,.88f,0,Facing).ImpactKey();
            k.Key(StrikeAt+.5f,crouch(-.42f,.52f,.42f)).Root(0,.88f,0,Facing);
            k.Key(RiseFrom,crouch(-.4f,.5f,.2f)).Root(0,.88f,0,Facing);
            // Up, arcs on its limbs, the head coming up.
            k.Key(RiseFrom+.45f,stand).Root(0,.88f,0,Facing).Drop(-.05f).J(C,-.04f).J(Hd,-.12f).J(RA,-.3f,0,-.4f).J(RE,-.5f);
            // The call: the staff thrust up to the sky, the free arm out, the chest open.
            k.Key(CallAt+.15f,stand).Root(0,.88f,0,Facing).Drop(.02f).J(B,-.06f).J(C,-.12f).J(Hd,-.42f).J(RA,-.25f,0,-1.25f).J(RE,-.2f);
            k.Key(CallStrike+.1f,stand).Root(0,.88f,0,Facing).Drop(-.03f).J(B,-.04f).J(C,-.1f).J(Hd,-.38f).J(RA,-.25f,0,-1.3f).J(RE,-.15f).ImpactKey();
            // The plant: the staff brought down, a small drop.
            k.Key(SpireFire,stand).Root(0,.88f,0,Facing).Drop(-.14f).J(B,.08f).J(C,.04f).J(Hd,-.05f).J(RA,-.4f,0,-.9f).J(RE,-.3f).ImpactKey();
            var hero=NativeEntranceKeys.With(NativeEntranceKeys.Hero(),q=>{NativeEntranceKeys.Set(q,CapturedRig.Head,-.1f,.12f,0);NativeEntranceKeys.Set(q,CapturedRig.RArm,-.2f,0,-.35f);});
            k.Key(CardAt-.2f,hero).Root(0,.88f,0,Facing);
            k.Key(Total,hero).Root(0,.88f,0,Facing);Keys=k;
            int s=0;
            StaffKey(s++,0,.46f,-.18f,.18f,.55f,.8f,.25f);StaffKey(s++,RiseFrom,.44f,-.12f,.2f,.5f,.82f,.25f);
            StaffKey(s++,RiseFrom+.45f,.28f,.12f,.18f,.08f,1,.1f);StaffKey(s++,CallAt+.15f,.18f,.7f,.12f,.04f,1,0);StaffKey(s++,CallStrike+.1f,.18f,.72f,.12f,.04f,1,0);
            StaffKey(s++,SpireFire,.2f,.02f,.32f,.05f,1,.05f);StaffKey(s++,CardAt-.2f,.27f,.1f,.17f,.06f,1,.1f);StaffKey(s++,Total,.27f,.1f,.17f,.06f,1,.1f);
            // The camera: low and wide on the summit, tilting to the sky; straight up into the vortex, turning; down from the cloud;
            // the smash to the strike; a fast orbit; rising with it; wide with the spires firing; the hero frame.
            Rig.Dolly(0,L(4.6f,.42f,7.2f),L(4.1f,.46f,6.5f),L(0,1.8f,0),L(0,3.4f,-1.6f),52,54)
               .Hold(ChargeFrom,L(0,.55f,.9f),L(0,18f,-5f),72).Roll(0,24).Ease(NativeIntroEase.Linear)
               .Dolly(GatherAt,L(.4f,19f,6.5f),L(.3f,15f,5f),new Vector3(0,0,0),new Vector3(0,.5f,0),40,44)
               .Dolly(StrikeAt,L(3.2f,.25f,3.8f),L(3.0f,.3f,3.5f),L(0,1.0f,0),L(0,.8f,0),62,56)
               .Orbit(StrikeAt+.3f,Vector3.Zero,40,-140,3.4f,3.0f,.35f,.6f,new Vector3(0,.75f,0),new Vector3(0,.8f,0),48,44)
               .Crane(RiseFrom,L(1.1f,.55f,2.3f),L(.95f,.55f,2.05f),.9f,new Vector3(0,-.1f,0),Vector3.Zero,38,36).Follow(NativeEntranceAnchor.Head,.04f)
               .Dolly(CallAt-.05f,L(-5.6f,1.2f,8.2f),L(-5.0f,1.4f,7.4f),L(0,3.2f,0),L(0,2.3f,0),58,54)
               .Hero(CardAt-.55f,-1,1.15f,3.1f,30)
               .Bars(0,1).Bars(CardAt-.3f,.6f).Black(0,1).Black(.08f,0);
            Ramps.Ramp(StrikeAt,.22f,.38f,.9f);
            // The model's own staff from the rise: up into the idle, the summon, the idle.
            Clips=new[]{W(RiseFrom,CallAt-.05f,Idle,0,CallAt-.05f-RiseFrom,.4f),W(CallAt-.05f,CallStrike+.05f,"2Hand-Staff-Cast-L-Summon1_start~mirror",0,.53f),W(CallStrike+.05f,SpireFire+.27f,"2Hand-Staff-Cast-L-Summon1_end~mirror",0,.47f),W(SpireFire+.27f,Total,Idle,.55f,.55f+Total-SpireFire-.27f,.25f)};
        }
        public override float Solid(float t,int bone)=>t<StrikeAt?0:1;
        /// <summary>The strike's white-hot copy over the body, cooling.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            if(t<StrikeAt||t>StrikeAt+.6f||into.Length<1)return 0;float u=(t-StrikeAt)/.6f;
            into[0]=new NativeEntranceGhost{Lag=0,Opacity=(1-u)*(1-u),Color=Vector3.Lerp(new Vector3(1.4f,1.35f,1.6f),Hex(0xc8aaff,1.4f),u),Offset=Vector3.Zero};return 1;
        }
        protected override Vector3 Focus(float t,Vector3 chest)=>t<StrikeAt?new Vector3(0,1.2f,0):chest;
        public override string AccentAt(float t)=>t<StrikeAt?"#1a1430":"#c8aaff";
        /// <summary>The spires' charge 0..1: humming from the start, rising to the strike, spent, charged again by the call.</summary>
        public static float Charge(float t)=>.2f+.8f*NativeEntranceMath.Ramp(t,ChargeFrom,StrikeAt-.05f)*(1-NativeEntranceMath.Ramp(t,StrikeAt,StrikeAt+.4f))+.9f*NativeEntranceMath.Ramp(t,CallStrike,SpireFire)*(1-NativeEntranceMath.Ramp(t,SpireFire+.3f,SpireFire+1.2f));
        /// <summary>The cloud's flash level at `t` (Reduced: only those .45 s apart, widened by Flash).</summary>
        private float Cloud(float t)
        {
            float v=0,last=-10;for(int i=0;i<CloudFlashes.Length;i++){float at=CloudFlashes[i];if(Context.Reduced&&at-last<.45f)continue;last=at;v=Math.Max(v,Flash(t,at,.02f,.12f));}
            return v;
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);float cloud=Cloud(t),strike=Flash(t,StrikeAt,.015f,.3f),call=Flash(t,CallStrike,.015f,.25f);
            l.FogNear=12;l.FogFar=70;var far=Vector3.Lerp(Hex(0x05070c),Hex(0x2a2f44),.6f*cloud+.8f*strike);l.Fog=far;l.Background=far;
            l.Sky=Storm*(.22f+.7f*cloud+1.4f*strike);l.Ground=Hex(0x101420,.3f);
            l.Add(new Vector3(-6,26,-14),Storm*(14*cloud),60);
            float charge=Charge(t);l.Add(Spire(0,4)+Spire(1,0)*.3f,Violet*(1.4f*charge),9);l.Add(Spire(4,4)+Spire(3,0)*.3f,Violet*(1.4f*charge),9);
            float eye=NativeEntranceMath.Ramp(t,GatherAt-.3f,StrikeAt)*(1-NativeEntranceMath.Ramp(t,StrikeAt,StrikeAt+.1f));if(eye>.01f)l.Add(new Vector3(0,16,0),Core*(3*eye),30);
            if(strike>.01f)l.Add(new Vector3(0,2.2f,.3f),Core*(16*strike),14);
            float glow=NativeEntranceMath.Ramp(t,StrikeAt,StrikeAt+.05f)*(1-NativeEntranceMath.Ramp(t,StrikeAt+.3f,RiseFrom+.5f));if(glow>.01f)l.Add(new Vector3(0,.3f,0),Violet*(2.5f*glow),4);
            if(call>.01f){Staff(CallStrike,out _,out var head);l.Add(head,Core*(9*call),9);}
            float crackle=NativeEntranceMath.Ramp(t,StrikeAt,StrikeAt+.1f);if(crackle>.01f){Staff(t,out _,out var head);l.Add(head+new Vector3(0,.1f,0),Violet*((.8f+.4f*Flicker(t,2))*crackle),3);}
            float hero=NativeEntranceMath.Ramp(t,CardAt-.6f,CardAt);if(hero>.01f){var c=Anchor(NativeEntranceAnchor.Chest,t);l.Add(c-Fwd*1.6f+new Vector3(0,.9f,0),Violet*(3.5f*hero),6);}
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Hex(0xc8aaff));v.Far=Hex(0x05070c);v.Floor=Hex(0x0c0f16);v.Pool=Violet;v.Key=.45f;v.PoolLevel=.3f+1.6f*Flash(t,StrikeAt,.02f,.5f);return v;}
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("lightning/plateau"))return false;var one=Vector3.One;var q=Quaternion.Identity;
            set.Piece("lightning/plateau",Vector3.Zero,q,1,one,one);set.Piece("lightning/spires",Vector3.Zero,q,1,one,one);set.Piece("lightning/far",Vector3.Zero,q,1,one,one);
            float charge=Charge(t);for(int i=0;i<SeamProps.Length;i++)set.Piece(SeamProps[i],Vector3.Zero,q,1,one,one*(.15f+1.6f*charge*(.85f+.15f*Flicker(t,i))));
            set.Piece("lightning/crater",Vector3.Zero,q,1,one,one*(.3f+2.2f*NativeEntranceMath.Ramp(t,StrikeAt,StrikeAt+.05f)*(1-NativeEntranceMath.Ramp(t,StrikeAt+.2f,RiseFrom+1))));
            return true;
        }
        public override void Card(float t,ref NativeIntroCardState card){base.Card(t,ref card);card.Style=NativeIntroCardStyle.Slam;card.Ink=new Vector3(.97f,.95f,1);card.Glow=Hex(0xc8aaff,1.8f);}
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.05f,"intro2.storm.bed","field.storm","lightning",.45f),
            new NativeIntroCue(ChargeFrom,"intro2.storm.charge","boss.station.arc","lightning",.55f),
            new NativeIntroCue(GatherAt,"intro2.storm.gather","chip.vacuum","dash",.6f),
            new NativeIntroCue(StrikeAt,"intro2.storm.strike","magic.lightning.hit","lightning",1f),
            new NativeIntroCue(StrikeAt+.25f,"intro2.storm.after","blast.shock","explosion",.6f),
            new NativeIntroCue(RiseFrom,"intro2.storm.crackle","magic.ball-lightning","electric-hook",.45f),
            new NativeIntroCue(CallStrike,"intro2.storm.call","magic.lightning.shot","electric-hook-hit",.8f),
            new NativeIntroCue(CardAt,"intro2.storm.name","blast.quake","explosion",.5f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            ViewAt(t,out var view);float floor=NativeEntranceStage.FloorY,flick=Context.Reduced?.42f:.05f,cloud=Cloud(t);
            // Rain, slanting.
            var slant=new Vector3(.9f,-11,.4f);
            for(int i=0,n=N(46,q);i<n;i++){float u=((t*1.1f+NativeEntranceMath.Rand(10100,i))%1);var p=new Vector3(NativeEntranceMath.Signed(10101,i)*9,9-9*u,NativeEntranceMath.Signed(10102,i)*9)+Fwd*2;if(p.Y<floor)continue;fx.Streak(p,slant,Rain*(.18f+.25f*cloud),.006f,.2f,.04f,.3f,u,10100+i);}
            // The cloud's underside, lit from inside.
            for(int i=0,n=N(18,q);i<n;i++){float a=i*.349f+t*.06f+(t>ChargeFrom?(t-ChargeFrom)*.5f*(1-i/18f):0),r=4+16*NativeEntranceMath.Rand(10150,i);var p=new Vector3((float)Math.Sin(a)*r,19+3*NativeEntranceMath.Rand(10151,i),(float)Math.Cos(a)*r);fx.Smoke(p,Hex(0x1a1f2c)+Storm*(.25f*cloud),.75f,6+4*NativeEntranceMath.Rand(10152,i),.4f,10150+i,-50);}
            for(int i=0;i<3;i++){float c=cloud*(i==0?1:.5f);if(c>.02f)fx.Glow(new Vector3(-6+i*7,22,-14+i*5),Storm*(1.4f*c),0,9,0,0,10180+i);}
            // St Elmo's fire on the spire tips and arcs creeping up them; arcs leaping spire to spire and crawling in over the stone.
            float charge=Charge(t);
            for(int i=0;i<6;i++){var tip=Spire(i,SpireTip);fx.Glow(tip,Violet*(.5f*charge*(.8f+.2f*Flicker(t,i))),0,.4f+.3f*charge,1,0,10200+i);if(charge>.3f&&NativeEntranceMath.Rand(10210+i,(int)(t/flick))<q)Bolt(fx,view,t,tip,tip+NativeEntranceMath.Direction(10220+i,(int)(t/flick),true)*.6f,1.2f,flick,Violet*(charge*.8f),Violet*.15f,.01f,10220+i*7,0,q);}
            if(t>=ChargeFrom&&t<StrikeAt)
            {
                float u=NativeEntranceMath.Clamp01((t-ChargeFrom)/(StrikeAt-ChargeFrom));
                for(int i=0;i<6;i++){if(NativeEntranceMath.Rand(10300+i,(int)(t/Math.Max(flick,.12f)))>.35f+.5f*u)continue;Bolt(fx,view,t,Spire(i,SpireTip-.2f),Spire((i+1)%6,SpireTip-.2f),1,flick,Core*(.5f+.5f*u),Violet*.2f,.016f,10300+i*11,1,q);}
                for(int i=0;i<6;i++){var from=Spire(i,.08f);var to=Vector3.Lerp(from,new Vector3(0,.08f,0),.25f+.7f*u);Bolt(fx,view,t,from,to,.8f,flick,Violet*(.6f+.4f*u),Violet*.12f,.012f,10400+i*9,0,q);}
            }
            // The vortex: the cloud turning over the summit, a point of light gathering in its eye.
            float eye=NativeEntranceMath.Ramp(t,GatherAt-.4f,StrikeAt)*(1-NativeEntranceMath.Ramp(t,StrikeAt,StrikeAt+.06f));
            if(t>ChargeFrom&&t<StrikeAt+.3f){for(int i=0,n=N(16,q);i<n;i++){float a=i*.3927f+(t-ChargeFrom)*(1.2f+.8f*i/16f),r=1.5f+6*(i/16f)*(1-.4f*eye);fx.Smoke(new Vector3((float)Math.Sin(a)*r,17.5f-1.2f*i/16f,(float)Math.Cos(a)*r),Hex(0x2a3044)+Violet*(.05f*eye),.6f,3.5f,.5f,10500+i,-50);}}
            if(eye>.01f){fx.Glow(new Vector3(0,16.5f,0),Core*(.8f*eye),0,1.4f*eye,1,0,10550);fx.Glow(new Vector3(0,16.5f,0),Violet*(.4f*eye),0,4*eye,0,0,10551);}
            // The strike: the trunk and its forks from the eye to the crouch, white over violet; the branches hang and die.
            float trunk=NativeEntranceMath.Ramp(t,StrikeAt-.03f,StrikeAt)*(1-NativeEntranceMath.Ramp(t,StrikeAt+.12f,StrikeAt+.45f));
            if(trunk>.01f)
            {
                var top=new Vector3(0,16.5f,0);var bottom=new Vector3(0,1.1f,0);float rf=Context.Reduced?.42f:.06f;
                Bolt(fx,view,t,top,bottom,1.0f,rf,Core*trunk,Violet*(.5f*trunk),.07f,10600,3,q);Bolt(fx,view,t+.017f,top,bottom,1.3f,rf,Violet*(.7f*trunk),Violet*(.1f*trunk),.03f,10650,2,q);
            }
            float branches=NativeEntranceMath.Ramp(t,StrikeAt-.02f,StrikeAt)*(1-NativeEntranceMath.Ramp(t,StrikeAt+.1f,StrikeAt+1.1f));
            if(branches>.01f)for(int b=0;b<6;b++)
            {
                float h=4+9*NativeEntranceMath.Rand(10700,b),a=NativeEntranceMath.Rand(10701,b)*6.283f;var from=new Vector3(NativeEntranceMath.Signed(10702,b)*.4f,h,NativeEntranceMath.Signed(10703,b)*.4f);
                var to=from+new Vector3((float)Math.Sin(a)*(2+3*NativeEntranceMath.Rand(10704,b)),-(1.5f+3*NativeEntranceMath.Rand(10705,b)),(float)Math.Cos(a)*(2+3*NativeEntranceMath.Rand(10704,b)));
                Bolt(fx,view,StrikeAt+b*.01f,from,to,1.1f,10,Violet*(branches*(.6f+.4f*NativeEntranceMath.Rand(10706,b))),Violet*(.08f*branches),.014f,10710+b*13,b%2,q);
            }
            if(t>=StrikeAt&&t<StrikeAt+.25f){float w=1-(t-StrikeAt)/.25f;fx.Glow(new Vector3(0,1.0f,0),(Vector3.One*1.2f+Violet*.5f)*w*(Context.Reduced?.5f:1),0,2.6f,1,0,10790);}
            // Arcs racing out over the stone, chips of rock and a burst of rain thrown off.
            float g=t-StrikeAt;
            if(g>0&&g<.7f){int arcs=Context.Low?4:7;for(int i=0;i<arcs;i++){float a=i*6.2831853f/arcs+.3f,R=.3f+g*7;var from=new Vector3((float)Math.Sin(a)*.2f,floor+.02f,(float)Math.Cos(a)*.2f);var to=new Vector3((float)Math.Sin(a)*R,floor+.02f,(float)Math.Cos(a)*R);Bolt(fx,view,t,from,to,.7f,flick,Violet*(1.4f*(1-g/.7f)),Violet*(.1f*(1-g/.7f)),.016f,10800+i*7,0,q);}}
            NativeEntranceFx.Sparks(fx,t,StrikeAt,new Vector3(0,.2f,0),new Vector3(0,1,0),1.3f,Context.Low?16:36,8,.8f,9,.6f,Core,Violet,.014f,10900,q,floor);
            NativeEntranceFx.GroundDust(fx,t,StrikeAt,new Vector3(0,floor,0),N(16,q),.3f,5,1.1f,Hex(0x3a3f4c),.5f,.6f,10950,q,floor);
            if(g>0&&g<.6f)for(int i=0,n=N(28,q);i<n;i++){var d=NativeEntranceMath.Direction(10980,i,true);var v=new Vector3(d.X*6,2+3*Math.Abs(d.Y),d.Z*6);var p=new Vector3(0,.5f,0)+v*g+new Vector3(0,-4.9f*g*g,0);if(p.Y<floor)continue;fx.Streak(p,v,Rain*(.8f*(1-g/.6f)),.008f,.05f,.04f,.4f,g,10980+i);}
            // Arcs crawling over its limbs while it rises; the staff head crackling from then on.
            if(t>=StrikeAt&&t<CallAt+.2f)
            {
                float on=1-NativeEntranceMath.Ramp(t,RiseFrom+.4f,CallAt+.2f);
                for(int i=0;i<(Context.Low?3:6);i++){var a=Anchor((NativeEntranceAnchor)((int)NativeEntranceAnchor.LeftShoulder+(i%2)),t);var b=Anchor(i<2?NativeEntranceAnchor.LeftHand+(i%2):i<4?NativeEntranceAnchor.LeftKnee+(i%2):NativeEntranceAnchor.Chest,t);Bolt(fx,view,t,a,b,1.4f,flick,Violet*(1.1f*on),Violet*(.08f*on),.008f,11000+i*7,0,q);}
            }
            if(t>=StrikeAt){Staff(t,out _,out var head);for(int i=0;i<2;i++)Bolt(fx,view,t,head,head+NativeEntranceMath.Direction(11050+i,(int)(t/flick),true)*.35f,1.2f,flick,Violet*.9f,Violet*.1f,.008f,11050+i*7,0,q);fx.Glow(head,Violet*(.6f+.2f*Flicker(t,4)),0,.22f,1,0,11060);}
            // The call: a bolt from the cloud into the staff head; from the head to every spire; the spires firing back into the sky.
            float call=NativeEntranceMath.Ramp(t,CallStrike-.02f,CallStrike)*(1-NativeEntranceMath.Ramp(t,CallStrike+.1f,SpireFire+.05f));
            if(call>.01f){Staff(CallStrike,out _,out var head);Bolt(fx,view,t,new Vector3(head.X+.4f,17,head.Z-.6f),head,1.1f,Context.Reduced?.42f:.06f,Core*call,Violet*(.4f*call),.05f,11100,2,q);}
            float fire=NativeEntranceMath.Ramp(t,SpireFire-.02f,SpireFire)*(1-NativeEntranceMath.Ramp(t,SpireFire+.25f,SpireFire+.7f));
            if(fire>.01f){Staff(SpireFire,out _,out var head);for(int i=0;i<6;i++){Bolt(fx,view,t,head,Spire(i,SpireTip),1,flick,Core*(.8f*fire),Violet*(.15f*fire),.022f,11200+i*13,0,q);Bolt(fx,view,t,Spire(i,SpireTip),Spire(i,SpireTip)+new Vector3(NativeEntranceMath.Signed(11300,i)*1.5f,13,NativeEntranceMath.Signed(11301,i)*1.5f),1.2f,flick,Core*(.7f*fire),Violet*(.12f*fire),.03f,11300+i*13,1,q);}}
        }
    }

    // =====================================================================================================================
    // BLACK SUN (darkness)
    // =====================================================================================================================
    /// <summary>
    /// Black Sun, the darkness staff's intro (8.4 s, the name at 6.6 s; docs/INTROS-ELEMENTAL.md). An obsidian plain at dusk, the sun
    /// framed in a broken gate: the moon slides over it and the light drains; every monolith's shadow peels off the ground and pours to
    /// the centre like ink; at totality the corona blooms round the black sun and the poured dark rises into a void; the pilot steps out
    /// of it (built in as it crosses the void's face), the dark running off its body; one eye of light ignites on its visor; the staff
    /// rises and the dark spreads behind its shoulders like wings, then snaps back to the monoliths. Centred, symmetrical frames.
    /// </summary>
    public sealed class NativeIntroBlackSun:NativeIntroElementalScript
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"darkness";public const string Name="Black Sun";
        public const float Total=8.4f,CardAt=6.6f;
        public const float EclipseFrom=1.0f,PourFrom=1.35f,TotalityAt=2.8f,RiseFrom=2.9f,OpenAt=3.55f,StepFrom=3.75f,EyeAt=4.55f,WingsAt=5.05f,SnapAt=5.7f;
        /// <summary>The void (its centre: on the forward line behind the mark) and the plane the pilot crosses out of it.</summary>
        public static readonly Vector3 Void=OnLine(-1.55f,1.25f);
        /// <summary>The sun: behind the pilot and up (m), its disc's radius; the moon slides over it (rig darkness/moon).</summary>
        public static readonly Vector3 Sun=OnLine(-58f,17f);public const float SunRadius=5.2f;
        /// <summary>The monoliths' bearings from the hero facing (degrees) and ring (tools/intros/elemental_set.py DARK_*); the gate
        /// stands behind at GateZ on the forward line.</summary>
        public static readonly float[] Monoliths={55,95,135,-55,-95,-135};public const float MonolithRing=11.5f,GateZ=-14f;
        private static readonly Vector3 Violet=Hex(0xaf6eff),Rim=Hex(0xc04dff),Halo=Hex(0xd9a6ff),Ink=new Vector3(.012f,.006f,.02f),Dusk=Hex(0xffd9b0),Mote=Hex(0xf0b0ff);
        private static readonly string[] GlyphProps={"darkness/glyphs/0","darkness/glyphs/1","darkness/glyphs/2","darkness/glyphs/3","darkness/glyphs/4","darkness/glyphs/5"};
        public static Vector3 Monolith(int i,float y=0){float a=Facing+Monoliths[i]*(float)(Math.PI/180);return new Vector3((float)Math.Sin(a)*MonolithRing,y,(float)Math.Cos(a)*MonolithRing);}
        public NativeIntroBlackSun():base(Id,Total,CardAt,TotalityAt,0xaf6eff)
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();var v0=OnLine(-1.7f);
            Start=Stance(.16f,.07f,.1f,-.18f,-.1f,-.42f,Facing,v0.X,v0.Z);
            Steps=new[]{Step(true,StepFrom+.05f,StepFrom+.5f,.16f,-1.05f,.1f,.08f),Step(false,StepFrom+.5f,StepFrom+.95f,-.18f,-.5f,-.42f,.08f),Step(true,StepFrom+.95f,StepFrom+1.4f,.16f,.07f,.1f,.07f),Step(false,StepFrom+1.25f,StepFrom+1.6f,-.18f,-.1f,-.42f,.05f)};
            Vector3 R(float z)=>OnLine(z,.88f);var r0=R(-1.7f);
            k.Key(0,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.06f).J(C,.08f).J(Hd,.42f).J(RA,-.08f,0,-.14f).J(RE,-.3f);
            k.Key(StepFrom,stand).Root(r0.X,r0.Y,r0.Z,Facing).Drop(-.03f).J(Sp,.06f).J(C,.08f).J(Hd,.36f).J(RA,-.08f,0,-.14f).J(RE,-.3f);
            var w1=R(-1.2f);var w2=R(-.62f);var w3=R(-.1f);var w4=R(0);
            k.Key(StepFrom+.32f,stand).Root(w1.X,w1.Y,w1.Z,Facing).Drop(-.03f).J(C,0,.05f,0).J(Hd,.26f).J(RA,.15f,0,-.14f).J(RE,-.35f).PassKey();
            k.Key(StepFrom+.75f,stand).Root(w2.X,w2.Y,w2.Z,Facing).Drop(-.02f).J(C,0,-.05f,0).J(Hd,.08f).J(RA,-.2f,0,-.14f).J(RE,-.4f).PassKey();
            // The eye: the head comes up and level as it reaches the mark.
            k.Key(EyeAt+.1f,stand).Root(w3.X,w3.Y,w3.Z,Facing).Drop(-.03f).J(Hd,-.04f).J(RA,-.1f,0,-.16f).J(RE,-.35f);
            k.Key(WingsAt-.1f,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(-.02f).J(Hd,-.04f).J(RA,-.1f,0,-.16f).J(RE,-.35f);
            // The wings: the staff raised before it, the free arm swept out and back, the chest open.
            k.Key(WingsAt+.35f,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(.01f).J(B,-.05f).J(C,-.14f).J(Hd,-.18f).J(RA,.25f,0,-1.15f).J(RE,-.15f);
            k.Key(SnapAt-.08f,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(.02f).J(B,-.06f).J(C,-.16f).J(Hd,-.2f).J(RA,.3f,0,-1.25f).J(RE,-.1f).PassKey();
            // The snap: the staff planted, the arm whipped in.
            k.Key(SnapAt,stand).Root(w4.X,w4.Y,w4.Z,Facing).Drop(-.12f).J(B,.06f).J(C,.04f).J(Hd,.02f).J(RA,-.2f,0,-.3f).J(RE,-.7f).ImpactKey();
            var hero=NativeEntranceKeys.With(NativeEntranceKeys.Hero(),q=>{NativeEntranceKeys.Set(q,CapturedRig.Chest,0,0,0);NativeEntranceKeys.Set(q,CapturedRig.Head,-.05f,0,0);});
            k.Key(CardAt-.3f,hero).Root(w4.X,w4.Y,w4.Z,Facing);
            k.Key(Total,hero).Root(w4.X,w4.Y,w4.Z,Facing);Keys=k;
            int s=0;
            StaffKey(s++,0,.27f,.1f,.16f,.06f,1,.1f);StaffKey(s++,WingsAt-.1f,.27f,.1f,.18f,.06f,1,.1f);
            StaffKey(s++,WingsAt+.35f,.22f,.45f,.42f,.12f,.8f,.6f);StaffKey(s++,SnapAt-.08f,.2f,.5f,.44f,.1f,.85f,.55f);
            StaffKey(s++,SnapAt,.2f,-.12f,.36f,.04f,1,.05f);StaffKey(s++,CardAt-.3f,.27f,.1f,.17f,.06f,1,.1f);StaffKey(s++,Total,.27f,.1f,.17f,.06f,1,.1f);
            // The camera: centred through the gate on the sun; a dolly zoom on the centre as the shadows pour; low and centred up at the
            // void under the black sun; locked off as it steps out; the eye; a slow pull back; the centred hero frame.
            Rig.Dolly(0,L(0,1.6f,12.5f),L(0,1.55f,10.8f),L(0,3.6f,-20),L(0,4.2f,-20),46,44)
               .Dolly(EclipseFrom+.25f,L(0,2.3f,10f),L(0,1.9f,4.6f),Void-new Vector3(0,.3f,0),Void-new Vector3(0,.25f,0),30,58).Ease(NativeIntroEase.Linear).In(NativeIntroCut.Blend,.4f)
               .Dolly(TotalityAt-.05f,L(0,.3f,5.2f),L(0,.32f,4.8f),L(0,4.0f,-9),L(0,4.6f,-10),50,48)
               .Hold(OpenAt-.05f,L(0,1.3f,3.9f),OnLine(-1.0f,1.2f),38)
               .Dolly(EyeAt-.1f,Fwd*.62f+Left*.06f+new Vector3(0,1.5f,0),Fwd*.52f+Left*.05f+new Vector3(0,1.51f,0),Vector3.Zero,Vector3.Zero,22,19).Follow(NativeEntranceAnchor.Head,0,1).Focus(0,0,.7f)
               .Dolly(WingsAt-.05f,L(0,1.35f,2.2f),L(0,1.25f,4.4f),L(0,1.45f,0),L(0,1.3f,0),40,36).In(NativeIntroCut.Blend,.3f)
               .Dolly(CardAt-.3f,L(0,.85f,4.9f),L(0,.9f,4.5f),L(0,1.32f,0),L(0,1.36f,0),32,30).In(NativeIntroCut.Blend,.5f).Ease(NativeIntroEase.Out)
               .Bars(0,1).Bars(CardAt-.3f,.6f).Black(0,1).Black(.1f,0);
            Ramps.Ramp(TotalityAt,.4f,.26f,.5f);
            // The model's own staff once out of the void: the summon (the wings), the strike (the snap), the idle.
            Clips=new[]{W(WingsAt-.05f,WingsAt+.07f,"2Hand-Staff-Cast-L-Summon2_start~mirror",0,.13f,.25f),W(WingsAt+.07f,SnapAt-.1f,"2Hand-Staff-Cast-L-Summon2_end~mirror",0,.73f),W(SnapAt-.1f,SnapAt+.05f,"2Hand-Staff-Cast-Attack1_start~mirror",.05f,.27f),W(SnapAt+.05f,SnapAt+.3f,"2Hand-Staff-Cast-Attack1_end~mirror",0,.23f),W(SnapAt+.3f,Total,Idle,0,Total-SnapAt-.3f,.25f)};
        }
        /// <summary>The eclipse's progress (0 the sun clear .. 1 totality).</summary>
        public static float Eclipse(float t)=>NativeEntranceMath.Smoother((t-EclipseFrom)/(TotalityAt-EclipseFrom));
        /// <summary>The void's size (0..1): risen from the poured dark, held while it opens, closing behind the pilot.</summary>
        public static float VoidSize(float t)=>NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-RiseFrom)/.6f))*(1-NativeEntranceMath.Ramp(t,StepFrom+.9f,StepFrom+1.6f));
        /// <summary>Built in as each part crosses out of the void's face.</summary>
        public override float Solid(float t,int bone)
        {
            if(t<OpenAt)return 0;if(t>StepFrom+1.8f)return 1;
            NativeEntranceAnchor a;switch(bone){case CapturedRig.Head:case CapturedRig.Neck:a=NativeEntranceAnchor.Head;break;case CapturedRig.Chest:case CapturedRig.Spine:a=NativeEntranceAnchor.Chest;break;
                case CapturedRig.LClav:case CapturedRig.LArm:a=NativeEntranceAnchor.LeftShoulder;break;case CapturedRig.RClav:case CapturedRig.RArm:a=NativeEntranceAnchor.RightShoulder;break;
                case CapturedRig.LElbow:a=NativeEntranceAnchor.LeftElbow;break;case CapturedRig.RElbow:a=NativeEntranceAnchor.RightElbow;break;case CapturedRig.LHand:a=NativeEntranceAnchor.LeftHand;break;case CapturedRig.RHand:a=NativeEntranceAnchor.RightHand;break;
                case CapturedRig.LKnee:a=NativeEntranceAnchor.LeftKnee;break;case CapturedRig.RKnee:a=NativeEntranceAnchor.RightKnee;break;case CapturedRig.LFoot:case CapturedRig.LToe:a=NativeEntranceAnchor.LeftFoot;break;case CapturedRig.RFoot:case CapturedRig.RToe:a=NativeEntranceAnchor.RightFoot;break;
                default:a=NativeEntranceAnchor.Pelvis;break;}
            float d=Vector3.Dot(Anchor(a,t)-Void,Fwd);return NativeEntranceMath.Smooth((d+.05f)/.22f);
        }
        protected override Vector3 Focus(float t,Vector3 chest)=>t<OpenAt?Void:chest;
        public override string AccentAt(float t)=>t<OpenAt?"#000000":t<EyeAt?"#2a0f40":"#af6eff";
        /// <summary>The visor's lit eye (the pilot's left), from the head anchor.</summary>
        private Vector3 Eye(float t){var h=Anchor(NativeEntranceAnchor.Head,t);return h+ForwardOf(t)*.13f+RightOf(t)*-.045f+new Vector3(0,-.07f,0);}
        public static Vector3 Moon(float t){float e=Eclipse(t);return Sun+Fwd*2.5f+Left*(7.2f*(1-e))+new Vector3(0,1.8f*(1-e),0);}
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);float e=Eclipse(t),tot=NativeEntranceMath.Ramp(t,TotalityAt-.1f,TotalityAt+.3f);
            l.FogNear=16;l.FogFar=90;var far=Vector3.Lerp(Hex(0x2a2433),Hex(0x030205),e);l.Fog=far;l.Background=far;
            l.Sky=Vector3.Lerp(Hex(0x9a8fb0,.4f),Hex(0x2a1840,.12f),e);l.Ground=Vector3.Lerp(Hex(0x3a3040,.3f),Hex(0x0a0610,.2f),e);
            if(e<.98f)l.Directional(Sun-new Vector3(0,8,0),Dusk*(1.1f*(1-e)));
            if(tot>.01f)l.Add(-Fwd*5+new Vector3(0,3.6f,0),Rim*(5.5f*tot),12);
            float rise=VoidSize(t);if(rise>.01f)l.Add(Void+Fwd*.6f,Violet*(3*rise),5);
            float eye=NativeEntranceMath.Ramp(t,EyeAt-.02f,EyeAt+.08f);if(eye>.01f)l.Add(Eye(t)+ForwardOf(t)*.12f,Halo*(1.6f*eye+2.2f*Flash(t,EyeAt,.02f,.35f)),1.6f);
            float wings=NativeEntranceMath.Ramp(t,WingsAt,WingsAt+.3f)*(1-NativeEntranceMath.Ramp(t,SnapAt,SnapAt+.3f));if(wings>.01f){var c=Anchor(NativeEntranceAnchor.Chest,t);l.Add(c-Fwd*.8f+new Vector3(0,.4f,0),Rim*(3.5f*wings),5);}
            float hero=NativeEntranceMath.Ramp(t,CardAt-.6f,CardAt);if(hero>.01f){var c=Anchor(NativeEntranceAnchor.Chest,t);l.Add(c+Fwd*2.4f+new Vector3(0,.6f,0),Hex(0x8a7aa8,1.1f*hero),6);}
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Violet);float e=Eclipse(t);v.Far=Vector3.Lerp(Hex(0x2a2433),Hex(0x030205),e);v.Floor=Vector3.Lerp(Hex(0x1a1620),Hex(0x050307),e);v.Pool=Violet;v.Key=.9f*(1-e)+.2f;v.PoolLevel=.5f;return v;}
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("darkness/plain"))return false;var one=Vector3.One;var q=Quaternion.Identity;
            set.Piece("darkness/plain",Vector3.Zero,q,1,one,one);set.Piece("darkness/stones",Vector3.Zero,q,1,one,one);set.Piece("darkness/far",Vector3.Zero,q,1,one,one);
            float e=Eclipse(t),glyph=.2f+.8f*NativeEntranceMath.Ramp(t,PourFrom,TotalityAt)+1.2f*Flash(t,SnapAt+.15f,.05f,.5f);for(int i=0;i<GlyphProps.Length;i++)set.Piece(GlyphProps[i],Vector3.Zero,q,1,one,one*glyph);
            set.Piece("darkness/moon",Moon(t),NativeEntranceMath.Yaw(Facing),1,one,one);
            return true;
        }
        public override void Card(float t,ref NativeIntroCardState card){base.Card(t,ref card);card.Style=NativeIntroCardStyle.Assemble;card.Ink=new Vector3(.93f,.88f,1);card.Glow=Hex(0xaf6eff,1.7f);}
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.05f,"intro2.void.wind","boss.noctiel.glide","darkness",.4f),
            new NativeIntroCue(PourFrom,"intro2.void.pour","boss.noctiel.eclipse.pool","darkness",.6f),
            new NativeIntroCue(TotalityAt,"intro2.void.totality","boss.noctiel.eclipse","darkness",.8f),
            new NativeIntroCue(OpenAt,"intro2.void.emerge","blast.implosion","dash",.65f),
            new NativeIntroCue(EyeAt,"intro2.void.eye","boss.noctiel.hush.open","install",.5f),
            new NativeIntroCue(WingsAt,"intro2.void.wings","boss.noctiel.wings.open","dash",.6f),
            new NativeIntroCue(SnapAt,"intro2.void.snap","magic.darkness.hit","darkness",.7f),
            new NativeIntroCue(CardAt,"intro2.void.name","magic.singularity","darkness",.45f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            ViewAt(t,out var view);float floor=NativeEntranceStage.FloorY,e=Eclipse(t),tot=NativeEntranceMath.Ramp(t,TotalityAt-.1f,TotalityAt+.25f);
            // Ash motes drifting, catching the dusk, then the violet.
            var mote=Vector3.Lerp(Dusk*.3f,Mote*.35f,e);for(int i=0,n=N(22,q);i<n;i++){var p=new Vector3(NativeEntranceMath.Signed(12000,i)*7,.2f+3*NativeEntranceMath.Rand(12001,i),NativeEntranceMath.Signed(12002,i)*7)+new Vector3((float)Math.Sin(t*.2f+i)*.3f,t*.04f,(float)Math.Cos(t*.17f+i)*.3f);fx.Glow(p,mote*(.25f+.15f*(float)Math.Sin(t*.8f+i)),0,.03f,0,0,12000+i);}
            // The sun: a hot disc and its glare, its light going as the moon covers it; the corona and the diamond at totality.
            float sun=1-e*.97f;fx.Glow(Sun,Dusk*(3.2f*sun),0,SunRadius*2.1f,1,0,12050);fx.Glow(Sun,Dusk*(.8f*sun),0,SunRadius*6,0,0,12051);
            if(tot>.01f)
            {
                fx.Glow(Sun-Fwd*.5f,Halo*(1.1f*tot),0,SunRadius*2.8f,0,0,12060);fx.Glow(Sun-Fwd*.5f,Rim*(.6f*tot),0,SunRadius*5,0,0,12061);
                for(int i=0,n=N(22,q);i<n;i++)
                {
                    float a=i*6.2831853f/n+NativeEntranceMath.Signed(12070,i)*.12f+t*.02f,len=SunRadius*(.5f+1.3f*NativeEntranceMath.Rand(12071,i))*(1+.06f*(float)Math.Sin(t*1.3f+i));
                    var d=Left*(float)Math.Cos(a)+Vector3.UnitY*(float)Math.Sin(a);var from=Sun+Fwd*1.6f+d*SunRadius*1.02f;Beam(fx,view,from+d*len,from,Halo*(.9f*tot),.18f+.12f*NativeEntranceMath.Rand(12072,i),.9f,0,12070+i);
                }
                float diamond=Flash(t,TotalityAt,.04f,.3f);if(diamond>.01f){var p=Sun+Fwd*2.6f+(Left*.7f+Vector3.UnitY*.7f)*SunRadius;Star(fx,view,p,Vector3.One*(3.5f*diamond),2.2f*diamond,Left,Vector3.UnitY,12095);}
            }
            // The pour: each monolith's shadow peeling off the ground and running in to the centre as ink.
            if(t>=PourFrom&&t<RiseFrom+.6f)
            {
                float u=NativeEntranceMath.Clamp01((t-PourFrom)/(TotalityAt-PourFrom)),gone=1-NativeEntranceMath.Ramp(t,RiseFrom,RiseFrom+.6f);var centre=new Vector3(Void.X,floor,Void.Z);
                for(int m=0;m<6;m++){var from=Monolith(m,floor);for(int i=0,n=N(9,q);i<n;i++){float s=((t-PourFrom)*.9f+i/(float)n)%1,reach=Math.Min(1,u*1.3f);if(s>reach)continue;var p=Vector3.Lerp(from,centre,s)+RightOfLine(from,centre)*(float)Math.Sin(s*7+m+t*2)*.35f*(1-s);p.Y=floor+.06f+.1f*s;fx.Smoke(p,Ink,.85f*gone*(.4f+.6f*s),.55f+.5f*(1-s),.3f,12100+m*20+i,floor-.1f);}}
            }
            // The void: the dark gathered into a black core against a violet glare behind it, motes falling in.
            float vs=VoidSize(t);
            if(vs>.01f)
            {
                fx.Glow(Void-Fwd*.6f,Rim*(1.2f*vs),0,2.6f*vs,0,0,12200);fx.Glow(Void-Fwd*.9f,Halo*(.5f*vs),0,4.2f*vs,0,0,12201);
                for(int i=0,n=N(26,q);i<n;i++){var d=NativeEntranceMath.Direction(12210,i);var p=Void+d*(.55f*vs*NativeEntranceMath.Rand(12211,i))+new Vector3(0,(float)Math.Sin(t*1.7f+i)*.05f,0);fx.Smoke(p,Ink,.95f*vs,.9f*vs,.45f+.1f*(float)Math.Sin(t+i),12210+i,floor);}
                for(int i=0,n=N(20,q);i<n;i++){float u=((t*.7f+i/(float)n)%1),a=i*2.4f+u*4;float r=2.4f*(1-u)*vs+.3f;var p=Void+(Left*(float)Math.Cos(a)+Vector3.UnitY*(float)Math.Sin(a))*r+Fwd*.2f;fx.Glow(p,Mote*(.5f*u*vs),0,.03f,0,u,12260+i);}
            }
            // Stepping out: the dark running off the body and dripping from it.
            if(t>=OpenAt&&t<StepFrom+1.8f)
            {
                float on=1-NativeEntranceMath.Ramp(t,StepFrom+1f,StepFrom+1.8f);
                for(int i=0,n=N(22,q);i<n;i++)
                {
                    var src=(NativeEntranceAnchor)(i%5==0?(int)NativeEntranceAnchor.Head:i%5==1?(int)NativeEntranceAnchor.LeftShoulder:i%5==2?(int)NativeEntranceAnchor.RightShoulder:i%5==3?(int)NativeEntranceAnchor.LeftHand:(int)NativeEntranceAnchor.RightHand);
                    float u=((t*1.6f+i/(float)n)%1);var p=Anchor(src,t-u*.3f)-Fwd*(.25f*u)+new Vector3(0,-.6f*u*u,0);if(p.Y<floor)continue;fx.Smoke(p,Ink,.8f*on*(1-u),.12f+.18f*u,u,12300+i,floor);
                }
            }
            // The eye: one point of light on the visor, a star of glints when it lights.
            if(t>=EyeAt-.02f)
            {
                var eye=Eye(t);float lit=NativeEntranceMath.Ramp(t,EyeAt-.02f,EyeAt+.06f),flare=Flash(t,EyeAt,.02f,.3f);
                fx.Glow(eye,Vector3.One*(1.8f*lit)+Halo*(.8f*lit),0,.035f,1,0,12400);fx.Glow(eye,Halo*(.5f*lit+1.4f*flare),0,.14f+.2f*flare,0,0,12401);
                if(flare>.01f)Star(fx,view,eye+ForwardOf(t)*.02f,Halo*(2.2f*flare),.32f*flare,RightOf(t),Vector3.UnitY,12402);
            }
            // The wings: the dark spreading behind the shoulders in two sweeps, violet along their edges; the snap back to the monoliths.
            float wings=NativeEntranceMath.Ramp(t,WingsAt,WingsAt+.35f)*(1-NativeEntranceMath.Ramp(t,SnapAt-.02f,SnapAt+.12f));
            if(wings>.01f)for(int side=0;side<2;side++)
            {
                var sh=Anchor(side==0?NativeEntranceAnchor.LeftShoulder:NativeEntranceAnchor.RightShoulder,t);var outward=(side==0?1:-1)*RightOf(t)*-1;
                for(int i=0,n=N(14,q);i<n;i++){float u=(i+.5f)/n,span=1.9f*wings;var p=sh-ForwardOf(t)*(.25f+.2f*u)+outward*(span*u)+new Vector3(0,.5f*(float)Math.Sin(Math.PI*u)*wings-.6f*u*u,0)+new Vector3(0,(float)Math.Sin(t*3+i)*.03f,0);fx.Smoke(p,Ink,.85f*wings,.35f+.25f*(1-u),.4f,12500+side*30+i,floor);if(i%2==0)fx.Glow(p+new Vector3(0,.18f,0),Rim*(.35f*wings),0,.12f,0,0,12560+side*30+i);}
            }
            float snap=t-SnapAt;
            if(snap>=0&&snap<.8f){float u=snap/.8f;for(int m=0;m<6;m++){var to=Monolith(m,floor+.05f);var d=Vector3.Normalize(Flat(to));var head=d*(1+10*NativeEntranceMath.EaseOut(u))+new Vector3(0,floor+.05f,0);Beam(fx,view,head,head-d*(1.6f*(1-u)+.2f),Rim*(1.6f*(1-u)),.05f,.6f,u,12600+m);}}
            NativeEntranceFx.GroundDust(fx,t,SnapAt,new Vector3(0,floor,0),N(14,q),.4f,4,1,Hex(0x1a1024),.6f,.6f,12650,q,floor);
        }
        private static Vector3 RightOfLine(Vector3 a,Vector3 b){var d=b-a;d.Y=0;if(d.LengthSquared()<1e-6f)return Vector3.UnitX;d=Vector3.Normalize(d);return new Vector3(d.Z,0,-d.X);}
    }
}
