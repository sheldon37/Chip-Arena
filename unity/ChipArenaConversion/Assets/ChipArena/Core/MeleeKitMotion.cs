using System;
using System.Numerics;
using static ChipArena.UnityConversion.CapturedRig;

namespace ChipArena.UnityConversion
{
    /// <summary>The replicated melee Q/E phase a pilot is in (MeleeKit.Phase).</summary>
    public enum KitPart{None,Windup,Active,Recovery,Recast,Fall}

    /// <summary>
    /// A pilot's melee Q/E cast as the presentation reads it from the replicated <see cref="NativeMeleeKit"/> (Kind, Phase, At,
    /// Release, Until) and the snapshot's legacy special timestamps (SpecialKind for the recasts, SpecialLanded): which ability,
    /// which phase, and when that phase began. A phase's start is derived from the replicated times only: the windup and the
    /// recast anticipation start at At; active and fall at Release; a recovery at Release, or where the simulation ends the
    /// movement (flash-step/bulwark-charge: Until - .2; Skyfall: its landing; the Whirlwind: its spin's end, MeleeKit.Until).
    /// </summary>
    public struct KitClock
    {
        public string Kind,Spec;public KitPart Part;public bool Recast;
        public float At,Release,Until,Start,Rate,Landed;
        /// <summary>The Whirlwind's spin end (MeleeKit.Until; Until is then its RecoverUntil); Until for every other kit.</summary>
        public float Spun;
        /// <summary>Seconds into the current phase.</summary>
        public float Age(float time)=>Math.Max(0,time-Start);
        public float Windup=>Math.Max(.01f,Release-At);
    }

    /// <summary>
    /// One key pose of the Q/E layer in the captured rig's source frame (+x the pilot's left, +z forward, radians): body offset
    /// and turn, torso joints, foot targets (on the floor, or as leg joints in the air), and both hands as grip targets with
    /// the weapon's axis in the pilot's facing frame about the chest (the weapon rides the wrist's +z; the greatsword's support
    /// hand can follow the grip). Blends are linear in every field, so a key track is continuous wherever its keys are.
    /// </summary>
    public sealed class KitPose
    {
        public float W;
        public Vector3 Body;public float Pitch,Yaw,Roll,Flip;
        public float PelvisX,PelvisY,SpineX,SpineY,ChestX,ChestY,ChestZ,HeadX,HeadY;
        public Vector3 LFoot=new Vector3(.13f,-.795f,0),RFoot=new Vector3(-.13f,-.795f,0);public float Air;
        public float LHipX,LHipZ,LKnee,RHipX,RHipZ,RKnee;
        public Vector3 RGrip,RBlade=Vector3.UnitZ,RUp=Vector3.UnitY,RPole=new Vector3(-1,-.7f,-.2f);
        public Vector3 LGrip,LDir=Vector3.UnitZ,LUp=Vector3.UnitY,LPole=new Vector3(1,-.8f,-.25f);public float Support;
        /// <summary>1: the pose rides over the gait (the Whirlwind): the legs stay the gait's planted, stepping feet and the body
        /// and pelvis turns add to the gait's; 0 (every other key): the layer poses them outright.</summary>
        public float Gait;
        public KitPose Copy()=>(KitPose)MemberwiseClone();
        public void From(KitPose o)
        {
            W=o.W;Body=o.Body;Pitch=o.Pitch;Yaw=o.Yaw;Roll=o.Roll;Flip=o.Flip;PelvisX=o.PelvisX;PelvisY=o.PelvisY;SpineX=o.SpineX;SpineY=o.SpineY;ChestX=o.ChestX;ChestY=o.ChestY;ChestZ=o.ChestZ;HeadX=o.HeadX;HeadY=o.HeadY;
            LFoot=o.LFoot;RFoot=o.RFoot;Air=o.Air;LHipX=o.LHipX;LHipZ=o.LHipZ;LKnee=o.LKnee;RHipX=o.RHipX;RHipZ=o.RHipZ;RKnee=o.RKnee;
            RGrip=o.RGrip;RBlade=o.RBlade;RUp=o.RUp;RPole=o.RPole;LGrip=o.LGrip;LDir=o.LDir;LUp=o.LUp;LPole=o.LPole;Support=o.Support;Gait=o.Gait;
        }
        /// <summary>this = a + (b - a) t (every field, the weight too).</summary>
        public KitPose Mix(KitPose a,KitPose b,float t)
        {
            float L(float x,float y)=>x+(y-x)*t;Vector3 V(Vector3 x,Vector3 y)=>x+(y-x)*t;
            W=L(a.W,b.W);Body=V(a.Body,b.Body);Pitch=L(a.Pitch,b.Pitch);Yaw=L(a.Yaw,b.Yaw);Roll=L(a.Roll,b.Roll);Flip=L(a.Flip,b.Flip);
            PelvisX=L(a.PelvisX,b.PelvisX);PelvisY=L(a.PelvisY,b.PelvisY);SpineX=L(a.SpineX,b.SpineX);SpineY=L(a.SpineY,b.SpineY);ChestX=L(a.ChestX,b.ChestX);ChestY=L(a.ChestY,b.ChestY);ChestZ=L(a.ChestZ,b.ChestZ);HeadX=L(a.HeadX,b.HeadX);HeadY=L(a.HeadY,b.HeadY);
            LFoot=V(a.LFoot,b.LFoot);RFoot=V(a.RFoot,b.RFoot);Air=L(a.Air,b.Air);LHipX=L(a.LHipX,b.LHipX);LHipZ=L(a.LHipZ,b.LHipZ);LKnee=L(a.LKnee,b.LKnee);RHipX=L(a.RHipX,b.RHipX);RHipZ=L(a.RHipZ,b.RHipZ);RKnee=L(a.RKnee,b.RKnee);
            RGrip=V(a.RGrip,b.RGrip);RBlade=V(a.RBlade,b.RBlade);RUp=V(a.RUp,b.RUp);RPole=V(a.RPole,b.RPole);LGrip=V(a.LGrip,b.LGrip);LDir=V(a.LDir,b.LDir);LUp=V(a.LUp,b.LUp);LPole=V(a.LPole,b.LPole);Support=L(a.Support,b.Support);Gait=L(a.Gait,b.Gait);
            return this;
        }
        /// <summary>The largest field difference to another pose (radians or metres; the continuity checks read it).</summary>
        public float Distance(KitPose o)
        {
            float d=0;void F(float x,float y){d=Math.Max(d,Math.Abs(x-y));}void V(Vector3 x,Vector3 y){d=Math.Max(d,(x-y).Length());}
            F(W,o.W);V(Body,o.Body);F(Pitch,o.Pitch);F(Yaw,o.Yaw);F(Roll,o.Roll);F(Flip,o.Flip);F(PelvisX,o.PelvisX);F(PelvisY,o.PelvisY);F(SpineX,o.SpineX);F(SpineY,o.SpineY);F(ChestX,o.ChestX);F(ChestY,o.ChestY);F(ChestZ,o.ChestZ);F(HeadX,o.HeadX);F(HeadY,o.HeadY);
            V(LFoot,o.LFoot);V(RFoot,o.RFoot);F(Air,o.Air);F(LHipX,o.LHipX);F(LHipZ,o.LHipZ);F(LKnee,o.LKnee);F(RHipX,o.RHipX);F(RHipZ,o.RHipZ);F(RKnee,o.RKnee);
            V(RGrip,o.RGrip);V(RBlade,o.RBlade);V(RUp,o.RUp);V(LGrip,o.LGrip);V(LDir,o.LDir);V(LUp,o.LUp);F(Support,o.Support);F(Gait,o.Gait);
            return d;
        }
    }

    /// <summary>
    /// Pilot animation for the melee Q/E kit (Astra's simulation, docs/MELEE-SIMULATION-HANDOFF.md; Claude's presentation): one
    /// authored pose layer per ability and phase over the captured guard, third person (CapturedAnimation's melee overlay) and
    /// the first-person arms (the generic view-model path), driven only by the replicated MeleeKit Kind/Phase and timestamps
    /// (KitClock). Every key track starts from the pose the previous phase ended on, evaluated at the replicated phase start,
    /// so the layer is continuous across the windup/active/recovery/recast/fall boundaries (Tests/MeleeKitPresentationChecks). The body is
    /// posed around where the simulation puts it: offsets stay within the rig (crouches, leans, the Whirlwind's spin about the
    /// spine); there is no root motion. Presentation only: hit timing, hitboxes, damage, cooldowns, movement and networking are
    /// the authority's (nothing here is read back).
    ///  - Flash Step: a chambered thrust in a low lunge (the telegraph), the extended dash-thrust, the blade returning.
    ///  - Rising Dragon: a deep coil with the blade trailing low behind, the rising slash ending overhead (the launch), held for
    ///    the recast window. Skyfall (recast): a gathering crouch with the blade raised, an airborne plunge (fall), the landing.
    ///  - Shadow Step: a crouched departure with the daggers crossed (the decoy keeps this silhouette), an arrival flourish.
    ///  - Kunai Mark: a side-on throw (the aim hand points), the whip follow-through. Recast: a coiled shimmer, the arrival stab.
    ///  - Bulwark Charge: a braced shield push-off, a shield-first sprint, the shove on impact.
    ///  - Aegis Ricochet: a cross-body frisbee coil, the throw and an open-handed follow-through.
    ///  - Earthsplitter: the blade dragged behind, raised overhead and chopped into the floor on the release.
    ///  - Whirlwind: a heavy wind-back with the blade dragged low behind, a two-handed spin of the upper body and level blade
    ///    over the gait's stepping legs (third person; first person the blade sweeps the lower view each turn), the stop.
    /// </summary>
    public static class MeleeKitMotion
    {
        private const float Tau=(float)(Math.PI*2);
        public static readonly string[] Kinds=
        {
            "flash-step","rising-dragon",
            "shadow-step","kunai-mark",
            "bulwark-charge","aegis-ricochet",
            "earthsplitter","whirlwind",
        };
        public static bool Known(string kind){if(kind==null)return false;foreach(var k in Kinds)if(k==kind)return true;return false;}
        public static string Spec(string kind)=>kind=="flash-step"||kind=="rising-dragon"?"sword":kind=="shadow-step"||kind=="kunai-mark"?"daggers":kind=="bulwark-charge"||kind=="aegis-ricochet"?"shield":kind=="earthsplitter"||kind=="whirlwind"?"greatsword":null;
        /// <summary>The fixed recovery tails the simulation appends (flash-step/bulwark after the dash ends, Skyfall after landing).</summary>
        public const float DashRecovery=.2f,SkyfallRecovery=.25f,EnterSeconds=.06f;

        /// <summary>The cast the pilot is in at `time`, from replicated fields only; false without one (or once it ended).</summary>
        public static bool Clock(Actor a,float time,out KitClock c)
        {
            c=default;var k=a?.MeleeKit;if(k==null||!Known(k.Kind)||string.IsNullOrEmpty(k.Phase)||!MathUtil.Finite(time))return false;
            // The Whirlwind's Until is its spin's end (moved up by a recast); its recovery runs on to RecoverUntil.
            bool whirl=k.Kind=="whirlwind";float until=whirl?Math.Max(k.Until,k.RecoverUntil>k.Until?k.RecoverUntil:k.Until+NativeWhirlwindRules.Recovery):k.Until;
            if(time<k.At-.0001f||time>=until)return false;
            c.Kind=k.Kind;c.Spec=Spec(k.Kind);c.At=k.At;c.Release=Math.Max(k.Release,k.At+.01f);c.Until=until;c.Spun=whirl?k.Until:until;c.Rate=a.CastRate>0?a.CastRate:1;c.Landed=a.SpecialLanded;
            c.Recast=k.Phase=="recast"||k.Phase=="fall"||a.SpecialKind=="skyfall"||a.SpecialKind=="kunai-recast";
            switch(k.Phase)
            {
                case "windup":c.Part=KitPart.Windup;c.Start=c.At;break;
                case "recast":c.Part=KitPart.Recast;c.Start=c.At;break;
                case "active":c.Part=KitPart.Active;c.Start=c.Release;break;
                case "fall":c.Part=KitPart.Fall;c.Start=c.Release;break;
                case "recovery":c.Part=KitPart.Recovery;c.Start=RecoveryStart(c);break;
                default:return false;
            }
            if(c.Start>time)c.Start=Math.Max(c.At,time);
            return true;
        }
        public static float RecoveryStart(in KitClock c)
        {
            float s=c.Release;
            if(c.Kind=="flash-step"||c.Kind=="bulwark-charge")s=c.Until-DashRecovery/1;
            else if(c.Kind=="whirlwind")s=c.Spun;
            else if(c.Kind=="rising-dragon"&&c.Recast)s=c.Landed>=c.Release?c.Landed:c.Until-SkyfallRecovery;
            return Math.Max(c.Release,Math.Min(s,c.Until));
        }
        /// <summary>This layer owns the pilot's special pose at `time` (it replaces the legacy NativeSpecialPose phrase).</summary>
        public static bool Owns(Actor a,float time)=>Clock(a,time,out _);
        /// <summary>The held weapon's extra wrist turn while the layer owns the pose: none (the grips aim the weapon).</summary>
        public static Quaternion Held(Actor a,float time,bool left)=>Quaternion.Identity;
        /// <summary>A readable state for the debug overlay ("flash-step active").</summary>
        public static string State(Actor a,float time)=>Clock(a,time,out var c)?c.Kind+(c.Recast?" recast ":" ")+c.Part.ToString().ToLowerInvariant():null;

        // ---------------------------------------------------------------------------------------------------------------
        // Key poses (built once). The guard values come from the captured guards (chest frame) so blends never flail.
        // ---------------------------------------------------------------------------------------------------------------
        private static Vector3 N(float x,float y,float z)=>Vector3.Normalize(new Vector3(x,y,z));
        private static KitPose Guard(string spec)
        {
            var k=new KitPose{W=1};
            switch(spec)
            {
                case "daggers":k.RGrip=V(-.46f,-.19f,.21f);k.RBlade=N(.34f,-.79f,-.51f);k.RUp=N(-.82f,.02f,-.57f);k.RPole=N(.05f,-.5f,-.86f);k.LGrip=V(.53f,-.11f,.23f);k.LDir=N(-.39f,-.89f,-.23f);k.LUp=N(-.68f,.12f,.72f);k.LPole=N(-.29f,-.7f,-.65f);break;
                case "greatsword":k.RGrip=V(-.04f,.33f,.16f);k.RBlade=N(-.05f,.84f,-.53f);k.RUp=N(.65f,-.37f,-.65f);k.RPole=N(-.58f,.47f,.67f);k.Support=1;k.LGrip=V(-.03f,.26f,.31f);k.LDir=N(-.07f,.89f,-.46f);k.LUp=N(.98f,-.02f,-.2f);k.LPole=N(.65f,.18f,.74f);break;
                case "shield":k.RGrip=V(-.61f,.17f,.28f);k.RBlade=N(-.49f,.6f,-.64f);k.RUp=N(.71f,.7f,.11f);k.RPole=N(-.51f,-.7f,-.51f);k.LGrip=V(.4f,-.14f,.32f);k.LDir=N(-.09f,.84f,.54f);k.LUp=N(-.19f,.52f,-.83f);k.LPole=N(.45f,-.59f,-.67f);break;
                default:k.RGrip=V(-.46f,-.19f,.21f);k.RBlade=N(-.34f,.79f,.51f);k.RUp=N(.82f,-.02f,.57f);k.RPole=N(.05f,-.5f,-.86f);k.LGrip=V(.53f,-.11f,.23f);k.LDir=N(.39f,.89f,.25f);k.LUp=N(-.58f,.45f,-.68f);k.LPole=N(-.29f,-.7f,-.65f);break;
            }
            return k;
        }
        private static readonly KitPose SwordGuard=Guard("sword"),DaggerGuard=Guard("daggers"),ShieldGuard=Guard("shield"),GreatGuard=Guard("greatsword");
        /// <summary>The captured guard as a key pose (shared; never edited).</summary>
        public static KitPose GuardOf(string spec)=>spec=="daggers"?DaggerGuard:spec=="shield"?ShieldGuard:spec=="greatsword"?GreatGuard:SwordGuard;
        private static KitPose Key(string spec,Action<KitPose> edit){var k=Guard(spec);edit(k);return k;}
        private static void Stance(KitPose k,float lx,float lz,float rx,float rz,float ly=-.795f,float ry=-.795f){k.LFoot=V(lx,ly,lz);k.RFoot=V(rx,ry,rz);}

        // ---- sword: Flash Step ----
        private static readonly KitPose FlashChamber=Key("sword",k=>{k.Body=V(0,-.17f,-.02f);k.Pitch=.22f;k.PelvisY=-.35f;k.ChestY=-.45f;k.ChestX=.08f;k.HeadY=.6f;k.HeadX=-.1f;
            Stance(k,.17f,.34f,-.16f,-.36f);k.RGrip=V(-.42f,-.12f,-.12f);k.RBlade=N(.08f,.04f,1);k.RUp=N(0,1,-.04f);k.RPole=N(-.6f,-.5f,-.6f);
            k.LGrip=V(.28f,.08f,.34f);k.LDir=N(-.3f,.35f,.9f);k.LUp=N(-.7f,.6f,-.1f);k.LPole=N(.7f,-.6f,-.3f);});
        private static readonly KitPose FlashThrust=Key("sword",k=>{k.Body=V(0,-.22f,.08f);k.Pitch=.42f;k.PelvisY=.15f;k.ChestY=.32f;k.ChestX=.1f;k.HeadY=-.3f;k.HeadX=-.3f;
            Stance(k,.16f,.5f,-.15f,-.62f,-.795f,-.74f);k.RGrip=V(-.18f,.14f,.5f);k.RBlade=N(.06f,-.06f,1);k.RUp=N(0,1,.06f);k.RPole=N(-.9f,-.4f,-.1f);
            k.LGrip=V(.42f,-.12f,-.18f);k.LDir=N(.35f,-.75f,.2f);k.LUp=N(-.6f,-.1f,.75f);k.LPole=N(.8f,-.3f,.4f);});
        private static readonly KitPose FlashSettle=Key("sword",k=>{k.Body=V(0,-.1f,.03f);k.Pitch=.12f;k.ChestY=.1f;Stance(k,.15f,.28f,-.14f,-.25f);
            k.RGrip=V(-.36f,-.02f,.32f);k.RBlade=N(-.1f,.35f,.93f);k.RUp=N(.4f,.85f,-.3f);});

        // ---- sword: Rising Dragon and Skyfall ----
        private static readonly KitPose RisingCoil=Key("sword",k=>{k.Body=V(0,-.24f,-.04f);k.Pitch=.26f;k.PelvisY=-.4f;k.ChestY=-.7f;k.ChestX=.18f;k.HeadY=.75f;k.HeadX=-.15f;
            Stance(k,.2f,.3f,-.18f,-.3f);k.RGrip=V(-.5f,-.42f,-.06f);k.RBlade=N(-.25f,-.5f,-.83f);k.RUp=N(-.9f,.35f,.1f);k.RPole=N(-.5f,-.2f,-.85f);
            k.LGrip=V(.35f,.12f,.3f);k.LDir=N(.1f,.5f,.85f);k.LUp=N(-.8f,.5f,-.2f);});
        private static readonly KitPose RisingSlash=Key("sword",k=>{k.Body=V(0,.06f,.05f);k.Pitch=-.12f;k.PelvisY=.25f;k.ChestY=.45f;k.ChestX=-.18f;k.HeadX=-.35f;k.HeadY=-.2f;
            Stance(k,.15f,.18f,-.15f,-.2f,-.75f,-.7f);k.RGrip=V(-.12f,.62f,.24f);k.RBlade=N(.18f,.95f,.25f);k.RUp=N(.95f,-.15f,-.25f);k.RPole=N(-.9f,.1f,-.4f);
            k.LGrip=V(.45f,-.16f,.05f);k.LDir=N(.5f,-.6f,.6f);k.LUp=N(-.4f,.6f,.6f);});
        private static readonly KitPose RisingHold=Key("sword",k=>{k.Body=V(0,-.04f,.02f);k.Pitch=.02f;k.ChestY=.2f;k.HeadX=-.25f;Stance(k,.16f,.16f,-.15f,-.18f);
            k.RGrip=V(-.3f,.38f,.28f);k.RBlade=N(0,.75f,.66f);k.RUp=N(.95f,.05f,-.2f);k.LGrip=V(.4f,.0f,.3f);k.LDir=N(.3f,.6f,.7f);k.LUp=N(-.7f,.6f,-.2f);});
        private static readonly KitPose SkyGather=Key("sword",k=>{k.Body=V(0,-.27f,-.03f);k.Pitch=.18f;k.ChestX=-.1f;k.HeadX=-.45f;Stance(k,.17f,.12f,-.17f,-.14f);
            k.RGrip=V(-.18f,.58f,-.02f);k.RBlade=N(-.05f,.35f,-.94f);k.RUp=N(.97f,.1f,0);k.RPole=N(-.9f,.3f,-.3f);k.LGrip=V(.3f,.5f,.15f);k.LDir=N(0,.95f,.3f);k.LUp=N(-.9f,0,.4f);});
        private static readonly KitPose SkyPlunge=Key("sword",k=>{k.Body=V(0,.05f,.04f);k.Pitch=.55f;k.ChestX=.2f;k.HeadX=-.4f;k.Air=1;k.LHipX=-1.2f;k.LKnee=1.6f;k.RHipX=-.4f;k.RKnee=1.4f;k.LHipZ=.15f;k.RHipZ=-.12f;
            k.RGrip=V(-.12f,.08f,.5f);k.RBlade=N(0,-.86f,.5f);k.RUp=N(1,0,0);k.RPole=N(-.9f,-.2f,-.3f);k.LGrip=V(.42f,.35f,-.05f);k.LDir=N(.6f,.6f,-.5f);k.LUp=N(.5f,-.6f,-.6f);});
        private static readonly KitPose SkyLand=Key("sword",k=>{k.Body=V(0,-.34f,.06f);k.Pitch=.48f;k.ChestX=.15f;k.HeadX=-.5f;Stance(k,.2f,.3f,-.18f,-.32f);
            k.RGrip=V(-.14f,-.22f,.48f);k.RBlade=N(0,-.86f,.5f);k.RUp=N(1,0,0);k.RPole=N(-.9f,-.3f,-.2f);k.LGrip=V(.5f,.0f,-.1f);k.LDir=N(.7f,-.3f,-.6f);k.LUp=N(.3f,.9f,-.2f);});

        // ---- daggers: Shadow Step and Kunai Mark ----
        private static readonly KitPose ShadowDepart=Key("daggers",k=>{k.Body=V(0,-.3f,.02f);k.Pitch=.42f;k.ChestX=.15f;k.ChestY=.1f;k.HeadX=-.45f;Stance(k,.2f,.22f,-.19f,-.22f);
            k.RGrip=V(-.06f,.08f,.36f);k.RBlade=N(.6f,-.7f,-.3f);k.RUp=N(-.5f,-.1f,-.85f);k.RPole=N(-.9f,-.4f,-.2f);
            k.LGrip=V(.06f,.12f,.34f);k.LDir=N(-.6f,-.7f,-.3f);k.LUp=N(-.5f,.1f,.85f);k.LPole=N(.9f,-.4f,-.2f);});
        private static readonly KitPose ShadowArrive=Key("daggers",k=>{k.Body=V(0,-.24f,0);k.Pitch=.3f;k.ChestX=.05f;k.HeadX=-.3f;Stance(k,.24f,.18f,-.22f,-.26f);
            k.RGrip=V(-.62f,-.05f,-.08f);k.RBlade=N(.2f,-.85f,-.45f);k.RUp=N(-.9f,-.2f,.3f);k.RPole=N(-.5f,-.3f,-.8f);
            k.LGrip=V(.62f,-.02f,-.06f);k.LDir=N(-.2f,-.85f,-.45f);k.LUp=N(-.8f,.2f,.5f);k.LPole=N(.5f,-.3f,-.8f);});
        private static readonly KitPose KunaiCock=Key("daggers",k=>{k.Body=V(0,-.1f,-.06f);k.Pitch=-.04f;k.PelvisY=-.55f;k.ChestY=-.6f;k.ChestX=-.08f;k.HeadY=.85f;Stance(k,.14f,.3f,-.18f,-.3f);
            k.RGrip=V(-.42f,.42f,-.22f);k.RBlade=N(-.1f,.55f,.82f);k.RUp=N(-.9f,.25f,-.3f);k.RPole=N(-.7f,-.2f,-.7f);
            k.LGrip=V(.2f,.28f,.48f);k.LDir=N(-.3f,-.85f,-.4f);k.LUp=N(-.6f,.3f,.7f);k.LPole=N(.7f,-.7f,.1f);});
        private static readonly KitPose KunaiThrow=Key("daggers",k=>{k.Body=V(0,-.16f,.08f);k.Pitch=.32f;k.PelvisY=.3f;k.ChestY=.45f;k.ChestX=.12f;k.HeadY=-.2f;k.HeadX=-.15f;Stance(k,.16f,.4f,-.16f,-.42f,-.795f,-.76f);
            k.RGrip=V(.04f,-.08f,.5f);k.RBlade=N(.2f,-.4f,.9f);k.RUp=N(-.9f,.1f,.2f);k.RPole=N(-.8f,-.6f,.1f);
            k.LGrip=V(.48f,-.1f,-.1f);k.LDir=N(.4f,-.8f,-.4f);k.LUp=N(-.6f,0,.8f);k.LPole=N(.7f,-.5f,.5f);});
        private static readonly KitPose RecastCoil=Key("daggers",k=>{k.Body=V(0,-.28f,-.02f);k.Pitch=.34f;k.ChestX=.2f;k.HeadX=-.3f;Stance(k,.19f,.18f,-.2f,-.2f);
            k.RGrip=V(-.2f,.0f,.22f);k.RBlade=N(.3f,-.8f,.5f);k.RUp=N(-.95f,-.2f,.1f);k.LGrip=V(.2f,.02f,.22f);k.LDir=N(-.3f,-.8f,.5f);k.LUp=N(-.95f,.2f,-.1f);});
        private static readonly KitPose RecastRaise=Key("daggers",k=>{k.Body=V(0,-.02f,0);k.Pitch=-.1f;k.ChestX=-.2f;k.HeadX=-.2f;Stance(k,.17f,.24f,-.16f,-.24f,-.76f,-.795f);
            k.RGrip=V(-.2f,.62f,.18f);k.RBlade=N(.05f,-.35f,.94f);k.RUp=N(-.98f,0,.1f);k.RPole=N(-.9f,.3f,-.3f);k.LGrip=V(.2f,.6f,.18f);k.LDir=N(-.05f,-.35f,.94f);k.LUp=N(-.98f,0,-.1f);k.LPole=N(.9f,.3f,-.3f);});
        private static readonly KitPose RecastStab=Key("daggers",k=>{k.Body=V(0,-.2f,.06f);k.Pitch=.4f;k.ChestX=.25f;k.HeadX=-.35f;Stance(k,.18f,.36f,-.17f,-.3f);
            k.RGrip=V(-.14f,.02f,.5f);k.RBlade=N(.05f,-.9f,.4f);k.RUp=N(-.98f,.05f,.1f);k.RPole=N(-.9f,-.2f,-.3f);k.LGrip=V(.14f,.0f,.48f);k.LDir=N(-.05f,-.9f,.4f);k.LUp=N(-.98f,-.05f,-.1f);k.LPole=N(.9f,-.2f,-.3f);});

        // ---- shield: Bulwark Charge and Aegis Ricochet ----
        private static readonly KitPose BulwarkBrace=Key("shield",k=>{k.Body=V(0,-.16f,-.04f);k.Pitch=.3f;k.PelvisY=-.25f;k.ChestY=-.35f;k.ChestX=.15f;k.HeadY=.2f;k.HeadX=-.3f;Stance(k,.18f,.3f,-.16f,-.4f);
            k.RGrip=V(-.06f,.1f,.42f);k.RBlade=N(-.15f,.05f,1);k.RUp=N(0,1,-.05f);k.RPole=N(-.9f,-.5f,0);k.LGrip=V(.38f,-.15f,.12f);k.LDir=N(.2f,-.2f,.95f);k.LUp=N(-.9f,.3f,.1f);});
        private static readonly KitPose BulwarkRush=Key("shield",k=>{k.Body=V(0,-.12f,.06f);k.Pitch=.42f;k.PelvisY=-.15f;k.ChestY=-.25f;k.ChestX=.12f;k.HeadY=.15f;k.HeadX=-.42f;
            k.RGrip=V(-.02f,.14f,.46f);k.RBlade=N(-.1f,.1f,1);k.RUp=N(0,1,-.1f);k.RPole=N(-.9f,-.5f,0);k.LGrip=V(.32f,-.1f,.2f);k.LDir=N(.2f,0,.98f);k.LUp=N(-.9f,.4f,.1f);});
        private static readonly KitPose BulwarkShove=Key("shield",k=>{k.Body=V(0,-.14f,.1f);k.Pitch=.32f;k.ChestY=.1f;k.HeadX=-.3f;Stance(k,.17f,.42f,-.16f,-.36f);
            k.RGrip=V(.02f,.18f,.58f);k.RBlade=N(0,.15f,1);k.RUp=N(0,1,-.15f);k.RPole=N(-.9f,-.4f,.1f);k.LGrip=V(.35f,-.05f,.3f);k.LDir=N(.2f,.1f,.97f);k.LUp=N(-.9f,.4f,.1f);});
        private static readonly KitPose AegisCoil=Key("shield",k=>{k.Body=V(0,-.12f,-.02f);k.Pitch=.12f;k.PelvisY=.4f;k.ChestY=.8f;k.ChestX=.1f;k.HeadY=-.7f;Stance(k,.18f,.26f,-.17f,-.3f);
            k.RGrip=V(.24f,.02f,.16f);k.RBlade=N(.95f,.25f,-.1f);k.RUp=N(-.25f,.95f,.1f);k.RPole=N(-.3f,-.6f,-.75f);k.LGrip=V(.5f,.1f,.3f);k.LDir=N(.3f,.5f,.8f);k.LUp=N(-.9f,.3f,.2f);});
        private static readonly KitPose AegisThrow=Key("shield",k=>{k.Body=V(0,-.1f,.06f);k.Pitch=.22f;k.PelvisY=-.3f;k.ChestY=-.55f;k.ChestX=.12f;k.HeadY=.4f;Stance(k,.17f,.34f,-.16f,-.36f,-.795f,-.77f);
            k.RGrip=V(-.6f,.12f,.3f);k.RBlade=N(-.95f,.15f,.25f);k.RUp=N(.15f,.98f,-.05f);k.RPole=N(-.3f,-.7f,-.6f);k.LGrip=V(.42f,-.12f,.05f);k.LDir=N(.5f,-.4f,.7f);k.LUp=N(-.7f,.6f,.3f);});

        // ---- greatsword: Earthsplitter (two-handed: the support hand follows the grip) ----
        private static readonly KitPose SplitDrag=Key("greatsword",k=>{k.Body=V(0,-.16f,-.06f);k.Pitch=.18f;k.PelvisY=-.45f;k.ChestY=-.75f;k.ChestX=.12f;k.HeadY=.8f;k.HeadX=-.1f;Stance(k,.2f,.3f,-.2f,-.34f);
            k.RGrip=V(-.42f,-.32f,-.1f);k.RBlade=N(-.28f,-.55f,-.79f);k.RUp=N(-.95f,.25f,.15f);k.RPole=N(-.5f,.3f,-.8f);});
        private static readonly KitPose SplitRaise=Key("greatsword",k=>{k.Body=V(0,-.04f,-.06f);k.Pitch=-.22f;k.PelvisY=-.1f;k.ChestY=-.1f;k.ChestX=-.25f;k.HeadX=-.25f;Stance(k,.2f,.3f,-.2f,-.34f);
            k.RGrip=V(-.06f,.62f,-.02f);k.RBlade=N(-.05f,.3f,-.95f);k.RUp=N(.99f,.05f,0);k.RPole=N(-.7f,.6f,.3f);});
        private static readonly KitPose SplitSlam=Key("greatsword",k=>{k.Body=V(0,-.3f,.08f);k.Pitch=.5f;k.PelvisY=.05f;k.ChestY=.08f;k.ChestX=.22f;k.HeadX=-.55f;Stance(k,.24f,.38f,-.22f,-.36f);
            k.RGrip=V(-.04f,-.12f,.45f);k.RBlade=N(0,-.5f,.87f);k.RUp=N(1,0,0);k.RPole=N(-.8f,-.2f,.5f);});

        // ---- greatsword: Whirlwind (owner 2026-10-12: "a whirlwind attack? Spinning around for a short period while able to
        // move ... make sure to add weight realism", "Think like Garens spinning attack"; Astra's rule: NativeWhirlwindRules) ----
        // The upper body and the two-handed blade spin to the pilot's left about the spine while the legs stay the gait's
        // planted, stepping feet (Gait 1; Stance below widens them and keeps them marking time). The wind-back loads through
        // the first WhirlLoad of the windup; the uncoil then brings the blade round from behind the right hip, its rate easing up
        // to the full spin by Release, so the tip crosses the facing at every pulse, Release + k x Interval (k = 0..6). The
        // hand keys are authored in the spin frame (the facing frame before the hands' turn, Grip, is applied).
        public const float WhirlLoad=.36f,WhirlCoil=-1f,WhirlStop=.3f,WhirlStopEarly=.2f,WhirlStopMin=.15f,WhirlStopMax=.4f,WhirlStopEarlyMax=.25f,WhirlLag=.3f,WhirlWiden=.08f,WhirlBlade=1.69f;
        /// <summary>The full spin rate (rad/s): one turn per pulse interval.</summary>
        public const float WhirlRate=Tau/NativeWhirlwindRules.Interval;
        private static KitPose Heavy(Action<KitPose> edit){var k=Guard("greatsword");k.Gait=1;edit(k);return k;}
        private static readonly KitPose WhirlGuard=Heavy(k=>{});
        // The wind-back: both hands low before the coiled chest, the blade trailing out behind them, its edge on the floor
        // (the coil carries it round behind the right hip).
        private static readonly KitPose WhirlDrag=Heavy(k=>{k.RGrip=V(-.1f,-.36f,.29f);k.RBlade=N(-.88f,-.21f,.4f);k.RUp=N(.4f,.3f,.86f);k.RPole=N(-.7f,-.6f,-.3f);});
        // The spin: the arms long, both hands before the belly, the blade out level to their right and trailing, flat to the floor.
        private static readonly KitPose WhirlLevel=Heavy(k=>{k.RGrip=V(-.06f,.02f,.4f);k.RBlade=N(-.86f,-.05f,.5f);k.RUp=N(.05f,1,.1f);k.RPole=N(-.6f,-.75f,-.2f);});
        // The stop: the blade's weight takes it down, the tip to the floor out front, the arms checking it.
        private static readonly KitPose WhirlCheck=Heavy(k=>{k.RGrip=V(-.08f,-.2f,.36f);k.RBlade=N(-.6f,-.38f,.7f);k.RUp=N(.25f,.85f,.45f);k.RPole=N(-.7f,-.65f,-.2f);});
        /// <summary>The level key's blade tip bearing from the hands' turn (the tip trails out to their right).</summary>
        public static readonly float WhirlTip=(float)Math.Atan2(WhirlLevel.RGrip.X+WhirlLevel.RBlade.X*WhirlBlade,WhirlLevel.RGrip.Z+WhirlLevel.RBlade.Z*WhirlBlade);
        // The uncoil's extra pace (a unit bump over its rate ramp) that carries the hands from the coil to the release turn.
        private static float UncoilBump(float seconds)=>(-WhirlTip-WhirlCoil)/(WhirlRate*seconds)-.5f;

        /// <summary>
        /// The Whirlwind's spin at a moment, from the replicated clock only. Angles in radians from the facing, + toward the
        /// pilot's left. Grip: the hands' turn (it carries the blade). Torso: the chest's, leading the trailing blade by WhirlLag
        /// at speed, behind it through the wind-back, and carried past the hands as the blade stops. Tip: the blade tip's
        /// bearing in the spin (0 as it crosses the front; exact from the uncoil on). Rate: the hands' turn rate (rad/s). Speed:
        /// Rate over WhirlRate (0..1). Load: the wind-back's coil (hips and torso loaded right). Low: the hips' sit. Drop: the
        /// stop's weight (the tip to the floor). Settle: the return to the guard (0..1). Stop: the moment the blade stops
        /// (recovery; else NaN); Since: seconds since it (else -1). Early: the spin ended by a recast before its full Duration.
        /// </summary>
        public struct Whirl{public float Grip,Torso,Tip,Rate,Speed,Load,Low,Drop,Settle,Stop,Since,Uncoil;public bool Early;}
        public static Whirl Spin(in KitClock c,float time)
        {
            var w=new Whirl{Stop=float.NaN,Since=-1};float release=-WhirlTip;
            if(c.Part==KitPart.Windup||c.Part==KitPart.Recast)
            {
                float wind=c.Windup,u=MathUtil.Clamp((time-c.At)/wind,0,1);w.Low=Ease(u);
                if(u<WhirlLoad)
                {
                    // The wind-back: the hands and torso coil to the right, easing into the loaded coil.
                    float v=u/WhirlLoad;w.Load=Ease(v);w.Grip=WhirlCoil*w.Load;w.Rate=WhirlCoil*30*v*v*(1-v)*(1-v)/(WhirlLoad*wind);
                }
                else
                {
                    // The uncoil: the hips fire, the hands come round with the blade trailing, the rate easing up from rest
                    // (the blade's inertia) to the full spin at Release, where the tip crosses the front.
                    float span=(1-WhirlLoad)*wind,x=(u-WhirlLoad)/(1-WhirlLoad),a=UncoilBump(span);
                    float i=x*x*x-.5f*x*x*x*x+a*(10*x*x*x-15*x*x*x*x+6*x*x*x*x*x),r=x*x*(3-2*x)+a*30*x*x*(1-x)*(1-x);
                    w.Grip=WhirlCoil+(release-WhirlCoil)*i/(.5f+a);w.Rate=WhirlRate*r;w.Speed=MathUtil.Clamp(r,0,1);w.Load=1-Smooth(x);w.Uncoil=x;
                }
            }
            else if(c.Part==KitPart.Active){w.Grip=release+WhirlRate*Math.Max(0,time-c.Release);w.Rate=WhirlRate;w.Speed=1;w.Low=1;w.Uncoil=1;}
            else
            {
                // The stop: the turn decelerates evenly from the full rate. Where its momentum can carry it there, it comes to rest
                // facing the front (the next whole turn: the full spin's last partial turn, an early recast's within WhirlStopMin to
                // WhirlStopEarlyMax); otherwise it stops after the nominal time and the pilot turns back to its facing the short way.
                // Then the weight settles and the hands return to the guard.
                float end=c.Start,dur=Math.Max(.01f,c.Until-end),u=Math.Max(0,time-end);w.Early=end-c.Release<NativeWhirlwindRules.Duration-.05f;
                float g0=release+WhirlRate*Math.Max(0,end-c.Release),r0=WhirlRate,ahead=Tau*(float)Math.Ceiling((g0+1e-4f)/Tau)-g0,land=2*ahead/r0;
                float most=Math.Min(w.Early?WhirlStopEarlyMax:WhirlStopMax,.7f*dur),least=Math.Min(WhirlStopMin,most);
                float d=land<least?least:land<=most?land:Math.Min(w.Early?WhirlStopEarly:WhirlStop,most);
                if(u<d){w.Grip=g0+r0*(u-u*u/(2*d));w.Rate=r0*(1-u/d);}
                else
                {
                    float stop=g0+r0*d*.5f,home=Tau*(float)Math.Round(stop/Tau),r1=d+.04f,r2=Math.Max(r1+.05f,dur-.02f);
                    w.Settle=Smooth((u-r1)/(r2-r1));w.Grip=stop+(home-stop)*w.Settle;w.Since=u-d;
                }
                w.Speed=Math.Max(0,w.Rate)/WhirlRate;w.Stop=end+d;w.Uncoil=1;w.Low=1-w.Settle;
                w.Drop=Smooth(u/(d+.06f))*(1-w.Settle);
            }
            w.Tip=w.Grip+WhirlTip;w.Torso=w.Grip+WhirlLag*w.Rate/WhirlRate;
            if(w.Since>=0)w.Torso+=.16f*Bump(w.Since/.22f);
            return w;
        }
        /// <summary>The pulse the spin is on at `time` (0 at Release; NativeWhirlwindRules pulses at Release + k x Interval) and
        /// that pulse's moment; -1 before Release or after the spin.</summary>
        public static int Pulse(in KitClock c,float time,out float at)
        {
            at=float.NaN;if(c.Kind!="whirlwind"||c.Part!=KitPart.Active||time<c.Release)return -1;
            int k=Math.Min(NativeWhirlwindRules.MaxTicks-1,(int)Math.Floor((time-c.Release)/NativeWhirlwindRules.Interval+1e-4f));at=c.Release+k*NativeWhirlwindRules.Interval;return k;
        }
        private static float Bump(float x)=>x<=0||x>=1?0:(float)Math.Sin(Math.PI*x);
        /// <summary>A spin-frame vector turned by `a` about the vertical (+ toward the pilot's left).</summary>
        public static Vector3 Turn(Vector3 v,float a){float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.X*c+v.Z*s,v.Y,-v.X*s+v.Z*c);}
        private static readonly KitPose whirlScratch=new KitPose();
        private static void WhirlPose(in KitClock c,float time,KitPose o)
        {
            var w=Spin(c,time);float k=w.Speed,psi=w.Tip,cos=(float)Math.Cos(psi),sin=(float)Math.Sin(psi),front=.5f+.5f*cos,back=1-front;
            // The hands (spin frame): the guard to the wind-back; the wind-back to the level spin through the uncoil; the blade
            // dipping on the far side of each turn (it scrapes low behind the pilot); its weight taking it down at the stop; the guard.
            if(c.Part==KitPart.Windup||c.Part==KitPart.Recast)
            {
                // The heavy blade drops from the high guard to the floor over half the windup, overlapping the uncoil's start.
                o.Mix(WhirlGuard,WhirlDrag,Ease((time-c.At)/(.5f*c.Windup)));if(w.Uncoil>0){whirlScratch.From(o);o.Mix(whirlScratch,WhirlLevel,Smooth(w.Uncoil));}
            }
            else o.From(WhirlLevel);
            float dip=k*back*w.Uncoil;o.RBlade.Y-=.22f*dip;o.RGrip.Y-=.06f*dip;
            if(w.Drop>0){whirlScratch.From(o);o.Mix(whirlScratch,WhirlCheck,w.Drop);}
            if(w.Settle>0){whirlScratch.From(o);o.Mix(whirlScratch,WhirlGuard,w.Settle);}
            // The hands' turn carries the arms and blade round.
            o.RGrip=Turn(o.RGrip,w.Grip);o.RBlade=Turn(o.RBlade,w.Grip);o.RUp=Turn(o.RUp,w.Grip);o.RPole=Turn(o.RPole,w.Grip);
            o.LGrip=Turn(o.LGrip,w.Grip);o.LDir=Turn(o.LDir,w.Grip);o.LUp=Turn(o.LUp,w.Grip);o.LPole=Turn(o.LPole,w.Grip);
            // Weight: the hips load right through the wind-back and fire first in the uncoil, then drive each turn (leading the
            // chest, which leads the trailing blade); the torso spins on the spine over the gait's hips, leaning back and away
            // from the blade (the counterbalance); the head holds the target through the coil, then leads into the turn; the hips
            // sit low with a dip as the blade comes round the front, the body's mass shifted against the blade; at the stop the
            // hips carry on and the mass lurches with the momentum.
            float load=w.Load,lurch=w.Since>=0?Bump(w.Since/.25f):0;
            float pelvis=-.45f*load+.28f*k*(float)Math.Cos(psi-.4f)+.12f*lurch;
            o.PelvisX=0;o.PelvisY=pelvis;o.SpineX=.14f*load+.05f*k+.1f*w.Drop;o.SpineY=w.Torso-pelvis;
            o.ChestX=.08f*load-.13f*k+.08f*w.Drop;o.ChestY=0;o.ChestZ=.1f*load-.17f*k;
            o.HeadY=-.7f*w.Torso*load+.32f*k;o.HeadX=-.05f*load+.1f*k+.15f*w.Drop;
            var tangent=new Vector3(cos,0,-sin);
            o.Body=V(-.035f*load,-(.15f+.022f*front*k)*w.Low-.03f*load-.19f*w.Drop,-.03f*load)-new Vector3(sin,0,cos)*(.045f*k)+tangent*(.06f*lurch);
            o.Pitch=.06f*load+.03f*k+.08f*w.Drop;o.Yaw=0;o.Roll=0;o.Flip=0;o.Air=0;o.Support=1;o.Gait=1;
            o.W=1;
            if(c.Part==KitPart.Recovery){float dur=Math.Max(.01f,c.Until-c.Start),u=time-c.Start;o.W=1-Ease((u-.6f*dur)/(.4f*dur));}
        }
        /// <summary>First person the camera never turns with the spin: the view hands ride an orbit that brings the blade across
        /// the lower view once a turn, right to left, then carries it round below the view (out of sight) back to the right.</summary>
        public static Vector3 ViewHands(float tip){float c=(float)Math.Cos(tip);return V(.36f*(float)Math.Sin(tip),-.34f+.1f*c,.3f+.06f*c);}
        /// <summary>The view hands' share of that orbit: on through the uncoil, held through the spin, off as the pilot settles.</summary>
        public static float ViewOrbit(in KitClock c,float time){var w=Spin(c,time);return c.Part==KitPart.Recovery?1-w.Settle:Smooth(w.Uncoil);}
        public const float ViewCoil=.45f,ViewDip=.1f;
        // First person: through the windup the hand keys turn by a damped share of the coil (the hands stay in view, low at the
        // right); on the orbit the blade turns with the spin as in third person, so from the low hands it lies out ahead along
        // the floor as it crosses the front, enters from the right and leaves to the left, and points down behind the camera
        // for the rest of the turn. The turn is taken modulo whole turns, so every spin and stop meets the guard.
        private static void ViewSweep(in KitClock c,float time,KitPose o)
        {
            var w=Spin(c,time);float m=ViewOrbit(c,time),g=w.Grip,wrapped=MeleeMath.Wrap(g),turn=ViewCoil*wrapped+(wrapped-ViewCoil*wrapped)*m,undo=turn-g;
            var grip=Turn(Turn(o.RGrip,-g),ViewCoil*wrapped);
            o.RGrip=Vector3.Lerp(grip,ViewHands(w.Tip),m);o.RBlade=Turn(o.RBlade,undo)-Vector3.UnitY*(ViewDip*m);o.RUp=Turn(o.RUp,undo);o.RPole=Turn(o.RPole,undo);
            o.LGrip=Turn(o.LGrip,undo);o.LDir=Turn(o.LDir,undo);o.LUp=Turn(o.LUp,undo);o.LPole=Turn(o.LPole,undo);
        }
        /// <summary>
        /// The gait's stance under a Whirlwind (the legs stay the gait's, MeleeGait Widen/Shuffle): the feet set wide through the
        /// windup, mark time once per turn per foot while the blade spins (planted feet never slide), and come back in as the
        /// pilot settles. Zero (the gait as it was) for anything else.
        /// </summary>
        public static void Stance(Actor a,float time,MeleeGait gait)
        {
            if(gait==null)return;gait.Widen=0;gait.Shuffle=0;
            if(!Clock(a,time,out var c)||c.Kind!="whirlwind")return;var w=Spin(c,time);
            gait.Widen=WhirlWiden*(c.Part==KitPart.Recovery?1-w.Settle:w.Low);
            gait.Shuffle=w.Speed>.05f?w.Speed*WhirlRate/Tau:0;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Sampling
        // ---------------------------------------------------------------------------------------------------------------
        public static float Smooth(float x){x=MathUtil.Clamp(x,0,1);return x*x*(3-2*x);}
        public static float Ease(float x){x=MathUtil.Clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);}
        private static float Out(float x){x=MathUtil.Clamp(x,0,1);return 1-(1-x)*(1-x)*(1-x);}
        private static float In(float x){x=MathUtil.Clamp(x,0,1);return x*x;}
        // Scratch poses (main thread only, as the other pose layers).
        private static readonly KitPose s0=new KitPose(),prev=new KitPose(),outPose=new KitPose();

        /// <summary>The layer's key pose at `time` (W the layer weight); false when the pilot casts nothing.</summary>
        public static bool Sample(Actor a,float time,KitPose o)
        {
            if(!Clock(a,time,out var c))return false;Sample(c,time,o);return true;
        }
        public static void Sample(in KitClock c,float time,KitPose o)
        {
            if(c.Kind=="whirlwind")WhirlPose(c,time,o);
            else switch(c.Part)
            {
                case KitPart.Windup:case KitPart.Recast:Lead(c,time,o);break;
                case KitPart.Active:case KitPart.Fall:Strike(c,time,o);break;
                default:Settle(c,time,o);break;
            }
            // The layer eases in from whatever the pilot showed over its first frames (the cast starts from the guard or a run).
            o.W*=Smooth((time-c.At)/EnterSeconds);
        }
        // The windup (telegraph) and the recast anticipation: [At, Release).
        private static void Lead(in KitClock c,float time,KitPose o)
        {
            float wind=c.Windup,u=MathUtil.Clamp((time-c.At)/wind,0,1);
            switch(c.Kind)
            {
                case "flash-step":o.Mix(SwordGuard,FlashChamber,Out(u/.75f));break;
                case "rising-dragon":
                    if(c.Recast)o.Mix(RisingHold,SkyGather,Out(u/.8f));
                    else o.Mix(SwordGuard,RisingCoil,Out(u/.8f));break;
                case "shadow-step":o.Mix(DaggerGuard,ShadowDepart,Out(u/.85f));break;
                case "kunai-mark":
                    if(c.Recast)o.Mix(DaggerGuard,RecastCoil,Out(u/.7f));
                    else o.Mix(DaggerGuard,KunaiCock,Out(u/.8f));break;
                case "bulwark-charge":o.Mix(ShieldGuard,BulwarkBrace,Out(u/.8f));break;
                case "aegis-ricochet":o.Mix(ShieldGuard,AegisCoil,Out(u/.85f));break;
                case "earthsplitter":
                    // Drag (to .55), raise overhead (to .82), chop to the floor on the release.
                    if(u<.55f)o.Mix(GreatGuard,SplitDrag,Out(u/.4f));
                    else if(u<.82f)o.Mix(SplitDrag,SplitRaise,Ease((u-.55f)/.27f));
                    else o.Mix(SplitRaise,SplitSlam,In((u-.82f)/.18f));break;
                default:o.From(GuardOf(c.Spec));break;
            }
            o.W=1;
        }
        // The pose the windup ends on (evaluated at Release), from which active/fall/recovery start.
        private static void LeadEnd(in KitClock c,KitPose o){var e=c;e.Part=c.Recast?KitPart.Recast:KitPart.Windup;e.Start=c.At;Lead(e,c.Release,o);}
        // Active and fall: [Release, phase end).
        private static void Strike(in KitClock c,float time,KitPose o)
        {
            float t=Math.Max(0,time-c.Release);LeadEnd(c,prev);
            switch(c.Kind)
            {
                case "flash-step":
                {
                    // The thrust snaps out over .06 s and holds through the dash; the rear foot trails a little more with distance.
                    o.Mix(prev,FlashThrust,Out(t/.07f));float drag=Smooth(t/.22f);o.RFoot.Z-=.08f*drag;o.Body.Y-=.02f*drag;break;
                }
                case "bulwark-charge":
                {
                    o.Mix(prev,BulwarkRush,Out(t/.08f));
                    // A shield-first sprint: alternating strides at the charge's pace (17.8 m/s, ~5 strides a second), blended in
                    // from the brace's stance.
                    float ph=t*5.2f*Tau,s=(float)Math.Sin(ph),k=Smooth(t/.08f);
                    o.LFoot=Vector3.Lerp(o.LFoot,V(.16f,-.795f+.12f*Math.Max(0,s),.05f+.34f*s),k);o.RFoot=Vector3.Lerp(o.RFoot,V(-.16f,-.795f+.12f*Math.Max(0,-s),.05f-.34f*s),k);
                    o.Body.Y+=.03f*Math.Abs(s)*k;o.ChestZ+=.06f*s*k;break;
                }
                case "rising-dragon":
                {
                    // Skyfall: the caster appears beside the airborne target and plunges (the simulation drives it down at 18 m/s,
                    // so it meets the floor within ~.06 s): the plunge, then the landing crouch it lands in. A miss ends like it.
                    if(t<.07f)o.Mix(prev,SkyPlunge,Out(t/.08f));else{s0.Mix(prev,SkyPlunge,Out(.07f/.08f));o.Mix(s0,SkyLand,Smooth((t-.07f)/.1f));}
                    o.W=Terminal(c,time);return;
                }
                case "kunai-mark":
                {
                    // Recast arrival behind the target: rise and stab down on the release frame's strike.
                    if(t<.05f)o.Mix(prev,RecastRaise,Out(t/.05f));
                    else{s0.Mix(prev,RecastRaise,1);o.Mix(s0,RecastStab,Smooth((t-.05f)/.07f));}
                    o.W=Terminal(c,time);return;
                }
                default:o.From(prev);break;
            }
            o.W=1;
        }
        // The recasts' active phase is their last (it ends at Until with no recovery): the layer fades out over its last .25 s.
        private static float Terminal(in KitClock c,float time)=>1-Ease((time-(c.Until-.25f))/.25f);
        // Recovery: [start, Until); the pose the previous phase ended on, through the ability's follow-through, out to the guard.
        private static void Settle(in KitClock c,float time,KitPose o)
        {
            // The Kunai recast now enters an explicit recovery at its strike (Astra, PR #136): the arrival stab still plays from the
            // release and fades over the cast's last .25 s, exactly as its active phase drew it before.
            if(c.Kind=="kunai-mark"&&c.Recast){var a=c;a.Part=KitPart.Active;a.Start=c.Release;Strike(a,time,o);return;}
            float start=c.Start,dur=Math.Max(.01f,c.Until-start),t=Math.Max(0,time-start);
            // The pose at the recovery's start: the end of the active/fall phase when there was one, else the windup's end.
            bool moved=c.Kind=="flash-step"||c.Kind=="bulwark-charge"||c.Kind=="rising-dragon"&&c.Recast;
            if(moved&&start>c.Release+.0001f){var a=c;a.Part=c.Kind=="rising-dragon"?KitPart.Fall:KitPart.Active;a.Start=c.Release;Strike(a,start,prev);}else LeadEnd(c,prev);
            float hold=.35f,fade;KitPose follow=null;float k=0;
            switch(c.Kind)
            {
                case "flash-step":follow=FlashSettle;k=Out(t/.12f);hold=.2f;break;
                case "bulwark-charge":follow=BulwarkShove;k=Out(t/.07f);hold=.45f;break;
                case "rising-dragon":
                    if(c.Recast){follow=SkyLand;k=Out(t/.06f);hold=.35f;}
                    else
                    {
                        // The rising slash sweeps up over .1 s, then holds high through the recast window before easing out.
                        if(t<.1f){o.Mix(prev,RisingSlash,Out(t/.1f));o.W=1;return;}
                        prev.From(RisingSlash);follow=RisingHold;k=Ease((t-.1f)/.25f);hold=.45f;
                    }
                    break;
                case "shadow-step":
                    // The arrival: the crouch unfolds, the daggers sweep out behind and the pilot rises into the guard.
                    follow=ShadowArrive;k=Out(t/.09f);hold=.25f;break;
                case "kunai-mark":
                    follow=KunaiThrow;k=Out(t/.07f);hold=.3f;break;
                case "aegis-ricochet":follow=AegisThrow;k=Out(t/.09f);hold=.4f;break;
                case "earthsplitter":follow=SplitSlam;k=1;hold=.4f;break;
                default:follow=GuardOf(c.Spec);k=1;break;
            }
            o.Mix(prev,follow,k);
            fade=1-Ease((t-hold*dur)/Math.Max(.01f,dur*(1-hold)));o.W=fade;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Applying a key pose to the rig
        // ---------------------------------------------------------------------------------------------------------------
        private static readonly Quaternion[] saved=new Quaternion[Bones.Length];
        private static readonly int[] LegBones={LHip,LKnee,LFoot,RHip,RKnee,RFoot};
        /// <summary>
        /// The Q/E layer over the pose already sampled (the captured guard, a run, or the first-person view model's idle). Third
        /// person poses the body, torso, legs (planted feet by leg IK on the floor, or the airborne tuck as joints) and both arms;
        /// first person poses only the arms and a damped torso (the view rig shows the forearms and the weapon; the camera is the
        /// body), and never flips. True when the layer owns the pilot.
        /// </summary>
        public static bool Apply(Pose p,Actor a,float time,bool firstPerson)
        {
            if(!Clock(a,time,out var c))return false;Sample(c,time,outPose);
            if(firstPerson&&c.Kind=="whirlwind")ViewSweep(c,time,outPose);
            // First person, the shield rides lower and to the right of the view so a shield-first cast never blinds its caster.
            if(firstPerson&&a.SourceSpec=="shield"){outPose.RGrip+=V(-.1f,-.14f,0);outPose.LGrip+=V(.05f,-.06f,0);}
            Put(p,outPose,firstPerson);return true;
        }
        public static void Put(Pose p,KitPose k,bool firstPerson)
        {
            float w=MathUtil.Clamp(k.W,0,1);if(!(w>0))return;
            Quaternion S(Quaternion from,Quaternion to)=>Quaternion.Normalize(Quaternion.Slerp(from,to,w));
            if(!firstPerson)
            {
                // Over the gait (Gait 1) the body and pelvis turns add to the gait's lean, bob and hip follow, and its feet stay.
                float g=MathUtil.Clamp(k.Gait,0,1);Quaternion body=Euler(k.Pitch,k.Yaw,k.Roll),pelvis=Euler(k.PelvisX,k.PelvisY);
                if(g>0){body=Quaternion.Normalize(Quaternion.Slerp(body,p.Rotation[Body]*body,g));pelvis=Quaternion.Normalize(Quaternion.Slerp(pelvis,p.Rotation[Pelvis]*pelvis,g));HoldLegs(p);}
                p.Position[Body]+=k.Body*w;
                p.Rotation[Body]=Rot(X,k.Flip*w)*S(p.Rotation[Body],body);
                p.Rotation[Pelvis]=S(p.Rotation[Pelvis],pelvis);
                p.Rotation[Spine]=S(p.Rotation[Spine],Euler(k.SpineX,k.SpineY));
                p.Rotation[Chest]=S(p.Rotation[Chest],Euler(k.ChestX,k.ChestY,k.ChestZ));
                p.Rotation[Head]=S(p.Rotation[Head],Euler(k.HeadX,k.HeadY));
                if(g>0)PlantLegs(p,g);
                if(g<1)Legs(p,k,w*(1-g));
            }
            else
            {
                // The view arms follow a damped chest (a full coil would swing them out of the view).
                p.Rotation[Chest]=S(p.Rotation[Chest],Euler(k.ChestX*.45f+k.Pitch*.25f,k.ChestY*.35f,k.ChestZ*.5f));
            }
            Arms(p,k,w);
            p.World();
        }
        private static void Legs(Pose p,KitPose k,float w)
        {
            for(int i=0;i<LegBones.Length;i++)saved[LegBones[i]]=p.Rotation[LegBones[i]];
            float air=MathUtil.Clamp(k.Air,0,1);
            // Planted: leg IK to the floor targets (the ground pass keeps them on the floor after this layer).
            Quaternion lh=saved[LHip],lk=saved[LKnee],lf=saved[LFoot],rh=saved[RHip],rk=saved[RKnee],rf=saved[RFoot];
            if(air<1)
            {
                Leg(p,true,k.LFoot,0);Leg(p,false,k.RFoot,0);
                lh=p.Rotation[LHip];lk=p.Rotation[LKnee];lf=p.Rotation[LFoot];rh=p.Rotation[RHip];rk=p.Rotation[RKnee];rf=p.Rotation[RFoot];
            }
            Quaternion Mix(Quaternion ground,Quaternion airborne)=>air<=0?ground:air>=1?airborne:Quaternion.Normalize(Quaternion.Slerp(ground,airborne,air));
            Quaternion F(int b,Quaternion q)=>Quaternion.Normalize(Quaternion.Slerp(saved[b],q,w));
            p.Rotation[LHip]=F(LHip,Mix(lh,Euler(k.LHipX,0,k.LHipZ)));p.Rotation[LKnee]=F(LKnee,Mix(lk,Euler(k.LKnee)));p.Rotation[LFoot]=F(LFoot,Mix(lf,Euler(.35f*air)));
            p.Rotation[RHip]=F(RHip,Mix(rh,Euler(k.RHipX,0,k.RHipZ)));p.Rotation[RKnee]=F(RKnee,Mix(rk,Euler(k.RKnee)));p.Rotation[RFoot]=F(RFoot,Mix(rf,Euler(.35f*air)));
        }
        // Over the gait the ankles stay where the gait planted them (and its foot roll) while the layer lowers, shifts and turns
        // the hips: each leg is re-solved from its moved hip onto its held ankle, the knee bending toward where it was.
        private static readonly Vector3[] heldAnkle=new Vector3[2],heldKnee=new Vector3[2];private static readonly Quaternion[] heldFoot=new Quaternion[2];
        private static void HoldLegs(Pose p)
        {
            p.World();
            for(int i=0;i<2;i++){int hip=i==0?LHip:RHip;heldAnkle[i]=p.WorldPosition[hip+2];heldKnee[i]=p.WorldPosition[hip+1];heldFoot[i]=p.WorldRotation[hip+2];}
        }
        private static void PlantLegs(Pose p,float weight)
        {
            p.World();
            for(int i=0;i<2;i++)
            {
                int hip=i==0?LHip:RHip,knee=hip+1,foot=hip+2;Quaternion h0=p.Rotation[hip],k0=p.Rotation[knee],f0=p.Rotation[foot];
                Vector3 h=p.WorldPosition[hip],dir=heldAnkle[i]-h;float d=dir.Length();if(!(d>1e-6f))continue;dir/=d;d=MathUtil.Clamp(d,.1f,.497f);
                Vector3 pole=heldKnee[i]-h,perp=pole-dir*Vector3.Dot(pole,dir);
                if(perp.LengthSquared()<1e-8f){perp=Vector3.Transform(Z,p.WorldRotation[Pelvis]);perp-=dir*Vector3.Dot(perp,dir);}
                if(perp.LengthSquared()<1e-10f)continue;perp=Vector3.Normalize(perp);
                Vector3 kn=h+dir*(d*.5f)+perp*(float)Math.Sqrt(Math.Max(0,.0625f-d*d*.25f));
                Vector3 t=Vector3.Normalize(kn-h),y=-t,z=Vector3.Normalize(perp-t*Vector3.Dot(perp,t)),x=Vector3.Cross(y,z);Quaternion qh=Basis(x,y,z);
                Quaternion hq=Quaternion.Normalize(Quaternion.Inverse(p.WorldRotation[Pelvis])*qh);
                Vector3 sh=Vector3.Transform(h+dir*d-kn,Quaternion.Inverse(qh));Quaternion kq=Rot(X,(float)Math.Atan2(-sh.Z,-sh.Y));
                Quaternion fq=Quaternion.Normalize(Quaternion.Inverse(qh*kq)*heldFoot[i]);
                if(weight<1){hq=Quaternion.Slerp(h0,hq,weight);kq=Quaternion.Slerp(k0,kq,weight);fq=Quaternion.Slerp(f0,fq,weight);}
                p.Rotation[hip]=hq;p.Rotation[knee]=kq;p.Rotation[foot]=fq;
            }
            p.World();
        }
        /// <summary>The layer rides the gait at `time` (the Whirlwind): it re-lays the gait's own planted ankles, which the
        /// ground pass then keeps as layered legs (MeleeRig.LegsLayered).</summary>
        public static bool RidesGait(Actor a,float time)=>Clock(a,time,out var c)&&c.Kind=="whirlwind";
        private static void Arms(Pose p,KitPose k,float w)
        {
            for(int b=LArm;b<=RHand;b++)saved[b]=p.Rotation[b];
            // Grips are authored in the pilot's facing frame about the chest (so a coiled torso keeps the weapon on its line);
            // the arm solve takes them in the chest's frame.
            p.World();var toChest=Quaternion.Inverse(p.WorldRotation[Chest]);
            var right=toChest*Look(Unit(k.RBlade,Z),Unit(k.RUp,Y));Arm(p,false,Vector3.Transform(k.RGrip,toChest),right,Vector3.Transform(k.RPole,toChest));
            Vector3 left=Vector3.Transform(k.LGrip,toChest);var leftQ=toChest*Look(Unit(k.LDir,Z),Unit(k.LUp,Y));
            if(k.Support>0)
            {
                // The support hand on the reached grip, .11 down the handle (the greatsword socket, CapturedRig.Sample).
                p.World();var chest=Quaternion.Inverse(p.WorldRotation[Chest]);var reached=Vector3.Transform(p.WorldPosition[RHand]-p.WorldPosition[Chest],chest);
                var held=Quaternion.Normalize(chest*p.WorldRotation[RHand]);float s=MathUtil.Clamp(k.Support,0,1);
                left=Vector3.Lerp(left,reached+Vector3.Transform(V(0,0,-.11f),held),s);leftQ=Quaternion.Normalize(Quaternion.Slerp(leftQ,held,s));
            }
            Arm(p,true,left,leftQ,Vector3.Transform(k.LPole,toChest));
            for(int b=LArm;b<=RHand;b++)if(b!=RClav&&b!=LClav)p.Rotation[b]=Quaternion.Normalize(Quaternion.Slerp(saved[b],p.Rotation[b],w));
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Blade trails: the strike windows during which the blade sweeps (NativeWeaponDressing.Sweeping reads this).
        // ---------------------------------------------------------------------------------------------------------------
        /// <summary>True while a kit strike swings its blade: the Flash Step thrust, the Rising Dragon slash, the Skyfall plunge,
        /// the recast stab, the Earthsplitter chop and the Whirlwind's spin.</summary>
        public static bool Sweeps(Actor a,float time)
        {
            if(!Clock(a,time,out var c))return false;float r=c.Release;
            switch(c.Kind)
            {
                case "flash-step":return time>=r-.02f&&time<r+.3f;
                case "rising-dragon":return c.Recast?c.Part==KitPart.Fall||c.Part==KitPart.Active||c.Part==KitPart.Recovery&&time<c.Start+.12f:c.Part==KitPart.Recovery&&time<r+.16f;
                case "kunai-mark":return c.Recast&&c.Part==KitPart.Active&&time<r+.16f;
                case "earthsplitter":return time>=r-.1f&&time<r+.06f;
                case "whirlwind":return c.Part==KitPart.Active||Spin(c,time).Speed>(c.Part==KitPart.Recovery?.25f:.5f);
                default:return false;
            }
        }
    }
}
