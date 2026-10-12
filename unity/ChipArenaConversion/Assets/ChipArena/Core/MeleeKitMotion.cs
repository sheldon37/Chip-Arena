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
    /// movement (flash-step/bulwark-charge: Until - .2; Wolf Knight and Skyfall: their landing).
    /// </summary>
    public struct KitClock
    {
        public string Kind,Spec;public KitPart Part;public bool Recast;
        public float At,Release,Until,Start,Rate,Landed;
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
        public KitPose Copy()=>(KitPose)MemberwiseClone();
        public void From(KitPose o)
        {
            W=o.W;Body=o.Body;Pitch=o.Pitch;Yaw=o.Yaw;Roll=o.Roll;Flip=o.Flip;PelvisX=o.PelvisX;PelvisY=o.PelvisY;SpineX=o.SpineX;SpineY=o.SpineY;ChestX=o.ChestX;ChestY=o.ChestY;ChestZ=o.ChestZ;HeadX=o.HeadX;HeadY=o.HeadY;
            LFoot=o.LFoot;RFoot=o.RFoot;Air=o.Air;LHipX=o.LHipX;LHipZ=o.LHipZ;LKnee=o.LKnee;RHipX=o.RHipX;RHipZ=o.RHipZ;RKnee=o.RKnee;
            RGrip=o.RGrip;RBlade=o.RBlade;RUp=o.RUp;RPole=o.RPole;LGrip=o.LGrip;LDir=o.LDir;LUp=o.LUp;LPole=o.LPole;Support=o.Support;
        }
        /// <summary>this = a + (b - a) t (every field, the weight too).</summary>
        public KitPose Mix(KitPose a,KitPose b,float t)
        {
            float L(float x,float y)=>x+(y-x)*t;Vector3 V(Vector3 x,Vector3 y)=>x+(y-x)*t;
            W=L(a.W,b.W);Body=V(a.Body,b.Body);Pitch=L(a.Pitch,b.Pitch);Yaw=L(a.Yaw,b.Yaw);Roll=L(a.Roll,b.Roll);Flip=L(a.Flip,b.Flip);
            PelvisX=L(a.PelvisX,b.PelvisX);PelvisY=L(a.PelvisY,b.PelvisY);SpineX=L(a.SpineX,b.SpineX);SpineY=L(a.SpineY,b.SpineY);ChestX=L(a.ChestX,b.ChestX);ChestY=L(a.ChestY,b.ChestY);ChestZ=L(a.ChestZ,b.ChestZ);HeadX=L(a.HeadX,b.HeadX);HeadY=L(a.HeadY,b.HeadY);
            LFoot=V(a.LFoot,b.LFoot);RFoot=V(a.RFoot,b.RFoot);Air=L(a.Air,b.Air);LHipX=L(a.LHipX,b.LHipX);LHipZ=L(a.LHipZ,b.LHipZ);LKnee=L(a.LKnee,b.LKnee);RHipX=L(a.RHipX,b.RHipX);RHipZ=L(a.RHipZ,b.RHipZ);RKnee=L(a.RKnee,b.RKnee);
            RGrip=V(a.RGrip,b.RGrip);RBlade=V(a.RBlade,b.RBlade);RUp=V(a.RUp,b.RUp);RPole=V(a.RPole,b.RPole);LGrip=V(a.LGrip,b.LGrip);LDir=V(a.LDir,b.LDir);LUp=V(a.LUp,b.LUp);LPole=V(a.LPole,b.LPole);Support=L(a.Support,b.Support);
            return this;
        }
        /// <summary>The largest field difference to another pose (radians or metres; the continuity checks read it).</summary>
        public float Distance(KitPose o)
        {
            float d=0;void F(float x,float y){d=Math.Max(d,Math.Abs(x-y));}void V(Vector3 x,Vector3 y){d=Math.Max(d,(x-y).Length());}
            F(W,o.W);V(Body,o.Body);F(Pitch,o.Pitch);F(Yaw,o.Yaw);F(Roll,o.Roll);F(Flip,o.Flip);F(PelvisX,o.PelvisX);F(PelvisY,o.PelvisY);F(SpineX,o.SpineX);F(SpineY,o.SpineY);F(ChestX,o.ChestX);F(ChestY,o.ChestY);F(ChestZ,o.ChestZ);F(HeadX,o.HeadX);F(HeadY,o.HeadY);
            V(LFoot,o.LFoot);V(RFoot,o.RFoot);F(Air,o.Air);F(LHipX,o.LHipX);F(LHipZ,o.LHipZ);F(LKnee,o.LKnee);F(RHipX,o.RHipX);F(RHipZ,o.RHipZ);F(RKnee,o.RKnee);
            V(RGrip,o.RGrip);V(RBlade,o.RBlade);V(RUp,o.RUp);V(LGrip,o.LGrip);V(LDir,o.LDir);V(LUp,o.LUp);F(Support,o.Support);
            return d;
        }
    }

    /// <summary>
    /// Pilot animation for the melee Q/E kit (Astra's simulation, docs/MELEE-SIMULATION-HANDOFF.md; Claude's presentation): one
    /// authored pose layer per ability and phase over the captured guard, third person (CapturedAnimation's melee overlay) and
    /// the first-person arms (the generic view-model path), driven only by the replicated MeleeKit Kind/Phase and timestamps
    /// (KitClock). Every key track starts from the pose the previous phase ended on, evaluated at the replicated phase start,
    /// so the layer is continuous across the windup/active/recovery/recast/fall boundaries (Tests/MeleeKitPresentationChecks). The body is
    /// posed around where the simulation puts it: offsets stay within the rig (crouches, leans, the Wolf Knight's flip about the
    /// waist); there is no root motion. Presentation only: hit timing, hitboxes, damage, cooldowns, movement and networking are
    /// the authority's (nothing here is read back).
    ///  - Flash Step: a chambered thrust in a low lunge (the telegraph), the extended dash-thrust, the blade returning.
    ///  - Rising Dragon: a deep coil with the blade trailing low behind, the rising slash ending overhead (the launch), held for
    ///    the recast window. Skyfall (recast): a gathering crouch with the blade raised, an airborne plunge (fall), the landing.
    /// </summary>
    public static class MeleeKitMotion
    {
        private const float Tau=(float)(Math.PI*2);
        public static readonly string[] Kinds=
        {
            "flash-step","rising-dragon",
        };
        public static bool Known(string kind){if(kind==null)return false;foreach(var k in Kinds)if(k==kind)return true;return false;}
        public static string Spec(string kind)=>kind=="flash-step"||kind=="rising-dragon"?"sword":kind=="shadow-step"||kind=="kunai-mark"?"daggers":kind=="bulwark-charge"||kind=="aegis-ricochet"?"shield":kind=="earthsplitter"||kind=="wolf-knight"?"greatsword":null;
        /// <summary>The fixed recovery tails the simulation appends (flash-step/bulwark after the dash ends, Skyfall after landing).</summary>
        public const float DashRecovery=.2f,SkyfallRecovery=.25f,WolfRecovery=.55f,EnterSeconds=.06f;

        /// <summary>The cast the pilot is in at `time`, from replicated fields only; false without one (or once it ended).</summary>
        public static bool Clock(Actor a,float time,out KitClock c)
        {
            c=default;var k=a?.MeleeKit;if(k==null||!Known(k.Kind)||string.IsNullOrEmpty(k.Phase)||!MathUtil.Finite(time))return false;
            if(time<k.At-.0001f||time>=k.Until)return false;
            c.Kind=k.Kind;c.Spec=Spec(k.Kind);c.At=k.At;c.Release=Math.Max(k.Release,k.At+.01f);c.Until=k.Until;c.Rate=a.CastRate>0?a.CastRate:1;c.Landed=a.SpecialLanded;
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
            else if(c.Kind=="wolf-knight")s=c.Landed>=c.Release?c.Landed:c.Until-WolfRecovery/c.Rate;
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
            switch(c.Part)
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
                case "rising-dragon":
                {
                    // Skyfall: the caster appears beside the airborne target and plunges (the simulation drives it down at 18 m/s,
                    // so it meets the floor within ~.06 s): the plunge, then the landing crouch it lands in. A miss ends like it.
                    if(t<.07f)o.Mix(prev,SkyPlunge,Out(t/.08f));else{s0.Mix(prev,SkyPlunge,Out(.07f/.08f));o.Mix(s0,SkyLand,Smooth((t-.07f)/.1f));}
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
            float start=c.Start,dur=Math.Max(.01f,c.Until-start),t=Math.Max(0,time-start);
            // The pose at the recovery's start: the end of the active/fall phase when there was one, else the windup's end.
            bool moved=c.Kind=="flash-step"||c.Kind=="bulwark-charge"||c.Kind=="wolf-knight"||c.Kind=="rising-dragon"&&c.Recast;
            if(moved&&start>c.Release+.0001f){var a=c;a.Part=c.Kind=="rising-dragon"?KitPart.Fall:KitPart.Active;a.Start=c.Release;Strike(a,start,prev);}else LeadEnd(c,prev);
            float hold=.35f,fade;KitPose follow=null;float k=0;
            switch(c.Kind)
            {
                case "flash-step":follow=FlashSettle;k=Out(t/.12f);hold=.2f;break;
                case "rising-dragon":
                    if(c.Recast){follow=SkyLand;k=Out(t/.06f);hold=.35f;}
                    else
                    {
                        // The rising slash sweeps up over .1 s, then holds high through the recast window before easing out.
                        if(t<.1f){o.Mix(prev,RisingSlash,Out(t/.1f));o.W=1;return;}
                        prev.From(RisingSlash);follow=RisingHold;k=Ease((t-.1f)/.25f);hold=.45f;
                    }
                    break;
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
            if(!Sample(a,time,outPose))return false;
            Put(p,outPose,firstPerson);return true;
        }
        public static void Put(Pose p,KitPose k,bool firstPerson)
        {
            float w=MathUtil.Clamp(k.W,0,1);if(!(w>0))return;
            Quaternion S(Quaternion from,Quaternion to)=>Quaternion.Normalize(Quaternion.Slerp(from,to,w));
            if(!firstPerson)
            {
                p.Position[Body]+=k.Body*w;
                p.Rotation[Body]=Rot(X,k.Flip*w)*S(p.Rotation[Body],Euler(k.Pitch,k.Yaw,k.Roll));
                p.Rotation[Pelvis]=S(p.Rotation[Pelvis],Euler(k.PelvisX,k.PelvisY));
                p.Rotation[Spine]=S(p.Rotation[Spine],Euler(k.SpineX,k.SpineY));
                p.Rotation[Chest]=S(p.Rotation[Chest],Euler(k.ChestX,k.ChestY,k.ChestZ));
                p.Rotation[Head]=S(p.Rotation[Head],Euler(k.HeadX,k.HeadY));
                Legs(p,k,w);
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
        /// the recast stab, the Earthsplitter chop and the Wolf Knight's flight and landing.</summary>
        public static bool Sweeps(Actor a,float time)
        {
            if(!Clock(a,time,out var c))return false;float r=c.Release;
            switch(c.Kind)
            {
                case "flash-step":return time>=r-.02f&&time<r+.3f;
                case "rising-dragon":return c.Recast?c.Part==KitPart.Fall||c.Part==KitPart.Active||c.Part==KitPart.Recovery&&time<c.Start+.12f:c.Part==KitPart.Recovery&&time<r+.16f;
                default:return false;
            }
        }
    }
}
