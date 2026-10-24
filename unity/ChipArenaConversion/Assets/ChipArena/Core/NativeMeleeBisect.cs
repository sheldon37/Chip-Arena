using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A lethal blade blow cuts the body's own mesh along the swing (presentation only): the plan NativeMeleeSlice makes from
    /// the killing blow's replicated swing and the Hit event's direction (its plane, where it meets the body, the blade's
    /// travel and the part it takes) becomes a <see cref="BisectRequest"/> for the shared split (NativeBisect: sealed caps,
    /// two half corpses, the seam's blood). It used to only pick ragdoll joints to detach whole.
    /// <list type="bullet">
    /// <item>Through the body (a sword's trunk cut, every greatsword cleave, a sword or greatsword chop): the whole body is cut
    /// by the swing's plane. Half 0 keeps the pelvis; half 1, the part the blade took off, slides away along the swing (down
    /// the cut when it slopes) and topples, or, for a top-down chop, the halves fall apart to the sides.</item>
    /// <item>A part (a sword's head or arm cut, a dagger's limb, a dagger chop's arm): only that part's bodies are cut (the
    /// request's <see cref="BisectRequest.Mask"/>), at the point the swing's plane crosses it (kept inside the limb), with the
    /// swing's bevel kept within <see cref="LimbTilt"/> (<see cref="NeckTilt"/> for the neck) of square across it. The part
    /// flies along the swing as the old severed joint did.</item>
    /// <item>No mesh cut: a dagger's slit throat (no part comes off) and every shield kill (a crush, or the thrown edge's
    /// severed joint) keep the joint presentation; so does a death this does not apply to (gore off, a kill effect).</item>
    /// </list>
    /// Pure and deterministic: the same plan, joints, velocity and seed give the same request on every peer.
    /// </summary>
    public static class NativeMeleeBisect
    {
        /// <summary>Most a part cut's plane leans off square across the limb (degrees), and across the neck.</summary>
        public const float LimbTilt=35,NeckTilt=15;
        /// <summary>Seconds the cut holds before the parts separate, by weapon: the blade's pass, felt as a hit-stop.</summary>
        public static float Hitch(string spec,bool chop)=>(spec=="greatsword"?.1f:spec=="daggers"?.045f:.06f)+(chop?.03f:0);
        /// <summary>The blade's speed through the body (m/s) by weapon.</summary>
        public static float Speed(string spec)=>spec=="greatsword"?7.5f:spec=="daggers"?10:9;
        /// <summary>Whether this plan cuts the mesh: a blade (sword, greatsword, daggers) that takes a part or goes through.</summary>
        public static bool Cuts(SlicePlan plan)=>plan!=null&&plan.Any&&(plan.Spec=="sword"||plan.Spec=="greatsword"||plan.Spec=="daggers")&&plan.Kind!=SliceKind.Slit&&plan.Kind!=SliceKind.Crush&&plan.Count>0;
        /// <summary>The ragdoll part root the cut takes off alone, or -1 when it goes through the body.</summary>
        public static int Piece(SlicePlan plan)
        {
            if(!Cuts(plan))return -1;
            if(plan.Kind==SliceKind.Cleave)return -1;
            if(plan.Kind==SliceKind.Chop)
            {
                if(plan.Spec!="daggers")return -1;
                for(int i=0;i<plan.Count;i++)if(Limb(plan.Joints[i]))return plan.Joints[i];return CapturedRig.Head;
            }
            int j=plan.Joints[0];return j==CapturedRig.Head||Limb(j)?j:-1;
        }
        private static bool Limb(int j)=>j==CapturedRig.LArm||j==CapturedRig.RArm||j==CapturedRig.LElbow||j==CapturedRig.RElbow;
        /// <summary>The bodies a part cut crosses: the head with the neck under it (on the spine's body), an upper arm with its
        /// forearm, a forearm alone.</summary>
        public static uint MaskOf(int piece)=>piece<0?uint.MaxValue:piece==CapturedRig.Head?NativeBisect.MaskOf(CapturedRig.Spine,CapturedRig.Head):piece==CapturedRig.LArm||piece==CapturedRig.RArm?NativeBisect.MaskOf(piece,piece+1):NativeBisect.MaskOf(piece);
        /// <summary>Where along the part (from its root joint to the next) the cut may fall.</summary>
        private static void Span(int piece,out int from,out int to,out float lo,out float hi)
        {
            if(piece==CapturedRig.Head){from=CapturedRig.Neck;to=CapturedRig.Head;lo=.35f;hi=.85f;return;}
            from=piece;to=piece+1;lo=.3f;hi=.7f;
        }
        /// <summary>
        /// The cut for `plan` on the posed body: `joints` each joint's Unity world position as the corpse is built (22, the
        /// corpse's bind), `velocity` the body's at death (NativeDeath.velocity), `seed` the death's. Null when the plan cuts no
        /// mesh (<see cref="Cuts"/>).
        /// </summary>
        public static BisectRequest Request(SlicePlan plan,Vector3[] joints,Vector3 velocity,uint seed)
        {
            if(!Cuts(plan)||joints==null||joints.Length<22)return null;
            string spec=plan.Spec;int piece=Piece(plan);bool chop=plan.Kind==SliceKind.Chop;
            var n=CapturedRig.Unit(plan.Normal,Vector3.UnitY);var point=plan.Point;var up=Vector3.UnitY;
            var t=plan.Travel-n*Vector3.Dot(plan.Travel,n);t=CapturedRig.Unit(t,CapturedRig.Unit(Vector3.Cross(up,n),Vector3.UnitX));
            if(!Finite(velocity))velocity=Vector3.Zero;if(velocity.Length()>12)velocity=Vector3.Normalize(velocity)*12;
            uint h=unchecked(seed*2654435761u);h^=h>>15;h*=0x5bd1e995u;h^=h>>13;float r=(h&0xffff)/65536f;
            float seamHalf;
            if(piece<0)
            {
                // Through the body: half 0 keeps the pelvis.
                if(Vector3.Dot(joints[CapturedRig.Pelvis]-point,n)>0)n=-n;seamHalf=.21f;
            }
            else
            {
                Span(piece,out int from,out int to,out float lo,out float hi);var a=joints[from];var b=joints[to];
                var axis=CapturedRig.Unit(b-a,up);float s0=Vector3.Dot(n,a-point),s1=Vector3.Dot(n,b-point);
                float u=Math.Abs(s0-s1)>1e-5f?s0/(s0-s1):.5f;if(float.IsNaN(u))u=.5f;u=Math.Max(lo,Math.Min(hi,u));point=a+(b-a)*u;
                // The swing's bevel, facing the part, kept within the tilt of square across it.
                if(Vector3.Dot(n,axis)<0)n=-n;float tilt=(piece==CapturedRig.Head?NeckTilt:LimbTilt)*(float)Math.PI/180,cos=Vector3.Dot(n,axis);
                if(cos<Math.Cos(tilt))
                {
                    var lean=n-axis*cos;lean=CapturedRig.Unit(lean,CapturedRig.Unit(Vector3.Cross(axis,Math.Abs(axis.Y)<.9f?up:Vector3.UnitX),Vector3.UnitX));
                    n=Vector3.Normalize(axis*(float)Math.Cos(tilt)+lean*(float)Math.Sin(tilt));
                }
                t=t-n*Vector3.Dot(t,n);t=CapturedRig.Unit(t,CapturedRig.Unit(Vector3.Cross(axis,up),Vector3.UnitX));seamHalf=piece==CapturedRig.Head?.07f:.05f;
            }
            float speed=Speed(spec),hitch=Hitch(spec,chop);var travel=t*speed;var carry=velocity*.6f;
            var q=new BisectRequest{Cause="melee-"+spec+"-"+(piece<0?(chop?"chop":"through"):piece==CapturedRig.Head?"head":"limb"),Point=point,Normal=n,Travel=travel,Hitch=hitch,Blood=plan.Blood,Sparks=false,Seed=seed,
                Mask=MaskOf(piece),Seam=t,SeamHalf=seamHalf};
            if(piece>=0)
            {
                // The part flies along the swing with the old severed joint's kick (NativeCorpse's slice), spinning about the cut;
                // the body goes on as it was going.
                var away=point-joints[CapturedRig.Spine];away.Y=0;away=away.LengthSquared()>1e-4f?Vector3.Normalize(away):t;
                q.Away1=carry+t*plan.Kick*(.85f+.3f*r)+away*1.1f+up*(piece==CapturedRig.Head?2.4f:1.4f);
                q.Spin1=CapturedRig.Unit(Vector3.Cross(n,t),up)*plan.Spin+n*(r-.5f)*4;q.Away0=carry;q.Spin0=Vector3.Zero;
            }
            else if(Math.Abs(n.Y)<.35f)
            {
                // Top-down: the halves fall apart to the sides and tip outward, as the slot saw's do.
                var tip=CapturedRig.Unit(Vector3.Cross(up,n),Vector3.Zero);float part=spec=="greatsword"?1.5f:1.2f;
                q.Away1=carry+n*part+up*.45f+travel*.08f;q.Away0=carry-n*part+up*.45f+travel*.08f;q.Spin1=tip*2.3f;q.Spin0=-tip*2.3f;
            }
            else
            {
                // Across: the part above the cut slides off along the swing, down the cut where it slopes, and topples the way the
                // blade went; the rest goes on, given a little of the blow.
                var down=-up-n*Vector3.Dot(-up,n);down=down.LengthSquared()>1e-4f?Vector3.Normalize(down):Vector3.Zero;
                float slide=spec=="greatsword"?.7f:.55f;
                q.Away1=carry+t*plan.Kick*slide+down*.9f+up*.35f;q.Spin1=CapturedRig.Unit(Vector3.Cross(up,t),Vector3.Zero)*(spec=="greatsword"?3.2f:2.6f)+n*(r-.5f)*1.5f;
                q.Away0=carry+t*.4f;q.Spin0=Vector3.Zero;
            }
            return q;
        }
        private static bool Finite(Vector3 v)=>!(float.IsNaN(v.X)||float.IsNaN(v.Y)||float.IsNaN(v.Z)||float.IsInfinity(v.X)||float.IsInfinity(v.Y)||float.IsInfinity(v.Z));
    }
}
