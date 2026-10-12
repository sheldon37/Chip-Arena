using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The plunging stab's poses (docs/MELEE-FEEL.md "Plunging stab"): per weapon a blade-down tuck over the .08 s windup, the
    /// committed drop (blade first under the pilot, legs drawn up, head down at the target), the plant (the blade driven into
    /// the floor ahead of the feet, the hips dropped into a deep crouch on planted soles, deeper for a bigger fall), the stuck
    /// hold with a strain, the pull-out timed to the recovery (the greatsword wrenches its blade free last) and the settle back
    /// to the guard. The explosive pack has no downward or plunge take (its Air-Attack takes all chop forward), so the keys are
    /// authored here as hand targets, blade directions and leg targets in the rig's group space, solved through the rig's own
    /// IK; the hand's roll follows the weapon's captured guard (a minimal turn of its blade onto the key's), so the grips keep
    /// the captured hands. Weapons:
    ///  - daggers: both reverse-grip blades point down, driven in side by side;
    ///  - sword: a one-handed downward stab, the free fist out for balance;
    ///  - shield: the heater's bottom rim leads and slams in, the free arm drawn back (the side sword stays sheathed on its back);
    ///  - greatsword: a two-handed reverse grip, point down, planted deep and angled ahead.
    /// Presentation only: positions in the rig's frame (MeleeRig: +x left, +z forward), no authority reads, no allocation.
    /// </summary>
    public static class NativePlungePose
    {
        /// <summary>One authored pose. Group space (the pilot's root frame at rest height; Body is the hips' drop).</summary>
        public struct Key
        {
            /// <summary>Main hand: the grip (for a plant key, the contact's point on the floor: x and z; y from Bury), the blade
            /// direction (the shield: its face), the shield's top direction (unused otherwise) and the elbow pole.</summary>
            public Vector3 Grip,Blade,Up,Pole;
            /// <summary>Left hand: the daggers' second blade (grip, blade direction, pole); the sword's and shield's free fist.</summary>
            public Vector3 LGrip,LBlade,LPole;
            /// <summary>The hips' drop (m), the body's pitch, the spine's and chest's forward bow, the head's pitch down (rad).</summary>
            public float Body,Pitch,Bow,Chest,Head;
            /// <summary>Ankle targets, knee hints and the feet's pitch (toe down positive).</summary>
            public Vector3 LFoot,RFoot,LKnee,RKnee;public float LPitch,RPitch;
            public static Key Lerp(in Key a,in Key b,float t)
            {
                Vector3 L(Vector3 x,Vector3 y)=>x+(y-x)*t;float F(float x,float y)=>x+(y-x)*t;
                return new Key{Grip=L(a.Grip,b.Grip),Blade=MeleeMath.Unit(L(a.Blade,b.Blade)),Up=MeleeMath.Unit(L(a.Up,b.Up)),Pole=L(a.Pole,b.Pole),LGrip=L(a.LGrip,b.LGrip),LBlade=MeleeMath.Unit(L(a.LBlade,b.LBlade)),LPole=L(a.LPole,b.LPole),
                    Body=F(a.Body,b.Body),Pitch=F(a.Pitch,b.Pitch),Bow=F(a.Bow,b.Bow),Chest=F(a.Chest,b.Chest),Head=F(a.Head,b.Head),LFoot=L(a.LFoot,b.LFoot),RFoot=L(a.RFoot,b.RFoot),LKnee=L(a.LKnee,b.LKnee),RKnee=L(a.RKnee,b.RKnee),LPitch=F(a.LPitch,b.LPitch),RPitch=F(a.RPitch,b.RPitch)};
            }
        }
        /// <summary>One weapon's keys: the tuck, the drop and the plant, how deep the contact goes in (m, plus HeavyBury for an 8 m
        /// fall), how far the hips may drop further to reach it, and the contact's point in the weapon's frame.</summary>
        public sealed class Keys
        {
            /// <summary>Raise: the windup's first half, the blade brought forward out of the guard (so a guard held up and back,
            /// the greatsword's, swings over the top and down in front, never back through the body); the tuck its second half.</summary>
            public Key Raise,Tuck,Drop,Plant;public float Bury,HeavyBury,HeavyCrouch;public Vector3 Contact;public bool Shield,Dual,TwoHand;
        }
        /// <summary>The sole's height in group space (the ankle's FLOOR less the foot) and the deepest extra hips drop the plant may
        /// take to reach its contact.</summary>
        public const float Sole=MeleeRig.FLOOR-.07f,MaxReachDrop=.16f,ReachMargin=.025f;
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static Vector3 U(float x,float y,float z)=>Vector3.Normalize(new Vector3(x,y,z));
        private const float F=MeleeRig.FLOOR;
        // Air legs: knees drawn up toward the chest, shins folded under (the committed drop draws them higher than the tuck).
        private static Key AirLegs(Key k,float lift)
        {
            k.LFoot=V(.13f,-.57f+lift,.07f);k.RFoot=V(-.12f,-.53f+lift,-.09f);k.LKnee=V(.15f,-.34f+lift*.6f,.30f);k.RKnee=V(-.14f,-.35f+lift*.6f,.24f);k.LPitch=.55f;k.RPitch=.65f;return k;
        }
        // Planted stance: lead (left) foot forward and flat, rear foot back on its toes (the greatsword nearly kneels).
        private static Key Stance(Key k,float leadX,float leadZ,float rearX,float rearZ,float rearUp,float rearPitch)
        {
            k.LFoot=V(leadX,F,leadZ);k.RFoot=V(rearX,F+rearUp,rearZ);k.LKnee=V(leadX+.03f,-.5f,leadZ+.4f);k.RKnee=V(rearX,-.75f,rearZ+.18f);k.LPitch=0;k.RPitch=rearPitch;return k;
        }
        public static readonly Keys Sword=new Keys
        {
            Contact=V(0,0,1.28f),Bury=.2f,HeavyBury=.08f,HeavyCrouch=.06f,
            Tuck=AirLegs(new Key{Grip=V(-.14f,.30f,.30f),Blade=U(.03f,-.9f,.42f),Pole=V(-1,.1f,-.3f),LGrip=V(.36f,.02f,.16f),LBlade=U(0,-1,.2f),LPole=V(1,-.6f,-.2f),Body=.02f,Pitch=0,Bow=.12f,Chest=.08f,Head=.25f},0),
            Drop=AirLegs(new Key{Grip=V(-.08f,-.16f,.36f),Blade=U(0,-.93f,.36f),Pole=V(-1,-.4f,-.2f),LGrip=V(.33f,-.02f,-.16f),LBlade=U(0,-1,-.3f),LPole=V(1,-.5f,.3f),Body=0,Pitch=.12f,Bow=.32f,Chest=.18f,Head=.45f},.07f),
            Plant=Stance(new Key{Grip=V(-.06f,0,.95f),Blade=U(0,-.8f,.6f),Pole=V(-1,-.5f,-.1f),LGrip=V(.30f,-.30f,-.08f),LBlade=U(0,-1,0),LPole=V(1,-.6f,.2f),Body=-.30f,Pitch=.08f,Bow=.36f,Chest=.18f,Head=-.05f},.16f,.24f,-.15f,-.30f,.035f,.5f),
        };
        public static readonly Keys Daggers=new Keys
        {
            Contact=V(0,0,.84f),Bury=.13f,HeavyBury=.05f,HeavyCrouch=.05f,Dual=true,
            Tuck=AirLegs(new Key{Grip=V(-.18f,.42f,.16f),Blade=U(-.05f,-.75f,.66f),Pole=V(-1,.3f,-.2f),LGrip=V(.18f,.42f,.16f),LBlade=U(.05f,-.75f,.66f),LPole=V(1,.3f,-.2f),Body=.02f,Bow=.05f,Chest=0,Head=.2f},0),
            Drop=AirLegs(new Key{Grip=V(-.13f,-.26f,.30f),Blade=U(-.06f,-.96f,.27f),Pole=V(-1,-.4f,-.2f),LGrip=V(.13f,-.26f,.30f),LBlade=U(.06f,-.96f,.27f),LPole=V(1,-.4f,-.2f),Body=0,Pitch=.15f,Bow=.38f,Chest=.2f,Head=.5f},.08f),
            Plant=Stance(new Key{Grip=V(-.21f,0,.56f),Blade=U(-.12f,-.93f,.35f),Pole=V(-1,-.5f,-.2f),LGrip=V(.21f,0,.56f),LBlade=U(.12f,-.93f,.35f),LPole=V(1,-.5f,-.2f),Body=-.34f,Pitch=.1f,Bow=.5f,Chest=.25f,Head=.1f},.23f,.08f,-.21f,-.06f,0,0),
        };
        public static readonly Keys Shield=new Keys
        {
            Contact=V(0,-.34f,.22f),Bury=.1f,HeavyBury=.05f,HeavyCrouch=.05f,Shield=true,
            Tuck=AirLegs(new Key{Grip=V(-.08f,.20f,.30f),Blade=U(0,.15f,.99f),Up=U(0,.99f,-.15f),Pole=V(-1,0,-.3f),LGrip=V(.33f,.04f,-.04f),LBlade=U(0,-1,.1f),LPole=V(1,-.6f,.2f),Body=.02f,Bow=.1f,Chest=.05f,Head=.2f},0),
            Drop=AirLegs(new Key{Grip=V(-.06f,-.30f,.36f),Blade=U(0,-.30f,.95f),Up=U(0,.95f,.30f),Pole=V(-1,-.5f,-.1f),LGrip=V(.33f,-.06f,-.22f),LBlade=U(0,-1,-.3f),LPole=V(1,-.4f,.4f),Body=0,Pitch=.1f,Bow=.35f,Chest=.15f,Head=.4f},.07f),
            Plant=Stance(new Key{Grip=V(-.04f,0,.48f),Blade=U(0,-.25f,.97f),Up=U(0,.97f,.25f),Pole=V(-1,-.5f,-.1f),LGrip=V(.30f,-.26f,-.26f),LBlade=U(0,-1,-.2f),LPole=V(1,-.4f,.4f),Body=-.36f,Pitch=.1f,Bow=.5f,Chest=.2f,Head=.05f},.17f,.22f,-.17f,-.28f,.035f,.5f),
        };
        public static readonly Keys Greatsword=new Keys
        {
            Contact=V(0,0,1.69f),Bury=.36f,HeavyBury=.1f,HeavyCrouch=.06f,TwoHand=true,
            Tuck=AirLegs(new Key{Grip=V(-.02f,.50f,.18f),Blade=U(0,-.93f,.37f),Pole=V(-1,.5f,-.1f),Body=.02f,Pitch=-.05f,Bow=.05f,Chest=0,Head=.15f},0),
            Drop=AirLegs(new Key{Grip=V(-.02f,-.02f,.34f),Blade=U(0,-.97f,.24f),Pole=V(-1,-.3f,-.2f),Body=0,Pitch=.12f,Bow=.3f,Chest=.15f,Head=.45f},.06f),
            Plant=Stance(new Key{Grip=V(0,0,1.02f),Blade=U(0,-.82f,.57f),Pole=V(-1,-.4f,-.1f),Body=-.40f,Pitch=.12f,Bow=.52f,Chest=.2f,Head=.1f},.18f,.26f,-.16f,-.36f,.045f,.6f),
        };
        private static Keys Raised(Keys k,Vector3 blade){k.Raise=k.Tuck;k.Raise.Blade=blade;return k;}
        static NativePlungePose(){Raised(Sword,U(0,.35f,.94f));Raised(Greatsword,U(0,.45f,.89f));Daggers.Raise=Daggers.Tuck;Shield.Raise=Shield.Tuck;}
        public static Keys For(string spec)=>spec=="daggers"?Daggers:spec=="shield"?Shield:spec=="greatsword"?Greatsword:spec=="sword"?Sword:null;

        // ---- the pull-out ----
        /// <summary>How far the grip draws back along the blade by the time it is free (m beyond the bury), the hips' rise and the
        /// greatsword's wrench (rad about the lateral axis at the contact, two rocks before the yank).</summary>
        public const float PullClear=.06f,PullRise=.07f,Wrench=.2f;
        /// <summary>The key at the moment `c` (windup, drop, plant, stuck, pull or settle; the windup's and settle's guard blends
        /// are the caller's) and the contact's depth below the sole while stuck (negative: in the floor).</summary>
        public static Key At(Keys w,in NativePlungeClock c,string spec,out float depth,out float wrench,out float guard)
        {
            depth=float.NaN;wrench=0;guard=0;
            switch(c.Phase)
            {
                // The windup: out of the guard into the raise over its first half, then the raise turns point-down into the tuck.
                case NativePlungePhase.Windup:if(c.Tuck<.5f){guard=1-MeleeMath.Smooth(c.Tuck/.5f);return w.Raise;}return Key.Lerp(w.Raise,w.Tuck,MeleeMath.Smooth((c.Tuck-.5f)/.5f));
                case NativePlungePhase.Drop:return Key.Lerp(w.Tuck,w.Drop,c.Commit);
            }
            if(c.Phase==NativePlungePhase.Settle)guard=c.Settle;
            var k=w.Plant;float bury=w.Bury+w.HeavyBury*c.Heavy;k.Body-=w.HeavyCrouch*c.Heavy;
            // The plant: from the drop's pose (its last frame) the blade drives in as the hips drop.
            // The blade's point meets the floor a frame before the feet (it leads the drop): it is part way in at the landing.
            if(c.Phase==NativePlungePhase.Plant)
            {
                depth=-bury*(.55f+.45f*c.Plant);var k0=w.Drop;k0.Grip=DropContact(w);k0.LGrip=DropContact(w,true);
                k0.LFoot=k.LFoot;k0.RFoot=k.RFoot;k0.LKnee=k.LKnee;k0.RKnee=k.RKnee;k0.LPitch=k.LPitch;k0.RPitch=k.RPitch;return Key.Lerp(k0,k,c.Plant);
            }
            // Stuck: a strain held over the planted blade, shaking off as the pull-out nears.
            float shake=c.Phase==NativePlungePhase.Stuck?(float)Math.Sin(c.Land*60)*Math.Max(0,1-(c.Land-.05f)/.25f):0;
            k.Body+=.006f*shake;k.Chest+=NativePlungeRules.Strain*shake;
            float pull=c.Phase>=NativePlungePhase.Pull?c.Pull:0;
            if(spec=="greatsword"&&pull>0){float u=Math.Min(1,pull/.6f);wrench=Wrench*(float)Math.Sin(u*MeleeMath.TAU*2)*(1-u);pull=MeleeMath.Smooth((pull-.45f)/.55f);}
            depth=-bury+(bury+PullClear)*pull;
            k.Body+=PullRise*pull;k.Bow-=.12f*pull;k.Head+=.05f*pull;
            return k;
        }

        /// <summary>Where the drop key's contact is (group space, the hand's own frame ignored for the blades: along the blade).</summary>
        public static Vector3 DropContact(Keys w,bool left=false)
        {
            var d=w.Drop;if(left)return w.Dual?d.LGrip+d.LBlade*w.Contact.Z:d.LGrip;
            return w.Shield?d.Grip+Vector3.Transform(w.Contact,ShieldHand(d.Blade,d.Up)):d.Grip+d.Blade*w.Contact.Z;
        }
        // ---- applying a key to the rig ----
        private static Vector3 RowV(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        /// <summary>The captured guard's main-hand orientation (chest space) from its row: blade along +z, edge along +x.</summary>
        private static Quaternion GuardHand(float[] f)
        {
            Vector3 blade=Vector3.Normalize(RowV(f,MeleeCapture.Blade)),edge=RowV(f,MeleeCapture.Edge);edge-=blade*Vector3.Dot(edge,blade);
            if(edge.LengthSquared()<1e-8f)edge=new Vector3(1,0,0)-blade*blade.X;edge=Vector3.Normalize(edge);return MeleeMath.Basis(edge,Vector3.Cross(blade,edge),blade);
        }
        private static Quaternion GuardLeft(float[] f)
        {
            Vector3 a=Vector3.Normalize(RowV(f,MeleeCapture.LFwd)),c=RowV(f,MeleeCapture.LUp);c-=a*Vector3.Dot(c,a);if(c.LengthSquared()<1e-8f)c=Vector3.UnitY;c=Vector3.Normalize(c);
            return MeleeMath.Basis(Vector3.Cross(c,a),c,a);
        }
        /// <summary>The hand's orientation (chest space) pointing its +z along `blade` (chest space) with the least turn from the
        /// captured guard's, so the wrist keeps the guard's roll.</summary>
        private static Quaternion Turned(Quaternion guard,Vector3 blade){Vector3 g=Vector3.Transform(Vector3.UnitZ,guard);return MeleeMath.FromTo(g,blade)*guard;}
        private static Quaternion ShieldHand(Vector3 face,Vector3 up){Vector3 z=Vector3.Normalize(face),y=up-z*Vector3.Dot(up,z);y=y.LengthSquared()<1e-8f?Vector3.UnitY:Vector3.Normalize(y);return MeleeMath.Basis(Vector3.Cross(y,z),y,z);}
        private static Vector3 ToChest(MeleeRig r,Vector3 group){Quaternion q=r.ChainQuat(CapturedRig.Chest,-1);return Vector3.Transform(group-r.ChainPoint(CapturedRig.Chest,-1),MeleeMath.Inverse(q));}
        private static Vector3 DirToChest(MeleeRig r,Vector3 group)=>Vector3.Transform(group,MeleeMath.Inverse(r.ChainQuat(CapturedRig.Chest,-1)));
        private static Vector3 PoleToClav(MeleeRig r,bool left,Vector3 group)=>Vector3.Transform(group,MeleeMath.Inverse(r.ChainQuat(left?CapturedRig.LClav:CapturedRig.RClav,-1)));
        /// <summary>The weapon-frame contact point (stretched by the reach) of the main hand, in group space.</summary>
        public static Vector3 ContactPoint(MeleeRig r,Keys w,bool left=false)
        {
            int h=left?CapturedRig.LHand:CapturedRig.RHand;return r.ChainPoint(h,-1)+Vector3.Transform(r.Reached(w.Contact),r.ChainQuat(h,-1));
        }
        /// <summary>
        /// Poses the rig on `k` over its torso as the guard row `guard` leaves it: the hips' drop and pitch, the bows, the head,
        /// the hands on their targets (for the stuck phases the contact `depth` below the sole at the key's x and z, the grip
        /// solved back from it, the hips dropping up to MaxReachDrop further when the arms cannot reach), the legs on their
        /// targets. Returns the extra hips drop it took.
        /// </summary>
        public static float Apply(MeleeRig r,MeleeSet set,Keys w,in Key k,float[] guard,float depth,float wrench,bool grounded)
        {
            float extra=0;
            for(int pass=0;pass<3;pass++)
            {
                Torso(r,set,k,guard,k.Body-extra);
                Quaternion chestQ=r.ChainQuat(CapturedRig.Chest,-1);
                // The main hand: its orientation, then its grip (from the contact on the floor while planted).
                Quaternion hand=w.Shield?MeleeMath.Inverse(chestQ)*ShieldHand(k.Blade,k.Up):Turned(GuardHand(guard),DirToChest(r,k.Blade));
                Vector3 grip=k.Grip;
                if(!float.IsNaN(depth))
                {
                    Vector3 tip=new Vector3(k.Grip.X,Sole+depth,k.Grip.Z);
                    if(wrench!=0){Quaternion rock=MeleeMath.AxisAngle(MeleeMath.X,wrench);hand=MeleeMath.Inverse(chestQ)*rock*chestQ*hand;}
                    grip=tip-Vector3.Transform(r.Reached(w.Contact),chestQ*hand);
                    // Out of reach: drop the hips (the arm hangs from the shoulder) until the grip is within the arm's reach.
                    if(pass<2)
                    {
                        float need=Need(r,false,grip);if(w.TwoHand)need=Math.Max(need,Need(r,true,grip+Vector3.Transform(r.Reached(MeleeRig.SupportGrip),chestQ*hand)));
                        if(w.Dual){Vector3 lt=new Vector3(k.LGrip.X,Sole+depth,k.LGrip.Z);Quaternion lh=chestQ*Turned(GuardLeft(guard),DirToChest(r,k.LBlade));need=Math.Max(need,Need(r,true,lt-Vector3.Transform(r.Reached(w.Contact),lh)));}
                        if(need>1e-3f&&extra<MaxReachDrop){extra=Math.Min(MaxReachDrop,extra+need);continue;}
                    }
                }
                r.BraceWeapon(ToChest(r,grip),hand,1,PoleToClav(r,false,k.Pole),w.TwoHand);
                if(w.Dual)
                {
                    Quaternion lh=Turned(GuardLeft(guard),DirToChest(r,k.LBlade));Vector3 lg=k.LGrip;
                    if(!float.IsNaN(depth))lg=new Vector3(k.LGrip.X,Sole+depth,k.LGrip.Z)-Vector3.Transform(r.Reached(w.Contact),chestQ*lh);
                    r.Hand(true,ToChest(r,lg),lh,1,PoleToClav(r,true,k.LPole));
                }
                else if(!w.TwoHand)r.Hand(true,ToChest(r,k.LGrip),Turned(GuardLeft(guard),DirToChest(r,k.LBlade)),1,PoleToClav(r,true,k.LPole));
                break;
            }
            Legs(r,k,grounded);
            return extra;
        }
        // How far the hips must drop for the hand to reach `grip` (group space), about: the vertical share of the overreach.
        private static float Need(MeleeRig r,bool left,Vector3 grip)
        {
            Vector3 s=r.ChainPoint(left?CapturedRig.LArm:CapturedRig.RArm,-1),d=grip-s;float len=d.Length(),max=.515f-ReachMargin;if(len<=max)return 0;
            // Lowering the shoulder by h shortens the reach to sqrt(dx^2+dz^2+(dy+h)^2): solve for the h that makes it max.
            float flat=d.X*d.X+d.Z*d.Z;if(flat>=max*max)return .1f;return Math.Max(0,-d.Y-(float)Math.Sqrt(max*max-flat));
        }
        private static void Torso(MeleeRig r,MeleeSet set,in Key k,float[] guard,float body)
        {
            MeleeCapture.Apply(r,set,guard,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0,root:0,arms:false));
            r.P[CapturedRig.Body]=new Vector3(0,body,0);r.SetE(CapturedRig.Body,k.Pitch,0,0);r.SetQ(CapturedRig.Pelvis,Quaternion.Identity);
            r.AddE(CapturedRig.Spine,k.Bow,0,0);r.AddE(CapturedRig.Chest,k.Chest,0,0);r.AddE(CapturedRig.Head,k.Head,0,0);
        }
        private static void Legs(MeleeRig r,in Key k,bool grounded)
        {
            r.SolveLeg(true,k.LFoot,.12f,k.LPitch,0,null,k.LKnee);r.SolveLeg(false,k.RFoot,-.12f,k.RPitch,0,null,k.RKnee);
            if(grounded)r.LegsAuthored=true;else r.LegsLayered=true;
        }

        // ---- the first-person view model ----
        /// <summary>
        /// The view model's hands (the view rig's frame: the camera's, mirrored, about .3 m ahead of the eye) for the drop: the
        /// grips low in view, each blade pointing at the ground below (world down in the camera's frame at the view pitch), so
        /// the pilot looks down the blade as it falls; the daggers one blade from each side, the greatsword centred two-handed,
        /// the shield low right with its rim leading.
        /// </summary>
        public struct ViewKey{public Vector3 Grip,LGrip;}
        public static ViewKey View(string spec)
        {
            switch(spec)
            {
                case "daggers":return new ViewKey{Grip=V(-.06f,-.07f,.11f),LGrip=V(.47f,-.07f,.11f)};
                case "greatsword":return new ViewKey{Grip=V(-.12f,-.22f,.1f)};
                case "shield":return new ViewKey{Grip=V(-.18f,-.1f,.14f),LGrip=V(.5f,-.45f,-.28f)};
                default:return new ViewKey{Grip=V(-.08f,-.1f,.13f),LGrip=V(.5f,-.45f,-.28f)};
            }
        }
        /// <summary>The view's blade direction at the view's pitch (degrees, negative looking down; Actor.Pitch): world down in the
        /// view rig's frame leaned ViewLean toward the view's axis, so the blade points down and away into the view's lower middle
        /// (straight down converges at the screen's bottom edge when looking down 45-60 degrees and reads as a dot).</summary>
        public static Vector3 ViewDown(float pitchDegrees)
        {
            float phi=MeleeMath.Clamp(-pitchDegrees,0,80)*(float)Math.PI/180;return Vector3.Normalize(new Vector3(0,-(float)Math.Cos(phi),(float)Math.Sin(phi)+ViewLean));
        }
        public const float ViewLean=.7f;
        /// <summary>How far the hands sink at the plant and rise back over the pull-out (view rig units).</summary>
        public const float ViewSink=.09f,ViewTuck=.1f,ViewIn=.16f;
        /// <summary>Poses the first-person view rig for the plunge at `c` (its guard from the row, the hands from View, the
        /// blades along ViewDown) and returns the weight of the guard to blend back to (the settle).</summary>
        public static float ViewPose(MeleeRig r,MeleeSet set,string spec,in NativePlungeClock c,float[] guard,float pitchDegrees)
        {
            var w=For(spec);if(w==null)return 1;var v=View(spec);Vector3 down=ViewDown(pitchDegrees);
            MeleeCapture.Apply(r,set,guard,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0,root:0,arms:false));
            r.P[CapturedRig.Body]=Vector3.Zero;r.SetQ(CapturedRig.Body,Quaternion.Identity);r.SetQ(CapturedRig.Pelvis,Quaternion.Identity);r.SetQ(CapturedRig.Spine,Quaternion.Identity);
            // The windup lifts the grips into the tuck, the drop pushes them down along the blade, the plant jolts them down.
            float lift=c.Phase==NativePlungePhase.Windup?ViewTuck*c.Tuck:c.Phase==NativePlungePhase.Drop?ViewTuck*(1-c.Commit):0;
            float sink=c.Landed?ViewSink*c.Plant*(1-c.Pull):0;
            Vector3 shift=new Vector3(0,lift,0)+down*(.06f*c.Commit-sink*.4f)+new Vector3(0,-sink,0);
            Quaternion chestQ=r.ChainQuat(CapturedRig.Chest,-1),inv=MeleeMath.Inverse(chestQ);
            if(w.Shield)
            {
                // Rim first: the shield's top up and back, its face forward, its bottom rim pointing down the view.
                Vector3 up=-down,face=new Vector3(0,down.Z,-down.Y);
                r.BraceWeapon(ToChest(r,v.Grip+shift),inv*ShieldHand(face,up),1,PoleToClav(r,false,V(-1,-.3f,-.3f)),false);
                r.Hand(true,ToChest(r,v.LGrip),GuardLeft(guard),1,PoleToClav(r,true,V(1,-.3f,-.5f)));
            }
            else
            {
                Quaternion hand=Turned(GuardHand(guard),DirToChest(r,Vector3.Normalize(down+new Vector3(ViewIn,0,0))));
                r.BraceWeapon(ToChest(r,v.Grip+shift),hand,1,PoleToClav(r,false,V(-1,-.4f,-.3f)),w.TwoHand);
                if(w.Dual)r.Hand(true,ToChest(r,v.LGrip+shift),Turned(GuardLeft(guard),DirToChest(r,Vector3.Normalize(down+new Vector3(-ViewIn,0,0)))),1,PoleToClav(r,true,V(1,-.4f,-.3f)));
                else if(!w.TwoHand)r.Hand(true,ToChest(r,v.LGrip),GuardLeft(guard),1,PoleToClav(r,true,V(1,-.3f,-.5f)));
            }
            r.MocapPosed=true;
            return c.Phase==NativePlungePhase.Settle?c.Settle:c.Phase==NativePlungePhase.Windup?1-c.Tuck:0;
        }
    }
}
