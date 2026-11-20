using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The ranged weapons' v2 intros (I4, owner 2026-11-20; docs/INTROS-RANGED.md): Sundown (pistol), One Breath (rifle), Brass Storm
    /// (machine gun) and Lightfall (bow). What the four share: the set bank (ChipArenaIntroRanged, modelled by tools/intros/ranged_set.py
    /// in stagekit's Unity space with the pilot at the origin facing +z, drawn turned by Facing), the hands the runtime hands in, the
    /// camera as the effects need it (the streak rule: every streak over a metre laid on its head's camera plane), and a few effects.
    /// Presentation only: pure functions of the presentation clock, no allocation per frame.
    /// </summary>
    public abstract class NativeRangedIntro:NativeIntroV2Script,INativeIntroHands
    {
        public const string Bank="ChipArenaIntroRanged";
        /// <summary>The model's eyes above the head anchor (NativeEntranceAnchor.Head and the keyed head joint are the skull's pivot at
        /// its base; I5 measured it in captures, integrator 2026-11-20): anything at the eyes or the mouth lifts by this along the head's up.</summary>
        protected const float ModelHeadLift=.16f;
        public override string SetBank=>Bank;
        protected NativeRangedIntro(string id,float total,float cardAt,float hit,int accent):base(id,total,cardAt,hit,accent){}

        // ---- the set's frame ----
        /// <summary>A set point authored in the bank's Unity space (ux, uy, uz) in the source frame, the set turned by Facing.</summary>
        public static Vector3 S(float ux,float uy,float uz)=>NativeEntranceMath.RotateY(new Vector3(-ux,uy,uz),Facing);
        public static Vector3 S(Vector3 u)=>S(u.X,u.Y,u.Z);
        /// <summary>The set's turn (a whole-set piece goes in at the origin with it).</summary>
        public static readonly Quaternion Turn=NativeEntranceMath.Yaw(Facing);
        /// <summary>A direction authored in Unity space, in the source frame.</summary>
        public static Vector3 SDir(float ux,float uy,float uz)=>NativeEntranceMath.RotateY(new Vector3(-ux,uy,uz),Facing);
        /// <summary>A turn about the set's vertical by `a` (radians, + toward the source frame's left) after the set's own turn.</summary>
        public static Quaternion SYaw(float a)=>NativeEntranceMath.Yaw(Facing+a);
        /// <summary>The pilot's local frame (x its left, z its front) at the end facing.</summary>
        public static Vector3 L(float x,float y,float z)=>Local(x,y,z);
        protected static readonly float Floor=NativeEntranceStage.FloorY;

        // ---- the hands (Runtime IntroV2Hands; a checker never hands them in, so every effect also works from the keyed rig) ----
        private NativeIntroHand right,left;private float handsAt=-1;
        public void Hands(float t,in NativeIntroHand r,in NativeIntroHand l){right=r;left=l;handsAt=t;}
        /// <summary>The drawn gun's muzzle and bore at `t` (Runtime: the frame the model's weapon is drawn on, as the match's muzzle
        /// flash reads it); false when the runtime has not handed one in for `t` (a checker, the Classic body, a bow).</summary>
        protected bool Drawn(float t,out Vector3 muzzle,out Vector3 bore)
        {
            if(Math.Abs(handsAt-t)<1e-4f&&right.HasMuzzle&&right.Bore.LengthSquared()>.5f){muzzle=right.Muzzle;bore=Vector3.Normalize(right.Bore);return true;}
            muzzle=bore=default;return false;
        }
        /// <summary>The drawn right (or left) wrist at `t`: the runtime's when it handed them in for `t`, else the keyed rig's.</summary>
        protected Vector3 Wrist(float t,bool leftHand=false)
        {
            if(Math.Abs(handsAt-t)<1e-4f)return leftHand?left.At:right.At;
            return Anchor(leftHand?NativeEntranceAnchor.LeftHand:NativeEntranceAnchor.RightHand,t);
        }

        // ---- the camera, as the effects need it ----
        /// <summary>The eye and its forward at presentation time `t` (what Camera shows; held from SettleAt).</summary>
        protected void View(float t,out Vector3 eye,out Vector3 forward)
        {
            float settle=Clock(SettleAt);Rig.Evaluate(this,Math.Min(t,settle),Context.Reduced,out var view);eye=view.Eye;
            if(eye.Y<Floor+.06f)eye.Y=Floor+.06f;var f=view.Look-eye;forward=f.LengthSquared()>1e-8f?Vector3.Normalize(f):Vector3.UnitZ;
        }
        /// <summary>A streak from `tail` to `head`, laid on the head's camera plane when it is longer than a metre
        /// (NativeCacheOpenFx.OntoHeadPlane: the LEGENDARY beam fix), so its ends stay on their line at any angle.</summary>
        protected static void Line(INativeEntranceFx fx,Vector3 eye,Vector3 forward,Vector3 head,Vector3 tail,Vector3 color,float width,float taper,float u,int seed)
        {
            var axis=head-tail;float len=axis.Length();if(len<1e-4f)return;
            if(len>.9f)
            {
                float ax=axis.X,ay=axis.Y,az=axis.Z,tp=taper;
                if(!NativeCacheOpenFx.OntoHeadPlane(eye.X,eye.Y,eye.Z,forward.X,forward.Y,forward.Z,.08f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref tp))return;
                axis=new Vector3(ax,ay,az);taper=tp;
            }
            fx.Streak(head,axis,color,width,0,1,taper,u,seed);
        }
        /// <summary>A four-point star in the camera's plane (so its long arms obey the streak rule).</summary>
        protected static void Star(INativeEntranceFx fx,Vector3 eye,Vector3 forward,Vector3 at,Vector3 color,float size,float turn,int seed)
        {
            if(size<=1e-3f)return;var right=Vector3.Cross(forward,Vector3.UnitY);if(right.LengthSquared()<1e-6f)right=Vector3.UnitX;right=Vector3.Normalize(right);var up=Vector3.Cross(right,forward);
            float c=(float)Math.Cos(turn),s=(float)Math.Sin(turn);var a=right*c+up*s;var b=up*c-right*s;
            fx.Glow(at,color,0,size*.55f,1,0,seed);
            fx.Streak(at+a*size,a*(size*2),color*1.3f,size*.05f,0,1,0,0,seed+1);
            fx.Streak(at+b*(size*.75f),b*(size*1.5f),color*1.1f,size*.045f,0,1,0,0,seed+2);
        }
        /// <summary>A pistol's, rifle's or machine gun's muzzle flash at `at` along `dir`, born at `born` (Reduced: no white core
        /// strobe, a softer bloom): a hot core, a short cone of flame streaks (each under a metre) and a smoke wisp.</summary>
        protected void Muzzle(INativeEntranceFx fx,float t,float born,Vector3 at,Vector3 dir,float size,Vector3 hot,int seed,float q,float life=.06f)
        {
            float a=t-born;if(a<0||a>life)return;float k=1-a/life;if(Context.Reduced)k*=.45f;
            fx.Glow(at+dir*(size*.25f),Vector3.One*(4.5f*k),0,size*.9f,1,0,seed);fx.Glow(at+dir*(size*.6f),hot*(2.6f*k),0,size*2.0f,0,0,seed+1);
            int n=q<.5f?4:8;
            for(int i=0;i<n;i++)
            {
                var d=Vector3.Normalize(dir*(1.2f+NativeEntranceMath.Rand(seed,i))+NativeEntranceMath.Direction(seed,i+3)*.45f);float len=size*(.8f+1.2f*NativeEntranceMath.Rand(seed,i+9));
                fx.Streak(at+d*len,d*len,Vector3.Lerp(Vector3.One*3.5f,hot*2.6f,NativeEntranceMath.Rand(seed,i+20))*k,size*.14f,0,1,.7f,0,seed+2+i);
            }
        }
        /// <summary>Drifting motes in a box round `centre` (dust in the low sun, rain-mist, gun smoke grit), at most `n`.</summary>
        protected static void Motes(INativeEntranceFx fx,float t,float q,Vector3 centre,Vector3 half,Vector3 tint,float size,int n,int seed,float drift=.15f)
        {
            int m=q<.5f?n/2:n;
            for(int i=0;i<m;i++)
            {
                var p=centre+new Vector3(NativeEntranceMath.Signed(seed,i*3)*half.X,NativeEntranceMath.Signed(seed,i*3+1)*half.Y,NativeEntranceMath.Signed(seed,i*3+2)*half.Z);
                p+=new Vector3((float)Math.Sin(t*.31f+i)*drift,(float)Math.Sin(t*.23f+i*1.7f)*drift*.7f,(float)Math.Cos(t*.27f+i*.6f)*drift);
                float tw=.55f+.45f*(float)Math.Sin(t*1.1f+i*2.3f);fx.Glow(p,tint*tw,0,size,0,0,seed+i);
            }
        }
        /// <summary>Linear rgb from a hex colour.</summary>
        protected static Vector3 H(int hex,float gain=1)=>NativeEntranceFx.Hex(hex,gain);
        /// <summary>A quaternion whose +z is `forward` and whose +y leans to `up`.</summary>
        public static Quaternion Look(Vector3 forward,Vector3 up)
        {
            var z=Vector3.Normalize(forward);var x=Vector3.Cross(up,z);if(x.LengthSquared()<1e-8f)x=Vector3.Cross(Vector3.UnitX,z);x=Vector3.Normalize(x);var y=Vector3.Cross(z,x);
            var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
        }
        /// <summary>A ballistic point: from `p` with velocity `v` after `a` s under gravity `g` (closed form).</summary>
        protected static Vector3 Ballistic(Vector3 p,Vector3 v,float a,float g=9.8f)=>p+v*a+new Vector3(0,-.5f*g*a*a,0);
    }
}
